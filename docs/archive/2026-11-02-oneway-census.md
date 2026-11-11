# ONEWAY1 — the jobs that still have two ways

Status: census, 2026-11-02, read-only on main at `e04e16dc9` under D228. The workflow logic is left out (WORKFLOWCLEAN1's
inventory, `docs/archive/2026-11-02-workflow-cleanup-inventory.md`, and its design); COMMS1's census is cited, not repeated. Paths: `Drv/`
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Host/` `…Driver.Host/`, `Mod/` `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Svc/`
`src/Daoris.Service/Daoris.Service.Core/`, `Http/` `…Service.Http/`, `Mcp/` `…Service.Mcp/`, `Web/` `src/Daoris.Web/src/`, `Cli/`
`src/Daoris.Cli/src/`.

## In short

37 jobs still have two or more ways: 22 true second ways and 15 transitional reads, of which 4 have a named end that is a live row
today (FOUNDATION1 row v, INTEGRATE1 row k, SETUPUX1y, CANVAS1e/f). Three places rewrite the person's records without their press,
which D228 rejects: `harnesses.json`'s old names at the next write, the account-pool fold, and the git move's first-start mark.
COMMS1 (D225) and STAFFPOLICY1 (D226), accepted before D228, add transitional writes with no end.

## 1. Staffing and roles

- **S1 — setting a role's agents.** `SET_ROLE`/`SET_ROLES` (`Mod/Bridge/Routes/DriverRoutes.cs:559-583`) beside `SET_ACTOR`
  (`:374-386`, the page's *Clear order* and its only editor on a home whose file is its own, `Web/agents/roles/Roles.tsx:173-183`);
  five terminal verbs (`driver adapter|intake|helper|actor|role set`, `Drv/Settings/DriverChoicesCommand.cs:105-131`, `:166-216`);
  Ask Daoris's `setting_propose` `adapter`/`intake`/`helper` (`Drv/Help/Proposals/HelpSettingProposals.cs:34-38`, `:225-308`) beside
  `role_propose`. The room still teaches `driver actor` and lists an owed actor door "MODELROLE1h builds", a row that exists nowhere
  (`Drv/Knowledge/Room/HelpRoomOwed.cs:93-109`). True second way.
- **S2 — where staffing is kept.** The store's `role_staffing` (`Drv/Roles/RoleStaffing.cs:92-108`) beside `driver.json`'s `actors` and
  adapter keys written back on every write (`:569-600`, `Drv/Roles/StaffingFile.cs`), the *found* changes and their door
  (`STAFFING_FOUND_ANSWER`, `driver actor found|export|import`), and a `DAORIS_DRIVER_CONFIG` home with no store. Transitional to
  FOUNDATION1 row v, whose condition is shown nowhere; the named-file path has no end.
- **S3 — *as before*.** A role with no stored order reads `adapter`, `intakeAdapter`, `helperAdapter` (`Drv/Settings/ActorKey.cs:28-50`),
  and readers skip the role for the legacy key (`Drv/Roles/StartPlan.cs:463`, `:467`; `Mod/Bridge/DriverContext.cs:184-194`;
  `HelpSettingProposals.cs:775`; `Drv/Loop/Driver.cs:755`). True second way, no end.
- **S4 — the words.** *actor* (key, verb, route, catalogue keys, the room) beside *role* (FOUNDATION1 row u). True second way in words.
- **S5 — the old Roles mode**, dark by default (WORKFLOWCLEAN1); the stored `roles` mode still read as `setup` with no end.

## 2. Setting up and adopting

- **A1 — adopting a repository.** `SETUP_PLAN`/`SETUP_PRESS`, the repository page's *Set up for agents* (shown lit or dark,
  `Web/projects/ProjectPage.tsx:262`), `daoris-driver setup`, Ask Daoris's `setup` door, beside `PLACE_APPLY`'s adoption. In
  WORKFLOWCLEAN1 row 1f; *Set up for agents* is missing from SETUPUX1y's list.
- **A2 — a whole workspace.** The workspace plan file worked on every look (`Drv/Setup/WorkspaceSetupFile.cs:9`,
  `Drv/Loop/Driver.cs:1125-1127`) beside *Set up…*; an open plan's conversion is not named.
- **A3 — Get Started** points at the old ways (WORKFLOWCLEAN1).

## 3. Sessions and conversations

- **C1 — one agent, two doors acting differently.** `claude-code` (native, the default) beside `claude-code-acp` (protocol): a limit
  cools no account on native (TOOL4n); the in-run transient retry only on protocol (`Drv/Loop/Driver.cs:4530-4537`); *Try again*
  briefed on native (D210); a word during a turn taken between turns on protocol, held to the turn's end on native; skills and settings
  handed only on native. True second way with behaviour differing by path; the choice of doors is the owner's.
- **C2 — adapter classes beside providers** through `LegacyDoor` (`Drv/Agents/Registry/LegacyDoor.cs:5-19`); its end, AGENTPROVIDER1t,
  parked.
- **C3 — seven executors** each spawning an agent with its own native-vs-protocol branch (driven, conversation, helper, intake,
  opinion, partner, brainstorm). One `AgentRun` (AGENTPROVIDER1m-i) parked.
- **C4 — a conversation's course beside a driven line's** under a staff policy; STAFFPOLICY1e3 open.
- **C5 — four presses to go on with words a session could not take** (`SESSION_START_FROM`, `SESSION_GO_ON_BRIEF`,
  `SESSION_GO_ON_NEW`, `SESSION_HAND_OVER`; `Web/work/WorkFrame.tsx:900-954`): go-on-new and go-on-brief likely one job.
- **C6 — the course's paths for an older build** (the unkeyed open, "an older host moved nothing", `held-words.json`, the import
  backups): transitional to row v, condition shown nowhere.

## 4. Messages and asks

- **M1 — answering a parked session.** `POST /api/sessions/{id}/answer` (`Http/Program.cs:2207-2213`), `daoris-driver answer`, the
  page's park card through it, beside the say door (`SESSION_INPUT`, `sessions say`). True second way; COMMS1 files no row for it.
- **M2 — `sessions say`** by the pipe, a request file for an older build, and the service's say door; `now` written beside `level` and
  `now: true` still read. Transitional, no end (the headless loop's request file has a reason; `now` has none).
- **M3 — `Session.Answer`** (the words joined into one text) beside the keyed `Said`, read by the course, the planner, attention and
  Ask Daoris. Transitional, no end.
- **M4 — two confirmation queues.** The pipe's wait however long; the service's person-door confirmations expire after two minutes
  (`Http/PersonConfirmations.cs:16-23`, `:127`). True second way with different behaviour — the owner's CONFIRMWAIT1 report.
- **M5 — COMMS1's kept guards and readers** (the helper's and partner's `Attempted`/`Handed`, the legacy answers-alone reader, the
  driver's own hand against an older host, old `answered` receipts) have no end.

## 5. Git

- **G1 — credentials.** Ambient on a home not marked moved (`Drv/Workplace/GitDoors.cs:93-100`) beside identities; the move marked
  silently at the first start (`Drv/Workplace/GitMove.cs:15-31`, `Drv/Loop/DriverWatch.cs:404-405`), where D228 wants the person's press.
- **G2 — may a session push.** `permissions.json`'s `no-push` and its off switch, read live as an allow (`Drv/Workplace/GitAccessRules.cs:22-87`),
  beside git access (`machineGitAccess.write`, `SET_GIT_ACCESS`) and the harness rule composed from it. Two settings for one permission.
- **G3 — deleting a branch.** The journaled ref act (`GIT_REF_DELETE`) beside unjournaled judged deletes (`DISCARD_SESSION_BRANCH`,
  `WITHDRAW_LANDING_BRANCH`, `SWEEP`, the look's tidy, `housekeeping_discard_branch`), each with its own undo and records.

## 6. The service and the CLI

- **X1 — the retired timeout**: `driver timeout`, Ask Daoris's `timeout` door in both twins, `settings_timeout`, the `timeoutMinutes`
  key read only to be said (D200 point 9 retired it). Retired doors kept with no end.
- **X2 — a brainstorm from a terminal**: `daoris brainstorm` through the service beside `daoris-driver brainstorm` writing files
  itself; they have diverged.
- **X3 — remotes and browser favorites**: the CLI's own TypeScript editors (`Cli/service/remotes.ts`, `remotemap.ts`,
  `Cli/browser/browser.ts`) beside the desktop's modules over the same files.
- **X4 — two names for one thing at the terminal**: `daoris driver …` and `daoris-driver driver …`; `--profile` beside `--account`;
  `--early`/`--near` beside `--switch`/`--curve`.
- **X5 — where a home's file lives**: every entry read at its new place, else its old (`Drv/Platform/DaorisHome.cs:105-180`) and
  the twins; trees under two roots; the old `driver.lock`; D63's move-in; the flat service host (LEGACYHOST1). No end.
- **X6 — six-character quest ids** still resolved beside twelve: history or converted, the owner's.

## 7. This repository's integration

- **I1** — `tools/merge-branch.mjs` and `merge-queue.mjs` beside the fabric's `integrate`; transitional to INTEGRATE1 row k.
- **I2** — twins inside it: `Drv/Workplace/BacklogMerge.cs` (a twin of merge-branch's backlog merge), `tools/merge-rows.mjs`'s git
  driver beside the declared rows merge; not named by row k.
- **I3** — the ungated merge rule's press riding the integration transport as "the named legacy exception"
  (`Drv/Workplace/IntegrationRequests.cs:53-56`): WORKFLOWCLEAN1's.

## 8. The page

- **P1** — the flag-dark Setup surfaces (WORKFLOWCLEAN1, SETUPUX1y); *Set up for agents* missing from 1y.
- **P2** — the canvas beside the older `Web/design/Flow.tsx`, still drawing the landing, quest, parts and session flows and
  `WorkflowStartNode`; the maps' own drawing ends with CANVAS1e/f, the life-flows with no row.
- **P3** — the page's branches for an older shell and pull routes kept for an older page, though page and shell ship as one build (D139).
- **P4** — readers of note shapes from before codes and parts: history or converted, the owner's.

## 9. Settings

K1 staffing keys (S2, S3); K2 `harnesses.json`'s old names, retired fields and folded workspace lists rewritten at the next write and
the account-pool fold (`Drv/Agents/HarnessSettings.cs:753`, `:1146-1156`; `Drv/Agents/AccountSet.cs:16-39`) — silent; K3 push
permission (G2); K4 `timeoutMinutes` (X1); K5 a home named by `DAORIS_DRIVER_CONFIG` with no store and no git-move mark.

## Looked at and not counted

The cross-language twins held by tests; pushed slices over one builder; the theme; the workspace scope in storage; the steward's verbs
(another authority, D209); propose beside act (D183); plan and press pairs on one handler; `CHECKOUT_BRANCH`; the headless `drive` loop.
