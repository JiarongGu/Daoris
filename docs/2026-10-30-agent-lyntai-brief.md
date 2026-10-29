# Daoris's providers on Lyntai's agent-provider layer — the brief

> AGENTLYNTAI1, phase 1 of a foundational co-design under D191 (2026-10-30). This brief is the one document every proposal is written
> from. It holds the direction, the process, the inputs, what the design must place, the constraints a proposal keeps, what is out of
> scope, the questions every proposal answers, the proof the design must name, what must not regress, and how the work lands. The
> facts of today, in both repositories, are in `docs/2026-10-30-agent-lyntai-survey.md`, by `file:line`, and are not repeated here: a
> proposal cites the survey's section and the code's line, never this brief, for a fact. The proposals will be one file per maker,
> `docs/2026-10-30-agent-lyntai-proposal-<maker>.md`, each written from this brief and the survey alone and sealed before its author
> reads another; a synthesis follows in a later quest. `Dnn` is `docs/decisions/Dnn.md`; *Lyntai's Dnn* is Lyntai's `docs/DECISIONS.md`.
> Paths follow the survey's aliases.

## 1. The direction

**Decided (the owner's direction, D213's note of 2026-10-29):** Daoris's provider layer is Lyntai's agent-provider layer, consumed as a
package. What an agent is (its contract, its one event stream, its accounts as a pool, its failure policy, its doors, its stand-in) is
built once, in the library, where any host can reuse it; Daoris keeps what is a host's (orchestration, review, the person's doors, the
driver's executors, what the person sees). AGENTPROVIDER1's rows a and b stand as the seam and the meter; rows c onward wait for this
design, which says, row by row, which become Lyntai's (built there from a quest that states the general need, never by an edit from
here) and which stay Daoris's; how `Agents/Contract` maps onto the layer's types; how the desktop builds against layer changes not yet
released; and the order. For what stays Daoris's, the accepted design stands. The note rejects building the rows here and moving them
later, a copy of the layer's types inside Daoris, and a dependency taken before this design.

The steward's reading, which the survey tests against both codebases: of the 57 sites Daoris's provider layer is built of, Lyntai's
layer answers 5 today, answers part of 21 and has nothing for 31 (survey §0). It is ahead where Daoris has only designed (the
restriction facts, a delivery's disposition, the pool and its reducer, the retry budget, the hand-over brief, the ability claims); it
lacks what Daoris built against measured streams (a live protocol run, a subagent's lines and background tasks, a held permission
request, a model carried on the protocol door); its account belongs to one provider where Daoris's key spans a provider's doors; and none
of it is released (survey §8, §11).

**The part of the company it makes work** (D193, `docs/2026-10-21-company-concept.md` §2): the equipment and vendors (part 14), since an
agent is a vendor's product and the layer becomes the one contract any of them is taken in by; and the departments (part 3), since the
layer is another department's work, which Daoris asks for and never edits.

## 2. The process

D191 governs it, with its amendments of 2026-10-20 and 2026-10-23 and its note of 2026-10-24. This design changes a foundation in the
senses the 2026-10-20 amendment names: the seam every agent's process is started, spoken to and read through moves into another
repository's package, and decided rules other designs rest on (D213, D22, D212's map) are re-cut. So:

1. **Two sealed proposals**, each from this brief and the survey alone, Codex's and the independent Opus reader's, as AGENTPROVIDER1's
   were (D213). Neither author reads the other's before its own is sealed.
2. **One synthesis**, written by Claude Opus, as a proposed amendment to D213 and a design document, comparing the proposals question by
   question (§7) and saying what each choice rejects.
3. **Both makers read it.** Each finding names the file and line it rests on, in either repository; the steward checks each against the
   code at the commits the design names (Daoris's and Lyntai's) before it is taken.
4. **Rounds until both makers' blocking findings are taken.** A part that widens or narrows what an agent may do or what reaches it (a
   posture, a rule, a credential, a server, an account it runs as) is ⚖ and is not accepted on one maker's read (D191's note of
   2026-10-24; D213 point 13).
5. **Rows**, each its own branch in its own repository: Daoris's rows here, each behind today's tests (§9); Lyntai's parts as quests to
   Lyntai, built and released there by Lyntai's own process.

**What the owner is still asked** (D191 point 3): a change of the product's direction (a hosted agent, which Lyntai's D233 keeps out, is
one); anything that spends the owner's accounts or money beyond the development's own runs (an installed proof on a real account, an
OpenRouter key's first measurement); a sign-in; anything that publishes, pushes or rewrites history (**every Lyntai release, and any
package published before one, is a publication**); anything that loses the owner's data. AG20's ruling is the owner's in Lyntai
(survey §13). A proposal that needs one of these says so, in a list of its own, each question saying what it is, what a yes does and
where to look.

## 3. The inputs

Each is decided, built or in flight, and an input to this design rather than a competitor of it: a proposal says, for each, whether it
stands as it is, moves into the layer by a request, or is re-cut.

| Input | Standing | What it brings |
|---|---|---|
| AGENTPROVIDER1, D213, `docs/2026-10-26-agent-provider-design.md` | accepted 2026-10-26; rows a and b built on the batch line, not yet on main; rows c onward waiting for this design | the contract, the registry, the census and the conformance suite (survey §1 to §7); rows c to u, the corrections s-i to s-ix on and t (§11); OPENROUTER1 as the first new provider (§12); every constraint of its points 1 to 14 for what stays Daoris's |
| AGENTINFRA1, `docs/2026-10-27-provider-infrastructure-design.md` | accepted amendment to D213 by a scoped owner exception (its §11); nothing built | providers across local programs, remote APIs and hosted runtimes; the execution-neutral run; scoped availability restrictions and their reducer; durable request disposition; the isolated home first, shared-home activation unavailable; stages A to E (§8) |
| FOUNDATION1, D212, `docs/2026-10-25-foundation-design.md` | Part A complete (mod-xxi); Part B accepted, rows g, j, q and r unbuilt | the module map (§14), the start plan and its one walk (§2.4, row j), failures read once (row g), start admission by the work (row q ⚖), the connector by work (row r ⚖), the desktop store (§13) |
| Lyntai's layer, its D219, `Lyntai/docs/agent-provider-layer.md` | rows AG1 to AG9 built, unreleased | the descriptor and registry, the turn's event stream, the failure with its restriction and delivery, the pool and its reducer, the retry, the transcript and brief, the generic ACP door, the usage reading (survey §1 to §8) |
| Lyntai's study of the accepted design, `Lyntai/docs/agent-provider-daoris-study.md`, with its D233 and its rows AG10 to AG20 (`Lyntai/TASKS.md` Part 412) | written 2026-10-29; AG10 to AG19 startable, AG20 decision-only; nothing built | Lyntai's own reading of every row: what the layer answers, what it judges the host's, and the general gaps it filed (survey §9) |
| Lyntai's D20, D70, D18, D19, D25 with D221, D222, D225, D226, D227, D228 | decided there | what Lyntai owns and refuses (credentials and binaries), its frozen surface, its release authorship, its packages, its ACP and pool rulings (survey §11) |
| D22 | decided here | the family's libraries at released versions, never a working tree; a capability, never a role (survey §11) |
| OPENROUTER1, CONNECTORS1 (backlog rows); COAGENT1e (the sheet, D213 row r) | designs owed | the first new provider; the connectors a session gets; the capability sheet |
| Lyntai's two answers owed (survey §13) | placeholders | AG20's ruling; how a host consumes the layer before a release |

## 4. What the design must place

The survey's groups, each a part a proposal places. A proposal may merge or split them, and says where each of today's sites goes.

| Part | What a proposal places | Survey |
|---|---|---|
| **The contract's records and ids** | `Provider`, `AgentDoor`, the typed ids, `Known<T>`, `AccountPool`, `AgentRun`: each the layer's type, a Daoris record over it, or kept beside | §1 |
| **The parts** | identity, program, accounts, tables, usage, lent, home, postures, abilities, reach, vocabulary: which the layer states, which Daoris keeps, which a quest asks for | §2 |
| **The ports** | the spawn, the two dialects, the usage question, the child vocabulary, the conversation home | §3 |
| **The registry and the registration list** | today's registry over the adapters, the one list, the stand-in flag, the records' ids | §4 |
| **The stand-ins** | Daoris's `stub` and `acp-stub`, row c's third stand-in, the layer's own stubs | §5 |
| **The meter** | the conformance suite's 60 cases and its views; the census's four twins | §6, §7 |
| **The doors that run** | the protocol door's live run and the native door's conversation against the layer's turn | §8 |
| **Accounts, holds and the walk** | the account rule, the cool-off and its file, the start plan's walk against the pool and its reducer | §8 |
| **Failures, readings, models, the hand-over** | the error reader and its tables, the event vocabulary, the offers and the carried model, the brief | §8 |
| **The spawn and containment** | Daoris's variables, the launcher (D201), the runner seam | §8 |
| **The package** | the reference, its version, the build before a release, the fallback without a feed | §11, §12 |

## 5. The constraints a proposal keeps

Each is decided and stands, unless a proposal argues to amend it. A proposal that amends one says which and why; an amendment to a
Daoris decision is appended to it as a dated note, and one Lyntai would make is a request to Lyntai, with its evidence, never written
here as Lyntai's.

**The doctrine** (`AGENTS.md`).
- **Orchestration is central; ownership is local.** A session changes its own repository and asks another through a quest (D45). A
  change Daoris needs in the layer is published as a quest to Lyntai that says what is needed and why, the general need with the asker
  struck, and is built there; nothing is written into Lyntai's files, its backlog included (the `repository-owns-its-work` rule).
- **Local operation does not require a remote.** A person who runs Daoris on one machine with no network keeps working; a build that
  needs a feed says what it needs and when (survey §12).
- **Model-dependent capabilities state the tier that actually ran** (D24): a provider names an agent, never a model of Daoris's
  choosing; the model asked, carried and reported stay apart (D205).
- **Machine-local stays on the desktop**: an agent's process, its streams, its accounts and their homes, its keys. A shared host never
  learns a provider's account, home or path.
- **No machine path, private repository name or credential** in a tracked file, a record that travels, an event that leaves the
  machine or a commit message. A key is kept where D172 point 3 keeps keys and handed only to its own provider's spawn.
- **Versions and publication.** Versions and release headings are the release workflow's; push and publication are the owner's call,
  in Daoris and in Lyntai (its D19).

**The family's libraries** (D22). Daoris consumes a sibling's capability as a library at a released version, never a sibling's working
tree, and never borrows its role; an awkward seam is a finding for that sibling, not a workaround here. A proposal that builds against
an unreleased layer says how that stands with D22, or amends it.

**The accepted provider design and its note** (D213). Rows a and b stand. For each row that stays Daoris's, its accepted text stands:
a provider is an agent with doors; every record keeps its strings and every id a shipped record holds keeps resolving by its kind
(`RecordIds`, survey §4); a move changes nothing and a disagreement is a correction of its own; the census and the conformance suite
measure each step; the ⚖ rows need both makers' reads; an installed proof that spends names its account and its bound.

**AGENTINFRA1.** Shared-home remote activation stays unavailable; new restriction semantics and remote execution run first in an
isolated home; an old record keeps its old meaning and `cooling.json` is never overloaded with a new one (its §7).

**FOUNDATION1's map and Part B** (D212). Agents names only its own types and those of the subdomains below it; what a higher subdomain
needs of a provider it reads through the contract, and what the contract needs of a higher one it takes as a value or a port. Loop
holds no rules; `Domain/` stays pure. Every start goes through the start plan's settled selection, so a provider never walks accounts
or choices itself; containment is the launcher's (D201). The structure census holds the map at every row (`StructureTests`).

**The receiver's rules.** Lyntai's decisions are Lyntai's, and a proposal designs within them or asks: it never stores a credential or
chooses a binary's version or place (its D20); its published surface is frozen under the full promise, a disclosed break allowed in a
minor only while every consumer is first-party (its D70, D18); its version is its release pipeline's (its D19); a package is earned by a
dependency it isolates or its promotion gate (its D25, D221); a value only a deployment can know is the host's option, never a type's
property (its generic-library rule 7); and its rulings on the ACP door, the pool and the brief (its D225, D227, D228, D233) stand until
it reverses them.

**Words, records and builds.**
- **Translation parity** (`.claude/knowledge/translation-parity.md`): the window keeps *agent* (智能体) and *way in* (接入方式), the
  product names as written; *provider* stays off the window (D213 point 11); a new term enters through the glossary.
- **Compatible builds** (`.claude/knowledge/compatible-builds.md`): a stored shape is never changed in place; a field or a code is added
  and the old one read as it was. The cool-off's file, the offers, the bindings and every record of `RecordIds` are read by builds
  already shipped; a twin changes with its twin (`.claude/knowledge/twins.md`).
- **The install's live home**: nothing in this work reads, writes or signs in to it; development never sets `DAORIS_HOME`; an installed
  proof is the parent's, after the row is staged.
- **Simple** (D193): a person who sets nothing sees Daoris work as today at every landing.

## 6. Out of scope

- **Building any part of the layer here**, or writing into Lyntai: the proposals name the general need each Lyntai part answers, and
  leave its types and its tests to Lyntai's own design.
- **New agents** beyond placing OPENROUTER1, whose own design follows.
- **Shared-home remote activation** (AGENTINFRA1 §7) and a hosted agent (Lyntai's D233).
- **Changing what any agent does today**: a move behind the layer changes no posture, carrier, limit rule, error kind or resume path; a
  difference the move finds is a correction row of its own.
- **The choice of agent, model and account** (D205, D212 §2.4): Roles' and the person's, as today.
- **The service's own use of Lyntai's packages for its vectors** (D22; `Service/Directory.Packages.props:6-24`), but where one version
  must serve both trees.
- **The CLI's doctrine layouts** (`src/Daoris.Cli/src/layout/harness.ts`, D117), which stay offline with no runtime dependency.

## 7. The questions every proposal answers

A proposal answers each, citing the survey's section and the code's line in either repository for every fact it rests on, and says
where it amends a decision or asks Lyntai to.

**The split.**
1. **Row by row.** For each row of the provider design's §11 (c to u, the corrections s-i to s-ix on, and t) and for OPENROUTER1 (§12):
   the layer's, Daoris's, or split, and which part each. Why, by the generic-library test (the sentence with the asker struck, and
   *could two honest applications answer this differently?*), citing the survey's §9. Where it departs from Lyntai's study's verdict,
   why, with the evidence. For each part that is the layer's, the general need its quest states and the AG row that answers it, or the
   new one it asks for; for each, what becomes of the row's census sites (deleted from Daoris, moved into a provider's folder, or kept).
2. **The host's line.** For each fact the two records place differently (survey §8, §10): the floor of a usage read, a default cool-off
   for an unstated reset (D125's sixty minutes against Lyntai's D227), the variables stripped from a spawn (s-vi), interactive, reach,
   the rule language, the release sources (row f against Lyntai's D20): which side holds it, and why.

**The mapping.**
3. **`Agents/Contract` onto the layer.** For each of the contract's 34 types (survey §1 to §3), the layer's type it becomes, wraps or
   stays beside. Whether Daoris's records become adapters over the layer's types (a Daoris provider an `IAgentProvider` with Daoris's own
   parts beside it) or are replaced by them (the layer's descriptor the contract, Daoris's parts requested of Lyntai or dropped); and how
   the choice is not the copy of the layer's types that D213's note rejects.
4. **Provider, door and account.** D213 point 1 (a provider with doors, one account key across them, a key resolving to a pool of
   providers) against Lyntai's D225 (an ACP agent its own provider, an account of exactly one). How each of Daoris's doors (`claude-code`,
   `claude-code-acp`, `codex-acp`, the `codex` holder, `dsh`, `stub`, `acp-stub`, a plugin's harness with its `accountOf` one level) maps
   onto the layer's providers and doors; how every id a shipped record holds still resolves (`RecordIds`' 22 fields); case-blind against
   ordinal ids, a duplicate refused against replaced; and what the design does under each of AG20's two answers, since it is unruled.
5. **What is known.** `Known<T>`, any fact with its source, evidence and eight-fact applicability, stated absent as today's behaviour,
   against `AgentAbility`, one of eleven names with one version, unknown read as not promised. Where a fact the layer's descriptor cannot
   state lives, and how a run is still admitted by its own evidence (D213 point 2).
6. **The run.** Daoris's live protocol run (steering, queued words, a held permission request, background tasks, a carried model and
   posture, a stop that keeps the conversation) and its native conversation with stdin open, against the layer's one turn per call and
   one process per turn (survey §8). Which of Daoris's doors run on the layer's doors and which keep Daoris's transports for now; whether
   Lyntai's D233 option of a long-lived run is asked for, and with what measurement as its trigger; and how a session on each door still
   starts, takes a turn, resumes its own conversation, cools and ends as today at every step.
7. **Accounts, holds and the walk.** Daoris's account rule, the roster's walk and `cooling.json` keyed by owner and profile, on a home
   with several processes, against the layer's pool, its holds keyed by provider and account name in its own store, one process, an
   unstated recovery held until cleared (survey §8). Whether Daoris takes the pool, the reducer alone, only the restriction vocabulary, or
   none; how D197, D205, D206 and D125 hold; how a cooling entry an older build wrote is read; and how AGENTINFRA1's restriction (its
   subject key, an owner action's reason, its revision) maps onto the layer's.
8. **Failures and words.** `AgentErrors.Read`, D210's precedence and each provider's and door's declared tables against `AgentFailure`, the
   one classifier and each door's internal reader. Whose tables a recorded sentence lands in, how one Daoris has recorded reaches the
   layer (a request with the recorded sentence), and how the four kinds stay one reading on every path meanwhile.
9. **Readings.** Daoris's `SessionEvent` kinds and D213 §3's readings against the layer's `AgentStreamEvent`: what Events reads from the
   layer, how FOUNDATION1's hubs and triggers still mint from what the sink keeps, and what of the readings the layer lacks (a child, a
   task, a held request, a refused model, provenance, a replay) waits for AG10, AG18, AG19 and AG12 and what Daoris keeps reading until
   they land.
10. **The spawn and containment.** How a door of the layer's starts through Platform's launcher (D201), its runner seam (Lyntai's D226)
    included; how Daoris's own variables, the session's tokens, the git wiring, the tools' `PATH` and the carriers reach it in today's
    order (D213 point 3); and how s-vi's strip reads the variables a provider declares (AG17).
11. **Models, postures, rules, skills, connectors and plugins.** D205's offers per door, version and account and the model carried on
    both doors (AG11, AG12); postures in the agent's own words against the layer's read-only or write intent; D72's rule grammar and the
    lineage hook; `ConnectorPlan` and CONNECTORS1; a plugin's declared door against the layer's generic ACP registration. Each ⚖ row's
    placing, and what it widens or narrows.
12. **The program.** Row f's release sources, pins and signed downloads against Lyntai's D20, and where Daoris's pin of each agent's
    program lives.

**The measure.**
13. **The census at each step.** How it counts when a site leaves Daoris for the layer; what a Daoris file may name of the layer (its
    types, its provider ids, the vendor variables a registration passes) and where; whether the layer's types are a token the census
    confines to the providers' folders or to Agents; and how `StructureTests` and the map treat an external package's types.
14. **The conformance suite at each step.** Which view runs over the layer's registry; how each of the 60 cases is answered by a provider
    on the layer's types, and which are held pending with the Daoris row or the Lyntai request that makes them pass; which rules the
    layer's own theory holds (and AG16 would) and which Daoris's suite keeps; and which stand-in proves the shape (row c's third
    stand-in here, the layer's ACP stand-in, or both). The answer shows, by the suite and the census, that Daoris works as today at
    every landing, rather than claiming it.

**The package.**
15. **The reference.** Which package (`Lyntai`, a metapackage that also brings storage and MCP, or `Lyntai.Core` and
    `Lyntai.Providers.Basic`), which project takes it (the driver alone, and within it the Agents subdomain alone), the layer's DI
    registration or its types constructed, its pin in `Desktop/Directory.Packages.props`, whether it is kept equal to the service's pin,
    and its transitive versions against the desktop's Shenora and SQLite pins, measured before the first row.
16. **Versioning.** The version each Daoris row needs; how a release is taken (D22's notes read a sibling's changelog at its tag for
    every version skipped); how a disclosed break in a minor (Lyntai's D18) is met; and how the layer's freeze at its first release
    (Lyntai's D70, D225, D227, D228) and its rule that the certain parts ship first meet the order of Daoris's rows.
17. **Building against changes not yet released.** D22 says released versions, never a working tree. Either each Daoris row waits for
    the release that carries its need, or the proposal amends D22 for this (a packed build on a local feed, a checkout, a source copy),
    saying what the merge tool's gates and the CI beside build on a machine with no Lyntai checkout, and what a Daoris branch may hold
    while a Lyntai row is merged and unreleased. Lyntai's answer to the quest (survey §13) is read when it lands; a proposal says what it
    does under each answer it can foresee.
18. **No feed reachable.** What a build needs when no feed answers: the machine's packages folder, anything kept in the repository, a
    clean machine's first build; and that the installed app restores nothing at run time.

**The order.**
19. **What moves when.** Each Daoris row and each Lyntai request in order, the Lyntai release each Daoris row waits for, the first three
    rows concrete and the rest in order; OPENROUTER1 placed (its executor named: Claude Code's program at a gateway, or the layer's HTTP
    door on Lyntai's D233, which reads an endpoint as an agent only with the library's own tool loop as its executor and no workspace),
    and AGENTINFRA1's stages A to E placed against the layer's restrictions and its absence of a hosted agent.
20. **FOUNDATION1's Part B rows g, j, q and r.** What each touches of the provider layer (g, one failure reading on every path; j, the
    start plan and its one walk, against the layer's pool picking a seat per turn; q ⚖, start admission by the work; r ⚖, the connector by
    work and the session's identity it reads), whether each reads the layer's types or Daoris's, and how each sequences with the provider
    rows and with Part B's own order.

**Across.**
21. **The names.** *Provider*, *door*, *agent*, *account*, *pool*, *hold* and *run* in Daoris's code and prose against the layer's
    words for the same and for different things; what the window and the glossary keep.
22. **What is the owner's.** Every point that needs D191 point 3's answer, each a Lyntai release among them.
23. **Proof.** Per step: the unit, census and Process cases, the conformance suite's lines, the rehearsals, what a person sees, and what
    is measured on the install and what it spends.

## 8. The proof the design names

The design names, at least:

- **The meter at every landing.** The census exact and the conformance suite green or pending by name in every view at each row's
  commit, so that each landing is shown to leave Daoris working as it did; a pending line names the Daoris row or the Lyntai request
  that makes it pass.
- **A provider on the layer, compared.** The first provider moved onto the layer passes the suite case for case as the one it replaces,
  old against new, as row b compared the stand-ins on the contract with their classes; a real-process start, turn, resume and stop on
  each of its doors.
- **A stand-in through the layer.** A stand-in added by its folder and one registration, through the layer, whose diff is its proof: a
  change outside its folder is a gap, named.
- **The boundary held.** `StructureTests` and the census hold the layer's types to the place the design gives them, and fail on one
  planted outside it.
- **The build.** A build at each pinned layer version; a clean build with no feed reachable, by the fallback the design names; the
  install's assemblies restoring nothing at run time.
- **The requests.** Each Lyntai part asked by a quest whose receipt the row records; nothing written into Lyntai.
- **The install**, after each row that moves a door: a session on each door reads, resumes, cools and ends as before, its records
  readable by the build before; each such proof names its account and its bound, and runs under a recorded authorization or the
  owner's bounded yes.

## 9. What must not regress

Every step keeps today's suites green, as `daoris.gates.json` declares them, and adds its own. Beside the suites the provider design's
brief names (`docs/2026-10-26-agent-provider-brief.md` §9: the doors, accounts and the toolchain, errors, permissions and children, the
rehearsals), which a step touching that part runs whole:

- **The meter**: `ProviderCensusTests` and its three twins, `ProviderConformanceTests` with its environment and Process halves,
  `ProviderRegistryTests`, `ProviderFilesTests` and the tools' `stand-ins.test.ts`, `StructureTests`.
- **The service's own use of Lyntai's packages** where a shared version moves: its knowledge search suites.

And the behaviours the install relies on, each with a case before the step that could break it: a session on each door starts, takes a
turn, resumes its own conversation and ends as today; a limit cools the account it ran as, a limit on Claude Code's protocol door
cooling its native door's account, and never strikes; a transient error is tried again in the same conversation, a word once on the
wire never sent again; a refused model passes its account first and then the choice; a full or asking start on a door with none
mapped is refused; a permission request held for the person waits for them; a subagent's lines stay the subagent's; a record an
older build holds still reads; and a person who sets nothing sees Daoris work as today.

## 10. How the work lands

**A feature a person can see working, end to end** (D187). Each row is named by its outcome (*Codex sessions read their failures
through the layer and cool as before*), taken by one builder across every lane it needs in this repository, and handed back working;
split only by sub-feature, never into a host half and a library half. A Lyntai part is Lyntai's own feature, built and released there;
the Daoris row that takes it up is the feature a person sees here, and waits for that release. A row that changes nothing a person sees,
as rows a and b, says why it is a row of its own and what it proves.

**Never a big-bang rewrite.** The move is reached in steps, each landing with every gate of §9 green and leaving Daoris working as it
did for a person who sets nothing:

1. **The reference first, measured.** The package taken, its versions and closure measured, no reader moved, the census and the suite
   unchanged.
2. **One door, or one part, at a time**, today's surface its adapter, the census shrinking by the sites it moved and failing on one that
   neither moved nor is named.
3. **A difference found is a decision, not a refactor**: where the layer and today's code answer one fact differently, the step states
   which stands, as a dated note on the decision it amends, or a request to Lyntai.
4. **Records last, and compatibly**: a stored id or shape changes only after every reader reads it through the new seam, in a shape older
   builds cannot misread.
5. **The stand-in early**, so every later step is proven on it; OPENROUTER1 after the parts it needs.

A proposal names its first three rows concretely, with their proofs and the Lyntai release each waits for, and the rest in order.
