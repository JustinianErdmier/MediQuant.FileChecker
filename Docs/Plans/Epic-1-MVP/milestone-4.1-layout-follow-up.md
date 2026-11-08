# Milestone 4.1 — Layout Follow-up Implementation Plan

**Date:** 2026-11-08

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Five layout changes the user asked for after milestone 4: project options disabled (not hidden) under Custom, a Project Settings group holding the
save checkbox and a status message, the *Include subfolders* column back on the right without horizontal overflow, a *Clear* button in the file-type editors,
and a window height fitted to the Missing Files tab with the Unindexed Files tab scrolling vertically.

**Architecture:** Small, independent view changes. The save checkbox moves out of `ReportView` into a new `ProjectSettingsView` / `ProjectSettingsViewModel` pair
under the shared top groups; its status message is computed in the view model from `ReportLocationState.SaveProjectSettings`, `ProjectSettingsService.FilePath`
and the shared report folder (`ReportLocationState.ResolveFolder()`). The window loses its fixed tab height and takes the Missing Files tab's natural height;
the Unindexed Files tab's content sits in a vertical `ScrollViewer`.

**Tech Stack:** unchanged (.NET 10, Avalonia 11.3.22, Classic.Avalonia 11.3.0.3, CommunityToolkit.Mvvm). No new packages.

**Spec:** the user's request (2026-11-08, items 1–5 below) and the master SDD [`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md)
§5 (window, Win9x conventions), §8 (project settings, save rule) and §12 (code conventions). No milestone spec: the request is the spec. Milestone 4's ledger
[`milestone-4-ledger.md`](../../Ledgers/Epic-1-MVP/milestone-4-ledger.md) records the layout this changes.

The user's request, verbatim in substance:

1. Undo hiding the project folder and stage inputs when Custom is ticked; keep them displayed but disabled.
2. Keep the Database and File Index Table groups side by side, and add a settings section under them. Move the save-settings checkbox into it. Below the
   checkbox, a message: saved as `FileAuditorSettings.json` in the same location as the audit reports; if the loaded settings file's path does not match the
   configured report path, say it is saved at a different path (no need to show the path); if unticked, say this project's settings are not being saved.
3. Move the *Include subfolders* column back to the right in the folders list, but truncate the folder column so the checkbox is never hidden — the list never
   overflows horizontally.
4. In the File Types and Exclude editors, add a button that clears the list; disabled when the list is empty.
5. No dead space: after the other changes, find the minimum height of the Missing Files tab and make it the main window's fixed height. When the Unindexed
   Files tab is selected, the tab content scrolls vertically if it overflows.

## Global Constraints

- **No automated tests.** Verify with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings) and by driving
  the built app with Windows UI Automation from PowerShell. **Before any keystroke or click, confirm FileAuditor.exe is the foreground window**; prefer UI
  Automation patterns over SendKeys. Never open a report in an external program. Close the app when done.
- Exact copy:
    - Group header: `Project Settings`. Checkbox: `Save project settings _with the report` (unchanged text and access key).
    - Status message, one of:
        - ticked, and no settings file is open or the open file is in the report folder: `Saved as FileAuditorSettings.json in the same folder as the audit reports.`
        - ticked, and the open settings file is in a different folder from the report folder: `Saved to the opened settings file, in a different folder from the audit reports.`
        - unticked: `This project's settings are not being saved.`
    - Editor button: `C_lear` — access key **L**, unique within the dialogue (N, E, D, R are taken; C is not used so the label reads as Clear).
- Folder comparison for the status message uses the same normalisation as the save rule (`SameFolder` in `ProjectSettingsService`). An incomplete report
  location counts as "no mismatch" when no file is open, and as "different" when a file is open and the location is incomplete.
- The window stays 640 px wide and fixed-size; its height becomes whatever the Missing Files tab needs with nothing clipped. `MainWindow.axaml` ≤ 100 lines.
- The untick warning, the save rule, the busy gate and change detection behave exactly as before; the checkbox only moves.
- All CLAUDE.md and SDD §12 conventions apply (pair rule: `ProjectSettingsViewModel` ↔ `ProjectSettingsView`). Commit each task automatically, staging only
  its files and ticking its boxes in this plan.

## Review Focus

1. **The status message keeps up.** It must change when the checkbox, the report location, *Open project...* or *Close project*, or a save that moves the project's
   file (the folder-changed prompt's *Report Folder*) changes the answer — Task 1 step 3.
2. **Nothing clipped, no dead space.** On the Missing Files tab the window shows every control with no empty band; on the Unindexed Files tab the content scrolls,
   the Audit group's buttons stay reachable, and nothing scrolls horizontally — Task 3 step 3.
3. **Long folder paths.** A path far wider than the list shows truncated with the checkbox still visible and clickable, and the full path available as a tool tip
   — Task 2 step 3.

---

### Task 1: Report options disabled again, and the Project Settings group

**Files:**

- Create: `Source/FileAuditor/ViewModels/Groups/ProjectSettingsViewModel.cs`, `Source/FileAuditor/Views/Groups/ProjectSettingsView.axaml(.cs)`
- Modify: `Views/Groups/ReportView.axaml`, `Views/Main/MainWindow.axaml`, `ViewModels/Main/MainWindowViewModel.cs`, `Services/Settings/ProjectSettingsService.cs`
  (expose what the message needs, e.g. a `FilePath` change notification or a method returning whether the open file is in a given folder)

- [x] **Step 1:** In `ReportView`, the project folder row and both stage groups are always visible; they are disabled while *Use a custom report location* is ticked (as before
  milestone 4's change). Remove the save checkbox from `ReportView`.
- [x] **Step 2:** `ProjectSettingsView` (group `Project Settings`, full width, below the side-by-side top groups and above the tabs): the checkbox bound to the
  shared `SaveProjectSettings`, then the message (`TextWrapping="Wrap"`). `ProjectSettingsViewModel` computes the message per Global Constraints and raises it
  whenever an input changes. Disabled with the other inputs while a run or a busy command is under way.
- [x] **Step 3: Verify.** Clean build. In the app: tick/untick (warning still appears once per session); with a custom location set to a scratch folder, Start
  saves the project, then reopen it — message is the "same folder" text; change the location to another scratch folder — message switches to the "different
  folder" text; *Close project* — back to the "same folder" text; Custom ticked shows the project options disabled, not hidden, on both tabs. Screenshot both tabs.
- [x] **Step 4: Commit** — `feat(✨): add a Project Settings group and disable report options instead of hiding them`.

### Task 2: The folders list column order and truncation

**Files:** Modify `Views/Tabs/UnindexedFilesTabView.axaml` (and its code-behind only if a tool tip needs it)

- [x] **Step 1:** Column order: folder path (star width) then *Include subfolders* (auto width) on the right, header row to match.
- [x] **Step 2:** The path cell truncates with an ellipsis (`TextTrimming="CharacterEllipsis"`) and shows the full path as a tool tip; the list's horizontal
  scroll bar is disabled so it can never overflow sideways.
- [x] **Step 3: Verify.** Clean build. Add a folder whose path is far wider than the list (a deeply nested scratch path): the checkbox stays visible and toggles;
  no horizontal scroll bar; the tool tip shows the full path. Screenshot.
- [x] **Step 4: Commit** — `fix(🐛): keep the Include subfolders column visible for long folder paths`.

### Task 3: Clear in the editors, and the fitted window height

**Files:** Modify `Views/Dialogues/FileTypesDialogueWindow.axaml(.cs)`, `ViewModels/Dialogues/FileTypesDialogueViewModel.cs`, `Views/Main/MainWindow.axaml`,
`Views/Tabs/UnindexedFilesTabView.axaml`

- [x] **Step 1:** A `C_lear` button in the editor's button column (after Delete, before Reset), removing every entry; disabled when the list is empty. OK with an
  empty list is allowed (an empty File types list is refused later, at Start, as today).
- [x] **Step 2:** Remove the tab control's fixed height. Size the window to the Missing Files tab's natural height (measure it with the app running — e.g. let the
  window size to content on the Missing tab once, read `Bounds`, then fix that height in XAML; keep `CanResize="False"`). Wrap the Unindexed Files tab's
  content in a `ScrollViewer` with vertical scrolling *Auto* and horizontal *Disabled*, so that tab scrolls inside the same window height.
- [x] **Step 3: Verify.** Clean build. The editors: Clear empties the list and disables itself; New re-enables it. The window: on the Missing tab no empty band
  below the Audit group and nothing clipped (custom location ticked and unticked); on the Unindexed tab a vertical scroll bar appears when needed, every control
  is reachable by scrolling, no horizontal scroll. Report the window size. Screenshot both tabs.
- [x] **Step 4: Commit** — `feat(✨): add Clear to the file-type editors and fit the window to the Missing Files tab`.

### Task 4: Documentation sync and ledger

**Files:** Modify `Docs/Specs/1-file-auditor-master-design.md`, this plan (tick boxes), `MediQuant.FileAuditor.slnx`; Create
`Docs/Ledgers/Epic-1-MVP/milestone-4.1-ledger.md`

- [x] **Step 1:** Amend the SDD: §5 window diagram and size, the Project Settings group and its messages, §5.3 (options disabled, not hidden, under Custom — and
  remove milestone 4's "hidden" wording everywhere), the folders list, the editors' Clear button, the scrolling Unindexed tab, §9 (new view pair), §12 (pair list).
- [x] **Step 2:** Write the ledger (rulings, user decisions, plan defects, outstanding items, incidents, not exercised, commits, final review "Pending.").
- [x] **Step 3: Verify** — clean build; Markdown prose ≤ 180 characters; every file under `Docs/` is in the `.slnx` (this plan and the ledger after milestone 4's,
  in milestone order).
- [x] **Step 4: Commit** — `docs(📝): sync the SDD with the layout follow-up and close the milestone 4.1 ledger`.
