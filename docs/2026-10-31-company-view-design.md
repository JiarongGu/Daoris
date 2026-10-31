# COMPANYVIEW1: the agents' structure, the design (2026-10-31)

**Standing:** the steward's synthesis under D191; both makers read it next. Read against the code on `parent/batch-1031d`. It serves D193's staff (point 4) and management (point 18). Two corrections to the inputs: D137 is MSG1, so *no edge it cannot prove* is held as the map design §1 states it; and `GO_PLACES` is not a route.

## 1. Where it lives and what it is called

**Choice: a second mode of the Agents place, *Roles* / *角色*, beside today's *Agents* / *智能体*, switched by the list's head the way Knowledge switches Search and Convergence** (`knowledge/KnowledgeView.tsx`: a `Segmented` head, a strip, `chosenIn`). The Agents mode is unchanged. The Roles mode's list pane holds five sections and opens on the one last left. Its main area shows the chosen section as a list or as a diagram. The mode is desktop only, as AI features' machine half is (`settings/AiDomain.tsx:37`).

**Why:** agents run, and roles are what they fill. The glossary already names the configured position *role* 角色 in both shapes (`glossary.json:476`), and D212 point 14 names its subdomain *Roles*. **Rejected:**
- A tenth place *Roles* (Opus). D150 point 2 and the UI/UX design's R5 make a tenth place a decision with its own counts, and it would split agents from the roles they staff.
- A Settings domain *Company / 公司* (Codex). Settings keeps what belongs to the machine and the person (D150 point 1). *Company* is D193's narrative, kept off the window by D193 and D212 point 14.
- Roles mixed into the agent list.
- Tabs on an agent's page, since one role names several agents.

**The sections** (the `nav` kind's budget: en 16, zh 5):

| Section | en / zh | Here |
|---|---|---|
| Working roles | *Working roles* / *工作角色* | configured: the catalogue and its staffing |
| Appointed roles | *Appointed roles* / *任命的角色* | configured: the steward and the supervisors |
| Work | *Work* / *工作* | read: each work's role and rule |
| Accounts | *Accounts* / *账户* | read: agents, their accounts, who leans on them |
| By repository | *By repository* / *按仓库* | read: each place's answer per work |

The section is *By repository* because *Repositories* already names a place. The main area's head offers *Show as list* / *以列表显示* and *Show as diagram* / *以角色图显示*, as a flow offers *Show as flowchart* (`locales/en/flow.json:2-3`).

**Glossary rows.**
- Add *diagram* / 角色图: the roles drawn in three columns, the list beside it. 提交图, 地图 and 流程图 are taken. Avoid *graph*, *org chart* and 组织架构图.
- Add *work* / 工作: one of `RoleWork.Codes`. Avoid 任务, and 工作类型, which is *kind of task*'s word.
- Amend *role* with 工作角色 where the working shape must be said, as 任命的角色 is said for the appointed shape.
- Amend *AI features*, whose choices move here.
- The mode and section keys join the `nav` kind; the two *Show as* keys join `button`.
- *Actor* leaves the window. The English card still says *Actors* (`locales/en/settings.ai.json:28`).

## 2. The model

**Choice: roles are configured here; what they stand on is read here, with a door to its home** (D150 point 1).

| Thing | Record | Door | Here |
|---|---|---|---|
| Working role, staffing | `roles`, `role_revisions`, `role_staffing` in `data/desktop.db` | `ROLES`, `SET_ROLE`, `ACTORS`, `SET_ACTOR`, `STAFFING_FOUND_ANSWER`; `daoris driver role`, `actor`; `role_propose` | configured, moved from AI features |
| Appointed role | `appointments`, `data/standing-confirmation.json` | `STATE`; `SET_APPOINTMENT` (`Window`) | configured, moved (row e) |
| A work's role at a place | `RoleCatalog.RoleFor` | new `ROLE_STRUCTURE` | read |
| A work's rule | `WorkPolicies.Of` over `driver.json` | `SET_HELPERS`, `SET_HAND_OVER`, `SET_OPINION`; Setup | read, door |
| Agent, accounts | roster; `profiles/` | `HARNESSES`, `ACCOUNTS`; Agents mode | read, door |
| Place, line, landing | registry; `driver.json` | `LINES`; Setup | read, door |
| Workflow, plugin | current workflow; manifest | `WORKFLOW_CURRENT`, `PLUGINS` | a line, door |

**Relations, each a read:**
- A role takes a work and is placed in a scope (its record's fields), and is staffed choice by choice.
- An agent has accounts.
- A place's work goes to a role by `RoleFor` alone (`Roles/RoleCatalog.cs:151-176`): the most specific scope wins, design falls back to build, and the start is held on a tie or an unreadable record.
- A rule fences agents.
- An appointment covers its scope; a repository's own comes first.

**Rejected:**
- Editors for accounts, rules, landings or workflows (each would be a second home).
- Accounts assigned to roles (D197).
- A page-side resolution (a second truth).
- A working role holding authority (D212 point 5).
- Codex's call table per work, which is code nobody sets.

## 3. The sections

Every section:
- folds a row to one line (R1);
- opens one editor at a time;
- says a refusal under its press (R3);
- names its terminal twin (R5);
- says a failed read rather than drawing an empty list;
- probes nothing as it opens (D150 point 5).

A held start marks its section's row; the bar counts nothing new (D205 point 4). The filters are shared: workspace, work, agent, a name, *Needs a look*.

### 3.1 Working roles

**Shows:** every role, seeded and added (`RoleDoor.Rows`).

**A row says:** its name as authored, its work, its place, its first choice (agent · model · effort, *account default* where none) and *+n*.

**In place:**
- One editor: names, work, place, one to four choices with *Move up*, *Move down* and *Remove*; *Off* where allowed; *Clear* back to *as before*.
- *Remove*, *Back to the default* and *Add a role*.
- The Designer's suggestion, and the found changes.

Each press keeps its door (`SET_ROLE`; `SET_ACTOR` for *Off* and *Clear*) until MODELROLE1h folds them.

**Unset and invalid:**
- *as before*; *Off*; *Removed*, its work waiting.
- Unreadable, in the driver's sentence.
- Two roles in one scope, refused at *Save*.
- A choice naming an agent this machine lacks, said.
- A home whose `driver.json` is its own: every editor held (`settings.ai.roles.unkept`).

### 3.2 Appointed roles

**Shows:** the steward for This Daoris, the supervisor per workspace, and the supervisor per repository wherever Setup draws it today.

**A row says:** role, scope, holder, since when, answers or acts this run, and the overrule window.

**In place:** today's molecules and asks (`projects/setup/SupervisorRow.tsx`, `settings/wiring/WiringSteward.tsx`), over `SET_APPOINTMENT`. Appointing stays the owner's press alone (D208 point 1).

**Unset and invalid:** *off*; *dormant until Appoint again*; covered by its workspace's appointment; *not offered in this build*, which both roles are today (`StandingConfirmation.cs:330`, `:336`).

### 3.3 Work

**Shows:** the nine works.

**A row says:** the role taking the work here, and *n places have their own*. At the filtered scope it says *on*, *ask* or *off*; the agents permitted, or *its role's choices decide*; its bound; and where it was set.

**Door:** *Open its setting*. Helpers and Hand-over are under Setup's Sessions; Second opinion is under Line and landing; Intake's and Ask Daoris's *Off* are in Working roles.

**Unset and invalid:**
- Design *takes building's role*.
- Partnering is *not a rule yet*.
- A work no role takes, in `RoleCatalog.NoRole`'s sentence.
- A held start, in its sentence.
- A fenced agent no choice names runs at *account default* (D205 point 6).

### 3.4 Accounts

**Shows:** each agent a choice names, or that has accounts.

**A row says:** the product; its accounts, worded as the Agents list words them (`agentRows`); when they were read; *used by* each role at its choice's position; *declared by plugin …*.

**Door:** *Open*, which goes to the Agents mode at that agent's accounts.

**Unset and invalid:** a named agent not installed; a first choice that cannot run by its last reading, with its pass code (`StartPasses.Codes`). Never a probe.

### 3.5 By repository

**Shows:** workspaces as folded groups (D150 point 3), with their repositories inside.

**A row says:**
- Folded: *takes this machine's roles*, or *n works differ*.
- Opened: each work's role, its first choice and where that role is placed; then line, landing, driving and workflow; then the steward's marks (§5).

**Doors:** Setup, Workflow, and *Add a role here*, which opens the editor with the place filled.

**Unset and invalid:** a held work's sentence; *not driven*; an unread registry.

## 4. The list and the diagram

**It draws three columns:**
- **Places:** This Daoris, and each workspace or repository that sets something of its own.
- **Roles:** the working roles, and the appointed roles where offered or appointed.
- **Agents:** each agent a choice names, with its accounts' phrase.

**Edges, each from a record:**
- *placed*: place to role.
- *staffed*: role to agent, numbered, dashed where the last reading says that choice cannot run.
- *appointed*: place to appointed role, dashed while dormant.

An agent the roster lacks is drawn as written, never invented (Codex).

**Inheritance is lit, never drawn** (Opus). Choosing a place lights, per work, the role `RoleFor` answered and its staffing, and the detail says it in words. Choosing an agent lights every role leaning on it.

**Never drawn:**
- quests, asks and chains (the Map's);
- a role over a role (D193);
- a resolution as an edge;
- workflow nodes (D205 point 1);
- accounts, rules, lines and plugins;
- a running session's bound role (Codex).

**Legible at size.** A place setting nothing of its own is counted in its workspace's node (*17 take this machine's roles*), as the Map does. Past 24 nodes, each workspace folds to one node (the flowchart design §3.6). Nothing is drawn smaller, and there is no pan, zoom or drag.

**Ranked by `design/flowLayout.ts`, placed by its own rule.** Opus's proposal to reuse `placementFor` would make the diagram a list at almost every width. Across allows only three nodes a rank (`flowLayout.ts:104`), and down needs about 90rem for the eight seeded roles in one row. Here the ranks are columns. The diagram is placed across where three 10rem tracks and two 3rem gaps fit (36rem); otherwise it is its rail, the section's list with its edges said in words. `PlacementBounds` gains a caller's column bound.

**One read, one function, one owner.**
- **The read.** `ROLE_STRUCTURE` is `Open` and told by event, as `ROLES` is (`DriverRoutes.cs:367-382`). It composes the catalogue, `RoleFor` per place and work, `WorkPolicies.Of` where a place sets its own rule, and each choice's pass from the walk `STARTS` reads (`AgentsRoutes.cs:261-266`). It carries names only and probes nothing. A test holds it to `StartPlan`'s answer, as `StartWiringRouteTests` holds `STARTS`.
- **Appointments** stay `STATE`'s (`bridge/person.ts:233-259`).
- **The function.** `buildStructure` (`agents/roles/structure.ts`) makes the rows, nodes and edges with one id each, as `map/topology.ts` does.
- **The owner.** The mode's hook owns the section, the selection, the filters, the open editor and every pending press, for both views (D168 point 3). The molecules import nothing of the bridge.

**Moving between them.** The toggle keeps the selection and the filters. Choosing a node of another kind moves the section, so *Show as list* lands on that node's row. A node's detail is its row's molecule, and its *Edit* is the same editor; a row's ⋯ offers *On the diagram*. The list is for setting, scanning and reading aloud. The diagram shows which roles lean on one agent, and a place's lit path.

**By width:**
- **80rem:** the sections stand beside the page; the diagram is across, with its detail beside it.
- **39rem:** the list may close to its strip (two modes, five sections); the detail goes below the diagram.
- **25rem:** the list is a strip or lies over the page; values go under their words (R1); the diagram is its rail, and the head says so.

## 5. Setup

**Choice: a new place takes the roles above it with no press** (Opus). The seed's roles are machine-wide (`RoleCatalog.cs:62-63`), so a new workspace or repository arrives *taking this machine's roles*.

- **Gaps are said in place, not walked.** *Needs a look* gathers §3's states, each a door. Get started gains a door here.
- **First run** offers `RoleSuggestion`'s Designer: one press, whose words say it can spend more. D205 point 7's card (MODELROLE1g, unbuilt) joins it once built. Neither applies itself, and `docs/model-roles.md` is never a preset (Codex).
- **Per place:** *Add a role here*, then *Save*.
- **Appointing** a workspace's supervisor covers its later repositories; the steward covers every registered repository (D209 point 1). Both stay dark until their flags are on.
- **Adoption** stays with Setup (`SETUP_PLAN`, `SETUP_PRESS`).

**The steward's part** (D209 point 3) begins once STEWARDROLE1d activates it and the owner appoints it. It covers driving, trees on, and a set-up the owner ticked (STEWARDROLE1g). The steward acts from its terminal through `ROLE_ACT`, never by a press on this page. By repository marks each act *by the steward at …*, from `STATE`'s `stewardActs`. The owner's press at the act's home overrules it and marks the target `owner-set`. Roles, staffing, appointments, accounts, landings and rules stay the person's (the steward design §3.2). Ask Daoris proposes roles through `role_propose`; the person's press applies them.

**Rejected:** a wizard, or Codex's checklist (a second Get started); automatic appointments; a steward's act on roles.

## 6. What each proposal contributed, and what was set aside

**From Opus:**
- the split between what is configured here and what is read;
- one role, one editor first;
- `RoleFor` as the only resolution;
- inheritance lit rather than drawn;
- folds without zoom;
- the read held to `StartPlan`;
- setup with no press.

**From Codex:**
- sections by domain;
- distinct unset states;
- unresolved references named, never invented;
- configuration kept apart from running sessions;
- no account assigned to a role;
- no suggestion used as a preset.

**Set aside:**
- both placements (§1);
- Opus's tree by place, which would be a second list of 29 repositories (the shape D150 rejected for Git);
- Codex's work, line, workflow and plugin nodes, its pan and zoom, and its Plugins section;
- the name `COMPANY_STRUCTURE`;
- placement by `placementFor` (§4);
- a steward pressing a row.

## 7. The rows, smallest first

Every row's proof includes page tests; stories at 80, 39 and 25rem (`design/storyWidths.tsx`) in en and zh and both themes; and the installed window wherever the row changes it.

1. **COMPANYVIEW1a: one role, one row** (web, still in AI features). `settings/ai/Roles.tsx` takes over `Actors.tsx`'s *Off*, *Clear*, order, ⓘ and found changes. Each seeded role is drawn once, and *Actors* leaves the window; `names:check --strict` passes. Waits on nothing.
2. **1b: the Roles mode and Working roles** (web). The place's hook over both modes, its head, strip and `chosenIn`. 1a's molecule moves here, and AI features keeps a door. It also covers:
   - `PLACE_PARTS` and `HelpPlaces`;
   - `RoleCatalog.NoRole` naming Agents → Roles;
   - `HelpCoverageTests` and the help room's goldens;
   - D150 point 9's counts;
   - a note amending D205 point 8.

   After 1a.
3. **1c: the read** (driver, modules). `ROLE_STRUCTURE` in every place `.claude/knowledge/bridge-presses.md` names; the test holding it to `StartPlan`; a test that opening probes nothing. Waits on nothing.
4. **1d: Work, Accounts and By repository** (web). `buildStructure`, the filters, *Needs a look*, and the states of §3.3 to §3.5 over a shared fixture. An agent's page gains *Used by*. After 1b and 1c.
5. **1e: Appointed roles at home** (web). Both molecules move into the section; Setup and This Daoris keep a door. Notes amend the supervisor design §5 and D209 point 1. Dark until SUPERVISOR1a-iii and STEWARDROLE1d. After 1b.
6. **1f: the diagram** (web). Placement, folds, lit paths, the detail and the flowchart's keys (§3.7). Playwright reads every edge clear of nodes at 80 and 39rem, and the rail at 25rem; one Narrator pass. After 1d.
7. **1g: setup** (web). *Needs a look*, *Add a role here* on rows and workspace groups, the suggestion, and Get started's door. After 1d.
8. **1h: the steward's marks** (web, over `STATE`). After STEWARDROLE1d; the set-up mark after STEWARDROLE1g.

**Not yet verified at synthesis:** whether `SET_ROLE` writes a seeded role's first agent through to `adapter`, `intakeAdapter` and `helperAdapter` as `SET_ACTOR` does (why §3.1 keeps both doors); whether the `STARTS` walk can give a pass code per choice for every role and place without probing an account; the width fits, which are arithmetic from `FLOW_REM`, not a measured story; D205 point 6's *account default* for a fenced agent no choice names, not traced in code.
