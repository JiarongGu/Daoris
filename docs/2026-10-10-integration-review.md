# Integration and review — 2026-10-10

## Scope and outcome

Owner request: recover unfinished branches, retain only `main`, review the code with UI/UX priority,
improve documentation and leave actionable open work for later agents. The owner confirmed that
the other agents are paused by a rate limit and authorised taking over their unfinished work.

Initial main: `38065d97`, with the ASKHIST1c merge open and `AskHistory.tsx` conflicted. Twelve
other branches and thirteen extra worktrees were inspected; none reported uncommitted changes.
Outstanding commits were on the ASKHIST1c, WORKFLOW1f and FLAKE3 branches. All are now merged;
all original branch heads and the detached tip are reachable from `main`.

## Recovery

1. ASKHIST1c landed in `58feba8f`, preserving the IME guards and the history's new layout and pending rename.
2. WORKFLOW1f landed in `0c79095e`; its damaged-binding fix and all ten selected gates pass.
3. FLAKE3's original history landed in `74755eb0`, with no resulting content change and four baseline gates passing.
4. UI state, keyboard, refusal and workflow boundaries were reviewed; fixes and remaining findings are below.
5. Documentation navigation and the open backlog were condensed, preserving historical acceptance details.
6. Twelve additional branches and thirteen extra Git worktrees were removed after preservation and ancestry checks.

FLAKE3's fixes and records were already present through its clean integration. The original branch
contains one rehearsal-authored records commit (`c2546559`) without a coauthor trailer. Its merge
used the tool's documented provenance-check override after inspecting that commit; no test gate was
skipped by that override, and no history was rewritten.

## Coverage and findings

Discovery: `doc-loader`, `pattern-finder`, `set-up-documents`, `post-feature`, `fix-log` and the
integration procedure in `dispatch-subagent`. Guidance read includes autonomous development,
claims needing checks, development documents and translation parity. UI contracts read include
the frontend architecture, platform design language, component method and Ask Daoris history.
The previous integration and second-opinion reviews supply prior findings; open work remains in
`TASKS.md`. This review records the recovery and its selected checks. The final full-set receipt is
the INTEGRATE2 outcome in `docs/task-archive.md`; until that entry exists, INTEGRATE2 remains open.

The documentation inventory contains 553 tracked Markdown files, including generated outlines,
doctrine mirrors and historical records. An inventory check is not a manual review of every
historical document or every source line; claims below will name the actual coverage.

Confirmed UI defects: index entries rendered `kind.Index` and omitted the service's source ranges
(ORIENT2i); a pending or refused New conversation switched drafts before END_CHAT accepted it.
Both have failing-first regression cases. The history layout preserves composing-key guards.
The merge's nine selected gates passed after the draft fix. The terminal hint for renaming also
interpolated arbitrary quoted names; failing-first bilingual cases reproduce quotes and operators.
It now uses the shared shell-word formatter, retaining the original saved name. All nine reached
gates pass: universal, code map, orientation index, CLI, service, driver, modules, release rehearsal
and web. The web receipt includes 284 suites / 5,231 tests, a production build and browser end-to-end
tests; the targeted history suite passes all 51 tests. These changes landed in `58feba8f`.

UXFIX1c's remaining accessibility work is included: plain menu acts no longer accept a visual-only
tick, and the views-menu target is 28 px. Forced-theme history stories confirmed no horizontal
overflow at the dock floor and the wider dock, in English and Chinese; Storybook checks do not
substitute for the installed-window proof still listed in the backlog.

Workflow review found that `WorkflowProcesses.Read` treats an existing but unreadable run-binding
file like an absent binding, falling back to Current. A corrupted named binding can therefore lose
its gate requirements. Recovery distinguishes absence from unreadability and holds the latter;
four invalid-JSON and missing-fields regressions failed in the driver's fast gate before the fix.
All 48 workflow gate tests now pass. All ten selected gates pass, including the family rehearsal;
the web gate also passes all 24 browser cases. The final full set covers real-git landing and Keep
cases before closing WORKFLOW1f; its completion receipt belongs in the archive.

Additional review follow-up: `WorkflowKeepCommand.KeepAsync` ignores the nullable update result,
so a binding removed between judging and writing can be reported as kept. `WORKFLOW1f2` records
the required race regression and retained-hold outcome; the normal press is covered by the branch's
existing tests. This item is separate from the damaged-binding gate fix.

UI follow-up: the run reader still derives Current (`WorkflowRun.Read.cs:120`), while named gates
now use their bound version. `WORKFLOW1c6` records the required agreement between the run diagram,
the terminal and the gate, including an unreadable-version state. The workflow editor remains
`WORKFLOW1g`; this recovery does not claim that unfinished screen is complete.

Landing UI follow-up: the module's LANDING preview carries review and opinion, but omits the
workflow gate. `SessionHead` consequently considers only those two when offering Accept.
`WORKFLOW1g2` covers unreadable and kind-path holds before the press, with appropriate person
actions; the backend already refuses the held landing. This is part of completing the workflow UI.

**Review closure (2026-10-10).** WORKFLOW1f2, WORKFLOW1c6 and WORKFLOW1g2 are fixed and archived.
Root causes are in `docs/FIX-LOG.md`; the code review and final receipts are in
`docs/2026-10-10-code-and-docs-review.md` (MAINT2). The preceding findings describe the reviewed
recovery snapshot; workflow editor work remains open as WORKFLOW1g.

Documentation: condensed the backlog without losing any of its 204 outstanding identifiers; full
acceptance details remain at `38065d97:TASKS.md`. Every row is within 60 words and all five budgeted
documents fit their ceilings. README's oversized driver catalogue now routes to its guide, the brief
fits its ceiling, and five router rows now point to their decision notes instead of repeating stale
implementation status. Historical archive entries remain intact.
The service guide now points test counts to gate logs and dated reviews instead of the open backlog.

Preservation: a verified complete Git bundle holds the original branch heads and the detached tip;
the original staged and working diffs are saved alongside it. Worktree-local records and harness
settings are retained under ignored `local/backups/worktree-private/`. Builds, dependency folders
and test fixtures were identified separately before cleanup. No remote branch other than `main`
was advertised; no remote mutation was needed. Only `main` remains locally, and only the primary
worktree remains registered. One empty directory, `.claude/worktrees/agent-a3b92da3b8bb159c9`, is
held open by the paused harness. Both Git removal and a checked empty-directory removal reported
the process lock; CLEANUP1 leaves that filesystem cleanup open until the harness releases it.
No live installation was changed and no commits were pushed.
