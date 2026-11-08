namespace FileAuditor.Models.Auditing;

/// <summary>The direction of an audit: which side of the file index table and the disk is checked against the other.</summary>
/// <remarks>The member names form the report file names (<c>FileAuditor&lt;Name&gt;Report_...</c>), so renaming a member renames the reports users already have.</remarks>
public enum AuditDirection
{
    /// <summary>Every file the index table points to is checked to exist on disk.</summary>
    Missing,

    /// <summary>Every file on disk is checked to be pointed to by the index table.</summary>
    Unindexed
}
