# AGENTSPLACE1: the Agents place made usable (brief for sealed proposals, 2026-11-01)

## What was found
The owner, using the installed window, reports three problems.
- The **Roles** mode of Agents is close to unusable: its UI and flows do not let a person do what they came to do.
- A **repository's set-up** is split across many pages, with no quick way to define it.
- The **fabric configuration** (the workflow a repository's work runs through: its nodes, gates, landing and integration) has no proper home or UI.

The owner wants all three designed properly, in the Agents place, as one coherent design.

## The question
Design the Agents place so that a person can do three things there:
- **(a) Roles:** see and run their company of agents.
- **(b) Set-up:** define a repository's set-up quickly, then adjust it in one place.
- **(c) Workflow:** see and change the workflow configuration that governs a repository's work.

## Where things are today (read the code, not only these pointers)
**Agents (web):** `src/Daoris.Web/src/agents/`.
- `AgentsView.tsx`, `AgentsModes.tsx`, `modes.ts`: the place and its modes.
- `roles/`: the Roles mode, built as COMPANYVIEW1a–g and i.
  - `Roles.tsx`, `RolesView.tsx`, `RoleSections.tsx`, `StructureSections.tsx`, `AppointedRoles.tsx`, `RoleDiagram.tsx`, `SuggestedChoicesCard.tsx`, `callTable.ts`.
  - Each has stories (`*.stories.tsx`) showing its states.

**The design records behind those:**
- The Roles mode's accepted design: `docs/archive/2026-10-31-company-view-design.md`, with its brief, two proposals and reads beside it.
- Roles and their holders: `docs/2026-10-24-supervisor-design.md` and `docs/archive/2026-10-24-steward-role-design.md` (D208, D209).
- Model roles: `docs/model-roles.md` (D205).
- The company picture: D193.

**Repository set-up (web):**
- `src/Daoris.Web/src/projects/setup/`: `RepositorySetup.tsx`, `SetUpForAgents.tsx`, `RepositoryRules.tsx`, `StandingAnswer.tsx`, `SupervisorDoor.tsx`, `WorkspaceSetup.tsx`.
- `src/Daoris.Web/src/settings/repository/`: Access, AgentRules, Commits, HandOver, HeavyGates, Helpers, `IntegrationDeclaration.tsx`, `LandedRoute.tsx`, Landings, and the rest.
- The set-up designs: `docs/archive/2026-09-30-setup-guide-design.md`, `docs/archive/2026-10-01-workspace-setup-design.md`, `docs/archive/2026-10-02-setup-pilot-lessons-design.md`.

**Set-up from the terminal:**
- `daoris import`, `daoris wire`, `daoris retire`;
- `daoris-driver drive`, `trees <r> on`, `setup <r> --plan`, `landing` rules;
- the repository's declaration files `daoris.json`, `daoris.gates.json`, `daoris.lanes.json`;
- accounts per workspace;
- the person-only acceptances of a declaration for the landed route and for integration (`WorkflowRoutes.cs`: `WORKFLOW_LANDED`, `INTEGRATION_GATES`).

**The workflow (fabric):**
- Web: `src/Daoris.Web/src/workflow/` (`WorkflowTab.tsx`, `WorkflowStartNode.tsx`, `WorkflowChart.tsx`, `WorkflowRunView.tsx`).
- The fabric's design: D173 and `docs/` files named fabric.
- The integration design: `docs/2026-10-28-integration-design.md` (D215).
- Driver: `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/`.
- Terminal: `daoris-driver workflow`, `daoris-driver integrate`.

**The UI and translation contracts:** read `docs/README.md`'s routes for them (D41, and the translation contract). The chrome is in English and Chinese; authored content stays as written.

## Constraints
- A person-only act stays the person's: accepting what an agent would run under, a Confirm, a sign-in.
  - Grouping several into one reviewed change with one Confirm is allowed where the contracts permit; say how.
- Machine-local state stays on the desktop (D172 home layout).
- Settings with a precedence (repository, workspace, machine) keep one reader of that precedence.
- Design for a person who has several workspaces and many repositories, most of which are imported but not driven.
- Keep what COMPANYVIEW1 decided unless you show why it fails. Then say what you would reverse, and why.

## What to return (a sealed proposal, about 2500 words at most)
1. **Diagnosis**, with evidence: what a person comes to each of (a), (b) and (c) to do, what the screens make them do today, and where it breaks. Give `file:line` references, and screenshots where you took them.
2. **The design:**
   - the Agents place's structure;
   - each screen, in words or ASCII;
   - the flows: first set-up of a repository, a change to its set-up, appointing a role, reading and changing the workflow;
   - the quick set-up: presets or profiles by repository kind, one plan, one review, and how its parts compose;
   - what moves, what merges and what is removed;
   - the data and route changes it needs.
3. **What you reject**, and why.
4. **Rows:** buildable pieces, each sized for one builder, with its proof.
5. **Risks and open questions.**

Write it as your own proposal. Do not read any other proposal for this round (files named `*agents-place-proposal*`). Never read any `local/` folder. Name no private repository, customer or employer.
