namespace FileAuditor.Services.Sql;

/// <summary>Counts and streams the rows of the file index table.</summary>
public static class IndexTableReader
{
    /// <summary>The SQL Server error numbers for an invalid column name and an invalid object name.</summary>
    private static readonly int[] NotFoundErrorNumbers = [207, 208];

    /// <summary>The SQL Server error number for a view or function that could not be bound because of earlier binding errors.</summary>
    private const int ViewBindingErrorNumber = 4413;

    /// <summary>Reads the system type and length of the path column as the audit statements see it.</summary>
    /// <remarks>
    ///     The type is read from the result-set metadata of <c>SELECT TOP (0) [fa_t].&lt;column&gt; FROM &lt;table&gt; AS [fa_t]</c> (<c>sp_describe_first_result_set</c>),
    ///     the shape the audit statements use, so permissions, synonyms (also to another database) and views behave exactly as they do for the statements: no permission is
    ///     reported as SQL Server reports it (error 229), not as a missing column.
    /// </remarks>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="tableSql">The quoted table name, as the audit statements use it.</param>
    /// <param name="columnSql">The quoted column name, as the audit statements use it.</param>
    /// <param name="cancellationToken">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>
    ///     The system type name (an alias type reports the type it is built on) and <c>max_length</c> in bytes (-1 for <c>max</c>); <see langword="null" /> when SQL Server reports
    ///     an invalid column or object name (errors 207 and 208); or <c>Sql.QueryFailed</c> for any other SQL Server error.
    /// </returns>
    public static async Task<ErrorOr<(string TypeName, short MaxLength)?>> ReadPathColumnTypeAsync(string            connectionString,
                                                                                                    string            tableSql,
                                                                                                    string            columnSql,
                                                                                                    CancellationToken cancellationToken)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new("EXEC sp_describe_first_result_set @tsql = @tsql;", connection);

            command.CommandTimeout = 0;

            command.Parameters.Add(new SqlParameter("@tsql", SqlDbType.NVarChar, -1)
            {
                Value = $"SELECT TOP (0) [fa_t].{columnSql} FROM {tableSql} AS [fa_t];"
            });

            await using CancellationTokenRegistration registration = cancellationToken.Register(command.Cancel);
            await using SqlDataReader                 reader       = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                (string TypeName, short MaxLength)? none = null;

                return none;
            }

            // system_type_name is the system type with its length ("nvarchar(300)" for an alias over it) and is NULL for a user-defined CLR type, which only has a user type
            // name (the built-in CLR types such as geography have a system type name).
            string? systemType = reader.IsDBNull(reader.GetOrdinal("system_type_name")) ? null : reader.GetString(reader.GetOrdinal("system_type_name"));
            string  typeName   = systemType ?? reader.GetString(reader.GetOrdinal("user_type_name"));
            int     paren      = typeName.IndexOf(value: '(');

            (string TypeName, short MaxLength)? found = (paren < 0 ? typeName : typeName[..paren], reader.GetInt16(reader.GetOrdinal("max_length")));

            return found;
        }
        catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // An invalid name is reported with its own error first, then wrapped (11501, 11529): the first error decides, so 229 and everything else stay a plain failure.
            // A view that no longer binds (error 4413 follows) also starts with 207 or 208, about a name inside the view, so it stays a plain failure with that message.
            if (NotFoundErrorNumbers.Contains(exception.Errors[index: 0].Number)
                && !exception.Errors.Cast<SqlError>().Any(error => error.Number == ViewBindingErrorNumber))
            {
                (string TypeName, short MaxLength)? none = null;

                return none;
            }

            // The first error is the real one; the wrapper SQL Server adds after it (for example 11529) only repeats that the metadata could not be determined.
            return SqlErrors.QueryFailed(exception.Errors[index: 0].Message);
        }
        catch (SqlException)
        {
            // SqlClient may fault a cancelled command with a SqlException; report it as the cancellation it is.
            throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <summary>Counts the rows of the table.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="query">The audit query.</param>
    /// <param name="cancellationToken">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>The number of rows, or <c>Sql.QueryFailed</c> when SQL Server reports an error.</returns>
    public static async Task<ErrorOr<long>> CountRowsAsync(string connectionString, AuditQuery query, CancellationToken cancellationToken)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(query.CountSql, connection);

            command.CommandTimeout = 0;

            await using CancellationTokenRegistration registration = cancellationToken.Register(command.Cancel);
            await using SqlDataReader                 reader       = await command.ExecuteReaderAsync(cancellationToken);

            await reader.ReadAsync(cancellationToken);

            return reader.GetInt64(i: 0);
        }
        catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return SqlErrors.QueryFailed(exception.Message);
        }
        catch (SqlException)
        {
            // SqlClient may fault a cancelled command with a SqlException; report it as the cancellation it is.
            throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <summary>Streams every row of the index table, in table order, to a callback; the first row arrives as soon as SQL Server starts the scan.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="query">The audit query.</param>
    /// <param name="onRow">Called once per row, in order.</param>
    /// <param name="cancellationToken">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>Success, or <c>Sql.QueryFailed</c> when SQL Server reports an error.</returns>
    public static async Task<ErrorOr<Success>> ReadAsync(string connectionString, AuditQuery query, Func<IndexRow, ValueTask> onRow, CancellationToken cancellationToken)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(query.SelectSql, connection);

            command.CommandTimeout = 0;

            await using CancellationTokenRegistration registration = cancellationToken.Register(command.Cancel);
            await using SqlDataReader                 reader       = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

            try
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    // SequentialAccess requires the columns to be read in order.
                    string  path = await ReadStringAsync(reader, ordinal: 0, cancellationToken) ?? string.Empty;
                    string? id   = query.HasIdColumn ? await ReadStringAsync(reader, ordinal: 1, cancellationToken) : null;

                    await onRow(new IndexRow(id, path));
                }
            }
            catch
            {
                // Closing a reader drains the remaining rows unless the command is cancelled first.
                command.Cancel();

                throw;
            }

            return Result.Success;
        }
        catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return SqlErrors.QueryFailed(exception.Message);
        }
        catch (SqlException)
        {
            // SqlClient may fault a cancelled command with a SqlException; report it as the cancellation it is.
            throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <summary>Streams the path column of the index table to a callback, restricted by the query's parameterised filter.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="query">The path query.</param>
    /// <param name="onPath">Called once per non-null path, one call at a time.</param>
    /// <param name="cancellationToken">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>Success, or <c>Sql.QueryFailed</c> when SQL Server reports an error.</returns>
    public static async Task<ErrorOr<Success>> ReadPathsAsync(string connectionString, PathQuery query, Action<string> onPath, CancellationToken cancellationToken)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(query.Sql, connection);

            command.CommandTimeout = 0;

            AddParameters(command, query.Parameters);

            await using CancellationTokenRegistration registration = cancellationToken.Register(command.Cancel);
            await using SqlDataReader                 reader       = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

            try
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    string? path = await ReadStringAsync(reader, ordinal: 0, cancellationToken);

                    if (path is not null)
                    {
                        onPath(path);
                    }
                }
            }
            catch
            {
                // Closing a reader drains the remaining rows unless the command is cancelled first.
                command.Cancel();

                throw;
            }

            return Result.Success;
        }
        catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return SqlErrors.QueryFailed(exception.Message);
        }
        catch (SqlException)
        {
            // SqlClient may fault a cancelled command with a SqlException; report it as the cancellation it is.
            throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <summary>Adds the statement's text parameters to a command as <c>nvarchar</c>.</summary>
    /// <param name="command">The command.</param>
    /// <param name="parameters">The parameter names and values.</param>
    private static void AddParameters(SqlCommand command, IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        foreach (KeyValuePair<string, string> parameter in parameters)
        {
            // Size 4000 keeps one cached plan for ordinary folders; -1 (max) only for a pattern too long for it.
            int size = parameter.Value.Length > 4000 ? -1 : 4000;

            command.Parameters.Add(new SqlParameter(parameter.Key, SqlDbType.NVarChar, size)
            {
                Value = parameter.Value
            });
        }
    }

    /// <summary>Reads one column of the current row as text, whatever its SQL type.</summary>
    /// <param name="reader">The reader, positioned on a row.</param>
    /// <param name="ordinal">The column's position; with sequential access the columns must be read in order.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The value as text, or <see langword="null" /> when the database value is <c>NULL</c>.</returns>
    private static async Task<string?> ReadStringAsync(SqlDataReader reader, int ordinal, CancellationToken cancellationToken)
    {
        if (await reader.IsDBNullAsync(ordinal, cancellationToken))
        {
            return null;
        }

        object value = await reader.GetFieldValueAsync<object>(ordinal, cancellationToken);

        return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
