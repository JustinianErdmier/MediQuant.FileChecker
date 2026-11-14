# 0. Introduction

**File Auditor** is a small Windows utility for MediQuant projects that keep documents on disk. Most such projects have a SQL Server **file index table**: one row per
document, holding the document's full path and, usually, an ID. File Auditor checks that table against the disk, and vice versa, and writes a plain-text **report** you can commit
to the project's repository as a record of the audit.

It answers two questions, one per tab:

- **Missing Files:** does every file to which the index table points actually exist on disk?
- **Unindexed Files:** is every file on disk to which you point actually in the index table?

A project can go wrong both ways: an index row whose file has gone, or a file on disk that never got indexed. Checking only one leaves the other invisible, so the app does both,
from the same inputs, in one window.

A few things to know before you start:

- **It runs from the shared file server.** File Auditor is a single program file. There is nothing to install, and your computer does not need .NET. For its exact location, see the
  table on the right side of this wiki.
- **It never changes your data.** It only reads the index table and lists folders. The only files it ever writes are the audit reports and the project settings file, both in
  the report's folder. The only folder it ever creates is a custom report folder, and only when you choose **Create Folder**.
- **It is honest about what it could not check.** A file it could not reach (a drive that is not mapped, a permission problem, a network error) is listed as *Unverified*,
  with the reason, and never silently marked as missing.
- **It looks like Windows 95.** That is deliberate: a plain, familiar layout with a menu, group boxes, keyboard shortcuts, and access keys on every control.

File Auditor replaces the old tool *File Checker*. Instead of typing a SQL query, you name the table and its columns; instead of a tab-separated "log", you get a readable detailed
report; and instead of one settings file beside the program that everyone shares, each project keeps its own settings beside its report, committed to the project's repository.

This guide is for everyone who runs an audit. The first sections walk you through the window and the two audits. Section 8 explains, briefly, how each audit is made fast and
trustworthy, for readers who want to know what happens under the hood.

# 1. Words Used in This Guide

| Word                  | What it means                                                                                                                 |
|-----------------------|-------------------------------------------------------------------------------------------------------------------------------|
| Index table           | The SQL Server table with one row per document. Also referred to as the *file index table*.                                   |
| Path column           | The column of the index table that holds each document's full path, such as `\\fs01\docs\2019\04\10443.pdf`.                  |
| ID column             | An optional column that identifies each row, such as `MqIdentity`. Used only in the Missing Files report.                     |
| Audit                 | One run of a check: a *Missing Files* audit or an *Unindexed Files* audit.                                                    |
| Report                | The plain-text file an audit writes when it finishes, stops, or fails part-way.                                               |
| Project               | The set of inputs for one DMS project: its server, database, table, columns, folders, and report location.                    |
| Project settings file | `FileAuditorSettings.json`, the file that stores a project's inputs. It is saved beside the report.                           |
| Project folder        | The project's folder name, such as `D12345`. It builds the report location when you do not use a custom one.                  |
| Project stage         | *Sample* or *Final*: which stage of the project you are auditing. It decides the report's folder.                             |
| Audit stage           | *Interim* or *Final*: whether this is an interim check or the final audit. It decides the report's file name.                 |
| Built location        | The report location the app works out from the project folder and stages: `U:\Projects\<Project folder>\DE\<Project stage>\`. |
| Custom location       | A report folder and file name you type yourself instead.                                                                      |
| Found                 | The file exists (and, if you asked, is not empty).                                                                            |
| Missing               | The file definitely is not there, or it is empty and you asked for empty files to count as missing.                           |
| Unverified            | The file, or a folder, could not be checked, for a reason the report records. It might be there; the app could not tell.      |
| Skipped               | An index row marked `SKIP` or `SKIPPED` instead of a path. It is counted but never checked or listed.                         |
| Duplicate             | Another index row for a path already counted. The file is checked once, not again for each row.                               |
| Indexed / Unindexed   | In the Unindexed Files audit: a file on disk whose path is / is not in the index table.                                       |
| Root                  | The start of a path: a drive letter such as `U:\`, or a server share such as `\\fs01\docs`.                                   |
| Link                  | A junction or symbolic link: a folder or file that points somewhere else. The Unindexed Files audit never follows one.        |
| File type entry       | One item in the *File types* or *Exclude* list, such as `.pdf` or `.zip.*`.                                                   |
| Glob pattern          | A pattern that uses `*` (any run of characters) and `?` (exactly one character) to match many file types at once.             |

# 2. Quick Start

**Before you start,** you need the name of the SQL Server and database that hold the project's index table, the table's name, and the name of its path column.

## 2.1 Check for Missing Files

1. Start **File Auditor**.
2. In **Database**, type the **Server** and **Database**.
3. In **File Index Table**, type the **Table** (for example `_DMS_FileIndex`, or `dbo._DMS_FileIndex` with its schema) and the **Path column**. Type an **ID column**
   too if the table has one; the report then shows each row's ID beside its path. Names can be typed as they are or in square brackets, such as `[dbo].[My Table]`.
4. Stay on the **Missing Files** tab.
5. In **Report**, type the **Project folder** (for example `D12345`) and choose the **Project stage** and **Audit stage**. Check that the **Location** shown is the project's
   folder. If you want the report somewhere else, tick **Use a custom report location and file name** and fill in the location and name instead.
6. Press **Start** (or F5). Answer any questions the app asks before it begins.
7. Watch the **Audit** group. When the audit finishes, a dialogue tells you so; press **View Report** in it (or Ctrl+R later, once the status bar reads `Completed`) to open the
   report.

## 2.2 Check for Unindexed Files

1. Fill in **Database** and **File Index Table** as above. _The **ID column** is not used by this audit._
2. Select the **Unindexed Files** tab.
3. In **Folders to Check**, press **Add...** and choose a folder. Repeat for each folder you want checked. Untick **Include subfolders** on a folder if only the files directly
   in it should be checked.
4. Check the **File types** list. The built-in list covers 15 common document and image formats (sourced from the Document MIME Types for the Documents Module wiki article). Press
   **Edit...** to change it, and use **Exclude** for types you never want checked.
5. Fill in **Report** as above (its values are shared with the Missing Files tab).
6. Press **Start**. When the audit finishes, a dialogue tells you so; press **View Report** in it to open the report.

**Your settings are saved for you.** With **Save project settings** ticked (it is, by default), the app saves the project's inputs as `FileAuditorSettings.json` in the report's
folder when you start an audit. Next time, use **File > Open project...** to load them again.

# 3. A Tour of the Main Window

The window has a fixed size. From top to bottom it holds:

- the **menu bar** (*File* and *Help*);
- the **Database** and **File Index Table** groups, side by side, used by both audits;
- the **Project Settings** group;
- the two **audit tabs**, *Missing Files* and *Unindexed Files*, each with its own options, and a semi-shared **Report** group and an **Audit** group;
- the **status bar**, which shows what the app is doing and, for the Missing Files audit, how far it has got.

Every label, field, button, and menu item has an **access key**: hold Alt and press the underlined letter to jump to it. A few controls on the Unindexed Files tab have none,
because every letter was already taken.

**Menus and shortcuts**

| Menu item                 | Shortcut | What it does                                                                          |
|---------------------------|----------|---------------------------------------------------------------------------------------|
| File > Open project...    |          | Loads a project's settings file (section 6).                                          |
| File > Close project      |          | Puts every input back to its default, as if you had just started the app (section 6). |
| File > Start              | F5       | Starts the audit on the selected tab.                                                 |
| File > Stop               | Esc      | Stops the running audit, whichever tab is showing.                                    |
| File > View Report        | Ctrl+R   | Opens the selected tab's report in your default program for `.txt` files.             |
| File > Exit               | Alt+F4   | Closes the app.                                                                       |
| Help > Help Topics        | F1       | Opens this guide in your web browser.                                                 |
| Help > About File Auditor |          | Shows the app's version.                                                              |

**The status bar** reads `Ready` when the selected tab can start, `Enter the required details` when something it needs is still empty, and, whilst an audit runs, its phase:
`Counting files...`, `Loading the index...`, `Checking files...` or `Writing report...`. When a run ends it reads `Completed`, `Stopped`, or `Failed`, and that result stays on its
tab until the next run. Whilst an audit runs, the status bar follows it even if you switch to the other tab.

## 3.1 SQL Server Connection

The **Database** group holds the **Server** and **Database** names. Both are required.

The **File Index Table** group holds:

- **Table:** required. Type the table's name, or `schema.table` if it is not in the default schema.
- **Path column:** required. The column holding each document's full path. It may be an **`nvarchar`** (Unicode text) or a **`varchar`** column, of any length, including the `max`
  forms. The app reads a `varchar` column as it is, but it warns you first, because `nvarchar` is the better choice (see section 8.4). Any other type, such as `char`, `nchar` or
  `text`, is refused when you press Start, with a message that names the column and its type (see section 9). Both audits apply this rule. If your path column is of another type,
  ask for it to be converted to `nvarchar`, or use a view of the table that casts it.
- **ID column:** optional. When it is filled in, the Missing Files report shows each row's ID beside its path. The Unindexed Files audit does not use it (a file that is not in
  the table has no ID), but it is still saved with the project. The ID must be **unique to its file**: one ID per path, such as a document ID. Do not use a patient or account
  ID that several files share or that repeats across a file's rows (see §4.1); the app does not check this.

You never type SQL. The app builds the query from these names and quotes each one safely, so a name with spaces or unusual characters is fine.

The app always connects with your Windows account and trusts the server's certificate. If you cannot connect, see section 9.

## 3.2 Project Settings

The **Project Settings** group holds one checkbox, **Save project settings**, and a short message saying where the settings are being saved.

- **Ticked** (the default): the project's inputs are saved automatically at four moments: when you start an audit, open a project, close a project, or close the app. They are
  saved only if something has changed since the last save; editing a field never saves on its own.
- **Unticked:** the app asks you whether to save instead. If you answer *No*, it stops asking until you close the project or restart the app.

When you untick it, the app warns you once and asks you to confirm. Section 6 explains saving, opening and closing in full, and section 7 describes the settings file itself.

## 3.3 Audit Tabs

There is one tab per audit:

- **Missing Files** holds an **Options** group (the empty-files option), the **Report** group, and the **Audit** group.
- **Unindexed Files** holds the **Folders to Check** group, the **Report** group and its own **Audit** group.

The **Audit** group at the bottom of each tab shows statistics for that tab, a progress bar, and three buttons: **View Report** on the left, **Start** and **Stop** on the right.
Each tab keeps the statistics of its own last run.

**Start**, **View Report**, and the status bar's idle message follow the **selected** tab. **Stop** follows the **running** audit: each tab's Stop button is enabled only on the
tab whose audit is running, whilst *File > Stop* and Esc stop it from either tab.

**Only one audit runs at a time.** Whilst either audit runs, both Start buttons are disabled and every input is locked, so nothing can change during a running audit. You can still
switch tabs, stop the audit, open the About window, and open this guide. *View Report* is disabled until the audit ends.

## 3.4 Audit Report Configuration

Both tabs show the same **Report** group, and its options are **shared**: change the project folder or either of the stages on one tab and the other tab shows the change. Only the
**File name** belongs to each tab, and the Unindexed Files tab adds one option of its own, **Group file types by** (section 5.5).

- **Use a custom report location and file name:** unticked by default. It decides whether the report location is built for you or typed by you.
- **Project folder:** the project's folder name, such as `D12345`. It must be a single folder name: no `\` or `/`, not a full path.
- **Project Stage:** *Sample* or *Final* (default *Final*).
- **Audit Stage:** *Interim* or *Final* (default *Interim*). Whilst the project stage is *Sample*, the audit stage is always *Interim* and cannot be changed.
- **Location** and **File name:** where the report goes.

**Built location (the box unticked).** Location and File name are worked out for you and shown read-only:

| Tab             | Location                                           | File name                                      |
|-----------------|----------------------------------------------------|------------------------------------------------|
| Missing Files   | `U:\Projects\<Project folder>\DE\<Project stage>\` | `FileAuditorMissingReport_<Audit stage>.txt`   |
| Unindexed Files | `U:\Projects\<Project folder>\DE\<Project stage>\` | `FileAuditorUnindexedReport_<Audit stage>.txt` |

For example, project folder `D12345`, project stage *Final* and audit stage *Interim* give `U:\Projects\D12345\DE\Final\FileAuditorMissingReport_Interim.txt`. Until the
project folder is filled in, the location shows the placeholder `<Project Folder>` and Start stays disabled.

**Custom location (the box ticked).** The project folder and stage options stay visible but are disabled. **Location** becomes a folder you type or choose with **Browse...**,
and **File name** becomes editable, starting at `FileAuditorMissingReport.txt` on the Missing Files tab and `FileAuditorUnindexedReport.txt` on the Unindexed Files tab. Each tab
keeps its own custom file name, and the two names must differ, because both reports go to the same folder.

**When you press Start**, before anything touches the database, the app checks the report location:

- A built location whose folder does not exist is an error: the folder has to be created first (the app does not create project folders for you). This is because the intention is
  to conform to the established SVN conventions for the team, which requires an empty project folder to be manually created by the engineer first when they do their first checkout.
  In other words, if following that convention, the folder should always be created first by virtue of the process, and treating it as an error helps protect you from accidentally
  saving the report and settings to the wrong project.
- A custom location whose folder does not exist offers **Create Folder** or **Cancel**.
- A folder to which the app cannot write is an error.
- A report that already exists offers **Overwrite** or **Cancel**. One that is read-only is an error.

These checks ensure that you never find out _after_ a long run that the report could not be written.

# 4. Missing Files Audit

## 4.1 Overview

The Missing Files audit reads every row of the index table and checks that the file at its path exists on disk. Many index tables are built around patients rather than files, so
one path can appear on many rows; the app checks each distinct path once, so each file is checked and counted once. With an ID column set, it treats each distinct path and ID pair
as one, which is why the ID must be unique to its file: a path listed with three different IDs is checked three times. Letter case and trailing spaces do not make two paths
distinct; any other difference, including a leading space, does.

As it reads the table, the app sorts every row into one of four kinds, and counts them as it goes, so you can see where every row went:

| Kind       | What it is                                                                                                                                       |
|------------|--------------------------------------------------------------------------------------------------------------------------------------------------|
| Distinct   | A real path, the first row to carry it (or the first row with that path and ID, when an ID column is set). These are the files that get checked. |
| Duplicate  | A further row for a path (or path and ID) already seen. Not checked again.                                                                       |
| Empty path | The path is `NULL`, empty, or only whitespace. Never checked; counted, and listed by ID when an ID column is set (see *Path is empty*, below).   |
| Skipped    | The path is a skip marker (below). Never checked and never listed.                                                                               |

Every row of the table is exactly one of these, so **distinct + duplicates + empty paths + skipped = the table's rows**. Each distinct path then lands in exactly one outcome:

| Outcome                                       | When                                                                                           |
|-----------------------------------------------|------------------------------------------------------------------------------------------------|
| Found                                         | The file exists (and, with the empty-files option on, is not empty).                           |
| Missing — Empty                               | The empty-files option is on, and the file exists but is zero bytes long.                      |
| Missing — Does not exist                      | The path's drive or share exists, but the file, or a folder on the way to it, does not.        |
| Unverified — Drive or share not found         | The drive letter or `\\server\share` at the start of the path does not exist for you.          |
| Unverified — Permission denied                | You are not allowed to see the file or its folder.                                             |
| Unverified — Network location unreachable     | The server or share cannot be reached over the network.                                        |
| Unverified — Path too long                    | The path is longer than Windows can handle.                                                    |
| Unverified — Invalid characters in path       | The path is malformed or contains characters Windows does not allow.                           |
| Unverified — Path is empty in the index table | The row's path value is `NULL`, empty, or only whitespaces. Counted and listed, never checked. |
| Unverified — Other errors                     | Any other failure; the report gives the reason and the system's message for each file.         |
| Skipped                                       | The row's path value is a skip marker (below). It _is_ counted but **not** checked or listed.  |

The important difference is between **Missing** and **Unverified**. *Missing* means the app is certain the file is not there. *Unverified* means it could not tell: if your
`U:` drive is not mapped, for example, every file on `U:` is *Drive or share not found*, not *Missing*, so a mapping problem can never flood the Missing list.

**Skipped rows.** Some index rows carry a marker instead of a path, written by hand or by the tool that builds the index table. A row is skipped when its value, ignoring spaces
around it and letter case, is `SKIP` or `SKIPPED`, or starts with `SKIP:` or `SKIPPED:` followed by anything (`SKIPPED: preflight`, `skip:duplicate`). Nothing else is a recognised
skip marker: not `SKIP preflight` (no colon), not `SKIPPEDX`, and not a real path that happens to contain the word, such as `C:\SKIPPED\a.txt`. A skipped row is never checked and
never listed in the report, but it is counted, so the numbers still add up. Skipped rows are counted as they are read, so *Skipped* grows during the run.

## 4.2 Empty Files Option

The **Options** group holds **Report empty (zero-byte) files as missing**, ticked by default. With it ticked, a file that exists but is zero bytes long is reported as *Missing —
Empty*, because an empty document is usually as good as a missing one. Untick it to count empty files as found.

The option belongs to the Missing Files audit only. The report's header says which way it was set (`Empty files: Reported as missing` or `Not reported`).

## 4.3 The Audit

Press **Start**. The app runs its pre-flight checks (section 3.4), saves the project's settings if they need saving (section 6), then:

1. **Counts the rows** (status `Counting files...`): the table's row count, which is *Total files*. This takes a moment, however large the table.
2. **Reads the rows and checks the files** (status `Checking files...`). The rows are read straight away, in the table's order, and each is sorted into its kind as it arrives. Only
   the distinct paths are checked, many at a time; duplicate, empty and skipped rows never reach this step. Every statistic except *Total files* is live, so you see the table being
   worked through.
3. **Writes the report** (status `Writing report...`).

**The statistics**

| Statistic    | Meaning                                                                                                                                                      |
|--------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Total files  | The table's rows. Known the moment counting ends, and fixed for the run.                                                                                     |
| Distinct     | The different real paths seen so far, each counted when it is first read. Each is then checked, so Checked trails it a little.                               |
| Duplicates   | Rows that repeat a path (or path and ID) already seen, such as a file listed once per patient. A path listed three times is one Distinct and two Duplicates. |
| Checked      | Files checked so far: Found + Missing + Unverified. Skipped, empty-path and duplicate rows are not included.                                                 |
| Found        | Files that exist (and, with the empty-files option on, are not empty) so far.                                                                                |
| Missing      | Missing files found so far.                                                                                                                                  |
| Unverified   | Files that could not be checked so far. When some rows have an empty path, their count so far follows in brackets: `120 (310)`.                              |
| Skipped      | Rows passed over because they hold a skip marker, counted as they are read.                                                                                  |
| Elapsed      | Time since you pressed Start, as `hh:mm:ss`.                                                                                                                 |
| Remaining    | Estimated time left, from how fast rows are being handled. It reads `Calculating...` whilst the rows are counted and for the first 5 seconds of checking.    |
| Files/second | The run's average speed in files checked, then, whilst the run is going, the recent speed in brackets, such as `54,112 (60,001)`.                            |

**How the numbers add up.** Two sums always hold once a run completes:

- **Found + Missing + Unverified = Checked = Distinct.** (The *Unverified* here is the number before the brackets, so it leaves out the empty-path rows.)
- **Distinct + Duplicates + the empty paths (the bracket on Unverified) + Skipped = Total files.**

Total files is known the moment counting ends and does not change during the run. Distinct, Duplicates, Skipped and the bracketed count grow as the rows are read, and Found,
Missing, Unverified and Checked as the files are checked, so the sums hold once the run completes; until then Checked trails Distinct a little. If you stop or the run fails, the
report adds a *Not checked* line for the rows the audit did not finish with (those it never read, and the distinct paths it read but did not check), so Checked + Duplicates + the
empty paths + Skipped + Not checked = Total files. The app assumes the index table does not change whilst an audit runs; if rows are added or removed meanwhile, the sums may not
hold.

The progress bar shows the share of Total files handled: a duplicate, empty or skipped row is handled the moment it is read, and a distinct path when its check ends. Remaining and
the status bar's percentage use the same count, so they stay accurate while a run of duplicates is read. If the table has no rows, the run still completes and the bar reads 100%.

**About Remaining.** The estimate follows the speed of roughly the last 30 seconds of rows handled, not the whole run, so it reacts when the network speeds up or slows down.
Between updates, it counts down one second per second and glides to each new estimate over a few seconds, so it never jumps about. If the work stalls (a slow share, say), it holds
still rather than running down to zero. At the end it reads `00:00:00`.

**About Files/second.** The first number counts *checked* files over the whole elapsed time: how fast this run has been. The number in brackets is the *recent* speed of checked
files: how fast it is going now. Remaining has its own recent speed, over rows handled, so a long run of duplicates changes Remaining but not Files/second. When the run ends, only
the first number stays.

**Stopping.** Press **Stop** (or Esc). The app stops reading rows and checking files and writes the report with `Result: Stopped by user`, listing what it checked so far, with a
*Not checked* count for the rest and the IDs of the empty-path rows it had read. If you stop before the row count has come back (a moment at the start), nothing has been checked,
so no report is written and the status bar reads `Stopped`.

**If the database fails.** A connection problem, a wrong table or column name, or a timeout shows an error. If no row had been handled yet, no report is written. If it fails
part-way, the report is written with `Result: Failed —` and the message, the counts so far, and the IDs of the empty-path rows read so far.

**When the audit ends.** A completed audit opens a dialogue that says *The Missing Files audit has finished.*, gives the final *Missing* and *Unverified* figures as the Audit group
shows them, and gives the full path of the report. Press **View Report** (or Alt+V) to open the report, or **OK** to close the dialogue. Windows plays its information sound,
and if you are working in another program the app's taskbar button also flashes until you switch back to it; the app never jumps in front of what you are doing. A failed audit
shows its error in the same way, with the error sound instead. If you stopped the audit yourself, nothing new appears, even when the run had already finished checking: you are
already at the window.

**Closing the window during an audit** asks *"An audit is in progress. Stop it and exit?"*. *Yes* stops the audit, writes the report, and closes the app; *No* carries on.

## 4.4 The Audit Report

The report is a plain-text file named, by default, `FileAuditorMissingReport_<Audit stage>.txt`. It is written the same way every time (files sorted by path, numbers in a fixed
format), so two reports of the same project compare cleanly in version control. An example:

```text
File Auditor Missing Report
===========================

Server:          KDCSQL15
Database:        Interim_AdventHealth_DocStorage_12345
Table:           _DMS_FileIndex
Path column:     MQ_FullPath
ID column:       MQ_DocID
Empty files:     Reported as missing
Project folder:  D12345
Project stage:   Final
Audit stage:     Interim

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
MQ_DocID 10871   \\fs01\docs\2019\05\10871.tif
...

[Does not exist (200)]
MQ_DocID 10443   \\fs01\docs\2019\04\10443.pdf
MQ_DocID 10502   \\fs01\docs\2019\04\10502.pdf
...

Unverified Files (430)
----------------------
[Permission denied (100)]
MQ_DocID 22010   \\fs02\restricted\22010.pdf
...

[Path is empty in the index table (310)]
MQ_DocID 22011
MQ_DocID 22012
...

[Other errors (20)]
MQ_DocID 22030   \\fs03\docs\22030.pdf   I/O error   The semaphore timeout period has expired.
...
```

How to read it:

- **The header** records exactly what was audited and how: the connection, the table and columns, the empty-files option and, for a built location, the project folder and
  stages. `ID column:` reads `(none)` when no ID column was set.
- **The result** is `Completed`, `Stopped by user`, or `Failed —` followed by the error.
- **The summary** always adds up, in the two ways of section 4.3: Found + Missing + Unverified = Distinct, and Distinct + Duplicates + the empty paths (the bracket on
  `Unverified:`) + Skipped = Total files. *Total files* is the table's rows; `Distinct:`, `Duplicates:` and `Skipped:` are always shown. A stopped or failed run adds a `Not
  checked:` line, after `Skipped:`, for the rows it did not finish with, so Found + Missing + Unverified + Duplicates + the empty paths + Skipped + Not checked = Total files; a
  completed run has no such line.
- **The Unverified Files count** (`Unverified Files (430)` above) includes the empty-path rows, since they are listed there: 120 files that could not be checked, plus 310 rows with
  no path.
- **Missing Files** and **Unverified Files** list each file under its reason, in square brackets with a count. Grouping by reason makes it easy to act on many files at once:
  a whole group of *Drive or share not found*, for example, usually means one unmapped drive. A group with no files is left out, and a section with none reads
  `No missing files.` or `No unverified files.`
- **Each file line** is the ID column's name and value, then the path, when an ID column is set; otherwise just the path. *Other errors* lines add the reason and the system's
  message.
- **Skipped rows are not listed**, only counted.

**The "Path is empty" group.** A row whose path is empty has no path to print, so the group *[Path is empty in the index table]* lists the row's ID instead, one line per row, so
the group's count matches its lines. That is why it can be long. It needs an ID column, and a stopped or failed run lists the IDs it had read so far. Without an ID column, the
group keeps its count but holds a single line in place of the IDs:

- `Missing filepaths not listed because no ID column was specified`: set an ID column and run the audit again to see which rows they are.

# 5. Unindexed Files Audit

## 5.1 Overview

The Unindexed Files audit works the other way round. It walks the folders you choose, and for every file of a type about which you care, it asks whether its full path is in the
index table's path column. A file whose path is not there is **unindexed**.

A path is **indexed** when it matches a value in the path column exactly, apart from letter case and any spaces around the value. The paths must be spelled the same way as on
disk: if the table holds `\\fs01\docs\...` but you add the folder as the mapped drive `U:\docs`, every file will look unindexed (see section 9).

The audit never opens a file, so it cannot fail on a single file. What it can fail on is a **folder** it cannot list (no permission, an unreachable share), or a **link**,
which it deliberately does not follow. Both are reported as *Unverified*.

## 5.2 Input Folders

The **Folders to Check** group lists the folders to walk, one per row:

- **Add...** opens a folder picker and adds the folder you choose. A folder already in the list is not added twice.
- **Remove** removes the selected folder.
- **Include subfolders** (ticked for a newly added folder) decides whether the folders beneath it are checked too. Untick it to check only the files directly in that folder.

Some details worth knowing:

- **Hidden and system files and folders are skipped,** and a skipped folder's contents go with it.
- **Nested folders are fine.** If you add `A` (with subfolders) and also `A\B`, each file is checked and reported once, under the deepest folder that covers it.
- **Links are not followed.** A junction or symbolic link (a folder or file that points somewhere else) is reported under *Unverified — Link not followed* and nothing beneath
  it is checked. If you need its contents checked, add the folder it points to as an input folder of its own. Other special folders, such as OneDrive placeholders or DFS
  folders, are treated as ordinary folders.

When you press Start, the app checks that there is at least one folder, that each one exists and is a full path, and that no folder is listed twice.

## 5.3 File Types & Exclusions

Two lists decide which files are checked:

- **File types** is the list of types to check. The built-in list holds the 15 corresponding extensions to the allowed mime types for the document module:
  `.bmp .doc .docm .docx .dotm .dotx .gif .html .jpeg .jpg .pdf .png .rtf .tiff .txt`.
- **Exclude** is the list of types never to check, empty by default. **Exclude always wins:** a file that matches both lists is not checked.

A file is checked only when it matches an entry in *File types* and no entry in *Exclude*.

**Editing the lists.** Each list has an **Edit...** button that opens a small editor, one entry per row:

| Button | What it does                                                                                |
|--------|---------------------------------------------------------------------------------------------|
| New    | Adds an empty row for a new entry.                                                          |
| Edit   | Lets you change the selected entry.                                                         |
| Delete | Removes the selected entry.                                                                 |
| Clear  | Removes every entry.                                                                        |
| Reset  | Puts back the built-in list (*File types* only).                                            |
| OK     | Checks every entry and saves the list; an invalid entry is named and the editor stays open. |
| Cancel | Throws away every change, including a Reset.                                                |

**Note:** Deleting, clearing, and reset do **not** ask for confirmation, and once you press OK they cannot be undone, with one exception: if the project's settings were saved
before the change, and nothing has been saved since, you can go back to the saved settings. Untick *Save project settings* and confirm the warning, choose *File > Close project*
and answer **No** when asked whether to save, then open the project again with *File > Open project...*. Settings are saved when you press Start, open or close a project, or close
the app, so this works only if none of those has saved the change already. The checkbox is ticked again automatically when the project opens.

**Writing an entry.** An entry is a file extension, with or without its leading dot: `pdf` and `.pdf` are the same, and so is `*.pdf`. Entries ignore letter case. An entry can
have several parts, such as `.tar.gz` or `.zip.001`, and can use the wildcards `*` (any run of characters) and `?` (exactly one character), such as `.tif*` or `.zip.*`.
Section 11 has a full cheat sheet with examples.

When a file matches several entries, it belongs to the **most specific** one: the entry with the most parts, then the fewest wildcards, then the first alphabetically.

**Checks when you press Start.** *File types* must have at least one entry, and *Exclude* must not hold an entry that matches every file (such as `*`), since that would exclude
everything. Three situations ask you to confirm with **Continue** or **Cancel**:

1. *File types* has an entry that matches every file **and** other entries, which are then redundant.
2. *File types* has an entry that matches every file **and** *Exclude* is not empty, so every file will be checked except the excluded types.
3. The same entry is in both lists, so that type will not be checked, because *Exclude* always wins.

Once you answer *Continue*, the same question is not asked again for the remainder of this session, i.e. until you close the project, open another one, or restart the app.

## 5.4 The Audit

Press **Start**. After the pre-flight checks and the save rule, the app:

1. **Loads the index** (status `Loading the index...`): it reads the path column, keeping only the rows under your folders where it can (section 8). On a very large table
   this can take a few minutes.
2. **Walks the folders** (status `Checking files...`), checking each matching file the moment it is found.
3. **Writes the report.**

**The statistics**

| Statistic    | Meaning                                                                                                                         |
|--------------|---------------------------------------------------------------------------------------------------------------------------------|
| Total files  | Matching files found so far. Every one is checked at once, so Total files = Indexed + Unindexed.                                |
| Excluded     | Files found that the file types skip: no File types entry matches them, or an Exclude entry does. Not part of Total files.      |
| Index paths  | The distinct paths loaded from the index to compare against. Counts up whilst the index loads.                                  |
| Indexed      | Files whose path is in the index table.                                                                                         |
| Unindexed    | Files whose path is not in the index table.                                                                                     |
| Unverified   | Folders that could not be listed, links not followed, and entries whose link status could not be read. Not part of Total files. |
| Elapsed      | Time since you pressed Start.                                                                                                   |
| Files/second | The run's average, then, once the walk has run for 5 seconds, the recent speed in brackets.                                     |

*Index paths* is the paths the app loaded to compare against, not the rows of the whole table: it is the rows under your folders, plus any whose path starts with a space or
tab, or the whole table when you have more than 20 folders (section 8). A file without an extension (such as `README`) matches no file type, so it counts as *Excluded*;
hidden and system files are skipped before that and are in no count.

There is **no Remaining** and **no percentage**: the app cannot know how many files there are until it has walked every folder. Instead, the progress bar shows a marquee from left
to right whilst the audit runs, and fills up when it ends.

**Stopping.** Press **Stop** (or Esc). If the index is still loading, no report is written (the screen keeps the Index paths count it had reached). Otherwise, the app stops
listing folders and writes the report with `Result: Stopped by user`, listing what it found and checked so far.

**If the database fails** whilst the index is loading, an error is shown and no report is written, since nothing was walked; the screen keeps the Index paths count it had reached.

**When the audit ends.** A completed audit opens a dialogue that says *The Unindexed Files audit has finished.*, gives the final *Unindexed* and *Unverified* figures as the Audit
group shows them, and gives the full path of the report. Press **View Report** (or Alt+V) to open the report, or **OK** to close the dialogue. Windows plays its information
sound, and if you are working in another program the app's taskbar button also flashes until you switch back to it; the app never jumps in front of what you are doing. A failed
audit shows its error in the same way, with the error sound instead. If you stopped the audit yourself, nothing new appears: you are already at the window.

## 5.5 The Audit Report

The report is named, by default, `FileAuditorUnindexedReport_<Audit stage>.txt`. An example:

```text
File Auditor Unindexed Report
=============================

Server:          KDCSQL15
Database:        Interim_AdventHealth_DocStorage_12345
Table:           _DMS_FileIndex
Path column:     MQ_FullPath
Folders:         \\kdcdms2\crm12345\Images (with subfolders)
                 \\kdcdms1\crm12345\Scans
File types:      .doc .docx .jpg .pdf .png .tif .tiff
Exclude:         .zip .zip.*
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
[\\kdcdms2\crm12345\Images (17)]

  [.pdf (2)]
  \\kdcdms2\crm12345\Images\2019\05\10871.pdf
  \\kdcdms2\crm12345\Images\2019\06\11002.pdf

  [.tif (15)]
  \\kdcdms2\crm12345\Images\2019\04\10443.tif
  ...

[\\kdcdms1\crm12345\Scans (3)]

  [.jpg (3)]
  \\kdcdms1\crm12345\Scans\IMG_0041.jpg
  ...

Unverified (3)
--------------
[Permission denied (2)]
\\kdcdms2\crm12345\Images\Restricted\
\\kdcdms2\crm12345\Images\Restricted2\

[Other errors (1)]
\\kdcdms2\crm12345\Images\2020\   I/O error   The semaphore timeout period has expired.
```

How to read it:

- **The header** lists each input folder (with `(with subfolders)` where ticked), both type lists, the grouping choice, and, for a built location, the project folder and stages.
  There is no ID column line.
- **Unindexed Files** are grouped first by **input folder**, in the order you listed them, then by **file type**, then sorted by path.
- **Group file types by** (in the Report group on this tab) chooses the file-type groups:
    - *Listed type* (the default): one group per entry in your *File types* list, such as `[.zip.* (12)]`.
    - *Found extension*: one group per real extension found, such as `[.zip.001 (5)]` and `[.zip.002 (7)]`. Useful when one wildcard entry covers many extensions.
- **Unverified** lists folders that could not be listed and links that were not followed, each only once, grouped by reason. A folder's path ends in `\`. Nothing beneath an
  unverified folder was checked, so its files are not in the counts. A section with nothing in it reads `No unindexed files.` or `No unverified folders or links.`

# 6. Saving, Closing, & Opening Project Settings

A project's inputs (server, database, table, columns, options, report location, folders, and file types) are saved in a **project settings file**,
`FileAuditorSettings.json`, in the **report's folder**. That way the settings live in the project's repository beside the report, and any teammate can open the same project from
VCS.

**When the app saves.** The app checks whether to save at four moments: when you press **Start**, when you **close the window**, and when you choose **Close project** or **Open
project...**. At each of them:

1. If nothing has changed since the project was opened or last saved, nothing happens.
2. If **Save project settings** is ticked, the settings are saved without asking.
3. If it is unticked, the app asks *"Do you want to save this project's settings?"*. *Yes* saves and ticks the box again; *No* does not save, and then it does not ask again
   until you close the project or restart the app.

**Where it saves.**

- A project you have never saved is saved in the report's folder. The report location must be complete for this (a project folder for a built location, or a custom location).
- A project you opened, or saved earlier, is saved back to the same file, without asking.
- If the report's folder has since changed (for example, you switched the project stage from *Sample* to *Final*), the app asks where to save: **Report Folder**, **Original
  File**, or **Cancel**.
- If a different `FileAuditorSettings.json` is already in the target folder, the app asks before overwriting it: **Overwrite**, **Skip Save**, or **Cancel**.
- If the settings cannot be saved (the folder does not exist, or it cannot be written to), the app says why and offers **Skip Save** or **Cancel**. It never creates a folder
  just to save settings.

Because the file sits beside the report, a built location gives each project stage its own settings file: *Sample* and *Final* are different folders, and could potentially use
different databases.

**Cancel backs out.** Choosing *Cancel* at any of these questions cancels what you were doing: the audit does not start, the window stays open, or the current project stays
open and unchanged.

**Opening a project.** Choose **File > Open project...**. The app first saves the current project if needed, then shows a file picker, starting in the report's folder when it
exists. Pick a `FileAuditorSettings.json`. The project is loaded, and that file becomes the project's file. If the file cannot be read, or is not a File Auditor project, the
app says so and leaves the current project as it was.

**Closing a project.** Choose **File > Close project**. The app saves if needed, then puts every input back to its default, ticks *Save project settings* and clears both tabs'
statistics, exactly as if you had restarted the app.

**Starting the app** always opens with every input at its default and no project open; it never opens the last project by itself.

# 7. Project Settings

This section describes the project settings file for readers who want to look inside it, or fix it by hand.

The file is ordinary JSON, written with indentation so it reads well in a repository. A typical file:

```json
{
  "Server": "KDCSQL15",
  "Database": "Interim_AdventHealth_DocStorage_12345",
  "Table": "_DMS_FileIndex",
  "PathColumn": "MQ_FullPath",
  "IdColumn": "MQ_DocID",
  "ReportEmptyFiles": true,
  "ProjectFolder": "D12345",
  "ProjectStage": "Final",
  "AuditStage": "Interim",
  "UseCustomReportLocation": false,
  "CustomLocation": "",
  "MissingCustomFileName": "FileAuditorMissingReport.txt",
  "ScanFolders": [
    {
      "Path": "U:\\Projects\\D12345\\Images",
      "IncludeSubfolders": true
    }
  ],
  "FileTypes": [ ".bmp", ".doc", ".docx", ".pdf", ".tif" ],
  "ExcludeTypes": [],
  "GroupByFoundExtension": false,
  "UnindexedCustomFileName": "FileAuditorUnindexedReport.txt"
}
```

| Key                               | What it holds                                                                         |
|-----------------------------------|---------------------------------------------------------------------------------------|
| `Server`, `Database`              | The SQL Server connection.                                                            |
| `Table`, `PathColumn`, `IdColumn` | The index table and its columns. `IdColumn` may be empty.                             |
| `ReportEmptyFiles`                | The Missing Files tab's empty-files option.                                           |
| `ProjectFolder`                   | The project folder name.                                                              |
| `ProjectStage`                    | `Sample` or `Final`.                                                                  |
| `AuditStage`                      | `Interim` or `Final`.                                                                 |
| `UseCustomReportLocation`         | Whether a custom report location is used.                                             |
| `CustomLocation`                  | The custom report folder.                                                             |
| `MissingCustomFileName`           | The Missing Files report's custom file name.                                          |
| `ScanFolders`                     | The Unindexed Files tab's folders, each with its `Path` and `IncludeSubfolders` flag. |
| `FileTypes`                       | The *File types* list.                                                                |
| `ExcludeTypes`                    | The *Exclude* list.                                                                   |
| `GroupByFoundExtension`           | `true` for *Found extension* grouping, `false` for *Listed type*.                     |
| `UnindexedCustomFileName`         | The Unindexed Files report's custom file name.                                        |

Things the file does **not** hold:

- **Not the *Save project settings* checkbox.** It is ticked every time you start the app, open a project, or close one.
- **Not your answers to the app's questions** (the *Continue* answers, or a *No* to saving). They last only for the session and are forgotten when you close the project or restart
  the app.
- **Not the selected tab, or the window's position.**

**Editing it by hand** is safe. A key that is missing, or set to `null`, takes its default; a value that makes no sense (an unknown number for a stage, say) takes its default
too; the rest of the file still loads. A folder with no path is dropped. An invalid file type entry is kept as written, so you can see it on the tab, and Start names it in an
error until you fix it. A file that is not a File Auditor project at all (any other JSON, or `{}`) is refused when you open it.

# 8. Key Optimisations & Design Choices

This section briefly explains how each audit stays fast on large projects and trustworthy in what it reports. You do not need any of it to use the app.

## 8.1 Both Audits

- **The report is a durable record, so it is deterministic.** Files are sorted by path, numbers and dates use a fixed format, and line endings are always Windows-style. Running
  the same audit twice gives the same report apart from the times, so changes show up cleanly in version control.
- **"Could not check" is never "missing" or "unindexed".** Every failure is mapped to a named *Unverified* reason instead. The old tool used a simple existence check that
  answered "no" for every kind of failure, so an unreachable share looked like N number of missing files. File Auditor asks Windows in a way that reports *why* a file could not
  be read.
- **Problems are caught before the long part.** The inputs, the report folder, and the report file are all checked when you press Start, before the audit reads the table (the only
  database call is a quick look at the path column's type, section 8.4), so a long audit cannot end with "the report could not be written".
- **No handwritten SQL.** The app builds the query from the names you give it and quotes every name, so a typing mistake cannot change what the query does.
- **Settings belong to the project, not to the computer.** The program runs from a shared server, so a settings file beside it would be shared by everyone. Keeping the settings
  beside each project's report means they travel with the project and never leak from one project into another.
- **Safe writes.** The report and the settings file are first written to a temporary file and then moved into place, so a crash or a full disk never leaves a half-written file.
  Nothing is ever written beside the program or in your user profile.
- **Responsive whilst it works.** Audits run in the background, the window refreshes its statistics four times a second, and slow checks such as "does this folder exist" never
  freeze the window.
- **No crashes.** An unexpected error the app can survive is shown in a message, and the app carries on. One that it cannot survive is explained in a message before the app closes.

## 8.2 Missing Files Audit

- **Count, then stream.** The rows are counted first, which takes a moment, and are then read as a stream from the first row, with nothing worked out in the database: no
  conversion, no de-duplicating, no filtering. The app sorts each row as it arrives and hands the distinct paths to the file checks through a queue of at most 10,000 rows, so
  checking starts at once however large the table is, and even a table of millions of rows never has to fit in memory at once.
- **Many checks at once.** Up to 32 files are checked at the same time. Most of a check's time is spent waiting on the network, so working in parallel makes a large audit many
  times faster without overloading the file server.
- **Each drive or share is checked once.** The first time a path's root (such as `U:\` or `\\fs01\docs`) is seen, the app checks that it exists and remembers the answer. If it
  does not exist, every file under it is *Drive or share not found* at once, without trying each file. If a file later comes back "not found" under a root that was fine more than
  5 seconds ago, the root is checked again, in case the drive has just been disconnected.
- **Unreachable folders are found once.** When a file cannot be reached, the app walks its path one folder at a time to find the first folder that cannot be reached, and
  remembers that folder. Every later file under it gets the same answer straight away, whilst files elsewhere are still checked normally. A single blocked folder therefore costs
  one slow lookup, not one per file, and its files are still listed one by one in the report.
- **Duplicates are found by fingerprint.** Each path (with its ID, when an ID column is set) is turned into a 128-bit fingerprint (a hash), ignoring letter case and trailing
  spaces, and only the fingerprints are kept: about 48 MB per million rows. A fingerprint seen before marks a duplicate row, which is not checked again. At 128 bits, two different
  paths sharing a fingerprint is so unlikely that it will not happen in any real table, so a path is never wrongly passed over as a duplicate.
- **Skip markers and empty paths are never checked.** The app recognises them as it reads each row, so they are counted but never sent to the file checks. The IDs of the empty-path
  rows are collected as the rows are read, when an ID column is set.
- **An honest Remaining.** The estimate follows a speed weighted to the last 30 seconds of rows handled, ignores the first few seconds of start-up, counts down smoothly, and holds
  still during a stall, so it is useful on long audits over a variable network.

## 8.3 Unindexed Files Audit

- **Only the relevant rows are loaded.** For up to 20 input folders, the database is asked only for rows whose path starts with one of those folders, ignoring letter case, so a
  table covering many projects sends just the project's rows. With more folders, every row is loaded. Either way the result is the same; the filter only saves time.
- **The index is kept as fingerprints.** Each path is turned into a 128-bit fingerprint (a hash), and only the fingerprints are kept: about 48 MB per million rows instead of
  several times that for the paths themselves. At 128 bits, two different paths sharing a fingerprint is so unlikely that it will not happen in any real table, so a file is
  never wrongly reported as unindexed. Looking a file up takes about a microsecond.
- **One walk over all the folders.** The input folders are walked together in a single pass, not one walk per folder, so a folder that is inside another input is listed once,
  and each file is reported once, under the deepest input folder that covers it.
- **Many folders listed at once.** Up to 16 folder listings run at the same time on their own threads, because each listing over a network share is a round trip to the server.
- **Files are never opened.** Each folder listing already says whether an entry is hidden, a system file or a link, so the audit never touches a file's contents. That is why a
  single file can never fail this audit.
- **Links are never followed.** Following junctions and symbolic links risks loops and counting the same files twice. Instead, each link is reported once as *Link not followed*,
  so nothing is silently left out, and you can add its target as an input folder if it matters.
- **A folder is checked whole or not at all.** If a folder's listing fails part-way, none of its files are reported, and the folder is listed as *Unverified*, so a partial
  listing can never pass for a complete one.
- **The recent speed waits for the walk.** Loading the index can take minutes on a large table, so the bracketed recent speed is timed from the start of the walk, not from Start.

## 8.4 varchar Path Columns

**The warning.** When the path column is a `varchar` column, pressing Start on either tab shows a warning before the audit starts: it names the column and its type, and says that
`nvarchar` is preferred because a `varchar` column may have changed some characters in its paths when they were stored. *Continue* goes on with the audit; *Cancel* stops the Start,
with no audit and no report written. It appears last, after the other checks, so you are not asked about a column when something else would stop the Start anyway. Once you choose
*Continue* it is not asked again for that server, database, table and path column until you use *Close project* or *Open project...* or restart the app; changing any of the four
asks again. The answer is not saved in the project file. An `nvarchar` column shows no warning. To decide whether to warn, the app looks up the column's type when you press Start,
and the audit then uses that answer rather than asking again. If the lookup fails (for example, the server cannot be reached), no warning is shown and the audit reports the problem
straight away, so you wait for the server only once. If the lookup takes longer than 30 seconds (for example, the table is locked), it is given up and the audit checks the column
itself.

**What is converted.** The Missing Files audit converts nothing: it reads a `varchar` column as it is, whatever its length, and does all its work on the paths in the app. The
Unindexed Files audit reads the column as it is too, and converts it in one place only: the folder filter that limits which rows are loaded. There, each comparison treats a
`varchar` path as `nvarchar(4000)`, whatever the column's length, because the filter compares only the start of the path (an input folder, or the first character), so characters
beyond the 4,000th can never change the result. With no filter (no input folder, more than 20, or an empty one) nothing is converted.

**Why the filter converts.** The folder filter compares letters ignoring case, and on a `varchar` value that comparison would be done in the column's code page first: a path
outside Latin-1 could then miss its own folder and be reported as unindexed. Converting the value first avoids that.

**Possible effects.** A `varchar` column cannot store characters outside its collation's code page, so such a character was already replaced when the row was written: by a
look-alike letter (`Ł` became `L`) or by `?`. This comes from how the paths were stored, not from the audits, and nothing can undo it.

- A replaced path may name a different file. If that file exists, the Missing Files audit reports the row as *Found* although the file it was meant to name may be missing, and the
  Unindexed Files audit counts that other file as indexed.
- A path holding `?` is listed by the Missing Files audit under *Unverified — Invalid characters in path*, and never matches a file in the Unindexed Files audit.

The cure is to have the column converted to `nvarchar`, after which the warning no longer appears. Paths that were already changed cannot be recovered from the table.

# 9. Troubleshooting

**Start is disabled, and the status bar reads "Enter the required details".** Fill in Server, Database, Table and Path column, and complete the report location: a project
folder for a built location, or a location and file name for a custom one. Also check that no audit is running.

**"The report folder does not exist".** With a built location, the app does not create project folders. Check the project folder name and the project stage, or create the folder
(for example `U:\Projects\D12345\DE\Final\`) first. With a custom location, the app offers to create it for you.

**"The project folder must be a single folder name, not a path...".** Type just the folder's name, such as `D12345`, not a full path. To use a full path, tick *Use a custom
report location and file name*.

**"The report location must be a full path".** A custom location must start with a drive letter or a server share, such as `C:\Reports` or `\\server\share\Reports`.

**"Both reports would have the same file name".** With a custom location, both tabs' reports go to the same folder, so give each tab's report its own file name.

**The report file is read-only, or the folder cannot be written to.** Check the file's properties and your permissions on the folder, or choose another location.

**A SQL error when the audit starts** (login failed, cannot open database, invalid column name for the ID column). Check the server, database and ID column names. The app signs in
with your Windows account, so your account needs permission to read the table; request access through IT if not. (A wrong table or path column gets its own message, below.)

**"The path column '...' is char(400). The File Auditor expects an nvarchar or varchar column."** The path column is neither an `nvarchar` nor a `varchar` column (the message names
its actual type, for example `char(400)`, `nchar(10)` or `int`). Nothing was read and no report was written. Ask for the column to be converted to `nvarchar`, or point the audit at
a view of the table that casts the column to `nvarchar`, and type the view's name as the Table. The ID column can be of any type.

**A warning says "The path column '...' is varchar(400)..." when I press Start.** The path column is a `varchar` column. The warning is not an error: *Continue* audits the column
as it is, and *Cancel* stops the Start with nothing written. Some of the paths may have been changed when they were stored; section 8.4 explains how, and what to do. You are not
asked again for that column until you close the project, open another, or restart the app.

**"The path column '...' was not found in the table '...'."** The table has no column with that name, or the table itself was not found. Check the spelling of both, and the schema
if the table is not in the default one. If your account has no permission to read the table, SQL Server's own permission error is shown instead (for example
"The SELECT permission was denied on the object ..."), and the permission advice above applies. A synonym, also one that points to a table in another database,
works like the table it stands for.

**Lots of "Drive or share not found".** The drive letter or share in the paths does not exist on your computer. Map the drive (for example `U:`) as the index expects, or check
that the server and share in the paths still exist. The files have not been marked missing.

**Lots of "Permission denied" or "Network location unreachable".** Check that you can open the folder in File Explorer, and that the file server is up. Ensure you're connected to
the company's VPN. Re-run the audit once it is reachable.

**The Unindexed Files audit reports files that you know are indexed.** The paths must be spelled the same way in the table and on disk, apart from letter case. A common cause is
adding a folder through a mapped drive (`U:\Projects\...`) when the table holds server paths (`\\fs01\Projects\...`), or the reverse. Add the folder using the same spelling as
the table.

**The Unindexed Files audit finds no files.** Check the *File types* list (the type you expect may not be on it, or may be in *Exclude*), *Include subfolders* on each folder, and
whether the files are hidden or system files, which are always skipped. A folder that is a link is not walked; see its *Link not followed* entry in the report.

**The audit is slow.** Most of the time goes on the network. Run the audit on one of the DMS servers not being heavily utilised.

**View Report is disabled.** It is enabled only when the selected tab's report exists at the location shown in the Report group, and no audit is running. If you changed the
project folder, stage, or location since the run, the location no longer points at that report.

**"The settings file could not be read: the file is not a File Auditor project."** The file you opened is not a project settings file. Choose the project's
`FileAuditorSettings.json`, normally in the report's folder.

**The app asks where to save the settings.** The report's folder has changed since the project was opened (often because the project stage changed). Choose *Report Folder* to
save beside the new report, or *Original File* to keep the settings where they were.

**"Something unexpected went wrong."** The app met an error it did not expect, but it can carry on. Press OK and continue; if it keeps happening, record what you were doing in
section 12.

**"File Auditor ran into a problem it cannot recover from and has to close."** Note the message and what you were doing, record it in section 12, and start the app again.

# 10. Frequently Asked Questions

**Does File Auditor change or delete anything?**
No. It only reads the index table and lists folders. The only files it writes are the reports and the project settings file, all in the report's folder, and the only
folder it creates is a custom report folder, when you choose **Create Folder**.

**What is the difference between Missing and Unverified?**
*Missing* means the app is sure the file is not there (or is empty, if you asked). *Unverified* means it could not check, and the report says why. Treat Unverified files as
"find out", not as "lost".

**Why are some rows "Skipped"?**
Their path value is a skip marker (`SKIP`, `SKIPPED`, or either followed by a colon and a note) instead of a path. They are counted so the totals add up, but never checked or
listed.

**Why is Checked smaller than Total files?**
Many index tables repeat a file on several rows (a document listed once per patient, say). The app checks each distinct path once, so Checked counts files, not rows. The rows it
did not check again are the *Duplicates* (and any empty-path or skipped rows, which are counted separately). Distinct + Duplicates + empty paths + Skipped = Total files, the
table's rows.

**Where are the rows with no path, and why does my report not list them?**
They are under *[Path is empty in the index table]*, which lists one ID per row when you set an ID column. With no ID column, the group has one line saying why instead (section
4.4). A stopped or failed audit lists the IDs of the empty-path rows it had read so far.

**Why do the figures not add up to Total files when I stop an audit?**
The report adds a *Not checked* line for the rows the audit did not finish with, so Found + Missing + Unverified + Duplicates + empty paths + Skipped + Not checked = Total files.

**Why is Total files on the Unindexed Files tab smaller than the files Explorer shows in my folders?**
Total files counts only the files the audit checks: those that match your File types and are not in Exclude. Files that do not match are counted as *Excluded*. Hidden and
system files are skipped without being counted at all, and a symbolic link to a file of a checked type is listed under *Unverified* instead. So Total files + Excluded can
still be fewer than Explorer's count when hidden or system files are shown in Explorer, or when you have such links.

**Why is there no Remaining time on the Unindexed Files tab?**
The app cannot know how many files the folders hold until it has walked them all, so it cannot estimate the time left.

**Can I run both audits at once?**
No, one at a time. You can still switch tabs whilst an audit runs.

**Where are my settings saved?**
In `FileAuditorSettings.json`, in the report's folder. The Project Settings group says which file is in use.

**Can I share a project with a colleague?**
Yes. Commit the settings file with the report; your colleague opens it with *File > Open project...*.

**Does the Unindexed Files audit look inside ZIP files or follow shortcuts?**
No. It checks files by name only and never opens them. Windows shortcuts (`.lnk` files) are ordinary files to it, and junctions and symbolic links are reported, not followed.

**Does letter case matter?**
No. Paths are compared ignoring letter case, in both audits, and file type entries ignore case too.

**Will the app report its own reports or settings files?**
Its reports can be. The built-in file types include `.txt`, so a report inside a folder you check is listed as unindexed unless the index table holds it. The lists match
extensions, not file names, so to avoid this, write your reports and settings to the project's SVN repo and not the project's interim file server.

**Which version am I using?**
See *Help > About File Auditor*, and the changelog in section 14.

# 11. Glob Pattern Matching Cheat Sheet

The *File types* and *Exclude* lists use simple glob patterns on a file's extension.

**The basics**

- An entry is one or more **parts** separated by dots, such as `pdf`, `tar.gz` or `zip.001`.
- The leading dot is optional: `pdf`, `.pdf` and `*.pdf` all mean the same.
- `*` matches any run of characters, including none. `?` matches exactly one character.
- A wildcard never crosses a dot: it matches within one part only.
- Letter case never matters. The app keeps entries in lower case, with a leading dot, sorted, without duplicates.
- An entry of *k* parts matches a file whose **last *k* parts** match it, part for part. The part before the first dot (the file's name) is never part of its extension, so a
  file with no dot, or one whose name starts with a dot (such as `.gitignore`), has no extension and matches nothing.

**Examples**

| Entry      | Matches                           | Does not match                     |
|------------|-----------------------------------|------------------------------------|
| `.pdf`     | `Report.pdf`, `REPORT.PDF`        | `Report.pdf.bak`, `Report.pdfx`    |
| `.tif*`    | `Scan.tif`, `Scan.tiff`           | `Scan.tf`                          |
| `.?if`     | `Scan.tif`, `Image.gif`           | `Scan.tiff`                        |
| `.doc?`    | `Letter.docx`, `Letter.docm`      | `Letter.doc`                       |
| `.zip.001` | `Backup.zip.001`                  | `Backup.zip`, `Backup.zip.001.bak` |
| `.zip.*`   | `Backup.zip.01`, `Backup.zip.001` | `Backup.zip`                       |
| `.tar.gz`  | `Archive.tar.gz`                  | `Archive.gz`                       |
| `.gz`      | `Archive.gz`, `Archive.tar.gz`    | `Archive.tgz`                      |
| `*`        | every file that has an extension  | `README` (no dot), `.gitignore`    |

**Combining the lists**

- *File types* `.gz` and *Exclude* `.tar.gz`: `x.gz` is checked, `x.tar.gz` is not. Exclude always wins.
- *File types* `.zip.*` and *Exclude* `.zip.000`: every split-ZIP part is checked except the `.000` one.
- A file that matches several *File types* entries is grouped under the most specific: the most parts, then the fewest wildcards, then the first alphabetically. So with `.gz`
  and `.tar.gz`, `x.tar.gz` belongs to `.tar.gz`.

**Entries that match every file**

`*`, `*.*` (the same as `*`), `?*` and `**` match every file. Such an entry is not allowed in *Exclude*, and in *File types* the app asks you to confirm when other entries or
an Exclude list make it redundant. `?` (every one-character extension) and `.*.*` (every two-part extension) do not match every file.

**Entries that are not valid**

An entry is refused when it is empty after the dot, has an empty part (`a..b`), contains a space, a comma, a `\` or `/`, or contains a character Windows does not allow in file
names (other than `*` and `?`). Enter one entry per row in the editor.

# 12. Bugs & Issues

This section is used to record bugs and issues found in the app. When one is fixed, it’ll be removed here and logged in the changelog below. When adding a bug or issue, use
an H2 header for the title, which should be a brief description of what it is, followed by a pipe and your name and date. For example: 'Report Not Written to Share |
Justinian Erdmier 01 January 2000'. Describe what you were doing, what you expected and what happened, and include any message the app showed.

# 13. Feature Ideas

This section is used to record ideas for possible future updates. If any are implemented, they’ll be removed here and logged in the changelog below. When adding an idea, use an
H2 header for the title, which should be a brief description of what it is, followed by a pipe and your name and date. For example: 'My Cool Idea | Justinian Erdmier 01
January 2000'.

# 14. Changelog

To see what version of the app you’re using, navigate to `Help > About File Auditor`.

## Version 2.0.0

**Release Date:** 13 November 2026

Audit optimisation: the audits read the index table differently, report more about what they read, and tell you when they finish.

**Changes that may affect existing projects:**

- **The path column must be `nvarchar` or `varchar`.** A `varchar` or `nvarchar` path column works on both audits (the app reads a `varchar` column as it is, after a warning that
  you can continue or cancel, section 8.4). Only another type (`char`, `nchar`, `text`, `ntext`, a number, …) is refused, naming the type, before a row is read (section 3.1). A
  project with such a column stops working; convert the column or point the app at a view that casts it (section 9).
- **Each path is checked once.** A path listed on several rows (an index built around patients rather than files) is checked once, so *Distinct* counts paths and *Checked* counts
  files, not rows; *Total files* is the table's rows. With an ID column, each ID is expected to belong to one path.
- **The reports' Summary has changed.** The Missing Files report adds *Distinct*, *Duplicates* and *Found*, makes *Total files* the table's rows and puts the empty paths beside
  *Unverified*; the Unindexed Files report adds *Excluded* and *Index paths*. Anything that reads the reports by line should be checked.

**New:**

- Missing Files tab: live counts. The table's rows are counted in a moment (*Total files*) and are then read and checked at once, however large the table; *Distinct*, *Duplicates*,
  *Skipped* and the empty paths in brackets on *Unverified* grow as the rows are read, and *Remaining* and the progress bar follow the rows handled. New statistics: *Distinct* (the
  different paths seen, which the checks then work through), *Found* and *Duplicates*. The report's Summary lists *Total files*, *Distinct*, *Duplicates*, *Found*, *Missing*,
  *Unverified* and *Skipped*, and *Not checked* for a stopped run; the IDs of empty-path rows are listed in the report as they were read (or a note says why they are not).
- Unindexed Files tab: *Excluded* (files the type filters skipped) and *Index paths* (the paths loaded to compare against, counted live while the index loads); *Indexed* moves
  to the top of the second column.
- A *finished* dialogue when an audit completes, with its key figures, the report's location and *View Report*. If you are in another program, the taskbar button flashes and
  Windows plays a sound; a failed audit gets the error sound and the flash too.

## Version 1.0.0

**Release Date:** 16 October 2026

First version of the app published and ready for testing and usage.
