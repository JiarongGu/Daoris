# LANDNODE1: landing as a performed, configurable node of the workflow (brief for sealed proposals, 2026-11-01)

## What was found
The owner's report: landing still runs from a fixed configuration instead of being a flexible node of the fabric. The code agrees.
- **The landing rule decides.** A branch unit is governed by its landing rule's cycle, whatever run produced it (`docs/archive/2026-10-20-fabric-cutover-design.md:21`, `NodeRuntime.cs:251`).
  - The landing rule is the per-repository or per-workspace configuration in `driver.json`'s `landings` and `workspaceLandings`: merge, a branch with its pattern, a landing plugin (push and pull request), and the `integrate` policy.
  - It is judged by `Settings/LandingKey.cs`.
- **The workflow only observes.** The workflow's landing nodes `handed`, `pushed` and `pull-request` are declared **Observed** from git (`Workflows/Nodes/NodeContract.cs:279`). They observe what the rule did; they do not perform it.
  - The graph rules forbid a cycle after the landing to declare a node of a landing's kind, "which the landing rule holds already" (`NodeGraphs.cs:69`).
  - D215 (`docs/2026-10-28-integration-design.md`, about :1675) splits the policy owners: the workflow owns the unit's process, and the landing rule owns the line's batching, order, gates and promotion.

## The question
Design landing as a node of the workflow that Daoris performs, whose options a person sets on the node, so that how work lands is part of the workflow a person reads and edits, and not a separate fixed rule. Say:
- **(a) The node.** What the landing node performs, and what its options are. Cover at least: method (merge, branch, push, pull request, integrate), target line, branch name pattern, plugin, commit message, the integrate policy, and the accepts and gates that precede it. Say which options belong to the node, and which stay the line's (one line, many units: batching, order, promotion).
- **(b) The order of landing and integration.** How performing replaces observing:
  - the node runtime starting the landing through today's landing code (`TreeLands`, `LandingIntegrate`, the plugins), its reconcile, its idempotency and its restart;
  - the person's Accept staying the person's.
- **(c) What becomes of today's landing rule.** For example: the node's default options, a preset, or a migration. How existing repositories and units cut over with nothing lost, and how older builds read the result.
- **(d) What changes in D215's split, the cutover design's line 21 and the graph rules,** and what it reverses, with why.
- **(e) How it meets the accepted AGENTSPLACE1 design.**
  - `docs/2026-11-01-agents-place-design.md` §3 Workflow and §7 rows 1d, 1h and 1i: its *Landing* row and *Edit steps* become this node's options editor.
  - The quick set-up's delivery presets: each preset becomes a landing node's options.

## Where things are (read the code, not only these pointers)
**Driver:**
- `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/`: `Nodes/NodeContract.cs`, `NodeGraphs.cs`, `BuiltInNodes.cs`, `IntegrateNode.cs`, `NodeRuntime.cs`, `WorkPerformer.cs`, `WorkflowSelection.cs`, `WorkflowPresets`;
- `Workplace/TreeLands.cs`, `LandingIntegrate.cs`, `IntegrationReplays.cs`;
- `Settings/LandingKey.cs`;
- `Plugins/` (landing plugins).

**Designs and decisions:**
- the fabric (D173 and the `docs/` files named fabric);
- the workflow design `docs/archive/2026-10-09-workflow-design.md`;
- the cutover design `docs/archive/2026-10-20-fabric-cutover-design.md`;
- the pipeline design (D192);
- the integration design (D215);
- D157 (the protected order: holds, opinion, look, landing);
- D87 (landing rules);
- D100 (landing plugins).

**Web:**
- `src/Daoris.Web/src/workflow/`;
- `src/Daoris.Web/src/settings/repository/Landings.tsx`, `LandedRoute.tsx`.

## Constraints
- The person's Accept, and any acceptance of what an agent would run under, stay the person's.
- No landing is performed twice. A restart mid-landing settles by postconditions.
- Units in flight keep the workflow version they were bound to.
- Machine-local state stays on the desktop.
- Many repositories are imported but not driven.
- English and Chinese chrome.

## What to return (a sealed proposal, about 2000 words at most)
1. **Diagnosis**, with `file:line` evidence.
2. **The design:** the node and its options; how it performs; what becomes of the landing rule; the cut-over and migration; the graph rules; the route and data changes; the UI it implies within AGENTSPLACE1.
3. **What you reject**, and why.
4. **Rows**, each sized for one builder, with its proof.
5. **Risks and open questions.**

Write it as your own proposal. Do not read any other proposal for this round (files named `*land-node-proposal*`). Never read any `local/` folder. Name no private repository, customer or employer.
