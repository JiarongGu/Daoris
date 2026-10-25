# AGENTPROVIDER1: Codex's sealed proposal (2026-10-26)

One of two sealed proposals written from `docs/2026-10-26-agent-provider-brief.md` and `docs/2026-10-26-agent-provider-survey.md` under D191, by Codex, read-only, at `9510a5390`, without the other. Kept verbatim, its headings set one level down; the steward ran it from outside the install. The other is `docs/2026-10-26-agent-provider-proposal-opus.md`.

## The proposal
## AGENTPROVIDER1 — proposal

**A provider represents an agent, with one or more doors. Daoris selects the work, account and model; the provider translates that settled selection into the agent’s mechanisms and translates its observations back into Daoris’s vocabulary.**

The contract is a composition of capabilities behind one registration. It does not make every agent implement a large interface full of ineffective default methods.

This proposal is based on checkout HEAD `9510a5390e1a5eabd9fc6dcc8190437abd4e0acc`. I read the brief and survey whole, FOUNDATION1’s module map and relevant participant, start, event and storage contracts, and checked the implementation cited below against HEAD. The survey’s 350-site count is its historical census, not a count I recomputed. No files were changed, tests run, installed home inspected, or other maker’s proposal read.

Paths below use the survey’s aliases: `D/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`; `M/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`; `Core/` is `src/Daoris.Service/Daoris.Service.Core/`; `Web/` is `src/Daoris.Web/src/`. **F** means `docs/2026-10-25-foundation-design.md`; **S** means `docs/2026-10-26-agent-provider-survey.md`. Proposed filenames describe the destination after mod-ix.

### 1. The contract’s shape

#### One provider, several doors

Register these providers initially:

| Provider id | Doors, preserving today’s runnable ids |
|---|---|
| `claude-code` | `claude-code`, `claude-code-acp` |
| `codex` | `codex-acp` |
| `dsh` | `dsh` |
| `stub` | `stub`, `acp-stub` |

A provider may have account-management and toolchain facilities without a runnable door. This absorbs today’s Codex holder instead of making “an adapter that is not an adapter” a permanent concept.

That distinction exists today: `AdapterSet.Built` registers six adapters and a separate holder (`D/Agents/Adapters/Adapters.cs:2548`), while `CodexAccounts.Toolchain` declares Codex’s account mechanisms (`D/Accounts/SignIns/CodexAccounts.cs:30`). Accounts and pins already have different identities: the owner determines accounts, while the runnable adapter determines its pin (S §1–§3; `D/Accounts/Harnesses/Harnesses.cs:278`, `D/Accounts/Harnesses/HarnessSettings.cs:36`).

A door id is opaque outside providers. Code may carry, compare for exact identity, store and display one. It may not infer behavior from its spelling.

#### A composed definition and a live run

Introduce:

```text
IAgentProvider
  Identity
  Toolchains
  Accounts
  Doors

IAgentDoor
  Descriptor
  Capabilities
  Models
  Access
  Connectors
  Conversations
  Failures
  Prepare(supplied start values)
  Attach(started process I/O)

IAgentRun
  Open or Resume
  ApplyChoice
  Send
  Steer
  StopTurn
  StopTask
  Close
  Observations
```

These are small typed facets, not a requirement that every operation exists. `IAgentRun` exposes supported operations through its effective capabilities; unsupported requests return a closed refusal before writing anything.

The start has distinct stages:

1. Roles supplies the settled selection.
2. The provider prepares its process and carrier recipes.
3. Platform starts and contains the process.
4. The door opens or resumes, reporting its negotiated capabilities and offers.
5. Roles handles a refused choice through its existing selection machinery.
6. The run receives words through Course’s dispatcher.

The provider never walks accounts, chooses another model, starts another participant, or decides whether a line should continue. FOUNDATION1 explicitly gives the settled selection and reservation to `StartPlan.Resolve` (F §2.4, `:437`); containment remains `AgentStarts.StartAsync` (`D/Platform/AgentStarts.cs:30`).

#### Capabilities carry evidence and applicability

Each capability has:

```text
key
state: supported | unsupported | unknown
declared evidence
negotiated observation, if any
measurement, if any
applicability
fallback
```

Applicability includes the relevant door, agent and adapter versions, posture, and configuration fingerprint. Safety measurements additionally retain the operating system, writable roots and network conditions required by COAGENT1.

Declared, negotiated and measured are separate evidence, not an ascending score where a handshake can replace a measurement. A previously negotiated roster reading describes that opening; a new run must negotiate again. A measurement for another effective configuration cannot admit a writing partner.

`AgentCapabilities` is the read of these sources. It is COAGENT1e’s sheet, not another registry to maintain. COAGENT1 already specifies these sources and version boundaries (`docs/2026-10-22-coagent-design.md:295`, `:336`).

Silence has explicit consequences:

| Missing answer | Result |
|---|---|
| No interactive facility | One-shot work only |
| No structured stream | Text console; unknown conversation, model and usage |
| No negotiated resume | Resume unavailable |
| No measured fail-closed resume | Ineligible for a limit resume requiring it |
| No steering evidence | Words wait for the supported boundary |
| No full or asking posture | Refuse that start |
| No rule carrier | State that Daoris’s rules do not reach the agent |
| No error match | Fatal, preserving D210 |
| No usage or model report | Unknown, never zero or the requested model |
| Unknown runnable id | Refuse, naming available choices |

The legacy bridge preserves existing adapter behavior explicitly. In particular, it records today’s protocol-resume default as eligibility to attempt negotiation, not proof that resume is supported. Today `ISessionAdapter.Resumes` defaults from `Wire` (`D/Agents/Adapters/Adapters.cs:1506`), whereas the actual resume method is negotiated (`D/Agents/Acp/Acp.cs:3461`).

#### Placement on FOUNDATION1’s map

| Owner | Types and responsibilities |
|---|---|
| **Platform, 1** | `ProcessRecipe`, started process I/O, environment application, clocks and containment. No provider selection. |
| **Store, 2** | Persistence primitives and migrations. No interpretation of provider identity. |
| **Settings, 3** | Parsing and preserving opaque configured ids and values. No concrete-provider defaults. |
| **Service, 4** | Existing record and account-change wire shapes. No provider catalog or machine-account lookup. |
| **Install, 5** | `ToolRecipe`, package/channel sources, verification and pin operations. Providers supply recipes or implement Install-owned source ports. |
| **Instance, 6** | Generic verb routing and leases. No references to concrete agent commands. |
| **Events, 7** | `AgentObservation`, normalized tool/turn/child/usage/failure value shapes, observation provenance and existing `SessionEvent` vocabulary. `DaorisEvent`, `EventCodes` and hubs stay here. |
| **Knowledge, 8** | Neutral `ConnectorSpec`, room facts and ports for obtaining agent facts. |
| **Agents, 9** | Provider/door ids, descriptors, registry, capability resolution, account mechanisms, model carriers, codecs, error reading, conversation-layout recipes and live agent runs. |
| **Workplace, 10** | Git/environment preparation and filesystem execution within approved roots. Supplies values or an Agents-owned port implementation. |
| **Roles, 11** | Staffing, model-choice policy and `StartPlan`. Converts its selection into Agents-owned start values. |
| **Participants, 12** | FOUNDATION1’s `Binding`, admission, lineage identity and its fold over normalized child facts. |
| **Context, 13** | Composed instructions, transcripts, briefs and execution of approved conversation carries. |
| **Course, 14** | Continuation, hand-over, move recovery and word delivery. Implements Agents-owned receipt callbacks. |
| **Higher subdomains and Loop** | Work policies, orchestration and composition. Supply values and implement lower-owned ports. |

The proposed source boundary is:

```text
Agents/Contract/
Agents/Domain/
Agents/Providers/ClaudeCode/
Agents/Providers/Codex/
Agents/Providers/DeepSeekHarness/
Agents/Providers/StandIn/
Agents/Providers/Declared/
Agents/Providers/Transports/Acp/
```

The shared ACP implementation sits inside the provider implementation boundary. Callers see neither `SessionWire` nor ACP types. Namespace changes remain FOUNDATION1’s later namespace rows.

The contract never takes `Participant.Binding`, `ActorAsked`, `ChatMessage`, `SessionTarget`, `PluginCatalog` or a review/work-order record. It takes the values it needs in types owned by Agents or below. Those values are snapshots of the existing binding and selection, not competing records.

#### Cutting the existing edges

The exact exception families are already recorded in `Driver.Tests/fixtures/structure.json:1307`.

| Existing edge | Cut |
|---|---|
| Agents → Context | Context supplies the composed instruction. Agents emits normalized observations through an Events-owned sink; Context records and renders them. No `SessionStreams`, transcript writer or instruction accountant in a codec. |
| Agents → Course | Replace `ChatMessage`, `DrivenInbox` and `SaidDoor` with Agents-owned outbound-word values and receipt ports. Course retains persistence and ordering. |
| Agents → Help | Pass a resolved posture request; remove `HelpModes` from ACP. |
| Agents → Participants | Supply resolved access values, hook artifacts and setup-change observations. Participants consumes normalized lineage; providers do not call its fold. |
| Agents → Plugins | Plugins converts its manifest into an Agents-owned declaration. Loop registers it. Agents never reads `PluginCatalog`. |
| Agents → Review | Move background-task observation values down to Events; supply review-specific environment and instruction values. |
| Agents → Roles | Replace `ActorAsked`, `ActorValue` and walk callbacks with model/effort values, carrier acknowledgements and refusal results. Roles owns the next selection. |
| Agents → Work orders | Compose quest, ask and brainstorm instructions above Agents. Supply role environment and retry eligibility through values or ports. |
| Agents → Workplace | Prepare Git values above Agents; provide bounded filesystem/trust operations through Agents-owned ports. |
| Service → Agents | Keep the account-change wire shape in Service; Agents converts to it. Service does not construct `AccountLine`. |
| Install → Agents | Install owns tool recipes and pin operations. Agents owns harness wiring and supplies the selected tool handle. |
| Events → Agents | Move observation payloads down: end facts, model refusal, token/model attribution and transient-episode summaries. Events never receives a live `HarnessEnding` or ACP exception. |
| Knowledge → Agents | Knowledge declares its fact-reader port; Loop implements it using Agents. Move `AcpMcpServer`’s neutral server description to Knowledge. |
| Instance → Agents | Declare route/lease metadata in Instance’s shape; concrete commands register from above. |

This addresses the lower crossings too, including `Install names Agents` at `structure.json:1002`, `Events names Agents` at `:1097`, and `Knowledge names Agents` at `:1186`. Merely moving concrete files under `Providers/` would not cut them.

### 2. Answers to the brief’s open questions

**1. The contract.** Use the composed facets above, with one registration and one effective capability sheet. Required identity and preparation are explicit; optional behavior has evidence and a fallback. This prevents a provider from “implementing” rules or resume through an empty default method. Today’s defaults and twenty-one-member seam are visible at `D/Agents/Adapters/Adapters.cs:1321` (S §0, §6).

**2. Provider, agent and door.** A provider is an agent with doors. `AccountOf` becomes an explicit account-owner reference on a door, resolved once by the catalog. A plugin door may name another provider’s account mechanism without replacing that provider’s identity. `codex` remains a valid account-management id without becoming a runnable alias. This rules out accidentally making `codex` launch whichever Codex door happens to be first. Evidence: S §1; `D/Agents/Adapters/Adapters.cs:2507`, `D/Accounts/Harnesses/Harnesses.cs:281`.

**3. The door.** Keep ACP as one shared protocol transport, parameterized by provider-owned extension codecs. Providers may also supply native or future transports. Claude’s `StructuredOutput` and `ClaudeStreams` move together into its provider. Steering, keeping-stop, background-task control, permission holding and fail-closed resume are individual capabilities. This prevents an ACP-shaped door from acquiring Claude’s queueing behavior: today queueing reads `claudeCode.promptQueueing`, and steering requires a second answer (`D/Agents/Acp/Acp.cs:1714`, `:1730`; S §6–§8).

**4. Plugins.** `DeclaredAcpAdapter` becomes a declaration-backed provider/door factory in Agents. Plugins supplies a validated declaration, including provenance. A manifest may describe identity, command, package, version question, account reference, home variable and posture words. It may not assert rules, limit grammar, lineage, confinement, steering or resume. The latter require the evidence source COAGENT1 assigns them. New full/asking manifest fields land in their own reviewed row; moving the factory grants nothing. This rules out a manifest granting itself a measured safety capability. Today’s declaration is `D/Plugins/DeclaredAcpAdapter.cs:21`; COAGENT1’s restriction is at `docs/2026-10-22-coagent-design.md:354` (S §10).

**5. Accounts.** Split `HarnessToolchain` into toolchain, account, access and door/reporting facets. The provider’s account mechanism owns sign-in, login status, trust, credential binding, usage questions, windows, reset semantics and minimum read interval. A key account is an opaque credential reference; only the selected provider’s launch receives its secret. A door explicitly delegates account facilities to its owner. Turn-report meaning and transport authentication codes remain door-specific. This prevents Codex’s account facts being stranded on `codex-acp`, and prevents inheriting another server’s `-32000` meaning. Evidence: S §3; `D/Accounts/Harnesses/Harnesses.cs:176`, `D/Agents/Errors/AgentErrors.cs:167`.

**6. Models.** Expose separate offers for session selection and stored account defaults, each with source, version and account applicability. ACP keeps its category-based carrier; native flags and account-settings schemas belong to the provider. Return requested, carried, acknowledged and reported values separately. Roles retains D205’s judge and next-choice policy. This rules out reporting a requested model as the one that ran, or assuming one effort list applies to every scope. Evidence: S §4; `D/Agents/Acp/Acp.cs:600`, `:667`, `:3043`; `D/Sessions/Domain/ActorRun.cs:53`.

**7. The session’s life.** Providers implement open, resume, word framing, interrupt and close mechanisms. Course decides whether to resume, go on, hand over or fall back to a brief. Context executes conversation carries from a provider’s versioned layout recipe, under the move’s recovery protocol. Cross-door or cross-version carry requires an explicit compatible recipe; sharing a provider id is insufficient. This prevents a Claude native conversation being handed to its ACP door merely because both use one account pool. Evidence: S §5; `D/Loop/DriverContinuations.cs:40`, `:54`, `:387`; SWITCHCTX1’s layout table remains its proposed evidence input.

**8. The stream.** Every run emits the normalized observations in §3, with unsupported and unknown fields explicit. Only the provider reads its own wire extensions. FOUNDATION1’s hubs and record projection mint lifecycle events. This rules out turning a protocol `end_turn` into a completed part. The code explicitly distinguishes these today (`D/Sessions/Domain/SessionEvents.cs:33`; S §8).

**9. Connectors.** Pass a single `ConnectorPlan`: selected servers, per-server environment, session identity, required inclusions/exclusions and the permitted sources. The provider returns how that plan can be carried and whether it can honor the exclusions. CONNECTORS1 owns the policy. A provider that cannot exclude inherited account/repository servers cannot claim isolation; an exclusion-dependent start is refused. This rules out assuming every server inherits the parent environment. Today connector scope is already carried in the entry (`D/Service/KnowledgeConnector.cs:118`), while native file serialization is Claude-specific (`D/Agents/Permissions/SpawnServers.cs:24`; S §9).

**10. Permissions, skills and hooks.** Providers render resolved access requests into their own posture and rule mechanisms. Existing Claude rule expressions remain a provider-specific language; this design does not invent a universal translator. Skills and hooks use explicit carriers with evidence. Codex continues to receive no Claude rules or plugins. The page and Ask Daoris say that those restrictions do not reach that agent. Claude’s lineage hook remains Claude’s codec and artifact; normalized child evidence is general. This rules out presenting a copied `.claude/settings.json` as enforcement on Codex. Evidence: S §10; `D/Agents/Adapters/Adapters.cs:1417`, `:1889`, `:2323`; `D/Agents/Permissions/SpawnSkills.cs:6`.

**11. Process and environment.** The provider contributes its executable, arguments and provider environment changes. Platform applies the common process policy; higher owners supply target and role variables, Git wiring and composed instructions. Before launching, remove registered provider credential/home variables from inherited environment, then bind only the selected account’s permitted values. Providers cannot overwrite Daoris identity or role fields. This rules out both a leaked key and Claude’s executable override reaching Codex. Today composition occurs inside `Spawning` and the Claude override is applied generically (`D/Agents/Adapters/Adapters.cs:1540`, `:1659`; `D/Accounts/Harnesses/HarnessSelection.cs:565`; S §7, §11).

**12. Errors.** Keep one `AgentErrors.Read`, backed by provider-owned evidence. A door first decodes its failure into a neutral failure value; the reader preserves D210’s precedence and own-then-owner rules. Replace behavioral use of the door string with opaque identity plus failure phase. One typed failure outcome feeds retry, cooling, sign-out and conclusion. Unknown errors remain fatal; Codex gains classifications only with recorded evidence. This rules out a pipe limit being lost in a second failure channel and a transcript sentence being mistaken for a limit. Evidence: S §12; `D/Agents/Errors/AgentErrors.cs:133`, `:144`; `D/Loop/Driver.cs:3233`, `:3855`.

**13. The map.** Use the placement and edge cuts in §1. Provider files may name only Agents and lower subdomains. Pure decoding and capability judgement live in `Domain/`; process execution and file access do not. Service-side metadata readers remain independent provider-scoped twins, not a dependency on the desktop driver. This rules out fixing the Agents cycle by creating an Agents → Plugins or Service → desktop dependency. Evidence: F §14.3, `:2363`; the exact crossing list starts at `Driver.Tests/fixtures/structure.json:1307`.

**14. Records.** Keep existing strings and meanings; resolve them through immutable aliases. New structured binding facts are machine-local and introduced only after every reader uses the contract, following FOUNDATION1’s storage rows. See §4. This rules out changing `Adapter = claude-code` from “native door” to “choose any Claude door.” Evidence: S §14; `D/Conversations/GoOn/HarnessConversations.cs:31`, `D/Accounts/Usage/UsageTurns.cs:19`.

**15. The page, service and tools.** The driver supplies grouped agents, door descriptions, available actions, capability explanations, model offers, sign-in progress and eligibility verdicts. The page renders them. Service records opaque identities and applies its admission rules; its provider-scoped call codecs read only necessary metadata. The CLI forwards runtime operations. Offline doctrine layouts stay independent and twinned with the service. This rules out `doors[0]` choosing an account action and `wire !== acp` implying “direct.” Evidence: S §1–§3, §13; `Web/agents/tools.ts:215`, `:238`; `Core/Sessions/CallLineage.cs:65`.

**16. The disagreements.** Settle them explicitly as follows. Each behavior change gets its own decision note and regression case, separate from the mechanical move.

| Disagreement | Answer and reason | Counterexample excluded |
|---|---|---|
| The machine’s door | There is no machine-wide behavioral door. Judge a selected start or an existing binding. Before selection, return eligibility per candidate through the same start judge. Unknown ids refuse. | One ACP choice makes a native choice appear connector-capable. Today’s competing rules are S §6; `D/Setup/SetupWorld.cs:20` even defaults unknown to pipe. |
| Native agent on an unadopted repository | Preserve today’s native driven-path restriction as an explicit connector-delivery condition. Other rooms retain their existing explicit handoff. Enabling connector injection for ordinary native driven starts is a separate measured change. | A refactor silently starts driving a repository through a newly injected connector. Current restriction: `D/Setup/SetupPress.cs:76`; carrier: `D/Agents/Permissions/SpawnServers.cs:60` (S §9). |
| “Resumes its own conversation” | Use the prior binding and an explicit compatible resume recipe, not the current machine choice or provider equality. | A changed default resumes the wrong agent’s conversation. `D/Loop/DriverContinuations.cs:40`, `:54` (S §5). |
| Stand-in detection | Declare `ExecutionKind = stand-in`; neither empty binary nor name identifies it. Its production admission remains prohibited. | A real wrapper with a configured command is counted as a model-free rehearsal. Existing conflicting sites: S §1; `D/Agents/AgentsCommand.cs:653`, `D/Platform/DriverChoicesCommand.cs:2195`. |
| Stand-in tables | Give stand-ins their own documented synthetic vocabulary. Keep Claude-shaped compatibility fixtures under Claude’s provider. | A passing stand-in accidentally proves only Claude’s grammar (S §2, §12; `D/Agents/Adapters/Adapters.cs:1709`). |
| Efforts | Separate session offers from account-default values. `max` is not categorically invalid: the settings writer refuses it as a persistent default, while the session carrier is separate. Remove the page’s universal suggestion list. | Applying the account-settings restriction to all session choices. `D/Accounts/AgentSettings.cs:65`, `:185`; `D/Sessions/Domain/ActorRun.cs:53`; `Web/settings/ai/Actors.tsx:27` (S §4). |
| Read floor | The provider’s usage question supplies freshness everywhere, including switch candidates and the page. | Repeated Codex reads caused by a universal one-minute candidate floor. `D/Accounts/Harnesses/HarnessRoster.cs:2129`; `D/Accounts/Domain/SwitchDecision.cs:71` (S §3). |
| Token weights | Preserve raw history. Make estimation use a versioned, sourced calibration applicable to provider/model/report meaning. Unknown calibration produces unknown estimation; it does not borrow Claude’s weights. | Treating Codex cumulative or unknown counts as Claude per-turn equivalents. Current constants: `D/Accounts/Domain/UsageProjection.cs:122`; D179 already requires per-maker/model tables at `docs/decisions/D179.md:44` (S §3). |
| Claude override on every ACP spawn | Only Claude’s provider resolves and supplies that executable dependency. | A Codex start receives `CLAUDE_CODE_EXECUTABLE`. `D/Accounts/Harnesses/HarnessSelection.cs:565` (S §11). |
| `ProfileMustExist` | Replace the unused distinction with the effective current policy: managed home accounts are provisioned before launch. Do not begin refusing absent homes during a move. | “Cleaning up” the field changes Codex’s first launch. Actual behavior: `D/Accounts/Harnesses/HarnessSelection.cs:576` (S §3). |
| `WeekFixed` | One provider-owned reset policy serves driver and modules. Wiring the driver to it is an explicit D130 correction. | The page carries a weekly reset while scheduling drops it. Current module read: `M/Bridge/Routes/AccountsRoutes.cs:62` (S §3). |
| Split failure channels | One classified outcome drives conclusion and account effects. Land the native cooling/sign-out correction explicitly. | A structured native limit receives a strike. `D/Loop/Driver.cs:2842`, `:3237`, `:3855` (S §12). |

The affected decision notes include D23/D53 for identity and doors, D64/D199 for declarations, D98/D205 for model scopes, D125/D130/D179 for accounts and estimation, and D210 for failure consumption. D212’s ownership and event semantics remain intact.

**17. The names.** On the window keep **agent / 智能体**, product names unchanged, and **way in / 接入方式**, with today’s direct/protocol wording. `Provider` is an implementation term, not a new product screen. Keep *harness* and *profile* out of chrome. In new code use `ProviderId`, `DoorId`, `AgentRun` and `NativeConversation`; retain serialized `adapter`, `harness` and `door` fields where compatibility requires them. Native transport code uses `NativeStdio`; terminal named-pipe code keeps its own terminology. This rules out showing two “agents” for one product or confusing a transport with the terminal’s pipe. Evidence: S §1; `Web/locales/glossary.json:594`; `Web/agents/tools.ts:238`.

**18. The order.** Land the contract and census, build the generic compatibility/conformance path, add the independent stand-in, migrate individual mechanisms and providers, cut consumers, settle disagreements, verify records, then build OPENROUTER1. Detailed rows are §5. This prevents OpenRouter from becoming the reason to bypass unfinished account or failure seams.

**19. What is the owner’s.** Only the concrete decisions in §6. Architectural facet names, ports and row boundaries go through the foundation readers and steward. This avoids asking the owner to resolve implementation choices already delegated by D191 (`docs/decisions/D191.md:19`).

**20. Proof.** Every row has contract, census and relevant behavioral proof; process mechanics use real model-free subprocesses. Installed measurements remain separate, parent-run evidence. §5 names the suites and acceptance cases. This rules out presenting a mocked bridge or a replayed fixture as proof that the installed agent resumes or cools correctly.

### 3. The normalized event stream

#### Observations first; lifecycle events at their existing owners

A provider emits an ordered local observation stream for one run generation. It does not mint `DaorisEvent`, choose a journal position, or allocate the hub’s sequence.

The Events-owned observation schema contains:

- Source run and correlation ids: conversation, turn, call, child and parent, where known.
- A typed observation payload.
- Provenance: agent-reported, transport-observed, computed or inferred.
- Explicit unknowns.
- Replay status, so resumed history cannot become new activity.
- Terminal coverage: what the run observed, and what remains unresolved.

Provider-native identifiers and raw payloads stay machine-local. The hub maps correlations to permitted generated ids before an outside projection. Raw words, paths, account names, model strings and provider error sentences are not facts in FOUNDATION1’s outward event envelope.

F §4.1 defines the envelope and minters at `:1090`; F §4.3 expressly keeps output channels as streams at `:1183`.

| Provider observation | Existing local representation | FOUNDATION1 mapping |
|---|---|---|
| Run opened; native conversation named | Binding update; opening diagnostic | `part.opened` comes from the service record opening, once—not from the handshake. |
| Turn started or prompt accepted | `user` plus correlated receipt where observed | No new lifecycle code. `words.taken` remains the dispatcher/store fact. |
| Message, thought, plan | `message`, `thought`, `plan` | Stream only. |
| Tool announced, updated, finished or cut | `tool`, merged by call identity | Stream only. A genuinely held permission projects `call.held`. |
| Turn ended | `turn`, normalized stop category and retained native reason | The running process’s answer judge may mint `turn.answered`; a stopped or failed turn is not automatically an answer. |
| Child announced or discovered through a call | Normalized child evidence; separate child stream | Participants establishes the child identity; its owning process tells `child.seen`. |
| Child explicitly ended | Child end evidence | `child.ended`, with explicit-end provenance. |
| Parent ended without a child-end report | Participants closes observation of the child; native outcome remains unknown | If `child.ended` is projected for that closure, its facts must identify parent-derived closure, not successful completion. |
| Usage, context pressure, model report | `usage` and turn attribution; source and meaning retained | Stream/history only. No invented lifecycle code. |
| Account-window update | Normalized account reading | Not a limit by itself. Account judgement may later produce `account.waits`. |
| Structured failure | Classified error observation and local diagnostic | No automatic part event. Retry may continue the same conversation. |
| Limit that ends a part | Typed limit outcome | Record projection produces `part.limited` and `part.ended` with class `limited`; account owner tells `account.waits`. |
| Retry waiting, trying or exhausted | Existing `transient` stream state | Exhaustion can result in `part.ended` class `transient-bound`. |
| Process exit, stream loss or close | Terminal observation, exit and unresolved facts | Conclusion owner updates the record; projection produces `part.ended` with its judged class. |

The accepted ending classes remain those in F §4.2, `:1152`: `completed`, `failed`, `limited`, `transient-bound`, `access`, the three stopped classes, `refused-choice`, `awaits-helper`, `stood-down`, and `unknown`.

The local `SessionEvent` kinds already separate messages, tools, usage, turns and receipts (`D/Sessions/Domain/SessionEvents.cs:13`). Extend payloads compatibly where needed; do not redefine those kinds as lifecycle events.

#### Where native wires are read

| Input | Reader after migration |
|---|---|
| Standard ACP request, response, update, permission and resume frames | `Agents/Providers/Transports/Acp/` |
| Claude ACP `_meta`, queueing, wake/background, child, limit and model extensions | `Agents/Providers/ClaudeCode/AcpCodec.cs` and its state reducer |
| Codex-specific refusal and metadata extensions | `Agents/Providers/Codex/AcpCodec.cs` |
| Claude native `stream-json`, control messages, children, usage and failed result | `Agents/Providers/ClaudeCode/NativeCodec.cs` |
| DeepSeek-specific profile and environment behavior | `Agents/Providers/DeepSeekHarness/` |
| Claude lineage-hook input | Claude provider’s hook artifact and codec |
| Connector call metadata | Independent service twins under `Core/Sessions/Providers/<provider>/` |
| Unknown frames | Bounded local diagnostic/raw stream; no guessed semantic event |

The current shared ACP reader contains Claude-specific queueing, limits and model keys (`D/Agents/Acp/Acp.cs:1714`, `:3030`, `:3043`), and Codex’s active-writer reason (`:3394`). The native capture separately invokes lineage reading before the mapper (`D/Agents/Streams/HarnessCapture.cs:123`, `:156`); `ChildBindings` also parses agent-specific evidence (`D/Sessions/Domain/ChildBindings.cs:81`). Those readers must converge on the provider codecs.

The service cannot import desktop code. Its call-metadata twins keep only validated ids and share conformance cases with the driver, following `.claude/knowledge/twins.md:9`. Today’s metadata reader is `Core/Sessions/CallLineage.cs:65`. Its rules do not move into a generic service class with a provider switch.

A Codex child discovered only through calls has no reported native ending. A missing parent remains unknown. A parent’s clean exit is evidence about observation closure, not that the child completed its work.

### 4. Records and older builds

#### Preserve identities before adding structure

Keep an immutable legacy lookup:

```text
claude-code      -> provider claude-code, door claude-code
claude-code-acp  -> provider claude-code, door claude-code-acp
codex-acp        -> provider codex,       door codex-acp
dsh             -> provider dsh,         door dsh
stub            -> provider stub,        door stub
acp-stub        -> provider stub,        door acp-stub
```

`codex` resolves for account management, not for starting a session. Provider and runnable-id lookup are distinct operations.

Disabled or uninstalled plugin declarations retain their identity information. Historical reading may return an unavailable descriptor preserving the original id; starting still refuses. Neither case guesses a maker or chooses another door.

| Record family from S §14 | Kept identity and compatibility treatment |
|---|---|
| Service session | Keep `Adapter`, `HarnessVersion`, local `Profile` and their meanings. Derive provider identity for new readers. |
| Actor asked/ran | Keep the recorded choice and provenance; do not replace a door id with a provider id in place. |
| `<session>.harness.json` | Keep `adapter` and native conversation id until FOUNDATION1 imports the binding. |
| Ask Daoris `.wired.json` | Preserve historical harness/door spellings. New receipts use contract facts; old receipts use a compatibility reader. |
| Word reach | Keep `turn-end`, `next-step`, `now`; a transport refactor does not reinterpret delivery. |
| Notes and resume reasons | Keep existing codes, including `resumed.native`; old text remains historical evidence. |
| `SessionEnded` | Preserve its compatibility projection and adapter value. |
| Usage history and `TurnBinding` | Preserve owner/account, runnable door and report meaning. New readers group by resolved provider without rewriting history. |
| Offers | Preserve runnable-id keys; new metadata adds applicability without changing an old reading’s meaning. |
| Machine log | Preserve historical fields; new diagnostics carry structured identity. No prose parsing. |
| Opinion state | Preserve adapter, maker and posture as recorded, rather than recomputing historical independence. |
| Brainstorm and meeting files | Preserve adapter/maker and stand-in declarations. |
| Hand-over/helper/partner/go-on requests | Their existing `agent` string remains a runnable choice. |
| Trust holds | Remain local; provider trust logic interprets them through a compatibility reader. |
| `driver.json` | Keep old keys and opaque ids. Provider defaults come from the catalog, without rewriting configured choices. |
| Pins and account homes | Keep versions keyed by runnable id and homes keyed by account owner. No directory renaming. |

Representative writers are `D/Conversations/GoOn/HarnessConversations.cs:31`, `D/Accounts/Usage/UsageTurns.cs:19`, `D/Review/Domain/OpinionAsk.cs:53`, `D/Brainstorms/BrainstormFiles.cs:219` and `D/Conversations/HandOver/HandOverFiles.cs:27`.

Existing records need no identity-field rewrite. Once FOUNDATION1’s binding storage is available, richer provider/version/evidence snapshots can live in a new Participants-owned binding-details table keyed by binding and process generation. It is descriptive metadata, not another selection or conversation authority. Older builds never inspect that table.

FOUNDATION1 retains ownership of the actual migrations:

- Agents: `agent_offers`, `harness_wiring`, account and usage families.
- Participants: `bindings`.
- Help: receipt/link storage.
- Roles: staffing.
- Store: import receipts and writer-version enforcement.

Use F §13.5’s confirmation and rollback rules. Actable conversation files are not moved aside before the start’s confirmation is settled. Backups are retained; this proposal authorizes no deletion or new dual writer.

#### What “an older build reads it” means

For existing providers, an older build sees the same strings and shapes it understands. Every migration must run its actual old reader and writer statements against copied data, including unknown JSON fields and interleaved writes.

For a newly added provider, an older build can preserve and display the opaque adapter string; it cannot execute that provider. No compatibility claim promises otherwise. The older executable must be tested on those records to establish that unsupported continuation refuses instead of substituting its configured default. A new flag is not protection from an older executable that never reads it; any new actable record that requires such protection must remain in a new table outside that build’s reach.

The sync shape stays unchanged. It already carries adapter and harness version while structurally omitting profile and tree (`Core/Sessions/SessionFeed.cs:22`, `:106`; `Core/Remote/SessionSync.cs:58`). Provider credentials, homes, bindings, negotiated capabilities and raw observations acquire no sync fields.

### 5. Rows and proof, after mod-ix

These rows start only after mod-ix. They use Agents-owned value ports while FOUNDATION1’s later contracts are still being built; they do not implement a substitute start plan, event hub or store.

Every row names its exact census removals. Behavior corrections are separate branches from moves.

| Row | What moves behind the contract | What changes for a person | Proof |
|---|---|---|---|
| **AP-a — contract and census** | Add `Agents/Contract/`, immutable identity lookup and the initial provider census beside the existing seam. Move no reader. Sized for one day. | Nothing. | Compile; capability/alias unit cases; census passes its exact baseline and fails on planted upward edges, literals and concrete-provider references. |
| **AP-b — compatibility runner and test infrastructure** | Add the adapter bridge and generic run/receipt/observation path. Exercise existing stand-ins through it. Parameterize conformance and rehearsal discovery before adding a provider. | Nothing. | Old/new behavior comparison for both stand-ins; real-process open, send, stop and resume; generic fake install/login/pin support; no production failure-policy change. |
| **AP-c — independent stand-in** | Add `Providers/ProbeStandIn/` and one registration. Its own synthetic wire, account vocabulary and fixtures implement the full path. | Available only in explicit development/test configuration. | The addition diff contains only its folder and registration. Generic suites discover it without another edit. All cases below pass. |
| **AP-d1 — Claude toolchain/accounts** | Move Claude release source, sign-in, trust, settings, usage, windows and credential facts into its provider. | Nothing. | Account/toolchain suites; pin verification and environment isolation; both doors use one account mechanism. |
| **AP-d2 — Codex toolchain/accounts** | Absorb the holder and scattered account facts into Codex’s provider. | Nothing. | `codex` account actions and `codex-acp` starts retain distinct identities; device-code/status/usage fixtures and real-process cases. |
| **AP-d3 — remaining declarations** | Move DeepSeek and existing stand-in account/toolchain declarations. | Nothing. | No-model detection is explicit; no production start admitted by stand-in evidence. |
| **AP-e — Claude native codec** | Move `StructuredOutput`, `ClaudeStreams`, native failure and duplicated native lineage/ending readers. | Nothing. | Stream, native-child, denial, model-refusal and ending suites; one input frame cannot create duplicate output or lineage facts. |
| **AP-f — native lifecycle** | Put native turn framing, interrupt, resume and retry-before-conversation-name behind the run interface. Driven work and chats share it. | Nothing. | Native chat/go-on/resume Process suites; exact receipt ordering and no repeated attempted word. |
| **AP-g — Claude ACP provider** | Extract Claude extensions, background/wake behavior and spawn dependencies from shared ACP. | Nothing. | Whole ACP background/steering/transient suites; extension fixtures; parent’s installed read/resume/cool/end proof. |
| **AP-h — Codex ACP provider** | Extract its refusal vocabulary and resume collision handling. | Nothing. | Resume/replay and active-writer cases; unknown model/count meaning remains unknown; parent’s installed proof. |
| **AP-i — declaration-backed doors** | Move plugin construction into Agents; Plugins supplies values. Retain existing capabilities. Add any new manifest posture fields separately under COAGENT1’s review. | Nothing from the move. | Manifest rejection cases, provenance, reserved ids and absent-capability conformance. |
| **AP-j — callers and dependency cuts** | Remove door branches from loops, Course, Participants and lower readers using §1’s ports. Feed FOUNDATION1’s start plan and hubs once their rows exist. | Nothing from the move. | Structure census; driven/chat equivalence; mixed-choice eligibility; dispatcher and admission suites. |
| **AP-k — page, service and tools** | Driver-owned roster/actions/offers/sign-in facts; provider-scoped service codecs and hook artifacts; generic CLI/help. | Same controls and wording, backed by one answer. | Web/module/service suites, twin tables, en/zh checks; no `doors[0]` or agent-text parser decides an action. |
| **AP-l1…lN — explicit corrections** | Land §2 question 16’s behavioral decisions separately: connector eligibility, native failure consumption, read freshness, estimation applicability, reset policy and remaining disagreements. | Only the correction named by that row. | Each planted old disagreement fails; paired old/new cases; relevant decision note. |
| **AP-m — records last** | Adopt FOUNDATION1’s migrated families and optional binding details after all identity readers use the contract. | Existing history remains readable. | Exact older-build matrix, import/rollback/interleaved-write cases, sync privacy and unchanged legacy ids. |
| **AP-n — OPENROUTER1** | Its own accepted design and first implementation on this contract. | Only its design’s explicit account/model controls. | Contract conformance, synthetic key account, limit cooling and closed-repository refusal before any paid proof. |

#### The independent stand-in

The third stand-in deliberately uses a synthetic wire unlike Claude’s formats. It supplies:

- Version, sign-in/status and local pin/install fixtures.
- Its own account variable and no real credential.
- Start, turn-taking and resume of its own conversation.
- A correlated tool call.
- A child announcement and explicit ending.
- Usage and reported model distinct from the requested model.
- Limit, access, transient and fatal failures.
- Supported and refused model/posture choices.
- Unknown and malformed frames.

Its fixtures and executable source live in its provider folder. The generic suite discovers them through registration and a common fixture layout established in AP-b. Its pin source is controlled test infrastructure, with no vendor download or account.

The proof is the diff. If AP-c needs a caller, page, service or rehearsal edit, the contract is incomplete; name and fix that gap before accepting the row.

#### The census

Use two complementary checks:

1. FOUNDATION1’s semantic type-edge census.
2. A provider-knowledge census covering the driver, host, modules, service, page, hooks and tools.

The second detects concrete-provider types, agent ids used as behavior, provider variables/files, vendor extension keys, and transport-door tests. It includes qualified references, aliases, serialized strings and wrapper helpers that merely conceal a branch.

Provider implementation boundaries include shared transports under `Agents/Providers/`, service provider-codec twins, and provider hook artifacts. Registration naming concrete implementations stays inside that boundary.

Existing exceptions are exact sites with their removing row. No new site is allowed. Fixtures, historical evidence, offline doctrine layouts and provider-specific probes are explicitly classified; they are not a blanket exemption for production branches. A terminal named pipe or Node’s stdio `"pipe"` is distinguished from an agent-door discriminator.

The completion criterion is **zero unexplained production sites outside provider boundaries**. New providers add their own identifiers to the census’s detected vocabulary through registration; they do not require manually extending a central deny-list.

#### Conformance and integration proof

Every registered provider runs the same contract suite. Mandatory cases cover identity, aliases, privacy, malformed input, effective capability resolution, preparation, failure precedence and unsupported-operation refusal. Optional facilities run their applicable cases; absence must pass its fallback case.

The behavioral matrix includes:

- New and resumed starts; missing conversation; another active writer.
- No prompt before a required model or posture is accepted, preserving D205’s measured native silent-refusal exception.
- Attempt recorded before wire write; sent receipt after write; attempted words never replayed after uncertainty.
- Limit cools the bound account and adds no strike.
- Transient retry retains its conversation and respects D210’s bounds.
- Access and fatal failures take their respective paths.
- Duplicate child signals merge; unknown parent and unknown ending remain unknown.
- Unknown counts stay null; computed context is identified as computed.
- Unrelated provider credentials and home variables are absent from a spawn.
- A provider cannot widen admission or containment.
- Old records retain their exact identity and meaning.

Run the brief’s affected suites whole, including the adapter/native/ACP, account rotation, continuation, error, permissions and lineage families. Cross-layer rows also run module, service/MCP and web suites. Integration boundaries run the full declared gates (`daoris.gates.json:5`), with real-process suites and family/deployment rehearsals serialized as required.

#### Folding in the other designs

- **FOUNDATION1:** retain its binding, selection, admission, event and storage owners.
- **MODELROLE1:** retain its judge and choice order; move provider-specific carriers and offers behind facets.
- **AGENTERRORS1:** retain its classifier precedence and retry policy; unify consumption of classified outcomes.
- **COAGENT1:** implement its capability sheet as the contract’s read; retain measurement-based partner admission.
- **COWORKVIEW1:** absorb settled observation behavior; do not accept its proposed routing through this migration.
- **SWITCHCTX1:** expose versioned conversation recipes; its carry remains Context/Course’s proposed work.
- **CONNECTORS1:** supply the handing and exclusion seam; leave its choices and page to its own design.
- **OPENROUTER1:** use the completed contract as the first new production-provider test.

OPENROUTER1 must specify the agent executor that owns tool execution, conversation and continuation; an inference endpoint alone does not satisfy this contract. Its provider supplies the key carrier, offers, observations and failure decoding. Repository closure is enforced before sending context or credentials.

Its gateway identity must not establish another-maker independence by itself. Where the actual model maker or routing is unknown, Review receives unknown independence. Paid models and their first measurements remain owner-approved.

### 6. What is the owner’s to decide

This proposal does not require the owner to choose between interfaces, folders or transport ports. Those choices go through D191’s foundation reads.

The following concrete actions remain the owner’s:

| Decision | What a yes authorizes | Review material |
|---|---|---|
| Supply an OpenRouter key and spending allowance | A bounded, named live measurement using that credential and selected model | OPENROUTER1’s account, tier and costed measurement plan |
| Sign in or authorize a new account | The specified provider’s sign-in flow | The provider’s measured account mechanism and requested account |
| Widen an agent’s authority | The exact new posture, rule carrier, connector reach or writing role | Capability evidence, admission change and both foundation reads |
| Change product direction | The named departure from today’s behavior | The proposed decision amendment |
| Delete data or end the retained rollback/export protection | The exact listed records and recovery consequence | Migration inventory, older-build proof and recovery procedure |
| Push, publish or rewrite history | The concrete reviewed operation | Green gates and final diff |

The parent performs installed-window measurements after staging, on the install: Claude and Codex start, read, resume, cool and end; prior records remain readable by the preceding build. Development fixtures and conformance runs establish mechanics. They do not substitute for that installed proof.
