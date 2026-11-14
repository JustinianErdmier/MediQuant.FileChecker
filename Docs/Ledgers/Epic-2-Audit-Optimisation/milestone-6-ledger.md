# SDD ledger — plan: Docs/Plans/Epic-2-Audit-Optimisation/milestone-6-streaming-missing-audit.md

**Date:** 2026-11-14

Branch `feat/milestone-6-streaming-missing-audit`, cut from `9c6d5ce` (the pushed `main`, tag `v2.0.0`), not from local `main`. Spec:
`Docs/Specs/Epic-2-Audit-Optimisation/milestone-6-streaming-missing-audit-design.md` (approved 2026-11-14, after a builder check).

## Pre-flight scan

| Tasks / task | Shared file or interface                      | Finding                                                                                       |
|--------------|-----------------------------------------------|-----------------------------------------------------------------------------------------------|
| 1 ↔ 2        | `AuditProgress` live members                  | Task 2 consumes Task 1's members (names in the plan's Interfaces); Task 1 must not edit the view. |
| 1 ↔ 3, 2 ↔ 3 | Copy, Summary order, layout                   | Task 3 documents what 1 and 2 build; no code overlap.                                         |
| 1, 2, 3      | self                                          | Consistent.                                                                                   |

## Rulings

- Ruling 1: the branch is cut from `9c6d5ce`, not from local `main`, whose top commit `6ea67e8` (the user's `Docs/Temp` reports, with internal server and share names) is not
  pushed; branching from it would publish those reports in the PR. The reports stay on local `main` until the post-merge clean-up — costs nothing if wrong.

## Notes

- The debug reports are readable with `git show main:Docs/Temp/<file>` (DebugReportV1.0.0_1/_2, DebugReportV2.0.0_1/_2).
- `FileAuditorPerf` (LocalDB): `dbo.Idx (Id int NOT NULL, P varchar(8000) NULL)`, clustered on `Id`; 1,540,598 rows
  `'\\fs01\docs\P' + RIGHT('0000' + CAST(i % 5000 AS varchar(10)), 5) + '\Doc_' + CAST(i AS varchar(20)) + '.pdf'`, plus the first 29,867 repeated with `Id` + 2,000,000.

## Progress

- Setup: branch created; design, plan and ledger registered in the slnx.
