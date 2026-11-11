# SETUPUX1 — the brief: one design for setting work up, composing its workflow and staffing it

Status: brief, 2026-11-01. Co-designed under D191: sealed proposals from this brief, one synthesis, then reads. Decision number reserved: D221.

## 1. Why

The owner's report on the installed window: the Agents place's Roles mode was close to unusable, a repository's set-up was split
across several pages with no quick way to define it, and the fabric's workflow configuration had no proper home. AGENTSPLACE1
(`docs/2026-11-01-agents-place-design.md`, D191's note) and FABRIC2 (`docs/2026-11-01-fabric2-plan.md`, D173's notes) built the
parts: a Setup mode of place pages (Roles, Workflow, Sessions, *Set up…*), dynamic workflows with decision steps, plugin steps and
a role per step, and a step editor. The parts were designed and built slice by slice, behind the flag `daoris.agents.setup`. What
is owed now is the experience as a whole: a person sets a repository up, composes how its work moves and staffs it, without
reading the system's internals, and what is complex stays reachable without being in the way.

## 2. What to design

1. **The Setup mode as one place.** The place tree (This machine, a workspace, a repository, imported and not driven), the place
   page's head and its sections, what is folded and what is open, and how a person moves between places and back.
2. **The quick set-up.** *Set up…* (delivery, adoption with *What it builds*, general driving, the plan, the acceptances, Apply),
   over one repository and over a workspace's several; how the plan reads before Apply; refusals in place.
3. **Composing a workflow.** The chart and its start node; *Change…* (a preset or a saved version) and *Edit steps*; the step
   editor's palette, its graph, a step's options, a decision step's outcomes and the route each takes (`when`, `back`), a plugin
   step (publish, release) and the role a step names; saving a version and choosing it; a run's view of where its work stands.
   The graph is a flow a person reads top to bottom; say how a branch, a loop back and an ending are drawn and edited at 1546 px
   and at 680 px.
4. **Staffing.** Roles on a place (per work, taken from a parent or its own), *All roles*, the role editor, the suggested line.
5. **The words.** One name per thing across the mode (落地, 合并, 工作流, 角色 …), checked against the glossary.

## 3. Constraints

- The routes stand as built: `PLACES`, `ROLE_STRUCTURE`, `PLACE_PLAN`, `PLACE_APPLY` (the person's alone), `SET_ROLE(S)`,
  `WORKFLOW_SAVE`, `WORKFLOW_USE`, `WORKFLOW_CURRENT`, `WORKFLOW_PALETTE`, `SET_LANDING`, the gates' acceptances. A design that
  needs a field names it as a row; it does not change what a press authorises.
- The design language and shared rules: `docs/archive/2026-09-19-platform-ux.md`, `docs/archive/2026-10-20-ui-ux-design.md`,
  `docs/archive/2026-10-05-ux7-design.md` (page heads, menus); translation parity (`.claude/knowledge/translation-parity.md`); the
  glossary (`src/Daoris.Web/src/locales/glossary.json`); molecules take props and send presses, organisms hold the hooks.
- Widths 80, 39 and 25rem (1546 and 680 px windows), en and zh, both themes, keyboard throughout.
- The two policy owners stay apart (D215): the workflow owns how a unit's work moves; the line's landing rule owns batching,
  order, gates and promotion.
- Nothing here is the page's judgement: a refusal is the driver's sentence.

## 4. What exists, to read

- Setup mode: `src/Daoris.Web/src/agents/setup/` (SetupPlaces, PlaceRoles, PlaceSessions, PlaceSetUp, PlaceSetUpPanel, placePlan),
  `src/Daoris.Web/src/agents/AgentsView.tsx`, `src/Daoris.Web/src/app/App.tsx`.
- Workflow: `src/Daoris.Web/src/workflow/` (WorkflowChart, WorkflowStartNode, WorkflowTab, WorkflowRunView), `workflow/place/`
  (PlaceWorkflowView, PlaceGates, ChangeWorkflow), `workflow/edit/` (WorkflowEditor, StepOptions, graphDraft, answers).
- Roles: `src/Daoris.Web/src/agents/roles/` (Roles, RoleDiagram, AppointedRoles, SuggestedChoicesCard, roleStructure).
- Each has stories (`*.stories.tsx`) and page tests; the locales under `src/Daoris.Web/src/locales/{en,zh}/`.
- The designs these came from: `docs/2026-11-01-agents-place-design.md`, `docs/2026-11-01-fabric2-plan.md`,
  `docs/2026-11-01-land-node-design.md`, the workflow design D157 names.

## 5. What a proposal returns

- The page structure, as wireframes in text, for each of §2's five parts, at 1546 and 680 px.
- The flows of `docs/2026-11-01-agents-place-design.md` §5, and two more: composing a release workflow with a decision, and
  staffing a step with a role the place adds.
- What it keeps, moves, merges and removes of what exists (§4), file by file, and why.
- Rows in build order, each with its contract and proof (page tests; stories at the three widths, en and zh, both themes; the
  installed window where it changes).
- What it leaves open, and what it would measure first.

A proposal reads the code and stories it names, never `local/`, and writes nothing into the repository.
