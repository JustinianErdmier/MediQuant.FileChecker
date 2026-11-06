namespace FileAuditor.Common.Errors;

/// <summary>
///     The expected non-SQL outcomes of running an audit.
/// </summary>
public static class AuditErrors
{
    /// <summary>The code of <see cref="StoppedBeforeChecking" />.</summary>
    public const string StoppedBeforeCheckingCode = "Audit.StoppedBeforeChecking";

    /// <summary>
    ///     Creates the error for a run the user stopped while the rows were still being counted, so there is nothing to report.
    /// </summary>
    public static Error StoppedBeforeChecking { get; } = Error.Conflict(StoppedBeforeCheckingCode, "The audit was stopped before any file was checked.");
}
