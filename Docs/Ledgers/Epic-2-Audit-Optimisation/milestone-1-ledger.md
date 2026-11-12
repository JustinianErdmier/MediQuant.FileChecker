# SDD ledger — plan: Docs/Plans/Epic-2-Audit-Optimisation/milestone-1-row-accounting.md

**Date:** 2026-11-12

Branch `feat/milestone-1-row-accounting`, cut from `main` at `b22d63c`.

## Groundwork

- `b22d63c` — `fix(🐛): check each distinct path in the index table once`. Made both directions' queries `DISTINCT`; amended the SDD and user guide.

## Incidents

- `b22d63c` was committed straight to `main`, outside any epic or plan, before this milestone existed. The user accepted it as is and asked for this epic to build on it; it is
  recorded here as the milestone's groundwork rather than rewritten.

## Pre-flight scan

| Tasks / task | Shared file or interface                                                                | Finding                                                                                                                         |
|--------------|-----------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------|
| 1 ↔ 2        | `AuditQuery`, `IndexTableReader.CountRowsAsync` (1 produces, 2 consumes in `AuditRunner`) | Task 1 removes `CountAsync` and `AuditQuery.CountSql`, which `AuditRunner` calls today, so Task 1 alone would not build. Ruling 1. |
| 1 ↔ 2        | `RowCounts`                                                                             | Consistent: 1 creates, 2 carries through progress and report.                                                                   |
| 2 ↔ 3        | `AuditProgress` (`TotalFiles`, `Skipped` kept, `Counts`, `Found` added)                   | Consistent: Task 2 keeps the two old properties so `AuditViewModel` builds until Task 3 reworks it.                             |
| 2 ↔ 3        | `AuditReport`                                                                           | Only `AuditRunner` constructs it and only `ReportBuilder` reads it; `MissingAuditRun` passes it through. No conflict.            |
| 2 ↔ 4, 3 ↔ 4 | SDD, user guide                                                                         | Task 4 documents what 2 and 3 build; no code overlap.                                                                           |
| 1            | self                                                                                    | Verify step expects T 22, E 5, S 7, D 7/6, Dup 3/4: recomputed from the seed list, consistent.                                  |
| 2            | self                                                                                    | Case (d) "empty table" gives D = 0, T = 0: no parentheses (`0`); "only empty and skip rows" gives `0 (n)`. Consistent with rules. |
| 3            | self                                                                                    | Consistent.                                                                                                                     |
| 4            | self                                                                                    | Consistent.                                                                                                                     |

## Rulings

- Ruling 1: Task 1 also updates `AuditRunner`'s count call minimally (`CountRowsAsync`, then `SetTotal(counts.Distinct)`) so its build is clean; Task 2 replaces that with
  `SetCounts` — every task must end on a clean build (CLAUDE.md) — costs nothing if wrong: Task 2 rewrites the same lines.
- Ruling 2: the project ledger (this file) is also the subagent-driven ledger; briefs, reports and review packages live in the git-ignored `.superpowers/sdd/` workspace —
  CLAUDE.md asks for one ledger per plan under `Docs/Ledgers/`, and two would drift — costs nothing if wrong.

## Progress
