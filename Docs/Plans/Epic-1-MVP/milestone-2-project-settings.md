# Milestone 2 — Project Settings Implementation Plan

**Date:** 2026-11-06

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the per-user `%APPDATA%` settings with per-project `FileAuditorSettings.json` files, opened and closed from the *File* menu and saved beside the
report under the control of a *Save project settings with the report* checkbox.

**Architecture:** `SettingsStore` becomes a stateless reader and writer of a settings file at a given path. A new `ProjectSettingsService` holds the open project's
in-memory state (its file, the settings last loaded or saved, the remembered answers) and runs the one save rule (§3.4–§3.7 of the spec) with its prompts.
`MainWindowViewModel` calls it at the four save points — Start, closing the window, *Close project* and *Open project...* — and owns the checkbox's untick warning.
`DialogueService` gains a three-button dialogue and a project-file picker.

**Tech Stack:** unchanged from milestone 1 — .NET 10 · Avalonia 11.3.22 · Classic.Avalonia 11.3.0.3 · CommunityToolkit.Mvvm 8.4.2 · ErrorOr 2.1.1 ·
System.Text.Json (source-generated). No new packages.

**Spec:** [`Docs/Specs/Epic-1-MVP/milestone-2-project-settings-design.md`](../../Specs/Epic-1-MVP/milestone-2-project-settings-design.md) — read it in full before any
task; section numbers below (§3.x) refer to it. The master SDD is [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md); its §12
code conventions bind every task. House rules: [`CLAUDE.md`](../../../CLAUDE.md).

## Global Constraints

- **No automated tests.** Each task is verified by `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings) and, for
  anything visible, by running the app and driving it. Computer use cannot be granted to the locally built app, so drive it with Windows UI Automation from PowerShell
  (`UIAutomationClient`: find controls by `AutomationId` or `Name`, `ValuePattern.SetValue`, `InvokePattern.Invoke`, `TogglePattern.Toggle`, `WindowPattern.Close`;
  owned dialogues appear as `Window` children of the main window). Close the app when done.
- No SQL Server is needed: every save point is reachable without one (Start saves after pre-flight and before the database is touched). Use a scratch folder under the
  session scratchpad as the custom report location, and delete scratch files afterwards.
- Settings file name: exactly `FileAuditorSettings.json`. Version stays `1.0.0`. Nothing is ever written beside the executable, and nothing is read from or written to
  `%APPDATA%` any more.
- Exact UI copy, used verbatim:
  - Menu: `O_pen project...`, `_Close project` (new first group of *File*, then a separator, then the existing items).
  - Checkbox (last row of the Report group): `Save project settings _with the report`.
  - Untick warning (warning icon), buttons `_Untick` / `Cancel`: `If you untick this box, this project's settings will no longer be saved automatically. You will be
    asked whether to save them instead.\n\nDo you want to untick it?`
  - Save prompt (question icon), buttons `_Yes` / `_No`: `Do you want to save this project's settings?`
  - Folder changed (question icon), buttons `_Report Folder` / `_Original File` / `Cancel`: `The report folder has changed.\n\nThis project's settings were opened
    from:\n{file}\n\nThe report is now written to:\n{folder}\n\nWhere should the settings be saved?`
  - Existing file (warning icon), buttons `_Overwrite` / `_Skip Save` / `Cancel`: `A project settings file already exists in this folder:\n{path}\n\nDo you want to
    overwrite it?`
  - Cannot save (error icon), buttons `_Skip Save` / `Cancel`: `This project's settings cannot be saved:\n{path}\n\n{reason}`, where `{reason}` is
    `The report location is incomplete.`, `The folder does not exist.` or the write error's message.
  - Open failed (error icon), button `OK`: `The project could not be opened:\n{path}\n\n{detail}`.
  - Picker title `Open Project`; filters `File Auditor project (FileAuditorSettings.json)` then `JSON files (*.json)`.
  - Every path in dialogue text goes through `PathText.Breakable`.
- Closing a question or warning dialogue with its title-bar ✕ counts as its last button (*Cancel*, or *No* for the Yes/No prompt).
- All CLAUDE.md and SDD §12 conventions apply. Commit each task automatically, `type(emoji): subject` + the `Co-Authored-By` trailer, staging only the task's files.

## Review Focus

1. **Path comparison when deciding "the report folder has changed".** A person expects `U:\P\DE\Final\` and `u:\p\de\final` (case, trailing separator) to be the same
   folder and *not* to be asked — Task 2 step 4 pins this with `Path.TrimEndingDirectorySeparator` + `StringComparison.OrdinalIgnoreCase`.
2. **A disconnected or unmapped report drive when the window closes.** A person expects the *cannot be saved* error with *Skip Save* / *Cancel*, not a hang or a crash —
   Task 2 step 4 runs the folder check off the UI thread; Task 3 step 5 exercises a missing folder at close.
3. **The opened project file is read-only or was deleted since opening.** A person expects the *cannot be saved* error, and *Skip Save* to let them leave — Task 3 step 5.
4. **Closing with nothing changed.** A person who opens a project and closes it (or launches and quits) expects no prompt and no write — Task 3 step 5 and Task 4 step 4.
5. **Choosing *Report Folder* where another project's file already exists.** A person expects the *Overwrite / Skip Save / Cancel* warning, never a silent overwrite —
   Task 2 step 4 orders the checks so the existing-file warning follows the folder-changed choice.

---

### Task 1: Three-button dialogues and the project-file picker

**Files:**

- Create: `Source/FileAuditor/ViewModels/Dialogues/DialogueChoice.cs`
- Modify: `Source/FileAuditor/ViewModels/Dialogues/MessageDialogueViewModel.cs`, `Source/FileAuditor/Views/Dialogues/MessageDialogueWindow.axaml(.cs)`,
  `Source/FileAuditor/Services/Dialogues/DialogueService.cs`

**Interfaces:**

- Produces:
  - `enum DialogueChoice { Primary, Secondary, Tertiary }`.
  - `MessageDialogueViewModel(string Message, DialogueIcon Icon, string PrimaryText, string? SecondaryText, string? TertiaryText = null)`, plus `bool HasTertiary`.
  - `DialogueService.ChooseAsync(string message, DialogueIcon icon, string primaryText, string secondaryText, string? tertiaryText = null) : Task<DialogueChoice>`.
  - `DialogueService.PickProjectFileAsync(string startFolder) : Task<string?>` — the chosen file's local path, or `null` when cancelled.
  - The existing `ShowErrorAsync`, `ConfirmAsync` and `AskYesNoAsync` keep their signatures and behaviour.

- [ ] **Step 1: Add the third button.** Buttons sit in order Primary, Secondary, Tertiary along the bottom right; Primary is `IsDefault`; the last visible button is
  `IsCancel`. The window closes with a `DialogueChoice`; the title-bar ✕ yields the last visible button's choice (`Secondary` for two buttons, `Tertiary` for three,
  `Primary` for one). `ShowAsync` returns that choice; the three existing methods map `Primary` → `true`, anything else → `false`.
- [ ] **Step 2: Add `PickProjectFileAsync`.** `owner.StorageProvider.OpenFilePickerAsync` with title `Open Project`, `AllowMultiple = false`, the two filters from Global
  Constraints (patterns `FileAuditorSettings.json` and `*.json`), and `SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(startFolder)`. Return
  `TryGetLocalPath()` of the first file, or `null`. With no owner window (designer), return `null`.
- [ ] **Step 3: Verify.** Clean build. Run the app and trigger an existing two-button dialogue (custom location set to a missing folder → *Create Folder* / *Cancel*):
  both buttons and the ✕ still behave as before. The three-button path is exercised in Task 2.
- [ ] **Step 4: Commit** — `feat(✨): add three-button dialogues and a project file picker`.

### Task 2: Per-file settings store and the project settings service

**Files:**

- Modify: `Source/FileAuditor/Services/Settings/SettingsStore.cs`, `Source/FileAuditor/Common/Errors/SettingsErrors.cs`
- Create: `Source/FileAuditor/Services/Settings/ProjectSettingsService.cs`, `Source/FileAuditor/Services/Settings/SavePointResult.cs`
- Modify: `Source/FileAuditor/Startup/App.axaml.cs`, `Source/FileAuditor/ViewModels/Main/MainWindowViewModel.cs` (constructor and Start only)

**Interfaces:**

- Consumes: Task 1's `DialogueService.ChooseAsync`, `DialogueChoice`.
- Produces:
  - `SettingsStore` (parameterless only): `const string FileName = "FileAuditorSettings.json"`; `ErrorOr<AppSettings> Load(string filePath)`;
    `ErrorOr<Success> Save(AppSettings settings, string filePath)`. A missing file is now `SettingsErrors.Unreadable` (opening a file that is not there is a failure);
    `Save` never creates a folder (`DirectoryNotFoundException` → `Unwritable`). `Normalise` and the temp-file + move are kept.
  - `sealed record SavePointResult(bool Proceed, bool TickCheckbox)` with `static SavePointResult Go`, `GoAndTick`, `Cancel`.
  - `ProjectSettingsService(SettingsStore store, DialogueService dialogues)`:
    - `string? FilePath { get; }` — the project's file, `null` until opened or first saved.
    - `bool UntickConfirmed { get; set; }` — the untick warning was confirmed this session.
    - `void Reset()` — forgets the file, the baseline (back to `AppSettings.Default`) and both remembered answers.
    - `void MarkLoaded(string filePath, AppSettings settings)` — after a successful open: the file and the baseline.
    - `bool IsChanged(AppSettings current)` — `current != baseline` (record equality).
    - `Task<SavePointResult> RunSavePointAsync(AppSettings current, ReportPath report, bool saveTicked)` — the whole rule.

- [ ] **Step 1: Rework `SettingsStore`** to the per-path API above; remove the `%APPDATA%` path and the path-holding constructor. Update the `SettingsErrors` XML docs
  (no longer "use the defaults").
- [ ] **Step 2: Start-up with defaults.** `App` no longer loads or saves settings: build `ProjectSettingsService` and pass it to `MainWindowViewModel`, which starts from
  `AppSettings.Default`; delete the `desktop.Exit` save. New constructor: `MainWindowViewModel(DialogueService dialogues, ProjectSettingsService project)`; the designer
  constructor builds both from `new DialogueService(() => null)` and `new SettingsStore()`.
- [ ] **Step 3: Write `RunSavePointAsync`** in this order (spec §3.4, §3.5, §3.7):

  ```text
  if !IsChanged(current)                         → Go
  tick = false
  if !saveTicked:
      if declined                                → Go
      if !AskYesNo(save prompt): declined = true → Go
      tick = true
  target, own = FilePath ?? null
  if FilePath is null:            target = report folder + FileName, own = false   (cannot save if report incomplete or not fully qualified)
  else if report complete && folder(FilePath) ≠ report.Directory  (compare per Review Focus 1):
      Choose(folder changed): Report Folder → target = report folder + FileName, own = false
                              Original File → target = FilePath, own = true
                              Cancel        → Cancel
  else: target = FilePath, own = true
  folder of target missing (Directory.Exists via Task.Run)      → CannotSave("The folder does not exist.")
  if !own && File.Exists(target) (via Task.Run):
      Choose(existing file): Overwrite → continue; Skip Save → Go(tick); Cancel → Cancel
  Save → error → CannotSave(error message)
  success → FilePath = target, baseline = current               → Go(tick)
  CannotSave(reason): Choose(cannot save, error icon, Skip Save / Cancel): Skip Save → Go(tick); Cancel → Cancel
  ```

  `Go(tick)` means `GoAndTick` when `tick` is set. An incomplete report location with no `FilePath` is `CannotSave("The report location is incomplete.")`, with
  `{path}` the report's display directory.
- [ ] **Step 4: Wire Start.** In `StartAsync`, replace `_settingsStore.Save(settings)` with `RunSavePointAsync(settings, reportPath, Report.SaveProjectSettings)`:
  `Cancel` → return without starting; `TickCheckbox` → `Report.SaveProjectSettings = true`. (The checkbox property arrives in Task 3; until then pass `true`.)
  Add the comparison helper `static bool SameFolder(string a, string b)` (trim ending separator, `OrdinalIgnoreCase`).
- [ ] **Step 5: Verify.** Clean build. Run the app with a custom location set to a scratch folder and Server/Database/Table/Path column filled (server `x`): press
  Start → `FileAuditorSettings.json` appears in the scratch folder before the SQL error. Put a different `FileAuditorSettings.json` in a second scratch folder, point
  the location there, change a field, Start → the *Overwrite / Skip Save / Cancel* warning appears; check each button: *Cancel* starts nothing, *Skip Save* leaves the
  file untouched and runs, *Overwrite* replaces it. Press Start again unchanged → no prompt, no write (check the file's timestamp).
- [ ] **Step 6: Commit** — `feat(✨): save settings per project beside the report`.

### Task 3: The checkbox, its untick warning, and saving on close

**Files:**

- Modify: `Source/FileAuditor/ViewModels/Groups/ReportViewModel.cs`, `Source/FileAuditor/Views/Groups/ReportView.axaml`,
  `Source/FileAuditor/ViewModels/Main/MainWindowViewModel.cs`, `Source/FileAuditor/Views/Main/MainWindow.axaml.cs`

**Interfaces:**

- Consumes: Task 2's `ProjectSettingsService`, `SavePointResult`.
- Produces: `ReportViewModel.SaveProjectSettings` (`[ ObservableProperty ] bool`, default `true`, **not** part of `ApplyTo`/`Load`); `MainWindowViewModel.ConfirmCloseAsync()`
  now covers every close, not only mid-run.

- [ ] **Step 1: Add the checkbox** as Report row 5 (below *File name*, spanning all columns), bound to `SaveProjectSettings`, text from Global Constraints. It is disabled
  during a run through the group's existing `IsEditable` binding.
- [ ] **Step 2: Untick warning.** `MainWindowViewModel` watches `Report.PropertyChanged` for `SaveProjectSettings`. On a change to `false` while
  `!project.UntickConfirmed` and not already reverting: show the warning; *Untick* → `UntickConfirmed = true`; *Cancel* → set it back to `true` under a re-entrancy
  guard. Ticking needs nothing.
- [ ] **Step 3: Pass the real checkbox to Start** (replace Task 2's temporary `true`).
- [ ] **Step 4: Save on every close.** `MainWindow.OnClosing` now cancels and re-issues *every* close (not only mid-run) until approved, keeping the `_isAsking` guard.
  `ConfirmCloseAsync`: if running, the existing stop-and-exit prompt first (No → `false`; Yes → cancel the run and await it); then
  `RunSavePointAsync(Snapshot(), Report.Current, Report.SaveProjectSettings)` — `Cancel` → `false`, otherwise `true`. *File → Exit* and Alt+F4 go through the same path.
- [ ] **Step 5: Verify.** Clean build. In the running app:
  - Untick → warning; *Cancel* keeps it ticked; untick again → *Untick* → unticked; tick and untick again → no warning.
  - With the box unticked, change a field and close → save prompt; *No* → closes, no file. Relaunch, untick (warning again: new session), change a field, Start →
    prompt; *Yes* → saves and the box is ticked.
  - Launch and close untouched → no prompt, no file. Change a field and close with the box ticked and a custom location whose folder does not exist → *cannot be saved*
    error; *Cancel* keeps the window open; *Skip Save* closes.
  - Make the saved project file read-only, change a field, close → *cannot be saved* error with the write error's message. Remove the read-only attribute afterwards.
- [ ] **Step 6: Commit** — `feat(✨): add the save-project-settings checkbox and save on close`.

### Task 4: Open project and Close project

**Files:**

- Modify: `Source/FileAuditor/Views/Main/MainWindow.axaml`, `Source/FileAuditor/ViewModels/Main/MainWindowViewModel.cs`

**Interfaces:**

- Consumes: Task 1's `PickProjectFileAsync`; Task 2's `SettingsStore`, `ProjectSettingsService.MarkLoaded` / `Reset`; the group view models' existing `Load(AppSettings)`.
- Produces: `OpenProjectCommand`, `CloseProjectCommand` (both `CanExecute` = `!IsRunning`, re-evaluated in `UpdateCommands`).

- [ ] **Step 1: Menu.** Insert the two items and a separator at the top of *File*, bound to the new commands. `MainWindow.axaml` must stay at 100 lines or fewer.
- [ ] **Step 2: `ResetProject()`** — every group `Load(AppSettings.Default)`, `Report.SaveProjectSettings = true`, `project.Reset()`, `_lastRunStatus = null`,
  `Audit.Reset()`, then `UpdateIdleStatus()` and `RefreshReportState()`. *Close project* = save point (Cancel → stop) then `ResetProject()`.
- [ ] **Step 3: *Open project...*** — save point (Cancel → stop); start folder = the report's directory when `Report.Current.IsComplete`, it is fully qualified and
  `Directory.Exists` (via `Task.Run`), else `Environment.GetFolderPath(SpecialFolder.UserProfile)`; pick (null → stop); `store.Load(path)` (error → *Open failed*
  dialogue, stop); `ResetProject()`; load the settings into the groups; `project.MarkLoaded(path, Snapshot())` — the baseline is the snapshot after loading, so a
  normalised file does not read as changed.
- [ ] **Step 4: Verify.** Clean build. In the running app:
  - *Open project...* with no valid location starts in the user's folder; with the Task 2 scratch location it starts there. Cancel the picker → nothing changes.
  - Open a valid file → the fields load, the box is ticked; close the window without changes → no prompt.
  - Open a file containing `not json` → *Open failed* error; the current project is unchanged.
  - Opened project: switch the Project Stage (built location) or change the custom location, then Start/close → *folder changed* prompt; check all three buttons.
  - *Close project* with unsaved changes and the box ticked → saves, then the window is at defaults and the status bar reads *Enter the required details*.
- [ ] **Step 5: Commit** — `feat(✨): open and close projects from the File menu`.

### Task 5: Documentation sync and ledger

**Files:**

- Modify: `Docs/Specs/1-file-auditor-master-design.md`, `Docs/Plans/Epic-1-MVP/milestone-2-project-settings.md` (tick boxes)
- Create: `Docs/Ledgers/Epic-1-MVP/milestone-2-ledger.md`
- Modify: `MediQuant.FileAuditor.slnx` (the plan, the milestone spec and the ledger, in milestone order)

- [ ] **Step 1: Amend the SDD** in the sections the spec's §5 lists, so that it describes project files, the File menu items, the checkbox, the save rule (or points
  to the milestone spec for the full prompt table) and the removed `%APPDATA%` file. Keep the master SDD the authority: copy the rules, not just a link.
- [ ] **Step 2: Write the ledger** from the run's notes: rulings, plan defects, outstanding items, incidents, what was not exercised.
- [ ] **Step 3: Verify** — clean build; every Markdown prose line ≤ 180 characters (PowerShell `.Length`); every file under `Docs/` is in the `.slnx`.
- [ ] **Step 4: Commit** — `docs(📝): sync the SDD with project settings and close the milestone 2 ledger`.
