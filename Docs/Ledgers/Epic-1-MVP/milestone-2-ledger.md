# Milestone 2 Ledger — Project Settings

**Date:** 2026-11-06

Ledger for [`Docs/Plans/Epic-1-MVP/milestone-2-project-settings.md`](../../Plans/Epic-1-MVP/milestone-2-project-settings.md), executed on branch
`feat/milestone-2-project-settings` (cut from `main` at `ae6a1ca`). The spec is
[`Docs/Specs/Epic-1-MVP/milestone-2-project-settings-design.md`](../../Specs/Epic-1-MVP/milestone-2-project-settings-design.md), approved 2026-11-06; the master SDD
([`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md)) was amended in Task 5 to match the build.
Tasks 1 to 5 are complete; each of Tasks 1 to 4 passed its task review.

While the run was in progress the live ledger was kept outside the repository (in the SDD working folder); this document is that ledger cleaned up.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

| #  | Task       | Ruling                                                                                                                                                          | Why                                                                                                                                              | Cost if wrong                                                                                                    |
|----|------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------|
| R1 | Pre-flight | Implementers drove the app with Windows UI Automation from PowerShell rather than computer use, as in milestone 1.                                              | Computer use cannot be granted to a locally built app that is not installed.                                                                     | Visual-only defects are caught only where a screenshot was captured (screenshots via `CopyFromScreen` cover that). |
| R2 | Pre-flight | The live ledger lived outside the repository during the run and was cleaned into this document by Task 5.                                                       | Writing the docs ledger between tasks would have added commits outside the task reviews, as in milestone 1.                                      | None.                                                                                                            |
| R3 | 2          | An opened project whose report location is incomplete saves back to its own file without asking.                                                                | The project's file is already known, so the report location is not needed; spec §3.7's "incomplete" case applies only to a never-saved project.  | A user who blanked the Project folder still gets the old file updated.                                           |
| R4 | 3          | Every close, including an operating-system logoff or shutdown close, waits on the save rule's prompts.                                                          | The spec says "every close".                                                                                                                     | A logoff with unsaved changes may be held by a prompt.                                                           |
| R5 | 3          | A title-bar ✕ or Esc on the save question counts as No (remembered for the session), per the plan's Global Constraints.                                       | The plan says closing a dialogue is the declining answer.                                                                                        | A "never mind" at close leaves without saving.                                                                   |
| R6 | 2          | Overwrite stays the default button on the existing-file prompt.                                                                                                 | It matches milestone 1's report-overwrite prompt.                                                                                               | Enter overwrites another project's file.                                                                         |

## User decisions

Taken by the user in the milestone 2 spec (§6), not rulings.

- Settings belong to the project, saved as `FileAuditorSettings.json` beside the report; the per-user `%APPDATA%` file goes away; the app opens empty.
- *Open project...* and *Close project* lead the *File* menu; *Close project* resets the app as if freshly launched.
- The picker starts in the report's Location when it is valid, otherwise in the user's folder.
- The checkbox goes on the last row of the Report group (mock-up option A), ticked by default.
- Start, closing the window and *Close project* all apply the same save logic.
- An opened project whose report folder has changed asks where to save.
- With a built location, each stage keeps its own file beside its report.
- An existing, different settings file prompts **Overwrite**, **Skip Save** or **Cancel**, where *Cancel* backs out of the triggering operation.
- The version stays 1.0.0.

## Plan defects

None found. The pre-flight scan of the plan's task interfaces found no mismatches; the one interim arrangement (Task 2 passes `true` for the checkbox until Task 3 adds it)
was planned and replaced in Task 3.

## Outstanding items

### Final review wave (this milestone)

Fixed in `8515420` (busy gate and load/probe/save-point fixes) and `eed820e` (non-project JSON and settings-save tidy-ups):

- **Fixed (`eed820e`) — a JSON file that is not a settings file loaded as defaults** (Task 4). `SettingsStore.Load` now refuses a JSON document that is not an object or holds
  none of the `AppSettings` property names, so `{"foo":1}` and `{}` are both "the file is not a File Auditor project." (Cost if the ruling is wrong: an empty-but-valid project
  file `{}` is refused.)
- **Fixed (`8515420`) — Start, Open project, Close project and closing the window could overlap.** A private busy flag, true while any of the three commands executes, disables
  the other two, Start and the inputs, and a window close while busy (no audit running) is refused. This closes the race where a stalled Start resumed after Close project.
- **Fixed (`8515420`) Task 4 minor:** the picker start-folder probe is abandoned after two seconds; `Load` runs off the UI thread through `ProjectSettingsService.LoadAsync`
  (the `Store` property is now private, which also closes the Task 2 minor about its visibility); a picked file with no local path shows the *Open failed* error.
- **Fixed (`8515420`) Task 2 minor:** `SameFolder` compares `Path.GetFullPath` of both sides. Every save point also refuses an invalid built Project folder
  (`ReportPathResolver.IsSingleFolderName`, shared with the pre-flight).
- **Fixed (`eed820e`) Task 2 minor:** the cannot-save reason is the bare exception message (carried in the error's metadata, `SettingsErrors.BareDetail`); a failed write
  deletes its `.tmp` file; the `AppSettings.Default` doc is corrected.
- **Fixed (`eed820e`) Task 1 minor:** the Tertiary button's `IsCancel` is bound to `HasTertiary`.
- **Fixed (`eed820e`; the `_isRevertingTick` comment in `8515420`) Task 3 minor:** the stale `OnReportChanged` comment and the `_isRevertingTick` comment.
- **Still open, Task 1 minor:** with no owner, `ChooseAsync` returns Secondary for a three-button dialogue (designer only); `SecondaryText` null with `TertiaryText` set gives
  two cancel buttons (unreachable through the service); `TryGetFolderFromPathAsync` probes on the UI thread (the start folder is pre-checked off-thread, with the new timeout).
- **Still open, Task 3 minor:** `ConfirmUntickAsync` is fire-and-forget (matches the existing pattern).
- **Still open, Task 4 note:** the *Open failed* detail for invalid JSON echoes the file's own content from the `System.Text.Json` message (for example `'not json' is an
  invalid JSON literal`); it comes from the `SettingsStore` Unreadable description (Task 2).
- **Still open, fix-wave minor:** closing the window runs its save point without setting the busy flag, so during a slow `Save` on a network drive the
  commands stay enabled briefly; a refused close (while busy) gives no feedback, and `LoadAsync`'s read is not time-bounded.

### Later / nice to have

- Recent-projects list, command-line opening and file associations (spec non-goals).
- Showing the open project in the window title (spec §4 keeps the title as *File Auditor*).
- Migrating the old `%APPDATA%\MediQuant\FileAuditor\settings.json`, which is left on disk untouched and is no longer read.
- Milestone 1's deferred minors that remain open are listed in the [milestone 1 ledger](milestone-1-ledger.md).

## Incidents

- **A stray `python` heredoc in a Bash call hung** during Task 1 and was stopped; the files had already been written and were correct.
- **UI Automation timing:** the first Task 1 run failed with "Value is read-only" on the Location box right after toggling the custom-location checkbox; it passed with a
  one-second pause. A test-harness matter only.
- **Computer use could not be granted to the locally built app**, so verification used Windows UI Automation through PowerShell (ruling R1).

## Not exercised at runtime

- **Open project... and Close project disabled while an audit runs**, and any audit at all: no SQL Server is reachable in this environment (Task 4).
- **A real disconnected or unmapped drive** as the settings target: a nonexistent folder was used for the cannot-save path (Task 3).
- **The title-bar ✕ on the three-button dialogues** (folder-changed, overwrite, cannot-save): only the *Create Folder* / *Cancel* dialogue's ✕ was driven (Task 1). The ✕ is
  designed to give the last visible button's choice.
- **The *Original File* button at Start:** it was driven from *Close project* (Task 4) but not from the Start save point (Task 2).
- **Overwrite and Skip Save buttons from Open or Close project** were not clicked there (Tasks 2 and 3 exercised them from Start and from closing the window).
- The three-button dialogue and the picker had no caller when Task 1 was driven; both were exercised later by Tasks 2 and 4.

## Commits

`git log --oneline main..HEAD`, newest first (the commit that closes this ledger follows these):

```
25da644 docs(📝): sync the SDD with the final-review fixes
eed820e fix(🐛): refuse non-project JSON and tidy settings saves
8515420 fix(🐛): stop save points and project commands overlapping
54acf57 docs(📝): sync the SDD with project settings and close the milestone 2 ledger
ed1a230
ed1a230 feat(✨): open and close projects from the File menu
772b7ac feat(✨): add the save-project-settings checkbox and save on close
f4a5f8f feat(✨): save settings per project beside the report
93843cc feat(✨): add three-button dialogues and a project file picker
9d522fc docs(📝): add the milestone 2 project settings design and plan
```
25da644 docs(📝): sync the SDD with the final-review fixes
eed820e fix(🐛): refuse non-project JSON and tidy settings saves
8515420 fix(🐛): stop save points and project commands overlapping
54acf57 docs(📝): sync the SDD with project settings and close the milestone 2 ledger
ed1a230

## Final review

The Opus whole-branch review of `ae6a1ca..54acf57` found 0 Critical, 2 Important and 14 Minor issues. The two Important ones were the overlap of Start, Open project,
Close project and closing the window, and non-project JSON loading as defaults. One fix wave (`8515420`, `eed820e`, `25da644`) addressed both and the queued minors, and its
Opus re-review found no Critical or Important issues; the minors it left are listed under *Still open* above. Rulings R5 and R6 were taken in that wave.
