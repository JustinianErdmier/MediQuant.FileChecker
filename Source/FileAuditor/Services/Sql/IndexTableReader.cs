namespace FileAuditor.Services.Sql;

/// <summary>Counts and streams the rows of the file index table.</summary>
public sealed class IndexTableReader
{
    /// <summary>Counts the rows in the index table.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="query">The audit query.</param>
    /// <param name="cancellationToken">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>The row count, or <c>Sql.QueryFailed</c> when SQL Server reports an error.</returns>
    public async Task<ErrorOr<long>> CountAsync(string connectionString, AuditQuery query, CancellationToken cancellationToken)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(query.CountSql, connection);

            command.CommandTimeout = 0;

            await using CancellationTokenRegistration registration = cancellationToken.Register(command.Cancel);

            object? result = await command.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
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

    /// <summary>Streams every row of the index table to a callback.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="query">The audit query.</param>
    /// <param name="onRow">Called once per row, in order.</param>
    /// <param name="cancellationToken">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>Success, or <c>Sql.QueryFailed</c> when SQL Server reports an error.</returns>
    public async Task<ErrorOr<Success>> ReadAsync(string connectionString, AuditQuery query, Func<IndexRow, ValueTask> onRow, CancellationToken cancellationToken)
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
    public async Task<ErrorOr<Success>> ReadPathsAsync(string connectionString, PathQuery query, Action<string> onPath, CancellationToken cancellationToken)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(query.Sql, connection);

            command.CommandTimeout = 0;

            foreach (KeyValuePair<string, string> parameter in query.Parameters)
            {
                // Size 4000 keeps one cached plan for ordinary folders; -1 (max) only for a pattern too long for it.
                int size = parameter.Value.Length > 4000 ? -1 : 4000;

                command.Parameters.Add(new SqlParameter(parameter.Key, SqlDbType.NVarChar, size)
                {
                    Value = parameter.Value
                });
            }

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
