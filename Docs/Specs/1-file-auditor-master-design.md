# File Auditor — Master Design

**Date:** 2026-11-06 **Status:** Approved 2026-11-06; amended through milestone 4 (2026-11-08)

## 1. Intent

On almost every MediQuant project that has documents on disk, the team builds a SQL **file index table**: one row per document, holding the document's full path (and
usually an ID). **File Auditor** confirms that every file the index table points to actually exists on disk, and writes a plain-text **report** that is committed to the
project's version-control repository as a record of the audit.

Milestone 4 adds the reverse question as a second **direction**: *is every file on disk in the index table?* The **unindexed files audit** walks one or more folders and
reports every document whose full path is not in the index table's path column. The two directions share the window's inputs and the report location, each has its own tab (§5) and
each writes its own report (§7).

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

| Thing                               | Name                                                 |
|-------------------------------------|------------------------------------------------------|
| Product (title bar)                 | File Auditor                                         |
| Solution                            | `MediQuant.FileAuditor.slnx`                         |
| Project / root namespace / assembly | `FileAuditor` under `Source/FileAuditor/`            |
| Settings file (per project)         | `FileAuditorSettings.json`, beside the report        |
| Missing report file (built)         | `FileAuditorMissingReport_<Audit Stage>.txt`         |
| Missing report file (custom)        | `FileAuditorMissingReport.txt` (default, editable)   |
| Unindexed report file (built)       | `FileAuditorUnindexedReport_<Audit Stage>.txt`       |
| Unindexed report file (custom)      | `FileAuditorUnindexedReport.txt` (default, editable) |
| Version                             | 2.0.0                                                |
| Company / copyright                 | MediQuant / Copyright © 2026 MediQuant               |

The existing `Source/FileChecker/` template project and `MediQuant.FileChecker.slnx` are renamed accordingly. The Git repository and its folder keep their current name (renaming
the GitHub repository is the user's call, outside this design).

## 5. The main window

A single, **fixed-size** window: 640 × 668 pixels (outer), not resizable, no maximise button; minimise and close only. Classic.Avalonia supplies the Win9x chrome. The height is the
minimum the Missing Files tab needs with nothing clipped and no dead space, measured at the current display scale (milestone 4.1, re-measured in milestone 5 and in Epic 2's
milestone 1); it must be re-measured whenever a group on that tab changes. The tab control fills the window's star row (§5.2a), so the size is the same on both tabs and switching
tabs never moves the window's edges. Tab order runs top to bottom, left to right.

The window holds one audit **direction** per tab (a standard Win9x tab control): **Missing Files**, where every file the index table points to is on disk, and **Unindexed
Files**, where every file on disk is in the index table. The inputs both directions use (the Database and File Index Table groups, side by side, and the Project Settings group,
§5.3a) stay above the tabs; the Report group is shown on every tab and its values are shared (§5.3).

**Win9x conventions.** Every window looks and behaves like a native Windows 95/98 utility:

- **Group boxes** organise related inputs; labels sit to the left of their inputs and end with a colon.
- **Access keys** on every label, input, button and menu item (`_` prefix in XAML), unique within a window. The exceptions are *OK* and *Cancel* in dialogues and a few
  controls on the Unindexed Files tab for which no letter is free (§11).
- **Command buttons** are 75 × 23 minimum, laid out horizontally; the action comes first, the escape after it (*Start* then *Stop*, *OK* then *Cancel*, *Overwrite*
  then *Cancel*).
- **Ellipsis (`...`)** only on a command that needs more input before it completes (e.g. *Browse...*). A command that acts immediately — including opening a file in
  another program, like *View Report* — has none.
- **Menus:** *File* ends with *Exit*; *Help* ends with *About File Auditor*. Menu items mirror the buttons and enable and disable with them (except *Stop*, which stays enabled on
  either tab while an audit runs, §5.8).
- **Dialogues** are modal, fixed-size, with an icon (error, warning, question or information) and buttons along the bottom right. A path in dialogue text sits on its own line and
  wraps only after a `\` or `/` separator, never at a space or hyphen inside a folder name (`PathText.Breakable`).
- **The end of a run** draws attention to the window without taking the keyboard. A completed run shows the *finished* dialogue (§6.4, §6.5.4): the information icon, the audit's
  name, its final figures, the report's full path, and *View Report* (primary; Enter; access key `V`) and *OK* (Esc and the close box). A run that fails shows its error dialogue
  as before. Both play a Windows sound (information for the finished dialogue, error for a failure), and when the main window is not active they also flash the caption and the
  taskbar button until the window comes to the foreground (`WindowAttention`, §9). The window is never activated or brought forward. Both Win32 calls are best effort: a failure
  is ignored and the dialogue still shows. A run the user stopped (even one whose result stays *Completed*, §6.4), a pre-flight error and a close mid-run show and sound nothing
  new, unless the stopped run then fails (its error dialogue keeps the sound and the flash).
- **No modern affordances** that Win9x lacked: no rounded corners, no flyouts, no toast notifications, no accent colours, no hamburger menus.

Layout, top to bottom. The approved mock-ups are [`direction-tabs-layout.html`](../UserInterfaceDesign/Mockups/direction-tabs-layout.html), which shows the target layout with the
tab control (including milestone 4's *Unindexed Files* tab), [`main-window-layout.html`](../UserInterfaceDesign/Mockups/main-window-layout.html) and
[`report-section-states.html`](../UserInterfaceDesign/Mockups/report-section-states.html). They are a design reference only: the latter's state 1 predates the Interim default for
Audit Stage, and the last two predate the tab control, the per-direction report names, the *Options* group, the final Report-file rows, the access keys, the *Audit* group (named
*Progress* there), and the project settings (the *Save project settings with the report* checkbox and the *Open project...* and *Close project* menu items, milestone 2). All of
them predate milestone 4's final layout (the Database and File Index Table groups side by side, the fixed tab height, the compact Unindexed tab and the File Types dialogue) and
milestone 4.1's (the Project Settings group, disabled rather than hidden custom-location options, the right-hand *Include subfolders* column and the fixed height) and milestone 5's
(the three-column statistics and the 651-pixel height) and Epic 2's milestone 1 (the four-row statistics of §5.4 and the 668-pixel height), and they are kept as historical
references, not refreshed. The approved mock-up of the Audit group's statistics is [`row-accounting-stats.html`](../UserInterfaceDesign/Mockups/row-accounting-stats.html). The XAML
and this section are the contract.

```text
┌ File Auditor ───────────────────────────────────────────────── _ × ┐
│ File  Help                                                         │
│ ┌ Database ────────────────────┐ ┌ File Index Table ─────────────┐ │
│ │ Server:   [                ] │ │ Table:       [               ]│ │
│ │ Database: [                ] │ │ Path column: [               ]│ │
│ │                              │ │ ID column:   [        ] (Opt.)│ │
│ └──────────────────────────────┘ └───────────────────────────────┘ │
│ ┌ Project Settings ──────────────────────────────────────────────┐ │
│ │ [x] Save project settings                                      │ │
│ │ Saved as FileAuditorSettings.json in the same folder as the .. │ │
│ └────────────────────────────────────────────────────────────────┘ │
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
│ │ └────────────────────────────────────────────────────────────┘ │ │
│ │ ┌ Audit ─────────────────────────────────────────────────────┐ │ │
│ │ │ Total files: 10     Found:      4     Elapsed:   00:02:14  │ │ │
│ │ │ Distinct:    6      Missing:    1     Remaining: 00:00:00  │ │ │
│ │ │ Duplicates:  2      Unverified: 1 (1) Files/second: 9      │ │ │
│ │ │ Checked:     6      Skipped:    1                          │ │ │
│ │ │ [██████████████████████████████████████████████]           │ │ │
│ │ │ [View Report]                      [Start]  [Stop]         │ │ │
│ │ └────────────────────────────────────────────────────────────┘ │ │
│ └────────────────────────────────────────────────────────────────┘ │
│ Completed                                                │ 100%    │
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
│ ┌ Project Settings ──────────────────────────────────────────────┐ │
│ │ [x] Save project settings                                      │ │
│ │ Saved as FileAuditorSettings.json in the same folder as the .. │ │
│ └────────────────────────────────────────────────────────────────┘ │
│  Missing Files  ┌───────────────┐                                  │
│ ┌──────────────┘Unindexed Files└─────────────────────────────────┐ │
│ │ ┌ Folders to Check ──────────────────────────────────────────┐ │ │
│ │ │ Folder                              Include subfolders     │ │ │
│ │ │ ┌──────────────────────────────────────┐ ┌──────────┐      │ │ │
│ │ │ │ U:\Projects\D12345\Images        [x] │ │ Add...   │      │ │ │
│ │ │ │ U:\Projects\D12345\Scans         [ ] │ │ Remove   │      │ │ │
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
│ │ └────────────────────────────────────────────────────────────┘ │ │
│ │ ┌ Audit ─────────────────────────────────────────────────────┐ │ │
│ │ │ Total files: 500   Indexed:    480  Elapsed:      00:00:50 │ │ │
│ │ │ Excluded:    31    Unindexed:  20   Files/second: 10 (11)  │ │ │
│ │ │ Index paths: 484   Unverified: 3                           │ │ │
│ │ │ [            ██████████                        ]           │ │ │
│ │ │ [View Report]                      [Start]  [Stop]         │ │ │
│ │ └────────────────────────────────────────────────────────────┘ │ │
│ └────────────────────────────────────────────────────────────────┘ │
│ Checking files...                                        │         │
└────────────────────────────────────────────────────────────────────┘
```

With *Use a custom report location and file name* ticked, the Report group keeps its Project folder row and both stage groups visible but disabled on either tab (§5.3).
The Unindexed Files tab's content scrolls vertically when it is taller than the tab (§5.2b).

### 5.1 Database group

- **Server** and **Database**: text inputs, both required.
- The connection string is always built as `Data Source=<Server>; Initial Catalog=<Database>; Integrated Security=True; TrustServerCertificate=True` (via
  `SqlConnectionStringBuilder`). There are no user/password fields anywhere — not in the UI, not in settings.

### 5.2 File Index Table group

- **Table**: required. Accepts `table` or `schema.table`.
- **Path column**: required. The column must be `nvarchar` or `varchar` (§6.2; a `varchar` column is read as it is, and converted only inside the Unindexed audit's folder filter,
  §6.5.2); on Start any other type is refused for both directions.
- **ID column**: optional, with a muted "(Optional)" note beside it. It is shared by both directions, but only the missing-files direction uses it: an unindexed file has no
  row, so no ID. On the Unindexed Files tab it stays editable and is still saved with the project.

The Database and File Index Table groups sit **side by side**, Database on the left and File Index Table on the right, each with its fields stacked vertically, and stay above
the tab control, with the Project Settings group (§5.3a) full-width beneath them. They are used by every direction. (The side-by-side placement is a trial the user may
reverse.)

### 5.2a Tab control and Options group

- A standard Win9x **tab control** sits below those two groups, one tab per audit direction: **Missing Files** and **Unindexed Files**. It fills the window's remaining (star) row,
  so its height is whatever the fixed 668-pixel window leaves (§5), which is the Missing Files tab's content height; the window does not change size when the tab
  changes. The Unindexed Files tab is taller, so its content sits in a scroll viewer that scrolls vertically when needed and never horizontally. The selected tab is not
  saved; the window opens on the first tab. Switching tabs is allowed while an audit runs.
- The Missing Files tab holds, top to bottom: an **Options** group, the **Report** group (§5.3) and the **Audit** group (§5.4).
- **Options** holds **Report empty (zero-byte) files as missing**: checkbox, checked by default. When ticked, an existing file of length 0 counts as *missing (empty)*. It
  applies to the missing-files direction only, which is why it is not in the shared File Index Table group.

### 5.2b The Unindexed Files tab

The tab holds, top to bottom, the **Folders to Check** group, the **Report** group (§5.3) and the **Unindexed Audit** group (§5.4a).

**Folders to Check** holds:

- A list of folders, one per row. Each row has the folder's path at the left (cut with an ellipsis when long, the whole path in a tooltip) and an **Include
  subfolders** checkbox in a fixed 110-pixel column at the row's right, ticked for a newly added folder; the header row above the list names the two columns. The path is
  truncated, never the checkbox, so the checkbox is always visible. The list never scrolls horizontally; its vertical scroll bar is always shown, and the header keeps a
  16-pixel right margin (the bar's width) so the header and rows line up. The list is about as tall as the two buttons beside it.
- **Add...** and **Remove**, stacked to the right of the list. *Add...* opens a folder picker (directories only) and appends the chosen folder, selecting it; a folder already
  listed (ignoring case and a trailing separator) is not added a second time. *Remove* removes the selected row and is disabled while no row is selected.
- **File types:** a read-only, single-line summary of the extension whitelist (sorted, separated by spaces), with an **Edit...** button. A built-in list of 15 common document and
  image formats is the default (`.bmp .doc .docm .docx .dotm .dotx .gif .html .jpeg .jpg .pdf .png .rtf .tiff .txt`, the user's list, `FileTypeList.Defaults`). It includes
  `.txt`, so the app's own reports are checked like any other file when they sit in a scanned folder; `.json` is not on it, so project files are never reported.
- **Exclude:** the extension blacklist, in the same form, empty by default, with its own **Edit...** button. A file that matches an Exclude entry is never checked, even when
  File types matches it: the blacklist wins.

The labels *File t_ypes:* and *E_xclude:* carry the access keys Y and X; *Add...*, *Remove* and the two *Edit...* buttons carry none, because every letter is taken on this
window (§11).

**File Types dialogue.** Each *Edit...* opens a modal dialogue (`FileTypesDialogueWindow`, titled *Edit File Types* or *Edit Exclude List*; 360 × 300, fixed-size, centred on the
main window, no maximise) that lists the entries one per row, as Windows' environment-variable editor does, with **New** (adds an empty row and focuses it), **Edit** (focuses the
selected row), **Delete** (removes the selected row; disabled while none is selected), **Clear** (`C_lear`, access key `L`; removes every row; disabled while the list is empty;
placed after *Delete* and before *Reset*), **Reset** (restores the built-in list; shown for File types only), **OK** and **Cancel**. **OK** validates every entry (the syntax of
§6.5.1) and, when one is invalid, shows the error and keeps the dialogue open; otherwise the summary shows the normalised entries (lower case, one leading dot, duplicates
dropped, sorted). **Cancel** discards every change, including a Reset. The Folders to Check group and its buttons are locked while an audit runs (§5.8).

### 5.3 Report group

Each tab shows a complete Report group. Its values are **one set shared by every tab**: change one on a tab and every other tab shows the change. The only per-tab item is
the **File name**, which belongs to the tab's direction (names are in §4), and, on the Unindexed Files tab only, the **Group file types by** option below it. The *Save project
settings* checkbox lives in the Project Settings group (§5.3a), outside this group, and is shared too.

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
    - *Ticked (custom):* the Project folder row and both stage groups stay **visible but disabled** on both tabs, and are enabled again when the box is unticked (the user's
      decision, milestone 4.1, reversing milestone 4's hiding). Location becomes an editable directory path, empty by default, with **Browse...** opening a *folder* picker
      (directories only). File name becomes editable, defaulting to the tab's custom default, `FileAuditorMissingReport.txt` on the Missing Files tab and
      `FileAuditorUnindexedReport.txt` on the Unindexed Files tab; each tab keeps its own custom file name. The two names must differ (§6.1).
- **Group file types by** (Unindexed Files tab only): two radio buttons, *Listed type* (default) and *Found extension*, on the group's last row, below *File name*. It chooses how
  the unindexed report groups files (§7.1) and is saved in the project file (§8). It belongs to the direction, so the Missing Files tab does not show it.

### 5.3a Project Settings group

A group box headed **Project Settings**, full width, between the Database / File Index Table row and the tab control (`ProjectSettingsView`, owned by the main window, so it is
the same on both tabs). It holds the *Save project settings* checkbox (access key `G`) and, below it, a wrapping status message. The checkbox's behaviour is
unchanged from milestone 2:

- It is **ticked by default** — on launch, after *Open project...* and after *Close project* — and its state is not stored in the settings file; it is a per-session preference.
  It is disabled while an audit is running (the whole group is). Ticking it needs no confirmation. **Unticking** first shows a warning dialogue (*"If you untick this box, this
  project's settings will no longer be saved automatically. You will be asked whether to save them instead. Do you want to untick it?"*) with **Untick** and **Cancel**; *Cancel*
  leaves the box ticked. Once *Untick* has been chosen the warning is not shown again, and later unticking goes straight through, until *Close project* or a fresh
  launch. What the box does is set out in §8.
- The **status message** is exactly one of:
    - ticked, and no settings file is open or the open file is in the report folder: `Saved as FileAuditorSettings.json in the same folder as the audit reports.`
    - ticked, and the open settings file is in a different folder from the report folder: `Saved to the opened settings file, in a different folder from the audit reports.`
    - unticked: `This project's settings are not being saved.`
- The folders are compared with the save rule's normalisation (`ProjectSettingsService.IsInFolder`, which uses the save rule's `SameFolder`, §8.2). An incomplete report location
  counts as "no mismatch" when no file is open, and as "different" when a file is open. The message is recomputed whenever the checkbox, the report location, the open project
  (*Open project...*, *Close project*) or the project file's path (a save that moves it) changes.

### 5.4 Audit group (Missing Files tab)

The Audit group sits at the bottom of its tab, below the Report group; each direction's tab has its own, so a tab's statistics are those of its own last run.

- Read-only statistics in three columns of four rows (the approved mock-up is [`row-accounting-stats.html`](../UserInterfaceDesign/Mockups/row-accounting-stats.html)):
  **Total files**, **Distinct**, **Duplicates** and **Checked** on the left; **Found**, **Missing**, **Unverified** and **Skipped** in the centre; **Elapsed**, **Remaining** and
  **Files/second** on the right (its fourth row is empty). Counts use thousands separators (`N0`, current culture); times are `hh:mm:ss`. Blank before the first run. The grid's
  columns are 74, 128, 68, 118, 82 and `*` pixels wide, so `9,999,999 (9,999,999)` fits in *Unverified*.
    - The figures are those of §6.2's row kinds, counted as the rows stream in (no figure is known up front but the table's row count): T is the table's rows, D the different real
      paths seen so far, E the empty-path rows read so far, S the skip-marker rows read so far, Dup the duplicate rows read so far and U the checked *Unverified* outcomes (E is not
      part of U). **Total files** is T, the table's row count, known the moment counting ends (an instant `COUNT_BIG(*)`) and fixed for the run. **Distinct** is D, counted when a
      new key enters the key set, so it never goes down; **Checked** trails it by the distinct rows queued or being checked (up to the channel's 10,000), and on screen *Distinct*
      is never shown below *Checked*, so one refresh never contradicts itself. **Duplicates** is Dup: each row whose path was already seen, one per row, so a path listed three
      times is one *Distinct* and two *Duplicates*. **Unverified** is `U`, or `U (E)` when E is above zero: the parenthesised rows are the empty-path rows, which are *Unverified*
      but never reach the checks (§6.3). **Skipped** is S. *Duplicates*, *Skipped* and the parenthesised E count rows and grow while the rows are read.
    - **Checked** is Found + Missing + U (each counter read once per refresh, so the three never disagree) and never includes skipped, empty-path or duplicate rows. **Found**
      is the files that exist (and are not empty, when that option is on). When the run completes, Found + Missing + U = Checked = D and D + Dup + E + S = T. The table is assumed
      not to change during a run (§6.2), so *Total files* is never adjusted to match what was read.
    - **Files/second** is a whole number (`N0`, culture-aware thousands separators, rounded). Its first figure counts checked files only: the whole run's average, files checked
      divided by **Elapsed** (the span the window shows, counting included); it stays blank until at least one full second has elapsed. Once the 5-second warm-up of checking has
      passed, and only while the run is under way, the **recent rate** follows in parentheses, as in `54,112 (60,001)`. Its recent rate is its own (a `RecentRate` of the screen's
      files-checked figure), not the one behind **Remaining**: files *checked* per second of *checking* time, which excludes the counting. The two figures therefore have
      different bases, on purpose: the first answers "how fast has this run been", the second "how fast is it going now". A burst of duplicates moves *Remaining* but not
      *Files/second*. After the run, the whole-run figure stands alone again. Parentheses rather than a middot: a Win9x dialogue puts a secondary figure in parentheses, and a
      middot was no Win9x convention.
    - **Remaining** is `Calculating...` while the rows are counted and for the first 5 seconds of checking, then the time left at the **recent rate**, an exponentially weighted
      rate over roughly the last 30 seconds (a 30-second time constant) of checking time, counting rows *handled* (a skipped, empty or duplicate row is handled when it is read, a
      distinct row when its check ends), over the T − handled rows still to do. The rate is seeded from the second half of the warm-up (from 2.5 s), so the slow start-up ramp does
      not drag it down. Between refreshes the display counts down one second per second and eases toward each new estimate with a 3-second time constant, so it neither holds
      still nor jumps. When the handled count has been unchanged for about 1 second the display holds where it is (no countdown, no easing) instead of running on to zero, while
      the rate keeps decaying, so a long stall lets Remaining rise when work resumes. The shown value is clamped to 36,000,000 seconds (10,000 hours) so no stall can overflow the
      format. It reads `00:00:00` when the run ends.
- A progress bar (the percentage of the table's rows T handled, by the same count as *Remaining*, so it stays accurate rather than pessimistic while duplicates are read). With
  T = 0 the run still completes and the bar reads 100%.
- A row of command buttons: **View Report** left-justified; **Start** then **Stop** right-justified. There is no Close button.

### 5.4a Unindexed Audit group

The Unindexed Files tab has its own group (headed *Audit*, `UnindexedAuditView`) at the bottom of the tab. It holds:

- Read-only statistics in three columns and three rows: **Total files**, **Excluded** and **Index paths** on the left; **Indexed**, **Unindexed** and **Unverified** in the centre;
  **Elapsed** and **Files/second** on the right (its third row is empty). There is **no Remaining**, neither label nor value, because the walk has no up-front count and so the
  total is not known until it ends. *Total files* is the files found so far, each of which is checked the moment it is found, so Total files = Indexed + Unindexed; *Unverified*
  counts folders that could not be listed, links not followed and entries that could not be classified (§6.5.3), and is never part of Total files. *Excluded* is the files the
  walk met and skipped because no *File types* entry matches them or an *Exclude* entry does (§6.5.3); it is never part of Total files, and counts up live during the walk.
  *Index paths* is the number of distinct paths loaded from the index to compare against (§6.5.2); it counts up live during `Loading the index...` and is final once the walk
  begins. Counts, times and the rate are formatted as in §5.4; Files/second is Total files over Elapsed and stays blank until a full second has elapsed, and once the run has
  scanned for 5 seconds it shows the recent rate in parentheses as well (§5.4). The Unindexed recent rate is files found per second of **scan** time, timed by a scan clock on
  `UnindexedProgress` that starts when the folder walk begins, so loading the index (which can take minutes) does not use up the warm-up. The whole-run figure stays on wall time.
  All are blank before the first run.
- A progress bar. While a run is under way it shows a **moving block** a fifth of the track wide that crosses a sunken track from left to right and starts again, moved by the 250
  ms refresh timer, instead of a fill that would read as a percentage of an unknown total. When the run ends the bar is full (and the status bar's percentage pane reads `100%`);
  before the first run it is empty. Classic.Avalonia's own indeterminate marquee was not used: it is blank for about 2.4 seconds of every 5-second cycle and its animation cannot
  be restyled.
- The same row of command buttons as §5.4: **View Report** left-justified, **Start** then **Stop** right-justified.

### 5.5 Status bar

One status bar for the window, with two sunken panes: a message pane that fills the width (`Ready`, `Counting files...`, `Loading the index...`, `Checking files...`, `Writing
report...`, `Completed`, `Stopped`, `Failed`) and a fixed-width percentage pane at the right edge. **While an audit runs**, the bar shows that run's phase and percentage whichever
tab is selected, so switching tabs never hides a run. **Otherwise** it shows the selected tab's own state: with no run on that tab, the message reads `Ready` only while that tab's
**Start** is enabled, and `Enter the required details` while it is not. Once a run has ended, its result (`Completed`, `Stopped` or `Failed`) stays on that tab until its next run
starts. Each tab keeps its own last result.

### 5.6 Menu bar

- **File**: **Open project...** (access key P), **Close project** (access key C), separator, **Start** (F5), **Stop** (Esc), **View Report** (Ctrl+R), separator, **Exit** (Alt+F4).
  Start and View Report enable and disable with the selected tab's buttons; Stop is enabled whenever an audit runs, even on a tab whose own Stop button is disabled (§5.8). *Open
  project...* and *Close project* have no button; they are disabled while an audit is running and while Start, *Open project...* or *Close project* is in progress (§5.8). **Start**
  and **View Report** (and F5, Ctrl+R) act on the **selected tab's** audit and follow that tab's buttons. **Stop** (and Esc) acts on the **running** audit, whichever tab is
  selected.
- **Help**: **Help Topics** (access key H, shortcut F1), separator, **About File Auditor**. *Help Topics* opens the File Auditor user guide, a wiki article, in the default
  browser by shell execute. The article's address is one hard-coded constant, `HelpLinks.UserGuideUrl`, which is an empty string until the article exists; while it is
  empty, the item and F1 are disabled.

### 5.7 About box

A small modal, fixed-size Win9x About dialog: the app icon, **File Auditor**, **Version 2.0.0**, **Copyright © 2026 MediQuant**, and an **OK** button.

### 5.8 Command states

| Command         | Enabled when                                                                                                                              |
|-----------------|-------------------------------------------------------------------------------------------------------------------------------------------|
| Start           | Not running and not busy, and Server, Database, Table, Path column are filled, and the selected tab's report path is complete (see below) |
| Stop            | Menu item: running (any tab). Each tab's button: running, and that tab is the running audit's                                             |
| View Report     | Not running, and a report file exists at the selected tab's current resolved path                                                         |
| Open project... | Not running and not busy                                                                                                                  |
| Close project   | Not running and not busy                                                                                                                  |
| Help Topics     | The user-guide address (`HelpLinks.UserGuideUrl`) is not empty                                                                            |

The report path is complete when, *built*: Project folder is filled (an Audit Stage is always selected); *custom*: Location and the tab's File name are filled. All inputs,
including the *Save project settings* checkbox, are disabled while a run is in progress. Start and View Report follow the selected tab. Each tab's Stop *button* is
enabled only on the running audit's own tab (so while the Missing audit runs, the Unindexed tab's Stop is disabled, and the reverse); the *Stop* menu item and Esc stop the
running audit from either tab.

**One run at a time.** While either direction runs, both tabs' Start buttons are disabled and every input is locked: the shared groups, the Folders to Check group (the list, its
checkboxes, *Add...*, *Remove* and both *Edit...* buttons) and the Report group. Only Stop, the tab selection and the Help menu stay live.

**Busy.** Start, *Open project...* and *Close project* are *busy* from the moment the command begins until it ends, including while its pre-flight checks, save prompts, file
picker or error dialogues are open (Start's busy period runs on into the audit itself). They never overlap: while one is busy the other two are disabled, and so is Start. The
inputs, including the checkbox, are disabled as well, so nothing can be edited and then wiped by a project being opened. Closing the window while one of them is busy but no
audit is running is refused; the window can be closed once the command's prompt has been answered or its work has finished. Closing during an audit keeps its own
stop-and-exit question (§6.4).

**View Report** opens the report in the user's default program for `.txt` (shell execute). No ellipsis: it needs no further input. **Browse...** keeps its ellipsis.

## 6. Running an audit

### 6.1 Pre-flight checks (on Start, before touching the database, but for one type lookup)

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
4. **varchar path column warning** (both tabs), the last check, so a Start that would fail for another reason never reaches the database. Unless the answer is already remembered
   for the current column, the path column's type is read with the lookup the runs use (`IndexTableReader.ReadPathColumnTypeAsync` on the quoted names, then
   `PathColumnType.Check`); this lookup is the one place pre-flight touches the database. When the column is `varchar`, a **warning dialogue** (*Continue*, access key `C`, and
   *Cancel*) says: `The path column '<column>' is <type>.` / `The File Auditor prefers an nvarchar column. A varchar column may have changed some characters in its paths when they
   were stored. See "varchar Path Columns" in the user guide.` / a blank line / `Do you want to continue?`. `<column>` is the name as typed (trimmed) and `<type>` is
   `varchar(<n>)` or `varchar(max)` (`PathColumnType.Format`, as in the refusal of §6.2). *Cancel* stops the Start: no run, no report, nothing saved. For an `nvarchar` column, a
   refused type, a missing column or table, any SQL error, any exception except out-of-memory, or a lookup that takes longer than 30 seconds (`VarcharLookupTimeout`; the lookup
   is then abandoned), nothing is shown and the Start goes on. Whatever the lookup returned (a type, not found, or a SQL error) is handed to the run (`PreflightResult`,
   `AuditRequest.PathColumnType`), which uses it instead of connecting again and reports a refusal or error exactly as before; so an unreachable server is waited for once. Only
   an abandoned lookup, an exception, or a remembered answer (no lookup) leaves the run to read the type itself. *Continue* is remembered for the column (server, database,
   table and path column, each trimmed and compared ignoring case) until *Close project*, *Open project...* or a fresh launch; a different column asks again, and while it is
   remembered no lookup is made. It is never saved in the project file.
5. Run the save rule (§8.2) for the project's settings. It may ask whether or where to save; *Cancel* on any of its prompts stops the run from starting.

### 6.2 Query

Built from the inputs; each identifier part is bracket-quoted with `]` escaped as `]]` (never string-concatenated raw). Two plain statements are used (`QueryBuilder.Build`, which
returns an `AuditQuery` holding both), the two statements the 1.0.0 audit ran (`<path>` and `<id>` stand for the quoted columns):

```sql
-- 1. The table's rows: Total files.
SELECT COUNT_BIG(*) FROM [schema].[table];
-- 2. Every row, in table order (ID part omitted when no ID column).
SELECT <path> AS [Path], <id> AS [Id] FROM [schema].[table];
```

SQL does nothing else: no conversion, no `WHERE`, no `DISTINCT` and no parameters, so it has nothing to classify. The count returns at once and the select streams its first row at
once, so checking starts as soon as counting ends, however long the table (milestone 6 measured the alternatives that classified or de-duplicated in SQL, §10). The path is read as
it is, `NULL` as empty (`IndexTableReader.CountRowsAsync` and `ReadAsync`).

**The row kinds.** Every row of the table is exactly one of:

| Kind              | Rule                                                                                                  | Symbol |
|-------------------|-------------------------------------------------------------------------------------------------------|--------|
| Skip marker       | A skip marker by the rule of §6.3                                                                     | S      |
| Empty path        | Not a skip marker, and `NULL` or made only of white-space characters (`char.IsWhiteSpace`)            | E      |
| First of its kind | Neither of the above, and the first row of its distinct path (or path and ID pair, with an ID column) | D      |
| Duplicate         | Neither of the above, and not the first row of its distinct path (or pair)                            | Dup    |

T (the table's rows) = D + Dup + E + S. Of the D distinct real paths, each lands in Found, Missing or Unverified (U, the checked *Unverified* outcomes; E is not part of U), so on a
completed run **Found + Missing + U = Checked = D** and **D + Dup + E + S = T**. On a stopped run **Not checked** is T − (Checked + Dup + E + S), the rows never read plus the
distinct rows read but not checked, so **Checked + Dup + E + S + Not checked = T**; D is then the distinct paths found before the stop, and Not checked is never below zero.

**The classification is done in the app, row by row, in the order of the table above** (`AuditRunner`; the reader is the only thread that classifies, so the key set needs no lock).
`SkipMarker.IsSkipped` first: a skip marker is *Skipped*, not checked and not de-duplicated. Then `string.IsNullOrWhiteSpace`: an empty path is *Unverified — Path is empty in the
index table*, not checked and not de-duplicated, and with an ID column its ID (`NULL` as the empty string) is kept for the report (§7). Otherwise the row's key is looked up in the
set of keys seen so far (`PathKeySet`): already there is a *Duplicate*, not checked; new is *Distinct*, and the row goes to the checks. Skipped, empty and duplicate rows are never
checked. Each counter moves the moment its row is read, which is what makes every figure on the screen live (§5.4).

**The de-duplication key (`PathKeySet`).** The path with trailing spaces (U+0020 only) removed and upper-cased in the invariant culture (`ToUpperInvariant`), plus, with an ID
column, the ID as read. The path's length and an ID flag are hashed ahead of the text, so the pair is unambiguous (`("a", "bc")` and `("ab", "c")` differ). The text is hashed to
128 bits with MD5 (speed and even spread, not security; a collision is negligible at 128 bits) and only the hash is kept, about 48 MB per million keys. Case is ignored because
Windows paths are case-insensitive, and so was 2.0.0's `DISTINCT` under the usual case-insensitive collations; trailing spaces are ignored because SQL Server's comparison ignores
them and Windows strips them from a path. Nothing else is trimmed: a path with a leading space, or a trailing tab or other white space, is its own key and is checked (the checker
reports it, for example as *Invalid characters in path*), so no row with a problem can hide behind a clean duplicate. Unlike a collation, the invariant upper-casing does not treat
`ß` as `ss`, so `Straße` and `Strasse` are two paths. `IndexPathSet` (§6.5.2) trims both ends, so it is not reused. With an ID column the unit is the path and ID pair, so the ID
column is expected to be **unique to its path** (one ID per file); the app does not check that, and an ID that varies across the rows of one path makes that path *Distinct* once
per ID.

**The path column must be `nvarchar` or `varchar`.** Before counting (or, for the unindexed audit, before loading the index) the app reads the path column's type with
`sp_describe_first_result_set` on a `SELECT` of the column alone, `SELECT TOP (0) [fa_t].<column> FROM <table> AS [fa_t];`, built from the same bracket-quoted names the audits use
(so a schema, spaces or `]` in a name are found). The type is therefore read as the audits see it: permissions, synonyms (also to a table in another database) and views behave as
they do for the audits' own statements. It is the column's *system* type (`system_type_name`; the user type name only for a user-defined CLR type, which is refused with its name)
that must be `nvarchar` or `varchar`, of any length including `max`; so an alias type built on either passes, and a view whose column is either passes. What the audits then work
on:

| Path column (system type)                                    | The Missing Files audit | The Unindexed Files audit                                         |
|--------------------------------------------------------------|-------------------------|-------------------------------------------------------------------|
| `nvarchar(n)`, `nvarchar(max)`                               | the column, as it is    | the column, as it is                                              |
| `varchar(n)`, `varchar(max)`                                 | the column, as it is    | the column, as it is; the folder filter compares `nvarchar(4000)` |
| anything else (`char`, `nchar`, `text`, `ntext`, numbers, …) | refused                 | refused                                                           |

`PathColumnType.Check` yields `null` for `nvarchar` and `nvarchar(4000)` for any `varchar`, whatever its length; only the Unindexed folder filter uses that target (§6.5.2), and the
Missing Files audit converts nothing. A `varchar` column stores only the characters of its collation's code page: anything else was changed when the row was written, to a near
equivalent where SQL Server has one (`Ł` → `L`) and to `?` otherwise. Nothing can undo that, so such a path may name a different file, which may exist, or none. A path holding `?`
is reported by the Missing Files audit under *Unverified — Invalid characters in path* (§6.3), and never matches a file in the Unindexed Files audit. The rule is the same for both
audits. The ID column is unrestricted and is still read as text. Two errors, both shown in the ordinary error dialogue, no report written, no row read (`PathColumnType.Check`):

- `The path column '<column>' is <type>. The File Auditor expects an nvarchar or varchar column.` (`Sql.PathColumnUnsupported`). `<type>` is the system type in lower case, with
  `(<n>)` for `char`, `binary` and `varbinary` (`n` is `max_length`) and for `nchar` (`n` is `max_length / 2`), `(max)` when `max_length` is -1, and the bare name for any other
  type (`int`).
- `The path column '<column>' was not found in the table '<table>'.` (`Sql.PathColumnNotFound`), with `<column>` and `<table>` as typed. It is given when the first error SQL
  Server raises is 207 (invalid column name) or 208 (invalid object name); a table that cannot be found gives the same error.

The lookup runs under each audit's existing first status (`Counting files...`, `Loading the index...`), with no status text of its own, and before the statements are built
(they are built from its result). Stop during it behaves as Stop while counting or loading does (§6.4, §6.5.4): nothing is checked, so no report. Any other first error is
`Sql.QueryFailed` with that error's message, as for any other statement: a missing permission on the table shows SQL Server's own error 229, not a "not found" message.
(`sp_describe_first_result_set` raises the real error first and then a wrapper, 11501 or 11529, which is ignored.) A wrong table or path column is thus covered by the two errors
above; only a bad ID column, which the lookup does not touch, fails later as a plain SQL error.

**The table is assumed not to change during a run.** The count is taken once, before the rows are read; the app does not snapshot the table or reconcile the count with the rows it
later streams. Changing the index table mid-audit is the user's responsibility, and the sums above hold only for a table that stays as it was counted; *Not checked* is the one
figure that is clamped (it is never shown below zero).

The count runs first (status `Counting files...`) so *Total files*, the progress and the remaining time can be shown. Command timeouts are long enough for large tables. The
unindexed direction reads only the path column, in the query of §6.5.2, and is unchanged by row accounting.

### 6.3 Checking a file

Each distinct real path is checked concurrently with bounded parallelism (builder chooses the mechanism and degree; the old app's unbounded `ThreadPool` + 1 million queue cap is
not a requirement). Empty-path rows, skip markers and duplicates never reach the checks: the audit classifies and removes them as it reads (§6.2), so the checker only ever sees
real paths. It keeps its own skip and empty tests as a safety net that should never fire: since milestone 6 the reader and the checker run the same tests on the same string, so a
stray skip or empty outcome cannot occur. Each file lands in exactly one outcome:

| Outcome                                       | When                                                                                |
|-----------------------------------------------|-------------------------------------------------------------------------------------|
| Skipped                                       | Missing audit only: the path value is a skip marker (below); counted, never checked |
| Found                                         | The file exists (and, if the empty option is on, its length > 0)                    |
| Missing — Does not exist                      | The path's root exists, but the file or a directory below the root does not         |
| Missing — Empty                               | Empty option on, and the file exists with length 0                                  |
| Unverified — Drive or share not found         | The path's root (drive letter, or `\\server\share`) does not exist for this user    |
| Unverified — Permission denied                | Access to the file or its location is denied                                        |
| Unverified — Network location unreachable     | The server or share in a UNC path cannot be reached                                 |
| Unverified — Path too long                    | The path exceeds what the system can handle                                         |
| Unverified — Link not followed                | Unindexed audit only: a link (junction or symlink) was skipped, not checked         |
| Unverified — Invalid characters in path       | The path is malformed or contains illegal characters                                |
| Unverified — Path is empty in the index table | The path is `NULL`, empty or whitespace; counted, listed by ID if set, not checked  |
| Unverified — Other errors                     | Any other I/O failure; the reason and system message are recorded per file          |

**The skip rule (missing-files audit only).** Index rows can carry a marker instead of a path, written by hand or by the tool that builds the index table. Before any other check
(before the empty-path test and before the path is parsed), the value is trimmed, and it is a **skip marker** when it equals `SKIP` or `SKIPPED`, or starts with `SKIP:` or
`SKIPPED:`, comparing letters in either case (`StringComparison.OrdinalIgnoreCase`). Nothing else is one: not `SKIP preflight`, not `SKIPPEDX`, not `SKIPPED :x`, and not a path
that merely contains the word (`C:\SKIPPED\a.txt` is checked as a file). A `NULL` or white-space-only value is not a marker; it is an empty path (§6.2), *Path is empty in the index
table*. A skipped row is never checked on disk and never listed in the report; it is counted (*Skipped*) when the row is read, and is handled at once for the progress bar and
*Remaining* (§6.4). The Unindexed audit is unaffected: it reads the index only to learn which paths exist, and a marker there matches no file.

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

- The UI refreshes stats a few times per second. Everything but *Total files* is live (§5.4): the rows are read and classified while the files are checked, so *Distinct*,
  *Duplicates*, *Skipped* and the parenthesised empty-path count grow as the stream advances. The progress bar and Remaining run over the table's rows T: a skipped, empty or
  duplicate row is handled the moment it is read, a distinct row when its check ends. They are estimated from the recent rate of rows handled, whereas Files/second keeps its own
  recent rate of distinct files checked (§5.4), so a burst of duplicates moves *Remaining* but not *Files/second*. With T = 0 (a table with no rows) the run completes, the bar
  reads 100% and the report is written.
- **Empty-path IDs.** With an ID column, the ID of each empty-path row is collected as the row is read and listed in the group *[Path is empty in the index table]* (§7). There is
  no separate read and no status of its own. A stopped or part-way failed run lists the IDs read so far, which are the rows its counts reflect.
- **Stop**: stops reading rows and checking files, then writes the report with `Result: Stopped by user`. Counts reflect what was read and checked so far; *Total files* is the
  table's rows, and *Not checked* (§7) is what remains. Each tab's *Stop* button is enabled only on the running audit's tab; the menu item and Esc stop it from either tab (§5.8).
  If Stop is pressed before the count has returned (during the type lookup or the count), no report is written and the status bar reads `Stopped`: nothing has been checked, so
  there is nothing to report.
- **SQL failure** (connection, a bad ID column, timeout; a bad table or path column, or a path column that is neither `nvarchar` nor `varchar`, gets the errors of §6.2): error
  dialog with the message. If no row had been handled when it failed, no report is written. If it fails mid-run, the report is written with `Result: Failed — <message>`, the counts
  so far and the empty-path IDs read so far.
- **Report write failure**: error dialog.
- **What follows the run.** A run that **completes** shows the finished dialogue, with *The Missing Files audit has finished.*, a figures line `Missing: <m>    Unverified: <u>`
  (four spaces between the two, each value as the Audit group shows it at the end of the run, so *Unverified* keeps its bracket when the group shows one), then `The report was
  saved to:` and the report's full path. *View Report* opens the report the run wrote (the one captured at Start, not the selected tab's) through the same code as the Audit group's
  *View Report*, including its error dialogue; *OK* closes the dialogue. A run that **fails** (a SQL failure or a report write failure; also a path column that is neither
  `nvarchar` nor `varchar`, or not found, which the run refuses, §6.2) shows its error dialogue, now with the error sound and the flash. A run the user **stops**, before or after
  checking began, shows nothing new, since they are at the window; that includes *Stop it and exit?* answered Yes (any stop request skips the finished dialogue). Pre-flight errors,
  found before the run starts, get neither the sound nor the flash.
- **Closing the window mid-run**: a prompt, *"An audit is in progress. Stop it and exit?"*, with **Yes** and **No**. Yes stops the run, writes the report as
  `Stopped by user`, then exits; No carries on. File → Exit and Alt+F4 behave the same. If Yes is chosen before the count query returns, no report is written (as for
  Stop) and the application simply exits.
- **Closing the window, in general**: every close, including one caused by the operating system logging off or shutting down, runs the save rule (§8.2) before the window
  goes. During a run the stop-and-exit prompt above comes first, and the save rule runs only if the answer is Yes. *Cancel* on any of the save rule's prompts keeps the
  window open.

### 6.5 Running the unindexed audit

Start on the Unindexed Files tab runs the pre-flight checks of §6.1 (the ID column is not used) with those of §6.5.1 added after the identifier, project folder and
file-name checks, the varchar warning (§6.1 step 4) coming last of all, and then the run itself.

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

After these checks, and after the report checks of §6.1, comes the *varchar* path column warning of §6.1 step 4, which both tabs share.

#### 6.5.2 Loading the index

Status `Loading the index...`. First the path column's type is checked, as §6.2 describes (it must be `nvarchar` or `varchar`; a column that is not, or is not found, is an error
and nothing is loaded). Then only the path column is read (never the ID), forward-only and with no command timeout:

```sql
SELECT DISTINCT [PathColumn] AS [Path] FROM [schema].[table] WHERE <prefix filter>;   -- every part bracket-quoted as in §6.2
```

The selected value is always the raw column (the app decodes a `varchar` value into the right string and hashes that). A `varchar` column is converted only inside the filter, as
`CAST([PathColumn] AS nvarchar(4000))`, whatever the column's length, in every `LIKE` clause below (the prefix clauses and the white-space clause), because those clauses carry a
`COLLATE` that would otherwise be applied to the `varchar` value before it meets the `nvarchar` parameters. Both kinds of clause test only the start of the path (a folder prefix,
or the first character), so characters beyond 4,000 can never change the result, and no `nvarchar(max)` is needed (milestone 6; it was `nvarchar(max)` for a longer
column before). With the filter dropped nothing is converted, and an `nvarchar` column never is.

- **Prefix filter.** For one to 20 folders the statement asks SQL only for rows under those folders: one case-insensitive `LIKE '<folder>\%'` per folder (`COLLATE
  Latin1_General_100_CI_AS`, so a case-sensitive column collation cannot drop rows), with `|`, `%`, `_` and `[` escaped, joined with `OR`. The filter ignores *Include subfolders*
  (a folder's prefix is a superset of its direct files), so tables that span many projects send only the relevant rows. A further `OR` clause selects every row whose value **starts
  with a whitespace character**, because the values are trimmed before they are matched (§6.5.3) and such a row would miss every prefix. With no folder, more than 20
  folders, or an empty folder path, the filter is dropped and every row is loaded. Each folder is made canonical once (a full path with one trailing separator) and the same
  list goes to the filter and to the walk, so the filter loads exactly the rows whose prefixes the walk reports under, whatever form the folder was typed in (`U:/A/./B`, other
  letter case, a trailing separator). So the filter never changes the result. There is one index set only: links are never followed (§6.5.3), so no file is matched by any path
  but its own.
- **Hashes.** Each path is trimmed, upper-cased (invariant culture) and hashed to **128 bits** (MD5 of its UTF-16 bytes: an in-box algorithm chosen for speed and even spread, not
  security); only the hashes are kept, in a set. `NULL` values are skipped. **Index paths** (§5.4a) counts the distinct keys in that set, each counted when it is added, so a
  repeat of a path (another letter case, or surrounding white space) adds nothing and the figure grows while the load runs. A 128-bit hash makes a false match, which would hide an
  unindexed file (anti-goal), practically impossible. Memory is about **48 MB per million rows**, measured (one million rows loaded in 1.6 seconds on a local SQL Server), so
  three million rows take roughly 150 MB.
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
- **Excluded files.** A file the walk meets that is not checked, because no File types entry matches it or an Exclude entry does, is counted as **Excluded** (§5.4a). A name with no
  extension counts, and so does a symbolic-link file of an excluded type (the filter runs before the link probe). Hidden and system files are skipped before the filter and are
  not counted. A folder that cannot be listed reports none of its files, so none of them is counted as Excluded. Excluded files are never part of *Total files*, so Total files +
  Excluded is every file met that is neither hidden nor system, except a symbolic-link file of a checked type and a file whose link status cannot be read, which are reported
  under *Unverified* (below) and are in neither count.
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
  times a second. There is no up-front count, no percentage and no *Remaining* (§5.4a). *Index paths* counts up while the index loads and *Excluded* while the walk runs.
- **Stop** while the index is loading (which includes the path column's type check, §6.2): no report is written and the status bar reads `Stopped`; the screen keeps the *Index
  paths* count reached. A failure while the index loads likewise writes no report and leaves the partial count on screen. After that, Stop (button, menu and Esc) stops
  starting new listings, abandons the ones in progress (their files are not reported) and writes the report with `Result: Stopped by user`; the counts are what was found and
  checked, and there is no *Not checked* line, since the total is unknown. A Stop that arrives after the walk has finished but before the run reads its stop request produces a
  *Stopped by user* report for a complete walk; this is cosmetic.
- **SQL failure** while loading the index (connection, timeout; a bad table or path column, or a path column that is neither `nvarchar` nor `varchar`, gets the errors of §6.2):
  an error dialogue, and no report, since nothing has been walked. **Report write failure:** an error dialogue.
- **What follows the run** is as in §6.4, with the first line `The Unindexed Files audit has finished.` and the figures line `Unindexed: <n>    Unverified: <u>` (four spaces
  between the two, each as the Audit group shows it at the end of the run). Stop, before or after the walk, shows nothing new; a failure gets the error sound and the flash.
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
Distinct:        98,200
Duplicates:      24,750
Found:           97,820
Missing:         260
Unverified:      120 (310)
Skipped:         1,240

Missing Files (260)
-------------------
[Empty (60)]
doc_id 10871   \\fs01\docs\2019\05\10871.tif
...

[Does not exist (200)]
doc_id 10443   \\fs01\docs\2019\04\10443.pdf
doc_id 10502   \\fs01\docs\2019\04\10502.pdf
...

Unverified Files (430)
----------------------
[Permission denied (100)]
doc_id 22010   \\fs02\restricted\22010.pdf
...

[Path is empty in the index table (310)]
doc_id 22011
doc_id 22012
...

[Other errors (20)]
doc_id 22030   \\fs03\docs\22030.pdf   I/O error   The semaphore timeout period has expired.
...
```

In the Summary, *Total files* is T, the table's rows; *Distinct* is D, the different real paths seen; *Duplicates* is the rows that repeated one; *Unverified* is U, with the
empty-path rows E in parentheses when there are any. All mirror the screen (§5.4) and use `ReportFormat.Count` (`N0`, invariant culture).

Rules:

- Group titles and their counts are wrapped in square brackets, each on its own line, with a blank line between groups.
- A group with no files is omitted. A section with no groups reads `No missing files.` / `No unverified files.`
- *[Path is empty in the index table]* is built from the empty-path count and IDs collected as the rows were read, not from the checks, so its count is the empty-path rows E read.
  With an ID column set it holds one line per empty-path row (not per distinct ID, so the count matches the lines), `<IdColumn> <id>`, sorted as the other groups are; a stopped or
  failed run lists the IDs read so far. Without an ID column it holds one line in place of the IDs and never blank lines: `Missing filepaths not listed because no ID column was
  specified`.
- `ID column:` reads `(none)` when not set. `Empty files:` reads `Reported as missing` or `Not reported`. The Project folder / stage lines are omitted when a custom location
  is used.
- Missing groups appear in the order *Empty*, *Does not exist*; Unverified groups in the order of the table in §6.3, *Other errors* last.
- The Summary lines come in this order: `Total files:`, `Distinct:`, `Duplicates:`, `Found:`, `Missing:`, `Unverified:`, `Skipped:`, then `Not checked:` for a *Stopped by user* or
  *Failed* result only. `Distinct:`, `Duplicates:` and `Skipped:` are always present (`0` when none). `Total files:` is T, the table's rows; `Unverified:` is `U`, or `U (E)` when E
  is above zero (§5.4). Skipped rows are counted, never listed. `Not checked:` is T − (Found + Missing + U + Duplicates + E + Skipped), never below zero, so Found + Missing + U +
  Duplicates + E + Skipped + Not checked = T (§6.2); the line is omitted for a completed run, where Found + Missing + U = Distinct and Distinct + Duplicates + E + Skipped = T.
- The *Unverified Files* section's heading count is U + E, since it lists both the checked outcomes and the empty-path rows. There are no blank lines other than the one between
  groups and sections.

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
Excluded:        1,113
Index paths:     48,190
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
- **Summary.** The lines come in this order: `Total files:`, `Excluded:`, `Index paths:`, `Indexed:`, `Unindexed:`, `Unverified:`, mirroring the screen column by column (§5.4a).
  `Total files:` is the files found and checked, so Total files = Indexed + Unindexed. `Excluded:` is the files skipped by the file type filters and is not part of Total files
  (§6.5.3). `Index paths:` is the distinct index paths loaded to compare against (§6.5.2). `Unverified:` counts the folders that could not be listed plus the links
  that were not followed, not files checked. A *Stopped by user* report has `Result: Stopped by user`. A run that fails while loading the index at the start writes no report
  (§6.5.4).
- **Unverified** lists the folders that could not be listed and the links that were not followed, in groups ordered as in §6.3 with *Link not followed* before *Other errors*:
  *Drive or share not found*, *Permission denied*, *Network location unreachable*, *Path too long*, *Link not followed*, *Other errors*. A folder path ends in `\`; a file link is
  a plain path. *Link not followed* lists every junction or symbolic link that was skipped (§6.5.3): an input folder, a folder met in the walk, or a matching file. *Other errors*
  shows the reason and the system message in aligned columns and also holds a folder whose path is malformed or any failure not listed above (reason text, no system message).
  An item is listed once, and nothing beneath a folder is. `No unverified folders or links.` when there are none. An entry whose link status cannot be read is reported under *Other
  errors* with the reason `Could not tell whether this is a link`, and is neither walked nor checked.

## 8. Settings

Settings belong to the **project**, not the Windows user. They are saved as `FileAuditorSettings.json` beside the project's report, so that the file lives in the project's
repository with the report and any teammate can open the same project. The per-user `%APPDATA%\MediQuant\FileAuditor\settings.json` of earlier builds is no longer read or
written; it is left on disk untouched and is not migrated.

The file holds every input of every direction: server, database, table, path column, ID column, empty-files option, project folder, project stage, audit stage (default Interim),
custom-location flag, custom location, and the custom file name of the missing-files report (`MissingCustomFileName`, default `FileAuditorMissingReport.txt`). The unindexed
direction adds its folder list (`ScanFolders`: each folder's path and *Include subfolders* flag), the whitelist (`FileTypes`), the blacklist (`ExcludeTypes`), the grouping choice
(`GroupByFoundExtension`) and its own custom file name (`UnindexedCustomFileName`, default `FileAuditorUnindexedReport.txt`). A file without them loads with an empty folder list,
the 15 built-in file types, an empty blacklist, *Listed type* grouping and the default name; a folder entry without a path is dropped, and an invalid entry in either type list is
dropped on its own while the others are kept. Older files load, but their custom file names fall back to the defaults; no migration is attempted. The remembered answers to the
three file-type warnings (§6.5.1) and to the varchar warning (§6.1) are not stored either. No credentials. The *Save project settings* checkbox is not stored in it. The file is
written with a temp file + `File.Move(overwrite: true)`; a save never creates a folder. A `null` value of any kind (a string, a flag, a list, a list element or a folder's path) and
an undefined enum value fall back, field by field, to that field's default; every other value is kept. The file is read into a `ProjectFile`, a model with nullable members that
mirrors the JSON (and a `ProjectFileFolder` for each scan folder), and `SettingsStore` maps it to an `AppSettings` that has no nullable members, so nothing else in the app ever
sees a missing value. The keys keep their names (`ProjectStage` and `AuditStage` among them), enums are written as strings and no `null` is written. Window size and position are
not saved — the window is fixed-size. The milestone 2 spec (`Docs/Specs/Epic-1-MVP/milestone-2-project-settings-design.md`) has the full prompt texts.

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
  warning answers (§6.5.1), the varchar warning's remembered column (§6.1) and the open file, and clears both Audit groups' statistics and the last run's status (the status bar
  reads `Enter the required details`, §5.5). It is equivalent to relaunching the app.

### 8.2 The save rule

One rule, applied at four save points: **Start** (after the pre-flight checks pass, §6.1 step 5), **closing the window** (§6.4), **Close project** and **Open project...**.

1. **Nothing has changed** since the project was opened or last saved (the current inputs equal the settings last loaded or saved; a fresh, untouched window counts as
   unchanged) → do nothing, and do not ask.
2. **Checkbox ticked** → save (§8.3) without asking.
3. **Checkbox unticked and the user has already answered No** this session → do nothing.
4. **Checkbox unticked and not yet answered** → ask *"Do you want to save this project's settings?"* with **Yes** and **No**. *Yes* saves and ticks the checkbox, so later
   saves happen without asking. *No* does not save, and the app does not ask again until *Close project* or a fresh launch.

So the three states read: *ticked* — always save; *unticked* — ask at the next save point; *unticked and declined* — never ask, never save, for the rest of the session.

**Backing out.** *Cancel* on any of the prompts in §8.3, or on a save failure, backs out of the operation that triggered the save:

| Save point             | Cancel means                                                        |
|------------------------|---------------------------------------------------------------------|
| **Start**              | The audit does not start; the window is left as it was.             |
| **Closing the window** | The window stays open.                                              |
| **Close project**      | The project stays open, unchanged.                                  |
| **Open project...**    | The picker is not shown; the current project stays open, unchanged. |

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
- **A different settings file is already there.** When the target is a `FileAuditorSettings.json` that is not the project's own file (a never-saved project, or after *Report
  Folder*), the app warns *"A project settings file already exists in this folder ... Do you want to overwrite it?"* with **Overwrite**, **Skip Save** and **Cancel**. *Overwrite*
  saves over it and it becomes the project's file; *Skip Save* does not save this time and the operation goes ahead; *Cancel* backs out.
- **A save that cannot be done:** a never-saved project whose report location is incomplete (no Project folder, or a custom Location that is not a full path), a folder that
  does not exist, or a failed write (read-only, access denied, disconnected drive). The app never creates a folder just to save the settings. It shows an error dialogue
  naming the path and the reason, with **Skip Save** (the operation goes ahead without saving) and **Cancel** (backs out). On Start the pre-flight checks have already
  confirmed the report folder exists and is writable, so a failure there is rare.

## 9. Code shape

One project, `Source/FileAuditor/`, organised by folder (namespaces mirror folders). Only the `.csproj` and `app.manifest` sit at the project root; `Assets/` holds the
app icon, and `Assets/Win95Icons/` the Windows 95 icon set. Dialogue icons are taken from that set (`error32.gif`, `warning32.png`, `question32.png`, `information32.png`),
never drawn in XAML. Each image the app uses is listed as an `AvaloniaResource` in the `.csproj`. The code folders are:

- `Startup/` — `App`, `Program` and `UnexpectedErrorHandler`, the global exception handler (see *Errors* below).
- `Views/` — `Main/` (`MainWindow`), `About/` (`AboutWindow`), `Dialogues/` (`MessageDialogueWindow`, `FileTypesDialogueWindow`), `Groups/` (a `UserControl` per group box:
  Database, File Index Table, Project Settings, Report, Audit, Unindexed Audit) and `Tabs/` (a `UserControl` per audit direction, the content of a tab: `MissingFilesTabView`,
  `UnindexedFilesTabView`).
- `ViewModels/` — one view model per view (CommunityToolkit.Mvvm), in the parallel folders `Main/`, `About/`, `Dialogues/` (`MessageDialogueViewModel`, `FileTypesDialogueViewModel`
  and its row item `FileTypeEntryItem`, which has no view of its own and so stays here), `Groups/` (including `ProjectSettingsViewModel`, which owns the Project Settings group's
  status message) and `Tabs/` (`MissingFilesTabViewModel`, which owns the direction's *Report empty files* option, its `ReportViewModel` and its `AuditViewModel`;
  `UnindexedFilesTabViewModel`, which owns the folder list, the two file-type summaries, its `ReportViewModel` and its `UnindexedAuditViewModel`). `Groups/` also holds three types
  that are deliberately not a `*ViewModel` (they have no view of their own): `ReportLocationState`, the Report group's shared values, owned by the main window (each direction's
  `ReportViewModel` reads it and adds the direction's file name and, for the Unindexed direction, the grouping choice, so the tabs' Report groups cannot drift apart);
  `AuditStatusBase`, the base of both audit view models and what the status bar binds to (`StatusMessage`, `StatusPercent`, and `IsStopEnabled`, which enables that tab's Stop
  button); and `ScanFolderItem`, a row of the folder list. `Groups/` also holds the Audit group's statistics helpers, each deliberately not a view model: `RecentRate`, the
  30-second exponentially weighted rate with its 5-second warm-up and the formatter of the *Files/second* text; `RemainingEstimator`, which owns a `RecentRate` and turns progress
  samples into the Remaining text (§5.4); and `TimeText`, the one overflow-safe `hh:mm:ss` formatter that Elapsed and Remaining share. `Main/` also holds `AuditRun` and its two
  subclasses `MissingAuditRun` and `UnindexedAuditRun`, which carry one direction's run (progress object, phase text, refresh, result text, and the finished dialogue's first
  line and figures line) for `MainWindowViewModel`. `MainWindowViewModel` routes Start and View Report to the selected tab's direction and Stop and the status bar to the
  running one.
- `Services/` — `Settings/` (`SettingsStore`, a static class that maps a settings file to and from `AppSettings` through `ProjectFile`, at a path it is given, and never creates a
  folder; `ProjectSettingsService`, which holds the open project's file path and baseline settings and runs the save rule of §8.2 with its prompts, returning a `SavePointResult`
  that says whether the triggering operation may proceed and whether the checkbox is to be ticked; it also remembers the three file-type warning answers of §6.5.1), `Sql/`
  (connection strings, SQL identifiers, query builder, which also builds the path-only, prefix-filtered query, and the static `IndexTableReader`, which counts the table's rows,
  streams them for the Missing Files audit and also reads the paths for the unindexed audit), `Checking/` (file checker, `SkipMarker`, which decides whether a path value is a skip
  marker, and the Win32 error mapper), `Scanning/` (the unindexed direction's `FileTypeList`, `FileTypePattern` and `FileTypeFilter`, the 128-bit-hash `IndexPathSet`, and the
  concurrent, static `FolderWalker`; `FileTypeList`, `FileTypePattern` and `IndexPathSet` stay here, not in `Models/`, because they carry logic), `Auditing/` (the static
  `AuditRunner` and `UnindexedAuditRunner`, pre-flight checker, `PathKeySet`, the 128-bit-hash set of the Missing Files audit's de-duplication keys (§6.2), and the progress types
  including `AuditProgress`, whose counters the reader and the checks write while the screen reads them, and `UnindexedProgress`, which also keeps the scan clock of §5.4a),
  `Reporting/` (the two report builders, `ReportFormat` with the formatting they share, writer and path resolver) and `Dialogues/` (dialogue service, which also offers a
  three-button choice returning a `DialogueChoices`, the finished dialogue and the failed-run error dialogue, the *Open Project* file picker, the File Types dialogue and
  `WindowAttention`, the static class that plays the end-of-run sound and flashes the main window's taskbar button through `MessageBeep` and `FlashWindowEx`, §5). Stateless
  services are static classes (§12). Return `ErrorOr<T>` for expected failures.
- `Models/` — plain data, with no behaviour worth a service. `Settings/` (`AppSettings`, the settings record with no nullable members; `ProjectFile` and `ProjectFileFolder`, the
  nullable JSON shape of §8; `ScanFolder`; and `SavePointResult`), `Auditing/` (`AuditQuery`, `PathQuery`, `IndexRow`, `RowCounts`, `CheckResult`, `MappedOutcome`, `AuditRequest`,
  `PreflightResult`, `ScannedFile` and `UnverifiedFolder`), `Dialogues/` (`ProjectFilePick`, the *Open Project* picker's result) and `Reporting/` (`AuditReport`, `UnindexedReport`
  and `ReportPath`).
- `Common/` — `GlobalUsings.cs`; `Constants/` (every enum, one file each, in a folder named for the area it serves and with a plural type name: `Auditing/` `AuditDirections`, the
  directions `Missing` and `Unindexed`, whose member names the report file names are tied to, and `CheckOutcomes`; `Dialogues/` `DialogueChoices` and `DialogueIcons`; `Reporting/`
  `AuditResultKinds`; `Settings/` `ProjectStages` and `AuditStages`); `Errors/` (the custom `Error` definitions: audit, query, report, settings, SQL); `Extensions/`
  (`ClassicWindow` helpers); `Links/` (`HelpLinks`, the user-guide address); `Serialisation/` (the source-generated settings JSON context, `SettingsJsonContext`, which serialises
  `ProjectFile`).

Services are wired by hand in `App`; there is no dependency-injection container. `MainWindow.axaml` composes the shared group-box views (the Database and File Index Table groups
in a two-column grid) and the tab control (each tab's content is a `Tabs/` view) and holds the menu bar and status bar; it stays at 100 lines or fewer by pushing content into the
group-box and tab `UserControl`s. `ReportPathResolver.Resolve` takes the settings and an `AuditDirections`, so a direction's report name comes from one place.

**Errors.** Services return `ErrorOr<T>`: expected failures (bad input, SQL errors, an unwritable report, unreadable settings) are values with custom errors from
`Common/Errors/`, not exceptions. Exceptions are caught at the boundary where they occur (SQL, file system) and translated into errors or check outcomes. SQL access is
plain `Microsoft.Data.SqlClient`, with no ORM.

**Unexpected exceptions.** `UnexpectedErrorHandler` (`Startup/`) catches what nothing else handled. `App` installs it at start-up and `Program.Main` wraps the lifetime in it.

- **Recoverable** — an exception unhandled on the UI thread (marked handled) or in a task nobody awaited (marked observed, then posted to the UI thread; for the task's
  wrapper the first inner exception's message is shown). The app carries on, and the user sees the main window's error dialogue (error icon, *OK*):

  ```
  Something unexpected went wrong:
  <the exception's message>

  File Auditor will carry on.
  ```

- **Fatal** — an exception that reaches the top of any thread (`AppDomain` unhandled), one that escapes the lifetime in `Program.Main`, and an `OutOfMemoryException` anywhere,
  including inside an `AggregateException`. A plain Windows message box (`MessageBoxW`, caption *File Auditor*, error icon) says:

  ```
  File Auditor ran into a problem it cannot recover from and has to close.

  <the exception's message>
  ```

  and the process then exits with code 1. The box is owned by the main window when that is safe to ask for; otherwise it asks to be topmost and in the foreground, which
  Windows may refuse to a process in the background. A second fatal error on another thread waits until the first box has been read.
- **One notice at a time.** While a recoverable notice is open, including the plain message box that takes over when there is no main window or the dialogue cannot be shown,
  further recoverable errors are dropped, so an exception repeated on every refresh tick cannot stack up dialogues. A fault that repeats (on every 250 ms refresh, say) shows the
  notice again after *OK*, never more than one at a time.
- **No crash log.** Nothing is written to disk, by the storage rule below.
- Every `async void` event handler (the File Types dialogue's *OK*, the Report group's *Browse...* and the Unindexed tab's *Add...*, and the main window's About handler) and
  every fire-and-forget task (the close confirmation, the report probe and the untick confirmation) wraps its whole body in `try` / `catch` and reports through the handler (§12),
  so no fault waits for finalisation to surface.

**Storage.** The app writes only the project settings file (§8) and the report (§7), both in the report's folder — never anything beside its executable, which runs from a
shared file server, and nothing under `%APPDATA%`.

**Publishing.** `dotnet publish` produces a single self-contained executable for 64-bit Windows (`win-x64`, native libraries and debug symbols embedded,
compressed), so the shared file server holds one file and the target machines need no .NET runtime. The settings sit in a `PropertyGroup` conditioned on publishing,
so ordinary builds and `dotnet run` are unchanged.

## 10. Builder's calls (decided by Claude, not the user)

- Read-only preview of the built report path, with `<placeholders>`; Start disabled until the path is complete. *(shown in mock-up, accepted)*
- Disabling, rather than hiding, the Project folder row and both stage groups while Custom is ticked, on both tabs. *(shown disabled in the mock-up and accepted; the user
  changed it to hidden during milestone 4 and back to disabled in milestone 4.1)*
- Pre-flight checks happen on Start, before the database is queried (milestone 5 adds one exception: the path column type lookup behind the varchar warning, §6.1).
- Report timestamps in local time; sorting by path; UTF-8 without BOM; CRLF.
- Behaviour on Stop and on mid-run SQL failure (§6.4).
- `schema.table` support and bracket quoting.
- The set of Unverified reasons and their wording (§6.3).
- Status bar messages and keyboard shortcuts.
- Extra report header lines (Empty files, Project folder, stages).
- `Total files` stays the distinct real-path count D on Stop or failure, with a `Not checked:` line (§7). *(Superseded for the Missing Files audit by milestone 6, below: *Total
  files* is the table's rows.)*
- Distinct paths (from user feedback, after milestone 5; commit `b22d63c`, the groundwork of Epic 2's milestone 1): both directions' queries are `DISTINCT`, so a path on many rows
  is checked once; with an ID column the unit is the path and ID pair, and the ID's uniqueness per path is the user's to keep, not checked (§6.2). The unindexed direction already
  kept one hash per path, so there `DISTINCT` only cuts the rows sent. *(The Missing Files direction's `DISTINCT` is superseded by milestone 6, below: the app de-duplicates; the
  Unindexed query is still `DISTINCT`.)*
- Row accounting (Epic 2, milestone 1), settled with the user: the table's rows, empty-path rows and skip-marker rows are counted up front, so Duplicates, Skipped and the empty
  count are final when counting ends, and `DISTINCT` no longer hides them; *Total files* is `D (T)` when they differ; a *Duplicates* statistic sits between *Total files* and
  *Checked*; *Found* is added on screen; empty paths stay under *Unverified*, with their count in parentheses on *Unverified*; skipped rows are counted by query, not one by one,
  and non-ASCII case folding in skip markers is not handled; the table is assumed not to change during a run, so the app does not reconcile drift; without an ID column the empty
  group shows its count and `Missing filepaths not listed because no ID column was specified`; the report's Summary mirrors the screen. *(Its SQL classification, its up-front
  counts, the `D (T)` form of *Total files* and the empty-ID read are superseded by milestone 6, below; the statistics and the rule that the Summary mirrors the screen stay.)*
- Row accounting, decided by Claude (for the user to check): *Unverified* hides its parentheses when E = 0, by analogy with *Total files*; the empty-path IDs are read only after a
  completed check, Stop abandons that read without changing the result, and its status is `Reading empty paths...`; the lines that replace the IDs when they were not read (§7) are
  worded after the user's no-ID line *(the read, its status and those lines are superseded by milestone 6, below)*; a failure with nothing checked still writes no report;
  empty-path rows are listed one line per row, not per distinct ID; the old `max(count, processed)` guard on *Total files* is dropped, since the table is assumed unchanged; the
  window grows to 640 × 668 for the fourth statistics row, with the grid columns of §5.4; on screen *Checked* is Found + Missing + U with each counter read once per refresh,
  because reading a separate Checked counter gave a torn snapshot mid-run (Checked below Missing); the checker keeps its skip and empty tests as an unreachable safety net, and a
  stray one is recorded as *Unverified — Other errors* with the reason `Skip marker not recognised by the database` (reachable, because the checker's `OrdinalIgnoreCase` folds
  `ſkip` to `SKIP` where the database does not), so it is counted and listed *(superseded by milestone 6, below: the reader and the checker run the same tests on the same
  string, so a stray one cannot occur)*; a failure of the empty-path ID read alone keeps the completed audit's report.
- The nvarchar path column (Epic 2, milestone 1.1; **its requirement that the column be `nvarchar` is superseded by milestone 4**, below, which accepts `varchar` too; the history
  stays as it was), settled with the user: the cast of the path column to `nvarchar(max)`, which made the classification type-agnostic but is the costliest type to work on, is
  removed; the app expects an `nvarchar` path column, checked before any row is read, for both audits, with the two errors of §6.2; the ID column stays unrestricted. Decided by
  Claude (for the user to check): the check reads the column's system type, so an alias type over `nvarchar` is accepted and `nchar` is refused, since the user said `nvarchar` (a
  one-line change if wrong); the type is read under each audit's existing first status with no new status text, and Stop during it is the stop-before-checking of §6.4 and §6.5.4.
  *(Superseded by milestone 6, below, for the Missing tab: the audit converts nothing.)* One cast remains, inside the trim's length and only for rows past the `skip` prefilter,
  because without it a 4,000-character value in an `nvarchar(4000)` column is truncated by the added character (a skip marker padded to 4,000 characters was misclassified, and one
  with 3,996 trailing spaces failed the whole audit with error 537). Measured on 200,000 rows in LocalDB, with the cast and without it: the counts statement 1,154 to 589 ms (about
  620 to 850 ms on a later, noisier run), the stream 1,654 to 1,385 ms, the empty-ID read 498 to 238 ms; these headline figures were taken on the fully cast-free statements, before
  the narrow `LEN` cast was restored. The final statements, re-timed on a separately generated 200,000-row `nvarchar(400)` table (140,000 real paths, 40,000 empty, 20,000 `SKIP`),
  three runs each in LocalDB on a different day: the counts statement 2,211, 1,796 and 1,490 ms (the first run is cold) and the stream (distinct real rows, with an ID column)
  1,530, 1,243 and 1,232 ms. These are not comparable with the figures above (different data and machine load), and say nothing about the cast; the type lookup was not timed.
- The path column's type lookup (Epic 2, milestone 1.1, final review), decided by Claude (for the user to check): the type is read from the result-set metadata of the statements'
  own SELECT shape (`sp_describe_first_result_set`, §6.2) rather than from the catalogue views, so permissions, synonyms (also to another database) and views behave as the
  statements do. A first error of 207 or 208 is "not found", unless a 4413 follows (a view that no longer binds, whose message is shown as it is); any other error, such as
  229 for a table the account cannot read, is `Sql.QueryFailed` with SQL Server's message, so a missing permission is not reported as a missing column. The first
  catalogue-view implementation (`sys.columns` with `OBJECT_ID`) reported a table with no SELECT permission, and a synonym, as not found, and crashed on a CLR-typed column.
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
  project files are never reported (later replaced by the user's 15-type list, which includes `.txt`, §5.2b); hidden and system folders are skipped with their contents; a
  file under two input folders is reported once, under the deepest; the path column is trimmed before hashing; the SQL prefix filter is an optimisation with a fallback to
  every row (§6.5.2); hashing is an in-box algorithm (MD5), so no package was added; *Indexed* replaces *Checked* in the statistics (every file found is checked at once);
  links are never followed, only reported; Stop and the status bar follow the running audit rather than the selected tab; Stop while the index loads writes no report; equal
  custom file names on the two tabs are refused; up to 16 folder listings run at once and the statistics refresh four times a second; a duplicate folder is not added to the list
  and, when it reaches Start by a hand-edited file, is refused; what "matches every file" means is defined in §6.5.1.

- Skipped rows and run statistics (milestone 5, from the user's five requests): the skip rule is exact and checked before every other check (§6.3); a skipped row is never listed,
  and the report's `Skipped:` line is always present on the missing-files report; on-screen *Total files* was `max(count, processed)` so the numbers agreed with the report if the
  table grew (dropped in Epic 2's milestone 1, §6.2); the processed count (checked plus skipped) drove the progress bar and Remaining (then checked rows only, and rows handled
  since Epic 2's milestone 6, §6.4). Remaining is the 30-second recent rate seeded from the warm-up's second half (the whole-warm-up average included the start-up ramp, so
  Remaining fell 1.4 to 1.9 s per second for the first half-minute), eased with a 3-second constant, and held once the processed count has stood still for about 1 second; the
  display is clamped to 36,000,000 seconds. The recent rate sits in parentheses after the whole-run Files/second, not after a middot (Win9x convention). The Missing figures have
  two bases (checked over wall time; processed over checking time, the rate behind Remaining) and the Unindexed recent rate runs on a scan clock that starts with the walk, so the
  index load does not use up its warm-up. `N (0)` after a stall is shown as it is. The statistics became three columns on both tabs, which took the Audit group from four rows to
  three and the window from 668 to 651 pixels high (it is 668 again since Epic 2's milestone 1, which added a fourth row). The Unindexed columns (Total files and Indexed; Unindexed
  and Unverified; Elapsed and Files/second) followed the user's Missing layout by analogy (regrouped in Epic 2's milestone 2, below). `Esc` was not changed.

- Unindexed audit statistics (Epic 2, milestone 2, design `Docs/Specs/Epic-2-Audit-Optimisation/milestone-2-unindexed-stats-design.md`), settled with the user: the Unindexed
  Audit group gains *Excluded* and *Index paths*, in the user's layout of three columns and three rows (Total files, Excluded, Index paths; Indexed, Unindexed, Unverified;
  Elapsed, Files/second). *Excluded* is every file the walk met that no File types entry matches or an Exclude entry does: never part of Total files (so Total files = Indexed +
  Unindexed stays exact), not hidden or system files, a name with no extension and an excluded symbolic-link file included, none from a folder that cannot be listed. *Index
  paths* is the distinct trimmed, case-folded keys of the path set, counted as they are added, so it is live during the load; with the folder filter on it includes the rows
  that start with white space, and with it off the whole table. Both were built by approach A (the walk's third callback; `IndexPathSet.Add` reporting a new path), chosen over a
  private tally returned at the end and over reading the set's size after the load, because both keep the figures live. *Found*, *Duplicates* and a *Not on disk* statistic were
  considered and left out. *Indexed* moved from the first column to the top of the centre one at the user's request; its old place was Claude's own call by analogy (ruling R6
  of Epic 1's milestone 5), not the user's. The design's section 6 records four possible optimisations of the index load and walk, none measured and
  none built.

- The finished dialogue (Epic 2, milestone 3, design `Docs/Specs/Epic-2-Audit-Optimisation/milestone-3-finished-dialogue-design.md`), settled with the user: approach A (the
  dialogue, a taskbar flash and a sound), chosen over bringing the window to the front (which could take the keyboard while the user types elsewhere) and over the dialogue alone
  (which opens behind other windows and goes unseen); the dialogue appears for a completed run only, a failed run keeps its error dialogue and gains the attention, and a stopped
  run, a pre-flight error and a close mid-run show nothing new; the dialogue's content is the audit's name, the final figures, the report's full path and *View Report* beside *OK*
  (§5, §6.4, §6.5.4). Decided by Claude (for the user to check): *View Report* carries the access key `V`, since the other dialogues' action buttons have one (*Create Folder*,
  *Yes*) and *OK* has none; the flash happens only when the main window is not active, and never activates it; both Win32 calls (`MessageBeep`, `FlashWindowEx`) are best effort, so
  a failure is ignored and the dialogue still shows; the dialogue's *View Report* opens the report of the run that finished (captured at Start), not the selected tab's; any stop
  request skips the finished dialogue, including a Stop during `Reading empty paths...` whose result stays *Completed* (that phase is gone since milestone 6);
  `Assets\Win95Icons\information32.png`, already in the project folder, gained its `AvaloniaResource` entry in the `.csproj`, as every image the app uses must have (§9).
- The varchar path column (Epic 2, milestone 4, design `Docs/Specs/Epic-2-Audit-Optimisation/milestone-4-varchar-path-column-design.md`), settled with the user: `varchar(n)` and
  `varchar(max)` path columns are accepted on both tabs and converted to `nvarchar` where the statements need it, which supersedes milestone 1.1's requirement of an `nvarchar`
  column; only `varchar` is added (`char`, `nchar`, `text`, `ntext`, numbers and the rest stay refused); approach A, an explicit conversion once per statement, chosen over letting
  SQL Server convert implicitly and over converting in the app (impossible for the counts); the reasoning behind converting inside the Unindexed folder filter (§6.5.2) is recorded
  in the design and was not measured, by the user's decision (the final review's spot check in LocalDB agreed: a `Greek_CI_AS` path matched its folder only with the conversion);
  the version stays 2.0.0, since the app is not published (the `v2.0.0` tag's position is the user's call). Decided by Claude (for the user to check): the refusal's wording is `...
  expects an nvarchar or varchar column.`; its code is renamed `Sql.PathColumnUnsupported` (from `Sql.PathColumnNotNvarchar`) to match the rule; on the Missing tab the conversion
  is computed in one apply (`[fa_p]`, column `[FaPath]`) and everything else uses it, and on the Unindexed tab it appears only in the folder filter, so the selected value stays the
  raw column and with no filter nothing is converted. *(Superseded by milestone 6, below, for the Missing tab: it converts nothing and reads the column as it is, and the folder
  filter converts to `nvarchar(4000)` whatever the column's length.)*
- The varchar warning (Epic 2, milestone 5, design `Docs/Specs/Epic-2-Audit-Optimisation/milestone-5-varchar-warning-design.md`), settled with the user: a *Continue* / *Cancel*
  warning on Start, on both tabs, before a `varchar` path column is audited, remembered like the file-type warnings (until *Close project*, *Open project...* or a fresh launch,
  never saved in the project file); approach A, the type read in pre-flight, chosen over asking from inside the run (which needs a question opened mid-run, plumbing from the
  background run to the window, a clock that counts while the user reads, and a new way for a run to end); a user guide section on `varchar` path columns (§8.4 of the guide;
  rewritten by milestone 6, as is the warning's middle sentence). Decided by Claude (for the user to check): the remembered answer is keyed to server, database, table and path
  column (trimmed, ignoring case), so a different column is asked about again; the silent cases (`nvarchar`, a refused type, a missing column or table, any SQL error, any exception
  except out-of-memory, or a lookup that takes longer than 30 seconds) defer to the run, which reports them as before; the warning is the last pre-flight step, after the
  existing-report check and before the save rule, so a Start that fails for another reason never reaches the database; and `PathColumnType.Format` is public so the warning can
  write `<type>` as the refusal does. Settled with the user after the final review: an unreachable server is waited for once, so the lookup's result (a type, not found, or a SQL
  error) is handed to the run, which uses it instead of connecting again (`PreflightResult`, `AuditRequest.PathColumnType`).
- The streaming Missing Files audit (Epic 2, milestone 6, design `Docs/Specs/Epic-2-Audit-Optimisation/milestone-6-streaming-missing-audit-design.md`), settled with the user. On a
  real 1,570,465-row `varchar(8000)` table 2.0.0 spent about 85 seconds in SQL before checking began (the counts 45 s, the `DISTINCT` select 38 s to its first row, on the user's
  server), against 0.2 s for 1.0.0's count; the swings between runs were the file server, not the app. Four ways to remove that wait were measured in LocalDB on the same shape of
  table, as seconds before checking can start (the user's server was about 4.7 times slower): 2.0.0's statements 18 s; A, one `DISTINCT` into a temporary table, 10 s; B, 2.0.0's
  statements with `nvarchar(4000)` for `max`, 8 s; C, a classification-only count with the app de-duplicating, 3 s; the **chosen** approach, the user's refinement of C, with
  nothing classified in SQL at all, **1.0.0's two statements** (`COUNT_BIG(*)` and a plain select), under half a second. This supersedes milestone 1's SQL classification, up-front
  counts, `DISTINCT` stream and empty-ID read, and milestone 4's conversion on the Missing tab (all kept above as history). The user's calls: the statistics stay but are counted
  live as the rows stream; a new *Distinct* statistic sits between *Total files* and *Duplicates*, and *Total files* is the table's rows (the `D (T)` form is gone); *Duplicates*
  keeps its **rows** meaning (each extra row once, so a path listed three times is one *Distinct* and two *Duplicates*), which is what makes the figures add up to *Total files*;
  the Missing Files audit converts nothing; the Unindexed folder filter converts a `varchar` column to `nvarchar(4000)`, not `max` (it tests only the start of the path); the
  varchar warning's middle sentence no longer says the audit converts the column. Decided by Claude (for the user to check): the de-duplication key is the path with trailing spaces
  (U+0020) only removed and upper-cased with `ToUpperInvariant`, with the ID joined to it unambiguously (the path's length and an ID flag hashed ahead of the text), hashed with MD5
  to 128 bits as `IndexPathSet` is, so `ß` is not `ss` and a leading space or trailing tab is its own path (`IndexPathSet` trims both ends, so it is not reused); *Distinct* is
  counted when a key is first seen and shown no lower than *Checked*; *Remaining* and the progress bar run on rows handled, *Files/second* on its own recent rate of files checked,
  so the two estimators differ; *Not checked* is the larger of zero and T − (Checked + Duplicates + empty + Skipped); the empty-path IDs are collected during the stream, so a
  stopped or failed run lists those read so far, and the three explanatory notes and the `Reading empty paths...` phase are gone; the diagnostic that reported a skip marker the
  database did not recognise is removed (the app and the checker now use the same test); the milestone's branch was cut from `9c6d5ce`, not from local `main`.

Settled with the user after the builder check: roots checked once with a *Drive or share not found* group; unreachable locations short-circuit at the directory level
that failed, found by walking the path (§6.3); the close-mid-run prompt.

Settled with the user for milestone 4: the second direction itself; one or more input folders, each with *Include subfolders* ticked by default; matching on the full path
ignoring case; the editable extension whitelist and blacklist (the blacklist wins), the glob wildcards, the most-specific rule and the Start-time errors and warnings of §6.5.1;
grouping by listed type or found extension; no up-front count, so no *Remaining*; and, during the build, the layout: a compact Unindexed tab and a fixed tab height, the *Include
subfolders* column at the left of the folder list (moved to the right in milestone 4.1), File types and Exclude shown as read-only summaries edited in the File Types dialogue (with
Reset only for File types), a custom report location hiding the project folder and stage options (disabled instead in milestone 4.1), *Unverified* in the first statistics
column, and the Database and File Index Table groups side by side (a trial the user may reverse).

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
- **Window size and access keys (milestone 4, size revised in 4.1, 5 and Epic 2's 1):** the window is 640 × 668 pixels (outer), measured at the current display scale (651 in
  milestone 5, which cut the Audit group from four rows to three; 668 again once Epic 2's milestone 1 added the fourth row); the tab control fills the remaining row. On the
  Unindexed tab, *File t_ypes:* is `Y` and *E_xclude:* is `X`; *Add...*, *Remove*, both *Edit...* buttons and the *Group file types by* radios carry none, because every letter is
  taken. In the File Types dialogue *New*, *Edit*, *Delete* and *Clear* (milestone 4.1) and *Reset* are `N`, `E`, `D`, `L` and `R`, and *OK* and *Cancel* carry none. In the Project
  Settings group the checkbox's access key is `G`; its label no longer says *with the report*, which the status message covers.
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
- **XML doc comments on every type and member, at every access level** — private fields, constants, methods, nested types, enum members, record parameters and event handlers
  included (`<summary>`, `<param>`, `<returns>`, `<remarks>`, `<exception>` as appropriate); `//` comments only for inline implementation notes. `<c>…</c>` and `<code>…</code>`
  carry no inside whitespace.
- **No pointers to design documents in code.** A comment or doc comment says what the code does and why, in its own words; it never cites the SDD, a milestone or a section number.
- **Line length:** lines of code and of comments are at most 180 characters.
- **Enum names:** an enum's type name is plural (`CheckOutcomes`, `DialogueChoices`) and its members are singular (`Found`, `Primary`); a variable, parameter or property that holds
  one value is singular (`CheckOutcomes outcome`, `DialogueChoices choice`). The app's shared enums live in `Common/Constants/<Area>/` (§9); a private enum
  used by one type only stays nested in it (`FolderWalker.LinkStates`).
- **The switch convention.** A `switch` on an enum gives every member it handles its own arm, and its `default` arm throws `UnreachableException`; a `default` never stands in for a
  real member, so a new member fails loudly instead of falling into an old behaviour. An enum's `ToString()` is never user-facing text: a label comes from an explicit arm.
  The one exception is an on-disk path component: the `AuditDirections` member names in report file names and the stage names in the report's folder and file names.
- **Static services.** A service with no state is a `static class`, not an instance created and passed around (`SettingsStore`, `IndexTableReader`, `FolderWalker`, `AuditRunner`,
  `UnindexedAuditRunner`, the report builders and the path resolver).
- **`async void` event handlers and fire-and-forget tasks** wrap their whole body in `try` / `catch` and report the exception through `UnexpectedErrorHandler.ReportRecoverable`
  (§9), because nothing can catch an exception that escapes them and a discarded task's fault surfaces only at finalisation.
- **ReSharper suppressions** take one of two forms: `// ReSharper disable once <Inspection>` on the line above the member, above its doc comment and never between the doc comment
  and the signature, or a `// ReSharper disable <Inspection>` / `// ReSharper restore <Inspection>` pair around a statement. Each carries a short reason when it is not obvious.
- **Spaced attribute brackets:** `[ STAThread ]`, `[ ObservableProperty ]`, `[ RelayCommand ]`.
- **Vertical spacing:** consecutive statements of different kinds (e.g. a declaration followed by an invocation) are separated by a blank line; like statements are
  grouped. Every `if` has braces.
- **UTC internally, local at the boundary.** Timestamps are captured and carried in UTC (`Utc` suffix on names) and converted to local time only for display and the
  report.
- **Source-generated MVVM:** `[ ObservableProperty ]` on `partial` properties, `[ RelayCommand ]` on private methods; no hand-rolled INPC. **Compiled bindings:** every
  XAML file declares `x:DataType`.
- **Designer constructors:** view models expose a parameterless constructor for the XAML designer only, built from `AppSettings.Default` and concrete services created in
  place (a `DialogueService` with no owner window). No interfaces are introduced just for the designer.
- **View composition pair rule:** every public `*ViewModel` has a sibling `*View.axaml` under the parallel `Views/…` folder, and vice versa. Windows are the exception to the
  `*View` suffix: `MainWindow`, `AboutWindow`, `MessageDialogueWindow` and `FileTypesDialogueWindow` pair with `MainWindowViewModel`, `AboutWindowViewModel`,
  `MessageDialogueViewModel` and `FileTypesDialogueViewModel`. State a view model needs but that has no view of its own is not named `*ViewModel` (`ReportLocationState`,
  `RecentRate`, `RemainingEstimator`, `TimeText`, `AuditStatusBase`, `ScanFolderItem`, `FileTypeEntryItem`). The Project Settings group follows the rule: `ProjectSettingsViewModel`
  ↔ `ProjectSettingsView`.
- **User-data writes** (settings, the report) use a temp file + `File.Move(overwrite: true)`, never an in-place write.
- **Never `File.Exists` / `Directory.Exists` inside the audit check** (§6.3): they turn "couldn't check" into "missing". Pre-flight checks of the report folder may
  use them.
