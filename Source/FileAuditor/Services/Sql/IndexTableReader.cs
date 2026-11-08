namespace FileAuditor.Services.Sql;

/// <summary>Counts and streams the rows of the file index table.</summary>
public sealed class IndexTableReader
{
    /// <summary>Counts the rows in the index table.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="query">The audit query.</param>
    /// <param name="ct">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>The row count, or <c>Sql.QueryFailed</c> when SQL Server reports an error.</returns>
    public async Task<ErrorOr<long>> CountAsync(string connectionString, AuditQuery query, CancellationToken ct)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(ct);

            await using SqlCommand command = new(query.CountSql, connection)
            {
                CommandTimeout = 0
            };

            await using CancellationTokenRegistration registration = ct.Register(command.Cancel);
            object?                                   result       = await command.ExecuteScalarAsync(ct);

            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        catch (SqlException ex) when (!ct.IsCancellationRequested)
        {
            return SqlErrors.QueryFailed(ex.Message);
        }
        catch (SqlException)
        {
            // SqlClient may fault a cancelled command with a SqlException; report it as the cancellation it is.
            throw new OperationCanceledException(ct);
        }
    }

    /// <summary>Streams every row of the index table to a callback.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="query">The audit query.</param>
    /// <param name="onRow">Called once per row, in order.</param>
    /// <param name="ct">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>Success, or <c>Sql.QueryFailed</c> when SQL Server reports an error.</returns>
    public async Task<ErrorOr<Success>> ReadAsync(string connectionString, AuditQuery query, Func<IndexRow, ValueTask> onRow, CancellationToken ct)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(ct);

            await using SqlCommand command = new(query.SelectSql, connection)
            {
                CommandTimeout = 0
            };

            await using CancellationTokenRegistration registration = ct.Register(command.Cancel);
            await using SqlDataReader                 reader       = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

            try
            {
                while (await reader.ReadAsync(ct))
                {
                    // SequentialAccess requires the columns to be read in order.
                    string  path = await ReadStringAsync(reader, ordinal: 0, ct) ?? "";
                    string? id   = query.HasIdColumn ? await ReadStringAsync(reader, ordinal: 1, ct) : null;

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
        catch (SqlException ex) when (!ct.IsCancellationRequested)
        {
            return SqlErrors.QueryFailed(ex.Message);
        }
        catch (SqlException)
        {
            // SqlClient may fault a cancelled command with a SqlException; report it as the cancellation it is.
            throw new OperationCanceledException(ct);
        }
    }

    /// <summary>Streams the path column of the index table to a callback, restricted by the query's parameterised filter.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="query">The path query.</param>
    /// <param name="onPath">Called once per non-null path; runs on the calling thread's continuation, one call at a time.</param>
    /// <param name="ct">Cancels the operation; cancellation surfaces as <see cref="OperationCanceledException" />.</param>
    /// <returns>Success, or <c>Sql.QueryFailed</c> when SQL Server reports an error.</returns>
    public async Task<ErrorOr<Success>> ReadPathsAsync(string connectionString, PathQuery query, Action<string> onPath, CancellationToken ct)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);

            await connection.OpenAsync(ct);

            await using SqlCommand command = new(query.Sql, connection)
            {
                CommandTimeout = 0
            };

            foreach (KeyValuePair<string, string> parameter in query.Parameters)
            {
                // Size 4000 keeps one cached plan for ordinary folders; -1 (max) only for a pattern too long for it.
                int size = parameter.Value.Length > 4000 ? -1 : 4000;

                command.Parameters.Add(new SqlParameter(parameter.Key, SqlDbType.NVarChar, size)
                {
                    Value = parameter.Value
                });
            }

            await using CancellationTokenRegistration registration = ct.Register(command.Cancel);
            await using SqlDataReader                 reader       = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

            try
            {
                while (await reader.ReadAsync(ct))
                {
                    string? path = await ReadStringAsync(reader, ordinal: 0, ct);

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
        catch (SqlException ex) when (!ct.IsCancellationRequested)
        {
            return SqlErrors.QueryFailed(ex.Message);
        }
        catch (SqlException)
        {
            // SqlClient may fault a cancelled command with a SqlException; report it as the cancellation it is.
            throw new OperationCanceledException(ct);
        }
    }

    private static async Task<string?> ReadStringAsync(SqlDataReader reader, int ordinal, CancellationToken ct)
    {
        if (await reader.IsDBNullAsync(ordinal, ct))
        {
            return null;
        }

        object value = await reader.GetFieldValueAsync<object>(ordinal, ct);

        return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
