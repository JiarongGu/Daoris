# Daoris's provider layer against Lyntai's agent-provider layer: every site, and what answers it there — a survey

> AGENTLYNTAI1, phase 1 of a foundational co-design under D191 (2026-10-30). The factual ground the sealed proposals stand on; the
> direction, the constraints and the questions are `docs/2026-10-30-agent-lyntai-brief.md`. Read at Daoris `e0c7b7e5b` (the batch line
> `parent/batch-1029o`, which carries AGENTPROVIDER1's rows a and b; main at `a3e226e58` carries neither) and at Lyntai `e010099f` (its
> `master`, 2026-10-30), every line number those commits'. Nothing was run but counts over the census fixtures and line counts of
> files. Lyntai is public and was only read. A fact not seen in a file or a record says *unmeasured*. `Dnn` is
> `docs/decisions/Dnn.md`; a Lyntai decision is named *Lyntai's Dnn* (its `docs/DECISIONS.md`). **Paths:** a path with no prefix is
> under `src/Daoris.Desktop/Daoris.Desktop.Driver/`; `Tests/` is `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/`; `Desktop/` is
> `src/Daoris.Desktop/`; `Service/` is `src/Daoris.Service/`. A path in Lyntai's repository is written `Lyntai/…`; `Lyntai/Core/` is
> its `src/Lyntai.Core/` and `Lyntai/Basic/` its `src/Lyntai.Providers.Basic/`. In the tables, **answers** means a Lyntai type holds the
> same fact today, **partly** that it holds some of it, and **none** that nothing there does. It proposes nothing: §14 lists what the
> records disagree on, and the brief turns the rest into questions.

## 0. In one page

**Daoris's provider layer, as built.** AGENTPROVIDER1's rows a and b (D213's notes of 2026-10-29 and 2026-10-30; the archive's
AGENTPROVIDER1a and 1b, `docs/task-archive.md:21602`, `:21620`) named the contract and its meter beside today's seam, and changed
nothing a person sees:

| Part | Where | Size |
|---|---|---|
| The contract | `Agents/Contract/`, 8 files | 506 lines; 34 public types and one internal |
| The registry and the one registration list | `Agents/Registry/`, 5 files | 702 lines; 9 types |
| The stand-in provider written on the contract | `Agents/Providers/Stub/` | 2 classes (126 lines), `provider.json`, `pending.json`, `Program/agent.mjs` (176 lines) |
| The conformance suite | `Tests/Agents/ProviderConformance*.cs` | 60 cases over three views; 6 pending files, 138 lines |
| The census | `Tests/fixtures/provider-census.json`, read by four twins | 354 rows, 745 sites in 172 files |

What runs is still the seam they measure: `ISessionAdapter` (`Agents/Adapters.cs:1330`), `AdapterSet` (`:2464`) and its adapters
(`:1685`, `:1765`, `:1833`, `:1972`, `:2065`, `:2178`; the file 2,564 lines), the protocol door `AcpSession` (`Agents/Acp.cs:299`; the
file 4,116 lines), the native readers (`Agents/ClaudeStreams.cs`, 299 lines; `Agents/StructuredOutput.cs`, 561), the error reader
(`Agents/AgentErrors.cs`, 344) and the roster (`Agents/HarnessRoster.cs`, 2,401). The contract's production readers are five:
`Agents/Adapters.cs:2547` (the stand-ins through the shim), `Agents/AgentsCommand.cs:654` and `Agents/HarnessRoster.cs:861` (the verbs'
operands and the listing), `Roles/ActorVerbs.cs:75` and `WorkOrders/Domain/BrainstormSynthesis.cs:12` (the stand-in flag).

**Lyntai's layer, as built.** Lyntai's D219 places the contracts and pure policy in `Lyntai.Core` (namespace `Lyntai.Agents`,
`Lyntai/Core/Agents/`, 34 files with the older tool loop among them) and the doors in `Lyntai.Providers.Basic`: the `claude` and
`codex` CLI doors and one generic Agent Client Protocol door (`Lyntai/docs/agent-provider-layer.md:61-67`). Rows AG1 to AG9 are built
(`:3-7`); rows AG10 to AG20 are open (`Lyntai/TASKS.md:293-394`). **None of it is released**: every entry sits under the changelog's
*Unreleased* (`Lyntai/CHANGELOG.md:20`, the layer's at `:107`, `:113`, `:140`, `:154`, `:161`), and the newest release, 3.5.3 of
2026-09-30 (`:307`; tag `v3.5.3`), predates it.

**The sites, and what answers each** (§1 to §7 give each line):

| Kind | Daoris sites | Answers | Partly | None |
|---|---|---|---|---|
| The contract's records and ids | 12 | 3 | 6 | 3 |
| A provider's and a door's parts | 15 | 1 | 6 | 8 |
| The ports | 7 | 1 | 1 | 5 |
| The registry and the registration list | 9 | 0 | 2 | 7 |
| The stand-in provider's files | 5 | 0 | 2 | 3 |
| The conformance suite's case families | 8 | 0 | 4 | 4 |
| The census (one fixture, four twins) | 1 | 0 | 0 | 1 |
| **Total** | **57** | **5** | **21** | **31** |

**Where the two differ most** (each with its section):

1. **A provider with doors against an agent per door.** Daoris's provider is an agent with one or more doors, an account key shared by
   every door that names it (D213 point 1; `Agents/Contract/Provider.cs:28`, `:62`). Lyntai's ACP agent is its own provider, apart from
   the CLI provider of the same vendor, and an account names one provider (Lyntai's D225, `Lyntai/docs/DECISIONS.md:6431-6455`;
   `Lyntai/Core/Agents/AgentSessionModels.cs:73`). AG20 asks the ruling (`Lyntai/TASKS.md:388`). §1, §8.
2. **A live run against one process per turn.** Daoris's protocol door keeps one process across turns, steers, queues words, holds a
   permission request for the person and reads background tasks (`Agents/Acp.cs:299`, `:330`, `:2077-2091`, `:3105`); its native door
   holds a conversation's stdin open (`Agents/Contract/Ports.cs:20-21`, `:52-56`). Lyntai's turn is one `StreamAsync` call, the ACP door
   a process started for the turn and ended with it, steering stated unsupported (`Lyntai/Core/Agents/IAgentSession.cs:9-11`;
   `Lyntai/Basic/Acp/AcpAgentSession.cs:14-29`; `Lyntai/Basic/Acp/AcpAgentDoor.cs:32`); a long-lived run is an option in Lyntai's D233,
   not a row (`Lyntai/docs/DECISIONS.md:6701-6704`). §8.
3. **A fact known with where it applies, against a named ability with a version.** `Known<T>` holds any value with its source, evidence
   and applicability (`Agents/Contract/Known.cs:19`); `AgentAbility` is one of eleven names with a support, a source, evidence, one
   version and side effects (`Lyntai/Core/Agents/AgentProviderDescriptor.cs:59-70`, `:175-176`). §1.
4. **Accounts.** Daoris keys a cooling account by its owner and profile in `cooling.json`, a file under the home
   (`Agents/AccountCooling.cs:10`), on a home where more than one process runs (`docs/2026-10-25-foundation-design.md:2731`); Lyntai's
   pool keys a hold by provider and account name in its key-value store, one process
   (`Lyntai/Core/Agents/AgentPoolReducer.cs:49-51`; `Lyntai/Core/Agents/AgentAccountPool.cs:32`, `:69`;
   `Lyntai/docs/agent-provider-layer.md:210-212`). §8.
5. **Words.** Daoris declares each provider's and door's tables as data (`Agents/Contract/ProviderParts.cs:81-87`, `:126`); Lyntai keeps
   one classifier and reads each door's failure in that door's internal reader (`Lyntai/Core/Inference/ProviderVerdictClassifier.cs:12`;
   `Lyntai/Basic/Acp/AcpFailure.cs:41`). §2, §8.
6. **What Daoris built that the layer has not.** A subagent's lines kept apart and background tasks read (`Agents/ClaudeStreams.cs:73`,
   `:99`, `:137`), a held permission request, a model carried on the protocol door (`Agents/Acp.cs:639`): Lyntai's AG10, AG18, AG19 and
   AG11 to AG12, open (`Lyntai/TASKS.md:299`, `:374`, `:381`, `:312`, `:322`). §8.

## 1. The contract's records and ids

| Daoris | Holds | Lyntai today | Answer |
|---|---|---|---|
| `Provider` (`Agents/Contract/Provider.cs:28-48`) | id, identity, doors, program, accounts, tables, usage, lent, models, children, rules, skills, conversations, reach, vocabulary, unavailable | `AgentProviderDescriptor` (`Lyntai/Core/Agents/AgentProviderDescriptor.cs:114-125`: `Id`, `Doors`, `Accounts`) and `IAgentProvider` (`Lyntai/Core/Agents/IAgentProvider.cs:13-22`: `Describe`, `Open(doorId)`) | partly |
| `AgentDoor` (`Provider.cs:62-75`) | id, transport, identity, program, home, lent, offers, postures, spawn, dialect, abilities, tables, `accountOf` | `AgentDoor` (`AgentProviderDescriptor.cs:85-109`: `Id`, `Kind`, `Abilities`, `SupportFor`, `Unhonourable`) | partly |
| `ProviderId` (`Agents/Contract/ProviderId.cs:8-19`) | typed; compared without case (`:62`) | a string (`AgentProviderDescriptor.Id`), compared ordinally (`IAgentProvider.cs:125`); the CLI providers' default ids `claude-cli` and `codex-cli`, host-nameable (`Lyntai/Basic/ClaudeCli/ClaudeCliProvider.cs:27`; `Lyntai/Basic/CodexCli/CodexCliProvider.cs:28`; `Lyntai/Basic/ClaudeCli/ClaudeCliBuilderExtensions.cs:88-89`) | partly |
| `DoorId` (`ProviderId.cs:25-36`) | typed; unique among all doors; may equal its own provider's id, never another's | a string, unique within its provider only (`AgentProviderDescriptor.cs:87`); the doors are `cli` and `acp` (`Lyntai/Basic/ClaudeCli/ClaudeAgentDoor.cs:11`; `Lyntai/Basic/CodexCli/CodexAgentDoor.cs:11`; `Lyntai/Basic/Acp/AcpAgentDoor.cs:12`) | partly |
| `AccountKey` (`ProviderId.cs:43-54`) | the string account records hold; a door's `accountOf` verbatim, else its provider's id; resolves to a pool of providers | `AgentAccount.Provider` names exactly one provider, and another provider refuses it (`AgentSessionModels.cs:73`; `IAgentProvider.cs:94-102`); one account per provider over the same home (`Lyntai/Basic/Acp/AcpBuilderExtensions.cs:20-22`) | none |
| `KnownBy` (`Agents/Contract/Known.cs:4-9`) | declared, negotiated, measured | `AgentAbilitySource` (`AgentProviderDescriptor.cs:18-28`), the same three | answers |
| `Known<T>` (`Known.cs:19-46`) | any value, its source, evidence, where it applies (COAGENT1 §3.3's eight facts, D213 point 2), or stated absent, read as today's behaviour (D53) | `AgentAbility` (`AgentProviderDescriptor.cs:59-70`): a named ability's support (unknown, supported, unsupported; unknown the default, read as *not promised*, `:5-15`), source, evidence, `AppliesTo` one version, `SideEffects` | partly |
| `Transport` (`Agents/Contract/Transport.cs:4-11`) | `Native`, `Protocol` | `AgentDoorKinds.Cli`, `.Acp`, open to a host's own kind (`AgentProviderDescriptor.cs:74-81`) | answers |
| `TransportOf` (`Transport.cs:14-17`) | today's `SessionWire` in the contract's word | — | none |
| `AccountPool` (`Agents/Contract/AccountPool.cs:8-25`) | an account key, the providers holding it and their doors, none first | — (Lyntai's `AgentAccountPool` is the walk and its holds, §8) | none |
| `Eligibility` (`Agents/Contract/AgentRun.cs:37-40`) | a protocol door's resume as eligibility negotiated at the open | resume `Unknown` with the source `Negotiated`, settled by the agent's `initialize` (`AcpAgentDoor.cs:26-27`; Lyntai's D225) | answers |
| `AgentRun` (`AgentRun.cs:9-31`) | open, resume, apply a refused choice's later one, send, steer, stop a turn, stop a task, close; an unsupported operation refused before a write | `IAgentSession.StreamAsync` (`IAgentSession.cs:30-33`): one turn; the conversation its `ResumeToken` (`AgentSessionModels.cs:31`); a stop the cancellation token; an option a door cannot honour refused before a spawn (`IAgentProvider.cs:104-113`) | partly |

## 2. A provider's and a door's parts

All in `Agents/Contract/ProviderParts.cs` unless named.

| Daoris part | Holds | Lyntai today | Answer |
|---|---|---|---|
| `PortRef` (`:11`) | a later row's port, held absent | — | none |
| `Reach` (`:14-18`) | `Everywhere` or `Opened`, never absent | `AgentSessionOptions.WorkingDirectory` alone (`AgentSessionModels.cs:52`); where a repository is opened is the host's (the study, `Lyntai/docs/agent-provider-daoris-study.md:86`) | none |
| `ProviderIdentity` (`:25`) | product, maker, mark, stand-in, the declaring plugins | — | none |
| `DoorIdentity` (`:31`) | the door's plugin, product and maker | — | none |
| `ProgramFacts` (`:34-47`) | binary, version question, install, update, package, channel, pinned environment, presence probe | not in the descriptor; `ICliBackend` drives a backend's own installer at a version the host names (`Lyntai/Core/Inference/Cli/ICliBackend.cs:20`; Lyntai's D20, `Lyntai/docs/DECISIONS.md:576-592`), and release sources are the host's (the study, `:63`) | none |
| `PoolFacts` (`:54-68`) | key variable, login question, sign-in and device code, ports, settings file, a fixed week | the home variable (`AgentAccountMapping`, `AgentProviderDescriptor.cs:131`); a signed-in probe (`IProviderAuth`, `Lyntai/Core/Inference/IProviderAuth.cs:21`); an account that is a key is AG17 (`Lyntai/TASKS.md:365`) | partly |
| `AccountsPart` (`:75`) | the pool's key, the door it references, its facts | `AgentProviderDescriptor.Accounts`, set exactly when the doors state `accounts` (`AgentProviderDescriptor.cs:122-124`) | partly |
| `TablesPart` (`:81-87`) | limit, window, sign-in and refused words; error words | one classifier's sentence tables (`ProviderVerdictClassifier.cs:12`) and each door's internal failure reader (`Lyntai/Basic/ClaudeCli/StreamJsonFailure.cs:18`; `Lyntai/Basic/Acp/AcpFailure.cs:41`); none a provider declares | partly |
| `UsagePart` (`:90-93`) | `IUsageQuestion` | `IAgentUsageSource` on the provider, found by type (`Lyntai/Core/Agents/AgentUsageSource.cs:10`; Lyntai's D222) | answers |
| `LentPart` (`:100-103`) | what a harness naming this id reads, one level | — | none |
| `DoorHome` (`:109-117`) | the door's own home variable, own home, trust file, must-exist | the home variable on the descriptor, not the door (`AgentProviderDescriptor.cs:122-131`), and the ACP door's `accountHomeVariable` (`AcpBuilderExtensions.cs:30-41`); no own home, trust file or must-exist | partly |
| `PosturesPart` (`:120`) | ordinary, asking and full in the agent's own words | `AgentToolPolicy` `ReadOnly` or `Write`, read-only the default, the guarantee the intent (`AgentSessionModels.cs:6`, `:22-28`); the ACP door rejects every permission request read-only and allows each once for a write (`Lyntai/Basic/Acp/AcpAgentSession.cs:28-29`; `Lyntai/Basic/Acp/AcpAgentReader.cs:196`) | partly |
| `AbilitiesPart` (`:123`) | interactive, resumes, takes settings | `resume` among eleven abilities (`AgentProviderDescriptor.cs:137-176`); interactive is the host's (the study, `:64`); nothing for taking settings | partly |
| `DoorTablesPart` (`:126`) | the door's error words and its turn report's meaning | internal readers only | none |
| `VocabularyPart` (`:132`) | the variables, files and wire keys it names | — (AG17 proposes the variables a provider reads, `Lyntai/TASKS.md:365-372`) | none |

## 3. The ports

All in `Agents/Contract/Ports.cs`.

| Daoris port | Holds | Lyntai today | Answer |
|---|---|---|---|
| `ISpawn` (`:15-37`) | a start's, a conversation's and a native resume's process; servers, settings and plugins handed; tools up front | each door composes its own spawn inside the library; the seam a host holds is `IProcessRunner` (`Lyntai/Core/Processes/IProcessRunner.cs:13`, `ExchangeLinesAsync` `:83`; Lyntai's D226) and the registration's environment, where a null value removes a variable (`AcpBuilderExtensions.cs:28-29`) | partly |
| `IDialect` (`:40`) | the marker of a door's dialect | — | none |
| `INativeDialect` (`:47-57`) | a native door's reader, its message and interrupt framing | internal `StreamJsonAgentReader` and `CodexAgentReader` (`Lyntai/Basic/ClaudeCli/StreamJsonAgentReader.cs:14`; `Lyntai/Basic/CodexCli/CodexAgentReader.cs:30`) under `CliAgentLoop` (`Lyntai/Basic/CliAgentLoop.cs:23`); none public | none |
| `IProtocolDialect` (`:63-67`) | what a protocol door reads beyond the spec | the ACP reader reads both vendors' extensions inline for every agent (`AcpAgentReader.cs:251`; `AcpFailure.cs:61-66`); a dialect seam is AG15 (`Lyntai/TASKS.md:349`) | none |
| `IUsageQuestion` (`:70-86`) | the usage question's program, command, recorded answers and freshness | `IAgentUsageSource.ReadAllowanceAsync`, a version probe and then the vendor's own server under the account's home, validated per version (`AgentUsageSource.cs:10`; `Lyntai/docs/agent-provider-layer.md:213-228`) | answers |
| `IChildVocabulary` (`:89-99`) | how a child is named and whether its end is said | — (AG10 and AG18, `Lyntai/TASKS.md:299`, `:374`) | none |
| `IConversationHome` (`:102-106`) | what a conversation keeps under an account's home, by version | — (the resume token alone) | none |

## 4. The registry and the registration list

| Daoris | Holds | Lyntai today | Answer |
|---|---|---|---|
| `ProviderRegistry` (`Agents/Registry/ProviderRegistry.cs:22-284`) | refuses a twice-registered id or door and a door named as another provider (`:30-73`); answers `Door`, `Provider`, `ProviderOf`, `OwnerOf`, `OwnerOfAgent`, `Pool`, `Resolve` by kind, the lent words, `AccountAgentOf`, `Listed`, `Operands` (`:91-225`) | `AgentProviderRegistry` (`IAgentProvider.cs:123-160`): `Ids`, `All`, `Get`, `TryGet`; an unknown id throws naming what exists; **a second registration of an id replaces the first** (`:121-122`, `:131`) | partly |
| `ResolvedId`, `AccountAgent` (`ProviderRegistry.cs:4`, `:7`) | an id resolved by its kind; the agent answering for a door's accounts | — | none ×2 |
| `IdKind`, `RecordIdField`, `RecordIds` (`Agents/Registry/RecordIds.cs:4-61`) | the 22 stored fields that hold an id, each by its kind | — (Lyntai keeps its own strings frozen, Lyntai's D70; the study, `:85`) | none ×3 |
| `ProviderRegistration` (`Agents/Registry/ProviderRegistration.cs:23-195`) | today's adapters grouped by account key by three rules | — | none |
| `Providers` (`Agents/Registry/Providers.cs:9-24`) | `Built()`, the one registration list; `IsStandIn` | a DI collection, `AddAgentProvider` (`Lyntai/Core/DependencyInjection/LyntaiBuilder.cs:90`), each door's `Add…AgentSession` registering its provider (`ClaudeCliBuilderExtensions.cs:88-99`; `AcpBuilderExtensions.cs:35-56`); no stand-in flag | partly |
| `LegacyDoor` (`Agents/Registry/LegacyDoor.cs:19-138`) | a contract door presented as an `ISessionAdapter` | — | none |

## 5. The stand-in provider

| Daoris | Holds | Lyntai today | Answer |
|---|---|---|---|
| `StubProvider` (`Agents/Providers/Stub/StubProvider.cs:16-96`) | provider `stub`, doors `stub` (native) and `acp-stub` (protocol), Claude Code's words by reference | three programs for Lyntai's own tests, `Lyntai/devtools/scripts/provider-stub.mjs`, `codex-stub.mjs`, `acp-stub.mjs`, and a suite's own `StandInProvider` (the study, `:59-60`) | partly |
| `StandInSpawn` (`Agents/Providers/Stub/StandInSpawn.cs`) | the stand-in's spawn of its folder's program | — | none |
| `provider.json` (`Agents/Providers/Stub/provider.json:1-9`) | the one place a tool reads a provider's stand-in facts | — | none |
| `pending.json` (`Agents/Providers/Stub/pending.json`, 36 lines) | the cases it cannot pass yet, each with its row | — | none |
| `Program/agent.mjs` | the scenario contract (D213's row b part 3 note) | `acp-stub.mjs`'s markers (`Lyntai/docs/agent-provider-layer.md:301-303`) | partly |

## 6. The conformance suite

Daoris's suite (`Tests/Agents/ProviderConformanceTests.cs:19-97`; its cases `Tests/Agents/ProviderConformanceCases.cs:15-75` and
`Tests/Agents/ProviderConformanceLive.cs:25-55`) runs 60 cases over three views (`ProviderConformanceTests.cs:35-63`): the registry over
today's adapters, `Providers.Built()` through the shim, and a test home's two plugins. 26 are mandatory, 14 an ability's or its
fallback's, 20 a stand-in's program run live, in a fast half, an environment half run alone and a Process half. Each provider holds what
it cannot pass in its own `pending.json`: `claude-code` 26 lines, `codex` 24, `dsh` 24, `stub` 36, the tests' `conformance-door` 22 and
`conformance-off-door` 6 (`Tests/fixtures/providers/*/pending.json`; `Agents/Providers/Stub/pending.json`).

Lyntai's theory (`Lyntai/tests/Lyntai.Tests/Agents/AgentProviderConformanceTests.cs:116-375`, 18 tests) holds the descriptor and the
spawn rules over its registry, and never drives a stream (the study, `:59`); AG16 proposes the stream rules in one theory
(`Lyntai/TASKS.md:357`).

| Daoris case family | Cases | Lyntai's theory | Answer |
|---|---|---|---|
| identity, parts, names (`identity.rules`, `parts.stated`, `ids.names-resolve`, `records.ids-resolve`) | 4 | every ability stated once; unknown id throws; malformed descriptor refused; a second registration replaces the first (`:116`, `:283`, `:310`, `:375`) | partly |
| postures and options (`posture.unmapped-refused`, `run.unsupported-refused`) | 2 | an option the door cannot honour refused before a spawn (`:138`) | partly |
| the recipe and the spawn's environment (`recipe.*`, `spawn.*`) | 8 | — | none |
| accounts (`accounts.pool-agrees`, `accounts.lent-exact`) | 2 | an account of another provider refused; its own reaching the child's home variable (`:198`, `:218`) | partly |
| interactive, tables, frames, admission, containment, applicability | 10 | — | none |
| abilities and their fallbacks (`ability.*`) | 12 | each door states every ability, an unmeasured one unknown (`:116`, `:126`, `:366`) | partly |
| dialects (`dialect.recorded-frames`, `dialect.cross-fed`) | 2 | — (AG15) | none |
| live cases (`live.*`) | 20 | — (AG16) | none |

## 7. The census

One fixture, `Tests/fixtures/provider-census.json`, its words and ids in `Tests/fixtures/provider-vocabulary.json` (9 ids, 15 words) and
its owner table in `Tests/fixtures/provider-owner.json` (13 cases), read by four twins: `Tests/Agents/ProviderCensusTests.cs`
(`:200-326`, eight facts), `Service/Daoris.Service.Tests/ProviderCensusTests.cs`, `src/Daoris.Web/src/test/providerCensus.test.ts` and
`src/Daoris.Cli/tool-test/provider-census.test.ts`. Each counts exactly: a site not named, a count that differs and an allowance nothing
needs each fail. Lyntai has none: the study files nothing for it, its own surface held by its API-vocabulary check (`:58`). Counted at
`e0c7b7e5b`:

| Section | Rows | Sites | Files | Behaviour | Permanent | Words | Measurement |
|---|---|---|---|---|---|---|---|
| The desktop (driver, host, modules) | 210 | 373 | 90 | 326 | 21 | 22 | 4 |
| The service | 6 | 10 | 4 | 3 | 4 | 3 | 0 |
| The page | 103 | 231 | 60 | 0 | 4 | 0 | 227 |
| The tools and the CLI | 35 | 131 | 18 | 10 | 5 | 15 | 101 |
| **Total** | **354** | **745** | **172** | **339** | **34** | **40** | **332** |

By the kinds of token the fixture's own `_why` names (`provider-census.json:2`), the desktop's 373 sites are 153 door tests, 124
provider types, 73 ids and 23 vocabulary words. **By the design's row that moves them** (the behaviour sites): the desktop's g 113 (31
files), e 53 (13), f 52 (8), t 48 (`Agents/Adapters.cs` 45, `Agents/Registry/LegacyDoor.cs` 3), h 15, o 12, i 6, j 6, k 5, p 4, d 3,
n 3, q 3, l 2, r 1; the service's r 3 (`Core/Sessions/CallLineage.cs`); the tools' b 6 (`tools/deployment-rehearsal.mjs`) and d 4
(`tools/live-logins.mjs`). Rows c, m, u and the corrections s-i to s-ix move no counted site. The files of the largest groups: row g's
in Loop (`Loop/Driver.cs` 20, `Loop/Chat/ChatRunner.cs` 17, `Loop/DriverContinuations.cs` 8), Help (`Help/HelpWired.cs` 7), set-up
and the host's consoles; row e's in `Agents/Adapters.cs` 14, `Agents/HarnessRoster.cs` 9 and the usage readers; row f's in Install's
release downloads (`Install/CodexReleaseDownloads.cs` 20, `Install/ClaudeReleaseDownloads.cs` 13, `Install/ReleaseChannels.cs` 13).

## 8. Beyond the contract: the readers rows c to u move, against Lyntai

| Daoris today | Lyntai today | The difference |
|---|---|---|
| **The protocol door.** `AcpSession`, one process across a session's turns (`Agents/Acp.cs:299`): a model by `session/set_config_option` (`:639`), a posture by `session/set_mode` (`:3444`), a stop by `session/cancel` (`:2786`), a permission request held for the person (`:330`, `:3105`), Claude Code's `promptQueueing` read inline (`:2077-2091`) | `AcpAgentSession`: `initialize`, `session/new` or a resume, then `session/prompt`, in a process started for the turn and ended with it; no filesystem and no terminal offered; a cancellation sends `session/cancel` then kills after a grace (`AcpAgentSession.cs:14-29`); no `set_mode` and no `set_config_option` under `Lyntai/Basic/Acp/` | a live run, steering, held requests and a carried model on the protocol door have no counterpart; a long-lived run is Lyntai's D233 option with its trigger (`Lyntai/docs/DECISIONS.md:6701-6704`); AG11, AG19 |
| **The native door.** Claude Code's stream read by `ClaudeStreams` and `StructuredOutput` (`Agents/StructuredOutput.cs:44`), a subagent's lines by `parent_tool_use_id` (`Agents/ClaudeStreams.cs:73`), background work by `task_started` and `task_notification` (`:99`, `:137`); a conversation's stdin held open with its framing (`Ports.cs:20-21`, `:52-56`) | `CliAgentLoop`, one process per turn fed its prompt on stdin (`Lyntai/Basic/CliAgentLoop.cs:23`; `ClaudeAgentDoor.cs:32`), `StreamJsonAgentReader` reading no parent id (`Lyntai/TASKS.md:299-303`); a `codex` CLI door Daoris does not have (`CodexAgentDoor.cs:11`) | AG10 and AG18; Daoris has no native Codex door and Lyntai no `dsh` (the generic ACP registration would carry it, unmeasured) |
| **Failures.** `AgentErrors.Read` (`Agents/AgentErrors.cs:116`) into `AgentErrorKind` limit, access, transient, fatal (`:6-18`), from a `DoorFailure` (`:47`), each door's table before its provider's (D210); the transient retry inside the run (`Agents/TransientRetry.cs:18`) | `AgentFailure` with `AgentFailureKind` (fatal the default, then limit, access, transient), a restriction and a delivery (`Lyntai/Core/Agents/AgentFailure.cs:8-22`, `:127-135`); the order limit, access, transient, the unread fatal (`Lyntai/docs/agent-provider-layer.md:166-169`); `RetryingAgentSession`, off unless registered, resending only a never-sent prompt (`Lyntai/Core/Agents/RetryingAgentSession.cs:27`; `LyntaiBuilder.cs:559`) | the four kinds agree; the reading's input differs (a door's failure record against each door's own fields), and Daoris's retry runs inside its live run |
| **Restrictions.** AGENTINFRA1's `AvailabilityRestriction`: cause, subject (`model-on-binding` among them), a subject key, recovery with an owner action's reason, observed time, evidence, dialect version, id and revision (`docs/2026-10-27-provider-infrastructure-design.md:121-132`), designed and not built | `AgentRestriction(Cause, Subject, Recovery)` (`AgentFailure.cs:104`), an owner action with no reason (`:77-78`), the id and revision on the pool's `AgentHold` (`Lyntai/Core/Agents/AgentPoolModels.cs:109-111`); `AgentDelivery` never sent, taken, uncertain (`AgentFailure.cs:109-120`) | the study reads them as one model, member for member (`:33-38`); the fields above are where they part |
| **Accounts and the walk.** The account rule `LineAccount` (`Agents/LineAccount.cs:201`) and the roster's walk (`Agents/HarnessRoster.cs`), Roles' ordered choices (D205), the cool-off in `cooling.json` keyed by owner and profile (`Agents/AccountCooling.cs:10`, `:29`), an unstated reset cooled 60 minutes (D125; the infrastructure design `:152-155`), several processes on one home (`docs/2026-10-25-foundation-design.md:2731`) | `AgentAccountPool` over ordered `AgentChoice`s, a seat per turn (`AgentAccountPool.cs:61`, `:100`, `:146`), holds by provider and account name (`AgentPoolReducer.cs:49-51`, `:218-224`) in the key-value store under `lyntai:agent-pool:` (`AgentAccountPool.cs:69`), an unstated recovery held until the host clears it (Lyntai's D227, `Lyntai/docs/DECISIONS.md:6482-6508`), two processes on one pool out of scope (Lyntai's D219, `:6271-6273`) | the hold's key (AG20), its store, its default for an unstated reset and its processes all differ |
| **Usage.** `ClaudeUsage` and `CodexUsage` (`Agents/ClaudeUsage.cs:21`; `Agents/CodexUsage.cs:91`) behind `IUsageQuestion`, read between turns | `ClaudeUsageDialect`, `CodexUsageDialect` (internal) behind `IAgentUsageSource`, host-enabled, validated per version (Lyntai's D222) | the shape agrees; the validated versions and the floor (the host's, the study `:62`) are each side's |
| **Models.** offers per door, version and account in Agents' offers store, one judge (`Agents/AgentOffers.cs:41`, `:86`; D205); the model carried on both doors | `AgentSessionOptions.Model` carried on both CLI doors (`AgentSessionModels.cs:39`), `AgentChoice.Model` and `Effort`, the effort the host's word (`AgentPoolModels.cs:13-17`); the ACP door reads no model (AG11) and a refused model holds nothing (AG12) | AG11, AG12 |
| **Readings.** `SessionEvent` and its kinds in Events (`Events/Domain/SessionEvents.cs:13`, `:190`); D213 §3's `WireReading`, designed, not built | `AgentStreamEvent`: session started, text, thinking, tool call and result, plan, live, final and nullable usage, one terminal `SessionEnded` with its failure (`Lyntai/Core/Agents/AgentStreamEvent.cs:7-87`) | no child, task, held request or refused model reading; provenance and replay not carried (the study, `:69`) |
| **The hand-over.** the move's brief (`Context/Domain/ContextBrief.cs:84`; D189) | `AgentBriefComposer` and `AgentHandover.Decide` (`Lyntai/Core/Agents/AgentBriefComposer.cs:53`; `Lyntai/Core/Agents/AgentHandover.cs:81-105`; Lyntai's D228): resume only on the same provider, door and account with resume supported | the same rule (the study, `:77`); the brief's composition differs in source and order |
| **The spawn and containment.** the spawn's shell and Daoris's variables (`Agents/Adapters.cs`), the launcher that starts and contains every agent (`Platform/AgentStarts.cs:18`; D201) | the door's own spawn through `IProcessRunner`, which a host may supply to sandbox, audit or remote it (Lyntai's D226, `Lyntai/docs/DECISIONS.md:6471-6473`) | where the launcher sits under a Lyntai door is the runner seam |

## 9. The design's rows, the census and the study

Each row of `docs/2026-10-26-agent-provider-design.md` §11 (`:1027-1153`, order `:1155-1158`), the census sites it moves (§7), and what
Lyntai's study answers (`Lyntai/docs/agent-provider-daoris-study.md:56-86`): *nothing* where the layer has it or files nothing, *host's*
where the study judges it the host's (§5 there), else the Lyntai row it files.

| Row | Census sites | The study's answer (its line) |
|---|---|---|
| a, b (built) | — | descriptor, registry, theory (`:58`); AG16 (`:59`) |
| c the third stand-in | 0 | AG15's proof, AG16 (`:60`) |
| d ⚖ the owner once | 3, and the tools' 4 | AG20, a ruling first (`:61`) |
| e the accounts' readers | 53 | nothing; the floor host's (`:62`) |
| f the program | 52 | host's, by Lyntai's D20 (`:63`) |
| g the doors as abilities | 113 | nothing; interactive host's (`:64`) |
| h ⚖ the spawn | 15 | AG17; Daoris's own variables host's (`:65`) |
| i the protocol transport | 6 | AG15 (`:66`) |
| j ⚖ each protocol door its dialect | 6 | with AG15, the narrowing the host's choice (`:67`) |
| k the native transport | 5 | nothing (`:68`) |
| l the readings and the sink | 2 | AG10, AG18, AG19, AG12 (`:69`) |
| m the session's life | 0 | an option in Lyntai's D233 (`:70`) |
| n models | 3 | AG11, AG12 (`:71`) |
| o ⚖ permissions, skills, hooks | 12 | AG19; the rule language, skills and hook host's (`:72`) |
| p ⚖ connectors | 4 | nothing (`:73`) |
| q ⚖ plugins' doors | 3 | nothing (`:74`) |
| r the page, the service, the tools | 1, and the service's 3 | nothing; host's (`:75`) |
| s-i, s-iii, s-iv, s-viii | 0 | host's (`:76`, `:78`, `:79`, `:83`) |
| s-ii a resume's own conversation | 0 | the same rule (`:77`) |
| s-v ⚖ the Claude Code seam | 0 | Daoris-specific (`:80`) |
| s-vi ⚖ other providers' credentials | 0 | AG17 (`:81`) |
| s-vii ⚖ counted by agent | 0 | AG20 (`:82`) |
| s-ix on | 0 | Daoris-internal (`:84`) |
| t the old seam's end | 48 | Lyntai's strings frozen (`:85`) |
| u ⚖ reach | 0 | host's (`:86`) |

**OPENROUTER1** (the design's §12, `:1165-1193`; `TASKS.md:225`): Claude Code's program pointed at OpenRouter, its key one variable and
another left empty. The study: the same composition runs in Lyntai for one account through a registration's environment; an account
that is a key is AG17; an HTTP door is AG13 and AG14 on Lyntai's D233, which takes `ToolLoop` as the executor and keeps the workspace
the host's (`:96-112`; `Lyntai/docs/DECISIONS.md:6675-6699`). Lyntai's D233 rejects Claude Code at a gateway as the only route
(`:6696-6697`).

**AGENTINFRA1** (`docs/2026-10-27-provider-infrastructure-design.md`; `TASKS.md:51`): stages A to E (`:292-298`), after mod-ix and
through AGENTPROVIDER1 (`:288`, `:359-360`), remote operation first in an isolated home (`:273-284`). Lyntai keeps a hosted agent out
(its D233, `:6687-6688`) and durable request records the host's (the study, `:154-155`).

**FOUNDATION1's Part B rows the design reads** (its §2.8, `:479-492`): **g**, failures read once on every path
(`docs/2026-10-25-foundation-design.md:1717`); **j**, one start plan and one walk (`:1721`); **q** ⚖, one policy shape and start
admission (`:1734`); **r** ⚖, the connector by work (`:1735`). None is built: D212's notes record none of them (`D212.md`, through
`:1503`). Part B's order puts them g, then j after h, i and o, then q and r near the end (`:1679-1680`). The study answers g with the one
classifier and j with the host's start plan, which `AgentAccountPool.PickAsync` would serve (`:92`), and q and r as the host's (`:93`).
FOUNDATION1's Part A is complete (D212's mod-xxi note, `D212.md:1458`).

## 10. What the host keeps

Lyntai's layer leaves to hosts which quest or task an agent works, session trees, branches and landing, roles, review and second
opinions, when to rotate, wait or hand over, which paid model, sign-in and every credential, where a transcript is kept, the clock
the waits run on, notifications and every screen (`Lyntai/docs/agent-provider-layer.md:310-316`); the study adds, for Daoris, durable
request records and their reconciliation, a default cool-off for an unstated reset, the usage floor, which variables to strip, where a
repository is opened to a provider, the rule language and skills, and the plugin catalogue (`:132-140`). In Daoris these sit in its
subdomains above Agents (D212 §14.2): the work orders and the course (Course, Work orders), the start plan and the staffing (Roles),
the executors (Loop), the binding and lineage (Participants), review (Review), Ask Daoris (Help), the composed instruction (Context),
the trees and git (Workplace), the pins and installs (Install), the launcher and containment (Platform), the page and its words
(`src/Daoris.Web/`).

## 11. The package

| Fact | Where |
|---|---|
| Lyntai's version source is one `VersionPrefix`, 3.5.3 at `e010099f`, stamped by its release pipeline alone | `Lyntai/src/Directory.Build.props:9`; Lyntai's D19 (`Lyntai/docs/DECISIONS.md:563-574`) |
| Its target framework is `net10.0`, as the desktop's | `Lyntai/src/Directory.Build.props:14`; `Desktop/Directory.Build.props:8` |
| Both trees manage package versions centrally | `Lyntai/src/Directory.Build.props:21`; `Desktop/Directory.Packages.props:18` |
| `Lyntai` is a metapackage with no assembly: `Lyntai.Core`, `Lyntai.Providers.Basic`, `Lyntai.Storage.Basic` and `Lyntai.Tools.Mcp`; new at 2.0.1 | `Lyntai/src/Lyntai.Bundle/Lyntai.Bundle.csproj:23`, `:27`, `:40-43`; `Lyntai/CHANGELOG.md:3628`, `:3645` |
| The layer lives in `Lyntai.Core` and `Lyntai.Providers.Basic`; no package of its own (a later one only through Lyntai's D221 gate) | `Lyntai/docs/agent-provider-layer.md:59-80`; Lyntai's D221 (`:6312-6339`) |
| `Lyntai.Core` references the DI and logging abstractions; `Lyntai.Providers.Basic` adds `Microsoft.Extensions.Http`; `Lyntai.Tools.Mcp` adds `ModelContextProtocol.Core` 2.2.0; `Lyntai.Storage.Basic` adds nothing; the Microsoft.Extensions pins 10.0.12 | `Lyntai/src/Lyntai.Core/Lyntai.Core.csproj:10-11`; `Lyntai/src/Lyntai.Providers.Basic/Lyntai.Providers.Basic.csproj:28-31`; `Lyntai/src/Lyntai.Tools.Mcp/Lyntai.Tools.Mcp.csproj:19`; `Lyntai/src/Lyntai.Storage.Basic/Lyntai.Storage.Basic.csproj:22-24`; `Lyntai/src/Directory.Packages.props:21-24`, `:48` |
| The transitive closure of either package, and how it meets the desktop's Shenora and SQLite pins | *unmeasured*: no restore was run |
| The layer registers through `LyntaiBuilder` on an `IServiceCollection`; its main types have public constructors | `LyntaiBuilder.cs:90`, `:559`, `:574`, `:595`, `:614`; `AcpBuilderExtensions.cs:35-56`; `IAgentProvider.cs:49`, `:128`; `AgentAccountPool.cs:61`; `AcpAgentSession.cs:64` |
| The driver takes no DI container: no `Microsoft.Extensions` namespace in its sources (the app, the modules and their tests use them) | a search of `Desktop/` for `using Microsoft.Extensions` |
| Releases push every package to nuget.org; packages carry no prerelease suffix; a preview train per package is rejected | `Lyntai/.github/workflows/release.yml:156-158`; `Lyntai/devtools/nuget-unlist.mjs:70`; Lyntai's D221 (`:6336`); `Lyntai/docs/vnext-modular-capabilities.md:58` |
| Whether nuget.org lists `Lyntai` 3.5.3 | *unmeasured*: no feed was asked |
| **The frozen surface.** Every package is under the full SemVer promise (Lyntai's D70); while every consumer is first-party a disclosed break may ship in a minor, never a storage or migration break, and the relaxation ends with the first external consumer (Lyntai's D18); a published member is frozen, so the layer's certain parts ship first, its records sealed with init members (the layer's §4); the ACP door's claims, the pool's constants and the brief's order are frozen from release (Lyntai's D225, D227, D228); the public surface is held by `ApiSurface` baselines | `Lyntai/docs/DECISIONS.md:1707-1745`, `:542-561`, `:6435-6436`, `:6486`, `:6513-6514`; `Lyntai/docs/agent-provider-layer.md:82-86`; `Lyntai/tests/Lyntai.Tests/Api/Baselines/Lyntai.Core.txt`, `Lyntai.Providers.Basic.txt` |
| The desktop's packages: `Shenora`, `Shenora.Windows`, `Shenora.Chromium` 0.24.0; `Microsoft.Data.Sqlite` 10.0.12 and `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5, the driver's only references; the test packages and Roslyn for the tests | `Desktop/Directory.Packages.props:30-49`; `Desktop/Daoris.Desktop.Driver/Daoris.Desktop.Driver.csproj:17-18` |
| No project in the desktop tree references Lyntai | `Desktop/*/*.csproj` |
| The service already takes `Lyntai.Core` and `Lyntai.Providers.Basic` 3.5.3, released versions under D22, its SQLite pins following Lyntai's 3.5.3 | `Service/Directory.Packages.props:6-11`, `:21`, `:24`, `:28-31` |
| Lyntai's tree now pins `Microsoft.Data.Sqlite` 10.0.14 (taken by `Lyntai.Storage.Sqlite` only) | `Lyntai/src/Directory.Packages.props:13` |
| **D22**: the service and the desktop take the family's libraries at released versions, never a sibling's working tree; a sibling's capability, never its role; an awkward seam is a finding for that sibling | `D22.md:3-4`, `:12-19`, `:41-42` |

## 12. Local operation and what a restore needs

- **Local operation does not require a remote** (`AGENTS.md:14`), and doctrine works without a service (`:14-15`). The CLI keeps no
  runtime dependency (`AGENTS.md:21-23`; D22 `:3-4`).
- **No `NuGet.Config` is tracked** in this repository, so a restore reads the machine's own package sources (*unmeasured* which).
- **The desktop already restores from nuget.org**: Shenora 0.24.0 was restored from nuget.org and npm (`Desktop/Directory.Packages.props:22-23`;
  D22's SHENORA024 note, `D22.md:100`), and a restore that misses a just-released version is retried with `--no-http-cache`
  (D22's SHEN4 note, `D22.md:96-98`).
- A restore takes a version already in the machine's global packages folder without a feed and fetches only what it lacks: NuGet's
  standard behaviour, *unmeasured* on this machine and in the CI beside.
- The install runs what its build copied; nothing at run time restores a package (*unmeasured* on the install, as a publish's layout
  was not read).
- Lyntai's layer itself spawns only what the host deployed and never downloads a runtime (Lyntai's D20, `:576-592`); its usage reading
  starts the vendor's own server, an effect the ability declares (Lyntai's D222).

## 13. The two answers Lyntai owes

Lyntai is answering two requests this design reads when they land. Each is a placeholder until then.

- **AG20's ruling**: whether one account is usable across a CLI door and an ACP door of the same agent, an identity the pool keys a hold
  by (`Lyntai/TASKS.md:388-394`; Lyntai's D225's re-open condition, `:6454-6455`). *Placeholder: not answered at `e010099f`; AG20 is
  decision-only, waiting for the owner's ruling there.*
- **How a host consumes the layer before a release**: how the desktop builds against layer changes Lyntai has merged and not released.
  *Placeholder: no answer in Lyntai's records at `e010099f`.*

Both are cited by one quest id, `0d4eba2a5815`: D213's direction note gives it for AG20 (`D213.md:417`) and the AGENTLYNTAI1 row for the
pre-release build (`TASKS.md:223`). Whether one quest carries both is *unmeasured*: the service was not asked.

## 14. Records that disagree, found while reading

1. **D213's direction note** says Lyntai records the one-account rule *as its AG20, amending its D225* (`D213.md:415-417`). At `e010099f`
   AG20 is a decision-only row whose *yes* and *no* are both open (`Lyntai/TASKS.md:388-394`), and D225 stands with no amendment
   (`Lyntai/docs/DECISIONS.md:6431-6455`).
2. **One quest id for two requests** (§13).
3. **The provider design's rows end at u.** The AGENTLYNTAI1 row asks about *rows c to w* (`TASKS.md:223`), as do the archive's
   AGENTPROVIDER1a and 1b outcomes (*b to w*, *c to w*, `docs/task-archive.md:21606`, `:21624`); §11 has rows a to u, the corrections
   s-i to s-ix on and t (`docs/2026-10-26-agent-provider-design.md:1027-1153`), and w-s, w and x are FOUNDATION1's (its §2.8, `:490-492`),
   as Lyntai's study also notes (`:23-25`).
4. **The registry's summary is stale.** `ProviderRegistry.cs:13` says nothing reads it but its tests; since row b the verbs' operands
   and the roster's listing do (`Agents/AgentsCommand.cs:654`; `Agents/HarnessRoster.cs:861`), beside the readers of `Providers` (§0).
5. **Rows a and b are on the batch line.** The AGENTPROVIDER1 row says they are built (`TASKS.md:222`); at `a3e226e58` main holds none
   of `Agents/Contract/` or `Agents/Registry/`, so the study's *row a on a branch and not yet on its main line* (`:14`) still holds of main.
6. **The design's order ran before Part A's end.** Rows h, l, m, n, o and q were to land before mod-xxi was opened
   (`docs/2026-10-26-agent-provider-design.md:1004-1005`); mod-xxi landed on 2026-10-29 with the structure census at no family
   (`D212.md:1458`), before any of them.

## 15. The count

57 Daoris sites of the provider layer as built (§0's table): 34 types of the contract, 9 of the registry, 5 files of the stand-in, 8
case families of the conformance suite and the census. Lyntai answers 5 today, partly answers 21 and has nothing for 31. Beneath them,
745 counted sites in 172 files that the design's rows move or keep (§7), 339 of them behaviour; and 11 open Lyntai rows, AG10 to AG20,
that the study files against the design's rows (§9), one of them, AG20, a ruling.
