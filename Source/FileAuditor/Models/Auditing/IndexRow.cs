namespace FileAuditor.Models.Auditing;

/// <summary>
///     One row read from the file index table.
/// </summary>
/// <param name="Id">The value of the ID column, or <see langword="null" /> when no ID column is configured or the value is <c>NULL</c>.</param>
/// <param name="Path">The value of the path column; empty when the database value is <c>NULL</c>.</param>
public readonly record struct IndexRow(string? Id, string Path);
