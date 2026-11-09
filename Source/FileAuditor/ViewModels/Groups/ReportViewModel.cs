namespace FileAuditor.ViewModels.Groups;

/// <summary>One audit direction's Report group: the shared location values come from <see cref="ReportLocationState" />, and only the file name belongs to the direction.</summary>
public sealed partial class ReportViewModel : ObservableObject
{
    /// <summary>
    ///     The custom file name for this view model's own direction. Only <see cref="ApplyTo" /> and <see cref="Load" /> map it to the direction's settings property.
    /// </summary>
    private string _customFileName;

    /// <summary>Whether <see cref="Refresh" /> is rewriting the boxes, so their change handlers do not treat that as typing.</summary>
    private bool _isRefreshing;

    /// <summary>Initialises a new instance with its own default shared state, for the XAML designer.</summary>
    public ReportViewModel()
        : this(new ReportLocationState(), AuditDirections.Missing)
    { }

    /// <summary>Initialises a new instance over the shared state.</summary>
    /// <param name="shared">The location values every direction shares.</param>
    /// <param name="direction">The audit direction whose report this group describes.</param>
    public ReportViewModel(ReportLocationState shared, AuditDirections direction)
    {
        Shared          = shared;
        Direction       = direction;
        _customFileName = direction is AuditDirections.Missing ? AppSettings.Default.MissingCustomFileName : AppSettings.Default.UnindexedCustomFileName;
        Current         = ReportPathResolver.Resolve(Resolved(), direction);

        Shared.PropertyChanged += OnSharedChanged;

        Refresh();
    }

    /// <summary>Gets the report path the current inputs resolve to.</summary>
    public ReportPath Current { get; private set; }

    /// <summary>Gets the audit direction whose report this group describes.</summary>
    public AuditDirections Direction { get; }

    /// <summary>Gets or sets the text of the File name box: a read-only preview when built, the typed name when custom.</summary>
    [ ObservableProperty ]
    public partial string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the unindexed report groups files by the found extension, not the listed type. Unindexed direction only.</summary>
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
    public bool IsUnindexed => Direction is AuditDirections.Unindexed;

    /// <summary>Gets or sets the text of the Location box: a read-only preview when built, the typed folder when custom.</summary>
    [ ObservableProperty ]
    public partial string Location { get; set; } = string.Empty;

    /// <summary>Gets the location values shared with every direction.</summary>
    public ReportLocationState Shared { get; }

    /// <summary>Copies this direction's own inputs (its custom file name, and for Unindexed the grouping choice) into the given settings.</summary>
    /// <param name="settings">The settings to start from.</param>
    /// <returns>A copy of <paramref name="settings" /> carrying this direction's inputs.</returns>
    public AppSettings ApplyTo(AppSettings settings)
        => Direction is AuditDirections.Missing
               ? settings with
               {
                   MissingCustomFileName = _customFileName
               }
               : settings with
               {
                   UnindexedCustomFileName = _customFileName,
                   GroupByFoundExtension = GroupByFoundExtension
               };

    /// <summary>Sets this direction's own inputs from the given settings, then refreshes from the shared state.</summary>
    /// <param name="settings">The settings to load.</param>
    public void Load(AppSettings settings)
    {
        _customFileName = Direction is AuditDirections.Missing ? settings.MissingCustomFileName : settings.UnindexedCustomFileName;

        GroupByFoundExtension = IsUnindexed && settings.GroupByFoundExtension;

        Refresh();
    }

    /// <summary>Stores the typed folder as the shared custom location while the custom location is in use.</summary>
    /// <param name="value">The new text of the Location box.</param>
    partial void OnLocationChanged(string value)
    {
        if (_isRefreshing
            || !Shared.UseCustomLocation)
        {
            return;
        }

        Shared.CustomLocation = value;
    }

    /// <summary>Stores the typed file name as this direction's custom file name while the custom location is in use.</summary>
    /// <param name="value">The new text of the File name box.</param>
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

    /// <summary>Refreshes the boxes and <see cref="Current" /> when a shared value changes. Everything but the save tick can change the location.</summary>
    /// <param name="sender">The shared state that changed.</param>
    /// <param name="eventArgs">The name of the property that changed.</param>
    private void OnSharedChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is not nameof(ReportLocationState.SaveProjectSettings))
        {
            Refresh();
        }
    }

    /// <summary>Gets the settings the shared values and this direction's inputs resolve to, on top of the defaults.</summary>
    /// <returns>The settings to resolve the report path from.</returns>
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

    /// <summary>Recomputes <see cref="Current" /> from the current inputs and tells the view.</summary>
    private void UpdateCurrent()
    {
        Current = ReportPathResolver.Resolve(Resolved(), Direction);

        OnPropertyChanged(nameof(Current));
    }
}
