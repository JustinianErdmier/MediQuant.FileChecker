# Milestone 6 — Streaming Missing Files Audit Implementation Plan

**Date:** 2026-11-14

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Missing Files audit runs 1.0.0's two statements (`COUNT_BIG(*)` and a plain select) and classifies, de-duplicates and collects empty-path IDs in the app,
with every milestone 1 statistic counted live and a new *Distinct* statistic; the Unindexed folder filter converts to `nvarchar(4000)` at most.

**Architecture:** `QueryBuilder.Build` returns the two plain statements. The single reader classifies each row (skip marker, empty, then a 128-bit key looked up in a new
key set) and only distinct real paths enter the channel to be checked; live counters on `AuditProgress` feed the Audit group, whose progress and *Remaining* move by rows
handled. The report's Summary mirrors the screen.

**Tech Stack:** unchanged (.NET 10, Avalonia 11.3, Microsoft.Data.SqlClient). No new packages.

**Spec:**
[`Docs/Specs/Epic-2-Audit-Optimisation/milestone-6-streaming-missing-audit-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-6-streaming-missing-audit-design.md),
with the master SDD [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) §5.4, §6.2, §6.4, §6.5.2, §7, §9, §12.

## Global Constraints

- **No automated tests and no test projects.** Verify with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings), with
  LocalDB harnesses in the scratchpad calling the real code, and by running the app.
- **Statements (Missing Files):** `SELECT COUNT_BIG(*) FROM <table>;` and `SELECT <path> AS [Path][, <id> AS [Id]] FROM <table>;` — identifiers quoted as today, no
  conversion, no `WHERE`, no `DISTINCT`, no parameters. The type check before them is unchanged (pre-flight's lookup still handed to the run).
- **Per row, in this order:** `SkipMarker.IsSkipped` → *Skipped*; `string.IsNullOrWhiteSpace` → empty path (its ID kept when there is an ID column); otherwise the key →
  seen before: *Duplicates*; new: *Distinct* and checked. Skipped, empty and duplicate rows are never checked.
- **Key:** `path.TrimEnd(' ')` (U+0020 only) upper-cased with `ToUpperInvariant`, plus, with an ID column, the ID as read (joined so that `("a", "bc")` and `("ab", "c")`
  differ), hashed to 128 bits with MD5 and kept in a set; nothing else trimmed. `IndexPathSet` is not reused as it is (it trims both ends).
- **Rows meaning (the user's decision):** *Distinct* = different real paths first seen (never decreases); *Duplicates* = each extra row whose key was already seen, one per
  row; *Skipped* and empty paths count rows.
- **Sums:** completed run — Found + Missing + Unverified = Checked = Distinct; Distinct + Duplicates + empty paths + Skipped = Total files. Stopped run — *Not checked* =
  max(0, Total files − (Checked + Duplicates + empty paths + Skipped)), so Checked + Duplicates + empty paths + Skipped + Not checked = Total files.
- **Screen (Audit group, four rows, three columns):** column 1 `Total files:`, `Distinct:`, `Duplicates:`, `Checked:`; column 2 `Found:`, `Missing:`, `Unverified:`,
  `Skipped:`; column 3 `Elapsed:`, `Remaining:`, `Files/second:`. *Total files* is the row count, fixed once counting ends; everything else live. *Unverified* keeps its
  bracket for the empty paths, hidden while it is 0, as today. Values formatted as today.
- **Progress, status-bar percentage and *Remaining*:** rows handled / Total files, where a skipped, empty or duplicate row is handled when read and a distinct row when its
  check ends; *Remaining* from the recent rate of rows handled. **Files/second** (both figures) stays distinct files checked per second, with its own recent rate.
- **Report Summary order:** `Total files:` (rows), `Distinct:`, `Duplicates:`, `Found:`, `Missing:`, `Unverified:` (with the bracket as today), `Skipped:`, and
  `Not checked:` for a stopped run. The `distinct (rows)` form is gone.
- **Empty-path IDs:** collected during the stream and listed in the *Unverified — Path is empty* group; `Missing filepaths not listed because no ID column was specified` stays;
  a stopped or failed run lists the IDs read so far; the `...because the audit was stopped`, `...because the audit failed`, `...because they could not be read` notes and the
  `Reading empty paths...` phase are removed.
- **Unindexed filter:** for a `varchar` column, each filter `LIKE` compares `CAST(<column> AS nvarchar(4000))`; `nvarchar` columns and runs without a filter unchanged.
- **Warning copy:** `The path column '<column>' is <type>.\nThe File Auditor prefers an nvarchar column. A varchar column may have changed some characters in its paths when
  they were stored. See "varchar Path Columns" in the user guide.\n\nDo you want to continue?` — everything else about the warning unchanged.
- The version stays 2.0.0. All CLAUDE.md and SDD §12 conventions apply. Commit each task automatically, staging only its files and ticking its boxes here.

## Review Focus

1. **The sums hold** on a completed run and on a stopped run (stop part-way), on screen and in the report — Task 1 step 5, Task 2 step 3.
2. **The key.** `A.PDF` / `a.pdf` and `x.pdf` / `x.pdf  ` are duplicates; `" x.pdf"`, `"x.pdf\t"` and `Straße` / `Strasse` are distinct; with an ID column, the same path with
   two IDs is two *Distinct* — Task 1 step 5.
3. **The same figures as 2.0.0** on milestone 1's boundary seed and on a duplicate-heavy table (except where §5's key intentionally differs, `ß`), and every distinct path
   checked exactly once — Task 1 step 5.
4. **Rows start at once.** On the 1,570,465-row `varchar(8000)` table, the time from Start to the first row read is under a second (2.0.0: about 18 s locally) — Task 1 step 5.
5. **Unindexed with a long path.** A `varchar(8000)` column with a path over 4,000 characters under a filtered folder is matched as before — Task 1 step 5.

---

Before Task 1 the controller creates branch `feat/milestone-6-streaming-missing-audit` from `main`, adds the design, this plan and the ledger
(`Docs/Ledgers/Epic-2-Audit-Optimisation/milestone-6-ledger.md`) to `MediQuant.FileAuditor.slnx` after milestone 5's entries, and commits them as
`docs(📝): add the streaming Missing Files audit design and plan`.

### Task 1: The engine and the report

**Files:** Modify `Source/FileAuditor/Services/Sql/QueryBuilder.cs`, `Services/Sql/IndexTableReader.cs`, `Services/Sql/PathColumnType.cs`, `Models/Auditing/AuditQuery.cs`,
`Models/Auditing/RowCounts.cs` (or remove it), `Services/Auditing/AuditRunner.cs`, `Services/Auditing/AuditProgress.cs`, `Services/Auditing/PreflightChecker.cs`,
`Models/Reporting/AuditReport.cs`, `Services/Reporting/ReportBuilder.cs`; Create the key set (e.g. `Services/Auditing/PathKeySet.cs`).

**Interfaces:**

- Produces: `AuditProgress` live members (read with `Interlocked`): `TotalRows`, `Distinct`, `Duplicates`, `Skipped`, `EmptyPaths`, `Checked`, `Found`, `Missing`,
  `Unverified`, `RowsHandled` (= Checked + Duplicates + EmptyPaths + Skipped), plus the existing clock members; `SetTotal(long rows)` when counting ends.
- Produces: the report record carries the final counts (rows, distinct, duplicates, skipped, empty paths, found, checked) and the empty-path IDs (or the no-ID note).
- Consumes (unchanged): `SkipMarker.IsSkipped`, `FileChecker.Check`, the type check and pre-flight's handed-over lookup.

- [x] **Step 1: Statements.** `QueryBuilder.Build(table, pathColumn, idColumn)` returns the two plain statements; the classification SQL, `@ws` and the empty-ID statement go.
  `PathColumnType.Check` keeps accepting `nvarchar` and `varchar` (same errors); the Unindexed filter's target for `varchar` is `nvarchar(4000)`. `IndexTableReader` keeps a
  count and a stream; the counts-statement and empty-ID readers go.
- [x] **Step 2: The runner.** Count → `SetTotal`; stream; classify per Global Constraints in the reader; only distinct real paths enter the channel; the 32-way checking is
  unchanged; empty IDs collected; no `Reading empty paths...` phase.
- [x] **Step 3: The report.** Summary order and *Not checked* per Global Constraints; the empty-path group from the collected IDs; the notes as specified.
- [x] **Step 4: The warning.** The new copy in `PreflightChecker`.
- [x] **Step 5: Verify.** Clean build. With a harness in the scratchpad calling the real runner against LocalDB:
    - milestone 1's boundary seed (milestone 1's plan, Task 1 Step 3) with and without an ID column: the final figures and empty IDs, compared with 2.0.0's (build the
      `v2.0.0` runner in the harness from `git show v2.0.0:…`, or use the figures milestone 1's ledger records);
    - a table with the Review Focus 2 cases (case, trailing spaces, leading space, trailing tab, `ß`, the same path with two IDs): the exact *Distinct* / *Duplicates*;
    - every distinct path checked exactly once (count calls to the checker, or compare against the report);
    - a stopped run (cancel part-way): the sums of Global Constraints hold in the report;
    - the existing `FileAuditorPerf` database (1,570,465 rows, `varchar(8000)`; recreate it if missing with the seed in the ledger's notes): time from start to the first row
      read, against the `v2.0.0` runner, three runs each;
    - Unindexed: a `varchar(8000)` table with one path over 4,000 characters under the chosen folder, with a filter: matched as indexed.
  Drop any scratch database you created (not `FileAuditorPerf`).
- [x] **Step 6: Commit** — `perf(⚡): stream the Missing Files audit and classify rows in the app`.

### Task 2: The Audit group

**Files:** Modify `Source/FileAuditor/ViewModels/Groups/AuditViewModel.cs`, `Views/Groups/AuditView.axaml`, `ViewModels/Groups/RemainingEstimator.cs` /
`RecentRate.cs` if needed, `ViewModels/Main/MissingAuditRun.cs`; `Docs/UserInterfaceDesign/Mockups/row-accounting-stats.html`.

**Interfaces:** Consumes Task 1's `AuditProgress` members.

- [ ] **Step 1: View model.** `Distinct` property; every figure live; progress and percentage by `RowsHandled / TotalRows`; *Remaining* from rows handled; *Files/second*
  from files checked with its own recent rate; `Reset` blanks *Distinct*; the finished dialogue's figures line unchanged in form.
- [ ] **Step 2: View and mock-up.** The four-row grid per Global Constraints (keep the column widths unless *Distinct* does not fit); the Missing Files panel of the mock-up
  updated to the new layout.
- [ ] **Step 3: Verify.** Clean build. In the app (UI Automation; project files opened through *File > Open project...*), against a LocalDB table of about 200,000 rows with
  duplicates, empty and skipped rows and local files: the figures move during the run, *Total files* stays fixed, the bar and *Remaining* move steadily, the final screen
  and report satisfy the sums; one stopped run. Close the app; drop the database.
- [ ] **Step 4: Commit** — `feat(✨): show live row accounting with a Distinct statistic`.

### Task 3: SDD, user guide and changelog

**Files:** Modify `Docs/Specs/1-file-auditor-master-design.md`, `Docs/UserGuide.md`

- [ ] **Step 1: SDD.** §5.4 (the grid and the live figures), §6.2 (the two statements; the classification, key and empty IDs in the app; the type rule unchanged; the warning's
  wording), §6.4 (progress by rows, stop, the removed phase), §6.5.2 (the `nvarchar(4000)` filter conversion), §7 (the Summary and the empty-path notes), §9 (new and removed
  members), §10 (this milestone's calls; mark milestone 1's SQL classification and up-front counts and milestone 4's Missing-tab conversion as superseded). Remove anything
  this makes false.
- [ ] **Step 2: User guide.** Section 4 (the statistics table, the sums, the sample report Summary), section 8.4 rewritten (only the Unindexed filter converts, to
  `nvarchar(4000)`; why; the effects of stored look-alikes and `?`), section 9 (the warning's new wording), section 8 / 10 wherever they describe counting, and the 2.0.0
  changelog entry (live counts and *Distinct*; the Summary's new lines). Hard-wrap at 180 characters, reflowing whole paragraphs.
- [ ] **Step 3: Commit** — `docs(📝): document the streaming Missing Files audit`.

After the PR is merged (not part of the PR): the controller deletes `Docs/Temp/` from `main` in a clean-up commit and the branches `diag/v1.0.0-debug-report` and
`diag/v2.0.0-debug-report` locally and on GitHub, and drops the `FileAuditorPerf` database.
