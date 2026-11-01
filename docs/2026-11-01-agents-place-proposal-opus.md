# AGENTSPLACE1: the Agents place made usable — sealed proposal (Opus)

Read on main at `cdd46a949`. Web paths are under `src/Daoris.Web/src/`; others name their root.

## 1. Diagnosis

**What I saw.** One screenshot of the installed window (one capture, kept outside the repository): the Work place mid-session in Chinese; the status bar shows a workspace of 19 repositories and *配置: 4/5*, Get started unfinished on the owner's own machine. The install has no debug port, so `click` could not move it; I restarted nothing. Every other screen is read from components and stories.

### (a) Roles

**A person comes to** see who does each work in a repository, reorder it, give one repository its own staffing, appoint a supervisor, and learn why a start is held.

**Where it breaks:**
- **The list pane holds five fixed section names, not things** (`agents/roles/RoleSections.tsx:18-47`, `agents/AgentsView.tsx:137-156`). Work, Accounts and By repository are read-only cuts of one read, each with its own filter bar (`StructureSections.tsx:91`, `:194-208`). "Who builds in this repository?" means crossing three lists.
- **The one edit asks for ids.** Place id, model and effort are typed text (`Roles.tsx:313-323`, `:343-361`) under five lines of prose (`:391-395`, `:410`). The account editor already offers the tool's own models and efforts as choices (`settings/accounts/AccountSettings.tsx:120`, `agents/tools.ts:203`).
- ***Add a role here* leaves the repository** for Working roles' list (`RolesView.tsx:209-214`). With no workspace filter, *Open its setting* opens the whole Repositories place (`:215-219`).
- ***Remove* has no confirmation** (`Roles.tsx:446-455`), though a work no role takes holds every start (`settings.ai.roles.body`).
- ***Use these choices* is not one change:** it applies role by role and stops at the first refusal, earlier roles written (`SuggestedChoicesCard.tsx:59-73`).
- **Appointed roles has nothing to press:** both roles are dark (`src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/StandingConfirmation.cs:330`, `:336`).
- **By repository lists every registered repository, driven or not,** nine works each (`agents/roles/roleStructure.ts:356-384`): on the owner's window, 19 rows of *takes this machine's roles*.

The mode follows its data's shape, not the person's question.

### (b) Set-up

**A person comes to** take an imported repository to "agents work here and land my way" in one sitting, then change one thing later.

**Today a first set-up touches:** Setup → Driving, *Drive* and *A tree per session*, each written on press (`projects/setup/RepositorySetup.tsx:343-377`); Details → *Set up for agents* (`projects/ProjectPage.tsx:245-260`), which refuses until both are on (workspace-setup design §2.1), so the person goes back and forth; Setup → Line and landing (`RepositorySetup.tsx:388-496`); the Workflow tab's *Accept the gates*; the Integration tab; the workspace's Setup (`projects/setup/WorkspaceSetup.tsx:180-374`); Agents → Roles.

**Where it breaks:**
- Seven tabs on one repository page (`projects/tabs.ts:8`).
- Two steps have no control: a landing rule's `integrate` policy (`src/Daoris.Desktop/Daoris.Desktop.Driver/Settings/LandingKey.cs:63-76`; `settings/repository/Landings.tsx` offers none) and choosing a workflow.
- About fifteen presses, each written at once, never reviewed together; nothing says what the repository will now do.
- Every press repeats per repository; the workspace set-up plan (WSSETUP6) covers adoption quests only.

### (c) Workflow

**A person comes to** read how work moves (who presses, which gates, where it lands) and change it. D193 point 7: "changing how a department works is an edit to its workflow".

**Where it breaks:**
- **Read-only:** the Workflow tab draws Current, and its editor (WORKFLOW1g) is unbuilt (`workflow/WorkflowTab.tsx:33-46`).
- **Presets are prose plus a terminal command** (`:112-136`): "No route writes a named workflow or chooses one" (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/DriverWorkflowsCommand.cs:16-17`, kept by D194's PUSH1f2b note).
- **Bad combinations surface only as stalled work:** of the integration design's ten combinations of workspace choice, repository choice and landing rule (§17.7), four *cannot start*.
- **One declaration, two places to accept it:** Setup (`RepositorySetup.tsx:491-496`) and the Workflow tab, by two routes (`src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/WorkflowRoutes.cs:196`, `:262`).

## 2. The design

**The spine: one list of places, one page per place; any change of more than one row goes through one plan, one review, one Apply.**

### 2.1 Structure

The Agents place keeps two modes: **Agents** (unchanged: agents and accounts) and **Set-up / 配置**, replacing Roles. Set-up's list is a tree of places: *This machine*, then each workspace with its repositories, driven first and the rest folded into one row (*17 imported, not driven*).

A place's page heads with its driving (driven, held, trees) and holds three sections, each folded to a line naming its values (D150 point 1):
- **Roles:** who takes each work here.
- **Workflow:** line, landing, integration, gates, opinion, look, kinds.
- **Sessions:** access, heavy gates and checks, language, helpers, hand-over, standing answer, reach, rules.

A repository's and a workspace's Setup and Workflow tabs move here. Repositories keeps Details, Branches, History, Changes and Integration (git and the line's runs); Details says the set-up in one line, with a door. Each setting keeps one home: the home moves, nothing is mirrored.

### 2.2 Screens

The list and a repository's page, 80rem:

```
[Agents | Set-up]            engine                         driven · its own trees  [Set up…]
Find…   [Needs a look]       Integrated into the line · 1 change of your own
This machine     2 roles     ⚠ The gates changed since you accepted them        [Review]
▾ family   Pull request      ▸ Roles     Build: Build (this machine's) · Claude Code · sonnet · high +1
   engine  ⚠ Integrated      ▸ Workflow  branch rule, integrated · gates accepted at 1a2b3c
   game    takes family's    ▸ Sessions  full access · helpers ask · reads across
   17 imported, not driven ▸
▸ work     Pull request · 4
```

**Roles, opened:**
- One row per work from `ROLE_STRUCTURE` (`RoleFor` alone): the role taking it, where it is placed, its first choice, that choice's state from the last readings.
- Inherited, the row offers *Give engine its own…*; placed here, *Edit* and *Back to family's*, an `InlineConfirm` naming the role it falls back to.
- The editor asks no ids: place filled and fixed, id generated (`build-engine`), agent, model and effort chosen from the agent's own offers (`AgentOffers`), *other…* for a typed name. One ⓘ replaces the prose.
- Supervisor (workspace, repository) and steward (This machine) are rows here; dark, each is one quiet line.
- *This machine*'s Roles holds the catalogue, every role once (COMPANYVIEW1's one row, one editor), and one *Suggested* line merging the Designer and MODELROLE1g's card, applied as one plan.
- *Show as diagram* stays on This machine's page and a workspace's, for that scope.

**Workflow, opened:**

```
Integrated into the line · v1 · chosen for engine                 [Change…] [Edit steps]
Start → work → its requests answered → landing (you accept) → integrate (you ask) → on the line
Gates     daoris.gates.json at main 1a2b3c: accepted · integration: accepted      [Review]
Landing   branch work/{quest} · integrate: batch 4, oldest first, gates declared   [Edit]
Line      main, by its checkout · Commits  Daoris's default
Second opinion off · Your look off · Heavy gates its own file's
Kinds     Documentation → A pull request (family's)
```

- Chart and start node are today's (`WorkflowChart`, `WorkflowStartNode`).
- The landed route and the integration declaration become one *Gates* row, accepted together.
- The landing editor gains the `integrate` form, judged by `LandingKey`.
- *Change…* lists the presets by their composed lines (`WorkflowPresets.GraphLine`), the saved workflows and *Current*.
- *Edit steps* is the workflow design's §6.3 editor, built here.

**Sessions** holds today's rows, moved whole (`Inheritable`, *Set for this repository*, *Clear*).

### 2.3 The quick set-up

*Set up…* opens a page in the main area (R1: a form takes the pane):

```
Set up engine                                         suggested from what it declares
( ) Not driven         ( ) Merge after you accept      (•) Integrated into the line
( ) A branch to push   ( ) A pull request — needs a landing plugin for its remote
[x] Second opinion  [ ] Your look  [ ] Its own roles  [x] Ask its session to set up for agents
Plan: 9 changes · 1 lowers your part · 1 declaration to read
 Driving   drive: no → yes · trees: shared → its own
 Landing   family's merge → its own branch rule, integrated (replaces family's whole)
 Workflow  Current → Integrated into the line, saved as `integrated` v1
 ⚠ Lowers  its work reaches the line only through integration
 Gates     [ ] I have read daoris.gates.json at main 1a2b3c          (the file, folded)
 Set-up    a quest to engine's own session                           (its text, folded)
 Terminal  daoris-driver place engine --preset integrated --with opinion,adoption --plan
                                                  [Apply 9 changes]  [Never mind]
```

**Presets by repository kind,** a driver table (`SetupPresets.cs`, beside `WorkflowPresets`): *Not driven*, *Merge after you accept*, *A branch to push*, *A pull request*, *Integrated into the line* (its *with no press* variant only where the same plan accepts the declaration). One is preselected from facts the driver already reads (a declared `integration`, a landing plugin for the remote's platform, adoption); nothing applies until Apply.

**How parts compose.** A preset is a list of parts: driving, line, landing, workflow choice, gates, opinion and look, roles, adoption. Each is a change an existing door already makes (`SET_DRIVABLE`, `SET_TREES`, `SET_LINE`, `SET_LANDING`, the workflow choice, `INTEGRATION_GATES`/`WORKFLOW_LANDED`, `SET_ROLE`, `SETUP_PRESS`).
- On a workspace, it writes the workspace's keys.
- On a repository, it writes only parts whose effective value differs: under a workspace already set that way, *drive* and *trees* alone.
- A preset is never stored or followed. The page derives which preset a place matches, or *n changes of your own*; no precedence level is added.

**One plan.** `PLACE_PLAN` (Open) applies the parts to a copy of `driver.json` and reads before and after through today's resolvers (lines, `LandingKey`, `WorkflowSelection`, `WorkPolicies.Of`, `RoleFor`, access): one reader per precedence, none on the page. It says every refusal at once (as `SETUP_PLAN` does), what lowers the person's part (`InvolvementRoutes.LowersRoute`), §17.7's reach per repository, and each declaration's blob, commit and text. Several repositories make one plan, identical changes said once.

**One review, one Apply.** `PLACE_APPLY` (Person) takes the plan's digest and the ticked blobs, then:
1. plans again, refusing `moved` if anything changed;
2. reads the tips, refusing `moved` if a ticked blob is no longer the tip's;
3. saves any named workflow (saved but unused is harmless);
4. writes every `driver.json` change, acceptances included, in one `DriverContext.Change`: all judged, one write;
5. publishes the set-up quest last.

**The person's acts stay theirs.** From a terminal the plan is one Notifications card, one Confirm (CONFIRMWAIT1). It carries an acceptance, so no standing confirmation covers it and no role's act reaches it (`WorkflowRoutes.cs:257-259`). A sign-in is never in a plan; a plan blocked by one says so, with a door to the agent.

**After.** A single row's *Save* stays one press; one that lowers the person's part or accepts a declaration shows its plan inline first.

### 2.4 The flows

- **First set-up:** Set-up → *family* → *17 imported, not driven* → engine → *Set up…* → keep the suggestion → read the plan, tick the declaration → *Apply 9 changes*. The page heads *Integrated into the line*; the set-up quest shows in Sessions.
- **A change:** engine → Workflow → Landing *Edit* → batch 4 → *Save*. Or *Change…* → *A pull request* → plan says *lowers: this line has no gates* → *Apply*.
- **Appointing:** engine → Roles → Build → *Give engine its own…* → Codex, then Claude Code → *Save* (`SET_ROLE`). family → Roles → Supervisor → *Appoint…* (today's `SupervisorRow` ask, the owner's press).
- **The workflow:** engine → Workflow shows the chart, why (*chosen for engine*), the gates' standing. *Edit steps* → *Add after… Second opinion* → *Save* makes v2, running work keeps v1; choosing v2 is a plan of one.

### 2.5 What moves, merges, goes

- **Moves:** the Setup and Workflow tabs (§2.1); *Set up for agents* into the plan's adoption part.
- **Merges:** Working roles, Work and By repository into place pages; Accounts into the Agents mode (*Used by*, `agents/AgentsView.tsx:241-244`, plus a list mark); Appointed roles into rows; the two acceptances into one Gates row; two suggestions into one.
- **Goes:** the section list and strip, per-section filter bars, the typed place id, presets-as-commands, the editor's prose.

### 2.6 Data and routes

- **New:** `SetupPresets`; `PLACE_PLAN` (Open); `PLACE_APPLY` (Person, drain *Waits* like `WORKFLOW_LANDED`), twin `daoris-driver place`; `WORKFLOW_SAVE` and `WORKFLOW_USE` (Person), the verb's writes as typed operations (D194 point 4), amending D194's PUSH1f2b note; `PLACES` (Open, by event): each place's matched preset, driving and *Needs a look* reasons (COMPANYVIEW1d's one owner kept).
- **Changed:** `SET_LANDING` carries `integrate`; `modes.ts` becomes `['agents','setup']`, a kept `roles` read as `setup`. `ROLE_STRUCTURE` unchanged.
- **Records:** glossary rows *preset / 预设* and the mode; notes amending D150 points 3-4, D157 §6.1, COMPANYVIEW1 §1 and §3.
- **Nothing new stored:** no preset record, no plan file; machine-local state stays in `driver.json` and `workflows/` (D172).

## 3. What I reject

- **COMPANYVIEW1's sections.** I keep its model: `RoleFor` the only resolution, the read, inheritance lit not drawn, no account assigned to a role, the call table read-only, a new place taking roles with no press. I reverse the five sections: three lists for one question.
- **A tenth place or a Settings domain** (D150 point 2, R5): set-up is a place's, not the machine's.
- **Set-up mirrored in two places:** the home moves (D150 point 1).
- **A wizard:** one page, presets and a plan; COMPANYVIEW1 §5's rejection stands.
- **A stored "profile" a repository follows:** a new precedence level needs a second reader, and *profile* is the account's code word, kept off the window by the glossary.
- **An atomic apply across git and the service:** the quest goes last; `driver.json` is one write.
- **The steward applying plans:** acceptances, landings and roles stay the owner's (D209); it keeps its driving acts.
- **A free canvas for workflows:** the design's list-shaped graph stays.
- **One Save across the page's sections:** it leaves half-edited pages; several edits go through a plan.

## 4. Rows

**Every row's proof:** page tests; stories at 80, 39, 25rem, en and zh, both themes; the installed window where it changes.

1. **AGENTSPLACE1a, presets and the plan** (driver, modules): `SetupPresets`, `PLACE_PLAN`, `place --plan`. Proof: a test per preset over fixtures (solo; declares integration; remote with plugin; without, refused); before and after equal to each resolver; writes and probes nothing; the verb's text fixture.
2. **1b, the apply** (modules): `PLACE_APPLY` in every place `bridge-presses.md` names. Proof: census Person; no standing cover; a role's act refused; one write; `moved` for digest and blob; step order; a terminal card confirmed once. After 1a.
3. **1c, workflow writes as routes** (driver, modules): `WORKFLOW_SAVE`, `WORKFLOW_USE`. Proof: the verb's text cases pass; §17.7's reach sentences said.
4. **1d, `integrate` in the landing rule** (modules, web): `SET_LANDING`'s key and form. Proof: `LandingKey`'s refusals under *Save*.
5. **1e, the Set-up mode and list** (web, modules): modes, place tree, `PLACES`, the not-driven fold, marks.
6. **1f, the page head and Sessions** (web): rows moved from `RepositorySetup` and `WorkspaceSetup`; Repositories loses two tabs and gains the line. Proof: every door re-pointed (What needs you, Get started, help rooms), held by `HelpCoverageTests`, `PLACE_PARTS`, D150's counts. After 1e.
7. **1g, Roles on a place** (web): per-work rows; *Give … its own* with generated id and offers; *Back to …* confirmed; This machine's catalogue and the suggestion as one plan; appointed rows; the scoped diagram. After 1a, 1b, 1e.
8. **1h, Workflow on a place** (web): chart, start node, one Gates row, `integrate`, *Change…*. After 1c, 1d, 1e.
9. **1i, the step editor** (web): the workflow design's §6.3 here, versions said. After 1h.
10. **1j, *Set up…*** (web): presets, parts, plan, ticks, Apply; one plan over several repositories; refusals in place (R3). After 1a, 1b, 1e.
11. **1k, Ask Daoris and records:** the `setup` kind's `place` door proposing a plan card; goldens; glossary; decision notes; the guide's verb. After 1j.

## 5. Risks and open questions

- **Moved doors.** Every door into Setup (What needs you, Get started, the help room, the chart's step doors) is re-pointed in 1f; a missed one opens nothing. `HelpCoverageTests` and a door census hold them.
- **The name.** *配置* also names Get started's status-bar count, which may need its own word. Open.
- **A wrong suggestion** is only preselected; the plan shows everything first.
- **A stale plan:** a landing during review makes Apply refuse `moved`, naming it; planning again is one press.
- **The Integration tab** stays on Repositories as the line's operations. Should its queue sit beside the Gates row? Open.
- **A renamed repository.** A generated role id names it; after a rename or `wire` the read draws the scope as written (COMPANYVIEW1). Carrying it across is open.
- **A workspace-wide choice** reaches every repository (§17.7, F42); the plan names each and what it does there.
- **Undo.** No inverse is stored: the way back is *Not driven* or each row's *Clear*. Keep the last plan's before values for one *Undo* plan? Open.
- **Older shells** with no `PLACE_PLAN` fall back to row-by-row presses; the old tabs stay until 1f.
- **Narrow widths.** At 25rem each change's before and after stack under its words; 1j's stories hold it.
