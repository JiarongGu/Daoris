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

1. Finish ASKHIST1c, preserving the IME guards and the history's new layout and pending rename.
2. Integrate WORKFLOW1f and FLAKE3 through the repository's merge tool; verify their combined tree.
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
It now uses the shared shell-word formatter, retaining the original saved name. This last change
must pass the reached gates before the merge is committed.

UXFIX1c's remaining accessibility work is included: plain menu acts no longer accept a visual-only
tick, and the views-menu target is 28 px. Forced-theme history stories confirmed no horizontal
overflow at the dock floor and the wider dock, in English and Chinese; Storybook checks do not
substitute for the installed-window proof still listed in the backlog.

Workflow review found that `WorkflowProcesses.Read` treats an existing but unreadable run-binding
file like an absent binding, falling back to Current. A corrupted named binding can therefore lose
its gate requirements. Recovery will distinguish absence from unreadability and hold the latter;
the regression and receipt will be recorded with WORKFLOW1f's integration.

Documentation: condensed the backlog without losing any of its 204 outstanding identifiers; full
acceptance details remain at `38065d97:TASKS.md`. Every row is within 60 words and all five budgeted
documents fit their ceilings. README's oversized driver catalogue now routes to its guide, the brief
fits its ceiling, and four router rows now point to their decision notes instead of repeating stale
implementation status. Historical archive entries remain intact.
