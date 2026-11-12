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

    /// <summary>The code of the error returned by <see cref="PathColumnNotNvarchar" />.</summary>
    public const string PathColumnNotNvarcharCode = "Sql.PathColumnNotNvarchar";

    /// <summary>The code of the error returned by <see cref="PathColumnNotFound" />.</summary>
    public const string PathColumnNotFoundCode = "Sql.PathColumnNotFound";

    /// <summary>Creates the error for a path column whose type is not <c>nvarchar</c>.</summary>
    /// <param name="column">The path column, as the user typed it.</param>
    /// <param name="type">The column's type, formatted for display (for example <c>varchar(400)</c>).</param>
    /// <returns>A validation error with code <c>Sql.PathColumnNotNvarchar</c>.</returns>
    public static Error PathColumnNotNvarchar(string column, string type)
        => Error.Validation(PathColumnNotNvarcharCode, $"The path column '{column}' is {type}. The File Auditor expects an nvarchar column.");

    /// <summary>Creates the error for a path column that cannot be found in the table (or a table that cannot be found).</summary>
    /// <param name="column">The path column, as the user typed it.</param>
    /// <param name="table">The table, as the user typed it.</param>
    /// <returns>A validation error with code <c>Sql.PathColumnNotFound</c>.</returns>
    public static Error PathColumnNotFound(string column, string table)
        => Error.Validation(PathColumnNotFoundCode, $"The path column '{column}' was not found in the table '{table}'.");
}
