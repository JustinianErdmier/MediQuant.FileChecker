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
    ///     The report that was written for a completed, stopped or mid-run failed audit; a <c>Sql.QueryFailed</c> error when SQL Server failed before any row was processed;
    ///     <c>Audit.ReadFailed</c> when the row stream failed for another reason before any row was processed; <see cref="AuditErrors.StoppedBeforeChecking" /> when stopped while
    ///     counting; or <c>Report.Unwritable</c> when the report could not be written.
    /// </returns>
    public static async Task<ErrorOr<AuditReport>> RunAsync(AuditRequest request, AuditProgress progress, CancellationToken cancellationToken)
    {
        AppSettings settings   = request.Settings;
        DateTime    startedUtc = DateTime.UtcNow;

        ErrorOr<AuditQuery> built = QueryBuilder.Build(settings.Table, settings.PathColumn, settings.IdColumn);

        if (built.IsError)
        {
            return built.Errors;
        }

        AuditQuery query            = built.Value;
        string     connectionString = ConnectionStrings.Build(settings.Server, settings.Database);

        progress.Phase = "Counting files...";

        ErrorOr<RowCounts> counted;

        try
        {
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

        long total = counted.Value.Distinct;

        progress.SetTotal(total);
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

                                            if (result.Outcome is not (CheckOutcomes.Found or CheckOutcomes.Skipped))
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
        long             skippedCount   = progress.Skipped;
        AuditResultKinds result         = AuditResultKinds.Completed;
        string?          failureMessage = null;

        if (stopped)
        {
            result = AuditResultKinds.StoppedByUser;
        }
        else if (readResult.IsError)
        {
            if (checkedCount + skippedCount is 0)
            {
                return readResult.Errors;
            }

            result         = AuditResultKinds.Failed;
            failureMessage = readResult.FirstError.Description;
        }

        progress.Phase = "Writing report...";

        // The table may have grown since it was counted; the report's arithmetic must still add up.
        AuditReport report = new(settings,
                                 startedUtc,
                                 DateTime.UtcNow,
                                 result,
                                 failureMessage,
                                 Math.Max(total, checkedCount + skippedCount),
                                 checkedCount - problems.Count,
                                 skippedCount,
                                 problems.ToArray());

        ErrorOr<Success> written = ReportWriter.Write(request.ReportPath.FullPath, ReportBuilder.Build(report));

        if (written.IsError)
        {
            return written.Errors;
        }

        return report;
    }
}
