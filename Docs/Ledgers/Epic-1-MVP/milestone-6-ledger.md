# Milestone 6 Ledger — Code Review Clean-up

**Date:** 2026-11-08

Ledger for [`Docs/Plans/Epic-1-MVP/milestone-6-code-review-clean-up.md`](../../Plans/Epic-1-MVP/milestone-6-code-review-clean-up.md), executed on branch
`feat/milestone-6-code-review-clean-up`. There is no milestone spec: the user's 44-item code review of 2026-11-08, summarised per task in the plan, with the rulings taken in chat,
is the spec. The master SDD ([`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md)) was amended in Task 8 to describe what was built.
Tasks 1 to 8 are complete. Tasks 1, 2, 3 and 5 passed their reviews with no Critical or Important findings; Tasks 4, 6 and 7 each had one Important finding
and were fixed in one round. Implementers ran on Sonnet and reviewers on Opus.

While the run was in progress the live ledger was kept outside the repository (in the SDD working folder); this document is that ledger cleaned up.

## What was built

The milestone changes no behaviour, with three exceptions the user approved: project files saved between commit `2cdf814` and this milestone keep their stages again, a `null` bool
in a project file now falls back to its default instead of making the file unreadable, and a global exception handler is new.

1. **Settings (Task 1).** Project files are read through a new `ProjectFile` model (and `ProjectFileFolder`) whose nullable members mirror the JSON, and mapped by the now-static
   `SettingsStore` into an `AppSettings` with no nullable members. The keys are those from before `2cdf814` (`ProjectStage` and `AuditStage` restored). Every `null` falls back
   field by field. `ProjectSettingsService` no longer takes a store.
2. **Static services and analyser clean-up (Task 2).** `IndexTableReader` and `FolderWalker` became static classes; unused members were removed; ReSharper suppressions follow the
   user's form; small inspections fixed.
3. **One switch convention (Task 3).** Every enum member a switch handles has its own arm and every `default` throws `UnreachableException`. No enum `ToString()` stands in for
   user-facing text.
4. **Global exception handler and records (Task 4).** `Startup/UnexpectedErrorHandler` (SDD §9 *Unexpected exceptions*); five plain records moved into `Models/`; the three `async
   void` handlers report through it.
5. **XAML (Task 5).** Every grid row and column is set explicitly; every `$parent[Window]` binding has a fallback value.
6. **Naming (Task 6).** Single enum values have singular names; abbreviated, single-letter and vague names were made descriptive.
7. **Doc comments (Task 7).** A doc comment on every type and member at every access level; every pointer to a design document dropped from the code; every line at most 180
   characters.
8. **SDD and ledger (Task 8).** §8, §9 and §12 amended; this ledger written.

## Commits

`git log --oneline c5a05cf..HEAD`, newest first (`c5a05cf` and `c76b4ea` are the plan):

```
(Task 8)  docs(📝): sync the SDD with the code review clean-up and close the milestone 6 ledger
55adafa docs(📝): correct the AbandonOpen doc and tidy the task 7 wording
c7e6fdd docs(📝): document every type and member and drop the pointers to the design documents
e9493e7 refactor(♻️): wrap the over-long lines and finish the task 6 renames
472e254 refactor(♻️): give single enum values singular names and make the remaining names descriptive
1cc3a54 style(🎨): set every grid row explicitly and give the parent bindings fallback values
eff6f62 fix(🐛): hold the one-notice rule on the fallback box and keep the fatal box in front
37d07c1 feat(✨): add a global exception handler, move the plain records to Models and guard the async click handlers
7ad3a01 refactor(♻️): name every enum member in switches and make the default arms unreachable
a7839f2 fix(🐛): keep the newest report probe winning and tidy the task 2 alignment
9b493a9 refactor(♻️): make the stateless services static and clear the analyser warnings
24f3aef fix(🐛): read project files through a nullable file model and restore the ProjectStage and AuditStage keys
c76b4ea (plan)
c5a05cf (plan)
```

## Rulings

Decisions taken without the user, each with what it costs if wrong.

- **R1 (pre-flight):** `MessageBoxW` is imported with `[ DllImport ]`; it built with 0 warnings and a clean Rider inspection, so no `LibraryImport` and no `AllowUnsafeBlocks`. Cost
  if wrong: a csproj line.
- **R2 (Task 1):** `SettingsStore.Serialise` stays public, because `ProjectSettingsService.IsChanged` compares through it. Cost if wrong: one extra public member.
- **R3 (Task 1):** files saved between `2cdf814` and `24f3aef`, which have the keys `ProjectStages` and `AuditStages`, load with the default stages. The app is unpublished. Cost if
  wrong: re-pick the stages once in an affected file.
- **R4 (Task 3):** an enum's `ToString()` stays allowed for on-disk path components: the `AuditDirections` member names in report file names and the stage names in the report's
  folder and file names (`ReportPathResolver`). Recorded as the exception in SDD §12. Cost if wrong: a later label switch for those names.
- **R5 (Task 3):** `MainWindowViewModel.SelectedDirection` switches on an `int` tab index, not an enum, and its default also covers index 0 and a transient -1, so it does not
  throw. Cost if wrong: none.
- **R6 (Task 4):** an `AggregateException` (the unobserved-task route) shows its first inner exception's message instead of the runtime's boilerplate. Cost if wrong: the notice
  text.
- **R7 (Task 5):** the extra `Grid.Column="0"` setters that Rider demanded after the `Grid.Row="0"` pass are kept. Cost if wrong: nil (0 is the default).
- **R8 (Task 6):** the tiny lambda parameters (`c`, `_`), enum members, JSON-keyed `ProjectFile` members, command method names, `ReportViewModel.Current` and `Shared` keep their
  names. Cost if wrong: another rename pass.
- **R9 (Task 7):** `LabelWidth` in the report formatter is documented as the column at which every value starts (the longest label, `Project folder:`, plus a two-space gap); the
  code is unchanged. Cost if wrong: none.
- **R10 (Task 8):** the SDD keeps `FileTypeList`, `FileTypePattern` and `IndexPathSet` in `Services/Scanning/` and `FileTypeEntryItem` in `ViewModels/Dialogues/`, saying why,
  rather than moving them to `Models/`. Cost if wrong: a later move.
- **R11 (commit trailers):** the task commits carry the `Claude Opus 5.5` trailer from the common rules, where the session's attribution reminder names `Claude Sonnet 5.5`; this
  ledger's commit carries the reminder's. Signed, not rewritten. Cost if wrong: cosmetic.

## Plan defects

- **Task 2 Step 7 names `FindFolderProblems`;** the method is `FindFolderProblem` (singular), taking an `IReadOnlyList<ScanFolder>`. No code impact.
- **Item 36 names `AuditView.axaml` twice;** the second was meant to be `FileIndexTableView.axaml`. The plan's ruling had Task 5 check both, which it did.
- **The stage report lines could not be exercised.** The plan's verification expected the `Project stage:` and `Audit stage:` report lines to be read, but they appear only in
  reports written to the built location, and that path is hard-coded as `U:\Projects\<Project Folder>\DE\...`. There is no `U:` drive, and the file-safety rules forbid writing to
  one.

## Defects found in review

- **Task 2:** three minors (the probe's newest-wins order, suppression placement, alignment); fixed in `a7839f2`.
- **Task 3:** two minors parked, a stale comment in `UnindexedReportBuilder` (fixed in Task 7) and the stage `ToString()` in `ReportPathResolver` (R4).
- **Task 4:** one Important finding, that the no-main-window fallback box did not hold the one-notice flag, and four minors (the fatal box could sit behind other windows, a
  cross-thread owner handle could hang, `Install` was not idempotent, and the fallback could loop on its own fault); fixed in `eff6f62`.
- **Task 6:** one Important finding, two lines pushed past 180 characters, and six minors (alignment, a leftover `i`, a `project` / `projectSettingsService` inconsistency,
  `isOpenedFile`, `rawEntry`, a dropped BOM); fixed in `e9493e7`.
- **Task 7:** one Important finding, a false `AbandonOpen` summary, and minors (ragged reflows, four imprecise clauses, a private constructor summary); fixed in `55adafa`.

## Outstanding items

For the user, before the pull request:

- **A manual look at the fatal-box flags.** The ownerless box's topmost and foreground flags, and the owner-only-on-the-UI-thread rule, were added in `eff6f62` and checked from the
  code only; the box itself was seen at runtime before that round.
- **A Stop-button check per tab.** Task 6 believed Stop was one shared button; each tab has its own. The per-tab enabling from milestone 5 was not re-checked after the XAML and
  naming passes (Task 5's screenshots were pixel-identical idle, and the Missing tab's Stop was enabled while running).
- **The `Project stage:` and `Audit stage:` report lines were checked by reading the code only** (see the plan defects). The stage loading was checked (a `Sample` file shows
  Sample; a `Final` / `Final` file shows Final / Final). Worth one look in a report written to the real location.
- **The OneDrive scan stall from milestone 5 is still open.** An Unindexed scan of OneDrive stalled at 3,717 files within 11 seconds. Not part of this milestone; not investigated.

Revisit when something changes:

- `UnreachableException` defaults mean a future enum member is a runtime throw in every switch until each switch gets an arm. That is the convention's point, but a new member needs
  a pass over `ReportBuilder`, `UnindexedReportBuilder`, `ReportFormat`, `AuditProgress`, `AuditRun`, `FolderWalker`, `FileChecker` and `ProjectSettingsService`.
- Files written between `2cdf814` and `24f3aef` (R3) load default stages.
- The doc-comment rule covers every member, so a new private field needs one; there is no analyser enforcing it (a `GenerateDocumentationFile` build was clean at the end of Task
  7).

## Incidents

- **Task 1: dialogue driving.** The Open dialogue's file-name box and Open button expose no UI Automation patterns, so the implementer set the name with `WM_SETTEXT` and pressed
  Open with `BM_CLICK`, which deviated from the "patterns only" rule. One lookup of Edit controls ran over the main window's subtree and saw only the dialogue's column headers, no
  file names. The rule was tightened: find the controls only by walking down from the dialogue's own HWND with `FindWindowEx`, never search the main window's subtree while a
  dialogue is open.
- **Task 2: a stray Enter.** A `{ENTER}` sent while the app was foreground and focused on *Add...* opened the folder picker once. The implementer pressed its `Cancel` at once
  through its own button, and no folder view was read. The rules were tightened again: no `SendKeys` at all, and never open a folder picker (only check that it opens and cancels).
- **Task 4: a browser window behind the fatal box.** Two early screenshots of the fatal message box also captured an unrelated browser window on the user's screen, because the box
  sat behind it. They were deleted after a glance and later captures used `PrintWindow` on the box. This is the defect the fix round addressed (an ownerless box is now topmost and
  foreground).
- **Task 4: the GPG timeout.** Committing the staged Task 4 diff timed out on signing, for the implementer and then the controller. The run stopped and the user unlocked GPG; the
  controller then committed (`37d07c1`) and the review ran on the identical diff. Signing was never bypassed.
- **Smaller:** a Task 1 foreground-steal attempt left another application in front; the check failed before any key was sent. A regex edit in Task 6 left lone LF line endings in
  two files, fixed to CRLF before the commit, and a BOM was dropped from `MainWindowViewModel.cs` in the same repair (the other files have none).

## Not exercised at runtime

- The `Project stage:` and `Audit stage:` report lines (above).
- The fatal box's `MB_TOPMOST` and `MB_SETFOREGROUND` flags and the second-fatal-error wait (`eff6f62`).
- No sample database ships with the repository; every run used LocalDB (`(localdb)\MSSQLLocalDB`) with a scratch database, dropped afterwards. Task 8 ran no application at all.

Exercised at runtime during the milestone: both audits on LocalDB, an Unindexed audit with a directory junction and a file symbolic link (the junction and the link were listed
under *Link not followed* and nothing beneath the junction was listed; this closes the manual check Task 3 asked for); the recoverable notice from the Stop command and from a
refresh-tick fault, shown once and not stacked; the fatal box from a plain thread and from start-up; a Missing report byte-identical to the previous build's apart from timestamps
after Tasks 3 and 6; both tabs pixel-identical after Task 5.

## Measurements

- Build after every task: `dotnet clean; dotnet build`, 0 errors and 0 warnings.
- Rider `get_file_problems` (with `errorsOnly: false`) was clean on the files each task touched where it was run: `UnexpectedErrorHandler`, `FolderWalker`, `MainWindowViewModel`,
  `AuditRunner`, `FileChecker`, `FileTypePattern`, and the eight `.axaml` files of Task 5.
- `MainWindow.axaml` is 95 lines (the limit is 100).
- No line in the `.cs` or `.axaml` files exceeds 180 characters.
