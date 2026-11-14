namespace FileAuditor.Models.Auditing;

/// <summary>The SQL statements used to audit an index table for missing files.</summary>
/// <param name="CountSql">The statement that returns the table's row count as one <c>bigint</c>.</param>
/// <param name="SelectSql">The statement that selects the path (and the ID) of every row, in table order, as the columns hold them.</param>
/// <param name="HasIdColumn">Whether <paramref name="SelectSql" /> returns an <c>Id</c> column as its second column.</param>
public sealed record AuditQuery(string CountSql, string SelectSql, bool HasIdColumn);
