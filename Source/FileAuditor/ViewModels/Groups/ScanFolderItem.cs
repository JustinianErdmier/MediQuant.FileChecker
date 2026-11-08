namespace FileAuditor.ViewModels.Groups;

/// <summary>A folder in the Unindexed Files tab's list: its path and whether the folders beneath it are checked too.</summary>
public sealed partial class ScanFolderItem : ObservableObject
{
    /// <summary>Initialises a new instance.</summary>
    /// <param name="path">The folder's full path.</param>
    /// <param name="includeSubfolders">Whether the folders beneath it are checked too.</param>
    public ScanFolderItem(string path, bool includeSubfolders = true)
    {
        Path              = path;
        IncludeSubfolders = includeSubfolders;
    }

    /// <summary>Gets or sets a value indicating whether the folders beneath it are checked too.</summary>
    [ ObservableProperty ]
    public partial bool IncludeSubfolders { get; set; }

    /// <summary>Gets the folder's full path.</summary>
    public string Path { get; }
}
