using System.Collections.Generic;
using System.Linq;
using FileAuditor.Models.Auditing;

namespace FileAuditor.Services.Sql;

/// <summary>
///     Builds the audit statements from the table and column names entered by the user.
/// </summary>
public static class QueryBuilder
{
    /// <summary>
    ///     Builds the count and select statements, quoting every identifier part.
    /// </summary>
    /// <param name="table">The table name, either <c>table</c> or <c>schema.table</c>.</param>
    /// <param name="pathColumn">The name of the column holding file paths.</param>
    /// <param name="idColumn">The name of the optional ID column; empty or whitespace for none.</param>
    /// <returns>The audit query, or <c>Query.InvalidIdentifier</c> when a name is malformed.</returns>
    public static ErrorOr<AuditQuery> Build(string table, string pathColumn, string idColumn)
    {
        ErrorOr<IReadOnlyList<string>> tableParts = SqlIdentifier.ParseMultipart(table, 2);

        if (tableParts.IsError)
        {
            return tableParts.Errors;
        }

        ErrorOr<IReadOnlyList<string>> pathParts = SqlIdentifier.ParseMultipart(pathColumn, 1);

        if (pathParts.IsError)
        {
            return pathParts.Errors;
        }

        string tableSql = string.Join('.', tableParts.Value.Select(SqlIdentifier.Quote));
        string selectList = $"{SqlIdentifier.Quote(pathParts.Value[0])} AS [Path]";
        bool hasId = !string.IsNullOrWhiteSpace(idColumn);

        if (hasId)
        {
            ErrorOr<IReadOnlyList<string>> idParts = SqlIdentifier.ParseMultipart(idColumn, 1);

            if (idParts.IsError)
            {
                return idParts.Errors;
            }

            selectList += $", {SqlIdentifier.Quote(idParts.Value[0])} AS [Id]";
        }

        return new AuditQuery(
            $"SELECT COUNT_BIG(*) FROM {tableSql};",
            $"SELECT {selectList} FROM {tableSql};",
            hasId);
    }
}