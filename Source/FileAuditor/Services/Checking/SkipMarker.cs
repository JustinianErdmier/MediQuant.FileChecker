namespace FileAuditor.Services.Checking;

/// <summary>Recognises the index path values that mark a row as deliberately skipped.</summary>
public static class SkipMarker
{
    /// <summary>Decides whether an index path value is a skip marker.</summary>
    /// <param name="path">The path value from the index table; <see langword="null" /> is not a marker.</param>
    /// <returns>
    ///     <see langword="true" /> when the trimmed value is <c>SKIP</c> or <c>SKIPPED</c>, or starts with <c>SKIP:</c> or <c>SKIPPED:</c>, ignoring case; otherwise
    ///     <see langword="false" />.
    /// </returns>
    public static bool IsSkipped(string? path)
    {
        if (path is null)
        {
            return false;
        }

        string value = path.Trim();

        return value.Equals(value: "SKIP", StringComparison.OrdinalIgnoreCase)
               || value.Equals(value: "SKIPPED", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith(value: "SKIP:", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith(value: "SKIPPED:", StringComparison.OrdinalIgnoreCase);
    }
}
