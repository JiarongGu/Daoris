# WORKFLOWCLEAN1 — the reads of the design

Status: Opus's read of `docs/2026-11-02-workflow-cleanup-design.md`, read-only on main at `e32f55c4e`, kept in brief; the steward's
settlement is the design's §13. Codex's read is owed while its spend cap holds.

## Blocking

- **B1** — Current stays reachable after 1a: a repository naming a kind but no default falls to Current
  (`Driver/Workflows/Domain/WorkflowSelection.cs:224`), and an explicit `current` is still written by `WORKFLOW_USE`, `workflow use` and
  Ask Daoris's choice, so the census can never read ended.
- **B2** — 1a would change how existing `land` versions land: their open `pattern`/`plugin` resolve from the live rule at the bind
  (`Driver/Workflows/WorkflowRunBindings.cs:135-138`) and `squash`/the key at every landing (`WorkflowGate.cs:83-89`, `:195-199`); a
  version following a rule's own pattern would land on the derived default.
- **B3** — a `land` with no `method` saves and takes its method from the live rule at every landing (`LandNodes.cs:84`,
  `WorkflowGate.cs:85`); once rules go it would merge.
- **B4** — deleting `GateSteps`' landing half leaves the process's landing set to the live rule for every reader (`TreeLands.cs:119`,
  `:634`, `:667-712`; `CurrentDeliveries.cs:52-55`; `AutoLander.cs:130`, `:785`; `EntryLands.cs:404`; `OpinionGateLook.cs:239`;
  `IntegrateChoice.cs:33-43` and others): 1c must say what a `land` run's process carries and move each reader to the intent.
- **B5** — the conversion's match to a shipped copy by digest can never match (`GraphOfCurrent`'s wire ids and ends differ from the
  shipped files'), so every Current place would get its own workflow.
- **B6** — the `handed` lift can drop a press that exists today (a merge-form `handed` under a `merge --auto-accept` rule presses
  through `GateSteps`, `WorkflowNamed.cs:521-524`).
- **B7** — §2.2's doors miss `LAND_ASK`, `ASK_LANDING` and `trees land --entry|--ask`, and its proof contradicts §4.7's transitional
  in-place landing for Current, steps and `handed` units.
- **B8** — Accepts given before the update (the `CarriedAccepts` file, open due entries) may never land once `ChooseAsync` skips
  `land` runs.
- **B9** — row z deletes what the conversion still needs (`WorkflowCurrent.Derive`, the rules' reader), and nothing refuses an update
  forward over open old-engine runs.
- **B10** — `workCycles` has no end if row j is not taken, and `sessionChecks` (`SET_SESSION_CHECKS`, `daoris-driver checks`) is read
  only by Current's derivation: a second way on every place following a graph.
- **B11** — editing the home default's copy asks consent without naming the places following the home default
  (`WorkflowOperations.cs:454-470`).
- **B12** — a start between the apply's second plan and its change can bind Current to a rule the change then removes; and an
  unconverted home stops integrating at the update (admission moved to `linePolicies`).
- **B13** — the rows ship retirements before the window that replaces them is lit: the dark window still calls `SET_LANDING`
  (`Web/projects/setup/useRepositorySetup.ts:63`, `:215`; `Web/projects/workspace/WorkspaceView.tsx:479`, `:577`) and the conversion's
  page is dark.

## Non-blocking

*Set up…* runs `WORKFLOW_USE`'s own judgement; match the home's copy by digest; a door for `defaultWorkflow`; the rollback refusal
compared against the first build reading `defaultWorkflow`; `GraphOfCurrent` freezing the opinion's reviewers; line-policy inheritance
per level; chat-flow §4.5 and CHATFLOW1f amended for word-and-wait; `LAND_WITHOUT_LOOK`'s skip for a Current-bound chat; a press on a
`land` run with no review; LANDBACK1's carrier when the performer's landing is refused; *Undo* removing what it wrote; refusing
`SET_OPINION none`/`SET_REVIEW required` for a place on a saved graph; the access refusal in the shared edit; the bind's per-repository
plugin; gating `handed-refused` like F47; the clean-up keeping the end judgement the door's tidy wrote; a door to end each open old run;
D190 point 7 named by ONEWAY1; mixed entries under `LAND_ENTRY`; `workflow convert --apply` with no Daoris running.

## The decided points against the code

The tidy's retirement holds; the gates reading the graph holds in direction (B4 first); the access setup's `landing: rule`
retirement holds (it also reaches `handed` runs, B6); CHATFLOW1f is necessary; the terminal alone keeping the word holds for `land`
runs (B7's carve-out).
