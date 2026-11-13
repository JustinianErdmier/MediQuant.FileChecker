namespace FileAuditor.Models.Auditing;

/// <summary>The SQL statements used to audit an index table.</summary>
/// <param name="CountsSql">
///     The statement that returns one row of four <c>bigint</c> columns, in the order of <see cref="RowCounts" />: the table rows, the empty paths, the skip markers and the
///     distinct real paths.
/// </param>
/// <param name="SelectSql">The statement that selects each distinct real path (or path and ID pair): neither empty nor a skip marker.</param>
/// <param name="EmptyIdsSql">The statement that selects the ID of every empty-path row, or <see langword="null" /> when there is no ID column.</param>
/// <param name="Parameters">The parameters (name and text value) that apply to all of the statements.</param>
/// <param name="HasIdColumn">Whether <paramref name="SelectSql" /> returns an <c>Id</c> column as its second column.</param>
/// <param name="QuotedTable">The table name with every part quoted, exactly as the statements use it.</param>
/// <param name="QuotedPathColumn">The name of the path column, quoted exactly as the statements use it.</param>
public sealed record AuditQuery(string                                       CountsSql,
                                string                                       SelectSql,
                                string?                                      EmptyIdsSql,
                                IReadOnlyList<KeyValuePair<string, string>> Parameters,
                                bool                                         HasIdColumn,
                                string                                       QuotedTable,
                                string                                       QuotedPathColumn);
