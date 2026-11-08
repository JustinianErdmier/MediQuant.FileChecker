namespace FileAuditor.Services.Reporting;

/// <summary>Works out where the report goes from the user's settings (SDD §5.3).</summary>
public static class ReportPathResolver
{
    private const string BuiltRoot = @"U:\Projects";

    private const string ProjectFolderPlaceholder = "<Project Folder>";

    /// <summary>
    ///     Resolves the report path for an audit direction. Built:
    ///     <c>U:\Projects\&lt;Project Folder&gt;\DE\&lt;Project Stage&gt;\FileAuditor&lt;Missing|Unindexed&gt;Report_&lt;Audit Stage&gt;.txt</c>, complete once the project folder is
    ///     filled in. Custom: the typed location and file name, complete once both are filled in.
    /// </summary>
    /// <param name="settings">The current settings.</param>
    /// <param name="directions">The audit direction the report belongs to.</param>
    /// <returns>The resolved report path.</returns>
    public static ReportPath Resolve(AppSettings settings, AuditDirections directions)
    {
        if (settings.UseCustomReportLocation)
        {
            string location = settings.CustomLocation.Trim();

            string fileName = directions is AuditDirections.Missing
                                  ? settings.MissingCustomFileName.Trim()
                                  : settings.UnindexedCustomFileName.Trim();

            return new ReportPath(location, fileName, location.Length > 0 && fileName.Length > 0, IsCustom: true);
        }

        string projectFolder = settings.ProjectFolder.Trim();

        AuditStages auditStages = settings.ProjectStages is ProjectStages.Sample
                                      ? AuditStages.Interim
                                      : settings.AuditStages;

        string directory = Path.Combine(BuiltRoot,
                                        projectFolder.Length > 0
                                            ? projectFolder
                                            : ProjectFolderPlaceholder,
                                        path3: "DE",
                                        settings.ProjectStages.ToString());

        return new ReportPath(directory, $"FileAuditor{directions}Report_{auditStages}.txt", projectFolder.Length > 0, IsCustom: false);
    }

    /// <summary>
    ///     Resolves the report folder alone, without a direction: the project file's folder follows the shared location, never a tab's file name (SDD §8.3). Built: the built folder,
    ///     complete once the project folder is filled in. Custom: the typed location, complete once it is not empty.
    /// </summary>
    /// <param name="settings">The current settings.</param>
    /// <returns>A path whose <see cref="ReportPath.FileName" /> is empty; only its folder, completeness and custom flag mean anything.</returns>
    public static ReportPath ResolveFolder(AppSettings settings)
    {
        if (settings.UseCustomReportLocation)
        {
            string location = settings.CustomLocation.Trim();

            return new ReportPath(location, string.Empty, location.Length > 0, IsCustom: true);
        }

        ReportPath built = Resolve(settings, AuditDirections.Missing);

        return built with
        {
            FileName = string.Empty
        };
    }

    /// <summary>Determines whether a project folder is a single, valid folder name: not empty, not rooted, no separators, not <c>.</c> or <c>..</c>, no invalid characters.</summary>
    /// <param name="folder">The trimmed project folder.</param>
    /// <returns><see langword="true" /> when it can safely be combined into the built report path.</returns>
    public static bool IsSingleFolderName(string folder)
        => folder.Length > 0
           && folder is not ("." or "..")
           && !Path.IsPathRooted(folder)
           && folder.IndexOfAny(['\\', '/'])                    < 0
           && folder.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
