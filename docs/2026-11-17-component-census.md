# Component census: which subsystems get the adapters' treatment, and the ports they share

Written 2026-11-17 by the steward's census agent, read-only. Code read on the integration branch `steward-roles` at `b4470b7681`
(the same code as `d909152654`; the commit between moved documents only). History read from `main`'s first parent at `d045735be1`.
Nothing was built, tested or run but measuring scripts over files, records and `git log`; the scripts and their raw output are in
`local/scratch/steward/census-work/` (`census.py`, `coupling.py`, `funnels.py`; `census.json`, `cells.json`, `coupling.json`,
`funnels.json`, `classes.json`, `instability.json`, `isolation.json`).

Read first, as briefed: D286 and `docs/2026-11-13-layers-design.md`, D300, `docs/2026-11-17-test-scope-plan.md` (whole),
`docs/areas/code-structure-and-process.md` and the driver's `fixtures/structure.json`. Ask Daoris's own plan
(`docs/2026-11-17-assistant-package-plan.md`, ASSISTPKG1a to z with the D310 draft) appeared while this was being written and
was read last; §2 and §4 are written to its shape: a package's core on ports of its own, a desktop adapter above the driver, one
lock for every fingerprinted package, and the driver offering generic seams that name no package. Where this census asks more of
that plan, it says so as a condition, not a change.

**Records and how each figure is taken.**

- *Size*: source and test files and lines under each subsystem's paths (`.cs`, `.ts`, `.tsx`, `.mjs`; the canon's `.md`), `bin`,
  `obj`, `node_modules` and generated folders left out. A test file is one in a `.Tests` or `.Rehearsals` project, a `test/` or
  `tool-test/` folder, or a `*.test.*` file.
- *Change rate*: landings are `main`'s first-parent merges since 2026-11-01: 1,014, of which **897 touched code**. A landing
  touches a subsystem when one of its changed paths is in it. *Alone* counts the landings that touched no other driver subsystem
  (the window, service, tools and documents not counted against it). Landings are wide: median 24 files and 3 driver subsystems,
  p90 71 files.
- *Test time*: case-minutes (the sum of the test cases' own durations) from named trx files, each class placed by the file that
  declares it: the driver's unit half `scope-92590ea/driver.scoped.trx` (2026-11-17 07:30, quiet-ish, 21,427 cases, 90.3 case-min),
  the modules' `scope-92590ea/modules.scoped.trx`, the service's `_fixtures/test-timing/service.trx` (11-15, 26.5 case-min), and the
  Process halves of the last full set `full-b739ed7/` (11-12: driver Process 152.9, driver git 304.2, modules 3.3, service 1.8 =
  **462.2 heavy case-min**). Classes since moved to `Driver.Rehearsals` are placed by name; 5.0 git case-min found no home. The web
  has no whole record left; its figures are the test-scope plan's.
- *Flakiness*: classes with a CI finding (`local/ci-findings.json`, findings first seen since 11-01; *open* = still red), and the
  `FLAKE:` lines of the merge logs and `local/merge-record.jsonl`'s per-gate flakes.
- *Wall-clock tests*: the layers census's `clock` allowance (`tools/layers-census.json`: 1,423 wall-clock reads in test files),
  shared out by subsystem.
- *Coupling*: the driver is one namespace (`Daoris.Driver`, 3,193 files), so `using` lines say nothing. A type-reference census
  instead: each type name declared once in non-test C#, counted where it stands in a type position (`new`, `is`, `as`, `typeof`,
  generic arguments, declarations, static access) in another subsystem's non-test file, and only where that project references the
  declaring one. Its noise, read against `StructureTests`' rule that no driver file names a later subdomain: 281 upward references
  against 10,414 downward (about 3%). Figures are distinct (file, type) references and distinct types.

## 0. In short

- **The heaviest by far is Workplace** (65% of the heavy case-minutes, 47 unstable classes), and LAYERS1 e and f are already taking
  it apart; what is left for packages, its reads and acts as a seam (P11), follows f. **Loop is the most unstable** (74 classes with findings, 66 open) and the most changed
  (44% of landings), but it names 1,129 types of everything below it, so it separates last, as COMPONENT1 b after LOOPPORTS1.
- **By benefit over cost the order is: the service's knowledge engine, Plugins, Ask Daoris, Agents, the Workflow engine (with
  Review's nodes), Workplace, the run (Loop with Conversations and Work orders).** Ask Daoris lands **third**: it is cheap (only 41
  of its types are named from outside, by Loop's start, the window's routes and two service modules; 6 Process test files) and
  the most change-dense subsystem (147 landings on 38 driver files), but its own tests save little (1.6 unit case-min; its one heavy class, 13.9
  case-min, is a git-tree scenario LAYERS1 f moves anyway). On raw benefit alone it ranks eighth.
- **Isolation is modest everywhere today.** Even counting the window, the roots and Loop's wiring as free, the best subsystems change
  alone in about 18% of their landings (Agents, the Workflow engine), Ask Daoris's gathered slice in 11%, Loop in 2%. The gains a
  separation can show soon are the heavy tier off merges, flaky real-process classes off merges, focused runs of one small project,
  and builds that skip the driver's 3,193 files; isolation grows only as the ports stand still.
- **There are two shapes of separation.** A *leaf above the driver* (Ask Daoris now, later the Workflow engine and the run) keeps
  its core on ports of its own and a desktop adapter over the driver, as ASSISTPKG1 lays out. A *provider below the application*
  (Plugins' runtime, Agents' harnesses, the service's knowledge engine) is D286's shape: a port in `Daoris.Ports` and a package that
  implements it. Both take one fingerprint row in one lock and a workspace kind with its own workflow.
- **The shared set is three layers.** D286's effect ports (clock, store, processes and memory built, git building; browser,
  network and harness with their ports in place and adapters to come), plus the plugin protocol as a new one. The driver's and modules' generic seams that every package's adapter
  builds on: conversation kinds and parts read from the root's assemblies (ASSISTPKG1 b and e), the facts hub's topics (the state
  reads), turn events, the supervisor walk, the workplace's reads, and the modules' routes, presses, places and pushes. And each
  package's own ports, which the assistant's nine start; a port shape a second package needs is promoted to the shared folder in the
  change that adds that second consumer, never copied.
- **The first three steps are ASSISTPKG1's own, taken as shared machinery:** a and c (a boundary ratchet per package, and one lock
  for every fingerprinted package); b and e (conversation kinds, and parts read from the root's assemblies); then the assistant's
  package itself. Plugins follows as the first provider package, and the first to reuse the lock, the kind and the part reader.

## 1. The census

### 1.1 Measures

Paths of each row are in §1.3. *Unit* is the driver's, modules' and service's unit halves; *heavy* the Process and git halves.
Shares: heavy of 462.2 case-min; wall-clock of 1,423 test-file reads. *Unstable*: classes with a finding since 11-01 (open).

| Subsystem | Source files / lines | Test files | Landings since 11-01 (of 897) | Alone | Unit case-min | Heavy case-min (share) | Wall-clock reads in tests (share) | Unstable classes (open) |
|---|---|---|---|---|---|---|---|---|
| Workflow engine (Workflows: Nodes, Kinds, Domain; its pages) | 567 / 54,365 | 288 | 284 (32%) | 37 | 10.0 | 28.0 (6.1%) | 64 (4.5%) | 12 (5) |
| Roles and supervision (Roles, Participants, Departments, service Teams) | 267 / 28,225 | 166 | 216 (24%) | 6 | 6.4 | 0.9 (0.2%) | 31 (2.2%) | 5 (5) |
| Loop, the driver's look (driver Loop, modules Loop) | 272 / 38,356 | 246 | 397 (44%) | 8 | 12.2 | 52.2 (11.3%) | 237 (16.7%) | **74 (66)** |
| Workplace (trees, landing, tidy, git) | 618 / 87,167 | 488 | 314 (35%) | 31 | 19.1 | **302.1 (65.4%)** | 158 (11.1%) | 47 (17) |
| Agents (harnesses, accounts, usage; its pages) | 505 / 61,431 | 325 | 297 (33%) | **46** | 11.9 | 7.0 (1.5%) | 90 (6.3%) | 14 (13) |
| Knowledge and the help room (driver, service engine, MCP, pages) | 226 / 25,401 | 146 | 200 (22%) | 12 | 15.1 (14.7 the service's) | 2.4 (0.5%) | 158 (11.1%) | 1 (0) |
| Ask Daoris (driver Help, service Help, web help) | 107 / 12,304 | 68 | 187 (21%); its gathered slice 232 | 7 | 1.6 (ASSISTPKG1's gathered count: 68 classes, 147 case-s quiet, 292 loaded) | 14.3 (3.1%), 13.9 of it one class | 7 (0.5%) | 4 (4); ASSISTPKG1 names five wall-clock-bound unit classes |
| Plugins (driver Plugins, pages, example plugins) | 205 / 27,865 | 131 | 130 (14%) | 14 | 1.4 | 5.2 (1.1%) | 40 (2.8%) | 5 (4), and the most `FLAKE:` lines |
| Install and Update (Install, Instance, modules Update, launcher, publish tool) | 83 / 13,974 | 77 | 147 (16%) | 12 | 5.4 | 0.3 (0.1%) | 32 (2.2%) | 1 (1) |
| Review (opinion, look, check; service Review) | 131 / 19,929 | 70 | 105 (12%) | 1 | 1.4 | 7.8 (1.7%) | 44 (3.1%) | 10 (9) |
| The service (knowledge, quests, sessions, asks; Http, Mcp) | 517 / 62,747 | 320 | 190 (21%) | — | 26.1 | 1.8 (0.4%) | 353 (24.8%) | 1 (1) |
| The web | 1,182 / 169,335 | 787 | 425 (47%) | — | 29 (Vitest, 6.3 min wall quiet) | — | — | web-unit flakes 5 |
| The CLI and doctrine (`src/Daoris.Cli`, `canon/`) | 166 / 14,328 | 77 | 92 (10%) | — | `cli-tests` gate 3.8 to 5.8 min | — | — | **25 merge flakes** (doctrine tests) |
| *Kernel (Platform, Store, Settings, Service client, Events)* | 347 / 41,118 | 194 | 323 (36%) | 9 | 5.8 | 6.3 (1.4%) | 53 (3.7%) | 6 (3) |
| *Conversations (Course, Context)* | 164 / 26,043 | 137 | 198 (22%) | 5 | 12.6 | 28.8 (6.2%) | 65 (4.6%) | 30 (29) |
| *Work orders (asks, quests, channels, the look's passes)* | 204 / 22,392 | 104 | 166 (19%) | 4 | 1.8 | 1.5 (0.3%) | 34 (2.4%) | 2 (2) |
| *Modules bridge (routes, platform, windows, browser)* | 333 / 41,701 | 290 | 343 (38%) | — | 4.4 | 2.6 (0.6%) | 186 (13.1%) | 10 (1) |
| *Place setup* | 52 / 7,377 | 27 | 58 (6%) | 2 | 0.3 | 1.9 | 4 | 1 (1) |

Rows in italics are not in the brief's list; they are measured because the brief's subsystems cannot be judged without them.
Devkit (16 files, 14 landings, 5.7 case-min) and the merge tool (72 files, 71 landings; MERGETS1) are already separate.

Heaviest classes per subsystem (case-seconds): Workplace `IntegrationPromoteGitTests` 670, `TreePoolGitTests` 661,
`PullRequestStateTests` 546; Loop `SessionMessagesTickTests` 628, `StartInSyncTickTests` 275, `SessionsOutliveTheirLookTests` 143;
Conversations `DrivenMoveDowngradeProcessTests` 366, `EndedLimitMoveStoreTests` 170; Workflows `ResolveDoorGitTests` 352,
`GraphRunLandingTests` 343, `ClosedRunEndTests` 202 (unit half, untagged real git); Ask Daoris `HelpTreesReadTests` 835 (rehearsals),
`HelpCommandTests` 42; Agents `LineAccountTests` 236; Plugins `PluginKitTests` 196, `RelayHostProcessTests` 58; Review
`ReviewTreeTests` 165; Install `StagedBuildTests` 98; the service's knowledge `KnowledgeStandingTests` 154, `PresentFirstBenchTests` 77.

The unstable classes with the most findings: Loop `SessionMessagesTickTests` 59, `ChatGoesOnProcessTests` 44,
`AccountRotationTickTests` 43, `TurnStopTests` 30; Conversations `DrivenMoveCrashProcessTests` 52, `DrivenMoveDowngradeProcessTests`
43; Ask Daoris `HelpChatTests` 12; Review `OpinionResumeTickTests` 12. In the merge logs the driver classes most often `FLAKE:` are
Plugins' `RelayHostProcessTests` (6) and `RelayOnceProcessTests` (3).

### 1.2 Coupling and the ports each would need

*Out*: references this subsystem makes to others (distinct types). *In*: references others make to it. "Ports it needs" names §2's
ports by number.

| Subsystem | Out refs / types | It names most | In refs / types | Named most by | Ports it needs to stand alone, and who implements them |
|---|---|---|---|---|---|
| Workflow engine | 1,482 / 482 | Kernel 431, Workplace 323 (`LandedBranches`, `WorkingTree`, `SessionTrees`), Plugins 308 (`NodeDeclaration`, `NodeReach`), Work orders 107, Review 92, Knowledge 85 | 357 / 173 | the modules 118, Loop 78, Transitional 58, Setup 45 | P10 plugin protocol (Plugins), P11 workplace surface (Workplace), P7 service doors (Network adapter), P9 actors read (Roles), P13 config, P3 doors, P1 state sources; git only through P11 |
| Roles and supervision | 628 / 247 | Kernel 406 (`DriverConfig`, `DesktopStore`, `ServiceClient`), Agents 70, Workplace 67 | **1,302 / 389** | Loop 394, the modules 251, Course 132, Work orders 123, Workflows 85, Ask Daoris 60 | provides P9; needs P5, P7, P8, P13, store |
| Loop | **3,235 / 1,129** | Kernel 836, Agents 482, Conversations 437, Roles 434, Work orders 279, Workplace 203, Review 145 | 161 / 42 | the modules 126 (`DriverLoop`, the state slices), the roots | implements P1 and P4 for everyone; needs every other port; as an assembly over the driver (COMPONENT1 b) it needs none to compile, all of them for its tests to be fast (LOOPPORTS1) |
| Workplace | 1,140 / 319 | Kernel 708, Knowledge 203 (`LandedBranch`, `Line`), Install 80 | **1,504 / 543** | Workflows 323, the modules 275, Loop 163, Work orders 159, Review 138 | provides P11; needs git and processes (D286, exist), store, clock, P7, P5's knowledge read |
| Agents | 678 / 198 | Kernel 522 (`SessionEvent`, `Json`, `DriverException`), Knowledge 74, Install 41 | 1,053 / 255 | Loop 459, Course 170, the modules 82 | provides P5 (exists in Ports: `IAgentProvider`); needs processes, store, clock, P8, P13, an accounts and usage read |
| Knowledge and the help room | 322 / 153 | Kernel 88, Ports 54 (memory) | 925 / 207 | Workplace 203, Work orders 98, Ask Daoris 93, Workflows 85, Agents 74 | the driver half is kernel-like records (`LandedBranch`, `Line`, `LoginState`); provides P6; the service's engine stands on `IKnowledgeStore`, `IKnowledgeSource`, `IMemoryEngines` (exist) |
| Ask Daoris | 334 / 195 | Knowledge 93 (`HelpFocus`, `HelpRead`, `HelpMachine`, `IHelpRoomSection`), Roles 58 (`AskDaorisReport`, `ReadTurn`, `ActorEffective`), Events 26, Course 23 (`IExecutor`, `IExecutorWiring`, `IChatHelping`), Service 21, Agents 18, Context 15, Settings 13, Plugins 13 | **86 / 41** | Loop 22, the modules 36 (Help routes, `HelpStarting`, the Help platform presses), the service's Sessions and Mcp 14 | ASSISTPKG1's nine, over §2's layers: `IMachineFacts` (P1 with P6, P9, P13 and the plugin catalogue as topics), `IConversations` (P4, P5, P7), `ITurnEvents` (P8), `IReaderWalk` (P9's walk), `IPressRecords` and `IWindowPlaces` (P3: 9 window routes, the `help` verb), `ICheckouts` (P11), `IAssistantRecords` (P14), `IRoomFolder`; implemented by its desktop adapter and the modules |
| Plugins | 360 / 159 | Platform 71, Events 50, Workplace 45, Knowledge 41, Agents 27 | 496 / 105 | Workflows 308 (the node contract), the modules 63, Loop 38 | provides P10; needs processes (exists), P8, P13, P3 (16 window routes) |
| Install and Update | 123 / 82 | Platform 39 (`HomeLease`, `Downloads`), Settings 17, Service 16 | 404 / 70 | Workplace 80, Work orders 66, Agents 41 (`Tools`, `ToolSpec`, `InstanceDoor`, `PersonPresses`) | Instance is P3's terminal door; Install is the tools' catalogue other parts read |
| Review | 513 / 265 | Workplace 138, Kernel 204, Knowledge 35 | 397 / 145 | Loop 139, Workflows 92, the service's Quests 32 | P11, P7, P13; its check and opinion kinds are Workflows' nodes (`ICheckPort : INodeDoor`) |
| The service | 382 / 314 (Contracts, Ports) | — | 0 (reached over HTTP through `Daoris.Contracts`) | — | already a process; LAYERS1 h's `IServiceUnitOfWork`; inside it the knowledge engine depends on Ports, Registry and Platform alone and is named by Http 30, Mcp 29, Sessions 27 |
| The web | imports by folder: `work` out 793 / in 388, `design` in 831, `data` in 392 | — | — | — | consumes the generated `routes.ts` and `service.ts`: the window's ports are already generated types |
| The CLI and doctrine | — | — | — | — | already separate, with no service or runtime dependency by the brief's constraint |
| *Kernel* | 290 / 217 | Contracts | **5,244 / 503** | Workplace 708, Loop 668, Agents 522, Course 457, Workflows 431, Work orders 421 | it is the base: each package's adapter maps its records into the package's own; its effects go to the adapters |
| *Conversations* | 1,026 / 329 | Kernel 566, Agents 202, Roles 139 | 730 / 171 | Loop 392, Work orders 113 | the run's own (with Loop) |
| *Work orders* | 1,055 / 439 | Kernel 421, Workplace 159, Roles 123, Conversations 113 | 623 / 198 | Loop 251 (`ILookPass`, `LookRound`, `Planner`), Workflows 107 | the run's own (with Loop) |
| *Modules bridge* | 1,810 / 945 | Kernel 358, Roles 339, Workplace 305, Loop 136 | 224 / 92 | the app's root | the window's host of P1, P2, P3: not a component |

Who co-changes with whom (landings touching the subsystem that also touched the cell): Ask Daoris with the modules' bridge 59%, Loop
58%, Knowledge 50%, Course 41%, Workplace 41%; Workflows with Workplace 45% and Loop 45%; Review with Workplace 71%, Loop 70%,
Workflows 58%; Roles with Loop 71%; Plugins with Loop 55% and Workplace 50%.

### 1.3 Where each row's paths are

Driver paths under `src/Daoris.Desktop/Daoris.Desktop.Driver/` and its tests and rehearsals by the same first folder; modules under
`Daoris.Desktop.Modules/`; service under `src/Daoris.Service/Daoris.Service.Core/`; web under `src/Daoris.Web/src/`.

- Workflow engine: driver `Workflows/`, `Defaults/`; `tools/workflows/`; web `workflow/`.
- Roles and supervision: driver `Roles/`, `Participants/`, `Departments/`; service `Teams/`.
- Loop: driver `Loop/`; modules `Loop/`.
- Workplace: driver `Workplace/` (and the tests' `Trees/`).
- Agents: driver `Agents/`; web `agents/`.
- Knowledge and the help room: driver `Knowledge/` (with `Room/`, the help room); service `Knowledge/` and `Daoris.Service.Mcp`; web
  `knowledge/`; `Daoris.Adapters.Memory.Lyntai`.
- Ask Daoris: driver `Help/`; service `Help/`; web `help/`. Its code is wider than these: about 170 source files across a dozen
  folders (web `help/` 56, driver `Knowledge/` 42 of the help room and its facts, `Help/` 38, the modules' Help routes and presses
  15, `Roles/AskDaoris*`, `Context/Help*`, `Events/Domain/AskDaorisRoom.cs`, `Loop/Supervise/AskDaoris*`, service `Help/` 4,
  `Driver.Host/HelpConsole.cs`). The "gathered slice" figures take all of these.
- Plugins: driver `Plugins/`; web `plugins/`; `examples/plugins/`.
- Install and Update: driver `Install/`, `Instance/`; modules `Update/`; `Daoris.Desktop.Launcher`, `Daoris.Desktop.PieceStart`;
  `tools/desktop-publish/`; web `update/`.
- Review: driver `Review/`; service `Review/`.
- The service: `src/Daoris.Service/`. The web: `src/Daoris.Web/`. The CLI and doctrine: `src/Daoris.Cli/` (less `tool-test/`) and
  `canon/`.
- Kernel: driver `Platform/`, `Store/`, `Settings/`, `Service/`, `Events/`. Conversations: `Course/`, `Context/`. Work orders:
  `WorkOrders/`. Place setup: `Setup/`. Modules bridge: the modules' `Bridge/`, `Platform/`, `Windows/`, `Browser/`.

## 2. The shared port set

Three layers, so that each piece is written once and every later package reuses it. Effects follow D286; packages follow ASSISTPKG1
(the D310 draft):

1. **Effect ports** in `Daoris.Ports` (the standard library alone), each with its adapter package, its twin-proved fake and one
   contract case class (D286). A package's core references these, `Daoris.Contracts` where it answers routes, and nothing of the
   driver.
2. **Generic seams of the driver and the modules**, which name no package and which every package's desktop adapter builds on.
   ASSISTPKG1 opens the first two (b: a conversation kind on the one chat runtime; e: parts read from the assemblies the root hands).
   The others exist as funnels today and are opened the same way, by the first package that needs them, for any package.
3. **A package's own ports**, in its core, whose records carry names and states only, never a driver type (ASSISTPKG1 §2.2: the
   assistant's nine). A port shape a second package needs moves, with its records and contract cases, to a shared folder (in or
   beside `Daoris.Ports`, named by that change's decision) in the change that adds the second consumer. It is never copied (one way
   per job), and nothing is made shared before a second consumer exists.

*Consumers* are the source files per subsystem that name the funnel's types today (comments and strings stripped). *In ASSISTPKG1*
names the assistant's port or step that meets it first.

| # | Port | Layer | Consumers today (files) | Today's funnel | Implemented by | State | In ASSISTPKG1 |
|---|---|---|---|---|---|---|---|
| P1 | **The driver's state reads**: the facts hub's topics for packages; the window's slices | 2, and each package's own read port | the modules 95, Loop 33 | the facts hub's judges, registered from the modules (`Modules/Bridge/DriverModule.cs:118`, `HelpMachineDesk.KeepFacts`); for the window `Modules/Loop/DriverLoop.cs:19` (103 members, the window's one handle on the driver), 16 slices such as `Modules/Loop/DriverStateSlice.cs:25`, each a judge delegate (`StateSlice`, `Modules/Platform/StateSlices.cs:79`) | each package's adapter maps the hub's topics into its records; the modules host the slices | hub and slices **exist**; a topic as a part any package can add is **new** | `IMachineFacts` (step f) |
| P2 | **Window pushes**: a scope told changed | 2 (the modules') | the modules 62, Loop 29 | `Modules/Platform/StatePublisher.cs:159`, `[Publishes]` (`Modules/Bridge/DriverModule.cs:606`, 185 uses), `[ByEvent]` (`:652`, 38 uses), Shenora's `IEventBus` | the modules, for every package's routes | **exists**; stays the inbound adapter's | the routes under `Modules/Assistant/` publish as today (step i) |
| P3 | **Doors and presses**: a package's window routes, its terminal verb, its press records and the window's places | 2 (the modules' and the driver host's), and each package's own press port | the modules 56, Work orders 23, Workplace 10, Install 7, Roles 5 | 317 `[DriverRoute]` handlers under `Modules/Bridge/Routes/`, mapped by `Modules/Bridge/RouteHandlers.cs:17` and `DriverModule.cs:25`, handed the whole `DriverContext` (`Modules/Bridge/DriverContext.cs:22`); terminal doors `Instance/InstanceDoor.cs:11`, `PersonPresses` (`Instance/InstanceRoutes.cs:132`), `Settings/SettingTwins.cs:8`; the service's `Service.Http/PersonDoors.cs:93` | the modules, with each package's routes in `Modules/<Package>/` answering its desk's records, one payload fixture each that both sides read | routes and parts kit **exist**; the per-package folder and payload fixtures are **new** | `IPressRecords`, `IWindowPlaces`; routes and fixtures (step i) |
| P4 | **Session and run control**: a conversation kind placed, started, gone on, stopped and swapped at a turn's end; parts found in the root's assemblies | 2 | Loop 43, Agents 7, the modules 7, Conversations 6, Work orders 5, Workplace 4 | `Loop/Chat/ChatRunner.cs:130` and four more part readers that read the driver's own assembly alone (`Loop/Driver.cs:140` look passes, 48; `:148` run stages, 13; `Agents/Acp.cs:601` turn stages; `Workflows/Kinds/NodeKit.cs:16` node kinds, 30); the executor seam `Course/Chat/Executors/IExecutor.cs:12` and `IExecutorWiring.cs:9` (13 executors, Ask Daoris's `Help/HelpStart.cs:18` among them); `Loop/Run/IRunWiring.cs:11`, `Participants/RunningSessions.cs:27`, `Agents/SessionProcesses.cs:32`; the modules' `Bridge/AgentActions.cs:37` | the run (Loop with Course) | the executor seam **exists**; `IConversationKind` and the cross-assembly reader are **new** | `IConversationKind` (step b), the part readers (step e), `IConversations` |
| P5 | **Harness**: an agent's run, the roster and selection | 1 (the run), 2 (the roster read) | Agents 65, Loop 46, Conversations 20, the modules 17, Roles 15, Plugins 12, Ask Daoris 3 | `Agents/Contract/AgentRun.cs:16`, `Agents/HeldRun.cs:9`, `Agents/HarnessRoster.cs:31`, `Agents/Registry/ProviderRegistry.cs:28`; `Ports/Agents/IAgentProvider.cs` | the `Agent.*` packages over `AgentRuntime.Lyntai` (LAYERS1 j); the roster by Agents | **exists** in Ports (D286); the roster read is **new** | reached through `IConversations` and `IMachineFacts`' agents topic |
| P6 | **Knowledge read**: the help room's facts, the doctrine, the landed records | 2 (the facts hub's topics) | Knowledge 56, Workplace 44, Workflows 17, Ask Daoris 13, Conversations 6, Loop 5 | five ambient locators installed by `[ModuleInitializer]`: `KnowledgeHelp.Current` (`Knowledge/KnowledgeHelp.cs:26` and `:45`, read in 8 files), `KnowledgeWorkOrders` (7), `KnowledgeWorkplace` (11), `KnowledgeReviews` (6), `KnowledgeWorkflows` (2); the service's `Knowledge/Abstractions.cs:11` (`IKnowledgeSource`) and `:32` (`IKnowledgeStore`) | Knowledge's records; the topics' judges in each package's adapter, which sits above the whole driver | the service side **exists** (D286 k); the locators are a bridge the ambient ratchet does not count | step f deletes three locators; `KnowledgeWorkplace` and `KnowledgeReviews` keep `Trace` and `SetUpSits` |
| P7 | **Service doors**: quests, sessions, asks and records over the service | 1 | Work orders 84, Loop 66, Workflows 53, Conversations 50, Workplace 45, Kernel 44, the modules 36, Roles 31, Review 19 | `Service/ServiceClient.cs:26`, `Service/ServiceDoors.cs:8` (`IServiceDoors`) | the Network adapter over HTTP (LAYERS1 g), `MemoryServiceDoors` | **exists** (to Ports at g) | the ledger's `/api/sessions/help` behind `IConversations` |
| P8 | **Turn and session events** | 2 | Conversations 45, Loop 44, Agents 28, Work orders 10, Roles 9, Kernel 9, Plugins 8, Ask Daoris 7 | `Events/ISessionEvents.cs:9`, `Events/Domain/SessionEvents.cs:192` (`SessionEvent`), `Context/Events/SessionEventLog.cs:25` | Kernel (Events, Context) | **exists** as `ISessionEvents` | `ITurnEvents` (step g): the first candidate to promote, since Agents, Plugins and the run read the same events |
| P9 | **Actors read and the supervisor walk**: the role catalogue, the effective actor, a packet-only walk on a supervisor's holder | 2 | Roles 41, Loop 29, the modules 20, Conversations 19, Workflows 7, Work orders 7, Ask Daoris 6 | `Roles/RoleCatalog.cs:43`, `Roles/ActorChoices.cs:9` (`ActorEffective`), `Roles/ISupervisorDesk.cs:29`, `Loop/Supervise/SupervisePass.cs:18` | Roles; the walk by the run | **exists** as types; a read port is **new** | `IReaderWalk` (step g) |
| P10 | **Plugin protocol**: the node contract and the work runtime over a process and a wire | 1 (a new effect port) | Workflows 133, Plugins 23, Loop 7 | `Plugins/WorkRuntimePorts.cs:7` (`IWorkPerformPort`) and `:46` (`IWorkRuntimePort`), `Plugins/Domain/WorkWire.cs:187` (the frames), `Plugins/HookPeer.cs:21`, `Plugins/HookProcess.cs:14`, `Plugins/Domain/NodeDeclaration.cs:86`, `Service/INodeDoor.cs:7` | Plugins' runtime package (the hook process over `IProcessHost`) | **exists** as driver interfaces; **new** as a port | the plugin catalogue and grants as an `IMachineFacts` topic |
| P11 | **The workplace's reads and acts**: a session's tree opened, closed and landed, the landed branches, a checkout read | 2 | Workflows (151 types), Loop (104), Review (79), Work orders, the modules | `Workplace/WorkingTree.cs:19`, `Workplace/SessionTrees.cs:37`, `Workplace/LandedBranches.cs:26`, `Workplace/TreeLanding.cs:7`, `Workplace/WorkplaceProcess.cs:9` | Workplace over `IGitRepository` | **new**, after LAYERS1 f's Integration and Landing families | `ICheckouts` (step h) reads Workplace through its adapter first; P11 later gives that read its seam |
| P12 | **Clock**, and work off the awaited path | 1 | 16 subsystems (Workplace 80, Loop 65, the modules 65) | `TimeProvider`; `Platform/Moments.cs:20`; work off the path is a `Task.Run` today (the readings queue) | `Daoris.Adapters.Clock`, `MovedClock`; a queue the adapter runs and a test pumps | the clock **exists** (D286 c); the queue is **new** | `TimeProvider`, `IWorkQueue` (step g): the queue is the second candidate to promote (the run's background passes want the same) |
| P13 | **Config**: the driver's configuration and machine settings | 2 | 15 subsystems (Workflows 70, the modules 52, Workplace 43, Loop 42) | `Settings/DriverConfig.cs:17`, the most named kernel type | Kernel (Settings), read by each adapter into its package's records | **exists** as a record | a topic of `IMachineFacts` |
| P14 | **Store**, and a package's own records | 1, and each package's own records port | Agents 25, Workplace 25, Kernel 23, Roles 14, Conversations 13, Ask Daoris 4 | `Store/DesktopStore.cs:13`; `Ports/Store/IDesktopUnitOfWork.cs:10` | `Daoris.Adapters.DesktopStore.Sqlite`, `MemoryUnitOfWork`; each package's families stay its own | **exists** (D286 b) | `IAssistantRecords` over its files and `conversation_facts` |
| P15 | **Git** | 1 | Workplace 194, Workflows 23, Review 16, Work orders 15, Loop 14 | `Workplace/Git/GitRepository.cs:12`; `Ports/Git/IGit.cs:4`, `Ports/Repositories/IGitRepository.cs:67` | `Daoris.Adapters.Git`, `ScriptedGit`, `MemoryRepository` | **exists** (D286 e, f in part) | none: packages reach checkouts through P11 |
| P16 | **Processes** | 1 | Agents 25, Workplace 19, Loop 14, Kernel 14, Install 6, Plugins 5 | `Platform/ProcessStarts.cs:29`; `Ports/Processes/IProcessHost.cs:7` | `Daoris.Adapters.Machine`, `ScriptedProcesses` | **exists** (D286 d) | none: the core starts nothing (banned effects) |
| — | Browser, network, memory engines | 1 | Loop 9 (browser); Install 9, Kernel 7 (network) | `Ports/Browser/IInAppBrowser.cs`; `Platform/DriverHttp.cs:12`; `Ports/Memory/MemoryEngine.cs:145` | D286 rows i, g, k | **exist** or under way | — |

**The package machinery is shared too**, and ASSISTPKG1 builds it once: the boundary ratchet per lane (a), one lock for every
fingerprinted package with its `daoris.package.json` and the `packages-lock` gate (c), the kind and its workflow in
`daoris-adapter.json`'s shape (k, with ADAPTERCHECK1 and ADAPTERRELEASE1), the payload fixtures both sides read (i), and the
banned-effects analyzer on a package's core from its first commit (d). Every later package takes them unchanged.

The brief's set maps so: the driver's state reads are P1 (P6, P8, P9 and P13 are topics or reads beside it); doors and presses P3;
session and run control P4; the knowledge read P6; the clock P12; the store P14; git P15, with P11 above it for packages; processes
P16; the plugin protocol P10; the window's pushes P2. P5 and P7 are added because every subsystem above the kernel names them.

What the set does not do: it wraps none of the about 150 wiring interfaces (`ILookWiring`, `IRunWiring`, `ITurnWiring`…), which stay
with the subsystems that own them, and it moves no kernel record into a shared assembly: each package's adapter maps the driver's
types into the package's own records, as ASSISTPKG1 §2.2 does, so the driver's surface can keep moving without moving a package's
fingerprint.

## 3. Ranking by benefit over cost

**Benefit.** *Off a landing*: the case-minutes a fingerprint would keep off a landing that does not change the subsystem, an upper
bound: its unit minutes x (0.76 - its landing share) plus its heavy minutes x (0.79 - its landing share), 0.76 and 0.79 being the
test-scope plan's §1.7 share of the driver's unit and Process time an average landing reaches (class reach already narrows below
this, so these rank, they do not promise). *Unstable*: classes with findings. *Alone after ports*: landings that touched only the
subsystem, its window, the roots and Loop's wiring, which is what conversation kinds and parts read across assemblies (step 2) make free.
**Cost.** *Surface*: distinct types it names outside plus distinct types named from outside, the ports' width. *Process files*: test
files tagged Process, the real-dependency tests to move or twin. *Coupling*: references out and in.

| Rank | Subsystem | Off a landing (case-min) | Unstable | Alone after ports (of its landings) | Surface (types) | Process files | Why here |
|---|---|---|---|---|---|---|---|
| 1 | **The service's knowledge engine** (inside the service) | 12.5 per service landing (85% of service landings leave it untouched); about 2.4 min of the service gate's 5 | 0 | 29 of 190 service landings touch it | its port exists (`IKnowledgeStore`, `IKnowledgeSource`) | 6 | 56% of the service's unit time sits in one module that names only Ports, Registry and Platform; the cheapest split there is |
| 2 | **Plugins** | 4.2 | 5, and the most `FLAKE:` lines (Relay host and once) | 19 of 130 (15%) | 264 | 13 | already a process and a wire; its real-process suite (`PluginKitTests` 196 s, the Relay classes) leaves merges whole; Workflows is its one big consumer |
| 3 | **Ask Daoris** | 9.2, of which 8.1 is `HelpTreesReadTests`, which LAYERS1 f's Help-trees family moves to memory regardless; about 1.1 is its own | 4 (`HelpChatTests` 12 findings) | 26 of 232 (11%) for the gathered slice | 236, and only **41 named from outside** | 6 | a leaf: nothing names it but Loop's start, the window's routes and the service's Sessions and Mcp; the most change-dense subsystem (147 landings on 38 driver files, 31 per thousand lines, against Loop's 12.5 and Knowledge's 14); it needs nearly every read seam but no git, process or store machinery, so it is the cheapest first package; its plan builds the machinery every later package reuses |
| 4 | **Agents** | 8.3 | 14 | 55 of 297 (18.5%, the most) | 453 | 36 | the best isolated driver subsystem; its harness port exists and LAYERS1 j packages the harnesses as providers found through step 2's part reader |
| 5 | **The Workflow engine, with Review's check, opinion and look nodes** | 17.7 + 6.1 | 12 + 10 | 52 of 284 (18%); Review alone 0 | 655 + 410 | 27 + 14 | high change and low inbound (173 types), but it names Workplace (151 types) and Plugins' node contract (46): it waits for P10 and P11; Review cannot stand apart (co-changes with Workplace 71%) |
| 6 | **Workplace** | **140.7** | 47 | 31 of 314 (10%) | 862 | 208 | the largest benefit by far and the largest cost; LAYERS1 e and f are doing its hard part (the git port and its memory fake); what is left for packages is P11, after f's Integration and Landing families |
| 7 | **The run: Loop, with Conversations and Work orders** | 22.0 + 23.2 + 1.9 | **74 + 30 + 2** | Loop 8 of 397 (2%) | 1,171 + 500 + 637 | 88 + 33 + 6 | the most unstable and most changed, but it composes everything (1,129 types named); COMPONENT1 a's test project is cheap and early, the assembly last after LOOPPORTS1 |
| — | Roles and supervision | 3.8 | 5 | 7 of 216 (3%) | 636 (389 named from outside) | 10 | not now (§5) |
| — | Install and Update | 3.4 | 1 | 5 of 147 (3%) | 152 | 5 | not now (§5) |
| — | Review alone | 6.1 | 10 | 0 of 105 | 410 | 14 | joins rank 5 |

**Where Ask Daoris lands, plainly:** third by benefit over cost, eighth by benefit alone. Its separation will not shorten the average
merge much: its own tests are 1.6 unit case-minutes in its folder (147 case-seconds quiet over the 68 classes ASSISTPKG1 gathers) and
one real class that belongs to LAYERS1 f's Help-trees family anyway; ASSISTPKG1 itself counts 0.3 to 0.5 minutes off an average merge
and 8 to 9 off an assistant-only one. What it buys is a small, fast development cycle for the subsystem that changes most often per
line, five wall-clock-bound unit classes off the driver's tier, and, more valuable to everyone else, the machinery every later package
reuses (one lock, the boundary ratchet, parts read across assemblies, conversation kinds, the kind and its workflow) at the lowest
risk: 6 Process files and 41 types named from outside. Both measures agree that assistant-only work is rare today (ASSISTPKG1: 5 of
680 row-sized landings since 11-03; here: 7 of 187 landings alone among the driver's subsystems, 26 of 232 with the window, the roots
and Loop's wiring free), so its isolation grows only as its steps b, f and l take the incidental touches behind declarations.

## 4. The order

Each step is one builder's package unless it says "in rows". At most three builders at once (the test-scope plan's rule);
GATEPREFLIGHT1 and REACHREPLAY1 apply to each. The steps reuse, never redefine: ASSISTPKG1 keeps its letters (a to l, z), LAYERS1
its rows (D286), COMPONENT1 its a to d. Prerequisites that are not this census's: REACHCOST1 (so build can be told from tests),
LAYERS1 e (`IGit`), TIMEWHY1 and UNITPURE1.

| Step | What | Size | After | Proof | Keep or retire |
|---|---|---|---|---|---|
| **1** | **ASSISTPKG1 a and c, as shared machinery**: a lane and a boundary ratchet per package; one lock for every fingerprinted package (`packages.ts`, `daoris.package.json`, `daoris.packages.lock`, the `packages-lock` gate) | S + M | none | ASSISTPKG1's; this census adds that the boundary census takes any lane as its argument, proved by a second planted lane | ASSISTPKG1's; and the lock is kept whatever step z decides for the assistant, since steps 4 to 6 read it |
| **2** | **ASSISTPKG1 b and e, as shared seams**: `IConversationKind` on the one chat runtime; the five part readers (executors, look passes, run stages, turn stages, node kinds: §2 P4) read the assemblies the root hands, not the driver's own | M + M | b after ASKPUSH1's code part; e after b and d | ASSISTPKG1's; this census adds that all five readers move in e, not the executors' alone, each proved by a part planted in a test assembly (a node kind, a look pass, a turn stage), since steps 4, 6, 8 and 9 need them | keep: one way to find parts; the driver-assembly-only read is deleted in the same change |
| **3** | **The rest of ASSISTPKG1** (d, f to l, z): the assistant's core on its nine ports, its desktop adapter, its window side, its evaluations, its kind and workflow, its capabilities as declarations | two S, four M, three L (d, f to l, z) | its own order | ASSISTPKG1's, and three conditions from this census: (i) the facts hub's topics are parts any package's adapter can add (P1), not a list in `HelpMachineDesk`; (ii) `ITurnEvents`, `IConversations`, `IPressRecords`, `IWindowPlaces`, `IReaderWalk` and `IWorkQueue` keep the assistant's nouns out of their shapes where their records allow, so a second package promotes rather than copies; (iii) the payload fixture of step i is written for any package's routes | ASSISTPKG1 z's |
| **4** | **Plugins' runtime as a provider package**: P10 to `Daoris.Ports` (the node contract, the frames, `IWorkPerformPort`, `IWorkRuntimePort`); the hook process, the peer, the relay and the kit's cycles to a package with its row in the one lock; its 13 Process files (`PluginKitTests`, `PluginKitExampleTests`, the Relay classes) its dependency suite; the catalogue, offers and grants stay the application's; its 16 routes to `Modules/Plugins/` with payload fixtures; FRAMESWEEP1's census reads the frames from the port | L | 1; reuses 3's fixtures | a change to Workflows alone runs no plugin process test; a change to the frames runs the package's suite and Workflows' node cases on the port's fake; the Relay classes leave the merge record's flakes | keep if plugin-only landings' merge wall falls by 2 min median and no Relay flake shows in a merge for two weeks; fold back if the node contract changes in more than a third of plugin landings in a month (not yet a boundary) |
| **5** | **The service's knowledge engine** within LAYERS1 h: `Knowledge/` (75 files) to its own project and test project (79 classes, 14.7 of the service's 26.5 unit case-min); Http, Mcp and Sessions reach it through `IKnowledgeStore` and `IKnowledgeSource` | M | h's start; beside 1 to 4 (the service's lane) | a service change outside the engine runs none of its classes; for those merges the service gate falls from about 5 min to under 3 | keep if that holds on the next 20 service merges; retire otherwise |
| **6** | **Agents**, in rows with LAYERS1 j, door by door as AGENTLYNTAI1 makes each layer-run: the `Agent.*` packages and `AgentRuntime.Lyntai` found as parts through step 2's reader (D286 §2.3 asks it), rows in the one lock; then accounts and usage as a read the run and the assistant's agents topic take; its 36 Process files to the packages' suites | XL in rows | 1, 2; LAYERS1 d (built) | LAYERS1 z's third exercise (a change to Codex's provider runs Codex's suite alone); `Providers.cs`'s built list deleted (D286 row j) | COMPONENT1's: keep if Agents-only merges save 2 min median and the cold build grows under 10% |
| **7** | **Workplace's reads and acts for packages**: P11 cut at the funnels its callers use, over `IGitRepository`; the assistant's `ICheckouts` adapter moves onto it first, Workflows' and Review's reads next | L, then rows | LAYERS1 f's Integration, Landing and Review-graph families | the callers' classes that name the workplace run on P11's fake; the git half's minutes off merges, read from the lock (LAYERS1 z) | a P11 member with one caller is folded back |
| **8** | **The Workflow engine with Review's nodes**, as a leaf package in ASSISTPKG1's shape: its core (the node kinds found by step 2's reader, the graph, the rules) on its own ports; its desktop adapter; Review's check, opinion and look kinds as its nodes; its routes in `Modules/Workflows/`; its kind and workflow | XL in rows | 4, 7 | LAYERS1 z's second exercise (a workflow rule's change runs no dependency test) and only the engine's suite; engine-only landings merge under 5 min | keep if engine-only landings save 2 min median; retire the package (keep the ports) if engine-only landings stay under 10% of its landings for a month |
| **9** | **The run**: COMPONENT1 b with Loop, Course, Context and Work orders together; it implements P4 and P1's slices and keeps the look passes and run stages as parts. COMPONENT1 a (Loop's test project) goes earlier, as queued, after REACHCOST1, and measures the project split's build cost for every step above | XL in rows | LOOPPORTS1; 2 to 8 | COMPONENT1's; the tick families' unit cases on memory under a second each (LOOPPORTS1); `DriverLoop`'s 103 members and `DriverContext`'s reach narrowed to what the run's routes need | COMPONENT1's: keep if the run's project runs under 3 min with its build and run-only merges' `reached` is under 5 min; retire if the cold build grows more than 10% |
| **z** | **Measured**, weekly with TESTMEASURE1: per package its own landings' merge wall, its test minutes off merges, its flakes in merges, the cold build, cold and warm labelled | S | each step | §1.1's figures again, one gate alone on a quiet machine (rule 17) | a package whose figures do not move is retired, not defended |

**How the steps share the work.** Steps 1 and 2 are built once, inside ASSISTPKG1, and every later step reads them: the lock and the
boundary ratchet (4 to 9), the part reader (Agents' providers in 6, node kinds in 8, look passes and run stages in 9), the conversation
kind (any later conversation persona, and helpers or partners if ASSISTPKG1 §2.4's condition is met). Step 3's port shapes are the
candidates to promote: `ITurnEvents` and `IWorkQueue` at the run (9), `IPressRecords` and `IWindowPlaces` at the engine (8), and each
is promoted in the change that adds its second consumer. P10 is built once in step 4 and read by 8; P11 once in step 7 and taken by the
assistant's `ICheckouts` adapter, 8 and 9.

**Where this meets the other plans.** ASSISTPKG1 is steps 1 to 3 unchanged, with the conditions above. COMPONENT1 a stays as queued; b
is step 9; c (Kernel as an assembly) is not needed, since packages map the driver's types into their own records rather than share
the kernel's; d is per package through ASSISTPKG1 c's one lock, which is the fingerprint the brief asks for. LAYERS1's rows are
unchanged; h gains step 5; j's providers are found through step 2. LOOPPORTS1 precedes step 9. ADAPTERCHECK1 and ADAPTERRELEASE1,
which ASSISTPKG1 k waits on, serve every package's kind.

## 5. What not to separate, and why

- **Kernel** (Platform, Store, Settings, the service client, Events): named 5,244 times from 26 cells (503 types), touched by 36% of
  landings and alone in none. It is the base: each package's adapter maps its records into the package's own, its effects go to
  D286's adapters, and COMPONENT1 c is not needed.
- **Roles and supervision, for now**: 389 of its types are named from outside (Loop 394 references, the modules 251, Course 132,
  Work orders 123); it changes alone in 3% of its landings and is being redrawn by D304's actor rows (ACTORFIELD1, ONEWAIT1,
  CASTING1, HOLDDOOR1, REPORTLINE1b); its tests are already fast (6.4 unit case-min, no heavy minutes to speak of). It offers P9's
  reads and walk (the assistant's `IReaderWalk` in step 3 is the first reader); its package is reconsidered once ACTORTRIAL1 has
  kept or retired the actor design.
- **Review on its own**: 105 landings, one alone, none alone even with the window and wiring free; it co-changes with Workplace 71%,
  Loop 70% and Workflows 58%. Its kinds are the engine's nodes (`ICheckPort : INodeDoor`): it joins step 8.
- **Install and Update**: 5.4 unit and 0.3 heavy case-minutes, 5 of 147 landings alone after ports. Its slow tests
  (`StagedBuildTests` 98 s, `HomeMigrationTests` 45 s) are disk and clock waits for TESTSLOW1's waves, not a package. Instance is
  P3's terminal door; the launcher and the piece starter are already programs of their own.
- **Conversations and Work orders apart from Loop**: Loop names them 437 and 279 times; their unstable classes (the Move matrix,
  `LimitWaitsProcessTests`, `IntakeTests`) are the run's and MOVEMATRIX1's. They separate with Loop at step 9, never from it.
- **The driver's Knowledge records**: `LandedBranch`, `Line`, `LoginState`, `ServerEntry` are named from 33 cells (925 references):
  kernel records, not a component. A package's adapter maps them into its own records; the help room and its facts go with Ask
  Daoris (ASSISTPKG1 f) and the service's engine is step 5.
- **Place setup, Departments, Transitional**: 52, 35 and 45 files, 58, 22 and 58 landings, under 2 heavy case-minutes together.
  Transitional is a migration shelf to delete, not to package.
- **The modules' bridge**: not a component but the window's host; its routes move into a folder per package (`Modules/Assistant/`
  first, ASSISTPKG1 i) that the package's kind owns, and what stays is the composition and the routes of the subsystems that stay.
- **The web**: already its own package and toolchain, and a window is one bundle. Its cost is reach by area and catalogue
  (WEBREACH1) and the 160 s stories file; the per-area split is a test-project question, not a package one.
- **The CLI and doctrine**: already its own package, with no service or runtime dependency by the brief's constraint. Its costs are
  CLIREACH1 and its doctrine tests' flakes, the most of any suite in the merge record (25: `doc-shapes`, `markdown-fence`,
  `tierrender`), which want a row of their own.
- **The service's Sessions, Quests and Asks**: a reference cycle (Quests and Asks 45 and 24 references each way, Sessions and
  Quests 34 and 21, Asks to Sessions 40). They split only along LAYERS1 h's line between policy and SQLite.
- **Devkit and the merge tool**: already separate; the tool's cost is MERGETS1's.

## 6. What this census could not settle

- **Build against test time.** A component split saves build time (not recompiling the driver's 3,193 files and its 21,000-case test
  assembly for a component change) as much as test time, and nothing records the split today; REACHCOST1's step clocks are needed
  before any step's keep criterion can read its build share.
- **The off-a-landing figures are upper bounds**: they use the plan's subdomain-level reach, and class reach already runs finer.
  They rank the subsystems; each step's proof measures its own.
- **Isolation is measured on paths**, and landings are wide (median 3 driver subsystems), partly from integration merges that carry
  several rows. Ask Daoris's 11% and the others' figures after ports are what the paths say today, not a forecast; ASSISTPKG1's own
  count (row-sized landings of 40 code files or fewer since 11-03) reads the same rarity a different way.
- **The heavy records are from 11-12**, before several classes moved to the rehearsals; they are placed by class name, and 5.0 of
  the 304.2 git case-minutes found no home.
- **The coupling census reads text**, with about 3% noise, and counts references, not calls; a port's real width is its funnel's
  member count, read when each step cuts it.
