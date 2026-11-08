# Milestone 6 — Code Review Clean-up Implementation Plan

**Date:** 2026-11-08

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the user's 44-item code review before the first publish: honest nullability in the settings file, no analyser or Rider warnings left, one switch
convention, types where they belong, descriptive names, a doc comment on every type and member, and an SDD that matches the code.

**Architecture:** No behaviour changes, with three exceptions. Project files saved before commit `2cdf814` keep their stages again. A `null` bool in a project file now falls
back to its default instead of making the file unreadable. A new global exception handler catches any unexpected exception. One the app can survive is shown in an error dialogue,
and the app carries on; one it cannot survive is explained in a message before the app closes. Settings are read through a new JSON-shaped `ProjectFile` model, mapped into
an `AppSettings` that has no nullable members. Stateless services become static classes.

**Tech Stack:** unchanged (.NET 10, Avalonia 11.3.22, Classic.Avalonia 11.3.0.3, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient). No new packages.

**Spec:** the user's 44-item review of 2026-11-08 (summarised per task below, with the rulings taken in chat) and the master SDD
[`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) §8 (settings), §9 (code shape) and §12 (code conventions). No milestone spec.

Decisions taken in chat on 2026-11-08:

- **Settings nullability (items 23–25):** a `ProjectFile` model with nullable members matching the JSON, mapped into an `AppSettings` with no nullable members.
- **Naming (item 15):** make every name that holds one enum value singular again, plus a sweep of the whole codebase for abbreviated, single-letter or vague names. JSON keys,
  report text and report file names do not change.

## Global Constraints

- Everything in `CLAUDE.md` and the SDD §12 applies. Additional rules: no new packages, no tests, no `[ UsedImplicitly ]`, PowerShell only, and lines of at most 180 characters.
- **The settings JSON keeps the shape it had before commit `2cdf814`.** Its keys are `Server`, `Database`, `Table`, `PathColumn`, `IdColumn`, `ReportEmptyFiles`,
  `ProjectFolder`, `ProjectStage`, `AuditStage`, `UseCustomReportLocation`, `CustomLocation`, `MissingCustomFileName`, `ScanFolders` (each `Path` and `IncludeSubfolders`),
  `FileTypes`, `ExcludeTypes`, `GroupByFoundExtension` and `UnindexedCustomFileName`. Enums are written as strings and the output is indented.
- **No user-visible text changes:** report text, report file names, dialogue copy and window copy stay the same. The exceptions are the two new messages of Task 4.
- **Enum type names stay plural** (`CheckOutcomes`, `DialogueChoices`, …): that is the user's convention. Variables, parameters and properties that hold *one* value of such a type
  have singular names (`CheckOutcomes outcome`, `DialogueChoices choice`).
- **ReSharper suppressions** use the user's form: `// ReSharper disable once <Inspection>` on the line above, or a `disable` / `restore` pair around a statement. A suppression
  goes above a member's doc comment, not between the comment and the signature.

## Review Focus

- **Old project files.** A file with the keys `ProjectStage`, `AuditStage` must load with those stages, not with the defaults.
- **Damaged project files.** A `null` string, bool, array or array element, a folder without a path, or an undefined numeric enum each fall back to its own default. The rest of
  the file still loads.
- **Save round trip.** Saving and reopening a project returns the same settings. The file's keys are those in the Global Constraints, with no `null` written for any of them.
- **Static conversions.** `SettingsStore`, `IndexTableReader` and `FolderWalker` stop being instantiated, and every call site still compiles and behaves the same, including the
  designer constructors.
- **A wrong exhaustive switch.** An enum switch rewritten to the new convention must keep the old arm's behaviour for every member that can occur. Option B of item 14 (an
  empty `Ordinary` case) would silently drop ordinary folders; it must not appear.
- **A burst of errors.** An exception repeated on every 250 ms refresh tick must not stack up dialogues: while one notice is open, more errors are not shown.

---

### Task 1: Settings file model (items 22, 23, 24, 25; JSON key regression)

Items:

- **22.** `SettingsStore.Load`/`Save` can be static.
- **23.** The `??` fallbacks warn "left operand is never null".
- **24.** `NormaliseFolders`/`NormaliseTypes` take nullable collections.
- **25.** `type is null` is "always false".
- **Regression found in review:** commit `2cdf814` renamed `AppSettings.ProjectStage`/`AuditStage` to `ProjectStages`/`AuditStages`, which renamed the JSON keys, so older files
  silently lose both stages.

**Files:**
- Create: `Source/FileAuditor/Models/Settings/ProjectFile.cs` and `Source/FileAuditor/Models/Settings/ProjectFileFolder.cs`
- Modify: `Models/Settings/AppSettings.cs`, `Models/Settings/ScanFolder.cs` (if needed), `Common/Serialisation/SettingsJsonContext.cs`, `Services/Settings/SettingsStore.cs`,
  `Services/Settings/ProjectSettingsService.cs`, `Startup/App.axaml.cs`, `ViewModels/Groups/ProjectSettingsViewModel.cs`, `ViewModels/Main/MainWindowViewModel.cs`, and every
  consumer of `ScanFolders` / `FileTypes` / `ExcludeTypes` that applies `?? []` or `!` (find them with a search for those three names)

**Interfaces:**
- Produces:
  - `sealed record ProjectFile`, the JSON shape. One nullable member per key of the Global Constraints, with the key's exact name (`string?`, `bool?`, `ProjectStages?`,
    `AuditStages?`, `ProjectFileFolder?[]?`, `string?[]?`).
  - `sealed record ProjectFileFolder(string? Path, bool? IncludeSubfolders)`.
  - `AppSettings` with **no nullable members**, which has three parts:
    - non-nullable `ScanFolder[] ScanFolders`, `string[] FileTypes` and `string[] ExcludeTypes`;
    - properties `ProjectStage` and `AuditStage`, typed `ProjectStages` and `AuditStages`;
    - the defaults of today, with `ScanFolders = []`, `FileTypes =` a copy of `FileTypeList.Defaults` and `ExcludeTypes = []`.
  - `AppSettings` is no longer deserialised, so the positional-constructor workaround in its remarks can go. Turn it into a record with `init` properties and initialisers
    if that is cleaner, keeping `AppSettings.Default`.
  - `static class SettingsStore` with `static ErrorOr<AppSettings> Load(string filePath)`, `static ErrorOr<Success> Save(AppSettings settings, string filePath)` and
    `const string FileName`.
  - `ProjectSettingsService` no longer takes a `SettingsStore`.
  - `SettingsJsonContext` serialises `ProjectFile` only.

- [x] **Step 1: Write the model types.** Create `ProjectFile` and `ProjectFileFolder`. Reshape `AppSettings` as above, then rename `ProjectStages`/`AuditStages` (properties) to
  `ProjectStage`/`AuditStage` everywhere.
- [x] **Step 2: Map in `SettingsStore`.**
  - `Load` deserialises a `ProjectFile`, keeps the `LooksLikeProject` check against `ProjectFile`'s keys, and maps the result to `AppSettings`. Each mapping rule is a fallback:
    - a `null` value takes `AppSettings.Default`'s value;
    - an undefined enum value takes its default;
    - a `null` `ScanFolders` becomes `[]`, and a `null` element or one with a `null` path is dropped;
    - a `null` `IncludeSubfolders` becomes `true`;
    - a `null` `FileTypes` takes a copy of the defaults, and a `null` `ExcludeTypes` becomes `[]`;
    - a `null` type entry is dropped, a valid one is normalised, and an invalid one is kept trimmed, as today.
  - `Save` maps `AppSettings` to a `ProjectFile` and serialises that.
  - The normalising helpers take the nullable *file* types, which is honest now, so no warning remains.
- [x] **Step 3: Remove the workarounds.** Make `SettingsStore` static and update its three construction sites. Remove every `?? []` and `!` that existed only because the three
  lists were nullable.
- [x] **Step 4: Build.** Run `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx`. Expected result: 0 errors and 0 warnings.
- [x] **Step 5: Verify in the app.** Write each project file below in the scratch folder, open it with *File ▸ Open Project* through the UIA `ValuePattern` file-name box, and read
  the fields back through UIA:
  - (a) An old-format file with `"ProjectStage": "Sample"` and `"AuditStage": "Final"`. Both stages show.
  - (b) A file with `"Server": null`, `"ReportEmptyFiles": null`, `"FileTypes": [null, ".pdf", "bad<"]` and `"ScanFolders": [null, {"Path": null}, {"Path": "<scratch>"}]`. Every
    other field loads; the types are `.pdf` and `bad<`; one folder remains.
  - (c) Save the project with *Save project settings* ticked, read the written JSON, and check its keys against the Global Constraints.
- [x] **Step 6: Commit.** Use `fix(🐛): read project files through a nullable file model and restore the ProjectStage and AuditStage keys`.

### Task 2: Analyser and inspection fixes (items 2, 3, 4, 11, 13, 16, 17, 18, 19, 26–31)

**Files:**
- Modify:
  - `Services/Auditing/AuditProgress.cs`, `AuditRunner.cs` and `UnindexedAuditRunner.cs`;
  - `Services/Sql/IndexTableReader.cs`;
  - `Services/Scanning/FolderWalker.cs`, `IndexPathSet.cs` and `FileTypeList.cs`;
  - `ViewModels/Main/MainWindowViewModel.cs`.

**Interfaces:**
- Produces: `static class IndexTableReader` and `static class FolderWalker`, with the same method signatures, now static. `AuditRunner` and `UnindexedAuditRunner` lose their
  `_reader` and `_walker` fields.

- [x] **Step 1: Remove dead members.**
  - **2.** Remove `AuditProgress.Processed`.
  - **19.** Remove `IndexPathSet.Count`.
  - Confirm that neither is referenced in C# or XAML.
- [x] **Step 2: Make the stateless services static.**
  - **13, 26–28.** Make `FolderWalker` and `IndexTableReader` static classes and update their callers.
  - `FolderWalker.MaxConcurrentListings` may stay `public const`.
- [x] **Step 3: Add the closure suppressions.** Both are safe, for different reasons:
  - **3.** The producer task is always awaited before `linked` is disposed. The normal path awaits `producer`, and the failure path awaits `Task.WhenAny(producer)` before
    rethrowing. Suppress with exactly:
    ```csharp
    // ReSharper disable AccessToDisposedClosure
    readResult = await IndexTableReader.ReadAsync(connectionString, query, row => channel.Writer.WriteAsync(row, linked.Token), linked.Token);
    // ReSharper restore AccessToDisposedClosure
    ```
  - **16.** `Task.WhenAll(workers)` finishes every worker before `linked` is disposed. Use the same disable/restore pair around the `Task[] workers = …` statement in
    `FolderWalker`.
- [x] **Step 4: Fix the `FolderWalker` inspections.**
  - **17.** `Entry.IsReparsePoint` hides the outer `IsReparsePoint(string)` method. Rename the outer method to `HasReparsePointAttribute(string path)`, keeping the record
    member's name.
  - **18.** `Queue.Add(folder)` in `Enqueue` stays without a token. The queue is unbounded, so `Add` never blocks. A token would only throw `OperationCanceledException` from
    the first `Enqueue` calls, which run before the workers start. That would break `WalkAsync`'s promise not to throw on cancellation, and would leave `_pending` counted
    for a folder that was never queued. Add `// ReSharper disable once MethodSupportsCancellation` with a one-line `//` reason.
- [x] **Step 5: Fix the `MainWindowViewModel` inspections.**
  - **29.** The CommunityToolkit generator declares `partial void OnIsRunningChanged(bool value)` and `partial void OnSelectedTabIndexChanged(int value)` with that parameter,
    and there is no parameterless overload. Put `// ReSharper disable once UnusedParameterInPartialMethod` above each method's doc comment.
  - **30.** Declare `string? error;` without the unused `= null`. `status` keeps its initialiser, because the `finally` block reads it.
  - **31.** In `ConfirmCloseAsync` and `ProbeReportAsync`, replace `_auditCts?.Cancel()` and `_probeCts?.Cancel()` with an awaited `CancelAsync()` on a non-null token source.
    That also moves the cancellation callbacks, including `SqlCommand.Cancel`, off the UI thread. The synchronous `Stop()` command is left as it is.
- [x] **Step 6: Fix `FileTypeList`.** **11.** Change `if (normalised is null)` to `if (string.IsNullOrWhiteSpace(normalised))`. `Normalise` returns `null` or `"." + entry`, so this
  changes nothing at runtime, and `IsNullOrWhiteSpace`'s `NotNullWhen(false)` keeps `normalised` non-null after the check.
- [x] **Step 7: Check item 4.** Confirm that `PreflightChecker` declares `ScanFolder[] folders`, which the user already changed, and that `FindFolderProblems` still takes
  `IReadOnlyList<ScanFolder>`.
- [x] **Step 8: Build and run.** Build with 0 warnings. Then run one Missing audit and one Unindexed audit on LocalDB scratch data, using a scratch folder as the scan input. Both
  must finish with the expected counts.
- [x] **Step 9: Commit.** Use `refactor(♻️): make the stateless services static and clear the analyser warnings`.

### Task 3: One switch convention (items 7, 14, 20, 21)

**The convention, added to the SDD §12 in Task 8.** A switch over one of the app's enums names every member it handles in its own arm, and its default arm throws
`UnreachableException`. A default arm never stands in for a real member, and no enum is shown to the user through `ToString()`. The one exception is `AuditDirections`, whose member
names are the report file names, as its remarks document.

**Files:**
- Modify:
  - `Services/Reporting/ReportBuilder.cs`, `UnindexedReportBuilder.cs` and `ReportFormat.cs`;
  - `Services/Auditing/AuditProgress.cs`;
  - `Services/Scanning/FolderWalker.cs`;
  - `Services/Settings/ProjectSettingsService.cs`;
  - `ViewModels/Main/AuditRun.cs` and `MainWindowViewModel.cs`;
  - any other switch over the app's enums found by searching for `switch`.

- [ ] **Step 1: Fix the report switches.**
  - **7.** `ReportBuilder.GroupTitle` gets `var _ => throw new UnreachableException()`. The arm is unreachable: the groups are built only from `MissingOrder` and
    `UnverifiedOrder`, and nothing in the Missing audit produces `UnverifiedLinkNotFollowed`. Do the same for `UnindexedReportBuilder.GroupTitle` and `GroupOf`.
  - `ReportFormat.ResultText` gets an explicit `Failed` arm.
  - `AddProjectLines` stops using `ToString()` for the *Project stage* and *Audit stage* lines. Add explicit label switches that give the same text (`Sample`, `Final`,
    `Interim`).
- [ ] **Step 2: Fix the `FolderWalker` switches.** **14.** Option A is the correct one of the user's two. Option B would drop every ordinary entry, because its empty `Ordinary`
  case breaks out without adding the folder. Do not use either at the three call sites. Instead, add one helper and call it from all three:
  ```csharp
  // Reports a link, or an entry whose link status could not be read; returns true only for an ordinary entry, which the caller then walks or reports.
  private static bool IsOrdinary(LinkStates state, string path, string? failure, Action<UnverifiedFolder> onUnverified)
  ```
  The helper uses an exhaustive switch, with `default: throw new UnreachableException()`. Each call site becomes one `if (IsOrdinary(...))` statement around its single action:
  `pathRoots.Add`, `_onFile` or `Enqueue`.
- [ ] **Step 3: Fix the `ProjectSettingsService` switches.**
  - **20.** In the report-folder prompt, `Tertiary` returns `SavePointResult.Cancel` on its own arm, and `default` throws `UnreachableException`.
  - **21.** In the overwrite prompt, `Primary` gets its own arm (overwrite), and `default` throws.
  - Apply the same to the third prompt, the *cannot save* one, if it switches on the choice.
- [ ] **Step 4: Sweep the remaining switches.**
  - `AuditProgress.Record` lists the eight `Unverified*` members explicitly, and `default` throws.
  - Bring `AuditRun.DescribeResult`, `MainWindowViewModel` (lines ~121, ~322, ~330) and any other enum switch to the convention.
  - Switches over BCL values, such as `Win32ErrorMapper`'s error codes or `PathText`, are not covered by the convention and stay as they are.
- [ ] **Step 5: Build and run.** Build with 0 warnings. Then:
  - run a Missing audit that has missing, unverified and skipped rows, and a stopped run, and compare their reports with ones made on `main` from the same data;
  - check that the reports match apart from the timestamps and the elapsed time.
- [ ] **Step 6: Commit.** Use `refactor(♻️): name every enum member in switches and make the default arms unreachable`.

### Task 4: Type locations, global exception handling and `async void` handlers (items 5, 6, 32, 38; global handler requested 2026-11-08)

**Rulings:**

- **Item 5.** Plain data records move to `Models/`; types that carry behaviour stay where they are.
  - **Move:**
    - `AuditRequest` and `MappedOutcome` (still `internal`) go to `Models/Auditing/`;
    - `ReportPath` goes to `Models/Reporting/`;
    - `SavePointResult` goes to `Models/Settings/`;
    - `ProjectFilePick` goes to a new `Models/Dialogues/`.
  - **Stay:**
    - `FileTypeList`: a static parser and formatter, like `QueryBuilder`.
    - `FileTypePattern`: a compiled matcher, like `FileTypeFilter`.
    - `IndexPathSet`: a hashing data structure used only by the scan.
    - `FileTypeEntryItem`: an `ObservableObject` row of the editor, the same kind as `ScanFolderItem`, so it stays in `ViewModels/Dialogues/`.
- **Item 6.** `EditFileTypesAsync` keeps returning `string[]?`, because `null` (cancelled) and `[]` (accepted with every entry removed) mean different things. The *Exclude*
  list may legitimately be emptied. The return's doc comment says so in one sentence.
- **Global exception handler (the user's request).** Nothing handles unexpected exceptions today. The user's rule: as far as possible nothing crashes the app. An error the app
  can survive is shown to the user, and the app carries on. One it cannot survive is explained to the user before the app closes.
  - **Recoverable.** Any exception reaching `Dispatcher.UIThread.UnhandledException`: async `void` continuations, `[ RelayCommand ]` async commands, bindings and timer ticks.
    It is marked handled and shown in the main window's error dialogue as `Something unexpected went wrong:\n<message>\n\nFile Auditor will carry on.` Any exception
    reaching `TaskScheduler.UnobservedTaskException` is marked observed and shown the same way, posted to the UI thread.
  - **Fatal.** Three cases:
    - an exception reaching `AppDomain.CurrentDomain.UnhandledException` (the runtime ends the process regardless);
    - one that escapes `StartWithClassicDesktopLifetime`;
    - an `OutOfMemoryException` on any route.

    These show `File Auditor ran into a problem it cannot recover from and has to close.\n\n<message>` and then the process exits.
  - The fatal message uses a Win32 `MessageBoxW` through P/Invoke (`user32.dll`, error icon, title `File Auditor`), because it can arrive on any thread, after the
    dispatcher has died. It is not a package.
  - **One notice at a time.** While a recoverable notice is open, further errors are dropped, not queued. If showing a notice throws, or there is no main window yet, the
    Win32 message box is used instead.
  - **Nothing is written to disk.** The SDD's storage rule forbids writing anything beside the executable or under `%APPDATA%`, so the handler keeps no crash log.
  - **Where it lives.** One static class, `Startup/UnexpectedErrorHandler.cs`, with:
    - `Install(IClassicDesktopStyleApplicationLifetime desktop)`, called from `App.OnFrameworkInitializationCompleted`;
    - `ReportRecoverable(Exception exception)`;
    - `ReportFatalAndExit(Exception exception)`, which `Program.Main` calls from a `try`/`catch` around the lifetime.

**Files:**
- Create: `Startup/UnexpectedErrorHandler.cs`.
- Move: the five records above, changing their namespaces, and update `Common/GlobalUsings.cs` if a namespace is new.
- Modify:
  - `Startup/Program.cs` and `Startup/App.axaml.cs`;
  - `Views/Dialogues/FileTypesDialogueWindow.axaml.cs`, `Views/Groups/ReportView.axaml.cs` and `Views/Tabs/UnindexedFilesTabView.axaml.cs`;
  - `Services/Dialogues/DialogueService.cs` (the doc line only).

- [ ] **Step 1: Move the records.** Use `git mv`, so each move is a pure rename. Then update each record's namespace.
- [ ] **Step 2: Write `UnexpectedErrorHandler`.** Install it from `App` and `Program` as ruled above.
- [ ] **Step 3: Wrap the click handlers.** **32 and 38.** All three `async void` handlers get a `try`/`catch (Exception exception)` around their body:
  `FileTypesDialogueWindow.OnOkClick`, `ReportView.OnBrowseClick` and `UnindexedFilesTabView.OnAddClick`. The `catch` calls
  `UnexpectedErrorHandler.ReportRecoverable(exception)`, so no `async void` method lets an exception escape.
- [ ] **Step 4: Build and run.** Build with 0 warnings. Then:
  - Open the File Types editor, and accept it, cancel it, and accept it with an invalid entry.
  - Open the report folder Browse dialogue and the Add folder dialogue, and cancel each without reading its tree.
- [ ] **Step 5: Exercise the handler.** Make each check below with a temporary source edit, then revert the edit. Screenshot each message, and confirm with `git diff` that nothing
  is left behind.
  - (a) A throw inside the Stop command while idle, through its menu item. The recoverable notice appears, and after OK the app still works.
  - (b) A throw in the audit refresh tick. One notice appears, not a stack of them, and the app carries on.
  - (c) A throw on a plain background `Thread`. The fatal message appears, and the process then exits.
  - (d) A throw in `App.OnFrameworkInitializationCompleted`. The fatal message appears.
- [ ] **Step 6: Commit.** Use `feat(✨): add a global exception handler, move the plain records to Models and guard the async click handlers`.

### Task 5: XAML inspections (items 33–44)

**Files:**
- Modify:
  - `Views/Groups/AuditView.axaml`, `DatabaseView.axaml`, `FileIndexTableView.axaml`, `ReportView.axaml` and `UnindexedAuditView.axaml`;
  - `Views/Main/MainWindow.axaml`;
  - `Views/Tabs/MissingFilesTabView.axaml` and `UnindexedFilesTabView.axaml`.
- Item 36 names `AuditView.axaml` again, but its contents (a `TextBlock` and two `Label`s) match `FileIndexTableView.axaml`. Check both files.

- [ ] **Step 1: Add the missing `Grid.Row` setters.** **33, 35–37, 39 and 43.** Every child of a multi-row `Grid` in row 0 gets an explicit `Grid.Row="0"`.
- [ ] **Step 2: Add fallback values to the parent bindings.** **34, 40, 41, 42 and 44.** Each `$parent[Window].((…)DataContext).X` binding gets a `FallbackValue`:
  - `IsEditable` bindings get `FallbackValue=True`. A detached control cannot be clicked anyway, and the XAML designer keeps showing the groups enabled.
  - `Command` bindings get `FallbackValue={x:Null}`.
- [ ] **Step 3: Build and run.** Build with 0 warnings. Run the app and screenshot both tabs while idle and while running, to confirm the layout is unchanged.
- [ ] **Step 4: Commit.** Use `style(🎨): set every grid row explicitly and give the parent bindings fallback values`.

### Task 6: Naming pass (items 12, 15)

**Files:** any C# or XAML file, but not the JSON keys or report text of the Global Constraints.

- [ ] **Step 1: Make single values singular.** Every variable, parameter or property that holds one value of a plural-named enum gets a singular name. These include:
  - `CheckResult.Outcomes` → `Outcome`, and `MappedOutcome.Outcomes` → `Outcome`;
  - `AuditProgress.Record(outcomes)`;
  - `ReportBuilder`'s `outcomes` parameters;
  - `MessageDialogueViewModel.Icons` → `Icon`, and `DialogueService.ChooseAsync(icons)` → `icon`;
  - `MessageDialogueWindow.Choices` → `Choice`, and every `DialogueChoices choices` → `choice`;
  - `UnindexedReport.Kinds` → `Result`, to match `AuditReport.Result`, and `AuditResultKinds kinds` → `result`;
  - `AuditDirections directions` → `direction`, and `ReportViewModel.Directions` → `Direction`;
  - `AuditStages auditStages` → `auditStage`.
  - Update the XAML bindings that use any renamed property.
- [ ] **Step 2: Fix the glob names.** **12.** In `FileTypePattern.GlobMatches`, rename `p` → `patternIndex`, `t` → `textIndex`, `star` → `starPatternIndex` and `starText` →
  `starTextIndex`.
- [ ] **Step 3: Sweep the rest of the code.** Go through every file for names that are abbreviated, a single letter (outside lambda parameters such as `_`), vague (`own`, `sub`,
  `raw`, `info`, `cts`-style suffixes on locals) or that no longer describe what they hold.
  - Rename each to say what it holds.
  - Keep the names that are already clear, and keep British English.
  - List every rename in the task report.
  - Use Rider's rename refactoring when it is connected; otherwise edit and build.
- [ ] **Step 4: Build and run.** Build with 0 warnings. Run both audits and open, save and close a project.
- [ ] **Step 5: Commit.** Use `refactor(♻️): give single enum values singular names and make the remaining names descriptive`.

### Task 7: Doc comments everywhere (items 8, 9, 10)

**Files:** every `.cs` file under `Source/FileAuditor/`.

- [ ] **Step 1: Strip the pointers.** **9.** Remove every pointer to the SDD, a spec, a milestone or a `§` section from doc and `//` comments. Rewrite each sentence so it still
  reads whole. A search for `SDD`, `spec`, `milestone` and `§` must find nothing in the source.
- [ ] **Step 2: Document every member.** **10.** Every type and member, whatever its access modifier, gets an XML doc comment:
  - classes, records, structs, enums and their members, nested types, fields, constants, properties, constructors, methods and event handlers;
  - generated-property partial declarations and the `.axaml.cs` code-behind as well;
  - `<param>`, `<returns>` and `<typeparam>` where they apply.
  - A `//` comment that describes a whole member becomes its doc comment; `//` notes inside a body stay as they are.
- [ ] **Step 3: Fix `LabelWidth`'s comment.** **8.** Its doc must state what the value is: the column, 17 characters from the left, at which every value starts. That is the
  longest label (`Project folder:`, 15 characters) plus a two-space gap. Check the 15 against both report builders' labels before writing it.
- [ ] **Step 4: Build.** Build with 0 warnings and confirm that no pointer search hits remain.
- [ ] **Step 5: Commit.** Use `docs(📝): document every type and member and drop the pointers to the design documents`.

### Task 8: SDD sync and ledger

**Files:** `Docs/Specs/1-file-auditor-master-design.md`, `Docs/Ledgers/Epic-1-MVP/milestone-6-ledger.md` and `MediQuant.FileAuditor.slnx`.

- [ ] **Step 1: Bring §9 up to date.**
  - The enums now live in `Common/Constants/<Area>/` with plural names, and the folder list must say so; today it still places them in `Models/` and `ViewModels/Dialogues/`.
  - Record the moved records, `ProjectFile`/`ProjectFileFolder` and the static services.
  - Rename the singular type names it quotes: `AuditDirection` → `AuditDirections`, `DialogueChoice` → `DialogueChoices`, and so on.
- [ ] **Step 2: Update §8 and §9.**
  - In §8, a `null` value of any kind falls back to its default, field by field.
  - In §9 *Errors*, describe the global exception handler: which exceptions are recoverable and which are fatal, both message texts, one notice at a time, and no crash log.
- [ ] **Step 3: Update §12.** Record the conventions:
  - doc comments on **every** type and member;
  - no pointers to design documents in code;
  - the switch convention;
  - plural enum type names with singular value names;
  - stateless services as static classes;
  - `async void` handlers wrapped in `try`/`catch`;
  - the ReSharper suppression form.
- [ ] **Step 4: Write the ledger and register it.** Write the ledger with the rulings above and add it to the `.slnx`.
- [ ] **Step 5: Commit.** Use `docs(📝): sync the SDD with the code review clean-up and close the milestone 6 ledger`.
