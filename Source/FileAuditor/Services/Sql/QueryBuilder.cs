namespace FileAuditor.Services.Sql;

/// <summary>Builds the audit statements from the table and column names entered by the user.</summary>
public static class QueryBuilder
{
    /// <summary>The character that escapes <c>LIKE</c> wildcards in a folder prefix.</summary>
    private const char EscapeCharacter = '|';

    /// <summary>The most folders the prefix filter is built for; more than this loads every row.</summary>
    public const int MaxFilterFolders = 20;

    /// <summary>Parses and quotes the table and the path column, the names the type check reads the column's type from.</summary>
    /// <param name="table">The table name, either <c>table</c> or <c>schema.table</c>.</param>
    /// <param name="pathColumn">The name of the column holding file paths.</param>
    /// <returns>The quoted table and path column, exactly as the statements use them; or <c>Query.InvalidIdentifier</c> when a name is malformed.</returns>
    public static ErrorOr<(string QuotedTable, string QuotedPathColumn)> QuoteTarget(string table, string pathColumn)
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

        return (string.Join(separator: '.', tableParts.Value.Select(SqlIdentifier.Quote)), SqlIdentifier.Quote(pathParts.Value[index: 0]));
    }

    /// <summary>Builds the Missing Files audit's two plain statements, quoting every identifier part.</summary>
    /// <remarks>
    ///     The statements are a row count and a select of every row in table order: no conversion, no <c>WHERE</c>, no <c>DISTINCT</c> and no parameters, so SQL Server has
    ///     nothing to classify and the first row arrives at once. The audit classifies, de-duplicates and collects the empty-path IDs in the app, row by row. The path column is
    ///     <c>nvarchar</c> or <c>varchar</c> (checked by <see cref="PathColumnType" /> before these statements are built) and is read as it is.
    /// </remarks>
    /// <param name="table">The table name, either <c>table</c> or <c>schema.table</c>.</param>
    /// <param name="pathColumn">The name of the column holding file paths.</param>
    /// <param name="idColumn">The name of the optional ID column; empty or whitespace for none.</param>
    /// <returns>The audit query, or <c>Query.InvalidIdentifier</c> when a name is malformed.</returns>
    public static ErrorOr<AuditQuery> Build(string table, string pathColumn, string idColumn)
    {
        ErrorOr<(string QuotedTable, string QuotedPathColumn)> target = QuoteTarget(table, pathColumn);

        if (target.IsError)
        {
            return target.Errors;
        }

        (string tableSql, string pathSql) = target.Value;

        string? idSql = null;

        if (!string.IsNullOrWhiteSpace(idColumn))
        {
            ErrorOr<IReadOnlyList<string>> idParts = SqlIdentifier.ParseMultipart(idColumn, maxParts: 1);

            if (idParts.IsError)
            {
                return idParts.Errors;
            }

            idSql = SqlIdentifier.Quote(idParts.Value[index: 0]);
        }

        return new AuditQuery($"SELECT COUNT_BIG(*) FROM {tableSql};",
                              $"SELECT {pathSql} AS [Path]{(idSql is null ? string.Empty : $", {idSql} AS [Id]")} FROM {tableSql};",
                              idSql is not null);
    }

    /// <summary>Builds the statement that selects the distinct values of the path column, restricted to the rows under the given folders where that is possible.</summary>
    /// <param name="table">The table name, either <c>table</c> or <c>schema.table</c>.</param>
    /// <param name="pathColumn">The name of the column holding file paths.</param>
    /// <param name="folders">
    ///     The folders to check. For 1 to <see cref="MaxFilterFolders" /> folders the statement filters on a case-insensitive <c>LIKE</c> prefix per folder; for none,
    ///     for more, or when any folder is empty (which has no prefix to match), the filter is dropped and every row is selected. The filter also selects every row that starts
    ///     with a whitespace character, so that <see cref="IndexPathSet" />, which ignores surrounding whitespace, can still match it.
    /// </param>
    /// <param name="convertTo">
    ///     The type to convert the path column to in the folder filter (<c>nvarchar(4000)</c>, from <see cref="PathColumnType.Check" />), or
    ///     <see langword="null" /> for an <c>nvarchar</c> column. Both <c>LIKE</c> clauses test only the start of the path, so characters beyond 4,000 never change the result. The selected value stays the column itself; only the filter's <c>LIKE</c> clauses compare the converted value,
    ///     because their <c>COLLATE</c> clause would otherwise be applied to the <c>varchar</c> value before it meets the <c>nvarchar</c> parameters. Without a filter nothing is
    ///     converted.
    /// </param>
    /// <returns>The query, or <c>Query.InvalidIdentifier</c> when a name is malformed.</returns>
    public static ErrorOr<PathQuery> BuildPaths(string table, string pathColumn, IReadOnlyList<string> folders, string? convertTo)
    {
        ErrorOr<(string QuotedTable, string QuotedPathColumn)> target = QuoteTarget(table, pathColumn);

        if (target.IsError)
        {
            return target.Errors;
        }

        (string tableSql, string columnSql) = target.Value;

        string sql = $"SELECT DISTINCT {columnSql} AS [Path] FROM {tableSql}";

        bool canFilter = folders.Count is >= 1 and <= MaxFilterFolders
                         && folders.All(IsRepresentable);

        if (!canFilter)
        {
            return new PathQuery($"{sql};", []);
        }

        string filterRef = convertTo is null ? columnSql : $"CAST({columnSql} AS {convertTo})";

        List<KeyValuePair<string, string>> parameters = new(folders.Count);
        List<string>                       clauses    = new(folders.Count);

        for (int index = 0; index < folders.Count; index++)
        {
            string name = $"@p{index}";

            parameters.Add(new KeyValuePair<string, string>(name, EscapePrefix(folders[index])));
            clauses.Add($"{filterRef} COLLATE Latin1_General_100_CI_AS LIKE {name} ESCAPE '{EscapeCharacter}'");
        }

        // Values with leading whitespace would miss every prefix above, yet the index set trims them, so load them all (LTRIM only strips spaces before SQL 2022).
        clauses.Add($"{filterRef} COLLATE Latin1_General_100_BIN2 LIKE @pw");
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
