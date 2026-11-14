# Milestone 5 Ledger — varchar Warning

**Date:** 2026-11-13

Ledger for [`Docs/Plans/Epic-2-Audit-Optimisation/milestone-5-varchar-warning.md`](../../Plans/Epic-2-Audit-Optimisation/milestone-5-varchar-warning.md), executed on branch
`feat/milestone-5-varchar-warning` (cut from `main` at `196853f`). Spec:
[`milestone-5-varchar-warning-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-5-varchar-warning-design.md), a short design approved on 2026-11-13. The master SDD
and the user guide (new section 8.4, and the 2.0.0 changelog entry) were amended in Task 2.

Both tasks are complete:
- **Task 1** passed review once the controller fixed its Important finding (`35bbf78`) and capped the lookup at 30 seconds (`d2454a5`).
- **Task 2** passed after one fix round.
- **The final whole-branch review** returned *Ready* with three Minor wording findings, all fixed.

## User decisions

Settled in the brainstorm (2026-11-13):

- **The warning:** one more layer of precaution. When the path column is `varchar`, Start shows a Continue / Cancel warning that `nvarchar` is preferred and that the app will
  convert the column. The user wrote "not `varchar`"; it was read as "is `varchar`", stated in the brainstorm and not corrected.
- **Remembered:** *Continue* lasts like the file-type warnings, until *Close project*, *Open project...* or a fresh launch.
- **Approach A:** the type is read in pre-flight, and the runs are unchanged. Asking from inside the run was rejected.
- **The user guide:** a section explaining the warning, how the conversion works, why it is done and its effects (8.4).
- **Version:** stays 2.0.0, and the changelog entry mentions the warning.
- **Parked:** the `DISTINCT` collation gap from milestone 4 (`ß`) waits until it is met in practice. It is recorded in the milestone 4 ledger.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

- **R1:** the remembered answer is keyed to the column. The key is server, database, table and path column as typed, each trimmed and compared ignoring case, so `dbo.T` and
  `[dbo].[T]` ask separately. The warning sits last in pre-flight, after the existing-report question. Cost if wrong: one comparison, or the warning moved.
- **R2:** the lookup is a courtesy and never stops a Start. It runs under `Task.Run`, so connection opening never blocks the UI. It is silent on any failure except
  out-of-memory, which SDD §9 treats as fatal. It is cancelled after 30 seconds, because it has no command timeout and a blocked `sp_describe_first_result_set` would otherwise
  keep Start busy indefinitely. The last two parts came from the task review. Cost if wrong: one constant, one filter.
- **R3:** `PathColumnType.Format` is made public and given a `varchar` case, for the warning's `<type>`. The refusal error text is unchanged. Cost if wrong: nothing.
- **R4** (after the final review; the user said an unreachable server should only be waited for once): pre-flight returns its lookup's result in a `PreflightResult`, and
  `AuditRequest.PathColumnType` carries it to the run. Both runners use it instead of reading the type again. A type, not found or a SQL error is passed on. A lookup that was
  abandoned or threw, or one that never ran because the answer was remembered, passes nothing, so the run reads the type itself. Errors still reach the user through the run's
  normal failure path. Cost if wrong: one optional record member.

## Defects found in the plan

- The plan's Global Constraints listed the lookup as `CancellationToken.None` with "any exception" silent. Both were refined by R2; the plan and the design carry a note.

## Commits

| Commit    | What                                                                                          |
|-----------|-----------------------------------------------------------------------------------------------|
| `a8eeb60` | Design, plan, ledger, `.slnx` registration                                                    |
| `78a99d0` | Task 1: the warning, the remembered column                                                    |
| `35bbf78` | Task 1 review: lookup under `Task.Run`; out-of-memory not swallowed                           |
| `d2454a5` | The lookup cancelled after 30 seconds (R2)                                                    |
| `fb79699` | Task 2: SDD and user guide (new 8.4, 3.1 trimmed, 9, 2.0.0 changelog)                         |
| `0c2c662` | Task 2 fix round: the warning's position wording, order, silent cases, delay, `?` in 8.4      |
| `aca652a` | Task 2 fix round: reflow                                                                      |
| `1caeadb` | Task 2 fix round: reflow                                                                      |
| `c1a7e1f` | Final review minors: the guide's timeout example, notes in the plan and design; this ledger   |
| (next)    | One wait for an unreachable server: pre-flight's lookup handed to the run (R4)                |

## Verification

- **Build:** clean, with 0 errors and 0 warnings, after every task and fix.
- **Task 1, in the app (UI Automation, LocalDB):**
  - **The text:** exact for `varchar(400)`, `varchar(5000)` and `varchar(max)`, with targets `nvarchar(400)`, `nvarchar(max)` and `nvarchar(max)`. The warning also appears
    on the Unindexed tab.
  - **Remembered:** a remembered Start made no pre-flight lookup, counted with Extended Events. Changing the table, the column or the database asked again, and so did *Open
    project...*.
  - **Cancel** started nothing.
  - **Silent cases:** `nvarchar`, `char(10)`, a missing column and a missing database stayed silent, and the run reported each one as before.
  - **Responsive:** the UI stayed responsive during the lookup.
- **Not exercised:**
  - *Close project* itself (it calls the same `Reset` as *Open project...*).
  - A truly unreachable server, before R4. After R4 the controller drove one in the real window (server `nonexistent-host-fa5`): the run's *could not read the file index table*
    error appeared 10 to 15 seconds after Start, with the run's Elapsed at `00:00:00` and status *Failed*, so there was one connection attempt.
  - The 30-second cancellation. Its path was confirmed by code reading in the final review: the cancellation reaches the catch, the token source is disposed only after the
    lookup completes, and nothing is left running.

## Outstanding items

- **Accepted:** with an unreachable server, the first Start for a column waits for one connection timeout, with no status text, before the run's error appears (R4 removed the
  second wait).
- **Accepted:** the remembered key is lexical (R1).

## Incidents

None.
