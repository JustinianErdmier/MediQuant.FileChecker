namespace FileAuditor.Services.Sql;

/// <summary>Checks that the path column is <c>nvarchar</c>, the only type the audit statements classify without a cast.</summary>
public static class PathColumnType
{
    /// <summary>Checks the type found for the path column.</summary>
    /// <param name="table">The table, as the user typed it.</param>
    /// <param name="column">The path column, as the user typed it.</param>
    /// <param name="found">The system type name and <c>max_length</c> of the column, or <see langword="null" /> when the column (or table) does not exist.</param>
    /// <returns>Success for <c>nvarchar(n)</c> and <c>nvarchar(max)</c>; otherwise <c>Sql.PathColumnNotFound</c> or <c>Sql.PathColumnNotNvarchar</c>.</returns>
    public static ErrorOr<Success> Check(string table, string column, (string TypeName, short MaxLength)? found)
    {
        if (found is null)
        {
            return SqlErrors.PathColumnNotFound(column, table);
        }

        (string typeName, short maxLength) = found.Value;

        if (string.Equals(typeName, "nvarchar", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success;
        }

        return SqlErrors.PathColumnNotNvarchar(column, Format(typeName, maxLength));
    }

    /// <summary>Formats a system type for the error text: lower case, with its length for the types that have one.</summary>
    /// <param name="typeName">The system type name.</param>
    /// <param name="maxLength">The column's <c>max_length</c> in bytes, or -1 for <c>max</c>.</param>
    /// <returns>For example <c>varchar(400)</c>, <c>nchar(10)</c>, <c>varbinary(max)</c> or <c>int</c>.</returns>
    private static string Format(string typeName, short maxLength)
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
