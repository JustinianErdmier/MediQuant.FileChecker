namespace FileAuditor.Services.Sql;

/// <summary>Counts and streams the rows of the file index table.</summary>
public static class IndexTableReader
{
    /// <summary>Counts how the rows of the table divide into empty paths, skip markers, distinct real paths and duplicates.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="query">The audit query.</param>
    /// <param name="cancellationToken">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>The row counts, or <c>Sql.QueryFailed</c> when SQL Server reports an error.</returns>
    public static async Task<ErrorOr<RowCounts>> CountRowsAsync(string connectionString, AuditQuery query, CancellationToken cancellationToken)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(query.CountsSql, connection);

            command.CommandTimeout = 0;

            AddParameters(command, query.Parameters);

            await using CancellationTokenRegistration registration = cancellationToken.Register(command.Cancel);
            await using SqlDataReader                 reader       = await command.ExecuteReaderAsync(cancellationToken);

            await reader.ReadAsync(cancellationToken);

            return new RowCounts(reader.GetInt64(i: 0), reader.GetInt64(i: 1), reader.GetInt64(i: 2), reader.GetInt64(i: 3));
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

    /// <summary>Reads the ID of every row whose path is empty, as text.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="query">The audit query; its <see cref="AuditQuery.EmptyIdsSql" /> must not be <see langword="null" />.</param>
    /// <param name="cancellationToken">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>The IDs, with <c>NULL</c> as an empty string, or <c>Sql.QueryFailed</c> when SQL Server reports an error.</returns>
    public static async Task<ErrorOr<IReadOnlyList<string>>> ReadEmptyIdsAsync(string connectionString, AuditQuery query, CancellationToken cancellationToken)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(query.EmptyIdsSql, connection);

            command.CommandTimeout = 0;

            AddParameters(command, query.Parameters);

            await using CancellationTokenRegistration registration = cancellationToken.Register(command.Cancel);
            await using SqlDataReader                 reader       = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

            List<string> ids = [];

            try
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    ids.Add(await ReadStringAsync(reader, ordinal: 0, cancellationToken) ?? string.Empty);
                }
            }
            catch
            {
                // Closing a reader drains the remaining rows unless the command is cancelled first.
                command.Cancel();

                throw;
            }

            return ids;
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

    /// <summary>Streams the distinct real rows of the index table (neither empty nor skip markers) to a callback.</summary>
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

            AddParameters(command, query.Parameters);

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
