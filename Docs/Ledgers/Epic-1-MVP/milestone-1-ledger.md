# Milestone 1 Ledger — Rewrite

**Date:** 2026-11-06

Ledger for [`Docs/Plans/Epic-1-MVP/milestone-1-rewrite.md`](../../Plans/Epic-1-MVP/milestone-1-rewrite.md), executed on branch `feat/milestone-1-rewrite` (merge base `69e233e`).
The authoritative spec is [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md), approved 2026-11-06 and amended at the end of this
milestone to match the build. Tasks 1 to 8 are complete; each of Tasks 1 to 7 passed its task review.

While the run was in progress the live ledger was kept outside the repository (in the SDD working folder); this document is that ledger cleaned up. The docs ledger was not
written task by task because doing so from the controller would have meant extra commits outside the task reviews.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

| #   | Task       | Ruling                                                                                                                                                            | Why                                                                                                                                                         | Cost if wrong                                                                                                                                                                   |
|-----|------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| R1  | Pre-flight | Designer constructors build view models from `AppSettings.Default` and parameterless concrete services; no interfaces were introduced for the designer.           | CLAUDE.md wants file-scoped null-object implementations, but the services are concrete classes; an interface with one implementation and no tests is YAGNI. | A later need for designer fakes means extracting interfaces then.                                                                                                               |
| R2  | Pre-flight | The live ledger lived outside the repository during the run and was cleaned into this document by Task 8.                                                         | Writing the docs ledger as controller between tasks would have added commits outside task reviews.                                                          | If the run had died before Task 8, this document would have had to be reconstructed from the live ledger.                                                                       |
| R3  | 1          | The NU1902/NU1903 warnings from the transitive `System.Security.Cryptography.Pkcs` 9.0.18 (via `Microsoft.Data.SqlClient` 7.1.1) are accepted and not suppressed. | The fix (pinning a patched Pkcs as a direct reference) adds a package and touches security, so it goes to the user.                                         | The branch carries four known-vulnerability warnings until the user decides.                                                                                                    |
| R4  | 1          | Win9x title-bar chrome comes from Classic.Avalonia's `ClassicWindow`; every window (main, About, dialogues) inherits it.                                          | The package README says to inherit `ClassicWindow` instead of `Window`; it also removes the Win11 rounded corners.                                          | If `ClassicWindow` misbehaved with fixed-size windows, native chrome would have been the fallback (it did not, see I2).                                                         |
| R5  | 2          | `AppSettings` is a positional record with default parameter values, not an init-property record.                                                                  | Source-generated JSON binds through the constructor, so only this keeps defaults for properties missing from the file.                                      | Nothing visible; the public surface is unchanged.                                                                                                                               |
| R6  | 4          | Access keys: Browse is `B`, Start is `A`, Stop is `O`.                                                                                                            | `R` and `M` were taken; SDD §11 leaves the letters to the builder.                                                                                          | Cosmetic.                                                                                                                                                                       |
| R7  | 4          | Start keeps `IsDefault`, so Enter starts an audit.                                                                                                                | Win9x dialogues have a default button, and the pre-flight prompts guard the destructive cases.                                                              | An accidental Enter starts a run the user must Stop.                                                                                                                            |
| R8  | 4          | Failures to launch *View Report* are routed to Task 7, which builds the dialogue service and shows an error dialogue.                                             | Task 4 had no dialogue service to show them with.                                                                                                           | None; Task 7 picked it up.                                                                                                                                                      |
| R9  | 5          | `Path.IsPathFullyQualified` is used instead of "rooted" for full-path validation.                                                                                 | `C:x` and `\x` would resolve against the application's current drive or folder, so they are treated as *Invalid characters in path*.                        | None plausible for index tables of absolute paths.                                                                                                                              |
| R10 | 5          | A path component longer than 255 characters is *Unverified: path too long*, not *Invalid characters* (OS error 123), via a pre-check in the final wave.           | That is what a person means by "path too long".                                                                                                             | Such rows would move between two Unverified groups.                                                                                                                             |
| R11 | 5          | Extended-length `\\?\` paths stay *Invalid characters in path*.                                                                                                   | The plan mandates the `?` rule and index tables hold ordinary paths.                                                                                        | A table that uses `\\?\` prefixes would be entirely Unverified.                                                                                                                 |
| R12 | 6          | Counts are formatted `N0` with `InvariantCulture`; elapsed time is in total hours (at least two digits).                                                          | Reports must diff identically on any machine.                                                                                                               | A non-English locale sees commas as group separators.                                                                                                                           |
| R13 | 6          | CR/LF inside values is flattened to a space and lines are right-trimmed; section headings always print with their counts.                                         | Keeps the aligned columns intact.                                                                                                                           | Cosmetic.                                                                                                                                                                       |
| R14 | 6          | With no ID column, a row in *Path is empty in the index table* renders as a blank line, as SDD §7 literally implies.                                              | A placeholder such as `(empty)` would be a behaviour change that needs the user's say.                                                                      | A confusing blank line in that group (see "For the user").                                                                                                                      |
| R15 | 7          | *OK* and *Cancel* in message dialogues carry no access keys.                                                                                                      | Win9x message boxes used Enter and Esc for them.                                                                                                            | Cosmetic.                                                                                                                                                                       |
| R16 | 7          | Message dialogue windows follow the `*Window` naming exception, like `MainWindow` and `AboutWindow`, for the view composition pair rule.                          | The dialogue is a window, not a `UserControl`.                                                                                                              | A CLAUDE.md wording tweak later.                                                                                                                                                |
| R17 | 7          | The custom `MessageDialogueWindow` is used for all dialogues.                                                                                                     | Classic.Avalonia's `MessageBox` cannot take custom button text.                                                                                             | None.                                                                                                                                                                           |
| R18 | Fix wave 1 | The root re-probe is throttled to one per 5 s per root.                                                                                                           | Cheap under 32-way parallelism.                                                                                                                             | Files checked within about 5 s of a mapped drive dropping can still be reported Missing. A single failed probe marks the root failed for the rest of the run, with no recovery. |
| R19 | Fix wave 1 | Stop after the count returns but before any row is checked still writes a Stopped report.                                                                         | The literal reading of SDD §6.4; the user's decision covered only Stop before the count returns.                                                            | A near-empty report.                                                                                                                                                            |
| R20 | Fix wave 2 | CLAUDE.md drift is left to the user, since CLAUDE.md is the user's instruction file (see *For the user*).                                                         | The file is not mine to edit.                                                                                                                               | The drift persists until the user amends it.                                                                                                                                    |

## User decisions

Taken by the user, not rulings.

- **2026-11-06 — no report on an early stop.** If Stop is pressed, or the close prompt is answered Yes, before the count query returns, no report is written and the status
  bar reads `Stopped`: nothing has been checked, so there is nothing to report. SDD §6.4 was amended to say so (Task 7 fix round 1).

## Plan defects

- **Designer constructors versus no interfaces.** CLAUDE.md asks for view-model designer constructors that delegate to `file`-scoped null-object implementations, while the
  plan makes every service a concrete class with no interfaces. Resolved by ruling R1.
- **Commit `39642e1` does not build on its own.** The rename of the template project landed before the `.slnx` path was updated; the fix arrived in `e62e272`. Anyone
  bisecting across that pair will hit a broken build.
- **Plan line 208 still lists `ReportErrors.FolderMissing`.** That error was removed during Task 7; the plan line is now annotated to say so (`f8265a7`).

## Outstanding items

### Final review wave (this milestone)

- **FIXED in `e6e30f7` — MUST fix — `DriveNotFoundException` mapping.** The exception carries HResult `0x80070003`, so the code switch in `Win32ErrorMapper` maps it to *Missing:
  does not exist* before the type switch is reached. That is the one path toward the anti-goal (reporting "missing" when the truth is "could not check"). Test
  `ex is DriveNotFoundException` first.
- **FIXED in `e6e30f7` — component longer than 255 characters** → *Unverified: path too long* (ruling R10), by a pre-check ahead of the OS call.
- **FIXED in `e6e30f7` — unwritable report folder.** Pre-flight now probes the folder by creating and removing the report's temporary file and stops the run with an error
  dialogue before any row is read (`ReportErrors.FolderNotWritable`).
- **FIXED in `65c41ee` — attribute bracket spacing.** `[ObservableProperty]` and `[RelayCommand]` were unspaced, against the `[ STAThread ]` convention.
- **FIXED in `e6e30f7` — `"Server": null` in `settings.json`** deserialised into a non-nullable `string` because `RespectNullableAnnotations` is off, and crashed start-up.
  `SettingsStore.Load` now replaces null strings and undefined enum values with their defaults.

### For the user

- **NU1902/NU1903 warnings** from the transitive `System.Security.Cryptography.Pkcs` 9.0.18 (ruling R3). **Resolved by the user's decision:** pinned to 10.0.14 as a
  direct reference; the build now has no warnings (SDD §3).
- **`\\?\` paths stay *Invalid characters in path*** (ruling R11).
- **No-ID empty-path row** renders as a blank line (ruling R14). **Accepted by the user** as intended; SDD §7 now says so, for the user guide to call out.
- **CLAUDE.md drift** (ruling R20). **Resolved by the user's decision:** CLAUDE.md now holds only repository context and rules for Claude, and points to the SDD; the
  design and code decisions it used to repeat (Win9x conventions, architecture, code conventions, including the pair-rule exception for `MessageDialogueWindow` and the
  error / warning / question icons) moved into SDD §5, §9 and §12.

### Later / nice to have

Minor findings deferred by the task reviews, none of which affects correctness today.

- **Task 1:** Avalonia usings are not consolidated into `Common/GlobalUsings.cs`; the `MainWindow` `x:DataType` was a placeholder until Task 4. (Missing trailing
  newlines: FIXED in `65c41ee`.)
- **Task 2:** `settings.json.tmp` is left behind if `File.Move` fails; `SettingsStore` has an unrequested extra path constructor; numeric enum values are accepted by the
  string enum converter.
- **Task 3:** double-quoted identifiers are treated literally (`"dbo"."T"` becomes `["dbo"].["T"]`) and fail at runtime rather than at validation; `ParseMultipart`'s
  `maxParts` is not guarded below 1; `Convert.ToInt64(null)` on the `COUNT_BIG` result would silently give 0; the outer
  `catch (SqlException)` in `ReadAsync` could mask an `onRow` exception if reader disposal throws after a cancel (speculative).
- **Task 4:** `MainWindow` event handlers are never unsubscribed and the About handler is `async void` without try/catch; `OnBrowseClick` is likewise `async void`;
  `ProgressView` binds its command row through `$parent[Window]`; `ReportViewModel` resolves the path twice in its constructor; leftover blank lines, a hard-coded
  `#404040` colour; a custom Location counts as complete when non-empty even if relative (the Task 7 pre-flight rejects it); (unsorted `GlobalUsings`: FIXED in `65c41ee`);
  `HideMaximiseButton` is silent if `PART_RestoreButton` is missing and acts on the first template application only; `ProbeReportAsync` discards its task and catches
  only `OperationCanceledException`, the CTS is never disposed, and *View Report* flickers for about 300 ms on path change or activation.
- **Task 5:** the first concurrent wave under a newly blocked directory is not probe-once (performance only); `C:/` and `C:\` roots are cached separately and so probed
  twice.
- **Task 6:** `TrimEnd` strips trailing whitespace that belongs to a path (it should trim padding only); `Flatten` handles only CR/LF, so tabs, NEL and U+2028 skew the
  alignment; the unverified count is derived as `Problems.Count - missing`; the temp file has a fixed `<name>.tmp` name.
- **Task 7:** two dialogues can stack if a run fails while the close prompt is open; the `AuditRunner.RunAsync` `<returns>` doc omitting `Audit.ReadFailed`, the ReadFailed
  dialogue lacking the "could not read the file index table:" prefix and the inline `"Audit.ReadFailed"` literal were FIXED in `e6e30f7`; the local `using` directives in
  `MainWindow.axaml.cs`, `App.axaml.cs`, `AboutWindow.axaml.cs` and `SettingsStore.cs` were FIXED in `65c41ee`.

## Incidents

- **GPG signing timed out twice** on the Task 7 commit until the user unlocked the key.
- **Native title bar appeared** before `ClassicWindow` was adopted (Task 1 fix round 1, `e92c31c`).
- **Test runs opened Notepad++ windows** in the user's session while exercising *View Report*.
- **An empty, locked `Source/FileChecker/` folder** is left on disk, untracked, from the project rename.
- **Alt+F4 could not be driven** by the automation tool, so only the window's X button and File → Exit were exercised. Alt+F4 shares the `Closing` handler with both.
- **The `.idea` files were committed by accident** during the fix wave. The commit was soft-reset straight away and the history was verified clean.
- **Computer use could not be granted to the locally built app**, so verification used Windows UI Automation through PowerShell.
- **No SQL Server with Windows authentication exists on this machine.** Verification used LocalDB, `(localdb)\MSSQLLocalDB`, only; remote-server and domain-account behaviour
  was not exercised.

## Not exercised at runtime

- The vanished-root reclassification, the 255-character segment rule and the `DriveNotFoundException` mapping: each builds and was code-reviewed only.
- Alt+F4.
- Any real SQL Server with Windows authentication (LocalDB only).
- The real `U:` drive.

## Commits

`git log --oneline 69e233e..HEAD`, newest first (the closing tidy-up commit that records this list follows these):

```
f2c0cdd docs(📝): sync the SDD with the final-review fixes and close the ledger
8d06c19 fix(🐛): pre-flight a read-only report and wrap long lines
f8265a7 docs(📝): align mock-ups and plan with the build
65c41ee style(🎨): apply house conventions across the branch
e6e30f7 fix(🐛): close final-review correctness gaps
3887338 docs(📝): sync the SDD and close the milestone 1 ledger
b447ca9 fix(🐛): address Task 7 review findings
d899e54 feat(✨): run audits with pre-flight checks, progress and stop
f2e1c82 docs(📝): tick completed plan tasks 4-6
8688a26 fix(🐛): sort report rows deterministically
2cdfb7f feat(✨): build and write the plain-text audit report
d2221f2 feat(✨): check files and classify missing and unverified outcomes
26ff808 fix(🐛): hide maximise and probe the report file off the UI thread
0f0f372 docs(📝): tick completed plan tasks 1-3
4484df5 feat(✨): add the File Auditor main window and About box
a8b5313 fix(🐛): cancel SQL reads cleanly and consolidate usings
7d13ac0 feat(✨): build the audit query from table and column names
0446a87 feat(✨): add per-user JSON settings store
e92c31c fix(🐛): use ClassicWindow for Win9x title-bar chrome
e62e272 build(📦): retarget to File Auditor on Avalonia 11.3 and Classic.Avalonia
39642e1 refactor(♻️): rename template project to FileAuditor
f8c5637 docs(📝): add the milestone 1 rewrite plan
```

## Final review

The Opus whole-branch review of `69e233e..3887338` found 0 Critical, 4 Important and 13 Minor issues. Fix wave 1 (`e6e30f7`, `65c41ee`, `f8265a7`) addressed them.
The Opus re-review then found the SDD out of sync and the ledger incomplete. Fix wave 2 (`8d06c19`, `f2c0cdd`) closed those.
The Opus re-review of fix wave 2 found no Critical or Important issues; its four Minor tidy-ups (a broken code fence here, two missing blank lines, the `ReadOnly`
error name, and two imprecisions in SDD §6.3's root re-probe text) went into one closing commit.
