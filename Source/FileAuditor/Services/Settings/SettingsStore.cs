namespace FileAuditor.Services.Settings;

/// <summary>Loads and saves <see cref="AppSettings" /> as a project settings JSON file at a path the caller chooses.</summary>
public static class SettingsStore
{
    /// <summary>The name of a project settings file.</summary>
    public const string FileName = "FileAuditorSettings.json";

    /// <summary>Loads the settings from a file. Properties absent from the file take their defaults.</summary>
    /// <param name="filePath">The full path of the settings file.</param>
    /// <returns>The settings, or <see cref="SettingsErrors.Unreadable" /> when the file is missing, cannot be read or is not valid JSON.</returns>
    public static ErrorOr<AppSettings> Load(string filePath)
    {
        string json;

        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception exception) when (exception is IOException
                                                       or UnauthorizedAccessException
                                                       or NotSupportedException
                                                       or ArgumentException)
        {
            return SettingsErrors.Unreadable(exception.Message);
        }

        try
        {
            if (!LooksLikeProject(json))
            {
                return SettingsErrors.Unreadable(detail: "the file is not a File Auditor project.");
            }

            ProjectFile? file = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.ProjectFile);

            if (file is null)
            {
                return SettingsErrors.Unreadable(detail: "the file is empty or contains null.");
            }

            return ToSettings(file);
        }
        catch (JsonException exception)
        {
            return SettingsErrors.Unreadable(exception.Message);
        }
    }

    /// <summary>Saves the settings to a file, writing to a temporary file first and then moving it over the real one. The folder is never created.</summary>
    /// <param name="settings">The settings to save.</param>
    /// <param name="filePath">The full path of the settings file.</param>
    /// <returns><see cref="Result.Success" />, or <see cref="SettingsErrors.Unwritable" /> when the write fails, including when the folder does not exist.</returns>
    public static ErrorOr<Success> Save(AppSettings settings, string filePath)
    {
        string tempPath = $"{filePath}.tmp";

        try
        {
            File.WriteAllText(tempPath, Serialise(settings));
            File.Move(tempPath, filePath, overwrite: true);

            return Result.Success;
        }
        catch (Exception exception) when (exception is IOException
                                                       or UnauthorizedAccessException
                                                       or NotSupportedException
                                                       or ArgumentException)
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception deleteException) when (deleteException is IOException
                                                                       or UnauthorizedAccessException
                                                                       or NotSupportedException
                                                                       or ArgumentException)
            {
                // Best effort only; the original failure is the one to report.
            }

            return SettingsErrors.Unwritable(exception.Message);
        }
    }

    /// <summary>Serialises the settings as the text of a project settings file, which is also the form in which two sets of settings are compared by value.</summary>
    /// <param name="settings">The settings to serialise.</param>
    /// <returns>The indented JSON text.</returns>
    public static string Serialise(AppSettings settings) => JsonSerializer.Serialize(ToFile(settings), SettingsJsonContext.Default.ProjectFile);

    // A project file is a JSON object holding at least one of ProjectFile's keys. Anything else, such as another application's JSON or "{}", is refused rather than
    // opened as a project of defaults. Throws JsonException for invalid JSON.
    private static bool LooksLikeProject(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        HashSet<string> knownPropertyNames = SettingsJsonContext.Default.ProjectFile.Properties.Select(property => property.Name)
                                                   .ToHashSet(StringComparer.Ordinal);

        return document.RootElement.EnumerateObject()
                       .Any(property => knownPropertyNames.Contains(property.Name));
    }

    // Maps the settings to the file shape. Every member is set, so no null is written.
    private static ProjectFile ToFile(AppSettings settings)
        => new()
        {
            Server = settings.Server,
            Database = settings.Database,
            Table = settings.Table,
            PathColumn = settings.PathColumn,
            IdColumn = settings.IdColumn,
            ReportEmptyFiles = settings.ReportEmptyFiles,
            ProjectFolder = settings.ProjectFolder,
            ProjectStage = settings.ProjectStage,
            AuditStage = settings.AuditStage,
            UseCustomReportLocation = settings.UseCustomReportLocation,
            CustomLocation = settings.CustomLocation,
            MissingCustomFileName = settings.MissingCustomFileName,
            ScanFolders = [.. settings.ScanFolders.Select(folder => new ProjectFileFolder(folder.Path, folder.IncludeSubfolders))],
            FileTypes = [.. settings.FileTypes],
            ExcludeTypes = [.. settings.ExcludeTypes],
            GroupByFoundExtension = settings.GroupByFoundExtension,
            UnindexedCustomFileName = settings.UnindexedCustomFileName
        };

    // A hand-edited or damaged file can hold nulls or numeric enum values that name nothing; each such value falls back to its default so one bad property never takes
    // down the others.
    private static AppSettings ToSettings(ProjectFile file)
    {
        AppSettings defaults = AppSettings.Default;

        return new AppSettings
        {
            Server = file.Server ?? defaults.Server,
            Database = file.Database ?? defaults.Database,
            Table = file.Table ?? defaults.Table,
            PathColumn = file.PathColumn ?? defaults.PathColumn,
            IdColumn = file.IdColumn ?? defaults.IdColumn,
            ReportEmptyFiles = file.ReportEmptyFiles ?? defaults.ReportEmptyFiles,
            ProjectFolder = file.ProjectFolder ?? defaults.ProjectFolder,
            ProjectStage = file.ProjectStage is { } projectStage && Enum.IsDefined(projectStage) ? projectStage : defaults.ProjectStage,
            AuditStage = file.AuditStage is { } auditStage && Enum.IsDefined(auditStage) ? auditStage : defaults.AuditStage,
            UseCustomReportLocation = file.UseCustomReportLocation ?? defaults.UseCustomReportLocation,
            CustomLocation = file.CustomLocation ?? defaults.CustomLocation,
            MissingCustomFileName = file.MissingCustomFileName ?? defaults.MissingCustomFileName,
            ScanFolders = NormaliseFolders(file.ScanFolders),
            FileTypes = NormaliseTypes(file.FileTypes, defaults.FileTypes),
            ExcludeTypes = NormaliseTypes(file.ExcludeTypes, []),
            GroupByFoundExtension = file.GroupByFoundExtension ?? defaults.GroupByFoundExtension,
            UnindexedCustomFileName = file.UnindexedCustomFileName ?? defaults.UnindexedCustomFileName
        };
    }

    // A null list is empty; a null element or one without a path is dropped; a null IncludeSubfolders is true.
    private static ScanFolder[] NormaliseFolders(ProjectFileFolder?[]? folders)
    {
        if (folders is null)
        {
            return [];
        }

        List<ScanFolder> keptFolders = [];

        foreach (ProjectFileFolder? folder in folders)
        {
            if (folder?.Path is not null)
            {
                keptFolders.Add(new ScanFolder(folder.Path, folder.IncludeSubfolders ?? true));
            }
        }

        return [.. keptFolders];
    }

    // A null list takes the fallback (a copy, never the shared default array). A null element is dropped. Each valid entry is normalised; an invalid one is kept as
    // written (trimmed), so that the tab shows it, and the pre-flight names it when Start is pressed, instead of the file silently losing it.
    private static string[] NormaliseTypes(string?[]? types, string[] fallback)
    {
        if (types is null)
        {
            return [.. fallback];
        }

        SortedSet<string> keptTypes = new(StringComparer.Ordinal);

        foreach (string? type in types)
        {
            if (type is null)
            {
                continue;
            }

            ErrorOr<string[]> parsed = FileTypeList.Parse(type);

            if (!parsed.IsError)
            {
                keptTypes.UnionWith(parsed.Value);
            }
            else
            {
                keptTypes.Add(type.Trim());
            }
        }

        return [.. keptTypes];
    }
}
