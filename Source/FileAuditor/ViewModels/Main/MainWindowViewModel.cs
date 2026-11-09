namespace FileAuditor.ViewModels.Main;

/// <summary>The main window: composes the group and tab view models and owns the Start, Stop, View Report, About and Exit commands.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>How long the report folder must stay unchanged before the probe for the report file runs, in milliseconds.</summary>
    private const int ProbeDebounceMilliseconds = 300;

    /// <summary>How long the folder probe for the Open Project picker may take before the user's profile folder is used instead, in milliseconds.</summary>
    private const int ProbeTimeoutMilliseconds = 2000;

    /// <summary>How often the Audit group is refreshed while a run is under way, in milliseconds.</summary>
    private const int RefreshMilliseconds = 250;

    /// <summary>Shows the errors, confirmations and questions.</summary>
    private readonly DialogueService _dialogues;

    /// <summary>Each direction's last run status (Completed, Stopped or Failed), shown in place of Ready until that direction runs again.</summary>
    private readonly Dictionary<AuditDirections, string?> _lastRunStatus = new();

    /// <summary>Runs the missing-files audit.</summary>
    private readonly AuditRunner _missingRunner = new();

    /// <summary>Checks the inputs when Start is pressed, before the database is touched.</summary>
    private readonly PreflightChecker _preflight;

    /// <summary>Holds the open project's settings file and saves it when an audit starts.</summary>
    private readonly ProjectSettingsService _projectSettingsService;

    /// <summary>Runs the unindexed-files audit.</summary>
    private readonly UnindexedAuditRunner _unindexedRunner = new();

    /// <summary>Stops the running audit; <see langword="null" /> when none is running.</summary>
    private CancellationTokenSource? _auditCancellationSource;

    /// <summary>The task of the running audit, awaited when the window closes so the report is written first.</summary>
    private Task _auditTask = Task.CompletedTask;

    /// <summary>
    ///     Whether Start, Open project or Close project is executing, including while their prompts are open. Those commands and closing the window must not overlap, and the
    ///     inputs must not be edited under them.
    /// </summary>
    private bool _isBusy;

    /// <summary>
    ///     Whether the checkbox is being changed by the code rather than the user (reverting an untick, ticking after a Yes, resetting a project), so those changes do not raise
    ///     the untick warning.
    /// </summary>
    private bool _isRevertingTick;

    /// <summary>Cancels the probe for the report file that a newer probe supersedes; <see langword="null" /> before the first probe.</summary>
    private CancellationTokenSource? _probeCancellationSource;

    /// <summary>The cached answer of the probe for the report file: whether the selected tab's report exists.</summary>
    private bool _reportExists;

    /// <summary>The direction whose audit is running, or <see langword="null" /> when none is.</summary>
    private AuditDirections? _runningDirection;

    /// <summary>Initialises a new instance with the default settings, for the XAML designer.</summary>
    public MainWindowViewModel()
        : this(new DialogueService(() => null))
    { }

    /// <summary>Initialises a new instance with the given dialogue service and a new project settings service that uses it.</summary>
    /// <param name="dialogues">Shows the errors, confirmations and questions.</param>
    private MainWindowViewModel(DialogueService dialogues)
        : this(dialogues, new ProjectSettingsService(dialogues))
    { }

    /// <summary>Initialises a new instance populated with the default settings.</summary>
    /// <param name="dialogues">Shows the errors, confirmations and questions.</param>
    /// <param name="projectSettingsService">Holds the open project's settings file and saves it when an audit starts.</param>
    public MainWindowViewModel(DialogueService dialogues, ProjectSettingsService projectSettingsService)
    {
        AppSettings settings = AppSettings.Default;

        _dialogues              = dialogues;
        _projectSettingsService = projectSettingsService;
        _preflight              = new PreflightChecker(dialogues, projectSettingsService);

        Database        = new DatabaseViewModel(settings);
        FileIndexTable  = new FileIndexTableViewModel(settings);
        ReportLocation  = new ReportLocationState();
        ProjectSettings = new ProjectSettingsViewModel(ReportLocation, projectSettingsService);
        MissingFiles    = new MissingFilesTabViewModel(ReportLocation);
        UnindexedFiles  = new UnindexedFilesTabViewModel(ReportLocation, dialogues);

        Database.PropertyChanged              += OnInputChanged;
        FileIndexTable.PropertyChanged        += OnInputChanged;
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

    /// <summary>Gets the Audit group that drives the status bar: the running audit's while one runs, otherwise the selected tab's.</summary>
    public AuditStatusBase StatusAudit => AuditFor(_runningDirection ?? SelectedDirection);

    /// <summary>Gets the Unindexed Files tab.</summary>
    public UnindexedFilesTabViewModel UnindexedFiles { get; }

    /// <summary>Gets the direction of the selected tab. The tabs are in the order of the XAML.</summary>
    private AuditDirections SelectedDirection
        => SelectedTabIndex switch
        {
            1     => AuditDirections.Unindexed,
            var _ => AuditDirections.Missing
        };

    /// <summary>Gets the Report group of the selected tab.</summary>
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
    ///     settings are then saved or offered for saving under the same rule as Start. While Start, Open project or Close project is in progress (before any audit is running) the
    ///     close is refused, so two save points never run at once; the user can close once that command's prompt has been answered.
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
            if (_auditCancellationSource is { } auditCancellationSource)
            {
                await auditCancellationSource.CancelAsync();
            }

            await _auditTask;
        }
        else if (_isBusy)
        {
            return false;
        }

        SavePointResult savePoint = await _projectSettingsService.RunSavePointAsync(Snapshot(), ReportLocation.ResolveFolder(), ReportLocation.SaveProjectSettings);

        return savePoint.Proceed;
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    /// <summary>Refreshes everything that depends on whether an audit is running.</summary>
    /// <param name="value">The new value of <see cref="IsRunning" />.</param>
    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(StatusAudit));
        UpdateCommands();
        RefreshReportState();
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    /// <summary>Re-evaluates Start, View Report and the idle status against the new tab's report, because each tab has its own.</summary>
    /// <param name="value">The new value of <see cref="SelectedTabIndex" />.</param>
    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(StatusAudit));

        _reportExists = false;

        UpdateCommands();
        RefreshReportState();
    }

    /// <summary>Re-evaluates the commands when an input changes.</summary>
    /// <param name="sender">The group that changed.</param>
    /// <param name="eventArgs">The name of the property that changed.</param>
    private void OnInputChanged(object? sender, PropertyChangedEventArgs eventArgs) => UpdateCommands();

    /// <summary>Keeps the busy flag in step with the three commands that must not overlap.</summary>
    /// <param name="sender">The command that changed.</param>
    /// <param name="eventArgs">The name of the property that changed.</param>
    private void OnCommandChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is not nameof(IAsyncRelayCommand.IsRunning))
        {
            return;
        }

        _isBusy = StartCommand.IsRunning || OpenProjectCommand.IsRunning || CloseProjectCommand.IsRunning;

        OnPropertyChanged(nameof(IsEditable));
        UpdateCommands();
    }

    /// <summary>
    ///     Raises the untick warning when the save checkbox is unticked. The shared location values matter to this class only for that; every other change to them reaches it as a
    ///     change of a report's <see cref="ReportViewModel.Current" />.
    /// </summary>
    /// <param name="sender">The shared state that changed.</param>
    /// <param name="eventArgs">The name of the property that changed.</param>
    private void OnLocationChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is not nameof(ReportLocationState.SaveProjectSettings)
            || ReportLocation.SaveProjectSettings
            || _projectSettingsService.UntickConfirmed
            || _isRevertingTick)
        {
            return;
        }

        _ = ConfirmUntickAsync();
    }

    /// <summary>Reacts to a change of a report's resolved path, which accompanies every change to its location or file name; everything else is ignored.</summary>
    /// <param name="sender">The report group that changed.</param>
    /// <param name="eventArgs">The name of the property that changed.</param>
    private void OnReportChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is not nameof(ReportViewModel.Current))
        {
            return;
        }

        // The cached answer belongs to the old path, so drop it until the new path has been probed.
        _reportExists = false;

        UpdateCommands();
        RefreshReportState();
    }

    /// <summary>Asks whether the save checkbox may be unticked, and ticks it again when the user declines.</summary>
    /// <returns>A task that completes when the question has been answered.</returns>
    private async Task ConfirmUntickAsync()
    {
        const string message = "If you untick this box, this project's settings will no longer be saved automatically. You will be asked whether to save them instead.\n\n"
                               + "Do you want to untick it?";

        if (await _dialogues.ConfirmAsync(message, confirmText: "_Untick"))
        {
            _projectSettingsService.UntickConfirmed = true;

            return;
        }

        SetSaveProjectSettings(value: true);
    }

    /// <summary>Sets the save checkbox from the code, without raising the untick warning.</summary>
    /// <param name="value">The new state of the checkbox.</param>
    private void SetSaveProjectSettings(bool value)
    {
        _isRevertingTick                   = true;
        ReportLocation.SaveProjectSettings = value;
        _isRevertingTick                   = false;
    }

    /// <summary>Probes, off the UI thread and after a short debounce, whether the selected tab's report file exists, and caches the answer for View Report.</summary>
    /// <returns>A task that completes when the probe has finished or been superseded.</returns>
    private async Task ProbeReportAsync()
    {
        CancellationTokenSource? previous = _probeCancellationSource;

        CancellationTokenSource cancellationTokenSource = new();

        _probeCancellationSource = cancellationTokenSource;

        if (previous is not null)
        {
            await previous.CancelAsync();
        }

        ReportViewModel report = SelectedReport;
        ReportPath      path   = report.Current;

        try
        {
            await Task.Delay(ProbeDebounceMilliseconds, cancellationTokenSource.Token);

            // File.Exists is acceptable here: it only decides whether the menu item and button are enabled, not an audit outcome.
            bool exists = path.IsComplete && await Task.Run(() => File.Exists(path.FullPath), cancellationTokenSource.Token);

            // Back on the UI thread; a newer probe, a changed path or a changed tab makes this answer stale.
            if (cancellationTokenSource.IsCancellationRequested
                || report != SelectedReport
                || path   != report.Current)
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

    /// <summary>Re-evaluates every command that depends on the inputs or on whether a run is under way, and the idle status.</summary>
    private void UpdateCommands()
    {
        OpenProjectCommand.NotifyCanExecuteChanged();
        CloseProjectCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ViewReportCommand.NotifyCanExecuteChanged();

        UpdateIdleStatus();
    }

    /// <summary>
    ///     Sets each tab's status while no run is active: Ready only when its Start would be enabled. A run's own phases and final status are set by <see cref="RunAuditAsync" />.
    /// </summary>
    private void UpdateIdleStatus()
    {
        if (IsRunning
            || _isBusy)
        {
            return;
        }

        foreach (AuditDirections direction in Enum.GetValues<AuditDirections>())
        {
            AuditFor(direction)
                .StatusMessage = _lastRunStatus.GetValueOrDefault(direction) ?? (CanStartFor(direction) ? "Ready" : "Enter the required details");
        }
    }

    /// <summary>Gets the Report group of a direction.</summary>
    /// <param name="direction">The audit direction.</param>
    /// <returns>The direction's Report group.</returns>
    /// <remarks>
    ///     Every direction has a tab. Checklist for adding another: the constructor's tab and Report PropertyChanged subscriptions (<c>OnInputChanged</c> and
    ///     <c>OnReportChanged</c>); <see cref="Snapshot" />; <c>LoadAll</c>; <c>ReportFor</c>; <c>AuditFor</c>; <c>SelectedDirection</c>; <c>StartAsync</c> (the run and its
    ///     pre-flight inputs); and the <c>TabItem</c> in <c>MainWindow.axaml</c>.
    /// </remarks>
    private ReportViewModel ReportFor(AuditDirections direction)
        => direction switch
        {
            AuditDirections.Missing   => MissingFiles.Report,
            AuditDirections.Unindexed => UnindexedFiles.Report,
            var _                     => throw new UnreachableException()
        };

    /// <summary>Gets the Audit group of a direction.</summary>
    /// <param name="direction">The audit direction.</param>
    /// <returns>The direction's Audit group.</returns>
    private AuditStatusBase AuditFor(AuditDirections direction)
        => direction switch
        {
            AuditDirections.Missing   => MissingFiles.Audit,
            AuditDirections.Unindexed => UnindexedFiles.Audit,
            var _                     => throw new UnreachableException()
        };

    /// <summary>Determines whether Start is available on the selected tab.</summary>
    /// <returns><see langword="true" /> when the selected tab's audit can start.</returns>
    private bool CanStart() => CanStartFor(SelectedDirection);

    /// <summary>Determines whether a direction's audit can start: nothing is running or busy, the required inputs are filled in and the report path is complete.</summary>
    /// <param name="direction">The audit direction.</param>
    /// <returns><see langword="true" /> when the audit can start.</returns>
    private bool CanStartFor(AuditDirections direction)
        => !IsRunning
           && !_isBusy
           && !string.IsNullOrWhiteSpace(Database.Server)
           && !string.IsNullOrWhiteSpace(Database.Database)
           && !string.IsNullOrWhiteSpace(FileIndexTable.Table)
           && !string.IsNullOrWhiteSpace(FileIndexTable.PathColumn)
           && ReportFor(direction)
              .Current.IsComplete;

    /// <summary>Determines whether Stop is available.</summary>
    /// <returns><see langword="true" /> while an audit is running.</returns>
    private bool CanStop() => IsRunning;

    /// <summary>Determines whether View Report is available. It reads the cached probe result; the file system is never touched from here.</summary>
    /// <returns><see langword="true" /> when no audit is running and the selected tab's report path is complete and the file exists.</returns>
    private bool CanViewReport() => !IsRunning && SelectedReport.Current.IsComplete && _reportExists;

    /// <summary>
    ///     Runs the pre-flight checks and the save point, then the audit. The command stays executing for the whole run, which also stops a second Start while the pre-flight
    ///     dialogues are open.
    /// </summary>
    /// <returns>A task that completes when the audit has ended.</returns>
    [ RelayCommand(CanExecute = nameof(CanStart)) ]
    private async Task StartAsync()
    {
        AuditDirections direction  = SelectedDirection;
        AppSettings     settings   = Snapshot();
        ReportPath?     reportPath = await _preflight.RunAsync(settings, direction);

        if (reportPath is null)
        {
            return;
        }

        SavePointResult savePoint = await _projectSettingsService.RunSavePointAsync(settings, reportPath, ReportLocation.SaveProjectSettings);

        if (!savePoint.Proceed)
        {
            return;
        }

        if (savePoint.TickCheckbox)
        {
            SetSaveProjectSettings(value: true);
        }

        AuditRequest request = new(settings, reportPath);

        AuditRun run;

        if (direction is AuditDirections.Unindexed)
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

    /// <summary>Runs one audit, refreshing the Audit group on a timer, and shows the final status and any error when it ends.</summary>
    /// <param name="run">The run to perform.</param>
    /// <param name="direction">The audit direction.</param>
    /// <returns>A task that completes when the run has ended and the window has been reset.</returns>
    private async Task RunAuditAsync(AuditRun run, AuditDirections direction)
    {
        AuditStatusBase audit = run.Audit;

        Stopwatch               clock                   = Stopwatch.StartNew();
        CancellationTokenSource cancellationTokenSource = new();

        _auditCancellationSource = cancellationTokenSource;

        audit.Reset();
        run.Begin();

        audit.StatusMessage = run.Phase;

        _runningDirection = direction;

        audit.IsStopEnabled = true;

        IsRunning = true;

        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(RefreshMilliseconds)
        };

        timer.Tick += (_, _) => Refresh(run, clock, isFinished: false);

        timer.Start();

        string? status = null;
        string? error;

        try
        {
            (status, error) = await run.ExecuteAsync(cancellationTokenSource.Token);

            timer.Stop();
            clock.Stop();

            Refresh(run, clock, isFinished: true);
        }
        finally
        {
            // The resets always happen, even if the final refresh throws, so the window is never left looking busy. The final status is shown before any error dialogue,
            // so the window behind it already reads correctly.
            timer.Stop();

            _auditCancellationSource = null;

            cancellationTokenSource.Dispose();

            if (status is not null)
            {
                _lastRunStatus[direction] = status;

                audit.StatusMessage = status;
            }

            _runningDirection = null;

            audit.IsStopEnabled = false;

            IsRunning = false;
        }

        if (error is not null)
        {
            await _dialogues.ShowErrorAsync(error);
        }
    }

    /// <summary>Stops the running audit; whatever was checked is reported, unless the run was stopped before checking began.</summary>
    [ RelayCommand(CanExecute = nameof(CanStop)) ]
    private void Stop() => _auditCancellationSource?.Cancel();

    /// <summary>Opens the selected tab's report in the program Windows associates with it.</summary>
    /// <returns>A task that completes when the report has been opened or the failure has been shown.</returns>
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
        catch (Exception exception) when (exception is Win32Exception
                                                       or InvalidOperationException
                                                       or FileNotFoundException)
        {
            await _dialogues.ShowErrorAsync($"The report could not be opened:\n{PathText.Breakable(path)}\n\n{exception.Message}");
        }
    }

    /// <summary>Opens the user guide in the default browser.</summary>
    /// <returns>A task that completes when the page has been opened or the failure has been shown.</returns>
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
        catch (Exception exception) when (exception is Win32Exception
                                                       or InvalidOperationException
                                                       or FileNotFoundException)
        {
            await _dialogues.ShowErrorAsync($"The user guide could not be opened:\n{PathText.Breakable(HelpLinks.UserGuideUrl)}\n\n{exception.Message}");
        }
    }

    /// <summary>Determines whether Open project and Close project are available.</summary>
    /// <returns><see langword="true" /> when no audit is running and no other project command is in progress.</returns>
    private bool CanChangeProject() => !IsRunning && !_isBusy;

    /// <summary>Runs the save point and then returns every group to its defaults, as if the application had just been launched.</summary>
    /// <returns>A task that completes when the project has been closed or the user has cancelled.</returns>
    [ RelayCommand(CanExecute = nameof(CanChangeProject)) ]
    private async Task CloseProjectAsync()
    {
        SavePointResult savePoint = await _projectSettingsService.RunSavePointAsync(Snapshot(), ReportLocation.ResolveFolder(), ReportLocation.SaveProjectSettings);

        if (!savePoint.Proceed)
        {
            return;
        }

        ResetProject();
    }

    /// <summary>Runs the save point, asks for a project file and loads it into every group.</summary>
    /// <returns>A task that completes when the project has been opened, or the attempt has been abandoned.</returns>
    [ RelayCommand(CanExecute = nameof(CanChangeProject)) ]
    private async Task OpenProjectAsync()
    {
        SavePointResult savePoint = await _projectSettingsService.RunSavePointAsync(Snapshot(), ReportLocation.ResolveFolder(), ReportLocation.SaveProjectSettings);

        if (!savePoint.Proceed)
        {
            return;
        }

        string           startFolder = await ResolvePickerStartFolderAsync();
        ProjectFilePick? pick        = await _dialogues.PickProjectFileAsync(startFolder);

        if (pick is null)
        {
            AbandonOpen(savePoint);

            return;
        }

        if (pick.LocalPath is not { } path)
        {
            AbandonOpen(savePoint);

            await _dialogues.ShowErrorAsync($"The project could not be opened:\n{PathText.Breakable(pick.Name)}\n\nOnly files on a local or network drive can be opened.");

            return;
        }

        ErrorOr<AppSettings> loaded = await _projectSettingsService.LoadAsync(path);

        if (loaded.IsError)
        {
            AbandonOpen(savePoint);

            await _dialogues.ShowErrorAsync($"The project could not be opened:\n{PathText.Breakable(path)}\n\n{loaded.FirstError.Description}");

            return;
        }

        ResetProject();

        LoadAll(loaded.Value);

        _projectSettingsService.MarkLoaded(path, Snapshot());
    }

    /// <summary>Loads settings into every group. The shared location is loaded before the tabs, so each tab's Report group refreshes against the new shared values.</summary>
    /// <param name="settings">The settings to load.</param>
    private void LoadAll(AppSettings settings)
    {
        Database.Load(settings);
        FileIndexTable.Load(settings);
        ReportLocation.Load(settings);
        MissingFiles.Load(settings);
        UnindexedFiles.Load(settings);
    }

    /// <summary>
    ///     Shows the tick the save point asked for when opening a project is abandoned: the user may have unticked the box and then answered Yes to the save prompt, so the
    ///     settings were saved; the project stays open, so that tick must be shown.
    /// </summary>
    /// <param name="savePoint">The result of the save point.</param>
    private void AbandonOpen(SavePointResult savePoint)
    {
        if (savePoint.TickCheckbox)
        {
            SetSaveProjectSettings(value: true);
        }
    }

    /// <summary>
    ///     Finds the folder the Open Project picker starts in: the report folder when it is complete and exists, otherwise the user's profile. The probe runs off the UI thread,
    ///     since the report folder may be on a slow or disconnected drive, and is abandoned after two seconds.
    /// </summary>
    /// <returns>The folder's full path.</returns>
    private async Task<string> ResolvePickerStartFolderAsync()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        (string directory, string _, bool isComplete, bool _) = ReportLocation.ResolveFolder();

        if (!isComplete
            || !Path.IsPathFullyQualified(directory))
        {
            return profile;
        }

        Task<bool> probe = Task.Run(() => Directory.Exists(directory));

        bool answered = await Task.WhenAny(probe, Task.Delay(ProbeTimeoutMilliseconds)) == probe;

        return answered && probe.Result ? directory : profile;
    }

    /// <summary>Returns every group to its defaults, as if the application had just been launched.</summary>
    private void ResetProject()
    {
        LoadAll(AppSettings.Default);

        _projectSettingsService.Reset();

        SetSaveProjectSettings(value: true);

        _lastRunStatus.Clear();

        foreach (AuditDirections direction in Enum.GetValues<AuditDirections>())
        {
            AuditFor(direction)
                .Reset();
        }

        UpdateIdleStatus();
        RefreshReportState();
    }

    /// <summary>Asks the window to show the About box.</summary>
    [ RelayCommand ]
    private void About() => AboutRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks the window to close.</summary>
    [ RelayCommand ]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Refreshes the Audit group from a run, and its status-bar text while it is still under way.</summary>
    /// <param name="run">The run to read the progress of.</param>
    /// <param name="clock">Times the run.</param>
    /// <param name="isFinished">Whether the run has ended.</param>
    private static void Refresh(AuditRun run, Stopwatch clock, bool isFinished)
    {
        run.Update(clock.Elapsed, isFinished);

        if (!isFinished)
        {
            run.Audit.StatusMessage = run.Phase;
        }
    }

    /// <summary>Determines whether the user guide can be opened: it has an address.</summary>
    /// <returns><see langword="true" /> when <see cref="HelpLinks.UserGuideUrl" /> is not empty.</returns>
    private static bool CanOpenHelpTopics() => !string.IsNullOrEmpty(HelpLinks.UserGuideUrl);

    /// <summary>Raised when the user asks for the About box.</summary>
    public event EventHandler? AboutRequested;

    /// <summary>Raised when the user asks to exit the application.</summary>
    public event EventHandler? ExitRequested;
}
