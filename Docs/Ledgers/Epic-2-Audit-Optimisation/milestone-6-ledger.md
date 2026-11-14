# Milestone 6 Ledger — Streaming Missing Files Audit

**Date:** 2026-11-14

Ledger for [`Docs/Plans/Epic-2-Audit-Optimisation/milestone-6-streaming-missing-audit.md`](../../Plans/Epic-2-Audit-Optimisation/milestone-6-streaming-missing-audit.md),
executed on branch `feat/milestone-6-streaming-missing-audit`. Spec:
[`milestone-6-streaming-missing-audit-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-6-streaming-missing-audit-design.md), approved on 2026-11-14 after a
builder check. The master SDD, the user guide (the 2.0.0 changelog included) and the `row-accounting-stats.html` mock-up were amended.

All three tasks are complete:
- **Task 1** passed review after one rewrap round.
- **Task 2** passed review first time.
- **Task 3**'s review was folded into the final review.
- **The final whole-branch review** found nothing Critical or Important; its 13 Minor findings were fixed in one wave.

## Why this milestone exists

Debug builds of 1.0.0 and 2.0.0 (branches `diag/v1.0.0-debug-report` and `diag/v2.0.0-debug-report`, reports in local `main`'s `Docs/Temp/`) showed:
- **Run-to-run swings were the file server.** `File.GetAttributes` averaged anywhere from 17.2 ms to 1.65 ms per file across the night, and checking was always the bottleneck.
- **2.0.0 also added a fixed ~85 seconds of SQL before checking.** The counts statement took 45–46 s and the blocking `DISTINCT` select 38–40 s to its first row, on a
  `varchar(8000)` column converted to `nvarchar(max)`.

## User decisions

Settled in the brainstorm (2026-11-14):

- **Approach:** C, de-duplication in the app, chosen over A (one `DISTINCT` into a temporary table) and B (a bounded conversion). It was refined by the user: the up-front
  classification is dropped too, so the Missing audit runs 1.0.0's two statements and converts nothing. The user asked why the cast was still needed with C, and that question
  led to this.
- **Live counts:** the counts become live, with a new *Distinct* statistic between *Total files* and *Duplicates* (the user's design), rather than switching *Total files*
  to `distinct (rows)` at the end.
- **Duplicates keeps its rows meaning:** one per extra row, so everything adds up to *Total files*. The user first read *Distinct* as paths listed once and asked; the
  rows meaning was confirmed.
- **The Unindexed filter:** it converts to `nvarchar(4000)` only (the user asked whether it needs the conversion at all; only the prefix test does).
- **Path length:** a correction for the record. The user remembered Windows' path limit as 200 characters; it is 260 (`MAX_PATH`). With no conversion left, there is
  nothing to size anyway.
- **Clean-up after the merge:** remove `Docs/Temp/` and both `diag/` branches.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

- **R1:** the branch is cut from `9c6d5ce` (the pushed `main`), not local `main`. Local `main`'s top commit `6ea67e8` holds the user's `Docs/Temp` reports, with internal server
  and share names, and is not pushed; branching from it would have published them in the PR. Cost if wrong: nothing.
- **R2:** the de-duplication key is `TrimEnd(' ')` (U+0020 only), `ToUpperInvariant`, and the ID joined unambiguously (a length prefix and an ID-present flag), as an MD5 128-bit
  hash. A leading space or a trailing tab keeps a row distinct, so it gets checked. A side effect: `Straße` and `Strasse` are now distinct, which retires milestone 4's parked
  gap. Cost if wrong: the key function.
- **R3:** *Distinct* is counted when a key first enters the set, and floored at *Checked* on screen. *Remaining* uses rows handled and *Files/second* has its own recent rate.
  Cost if wrong: display only.
- **R4:** *Not checked* = max(0, Total files − (Checked + Duplicates + empty paths + Skipped)). Empty-path IDs are listed as far as a stopped or failed run got, and the old
  "not listed because…" notes are removed. Cost if wrong: report wording.
- **R5:** 2.0.0's "Skip marker not recognised by the database" diagnostic is gone. The reader and the checker run the same tests on the same string, so a stray outcome cannot
  occur. Cost if wrong: a diagnostic, if they ever diverge.

## Defects found in the plan

- None of substance. The builder check raised five questions before planning, all settled as recorded in the design's §12.

## Commits

| Commit    | What                                                                                         |
|-----------|----------------------------------------------------------------------------------------------|
| `1432a15` | Design, plan, ledger, `.slnx` registration                                                   |
| `e06178f` | Task 1: two plain statements; classification, key set and empty IDs in the app; live counters; report; Unindexed `nvarchar(4000)`; warning wording |
| `cc9f3df` | Task 1 review: rewraps, `typeCheck`                                                          |
| `667a28e` | Task 2: the Audit group (*Distinct*, live figures, progress by rows, separate rates), mock-up |
| `671988d` | Task 3: SDD and user guide (§8.4 rewritten, 2.0.0 changelog)                                 |
| `a8b0dbe` | Final review: 13 minors (guide statements, SDD history notes, mock-up figures, dead code)     |
| (next)    | This ledger                                                                                  |

## Verification

- **Build:** clean, with 0 errors and 0 warnings, after every task and the fix wave.
- **Task 1's LocalDB harness against the `v2.0.0` runner:**
  - **Milestone 1's seed:** Total files 22, *Distinct* 7 and *Duplicates* 3 with an ID column, 6 and 4 without, empty IDs 10–14. The report bodies were identical.
  - **Key cases:** behaved as designed.
  - **A 200,000-row duplicate-heavy table:** *Distinct* 16,000 and *Duplicates* 144,000, identical bodies, and *Checked* = *Distinct*.
  - **Stopped runs:** they add up exactly.
  - **Time to the first row** on the 1,570,465-row `varchar(8000)` table: 569–621 ms, against 21,670–22,903 ms for 2.0.0's statements.
  - **The Unindexed `varchar` filter** returns a 4,515-character path.
- **Task 2 in the app** (UI Automation, LocalDB 200,000 and 1,200,000 rows):
  - **Completed run:** Found + Missing + Unverified = Checked = Distinct = 762,286, and 762,286 + 372,571 + 48,000 + 17,143 = 1,200,000. The report Summary matched.
  - **Stopped run:** Checked 424,583 plus Not checked 751,044 add up.
  - **Live behaviour:** *Total files* stayed fixed, and *Remaining* counted down steadily.
- **Not exercised:**
  - A run against the user's real server and share. The user's own run after the merge will show the real gain.
  - An ID column with duplicates in the app run (the harness covered it).

## Outstanding items

- **After the merge** (the user's instruction):
  - delete `Docs/Temp/` from local `main`;
  - delete the `diag/v1.0.0-debug-report` and `diag/v2.0.0-debug-report` branches, locally and on GitHub;
  - drop the LocalDB `FileAuditorPerf` database.
- **Recorded optimisations:** the Unindexed audit's ideas from milestone 2's design §6 remain unmeasured.

## Incidents

- **Line endings:** Task 3's implementer stripped the SDD's CRLF endings with a Bash `sed -i`, then restored them. The controller confirmed CR = LF; no content was lost.
