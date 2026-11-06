# Milestone 1 — File Auditor Rewrite Implementation Plan

**Date:** 2026-11-06

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Avalonia 12 template under `Source/FileChecker/` with the complete File Auditor app described by the SDD.

**Architecture:** One WinExe project, `Source/FileAuditor/FileAuditor.csproj` (root namespace `FileAuditor`), organised by folder. Plain services return `ErrorOr<T>`;
CommunityToolkit.Mvvm view models drive Classic.Avalonia views; a main window composes one `UserControl` per group box.

**Tech Stack:** .NET 10 · Avalonia 11.3.22 · Classic.Avalonia.Theme 11.3.0.3 (+ Classic.CommonControls.Avalonia 11.3.0.3) · CommunityToolkit.Mvvm 8.4.2 ·
ErrorOr 2.1.1 · Microsoft.Data.SqlClient 7.1.1 · System.Text.Json (in-box, source-generated).

**Spec:** [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) — read it in full before any task. Mock-ups:
[`Docs/UserInterfaceDesign/Mockups/`](../../UserInterfaceDesign/Mockups/). House rules: [`CLAUDE.md`](../../../CLAUDE.md).

## Global Constraints

- **No automated tests.** Each task is verified by `dotnet build MediQuant.FileAuditor.slnx` (0 errors, no new warnings) and, where visible, by running the app.
- Packages are exactly those in *Tech Stack*. Nothing else (no Fluent theme, no Inter font, no `AvaloniaUI.DiagnosticsSupport`, no DI container, no logging library).
- `TargetFramework` `net10.0-windows` is not required; keep `net10.0`, `OutputType` `WinExe`, `Nullable` enable, `AvaloniaUseCompiledBindingsByDefault` true.
- Assembly metadata: `Version` 1.0.0, `Product` File Auditor, `Company` MediQuant, `Copyright` Copyright © 2026 MediQuant, `AssemblyName` FileAuditor.
- Connection string: `SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true }`. No
  credentials anywhere.
- Settings file: `Environment.GetFolderPath(SpecialFolder.ApplicationData)` + `MediQuant\FileAuditor\settings.json`. Nothing is ever written beside the executable.
- Built report location: `U:\Projects\<Project Folder>\DE\<Project Stage>\`; built name `FileAuditorReport_<Audit Stage>.txt`; custom default name
  `FileAuditorReport.txt`. Stage words are exactly `Sample` / `Final` and `Interim` / `Final`.
- Report: UTF-8 without BOM, CRLF, local timestamps `yyyy-MM-dd HH:mm:ss`, layout exactly as SDD §7.
- All CLAUDE.md conventions apply: one type per file, no code at project root, `Common/GlobalUsings.cs`, file-scoped namespaces, British English, XML docs on all public
  members, `[ STAThread ]` spacing, compiled bindings with `x:DataType`, designer constructors, temp-file + `File.Move(overwrite: true)` writes, no `File.Exists` in the audit.
- Commit each task with the conventional-commit + gitmoji format; stage only the task's files.

## Review Focus

1. **A dead share or unmapped drive in a large table.** A person expects one Unverified group and a run that still finishes in minutes — Task 5 step 4 pins this with
   `\\FileAuditorNoSuchHost\share` and an unmapped drive letter across many rows.
2. **A deny-ACL folder deep in an otherwise healthy tree.** A person expects only files under that folder to be Unverified — Task 5 step 4 pins this with sibling folders.
3. **Table or column names with spaces, brackets or a schema.** A person expects `dbo.My Table` and `[Odd]]Name]` to work and `x; DROP TABLE y` to be quoted, not executed —
   Task 3 step 3 pins this.
4. **Closing the window mid-run, or clicking Stop during the count.** A person expects a prompt, then a *Stopped by user* report with a `Not checked:` line — Task 7 step 6.
5. **Settings file corrupt or from an older shape.** A person expects the app to open with defaults, not crash — Task 2 step 3.

---

### Task 1: Retarget the project to File Auditor on Avalonia 11.3 + Classic.Avalonia

**Files:**
- Move: `Source/FileChecker/` → `Source/FileAuditor/`; `FileChecker.csproj` → `FileAuditor.csproj`
- Move: `App.axaml`, `App.axaml.cs`, `Program.cs` → `Source/FileAuditor/Startup/`
- Create: `Source/FileAuditor/Common/GlobalUsings.cs`
- Move: `MainWindow.axaml(.cs)` → `Source/FileAuditor/Views/Main/`
- Modify: `app.manifest` (`assemblyIdentity name="FileAuditor"`), `MediQuant.FileAuditor.slnx` (project path)

**Interfaces:**
- Produces: namespaces `FileAuditor.Startup` (`App`, `Program`), `FileAuditor.Views.Main` (`MainWindow`).

- [x] **Step 1:** Rename the folder and project with `git mv` (the template is tracked), as its own pure-rename step so rename detection holds. Update the `.slnx` `<Project Path>` to `Source/FileAuditor/FileAuditor.csproj`.
- [x] **Step 2:** Replace the package references with Avalonia 11.3.22, Avalonia.Desktop 11.3.22, Classic.Avalonia.Theme 11.3.0.3, Classic.CommonControls.Avalonia
  11.3.0.3, CommunityToolkit.Mvvm 8.4.2, ErrorOr 2.1.1, Microsoft.Data.SqlClient 7.1.1. Add the assembly metadata from *Global Constraints*.
- [x] **Step 3:** In `App.axaml` replace `<FluentTheme />` with the Classic theme (`<ClassicTheme />` from `xmlns:classic="https://github.com/BAndysc/Classic.Avalonia"`),
  `RequestedThemeVariant="Light"`. In `Program` drop `.WithInterFont()` and `.WithDeveloperTools()`. Namespaces and `x:Class` follow the new folders.
- [x] **Step 4:** `MainWindow`: title `File Auditor`, `CanResize="False"`, a placeholder body. Leave sizing to Task 4.
- [x] **Step 5: Verify.** `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` → 0 errors. Run the app; screenshot shows Win9x chrome
  titled *File Auditor*. Close it.
- [x] **Step 6: Commit** `build(📦): retarget to File Auditor on Avalonia 11.3 and Classic.Avalonia`.

### Task 2: Settings model and per-user settings store

**Files:**
- Create: `Models/Settings/ProjectStage.cs`, `Models/Settings/AuditStage.cs`, `Models/Settings/AppSettings.cs`
- Create: `Common/Serialisation/SettingsJsonContext.cs`
- Create: `Common/Errors/SettingsErrors.cs`
- Create: `Services/Settings/SettingsStore.cs`

**Interfaces:**
- Produces: `enum ProjectStage { Sample, Final }`; `enum AuditStage { Interim, Final }`.
- Produces: `sealed record AppSettings` with init properties `Server`, `Database`, `Table`, `PathColumn`, `IdColumn` (`string`, default `""`), `ReportEmptyFiles`
  (`bool`, default `true`), `ProjectFolder` (`""`), `ProjectStage` (`Final`), `AuditStage` (`Interim`), `UseCustomReportLocation` (`false`), `CustomLocation` (`""`),
  `CustomFileName` (`"FileAuditorReport.txt"`); `static AppSettings Default { get; }`.
- Produces: `sealed class SettingsStore` — `ErrorOr<AppSettings> Load()`, `ErrorOr<Success> Save(AppSettings settings)`, `string FilePath { get; }`.
- Produces: `static class SettingsErrors` — `Error Unreadable(string detail)`, `Error Unwritable(string detail)`.

- [x] **Step 1:** Write the models. Enums serialise as strings (`JsonStringEnumConverter` via the source-generated context's options); indented output.
- [x] **Step 2:** `SettingsStore.Load`: missing file → `AppSettings.Default` (not an error); unreadable or invalid JSON → `SettingsErrors.Unreadable`. Missing properties
  take their defaults. `Save`: create the directory, write to `settings.json.tmp`, then `File.Move(tmp, path, overwrite: true)`; I/O failure → `SettingsErrors.Unwritable`.
- [x] **Step 3: Verify.** Build. Callers (Task 4) treat `Unreadable` as "use defaults" — note it in the XML doc. Manually: write `{ "Server": 5, ` to the file later in
  Task 4's run and confirm the app opens with defaults.
- [x] **Step 4: Commit** `feat(✨): add per-user JSON settings store`.

### Task 3: Query builder and index table reader

**Files:**
- Create: `Models/Auditing/IndexRow.cs`, `Models/Auditing/AuditQuery.cs`
- Create: `Common/Errors/QueryErrors.cs`, `Common/Errors/SqlErrors.cs`
- Create: `Services/Sql/SqlIdentifier.cs`, `Services/Sql/QueryBuilder.cs`, `Services/Sql/IndexTableReader.cs`

**Interfaces:**
- Produces: `readonly record struct IndexRow(string? Id, string Path)` — `Path` is `""` for `NULL`.
- Produces: `sealed record AuditQuery(string CountSql, string SelectSql, bool HasIdColumn)`.
- Produces: `static class SqlIdentifier` — `string Quote(string part)` (wraps in `[` `]`, doubles `]`); `ErrorOr<IReadOnlyList<string>> ParseMultipart(string text,
  int maxParts)`.
- Produces: `static class QueryBuilder` — `ErrorOr<AuditQuery> Build(string table, string pathColumn, string idColumn)`.
- Produces: `sealed class IndexTableReader` — `Task<ErrorOr<long>> CountAsync(string connectionString, AuditQuery query, CancellationToken ct)`;
  `Task<ErrorOr<Success>> ReadAsync(string connectionString, AuditQuery query, Func<IndexRow, ValueTask> onRow, CancellationToken ct)`.
- Produces: `static string ConnectionStrings.Build(string server, string database)` in `Services/Sql/ConnectionStrings.cs`.

- [x] **Step 1:** `ParseMultipart`: trim; split on `.` outside brackets; a part may be written bracketed (`[My Table]`, with `]]` as an escaped `]`) or bare; reject empty
  parts or more than `maxParts` with `QueryErrors.InvalidIdentifier(text)`. Table allows 2 parts (`schema.table`), columns 1.
- [x] **Step 2:** `Build` emits exactly `SELECT COUNT_BIG(*) FROM <table>;` and `SELECT <path> AS [Path][, <id> AS [Id]] FROM <table>;` with every part through `Quote`.
  Empty `idColumn` → no ID part.
- [x] **Step 3: Verify the quoting** with a throwaway console check or a debugger evaluation (not committed): `dbo.My Table` → `[dbo].[My Table]`; `[Odd]]Name]` →
  `[Odd]]Name]`; `x; DROP TABLE y` → `[x; DROP TABLE y]`; `a.b.c` → `InvalidIdentifier`; `""` → `InvalidIdentifier`.
- [x] **Step 4:** `IndexTableReader`: `CommandTimeout = 0` for both commands; `CountAsync` reads the scalar as `long`; `ReadAsync` uses `CommandBehavior.SequentialAccess`,
  reads columns with `IsDBNull` → `""` / `null`, converts non-string values with `Convert.ToString(value, CultureInfo.InvariantCulture)`. `SqlException` →
  `SqlErrors.QueryFailed(message)`; cancellation propagates as `OperationCanceledException` (the runner owns it).
- [x] **Step 5: Verify.** Build.
- [x] **Step 6: Commit** `feat(✨): build the audit query from table and column names`.

### Task 4: Main window, group-box views and report-path behaviour

**Files:**
- Create: `ViewModels/Main/MainWindowViewModel.cs`, `Views/Main/MainWindow.axaml(.cs)` (replace placeholder)
- Create: `ViewModels/Groups/{DatabaseViewModel, FileIndexTableViewModel, ReportViewModel, ProgressViewModel}.cs`
- Create: `Views/Groups/{DatabaseView, FileIndexTableView, ReportView, ProgressView}.axaml(.cs)`
- Create: `Services/Reporting/ReportPath.cs`, `Services/Reporting/ReportPathResolver.cs`
- Create: `ViewModels/About/AboutWindowViewModel.cs`, `Views/About/AboutWindow.axaml(.cs)`
- Modify: `Startup/App.axaml.cs` (load settings, build view models, save on close)

**Interfaces:**
- Consumes: `AppSettings`, `SettingsStore` (Task 2).
- Produces: `sealed record ReportPath(string Directory, string FileName, bool IsComplete, bool IsCustom)` with `string FullPath` (= `Path.Combine`) and
  `string DisplayDirectory` (built + empty folder → `U:\Projects\<Project Folder>\DE\<Stage>\`).
- Produces: `static ReportPath ReportPathResolver.Resolve(AppSettings settings)`.
- Produces: each group view model exposes its inputs as `[ObservableProperty]` and `AppSettings ApplyTo(AppSettings settings)` / `void Load(AppSettings settings)`;
  `ReportViewModel.Current` (`ReportPath`); `ProgressViewModel` exposes display strings `TotalFiles`, `Checked`, `Missing`, `Unverified`, `Elapsed`, `Remaining`,
  `FilesPerSecond`, `Percent` (0–100), `StatusMessage`, `StatusPercent`.
- Produces: `MainWindowViewModel` — `bool IsRunning`, `AppSettings Snapshot()`, commands `StartCommand`, `StopCommand`, `ViewReportCommand`, `AboutCommand`, `ExitCommand`
  (Start/Stop bodies are stubs until Task 7; their `CanExecute` rules are final here, per SDD §5.8).

- [x] **Step 1:** Group views per SDD §5.1–5.4 and the mock-ups: labels with colons and unique access keys, `(Optional)` note, the checkbox (default ticked), Project folder
  watermark `D12345`, two radio group boxes, the custom checkbox, Location + `B_rowse...`, File name, the stats grid, progress bar, and the command row (`_View Report`
  left; `S_tart`, `St_op` right). Buttons ≥ 75 × 23.
- [x] **Step 2:** `ReportViewModel` rules: Sample forces Interim and disables Audit Stage; back to Final re-enables and keeps Interim; Custom ticked disables Project folder and
  both stage groups, makes Location and File name editable (Location empty, File name `FileAuditorReport.txt` the first time it is ticked); unticked shows read-only built
  previews. Browse opens `StorageProvider.OpenFolderPickerAsync` (single folder).
- [x] **Step 3:** `MainWindow`: fixed size (`CanResize="False"`, `SizeToContent="WidthAndHeight"` or fixed dimensions — builder's choice), menu bar (`_File`: Start F5, Stop
  Esc, View Report Ctrl+R, separator, Exit Alt+F4; `_Help`: About File Auditor), two-pane status bar. Keep the `.axaml` ≤ 100 lines. View Report opens the file with
  `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })`.
- [x] **Step 4:** About window: modal, fixed-size, app icon, `File Auditor`, `Version 1.0.0` (from the assembly), `Copyright © 2026 MediQuant`, `OK` (default + cancel button).
- [x] **Step 5:** App start: `SettingsStore.Load` (error → defaults); app close: `Save(Snapshot())` (error → ignore silently; record the ruling in the ledger).
- [x] **Step 6: Verify.** Build; run; screenshot each of the four Report states and compare with `report-section-states.html`; confirm Start is disabled with empty inputs
  and enabled once filled; type values, close, reopen → values restored from `%APPDATA%\MediQuant\FileAuditor\settings.json`; corrupt the file → app opens with defaults.
  Open Help → About. Close the app.
- [x] **Step 7: Commit** `feat(✨): add the File Auditor main window and About box`.

### Task 5: File checker

**Files:**
- Create: `Models/Auditing/CheckOutcome.cs`, `Models/Auditing/CheckResult.cs`
- Create: `Services/Checking/FileChecker.cs`, `Services/Checking/Win32ErrorMapper.cs`

**Interfaces:**
- Consumes: `IndexRow` (Task 3).
- Produces: `enum CheckOutcome { Found, MissingEmpty, MissingDoesNotExist, UnverifiedDriveOrShareNotFound, UnverifiedPermissionDenied,
  UnverifiedNetworkUnreachable, UnverifiedPathTooLong, UnverifiedInvalidCharacters, UnverifiedPathEmpty, UnverifiedOther }` — Unverified members in SDD §6.3 order.
- Produces: `sealed record CheckResult(IndexRow Row, CheckOutcome Outcome, string? Reason, string? SystemMessage)` — `Reason`/`SystemMessage` set only for
  `UnverifiedOther` (e.g. `"I/O error"`, the exception message).
- Produces: `sealed class FileChecker(bool reportEmptyFiles)` — `CheckResult Check(IndexRow row)`, thread-safe, one instance per run.

- [x] **Step 1:** `Win32ErrorMapper.Map(Exception) → (CheckOutcome, string? reason, string? message)` using `ex.HResult & 0xFFFF` and the exception type:
  2, 3, `FileNotFoundException`, `DirectoryNotFoundException` → `MissingDoesNotExist`; 5, `UnauthorizedAccessException` → `UnverifiedPermissionDenied`; 51, 53, 64,
  1222, 1231 → `UnverifiedNetworkUnreachable`; 15, 67 → `UnverifiedDriveOrShareNotFound`; 206, `PathTooLongException` → `UnverifiedPathTooLong`; 123, 161,
  `ArgumentException`, `NotSupportedException` → `UnverifiedInvalidCharacters`; anything else → `UnverifiedOther` with reason `"I/O error"` and `ex.Message`.
- [x] **Step 2:** `Check` in this order (never `File.Exists` / `Directory.Exists`):
  1. Blank or whitespace path → `UnverifiedPathEmpty`. Not rooted, or contains `Path.GetInvalidPathChars()` / `*` / `?` → `UnverifiedInvalidCharacters`.
  2. Root (`Path.GetPathRoot`; for UNC `\\server\share\`) — cached in a `ConcurrentDictionary`. Probe with `File.GetAttributes(root)`: success → reachable; a mapped
     `MissingDoesNotExist` or `UnverifiedDriveOrShareNotFound` → `UnverifiedDriveOrShareNotFound`; any other mapped failure → that outcome for every file under it.
  3. Short-circuit cache: walk the file's ancestor directories (deepest first, stopping at the root) and return the cached outcome of the first one present.
  4. `File.GetAttributes(path)`: directory attribute → `UnverifiedOther` (`"Path is a directory"`); success and `reportEmptyFiles` and `new FileInfo(path).Length == 0` →
     `MissingEmpty`; success → `Found`. Failure → map it.
  5. If the mapped outcome is `UnverifiedPermissionDenied`, `UnverifiedNetworkUnreachable` or `UnverifiedOther`: probe each directory from the root's child down to the
     file's parent with `File.GetAttributes`. The first that fails with a short-circuit outcome is cached with that outcome (and reason/message) and returned for this file;
     one that maps to `MissingDoesNotExist` makes this file `MissingDoesNotExist`; if all succeed, return the file's own outcome and cache nothing.
  Directory keys are compared with `StringComparer.OrdinalIgnoreCase`, normalised with a trailing separator.
- [x] **Step 3: Verify.** Build.
- [x] **Step 4: Verify behaviour** with a throwaway harness under the scratchpad (not committed) against a fixture tree created in `%TEMP%\FileAuditorFixture\`: a present
  file, a 0-byte file (with `reportEmptyFiles` on and off), a missing file, a missing folder, `ok\a.txt` beside `denied\b.txt` and `denied\deep\c.txt` where `denied` has an
  `icacls /deny "%USERNAME%":(RX)` ACE (remove the ACE and the tree afterwards), an unmapped drive letter (e.g. `Q:\x.txt` — check it is unmapped first),
  `\\FileAuditorNoSuchHost\share\x.txt` × 200 (must finish in seconds after the first), `\\localhost\FileAuditorNoSuchShare\x.txt`, `""`, `C:\bad|name.txt`, and a
  300-character path. Every row lands in the outcome the SDD table names, and `ok\a.txt` stays `Found`.
- [x] **Step 5: Commit** `feat(✨): check files and classify missing and unverified outcomes`.

### Task 6: Report builder and writer

**Files:**
- Create: `Models/Reporting/AuditResultKind.cs`, `Models/Reporting/AuditReport.cs`
- Create: `Services/Reporting/ReportBuilder.cs`, `Services/Reporting/ReportWriter.cs`, `Common/Errors/ReportErrors.cs`

**Interfaces:**
- Consumes: `CheckResult`, `CheckOutcome` (Task 5); `AppSettings` (Task 2).
- Produces: `enum AuditResultKind { Completed, StoppedByUser, Failed }`.
- Produces: `sealed record AuditReport(AppSettings Settings, DateTime StartedUtc, DateTime FinishedUtc, AuditResultKind Result, string? FailureMessage, long TotalFiles,
  long FoundCount, IReadOnlyList<CheckResult> Problems)` — `Problems` holds only Missing and Unverified results, so a million-row run does not keep every Found row in
  memory; Checked = `FoundCount + Problems.Count`.
- Produces: `static string ReportBuilder.Build(AuditReport report)`; `static ErrorOr<Success> ReportWriter.Write(string fullPath, string content)`.
- Produces: `static class ReportErrors` — `Error FolderMissing(string folder)`, `Error Unwritable(string detail)`.

- [x] **Step 1:** `Build` produces SDD §7 exactly: header block with label column padded to 17 characters; `ID column: (none)`; `Empty files: Reported as missing` /
  `Not reported`; Project lines omitted for custom; `Result: Completed` / `Stopped by user` / `Failed — <message>`; Summary with `N0` counts and `Not checked:` only
  when not Completed; Missing groups `Empty` then `Does not exist`; Unverified groups in enum order with titles `Drive or share not found`, `Permission denied`,
  `Network location unreachable`, `Path too long`, `Invalid characters in path`, `Path is empty in the index table`, `Other errors`; `[<Title> (<n>)]` headers;
  `No missing files.` / `No unverified files.`; file lines `<IdColumn> <Id>` padded to the longest in the group + 3 spaces, then the path (paths padded likewise in
  *Other errors*, then reason padded, then message); rows sorted by path with `StringComparer.OrdinalIgnoreCase`; underlines match title length; CRLF.
- [x] **Step 2:** `Write`: UTF-8 without BOM to `<name>.tmp` in the same folder, then `File.Move(overwrite: true)`; I/O failure → `ReportErrors.Unwritable`.
- [x] **Step 3: Verify.** Build. With the throwaway harness, build reports for: all found; the SDD §7 sample data; a Stopped run; no ID column. Diff by eye against SDD §7.
- [x] **Step 4: Commit** `feat(✨): build and write the plain-text audit report`.

### Task 7: Audit runner, pre-flight dialogues and run control

**Files:**
- Create: `Services/Auditing/AuditProgress.cs`, `Services/Auditing/AuditRequest.cs`, `Services/Auditing/AuditRunner.cs`
- Create: `Services/Dialogues/DialogueService.cs` and the dialogue window(s) it needs under `Views/Dialogues/` + `ViewModels/Dialogues/` (or Classic.CommonControls'
  message box if it provides Win9x error/warning boxes with custom button text — implementer's choice, recorded in the ledger)
- Create: `Services/Auditing/PreflightChecker.cs`
- Modify: `ViewModels/Main/MainWindowViewModel.cs`, `Views/Main/MainWindow.axaml.cs` (closing handler)

**Interfaces:**
- Consumes: everything above.
- Produces: `sealed class AuditProgress` — `long TotalFiles`, `long Checked`, `long Missing`, `long Unverified` updated with `Interlocked`; `string Phase` (status text).
- Produces: `sealed record AuditRequest(AppSettings Settings, ReportPath ReportPath)`.
- Produces: `sealed class AuditRunner` — `Task<ErrorOr<AuditReport>> RunAsync(AuditRequest request, AuditProgress progress, CancellationToken ct)`; returns a report for
  Completed, StoppedByUser (on `ct`) and mid-run Failed; returns an `SqlErrors` error when nothing was checked.
- Produces: `DialogueService` — `Task ShowErrorAsync(string message)`, `Task<bool> ConfirmAsync(string message, string confirmText)` (warning icon, *confirmText* +
  *Cancel*), `Task<bool> AskYesNoAsync(string message)`. Owner is the main window; title `File Auditor`.

- [x] **Step 1:** `PreflightChecker` runs SDD §6.1 in order: identifiers (`QueryBuilder.Build`), custom file name (`Path.GetInvalidFileNameChars`), folder (built missing →
  error *"The report folder does not exist:\n<folder>"*; custom missing → confirm *Create Folder*, `Directory.CreateDirectory` failure → error), existing report → confirm
  *Overwrite*. Folder existence here may use `Directory.Exists` (it is not the audit).
- [x] **Step 2:** `AuditRunner`: count, then read rows into a bounded `Channel<IndexRow>` (capacity 10,000) consumed by `Parallel.ForEachAsync` with
  `MaxDegreeOfParallelism = 32`, each row through one `FileChecker`; count Found rows with `Interlocked` and collect the rest in a
  `ConcurrentBag<CheckResult>`. On cancellation stop reading and checking, keep what was checked.
  Then `ReportBuilder.Build` + `ReportWriter.Write`.
- [x] **Step 3:** `MainWindowViewModel.Start`: pre-flight → save settings → `IsRunning = true` (all inputs disabled) → run → a `DispatcherTimer` (250 ms) refreshes
  `ProgressViewModel` (Elapsed, Remaining from the rate so far, Files/second `0.00`, percent) → final status `Completed` / `Stopped` / `Failed`. SQL error with nothing
  checked → error dialogue, no report. Write failure → error dialogue. `Stop` cancels the token.
- [x] **Step 4:** Window closing while running: cancel the close, `AskYesNoAsync("An audit is in progress. Stop it and exit?")`; Yes → stop, await the run (report
  written), then close; No → carry on. File → Exit and Alt+F4 go through the same path.
- [x] **Step 5: Verify against LocalDB.** Create scratch database `FileAuditorScratch` on `(localdb)\MSSQLLocalDB` with `sqlcmd`, table `dbo.[File Index]` (`doc_id int`,
  `MQ_FullPath nvarchar(400)`) seeded with the Task 5 fixture paths plus ~50,000 generated rows pointing at real fixture files. Run the app with Server
  `(localdb)\MSSQLLocalDB`, Database `FileAuditorScratch`, Table `dbo.File Index`, custom report location in `%TEMP%\FileAuditorReports\`. Check: Create Folder prompt;
  progress updates; report matches SDD §7 structure; second run → Overwrite prompt; View Report opens it; built location with Project folder `D99999` → folder-missing
  error; bad column name → error dialogue, no report; run with no ID column.
- [x] **Step 6: Verify stop paths.** Stop mid-run → `Result: Stopped by user` and a `Not checked:` line whose arithmetic adds up; close the window mid-run → prompt, Yes →
  report written and app exits; No → run continues.
- [x] **Step 7:** Drop `FileAuditorScratch`, delete the fixture and report folders, close the app.
- [x] **Step 8: Commit** `feat(✨): run audits with pre-flight checks, progress and stop`.

### Task 8: Documentation sync and final verification

**Files:**
- Modify: `Docs/Specs/1-file-auditor-master-design.md` (only where the build differs, e.g. final folder names in §9)
- Create: `Docs/Ledgers/Epic-1-MVP/milestone-1-ledger.md` (written throughout; cleaned here)
- Modify: `MediQuant.FileAuditor.slnx` (register the plan and ledger under `/Docs/…`)

- [ ] **Step 1:** Amend the SDD wherever the implementation settled something it left open (§9 folder list, §11). No behaviour changes without the user.
- [ ] **Step 2:** Clean the ledger: rulings, plan defects, outstanding items, incidents.
- [ ] **Step 3: Verify.** `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` → 0 errors, 0 warnings introduced. Markdown line lengths ≤ 180
  (PowerShell `[string].Length`). Every file under `Docs/` appears in the `.slnx`.
- [ ] **Step 4: Commit** `docs(📝): sync the SDD and close the milestone 1 ledger`.
