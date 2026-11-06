namespace FileAuditor.Services.Reporting;

/// <summary>
///     Works out where the report goes from the user's settings (SDD §5.3).
/// </summary>
public static class ReportPathResolver
{
    private const string BuiltRoot = @"U:\Projects";

    private const string ProjectFolderPlaceholder = "<Project Folder>";

    /// <summary>
    ///     Resolves the report path. Built: <c>U:\Projects\&lt;Project Folder&gt;\DE\&lt;Project Stage&gt;\FileAuditorReport_&lt;Audit Stage&gt;.txt</c>, complete once the
    ///     project folder is filled in. Custom: the typed location and file name, complete once both are filled in.
    /// </summary>
    /// <param name="settings">The current settings.</param>
    /// <returns>The resolved report path.</returns>
    public static ReportPath Resolve(AppSettings settings)
    {
        if (settings.UseCustomReportLocation)
        {
            string location = settings.CustomLocation.Trim();
            string fileName = settings.CustomFileName.Trim();

            return new ReportPath(location, fileName, location.Length > 0 && fileName.Length > 0, IsCustom: true);
        }

        string projectFolder = settings.ProjectFolder.Trim();
        AuditStage auditStage = settings.ProjectStage == ProjectStage.Sample ? AuditStage.Interim : settings.AuditStage;
        string directory = Path.Combine(BuiltRoot, projectFolder.Length > 0 ? projectFolder : ProjectFolderPlaceholder, "DE", settings.ProjectStage.ToString());

        return new ReportPath(directory, $"FileAuditorReport_{auditStage}.txt", projectFolder.Length > 0, IsCustom: false);
    }
}
