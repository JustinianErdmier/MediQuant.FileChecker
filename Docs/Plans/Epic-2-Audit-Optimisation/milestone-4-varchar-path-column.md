# Milestone 4 — varchar Path Column Implementation Plan

**Date:** 2026-11-13

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Both audits accept a `varchar(n)` or `varchar(max)` path column, converting it to `nvarchar` where the statements need it, and give the same results an `nvarchar`
column holding the same paths would give; every other non-`nvarchar` type stays refused.

**Architecture:** The runners check the column type before building their statements. `PathColumnType.Check` returns the conversion target (`null` for `nvarchar`,
`nvarchar(n)` or `nvarchar(max)` for `varchar`), and `QueryBuilder` builds from it: the Missing Files statements classify and select a converted value computed in one apply;
the Unindexed statement selects the raw column and converts only inside its folder filter.

**Tech Stack:** unchanged (.NET 10, Avalonia 11.3, Microsoft.Data.SqlClient). No new packages.

**Spec:** [`Docs/Specs/Epic-2-Audit-Optimisation/milestone-4-varchar-path-column-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-4-varchar-path-column-design.md),
with the master SDD [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) §6.2, §6.5.2, §9, §10 and §12, and milestone 1's design
[`milestone-1-row-accounting-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-1-row-accounting-design.md) §4 (the classification, which must stay exact).

## Global Constraints

- **No automated tests and no test projects.** Verify with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings), with
  LocalDB, and by running the app.
- **The rule** (the column's *system* type, so an alias type counts as the type it is built on):
  - `nvarchar(n)`, `nvarchar(max)` → no conversion; the statements are byte-for-byte today's.
  - `varchar(n)` with `n` ≤ 4,000 → `CAST(<column> AS nvarchar(n))`.
  - `varchar(n)` with `n` > 4,000, and `varchar(max)` (`max_length` -1) → `CAST(<column> AS nvarchar(max))`.
  - Anything else → refused, unchanged except the error's wording.
- **Error copy:** `The path column '<column>' is <type>. The File Auditor expects an nvarchar or varchar column.` (type formatting as today). The not-found error is unchanged.
- **Missing Files:** the converted value is computed in one `CROSS APPLY` (alias `[fa_p]`, column `[FaPath]`) right after `FROM … AS [fa_t]`; the classification, the `DISTINCT`
  select (`AS [Path]`), the empty-ID read and the counts use it wherever they use the column today. The narrow `CAST(… AS nvarchar(max))` inside the trim's `LEN` stays.
- **Unindexed Files:** `SELECT DISTINCT <raw column> AS [Path]` is unchanged; each folder-filter `LIKE` clause (the prefix clauses and the leading-white-space clause) compares the
  converted value instead of the column. No filter → no conversion.
- Type check order: under the run's existing first status (`Counting files...` / `Loading the index...`), before the statements are built; Stop during it as today
  (`StoppedBeforeChecking`).
- Do not measure the code-page behaviour of §6 of the design (the user's decision).
- All CLAUDE.md and SDD §12 conventions apply. Commit each task automatically, staging only its files and ticking its boxes here.

## Review Focus

1. **Same results as nvarchar.** A `varchar(400)` table and an `nvarchar(400)` copy of it (`INSERT … SELECT`) give the same counts, the same selected rows and the same empty IDs,
   on both tabs — Task 1 step 4.
2. **Length boundaries.** `varchar(4000)`, `varchar(4001)`, `varchar(8000)` and `varchar(max)` are accepted with the right target type, and a 4,000-character skip marker in a
   `varchar(4000)` column is classified as a skip marker (milestone 1.1's case) — Task 1 step 4.
3. **Still refused.** `char(10)`, `nchar(10)`, `text`, `ntext` and `int` are refused with the new copy — Task 1 step 4.
4. **nvarchar unchanged.** For an `nvarchar` column the built statements are identical to `main`'s — Task 1 step 4.
5. **Alias over varchar** is accepted — Task 1 step 4.

---

Before Task 1 the controller creates branch `feat/milestone-4-varchar-path-column` from `main`, adds the design doc, this plan and the ledger
(`Docs/Ledgers/Epic-2-Audit-Optimisation/milestone-4-ledger.md`) to `MediQuant.FileAuditor.slnx` after milestone 3's entries, and commits them as
`docs(📝): add the varchar path column design and plan`.

### Task 1: Accept varchar and convert it

**Files:**

- Modify: `Source/FileAuditor/Services/Sql/PathColumnType.cs`, `Services/Sql/QueryBuilder.cs`, `Common/Errors/SqlErrors.cs`, `Services/Auditing/AuditRunner.cs`,
  `Services/Auditing/UnindexedAuditRunner.cs`, and `Models/Auditing/AuditQuery.cs` / `PathQuery.cs` if members become unused

**Interfaces:**

- Produces: `QueryBuilder.QuoteTarget(string table, string pathColumn) : ErrorOr<(string QuotedTable, string QuotedPathColumn)>` — the parsing and quoting `Build` and
  `BuildPaths` do today, for the type check to use before either is called.
- Produces: `PathColumnType.Check(string table, string column, (string TypeName, short MaxLength)? found) : ErrorOr<string?>` — `null` for `nvarchar`, `"nvarchar(<n>)"` or
  `"nvarchar(max)"` for `varchar` per Global Constraints (`n` = `max_length`, bytes = characters for `varchar`), the existing errors otherwise.
- Changes: `QueryBuilder.Build(string table, string pathColumn, string idColumn, string? convertTo)` and
  `QueryBuilder.BuildPaths(string table, string pathColumn, IReadOnlyList<string> folders, string? convertTo)`.
- Changes: `SqlErrors.PathColumnNotNvarchar` → `SqlErrors.PathColumnUnsupported` (code `Sql.PathColumnUnsupported`), with the new copy.

- [ ] **Step 1: Check first.** Both runners: `QuoteTarget`, read the type, `Check`, then `Build` / `BuildPaths` with the result. Remove `QuotedTable` / `QuotedPathColumn` from
  `AuditQuery` and `PathQuery` if nothing else uses them.
- [ ] **Step 2: Missing Files statements.** The `[fa_p]` apply when `convertTo` is not `null`; every reference to `[fa_t].<column>` in the classification, the select, the
  empty-ID read and the counts becomes the path reference (`[fa_p].[FaPath]` or the column). Update the method's comments and `<remarks>`.
- [ ] **Step 3: Unindexed statement.** The filter's `LIKE` clauses use `CAST(<column> AS <convertTo>)` when `convertTo` is not `null`; the select is unchanged. Update the XML
  docs.
- [ ] **Step 4: Verify.** Clean build. In LocalDB scratch database `FileAuditorE2M4Scratch` (dropped at the end), with a harness in the scratchpad calling the real
  `IndexTableReader`, `PathColumnType`, `QueryBuilder` and runners:
    - milestone 1's boundary seed (milestone 1's plan, Task 1 Step 3) in a `varchar(400)` table (`Latin1_General_CI_AS`) and an `nvarchar(400)` copy made with
      `INSERT … SELECT`: counts, selected rows and empty IDs identical, with and without an ID column; and the seed in its original `nvarchar(400)` table still gives T 22, E 5,
      S 7, D 7 / 6, Dup 3 / 4, empty IDs 10–14;
    - the Review Focus 2, 3 and 5 types (exact target type or exact error text);
    - for an `nvarchar` column, `Build` and `BuildPaths` return exactly the statements `main` builds (compare strings against `git show main:…` built by the same harness);
    - an Unindexed run (harness or app) on a `varchar(400)` table with a one-folder filter: the same Indexed / Unindexed result as its `nvarchar` copy.
  Run the app once on each tab against the `varchar(400)` table to completion, and once against a `char(10)` column for the refusal dialogue (read by UI Automation). Close the app.
- [ ] **Step 5: Commit** — `feat(✨): accept a varchar path column and convert it to nvarchar`.

### Task 2: SDD, user guide and changelog

**Files:** Modify `Docs/Specs/1-file-auditor-master-design.md`, `Docs/UserGuide.md`

- [ ] **Step 1: SDD.** §6.2 (the rule, the conversion, the error copy, the type check before the statements are built), §6.5.2 (the conversion inside the filter only), §9 if
  member names changed, §10 (this milestone's calls, from the design and the ledger, and that milestone 1.1's "requires `nvarchar`" is superseded). Remove everything this makes
  false (search for `nvarchar`, `expects an nvarchar`, `PathColumnNotNvarchar`).
- [ ] **Step 2: User guide.** Section 3.1 (the path column may be `nvarchar` or `varchar`; why `nvarchar` is still preferred: a `varchar` column cannot store characters outside
  its code page, and those rows can never match a file), section 9's refusal entry (new message; only types other than `nvarchar` and `varchar`), sections 8 and 10 where they
  mention the rule, and the **2.0.0 changelog entry**: a `varchar` path column no longer stops a project working; only other types (`char`, `nchar`, `text`, numbers, …) do.
  Hard-wrap at 180 characters, reflowing whole paragraphs.
- [ ] **Step 3: Commit** — `docs(📝): document the varchar path column`.
