# COMPANYVIEW1: the Opus maker's sealed proposal (2026-10-31)

**Standing:** a sealed proposal under D191, written from `2026-10-31-company-view-brief.md` alone.

**Read** on `parent/batch-1031d`: the brief; D150, D168 point 3, D193, D205, D208, D209, D212 points 5, 14, 16, 17 and its q, w, x, y-i and r notes; the map design §1, §4; the flowchart design §1, §3, §5.5, §6; the UI/UX design R1 to R7 and §3.10; `docs/model-roles.md`; the glossary. Code (web under `src/Daoris.Web/src/`, driver under `src/Daoris.Desktop/`): `settings/AiDomain.tsx:155-202`, `settings/ai/Roles.tsx`, `settings/ai/Actors.tsx`, `settings/domains.ts`, `settings/wiring/WiringSteward.tsx`, `projects/setup/SupervisorRow.tsx`, `projects/tabs.ts`, `agents/AgentPage.tsx`, `settings/accounts/AccountUse.tsx`, `map/MapView.tsx`, `map/StartWiring.tsx`, `design/Flow.tsx`, `design/flowLayout.ts`, `help/places.ts:37`, `Driver/Roles/RoleCatalog.cs:146-183`, `Driver/Roles/StartPlan.cs:35`, `Driver/Roles/RoleSuggestion.cs`, `Driver/Participants/Domain/WorkPolicy.cs`, `Modules/Bridge/Routes/DriverRoutes.cs:323-397`, `src/Daoris.Service/Daoris.Service.Mcp/CallTable.cs`.

**Three facts shape it.**
- The structure is already one model in the driver. `RoleCatalog.RoleFor(work, place)` answers which role takes a work at a place: the most specific scope, design falling back to build, held where two tie or one does not read. `WorkPolicies.Of` answers each work's consent, fence and source. No route hands either to the page; `ROLES` hands the catalogue alone.
- The page shows one role twice. AI features draws *Actors* (`SET_ACTOR`) and *Role catalogue* (`SET_ROLE`), both editing a seeded role's choices (`AiDomain.tsx:194-201`), and the English card says *Actors* (`locales/en/settings.ai.json:28`), a word the glossary keeps off the window (`glossary.json:477`).
- *Graph* is the commit graph (`glossary.json:575`), *map* the requests between repositories, *flowchart* a process; *company* is D193's narrative, never a window word (D193's rejections, D212 point 14). The brief's graph needs a name of its own.

The brief cites D137 for *never an edge it cannot prove*; D137 is MSG1. The rule applies as the map design §1 states it (edges are what the records say) and as MAP1b held its wiring to `SelectAsync`.

## 1. The model

**Choice: positions are configured here; what they stand on is read here, with a door to its home.**

| Thing | Record and door | Here |
|---|---|---|
| Working role: names, work, place, one to four choices | `roles`, `role_revisions`, `role_staffing` in `data/desktop.db`; `ROLES`, `SET_ROLE`, `daoris driver role`, `role_propose` | configured; home moves here |
| Appointed role: supervisor (repository or workspace, window), steward (machine, acts, set-up tick), its holder | `appointments` in `data/standing-confirmation.json`; `SET_APPOINTMENT` (`Window`) and its ask; `SupervisorOffered`, `StewardOffered` | configured; home moves here |
| Work | `RoleWork.Codes`, code | read |
| Work policy: consent, fence, bound, source | `WorkPolicies.Of`; Setup's Opinions, Helpers, Hand-over | read, door |
| Agent, accounts, last known state and when | the roster; the Agents page | read, door |
| Place: This Daoris, workspace, repository, line, lanes, landing | registry; Setup | read, door |
| Workflow, integration | Current or the bound version; the Workflow and Integration tabs | read, door |
| Plugin | Plugins | read, as the agents it declares and a landing it names; door |

**Relations, each a read:** a role *takes* a work and *is placed* in a place (fields); it *is staffed by* an agent (choice n, model and effort as written); an agent *has* accounts; a place's work *goes to* a role (`RoleFor`, the driver's answer only); a policy *fences* agents (`WorkPolicy.Agents`); an appointment *covers* a place (`StandingConfirmation.Covers`), held by a holder. The appointed half of the catalogue stays D212's later design (rows w-0, t, y-ii, z-iv); this reads appointments where they are.

**Why:** D150 point 1 puts a setting on its thing. A role and an appointment have no other thing, and are spread over four screens today (AI features; This Daoris → *Roles*; repository Setup → Driving; workspace Setup → *Supervisor*). An account, a rule, a line, a workflow and a plugin each have one. **Rejects:** account, rule or landing editors here (a second home); a new store or a page-side resolution (a second truth, D212 point 1).

## 2. The list

**Choice: one tree by place, This Daoris › workspace › repository, as Repositories groups them (D150 point 3), each role once, under its place.**
- **A role row**, folded (R1): its name as authored, its work, its first choice and *n more*; for an appointment its holder, scope, since when and *answers held n min*. *Edit*, *Remove* and *Back to the default* open its editor in place (`Roles.tsx`'s, and the appointment asks as built). One editor open in the place (D168 point 3); a refusal under its press (R3).
- **A place row**: for a repository, each work's role as a chip naming where it is placed (*Building → Builder, this machine*), from `RoleFor`; its line, landing and workflow a line each, with doors to Setup, Integration and Workflow; *Add a role here*, its place filled.
- **Filters**: work, agent, workspace, a name search, and *Needs a look*.
- **Unset or invalid, in the driver's sentence**: a work no role takes (`RoleCatalog.NoRole`), two roles tied, a record that does not read; a choice that cannot run by its pass code from the last reading (`StartPasses.Codes`: unavailable, no posture, no connector, not offered, cooling until …, signed out), never probed (D150 point 5); a fenced agent with no choice, *account default*; an appointment *dormant until Appoint again*; *not offered in this build*; a home whose `driver.json` is its own, every editor held (`settings.ai.roles.unkept`).
- **No number on the bar**: a held start is no new kind of wait (D205 point 4, §3.10).

**Rejects:** a list by role alone (a repository's answer assembled by eye); a work × place matrix (it fails at 25rem).

## 3. The diagram

**Choice: a *diagram* (结构图, a new glossary row), three ranks: places, roles, agents.**
- **Nodes**: a place that sets something of its own (a role, an appointment, a fence); roles inside their place's group; agents with their accounts' count and last known state.
- **Edges**: *staffed*, role to agent, numbered, dashed where its last reading cannot run; *covers*, appointed role to place.
- **Inheritance is lit, not drawn**: selecting a repository lights, per work, the path place → role → agent that `RoleFor` answered.
- **Never drawn**: quests, asks or chains (the Map's); a role over a role or a session (D193 rejects a hierarchy); a guessed resolution; a workflow's nodes, which take the roles of what they start (D205 point 1).
- **Legible at size**: a repository setting nothing of its own is counted in its workspace's node (*17 more take its roles*), as the Map counts what it does not draw; past 24 nodes each workspace folds to one node with its press; a work filter. Laid out by `design/flowLayout.ts`, deterministic, with no pan, zoom or drag and nothing drawn smaller; placed by the pane's measured width, the rail at narrow being the list; the flowchart's reader and keys (§3.7).
- **Editing**: none on a node. A press selects, and the same row molecule opens in the detail beside it, below it at narrow.
- **With the Map and the Workflow tab**: a repository's detail in each gains *Its roles* or *On the map*; neither draws the other's.

**Rejects:** the Map as host (a browser's too, and it draws what happened: map design §4, flowchart §5.5); `Flow.tsx` itself (its words are a process's); drag to staff (no keyboard path, no place to say a refusal).

## 4. One model, two views

**Choice: one read, one pure function, one owner.**
- **The read**: `ROLE_STRUCTURE`, an `Open` route told by event, composed from `RoleCatalog`, `RoleFor` per place and work, `WorkPolicies.Of`, the appointments and flags, `STEWARD_ACTS` and the roster's last readings. A test holds it to `StartPlan`'s answer over `Driver.Tests/fixtures/structure.json`, as MAP1b held `WiringAsync` to `SelectAsync`. Names only, no path.
- **The function**: `roles/structure.ts`'s `buildStructure`, `map/topology.ts`'s shape: rows, nodes and edges sharing one id each.
- **The owner**: the place holds the selection, the open editor and every press's pending, handed to both views (D168 point 3); the molecules import nothing of the bridge.
- **Where each is better**: the list to set, scan, filter and hear; the diagram to see which roles lean on one agent when its accounts cool, and a repository's lit path. *Show as list* or *Show as diagram* in the head, kept per view; the selection crosses.

**Rejects:** the page computing the most specific place; two editors kept in sync.

## 5. Setup

**Choice: a new place takes the roles above it with no press.**
- **The seed** places D205's eight roles on This Daoris, *as before*; a workspace or repository the registry adds arrives answered, *takes this machine's roles*.
- **First run**: the head offers `RoleSuggestion`'s Designer and, once MODELROLE1g lands, D205 point 7's card (Build on `sonnet` · high, then `opus` · high, `docs/model-roles.md`'s build-settled), a press each, its words saying it can spend more.
- **Per place**: *Add a role here*, then *Save*. *Appoint…* on a workspace's row covers its later repositories (SUPERVISOR1e); the steward's, on This Daoris, every registered repository (D209 point 1).
- **The steward**, appointed and offered, makes only D209 point 3's acts from a repository's row: drive, trees on, the set-up the owner ticked. Each says *Set by the steward at …*; the owner's press there reverses it and marks the target `owner-set`.
- **The person's alone**: roles, staffing, appointments, accounts, landings and rules (D209 §3.2: spending, every widening). Ask Daoris proposes roles by `role_propose`, applied by the person's press.

**Rejects:** a set-up wizard (each step has its door); a steward's act on roles.

## 6. Where it lives

**Choice: a tenth place, *Roles* (角色), the desktop's alone (D47 §4)**, which D150 point 2 and R5 allow with counts: the owner names it a main function, and it spans the machine, every workspace and the agents, which no place holds. AI features, This Daoris → *Roles* and the two Setup rows become read-only lines with a door; `RoleCatalog.NoRole` names the place. **Rejects:** a Settings domain (a main function as a machine setting); a workspace's tab (the steward and machine roles have no workspace).

## 7. The rows, smallest first

1. **COMPANYVIEW1a, one role one editor** (web, over `SET_ROLE`): *Actors* folded into the catalogue's rows, its ⓘ, *Off* and found card kept, the word off the window. Proof: page tests; stories at 80, 39 and 25rem, en and zh, both themes; `names:check --strict`. Waits on nothing.
2. **1b, the read** (driver, modules): `ROLE_STRUCTURE`, its census class, the held test. Waits on nothing: FOUNDATION1q, x and y-i are built.
3. **1c, the list** (web): `buildStructure`, `RolesList`, every unset and invalid state. Proof as 1a, over the shared fixture. After 1b.
4. **1d, the place** (web, modules): `help/places.ts`, `GO_PLACES`, the palette, `HelpCoverageTests`' door, the foot naming `daoris driver role` and that appointing is the window's alone, D150 point 9's counts before and after, the doors left behind, the help room's goldens. Proof: page tests, the counts, the installed window. After 1c and the owner's yes to a tenth place.
5. **1e, appointments at home** (web, over `SET_APPOINTMENT`): `SupervisorRow` and the steward's row moved, their asks unchanged, dark. Usable as SUPERVISOR1a-iii and STEWARDROLE1d activate each. After 1d.
6. **1f, the diagram** (web): layout, folds, lit path, keys; Playwright reads every edge clear of nodes at the three widths. Proof also the installed window. After 1c and 1d.
7. **1g, setup** (web): the first-run head, the suggestions, *Add a role here*, Get started's door. After 1d.
8. **1h, the steward's part** (modules, web, over `ROLE_ACT` and `STEWARD_ACTS`): its marks and acts on repository rows. After STEWARDROLE1d and STEWARDROLE1g.
