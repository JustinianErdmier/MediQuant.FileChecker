namespace FileAuditor.Services.Reporting;

/// <summary>Works out where the report goes from the user's settings (SDD §5.3).</summary>
public static class ReportPathResolver
{
    private const string BuiltRoot = @"U:\Projects";

    private const string ProjectFolderPlaceholder = "<Project Folder>";

    /// <summary>
    ///     Resolves the report path for an audit direction. Built:
    ///     <c>U:\Projects\&lt;Project Folder&gt;\DE\&lt;Project Stage&gt;\FileAuditor&lt;Missing|Unindexed&gt;Report_&lt;Audit Stage&gt;.txt</c>,
    ///     complete once the project folder is filled in.
    ///     Custom: the typed location and file name, complete once both are filled in.
    /// </summary>
    /// <param name="settings">The current settings.</param>
    /// <param name="direction">The audit direction the report belongs to.</param>
    /// <returns>The resolved report path.</returns>
    public static ReportPath Resolve(AppSettings settings, AuditDirection direction)
    {
        if (settings.UseCustomReportLocation)
        {
            string location = settings.CustomLocation.Trim();
            string fileName = direction == AuditDirection.Missing ? settings.MissingCustomFileName.Trim() : settings.UnindexedCustomFileName.Trim();

            return new ReportPath(location, fileName, location.Length > 0 && fileName.Length > 0, IsCustom: true);
        }

        string     projectFolder = settings.ProjectFolder.Trim();
        AuditStage auditStage    = settings.ProjectStage == ProjectStage.Sample ? AuditStage.Interim : settings.AuditStage;
        string     directory     = Path.Combine(BuiltRoot, projectFolder.Length > 0 ? projectFolder : ProjectFolderPlaceholder, path3: "DE", settings.ProjectStage.ToString());

        return new ReportPath(directory, $"FileAuditor{direction}Report_{auditStage}.txt", projectFolder.Length > 0, IsCustom: false);
    }

    /// <summary>
    ///     Resolves the report folder alone, without a direction: the project file's folder follows the shared location, never a tab's file name (SDD §8.3). Built: the
    ///     built folder, complete once the project folder is filled in. Custom: the typed location, complete once it is not empty.
    /// </summary>
    /// <param name="settings">The current settings.</param>
    /// <returns>A path whose <see cref="ReportPath.FileName" /> is empty; only its folder, completeness and custom flag mean anything.</returns>
    public static ReportPath ResolveFolder(AppSettings settings)
    {
        if (settings.UseCustomReportLocation)
        {
            string location = settings.CustomLocation.Trim();

            return new ReportPath(location, FileName: "", location.Length > 0, IsCustom: true);
        }

        ReportPath built = Resolve(settings, AuditDirection.Missing);

        return built with
        {
            FileName = ""
        };
    }

    /// <summary>
    ///     Determines whether a project folder is a single, valid folder name: not empty, not rooted, no separators, not <c>.</c> or <c>..</c>, no invalid
    ///     characters.
    /// </summary>
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
