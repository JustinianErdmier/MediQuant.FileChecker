namespace FileAuditor.Common.Errors;

/// <summary>The expected failures of the settings store.</summary>
public static class SettingsErrors
{
    /// <summary>Creates the error for a settings file that exists but cannot be read or parsed. Callers treat this as "use the defaults".</summary>
    /// <param name="detail">What went wrong.</param>
    /// <returns>A failure error with code <c>Settings.Unreadable</c>.</returns>
    public static Error Unreadable(string detail) => Error.Failure(code: "Settings.Unreadable", $"The settings file could not be read: {detail}");

    /// <summary>Creates the error for settings that cannot be written to disk.</summary>
    /// <param name="detail">What went wrong.</param>
    /// <returns>A failure error with code <c>Settings.Unwritable</c>.</returns>
    public static Error Unwritable(string detail) => Error.Failure(code: "Settings.Unwritable", $"The settings file could not be saved: {detail}");
}
