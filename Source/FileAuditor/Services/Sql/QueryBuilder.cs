namespace FileAuditor.Services.Sql;

/// <summary>Builds the audit statements from the table and column names entered by the user.</summary>
public static class QueryBuilder
{
    /// <summary>The character that escapes <c>LIKE</c> wildcards in a folder prefix.</summary>
    private const char EscapeCharacter = '|';

    /// <summary>The most folders the prefix filter is built for; more than this loads every row.</summary>
    public const int MaxFilterFolders = 20;

    /// <summary>Builds the count and select statements, quoting every identifier part.</summary>
    /// <remarks>
    ///     Both statements are <c>DISTINCT</c>, so a path listed on several rows (an index built around patients rather than files) is checked once. With an ID column the
    ///     distinct unit is the path and ID pair: the ID is expected to be unique to its path, and the app does not check that.
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

        string tableSql   = string.Join(separator: '.', tableParts.Value.Select(SqlIdentifier.Quote));
        string selectList = $"{SqlIdentifier.Quote(pathParts.Value[index: 0])} AS [Path]";
        bool   hasId      = !string.IsNullOrWhiteSpace(idColumn);

        if (hasId)
        {
            ErrorOr<IReadOnlyList<string>> idParts = SqlIdentifier.ParseMultipart(idColumn, maxParts: 1);

            if (idParts.IsError)
            {
                return idParts.Errors;
            }

            selectList += $", {SqlIdentifier.Quote(idParts.Value[index: 0])} AS [Id]";
        }

        string distinctSql = $"SELECT DISTINCT {selectList} FROM {tableSql}";

        return new AuditQuery($"SELECT COUNT_BIG(*) FROM ({distinctSql}) AS [Rows];",
                              $"{distinctSql};",
                              hasId);
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
    private static string WhitespacePattern()
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

        StringBuilder builder = new(value: "[");

        foreach ((int first, int last) in ranges)
        {
            builder.Append((char)first);

            if (last > first)
            {
                builder.Append(value: '-')
                       .Append((char)last);
            }
        }

        return builder.Append(value: "]%")
                      .ToString();
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
