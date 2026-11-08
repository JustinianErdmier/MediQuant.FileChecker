namespace FileAuditor.Services.Auditing;

/// <summary>Runs one audit: counts the rows, streams them through a bounded channel to parallel file checks, then builds and writes the report (SDD §6.4).</summary>
public sealed class AuditRunner
{
    private const int ChannelCapacity = 10_000;

    private const int MaxParallelism = 32;

    private readonly IndexTableReader _reader = new();

    /// <summary>Runs the audit.</summary>
    /// <param name="request">The settings and report path.</param>
    /// <param name="progress">Receives the live statistics.</param>
    /// <param name="ct">Stops the run; whatever was checked is reported.</param>
    /// <returns>
    ///     The report that was written for a completed, stopped or mid-run failed audit; a <c>Sql.QueryFailed</c> error when SQL Server failed before any row was checked;
    ///     <c>Audit.ReadFailed</c> when the row stream failed for another reason before any row was checked; <see cref="AuditErrors.StoppedBeforeChecking" /> when stopped while counting;
    ///     or <c>Report.Unwritable</c> when the report could not be written.
    /// </returns>
    public async Task<ErrorOr<AuditReport>> RunAsync(AuditRequest request, AuditProgress progress, CancellationToken ct)
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

        ErrorOr<long> counted;

        try
        {
            counted = await _reader.CountAsync(connectionString, query, ct);
        }
        catch (OperationCanceledException)
        {
            return AuditErrors.StoppedBeforeChecking;
        }

        if (counted.IsError)
        {
            return counted.Errors;
        }

        long total = counted.Value;

        progress.SetTotal(total);
        progress.Phase = "Checking files...";

        FileChecker checker = new(settings.ReportEmptyFiles);

        ConcurrentBag<CheckResult> problems =
            [];

        Channel<IndexRow> channel = Channel.CreateBounded<IndexRow>(new BoundedChannelOptions(ChannelCapacity)
        {
            SingleWriter = true,
            FullMode     = BoundedChannelFullMode.Wait
        });

        // Cancelled by the caller's token, and by this method when the consumer dies so the reader is never left blocked on a full channel.
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        ErrorOr<Success> readResult = Result.Success;

        Task producer = Task.Run(async () =>
                                 {
                                     try
                                     {
                                         readResult = await _reader.ReadAsync(connectionString, query, row => channel.Writer.WriteAsync(row, linked.Token), linked.Token);
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
            await Parallel.ForEachAsync(channel.Reader.ReadAllAsync(linked.Token),
                                        new ParallelOptions
                                        {
                                            MaxDegreeOfParallelism = MaxParallelism,
                                            CancellationToken      = linked.Token
                                        },
                                        (row, _) =>
                                        {
                                            CheckResult result = checker.Check(row);

                                            if (result.Outcome is not (CheckOutcome.Found or CheckOutcome.Skipped))
                                            {
                                                problems.Add(result);
                                            }

                                            progress.Record(result.Outcome);

                                            return ValueTask.CompletedTask;
                                        });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            stopped = true;
        }
        catch
        {
            await linked.CancelAsync();

            await Task.WhenAny(producer);

            throw;
        }

        try
        {
            await producer;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            stopped = true;
        }
        catch (Exception ex)
        {
            // Anything but a user stop or a SQL error: report it like a mid-run SQL failure, keeping what was checked.
            readResult = AuditErrors.ReadFailed(ex.Message);
        }

        long            checkedCount   = progress.Checked;
        long            skippedCount   = progress.Skipped;
        AuditResultKind kind           = AuditResultKind.Completed;
        string?         failureMessage = null;

        if (stopped)
        {
            kind = AuditResultKind.StoppedByUser;
        }
        else if (readResult.IsError)
        {
            if (checkedCount == 0)
            {
                return readResult.Errors;
            }

            kind           = AuditResultKind.Failed;
            failureMessage = readResult.FirstError.Description;
        }

        progress.Phase = "Writing report...";

        // The table may have grown since it was counted; the report's arithmetic must still add up.
        AuditReport report = new(settings,
                                 startedUtc,
                                 DateTime.UtcNow,
                                 kind,
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
