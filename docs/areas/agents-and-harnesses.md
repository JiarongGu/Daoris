# Agents and harnesses — how it works now

## What it does

Daoris runs other makers' coding agents as processes on this machine: Claude Code, Codex and dsh, plus two stand-ins with no
model and any harness a plugin declares. Each agent is reached through a door: the native pipe (one process per run, read line
by line) or the protocol door (the Agent Client Protocol on stdio). A start chooses the agent's binary and one of Daoris's
accounts, composes the agent's settings, rules, servers, skills and first prompt, and hands them over in that agent's own
vocabulary. The run is then held through one unit, `TransportRun`, for every kind of session. Daoris reads what the agent
reports about usage, limits and failures from the door, never from the agent's words, and decides from it: cool an account,
walk to another, retry, or stop. Model and effort come from the role's choices, carried in the tool's own terms. Daoris runs
no model loop of its own.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/` unless they start with `Loop/` or `Roles/`.

| Unit | File | Its job |
|---|---|---|
| `Provider`, `AgentDoor`, `Known<T>` | `Contract/Provider.cs`, `Contract/Known.cs`, `Contract/ProviderParts.cs` | The provider record: identity, doors, account pool, word tables, usage question, reach; every part known with its source, or stated absent |
| `DoorAbilities`, `Adoption` | `Contract/DoorAbilities.cs`, `Domain/Adoption.cs` | What a door can do as its callers read it: interactive, resumes and takes settings as the door declares them, and the rest its transport's (servers and rules on the wire, set at the open, words at the next step, held on the wire, resumed by a restart), so no caller tests a door's wire. `Adoption` judges driving an unadopted repository over door ids: a door whose servers ride its wire may; `Drives` asks it of Build's candidates, and the five places that plan by it (the loop, set-up, the terminal's two consoles, the window's `DriverContext`) read the one answer, `BuildDoor.Drives` in Roles (any entry of Build's order that rides the protocol), and carry it as a bool (`unadopted`), never a `SessionWire`; an unknown id may not |
| `AgentRun`, `Transport` | `Contract/AgentRun.cs`, `Contract/Transport.cs` | What an executor holds of a live run; the two transports, `Native` and `Protocol` |
| `AgentCapabilities`, `IAgentFacts`, `Sheet`, `Measurement`, `ProviderFacts` | `Knowledge/AgentCapabilities.cs`, `Knowledge/IAgentFacts.cs`, `Domain/Sheet.cs`, `Domain/Measurement.cs`, `Registry/ProviderFacts.cs` | The provider sheet (COAGENT1 §3): `Sheet.Of` reads each door's capabilities from its registration, an opening's answers or the measurements table (each row's evidence document held to exist), else absent with its fallback; `ProviderRegistry.Facts` answers each agent once with its account acts and its doors' sheets, the door named for it first. The port and its values sit in Knowledge, below Agents, so the room may read them. The roster's `HARNESSES` answer carries them as `agents`, and each door's `wire` is its sheet's word (`acp`, `pipe`), as the help receipt's is. The agent's page reads from it, never from a door of its list: `Tool.acts` (sign-in, key, device code, install) and `Tool.lead`, with each door's rows drawn by `DoorSheetLines`; the room (`HelpRoom.Describe`'s `facts`) says what a person calls a tool from it; `SheetLine` says an installed agent's sheet in `daoris agent list` |
| `ProviderRegistry`, `ProviderRegistration`, `Providers` | `Registry/` | The registration list (Claude Code, Codex, dsh and the stand-ins), the registry made over the built set and the plugins' doors, id resolution by kind. The registry's `OwnerOfAgent` is the one reader of whose accounts a door runs as, its `AgentNamed` of a door's or a holder's program and accounts (an `AccountAgent`) and its `DeclaredBy` of the plugin a door came from; the roster, the terminal's verbs, the window's routes and the hand-over read nothing else. Codex's limit words and usage question are provider `codex`'s: its built door reads them through it, a plugin's door naming `codex-acp` by its `Lent` part, one joined to `codex` neither |
| `AgentOfDoor`, `ProviderReach`, `ReachPlace`, `IReachOpenings` | `Registry/` | Which agent a door is: its provider, never its account key, so one agent's two doors are one agent and two providers sharing a pool are two (a door no provider holds is its own). Whether a door's provider may work at a start's place: `Everywhere`, or `Opened` only where the person opened the repository, the workspace (a start on no repository) or the machine's room (a start across repositories) to it; a joined door is bound by its provider's; nothing is opened until OPENROUTER1 designs how |
| `DoorPorts`, `RegisteredVariables` | `Registry/` | What a reader asks of a registered door beyond its record: on the door, its abilities (`Can`, `DoorAbilities`), its spawn (`Spawned()`, refused by name where it declares none), its dialect's stream reader, message and interrupt frames and session `_meta` (Claude Code's `ClaudeNativeDialect` and `ClaudeProtocolDialect`), its server handing and whether a plugin declared it; through the registry, a start's, a conversation's and a resume's process (`Start`, `Chat`, `Resume`, each the door's recipe completed by the shell and stripped of every other provider's registered variable but for a declared door's), its provider's maker (a declared door's own), rule language and skill layout, and a door's abilities by name. What every registration declared of a spawn's environment, and the refusal of a declared name of Daoris's list |
| `SpawnRequest`, `DoorStarts`, `ProcessRecipe`, `ISpawn`, `Shell`, `ISessionGit` | `Contract/SpawnRequest.cs`, `DoorStarts.cs`, `Contract/ProcessRecipe.cs`, `Contract/Ports.cs`, `Transports/` | What a start asks of a door's spawn (the machine's command, the mode, the asked choice, the conversation it goes on in, whose work it serves, and the home its tools and git holds are read from: the driver's own, or the conversation runner's, never the process's), composed by the start's executor in Loop (`Loop/Session/SpawnRequests.cs`: a driven start's and a resume's from its target, a conversation's from its place) and handed to the registry's `Start`, `Resume` and `Chat` for its door; a door's spawn reads it and answers a recipe (program, arguments, input, its own variables); the transports' one shell completes the recipe from the request alone into every kind of session's process and holds the account half every probe and usage question takes too; the session's git a port the shell declares and Workplace installs, unset throwing |
| `ClaudeCodeProvider`, `CodexProvider`, `DshProvider`, `StubProvider`, `AltStubProvider` | `Providers/<Name>/` | Each provider written on the contract, its facts declared in its folder: Claude Code's two doors, `codex` with its door `codex-acp` and its account facts (`CodexAccounts`), dsh, and the stand-ins `stub` (pipe) and `acp-stub` (protocol), which run a configured command and no model, and `alt-stub` with `alt-acp-stub`, the second pair, whose limit, window and sign-in words (`AltStubWords`) and home variable are its own; each door's spawn its own there (`ClaudeCodeSpawn`, `ClaudeAcpSpawn` with `ClaudeAcp`'s seam, `CodexAcpSpawn`, `DshSpawn` with `DshProfile`, `StandInSpawn`) |
| `ProviderRegistry`, `AccountAgent` | `Registry/ProviderRegistry.cs` | The one registry, made of providers alone (`ProviderRegistry.Built()`; `Joined(doors, declared)`, where `JoinedProviders` places each plugin's door by its account key: into the provider whose own key it names, as a provider of its own keyed by the door it names and reading that door's declarations, or into a provider made of the doors naming a key nothing registers, none depending on the plugins' order, and no carried name replaced): `Names`, `Doors`, `Door(id)` refusing an unknown name, `Reserved` and `Known` derived from the providers, `Resolve(id, kind)` by the kind a field holds. An agent as a roster, a probe and the account verbs read it is an `AccountAgent` (its `ToolSpec` program, pool facts, home, identity, tables, turn report, usage question and account-of), a door's own by `AgentNamed(name)` and the one answering for its accounts by `AccountAgentOf`. Tests that need a shipped door read it from `BuiltDoors.Of(id)`; one that needs a harness the shipped doors are not makes the provider of a registered door with `TestDoors.Door` (id, a `TestAgent`'s facts, transport, interactive, resumes, postures, spawn, dialect) and a registry of them with `TestDoors.Registry` |
| `Spawning`, `CommitTrailers`, `PinNewest` | same names | Daoris's own variables, a list (the identity variables and what the session's git answers it sets); the maker's trailer a commit carries; the newest release a pin moves to, asked one way for the window and the terminal |
| `SessionServers`, `ServerPackages`, `PackageStarts`, `NpxCommand`, `PackageBin` | same names; the last two in `Domain/` | The servers a session is handed, as the harness starts them; a server's package installed once into the toolchain; a package's program started as node and its script; `npx` read as npm reads it; the script a package's manifest names |
| `SessionTarget`, `EarlierMaker` | same names | What a driven start's executor composes, its spawn request composed from it |
| `TargetPrompt` | same name (hub, 11 KB) | The composed first prompt: its doors, the head, the beneath and the tail; the pieces come from the units below |
| `ClaimedPrompt`, `ResumedPrompt`, `AskedPrompt`, `RequirementsPrompt`, `WorkplacePrompt` | same names | The instruction's pieces for a claim and its retry, for a resume or a carry-on, for the ask, for the requirements and their evidence, and for the working tree |
| `TransportRun`, `TransportStart` | `TransportRun.cs`, `TransportStart.cs` | The one run over both transports: spawn, lineage token, capture, wait, the next process of a go-on and a native resume's one retry; the start names its session, transcript, opening and preamble, and everything the door says leaves as readings to the sink its executor hands it |
| `DoorInput`, `DoorWord`, `Delivery` | `DoorInput.cs` | A run's one queue of native words (D243): each word's ids kept from its write until the harness names the conversation, so the run's retry hands the next process every word the ended one never acknowledged, in order, and never one it did; a go-on's held words wait on it for the next process's prompt. Nothing above the run writes to a process's stdin (`ConversationRunCensusTests` holds it). It is the native door's port, beside the protocol door's `ProtocolWords` (D246): a protocol door's input refuses a line, a held word and a control line before anything is written, and `AgentRun` names both ports and no word operation of its own |
| `ProtocolWords` | `ProtocolWords.cs` | A protocol door's words once its session opened, `AgentRun.Words` (D246): its prompt, a word into the running turn and the steer, each a request the session answers, acknowledged by that answer and retried on the same session within the turn; `ProtocolChat` says them here and never writes through the session |
| `IResumeState` | `Contract/` | A native resume's state, which Course's `ResumeAsk` implements: the run marks it opened when its harness names the conversation, and its end tries a structured resume once more where the harness exited nonzero before naming it, never after a clean exit, a refusal, a stop asked or on the plain capture |
| `WireReading`, `IReadingSink`, `ReadingSinks`, `DoorFailure`, `ChildSignal` | `Events/Readings/` | The closed set of readings a run writes (an event, a console line, a stream beside, the conversation named, the account's windows, a child seen or ended, a failure), the one port it writes them to and its composition; each carries its run, the generation of the process that said it, that process's lineage token and its place in the run's order (`StampedSink`), the protocol door's windows and a conversation's failed turns among them; `Failed` carries the `DoorFailure` every failure is on either door, which `DoorFailures` in Agents builds from each wire. An executor composes its run's sink of its owners' parts: Context's `RecordReadings` (the event file and the console), the binding's conversation, Participants' `LineageReadings` (the lineage), the windows |
| `ChatAgent`, `SpawnEquipment` | same names | What a conversation reads of its agent (its door's facts, its process prepared and equipped, its carriers, framing, failure reads, trailers) and the run it makes; the toolchain's environment and the tools-at-the-start switch every session's process is equipped with |
| `AcpSession` | `Acp.cs` (38 KB) | The protocol door's root: the session's state and its public doors, each a line into the units below; builds the turn's stages in their order and implements the wiring interfaces every unit reaches it through |
| `OpenPrompts`, `OpenPrompt`, `PromptPassage` | `Turn/` | The prompts on the wire and the run's door they pass, under one gate whose header names what it guards, the order of the locks and what a race would break: a prompt opens as it is admitted under the write lock, the door shuts between two writes, a word is taken once |
| `AcpPrompting` | same name | A prompt written (through the door, its words marked first, in the order asked) and its answer read on the reader's thread: the turn's event, the hand-off to the words behind it, the receipts, the calls a steer cut; reaches the session through `ITurnPromptWiring` |
| `AcpSteering` | same name | The person's *Send now* steered into the running turn, and its outcome said on the words it cost |
| `AcpTry` | same name | One try of a transient episode: the held words and the go-on in one prompt, each marked before the wire |
| `AcpRun` | same name | A driven run: the open or the resume, the first prompt, the loop over the person's words and the turn ends, and its end, which shuts the door before the words are counted; reaches the session through `ITurnRunWiring` |
| `AcpReader` | same name | The stream read frame by frame, each dispatched before the next: an answer to its request, an update or a request through the turn's stages; its end faults what was awaited; reaches the session through `ITurnReadWiring` |
| `AcpOutcome`, `AcpConfigOption`, `AcpRefusal` | same names | What a run ends with, one config option as the agent offers it, the agent's refusal of a call (and the small failures a run's door throws) |
| `AcpProtocol` | same name | What an `initialize` answer declares (origins, mid-turn words, steering, resume), the open's meta and servers, the client's capabilities and the typed failure; pure |
| `AcpOptions` | same name | The session's config options as last said, the list the open began with, the person's change of one, and the actor's choice carried at the open |
| `AcpOpening` | same name | A session's start and a conversation's resume: the handshake once, the session asked for with the servers and rules, the options kept, the choice carried, the posture set |
| `AcpPosture` | same name | The posture's marks and the mode set on the session once it answers the open |
| `AcpWire` | same name | The frames over the two streams: one line a frame, the requests awaiting their answer, the one writer |
| `AcpWaits` | same name | What a run does where a turn ends: the wait on its own background work and the harness's woken turn, the go-on, the quest re-read, the carry-on of a done quest's tree; reaches the session through `ITurnEndWiring` |
| `AcpEpisodes` | same name | A prompt's or an open's transient failure read, and the wait-and-try episode in the same conversation; reaches the session through `ITurnRetryWiring` |
| `StepCalls` | `Turn/` | The calls announced and not yet finished, and those a steer cut |
| The turn's stages | `Turn/`, listed in `docs/index/registered.md` | Each stage a module registers (`[TurnStage(name, phase, after)]`, `ITurnStage`), run in the kit's order at its phase's anchor in `AcpReader` (Update: each of the session's own updates; Request: the stage that takes the method, else not implemented), built once per session with the session as its `ITurnWiring`; `RefusedCalls` is the state two stages share |
| `AcpReplay`, `AcpWatch`, `AcpHeard`, `AcpSubagent` | same names | The first Update stages, in that order: a load's replay passed over; the update counted and read by the background work and the wake; a word an error left untold taken once the agent goes on; a subagent's update routed to its own stream and taken there |
| `AcpConsole` | same name | The console's lines joined from the agent's streamed words, ended when anything else is said or the words go quiet |
| `AcpMeasure`, `AcpRender`, `AcpPermission` | same names | The stages: the context's high-water mark, the account's windows, the turn's models and the options the agent changed; each update as the record's event and the console's line, and the readings of an update the door's other units call; a permission request refused, or held for the person, and released |
| `HarnessCapture`, `ClaudeStreams`, `StructuredOutput`, `NativePrompt` | same names | The native door: capture, `stream-json` mapping, the prompt written on stdin |
| `NativeFrame` | `NativeFrame.cs` | A native stdout line parsed once: the capture reads each line into one frame, which the children it says (`NativeChildren`), the run's own ending (`HarnessWords`), the streams beside (`ClaudeStreams`, a child's words included) and the dialect's mapper each take, none parsing the text again |
| `NativeChildren` | `NativeChildren.cs` | The children a native stream says: a subagent's `task_started` and its `task_notification`, each read once per child into one `ChildSeen` or `ChildEnded` reading on the run's sink, in the wire's own words; the protocol door's `AcpStreams` says its announcements and ends the same way |
| `HarnessRoster` | `HarnessRoster.cs` (hub, 26 KB) | One judgement for every start, kept as one-line doors to the units below, with the roster's state and settings reads |
| `RosterCooling`, `RosterReport`, `RosterSelection` | same names | Cool-offs, refusals and signed-out accounts; the Agents page's report, a press and an account's status question; choosing the harness and account of a start, a resume and a carry-on |
| `RosterWalk`, `RosterNext`, `RosterStarts`, `RosterUsage` | same names | The account walk and its pass; the next start's reason, last reading and wiring; the numbered starts in flight; the usage questions and the accounts a switch could reach. What each account carried (`SessionUsage.ByAccount`) is counted by agent, each session keeping its door |
| `OfferAsk`, `WalkMemory`, `WalkPass` | same names | What a start asked of the walk, what one walk remembered, and what one pass held |
| `HarnessSettings`, `AccountSet`, `PoolReceipts` | same names | `harnesses.json`: pins, each agent's one account set, its order and use settings, the fold and its receipts; `HarnessSettings` keeps its fields, the resolving and `With…` edits and one-line doors over the three units below |
| `HarnessSettingsFile`, `HarnessSettingsChange`, `HarnessAccountFolders` | same names | The file read and written in the writer's layout; one change under the keep lock, the come-up fold and the pin; where managed versions and accounts live on disk, which folders are accounts, and removing one |
| `AgentAccountUse`, `AgentAccountLines`, `AccountVerbContext` | same names | `agent account use`, the terminal's words about an account's use and last reading, and what every account verb reads; `AgentAccountsCommand` keeps the other verbs and one-line doors to the lines |
| `AccountRotation`, `RotatedOpening`, `RotationWords`, `RotationUse`, `UsageCurves`, `SwitchDecision` | same names, `Domain/` | The walk's order, the rotated start's opening and the walk's words, the next start's reason, the curves, a running session's switch (pure); its records and enums (`AccountState`, `WalkStep`, `NextStart`, and the rest) each in a file of their own |
| `AccountLimits`, `AccountCooling`, `RefusedAccounts`, `AccountSignIns` | same names | Limit words and resets, `cooling.json`, refused credentials, sign-in refusals: the readers only. Each provider's words are its own types in its folder (`ClaudeLimits`, `ClaudeWindows`, `ClaudeSignIn`, `CodexLimits`), and the roster's `LimitsOf`, `SignInOf`, `WindowsOf`, `UsageOf`, `KeyVariableOf` and `WeekFixedOf` read them through the registry's door and the account key it lends one level |
| `AgentErrors`, `TransientRetry`, `TransientRetries` | same names | The four-kind reader of a door's failure, and the retry in the same conversation |
| `AccountWindows`, `IUsageRead`, `UsageReads`, `UsageQuestion`, `ClaudeUsage`, `CodexUsage`, `UsageProjection` | same names, the port in `Contract/IUsageRead.cs`, the readers in `Providers/ClaudeCode/` and `Providers/Codex/`, `Domain/UsageProjection.cs` | Each account's windows, read at no model spend: the usage port (`IUsageRead`, the question's declaration and its read) that `UsagePart` and the toolchain hold, answered by each provider's own `UsageQuestion` (`ClaudeUsageQuestion`, `CodexUsageQuestion`) in its folder saying how it is asked, how long a reading stands and its source, or by the layer's read where `UsageReads` binds one (rule 47), so the roster switches on no kind; the capacity estimate |
| `UsageHistory`, `UsageHistoryReads` | same names | The readings and turns history: kept, rotated and pruned; read back by month with the gaps and the unread counted; its records (`KeptReading`, `KeptTurn`, `HistoryGap`, and the rest) each in a file |
| `HarnessProbe`, `HarnessKeys`, `HarnessSelection`, `StartWiring`, `ProbeLock`, `ScratchSignIns`, `SignInPorts` | same names | Version and sign-in questions, API-key accounts, a selection and the wiring a start is handed, one question per home at a time, no real sign-in under a scratch home |
| `AgentSettings`, `AgentOffers`, `DoorOffers`, `SessionChoices`, `ISettingsFile` | same names; `Providers/ClaudeCode/ClaudeSettingsFile.cs` | An account's model and effort in the tool's own file (the provider's `Models` part states its aliases and efforts); what an agent offered per version and account, read by `AgentOffers.Offers` in two scopes (a session's, an account default's); `SessionChoices` is the session scope the page's actors offer per door, read through each door's `Offers` part (`SettingsOffers`: the provider's file, `max` only where the door says its native session carries it, which is Claude Code's native door; a door that states none, `ListedOffers`: what its open last listed). The start plan, the walk and a switch's candidates ask `AgentOffers.Offers(home, door, version, account, now).Session.Judge(model)`, one account's refusal passing that account while another is tried |
| `IChoiceReask` | `Contract/IChoiceReask.cs`; Loop's `ChoiceReask` | The open's answer to a refused choice, implemented in Loop where the executor is built, the refusal kept in the offers store before the walk: a later choice carried on the same session, or none |
| `PermissionRules`, `RuleProposals`, `ClaudeTrust`, `Across` | `Permissions.cs`, `Providers/ClaudeCode/ClaudeTrust.cs` and same names | Rules in scopes and defaults, an agent's proposals, Claude Code's folder trust, reach into other checkouts |
| `IRuleLanguage`, `ISkillLayout`, `IServerHanding`; `HandedRules`, `SkillSource`, `ConnectorPlan`, `ConnectorSource`, `PostureAsked` | `Contract/Ports.cs`; `Contract/`, same names | How a provider takes the product's rules, lays out a start's skills, and how a door takes a start's servers at its spawn, each handed plain values; a provider or door stating one absent is handed none of it; the posture a start asks of a protocol door (modes, required, the person answers) as one value with its one wire string and reader |
| `ConnectorPlans`, `PluginRegistry` | `Agents/ConnectorPlans.cs`, `Plugins/PluginRegistry.cs` | The one composer of a start's plan per workspace, and where the plugins' doors join the registry (`WithPlugins`, over `ProviderRegistry.Joined`) |
| `DeclaredDoor`, `DeclaredDoors`, `DeclaredSpawn` | `Contract/DeclaredDoor.cs`, `DeclaredDoors.cs`, `DeclaredSpawn.cs` | A plugin's door: Plugins (`PluginDoors`) converts a manifest's named fields into the `DeclaredDoor`, which has no field for steering, resume, rules, words, lineage or confinement (a manifest naming one reaches nothing); the factory (`DeclaredDoors.Door`) makes it a protocol-transport contract door with its own `Identity` (plugin, product, maker), own `Home` (variable, own home) and own program, interactive and no settings file stated here and its resume the transport's (eligibility to negotiate at the open), a door that reads none of its provider's identity, home, program, rule language or skill layout (`Declared`: no rules, Claude Code's skill layout); its provider is the registry's, placed by the door's `accountOf` when it joins (`JoinedProviders`) |
| `ClaudeRules`, `ClaudeSkills`, `ClaudeServers`, `ClaudeLineageHook`, `LineageHook`, `ClaudeConversationHome` | `Providers/ClaudeCode/` | Claude Code's: its settings file with its hooks and hard denials; its per-session plugin copies; the servers file its native door names; the lineage hook's script (`hooks/lineage-hook.mjs`), installed for the lineage file Loop hands; its conversation home, whose project-folder layout (the home variable, its default place, the `projects` folder, a directory's flat name) is what the tool-home leftovers list reads, so Codex, which files by date, is unlisted |
| `SpawnSkills`, `SpawnFiles`, `DshProfile` | same names; the last in `Providers/Dsh/` | A session's skills laid out by its door's provider (`ProviderRegistry.SkillLayout`) and handed by its spawn; what a spawn wrote, removed with the session; the dsh home Daoris made |
| `SessionEquipment.Prepare`, `HandRules` | `Loop/Session/SessionEquipment.cs`, `Loop/Session/SessionRules.cs` | The spawn's assembly, shared by every kind of start, equipped through `SpawnEquipment` |
| `daoris agent …` verbs | `AgentsCommand.cs` (with `AgentRulesVerb`, `AgentTrustVerb`, `AgentRoutes`, `AgentsWorld`), `AgentListCommand.cs`, `AgentAccountsCommand.cs`, `AgentLoginCommand.cs`, `AgentPinCommand.cs`, `AgentLongCommand.cs` | The terminal's doors: list, accounts, sign-in, key, install, pin, update, rules, trust, settings |
| `SessionProcesses`, `LineageTokens` | same names | The live process per session (never sent to the service), and the token each process runs under |

## Its rules

**Agents and doors**

1. An adapter is added deliberately. An unknown agent or door name is an error that names what exists, never a fallback to
   another harness. (from D23, D213)
2. The built set is `claude-code` (pipe), `claude-code-acp`, `codex-acp` and `dsh` (protocol), and the stand-ins. A plugin may
   add harnesses but never replace a built name. The machine's default adapter is `claude-code`. (from D53, D57, D64)
3. Every harness reached over the protocol is a configuration of the one protocol door, not a new seam. (from D53)
4. A provider is an agent with one or more doors. A door's account key is its harness's `accountOf`, else its provider's id,
   followed one level only. `claude-code-acp` runs as `claude-code`'s accounts; `codex-acp` as `codex`'s, held without a door. (from D213)
5. Ids are typed and opaque: carried, stored, compared and shown, never read for behaviour. Every stored record keeps its strings. (from D213)
6. Silence preserves: an adapter that declares nothing behaves as before the feature existed. Every claim about another
   program is read from its binary, source or wire first, never guessed. A contract part stated `Absent` reads the same way;
   `Reach` is never absent. It is read in the plan's one ask (`StartPlan.AccountsAsync`) before any account is asked: a choice whose
   provider is not opened to the start's place passes `not-opened` and a later choice runs, an order of one holding on its sentence;
   a role holder's and a meeting's start, outside the plan, refuse it before anything starts. (from D53, D57, D213, D212)
7. Each `AgentRun` operation is admitted by the run's own evidence. One it lacks is refused as unsupported before anything is
   written. (from D213)
8. Every executor holds its run through `TransportRun`: the driven start, the continuations, the go-on (the held words on the
   run's `DoorInput`, then `ResumeAsync` and `EndAsync` again), the resume's one retry (a driven run's and a conversation's, the
   run's own under the same record, D243) and the conversation's runner. No executor prepares a process itself. A conversation reads its agent through `ChatAgent` (its door with the
   registry beside it) and the run it makes. (from D213)
   A run's words are two named ports, one per transport: a native door's lines on `AgentRun.Input`, a protocol door's requests on
   `AgentRun.Words`; each door's other port refuses or is absent, and the contract has no word operation of its own. (from D246)
9. A provider translates; Daoris selects. A provider never walks accounts, picks a model, starts another participant or
   decides whether a line goes on. (from D213, D67)
10. On the protocol door the wire enriches the console and transcript. A record still concludes from the exit code and the
    quest's state, because the wire's stop reason flattens failures. (from D46, D53)
11. A driven session takes no person's line on its stdin. The protocol door's stdin carries Daoris's frames. A driven native
    prompt goes on stdin under a bare `-p`, as BOM-less UTF-8, never as an argument. (from D213, D49)

**The spawn**

12. One shell completes every kind of session's process from its door's recipe, in one order: the role variables cleared,
    every other provider's registered credential and home dropped (a plugin's door, its program unmeasured, keeps all), the
    repository and service, the tools, the session's git, the identity variables, the recipe's own; then the account's key,
    the binary and its pin, the program resolved on the tools' `PATH`, Claude Code's seam on a door whose program runs it
    (Claude Code's protocol door, `acp-stub`, a plugin's protocol door; never Codex's or dsh's), the home last; then the
    tools-at-the-start switch (`ENABLE_TOOL_SEARCH=false` for Claude Code); the carriers after all of it. A provider declaring
    a name of Daoris's list is refused at registration, but for the session's tokens. (from D49, D230, D172 point 3)
13. A start's connectors are one `ConnectorPlan`, composed once per workspace by `ConnectorPlans.For` (the workspace, the servers, the
    sources it permits: Daoris's connector, the plugins' and the browser's for a session, Daoris's alone for a reviewer, helper,
    partner or brainstorm pass, and the servers it requires; a server from a source not permitted, a name given twice or a required
    server missing is refused naming it). The protocol door's open (`session/new`, `session/resume`) reads that plan's servers; the
    door's own carrier (`IServerHanding`) is asked the same plan once: Claude Code's native door writes its `.mcp.json` layout under the
    home (`--mcp-config`, the name `ClaudeServers.FileSuffix`), removed with the session; a door with no carrier at its spawn (dsh, the
    stand-ins) is written nothing. No start leaves the agent's own servers out, so the plan has no exclusion and no door states an ability for one. A native door drives an adopted repository by its own
    `.mcp.json` only where its servers inherit its environment (`ServersInherit`, Claude Code's native door and every undeclared native
    door), and `Adoption.Drives(abilities, adopted)` is the judge. Daoris's connector is always offered. A server declared through `npx` is
    handed as the whole path of node and its package's own script, so the harness starts one node: the package is installed
    once by npm `--prefix` into `cache/toolchain/<package>/<version>`, npm's cache first, and the log says so
    (`server.installed`). A tag or a range is read at that first install; later starts take the newest version installed. A
    server whose package cannot be started is not handed (`server.refused`). A server that drives Daoris's browser is
    started under `server-keeper.mjs`, which starts it afresh when its working set passes 512 MB, checked every 15
    seconds and only with no call in flight; the tabs are the browser's, so a chat loses only its snapshot references: a call that names a `ref` from before the
    restart is answered by the keeper itself with a fresh `browser_snapshot` and a sentence saying the reference is out of
    date, and is not sent on.
    Playwright MCP keeps every request a tab made since its last navigation (about 50 KB each) and has no option that
    bounds it. (`SessionServers`, `ServerPackages`, `ServerKeeper`)
14. Plugin skills are composed by Plugins as `SkillSource` values, laid out by the door's provider (`ISkillLayout`; Claude Code's
    copies them per session under the home's spawn folder; a provider that states none, Codex, dsh and the stand-ins, is handed none)
    and handed by the harness's session-only door
    (`--plugin-dir`, or `_meta` beside the rules). A harness that loads no tree skills (Codex) is told their folders. (from D170, D230)
15. Every Claude session's settings file turns auto-memory, synced skills, synced plugins and account connectors off for that
    run. No account home, vendor folder or the person's tool home is written. (from D230)
16. The standing block is composed by the driver for every kind of session, handed at a start and at a move, never on a
    resume. The whole first prompt fits the native door's single argument. (from D230)
17. Every session runs in a configuration home Daoris owns. With no Daoris account signed in, a start waits for a sign-in. It
    never runs in the person's own tool home, but a conversation begun there goes on there. (from D175, D197)

**Permissions and posture**

18. What an agent may do is Daoris's rules in three scopes (machine, workspace, repository) in `permissions.json`, beneath the
    defaults `connector`, `commit`, `no-push`, `no-publish` and `tree-guard`, each switched off by the person but `no-push`, which
    is fixed and opened only by the session's git access Write. The union is handed as the harness's command-line settings tier;
    the harness's own precedence decides, and `deny` wins. (from D72)
19. Only a harness whose provider states a rule language is handed rules (`IRuleLanguage`: Claude Code's `ClaudeRules`, its settings
    file with the tree guard's and the lineage hook's entries and the hard denials, for its two doors). Others
    are handed none, and nothing is translated until measured. Such a harness (Codex) holds none of the person's denies: its
    standing block and the access setup say so, and the git doors judge its push, force and delete. (from D72, D213, D233)
20. A session may propose a rule change with `permission_propose`. A narrowing applies at the next look; a widening waits for
    the person's yes. Every change records who made it. (from D74)
21. A driven session runs in its harness's own judged mode: `auto`, else `acceptEdits`, on Claude's protocol door; `acceptEdits`
    on its pipe; `agent` on Codex; `workspace-write` through dsh's environment. (from D81, D53)
22. Full access (`bypassPermissions`, `agent-full-access`) and the asking mode (`default`, `workspace-write`) are used only by the
    person's own setting: Ask Daoris's full access or a repository's access setup. A harness with no mapped mode refuses the
    start; it never runs a narrower mode under the label. (from D183, D184)
23. A permission request reaching the driver is refused, except in the asking mode on the protocol door, where it is held for the
    person to allow once or refuse. (from D81, D184)
24. Folder trust is written only on the person's act naming the folder, one flag in the harness's own file. With no account
    named it trusts the folder for every account of the set, never the person's own home. Trust gates a start only where
    the handed rules would not let the connector through. (from D73, D197)
25. A session reads the workspace's registered checkouts by default and writes another only where the person declared it.
    Reading is the read checkout's setting. (from D107)

**Accounts**

26. An account is made by signing in into a fresh `account-N` folder; removing it deletes the folder. An API key is an account
    too: kept machine-local, handed in the tool's own variable, shown only by its last four characters. (from D66, D67)
27. Each agent's accounts are one set serving every workspace, with one order and one set of use settings. There are no
    machine or workspace lists. The person's pick at a start is the one exception. (from D197)
28. The person's own sign-in is outside the set. Daoris never signs it in or out, never asks it anything and never starts work
    in it. (from D197, D175)
29. The walk: under `goal` (the default) a near account goes last, then fewest of Daoris's sessions running, then a week about to
    lapse, then most room under the curve, then least recently started, then the order. Under `order` the first ready
    account runs. The cap stays one number per machine. (from D130, D165, D197)
30. A cooling account is held first, by a file read, before any probe. A refused or signed-out account is passed until a
    person acts. With no account ready the start waits, saying the soonest way out. (from D125, D130)
31. A running session moves at its turn's end to another account of the same agent with room (10 points under its curve on
    every window), never mid-turn, and not again within 30 minutes unless refused. Where none has room, the `handOver` rule
    decides another agent. (from D165, D163)
32. A resume asks for its record's own account, because a harness keeps a conversation in that account's home. A start on
    another agent is on another account, whatever the names. (from D131, D137)
33. One status question per configuration home runs at a time. No real sign-in runs under a scratch home. (from D125, D150)

**Usage, limits and errors**

34. Usage is machine-local: it names accounts, lives under the home and has no HTTP route. Each account's real use is read at
    no model spend (Claude's `get_usage`, Codex's app server), with its source and age. (from D57, D165)
35. Capacity is estimated from Daoris's own turns and the readings, with weights that are dated ratios, never prices.
    Acting on the estimate waits. (from D179)
36. A door's failure is read by one reader into four kinds, in order: limit, access, transient, fatal. It reads the door's
    own tables first, then its owner's. The agent's own message is never read. Anything unread is fatal. An access failure
    carries how it was read (a refused credential, a sign-in's words, or a code the door declares), and a refused resume's
    open and the conclusion's sign-out read that, never the name of the entry. (from D210, D125)
37. A limit cools that account until the reset the agent named, plus 2 minutes, with 15 minutes' grace and nothing believed
    past 8 days, else 60 minutes. The grace is a dated reset's alone, never a time of day's or a weekday's. One function
    holds these bounds (`ResetBounds`), whoever read the reset. A limit is never a strike. (from D125)
38. A limited line resumes by itself at the look: its own conversation on its own account when ready, else a briefed resume on
    another account or choice, else it waits showing the earliest reset. (from D206)
39. A transient failure is tried again in the same conversation after 1, 2, 4, 8 and 16 minutes, up to the line's tries
    rule. It is never a strike, park, cool-off or walk. Past the bound the quest is held until the person's word or
    *Try again*. Every run on the protocol door takes that one retry (`TransientRetries.For` at the hold, `ChatRetry` for a
    conversation), each with its owner's check: a driven session, an intake, a helper, an opinion, a partner and a
    brainstorm voice. A pass with minutes of its own waits none past them. Past the bound, or with its minutes ended inside
    the episode, it fails with its kind's code (`helper.transient`, refused `transient`; `opinion.transient`, which the
    host reads as the pass's `transient` and the gate as `Unavailable` with that code and no reset, so only the person's
    press asks again; `intake.transient`, its ask unbound once and kept bound to the second such record
    (`IntakeTransients`, in the service's session advance); `partner.transient`, its work kept; a voice dropped), never
    *timed out* (`TransientEnded`). Each retry's check is its kind's own authority (`TransientOwners`): a helper's answer
    stored, an intake's ask closed, deleted or gained quests, an opinion given, a partner's piece ending; a voice's is the
    person's stop alone. (from D210, D229)
40. Access goes to the person: the account is marked refused or signed out, with no retry. A fatal error fails with its
    reason and is a strike. (from D210)

**Toolchain and models**

41. The binary is the explicit command, else the managed pin (workspace, then machine), else `PATH`. A pin with nothing
    installed refuses; it never falls back. Daoris never installs an agent unasked. (from D57, D121)
42. Agents install from their makers' channels into the home's toolchain folder. An ACP adapter is its own package with its own
    pin; the managed `claude` it runs reads `claude-code`'s pin. On Windows an agent npm installed starts as the node and script
    its `.cmd` shim would run, never through the shim's `cmd` (`PackageStarts`). (from D57, D121)
43. A start's model and effort come from the role's choices, in the tool's own words. The protocol door carries them by
    `session/set_config_option` after the posture and before the first prompt; the pipe door by `--model` and `--effort`. An
    absent value is the account default. (from D205)
44. A model the agent does not offer stops the process before any prompt, with no strike, and the walk goes on. A running
    session never moves to another choice. The record keeps what was asked, acknowledged and ran. (from D205)
45. An account's own model and effort live in the tool's own settings file under that account, for Claude Code only. Daoris
    offers nothing for a tool whose settings it has not read. `max` is refused as an account default. (from D98)
46. A conversation's model and effort options are the ones the agent offered on its wire. The session's mode is never offered
    as an option. (from D98)
47. Lyntai's agent layer (`Lyntai.Core` and `Lyntai.Providers.Basic`, never the metapackage) is taken by the driver alone, from
    Lyntai's local build: the desktop's package props import the service's ignored `local/lyntai-local.props`, and the driver
    references the layer only under `DaorisLyntaiLayer`, true when that build is in use. Without it the driver has no Lyntai
    assembly. In the driver the layer is named by `Agents/Layer/` and the providers' folders alone, a vendor's registration and
    options by its folder alone, with no `global using` of it and no layer-own id (`LayerCensusTests`). `Agents/Layer/`, each
    provider folder's `Layer/` and their tests compile only under `DaorisLyntaiLayer`. There Codex's usage question is read through
    the layer (`LayerUsageRead`, bound when the driver loads) and Claude Code's by today's reader, since the layer's reader reads the
    installed server's answer as unknown; without it today's readers read both. A layer door's failure enters as a `NormalizedFailure`, built in
    `Agents/Layer/` alone and answered by `AgentErrors.OfNormalized` with no table read. (from D213, D248)

## Where a change goes

**By subdomain.** Agents sits after Knowledge and before Workplace, Roles, Participants, Context, Course and Loop
(`Driver.Tests/StructureTests.cs:448`). Agents may use what is below it. What sits above reaches it through ports installed
when the driver loads (`IAgentContext`, `IAgentWorkplace`, `IAgentWorkWords`, `IAgentPlugins`).

- A new agent or door: a registration line in `Registry/Providers.cs` and a folder under `Agents/Providers/<id>/`, on the
  contract. A door is a record of its provider, never a class of its own; a test's harness is a `TestDoors.Door`.
- Code that names Lyntai's agent layer: `Agents/Layer/`, compiled only where the local build is in use, or a vendor's registration
  in its provider folder's `Layer/`, compiled under the same condition (rule 47).
- A new fact about an existing real agent (a flag, a posture, a word table entry): its provider's or door's part in its
  provider's folder, measured first, with a recorded sentence for any word table.
- New protocol-door behaviour on a frame: a stage of its own beside `Acp.cs`, marked `[TurnStage]` with its phase and what it
  follows (as `AcpMeasure`, `AcpRender`, `AcpPermission` are); what it reaches of the session goes on `ITurnWiring`, and what it
  shares with another stage on a state of its own (`RefusedCalls`, `StepCalls`); `AcpPartsTests` holds the order. Other door
  behaviour is a unit of its own beside it (as `AcpBackground`, `AcpWake`, `AcpStreams`, `AcpWaits`, `AcpEpisodes`, `AcpPrompting`,
  `AcpRun` are), reaching the session through a wiring interface. What touches the open prompts goes through `OpenPrompts`, whose
  gate is the only lock of that state: a new member that reads or changes it is added there, under the gate, calling nothing out
  while it is held. `Acp.cs` is a root that wires: never more code in it.
- How a run starts, waits or goes on: `TransportRun` and `TransportStart`. What a run's door says goes out as a `WireReading` on its
  sink, never as a callback or a store the run names: a new fact is a reading in `Events/Readings/`, kept by its owner's part where the
  executor composes the sink (`SessionHold`, `ChatWatch`).
- Which account or binary a start takes: `HarnessRoster` for the walk's facts. The pure ranking goes in `AccountRotation` or
  `Domain/`. Settings shape goes in `HarnessSettings` or `AccountSet`.
- Which choice or model a start takes: Roles (`Roles/`), not Agents.
- A failure's kind: `AgentErrors` and the door's `ErrorWords`. What happens after the kind (park, hold, resume): Loop and Course.
- What a session is handed at spawn: `Driver.Prepare`, `Equip` and `HandRules` in Loop; a door's arguments, own variables and
  carriers on its spawn in its provider's folder; Daoris's own variables in `Shell`, never in a door's recipe.
- A branch on what a door can do: ask its ability (`door.Can`, `DoorAbilities`), never its wire; whether it may drive an
  unadopted repository is `Adoption`'s. A door's transport is the contract's `Transport` (`Native` or `Protocol`), read where a
  transport is the thing asked: the transports, the providers and the shim.

**Tests that pin the area** (`src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Agents/`):
- contract: `ProviderCensusTests`, `ProviderVocabularyTests`, `ProviderConformanceTests` (and its process and live cases), `PendingRowsTests`,
  `ParityOracleTests`, `ProviderRegistryTests`, `ProvidersBuiltTests`, `StandInShimProcessTests`;
- doors: `AcpTests`, `AcpPartsTests`, `AcpSteerTests`, `AcpTransientTests`, `AcpTypedFailureTests`, `AcpResumeTests`,
  `HarnessRunTests`, `ClaudeStreamJsonTests`, `NativePromptProcessTests`, `AdapterTests`, `Acp3AdapterTests`, `WireReadingsTests` (the
  readings and the sink, each door's event file as before, a subagent's card `think` on both doors);
- spawn: `SpawnTableTests`, `SpawnInheritedTests`, `ShellStructureTests`, `ToolsAtTheStartTests` and its census, `OwnContextTests`, `OwnSpaceTests`, `SpawnSkillsTests`, `PermissionSpawnTests`,
  `StandingHandedTests`, `TargetPromptGoldenTests`;
- accounts: `AccountRotation*Tests`, `AccountSetTests`, `AccountNextStartTests`, `SwitchDecisionTests`, `UsageCurvesTests`,
  `AccountCoolingTests`, `AccountLimitsTests`, `ResumeAccountTests`, `LineAccountTests` and its callers' census;
- errors and usage: `AgentErrorsTests`, `ClaudeUsageTests`, `CodexUsageTests`, `UsageHistoryTests`, `UsageProjectionTests`;
- rules: `PermissionRulesTests`, `RuleProposalsTests`, `WorkspaceTrustTests`, `AcrossTests`;
- the twins with the CLI: `RotationTwinTests`, `RotationUseTwinTests`, `CoolingTwinTests`, `WindowsTwinTests`,
  `UsageCurvesTwinTests`, `AccountNamesTwinTests`.

**Gates that bite** (`daoris.gates.json`): `driver` (every test above but process ones), `censuses` (the provider census, the
tools-at-the-start census, the line-account callers' census), `driver-process` (real processes: conformance, native prompt,
usage questions, session processes), the structure gate inside `driver`, `cli` for the twins' other half, `web-unit` for the
Agents page, and `orient-index` and `code-map` once files move.

## Contradictions

1. D23 (one harness supported; a second written only when a repository adopts one) vs `Providers.cs` (four real doors,
   stand-ins and plugin harnesses). D53, D57 and D213 read right; only D23's "unknown name is an error" lives.
2. D24 and D98 (Daoris names no model and chooses none) vs D205 and `Roles/ActorSuggestion.cs:23`, `Roles/RoleSuggestion.cs:23`
   (the suggestions name `sonnet` and `opus`; starts carry the role's model). D205 reads right.
3. D205 point 7 (the suggestion leads Build with `opus` · high) vs its note of 2026-10-25 and `Roles/ActorSuggestion.cs:23`
   (`sonnet` · high, then `opus` · high). The note and the code read right.
4. D81 (a driven session is never `bypassPermissions`) vs D184 and `ClaudeCodeSpawn.cs:137` (bypass by a repository's unrestricted
   setup). D184 reads right; D81 holds only for the auto setup.
5. D184 point 5 (the person's deny rules hold in every setup) vs `CodexAcpAdapter.cs:46-47` (Codex takes no rules file, so no deny
   reaches it in any mode). The code reads right, and is said (D233 choice 3).
6. D125 point 4 (per-workspace rotation) and D130 points 3-4 (machine and workspace lists) vs D197 and `RosterWalk.cs:34`
   (one set per agent, whatever the workspace). D197 reads right.
7. D125 point 5 (the tool's own home as an account a start may use) vs D175, D197 and `RosterWalk.cs:60` (a start with
   no Daoris account waits). D175 and D197 read right.
8. D73 (trust with no account named writes the machine default's, else the tool's own home) vs D197 point 3 (every account of
   the set, never the own home). D197 reads right.
9. D57 (toolchain, accounts and usage under `~/.daoris/`) vs `HarnessAccountFolders.ManagedHome` and `ProfileHome` (entries of Daoris's home). The
   code and D63/D172 read right.
10. D213 point 2 (each fact in one place; no reader asks "own, else owner") vs `AgentErrors.cs:143-185` (own table, then
    owner's), as D210 point 2 prescribes. The code and D210 read right today; D213 is the target.
11. D213's note of 2026-11-04 (row m-i complete) vs `TASKS.md:29` (m-i still open). The note reads right; the row belongs in
    the archive.
12. `HarnessAccountFolders.ManagedBinary` (a pin with nothing installed falls back to `PATH` and says so) vs `RosterWalk.cs:91`
    (refused). The refusal reads right (D57, D121).
13. `Registry/ProviderRegistry.cs:12-13` (nothing reads the registry but its tests) vs `RosterReport.cs:376` and
    `AgentsCommand.cs:282`. The callers read right.
14. `ClaudeCodeSpawn.cs:89` (the repository's `.mcp.json` wires the connector; the driver may not write it) vs
    `Loop/Driver.cs:3323` and `:5209` (Daoris offers its connector at every spawn). The spawn reads right.
15. `CodexAcpAdapter.cs:10-15` (codex-acp offers three modes, "no third reading") vs `CodexAcpAdapter.cs:57` (`workspace-write` read in
    codex-acp 2.1.1). The property reads right.

## Open

- **AGENTPROVIDER1d**'s rest: s-ix on, whose three cells are named in the design (`p-claude-bare`, `p-dsh` and `p-b`, where the
  page's own-home question reads the owner's first door and the walk the door's own), waits on the owner's choice: the page's agent
  card is one per account key, so which door's home it speaks for where an owner's doors disagree is not yet chosen
  (`RosterNext.OwnersToolchain` reads the owner's first door as before). `OwnerOf` of the old set is retired, every read asks
  `ProviderRegistry.OwnerOfAgent`, and every read of an agent's program and accounts `ProviderRegistry.AgentNamed`, which answers an
  `AccountAgent` (a holder's included). The roster's `LimitsOf`, `SignInOf`, `WindowsOf` and `UsageOf` read through the registry.
  **1e**'s rest: a door's own limit, window, sign-in and refused words are its `DoorTablesPart` (`Own`), beside its provider's
  `TablesPart` and its `Lent` words (a plugin's door states its toolchain's once in both; `codex-acp` says Codex's limits, as it
  lends them); a candidate's read floor is its provider's (s-iii, `SwitchCandidate.Floor`), and the page's is the accounts answer's
  `readFloor` per agent, its question's own, a minute only for a shell that names none. `alt-stub` (`Providers/AltStub/`) is built: its own
  limit, window and sign-in grammar and home variable, one line in `Providers.Built()`, its pending file beside its program. Its native door is Daoris-run on today's plain capture as `stub`'s
  is and passes the stream cases `stub`'s does; its protocol door's frames cases wait on row `j` (D248's note), its others on `l` and `n`. Neither
  door has an own-vocabulary stream reader yet (the native door's waits on LQ13's turn at AGENTLYNTAI1f-ii, the protocol door's on AG15 at j).
- **AGENTPROVIDER1** host rows (h, l, m-ii, n, o, p, q, r, s, u): every provider's and door's program is Install's `ToolSpec`
  (`Install/ToolSpec.cs`), the pin's install is one decision,
  `PinInstall.Plan`, which the window's actions and the terminal's `agent pin` and `agent update` both ask, each running what it names
  its own way (the terminal at its console and under the home's lease, the window redirected: s-ix f4); an unknown door id is refused
  (`UnknownDoorException`), never the pipe, by `Adoption`, `BuildDoor` and the window's, set-up's and the terminal's reads, while the
  loop's plan reads it as a door that may not drive and the start holds its quest on the refusal, so a tick still reports; the commit
  trailer reads the registration's maker (`ProviderRegistry.MakerOf`); a door's spawn and the shell read the start's `SpawnRequest`
  alone, which Loop's executors compose (`SpawnRequests`) and hand the registry's `Start`, `Resume` and `Chat` for the door, a
  go-on's resume asking the start's own request again naming its conversation (`TransportStart.Request`), and every agent's completed
  recipe is started and contained by Platform's launcher (`AgentStarts.StartAsync`, D201) through the run; h's rest:
  `containment.not-widened`, whose proof that a door's process is born in the agents' job needs the installed launcher, and the
  stand-ins preparing through the door rather than the seam (row t); the identity names are down (`AgentWorkVariables`, `ChildMarks`);
  `HolderTerminal`'s program and home variable read from the registration, its binary's hash and path D209's; the installed proof that
  a Codex and a dsh session start as before without Claude Code's seam (s-v) and with no other provider's credential (s-vi);
  `ChildBindings`' fold names no agent: each line is learnt, and each call read as the child it came from (`ChildCall`), by the
  reader its agent's `IChildVocabulary` gives (`ClaudeChildLines`, joining a call by the hook's tool use; `CodexChildLines`, a thread
  with no parent the root), and whether a later line moves a reading is the vocabulary's `Wake`; each native line is read once
  (`NativeFrame`), and `NativeChildren`, `HarnessWords`, `ClaudeStreams` and the mapper take that frame; the lineage is Participants'
  part of the run's sink (`LineageReadings`, which `SessionHold` and `ChatWatch` compose), written from each door's `ChildSeen` and
  `ChildEnded` readings alone, each said once and stamped with its process's lineage token, the labels bounded as it writes (l's rest:
  `ClaudeStreams` and `HarnessWords` still read a child's or a task's start and end off the frame, not from those readings); the open's
  refused model is one
  `WireReading.ModelRefused`, said by
  the transport's `RefusalReading` before the re-ask and kept by the offers store's part, `OfferRefusals`), m-ii is built (D243 for
  the native door: the words go through the run's `DoorInput`, the chat's retry is the run's own and replays what the ended
  process never acknowledged, Claude Code and `alt-stub` declare their `IConversationHome`; D246 for both: the words are two named
  ports, `AgentRun.Input` and `AgentRun.Words` (`ProtocolWords`), which `ProtocolChat` says and the session is never written through
  by; the protocol open or resume is `ProtocolOpen.BeginAsync`, read by the driven run and the conversation alike; the hand-over's
  accounts are `HandOverAccounts.Of`, read by the chat runner, the curve and the loop; `TransportRun.Session` is kept for the options
  its agent lists), n is built (the doors' `Offers` part and the offers
  read through `AgentOffers.Offers` are built; the asked choice is read from the start's whole `SpawnRequest`
  (`ActorAsked.ForSpawn`: the request's asked choice, else the record's, else the opening's default; a flag of the machine's command
  the command's own; the program's own variables, `ISpawn.ChoiceVariables`, from the environment it starts with), never from the
  arguments its door's recipe carries it by, so asked, carried, acknowledged (`ActorEntry`) and reported (`ActorReports`) are each
  read apart; a plugin's adapter keeps Claude Code's names until its spawn declares its own); a start's words go on in its
  record's own conversation by one rule, `OwnResume`, which the continuation's walk and the sessions' groups both read (s-ii); o and p are built (`PostureAsked`, the trust preflight through the door's `Home`, an unmapped full or asking start refused
  naming it, the questions pause's value read from its provider; one `ConnectorPlan` per workspace read by both doors, the session
  home's servers file named by its provider, the rules file too, `ClaudeRules.FileSuffix`); p and q are built but for what t removes: `ServerEntry` is Knowledge's one
  name, the five places of `Adoption` carry a bool, and a plugin's door is a contract door made from a `DeclaredDoor`, its provider
  made from declarations by the registry; r is built (the sheet, `IAgentFacts`, the roster's `agents` half, the service's call
  keys; the page reads an agent's account acts and its lead door from the sheet, no `doors[0]` left; each door's sheet is drawn under
  *Ways in* in both languages; `agent list` says an installed agent's sheet in one line; the room names a tool by `IAgentFacts`; a
  manifest's harness may name `fullPosture` and `askingPosture`; the family rehearsal declares the Stub Two manifest) but for the
  Stub Two phase's partner exchange (**COAGENT1e**), which the rehearsal cannot run as designed: a production start is admitted by a
  measured row of a real agent only (`PartnerMeasurements.Admitting`, no stand-in's row), and a declared stub has none, so the exchange
  reaches no piece and its design (§8.4) waits on a decision of what admits the stub; s-ix o1 to o3 are built as the
  steward chose: the page's next start speaks for the account's own door and says one line (`NextStart.Doors`) for each other door
  whose own-home answer differs, so `provider-owner.json`'s `disagrees` cells stay the walk's own question and the page no longer
  mistakes them. s-vii is built: the head's leading agent (`leadingAgentOf`, by the roster's sheet) and the usage answer's account
  shares (`SessionUsage.ByAccount`, the wire's `agent`) count by agent, and PICKCARRY1 carries a pick to a new part on the picked
  account where the two doors are one agent (`AgentOfDoor.Same`), never between two providers sharing a pool. u is built: the plan's
  walk reads `Reach` (`ProviderReach`) with the pass code `not-opened`, a brainstorm voice by its workspace, Ask Daoris by the
  machine's room, and a meeting's participant and a role holder's harness refused; a chat's walk is handed its workspace, not its
  repository, so it reads the workspace's opening until its place carries the repository. Left: the rest of D213's design.
- **AGENTPROVIDER1t**: the old seam ends. No provider reads an adapter class; `ClaudeAcpAdapter`, `CodexAcpAdapter`, `DshAdapter`,
  the two dialect bridges, `ClaudeCodeAdapter`, `StubAdapter` and `AcpStubAdapter` are gone, `SessionWire` is the contract's
  `Transport`, and the shell applies the seam a door's own spawn states (`ISpawn.PointAtClaude`), naming no provider; the census's
  exceptions under this row are none. The adapter seam and the toolchain are gone from the product: the registry is made of
  providers alone, a reader holding a door asks the door's record and, for what its provider adds, the registry beside it
  (`DoorPorts`), and an agent's program and accounts are the `AccountAgent`'s, a value the registry answers per read; a plugin's door
  joins as a door the registry places (`JoinedProviders`), never a provider of the plugin's making. The desktop tests are on providers
  too: a test's door is `TestDoors.Door` over a `TestAgent`, its registry `TestDoors.Registry`, a shipped door's `TestDoors.Shipped`,
  a holder `TestDoors.Holder`, and the census (`OldSeamNamesCensusTests`) pins none of the old names in any source, tests included.
  The provider censuses' ids and words are the registrations' own: Claude Code, Codex and dsh declare their `Vocabulary` part,
  `ProviderVocabulary` reads every provider's id, its program's name, its doors' ids and those words, the driver's census reads it and
  the service's, page's and tools' read it as the contracts' generator writes it (`PROVIDER_VOCABULARY` in the page's service types),
  every hyphened id counted in a sentence; a vocabulary's variable is its program's, kept in its spawn and stripped from every other
  provider's. Left: every record still reads.
- The agent-layer adoption (D213's notes of 2026-10-29 and 2026-10-30, D248): Daoris's providers consumed from the sibling
  agent-provider library, built against its local build. The reference and its census are in (rule 47). The host's policy for a
  layer door stands on Daoris's own types, read by Claude Code's driven door where the driver carries the layer: the access evidence each account reader reads (`AccessEvidence`), the
  trusted limit and sign-out entries that read no sentence (`HarnessRoster.LimitedTrusted`, `RefusedAccounts.SignOut`), which
  readers each door's site calls (`AccountReaders`), a refused model by its whole outcome and then by door and phase
  (`ModelRefusals`), the retry table by door, phase and run (`RetryTable`) and what was hit (`LimitHit`). `Agents/Layer/` holds the
  failure ingress (`LayerFailures`, a prompt's delivery read by `LayerDelivery` from what its own turn showed after the write, the
  layer's stamp standing only for a terminal that states no turn), the translations of claims (`LayerClaims`), accounts
  (`LayerAccounts`) and events (`LayerReadings`, each event into today's readings in today's order, a stop Daoris asked read as a
  stop; the conversation named by the first start the agent announced; a refused model told by `LayerRefusals` against the door's
  model-refusal entries, the ending's flag on a native turn that ran nothing; the turn's own report, `LayerTurnReport`: denials, the
  context against the window the harness names and the tokens by model, said at the terminal as the native capture says them, a protocol
  door's context figures as sent), the two containers
  (`LayerContainer`: the registry's, which never spawns and refuses a door registered twice; one per start or read over its own
  runner), `LauncherRunner` (every layer start through `AgentStarts`, no clock on a turn, a read bounded by its patience, each raw line
  heard, the lines outside a frame and stderr kept as the harness's own, stdin recorded, a stop the kill of the tree), `LayerRun`
  (`AgentRun` over one turn per call, a resume a start of its own, an operation the layer's members do not carry refused before a
  write) and the registry's layer view (`LayerView`: bound doors by the registry's spelling and by run kind, their layer doors and
  claims). The conformance suite's fourth view runs the registry through that view on its run kind (`LayerConformanceTests`). All of it
  is proven on the tests' own provider (`LayerTestProvider`). Claude Code's driven kind is bound (`ClaudeDrivenLayer`): Lyntai's Claude
  door over the program the start resolved (its file and the arguments before `-p`, the launcher's seat starting it in the door's
  command's place) and the environment its shell composed, its two refused-model entries, a tool call read by Claude Code's own
  vocabulary (a door's `Steps`), and the start's mode, model, effort and plugin folders with its rules and servers files (a door's
  `Options`) carried as today's spawn carries them. Where the driver carries the layer, `SessionHold` holds a native door's driven start
  on the run its door's binding answers (`DrivenRuns`, bound when the driver loads), today's `TransportRun` otherwise; both are a
  `HeldRun`, the run and the hold's own steps around it, and a partner's piece stays on today's. A held `LayerRun` is one turn per
  process, the first's, the native resume's one retry's and each go-on's, and takes today's steps through the same host
  (`LayerHold`): the downloads kept, each process's lineage token issued and set on its environment, the process tracked and reaped,
  each raw line stamped for the watch, the first process's wait under its start's minutes and the watch's quiet and a later one's under
  the machine's, its turn drained and its lineage ended. Its prompt is the lines its native input hands the next process, written as
  the layer's stdin write returns and acknowledged once the agent names its conversation; a go-on carries the held words, a retry the
  words the ended process never acknowledged. Its readings say nothing of an end Daoris made (a bound, the quiet, a registered stop) or
  of a clean exit before the agent stated its end, as today's capture says nothing of a stream without its result, and its ending is
  the harness's own words with what its stream said of its background work (`HarnessWords`, each raw frame handed by the seat). A
  move's parity is the oracle's
  (`Tests/Agents/Parity/`): what each path observed, by a fixed list of channels, compared exactly and in order but for the
  projection's fields (a provenance by the door's relation of permitted pairs and the pair its scenario expects, a generated id by
  first appearance, a reading's lineage token among them, a time by its interval from the run's start, a named library clock within its
  interval), a held case holding alike only on the same missing reader. The layer path runs held as a driven start holds it and handed
  the Daoris path's session (`ParityLayerPath`), so each reading's stamp is compared with it. On the tests' provider today's Claude reader on `TransportRun` and `LayerRun` read a
  scenario alike (a refused model, a denial, the context and tokens by model, the unannounced start and a meter-only failure among them)
  but for a tool step's kind, which the Claude reader derives from the tool's name, and the context a count written to the cache
  adds, which the layer's tick does not carry (`LayerParityProcessTests`). A difference the oracle knows is a named gap, set apart
  before the rest is compared and failing once it sets nothing apart (`ParityGap`). Over every recorded Claude Code stream, today's
  door and the driven binding read alike (`ClaudeDrivenParityProcessTests`), the init's reading of what the harness loaded handed
  before the conversation its frame names, but for a message's id on its words, which the layer's words do not carry. Their spawns hand
  Claude Code the same arguments on each driven mode, `default` among them, but for the three flow tools the layer's door always denies
  (`ClaudeDrivenSpawnParityProcessTests`). A
  pending line may name the run kind it holds its case on (`run`, read by the suite and `tools/stand-ins.mjs` alike), and each
  waits on a row a case can still wait for, the agent-lyntai design's own among them (`PendingRowsTests`). Codex's account
  windows are read through the layer where the driver carries it (`LayerUsageRead` over each provider folder's `Layer/` vendor,
  Lyntai's AG21 to AG24): today's command, environment, working folder, client name, patience and exchange order, any version read
  without a probe, the reading named as today's readers name it (a Claude limit row scoped by an object keeps no scope, as today's)
  and the history's source `layer:<question>`. Claude Code's has its layer vendor and stays on today's reader: the installed server
  names a scoped row's model with a null `id`, which the layer's reader reads as an unknown answer. The oracle over every recorded
  answer and both readers' fixtures (`LayerUsageParityTests`) finds the two alike but for named gaps: Claude's server starts with
  `-p` on the layer (measured to change no answer), and a value the layer's reader cannot recognise, that null `id` among them,
  reads its whole answer unknown where today's keeps the rest for the history. The other moves wait on the capabilities requested
  in `docs/2026-11-06-lyntai-requests.md`.
- **AGENTINFRA1**: an execution-neutral run for remote APIs and hosted runtimes, with scoped availability and explicit
  recovery. Accepted; built after AGENTPROVIDER1, isolated home first.
- **AGENTERRORS1c**: the native door's failure reads and the measured `--resume`; **1e**: what remains of point 8 (the
  native door's live, driven and one-turn tries). 1d is built.
- **LIMITRESUME1f** and **TOOL4n**: the native door's limit read, so a native session ended by a limit cools its account.
  Waits on a recorded native limit, the owner's yes, which is all that remains of D210's row 1c. A failed native `result` is one
  `WireReading.Failed` (`pipe`, `DoorWhere.Result`, the result's own words) on the run's sink, and `NativeLimits`, a sink part composed in
  `SessionHold` and `ChatWatch`, reads it through `AgentErrors.Read` and holds a limit as the limit its turn ended on. A run concludes on it
  as on the protocol door's refused turn: a driven run's conclusion reads it as the turn's failure (`NativeLimits.TurnFailed`), so
  `AccountEndings.Limited` cools the account once, notes it and the record says `limit`, then parks or waits by its line's rules; a native
  conversation's cooling line is recorded before its turn's end, which `LimitHeld` reads limited (a failed result's subtype, never
  `end_turn` or `cancelled`), so `LimitMove` moves it to the next account and its end says `limited`.
- Row s-ii of the agent-layer adoption: a driven Claude Code start denies or keeps the three tools a headless turn cannot
  answer. The measurement needs the owner's yes; until it is made, the layer's door denying them is a gap of row d's spawn.
- Row d of the agent-layer adoption: its installed proof, and two gaps the sibling library answers: a message's id on its words, and a
  child's start and end and the streams beside, which the layer's members do not carry yet (AG18 with LQ7), so a layer run's lineage
  file holds the process's token but no child and a watched console no stream of a background task.
- **CODEXTOOLS1**: Codex's first tool catalogue waits for the connector to be ready.
- **STEER3**, **MSG1h**, **MSG1i**: steering and next-step words measured on other doors (Codex, native).
- **BGWAIT2b**, **SESSCTX1a**, **HELPMODE1a3**, **FABRIC-C2b**: woken-turn words, resume and fresh part per door, full access
  on the installed pins, the two-harness canary.
- **POOL1d**: the install's fold of `harnesses.json` read rule by rule, applied on the person's press.
- **MODELROLE1c4**, **MODELROLE1d3**: the installed proofs of a refused model passing an account and a chat's chosen model.
