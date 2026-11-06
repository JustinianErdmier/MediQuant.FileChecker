namespace FileAuditor.Common.Errors;

/// <summary>The expected non-SQL outcomes of running an audit.</summary>
public static class AuditErrors
{
    /// <summary>The code of the error returned by <see cref="ReadFailed" />.</summary>
    public const string ReadFailedCode = "Audit.ReadFailed";

    /// <summary>The code of <see cref="StoppedBeforeChecking" />.</summary>
    public const string StoppedBeforeCheckingCode = "Audit.StoppedBeforeChecking";

    /// <summary>Gets the error for a run the user stopped while the rows were still being counted, so there is nothing to report.</summary>
    public static Error StoppedBeforeChecking { get; } = Error.Conflict(StoppedBeforeCheckingCode, description: "The audit was stopped before any file was checked.");

    /// <summary>Creates the error for a row stream that failed part-way for a reason other than SQL Server or the user.</summary>
    /// <param name="message">What went wrong.</param>
    /// <returns>A failure error with code <c>Audit.ReadFailed</c>.</returns>
    public static Error ReadFailed(string message) => Error.Failure(ReadFailedCode, message);
}
