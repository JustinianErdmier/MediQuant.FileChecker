# Milestone 4.1 Ledger — Layout Follow-up

**Date:** 2026-11-08

Ledger for [`Docs/Plans/Epic-1-MVP/milestone-4.1-layout-follow-up.md`](../../Plans/Epic-1-MVP/milestone-4.1-layout-follow-up.md), executed on branch
`feat/milestone-4.1-layout-follow-up` (cut from `main` at `68f044a`; the plan was committed in `5d39fac`). There is no milestone spec: the user's five requests, quoted in the
plan, are the spec. The master SDD ([`Docs/Specs/1-file-auditor-master-design.md`](../../Specs/1-file-auditor-master-design.md)) was amended in Task 4 to describe what was built.
Tasks 1 to 4 are complete; each of Tasks 1 to 3 passed its task review with no Critical or Important findings.

While the run was in progress the live ledger was kept outside the repository (in the SDD working folder); this document is that ledger cleaned up.

## Rulings

Decisions taken without the user, each with what it costs if wrong.

- **R1 (pre-flight):** no milestone spec; the user's five items, quoted in the plan, are the spec. Cost if wrong: none.
- **R2 (pre-flight):** the group name *Project Settings*, the three status messages and *Clear*'s access key `L` were chosen by the controller and shown to the user before
  "go". Cost if wrong: cosmetic.
- **R3 (Task 1):** the commit trailers read `Claude Sonnet 5.5`, as the session's attribution reminder says, not the `Claude Opus 5.5` of the common rules (`5cf0ae3`,
  `ae79471`, `9bc3bb4` and `dede8d1`; signed, not rewritten). Cost if wrong: cosmetic.
- **R4 (Task 3, re-measured in the polish wave):** the window height is a fixed **668** pixels (outer 640 × 668; Task 3 measured 672 before the polish wave removed
  the Report group's empty row), measured at the current display scale with *Custom* ticked and unticked (identical).
  Cost if wrong: another display scale may clip or leave a gap; re-measure whenever a group changes.
- **R5 (final review):** Task 4 (docs only) was reviewed by the final whole-branch review rather than a separate task review. Cost if wrong: doc defects found one stage later.

## User decisions

The five requests that drive the milestone (2026-11-08):

1. The project folder and stage inputs stay displayed but disabled when *Custom* is ticked, instead of hidden (reverses milestone 4).
2. The Database and File Index Table groups stay side by side; a *Project Settings* group sits under them. The save checkbox moves into it, with a status message below it:
   saved as `FileAuditorSettings.json` with the reports; saved at a different path when the open file is not in the report folder (no path shown); not saved when unticked.
3. The *Include subfolders* column moves back to the right of the folders list, the folder column truncates so the checkbox is never hidden, and the list never overflows
   horizontally.
4. The File Types and Exclude editors get a button that clears the list, disabled while the list is empty.
5. No dead space: the window's fixed height is the Missing Files tab's minimum; the Unindexed Files tab scrolls vertically when it overflows.

## Plan defects

- **Task 2 Step 1** called the *Include subfolders* column "auto width". A fixed 110 pixels was used in the header and item grids instead: each row is its own `Grid`, so `Auto`
  would size every row on its own and the columns would not line up. Cost if wrong: none visible.

## Defects found in review

- **Task 2:** the header drifted about 17 pixels from the rows when the vertical scroll bar appeared, and the header text was left-aligned over centred checkboxes. Fixed in Task 3:
  the list's vertical scroll bar is pinned to `Visible`, the header has a 16-pixel right margin, and the header text is centred.

## Outstanding items

Fixed in the final-review polish wave (`0b5c357`):

- The empty fifth row of the Report group, which left a gap under *File name* on the Missing Files tab; the window height is re-measured at **668** pixels (was 672).
- `MainWindow`'s `d:DesignHeight` (now 668).
- The folders header's 16-pixel right margin is commented (the vertical scroll bar's width).
- The SDD nits: the blank line before §5.4, the ragged §11 bullet, and the reference to the private `SameFolder` in §5.

Revisit when the groups or display scale change:

- `Height="668"` has zero slack and was measured at the current display scale only; re-measure if a group changes or the app is run at another scale.

Left as is:

- **Task 1:** the status message is recomputed on every `ReportLocationState` change, including keystrokes (cheap).

## Incidents

- **A stray file in Task 3:** the implementer's first shell edit hung and left an `UnindexedFilesTabView.axaml` at the repository root; the implementer removed it.
- **The app did not close through `WindowPattern.Close` in Task 3** (probably the close prompt); the implementer stopped its own process.

## Not exercised at runtime

- The message between a save that moves the project's file and the following reset (covered only by the `FilePathChanged` wiring).
- The folders list with more rows than fit (the vertical scroll bar's behaviour with rows) and a populated list built through the real folder picker.
- The Exclude editor opened directly.
- Anything needing SQL Server: Start saves the project first and the resulting SQL error dialogue was dismissed.

## Measurements

- **Window:** 640 × 668 pixels (outer), at the current display scale; the longest Project Settings message is about 385 pixels wide, so it stays on one line.

## Commits

`git log --oneline 68f044a..HEAD`, newest first (a last ledger wording fix follows these):

```
0b5c357 fix(🐛): close the milestone 4.1 final review follow-ups
dede8d1 docs(📝): sync the SDD with the layout follow-up and close the milestone 4.1 ledger
9bc3bb4 feat(✨): add Clear to the file-type editors and fit the window to the Missing Files tab
ae79471 fix(🐛): keep the Include subfolders column visible for long folder paths
5cf0ae3 feat(✨): add a Project Settings group and disable report options instead of hiding them
5d39fac docs(📝): add the milestone 4.1 layout follow-up plan
```

## Final review

No Critical or Important findings. Six minor findings, plus the parked ones, were all addressed in the polish wave (`0b5c357`). Its
scoped re-review was clean; its two ledger wording nits are fixed in the commit after it.
