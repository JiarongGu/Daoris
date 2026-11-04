# Workflows, review and landing — how it works now

## What it does

A workflow says how a repository's work moves from an agent's done to the line. It is a saved graph of steps of fixed kinds:
holds, a second opinion, the person's look, the person's Accept (`review`), one landing (`land`), and what comes after it (a
pushed branch, a pull request, integration, a release). Each place (this machine, a workspace, a repository) follows one saved
workflow; a fresh home follows a shipped copy of *Merge after you accept*. A chain's work in one repository is a run, bound at
its first start to one version. The node runtime in the driver loop evaluates the run's graph, and it performs the `land` itself
once the gates hold. A merge goes into the line; a branch is made for the person; a plugin pushes and opens a pull request; or
the line's integration takes it. Work no saved workflow governs is held, and nothing lands it. The release's migration converts
the old records (steps versions, `handed`, `land` with `accept`, the places on the landing rules) to saved graphs once.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they name another root.

| Unit | File | Its job |
|---|---|---|
| Shipped defaults | `Defaults/workflows/*.json`, `Workflows/WorkflowDefaults.cs`, `Workflows/HomeSeed.cs` | Eight shipped graphs read once and judged; the copy a home saves; a fresh home seeded with `merge-after-accept` |
| Delivery presets | `Workflows/Domain/SetupPresets.cs` | *Set up…*'s deliveries, the suggestion, the remote platform's plugin, *Keep how it lands* |
| Graph presets | `Workflows/Domain/WorkflowPresets.cs` | The shipped graphs for `workflow new --from`, with the opinion and look variants |
| Saved workflows | `Workflows/WorkflowStore.cs`, `Workflows/Domain/WorkflowNamed.cs` | One file per workflow; versions appended whole; the one save judge |
| Kinds and graph rules | `Workflows/Nodes/NodeContract.cs`, `Workflows/Nodes/NodeGraphs.cs` | The node kinds and states; `Problems()` refuses a graph that breaks a rule |
| Palette | `Workflows/WorkflowPalette.cs` | What the editor offers per kind: alone, as a landing's tail, or retired |
| Choosing | `Workflows/Domain/WorkflowSelection.cs` | Which workflow a chain's work follows, and the landed route |
| Run binding | `Workflows/Domain/WorkflowRunBinding.cs`, `Workflows/WorkflowRunBindings.cs`, `Workflows/WorkflowRunStart.cs` | A run bound at its first start, kept in `workflows/runs/<run>.json` |
| Process per gate | `Workflows/WorkflowProcesses.cs` | The binding and gate interface: a graph version read from its gate nodes; no binding, no workflow |
| Node runtime | `Workflows/Nodes/NodeRuntime.cs`, `WorkPerformer.cs`, `WorkObserver.cs`, `WireEvaluation.cs`, `NodeRecordFiles.cs` | One pass at a time: evaluate each governed unit, keep its moves, start what it proposes |
| `NodeGoverning` | `Workflows/Nodes/NodeGoverning.cs` | Which units a graph governs and how: a closure graph's quest, a run bound to a graph version on its own record, a unit held for the person; the quests bound to a closure graph the choices no longer hold. `NodeRuntime` keeps one-line doors |
| `NodeEvaluations` | `Workflows/Nodes/NodeEvaluations.cs`, `PassReads.cs` | A governed unit's graph judged on its record: a unit's, a run's over its subjects, and the pass's gate, content and git reads it waits on |
| `NodeTelling` | `Workflows/Nodes/NodeTelling.cs` | The awaited set the plugins are told: each unit's frame, the tips last told, the next timer the records persist |
| `NodeClosing` | `Workflows/Nodes/NodeClosing.cs` | A graph closing a quest: the `quest.close` start and its fence, what a closure graph's unit owes, the pruning of closure graphs nothing needs |
| `NodeRunKeeping` | `Workflows/Nodes/NodeRunKeeping.cs` | What a run bound to a graph version has kept around its rounds: the person's skips and stops, what a review sent back told to the work, a landing started, the run's end and reopening |
| `NodeIntegrating` | `Workflows/Nodes/NodeIntegrating.cs` | A unit's `integrate` and a run's `request` started through their doors, and the card waits |
| `NodeOwing` | `Workflows/Nodes/NodeOwing.cs` | Which units a pass judges: those owing a start or a rebinding, and the turn they are taken in past a pass's bound |
| Performing | `Workflows/Nodes/WorkPerformer.cs`, `WorkKeeping.cs`, `WorkHearing.cs`, `WorkReceipting.cs`, `WorkOutcomes.cs`, `WorkPersonWord.cs`, `NodeRecordEdits.cs` | The performer sends, reconciles and retries; keeping an evaluation's moves and a no-dispatch start, hearing a completion, taking its receipts, applying an outcome, and the person's word on a held attempt are each their own type |
| Evaluating | `Workflows/Nodes/WireEvaluation.cs`, `WireReceipts.cs`, `WireLapsing.cs`, `WireEligible.cs` | The evaluation's pass and firing keys; the receipts it judges on, why a bound node lapses, one node's eligibility; the wire records in files of their own |
| Graph reading | `Workflows/Nodes/NodeGraphs.cs`, `NodeGraphStarts.cs`, `NodeGraphPlaces.cs`, `NodeGraphReading.cs` | The save's rules; which nodes can start before a gate; the digest and each node's place; a graph's JSON read into its parts |
| Closing | `Workflows/Nodes/QuestCloser.cs`, `QuestClosing.cs`, `ClosureGraphs.cs`, `QuestReads.cs`, `ClosureBinding.cs` | The close node's sending and answering; what it judges on; the closure graphs kept by digest; the last quest read; the bind at the take |
| Run derivation | `Workflows/Domain/WorkflowRuns.cs`, `WorkflowChatSteps.cs`, `WorkflowReviewSteps.cs`, `WorkflowLandingSteps.cs` | A run derived from its records: the work and the pass in the derivation, the chat, review and landing steps each their own type; the run's records in files of their own |
| Named workflow reading | `Workflows/Domain/WorkflowNamed.cs`, `NamedStepReading.cs`, `NamedStepCanon.cs`, `Workflows/WorkflowOperations.cs`, `WorkflowUsing.cs` | A saved file and its ids and plan to add a version; a version's steps read, and their canonical text and digest; the typed operations, with choosing a workflow for a place apart |
| `daoris workflow` | `Workflows/DriverWorkflowsCommand.cs`, `WorkflowTerminal.cs`, `WorkflowListing.cs`, `WorkflowChoosing.cs`, `WorkflowWriting.cs` | The usage and the door; the terminal's flags, say and press; the reading, choosing and writing verbs |
| Holds | `Workflows/Nodes/HoldNodes.cs`, `WorkRequestsNode.cs`, `Workplace/ContentHolds.cs` | `quest.holds`, `content.holds` and `work.requests` before the landing |
| Land node | `Workflows/Nodes/LandNodes.cs` | `land`'s options, their resolution at the bind, the intent a run lands by |
| Landing door | `Workplace/LandingDoor.cs`, `Workplace/LandPerformer.cs` | The runtime's landing off the pass, under the tree's hold and the delivery key; the performer of a run's `land` |
| Tree landing | `Workplace/SessionTrees.cs`, `Workplace/TreeLands.cs`, `Workplace/Landing.cs` | The merge, the branch and the squash; the plan and its refusals; `LandingRules`, a landing's shape and the line's landing outside a run |
| Delivery record | `Workplace/DeliveryStore.cs`, `DeliveryReconciler.cs`, `CurrentDeliveries.cs` | Each landing's phases written ahead, settled after a restart |
| Landed branches, pull requests | `Workplace/LandedBranches.cs`, `PullRequestAsks.cs`, `PullRequestStates.cs`, `PullRequestBounds.cs`, `BranchTidying.cs` | `landings.json`, a pull request's state from its plugin, the look's tidy |
| Integration | `Workflows/Nodes/IntegrateNode.cs`, `Workflows/IntegrationTransport.cs`, `Workplace/IntegrationRequests.cs`, `IntegrationStore.cs`, `IntegrationLines.cs`, `Review/Integration/` | One request door, one run per line, its gates and judgement |
| Landing gate | `Review/SessionTreeGates.cs`, `Workflows/Domain/GraphGate.cs`, `GraphGates.cs`, `WorkflowGate.cs` | Whether work may land now, read once per decision and handed to every door |
| Look and opinion | `Review/Domain/ReviewGate.cs`, `ReviewRules.cs`, `OpinionGate.cs`, `OpinionRules.cs`, `Review/OpinionPresses.cs` | The review environment's levels; the second opinion and the person's words at it |
| Entry Accept | `Review/EntryAccepts.cs`, `Workplace/EntryLands.cs`, `Workplace/TogetherPress.cs` | One Accept for an entry or an ask, part by part: the word kept for each part a `land` performs, the rest held; the plan |
| Verification check | `Workflows/Nodes/CheckPort.cs`, `Workflows/VerifyRuns.cs`, `Review/Heavy/HeavyExecutor.cs` | The `check` node performed by Daoris, off the pass, under the heavy slots |
| Chat runs | `Workflows/ChatRuns.cs`, `Workflows/ChatCandidates.cs`, `Review/ChatPresses.cs` | A conversation's run bound to its graph; its subject the submitted candidate |
| Place set-up | `Setup/PlacePlan.cs`, `Setup/PlaceApply.cs`, `Workflows/PlaceProjection.cs` | *Set up…*'s plan and receipt-kept Apply; the `PLACES` read |
| Workflow migration | `Workflows/Migration/WorkflowMigration.cs`, `MigratedPlaces.cs`, `MigratedGraphs.cs`; `Install/Migration/WorkflowsStep.cs` | The release migration's workflow step: old versions and the places on the rules converted to saved graphs once |
| Routes and page | Modules `Bridge/Routes/WorkflowRoutes.cs`, `WorkflowEditRoutes.cs`, `SetupRoutes.cs`, `TreesRoutes.cs`, `EntriesRoutes.cs`; web `src/workflow/` (`chart/`, `edit/`, `place/`), `src/agents/setup/` | The bridge doors; the chart, the editor and the place page |

## Its rules

**Choosing and saving**

1. A place follows a saved graph workflow. A fresh home saves a copy of `merge-after-accept` and names it `defaultWorkflow`, once.
   A home that already has a `driver.json` is never seeded; it converts on the person's press. (from D227)
2. The workflow a chain's work follows is the first of: the task's choice, the repository's for the kind, its default, the
   workspace's for the kind, its default, the home default. Where none names one, its run is bound with no workflow (`current`)
   and held. (from D157, D227, D232)
3. A repository that names any development workflow replaces its workspace's whole. No door writes `current` as a development
   choice; a file that holds it is still read. (from D157, D227)
4. The landed route (`landed`) resolves on its own, names only a verification pipeline, and keeps the blob id of the accepted
   `daoris.gates.json`. A route changed applies to the next run. (from D192)
5. Saving (`WORKFLOW_SAVE`) and choosing (`WORKFLOW_USE`, a plan of one) are two presses. *Set up…* alone saves the shipped copy
   and chooses it, in one receipt-kept Apply. (from D221, D227)
6. A workflow is one file, `<home>/workflows/<id>.json`, on this machine. A version is whole and never edited; a save appends the
   next number. The newest 20 and every version a kept run names are kept. (from D157)
7. Only graphs are saved. One judge at the one save point refuses steps (`steps-refused`), a `land`
   with no method (`land-method`) and an `accept` option (`accept`). Only a graph version binds and runs. (from D227, D232)
8. A graph holds at most 24 authored nodes and 96 wires, and one landing: one `land`. There is no `handed` kind.
   (from D190, D227, D232)
9. The holds, the opinion, the look and the `review` each come before every landing and are wired straight into it. No landing
   may start before a hold or a look has passed. The opinion need not come before the look. (from D157, D190)
10. A `land` method brings its tail: `pull-request` brings `pushed` then `pull-request`; `push` brings `pushed`; `integrate`
    brings one `integrate`; `merge` and `branch` bring nothing. (from D215, D227)
11. A workflow file marks its pipeline: development (absent) or verification. A verification version holds at least one `check`
    and no `claimed`, `opinion`, `look`, `go-ahead`, `land` or `quest.close`. (from D192)
12. A change that lowers the person's part applies only by the person's press; an agent's stays a proposal. Editing the home
    default's copy asks the consent of every place that follows it. (from D157, D192, D227)
13. Ask Daoris proposes against the open draft's fingerprint (`workflow_propose`'s `draft`). Its proposal is drawn as a diff. It
    never saves, chooses or runs a workflow. (from D223)
14. The shipped defaults hold no `quest.close`: the agent's done closes the quest, and the flow's end is the work's. (from D190)

**Runs**

15. A run is a chain's work in one repository, bound at its first start on this machine and written once. Its id is the chain's
    first quest in that repository. (from D157)
16. One engine: a run bound to a graph version is the node runtime's. A run bound to anything else is held, its problem said.
    (from D190, D227, D232)
17. A graph run is the unit `run:<run>` of its own node record. `claimed` observes the chain's quests; the gates, the landing's
    nodes and a plugin's nodes the run's branch at its tip; a `go-ahead` the run's ask. (from D190)
18. The runtime is the record's one writer. A door keeps the person's word (skip, stop, ask again, go on, accept, changes) on the
    record, and the next pass applies it. A quest bound to a closure graph is closed by that graph. (from D190)
19. A `go-ahead` holds the landing until the person's yes or their skip with words. (from D190)
20. A conversation's run binds its graph. Its subject is the candidate the person submits; a newer submission lapses it. A graph
    holding `quest.close` holds a conversation's press. (from D227)
21. On a place that follows a saved workflow, a session's checks are `claimed.checks` on the graph; a `place checks` write is
    refused. Under `light`, a session runs only the gates not marked heavy, and the heavy ones are refused. (from D192, D227)

**Review and Accept**

22. The person's Accept is a `review` step before `land`. A flow with no `review` lands with no press, and the standing authority
    is judged again before the first effect. (from D145, D227)
23. A press under a run whose `land` the runtime performs keeps the word, asks a pass and waits up to 20 seconds for the landing.
    Past that it answers *accepted; landing*. The person's press is also the try again. (from D227)
24. An entry's or an ask's one Accept keeps each part's word where its run's `land` is the runtime's, and lands it at the pass. A
    part no `land` node performs is held, said. `TOGETHER_PRESS` brings sessions together and lands nothing. (from D167, D227, D232)
25. The look is the person's verdict in a review environment; no agent or card gives it. Under a saved graph the `look` node says
    whether one is needed; the environment is still the `reviews` rule's. `SET_REVIEW required` there is refused. (from D154, D227)
26. A second opinion is another maker's read-only reading before the landing. Its findings reach the working session once. What
    it disputes is answered by the person's *Reviewed* or *Accept…*. `SET_OPINION none` on a saved-graph place is refused.
    (from D155, D190, D227)
27. Review's diff is a dependency graph of today's git reads, uncached, taking no lock and refreshing no index in a tree Daoris does
    not own. Leaving the page stops its git. (from D202)

**Landing**

28. For a graph run the runtime is the one performer of `land`. The pass that finds it active at its tip starts it once on the
    landing door, off the pass, under the tree's hold and the delivery key, so a press and a pass land one delivery. (from D227)
29. A `land`'s open options are resolved once at the bind and kept: the pattern `feature/{quest}-{slug}`, the remote platform's
    plugin for `push` and `pull-request`, the default key where the pattern says `{ticket}`. A written option stands; no landing rule
    is read. (from D164, D227, D232)
30. `merge` lands into the line. `branch` makes a new branch from the session's branch, moves no checkout, and refuses a name that
    exists. `push` and `pull-request` hand the branch to a plugin. `integrate` hands it to the line's integration. (from D87, D215)
31. Daoris never pushes and never opens a pull request. A plugin speaking on `HookPoints.Land` does, for its platform. (from D87, D100)
32. A merge runs through the integration transport under the legacy policy: the owner's press, no gates, one at a time, a merge
    commit. (from D215)
33. Each delivery's phases are written ahead of their effects: prepared, ref written, recorded, plugin dispatched, plugin answered.
    A restart settles it phase by phase. (from D215, D227)
34. A landing refused for work the session can clear carries the session on and keeps the Accept. A refusal nothing changed is not
    tried again until the tip moves or the person presses. (from D145, D227)
35. In a session tree the agent commits on the tree's branch as it goes, and never switches or pushes. The commit rule
    (`commits`) may say `ask` or `at-end`. Daoris follows a tree an agent moved anyway. (from D164)
36. A chain's next step in the same repository starts on the step before's branch, and the chain lands on one branch, which
    advances only while its pull request can take it. (from D82, D149)
37. Branches a landing made are kept in `<home>/landings.json`, with traces once gone. A landing removes no tree and no other
    session branch. A session branch goes only by its own door or the look's tidy, behind D88's proof: every commit is on a branch
    that is not Daoris's, never forced. (from D88, D102, D232)
38. A pull request's state is asked of the plugin that opened it, at most once a minute per pull request. A landed session's
    review reads its landed branch. (from D113, D148)
39. Integration is one run per line, outside every run, with fixed steps. `done` and `steward` requests need declared, accepted
    gates. The line's policy is `linePolicies`, the only `integrate` read. A failure is confirmed once before it is blamed.
    (from D215, D227, D232)
40. A verification pipeline's `check` is Daoris's, with no agent session, never skipped. One run per line at a time, each batch
    run to its verdict. A failure becomes a fix ask, by the person's press or a standing `ask`. (from D192)
41. `SET_ACCESS` refuses the `landing` pause, and `full` access opens no landing pause on a saved-graph place. (from D184, D227)

**Set up, conversion and Current**

42. *Set up…* offers *Not driven* and the shipped deliveries marked `setUp`: merge after accept, a branch to push, a pull request,
    integrated. It suggests integrated where the line declares integration, a pull request where a plugin serves the remote's
    platform, else merge after accept. *Keep how it lands* writes no landing and no choice. (from D221, D227)
43. `PLACE_APPLY` is the one adoption door. `place --workspace … --adoption` paces a workspace's adoption by `atOnce`. (from D227)
44. The release's one migration converts the workflow records once through `WorkflowMigration.Convert`, its `workflows` step
    reading the repositories from the service's registry table: each steps, `handed` or `land`+`accept` newest gets the next
    version as a graph; each place on the landing rules follows the home default or a saved copy of what its rules drew; a rule's
    `integrate` becomes its line's policy; the landing rules and work cycles leave `driver.json`. A version the save refuses is
    left, said. There is no door, receipt or undo. (from D232)
45. No landing rule or work cycle is read or written: `driver.json` holds no `landings`, `workspaceLandings` or work cycles, and no
    door sets one. A hand-off pushes to the plugin its card names. (from D232)
46. Each old way still read is registered with the condition that ends it and the row that deletes it. There is no auto lander,
    no look that lands, no due list, and no landing rule's `tidy`; the performer of a run's `land` is the only lander. (from D227, D228, D232)

**Drawing**

47. One drawing serves the place's chart, the editor, a run and a proposal's diff: Daoris's components on the measured layout,
    always top to bottom, positions never saved. A wire's kind follows from where it goes. (from D221, D223)
48. Every graph stands on one canvas: a viewport with pan, zoom, fit and find; no node is moved by hand. The workflow zooms 80 to
    200% while edited. (from D224)

## Where a change goes

- **A new step kind or a graph rule:** the kind in `Workflows/Nodes/NodeContract.cs`, its rule in `NodeGraphs.Problems()`, its
  offer in `WorkflowPalette.cs`, its behaviour in a file of its own under `Workflows/Nodes/` (as `LandNodes.cs`, `HoldNodes.cs`).
  A performed kind starts through `WorkPerformer`; an observed one is reached through `WorkObserver`.
- **Which workflow work follows:** `Workflows/Domain/WorkflowSelection.cs`, held cell for cell by `fixtures/workflow-selection.json`.
- **A shipped workflow or delivery:** a JSON file under `Defaults/workflows/`; `WorkflowDefaults` reads it, and the presets derive.
- **How a landing behaves:** the method's options in `LandNodes.cs`; the act in `Workplace/` (`LandingDoor`, `SessionTrees`,
  `TreeLands`, the delivery files, `LandPerformer`).
- **Integration:** `Workplace/Integration*.cs` and `Review/Integration/`; the node's half in `Workflows/Nodes/IntegrateNode.cs`.
- **A gate or a hold at landing:** `Review/Domain/` (look, opinion) or `Workflows/Domain/GraphGate.cs` (the graph's part).
- **An old shape to convert:** the release migration's workflow step, `Workflows/Migration/WorkflowMigration.cs`.
- **A door:** a route in the module's `Bridge/Routes/`, its census rows, and the page under `src/workflow/` or `src/agents/setup/`.
- **The tests that pin it:** driver `Workflows/` (`NodeGraphsTests`, `SavesRefusedTests`, `WorkflowSelectionTests`,
  `WorkflowRunBindingTests`, `GraphRunLandingTests`, `GraphGatesTests`, `ChatGraphRunTests`, `HomeDefaultTests`,
  `SetupPresetsTests`, `NodeRuntime*Tests`, `WorkflowMigrationTests`), `Review/` (`ReviewGateTests`, `OpinionGateTests`), `Workplace/`
  (`Landing*Tests`, `Delivery*Tests`, `Integration*Tests`, `PullRequest*Tests`, each landing through a run bound by `GraphLand`),
  `Setup/PlacePlanTests` and `PlaceApplyTests`; modules `DriverModuleWorkflow*Tests`,
  `DriverModulePlace*Tests`, `PipeCensusTests`, `OwnerSetCensusTests`; web `src/workflow/**/*.test.ts*` and
  `src/agents/setup/*.test.tsx`; the family rehearsal's 17c set-up phase (`tools/setup-kit.mjs`).
- **The gates that bite:** `driver`, `driver-process-git` (landing and integration over real git), `modules`, `censuses` (a new
  route), `web-unit`, `web-rules`, `universal` (the structure gate, the names and catalogue checks), `orient-index`, and in
  `npm run verify` `transitional-markers` and `size-ratchet`.

## Contradictions

- D190 point 4 (a named version holds one `handed`) vs `NodeKinds` (no `handed` kind): a version lands by `land`. D227 and D232 read right.
- D190 point 8 (no look starts with no opinion passed) vs `NodeGraphs.cs:352-391` (gates only before the landing): the code and D190 point 5 read right.
- D190 point 1 and D157 point 7 (never a migration) vs `Conversion/` and `CONVERT_APPLY`: D227 point 3 reads right.
- D190 "Not settled: FABRIC-CLOSE1k" vs `NodeGoverning.Govern` (a bound quest closed by its bound graph): the code reads right.
- D157 point 1 (a workflow is a list of steps) vs `WorkflowNamed.ReadVersion(onSave)` refusing steps: D227 point 2 reads right.
- D157 point 15 (nothing set is Current) vs `WorkflowSelection.cs:234-236` (the home default first): D227 reads right.
- D157's rejection of *merging with nobody* vs the shipped `Defaults/workflows/merge-automatic.json` and `integrated-no-press.json`: the code reads right.
- D87 (how work lands is a workspace's landing rule) vs no rule read (`LandingRules.Line`, `Landing.cs`): D227 and D232 read right.
- D164 amendment point 2 (the rule holds pattern, key, squash) vs `LandNodes.cs:7` (`land`'s options): D227 reads right.
- D145 point 1 (a branch rule's `autoAccept`) vs `LandNodes.Reviewed` (no `review` step means no press): D227 reads right.
- D88 (without `tidy` the tree stays; clean-up is the person's press) vs `BranchTidying.cs` (the look's own tidy): the code reads right.
- D215 point 2 (the install's policy on a branch rule's `integrate`) vs `LandingRules.Line` (`linePolicies` alone): D227's 1a note reads right.
- D215 point 5 (no node added to any graph) vs `NodeContract.cs:60` (`integrate` kind) and `land` method `integrate`: the code reads right.
- D221's rejection of a place's choice changing through `PLACE_APPLY` vs `PlacePlan.cs:336` (Apply chooses the copy): D227 reads right.
- D227's 1e note (`daoris-driver workflow convert`) vs `Driver.Host/Program.cs:150` (`daoris-driver convert`): the code reads right.
- D157 and D223 (no zoom, no minimap) vs D224 and `workflow/chart/FlowChart.tsx:62`: D224 reads right; `WorkflowChart.tsx:353` still says "no zoom".
- D186 point 3 (a flow placed left to right where it fits) vs D221/D223 (a workflow always top to bottom, `flowLayout.ts:291`): both right, each for its flows.
- `NodeContract.cs:49-53` (`check` started by nothing) vs `Loop/DriverWatch.cs:627` (the check's executor wired): the code reads right.

## Open

- **SETUPUX1i:** the place page's tree.
- **SETUPUX1x:** the installed sitting of the place page, and its fixes.
- **SETUPUX1y:** Setup is the Agents place's default and its tree of places is always drawn; the opener's `roleSection` names Appointed
  roles or All roles only. A door naming a repository's place says `placeOf`, which `placeOpening` resolves to the place's id.
- **WORKFLOWCLEAN1j:** a saved workflow that runs a work cycle, only if a place with one is found.
- **WORKFLOWCLEAN1z (NOCOMPAT1b4):** the runtime suites still seeding work cycles in `driver.json` (`ObservedEvent*`,
  `IntegrateNodeTests`, `LookParallel`, `NodeWakes`, which the runtime no longer reads) rebound onto saved graphs; the `handed` node in
  about 22 test files.
- **CHATFLOW1 (1g, the rest):** the terminal's `ChatLandCommand` still lands a graph-bound chat in place; the installed window.
- **PROCESS1d-iii:** a session told its squash from `land`'s options, and a conversation's process proven.
- **INTEGRATE1 k, i, m:** the flip of this repository's integrator, the steward's acts, a sibling repository's declared integration; h's installed proof.
- **FABRIC-PIPELINE1d:** the verification run on a line has no view of its own yet.
- **FABRIC-PERFORM1l:** the pull-request event keeps its fields; WORKFLOW1j and 1k on observed nodes.
- **LANDNODE1j:** the installed window's proof; the performer's combined-head landing has no test of its own.
- **REVIEWFLOW1c and LINESTOGETHER1:** a session's steps as one flow, proven in the installed window.
- **GITAPP1 part two:** keeping or reusing review answers across opens, proposed and not designed.
- **WORKSETUP1:** the work workspace set to a pull request after the owner's Accept, on the install.
