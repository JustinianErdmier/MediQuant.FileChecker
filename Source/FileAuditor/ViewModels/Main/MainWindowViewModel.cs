namespace FileAuditor.ViewModels.Main;

/// <summary>
///     The main window: composes the four group view models and owns the Start, Stop, View Report, About and Exit commands.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private const int ProbeDebounceMilliseconds = 300;

    private const int RefreshMilliseconds = 250;

    private readonly DialogueService _dialogues;

    private readonly SettingsStore _settingsStore;

    private readonly PreflightChecker _preflight;

    private readonly AuditRunner _runner = new();

    private CancellationTokenSource? _probeCts;

    private CancellationTokenSource? _auditCts;

    private Task _auditTask = Task.CompletedTask;

    private bool _reportExists;

    /// <summary>Initialises a new instance with the default settings, for the XAML designer.</summary>
    public MainWindowViewModel()
        : this(AppSettings.Default, new DialogueService(() => null), new SettingsStore())
    {
    }

    /// <summary>Initialises a new instance populated from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    /// <param name="dialogues">Shows the errors, confirmations and questions.</param>
    /// <param name="settingsStore">Saves the settings when an audit starts.</param>
    public MainWindowViewModel(AppSettings settings, DialogueService dialogues, SettingsStore settingsStore)
    {
        _dialogues = dialogues;
        _settingsStore = settingsStore;
        _preflight = new PreflightChecker(dialogues);

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

    // Stays "executing" for the whole run, which also stops a second Start while the pre-flight dialogues are open.
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        AppSettings settings = Snapshot();
        ReportPath? reportPath = await _preflight.RunAsync(settings);

        if (reportPath is null)
        {
            return;
        }

        // A failed save is deliberately ignored: the user loses nothing but the convenience of remembered inputs.
        _settingsStore.Save(settings);

        _auditTask = RunAuditAsync(new AuditRequest(settings, reportPath));

        await _auditTask;
    }

    private async Task RunAuditAsync(AuditRequest request)
    {
        AuditProgress progress = new() { Phase = "Counting files..." };
        Stopwatch clock = Stopwatch.StartNew();
        CancellationTokenSource cts = new();

        _auditCts = cts;

        Progress.Reset();
        Progress.StatusMessage = progress.Phase;
        IsRunning = true;

        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(RefreshMilliseconds) };

        timer.Tick += (_, _) => Refresh(progress, clock, isFinished: false);
        timer.Start();

        ErrorOr<AuditReport> outcome;

        try
        {
            // Off the UI thread, so the row stream's continuations never queue behind the dispatcher.
            outcome = await Task.Run(() => _runner.RunAsync(request, progress, cts.Token));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            outcome = Error.Unexpected("Audit.Unexpected", ex.Message);
        }

        timer.Stop();
        clock.Stop();
        Refresh(progress, clock, isFinished: true);

        _auditCts = null;
        cts.Dispose();

        // The final status is shown before any error dialogue, so the window behind it already reads correctly.
        (string status, string? error) = Describe(outcome);

        Progress.StatusMessage = status;
        IsRunning = false;

        if (error is not null)
        {
            await _dialogues.ShowErrorAsync(error);
        }
    }

    private static (string Status, string? Error) Describe(ErrorOr<AuditReport> outcome)
    {
        if (outcome.IsError)
        {
            Error first = outcome.FirstError;

            if (first.Code == AuditErrors.StoppedBeforeCheckingCode)
            {
                return ("Stopped", null);
            }

            return ("Failed", first.Code == SqlErrors.QueryFailedCode ? $"The audit could not read the file index table:\n\n{first.Description}" : first.Description);
        }

        AuditReport report = outcome.Value;

        return report.Result switch
        {
            AuditResultKind.Completed => ("Completed", null),
            AuditResultKind.StoppedByUser => ("Stopped", null),
            _ => ("Failed", $"The audit failed part-way through:\n\n{report.FailureMessage}\n\nThe report so far has been written.")
        };
    }

    private void Refresh(AuditProgress progress, Stopwatch clock, bool isFinished)
    {
        Progress.Update(progress, clock.Elapsed, isFinished);

        if (!isFinished)
        {
            Progress.StatusMessage = progress.Phase;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _auditCts?.Cancel();

    /// <summary>
    ///     Called when the window is asked to close. With no audit running the close may proceed; otherwise the user is asked, and on Yes the run is stopped and its
    ///     report written before the close may proceed.
    /// </summary>
    /// <returns><see langword="true" /> when the window may close.</returns>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (!IsRunning)
        {
            return true;
        }

        if (!await _dialogues.AskYesNoAsync("An audit is in progress. Stop it and exit?"))
        {
            return false;
        }

        // The run may have ended while the question was open; cancelling a finished run is harmless.
        _auditCts?.Cancel();

        await _auditTask;

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanViewReport))]
    private async Task ViewReportAsync()
    {
        string path = Report.Current.FullPath;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            await _dialogues.ShowErrorAsync($"The report could not be opened:\n{path}\n\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void About() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
