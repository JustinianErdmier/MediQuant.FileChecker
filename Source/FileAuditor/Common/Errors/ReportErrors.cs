namespace FileAuditor.Common.Errors;

/// <summary>
///     The expected failures of writing the report.
/// </summary>
public static class ReportErrors
{
    /// <summary>
    ///     Creates the error for a report folder that cannot be written to, found by the pre-flight probe before the run starts.
    /// </summary>
    /// <param name="folder">The report folder.</param>
    /// <param name="detail">What went wrong.</param>
    /// <returns>A failure error with code <c>Report.FolderNotWritable</c>.</returns>
    public static Error FolderNotWritable(string folder, string detail) =>
        Error.Failure("Report.FolderNotWritable", $"The report cannot be written to {folder}:\n\n{detail}");

    /// <summary>
    ///     Creates the error for an existing report file that is marked read-only, found by the pre-flight check before the run starts.
    /// </summary>
    /// <param name="path">The full path of the report.</param>
    /// <returns>A failure error with code <c>Report.ReadOnly</c>.</returns>
    public static Error ReadOnly(string path) =>
        Error.Failure("Report.ReadOnly", $"The existing report is read-only and cannot be overwritten:\n{path}\n\nClear its read-only attribute, or choose another location.");

    /// <summary>
    ///     Creates the error for a report that cannot be written to disk.
    /// </summary>
    /// <param name="detail">What went wrong.</param>
    /// <returns>A failure error with code <c>Report.Unwritable</c>.</returns>
    public static Error Unwritable(string detail) =>
        Error.Failure("Report.Unwritable", $"The report could not be written: {detail}");
}
