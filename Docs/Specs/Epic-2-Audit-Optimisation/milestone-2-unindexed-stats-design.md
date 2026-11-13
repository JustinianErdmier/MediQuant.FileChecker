# Milestone 2 — Unindexed Audit Statistics: Design

**Date:** 2026-11-13

A short design: a small change, agreed in conversation on 2026-11-13. The master SDD
([`1-file-auditor-master-design.md`](../1-file-auditor-master-design.md)) stays the contract; this document records what milestone 2 changes and why, and what was considered
and left for later.

## 1. Intent

Milestone 1 gave the Missing Files audit a fuller account of what it read. The user asked which of those statistics carry over to the Unindexed Files audit, and what else that
audit could show. Two answers came out of it:

- *Found* and *Duplicates* do not carry over. *Indexed* already means "on disk and in the index", so *Found* would say the same thing twice. Duplicate index rows cannot change
  this audit's result (the app keeps each path once), and since only the rows under the chosen folders are loaded, a duplicate count here would not match the Missing Files
  tab's, which would confuse.
- Two statistics this audit does not show today are worth adding: **Excluded** (files the type filters skipped) and **Index paths** (how many index paths the disk was compared
  against). Both are free: the walk already decides every exclusion, and the load already builds the set of paths.

The user has not yet run this audit against a table large enough to show a performance problem, so the optimisation ideas raised alongside are recorded (§6), not built.

## 2. Goals, non-goals

- **Goals:** add *Excluded* and *Index paths* to the Unindexed tab's Audit group and to the unindexed report's Summary; regroup the statistics so the index-related counts sit
  together.
- **Non-goals:** a *Found* or *Duplicates* stat on this tab; a *Not on disk* stat (index paths under the folders that no file matched: the type filters make it misleading, since
  an index row for a type that was not checked would count as not on disk); any change to what is checked or how; the optimisations of §6.
- **Anti-goal:** a new count that changes or blurs the existing sum. *Total files* stays exactly *Indexed* + *Unindexed*.

## 3. The statistics

**Layout (the user's).** Three columns, three rows (two today):

```text
Total files:  12,480     Indexed:     12,301     Elapsed:       00:01:12
Excluded:      3,912     Unindexed:      179     Files/second:  173 (190)
Index paths:  12,644     Unverified:       2
```

*Indexed* moves from the first column to the top of the second. Its place in the first column was Claude's call (Epic 1 milestone 5, ruling R6, "by analogy with the user's
Missing layout"), not the user's, so nothing is lost by moving it. Values are formatted as today (§5.4 of the SDD) and are blank before the first run.

**Excluded.** The files the walk met and skipped because no *File types* entry matches them or an *Exclude* entry does.

- Excluded is **never part of Total files**: an excluded file is not checked. So Total files + Excluded = every file the walk met that is not hidden or system.
- Hidden and system files are **not** counted: the walk skips them by an older rule, before any filter decision.
- A file with no extension (`README`, `.gitignore`) matches no entry, so it is Excluded.
- A symbolic-link file of an excluded type is Excluded, not *Link not followed*: the type filter runs first, as today.
- A folder that cannot be listed reports none of its files (SDD §6.5.3), so it reports no exclusions either.
- Live: it counts up during the walk, like Total files.

**Index paths.** The number of distinct paths loaded from the index to compare against, after trimming surrounding white space and ignoring letter case (the same key the
comparison uses).

- With the folder filter on, these are the rows under the chosen folders, plus any row whose value starts with white space (loaded from anywhere, SDD §6.5.2); with the
  filter off (no folder, more than 20, or an empty one), the whole table. The user guide says so in a line, so the figure is not read as "rows under these folders".
- Live: it counts up during `Loading the index...`, which today shows nothing; it is final once the walk begins.

**Stop.** Both show what was reached. A Stop while the index is loading still writes no report (SDD §6.5.4, unchanged).

## 4. The report

The Summary mirrors the screen, column by column, as the Missing Files report does:

```text
Summary
-------
Total files:     48,207
Excluded:        1,113
Index paths:     48,190
Indexed:         48,187
Unindexed:       20
Unverified:      3
```

## 5. Approaches (chosen with the user)

**Excluded.**

- **A (chosen):** the walk gets a third callback beside its file and unverified-folder callbacks, called for each excluded file; the run bumps a counter on
  `UnindexedProgress`. Live, and consistent with the walk's all-or-nothing listing for free.
- B: the walk keeps a private tally and returns it at the end. Simpler, but blank until the run ends, unlike every other statistic.
- C: excluded files go through the file callback with a flag. Mixes "skip" into "check"; every caller has to filter again.

**Index paths.**

- **A (chosen):** `IndexPathSet.Add` reports whether the path was new, and the run bumps a counter on `UnindexedProgress` when it was. Live during the load.
- B: read the set's size once the load ends. Simpler, but the load still shows nothing.

## 6. Possible future optimisations (recorded, not built)

None has been measured: the user has not yet had a table large enough to show a problem. Measure before building any of them, on both a true file index and a patient-centred
table.

1. **Walk while the index loads.** Today the load and the walk run one after the other, and both mostly wait on the network (SQL Server, file shares). Overlapping them would
   bring the run towards the longer of the two instead of their sum, whatever the table's design. Costs: files found before the load ends must be held in memory, and a Stop
   during the load would then have something to report, which changes today's no-report rule.
2. **Question `DISTINCT` in the index load.** The app already keeps each path once in its set. `DISTINCT` only pays when it cuts what crosses the network (a patient-centred
   table); for a true file index it is server work for nothing.
3. **Let SQL Server use an index on the path column.** The folder filter applies its own case-insensitive collation to the column, and the leading-white-space clause is a
   second barrier; either rules out an index seek, so every load scans the table. Since milestone 1.1 the type lookup could also return the column's collation; when that is
   already case-insensitive (SQL Server's default) the explicit collation can be dropped. Pays only for tables that have such an index.
4. **Size the path set up front.** `IndexPathSet` takes a capacity that the run does not pass. Small.

## 7. What changes

- Code: `FolderWalker` (the excluded callback), `IndexPathSet.Add` (returns whether the path was new), `UnindexedProgress` (two counters), `UnindexedAuditRunner` (wiring),
  `UnindexedReport` and `UnindexedReportBuilder` (two Summary lines), `UnindexedAuditViewModel` and `UnindexedAuditView` (the grid: three rows, the new order).
- Every other caller of `FolderWalker` (if any) passes or ignores the new callback; behaviour unchanged.
- SDD §5.4a, §6.5.2, §6.5.3, §6.5.4, §7.1 and §10; the user guide's Unindexed tab and report sections.
- Mock-ups under `Docs/UserInterfaceDesign/Mockups/`: the Summary in `unindexed-report-layout.html`, and the Unindexed Audit group's statistics, which no mock-up shows today
  (a panel added to `row-accounting-stats.html`, beside milestone 1's Missing Files statistics).
- Window height: unchanged. The Unindexed tab sits in a scroll view, so its extra row never changes the main window's height (the user's correction).

## 8. Left to the builder

The first column's width, if *Index paths:* needs more room; names of the new members; whether the excluded callback carries the file's path or nothing (nothing is needed
today).
