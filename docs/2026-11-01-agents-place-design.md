# AGENTSPLACE1: the Agents place made usable (accepted 2026-11-01)

**Standing:** accepted under D191 on 2026-11-01. Round 1's two sealed proposals were:
- the Opus maker's, `2026-11-01-agents-place-proposal-opus.md`;
- Codex's, `2026-11-01-agents-place-proposal-codex.md`.

Both answer the brief `2026-11-01-agents-place-brief.md`. The steward synthesized them, both makers read the synthesis and accepted it with named changes, and the steward settled those changes in `2026-11-01-agents-place-reads.md`. This text is the design as settled.

## 1. What both proposals found

The three surfaces are organized by their data, not by the person's question.
- **Roles** answers "who does this work here, can it run, what can I change?" only across three read-only lists:
  - Work, Accounts and By repository (`agents/roles/RoleSections.tsx:18-47`, `StructureSections.tsx:91`);
  - its one editor asks for a typed place id, model and effort (`Roles.tsx:313-361`);
  - *Remove* has no confirmation (`:446-455`);
  - *Use these choices* applies role by role and stops at the first refusal (`SuggestedChoicesCard.tsx:59-73`);
  - *Open its setting* opens a whole place rather than the setting (`RolesView.tsx:215-219`).
- **Set-up** of one repository takes about fifteen presses across six screens, each written at once and never reviewed together:
  - *Set up for agents* on Details refuses until Drive and A tree per session are on, and those live on another tab (`projects/ProjectPage.tsx:245-260`, `projects/setup/RepositorySetup.tsx:343-377`);
  - a landing rule's `integrate` policy has no control in the window (`Settings/LandingKey.cs:63-76`).
- **Workflow** is read-only in the window:
  - "No route writes a named workflow or chooses one" (`Workflows/DriverWorkflowsCommand.cs:16-17`);
  - four of the integration design's ten combinations of workspace choice, repository choice and landing rule cannot start (§17.7), which the person learns only as stalled work;
  - one declaration is accepted in two places by two routes (`WorkflowRoutes.cs:196`, `:262`).

## 2. The spine

1. **One page per place.**
   - A place is this machine, a workspace or a repository.
   - The page says what the place does: who takes each work, how work moves and lands, and how its sessions run.
   - Each value says where it comes from: *set here*, *from the workspace*, *from this machine* or *default*.
2. **Any change of more than one row goes through one plan, one review and one Apply.** The plan is read through the driver's existing resolvers, one reader per precedence. A single row's *Save* stays one press.
3. **Presets are recipes, never a stored level.**
   - A preset proposes changes that existing doors make. Nothing records that a place follows a preset.
   - The page derives which preset a place matches, or says *n changes of your own*.
4. **The person's acts stay theirs.**
   - Accepting a declaration, appointing, signing in and Confirm are never taken by an agent or a role.
   - A plan may carry acceptances, each chosen for its own purpose, pinned to the blob that was read, and written to its own record. From a terminal, the plan is one Confirm card.
5. **Configuration and operation stay apart.**
   - Repositories keeps the line's operations: Details, Branches, History, Changes, Integration.
   - Agents gains the configuration.

## 3. Structure

The Agents place has two modes:
- **Agents**: agents, accounts, sign-in and usage. Accounts gains *Used by*, from Accounts' section today.
- **Setup (配置)**: replaces the Roles mode.
  - Its English name is *Setup*. The glossary's *set-up* is 搭建, the review environment's step, and the mode's row amends that entry.

The mode's list is a tree of places, `This machine`, then each workspace with its repositories:
- driven repositories first;
- the rest folded into one row, *17 imported, not driven*;
- a search, and a *Needs a look* filter judged in the driver from the configuration alone.

*Roles* stays the name of the system of roles and of each page's staffing section.

**All roles**, on *This machine*'s page, is the cross-place catalogue:
- every role once, with scope filters, set apart from the roles placed machine-wide;
- its inspector and the scoped diagram;
- **one editor per role:** a role is edited on its own place's page, and in *All roles* a role placed elsewhere is read-only, with *Open at \<place\>*;
- opening a policy, account or session keeps the selection, the scope and the way back.

A place's page has a head (driving, held, trees, the matched preset, and *Set up…*) and three sections, each folded to one line naming its values.

**Roles:** one row per work, read from `ROLE_STRUCTURE` (`RoleFor` alone) for the chosen place's chain only.
- Each row shows the role taking it, where it is placed, its first choice and that choice's last reading.
- A row taken from a parent offers *Give \<place\> its own…*; a role placed here offers *Edit* and *Back to \<parent\>'s*, with an `InlineConfirm` naming the fallback.
- *Off*, *Clear staffing* and *Restore role defaults* stay three acts.
- The editor asks for no ids: the place is filled and fixed, the id is generated, and agent, model and effort are chosen from the agent's own offers (`AgentOffers`), with *other…* for a typed name. The work, name and scope sit under *Edit role*.
- Appointed roles (supervisor, steward) are rows here; a dark one is one quiet line.

**Workflow:** the governing configuration.
- **The route:** default work, a kind of task and its paths, and landed work, each to the workflow it names. The kinds stay read-only until `WORKFLOW_KIND` lands. The landed route is chosen here and accepted in Gates.
- **The chart and start node,** as today.
- **Gates:** the declaration shown once, with two choices, *Accept for landed verification* and *Accept for integration*. Each names its purpose, blob and resulting standing, and keeps its own record and withdrawal; one Confirm applies the ones selected.
  - Changed landed gates stop verification (`LandedGates.cs:29`).
  - Integration goes on under its last accepted blob (`IntegrationAcceptance.cs:31`).
- **Landing,** which gains the `integrate` form judged by `LandingKey`.
- **Line, Commits,** *Second opinion*, *Your look* and *Heavy gates*.
- ***Change…*** lists presets by their composed line, the saved workflows, and *Current*, labelled live and derived.
- ***Edit steps*** edits a graph version's node options, and adds or removes the second opinion and your look by `WithGraphGates`' rule. An ordered list replaces it at narrow widths. Adding nodes and wires needs its own design.
- **The two policy owners stay visible and apart** (D215, the integration design's `:1675`): the workflow owns the unit's process, and the landing rule owns the line's batching, order, gates and promotion.

**Sessions:** today's rows moved whole: access, heavy gates and checks, language, helpers, hand-over, standing answer, reach and rules, each with *Set for this repository* or *Clear*.

## 4. The quick set-up

*Set up…* opens a page in the main area with three parts kept apart.
- **Delivery,** one choice: *Not driven*, *Merge after you accept*, *A branch to push*, *A pull request* (it needs a landing plugin for the remote), or *Integrated into the line*.
  - One is preselected from facts the driver already reads: a declared `integration`, a landing plugin for the remote's platform.
  - Second opinion and your look are ticks.
- **Adoption**, *ask its session to set up for agents*: a tick.
  - While it is ticked, **What it builds** shows: library or package, web or service, desktop or game, documentation or tooling.
  - That choice shapes only the adoption quest's request, which checks the repository's own session is asked to declare. It never sets permissions, appointments, accounts or models.
  - The repository's files stay its own quest's. A declaration that quest will author is accepted after it lands: the What-needs-you row for a declaration owed acceptance (INTEGRATE1h) points to the place's Gates row.
- **General driving**, *Enable general driving*: a separate choice, off unless chosen.
  - The adoption quest is admitted alone, by its quest, while general driving stays as it was.
  - When driving is chosen, the plan lists the queued work it releases, by title, and offers *Hold it after driving*.

Roles are not a part of a preset. The *Suggested* line on Roles is its own batch `SET_ROLES`, judged whole in one desktop-store transaction.

**The plan** (`PLACE_PLAN`, Open) applies the parts to a copy of the configuration and reads before and after through today's resolvers: lines, `LandingKey`, `WorkflowSelection`, `WorkPolicies.Of` and access. It says:
- every refusal at once;
- what lowers the person's part (`InvolvementRoutes.LowersRoute`);
- §17.7's reach per repository;
- the work it releases;
- each declaration's blob, commit and text, and each acceptance's purpose;
- the terminal twin's line, `daoris-driver place <name> --preset … --plan`.

Several repositories make one plan, with identical changes said once. On a repository, only the parts whose effective value differs are written.

**Apply** (`PLACE_APPLY`, Person) takes the plan's digest and the chosen acceptances.
1. **It writes a durable application receipt** (a home entry under `data/`) before its first effect. The receipt lists every authorized operation, and each one's outcome is recorded as done, failed or uncertain.
2. **It plans again** under the configuration's write guard, refusing `moved` where anything changed.
3. **It reads the tips,** refusing `moved` where a chosen blob is no longer the tip's.
   - `WORKFLOW_LANDED` takes the same check as `INTEGRATION_GATES`, through an optional expected-blob field (`WorkflowRoutes.cs:220` against `:294`).
4. **It saves any named workflow** under a fresh id. Where it appends to an existing one, the plan has already said and authorized every scope that reaches (`WorkflowRunBindings.cs:124`).
5. **It writes the configuration's changes, acceptances included,** in one `DriverContext.Change`, all judged first.
6. **It publishes the adoption quest last,** keyed by the plan's digest.
   - A retry after an uncertain publication finds the quest already published rather than making a second.
   - Its admission holds the repository's other work, as above.

At a restart, the receipt is read again: what is done stays done, and an uncertain step is settled by its postcondition (the configuration's values, the quest found by its key), never replayed blind. The outcome is said precisely, for example *configuration applied; adoption pending*.

## 5. The flows

- **First set-up:** Setup → the workspace → *n imported, not driven* → the repository → *Set up…* → keep the suggestion → read the plan, choose the acceptances → *Apply*. The head then names the preset, and the adoption quest shows in Sessions.
- **A change:** a row's *Edit* → *Save*. Or *Change…* → another preset → the plan → *Apply*.
- **Staffing:** the repository → Roles → Build → *Give it its own…* → choose the agents in order → *Save* (`SET_ROLE`).
- **An appointment:** the workspace's Roles → Supervisor → *Appoint…*, today's owner-only press.
- **The workflow:** the repository → Workflow shows the chart, why it is the one chosen, and the gates' standing. *Edit steps* → *Save* makes a new version, and running work keeps its own. Choosing the new version is a plan of one.

## 6. What moves, merges and goes

- **Moves:**
  - a repository's and a workspace's Setup and Workflow tabs move into their place pages;
  - *Set up for agents* becomes the adoption part;
  - every door into them is re-pointed: What needs you, Get started, the help room, the chart's step doors.
  - **A removal waits for its replacement.** The Setup mode stays dark, or the old Roles sections stay reachable, until Roles on a place merges. Repositories' Setup tab goes with the page head row; its Workflow tab goes with Workflow on a place.
- **Merges:**
  - Working roles, Work and By repository become place pages and *All roles*;
  - Accounts becomes the Agents mode;
  - Appointed roles become rows;
  - the two acceptances share one Gates row as two choices;
  - the Designer's and MODELROLE1g's suggestions become one *Suggested* line.
- **Goes:** the section list and strip, the per-section filter bars, the typed place id, presets as terminal commands, and the editor's prose.
- **Kept from COMPANYVIEW1:**
  - `RoleFor` as the only resolution, and the one read;
  - inheritance lit, not drawn;
  - no account assigned to a role;
  - the read-only call table;
  - a new place taking roles with no press;
  - the three-column diagram (on *This machine*'s and a workspace's page, for that scope).

## 7. Rows

**Every visible row's proof:** page tests; stories at 80, 39 and 25rem, in en and zh and both themes, with keyboard and measured layout; the installed window where it changes. Each row writes its own decision note and its own translations.

1. **AGENTSPLACE1a0, the landed acceptance pinned** (modules, web): `WORKFLOW_LANDED`'s optional expected blob, and today's *Accept the gates* sending it.
   - Proof: `moved` refused, and an older caller unchanged.
   - Waits on nothing.
2. **1c, workflow writes as routes** (driver, modules): `WORKFLOW_SAVE`, `WORKFLOW_USE` and `WORKFLOW_KIND`, Person, sharing the verb's handlers; a fresh id, or the scopes an append reaches said. D194's note.
   - Proof: the verb's text cases, and §17.7's reach sentences.
3. **1d, `integrate` in the landing rule** (modules, web): `SET_LANDING`'s key and its form.
   - Proof: `LandingKey`'s refusals under *Save*.
4. **1a, the effective projection** (driver, modules, web): `PLACES` (Open, by event, paged, *Needs a look* from the configuration); `ROLE_STRUCTURE` asked for the chosen place's chain; the eager consumer replaced (`structureRead.ts:10`).
   - Proof: every value and its source equal to its execution reader; opening probes nothing.
5. **1a2, presets and the plan** (driver, modules): `SetupPresets` beside `WorkflowPresets`; `PLACE_PLAN`; `place --plan`; *What it builds* in the adoption quest's body (`SETUP_PLAN`, `SETUP_PRESS` and their fixture); the released work listed.
   - Proof: a test per preset over fixtures (solo, declares integration, a remote with a plugin, one without refused); nothing written or probed.
   - After 1a, 1c and 1d.
6. **1b, setup-only admission** (driver): the adoption quest admitted alone while general driving stays off.
   - Proof: queued and newly arriving unrelated work held through apply and restart.
7. **1b2, the recoverable apply** (driver, modules): `PLACE_APPLY`'s steps, the receipt's home entry, the quest keyed by the digest, the restart's settling by postconditions.
   - Proof: a stop at every boundary leaves a precise, recoverable outcome; no second quest; no early start.
   - After 1a2 and 1b.
8. **1b3, the apply's authority** (modules): `PLACE_APPLY` and the terminal twin in every place `bridge-presses.md` names.
   - Proof: the census marks it Person, with no standing cover; a role's act is refused; a stale plan is refused; the two acceptances stay distinct; a terminal card is confirmed once.
   - After 1b2.
9. **1e, the Setup mode and its list** (web, modules): the modes (`['agents','setup']`, with a kept `roles` read as `setup`), the place tree from `PLACES`, the not-driven fold and its marks, the status bar's own word, and the glossary's *Setup* row. The mode is dark until 1g.
   - After 1a.
10. **1g, All roles and the role editor** (web): the catalogue, its scope filters, the inspector, one editor per role, *Off*, *Clear staffing* and *Restore role defaults*, and the kept destinations. COMPANYVIEW1's note.
    - After 1e.
11. **1g1, Roles on a place** (web): per-work rows, *Give … its own*, *Back to …* confirmed, appointed rows, and the scoped diagram. The old Roles sections go.
    - After 1g.
12. **1f, the page head and Sessions** (web): rows moved from `RepositorySetup` and `WorkspaceSetup`; Repositories loses its Setup tab. D150's note.
    - Proof: every door re-pointed, held by `HelpCoverageTests` and a door census.
    - After 1e.
13. **1h, Workflow on a place** (web): the route, the chart, Gates with its two choices, Landing with `integrate`, and *Change…*. Repositories loses its Workflow tab. D157's note.
    - After 1a2, 1b2, 1c, 1d and 1e.
14. **1j, *Set up…*** (web): delivery, adoption with *What it builds*, general driving, the plan, the acceptances and Apply; one plan over several repositories; refusals in place.
    - After 1b3 and 1e.
15. **1g2, the suggestion as one change** (driver, modules, web): `SET_ROLES`, judged whole in one desktop-store transaction.
    - After 1g.
16. **1i, the step options** (web): a graph version's node options, and the second opinion and your look added or removed by `WithGraphGates`.
    - After 1h.
17. **1k, Ask Daoris:** the `setup` kind's `place` door proposing a plan card, its goldens, and the guide's verb.
    - After 1j.
18. **1l, the installed window** (the steward): the four flows over several workspaces with mostly undriven repositories, a refusal, a restart mid-review, an interrupted write and an uncertain publication.

## 8. The brainstorm, and what stays open

- **The install's brainstorm** (`1f2a4e0e`) was asked of Claude and Codex on a packet that missed the designs this question names. BRAINSTORMPACKET1 fixed how a packet is chosen. It ended with no answer, since no agent could take it (its packet kept), so the design rests on the two sealed proposals and their reads alone.
- **Open:**
  - an *Undo* plan from the last plan's before values;
  - whether the Integration tab's queue sits beside the Gates row;
  - carrying a generated role id across a repository's rename.
