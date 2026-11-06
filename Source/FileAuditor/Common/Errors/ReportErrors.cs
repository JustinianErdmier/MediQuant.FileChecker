namespace FileAuditor.Common.Errors;

/// <summary>
///     The expected failures of writing the report.
/// </summary>
public static class ReportErrors
{
    /// <summary>
    ///     Creates the error for a report that cannot be written to disk.
    /// </summary>
    /// <param name="detail">What went wrong.</param>
    /// <returns>A failure error with code <c>Report.Unwritable</c>.</returns>
    public static Error Unwritable(string detail) =>
        Error.Failure("Report.Unwritable", $"The report could not be written: {detail}");
}
