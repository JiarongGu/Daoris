# The assistant as a package: Ask Daoris on its own ports, with its own development cycle

Written 2026-11-17 by a planning agent, read-only. I read the integration branch `steward/roles-scope` at `d909152654`, in its
worktree. I read main only as committed objects (`d045735be1`, for the test-plan rows that the branch does not have yet). I ran no
build, test or merge. The figures come from these records, each named where it is used:

- the queue's candidate runs of today: `local/scratch/scope-92590ea/` (the driver's unit half run quiet-ish at 07:30, with the
  modules, service and web-unit runs beside it) and `scope-b34c44d/` (the unit half under load at 08:12);
- the last complete Process-half run: `local/scratch/full-b739ed7/` (2026-11-12);
- `local/merge-record.jsonl` (101 attempts);
- `git log --first-parent main --since=2026-11-03`.

`local/scope-tree/_fixtures/test-timing/` holds only `tool-tests.xml`, so it had no timings for this plan. Read this plan with
`test-scope-plan.md` §1 and §2. Its COMPONENT1 and WEBREACH1 rows are the same kind of cut, and two steps below share their work.

## 0. In short

- **The boundary.** The assistant owns three things:
  - what Ask Daoris knows: the room, its sections, the doors and verbs tables, the go-link places;
  - how a conversation is set up: the focus, the access mode and grants, the start plan, the beyond receipts;
  - what is checked after a turn: the second check's selection, packets, reports, readings and told lines.

  It reaches everything else through nine ports of its own. The driver, the modules and the service implement them.
- **The packages.** These live under `src/Daoris.Assistant/`:
  - `Daoris.Assistant` references the BCL and `Daoris.Ports` only;
  - `Daoris.Assistant.Testing` holds the fakes and the contract cases;
  - `Daoris.Assistant.Tests` holds the unit cases, the fake side of the contracts and the room-tier evals;
  - `Daoris.Assistant.Desktop` holds the ports over the driver and modules (the "desktop adapter"), with its own `.Tests`;
  - an `evals/` folder holds the evaluation cases.

  The driver names nothing of the assistant. It gains two generic seams: a conversation kind, and parts read from the root's
  assemblies.
- **The cycle.** The assistant has its own workspace kind, `assistant`, mapped to `tools/workflows/daoris-assistant.json`. Its suite
  and its evaluation set run only when its fingerprint moves in the one packages lock. A merge reads the lock in milliseconds. A
  driver change runs the adapter's contract suite, never the assistant's suite.
- **The steps.** There are thirteen rows (ASSISTPKG1a to z), each landing green: three S (a, k, z), seven M (b, c, d, e, g, i, j)
  and three L (f, h, l). The room, the start, and the capabilities as declarations are the L rows.
- **The saving.**
  - About 0.5 min of every whole queue run, and less on a reached merge.
  - About 8 to 9 min on an assistant-only merge (12.8 min median today, about 4 after).
  - The assistant's focused test run drops from a `Driver.Tests` build plus 570 cases (147 case-s quiet, 292 loaded) to one small
    project on fakes in under a minute.
  - The five flaky Ask Daoris unit classes leave the driver's unit tier.

## 1. The assistant as it is

### 1.1 Production types

Paths start at `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless named otherwise. Line counts are `wc -l` on the branch.

| Part | Where | Size | What it is |
|---|---|---|---|
| The room's rendering | `Knowledge/Room/` (26 files: `HelpRoomIntro`, `MayDo`, `Verbs`, `Growth`, `MakingAPlugin`, `OwnPlugins`, `Doors` with `HelpDoorRows{Setup,Machine,Acts}`, `HelpDoorPlaces`, `HelpDoorThrough`, `TerminalActs`, `Landing`, `Places`, `Window`, `MachineNow`, `Work`, `Exchanges`, `Workspaces`, `Agents`, `Roles`, `Rules`, `Asks`), `Knowledge/HelpRoomSections.cs` | 2,268 lines | One section per file, registered by one line in `HelpRoomSections.All`. The doors and verbs tables are hand-written English inside C# string builders |
| The room's facts | `Knowledge/Help*.cs` (15 files: `HelpFacts`, `HelpFactsRead`, `HelpMachine`, `HelpFocus`, `HelpFocusGathered`, `HelpFocusTold`, `HelpCircle`, `HelpCheckouts`, `HelpTreeFacts`, `HelpTreesRecords`, `HelpWorkFacts`, `HelpWorkRecords`, `HelpRoomCatchUp`, `HelpRoomFamilies`, `HelpRoomSections`), `Knowledge/KnowledgeHelp.cs` | 1,997 lines | The facts the loop's hub keeps for each topic of the room (machine, sessions, trees, work, history, sweep, exchanges), the room's model (`HelpMachine`) and the catch-up after a write |
| The room and the conversation | `Help/` (28 files: `HelpRoom`, `HelpRoomScope`, `HelpRoomDescriptions`, `HelpRoomWrites`, `HelpStart`, `ChatHelp`, `HelpAccessSwap`, `HelpPlacement`, `HelpPlaces`, `HelpMode`, `HelpFocusing`, `HelpFocusTurn`, `HelpFocusAhead`, `HelpBeyond`, `HelpPressed`, `HelpPressPlace`, `HelpPublished`, `HelpWired`, `HelpLoaded`, `HelpSwap`, `HelpCommand`, the fact readers `HelpMachineFacts`, `HelpRecordFacts`, `HelpRoleFacts`, `HelpRuleFacts`, `HelpBrowserFacts`, `HelpPlaceNotes`, and `KnowledgeHelpReads`) | 3,589 lines | The room written per workspace; the start as `[Executor("help", Start)]` on the one chat runtime; the focus; the mode and its swap at a turn's end; the receipt of what a start was handed; beyond receipts; the `daoris help` verb |
| The second check | `Help/SecondCheck/` (10 files: `AskDaorisWatch`, `AskDaorisTurnEnds`, `AskDaorisSelection`, `AskDaorisRanges`, `AskDaorisPacket`, `PacketRedaction`, `AskDaorisReadings`, `AskDaorisKept`, `AskDaorisTold`, `AskDaorisSecondCheck`), `Roles/AskDaorisReport(s).cs`, `Roles/AskDaorisCharge.cs`, `Roles/SecondCheck.cs` | 1,050 + 369 lines | A turn's end, heard from the record's log, is queued on its own task. Its acts with no yes from the person are selected into ranges and composed into packet-only text. The report keeps the reader's flags. An unseen flag is told once, ahead of the person's next words |
| Its reading queue | `Loop/Supervise/AskDaorisReadingQueue.cs`, `AskDaorisReadingLife.cs` | 215 lines | Off the loop's awaited path, one reading at a time per machine, oldest first, each through the one walk (`SupervisePass`) on the `ask-daoris` supervisor; a static `Reading` set, `Task.Run` and `DateTimeOffset.UtcNow` |
| Smaller parts | `Context/HelpConversations.cs`, `HelpFoci.cs`, `Course/Chat/HelpPlaced.cs`, `Workplace/HelpFocusMark.cs`, `HelpKept.cs`, `Settings/HelpModeNames.cs`, `Plugins/HelpGrants.cs`, `Events/Domain/AskDaorisRoom.cs`, `Transitional/Migration/HelpProposalsStep.cs`, `SecondCheckStep.cs` | 803 lines | The history list, the kept focus, the mode's names, the plugin grants, and the home's migrations |
| The window's routes | `Daoris.Desktop.Modules/Bridge/Routes/Help{Routes,FocusRoutes,BeyondRoutes,ReadingsRoutes,MachineDesk,Payloads,WaitsFact,GrantRow}.cs`, `Loop/HelpStarting.cs`, `SecondCheckHookUp.cs`, `AttentionSecondCheckRows.cs`, `Platform/Help{PressRecord,PressStep,AskedPress,CardPlace,RoomLookLog}.cs`, `SupervisorKinds/AskDaorisKind.cs`, `Platform/Views/SecondCheckOnSaid.cs` | 1,297 lines | `START_HELP`, `HELP_CONVERSATIONS`, `HELP_FOCUS`, `HELP_PUBLISHED`, `HELP_READINGS`, `HELP_READING_SEEN`, `HELP_START_FROM`, `HELP_WIRED`, `HELP_BEYOND`, `SET_HELP_MODE`, `SET_HELP_GRANT`. `HelpMachineDesk` composes the room's facts from about twenty sources. The press record. The second check's hook-up and its attention rows |
| Doors and presses | the room's doors and verbs tables (above); the person's cards are the window's `Platform/Views/` (52 press views, shared with every session's proved press); Ask Daoris presses through the `daoris` verb with its delegate key | | The assistant's code never presses. Its agent runs the verb, and the window judges and cards the press |
| MCP tools it uses | its allow-list (`HelpRoom.Allowed`): `registry`, `knowledge_search`, `knowledge_get`, `knowledge_repositories`, `quest_list`, and `checkout_read` where reads are granted; `Bash/PowerShell(daoris:*)`; edits under the drafts folder | | Only `checkout_read` is its own: the service's `Mcp/CheckoutTools.cs`, `Core/Help/CheckoutBox.cs` and `HelpRequestExchange.cs` (756 lines with `RuleProposals` and `SystemPlanBox`, which serve every session), judged by the driver's `HelpCheckouts` |
| The web's page | `src/Daoris.Web/src/help/` (56 source files, 5,863 lines: `AskDaoris`, `AskPanel`, `AskConversation`, `AskHistory`, `QuickAsk`, `access/`, `focus/`, `receipt/`, `secondCheck/`, `published/`, `ask/`), `bridge/help.ts`, `helpBeyond.ts`, `helpReadings.ts` (391 lines), `locales/*/help.json`, `notifications/HelpPressLine.tsx` | | It imports 32 times from `work/` (the conversation view, the composer, the frame's layout, waits), 27 from `bridge/`, 35 from `design/` and 31 from `locales/` |

**Helpers and partners** are mapped here but stay outside the boundary (§2.4). They are co-working's participants: `Participants/Domain/Helper*`,
`Partner*`, `Participants/Helpers/`, `Participants/Partners/` (41 files, 4,119 lines); `Loop/DriverHelpers.cs`, `DriverPartners.cs`,
`Loop/Helpers/`, `Loop/Look/HelpersLook.cs`, `PartnersLook.cs`, `PartnerActRunner.cs`, `PartnerPassRunner.cs` (2,196 lines); the
service's `HelperDesk`, `HelperBox`, `PartnerDesk`, `PartnerBox`, `HelperScout`, and `Mcp/HelperTools.cs`, `PartnerTools.cs`; the
web's `work/start/AskHelper`, `work/waits/HelperAsks`, `work/pieces/AskPartner` and `settings/repository/Helpers`.

### 1.2 What the assistant depends on

This is a text census of type names, written for this plan. Generic nested names such as `Read`, `State` and `Entry` collide, so the
counts are rough; `StructureTests` already holds the order.

| From | It names (types) |
|---|---|
| `Help/` | Knowledge 44 (its own room model), Workplace 26 (`ConversationFacts`, `GitIdentities`, `SessionHomeFiles`), Course 20 (`IExecutorWiring`, `ChatContext`, `IChatRunning`, `ChatStart`, `ChatQueue`, `ChatPending`), Agents 20 (`PermissionRules`, `RuleLists`, `ProviderRegistry`, `StandingFacts`, `HarnessSelection`), Events 20 (`SessionEventText`, `WireReading`, `ReadingSinks`), Roles 16 (`RoleCatalog`, `StaffLedger`, `SupervisorRuleBook`), Workflows 11, WorkOrders 11, Service 9 (`ServiceClient`, `AskView`, `SessionRecord`), Settings 8 (`DriverConfig`), Context 8, Plugins 8, Platform 7, Participants 7, Instance 5 (`PersonPresses`), Loop 2 (`Driver`) |
| `Help/SecondCheck/` | Roles 21 (the report, the supervisor's decisions, `TurnPress`), Events 12 (`ISessionEvents`, `SessionEvent`), Workplace 11, Platform 7 |
| `Knowledge/Room/` | Workplace 20, Workflows 11, Agents 9, and the rest under 6 each |
| `Loop/Supervise/AskDaoris*` | Roles 17 (supervisor resolution, grants, `AgentHold`), Loop 9 (`LoopContext`, `SupervisePass`), Service (`PriorSession`) |

The room's model takes about twenty driver types (`HelpMachineSources`: `DriverConfig`, `IKnowledgeSnapshot`, `RepositoryLine`,
`HarnessReport`, `AskView`, `PluginCatalog`, `LandedBranch`, `PluginOffer`, `ParkedQuest`, `HeldQuest`, `HistoryReading`,
`WorkflowFileFound`, `HelpGrantSet` and more). Today five ambient locators carry the upward reads (FOUNDATION1 mod-viii):
`KnowledgeHelp`, `KnowledgeWorkOrders`, `KnowledgeWorkplace`, `KnowledgeReviews` and `KnowledgeWorkflows` (`.Current`, installed by
`[ModuleInitializer]`). Only the assistant's files read `KnowledgeHelp`, `KnowledgeWorkOrders` and `KnowledgeWorkflows`.
`KnowledgeWorkplace` also serves `Trace`; `KnowledgeReviews` also serves `WorkOrders/SetUpSits` and `QuestReviewCommand`.

### 1.3 What depends on the assistant

| Dependent | Files | What it names |
|---|---|---|
| Driver `Loop/Chat` | `ChatRunner.cs` (25 uses), `ChatRun.cs` (14), `ChatHolds`, `ChatConclusion`, `ChatServers`, `ChatWatch` | Ask Daoris special cases inside the one chat runtime: `place.Name == HelpRoom.Repository`, `HelpPlacement.UnderMode`, `HelpWired` receipts, `AskDaorisTold.Ahead` as `Turns.Ahead`, `HelpAccessSwap` at a turn's end, `Context.HelpModes`, `Context.Rooms`, and `GitSessionAccess` in full mode |
| Driver `Course/Chat` | `ChatHelping`, `ChatPlace`, `ChatContext`, `ChatRunning`, `GoingOnRun`, `BriefedMove` | the help executor's port `IChatHelping`, `HelpPlace`, the go-on of an Ask Daoris conversation |
| Driver, other | `Loop/Look/LookDoors.cs:39` (`OfferReadings`), `Workplace/ConversationPlaces`, `CanonicalLine`, `KnowledgeWorkplaceReads`, `WorkOrders/AtWorkExchangeReading`, `KnowledgeWorkOrderReads`, `Roles/Domain/ParticipantWork`, `QuestionSupervisorDecisions`, `SupervisorInstruction`, `Instance/InstanceRoutes` (`HELP_FOCUS`), `Transitional` | the look offering readings, conversation places, the `ask-daoris` work, the supervisor's kind, the route names |
| Driver host | `HelpConsole.cs`, `Program.cs:434` | `daoris help` |
| Modules | `Bridge/DriverModule.cs:118` (`HelpMachineDesk.KeepFacts`), `Loop/LoopRun.cs:60` (`SecondCheckHookUp.Start`), 16 `Loop/`, 12 `Bridge/` and 7 `Platform/` files | the room's facts and the second check composed at the window's start, plus the routes, attention rows and press record |
| Service | `Sessions/ParticipantWork.AskDaoris`, `SessionLedger.IsHelp` and `HelpRepository` (the `daoris:help` twin), `Help/CheckoutBox`, `Mcp/CheckoutTools`, `CallPlace` | the record's identity and the checkout read's exchange |
| Web | `app/` (shell doors, opener, commands), `settings/` (`AiDomain`, `AiJobs`, `WiringDomain`, `GetStarted`), `work/` (`WorkFrame`, `NewConversation`, `StartPane`, `RenameAsk`, `Markdown`), `workflow/edit/WorkflowEditor` (`openDraft`), `person/standing`, `notifications/HelpPressLine`; the twins `PLACE_DOMAINS` (`help/places.ts`) and `HelpPlaces.Domains`, and `HELP_REPOSITORY` | |

### 1.4 Its tests, their time, and what binds them

Case-seconds come from the trx records named in the header. "Quiet" is `scope-92590ea`; "loaded" is `scope-b34c44d`.

| Project, tier | Classes | Cases | Case-s | Heaviest |
|---|---|---|---|---|
| Driver.Tests unit (79 source files, fixtures included) | 68 | 567 | 147 quiet, 292 loaded (2.7% and 3.6% of the half) | `HelpCommandTests` 42 / 7, `HelpConversationsTests` 29 / 98 (the desktop store), `HelpFactsTests` 12 / 96 (61 clock and wait reads, files), `AskDaorisReadingQueueTests` 11 (10 cases, each builds a whole `Driver` with role staffing and a home migration on disk), `HelpGoOnTests` 8 / 24 |
| Driver.Tests Process (7 tagged files) | 6 | 20 | 25 | `HelpChatTests` 9, `AskDaorisOpeningTests` 7, `FailedOpening`, `Stop`, `Focus`; `PacketOnlyReaderProcessTests` not executed on 11-12 |
| Git half, now the rehearsals | 2 | 10 | 837 | `HelpTreesReadTests` 835 (145 worktrees opened in turn). This is the room's trees fact over real git: LAYERS1 f's item, which stays driver-side |
| Modules.Tests unit | 13 | 68 | 4.5 | `DriverModuleHelpStartTests`, `DriverModuleHelpHistoryTests` |
| Service | 1 | 11 | 1.5 | `HelpSessionTests` (plus the `McpCheckoutToolTests` cases) |
| Web (Vitest) | 45 files | 468 | 109 file-s | `AskDaoris.test.tsx` 48.9, `AskHistory` 18.8, `AskDaoris.scope` 11.8, `HelpModePicker` 11.7 |
| Helpers and partners (outside), unit / Process / git / modules / service | 22 / 4 / 2 / 3 / 6 | 329 / 13 / 12 / 33 / 23 | 10 / 122 / 42 / 1.4 / 27 | `HelperProcessTests` 55, `PartnerProcessTests` 40, `PartnerLandingTests` 31, `McpHelperToolTests` 22 |

**The flaky ones today.** None of these classes is slow by case time; they are bound to the wall clock and to background work.

- **`AskDaorisReadingQueue`** (1.1 s a case, `Task.Run` drains over a whole `Driver`).
- **`AskDaorisWatch`, `SecondCheck` and `ToldDispatch`.** They wait in `Poll.Until` for `AskDaorisTurnEnds`' own task, over real
  `SessionEvents` files.
- **`HelperLook` and `PartnerLook`.** These are the loop's looks with stand-in agents.
- **`HelperProcess` and `PartnerProcess`.** These are node stand-in agents in the Process tier.

Each waited on a 10 or 15 s deadline that a loaded run reached first. `6ec62b4d1f` and `d909152654` replaced those deadlines with the
suite's counted polls. That fixes the symptom. The cause stays: real threads, files and clocks in unit-tier tests.

**How often it changes.** Since 11-03, 103 of 680 row-sized landings on main (40 code files or fewer, excluding records) touched the
assistant's files. Only 5 touched the assistant alone (with the catalogues and ratchet lists), and 8 touched the assistant plus the
conversation plumbing. Most others touched it in passing: a new verb's or door's row in `HelpDoorRows*` or `HelpRoomVerbs`
(`954e55c29d`, `7c1395e38c`), or a cross-cutting test fix (`a23606248c`, `6de1fd542d`, `90deec3137`).

## 2. The boundary

### 2.1 What the assistant owns

1. **The room.**
   - What Ask Daoris is told: the sections and their order, each section's facts and words.
   - The projection of the facts to a workspace or a focus (`HelpRoomScope`).
   - The doors table, the verbs table, the terminal acts and the go-link places.
   - What the room allows and asks (the allow-list, the drafts rule, the posture by mode).
   - The room folder's contents (`AGENTS.md`, `CLAUDE.md`, `.claude/settings.json`).
2. **The conversation's setup.**
   - The start plan: the room, the rules handed by mode, the reads granted, the plugin grants, the focus told in the standing block,
     and the brief carried from an earlier conversation.
   - The focus and its changes (ahead, by turn, widened).
   - The access modes and grants, and what an access change ends at a turn's end.
   - The receipt of what was handed, and the beyond receipts.
   - The history list's rows (`HelpConversations`).
   - The judgement of a checkout read.
   - The `daoris help` verb's semantics.
3. **The second check.**
   - The selection of a turn's unconsented acts, the ranges, the packet and its redaction.
   - The reports and their findings.
   - The queue's policy: oldest first, one at a time, a day's stale close, off closes unread.
   - The told lines and when a flag is told.
4. **The capabilities registry** (step l). One declaration per capability (a tool, a door, a verb, a reader, a section), each with
   its allow rules, its room words, the facts it reads and its eval cases.

It does not own the conversation runtime (D280: one conversation under every chat, driven session and Ask Daoris). It also leaves
out the agents and harnesses, the supervisors' walk, the press cards and their judgement, the facts hub and its judges' reads of git,
the service's ledger, the MCP host, and the knowledge store.

### 2.2 Its ports

Each port is an interface in `Daoris.Assistant/Ports/`. Its value records are the assistant's own and carry names and states only:
the room's own rule, never a driver type. Each has a fake in `Daoris.Assistant.Testing` and contract cases run on both sides.

| Port | What it answers | Implemented by | Fake |
|---|---|---|---|
| `IMachineFacts` | the room's readings by topic (machine, sessions, trees, work, history, sweep, exchanges, roles, rules, plugins with offers and grants, workflows, browser, agents), each "not read yet" where its judge has not answered, and a `Changed` signal | `Daoris.Assistant.Desktop`'s `DriverMachineFacts`, over the loop's facts hub. Today's `HelpFacts` judges, the `Knowledge/Help*Facts` and `HelpMachineDesk` move there and map into the port's records | `MemoryMachineFacts` |
| `IConversations` | place and start an assistant conversation in a room folder under a mode, with the rules and reads handed; go on, stop, swap access at a turn's end; read a conversation's record (agent, access, mode, focus) | the desktop adapter, over the driver's new `IConversationKind` seam (step b) on the one chat runtime and the service's ledger (`/api/sessions/help`) | `ScriptedConversations` |
| `ITurnEvents` | the turn ends of the assistant's conversations, and a conversation's events after an index | the desktop adapter, over `SessionEvents` (`ISessionEvents`) | `ScriptedTurnEvents` |
| `IReaderWalk` | one packet-only reading of a range on the `ask-daoris` supervisor's holder, answering kept, ended or still queued with the walk's sentence | the desktop adapter, over `Loop/Supervise/SupervisePass` and the role catalogue's resolution and grants | `ScriptedReader` |
| `IAssistantRecords` | its own records: reports and findings, start receipts, the kept focus and marks, the conversation facts rows, the kept presses | the desktop adapter, over today's files under the home (`AskDaorisReports`, `HelpWired`, `HelpKept`) and the desktop store's `conversation_facts`, formats unchanged | `MemoryAssistantRecords` |
| `IPressRecords` | the presses a conversation made (route, place, card state, receipt) | the modules' `HelpPressRecord` | in memory |
| `ICheckouts` | the checkouts on this machine with where they are, and one read answered | the desktop adapter, over Workplace and the service's `CheckoutBox` exchange; the judgement is the core's | a memory tree |
| `IWindowPlaces` | the window's views, domains and parts that a go link names, and the doors the window has (each press view's sentence) | the modules, over `PressViews.All` and the place catalogue, held to one fixture the web's `help/places.ts` reads too | fixture-backed |
| `IRoomFolder` | write the room's files whole under the room's own gate, and say whether a room was written | the desktop adapter, over `AtomicFile` and `HelpRoomWrites` | in memory |

Platform seams come from `Daoris.Ports`: the clock is `TimeProvider` (`MovedClock` in tests). Work off the awaited path is an
injected `IWorkQueue`: a task in the adapter, and a pump that tests step by hand. The banned-effects analyzer is on from the first
commit, so the core cannot start a process, sleep, read the wall clock or open a socket.

**The inbound side.** `AssistantDesk` is the core's application API: start, go on, focus, mode, grant, conversations, readings, seen,
wired, published, beyond, and the help verb. The modules' routes call it. So does the driver host's `HelpConsole`, and so do the
desktop adapter's executors and look pass.

### 2.3 What the driver gains instead

The driver keeps no knowledge of the assistant and offers two generic seams:

- **`IConversationKind`**, on the chat runtime. A conversation kind supplies four things:
  - its placement under a mode (posture and rules);
  - its receipt of what a start was handed;
  - the words told ahead of the person's next words;
  - what a setting change does at a turn's end.

  `ChatRun` and `ChatRunner` lose their 39 Ask Daoris references, and a second kind (a future assistant persona, a helper) is a part,
  not a branch in the runtime.
- **Parts read from the assemblies the root hands.** Executors (`ExecutorAttribute`, today read from the driver's assembly alone at
  `ChatRunner.cs:130`), look passes and run stages are found this way. The desktop adapter's executors are then found without the
  driver naming them; D286 §2.3 asks the same of agent providers, and COMPONENT1 b needs it for Loop.

### 2.4 Helpers and partners stay co-working's

The brief listed them. They are a session's co-workers:

- **A helper** reads and answers once. **A partner** writes one piece in its own clone (co-working area).
- **Their lifecycle** is a driven line's: admission, waits, the person's acts, tree pools and landings.
- **Their costs** are the Loop's tick (LOOPPORTS1) and Workplace's git (`PartnerLandingTests`).

Moving them would widen `IConversations` to driven lines and trees, which is the "port too wide" risk. Their flakes had the same
cause as the assistant's and were fixed the same way (`d909152654`).

They join the assistant when a capability is shared. That condition is met when a helper or partner needs the room, the capability
registry or the second check, recorded as that row's decision.

## 3. The packages and how the product composes them

```
src/Daoris.Assistant/
  Daoris.Assistant/                 the domain and the application: Room/, Conversation/, Focus/, Access/, SecondCheck/,
                                    Capabilities/, Ports/, AssistantDesk; references the BCL and Daoris.Ports (Clocks)
  Daoris.Assistant.Testing/         the fakes of its ports and the shared contract cases
  Daoris.Assistant.Tests/           unit cases on the fakes, the contract cases' fake side, the room-tier evals, the room goldens
  Daoris.Assistant.Desktop/         the desktop adapter: each port over the driver and the modules' state; the help executors
                                    (start, go-on from an earlier conversation, access swap) as IConversationKind parts; the
                                    readings' look pass; the facts judges and HelpMachineDesk's mapping; references the driver
                                    (a friend declaration) and Daoris.Assistant
  Daoris.Assistant.Desktop.Tests/   the contract cases' real side over the driver's fixtures (unit category), and the Process
                                    classes that run a real conversation (AskDaorisOpening, FailedOpening, Stop, Focus, HelpChat)
  evals/cases/*.json, evals/facts/*.json, evals/baseline.json
  daoris.package.json               the manifest: suite, tests, contract folders, the Daoris.Ports folders it names, evals
```

```
Daoris.Ports (BCL)  <---------  Daoris.Assistant (BCL + Ports)  <---  Daoris.Assistant.Testing / .Tests
                                       ^            ^
Daoris.Desktop.Driver (names none) <--- Daoris.Assistant.Desktop     Daoris.Desktop.Modules: routes call AssistantDesk;
                                       ^                              implements IPressRecords, IWindowPlaces
roots: Daoris.Desktop.App, Daoris.Desktop.Driver.Host --- compose AssistantDesk with the adapter's ports and the modules',
                                                          and hand the adapter's assembly to the driver's part readers
web: src/help/** --> bridge/help*.ts --> HELP_* routes (payload records the desk answers, one fixture both sides read)
```

- **The driver** references nothing of the assistant. A driver change rebuilds the desktop adapter, and class reach selects the
  adapter's contract cases, which guard the ports against the driver's drift.
- **The modules** reference `Daoris.Assistant` for `AssistantDesk` and its answer records. Their route tests are the inbound
  adapter's.
- **The web's page** stays in the web app, its tests in `web-unit`, chosen by area once WEBREACH1 is built. An import-boundary census
  holds `src/help/**` to `bridge/help*`, `design/`, `locales/`, `lib/` and a declared conversation kit (`work/conversation/`'s
  composer, view and markdown). The allowance only falls; it is 32 `work/` imports today. The page becomes a package of its own only
  when the census allows nothing else (§6).
- **The service** keeps the ledger's `daoris:help` identity and the checkout read's exchange. The identity's spelling is held by
  one fixture that the core, the service and the web read, so it is no longer a hand-kept twin.

**The fingerprint**, mirroring D286 §4 and the adapters' tool, is one hash over:

- the tree hashes of `Daoris.Assistant`, `.Testing`, `.Tests` and `evals/`;
- the `Daoris.Ports` folders its manifest names;
- the fixtures inside its folder.

The shared build files are not in it, as with the adapters. `daoris.packages.lock` (step c) holds the row its suite last passed at,
with the evals' result. The adapter, the modules and the web are outside the fingerprint: they change with the rest and are reached
like any other code.

## 4. Its development cycle

**The kind.** `assistant` is a workspace kind declared as the adapters' kind is (ADAPTERROOM1's left declaration). It maps to
`tools/workflows/daoris-assistant.json`. Its paths:

- `src/Daoris.Assistant/**`;
- `src/Daoris.Desktop/Daoris.Desktop.Modules/Assistant/**`, where the routes move;
- `src/Daoris.Web/src/help/**`, `src/Daoris.Web/src/bridge/help*.ts` and `src/Daoris.Web/src/locales/*/help.json`;
- `daoris.packages.lock`.

Assistant work is asked as that kind. Nothing infers the kind from paths (D300).

**The workflow** is `daoris-adapter.json`'s shape with one more check:

```
apart(isolate) -> work -> requests -> holds -> content
  -> suite    check ["packages-lock"]     back to work on failed: no green release of the assistant at the committed fingerprint
  -> evaluate check ["assistant-evals"]   back to work on failed: the agent tier under its baseline, or a case red
  -> check    check "reached"             the repository's gates the change reaches: the adapter's contracts, the routes' tests,
                                          the page's tests by area; never the assistant's suite
  -> opinion -> landing(merge) -> line -> tree(release)
```

The work releases the package (`node tools/merge/packages.ts release Daoris.Assistant`), which runs the suite on the committed
tree. Green, it writes the row, and the work commits it. ADAPTERCHECK1 lets a package declare gates that a check names and that no
reached or full set reads. ADAPTERRELEASE1 makes the release a step. Both are the adapters' left rows, and the assistant needs them
as they do.

**Its gates.**

| Gate | Runs | Where |
|---|---|---|
| the assistant's suite (`Daoris.Assistant.Tests`): unit cases on fakes, every port's contract on the fake, the room goldens, the room-tier evals | when its fingerprint moves: the release, the workflow's `suite` | never at a merge, in the queue or in the full set; `packages-lock` reads its row in milliseconds |
| `assistant-evals`, the agent tier | the workflow's `evaluate` node, and on demand | never at a merge or in the full set |
| `assistant-desktop`, the contracts' real side (unit) | a repository gate, reached by the driver, the modules and the core's ports | every merge that reaches it; small (an estimated 60 cases, under 10 s) |
| `assistant-desktop-process` | the heavy tier, as the driver's Process half today | after a landing |
| the page's tests | `web-unit`, by area | every merge whose paths reach `src/help/**` |

So a driver change runs the adapter's contracts and never the assistant's suite. An assistant-only change runs:

- its suite and evals, in its own development;
- at its merge, `packages-lock`, the light gates, and the adapter's contracts and routes' tests where reached.

None of the driver's unit or Process halves, the full web run or the service's suites runs.

**The evaluation set.**

- **A case** (`evals/cases/<id>.json`) holds:
  - the person's words and the mode (restricted or full);
  - the focus, and the facts fixture it runs over (`evals/facts/<name>.json`, `IMachineFacts` records);
  - what is expected, as an act, never as prose. The kinds are:
    - an answer that names given facts;
    - a go link to a place;
    - a press of a verb with given arguments, on a card in restricted mode and with no card in full access;
    - an ask (`daoris ask --workspace`);
    - a quest sent on to a repository;
    - a refusal naming its rule.
  - what must not appear.
- **The room tier** is deterministic, needs no model, and runs in the suite. For each case it renders the room from the case's facts
  and checks that everything the expected act needs is there:
  - the verb, with the rule its view judges by;
  - the place, in `IWindowPlaces`' fixture;
  - the facts, under the focus;
  - the refusal's rule.

  It is cheap, and it catches a capability removed or a fact that no longer reaches the room.
- **The agent tier** runs a model, in the assistant's own development only (`tools/assistant-eval.mjs`).
  - **How it runs.** Each case gets a scratch home under `_fixtures/assistant-eval/`, with the room written from the case's facts.
    The conversation starts on whatever the `ask-daoris` role's staffing selects (no model is named; model-decoupling). The connector
    runs over a memory service holding the facts, and the `daoris` verb answers from a stand-in that records each press and acts on
    nothing.
  - **How it is graded.** On the press records, the go links parsed from the answer, and the ask or quest records.
  - **What it reports.** The tier, the agent, the model, the tokens, each case's verdict, and the score against `evals/baseline.json`
    per agent family.
  - **What it costs.** It is bounded to the case count (30 to start) and runs on the accounts development already spends. A larger
    budget is the owner's call.
- **Where the cases come from.** Today's real asks are the source. A harvest reads the install's Ask Daoris conversations
  (machine-local) with their receipts and press records, and proposes cases. The builder rewrites every private name to a neutral
  stand-in, then the `sensitive` scan runs before anything is tracked (rule 5). An ask that cannot be made neutral stays out.

## 5. What it unlocks

| To add | Today | In the package |
|---|---|---|
| A room section | a file under `Knowledge/Room/` plus a line in `HelpRoomSections.All`, with its facts read through `HelpMachineSources` (about twenty driver types) and its tests over a driver fixture | a capability file in `Capabilities/`: its facts as `IMachineFacts` records, its words, its eval cases; tested on `MemoryMachineFacts` in milliseconds |
| A door or verb Ask Daoris presses | a row hand-written into `HelpDoorRows*` or `HelpRoomVerbs` (C# string builders), a press view in the modules, the twins in the web | a door declaration (the act, the screen, the verb, how Ask Daoris meets it) beside its capability; the press view stays the window's, named through `IWindowPlaces`; an eval case proves the room offers it |
| A tool | the service's MCP tool, `ToolClasses`, `CallPlace`, the room's allow-list, a keyed request box, and the driver's take and judge | the declaration names the tool, its allow rule, its request record and its judgement (the core's); the service hosts it through the one exchange; one eval case |
| A knowledge reader | a judge on the facts hub registered from the modules (`HelpMachineDesk.KeepFacts`), a `Help*Facts` type, a section | a topic on `IMachineFacts` (the record and the adapter's judge), then sections read it; the port's contract has one case each side |
| A behaviour after a turn | a change inside `AskDaorisTurnEnds`, tested by polling a real task | a step on the turn-end pipeline, tested by pumping `IWorkQueue` by hand |

**One place to test.** For a capability, that is the assistant's suite, with no driver build. For its effect on the person, that is
an eval case. For its reach into the machine, that is one port contract case.

## 6. The migration, in steps

Each step lands green on its own through the ordinary merge, until step k gives the kind its workflow. Sizes follow
the house's S, M and L. The order keeps each move small and avoids the open Ask Daoris rows:

- step b waits for ASKPUSH1's code part, which edits the same chat runtime;
- step g waits for ASKSUPERVISE1 d's code, which edits the second check;
- ASKSCOPE2 and ASKFULL1 have only installed proofs left.

| Step | What changes | Size | Proof | The older reader ends |
|---|---|---|---|---|
| **a. The boundary declared and counted** | the assistant's paths as lane `assistant` in `daoris.lanes.json`; the layers census gains a cross-boundary reference count both ways (assistant to rest, rest to assistant) per type, as a ratchet; nothing moves | S | the census prints today's counts; a planted new reference fails it | none |
| **b. The chat runtime's conversation kind** | `IConversationKind` on the chat runtime (placement under mode, receipt, words ahead, setting change at a turn's end); Ask Daoris its first part, still in the driver; `ChatRun` and `ChatRunner` name no `Help*` or `AskDaoris*` type | M | every Help and Loop chat test green unchanged; a census holds `Loop/Chat/` and `Course/Chat/` free of the assistant's types; a stand-in second kind in a unit test proves the seam | none, in place |
| **c. One lock for every fingerprinted package** | `tools/merge/adapters.ts` becomes `packages.ts` over package roots (`src/Daoris.Adapters`, `src/Daoris.Assistant`); `adapter.json` becomes `daoris.package.json`; `daoris.adapters.lock` becomes `daoris.packages.lock` (schema 2, `packages`, an optional `evals` field); `adapters-lock` becomes `packages-lock`; `daoris-adapter.json`'s suite node names the new gate | M | the six adapters' rows carried over byte for byte; `tools/merge/adapters.test.ts`'s fingerprint cases pass as `packages.test.ts`; a planted input of each kind moves the fingerprint | the tool reads `daoris.adapters.lock` from a branch that carries one, and refuses its merge naming the rebase, until no local branch or worktree HEAD holds the old file; the merge tool lists those refs |
| **d. The core's skeleton** | `Daoris.Assistant`, `.Testing` and `.Tests` with the nine ports, their records, fakes and fake-side contracts, the manifest and the first lock row; the banned effects on; the census bars any `Daoris.Desktop.*` reference | M | the empty package releases its row; a planted reference to the driver or a planted `DateTime.UtcNow` fails the build | none |
| **e. The desktop adapter and parts across assemblies** | `Daoris.Assistant.Desktop` and its tests created; the driver's part readers take the root's assemblies; `HelpStart`, `ChatHelp` and `HelpAccessSwap` move to the adapter unchanged as conversation-kind parts; the roots reference the adapter; the friend declaration added | M | the executors are listed from the adapter's assembly; every Help test green; the driver's source names no Help executor | the driver-assembly-only part read is deleted in the same change |
| **f. The room** | `Knowledge/Room/*`, the room's model (`HelpMachine` as records), `HelpRoomScope`, `HelpRoom`'s rendering and rules, `HelpPlaces`, the doors and verbs tables, and the focus model move to the core over `IMachineFacts` and `IRoomFolder`; the facts judges (`HelpFacts`, `Help*Facts`, `HelpTreesRecords`, `HelpWorkRecords`, `HelpRoomCatchUp`) and `HelpMachineDesk` move to the adapter as `DriverMachineFacts`; `KnowledgeHelp`, `KnowledgeWorkOrders` and `KnowledgeWorkflows` are deleted; `KnowledgeWorkplace` and `KnowledgeReviews` keep their other readers; the `Knowledge/Room` tests and goldens move to the core's tests on fakes | L | the five room goldens (`empty`, `sparse`, `scoped`, `full`, `full-access`) byte-identical; `MachineFactsContract` green on both sides; the moved classes keep their case counts; `HelpTreesReadTests` stays a driver rehearsal (LAYERS1 f) | none: the room's text is unchanged; the three ambient ports go in the same change |
| **g. The second check** | selection, ranges, packet, redaction, reports, readings, told lines and the queue's policy move to the core over `ITurnEvents`, `IReaderWalk`, `IAssistantRecords`, `IPressRecords`, `TimeProvider` and `IWorkQueue`; `AskDaorisWatch`, the queue's `Task.Run` and `SecondCheckHookUp` become the adapter's thin wiring; the flaky classes are rewritten on fakes with a hand-pumped queue | M | the same cases, each under 50 ms; no `Poll.Until`, no `Task.Run` and no wall clock in the assistant's tests; the adapter keeps one wiring case per seam (a real `SessionEvents` turn reaches the queue; a real walk on the stand-in supervisor answers); report files read by the old and new code alike, held by a golden | none: the report's JSON shape is unchanged (compatible builds) |
| **h. The start, the focus and the access** | the start plan, `HelpFocusing`, `HelpFocusTurn`, `HelpFocusAhead`, the mode and grant judgements, `HelpBeyond`, the checkout read's judgement, `HelpConversations`' rows and `HelpCommand` move to the core over `IConversations`, `ICheckouts`, `IAssistantRecords` and `IPressRecords`; the adapter's executors ask the core for the plan and run it on the chat runtime; the Process classes move to `Daoris.Assistant.Desktop.Tests` (Process); `Help` leaves the driver's `structure.json` | L | the Help unit classes on fakes, case counts kept; the Process classes green in their new home; `structure.json` without `Help`; a cold build of the desktop no more than 10% slower (COMPONENT1's bound) | none: the receipt, the kept focus and `conversation_facts` keep their shapes |
| **i. The window's side** | the routes move to `Modules/Assistant/` and answer the desk's records; one payload fixture per route that the C# route test and the page's bridge test both read; one fixture for the places and the `daoris:help` identity, read by the core, the service and the web; the page's import-boundary census | M | a planted payload change red on both sides; a planted import red; `PLACE_DOMAINS` and `HelpPlaces.Domains` read from the one fixture | the page's imports outside the boundary are listed and only fall; the list ends empty, and the page may then become a web package of its own |
| **j. The evaluation set** | the case format; the room tier in the suite; the harvest from the install's asks, scrubbed; `tools/assistant-eval.mjs` for the agent tier; the baseline; the lock row's `evals` | M | 30 cases harvested and neutral (the scan clean); the room tier green; a planted capability removal fails its case; one agent-tier run reports its tier, agent, model and score | none |
| **k. The kind and its workflow** | `tools/workflows/daoris-assistant.json`; the kind `assistant` with its paths; `assistant-desktop` and `assistant-desktop-process` in `daoris.gates.json`; the assistant's suite and evals declared as the package's own (ADAPTERCHECK1) and its release as a step (ADAPTERRELEASE1) | S | an assistant-only change's merge runs `packages-lock`, the light gates and only the reached adapter, route and page tests, under 5 min; a driver-only change runs no assistant suite (the row kept); a change with no green release goes back to the work naming the release | none |
| **l. Capabilities as declarations** | a capability (a section, a door, a verb, a tool, a reader) is one declaration found by `Parts.Declared`; the room's sections, allow-list, doors and verbs tables are built from them; the service's Ask Daoris tool runs through one exchange over declared request records (`checkout_read` first) | L | the room goldens unchanged by the refactor; a planted capability touches one folder and is proved by its own eval case and contract case | the hand-written `HelpDoorRows*` and `HelpRoomVerbs` builders are deleted when their last row is a declaration; the keyed request box's old kind is read until an update's drain finds none pending |
| **z. Measured** | two weeks after k | S | §7's figures read from the merge record and the lock | |

The total is three S (a, k, z), seven M (b, c, d, e, g, i, j) and three L (f, h, l). Steps a, b and c can run as three builders at
once, and step d follows c. Step e follows b and d. Steps f, g and h follow e and can run beside each other once e lands. Steps i and
j follow f, and step k follows j and the adapters' two rows. Step l comes last.

## 7. What it saves, and how it is measured

| Figure | Today | After k | Read from |
|---|---|---|---|
| The assistant's classes in each whole queue run | driver unit 147 case-s quiet, 292 loaded; modules 4.5; service 1.5; web 109 file-s; Process half 25 | none but the adapter's contracts (an estimated 10 s) | the queue's trx and web logs |
| Their wall in a whole queue run | about 0.5 min (the unit half runs about 14 cases at once, the web about 4.6) | under 0.1 min | the same |
| An assistant-only merge | 12.8 min median (the whole fast tier) | about 4 min: `packages-lock`, the light gates (about 1.7) and the reached adapter, route and page tests | `local/merge-record.jsonl` |
| A focused assistant run while building | a `Driver.Tests` build (unrecorded until REACHCOST1) plus 570 cases at 147 to 292 case-s | the core's project alone, under 60 s with its build, each case under 50 ms | the builder's run |
| Flaky Ask Daoris unit classes | five, deadline-bound until `6ec62b4d1f` | none on threads, files or clocks | the time ratchet and the merge record's flakes |

**Per merge.** The average merge saves about 0.3 to 0.5 min, from the whole runs and from reached merges that touched the
conversation runtime. An assistant merge saves about 8 to 9 min.

Since 11-03 about 1% of row-sized landings (5 to 8 of 680) were assistant-only, and 15% touched the assistant. Steps b, f and l move
the incidental touches (a door row, a verb line, a runtime special case) behind declarations and seams. The owner's intent to
engineer the assistant is what raises the assistant-only share.

These are estimates. Step z keeps the change only if two things hold: assistant-only merges have a median under 5 min, and the
desktop's cold build is no more than 10% slower. Otherwise the projects fold back.

## 8. Risks, and how each is met

- **A port too wide.** `HelpMachineSources` takes about twenty driver types, so a port that passes them through is no port.
  - Each port returns the assistant's own records of names and states, one read per room topic.
  - Each port has an API baseline file, as `Daoris.Ports` has, so every widening is a line a reviewer reads.
  - The cross-boundary census (step a) counts members as a ratchet.
  - A port is split before it serves two unrelated topics.
  - Helpers and partners stay out precisely so that `IConversations` does not grow driven lines and trees (§2.4).
- **The knowledge read crossing back.** Today the cycle is broken only by ambient locators: Knowledge sits low, its `Help*` files
  read up through `KnowledgeHelp.Current`, and `Help/` reads down into `KnowledgeRoomPaths`. If the room's facts stayed driver-side
  but spoke the assistant's records, the driver would have to reference the assistant.
  - The facts judges move to the desktop adapter (step f). The adapter sits above the whole driver, so it reads WorkOrders and
    Workplace directly, and three of the five locators are deleted rather than moved.
  - The core reads facts only through `IMachineFacts`. The census bars any driver reference from the core.
  - `Trace` keeps `KnowledgeWorkplace` as the driver's own concern (LAYERS1).
  - If the room ever needs knowledge text (search results, the code map), it goes through a new topic on `IMachineFacts`. It never
    goes through the connector's MCP tools, which are the agent's and not the code's.
- **The window's page coupled to driver internals.** Today the page's types mirror route payloads built from driver records, and the
  page imports `work/` 32 times.
  - The routes answer the desk's records, with one payload fixture each that both sides read (step i).
  - The import boundary only falls.
  - The conversation kit is declared, so a change there reaches the page by import, as it should.
- **Special cases creeping back into the chat runtime, or a second runtime.** The assistant is a conversation kind on the one runtime
  (D280). The step b census keeps `Loop/Chat/` and `Course/Chat/` free of its types. A behaviour another conversation kind could want
  goes on the seam, never into a kind check.
- **The adapter drifting from the driver unseen.** The assistant's fingerprint does not move with the driver. So the real side of
  every port's contract is a repository gate (`assistant-desktop`), reached by the driver and modules paths. A divergence found later
  becomes a contract case on both sides.
- **A fingerprint missing an input.** The manifest lists every folder the suite reads, and the fixtures live inside the package.
  `packages.ts` keeps the adapters' planted tests (an input of each kind moves the fingerprint). Once a week the full set ignores the
  row and runs the suite. A red found there that the row kept is filed as a defect of the fingerprint.
- **Records an older build opens.** These are the reports, the receipts, the kept focus and `conversation_facts`. Every move keeps
  their shapes (`compatible-builds`), each is held by a golden read by both builds, and a rename waits for its own row with a stated
  end.
- **The friend declaration widening.** The adapter sees the driver's internals. A census lists the internal types it names, and that
  list only falls as the driver's generic seams grow.
- **In-flight Ask Daoris rows.** Steps b and g wait for ASKPUSH1 and ASKSUPERVISE1 d. Every other move lands in one short branch, and
  a row in flight rebases onto moved files with the type names kept.
- **The evals.**
  - **Flakiness and cost.** Only the room tier, which is deterministic, gates the suite. The agent tier gates only the assistant's own
    workflow, against a baseline with a stated tolerance, is graded on acts, never prose, and is bounded to its case count.
  - **Private names.** Harvested asks are rewritten and scanned before they are tracked; an ask that cannot be made neutral is
    dropped.
  - **Model naming.** No case names a model, and every run says which tier ran.
- **Build time.** The change adds five projects. COMPONENT1's bound applies: the cold build grows no more than 10%, or the projects
  fold back (step z).

## 9. The decision draft

D310 is free on main and on the branch. It is reserved here, and the builder claims it at step c or d, whichever lands first.

> ## D310 — Ask Daoris is a package on its own ports, developed as the workspace kind `assistant` (2026-11-17)
> **Decision (ASSISTPKG1).** The assistant (room, conversation setup, focus and access, second check, capabilities) is
> `Daoris.Assistant` under `src/Daoris.Assistant/`, on the BCL and `Daoris.Ports` alone. It reaches the rest through nine ports of its
> own, which `Daoris.Assistant.Desktop` and the modules implement; the driver names none of it and offers a conversation kind and parts
> read from the root's assemblies. Its suite and evals run only when its fingerprint moves in the one `daoris.packages.lock`. Its
> development is the kind `assistant`, mapped to `tools/workflows/daoris-assistant.json`. Helpers and partners stay co-working's.
> **Rejected:** a folder in the driver (no build or test boundary); a service process (D286); its own repository (D300); a second lock
> (two ways for one job); the page as a web package now (32 `work/` imports); helpers and partners inside (a driven line's port).

## 10. Rows, in order

Placed under *Conversations and context* in TASKS.md, after ASKPUSH1.

- [ ] **ASSISTPKG1a — the assistant's boundary declared and counted** (tools, driver, modules, web; the owner, 2026-11-17: "a
  comprehensive assistant might need its own package and development cycle"): lane `assistant` with the paths of §1.1, listing the
  rooms of the units that compile them (driver, modules, web; D310's note); the layers
  census counts references across the boundary both ways per type, a ratchet that only falls. Proof: today's counts printed; a planted
  new reference red. Size S. After: none. → `docs/2026-11-17-assistant-package-plan.md`
- [ ] **ASSISTPKG1b — the chat runtime's conversation kind** (driver; D280): `IConversationKind` (placement under mode, receipt,
  words ahead, a setting change at a turn's end), Ask Daoris its first part; `ChatRun` and `ChatRunner` name none of the assistant's
  types (39 uses today). Proof: Help and chat tests unchanged and green; a census holds `Loop/Chat/` and `Course/Chat/` clean; a
  stand-in second kind. Size M. After ASKPUSH1's code part.
- [ ] **ASSISTPKG1c — one lock for every fingerprinted package** (tools; D286, D300): `packages.ts` over package roots,
  `daoris.package.json`, `daoris.packages.lock` with an `evals` field, the gate `packages-lock`. Proof: the adapters' rows carried
  over byte for byte; the fingerprint's planted inputs; the old lock read only from a branch carrying it, until none does. Size M.
  After: none.
- [ ] **ASSISTPKG1d — the assistant's core, its ports and fakes** (new `src/Daoris.Assistant/`; D286): the nine ports, their records,
  fakes and fake-side contract cases, the manifest, the first row; banned effects on; no driver reference. Proof: the empty package
  releases; a planted driver reference and wall clock fail the build. Size M. After c.
- [ ] **ASSISTPKG1e — the desktop adapter, and parts from the root's assemblies** (driver, modules, roots; with COMPONENT1 b): the
  executors found from the assemblies the root hands; `HelpStart`, `ChatHelp` and `HelpAccessSwap` move to `Daoris.Assistant.Desktop`
  unchanged. Proof: executors listed from the adapter; Help tests green. Size M. After b, d.
- [ ] **ASSISTPKG1f — the room in the package** (driver, assistant): sections, model, projection, doors, verbs and places to the core
  over `IMachineFacts` and `IRoomFolder`; the facts judges and `HelpMachineDesk` to the adapter; `KnowledgeHelp`,
  `KnowledgeWorkOrders` and `KnowledgeWorkflows` deleted. Proof: the five room goldens byte-identical; `MachineFactsContract` both
  sides; case counts kept. Size L. After e.
- [ ] **ASSISTPKG1g — the second check in the package** (driver, modules, assistant; D272): selection, ranges, packet, reports,
  readings, told lines and the queue's policy to the core over `ITurnEvents`, `IReaderWalk`, `IAssistantRecords`, `IPressRecords`,
  `TimeProvider` and `IWorkQueue`; the five flaky classes on fakes. Proof: each case under 50 ms, no poll, `Task.Run` or wall clock
  in the assistant's tests; one real wiring case per seam; report files read alike by both builds. Size M. After e and ASKSUPERVISE1
  d.
- [ ] **ASSISTPKG1h — the start, focus and access in the package** (driver, assistant): the start plan, focus, modes and grants,
  beyond receipts, the checkout judgement, the history rows and `daoris help` to the core; the Process classes to
  `Daoris.Assistant.Desktop.Tests`; `Help` out of `structure.json`. Proof: case counts kept; Process classes green; the cold build no
  more than 10% slower. Size L. After e.
- [ ] **ASSISTPKG1i — the window's side of the assistant** (modules, web): routes under `Modules/Assistant/` answering the desk's
  records; one payload fixture per route both sides read; one fixture for places and the `daoris:help` identity; the page's import
  census. Proof: planted payload and import breaks red. Size M. After f.
- [ ] **ASSISTPKG1j — the assistant's evaluation set** (assistant, tools): the case format, the room tier in the suite, 30 cases
  harvested from the install's asks and made neutral, the agent tier reporting its tier, agent, model and score against a baseline.
  Proof: scan clean; a planted capability removal red; one agent-tier run reported. Size M. After f.
- [ ] **ASSISTPKG1k — the assistant's development is the kind `assistant`** (tools, the place's kinds; D300's pattern): its workflow
  `daoris-assistant.json` (suite, evaluate, check), its paths, `assistant-desktop` and its Process twin as repository gates. Proof: an
  assistant-only merge under 5 min running none of the driver's halves; a driver-only merge running no assistant suite. Size S. After
  j, ADAPTERCHECK1, ADAPTERRELEASE1.
- [ ] **ASSISTPKG1l — a capability is one declaration** (assistant, service): sections, doors, verbs, tools and readers declared once
  and found by `Parts.Declared`; the hand-written door and verb builders deleted; the Ask Daoris tool through one exchange. Proof:
  goldens unchanged; a planted capability in one folder proved by its eval and contract case. Size L. After k.
- [ ] **ASSISTPKG1z — the assistant package measured** (steward): §7's table two weeks after k; kept if assistant-only merges have a
  median under 5 min and the cold build is no more than 10% slower, otherwise folded back. Size S. After k.
