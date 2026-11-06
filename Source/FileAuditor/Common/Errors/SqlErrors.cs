namespace FileAuditor.Common.Errors;

/// <summary>The expected failures of talking to SQL Server.</summary>
public static class SqlErrors
{
    /// <summary>The code of the error returned by <see cref="QueryFailed" />.</summary>
    public const string QueryFailedCode = "Sql.QueryFailed";

    /// <summary>Creates the error for a query that SQL Server could not run or that failed part-way.</summary>
    /// <param name="message">The message reported by SQL Server.</param>
    /// <returns>A failure error with code <c>Sql.QueryFailed</c>.</returns>
    public static Error QueryFailed(string message) => Error.Failure(QueryFailedCode, message);
}
