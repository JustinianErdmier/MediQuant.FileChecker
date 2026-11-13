# Milestone 1.1 Ledger — nvarchar Path Column

**Date:** 2026-11-12

Ledger for [`Docs/Plans/Epic-2-Audit-Optimisation/milestone-1.1-nvarchar-path-column.md`](../../Plans/Epic-2-Audit-Optimisation/milestone-1.1-nvarchar-path-column.md),
executed on branch `feat/milestone-1.1-nvarchar-path-column` (cut from `main` at `089ef2e`). No milestone spec: the approved description quoted in the plan is the spec. The
master SDD and the user guide were amended in Task 2 and the final fix wave. Tasks 1 and 2 are complete; Task 1 passed its review after one fix round, Task 2 first time, and
the final whole-branch review's fix wave passed its re-review (Clean, three minors, all fixed). The run was paused once at the user's request (2026-11-12) and resumed on
2026-11-13.

## User decisions

- The path column must be `nvarchar(n)` or `nvarchar(max)`; any other type fails the audit with a message naming the type, and no report is written.
- The rule applies to both audits, since milestone 2 builds on it.
- The user guide explains briefly why `nvarchar` is required.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

- **R1:** an alias type over `nvarchar` is accepted (the check reads the system type) and `nchar` is refused, since the user said `nvarchar`. Cost if wrong: a one-line change.
- **R2:** the type is read under each audit's existing first status (`Counting files...` / `Loading the index...`), with no new status text; Stop during it is
  `StoppedBeforeChecking`. Cost if wrong: nothing.
- **R3:** one cast stays, inside the trim's `LEN` and only for rows past the `skip` prefilter. Without it, a 4,000-character value in an `nvarchar(4000)` column is truncated by
  the appended `N'x'`: a padded skip marker was misclassified, and `'SKIP'` plus 3,996 spaces failed the whole audit with Msg 537. Cost if wrong: a little speed on skip
  candidates only.
- **R4:** the type is read from the statement's own result-set metadata (`sp_describe_first_result_set` on `SELECT TOP (0) [fa_t].<column> FROM <table> AS [fa_t];`) instead of
  `sys.columns`. Permissions, synonyms (also cross-database) and views then behave exactly as they do for the audit statements. Cost if wrong: a different lookup.
- **R5:** a first error of 207 or 208 means not found; any other error (229 for no SELECT permission) is `Sql.QueryFailed` with SQL Server's message. Where 4413 follows, a
  view that no longer binds is shown with its own message, not as a missing path column. Cost if wrong: one condition.

## Measurements

200,000 rows in LocalDB (140,000 real, 40,000 empty, 20,000 `SKIP`, `nvarchar(400)`), mean of three runs, with the cast and fully without:

- `CountsSql`: 1,154 → 589 ms.
- `SelectSql`: 1,654 → 1,385 ms.
- `EmptyIdsSql`: 498 → 238 ms.

After R3 restored the narrow cast, `CountsSql` measured 616–1,104 ms on a noisy run (1,121–1,271 ms with the full cast). The final statements were re-timed on a separately
generated table: `CountsSql` 2,211 / 1,796 / 1,490 ms (the first run cold) and `SelectSql` 1,530 / 1,243 / 1,232 ms. These are not comparable with the figures above.

## Defects found in the plan

- The plan's lookup (`sys.columns` with `OBJECT_ID`) hid the table from an account with no permission, reporting it as not found. It also lost synonyms, with no workaround,
  since the Table field takes two parts at most, and crashed on CLR types (`TYPE_NAME` is NULL). The final review found this; replaced by R4.
- The plan's "no cast" step was unsafe at `nvarchar(4000)`; replaced by R3.

## Commits

| Commit    | What                                                                          |
|-----------|-------------------------------------------------------------------------------|
| `1b0b1fe` | Plan, ledger, `.slnx` registration                                            |
| `bd1af51` | Task 1: the type check, both runners, classification without the cast        |
| `f1fc6ef` | Task 1 fix round: the narrow cast inside `LEN` (R3)                           |
| `a7a5288` | Task 2: SDD and user guide                                                    |
| `a493036` | Final fix: type read from result-set metadata (R4), long lines rewrapped     |
| `7433c69` | Final fix: guide and SDD wording, §10 timings                                 |
| `941ff15` | Final fix: docs rewrapped to 180 characters                                   |
| `4c9e0ef` | Re-review minors: a view that no longer binds (R5), CLR comment, §10 labelling |

## Verification

Clean build, 0 errors and 0 warnings, after every task. Run against LocalDB:

- **Accepted:** `nvarchar(400)`, `nvarchar(4000)`, `nvarchar(max)`, and an alias over `nvarchar`.
- **Refused, with the exact text:** `varchar(400)`, `varchar(max)`, `nchar(10)`, `int`, `uniqueidentifier`, and `geography`.
- **Names:** a non-default schema, and names with spaces or `]`, are found.
- **Not found:** a missing table (first error 208) or a missing column (207) gives the not-found message.
- **No permission:** no SELECT permission (229) gives `Sql.QueryFailed`.
- **Synonyms:** same-database and cross-database synonyms are accepted, and the statements run.
- **Classification:** milestone 1's seed figures are unchanged.
- **App:** the refusal dialogue appears on both tabs, read by UI Automation.

The R5 condition is not verified with a live run. It rests on the error sequence the re-reviewer reproduced: 207, 4413, 11501.

## Outstanding items

- **Next milestone (2):** builds on the `nvarchar` rule; nothing carried over.
- **Deferred minor, not fixed:** the `SqlErrors` code constants for the two new errors sit after `QueryFailed`, not beside the existing constant. This is cosmetic.

## Incidents

- **Task 2 background-work notice:** the implementer's notification said it stopped with background work still running. On checking, no FileAuditor or sqlcmd process was
  left; only Rider's processes and the compiler server remained. Nothing was stopped.
- **The pause:** the first final-fix implementer was stopped mid-run after committing `a493036`, and it wrote no report. A fresh implementer verified `a493036` and finished
  the docs. The scratch databases and the test login were dropped.
