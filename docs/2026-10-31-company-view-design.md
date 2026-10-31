# COMPANYVIEW1: the agents' structure, the design (2026-10-31)

**Standing:** accepted 2026-10-31 after the steward's named changes. It is the steward's synthesis under D191; both makers read it and accepted after named changes (`2026-10-31-company-view-reads.md`), and the changes are written in below. Read against the code on `parent/batch-1031d`. It serves D193's staff (point 4) and management (point 18). Two corrections to the inputs: D137 is MSG1, so *no edge it cannot prove* is held as the map design §1 states it; and `GO_PLACES` is not a route.

## 1. Where it lives and what it is called

**Choice: a second mode of the Agents place, *Roles* / *角色*, beside today's *Agents* / *智能体*, switched by the list's head the way Knowledge switches Search and Convergence** (`knowledge/KnowledgeModes.tsx`'s `Segmented` head, with `knowledge/KnowledgeView.tsx`'s strip and `chosenIn`). The Agents mode is unchanged. The Roles mode's list pane holds five sections and opens on the one last left. Its main area shows the chosen section as a list or as a diagram. The mode is desktop only, as AI features' machine half is (`settings/AiDomain.tsx:37`).

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
- Add *work* / 工作: one of `RoleWork.Codes`. Avoid 任务, and 工作类型, which is *kind of task*'s word. Its `match` is narrow: *work* in English is everywhere, and 工作 begins 工作区, 工作流 and 工作记录 (`glossary.json:504`, `:522`, `:572`), so the row matches the section's and a row's own keys only.
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
| What a work may call | `CallTable` (`src/Daoris.Service/Daoris.Service.Mcp/CallTable.cs:95`), code | none | read only (row i) |
| Agent, accounts | roster; `profiles/` | `HARNESSES`, `ACCOUNTS`; Agents mode | read, door |
| Place, line, landing | registry; `driver.json` | `LINES`; Setup | read, door |
| Workflow, integration declaration | current workflow; its declaration (`workflow/current.ts:109`) | `WORKFLOW_CURRENT`; the Workflow and Integration tabs, where acceptance stays (`settings/repository/IntegrationDeclaration.tsx:18`) | a line each, door |
| Plugin | manifest | `PLUGINS`; Plugins | a line (the agents it declares, a landing it names), door |

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
- An editor for the call table, which is code nobody sets; what each work may call is shown read-only instead (row i), since the works differ in it.

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

**Doors, per press.**
- *Save* goes by `SET_ROLE`, which writes a seeded role's first agent through to the old keys as `SET_ACTOR` does (`Roles/RoleDoor.cs:60-64`, `Roles/RoleStaffing.cs:325-337`).
- *Off*, where the work allows it, is `SET_ROLE` with `choices: []` for a seeded or an added role (`RoleDoor.cs:86-87`).
- *Clear* (a seeded role's staffing back to *as before*, `choices` null) has no `SET_ROLE` form and stays `SET_ACTOR`'s (`DriverRoutes.cs:350-353`); an added role has no *as before* and offers no *Clear* (`Settings/ActorKey.cs:58` refuses its id).
- *Clear* is the staffing's; *Back to the default* is the catalogue's (a seeded role's names, work and place restored). The two are worded apart.
- A home whose `driver.json` is its own: `SET_ROLE` refuses there (`DriverRoutes.cs:390-391`) while `SET_ACTOR` writes the file (`Modules/Bridge/DriverContext.cs:156-162`), so the seeded rows keep `SET_ACTOR`'s editor and only *Add a role*, *Remove* and the place are held (`settings.ai.roles.unkept`).
- An older shell answering `ACTORS` but not `ROLES` (`bridge/driver.ts:544`) keeps one staffing editor over `SET_ACTOR`, and the section tells *not offered by this shell*, a failed read and an empty catalogue apart.

These fold into one door when MODELROLE1h does.

**Unset and invalid:**
- *as before* (a seeded role only); *Off*; *Removed*, its work waiting.
- Unreadable, in the driver's sentence.
- Two roles in one scope, refused at *Save*.
- A choice naming an agent this machine lacks, said.

### 3.2 Appointed roles

**Shows:** the steward for This Daoris, the supervisor per workspace, and the supervisor per repository wherever Setup draws it today.

**A row says:** role, scope, holder, since when, answers or acts this run, and the overrule window.

**In place:** today's molecules and asks (`projects/setup/SupervisorRow.tsx`, `settings/wiring/WiringSteward.tsx`), over `SET_APPOINTMENT`. Appointing stays the owner's press alone (D208 point 1).

**Unset and invalid:** *off*; *dormant until Appoint again*; covered by its workspace's appointment; *not offered in this build*, which both roles are today (`StandingConfirmation.cs:330`, `:336`).

### 3.3 Work

**Shows:** the nine works.

**A row says:** the role taking the work here, and *n places have their own*. At the filtered scope it says the effective rule, inherited or default where the place sets none: *on*, *ask* or *off*; the agents permitted, or *its role's choices decide*; its bound; and where it was set (the repository, its workspace, this machine or the default). Opened, it names what the work may call (row i).

**Door:** *Open its setting*. Helpers and Hand-over are under Setup's Sessions; Second opinion is under Line and landing; Intake's and Ask Daoris's *Off* are in Working roles.

**Unset and invalid:**
- Design *takes building's role*.
- Partnering is *not a rule yet*.
- A work no role takes, in `RoleCatalog.NoRole`'s sentence.
- A held start, in its sentence.
- A fenced agent no choice names runs at *account default* (D205 point 6; `rule-default` in `Roles/ActorChoices.cs:7`, `:55`, `:83`, `Participants/Domain/ReviewerChoice.cs:172`, `Participants/Helpers/HelperChoice.cs:38`).

### 3.4 Accounts

**Shows:** each agent a choice names, or that has accounts.

**A row says:** the product; its accounts, worded as the Agents list words them (`agentRows`); when they were read; *used by* each role at its choice's position; *declared by plugin …*.

**Door:** *Open*, which goes to the Agents mode at that agent's accounts.

**Unset and invalid:** a named agent not installed; a choice that cannot run by its last reading, with its code and the reading's time; a choice *not read yet* where no reading exists. Never a probe.

### 3.5 By repository

**Shows:** workspaces as folded groups (D150 point 3), with their repositories inside.

**A row says:**
- Folded: *takes this machine's roles*, or *n works differ*.
- Opened: each work's role, its first choice and where that role is placed; then line, landing, driving and workflow; its integration declaration's state (none, proposed or accepted, with a door to the Integration tab, where acceptance stays); the plugins whose agents or landing it uses (a door to Plugins); then the steward's marks (§5).

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

**Legible at size.** A place setting nothing of its own is counted in its workspace's node (*17 take this machine's roles*), as the Map does. Past 24 nodes, each workspace folds to one node: new code beside `design/flowFolds.ts`, which folds a process's stacks and stretches, not places. Nothing is drawn smaller, and there is no pan, zoom or drag.

**Ranked by `design/flowLayout.ts`, placed by its own rule.** Opus's proposal to reuse `placementFor` would make the diagram a list at almost every width. Across allows only three nodes a rank (`flowLayout.ts:104`, hard-coded in `placementFor` at `:286`), and down needs about 90rem for the eight seeded roles in one row. Here the ranks are columns. A node's floor is 12rem, since a role node names *Claude Code · sonnet · high*; across then needs three tracks and two 3rem gaps, 42rem of the main area (`work/frame/ViewMain.tsx`'s inner width). It is placed across where that fits, which is 80rem and not 39rem (576px inner, `design/storyWidths.tsx:13`, `:18`); otherwise it is its rail, the section's list with its edges said in words. `PlacementBounds` (`:273`) gains a caller's column bound, a signature change.

**One read, one function, one owner.**
- **The read.** `ROLE_STRUCTURE` is `Open` and told by event, as `ROLES` is (`DriverRoutes.cs:367-382`). It composes, in .NET:
  - the catalogue, and `RoleFor` per requested place and work;
  - `WorkPolicies.Of` for every requested work and place, inherited and default answers included, each with its provenance and a per-item failure (`Participants/Domain/WorkPolicy.cs:94`, `:110`);
  - each choice's state from the last readings alone: `AccountReads.Of`, the cool-off states, `AgentOffers.Judge` and `ReportAsync(refresh: false)`, each with its reading's time, and *not read yet* where none exists. It never calls `ChooseAsync` or `Pass`, and is not `STARTS`: that walk reads only a workspace's Build and Intake adapters (`AgentsRoutes.cs:278-291`), stops at the first runnable choice (`Roles/StartPlan.cs:309`), and can ask a signed-out account or the binary even with `counts: false` (`Agents/HarnessRoster.cs:1283-1288`, `:1359-1370`, `:1556-1562`).
  
  It carries names only. It is a projection of the configuration, apart from a start's selection; a test holds its role answers to `StartPlan`'s over a shared fixture, and another holds that opening it runs no probe and takes no reservation.
- **Appointments** stay `STATE`'s (`bridge/person.ts:233-259`).
- **The function.** `buildStructure` (`agents/roles/structure.ts`) makes the rows, nodes and edges with one id each, as `map/topology.ts` does.
- **The owner.** The mode's hook owns the section, the selection, the filters, the open editor and every pending press, for both views (D168 point 3). The molecules import nothing of the bridge.

**Moving between them.** The toggle keeps the selection and the filters. Choosing a node of another kind moves the section, so *Show as list* lands on that node's row. A node's detail is its row's molecule, and its *Edit* is the same editor; a row's ⋯ offers *On the diagram*. The list is for setting, scanning and reading aloud. The diagram shows which roles lean on one agent, and a place's lit path.

**By width:**
- **80rem:** the sections stand beside the page; the diagram is across, with its detail beside it.
- **39rem:** the list may close to its strip (two modes, five sections); the diagram is its rail, with the detail below.
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
- Opus's roles under their places: By repository keeps the places' tree, but the roles stay one flat catalogue in Working roles, so one role has one row and one editor in one section;
- Codex's work, line, workflow and plugin nodes, its pan and zoom, and its Plugins section (plugins and integration declarations are lines in By repository and Accounts instead);
- the name `COMPANY_STRUCTURE`;
- placement by `placementFor` (§4);
- a steward pressing a row.

## 7. The rows, smallest first

Every row's proof includes page tests; stories at 80, 39 and 25rem (`design/storyWidths.tsx`) in en and zh and both themes; and the installed window wherever the row changes it.

1. **COMPANYVIEW1a: one role, one row** (web, still in AI features). `settings/ai/Roles.tsx` takes over `Actors.tsx`'s *Off*, *Clear*, order (*Move up*, *Move down*, `Actors.tsx:195`), ⓘ and found changes, with §3.1's doors per press, the unkept home and the older shell. Each seeded role is drawn once, and *Actors* leaves the window; `names:check --strict` passes. Waits on nothing.
2. **1b: the Roles mode and Working roles** (web). The place's hook over both modes, its head, strip and `chosenIn`. 1a's molecule moves here, and AI features keeps a door. It also covers:
   - `PLACE_PARTS` and `HelpPlaces`;
   - `RoleCatalog.NoRole`'s sentence (`RoleCatalog.cs:183`) naming Agents → Roles;
   - `HelpCoverageTests` and the help room's goldens;
   - D150 point 9's counts;
   - notes amending D150 point 4 (the Agents place now holds the roles its agents fill) and D205 point 8.

   After 1a.
3. **1c: the read** (driver, modules). `ROLE_STRUCTURE` as §4 states it, in every place `.claude/knowledge/bridge-presses.md` names; the test holding its role answers to `StartPlan`; tests for a signed-out account overdue for rereading, a later choice, a scoped role and an inherited rule with no local override; the test that opening runs no probe and takes no reservation. Waits on nothing.
4. **1d: Work, Accounts and By repository** (web). `buildStructure`, the filters, *Needs a look* (its one owner), and the states of §3.3 to §3.5 over a shared fixture, the integration declaration's and the plugins' lines included. An agent's page gains *Used by*. After 1b and 1c.
5. **1e: Appointed roles at home** (web). Both molecules move into the section; Setup and This Daoris keep a door. Notes amend the supervisor design §5 and D209 point 1. Built dark; active as SUPERVISOR1a-iii and STEWARDROLE1d activate each. After 1b.
6. **1f: the diagram** (web). Placement with the column bound, the workspace fold (new), lit paths, the detail and the flowchart's keys (§3.7); an appointed node selects its section. Playwright reads every edge clear of nodes at 80rem, and the rail at 39 and 25rem; one Narrator pass. After 1d and 1e.
7. **1g: setup** (web). *Add a role here* on rows and workspace groups, the suggestion, and Get started's door; *Needs a look* stays 1d's. After 1d.
8. **1h: the steward's marks** (web, over `STATE`). After 1d and STEWARDROLE1d; the set-up mark after STEWARDROLE1g.
9. **1i: what a work may call** (service, driver, web). The call table's answer per work, read-only, in Work's opened row; its read is named by the row (the call table lives in the service). After 1d.

**Settled at reading:** `SET_ROLE` writes a seeded role's first agent through (§3.1); `STARTS` cannot give a choice's state without probing, so the read composes from the last readings (§4); the widths are arithmetic, held by 1f's measured stories; *account default* is `rule-default` in the code (§3.3). Whether `STARTS` asks a signed-out account when a view opens is STARTSPROBE1's to check.
