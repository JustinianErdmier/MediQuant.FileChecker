namespace FileAuditor.ViewModels.Main;

/// <summary>
///     The main window: composes the four group view models and owns the Start, Stop, View Report, About and Exit commands.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private const int ProbeDebounceMilliseconds = 300;

    private CancellationTokenSource? _probeCts;

    private bool _reportExists;

    /// <summary>Initialises a new instance with the default settings, for the XAML designer.</summary>
    public MainWindowViewModel()
        : this(AppSettings.Default)
    {
    }

    /// <summary>Initialises a new instance populated from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    public MainWindowViewModel(AppSettings settings)
    {
        Database = new DatabaseViewModel(settings);
        FileIndexTable = new FileIndexTableViewModel(settings);
        Report = new ReportViewModel(settings);
        Progress = new ProgressViewModel();

        Database.PropertyChanged += OnInputChanged;
        FileIndexTable.PropertyChanged += OnInputChanged;
        Report.PropertyChanged += OnReportChanged;

        RefreshReportState();
    }

    /// <summary>Raised when the user asks for the About box.</summary>
    public event EventHandler? AboutRequested;

    /// <summary>Raised when the user asks to exit the application.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Gets the Database group.</summary>
    public DatabaseViewModel Database { get; }

    /// <summary>Gets the File Index Table group.</summary>
    public FileIndexTableViewModel FileIndexTable { get; }

    /// <summary>Gets the Report group.</summary>
    public ReportViewModel Report { get; }

    /// <summary>Gets the Progress group and status bar.</summary>
    public ProgressViewModel Progress { get; }

    /// <summary>Gets or sets a value indicating whether an audit is running; every input is disabled while it is.</summary>
    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    /// <summary>Gets a value indicating whether the inputs are editable, that is, no audit is running.</summary>
    public bool IsEditable => !IsRunning;

    /// <summary>Collects every input into a settings record.</summary>
    /// <returns>The current settings.</returns>
    public AppSettings Snapshot()
        => Report.ApplyTo(FileIndexTable.ApplyTo(Database.ApplyTo(AppSettings.Default)));

    /// <summary>
    ///     Re-probes whether the report file exists, for when it may have appeared or vanished outside the application. The probe is debounced and runs off the UI
    ///     thread, because the report folder may be on a slow or disconnected drive.
    /// </summary>
    public void RefreshReportState() => _ = ProbeReportAsync();

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditable));
        UpdateCommands();
        RefreshReportState();
    }

    private void OnInputChanged(object? sender, PropertyChangedEventArgs e) => UpdateCommands();

    // Only a change of the resolved report path matters here; every other Report property change accompanies one.
    private void OnReportChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ReportViewModel.Current))
        {
            return;
        }

        // The cached answer belongs to the old path, so drop it until the new path has been probed.
        _reportExists = false;
        UpdateCommands();
        RefreshReportState();
    }

    private async Task ProbeReportAsync()
    {
        _probeCts?.Cancel();

        CancellationTokenSource cts = new();
        _probeCts = cts;

        ReportPath path = Report.Current;

        try
        {
            await Task.Delay(ProbeDebounceMilliseconds, cts.Token);

            // File.Exists is acceptable here: it only decides whether the menu item and button are enabled, not an audit outcome.
            bool exists = path.IsComplete && await Task.Run(() => File.Exists(path.FullPath), cts.Token);

            // Back on the UI thread; a newer probe or a changed path makes this answer stale.
            if (cts.IsCancellationRequested || path != Report.Current)
            {
                return;
            }

            _reportExists = exists;
            ViewReportCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer probe.
        }
    }

    private void UpdateCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ViewReportCommand.NotifyCanExecuteChanged();
    }

    private bool CanStart()
        => !IsRunning
           && !string.IsNullOrWhiteSpace(Database.Server)
           && !string.IsNullOrWhiteSpace(Database.Database)
           && !string.IsNullOrWhiteSpace(FileIndexTable.Table)
           && !string.IsNullOrWhiteSpace(FileIndexTable.PathColumn)
           && Report.Current.IsComplete;

    private bool CanStop() => IsRunning;

    // Reads the cached probe result; the file system is never touched from here.
    private bool CanViewReport() => !IsRunning && Report.Current.IsComplete && _reportExists;

    // The body is supplied by the run-control task; only the CanExecute rule is final.
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
    }

    // The body is supplied by the run-control task; only the CanExecute rule is final.
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
    }

    [RelayCommand(CanExecute = nameof(CanViewReport))]
    private void ViewReport()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Report.Current.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // No program is associated with .txt or the file vanished; nothing sensible to do without a dialogue helper.
        }
    }

    [RelayCommand]
    private void About() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
