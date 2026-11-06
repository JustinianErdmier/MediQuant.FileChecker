using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FileAuditor.Common.Errors;
using FileAuditor.Models.Auditing;
using Microsoft.Data.SqlClient;

namespace FileAuditor.Services.Sql;

/// <summary>
///     Counts and streams the rows of the file index table.
/// </summary>
public sealed class IndexTableReader
{
    /// <summary>
    ///     Counts the rows in the index table.
    /// </summary>
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

            await using SqlCommand command = new(query.CountSql, connection) { CommandTimeout = 0 };
            object? result = await command.ExecuteScalarAsync(ct);

            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        catch (SqlException ex)
        {
            return SqlErrors.QueryFailed(ex.Message);
        }
    }

    /// <summary>
    ///     Streams every row of the index table to a callback.
    /// </summary>
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

            await using SqlCommand command = new(query.SelectSql, connection) { CommandTimeout = 0 };
            await using SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

            while (await reader.ReadAsync(ct))
            {
                // SequentialAccess requires the columns to be read in order.
                string path = await ReadStringAsync(reader, 0, ct) ?? "";
                string? id = query.HasIdColumn ? await ReadStringAsync(reader, 1, ct) : null;

                await onRow(new IndexRow(id, path));
            }

            return Result.Success;
        }
        catch (SqlException ex)
        {
            return SqlErrors.QueryFailed(ex.Message);
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