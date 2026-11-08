namespace FileAuditor.Services.Settings;

/// <summary>Holds the open project's settings file and baseline, and runs the one save rule with its prompts.</summary>
public sealed class ProjectSettingsService
{
    private readonly SettingsStore _store;

    private readonly DialogueService _dialogues;

    private AppSettings _baseline = AppSettings.Default;

    // The save prompt was answered No; it is not asked again this session.
    private bool _saveDeclined;

    /// <summary>Initialises a new instance.</summary>
    /// <param name="store">Reads and writes the settings files.</param>
    /// <param name="dialogues">Shows the prompts.</param>
    public ProjectSettingsService(SettingsStore store, DialogueService dialogues)
    {
        _store     = store;
        _dialogues = dialogues;
    }

    /// <summary>Loads a project settings file off the UI thread.</summary>
    /// <param name="filePath">The full path of the settings file.</param>
    /// <returns>The settings, or <see cref="SettingsErrors.Unreadable" /> when the file cannot be read or is not a File Auditor project.</returns>
    public Task<ErrorOr<AppSettings>> LoadAsync(string filePath) => Task.Run(() => _store.Load(filePath));

    /// <summary>Gets the full path of the project's settings file, or <see langword="null" /> until one is opened or first saved.</summary>
    public string? FilePath { get; private set; }

    /// <summary>Gets or sets a value indicating whether the untick warning was confirmed this session.</summary>
    public bool UntickConfirmed { get; set; }

    /// <summary>Forgets the file, the baseline (back to <see cref="AppSettings.Default" />) and both remembered answers.</summary>
    public void Reset()
    {
        FilePath        = null;
        _baseline       = AppSettings.Default;
        _saveDeclined   = false;
        UntickConfirmed = false;
    }

    /// <summary>Records a successfully opened project.</summary>
    /// <param name="filePath">The file the project was opened from.</param>
    /// <param name="settings">The settings that were loaded; they become the baseline.</param>
    public void MarkLoaded(string filePath, AppSettings settings)
    {
        FilePath  = filePath;
        _baseline = settings;
    }

    /// <summary>Determines whether the current settings differ from the baseline.</summary>
    /// <param name="current">The current settings.</param>
    /// <returns><see langword="true" /> when they differ.</returns>
    public bool IsChanged(AppSettings current) => current != _baseline;

    /// <summary>Runs the save rule: saves the settings when they have changed and the rules allow it, asking whether and where to as needed.</summary>
    /// <param name="current">The current settings.</param>
    /// <param name="report">
    ///     The report folder, which decides where a new project file goes. Only its folder, completeness and custom flag are read, never its file name, so Start may pass
    ///     a direction's path and the other save points the shared folder from <see cref="ReportPathResolver.ResolveFolder" />.
    /// </param>
    /// <param name="saveTicked">Whether the "save project settings with the report" checkbox is ticked.</param>
    /// <returns>Whether the triggering operation may proceed, and whether the checkbox should be ticked.</returns>
    public async Task<SavePointResult> RunSavePointAsync(AppSettings current, ReportPath report, bool saveTicked)
    {
        if (!IsChanged(current))
        {
            return SavePointResult.Go;
        }

        bool tick = false;

        if (!saveTicked)
        {
            if (_saveDeclined)
            {
                return SavePointResult.Go;
            }

            if (!await _dialogues.AskYesNoAsync(message: "Do you want to save this project's settings?"))
            {
                _saveDeclined = true;

                return SavePointResult.Go;
            }

            tick = true;
        }

        SavePointResult Done() => tick ? SavePointResult.GoAndTick : SavePointResult.Go;

        // A built location is only usable when the project folder is a single valid folder name; Start checks this in the pre-flight, other save points here.
        bool reportUsable = report.IsComplete
                            && Path.IsPathFullyQualified(report.Directory)
                            && (report.IsCustom || ReportPathResolver.IsSingleFolderName(current.ProjectFolder.Trim()));

        string target;
        bool   own;

        if (FilePath is null)
        {
            if (!reportUsable)
            {
                return await CannotSaveAsync(report.DisplayDirectory, reason: "The report location is incomplete.", Done);
            }

            target = Path.Combine(report.Directory, SettingsStore.FileName);
            own    = false;
        }
        else if (reportUsable
                 && !SameFolder(Path.GetDirectoryName(FilePath) ?? string.Empty, report.Directory))
        {
            DialogueChoice choice = await _dialogues.ChooseAsync(
                $"The report folder has changed.\n\nThis project's settings were opened from:\n{PathText.Breakable(FilePath)}\n\n"
                + $"The report is now written to:\n{PathText.Breakable(report.DisplayDirectory)}\n\nWhere should the settings be saved?",
                DialogueIcon.Question,
                primaryText: "_Report Folder",
                secondaryText: "_Original File",
                tertiaryText: "Cancel");

            switch (choice)
            {
                case DialogueChoice.Primary:
                    target = Path.Combine(report.Directory, SettingsStore.FileName);
                    own    = false;

                    break;
                case DialogueChoice.Secondary:
                    target = FilePath;
                    own    = true;

                    break;
                default:
                    return SavePointResult.Cancel;
            }
        }
        else
        {
            target = FilePath;
            own    = true;
        }

        string folder = Path.GetDirectoryName(target) ?? string.Empty;

        if (!await Task.Run(() => Directory.Exists(folder)))
        {
            return await CannotSaveAsync(target, reason: "The folder does not exist.", Done);
        }

        if (!own
            && await Task.Run(() => File.Exists(target)))
        {
            DialogueChoice choice = await _dialogues.ChooseAsync(
                $"A project settings file already exists in this folder:\n{PathText.Breakable(target)}\n\nDo you want to overwrite it?",
                DialogueIcon.Warning,
                primaryText: "_Overwrite",
                secondaryText: "_Skip Save",
                tertiaryText: "Cancel");

            if (choice == DialogueChoice.Secondary)
            {
                return Done();
            }

            if (choice == DialogueChoice.Tertiary)
            {
                return SavePointResult.Cancel;
            }
        }

        ErrorOr<Success> saved = await Task.Run(() => _store.Save(current, target));

        if (saved.IsError)
        {
            return await CannotSaveAsync(target, SettingsErrors.BareDetail(saved.FirstError), Done);
        }

        FilePath  = target;
        _baseline = current;

        return Done();
    }

    private async Task<SavePointResult> CannotSaveAsync(string path, string reason, Func<SavePointResult> done)
    {
        DialogueChoice choice = await _dialogues.ChooseAsync(
            $"This project's settings cannot be saved:\n{PathText.Breakable(path)}\n\n{reason}",
            DialogueIcon.Error,
            primaryText: "_Skip Save",
            secondaryText: "Cancel");

        return choice == DialogueChoice.Primary ? done() : SavePointResult.Cancel;
    }

    // Compares the full paths, so a relative segment or a mixed separator does not make the same folder look different.
    private static bool SameFolder(string a, string b)
    {
        try
        {
            a = Path.GetFullPath(a);
            b = Path.GetFullPath(b);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Compare the paths as given.
        }

        return string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);
    }
}
