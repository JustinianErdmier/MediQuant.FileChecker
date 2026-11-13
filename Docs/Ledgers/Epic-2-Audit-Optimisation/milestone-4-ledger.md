# Milestone 4 Ledger — varchar Path Column

**Date:** 2026-11-13

Ledger for [`Docs/Plans/Epic-2-Audit-Optimisation/milestone-4-varchar-path-column.md`](../../Plans/Epic-2-Audit-Optimisation/milestone-4-varchar-path-column.md), executed
on branch `feat/milestone-4-varchar-path-column` (cut from `main` at `1836c90`, tagged `v2.0.0`). Spec:
[`milestone-4-varchar-path-column-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-4-varchar-path-column-design.md), a short design approved on 2026-11-13. The master
SDD, the user guide and its 2.0.0 changelog entry were amended in Task 2 and the final fix. Both tasks are complete, each passing its review with Minor findings only, which the
controller fixed. The final whole-branch review found no code defect and one Important documentation error, which was fixed.

## User decisions

Settled in the brainstorm (2026-11-13):

- `varchar(n)` path columns are common in practice, so both audits accept `varchar(n)` and `varchar(max)` and convert them to `nvarchar`. Only `varchar` is added; `char`,
  `nchar`, `text`, `ntext` and the rest stay refused.
- **Approach A:** an explicit conversion, once per statement. The rejected alternatives were implicit conversion and conversion in the app.
- **The Unindexed audit:** the user asked whether it needs the conversion at all. It converts only inside its folder filter, because the filter's own collation would otherwise
  apply to the `varchar` value. The selected value stays the raw column.
- **No measurement:** that reasoning was not to be measured, only recorded.
- **Version:** stays 2.0.0, because the app is not published. The user guide and the 2.0.0 changelog entry are updated.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

- **R1:** the refusal reads `... expects an nvarchar or varchar column.`, and its code is renamed `Sql.PathColumnUnsupported` (the design left the name open). Cost if wrong: a
  rename.
- **R2:** on the Missing tab the conversion is computed in one apply (`[fa_p]`, column `[FaPath]`) that everything else uses. With an `nvarchar` column the statements are
  byte-identical to `main`'s. Cost if wrong: nothing.
- **R3:** the type check now runs before the statements are built (`QueryBuilder.QuoteTarget`). As a result, a malformed ID column is caught after the type read rather than
  before it. Pre-flight catches it first anyway. Cost if wrong: nothing visible.

## Defects found in the plan

- The design said a `varchar` column stores `?` for any character outside its code page, and that such rows are reported as non-existent paths. The final review showed both
  are wrong:
  - SQL Server best-fits where it can (`Ł` → `L`, `ő` → `o`), so a path may name a different, existing file.
  - A `?` path is *Unverified — Invalid characters in path* on the Missing tab, not *Missing*.
  
  The design, the SDD and the user guide are corrected.

## Commits

| Commit    | What                                                                                   |
|-----------|----------------------------------------------------------------------------------------|
| `466e5b7` | Design, plan, ledger, `.slnx` registration                                             |
| `c6bd5c3` | Task 1: accept `varchar`, the conversion, the type check before building               |
| `58f43e4` | Task 1 review minors: rewraps, comments, the unreachable `varchar` formatting case     |
| `e5c930a` | Task 2: SDD, user guide, 2.0.0 changelog entry                                         |
| `224576b` | Task 2 review minors: SDD paragraph reflows                                            |
| (next)    | Final review: the code-page wording, the UTF-8 comment, the §10 list; this ledger      |

## Verification

- **Build:** clean, with 0 errors and 0 warnings, after every task and fix.
- **Task 1's LocalDB harness** (calling the real reader, check, builder and runners):
  - **`varchar` against an `nvarchar` copy:** a `varchar(400)` table and an `nvarchar(400)` copy made from it gave identical counts, rows and empty IDs, with and without an ID
    column.
  - **Milestone 1's seed** still gives its figures.
  - **Boundaries:**
    - `varchar(4000)` → `nvarchar(4000)`;
    - `varchar(4001)`, `varchar(8000)` and `varchar(max)` → `nvarchar(max)`;
    - an alias over `varchar(300)` → `nvarchar(300)`;
    - a 4,000-character skip marker in a `varchar(4000)` column is classified correctly.
  - **Refused:** `char`, `nchar`, `text`, `ntext` and `int` are refused with the exact copy.
  - **`nvarchar` unchanged:** the statements matched `main`'s in 9 of 9 cases.
  - **The Unindexed filter on `varchar`** matched its `nvarchar` copy.
- **The app on both tabs:** `varchar(400)` runs completed, and the `char(10)` refusal text was exact.
- **The final review's LocalDB spot checks:**
  - **Filter conversion:** a `Greek_CI_AS` `varchar` path matched its folder prefix only with the conversion (1 against 0). That agrees with the design's §6 reasoning, which
    the user chose not to measure.
  - **Best-fit mapping:** `C:\Łódź\ő\日本.pdf` stored in a Latin-1 `varchar` became `C:\Lódz\o\??.pdf`.

## Outstanding items

- **Parked; brainstorm only if it is met in practice** (the user's decision, 2026-11-13: files with `ß` and similar are expected to be rare). `DISTINCT` on the path value follows
  Unicode comparison rules under a SQL collation. On a `SQL_Latin1_General_CP1_CI_AS` column, `Straße.pdf` and `Strasse.pdf` collapse into one row, so the Missing Files audit
  checks only one of two different files. `nvarchar` columns already behaved this way before this milestone, so it is not a regression. Fixing it (a binary `DISTINCT`) changes
  `nvarchar` behaviour and needs its own design.
- **Tag:** `v2.0.0` still points at `1836c90`. Moving it to include this milestone is the user's call (design §8).

## Incidents

None.
