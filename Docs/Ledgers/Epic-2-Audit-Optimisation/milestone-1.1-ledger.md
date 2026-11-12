# SDD ledger — plan: Docs/Plans/Epic-2-Audit-Optimisation/milestone-1.1-nvarchar-path-column.md

**Date:** 2026-11-12

Branch `feat/milestone-1.1-nvarchar-path-column`, cut from `main` at `089ef2e`. No milestone spec: the approved description quoted in the plan is the spec.

## Pre-flight scan

| Tasks / task | Shared file or interface                     | Finding                                                                                       |
|--------------|----------------------------------------------|-----------------------------------------------------------------------------------------------|
| 1 ↔ 2        | SDD, user guide (2 documents what 1 builds)   | No code overlap; Task 2 reads Task 1's error copy and lookup from the code.                   |
| 1            | self                                         | Consistent: the lookup takes the quoted table name, so it needs the quoting the statements use. |
| 2            | self                                         | Consistent.                                                                                   |

## Rulings

- Ruling 1: an alias type over `nvarchar` is accepted (the check reads the system type) and `nchar` is refused — the user said `nvarchar` — costs a one-line change if wrong.
- Ruling 2: the type is read under each audit's existing first status, with no new status text; Stop during it is `StoppedBeforeChecking` — costs nothing if wrong.

## Progress
