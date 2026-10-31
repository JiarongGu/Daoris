# AGENTSPLACE1: the Agents place made usable (round 1 synthesis, 2026-11-01)

**Standing:** the steward's synthesis of the round's two sealed proposals:
- the Opus maker's, `2026-11-01-agents-place-proposal-opus.md`;
- Codex's, `2026-11-01-agents-place-proposal-codex.md`.

Both answer the brief `2026-11-01-agents-place-brief.md`. The install's brainstorm on the same question is read in §8 once its passes return. The synthesis waits for both makers' reads (D191).

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

## 2. The spine (both proposals)

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
   - A plan may carry acceptances, each pinned to the blob that was read and each written to its own record. From a terminal, the plan is one Confirm card.
5. **Configuration and operation stay apart.**
   - Repositories keeps the line's operations: Details, Branches, History, Changes, Integration.
   - Agents gains the configuration.

## 3. Structure (decided: the Opus shape, with Codex's catalogue kept)

The Agents place has two modes:
- **Agents**: agents, accounts, sign-in and usage. Accounts gains *Used by*, from Accounts' section today.
- **Set-up (配置)**: replaces the Roles mode.

Its list is a tree of places, `This machine`, then each workspace with its repositories:
- driven repositories first;
- the rest folded into one row, *17 imported, not driven*;
- a search, and a *Needs a look* filter.

*Roles* stays the name of the system of roles and of each page's staffing section (the owner's choice of name). Only the mode is renamed, since it now holds a place's whole set-up. The status bar's *配置 n/5* count of Get started takes its own word in 1e.

Codex's three modes (Roles | Repositories | Agents) are not taken, but Codex's cross-place catalogue is kept. It is *This machine*'s Roles section: every role once, with its inspector and the scoped diagram. That keeps one place to see the whole company, while the question "who builds here?" is answered on the repository's own page.

A place's page has a head (driving, held, trees, the matched preset, and *Set up…*) and three sections, each folded to one line naming its values:
- **Roles:** one row per work, read from `ROLE_STRUCTURE` (`RoleFor` alone):
  - each row shows the role taking it, where it is placed, its first choice and that choice's last reading;
  - an inherited row offers *Give \<place\> its own…*; a role placed here offers *Edit* and *Back to \<parent\>'s*, with an `InlineConfirm` naming the fallback;
  - the editor asks for no ids: the place is filled and fixed, the id is generated, and agent, model and effort are chosen from the agent's own offers (`AgentOffers`), with *other…* for a typed name;
  - the work, name and scope sit under *Edit role* (Codex);
  - appointed roles (supervisor, steward) are rows here; a dark one is one quiet line.
- **Workflow:** the governing configuration:
  - the route: default work, a kind and its paths, landed work → the workflow each names (Codex's routing region, the Opus *Kinds* row);
  - the chart and start node as today;
  - one **Gates** row with both acceptances' standings, one tick accepting both, each by its pinned blob;
  - **Landing**, which gains the `integrate` form judged by `LandingKey`;
  - **Line**, **Commits**, *Second opinion*, *Your look* and *Heavy gates*;
  - *Change…* lists presets by their composed line, the saved workflows and *Current*, labelled live and derived;
  - *Edit steps* is the workflow design's §6.3 editor, with an equivalent ordered-list editor at narrow widths.
  - The two policy owners stay visible and apart (D215 at the integration design's `:1675`): the workflow owns the unit's process, and the landing rule owns the line's batching, order, gates and promotion.
- **Sessions:** today's rows moved whole: access, heavy gates and checks, language, helpers, hand-over, standing answer, reach, rules, each with *Set for this repository* or *Clear*.

## 4. The quick set-up

*Set up…* opens a page in the main area. It holds:
- **A delivery preset**, one choice: *Not driven*, *Merge after you accept*, *A branch to push*, *A pull request* (needs a landing plugin for the remote), or *Integrated into the line*.
  - One is preselected from facts the driver already reads: a declared `integration`, a landing plugin for the remote's platform, adoption.
- **Parts**, each a tick: second opinion, your look, its own roles, and *ask its session to set up for agents* (the adoption quest).
- **A kind** (Codex): library or package, web or service, desktop or game, documentation or tooling.
  - It never sets permissions, appointments, accounts or models.
  - It only shapes the adoption quest's request: which checks the repository's own session is asked to declare.
  - The repository's files stay its own quest's, and a declaration that quest will author is accepted after it lands. The What-needs-you row for a declaration owed acceptance (INTEGRATE1h) points to the place's Gates row.

**The plan** (`PLACE_PLAN`, Open) applies the parts to a copy of `driver.json` and reads before and after through today's resolvers: lines, `LandingKey`, `WorkflowSelection`, `WorkPolicies.Of`, `RoleFor` and access. It says:
- every refusal at once;
- what lowers the person's part (`InvolvementRoutes.LowersRoute`);
- §17.7's reach per repository;
- **the queued work that driving would release**, which Codex flagged as the risk of a set-up that only meant to set up;
- each declaration's blob, commit and text;
- the terminal twin's line.

Several repositories make one plan, with identical changes said once. On a repository, only the parts whose effective value differs are written.

**Apply** (`PLACE_APPLY`, Person) takes the plan's digest and the ticked blobs, then:
1. plans again, refusing `moved` where anything changed;
2. reads the tips, refusing `moved` where a ticked blob is no longer the tip's;
   - `WORKFLOW_LANDED` gains the same expected-blob check that `INTEGRATION_GATES` has (`WorkflowRoutes.cs:220` against `:294`; Codex);
3. saves any named workflow; a saved one unused is harmless;
4. writes every `driver.json` change, acceptances included, in one `DriverContext.Change`, all judged first;
5. publishes the adoption quest last.

A quest that cannot be published leaves the plan applied, says so, and offers its retry. There is no journal: one file write is the only effect that needs to be whole (the Opus position). Codex's journalled application is rejected for this reason, and its partial-completion outcome is kept for step 5.

## 5. The flows

- **First set-up:** Set-up → the workspace → *n imported, not driven* → the repository → *Set up…* → keep the suggestion → read the plan, tick the declaration → *Apply*. The head then names the preset, and the adoption quest shows in Sessions.
- **A change:** a row's *Edit* → *Save*. Or *Change…* → another preset → the plan → *Apply*.
- **Staffing:** the repository → Roles → Build → *Give it its own…* → choose the agents in order → *Save* (`SET_ROLE`). An appointment is the workspace's Roles → Supervisor → *Appoint…*, today's owner-only press.
- **The workflow:** the repository → Workflow shows the chart, why it is the one chosen, and the gates' standing. *Edit steps* → *Save* makes a new version, and running work keeps its own. Choosing the new version is a plan of one.

## 6. What moves, merges and goes

- **Moves:**
  - a repository's and a workspace's Setup and Workflow tabs move into their place pages;
  - *Set up for agents* becomes the plan's adoption part;
  - every door into them is re-pointed (What needs you, Get started, the help room, the chart's step doors).
- **Merges:**
  - Working roles, Work and By repository become place pages;
  - Accounts becomes the Agents mode;
  - Appointed roles become rows;
  - the two acceptances become one Gates row;
  - the Designer's and MODELROLE1g's suggestions become one *Suggested* line applied as a plan.
- **Goes:** the section list and strip, the per-section filter bars, the typed place id, presets as terminal commands, and the editor's prose.
- **Kept from COMPANYVIEW1:**
  - `RoleFor` as the only resolution, and the one read;
  - inheritance lit, not drawn;
  - no account assigned to a role;
  - the read-only call table;
  - a new place taking roles with no press;
  - the three-column diagram (on *This machine*'s and a workspace's page, for that scope).

## 7. Rows

**Every visible row's proof:** page tests; stories at 80, 39 and 25rem, in en and zh and both themes, with keyboard and measured layout; the installed window where it changes.

1. **AGENTSPLACE1a, presets and the plan** (driver, modules): `SetupPresets` beside `WorkflowPresets`, `PLACE_PLAN`, `daoris-driver place <name> --preset … --plan`.
   - Proof: a test per preset over fixtures (solo, declares integration, a remote with a plugin, without one refused); before and after equal to each resolver; the released queued work said; nothing written or probed.
2. **1b, the apply** (modules): `PLACE_APPLY`, Person, in every place `bridge-presses.md` names, with the expected-blob check on `WORKFLOW_LANDED`.
   - Proof: the census marks it Person, with no standing cover; a role's act is refused; one write; `moved` for the digest and the blob; the step order; a quest that fails to publish leaves the plan applied and says so; a terminal card is confirmed once.
   - After 1a.
3. **1c, workflow writes as routes** (driver, modules): `WORKFLOW_SAVE` and `WORKFLOW_USE`, Person, sharing the verb's handlers (D194 point 4, amending its PUSH1f2b note).
   - Proof: the verb's text cases pass, and §17.7's reach sentences are said.
4. **1d, `integrate` in the landing rule** (modules, web): `SET_LANDING`'s key and its form.
   - Proof: `LandingKey`'s refusals under *Save*.
5. **1e, the Set-up mode and its list** (web, modules): the modes (`['agents','setup']`, with a kept `roles` read as `setup`), the place tree, `PLACES` (Open, by event, scoped and paged; Codex's risk on `structureRead.ts:9`), the not-driven fold and the marks, plus the status bar's own word.
6. **1f, the page head and Sessions** (web): rows moved from `RepositorySetup` and `WorkspaceSetup`; Repositories loses two tabs and gains the line.
   - Proof: every door re-pointed, held by `HelpCoverageTests` and a door census.
   - After 1e.
7. **1g, Roles on a place** (web): per-work rows; *Give … its own* with a generated id and offers; *Back to …* confirmed; *This machine*'s catalogue with its inspector and the scoped diagram; the suggestion as one plan; appointed rows.
   - After 1a, 1b and 1e.
8. **1h, Workflow on a place** (web): the route, the chart, one Gates row, `integrate`, *Change…*.
   - After 1c, 1d and 1e.
9. **1i, the step editor** (web): the workflow design's §6.3 here, with the ordered-list editor and its versions said.
   - After 1h.
10. **1j, *Set up…*** (web): the preset, parts, kind, plan, ticks and Apply; one plan over several repositories; refusals in place.
    - After 1a, 1b and 1e.
11. **1k, Ask Daoris and the records:** the `setup` kind's `place` door proposing a plan card; goldens; the glossary (*preset / 预设*, the mode); notes amending D150 points 3–4, D157 §6.1 and COMPANYVIEW1 §1 and §3; the guide's verb.
    - After 1j.
12. **1l, the installed window** (the steward): the four flows over several workspaces with mostly undriven repositories, a refusal, and a restart mid-review.

## 8. The brainstorm, and what stays open

- **The install's brainstorm** (`1f2a4e0e`) ran on a packet that missed the designs this question names (BRAINSTORMPACKET1). What it adds is read here when it returns.
- **Open:**
  - an *Undo* plan from the last plan's before values;
  - whether the Integration tab's queue sits beside the Gates row;
  - carrying a generated role id across a repository's rename.
