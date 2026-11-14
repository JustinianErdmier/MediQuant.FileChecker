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
    ///     missing or is neither <c>nvarchar</c> nor <c>varchar</c> (checked before any row is read); a <c>Sql.QueryFailed</c> error when SQL Server failed before any row
    ///     was processed; <c>Audit.ReadFailed</c> when the row stream failed for another reason before any row was processed; <see cref="AuditErrors.StoppedBeforeChecking" />
    ///     when stopped while counting; or <c>Report.Unwritable</c> when the report could not be written.
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

        AuditQuery   query;
        ErrorOr<long> counted;

        try
        {
            // Pre-flight's read, when it made one, so an unreachable server is not waited for twice.
            ErrorOr<(string TypeName, short MaxLength)?> columnType = request.PathColumnType
                                                                      ?? await IndexTableReader.ReadPathColumnTypeAsync(connectionString,
                                                                                                                        target.Value.QuotedTable,
                                                                                                                        target.Value.QuotedPathColumn,
                                                                                                                        cancellationToken);

            if (columnType.IsError)
            {
                return columnType.Errors;
            }

            // The type check only; the Missing Files audit converts nothing.
            ErrorOr<string?> typeCheck = PathColumnType.Check(settings.Table, settings.PathColumn, columnType.Value);

            if (typeCheck.IsError)
            {
                return typeCheck.Errors;
            }

            ErrorOr<AuditQuery> built = QueryBuilder.Build(settings.Table, settings.PathColumn, settings.IdColumn);

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

        progress.SetTotal(counted.Value);
        progress.Phase = "Checking files...";

        FileChecker checker = new(settings.ReportEmptyFiles);

        ConcurrentBag<CheckResult> problems = [];

        // Written by the reader only (one thread); read after the reader has finished.
        PathKeySet   seen         = new();
        List<string> emptyPathIds = [];

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
                                                                                       row => Classify(row, query.HasIdColumn, seen, emptyPathIds, progress, channel.Writer, linkedSource.Token),
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
                                            // The reader has already excluded skip markers with the same SkipMarker test the checker uses, so a Skipped outcome cannot arrive.
                                            CheckResult result = checker.Check(row);

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

        AuditResultKinds result         = AuditResultKinds.Completed;
        string?          failureMessage = null;

        if (stopped)
        {
            result = AuditResultKinds.StoppedByUser;
        }
        else if (readResult.IsError)
        {
            // Nothing read before the failure: report it as a plain error rather than as an empty run.
            if (progress.Distinct is 0
                && progress.RowsHandled is 0)
            {
                return readResult.Errors;
            }

            result         = AuditResultKinds.Failed;
            failureMessage = readResult.FirstError.Description;
        }

        progress.Phase = "Writing report...";

        // The reader and the checks have all ended, so the counters are final and agree with one another.
        RowCounts counts = new(progress.TotalRows, progress.Distinct, progress.Duplicates, progress.Skipped, progress.EmptyPaths);

        AuditReport report = new(settings,
                                 startedUtc,
                                 DateTime.UtcNow,
                                 result,
                                 failureMessage,
                                 counts,
                                 progress.Found,
                                 problems.ToArray(),
                                 query.HasIdColumn ? emptyPathIds : null);

        ErrorOr<Success> written = ReportWriter.Write(request.ReportPath.FullPath, ReportBuilder.Build(report));

        if (written.IsError)
        {
            return written.Errors;
        }

        return report;
    }

    /// <summary>Classifies one row as it is read and passes a new real path on to be checked.</summary>
    /// <remarks>
    ///     In this order: a skip marker is counted as Skipped; an empty path is counted (and its ID kept when there is an ID column); any other path is looked up in
    ///     <paramref name="seen" />, where a key seen before is a Duplicate and a new key is Distinct and written to the channel. Only the reader calls this, one row at a time.
    /// </remarks>
    /// <param name="row">The row as read.</param>
    /// <param name="hasIdColumn">Whether the audit has an ID column.</param>
    /// <param name="seen">The keys of the real paths seen so far.</param>
    /// <param name="emptyPathIds">Receives the ID of each empty-path row.</param>
    /// <param name="progress">Receives the counts.</param>
    /// <param name="writer">The channel that feeds the file checks.</param>
    /// <param name="cancellationToken">Stops the wait for room in the channel.</param>
    /// <returns>A task that ends when the row has been handled, which for a new real path is when it has a place in the channel.</returns>
    private static ValueTask Classify(IndexRow row, bool hasIdColumn, PathKeySet seen, List<string> emptyPathIds, AuditProgress progress, ChannelWriter<IndexRow> writer, CancellationToken cancellationToken)
    {
        if (SkipMarker.IsSkipped(row.Path))
        {
            progress.AddSkipped();

            return ValueTask.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(row.Path))
        {
            if (hasIdColumn)
            {
                emptyPathIds.Add(row.Id ?? string.Empty);
            }

            progress.AddEmptyPath();

            return ValueTask.CompletedTask;
        }

        if (!seen.Add(row.Path, row.Id, hasIdColumn))
        {
            progress.AddDuplicate();

            return ValueTask.CompletedTask;
        }

        progress.AddDistinct();

        return writer.WriteAsync(row, cancellationToken);
    }
}
