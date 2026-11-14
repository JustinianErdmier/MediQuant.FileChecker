namespace FileAuditor.Models.Auditing;

/// <summary>How the rows of the index table divided when the run ended: the figures the report adds up.</summary>
/// <param name="TableRows">Every row of the table, as counted when the run began.</param>
/// <param name="Distinct">The different real paths (or path and ID pairs) first seen.</param>
/// <param name="Duplicates">The rows whose path (or path and ID pair) had already been seen, each counted once.</param>
/// <param name="Skipped">The rows whose path is a skip marker.</param>
/// <param name="EmptyPaths">The rows whose path is <c>NULL</c> or only white space.</param>
public sealed record RowCounts(long TableRows, long Distinct, long Duplicates, long Skipped, long EmptyPaths);
