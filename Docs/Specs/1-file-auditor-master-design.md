# File Auditor — Master Design

**Date:** 2026-11-06 **Status:** Approved 2026-11-06; amended through milestone 4 (2026-11-08)

## 1. Intent

On almost every MediQuant project that has documents on disk, the team builds a SQL **file index table**: one row per document, holding the document's full path (and
usually an ID). **File Auditor** confirms that every file the index table points to actually exists on disk, and writes a plain-text **report** that is committed to the
project's version-control repository as a record of the audit.

Milestone 4 adds the reverse question as a second **direction**: *is every file on disk in the index table?* The **unindexed files audit** walks one or more folders and
reports every document whose full path is not in the index table's path column. The two directions share the window's inputs and the report location, each has its own tab
(§5) and each writes its own report (§7).

File Auditor is a rewrite of the old WinForms .NET Framework tool *File Checker* (kept read-only under `OriginalSource/` for reference). The rewrite keeps the core job and
changes almost everything around it:

| Area           | File Checker (old)                                        | File Auditor (new)                                                        |
|----------------|-----------------------------------------------------------|---------------------------------------------------------------------------|
| Query          | User types a whole SQL `SELECT`                           | User names the table and columns; the app builds the query                |
| ID column      | Required (first column of the query)                      | Optional                                                                  |
| Authentication | SQL user/password or integrated security                  | Integrated security only, server certificate always trusted               |
| Settings       | `FileChecker.ini` beside the executable, shared by all    | `FileAuditorSettings.json` per project, saved beside the report (§8)      |
| Output         | Tab-delimited "log" (`MissingID<TAB>MissingFile`)         | Plain-text **report** with stats, grouped Missing and Unverified sections |
| Output path    | Free text, default `_DMS_Output.txt` in working directory | Built from project folder + stages, or a custom location and name         |
| Errors         | Files that can't be checked are silently "missing"        | Files that can't be checked are listed as **Unverified**, with a reason   |
| UI             | Ad hoc WinForms layout                                    | Standard Windows 9x layout and conventions, via Classic.Avalonia          |

### Why the changes

- **Settings per project.** The executable lives on a shared file server, not on each user's machine, so an INI beside it is shared by everyone who opens the app and
  defeats the purpose of remembering settings. The inputs belong to a *project* (its server, database, index table and report location), not to a person, so they are
  saved as a project file beside the project's report, where they are committed to the repository with it and any teammate can open the same project (§8).
- **No hand-written SQL.** Users only ever need "path column (and ID column) from table"; typing the query is error-prone and unnecessary.
- **A report, not a log.** The output is a durable record committed to the project repo. It has to read well to a person, say clearly when nothing is missing, and make it
  easy to act on many missing files at once — hence grouping by reason instead of a reason column.
- **Two directions.** A project can go wrong both ways: an index row whose file is gone (*missing*), or a file on disk that nobody indexed (*unindexed*). Checking only the
  first leaves the second invisible, so the app answers both, from the same inputs, in one window.

## 2. Goals, non-goals, anti-goals

**Goals**

- Verify every row of a file index table: the file exists, and (optionally) is not empty.
- Distinguish *missing* (definitely not there, or empty) from *unverified* (could not be checked, and why).
- Produce a readable, deterministic plain-text report suitable for committing.
- Remember each project's inputs between runs, in a project settings file beside the report.
- Look and behave like a native Windows 95/98 utility.
- Find every file under chosen folders whose full path is not in the index table (milestone 4), at the scale of millions of files and rows, over network shares.
- Let the user say which file types count, with wildcard extension patterns and a blacklist, and group the unindexed files in the report so they can be acted on in bulk.

**Non-goals**

- How missing files, or unindexed files, are followed up. That varies by project and is outside this app.
- Translating between path spellings (`\\server\share` versus a mapped drive, relative paths): the index holds full paths that match the disk exactly, apart from letter case.
- Guessing which files matter: the user's file-type lists decide, never the app.
- SQL authentication, other database engines, or arbitrary queries.
- Automated tests. This is a small app; none are written.
- Multi-project / layered architecture. Everything lives in one project.
- Installers, auto-update, localisation.

**Anti-goals** (it would be a failure even if it technically worked)

- Reporting a file as *missing* when the truth is that it could not be checked (the old app's `File.Exists` did this silently).
- A report that is hard to scan when hundreds of files are missing.
- Settings that leak between projects or between users: one project's settings silently saved over another project's file, or a per-user file that follows a person
  into a project that is not theirs.
- Asking the same save question again after the user has answered it, until the project is closed or the app is relaunched.
- Losing the user's edits without asking when the window or the project is closed.
- Discovering after a long run that the report cannot be written.
- Reporting a file as *unindexed* when it is indexed (a hash collision, a letter-case difference, or a stray space in the table), or silently skipping a folder that could
  not be read.

## 3. Technology

- **.NET 10**, Windows only. A single WinExe project.
- **Avalonia 11.3** with the **Classic.Avalonia** theme (Windows 95/98 chrome). The project template was created on Avalonia 12; it is downgraded to 11.3 because
  Classic.Avalonia targets Avalonia 11. The Fluent theme and Inter font packages are removed.
- **CommunityToolkit.Mvvm** (source-generated `[ObservableProperty]` / `[RelayCommand]`).
- **ErrorOr** for expected failures: services return `ErrorOr<T>` with custom errors instead of throwing.
- **Microsoft.Data.SqlClient** for SQL Server.
- **System.Text.Json** (source-generated context) for settings.
- **System.Security.Cryptography.Pkcs** pinned as a direct reference to a patched version, because Microsoft.Data.SqlClient 7.1.1 pulls in a vulnerable one (GHSA-846f-f764-76mx,
  GHSA-2fmv-rv2c-59x7). Remove the pin once SqlClient references a fixed version.

Explicitly not used: Mediator/CQRS, FluentValidation, EF Core/Dapper, Serilog, `System.IO.Abstractions`, test projects. No other package is added without the user's
say.

## 4. Naming

| Thing                               | Name                                            |
|-------------------------------------|-------------------------------------------------|
| Product (title bar)                 | File Auditor                                    |
| Solution                            | `MediQuant.FileAuditor.slnx`                    |
| Project / root namespace / assembly | `FileAuditor` under `Source/FileAuditor/`       |
| Settings file (per project)         | `FileAuditorSettings.json`, beside the report   |
| Missing report file (built)         | `FileAuditorMissingReport_<Audit Stage>.txt`    |
| Missing report file (custom)        | `FileAuditorMissingReport.txt` (default, editable) |
| Unindexed report file (built)       | `FileAuditorUnindexedReport_<Audit Stage>.txt` |
| Unindexed report file (custom)      | `FileAuditorUnindexedReport.txt` (default, editable) |
| Version                             | 1.0.0                                           |
| Company / copyright                 | MediQuant / Copyright © 2026 MediQuant          |

The existing `Source/FileChecker/` template project and `MediQuant.FileChecker.slnx` are renamed accordingly. The Git repository and its folder keep their current name (renaming
the GitHub repository is the user's call, outside this design).

## 5. The main window

A single, **fixed-size** window: 640 × 761 pixels, not resizable, no maximise button; minimise and close only. Classic.Avalonia supplies the Win9x chrome. The size is the
same on both tabs (the tab control has a fixed height, §5.2a), so switching tabs never moves the window's edges. Tab order runs top to bottom, left to right.

The window holds one audit **direction** per tab (a standard Win9x tab control): **Missing Files**, where every file the index table points to is on disk, and **Unindexed
Files**, where every file on disk is in the index table. The inputs both directions use (the Database and File Index Table groups, side by side) stay above the tabs; the
Report group is shown on every tab and its values are shared (§5.3).

**Win9x conventions.** Every window looks and behaves like a native Windows 95/98 utility:

- **Group boxes** organise related inputs; labels sit to the left of their inputs and end with a colon.
- **Access keys** on every label, input, button and menu item (`_` prefix in XAML), unique within a window. The exceptions are *OK* and *Cancel* in dialogues and a few
  controls on the Unindexed Files tab for which no letter is free (§11).
- **Command buttons** are 75 × 23 minimum, laid out horizontally; the action comes first, the escape after it (*Start* then *Stop*, *OK* then *Cancel*, *Overwrite*
  then *Cancel*).
- **Ellipsis (`...`)** only on a command that needs more input before it completes (e.g. *Browse...*). A command that acts immediately — including opening a file in
  another program, like *View Report* — has none.
- **Menus:** *File* ends with *Exit*; *Help* ends with *About File Auditor*. Menu items mirror the buttons and enable and disable with them.
- **Dialogues** are modal, fixed-size, with an icon (error, warning or question) and buttons along the bottom right. A path in dialogue text sits on its own line and
  wraps only after a `\` or `/` separator, never at a space or hyphen inside a folder name (`PathText.Breakable`).
- **No modern affordances** that Win9x lacked: no rounded corners, no flyouts, no toast notifications, no accent colours, no hamburger menus.

Layout, top to bottom. The approved mock-ups are [`direction-tabs-layout.html`](../UserInterfaceDesign/Mockups/direction-tabs-layout.html), which shows the target layout
with the tab control (including milestone 4's *Unindexed Files* tab), [`main-window-layout.html`](../UserInterfaceDesign/Mockups/main-window-layout.html) and
[`report-section-states.html`](../UserInterfaceDesign/Mockups/report-section-states.html). They are a design reference only: the latter's state 1 predates the Interim
default for Audit Stage, and the last two predate the tab control, the per-direction report names, the *Options* group, the final Report-file rows, the access keys, the *Audit*
group (named *Progress* there), and the project settings (the *Save project settings with the report* checkbox and the *Open project...* and *Close project* menu items,
milestone 2). All of them predate milestone 4's final layout (the Database and File Index Table groups side by side, the fixed tab height, the compact Unindexed tab and the File
Types dialogue), and they are kept as historical references, not refreshed. The XAML and this section are the contract.

```text
┌ File Auditor ───────────────────────────────────────────────── _ × ┐
│ File  Help                                                         │
│ ┌ Database ────────────────────┐ ┌ File Index Table ─────────────┐ │
│ │ Server:   [                ] │ │ Table:       [               ]│ │
│ │ Database: [                ] │ │ Path column: [               ]│ │
│ │                              │ │ ID column:   [        ] (Opt.)│ │
│ └──────────────────────────────┘ └───────────────────────────────┘ │
│  ┌─────────────┐ Unindexed Files                                   │
│ ┌┘Missing Files└─────────────────────────────────────────────────┐ │
│ │ ┌ Options ───────────────────────────────────────────────────┐ │ │
│ │ │ [x] Report empty (zero-byte) files as missing              │ │ │
│ │ └────────────────────────────────────────────────────────────┘ │ │
│ │ ┌ Report ────────────────────────────────────────────────────┐ │ │
│ │ │ [ ] Use a custom report location and file name             │ │ │
│ │ │ Project folder: [D12345  ]                                 │ │ │
│ │ │ ┌ Project Stage ───────────┐ ┌ Audit Stage ─────────────┐  │ │ │
│ │ │ │ ( ) Sample  (•) Final    │ │ (•) Interim  ( ) Final   │  │ │ │
│ │ │ └──────────────────────────┘ └──────────────────────────┘  │ │ │
│ │ │ Location:    [U:\Projects\D12345\DE\Final\]  Browse...     │ │ │
│ │ │ File name:   [FileAuditorMissingReport_Interim.txt]        │ │ │
│ │ │ [x] Save project settings with the report                  │ │ │
│ │ └────────────────────────────────────────────────────────────┘ │ │
│ │ ┌ Audit ─────────────────────────────────────────────────────┐ │ │
│ │ │ Total files: 124,500    Elapsed:      00:02:14             │ │ │
│ │ │ Checked:     61,200     Remaining:    00:02:18             │ │ │
│ │ │ Missing:     12         Files/second: 456.72               │ │ │
│ │ │ Unverified:  3                                             │ │ │
│ │ │ [██████████████████                            ]           │ │ │
│ │ │ [View Report]                      [Start]  [Stop]         │ │ │
│ │ └────────────────────────────────────────────────────────────┘ │ │
│ └────────────────────────────────────────────────────────────────┘ │
│ Checking files...                                        │ 49%     │
└────────────────────────────────────────────────────────────────────┘
```

The diagram shows the Missing Files tab with the Report group in its built state. The Unindexed Files tab (§5.2b) replaces the Options group with *Folders to Check* and adds a
*Group file types by* row to the Report group:

```text
┌ File Auditor ───────────────────────────────────────────────── _ × ┐
│ File  Help                                                         │
│ ┌ Database ────────────────────┐ ┌ File Index Table ─────────────┐ │
│ │ Server:   [                ] │ │ Table:       [               ]│ │
│ │ Database: [                ] │ │ Path column: [               ]│ │
│ │                              │ │ ID column:   [        ] (Opt.)│ │
│ └──────────────────────────────┘ └───────────────────────────────┘ │
│  Missing Files  ┌───────────────┐                                  │
│ ┌──────────────┘Unindexed Files└─────────────────────────────────┐ │
│ │ ┌ Folders to Check ──────────────────────────────────────────┐ │ │
│ │ │ Include subfolders   Folder                                │ │ │
│ │ │ ┌──────────────────────────────────────┐ ┌──────────┐      │ │ │
│ │ │ │ [x]  U:\Projects\D12345\Images       │ │ Add...   │      │ │ │
│ │ │ │ [ ]  U:\Projects\D12345\Scans        │ │ Remove   │      │ │ │
│ │ │ └──────────────────────────────────────┘ └──────────┘      │ │ │
│ │ │ File types: [.bmp .doc .docx .eml .gif ..   ]  [ Edit... ] │ │ │
│ │ │ Exclude:    [                              ]  [ Edit... ]  │ │ │
│ │ └────────────────────────────────────────────────────────────┘ │ │
│ │ ┌ Report ────────────────────────────────────────────────────┐ │ │
│ │ │ [ ] Use a custom report location and file name             │ │ │
│ │ │ Project folder: [D12345  ]                                 │ │ │
│ │ │ ┌ Project Stage ───────────┐ ┌ Audit Stage ─────────────┐  │ │ │
│ │ │ │ ( ) Sample  (•) Final    │ │ (•) Interim  ( ) Final   │  │ │ │
│ │ │ └──────────────────────────┘ └──────────────────────────┘  │ │ │
│ │ │ Location:    [U:\Projects\D12345\DE\Final\]  Browse...     │ │ │
│ │ │ File name:   [FileAuditorUnindexedReport_Interim.txt]      │ │ │
│ │ │ Group file types by: (•) Listed type  ( ) Found extension  │ │ │
│ │ │ [x] Save project settings with the report                  │ │ │
│ │ └────────────────────────────────────────────────────────────┘ │ │
│ │ ┌ Audit ─────────────────────────────────────────────────────┐ │ │
│ │ │ Total files: 48,207     Elapsed:      00:01:29             │ │ │
│ │ │ Indexed:     48,187     Files/second: 541.65               │ │ │
│ │ │ Unindexed:   20                                            │ │ │
│ │ │ Unverified:  3                                             │ │ │
│ │ │ [██████████████████                            ]           │ │ │
│ │ │ [View Report]                      [Start]  [Stop]         │ │ │
│ │ └────────────────────────────────────────────────────────────┘ │ │
│ └────────────────────────────────────────────────────────────────┘ │
│ Checking files...                                        │ 49%     │
└────────────────────────────────────────────────────────────────────┘
```

With *Use a custom report location and file name* ticked, the Report group hides its Project folder row and both stage groups on either tab (§5.3).

### 5.1 Database group

- **Server** and **Database**: text inputs, both required.
- The connection string is always built as `Data Source=<Server>; Initial Catalog=<Database>; Integrated Security=True; TrustServerCertificate=True` (via
  `SqlConnectionStringBuilder`). There are no user/password fields anywhere — not in the UI, not in settings.

### 5.2 File Index Table group

- **Table**: required. Accepts `table` or `schema.table`.
- **Path column**: required.
- **ID column**: optional, with a muted "(Optional)" note beside it. It is shared by both directions, but only the missing-files direction uses it: an unindexed file has no
  row, so no ID. On the Unindexed Files tab it stays editable and is still saved with the project.

The Database and File Index Table groups sit **side by side**, Database on the left and File Index Table on the right, each with its fields stacked vertically, and stay above
the tab control. They are used by every direction. (The side-by-side placement is a trial the user may reverse.)

### 5.2a Tab control and Options group

- A standard Win9x **tab control** sits below those two groups, one tab per audit direction: **Missing Files** and **Unindexed Files**. Its height is fixed (575 pixels), tall
  enough for the taller Unindexed tab, so the window does not change size when the tab changes. The selected tab is not saved; the window opens on the first tab. Switching
  tabs is allowed while an audit runs.
- The Missing Files tab holds, top to bottom: an **Options** group, the **Report** group (§5.3) and the **Audit** group (§5.4).
- **Options** holds **Report empty (zero-byte) files as missing**: checkbox, checked by default. When ticked, an existing file of length 0 counts as *missing (empty)*. It
  applies to the missing-files direction only, which is why it is not in the shared File Index Table group.

### 5.2b The Unindexed Files tab

The tab holds, top to bottom, the **Folders to Check** group, the **Report** group (§5.3) and the **Unindexed Audit** group (§5.4a).

**Folders to Check** holds:

- A list of folders, one per row. Each row has an **Include subfolders** checkbox in a column at the row's left, ticked for a newly added folder, and the folder's path beside
  it (cut with an ellipsis when long, the whole path in a tooltip). The list scrolls vertically only and is about as tall as the two buttons beside it.
- **Add...** and **Remove**, stacked to the right of the list. *Add...* opens a folder picker (directories only) and appends the chosen folder, selecting it; a folder already
  listed (ignoring case and a trailing separator) is not added a second time. *Remove* removes the selected row and is disabled while no row is selected.
- **File types:** a read-only, single-line summary of the extension whitelist (sorted, separated by spaces), with an **Edit...** button. A built-in list of 19 common document and
  image formats is the default (`.bmp .doc .docx .eml .gif .jpeg .jpg .msg .odt .pdf .png .ppt .pptx .rtf .tif .tiff .xls .xlsx .xps`); `.txt`, `.json` and other text or
  configuration formats are not on it, so the app's own reports and project files are never reported.
- **Exclude:** the extension blacklist, in the same form, empty by default, with its own **Edit...** button. A file that matches an Exclude entry is never checked, even when
  File types matches it: the blacklist wins.

The labels *File t_ypes:* and *E_xclude:* carry the access keys Y and X; *Add...*, *Remove* and the two *Edit...* buttons carry none, because every letter is taken on this
window (§11).

**File Types dialogue.** Each *Edit...* opens a modal dialogue (`FileTypesDialogueWindow`, titled *Edit File Types* or *Edit Exclude List*; 360 × 300, fixed-size, centred on the
main window, no maximise) that lists the entries one per row, as Windows' environment-variable editor does, with **New** (adds an empty row and focuses it), **Edit** (focuses the
selected row), **Delete** (removes the selected row; disabled while none is selected), **Reset** (restores the built-in list; shown for File types only), **OK** and **Cancel**.
**OK** validates every entry (the syntax of §6.5.1) and, when one is invalid, shows the error and keeps the dialogue open; otherwise the summary shows the normalised entries
(lower case, one leading dot, duplicates dropped, sorted). **Cancel** discards every change, including a Reset. The Folders to Check group and its buttons are locked while an
audit runs (§5.8).

### 5.3 Report group

Each tab shows a complete Report group. Its values are **one set shared by every tab**: change one on a tab and every other tab shows the change. The only per-tab item is
the **File name**, which belongs to the tab's direction (names are in §4), and, on the Unindexed Files tab only, the **Group file types by** option below it. The *Save project
settings with the report* checkbox is shared too.

- **Use a custom report location and file name**: checkbox, unchecked by default. It is the group's first control, because it decides whether the rest apply.
- **Project folder**: text input with watermark `D12345`.
- **Project Stage** radio group: *Sample*, *Final*. Default **Final**.
- **Audit Stage** radio group: *Interim*, *Final*. Default **Interim**.
- While *Project Stage = Sample*, *Audit Stage* is forced to **Interim** and the group is disabled (read-only). Switching back to *Final* re-enables it and keeps Interim
  selected.
- **Location** and **File name**:
    - *Unticked (built):* both are read-only previews. Location is `U:\Projects\<Project Folder>\DE\<Project Stage>\`; File name is
      the tab's built name, `FileAuditorMissingReport_<Audit Stage>.txt` on the Missing Files tab and `FileAuditorUnindexedReport_<Audit Stage>.txt` on the Unindexed Files tab.
      An empty Project folder shows as the literal placeholder
      `<Project Folder>`. **Browse...** is disabled.
    - *Ticked (custom):* the Project folder row and both stage groups are **hidden**, not disabled, on both tabs, and come back when the box is unticked (the user's decision).
      Location becomes an editable directory path, empty by default, with **Browse...** opening a *folder* picker (directories only). File name becomes editable, defaulting to
      the tab's custom default, `FileAuditorMissingReport.txt` on the Missing Files tab and `FileAuditorUnindexedReport.txt` on the Unindexed Files tab; each tab keeps its own
      custom file name. The two names must differ (§6.1).
- **Group file types by** (Unindexed Files tab only): two radio buttons, *Listed type* (default) and *Found extension*, on the row above the Save checkbox. It chooses how the
  unindexed report groups files (§7.1) and is saved in the project file (§8). It belongs to the direction, so the Missing Files tab does not show it.
- **Save project settings with the report**: checkbox on the group's last row, below *File name*. It is **ticked by default** — on launch, after *Open project...* and after
  *Close project* — and its state is not stored in the settings file; it is a per-session preference. It is disabled while an audit is running. Ticking it needs no
  confirmation. **Unticking** first shows a warning dialogue (*"If you untick this box, this project's settings will no longer be saved automatically. You will be asked
  whether to save them instead. Do you want to untick it?"*) with **Untick** and **Cancel**; *Cancel* leaves the box ticked. Once *Untick* has been chosen the warning is not
  shown again, and later unticking goes straight through, until *Close project* or a fresh launch. What the box does is set out in §8.

### 5.4 Audit group (Missing Files tab)

The Audit group sits at the bottom of its tab, below the Report group; each direction's tab has its own, so a tab's statistics are those of its own last run.

- Read-only statistics in two columns: **Total files**, **Checked**, **Missing**, **Unverified**; **Elapsed**, **Remaining**, **Files/second**. Counts use thousands
  separators (`N0`); times are `hh:mm:ss`; rate has two decimals. Blank before the first run.
  **Files/second** is the files checked divided by the whole run's **Elapsed** (the span the window shows, counting included) and stays blank until at least one full
  second has elapsed. **Remaining** is estimated from the checking rate alone (files checked divided by the time spent checking, which excludes the counting), so the two
  figures are computed over different spans.
- A progress bar (percentage of rows checked).
- A row of command buttons: **View Report** left-justified; **Start** then **Stop** right-justified. There is no Close button.

### 5.4a Unindexed Audit group

The Unindexed Files tab has its own group (headed *Audit*, `UnindexedAuditView`) at the bottom of the tab. It holds:

- Read-only statistics in two columns: **Total files**, **Indexed**, **Unindexed** and **Unverified** on the left; **Elapsed** and **Files/second** on the right. There is **no
  Remaining**, neither label nor value, because the walk has no up-front count and so the total is not known until it ends. *Total files* is the files found so far, each of which
  is checked the moment it is found, so Total files = Indexed + Unindexed; *Unverified* counts folders that could not be listed, links not followed and entries that could
  not be classified (§6.5.3), and is never part of Total files. Counts, times and the rate are formatted as in §5.4; Files/second is Total files over Elapsed and stays
  blank until a full second has elapsed. All are blank before the first run.
- A progress bar. While a run is under way it shows a **moving block** a fifth of the track wide that crosses a sunken track from left to right and starts again, moved by the 250
  ms refresh timer, instead of a fill that would read as a percentage of an unknown total. When the run ends the bar is full (and the status bar's percentage pane reads `100%`);
  before the first run it is empty. Classic.Avalonia's own indeterminate marquee was not used: it is blank for about 2.4 seconds of every 5-second cycle and its animation cannot
  be restyled.
- The same row of command buttons as §5.4: **View Report** left-justified, **Start** then **Stop** right-justified.

### 5.5 Status bar

One status bar for the window, with two sunken panes: a message pane that fills the width (`Ready`, `Counting files...`, `Loading the index...`, `Checking files...`, `Writing
report...`, `Completed`, `Stopped`, `Failed`) and a fixed-width percentage pane at the right edge. **While an audit runs**, the bar shows that run's phase and percentage
whichever tab is selected, so switching tabs never hides a run. **Otherwise** it shows the selected tab's own state: with no run on that tab, the message reads `Ready` only while
that tab's **Start** is enabled, and `Enter the required details` while it is not. Once a run has ended, its result (`Completed`, `Stopped` or `Failed`) stays on that tab until
its next run starts. Each tab keeps its own last result.

### 5.6 Menu bar

- **File**: **Open project...** (access key P), **Close project** (access key C), separator, **Start** (F5), **Stop** (Esc), **View Report** (Ctrl+R), separator,
  **Exit** (Alt+F4). Start, Stop and View Report enable and disable with their buttons. *Open project...* and *Close project* have no button; they are disabled while an
  audit is running and while Start, *Open project...* or *Close project* is in progress (§5.8). **Start** and **View Report** (and F5, Ctrl+R) act on the **selected
  tab's** audit and follow that tab's buttons. **Stop** (and Esc) acts on the **running** audit, whichever tab is selected.
- **Help**: **Help Topics** (access key H, shortcut F1), separator, **About File Auditor**. *Help Topics* opens the File Auditor user guide, a wiki article, in the default
  browser by shell execute. The article's address is one hard-coded constant, `HelpLinks.UserGuideUrl`, which is an empty string until the article exists; while it is
  empty, the item and F1 are disabled.

### 5.7 About box

A small modal, fixed-size Win9x About dialog: the app icon, **File Auditor**, **Version 1.0.0**, **Copyright © 2026 MediQuant**, and an **OK** button.

### 5.8 Command states

| Command         | Enabled when                                                                                                  |
|-----------------|---------------------------------------------------------------------------------------------------------------|
| Start           | Not running and not busy, and Server, Database, Table, Path column are filled, and the selected tab's report path is complete (see below) |
| Stop            | Running (any tab)                                                                                                           |
| View Report     | Not running, and a report file exists at the selected tab's current resolved path                                           |
| Open project... | Not running and not busy                                                                                                    |
| Close project   | Not running and not busy                                                                                                    |
| Help Topics     | The user-guide address (`HelpLinks.UserGuideUrl`) is not empty                                                              |

The report path is complete when, *built*: Project folder is filled (an Audit Stage is always selected); *custom*: Location and the tab's File name are filled. All inputs,
including the *Save project settings with the report* checkbox, are disabled while a run is in progress. Start and View Report follow the selected tab; Stop follows the
running audit.

**One run at a time.** While either direction runs, both tabs' Start buttons are disabled and every input is locked: the shared groups, the Folders to Check group (the list, its
checkboxes, *Add...*, *Remove* and both *Edit...* buttons) and the Report group. Only Stop, the tab selection and the Help menu stay live.

**Busy.** Start, *Open project...* and *Close project* are *busy* from the moment the command begins until it ends, including while its pre-flight checks, save prompts, file
picker or error dialogues are open (Start's busy period runs on into the audit itself). They never overlap: while one is busy the other two are disabled, and so is Start. The
inputs, including the checkbox, are disabled as well, so nothing can be edited and then wiped by a project being opened. Closing the window while one of them is busy but no
audit is running is refused; the window can be closed once the command's prompt has been answered or its work has finished. Closing during an audit keeps its own
stop-and-exit question (§6.4).

**View Report** opens the report in the user's default program for `.txt` (shell execute). No ellipsis: it needs no further input. **Browse...** keeps its ellipsis.

## 6. Running an audit

### 6.1 Pre-flight checks (on Start, before touching the database)

1. Validate inputs (identifiers well formed; custom File name has no invalid file-name characters). With the built location, the Project folder must be a single
   folder name: not rooted, no `\` or `/`, not `.` or `..`, and no invalid file-name characters. Failure → error dialog, run does not start.
   With a custom location the two tabs' custom file names must also differ (ignoring case), since both reports are written to that one location and would overwrite each
   other. For the Unindexed direction the checks of §6.5.1 run after the identifier, Project folder and file-name checks above and before the rest of this step.
2. Report folder:
    - *Built location* and the folder does not exist → **error dialog**; the run does not start.
    - *Custom location* and the folder does not exist → **warning dialog** with **Create Folder** and **Cancel**. Create Folder tries to create it (failure → error dialog,
      run does not start).
    - Once the folder exists (or has been created), a writability probe creates and removes the report's temporary file. If the folder cannot be written to → error dialog,
      run does not start.
3. Report file already exists (either mode):
    - if it is read-only → **error dialog**, run does not start;
    - otherwise → **warning dialog** with **Overwrite** and **Cancel**.
4. Run the save rule (§8.2) for the project's settings. It may ask whether or where to save; *Cancel* on any of its prompts stops the run from starting.

### 6.2 Query

Built from the inputs; each identifier part is bracket-quoted with `]` escaped as `]]` (never string-concatenated raw):

```sql
SELECT COUNT_BIG(*) FROM [schema].[table];
SELECT [PathColumn] AS [Path], [IdColumn] AS [Id] FROM [schema].[table];   -- ID part omitted when no ID column
```

The count runs first (status `Counting files...`) so progress and remaining time can be shown. Values are read as strings (`NULL` → empty). Command timeouts are long
enough for large tables. The unindexed direction reads only the path column, in the query of §6.5.2.

### 6.3 Checking a file

Each row is checked concurrently with bounded parallelism (builder chooses the mechanism and degree; the old app's unbounded `ThreadPool` + 1 million queue cap is not a
requirement). Each file lands in exactly one outcome:

| Outcome                                       | When                                                                             |
|-----------------------------------------------|----------------------------------------------------------------------------------|
| Found                                         | The file exists (and, if the empty option is on, its length > 0)                 |
| Missing — Does not exist                      | The path's root exists, but the file or a directory below the root does not      |
| Missing — Empty                               | Empty option on, and the file exists with length 0                               |
| Unverified — Drive or share not found         | The path's root (drive letter, or `\\server\share`) does not exist for this user |
| Unverified — Permission denied                | Access to the file or its location is denied                                     |
| Unverified — Network location unreachable     | The server or share in a UNC path cannot be reached                              |
| Unverified — Path too long                    | The path exceeds what the system can handle                                      |
| Unverified — Link not followed                | Unindexed audit only: a link (junction or symlink) was skipped, not checked      |
| Unverified — Invalid characters in path       | The path is malformed or contains illegal characters                             |
| Unverified — Path is empty in the index table | The path value is `NULL`, empty or whitespace                                    |
| Unverified — Other errors                     | Any other I/O failure; the reason and system message are recorded per file       |

The check must not use `File.Exists` alone, because it returns `false` for every failure and would report unreachable files as missing. Use an API that surfaces the error (e.g.
`FileInfo` / `File.GetAttributes`) and map exceptions and Win32 error codes to the outcomes above.

**Roots are checked once.** The root of each path (a drive letter such as `U:\`, or `\\server\share`) is checked the first time it is seen and the result is cached for
the run. If the root does not exist, every file under it is *Unverified — Drive or share not found* without being tried. *Missing — Does not exist* is reserved for files
whose root exists, so an unmapped drive or a missing share can never flood the Missing section. A root confirmed reachable is re-probed when a file under it comes back
not-found and the root's last confirmation is 5 seconds old or more. If the root no longer exists, that file and every later file under it are *Unverified — Drive or
share not found*; if the root now fails for another reason (permission, network), they take that outcome instead.

Two mappings guard the same rule: a .NET DriveNotFoundException maps to *Unverified — Drive or share not found* (never *Missing*), and a path segment longer than 255
characters maps to *Unverified — Path too long* before the OS is asked.

**Unreachable locations short-circuit, at the level that actually failed.** When a file whose root exists comes back unreachable for any reason (*Permission denied*, *Network
location unreachable* or *Other errors*), the checker walks the path one directory at a time from the root down (`x\`, then `x\y\`, then `x\y\z\`) to find the
first directory that cannot be reached. That directory and its reason are cached for the run, and every later file *under that directory* gets the same Unverified
outcome without being tried. Files elsewhere are still checked: if `x\y\z\` is unreachable, files in `x\` and `x\y\other\` are not affected. If every directory in the
path is reachable, the failure belongs to the file itself and nothing is cached. Short-circuited files are still listed individually in the report. Path-shape failures (*Path too
long*, *Invalid characters in path*, *Path is empty in the index table*) never short-circuit other files.

### 6.4 Progress, stop and failure

This section describes the missing-files audit; §6.5.4 gives the unindexed audit's differences. Closing the window mid-run and the save rule behave the same for both.

- The UI refreshes stats a few times per second. Remaining time is estimated from the checking rate so far; Files/second is over the whole run (§5.4).
- **Stop**: stops reading rows and checking files, then writes the report with `Result: Stopped by user`. Counts reflect only what was checked.
  If Stop is pressed while the rows are still being counted (before the count query returns), no report is written and the status bar reads `Stopped`: nothing
  has been checked, so there is nothing to report.
- **SQL failure** (connection, bad table/column, timeout): error dialog with the message. If no rows were checked, no report is written. If it fails mid-run, the report is
  written with `Result: Failed — <message>` and the counts so far.
- **Report write failure**: error dialog.
- **Closing the window mid-run**: a prompt, *"An audit is in progress. Stop it and exit?"*, with **Yes** and **No**. Yes stops the run, writes the report as
  `Stopped by user`, then exits; No carries on. File → Exit and Alt+F4 behave the same. If Yes is chosen before the count query returns, no report is written (as for
  Stop) and the application simply exits.
- **Closing the window, in general**: every close, including one caused by the operating system logging off or shutting down, runs the save rule (§8.2) before the window
  goes. During a run the stop-and-exit prompt above comes first, and the save rule runs only if the answer is Yes. *Cancel* on any of the save rule's prompts keeps the
  window open.

### 6.5 Running the unindexed audit

Start on the Unindexed Files tab runs the pre-flight checks of §6.1 (the ID column is not used), then those of §6.5.1, then the run itself.

#### 6.5.1 Pre-flight additions

Each is an error dialogue that stops the Start, unless marked *warning*. The first problem found is the one shown, with the offending entry.

- **Folders:** at least one folder; each a fully qualified path that exists (probed off the UI thread, since it may be on a slow share); no folder listed twice, compared in
  canonical form (a full path with one trailing separator, ignoring case), so `U:\A` and `u:/a/` are the same folder.
- **File types syntax.** An *entry* is one or more dot-separated *parts*, such as `pdf`, `tar.gz`, `zip.001` or `zip.*`. A leading dot is optional (`pdf` and `.pdf` are the same)
  and a leading `*.` means the same as a leading dot. Entries are case-insensitive and are kept in lower case with a leading dot, duplicates dropped, sorted. `*` stands for any
  run of characters and `?` for exactly one, the usual glob meanings; there are no character sets, and a wildcard stays within one part (it never matches a dot). An entry is
  invalid when it is empty after the leading dot, has an empty part (`a..b`), or contains a path separator or a character Windows forbids in file names other than `*` and `?`.
  The File Types dialogue accepts only valid entries, one value per line (a line holding a space or a comma is refused as not valid), so an invalid one can reach Start only
  through a hand-edited project file. Opening such a file keeps the invalid entries as written, so the tab's summary shows them, and Start names the first one in an error.
- **File types** has at least one entry.
- **Exclude** has no entry that matches every file (error: it would exclude everything).

An entry **matches every file** when it has exactly one part, that part is made only of wildcards, and it contains at least one `*` and at most one `?`. So `*`, `*.*` (the same
as `*`), `?*` and `**` match every file, while `?` (every one-character extension) and `.*.*` (two parts) do not, and are accepted. Messages show the normalised entry, in which
`*` appears as `.*`.

Three **warnings** (*Continue* and *Cancel*) follow, in this order. *Cancel* stops the Start. Each one, once answered *Continue*, is not asked again until *Close project*, *Open
project...* or a fresh launch, like the remembered answers of §8.1; each has its own remembered answer, and none is saved in the project file.

1. File types has a match-every-file entry **and other entries**: the wildcard makes the others redundant.
2. File types has a match-every-file entry **and** Exclude is not empty: every file will be checked except the excluded types.
3. The same entry (after normalising) is in both lists: the blacklist wins, so that type is not checked. Only identical entries count; overlapping patterns such as `zip.*` and
   `zip.000` are ordinary use and raise nothing.

A match-every-file entry alone in File types, with an empty Exclude list, is accepted without a warning.

#### 6.5.2 Loading the index

Status `Loading the index...`. Only the path column is read (never the ID), forward-only and with no command timeout:

```sql
SELECT [PathColumn] AS [Path] FROM [schema].[table] WHERE <prefix filter>;   -- every part bracket-quoted as in §6.2
```

- **Prefix filter.** For one to 20 folders the statement asks SQL only for rows under those folders: one case-insensitive `LIKE '<folder>\%'` per folder (`COLLATE
  Latin1_General_100_CI_AS`, so a case-sensitive column collation cannot drop rows), with `|`, `%`, `_` and `[` escaped, joined with `OR`. The filter ignores *Include subfolders*
  (a folder's prefix is a superset of its direct files), so tables that span many projects send only the relevant rows. A further `OR` clause selects every row whose value
  **starts with a whitespace character**, because the values are trimmed before they are matched (§6.5.3) and such a row would miss every prefix. With no folder, more than 20
  folders, or an empty folder path, the filter is dropped and every row is loaded. Each folder is made canonical once (a full path with one trailing separator) and the same
  list goes to the filter and to the walk, so the filter loads exactly the rows whose prefixes the walk reports under, whatever form the folder was typed in (`U:/A/./B`, other
  letter case, a trailing separator). So the filter never changes the result. There is one index set only: links are never followed (§6.5.3), so no file is matched by any path
  but its own.
- **Hashes.** Each path is trimmed, upper-cased (invariant culture) and hashed to **128 bits** (MD5 of its UTF-16 bytes: an in-box algorithm chosen for speed and even spread, not
  security); only the hashes are kept, in a set. `NULL` values are skipped. A 128-bit hash makes a false match, which would hide an unindexed file (anti-goal), practically
  impossible. Memory is about **48 MB per million rows**, measured (one million rows loaded in 1.6 seconds on a local SQL Server), so three million rows take roughly 150 MB.
- A path is **indexed** when its full path equals a value in the column ignoring letter case and nothing else; leading and trailing whitespace in the value is ignored.

#### 6.5.3 Walking the folders

Status `Checking files...`.

- **One traversal** over the union of the input folders, not one walk per input. Up to **16 folder listings run at once**, on dedicated threads, because each listing over a
  network share is a round trip; the per-file lookup in the hashes takes a microsecond, so files are not batched across threads. A listing returns the files' attributes, so hidden
  and system files and folders are skipped with no further disk access; **a hidden or system folder's contents are skipped with it**.
- **Which files are checked.** A file name is split on dots; the first part is the *stem* and must not be empty, so a name with no dot, or one that starts with a dot
  (`.gitignore`), has no extension and matches nothing. An entry of *k* parts matches a name that has at least *k* parts after the stem when the name's **last *k* parts** match
  the entry's parts one for one, each compared with its wildcards, ignoring case. So `.zip.001` matches `Backup.zip.001` but neither `Backup.zip` nor `Backup.zip.001.bak`;
  `zip.*` matches `Backup.zip.01` and `Backup.zip.001`; and with File types `.gz` and Exclude `.tar.gz`, `x.gz` is checked and `x.tar.gz` is not (the blacklist wins). A file that
  matches several File types entries belongs to the **most specific**: the most parts, then the fewest wildcard characters, then the first alphabetically. A file is checked
  only when a File types entry matches it and no Exclude entry does.
- **Nested inputs.** A file reached through two input folders (one inside another) is checked and reported **once**, under the deepest input folder whose scope covers its
  folder. With inputs `A` (with subfolders) and `A\B` (without), `A\B\x.pdf` is reported under `A\B` and `A\B\C\y.pdf` under `A`.
- **Links are not followed** (the user's decision, after three rounds of review found edge cases in following them). Every junction or symbolic link met is skipped, reported and
  never checked: an **input folder** that is a link (it is not walked, and the run goes on with the other inputs; if every input is a link the run completes with no files), a
  **folder** met during the walk (its target's files are not counted unless the target is walked directly as an input), and a **file** that is a symbolic link and matches the file
  types (it is in neither *Total files*, *Indexed* nor *Unindexed*). Each is reported once under *Unverified* in the group *Link not followed* (§7.1) and counts towards
  *Unverified*. Only an entry that **has a link target** is a link; any other reparse point (a DFS namespace folder, a OneDrive placeholder, a deduplicated file) is an ordinary
  folder or file. An entry whose link target cannot be read is not guessed at: it is reported under *Other errors* (reason `Could not tell whether this is a link`) and neither
  walked nor checked. A hidden or system junction (for example the profile's `Application Data`) is skipped by the hidden and system rule and is not reported. An input whose own
  path passes through a junction, or that lies under an input that is a refused link, is still walked, because it is the user's explicit choice. With no link followed, every
  folder is reached by one route, so no loop and no duplicate is possible; an input nested inside another is queued once, as an input.
- **Unverified folders.** A folder that cannot be listed (permission denied, share unreachable, path too long, any other I/O failure; a folder deleted mid-run is *Other errors*)
  is reported once as **unverified** and nothing beneath it is walked. Its contents are not checked, so it is counted separately (*Unverified*), never in *Total files*. A listing
  that fails part-way reports none of that folder's files, so a folder is either wholly checked or wholly unverified.

#### 6.5.4 Progress, stop and failure

- **Counting.** Every file found is checked at once, so *Total files* = *Indexed* + *Unindexed*; the audit never opens a file, so no single file can fail. The UI refreshes four
  times a second. There is no up-front count, no percentage and no *Remaining* (§5.4a).
- **Stop** while the index is loading: no report is written and the status bar reads `Stopped`. After that, Stop (button, menu and Esc) stops starting new listings, abandons the
  ones in progress (their files are not reported) and writes the report with `Result: Stopped by user`; the counts are what was found and checked, and there is no *Not checked*
  line, since the total is unknown. A Stop that arrives after the walk has finished but before the run reads its stop request produces a *Stopped by user* report for a
  complete walk; this is cosmetic.
- **SQL failure** while loading the index (connection, bad table or column): an error dialogue, and no report, since nothing has been walked. **Report write failure:** an error
  dialogue.
- **Closing the window mid-run** asks the question of §6.4; *Yes* stops the run as above (no report if the index was still loading) and exits.

## 7. The report

Plain text, UTF-8 without BOM, Windows (CRLF) line endings. Timestamps are local time (a person reads this). Within each group, files are sorted by path (ordinal,
case-insensitive) so re-runs produce stable diffs. Each file line is `<IdColumn> <IdValue>   <Path>` when an ID column is set, otherwise just `<Path>`. Columns are aligned
with spaces.

This is the **missing-files** report (`FileAuditorMissingReport_<Audit Stage>.txt`); the unindexed-files report, titled *File Auditor Unindexed Report*, follows in §7.1.

```text
File Auditor Missing Report
===========================

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
- With no ID column set, a file line is just the path, so a row in *[Path is empty in the index table]* prints as a **blank line** — one per such row, so the
  group's count still matches its lines. This is intended; the user guide should call it out.
- `ID column:` reads `(none)` when not set. `Empty files:` reads `Reported as missing` or `Not reported`. The Project folder / stage lines are omitted when a custom location
  is used.
- Missing groups appear in the order *Empty*, *Does not exist*; Unverified groups in the order of the table in §6.3, *Other errors* last.
- `Total files:` is always the table's row count. When the result is *Stopped by user* or *Failed*, the Summary adds a `Not checked:` line, so that Found + Missing +
  Unverified + Not checked = Total files. The line is omitted for a completed run.

### 7.1 The unindexed report

`FileAuditorUnindexedReport_<Audit Stage>.txt`, with the same rules as §7 (UTF-8 without BOM, CRLF, local timestamps, deterministic order, aligned labels):

```text
File Auditor Unindexed Report
=============================

Server:          KDCSQL15
Database:        InterimBJC_Soarian_DocStorage
Table:           _DMS_Generic_Index
Path column:     MQ_FullPath
Folders:         U:\Projects\D12345\Images (with subfolders)
                 U:\Projects\D12345\Scans
File types:      .doc .docx .jpg .pdf .png .tif .tiff .zip.*
Exclude:         .zip.000
Grouped by:      Listed type
Project folder:  D12345
Project stage:   Final
Audit stage:     Interim

Started:         2026-11-08 14:02:11
Finished:        2026-11-08 14:03:40
Elapsed:         00:01:29
Result:          Completed

Summary
-------
Total files:     48,207
Indexed:         48,187
Unindexed:       20
Unverified:      3

Unindexed Files (20)
--------------------
[U:\Projects\D12345\Images (17)]

  [.pdf (2)]
  U:\Projects\D12345\Images\2019\05\10871.pdf
  U:\Projects\D12345\Images\2019\06\11002.pdf

  [.tif (15)]
  U:\Projects\D12345\Images\2019\04\10443.tif
  ...

[U:\Projects\D12345\Scans (3)]

  [.jpg (3)]
  U:\Projects\D12345\Scans\IMG_0041.jpg
  ...

Unverified (3)
--------------
[Permission denied (2)]
U:\Projects\D12345\Images\Restricted\
U:\Projects\D12345\Images\Restricted2\

[Other errors (1)]
U:\Projects\D12345\Images\2020\   I/O error   The semaphore timeout period has expired.
```

- **Unindexed Files** are grouped by **input folder** (in the order listed), then by **file type** (alphabetical), then sorted by path (ordinal, ignoring case). A folder group
  and a file-type group are each wrapped in square brackets with their counts; type groups and file lines are indented two spaces; a blank line separates the groups. Every folder
  that has unindexed files gets its group, even when only one folder was chosen; groups with none are omitted. `No unindexed files.` when there are none.
- **File-type groups** follow the *Group file types by* option (§5.3), saved in the project file:
    - *Listed type* (default): one group per File types entry as normalised, such as `[.zip.* (12)]` or `[.pdf (2)]`.
    - *Found extension*: one group per real extension found, the file name's last *k* parts in lower case, where *k* is the matching entry's part count, such as `[.zip.001 (5)]`
      and `[.zip.002 (7)]`.
    - Either way, a file that matches several File types entries is grouped through the most specific one (§6.5.3).
- **Header.** `Folders:` lists each input folder on its own line, `(with subfolders)` where ticked (`(none)` when the list is empty). `File types:` and `Exclude:` are each sorted
  and space-separated, and `Exclude:` reads `(none)` when empty. `Grouped by:` reads `Listed type` or `Found extension`. The Project folder and stage lines are omitted when a
  custom location is used. There is no ID column line.
- **Summary.** `Total files:` is the files found and checked, so Total files = Indexed + Unindexed. `Unverified:` counts the folders that could not be listed plus the links
  that were not followed, not files checked. A *Stopped by user* report has `Result: Stopped by user`. A run that fails while loading the index at the start writes no report
  (§6.5.4).
- **Unverified** lists the folders that could not be listed and the links that were not followed, in groups ordered as in §6.3 with *Link not followed* before *Other errors*:
  *Drive or share not found*, *Permission denied*, *Network location unreachable*, *Path too long*, *Link not followed*, *Other errors*. A folder path ends in `\`; a file link is
  a plain path. *Link not followed* lists every junction or symbolic link that was skipped (§6.5.3): an input folder, a folder met in the walk, or a matching file. *Other errors*
  shows the reason and the system message in aligned columns and also holds a folder whose path is malformed or any failure not listed above (reason text, no system message).
  An item is listed once, and nothing beneath a folder is. `No unverified folders or links.` when there are none. An entry whose link status cannot be read is reported under
  *Other errors* with the reason `Could not tell whether this is a link`, and is neither walked nor checked.

## 8. Settings

Settings belong to the **project**, not the Windows user. They are saved as `FileAuditorSettings.json` beside the project's report, so that the file lives in the project's
repository with the report and any teammate can open the same project. The per-user `%APPDATA%\MediQuant\FileAuditor\settings.json` of earlier builds is no longer read or
written; it is left on disk untouched and is not migrated.

The file holds every input of every direction: server, database, table, path column, ID column, empty-files option, project folder, project stage, audit stage (default Interim),
custom-location flag, custom location, and the custom file name of the missing-files report (`MissingCustomFileName`, default `FileAuditorMissingReport.txt`). The unindexed
direction adds its folder list (`ScanFolders`: each folder's path and *Include subfolders* flag), the whitelist (`FileTypes`), the blacklist (`ExcludeTypes`), the grouping choice
(`GroupByFoundExtension`) and its own custom file name (`UnindexedCustomFileName`, default `FileAuditorUnindexedReport.txt`). A file without them loads with an empty folder list,
the 19 built-in file types, an empty blacklist, *Listed type* grouping and the default name; a folder entry without a path is dropped, and an invalid entry in either type list is
dropped on its own while the others are kept. Older files load, but their custom file names fall back to the defaults; no migration is attempted. The remembered answers to the
three file-type warnings (§6.5.1) are not stored either. No credentials. The *Save project settings with the report* checkbox is not stored in it. The file is written with a temp
file + `File.Move(overwrite: true)`; a save never creates a folder. A null string value or an undefined enum value in the file falls back, field by field, to its default; other
values are kept. Window size and position are not saved — the window is fixed-size. The milestone 2 spec (`Docs/Specs/Epic-1-MVP/milestone-2-project-settings-design.md`) has the
full prompt texts.

### 8.1 Start-up, opening and closing a project

- **Start-up:** the app opens with every input at its default (`AppSettings.Default`) and no project open. Nothing is read from disk, and the checkbox is ticked.
- **Open project...** runs the save rule (§8.2) for the current project; *Cancel* there stops. It then shows a file picker titled *Open Project*, filtered to JSON files, with
  `FileAuditorSettings.json` suggested. The picker starts in the report's *Location* when that is a full path to a folder that exists, otherwise in the user's profile folder.
    - *Picker cancelled:* nothing changes; the current project stays open as it was, remembered answers included.
    - *File cannot be read* (invalid JSON, access denied, not a settings file, or a picked file that is not on a local or network drive): an error dialogue; the current
      project stays open unchanged. A file is *not a settings file* — "the file is not a File Auditor project." — when its JSON is not an object or the object holds none of
      the settings properties, so `{"foo":1}` and `{}` are both refused rather than opened as a project of defaults. A file with at least one settings property opens, and
      the properties it lacks take their defaults. The file is read off the UI thread, and the picker's start-folder probe is abandoned after two seconds (the profile
      folder is used instead), so a slow or disconnected drive does not freeze the window.
    - *File read:* the project is reset as for *Close project*, the file's settings are loaded, and it becomes the project's file. The loaded settings are the baseline for
      "nothing has changed" (§8.2). The window title does not show the open project.
- **Close project** runs the save rule, then resets every input to its default, ticks the checkbox, forgets the *No* answer, the untick-warning answer, the three file-type
  warning answers (§6.5.1) and the open file, and clears both Audit groups' statistics and the last run's status (the status bar reads `Enter the required details`, §5.5). It is
  equivalent to relaunching the app.

### 8.2 The save rule

One rule, applied at four save points: **Start** (after the pre-flight checks pass, §6.1 step 4), **closing the window** (§6.4), **Close project** and **Open project...**.

1. **Nothing has changed** since the project was opened or last saved (the current inputs equal the settings last loaded or saved; a fresh, untouched window counts as
   unchanged) → do nothing, and do not ask.
2. **Checkbox ticked** → save (§8.3) without asking.
3. **Checkbox unticked and the user has already answered No** this session → do nothing.
4. **Checkbox unticked and not yet answered** → ask *"Do you want to save this project's settings?"* with **Yes** and **No**. *Yes* saves and ticks the checkbox, so later
   saves happen without asking. *No* does not save, and the app does not ask again until *Close project* or a fresh launch.

So the three states read: *ticked* — always save; *unticked* — ask at the next save point; *unticked and declined* — never ask, never save, for the rest of the session.

**Backing out.** *Cancel* on any of the prompts in §8.3, or on a save failure, backs out of the operation that triggered the save:

| Save point             | Cancel means                                                             |
|------------------------|--------------------------------------------------------------------------|
| **Start**              | The audit does not start; the window is left as it was.                  |
| **Closing the window** | The window stays open.                                                   |
| **Close project**      | The project stays open, unchanged.                                       |
| **Open project...**    | The picker is not shown; the current project stays open, unchanged.      |

### 8.3 Where the file is saved

- **Beside the report, per stage.** The file goes in the report's folder (the resolved *Location*). With a built location, Sample and Final are different folders, so each
  stage has its own settings file; the two stages may use different databases. The folder comes from the shared report location alone, so it is the same whichever tab is
  selected, and whether the location is complete is judged from that shared state, not from a tab's file name.
- **A project that has never been saved** (no file opened, nothing saved yet) → saved in the report's folder.
- **A project opened from a file, or saved before in this session** → saved back to that file, without asking, even when the report location is currently incomplete (the
  project's file is already known). If the report location is complete and its folder no longer matches the folder the file is in (for example, the stage was switched; the
  two folders are compared as full paths, ignoring case and a trailing separator), the app asks, in a question dialogue:
  *"The report folder has changed.", a blank line, "This project's settings were opened from:", the file's path, a blank line, "The report is now written to:", the report's
  folder, a blank line, "Where should the settings be saved?"* with **Report Folder**, **Original File** and **Cancel**. *Report Folder* saves to the report's folder, and
  from then on that is the project's file; *Original File* saves back where it was opened from; *Cancel* backs out (§8.2).
- **A built location is complete only with a valid Project folder.** At every save point, not only Start, a built report location counts as usable only when the Project
  folder is a single folder name (the rule of §6.1: not rooted, no `\` or `/`, not `.` or `..`, no characters that are not allowed in a file name). Otherwise the settings
  are never written through it: a never-saved project gets the *report location is incomplete* error below, and an opened project saves back to its own file without the
  folder-changed question.
- **A different settings file is already there.** When the target is a `FileAuditorSettings.json` that is not the project's own file (a never-saved project, or after
  *Report Folder*), the app warns *"A project settings file already exists in this folder ... Do you want to overwrite it?"* with **Overwrite**, **Skip Save** and
  **Cancel**. *Overwrite* saves over it and it becomes the project's file; *Skip Save* does not save this time and the operation goes ahead; *Cancel* backs out.
- **A save that cannot be done:** a never-saved project whose report location is incomplete (no Project folder, or a custom Location that is not a full path), a folder that
  does not exist, or a failed write (read-only, access denied, disconnected drive). The app never creates a folder just to save the settings. It shows an error dialogue
  naming the path and the reason, with **Skip Save** (the operation goes ahead without saving) and **Cancel** (backs out). On Start the pre-flight checks have already
  confirmed the report folder exists and is writable, so a failure there is rare.

## 9. Code shape

One project, `Source/FileAuditor/`, organised by folder (namespaces mirror folders). Only the `.csproj` and `app.manifest` sit at the project root; `Assets/` holds the
app icon, and `Assets/Win95Icons/` the Windows 95 icon set. Dialogue icons are taken from that set (`error32.gif`, `warning32.png`, `question32.png`), never drawn in
XAML. Each image the app uses is listed as an `AvaloniaResource` in the `.csproj`. The code folders are:

- `Startup/` — `App` and `Program`.
- `Views/` — `Main/` (`MainWindow`), `About/` (`AboutWindow`), `Dialogues/` (`MessageDialogueWindow`, `FileTypesDialogueWindow`), `Groups/` (a `UserControl` per group box:
  Database, File Index Table, Report, Audit, Unindexed Audit) and `Tabs/` (a `UserControl` per audit direction, the content of a tab: `MissingFilesTabView`,
  `UnindexedFilesTabView`).
- `ViewModels/` — one view model per view (CommunityToolkit.Mvvm), in the parallel folders `Main/`, `About/`, `Dialogues/` (including `DialogueIcon`, `DialogueChoice`,
  `FileTypesDialogueViewModel` and its row item `FileTypeEntryItem`), `Groups/` and `Tabs/` (`MissingFilesTabViewModel`, which owns the direction's *Report empty files* option,
  its `ReportViewModel` and its `AuditViewModel`; `UnindexedFilesTabViewModel`, which owns the folder list, the two file-type summaries, its `ReportViewModel` and its
  `UnindexedAuditViewModel`). `Groups/` also holds three types that are deliberately not a `*ViewModel` (they have no view of their own): `ReportLocationState`, the Report
  group's shared values, owned by the main window (each direction's `ReportViewModel` reads it and adds the direction's file name and, for the Unindexed direction, the grouping
  choice, so the tabs' Report groups cannot drift apart); `AuditStatusBase`, the base of both audit view models and what the status bar binds to (`StatusMessage`,
  `StatusPercent`); and `ScanFolderItem`, a row of the folder list. `Main/` also holds `AuditRun` and its two subclasses `MissingAuditRun` and `UnindexedAuditRun`, which carry one
  direction's run (progress object, phase text, refresh, result text) for `MainWindowViewModel`. `MainWindowViewModel` routes Start and View Report to the selected tab's
  direction and Stop and the status bar to the running one.
- `Services/` — `Settings/` (`SettingsStore`, which loads and saves one settings file at a path it is given and never creates a folder; `ProjectSettingsService`, which holds the
  open project's file path and baseline settings and runs the save rule of §8.2 with its prompts, returning a `SavePointResult` that says whether the triggering operation may
  proceed and whether the checkbox is to be ticked; it also remembers the three file-type warning answers of §6.5.1), `Sql/` (connection strings, SQL identifiers, query
  builder, which also builds the path-only, prefix-filtered query, and index table reader, which also reads the paths for the unindexed audit), `Checking/` (file checker and Win32
  error mapper), `Scanning/` (the unindexed direction's `FileTypeList`, `FileTypePattern` and `FileTypeFilter`, the 128-bit-hash `IndexPathSet` and the concurrent `FolderWalker`),
  `Auditing/` (audit runner and `UnindexedAuditRunner`, pre-flight checker, request and progress types including `UnindexedProgress`), `Reporting/` (the two report builders,
  `ReportFormat` with the formatting they share, writer and path resolver) and `Dialogues/` (dialogue service, which also offers a three-button choice returning a
  `DialogueChoice`, the *Open Project* file picker and the File Types dialogue). Return `ErrorOr<T>` for expected failures.
- `Models/` — `Settings/` (settings record, `ScanFolder` and the project and audit stage enums), `Auditing/` (audit query, `PathQuery`, index row, check outcome and result,
  `ScannedFile`, `UnverifiedFolder`, and `AuditDirection`, the enum of directions, `Missing` and `Unindexed`, whose member names the report file names are tied to) and
  `Reporting/` (audit report, `UnindexedReport` and result kind).
- `Common/` — `GlobalUsings.cs`; `Errors/` (the custom `Error` definitions: audit, query, report, settings, SQL); `Extensions/` (`ClassicWindow` helpers); `Links/`
  (`HelpLinks`, the user-guide address); `Serialisation/` (the source-generated settings JSON context).

Services are wired by hand in `App`; there is no dependency-injection container. `MainWindow.axaml` composes the shared group-box views (the Database and File Index Table groups
in a two-column grid) and the tab control (each tab's content is a `Tabs/` view) and holds the menu bar and status bar; it stays at 100 lines or fewer by pushing content into the
group-box and tab `UserControl`s. `ReportPathResolver.Resolve` takes the settings and an `AuditDirection`, so a direction's report name comes from one place.

**Errors.** Services return `ErrorOr<T>`: expected failures (bad input, SQL errors, an unwritable report, unreadable settings) are values with custom errors from
`Common/Errors/`, not exceptions. Exceptions are caught at the boundary where they occur (SQL, file system) and translated into errors or check outcomes. SQL access is
plain `Microsoft.Data.SqlClient`, with no ORM.

**Storage.** The app writes only the project settings file (§8) and the report (§7), both in the report's folder — never anything beside its executable, which runs from a
shared file server, and nothing under `%APPDATA%`.

## 10. Builder's calls (decided by Claude, not the user)

- Read-only preview of the built report path, with `<placeholders>`; Start disabled until the path is complete. *(shown in mock-up, accepted)*
- Hiding, rather than disabling, the Project folder row and both stage groups while Custom is ticked, on both tabs. *(shown disabled in the mock-up and accepted; the user
  changed it to hidden during milestone 4)*
- Pre-flight checks happen on Start, before the database is queried.
- Report timestamps in local time; sorting by path; UTF-8 without BOM; CRLF.
- Behaviour on Stop and on mid-run SQL failure (§6.4).
- `schema.table` support and bracket quoting.
- The set of Unverified reasons and their wording (§6.3).
- Status bar messages and keyboard shortcuts.
- Extra report header lines (Empty files, Project folder, stages).
- `Total files` stays the table's row count on Stop or failure, with a `Not checked:` line (§7).
- Project settings (milestone 2, §8): *Open project...* runs the save rule first and forgets the *No* answer, since the new file is a different project; a cancelled picker or
  an unreadable file leaves the current project untouched; "nothing has changed" compares the current inputs with the settings last loaded or saved; an opened project
  whose report location is incomplete saves back to its own file without asking; every close, including an operating-system logoff close, goes through the save rule.

- Direction tabs (milestone 3): the tab control appears now with one tab, so the window is complete and consistent; *Report empty files as missing* moves to an *Options*
  group on the Missing Files tab; the Report group's shared values live in one object that every tab's Report view reads, so mirroring is automatic; Start, View Report and
  the idle status follow the selected tab, Stop and the running status follow the running audit; the selected tab is not saved; the missing report's title is *File Auditor
  Missing Report*; the custom-name setting is `MissingCustomFileName`; *Help Topics* is F1 (the Windows standard), disabled while the article address is empty.

- Unindexed audit (milestone 4): *Group file types by* is a pair of radio buttons in the Unindexed tab's Report group, owned by that tab's `ReportViewModel`; ties between
  equally specific file types go to the fewest wildcard characters, then the first alphabetically; an entry is invalid when it is empty after the leading dot, has an empty part,
  or contains a path separator or a character Windows forbids in file names other than `*` and `?`; `.txt` and `.json` are not default file types, so the app's own reports and
  project files are never reported; hidden and system folders are skipped with their contents; a file under two input folders is reported once, under the deepest; the path column
  is trimmed before hashing; the SQL prefix filter is an optimisation with a fallback to every row (§6.5.2); hashing is an in-box algorithm (MD5), so no package was added;
  *Indexed* replaces *Checked* in the statistics (every file found is checked at once); links are never followed, only reported; Stop and the status bar follow the running audit
  rather than the selected tab; Stop while the index loads writes no report; equal custom file names on the two tabs are refused; up to 16 folder listings run at once and the
  statistics refresh four times a second; a duplicate folder is not added to the list and, when it reaches Start by a hand-edited file, is refused; what "matches every file" means
  is defined in §6.5.1.

Settled with the user after the builder check: roots checked once with a *Drive or share not found* group; unreachable locations short-circuit at the directory level
that failed, found by walking the path (§6.3); the close-mid-run prompt.

Settled with the user for milestone 4: the second direction itself; one or more input folders, each with *Include subfolders* ticked by default; matching on the full path ignoring
case; the editable extension whitelist and blacklist (the blacklist wins), the glob wildcards, the most-specific rule and the Start-time errors and warnings of §6.5.1; grouping
by listed type or found extension; no up-front count, so no *Remaining*; and, during the build, the layout: a compact Unindexed tab and a fixed tab height, the *Include
subfolders* column at the left of the folder list, File types and Exclude shown as read-only summaries edited in the File Types dialogue (with Reset only for File types), a custom
report location hiding the project folder and stage options, *Unverified* in the first statistics column, and the Database and File Index Table groups side by side (a trial the
user may reverse).

## 11. Left to the builder

Concurrency mechanism and degree; UI refresh interval; exact window dimensions; access-key letters; how dialogs are implemented on Classic.Avalonia; icon artwork.

Settled by the build (milestone 1), with no change in behaviour (milestone 4's additions are marked):

- **Access keys:** *Browse...* is `B`, *Start* is `A` (St **a**rt) and *Stop* is `O` (St **o**p); `R` and `M` were already taken by neighbouring labels. The *OK* and *Cancel*
  buttons in message dialogues carry no access keys, as with Win9x message boxes (Enter and Esc).
- **Default button:** *Start* is the window's default button, so Enter starts an audit.
- **Dialogues:** every message dialogue uses the custom `MessageDialogueWindow` (a `ClassicWindow` with icon, message and right-aligned buttons), because Classic.Avalonia's
  own message box cannot take custom button text. The one editing dialogue, `FileTypesDialogueWindow` (milestone 4), is a `ClassicWindow` of its own with the same conventions:
  modal, fixed-size, centred on its owner, buttons along the bottom right.
- **Number and time formatting in the report:** counts are formatted `N0` with the invariant culture, so reports diff identically on any machine; elapsed time is shown in
  total hours (at least two digits), not rolled over into days.
- **Window size and access keys (milestone 4):** the window is 640 × 761 pixels with a 575-pixel tab control. On the Unindexed tab, *File t_ypes:* is `Y` and *E_xclude:* is `X`;
  *Add...*, *Remove*, both *Edit...* buttons and the *Group file types by* radios carry none, because every letter is taken. In the File Types dialogue *New*, *Edit*, *Delete* and
  *Reset* are `N`, `E`, `D` and `R`, and *OK* and *Cancel* carry none.
- **Concurrency and memory (milestone 4):** up to 16 folder listings at once; index paths held as 128-bit MD5 hashes (§6.5.2); the statistics refresh every 250 ms.
- **Progress bar (milestone 4):** the Unindexed audit's moving block (§5.4a) is drawn by the view from a timer-driven column split, because the theme's marquee has a blank gap.
- **Full-path validation:** a path is accepted as absolute only if `Path.IsPathFullyQualified` says so, so `C:x` and `\x` (which would resolve against the application's
  current drive or folder) count as *Invalid characters in path*.

## 12. Code conventions

- **One type per file, no exceptions.** Class, record, struct, interface, enum and delegate each in its own file, named after the type, in a folder mirroring the
  namespace.
- **No code files at the project root** (§9). `App` and `Program` live under `Startup/`, not `Application/`, whose namespace would shadow Avalonia's `Application`.
- **Usings** are consolidated in `Common/GlobalUsings.cs`; a local `using` only for a namespace conflict or an alias. **File-scoped namespaces** everywhere.
- **British English** in comments, XML doc text, UI copy, commit messages, Markdown, and identifier wording where there is a choice (`Initialise`, `Behaviour`,
  `Colour`, `Dialogue`). A US spelling fixed by an external API (`System.IO`, `Color`, `Dialog`) stays.
- **XML doc comments on every public type and member** (`<summary>`, `<param>`, `<returns>`, `<remarks>`, `<exception>` as appropriate); `//` comments only for inline
  implementation notes. `<c>…</c>` and `<code>…</code>` carry no inside whitespace.
- **Spaced attribute brackets:** `[ STAThread ]`, `[ ObservableProperty ]`, `[ RelayCommand ]`.
- **Vertical spacing:** consecutive statements of different kinds (e.g. a declaration followed by an invocation) are separated by a blank line; like statements are
  grouped. Every `if` has braces.
- **UTC internally, local at the boundary.** Timestamps are captured and carried in UTC (`Utc` suffix on names) and converted to local time only for display and the
  report.
- **Source-generated MVVM:** `[ ObservableProperty ]` on `partial` properties, `[ RelayCommand ]` on private methods; no hand-rolled INPC. **Compiled bindings:** every
  XAML file declares `x:DataType`.
- **Designer constructors:** view models expose a parameterless constructor for the XAML designer only, built from `AppSettings.Default` and parameterless concrete
  services. No interfaces are introduced just for the designer.
- **View composition pair rule:** every public `*ViewModel` has a sibling `*View.axaml` under the parallel `Views/…` folder, and vice versa. Windows are the exception
  to the `*View` suffix: `MainWindow`, `AboutWindow`, `MessageDialogueWindow` and `FileTypesDialogueWindow` pair with `MainWindowViewModel`, `AboutWindowViewModel`,
  `MessageDialogueViewModel` and `FileTypesDialogueViewModel`. State a view model needs but that has no view of its own is not named `*ViewModel` (`ReportLocationState`,
  `AuditStatusBase`, `ScanFolderItem`, `FileTypeEntryItem`).
- **User-data writes** (settings, the report) use a temp file + `File.Move(overwrite: true)`, never an in-place write.
- **Never `File.Exists` / `Directory.Exists` inside the audit check** (§6.3): they turn "couldn't check" into "missing". Pre-flight checks of the report folder may
  use them.
