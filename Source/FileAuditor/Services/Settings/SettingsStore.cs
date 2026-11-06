using FileAuditor.Common.Errors;
using FileAuditor.Common.Serialisation;
using FileAuditor.Models.Settings;

namespace FileAuditor.Services.Settings;

/// <summary>
///     Loads and saves <see cref="AppSettings" /> as JSON in <c>%APPDATA%\MediQuant\FileAuditor\settings.json</c>, per Windows user.
/// </summary>
public sealed class SettingsStore
{
    /// <summary>
    ///     Initialises a new instance that uses the default per-user settings path.
    /// </summary>
    public SettingsStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MediQuant", "FileAuditor", "settings.json"))
    {
    }

    /// <summary>
    ///     Initialises a new instance that uses the given settings file path.
    /// </summary>
    /// <param name="filePath">The full path of the settings file.</param>
    public SettingsStore(string filePath)
    {
        FilePath = filePath;
    }

    /// <summary>Gets the full path of the settings file.</summary>
    public string FilePath { get; }

    /// <summary>
    ///     Loads the settings. A missing file yields <see cref="AppSettings.Default" />; properties absent from the file take their defaults.
    /// </summary>
    /// <returns>
    ///     The settings, or <see cref="SettingsErrors.Unreadable" /> when the file cannot be read or is not valid JSON. Callers should treat that error as
    ///     "use <see cref="AppSettings.Default" />".
    /// </returns>
    public ErrorOr<AppSettings> Load()
    {
        string json;

        try
        {
            json = File.ReadAllText(FilePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return AppSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return SettingsErrors.Unreadable(ex.Message);
        }

        try
        {
            AppSettings? settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);

            if (settings is null)
            {
                return SettingsErrors.Unreadable("the file is empty or contains null.");
            }

            return settings;
        }
        catch (JsonException ex)
        {
            return SettingsErrors.Unreadable(ex.Message);
        }
    }

    /// <summary>
    ///     Saves the settings, writing to a temporary file first and then moving it over the real one.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <returns><see cref="Result.Success" />, or <see cref="SettingsErrors.Unwritable" /> when the write fails.</returns>
    public ErrorOr<Success> Save(AppSettings settings)
    {
        string tempPath = FilePath + ".tmp";

        try
        {
            string? directory = Path.GetDirectoryName(FilePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
            File.Move(tempPath, FilePath, overwrite: true);

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return SettingsErrors.Unwritable(ex.Message);
        }
    }
}
