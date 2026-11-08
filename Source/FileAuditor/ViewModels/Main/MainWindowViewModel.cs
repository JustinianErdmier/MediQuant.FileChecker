namespace FileAuditor.ViewModels.Main;

/// <summary>The main window: composes the group and tab view models and owns the Start, Stop, View Report, About and Exit commands.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private const int ProbeDebounceMilliseconds = 300;

    private const int RefreshMilliseconds = 250;

    private const int ProbeTimeoutMilliseconds = 2000;

    private readonly DialogueService _dialogues;

    private readonly PreflightChecker _preflight;

    private readonly AuditRunner _missingRunner = new();

    private readonly UnindexedAuditRunner _unindexedRunner = new();

    private readonly ProjectSettingsService _project;

    // Each direction's last run status (Completed, Stopped or Failed), shown in place of Ready until that direction runs again.
    private readonly Dictionary<AuditDirection, string?> _lastRunStatus = new();

    private CancellationTokenSource? _auditCts;

    // The direction whose audit is running, or null when none is.
    private AuditDirection? _runningDirection;

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
        _preflight = new PreflightChecker(dialogues, project);

        Database        = new DatabaseViewModel(settings);
        FileIndexTable  = new FileIndexTableViewModel(settings);
        ReportLocation  = new ReportLocationState();
        ProjectSettings = new ProjectSettingsViewModel(ReportLocation, project);
        MissingFiles    = new MissingFilesTabViewModel(ReportLocation);
        UnindexedFiles  = new UnindexedFilesTabViewModel(ReportLocation, dialogues);

        Database.PropertyChanged            += OnInputChanged;
        FileIndexTable.PropertyChanged      += OnInputChanged;
        MissingFiles.PropertyChanged          += OnInputChanged;
        UnindexedFiles.PropertyChanged        += OnInputChanged;
        ReportLocation.PropertyChanged        += OnLocationChanged;
        MissingFiles.Report.PropertyChanged   += OnReportChanged;
        UnindexedFiles.Report.PropertyChanged += OnReportChanged;

        StartCommand.PropertyChanged        += OnCommandChanged;
        OpenProjectCommand.PropertyChanged  += OnCommandChanged;
        CloseProjectCommand.PropertyChanged += OnCommandChanged;

        UpdateIdleStatus();
        RefreshReportState();
    }

    /// <summary>Gets the Database group.</summary>
    public DatabaseViewModel Database { get; }

    /// <summary>Gets the File Index Table group.</summary>
    public FileIndexTableViewModel FileIndexTable { get; }

    /// <summary>Gets a value indicating whether the inputs are editable, that is, no audit is running and no Start, Open project or Close project is in progress.</summary>
    public bool IsEditable => !IsRunning && !_isBusy;

    /// <summary>Gets or sets a value indicating whether an audit is running; every input is disabled while it is.</summary>
    [ ObservableProperty ]
    public partial bool IsRunning { get; set; }

    /// <summary>Gets the Missing Files tab.</summary>
    public MissingFilesTabViewModel MissingFiles { get; }

    /// <summary>Gets the Project Settings group.</summary>
    public ProjectSettingsViewModel ProjectSettings { get; }

    /// <summary>Gets the Report group's values that every tab shares.</summary>
    public ReportLocationState ReportLocation { get; }

    /// <summary>Gets or sets the index of the selected tab. It is not saved with the project.</summary>
    [ ObservableProperty ]
    public partial int SelectedTabIndex { get; set; }

    /// <summary>Gets the Unindexed Files tab.</summary>
    public UnindexedFilesTabViewModel UnindexedFiles { get; }

    /// <summary>Gets the Audit group that drives the status bar: the running audit's while one runs, otherwise the selected tab's.</summary>
    public AuditStatusBase StatusAudit => AuditFor(_runningDirection ?? SelectedDirection);

    // Maps the selected tab to its direction; the tabs are in the order of the XAML.
    private AuditDirection SelectedDirection
        => SelectedTabIndex switch
        {
            1     => AuditDirection.Unindexed,
            var _ => AuditDirection.Missing
        };

    private ReportViewModel SelectedReport => ReportFor(SelectedDirection);

    /// <summary>Collects every input into a settings record.</summary>
    /// <returns>The current settings.</returns>
    public AppSettings Snapshot() => UnindexedFiles.ApplyTo(MissingFiles.ApplyTo(ReportLocation.ApplyTo(FileIndexTable.ApplyTo(Database.ApplyTo(AppSettings.Default)))));

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

        SavePointResult savePoint = await _project.RunSavePointAsync(Snapshot(), ReportLocation.ResolveFolder(), ReportLocation.SaveProjectSettings);

        return savePoint.Proceed;
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(StatusAudit));
        UpdateCommands();
        RefreshReportState();
    }

    // Each tab has its own Start, View Report and idle status, so a change of tab re-evaluates them all against the new tab's report.
    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(StatusAudit));

        _reportExists = false;

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

    // The shared location values matter to this class only for the save checkbox (the untick warning); every other change to them reaches it as a change of a report's
    // Current.
    private void OnLocationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ReportLocationState.SaveProjectSettings)
            || ReportLocation.SaveProjectSettings
            || _project.UntickConfirmed
            || _isRevertingTick)
        {
            return;
        }

        _ = ConfirmUntickAsync();
    }

    // The resolved report path (Current) accompanies every change to a report's location or file name; everything else is ignored.
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
        _isRevertingTick                   = true;
        ReportLocation.SaveProjectSettings = value;
        _isRevertingTick                   = false;
    }

    private async Task ProbeReportAsync()
    {
        _probeCts?.Cancel();

        CancellationTokenSource cts = new();

        _probeCts = cts;

        ReportViewModel report = SelectedReport;
        ReportPath      path   = report.Current;

        try
        {
            await Task.Delay(ProbeDebounceMilliseconds, cts.Token);

            // File.Exists is acceptable here: it only decides whether the menu item and button are enabled, not an audit outcome.
            bool exists = path.IsComplete && await Task.Run(() => File.Exists(path.FullPath), cts.Token);

            // Back on the UI thread; a newer probe, a changed path or a changed tab makes this answer stale.
            if (cts.IsCancellationRequested
                || report != SelectedReport
                || path != report.Current)
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

    // While no run is active each tab's status reads Ready only when its Start would be enabled; a run's own phases and final status are set by RunAuditAsync.
    private void UpdateIdleStatus()
    {
        if (IsRunning
            || _isBusy)
        {
            return;
        }

        foreach (AuditDirection direction in Enum.GetValues<AuditDirection>())
        {
            AuditFor(direction).StatusMessage = _lastRunStatus.GetValueOrDefault(direction) ?? (CanStartFor(direction) ? "Ready" : "Enter the required details");
        }
    }

    // Every direction has a tab. Checklist for adding another: the constructor's tab and Report PropertyChanged subscriptions (OnInputChanged and OnReportChanged); Snapshot;
    // LoadAll; ReportFor; AuditFor; SelectedDirection; StartAsync (the run and its pre-flight inputs); and the TabItem in MainWindow.axaml.
    private ReportViewModel ReportFor(AuditDirection direction)
        => direction switch
        {
            AuditDirection.Missing   => MissingFiles.Report,
            AuditDirection.Unindexed => UnindexedFiles.Report,
            var _                    => throw new ArgumentOutOfRangeException(nameof(direction), direction, message: null)
        };

    private AuditStatusBase AuditFor(AuditDirection direction)
        => direction switch
        {
            AuditDirection.Missing   => MissingFiles.Audit,
            AuditDirection.Unindexed => UnindexedFiles.Audit,
            var _                    => throw new ArgumentOutOfRangeException(nameof(direction), direction, message: null)
        };

    private bool CanStart() => CanStartFor(SelectedDirection);

    private bool CanStartFor(AuditDirection direction)
        => !IsRunning
           && !_isBusy
           && !string.IsNullOrWhiteSpace(Database.Server)
           && !string.IsNullOrWhiteSpace(Database.Database)
           && !string.IsNullOrWhiteSpace(FileIndexTable.Table)
           && !string.IsNullOrWhiteSpace(FileIndexTable.PathColumn)
           && ReportFor(direction).Current.IsComplete;

    private bool CanStop() => IsRunning;

    // Reads the cached probe result; the file system is never touched from here.
    private bool CanViewReport() => !IsRunning && SelectedReport.Current.IsComplete && _reportExists;

    // Stays "executing" for the whole run, which also stops a second Start while the pre-flight dialogues are open.
    [ RelayCommand(CanExecute = nameof(CanStart)) ]
    private async Task StartAsync()
    {
        AuditDirection direction = SelectedDirection;
        AppSettings settings     = Snapshot();
        ReportPath? reportPath   = await _preflight.RunAsync(settings, direction);

        if (reportPath is null)
        {
            return;
        }

        SavePointResult savePoint = await _project.RunSavePointAsync(settings, reportPath, ReportLocation.SaveProjectSettings);

        if (!savePoint.Proceed)
        {
            return;
        }

        if (savePoint.TickCheckbox)
        {
            SetSaveProjectSettings(true);
        }

        AuditRequest request = new(settings, reportPath);

        AuditRun run;

        if (direction == AuditDirection.Unindexed)
        {
            run = new UnindexedAuditRun(UnindexedFiles.Audit, _unindexedRunner, request);
        }
        else
        {
            run = new MissingAuditRun(MissingFiles.Audit, _missingRunner, request);
        }

        _auditTask = RunAuditAsync(run, direction);

        await _auditTask;
    }

    private async Task RunAuditAsync(AuditRun run, AuditDirection direction)
    {
        AuditStatusBase audit = run.Audit;

        Stopwatch               clock = Stopwatch.StartNew();
        CancellationTokenSource cts   = new();

        _auditCts = cts;

        audit.Reset();
        run.Begin();

        audit.StatusMessage = run.Phase;
        _runningDirection   = direction;
        audit.IsStopEnabled = true;
        IsRunning           = true;

        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(RefreshMilliseconds)
        };

        timer.Tick += (_, _) => Refresh(run, clock, isFinished: false);
        timer.Start();

        string? status = null;
        string? error  = null;

        try
        {
            (status, error) = await run.ExecuteAsync(cts.Token);

            timer.Stop();
            clock.Stop();
            Refresh(run, clock, isFinished: true);
        }
        finally
        {
            // The resets always happen, even if the final refresh throws, so the window is never left looking busy. The final status is shown before any error dialogue,
            // so the window behind it already reads correctly.
            timer.Stop();

            _auditCts = null;
            cts.Dispose();

            if (status is not null)
            {
                _lastRunStatus[direction] = status;
                audit.StatusMessage       = status;
            }

            _runningDirection   = null;
            audit.IsStopEnabled = false;
            IsRunning           = false;
        }
        if (error is not null)
        {
            await _dialogues.ShowErrorAsync(error);
        }
    }

    private static void Refresh(AuditRun run, Stopwatch clock, bool isFinished)
    {
        run.Update(clock.Elapsed, isFinished);

        if (!isFinished)
        {
            run.Audit.StatusMessage = run.Phase;
        }
    }

    [ RelayCommand(CanExecute = nameof(CanStop)) ]
    private void Stop() => _auditCts?.Cancel();

    [ RelayCommand(CanExecute = nameof(CanViewReport)) ]
    private async Task ViewReportAsync()
    {
        string path = SelectedReport.Current.FullPath;

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

    private static bool CanOpenHelpTopics() => !string.IsNullOrEmpty(HelpLinks.UserGuideUrl);

    [ RelayCommand(CanExecute = nameof(CanOpenHelpTopics)) ]
    private async Task HelpTopicsAsync()
    {
        try
        {
            Process.Start(new ProcessStartInfo(HelpLinks.UserGuideUrl)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            await _dialogues.ShowErrorAsync($"The user guide could not be opened:\n{PathText.Breakable(HelpLinks.UserGuideUrl)}\n\n{ex.Message}");
        }
    }

    private bool CanChangeProject() => !IsRunning && !_isBusy;

    [ RelayCommand(CanExecute = nameof(CanChangeProject)) ]
    private async Task CloseProjectAsync()
    {
        SavePointResult savePoint = await _project.RunSavePointAsync(Snapshot(), ReportLocation.ResolveFolder(), ReportLocation.SaveProjectSettings);

        if (!savePoint.Proceed)
        {
            return;
        }

        ResetProject();
    }

    [ RelayCommand(CanExecute = nameof(CanChangeProject)) ]
    private async Task OpenProjectAsync()
    {
        SavePointResult savePoint = await _project.RunSavePointAsync(Snapshot(), ReportLocation.ResolveFolder(), ReportLocation.SaveProjectSettings);

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

        LoadAll(loaded.Value);

        _project.MarkLoaded(path, Snapshot());
    }

    // The shared location is loaded before the tabs, so each tab's Report group refreshes against the new shared values.
    private void LoadAll(AppSettings settings)
    {
        Database.Load(settings);
        FileIndexTable.Load(settings);
        ReportLocation.Load(settings);
        MissingFiles.Load(settings);
        UnindexedFiles.Load(settings);
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
        ReportPath current = ReportLocation.ResolveFolder();

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
        LoadAll(AppSettings.Default);

        _project.Reset();
        SetSaveProjectSettings(true);

        _lastRunStatus.Clear();

        foreach (AuditDirection direction in Enum.GetValues<AuditDirection>())
        {
            AuditFor(direction)
                .Reset();
        }

        UpdateIdleStatus();
        RefreshReportState();
    }

    [ RelayCommand ]
    private void About() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [ RelayCommand ]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Raised when the user asks for the About box.</summary>
    public event EventHandler? AboutRequested;

    /// <summary>Raised when the user asks to exit the application.</summary>
    public event EventHandler? ExitRequested;
}
