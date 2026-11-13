# Milestone 2 — Unindexed Audit Statistics Implementation Plan

**Date:** 2026-11-13

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Unindexed Files tab shows two new live statistics, *Excluded* and *Index paths*, with *Indexed* moved to the top of the second column, and the unindexed report's
Summary mirrors the screen.

**Architecture:** `FolderWalker.WalkAsync` gains a third callback, called once per file the type filters skip; `IndexPathSet.Add` returns whether the path was new. The runner
bumps two new counters on `UnindexedProgress` from those, the view model shows them, and `UnindexedReport` carries them to the Summary.

**Tech Stack:** unchanged (.NET 10, Avalonia 11.3 + Classic.Avalonia, CommunityToolkit.Mvvm, ErrorOr, Microsoft.Data.SqlClient). No new packages.

**Spec:** [`Docs/Specs/Epic-2-Audit-Optimisation/milestone-2-unindexed-stats-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-2-unindexed-stats-design.md),
with the master SDD [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) §5.4a, §6.5 and §7.1, and §9 and §12 for code shape and conventions.

## Global Constraints

- **No automated tests and no test projects.** Verify with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings) and by running
  the app, driven by Windows UI Automation from PowerShell (foreground check before any keystroke or click; reports read with `Get-Content`; close the app when done).
- SQL Server for verification: LocalDB `(localdb)\MSSQLLocalDB`, scratch database `FileAuditorE2M2Scratch`, dropped when the task ends. Throwaway folders and harnesses live in the
  session scratchpad, never in the repository.
- **Labels, exactly:** `Excluded:` and `Index paths:` (screen and report).
- **Screen layout** (three columns, three rows): column 1 `Total files:`, `Excluded:`, `Index paths:`; column 2 `Indexed:`, `Unindexed:`, `Unverified:`; column 3 `Elapsed:`,
  `Files/second:` (row 3 empty). Values formatted `N0` like the others, blank before the first run and after `Reset`.
- **Report Summary order:** `Total files:`, `Excluded:`, `Index paths:`, `Indexed:`, `Unindexed:`, `Unverified:`.
- **Excluded** = files the walk met that no *File types* entry matches or an *Exclude* entry matches. Not hidden or system files (skipped before the filter, as today). A name with
  no extension counts. An excluded symbolic-link file counts as Excluded (the filter runs before the link probe, as today). A folder that cannot be listed reports none. Never part
  of Total files; Total files = Indexed + Unindexed stays exact.
- **Index paths** = the number of distinct keys in the `IndexPathSet` (trimmed, case-folded), counted as they are added, so it is live during `Loading the index...`.
- Nothing else changes: what is checked, the query, the Missing Files tab, the window size (the Unindexed tab sits in a scroll view).
- All CLAUDE.md and SDD §12 conventions apply. Commit each task automatically, staging only its files and ticking its boxes here.

## Review Focus

1. **The sum.** Total files = Indexed + Unindexed on screen and in the report, with Excluded outside it — Task 1 step 4.
2. **Index paths counts keys, not rows.** Case variants, a leading-space variant and exact duplicates of one path count once; a `NULL` row counts nothing — Task 1 step 4.
3. **Filter off.** With more than 20 input folders the whole table is loaded, so Index paths includes rows outside the folders — Task 1 step 4.
4. **Hidden files and no-extension files.** A hidden `.pdf` is in no count; `README` is Excluded — Task 1 step 4.
5. **Reset and a second run.** The new values blank on `Reset` and start from zero on the next run — Task 1 step 4.

---

Before Task 1 the controller creates branch `feat/milestone-2-unindexed-stats` from `main`, adds the design doc, this plan and the ledger
(`Docs/Ledgers/Epic-2-Audit-Optimisation/milestone-2-ledger.md`) to `MediQuant.FileAuditor.slnx` after milestone 1.1's entries, and commits them as
`docs(📝): add the unindexed statistics design and plan`.

### Task 1: The two statistics, on screen and in the report

**Files:**

- Modify: `Source/FileAuditor/Services/Scanning/FolderWalker.cs`, `Services/Scanning/IndexPathSet.cs`, `Services/Auditing/UnindexedProgress.cs`,
  `Services/Auditing/UnindexedAuditRunner.cs`, `Models/Reporting/UnindexedReport.cs`, `Services/Reporting/UnindexedReportBuilder.cs`,
  `ViewModels/Groups/UnindexedAuditViewModel.cs`, `Views/Groups/UnindexedAuditView.axaml`

**Interfaces:**

- Produces: `FolderWalker.WalkAsync(IReadOnlyList<ScanFolder> folders, FileTypeFilter filter, Action<ScannedFile> onFile, Action<UnverifiedFolder> onUnverified, Action onExcluded,
  CancellationToken cancellationToken)` — `onExcluded` is called once per file `_filter.Match` returns `null` for, from the walker's threads (so the callee must be thread-safe).
- Produces: `IndexPathSet.Add(string path) : bool` — `true` when the key was not already in the set.
- Produces: `UnindexedProgress.Excluded`, `UnindexedProgress.IndexPaths` (`long`, read with `Interlocked.Read`), `RecordExcluded()`, `RecordIndexPath()` (`Interlocked.Increment`).
- Produces: `UnindexedReport(…, long TotalFiles, long Indexed, long Excluded, long IndexPaths, string? FailureMessage = null)`.
- Produces: `UnindexedAuditViewModel.Excluded`, `UnindexedAuditViewModel.IndexPaths` (`string` observable properties).

- [x] **Step 1: Count.** Add the walker callback and the `Add` result; the runner passes `progress.RecordExcluded` to the walk and, for the load,
  `path => { if (paths.Add(path)) progress.RecordIndexPath(); }`. Update the XML docs (including `TotalFiles`'s, which stays Indexed + Unindexed).
- [x] **Step 2: Report.** `UnindexedReport` gets the two counts from the runner (`progress.Excluded`, `progress.IndexPaths`); `UnindexedReportBuilder.AddSummary` writes the
  Summary in the Global Constraints order.
- [x] **Step 3: Screen.** The view model sets and resets the two values; the view's grid becomes three rows in the Global Constraints layout. Keep the column widths unless
  `Index paths:` does not fit its column at the default font, in which case widen column 0 only.
- [x] **Step 4: Verify.** Clean build. Then, with a local folder `Root` in the scratchpad and `FileAuditorE2M2Scratch`:
    - `Root` holds `a.pdf`, `b.pdf`, `c.pdf`, `d.pdf`, `notes.txt`, `log.txt`, `README` (no extension) and a hidden `h.pdf`. File types `.pdf`, Exclude empty, no subfolders.
    - Table `dbo.Idx (Id int, P nvarchar(400))` with rows `<Root>\a.pdf`, `<ROOT>\A.PDF` (upper case), `' <Root>\b.pdf'` (leading space), `<Root>\c.pdf` twice, `<Root>\gone.pdf`,
      `X:\Elsewhere\z.pdf`, and `NULL`.
    - Run on the Unindexed tab with `Root` as the only folder. Expect: Total files 4, Excluded 3, Index paths 4, Indexed 3, Unindexed 1 (`d.pdf`), Unverified 0, on screen (read
      by UI Automation) and in the report's Summary in the Global Constraints order.
    - Run again with `Root` plus 20 empty sibling folders as inputs (21, so the filter is dropped). Expect Index paths 5 (`z.pdf` now loaded) and every other figure unchanged;
      the screen values start from zero, not from the first run's.
    - If the account can create a symbolic link, add `link.txt` → `notes.txt` and expect Excluded 4 and no *Link not followed* entry; otherwise say so in the report.
    - Close the app; drop the database.
- [x] **Step 5: Commit** — `feat(✨): show excluded files and index paths in the unindexed audit`.

### Task 2: SDD, user guide and mock-ups

**Files:** Modify `Docs/Specs/1-file-auditor-master-design.md`, `Docs/UserGuide.md`, `Docs/UserInterfaceDesign/Mockups/row-accounting-stats.html`,
`Docs/UserInterfaceDesign/Mockups/unindexed-report-layout.html`

- [ ] **Step 1: SDD.** §5.4a (the layout and the two statistics' meaning), §6.5.2 (Index paths counted as keys are added), §6.5.3 (Excluded counted by the walk), §6.5.4 (Stop
  shows what was reached), §7.1 (the Summary block and its bullet), §10 (this milestone's calls, from the design's §3 and §5). Remove anything this makes false, including the
  "Total files and Indexed on the left" wording.
- [ ] **Step 2: User guide.** The Unindexed statistics table (~line 475) and the sample Summary (~line 518): add both, in the new order. One line says Index paths is the paths
  loaded to compare against: the rows under the folders plus any that start with a space, or the whole table with more than 20 folders. A short FAQ entry: why Total files is
  smaller than the files Explorer shows (Excluded, hidden and system files). Hard-wrap at 180 characters.
- [ ] **Step 3: Mock-ups.** `unindexed-report-layout.html`: the Summary block. `row-accounting-stats.html`: add a panel for the Unindexed Audit group's statistics, in the
  layout and style of the Missing Files panel already there.
- [ ] **Step 4: Commit** — `docs(📝): document the unindexed audit's new statistics`.
