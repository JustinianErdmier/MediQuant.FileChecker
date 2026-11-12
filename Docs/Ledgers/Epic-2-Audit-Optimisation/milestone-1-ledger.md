# Milestone 1 Ledger — Row Accounting

**Date:** 2026-11-12

Ledger for [`Docs/Plans/Epic-2-Audit-Optimisation/milestone-1-row-accounting.md`](../../Plans/Epic-2-Audit-Optimisation/milestone-1-row-accounting.md), executed on
branch `feat/milestone-1-row-accounting` (cut from `main` at `b22d63c`). Spec:
[`milestone-1-row-accounting-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-1-row-accounting-design.md). The master SDD and the user guide were amended in
Task 4 and the final fix wave. Tasks 1 to 4 are complete; each passed its task review with no open Critical or Important findings (Tasks 2, 3 and 4 after one fix round
each), and the final whole-branch review's one fix wave passed its re-review.

## Groundwork

- `b22d63c` — `fix(🐛): check each distinct path in the index table once`. Made both directions' queries `DISTINCT`; amended the SDD and user guide.

## User decisions

Settled in the brainstorm (2026-11-12), recorded in the design's §7:

- `DISTINCT` on both directions; the ID column's uniqueness per path is the user's responsibility.
- Up-front counts of the table's rows, empty paths and skip markers; empty-path rows fetched at the end so their IDs can be printed.
- *Total files* `D (T)`; a *Duplicates* stat between *Total files* and *Checked*; *Found* at the top of the middle column; empty paths stay under *Unverified — Path is
  empty*, with their count in parentheses on *Unverified*; skipped rows counted by query.
- Without an ID column the empty group shows its count and `Missing filepaths not listed because no ID column was specified`.
- The report's Summary mirrors the screen.
- Non-ASCII case folding in skip markers is not handled.
- The table is assumed not to change during a run; the app does not reconcile drift.

## Rulings

Decisions taken without the user, each with what it costs if wrong. The design's §8 lists the brainstorm-time calls (Unverified's parentheses hidden at E = 0; the empty-ID
read only after a completed check, Stop leaving *Completed*, status `Reading empty paths...`; the replacement lines; no report when nothing was checked; one line per
empty-path row; the `max(count, processed)` guard dropped).

- **R1:** Task 1 also updated `AuditRunner`'s count call minimally so its build was clean; Task 2 replaced it. Cost if wrong: none (Task 2 rewrote the same lines).
- **R2:** this file doubled as the subagent-driven ledger while the run was live; briefs, reports and review packages stayed in the git-ignored `.superpowers/sdd/` workspace.
  Cost if wrong: none.
- **R3** (implementer's, accepted): on screen, *Checked* is Found + Missing + Unverified, each counter read once per refresh, because reading the Checked counter separately
  gave a torn snapshot mid-run. Cost if wrong: none; the two are equal at the end of a run.
- **R4:** a stray *Skipped* outcome (reachable only through non-ASCII case folding, e.g. `ſkip`, which SQL classes as a real path) becomes *Unverified — Other errors* with the
  reason `Skip marker not recognised by the database`, so the report stays self-consistent. Cost if wrong: one reason string.
- **R5:** empty-path IDs keep string ordering, like the other groups (`Id 1, Id 10, Id 2`), not numeric. Cost if wrong: a small follow-up if the user prefers numeric order.
- **R6:** the counts statement reads the row kind once, as a weight in a single `SUM` (empty 1, skip 2^32), because SQL Server re-evaluated the classification for every
  reference and three `CASE` sums cost three times as much. Limits: the skip count must stay below about 2^31 rows (overflow error beyond) and an empty count of 2^32 or more
  would carry into the skip count — billions of rows, irrelevant in practice. Cost if wrong: a less obvious statement for a future editor (commented in code and the SDD).
- **R7:** GUI verification by Windows UI Automation from PowerShell (subagents have no desktop-control tools); Tasks 1 and 2 and the fix wave were verified through a
  scratchpad harness calling the real classes against LocalDB. Cost if wrong: see *Not exercised* below.

## Defects found in the plan

- Task 1 removed an interface (`CountAsync`) that unchanged code still called, so it could not end on a clean build as written (R1).
- Task 2's interface first dropped `AuditProgress.TotalFiles`/`Skipped`, which would have broken the build until Task 3; corrected before execution by keeping them as
  wrappers.
- The plan's SQL shape (a `CROSS APPLY` trim with `nvarchar(max)` casts, classified twice in the counts) was correct but ~19× slower than the old count on 200,000 rows;
  found by the final review and fixed in the wave (`079b686`).
- The plan did not ask for the table to be aliased, which made table names such as `p` and column names such as `Length` fail; fixed in `079b686`.

## Performance (LocalDB, 200,000 rows: 140,000 real, 40,000 empty, 20,000 `SKIP`)

| Statement                         | Before this milestone | After Task 1 | After the fix wave |
|-----------------------------------|-----------------------|--------------|--------------------|
| Count                             | 0.30 s                | 4.3–5.8 s    | 1.3 s              |
| Stream (`SelectSql`, as a count)  | —                     | 2.3–4.3 s    | 0.75–1.2 s         |
| Empty-ID read                     | —                     | 0.16–0.4 s   | 0.6 s              |

Checking runs at about 500 files per second, so counting stays well under 1% of a run.

## Outstanding items

**Candidates for a next milestone in this epic**

- Cast the path column to its own bounded type (or `nvarchar(4000)` when `sys.columns` allows) instead of `nvarchar(max)`, keeping `max` only for LOB types: most of the
  remaining counting cost. Needs a metadata read; gain not measured.
- Numeric ordering of empty-path IDs, if wanted (R5).

**Minor, left as they are** (triaged by the final review)

- `ReadEmptyIdsAsync` has no null guard (its only caller checks); `CountRowsAsync` ignores the `ReadAsync` result (an aggregate always returns one row); the cancellation
  catch around the empty-ID read has no `when` filter (the only source is the caller's token).
- A stray *Path is empty* problem would print a second group of the same title, and `AddEmptyPathGroup` would print a blank line if both its inputs were null: unreachable.
- `AuditViewModel`: *Duplicates*/*Found* out of alphabetical member order; `HasTotal` read separately from `Counts` (a one-refresh 0% blip is possible).
- Docs wording: SDD §6.3 opens "Each file lands in exactly one outcome" with *Skipped* listed first; the guide's "bracketed counts" (~276) and "(the bracket on
  `Total files:`)" (~367); the guide's step-3 reason ("has to work out every row's kind") implies the ID read does not, though it evaluates the same kind; the code comment on
  the weighted `SUM` does not state the skip-side limit.

**Not exercised**

- The stray-*Skipped* mapping (R4) was verified by build only.
- After the fix wave's SQL change the app was not driven again; the SQL was re-verified against the Task 1 seed and the interfaces did not change.
- Stop mid-check was exercised through the harness (Task 2), not in the GUI.

## Incidents

- `b22d63c` was committed straight to `main`, outside any epic or plan, before this milestone existed. The user accepted it as is; it is recorded as groundwork.
- The first setup commit timed out on GPG signing; a retry succeeded. No signing was bypassed.
- During Task 3's fix round the implementer committed a non-building edit (`d7e481e`, a literal `\r\n`) and amended it into `479a110` on the unpushed branch.
- Implementers edited some files with tools that left LF line endings in the working tree (the index is LF throughout; harmless).

## Commits

`f94a21d` docs (design, plan, ledger) · `ed1cef0` Task 1 · `96a3afe`, `df7962c` Task 2 · `589a235`, `479a110` Task 3 · `6700aa7`, `d82f387`, `a06e2e4` Task 4 · `7d2dbe1`
ledger · `079b686`, `e35d52f`, `afc8312` final fix wave.
