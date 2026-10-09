# Integration and review — 2026-10-10

## Scope and working state

Owner request: recover unfinished branches, retain only `main`, review the code with UI/UX priority,
improve documentation and leave actionable open work for later agents. The owner confirmed that
the other agents are paused by a rate limit and authorised taking over their unfinished work.

Initial main: `38065d97`, with the ASKHIST1c merge open and `AskHistory.tsx` conflicted. Twelve
other branches and thirteen extra worktrees were inspected; none reported uncommitted changes.
Outstanding commits are on the ASKHIST1c, WORKFLOW1f and FLAKE3 branches. Locks belonging to the
paused harness must be released only after recovery and ancestry checks.

## Plan

1. ASKHIST1c landed in `58feba8f`, preserving the IME guards and the history's new layout and pending rename.
2. WORKFLOW1f merged cleanly; its damaged-binding fix and all ten selected gates pass. FLAKE3 remains to integrate.
3. Review UI state, keyboard and refusal flows, then boundary and integration code; fix confirmed defects.
4. Improve documentation navigation and the open backlog, keeping historical records intact.
5. Preserve branch heads and private worktree material, verify reachability, then remove merged trees and branches.

## Coverage and findings

Discovery: `doc-loader`, `pattern-finder`, `set-up-documents`, `post-feature`, `fix-log` and the
integration procedure in `dispatch-subagent`. Guidance read includes autonomous development,
claims needing checks, development documents and translation parity. UI contracts read include
the frontend architecture, platform design language, component method and Ask Daoris history.
The previous integration and second-opinion reviews supply prior findings; open work remains in
`TASKS.md`. Review coverage and gate results will be recorded here as they land.

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
the final full set will cover its real-git landing and Keep cases before closing WORKFLOW1f.

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
was advertised; no remote mutation is needed.
