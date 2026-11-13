namespace FileAuditor.Services.Auditing;

/// <summary>Runs one audit: counts the rows, streams them through a bounded channel to parallel file checks, then builds and writes the report.</summary>
public static class AuditRunner
{
    /// <summary>How many index rows can wait between the reader and the file checks before the reader pauses.</summary>
    private const int ChannelCapacity = 10_000;

    /// <summary>The most file checks that run at the same time.</summary>
    private const int MaxParallelism = 32;

    /// <summary>Runs the audit.</summary>
    /// <param name="request">The settings and report path.</param>
    /// <param name="progress">Receives the live statistics.</param>
    /// <param name="cancellationToken">Stops the run; whatever was checked is reported.</param>
    /// <returns>
    ///     The report that was written for a completed, stopped or mid-run failed audit; <c>Sql.PathColumnNotFound</c> or <c>Sql.PathColumnUnsupported</c> when the path column is
    ///     missing or is neither <c>nvarchar</c> nor <c>varchar</c> (checked before any row is read); a <c>Sql.QueryFailed</c> error when SQL Server failed before any row was processed;
    ///     <c>Audit.ReadFailed</c> when the row stream failed for another reason before any row was processed; <see cref="AuditErrors.StoppedBeforeChecking" /> when stopped while
    ///     counting; or <c>Report.Unwritable</c> when the report could not be written.
    /// </returns>
    public static async Task<ErrorOr<AuditReport>> RunAsync(AuditRequest request, AuditProgress progress, CancellationToken cancellationToken)
    {
        AppSettings settings   = request.Settings;
        DateTime    startedUtc = DateTime.UtcNow;

        ErrorOr<(string QuotedTable, string QuotedPathColumn)> target = QueryBuilder.QuoteTarget(settings.Table, settings.PathColumn);

        if (target.IsError)
        {
            return target.Errors;
        }

        string connectionString = ConnectionStrings.Build(settings.Server, settings.Database);

        progress.Phase = "Counting files...";

        AuditQuery         query;
        ErrorOr<RowCounts> counted;

        try
        {
            ErrorOr<(string TypeName, short MaxLength)?> columnType = await IndexTableReader.ReadPathColumnTypeAsync(connectionString,
                                                                                                                      target.Value.QuotedTable,
                                                                                                                      target.Value.QuotedPathColumn,
                                                                                                                      cancellationToken);

            if (columnType.IsError)
            {
                return columnType.Errors;
            }

            ErrorOr<string?> convertTo = PathColumnType.Check(settings.Table, settings.PathColumn, columnType.Value);

            if (convertTo.IsError)
            {
                return convertTo.Errors;
            }

            ErrorOr<AuditQuery> built = QueryBuilder.Build(settings.Table, settings.PathColumn, settings.IdColumn, convertTo.Value);

            if (built.IsError)
            {
                return built.Errors;
            }

            query = built.Value;

            counted = await IndexTableReader.CountRowsAsync(connectionString, query, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return AuditErrors.StoppedBeforeChecking;
        }

        if (counted.IsError)
        {
            return counted.Errors;
        }

        RowCounts counts = counted.Value;

        progress.SetCounts(counts);
        progress.Phase = "Checking files...";

        FileChecker checker = new(settings.ReportEmptyFiles);

        ConcurrentBag<CheckResult> problems = [];

        Channel<IndexRow> channel = Channel.CreateBounded<IndexRow>(new BoundedChannelOptions(ChannelCapacity)
        {
            SingleWriter = true,
            FullMode     = BoundedChannelFullMode.Wait
        });

        // Cancelled by the caller's token, and by this method when the consumer dies, so the reader is never left blocked on a full channel.
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        ErrorOr<Success> readResult = Result.Success;

        Task producer = Task.Run(async () =>
                                 {
                                     try
                                     {
                                         // ReSharper disable AccessToDisposedClosure
                                         readResult = await IndexTableReader.ReadAsync(connectionString,
                                                                                       query,
                                                                                       row => channel.Writer.WriteAsync(row, linkedSource.Token),
                                                                                       linkedSource.Token);

                                         // ReSharper restore AccessToDisposedClosure
                                     }
                                     finally
                                     {
                                         channel.Writer.TryComplete();
                                     }
                                 },
                                 CancellationToken.None);

        bool stopped = false;

        try
        {
            await Parallel.ForEachAsync(channel.Reader.ReadAllAsync(linkedSource.Token),
                                        new ParallelOptions
                                        {
                                            MaxDegreeOfParallelism = MaxParallelism,
                                            CancellationToken      = linkedSource.Token
                                        },
                                        (row, _) =>
                                        {
                                            CheckResult result = checker.Check(row);

                                            // The database classified this row as a real path, so a Skipped outcome here means the two skip tests disagree (the checker folds
                                            // some non-ASCII letters that SQL does not). List it with the other errors rather than counting it nowhere.
                                            if (result.Outcome is CheckOutcomes.Skipped)
                                            {
                                                result = result with
                                                         {
                                                             Outcome       = CheckOutcomes.UnverifiedOther,
                                                             Reason        = "Skip marker not recognised by the database",
                                                             SystemMessage = "The path looks like a skip marker to the checker but not to the database, so it was not skipped."
                                                         };
                                            }

                                            if (result.Outcome is not CheckOutcomes.Found)
                                            {
                                                problems.Add(result);
                                            }

                                            progress.Record(result.Outcome);

                                            return ValueTask.CompletedTask;
                                        });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopped = true;
        }
        catch
        {
            await linkedSource.CancelAsync();

            await Task.WhenAny(producer);

            throw;
        }

        try
        {
            await producer;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopped = true;
        }
        catch (Exception exception)
        {
            // Anything but a user stop, or a SQL error: report it like a mid-run SQL failure, keeping what was checked.
            readResult = AuditErrors.ReadFailed(exception.Message);
        }

        long             checkedCount   = progress.Checked;
        AuditResultKinds result         = AuditResultKinds.Completed;
        string?          failureMessage = null;

        if (stopped)
        {
            result = AuditResultKinds.StoppedByUser;
        }
        else if (readResult.IsError)
        {
            if (checkedCount is 0)
            {
                return readResult.Errors;
            }

            result         = AuditResultKinds.Failed;
            failureMessage = readResult.FirstError.Description;
        }

        IReadOnlyList<string>? emptyPathIds  = null;
        string?                emptyPathNote = null;

        if (counts.EmptyPaths > 0)
        {
            if (query.EmptyIdsSql is null)
            {
                emptyPathNote = "Missing filepaths not listed because no ID column was specified";
            }
            else if (result is AuditResultKinds.StoppedByUser)
            {
                emptyPathNote = "Missing filepaths not listed because the audit was stopped";
            }
            else if (result is AuditResultKinds.Failed)
            {
                emptyPathNote = "Missing filepaths not listed because the audit failed";
            }
            else
            {
                progress.Phase = "Reading empty paths...";

                try
                {
                    ErrorOr<IReadOnlyList<string>> read = await IndexTableReader.ReadEmptyIdsAsync(connectionString, query, cancellationToken);

                    if (read.IsError)
                    {
                        emptyPathNote = $"Missing filepaths not listed because they could not be read: {read.FirstError.Description}";
                    }
                    else
                    {
                        emptyPathIds = read.Value;
                    }
                }
                catch (OperationCanceledException)
                {
                    emptyPathNote = "Missing filepaths not listed because the audit was stopped";
                }
                catch (Exception exception)
                {
                    // Anything but a user stop or a SQL error: the audit is done, so keep its report and say why the IDs are missing.
                    emptyPathNote = $"Missing filepaths not listed because they could not be read: {exception.Message}";
                }
            }
        }

        progress.Phase = "Writing report...";

        AuditReport report = new(settings,
                                 startedUtc,
                                 DateTime.UtcNow,
                                 result,
                                 failureMessage,
                                 counts,
                                 progress.Found,
                                 problems.ToArray(),
                                 emptyPathIds,
                                 emptyPathNote);

        ErrorOr<Success> written = ReportWriter.Write(request.ReportPath.FullPath, ReportBuilder.Build(report));

        if (written.IsError)
        {
            return written.Errors;
        }

        return report;
    }
}
