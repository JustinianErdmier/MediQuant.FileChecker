namespace FileAuditor.Services.Reporting;

/// <summary>Works out where the report goes from the user's settings (SDD §5.3).</summary>
public static class ReportPathResolver
{
    private const string BuiltRoot = @"U:\Projects";

    private const string ProjectFolderPlaceholder = "<Project Folder>";

    private const string UnindexedDefaultFileName = "FileAuditorUnindexedReport.txt";

    /// <summary>
    ///     Resolves the report path for an audit direction. Built:
    ///     <c>U:\Projects\&lt;Project Folder&gt;\DE\&lt;Project Stage&gt;\FileAuditor&lt;Missing|Unindexed&gt;Report_&lt;Audit Stage&gt;.txt</c>, complete once the project folder is filled in.
    ///     Custom: the typed location and file name, complete once both are filled in.
    /// </summary>
    /// <remarks>
    ///     For <see cref="AuditDirection.Unindexed" /> with a custom location the file name is the built-in default <c>FileAuditorUnindexedReport.txt</c> for now;
    ///     the setting that lets the user change it arrives with the unindexed-files audit.
    /// </remarks>
    /// <param name="settings">The current settings.</param>
    /// <param name="direction">The audit direction the report belongs to.</param>
    /// <returns>The resolved report path.</returns>
    public static ReportPath Resolve(AppSettings settings, AuditDirection direction)
    {
        if (settings.UseCustomReportLocation)
        {
            string location = settings.CustomLocation.Trim();
            string fileName = direction == AuditDirection.Missing ? settings.MissingCustomFileName.Trim() : UnindexedDefaultFileName;

            return new ReportPath(location, fileName, location.Length > 0 && fileName.Length > 0, IsCustom: true);
        }

        string     projectFolder = settings.ProjectFolder.Trim();
        AuditStage auditStage    = settings.ProjectStage == ProjectStage.Sample ? AuditStage.Interim : settings.AuditStage;
        string     directory     = Path.Combine(BuiltRoot, projectFolder.Length > 0 ? projectFolder : ProjectFolderPlaceholder, path3: "DE", settings.ProjectStage.ToString());

        return new ReportPath(directory, $"FileAuditor{direction}Report_{auditStage}.txt", projectFolder.Length > 0, IsCustom: false);
    }

    /// <summary>Determines whether a project folder is a single, valid folder name: not empty, not rooted, no separators, not <c>.</c> or <c>..</c>, no invalid characters.</summary>
    /// <param name="folder">The trimmed project folder.</param>
    /// <returns><see langword="true" /> when it can safely be combined into the built report path.</returns>
    public static bool IsSingleFolderName(string folder)
        => folder.Length > 0
           && folder is not ("." or "..")
           && !Path.IsPathRooted(folder)
           && folder.IndexOfAny([
               '\\',
               '/'
           ])
           < 0
           && folder.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
