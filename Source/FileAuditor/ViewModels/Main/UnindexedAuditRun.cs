namespace FileAuditor.ViewModels.Main;

/// <summary>A run of the unindexed-files audit.</summary>
/// <param name="audit">The Unindexed Files tab's Audit group.</param>
/// <param name="runner">The runner that performs the audit.</param>
/// <param name="request">The settings and report path.</param>
public sealed class UnindexedAuditRun(UnindexedAuditViewModel audit, UnindexedAuditRunner runner, AuditRequest request) : AuditRun(audit)
{
    // Fresh for every run: the report's Summary reads the counters, so a reused one would carry the last run's totals into this one.
    private readonly UnindexedProgress _progress = new()
    {
        Phase = "Loading the index..."
    };

    /// <inheritdoc />
    public override string Phase => _progress.Phase;

    /// <inheritdoc />
    public override void Begin() => audit.Begin();

    /// <inheritdoc />
    public override void Update(TimeSpan elapsed, bool isFinished) => audit.Update(_progress, elapsed, isFinished);

    /// <inheritdoc />
    protected override async Task<(string Status, string? Error)> RunAsync(CancellationToken cancellationToken)
    {
        ErrorOr<UnindexedReport> outcome = await runner.RunAsync(request, _progress, cancellationToken);

        return outcome.IsError ? DescribeError(outcome.FirstError) : DescribeResult(outcome.Value.Result, outcome.Value.FailureMessage);
    }
}
