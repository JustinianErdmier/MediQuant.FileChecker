namespace FileAuditor.ViewModels.Main;

/// <summary>A run of the missing-files audit.</summary>
/// <param name="audit">The Missing Files tab's Audit group.</param>
/// <param name="runner">The runner that performs the audit.</param>
/// <param name="request">The settings and report path.</param>
public sealed class MissingAuditRun(AuditViewModel audit, AuditRunner runner, AuditRequest request) : AuditRun(audit)
{
    /// <summary>The live counters of this run.</summary>
    private readonly AuditProgress _progress = new()
    {
        Phase = "Counting files..."
    };

    /// <inheritdoc />
    public override string Phase => _progress.Phase;

    /// <inheritdoc />
    public override void Update(TimeSpan elapsed, bool isFinished) => audit.Update(_progress, elapsed, isFinished);

    /// <inheritdoc />
    protected override async Task<(string Status, string? Error)> RunAsync(CancellationToken cancellationToken)
    {
        ErrorOr<AuditReport> outcome = await runner.RunAsync(request, _progress, cancellationToken);

        return outcome.IsError ? DescribeError(outcome.FirstError) : DescribeResult(outcome.Value.Result, outcome.Value.FailureMessage);
    }
}
