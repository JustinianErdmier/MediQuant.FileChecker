# Milestone 4 — Unindexed Files Audit

**Date:** 2026-11-08
**Status:** Approved 2026-11-08

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
   - a list of folders, one per row, each with an **Include subfolders** checkbox, **ticked by default** (the user's decision);
   - **Add...** opens a folder picker and appends the folder; **Remove** removes the selected row;
   - **File types:** the extension whitelist (§3.3), with **Reset** restoring the built-in list.
2. The complete **Report** group, shared with the Missing Files tab exactly as milestone 3 describes; the file name here is the unindexed report's
   (`FileAuditorUnindexedReport_<Audit Stage>.txt`, custom default `FileAuditorUnindexedReport.txt`).
3. The **Audit** group (§3.6) with View Report / Start / Stop.

The shared Database and File Index Table groups above the tabs are used as they are; the **ID column is not used** by this direction (an unindexed file has no
row, so no ID).

### 3.2 Which files are checked

- Each folder is walked; with *Include subfolders* ticked, every folder beneath it too.
- **Always skipped:** files and folders with the **hidden** or **system** attribute (a skipped folder's contents are skipped with it), and anything whose
  extension is not in the file-type list. "Configuration files" are covered by the list: an extension that is not listed is never checked (the user's decision).
- A file reached through two input folders (one folder inside another) is checked and reported **once**, under the deepest input folder that contains it.
- **Links are followed.** Junctions, symbolic links and DFS links to folders are walked like ordinary folders (DFS namespaces on file shares appear as links, and
  skipping them would silently drop content). Files are reported by the path they were reached through. To prevent loops, a folder whose target was already
  walked in this run is not walked again; its contents were already checked, so nothing is lost.

### 3.3 File types

An editable list of extensions, saved in the project file. Built-in defaults include common document and image formats — at least `.pdf`, `.tif`, `.tiff`,
`.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`, `.doc`, `.docx`, `.rtf`, `.odt`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, `.msg`, `.eml`, `.xps` (the builder may add other
common document and image formats). `.txt`, `.json` and other text or configuration formats are **not** defaults; the user may add them. Matching ignores case.

- **With or without the dot.** An extension may be typed with or without its leading dot (`pdf` or `.pdf`); both mean the same, and the list is shown and saved
  with the dot (the user's decision).
- **Multi-dot extensions** are supported (the user's decision): `.nii.gz` or `tar.gz` matches `scan.nii.gz`. A file matches a listed extension when its name ends
  with that extension (ignoring case) and has at least one character before it. When a file matches several listed extensions (`.gz` and `.nii.gz`), it is grouped
  in the report under the **longest** one.

### 3.4 Matching a file to the index

A file is **indexed** when its full path equals a value in the path column, **ignoring letter case** and nothing else (the user's decision). Leading and trailing
whitespace in the column's values is trimmed.

### 3.5 How it runs (built for millions)

1. **Pre-flight** as today for the shared inputs and the report (SDD §6.1), plus: at least one folder; each folder a full path that exists; at least one file
   type, each a valid extension.
2. **Load the index.** Read only the path column (no ID). Where possible, ask SQL only for rows under the chosen folders (a `LIKE '<folder>\%'` prefix per folder,
   with `%`, `_` and `[` escaped), so tables that span many projects send only the relevant rows. Each path is **upper-cased and hashed to 128 bits**, and only
   the hashes are kept in memory: about 16 bytes per row instead of about 200, so three million rows take roughly 50 MB. A 128-bit hash makes a false match
   (anti-goal 1) practically impossible.
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

Statistics on the Unindexed Files tab: **Total files** (found so far), **Indexed**, **Unindexed**, **Unverified** (folders), **Elapsed**, **Files/second**.
**Remaining is hidden** on this tab — label and value — because the total is not known until the walk ends (the user's decision). The progress bar is **indeterminate**
(marquee) while the run is under way and full when it ends. Status bar phases: `Loading the index...`, `Checking files...`, `Writing report...`, then the result.

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
File types:      .doc .docx .jpg .pdf .png .tif .tiff
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
- **Unverified Folders** lists folders that could not be listed (path ending in `\`; their contents were not checked), grouped by the same reasons as SDD §6.3
  (*Drive or share not found*, *Permission denied*, *Network location unreachable*, *Path too long*, *Other errors* with reason and system message). Only the
  first folder that failed on each branch is listed. `No unverified folders.` when there are none.
- Folders, file types and the project lines follow the same rules as today's header (custom location → no project lines). `Folders:` notes
  `(with subfolders)` per folder.

### 3.8 The project file

Adds, for this direction: the folder list (each with its *Include subfolders* flag), the file-type list, and the unindexed report's custom file name. A project
with no folders saved starts with an empty list; a missing file-type list starts with the built-in defaults.

## 4. Builder's calls (decided by Claude, not the user)

- File types are edited in a single text box (space-separated extensions) with a **Reset** button, rather than a separate dialogue; entries are normalised to
  lower case with a leading dot, and duplicates are dropped.
- With multi-dot extensions, a file belongs to the longest listed extension it ends with.
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
- Extensions may be listed with or without the leading dot, and multi-dot extensions are supported.
- Its own plain-text report in the shared report location, named `FileAuditorUnindexedReport_<Audit Stage>.txt` (custom default
  `FileAuditorUnindexedReport.txt`), grouped by input folder, then file type, then path.
- Built for millions of files and rows; loading the table's paths into memory is acceptable.
- No up-front count: live counts, an indeterminate progress bar, **Remaining hidden**, **Files/second shown**.
- The version stays 1.0.0.
