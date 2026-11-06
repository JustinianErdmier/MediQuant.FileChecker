namespace FileAuditor.ViewModels.Groups;

/// <summary>The Database group: the SQL Server instance and database to read the file index table from.</summary>
public sealed partial class DatabaseViewModel : ObservableObject
{
    /// <summary>Initialises a new instance with the default settings, for the XAML designer.</summary>
    public DatabaseViewModel()
        : this(AppSettings.Default)
    { }

    /// <summary>Initialises a new instance populated from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    public DatabaseViewModel(AppSettings settings)
    {
        Load(settings);
    }

    /// <summary>Gets or sets the database name.</summary>
    [ ObservableProperty ]
    public partial string Database { get; set; } = "";

    /// <summary>Gets or sets the SQL Server instance name.</summary>
    [ ObservableProperty ]
    public partial string Server { get; set; } = "";

    /// <summary>Copies this group's inputs into the given settings.</summary>
    /// <param name="settings">The settings to start from.</param>
    /// <returns>A copy of <paramref name="settings" /> carrying this group's inputs.</returns>
    public AppSettings ApplyTo(AppSettings settings)
        => settings with
        {
            Server = Server,
            Database = Database
        };

    /// <summary>Sets this group's inputs from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    public void Load(AppSettings settings)
    {
        Server   = settings.Server;
        Database = settings.Database;
    }
}
