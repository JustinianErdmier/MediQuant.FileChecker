# Milestone 6 — Streaming Missing Files Audit: Design

**Date:** 2026-11-14

The master SDD ([`1-file-auditor-master-design.md`](../1-file-auditor-master-design.md)) stays the contract; this document records what milestone 6 changes and why. It
reverses parts of milestone 1 ([`milestone-1-row-accounting-design.md`](milestone-1-row-accounting-design.md)): the statistics stay, but the classification and the
de-duplication move from SQL into the app, and the counts become live.

## 1. Intent

On a real 1,570,465-row table (`varchar(8000)` path column; 29,867 duplicates, 33 missing, 2 unverified) the user compared 1.0.0 and 2.0.0. Debug builds of both
(branches `diag/v1.0.0-debug-report` and `diag/v2.0.0-debug-report`; reports kept in `Docs/Temp/` until this milestone is finished) showed:

- **The large swings between runs were the file server, not the app.** The same 1.0.0 build took 22:36 and then 3:34; `File.GetAttributes` averaged 17.2 ms per file in
  the slow run and 2.45 ms in the fast one. In every run the SQL reader spent nearly all its time waiting for room in the channel: checking was the bottleneck.
- **2.0.0 adds a fixed ~85 seconds of SQL before checking starts, on every run:** the counts statement 45–46 s (1.0.0's count: 0.2 s) and the `DISTINCT` select 38–40 s
  before its first row. Both scan and classify all rows converted to `nvarchar(max)`, and both de-duplicate (the counts to count the distinct paths, the select to return
  them); the `DISTINCT` is a blocking hash aggregate, so no row arrives until it has finished.

The intent is to remove that fixed cost while keeping every statistic and the report milestone 1 introduced.

## 2. Goals, non-goals

- **Goals:** the Missing Files audit's SQL back to 1.0.0's two statements, rows streaming as soon as counting ends; classification (empty, skip marker) and de-duplication
  in the app; every milestone 1 statistic kept, counted live, with a new *Distinct* statistic; empty-path IDs collected during the stream; the Unindexed folder filter's
  conversion capped at `nvarchar(4000)`.
- **Non-goals:** any change to how a file is checked, to the parallelism, to the accepted column types (`nvarchar`, `varchar`), or to what the Unindexed audit reports.
- **Anti-goal:** a figure or a report that no longer adds up, or a distinct path that is not checked.

## 3. Measurements behind the choice

LocalDB, the same shape as the user's table (1,570,465 rows, `varchar(8000)`, 29,867 duplicates). The user's server ran 2.0.0's statements about 4.7 times slower than
LocalDB, so the last column is that factor applied.

| Before checking can start                                                                      | LocalDB | User's server (est.) |
|---|---|---|
| 2.0.0: counts (11.5 s) + `DISTINCT` select to first row (6.5 s)                                  | 18 s    | ~85 s (measured)     |
| A: `DISTINCT` once into a temporary table, then count and stream from it                         | 10 s    | ~47 s                |
| B: 2.0.0's statements with `nvarchar(4000)` instead of `max` (longest-value check 0.35 s)         | 8 s     | ~38 s                |
| C: classification-only count (no `DISTINCT`), rows stream in table order, de-duplicated in the app | 3 s     | ~14 s                |
| Chosen: `COUNT_BIG(*)` and a plain select (1.0.0's statements)                                  | < 0.5 s | 0.2 s (measured, 1.0.0) |

## 4. Approaches (chosen with the user)

- A: one `DISTINCT` into a temporary table. Keeps today's numbers up front; still pays the full de-duplication before the first row; needs one connection kept open.
- B: size the conversion (`nvarchar(4000)`, or the longest actual value) instead of `nvarchar(max)`. Contained; cuts the wait by about half.
- C: de-duplicate in the app; keep an up-front classification count. Largest gain, but the count still needs SQL to classify, so still a conversion.
- **Chosen (the user's refinement of C):** the up-front classification is dropped too. With the empty and skip-marker figures counted live, SQL has nothing to classify or
  convert: the audit runs 1.0.0's two statements and does everything else in the app. The user's decision; B becomes unnecessary for the Missing Files audit.

## 5. The Missing Files audit

**Statements.** After the type check (unchanged, §6.2 of the SDD; pre-flight's lookup still handed to the run):

- `SELECT COUNT_BIG(*) FROM <table>;` — the table's row count, *Total files*.
- `SELECT <path> AS [Path][, <id> AS [Id]] FROM <table>;` — every row, in table order, the column read as it is (no conversion, no `WHERE`, no `DISTINCT`); a `NULL` path
  reads as empty.

No other statement: the classification SQL, the `@ws` parameter, the counts statement's weighted sum and the empty-ID statement are removed.

**Per row, in the app,** in this order (the rule is unchanged; it is the one milestone 1's SQL was written to match):

1. **Skip marker** (`SkipMarker.IsSkipped`) → *Skipped*; not checked, not de-duplicated.
2. **Empty** (`null` or only `char.IsWhiteSpace` characters) → *Unverified — Path is empty*; not checked, not de-duplicated; its ID, when there is an ID column, is kept
   for the report.
3. **Otherwise a real path:** its key is looked up in the set of keys seen so far. Already seen → *Duplicates*, not checked. New → added to the set and checked as today.

**The de-duplication key (Claude's call, for the user to check):** the path with trailing spaces (U+0020 only) removed and upper-cased in the invariant culture, plus,
with an ID column, the ID as read — kept as a 128-bit hash (MD5, as `IndexPathSet` does) in a set, about 48 MB per million keys. Case is ignored because Windows paths are
case-insensitive, and so was 2.0.0's `DISTINCT` under the usual case-insensitive collations; trailing spaces are ignored because SQL Server's comparison ignores them and
Windows strips them from a path. Nothing else is trimmed: a path with a leading space, or a trailing tab or other white space, is its own key and is checked (the checker
reports it, for example as *Invalid characters*), so no row with a problem can hide behind a clean duplicate. `IndexPathSet` trims both ends with `char.IsWhiteSpace`, so it
cannot be reused as it is; a sibling (or a mode of it) is needed. Unlike the collation, the invariant upper-casing does not treat `ß` as `ss`, so milestone 4's parked
`Straße`/`Strasse` collapse goes away. With an ID column the unit stays the path and ID pair, as in 2.0.0.

**Who does what (left to the builder, with one constraint):** the classification and the set may live in the single reader (the producer), which keeps the set
single-threaded; what matters is that the sums of §6 hold at any moment the report is written.

## 6. Statistics and screen (the user's)

```text
Total files: 1,570,465   Found:      1,512,400   Elapsed:      00:02:10
Distinct:    1,512,435   Missing:    33          Remaining:    00:00:50
Duplicates:  29,100      Unverified: 2 (0)       Files/second: 11,980
Checked:     1,512,435   Skipped:    0
```

- ***Total files*:** the table's row count, known when counting ends (instant) and fixed for the run. The `distinct (rows)` form is gone.
- ***Distinct* (new, between *Total files* and *Duplicates*):** the number of different real paths **first seen** so far in the stream, counted when a new key enters the
  set (Claude's call). *Checked* trails it by the distinct rows queued or being checked (up to the channel's 10,000); at the end of a completed run the two are equal.
- ***Duplicates* keeps its rows meaning (the user's decision, 2026-11-14):** the extra rows whose path was already seen, each such row counted once — a path listed three
  times is one *Distinct* and two *Duplicates*. *Distinct* never goes down. This is what makes the figures add up to *Total files*.
- ***Skipped* and *Unverified*'s bracket (the empty paths):** counted live, when the row is read (rows, not distinct values).
- ***Found*, *Missing*, *Unverified*, *Checked*:** as today; *Checked* = Found + Missing + Unverified (without the bracket).
- **Sums (completed run):** Found + Missing + Unverified = Checked = Distinct; Distinct + Duplicates + empty paths + Skipped = Total files.
- **Sums (stopped run):** *Not checked* = Total files − (Checked + Duplicates + empty paths + Skipped), the rows never read plus the distinct rows read but not checked, so
  Checked + Duplicates + empty paths + Skipped + Not checked = Total files; *Distinct* is the distinct paths found before the stop.
- **A table that changes during the run** is out of scope (the user's decision, milestone 1): the sums may then not hold; *Not checked* is never shown below zero.
- **Progress, Remaining and the status-bar percentage:** rows handled out of *Total files* (a duplicate, empty or skipped row is handled the moment it is read; a distinct
  row when its check ends), so they stay accurate rather than pessimistic. *Remaining* is estimated from the recent rate of rows handled.
- **Files/second:** distinct files checked per second, both the whole-run figure and the recent figure in its bracket, as today; it no longer shares its recent rate with
  *Remaining*, which uses rows handled (two estimators; Claude's call), so a burst of duplicates moves *Remaining* but not *Files/second*.
- Formatting, the column widths and the four-row grid follow the existing Audit group; *Distinct* takes the first column's second row and *Duplicates* and *Checked* move
  down one.

## 7. The report

- **Summary mirrors the screen** (the milestone 1 rule): `Total files:` (rows), `Distinct:`, `Duplicates:`, `Found:`, `Missing:`, `Unverified:` (with its bracket),
  `Skipped:`, and `Not checked:` for a stopped run.
- **Empty paths:** the *Unverified — Path is empty* group lists the IDs collected during the stream. The note `Missing filepaths not listed because no ID column was specified`
  stays. **Claude's call:** a stopped or part-way failed run lists the IDs read so far (its counts are also what was reached), so the `...because the audit was stopped`,
  `...because the audit failed` and `...because they could not be read` notes and the `Reading empty paths...` phase go away.
- Everything else in the report is unchanged.

## 8. The Unindexed Files audit

- Unchanged except the folder filter's conversion: for a `varchar` column, both `LIKE` clauses compare `CAST(<column> AS nvarchar(4000))`, whatever the column's length
  (no `nvarchar(max)`). Both clauses test only the start of the path (a folder prefix, or the first character), so characters beyond 4,000 can never change the result. An
  `nvarchar` column and a run with no filter are unchanged; the selected value is still the raw column.

## 9. What the `varchar` rule and warning become

- **The type rule is unchanged:** only `nvarchar` and `varchar` path columns are accepted, on both tabs.
- **The warning stays, its middle sentence reworded** (the Missing Files audit no longer converts): `The path column '<column>' is <type>.` / `The File Auditor prefers an
  nvarchar column. A varchar column may have changed some characters in its paths when they were stored. See "varchar Path Columns" in the user guide.` / blank line /
  `Do you want to continue?` Everything else about it (remembered per column, the lookup, the silent cases, the 30-second cap, the lookup handed to the run) is unchanged.
- **User guide §8.4** is rewritten: what is converted (only the Unindexed folder filter's prefix test, to `nvarchar(4000)`), why, and the effects — the look-alike letters and
  `?` still apply, because they come from how the paths were stored.

## 10. What changes

- Code: `QueryBuilder.Build` (two plain statements; the classification SQL removed), `AuditQuery`, `RowCounts` and `IndexTableReader` (the count, the stream; the counts and
  empty-ID readers removed), `AuditRunner` (classification, de-duplication and empty IDs in the app; no `Reading empty paths...`), `AuditProgress`, `AuditViewModel` and
  `AuditView` (*Distinct*, live figures, progress by rows), `ReportBuilder` and `AuditReport` (the Summary, the empty-path notes), `QueryBuilder.BuildPaths` (the
  `nvarchar(4000)` filter conversion), `PathColumnType` (no conversion target for the Missing Files audit), `PreflightChecker` (the warning's wording).
- SDD §5.4, §6.2, §6.4, §6.5.2, §7, §10; user guide sections 4 (the statistics and the report), 8.4, 9 and the 2.0.0 changelog entry (the version stays 2.0.0); the
  `row-accounting-stats.html` mock-up.
- **After the milestone's PR is merged** (the user's instruction, "once we're completely finished"): delete `Docs/Temp/` from `main` in a clean-up commit (the folder was
  never registered in the `.slnx`), and delete the branches `diag/v1.0.0-debug-report` and `diag/v2.0.0-debug-report` locally and on GitHub. This is a step after the
  merge, not a task in the milestone's PR, so the reports stay available while the milestone runs.

## 11. Left to the builder

Where the classification and the key set live (§5's constraint holds); a sibling of `IndexPathSet` or a mode of it for the key set (§5); the names of new members; the
measurement in the plan's verification (the same LocalDB table, before and after).

## 12. Settled after the builder check

The builder check raised five questions, all settled from the conversation or as Claude's calls: *Distinct* counts paths first seen, not paths checked, and *Duplicates*
keeps counting extra rows (confirmed by the user; §6); the key trims only
trailing spaces (§5); the clean-up happens after the merge, the branches on GitHub included (§10); *Remaining* and *Files/second* use separate rates (§6); a table changing
mid-run stays out of scope (§6).
