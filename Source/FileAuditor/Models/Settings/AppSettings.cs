namespace FileAuditor.Models.Settings;

/// <summary>Every user input that File Auditor persists between sessions. Holds no credentials and no nullable members.</summary>
public sealed record AppSettings
{
    /// <summary>Gets the audit stage.</summary>
    public AuditStages AuditStage { get; init; } = AuditStages.Interim;

    /// <summary>Gets the custom report folder, used when <see cref="UseCustomReportLocation" /> is set.</summary>
    public string CustomLocation { get; init; } = "";

    /// <summary>Gets the database name.</summary>
    public string Database { get; init; } = "";

    /// <summary>Gets the default settings: the settings a new or closed project starts from.</summary>
    public static AppSettings Default { get; } = new();

    /// <summary>Gets the Exclude blacklist, normalised.</summary>
    public string[] ExcludeTypes { get; init; } = [];

    /// <summary>Gets the File types whitelist, normalised.</summary>
    public string[] FileTypes { get; init; } = [.. FileTypeList.Defaults];

    /// <summary>Gets a value indicating whether the unindexed report groups files by the extension found rather than the listed type.</summary>
    public bool GroupByFoundExtension { get; init; }

    /// <summary>Gets the name of the optional ID column.</summary>
    public string IdColumn { get; init; } = "";

    /// <summary>Gets the custom report file name of the missing-files audit, used when <see cref="UseCustomReportLocation" /> is set.</summary>
    public string MissingCustomFileName { get; init; } = "FileAuditorMissingReport.txt";

    /// <summary>Gets the name of the column holding file paths.</summary>
    public string PathColumn { get; init; } = "";

    /// <summary>Gets the project folder used to build the report location.</summary>
    public string ProjectFolder { get; init; } = "";

    /// <summary>Gets the project stage.</summary>
    public ProjectStages ProjectStage { get; init; } = ProjectStages.Final;

    /// <summary>Gets a value indicating whether empty files are reported.</summary>
    public bool ReportEmptyFiles { get; init; } = true;

    /// <summary>Gets the folders the unindexed-files audit checks.</summary>
    public ScanFolder[] ScanFolders { get; init; } = [];

    /// <summary>Gets the SQL Server instance name.</summary>
    public string Server { get; init; } = "";

    /// <summary>Gets the file index table name, optionally schema-qualified.</summary>
    public string Table { get; init; } = "";

    /// <summary>Gets the custom report file name of the unindexed-files audit, used when <see cref="UseCustomReportLocation" /> is set.</summary>
    public string UnindexedCustomFileName { get; init; } = "FileAuditorUnindexedReport.txt";

    /// <summary>Gets a value indicating whether the report is written to a custom location.</summary>
    public bool UseCustomReportLocation { get; init; }
}
