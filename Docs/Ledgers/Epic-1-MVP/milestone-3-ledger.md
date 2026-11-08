# Milestone 3 Ledger — Direction Tabs

**Date:** 2026-11-08

Ledger for [`Docs/Plans/Epic-1-MVP/milestone-3-direction-tabs.md`](../../Plans/Epic-1-MVP/milestone-3-direction-tabs.md), executed on branch
`feat/milestone-3-direction-tabs` (cut from `main` at `097999b`). The spec is
[`Docs/Specs/Epic-1-MVP/milestone-3-direction-tabs-design.md`](../../Specs/Epic-1-MVP/milestone-3-direction-tabs-design.md), approved 2026-11-08; the master SDD ([
`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md)) was amended in Task 4 to match the build.
Tasks 1 to 4 are complete; each of Tasks 1 to 3 passed its task review.

While the run was in progress the live ledger was kept outside the repository (in the SDD working folder); this document is that ledger cleaned up.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

| #  | Task       | Ruling                                                                                                                                                          | Why                                                                                                                   | Cost if wrong                                                                                                       |
|----|------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------|
| R1 | Pre-flight | Implementers drove the app with Windows UI Automation from PowerShell rather than computer use, as in milestones 1 and 2.                                       | Computer use cannot be granted to a locally built app that is not installed.                                          | Visual-only defects are caught only where a screenshot was captured.                                                |
| R2 | Pre-flight | The live ledger lived outside the repository during the run and was cleaned into this document by Task 4.                                                       | Writing the docs ledger between tasks would add commits outside the task reviews.                                     | None.                                                                                                               |
| R3 | 1          | With a custom location, the Unindexed report uses the built-in name `FileAuditorUnindexedReport.txt` until milestone 4 adds its custom-name setting.            | There is no Unindexed tab or setting yet, so nothing can reach that name.                                             | None today; milestone 4 must replace the constant with a setting.                                                   |
| R4 | 2          | The project file's location is decided by the shared report location alone: save points judge "location complete" from the shared state, not from a tab's name. | The folder is shared between tabs; a tab's file name must not change where the project file goes. Fixed in `8608408`. | None; fixed before a second tab exists.                                                                             |
| R5 | Final wave | Files/second is computed over the Elapsed the window shows (the whole run) and left blank until a full second has elapsed (user-reported, see below).           | The old figure divided by checking time alone, which is milliseconds on a tiny table and meaningless.                 | The rate reads lower than the pure checking speed on runs where the count and query take a large share of the time. |
| R6 | Final wave | Remaining keeps its checking-time rate; only the displayed Files/second moves to the whole-run Elapsed (controller's ruling).                                   | Remaining should reflect how fast files are checked, which excludes the counting phase.                               | The two figures are computed over different spans, so Files/second and Remaining do not agree exactly.              |

## User decisions

Taken by the user in the milestone 3 spec (§6), not rulings.

- Two directions, each in its own tab, standard Win9x; shared inputs above the tabs (mock-up layout A, revised).
- The ID column is shared, not per direction.
- Each tab shows a complete Report group; every value is mirrored between tabs except the file name; the *Save project settings* checkbox is mirrored too.
- Reports renamed to pair up: `FileAuditorMissingReport_…` and `FileAuditorUnindexedReport_…`; custom defaults `FileAuditorMissingReport.txt` and
  `FileAuditorUnindexedReport.txt`.
- One project settings file holds both directions.
- The work is split into two milestones: this one (tabs) and milestone 4 (the unindexed audit).
- A Help menu item opens the user-guide wiki article, with F1; its address is hard-coded as an empty string until the article exists.
- The version stays 1.0.0.
- During the run the user reported that Files/second showed about 1,300 for a 6-file run; ruling R5 is the fix.

## Plan defects

None found. The pre-flight scan of the plan's task interfaces found no mismatches. `ReportLocationState` is deliberately not a `*ViewModel`, so the pair rule does not apply
to it.

## Outstanding items

### Final review wave (this milestone)

Fixed in the final-review fix wave; the shell-open item stays for later.

- **Task 2 ruling (R4), fixed in `8608408`:** the project file's folder is now resolved from the shared report location alone (`ReportLocationState.ResolveFolder`), used by
  Close project, Open project, window close and the picker's start folder. Start keeps the pre-flight's resolved path. `PreflightChecker.RunAsync` now takes the direction.
- **Task 2 minor, fixed in `8608408`:** `ReportViewModel` has one custom-file-name field for its own direction, mapped to the settings only in `ApplyTo` and `Load`;
  the `SelectedDirection` and `IsTabbed` comments now carry the milestone 4 checklist.
- **User-reported (R5), fixed in `f49c16c`:** Files/second is the files checked over the whole-run Elapsed, blank until a full second has elapsed. SDD §5.4 is amended.
- **Task 1 minor, closed:** the `_customFileName` field name is now correct (the restructure made it generic); the `AuditDirection` XML doc now notes that its member
  names form the report file names (`8608408`).
- **Task 3 minor, open:** see *Later*.

### Later / nice to have

- Milestone 4: the Unindexed Files tab, its inputs, its custom-name setting and report (replaces ruling R3's constant); generalising `ReportViewModel`'s direction-specific
  custom file name; adding the direction to `SelectedDirection`, `ReportFor`, `AuditFor` and `IsTabbed` (the full checklist is the comment on `IsTabbed`).
- Milestone 4: the status bar is typed to the missing-files `AuditViewModel`, so the Unindexed tab's different statistics will need a shared status surface (`StatusMessage` and
  `StatusPercent`) that both tabs feed.
- The shell-open try/catch is duplicated in `ViewReportAsync` and `HelpTopicsAsync`; extract it if a third caller appears.
- The `HelpLinks.UserGuideUrl` constant is empty; the user fills it in once the wiki article exists, which enables Help Topics.
- Milestone 2's and 1's deferred minors that remain open are listed in the [milestone 2 ledger](milestone-2-ledger.md) and the [milestone 1 ledger](milestone-1-ledger.md).

## Incidents

- **A stray interactive `python` command in Task 1** was run by the implementer despite the rule; it was stopped and had no effect.
- **A force-stopped app instance in Task 1:** after the SQL error dialogue could not be dismissed through UI Automation, the implementer force-stopped its own app process.
- **A browser tab left open in Task 3:** checking F1 with a temporary address opened `example.com` in the user's Edge; the tab was left open, as the browser holds the user's
  own tabs. The constant was restored to the empty string and rebuilt before the commit.

## Not exercised at runtime

- **F5, Ctrl+R and Esc** through the menu hot keys: injected keystrokes do not trigger them under UI Automation, before or after this milestone (confirmed against the
  pre-change commit). The XAML is unchanged, and the menu items enable and disable correctly (checked by screenshot). Worth a manual check.
- **Switching tabs:** only one tab exists. Per-tab routing (Start and View Report to the selected tab, Stop and the status bar to the running one, per-tab last result) has
  no second tab to exercise until milestone 4.
- **Help Topics with a real address:** only a temporary `https://example.com` was tried (it opened the browser); the committed address is empty, so the item is disabled.
- **The shared Report group across tabs** (a change on one tab shown on the other): there is one tab, so the mirroring is by construction (one shared object), not observed.

## Commits

`git log --oneline main..HEAD`, newest first (the commit that closes this ledger follows these):

```
bc577bf docs(📝): sync the SDD with the final-review fixes
f49c16c fix(🐛): compute Files/second over the whole run
8608408 fix(🐛): judge the project folder from the shared location and route pre-flight per direction
dd95ad0 docs(📝): sync the SDD with direction tabs and close the milestone 3 ledger
ddce716 feat(✨): add a Help Topics item for the user guide
fafbd45 feat(✨): put the missing-files audit in a tab with a shared report location
ef35a0f feat(✨): name reports per audit direction
5d021cc docs(📝): add the milestone 3 and 4 designs, mock-ups and the milestone 3 plan
```

## Final review

The Opus whole-branch review of `097999b..dd95ad0` found 0 Critical, 3 Important and 10 Minor issues. The Important ones: the project file's folder was still judged
from the selected tab's report while the SDD said the shared location (R4); the user-reported Files/second figure (R5); and pre-flight hard-wired to the missing
direction, which would have sent a milestone 4 Start to the wrong report. One fix wave (`8608408`, `f49c16c`, `bc577bf`) addressed all three and the minors, and its
Opus re-review found no Critical or Important issues. Its three small polish items (two doc lines and a blank line) went into the closing commit, together with
re-wrapping two over-long XML doc lines in `ReportPathResolver`.

**Incident in the fix wave:** to exercise the built `U:\Projects\…` location, the implementer mapped a temporary `U:` drive with `subst` and removed it afterwards.
