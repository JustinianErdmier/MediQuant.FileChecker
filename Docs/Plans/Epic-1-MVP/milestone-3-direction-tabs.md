# Milestone 3 — Direction Tabs Implementation Plan

**Date:** 2026-11-08

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prepare the window and project file for two audit directions: a tab control with a **Missing Files** tab holding today's audit, a Report group whose
location is shared between tabs while each direction keeps its own file name, paired report names, and a *Help Topics* menu item.

**Architecture:** An `AuditDirection` enum names the two directions; `ReportPathResolver` resolves per direction. The Report group splits into a shared
`ReportLocationState` (everything but the file name) and a per-direction `ReportViewModel` that binds to it, so every tab's Report view shows the same location.
A `MissingFilesTabViewModel` / `MissingFilesTabView` pair holds the tab's Options, Report and Audit groups; `MainWindowViewModel` keeps the commands and routes
Start / View Report to the selected tab and Stop / the status bar to the running audit.

**Tech Stack:** unchanged — .NET 10 · Avalonia 11.3.22 · Classic.Avalonia 11.3.0.3 · CommunityToolkit.Mvvm 8.4.2 · ErrorOr 2.1.1 · System.Text.Json. No new packages.

**Spec:** [`Docs/Specs/Epic-1-MVP/milestone-3-direction-tabs-design.md`](../../Specs/Epic-1-MVP/milestone-3-direction-tabs-design.md) — read it in full first; §3.x
below refers to it. Milestone 4's spec ([`milestone-4-unindexed-audit-design.md`](../../Specs/Epic-1-MVP/milestone-4-unindexed-audit-design.md)) is context only:
this plan must leave room for its second tab but builds none of it. The master SDD [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md)
§12 code conventions bind every task. Mock-up: [`direction-tabs-layout.html`](../../UserInterfaceDesign/Mockups/direction-tabs-layout.html) (left panel).

## Global Constraints

- **No automated tests.** Verify each task with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings) and by
  running the built exe and driving it with Windows UI Automation from PowerShell (computer use cannot be granted to the locally built app). LocalDB
  `(localdb)\MSSQLLocalDB` is available for a real audit run: create a scratch database and table, and drop them afterwards. Close the app when done.
- Version stays `1.0.0`. No compatibility with project files saved by milestone 2's build is required.
- Exact copy, verbatim:
  - Tab header: `Missing Files`. Group on that tab: `Options`, holding the existing checkbox `Report empt_y (zero-byte) files as missing`.
  - Report names: built `FileAuditorMissingReport_<Audit Stage>.txt`; custom default `FileAuditorMissingReport.txt`.
  - Report title lines: `File Auditor Missing Report` and `===========================` (27 `=`).
  - Help menu: `_Help Topics` with `HotKey="F1"` / `InputGesture="F1"`, then a separator, then the existing `_About File Auditor`.
  - The user-guide address: `public const string UserGuideUrl = "";` in `Source/FileAuditor/Common/Links/HelpLinks.cs` (static class `HelpLinks`).
- Project file property renamed: `CustomFileName` → `MissingCustomFileName` (default `FileAuditorMissingReport.txt`).
- The pair rule (SDD §12): every public `*ViewModel` has a sibling `*View.axaml`. Shared state that has no view of its own is **not** named `*ViewModel`.
- `MainWindow.axaml` stays at 100 lines or fewer.
- All CLAUDE.md and SDD §12 conventions apply. Commit each task automatically, `type(emoji): subject` plus the `Co-Authored-By` trailer, staging only the task's files.

## Review Focus

1. **Mirroring without drift.** A person who changes the project folder, a stage, the custom flag or the custom location expects every tab's Report group to show
   it, and the file name not to change direction — Task 2 step 2 makes one shared object the single source; Task 2 step 6 checks it in the app.
2. **Today's audit unchanged.** A person expects the same pre-flight, run, Stop, close-mid-run and report body as before, only renamed — Task 2 step 6 runs a real
   audit on LocalDB and compares the report body to milestone 2's layout.
3. **Settings round-trip.** A person who saves, closes and reopens a project expects every value back, including *Report empty files* now living on the tab and the
   renamed custom file name — Task 2 step 6.
4. **Keyboard.** F5, Esc, Ctrl+R and F1 keep working after the inputs move into a tab, and access keys stay unique — Task 2 step 6, Task 3 step 2.
5. **Change detection.** Moving fields between view models must not make an untouched project read as changed (no spurious save prompt) — Task 2 step 6.

---

### Task 1: Directions, paired report names and the renamed setting

**Files:**

- Create: `Source/FileAuditor/Models/Auditing/AuditDirection.cs`
- Modify: `Source/FileAuditor/Services/Reporting/ReportPathResolver.cs`, `Source/FileAuditor/Services/Reporting/ReportBuilder.cs`,
  `Source/FileAuditor/Models/Settings/AppSettings.cs`, `Source/FileAuditor/Services/Settings/SettingsStore.cs`, `Source/FileAuditor/ViewModels/Groups/ReportViewModel.cs`

**Interfaces:**

- Produces: `enum AuditDirection { Missing, Unindexed }`; `ReportPathResolver.Resolve(AppSettings settings, AuditDirection direction) : ReportPath` (built prefix
  `FileAuditorMissingReport_` / `FileAuditorUnindexedReport_`; custom name from `MissingCustomFileName` for Missing — the Unindexed custom name arrives in
  milestone 4, so for now Unindexed uses the built-in default `FileAuditorUnindexedReport.txt`); `AppSettings.MissingCustomFileName`.

- [x] **Step 1:** Add `AuditDirection`, the per-direction `Resolve`, and rename the setting (including `SettingsStore`'s normalisation and the known-property check).
  Every existing caller passes `AuditDirection.Missing`.
- [x] **Step 2:** Change the report title lines to the Global Constraints text. Nothing else in the report body changes.
- [x] **Step 3: Verify.** Clean build. Run the app: with Project folder `D12345` the File name preview reads `FileAuditorMissingReport_Interim.txt`; with the custom
  location ticked it defaults to `FileAuditorMissingReport.txt`. Save a project and confirm the JSON has `MissingCustomFileName` and no `CustomFileName`.
- [x] **Step 4: Commit** — `feat(✨): name reports per audit direction`.

### Task 2: The tab control, the Missing Files tab and the shared Report location

**Files:**

- Create: `Source/FileAuditor/ViewModels/Groups/ReportLocationState.cs`, `Source/FileAuditor/ViewModels/Tabs/MissingFilesTabViewModel.cs`,
  `Source/FileAuditor/Views/Tabs/MissingFilesTabView.axaml(.cs)`
- Modify: `ReportViewModel.cs`, `Views/Groups/ReportView.axaml`, `FileIndexTableViewModel.cs`, `Views/Groups/FileIndexTableView.axaml`,
  `ViewModels/Main/MainWindowViewModel.cs`, `Views/Main/MainWindow.axaml(.cs)`, `Common/GlobalUsings.cs`

**Interfaces:**

- Consumes: Task 1's `AuditDirection`, `Resolve(settings, direction)`.
- Produces:
  - `ReportLocationState : ObservableObject` — the shared Report values: `UseCustomLocation`, `ProjectFolder`, the four stage booleans, `IsBuilt`,
    `IsAuditStageEnabled`, the custom location, `SaveProjectSettings`; `ApplyTo(AppSettings)` / `Load(AppSettings)` for the shared fields (not
    `SaveProjectSettings`, which stays out of the file).
  - `ReportViewModel(ReportLocationState shared, AuditDirection direction)` — `Shared`, `Direction`, `Location` (display text), `FileName`, `Current : ReportPath`;
    `ApplyTo`/`Load` for its own custom file name. Raises `Current` changes when the shared state changes.
  - `MissingFilesTabViewModel(ReportLocationState shared)` — `ReportEmptyFiles`, `Report : ReportViewModel` (Missing), `Audit : AuditViewModel`; `ApplyTo`/`Load`
    for `ReportEmptyFiles` and the report's custom name.
  - `MainWindowViewModel`: `ReportLocation : ReportLocationState`, `MissingFiles : MissingFilesTabViewModel`, `SelectedTabIndex : int`, `StatusAudit :
    AuditViewModel` (the running tab's Audit while one runs, else the selected tab's). Existing commands keep their names.

- [x] **Step 1: Move the checkbox.** *Report empty files as missing* leaves `FileIndexTableViewModel` / `FileIndexTableView` for the Missing Files tab's **Options**
  group. The File Index Table group keeps Table, Path column, ID column.
- [x] **Step 2: Split the Report group.** Shared values in `ReportLocationState` (one instance, owned by `MainWindowViewModel`); `ReportViewModel` per direction,
  holding only its file name and computing `Location`, `FileName` and `Current` from the shared state plus its direction. `ReportView.axaml` keeps its look and
  binds through `Shared` for the shared controls. The untick warning (milestone 2) keeps watching `SaveProjectSettings`, now on the shared state.
- [x] **Step 3: The tab.** `MissingFilesTabView` stacks the Options group, `ReportView` and `AuditView`. `MainWindow.axaml` replaces the old Report and Audit groups
  with a `TabControl` (bound to `SelectedTabIndex`) holding one `TabItem` `Missing Files`. The Database and File Index Table groups stay above it.
- [x] **Step 4: Routing.** `Snapshot()` / `Load` go through `ReportLocation`, `FileIndexTable`, `Database` and `MissingFiles`. Start and View Report use the
  selected tab's report and Audit; Stop and the status bar (`StatusAudit`) use the running audit while one runs, otherwise the selected tab's; each tab keeps its
  own last-run status for the idle text (spec §3.3–§3.4). With one tab every route resolves to Missing Files, but write it per direction so milestone 4 only adds
  a tab.
- [x] **Step 5:** Keep `IsEditable`, the busy gate and save-on-close behaviour unchanged; `AuditView`'s `$parent[Window]` command bindings still resolve.
- [x] **Step 6: Verify.** Clean build. In the running app (UI Automation):
  - The window shows Database, File Index Table, then the tab control with *Missing Files* (Options, Report, Audit), then the status bar; screenshot it.
  - Change project folder, stage, custom flag, custom location: Location/File name update as before; File name stays the missing report's.
  - Save a project (Start or close with the box ticked), *Close project*, *Open project...* it again: every value returns, including *Report empty files*.
    Open a project and close without changes: no save prompt.
  - F5 starts, Ctrl+R views the report, Esc stops; access keys are unique (list every `_` key on the window).
  - Real run on LocalDB: create a scratch database with a small index table (a few existing and missing files under the scratch folder), run, and check the report
    title and name, and that the body matches milestone 2's layout. Stop mid-run and close mid-run still behave as before. Drop the scratch database.
- [x] **Step 7: Commit** — `feat(✨): put the missing-files audit in a tab with a shared report location`.

### Task 3: Help Topics

**Files:**

- Create: `Source/FileAuditor/Common/Links/HelpLinks.cs`
- Modify: `Views/Main/MainWindow.axaml`, `ViewModels/Main/MainWindowViewModel.cs`, `Common/GlobalUsings.cs`

**Interfaces:**

- Produces: `HelpLinks.UserGuideUrl` (`const string`, `""`); `HelpTopicsCommand` (`CanExecute` = the address is not empty).

- [ ] **Step 1:** Add the menu item and command per Global Constraints and spec §3.3a. It opens the address by shell execute; a launch failure shows an error
  dialogue (`The user guide could not be opened:\n{address}\n\n{message}`, address through `PathText.Breakable`).
- [ ] **Step 2: Verify.** Clean build. In the running app the item shows `F1` and is disabled; F1 does nothing. Temporarily set the constant to
  `https://example.com`, rebuild, and confirm the item is enabled and F1 opens the browser; **restore the empty string** before committing. Close the browser tab
  you opened and the app.
- [ ] **Step 3: Commit** — `feat(✨): add a Help Topics item for the user guide`.

### Task 4: Documentation sync and ledger

**Files:**

- Modify: `Docs/Specs/1-file-auditor-master-design.md`, `Docs/Plans/Epic-1-MVP/milestone-3-direction-tabs.md` (tick boxes)
- Create: `Docs/Ledgers/Epic-1-MVP/milestone-3-ledger.md`
- Modify: `MediQuant.FileAuditor.slnx`

- [ ] **Step 1:** Amend the SDD in every section the spec's §5 lists, so it describes the tabbed window, the shared Report location, the paired names, the routing
  of commands and the status bar, *Help Topics*, and the project file. Update the window diagram. Keep the SDD the authority.
- [ ] **Step 2:** Write the ledger (rulings, user decisions, plan defects, outstanding items by when to revisit, incidents, not exercised, commits, final review
  "Pending.").
- [ ] **Step 3: Verify** — clean build; Markdown prose ≤ 180 characters; every file under `Docs/` is in the `.slnx`.
- [ ] **Step 4: Commit** — `docs(📝): sync the SDD with direction tabs and close the milestone 3 ledger`.
