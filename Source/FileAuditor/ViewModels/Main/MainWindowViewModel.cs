namespace FileAuditor.ViewModels.Main;

/// <summary>
///     The main window: composes the four group view models and owns the Start, Stop, View Report, About and Exit commands.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
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
        Report.PropertyChanged += OnInputChanged;
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

    /// <summary>Re-evaluates the View Report command, for when the report file may have appeared or vanished outside the application.</summary>
    public void RefreshReportState() => ViewReportCommand.NotifyCanExecuteChanged();

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditable));
        UpdateCommands();
    }

    private void OnInputChanged(object? sender, PropertyChangedEventArgs e) => UpdateCommands();

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

    // File.Exists is acceptable here: it only decides whether the menu item and button are enabled, not an audit outcome.
    private bool CanViewReport() => !IsRunning && Report.Current.IsComplete && File.Exists(Report.Current.FullPath);

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
