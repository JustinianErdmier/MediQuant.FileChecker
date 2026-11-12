# Milestone 1 — Row Accounting: Design

**Date:** 2026-11-12

Epic 2 makes the audits faster and their numbers more truthful on real MediQuant index tables. This first milestone finishes what commit `b22d63c` started.

## 1. Why

Many index tables are built around patients, not files, so one file path sits on many rows. Commit `b22d63c` (made on `main` before this epic existed, and recorded here as
this milestone's groundwork) made both audits read each distinct path once (SDD §6.2). That fixed the repeated checks, but `DISTINCT` also hides things the user needs to see:

- how many rows the table really has, and how many of them were duplicates;
- how many rows have no path: `DISTINCT` folds every `NULL`, empty and blank path into one or two rows, so *Path is empty in the index table* under-reports badly;
- how many rows are skip markers: `DISTINCT` folds a thousand `SKIP` rows into one, so *Skipped* under-reports too;
- which rows have no path: with an ID column the user needs every such row's ID to fix it, and the folded rows lose all but one.

Success: on a patient-centred table, the Audit group and the report show the distinct files checked, the duplicates, the empty paths and the skip markers, and every number
adds up both ways (§3). The empty-path rows are listed by ID when an ID column is set.

## 2. Goals, non-goals, anti-goals

**Goals**

- Count the table's rows, its empty-path rows and its skip-marker rows up front, and the distinct real paths, so the empty-path and skipped figures are final the moment
  counting ends.
- Check only the distinct real paths (no empty path, no skip marker reaches the file checks).
- Show the new figures in the Missing Files tab's Audit group and in the missing-files report's Summary.
- List every empty-path row by ID at the end of the run when an ID column is set; otherwise say why they are not listed.

**Non-goals**

- The Unindexed Files audit is unchanged: it reads paths only to learn which exist, and its index set already keeps one hash per path.
- Checking that the ID column is unique to its path. That is the user's to keep (SDD §6.2), as before.
- Any change to the skip rule's meaning (SDD §6.3) or to how a real path is checked.

**Anti-goals** (failure even if it works)

- Numbers that do not add up, on screen or in the report, on any result (*Completed*, *Stopped by user*, *Failed*), for a table that does not change during the run.
- The SQL-side classification and the app's own skip and empty rules disagreeing, so a row is counted in one place and checked in another.
- A blank line per empty-path row when no ID column is set (today's behaviour, SDD §7): it carried no information and, with the true count, could run to thousands of lines.

## 3. The numbers

Every row of the table is exactly one of:

| Kind              | Rule                                                                                                 | Symbol |
|-------------------|------------------------------------------------------------------------------------------------------|--------|
| Empty path        | `NULL`, or made only of white-space characters (`char.IsWhiteSpace`), including the empty string      | E      |
| Skip marker       | Not empty, and a skip marker by the rule of SDD §6.3                                                 | S      |
| First of its kind | Neither of the above, and the first row of its distinct path (or path and ID pair, with an ID column) | D      |
| Duplicate         | Neither of the above, and not the first row of its distinct path (or pair)                           | Dup    |

T (the table's rows) = D + Dup + E + S. Dup is never counted directly: it is T − D − E − S.

Of the D distinct real paths, each lands in Found, Missing or Unverified (the checked Unverified outcomes, written U below; E is *not* part of U). So:

- **Found + Missing + U + Not checked = D** (Not checked is 0 for a completed run), and
- **D + Dup + E + S = T**.

## 4. Running the missing-files audit

**Counting** (status `Counting files...`, as today; still after the pre-flight checks, which never touch the database). One or more statements return T, E, S and D. D is
`COUNT_BIG(*)` over `SELECT DISTINCT path[, id] … WHERE NOT empty AND NOT skip`. Stop while counting writes no report (unchanged).

**The classification must agree with the app's rules exactly.** The empty and skip predicates are built in SQL so they match `string.IsNullOrWhiteSpace` and `SkipMarker`
character for character, whatever the column's collation: the same binary-collation technique as the unindexed whitespace clause (SDD §6.5.2, `WhitespacePattern`), with
`[^<white space>]` classes for "only white space" and for trimming. Matching `SKIP` and `SKIPPED` on plain ASCII letters in either case is enough: the non-ASCII letters that
`OrdinalIgnoreCase` also folds to `I` or `S` (such as `ı` and `ſ`) do not occur in marker values and need no handling (the user's call).

**Checking** (status `Checking files...`). The stream is the same distinct query, filtered by the same `WHERE`. So no empty path and no skip marker reaches the checker. The
checker keeps its own skip and empty tests as a safety net, but a row they catch must still fit the sums of §3 (the builder decides how, and records it).

**The empty-path rows** (only when E > 0 and an ID column is set). After the checking ends, one more query reads the ID of every empty-path row (not distinct: one line per
row, so the group's count matches its lines), with the status `Reading empty paths...` (a new entry in SDD §5.5's phase list). It runs only when checking **completed**. It never
changes the run's result:

- Stop (or *Stop it and exit? → Yes*) while it runs abandons it; the result stays *Completed*, since every file was checked.
- When it is abandoned, fails, or is not run (the run was *Stopped by user* or *Failed*), the group keeps its count and holds one line in place of the IDs, in the pattern of the
  user's no-ID wording: `Missing filepaths not listed because the audit was stopped`, `… because the audit failed`, or `… because they could not be read: <message>`.

**No report when nothing was checked** (SDD §6.4) stays as it is: a failure with Checked = 0 writes no report, even though T, E and S are known.

**The table is assumed not to change during a run** (the user's call): changing it mid-audit is the user's responsibility, and the app does nothing to reconcile the counts
with the rows streamed or read. No snapshot, no clamping, no recounting.

**Progress.** The progress bar and *Remaining* run over D; checked rows are the only processed rows (skipped rows are no longer processed one by one, so they no longer move the
bar, unlike milestone 5).

## 5. The Audit group (Missing Files tab)

Four rows instead of three, in the same three label/value column pairs. The approved mock-up is `Docs/UserInterfaceDesign/Mockups/row-accounting-stats.html`.

| Row | Left                  | Middle                 | Right             |
|-----|-----------------------|------------------------|-------------------|
| 1   | Total files: `D (T)`  | Found: `n`             | Elapsed:          |
| 2   | Duplicates: `Dup`     | Missing: `n`           | Remaining:        |
| 3   | Checked: `n`          | Unverified: `U (E)`    | Files/second:     |
| 4   |                       | Skipped: `S`           |                   |

- *Total files* shows D, with T in parentheses only when T ≠ D. *Unverified* shows U, with E in parentheses only when E > 0 (the user's call for Total files; Unverified's
  rule follows it by analogy, the builder's call, accepted in the mock-up).
- *Duplicates*, *Skipped*, *Total files* and the parenthesised E are set the moment counting ends and never change during the run.
- *Checked* = Found + Missing + U. *Found* is new on screen.
- The value columns must be wide enough for `9,999,999 (99,999,999)` in *Total files* without clipping; the window may grow in width and grows by one row in height (the SDD's
  window size is amended). The Unindexed tab keeps its three rows; the tabs share one height (SDD §5), so its group simply has spare space.

## 6. The missing-files report

**Summary** mirrors the screen:

```text
Summary
-------
Total files:     98,200 (124,500)
Duplicates:      24,750
Found:           97,820
Missing:         260
Unverified:      120 (310)
Skipped:         1,240
```

Same rules as on screen: the parentheses appear only when they differ (T ≠ D) or are non-zero (E > 0). `Duplicates:` and `Skipped:` are always present. `Not checked:` (Stopped
or Failed) is D − Found − Missing − U, so the first sum of §3 holds. Values start at the label column as today (`ReportFormat.Labelled`).

**Unverified Files.** The section's heading count is U + E (it lists both). The group *[Path is empty in the index table (E)]* holds:

- with an ID column: one line per empty-path row, `<IdColumn> <id>`, sorted as the other groups are;
- without one: the single line `Missing filepaths not listed because no ID column was specified` (the user's wording) in place of the blank lines;
- with one, when the IDs were not read: the one line of §4.

## 7. Settled with the user

- `DISTINCT` on both directions' queries, with the ID column the user's responsibility (commit `b22d63c`).
- Counts of the table's rows, empty paths and skip markers, taken before checking; empty-path rows fetched at the end so their IDs can be printed.
- *Total files* as `D (T)` when they differ; a *Duplicates* stat between *Total files* and *Checked*; *Found* added at the top of the middle column.
- Empty paths stay under *Unverified — Path is empty in the index table*; the empty count is shown in parentheses on *Unverified*.
- Skipped rows counted by query, not one by one; non-ASCII case folding in skip markers is not handled.
- The table is assumed unchanged during a run; the app does not reconcile drift.
- Without an ID column the empty group shows its count and `Missing filepaths not listed because no ID column was specified`.
- The report's Summary mirrors the screen, adding Duplicates and the table's rows.

## 8. Decided by Claude (for the user to check)

- *Unverified* hides its parentheses when E = 0, by analogy with *Total files*.
- The empty-path ID query runs only after a completed check, Stop abandons it without changing the result, and its new status text is `Reading empty paths...`.
- The one-line replacements for the IDs when they were not read (§4), worded after the user's no-ID line.
- A failure with nothing checked still writes no report.
- Empty-path rows are listed one line per row, not per distinct ID.
- The old `max(count, processed)` guard on *Total files* (SDD §7, §10) is dropped, since the app no longer reconciles a changing table.

## 9. Left to the builder

- How many statements the counts take, and whether they share one round trip.
- The exact SQL of the empty and skip predicates, provided they match the C# rules (§4).
- What the checker does with a skip marker or empty path that reaches it after all (§4), provided the sums hold.
- Column widths in the Audit group and the window's new size.
- The SDD, user guide and mock-up updates that follow from all of the above.
