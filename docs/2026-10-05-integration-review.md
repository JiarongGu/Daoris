# Integration and review — 2026-10-05

## Scope and working state

Owner request: integrate unfinished work, retain only `main`, review the code with UI/UX priority,
improve documentation, and leave open work ready for the next agent.

Completed 2026-10-07. The recovered work and four review fixes are committed as `975978dc`.
Only the main checkout and `main` branch remain locally; the live remote also has only `main`.
All 31 branch heads saved before cleanup are reachable from `main`. Open UI findings and
installed-window proof remain in `TASKS.md`; manual review coverage is stated below.

### Initial inventory

- Initial `main`: `7ccd0372`, clean, matching the local `origin/main` reference.
- Thirty additional worktrees inspected. Six branches contained commits outside `main`:
  `integrate-ab`, and agent branches ending `a71c125219d8b3385`, `a8286a6a46e668262`,
  `ab9b50d02cf2af17f`, `adc4c6791584c614d`, `af1eb8af7a2a45393`.
- The account-naming branch (`a8286a6a46e668262`) also contained 22 modified UI files and one
  untracked test, recovered as `f6572c88` before cleanup.
- Five worktree locks referenced a process absent at inspection, rechecked before unlocking.
- No stash entries found. Other worktrees had no tracked or untracked changes reported by Git.

## Findings and handover

| Priority | Finding | Disposition and evidence |
|---|---|---|
| P1 | Lane policy could forgive source changes and reuse untested gate verdicts. | Fixed; failing-first regression and 67 gate-tool tests. Details: `docs/FIX-LOG.md`. |
| P2 | Completion notices ignored chosen account names. | Fixed; existing failing case and 72 affected UI tests pass. Details: fix log. |
| P2 | Help → Update opened above its card. | Fixed; full-App regression includes anchor clearing on later Settings visits. Details: fix log. |
| P2 | Windows held-file refusals stopped packaged CLI publication. | Fixed on 2026-10-07; deterministic failing-first regression exercises all three layout renames. Details: fix log. |
| P2 | Terminal hints do not quote valid account names. | Open: ACCTQUOTE1; validator accepts `R&D`, hint source interpolates it directly. |
| P2 | Inline account editors close before mutation success. | Open: ACCTEDIT1; source inspection found immediate closure at `AgentPage.tsx:429`/`:439`; rejection/retry tests required. |

The backlog prioritizes remaining UI work and separates rendered component evidence from installed
window proof. Original detailed task wording remains in the frozen Git revision cited there.

Post-feature audit: shared Settings wiring, roster-cache naming, optional bridge name fields,
stable account ids and English/Chinese catalogue counterparts were checked. Changelog, fix log,
decisions, router and task records reflect the integrated work. Explicit follow-ups are UX7b/UX7e
(installed-window proof), UX7a2 (compact menu), ACCTQUOTE1 and ACCTEDIT1; their contracts and proofs
are in the open backlog. The final gate receipt and branch cleanup are recorded below.

## Plan

1. Read discovery guidance, contracts, records and the branch changes; preserve unfinished changes.
2. Integrate the six branches, resolving documentation and generated-index conflicts deliberately.
3. Run relevant gates; review UI flows and boundary code; fix reproducible defects.
4. Improve documentation navigation and handover; archive completed tasks and retain open tasks.
5. Verify all original branch tips are reachable from `main`, preserve private worktree material,
   remove integrated worktrees/branches, and report verification limits.

## Verification

- 2026-10-06: preserved the unfinished UI changes as `f6572c88` on their existing branch.
  The account branch includes UX7b; the latest gate branch includes GATE6 and DUPNOTE1, so four
  merges cover all six outstanding tips.
- First integration: universal, code-map and orientation checks passed. CLI run: 1,281/1,283
  tests passed; two Windows directory renames failed with `EPERM` in `layCli` and
  `installFromChannel`. Both tests passed immediately when run together in isolation (2/2).
  Full CLI rerun passed all 1,283 tests; service passed 1,170 and HTTP host 68. These transient
  failures remain recorded. GATE6/GATE6b and DUPNOTE1 are integrated after five selected gates passed.
- 2026-10-06: `integrate-ab` merged with five selected gates passing. Its effective change is
  removal of one stray D137 note fragment; its other historical merge tips are now reachable.
  Parallel review agents stopped on a workspace credit error; their incomplete passes are not
  counted as completed reviews. Documentation findings returned before that error are retained.
- RETRY1b integrated after seven selected gates passed. The page and helper now use the planner's
  total failures; the terminal reads records with the driver's twin count rather than assuming the
  strike limit. At that checkpoint, full process/deployment gates had not run.
- Review finding: gate reuse trusts the current steward lane to classify forgiven records, including
  `daoris.lanes.json`. A lane edit can forgive itself and untested source paths. Add a regression and
  restrict forgiven records to stable record paths; lane configuration must invalidate every gate.
- Account merge: nine of ten selected gates passed, including release and family rehearsals. Web
  found one failing case: sign-in completion used the email instead of the account's chosen name.
  Fixed sign-in and default-change notices using the current roster cache, without reading credentials.
  The affected Agents and menu suites pass (72 tests); gate-tool suite passes (67), including the
  failing-first lane/union regression. Help → Update now anchors and clears after its first visit.
- Documentation: condensed open rows while preserving every task identifier except completed RETRY1b.
  Original supporting detail and owner quotations remain at `a3b20973:TASKS.md`; this is the frozen
  pre-condensation evidence. The draft has been retired. Router, roadmap, workspace ordering amendment,
  archive links and product introductions now agree with the decision records.
- Scope of review: discovery contracts, current UI frame/accounts/menu/heads, integration tooling and
  recovered changes were inspected. The generated document inventory and router were checked; this is
  not a claim that every historical evidence document or every source line received a manual review.
  Remaining installed-window proof and compact-menu implementation stay in UX7e/UX7a2.
- Review checkpoints: all five measured documentation files fit their existing word budgets; every
  open task fits 60 words. Task-id parity against the frozen backlog is intact. No duplicate records
  or split decision bodies found. The router's four oversized rows were condensed; historical archive
  entries remain unchanged. A verified Git bundle is retained at `local/branch-recovery.bundle`.
  Ignored private settings, local output, fixtures and logs from every worktree are preserved under
  `local/recovered-worktrees/`; reproducible dependencies and build outputs are not copied.
- Rendered story review: `Agents/AgentPage/TheInstall` at 1546 px and `TheInstallNarrow` at 680 px
  (English/light) show clear per-account rows without page overflow. The Help menu fits at 680 px
  in Chinese/dark. At 560 px English/light, the command center reduces to its icon and the seven
  menu labels remain; UX7a2 retains the designed compact-menu follow-through. These browser stories
  are component evidence, not installed frameless-window proof; UX7b/UX7e keep that distinction.
- Cleanup checkpoint: 28 worktrees and their merged branches removed with ancestry checks and
  ordinary `git branch -d`, after ignored material was preserved. Five stale locks were rechecked
  against their recorded process before unlocking. The two account-related branches still remained
  at that checkpoint, awaiting their merge commit. Live remote-head inventory contained only `main`.
- Keyboard story check: Escape returned from Help to its trigger; Left then Enter opened Terminal
  and focused its first item. Viewport overrides and the temporary browser tab were cleaned up.
- Records checkpoint: ACCT2b moved to the archive with its original wording and verification.
  UX7b remains open for installed account-page proof. Desktop README now leads with an isolated
  build/run quick start. Service and driver introductions distinguish original design status from
  current amendments, and historical corpus measurements are labelled as history.
- Open finding, ACCTQUOTE1: `AddAccount.tsx:112` and `:204` interpolate account names directly into
  terminal hints. Both validators accept shell metacharacters; the pure CLI validator accepts `R&D`
  with no problem. A pasted hint can therefore change argument boundaries or command meaning.
  No hint was executed. The backlog requests supported-shell quoting or explicit placeholders and
  covers workspace arguments too; narrowing the shared naming contract is not an incidental UI fix.
- Full-run checkpoint: universal, code-map, orientation, CLI (1,293), service (1,170 plus 68 HTTP),
  devkit (80), fast driver (4,240) and fast modules (603) passed. Process, web and deployment gates
  were pending at that checkpoint. Do not read the fast counts as totals.
- Process results: driver 709/709 in 27 minutes and modules 118/118 in 21 seconds, with no failures
  or skipped cases. Combined totals are 4,949 driver and 721 module tests. Gate logs and per-test
  timing receipts are under `local/scratch/merge-worktree-agent-a8286a6a46e668262/`.
  The four newly archived tasks now have separate entries, original wording and short outcomes;
  earlier archive entries remain as written.
- Web checkpoint: 4,129/4,129 unit tests passed, then production typechecking stopped on an optional
  roster passed to `byTool` and an unused parameter in the recovered ChainStrip fixture. Added the
  missing-cache empty-array fallback and marked the fixture's unused owner explicitly. Re-run web
  and affected fast readers, keeping valid Process receipts. Deployment had not run at that point.
- 2026-10-07: CLI re-verification hit the repeated `layCli` Windows `EPERM` failure (1,292/1,293).
  D139 and the update contract were loaded; `promoteStage` and shared `renameHeld` supplied the pattern.
  A failing-first injection exposed the missing retry wiring. All three package/launcher renames now
  use that helper; focused tests pass (2/2). No unrelated filesystem failure is hidden. The full
  rerun must include CLI, family and deployment gates affected by this change.
- Final source verification (2026-10-07): all 14 gates pass, confirmed by
  `node tools/merge-branch.mjs --passed`. CLI 1,294, web unit 4,129 and browser 24; family 374,
  deployment 110. Other suite totals are in `TASKS.md`. Full gate logs are under the scratch
  directory cited above. The production build checks 3,088 translation keys across 78 areas.
- Final cleanup: the remaining two worktrees and branches were removed with clean-tree,
  ignored-material, live-process, path-boundary and ancestry checks. No forced deletion was used.
  All 31 saved branch heads are reachable from committed `main`. The verified Git bundle and
  recovered local material remain under the ignored `local/` directory. INTEGRATE1 is archived;
  the handover contains only open work. Final document records receive fresh baseline/service gates.
