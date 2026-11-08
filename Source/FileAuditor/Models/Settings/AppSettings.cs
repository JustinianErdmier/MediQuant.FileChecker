namespace FileAuditor.Models.Settings;

/// <summary>Every user input that File Auditor persists between sessions. Holds no credentials.</summary>
/// <remarks>
///     Declared with a positional constructor whose parameters carry the defaults because the source-generated JSON context binds init-only properties through the constructor,
///     and would otherwise give properties missing from the file the type's zero value instead of the default.
/// </remarks>
/// <param name="Server">The SQL Server instance name.</param>
/// <param name="Database">The database name.</param>
/// <param name="Table">The file index table name, optionally schema-qualified.</param>
/// <param name="PathColumn">The name of the column holding file paths.</param>
/// <param name="IdColumn">The name of the optional ID column.</param>
/// <param name="ReportEmptyFiles">Whether empty files are reported.</param>
/// <param name="ProjectFolder">The project folder used to build the report location.</param>
/// <param name="ProjectStage">The project stage.</param>
/// <param name="AuditStage">The audit stage.</param>
/// <param name="UseCustomReportLocation">Whether the report is written to a custom location.</param>
/// <param name="CustomLocation">The custom report folder, used when <paramref name="UseCustomReportLocation" /> is set.</param>
/// <param name="MissingCustomFileName">The custom report file name of the missing-files audit, used when <paramref name="UseCustomReportLocation" /> is set.</param>
/// <param name="ScanFolders">The folders the unindexed-files audit checks; <see langword="null" /> in a file means none.</param>
/// <param name="FileTypes">The File types whitelist, normalised; <see langword="null" /> in a file means the built-in defaults.</param>
/// <param name="ExcludeTypes">The Exclude blacklist, normalised; <see langword="null" /> in a file means none.</param>
/// <param name="GroupByFoundExtension">Whether the unindexed report groups files by the extension found rather than the listed type.</param>
/// <param name="UnindexedCustomFileName">The custom report file name of the unindexed-files audit, used when <paramref name="UseCustomReportLocation" /> is set.</param>
public sealed record AppSettings(string       Server                  = "",
                                 string       Database                = "",
                                 string       Table                   = "",
                                 string       PathColumn              = "",
                                 string       IdColumn                = "",
                                 bool         ReportEmptyFiles        = true,
                                 string       ProjectFolder           = "",
                                 ProjectStage ProjectStage            = ProjectStage.Final,
                                 AuditStage   AuditStage              = AuditStage.Interim,
                                 bool         UseCustomReportLocation = false,
                                 string       CustomLocation          = "",
                                 string       MissingCustomFileName   = "FileAuditorMissingReport.txt",
                                 ScanFolder[]? ScanFolders            = null,
                                 string[]?    FileTypes               = null,
                                 string[]?    ExcludeTypes            = null,
                                 bool         GroupByFoundExtension   = false,
                                 string       UnindexedCustomFileName = "FileAuditorUnindexedReport.txt")
{
    /// <summary>Gets the default settings: the settings a new or closed project starts from.</summary>
    public static AppSettings Default { get; } = new()
    {
        ScanFolders  = [],
        FileTypes    = [..FileTypeList.Defaults],
        ExcludeTypes = []
    };
}
