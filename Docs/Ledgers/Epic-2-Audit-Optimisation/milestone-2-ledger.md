# Milestone 2 Ledger — Unindexed Audit Statistics

**Date:** 2026-11-13

Ledger for [`Docs/Plans/Epic-2-Audit-Optimisation/milestone-2-unindexed-stats.md`](../../Plans/Epic-2-Audit-Optimisation/milestone-2-unindexed-stats.md), executed on branch
`feat/milestone-2-unindexed-stats` (cut from `main` at `2543368`). Spec:
[`milestone-2-unindexed-stats-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-2-unindexed-stats-design.md), a short design approved on 2026-11-13. The master
SDD and the user guide were amended in Task 2. Both tasks are complete: Task 1 passed its review first time; Task 2 passed after one fix round and a controller reflow. The
final whole-branch review returned *Ready* with five Minor findings, all fixed.

## User decisions

Settled in the brainstorm (2026-11-13):

- *Found* and *Duplicates* do not carry over: *Indexed* already serves as *Found*, and duplicate index rows cannot change this audit's result.
- Two new statistics, *Excluded* and *Index paths*. *Not on disk* was rejected, because the type filters make it misleading.
- The layout: *Indexed* at the top of the second column, the two new statistics under *Total files* in the first. The user asked why *Indexed* had been in the first column;
  it was Claude's own call (Epic 1 milestone 5, ruling R6), so moving it loses nothing.
- Approach A for both: the walk gets a third callback, and `IndexPathSet.Add` reports a new path. Both keep the figures live.
- The window height does not change, because the Unindexed tab sits in a scroll view (the user's correction to the design).
- The optimisation ideas raised alongside are recorded in the design's §6, not built: the user has not yet had a table large enough to show a problem.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

- **R1:** the report Summary mirrors the screen column by column, as the Missing Files report does. Cost if wrong: line order.
- **R2:** *Excluded* leaves out hidden and system files, which are skipped before the filter. It includes names with no extension, and symbolic-link files of an excluded type,
  because the filter runs before the link probe. Cost if wrong: one condition in the walk.
- **R3:** *Index paths* counts distinct keys (trimmed, case-folded), not rows, so it includes the leading-white-space rows the filter loads from anywhere. With the filter off it
  covers the whole table; the guide says so. Cost if wrong: a label or a line of the guide.
- **R4:** the column widths were kept, because `Index paths:` fits the first column at the default font. Cost if wrong: one width.
- **R5:** the property `Excluded` is placed in alphabetical order in `UnindexedAuditViewModel`. `IndexPaths` stays before `Indexed` (ordinal order), as the implementer wrote it,
  since the file's convention does not settle case-sensitive order. Cost if wrong: a member move.
- **R6:** Task 2 also corrected a sum error that already existed in `unindexed-report-layout.html` (Total files 48,210 where Indexed + Unindexed is 48,207). Cost if wrong:
  nothing.
- **R7:** the SDD's Unindexed-tab diagram (§5) uses smaller sample figures, so the new rows fit the drawn box with left-aligned values, as the app shows them. Cost if wrong:
  nothing.

## Defects found in the plan

- The design said four rows; with two new statistics the grid has three. Fixed in the design before execution.
- The design's §3 (and, at first, SDD §6.5.3 and the guide's FAQ) said Total files + Excluded covers every non-hidden file. A symbolic-link file of a checked type, and an entry
  whose link status cannot be read, go to *Unverified* and are in neither count. Corrected in all three.

## Commits

| Commit    | What                                                                          |
|-----------|-------------------------------------------------------------------------------|
| `9908f7c` | Design, plan, ledger, `.slnx` registration                                    |
| `1e862b0` | Task 1: the two statistics on screen and in the report                        |
| `c5aa00a` | Task 2: SDD, user guide, mock-ups                                             |
| `a516616` | Task 2 fix round: the Excluded sum, mock-up slips, the guide's failure line   |
| `59e4dad` | Task 2 fix round: §6.5.2 reflow                                               |
| `9fb1550` | Task 2 fix round: §6.5.2 reflow, finished                                     |
| `a9e51f7` | Controller: remaining ragged paragraphs, the FAQ's link wording               |
| (next)    | Final review minors: property order, design §3, SDD diagram and §10, this ledger |

## Verification

- Clean build, 0 errors and 0 warnings, after every task.
- Task 1's harness ran the real runner against LocalDB:
  - `Root` alone gave Total files 4, Excluded 3, Index paths 4, Indexed 3, Unindexed 1 and Unverified 0. The table's case variant, leading-space variant, duplicate row and
    `NULL` row collapsed or were skipped as expected.
  - With 21 input folders, which turns the filter off, Index paths was 5.
  - An excluded symbolic link counted as Excluded.
- The controller ran the app in the real window (computer use), opening each test project through *File > Open project...*:
  - **First project:** the figures above appeared on screen and in the report Summary, in the specified order.
  - **Opening the 21-folder project:** the statistics were blanked.
  - **That project's run:** Index paths 5, with every other figure unchanged.
- Not exercised live: *Index paths* counting up during a long load (the test table loads instantly), and a Stop during the load. Both rest on code reading, confirmed by the
  task review and the final review: the timer starts before the run, and the final refresh runs on success, Stop and error.

## Outstanding items

- **Future milestones:** the four optimisations in the design's §6 (walk while the index loads; question `DISTINCT` in the load; let SQL Server use a path index; size the
  path set up front). Measure on a large table first.
- **Out of scope, seen in passing:** `Docs/UserInterfaceDesign/Mockups/direction-tabs-layout.html` (~290) still shows an outdated Unindexed Audit group (Checked, Remaining). This
  predates the branch.

## Incidents

None.
