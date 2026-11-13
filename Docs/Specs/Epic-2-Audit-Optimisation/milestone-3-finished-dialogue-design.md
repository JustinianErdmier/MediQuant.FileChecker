# Milestone 3 — Finished Dialogue: Design

**Date:** 2026-11-13

A short design: a small change, agreed in conversation on 2026-11-13. The master SDD
([`1-file-auditor-master-design.md`](../1-file-auditor-master-design.md)) stays the contract; this document records what milestone 3 changes and why.

## 1. Intent

Audits can run for a long time, and the user works in another window meanwhile. Today a completed run ends silently: the statistics freeze, the bar fills and the status bar
reads `Completed`, which nobody sees from another application. The app should tell the user when a run has finished, in a way that reaches them while they are elsewhere.

## 2. Goals, non-goals

- **Goals:** when a run completes, show a *finished* dialogue that names the audit, gives the figures that say whether the report needs a look, says where the report is and
  offers to open it; draw attention to it from another application with a flashing taskbar button and the Windows information sound. A run that **fails** gets the same
  attention for its existing error dialogue.
- **Non-goals:** bringing the window to the front or taking the keyboard; any change to what a run does, reports or shows in the Audit group; a dialogue for a run the user
  stopped.
- **Anti-goal:** stealing focus. Keystrokes meant for another application must never land in File Auditor or press one of its buttons.

## 3. When it appears (the user's)

| How the run ends | What happens |
|---|---|
| Completed | The finished dialogue (§4), with the taskbar flash and the information sound. |
| Failed (any error that ends a started run, including a report that could not be written) | The existing error dialogue, unchanged, now with the taskbar flash and the error sound. |
| Stopped by the user, with or without a report | Nothing new: the user pressed Stop, so they are at the window. The status bar reads `Stopped`, as today. |
| The window closed mid-run | Nothing new, as today. |

Pre-flight errors (before a run starts) are unchanged: the user has just pressed Start, so they are at the window.

## 4. The finished dialogue (the user's)

```text
┌ File Auditor ───────────────────────────────── × ┐
│  (i)  The Missing Files audit has finished.      │
│                                                  │
│       Missing: 12    Unverified: 3 (1)           │
│                                                  │
│       The report was saved to:                   │
│       U:\Projects\D12345\DE\Final\               │
│       FileAuditorMissingReport_Interim.txt       │
│                                                  │
│                      [ View Report ]  [  OK  ]   │
└──────────────────────────────────────────────────┘
```

- **Icon:** the Win9x information icon (`Assets/Win95Icons/information32.png`, already in the project). §5's dialogue rule gains *information* beside error, warning and question.
- **First line:** `The Missing Files audit has finished.` or `The Unindexed Files audit has finished.`
- **Figures line:** the run's final figures, formatted exactly as the Audit group shows them: `Missing:` and `Unverified:` on the Missing Files tab (Unverified with its bracket
  when the Audit group shows one), `Unindexed:` and `Unverified:` on the Unindexed Files tab.
- **Report:** `The report was saved to:` then the report's full path through `PathText.Breakable`, as every path in a dialogue (SDD §5).
- **Buttons:** *View Report* (primary; Enter) opens the report exactly as the Audit group's *View Report* does, including its error dialogue if the report cannot be opened;
  *OK* (Esc, or the close box) closes the dialogue. *View Report* carries the access key `V`, like the action buttons of the other message dialogues (*Create Folder*,
  *Yes*); *OK* carries none (SDD §5, §12).
- The final status and statistics are already on screen behind the dialogue, as with the error dialogue today.

## 5. Drawing attention (approach A, chosen with the user)

- **Taskbar flash:** when the dialogue opens and File Auditor is not the active application, its taskbar button flashes until the user switches to it (`FlashWindowEx` on the
  main window, flashing the caption and the taskbar button until the window comes to the foreground). When File Auditor is already active there is nothing to flash.
- **Sound:** `MessageBeep` with the information sound for the finished dialogue and the error sound for a failure, played whether or not File Auditor is active.
- **No focus change:** the window is never brought to the front. Windows' own foreground rules would mostly refuse it anyway, and taking the keyboard is the anti-goal.

### Approaches considered

- **A (chosen):** dialogue, taskbar flash, sound. The standard way a background Windows application asks for attention.
- B: also bring the window to the front. More visible, but it could take the keyboard while the user types elsewhere, and Windows usually refuses it and flashes instead, so it
  behaves like A, less predictably.
- C: the dialogue only. It opens behind the user's other windows, so on a long run it goes unseen — it misses the reason for the change.

## 6. Constraints

- Two Win32 calls (`FlashWindowEx`, `MessageBeep`) through P/Invoke, as the app already does for its crash message box (`MessageBoxW`). No new package. Windows only, like the app.
- Both calls are best effort: if either fails, the dialogue still shows and nothing is reported.
- SDD §5 (dialogue icons; the end of a run), §6.4 and §6.5.4 (what follows a completed or failed run), §10 (this milestone's calls) and the user guide (what to expect when a run
  ends) are amended.

## 7. Left to the builder

Where the two Win32 calls live (beside the existing `MessageBoxW` call or in a small helper of their own); how the dialogue service offers the information dialogue (a new method
beside `ShowErrorAsync`, or the existing `ChooseAsync` with an information icon); how the view model gets the window handle for the flash.
