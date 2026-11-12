# SDD ledger — plan: Docs/Plans/Epic-2-Audit-Optimisation/milestone-1-row-accounting.md

**Date:** 2026-11-12

Branch `feat/milestone-1-row-accounting`, cut from `main` at `b22d63c`.

## Groundwork

- `b22d63c` — `fix(🐛): check each distinct path in the index table once`. Made both directions' queries `DISTINCT`; amended the SDD and user guide.

## Incidents

- `b22d63c` was committed straight to `main`, outside any epic or plan, before this milestone existed. The user accepted it as is and asked for this epic to build on it; it is
  recorded here as the milestone's groundwork rather than rewritten.

## Pre-flight scan

| Tasks / task | Shared file or interface                                                                | Finding                                                                                                                         |
|--------------|-----------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------|
| 1 ↔ 2        | `AuditQuery`, `IndexTableReader.CountRowsAsync` (1 produces, 2 consumes in `AuditRunner`) | Task 1 removes `CountAsync` and `AuditQuery.CountSql`, which `AuditRunner` calls today, so Task 1 alone would not build. Ruling 1. |
| 1 ↔ 2        | `RowCounts`                                                                             | Consistent: 1 creates, 2 carries through progress and report.                                                                   |
| 2 ↔ 3        | `AuditProgress` (`TotalFiles`, `Skipped` kept, `Counts`, `Found` added)                   | Consistent: Task 2 keeps the two old properties so `AuditViewModel` builds until Task 3 reworks it.                             |
| 2 ↔ 3        | `AuditReport`                                                                           | Only `AuditRunner` constructs it and only `ReportBuilder` reads it; `MissingAuditRun` passes it through. No conflict.            |
| 2 ↔ 4, 3 ↔ 4 | SDD, user guide                                                                         | Task 4 documents what 2 and 3 build; no code overlap.                                                                           |
| 1            | self                                                                                    | Verify step expects T 22, E 5, S 7, D 7/6, Dup 3/4: recomputed from the seed list, consistent.                                  |
| 2            | self                                                                                    | Case (d) "empty table" gives D = 0, T = 0: no parentheses (`0`); "only empty and skip rows" gives `0 (n)`. Consistent with rules. |
| 3            | self                                                                                    | Consistent.                                                                                                                     |
| 4            | self                                                                                    | Consistent.                                                                                                                     |

## Rulings

- Ruling 1: Task 1 also updates `AuditRunner`'s count call minimally (`CountRowsAsync`, then `SetTotal(counts.Distinct)`) so its build is clean; Task 2 replaces that with
  `SetCounts` — every task must end on a clean build (CLAUDE.md) — costs nothing if wrong: Task 2 rewrites the same lines.
- Ruling 2: the project ledger (this file) is also the subagent-driven ledger; briefs, reports and review packages live in the git-ignored `.superpowers/sdd/` workspace —
  CLAUDE.md asks for one ledger per plan under `Docs/Ledgers/`, and two would drift — costs nothing if wrong.

## Progress

- Setup: commit f94a21d (design, mock-up, plan, ledger, slnx). Task 1 dispatched (Sonnet), BASE f94a21d.
- Task 1: implementer DONE, commit ed1cef0; task review dispatched (Opus).
- Task 1: complete (commits f94a21d..ed1cef0, review clean)
- Task 1: minor (deferred): unqualified user columns after the CROSS APPLY chain become ambiguous if a column is named V/Lead/Trail/Length/IsEmpty/Trimmed/IsSkip — alias the table and qualify.
- Task 1: minor (deferred): ReadEmptyIdsAsync has no null guard for EmptyIdsSql.
- Task 1: minor (deferred): CountRowsAsync ignores the ReadAsync result.
- Task 1: minor (deferred): `string? idSql` breaks the aligned-declaration style in QueryBuilder.Build.
- Task 2 dispatched (Sonnet), BASE ed1cef0.
- Task 2: implementer DONE_WITH_CONCERNS, commit 96a3afe. Verified by a scratchpad console harness through the real AuditRunner/ReportBuilder, not the GUI (no desktop
  control in the subagent); GUI verification moves to Task 3. Safety net (a stray Skipped outcome is recorded under Unverified) noted here on the implementer's behalf.
  ReportBuilder.cs left with LF line endings in the working tree (the index is LF throughout; harmless). Task review dispatched (Opus).
- Task 2: review — Needs fixes: Important 1 (a non-SQL exception from the empty-ID read escapes RunAsync and loses a completed audit's report). The owed safety-net
  ledger note is the entry above.
- Task 2: minor (deferred): the OCE catch around the empty-ID read has no `when (cancellationToken.IsCancellationRequested)` filter.
- Task 2: minor (deferred): a stray Skipped outcome (safety net, unreachable) counts in the Summary's Unverified but is listed nowhere, so Summary and heading would differ.
- Task 2: minor (deferred): a stray PathEmpty problem (unreachable) would print a second group with the same title.
- Task 2: minor (deferred): AddEmptyPathGroup writes a blank line if both EmptyPathIds and EmptyPathNote are null (contract breach only).
- Task 2: fix round 1 dispatched (resumed implementer), FIX_BASE 96a3afe.
- Task 2: fix round 1/5 (1 addressed, 0 open — non-SQL exception in the empty-ID read; commits 96a3afe..df7962c)
- Task 2: complete (commits ed1cef0..df7962c, review clean)
- Task 3 dispatched (Sonnet), BASE df7962c. GUI driven by PowerShell UI Automation (milestone 5's method), since subagents have no desktop-control tools.
- Task 3: implementer DONE, commit 589a235. Window 640 × 651 → 640 × 668 (measured with SizeToContent on the Missing tab). Audit grid columns 74,128,68,118,82,*.
  Verified by UI Automation: cases (a), (b), (d), the empty table and dbo.Big. Not re-driven: Stop mid-check in the GUI (covered by Task 2's harness). Scratch database dropped.
- Ruling 3 (implementer's, accepted): on screen, Checked is the sum Found + Missing + U, each counter read once per refresh — reading the Checked counter separately gave a
  torn snapshot mid-run (Checked below Missing) — costs nothing if wrong: at the end of a run the two are equal.
- Task 3: task review dispatched (Opus).
- Task 3: review — Needs fixes: Important 1 (Reset does not blank Found and Duplicates).
- Task 3: minor (deferred): stray blank line between the Missing doc comment and its attribute in AuditViewModel.cs.
- Task 3: minor (deferred): Duplicates/Found inserted out of the file's alphabetical member order.
- Task 3: minor (deferred): HasTotal read separately from Counts in Update — a one-refresh blip (0%) is possible; derive hasTotal from `counts is not null`.
- Task 3: fix round 1 dispatched (resumed implementer), FIX_BASE 589a235.
- Incident: during Task 3 fix round 1 the implementer committed a non-building edit (d7e481e, a literal `\r\n` in AuditViewModel.cs) and amended it into 479a110; the branch
  was unpushed, so no shared history was rewritten. Only 479a110 remains.
- Task 3: fix round 1/5 (1 addressed, 0 open — Reset leaves Found/Duplicates; commits 589a235..479a110)
- Task 3: complete (commits df7962c..479a110, review clean)
- Task 4 dispatched (Sonnet), BASE 479a110.
- Task 4: implementer DONE_WITH_CONCERNS (minor), commit 6700aa7; task review dispatched (Opus).
- Task 4: review — Needs fixes: Important 1 (guide calls the empty-path read "usually quick", unmeasured; it is a full-table pass), Important 2 (guide §8.2 "Skip
  markers are tested first" made false by this milestone).
- Task 4: minor (deferred): FAQ answers at UserGuide.md ~780/785 break early; rewrap.
- Task 4: minor (deferred): "always listed" / "counted and listed" for the empty group (guide ~212, SDD ~509) — the IDs are listed only with an ID column after a completed check.
- Task 4: minor (deferred): SDD §6.3 opens "Each file lands in exactly one outcome" with Skipped first, beside the new sentence that skip rows never reach the checks.
- Task 4: minor (deferred): guide ~276 "bracketed counts" could be read as including the Files/second bracket; ~367 assumes T ≠ D.
- Observation for the final review: the empty-ID statement reuses the full CROSS APPLY source, so it scans the whole table once more after checking.
- Task 4: fix round 1 dispatched (resumed implementer), FIX_BASE 6700aa7.
- Task 4: fix round 1/5 (2 addressed, 0 open — "usually quick", stale skip-marker bullet; commits 6700aa7..a06e2e4)
- Task 4: complete (commits 479a110..a06e2e4, review clean)
- Task 4: minor (deferred): guide line ~694 "never sent to the app" is slightly broad for empty paths (their IDs are read for the report).
- Final whole-branch review dispatched (Opus), range b22d63c..HEAD.
