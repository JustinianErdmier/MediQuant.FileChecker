namespace FileAuditor.Common.Errors;

/// <summary>
///     The expected failures of building the audit query.
/// </summary>
public static class QueryErrors
{
    /// <summary>
    ///     Creates the error for a table or column name that cannot be turned into a safe SQL identifier.
    /// </summary>
    /// <param name="text">The text that was rejected.</param>
    /// <returns>A validation error with code <c>Query.InvalidIdentifier</c>.</returns>
    public static Error InvalidIdentifier(string text) =>
        Error.Validation("Query.InvalidIdentifier", $"'{text}' is not a valid table or column name.");
}
