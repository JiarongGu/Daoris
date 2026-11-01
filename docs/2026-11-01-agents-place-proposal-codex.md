# AGENTSPLACE1: Codex's sealed proposal (round 1, 2026-11-01)

Written by Codex, read-only on main at `cdd46a949`, from the brief beside it; kept as written.


## 1. Diagnosis

**Roles should answer “Who does this work here, can they run, and what can I change?”** Today the person must assemble that answer across five sections (`src/Daoris.Web/src/agents/modes.ts:15`). Working roles edits staffing; Work explains effective policy; Accounts explains dependencies; By repository explains scope. “Open its setting” usually sends the person to workspace Setup or Repositories without selecting the setting (`src/Daoris.Web/src/agents/roles/RolesView.tsx:215`). Creating a scoped role requires typing its place identifier (`src/Daoris.Web/src/agents/roles/Roles.tsx:313`). These are useful records presented as an inspection system, with incomplete paths from a finding to its remedy.

**Set-up should answer “Prepare this repository for how I intend to work.”** Today its machine settings span six sections, while adoption is a separate action on Details (`src/Daoris.Web/src/projects/setup/RepositorySetup.tsx:35`; `src/Daoris.Web/src/projects/ProjectPage.tsx:260`). Workspace defaults have another form (`src/Daoris.Web/src/projects/setup/WorkspaceSetup.tsx:30`). The adoption plan assumes prerequisites such as driving and separate trees have already been arranged (`docs/2026-10-01-workspace-setup-design.md:199`). The person supplies the ordering and discovers missing prerequisites through refusals.

**Workflow should answer “What governs new work, why, and how do I change it?”** The web draws Current and offers preset command hints; the terminal supplies creation, editing and selection (`src/Daoris.Web/src/workflow/WorkflowTab.tsx:102`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/DriverWorkflowsCommand.cs:30`). Landed verification is chosen in Setup, accepted through Workflow, and integration acceptance sits beside the Setup route. Consequently, understanding the drawing and changing its causes require different surfaces.

These findings come from source and story inspection. I took no screenshots and make no independent claim about the installed window.

## 2. The design

### Structure

Keep one Agents place, with three modes:

```text
Roles | Repositories | Agents

Repositories                 Selected repository
Search                       workspace / repository     Not driven
Workspace A                  Set-up | Workflow
  Repository …               Effective configuration
Workspace B                  …
```

Agents retains installed agents, accounts, sign-in and usage. Roles becomes the staffing and appointments surface. Repositories becomes the home for preparing and configuring agent work, with adjacent Set-up and Workflow pages.

The existing Repositories place retains ownership, knowledge, branches, history, changes and integration operations. Its configuration doors open the selected repository inside Agents, with a return destination.

The repository list includes imported, unadopted and undriven repositories. Fold by workspace; offer search and filters for Driven, Not driven and Needs a look. Default to the current workspace. Never make “not driven” itself an error or expand hundreds of imported repositories into warnings.

Selection, scope and return destination survive movement between Roles and repository configuration. Opening a surface reads recorded state; expensive inspection is an explicit action.

### Roles: staffing first, explanation beside it

Replace the five section pages with one searchable catalogue, grouped into **Working roles** and **Appointed roles**. Scope choices are All roles, This Daoris, workspace and repository.

A working-role row shows:

```text
Build     repository scope
Agent · model · effort → fallback +1
Last reading: signed out                 Change staffing
```

One role has one row, even when it takes several works. At a selected repository, inherited rows say where they come from. An inspector shows:

- Works taken, effective policy and its source.
- Ordered choices and dated availability readings.
- Repositories affected and recorded sessions using this role.
- “Change policy”, “Open accounts” and supported work-start doors.

“Change policy” opens the exact repository, workspace or machine row. “Start work” opens the existing appropriate composer with scope filled; it does not invent a generic role executor. Recorded sessions open in Sessions. Older sessions lacking a role binding remain unattributed.

Staffing changes normally require only agent, model and effort. Name, works and scope belong under “Edit role”. Scope uses registered workspace/repository selectors; unresolved existing references remain visible as written. Preserve Off, Clear staffing and Restore role defaults as distinct actions.

Retain the three-column diagram, numbered staffing edges, inheritance highlighting, folds and narrow list alternative. Keep workflow nodes and running-session relationships out of it. Its selection opens the same inspector and editor.

Appointments show coverage, holder, active/off/dormant state and available acts. The appointment flow previews scope, holder sharing, bounds and supervisor overrule time, then uses the existing owner-only window press. Restarted appointments require Appoint again. Unsupported roles explain their unavailable capability; a redesigned button cannot activate a dark feature. These remain D208’s appointments (`docs/2026-10-24-supervisor-design.md:228`).

### Repository Set-up: one effective configuration

The page starts with readiness facts: checkout, adoption, available staffing, driving and workflow. It distinguishes “configured”, “waiting for you” and “unread”.

Below are searchable, collapsible groups:

1. **Location and participation:** workspace, checkout, driving, hold, separate trees.
2. **Agents and sessions:** effective roles, helpers, hand-over, language, standing answer.
3. **Access and rules:** pauses, full access, reading/writing relationships, harness rules.
4. **Repository declarations:** doctrine, documents, gates and lanes.
5. **Delivery:** effective workflow, line and acceptance state, linking to Workflow.

Each value says **set here**, **from workspace**, **from this machine** or **default**. Editing inherited values explicitly creates an override; clearing previews the resulting inherited value.

Workspace selection presents defaults and their affected repositories. A workspace change reviews exceptions and incompatible repositories individually.

### Workflow: the governing configuration, with an editor

The page has three connected regions:

```text
Routing
  Default work → workflow
  Kind + paths → workflow
  Landed work → verification workflow

Selected workflow
  Current / named workflow / saved version
  Automatically laid-out graph + node inspector
  Add node · Connect · Set end · Review changes

Line and declarations
  Landing pattern/plugin
  Integration policy and gates
  Landed verification acceptance / integration acceptance
```

Selecting a route displays the workflow it actually names. Current is labelled live and derived. It offers “Edit contributing settings” and “Save a named copy”; it is never treated as an immutable saved version.

Named workflows support new-from-preset, copy, version history and editing. Saving appends a version. Selecting it for a scope is a separate, reviewable change that can join the same plan.

Use automatically placed nodes and typed connection selectors, with a drawer for options. Provide an equivalent ordered list editor. The palette comes from supported runtime and plugin contracts; unsupported kinds remain preserved and explained. Driver validation remains authoritative, including graph bounds and node declarations (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeGraphs.cs:199`).

Keep two policy owners visible:

- The workflow owns the unit’s process, including integration request/conflict behavior.
- The landing rule owns the line’s batching, ordering, gates and promotion policy.

That split is already explicit in D215 (`docs/2026-10-28-integration-design.md:1675`). Presenting them together must not merge their records.

Configuration and execution remain distinct. Show affected running work and links to its bound versions; retain `WorkflowRunView` for answering actual run waits (`src/Daoris.Web/src/workflow/WorkflowRunView.tsx:103`). Saving a named version never migrates existing runs. Current changes disclose their live effect.

### Quick set-up and the four flows

**First set-up.** Select an imported repository, choose Quick set-up, inspect its recorded declarations, select a profile and delivery choice, then review one composed plan. Sign-in opens its existing account flow and returns to the plan. Importing never implies driving.

Profiles combine independent parts:

| Repository kind | Suggested preparation |
|---|---|
| Library/package | Package checks and downstream-use declarations |
| Web/service | Build, test and review-environment declarations |
| Desktop/game | Build/platform checks and suitable heavy-gate separation |
| Documentation/tooling | Documentation or tool checks without invented application stages |

A separate delivery choice offers Keep current, reviewed merge, integrated branch or pull request. Participation defaults to unchanged; driving requires an explicit selection.

Profiles are versioned recipes, not runtime precedence layers. They propose fragments: documentation work, tools, staffing inheritance, session policy, workflow and verification. Existing explicit values win unless selected for replacement. Conflicting fragments require a choice before review. Repository kind never determines permissions, appointments, accounts or model names.

**Review** shows before/after values, provenance, exact commands and declarations, affected repositories, added permissions, changed human participation and deferred work. No concealed “recommended” side effects. Suggested staffing is optional and separately identified; today its card applies roles sequentially (`src/Daoris.Web/src/agents/roles/SuggestedChoicesCard.tsx:59`).

One Confirm may authorize the listed configuration changes and individually selected declaration acceptances. Each acceptance retains its own purpose and record. Appointment and sign-in stay their existing personal acts.

For a declaration already on the line, review pins its exact blob. Integration already rejects a moved blob (`src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/WorkflowRoutes.cs:294`). Extend landed-route application with the same expected-blob check; its current handler reads and accepts the tip during application (`src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/WorkflowRoutes.cs:220`).

A declaration that the repository’s future set-up quest will author cannot be accepted beforehand. It returns to the same plan after landing for fresh review. Quick set-up promises one review of known changes, not advance consent to unseen commands.

Repository files are changed by that repository’s own quest. Preserve checks before/after and existing knowledge locations (`docs/2026-10-02-setup-pilot-lessons-design.md:85`; `:141`). A setup-only request must not accidentally release unrelated queued work.

**Later set-up change.** Edit rows in place into a draft. Review the resulting configuration and apply through the same planner. Recheck any scope, declaration or configuration that changed since preview.

**Appoint a role.** Select its scope in Roles, review the existing appointment form, appoint, then show holder startup and outcome in place. Revocation and supervisor overrule remain available.

**Read/change workflow.** Select repository → Workflow → route. Read its effective graph and provenance, edit Current’s contributing settings or draft a named version, review validation and affected scopes, then save/select. Explain whole-choice replacement: a repository workflow choice does not merge individual kinds with its workspace (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Domain/WorkflowSelection.cs:80`).

### Moves, data and routes

Explicitly reverse COMPANYVIEW1’s five-section navigation and its external Setup/Workflow editor destinations. The navigation failure above and this brief’s combined ownership justify those reversals. Move Work and Accounts explanations into inspectors; merge By repository into the new repository mode. Preserve its resolution, staffing, authority and diagram decisions.

Move existing repository/workspace configuration components into the new owning screens. Remove their old mounted editors, retaining deep links. Merge duplicate landed-acceptance presentations. Keep integration **operations** in Repositories and configuration in Agents.

Proposed additions:

- `REPOSITORY_SETUP_READ`, `...PLAN`, `...APPLY`: effective values, provenance, capabilities, guarded plan and per-operation outcomes.
- Workflow catalogue/version reads and structured plan/save/select routes, sharing domain handlers with terminal commands.
- A terminal configuration-plan twin, distinct from existing adoption `setup <repository> --plan`.

Keep `SETUP_PLAN`/`SETUP_PRESS` for adoption and existing mutation semantics underneath. Classify composite application as person-only; agents may propose it, never accept declarations through it.

Persist drafts/application receipts locally, with base configuration revisions, role revisions, workflow digests and declaration blobs. Keep existing canonical stores. Resolve precedence through existing driver readers, especially `RoleFor` (`src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleCatalog.cs:151`).

Application is journalled, not falsely described as atomic across files, database and quest publication. Revalidate before effects, block new starts during application, enable driving last, and expose partial completion. Resume only unchanged authorized operations. Never replay uncertain external effects blindly.

Use D172 home entries; sign-ins stay private and local (`docs/decisions/D172.md:15`). Accounts remain the existing shared pool, rather than recreating workspace-owned accounts (`src/Daoris.Web/src/agents/AgentsView.tsx:313`).

All screens follow existing row/container-width contracts, translated chrome and unchanged authored content (`docs/2026-09-19-platform-ux.md:318`). At narrow widths, inspectors become drawers and graphs become lists.

## 3. What I reject

- **Another top-level place:** it separates staffing from its repositories.
- **A larger Roles dashboard:** more summaries do not repair action destinations.
- **A modal wizard with copied forms:** quick set-up composes the same editors and planner.
- **A universal editable company graph:** authority, staffing and workflow have different meanings.
- **Automatic adoption, appointments or no-press integration:** each requires its existing authority and prerequisites.
- **New profile precedence or page-side resolution:** both create competing answers.
- **Automatic conversion of saved workflows:** existing runs retain their engine and version (`docs/2026-10-20-fabric-cutover-design.md:56`).

## 4. Buildable rows

Each row is one builder’s bounded delivery.

| Row | Piece and proof |
|---|---|
| A | Navigation and precise returnable destinations. Page tests cover old doors, repository selection and scope preservation. |
| B | Roles catalogue and shared inspector/editor. Prove inherited roles, missing references, staffing states and diagram/list parity. |
| C | Effective setup projection. Driver fixtures compare every value and source with its execution reader; opening performs no probes. |
| D | Pure plan composition and profiles. Test conflicts, overrides, whole-rule replacement, declaration changes and imported repositories. |
| E | Journalled application. Failure/restart tests at every persistence boundary; prove no duplicate quests or premature starts. |
| F | Composite person-only bridge and terminal twin. Prove unauthorized callers refused, stale plans rejected and both acceptances distinct. |
| G | Set-up screen and review. Interaction tests cover quick setup, later edits, deferred declaration acceptance and partial outcomes. |
| H | Structured workflow catalogue/save/select services. Prove terminal parity, validation, immutable versions and unchanged bound runs. |
| I | Workflow editor. Prove branching graphs, typed wires, unsupported kinds, Current, line policy and acceptance handling. |
| J | Adoption progress and old-surface retirement. Prove setup-only execution and every legacy destination reaches its new owner. |
| K | Installed-window acceptance. Complete all four flows across several workspaces, mostly undriven repositories, refusal and restart. |

B follows A; F follows D/E; G follows C/F; I follows H. Every visible row needs stories at 80/39/25rem, English/Chinese, both themes, keyboard and measured layout proof. Installed proof remains separate from stories.

## 5. Risks and open questions

- Setup-only execution needs a narrowly scoped admission path; ordinary driving would release unrelated work.
- Journal recovery is the hardest implementation boundary and must precede combined application.
- Large registries need scoped/paged detail reads; the current structure hook requests every place (`src/Daoris.Web/src/agents/roles/structureRead.ts:9`).
- Workspace changes can affect many repositories; review must enumerate consequential exceptions.
- Profile command discovery needs explicit inspection and repository-owned verification. Kind detection alone proves nothing.
- Appointment activation remains dependent on its own containment proof; this proposal does not bypass it.