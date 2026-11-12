namespace FileAuditor.Models.Auditing;

/// <summary>A parameterised statement that selects the path column of the index table, optionally restricted to the rows under some folders.</summary>
/// <param name="Sql">The statement; every folder value is referenced as a parameter, never concatenated.</param>
/// <param name="Parameters">The parameter names (<c>@p0</c>, <c>@p1</c>, …) and their <c>NVarChar</c> values.</param>
/// <param name="QuotedTable">The table name with every part quoted, exactly as <paramref name="Sql" /> uses it.</param>
/// <param name="QuotedPathColumn">The name of the path column, quoted exactly as <paramref name="Sql" /> uses it.</param>
public sealed record PathQuery(string Sql, IReadOnlyList<KeyValuePair<string, string>> Parameters, string QuotedTable, string QuotedPathColumn);
