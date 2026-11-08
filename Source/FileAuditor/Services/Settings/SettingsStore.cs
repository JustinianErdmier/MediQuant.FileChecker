namespace FileAuditor.Services.Settings;

/// <summary>Loads and saves <see cref="AppSettings" /> as a project settings JSON file at a path the caller chooses.</summary>
public sealed class SettingsStore
{
    /// <summary>The name of a project settings file.</summary>
    public const string FileName = "FileAuditorSettings.json";

    /// <summary>Loads the settings from a file. Properties absent from the file take their defaults.</summary>
    /// <param name="filePath">The full path of the settings file.</param>
    /// <returns>The settings, or <see cref="SettingsErrors.Unreadable" /> when the file is missing, cannot be read or is not valid JSON.</returns>
    public ErrorOr<AppSettings> Load(string filePath)
    {
        string json;

        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return SettingsErrors.Unreadable(ex.Message);
        }

        try
        {
            if (!LooksLikeProject(json))
            {
                return SettingsErrors.Unreadable(detail: "the file is not a File Auditor project.");
            }

            AppSettings? settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);

            if (settings is null)
            {
                return SettingsErrors.Unreadable(detail: "the file is empty or contains null.");
            }

            return Normalise(settings);
        }
        catch (JsonException ex)
        {
            return SettingsErrors.Unreadable(ex.Message);
        }
    }

    /// <summary>Saves the settings to a file, writing to a temporary file first and then moving it over the real one. The folder is never created.</summary>
    /// <param name="settings">The settings to save.</param>
    /// <param name="filePath">The full path of the settings file.</param>
    /// <returns><see cref="Result.Success" />, or <see cref="SettingsErrors.Unwritable" /> when the write fails, including when the folder does not exist.</returns>
    public ErrorOr<Success> Save(AppSettings settings, string filePath)
    {
        string tempPath = filePath + ".tmp";

        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
            File.Move(tempPath, filePath, overwrite: true);

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception deleteEx) when (deleteEx is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                // Best effort only; the original failure is the one to report.
            }

            return SettingsErrors.Unwritable(ex.Message);
        }
    }

    // A project file is a JSON object holding at least one of AppSettings' properties. Anything else, such as another application's JSON or "{}", is refused rather than
    // opened as a project of defaults. Throws JsonException for invalid JSON.
    private static bool LooksLikeProject(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        HashSet<string> known = SettingsJsonContext.Default.AppSettings.Properties.Select(property => property.Name)
                                                   .ToHashSet(StringComparer.Ordinal);

        return document.RootElement.EnumerateObject()
                       .Any(property => known.Contains(property.Name));
    }

    // A hand-edited or damaged file can hold null strings or numeric enum values that name nothing; each such value falls back to its default so one bad property
    // never takes down the others.
    private static AppSettings Normalise(AppSettings settings)
    {
        AppSettings defaults = AppSettings.Default;

        return settings with
        {
            Server = settings.Server               ?? defaults.Server,
            Database = settings.Database           ?? defaults.Database,
            Table = settings.Table                 ?? defaults.Table,
            PathColumn = settings.PathColumn       ?? defaults.PathColumn,
            IdColumn = settings.IdColumn           ?? defaults.IdColumn,
            ProjectFolder = settings.ProjectFolder ?? defaults.ProjectFolder,
            ProjectStage = Enum.IsDefined(settings.ProjectStage) ? settings.ProjectStage : defaults.ProjectStage,
            AuditStage = Enum.IsDefined(settings.AuditStage) ? settings.AuditStage : defaults.AuditStage,
            CustomLocation = settings.CustomLocation ?? defaults.CustomLocation,
            MissingCustomFileName = settings.MissingCustomFileName ?? defaults.MissingCustomFileName
        };
    }
}
