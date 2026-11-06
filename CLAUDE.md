# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository. It holds repository context and rules for Claude only. **Every
design and code decision — behaviour, UI, report format, settings, architecture, packages and code conventions — lives in the SDD**, and is not repeated here.

## Project

**File Auditor**: a Windows desktop utility for MediQuant projects that checks every file a SQL Server file index table points to exists on disk, and writes a
plain-text report for the project's repository. .NET 10, Avalonia 11.3 + Classic.Avalonia (Win9x look). See the SDD §1–§3 for the what and why.

- **One project**, [`Source/FileAuditor/`](./Source/FileAuditor/). There are no test projects and no automated tests: do not add any.
- [`OriginalSource/`](./OriginalSource/) holds the old WinForms tool *File Checker* as **read-only reference**. Read it to understand the original behaviour; never
  edit it, build it, or copy-paste from it. The SDD is the contract, not the old code.
- **Do not add any package** without asking the user.

## Authoritative documents

Work is organised into **epics** (e.g. `Epic-1-MVP`) made of numbered **milestones**. A milestone has a plan, sometimes a design spec, and a ledger, all sharing the same
`milestone-<N>-<feature-name>` stem. Number milestones in creation order within the epic; a follow-up to milestone `<N>` that needs its own document is `<N>.1`, `<N>.2`, …

- **SDD (authoritative spec):** [`Docs/Specs/1-file-auditor-master-design.md`](./Docs/Specs/1-file-auditor-master-design.md). Read the sections you touch before
  writing code: §5 (window and Win9x conventions), §9 (code shape, errors, storage) and §12 (code conventions) apply to every change. When the SDD and any other document
  disagree, the SDD wins. **Keep it current:** when a milestone changes behaviour, the window layout, the report format, the settings, the folder layout or a
  convention, amend the SDD in the same milestone — never let the code and the spec drift apart. Record new design or code decisions there, not in this file.
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
- **Design reference:** the approved HTML mock-ups under [`Docs/UserInterfaceDesign/Mockups/`](./Docs/UserInterfaceDesign/Mockups/). Open them in a browser when
  building or changing a view. They are a reference only; the SDD §5 is the contract.

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

## Writing rules for Claude

- **Markdown hard-wrap at 180 characters.** When writing *or* editing any Markdown file, manually wrap prose at a maximum of 180 characters per line. The user's
  IDE hard-wraps at 180 but does not auto-repair the stray paragraph breaks that unwrapped edits leave behind, so emitting pre-wrapped Markdown saves a manual
  clean-up every time. Wrap prose and bullet text; leave fenced code blocks, tables, headings, and bare URLs intact on their own lines (never break a URL). To *verify* the wrap,
  measure line length with PowerShell (`[string].Length`) — not `awk length` or `wc -c`, which count bytes, so this repo's `—`, `×`, `…` (multi-byte UTF-8) inflate the count and
  raise false over-limit hits.
- **Don't add `[ UsedImplicitly ]` proactively.** JetBrains.Annotations may be referenced, but the user adds the attribute themselves when ReSharper/Rider actually complains.
  Don't speculatively mark designer-only types, DI-resolved types, or anything else that "looks dead but isn't" — and when a plan or spec shows `[UsedImplicitly]`, omit just
  that attribute and copy the rest of the type verbatim. If an analyser warning explicitly demands it on a specific member, propose it inline and let the user decide.
