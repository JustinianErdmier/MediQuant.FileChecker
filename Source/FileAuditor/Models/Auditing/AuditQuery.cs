namespace FileAuditor.Models.Auditing;

/// <summary>
///     The pair of SQL statements used to audit an index table.
/// </summary>
/// <param name="CountSql">The statement that counts the rows in the table.</param>
/// <param name="SelectSql">The statement that selects the path (and optional ID) of every row.</param>
/// <param name="HasIdColumn">Whether <paramref name="SelectSql" /> returns an <c>Id</c> column as its second column.</param>
public sealed record AuditQuery(string CountSql, string SelectSql, bool HasIdColumn);
