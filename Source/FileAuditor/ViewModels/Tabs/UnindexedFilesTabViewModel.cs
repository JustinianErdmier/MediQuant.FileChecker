namespace FileAuditor.ViewModels.Tabs;

/// <summary>The Unindexed Files tab: its Folders to Check group, its direction's Report group and its Audit group.</summary>
public sealed partial class UnindexedFilesTabViewModel : ObservableObject
{
    private readonly DialogueService? _dialogues;

    /// <summary>Initialises a new instance with its own default shared state, for the XAML designer.</summary>
    public UnindexedFilesTabViewModel()
        : this(new ReportLocationState())
    { }

    /// <summary>Initialises a new instance over the shared report location.</summary>
    /// <param name="shared">The report location values every tab shares.</param>
    /// <param name="dialogues">Shows the file types editor; <see langword="null" /> in the XAML designer.</param>
    public UnindexedFilesTabViewModel(ReportLocationState shared, DialogueService? dialogues = null)
    {
        _dialogues = dialogues;

        Report = new ReportViewModel(shared, AuditDirection.Unindexed);
        Audit  = new UnindexedAuditViewModel();

        Folders.CollectionChanged += OnFoldersChanged;
    }

    /// <summary>Gets the Audit group.</summary>
    public UnindexedAuditViewModel Audit { get; }

    /// <summary>Gets or sets the Exclude summary: the extension blacklist in normal form, changed only through its editor.</summary>
    [ ObservableProperty ]
    public partial string ExcludeText { get; set; } = "";

    /// <summary>Gets or sets the File types summary: the extension whitelist in normal form, changed only through its editor.</summary>
    [ ObservableProperty ]
    public partial string FileTypesText { get; set; } = FileTypeList.Format(FileTypeList.Defaults);

    /// <summary>Gets the folders to check, in the order listed.</summary>
    public ObservableCollection<ScanFolderItem> Folders { get; } = [];

    /// <summary>Gets the Report group for the unindexed-files direction.</summary>
    public ReportViewModel Report { get; }

    /// <summary>Gets or sets the selected row of the folder list.</summary>
    [ ObservableProperty ]
    [ NotifyCanExecuteChangedFor(nameof(RemoveCommand)) ]
    public partial ScanFolderItem? SelectedFolder { get; set; }

    /// <summary>Adds a folder to the list unless it is already there.</summary>
    /// <param name="path">The folder's full path.</param>
    /// <returns><see langword="true" /> when it was added; <see langword="false" /> when the same folder (ignoring case and a trailing separator) is already listed.</returns>
    public bool AddFolder(string path)
    {
        string key = Path.TrimEndingDirectorySeparator(path);

        if (Folders.Any(folder => string.Equals(Path.TrimEndingDirectorySeparator(folder.Path), key, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        ScanFolderItem item = new(path);

        Folders.Add(item);

        SelectedFolder = item;

        return true;
    }

    /// <summary>Copies this tab's inputs into the given settings.</summary>
    /// <param name="settings">The settings to start from.</param>
    /// <returns>A copy of <paramref name="settings" /> carrying this tab's inputs.</returns>
    public AppSettings ApplyTo(AppSettings settings)
        => Report.ApplyTo(settings with
        {
            ScanFolders = Folders.Select(folder => new ScanFolder(folder.Path, folder.IncludeSubfolders))
                                 .ToArray(),
            FileTypes = Entries(FileTypesText),
            ExcludeTypes = Entries(ExcludeText)
        });

    /// <summary>Sets this tab's inputs from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    public void Load(AppSettings settings)
    {
        Folders.Clear();

        foreach (ScanFolder folder in settings.ScanFolders ?? [])
        {
            Folders.Add(new ScanFolderItem(folder.Path, folder.IncludeSubfolders));
        }

        FileTypesText = FileTypeList.Format(settings.FileTypes ?? FileTypeList.Defaults);
        ExcludeText   = FileTypeList.Format(settings.ExcludeTypes ?? []);

        Report.Load(settings);
    }


    private static string[] Entries(string text) => FileTypeList.Split(text);

    [ RelayCommand ]
    private async Task EditFileTypesAsync()
    {
        string[]? edited = _dialogues is null ? null : await _dialogues.EditFileTypesAsync("Edit File Types", Entries(FileTypesText), canReset: true);

        if (edited is not null)
        {
            FileTypesText = FileTypeList.Format(edited);
        }
    }

    [ RelayCommand ]
    private async Task EditExcludeAsync()
    {
        string[]? edited = _dialogues is null ? null : await _dialogues.EditFileTypesAsync("Edit Exclude List", Entries(ExcludeText), canReset: false);

        if (edited is not null)
        {
            ExcludeText = FileTypeList.Format(edited);
        }
    }

    private bool CanRemove() => SelectedFolder is not null;

    [ RelayCommand(CanExecute = nameof(CanRemove)) ]
    private void Remove()
    {
        if (SelectedFolder is { } folder)
        {
            Folders.Remove(folder);
        }
    }

    // A change to the list or to a row's checkbox is an input change, like a change to either box.
    private void OnFoldersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (ScanFolderItem item in e.OldItems?.OfType<ScanFolderItem>() ?? [])
        {
            item.PropertyChanged -= OnFolderChanged;
        }

        foreach (ScanFolderItem item in e.NewItems?.OfType<ScanFolderItem>() ?? [])
        {
            item.PropertyChanged += OnFolderChanged;
        }

        OnPropertyChanged(nameof(Folders));
    }

    private void OnFolderChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Folders));
}
