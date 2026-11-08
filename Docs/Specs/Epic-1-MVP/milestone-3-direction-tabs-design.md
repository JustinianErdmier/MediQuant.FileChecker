# Milestone 3 — Direction Tabs

**Date:** 2026-11-08 **Status:** Approved 2026-11-08

## 1. Intent

File Auditor today checks one direction: every file the index table points to is on disk (*missing files*). Milestone 4 adds the opposite direction: every file on
disk is in the index table (*unindexed files*). This milestone prepares the window and the project file for two directions, without adding the second audit yet:
the window gains a tab control, today's audit moves into a **Missing Files** tab, the Report group learns to be shared between tabs, and the reports are renamed so
the two will pair up.

Splitting it this way keeps the app working and shippable at the end of each milestone. The version stays **1.0.0**: both milestones belong to the MVP epic.

## 2. Goals, non-goals, anti-goals

**Goals**

- A standard Win9x tab control holds each direction; the inputs both directions use stay above it.
- Today's audit behaves exactly as before, from inside the **Missing Files** tab.
- The Report group's location is one set of values, ready to be shown on every tab, while each direction keeps its own report file name.
- Report names pair up: `FileAuditorMissingReport_…` now, `FileAuditorUnindexedReport_…` in milestone 4.

**Non-goals**

- The unindexed audit itself, its tab, inputs and report (milestone 4).
- Loading project files saved by milestone 2's build. Nothing is released yet, so the project file's shape may change freely.

**Anti-goals**

- Any change to how the missing-files audit checks files, counts, or writes its report body.
- Two copies of the report location that can drift apart.

## 3. Behaviour

### 3.1 The window (layout A, revised — chosen by the user from mock-ups)

Top to bottom:

1. Menu bar (unchanged).
2. **Database** group (unchanged), shared by both directions.
3. **File Index Table** group: **Table**, **Path column**, **ID column** (Optional), shared by both directions. The *Report empty (zero-byte) files as missing*
   checkbox moves out of this group (it applies to the missing-files direction only).
4. A **tab control**. In this milestone it has one tab, **Missing Files**; milestone 4 adds **Unindexed Files**. The Missing Files tab holds, top to bottom:
    - an **Options** group with *Report empty (zero-byte) files as missing* (unchanged behaviour and default);
    - the complete **Report** group (§3.2);
    - the **Audit** group (statistics, progress bar, View Report / Start / Stop), unchanged.
5. Status bar (§3.4).

The window stays fixed-size and grows in height to fit. Tab names: **Missing Files**, **Unindexed Files**.

### 3.2 The Report group, shared between tabs

Each tab shows a complete Report group, never split apart (the user's decision). All of its values are **the same on every tab** — change one on one tab and the
other tab shows the change — except the **file name**, which belongs to the tab's direction:

| Report group item                            | Shared or per tab |
|----------------------------------------------|-------------------|
| *Use a custom report location and file name* | Shared            |
| Project folder, Project Stage, Audit Stage   | Shared            |
| Location (built preview or custom)           | Shared            |
| File name                                    | **Per tab**       |
| *Save project settings with the report*      | Shared            |

Report names (the user's decision):

| Direction       | Built name                                     | Custom default                   |
|-----------------|------------------------------------------------|----------------------------------|
| Missing files   | `FileAuditorMissingReport_<Audit Stage>.txt`   | `FileAuditorMissingReport.txt`   |
| Unindexed files | `FileAuditorUnindexedReport_<Audit Stage>.txt` | `FileAuditorUnindexedReport.txt` |

The missing-files report's title line becomes `File Auditor Missing Report` (underlined to match). Nothing else in its body changes.

### 3.3 Commands and the menu

*File → Start* and *View Report* (and F5, Ctrl+R) act on the **selected tab's** audit, and enable and disable with that tab's buttons. *Stop* (and Esc) acts on
the **running** audit, whichever tab is selected. With one tab this is today's behaviour; the design is laid down now so that milestone 4 only adds a tab.

### 3.3a Help menu

The *Help* menu gains a first item, **Help Topics** (access key `H`, shortcut **F1**), followed by a separator and the existing *About File Auditor*. It opens the
File Auditor user guide (a wiki article) in the default browser by shell execute (the user's decision). The article's address is a single hard-coded constant that
is an **empty string for now**; the user will fill it in once the article exists. While the address is empty, the item and F1 are disabled.

### 3.4 Status bar

One status bar for the window. **While an audit runs**, it shows that run's phase and percentage, whichever tab is selected, so switching tabs never hides a run. **Otherwise** it
shows the selected tab's last result, or its idle text (`Ready` / `Enter the required details`, computed from that tab's Start). Each tab keeps
its own last result.

### 3.5 The project file

`FileAuditorSettings.json` holds everything for both directions. In this milestone: the shared inputs as today, and the missing-files report's custom file name.
Milestone 4 adds the unindexed direction's folders, file types and custom file name. No compatibility with milestone 2's files is kept (unreleased).

## 4. Builder's calls (decided by Claude, not the user)

- The tab control appears now with a single tab, so milestone 3 ships a complete, consistent window; a one-tab property sheet is ordinary Win9x.
- *Report empty files as missing* moves into an **Options** group on the Missing Files tab, since it no longer applies to every direction.
- The Report group's shared values live in one view model that every tab's Report view binds to; only the file name is per direction. That makes mirroring
  automatic and drift impossible.
- Start, View Report and the idle status follow the selected tab; Stop and the status bar follow the running audit while one runs.
- The two mock-ups the user chose (window layout A revised, unindexed report option A) are added under `Docs/UserInterfaceDesign/Mockups/` as references,
  registered in the `.slnx`; the spec text remains the contract.
- The report title becomes *File Auditor Missing Report* to pair with *File Auditor Unindexed Report*.
- The project file's custom-name property is renamed for the missing direction (for example `MissingCustomFileName`); old files need not load.
- The selected tab is not saved.
- *Help Topics* is the Win9x name for the help item; F1 is its shortcut (the user suggested F1 or Alt+F1; F1 is the Windows standard). It is disabled while the
  article address is empty, rather than showing a message.

## 5. SDD sections to amend in this milestone

§4 (report names), §5 (window diagram, tab control, §5.2 group contents, §5.3 Report group sharing, §5.4 Audit group placement, §5.5 status bar, §5.6 menu
targets and the Help Topics item, §5.8 command states), §7 (report title and names), §8 (project file contents), §9 (code shape for the new views and view
models), and the mock-up note.

## 6. Decided by the user

- Two directions, each in its own tab, standard Win9x; shared inputs above the tabs (mock-up layout A, revised).
- The ID column is shared, not per direction.
- Each tab shows a complete Report group; every value is mirrored between tabs except the file name; the *Save project settings* checkbox is mirrored too.
- Reports renamed to pair up: `FileAuditorMissingReport_…` and `FileAuditorUnindexedReport_…`; custom defaults `FileAuditorMissingReport.txt` and
  `FileAuditorUnindexedReport.txt`.
- One project settings file holds both directions.
- The work is split into two milestones: this one (tabs) and milestone 4 (the unindexed audit).
- A Help menu item opens the user-guide wiki article, with F1; its address is hard-coded as an empty string until the article exists.
