namespace FileAuditor.ViewModels.Main;

/// <summary>The main window: composes the four group view models and owns the Start, Stop, View Report, About and Exit commands.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private const int ProbeDebounceMilliseconds = 300;

    private const int RefreshMilliseconds = 250;

    private const int ProbeTimeoutMilliseconds = 2000;

    private readonly DialogueService _dialogues;

    private readonly PreflightChecker _preflight;

    private readonly AuditRunner _runner = new();

    private readonly ProjectSettingsService _project;

    private CancellationTokenSource? _auditCts;

    // The status of the last run (Completed, Stopped or Failed), shown in place of Ready until the next run starts.
    private string? _lastRunStatus;

    private Task _auditTask = Task.CompletedTask;

    private CancellationTokenSource? _probeCts;

    private bool _reportExists;

    // Set while the checkbox is being changed by the code rather than the user (reverting an untick, ticking after a Yes, resetting a project), so those changes do not
    // raise the untick warning.
    private bool _isRevertingTick;

    // True while Start, Open project or Close project is executing, including while their prompts are open. Those commands and closing the window must not overlap, and the
    // inputs must not be edited under them.
    private bool _isBusy;

    /// <summary>Initialises a new instance with the default settings, for the XAML designer.</summary>
    public MainWindowViewModel()
        : this(new DialogueService(() => null))
    { }

    private MainWindowViewModel(DialogueService dialogues)
        : this(dialogues, new ProjectSettingsService(new SettingsStore(), dialogues))
    { }

    /// <summary>Initialises a new instance populated with the default settings.</summary>
    /// <param name="dialogues">Shows the errors, confirmations and questions.</param>
    /// <param name="project">Holds the open project's settings file and saves it when an audit starts.</param>
    public MainWindowViewModel(DialogueService dialogues, ProjectSettingsService project)
    {
        AppSettings settings = AppSettings.Default;

        _dialogues = dialogues;
        _project   = project;
        _preflight = new PreflightChecker(dialogues);

        Database       = new DatabaseViewModel(settings);
        FileIndexTable = new FileIndexTableViewModel(settings);
        Report         = new ReportViewModel(settings);
        Audit          = new AuditViewModel();

        Database.PropertyChanged       += OnInputChanged;
        FileIndexTable.PropertyChanged += OnInputChanged;
        Report.PropertyChanged         += OnReportChanged;

        StartCommand.PropertyChanged        += OnCommandChanged;
        OpenProjectCommand.PropertyChanged  += OnCommandChanged;
        CloseProjectCommand.PropertyChanged += OnCommandChanged;

        UpdateIdleStatus();
        RefreshReportState();
    }

    /// <summary>Gets the Audit group and status bar.</summary>
    public AuditViewModel Audit { get; }

    /// <summary>Gets the Database group.</summary>
    public DatabaseViewModel Database { get; }

    /// <summary>Gets the File Index Table group.</summary>
    public FileIndexTableViewModel FileIndexTable { get; }

    /// <summary>Gets a value indicating whether the inputs are editable, that is, no audit is running and no Start, Open project or Close project is in progress.</summary>
    public bool IsEditable => !IsRunning && !_isBusy;

    /// <summary>Gets or sets a value indicating whether an audit is running; every input is disabled while it is.</summary>
    [ ObservableProperty ]
    public partial bool IsRunning { get; set; }

    /// <summary>Gets the Report group.</summary>
    public ReportViewModel Report { get; }

    /// <summary>Collects every input into a settings record.</summary>
    /// <returns>The current settings.</returns>
    public AppSettings Snapshot() => Report.ApplyTo(FileIndexTable.ApplyTo(Database.ApplyTo(AppSettings.Default)));

    /// <summary>
    ///     Re-probes whether the report file exists, for when it may have appeared or vanished outside the application. The probe is debounced and runs off the UI thread, because
    ///     the report folder may be on a slow or disconnected drive.
    /// </summary>
    public void RefreshReportState() => _ = ProbeReportAsync();

    /// <summary>
    ///     Called whenever the window is asked to close. With an audit running the user is asked first, and on Yes the run is stopped and its report written. The project's
    ///     settings are then saved or offered for saving under the same rule as Start. While Start, Open project or Close project is in progress (before any audit is running)
    ///     the close is refused, so two save points never run at once; the user can close once that command's prompt has been answered.
    /// </summary>
    /// <returns><see langword="true" /> when the window may close.</returns>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (IsRunning)
        {
            if (!await _dialogues.AskYesNoAsync(message: "An audit is in progress. Stop it and exit?"))
            {
                return false;
            }

            // The run may have ended while the question was open; cancelling a finished run is harmless.
            _auditCts?.Cancel();

            await _auditTask;
        }
        else if (_isBusy)
        {
            return false;
        }

        SavePointResult savePoint = await _project.RunSavePointAsync(Snapshot(), Report.Current, Report.SaveProjectSettings);

        return savePoint.Proceed;
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditable));
        UpdateCommands();
        RefreshReportState();
    }

    private void OnInputChanged(object? sender, PropertyChangedEventArgs e) => UpdateCommands();

    // Keeps the busy flag in step with the three commands that must not overlap.
    private void OnCommandChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IAsyncRelayCommand.IsRunning))
        {
            return;
        }

        _isBusy = StartCommand.IsRunning || OpenProjectCommand.IsRunning || CloseProjectCommand.IsRunning;

        OnPropertyChanged(nameof(IsEditable));
        UpdateCommands();
    }

    // Two Report properties matter here: the save checkbox (the untick warning) and the resolved report path (Current), which every other change to the report location
    // accompanies. Everything else is ignored.
    private void OnReportChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReportViewModel.SaveProjectSettings))
        {
            if (!Report.SaveProjectSettings
                && !_project.UntickConfirmed
                && !_isRevertingTick)
            {
                _ = ConfirmUntickAsync();
            }

            return;
        }

        if (e.PropertyName != nameof(ReportViewModel.Current))
        {
            return;
        }

        // The cached answer belongs to the old path, so drop it until the new path has been probed.
        _reportExists = false;
        UpdateCommands();
        RefreshReportState();
    }

    private async Task ConfirmUntickAsync()
    {
        const string message = "If you untick this box, this project's settings will no longer be saved automatically. You will be asked whether to save them instead.\n\n"
                               + "Do you want to untick it?";

        if (await _dialogues.ConfirmAsync(message, "_Untick"))
        {
            _project.UntickConfirmed = true;

            return;
        }

        SetSaveProjectSettings(true);
    }

    private void SetSaveProjectSettings(bool value)
    {
        _isRevertingTick           = true;
        Report.SaveProjectSettings = value;
        _isRevertingTick           = false;
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
            if (cts.IsCancellationRequested
                || path != Report.Current)
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
        OpenProjectCommand.NotifyCanExecuteChanged();
        CloseProjectCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ViewReportCommand.NotifyCanExecuteChanged();
        UpdateIdleStatus();
    }

    // While no run is active the status bar reads Ready only when Start is enabled; a run's own phases and final status are set by RunAuditAsync.
    private void UpdateIdleStatus()
    {
        if (IsRunning
            || _isBusy)
        {
            return;
        }

        Audit.StatusMessage = _lastRunStatus ?? (CanStart() ? "Ready" : "Enter the required details");
    }

    private bool CanStart()
        => !IsRunning
           && !_isBusy
           && !string.IsNullOrWhiteSpace(Database.Server)
           && !string.IsNullOrWhiteSpace(Database.Database)
           && !string.IsNullOrWhiteSpace(FileIndexTable.Table)
           && !string.IsNullOrWhiteSpace(FileIndexTable.PathColumn)
           && Report.Current.IsComplete;

    private bool CanStop() => IsRunning;

    // Reads the cached probe result; the file system is never touched from here.
    private bool CanViewReport() => !IsRunning && Report.Current.IsComplete && _reportExists;

    // Stays "executing" for the whole run, which also stops a second Start while the pre-flight dialogues are open.
    [ RelayCommand(CanExecute = nameof(CanStart)) ]
    private async Task StartAsync()
    {
        AppSettings settings   = Snapshot();
        ReportPath? reportPath = await _preflight.RunAsync(settings);

        if (reportPath is null)
        {
            return;
        }

        SavePointResult savePoint = await _project.RunSavePointAsync(settings, reportPath, Report.SaveProjectSettings);

        if (!savePoint.Proceed)
        {
            return;
        }

        if (savePoint.TickCheckbox)
        {
            SetSaveProjectSettings(true);
        }

        _auditTask = RunAuditAsync(new AuditRequest(settings, reportPath));

        await _auditTask;
    }

    private async Task RunAuditAsync(AuditRequest request)
    {
        AuditProgress progress = new()
        {
            Phase = "Counting files..."
        };

        Stopwatch               clock = Stopwatch.StartNew();
        CancellationTokenSource cts   = new();

        _auditCts = cts;

        Audit.Reset();
        Audit.StatusMessage = progress.Phase;
        IsRunning           = true;

        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(RefreshMilliseconds)
        };

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
            outcome = Error.Unexpected(code: "Audit.Unexpected", ex.Message);
        }

        timer.Stop();
        clock.Stop();
        Refresh(progress, clock, isFinished: true);

        _auditCts = null;
        cts.Dispose();

        // The final status is shown before any error dialogue, so the window behind it already reads correctly.
        (string status, string? error) = Describe(outcome);

        _lastRunStatus      = status;
        Audit.StatusMessage = status;
        IsRunning           = false;

        if (error is not null)
        {
            await _dialogues.ShowErrorAsync(error);
        }
    }

    private void Refresh(AuditProgress progress, Stopwatch clock, bool isFinished)
    {
        Audit.Update(progress, clock.Elapsed, isFinished);

        if (!isFinished)
        {
            Audit.StatusMessage = progress.Phase;
        }
    }

    [ RelayCommand(CanExecute = nameof(CanStop)) ]
    private void Stop() => _auditCts?.Cancel();

    [ RelayCommand(CanExecute = nameof(CanViewReport)) ]
    private async Task ViewReportAsync()
    {
        string path = Report.Current.FullPath;

        try
        {
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            await _dialogues.ShowErrorAsync($"The report could not be opened:\n{PathText.Breakable(path)}\n\n{ex.Message}");
        }
    }

    private bool CanChangeProject() => !IsRunning && !_isBusy;

    [ RelayCommand(CanExecute = nameof(CanChangeProject)) ]
    private async Task CloseProjectAsync()
    {
        SavePointResult savePoint = await _project.RunSavePointAsync(Snapshot(), Report.Current, Report.SaveProjectSettings);

        if (!savePoint.Proceed)
        {
            return;
        }

        ResetProject();
    }

    [ RelayCommand(CanExecute = nameof(CanChangeProject)) ]
    private async Task OpenProjectAsync()
    {
        SavePointResult savePoint = await _project.RunSavePointAsync(Snapshot(), Report.Current, Report.SaveProjectSettings);

        if (!savePoint.Proceed)
        {
            return;
        }

        string startFolder = await ResolvePickerStartFolderAsync();
        ProjectFilePick? pick = await _dialogues.PickProjectFileAsync(startFolder);

        if (pick is null)
        {
            AbandonOpen(savePoint);

            return;
        }

        if (pick.LocalPath is not { } path)
        {
            AbandonOpen(savePoint);

            await _dialogues.ShowErrorAsync(
                $"The project could not be opened:\n{PathText.Breakable(pick.Name)}\n\nOnly files on a local or network drive can be opened.");

            return;
        }

        ErrorOr<AppSettings> loaded = await _project.LoadAsync(path);

        if (loaded.IsError)
        {
            AbandonOpen(savePoint);

            await _dialogues.ShowErrorAsync($"The project could not be opened:\n{PathText.Breakable(path)}\n\n{loaded.FirstError.Description}");

            return;
        }

        ResetProject();

        AppSettings settings = loaded.Value;

        Database.Load(settings);
        FileIndexTable.Load(settings);
        Report.Load(settings);

        _project.MarkLoaded(path, Snapshot());
    }

    // The save point may have saved and unticked-then-answered Yes; if the operation is abandoned the project stays open, so that tick must be shown.
    private void AbandonOpen(SavePointResult savePoint)
    {
        if (savePoint.TickCheckbox)
        {
            SetSaveProjectSettings(true);
        }
    }

    // The folder probe runs off the UI thread, since the report folder may be on a slow or disconnected drive, and is abandoned after two seconds.
    private async Task<string> ResolvePickerStartFolderAsync()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        ReportPath current = Report.Current;

        if (!current.IsComplete
            || !Path.IsPathFullyQualified(current.Directory))
        {
            return profile;
        }

        string directory = current.Directory;

        Task<bool> probe = Task.Run(() => Directory.Exists(directory));

        bool answered = await Task.WhenAny(probe, Task.Delay(ProbeTimeoutMilliseconds)) == probe;

        return answered && probe.Result ? directory : profile;
    }

    // Every group goes back to its defaults, as if the application had just been launched.
    private void ResetProject()
    {
        AppSettings defaults = AppSettings.Default;

        Database.Load(defaults);
        FileIndexTable.Load(defaults);
        Report.Load(defaults);

        _project.Reset();
        SetSaveProjectSettings(true);

        _lastRunStatus = null;
        Audit.Reset();

        UpdateIdleStatus();
        RefreshReportState();
    }

    [ RelayCommand ]
    private void About() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [ RelayCommand ]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    private static (string Status, string? Error) Describe(ErrorOr<AuditReport> outcome)
    {
        if (outcome.IsError)
        {
            Error first = outcome.FirstError;

            if (first.Code == AuditErrors.StoppedBeforeCheckingCode)
            {
                return ("Stopped", null);
            }

            bool readFailed = first.Code is SqlErrors.QueryFailedCode or AuditErrors.ReadFailedCode;

            return ("Failed", readFailed ? $"The audit could not read the file index table:\n\n{first.Description}" : first.Description);
        }

        AuditReport report = outcome.Value;

        return report.Result switch
        {
            AuditResultKind.Completed     => ("Completed", null),
            AuditResultKind.StoppedByUser => ("Stopped", null),
            var _                         => ("Failed", $"The audit failed part-way through:\n\n{report.FailureMessage}\n\nThe report so far has been written.")
        };
    }

    /// <summary>Raised when the user asks for the About box.</summary>
    public event EventHandler? AboutRequested;

    /// <summary>Raised when the user asks to exit the application.</summary>
    public event EventHandler? ExitRequested;
}
