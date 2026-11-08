# Milestone 2 — Project Settings

**Date:** 2026-11-06
**Status:** Approved 2026-11-06

## 1. Intent

Today File Auditor remembers one set of inputs per Windows user, in `%APPDATA%\MediQuant\FileAuditor\settings.json`. In practice the inputs belong to a **project**, not
a person: every project has its own server, database, index table and report location, and a user moves between projects. This milestone makes the settings a
**project file**, `FileAuditorSettings.json`, saved beside the project's report, so that it lives in the project's repository with the report and any teammate can open
the same project. The per-user file goes away.

The version stays at **1.0.0**: this milestone is part of the MVP epic.

## 2. Goals, non-goals, anti-goals

**Goals**

- Each project's inputs are saved in its own `FileAuditorSettings.json`, beside the report.
- The app opens empty, with its defaults, and the user opens a project from the *File* menu.
- Saving is on by default and happens without fuss; turning it off is deliberate and does not nag.

**Non-goals**

- Recent-projects lists, command-line arguments or file associations for opening a project.
- Migrating the old `%APPDATA%` settings file. It is simply no longer read or written.
- Changing the version number.

**Anti-goals**

- Silently saving a project's settings over another project's file.
- Asking the same save question again after the user has answered it, until the project is closed or the app is relaunched.
- Losing the user's edits without asking, when the window or the project is closed.

## 3. Behaviour

### 3.1 Start-up

The app opens with every input at its default (`AppSettings.Default`) and no project open. Nothing is read from `%APPDATA%`. The checkbox *Save project settings with
the report* (§3.3) is ticked.

### 3.2 File menu

A new first group, followed by a separator, then the existing items:

| Item                  | Behaviour                                                                                                   |
|-----------------------|-------------------------------------------------------------------------------------------------------------|
| **Open project...**   | Runs the save logic for the current project (§3.4), then opens a file picker (§3.6) and loads the chosen file |
| **Close project**     | Runs the save logic for the current project (§3.4), then resets the app as if it had just been launched (§3.6) |
| *(separator)*         |                                                                                                             |
| Start, Stop, View Report, *(separator)*, Exit | Unchanged                                                                             |

Both new items are disabled while an audit is running, like every other input. Access keys are left to the builder (unique within the menu).

### 3.3 The Save project settings checkbox

- **Placement:** the last row of the Report group, below *File name*. Label: *Save project settings with the report* (access key left to the builder). Chosen by the
  user from three mock-ups (option A).
- **Default:** ticked — on launch, after *Open project...* and after *Close project*.
- **Unticking** shows a warning dialogue first, making sure it is not a mistake: with the box unticked, the project's settings are no longer saved automatically,
  and the app will ask instead (§3.4). Buttons **Untick** (or similar) and **Cancel**; *Cancel* leaves the box ticked. Once the user has confirmed, the warning is
  not shown again — unticking later in the same session goes straight through — until *Close project* or a fresh launch.
- **Ticking** it again needs no confirmation.
- The checkbox is disabled while an audit is running.

### 3.4 The save logic

One rule, applied in three places: when **Start** is pressed (after the pre-flight checks pass, where settings are saved today — SDD §6.1 step 4), when the **window is
closed**, and on **Close project** (and *Open project...*, which closes the current project first).

1. **Nothing has changed** since the project was opened or last saved → do nothing, and do not ask. *(A fresh, untouched window counts as unchanged.)*
2. **Checkbox ticked** → save (§3.5), without asking.
3. **Checkbox unticked and the user has already answered No** this session → do nothing.
4. **Checkbox unticked and not yet answered** → ask *"Do you want to save this project's settings?"* with **Yes** and **No**:
   - **Yes** saves (§3.5) and ticks the checkbox, so later saves happen without asking (the user's decision).
   - **No** does not save, and the app remembers the answer: it does not ask again until *Close project* or a fresh launch.

So the three states read: *ticked* — always save; *unticked* — ask at the next save point (*Yes* ticks the box again); *unticked and declined* — never ask, never
save, for the rest of the session.

On **Start**, the save (and any prompt) comes after the pre-flight checks pass and before the audit begins. *Yes* or *No* lets the operation go ahead; the
prompts about where to save, and save failures, can also back out of it (§3.5, §3.7).

When the window is closed **during a run**, the existing *"An audit is in progress. Stop it and exit?"* prompt (SDD §6.4) comes first; the save logic runs only if
the user answers *Yes*.

### 3.5 Where the file is saved

- **File name:** `FileAuditorSettings.json`.
- **Beside the report, per stage.** The file goes in the report's folder (the resolved *Location*). With a built location, Sample and Final are different folders
  (`U:\Projects\<folder>\DE\Sample\` and `...\DE\Final\`), so each stage has its own settings file. That is intended (the user's decision): the two stages may use
  different databases.
- **A project that has never been saved** (no file was opened, nothing saved yet) → saved in the report's folder.
- **A project opened from a file** (or saved before in this session) → saved back to that file. If the report's folder no longer matches the folder the file is in
  (for example, the stage was switched), the app asks: *"The report folder has changed.", a blank line, "This project's settings were opened from:", the file's path, a
  blank line, "The report is now written to:", the report's folder, a blank line, "Where should the settings be saved?"* with **Report Folder**,
  **Original File** and **Cancel**. *Report Folder* saves to the report's folder, and from then on that is the project's file; *Original File* saves back where it
  was opened from; *Cancel* backs out of the operation that triggered the save (§3.7).
- **A different settings file is already there.** When the target is a `FileAuditorSettings.json` that is not the project's own file (a never-saved project, or
  after choosing *Report Folder*), the app warns: *"A project settings file already exists in this folder: <path>. Do you want to overwrite it?"* with
  **Overwrite**, **Skip Save** and **Cancel** (the user's decision):
  - **Overwrite** saves over it, and it becomes the project's file; the operation goes ahead.
  - **Skip Save** does not save this time; the operation goes ahead.
  - **Cancel** backs out of the operation that triggered the save (§3.7).
- **Format:** the same JSON as today's settings record (`AppSettings`), written with the temp-file + `File.Move(overwrite: true)` pattern.

### 3.6 Opening and closing a project

- **Open project...** first runs the save logic (§3.4); *Cancel* there stops here. It then opens a file picker titled *Open Project*, filtered to JSON files, with
  `FileAuditorSettings.json` suggested. It starts in the report's *Location* when that is a full path to a folder that exists, otherwise in the user's profile
  folder (`%USERPROFILE%`).
  - **Picker cancelled** → nothing changes: the current project stays open exactly as it was, remembered answers included.
  - **File cannot be read** (invalid JSON, access denied, not a settings file) → an error dialogue, and the current project stays open unchanged.
  - **File read** → the project is reset as for *Close project*, then the file's settings are loaded and it becomes the project's file. Fields are read with
    today's rules (SDD §8): a missing field, a null string or an undefined enum value falls back to its default, field by field.
- **Close project** resets every input to its default, ticks the checkbox, forgets the "don't ask again" answer and the open file, and clears the Audit group's
  statistics and the last run's status (the status bar shows *Enter the required details*, SDD §5.5). It is equivalent to relaunching the app.

### 3.7 Backing out, and when a save cannot be done

Every save point can be backed out of. *Cancel* (on the overwrite or folder-changed prompts, or on a failure) means:

| Save point             | Cancel means                                                                 |
|------------------------|------------------------------------------------------------------------------|
| **Start**              | The audit does not start; the window is left as it was.                      |
| **Closing the window** | The window stays open.                                                       |
| **Close project**      | The project stays open, unchanged.                                           |
| **Open project...**    | The picker is not shown; the current project stays open, unchanged.         |

A save **cannot be done** when the report's location is incomplete (no Project folder, or a custom Location that is not a full path), its folder does not exist,
or the write fails (read-only, access denied, disconnected drive). The app never creates a folder just to save the settings. In each case it shows an error
dialogue naming the path and the reason, with **Skip Save** and **Cancel**, with the same meanings as above. On **Start** the pre-flight checks (SDD §6.1) have
already confirmed the folder exists and is writable, so a failure there is rare.

## 4. Builder's calls (decided by Claude, not the user)

- *Open project...* runs the same save logic as *Close project* before loading, because loading replaces the current project; it also forgets the "don't ask
  again" answer, since the new file is a different project.
- The checkbox's state is **not** stored in the settings file; it is a per-session preference, ticked by default.
- *Cancel* on the folder-changed prompt, and *Skip Save* / *Cancel* on a save failure, follow the user's three-way rule for an existing file (§3.5, §3.7), so every
  save point behaves the same way.
- A save never creates a folder; an incomplete or missing report folder is a save that cannot be done (§3.7).
- *Open project...*: a cancelled picker or an unreadable file leaves the current project untouched; the reset happens only once a file has been read (§3.6).
- Closing mid-run: the existing stop-and-exit prompt comes before the save logic.
- "Nothing has changed" is judged by comparing the current inputs (`Snapshot()`) with the settings last loaded or saved.
- The old `%APPDATA%\MediQuant\FileAuditor\settings.json` is left on disk untouched; `SettingsStore` stops reading and writing it.
- The window title stays *File Auditor*; the open project is not shown in it.

## 5. SDD sections to amend in this milestone

§1 (comparison table and *Why the changes*), §2 (anti-goal "settings that leak between users"), §4 (Settings file row), §5.3 (checkbox), §5.6 (File menu), §5.8
(command states for the new items), §6.1 step 4, §6.4 (closing the window), §8 (rewritten for project files), §9 (`SettingsStore` and any new service), and the
mock-up note in §5.

## 6. Decided by the user

- Settings belong to the project, saved as `FileAuditorSettings.json` beside the report; the per-user `%APPDATA%` file goes away; the app opens empty.
- *Open project...* and *Close project* lead the *File* menu; *Close project* resets the app as if freshly launched.
- The picker starts in the report's Location when it is valid, otherwise in the user's folder.
- The checkbox goes on the last row of the Report group (mock-up option A), ticked by default.
- Start, closing the window and *Close project* all apply the same save logic.
- An opened project whose report folder has changed asks where to save.
- With a built location, each stage keeps its own file beside its report.
- An existing, different settings file prompts **Overwrite**, **Skip Save** or **Cancel**, where *Cancel* backs out of the triggering operation.
- The version stays 1.0.0.
