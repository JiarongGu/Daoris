# One provider contract every agent implements (AGENTPROVIDER1, 2026-10-26: round 1, proposed)

**Standing (round 1, proposed under D191, D213):** the synthesis of the two sealed proposals, Codex's
(`docs/2026-10-26-agent-provider-proposal-codex.md`, *X*) and the independent Opus reader's
(`docs/2026-10-26-agent-provider-proposal-opus.md`, *O*), each written from the brief (`docs/2026-10-26-agent-provider-brief.md`) and the
survey (`docs/2026-10-26-agent-provider-survey.md`) alone and sealed before its author read the other. The brief's §2 point 1 named three
proposals, Claude's first; two were sealed, and this synthesis, written by Claude Opus 5.5 from both, is the voice the brief's point 2 gives
Claude. Where the two agree it is said once; where they differ the choice names whose it follows, why, and the case the other would get wrong
(§1.1). Every one of the brief's twenty questions is answered in §1's table and the section it names. It changes a foundation in the senses
D191's amendment of 2026-10-20 names (the seam every agent's process is started, spoken to and read through; decided rules other designs rest
on), so **both makers read this round before any of it is accepted** (D191's note of 2026-10-24; the brief's §2 point 4), and the rows marked
⚖ (§11) are built only after both makers' reads find nothing blocking in them. Nothing here is built, spends an account, signs in, publishes or
deletes. Every row lands after FOUNDATION1's row mod-ix (D212 §14.6), which has not landed.

**Read at** main `0f4d2a8a7`. The proposals read the code at `9510a5390` (whose `src/` is the survey's `f58f1be61`); since then FOUNDATION1's
mod-iii (the Settings subdomain moved, the settings' verbs split) and z-iii (the bindings in the desktop store) landed, so each line §0.2
rechecked is cited at `0f4d2a8a7`, and every other line as the survey and the proposals cite it, in files those rows did not move. Paths follow
the survey's aliases (a bare path is under the driver library; `Modules/`, `Host/`, `Core/`, `Mcp/`, `Http/`, `Web/`, `Cli/`); `Dnn` is
`docs/decisions/Dnn.md`; *X-n* is Codex's answer to the brief's question n and *O-n* the Opus reader's; *X §n* and *O §n* a section of each;
D212's rows are named *FOUNDATION1's row x* where this design's rows share a letter. Read for it: the brief, the survey and both proposals
whole; FOUNDATION1's design at §2.1 to §2.4, §2.7, §4.1 to §4.5, §10's Part B rows, §13.3 and §14; COAGENT1 §3 (D199); D23, D53, D57, D64,
D70, D179, D205 and D210 at the points this cites; `.claude/knowledge/model-decoupling.md`, `compatible-builds.md`, `session-words.md`,
`twins.md`, `connector-tools.md`, `claims-need-checks.md` and `development-documents.md`; the glossary's *agent*, *harness*, *profile* and *way
in*.

## 0. What this rests on

### 0.1 The direction

The owner's direction of 2026-10-26, as the brief §1 carries it: Daoris will support more agents than the two makers it runs, so what an agent
is to Daoris becomes one provider contract every agent implements, and every place outside a provider that tells one agent or one door from
another moves behind it; Claude Code, Codex and the stand-ins its first providers, OpenRouter (OPENROUTER1) the first new one and its proof;
designed now, built after FOUNDATION1's move of the Agents subdomain, never as a big-bang rewrite. In D193's picture it serves the staff, the
equipment and vendors, and finance: a company that hires from a new agency by one contract can take on the next at the cost of that contract.

What the two proposals share, and this design keeps without argument: a **provider is an agent with one or more doors**, today's adapter ids
kept as the doors' ids; **one registration** composes it of small parts rather than one interface of defaults, each optional fact with its
source (declared, negotiated, measured) and its fallback where absent, as COAGENT1 §3.1 has it; **Daoris selects** the work, the role, the
account and the model (FOUNDATION1's start plan, D212 §2.4) and **the provider translates** that settled selection into its agent's mechanisms
and the agent's wire back into Daoris's vocabulary, never walking an account, choosing a model, starting another participant or deciding
whether a line goes on; the protocol door stays **one shared transport**, each agent's extensions behind its own provider; the normalized
stream's vocabulary sits **in Events**, beside `SessionEvent`, and the provider **mints no event**; COAGENT1e's sheet is **a read of the
contract**; **one error reader**, D210's; **no record changes its strings**; the pipe door's stream readers move into Claude Code's provider;
Claude Code's rule grammar stays the product's rule language (D72), Claude Code's provider's own; the lineage hook stays Claude Code's;
*provider* never reaches the window; and **the census, a conformance suite and a third stand-in with a vocabulary of its own** are the proof,
OPENROUTER1 after the parts it needs.

### 0.2 Corrections to the brief, the survey and the proposals, checked

| Claim | At `0f4d2a8a7` | Who is right |
|---|---|---|
| The brief §2 point 1: three sealed proposals, Claude's first | Two were sealed, Codex's and the Opus reader's; no proposal of Claude's is on main. This synthesis is written from both | — |
| O-16 item 5 and the survey §4 (`Actors.tsx`, *a twin that already disagrees*): the driver refuses `max`, so the page should stop offering it | `max` is refused only as an **account's default** in the settings file (`Accounts/AgentSettings.cs:185-192`, whose refusal says *Choose it for one conversation instead*); a session's carrier takes it (`--effort`, `Sessions/Domain/ActorRun.cs:44`, `:57-58`). What is wrong on the page is the pipe door's words offered for every agent (`Web/settings/ai/Actors.tsx:26-27`), not `max` | X |
| X-16 (the token weights): Codex's cumulative or unknown counts are weighed as Claude Code's per-turn tokens | Already ruled out: D179's USAGEPROBE1 note weighs only a turn whose door reports its own tokens, and *a Codex turn, its meaning `unknown` … is spend nothing weighs* (`D179.md:207-210`). The weights are that note's decided interim, *used for every agent until USAGECAP1b's table stands* | O |
| O §0 item 6: the holder's terminal runs Claude Code *on Daoris's servers alone* | It runs on none: `--strict-mcp-config --mcp-config {"mcpServers":{}}` (`Modules/Platform/HolderTerminal.cs:44`). What O draws from it stands: Claude Code's native door can be held to exactly the servers it is handed | O, in kind |
| Both: the contract's rows cut *Events names Agents* (O's P10, X §1), *Install names Agents* (O's P5, X §1) and *Knowledge names Agents* (O's P14 and P15, X §1) | Each is cut **after a lower subdomain's move**, before mod-ix: `"after": "Events"`, `"Install"` and `"Knowledge"` (`Driver.Tests/fixtures/structure.json:1122-1131`, `:1034-1039`, `:1211-1222`), as are *Service names Agents* and *Instance names Agents* (`:862-867`, `:1065-1070`). FOUNDATION1's own cuts land them before this design's first row. *Events names Agents* is also wider than O lists: `HomeWatch` names `AccountCooling`, `NoteCodes` names `AcpResumeRefused` and `CoolingEntry`, `SessionLog` names `AccountLine` (`:1122-1131`). §2.7 says what this design needs those cuts to leave, and adopts what lands | neither |
| Neither proposal: who cuts the *Agents names …* families | They are `"after": "Agents"` (`structure.json:1332-1425`): FOUNDATION1 cuts them between mod-ix and mod-xxi, which lands only on a census with none left (D212 §14.6). The ports this design adds are those cuts for the files the contract replaces (§2.7), so each such edge is cut once, by the row named there, and D212 takes a note saying so | — |
| The survey §1, §4 and §10 and O cite `Platform/DriverConfig.cs`, `Platform/DriverChoicesCommand.cs` and `Sessions/Domain/AccessRules.cs:117-127` | mod-iii moved them: the empty configuration's `"claude-code"` is `Settings/DriverConfig.cs:72`; the stand-ins told by name, `Platform/ActorVerbs.cs:75`; the *questions* pause's `claude-code`, `Settings/AccessKey.cs:74` | — |
| The survey §14 and both proposals' records tables: `<session>.harness.json` holds the binding | z-iii imported it into the desktop store's `bindings (session, adapter, conversation, kept)`, each file set aside once imported (`Participants/BindingStore.cs:91`, `:236-242`); its `adapter` holds the door's id | — |
| O §0 item 3: Claude Code's and Codex's wire extensions appear 63 times in 24 files outside `docs/` | Holds (`thread_active_writer`, `promptQueueing`, `_claude/rateLimit`, `_claude/model`, `subagent_spawned`) | O |
| The survey §1: a stand-in told by an empty binary in one place and by its names in another | Holds: `Agents/AgentsCommand.cs:653-658` lists a reserved id only where its toolchain names a binary; `Platform/ActorVerbs.cs:75` and `Brainstorms/Domain/BrainstormSynthesis.cs:12` name `stub` and `acp-stub` | — |

The rest holds as the survey and the proposals cite it.

### 0.3 The inputs, folded

| Input (the brief §3) | Folded as |
|---|---|
| FOUNDATION1, D212 | the ground: the map (§14.2 row 9), the participant's `Binding` the contract's identity at a start, the start plan's settled selection, the four admission layers, the launcher's containment, the event envelope and vocabulary, the desktop store; the *Agents names …* edges in the files the contract replaces cut by this design's rows (§2.7); its Part B rows that touch agents read the contract as §2.8 says |
| MODELROLE1, D205 | carried: each door's offers and carriers (§4.2); its judge and walk Roles', unchanged |
| AGENTERRORS1, D210 | absorbed: one reader, its tables split between provider and door (§4.7); its row 1c lands on the one failure channel (row l) |
| COAGENT1, D199 | re-cut: the sheet a read of the contract (row r is COAGENT1e), its §3.4 carried whole (§4.8) |
| COWORKVIEW1 | carried: a child's announcement and end as readings, its row a's `start` and `end` lines written by the sink (§3) |
| SWITCHCTX1's carry | carried: its table of identities is each provider's conversation home (§4.3); the carry stays Context's and Course's |
| CONNECTORS1 | its handing seam is this design's (§4.4); its choices and page its own |
| OPENROUTER1 | the first new provider, its own design on the contract (§12) |

## 1. The two proposals, question by question

The question numbers are the brief's §7.

| Q | X | O | Chosen |
|---|---|---|---|
| 1 **The contract** | `IAgentProvider` of facets and `IAgentDoor` of facets with `Prepare` and `Attach`; capabilities with a state, evidence and applicability; `IAgentRun` the live run | one registration per provider, a sealed record of declared parts and small ports, every part required, `Known<T>` with its source and `Absent` a statement; the sheet a read | **O's record of required parts and ports and its `Known<T>` (§2.3, §2.4); X's applicability on every known value and its rule that a negotiation describes one opening (§2.4); X's live run, `AgentRun`, each operation admitted by the door's abilities and refused closed before anything is written (§2.5); the sheet a read (both)** |
| 2 **Provider, agent and door** | an agent with doors; a provider may hold accounts and a toolchain with no door; `codex` an account id, never runnable | an agent with doors; no door, no provider; the holder becomes Codex's provider | **An agent with doors (both); no door, no provider (O; COAGENT1 §3.2's *Door* row); `codex` the provider's id, never a door's, an agent's id where a door is asked D23's error (both)** (§2.2) |
| 3 **The door** | ACP one shared transport with provider codecs; a provider may supply a native or future transport | two shared agent-neutral transports, native and protocol, each door handing its dialect | **O's two shared transports, outside the providers' folders; X's opening for a transport of another kind kept only by a design of its own** (§2.6) |
| 4 **Plugins** | a declaration-backed provider and door factory in Agents; Plugins supplies a validated declaration | a door joining its `accountOf` provider or a provider of its own; `DeclaredAcpAdapter` becomes Plugins' `IProviderSource` | **X's factory in Agents over an Agents-owned declaration with no field for what a manifest may never claim; O's list of what it may say, COAGENT1 §3.4 whole** (§4.8) |
| 5 **Accounts** | `HarnessToolchain` split into toolchain, account, access and door facets; a key an opaque credential reference | an `Accounts` part and a `Tables` part on the provider; the door keeps its turn report and its wire's codes | **Both, said once** (§4.1) |
| 6 **Models** | session offers apart from account defaults; asked, carried, acknowledged and reported apart | offers per door and version, negotiated or declared; the door's carrier; `ModelAsked` | **O's offers and carriers; X's two scopes and four values** (§4.2) |
| 7 **The session's life** | the provider's mechanisms, Course's decisions; a carry across doors or versions only by an explicit compatible recipe | the same; the conversation home is SWITCHCTX1's table | **Both; X's recipe rule** (§4.3) |
| 8 **The stream** | an ordered observation stream per run generation, with provenance, replay status and coverage, in Events | `WireReading` in Events beside `SessionEvent`, written to one sink, minting nothing | **O's closed readings and sink in Events; X's provenance, replay and coverage on each; X's *a stopped or failed turn is no answer*** (§3) |
| 9 **Connectors** | one `ConnectorPlan`: servers, their environment, identity, inclusions and exclusions, sources; the provider says how it carries it and whether it can exclude | `IServerHanding` over `ServerEntry` values, on the wire, by a file or not at all; the provider's own servers measured | **X's plan through O's seam, the entry Knowledge's** (§4.4) |
| 10 **Permissions, skills, hooks** | resolved access rendered by providers; Claude Code's rules its language; skills and hooks by explicit carriers | postures per door; `PostureAsked`; `IRuleLanguage`, `ISkillLayout`; the hook Claude Code's | **Both, said once** (§4.5) |
| 11 **Process and environment** | the provider contributes its executable, arguments and environment; other providers' credential and home variables stripped before the account's are bound | `ISpawn` returns a plan; Daoris's variables applied after it; no `DAORIS_` variable in a plan | **O's plan and order, the plan a Platform `ProcessRecipe` (X); X's strip a correction of its own, measured first** (§4.6, row s-vi) |
| 12 **Errors** | one reader over a neutral failure value; opaque identity plus phase; one typed outcome | one reader, the door's table before the provider's; one `Failed` reading on both transports | **Both, said once** (§4.7) |
| 13 **The map** | placements across the twenty-one subdomains; shared ACP under `Agents/Providers/Transports/` | the contract in Agents, readings in Events, the entry and the room's port in Knowledge, the program's spec in Install | **O's placement, the transports in a folder of their own; X's Platform `ProcessRecipe` and Install-owned release sources; the lower families FOUNDATION1's cuts** (§2.7) |
| 14 **Records** | an immutable legacy lookup; an unavailable descriptor for an id a build lacks; a later binding-details table | no record changes shape; a table of each field's kind of id; `Resolve(id, kind)` | **O's table and resolution; X's unavailable descriptor; no binding-details table until a reader needs one** (§5) |
| 15 **The page, service, tools** | the driver answers agents, doors, actions, offers and sign-in progress; service codecs per provider | the roster's answer as the sheet; named twins; the service's call keys a twin held by one fixture | **O's, with X's service files per provider** (§6) |
| 16 **The disagreements** | each a decision note on a branch of its own, apart from the move | each a dated note in the row that unifies it | **X's: a move changes nothing, and each correction is its own row with its old and new cases** (§7, rows s-i to s-vii) |
| 17 **The names** | `ProviderId`, `DoorId`, `AgentRun`, `NativeStdio`; *codec* | `Provider`, `AgentDoor`, `Transport`, *dialect*, `WireReading`, `Known<T>`, `StandIn` | **O's names; X's typed ids and `AgentRun`** (§8) |
| 18 **The order** | a contract and census; a compatibility runner; the stand-in; then by provider; corrections and records last | P1 to P16 by part; the stand-in second; the owner once third | **The first rows both share; X's split of the stand-in from its plumbing; O's cut by part thereafter; X's corrections apart** (§11) |
| 19 **The owner's** | concrete acts alone: a key and its spend, a sign-in, a widening, a deletion, a push | the OpenRouter measurement; OpenRouter as an agent of its own, asked only on a departure | **§13** |
| 20 **Proof** | per row; real model-free subprocesses; the installed proof the parent's | per row; the stand-in's pending table; the installed proof the parent's | **Both** (§9, §10, §11) |

### 1.1 Where they differ, and which way

| # | The difference | Chosen, and why | The case the other gets wrong |
|---|---|---|---|
| 1 | One interface of facets (X) or one registration of a record of parts (O) | **O's record**, every part required and a part stated `Absent` reading as today for an agent that says nothing (D53). X's facets are O's parts; what differs is whether a member can be left out. A record whose members are required makes a member the contract gains later break every provider's construction until each answers it, where today's twenty-one defaults (`Adapters.cs:1321-1513`) gave `DeclaredAcpAdapter` `Resumes` true by omission (survey §6) | A facet with a default: a provider added before the contract gains *its servers inherit its environment* reads whatever the default says, and a session's identity is trusted to reach a connector that Codex starts with its entry alone (CONNECTOR2; O-1) |
| 2 | A live run interface (X's `IAgentRun`) or none named (O) | **X's**, as `AgentRun`, each operation admitted by the door's abilities and refused closed before a write. Callers branch on three captures (`Loop/Driver.cs:3806-3825`) and a chat's three doors (`Conversations/Chat/ChatRunner.cs:561-570`) today; O's P11 needs a face to remove them and names none | Without it, row m moves start, resume and stop *through the door* with nothing typed to hold, and each executor keeps its test of which transport it holds |
| 3 | A known value's source and evidence (O) or also its applicability (X) | **O's `Known<T>` with X's applicability** (door, agent and adapter versions, posture, configuration digest) and X's rule that a negotiation describes the opening that answered it | O's alone: a reading negotiated on one adapter's version admits *now* on the next, which may reset its turn on a second prompt as Codex's adapter does (survey §7) |
| 4 | A provider with no door (X) or none (O) | **O's**: every agent has a door once the holder folds in, and COAGENT1 §3.2 already says *without a door it is no provider* | X's: the `codex` holder survives as a doorless provider, a fifth place an owner is resolved (survey §15 tangle 3) |
| 5 | The native transport shared (O) or a provider's own (X: *providers may also supply native or future transports*) | **O's**: one native transport, its process per run, capture and retry once, each door handing it a dialect; X's opening kept only for a transport of another kind, by a design of its own | X's: the stub's plain capture, the third stand-in's lines and the next command-line agent each copy the process per run and the retry before a conversation is named, three copies today (survey §5) |
| 6 | The shared transports inside the providers' boundary (X, `Agents/Providers/Transports/Acp/`) or apart (O, `Agents/Transports/`) | **O's**: the census holds the transports to the spec's frames and plain lines, as every file outside the providers, so `Acp.cs`'s inline reads of one agent (survey §15 tangle 1) cannot grow back | X's: a transport inside the boundary names `claudeCode.promptQueueing` again and the census passes it |
| 7 | The stand-in added with its plumbing (O's P2) or apart (X's AP-b, AP-c) | **X's split**: row b makes the registration list, the conformance suite and the rehearsals discover stand-ins; row c adds `alt-stub` by its folder and one line, so the brief §8's *the diff is its proof* holds literally | O's P2 edits `tools/family-rehearsal.mjs` and the shim in the row that adds the stand-in, so its diff cannot show the contract was enough |
| 8 | The third stand-in's program in `tools/stand-ins/` (O) or in its provider's folder (X) | **X's**: its program and fixtures in `Agents/Providers/AltStub/`, as the lineage hook's program sits in the driver's tree (`Sessions/hooks/lineage-hook.mjs`) | O's: every later stand-in a second folder outside its provider, and the census letting `tools/` name its id |
| 9 | Today's stand-ins keep Claude Code's and Codex's vocabulary by reference (O) or get their own (X) | **O's for `stub` and `acp-stub`**, which rehearse those agents' paths and are fed their extensions 63 times in 24 files (§0.2); **X's for the third**, whose vocabulary is no real agent's | X's applied to today's stubs: `LimitResumeTickTests`, `AcpSteerTests` and the family rehearsal's protocol phase fail on a change of vocabulary that is no regression |
| 10 | A move and a correction in one row (O: P4, P6, P7, P12 and P15 each carry a change) or apart (X) | **X's**: every move reproduces today, each disagreement its own row (s-i to s-vii) with its old and new cases and its dated note, so a regression on the install reverts the correction and keeps the move | O's P4: a candidate's floor read per provider rides the accounts' readers' move, and reverting the floor reverts the move |
| 11 | Rows by provider (X: Claude Code's accounts, Codex's, the rest) or by part (O) | **O's by part** after the first three: the copies the survey counts are shared code (*own, else owner* four times, the limit reader three times), so a row by part removes a whole census family and un-pends one case of the stand-in | X's AP-d1 moves Claude Code's account facts while the four copies that read them stay, to be moved again in AP-d2 |
| 12 | A plugin's door built in Agents from a declaration (X) or by Plugins' source (O) | **X's factory in Agents**: what a manifest may never claim is the contract's rule, enforced where the contract is; Plugins converts a manifest into an Agents-owned `DeclaredDoor`, which has no field for steering, resume, rules, words, lineage or confinement | O's: a source in Plugins returning `Provider` records can state steering *declared* |
| 13 | The machine's door one function over Build's order (O) or no machine door, each candidate's eligibility (X) | **X's shape with O's answer**: no machine door; `Reach.Drives(order, adopted)` asks each of Build's candidates through the start's judge, and the loop's answer, *a protocol choice rides it*, is their any | A single door read by the window: a native first choice reported as unable to drive where a protocol choice later in Build's order is what the loop starts |
| 14 | Session offers and account defaults one list (O) or two scopes (X) | **X's** (§0.2) | O's P12: `max` taken from Claude Code's pipe door, where a conversation may carry it |
| 15 | The token weights: unknown calibration reads unknown (X) or D179's interim stands (O) | **O's** (§0.2): the interim is decided and weighs no turn of unknown meaning; X's rule is the shape USAGECAP1b's table takes | X's now: Claude Code's turns lose their weighing until USAGECAP1b, and the projection's asks change |
| 16 | `WeekFixed` wired into the driver's scheduling (X, a D130 correction) or the provider's fact read by its one consumer (O) | **O's**: its one consumer is the modules' route (`Modules/Bridge/Routes/AccountsRoutes.cs:62`) and no driver reader disagrees, so no correction is filed until one needs it | X's: a correction no reader asked for, changing projections D130 decided |
| 17 | Other providers' credential and home variables stripped from a spawn's inherited environment (X) or not named (O) | **X's, as a correction of its own** (s-vi ⚖), after the variables each agent reads are measured, and only other providers' | Without it, a Codex spawn inherits the person's `ANTHROPIC_API_KEY` |
| 18 | A binding-details table now (X, optional) or none (O) | **O's**: no reader needs it (D173's first guard); COAGENT1e's negotiated readings, if kept, go in a new table of Agents', every column required (`compatible-builds.md`) | X's: provider and evidence snapshots per binding written by every start and read by nothing |
| 19 | *Dialect* (O) or *codec* (X) | **Dialect**: an agent's vocabulary on a shared wire, read and written; *codec* names a byte format | — |

### 1.2 What each got that the other missed

**Codex's.** The run as a typed face with closed refusals (§2.5). Applicability on a known value, and a negotiation that describes one
opening (§2.4). Session offers apart from account defaults, and the four model values (§4.2; §0.2). A carry across doors or versions only by a
compatible recipe (§4.3). A stopped or failed turn never an answer, and a child closed by its parent's end saying so (§3). The connector plan
with its exclusions (§4.4). Other providers' credentials stripped from a spawn (row s-vi). An unavailable descriptor for an id a build lacks
(§5). Corrections apart from moves (§7). OPENROUTER1's executor named, and its gateway no proof of another maker (§12).

**The Opus reader's.** The readings in Events, since *Events names Agents* already reads end and turn facts declared above it (O §0 item 2).
The two shared transports (§2.6). Required parts and `Absent` as a statement (§1.1 row 1). The stand-ins' vocabulary by reference, counted
(§0.2). D70's reason read in the planner, so *a native agent cannot drive an unadopted repository* is a choice, not a fact of the door (§7).
The token weights' decided interim and the pipe door's limit as D210's row 1c (§0.2). Each record field's kind of id (§5). `WeekFixed`'s one
consumer.

## 2. The contract

### 2.1 In one paragraph

A **provider is an agent** (Claude Code, Codex, dsh, a stand-in, a plugin's) **with one or more doors**, each door a way in, today an
adapter, keeping that adapter's id. It is **one registration** of a sealed record of **parts**: what is a fact about another program (its
binary, its accounts' variables, its postures in its own words, its limit and error sentences) is declared data, each fact in one place, on the
provider where it is the agent's and on the door where it is the door's, so no reader asks *own, else owner*; what differs in behaviour (reading
a wire, asking an account's usage, rendering rules, preparing a spawn) is a **small port** the provider implements; and every optional fact is a
**`Known<T>`** naming its source, its evidence and where it applies, or saying it is absent. **Daoris selects, the provider translates**: Roles'
start plan settles the role, choice, agent, model and account; the door prepares a process and its carriers; the launcher starts and contains
it; and the door's **`AgentRun`** opens or resumes, takes words, steers, stops and closes, each operation admitted by the door's abilities. Two
**transports** are shared and agent-neutral, the native one and the protocol one, and each door hands its transport its **dialect**, its agent's
vocabulary on that wire. Every provider tells what happened as **readings**, a closed vocabulary declared in Events and written to **one sink**
per run; the provider mints nothing. COAGENT1's sheet is **a read** of the contract. Every record keeps its strings, and one registry resolves
each by the kind of id its field holds.

### 2.2 Provider, agent and door

| Today | After |
|---|---|
| `claude-code` (the pipe door and its accounts' owner); `claude-code-acp` (`AccountOf: claude-code`) | provider `claude-code`, doors `claude-code` (native) and `claude-code-acp` (protocol) |
| `codex-acp` (`AccountOf: codex`) and the `codex` holder, a toolchain with no door (`Adapters.cs:2567-2571`, `Accounts/SignIns/CodexAccounts.cs:30-53`) | provider `codex`, door `codex-acp`; no holder |
| `dsh` | provider `dsh`, door `dsh` (protocol) |
| `stub`, `acp-stub` (`AccountOf: stub`) | provider `stub`, `StandIn`, doors `stub` (native) and `acp-stub` (protocol), declared to speak Claude Code's vocabulary and, on `acp-stub`, Codex's protocol dialect too, by reference, never by copy |
| a plugin's harness with `accountOf` | a door of that provider, `DeclaredBy` its plugin |
| a plugin's harness with none | a provider of its own, named by the harness, with that one door |
| — | provider `alt-stub`, `StandIn`, doors `alt-stub` (native) and `alt-acp-stub` (protocol), its vocabulary its own (row c) |

**Identity's rules.** A provider's id is unique among providers and a door's among doors; a door's id may equal its own provider's
(`claude-code`, `dsh`, `stub`), never another provider's; a provider has at least one door; a plugin declares no id the built set holds (D64's
rule 3, `AdapterSet.Reserved`); an unknown id is an error naming what exists (D23), and a provider's id where a door is asked is that same
error, never the provider's first door. **A door's id is opaque outside the providers** (X): code may carry it, store it, compare it for
identity and show it, and never infers behaviour from its spelling. The ids are typed, `ProviderId` and `DoorId` (X), so one passed where the
other is wanted does not compile.

### 2.3 The parts

`Provider` and `AgentDoor` are sealed records in `Agents/Contract/`. Every part is required: a provider states each, `Absent` being a
statement, and a part stated absent reads as today's behaviour for an agent that says nothing (D53). Which row moves each of today's fields
is §11's.

**The provider:**

| Part | Holds | From today | Stated absent, reads as |
|---|---|---|---|
| `Id`, `Identity` | the agent's id; product, maker, mark; `StandIn` (runs no model); `DeclaredBy` (set by the source, never by a manifest); the maker's commit attribution | `Owner(name)`; `Product`, `Maker`, `Mark` (`Harnesses.cs:199-243`); `AdapterSet.DeclaredBy`'s type test; the trailer's switch on the maker (`Adapters.cs:383-431`) | product unknown, the page's own mark; a maker not declared no family of any other (COAGENT1 §3.2) |
| `Doors` | its `AgentDoor`s | the adapters of one owner | refused at registration |
| `Program` | its own binary as Install's `ToolSpec`: binary, version question, install, update, release source, pinned environment, presence probe | `Binary`, `VersionArguments`, `Install`, `UpdateArguments`, `Package`, `Channel`, `PinnedEnvironment`, `ProbeByPresence` | Daoris manages nothing of it |
| `Accounts` | the kind (a sign-in home, an API key, a provider's key with its variables); home and key variables; `RunsInOwnHome`; the sign-in, its device code and ports; the login question; the trust file; the settings file; `WeekFixed`; the reading floor | the toolchain's account fields; the floors (`HarnessRoster.cs:2107-2134`) | its own home: no walk, no cool-off, a limit read as a failure (COAGENT1 §3.2) |
| `Tables` | limit, window, sign-in and refused words; error words (kinds, statuses, transient sentences), each on a recorded sentence | `Limits`, `Windows`, `SignIn`, `Refused`; `ClaudeErrors` (`AgentErrors.cs:277-316`) | fatal, no cool-off, no window (D210) |
| `Usage` | `IUsageQuestion`: an account's windows asked between turns | `UsageKind {Codex, Claude}` (`Accounts/Usage/CodexUsage.cs:21-39`) | nothing asked |
| `Models` | `ISettingsFile`: an account's default model and effort, its keys, the efforts a default may hold | `Accounts/AgentSettings.cs` | no account default offered |
| `Children` | `IChildVocabulary`: how its lineage names a child (its hook's lines, its calls' `_meta` keys, its stream's signals), and whether it says a child's end | the fold's branches (`Sessions/Domain/ChildBindings.cs:40-60`, `:128-266`, `:293-323`) | children unknown, a call bound to nobody |
| `Rules`, `Skills` | `IRuleLanguage`: Daoris's rules in its grammar, its hard deny and hooks; `ISkillLayout` | `Agents/Permissions/Permissions.cs`; `SpawnSkills.cs`; `DshProfile.cs:48-56` | takes none, the copy the floor, said; handed none |
| `Conversations` | `IConversationHome`: what a conversation keeps under an account's home, by version | `Platform/ToolHomeLeftovers.cs:54-99`; SWITCHCTX1 §1.5 | no carry: a move hands the brief (D189) |
| `Reach` | where it may work: everywhere, or only the repositories opened to it | — (OPENROUTER1's) | everywhere, as every agent today |

**The door:**

| Part | Holds | From today | Stated absent, reads as |
|---|---|---|---|
| `Id`, `Transport` | the door's id; `Native` or `Protocol` | `ISessionAdapter.Name`; `Wire` | required |
| `Program` | its own `ToolSpec` where it is a package apart (ACP2) | the protocol doors' `Binary` and `Package` | the provider's |
| `Postures` | ordinary, asking and full, each a mode in its own words or a variable and its value | `AcpPosture`, `FullPosture`, `AskingPosture` (`Adapters.cs:1348-1376`); `DSH_PERMISSION_MODE` | ordinary the agent's default; asking or full refused naming the agent (D183, D184) |
| `Spawn` | `ISpawn`: a start's, a chat's and a native resume's `ProcessRecipe`, and the variables its own process takes | `Prepare`, `PrepareChat`, `PrepareResume`, `ToolsUpFront`, `HandServers`, `HandSettings`, `HandPlugins` | required |
| `Dialect` | `INativeDialect` or `IProtocolDialect` (§2.6) | `StructuredOutput()`, `FrameMessage`, `FrameInterrupt`; the reads inline in `AcpSession` | native: plain lines and an end, nothing more; protocol: the spec alone |
| `Abilities` | a `Known<bool>` each: interactive; structured; words at the next step; steering; a stop that keeps the conversation; one task stopped; resumes; a resume that fails closed; requests held for the person; takes rules (by flag or `_meta`); hands servers (on the wire, by a file, not at all); its servers inherit its environment; leaves its own servers out | `Interactive`, `Resumes`, `TakesSettings`; the `Wire` tests in the callers (survey §6); `TurnDoor(Steers, KeepingStop)` | COAGENT1 §3.2's fallback, row by row |
| `Tables` | its wire's codes (`AuthRequiredCode`), its own error words, its turn report's meaning | `ClaudeAcpErrors` (`AgentErrors.cs:325-344`); `TurnReport` | nothing read; the meaning `unknown` (D179 point 8) |

### 2.4 What is known, and where it applies

`Known<T>` is `{ Value, By: Declared | Negotiated | Measured, Evidence, Applies }` or `Absent`. `Applies` names the door, the agent's and
the adapter's versions, the posture and the digest of the configuration Daoris handed (X; COAGENT1 §3.3's *effective configuration*). **A
negotiated value describes the opening that answered it** (X): a new run negotiates again, and a kept reading (per door and version, where
COAGENT1e keeps it) shows on the sheet as *negotiated at* that version and admits nothing by itself. **A measurement admits only its effective
configuration**: another version, posture or configuration reads *declared* until measured again (COAGENT1 §3.1). Declared, negotiated and
measured are separate evidence, never a score in which a handshake stands in for a measurement (X). The name is `Known`, not *capability*,
since Roles' `Capabilities` is the work's vocabulary (D212 §2.7; O §1.3).

COAGENT1e's sheet, `AgentCapabilities`, is `Sheet.Of(provider, door, negotiated, measurements)`, a pure read in `Agents/Domain/`; the
measurements table (COAGENT1 §3.5) stays its own record.

### 2.5 The run, and a start's stages

A start passes six stages, each its owner's (X):

1. **Roles** settles the selection, `StartPlan.Resolve` (D212 §2.4): role, choice, agent, door, asked model and effort, account, reservation.
2. **The door** prepares: its `ISpawn` answers a `ProcessRecipe` (Platform's value: the argument vector, the door's own variables, the working
   folder, the input) and its carriers for the model, the posture, the servers and the rules.
3. **Platform** applies Daoris's own variables after the recipe (§4.6) and starts and contains the process (`AgentStarts.StartAsync`, D201).
4. **The door's transport** opens or resumes; the dialect reports the negotiated abilities and the offers as readings.
5. **Roles** takes a refused choice through its own re-ask (D205); the provider never walks.
6. **Course** hands words through its dispatcher (`session-words.md`), the run attempting each before the wire and acknowledging it once
   written.

`AgentRun`, in `Agents/Contract/`, is what an executor holds: `Open` or `Resume`, `ApplyChoice(ModelAsked)`, `Send(DoorInput)`, `Steer`,
`StopTurn`, `StopTask`, `Close`, and its readings to the run's sink. **Each operation is admitted by the door's abilities**: one the door does
not have is answered `unsupported`, naming the ability, before anything is written, never attempted and failed on the wire (X). `DoorInput`
carries a word's text, files, id and key and the attempt port Course implements (`Attempting`, `Went`, today `SaidDoor`'s), so no door names
Course's types. The provider never walks accounts, chooses another model, starts another participant or decides whether a line goes on: those
are Roles' and Course's (both).

### 2.6 The transports and the dialects

- **The protocol transport** is `AcpSession` with every read of one agent taken out: the spec's `initialize`, `session/new`, `session/resume`
  or `session/load` by the agent's own capabilities (`Agents/Acp/Acp.cs:3457-3479`), `session/prompt`, the spec's `session/update` kinds,
  `session/request_permission`, `session/set_mode`, `session/set_config_option` found by category (`:667-672`, `:728-761`), `session/cancel`,
  and the transient retry's policy (D210). What it does not know it hands its door's `IProtocolDialect`: the `_meta` it sends at `initialize`
  and at `session/new` (`jetbrains.air`, `:1789`; Claude Code's settings and plugins), whether the agent queues words and steers
  (`:1704-1734`), each update outside the spec (subagent frames, async tasks, a woken turn), a turn's `_meta` (its model, its usage per model,
  its windows), a refused resume's reason (`thread_active_writer`, `:3387-3394`) and an error's shape (`:175-239`). Each answer is a reading or
  a negotiated ability.
- **The native transport** is the process per run with its target on its command line, read line by line: the plain capture
  (`Agents/Streams/HarnessCapture.cs:46-78`), the structured capture through the door's `INativeDialect` (today's `IStreamMapper`, with
  `FrameMessage` and `FrameInterrupt`), and a resume as a new process on the conversation the dialect named, tried once more where the run exits
  before naming it, written once (three copies today, survey §5). It is no one agent's.
- **Both** start through the one shell (`Spawning.Shell`, `Adapters.cs:1606-1653`) and the launcher (D201), unchanged. **Neither names an
  agent**: the census holds the transports to the spec's frames and plain lines (§9). A transport of another kind, an agent's HTTP interface
  say, is a provider's only through a design of its own, never a branch in these two (X's opening, kept narrow).

### 2.7 Where each type sits, and the census edges it cuts

Bottom to top in D212 §14.2's order; each type names only its own subdomain's types and lower ones.

| Type | Subdomain | The census family it meets |
|---|---|---|
| `ProcessRecipe` (X) | Platform (1) | — |
| `ToolSpec`; an Install-owned `IReleaseSource` (a maker's signed channel, an npm package) | Install (5) | *Install names Agents* (`ToolInstall.cs` naming `HarnessSettings`, `structure.json:1034-1039`): **FOUNDATION1's cut, after mod-v**; this design asks that it leave Install handed specs, never a provider, and adopts the shape that lands |
| `WireReading` and its kinds (§3), beside `SessionEvent`; the end, refusal, transient and turn facts it stands on | Events (7) | *Events names Agents* (`structure.json:1122-1131`): **FOUNDATION1's cut, after mod-vii**, which moves `HarnessEnding`'s facts, `ModelRefusal`, `TransientEpisode` and `TurnModels` down (X §1's *Events → Agents*) |
| `ServerEntry` (today's `AcpMcpServer`, `Acp.cs:36-57`); `IAgentFacts`, the room's port for agents and their sign-in states | Knowledge (8) | *Knowledge names Agents* (`structure.json:1211-1222`): **FOUNDATION1's cut, after mod-viii**, which this design asks to move the entry down and declare the port |
| `Provider`, `AgentDoor`, `ProviderId`, `DoorId`, `Known<T>`, the parts and ports, `AgentRun`, `DoorInput`, `ModelAsked`, `PostureAsked`, `SpawnRequest`, `ConnectorPlan`, `DeclaredDoor`, `IReadingSink` | Agents (9), `Agents/Contract/` | — |
| `ProviderRegistry`, `Providers.Built()` (the one registration list), `RecordIds` | Agents, `Agents/Registry/` | — |
| `Sheet.Of`, `Reach`, the abilities' and identity's judges | Agents, `Agents/Domain/`, pure (D212 §14.3) | — |
| the two transports | Agents, `Agents/Transports/Native/`, `Agents/Transports/Protocol/` | — |
| each provider | Agents, `Agents/Providers/<Id>/` (`ClaudeCode`, `Codex`, `Dsh`, `Stub`, `AltStub`) | — |

The *Agents names …* edges in the files the contract replaces are cut by this design's rows, each named in the census with its row; the rest
of each family stays FOUNDATION1's cut:

| Family (`structure.json:1332-1425`) | Cut by this design | FOUNDATION1's |
|---|---|---|
| *Agents names Context* | `Adapters.cs` (row h: the composed instruction handed in); `Acp.cs`, `AcpStreams.cs`, `ClaudeStreams.cs`, `HarnessCapture.cs`, `StructuredOutput.cs` (row l: the sink) | `AccountRotation.cs`, `UsageTurns.cs`, `BatchRelay.cs`, `ConsoleRelay.cs`, `TransientRetries.cs` |
| *Agents names Course* | `Acp.cs`, `TransientRetry.cs`, `HarnessCapture.cs` (row m: `DoorInput`); `Adapters.cs`'s `HandOverNote` (row h) | `TransientRetries.cs`, `SessionProcesses.cs` |
| *Agents names Help* | `Acp.cs`'s `HelpModes` (row o: `PostureAsked`) | — |
| *Agents names Participants* | `Adapters.cs`'s `DrivenAccess` and `Permissions.cs`'s lineage hook (row o); `HarnessCapture.cs`'s `ChildLineage` (row l) | `HarnessEnding.cs`, `SessionProcesses.cs` |
| *Agents names Plugins* | all four files (row q) | — |
| *Agents names Review* | `Acp.cs`, `AcpBackground.cs` (row l: a task's readings); `Adapters.cs`'s `ReviewDesk` (row h) | — |
| *Agents names Roles* | `Acp.cs`, `Adapters.cs` (row n: `ModelAsked`); `HarnessCapture.cs`'s `SupervisorHanded` (row l) | `AccountRotation.cs` |
| *Agents names Work orders* | `Adapters.cs` (row h) | `HarnessRoster.cs`, `TransientRetries.cs` |
| *Agents names Workplace* | `Adapters.cs` (row h); `Permissions.cs`'s tree guard (row o) | `ResumeAccount.cs` |

*Service names Agents* (`AccountLine`) and *Instance names Agents* (`VerbLeases` naming `AgentsCommand`) are FOUNDATION1's, as is the account
rule's vocabulary moved down (`structure.json:1426-1433`). D212 takes a note in row a that the edges above are cut here, once.

### 2.8 Beside FOUNDATION1's Part B rows that touch agents

| D212's row | What it reads of the contract |
|---|---|
| **a-ii**, the account pick's provenance on the binding | the binding's `adapter` (a door's id) as it stands; the pick is among the accounts of `ProviderOf(door)` (row d); no field added |
| **a-iii**, the briefed move's walk, one selection, a closed quest's session | the walk over the provider's accounts, the owner read once (row d) |
| **j**, one start plan and one walk | stage 1 of §2.5: the plan reads `ProviderRegistry.Offers(door, version)` and each door's carriers as values, names no provider, and its one re-ask is stage 5; whichever of j and this design's row n lands second moves `ActorAsked` to `ModelAsked` under the other |
| **w-s**, the roles' staffing in `role_staffing` | the staffing's door ids (`adapter`, `intakeAdapter`, `helperAdapter`, `actors[].agent`) kept as they are, resolved through the registry; nothing changes in w-s |
| **w**, the catalogue of working roles | `RoleCatalog.Judge` asks the registry that each door a role names exists (D23), refusing an unknown one with its closed code |
| **x**, starts take their role | nothing of the provider: the role and its revision on the binding beside its door's id |

## 3. The readings: the one normalized stream

### 3.1 The vocabulary and its sink

A provider says what happened as **readings**, `WireReading`, a closed set of records declared in Events beside `SessionEvent`, which it
extends rather than replaces: `Said` carries a `SessionEvent` of today's kinds (`Sessions/Domain/SessionEvents.cs:13-55`). Every reading carries
(X) its run, the run's generation and its sequence; the correlation ids it knows (conversation, turn, call, child, parent); its provenance,
`reported` by the agent, `observed` by the transport, `computed` or `inferred`; `replayed` where a resume's history replays it, so a replay is
never new activity (the protocol resume's replay, kept out today, `Acp.cs:826-827`); and its unknowns, stated. The transport reads its wire,
hands its door's dialect what it does not know, and writes each reading to the run's **`IReadingSink`**, which the executor composes (the look's
for a driven run, the chat runner's for a conversation): Context keeps the event file and the consoles, Participants the lineage, Agents the
binding's conversation (the store's `bindings`), the account's readings and the usage history, and the hub of the process that runs the work
tells FOUNDATION1's machine events from what was kept (D212 §4.1). **The provider mints nothing.** Nothing a provider says leaves the machine
but as an event's ids and codes: its raw frames, words, paths, account names, model strings and error sentences stay local (D212 §4.4). **What
a provider does not say is never emitted**, and every reader keeps it unknown (D173's guard). Until Part B's event rows (FOUNDATION1's rows k,
l and m) and the hubs land, the sink writes today's stores and calls today's channels, so the readings change no event a reader sees.

### 3.2 Mapped onto FOUNDATION1's codes and today's kinds

| Reading | Its facts | Read today at | Kept in | Minted as (D212 §4.2) | Unknown where |
|---|---|---|---|---|---|
| `Said` | a `SessionEvent`: user, message, thought, tool, plan, usage, raw, receipt | `Acp.cs:3501-3642`; `StructuredOutput.cs:80-177`, `:447-538` | the event file (Context) | none: the event file is a store (D212 §4.3) | — |
| `Negotiated` | the abilities and offers an open answered | `Acp.cs:74-135`, `:1639-1680` | the offers (`agent_offers`); the sheet's negotiated reading | none | a native door: none negotiated |
| `TurnEnded` | the stop reason in the protocol's words, the tokens, the split per model, the context and its provenance | `Acp.cs:2183-2197`, `:2138-2160`, `:3048-3053`; `StructuredOutput.cs:309-370`, `:426-445` | the event file's `turn`; the usage history | `turn.answered` only where the turn ended as an answer, by the answer judge of the process that runs it; a stopped, refused or failed turn is no answer (X; `SessionEvents.cs:33`) | a count the wire did not say: null, never zero (TOOL3) |
| `ConversationNamed` | the agent's conversation id | `Acp.cs:1666-1667`; `StructuredOutput.cs:130-134`; `HarnessCapture.cs:124-127` | the store's `bindings` | none: `part.opened` is the record's opening, once, never the handshake (X) | a wire that names none |
| `ModelReported` | the model a turn ran | `Acp.cs:3042-3046`; `StructuredOutput.cs:118-127` | D205 point 10's actor record; the turn's models | none | Codex, dsh, the stubs: unknown, never the model asked |
| `ModelRefused` | the model asked | the protocol open (`Acp.cs:165-173`, `:680-722`); `StructuredOutput.cs:219-232` | the start plan's re-ask | `part.ended`, class `refused-choice`, by the record's trigger | — |
| `WindowsSaid` | an account's windows, read by the provider's window words | `Acp.cs:3030`; `StreamMapped.Limits` | the windows (Agents) | none, and no limit by itself (X): the account's judgement may later tell `account.waits` | an agent that words none |
| `Failed` | a `DoorFailure`: where it was met, its code, kind word, status, sentence | the protocol's `turnFailed`; `HarnessEnding.Failure` (survey §12) | read by `AgentErrors.Read` into cooling, sign-out, the transient episode or the conclusion | a limit's: `part.limited` by the record's trigger and `account.waits` by the hub; the end's class `limited`, `access`, `transient-bound` or `failed` | — |
| `TransientTry` | a try's number; its wait; exhausted | `Acp.cs:1106-1351` | the event file's `transient` (D210 §5.1) | none; exhaustion may end the part `transient-bound` | — |
| `ChildSeen` | a child's native id, parent, type, signal | `AcpStreams.cs:77-103`; `ClaudeStreams.cs:95-167`; `ChildBindings.cs:293-323` | the lineage's `stream` line (Participants); the child's console | `child.seen`, by the hub, once Participants has placed the child | its parent and depth where no harness said |
| `ChildSaid` | a child's id and a `SessionEvent` | `AcpStreams.cs:152-204`; `ClaudeStreams.cs:73-81` | the child's console, never the record | none | — |
| `ChildEnded` | a child's id; finished or killed; the provenance of its end | `AcpStreams.cs:309-323`; `ClaudeStreams.cs:262-275` | the lineage's line for that child | `child.ended`; a child closed because its parent ended is `inferred`, its outcome unknown, never finished (X) | Codex says none: the fold ends the child with its part (`ChildBindings.cs:52-56`) |
| `TaskStarted`, `TaskEnded` | a background task's id, kind, output | `AcpBackground.cs:42-79`; `ClaudeStreams.cs:27`, `:46-51` | the running and long calls (`Sessions/Heavy/LongCalls.cs:14-21`); the console | none | on the native transport the turn ends its work (`Observation.cs:148-157`) |
| `Woken` | a turn the harness woke | `AcpWake.cs:60-102`, `:216-230` | the turn's queue | none | — |
| `RequestHeld` | a permission request held for the person | `Acp.cs:2757-2910` | `HeldPermissions` (Events) | `call.held` | — |
| `CallRefused` | a call the agent or the person refused | `Acp.cs:2694-2755`; `StructuredOutput.cs:372-408` | the event file, the tool marked refused | none | — |
| `Outside` | a line the harness wrote outside its protocol | `StreamMapped.Outside`; stderr | the console; read for access alone (`AgentErrors.cs:144-152`) | none | — |
| `Unread` | a frame no reader knew, bounded | — | the raw stream, local | none: no guessed meaning (X) | — |
| `Ended` | the exit; the run's own end of its stream; background cut; what remains unresolved (X's coverage) | `HarnessEnding`; `AcpEnded.cs:11-15`; `AcpEnding.cs:41-57` | the conclusion (Loop, Course), then the record | `part.ended` with its judged class, by the record's trigger | — |

`part.quiet` is the watch's, never a provider's. The four parsings of Claude Code's `stream-json` (the mapper, its streams, the lineage's,
`Observation.cs:564-676`'s `HarnessWords`; survey §15 tangle 2) become one, Claude Code's native dialect, the other three reading readings. A
child's card kind, `think` on one door and `other` on the other (`AcpStreams.cs:36-37`; `StructuredOutput.cs:491-500`), is the reading's one
kind.

### 3.3 Where each agent's wire is read

| Folder | Takes | Reads |
|---|---|---|
| `Agents/Providers/ClaudeCode/` | `ClaudeStreamJson` and `ClaudeStreams` (its native dialect); a protocol dialect from `Acp.cs`'s `_claude/` and `claudeCode` reads, `AcpWake.cs`, `AcpBackground.cs` and the subagent frames of `AcpStreams.cs`; `ClaudeUsage`; its window, limit, sign-in and error words; `ClaudeTrust`; the rules' rendering, the plugins' layout and the settings' schema; the conversation's layout; the lineage hook; the release channel as an `IReleaseSource` | `stream-json`; the adapter's `_meta`; the hook's input; its home's files |
| `Agents/Providers/Codex/` | the holder's toolchain as its program and accounts; `CodexUsage`; its limit words (`AccountLimits.cs:330-361`); `SignInPorts`; a protocol dialect reading `thread_active_writer` | the app server's answers; the adapter's errors |
| `Agents/Providers/Dsh/` | `DshProfile` (its telemetry patch and skills); its posture variable | its profile file |
| `Agents/Providers/Stub/` | `stub`, `acp-stub`; their vocabulary by reference to Claude Code's tables and dialects and Codex's protocol dialect | as the rehearsal plays them |
| `Agents/Providers/AltStub/` | its own words, lines and `_meta`; its program and fixtures | its own program's |

The shared transports read the spec's frames and plain lines alone. The service's reading of a call's `_meta` (`Core/Sessions/CallLineage.cs:26-80`,
`Mcp/CallLineageFilter.cs:14-22`) is the one wire read outside the driver's providers: it moves into one file per provider under
`Core/Sessions/Providers/` (X), each a twin of that provider's `IChildVocabulary` call keys, held by one fixture both suites read (`twins.md`).

## 4. What each part answers

### 4.1 Accounts (Q5)

The provider owns its accounts' kind, home and key variables, `RunsInOwnHome`, its sign-in and login question, its trust file, its usage
question, its windows, its floor, `WeekFixed` and its words; the door keeps its turn report's meaning and its wire's codes. **A door onto
another agent's accounts reads that agent's** accounts, sign-in, usage question, windows, limit words, weekly reset and floor, and **never
inherits** its `AuthRequiredCode`, its turn report or its program (both; D210's *own, then owner*, `AgentErrors.cs:83-91`, made the types'). An
account that is a key is `Accounts.Kind = Key` with its own variables, kept where D172 point 3 keeps keys (`profiles/`), an opaque reference
until the spawn (X), handed only to its own provider's spawn by its door's `ISpawn`. The owner is resolved once, `ProviderRegistry.ProviderOf`,
where four copies resolve it with four rules today (survey §15 tangle 3); the holder folds into Codex's provider, and the facts one door declares
for its agent move to the agent (`codex-acp`'s Codex limits and usage, `Adapters.cs:2104-2125`; `claude-code-acp`'s repeated identity and trust
file, `:1919-1945`). One account on two doors is one account (`Accounts/Usage.cs:69-76` groups by door today, a page twin row s-vii reads).

### 4.2 Models (Q6)

A door's offers come from its source: negotiated at the protocol open from `configOptions` and kept per door and version (today
`data/agent-offers.json`, Part B's `agent_offers`), or declared by the provider for a native door (Claude Code's aliases and effort words). **Two
scopes** (X): the offers a session may carry, and the values an account's default may hold, which differ (`max` a session's alone, §0.2). **Four
values** (X): the model and effort **asked** (D205 point 10), **carried** (the flags or the option set), **acknowledged** (the door's answer to
its carrier: `set_config_option`'s returned list, `ActorRun.cs:82-97`) and **reported** (the turn's own word), each kept apart, so a requested
model is never read as the one that ran. A choice is carried by the door: the protocol transport's `set_config_option` by category, or the native
spawn's flags. A refusal is read before any prompt at the protocol open, or by the native dialect's `ModelRefused` after a silent turn
(`StructuredOutput.cs:219-232`). D205's judge and the start plan read `ProviderRegistry.Offers(door, version)` and the carrier as values and name
no provider; what a spawn asked (`ActorRun.cs:29-50`, today read from Claude Code's flags and variables for every agent) is the door's `Spawn`
answering for itself. A model is never carried by writing the account's settings or environment (D205 point 3).

### 4.3 The session's life (Q7)

**The provider's**: how to start (its spawn and the open), resume its own conversation (the negotiated method on the wire, or its spawn's resume
recipe), take words (at the next step only where negotiated, *now* only where steering is measured), stop keeping the conversation, close, and
where it keeps a conversation (`IConversationHome`, SWITCHCTX1's table). **Course's**: whether and when to go on, resume, move, hand over or
carry, with which words (D212 §3), on the account `LineAccount` decides. A carry across doors or versions requires a recipe the provider
declares compatible for that pair; sharing a provider or an account pool is not enough (X), so a native conversation of Claude Code's is never
handed to its protocol door by a shared pool. The protocol resume written twice (`Loop/Driver.cs:4397-4441`, `ChatRunner.cs:3045-3173`), the
native retry three times and the hand-over's accounts three times (survey §5) become one each, behind `AgentRun` and the registry. A provider
that would decide at its limit to resume on another account would be a second account rule beside `LineAccount`, LIMITSWITCH1's class (D212):
none does.

### 4.4 Connectors (Q9)

One seam, `IServerHanding`, given one **`ConnectorPlan`** (X): the servers a start chose (Daoris's connector, the plugins', the browser's) as
`ServerEntry` values in Knowledge's type, each with its own environment; the session's identity; the inclusions and exclusions the start
requires; and the sources permitted. The door answers by its ability: on the wire (`session/new`'s `mcpServers`, `Acp.cs:1736-1763`), by a file
in its provider's own layout (Claude Code's `--mcp-config`, `SpawnServers.cs:5-53`, written today for every pipe agent), or not at all; and
whether it can leave its agent's own servers out (Claude Code's native door can, by `--strict-mcp-config`, `HolderTerminal.cs:44`; the rest
unmeasured, absent reading *cannot be left out, said*). A start that depends on an exclusion its door cannot honour is refused (X); no start does
today. **The session's identity rides in every entry Daoris writes**, as the home already does (`Service/KnowledgeConnector.cs:80-106`), never
trusted to an inherited environment, which Codex does not pass (CONNECTOR2). CONNECTORS1's choices act on the plan, nowhere else. The five
callers' tests of the door before the helper (`Loop/DriverHelpers.cs:993-1000` and four more, survey §9) and the helper's own
(`SpawnServers.cs:55-68`) all ask the door's carrier, and go. *A native agent cannot drive an unadopted repository* stands, once (§7 row 2).

### 4.5 Permissions, skills and hooks (Q10)

Postures per door, in the agent's own words (§2.3), a full or asking start with none mapped refused naming the agent (D183, D184). The marks packed
into a posture's string, `!` required and `?` the person answers (`Acp.cs:326-336`, `:2994-3009`), become `PostureAsked`, a typed value Help
and Participants build. Claude Code's `IRuleLanguage` renders the product's rules into its settings file with its hooks (the tree guard's
among them) and the hard deny, carried by `--settings` on its native door and by `_meta.claudeCode.options.settings` on its protocol door; the
rules themselves stay Claude Code's grammar, the product's only rule language (D72; survey §15 tangle 2), named in the census as the product's,
since a rule model of no agent's would change what the person types and is a design of its own (§13). Skills by `ISkillLayout` (Claude Code's
plugins by `--plugin-dir` or the `_meta`; dsh's `.claude/skills`). Where a provider takes no rules, the sheet says so and the page and Ask
Daoris's room say the person's denies do not reach it (COAGENT1 §3.2); a copied settings file is never presented as enforcement on an agent that
does not read it (X). The lineage hook stays Claude Code's asset, handed in its settings; Codex's calls carry their thread and need none; no
provider is made to take a hook (both).

### 4.6 Process and environment (Q11)

A door's `ISpawn` returns a `ProcessRecipe`: its argument vector and the variables its process takes (its home and key variables, its pinned
environment, its own seams such as `CLAUDE_CODE_EXECUTABLE`, its posture variable, its tools-up-front switch). **Daoris's own variables**
(`Spawning.Roles`, `Adapters.cs:1659-1665`; the target; the repository and the service; the tools' path; the git wiring) come in the
`SpawnRequest` from the executor and are **applied after the recipe** by Platform, so no provider sets or keeps one; a recipe naming any of them,
or any `DAORIS_` variable, fails the conformance suite (the FIX-LOG's ENVLEAK1 entry made the role variables Daoris's). **The instruction leaves the adapter**: Context composes it (today
`TargetPrompt.Compose` inside `Spawning.InRoot`, `Adapters.cs:1540-1571`), the request carries it as a string, and the door carries it its own
way (`DAORIS_TARGET` for every door as today, `-p` too on Claude Code's native door; the first prompt on the protocol transport). Containment
stays the launcher's (D201). Two corrections stay apart from the move (§7): the Claude Code seam on every protocol door's spawn (s-v), and
other providers' credential and home variables stripped from the inherited environment (s-vi ⚖).

### 4.7 Errors (Q12)

The provider's `Tables` (kinds, statuses, transient sentences; limit, sign-in and refused words) and the door's (its wire's codes), read by the
one `AgentErrors.Read` in D210's order, the door's before the provider's, never inherited the other way. A door decodes its failure into a
neutral `DoorFailure` (X) with its phase (`open`, `prompt`, `result`, `harness`) and the transport's spelling; `DoorFailure.Door` keeps the
spellings records hold (`protocol`, `pipe`) through one table (§5), and no reader branches on it. Codex's provider keeps no table, its transient
errors fatal as today, until AGENTERRORS1's row adds one on a recorded sentence (`codexErrorInfo`'s reader that row's). **The two channels
become one**: every failure is a `Failed` reading on both transports, read by the conclusion's limit and sign-out readers; that the native
transport's limit then cools its account is D210's row 1c, switched on by that row on row l's channel, never by a move (§0.2).

### 4.8 Plugins (Q4)

A manifest is converted by Plugins into an Agents-owned **`DeclaredDoor`** (X), which Agents' factory makes into a door joining the provider
its `accountOf` names, or a provider of its own, riding the protocol transport with the spec's dialect alone. The declaration has fields for its
identity, program, home variable and own home, and postures in its own words (`posture`, and from COAGENT1e `fullPosture` and
`askingPosture`, each in a reviewed row of COAGENT1e's own); **it has no field** for rules, skills, limit, window, sign-in, refused or error
words, a usage question, children or lineage, confinement, steering, queueing or resume, a dialect, a conversation home, `StandIn` or
`DeclaredBy` (COAGENT1 §3.4; both). Joining a provider gives it that provider's tables, never another door's (`AuthRequiredCode`). Plugins
implements the source of declarations, Loop composes it, and `AdapterSet.WithPlugins` (`Adapters.cs:2521-2537`) leaves Agents. Moving the
factory grants nothing (X).

## 5. Records and older builds (Q14)

No record changes shape (the brief §10 point 4: a field changes only after every reader reads it through the contract, and none needs to).
Row a writes the table below into `Agents/Registry/RecordIds.cs`, each field with the kind of id it holds, so `ProviderRegistry.Resolve(id,
kind)` resolves it by its kind and no reader guesses (O); `codex` resolves as a provider, never as a door (X). **An id a build lacks** reads as
an unavailable door or provider keeping its id, its maker never guessed, and a start or continuation on it refuses naming what exists (D23; X's
unavailable descriptor), as a removed plugin's door does today; an account folder naming an agent a build lacks is passed over (POOLSTRAY1).

| Record (survey §14) | Field | Holds | After | An older build |
|---|---|---|---|---|
| The session record (service) | `Adapter`, `HarnessVersion`; `Profile` | a door's id and its program's version; the account | unchanged; the agent derived through the registry; `Adapter` and `HarnessVersion` still travel, `Profile` still does not (`Core/Remote/SessionSync.cs:59`) | unchanged |
| The actor asked and ran (D205 point 10) | `agent` | a door's id | unchanged | unchanged |
| The store's `bindings` (z-iii) | `adapter`, `conversation` | the door that named the conversation | unchanged | unchanged; z-iii's own downgrade rules |
| Ask Daoris's `<id>.wired.json` | `harness`; `door` | a door's id; `pipe` or `acp` | unchanged, the spelling written from the transport's spelling table | unchanged |
| Note codes | `resumed.native`; `gone`, `exited`, `elsewhere` | a resume's reasons | unchanged: `gone` from the spec's refusal, `exited` from the native transport, `elsewhere` from Codex's dialect | unchanged |
| `SessionEnded`, the usage history | `Adapter`; `TurnBinding(Agent, Account, Door, Meaning)` | a door; an agent and a door | unchanged; `Agent` from `ProviderOf`; history never rewritten, new readers grouping by provider (X) | unchanged |
| The offers, then `agent_offers` | per door and version | a door | unchanged | unchanged |
| The machine log | `adapter`, `agent`, a failure's `door` | a door; a door; `protocol` or `pipe` | unchanged, spellings from the table | unchanged |
| An opinion's state; a brainstorm's and a meeting's files | `adapter`, `maker`, `posture`, `stubs` | as recorded | unchanged, never recomputed for history (X); `stubs` judged by `StandIn` for new writes | unchanged |
| A hand-over's, a helper's, a partner's ask; a go-on mark | `agent` | a door's id | unchanged | unchanged |
| A trust hold | the trust file's path | Claude Code's | unchanged, from the provider's `Accounts` | unchanged |
| `driver.json`, and w-s's `role_staffing` | `adapter`, `intakeAdapter`, `helperAdapter`, `commands`, `actors[].agent`; the *questions* pause's `claude-code` (`Settings/AccessKey.cs:74`) | doors' ids; a provider's id as a setting's value | unchanged; read through the registry | unchanged |
| The pins, then `harness_wiring` | versions by id (`harnesses.json`) | a program's owner: the provider's id for its binary, the door's for a door's package | unchanged; `ToolSpec` carries its key | unchanged |
| The account homes, cooling, windows | `profiles/agents/<agent>/`; by agent | a provider's id | unchanged | unchanged |
| The lineage | `harness` | a provider's id (`ChildBindings.cs:11`) | unchanged; the fold resolves the provider by it | unchanged |

**What an older build meets.** Nothing it wrote is read differently, and nothing this design writes is in a shape it reads differently. The only
new strings are ids: `alt-stub`'s, on a rehearsal's home alone and listed nowhere without a configured command (`HarnessRoster.cs:863-867`), and
OpenRouter's, whose design measures its own case. An older build that meets a record naming a door it lacks refuses to continue it, naming what
exists, as it does for a removed plugin's door; that it refuses rather than substituting its configured default is tested with the older build's
own reader (X; `compatible-builds.md`). COAGENT1e's negotiated readings, if kept on disk, go in a new table of Agents' that no older build reads,
every column required, and lost they read *negotiated, not yet read*. No binding-details table is added until a reader needs one (§1.1 row 18).
The proof is row t's: every value a shipped build writes in each row above resolves through the registry, held against fixtures of those
builds' own writes. The sync wire gains nothing: a provider's credentials, homes, bindings, negotiated readings and raw readings have no field
there (`session-record-fields.md`).

## 6. The page, the service and the tools (Q15)

The roster's answer (`Modules/Bridge/Routes/AgentsRoutes.cs:123-139`) becomes the sheet, per provider with its doors, and the page reads every
agent fact from it (D182 point 6): a door's agent (`Web/agents/tools.ts:226-316`, `Web/work/say.ts:211-222`, `Web/work/waits/limitWait.ts:63-64`,
`Web/work/head/headFacts.ts:75-86`); the agent's account acts in place of `doors[0]` (`Web/agents/agents.ts:565`, `AgentPage.tsx:156-158`,
`:340-356`); the mark; the windows' names and order; the floor; the offers of each scope (§4.2); *can be stopped* and the cap on a native door's
words from the door's abilities (`WorkFrame.tsx:2635-2636`, `say.ts:309-320`); *takes rules* (`help/access/grants.ts:13-18`); the hand-over's
offer from the driver's `HandOverAsks` (`work/start/handOverOffer.ts:38-86`). Where today's page answers differ from the driver's, the page
takes the driver's in a correction row (s-iii, s-iv, s-vii), never inside the move. **Twins that stay**, each named in the page's census: the
sign-in's progress read from each tool's text (`SignInSteps.tsx:10-14`, `:148-177`), until a row of its own has the driver answer states,
D197's flow unchanged; and the doctrine's layouts (`Cli/layout/harness.ts`, out of scope, the brief §6). **The service** knows no agent but in
two places: the calls' `_meta` lineage keys, one file per provider (§3.3), and the default `claude-code` where an open names no adapter
(`Http/Program.cs:1291-1293`, `:1322-1324`, `:1966-1967`), a census exception until every driver that opens such a record names the door it
started. The lineage hook is Claude Code's asset. The **tools**' sites are the rehearsal's stand-ins (which read the registry's `StandIn`
providers from row b), the proof kit and the dated probes (measurement, kept), and the door-to-owner twin in `tools/live-logins.mjs:34-35`, which
reads the driver's answer (row d). The CLI forwards runtime verbs to the driver and keeps its offline doctrine layouts (X).

## 7. The disagreements this settles (Q16)

Every move row reproduces today. Each disagreement the survey found is settled in a **correction row of its own**, after the move that makes it
one place, with its old and new cases paired and a dated note on the decision it amends (X; the brief §10 point 3).

| # | Question | Today | The answer that stands | Note on, in row |
|---|---|---|---|---|
| 1 | The machine's door (five places, four rules, survey §6) | the loop: protocol where any of Build's choices is (`Loop/Driver.cs:662-690`); the window, the terminal's two and set-up: the configured adapter's, set-up's unknown a pipe (`Setup/SetupWorld.cs:20-29`) | **No machine door.** `Reach.Drives(order, adopted)` asks each of Build's candidates' eligibility through the start's judge, its answer the loop's today; the window, the terminal and set-up read it, the planner is handed it; an unknown id refuses, never a pipe (X's shape, O's answer) | D70, D205; s-i, after g |
| 2 | *A native agent cannot drive an unadopted repository* (five places, §9) | the same answer in five places | **D70's rule stands, once**: `Reach.Unadopted(door)` is *its servers ride the wire*, read by the planner (`Planner.cs:1938-1947`), the walk (`Driver.cs:743-746`), the hand-over's quest (`HandOverQuest.cs:199-205`), set-up's press (`SetupPress.cs:76-82`) and the page. Since the five agree, unifying them is a move (row p); whether a door that hands servers by file may drive one is CONNECTORS1's, measured (§15) | D70; row p, no correction |
| 3 | *It resumes its own conversation* (§5) | `DriverContinuations.ResumesOwn` judges the record's own door; `Trees/SessionGroups.cs:975-983` the machine's adapter | **The record's door**, with a compatible recipe across doors or versions (§4.3): a continuation keeps its record's agent, never the machine's (D205 point 1, D212 §2.4) | D205; s-ii, after m |
| 4 | A stand-in told apart by binary or by name; the stand-ins borrowing Claude Code's tables (§1, §2) | `AgentsCommand.cs:653-658` by binary; `Platform/ActorVerbs.cs:75` and `BrainstormSynthesis.cs:12` by name; `Adapters.cs:1709-1741` copies Claude Code's tables | **`Identity.StandIn`, declared**, answering all three, equal on today's set, a plugin's harness included, held by a table; `stub` and `acp-stub` declare by reference that they speak Claude Code's vocabulary and `acp-stub` Codex's protocol dialect too, never by copy; `alt-stub` speaks its own, so a path that reads one agent's vocabulary for all fails on it | D46's stand-in, D23; row d, no correction |
| 5 | The efforts offered (§4) | the page offers the pipe door's words for every agent (`Actors.tsx:26-27`) | **Two scopes** (§4.2): the page reads each door's session offers from the driver; `max` stays a session's on Claude Code's pipe door and never an account's default (§0.2) | D205; s-iv, after n |
| 6 | The read floor; the token weights (§3) | the floor per provider in the roster, Claude Code's minute for every candidate (`SwitchDecision.cs:70-71`) and on the page (`AgentsView.tsx:26-27`); D179's weights for every agent | **The floor is the provider's**, read by `SwitchDecision` and the page. **The weights stay D179's interim** (§0.2), written in the usage part as D179's product default, not Claude Code's; USAGECAP1b's table takes X's shape (per maker, model and report meaning; unknown calibration, unknown estimate) | D130 (s-iii, after e); D179 (row e, no change) |
| 7 | The Claude Code seam on every protocol door's spawn (§11) | `CLAUDE_CODE_EXECUTABLE` and `DISABLE_UPDATES` from Claude Code's pin on every protocol door (`Adapters.cs:2132-2153`; `HarnessSelection.cs:562-565`) | **On Claude Code's doors alone**, by its `ISpawn`; row h keeps today's set declared on each protocol door as an inherited seam the census names, and s-v takes it off Codex's and dsh's once the install shows neither reads them | D57 (ACP2); s-v, after h |
| 8 | Declared fields nothing reads (§3) | `ProfileMustExist` read nowhere, the home always made (`HarnessSelection.cs:567-580`); `WeekFixed` read by the modules' route alone | **`ProfileMustExist` retired**, today's behaviour kept (a managed home is made before a launch; no start begins refusing an absent home, X); **`WeekFixed` the provider's**, read by its one consumer through the registry | D57; row d (`ProfileMustExist`), row e (`WeekFixed`) |
| 9 | A spawn inherits other providers' credentials and homes (X) | the person's environment passes whole to every spawn but the role variables ENVLEAK1 drops | **Other providers' registered credential and home variables stripped** before the selected account's are bound, never the selected provider's own; after a measurement of each agent's reads | D172 point 3 (a key handed only to its own provider's spawn), beside the FIX-LOG's ENVLEAK1 entry; s-vi ⚖, after h |
| 10 | The leading agent and usage counted by door on the page (§1, §3) | `headFacts.ts:75-86`, `Accounts/Usage.cs:69-76`, `AgentPage.tsx:813-838` by door | **By agent**, a move between one agent's two doors reading as one | D182 point 6; s-vii, after r |

D210's row 1c (the native door's limit cooling its account) is not this design's correction: it is decided and lands on row l's one channel.

## 8. The names (Q17)

| Concept | In code | In prose | On the window, en and zh | Retires |
|---|---|---|---|---|
| provider | `Provider`, `ProviderId`, `ProviderRegistry` | *provider* | never: the window says *agent*, 智能体, by its product's name kept as written | *harness* in new code (the glossary's code word, `glossary.json:595`) |
| agent | a provider's id; `agent` in the records that name one | *agent* | *agent*, 智能体 | — |
| door | `AgentDoor`, `DoorId`, its id the records' `adapter` | *door*: a way in, one meaning in new code (as `HandOverDoor` and `TurnBinding.Door` already mean) | *way in*, 接入方式 | `ISessionAdapter`, `AdapterSet`, `HarnessToolchain` (row t); *adapter* in prose |
| transport | `Transport.Native`, `Transport.Protocol` | *the native transport*, *the protocol transport* | *directly*, 直连; *over the protocol*, 协议 | `SessionWire` (row t); *pipe* for a door in new code (X's `NativeStdio` not taken: *native* already names it) |
| dialect | `INativeDialect`, `IProtocolDialect` | *dialect* | never | `IStreamMapper` (row k) |
| run | `AgentRun` | *the run* | never | the callers' three captures |
| reading | `WireReading`, `IReadingSink` | *reading* | never | `StreamMapped`'s extra fields; `AcpUsage`, named for one door, as the turn's context in Events |
| known | `Known<T>` | *known*, *declared*, *negotiated*, *measured*, *absent* | the sheet's existing words (COAGENT1) | — |
| stand-in | `Identity.StandIn` | *stand-in* | never | the literal ids |

*Pipe* keeps one meaning in new code, the terminal's named pipe (`Platform/PipeName`); the records keep their `pipe` (§5). Nothing new reaches the
window, so the glossary gains no entry (D193's rejection; the brief §5's *simple*); the page's UI *door* (`glossary.json:434-471`) is unchanged.

## 9. The census

One test, `Driver.Tests/Agents/ProviderCensusTests.cs`, with one fixture, `fixtures/provider-census.json`, named to the merge tool (`twins.md`'s
reach rows), and its twins in the page's and the tools' suites reading the fixture's own sections. It fails on **any site outside the providers'
folders** (and the service's per-provider files and the lineage hook) that names:

- **an agent's or a door's id** used as behaviour: a literal compared, switched on, defaulted or passed where a door's spelling decides anything,
  including a prefix (`adapter.StartsWith("codex")`, `Setup/SetupCommand.cs:116-118`) and a helper that only hides the branch (X);
- **a provider's type** (today: the adapter classes, `ClaudeStreamJson`, `ClaudeStreams`, `ClaudeUsage`, `CodexUsage`, `CodexAccounts`,
  `ClaudeTrust`, `ClaudeErrors`, `ClaudeAcpErrors`, `DshProfile`, …);
- **a provider's variable, file or wire key** (`CLAUDE_CONFIG_DIR`, `CODEX_HOME`, `DSH_HOME`, `ANTHROPIC_API_KEY`, `ANTHROPIC_MODEL`,
  `CLAUDE_CODE_EFFORT_LEVEL`, `CLAUDE_CODE_EXECUTABLE`, `DISABLE_UPDATES`, `ENABLE_TOOL_SEARCH`, `.claude.json`, `.mcp.json`,
  `_claude/…`, `claudeCode.…`, `thread_active_writer`, `x-codex-turn-metadata`, …), **the list read from the providers' registrations**, so a
  new provider adds its own vocabulary by registering and never by editing a central list (X);
- **a door's test**: `SessionWire`, `.Wire`, or a door's spelling (`"pipe"`, `"acp"`, `"protocol"`) where it decides behaviour, the terminal's
  named pipe and Node's stdio `"pipe"` told apart (X).

The desktop's projects are read by Roslyn's semantic model, as mod-0's structure gate reads them, so a qualified, aliased or static reference
counts; the service's three projects by their syntax trees (they can name no driver type, so literals and keys are what can cross); the page
and the tools by their own suites over the fixture. **Every site today is a named exception**, file and token, with the row that removes it
(O §0 item 7's baseline: `SessionWire.Acp` or `.Pipe` 55 times in 23 files, `.Wire` 56 times in 26, an agent's id as a literal 36 times in
15, outside the tests, by search, a bound from above). **The permanent ones carry their decision**: D23's supported default
(`Settings/DriverConfig.cs:72`); D209's holder (`Modules/Platform/HolderTerminal.cs`, its argument vector D209's, read through the registry from
row h); D72's rule grammar, Claude Code's and the product's; the terminal's named pipe (`Platform/DaorisHome.cs`,
`Modules/Platform/InstancePipe.cs`); the doctrine's layouts (the brief §6); ids as examples in help and refusal text, classified as words; the
dated probes, fixtures and evidence, classified as measurement. Classified is never blanket: each is a site, never a folder (X). The completion
criterion is **no unexplained production site outside the providers** (X), reached at row t.

## 10. The conformance suite and the third stand-in

**The conformance suite**, `Driver.Tests/Agents/ProviderConformanceTests.cs`, a theory over every provider and door the registry holds, the
stand-ins with no model in it, run per provider so a provider that answers the contract wrongly fails there rather than in a caller. **Every
provider passes the mandatory cases** (X): its identity's rules (§2.2); every part stated; its ids and aliases; a full or asking start with none
mapped refused naming it; a recipe setting no role and no `DAORIS_` variable, its key variables only in its own spawn, and no other provider's
credential or home in it (from s-vi); each word table's patterns standing on a recorded sentence and reading it (`AgentErrorsTests`' rule,
generalized); D210's precedence over its tables; an unsupported operation refused before a write; unknown and malformed frames kept unread,
never guessed; a provider unable to widen admission or containment; and its old records' ids resolving exactly. **Each ability it states runs
its applicable cases**, and **an ability stated absent passes its fallback's case** (X): the dialect over its recorded frames
(`fixtures/providers/<id>/`, Claude Code's and Codex's from the frames today's tests hold) giving the readings expected and emitting nothing it
was not told; and for a stand-in, its program run live, serially, as real model-free processes: a start, a turn, a resume of its own
conversation, a missing conversation and another active writer, a word attempted before the wire and acknowledged after it and never replayed
once uncertain, a limit cooling the bound account with no strike, a transient error tried again in the same conversation within D210's bounds,
access and fatal on their paths, a child seen and ended and duplicate child signals merged, an unknown parent and an unknown end kept unknown,
usage with a null count where none was said and computed context marked computed, a reported model distinct from the one asked, a refused model
passing to the next choice before any prompt, a sign-in, a pin from controlled infrastructure, and a listing with a command configured.
**`fixtures/provider-pending.json`** holds each case a provider cannot pass yet with the row that makes it pass; a pending case that passes fails
the suite until its line goes (O).

**The third stand-in**, `alt-stub` (row c): `StandIn`; its home variable; a sign-in and login question in its own words; limit, window,
sign-in and error words in a grammar no real agent uses, each on a sentence its program prints; its usage question; its children's frames, with
an explicit end; its conversation home; its postures; and two doors, `alt-stub` (native, its own line format, its resume by its own flag) and
`alt-acp-stub` (protocol, its own `_meta` keys for its model, usage, windows and children). Its program is one file in its folder, both doors by
an argument, with its fixtures beside it (X). No vendor download and no account: its pin is served by a controlled fixture, else pending,
named. **The diff is its proof**: outside its folder, its one line in `Providers.Built()`; a change anywhere else is a gap in the contract, named
and closed before the row lands (both).

## 11. The rows, in build order

Each row is its own branch, after FOUNDATION1's mod-ix and on files no open branch holds (D212 §14.6), so a row touching `Loop/Driver.cs`,
`ChatRunner.cs`, Course's or Participants' files waits for no mod row holding them (mod-xii, mod-xiv, mod-xxi). Each lands with every gate of
`daoris.gates.json` green and the brief §9's suites for its part run whole, real-process suites and rehearsals serially, and leaves Daoris
working as it did for a person who sets nothing, but a correction row's one named change. **Rows marked ⚖ move or change what an agent may do
or what reaches it (a posture, a rule, a credential, a server, a manifest's claim) and are built only after both makers' reads find nothing
blocking in them** (D191's note of 2026-10-24; the brief §2 point 4). *Row a* is AGENTPROVIDER1a, and so on. The installed proof after the rows
that move the protocol doors (i, j, l, m) is the parent's, on the install, after each row is staged: a session on Claude Code's and on Codex's
protocol door reads, resumes, cools and ends as before, its records readable by the build before.

**Row a first** because it is a day, changes nothing and makes every later row measurable: the registry over today's adapters answers every
question the contract asks, and the census names every site a later row moves. **Row b** builds the plumbing the stand-in needs, so **row c**
can add it by its folder alone: the stand-in is the contract's meter, each case it cannot pass yet a named gap with the row that closes it.
**Row d** is the most copied fact and OPENROUTER1's first need.

### a. The contract named, the registry over today's adapters, the census. No behaviour. A day.

- **Code.** `Agents/Contract/`: `Provider`, `AgentDoor`, `ProviderId`, `DoorId`, `Transport`, `Known<T>`, the parts of §2.3 as records, no port
  yet. `Agents/Registry/ProviderRegistry.cs`: `ProviderRegistry.Of(AdapterSet)` groups today's adapters by their owner, folds the `codex`
  holder in as Codex's provider, and answers `Providers`, `Door(id)`, `Provider(id)`, `ProviderOf(door)`, `Reserved` and `Resolve(id, kind)`;
  `RecordIds.cs`, §5's table. Nothing reads them but their tests. D212 takes a note that the *Agents names …* edges §2.7 assigns here are this
  design's cuts.
- **The census** (§9) with every site the survey lists as an exception, file and token, with its removing row.
- **What a person sees.** Nothing.
- **Proof.** Unit: the registry over `AdapterSet.Built()` with a plugin fixture (a harness with `accountOf` and one without) answers, for every
  door, the owner `Toolchain.Owner` gives and, for every fact the four copies answer (limits, sign-in, windows, usage question, login question,
  sign-in command, reporting door, readiness: `HarnessRoster.cs:120-197`, `:782-841`, `:1694-1715`, `:2082-2098`), a table of today's answers,
  `fixtures/provider-owner.json`, each cell where two copies disagree listed as row d's decision; every id of §5 resolves by its kind; an unknown
  one is D23's error naming what exists; `codex` resolves as a provider and is refused as a door. Census: passes at the row's commit, and fails
  on a planted `"codex-acp"` literal in a Loop file, a planted `SessionWire.Acp` test in a Course file, a planted `ClaudeStreams` reference in a
  Participants file, a planted `adapter.StartsWith("claude")`, and a planted `CODEX_HOME` in the modules; passes the terminal's named pipe.
  `StructureTests` whole, the contract naming nothing above Agents.

### b. The shim, the one registration list, the conformance suite, and stand-ins discovered. No behaviour.

- **Code.** The ports a provider written on the contract alone needs: `ISpawn`, `INativeDialect`, `IProtocolDialect` (declared, read from row i),
  `IUsageQuestion`, `IChildVocabulary`, `IConversationHome`, `AgentRun`. `Agents/Registry/Providers.cs`: `Providers.Built()`, **the one
  registration list**, empty of providers here. `Agents/Registry/LegacyDoor.cs`, the shim that presents a contract door to today's readers as an
  `ISessionAdapter` with its `HarnessToolchain`, and its native dialect as an `IStreamMapper`; `AdapterSet.Built()` takes `Providers.Built()`'s
  doors through it, once, so a provider added later touches no adapter file. The shim records today's protocol-resume default as eligibility to
  negotiate, never as a resume known (X; `Adapters.cs:1506` against `Acp.cs:3457-3479`). The conformance suite (§10) with its pending table.
  The family rehearsal (`tools/family-rehearsal.mjs`) and the suite take their stand-ins from the registry's `StandIn` providers, and their
  programs and fixtures from each provider's folder by one layout (X), once, so no later stand-in changes either. `PluginCatalogTests` holds the
  reserved names as the shipped list and, beyond it, every id the registry holds, so a provider is reserved by registering.
- **What a person sees.** Nothing.
- **Proof.** Both of today's stand-ins run through the shim with their behaviour compared, old against new (X): a real-process open, send, stop
  and resume on each door; the family rehearsal's stub phases whole; the conformance suite green over today's providers with every case they
  cannot pass yet pending and named; a planted pending case that passes failing the suite.

### c. The third stand-in, `alt-stub`, through the contract alone.

- **Code.** `Agents/Providers/AltStub/` (§10) and its one line in `Providers.Built()`. Nothing else.
- **What a person sees.** Nothing: listed nowhere without a configured command, as `stub` is not (`HarnessRoster.cs:863-867`).
- **Proof.** The diff: its folder and one line, nothing else, a gap named and closed before it lands (both). Conformance: at this row it passes
  its start, turn, resume, error, sign-in and listing on both doors and its usage and model on the native one, and is pending, each case named
  with its row, its owner's facts (d), windows and limit grammar (e), pin (f), spawn (h), protocol `_meta` (i), children and one failure channel
  (l), conversation home and the run's operations (m). The family rehearsal runs a third pair, `alt-stub` and `alt-acp-stub`, with no edit to the
  rehearsal. The census holds no `alt-stub` site outside its folder.

### d to t

| Row | What moves behind the contract | What a person sees | Proof |
|---|---|---|---|
| **d** the owner, once | `ProviderOf(door)` the one reader of the agent a door runs as: every read of `AccountOf`, `Owner(...)` and `AdapterSet.Holder` (`HarnessRoster.cs:66-72`, `:114`, `:120-144`, `:188-197`, `:427-462`, `:476-498`, `:766-780`, `:782-841`, `:1694-1715`, `:1987-1999`, `:2082-2098`; `AgentErrors.cs:249-261`; `UsageTurns.cs:23-29`; `AgentsCommand.cs:624-642`; `AgentAccountsCommand.cs`; `AgentListCommand.cs:110-111`; `AgentLoginCommand.cs:129-147`; the hand-over's three; the modules' `AccountsRoutes.cs:48-75`, `AgentsRoutes.cs:206-216`, `AgentActions.cs:212-236`); `Agents/Providers/Codex/` holding agent `codex` and door `codex-acp`, Codex's limit words and usage question moved from the door to the agent; `claude-code-acp`'s identity and trust file read from its provider; `Identity.StandIn` read wherever a stand-in is told (§7 row 4); `ProfileMustExist` retired (§7 row 8); `tools/live-logins.mjs` reading the driver's answer | nothing | `provider-owner.json` equal cell for cell; `CodexAccountTests`, `HarnessTests`, `AccountRotationGoalTests`, `AccountRotationWalkTests`, `AccountRotationTickTests`, `ResumeAccountTests`, `AgentsCommandTests`, `AgentErrorsTests`, `HandOverTests` and the modules' tests whole; a stand-in table equal on today's set, a plugin's harness included; `LimitWaitsProcessTests` and `ChatGoesOnProcessTests` whole (a limit on `claude-code-acp` cools a `claude-code` account as today); `alt-stub`'s account cases un-pend; census shorter by every owner site |
| **e** the accounts' readers | `UsageKind`'s switch to `IUsageQuestion` (`ClaudeUsage`, `CodexUsage` into their folders, `HarnessRoster.cs:2287-2360`); each provider's floor; its window words (`AccountReadings.cs:107-183`, `:299-335`), limit grammar (`AccountLimits.cs:121-125`, `:197-208`, `:296-361`), sign-in, refused and error words split into the provider's and the door's tables; `ClaudeTrust` into Claude Code's folder; the key variable read once (`AgentLongCommand.cs:117-122`); `ToolHomeLeftovers`' homes from the providers; `WeekFixed` through the registry; D179's weights written as D179's | nothing (`SwitchDecision`'s floor stays as today until s-iii) | the usage and limit readers' tests, `AccountReadingsTests`, `AccountRotationTickTests`, `AgentErrorsTests` whole; `alt-stub`'s windows and limit grammar un-pend; census shorter by the survey's §3 sites |
| **f** the program | each provider's and door's program as a `ToolSpec`, on the shape FOUNDATION1's *Install names Agents* cut left; the release sources as Install-owned `IReleaseSource`s (`Setup/ReleaseChannels.cs:25-80`'s switch on ids gone; Claude Code's signed bucket and its verified hash a source's property, `ClaudeReleaseDownloads.cs:130-132`); `AgentPinCommand`'s second copy of pin, update and newest (`:69-325`) onto `HarnessActions`, each difference listed as a correction before it lands | nothing | `ToolInstallTests`, `ToolResourcesTests`, `HarnessTests`, the pin's and update's suites whole; the deployment rehearsal; `alt-stub` pinned from a controlled fixture, else pending, named |
| **g** the doors as abilities | every `Wire` test in a caller (survey §6, §9) read as a door's `Known` ability; `Reach.Drives` and `Reach.Unadopted` written, the loop reading `Drives` with today's answer, the window, the terminal's two and set-up reading their configured door's abilities with today's answers until s-i | nothing | `PlannerTests`, `ContinuationTests`, `HandOverTests` and the set-up suites whole; a case table asking `Reach.Drives` the loop's cases; census without `SessionWire` outside the transports, the providers and the readers s-i moves, each named |
| **h** ⚖ the spawn | `ISpawn` per door returning a `ProcessRecipe`; `SpawnRequest` with Daoris's values applied after it, the composed target, role variables, git wiring and review's environment handed in; the instruction composed by Context; `DshProfile` into its folder; the maker's attribution into `Identity`; `HolderTerminal`'s program, path and hash through the registry, its vector D209's; the Claude Code seam declared on each protocol door as today's inherited seam, named for s-v; cuts §2.7's spawn edges | nothing | `PermissionSpawnTests`, `EveryChildIsHandedTheToolsTests`, the adapters' tests whole; the environment of each built-in door's spawn equal to today's, variable for variable, in a table; conformance's recipe cases un-pend for every provider (no role variable, no `DAORIS_` variable, keys only in their own spawn); ⚖ since it rehomes how credentials reach a process |
| **i** the protocol transport and its dialects | each per-agent read of `AcpSession` behind `IProtocolDialect` (§2.6), **every protocol door on a composite dialect** (Claude Code's and Codex's readers together), so nothing changes; the error shapes per dialect (`Acp.cs:175-239`); the retry's policy typed on `DoorFailure` (`TransientRetry.cs:3-31`); `alt-acp-stub`'s own dialect | nothing | `AcpTests`, `AcpBackgroundTests`, `AcpSteerTests`, `AcpTransientTests`, `AcpResumeTests`, `ClaudeAcpTests`, `ChatTurnsTests` and every tick test that plays Claude Code's or Codex's `_meta` (§0.2's 24 files) whole; `alt-acp-stub`'s model, usage and windows un-pend; census without `_claude/`, `claudeCode` or `thread_active_writer` in the transport; the parent's installed proof |
| **j** ⚖ each protocol door its own dialect | `claude-code-acp` reads Claude Code's, `codex-acp` Codex's, `acp-stub` both, `dsh` and a plugin's door the spec's alone; `jetbrains.air` sent to Claude Code's door alone (`Acp.cs:1789`) | none on the built-in agents, each reading what it emits; a plugin's door reads no agent's extension, and Codex is sent no `jetbrains.air` | row i's suites; dsh's recorded frames (`tools/dsh-probes`) read the same; the parent's installed proof that a Codex session opens, resumes, is refused `elsewhere` and ends as before with no `jetbrains.air` sent, and a Claude Code session queues, steers and reads its windows as before; a dated note on D53 (a configuration of the door reads its own extensions); ⚖ since it narrows what each door reads and what Codex is sent |
| **k** the native transport and Claude Code's native dialect | `StructuredOutput` and `ClaudeStreams` into Claude Code's folder as its native dialect, `StructuredOutput.cs`'s shared record staying with the transport; the four `stream-json` readers one; the native retry once; native turn framing, interrupt and resume behind `AgentRun`, driven work and conversations sharing it (X) | nothing | `NativeStreamsTests`, `ClaudeStreamJsonTests`, `ChatGoOnTests`, `ChatGoesOnProcessTests`, `ContinuationTests` whole; exact receipt order and no attempted word repeated (X); one input frame never two readings or two lineage lines; `alt-stub`'s native lines read by its own dialect |
| **l** the readings and the sink | `WireReading` and `IReadingSink` (§3), both transports writing it; `ChildBindings`' branches per harness into the providers' `IChildVocabulary`; `HarnessEnding`'s fields as readings; every failure one `Failed` reading, the native limit still cooling nothing until D210's row 1c switches its reader; cuts §2.7's sink edges | nothing | `NativeStreamsTests`, `ChildLineageTests`, `LineageHookProcessTests`, `ChatGoesOnProcessTests`, `LimitWaitsProcessTests`, the service's `CallLineageTests` whole; a fixture session's event file byte for byte as before on each door; `alt-stub`'s children reach the lineage and its failure one channel; the console's latency on the install before and after, the parent's |
| **m** the session's life | the executors hold `AgentRun`: start, resume, words, steer, stop and close through it; the protocol resume once for a driven run and a conversation; the hand-over's accounts once; `IConversationHome` (SWITCHCTX1's table); `DoorInput` with Course's attempt port, cutting §2.7's Course edges; `ResumesOwn` the one rule for the continuation, `SessionGroups`' report left for s-ii | nothing | `ContinuationTests`, `ChatGoOnTests`, `ChatGoesOnProcessTests`, `ChatTransientProcessTests`, `HandOverTests`, `AcpResumeTests`, `LimitResumeTickTests`, `DrivenInboxTests` whole; an unsupported operation refused before a write on each door; `alt-stub`'s conversation home un-pends; the parent's installed proof that a session on each protocol door resumes its own conversation and ends as before |
| **n** models | the providers' `Models`; Claude Code's settings schema into its folder; `ActorRun`'s asked from the door's spawn; offers through the registry in two scopes; asked, carried, acknowledged and reported kept apart; `ModelAsked`, cutting §2.7's Roles edges; FOUNDATION1's row j adopting it as §2.8 says | nothing (the page's efforts stay until s-iv) | `ActorChoicesTests`, the actor run's and offers' tests; a refused model passing to the next choice before any prompt on each door (the brief §9); a reported model never the asked one where none is reported |
| **o** ⚖ permissions, skills and hooks | Claude Code's `IRuleLanguage` (its settings file, hooks, hard deny, `mcp__` names: `Permissions.cs:105-143`, `:487-588`); `ISkillLayout`; `PostureAsked`, cutting §2.7's Help and Participants edges; the trust preflight through the provider (`Driver.cs:2282-2303`, `DriverIntakes.cs:262-272`, `DriverRoutes.cs:367-398`); the lineage hook Claude Code's asset; the rooms' Claude Code files named as the doctrine's | nothing | `PermissionRulesTests`, `PermissionSpawnTests`, `PermissionSeamTests`, `AgentsTextTests`, `McpToolsTests` whole; a full or asking start on a provider with none mapped refused naming it (the brief §9); the settings each built-in door is handed equal byte for byte; ⚖ since it moves postures and rules |
| **p** ⚖ connectors | `IServerHanding` and `ConnectorPlan`; `ServerEntry` on the shape FOUNDATION1's *Knowledge names Agents* cut left; the five callers' door tests and the helper's own gone; `Reach.Unadopted` read by the five places of §7 row 2; each provider's own servers answered, absent but Claude Code's native door's, measured | nothing | `McpToolsTests`, `EveryChildIsHandedTheToolsTests`, the helpers', reviewers', partners' and intakes' starts whole; the entries each door hands equal to today's, the session's identity in each; ⚖ since what servers a session gets is what it may call |
| **q** ⚖ plugins' declared doors | the factory of `DeclaredDoor`s in Agents (§4.8); Plugins converting manifests; `AdapterSet.WithPlugins` out of Agents, cutting *Agents names Plugins* | nothing from the move; `fullPosture` and `askingPosture` stay COAGENT1e's own reviewed row | manifest refusal cases (a claim with no field refused at the manifest's read), provenance, reserved ids, absent-ability conformance for a plugin's door; `PluginCatalogTests` whole; ⚖ since it is where a manifest's claims are held |
| **r** the page, the service and the tools | the roster's answer as the sheet (COAGENT1e, built as this read); the page's agent facts from it (§6); `IAgentFacts` read by the room; the service's call keys one file per provider and their twin fixture; the open's default a named exception | the same controls and words, from one answer | `AgentsCommandTests`, the modules' roster tests, web-unit with the page's census; the service's `CallLineageTests` with its twin fixture; translation parity in both catalogues; no `doors[0]` and no tool-text parser deciding an act |
| **s-i** the machine's door | §7 row 1 | a window, the terminal and set-up say an unadopted repository can be driven where Build's later choice rides the protocol, as the loop already starts it | old and new cases paired for the window, the terminal's two and set-up (none tests `DriverContext.DoorOf` or `SetupWorld.DoorOf` today); an unknown id refused, never a pipe; notes on D70 and D205 |
| **s-ii** a resume's own conversation | §7 row 3 | a group's report of a resume agrees with what the continuation does | the old disagreement planted and failing; `SessionGroups`' cases on Build's second choice; a note on D205 |
| **s-iii** the read floor | §7 row 6: `SwitchDecision` and the page read the candidate's provider floor | a Codex account read at most every five minutes as a candidate, as the roster already reads it | a Codex candidate read *first* inside its floor, old and new (§15); the page's floor per agent; a note on D130 |
| **s-iv** the efforts | §7 row 5 | each agent's actors offer that door's efforts; `max` only where a session may carry it | the page's actors' tests with Codex's and Claude Code's offers; the driver's refusal of `max` as a default unchanged; a note on D205 |
| **s-v** the Claude Code seam | §7 row 7, after the install shows Codex's and dsh's adapters read neither variable | nothing, two variables gone from Codex's and dsh's spawns | the spawn table without the two on those doors; the parent's installed proof that a Codex and a dsh session start as before; a note on D57 |
| **s-vi** ⚖ other providers' credentials | §7 row 9, after each agent's reads are measured | nothing, where nothing relied on another agent's key | the conformance case (no other provider's credential or home in a recipe) un-pends for every provider; a selected provider's own inherited variables kept; ⚖ since it changes what credentials reach a process |
| **s-vii** counted by agent | §7 row 10 | the leading agent and usage counted by agent, a move between one agent's two doors one agent | the page's head and usage tests, old and new; a note on D182 |
| **t** the old seam's end, and the records held | `ISessionAdapter`, `HarnessToolchain`, `SessionWire` and `AdapterSet` gone once nothing reads them; every built-in in `Providers.Built()`; the shim gone | nothing | the census's exceptions none but the permanent ones; the pending table empty; every value of §5 a shipped build writes resolving through the registry against fixtures of those builds' own writes; an older build's own reader refusing a record naming a door it lacks rather than substituting its default; the family and deployment rehearsals whole; the parent's installed proof |

**The order**: a, b, c, d, then e to r by part, each correction after the move that makes it one place (s-i after g, s-ii after m, s-iii after
e, s-iv after n, s-v and s-vi after h, s-vii after r), t last; D210's row 1c lands on l's channel; OPENROUTER1 after d, e, f, h, l and n.
**For a person's eye** (the brief §9, each before the row that could break it): a session on each provider starts, takes a turn, resumes its
own conversation and ends as today (h, k, l, m); a limit cools the account it ran as and never strikes (d, e, l); a transient error is tried
again in the same conversation (i); a refused model passes to the next choice before any prompt (n); a full or asking start on a provider with
none mapped is refused (o); a record an older build holds still reads (t).

## 12. OPENROUTER1, the first new provider

Its design is its own, written on the contract once rows d, e, f, h, l and n have landed (the owner, the accounts, the program, the spawn, the
readings, the models). What the contract gives it, so that no file outside its folder changes but those its own design names:

- **Its executor named** (X): an inference endpoint alone does not satisfy the contract. The row's reading is Claude Code's program pointed at
  OpenRouter (the base address, the key in one variable, another left empty), so its registration **runs Claude Code's program** and its doors
  reuse Claude Code's native and protocol dialects by reference, an edge the census allows only where a registration declares it (O). Whether
  Codex's program can be pointed there is unread, and OPENROUTER1's.
- **A key account**: `Accounts.Kind = Key` with its own variables, handed to its own spawn alone, kept in `profiles/` (D172 point 3).
- **Its offers** declared or read, never Claude Code's; its limit and error words from recorded sentences, a daily cap read as a limit and
  D125's cool-off.
- **`Reach`, opened repositories alone**, refused at the start layer (D212 §2.3) before any context or credential is sent (X).
- **Its gateway is no proof of another maker** (X): where the model's maker or routing is unknown, a review it serves reads its independence as
  unknown (D155), never as another maker's.
- **Proof** as its row names: a stand-in in its shape (a key account, no network), its limit read as a limit, a closed repository refused;
  conformance whole; the census holding no `openrouter` site outside its folder. **The measurement with a real key is the owner's** (§13).

## 13. What is the owner's (Q19)

**Nothing in rows a to t, s-i to s-vii included, needs D191 point 3's answer.** None changes the product's direction: the owner's direction of
2026-10-26 asks for this structure, and the corrections each make two places agree on an answer a decision already gave. None spends an account
or money beyond the development's own runs, signs in, publishes, pushes or rewrites history. None loses data: no record is deleted or
reshaped; `ProfileMustExist`, the `codex` holder and the old seam are declarations in code, not records. The installed proofs are the parent's,
on the install, after each row is staged. **Near the line, and both makers' rather than the owner's**: rows h, j, o, p, q and s-vi move or
narrow what an agent is handed or may do, and each is built only after both makers' reads.

**Asked in OPENROUTER1's design, not before** (each its own question there):

1. **A real OpenRouter key and its first measurement.** *What it is:* OPENROUTER1's measurement row, an orientation pack and a brainstorm voice
   on free models compared with Sonnet 5.5's on the same input. *What a yes does:* lets that row run with a key the owner enters in the window, on
   free models alone, which OpenRouter caps and may log; a paid model's first measurement is a further yes. *Where to look:* the OPENROUTER1 row in
   `TASKS.md` and §12 here.
2. **The key itself.** Entering it is a sign-in in D191's sense: the owner's own act in the window, never a session's.

**Read by the steward, the owner may overrule:** OpenRouter appears on the Agents page as an agent of its own, its accounts keys and its
models its own, running Claude Code's program underneath, as the OPENROUTER1 row's *as a provider* says; an account kind of Claude Code would
put OpenRouter's models among Claude Code's choices and walk its keys with Claude Code's accounts. Asked only if OPENROUTER1's design departs
from it.

**Not asked, and not proposed:** a rule language of no agent's. The person's rules stay Claude Code's grammar (D72), so Codex, dsh and a
plugin's agent take none, said on the page; a direction to change that would be a design of its own.

## 14. Rejected

| Rejected | Why |
|---|---|
| One interface, today's grown, or an interface of facets with defaults | §1.1 row 1: a member left out answers by omission |
| A provider per agent-and-door pair | every account fact declared twice and the owner rebuilt wherever a door is read (survey §15 tangle 3); OpenRouter on two pairs would cool on one door and read ready on the other |
| A provider with no door | §1.1 row 4 |
| Negotiating everything at the open | the native transport negotiates nothing, and no fact of another program is claimed without a measurement (D53, D57) |
| A negotiated reading admitting a later run | §1.1 row 3 |
| The native transport as one provider's own, or the shared transports inside the providers' boundary | §1.1 rows 5 and 6 |
| A provider's own event channel, or a provider minting events | FOUNDATION1 mints at the hub of the process that runs the work and by the store's trigger (D212 §4.1); a child seen by the hook and the stream would be told twice |
| The readings declared in Agents | `Observation`, in Events, would read them upward for good (O §0 item 2) |
| A protocol `end_turn`, a stop or a failure read as a completed part | a turn's end is no part's end; an answer is the answer judge's (§3.2) |
| An `agent` field beside `adapter` on the session record | a teammate's older build would not write it, so a newer build would hold two truths about one record |
| Renaming the door ids | every record, choice and pin holds them (§5) |
| A binding-details table now | §1.1 row 18 |
| Narrowing the dialects, or any other correction, inside a move | a behaviour change hidden in a refactor (the brief §6); §1.1 row 10 |
| Today's stand-ins given a vocabulary of their own | §1.1 row 9 |
| A lineage hook every provider must take | Codex and dsh could answer only by claiming what they cannot keep |
| A manifest declaring a dialect, words or abilities | a manifest is a word, never a measurement (COAGENT1 §3.4) |
| An assembly per provider now | D212 §14.4's reasons hold; a provider's folder can become one once its edges all point down |
| *Provider* on the window | the person knows agents and ways in; a new word would be vocabulary D193 rejects |
| A universal rule translator | the person's rules are Claude Code's grammar (D72); a rule model of no agent's changes what the person types, a design of its own |
| A provider deciding to resume on another account at its limit | a second account rule beside `LineAccount`, LIMITSWITCH1's class (D212) |
| X's token calibration now, X's `WeekFixed` in the driver now | §1.1 rows 15 and 16 |
| Cutting the lower *… names Agents* families a second time | FOUNDATION1's cuts land them first (§0.2) |

## 15. What stays unmeasured, and what this design's checks do not cover

- Whether Codex or dsh emits any of Claude Code's `_meta` keys, or reads `CLAUDE_CODE_EXECUTABLE` or `DISABLE_UPDATES`: rows j and s-v assume
  neither, and measure both on the install and in dsh's recorded frames before they land.
- Which variables each agent reads from its inherited environment, so s-vi strips none an agent relies on: measured before s-vi.
- Whether a plugin's harness is installed on any machine, so row j's narrowing would change what one reads: no plugin harness is among the
  install's doors as the brief §5 reports it (read from the brief, not from the install).
- Whether Claude Code's native door handed Daoris's connector by `--mcp-config` beside a repository's own `.mcp.json` server of the same name
  conflicts: CONNECTORS1's, before any change to D70's rule.
- Whether `alt-stub` can be pinned with no network from a controlled fixture; if not, its pin stays pending, named.
- OpenRouter's facts (its variables, its caps, its tool use through Claude Code's program), all unconfirmed in its row: OPENROUTER1's.
- `AgentRun`'s and the sink's cost on the console's hot path: expected to be a call where two callbacks are today; row l measures the console's
  latency on the install before and after.
- Whether a Codex candidate read *first* inside its five-minute floor is declined by the roster today (§7 row 6): read, not run; s-iii's case runs
  it.
- **What this round's checks do not cover.** It is a document. `npm run verify` and the documentation checks prove its routing, its index and its
  record shapes, not its claims about the code; those were read at `0f4d2a8a7` for the rows of §0.2 and otherwise rest on the survey's and the
  proposals' citations, which both makers' reads check next.

## 16. Rounds

| Round | Written | Read | Answered |
|---|---|---|---|
| 1 | 2026-10-26, Claude Opus 5.5, from main at `0f4d2a8a7`: the synthesis of Codex's and the independent Opus reader's sealed proposals, every question of the brief answered (§1), each difference chosen with its reason and counterexample (§1.1), the rows in order with their proofs (§11), D213 proposed | owed: Codex's, and the independent Opus reader's as a foundation read, neither shown the other's (D191 point 1) | — |
