# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**File Auditor**: a native Windows desktop utility for MediQuant projects. It reads a SQL Server **file index table** (a path column and an optional ID column), checks
that every file the table points to exists on disk (and, optionally, is not empty), and writes a plain-text **report** — summary statistics, then *Missing* and *Unverified*
files grouped by reason — that is committed to the project's version-control repository. Windows-only; .NET 10, Avalonia 11.3 + Classic.Avalonia theme (Windows 95/98
aesthetic), laid out to standard Win9x conventions.

File Auditor is a rewrite of the old WinForms .NET Framework tool *File Checker*, whose source is kept under [`OriginalSource/`](./OriginalSource/) as **read-only
reference**. Read it to understand the original behaviour; never edit it, build it, or copy-paste from it. The rewrite deliberately departs from it (no hand-written SQL,
optional ID, integrated security only, per-user JSON settings, a grouped report instead of a delimited log, a new UI) — the SDD is the contract, not the old code.

The whole app is **one project**, [`Source/FileAuditor/`](./Source/FileAuditor/), organised by folder. There are no test projects and no automated tests: do not add any.

## Authoritative documents

Work is organised into **epics** (e.g. `Epic-1-MVP`) made of numbered **milestones**. A milestone has a plan, sometimes a design spec, and a ledger, all sharing the same
`milestone-<N>-<feature-name>` stem. Number milestones in creation order within the epic; a follow-up to milestone `<N>` that needs its own document is `<N>.1`, `<N>.2`, …

- **SDD (authoritative spec):** [`Docs/Specs/1-file-auditor-master-design.md`](./Docs/Specs/1-file-auditor-master-design.md). When the SDD and any other document
  disagree, the SDD wins. **Keep it current:** when a milestone changes behaviour, the window layout, the report format, the settings or the folder layout, amend the SDD in
  the same milestone — never let the code and the spec drift apart.
- **Milestone specs:** under `Docs/Specs/<Epic>/`, named `milestone-<N>-<feature-name>-design.md`, matching the plan it designs, with **no date prefix**; the date lives
  inside the file as a `**Date:** YYYY-MM-DD` line. The master SDD is the one spec that sits directly under `Docs/Specs/` rather than in an epic folder. Not every milestone
  needs a spec.
- **Milestone plans:** under `Docs/Plans/<Epic>/`, named `milestone-<N>-<feature-name>.md` with **no date prefix**; the date lives inside the file as a
  `**Date:** YYYY-MM-DD` line under the title. Each plan is an executable task list. **Tick each task's checkbox (`- [ ]` → `- [x]`) in the plan file as it is completed**,
  as part of working the plan — not in a clean-up pass afterwards.
- **Plan ledgers:** under `Docs/Ledgers/<Epic>/`, named `milestone-<N>-ledger.md`, one per plan. Write the ledger *while* executing — it is the recovery map if context is
  lost — and clean it into a readable document when the plan finishes. It must record, at minimum: every **ruling** taken without the user and what it costs if wrong;
  **defects found in the plan itself**; **outstanding items** grouped by when they should be revisited; and any **incidents**.
- **Keep the solution view in sync:** [`MediQuant.FileAuditor.slnx`](./MediQuant.FileAuditor.slnx) lists every file under `Docs/` (in the top-level `/Docs/…` solution
  folders, which mirror the on-disk path — never under `/SolutionItems/`) and the root-level files under `/SolutionItems/`. **Whenever such a file is created, renamed,
  moved or deleted, update the `.slnx` to match** in the same commit, keeping milestone documents in milestone order. The user reads these documents through Rider's
  solution view, where an unregistered file is invisible; Rider's auto-sync is a convenience, not a guarantee.
- **Design reference:** the approved HTML mock-ups under [`Docs/UserInterfaceDesign/Mockups/`](./Docs/UserInterfaceDesign/Mockups/) —
  `main-window-layout.html` (the whole window mid-run) and `report-section-states.html` (the Report group's four states). Open them in a browser when building or changing a
  view. The SDD (§5), not the mock-ups, is the contract; where they differ (e.g. the Audit Stage default), the SDD wins.

## Executing a plan

Drive every plan with `superpowers:subagent-driven-development`. **Default to it without asking about the approach**; ask only for a "go" / "stop" to start.

- **Start on a feature branch.** When a plan starts and it doesn't already have a feature branch, create one named `feat/<milestone plan name>` — the plan's file stem, e.g.
  `feat/milestone-1-rewrite` for `milestone-1-rewrite.md`. If the current branch has uncommitted work, stop and ask the user whether to carry it over onto the new
  branch, or to commit and push it first.
- **Models:** dispatch implementers (including fix-round re-dispatches) on the latest version of **Sonnet** and every advisory or review seat — task reviewers, scoped
  re-reviews, the final whole-branch review, any advisor or escalation — on the latest version of **Opus**. Always name the model explicitly on dispatch; an omitted model
  silently inherits the session's.
- **Commit each task automatically** as it completes. Do not ask for per-task approval, and do not wait for a review of the work before committing — the task review happens
  against the committed diff. Say so in every implementer dispatch, or implementers following *Git commits* below will leave their work uncommitted.
- **Run semi-silently.** Do not narrate each task. Surface only: a real problem, a decision that needs the user's judgement, and anything the user must act on themselves.
  **Report once, at the end**, with what was built, the commits, the build position, and the rulings taken on their behalf.
- **Verify by building and by using the app.** There are no tests, so a task is verified by a clean build plus, for anything visible, running the app
  (`dotnet run --project Source/FileAuditor/FileAuditor.csproj`, or the `run` skill) and driving it as a user would — clicking through states and dialogues with computer use
  (desktop control, the `mcp__computer-use__*` tools) and taking screenshots — without asking first. Close the app when done: a running app holds the build output (see
  *Build file-lock errors*) and counts as background work left running. There is no sample database in the repository; when a flow needs SQL Server and none is reachable,
  verify the UI states, pre-flight checks and dialogues, and say plainly in the ledger what could not be exercised.
- **Fix review follow-ups before the PR.** After the final whole-branch review, run further reviewed fix waves for defects, polish, stale docs, mock-up mismatches and spec
  text that contradicts the implementation, rather than shipping them as a "known gaps" list. Stop once a wave's re-review has no Critical or Important findings. Only
  genuinely new features stay as follow-ups — they need their own milestone.
- **Finish with push + PR.** When the run is done on a feature branch, push the branch and open a pull request without asking — don't present the branch-completion menu.
- **Stop and ask** for anything else irreversible or outward-facing (a merge, a push to `main` or another shared branch, a publish), a security-sensitive action, or a plan
  defect with no sane path forward. Everything else is a ruling: decide it, record it in the ledger, keep going.
- **Never let a subagent leave background work running.** Prohibit background commands and filesystem-wide searches (`Get-ChildItem C:\ -Recurse`, `find /`) in every
  dispatch; a task notification saying an agent "stopped with background work of its own still running" is a signal to investigate, not boilerplate.

## Common commands

> **Always run shell commands via PowerShell — never Bash.**

- **Build:** prefer the Rider MCP build tool when it is connected. Fallback: `dotnet build MediQuant.FileAuditor.slnx`.
- **Run:** `dotnet run --project Source/FileAuditor/FileAuditor.csproj`.
- **Validate task completion:** clean, then build. `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx`. The build must finish with no
  errors and no new warnings.

## Build file-lock errors

Builds may fail with a **file lock error** (an output assembly or DLL that cannot be written because it is "in use"). Almost 10 times out of 10 this is **not** because the user has
the app running — it is an artefact of the **Avalonia Preview plugin in Rider** holding a handle on the built output. Do the following, in order:

1. **Check for your own process first.** If you launched the app earlier in the task, make sure you closed it.
2. **Otherwise, assume the preview plugin.** Do not kill processes you did not start.
3. **When unsure, stop and ask the user.** If you cannot safely determine whether the lock can be ignored, ask — the user can resolve or clarify it quickly.

**Never** get sidetracked troubleshooting the lock, running excessive diagnostic commands, or looping on retries. This is a known, benign artefact; a single question to the user is
always cheaper than an investigation.

## Git commits

Use the **conventional commit** format with a **gitmoji** in the type scope: `type(emoji): subject`.

Type→gitmoji map: `feat`→✨, `fix`→🐛, `refactor`→♻️, `style`→🎨, `docs`→📝, `chore`→🔧, `perf`→⚡, `build`→📦, `ci`→👷.

Commit per logical task, not per file.

**During a plan run, commit each task automatically** (see *Executing a plan*). **Outside a plan run, after each completed task, automatically ask the user whether to
commit** (via `AskUserQuestion` or an inline yes/no): propose the conventional-commit message you would use, list the files that would be staged, and wait for explicit approval
before running `git commit`. Either way, stage only the files the task touched; never `git add -A` over a tree you have not inspected.

**Merging a pull request** (only once the user says to merge) uses a **merge commit**, never a squash by default:
`gh pr merge <N> --repo JustinianErdmier/MediQuant.FileChecker --merge --subject "feat(🔀): merge pull request #<N>"`. A merge commit keeps the branch's commits —
the ones the milestone ledger cites — reachable from `main`, and keeps pure-rename commits separate so Git's rename detection and `git log --follow` stay reliable. Then
check out `main`, bring it up to date, and delete the feature branch locally and on the remote. **Squash is reserved for special cases when needed** (for example, a branch
whose commit subjects need tidying) and only when the user asks for it.

## Architecture (summary)

A single WinExe project, `FileAuditor` (root namespace `FileAuditor`), organised by folder with namespaces mirroring folders (SDD §9):

- `Views/` — `MainWindow`, `AboutWindow`, and small `UserControl`s per group box (Database, File Index Table, Report, Progress).
- `ViewModels/` — one view model per view (CommunityToolkit.Mvvm).
- `Services/` — settings store, query builder, file checker, audit runner, report writer, dialogue and launcher helpers.
- `Models/` — the settings record, index rows, outcome enumerations, audit results.
- `Common/Errors/` — the custom ErrorOr `Error` definitions; `Common/Extensions/` for wiring and helpers.

Services return `ErrorOr<T>`; expected failures (bad input, SQL errors, unwritable report, unreadable settings) are values with custom errors, not exceptions. Exceptions
are caught at the boundary where they occur (SQL, file system) and translated into errors or check outcomes. SQL access is plain `Microsoft.Data.SqlClient` — no ORM.
The connection string always uses `Integrated Security=True; TrustServerCertificate=True`; there are no credentials anywhere.

Storage: per-user settings in `%APPDATA%\MediQuant\FileAuditor\settings.json` (System.Text.Json, source-generated context). The app writes nothing beside its executable —
it runs from a shared file server.

Presentation: Avalonia 11.3 + Classic.Avalonia (Windows 95/98 chrome) + CommunityToolkit.Mvvm, on a fixed-size main window. `MainWindow.axaml` composes the group-box views
and holds the menu bar, command row and status bar; keep it at ≤ 100 lines by pushing content into the group-box `UserControl`s.

Not used, on purpose: Mediator/CQRS, FluentValidation, EF Core/Dapper, Serilog, `System.IO.Abstractions`, test projects. Do not add them, or any other package, without asking.

## Win9x UI conventions

The window must look and behave like a native Windows 95/98 utility. Follow these, and the SDD §5, for every view:

- **Group boxes** organise related inputs; labels sit to the left of their inputs and end with a colon.
- **Access keys** on every label, input, button and menu item (underlined letter, `_` prefix in XAML), unique within a window. Tab order runs top to bottom, left to right.
- **Command buttons** are 75 × 23 minimum, laid out horizontally; the action comes first, the escape after it (*Start* then *Stop*, *OK* then *Cancel*, *Overwrite* then
  *Cancel*).
- **Ellipsis (`...`)** only on a command that needs more input before it completes (e.g. *Browse...*). A command that acts immediately — including opening a file in
  another program, like *View Report* — has none.
- **Menus**: *File* ends with *Exit*; *Help* ends with *About File Auditor*. Menu items mirror the buttons and enable and disable with them.
- **Dialogues** are modal, fixed-size, with an icon (error / warning / information) and buttons along the bottom right.
- **No modern affordances** that Win9x lacked: no rounded corners, no flyouts, no toast notifications, no accent colours, no hamburger menus.

## Conventions

- **One type per file. No exceptions.** Class, record, struct, interface, enum, delegate each in its own file, named after the type, in a folder mirroring the namespace.
- **No code files at the project root.** Every code and XAML file lives under a subfolder mirroring its namespace (e.g. `App` and `Program` under `Startup/` — not
  `Application/`, whose namespace would shadow Avalonia's `Application` type). Only the `.csproj` and `app.manifest` sit at the root.
- **`GlobalUsings.cs`** (under `Common/`) consolidates `using` directives. Keep a local `using` only when truly necessary (namespace conflict, alias).
- **File-scoped namespaces** everywhere.
- **British English everywhere** — code comments, XML doc text, UI copy, identifier wording where there's a choice (`Initialise`, `Behaviour`, `Colour`, `Dialogue`),
  commit messages, Markdown docs. Don't switch a US spelling already established by an external API surface (`System.IO`, `Color`, `Dialog`, etc.).
- **Markdown hard-wrap at 180 characters.** When writing *or* editing any Markdown file, manually wrap prose at a maximum of 180 characters per line. The user's
  IDE hard-wraps at 180 but does not auto-repair the stray paragraph breaks that unwrapped edits leave behind, so emitting pre-wrapped Markdown saves a manual
  clean-up every time. Wrap prose and bullet text; leave fenced code blocks, tables, headings, and bare URLs intact on their own lines (never break a URL). To *verify* the wrap,
  measure line length with PowerShell (`[string].Length`) — not `awk length` or `wc -c`, which count bytes, so this repo's `—`, `×`, `…` (multi-byte UTF-8) inflate the count and
  raise false over-limit hits.
- **XML doc comments on every public member and type** (`///`-prefixed `<summary>`, `<param>`, `<returns>`, `<remarks>`, `<exception>` as appropriate). Plain `//` comments are for
  inline implementation notes only.
- **`<c>…</c>` tags carry no inside whitespace.** Write `<c>settings.json</c>`, never `<c> settings.json </c>`. Same applies to `<code>…</code>`.
- **Spaced bracket style:** `[ STAThread ]`. Match the established style.
- **Vertical spacing:** separate consecutive statements of different kinds with a blank line (e.g. an object instantiation followed by a method call, or a local declaration
  followed by a member/static invocation). Keep like statements grouped, with blank lines between groups.
- **UTC internally, local at the boundary.** Timestamps are captured and carried in UTC (`Utc` suffix on names); convert to local time only for display and for the report.
- **Source-generated MVVM:** `[ObservableProperty]` on `partial` properties, `[RelayCommand]` on private async methods. Don't hand-roll INPC.
- **Compiled bindings:** every XAML file declares `x:DataType`.
- **Designer constructors:** view models expose a parameterless constructor that delegates to `file`-scoped null-object implementations for the XAML designer only.
- **File writes that touch user data** (settings, the report) use the temp-file + `File.Move(overwrite: true)` pattern. Don't write in place.
- **Never `File.Exists` / `Directory.Exists` for the audit itself.** They return `false` for every failure and would turn "couldn't check" into "missing". Use APIs that
  surface the error and map it to an outcome (SDD §6.3).
- **Don't add `[ UsedImplicitly ]` proactively.** JetBrains.Annotations may be referenced, but the user adds the attribute themselves when ReSharper/Rider actually complains.
  Don't speculatively mark designer-only types, DI-resolved types, or anything else that "looks dead but isn't" — and when a plan or spec shows `[UsedImplicitly]`, omit just
  that attribute and copy the rest of the type verbatim. If an analyser warning explicitly demands it on a specific member, propose it inline and let the user decide.
- **View composition pair rule:** every public `*ViewModel` class has a sibling `*View.axaml` file under the parallel `Views/…` folder, and vice versa. `MainWindow` and
  `AboutWindow` (with `MainWindowViewModel` / `AboutWindowViewModel`) are the documented exception to the `*View` suffix.
