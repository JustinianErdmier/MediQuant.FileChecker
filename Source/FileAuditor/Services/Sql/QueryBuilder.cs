namespace FileAuditor.Services.Sql;

/// <summary>Builds the audit statements from the table and column names entered by the user.</summary>
public static class QueryBuilder
{
    /// <summary>The character that escapes <c>LIKE</c> wildcards in a folder prefix.</summary>
    private const char EscapeCharacter = '|';

    /// <summary>The most folders the prefix filter is built for; more than this loads every row.</summary>
    public const int MaxFilterFolders = 20;

    /// <summary>Builds the counts, select and empty-ID statements, quoting every identifier part.</summary>
    /// <remarks>
    ///     Each row is classified in SQL: empty (<c>NULL</c> or only white space), skip marker (the rule of <see cref="SkipMarker" />) or real path. The select statement is
    ///     <c>DISTINCT</c> over the real paths only, so a path listed on several rows (an index built around patients rather than files) is checked once. With an ID column the
    ///     distinct unit is the path and ID pair: the ID is expected to be unique to its path, and the app does not check that. The white-space characters are passed as the
    ///     <c>@ws</c> parameter, listed in <see cref="AuditQuery.Parameters" />.
    /// </remarks>
    /// <param name="table">The table name, either <c>table</c> or <c>schema.table</c>.</param>
    /// <param name="pathColumn">The name of the column holding file paths.</param>
    /// <param name="idColumn">The name of the optional ID column; empty or whitespace for none.</param>
    /// <returns>The audit query, or <c>Query.InvalidIdentifier</c> when a name is malformed.</returns>
    public static ErrorOr<AuditQuery> Build(string table, string pathColumn, string idColumn)
    {
        ErrorOr<IReadOnlyList<string>> tableParts = SqlIdentifier.ParseMultipart(table, maxParts: 2);

        if (tableParts.IsError)
        {
            return tableParts.Errors;
        }

        ErrorOr<IReadOnlyList<string>> pathParts = SqlIdentifier.ParseMultipart(pathColumn, maxParts: 1);

        if (pathParts.IsError)
        {
            return pathParts.Errors;
        }

        string  tableSql = string.Join(separator: '.', tableParts.Value.Select(SqlIdentifier.Quote));
        string  pathSql  = SqlIdentifier.Quote(pathParts.Value[index: 0]);
        string? idSql    = null;

        if (!string.IsNullOrWhiteSpace(idColumn))
        {
            ErrorOr<IReadOnlyList<string>> idParts = SqlIdentifier.ParseMultipart(idColumn, maxParts: 1);

            if (idParts.IsError)
            {
                return idParts.Errors;
            }

            idSql = SqlIdentifier.Quote(idParts.Value[index: 0]);
        }

        // Every row gets one kind in one pass: 0 real path, 1 empty, 2^32 skip marker (a weight, see the counts). Every comparison is binary so that the rule matches SkipMarker and char.IsWhiteSpace
        // exactly, whatever the column's collation. Empty is a single LIKE (no non-white-space character). The trim and the four skip LIKEs are guarded by a cheap
        // test for "skip" anywhere in the value (a WHEN of the CASE, ahead of them), so they run only for the few rows that could be markers. SQL Server inlines the apply
        // scalars: [fa_n] is not computed once per row, it is recomputed in each LIKE that uses it, which is cheap only because of that guard.
        // The aliases are deliberately unlikely names and the table's columns are always qualified, so a table or column named like an alias cannot collide.
        string source = $"""
                         FROM {tableSql} AS [fa_t]
                         CROSS APPLY (SELECT CAST([fa_t].{pathSql} AS nvarchar(max)) AS [FaValue]) AS [fa_v]
                         CROSS APPLY (SELECT SUBSTRING([fa_v].[FaValue],
                                                       PATINDEX(N'%[^' + @ws + N']%', [fa_v].[FaValue] COLLATE Latin1_General_100_BIN2),
                                                       LEN([fa_v].[FaValue] + N'x') - 1
                                                       - PATINDEX(N'%[^' + @ws + N']%', [fa_v].[FaValue] COLLATE Latin1_General_100_BIN2)
                                                       - PATINDEX(N'%[^' + @ws + N']%', REVERSE([fa_v].[FaValue]) COLLATE Latin1_General_100_BIN2) + 2) AS [FaTrimmed]) AS [fa_n]
                         CROSS APPLY (SELECT CASE WHEN [fa_v].[FaValue] IS NULL
                                                       OR [fa_v].[FaValue] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^' + @ws + N']%'
                                                  THEN 1
                                                  WHEN [fa_v].[FaValue] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[Ss][Kk][Ii][Pp]%'
                                                  THEN 0
                                                  WHEN [fa_n].[FaTrimmed] COLLATE Latin1_General_100_BIN2 LIKE N'[Ss][Kk][Ii][Pp]'
                                                       OR [fa_n].[FaTrimmed] COLLATE Latin1_General_100_BIN2 LIKE N'[Ss][Kk][Ii][Pp][Pp][Ee][Dd]'
                                                       OR [fa_n].[FaTrimmed] COLLATE Latin1_General_100_BIN2 LIKE N'[Ss][Kk][Ii][Pp]:%'
                                                       OR [fa_n].[FaTrimmed] COLLATE Latin1_General_100_BIN2 LIKE N'[Ss][Kk][Ii][Pp][Pp][Ee][Dd]:%'
                                                  THEN CAST(4294967296 AS bigint)
                                                  ELSE 0
                                             END AS [FaKind]) AS [fa_k]
                         """;

        string pathRef = $"[fa_t].{pathSql}";
        string idRef   = idSql is null ? string.Empty : $"[fa_t].{idSql}";

        string realSql = $"SELECT DISTINCT {pathRef} AS [Path]{(idSql is null ? string.Empty : $", {idRef} AS [Id]")} {source} WHERE [fa_k].[FaKind] = 0";

        // The counts read the kind once. SQL Server inlines the apply scalars, so every extra reference to [FaKind] would classify the whole table again (three references
        // cost three times as much), and the kind is therefore a weight that one SUM can carry: an empty row weighs 1 and a skip marker weighs 2^32, so the sum holds the
        // empty count below 2^32 and the skip count above it. The distinct real paths are counted over the select statement, a second pass over the table.
        string countsSql = $"""
                            SELECT [c].[Total],
                                   CAST([c].[Weights] % 4294967296 AS bigint) AS [Empty],
                                   CAST([c].[Weights] / 4294967296 AS bigint) AS [Skipped],
                                   [d].[Distinct]
                            FROM (SELECT COUNT_BIG(*) AS [Total], COALESCE(SUM([fa_k].[FaKind]), CAST(0 AS bigint)) AS [Weights]
                                  {source}) AS [c]
                            CROSS JOIN (SELECT COUNT_BIG(*) AS [Distinct] FROM ({realSql}) AS [r]) AS [d];
                            """;

        string? emptyIdsSql = idSql is null ? null : $"SELECT {idRef} AS [Id] {source} WHERE [fa_k].[FaKind] = 1;";

        return new AuditQuery(countsSql,
                              $"{realSql};",
                              emptyIdsSql,
                              [new KeyValuePair<string, string>(key: "@ws", WhitespaceClass())],
                              idSql is not null);
    }

    /// <summary>Builds the statement that selects the distinct values of the path column, restricted to the rows under the given folders where that is possible.</summary>
    /// <param name="table">The table name, either <c>table</c> or <c>schema.table</c>.</param>
    /// <param name="pathColumn">The name of the column holding file paths.</param>
    /// <param name="folders">
    ///     The folders to check. For 1 to <see cref="MaxFilterFolders" /> folders the statement filters on a case-insensitive <c>LIKE</c> prefix per folder; for none,
    ///     for more, or when any folder is empty (which has no prefix to match), the filter is dropped and every row is selected. The filter also selects every row that starts with a
    ///     whitespace character, so that <see cref="IndexPathSet" />, which ignores surrounding whitespace, can still match it.
    /// </param>
    /// <returns>The query, or <c>Query.InvalidIdentifier</c> when a name is malformed.</returns>
    public static ErrorOr<PathQuery> BuildPaths(string table, string pathColumn, IReadOnlyList<string> folders)
    {
        ErrorOr<IReadOnlyList<string>> tableParts = SqlIdentifier.ParseMultipart(table, maxParts: 2);

        if (tableParts.IsError)
        {
            return tableParts.Errors;
        }

        ErrorOr<IReadOnlyList<string>> pathParts = SqlIdentifier.ParseMultipart(pathColumn, maxParts: 1);

        if (pathParts.IsError)
        {
            return pathParts.Errors;
        }

        string tableSql  = string.Join(separator: '.', tableParts.Value.Select(SqlIdentifier.Quote));
        string columnSql = SqlIdentifier.Quote(pathParts.Value[index: 0]);
        string sql       = $"SELECT DISTINCT {columnSql} AS [Path] FROM {tableSql}";

        bool canFilter = folders.Count is >= 1 and <= MaxFilterFolders
                         && folders.All(IsRepresentable);

        if (!canFilter)
        {
            return new PathQuery($"{sql};", []);
        }

        List<KeyValuePair<string, string>> parameters = new(folders.Count);
        List<string>                       clauses    = new(folders.Count);

        for (int index = 0; index < folders.Count; index++)
        {
            string name = $"@p{index}";

            parameters.Add(new KeyValuePair<string, string>(name, EscapePrefix(folders[index])));
            clauses.Add($"{columnSql} COLLATE Latin1_General_100_CI_AS LIKE {name} ESCAPE '{EscapeCharacter}'");
        }

        // Values with leading whitespace would miss every prefix above, yet the index set trims them, so load them all (LTRIM only strips spaces before SQL 2022).
        clauses.Add($"{columnSql} COLLATE Latin1_General_100_BIN2 LIKE @pw");
        parameters.Add(new KeyValuePair<string, string>(key: "@pw", WhitespacePattern()));

        return new PathQuery($"{sql} WHERE {string.Join(separator: " OR ", clauses)};", parameters);
    }

    /// <summary>Determines whether a folder can be turned into a <c>LIKE</c> prefix: an empty folder has no prefix to match.</summary>
    /// <param name="folder">The folder.</param>
    /// <returns><see langword="true" /> when the folder is not empty.</returns>
    private static bool IsRepresentable(string folder) => folder.Length > 0;

    /// <summary>Builds a pattern matching a first character that <see cref="char.IsWhiteSpace(char)" /> accepts, from its code point ranges.</summary>
    /// <returns>The <c>LIKE</c> pattern that matches any value starting with a white-space character.</returns>
    private static string WhitespacePattern() => $"[{WhitespaceClass()}]%";

    /// <summary>Builds the body of a <c>LIKE</c> character class (without the brackets) holding the characters <see cref="char.IsWhiteSpace(char)" /> accepts.</summary>
    /// <returns>The class body, as code point ranges.</returns>
    private static string WhitespaceClass()
    {
        (int First, int Last)[] ranges =
        [
            (0x0009, 0x000D),
            (0x0020, 0x0020),
            (0x0085, 0x0085),
            (0x00A0, 0x00A0),
            (0x1680, 0x1680),
            (0x2000, 0x200A),
            (0x2028, 0x2029),
            (0x202F, 0x202F),
            (0x205F, 0x205F),
            (0x3000, 0x3000)
        ];

        StringBuilder builder = new();

        foreach ((int first, int last) in ranges)
        {
            builder.Append((char)first);

            if (last > first)
            {
                builder.Append(value: '-')
                       .Append((char)last);
            }
        }

        return builder.ToString();
    }

    /// <summary>Builds the <c>LIKE</c> pattern for the paths under a folder: the folder with its wildcards escaped, a trailing backslash if it has none, and <c>%</c>.</summary>
    /// <param name="folder">The normalised folder path.</param>
    /// <returns>The pattern, to be used with the escape character.</returns>
    private static string EscapePrefix(string folder)
    {
        StringBuilder builder = new(folder.Length + 8);

        foreach (char character in folder)
        {
            if (character is EscapeCharacter
                             or '%'
                             or '_'
                             or '[')
            {
                builder.Append(EscapeCharacter);
            }

            builder.Append(character);
        }

        if (folder[^1] is not '\\')
        {
            builder.Append(value: '\\');
        }

        return builder.Append(value: '%')
                      .ToString();
    }
}
