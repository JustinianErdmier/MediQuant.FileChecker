# Milestone 4 Ledger — Unindexed Files Audit

**Date:** 2026-11-08

Ledger for [`Docs/Plans/Epic-1-MVP/milestone-4-unindexed-audit.md`](../../Plans/Epic-1-MVP/milestone-4-unindexed-audit.md), executed on branch
`feat/milestone-4-unindexed-audit` (cut from `main` at `fd24069`; the plan was committed in `4bd221b`). The spec is
[`Docs/Specs/Epic-1-MVP/milestone-4-unindexed-audit-design.md`](../../Specs/Epic-1-MVP/milestone-4-unindexed-audit-design.md), approved 2026-11-08 and amended the same day for the
user's file-type decisions and again during the build; the master SDD ([`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md)) was amended in
Task 6 to describe what was built. Tasks 1 to 6 are complete; each of Tasks 1 to 5 passed its task review (Tasks 2, 3 and 5 after a fix round).

While the run was in progress the live ledger was kept outside the repository (in the SDD working folder); this document is that ledger cleaned up.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

| #   | Task       | Ruling                                                                                                                                                                         | Why                                                                                                                       | Cost if wrong                                                                                  |
|-----|------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------|
| R1  | Pre-flight | Implementers drove the app with Windows UI Automation from PowerShell instead of computer use, and used throwaway harnesses (never committed) for service-level tasks.         | Computer use cannot be granted to a locally built app; as in milestones 1 to 3.                                           | Visual-only defects are caught only where a screenshot was captured.                           |
| R2  | Pre-flight | The live ledger (`progress.md`) lived outside the repository during the run and Task 6 cleaned it into this document.                                                          | Writing the docs ledger between tasks would add commits outside the task reviews.                                         | None.                                                                                          |
| R3  | 1          | The "matches every file" checks use a new `FileTypePattern.MatchesEverything` (one part, only wildcards, at most one `?`) instead of the literal "only wildcards" reading.     | `?` or `.*.*` in Exclude must not be refused as "matches every file".                                                     | None known. Superseded by R10.                                                                 |
| R4  | 1          | The entry `*` normalises to `.*` (one part): it matches any name that has an extension, not a name without a dot. An empty part in a file name (`x.`) never matches.            | Agrees with the spec's table and with "a name with no dot matches nothing".                                               | None.                                                                                          |
| R5  | 1          | The commit trailer is `Claude Sonnet 5.5`, as the session's attribution reminder says, not the `Claude Opus 5.5` of the common rules.                                         | The session attribution takes precedence.                                                                                 | Cosmetic (two commits, `cc33399` and `24b0b3e`, carry the Sonnet trailer).                     |
| R6  | 2          | The SQL prefix filter falls back to loading every row only for no folder, more than 20 folders, or an empty folder path. `\|`, `%`, `_` and `[` are escaped, not a reason to fall back. | The escaping represents them exactly, so the optimisation stays on wherever it is safe.                                   | None; verified with folders containing each character.                                         |
| R7  | 3          | One traversal over the union of the input folders (each folder listed once, its owner worked out from the inputs) instead of one walk per input.                                | A file under nested inputs can then never be reported twice, and no folder is listed twice.                               | None; verified with nested inputs in both orders.                                              |
| R8  | 3, final review | A link whose target lies inside the scope of any input folder is not walked: the direct visit wins, so attribution is deterministic. A link whose target lies outside every input is walked, its files carry a canonical path (the link prefix replaced by the target), a file counts as indexed when the index holds either path, and folders are keyed by canonical path. This replaces the first-wins ruling. | The final review showed that first-wins reported indexed files as unindexed (index rows hold the real path); the controller superseded the ruling (`4efa941`). | Index rows that hold a link path into an in-scope folder no longer match (such files are reported unindexed under their real path). A file reached through two outside links is reported under whichever is listed first. |
| R9  | 3          | Content reachable through links to a folder and to its descendant (`L1`→`M`, `L2`→`M\N`) may be listed twice, under different link paths. Superseded by R8.        | The plan's loop-guard key was the resolved target for links and the full path otherwise.                                  | None now: the guard is keyed by canonical path, so a physical folder is listed once.           |
| R10 | 4          | Unverified folders with *Invalid characters* (or any other unlisted reason) go under *Other errors* with their reason text and no system message.                              | Spec §3.7 has five groups.                                                                                                | None.                                                                                          |
| R11 | 4          | A Stop that arrives after the walk finished but before the token is read writes a *Stopped by user* report for a complete walk.                                                | The runner tells a stop from completion by the token afterwards.                                                          | Cosmetic.                                                                                      |
| R12 | 5          | `MatchesEverything` is one part, only wildcards, **at least one `*`**, at most one `?`. This supersedes R3.                                                                    | The brief's wording alone would treat `?` as matching every file, against "`?` must not be refused"; caught in the UI run. | None; SDD §6.5.1 and spec §3.3 are reworded.                                                   |
| R13 | 5          | The *Group file types by* radios are owned by `ReportViewModel` (shown only for the Unindexed direction), not by the tab view model.                                           | A content slot in the shared Report view would need `$parent[...]` casts.                                                 | The Report view model knows about one direction-specific option.                               |
| R14 | 5          | Access keys: `Y` and `X` on the File types and Exclude labels; *Add...*, *Remove*, both *Edit...* buttons and the grouping radios carry none.                                  | Every letter on the window is taken.                                                                                      | Those controls have no keyboard accelerator.                                                   |
| R15 | 5          | *Add...* ignores a folder already listed (ignoring case and a trailing separator) and selects the added row; a duplicate that arrives by a hand-edited file is a pre-flight error. | A list with the same folder twice would be walked and reported confusingly.                                               | None.                                                                                          |
| R16 | 5          | Pre-flight order: identifiers, project-folder name, custom-name collision, the Unindexed folders, types and warnings, then the report checks.                                   | The cheap and most fundamental problems come first.                                                                       | None.                                                                                          |
| R17 | 5          | The window may widen to fit the side-by-side groups (target 640 px); a small `Folder` header was kept above the path column.                                                   | The two groups need the width; the header labels the column next to *Include subfolders*.                                 | A wider window, reversible with the user's trial. It built at 640 px.                          |
| R18 | 5          | The folder list is sized to the *Add...* / *Remove* stack beside it (a few rows), not to exactly three rows.                                                                   | "3 rows" was the controller's suggestion, not the user's.                                                                 | None.                                                                                          |
| R19 | 5          | While a run is under way the progress bar is a timer-driven fixed-width block on a sunken track, not `IsIndeterminate`.                                                        | The theme's marquee is blank for about 2.4 s of each 5 s cycle and its transform cannot be restyled.                      | A custom visual instead of the theme's. SDD §5.4a and spec §3.6 are reworded.                  |
| R20 | final review | A hand-edited project file's invalid File types or Exclude entries are kept as written (trimmed), shown in the tab's summary and named by the pre-flight at Start, instead of being dropped when the file loads. | Spec §4 says an invalid entry is a pre-flight error naming it; dropping it on load made that unreachable (the controller's ruling, `4efa941`). | The tab can show an entry the editor would refuse; the user must fix it before Start.          |
| R21 | final review | The HTML mock-ups are frozen as historical references and are not refreshed for milestone 4's layout; the SDD §5 and the XAML are the contract.                    | The controller's ruling; the layout changed repeatedly during the build and the mock-ups no longer match it.              | The mock-ups mislead anyone who reads them as the target; the SDD says they do not.            |
| R22 | fix wave 2 | Every link and scope comparison is made in final-path space: each input's own final path is resolved once (GetFinalPathNameByHandle), a link's target the same way, and a target under an input's final path is written back in that input's entered form for `CanonicalPath`. Loop-guard keys are final paths (not the entered form), so inputs given on a `subst` drive, a link and a nested input of another form all share one key. A link into an input without subfolders is walked for its subfolders only. | The controller's ruling (`1774980`); `ResolveLinkTarget` returns final paths, so `U:\` inputs never matched.              | A mapped drive was reasoned about, not exercised (no network mapping made): its UNC final path is handled like the `subst` case, but a DFS link still stays an ordinary folder. Two roots with the same final path are attributed to whichever lists first. |
| R23 | fix wave 2 | A SQL failure in the second, outside-link index query writes the report as `Failed` with everything already decided, instead of discarding the walk; held-back files are in neither total and the Summary gains a `Not checked:` line (also after a Stop before that query ends). | The controller's ruling (`1774980`): throwing away a long walk for a late failure was worse than a partial report.        | The report is partial, but says so (`Result: Failed`, the `Not checked:` line).                |

## User decisions

Taken by the user, not rulings.

**In the spec (§6), including the file-type decisions made between its approval and the plan:**

- A second direction that finds files on disk that are not in the index, alongside today's audit; usually one input folder, sometimes several, each with *Include subfolders*
  ticked by default.
- A file matches when its full path equals the index's path ignoring letter case; no path translation.
- An extension whitelist with built-in defaults for common document and image formats, editable per project, and a blacklist (**Exclude**) that wins over it; hidden, system and
  configuration files are always ignored and the app never guesses what to include.
- Extensions are written with or without the leading dot, multi-dot extensions are supported, and a leading `*.` means a leading dot. Matching is exact, counted from the end of
  the name one dot-separated part at a time; `*` and `?` are glob wildcards that stay within one part. Names with no dot, or starting with a dot, match nothing. Overlapping
  matches go to the most specific whitelist entry.
- Start-time checks: a wildcard-only Exclude entry is an error; a wildcard-only File types entry with other entries or with a non-empty Exclude, and an entry in both lists, are
  warnings (Continue / Cancel), each remembered for the session once answered.
- The report groups file types by listed type (default) or found extension, chosen in the Unindexed tab's Report group, and has its own name and layout.
- Built for millions of files and rows; loading the table's paths into memory is acceptable. No up-front count: live counts, **Remaining hidden**, **Files/second shown**.
- The version stays 1.0.0.

**Layout decisions during the build (2026-11-08, after the Task 5 review):**

- Compact the Unindexed tab and fix the tab height, so the window does not jump between tabs (built: 640 × 761 pixels on both tabs).
- The *Include subfolders* column goes on the left of the folder list.
- File types and Exclude become read-only single-line summaries with an **Edit...** button opening a dialogue with one entry per row, like Windows' environment-variable editor;
  **Reset** moves into the File types dialogue.
- Ticking *Use a custom report location and file name* **hides** the project folder and stage options instead of disabling them (on both tabs).
- *Unverified* goes back to the first statistics column.
- The Database and File Index Table groups sit side by side, each vertical inside: **a trial the user may reverse**.

## Plan defects

No task-interface mismatches (the pre-flight scan of the plan found none). Defects in the plan's and spec's own wording, found during the build:

- **"Matches every file" was defined too loosely.** The brief's definition (one part, only wildcards, at most one `?`) made `?` match every file; fixed by R12, and spec §3.3 and
  SDD §6.5.1 now state the rule.
- **Spec §3.5's memory figure was too low:** about 16 bytes a row (50 MB for three million rows), where the hashes and their set measure about 48 MB per million rows. Amended.
- **Spec §3.5 forgot the whitespace-led rows:** values are trimmed before matching, so a row with a leading space would miss every folder prefix and be reported as unindexed (found
  in the Task 2 review). The filter now also loads every row that starts with a whitespace character; spec §3.5 and SDD §6.5.2 say so.
- **Spec §3.6's "indeterminate (marquee)" bar could not be delivered with the theme** as it stands (R19); spec §3.6 and SDD §5.4a describe the moving block.
- **Spec §3.1's first layout** (a text box per file-type list, Reset on the main window, a window about 880 px tall) was replaced by the user's decisions above.
- **SDD contradictions found by the reviewers, fixed in Task 6:** §10 said the project folder and stages are *disabled* under Custom (now hidden); §11 said every dialogue is
  `MessageDialogueWindow` (now `FileTypesDialogueWindow` is named); §12's list of window and view-model pairs omitted the File Types dialogue.

## Defects found in review

Implementation defects, all fixed in a review round before the task was accepted.

- **Task 2, Important:** the prefix filter dropped index rows with leading whitespace, giving false "unindexed" files. Fixed in `8eecbb2` with a whitespace-led clause.
- **Task 3, Important:** the link loop guard ignored whether the first listing had descended, so a subtree reachable only through a link from a subfolders scope was silently
  dropped. Fixed in `1ae395c` (the guard records whether the folder descended and upgrades it).
- **Task 5, Important (two):** the sweeping progress bar read as a real percentage, against the spec's marquee; and the window was about 880 px tall and jumped about 170 px between
  tabs, overflowing a 768 px screen. Fixed in `f376d08` together with the user's layout decisions (R19).
- Tasks 1 and 4 passed their reviews with no Important finding.
- **Final review, Important (three):** a link into an input's own scope made first-wins attribution report files as unindexed whenever the index held the real path, and a link
  leading outside every input could not be matched against an index that held the target path (fixed in `4efa941`, R8); input folders were not canonicalised, so a folder typed
  as `U:/A/./B` loaded the wrong rows and escaped the duplicate check (fixed in `4efa941`: one canonical list feeds the filter, the walk and the duplicate check); invalid
  hand-edited file-type entries were dropped on load, so they were never shown or named (fixed in `4efa941`, R20).

- **Fix wave 2, Important (three), found by the re-review of wave 1 and fixed in `1774980`:** a link was skipped whenever any input covered its target, which dropped the
  subfolders of an input without subfolders reached through a link (N1); `ResolveLinkTarget` returns final paths, so inputs on a `subst` or mapped drive never matched a link
  target and canonical paths came out in a form the index does not use (N2, R22); and a SQL failure in the second index query discarded the whole walk (N3, R23). The minors:
  two double blank lines (N4) and the documentation of the second index set and of links to an ancestor of an input. An invalid hand-edited element holding a space or comma (N5)
  was traced from load through the summary round-trip to the snapshot, and opening and closing such a file gives no save prompt; no code change was needed.

## Outstanding items

### Final review wave (this milestone)

Minor findings from the task reviews, fixed in the final-review fix wave unless marked open below. Fixed in `de70382`: the `FileTypeList.Parse` split, the shared default array
(fixed in `4efa941`), the `ReportViewModel` spacing, the `stackalloc` size and its memory summary, the blank lines, the long summary lines, the two-elements-on-a-line XAML, the
hard-coded marquee colours, the File Types dialogue (Edit disabled with no selection, a visible selected row, a space or comma refused), the `Alt+Y` / `Alt+X` targets, the final
newlines, and the double-unverified upgrade edge and the Stop wording (`4efa941`). **Still open** (not worth a change on their own): the baseline JSON re-serialised on every
call, per-pattern dot scans, the forwarding wrappers in `ReportBuilder`, and documenting that the Summary numbers come from two sources.

- **Task 1:** `FileTypeList.Parse` splits only on `, space \t \r \n` (use `char.IsWhiteSpace`); `SettingsStore` returns the shared `Default.FileTypes` array instance (return a
  copy); `ReportViewModel` lost a space after `=`; the baseline JSON is re-serialised on every call (cache it if per-keystroke change tracking appears); per-pattern dot scans
  (only if profiling shows a need).
- **Task 2:** `stackalloc char[StackLimit]` zero-fills 2 KB on every hash (use `stackalloc char[trimmed.Length]`).
- **Task 3:** an upgrade re-listing through a link can report the same physical folder unverified twice, or unverified after its files were reported (transient failures only); a
  link whose target is an input root could defer to the root's own visit for deterministic attribution; a missing blank line in `FolderWalker.cs` near line 316.
- **Task 4:** two XML summary lines over 180 characters (`UnindexedAuditRunner.cs:3`, `UnindexedReportBuilder.cs:3`); a blank line lost between `AddHeader` and `AddSummary` in
  `ReportBuilder`; forwarding wrappers in `ReportBuilder`; the Summary numbers come from two sources (document that progress is fresh per run); local declaration alignment in
  `UnindexedAuditRunner`.
- **Task 5:** formatting slips (two elements on one line in `UnindexedAuditView.axaml` and `UnindexedFilesTabView.axaml`); a missing blank line before `CanRemove`; the marquee
  colours are hard-coded (use theme brushes); in the File Types dialogue *Edit* does nothing with no selection (disable it like *Delete*, and show a row highlight), and a line
  containing a space or a comma is split silently (reject it, like the environment-variable editor); `Alt+Y` and `Alt+X` should reach the *Edit...* buttons; trailing newlines on
  new files.
- **Lines over 180 characters** in `PreflightChecker.cs` (two) and `DialogueService.cs` (one) pre-date this milestone.

### Later / nice to have

- **Mock-ups:** `direction-tabs-layout.html` and the other mock-ups predate the side-by-side top groups, the compact Unindexed tab and the File Types dialogue; they are kept as
  historical references (R21), and the SDD §5 is the contract.
- **The side-by-side groups are a trial:** if the user reverses it, the window returns to a taller layout and the SDD §5 diagram and text change with it. The 761 px height
  overflows a 768 px screen.
- **Theme marquee:** if Classic.Avalonia's indeterminate bar gains a restylable or gap-free animation, replace the custom moving block (R19).
- **Help topics:** the user guide article should describe the unindexed audit, its file-type syntax and its report (the constant `HelpLinks.UserGuideUrl` is still empty).
- **Task 2 deferred:** `COLLATE` on a `varchar` column in another code page could best-fit convert (unrealistic here); folding differences between SQL's `Latin1_General_100` and
  .NET upper-casing for exotic letters could make the filter and the hash disagree, which could report a file as unindexed, not only hide one; exotic, deferred.
- **Task 3 deferred:** a per-entry `FileName.ToString()` allocation, and each folder's full listing held in memory (fine for realistic trees).
- **Cold network shares:** the walk was measured only on a local SSD (see *Measurements*); a first run on a real share, with DFS links, may want a different listing limit.
- Milestone 3's, 2's and 1's deferred minors that remain open are listed in the [milestone 3 ledger](milestone-3-ledger.md), the [milestone 2 ledger](milestone-2-ledger.md) and the
  [milestone 1 ledger](milestone-1-ledger.md).

## Incidents

- **A Notepad++ tab left open in Task 5:** View Report opened a report in the user's own Notepad++ (the default `.txt` handler). The tab was not closed, since the editor holds the
  user's own tabs.
- **A temporary `U:` drive in Task 5:** to exercise the built `U:\Projects\…` location the implementer mapped `U:` with `subst` and removed it afterwards.
- **A wrong diagnosis in Task 5, corrected in its fix round:** the implementer first reported that `ProgressBar.IsIndeterminate` draws nothing in Classic; it does draw, and the
  frames had fallen in its idle gap (R19). While finding that out, a throwaway test window that overrode the marquee's `RenderTransform` crashed; nothing in the repository was
  affected.
- **Scratch system changes in Tasks 3 and 4:** an ACE denying a folder, junctions and scratch databases were created on scratch paths for testing and all were removed or dropped
  before each task finished.

## Not exercised at runtime

- **Stop during the walk, in the UI:** 300,000 files finish in under a second, so UI polling could not hit it. It was exercised at service level (a Stop 25 ms into the walk gave
  a *Stopped by user* report with partial counts). Stop during *Loading the index...* was exercised in the UI, with a stalled load.
- **Cold network shares, DFS links and large remote tables:** the walk was measured on a local SSD against LocalDB; behaviour over a real share, and at three million rows or
  more, is extrapolated, not measured.
- **F5, Ctrl+R and Esc** through the menu hot keys: injected keystrokes do not trigger them under UI Automation (as in milestone 3). The XAML is unchanged, and the menu items
  enable and disable with the buttons. Worth a manual check, Esc as Stop especially.
- **View Report on the Unindexed tab** was opened once, in the Task 5 round 0; after the layout rework only its enabled state was checked.
- **Closing the window mid-run** on the Unindexed tab (the stop-and-exit prompt) was not driven.
- **Case-sensitive column collations** in a real project database: covered at service level (a case-sensitive column collation on LocalDB), not on a project's own table.
- **The final layout** was measured with UI Automation and screenshots, but has not been reviewed by the user at their screen size.

- **Links outside every input** (the second, filtered index query for files indexed under their target path) were exercised at service level against LocalDB with one target
  folder, in forward-slash and `.\` input forms and with a hand-edited project file opened in the app; not with more than 20 target folders (which loads every row) or at scale.

## Measurements

All on this machine, a local SSD and LocalDB, warm cache.

- **Loading the index:** 1,000,000 paths in 1.62 s, +47.9 MB managed (about 48 MB per million rows).
- **Whole run (Task 4):** 104,000 files and 106,000 index rows in 3.1 s, peak working set 69.9 MB.
- **Walk:** 50,000 files in 500 folders in 0.30 to 0.44 s.

## Commits

`git log --oneline main..HEAD`, newest first (the commit that closes this ledger follows these):

```
f376d08 fix(🐛): rework the Unindexed tab layout and the progress bar
8927cb5 feat(✨): add the Unindexed Files tab
5d4f0a3 feat(✨): run the unindexed audit and write its report
1ae395c fix(🐛): keep subtrees reachable through links when a folder was first listed without descending
6529baf feat(✨): walk folders concurrently for the unindexed audit
8eecbb2 fix(🐛): keep index rows with leading whitespace in the prefix filter
24b0b3e feat(✨): load index paths as 128-bit hashes
cc33399 feat(✨): add scan folders and file types to project settings
4bd221b docs(📝): add the milestone 4 plan and amend its design for file-type lists
```

## Final review

Pending.
