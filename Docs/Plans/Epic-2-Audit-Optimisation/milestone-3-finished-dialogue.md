# Milestone 3 — Finished Dialogue Implementation Plan

**Date:** 2026-11-13

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A completed audit shows a *finished* dialogue (information icon, the key figures, the report path, *View Report* and *OK*); it and the existing run-failure error
dialogue flash the taskbar button when File Auditor is not active and play the matching Windows sound.

**Architecture:** A small static `WindowAttention` class wraps `FlashWindowEx` and `MessageBeep`. `DialogueService` gains two methods that request attention for their owner
window and then show the dialogue: one for the finished dialogue, one for a run failure. `MainWindowViewModel.RunAuditAsync` calls them where a run ends; each `AuditRun` supplies
its figures line.

**Tech Stack:** unchanged (.NET 10, Avalonia 11.3 + Classic.Avalonia, CommunityToolkit.Mvvm, ErrorOr). P/Invoke to `user32.dll`, as `UnexpectedErrorHandler` already does. No new
packages.

**Spec:** [`Docs/Specs/Epic-2-Audit-Optimisation/milestone-3-finished-dialogue-design.md`](../../Specs/Epic-2-Audit-Optimisation/milestone-3-finished-dialogue-design.md), with
the master SDD [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) §5, §6.4, §6.5.4, §9 and §12.

## Global Constraints

- **No automated tests and no test projects.** Verify with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings) and by
  running the app.
- **Exact copy:**
  - First line: `The Missing Files audit has finished.` / `The Unindexed Files audit has finished.`
  - Figures line: `Missing: <m>    Unverified: <u>` / `Unindexed: <n>    Unverified: <u>` — four spaces between the two; each value is the Audit group's display string at the
    end of the run (so Unverified keeps its bracket when the group shows one).
  - Then `The report was saved to:` and `PathText.Breakable(<report full path>)`.
  - Lines separated as in the design's sketch: a blank line after the first line and after the figures line.
  - Buttons: `_View Report` (primary, Enter) and `OK` (secondary, Esc and the close box).
- **When:** the finished dialogue only for status `Completed`; the attention on the error dialogue only for a run's error (the `error` returned by `AuditRun.ExecuteAsync`).
  `Stopped` (any kind) and pre-flight errors are unchanged: no dialogue, no attention.
- **Attention:** information sound (`MB_ICONASTERISK`) with the finished dialogue, error sound (`MB_ICONHAND`) with the failure; the taskbar flash only when the main window is not
  active, flashing caption and taskbar button until the window comes to the foreground (`FLASHW_ALL | FLASHW_TIMERNOFG`). Never activate or bring the window forward. Both calls
  best effort: a failure is ignored.
- *View Report* in the dialogue opens the report the run wrote, through the same code as the Audit group's *View Report* (including its error dialogue).
- All CLAUDE.md and SDD §12 conventions apply. Commit each task automatically, staging only its files and ticking its boxes here.

## Review Focus

1. **Stop gives nothing new.** A run stopped by the user (before or after checking began) shows no dialogue and makes no sound — Task 1 step 4.
2. **Background run.** With another application active when the run completes, the taskbar button flashes and File Auditor does not take the keyboard — Task 1 step 4.
3. **Foreground run.** With File Auditor active, no flash; the sound still plays and the dialogue opens — Task 1 step 4.
4. **The right report.** *View Report* in the dialogue opens the report of the run that finished, even if the other tab is selected when the run ends — Task 1 step 4.
5. **Failure.** A run that fails (a path column that is not `nvarchar` is the easy case) shows the unchanged error text with the error sound — Task 1 step 4.

---

Before Task 1 the controller creates branch `feat/milestone-3-finished-dialogue` from `main`, adds the design doc, this plan and the ledger
(`Docs/Ledgers/Epic-2-Audit-Optimisation/milestone-3-ledger.md`) to `MediQuant.FileAuditor.slnx` after milestone 2's entries, and commits them as
`docs(📝): add the finished dialogue design and plan`.

### Task 1: The finished dialogue and the attention signal

**Files:**

- Create: `Source/FileAuditor/Services/Dialogues/WindowAttention.cs`
- Modify: `Common/Constants/Dialogues/DialogueIcons.cs`, `ViewModels/Dialogues/MessageDialogueViewModel.cs`, `Views/Dialogues/MessageDialogueWindow.axaml`,
  `Services/Dialogues/DialogueService.cs`, `ViewModels/Main/AuditRun.cs`, `ViewModels/Main/MissingAuditRun.cs`, `ViewModels/Main/UnindexedAuditRun.cs`,
  `ViewModels/Main/MainWindowViewModel.cs`

**Interfaces:**

- Produces: `DialogueIcons.Information`; `MessageDialogueViewModel.IsInformation`; the dialogue window shows `Assets/Win95Icons/information32.png` for it.
- Produces: `public static class WindowAttention { public static void Request(Window window, DialogueIcons icon); }` — plays the icon's sound (Information → `MB_ICONASTERISK`,
  Error → `MB_ICONHAND`; others as Win32 maps them) and, when `window.IsActive` is false, flashes it with `FlashWindowEx`. Swallows any failure.
- Produces: `DialogueService.ShowFinishedAsync(string message) : Task<bool>` (`true` for *View Report*) and `DialogueService.ShowRunFailedAsync(string message) : Task`; both call
  `WindowAttention.Request` on the owner just before showing the dialogue; with no owner they do nothing, like the other methods.
- Produces: `AuditRun.FinishedTitle : string` (the first line) and `AuditRun.FinishedFigures : string` (the figures line), implemented by both runs from their Audit group.

- [x] **Step 1: Icon and attention.** Add the enum member, the view-model flag and the image; add `WindowAttention` with its two `user32` imports, following the pattern of
  `UnexpectedErrorHandler`'s `MessageBoxW` import.
- [x] **Step 2: Dialogue service.** The two methods, building the dialogue through the existing `ShowAsync`.
- [x] **Step 3: Where a run ends.** `RunAuditAsync` takes the run's `ReportPath`; after the existing resets it shows the finished dialogue for `Completed` (opening the report on
  *View Report* through the code the Audit group's command uses, factored into one method taking a path) and `ShowRunFailedAsync` in place of `ShowErrorAsync` for a run error.
- [x] **Step 4: Verify.** Clean build. Then in the app, against a LocalDB scratch database `FileAuditorE2M3Scratch` and a folder in the scratchpad (drop both at the end), read
  each dialogue's text by UI Automation and check it against Global Constraints: a completed Missing Files run, a completed Unindexed Files run, a stopped run (no dialogue), and a
  failure (a path column of type `varchar(400)`). Choose *View Report* once and confirm the report opens (then close the program it opened). Say in the report which of Review
  Focus 2–4 you could not exercise without desktop control; the controller drives those with computer use. Close the app.
- [x] **Step 5: Commit** — `feat(✨): show a finished dialogue and signal the end of a run`.

### Task 2: SDD and user guide

**Files:** Modify `Docs/Specs/1-file-auditor-master-design.md`, `Docs/UserGuide.md`

- [ ] **Step 1: SDD.** §5's Win9x conventions (dialogue icons gain *information*; the finished dialogue and the attention signal), §6.4 and §6.5.4 (what follows a completed,
  stopped and failed run), §9 (`WindowAttention` in `Services/Dialogues/`), §10 (this milestone's calls, from the design's §3–§6 and the ledger). Remove anything this makes
  false.
- [ ] **Step 2: User guide.** Where a run's end is described for each tab: the finished dialogue with its figures and *View Report*, the flashing taskbar button and the sound when
  you are in another program, and that a run you stopped shows nothing new. Hard-wrap at 180 characters.
- [ ] **Step 3: Commit** — `docs(📝): document the finished dialogue`.
