# Milestone 1.1 — nvarchar Path Column Implementation Plan

**Date:** 2026-11-12

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Both audits require the path column to be `nvarchar(n)` or `nvarchar(max)` and refuse any other type with an error; the Missing Files classification then runs on
the column directly, without the `CAST(… AS nvarchar(max))` milestone 1 added.

**Architecture:** A new `IndexTableReader.ReadPathColumnTypeAsync` reads the path column's system type and length from `sys.columns`; a small `PathColumnType` check turns the
answer into success or an error. Both runners call it first, under their existing first status, before counting or loading the index. `QueryBuilder.Build` drops the cast and
the `[fa_v]` apply, and classifies `[fa_t].<column>` directly.

**Tech Stack:** unchanged (.NET 10, Avalonia 11.3, Microsoft.Data.SqlClient). No new packages.

**Spec:** no milestone spec: the approved description below is the spec, with the master SDD
[`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) §6.2, §6.4, §6.5.2 and §12, and milestone 1's design
[`milestone-1-row-accounting-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-1-row-accounting-design.md) §4 (the classification, which must stay exact).

The approved description (2026-11-12), in substance:

1. The cast to `nvarchar(max)` exists only to make the classification type-agnostic; it is the most expensive type to work on. Instead, **the app expects the path column to be
   `nvarchar`**: `nvarchar(n)` or `nvarchar(max)`. Anything else makes the audit fail before any row is read, and the user is told the column and its actual type. No report.
2. The rule is the same for **both** audits (the Unindexed audit does not need it today, but milestone 2 will build on it).
3. A path column that cannot be found is the same kind of error.
4. The ID column is unrestricted; it is still read as text.
5. The cast is removed; the classification runs on the column directly. The gain is measured before and after and recorded in the ledger.
6. The user guide explains briefly why `nvarchar` is required; the SDD records the rule.

## Global Constraints

- **No automated tests and no test projects.** Verify with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings) and by
  running the app, driven by Windows UI Automation from PowerShell (foreground check before any keystroke or click; reports read with `Get-Content`; close the app when done).
- SQL Server for verification: LocalDB `(localdb)\MSSQLLocalDB`, scratch database `FileAuditorE2M11Scratch`, dropped when the task ends. Throwaway harnesses live in the
  session scratchpad, never in the repository.
- **Accepted types:** the column's *system* type (`TYPE_NAME(system_type_id)`, so an alias type over `nvarchar` passes) is `nvarchar`, any length including `max`
  (`max_length = -1`). Everything else is refused, `nchar` included.
- Exact copy (error descriptions, shown in the existing error dialogue):
  - `The path column '<column>' is <type>. The File Auditor expects an nvarchar column.` — `<type>` is the system type name in lower case, with `(<n>)` for `char`,
    `varchar`, `binary`, `varbinary` (`n` = `max_length`), `nchar` (`n` = `max_length / 2`), and `(max)` when `max_length = -1`; any other type by name alone (`int`).
  - `The path column '<column>' was not found in the table '<table>'.` — `<column>` and `<table>` as the user typed them.
- The type is read under the run's existing first status (`Counting files...` / `Loading the index...`); no new status text. Stop during it behaves as Stop while counting or
  loading does today (`StoppedBeforeChecking`, no report). A SQL error reading it is `Sql.QueryFailed`, as today.
- The classification stays **exactly** milestone 1's rule; Task 1 re-runs its boundary seed (T 22, E 5, S 7, D 7 with ID, 6 without; Dup 3 / 4).
- All CLAUDE.md and SDD §12 conventions apply. Commit each task automatically, staging only its files and ticking its boxes here.

## Review Focus

1. **Type boundaries.** `nvarchar(400)`, `nvarchar(4000)`, `nvarchar(max)` and an alias type over `nvarchar` pass; `varchar(400)`, `varchar(max)`, `nchar(10)`, `int`,
   `uniqueidentifier` are refused with the exact text — Task 1 step 4.
2. **Names that need quoting.** A table in a non-default schema, a table and a column with spaces or `]` in their names are found (the lookup uses the quoted name the
   statements use) — Task 1 step 4.
3. **Missing table or column.** Either gives the not-found error, on both tabs, before any row is read — Task 1 step 4.
4. **Both tabs.** The Unindexed tab refuses the same columns with the same text — Task 1 step 4.
5. **The classification unchanged.** The milestone 1 seed gives the same figures without the cast — Task 1 step 4.

---

Before Task 1 the controller creates branch `feat/milestone-1.1-nvarchar-path-column` from `main`, adds this plan and the ledger
(`Docs/Ledgers/Epic-2-Audit-Optimisation/milestone-1.1-ledger.md`) to `MediQuant.FileAuditor.slnx` after milestone 1's entries, and commits them as
`docs(📝): add the nvarchar path column plan`.

### Task 1: The type check, and the classification without the cast

**Files:**

- Create: `Source/FileAuditor/Services/Sql/PathColumnType.cs`
- Modify: `Source/FileAuditor/Services/Sql/IndexTableReader.cs`, `Services/Sql/QueryBuilder.cs`, `Common/Errors/SqlErrors.cs`, `Services/Auditing/AuditRunner.cs`,
  `Services/Auditing/UnindexedAuditRunner.cs`

**Interfaces:**

- Produces: `IndexTableReader.ReadPathColumnTypeAsync(string connectionString, string tableSql, string column, CancellationToken cancellationToken) :
  Task<ErrorOr<(string TypeName, short MaxLength)?>>` — `null` when no such column (or table); errors and cancellation as the other reader methods. `tableSql` is the quoted
  multipart name the statements use, passed as a parameter to `OBJECT_ID(@table)`; `column` is the unquoted name, compared with `sys.columns.name`.
- Produces: `public static class PathColumnType { public static ErrorOr<Success> Check(string table, string column, (string TypeName, short MaxLength)? found); }` — formats the
  type per Global Constraints.
- Produces: `SqlErrors.PathColumnNotNvarchar(string column, string type)` and `SqlErrors.PathColumnNotFound(string column, string table)` (codes `Sql.PathColumnNotNvarchar`,
  `Sql.PathColumnNotFound`), with the exact descriptions.
- `QueryBuilder` exposes the quoted table name both runners need (e.g. a `QuotedTable` member on `AuditQuery` and `PathQuery`, or a public helper) — the implementer's choice,
  provided both runners and the lookup use the same quoting.

- [ ] **Step 1: Lookup and check.** The statement: `SELECT TYPE_NAME(c.system_type_id), c.max_length FROM sys.columns AS c WHERE c.object_id = OBJECT_ID(@table) AND
  c.name = @column;` (both `NVarChar` parameters). Zero rows → `null`.
- [ ] **Step 2: Runners.** `AuditRunner` and `UnindexedAuditRunner` build their query, then read and check the type before counting / loading; any error returns as the run's
  error (no report), cancellation as `StoppedBeforeChecking`. Update the XML docs of `RunAsync` (`<returns>`).
- [ ] **Step 3: No cast.** In `QueryBuilder.Build`, remove the `[fa_v]` apply and use `[fa_t].<column>` wherever `[fa_v].[FaValue]` was; keep everything else (binary
  collation, `@ws`, the prefilter, the weighted sum). Update the comments that mention the cast.
- [ ] **Step 4: Verify.** Clean build. In `FileAuditorE2M11Scratch`:
    - the milestone 1 seed (the list in milestone 1's plan, Task 1 Step 3, in an `nvarchar(400)` column): the four counts both ways unchanged, `SelectSql` the real rows,
      `EmptyIdsSql` IDs 10–14;
    - timing: a 200,000-row table (140,000 real, 40,000 empty, 20,000 `SKIP`, `nvarchar(400)`): `CountsSql` and `SelectSql` with the cast (milestone 1's statements, from
      `git show main:Source/FileAuditor/Services/Sql/QueryBuilder.cs`) and without, three runs each, recorded in the report;
    - in the app, on both tabs: the Review Focus 1–3 columns and names (one table per case), reading each error dialogue's text by UI Automation; an accepted column
      runs to completion.
  Drop the database.
- [ ] **Step 5: Commit** — `perf(⚡): require an nvarchar path column and classify it without a cast`.

### Task 2: SDD and user guide

**Files:** Modify `Docs/Specs/1-file-auditor-master-design.md`, `Docs/UserGuide.md`

- [ ] **Step 1: SDD.** §6.1 or §6.2 (the type rule, the lookup, the two errors, both audits), §6.2 (the statements without the cast; remove the cast's rationale), §6.5.2 (the
  Unindexed audit now checks the type first), §10 (this milestone's rulings, from the ledger). Remove every statement this milestone makes false.
- [ ] **Step 2: User guide.** Where the File Index Table inputs are described: the path column must be `nvarchar` (Unicode); a short reason in plain words — the app must read
  each path exactly as Windows sees it, character for character, and only a Unicode column guarantees that; other types are refused with a message naming the type. The
  troubleshooting/FAQ section gets the two error messages and what to do (ask for the column to be converted, or use a view that casts it). Hard-wrap at 180 characters.
- [ ] **Step 3: Commit** — `docs(📝): document the nvarchar path column rule`.
