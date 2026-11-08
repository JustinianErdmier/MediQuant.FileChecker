namespace FileAuditor.ViewModels.Groups;

/// <summary>The File Index Table group: which table and columns hold the file paths.</summary>
public sealed partial class FileIndexTableViewModel : ObservableObject
{
    /// <summary>Initialises a new instance with the default settings, for the XAML designer.</summary>
    public FileIndexTableViewModel()
        : this(AppSettings.Default)
    { }

    /// <summary>Initialises a new instance populated from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    public FileIndexTableViewModel(AppSettings settings)
    {
        Load(settings);
    }

    /// <summary>Gets or sets the name of the optional ID column.</summary>
    [ ObservableProperty ]
    public partial string IdColumn { get; set; } = "";

    /// <summary>Gets or sets the name of the column holding file paths.</summary>
    [ ObservableProperty ]
    public partial string PathColumn { get; set; } = "";

    /// <summary>Gets or sets the table name, optionally schema-qualified.</summary>
    [ ObservableProperty ]
    public partial string Table { get; set; } = "";

    /// <summary>Copies this group's inputs into the given settings.</summary>
    /// <param name="settings">The settings to start from.</param>
    /// <returns>A copy of <paramref name="settings" /> carrying this group's inputs.</returns>
    public AppSettings ApplyTo(AppSettings settings)
        => settings with
        {
            Table = Table,
            PathColumn = PathColumn,
            IdColumn = IdColumn
        };

    /// <summary>Sets this group's inputs from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    public void Load(AppSettings settings)
    {
        Table      = settings.Table;
        PathColumn = settings.PathColumn;
        IdColumn   = settings.IdColumn;
    }
}
