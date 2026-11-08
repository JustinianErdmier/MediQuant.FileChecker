# Milestone 4 — Unindexed Files Audit Implementation Plan

**Date:** 2026-11-08

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the second audit direction: an **Unindexed Files** tab that walks chosen folders and reports every file of a listed type whose full path is not in the
file index table, built for millions of files and rows.

**Architecture:** The index's paths are streamed from SQL (prefix-filtered per folder where safe) into an in-memory set of 128-bit hashes (`IndexPathSet`). A
`FolderWalker` lists folders concurrently, skipping hidden/system entries and unlisted types, following links with a loop guard, and attributing each file to the
deepest input folder whose scope covers it. An `UnindexedAuditRunner` drives the phases and an `UnindexedReportBuilder` writes the report, sharing format helpers
with the existing builder through a new `ReportFormat`. The UI adds the tab per milestone 3's checklist; a small `AuditStatusBase` lets the status bar show either
direction's Audit group.

**Tech Stack:** unchanged — .NET 10 · Avalonia 11.3.22 · Classic.Avalonia 11.3.0.3 · CommunityToolkit.Mvvm 8.4.2 · ErrorOr 2.1.1 · Microsoft.Data.SqlClient 7.1.1 ·
System.Text.Json. Hashing uses in-box `System.Security.Cryptography.MD5` (non-cryptographic use, 128 bits). No new packages.

**Spec:** [`Docs/Specs/Epic-1-MVP/milestone-4-unindexed-audit-design.md`](../../Specs/Epic-1-MVP/milestone-4-unindexed-audit-design.md) — read it in full first; §3.x
below refers to it. Milestone 3's spec and the master SDD [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) (§5, §6, §7, §8,
§9, §12) describe what this builds on. Mock-ups: [`direction-tabs-layout.html`](../../UserInterfaceDesign/Mockups/direction-tabs-layout.html) (right panel) and
[`unindexed-report-layout.html`](../../UserInterfaceDesign/Mockups/unindexed-report-layout.html) (option A). `MainWindowViewModel` carries a comment listing every
place milestone 4 must touch (above `IsTabbed`).

## Global Constraints

- **No automated tests.** Verify with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings) plus each task's
  checks. Throwaway harnesses (a scratch console project under the scratchpad referencing `Source/FileAuditor/FileAuditor.csproj`, or `dotnet run` of a script
  project) are allowed for service-level tasks and are never committed. LocalDB `(localdb)\MSSQLLocalDB` is available; use a scratch database and drop it.
  The app is driven with Windows UI Automation from PowerShell. Close everything you start.
- Version stays `1.0.0`. No new packages. No compatibility with older project files beyond SDD §8's "they load; new fields take defaults".
- Exact copy, verbatim:
  - Tab header `Unindexed Files`; group `Folders to Check`; column header `Include subfolders`; buttons `Add...`, `Remove`, `Reset`; labels `File t_ypes:` and
    `E_xclude:`. In the Unindexed tab's Report group, below File name: label `Group file types by:` with radio buttons `Listed type` (default) and
    `Found extension`.
  - Unindexed report: built `FileAuditorUnindexedReport_<Audit Stage>.txt`; custom default `FileAuditorUnindexedReport.txt`; title `File Auditor Unindexed Report`
    over 29 `=`; sections `Unindexed Files (n)` / `Unverified Folders (n)` underlined with `-` to their length; empty texts `No unindexed files.` /
    `No unverified folders.`; Summary labels `Total files:`, `Indexed:`, `Unindexed:`, `Unverified:`; header `Folders:` lines with ` (with subfolders)`,
    `File types:` and `Exclude:` (space-separated, sorted; `Exclude:` reads `(none)` when empty) and `Grouped by:` (`Listed type` / `Found extension`). Layout
    exactly as spec §3.7.
  - Statistics labels on the tab: `Total files:`, `Indexed:`, `Unindexed:`, `Unverified:`, `Elapsed:`, `Files/second:` (no `Remaining:`).
  - Status phases: `Loading the index...`, `Checking files...`, `Writing report...`.
  - Pre-flight errors: `Add at least one folder to check.`; `This folder is not a full path:\n{path}`; `This folder does not exist:\n{path}`;
    `This folder is listed twice:\n{path}`; `Add at least one file type.`; `This file type is not valid:\n{entry}`;
    `The Exclude list contains {entry}, which matches every file, so nothing would be checked. Remove it to run the audit.`;
    `Both reports would have the same file name:\n{name}\n\nGive each tab's report its own name.` (paths through `PathText.Breakable`).
  - Pre-flight warnings (warning icon, buttons `_Continue` / `Cancel`), each remembered for the session once *Continue* is chosen:
    `File types contains {entry}, which matches every file, so the other file types are redundant.\n\nDo you want to continue?`;
    `File types contains {entry}, which matches every file, so every file will be checked except the excluded types.\n\nDo you want to continue?`;
    `These types are in both File types and Exclude, so they will not be checked:\n{entries}\n\nDo you want to continue?`.
- Default file types (spec §3.3, sorted when shown): `.bmp .doc .docx .eml .gif .jpeg .jpg .msg .odt .pdf .png .ppt .pptx .rtf .tif .tiff .xls .xlsx .xps`.
- Project-file properties added to `AppSettings`: `ScanFolder[]? ScanFolders` (null → empty), `string[]? FileTypes` (null → the defaults),
  `string[]? ExcludeTypes` (null → empty), `bool GroupByFoundExtension = false`, `string UnindexedCustomFileName = "FileAuditorUnindexedReport.txt"`.
  `ScanFolder` is `sealed record ScanFolder(string Path, bool IncludeSubfolders = true)`.
- File-type matching is exactly spec §3.3: dot-separated parts counted from the end of the name, `*`/`?` within one part, a non-empty stem, case-insensitive;
  leading `.` or `*.` optional; the blacklist wins; overlaps go to the most specific whitelist entry (most parts, then fewest wildcard characters, then
  alphabetical).
- Concurrency: at most 16 folder listings at once. The SQL prefix filter is used for up to 20 folders; more, or any folder the escaping cannot represent, loads
  every row.
- All CLAUDE.md and SDD §12 conventions apply (pair rule: shared state without a view is not named `*ViewModel`). Commit each task automatically, staging only its
  files, ticking its boxes in this plan in the same commit.

## Review Focus

1. **Scale.** A person with a 3-million-row table and a folder tree of hundreds of thousands of files expects the run to finish in minutes with modest memory —
   Task 2 step 4 loads 1,000,000 LocalDB rows and reports the set's memory; Task 4 step 4 runs end to end over a generated tree of at least 100,000 files.
2. **Case and whitespace.** Index `U:\X\A.PDF ` and disk `u:\x\a.pdf` must count as indexed (anti-goal 1) — Task 2 step 4 and Task 4 step 4.
3. **An unreadable folder deep in a tree.** A person expects that folder listed once under *Unverified Folders* and its siblings still checked — Task 3 step 4
   (deny-list ACE on one subfolder).
4. **Links and nested inputs.** A junction loop must not hang or double-count; a file under two input folders appears once, under the deepest whose scope covers
   it — Task 3 step 4 (`mklink /J` loop; nested inputs with mixed *Include subfolders*).
5. **Pattern surprises.** `*.pdf` must match `Report.pdf`; `zip.*` must match `Backup.zip.001` but not `Backup.zip` or `Backup.zip.001.bak`; `.gitignore` and
   `README` match nothing; the app's own reports and `FileAuditorSettings.json` are never listed with the defaults; two colliding custom names are refused — Task 1
   step 4, Task 4 step 4, Task 5 step 6.

---

### Task 1: Settings, scan folders and file types

**Files:**

- Create: `Source/FileAuditor/Models/Settings/ScanFolder.cs`, `Source/FileAuditor/Services/Scanning/FileTypeList.cs`,
  `Source/FileAuditor/Services/Scanning/FileTypePattern.cs`, `Source/FileAuditor/Services/Scanning/FileTypeFilter.cs`
- Modify: `Models/Settings/AppSettings.cs`, `Services/Settings/SettingsStore.cs`, `Services/Settings/ProjectSettingsService.cs`,
  `Services/Reporting/ReportPathResolver.cs`, `ViewModels/Groups/ReportViewModel.cs`, `Common/GlobalUsings.cs`

**Interfaces:**

- Produces: `ScanFolder`; the five `AppSettings` properties (Global Constraints).
  - `static class FileTypeList` — `IReadOnlyList<string> Defaults`; `ErrorOr<string[]> Parse(string text)` (split on whitespace and commas; strip a leading `*.`
    or `.`, then add a single leading `.`; lower-case; drop duplicates; invalid per spec §4 — empty, an empty part, a path separator, or a forbidden file-name
    character other than `*` and `?`; error message per Global Constraints); `string Format(IEnumerable<string> types)` (sorted, space-separated).
  - `sealed class FileTypePattern` — built from one normalised entry: `string Text`, `int PartCount`, `int WildcardCount`, `bool IsWildcardOnly`,
    `bool Matches(string fileName)` (spec §3.3 rules), `string FoundExtension(string fileName)` (the name's last `PartCount` parts, lower case, with a dot).
  - `sealed class FileTypeFilter(IReadOnlyList<string> include, IReadOnlyList<string> exclude)` — `FileTypePattern? Match(string fileName)`: `null` if any
    exclude pattern matches or no include pattern does; otherwise the **most specific** matching include pattern (most parts, then fewest wildcards, then
    alphabetical). Patterns are compiled once; `Match` is safe to call concurrently.

- [x] **Step 1:** Add `ScanFolder` and the `AppSettings` properties; `SettingsStore` normalises them (null list → empty / defaults; null folder paths dropped; file
  types re-parsed through `FileTypeList.Parse`, invalid entries dropped).
- [x] **Step 2: Change detection by value.** Arrays break record equality, so `ProjectSettingsService.IsChanged` compares the two settings serialised with
  `SettingsJsonContext` instead of `!=`. Keep every other use of the baseline unchanged.
- [x] **Step 3:** `ReportPathResolver.Resolve` uses `UnindexedCustomFileName` for the Unindexed direction with a custom location; `ReportViewModel` maps it in
  `ApplyTo`/`Load` (remove milestone 3's "no setting yet" fallback and its doc remark).
- [x] **Step 4: Verify.** Clean build. A scratch harness asserting every row of spec §3.3's table, plus:
  - `Parse("pdf .PDF *.pdf tar.gz  .nii.gz, ZIP.*")` → `.nii.gz .pdf .tar.gz .zip.*`; `Parse("a/b")`, `Parse("a..b")`, `Parse(".")` → errors.
  - `*.pdf` matches `Report.pdf`; `zip.0??` matches `Backup.zip.001` but not `Backup.zip.01`; `.gitignore`, `README` and `x.` match nothing.
  - Filter include `[.gz, .tar.gz]` → `scan.tar.gz` matches `.tar.gz`; include `[.gz]` exclude `[.tar.gz]` → `scan.tar.gz` is `null`, `x.gz` is `.gz`;
    include `[zip.*, zip.001]` → `B.zip.001` matches `.zip.001`; `FoundExtension("B.ZIP.002")` for `zip.*` → `.zip.002`.
  - A project file round-trips folders, both lists and the grouping choice through `SettingsStore`; two equal settings with separate arrays compare unchanged.
  - Run the app: an untouched window still closes with no save prompt.
- [x] **Step 5: Commit** — `feat(✨): add scan folders and file types to project settings`.

### Task 2: Loading the index as path hashes

**Files:**

- Create: `Source/FileAuditor/Models/Auditing/PathQuery.cs`, `Source/FileAuditor/Services/Scanning/IndexPathSet.cs`
- Modify: `Services/Sql/QueryBuilder.cs`, `Services/Sql/IndexTableReader.cs`

**Interfaces:**

- Produces:
  - `sealed record PathQuery(string Sql, IReadOnlyList<KeyValuePair<string, string>> Parameters)`.
  - `QueryBuilder.BuildPaths(string table, string pathColumn, IReadOnlyList<string> folders) : ErrorOr<PathQuery>` — `SELECT [col] AS [Path] FROM [t]` plus,
    for 1–20 folders, `WHERE [col] COLLATE Latin1_General_100_CI_AS LIKE @p0 ESCAPE '|' OR …` with each parameter `folder` (trailing `\` ensured) with `|`, `%`, `_`
    and `[` escaped by `|`, followed by `%`. Values are parameters, never concatenated.
  - `IndexTableReader.ReadPathsAsync(string connectionString, PathQuery query, Action<string> onPath, CancellationToken ct) : Task<ErrorOr<Success>>` — the same
    cancellation and error mapping as `ReadAsync`; null values are skipped.
  - `sealed class IndexPathSet` — `void Add(string path)`, `bool Contains(string path)`, `int Count`; key = MD5 of the UTF-16 bytes of
    `path.Trim().ToUpperInvariant()` as `UInt128`, in a `HashSet<UInt128>`. Thread-safe for concurrent `Contains` after loading (single-threaded `Add`).

- [x] **Step 1:** `BuildPaths` and `PathQuery` (the existing `Build` stays for the missing direction).
- [x] **Step 2:** `ReadPathsAsync`.
- [x] **Step 3:** `IndexPathSet`, with an XML doc explaining the 128-bit choice (spec anti-goal 1).
- [x] **Step 4: Verify.** Clean build. Harness against LocalDB: a table with 1,000,000 paths under two roots plus 1,000 under a third → with the two roots as
  folders the reader returns exactly the first million (prefix filter works, case-insensitive with a `CS` column collation too); a folder containing `_`, `%`, `[`
  or `|` matches only itself; `Contains("u:\x\a.pdf")` is true after `Add(@"U:\X\A.PDF ")`; report load time and `GC.GetTotalMemory` before/after (expect tens of
  MB). Drop the database.
- [x] **Step 5: Commit** — `feat(✨): load index paths as 128-bit hashes`.

### Task 3: The folder walker

**Files:**

- Create: `Source/FileAuditor/Services/Scanning/FolderWalker.cs`, `Source/FileAuditor/Models/Auditing/ScannedFile.cs`,
  `Source/FileAuditor/Models/Auditing/UnverifiedFolder.cs`

**Interfaces:**

- Consumes: Task 1's `ScanFolder`, `FileTypeFilter`; the existing `Win32ErrorMapper.Map` / `CheckOutcome` reasons.
- Produces:
  - `sealed record ScannedFile(string Path, string InputFolder, FileTypePattern Type)`.
  - `sealed record UnverifiedFolder(string Path, CheckOutcome Reason, string? ReasonText, string? SystemMessage)` (path ends with `\`).
  - `sealed class FolderWalker` — `Task WalkAsync(IReadOnlyList<ScanFolder> folders, FileTypeFilter filter, Action<ScannedFile> onFile,
    Action<UnverifiedFolder> onUnverified, CancellationToken ct)`; `onFile` may be called concurrently.

- [x] **Step 1: Listing.** One non-recursive listing per folder (`FileSystemEnumerable` or `Directory.EnumerateFileSystemEntries` with `EnumerationOptions`:
  `AttributesToSkip = Hidden | System`, `IgnoreInaccessible = false`, `ReturnSpecialDirectories = false`), so attributes come from the listing with no per-file
  call. Subfolders queue for listing when their input folder includes subfolders. At most 16 listings run at once.
- [x] **Step 2: Rules (spec §3.2).**
  - A listing that throws → `onUnverified` with the mapped reason; nothing beneath it is listed. Only that folder is reported, not each of its descendants.
  - Files for which `filter.Match` returns null are skipped (not counted anywhere).
  - Links (junctions, symbolic links, DFS) are listed like folders; a folder whose resolved target (`DirectoryInfo.ResolveLinkTarget(returnFinalTarget: true)`
    for links, else the full path; compared ignoring case) was already listed in this run is skipped.
  - Each file is reported once (dedupe by full path, ignoring case) under the **deepest** input folder whose scope covers it (the folder itself, plus its
    subfolders when ticked).
  - Cancellation stops new listings promptly and returns.
- [x] **Step 3:** XML docs on the scope and attribution rules.
- [x] **Step 4: Verify.** Clean build. Harness over a scratch tree: hidden and system files skipped; `.txt`/`.json` skipped with the defaults; with include
  `[.gz]` and exclude `[.tar.gz]`, `a.gz` is reported and `b.tar.gz` is not;
  a subfolder with a deny-list ACE (`icacls … /deny "%USERNAME%":(RX)`, removed afterwards) appears once as *Permission denied* and its
  siblings are walked; a `mklink /J` junction pointing back to an ancestor terminates and lists no file twice; inputs `A` (subfolders) and `A\B` (no subfolders):
  `A\B\x.pdf` is attributed to `A\B`, `A\B\C\y.pdf` to `A`, and nothing appears twice. Remove the ACE and junction, delete the tree.
- [x] **Step 5: Commit** — `feat(✨): walk folders concurrently for the unindexed audit`.

### Task 4: The unindexed audit runner and report

**Files:**

- Create: `Source/FileAuditor/Services/Auditing/UnindexedAuditRunner.cs`, `Source/FileAuditor/Services/Auditing/UnindexedProgress.cs`,
  `Source/FileAuditor/Models/Reporting/UnindexedReport.cs`, `Source/FileAuditor/Services/Reporting/UnindexedReportBuilder.cs`,
  `Source/FileAuditor/Services/Reporting/ReportFormat.cs`
- Modify: `Services/Reporting/ReportBuilder.cs` (use `ReportFormat`; output byte-for-byte unchanged)

**Interfaces:**

- Consumes: Tasks 1–3; `AuditRequest(AppSettings Settings, ReportPath ReportPath)`; `ReportWriter.Write`; `AuditErrors.StoppedBeforeChecking`; `AuditResultKind`.
- Produces:
  - `UnindexedProgress` — thread-safe `Phase`, `TotalFiles`, `Indexed`, `Unindexed`, `Unverified`, `void Record(bool indexed)`, `void RecordUnverified()`.
  - `sealed record UnindexedReport(AppSettings Settings, DateTime StartedUtc, DateTime FinishedUtc, AuditResultKind Kind, IReadOnlyList<ScannedFile> Unindexed,
    IReadOnlyList<UnverifiedFolder> Unverified, long TotalFiles, long Indexed)`.
  - `UnindexedAuditRunner.RunAsync(AuditRequest request, UnindexedProgress progress, CancellationToken ct) : Task<ErrorOr<UnindexedReport>>` — phases per Global
    Constraints; SQL failure while loading → its error (no report); Stop while loading → `AuditErrors.StoppedBeforeChecking`; Stop while walking → a
    `StoppedByUser` report; report write failure → `Report.Unwritable`.
  - `UnindexedReportBuilder.Build(UnindexedReport report) : string`; `static class ReportFormat` holding the helpers both builders share (label padding,
    counts, timestamps, elapsed, flattening, the common header lines).

- [ ] **Step 1:** Extract `ReportFormat` from `ReportBuilder` without changing its output (compare a built missing report before and after, byte for byte).
- [ ] **Step 2:** `UnindexedProgress`, `UnindexedReport`, `UnindexedAuditRunner` (load the set, then walk, checking each file with `Contains` as it arrives).
- [ ] **Step 3:** `UnindexedReportBuilder` — exactly spec §3.7: header (no ID column line; `File types:`, `Exclude:`, `Grouped by:`), Summary, *Unindexed Files*
  grouped by input folder (input order) → `  [<type> (n)]` (alphabetical), where `<type>` is the matched pattern's `Text` (*Listed type*) or its
  `FoundExtension` (*Found extension*, per `settings.GroupByFoundExtension`) → paths sorted ordinal-ignore-case, indented two spaces;
  *Unverified Folders* by the SDD §6.3 reason order with *Other errors*
  columns; the stopped report has no *Not checked* line.
- [ ] **Step 4: Verify.** Clean build. Harness against LocalDB: a generated tree of at least 100,000 files over two input folders, with the index holding all but a
  known 50 (some differing only in case or trailing spaces in the index) plus a deny-list subfolder → the report lists exactly those 50, grouped and sorted as
  specified, the deny-list folder under *Unverified Folders*, and Total = Indexed + Unindexed. A `FileAuditorSettings.json` and a `.txt` report inside the tree are
  not listed. With `zip.*` listed and `B.zip.001`/`B.zip.002` unindexed: *Listed type* gives one `[.zip.* (2)]` group, *Found extension* gives `[.zip.001 (1)]`
  and `[.zip.002 (1)]`. Stop during the walk → a `Stopped by user` report. Record elapsed time and peak working set. Clean up the tree, ACE and database.
- [ ] **Step 5: Commit** — `feat(✨): run the unindexed audit and write its report`.

### Task 5: The Unindexed Files tab

**Files:**

- Create: `Source/FileAuditor/ViewModels/Tabs/UnindexedFilesTabViewModel.cs`, `Source/FileAuditor/Views/Tabs/UnindexedFilesTabView.axaml(.cs)`,
  `Source/FileAuditor/ViewModels/Groups/UnindexedAuditViewModel.cs`, `Source/FileAuditor/Views/Groups/UnindexedAuditView.axaml(.cs)`,
  `Source/FileAuditor/ViewModels/Groups/ScanFolderItem.cs`, `Source/FileAuditor/ViewModels/Groups/AuditStatusBase.cs`
- Modify: `ViewModels/Groups/AuditViewModel.cs`, `ViewModels/Main/MainWindowViewModel.cs`, `Views/Main/MainWindow.axaml`, `Services/Auditing/PreflightChecker.cs`,
  `Common/GlobalUsings.cs`

**Interfaces:**

- Consumes: Tasks 1–4.
- Produces: `AuditStatusBase : ObservableObject` (`StatusMessage`, `StatusPercent`) as the base of both Audit view models; `StatusAudit` returns it.
  `ScanFolderItem : ObservableObject` (`Path`, `IncludeSubfolders`). `UnindexedFilesTabViewModel(ReportLocationState shared)` — `Folders`
  (`ObservableCollection<ScanFolderItem>`), `SelectedFolder`, `FileTypesText`, `ExcludeText`, `GroupByFoundExtension`, `Report` (Unindexed),
  `Audit : UnindexedAuditViewModel`, `ApplyTo`/`Load`; `MainWindowViewModel.UnindexedFiles`. The grouping radio buttons sit in the Unindexed tab's
  `ReportView` only (for example a slot in `ReportView` shown when its direction is Unindexed, bound to the tab's `GroupByFoundExtension`).

- [ ] **Step 1: The tab** (mock-up right panel): *Folders to Check* group with a list of folders (each row: the path and an `Include subfolders` checkbox, ticked by
  default), `Add...` (folder picker, appends; a folder already listed is not added twice) and `Remove` (enabled with a selection); `File t_ypes:` text box with
  `Reset`; `E_xclude:` text box; then `ReportView` (with the *Group file types by* radios) and `UnindexedAuditView`. Both type boxes accept free text, parsed on
  Start and when saved.
- [ ] **Step 2: The Audit group** — the six statistics in Global Constraints (no Remaining); `Files/second` follows milestone 3's rule (files found ÷ the whole-run
  elapsed, blank under one second); `ProgressBar.IsIndeterminate` while running, full at the end; the same View Report / Start / Stop buttons.
- [ ] **Step 3: Wiring** — every item in the `IsTabbed` checklist comment in `MainWindowViewModel`, then remove the comment's "Milestone 4" wording. Start runs
  `UnindexedAuditRunner` for the Unindexed direction; one run at a time; tabs stay switchable during a run; Stop and the status bar follow the running audit.
  If `MainWindowViewModel` would pass about 750 lines, move the per-direction run code into a helper class rather than growing it further, and say so.
- [ ] **Step 4: Pre-flight** — the Global Constraints errors for the Unindexed direction (folders fully qualified and existing — probed off the UI thread; no
  duplicates; valid entries in both lists; at least one file type; no wildcard-only Exclude entry), then the three warnings in spec §3.3's order, each skipped if
  already answered *Continue* this session (remembered in `ProjectSettingsService` beside the save-prompt answer and cleared by its `Reset`); and, for both
  directions, the custom-name collision when the custom location is used.
- [ ] **Step 5: Access keys** — `File t_ypes:` uses Y and `E_xclude:` uses X (both free on this tab); the grouping radios carry none if no free letter fits.
  Every other letter of `Add...`, `Remove` and `Reset` is taken by controls visible with
  this tab, so those three carry no access key, like OK/Cancel in message dialogues; record the ruling.
- [ ] **Step 6: Verify.** Clean build. In the app (UI Automation, LocalDB): build the tab's inputs, Add two folders (untick one), edit file types (`pdf tar.gz`),
  Reset restores the defaults; save, close, reopen — all restored; switching tabs during a run keeps the status bar on the run and Stop works; Start with no
  folders, a relative folder, a missing folder, a duplicate, an empty type list, an invalid type, `*` in Exclude, and colliding custom names → each error;
  `* .pdf` → the redundancy warning, then *Continue* is not asked again until *Close project*; `*` with Exclude `.tmp` → the "every file except" warning; `.pdf`
  in both lists → the overlap warning; a real run over a small
  tree writes `FileAuditorUnindexedReport_Interim.txt`, View Report opens it; the Missing Files tab still runs as before. Screenshot both tabs. Clean up.
- [ ] **Step 7: Commit** — `feat(✨): add the Unindexed Files tab`.

### Task 6: Documentation sync and ledger

**Files:**

- Modify: `Docs/Specs/1-file-auditor-master-design.md`, `Docs/Plans/Epic-1-MVP/milestone-4-unindexed-audit.md` (tick boxes)
- Create: `Docs/Ledgers/Epic-1-MVP/milestone-4-ledger.md`
- Modify: `MediQuant.FileAuditor.slnx`

- [ ] **Step 1:** Amend the SDD in every section the spec's §5 lists (the second direction in §1–§2; the Unindexed tab, its groups, command states and
  one-run-at-a-time in §5; a new §6 subsection for running the unindexed audit; the unindexed report in §7; the project-file additions in §8; the new types in §9;
  §10/§11). Update the window diagram.
- [ ] **Step 2:** Write the ledger (rulings, user decisions, plan defects, outstanding items by when to revisit, incidents, not exercised, commits, final review
  "Pending.").
- [ ] **Step 3: Verify** — clean build; Markdown prose ≤ 180 characters; every file under `Docs/` is in the `.slnx`.
- [ ] **Step 4: Commit** — `docs(📝): sync the SDD with the unindexed audit and close the milestone 4 ledger`.
