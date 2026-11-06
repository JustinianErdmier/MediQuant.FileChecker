namespace FileAuditor.Common.Errors;

/// <summary>
///     The expected failures of locating and writing the report.
/// </summary>
public static class ReportErrors
{
    /// <summary>
    ///     Creates the error for a report folder that does not exist.
    /// </summary>
    /// <param name="folder">The folder that is missing.</param>
    /// <returns>A validation error with code <c>Report.FolderMissing</c>.</returns>
    public static Error FolderMissing(string folder) =>
        Error.Validation("Report.FolderMissing", $"The report folder '{folder}' does not exist.");

    /// <summary>
    ///     Creates the error for a report that cannot be written to disk.
    /// </summary>
    /// <param name="detail">What went wrong.</param>
    /// <returns>A failure error with code <c>Report.Unwritable</c>.</returns>
    public static Error Unwritable(string detail) =>
        Error.Failure("Report.Unwritable", $"The report could not be written: {detail}");
}
