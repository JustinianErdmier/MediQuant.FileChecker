# Milestone 1 — Row Accounting Implementation Plan

**Date:** 2026-11-12

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Missing Files audit counts the table's rows, its empty-path rows, its skip-marker rows and its distinct real paths up front, checks only the distinct real paths,
shows *Duplicates*, *Found* and the new parenthesised figures in the Audit group and the report, and lists every empty-path row by ID at the end.

**Architecture:** `QueryBuilder` gains SQL predicates for "empty path" and "skip marker" that match `string.IsNullOrWhiteSpace` and `SkipMarker` exactly, and builds three
statements from them: one counts statement (four figures in one row), the distinct stream with both kinds filtered out, and the empty-path ID read. A new `RowCounts` record
carries the four figures through `AuditProgress`, `AuditRunner`, `AuditReport` and `ReportBuilder` to the screen and the report.

**Tech Stack:** unchanged (.NET 10, Avalonia 11.3, Classic.Avalonia, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient). No new packages.

**Spec:** [`Docs/Specs/Epic-2-Audit-Optimisation/milestone-1-row-accounting-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-1-row-accounting-design.md), with the
master SDD [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) §5.4–§5.5, §6.2–§6.4, §7 and §12. Approved mock-up:
[`row-accounting-stats.html`](../../UserInterfaceDesign/Mockups/row-accounting-stats.html). Groundwork: commit `b22d63c` (distinct queries, on `main`).

## Global Constraints

- **No automated tests and no test projects.** Verify with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings) and, for
  anything visible, by running the app and driving it (computer use, or Windows UI Automation from PowerShell as milestone 5 did). **Before any keystroke or click, confirm
  FileAuditor.exe is the foreground window.** Never open a report in an external program (read it with `Get-Content`). Close the app when done.
- SQL Server for verification: LocalDB `(localdb)\MSSQLLocalDB`, scratch database `FileAuditorE2M1Scratch`, dropped when the task ends. A throwaway harness, if one is needed,
  lives in the session scratchpad, never in the repository.
- Shell commands via PowerShell. No background commands, no filesystem-wide searches.
- **Row kinds (spec §3).** *Empty*: `NULL`, or only `char.IsWhiteSpace` characters (the ranges of `QueryBuilder.WhitespacePattern`), the empty string included. *Skip marker*:
  not empty, and after trimming those same characters from both ends, equal to `SKIP` or `SKIPPED` or starting with `SKIP:` or `SKIPPED:`, ASCII letters in either case
  (non-ASCII case folding is not handled, the user's call). Every other row is a real path. Duplicates = TableRows − Distinct − EmptyPaths − Skipped.
- **The table is assumed not to change during a run.** No reconciliation, no clamping; the old `Math.Max(total, processed)` guards are removed.
- Exact copy:
  - status `Reading empty paths...` (new; `Counting files...`, `Checking files...`, `Writing report...` unchanged);
  - stat labels `Total files:`, `Duplicates:`, `Checked:`, `Found:`, `Missing:`, `Unverified:`, `Skipped:`;
  - report Summary lines in this order: `Total files:`, `Duplicates:`, `Found:`, `Missing:`, `Unverified:`, `Skipped:`, then `Not checked:` for Stopped or Failed only;
  - in place of the empty-path IDs: `Missing filepaths not listed because no ID column was specified` (no ID column, whatever the result);
    `Missing filepaths not listed because the audit was stopped`; `Missing filepaths not listed because the audit failed`;
    `Missing filepaths not listed because they could not be read: <message>`.
- Parentheses: *Total files* is `D` or `D (T)` when T ≠ D; *Unverified* is `U` or `U (E)` when E > 0, U being the checked Unverified outcomes. Same on screen (`N0`,
  `CultureInfo.CurrentCulture`) and in the report (`ReportFormat.Count`, invariant).
- The sums: Found + Missing + U + Not checked = D; D + Duplicates + E + S = T.
- All CLAUDE.md and SDD §12 conventions apply. Commit each task automatically (conventional commit with gitmoji), staging only its files and ticking its boxes in this plan.

## Review Focus

1. **Classification boundaries.** `NULL`, `''`, spaces, a tab and an ideographic space are empty; ` skipped: preflight `, `Skip`, `SKIPPED:`, tab-led and tab-trailed `SKIP`
   are skipped; `SKIP preflight`, `SKIPPEDX` and `C:\SKIPPED\a.txt` are real paths — Task 1 step 3.
2. **No ID column with empty paths.** The group shows its count and the one no-ID line, never blank lines — Task 2 step 4.
3. **Stop mid-check on a table with duplicates, empties and skip markers.** The report's two sums hold, `Not checked:` appears, the empty group carries the stopped line —
   Task 2 step 4.
4. **A table with no real paths** (only empty and skip rows, or no rows at all). D = 0: the run completes, the bar reads 100%, nothing divides by zero, the report is written —
   Task 2 step 4 and Task 3 step 4.
5. **Nothing clipped.** `9,999,999 (99,999,999)` in *Total files* and `9,999,999 (9,999,999)` in *Unverified* fit; the window's new height leaves no gap — Task 3 step 4.

---

Before Task 1 the controller creates branch `feat/milestone-1-row-accounting` from `main`, adds the design, the mock-up, this plan and the ledger
(`Docs/Ledgers/Epic-2-Audit-Optimisation/milestone-1-ledger.md`) to `MediQuant.FileAuditor.slnx` in new `/Docs/…/Epic-2-Audit-Optimisation/` folders (and the mock-up under
`/Docs/UserInterfaceDesign/Mockups/`), and commits them as `docs(📝): add the row accounting design and plan`.

### Task 1: Classification and the three statements

**Files:**

- Create: `Source/FileAuditor/Models/Auditing/RowCounts.cs`
- Modify: `Source/FileAuditor/Models/Auditing/AuditQuery.cs`, `Source/FileAuditor/Services/Sql/QueryBuilder.cs`, `Source/FileAuditor/Services/Sql/IndexTableReader.cs`

**Interfaces:**

- Produces: `public sealed record RowCounts(long TableRows, long EmptyPaths, long Skipped, long Distinct) { public long Duplicates { get; } }` (Duplicates computed from the
  others).
- Produces: `public sealed record AuditQuery(string CountsSql, string SelectSql, string? EmptyIdsSql, IReadOnlyList<KeyValuePair<string, string>> Parameters, bool HasIdColumn)`
  — `EmptyIdsSql` is `null` without an ID column; the parameters apply to all three statements.
- Produces: `IndexTableReader.CountRowsAsync(string connectionString, AuditQuery query, CancellationToken cancellationToken) : Task<ErrorOr<RowCounts>>` (replaces
  `CountAsync`); `IndexTableReader.ReadEmptyIdsAsync(string connectionString, AuditQuery query, CancellationToken cancellationToken) : Task<ErrorOr<IReadOnlyList<string>>>`
  (each row's ID as text, `NULL` → empty); `ReadAsync` unchanged in signature. Errors and cancellation behave as the existing methods do.

- [x] **Step 1: Predicates.** In `QueryBuilder`, build the white-space character class once (from the existing `WhitespacePattern` ranges) and pass it as a parameter
  (`NVarChar`, as `ReadPathsAsync` adds them; move that loop into a shared private helper in `IndexTableReader`). Compute the trimmed value once per row with `CROSS APPLY`,
  every comparison under `COLLATE Latin1_General_100_BIN2`. The shape (`<c>` is the quoted path column, `@ws` the class body without brackets, e.g. `\t-\r …`):

  ```sql
  FROM <table>
  CROSS APPLY (SELECT PATINDEX(N'%[^' + @ws + N']%', <c> COLLATE Latin1_General_100_BIN2)            AS Lead,
                      PATINDEX(N'%[^' + @ws + N']%', REVERSE(<c>) COLLATE Latin1_General_100_BIN2)   AS Trail,
                      LEN(CAST(<c> AS nvarchar(max)) + N'x') - 1                                     AS Length) AS [n]
  CROSS APPLY (SELECT CASE WHEN <c> IS NULL OR [n].Lead = 0 THEN 1 ELSE 0 END AS IsEmpty,
                      SUBSTRING(CAST(<c> AS nvarchar(max)), [n].Lead, [n].Length - [n].Lead - [n].Trail + 2) AS Trimmed) AS [k]
  -- skip marker: [k].IsEmpty = 0 AND ([k].Trimmed COLLATE BIN2 LIKE N'[Ss][Kk][Ii][Pp]' OR … N'[Ss][Kk][Ii][Pp][Pp][Ee][Dd]'
  --                                   OR … N'[Ss][Kk][Ii][Pp]:%' OR … N'[Ss][Kk][Ii][Pp][Pp][Ee][Dd]:%')
  ```

  The builder may restructure (one `CROSS APPLY`, a `CASE` column for the kind) provided the classification is exactly the Global Constraints rule.
- [x] **Step 2: Statements.** `CountsSql` returns one row, four `bigint` columns in the order of `RowCounts`: `COUNT_BIG(*)`, the empty count, the skip count, and
  `COUNT_BIG(*)` over the distinct stream. `SelectSql` is `SELECT DISTINCT <c> AS [Path][, <id> AS [Id]] … WHERE <not empty> AND <not skip>;`. `EmptyIdsSql` is
  `SELECT <id> AS [Id] … WHERE <empty>;` (not distinct). `CountRowsAsync` and `ReadEmptyIdsAsync` follow `CountAsync`/`ReadAsync` (no command timeout, cancellation registered,
  `SqlException` mapped the same way); all three reader methods add `query.Parameters` to their command. Update the XML docs of everything touched.
- [x] **Step 3: Verify.** Clean build. In `FileAuditorE2M1Scratch`, table `dbo.Idx (Id int, P nvarchar(400))` with: `C:\s\a.txt` ×3 and `c:\S\A.TXT`, all `Id` 1; `C:\s\b.txt`
  with `Id` 2 and 3; empty `NULL`, `''`, `'   '`, `NCHAR(9)`, `N' ' + NCHAR(0x3000)` (`Id` 10–14); skip `SKIP` ×2, `' skipped: preflight '`, `Skip`, `SKIPPED:`,
  `NCHAR(9) + 'SKIP'`, `'SKIP' + NCHAR(9)`; real `SKIP preflight`, `SKIPPEDX`, `C:\SKIPPED\a.txt`, `C:\s\missing.txt`. Run the built statements (print them from a scratchpad
  harness referencing the built `FileAuditor.dll`, or copy them from a debugger, then `sqlcmd`/`Invoke-Sqlcmd`). Expected with ID column `Id`: T 22, E 5, S 7, D 7, Dup 3;
  without: D 6, Dup 4. `SelectSql` returns exactly the D real rows; `EmptyIdsSql` returns 10–14. Keep the database for Task 2.
- [x] **Step 4: Commit** — `feat(✨): count empty paths and skip markers in SQL and stream only distinct real paths`.

### Task 2: Runner, progress and report

**Files:** Modify `Source/FileAuditor/Services/Auditing/AuditProgress.cs`, `Services/Auditing/AuditRunner.cs`, `Models/Reporting/AuditReport.cs`,
`Services/Reporting/ReportBuilder.cs`

**Interfaces:**

- Consumes: `RowCounts`, `AuditQuery`, `CountRowsAsync`, `ReadEmptyIdsAsync` (Task 1).
- Produces on `AuditProgress`: `void SetCounts(RowCounts counts)` (replaces `SetTotal`, starts the checking clock); `RowCounts? Counts`; `long Found` (new counter);
  `long Checked`, `long Missing`, `long Unverified` as today; `bool HasTotal`; `TimeSpan CheckingElapsed`; `TotalFiles` and `Skipped` stay, now read from `Counts`
  (`Distinct`, `Skipped`; 0 before counting), so `AuditViewModel` still compiles until Task 3 reworks it. Task 3 consumes these.
- Produces: `AuditReport(AppSettings Settings, DateTime StartedUtc, DateTime FinishedUtc, AuditResultKinds Result, string? FailureMessage, RowCounts Counts, long FoundCount,
  IReadOnlyList<CheckResult> Problems, IReadOnlyList<string>? EmptyPathIds, string? EmptyPathNote)` — when `Counts.EmptyPaths > 0`, exactly one of the last two is non-null.

- [x] **Step 1: Progress.** `Record` counts Found (new), Missing and Unverified and increments `Checked` for each. The checker keeps its own skip and empty tests, but the
  stream excludes those rows, so a `Skipped` outcome should never arrive: if one does, record it under Unverified (never throw, never touch the counted Skipped) and keep it
  out of the report's empty group. Likewise a `PathEmpty` outcome stays an ordinary Unverified problem. Note this safety net in the ledger.
- [x] **Step 2: Runner.** Count with `CountRowsAsync` (Stop → `StoppedBeforeChecking`, as now), `SetCounts`, stream and check as now. Afterwards:
  - result as now, except that "no report" is `progress.Checked is 0` on failure;
  - when `Counts.EmptyPaths > 0`: no ID column → the no-ID line; *Stopped* → the stopped line; *Failed* → the failed line; *Completed* → status `Reading empty paths...` and
    `ReadEmptyIdsAsync` with the caller's token: success → the IDs; cancelled → the stopped line, result stays *Completed*; error → the could-not-be-read line with the
    message, result unchanged;
  - build `AuditReport` with `Counts` as counted (no `Math.Max`).
- [x] **Step 3: Report.** Summary per Global Constraints (`Not checked:` = `Counts.Distinct − FoundCount − Problems.Count`). *Unverified Files* heading count =
  unverified problems + E. The *Path is empty in the index table (E)* group sits in its usual place in the Unverified order and is built from `Counts`/`EmptyPathIds`/
  `EmptyPathNote`, not from `Problems`: one `<IdColumn> <id>` line per ID (sorted `OrdinalIgnoreCase` then `Ordinal`), or the one note line. Update the XML docs.
- [x] **Step 4: Verify.** Clean build. Create the existing files the Task 1 rows name under a scratch folder (point the paths at it) and run the Missing audit in the app
  against `dbo.Idx`: (a) with ID column `Id` — the report's Summary reads Total files `7 (22)`, Duplicates `3`, Skipped `7`, Unverified `n (5)`, the empty group lists
  `Id 10` … `Id 14`, and both sums hold; (b) without an ID column — `6 (22)`, Duplicates `4`, the group reads `(5)` and the no-ID line; (c) a 200,000-row variant
  (repeated paths, every tenth row empty or a marker), stopped after a few seconds — `Not checked:` present, both sums hold, the group carries the stopped line; (d) a table
  with only empty and skip rows, and an empty table — Completed, report written, `Total files: 0 (…)`/`0`. The on-screen stats are not checked yet (Task 3). Keep the database
  for Task 3.
- [x] **Step 5: Commit** — `feat(✨): report duplicates, empty paths and skipped rows from the up-front counts`.

### Task 3: The four-row Audit group

**Files:** Modify `Source/FileAuditor/ViewModels/Groups/AuditViewModel.cs`, `Views/Groups/AuditView.axaml`, `Views/Main/MainWindow.axaml` (height, and width only if the
values cannot fit at 640)

**Interfaces:**

- Consumes: `AuditProgress.Counts`, `Found`, `Checked`, `Missing`, `Unverified`, `HasTotal`, `CheckingElapsed` (Task 2); `RemainingEstimator.Next(long processed, long total,
  …)` as today.
- Produces: `AuditViewModel.Duplicates` and `AuditViewModel.Found` (`string`, observable, blank in `Reset`).

- [x] **Step 1: View model.** `TotalFiles`, `Duplicates`, `Skipped` and the `(E)` part of `Unverified` come from `Counts` once known (blank before); `Found`, `Missing`,
  `Checked` and `U` from the counters. Parentheses per Global Constraints. `Percent` and `RemainingEstimator.Next` use `Checked` over `Counts.Distinct`; D = 0 at the end reads
  100%. Remove the `Math.Max` and the `done + skipped` logic and update the XML docs (the recent rate now counts checked rows only).
- [x] **Step 2: View.** Four rows (spec §5 table, mock-up): left *Total files*, *Duplicates*, *Checked*; middle *Found*, *Missing*, *Unverified*, *Skipped*; right *Elapsed*,
  *Remaining*, *Files/second*. Widen the value columns so the Review Focus 5 values fit; keep the Unindexed tab's group unchanged.
- [x] **Step 3: Window.** Re-measure the natural height on the Missing tab (temporarily `SizeToContent="Height"`, read it, restore a fixed `Height`), as milestones 4.1 and 5
  did. Record the old and new size in the ledger.
- [x] **Step 4: Verify.** Clean build. Run the Task 2 cases (a), (b) and (d) in the app: the stats match the mock-up's arrangement and the report; *Total files*, *Duplicates*,
  *Skipped* and the `(E)` appear as soon as counting ends and do not change; the bar reaches 100%; Checked = Found + Missing + U. Use the 200,000-row table to watch the
  live values. Then set long values temporarily (or a design-time `Design.DataContext`) to confirm Review Focus 5 at the window's width. Screenshot the Audit group on both
  tabs. Drop the database.
- [x] **Step 5: Commit** — `feat(✨): show duplicates, found and the empty-path count in the Audit group`.

### Task 4: SDD, user guide and mock-ups

**Files:** Modify `Docs/Specs/1-file-auditor-master-design.md`, `Docs/UserGuide.md`; and the older mock-ups only where they now contradict the SDD (mark superseded, do not
redraw).

- [ ] **Step 1: SDD.** §5.4 (Missing tab statistics, four rows, the parentheses, Checked = Found + Missing + Unverified), §5.5 (the new status), the window size, §6.2 (the three
  statements, the row kinds, the table assumed unchanged), §6.3 (empty and skip rows never reach the checker), §6.4 (progress over D; no report when nothing was checked), §7
  (the Summary example and rules, the empty group without blank lines, the four replacement lines; drop `max(count, processed)`), §10 (this milestone's rulings, from the
  design's §8 and the ledger). Remove every statement this milestone makes false.
- [ ] **Step 2: User guide.** The Missing Files sections: the new statistics and Summary lines and how the sums work, Duplicates explained for patient-centred tables, the
  empty-path group (IDs, or the no-ID line), and the FAQ entry about blank lines (now gone). Markdown hard-wrapped at 180 characters, measured with PowerShell.
- [ ] **Step 3: Commit** — `docs(📝): document row accounting in the SDD and user guide`.
