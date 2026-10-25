# One provider contract every agent implements — the brief

> AGENTPROVIDER1, phase 1 of a foundational co-design under D191 (2026-10-26). This brief is the one document every proposal is
> written from. It holds the direction, the process, what the contract must cover, the constraints a proposal keeps, what is out of
> scope, the proof the design must name, the questions every proposal answers, what must not regress, and how the work lands. The
> facts of today are in `docs/2026-10-26-agent-provider-survey.md`, by `file:line`, and are not repeated here: a proposal cites the
> survey's section and the code's line, never this brief, for a fact. The proposals will be one file per maker,
> `docs/2026-10-26-agent-provider-proposal-<maker>.md`, each written from this brief and the survey alone and sealed before its author
> reads another; a synthesis follows in a later quest. `Dnn` is `docs/decisions/Dnn.md`. Paths follow the survey's aliases.

## 1. The direction

**Decided (the owner's direction, 2026-10-26, AGENTPROVIDER1):** Daoris will support more agents than the two makers it runs today, so
what an agent is to Daoris becomes **one provider contract that every agent implements**, and every place that tells one agent or one
door from another outside a provider moves behind it. Claude Code, Codex and the stand-ins are its first providers, and OpenRouter
(OPENROUTER1) is the first new one and its proof. It is designed now, under D191 as a foundation, and built after FOUNDATION1's move of
the Agents subdomain (row mod-ix, D212), never as a big-bang rewrite.

The steward's reading, which the survey tests against the code: what an agent is to Daoris is spread over the adapters and their two
doors (`Agents/Adapters/Adapters.cs`, `Agents/Acp/Acp.cs`), the harness record under `Accounts/Harnesses/` (`HarnessToolchain`), one
harness's stream reader (`Agents/Streams/ClaudeStreams.cs`), per-agent tables for limits, windows, sign-in, usage and errors, the
permissions and skills handed at a spawn, and doors named by strings in several records (`TurnDoor`, `HandOverDoor`, `DoorFailure`,
`TurnBinding`, `HelpWired`), so each new agent, and each question of what one agent's child or connector looks like, reaches many files.
COWORKVIEW1's rounds kept meeting it (`docs/2026-10-24-cowork-view-design.md` §1.1, §3.6): a connector handed its session by the
environment on one agent and by its server entry on another, two doors' lines for one child, two harnesses' child announcements, and a
Codex child that exists only through its calls. The survey counts 350 sites in 234 files, 160 of them accidents of where the code
was written rather than differences of the agent (survey §0, §15).

**The part of the company it makes work** (D193, `docs/2026-10-21-company-concept.md` §2): the staff (part 4), since every session is an
agent on an account; the equipment and vendors (part 14), since an agent is a vendor's product Daoris runs; and finance (part 17), since
an agent's accounts, limits and usage are its seats and their allowance. A company that hires from a new agency by one contract, rather
than by rewriting its handbook for each, can take on the next agency at the cost of that contract alone.

## 2. The process

D191 governs it, with its amendments of 2026-10-20 and 2026-10-23 and its note of 2026-10-24. This design changes a foundation in the
senses the 2026-10-20 amendment names: a protocol between processes (the seam every agent's process is started, spoken to and read
through), and decided rules other designs rest on (D23, D24, D53, D57, D98, D125, D199, D201, D205, D210). So:

1. **Three sealed proposals**, each from this brief and the survey alone: Claude's first, then Codex's and an independent Opus reader's
   (in Fable's place, D191's amendment of 2026-10-23). No author reads another's before its own is sealed.
2. **One synthesis**, written by Claude Opus, as a proposed decision and a design document, comparing the proposals question by question
   (§7) and saying what each choice rejects.
3. **Both makers read it**: Codex, and the independent Opus reader as a foundation read. Each finding names the file and line it rests
   on, and the steward checks each against the code before it is taken.
4. **Rounds until both makers' blocking findings are taken.** By D191's note of 2026-10-24 this design is not accepted on one maker's
   read, and a part of it that widens what an agent may do (a posture, a full or asking mode, a rule a provider takes or does not) or
   deletes a record is not accepted without both.
5. **Rows**, each its own branch, each behind today's tests (§10), none before row mod-ix has landed.

**What the owner is still asked** (D191 point 3): a change of the product's direction; anything that spends the owner's accounts or
money beyond the development's own runs (an OpenRouter key's credit, a paid model's first measurement); a sign-in; anything that
publishes, pushes or rewrites history; anything that loses the owner's data. A proposal that needs one of these says so, in a list of its
own, each question saying what it is, what a yes does and where to look.

## 3. The inputs

These designs are decided or in flight. Each is an input to AGENTPROVIDER1, not a competitor of it: a proposal says, for each, whether
the contract absorbs it as it stands, carries it as a provider's declaration, or re-cuts it.

| Design | Standing | What it brings to the contract |
|---|---|---|
| FOUNDATION1, D212, `docs/2026-10-25-foundation-design.md` | Part A accepted and building (mod-ii landed; mod-ix, the Agents move, ahead); Part B accepted, its rows waiting | the module map (§14: Agents is subdomain 9, with the lower and higher subdomains it may and may not name), the participant's `Binding` (adapter, account, door, harness version, the kept conversation, §2.1), the start plan and its settled selection (§2.4), the four admission layers and containment by capability (§2.3, §2.7), the event envelope and vocabulary (§4.1, §4.2: `part.ended` and its class, `part.limited`, `child.seen`, `child.ended`), the desktop store (§13) |
| MODELROLE1, D205, `docs/2026-10-23-actors-design.md` | accepted; rows a to e built, the later rows open | a model and effort carried at the session level per door (`set_config_option` on the protocol door, `--model` and `--effort` on the pipe door), offers per agent kept in `data/agent-offers.json`, one judge for every door (`ActorChoices.Judge`), the walk choice by choice; what each door accepts, measured (`docs/2026-10-23-model-effort-doors-evidence.md`) |
| AGENTERRORS1, D210, `docs/2026-10-24-agent-errors-design.md` | accepted on the amended text; rows 1a, 1a2, 1b, 1b2 built | one reader at the agent interface (`AgentErrors.Read`), each door handing a `DoorFailure`, each agent's `ErrorWords` read own then owner, the four kinds |
| COAGENT1, D199, `docs/2026-10-22-coagent-design.md` | accepted; rows 1a and 1b built, 1c to 1g open | the nearest prior design of a provider contract: §3's one sheet per agent (`AgentCapabilities`), each row with its source (declared, negotiated, measured, absent) and its fallback, and what a plugin's manifest may and may never claim (§3.4); COAGENT1e builds the sheet and is open |
| COWORKVIEW1, `docs/2026-10-24-cowork-view-design.md` | proposed, round 9; row a settled alone | how each door and each harness announces, names and ends a native child, and how a process's token reaches the hook and the connector on each door (§1.1, §3.6) |
| SWITCHCTX1's carry, `docs/2026-10-24-switch-context-design.md` | proposed; re-cut onto FOUNDATION1's move record | where each agent keeps its own conversation under its account's home, by agent and version (its table of identities) |
| CONNECTORS1 (backlog row) | design owed | which connectors and servers a session's agent gets, from where, and how one is left out: the handing seam is one of the contract's parts |
| OPENROUTER1 (backlog row) | design owed | the first new provider: an account that is a key, its models offered by role, its limits read as limits, and the repositories closed to it |

Its ground is the adapter seam as it stands: `ISessionAdapter` with its twenty-one members and `HarnessToolchain` with its thirty fields
(survey §0), the plugin harness built as `DeclaredAcpAdapter` (D64), and the decisions each member cites.

## 4. What the contract must cover

The survey's groups, each a part of the contract a proposal places. Each names what differs today (the survey's section) and what a
provider must answer for it; a proposal may merge or split them, and says where each of today's sites goes.

| Part | What a provider answers | Survey |
|---|---|---|
| **Identity** | its id, the agent it runs as (today `AccountOf`/`Owner`), its product, maker and mark, the door it speaks, and the ids a record already holds for it | §1 |
| **Toolchain** | its binary and how its version is asked, its install, update and pin, its release channel or package, what a pinned copy runs with | §2 |
| **Accounts** | its accounts' kind (a sign-in home, an API key, a provider key), the variable that names a home, whether a home must exist, its sign-in and its login question, its trust record, and its usage, windows, limits, weekly reset and turn counts | §3 |
| **Models and efforts** | what it offers (declared, negotiated at the open, or read from a settings file), how a choice is carried on its door, how a refusal is read, and what model a turn reports | §4 |
| **The session's life** | how a session starts, resumes (on its wire or by its own resume), goes on, is steered, is stopped while keeping its conversation, and is handed over; and where it keeps its conversation | §5 |
| **The door** | which door it speaks and what that door supports, as a declared or negotiated capability, never as a branch on its name | §6 |
| **The turn** | how the target or the person's words reach it, framed for its door, and what a turn's end is | §7 |
| **The event stream** | one normalized stream the rest of Daoris reads: turns, tool calls, children and their ends, limits, errors, usage, the model reported and the ended reasons, with what it does not say kept unknown | §8 |
| **Connectors** | how Daoris's connector and the session's own identity reach it (a server entry, a file, the environment), how the repository's and the account's own servers are kept or left out | §9 |
| **Permissions and skills** | its postures in its own words (ordinary, asking, full), whether it takes Daoris's rules and how, the plugins and skills it is handed, the hooks it runs | §10 |
| **Process and environment** | what its spawn sets and drops in the environment, its working directory and encoding, and the containment the launcher applies (which stays Platform's) | §11 |
| **Errors** | its codes, kind words, statuses and recorded sentences for D210's four kinds, its limit and sign-in words, its refused credential | §12 |
| **Outside the driver** | what the page, the service, the hook and the tools know of an agent, and which of those facts become the driver's answer | the `Web/`, `Core/`, `Mcp/`, `Http/`, `Cli/` and `tools/` rows of every section; §13 |

## 5. The constraints a proposal keeps

Each is decided and stands, unless a proposal argues to amend it. A proposal that amends one says which and why, and the amendment is
appended to the decision as a dated note, never rewritten into it.

**The doctrine** (`AGENTS.md`).
- **Orchestration is central; ownership is local.** A provider changes nothing about who may write where: a session changes its own
  repository and asks another through a quest (D45).
- **Model-dependent capabilities say the tier that ran** (D24, `.claude/knowledge/model-decoupling.md`): a provider names an agent,
  never a model of Daoris's choosing (D23, D24); the model is the person's choice through the role's staffing (D205), and every start says
  the agent, the model asked and the model the agent reported.
- **Machine-local stays on the desktop**: an agent's process, its streams, its accounts and their homes, its keys. A shared host never
  learns a provider's account, home or path; a field that must not cross a sync has no field on the wire
  (`.claude/knowledge/session-record-fields.md`).
- **No machine path, private repository name or credential** in a tracked file, a record that travels, an event that leaves the machine,
  or a commit message. A provider's key is kept where D172 point 3 keeps keys (`profiles/`), handed only to that provider's own spawn,
  never in another session's environment (OPENROUTER1's row says the same).

**FOUNDATION1's map and its accepted Part B** (D212).
- **The contract lives in Agents** (§14.2 row 9), which names only its own types and those of the subdomains below it (Platform, Store,
  Settings, Service, Install, Instance, Events, Knowledge). What a higher subdomain needs of a provider it reads through the contract;
  what the contract needs of a higher one (the instruction composed by Context, the words kept by Course, a role's choice from Roles) it
  takes as a value or a port, never by naming the type. The structure census already names Agents' upward edges today, family by family
  (`Driver.Tests/fixtures/structure.json`, *Agents names Context*, *Course*, *Help*, *Participants*, *Plugins*, *Review*, *Roles*, *Work
  orders*, *Workplace*) and the lower subdomains' edges into Agents (*Service*, *Install*, *Events*, *Knowledge names Agents*); a proposal
  adds none and says which of them it cuts.
- **Part B's contracts stand**: the participant's `Binding` is the contract's identity at a start (§2.1); every start goes through the start
  plan's settled selection (§2.4), so a provider never walks accounts or choices itself; admission is the four layers keyed by capability,
  and containment is the launcher's, unchanged (§2.3, D201); the events are FOUNDATION1's envelope, minted by the hub of the process that
  runs the work or by the store's trigger, never a channel of the provider's own (§4.1); the binding, the offers and the wiring move to the
  desktop store by Part B's rows (§13.3: `agent_offers`, `harness_wiring`, `bindings`).
- **Loop holds no rules; `Domain/` stays pure** (§14.3): a provider's judgement (a door's capability, a reading of its wire) is a pure
  function in Agents, executed by the loops.

**D191's two makers' reads** (§2 above): both Codex and the independent Opus reader read every round; no part that widens an agent's
authority or deletes is accepted on one read.

**The adapter seam's own rules**, which the contract keeps unless a proposal amends them:
- **An agent arrives deliberately** (D23): an unknown id is an error naming what exists, never a fallback to another agent.
- **Silence preserves** (D53, the seam's defaults): a capability a provider does not declare reads as today's behaviour for an agent that
  says nothing, never as support. What a harness does not report stays unknown (D173's guard).
- **Every claim about another program is measured or read in its source**, with its version and its evidence (D53, D57, D125 point 1.4):
  a provider's postures, flags, limit words, error words and file layout are each a recorded fact, never a guess.
- **Postures in the agent's own vocabulary**; a full or asking start on a provider with none mapped is refused naming it, never run in a
  narrower mode under the full label (D183, D184).
- **The agent's own words are never read** for a limit, an error or a sign-in (D125 point 1.4, D210 point 1): only the door's failure.
- **A model is carried at the session level**, never by writing the agent's settings file or its environment (D205 point 3).
- **Every agent Daoris starts is contained through the launcher** (D201); a provider supplies its process, never its containment.

**Words, records and builds.**
- **Translation parity** (`.claude/knowledge/translation-parity.md`, D116): the page's chrome in English and Chinese within its kind's
  budget; an agent's product name is the maker's, kept as written; a new term enters through the glossary
  (`src/Daoris.Web/src/locales/glossary.json`) with its reasons. The glossary already says *agent* (智能体) on the window and *harness* and
  *profile* in code alone, and *way in* (接入方式) for the door, *directly* (直连) or *over the protocol* (协议); a proposal that names
  *provider* says whether it reaches the window and in which words.
- **Compatible builds** (`.claude/knowledge/compatible-builds.md`): every record that names an agent or a door today (the survey's
  §1 and §14 list them) is read by builds already shipped. An id they hold keeps resolving; a new meaning goes where those builds cannot
  see it; a twin changes with its twin (`.claude/knowledge/twins.md`).
- **The install's live home**: the installed build drives real sessions on the owner's machine with the protocol doors of Claude Code and
  Codex. A row changes no record that install holds in a way its own build cannot read, and nothing in this work reads, writes or signs in
  to that home: development never sets or uses `DAORIS_HOME`, and an installed proof is the parent's, on the install, after the row is
  staged.
- **Simple** (D193's rejection): a person who sets nothing sees Daoris work as today. The contract is a structure for the code, not a new
  screen or vocabulary on the page, beyond what OPENROUTER1's own design adds.

## 6. Out of scope

- **New agents beyond the proof.** The contract makes a new provider an addition; this design adds only the stand-in provider of its proof
  (§8) and leaves OpenRouter's account kind, model offers and tier rule to OPENROUTER1's own design, built on the contract.
- **Changing what any agent does today.** A move behind the contract changes no posture, carrier, limit rule, error kind or resume path; a
  disagreement the survey finds (two places deciding one fact differently) is settled by a decision note, never silently inside a move
  (FOUNDATION1's brief §7 point 3).
- **The choice of agent and model** (D205's actors, FOUNDATION1's role catalogue and start plan): the contract supplies a provider's offers
  and carriers; which role takes which choice stays Roles'.
- **The protocol door's implementation as a protocol**: the Agent Client Protocol's session, prompt, update and permission frames stay
  one shared transport; only what in it belongs to one agent (a `_meta` vocabulary, an agent's own update kind) is in scope.
- **The event journal, the move record and the store** (FOUNDATION1 Part B): the contract feeds them; it designs none of them.
- **CONNECTORS1's page and choices**: which connectors a person allows is CONNECTORS1's; the seam that hands a connector to a provider is
  this design's.
- **The service's connector tools and their admission** (`.claude/knowledge/connector-tools.md`, D212's call layer): unchanged, but for
  how a provider's child or call is placed.
- **The CLI's doctrine layouts** (`src/Daoris.Cli/src/layout/harness.ts`, D117): where an agent reads its instructions and skills on disk
  is the doctrine's, which stays offline with no runtime dependency; a proposal may say how the two ids relate, and changes neither.
- **The account pool's page and the sign-in's flow** (D197): kept; the contract carries each provider's sign-in, never a new one.

## 7. The questions every proposal answers

A proposal answers each, citing the survey's section and the code's line for every fact it rests on, and says where it amends a decision.

**The shape.**
1. **The contract.** One interface, or a small set of capabilities each provider implements (identity, toolchain, accounts, models, door,
   stream, connectors, permissions, errors); which members are declared, which negotiated at the open, which measured (COAGENT1 §3.1's
   sources), and what each answers where a provider says nothing. How COAGENT1e's sheet (`AgentCapabilities`) and the contract relate: one
   thing, or the sheet a read of the contract.
2. **Provider, agent and door.** Is a provider an agent (Claude Code) with doors (pipe, protocol), or each agent-and-door pair as today's
   adapters are (`claude-code`, `claude-code-acp`)? How today's `AccountOf`/`Owner`, the `codex` holder that has accounts and no door, and
   tables declared on one door for another agent (survey §1, §3) fold in; which id a record keeps, and how every id a shipped record holds
   still resolves.
3. **The door.** Whether the protocol door is one transport every protocol provider shares, its per-agent vocabulary handed in by the
   provider, and the pipe door one provider's own transport, or both doors are transports a provider picks; where the pipe door's stream
   reader (`ClaudeStreams`, `StructuredOutput`) lives; and how each door-specific path the survey lists (steering, a stop that keeps the
   conversation, background terminals, held permissions, the native resume's retry) becomes a capability rather than a test of `Wire`.
4. **Plugins.** How a plugin-declared harness (D64, `DeclaredAcpAdapter`) implements the contract by declaration, which members a manifest
   may declare, and which it may never claim (COAGENT1 §3.4: rules, limit words, lineage, confinement, steering, resume).

**What a provider answers.**
5. **Accounts.** One provider entry for its accounts' kind, home, sign-in, login question, trust, key, usage question, windows, limits,
   weekly reset and turn meaning, or today's `HarnessToolchain` fields kept and split; an account that is a key handed to a provider by its
   own variables (OPENROUTER1's case); what a door onto another agent's accounts reads as its owner's, and what it never inherits
   (D210's `AuthRequiredCode`).
6. **Models.** Where a provider's offers come from, how a choice is carried on its door, how a refusal and a reported model are read, and
   how D205's judge and FOUNDATION1's start plan read them without naming a provider.
7. **The session's life.** Which of start, resume, go-on, steer, stop, hand-over and the carry of a conversation's files are the
   provider's and which stay Course's (FOUNDATION1 §3); where a provider keeps its conversation (SWITCHCTX1's table) and whether that is
   the contract's.
8. **The stream.** The one normalized event stream: its vocabulary (a turn's start and end, a tool call, a child's announcement and end, a
   limit, an error, usage, the model reported, an ended reason), how it maps onto FOUNDATION1's `EventCodes` and today's `SessionEvent`
   kinds, what each provider leaves unknown (a Codex child's end), and where today's readers of one harness's wire (survey §8) move.
9. **Connectors.** How Daoris's connector and the session's identity reach every provider by one seam (today an environment variable on
   one agent and a server entry on another, survey §9), and how the repository's and the account's own servers are handed, kept or left
   out, so CONNECTORS1's choices have one place to act.
10. **Permissions, skills and hooks.** A provider's postures; whether it takes Daoris's rules, its skills and its hooks, by which flag or
    `_meta`; what the page and Ask Daoris's room say where it takes none (today Codex's case); and how the lineage hook, which is Claude
    Code's, generalizes or stays a provider's own.
11. **Process and environment.** What a provider sets and drops in its spawn's environment (survey §11), how the role variables
    (`Spawning.Roles`, ENVLEAK1) and the target's variables stay Daoris's and never a provider's, and how the prompt's composition
    (`TargetPrompt`, today inside the adapter's spawn) leaves the provider for Context.
12. **Errors.** One provider table for D210's kinds, its limit and sign-in words and its refused credential, or today's separate fields;
    the door string on a failure (`DoorFailure.Door`); a provider with no table (Codex's today, whose transient errors read fatal); and the
    two channels a failure travels in today, by which a pipe door's limit cools no account (survey §12).

**Across the contract.**
13. **The map.** Where the contract and each provider sit (one folder per provider inside Agents, or another cut), and how each named edge
    of the structure census (§5 above) that touches an agent is cut: Install's reading of a provider's install, Knowledge's and Events' readings of agent types, Participants'
    lineage fold reading one harness's child vocabulary, and the adapters naming Context, Course, Roles, Plugins and Work orders.
14. **Records.** Every record that names an agent or a door (the survey's §1 and §14): which keep their strings, which gain a field, which
    move to the desktop store, and how an older build reads each.
15. **The page, the service and the tools.** Which agent facts the page draws from the driver's answer rather than its own table (D182
    point 6), which twins remain and how each is held, and what the service and the hook know of an agent.
16. **The disagreements.** For each fact the survey finds decided in two places with two answers, which answer stands, with its reason:
    the machine's door (five places, four rules, survey §6); *a pipe agent cannot drive an unadopted repository* (five places, §9); *it
    resumes its own conversation* (§5); a stand-in told apart by its binary or by its name, and the stand-ins borrowing Claude Code's
    tables (§1, §2); the efforts offered (§4); the read floor and the token weights applied to every agent (§3); the Claude Code seam set
    on every protocol door's spawn (§11); and the declared fields nothing reads (`ProfileMustExist`; `WeekFixed` outside the modules, §3).
17. **The names.** *Provider*, *agent*, *harness*, *adapter* and *door* in code and on the window, in English and Chinese against the
    glossary; the word *pipe*, which names both the native door and the terminal's named pipe (survey §1); which code names change and
    which only in prose.
18. **The order.** The steps after mod-ix, each a row (§10), the first small enough to land in a day and prove the shape, the stand-in
    provider early, OPENROUTER1 after; how each input of §3 folds in.
19. **What is the owner's.** Every point that needs D191 point 3's answer.
20. **Proof.** Per step: the unit, census and Process cases, the rehearsals, and what is measured on the install.

## 8. The proof the design names

The design names, at least:

- **A stand-in provider added through the contract alone.** A third stand-in, beside today's `stub` and `acp-stub`, added by its own folder
  and one registration, no other file changed, which starts, takes turns, resumes, reports a limit read as a limit, an error read by its
  kind, a child announced and ended, its usage and its model, and is listed, signed in and pinned as any agent is, through the same suites
  the stand-ins pass today (the family rehearsal's phases among them). The diff is its proof: a change outside its folder is a gap in the
  contract, named.
- **The census.** A test that no file outside the providers names an agent's id (`"claude-code"`, `"codex-acp"`, …), a provider's type
  (`ClaudeCodeAdapter`, `ClaudeStreams`, `CodexUsage`, …), an agent's environment variable or file, or a door (`SessionWire`, a `"pipe"`
  or `"acp"` or `"protocol"` string for the door), across the driver, the modules and the service, with today's sites (the survey's
  list) as its named exceptions, each with the row that moves it, failing on a new one; the page's and the hook's sites held the same way
  in their own suites, or named as twins.
- **OPENROUTER1 as the first new provider**: its design written on the contract, its first rows a provider of its own, with the proof its
  row names (a stand-in provider's account, its limit read as a limit, a closed repository refused), and no file outside its provider
  changed but those its own design names.
- **A conformance suite** every provider passes, the stand-ins with no model in it, run per provider, so a provider that answers the
  contract wrongly fails there rather than in a caller.
- **The install**, after the rows that move Claude Code's and Codex's protocol doors: a session on each reads, resumes, cools and ends
  as before, its records readable by the build before.

## 9. What must not regress

Every step keeps today's suites green, as `daoris.gates.json` declares them (FOUNDATION1's brief §6 lists the gates), and adds its own.
The suites that hold what an agent is today, which a step touching that part runs whole, not by sample:

- **The doors**: `AcpTests`, `AcpBackgroundTests`, `AcpSteerTests`, `AcpTransientTests`, `ChatTurnsTests`, `ChatGoOnTests`,
  `ChatGoesOnProcessTests`, `ChatTransientProcessTests`, `ContinuationTests`, `HandOverTests`, `LimitWaitsProcessTests`, `AcpResumeTests`,
  and the adapters' and the pipe door's readers: `AdapterTests`, `Acp3AdapterTests`, `ClaudeAcpTests`, `ClaudeStreamJsonTests`,
  `NativeStreamsTests`, `DshProfileTests`.
- **Accounts and the toolchain**: `HarnessTests`, `AccountRotationGoalTests`, `AccountRotationWalkTests`, `AccountRotationTickTests`,
  `ResumeAccountTests`, `ToolInstallTests`, `ToolResourcesTests`, `AgentsCommandTests`, the usage and limit readers' tests.
- **Errors, permissions and children**: `AgentErrorsTests`, `PermissionRulesTests`, `PermissionSpawnTests`, `PermissionSeamTests`,
  `AgentsTextTests`, `ChildLineageTests`, `LineageHookProcessTests`, `EveryChildIsHandedTheToolsTests`, the service's `McpToolsTests` and
  `CallLineageTests`.
- **The rehearsals**: the family rehearsal (its stub and protocol-stub phases) and the deployment rehearsal.

And the behaviours the install relies on, each with a case before the step that could break it: a session on each provider starts, takes
a turn, resumes its own conversation and ends as today; a limit cools the account it ran as and never strikes; a transient error is tried
again in the same conversation; a refused model passes to the next choice before any prompt; a full or asking start on a provider with no
such posture is refused; a record an older build holds still reads.

## 10. How the work lands

**Never a big-bang rewrite.** The contract is reached in steps, each its own row and branch, each landing with every gate of §9 green, each
leaving Daoris working as it did for a person who sets nothing, and none before row mod-ix has moved the Agents subdomain into its folder.
FOUNDATION1's brief §7 sets the shape, which this design keeps:

1. **Name the contract, then its census.** The first step introduces the contract beside today's seam, with the census of §8 listing every
   site as not yet moved, and moves no reader.
2. **Move one provider, or one part, at a time.** Each step moves one part of the contract (§4) or one provider behind it, today's surface
   its adapter, the census shrinking by the sites it moved and failing on a site that neither moved nor is named.
3. **A disagreement found is a decision, not a refactor.** Where the survey shows two places deciding one agent fact differently, the step
   that unifies them states which answer stands, with its reason, as a dated note on the decision it amends.
4. **Records last, and compatibly.** A record's agent or door field changes only after every reader reads it through the contract, in a
   shape older builds cannot misread.
5. **The stand-in provider early**, as soon as the contract can carry it, so every later step is proven on it; OPENROUTER1 after the
   parts it needs have moved.

A proposal names its first three rows concretely, with their proofs, and the rest in order.
