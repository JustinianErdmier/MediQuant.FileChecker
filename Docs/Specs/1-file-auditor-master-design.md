# File Auditor — Master Design

**Date:** 2026-11-06
**Status:** Approved 2026-11-06

## 1. Intent

On almost every MediQuant project that has documents on disk, the team builds a SQL **file index table**: one row per document, holding the document's full path (and
usually an ID). **File Auditor** confirms that every file the index table points to actually exists on disk, and writes a plain-text **report** that is committed to the
project's version-control repository as a record of the audit.

File Auditor is a rewrite of the old WinForms .NET Framework tool *File Checker* (kept read-only under `OriginalSource/` for reference). The rewrite keeps the core job and
changes almost everything around it:

| Area           | File Checker (old)                                        | File Auditor (new)                                                                 |
|----------------|-----------------------------------------------------------|------------------------------------------------------------------------------------|
| Query          | User types a whole SQL `SELECT`                           | User names the table and columns; the app builds the query                         |
| ID column      | Required (first column of the query)                      | Optional                                                                           |
| Authentication | SQL user/password or integrated security                  | Integrated security only, server certificate always trusted                        |
| Settings       | `FileChecker.ini` beside the executable, shared by all    | `settings.json` under each user's `%APPDATA%`                                      |
| Output         | Tab-delimited "log" (`MissingID<TAB>MissingFile`)         | Plain-text **report** with stats, grouped Missing and Unverified sections          |
| Output path    | Free text, default `_DMS_Output.txt` in working directory | Built from project folder + stages, or a custom location and name                  |
| Errors         | Files that can't be checked are silently "missing"        | Files that can't be checked are listed as **Unverified**, with a reason            |
| UI             | Ad hoc WinForms layout                                    | Standard Windows 9x layout and conventions, via Classic.Avalonia                   |

### Why the changes

- **Settings per user.** The executable lives on a shared file server, not on each user's machine. An INI beside it is shared by everyone who opens the app, which defeats
  the purpose of remembering settings.
- **No hand-written SQL.** Users only ever need "path column (and ID column) from table"; typing the query is error-prone and unnecessary.
- **A report, not a log.** The output is a durable record committed to the project repo. It has to read well to a person, say clearly when nothing is missing, and make it
  easy to act on many missing files at once — hence grouping by reason instead of a reason column.

## 2. Goals, non-goals, anti-goals

**Goals**

- Verify every row of a file index table: the file exists, and (optionally) is not empty.
- Distinguish *missing* (definitely not there, or empty) from *unverified* (could not be checked, and why).
- Produce a readable, deterministic plain-text report suitable for committing.
- Remember each user's inputs between runs.
- Look and behave like a native Windows 95/98 utility.

**Non-goals**

- How missing files are followed up. That varies by project and is outside this app.
- SQL authentication, other database engines, or arbitrary queries.
- Automated tests. This is a small app; none are written.
- Multi-project / layered architecture. Everything lives in one project.
- Installers, auto-update, localisation.

**Anti-goals** (it would be a failure even if it technically worked)

- Reporting a file as *missing* when the truth is that it could not be checked (the old app's `File.Exists` did this silently).
- A report that is hard to scan when hundreds of files are missing.
- Settings that leak between users.
- Discovering after a long run that the report cannot be written.

## 3. Technology

- **.NET 10**, Windows only. A single WinExe project.
- **Avalonia 11.3** with the **Classic.Avalonia** theme (Windows 95/98 chrome). The project template was created on Avalonia 12; it is downgraded to 11.3 because
  Classic.Avalonia targets Avalonia 11. The Fluent theme and Inter font packages are removed.
- **CommunityToolkit.Mvvm** (source-generated `[ObservableProperty]` / `[RelayCommand]`).
- **ErrorOr** for expected failures: services return `ErrorOr<T>` with custom errors instead of throwing.
- **Microsoft.Data.SqlClient** for SQL Server.
- **System.Text.Json** (source-generated context) for settings.

Explicitly not used: Mediator/CQRS, FluentValidation, EF Core/Dapper, Serilog, `System.IO.Abstractions`, test projects.

## 4. Naming

| Thing                  | Name                                                       |
|------------------------|------------------------------------------------------------|
| Product (title bar)    | File Auditor                                               |
| Solution               | `MediQuant.FileAuditor.slnx`                               |
| Project / root namespace / assembly | `FileAuditor` under `Source/FileAuditor/`    |
| Settings file          | `%APPDATA%\MediQuant\FileAuditor\settings.json`            |
| Report file (built)    | `FileAuditorReport_<Audit Stage>.txt`                      |
| Report file (custom)   | `FileAuditorReport.txt` (default, editable)                |
| Version                | 1.0.0                                                      |
| Company / copyright    | MediQuant / Copyright © 2026 MediQuant                     |

The existing `Source/FileChecker/` template project and `MediQuant.FileChecker.slnx` are renamed accordingly. The Git repository and its folder keep their current name
(renaming the GitHub repository is the user's call, outside this design).

## 5. The main window

A single, **fixed-size** window (not resizable, no maximise button; minimise and close only). Classic.Avalonia supplies the Win9x chrome. All labels end with a colon; every
input and button has an access key (underlined letter, Alt+letter); access keys are unique within the window. Tab order runs top to bottom, left to right.

Layout, top to bottom. The approved mock-ups are [`main-window-layout.html`](../UserInterfaceDesign/Mockups/main-window-layout.html) and
[`report-section-states.html`](../UserInterfaceDesign/Mockups/report-section-states.html). They are a design reference only: the latter's state 1 predates the Interim
default for Audit Stage, and both predate the final Report-file rows and the access keys. The XAML and this section are the contract.

```text
┌ File Auditor ─────────────────────────────────────── _ × ┐
│ File  Help                                               │
│ ┌ Database ────────────────────────────────────────────┐ │
│ │ Server:       [                                    ] │ │
│ │ Database:     [                                    ] │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌ File Index Table ────────────────────────────────────┐ │
│ │ Table:        [                                    ] │ │
│ │ Path column:  [                                    ] │ │
│ │ ID column:    [                          ] (Optional)│ │
│ │ [ ] Report empty (zero-byte) files as missing        │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌ Report ──────────────────────────────────────────────┐ │
│ │ Project folder: [D12345  ]                           │ │
│ │ ┌ Project Stage ─────────┐ ┌ Audit Stage ──────────┐ │ │
│ │ │ ( ) Sample  (•) Final  │ │ (•) Interim ( ) Final │ │ │
│ │ └────────────────────────┘ └───────────────────────┘ │ │
│ │ [ ] Use a custom report location and file name       │ │
│ │ Location:     [U:\Projects\D12345\DE\Final\ ] Browse...│ │
│ │ File name:    [FileAuditorReport_Interim.txt       ] │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌ Progress ────────────────────────────────────────────┐ │
│ │ Total files:  124,500     Elapsed:       00:02:14    │ │
│ │ Checked:      61,200      Remaining:     00:02:18    │ │
│ │ Missing:      12          Files/second:  456.72      │ │
│ │ Unverified:   3                                      │ │
│ │ [██████████████████                                ] │ │
│ │ [View Report]                       [Start]  [Stop]  │ │
│ └──────────────────────────────────────────────────────┘ │
│ Checking files...                              │ 49%   │ │
└──────────────────────────────────────────────────────────┘
```

### 5.1 Database group

- **Server** and **Database**: text inputs, both required.
- The connection string is always built as `Data Source=<Server>; Initial Catalog=<Database>; Integrated Security=True; TrustServerCertificate=True` (via
  `SqlConnectionStringBuilder`). There are no user/password fields anywhere — not in the UI, not in settings.

### 5.2 File Index Table group

- **Table**: required. Accepts `table` or `schema.table`.
- **Path column**: required.
- **ID column**: optional, with a muted "(Optional)" note beside it.
- **Report empty (zero-byte) files as missing**: checkbox, checked by default. When ticked, an existing file of length 0 counts as *missing (empty)*.

### 5.3 Report group

- **Project folder**: text input with watermark `D12345`.
- **Project Stage** radio group: *Sample*, *Final*. Default **Final**.
- **Audit Stage** radio group: *Interim*, *Final*. Default **Interim**.
- While *Project Stage = Sample*, *Audit Stage* is forced to **Interim** and the group is disabled (read-only). Switching back to *Final* re-enables it and keeps Interim
  selected.
- **Use a custom report location and file name**: checkbox, unchecked by default.
- **Location** and **File name**:
  - *Unticked (built):* both are read-only previews. Location is `U:\Projects\<Project Folder>\DE\<Project Stage>\`; File name is
    `FileAuditorReport_<Audit Stage>.txt`. An empty Project folder shows as the literal placeholder `<Project Folder>`. **Browse...** is disabled.
  - *Ticked (custom):* Project folder and both stage groups are disabled. Location becomes an editable directory path, empty by default, with **Browse...** opening a
    *folder* picker (directories only). File name becomes editable, defaulting to `FileAuditorReport.txt`.

### 5.4 Progress group

- Read-only statistics in two columns: **Total files**, **Checked**, **Missing**, **Unverified**; **Elapsed**, **Remaining**, **Files/second**. Counts use thousands
  separators (`N0`); times are `hh:mm:ss`; rate has two decimals. Blank before the first run.
- A progress bar (percentage of rows checked).
- A row of command buttons: **View Report** left-justified; **Start** then **Stop** right-justified. There is no Close button.

### 5.5 Status bar

Two panes: a message (`Ready`, `Counting files...`, `Checking files...`, `Writing report...`, `Completed`, `Stopped`, `Failed`) and the percentage.

### 5.6 Menu bar

- **File**: **Start** (F5), **Stop** (Esc), **View Report** (Ctrl+R), separator, **Exit** (Alt+F4). Items enable and disable with their buttons.
- **Help**: **About File Auditor**.

### 5.7 About box

A small modal, fixed-size Win9x About dialog: the app icon, **File Auditor**, **Version 1.0.0**, **Copyright © 2026 MediQuant**, and an **OK** button.

### 5.8 Command states

| Command     | Enabled when                                                                                                     |
|-------------|------------------------------------------------------------------------------------------------------------------|
| Start       | Not running, and Server, Database, Table, Path column are filled, and the report path is complete (see below)    |
| Stop        | Running                                                                                                          |
| View Report | Not running, and a report file exists at the current resolved path                                               |

The report path is complete when, *built*: Project folder is filled (an Audit Stage is always selected); *custom*: Location and File name are filled. All inputs are disabled
while a run is in progress.

**View Report** opens the report in the user's default program for `.txt` (shell execute). No ellipsis: it needs no further input. **Browse...** keeps its ellipsis.

## 6. Running an audit

### 6.1 Pre-flight checks (on Start, before touching the database)

1. Validate inputs (identifiers well formed; custom File name has no invalid file-name characters). With the built location, the Project folder must be a single
   folder name: not rooted, no `\` or `/`, not `.` or `..`, and no invalid file-name characters. Failure → error dialog, run does not start.
2. Report folder:
   - *Built location* and the folder does not exist → **error dialog**; the run does not start.
   - *Custom location* and the folder does not exist → **warning dialog** with **Create Folder** and **Cancel**. Create Folder tries to create it (failure → error dialog,
     run does not start).
   - Once the folder exists (or has been created), a writability probe creates and removes the report's temporary file. If the folder cannot be written to → error dialog,
     run does not start.
3. Report file already exists (either mode):
   - if it is read-only → **error dialog**, run does not start;
   - otherwise → **warning dialog** with **Overwrite** and **Cancel**.
4. Save settings.

### 6.2 Query

Built from the inputs; each identifier part is bracket-quoted with `]` escaped as `]]` (never string-concatenated raw):

```sql
SELECT COUNT_BIG(*) FROM [schema].[table];
SELECT [PathColumn] AS [Path], [IdColumn] AS [Id] FROM [schema].[table];   -- ID part omitted when no ID column
```

The count runs first (status `Counting files...`) so progress and remaining time can be shown. Values are read as strings (`NULL` → empty). Command timeouts are long
enough for large tables.

### 6.3 Checking a file

Each row is checked concurrently with bounded parallelism (builder chooses the mechanism and degree; the old app's unbounded `ThreadPool` + 1 million queue cap is not a
requirement). Each file lands in exactly one outcome:

| Outcome                       | When                                                                                           |
|-------------------------------|------------------------------------------------------------------------------------------------|
| Found                         | The file exists (and, if the empty option is on, its length > 0)                               |
| Missing — Does not exist      | The path's root exists, but the file or a directory below the root does not                    |
| Missing — Empty               | Empty option on, and the file exists with length 0                                             |
| Unverified — Drive or share not found | The path's root (drive letter, or `\\server\share`) does not exist for this user       |
| Unverified — Permission denied | Access to the file or its location is denied                                                  |
| Unverified — Network location unreachable | The server or share in a UNC path cannot be reached                                |
| Unverified — Path too long    | The path exceeds what the system can handle                                                    |
| Unverified — Invalid characters in path | The path is malformed or contains illegal characters                                 |
| Unverified — Path is empty in the index table | The path value is `NULL`, empty or whitespace                                  |
| Unverified — Other errors     | Any other I/O failure; the reason and system message are recorded per file                     |

The check must not use `File.Exists` alone, because it returns `false` for every failure and would report unreachable files as missing. Use an API that surfaces the error
(e.g. `FileInfo` / `File.GetAttributes`) and map exceptions and Win32 error codes to the outcomes above.

**Roots are checked once.** The root of each path (a drive letter such as `U:\`, or `\\server\share`) is checked the first time it is seen and the result is cached for
the run. If the root does not exist, every file under it is *Unverified — Drive or share not found* without being tried. *Missing — Does not exist* is reserved for files
whose root exists, so an unmapped drive or a missing share can never flood the Missing section. A root confirmed reachable is re-probed when a file under it comes back
not-found and the root's last confirmation is 5 seconds old or more. If the root no longer exists, that file and every later file under it are *Unverified — Drive or
share not found*; if the root now fails for another reason (permission, network), they take that outcome instead.

Two mappings guard the same rule: a .NET DriveNotFoundException maps to *Unverified — Drive or share not found* (never *Missing*), and a path segment longer than 255
characters maps to *Unverified — Path too long* before the OS is asked.

**Unreachable locations short-circuit, at the level that actually failed.** When a file whose root exists comes back unreachable for any reason (*Permission denied*,
*Network location unreachable* or *Other errors*), the checker walks the path one directory at a time from the root down (`x\`, then `x\y\`, then `x\y\z\`) to find the
first directory that cannot be reached. That directory and its reason are cached for the run, and every later file *under that directory* gets the same Unverified
outcome without being tried. Files elsewhere are still checked: if `x\y\z\` is unreachable, files in `x\` and `x\y\other\` are not affected. If every directory in the
path is reachable, the failure belongs to the file itself and nothing is cached. Short-circuited files are still listed individually in the report. Path-shape failures
(*Path too long*, *Invalid characters in path*, *Path is empty in the index table*) never short-circuit other files.

### 6.4 Progress, stop and failure

- The UI refreshes stats a few times per second. Remaining time is estimated from the rate so far.
- **Stop**: stops reading rows and checking files, then writes the report with `Result: Stopped by user`. Counts reflect only what was checked.
  If Stop is pressed while the rows are still being counted (before the count query returns), no report is written and the status bar reads `Stopped`: nothing
  has been checked, so there is nothing to report.
- **SQL failure** (connection, bad table/column, timeout): error dialog with the message. If no rows were checked, no report is written. If it fails mid-run, the report is
  written with `Result: Failed — <message>` and the counts so far.
- **Report write failure**: error dialog.
- **Closing the window mid-run**: a prompt, *"An audit is in progress. Stop it and exit?"*, with **Yes** and **No**. Yes stops the run, writes the report as
  `Stopped by user`, then exits; No carries on. File → Exit and Alt+F4 behave the same. If Yes is chosen before the count query returns, no report is written (as for
  Stop) and the application simply exits.

## 7. The report

Plain text, UTF-8 without BOM, Windows (CRLF) line endings. Timestamps are local time (a person reads this). Within each group, files are sorted by path (ordinal,
case-insensitive) so re-runs produce stable diffs. Each file line is `<IdColumn> <IdValue>   <Path>` when an ID column is set, otherwise just `<Path>`. Columns are aligned
with spaces.

```text
File Auditor Report
===================

Server:          KDCSQL15
Database:        InterimBJC_Soarian_DocStorage
Table:           _DMS_Generic_Index
Path column:     MQ_FullPath
ID column:       doc_id
Empty files:     Reported as missing
Project folder:  D12345
Project stage:   Final
Audit stage:     Final

Started:         2026-11-06 14:02:11
Finished:        2026-11-06 14:06:43
Elapsed:         00:04:32
Result:          Completed

Summary
-------
Total files:     124,500
Found:           124,485
Missing:         12
Unverified:      3

Missing Files (12)
------------------
[Empty (3)]
doc_id 10871   \\fs01\docs\2019\05\10871.tif
...

[Does not exist (9)]
doc_id 10443   \\fs01\docs\2019\04\10443.pdf
doc_id 10502   \\fs01\docs\2019\04\10502.pdf
...

Unverified Files (3)
--------------------
[Permission denied (1)]
doc_id 22010   \\fs02\restricted\22010.pdf

[Path is empty in the index table (1)]
doc_id 22011

[Other errors (1)]
doc_id 22030   \\fs03\docs\22030.pdf   I/O error   The semaphore timeout period has expired.
```

Rules:

- Group titles and their counts are wrapped in square brackets, each on its own line, with a blank line between groups.
- A group with no files is omitted. A section with no groups reads `No missing files.` / `No unverified files.`
- `ID column:` reads `(none)` when not set. `Empty files:` reads `Reported as missing` or `Not reported`. The Project folder / stage lines are omitted when a custom location
  is used.
- Missing groups appear in the order *Empty*, *Does not exist*; Unverified groups in the order of the table in §6.3, *Other errors* last.
- `Total files:` is always the table's row count. When the result is *Stopped by user* or *Failed*, the Summary adds a `Not checked:` line, so that Found + Missing +
  Unverified + Not checked = Total files. The line is omitted for a completed run.

## 8. Settings

`%APPDATA%\MediQuant\FileAuditor\settings.json`, per Windows user. Holds every input: server, database, table, path column, ID column, empty-files option, project folder,
project stage, audit stage (default Interim), custom-location flag, custom location, custom file name. No credentials. Loaded on launch (missing or unreadable file → defaults);
saved when a run starts and when the app closes, using a temp file + `File.Move(overwrite: true)`. A null string value or an undefined enum value in the file falls
back, field by field, to its default; other values are kept. Window size and position are not saved — the window is fixed-size.

## 9. Code shape

One project, `Source/FileAuditor/`, organised by folder (namespaces mirror folders). Only the `.csproj` and `app.manifest` sit at the project root; `Assets/` holds the
icon artwork. The code folders are:

- `Startup/` — `App` and `Program`.
- `Views/` — `Main/` (`MainWindow`), `About/` (`AboutWindow`), `Dialogues/` (`MessageDialogueWindow`) and `Groups/` (a `UserControl` per group box: Database, File Index
  Table, Report, Progress).
- `ViewModels/` — one view model per view (CommunityToolkit.Mvvm), in the parallel folders `Main/`, `About/`, `Dialogues/` (including `DialogueIcon`) and `Groups/`.
- `Services/` — `Settings/` (settings store), `Sql/` (connection strings, SQL identifiers, query builder, index table reader), `Checking/` (file checker and Win32 error
  mapper), `Auditing/` (audit runner, pre-flight checker, request and progress types), `Reporting/` (report builder, writer and path resolver) and `Dialogues/` (dialogue
  service). Return `ErrorOr<T>` for expected failures.
- `Models/` — `Settings/` (settings record and the project and audit stage enums), `Auditing/` (audit query, index row, check outcome and result) and `Reporting/` (audit
  report and result kind).
- `Common/` — `GlobalUsings.cs`; `Errors/` (the custom `Error` definitions: audit, query, report, settings, SQL); `Extensions/` (`ClassicWindow` helpers); `Serialisation/`
  (the source-generated settings JSON context).

There is no dependency-injection container requirement; the builder may wire services by hand in `App` or use `Microsoft.Extensions.DependencyInjection`.

## 10. Builder's calls (decided by Claude, not the user)

- Read-only preview of the built report path, with `<placeholders>`; Start disabled until the path is complete. *(shown in mock-up, accepted)*
- Disabling Project folder and both stage groups while Custom is ticked. *(shown in mock-up, accepted)*
- Pre-flight checks happen on Start, before the database is queried.
- Report timestamps in local time; sorting by path; UTF-8 without BOM; CRLF.
- Behaviour on Stop and on mid-run SQL failure (§6.4).
- `schema.table` support and bracket quoting.
- The set of Unverified reasons and their wording (§6.3).
- Status bar messages and keyboard shortcuts.
- Extra report header lines (Empty files, Project folder, stages).
- `Total files` stays the table's row count on Stop or failure, with a `Not checked:` line (§7).

Settled with the user after the builder check: roots checked once with a *Drive or share not found* group; unreachable locations short-circuit at the directory level
that failed, found by walking the path (§6.3); the close-mid-run prompt.

## 11. Left to the builder

Concurrency mechanism and degree; UI refresh interval; exact window dimensions; access-key letters; how dialogs are implemented on Classic.Avalonia; icon artwork.

Settled by the build (milestone 1), with no change in behaviour:

- **Access keys:** *Browse...* is `B`, *Start* is `A` (St**a**rt) and *Stop* is `O` (St**o**p); `R` and `M` were already taken by neighbouring labels. The *OK* and *Cancel*
  buttons in message dialogues carry no access keys, as with Win9x message boxes (Enter and Esc).
- **Default button:** *Start* is the window's default button, so Enter starts an audit.
- **Dialogues:** every dialogue uses the custom `MessageDialogueWindow` (a `ClassicWindow` with icon, message and right-aligned buttons), because Classic.Avalonia's own
  message box cannot take custom button text.
- **Number and time formatting in the report:** counts are formatted `N0` with the invariant culture, so reports diff identically on any machine; elapsed time is shown in
  total hours (at least two digits), not rolled over into days.
- **Full-path validation:** a path is accepted as absolute only if `Path.IsPathFullyQualified` says so, so `C:x` and `\x` (which would resolve against the application's
  current drive or folder) count as *Invalid characters in path*.
