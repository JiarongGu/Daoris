# Future directions, reviewed against the record

**What this is.** [`DAORIS_FUTURE_DIRECTIONS.md`](DAORIS_FUTURE_DIRECTIONS.md) is an outside analysis the owner
brought in on 2026-10-03, kept with its words unchanged. Its 73 sections suggest how Daoris could prove, explain,
recover and learn from the work it drives. This review weighs each one as a suggestion against what is built and
decided, at main `5d149602`. Where a suggestion conflicts with a decision the owner made, it is reported as a conflict,
with the reason, and not adopted. Nothing here is decided. The order in §3 is a recommendation for the owner.

**Classes.** *Exists*: the record already does it. *Partly*: what exists, and exactly what is missing. *New*: nothing
does it yet. *Conflicts*: it contradicts a decision, which is named. `Dnn` is `docs/decisions/Dnn.md` (D134). A row ID
is in `TASKS.md` while it is open and in `docs/task-archive.md` once it is closed. A design named by its subject
(the intake, plugin, toolchain, working-surface and service designs) is the contract [`README.md`](README.md) lists
for it. *D46 §4* is the driver design's §4 (`2026-09-19-driver-design.md`), as the backlog cites it.

**The count:** 18 exist, 38 partly, 15 new, 2 conflict. The analysis's §0 lists what Daoris has, and the list is
accurate.

## 1. Section by section

| § | Suggestion | Class | Where the record stands |
|---|---|---|---|
| 1 | Evidence contracts | partly | A quest's requirements quote the person, each with the check that proves it. A done answers each one, met or departed, and a departure holds the quest for the person's yes (D133, DRIFT1c, DRIFT1d). Missing: Daoris checking the evidence. A met answer is the agent's word (D133's *not covered*). The record's evidence holds the commits the driver saw the session make, and the gate results D46 §4 names are not recorded |
| 2 | Engineering ledger | partly | Each piece is recorded, in five places: the session record (adapter, harness version, account, the commits it made), the quest's operations (D68), the ask's words and go-aheads (D133, D135), the composed instruction and the events (D76), and the landing's branch and commits (D102). Missing: one read that joins them, and a way from a commit back to why |
| 3 | Replayable sessions | partly | A session resumes by the id Daoris kept (D131, D137), and the record names its versions and account. The rules handed are written per session under the home (`SpawnSettings`), and nothing reads them back. Missing: the envelope read back (`--inspect`), and the canon version kept per session. A fork is held (MSG1k) |
| 4 | Checkpoints and recovery | exists | A cut-off is carried on in its tree and told what cut it off and what it left uncommitted (D80, D104). An answer resumes the conversation (D131, D137). The ask's words, go-aheads and standing answer survive any account (D133, D135). A limit rotates the account (D125). D46 §4 keeps checkpoints observed, never reported |
| 5 | Outcome evaluation | partly | Usage per session and per account (D57, TOOL3), the strike count derived from records (D58), and the machine log's timings and refusals (D94). Missing: an outcome per quest (sessions, carry-ons, answers, departures, reopenings). Its own boundary, no universal score, is D54's |
| 6 | Evidence-based agent routing | conflicts | D130: the person says which accounts run a workspace and how they are used, and Daoris infers nothing. The toolchain design's §3 (D57): the agent is the person's pick, then the workspace's, then the machine's. A selection Daoris makes from history is an inference. What is compatible: outcomes shown beside the pick |
| 7 | Repository intelligence 2.0 | partly | Each repository's modules and dependencies (MAP3a–e: `code-map.json`, `daoris-devkit map`, fed with its commit), and the workspace topology with declared `uses` (MAP2, D91). Missing: symbols, contracts, impact, and annotations beyond a module's summary. ROADMAP's *Long term* names Roslyn for it |
| 8 | Change plans and missions | partly | An ask is the work above its quests (D65, D132). A chain sends steps to other repositories (`then`, up to five, intake design §1g), and a quest can wait on another (D79). Missing: a step after several (fan-in). The intake design §2 declines a workflow engine and a coordinator: a chain is data |
| 9 | Contract-driven changes | new | Nothing detects an API, schema or package diff. D91's `uses` names the consumers it would ask. A dependent quest is a request a session publishes (D32), which fits |
| 10 | Workspace invariants | new | Gates belong to a repository (`daoris.gates.json`, D26). A workspace is wiring and holds no tracked file (D48, WSP1), so a workspace gate would live in the repository that owns the integration, or on the machine |
| 11 | Human decision objects | exists | A go-ahead is held on the ask, named by its act and answered in the person's words. A standing answer is kept per repository and handed to every later session (D135 §2–§3). Every word on the ask is kept (D133). Missing only expiry and a workspace scope, and nobody has asked for either |
| 12 | Knowledge provenance | partly | An entry carries its repository, path, kind (decision, fix, task outcome…) and whether it is canonical or local, and its class decides where it may go (service design §4). A fed entry names the commit it speaks for (WSP4). Missing: a lifecycle (proposed, verified, superseded) |
| 13 | Contradiction detection | new | Convergence finds agreement (D17, D30), and nothing finds disagreement. It is a tier that needs a model, so D24 applies: a floor that needs none, the tier named, and a person who disposes (D31) |
| 14 | Knowledge decay | partly | The index is rebuilt from files, so a removed source leaves it, and a README stops speaking once a lock exists (D124 §6). Missing: staleness from changed code or a superseded decision. It would report, never gate (D54) |
| 15 | Context compiler | partly | The driver composes each instruction from named sections (the quest, the ask's words, requirements, go-aheads, the standing answer, the code map, the indexes), each bounded and saying what it left out, and keeps it as the session's first event (D76). Missing: each section's size and source (`context explain`) |
| 16 | Context experiments | partly | KNOW3 ran 72 real sessions behind six knowledge designs (`tools/knowledge-bench.mjs`), and KNOWUSE2 replays recorded questions. Both are development benches. Comparing contexts over real work waits on §5 |
| 17 | Doctrine effectiveness | partly | KNOWUSE1 classed 46 questions against what the sessions had read, and DOC7 puts each read in the machine log. Missing: a rule's effect on outcomes, which needs §5. Retiring a rule stays a person's review |
| 18 | Failure taxonomy | partly | A record ends `completed`, `declined`, `stood-down`, `failed`, `stopped` or interrupted, each by observation (D46 §4, D104). A limit is read from the agent's words and is never a strike (D125), and refusals carry codes (D94). Missing: causes kept on the quest across its sessions |
| 19 | Incident to doctrine | partly | `upstream` promotes under review (D9). Convergence proposes and a person disposes (D17, D31), and the fix-log skill records causes. Missing: repeated failures grouped as candidate lessons. The canon's bar is two repositories (CANON9), so one repository's lesson stays local |
| 20 | Safe parallelism | exists | The tree is the unit of exclusion, and a repository may have several (D51). Lanes are path globs a repository declares, with lane locks, a cap and a queue (D115: DEV2–DEV4 built, DEV5–DEV11 open). The record is ahead of the analysis's order here |
| 21 | Speculative execution | conflicts | A quest has one take. The quest state machine is the only lock, and a second take stands down (D46, D69). What is compatible: two quests with the same requirements, and the person choosing |
| 22 | Budget and cost policies | partly | Strikes are `maxRetries` (D58), and a session timeout (D80) and the cap bound a run. A plugin's `quest/consider` hold is where a budget policy would speak (plugin design §4). A price conflicts with D57, which claims none. COST1 is the first slice: measure first |
| 23 | Environment contracts | partly | Each tool is the system's, managed, or a file named, with its version (D121). A pin nobody installed refuses (D57), and a missing harness refuses naming its install. Missing: what a repository requires, checked before a start |
| 24 | Remote workers | partly | Every machine drives its own repositories and claims by push (D47, D69), and a headless driver is one more machine (D50, D104). Missing: what a machine can run, read by its own planner. A dispatcher that assigns work would conflict with D46, where sessions claim their own |
| 25 | Secrets as capabilities | partly | Credentials stay outside the model. A push is a plugin's, run as the person (D87, D100). A browser's sign-in belongs to a plugin or to Daoris (D77, D78), and a key is Daoris's (D67). Missing: named capabilities, and grants per quest |
| 26 | Policy simulation | new | A widening already waits for the person (D74), and its card says what it changes (D89). Nothing shows which sessions a rule change would block |
| 27 | Supply-chain security | partly | The devkit is hash-pinned (D27). Every tool download is checked against its maker's published sum (D121; TOOLS9 open), and a plugin records where it came from (D103). The repository signature is held (PLUGDIST1h). Missing: a plugin's permissions, and their diff on an update (§28) |
| 28 | Plugin capability model | partly | A manifest declares harnesses, points and servers, and a plugin answers only the points it declared. A server handed is available, not approved (plugin design §3–§4). Missing: filesystem, network and process capabilities. A plugin runs as the person (D101), so a declared limit would be a claim, not a fact |
| 29 | Plugin protocol versioning | exists | `apiVersion` is read first, and an unknown one is refused naming both numbers. `initialize` carries `protocolVersion` (plugin design §3–§4). The kit's trial is the doctor (D101), and PLUGUI1g adds a plugin's own checks |
| 30 | Quest templates | new | The intake composes a chain (D65), and nothing names a kind of quest with its default checks |
| 31 | Risk classification | partly | What stays human is fixed (D37) and held by rules in scopes (D72). A repository declares its safe work (D122; UNBLOCK2 open). Missing: a declared risk per quest or path |
| 32 | Blast radius | new | It needs §7's impact layer. D91's `uses` gives the dependents |
| 33 | Independent reviewer | partly | A chain's verify step is a separate session in its own tree, grown from the parent's branch (intake design §1g, D82). D133 §5 tells it the requirements are the measure (DRIFT1e, design first). Missing: which topology ran, on the record |
| 34 | Adversarial review | new | It would be a verify step's instruction, and nothing composes one |
| 35 | Repository health | partly | Overview's *What needs you* (D40, INT4d), sessions by what they need (D126), plugin health (D119), the setup guide's state (D97), and `daoris status` and `check`. Missing: one page per repository where every item says what to do |
| 36 | Workspace timeline | partly | A session's timeline is derived from transitions, quest moves and commits (working-surface design §3). A quest is its operations (D68), and the machine log keeps the rest (D94). Missing: one timeline across a workspace |
| 37 | "Why?" queries | partly | Each resolution names what decided it (D86, the toolchain design's §3, D24's tier, D77). A sitting quest always says why (D46 §3), and Ask Daoris reads the machine (D89, D110). Missing: a stored *because* for every automated choice, readable later |
| 38 | Event-sourced control plane | exists | A quest is its operations, replayed through one transition table (D68). A session is its typed events (D76). Counts are derived, never stored (D58, USE1c) |
| 39 | Offline-first conflicts | exists | Every machine commits locally, and sync is fetch, rebase, push (D68). A take claims by push (D69), and a delete travels as an operation (D95) |
| 40 | Multi-user roles | new | A shared host gates every route with keys per person and per machine (D47), and there are no roles. §68.6 agrees: not before a team asks |
| 41 | Intake adapters | exists | The intake is a session that reads a ticket through a server a plugin declares (D65, D78). The intake design §2 declines a ticket integration in the host, which a host-side source type would be |
| 42 | Review feedback loop | new | A landing records the pull request a plugin opened (D102), so the link exists. Reading the review would be a plugin's to speak (D64) |
| 43 | Release missions | new | Nothing coordinates a release. Publishing stays the person's (D37), and Daoris is not CI (§68.2; the release workflow is manual) |
| 44 | Production feedback | new | An alert would enter as an ask (D65), through a plugin's server. Nothing does that yet |
| 45 | Architecture fitness | partly | A repository declares its gates (D26), and `map --check` gates the code map as a fact (D54). Missing: rules over the map (*A may depend only on B*) |
| 46 | Architecture drift | new | It would report and never fail, unless a repository declares it as a gate (D54) |
| 47 | Repository brief | exists | A set-up quest, worked by the repository's own session, writes its domain, knowledge, brief, rooms and checks for its owner's review (D124 §3, D117 §6, D122). Until then its README speaks for it (D77, D124 §6) |
| 48 | Workspace onboarding | exists | A folder becomes a workspace in one statement (D77), and its set-ups run as a paced plan (D124 §5). A dependency is declared by the repository, never guessed (D91) |
| 49 | Test selection | partly | A lane names the declared gates its landing needs (D115). Missing: affected tests read from the code map, advisory first |
| 50 | Cached verification | new | Every gate runs again at every merge |
| 51 | Semantic diff | new | It needs §7's contract layer. D24 applies: tooling first, and a model only as a named tier |
| 52 | Acceptance criteria generator | partly | The intake writes each requirement with the check that proves it (D133 §3). A criterion the person never said is refused as a requirement. Proposed to them and answered, it becomes their words. Missing: that proposal before the work starts |
| 53 | Ambiguity detection | exists | A session looks before it asks, and only what no source holds stops it (D124 §7). A close keeps *Needs you* apart from *Readings* (D135 §4), and an intake parks to ask (INT4d) |
| 54 | Assumption ledger | partly | A close names the source of each reading (D135 §4), and a departure quotes the words it turns on and holds (D133 §4). Missing: a structured ledger. It would amend D135 §4's choice that nothing parses a close |
| 55 | Decomposition quality | new | It needs §5's outcomes. D132 reads an ask's work as one thing, which is where it would be measured |
| 56 | "Do not automate" registry | exists | Destructive, irreversible, cross-repository and publishing acts are the person's (D37). Deny and ask rules sit in machine, workspace and repository scopes (D72), and a production act is a go-ahead (D135 §2) |
| 57 | Time-scoped permissions | partly | A go-ahead lasts as long as its ask (D135 §2), and a widening waits for the person (D74). Missing: a rule scoped to a quest or a session, with an expiry |
| 58 | Network policy | partly | Scopes hand over the harness's own rules, which can name domains (D72). Daoris invents no rule language (D72), so a vocabulary of its own would conflict. Recording the rules a session was handed is §2's |
| 59 | Redaction boundary | exists | What may leave a machine is declared, and silence means local (D21, service design §4, D47). Conversations and the log stay on the machine (D76, D94), and a root is stripped on sync. AGT3c is an open leak of exactly this kind |
| 60 | Knowledge export | partly | Knowledge is the repositories' own files, and doctrine survives Daoris's absence (D2, D16, D48 §2a). The code map is a committed file. Missing: an export of quests, sessions and asks |
| 61 | Headless driver | exists | `daoris-driver` runs the same loop without the window (D46). It names its kind on the home's lock (D104), and a terminal can set everything it needs (D50) |
| 62 | Protocol surface | partly | The ACP door (D53), the plugin wire (D64), the connector's MCP tools and the HTTP routes all exist, and files are the API (D50). None is published as stable, since nothing is published (ROADMAP, *Versions*) |
| 63 | Compatibility test kit | partly | The kit tries a plugin as the driver would (D101), and the family rehearsal's ACP stub holds the door (D53). Missing: a conformance run for a harness |
| 64 | Sandbox workspace | exists | The example family (D39), with a repository born inside the loops (D44) |
| 65 | Doctor 2.0 | partly | `daoris doctor` (D17), `status` and `check`, the setup guide's state (D97), plugin health (D119) and `daoris-driver logs` (D94). Missing: one summary of what needs acting on |
| 66 | Explainable automation | exists | Held across the decisions (§4), and recommended as one decision |
| 67 | Deterministic core | exists | D24, D46 §4, D54, D65 §1b, D74 and D89. The human column is D37's |
| 68 | What not to become | exists | 68.1 is D1 and D22. 68.2 is the manual release workflow and D87. 68.3 is D55, which calls the desktop a code-gen-driven IDE and rules out an editor: the same substance in another word. 68.4 is the intake design §2, 68.5 is D46 and D79, and 68.6 is ROADMAP's *adoption gates growth* |
| 69 | Phased roadmap | partly | Weighed in §2. A4, B1, B3, C1, F1 and F3 are built or designed, and E2 conflicts (§6) |
| 70 | Near-term top 10 | partly | Items 4, 6 and 7 are built. Items 1, 2, 3 and the outcome half of 10 lead §3. Item 5 rides open rows, and items 8 and 9 wait on a trigger |
| 71 | Long-term architecture | partly | The control plane, quests, sessions, knowledge, policy and intake are built (D45). The mission's fan-in, the ledger read and outcomes are not |
| 72 | The strategic moat | partly | The intent is kept verbatim (D133), the decisions too (D135), the outcome is observed (D46 §4) and a lesson is promoted (D9). The links between them are what is missing |
| 73 | Final direction | exists | *Daoris manages engineering reality; agents supply judgement* is D65 §1b (the driver's brain is a session, not a model) and D46 §4 (the record is concluded from what is observed) |

## 2. The analysis's order, weighed (§69–§70)

The order lags the record on reliability and scale. Recovery (B1) is D80, D131 and D137. Sync conflicts (B3) are D68.
The headless driver (F1) shipped with D46, and parallel sessions (F3) are D51 and D115. Human decisions (A4) are D135,
and the repository brief (C1) is D124.

What it gets right is the gap. Daoris records nearly everything the ledger asks for, in separate stores: session
records, quest operations, the ask's words, landings, events and the machine log. Nothing reads them together. And
since D133 a done answers each requirement, but nothing checks the answer against the work. The first phase's
substance, a trace and evidence a fact can check, is the right next step.

One item should not be taken as written. E2 (routing) chooses for the person what D130 says the person chooses.
Its measurement half, E1, is worth having.

## 3. Recommended sequence

Each row below is a proposal for the backlog. The parent files them, and the decision numbers are reserved when a
row is dispatched.

| # | Row | Serves | Smallest slice that proves it | Decision it needs | Depends on |
|---|---|---|---|---|---|
| 1 | **TRACE1** | §2, §3's `--inspect`, §37 | `daoris-driver trace <commit\|session\|quest>` reads only what is recorded (landings, session records, quest operations, the ask's words and go-aheads, the instruction event, the rules handed) and prints the chain from ask to commit. A missing link is said to be missing, never guessed | One: §66 as a rule (a choice keeps its *because*), with both doors (D50, D110) | Nothing new: every store exists |
| 2 | **EVID1** | §1, §33 | Design first. A requirement's check may name a file the done's branch must hold, or a declared gate. Daoris checks it when the session ends, and a met answer without its evidence holds the quest as a departure does | One, amending D133 §4 and completing the evidence D46 §4 names | D133 (built) and DRIFT1e's design. Gate evidence waits for DEV5's queue |
| 3 | **CONTEXT1** | §15, §17 | Each section of the composed instruction, with its size, its source and what its bound left out, is kept beside the first event and shown on the session's page and at a terminal door | A note on D127 | Nothing. KNOW2b and DOC7 add to it later |
| 4 | **OUTCOME1** | §5, §18, §55 | `tools/usage-report.mjs` gains an outcome per quest, derived from the records: sessions, carry-ons, answers, departures, strikes, limits counted apart, and how it closed. It is a report (D54) with no new store, and an absent value is never zero (D57) | None for the report. A cause vocabulary kept on the quest would need one | TRACE1's reader |
| 5 | **AFTER1** | §8 | Held until an ask needs a step after two repositories' work. A chain step names the quests it starts after, and the planner holds it, saying which are still open | One, amending the linear chain (intake design §1g) | A real ask |

**Already open, and serving the analysis:** DRIFT1e and DRIFT1d2 (§1, §33); ANSWER1d, MSG1b–MSG1j and TOOL6d (§4);
COST1 (§22, measured first); DOC7 and KNOW2b (§15, §17); KNOWUSE2 (§16); DEV5–DEV7 (§1's gate evidence, §20); TOOLS9
(§27); and WSSETUP14b–f (§47).

**Held until a trigger, as the ROADMAP's rule asks** (*a capability nobody has asked for is a guess*):

- **Surfaces over the same joins** (§35's health page, §36's workspace timeline, §60's export, §65's summary) come
  after TRACE1 and OUTCOME1, which give them a reader. TRACE1's output as JSON is the ledger export's first form.
- **Lessons from failures** (§19) come after OUTCOME1 has causes to group, and keep the canon's two-repository bar.
- **Repository intelligence** (§7, §9, §32, §45, §46, §49, §51) waits for the first contract break seen across two
  repositories. That is its consumer, and the Roslyn layer the ROADMAP names would serve it.
- **ENV1** (§23) comes after TOOLS6, and **risk** (§31) after UNBLOCK2 has landed the safe-work declaration.
- **Knowledge provenance, decay and contradiction** (§12–§14) come after KNOW2a's recall probe.
- **The intake's proposed criteria and an assumption ledger** (§52, §54) come after KNOWUSE2 measures how sessions
  read, since each would amend D133 or D135.
- **Plugin capabilities, supply chain and per-quest grants** (§25, §27, §28) come with the first plugin from outside
  Daoris (PLUGDIST1f). **Protocols and a conformance kit** (§62, §63) come at the first publish.
- **What a machine can run** (§24) comes when a second machine drives a workspace it cannot fully run.
- **Routing** (§6) comes only as outcomes shown beside the person's pick, after OUTCOME1 has a month of data.
- The rest (§10, §26, §30, §34, §40, §42–§44, §50, §55, §57, §58) wait until a workspace, a team, a release or a
  measured cost asks for one.

## 4. Principles

**Already held.**

- **§66, explainable automation.** D24 says which tier answered. D46 §3 makes a sitting quest say why, D86 names what
  set a line, the toolchain design's §3 says which pick won, and D110 gives every door an answer.
- **§67, a deterministic core and a probabilistic edge.** D24 keeps the model a deployment's choice. D46 §4 concludes a
  record from what is observed, and D54 lets a fact gate while a judgement reports. D65 §1b makes the driver's brain a
  session, not a model. D74 and D89 let an agent propose while the person decides, and D37 is the human column.
- **§68 and §73.** These are the boundaries D1, D22, D55, D87 and the intake design §2 already draw.

**To adopt as a decision.** Only §66, as one rule that binds every new feature: *a choice Daoris makes keeps the
facts it was made from, and a door answers why*. Today it is spread across decisions and designs, each for its own
feature. TRACE1 would carry it. §67 needs no decision of its own, because D24, D54 and D37 already state its three
columns.

**Not adopted, each for a decision's reason:**

- choosing an agent from history (§6, D130)
- a second take of one quest (§21, D46 and D69)
- a criterion the person never said, as a requirement (§52, D133 §3)
- a tracked workspace file (§10, D48)
- a dispatcher that assigns work to machines (§24, D46)
- a rule language of Daoris's own (§58, D72)
- a price on a budget (§22, D57)

**What this review does not cover.** It reads the record and runs nothing. Each *exists* is the record's claim, and
the *not covered* notes inside those decisions stand.
