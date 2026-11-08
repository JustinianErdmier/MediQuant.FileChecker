# Milestone 5 — Skipped Rows and Run Statistics Implementation Plan

**Date:** 2026-11-08

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Missing Files audit skips index rows marked `SKIP` / `SKIPPED` and counts them; *Files/second* reads as a whole number; each tab's *Stop* is enabled only on
the running audit's tab; and *Remaining* follows the recent checking speed and counts down steadily.

**Architecture:** A static `SkipMarker` decides whether a path value is a skip marker; `FileChecker` returns a new `CheckOutcome.Skipped` for it before any other check, and
`AuditProgress`, `AuditRunner`, `AuditReport` and `ReportBuilder` carry a skipped count alongside the existing ones. A new `RemainingEstimator` (view-side, in
`ViewModels/Groups/`) turns the progress samples the 250 ms refresh already takes into the displayed *Remaining*. *Stop* gets a per-tab enabled flag on `AuditStatusBase`.

**Tech Stack:** unchanged (.NET 10, Avalonia 11.3.22, Classic.Avalonia 11.3.0.3, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient). No new packages.

**Spec:** the user's requests, approved in chat on 2026-11-08 and quoted below, and the master SDD
[`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md) §5.4 (Audit group), §6.2–§6.4 (checking, stop), §7 (report) and §12 (code
conventions). No milestone spec: the approved description is the spec.

The approved description, verbatim in substance:

1. **Skipped rows (Missing Files audit).** A path value that, ignoring surrounding spaces and case, is `SKIP` or `SKIPPED` on its own or followed by `:` and any text is
   skipped: never checked on disk, never listed in the report. The rows are written by hand or by the tool that builds the index table. A new **Skipped** stat sits beside
   *Unverified*; the report header (summary) gets a `Skipped:` line. *Total files* = *Checked* + *Skipped* + *Not checked*, on screen and in the report; on Stop, *Skipped* counts
   the skipped rows reached before the stop. The progress bar and *Remaining* treat skipped rows as done. The Unindexed audit is unaffected.
2. **Files/second:** a whole number with thousands separators, on both tabs. It counts checked files only.
3. **Stop:** each tab's *Stop* button is enabled only on the tab of the running audit. The *Stop* menu item and Esc keep stopping the running audit from either tab.
4. **Remaining:** estimated from a rate weighted to roughly the last 30 seconds of checking; between estimates the display counts down one second per second and eases toward
   a new estimate over a few seconds instead of jumping; `Calculating...` until about 5 seconds of checking have passed; `00:00:00` once the run ends. The user saw the old
   estimate hold still until about 30% of a two-minute run and then drift away from real time.

## Global Constraints

- **No automated tests.** Verify with `dotnet clean MediQuant.FileAuditor.slnx; dotnet build MediQuant.FileAuditor.slnx` (0 errors, 0 warnings) and by driving the built
  app with Windows UI Automation from PowerShell. **Before any keystroke or click, confirm FileAuditor.exe is the foreground window**; prefer UI Automation patterns over
  SendKeys. Never open a report in an external program (read it with `Get-Content`). Close the app when done.
- SQL Server for verification: LocalDB `(localdb)\MSSQLLocalDB`, scratch database `FileAuditorM5Scratch`, dropped when the task ends.
- **Skip rule** (exact): trim the value; it is a skip marker when it equals `SKIP` or `SKIPPED`, or starts with `SKIP:` or `SKIPPED:`, comparing with
  `StringComparison.OrdinalIgnoreCase`. Nothing else is: not `SKIP preflight`, not `SKIPPEDX`, not `SKIPPED :x`, not a path containing the word (`C:\SKIPPED\a.txt`). A `null`
  value is not a marker (it stays *Path is empty*). The rule is checked **before** every other check.
- Exact copy: stat label `Skipped:`; report summary line `Skipped:` (after `Unverified:`, always present on the Missing report, `0` when none); remaining placeholder
  `Calculating...`.
- *Checked* (on screen) = Found + Missing + Unverified, never skipped rows. The report's arithmetic: Total files = Found + Missing + Unverified + Skipped + Not checked.
- *Files/second*: `N0` with `CultureInfo.CurrentCulture` (as the counts), rounded; still blank until a full second has elapsed; Missing tab = checked ÷ whole-run elapsed,
  Unindexed tab = total files ÷ whole-run elapsed (unchanged bases).
- Remaining-estimator constants: rate time constant **30 s**, display easing time constant **3 s**, warm-up **5 s** of checking time (`AuditProgress.CheckingElapsed`).
- The window stays 640 × 668; the Skipped stat takes the empty cell beside *Unverified* (row 3, columns 2–3 of the Audit grid), so no row is added.
- All CLAUDE.md and SDD §12 conventions apply. Commit each task automatically, staging only its files and ticking its boxes in this plan.

## Review Focus

1. **The counts add up when skipped rows are interleaved and the run is stopped.** Stopping mid-run on a table where every tenth row is a marker gives a report whose Total =
   Found + Missing + Unverified + Skipped + Not checked, and on-screen Checked + Skipped never exceeds Total — Task 1 step 3.
2. **Skip-rule boundaries.** ` skipped: preflight `, `Skip`, `SKIPPED:` are skipped; `SKIP preflight`, `SKIPPEDX`, `C:\SKIPPED\a.txt` are not (the first two land in *Invalid
   characters in path*, the last is checked as a file) — Task 1 step 3.
3. **Remaining in awkward runs.** A run shorter than 5 s shows `Calculating...` then `00:00:00`; a stall (no rows processed) never shows a negative, infinite or wildly jumping
   value; a table that grew after counting (processed > total) reads `00:00:00` — Task 3 step 3.
4. **Stop across tabs.** While one direction runs, the other tab's Stop is disabled, the running tab's is enabled, Esc and *Stop* in the menu still stop the run from either
   tab, and after the run both buttons are disabled — Task 2 step 3.
5. **No layout change.** With the Skipped stat added, the Missing tab still fits 640 × 668 with nothing clipped — Task 1 step 3.

---

### Task 1: Skipped rows in the Missing Files audit

**Files:**

- Create: `Source/FileAuditor/Services/Checking/SkipMarker.cs`
- Modify: `Models/Auditing/CheckOutcome.cs`, `Services/Checking/FileChecker.cs`, `Services/Auditing/AuditProgress.cs`, `Services/Auditing/AuditRunner.cs`,
  `Models/Reporting/AuditReport.cs`, `Services/Reporting/ReportBuilder.cs`, `ViewModels/Groups/AuditViewModel.cs`, `Views/Groups/AuditView.axaml`

**Interfaces:**

- Produces: `public static class SkipMarker { public static bool IsSkipped(string? path); }`; `CheckOutcome.Skipped`; `AuditProgress.Skipped` (`long`) and
  `AuditProgress.Processed` (`long`, `Checked + Skipped`); `AuditReport` gains `long SkippedCount` after `FoundCount`; `AuditViewModel.Skipped` (`string`).
- Task 3 consumes `AuditProgress.Processed`, `TotalFiles`, `HasTotal` and `CheckingElapsed`.

- [ ] **Step 1:** `SkipMarker.IsSkipped` implements the Global Constraints rule. `FileChecker.CheckCore` returns `CheckOutcome.Skipped` for a marker before the empty-path
  check. `AuditProgress.Record(CheckOutcome.Skipped)` increments the skipped counter and **not** `Checked`. `AuditRunner` never adds a skipped result to `problems`, passes the
  skipped count to the report, and uses `Math.Max(total, checked + skipped)` for *Total files*. `ReportBuilder` adds `Skipped:` after `Unverified:` and computes *Not checked*
  as Total − Found − Problems − Skipped. `GroupTitle` and the section orders never see `Skipped`.
- [ ] **Step 2:** The Audit group shows `Skipped:` and its count in row 3, columns 2–3 (beside *Unverified*), formatted `N0`, blank before the first run (`Reset`). `Percent`
  and the existing *Remaining* formula use `Processed` instead of `Checked`; *Checked* and *Files/second* keep `Checked`.
- [ ] **Step 3: Verify.** Clean build. In `FileAuditorM5Scratch`, a table of about 20 rows: existing scratch files, a missing one, an empty-path row, and the Review Focus 2
  values. Run the Missing audit: the stats and the report (`Get-Content`) show the expected Skipped count; no marker appears in either section; the three non-markers land as
  stated. Then a table of about 200,000 rows with every tenth row a marker: start, stop after a few seconds via the Stop button's `InvokePattern`, and check the report's
  arithmetic and that on-screen Checked + Skipped ≤ Total. The window is still 640 × 668 with nothing clipped. Screenshot the Audit group. Drop the database.
- [ ] **Step 4: Commit** — `feat(✨): skip index rows marked SKIP or SKIPPED in the missing-files audit`.

### Task 2: Whole-number Files/second and a per-tab Stop

**Files:** Modify `ViewModels/Groups/AuditStatusBase.cs`, `ViewModels/Groups/AuditViewModel.cs`, `ViewModels/Groups/UnindexedAuditViewModel.cs`,
`ViewModels/Main/MainWindowViewModel.cs`, `Views/Groups/AuditView.axaml`, `Views/Groups/UnindexedAuditView.axaml`

**Interfaces:**

- Produces: `AuditStatusBase.IsStopEnabled` (`bool`, observable, `false` by default).

- [ ] **Step 1:** Both view models format *Files/second* as `N0` (Global Constraints); update their XML docs ("a whole number").
- [ ] **Step 2:** `RunAuditAsync` sets the running direction's `audit.IsStopEnabled = true` when the run begins and `false` when it ends (before `IsRunning = false`). Each tab's
  Stop button binds `IsEnabled="{Binding IsStopEnabled}"` beside its existing `Command`. `StopCommand`, the menu item and the Esc hot key are unchanged.
- [ ] **Step 3: Verify.** Clean build. With LocalDB, start a Missing audit long enough to switch tabs (a large scratch table): the Unindexed tab's Stop is disabled, the Missing
  tab's is enabled; Esc (foreground guard first) stops the run while the Unindexed tab is selected; afterwards both Stops are disabled. Repeat starting from the Unindexed tab
  (a scratch folder with enough files, or confirm by reading `IsEnabled` while it runs). Files/second reads as a whole number with separators on both tabs. Drop the database.
- [ ] **Step 4: Commit** — `fix(🐛): enable Stop only on the running audit's tab and show Files/second as a whole number`.

### Task 3: A steadier Remaining estimate

**Files:**

- Create: `Source/FileAuditor/ViewModels/Groups/RemainingEstimator.cs`
- Modify: `ViewModels/Groups/AuditViewModel.cs`

**Interfaces:**

- Consumes: `AuditProgress.Processed`, `TotalFiles`, `HasTotal`, `CheckingElapsed` (Task 1).
- Produces: `public sealed class RemainingEstimator { public void Reset(); public string Next(long processed, long total, TimeSpan checkingElapsed, bool hasTotal,
  bool isFinished); }`. It is not a `*ViewModel` (no view), like `ReportLocationState`; `AuditViewModel` owns one, resets it in `Reset()` and sets `Remaining` from
  `Next(...)` on every `Update`.

- [ ] **Step 1:** Implement `Next`, called every refresh (about 250 ms), with this algorithm:
    - `isFinished` → `00:00:00`. `!hasTotal`, or checking time under the 5 s warm-up → `Calculating...`. `processed >= total` → `00:00:00`.
    - **Rate:** an exponentially weighted moving average of the per-sample rate (`Δprocessed ÷ Δt` over checking time), weight `1 − e^(−Δt / 30 s)`. At the end of the
      warm-up it is seeded with the whole-warm-up average (`processed ÷ checkingElapsed`) so it starts sensibly. A sample with `Δt ≤ 0` is ignored.
    - **Target:** `(total − processed) ÷ rate`; a rate `≤ 0` keeps the last shown value (or `Calculating...` if nothing has been shown yet).
    - **Display:** the first shown value is the target; after that, each call subtracts `Δt` (wall time between calls, from the checking clock) from the shown value, then moves
      it toward the target by `1 − e^(−Δt / 3 s)` of the difference; never below zero. Formatted `hh:mm:ss` as today (hours may exceed 24).
- [ ] **Step 2:** `AuditViewModel.Update` uses the estimator; remove the old inline formula. XML docs on the new type and members; the 30 s, 3 s and 5 s values are named
  constants with a comment on why.
- [ ] **Step 3: Verify.** Clean build. In `FileAuditorM5Scratch`, a table sized for a 60–120 s run (calibrate the row count with a short trial; repeated paths to a few
  existing scratch files keep the report small). If `\\localhost\C$` is reachable, make the first half of the rows UNC paths and the second half local paths so the speed
  changes mid-run; otherwise say so in the report. Sample *Elapsed* and *Remaining* by UI Automation once a second for the whole run and save the samples in the task report.
  Pass: `Calculating...` for the first ~5 s; from 30 s on, every 10 s window shows Remaining falling by 7–13 s, except up to 15 s after the speed change; no two consecutive
  samples differ by more than 5 s beyond the second that passed; in the last 10 s of checking Remaining reads 15 s or less; `00:00:00` once finished. Also run a table small
  enough to finish under 5 s (`Calculating...` then `00:00:00`). Drop the database.
- [ ] **Step 4: Commit** — `feat(✨): estimate Remaining from the recent checking speed and count it down steadily`.

### Task 4: Documentation sync and ledger

**Files:** Modify `Docs/Specs/1-file-auditor-master-design.md`, this plan (tick boxes), `MediQuant.FileAuditor.slnx`; Create `Docs/Ledgers/Epic-1-MVP/milestone-5-ledger.md`

- [ ] **Step 1:** Amend the SDD: the skip rule and where it applies (§6, the checking order; the Unindexed audit unaffected); §5.4 statistics (Skipped, Checked excludes
  skipped, the Files/second format, the Remaining algorithm and `Calculating...`, both window diagrams); §5.4/§6.4 Stop per tab; §7 the report summary with `Skipped:` and the
  Not-checked arithmetic (and the sample report); §9 (`SkipMarker`, `RemainingEstimator`); §10 the builder's calls taken here.
- [ ] **Step 2:** Write the ledger (rulings, user decisions, plan defects, outstanding items, incidents, not exercised, measurements, commits, final review "Pending.").
- [ ] **Step 3: Verify** — clean build; Markdown prose ≤ 180 characters (PowerShell `[string].Length`); every file under `Docs/` is in the `.slnx`, this plan and the ledger in
  milestone order.
- [ ] **Step 4: Commit** — `docs(📝): sync the SDD with skipped rows and the run statistics and close the milestone 5 ledger`.
