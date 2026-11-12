namespace FileAuditor.Models.Auditing;

/// <summary>How the rows of the index table divide into empty paths, skip markers, distinct real paths and duplicates.</summary>
/// <param name="TableRows">Every row of the table.</param>
/// <param name="EmptyPaths">The rows whose path is <c>NULL</c> or only white space.</param>
/// <param name="Skipped">The non-empty rows whose path is a skip marker.</param>
/// <param name="Distinct">The distinct real paths (or path and ID pairs) the audit checks.</param>
public sealed record RowCounts(long TableRows, long EmptyPaths, long Skipped, long Distinct)
{
    /// <summary>Gets the rows repeating a real path (or path and ID pair) already counted in <see cref="Distinct" />.</summary>
    public long Duplicates => TableRows - Distinct - EmptyPaths - Skipped;
}
