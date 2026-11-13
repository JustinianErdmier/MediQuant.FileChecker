namespace FileAuditor.Services.Sql;

/// <summary>Checks that the path column is <c>nvarchar</c> or <c>varchar</c> and says what the audit statements convert it to.</summary>
public static class PathColumnType
{
    /// <summary>The longest <c>nvarchar(n)</c>; a <c>varchar</c> column longer than this converts to <c>nvarchar(max)</c>.</summary>
    private const int MaxNvarcharLength = 4000;

    /// <summary>Checks the type found for the path column.</summary>
    /// <param name="table">The table, as the user typed it.</param>
    /// <param name="column">The path column, as the user typed it.</param>
    /// <param name="found">The system type name and <c>max_length</c> of the column, or <see langword="null" /> when the column (or table) does not exist.</param>
    /// <returns>
    ///     <see langword="null" /> for <c>nvarchar(n)</c> and <c>nvarchar(max)</c> (used as they are); <c>nvarchar(n)</c> for <c>varchar(n)</c> up to 4,000 characters and
    ///     <c>nvarchar(max)</c> for a longer <c>varchar(n)</c> and for <c>varchar(max)</c> (the type to convert to); otherwise <c>Sql.PathColumnNotFound</c> or
    ///     <c>Sql.PathColumnUnsupported</c>.
    /// </returns>
    public static ErrorOr<string?> Check(string table, string column, (string TypeName, short MaxLength)? found)
    {
        if (found is null)
        {
            return SqlErrors.PathColumnNotFound(column, table);
        }

        (string typeName, short maxLength) = found.Value;

        if (string.Equals(typeName, "nvarchar", StringComparison.OrdinalIgnoreCase))
        {
            return (string?)null;
        }

        if (string.Equals(typeName, "varchar", StringComparison.OrdinalIgnoreCase))
        {
            // max_length is in bytes, at least the number of characters (UTF-8 collations included); -1 is max.
            return maxLength is >= 1 and <= MaxNvarcharLength ? $"nvarchar({maxLength})" : "nvarchar(max)";
        }

        return SqlErrors.PathColumnUnsupported(column, Format(typeName, maxLength));
    }

    /// <summary>Formats a system type for the error text: lower case, with its length for the types that have one.</summary>
    /// <param name="typeName">The system type name.</param>
    /// <param name="maxLength">The column's <c>max_length</c> in bytes, or -1 for <c>max</c>.</param>
    /// <returns>For example <c>char(10)</c>, <c>nchar(10)</c>, <c>varbinary(max)</c> or <c>int</c>.</returns>
    private static string Format(string typeName, short maxLength)
    {
        string name = typeName.ToLowerInvariant();

        switch (name)
        {
            case "char" or "binary" or "varbinary":
                return maxLength is -1 ? $"{name}(max)" : $"{name}({maxLength})";

            case "nchar":
                return $"{name}({maxLength / 2})";

            default:
                return name;
        }
    }
}
