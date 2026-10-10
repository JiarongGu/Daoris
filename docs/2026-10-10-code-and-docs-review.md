# Code and documentation maintenance review — 2026-10-10

## Scope and plan

MAINT1: the owner's request for code review alongside documentation, refactoring, deduplication
and cleanup to improve future code-generation sessions. This pass reviews development checks and
generators: the inputs they share, Markdown parsing, invocation chains and contributor guidance.
It is not a claim to have reviewed every application component or historical paragraph.

Starting tree: clean at `77fa48de`. Discovery: doc-loader, pattern-finder and post-feature;
contracts: development-documents, claims-need-checks, twins, D122/D127/D151/D159 and the contributor
guide. Deliberate runtime twins remain independent; code sharing stays within repository tooling.
Historical records and blocked cleanup are preserved.

Completed plan: establish failing-first review cases; consolidate the inventory reader; reuse the existing
fence parser in document checks; update guidance at its existing home; regenerate indexes, run
reached tests and baseline verification, review the final diff and archive MAINT1.

## Findings

| Finding | Evidence before repair | Repair |
|---|---|---|
| Two inventory readers disagree | `orient-index.mjs:listFiles` checks repository root and regular files; `doc-system.mjs` independently lists Git files and accepts any existing path | One shared reader in fsx, preserve the generator's existing exported API and use it in both callers |
| Quoted tables become live entries | `routedPaths`, `backlogRows`, `routerRows` and generic duplicate checks parse fenced examples; archive/decision readers already mask fences | Reuse `doc-duplicates.mjs:fenced`, preserving its tested CommonMark-style rules |

The exemplar is `tools/fsx.mjs`: one named helper per operation, no runner on import, explicit
root and repository scratch for fixture tests. `fenced` already has parity fixtures and callers;
another Markdown implementation would add drift rather than remove it. Tiny role-path helpers
are not extracted merely to reduce a line count, and runtime twins are not merged into a library.

## Verification

Failing-first cases reproduce quoted examples becoming live duplicate, shape and router entries.
The new inventory contract also fails against an empty implementation: real files are required,
and a nested directory must be refused. Existing indented examples still contribute to a real
backlog row's reading cost.

Current practice stays in `docs/development.md`; non-obvious defect mechanisms go to the fix log.

The focused run initially failed six of 57 tests: four reproduce defects and two specify the new
shared inventory API. After implementation, all 62 focused tests pass, including the orientation
generator's existing tests. Both callers now use `fsx:repositoryFiles`; `orient-index:listFiles`
remains an exported alias. Sorting, ignored/untracked inputs, deleted files, replaced directories
and unchanged Git state are covered. Tracked files remain included even under an ignored directory.

The fence parser itself and runtime twins were not changed. Shape/duplicate/router callers now
apply its existing mask. Current contributor guidance identifies these reuse points and their
limits without duplicating the generated helper catalogue.

The first baseline attempt stopped at TypeScript's strict tuple indexing in a new test matrix.
The matrix now declares literal tuples; runtime assertions and production behavior are unchanged.

`npm run verify` passes: TypeScript; 1,484 CLI tests; doctrine; six document budgets; shape reports;
six duplicate-checked records and 158 decision files; the structural audit of 563 Markdown files;
60 tooling tests; release consistency; and all five universal devkit gates. The final staged
universal check also passes: links cover 563 tracked Markdown documents, including this review.
Orientation is fresh across 133 generated files; backlog and router entries are within their
60-word shapes. Existing archive overruns remain advisory; MAINT1's outcome is within its shape.

## Final review

- Wiring: both inventory consumers import the same helper, and `listFiles` remains available.
  Existing CLI generator and shared fence-fixture tests pass alongside the new regressions.
- Records: current practice updated in the contributor guide, defect logged, release-facing check
  behavior noted, router extended, indexes regenerated and original MAINT1 row moved to the archive.
- Reuse: helpers have one implementation within tooling; no change to the independent runtime twins,
  no new package dependency and no runner executes on import.
- Scope: this is a development-tooling and documentation maintenance review. Product UI, installed
  windows, live account flows and deployment were not exercised by this change. Their existing
  backlog/proof boundaries remain; blocked scratch cleanup stays open as DOCSCRATCH1.

## MAINT2 — close review findings

Bound-run regression now holds the saved v1 step ids and look environment after both Current and the saved workflow advance; unread binding/version regressions return a problem with no replacement graph. The landing UI reproduced all three workflow holds on both doors and a hold arriving during an open confirmation. The shared gate projection now carries the driver's explanation to both preview and press; both UI doors use one hold component and suppress Accept.

Owner follow-up: fix all findings. Starting tree is clean at `15f81724`. The two MAINT1 code findings
and all seven DOCSYS1 drift findings already have repairs and green receipts; DOCSYS2's mapping
and fragment defects are also fixed. Earlier integration review still names three concrete defects:
WORKFLOW1f2 (Keep write race), WORKFLOW1c6 (bound-version diagram) and WORKFLOW1g2 (landing preview).
The owner confirmed all three workflow defects are in scope. Their existing task rows remain open
until code and reached verification land. Discovery also loaded translation parity, frontend and
workflow contracts for the diagram and landing surfaces.

Plan: confirm closure against code/tests, resolve remaining defects in the requested scope, verify
the reached behavior and record each outcome. Historical archive word overruns are advisory under
D127/D159 and remain evidence, rather than defects to erase. DOCSCRATCH1 and CLEANUP1 are separate
filesystem findings; only the primary worktree is registered, and their exact paths are ignored.

Cleanup recheck: preview metadata contains only its own files and directories, with no reparse
points; the unregistered harness directory is empty. A checked native PowerShell removal was again
rejected as blocked by execution policy before running. No deletion was routed through another
mechanism; DOCSCRATCH1 and CLEANUP1 remain open under that external restriction.

WORKFLOW1f2 failing-first proof: two real-Git cases remove or corrupt the binding during the review's
read, after the workflow hold is judged. Both previously returned success and said Kept; removal
also substituted Current in that sentence. The command now requires a non-null saved binding and
returns refusal with the original hold if the save could not read it. The existing successful
Keep/automatic-landing case remains the acceptance regression.

Focused proof: seven run bridge cases and 36 UI cases passed before the added bilingual/passing
workflow coverage. Real-Git preview/press tests pass for kind-path, missing-version and unreadable
binding holds, preserving identical gate data and writing no branch or landing record. Their
fixture homes use the bridge's new optional repository fixture directory; Git object read-only
attributes are cleared for owned test cleanup on Windows. The full driver process half is running.

Implementation review retained D154's task-added look even when the named version omits it, and
retained plugin readiness/state limits on named graph cells. Named ids/order are authoritative;
partial go-ahead steps remain visible with their existing limit instead of disappearing. These
contracts still need the final reached gates; editor Follow/Choose/Keep controls remain WORKFLOW1g.

The final diff found a saved step id can already be `look` when D154 adds a look to a version that
omits one. Added looks now choose a noncolliding id. A regression holds that id, the partial
go-ahead's place/limit, the task-added floor and the terminal's bound-version name together.

The wiring review also found the work frame keyed its session mark and owned controls by Current's
fixed ids. A saved-id frame regression failed first; the frame now maps each owner by kind to the
driver's actual step id. This keeps named review/opinion controls and the session mark attached
to their rows rather than disappearing after the backend starts preserving saved ids.

The review reader's service-error condition now matches `ReviewGate.ReadAsync`: a named look that
cannot start still counts as a possible review, even when no review declaration stands. An unread
ask must not turn that bound look into a skipped step. Two cases cover declared absence with an
answered or unreadable ask.

Bound graphs also read the workflow path gate for a standing tree, using `WorkflowAsync` rather
than reproducing its formulas. The bridge's real-Git kind-path case checks the run agrees with
preview/press. The run and terminal show the hold; a held landing has no Accept state. Missing
opinion/landing declarations have cannot-start regressions even without a standing tree.

Final UI focused check: four suites, 45 tests pass. Documentation budgets, shapes, duplicates and
structural coverage pass. The driver process half started after the Keep repair; final run-view
edge cases added during that run are covered by the subsequent fast/module process checks.

### MAINT2 gate receipts

Visual QA: six local Chromium Storybook renders passed without page errors or horizontal overflow
at a 420 px viewport, including the 360 px run panel, unread binding, workflow path hold and
Chinese/dark chrome. Inspected each screenshot under `local/maint2/screenshots/`: explanations and
terminal commands wrap, and the held head offers no Accept. Browser-act and uv were unavailable;
the repository's installed Playwright runtime supplied this check. This is local component proof,
not installed-window proof; no live install was changed.

Full checks run serially; a failed gate stops this sequence for investigation.

| Check | Result |
|---|---|
| Final baseline | Pass: `npm run verify` |
| Staged universal | Pass: five gates after record closure |
| Code map / orientation | Pass: 18 modules / 17 dependencies; 133 generated files fresh |
| Family rehearsal | Pass: 425 checks; `_fixtures/rehearsal-logs/family-2026-10-10T08-04-14-768Z.log` |
| Service | Pass: 1,758 core/MCP and 245 HTTP tests |
| Web pyramid | Pass: 284 suites / 5,260 tests, production build and 24 browser cases |
| Keep final regression | Pass |
| Modules process | Pass: 125 tests |
| Modules fast | Pass: 770 tests |
| Driver fast — corrected final reader | Pass: 6,016 tests |
| Driver fast — final run reader | Initial compile failure; corrected rerun passes above |
| Driver fast compile correction | Renamed the new wording pattern variable, which collided with an existing switch variable |
| Driver process — Keep repair | Pass: 809 tests, 30 m 47 s |

### MAINT2 final review

The three confirmed workflow defects are fixed and moved to the archive with their original task
wording. Desktop/web guides, D157, the release log and root-cause records describe the implemented
behavior. Saved-id owner controls, added-look collisions, unavailable declarations and unreadable
asks are included in the regressions. The shared runtime process and gate remain authoritative;
no Current twin algorithm or package dependency changed.

Reached application gates, family rehearsal and six local visual renders pass. Documentation
budgets, shapes, duplicates, router coverage and freshness declarations pass; generated orientation
is fresh. Final `npm run verify` passes after record closure: CLI typecheck/tests, doctrine, document
checks, 60 tooling tests, release consistency and five universal gates. Existing
historical archive overruns remain advisory. DOCSCRATCH1 and CLEANUP1 remain open because native
removal was rejected with “blocked by policy”; no alternate deletion was attempted. Workflow editor
and installed-window proof remain their existing tasks, and no live installation or push occurred.

Local implementation commit: `423cc132`. The documentation follow-up fills its fix-log provenance;
it changes no runtime behavior and is checked by the documentation and universal gates.

### Cleanup authorization follow-up

The owner explicitly requested resolving the policy refusal and continuing the two cleanup actions.
Session permissions already report full filesystem access with approval prompts disabled; the prior
rejection did not identify a repository rule to edit. Reinspection confirms the scratch metadata is
ignored with no reparse points, the harness directory is empty, and only the primary Git worktree is
registered. Retry the exact native cleanup through the normal command review under this explicit
authorization; do not route a refused deletion through another mechanism.

The owner further requested repository-local policy rather than global Codex changes. Added
`.codex/config.toml` with the active full-access/no-prompt defaults and placed the local cleanup
procedure in the development guide, linked from the handwritten brief. Global settings were only
inspected. The exact native removal was reviewed again under explicit authorization and rejected
before execution with “blocked by policy.” Both directories still exist; no alternate API was used.
This proves the active command restriction remains independently enforced, not that future sessions
loaded the new project configuration. DOCSCRATCH1/CLEANUP1 remain open; documentation checks follow.

Configuration TOML parses and contains only the requested two defaults. Doctrine, six document
budgets, backlog/router shapes, duplicate checks and structural coverage pass; historical archive
overruns remain advisory. The orientation index is regenerated. This changes local configuration
and contributor guidance; application behavior and its preceding gate verdicts remain unchanged.
Staged universal verification passes all five gates, including 59 links across 563 Markdown files.
