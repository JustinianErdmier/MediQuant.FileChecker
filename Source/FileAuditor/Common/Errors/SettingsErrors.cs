namespace FileAuditor.Common.Errors;

/// <summary>The expected failures of the settings store.</summary>
public static class SettingsErrors
{
    /// <summary>Creates the error for a project settings file that is missing, cannot be read or cannot be parsed.</summary>
    /// <param name="detail">What went wrong.</param>
    /// <returns>A failure error with code <c>Settings.Unreadable</c>.</returns>
    public static Error Unreadable(string detail) => Error.Failure(code: "Settings.Unreadable", $"The settings file could not be read: {detail}");

    /// <summary>Creates the error for settings that cannot be written to disk. The bare detail is also carried in the error's metadata, for <see cref="BareDetail" />.</summary>
    /// <param name="detail">What went wrong.</param>
    /// <returns>A failure error with code <c>Settings.Unwritable</c>.</returns>
    public static Error Unwritable(string detail)
        => Error.Failure(code: "Settings.Unwritable", $"The settings file could not be saved: {detail}", new Dictionary<string, object>
        {
            [DetailKey] = detail
        });

    /// <summary>Gets what went wrong without the sentence that introduces it, for showing inside a dialogue that has its own introduction.</summary>
    /// <param name="error">An error from the settings store.</param>
    /// <returns>The bare detail the error carries, or its description when it carries none.</returns>
    public static string BareDetail(Error error) => error.Metadata?.GetValueOrDefault(DetailKey) as string ?? error.Description;

    private const string DetailKey = "Detail";
}
