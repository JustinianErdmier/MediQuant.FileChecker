namespace FileAuditor.Services.Reporting;

/// <summary>Writes the finished report to disk.</summary>
public static class ReportWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Writes the report as UTF-8 without a byte order mark, to a temporary file beside the target and then moved over it, so a failure never leaves a half-written report.</summary>
    /// <param name="fullPath">The full path of the report file.</param>
    /// <param name="content">The report text.</param>
    /// <returns>Success, or <see cref="ReportErrors.Unwritable" /> when the file cannot be written.</returns>
    public static ErrorOr<Success> Write(string fullPath, string content)
    {
        string tempPath = fullPath + ".tmp";

        try
        {
            File.WriteAllText(tempPath, content, Utf8NoBom);
            File.Move(tempPath, fullPath, overwrite: true);

            return Result.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            TryDelete(tempPath);

            return ReportErrors.Unwritable(exception.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Best effort: the original failure is the one worth reporting.
        }
    }
}
