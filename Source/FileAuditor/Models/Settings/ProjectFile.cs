namespace FileAuditor.Models.Settings;

/// <summary>
///     The shape of a project settings file. Each member is named like its JSON key and is nullable, because a hand-edited or damaged file can hold <see langword="null" /> for
///     any of them or leave it out.
/// </summary>
public sealed record ProjectFile
{
    /// <summary>Gets the audit stage.</summary>
    public AuditStages? AuditStage { get; init; }

    /// <summary>Gets the custom report folder.</summary>
    public string? CustomLocation { get; init; }

    /// <summary>Gets the database name.</summary>
    public string? Database { get; init; }

    /// <summary>Gets the Exclude blacklist.</summary>
    public string?[]? ExcludeTypes { get; init; }

    /// <summary>Gets the File types whitelist.</summary>
    public string?[]? FileTypes { get; init; }

    /// <summary>Gets a value indicating whether the unindexed report groups files by the extension found.</summary>
    public bool? GroupByFoundExtension { get; init; }

    /// <summary>Gets the name of the optional ID column.</summary>
    public string? IdColumn { get; init; }

    /// <summary>Gets the custom report file name of the missing-files audit.</summary>
    public string? MissingCustomFileName { get; init; }

    /// <summary>Gets the name of the column holding file paths.</summary>
    public string? PathColumn { get; init; }

    /// <summary>Gets the project folder used to build the report location.</summary>
    public string? ProjectFolder { get; init; }

    /// <summary>Gets the project stage.</summary>
    public ProjectStages? ProjectStage { get; init; }

    /// <summary>Gets a value indicating whether empty files are reported.</summary>
    public bool? ReportEmptyFiles { get; init; }

    /// <summary>Gets the folders the unindexed-files audit checks.</summary>
    public ProjectFileFolder?[]? ScanFolders { get; init; }

    /// <summary>Gets the SQL Server instance name.</summary>
    public string? Server { get; init; }

    /// <summary>Gets the file index table name.</summary>
    public string? Table { get; init; }

    /// <summary>Gets the custom report file name of the unindexed-files audit.</summary>
    public string? UnindexedCustomFileName { get; init; }

    /// <summary>Gets a value indicating whether the report is written to a custom location.</summary>
    public bool? UseCustomReportLocation { get; init; }
}
