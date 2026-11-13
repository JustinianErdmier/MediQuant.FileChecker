# Milestone 4 — varchar Path Column: Design

**Date:** 2026-11-13

A short design: a small change, agreed in conversation on 2026-11-13. The master SDD
([`1-file-auditor-master-design.md`](../1-file-auditor-master-design.md)) stays the contract; this document records what milestone 4 changes and why. It amends milestone
1.1's rule ([`milestone-1.1-nvarchar-path-column.md`](../../Plans/Epic-2-Audit-Optimisation/milestone-1.1-nvarchar-path-column.md)).

## 1. Intent

Milestone 1.1 made both audits refuse any path column that is not `nvarchar`. Testing since has shown that `varchar(n)` path columns are common in the index tables this app
audits, so refusing them makes the app unusable on those tables. The audits should accept a `varchar` path column and convert it to `nvarchar` where the statements need it.

The app is not yet published, so the version stays 2.0.0; the user guide and the 2.0.0 changelog entry are updated to match.

## 2. Goals, non-goals

- **Goals:** accept `varchar(n)` and `varchar(max)` path columns on both tabs, with the same results an `nvarchar` column holding the same paths would give; keep `nvarchar`
  columns running exactly as they do now.
- **Non-goals:** accepting any other type (`char`, `nchar`, `text`, `ntext` and the rest stay refused, the user's decision: they are padded or deprecated, and none has been met);
  recovering characters a `varchar` column could not store.
- **Anti-goal:** a file reported as *unindexed*, or a path as *missing*, because of the conversion.

## 3. The rule (the user's)

| Path column (system type, so an alias type counts as the type it is built on) | Accepted | The audits work on                       |
|---|---|---|
| `nvarchar(n)`, `nvarchar(max)`                                                 | Yes      | the column itself, as today               |
| `varchar(n)`, `n` ≤ 4,000                                                     | Yes      | `CAST(<column> AS nvarchar(n))`           |
| `varchar(n)`, `n` > 4,000, and `varchar(max)`                                 | Yes      | `CAST(<column> AS nvarchar(max))`         |
| anything else (`char`, `nchar`, `text`, `ntext`, numbers, binary, …)          | No       | refused before any row is read, as today  |

- A `varchar(n)` holds at most *n* characters, so `nvarchar(n)` holds every value, UTF-8 collations included. `nvarchar(n)` stops at 4,000, hence `max` above it.
- The refusal error changes its second sentence (Claude's call): `The path column '<column>' is <type>. The File Auditor expects an nvarchar or varchar column.` The type
  formatting, the not-found error and everything else about the check are unchanged.
- **What the conversion cannot do:** a `varchar` column stores only the characters of its collation's code page; anything else was changed when the row was written, to a near
  equivalent where SQL Server has one (`Ł` → `L`) and to `?` otherwise (corrected after the final review, which found the best-fit mapping). Such a path may name a different
  file, which may exist, or none; a `?` path is *Unverified — Invalid characters in path* on the Missing Files tab and never matches on the Unindexed Files tab. The user
  guide says so where it explains the path column.

## 4. Approach A (chosen with the user): an explicit conversion, once per statement

- The type check moves ahead of building the statements, and the statements are built from a **path expression**: the column for `nvarchar`, the `CAST` of §3 for `varchar`.
- **Missing Files:** the expression is computed once per row (an apply, as milestone 1 did with its `nvarchar(max)` cast) and everything after it — the classification, the
  `DISTINCT` select, the empty-ID read and the counts — uses it unchanged. The narrow `nvarchar(max)` cast inside the trim's length (milestone 1.1) stays, for the 4,000-character
  case it already covers.
- **Unindexed Files:** the selected value stays the raw column (SqlClient decodes `varchar` into the right string, and the app hashes it), so `SELECT DISTINCT` is unchanged. Only
  the **folder filter** uses the expression: both its `LIKE` clauses compare the expression, not the column, because the filter applies its own collation
  (`Latin1_General_100_CI_AS`, `Latin1_General_100_BIN2`), and on a `varchar` value that collation is applied before the comparison with the `nvarchar` parameters (the reason is
  in §6). With the filter dropped (no folder, more than 20, an empty one) there is no conversion at all.
- `nvarchar` columns produce exactly today's statements.

### Approaches considered

- **A (chosen):** the explicit conversion above. The statements say what they do, and the conversion is paid once per row.
- B: let SQL Server convert implicitly where the column meets an `N'…'` pattern. Almost no code, but the conversion happens inside each of the classification's comparisons, and
  trimming and the length trick would rest on unwritten rules — hard to review or trust.
- C: convert in the app. Impossible for the Missing Files audit, whose counts and classification run in SQL.

## 5. Cost

A bounded `CAST` to `nvarchar(n)` is cheap; the slow case is `varchar(max)` (or `varchar(n)` above 4,000), whose `nvarchar(max)` conversion milestone 1.1 measured at roughly
twice the counting time of the column itself. On the Unindexed tab the conversion only replaces the implicit one SQL Server would make anyway to compare with the parameters.
Not re-measured in this milestone.

## 6. Recorded, not measured (the user's decision)

The reason for converting inside the Unindexed folder filter is reasoned, not tested: a `COLLATE` clause on a `varchar` value whose column collation uses another code page
(Japanese, Greek, Cyrillic) is believed to translate the value through the target collation's code page (Latin-1 here) before the comparison, so a path with characters
outside Latin-1 could miss its folder's prefix, its index row would not be loaded, and the file would be reported unindexed. Converting to `nvarchar` first avoids that. If it is
ever measured — a non-Latin `varchar` column, the filter with and without the conversion — and the conversion proves unnecessary, the Unindexed audit can drop it.

## 7. What changes

- Code: the type check (`PathColumnType`: accepts `varchar` and yields the path expression), the error copy (`SqlErrors`), `QueryBuilder.Build` and `BuildPaths` (built from the
  expression, after the check), both runners (check, then build).
- SDD §6.2, §6.5.2 and §10; the user guide's path column description (section 3.1), its troubleshooting entry for the refusal (section 9), its sections 8 and 10 where they
  mention `nvarchar`, and the 2.0.0 changelog entry (a `varchar` path column no longer stops a project working; other types still do).

## 8. Open item for the user

The tag `v2.0.0` already points at `1836c90` on GitHub. If 2.0.0 is to include this milestone, the tag has to move to the new `main` (deleting and re-pushing a tag is the
user's call), or be left as a marker of the earlier state.

## 9. Left to the builder

The names of the path-expression member and of the renamed error (its code, `Sql.PathColumnNotNvarchar`, may keep its name or be renamed to match the new rule); where the type
check sits in each runner relative to the existing `Counting files...` / `Loading the index...` status (it stays under that status, as in milestone 1.1).
