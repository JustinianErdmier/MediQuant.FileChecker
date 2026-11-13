# Milestone 3 Ledger — Finished Dialogue

**Date:** 2026-11-13

Ledger for [`Docs/Plans/Epic-2-Audit-Optimisation/milestone-3-finished-dialogue.md`](../../Plans/Epic-2-Audit-Optimisation/milestone-3-finished-dialogue.md), executed on
branch `feat/milestone-3-finished-dialogue` (cut from `main` at `8b1a79b`). Spec:
[`milestone-3-finished-dialogue-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-3-finished-dialogue-design.md), a short design approved on 2026-11-13. The master
SDD and the user guide were amended in Task 2 and the controller's fix. Both tasks are complete:
- **Task 1** passed its review first time.
- **Task 2** passed its review once the controller's fix (`6ca0eb4`) settled its Important finding.
- **The final whole-branch review** returned *Ready* with three Minor findings: two fixed, one accepted (Outstanding items).

## User decisions

Settled in the brainstorm (2026-11-13):

- **Why:** audits run long while the user works in another window, so the end of a run has to reach them there.
- **Approach A:** the dialogue, plus a taskbar flash and the Windows sound. Bringing the window to the front (it could take the keyboard) and the dialogue alone (it opens unseen
  behind other windows) were rejected.
- **When:** the finished dialogue appears for a completed run only. A failed run keeps its error dialogue and gains the attention signal. A stopped run, a pre-flight error and
  a close mid-run show nothing new.
- **Content:** the audit's name, the final figures as the Audit group shows them, the report's full path, and *View Report* beside *OK*.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

- **R1:** *View Report* carries the access key `V`, as the other dialogues' action buttons do (*Create Folder*, *Yes*), and *OK* carries none. The design first said no
  access keys; it was corrected before execution. Cost if wrong: one underscore.
- **R2:** any stop request skips the finished dialogue. A Stop during `Reading empty paths...` leaves the result *Completed*, and so does *Stop it and exit?* answered Yes; the
  Task 2 review found the code showed the dialogue in both cases, although the design said a stopped run shows nothing new. The code was fixed to match the design rather than
  the docs changed. Cost if wrong: one condition.
- **R3:** the flash happens only when the main window is not active; the window is never activated. Both Win32 calls (`MessageBeep`, `FlashWindowEx`) are best effort.
- **R4:** *View Report* in the dialogue opens the report of the run that finished, captured at Start, not the selected tab's.
- **R5:** `information32.png` gained the `AvaloniaResource` entry in the `.csproj` that every image needs. It was outside the plan's file list. Cost if wrong: nothing.

## Defects found in the plan

- The design's sketch said the buttons carry no access keys (R1).
- The design's "stopped run shows nothing" did not account for a Stop that leaves the result *Completed* (R2). The design's §3 table was updated.
- The Task 1 implementer reported that a `varchar` path column is refused by pre-flight. That was wrong: the type check is inside the run, so it is a run failure and gets the
  attention signal. The task review caught it, and the docs say the right thing.

## Commits

| Commit    | What                                                                                     |
|-----------|------------------------------------------------------------------------------------------|
| `648737e` | Design, plan, ledger, `.slnx` registration                                               |
| `59b35ae` | Task 1: the finished dialogue, `WindowAttention`, the run's title and figures            |
| `ab57fca` | Task 2: SDD and user guide                                                               |
| `6ca0eb4` | No finished dialogue on any stop request (R2); docs wording; rewraps                     |
| (next)    | Final review minors: SDD §5 close wording, `ConfirmCloseAsync` comment; this ledger        |

## Verification

- **Build:** clean, with 0 errors and 0 warnings, after every task and fix.
- **Task 1, by UI Automation:**
  - both tabs' dialogue text matched the specified copy;
  - *View Report* opened the report;
  - a stopped run showed nothing;
  - a report-write failure part-way through showed the unchanged error.
- **Controller, in the real window with computer use** (a 3,000,003-row LocalDB table, then 300,003 rows):
  - **Run in the background:** with Notepad active, the taskbar button flashed when the run completed. Text and Enter typed into Notepad stayed there while the dialogue was
    open, and switching back stopped the flash. The dialogue showed the information icon and the specified copy, including `Unverified: 0 (1)` (the bracket form, from a `NULL`
    row).
  - **Run in front, Unindexed tab selected:** there was no flash, and *View Report* opened `FileAuditorMissingReport.txt`, the report of the run that finished.
- **Not observed:** the sounds, which a screenshot cannot capture, and a Stop during `Reading empty paths...` after R2. That case rests on code reading, confirmed by the final
  review.

## Outstanding items

- **Accepted (final review, Minor):** the end-of-run dialogues belong to Start's command, so a close (including an OS log-off) is refused while one is open, and the close is
  silent. The run-error dialogue already behaved this way. Revisit if users report File Auditor holding up a shutdown; the fix is to show the dialogue after Start completes.
- **Deferred (Minor):** a run that completes while the exit question is open shows the finished dialogue beside it.
- **Deferred (Minor):** the finished dialogue is chosen by matching the status string `"Completed"`.

## Incidents

None.
