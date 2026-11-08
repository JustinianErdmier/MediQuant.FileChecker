namespace FileAuditor.ViewModels.Groups;

/// <summary>
///     One audit direction's Report group: the shared location values come from <see cref="ReportLocationState" />, and only the file name belongs to the direction
///     (SDD §5.3).
/// </summary>
public sealed partial class ReportViewModel : ObservableObject
{
    // The custom file name for this view model's own direction. Only ApplyTo and Load map it to the direction's settings property.
    private string _customFileName;

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
        Shared          = shared;
        Direction       = direction;
        _customFileName = direction == AuditDirection.Missing ? AppSettings.Default.MissingCustomFileName : AppSettings.Default.UnindexedCustomFileName;
        Current   =ReportPathResolver.Resolve(Resolved(), direction);

        Shared.PropertyChanged += OnSharedChanged;

        Refresh();
    }

    /// <summary>Gets the report path the current inputs resolve to.</summary>
    public ReportPath Current { get; private set; }

    /// <summary>Gets the audit direction whose report this group describes.</summary>
    public AuditDirection Direction { get; }

    /// <summary>Gets or sets a value indicating whether the unindexed report groups files by the extension found, not the listed type. Used by the Unindexed direction only.</summary>
    [ ObservableProperty ]
    [ NotifyPropertyChangedFor(nameof(GroupByListedType)) ]
    public partial bool GroupByFoundExtension { get; set; }

    /// <summary>Gets or sets a value indicating whether the unindexed report groups files by the listed type; the opposite of <see cref="GroupByFoundExtension" />.</summary>
    public bool GroupByListedType
    {
        get => !GroupByFoundExtension;

        set => GroupByFoundExtension = !value;
    }

    /// <summary>Gets a value indicating whether this is the Unindexed direction's group, which has the Group file types by option.</summary>
    public bool IsUnindexed => Direction == AuditDirection.Unindexed;

    /// <summary>Gets or sets the text of the File name box: a read-only preview when built, the typed name when custom.</summary>
    [ ObservableProperty ]
    public partial string FileName { get; set; } = "";

    /// <summary>Gets or sets the text of the Location box: a read-only preview when built, the typed folder when custom.</summary>
    [ ObservableProperty ]
    public partial string Location { get; set; } = "";

    /// <summary>Gets the location values shared with every direction.</summary>
    public ReportLocationState Shared { get; }

    /// <summary>
    ///     Copies this direction's own inputs (its custom file name, and for Unindexed the grouping choice) into the given settings.
    /// </summary>
    /// <param name="settings">The settings to start from.</param>
    /// <returns>A copy of <paramref name="settings" /> carrying this direction's inputs.</returns>
    public AppSettings ApplyTo(AppSettings settings)
        => Direction == AuditDirection.Missing
            ? settings with { MissingCustomFileName = _customFileName }
            : settings with { UnindexedCustomFileName = _customFileName, GroupByFoundExtension = GroupByFoundExtension };

    /// <summary>
    ///     Sets this direction's own inputs from the given settings, then refreshes from the shared state.
    /// </summary>
    /// <param name="settings">The settings to load.</param>
    public void Load(AppSettings settings)
    {
        _customFileName = Direction == AuditDirection.Missing ? settings.MissingCustomFileName : settings.UnindexedCustomFileName;

        GroupByFoundExtension = IsUnindexed && settings.GroupByFoundExtension;

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

        _customFileName = value;

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
        FileName      = Shared.UseCustomLocation ? _customFileName : path.FileName;
        _isRefreshing = false;

        UpdateCurrent();
    }

    private void UpdateCurrent()
    {
        Current = ReportPathResolver.Resolve(Resolved(), Direction);
        OnPropertyChanged(nameof(Current));
    }
}
