# One provider contract every agent implements — the independent Opus reader's proposal

> AGENTPROVIDER1, phase 1 under D191 (its amendment of 2026-10-23 seats an independent Opus reader beside the makers). One of three sealed
> proposals, written from `docs/2026-10-26-agent-provider-brief.md` and `docs/2026-10-26-agent-provider-survey.md` alone, with FOUNDATION1's
> accepted design (`docs/2026-10-25-foundation-design.md`, D212) as its ground, before any other proposal for this design was read. The code
> was read at `9510a5390`, whose `src/` and `tools/` are the survey's `f58f1be61` unchanged, so every line the survey cites holds and every
> line cited here is that commit's. Read, not run: nothing here is built, spends, signs in, publishes or deletes. Paths follow the survey's
> aliases (a bare path is under the driver library; `Modules/`, `Host/`, `Core/`, `Mcp/`, `Http/`, `Web/`, `Cli/`); `Dnn` is
> `docs/decisions/Dnn.md`; a folder named *after mod-ix* is where row mod-ix leaves the Agents subdomain (D212 §14.2 row 9); rows are named
> P1 to P16 here, and OPENROUTER1's O1 to O3, for the synthesis to name as it files them.

*Sealed 2026-10-26, before any other proposal for this design was read.*

## 0. What I checked, and where the survey is short

I read again every line this proposal rests on: `Agents/Adapters/Adapters.cs:1292-1676`, `:1683-1806`, `:2449-2572`;
`Accounts/Harnesses/Harnesses.cs:150-282`; `Agents/Streams/StructuredOutput.cs:1-135`; `Agents/Streams/HarnessCapture.cs:80-137`;
`Agents/Acp/Acp.cs:300-344` and every `_claude/`, `claudeCode`, `jetbrains`, `model_usage` and `thread_active_writer` read under `Agents/Acp/`;
`Agents/Errors/AgentErrors.cs:1-345`; `Plugins/DeclaredAcpAdapter.cs`; `Sessions/Domain/SessionEvents.cs:1-130`;
`Sessions/Domain/ChildBindings.cs:1-90`; `Accounts/Usage/UsageTurns.cs:1-35`; `Accounts/AccountWindows.cs:50-90`;
`Accounts/Domain/SwitchDecision.cs:55-80`; `Accounts/Domain/UsageProjection.cs:85-126`; `Loop/Driver.cs:655-750`;
`Quests/Domain/Planner.cs:1920-1950`; `Agents/Permissions/SpawnServers.cs:50-70`; `Setup/SetupWorld.cs:15-31`;
`Modules/Bridge/DriverContext.cs:150-173`; `Modules/Platform/HolderTerminal.cs:1-56`; `Help/HelpWired.cs:170-189`; the structure census
(`Driver.Tests/fixtures/structure.json:828-835`, `:1000-1045`, `:1095-1106`, `:1184-1197`, `:1306-1399`); COAGENT1's §3; FOUNDATION1's §2.1 to
§2.7, §4.1 to §4.5, §10's Part B rows, §13.3 and §14; D23, D53, D64, D70 and D210's rows; the glossary's *agent*, *harness*, *profile* and
*way in* (`Web/locales/glossary.json:594-600`). The survey holds where I lean on it; these are where it is short.

1. **Each door already has a sink, and the two disagree in shape.** The native door's structured capture is handed `observed`, `said`,
   `named`, `own`, `lineageHome` and `handed` (`HarnessCapture.cs:114-120`); the protocol door's session is handed `onLine`, `onEvent`,
   `streams`, `onOptions` and `onConversation` (`Acp.cs:308-320`), and its failures leave by a third route (survey §12). The normalized stream
   the brief asks for exists twice, as two sets of callbacks; the survey lists their readers, not the two sinks. The contract's stream is their
   union given one shape (§3).
2. **The stream's vocabulary cannot live in Agents.** The census's *Events names Agents* family (`structure.json:1097-1105`) is
   `Observation.cs` naming `HarnessEnding`, `ModelRefusal` and `TransientEpisode`, and `SessionEvents.cs` naming `TurnModels`: the session's end
   and turn vocabulary, which D212 placed in Events (§14.2 row 7, O4-5), already reads facts declared above it. A provider stream declared in
   Agents adds that edge for every reading. The brief's *the contract lives in Agents* holds for the contract; what a provider says belongs one
   floor down, in Events, beside `SessionEvent` (§1.5).
3. **The protocol stub speaks both makers' dialects, not only Claude Code's tables.** Beyond the toolchain the survey names
   (`Adapters.cs:1709-1741`), the rehearsal and the tests feed Claude Code's and Codex's wire extensions to `acp-stub`: `promptQueueing`
   under `DAORIS_STUB_QUEUEING` (`tools/rehearsal-kit.mjs:824-828`), and Codex's `thread_active_writer` in `LimitResumeTickTests.cs:409`,
   `OpinionResumeTickTests.cs:341` and `SessionMessagesTickTests.cs:780` (the five keys `_claude/rateLimit`, `_claude/model`, `promptQueueing`,
   `subagent_spawned` and `thread_active_writer` appear 63 times in 24 files outside `docs/`, most of them tests). A move that puts each
   extension behind its own agent's dialect must keep `acp-stub` on all of them, or those cases fail for a reason that is no regression.
4. **Two of the brief's disagreements are decided already.** The token weights applied to every agent (`UsageProjection.cs:93-97`, `:121-122`)
   are D179 point 3's, *used for every agent until USAGECAP1b's table stands*: an interim decided, not an accident. And the pipe door's limit
   that cools nothing (survey §12) is D210's row 1c, *which closes D206's row 1f gap* (`D210.md:150`), with FOUNDATION1's row 1g beside it: a
   change decided and owed, which a move must neither make silently nor block.
5. **`WeekFixed` disagrees with nothing.** `AccountWindows.WeekOf`, the field's one consumer, has one caller, the modules' route
   (`Modules/Bridge/Routes/AccountsRoutes.cs:62`, `:98`); the driver has no second reader to disagree with it. What is wrong is the route's own
   rebuild of the owner (`owner.Any(door => door.Toolchain.WeekFixed)`), a fifth copy of *own, else owner*.
6. **D70 is why a pipe agent cannot drive an unadopted repository, and its reason binds less than the five copies suggest.** The planner says
   why (`Planner.cs:1923-1925`): the pipe door's driven session reaches the connector only through the repository's `.mcp.json`, which the
   driver may never write. The helpers' file (`SpawnServers.cs:5-53`) is written under the home, not in the repository, and the holder's
   terminal already runs Claude Code on Daoris's servers alone (`--strict-mcp-config`, `HolderTerminal.cs:44`). So the five places decide a
   choice D70 made, not a fact of the door; the contract states it once (§2, Q16), and whether to change it is CONNECTORS1's.
7. **The census's baseline, counted.** Outside the test projects under `src/Daoris.Desktop`, `SessionWire.Acp` or `SessionWire.Pipe` is named
   55 times in 23 files, `.Wire` read 56 times in 26 files, and an agent's id written as a string literal 36 times in 15 files (by search,
   counting declarations; a bound from above). Each is a site P1's census names.

## 1. The contract's shape

### 1.1 In one paragraph

A **provider is an agent** (Claude Code, Codex, dsh, a stand-in, a plugin's) **with one or more doors**, each door a way in that is today an
adapter and keeps that adapter's id. The contract is **one registration per provider**, a record of parts rather than one interface: what is a
fact about another program (its binary, its accounts' variables, its postures in its own words, its limit and error sentences) is **declared
data**, each fact in exactly one place, on the provider where it is the agent's and on the door where it is the door's, so no reader asks
*own, else owner*; what differs in behaviour (reading a wire, asking an account's usage, rendering rules, building a spawn) is **a small port**
the provider implements; and every optional fact is a `Known<T>` that names its source (declared, negotiated at the open, measured) or says it
is absent, so a provider states its silence instead of inheriting a default by omission. Two **transports** are shared and agent-neutral, the
native one (a process per run, read line by line) and the protocol one (ACP's spec frames); each door picks one and hands it its **dialect**,
the agent's own vocabulary on that wire, so no agent's key is read in shared code. Every provider tells what happened in **one vocabulary of
readings**, declared in Events beside `SessionEvent` and written to **one sink** the executor composes; the provider mints no event, and
FOUNDATION1's hubs and triggers mint from what the sink keeps. COAGENT1's sheet is **a read of the contract**, never a second declaration. No
record changes shape: a record keeps a door's id where it means a door and an agent's where it means an agent, and one registry resolves both.

### 1.2 What a provider is

| Today | After | Why |
|---|---|---|
| `claude-code` (the pipe door and the accounts' owner), `claude-code-acp` (the protocol door, `AccountOf: claude-code`) | provider `claude-code`, doors `claude-code` (native) and `claude-code-acp` (protocol) | the accounts, the words and the identity are the agent's (AGT7); a door's id equal to its own agent's stays legal |
| `codex-acp` (`AccountOf: codex`) and the `codex` holder, a toolchain with no door (`Adapters.cs:2567-2571`, `Accounts/SignIns/CodexAccounts.cs:30-53`) | provider `codex`, door `codex-acp`; no holder | the holder exists because the agent had no door (survey §1, CODEXACCT1); as a provider it has one |
| `dsh` | provider `dsh`, door `dsh` (protocol) | — |
| `stub`, `acp-stub` (`AccountOf: stub`) | provider `stub`, doors `stub` (native) and `acp-stub` (protocol), declared to speak Claude Code's vocabulary, and `acp-stub` Codex's dialect too (§0 item 3; Q16) | they rehearse those agents' paths, and say so |
| a plugin's harness with `accountOf` | a door of that provider, `DeclaredBy` its plugin | today's *own, then owner* (`AgentErrors.cs:83-86`), made membership |
| a plugin's harness with none | a provider of its own, named by the harness, with that one door | `Owner(name)` is the name today (`Harnesses.cs:281`) |
| — | provider `alt-stub`, doors `alt-stub` (native) and `alt-acp-stub` (protocol): the third stand-in, its vocabulary its own (P2) | the brief's proof (§8) |

**Identity's rules.** An agent's id is unique among agents and a door's among doors; a door's id may equal its own provider's
(`claude-code`, `dsh`, `stub`) and never another provider's; a plugin may declare neither kind of id that the built set holds (D64 rule 3,
`AdapterSet.Reserved`, `Adapters.cs:2490-2495`); an unknown id is an error naming what exists (D23, `Adapters.cs:2539-2546`), and an agent's id
where a door is needed is that same error, never the agent's first door.

### 1.3 The parts

`Provider` and `AgentDoor` are sealed records in `Agents/Contract/` (after mod-ix); `AgentDoor`, because tests already keep private `Door`
doubles of today's adapter (`CodexAccountTests.cs:344`, `HelpPlaceWorkspaceTests.cs:33`) and the service's host has a public `Door`
(`Http/PersonDoors.cs:32`). Each part names where today's fields go (`HarnessToolchain`'s thirty, `ISessionAdapter`'s twenty-one), its source in
COAGENT1 §3.1's sense, and what a provider that states it absent gets, which is today's behaviour for an agent that says nothing (D53).

**The provider, the agent:**

| Part | Holds | From today | Source | Stated absent, reads as |
|---|---|---|---|---|
| `Id` | the agent's id | `Owner(name)` | declared | required |
| `Identity` | product, maker, mark; `StandIn` (runs no model); `DeclaredBy` (a plugin's id, set by the source, never by a manifest); the maker's commit attribution | `Product`, `Maker`, `Mark` (`Harnesses.cs:199-243`); `AdapterSet.DeclaredBy`'s type test (`Adapters.cs:2511-2514`); the trailer's switch on the maker (`Adapters.cs:383-431`) | declared | product unknown; the page derives a mark (`Web/agents/agents.ts:499-536`); a maker not declared is no family of any other (COAGENT1 §3.2) |
| `Doors` | its `AgentDoor`s | the adapters of one owner | declared | refused at registration: no door, no provider |
| `Program` | the agent's own binary as Install's `ToolSpec`: binary, version question, install, update, package or channel, pinned environment, presence probe | `Binary`, `VersionArguments`, `Install`, `UpdateArguments`, `Package`, `Channel`, `PinnedEnvironment`, `ProbeByPresence` | declared | Daoris manages nothing about it (today's null toolchain, `Adapters.cs:1476`) |
| `Accounts` | the kind (a sign-in home, a key, a provider's key with its variables); the home variable; the key variables; `RunsInOwnHome`; the sign-in, the device code and its ports; the login question; the trust file; the settings file; `WeekFixed`; the reading floor | `ProfileVariable`, `KeyVariable`, `LoginArguments`, `LoginCheck`, `DeviceLoginArguments`, `LoginPorts`, `TrustFile`, `SettingsFile`, `WeekFixed`, `RunsInOwnHome`; the floors (`HarnessRoster.cs:2107-2134`) | declared | its own home: no walk, no cool-off, a limit read as a failure (COAGENT1 §3.2's *Accounts* row) |
| `Tables` | its limit, window, sign-in and refused words and its error words (kinds, statuses, transient sentences), each entry on a recorded sentence | `Limits`, `Windows`, `SignIn`, `Refused`, and `ErrorWords` as `ClaudeErrors` is (`AgentErrors.cs:281-323`) | declared on a recorded sentence (D125 point 1.4, D210 §3.3) | read as today with no table: fatal, no cool-off, no window |
| `Usage` | `IUsageQuestion`: an account's windows asked between turns | `UsageQuestion`; `UsageKind {Codex, Claude}` (`Accounts/Usage/CodexUsage.cs:21-39`; `HarnessRoster.cs:2287-2360`) | declared, a port | nothing asked; near and pace stand aside |
| `Models` | `ISettingsFile` (an account's default model and effort, its keys and aliases); the efforts it words; where its offers come from | `SettingsFile`; `Accounts/AgentSettings.cs:48-168`, `:234-262` | declared | no account default offered, as a null `SettingsFile` says today |
| `Children` | `IChildVocabulary`: how its lineage names a child (its hook's lines, its calls' `_meta` keys, its stream's signals), and whether it says a child's end | the fold's branches per harness (`ChildBindings.cs:40-60`, `:128-266`, `:293-323`) | declared; measured per door and version (FABRIC-C2b) | children unknown: a call bound to nobody, counted apart (`ChildBindings.cs:52-56`) |
| `Rules` | `IRuleLanguage`: Daoris's rules in its own grammar, with its hard deny and hooks | `Agents/Permissions/Permissions.cs:42-48`, `:188-255`, `:487-588` | declared | takes none: the copy is the floor, and the page says the person's denies do not reach it (COAGENT1 §3.2) |
| `Skills` | `ISkillLayout`: how it is handed skills | `SpawnSkills.cs:6-31`, `:57-90`; `DshProfile.cs:48-56` | declared | handed none |
| `Conversations` | `IConversationHome`: what a conversation keeps under an account's home | `Platform/ToolHomeLeftovers.cs:54-55`, `:67-99`; SWITCHCTX1 §1.5 | declared, read in its files | no carry: a move hands the brief (D189) |
| `Reach` | where it may work: everywhere, or only the repositories opened to it | — (OPENROUTER1's) | declared | everywhere, as every agent today |

**The door, a way in:**

| Part | Holds | From today | Source | Stated absent, reads as |
|---|---|---|---|---|
| `Id` | the door's id, today's adapter's | `ISessionAdapter.Name` | declared | required |
| `Transport` | `Native` or `Protocol` | `Wire`, `SessionWire` (`Adapters.cs:1293-1330`) | declared | required: today's default `Pipe` becomes a statement |
| `Program` | its own `ToolSpec` where it is a package apart (an ACP adapter, ACP2) | the protocol doors' `Binary` and `Package` | declared | the provider's program |
| `Postures` | ordinary, asking and full, each a mode in its own words or a variable and its value (dsh's) | `AcpPosture`, `FullPosture`, `AskingPosture` (`Adapters.cs:1348-1376`); `DSH_PERMISSION_MODE` (`:1981-2015`) | declared, measured | ordinary: the agent's own default; asking or full: refused naming the agent (D183, D184) |
| `Spawn` | `ISpawn`: the argument vector for a start, a chat and a native resume, and the variables this door's process takes (its home and key variables, its pinned environment, its own seams, its tools-up-front switch) | `Prepare`, `PrepareChat`, `PrepareResume`, `ToolsUpFront`, `HandServers`, `HandSettings`, `HandPlugins` | declared | required |
| `Dialect` | `INativeDialect` or `IProtocolDialect` (§1.4) | `StructuredOutput()`, `FrameMessage`, `FrameInterrupt`; the reads inline in `AcpSession` | declared | native: plain text, no events (`HarnessCapture.cs:6-11`); protocol: the spec alone |
| `Abilities` | a `Known<bool>` each: interactive; structured; words at the next step; steering; a stop that keeps the conversation; one task stopped; resumes; a resume that fails closed; requests held for the person; takes rules (by flag or by `_meta`); hands servers (on the wire, by a file, not at all); its servers inherit its environment | `Interactive`, `Resumes`, `TakesSettings`; the `Wire` tests in the callers (survey §6); `TurnDoor(Steers, KeepingStop)` (`Conversations/Domain/ChatQueue.cs:157`) | declared, or negotiated at the open (queueing, steering, the resume method), or measured (steering's effect, lineage) | COAGENT1 §3.2's fallback per row: words wait for the turn's end, *now* waits, *Stop* ends the part, a new part handed the brief, refused, the copy the floor |
| `Tables` | its own wire's codes (`AuthRequiredCode`), its own error words, its turn report's meaning | `ClaudeAcpErrors` (`AgentErrors.cs:329-344`); `TurnReport` (`Harnesses.cs:252`) | declared | nothing read by code; the meaning `unknown` (D179 point 8) |

`Known<T>` is `{ Value, By: Declared | Negotiated | Measured, Evidence }` or `Absent`; a negotiated one names the frame that answers it and holds
no value until a session's open has answered. **The parts are required members**: a provider must state each, `Absent` being a statement, so
a member the contract gains later breaks every provider's construction until each answers it, which is the opposite of today's twenty-one
defaults (`Adapters.cs:1321-1513`), under which `DeclaredAcpAdapter` is given `Resumes` true by its door and every other answer by omission
(survey §6). The name is `Known`, not *capability*, since Roles' `Capabilities` is the work vocabulary (D212 §2.7) and a simple name that
binds to two things is what §14.6 of that design avoids.

### 1.4 The transports and the dialects

- **The protocol transport** is `AcpSession` with every read of one agent taken out: the spec's `initialize`, `session/new`, `session/resume`
  or `session/load` by the agent's own capabilities (`Acp.cs:3457-3479`), `session/prompt`, the spec's `session/update` kinds,
  `session/request_permission`, `session/set_mode`, `session/set_config_option` found by category (`Acp.cs:667-672`, `:728-761`),
  `session/cancel`, and the transient retry's policy. What it does not know it hands its door's `IProtocolDialect`: the `_meta` it sends at
  `initialize` and at `session/new` (`jetbrains.air`, `Acp.cs:1789`; Claude Code's settings and plugins, `Adapters.cs:1881-1906`), whether the
  agent queues words and steers (`Acp.cs:1704-1734`), each update outside the spec (subagent frames, async tasks, a woken turn:
  `AcpStreams.cs:77-103`, `AcpBackground.cs:42-79`, `AcpWake.cs:216-230`), a turn's `_meta` (its model, its usage per model, its windows:
  `Acp.cs:2138-2160`, `:3030`, `:3043`), a refused resume's reason (`thread_active_writer`, `Acp.cs:3387-3394`) and an error's shape
  (`Acp.cs:175-239`). Each answer is a reading (§3) or a negotiated ability.
- **The native transport** is the process per run with its target on its command line, read line by line: the plain capture
  (`HarnessCapture.cs:46-78`), the structured capture with the door's `INativeDialect` (today's `IStreamMapper`, `StructuredOutput.cs:44-65`,
  with `FrameMessage` and `FrameInterrupt`), and a resume as a new process on the conversation the dialect named, tried once more where the run
  exits before naming it (three copies today, survey §5). It is no one agent's: the stub rides it with no dialect, `alt-stub` with its own.
- **Both** start their process through the one shell (`Spawning.Shell`, `Adapters.cs:1606-1653`) and the launcher's containment (D201),
  unchanged and agent-neutral.

### 1.5 Where each type sits

Bottom to top in D212 §14.2's order; each names only its own subdomain's types and lower ones, and each cut is the edge family the census
already lists.

| Type | Subdomain | Names | The edge it cuts |
|---|---|---|---|
| `ToolSpec`: a program to install, pin, update and ask its version | Install (5) | Platform | *Install names Agents* (`ToolInstall.cs` naming `HarnessSettings`, `structure.json:1002-1006`): Install is handed specs, never a provider |
| `WireReading` and its kinds (§3), with the turn's tokens and models moved down from `TurnModels` and `AcpUsage` | Events (7) | Platform, Events | *Events names Agents* (`structure.json:1097-1105`): the end and turn vocabulary is Events', read by `Observation`, written by providers |
| `ServerEntry` (today's `AcpMcpServer`, `Acp.cs:36-57`, both doors' server shape) | Knowledge (8) | Platform | *Knowledge names Agents*' `KnowledgeConnector` row (`structure.json:1193`): the connector builds its entry in its own type |
| `IAgentFacts` (the agents and their sign-in states a room reads, as values), declared in Knowledge and implemented by Agents | Knowledge (8) | — | the room's rows of that family (`HelpFacts`, `HelpRoomAgents`, `structure.json:1190-1191`) |
| `Provider`, `AgentDoor`, `Known<T>`, the parts and ports, `IReadingSink`, `SpawnRequest`, `DoorInput`, `ModelAsked`, `PostureAsked`, `IProviderSource`, `Sheet` | Agents (9), `Agents/Contract/` | Install, Events, Knowledge and below | — |
| `ProviderRegistry`, `Providers.Built()` (the one registration list) | Agents, `Agents/Registry/` | the contract | — |
| the two transports and the one shell | Agents, `Agents/Transports/Native/`, `Agents/Transports/Protocol/` | the contract; Platform's launcher | — |
| each provider | Agents, `Agents/Providers/<Agent>/` | the contract; another provider's program only where its registration says it runs it (OPENROUTER1, §5) | — |
| the sink's writers: the event file and the consoles; the lineage | Context (13); Participants (12) | the sink, the readings | *Agents names Context*'s capture rows and *Agents names Participants*' `ChildLineage` rows (`structure.json:1307-1321`, `:1343-1351`) |
| `SpawnRequest`'s values (the composed target, the role variables, the git wiring, the servers chosen), built by the executors in Loop and Course from Context, Work orders, Review and Workplace | the request is Agents'; its builders are above | Agents' request | the spawn rows of *Agents names Context*, *Work orders*, *Review*, *Workplace*, *Roles* and *Participants* in `Adapters.cs` (`structure.json:1314`, `:1329`, `:1346`, `:1369`, `:1378`, `:1387`, `:1396`) |
| `DoorInput` (a word's text, files, id and key as a door sends it) | Agents | — | *Agents names Course* (`structure.json:1324-1333`): Course maps its `ChatMessage` and `DaorisWord` to it |
| `PostureAsked` (ordinary, asking or full; required; the person answers) | Agents | — | *Agents names Help* (`HelpModes`, `structure.json:1336-1340`) and the `DrivenAccess` row of *Agents names Participants* |
| the plugins' `IProviderSource` | Plugins (17) | Agents | *Agents names Plugins* (`structure.json:1354-1361`) |

The census families no contract row cuts are FOUNDATION1's own: *Service names Agents* (`AccountLine`, `structure.json:830-835`) and *Instance
names Agents* (`VerbLeases` naming `AgentsCommand`, `:1040-1045`).

## 2. The questions, by number

Each answer gives its reason and the counterexample it rules out.

**Q1. The contract.** One registration per provider, of declared parts and small ports, its doors records inside it (§1.3), each optional
fact a `Known<T>` with its source; a part stated absent reads as today. COAGENT1e's sheet is `Sheet.Of(provider, door, negotiated,
measurements)`, a pure read; the measurements table (COAGENT1 §3.1) stays its own record, the sheet's other input, and the negotiated readings
are kept per door and version where COAGENT1e keeps them. *Reason:* one interface with defaults hides answers in omissions, and a negotiation
alone serves neither the native transport, which negotiates nothing, nor a claim Daoris may make only on a measurement (D53, D57). *Rules out:*
a new provider that says nothing about whether its servers inherit its environment being read the way Claude Code's are, which hand their
servers the whole environment (survey §9), so a session's identity would be trusted to reach a connector that Codex starts with its entry
alone (CONNECTOR2); as a required `Known<bool>` it must be stated, and absent reads *does not*, which is what Daoris already assumes when it
writes the home into every entry (`Service/KnowledgeConnector.cs:80-106`).

**Q2. Provider, agent and door.** A provider is an agent with doors (§1.2); `AccountOf` and `Owner` become a door's membership in its
provider, the `codex` holder becomes the Codex provider, and the facts one door declares for its agent move to the agent (`codex-acp`'s
Codex limits and usage, `Adapters.cs:2104-2125`; `claude-code-acp`'s repeated identity and trust file, `:1919-1945`). Every record keeps its
id (§4), and `ProviderRegistry.Resolve(id, kind)` resolves each by the kind its field holds. *Reason:* an account is the agent's (AGT7), and the
agent-and-door shape is why four copies resolve the owner with four rules (survey §15 tangle 3). *Rules out:* OpenRouter declared as two pairs,
a native one and a protocol one, each with its key account and limit words, so a limit read on one door cools an account the other reads as
ready; one account on two doors already counts as two rows today (`Accounts/Usage.cs:69-76`).

**Q3. The door.** Both transports are shared; each door hands its transport its dialect (§1.4), and the pipe door's readers
(`ClaudeStreamJson`, `ClaudeStreams`) move into Claude Code's folder as its native dialect, `StructuredOutput.cs`'s shared record staying with
the transport. The door's paths become abilities: words at the next step and steering negotiated by the dialect (`Acp.cs:1704-1734`), *now*
admitted only beside a measured row (COAGENT1 §3.2); a stop that keeps the conversation by the spec's `session/cancel`, or by the native
dialect's interrupt frame where it declares one (`StructuredOutput.cs:410-424`); one background task stopped where the dialect negotiates
async tasks, never on the native transport (`ClaudeStreams.cs:23-25`); requests held for the person on the protocol transport under an asking
posture (`Acp.cs:2757-2910`); the native resume's retry once, in the native transport; and the limit resume's `Wire == Acp`
(`Loop/DriverContinuations.cs:54-63`) read as `ResumeFailsClosed`, known by the spec's refusal on the protocol transport and absent on the native
one until measured, the same answer as today. *Reason:* D53 adopted ACP as a protocol whose harnesses are configurations of the door; what made
the door one agent's is the vocabulary read inline (survey §15 tangle 1). *Rules out:* making the pipe door Claude Code's own transport, so the
stub's plain capture, `alt-stub`'s lines and the next command-line agent each copy the process per run, the resume's retry and the capture.

**Q4. Plugins.** A manifest declares a door that joins the provider its `accountOf` names, or a provider of its own: its identity, its program,
its home variable and own home, and its postures in its own words (`posture`, and from COAGENT1e `fullPosture` and `askingPosture`). Its door
rides the protocol transport with the spec's dialect alone. It may never claim rules, skills, limit, window, sign-in, refused or error words, a
usage question, children or lineage, confinement, steering, queueing or resume (the wire answers those, or a measurement does: COAGENT1 §3.4), a
dialect, a conversation home, `StandIn` or `DeclaredBy`. Joining a provider gives it that provider's tables, as *own, then owner* gives it
today, and never another door's (`AuthRequiredCode`). `DeclaredAcpAdapter` becomes the plugins' `IProviderSource`, and `AdapterSet.WithPlugins`
(`Adapters.cs:2521-2537`) leaves Agents for it. *Reason:* a manifest is a word, and every claim about another program stands on a
measurement (D53, D57, D125 point 1.4). *Rules out:* a manifest saying `steering: true` of an agent that resets its turn on a second prompt, as
Codex's adapter does (survey §7), so *now* would cut the turn's work.

**Q5. Accounts.** One provider entry, `Accounts` with `Tables` (§1.3); the door keeps its turn report and its wire's codes. An account that is
a key is `Accounts.Kind = Key` with its own variables, handed only to its own provider's spawn by its door's `ISpawn`, read from where D172
point 3 keeps keys (`profiles/`). A door onto another agent reads that agent's accounts, sign-in, usage question, windows, limit words, weekly
reset and floor, and never inherits `AuthRequiredCode`, a turn report or a program. *Reason:* D210 already reads *own, then owner* in this split
(`AgentErrors.cs:83-91`); the contract makes it the types'. *Rules out:* Claude Code's protocol adapter's `-32000`, *authentication required*
(`AgentErrors.cs:325-344`), read on a door whose server means something else by it, signing out a good account.

**Q6. Models.** A door's offers come from its source: negotiated at the protocol open from `configOptions` and kept per door and version (today
`data/agent-offers.json`, `Accounts/AgentOffers.cs:26-40`; Part B's `agent_offers`), or declared by the provider for a native door (Claude Code's
aliases and effort words, `AgentSettings.cs:48-168`). A choice is carried by the door: the protocol transport's `set_config_option` by category,
or the native spawn's flags (`Adapters.cs:2191`, `:2227`, `:2283`). A refusal is read before any prompt by the protocol transport at the open,
or by the native dialect's `ModelRefused` after a silent turn (`StructuredOutput.cs:219-232`); a reported model is the dialect's `ModelReported`,
unknown where it says none. D205's judge and the start plan (Roles) read `ProviderRegistry.Offers(door, version)` and the door's carrier as
values and name no provider; what a spawn asked (`Sessions/Domain/ActorRun.cs:29-50`, Claude Code's flags and variables read for every agent)
is the door's `Spawn` answering for itself. *Reason:* D205 point 3 carries a model at the session level, per door. *Rules out:* a model carried
by writing the account's settings file or environment, which moves every other session on that account (D205 point 3's rejection).

**Q7. The session's life.** The provider's: how to start (its spawn and the open), resume its own conversation (the negotiated method on the
wire, or its spawn's resume vector), take words (at the next step only where negotiated, *now* only where steering is measured), stop keeping
the conversation, close, and where it keeps a conversation (`IConversationHome`). Course's: whether and when to go on, resume, move, hand over
or carry, with which words (D212 §3), on the account `LineAccount` decides (Agents, D212 §14.2 row 9). SWITCHCTX1's table is the providers'
`IConversationHome`; the carry's copy and read-back stay Context's (D212 §14.2 row 13). The protocol resume written twice (`Loop/Driver.cs:4397-4441`,
`Conversations/Chat/ChatRunner.cs:3045-3173`), the native retry three times and the hand-over's accounts three times (survey §5) become one
each, behind the door. *Reason:* FOUNDATION1 put every *whether* in one decision; only the *how* differs by agent. *Rules out:* a provider that
decides at its limit to resume on another account, a second account rule beside `LineAccount`: LIMITSWITCH1's class (D212).

**Q8. The stream.** §3.

**Q9. Connectors.** One seam, `IServerHanding`, given the servers a start chose (Daoris's connector, the plugins', the browser's) as
`ServerEntry` values and handing them by the door's ability: on the wire (`session/new`'s `mcpServers`, `Acp.cs:1736-1763`), by a file in the
provider's own layout (Claude Code's `--mcp-config` in `.mcp.json`'s shape, `SpawnServers.cs:5-53`, today written for every pipe agent), or
not at all. The session's identity rides in every entry Daoris writes, as the home already does (`KnowledgeConnector.cs:80-106`); the provider
declares whether its servers inherit its environment, which only a repository's own server list relies on. The repository's and the account's
own servers are the provider's `OwnServers` answer, what it reads by itself and how each is left out, measured per door (Claude Code's native
door can be held to Daoris's servers alone, `HolderTerminal.cs:44`; the rest unmeasured), absent reading *cannot be left out, said*.
CONNECTORS1's choices act there and nowhere else. *Reason:* the five copies of the door's test before the helper (`Loop/DriverHelpers.cs:993-1000`
and four more, survey §9) and the helper's own (`SpawnServers.cs:55-68`) all ask one thing, the door's carrier. *Rules out:* a session's identity
trusted to the environment its agent passes on, which Codex does not pass (CONNECTOR2), so its connector would not know the session.

**Q10. Permissions, skills and hooks.** Postures per door, in its own words (§1.3). The marks packed into a posture's string, `!` required and
`?` the person answers (`Acp.cs:326-336`, `:2994-3009`), become `PostureAsked`, a typed value. Claude Code's `IRuleLanguage` renders the
product's rules into its settings file with its hooks and the hard deny, carried by `--settings` on its native door and by
`_meta.claudeCode.options.settings` on its protocol door (`Adapters.cs:1881-1906`, `:2316-2341`); the rules themselves stay in Claude Code's
grammar, which is the product's only rule language (survey §15 tangle 2), named in the census as the product's and not moved, since a rule
model of no agent's would change what the person types, a design of its own (§6). Skills: `ISkillLayout` (Claude Code's plugins by
`--plugin-dir` or the `_meta`; dsh's `.claude/skills`). Where a provider takes no rules, the sheet says absent and the page and Ask Daoris's room
say so from it (COAGENT1 §3.2). The lineage hook stays Claude Code's: its `IChildVocabulary` hands it in its settings; Codex's calls carry their
thread and need none. *Reason:* what an agent takes is a measured fact of its program. *Rules out:* a lineage hook every provider must take,
which Codex and dsh could answer only by claiming a capability they do not have.

**Q11. Process and environment.** A door's `ISpawn` returns a plan: its argument vector and the variables its process takes (its home and key
variables, its pinned environment, its own seams such as `CLAUDE_CODE_EXECUTABLE`, its posture variable, its tools-up-front switch). Daoris's
own variables (`Spawning.Roles`, `Adapters.cs:1659-1665`; the target; the repository and service; the tools' path; the git wiring) come in the
`SpawnRequest` from the executor and are applied after the plan, so no provider sets or keeps one; a plan naming any of them, or any `DAORIS_`
variable, fails the conformance suite. The instruction leaves the adapter: Context composes it (today `TargetPrompt.Compose` inside
`Spawning.InRoot`, `Adapters.cs:1540-1571`), the request carries it as a string, and the door carries it its own way (`DAORIS_TARGET` for every
door as today, `-p` too on Claude Code's native door, `Adapters.cs:2190`; the first prompt on the protocol transport). Containment stays the
launcher's (D201). *Reason:* ENVLEAK1 made the role variables Daoris's. *Rules out:* a provider's plan setting `DAORIS_SESSION_ID`, so a session
calls the connector as another.

**Q12. Errors.** The provider's `Tables` (kinds, statuses, transient sentences; limit, sign-in and refused words) and the door's (its wire's
codes), read by the one `AgentErrors.Read` in D210's order, the door's before the provider's, never inherited the other way. `DoorFailure.Door`
keeps its spellings, `protocol` and `pipe`, through one table of the transport's spelling per record (§4). Codex's provider keeps no table, its
transient errors fatal as today, until AGENTERRORS1's row adds one on a recorded sentence, and the reader of `codexErrorInfo` (survey §12) is that
row's. The two channels become one: every failure is a `Failed` reading on both transports (§3), and the conclusion's limit and sign-out
readers read it; that the native transport's limit then cools its account is AGENTERRORS1c's decided change (§0 item 4), switched on by that
row, never by a move. *Reason:* D210 built one reader; the channels are where it is fed (survey §12, the three callers of `Limited`).
*Rules out:* a reader per provider, so one provider reads access before limit and signs out an account a limit had only paused.

**Q13. The map.** §1.5: the contract and every provider in Agents, one folder per provider under `Agents/Providers/`; the vocabulary in Events;
the server entry and the agent facts' port in Knowledge; the program's spec in Install. Each edge family of the census that touches an agent is
cut by the row §5 names: Install's reading of an install by `ToolSpec` (P5); Knowledge's and Events' readings of agent types by types moved down
(P10, P14, P15); Participants' fold by `IChildVocabulary`, Participants naming Agents downward (P10); the adapters' names of Context, Course,
Roles, Plugins, Work orders, Review and Workplace by the request's values, `DoorInput`, `ModelAsked`, `PostureAsked` and `IProviderSource`
(P2, P7, P11, P12, P13). *Reason:* D212 §14.3, downward only. *Rules out:* the readings declared in Agents, which would leave `Observation`, in
Events, naming them upward for good.

**Q14. Records.** §4.

**Q15. The page, the service and the tools.** The roster's answer (`Modules/Bridge/Routes/AgentsRoutes.cs:123-139`) becomes the sheet, per agent
with its doors, and the page reads every agent fact from it (D182 point 6): a door's agent (`Web/agents/tools.ts:226-316`, `Web/work/say.ts:211-222`,
`Web/work/waits/limitWait.ts:63-64`, `Web/work/head/headFacts.ts:75-86`); the agent's account acts in place of `doors[0]` (`Web/agents/agents.ts:565`,
`AgentPage.tsx:156-158`, `:340-356`); the mark; the windows' names and order (`agents.ts:120-121`, `settings/accounts/accounts.ts:325-329`); the
floor (`AgentsView.tsx:26-27`); the efforts (`settings/ai/Actors.tsx:26-27`); *can be stopped* and the 24,000-character cap from the door's
abilities (`WorkFrame.tsx:2635-2636`, `say.ts:309-320`); *takes rules* (`help/access/grants.ts:13-18`); the hand-over's offer from the driver's
`HandOverAsks` (`work/start/handOverOffer.ts:38-86`). The twins left, each named in the page's census: the sign-in's progress read from each
tool's text (`SignInSteps.tsx:10-14`, `:148-177`), until the driver reads it and answers states, D197's flow unchanged; and the doctrine's layouts
(brief §6). The service knows no agent but in two places: the calls' `_meta` lineage keys (`Core/Sessions/CallLineage.cs:26-80`,
`Mcp/CallLineageFilter.cs:14-22`), a twin of each provider's `IChildVocabulary` call keys, held by one fixture both suites read (`twins.md`); and
the default `claude-code` where an open names no adapter (`Http/Program.cs:1291-1293`, `:1322-1324`, `:1966-1967`), a census exception until
every driver that opens such a record names the door it started. The lineage hook is Claude Code's asset. The tools' sites are the rehearsal's
stand-ins, which read the registry's stand-ins (P2), the proof kit and the probes, which are measurement (survey §2, §9), and the door-to-owner
twin in `tools/live-logins.mjs:34-35`, which reads the driver's answer (P5). *Reason:* D182 point 6, a judgement made once in .NET. *Rules out:*
the page offering `max` effort, which the driver refuses (`AgentSettings.cs:65`, `:188`; survey §4).

**Q16. The disagreements.** Which answer stands, and why; each a dated note on the decision named, in the row that unifies it.

1. **The machine's door** (five places, four rules): the loop's stands, the protocol door where any of Build's choices rides it
   (`Loop/Driver.cs:656-690`), as one function, `Reach.Drives(order, adopted)`, over Build's effective order with the plugins' doors; the window
   (`DriverContext.cs:155-172`), the terminal's two (`Host/SessionsConsole.cs:37-48`, `Host/DriverChoicesConsole.cs:56-71`) and set-up
   (`SetupWorld.cs:16-31`) read it, and the planner is handed its answer. *Why:* Build's order is the machine's choice (D205), and the loop's is
   the rule that starts sessions; the others report it, and a report that disagrees with the act is the defect. Note on D70 (P6).
2. ***A pipe agent cannot drive an unadopted repository*** (five places): D70's rule stands, once: `Reach.Unadopted(door)` is *its servers ride
   the wire*, read by the planner (`Planner.cs:1938-1947`), the walk (`Driver.cs:743-746`), the hand-over's quest (`HandOverQuest.cs:199-205`),
   set-up's press (`SetupPress.cs:76-82`) and the page through the answer. Whether a door that hands servers by file may drive an unadopted
   repository with the connector handed by file (§0 item 6) is CONNECTORS1's question, not this design's (P6).
3. ***It resumes its own conversation***: `DriverContinuations.ResumesOwn`, judged on the record's own door, stands; `Trees/SessionGroups.cs:975-983`'s
   reading of the machine's adapter goes. *Why:* a continuation keeps its record's agent, never the machine's (D205 point 1, D212 §2.4). Note on
   D205 (P11).
4. **A stand-in told apart by binary or by name; the stand-ins borrowing Claude Code's tables**: `Identity.StandIn`, declared, answers
   `AgentsCommand.cs:653-658`, `DriverChoicesCommand.cs:2195-2197` and `BrainstormSynthesis.cs:9-13` alike. `stub` and `acp-stub` declare that they
   speak Claude Code's vocabulary, by reference to its tables and dialects (and `acp-stub` Codex's dialect too), never by copy, so a rehearsal of
   Claude Code's limit reads Claude Code's words as they are; `alt-stub` speaks its own, so a path that reads one agent's vocabulary for all fails
   on it. Note on D46's stand-in (P2, P3).
5. **The efforts offered**: the driver's stand (the door's declared or negotiated efforts; `AgentSettings.cs:65`, `:188` refuse `max`), and the
   page reads them. Note on D205 (P12).
6. **The read floor and the token weights**: the floor is the provider's (`HarnessRoster.cs:2107-2134`, a minute for Claude Code, five for
   Codex), read by `SwitchDecision` (`SwitchDecision.cs:70-71`) and by the page (`AgentsView.tsx:26-27`); a candidate's reading cannot be fresher
   than its provider's floor lets the roster ask, so a Codex candidate read *first* inside five minutes asks for a read the roster's own floor
   declines (read, not run). The weights stay D179 point 3's product default until USAGECAP1b, written in the contract's usage part as D179's
   and not as Claude Code's (§0 item 4). Note on D130 and D179 (P4).
7. **The Claude Code seam on every protocol door's spawn**: `CLAUDE_CODE_EXECUTABLE` and `DISABLE_UPDATES` from Claude Code's pin
   (`Adapters.cs:2132-2153`; `HarnessRoster.cs:1303-1308`; `HarnessSelection.cs:562-565`, `:603-617`; `Driver.cs:2996-3010`;
   `ChatRunner.cs:1483-1498`) go on Claude Code's doors alone, by its `ISpawn`. *Why:* they name Claude Code's binary to Claude Code's adapter.
   Codex's and dsh's spawns lose two variables nothing shows they read (P7 measures it on the install). Note on ACP2's decision, D57 (P7).
8. **Declared fields nothing reads**: `ProfileMustExist` is retired, since the home is always made (`HarnessSelection.cs:567-580`), which
   satisfies the agent that demands it; `WeekFixed` is the provider's and read by its one consumer through the registry (§0 item 5). Note on
   D57 (P3, P4).

**Q17. The names.**

| Concept | In code | In prose | On the window, en and zh | Retires |
|---|---|---|---|---|
| provider | `Provider`, `ProviderRegistry` | *provider* | never: the window says *agent*, 智能体, by its product's name kept as written | *harness* in new code (the glossary's code word, `glossary.json:595`) |
| agent | a provider's `Id`; `agent` in the records that name one | *agent* | *agent*, 智能体 | — |
| door | `AgentDoor`, its id the records' `adapter` | *door*: a way in, one meaning in new code (as `HandOverDoor` and `TurnBinding.Door` already mean) | *way in*, 接入方式 | `ISessionAdapter`, `AdapterSet`, `HarnessToolchain` (P16); *adapter* in prose |
| transport | `Transport.Native`, `Transport.Protocol` | *the native transport*, *the protocol transport* | *directly*, 直连; *over the protocol*, 协议 | `SessionWire` (read by unmoved callers until P6, renamed at Agents' namespace row); *pipe* for a door in new code |
| dialect | `INativeDialect`, `IProtocolDialect` | *dialect* | never | `IStreamMapper` (P10) |
| reading | `WireReading` | *reading* | never | `StreamMapped`'s extra fields; `AcpUsage`, named for one door (survey §8), as the turn's context in Events |
| stand-in | `Identity.StandIn` | *stand-in* | never | the literal ids in `BrainstormSynthesis` |

*Pipe* keeps one meaning in new code, the terminal's named pipe (`Platform/PipeName`); the records keep their `pipe` (§4). Nothing new reaches
the window, so the glossary gains no entry (D193's rejection; brief §5's *simple*); a UI door (the glossary's `door` and `opens` table,
`glossary.json:434-471`) is the page's own word and unchanged.

**Q18. The order.** §5. How each input of the brief §3 folds in: **FOUNDATION1** is absorbed as the ground (the map, the `Binding`, the start
plan, admission, the event envelope, the store), and the contract's rows are where its Agents edge families are cut, once (§1.5, §5);
**MODELROLE1** (D205) is carried as each door's offers and carrier (P12); **AGENTERRORS1** (D210) is absorbed as it stands, its reader one and
its tables re-cut into the provider's and the door's (P4, P8), its row 1c built on the one channel (P10); **COAGENT1** (D199) is re-cut: its sheet
a read of the contract (P15 is COAGENT1e) and its §3.4 carried whole (Q4); **COWORKVIEW1** is carried: a child's announcement and end as readings
and the provider's vocabulary (P10), the session's token in the entry (P14), row a's `start` and `end` lines written by the sink; **SWITCHCTX1**'s
table of identities is the providers' `IConversationHome` (P11); **CONNECTORS1**'s handing seam is `IServerHanding` (P14); **OPENROUTER1** is built
on the contract after the parts it needs (O1 to O3).

**Q19. What is the owner's.** §6.

**Q20. Proof.** Per row, in §5.

## 3. The normalized event stream

### 3.1 The vocabulary and its sink

A provider says what happened as **readings**, `WireReading`, a closed set of records declared in Events beside `SessionEvent`, which it extends
rather than replaces: `Said` carries a `SessionEvent` of today's kinds (`SessionEvents.cs:13-55`). Each transport reads its wire, hands its
door's dialect every frame the transport does not know, and writes each reading to the run's **`IReadingSink`**, which the executor composes (the
look's for a driven run, the chat runner's for a conversation): Context's writer keeps the event file and the consoles, Participants' keeps the
lineage, Agents keeps the binding's conversation, the account's readings and the usage history, and the hub of the process that runs the work
tells FOUNDATION1's machine events from what was kept (D212 §4.1). **The provider mints nothing**, and nothing a provider says leaves the
machine but as an event's ids and codes (D212 §4.4). **What a provider does not say is never emitted**, and every reader keeps it unknown (D173's
guard). Until Part B's event rows (1k, 1l, 1m) and the hubs land, the sink writes today's stores and calls today's channels, so the readings change
no event a reader sees; once they land, the hubs and triggers mint from the same stores.

### 3.2 Mapped onto FOUNDATION1's codes and today's kinds

| Reading | Its facts | Read today at | The sink keeps it in | Minted as (D212 §4.2) | Unknown where |
|---|---|---|---|---|---|
| `Said` | a `SessionEvent`: user, message, thought, tool, plan, usage, raw, receipt | both doors' maps (`Acp.cs:3501-3642`; `StructuredOutput.cs:80-177`, `:447-538`) | the event file (Context) | none: the event file is a store (§4.3) | — |
| `TurnEnded` | the stop reason in the protocol's words, the tokens, the split per model, the context | `Acp.cs:2183-2197`, `:2138-2160`, `:3048-3053`; `StructuredOutput.cs:309-370`, `:426-445` | the event file's `turn`; `UsageHistory.Turned` | `turn.answered` for a conversation's turn, keyed by the run and the moment (§4.5) | a count the wire did not say, null, never zero (TOOL3) |
| `ConversationNamed` | the agent's conversation id | `Acp.cs:1666-1667`; `StructuredOutput.cs:130-134`, `HarnessCapture.cs:124-127` | the binding (`HarnessConversations`, then Part B's `bindings`) | none | a wire that names none |
| `ModelReported` | the model a turn ran | `Acp.cs:3042-3046`; `StructuredOutput.cs:118-127` | D205 point 10's actor record; the turn's models | none (this machine's) | Codex, dsh, the stubs: unknown |
| `ModelRefused` | the model asked | the protocol open (`Acp.cs:165-173`, `:680-722`); `StructuredOutput.cs:219-232` | the start's re-ask (the start plan, D212 §2.4) | `part.ended`, class `refused-choice`, by the record's trigger | — |
| `WindowsSaid` | an account's windows, read by the provider's window words | `Acp.cs:3030`; `StreamMapped.Limits` (`StructuredOutput.cs:15`) | `windows.json` (Agents) | none (the usage slice's state) | an agent that says none |
| `Failed` | a `DoorFailure` | the protocol's `turnFailed`, `HarnessEnding.Failure` (survey §12) | read by `AgentErrors.Read` into cooling, sign-out, the transient episode or the conclusion | a limit: `part.limited` by the record's trigger and `account.waits` by the hub; the end's class `limited`, `access`, `transient-bound` or `failed` | — |
| `TransientTry` | a try's number, a wait's end | `Acp.cs:1106-1351` | the event file's `transient` (D210 §5.1) | none | — |
| `ChildSeen` | a child's native id, its parent, its type, the signal | `AcpStreams.cs:77-103`; `ClaudeStreams.cs:95-167`; `ChildBindings.cs:293-323` | the lineage's `stream` line (Participants); the child's console stream | `child.seen`, by the hub | its parent and depth where no harness said |
| `ChildSaid` | a child's id and a `SessionEvent` | `AcpStreams.cs:152-204`; `ClaudeStreams.cs:73-81` | the child's console stream, never the record | none | — |
| `ChildEnded` | a child's id, finished or killed | `AcpStreams.cs:309-323`; `ClaudeStreams.cs:262-275` | the lineage's line for that child | `child.ended` | Codex says none: the fold ends the child with its part, `ended` or `lost`, never `finished` (`ChildBindings.cs:52-56`) |
| `TaskStarted`, `TaskEnded` | a background task's id, kind and output | `AcpBackground.cs:42-79`; `ClaudeStreams.cs:27`, `:46-51` | the running and long calls (`Sessions/Heavy/LongCalls.cs:14-21`); the console | none | on the native transport the turn ends its work (`Observation.cs:148-157`) |
| `Woken` | a turn the harness woke | `AcpWake.cs:60-102`, `:216-230` | the turn's queue | none | — |
| `RequestHeld` | a permission request held for the person | `Acp.cs:2757-2910` | `HeldPermissions` (Events) | `call.held` | — |
| `CallRefused` | a call the agent or the person refused | `Acp.cs:2694-2755`; `StructuredOutput.cs:372-408` | the event file (the tool marked refused) | none | — |
| `Outside` | a line the harness wrote itself | `StreamMapped.Outside`; stderr | the console; read for access alone (`DoorWhere.Harness`, `AgentErrors.cs:144-152`) | none | — |
| `Ended` | the exit, the run's own end of its stream, background cut | `HarnessEnding`; `AcpEnded.cs:11-15`; `AcpEnding.cs:41-57` | the conclusion (Loop, Course), then the record | `part.ended`, by the record's trigger | — |

`part.quiet` is the watch's, never a provider's. The four parsings of Claude Code's `stream-json` (the mapper, its streams, the lineage's,
`Observation.cs:564-676`'s `HarnessWords`; survey §15 tangle 2) become one, Claude Code's native dialect, and the other three read readings. A
child's card kind, `think` on one door and `other` on the other (`AcpStreams.cs:36-37`; `StructuredOutput.cs:491-500`), is the reading's one kind.

### 3.3 Where each agent's wire is read

| Folder, after mod-ix | Takes | Reads |
|---|---|---|
| `Agents/Providers/ClaudeCode/` | `ClaudeStreamJson` and `ClaudeStreams` (its native dialect); a protocol dialect from `Acp.cs`'s `_claude/` and `claudeCode` reads, `AcpWake.cs`, `AcpBackground.cs` and the subagent frames of `AcpStreams.cs`; `ClaudeUsage`; its window, limit, sign-in and error words (`AccountReadings.cs:107-183`, `:299-335`; `AccountLimits.cs:197-208`, `:296-328`; `ClaudeErrors`, `ClaudeAcpErrors`); `ClaudeTrust`; the rules' rendering; the plugins' layout; the settings' schema; the conversation's layout; the lineage hook; `ClaudeReleaseDownloads`' channel as a spec | `stream-json`; the adapter's `_meta`; the hook's input; its home's files |
| `Agents/Providers/Codex/` | `CodexAccounts` (the holder's toolchain as the provider's program and accounts); `CodexUsage`; its limit words (`AccountLimits.cs:330-361`); `SignInPorts`; a protocol dialect reading `thread_active_writer` | the app server's answers; the adapter's errors |
| `Agents/Providers/Dsh/` | `DshProfile` (its telemetry patch and skills); its posture variable | its profile file |
| `Agents/Providers/Stub/` | `stub`, `acp-stub`; their vocabulary by reference (§2, Q16 item 4) | Claude Code's and Codex's, as the rehearsal plays them |
| `Agents/Providers/AltStub/` | its own words, its lines, its `_meta` | its own program's |

The shared transports read the spec's frames and the plain lines alone. The service's reading of the calls' `_meta` (§2, Q15) is the one wire
read outside the driver's providers, a twin held by a fixture.

## 4. Records and older builds

No record changes shape in this design (brief §10 point 4: a record's agent or door field changes only after every reader reads it through
the contract, and none needs to). P1 writes the table below into `Agents/Registry/RecordIds.cs`, each field with the kind of id it holds, so
the registry resolves it by its kind and no reader guesses.

| Record (survey §14) | Field | Holds | After | An older build |
|---|---|---|---|---|
| The session record | `Adapter`, `HarnessVersion`; `Profile` | a door's id, its program's version; the account | unchanged; the agent derived through the registry; `Adapter` and `HarnessVersion` still travel, `Profile` still does not | unchanged |
| The actor asked and ran (D205 point 10) | `agent` | a door's id | unchanged | unchanged |
| `<s>.harness.json`, then Part B's `bindings` | `adapter`, `conversation` | the door that named the conversation | unchanged, the binding's door column | unchanged; z-iii's import as D212 §13.6 says |
| Ask Daoris's `<id>.wired.json` | `harness`; `door` | a door's id; `pipe` or `acp` | unchanged, the spelling written from the transport's spelling table | unchanged |
| Note codes | `resumed.native`; `gone`, `exited`, `elsewhere` | the reasons a resume fails | unchanged: `gone` from the spec's refusal, `exited` from the native transport, `elsewhere` from Codex's dialect | unchanged |
| `SessionEnded`, the usage history | `Adapter`; `TurnBinding(Agent, Account, Door, Meaning)` | a door; an agent and a door | unchanged; `Agent` from `ProviderOf`, `Door` the door | unchanged |
| The offers, then `agent_offers` | per door's id and version | a door | unchanged | unchanged |
| The machine log | `adapter`; `agent`; the failure's `door` | a door; a door; `protocol` or `pipe` | unchanged, the spelling from the table | unchanged |
| An opinion's state; a brainstorm's and a meeting's files | `adapter`, `maker`, `stubs` | a door, a maker, stand-ins | unchanged; `stubs` judged by `StandIn` | unchanged |
| A hand-over's, a helper's, a partner's ask, a go-on mark | `agent` | a door's id | unchanged | unchanged |
| A trust hold | the trust file's path | Claude Code's | unchanged, from the provider's `Accounts` | unchanged |
| `driver.json` | `adapter`, `intakeAdapter`, `helperAdapter`, `commands`, `actors[].agent`; the *questions* pause's `claude-code` (`AccessRules.cs:117-127`) | doors' ids; an agent's id as a setting's value | unchanged; read through the registry | unchanged |
| The pins, then `harness_wiring` | versions by id (`harnesses.json`) | a program's owner: the agent's id for its own binary, the door's for a door's package | unchanged; `ToolSpec` carries its key | unchanged |
| The account homes, cooling, windows | `profiles/agents/<agent>/`; by agent | an agent's id | unchanged | unchanged |
| The lineage | `harness` | an agent's id (`ChildBindings.cs:11`) | unchanged; the fold resolves the provider by it | unchanged |

**What an older build meets.** Nothing it wrote is read differently, and nothing this design writes is in a shape it reads differently. New ids
are the only new strings: `alt-stub`'s, on a rehearsal's home and listed nowhere without a configured command (`HarnessRoster.cs:863-867`), and
OpenRouter's, whose design measures its own case. An older build that meets a record naming a door it lacks refuses to continue it, naming what
exists (D23), as it does today for a removed plugin's door; an account folder naming an agent it lacks is passed over (POOLSTRAY1,
`Adapters.cs:2469-2482`). COAGENT1e's negotiated readings, if kept on disk, go in a shape no older build reads, a new table with every column
required (`compatible-builds.md`), and lost they read *negotiated, not yet read*. The proof is P16's: every value a shipped build writes in each
row above resolves through the registry, held against fixtures of those builds' own writes.

## 5. The rows, in build order after mod-ix

Each row is its own branch, after mod-ix and on files no open branch holds (D212 §14.6), lands with every gate of `daoris.gates.json` green and
the brief §9's suites for its part run whole, and leaves Daoris as it was for a person who sets nothing, but where its *Changes* says a decided
change. **P1 is first because it is a day and changes nothing, and it makes every later row measurable**: the registry over today's adapters
answers every question the contract asks, and the census names every site a later row must move. **P2 is second because the stand-in is the
contract's meter**: added as a provider of the contract alone, every case it cannot pass yet is a named gap with the row that closes it, and the
gaps reach zero as the rows land. **P3 is third because it is the most copied fact and the first OPENROUTER1 needs**: the owner, resolved in
four driver copies with four rules (survey §15 tangle 3), an account kind being OpenRouter's first part.

### P1. The contract named, the registry over today's adapters, the census. No behaviour. A day.

- **Code.** `Agents/Contract/`: `Provider`, `AgentDoor`, `Transport`, `Known<T>`, the parts of §1.3 as records (`Identity`, `AccountFacts`,
  `ProviderTables`, `Postures`, `DoorAbilities`, `DoorTables`); no port yet. `Agents/Registry/ProviderRegistry.cs`: `ProviderRegistry.Of(AdapterSet)`
  groups today's adapters by their owner, folds the `codex` holder in as Codex's provider, and answers `Providers`, `Door(id)`, `Provider(id)`,
  `ProviderOf(door)`, `Reserved` and `Resolve(id, kind)`; `RecordIds.cs`, §4's table. Nothing reads them but their tests.
- **The census.** `Driver.Tests/Agents/ProviderCensusTests.cs` with `fixtures/provider-census.json`, scanning the driver, the host, the modules and
  the service's three projects, by Roslyn's semantic model as mod-0's structure gate does, for: an agent's or a door's id as a string literal; a
  type declared in a provider's folder (today: the adapter classes, `ClaudeStreamJson`, `ClaudeStreams`, `ClaudeUsage`, `CodexUsage`,
  `CodexAccounts`, `ClaudeTrust`, `ClaudeErrors`, `ClaudeAcpErrors`, `DshProfile`, …); a provider's variable or file named as a literal (`CLAUDE_CONFIG_DIR`,
  `CODEX_HOME`, `DSH_HOME`, `ANTHROPIC_API_KEY`, `ANTHROPIC_MODEL`, `CLAUDE_CODE_EFFORT_LEVEL`, `CLAUDE_CODE_EXECUTABLE`, `DISABLE_UPDATES`,
  `ENABLE_TOOL_SEARCH`, `.claude.json`, `.mcp.json`, …, the list read from the providers' declarations so it grows with them); and a reference to
  `SessionWire` or a door's spelling (`"pipe"`, `"acp"`, `"protocol"`). Every site the survey lists is an exception, file and token, with the row
  that moves it; the permanent ones carry their decision instead: D23's supported default (`Platform/DriverConfig.cs:70-75`), D209's holder
  (`HolderTerminal.cs:31-55`, whose argument vector D209 states whole and the walk checks), D72's rule grammar (§2, Q10), the terminal's named pipe
  (`Platform/DaorisHome.cs:432`, `Modules/Platform/InstancePipe.cs:108`). A new site fails. The page's census, `Web/agents/providerCensus.test.ts`,
  reads the fixture's `web` section the same way, the fixture named to the merge tool (`twins.md`'s REACH rows).
- **Changes.** None.
- **Proof.** Unit: the registry over `AdapterSet.Built()` with a plugin fixture (a harness with `accountOf` and one without) answers, for every
  door, the owner `Toolchain.Owner` gives and, for every fact the four copies answer (limits, sign-in, windows, usage question, login question,
  sign-in command, reporting door, readiness: `HarnessRoster.cs:120-197`, `:782-841`, `:1694-1715`, `:2082-2098`), a table of today's answers,
  `fixtures/provider-owner.json`, recorded in the same commit, each cell where two copies disagree listed as P3's decision; every id of §4 resolves
  by its kind, and an unknown one is D23's error naming what exists. Census: it passes at the row's commit, and fails on a planted `"codex-acp"`
  literal in a Loop file, a planted `SessionWire.Acp` test in a Course file, a planted `ClaudeStreams` reference in a Participants file and a
  planted `CODEX_HOME` in the modules. Structure: `StructureTests` whole, the contract naming nothing above Agents.

### P2. The shim, the registration list, the conformance suite, and the third stand-in. No behaviour for a person.

- **Code.** The ports a provider of the contract alone needs: `ISpawn`, `INativeDialect`, `IProtocolDialect` (declared, read from P8),
  `IUsageQuestion`, `IChildVocabulary`, `IConversationHome`. `Agents/Registry/Providers.cs`: `Providers.Built()`, **the one registration
  list**, holding providers written on the contract (here only `alt-stub`); `Agents/Registry/LegacyDoor.cs`, the shim that presents a contract
  door to today's readers as an `ISessionAdapter` with its `HarnessToolchain` and its dialect as an `IStreamMapper`; `AdapterSet.Built()` takes
  `Providers.Built()`'s doors through it, once, so a provider added later touches no adapter file. The plugins' `IProviderSource` replaces
  `WithPlugins`, Plugins implementing it and Loop composing it, cutting *Agents names Plugins*.
- **The stand-in.** `Agents/Providers/AltStub/`: the provider `alt-stub`, `StandIn`, its home variable, its sign-in and login question in its
  own words, its limit, window, sign-in and error words in a grammar no real agent uses, each on a sentence its program prints, its usage
  question, its children's frames, its conversation home, its postures, and two doors: `alt-stub`, native, its own line format, its resume by its
  own flag; `alt-acp-stub`, protocol, its own `_meta` keys for its model, usage, windows and children. Its program is one file,
  `tools/stand-ins/alt-stub.mjs`, both doors by an argument, the binary it would ship as.
- **The conformance suite.** `Driver.Tests/Agents/ProviderConformanceTests.cs`, a theory over every provider and door the registry holds: its
  identity's rules (§1.2); every part stated; a full or asking start with none mapped refused naming it; its spawn's plan setting no role and no
  `DAORIS_` variable, and its key variables only in its own spawn; each word table's patterns standing on a recorded sentence and reading it
  (`AgentErrorsTests`' rule, generalized); its dialect over its recorded frames (`fixtures/providers/<agent>/`: Claude Code's and Codex's from the
  frames today's tests already hold, `ClaudeStreamJsonTests`, `ClaudeAcpTests`, `AcpTests`, `AcpResumeTests`) giving the readings expected and
  emitting nothing it was not told; for a stand-in, its program run live: a start, a turn, a resume, a limit read as a limit, an error read by its
  kind, a child seen and ended, its usage and its model, a sign-in, a pin, a listing with a command configured. `fixtures/provider-pending.json`
  holds each case a provider cannot pass yet with the row that makes it pass; a pending case that passes fails the suite until its line goes.
- **Changes.** None: `alt-stub` is listed nowhere without a configured command, as `stub` is not (`HarnessRoster.cs:863-867`); the plugins'
  harnesses register as before.
- **Proof.** The conformance suite with the pending table: at this row `alt-stub` passes its start, turn, resume, error, sign-in and listing on
  both doors and its usage and model on the native one, and is pending its windows and limit grammar (P4), its pin (P5), its protocol `_meta`
  (P8), its children in the lineage and the one failure channel (P10), its conversation home (P11); the built-in providers are pending their spawn
  plans (P7). The family rehearsal's stub phases run a third pair, `alt-stub` and `alt-acp-stub`, `tools/family-rehearsal.mjs` taking its
  stand-ins from the registry's `StandIn` providers, once, so no later stand-in changes it (real processes, serially). `PluginCatalogTests` holds
  the reserved names as `fixtures/reserved-harnesses.json`'s shipped list and, beyond it, every id the registry holds, so a provider is reserved by
  its registration alone. The census holds no `alt-stub` site: no file outside its folder and its program names its id, its types or its
  variables. The diff: outside the contract's own files, the stand-in's folder, its program and its one line in `Providers.Built()`.

### P3. The owner, once. No behaviour.

- **Code.** `ProviderRegistry.ProviderOf(door)` is the one reader of the agent a door runs as. Every read of `AccountOf`, `Owner(...)` and
  `AdapterSet.Holder` reads it: `HarnessRoster.cs:66-72`, `:114`, `:120-144`, `:188-197`, `:427-462`, `:476-498`, `:766-780`, `:782-841`,
  `:1694-1715`, `:1987-1999`, `:2082-2098`; `AgentErrors.cs:249-261`; `UsageTurns.cs:23-29`; `Agents/AgentsCommand.cs:624-642`;
  `Agents/AgentAccountsCommand.cs:668-674`, `:700-704`, `:711-721`; `Agents/AgentListCommand.cs:110-111`; `Agents/AgentLoginCommand.cs:129-147`;
  the hand-over's three (`Loop/DriverHandOvers.cs:353-365`, `Conversations/Chat/ChatHandOver.cs:203-217`, `Conversations/HandOver/HandOverQuest.cs:73-87`);
  the modules' `AccountsRoutes.cs:48-75`, `AgentsRoutes.cs:206-216` and `AgentActions.cs:212-236`. `Agents/Providers/Codex/` holds the Codex
  provider: agent `codex` from `CodexAccounts.cs:30-53`, door `codex-acp` from `Adapters.cs:2063-2129`, with Codex's limit words and usage
  question moved from the door to the agent (`Adapters.cs:2122-2125`); `AdapterSet`'s `holders` and `Holder` go. `claude-code-acp`'s product,
  maker, mark and trust file are read from its provider (`Adapters.cs:1919-1945`), its program, turn report and `ClaudeAcpErrors` its own.
  `ProfileMustExist` is retired (Q16 item 8).
- **Changes.** None, each by a decision where the copies disagree: the cells P1's table listed, each answered as the provider's fact (the
  holder-blind copies answer the same today only because `codex-acp` repeats Codex's facts on itself, survey §3); a dated note on D57 for
  `ProfileMustExist`.
- **Proof.** Unit: `fixtures/provider-owner.json`'s every cell equal after the change; `CodexAccountTests`, `HarnessTests`,
  `AccountRotationGoalTests`, `AccountRotationWalkTests`, `AccountRotationTickTests`, `ResumeAccountTests`, `AgentsCommandTests`, `AgentErrorsTests`,
  `HandOverTests` whole, and the modules' tests. Census: every owner site gone from the fixture. Conformance: `alt-stub`'s account cases (a door
  signing in through its provider, a cool-off kept under the provider) pass on both doors. The reserved names keep `codex`, now a provider's id.
  Process: `LimitWaitsProcessTests` and `ChatGoesOnProcessTests` whole (a limit on `claude-code-acp` cools a `claude-code` account as today).

### P4 to P16

| Row | What it moves behind the contract | Changes for a person | Proof | Folds in |
|---|---|---|---|---|
| **P4** the accounts' readers | `UsageKind`'s switch to `IUsageQuestion` (`ClaudeUsage`, `CodexUsage` into their folders, `HarnessRoster.cs:2287-2360`); each provider's floor; its window words (`AccountReadings.cs:107-183`, `:299-335`), its limit grammar (`AccountLimits.cs:121-125`, `:197-208`, `:296-361`), its sign-in, refused and error words; `ClaudeTrust` into Claude Code's folder; the key's variable read once (`AgentLongCommand.cs:117-122`); `ToolHomeLeftovers`' homes from the providers (`:41-47`); `WeekFixed` read through the registry | none; `SwitchDecision` reads the candidate's provider floor (Q16 item 6), a note on D130 and D179 | the usage and limit readers' tests, `AccountReadingsTests`, `AccountRotationTickTests`, `AgentErrorsTests` whole; `alt-stub`'s windows and limit grammar un-pend; census shrinks by survey §3 | AGENTERRORS1's tables split provider and door |
| **P5** the program | `ToolSpec` down to Install, cutting *Install names Agents*; each provider's and door's program as a spec; the channels declared (`Setup/ReleaseChannels.cs:25-80`'s switch on ids gone; Claude Code's signed bucket a spec, its verified hash a channel's property, `ClaudeReleaseDownloads.cs:130-132`); `AgentPinCommand`'s second copy of pin, update and newest (`:69-325`) onto `HarnessActions`; `tools/live-logins.mjs:34-35` reading the driver's answer | none | `ToolInstallTests`, `ToolResourcesTests`, `HarnessTests`, the pin's and update's suites whole; the deployment rehearsal; `alt-stub` pinned from the registry fixture the install tests serve, else its pin stays pending, named | — |
| **P6** the doors as abilities | every `Wire` test in a caller (survey §6, §9; §0 item 7's 56 reads) read as a door's ability; `Reach.Drives` and `Reach.Unadopted` (Q16 items 1 and 2); `Resumes`, `Interactive`, *structured*, *takes rules*, *hands servers* from `DoorAbilities` | a window, the terminal and set-up say an unadopted repository can be driven where Build's later choice is a protocol agent, as the loop already starts it; notes on D70 | `PlannerTests`, `ContinuationTests`, `HandOverTests` and the set-up suites whole; a new case table asking `Reach.Drives` and the window's, the terminal's and set-up's readers the loop's cases (none tests `DriverContext.DoorOf` or `SetupWorld.DoorOf` today); census without `SessionWire` outside the transports and providers | D70; D205's order |
| **P7** the spawn | `ISpawn` per door; `SpawnRequest` with Daoris's values applied last, the composed target, the role variables, the git wiring and the review's environment handed in, cutting the spawn rows of *Agents names Context*, *Work orders*, *Review*, *Workplace*, *Roles* and *Participants*; the Claude seam on Claude Code's doors alone (Q16 item 7); `DshProfile` into its folder; the maker's attribution into `Identity` (`Adapters.cs:383-431`); `HolderTerminal`'s program, path and hash through the registry, its vector D209's | none: two variables leave Codex's and dsh's spawns; note on D57 | `PermissionSpawnTests`, `EveryChildIsHandedTheToolsTests`, the adapters' tests whole; conformance's spawn cases un-pend for every provider (no role variable, keys only in their own spawn); the environment of each built-in door's spawn compared, variable for variable, with today's but the two; install: a Codex and a dsh session start as before, the parent's | ENVLEAK1 |
| **P8** the protocol dialect | each per-agent read of `AcpSession` behind `IProtocolDialect` (§1.4), every protocol door on today's composite (Claude Code's and Codex's readers together), so nothing changes; the error shapes per dialect (`Acp.cs:175-239`); the retry's policy typed on `DoorFailure` (`TransientRetry.cs:3-31`); `alt-acp-stub`'s own dialect | none | `AcpTests`, `AcpBackgroundTests`, `AcpSteerTests`, `AcpTransientTests`, `AcpResumeTests`, `ClaudeAcpTests`, `ChatTurnsTests` and every tick test that plays Claude Code's or Codex's `_meta` (§0 item 3) whole; `alt-acp-stub`'s model, usage and windows un-pend; census without `_claude/`, `claudeCode` or `thread_active_writer` outside the providers | COAGENT1's negotiated rows |
| **P9** ⚖ each door reads its own dialect | `claude-code-acp` Claude Code's, `codex-acp` Codex's, `acp-stub` both, dsh and a plugin's door the spec's; `jetbrains.air` sent to Claude Code's door alone (`Acp.cs:1789`) | none on the built-in agents, each reading what it emits; a plugin's door reads no agent's extension; a dated note on D53 (a configuration of the door reads its own extensions) | P8's suites; dsh's recorded frames (`tools/dsh-probes`) read the same; install: a Codex session opens, resumes, is refused `elsewhere` and ends as before with no `jetbrains.air` sent, a Claude Code session queues, steers and reads its windows as before, the parent's; **both makers** | D53 |
| **P10** the stream | `WireReading` in Events and `IReadingSink` (§3); both transports write it; the four `stream-json` readers one; `ChildBindings`' branches per harness into the providers' `IChildVocabulary`; `HarnessEnding`'s fields as readings; every failure one `Failed` reading, the native transport's limit still cooling nothing until AGENTERRORS1c switches its reader (Q12); cuts *Events names Agents*, the capture rows of *Agents names Context* and the lineage rows of *Agents names Participants* | none | `NativeStreamsTests`, `ClaudeStreamJsonTests`, `ChildLineageTests`, `LineageHookProcessTests`, `ChatGoesOnProcessTests`, `LimitWaitsProcessTests`, the service's `CallLineageTests` whole; the event file of a fixture session byte for byte as before on each door; `alt-stub`'s children reach the lineage, its failure one channel; install: a console and a session's events as before, the parent's | AGENTERRORS1c; D212's row 1g; COWORKVIEW1's row a |
| **P11** the session's life | start, resume, words, stop and close through the door; the protocol resume once for a driven run and a conversation; the native retry once; the hand-over's accounts once; `ResumesOwn` one rule (Q16 item 3); `IConversationHome` (SWITCHCTX1's table); `DoorInput`, cutting *Agents names Course* | a group's report of a resume agrees with what the continuation does; note on D205 | `ContinuationTests`, `ChatGoOnTests`, `ChatGoesOnProcessTests`, `ChatTransientProcessTests`, `HandOverTests`, `AcpResumeTests`, `LimitResumeTickTests` whole; `alt-stub`'s conversation home un-pends; install: a session on each protocol door resumes its own conversation and ends as before, its records read by the build before, the parent's | SWITCHCTX1's carry |
| **P12** models | the providers' `Models`; `AgentSettings`' Claude Code schema into its folder; `ActorRun`'s asked from the door's spawn; offers through the registry; `ModelAsked`, cutting *Agents names Roles*; the efforts on the page from the driver (Q16 item 5) | `max` no longer offered where it would be refused; note on D205 | `ActorChoicesTests`, the actor run's and offers' tests, the page's actors' tests; a refused model passes to the next choice before any prompt on each door (brief §9) | D205 |
| **P13** ⚖ permissions, skills and hooks | Claude Code's `IRuleLanguage` (the settings file, hooks, hard deny, `mcp__` names: `Permissions.cs:105-143`, `:487-588`); `ISkillLayout`; `PostureAsked`, cutting *Agents names Help*; the trust preflight through the provider (`Driver.cs:2282-2303`, `DriverIntakes.cs:262-272`, `DriverRoutes.cs:367-398`); the lineage hook as Claude Code's asset; the rooms' Claude Code files named as the doctrine's (brief §6) | none | `PermissionRulesTests`, `PermissionSpawnTests`, `PermissionSeamTests`, `AgentsTextTests`, `McpToolsTests` whole; a full or asking start on a provider with none mapped refused, naming it (brief §9); **both makers**, since it moves postures and rules | D183, D184, D72 |
| **P14** connectors | `IServerHanding`; `ServerEntry` down to Knowledge; the five callers' door tests and the helper's own gone (survey §9); the providers' `OwnServers`, absent but Claude Code's native door's, measured | none | `McpToolsTests`, `EveryChildIsHandedTheToolsTests`, the helpers', reviewers', partners' and intakes' starts whole; the entries each door hands compared with today's | CONNECTORS1's one place |
| **P15** the page, the service and the tools | the roster's answer as the sheet (COAGENT1e built as this read); the page's twins of Q15 reading it; `IAgentFacts`, cutting Knowledge's room rows; the service's call keys twin and its default's exception | the leading agent counted by agent, not by door (`headFacts.ts:75-86`), so a move between one agent's two doors reads as one | `AgentsCommandTests`, the modules' roster tests, web-unit with the page's census; the service's `CallLineageTests` and its twin fixture; translation parity in both catalogues | D182 point 6; COAGENT1e |
| **P16** the end of the old seam | `ISessionAdapter`, `HarnessToolchain`, `SessionWire` and `AdapterSet` gone once nothing reads them; every built-in in `Providers.Built()`; §4's ids held against older builds' writes | none | the census's provider exceptions none but the permanent ones; the pending table empty; every value of §4 a shipped build writes resolves; the family and deployment rehearsals whole; install: the parent's | — |

**Proof for the person's eye** (brief §9, before the row that could break it): a session on each provider starts, takes a turn, resumes its
own conversation and ends as today (P7, P10, P11); a limit cools the account it ran as and never strikes (P3, P4, P10); a transient error is tried
again in the same conversation (P8); a refused model passes to the next choice before any prompt (P12); a full or asking start on a provider with
none mapped is refused (P13); a record an older build holds still reads (P16).

**Beside FOUNDATION1's rows.** The edge families of §1.5 are cut once, by the contract's row named there, never again as a FOUNDATION1 cut;
the synthesis files each where it falls. Part B's 1j (the start plan) reads the registry for a door's offers and accounts; 1z-iii and 1z-vii
keep the ids of §4; 1g and AGENTERRORS1c land on P10's one channel. P rows touching `Loop/Driver.cs`, `Conversations/Chat/ChatRunner.cs`,
Course's or Participants' files wait for no mod row holding them (mod-xii, mod-xiv, mod-xxi).

### OPENROUTER1, the first new provider

Its design is its own, written on the contract once P3 to P7, P10 and P12 have landed. What the contract gives it, so that no file outside its
folder changes but those its design names:

- **O1, the measurement** (the owner's, §6): with the owner's key and free models alone, as its row says (`TASKS.md`'s OPENROUTER1 row), and its
  facts confirmed against OpenRouter's own documentation, which the row says are unconfirmed.
- **O2, the provider.** `Agents/Providers/OpenRouter/`: agent `openrouter`, its identity OpenRouter's; `Accounts.Kind = Key` with its own
  variables (the row's reading: the base address, the key in one variable and another left empty), handed to its own spawn alone; its registration
  saying it **runs Claude Code's program**, its doors reusing Claude Code's native and protocol dialects by reference, which the census allows only
  for an edge its registration declares; its offers declared or read, never Claude Code's; its limit and error words from recorded sentences (a
  daily cap read as a limit, D125's cool-off); `Reach` *opened repositories*, the start layer refusing a closed one (D212 §2.3). Proof as its row
  names: a stand-in in its shape (a key account, no network), its limit read as a limit, a closed repository refused; conformance whole; the
  census holding no `openrouter` site outside its folder.
- **O3, the window.** The key entered in the window and kept in `profiles/` (D172 point 3); its models in the roles' choices as agent and model
  (D205); its words in both catalogues, the product's name kept as written.

## 6. What is the owner's

1. **Nothing in P1 to P16** changes the product's direction, spends an account or money beyond the development's own runs, signs in, publishes,
   pushes, rewrites history or loses data. No record is deleted; `ProfileMustExist` and the `codex` holder are declarations in code, not
   records. The install's proofs after P7 to P11 are the parent's, on the install, after each row is staged.
2. **O1 spends the owner's account.** *May Daoris's development run OpenRouter's free models with your own key?* It is OPENROUTER1's measurement:
   an orientation pack and a brainstorm voice on free models, compared with Sonnet 5.5's on the same input. A yes lets O1 run with the key you
   enter, on free models alone, which OpenRouter caps and may log. Look at the OPENROUTER1 row in `TASKS.md` and this proposal's O1. Asked in
   OPENROUTER1's design, not before.
3. **OpenRouter as an agent of its own on the window.** *Should OpenRouter appear on the Agents page as an agent of its own, its accounts keys and
   its models its own, running Claude Code's program underneath?* This proposal shapes it so (§2, Q2; O2), since an account kind of Claude Code
   would put OpenRouter's models in Claude Code's choices and walk its keys with Claude Code's accounts. A yes keeps OPENROUTER1's design on that
   shape; a no makes it an account kind of Claude Code. Look at §2's Q2 and the O rows. Asked only if OPENROUTER1's design departs from the
   row's *a provider*.
4. **Not asked, and not proposed:** a rule language of no agent's (§2, Q10). The person's rules stay Claude Code's grammar (D72), so Codex, dsh
   and a plugin's agent take none; a direction to change that would be a design of its own.
5. **Not the owner's, but both makers' (D191's note of 2026-10-24):** P9 narrows what each door reads and what Daoris sends Codex, and P13 moves
   postures and rules; neither widens what an agent may do, and each is built only after both makers read it.

## 7. Rejected

- **One interface, today's grown.** Twenty-one members with defaults gave every plugin's door its answers by omission (survey §6); a record of
  stated parts makes silence a statement.
- **A provider per agent and door.** Every account fact declared twice, and the owner rebuilt wherever a door is read (survey §15 tangle 3).
- **Negotiating everything at the open.** The native transport negotiates nothing, and Daoris claims no fact of another program without a
  measurement (D53, D57).
- **The pipe door as Claude Code's own transport.** The stub and every later command-line agent would copy its process handling (Q3).
- **A provider's own event channel, or a provider minting events.** FOUNDATION1 mints at the hub of the process that runs the work and by the
  store's trigger (D212 §4.1); a child the hook and the stream both see would be told twice.
- **The readings declared in Agents.** `Observation`, in Events, would read them upward for good (§0 item 2).
- **An `agent` field beside `adapter` on the session record.** A teammate's older build would not write it, so a newer build would hold two
  truths about one record; derived through the registry it is one.
- **Renaming the door ids** (`claude-code` to a native-specific id). Every record, choice and pin holds them (§4).
- **Narrowing the dialects inside the move.** A behaviour change hidden in a refactor (brief §6); P9 is its own row with its note.
- **A lineage hook every provider must take.** Codex and dsh could answer it only by a claim they cannot keep (Q10).
- **A plugin declaring a dialect, words or abilities.** A manifest is a word, never a measurement (COAGENT1 §3.4).
- **An assembly per provider now.** D212 §14.4's reasons hold; a provider's folder can become one once its edges all point down.
- ***Provider* on the window.** The person knows agents and ways in; a new word would be vocabulary D193 rejects.

## 8. What stays unmeasured

- Whether Codex or dsh emits any of Claude Code's `_meta` keys, or reads `CLAUDE_CODE_EXECUTABLE` or `DISABLE_UPDATES`: P9 and P7 assume neither,
  and measure both on the install and in dsh's recorded frames.
- Whether a plugin's harness is installed on any machine, so P9's narrowing would change what one reads: no plugin harness is among the
  install's doors (read from the brief §5, not from the install).
- Whether Claude Code's native door handed Daoris's connector by `--mcp-config` beside a repository's own `.mcp.json` server of the same name
  conflicts: CONNECTORS1's, before any change to D70's rule.
- Whether `alt-stub` can be pinned with no network, from a registry fixture the install's tests serve; if not, its pin stays pending, named.
- OpenRouter's facts (its variables, its caps, its tool use through Claude Code), all unconfirmed in its row: O1's.
- The sink's cost per line on the console's hot path: expected negligible, a call where two callbacks are today; P10 measures the console's
  latency on the install before and after.
- Whether a Codex candidate read *first* inside its five-minute floor is in fact declined by the roster (Q16 item 6): read, not run; P4's case
  runs it.
