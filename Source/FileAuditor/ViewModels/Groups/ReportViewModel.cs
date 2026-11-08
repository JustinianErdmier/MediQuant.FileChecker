namespace FileAuditor.ViewModels.Groups;

/// <summary>
///     One audit direction's Report group: the shared location values come from <see cref="ReportLocationState" />, and only the file name belongs to the direction
///     (SDD §5.3).
/// </summary>
public sealed partial class ReportViewModel : ObservableObject
{
    private string _missingCustomFileName = AppSettings.Default.MissingCustomFileName;

    private bool _isRefreshing;

    /// <summary>Initialises a new instance with its own default shared state, for the XAML designer.</summary>
    public ReportViewModel()
        : this(new ReportLocationState(), AuditDirection.Missing)
    { }

    /// <summary>Initialises a new instance over the shared state.</summary>
    /// <param name="shared">The location values every direction shares.</param>
    /// <param name="direction">The audit direction whose report this group describes.</param>
    public ReportViewModel(ReportLocationState shared, AuditDirection direction)
    {
        Shared    = shared;
        Direction = direction;
        Current   = ReportPathResolver.Resolve(Resolved(), direction);

        Shared.PropertyChanged += OnSharedChanged;

        Refresh();
    }

    /// <summary>Gets the report path the current inputs resolve to.</summary>
    public ReportPath Current { get; private set; }

    /// <summary>Gets the audit direction whose report this group describes.</summary>
    public AuditDirection Direction { get; }

    /// <summary>Gets or sets the text of the File name box: a read-only preview when built, the typed name when custom.</summary>
    [ ObservableProperty ]
    public partial string FileName { get; set; } = "";

    /// <summary>Gets or sets the text of the Location box: a read-only preview when built, the typed folder when custom.</summary>
    [ ObservableProperty ]
    public partial string Location { get; set; } = "";

    /// <summary>Gets the location values shared with every direction.</summary>
    public ReportLocationState Shared { get; }

    /// <summary>Copies this direction's own inputs (its custom file name) into the given settings.</summary>
    /// <param name="settings">The settings to start from.</param>
    /// <returns>A copy of <paramref name="settings" /> carrying this direction's inputs.</returns>
    public AppSettings ApplyTo(AppSettings settings)
        => Direction == AuditDirection.Missing ? settings with { MissingCustomFileName = _missingCustomFileName } : settings;

    /// <summary>Sets this direction's own inputs from the given settings, then refreshes from the shared state.</summary>
    /// <param name="settings">The settings to load.</param>
    public void Load(AppSettings settings)
    {
        if (Direction == AuditDirection.Missing)
        {
            _missingCustomFileName = settings.MissingCustomFileName;
        }

        Refresh();
    }

    partial void OnLocationChanged(string value)
    {
        if (_isRefreshing
            || !Shared.UseCustomLocation)
        {
            return;
        }

        Shared.CustomLocation = value;
    }

    partial void OnFileNameChanged(string value)
    {
        if (_isRefreshing
            || !Shared.UseCustomLocation)
        {
            return;
        }

        if (Direction == AuditDirection.Missing)
        {
            _missingCustomFileName = value;
        }

        UpdateCurrent();
    }

    // Everything but the save tick can change the location, so the boxes and Current follow it.
    private void OnSharedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ReportLocationState.SaveProjectSettings))
        {
            Refresh();
        }
    }

    private AppSettings Resolved() => ApplyTo(Shared.ApplyTo(AppSettings.Default));

    /// <summary>Rewrites the Location and File name boxes for the current mode and recomputes <see cref="Current" />.</summary>
    private void Refresh()
    {
        ReportPath path = ReportPathResolver.Resolve(Resolved(), Direction);

        _isRefreshing = true;
        Location      = Shared.UseCustomLocation ? Shared.CustomLocation : path.DisplayDirectory;
        FileName      = Shared.UseCustomLocation ? _missingCustomFileName : path.FileName;
        _isRefreshing = false;

        UpdateCurrent();
    }

    private void UpdateCurrent()
    {
        Current = ReportPathResolver.Resolve(Resolved(), Direction);
        OnPropertyChanged(nameof(Current));
    }
}
