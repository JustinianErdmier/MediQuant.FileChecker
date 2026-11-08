namespace FileAuditor.ViewModels.Tabs;

/// <summary>The Unindexed Files tab: its Folders to Check group, its direction's Report group and its Audit group.</summary>
public sealed partial class UnindexedFilesTabViewModel : ObservableObject
{
    /// <summary>Initialises a new instance with its own default shared state, for the XAML designer.</summary>
    public UnindexedFilesTabViewModel()
        : this(new ReportLocationState())
    { }

    /// <summary>Initialises a new instance over the shared report location.</summary>
    /// <param name="shared">The report location values every tab shares.</param>
    public UnindexedFilesTabViewModel(ReportLocationState shared)
    {
        Report = new ReportViewModel(shared, AuditDirection.Unindexed);
        Audit  = new UnindexedAuditViewModel();

        Folders.CollectionChanged += OnFoldersChanged;
    }

    /// <summary>Gets the Audit group.</summary>
    public UnindexedAuditViewModel Audit { get; }

    /// <summary>Gets or sets the Exclude box: the extension blacklist, parsed on Start and when saved.</summary>
    [ ObservableProperty ]
    public partial string ExcludeText { get; set; } = "";

    /// <summary>Gets or sets the File types box: the extension whitelist, parsed on Start and when saved.</summary>
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

    /// <summary>Gets the two boxes as typed, for the pre-flight to parse.</summary>
    public FileTypeText TypeText => new(FileTypesText, ExcludeText);

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

    /// <summary>Copies this tab's inputs into the given settings. Entries that are not valid are left out; the pre-flight reports them.</summary>
    /// <param name="settings">The settings to start from.</param>
    /// <returns>A copy of <paramref name="settings" /> carrying this tab's inputs.</returns>
    public AppSettings ApplyTo(AppSettings settings)
        => Report.ApplyTo(settings with
        {
            ScanFolders = Folders.Select(folder => new ScanFolder(folder.Path, folder.IncludeSubfolders))
                                 .ToArray(),
            FileTypes = ParseLeniently(FileTypesText),
            ExcludeTypes = ParseLeniently(ExcludeText)
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

    /// <summary>Rewrites both boxes in their normal form (lower case, a leading dot, sorted), once the pre-flight has accepted them.</summary>
    /// <param name="settings">The settings the pre-flight accepted.</param>
    public void Normalise(AppSettings settings)
    {
        FileTypesText = FileTypeList.Format(settings.FileTypes ?? []);
        ExcludeText   = FileTypeList.Format(settings.ExcludeTypes ?? []);
    }

    private static string[] ParseLeniently(string text)
    {
        ErrorOr<string[]> parsed = FileTypeList.Parse(text);

        if (!parsed.IsError)
        {
            return parsed.Value;
        }

        return text.Split([ ',', ' ', '\t', '\r', '\n' ], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(FileTypeList.Parse)
                   .Where(entry => !entry.IsError)
                   .SelectMany(entry => entry.Value)
                   .Distinct()
                   .Order(StringComparer.Ordinal)
                   .ToArray();
    }

    [ RelayCommand ]
    private void ResetTypes() => FileTypesText = FileTypeList.Format(FileTypeList.Defaults);

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
