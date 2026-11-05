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
| `DoorAbilities`, `Adoption` | `Contract/DoorAbilities.cs`, `Domain/Adoption.cs` | What a door can do as its callers read it: interactive, resumes and takes settings as the door declares them, and the rest its transport's (servers and rules on the wire, set at the open, words at the next step, held on the wire, resumed by a restart), so no caller tests a door's wire. `Adoption` judges driving an unadopted repository over door ids: a door whose servers ride its wire may; `Drives` asks it of Build's candidates for the machine's door, an unknown id the pipe until s-i |
| `AgentRun`, `Transport` | `Contract/AgentRun.cs`, `Contract/Transport.cs` | What an executor holds of a live run; the two transports, `Native` and `Protocol` |
| `ProviderRegistry`, `ProviderRegistration`, `Providers` | `Registry/` | The registration list (Claude Code, Codex, dsh and the stand-ins), the registry made over the built set and the plugins' adapters, id resolution by kind. `AdapterSet.OwnerOf` (the registry's `OwnerOfAgent`) is the one reader of whose accounts a door runs as and `AdapterSet.ToolchainOf` of a door's or a holder's toolchain; the roster, the terminal's verbs, the window's routes and the hand-over read nothing else. Codex's limit words and usage question are provider `codex`'s: its built door reads them through it, a plugin's door naming `codex-acp` by its `Lent` part, one joined to `codex` neither |
| `LegacyDoor`, `AdapterNativeDialect`, `AdapterProtocolDialect`, `AdapterAbilities`, `AdapterClaudeSeam`, `RegisteredVariables` | `Registry/` | Presents a contract door to today's readers as an adapter and a toolchain, its spawn the door's recipe completed by the shell; a real door's stream reader and session `_meta` as its adapter class makes them, until rows i and k move them into the provider's folder; `adapter.Abilities()`, a contract door's from its record and a plugin's or a test's adapter from its declarations and wire; whether a start takes Claude Code's seam (a contract door's spawn says, any other protocol adapter does); what every registration declared of a spawn's environment, and the refusal of a declared name of Daoris's list |
| `ProcessRecipe`, `ISpawn`, `Shell`, `ISessionGit` | `Contract/ProcessRecipe.cs`, `Contract/Ports.cs`, `Transports/` | A door's spawn answers a recipe (program, arguments, input, its own variables); the transports' one shell completes it into every kind of session's process and holds the account half every probe and usage question takes too; the session's git a port the shell declares and Workplace installs, unset throwing |
| `ClaudeCodeProvider`, `CodexProvider`, `DshProvider`, `StubProvider` | `Providers/<Name>/` | Each provider written on the contract, its facts declared in its folder: Claude Code's two doors, `codex` with its door `codex-acp` and its account facts (`CodexAccounts`), dsh, and the stand-ins `stub` (pipe) and `acp-stub` (protocol), which run a configured command and no model; each door's spawn its own there (`ClaudeCodeSpawn`, `ClaudeAcpSpawn` with `ClaudeAcp`'s seam, `CodexAcpSpawn`, `DshSpawn` with `DshProfile`, `StandInSpawn`) |
| `ISessionAdapter`, `SessionWire`, `AdapterSet` | same names | The adapter seam with its silent defaults; the built set, every door of it through `LegacyDoor` from the registration list; `Resolve` refuses an unknown name |
| `ClaudeCodeAdapter`, `ClaudeAcpAdapter`, `CodexAcpAdapter`, `DshAdapter` | same names | The four real doors' postures as their classes declared them, their spawn members one-line doors to their door's spawn; their toolchains are the old side a test holds equal to the registrations until row t |
| `StubAdapter`, `AcpStubAdapter`, `Spawning`, `CommitTrailers`, `PinNewest` | `StubAdapters.cs`, same names | The stand-in adapters; Daoris's own variables, a list (the identity variables and what the session's git answers it sets); the maker's trailer a commit carries; the newest release a pin moves to, asked one way for the window and the terminal |
| `SessionServers`, `ServerPackages`, `PackageStarts`, `NpxCommand`, `PackageBin` | same names; the last two in `Domain/` | The servers a session is handed, as the harness starts them; a server's package installed once into the toolchain; a package's program started as node and its script; `npx` read as npm reads it; the script a package's manifest names |
| `SessionTarget`, `ChatTarget`, `EarlierMaker` | same names | What a spawn carries |
| `TargetPrompt` | same name (hub, 11 KB) | The composed first prompt: its doors, the head, the beneath and the tail; the pieces come from the units below |
| `ClaimedPrompt`, `ResumedPrompt`, `AskedPrompt`, `RequirementsPrompt`, `WorkplacePrompt` | same names | The instruction's pieces for a claim and its retry, for a resume or a carry-on, for the ask, for the requirements and their evidence, and for the working tree |
| `TransportRun`, `TransportStart` | `TransportRun.cs`, `TransportStart.cs` | The one run over both transports: spawn, lineage token, capture, native prompt, wait and go-on; the start names its session, transcript, opening and preamble, and everything the door says leaves as readings to the sink its executor hands it |
| `IResumeState` | `Contract/` | A native resume's state, which Course's `ResumeAsk` implements: the run marks it opened when its harness names the conversation, and its end tries a structured resume once more where the harness exited nonzero before naming it, never after a clean exit, a refusal, a stop asked or on the plain capture |
| `WireReading`, `IReadingSink`, `ReadingSinks`, `DoorFailure` | `Events/Readings/` | The closed set of readings a run writes (an event, a console line, a stream beside, the conversation named, the account's windows, a failure), the one port it writes them to and its composition; each carries its run, the generation of the process that said it and its place in the run's order (`StampedSink`), the protocol door's windows and a conversation's failed turns among them; `Failed` carries the `DoorFailure` every failure is on either door, which `DoorFailures` in Agents builds from each wire. An executor composes its run's sink of its owners' parts: Context's `RecordReadings` (the event file and the console), the binding's conversation, the windows |
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
| `HarnessRoster` | `HarnessRoster.cs` (hub, 26 KB) | One judgement for every start, kept as one-line doors to the units below, with the roster's state and settings reads |
| `RosterCooling`, `RosterReport`, `RosterSelection` | same names | Cool-offs, refusals and signed-out accounts; the Agents page's report, a press and an account's status question; choosing the harness and account of a start, a resume and a carry-on |
| `RosterWalk`, `RosterNext`, `RosterStarts`, `RosterUsage` | same names | The account walk and its pass; the next start's reason, last reading and wiring; the numbered starts in flight; the usage questions and the accounts a switch could reach |
| `OfferAsk`, `WalkMemory`, `WalkPass` | same names | What a start asked of the walk, what one walk remembered, and what one pass held |
| `HarnessSettings`, `AccountSet`, `PoolReceipts` | same names | `harnesses.json`: pins, each agent's one account set, its order and use settings, the fold and its receipts; `HarnessSettings` keeps its fields, the resolving and `With…` edits and one-line doors over the three units below |
| `HarnessSettingsFile`, `HarnessSettingsChange`, `HarnessAccountFolders` | same names | The file read and written in the writer's layout; one change under the keep lock, the come-up fold and the pin; where managed versions and accounts live on disk, which folders are accounts, and removing one |
| `AgentAccountUse`, `AgentAccountLines`, `AccountVerbContext` | same names | `agent account use`, the terminal's words about an account's use and last reading, and what every account verb reads; `AgentAccountsCommand` keeps the other verbs and one-line doors to the lines |
| `AccountRotation`, `RotatedOpening`, `RotationWords`, `RotationUse`, `UsageCurves`, `SwitchDecision` | same names, `Domain/` | The walk's order, the rotated start's opening and the walk's words, the next start's reason, the curves, a running session's switch (pure); its records and enums (`AccountState`, `WalkStep`, `NextStart`, and the rest) each in a file of their own |
| `AccountLimits`, `AccountCooling`, `RefusedAccounts`, `AccountSignIns` | same names | Limit words and resets, `cooling.json`, refused credentials, sign-in refusals |
| `AgentErrors`, `TransientRetry`, `TransientRetries` | same names | The four-kind reader of a door's failure, and the retry in the same conversation |
| `AccountWindows`, `UsageQuestion`, `ClaudeUsage`, `CodexUsage`, `UsageProjection` | same names, the readers in `Providers/ClaudeCode/` and `Providers/Codex/`, `Domain/UsageProjection.cs` | Each account's windows, read at no model spend: the question and its answer, each provider's own `UsageQuestion` (`ClaudeUsageQuestion`, `CodexUsageQuestion`) in its folder saying how it is asked, how long a reading stands and its source, so the roster switches on no kind; the capacity estimate |
| `UsageHistory`, `UsageHistoryReads` | same names | The readings and turns history: kept, rotated and pruned; read back by month with the gaps and the unread counted; its records (`KeptReading`, `KeptTurn`, `HistoryGap`, and the rest) each in a file |
| `HarnessProbe`, `HarnessKeys`, `HarnessSelection`, `StartWiring`, `ProbeLock`, `ScratchSignIns`, `SignInPorts` | same names | Version and sign-in questions, API-key accounts, a selection and the wiring a start is handed, one question per home at a time, no real sign-in under a scratch home |
| `AgentSettings`, `AgentOffers`, `DoorOffers`, `SessionChoices`, `ISettingsFile` | same names; `Providers/ClaudeCode/ClaudeSettingsFile.cs` | An account's model and effort in the tool's own file (the provider's `Models` part states its aliases and efforts); what an agent offered per version and account, read by `AgentOffers.Offers` in two scopes (a session's, an account default's); `SessionChoices` is the session scope the page's actors offer per door, `max` only on Claude Code's native door |
| `IChoiceReask` | `Contract/IChoiceReask.cs`; Loop's `ChoiceReask` | The open's answer to a refused choice, implemented in Loop where the executor is built, the refusal kept in the offers store before the walk: a later choice carried on the same session, or none |
| `PermissionRules`, `RuleProposals`, `ClaudeTrust`, `Across` | `Permissions.cs`, `Providers/ClaudeCode/ClaudeTrust.cs` and same names | Rules in scopes and defaults, an agent's proposals, Claude Code's folder trust, reach into other checkouts |
| `IRuleLanguage`, `ISkillLayout`, `IServerHanding`; `HandedRules`, `SkillSource`, `ConnectorPlan`, `ConnectorSource`, `PostureAsked` | `Contract/Ports.cs`; `Contract/`, same names | How a provider takes the product's rules, lays out a start's skills, and how a door takes a start's servers at its spawn, each handed plain values; a provider or door stating one absent is handed none of it; the posture a start asks of a protocol door (modes, required, the person answers) as one value with its one wire string and reader |
| `ConnectorPlans`, `PluginAdapterSets` | `Agents/ConnectorPlans.cs`, `Plugins/PluginAdapterSets.cs` | The one composer of a start's plan per workspace, and where the plugins' doors join the adapter set (`WithPlugins`, over `AdapterSet.Joined`) |
| `ClaudeRules`, `ClaudeSkills`, `ClaudeServers`, `ClaudeLineageHook`, `LineageHook` | `Providers/ClaudeCode/` | Claude Code's: its settings file with its hooks and hard denials; its per-session plugin copies; the servers file its native door names; the lineage hook's script (`hooks/lineage-hook.mjs`), installed for the lineage file Loop hands |
| `SpawnSkills`, `SpawnFiles`, `AdapterParts`, `DshProfile` | same names; `Registry/AdapterParts.cs`; the last in `Providers/Dsh/` | A session's skills laid out by its door's provider and handed by its spawn; what a spawn wrote, removed with the session; a door's rule language, layout and server carrier as today's readers ask an adapter (a plugin's or a test's adapter, which declares none, takes Claude Code's, as before); the dsh home Daoris made |
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
   `Reach` is never absent. (from D53, D57, D213)
7. Each `AgentRun` operation is admitted by the run's own evidence. One it lacks is refused as unsupported before anything is
   written. (from D213)
8. Every executor holds its run through `TransportRun`: the driven start, the continuations, the go-on, the resume's one retry
   and the conversation's runner. No executor prepares a process itself. A conversation reads its agent through `ChatAgent` and the
   run it makes; no file of `Loop/Chat/` names `ISessionAdapter`. (from D213)
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
    stand-ins) is written nothing. A start that leaves the agent's own servers out is refused on a door that cannot (`OwnServersLeftOut`,
    stated only by Claude Code's native door; no start requires it yet). A native door drives an adopted repository by its own
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
    own tables first, then its owner's. The agent's own message is never read. Anything unread is fatal. (from D210, D125)
37. A limit cools that account until the reset the agent named, plus 2 minutes, with 15 minutes' grace and nothing believed
    past 8 days, else 60 minutes. A limit is never a strike. (from D125)
38. A limited line resumes by itself at the look: its own conversation on its own account when ready, else a briefed resume on
    another account or choice, else it waits showing the earliest reset. (from D206)
39. A transient failure is tried again in the same conversation after 1, 2, 4, 8 and 16 minutes, up to the line's tries
    rule. It is never a strike, park, cool-off or walk. Past the bound the quest is held until the person's word or
    *Try again*. Every run on the protocol door takes that one retry (`TransientRetries.For` at the hold, `ChatRetry` for a
    conversation), each with its owner's check: a driven session, an intake, a helper, an opinion, a partner and a
    brainstorm voice. A pass with minutes of its own waits none past them. Past the bound, or with its minutes ended inside
    the episode, it fails with its kind's code (`helper.transient`, refused `transient`; `opinion.transient`;
    `intake.transient`, its ask unbound; `partner.transient`, its work kept; a voice dropped), never *timed out*
    (`TransientEnded`). (from D210, D229)
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

## Where a change goes

**By subdomain.** Agents sits after Knowledge and before Workplace, Roles, Participants, Context, Course and Loop
(`Driver.Tests/StructureTests.cs:448`). Agents may use what is below it. What sits above reaches it through ports installed
when the driver loads (`IAgentContext`, `IAgentWorkplace`, `IAgentWorkWords`, `IAgentPlugins`).

- A new agent or door: a registration line in `Registry/Providers.cs` and a folder under `Agents/Providers/<id>/`, on the
  contract. Do not add another `ISessionAdapter` class; those end at row t.
- A new fact about an existing real agent (a flag, a posture, a word table entry): its adapter or `HarnessToolchain` in
  its adapter file today, measured first, with a recorded sentence for any word table.
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
- A branch on what a door can do: ask its ability (`adapter.Abilities()`, `DoorAbilities`), never its wire; whether it may drive an
  unadopted repository is `Adoption`'s. Only the transports, the providers and the machine door's holders s-i removes name
  `SessionWire`, each a named exception of the provider census.

**Tests that pin the area** (`src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Agents/`):
- contract: `ProviderCensusTests`, `ProviderConformanceTests` (and its process and live cases), `ProviderRegistryTests`,
  `ProvidersBuiltTests`, `StandInShimProcessTests`;
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

1. D23 (one harness supported; a second written only when a repository adopts one) vs `AdapterSet.cs:97` (four real doors,
   stand-ins and plugin harnesses). D53, D57 and D213 read right; only D23's "unknown name is an error" lives.
2. D24 and D98 (Daoris names no model and chooses none) vs D205 and `Roles/ActorSuggestion.cs:23`, `Roles/RoleSuggestion.cs:23`
   (the suggestions name `sonnet` and `opus`; starts carry the role's model). D205 reads right.
3. D205 point 7 (the suggestion leads Build with `opus` · high) vs its note of 2026-10-25 and `Roles/ActorSuggestion.cs:23`
   (`sonnet` · high, then `opus` · high). The note and the code read right.
4. D81 (a driven session is never `bypassPermissions`) vs D184 and `ClaudeCodeAdapter.cs:161` (bypass by a repository's unrestricted
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
14. `ClaudeCodeAdapter.cs:19-22` (the repository's `.mcp.json` wires the connector; the driver may not write it) vs
    `Loop/Driver.cs:3323` and `:5209` (Daoris offers its connector at every spawn). The spawn reads right.
15. `CodexAcpAdapter.cs:10-15` (codex-acp offers three modes, "no third reading") vs `CodexAcpAdapter.cs:57` (`workspace-write` read in
    codex-acp 2.1.1). The property reads right.

## Open

- **AGENTPROVIDER1d**'s rest: s-ix on (each disagreeing cell of the owner table, one row each); the toolchain classes
  `OwnerOf` and `ToolchainOf` still read go with rows f, h and t; the roster's four word readers (`LimitsOf`, `SignInOf`, `WindowsOf`,
  `UsageOf`) still read toolchains, not the registry. **1e**'s rest: the window, limit and sign-in tables split between provider and
  door (`AccountLimits.cs`, `AccountReadings.cs`, `HarnessAccountFolders.cs`), `ToolHomeLeftovers`' homes from the providers, the key
  variable read once, `alt-stub`'s account cases un-pended; then s-iii.
- **AGENTPROVIDER1** host rows (f, h, l, m-ii, n, o, p, q, r, s, u): f's programs as `ToolSpec`s, and `AgentPinCommand`'s pin
  install and its update's move folded onto `HarnessActions` (the newest is one way, `PinNewest`; the terminal's install runs at
  its console and the window's redirected, a difference still to be recorded); s-i, the machine door's holders reading
  `Adoption.Drives` and an unknown id refused; h's rest: the identity values composed by Loop's executor into a request the
  shell reads, Platform's launcher starting and containing the completed recipe (D201), the identity names moved down from
  `IntakeRoom` and `BrainstormVariables`, `CommitTrailers` and `HolderTerminal` through the registry; the installed proof that
  a Codex and a dsh session start as before without Claude Code's seam (s-v) and with no other provider's credential (s-vi);
  the sink's rest (l: the open's refused model still a re-ask and no reading, the lineage still a writer the run makes rather than
  Participants' part, the four `stream-json` readers not yet one, `ChildBindings` not yet each provider's `IChildVocabulary`),
  m-ii's rest (words through the run by `DoorInput` and `GoOnAsync` gone with it; a conversation's native retry still `ChatWatch`'s own,
  a start of its own rather than the run's, since the conversation's words go to the process its `NativeChat` holds; the protocol resume
  once for a driven run and a conversation; the hand-over's accounts once; `IConversationHome`), n's rest (the doors' `Offers` part, the
  actor run's asked choice read from the door's spawn, the offers store read by the start plan and the walk through `AgentOffers.Offers`
  rather than `Read` and `Judge`), s-ii (the group's report of a resume against `ResumesOwn`, whose two reads agree on inspection and
  want a planted disagreement); o and p are built (`PostureAsked`, the trust preflight through the door's `Home`, an unmapped full or asking start refused
  naming it, the questions pause's value read from its provider; one `ConnectorPlan` per workspace read by both doors, the session
  home's servers file named by its provider); p's rest: `AcpMcpServer` renamed `ServerEntry` (a mechanical rename over some thirty files),
  the five places of `Adoption` carrying a bool where they still carry `SessionWire`, and the page's twin; q is built in part
  (`WithPlugins` out of Agents into Plugins); q's rest: the factory of `DeclaredDoor`s, a plugin's door a contract door in place of
  `DeclaredAcpAdapter`, each door's own `Identity` and `Home` from its declaration, a provider made from declarations; the provider sheet (r, with **COAGENT1e**), and the rest of D213's design.
- **AGENTPROVIDER1t**: the old seam ends. `ISessionAdapter`, `HarnessToolchain`, `SessionWire`, `AdapterSet`, `LegacyDoor`
  and the stand-in classes go; every record still reads.
- The agent-layer adoption (D213's notes of 2026-10-29 and 2026-10-30): Daoris's providers consumed from the sibling
  agent-provider library as a package. Its rows wait on that library's releases; nothing is referenced by the driver yet.
- **AGENTINFRA1**: an execution-neutral run for remote APIs and hosted runtimes, with scoped availability and explicit
  recovery. Accepted; built after AGENTPROVIDER1, isolated home first.
- **AGENTERRORS1c**: the native door's failure reads and the measured `--resume`; **1d**'s rest: an intake's ask bound
  again after a second `intake.transient`, an opinion's gate read `Unavailable` / `transient` from its code, and each
  kind's own authority as its check; **1e**: what remains of point 8 (the native door's live, driven and one-turn tries).
- **LIMITRESUME1f** and **TOOL4n**: the native door's limit read, so a native session ended by a limit cools its account.
  Waits on a recorded native limit, the owner's yes. The channel and the reader are built: a failed native `result` is one
  `WireReading.Failed` (`pipe`, `DoorWhere.Result`, the result's own words) on the run's sink, and `NativeLimits`, a sink part composed in
  `SessionHold` and `ChatWatch`, reads it through `AgentErrors.Read` and cools the account on a limit, saying it on the account's line.
  What is not built: the conclusion still reads no native limit (the record ends as a failed or completed run, with no limit note and no
  park), and a native conversation moves to no next account; those and the recorded limit are what remain of D210's row 1c.
- Row s-ii of the agent-layer adoption: a driven Claude Code start denies or keeps the three tools a headless turn cannot
  answer. The measurement needs the owner's yes.
- **CODEXTOOLS1**: Codex's first tool catalogue waits for the connector to be ready.
- **STEER3**, **MSG1h**, **MSG1i**: steering and next-step words measured on other doors (Codex, native).
- **BGWAIT2b**, **SESSCTX1a**, **HELPMODE1a3**, **FABRIC-C2b**: woken-turn words, resume and fresh part per door, full access
  on the installed pins, the two-harness canary.
- **HELPWIRED1b**: what the door says it loaded, beside what was handed.
- **POOL1d**: the install's fold of `harnesses.json` read rule by rule, applied on the person's press.
- **MODELROLE1c4**, **MODELROLE1d3**: the installed proofs of a refused model passing an account and a chat's chosen model.
