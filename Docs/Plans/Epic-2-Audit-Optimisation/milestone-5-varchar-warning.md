# Milestone 5 — varchar Warning Implementation Plan

**Date:** 2026-11-13

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On Start, when the path column is `varchar`, pre-flight shows a Continue / Cancel warning that `nvarchar` is preferred and the app will convert the column; *Continue*
is remembered for that column until the project is closed or reopened. The user guide gains a section on `varchar` path columns.

**Architecture:** `PreflightChecker` gains a last step: unless the answer is remembered for the current column, it reads the type with the runs' own lookup
(`QueryBuilder.QuoteTarget`, `ConnectionStrings.Build`, `IndexTableReader.ReadPathColumnTypeAsync`, `PathColumnType.Check`) and warns for `varchar`. `ProjectSettingsService`
remembers the confirmed column and forgets it in `Reset`. The runs do not change.

**Tech Stack:** unchanged (.NET 10, Avalonia 11.3, Microsoft.Data.SqlClient). No new packages.

**Spec:** [`Docs/Specs/Epic-2-Audit-Optimisation/milestone-5-varchar-warning-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-5-varchar-warning-design.md), with the
master SDD [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) §6.1, §6.5.1, §8.1, §9, §10 and §12.

## Global Constraints

- **No automated tests and no test projects.** Verify with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings), with
  LocalDB, and by running the app.
- **Exact copy** (`DialogueService.ConfirmAsync`, warning icon, confirm text `_Continue`, then *Cancel*):
  `The path column '<column>' is <type>.\nThe File Auditor prefers an nvarchar column. It can convert this one to <target> and audit it, but a varchar column may have changed
  some characters in its paths when they were stored. See "varchar Path Columns" in the user guide.\n\nDo you want to continue?` — one string; the line break after the first
  sentence and the blank line before the question are the only breaks. `<column>` as the user typed it (trimmed); `<type>` `varchar(<n>)` or `varchar(max)`; `<target>` exactly
  what `PathColumnType.Check` returns (`nvarchar(<n>)` or `nvarchar(max)`).
- **When:** the last pre-flight step, after the existing-report check (and its *Overwrite* question), on both tabs, only when the answer is not remembered for the current column.
  *Cancel* → `RunAsync` returns `null` (no run, nothing saved).
- **Silent cases:** `nvarchar`, a refused type, a missing column or table, any SQL error or exception from the lookup → no dialogue, the Start goes on, and the run's own check
  reports it as today.
- **Remembered:** the confirmed column key is server, database, table and path column, each trimmed and compared ignoring case; any difference asks again. Forgotten in
  `ProjectSettingsService.Reset` (with the other remembered answers); never saved in the project file. While remembered for the current column, no lookup is made.
- The runs, the conversion and the version (2.0.0) do not change.
- All CLAUDE.md and SDD §12 conventions apply. Commit each task automatically, staging only its files and ticking its boxes here.

## Review Focus

1. **Remembered per column.** After *Continue*, a second Start on the same column shows nothing and makes no lookup; changing the table (or the path column, or the database)
   asks again; *Close project* / *Open project...* asks again — Task 1 step 3.
2. **Cancel.** *Cancel* starts no run and writes no report — Task 1 step 3.
3. **Silent cases.** `nvarchar`, a `char(10)` column, a missing column and an unreachable database give no warning, and the run then reports as it did before — Task 1 step 3.
4. **The text.** For `varchar(400)`, `varchar(5000)` and `varchar(max)` the dialogue reads exactly as specified, with `nvarchar(400)`, `nvarchar(max)` and `nvarchar(max)` —
   Task 1 step 3.
5. **The UI stays responsive** while the lookup runs (it is awaited, never blocking the UI thread) — Task 1 step 3.

---

Before Task 1 the controller creates branch `feat/milestone-5-varchar-warning` from `main`, adds the design doc, this plan and the ledger
(`Docs/Ledgers/Epic-2-Audit-Optimisation/milestone-5-ledger.md`) to `MediQuant.FileAuditor.slnx` after milestone 4's entries, and commits them as
`docs(📝): add the varchar warning design and plan`.

### Task 1: The warning

**Files:** Modify `Source/FileAuditor/Services/Auditing/PreflightChecker.cs`, `Services/Settings/ProjectSettingsService.cs`; `Services/Sql/PathColumnType.cs` only if its type
formatting is made reusable for `<type>`.

**Interfaces:**

- Produces: `ProjectSettingsService.VarcharConfirmedFor : string?` — the confirmed column's key (`null` when none), set by pre-flight on *Continue*, cleared in `Reset`.
- Consumes (existing): `QueryBuilder.QuoteTarget`, `ConnectionStrings.Build(server, database)`, `IndexTableReader.ReadPathColumnTypeAsync(connectionString, tableSql, columnSql,
  CancellationToken.None)`, `PathColumnType.Check`.

- [ ] **Step 1: Remember.** Add the member and clear it in `Reset`; document it like the other remembered answers.
- [ ] **Step 2: Warn.** In `PreflightChecker.RunAsync`, the last step before returning the report: the key, the lookup (only when not remembered), the warning for `varchar`, and
  the silent cases per Global Constraints. Update the class and method XML docs (the summary that says pre-flight runs "before the database is touched").
- [ ] **Step 3: Verify.** Clean build. LocalDB scratch database `FileAuditorE2M5Scratch` (dropped at the end) with tables of `varchar(400)`, `varchar(5000)`, `varchar(max)`,
  `nvarchar(400)` and `char(10)` path columns; project files in the scratchpad opened through *File > Open project...*. In the app, read each dialogue's text by UI Automation and
  exercise Review Focus 1–4 on the Missing Files tab, and the `varchar(400)` warning once on the Unindexed Files tab. For the unreachable database, use a database name that does
  not exist. Close the app.
- [ ] **Step 4: Commit** — `feat(✨): warn before auditing a varchar path column`.

### Task 2: SDD and user guide

**Files:** Modify `Docs/Specs/1-file-auditor-master-design.md`, `Docs/UserGuide.md`

- [ ] **Step 1: SDD.** §6.1 (the warning, its position, the remembered column, the silent cases, and the changed "before touching the database" principle), §6.5.1 if it lists the
  warnings, §8.1 (the remembered answers), §10 (this milestone's calls, from the design and the ledger). Remove anything this makes false.
- [ ] **Step 2: User guide.** New section **8.4 varchar Path Columns** with the four parts of the design's §6 (the warning; how the conversion works; why it is done; possible
  effects); section 3.1 keeps the accepted types and points to 8.4; section 9 gains an entry for the warning; the **2.0.0 changelog entry** mentions the warning. Keep the
  version 2.0.0. Hard-wrap at 180 characters, reflowing whole paragraphs.
- [ ] **Step 3: Commit** — `docs(📝): document the varchar warning`.
