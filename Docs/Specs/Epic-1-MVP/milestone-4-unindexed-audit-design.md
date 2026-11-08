# Milestone 4 — Unindexed Files Audit

**Date:** 2026-11-08
**Status:** Approved 2026-11-08; §3.3 and §3.7 amended the same day with the user's whitelist, blacklist and wildcard decisions

> **Amended during the build (2026-11-08).** §3.1 now records the layout the user settled on while the tab was being built; §3.3 defines what "matches every file" means; §3.5
> adds the whitespace-led rows to the index load and corrects the memory figure; §3.6 describes the moving block that replaced the indeterminate bar; §4 notes the File Types
> dialogue. The master SDD (`Docs/Specs/1-file-auditor-master-design.md`) describes what was built and is authoritative where the two differ.

## 1. Intent

Today's audit answers *"is every indexed file on disk?"* and finds **missing** files. It cannot find the opposite problem: files sitting on disk that nobody
indexed. This milestone adds that second direction, the **unindexed files audit**: walk one or more folders, and report every document whose full path is not in
the file index table. It builds on milestone 3's tabbed window, adding the **Unindexed Files** tab.

Folders and tables on real projects hold **millions** of files and rows, so the audit must be designed for that scale from the start.

The version stays **1.0.0** (MVP epic).

## 2. Goals, non-goals, anti-goals

**Goals**

- Find every file under the chosen folders whose full path is not in the index table's path column.
- Let the user choose which folders, whether each includes its subfolders, and which file types count.
- Handle millions of files on disk and millions of rows in the table, over network shares, in reasonable time and memory.
- Write a readable plain-text report, grouped so unindexed files can be acted on in bulk.

**Non-goals**

- Translating between path spellings (`\\server\share` versus a mapped drive, relative paths). The index always holds full paths that match the disk exactly,
  apart from letter case (the user's statement).
- Guessing which files matter. The app never decides on its own what to include; the user's file-type list does.
- Changing the missing-files audit.

**Anti-goals**

- Reporting a file as unindexed when it is indexed (for example through a hash collision or a case difference).
- Silently skipping a folder the app could not read; it must be reported.
- Walking the folders twice just to show a percentage.

## 3. Behaviour

### 3.1 The Unindexed Files tab

Milestone 3's tab control gains a second tab, **Unindexed Files**. Top to bottom:

1. **Folders to Check** group:
   - a list of folders, one per row, each with an **Include subfolders** checkbox in a column at the row's left, **ticked by default** (the user's decision);
   - **Add...** opens a folder picker and appends the folder; **Remove** removes the selected row; both buttons sit to the right of the list;
   - **File types:** a read-only one-line summary of the extension whitelist (§3.3) with an **Edit...** button;
   - **Exclude:** the same for the extension blacklist (§3.3), empty by default.

   Each **Edit...** opens the **File Types dialogue**: one entry per row, as in Windows' environment-variable editor, with **New**, **Edit** and **Delete**, **Reset** (restoring
   the built-in list) in the File types dialogue only, and **OK** and **Cancel**. OK checks the entries and keeps the dialogue open on an invalid one; Cancel discards every
   change. (The first design was one text box per list; the user changed it during the build.)
2. The complete **Report** group, shared with the Missing Files tab exactly as milestone 3 describes; the file name here is the unindexed report's
   (`FileAuditorUnindexedReport_<Audit Stage>.txt`, custom default `FileAuditorUnindexedReport.txt`). On this tab only, the group has one more option below the
   file name: **Group file types by** — *Listed type* (default) or *Found extension* (§3.7; the user's decision). With the custom option ticked, the project folder and the
   stage options are **hidden**, not disabled, on both tabs (the user's decision).
3. The **Audit** group (§3.6) with View Report / Start / Stop.

**Layout decisions taken by the user while the tab was built (2026-11-08).** The Unindexed tab is compact and the tab control has a fixed height, so the window does not jump
between tabs (built: 640 × 761 pixels on both tabs). The Include subfolders column sits at the left of the folder list. File types and Exclude are summaries edited in a
dialogue (above), with Reset only for File types. A custom report location hides the project folder and stage options. *Unverified* is the fourth statistic of the first column
(§3.6). The Database and File Index Table groups sit side by side, each vertical inside; this is a trial the user may reverse.

The shared Database and File Index Table groups above the tabs are used as they are; the **ID column is not used** by this direction (an unindexed file has no
row, so no ID).

### 3.2 Which files are checked

- Each folder is walked; with *Include subfolders* ticked, every folder beneath it too.
- **Always skipped:** files and folders with the **hidden** or **system** attribute (a skipped folder's contents are skipped with it), and anything whose
  name is not matched by the whitelist, or is matched by the blacklist (§3.3). "Configuration files" are covered by the lists: an extension that is not listed is
  never checked (the user's decision).
- A file reached through two input folders (one folder inside another) is checked and reported **once**, under the deepest input folder that contains it.
- **Links are followed.** Junctions, symbolic links and DFS links to folders are walked like ordinary folders (DFS namespaces on file shares appear as links, and
  skipping them would silently drop content). Files are reported by the path they were reached through. To prevent loops, a folder whose target was already
  walked in this run is not walked again; its contents were already checked, so nothing is lost.

### 3.3 File types: whitelist and blacklist

Two editable lists of extension patterns, both saved in the project file (the user's decisions):

- **File types** (the whitelist): a file is checked only if it matches an entry here. Built-in defaults: common document and image formats — at least `.pdf`,
  `.tif`, `.tiff`, `.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`, `.doc`, `.docx`, `.rtf`, `.odt`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, `.msg`, `.eml`, `.xps` (the builder
  may add other common document and image formats). `.txt`, `.json` and other text or configuration formats are **not** defaults; the user may add them.
- **Exclude** (the blacklist): a file that matches an entry here is never checked, even if the whitelist matches it — **the blacklist wins**. Empty by default.
  Example: whitelist `.gz`, blacklist `.tar.gz` checks `x.gz` but not `x.tar.gz`.

**Writing an entry.** An entry is one or more dot-separated **parts**, for example `pdf`, `tar.gz`, `zip.001`, `zip.*`.

- A leading dot is optional: `pdf` and `.pdf` are the same. A leading `*.` is the same as a leading dot: `*.pdf` is `.pdf` (the user's decision, so the habit of
  typing `*.pdf` works).
- Entries are case-insensitive, shown and saved in lower case with a leading dot; duplicates are dropped.
- **Wildcards** (the user's decision): `*` stands for any run of characters and `?` for exactly one character, the usual glob meanings. There are no character sets.
  A wildcard **stays within one part**: it never matches a dot.

**Matching a file name** (exact, counted from the end — the user's decision):

1. Split the file name on dots. The first part is the **stem** and must not be empty, so a name with no dot, or one that starts with a dot (`.gitignore`), has no
   extension and matches nothing.
2. An entry with *k* parts matches when the name has at least *k* parts after the stem and its **last *k* parts** match the entry's parts one for one, each part
   compared with its wildcards, ignoring case.

| Entry      | `Backup.zip` | `Backup.zip.01` | `Backup.zip.001` | `Backup.zip.001.bak` | `scan.tar.gz` | `x.gz` |
|------------|--------------|-----------------|------------------|----------------------|---------------|--------|
| `.zip.001` | no           | no              | **yes**          | no                   | —             | —      |
| `zip.*`    | no           | **yes**         | **yes**          | no                   | —             | —      |
| `zip.0??`  | no           | no              | **yes**          | no                   | —             | —      |
| `.gz`      | —            | —               | —                | —                    | **yes**       | **yes** |
| `.tar.gz`  | —            | —               | —                | —                    | **yes**       | no     |
| `.nii.gz`  | —            | —               | —                | —                    | no            | no     |
| `*`        | **yes**      | **yes**         | **yes**          | **yes**              | **yes**       | **yes** |

**Most specific entry.** When a file matches more than one whitelist entry (`zip.*` and `zip.001`; `.gz` and `.tar.gz`), it belongs to the **most specific**: the
one with the most parts; among those, the fewest wildcard characters; among those, the first in alphabetical order (the user's decision on overlap; the tie-breaks
are the builder's).

**Checks at Start** (part of pre-flight, SDD §6.1; the user's decisions). An entry is **wildcard-only**, meaning it matches every file, when it has exactly one part, that part is
made only of wildcards, and it contains at least one `*` and at most one `?`. So `*`, `*.*` (the same as `*`), `?*` and `**` are wildcard-only, while `?` (every one-character
extension) and `.*.*` (two parts) are ordinary entries that raise nothing. (The first wording, "made only of wildcards", would have refused `?` as "matching every file".)

| Situation                                                                 | Result                                                                                       |
|---------------------------------------------------------------------------|----------------------------------------------------------------------------------------------|
| The blacklist has a wildcard-only entry                                   | **Error**: it would exclude everything, so the audit cannot run.                             |
| The whitelist is empty                                                    | **Error** (no file would be checked).                                                        |
| The whitelist has a wildcard-only entry **and other entries**             | **Warning** (Continue / Cancel): the wildcard makes the other entries redundant.             |
| The whitelist has a wildcard-only entry **and** the blacklist is not empty | **Warning** (Continue / Cancel): every file will be checked *except* the blacklisted types. |
| The same entry (after normalising) is in both lists                       | **Warning** (Continue / Cancel): the blacklist wins, so that type is not checked.            |
| A wildcard-only whitelist entry alone, with an empty blacklist            | Accepted without a warning.                                                                  |

Overlapping **patterns** (whitelist `zip.*`, blacklist `zip.000`) are ordinary use and raise nothing; only identical entries warn. Each warning, once answered
*Continue*, is not asked again until *Close project* or a fresh launch, like the remembered answer to the save prompt (the user's decision). *Cancel* stops the
Start.

### 3.4 Matching a file to the index

A file is **indexed** when its full path equals a value in the path column, **ignoring letter case** and nothing else (the user's decision). Leading and trailing
whitespace in the column's values is trimmed.

### 3.5 How it runs (built for millions)

1. **Pre-flight** as today for the shared inputs and the report (SDD §6.1), plus: at least one folder; each folder a full path that exists; at least one file
   type, each a valid extension.
2. **Load the index.** Read only the path column (no ID). Where possible, ask SQL only for rows under the chosen folders (a `LIKE '<folder>\%'` prefix per folder,
   with `%`, `_` and `[` escaped), so tables that span many projects send only the relevant rows. Because values are trimmed before they are matched, the filter also
   loads every row that **starts with a whitespace character**, which no folder prefix could match (a further `OR` clause); with more than 20 folders, or an empty folder
   path, it is dropped and every row is loaded. Each path is trimmed, **upper-cased and hashed to 128 bits**, and only the hashes are kept in memory: measured at about
   **48 MB per million rows** (the hashes plus the set that holds them; the first estimate of 16 bytes a row was too low), so three million rows take roughly 150 MB. A
   128-bit hash makes a false match (anti-goal 1) practically impossible.
3. **Walk the folders.** Folder listings run **concurrently**, up to a fixed limit (for example 16), because each listing over a network share is a round trip.
   Each listing returns the files' attributes, so hidden and system files are skipped without extra disk access, and each file is checked against the in-memory
   hashes as it is found — a microsecond lookup. Files are not batched across threads: the lookup is too cheap to be worth it; the parallelism is in listing.
4. **Write the report** (§3.7).

The user agreed the index may be loaded fully into memory (as hashes); the builder chooses the exact hashing, concurrency limit and data structures.

### 3.6 Progress

**What is counted.** A *file* is a file of a listed type, not hidden or system, found by the walk. Each is checked the moment it is found, so every file found is
either **indexed** or **unindexed**: *Total files* = *Indexed* + *Unindexed*. The audit never opens a file, so a single file cannot fail; what can fail is
**listing a folder**. Such a folder is **Unverified**: its contents are not checked, and it is counted separately (*Unverified*, in folders), never inside *Total
files*.

Statistics on the Unindexed Files tab: **Total files** (found so far), **Indexed**, **Unindexed**, **Unverified** (folders), **Elapsed**, **Files/second**. **Remaining is
hidden** on this tab — label and value — because the total is not known until the walk ends (the user's decision). While the run is under way the progress bar shows a **moving
block** of fixed width that crosses a sunken track and starts again, driven by the 250 ms refresh timer, and the bar is full when it ends. Classic.Avalonia's own indeterminate
marquee was tried and set aside: it is blank for about 2.4 seconds of every 5-second cycle and cannot be restyled, and a growing fill would have read as a percentage of an
unknown total. The statistics are laid out in two columns: **Total files**, **Indexed**, **Unindexed** and **Unverified** on the left, **Elapsed** and **Files/second** on the
right. Status bar phases: `Loading the index...`, `Checking files...`, `Writing report...`, then the result.

Only **one audit runs at a time**: while either direction runs, both tabs' Start buttons are disabled and every input is locked; switching tabs is allowed.

While a run is under way, **Stop** (button, menu and Esc) and the **status bar** belong to the running audit whichever tab is selected, so switching tabs never
hides a run (see milestone 3 §3.3–§3.4, which this extends).

**Stop** during *Loading the index...* writes no report (as Stop during today's count). After that, **Stop** stops the walk and writes the report as
`Stopped by user`; the counts reflect what was found and checked (there is no *Not checked* line, since the
total is unknown). Closing the window mid-run behaves as today (SDD §6.4).

### 3.7 The report

Plain text, same rules as today's report (UTF-8 without BOM, CRLF, local timestamps, deterministic order). Layout (report option A, the user's choice):

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

Unverified Folders (3)
----------------------
[Permission denied (2)]
U:\Projects\D12345\Images\Restricted\
U:\Projects\D12345\Images\Restricted2\

[Other errors (1)]
U:\Projects\D12345\Images\2020\   I/O error   The semaphore timeout period has expired.
```

- Unindexed files are grouped by **input folder** (in the order listed), then by **file type** (alphabetical), then sorted by path (ordinal, ignoring case).
  Every folder that has unindexed files gets its group, even when only one folder was chosen; groups with none are omitted. `No unindexed files.` when there are
  none.
- **Which file-type groups** (the user's decision, an option in the tab's Report group, saved in the project file):
  - **Listed type** (default): one group per whitelist entry as entered, after normalising — `[.zip.* (12)]`, `[.pdf (2)]`.
  - **Found extension**: one group per real extension found — the file name's last *k* parts, lower case, where *k* is the matching entry's part count —
    `[.zip.001 (5)]`, `[.zip.002 (7)]`.
  - Either way, a file that matches several whitelist entries is grouped through the **most specific** one (§3.3): under *Found extension* that entry's part count
    decides how many parts make up the extension.
- The header lists `File types:` and `Exclude:` (each sorted, space-separated; `Exclude:` reads `(none)` when empty) and `Grouped by:` (`Listed type` or
  `Found extension`).
- **Unverified Folders** lists folders that could not be listed (path ending in `\`; their contents were not checked), grouped by the same reasons as SDD §6.3
  (*Drive or share not found*, *Permission denied*, *Network location unreachable*, *Path too long*, *Other errors* with reason and system message). Only the
  first folder that failed on each branch is listed. `No unverified folders.` when there are none.
- Folders, file types and the project lines follow the same rules as today's header (custom location → no project lines). `Folders:` notes
  `(with subfolders)` per folder.

### 3.8 The project file

Adds, for this direction: the folder list (each with its *Include subfolders* flag), the whitelist, the blacklist, the *Group file types by* choice, and the
unindexed report's custom file name. A project with no folders saved starts with an empty list; a missing whitelist starts with the built-in defaults; a missing
blacklist starts empty; a missing grouping choice is *Listed type*. The remembered warning answers (§3.3) are not saved.

## 4. Builder's calls (decided by Claude, not the user)

- File types and Exclude were first designed as single text boxes (entries separated by spaces or commas) with **Reset** on the whitelist only; during the build the user
  chose read-only summaries with **Edit...** buttons opening the File Types dialogue (§3.1). Labels `File t_ypes:` and `E_xclude:` (Y and X are free on that tab).
- *Group file types by* is a pair of radio buttons in the Unindexed tab's Report group (`Listed type`, `Found extension`); it is per direction, like the file
  name, so it does not appear on the Missing Files tab.
- Tie-breaks for "most specific" beyond the part count (fewest wildcard characters, then alphabetical) are the builder's.
- An entry is invalid if it is empty after the leading `.` or `*.`, has an empty part (`a..b`), or contains a path separator or a character Windows forbids in file
  names other than `*` and `?`; an invalid entry is a pre-flight error naming it.
- The default list excludes `.txt` and `.json`, so the app's own reports and project files are never reported as unindexed unless the user adds them.
- A file under two input folders is reported once, under the deepest folder.
- Hidden and system **folders** are skipped along with their contents.
- The stopped report has no *Not checked* line; *Total files* is the number found before the stop.
- Path-column values are trimmed before hashing.
- The SQL prefix filter is an optimisation: if the builder finds it unsafe for some input (for example a folder path the `LIKE` escaping cannot represent), it
  falls back to loading every row.
- Hashing uses an in-box .NET algorithm (no new package); a dedicated hashing package would need the user's approval.
- Counting: *Total files* = *Indexed* + *Unindexed*; folders that cannot be listed are counted apart as *Unverified* (folders). The tab shows *Indexed* in place of
  *Checked* (every file found is checked at once, so *Checked* would always equal *Total files*).
- Links (junctions, symbolic links, DFS) are followed, with a guard so a folder target is walked once per run.
- While a run is under way, Stop and the status bar follow the running audit, not the selected tab.
- Stop while the index is loading writes no report.
- The SQL prefix filter is case-insensitive, so a case-sensitive column collation cannot drop rows.
- If both tabs' custom file names are the same, pre-flight refuses with an error (the two reports would overwrite each other).

## 5. SDD sections to amend in this milestone

§1, §2 (the second direction), §5 (the Unindexed Files tab, its groups, command states, one-run-at-a-time), §6 (a new section for running the unindexed audit:
pre-flight additions, loading, walking, progress, stop), §7 (the unindexed report), §8 (project file additions), §9 (new services and view models), §10/§11.

## 6. Decided by the user

- A second direction that finds files on disk that are not in the index, alongside today's audit.
- Usually one input folder, sometimes several; each folder has an *Include subfolders* checkbox, ticked by default.
- A file matches when its full path equals the index's path, ignoring case; no path translation.
- An extension whitelist with built-in defaults for common document and image formats, editable per project; hidden, system and configuration files are always
  ignored; the app never guesses what to include.
- Extensions may be listed with or without the leading dot, and multi-dot extensions are supported; a leading `*.` means the same as a leading dot.
- Matching is exact and counted from the end of the name, one dot-separated part at a time; `*` and `?` glob wildcards stay within one part.
- A blacklist (**Exclude**) alongside the whitelist; the blacklist wins. Overlapping matches go to the most specific whitelist entry.
- Start-time checks: a wildcard-only blacklist entry is an error; a wildcard-only whitelist entry with other entries, or with a non-empty blacklist, and an entry
  in both lists are warnings (Continue / Cancel), each remembered for the session once answered; only identical entries count as "in both".
- Names with no dot, or starting with a dot, have no extension and match nothing.
- The report can group file types by listed type (default) or found extension, chosen in the Unindexed tab's Report group.
- Its own plain-text report in the shared report location, named `FileAuditorUnindexedReport_<Audit Stage>.txt` (custom default
  `FileAuditorUnindexedReport.txt`), grouped by input folder, then file type, then path.
- Built for millions of files and rows; loading the table's paths into memory is acceptable.
- No up-front count: live counts, an indeterminate-style progress bar (built as a moving block, §3.6), **Remaining hidden**, **Files/second shown**.
- The version stays 1.0.0.
