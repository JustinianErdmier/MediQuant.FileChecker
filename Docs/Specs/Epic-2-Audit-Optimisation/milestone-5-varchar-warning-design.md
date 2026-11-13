# Milestone 5 — varchar Warning: Design

**Date:** 2026-11-13

A short design: a small change, agreed in conversation on 2026-11-13. The master SDD ([`1-file-auditor-master-design.md`](../1-file-auditor-master-design.md)) stays the
contract; this document records what milestone 5 changes and why. It builds on milestone 4
([`milestone-4-varchar-path-column-design.md`](milestone-4-varchar-path-column-design.md)), which made both audits accept a `varchar` path column and convert it.

## 1. Intent

Milestone 4 converts a `varchar` path column silently. A `varchar` column may already have changed characters in its paths when they were stored (a look-alike letter or `?`
in place of a character outside its code page), and the conversion cannot undo that. The user wants one more layer of precaution: before auditing a `varchar` column, the app
warns that `nvarchar` is preferred, says it will convert the column, and lets the user cancel or continue. The user guide explains the warning, how the conversion works, why it
is done and what its effects can be.

## 2. Goals, non-goals

- **Goals:** a Continue / Cancel warning on Start, on both tabs, when the path column is `varchar`; the answer remembered (§4); a user guide section on `varchar` path columns.
- **Non-goals:** any change to what the conversion does or to the runs; a warning for `nvarchar` (nothing to warn about) or for other types (already refused).
- **Anti-goal:** a warning so frequent it gets clicked through unread.

## 3. The warning (the user's)

```text
┌ File Auditor ──────────────────────────────────────────── × ┐
│  /!\  The path column 'MQ_FullPath' is varchar(400).        │
│       The File Auditor prefers an nvarchar column. It can   │
│       convert this one to nvarchar(400) and audit it, but   │
│       a varchar column may have changed some characters in  │
│       its paths when they were stored. See "varchar Path    │
│       Columns" in the user guide.                           │
│                                                             │
│       Do you want to continue?                              │
│                                       [Continue]  [Cancel]  │
└─────────────────────────────────────────────────────────────┘
```

- The ordinary warning dialogue (`DialogueService.ConfirmAsync`, warning icon, *Continue* with access key `C`, then *Cancel*), as the file-type warnings use.
- Text, with `<column>` as the user typed it, `<type>` the column's type formatted as in the refusal error (`varchar(400)`, `varchar(max)`) and `<target>` the type it converts
  to (`nvarchar(400)`, or `nvarchar(max)` above 4,000 characters and for `varchar(max)`):
  `The path column '<column>' is <type>.` / `The File Auditor prefers an nvarchar column. It can convert this one to <target> and audit it, but a varchar column may have changed
  some characters in its paths when they were stored. See "varchar Path Columns" in the user guide.` / blank line / `Do you want to continue?`
- *Cancel* stops the Start, as the other warnings do: no run, no report, nothing saved.
- It comes after the other pre-flight checks (and their warnings) and before the save rule, so a run that would fail pre-flight for another reason never reaches the database.

## 4. Remembering the answer (the user's, with Claude's scope)

- *Continue* is remembered like the file-type warnings: not asked again until *Close project*, *Open project...* or a fresh launch, and never saved in the project file (the
  user's decision).
- **Claude's call:** the remembered answer belongs to the column it was given for — server, database, table and path column as typed (compared ignoring case and surrounding
  white space). Changing any of them asks again. The file-type warnings are about the user's own inputs; this one is about a database column, so a different column deserves its
  own answer.
- While the answer is remembered for the current column, pre-flight does not read the type at all.

## 5. Approach A (chosen with the user): the type read in pre-flight

- When the warning has not been answered for the current column, pre-flight reads the path column's type with the lookup the runs use (`IndexTableReader.ReadPathColumnTypeAsync`
  on the quoted names, then `PathColumnType.Check`) and shows the warning when the column is `varchar`.
- For `nvarchar` it shows nothing. For a refused type, a missing column or a SQL error, pre-flight shows nothing either and lets the run start: the run's own type check reports
  those exactly as today, so their errors, attention signal and report behaviour do not change. (Added during execution, ledger ruling R1–R2: the same holds for any exception
  except out-of-memory, and for a lookup cancelled after 30 seconds.)
- The run is unchanged: it still reads and checks the type itself. The second read is one metadata call.
- **What changes in a principle:** SDD §6.1 says pre-flight runs *before touching the database*. That becomes "before touching the database, except for this one type lookup,
  made only while the warning is unanswered". A slow or unreachable server now delays the step after Start by the lookup's connection time, with no status text, before the
  run's own error appears.

### Approaches considered

- **A (chosen):** the type read in pre-flight, as above. Reuses the warning pattern; the runs stay as they are.
- B: ask from inside the run when its type check finds `varchar`. Keeps pre-flight free of the database and reads the type once, but needs a question opened mid-run, plumbing
  from the background run to the window, a clock that counts while the user reads, and a new way for a run to end (cancelled, no report).

## 6. The user guide

- A new section **8.4 varchar Path Columns** under *Key Optimisations & Design Choices*:
  - **The warning:** when it appears, what *Continue* and *Cancel* do, and how long the answer is remembered (§4).
  - **How the conversion works:** `varchar(n)` → `nvarchar(n)`; over 4,000 characters or `max` → `nvarchar(max)`; the Missing Files audit converts each row once and works on
    the converted path throughout; the Unindexed Files audit selects the column as it is and converts only inside its folder filter (none without a filter).
  - **Why it is done:** the Missing Files checks need one consistent Unicode form of each path (the white-space and skip-marker tests are Unicode rules); the folder filter's
    case-insensitive comparison would otherwise compare a `varchar` path in the wrong code page, and a path outside Latin-1 could miss its folder and be reported unindexed.
  - **Possible effects:** characters the column could not store were replaced when the row was written, by a look-alike letter (`Ł` became `L`) or `?`. A replaced path may name
    a different file: if that file exists, the Missing Files audit reports the row as *Found* although the file it was meant to name may be missing, and the Unindexed Files
    audit counts that other file as indexed. A path holding `?` is listed under *Unverified — Invalid characters in path*. A `varchar(max)` column (or one over 4,000 characters)
    counts more slowly. The cure is to convert the column to `nvarchar`.
- Section 3.1's path column paragraph keeps the accepted types and points to 8.4 for the rest; section 9 gains a short entry for the warning; the 2.0.0 changelog entry mentions
  the warning.

## 7. What changes

- Code: `PreflightChecker` (the lookup and the warning, after the existing checks), `ProjectSettingsService` (the remembered column, reset where the other remembered answers are
  reset), and a way for pre-flight to build the quoted names and the connection string (`QueryBuilder.QuoteTarget`, `ConnectionStrings.Build`, both existing).
- SDD §6.1 (the warning and the changed principle), §6.5.1 if it lists the warnings, §8.1 (the remembered answers), §10 (this milestone's calls).
- User guide sections 3.1, 8.4 (new), 9 and the 2.0.0 changelog entry. The version stays 2.0.0.

## 8. Left to the builder

The name of the remembered-column member; whether the warning's text is built in `PreflightChecker` or in a small helper; whether the formatting of `<type>` and `<target>`
reuses `PathColumnType`'s formatting (preferred) or `Check`'s result.
