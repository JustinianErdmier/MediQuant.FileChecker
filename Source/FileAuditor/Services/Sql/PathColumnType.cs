namespace FileAuditor.Services.Sql;

/// <summary>Checks that the path column is <c>nvarchar</c> or <c>varchar</c> and says what the audit statements convert it to.</summary>
public static class PathColumnType
{
    /// <summary>The type a <c>varchar</c> column is converted to in the Unindexed folder filter, whatever its length: the filter tests only the start of the path.</summary>
    private const string FilterTarget = "nvarchar(4000)";

    /// <summary>Checks the type found for the path column.</summary>
    /// <param name="table">The table, as the user typed it.</param>
    /// <param name="column">The path column, as the user typed it.</param>
    /// <param name="found">The system type name and <c>max_length</c> of the column, or <see langword="null" /> when the column (or table) does not exist.</param>
    /// <returns>
    ///     <see langword="null" /> for <c>nvarchar</c> (used as it is); <c>nvarchar(4000)</c> for any <c>varchar</c> (the type the Unindexed folder filter converts to; the Missing
    ///     Files audit converts nothing); otherwise <c>Sql.PathColumnNotFound</c> or <c>Sql.PathColumnUnsupported</c>.
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
            return FilterTarget;
        }

        return SqlErrors.PathColumnUnsupported(column, Format(typeName, maxLength));
    }

    /// <summary>Formats a system type for the error text and the varchar warning: lower case, with its length for the types that have one.</summary>
    /// <param name="typeName">The system type name.</param>
    /// <param name="maxLength">The column's <c>max_length</c> in bytes, or -1 for <c>max</c>.</param>
    /// <returns>For example <c>char(10)</c>, <c>varchar(400)</c>, <c>nchar(10)</c>, <c>varbinary(max)</c> or <c>int</c>.</returns>
    public static string Format(string typeName, short maxLength)
    {
        string name = typeName.ToLowerInvariant();

        switch (name)
        {
            case "char" or "varchar" or "binary" or "varbinary":
                return maxLength is -1 ? $"{name}(max)" : $"{name}({maxLength})";

            case "nchar":
                return $"{name}({maxLength / 2})";

            default:
                return name;
        }
    }
}
