# SDD ledger — plan: Docs/Plans/Epic-2-Audit-Optimisation/milestone-4-varchar-path-column.md

**Date:** 2026-11-13

Branch `feat/milestone-4-varchar-path-column`, cut from `main` at `1836c90` (tagged `v2.0.0`). Spec:
`Docs/Specs/Epic-2-Audit-Optimisation/milestone-4-varchar-path-column-design.md` (approved 2026-11-13).

## Pre-flight scan

| Tasks / task | Shared file or interface                    | Finding                                                                                |
|--------------|---------------------------------------------|----------------------------------------------------------------------------------------|
| 1 ↔ 2        | Rule, error copy, member names               | Task 2 documents what Task 1 builds; no code overlap.                                  |
| 1            | self                                        | Consistent: `QuoteTarget` → type → `Check` → `Build` / `BuildPaths` with `convertTo`.  |
| 2            | self                                        | Consistent.                                                                            |

## Rulings

- Ruling 1: the error code is renamed `Sql.PathColumnUnsupported` (the design left the name open), since "not nvarchar" is no longer the rule — costs a rename if wrong.

## Progress

- Setup: branch created; design, plan and ledger registered in the slnx.
