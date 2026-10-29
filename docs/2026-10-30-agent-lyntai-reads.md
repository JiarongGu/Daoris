# AGENTLYNTAI1: the reads of rounds 1 and 2 (D191)

The two makers' reads of each round of `docs/2026-10-30-agent-lyntai-design.md`, each written read-only and apart, neither seeing the other's
before its own was written: for each round the Opus maker's first, and Codex's beside it, added verbatim when it lands. Round 2's reads
follow round 1's, below. Each finding names the line it rests
on in Daoris or in Lyntai, in the design's aliases: a bare path is under the driver library, `Tests/` is its tests, `Lyntai/Core/` and
`Lyntai/Basic/` are Lyntai's `src/Lyntai.Core/` and `src/Lyntai.Providers.Basic/`, and a Lyntai line marked *(AG20)* is its commit
`2fb2dc7e`'s, any other its `master` `9a41435c`'s. The steward checks each finding against the code before it is taken (D191 point 2), and
round 2 answers every finding of both, one row each.

## The Opus maker, round 1

The Opus maker's read of round 1 at Daoris `460b6d47c` (the batch line `parent/batch-1029p` with the design merged; of the files cited
here only `Loop/Chat/ChatRunner.cs` moved since the design's `23cc27254`, and its lines are this commit's) and at Lyntai `master`
`9a41435c` with its AG20 commit `2fb2dc7e`, Lyntai only read: 16 findings, 6 blocking (F1 to F6); **another round**. Read for it: the
design whole, D191, D213's point 10 and its notes of 2026-10-29 and 2026-10-30, the brief whole, the provider design's §11 opening,
AGENTINFRA1's §4 and §7, FOUNDATION1's §19.1, and every line cited below in both repositories.

### 1. Claims about the code

**What holds.**
- §0.2's rows 1 to 6 and 9, at the lines they cite (rows 7, 8 and 10 not checked again). Row 1 more firmly than it says: `2fb2dc7e` and
  its replay `0cbd84c1` are neither of them ancestors of `master`, the replay is its session branch's tip, and the two commits' `src/`
  differ only in `Lyntai.Generation`, so the layer's sources the design read are the replay's.
- **The run-kind split** (§1.1 row 3). A driven native start is one process with its prompt: the prompt is the argument after `-p`
  (`Agents/Adapters.cs:2188-2197`), spawned through a shell whose stdin stays closed (`:1532-1541`); words said while it runs are held and
  go on as a resume process each, the settings and servers files handed again (`Loop/DriverContinuations.cs:1149-1197`, MSG1b); a park's
  answer is a resume process too (`:385-389`). A message and an interrupt framed on stdin are a conversation's alone
  (`Loop/Chat/ChatRunner.cs:3642`, `:3660-3690`). Nothing of a held stdin or a live run serves a driven start, so the layer's one process
  per turn fits it.
- **The third row** (§14 item 1). The usage reading is the right first part to move, and LQ2 is generic: the version gate is already a
  parameter of the usage source (`Lyntai/Basic/CliUsageSource.cs:56` *(AG20)*), left true by the registration
  (`ClaudeCli/ClaudeCliBuilderExtensions.cs:102-104` *(AG20)*), so the general need is a host's option, which Lyntai's rule 7 admits.
- The reducer is pure and internal, the pool one process (`Lyntai/Core/Agents/AgentPoolReducer.cs:17-27`,
  `AgentAccountPool.cs:32-34` *(AG20)*), as §0.2 row 5 says.

**What is wrong:**
- §2.2 lets a move row state its differences, against D213 point 10 (F4).
- §4.1's bound door states `Spawn`, `Dialect` and its tables absent, which a door split by run kind cannot (F3).
- §5.3's translation into a `DoorFailure` leaves `AgentErrors.Read` nothing to read (F9).
- §9.3's 1f says the native transport goes; `stub` still converses on it (F7).
- §3.2 and LQ12 rest *several processes on one home* on a line about two shells (F14).
- §0.3's dsh row calls dsh's home variable unmeasured; the adapter's own premise states it (F15).
- §5.1 and §1.1 row 3 cite `CliAgentLoop.cs:23` for the prompt on stdin, the class's line (F16).

### 2. What §14 asks of the Opus maker

- **Item 2, the native door split by run kind.** The split holds (§1 above). The spawn table's three differences are a decision, a
  decision and none, and none of them belongs inside 1d (F4); the table misses the runner's clock (F5), stderr and the stop (F10), the
  bypass flag, the asking posture and the argument order (F4).
- **Item 3, one registration per start.** It holds under concurrent starts, since each private container holds one registration and
  shares no keyed singleton; its cost is `AddLyntai` whole per start, it reads the driver's `LYNTAI_*` variables at every start, and its
  lifetime and the long-lived container it needs beside it are unsaid (F8, F5).
- **Item 4, the stand-ins at a move.** `alt-stub`'s native door as a host's own provider is Daoris writing the layer's turn loop (F6);
  `stub`'s fate is forced at 1f, not row t (F7); the Process suites' Claude-vocabulary programs read the prompt after `-p` today (F4).
- **Item 5, OPENROUTER1 on Daoris's transport before 1d.** Right, with three changes: no AG17 for its move, its own move after 1d, and its
  door on the contract by reference (F12).

### 3. What §14 asks of both makers

- **Item 7, the parity rule's mechanism.** Joining the fourth view in the moving row does not prove parity by the suite's own rule as
  claimed: no live case runs on a real provider, the old views reach a bound door through a shim that cannot present it, the live cases'
  bodies differ by view, and *the commit before* has no mechanism (F2, resting on F1).
- **Item 6, LQ12's shape.** Right in kind; it names three of the five operations stage B needs and misses the credential epoch (F13).
- **Whether an isolated home runs several processes.** It does, by lines stronger than the design cites (F14).

### 4. Findings

**F1 (BLOCKING). Row 1b and every move need row m first, and 1b-i a sink before row l.** No executor can start a layer-run door today.
The executors hold `ISessionAdapter`s and start the `ProcessStartInfo` each prepares (`Loop/DriverContinuations.cs:387-391`); `AgentRun` is
declared for row m to make them hold it (`Agents/Contract/AgentRun.cs:3-8`), nothing in the desktop implements it, and neither
`IReadingSink` nor `WireReading` exists yet. A layer-run door has no `ProcessStartInfo`: it is a session's `StreamAsync` over Daoris's
runner. Yet 1b-ii's proof has the family rehearsal's stand-in phase run `alt-stub`'s pair *with no edit* (§9.2, §12), and that phase drives
each door through the driver, a quest taken to done (D213's note of row b part 3, `docs/decisions/D213.md:439-444`); 1d's proof runs
`LimitWaitsProcessTests`, `ContinuationTests` and the native suites *through the layer's door* (§12), each an executor's path. §9.1
places m *beside* 1a to 1c, in no order against them. And 1b-i writes the translated events *to the same sink as today's transports*
(§5.4), a sink that is row l's.
*Change:* order row m's first part (the executors holding `AgentRun`, a Daoris-run `AgentRun` over today's transports among it) before
1b-ii and before every move, or narrow 1b-ii's proof to the suite and its own program until m lands; and say what 1b-i writes the
translated readings into until row l lands (today's event store and transcript writer, re-pointed at l), each with its case.

**F2 (BLOCKING). The fourth view does not prove parity by the suite's own rule.** §6.2 claims that a case passing on the old view and
failing on the layer's cannot be held pending, so joining in the moving row proves parity. Four things stand against it:
- The suite runs live cases on stand-ins alone (`Tests/Agents/ProviderConformanceLive.cs:61`; `ProviderConformanceTests.cs:58-60`), so a
  real provider's cases in the fourth view are its declarations and its mapping; its behaviour at a move is the Process suites' and the
  rehearsals', outside the rule.
- The old views are built over `AdapterSet.Built()` and `LegacyDoor.Over(Providers.Built())` (`Tests/Agents/ProviderConformance.cs:99-114`),
  and `AdapterSet.Built()` takes the registration list's providers through the shim as well (`docs/decisions/D213.md:352`). A bound door
  registered in `Providers.Built()` (1b-ii's one line) reaches both old views through `LegacyDoor`, which presents a door's `ISpawn`, while
  §4.1 has a bound door state `Spawn` absent. Every case on it fails there, and since a provider's pending file has no view
  (`ProviderConformance.cs:202-215`, `:283-308`), none can be held: the suite is red at the first binding, unless the old views leave
  bound doors out, which removes the comparison the claim rests on.
- The live cases are written over `run.Adapter.Prepare` (`ProviderConformanceLive.cs:84`, `:132`), so a live case in the fourth view is a
  second body, and two bodies' equal verdicts prove parity only where they assert the same.
- *Its old-view cases the commit before* has no mechanism: nothing records a view's verdicts from one commit for the next.

*Change:* make parity a comparison inside one commit. The live cases are written once over `AgentRun`, today's run (F1) beside
`LayerRun`; the moving row presents the door on both paths in one commit, its old path kept constructible there and out of production,
each case a theory over the two paths under the one pending file, and the door's Process suites' programs run on both paths the same way;
the old path and its rows go in the row's last commit, the census falling there. Say what each old view holds for a bound door after it.

**F3 (BLOCKING). A door split by run kind cannot be written as §4.1 binds a door.** §4.1 gives `AgentDoor` one binding, a
`Known<LayerDoor>`, and has a bound door state `Spawn`, `Dialect` and its tables absent; §2.3 rule 2 refuses a registration declaring a home
variable or a table beside a binding. But 1d binds `claude-code` for its driven start alone, its conversations Daoris-run until 1f (§9.3),
and a conversation needs the door's `Chat` spawn and its dialect's framing (`Agents/Contract/Ports.cs:20-21`, `:52-56`;
`Agents/Adapters.cs:2239-2287`) and Claude Code's failure tables. The suite's rows carry no run kind either (a view, a provider, a door and
a case: `Tests/Agents/ProviderConformance.cs:189-215`), so *the suite holds each live case on the kind it drives* (§5.1) has nowhere to
say it.
*Change:* the binding names the run kinds it covers (driven, conversation); a door states for each Daoris-run kind what it states today;
rules 2 and 3 and the census read per kind, the home variable read from the descriptor for both kinds once one is bound, Daoris's tables
standing behind the Daoris-run kind alone, so one reading holds per path; the suite's row carries the run kind for a door split by it.

**F4 (BLOCKING). A move row states no difference; 1d's three are a decision, a decision and none.** D213 point 10 stands for what stays
Daoris's: *a move changes nothing; each disagreement is a correction of its own* (`docs/decisions/D213.md:115`), as the brief §6 says.
§2.2 instead lets a move row state each spawn-table difference *as a decision note or a request*, and 1d states three (§9.3). Each, checked:
- **The prompt on stdin is a decision, Daoris's, not a request.** Lyntai's rule is general and principled: a prompt never on argv, which is
  readable machine-wide and carries a shell's metacharacters (`Lyntai/Basic/ClaudeCli/ClaudeArgs.cs:6-9`, `ClaudeAgentArgs.cs:6-7`), and
  nothing Daoris does needs argv. Today the prompt is the argument after `-p` on a start and a resume (`Agents/Adapters.cs:2192`, `:2228`),
  and Daoris's tests and fake programs read it there (`Tests/Loop/NativeResumeTests.cs:29`; `Tests/Loop/SessionMessagesTickTests.cs:851-853`).
  It is a correction row of its own before 1d, on today's transport: the start, the resume and the go-on write the prompt to stdin and
  close it, the tests and programs moved to read it there.
- **The three tools always denied** (`ClaudeAgentArgs.cs:10-12`, with no option to lift them) narrow what a driven start may do, so they are
  a ⚖ decision first: whether a driven start denies `AskUserQuestion`, `ExitPlanMode` and `EnterPlanMode`, with what each does on today's
  `-p` run measured. A correction row of its own if yes; a request (the host stating the set) only if no, 1d then waiting for it. A
  conversation has a person to answer, so 1f asks the question again.
- **The server file is no difference.** `ClaudeAgentOptions.McpConfigPath` puts the caller's own document first under the one
  `--mcp-config` (`ClaudeAgentArgs.cs:119-134`), so the native door handed Daoris's file there, with `McpServers` empty, carries exactly
  today's `--mcp-config` and file (`Agents/Adapters.cs:2311-2315`). Decide it now; nothing widens or narrows, so nothing of it is ⚖ at
  1d, and the layer's rendering stays the protocol doors' question.

The table also misses three, each needing the same answer: `bypassPermissions` becomes `--dangerously-skip-permissions`, which suppresses
any mode and allowed tools (`ClaudeAgentArgs.cs:81-94`), where Daoris measured `--permission-mode bypassPermissions`
(`Agents/Adapters.cs:2289-2295`), a request under LQ4 or an equivalence measured; the asking posture has no mapping, the tool policy having
two values and read-only adding write denials with no mode (`Lyntai/Core/Agents/AgentSessionModels.cs:6`, `:22-28` *(AG20)*;
`ClaudeAgentArgs.cs:67-73`), which is LQ4's; and the argument vector's order differs throughout, `--mcp-config` taking a list, so §5.5's
equality needs its rule. `--setting-sources`, `--strict-mcp-config` and `--append-system-prompt` add nothing when left unset
(`ClaudeArgs.cs:46-57`; `ClaudeAgentArgs.cs:96-100`).
*Change:* §2.2 says a move row states no difference, each one found being a correction row or a request the move waits for; 1d's list
becomes the stdin correction, the denied tools' ⚖ correction, the server file decided now and the mode carried by LQ4; the table compares
each flag with its values as a pair, keeping order where a flag repeats or takes a list.

**F5 (BLOCKING). The layer arms a clock on every agent turn, which Daoris's sessions do not have.** The Claude CLI door hands the runner an
inactivity window from the options (`Lyntai/Basic/ClaudeCli/ClaudeAgentSession.cs:102-104`; `CliAgentLoop.cs:68-75`), two minutes unless
set (`Lyntai/Core/LyntaiOptions.cs:30`, `:140-149`), and its expiry is a transient failure (`CliAgentLoop.cs:47-49`), which Daoris's retry
would run again; the ACP door does the same (`Lyntai/Basic/Acp/AcpAgentSession.cs:147-148`). Daoris limits no session by time
(`Settings/DriverChoicesCommand.cs:664`), and a driven turn can be silent on stdout through a long tool step. `AddLyntai` applies the
`LYNTAI_*` variables after the host's configuration (`Lyntai/Core/DependencyInjection/ServiceCollectionExtensions.cs:48-51` *(AG20)*;
`LyntaiOptions.cs:167-170`), so an inherited `LYNTAI_TIMEOUT_SECONDS` would set every start's window whatever Daoris configures. The
runner seam lets a runner keep its own clock policy (`Lyntai/Core/Processes/IProcessRunner.cs:15-22`).
*Change:* §5.5 states `LauncherRunner`'s clocks: none on an agent turn, as today, and a usage read's patience as today's; held by a case of
a stand-in silent past the layer's default that ends by its own terminal, and one where a `LYNTAI_*` variable in the driver's environment
changes no start.

**F6 (BLOCKING). A native door the layer cannot read has Daoris write the layer's turn loop; ask for it now.** §3.1 row c makes
`alt-stub`'s native door *a host's own `IAgentProvider` in its folder, since no layer door can be taught a third agent's lines*. The
provider type is public (`Lyntai/Core/Agents/IAgentProvider.cs:51` *(AG20)*), but the turn loop every CLI door shares (exactly one terminal,
a fault made the terminal, the delivery stamped from what the turn showed) is internal (`Lyntai/Basic/CliAgentLoop.cs:23`, `:28-43`,
`:88-156`). So the stand-in's session re-implements it in Daoris: the copy D213's note and §2.3 reject, a second delivery rule beside the
one §5.3's retry reads, and held to the layer's stream rules by nothing Daoris runs (AG16 is Lyntai's own theory,
`Lyntai/TASKS.md:354-360` *(AG20)*). The trigger §8 defers, *when a third native agent is chosen*, is this row's own native door, and
`stub`'s plain lines need the same at 1f (F7).
*Change:* a request now: a host whose agent's lines the library does not read needs the CLI turn's rules over a line reader it supplies.
1b-ii lands `alt-stub`'s protocol door on the generic ACP door and holds its native door's cases pending on a row of this design whose why
names the request; no host turn loop is written meanwhile.

**F7 (not blocking; before 1d). `stub`'s fate is forced at 1f, not at row t.** `stub` mirrors Claude Code's stream words by reference (its
limits, windows, sign-in, refused words and error codes: `Agents/Adapters.cs:1725-1740`), which §3.1 row e makes the layer's at
`claude-code`'s move and §2.3 rule 4 deletes with its last run kind. `stub` is interactive, a conversation on stdin (`:1692-1702`), so it
rides the native conversation transport that 1f says goes. And the rehearsal's stub phases rehearse Claude Code's and Codex's paths over the
programs they write (`docs/decisions/D213.md:448-449`), a path `claude-code` leaves at 1d.
*Change:* decide now. From 1d `stub`'s mirrored words are its own, in its folder, counted as the stand-in's; at 1f `stub` moves onto F6's
answer, so 1f retires the native conversation transport whole and row t holds nothing of `stub`'s. Retiring it is refused: it is the
model-free gate of the loop, a conversation and the account phases (`Agents/Adapters.cs:1680-1683`, D46 §8). The rehearsal phases that
rehearse Claude Code's path are re-pointed at 1d to a Claude-vocabulary program through the layer's door, or said to rehearse the stand-in
alone; §9.3's 1f says Claude Code's native dialect goes there.

**F8 (not blocking; before 1b-i). One private container per start: its cost, its inputs, its lifetime.** The builder's constructor is
internal (`Lyntai/Core/DependencyInjection/LyntaiBuilder.cs:26` *(AG20)*), so every registration runs inside `AddLyntai` whole, each start
registering every feature area (the text front door, cortex, memory, the vector space, agents, jobs, guards and chat), checking
capabilities and reading the driver's `LYNTAI_*` variables (`ServiceCollectionExtensions.cs:25-105` *(AG20)*). Concurrent starts share no
keyed singleton, since each container holds one registration (`Lyntai/Basic/ClaudeCli/ClaudeCliBuilderExtensions.cs:100-106` *(AG20)*);
they share the layer's process-wide state, the verdict classifier's matchers among it (`Lyntai/Core/Inference/ProviderVerdictClassifier.cs:16`,
`:32`), which Daoris leaves empty. The design does not say three things: the container lives as long as the record's run, a go-on and a
resume running on the start's spawn (`Loop/DriverContinuations.cs:1149-1177`), and is disposed at its end; the registry's layer view and
1c's usage sources need a container no start makes, and D234's check of one variable per agent runs only in a container holding several
doors of one agent (`LyntaiBuilder.cs:107-117` *(AG20)*), which a per-start container never is, so §4.2's exception is shown in the
long-lived one; and the per-start runner carries the start's own launcher arguments, its kind, class, home and log
(`Platform/AgentStarts.cs:30-31`).
*Change:* name the long-lived container and the per-start one, what each holds and how long it lives; 1b-i measures a start's composition
against its spawn and shows that the two describe a door alike.

**F9 (not blocking; before 1b-i). The failure's translation has no place in `DoorFailure`.** `DoorFailure` is a door's raw failure (its
code, kind word, status, words and where: `Agents/AgentErrors.cs:41-52`), which `AgentErrors.Read` classifies by tables (`:104-115`), a
limit's reset coming from a table's reading (`:58-59`). The layer's failure arrives classified, its reset in the restriction
(`Lyntai/Core/Agents/AgentFailure.cs:85-104`, `:127-135`). *The adapter turns it into a `DoorFailure`, which `AgentErrors.Read` reads, no
Daoris table behind it* (§5.3) leaves the reader nothing to read.
*Change:* say the shape: a `DoorFailure` of the layer's door (its kind word the layer's, a `Where` of its own) read by one four-row table
declared once in `Agents/Layer/`, the kinds one to one and `By` carried, its limit's reading built from the restriction's recovery with
D125's sixty minutes where none is stated; FOUNDATION1 1g's proof holds it.

**F10 (not blocking; before 1d). What Daoris keeps of the process: its stderr and its stop.** The runner yields stdout lines, stderr only as
a tail on a nonzero exit (`Lyntai/Core/Processes/IProcessRunner.cs:33-38`); Daoris's driven capture writes the harness's stderr into the
transcript beside its stdout (`Loop/Driver.cs:4273-4277`) and reads access from it (`Agents/AgentErrors.cs:33-34`). A stop is Daoris's kill
of the process tree through `SessionProcesses` (`Agents/SessionProcesses.cs:264-277`), after which the layer's turn ends on a nonzero exit
read as fatal (`Lyntai/Basic/CliAgentLoop.cs:47-53`). This answers part of what §14 leaves unmeasured.
*Change:* `LauncherRunner` pumps stderr into the transcript as today and registers the process with `SessionProcesses`; the access reading
of stderr becomes the layer's fault terminal's, each recorded sentence held both ways before 1d; `LayerRun` reads a stop requested before
the layer's terminal, as the executors read one today.

**F11 (not blocking; before 1c). 1c keeps the usage read's host half and states the read's own spawn table.** 1c deletes *the usage switch
(`HarnessRoster.cs:2295-2368`)*, which holds the host's rules around the read: an account that is a key is not read, the account's probe
lock is taken, one question per account across processes, and none is asked while a session of Daoris's runs on it
(`Agents/HarnessRoster.cs:2299-2305`; `Agents/ProbeLock.cs:8-19`). The layer's read starts the vendor's server, which may refresh the
account's token (`Lyntai/Basic/UsageAgentProvider.cs:12-17`): two at once on one account is what the lock prevents. And the read's spawn
differs from today's one command (`Agents/ClaudeUsage.cs:36-46`): a version probe before every read (`Lyntai/Basic/CliUsageSource.cs:77-82`
*(AG20)*), a fresh working directory under the operating system's temporary folder (`:84` *(AG20)*), the server's own arguments.
*Change:* 1c deletes the question and its readers and keeps the key skip, the probe lock and the busy check around the layer's read; its
proof adds the usage read's spawn table against today's.

**F12 (not blocking; for OPENROUTER1's placing). OPENROUTER1 before 1d holds, with three changes.** Building it on Daoris's transport first
is right: it needs no release while Claude Code's door is Daoris-run (§9.4). But:
- its key reaches the layer's door through the per-start registration's environment (§5.5), so its move needs no AG17, which is an account
  that is a key and the variables a provider reads (`Lyntai/TASKS.md:362-369` *(AG20)*), s-vi's need; 1d's wait on R-OR (§9.1 item 5)
  goes;
- its door's recorded sentences (a gateway's limit, credit and refusal as Claude Code prints them, first measured on the owner's key, §11
  item 4) must be read by the layer before it moves (§5.3), and its installed proof spends the owner's key; neither should hold Claude
  Code's own move, so its move is a row of its own after 1d, with its proof in §11 item 3;
- built before 1d, its door is on the contract, Claude Code's spawn and native dialect by reference with its own environment and tables, so
  its move is a binding and nothing else.

**F13 (not blocking; before LQ12 is sent). LQ12 names three of the five operations stage B needs.** The reducer is already what LQ12 asks,
in kind: pure functions over a set of holds, the clock and an event, owning no clock, store or lock (`Lyntai/Core/Agents/AgentPoolReducer.cs:17-27`
*(AG20)*), only internal. Stage B needs its whole event set over a snapshot the host holds: a failure observed as a hold, restated at a
higher revision when its attempt fails; admission over a seat's covering scopes, a door's allowance among them; a probe lease with its
token; a report by that token resolving the lease; and a clear by id and revision (`AgentAccountPool.cs:17-31`, `:105-140` *(AG20)*).
LQ12 names admission, a revision-checked release and a lease. AGENTINFRA1 also binds a restriction to its credential identity and epoch,
so that an observation for an old credential never releases its replacement (`docs/2026-10-27-provider-infrastructure-design.md:171-174`,
`:183-185`), which the layer's hold does not carry (`AgentPoolReducer.cs:7-9` *(AG20)*).
*Change:* LQ12 states the five operations, each pure over a snapshot whose holds carry their revisions, so a host's lock or
compare-and-set sees a concurrent writer, the stored form the host's; and §5.2 either asks the credential epoch as part of a hold's subject
or names it the host's envelope.

**F14 (not blocking). A newer build's isolated home does run several processes; cite the lines that show it.** The window's limit read and a
terminal's `daoris agent` verbs write `cooling.json` on one home, under a lock taken across processes (`Agents/AccountCooling.cs:236-246`;
`Tests/Agents/CoolingTwinTests.cs:8-10`: the screen's *Try now* and the terminal's `profile ready`), and a status question is locked across
processes because the terminal's `agent list` asks the same accounts (`Agents/ProbeLock.cs:14-16`). AGENTINFRA1's isolated home admits no
legacy connector or competing loop (`docs/2026-10-27-provider-infrastructure-design.md:273-277`), not the same build's terminal verbs. So
the layer's one-process pool (`AgentAccountPool.cs:32-34` *(AG20)*) cannot be stage B's store, and LQ12 is needed.
*Change:* §3.2 and LQ12's evidence cite these lines; FOUNDATION1 §19.1 (`docs/2026-10-25-foundation-design.md:2731`) is about two shells on
one home and supports the claim less directly.

**F15 (not blocking; before 1e). dsh's move waits for releases dsh does not use.** 1e-i waits on R-e, which carries AG11, AG12, AG19, LQ4's
and LQ7's protocol halves and LQ8 (§7.2). dsh's posture is an environment variable set at boot (`Agents/Adapters.cs:1985-2014`), its
escalations refused by construction (D52; `:1989-1994`), which is the layer's read-only answer to every permission request
(`Lyntai/Basic/Acp/AcpAgentReader.cs:192-202`; `Acp/AcpAgentDoor.cs:29` *(AG20)*), and Daoris never turns its model (`:1962-1965`,
`:1970`). Its home is read as a profile under `$DSH_HOME/profiles` by the adapter's own premise (`:1957-1960`), where §0.3 calls the read
unmeasured.
*Change:* 1e-i takes a label of its own (LQ3's protocol half, with LQ7's if its children are read), its registration's command carrying
`--profile acp` and its environment the posture (`:2004-2014`), so the first protocol move is not held for Claude Code's and Codex's needs;
§0.3 cites the premise and says whether it was measured.

**F16 (not blocking; before 1b-i). The 60 cases' rows are another proposal's.** §6.2 has X §14's table account for each of the 60 cases by
name, *its M2 lines as each door's move*, but X's lines rest on M2's decoder over Q-F (`docs/2026-10-30-agent-lyntai-proposal-codex.md:449`,
`:463`, `:465`), which this design does not ask (§1.1 row 1), so a builder of 1b-i cannot write the pending files from it. And §5.1 and
§1.1 row 3 cite `CliAgentLoop.cs:23` for the prompt on stdin, the class's line; the prompt is `CliAgentLoop.cs:68-75` with
`ClaudeAgentArgs.cs:6-7`.
*Change:* the design carries its own table of the 60 cases, each with its closing row (this design's or D213's) and the request its why
names; the citations re-pointed.

16 findings, 6 blocking (F1 to F6).

**Verdict: another round.** The shape holds: the layer is what a door is and Daoris what is done with doors, the contract an adapter, the
pool never the walk, D22 standing for what lands, the native door split by run kind. But the six blocking findings re-cut rows 1b and 1d
and the rule every move rests on, and 1c to 1f are ⚖, so both makers read what replaces them. Round 2 must: order row m's executors over
`AgentRun` before 1b-ii and the moves, and name 1b-i's sink until row l (F1); give the parity rule a mechanism inside one commit over
`AgentRun` on both paths (F2); bind a door per run kind (F3); keep D213 point 10 for every move, 1d's differences becoming the stdin
correction, the denied tools' ⚖ correction and the server file decided now (F4); state `LauncherRunner`'s clocks (F5); and ask now for the
CLI turn's rules over a host's reader, `alt-stub`'s native door pending on it (F6). Before the rows they name: F8, F9 and F16 before 1b-i,
F11 before 1c, F7 and F10 before 1d, F15 before 1e, F13 before LQ12 is sent, F12 in OPENROUTER1's placing and F14 in §3.2's evidence.

## Codex, round 1

*Codex's read of round 1, read-only, at `460b6d47c`, written without seeing the Opus maker's; kept verbatim.*

**Verdict: another round.** The ownership split holds, but the parity mechanism, usage move and failure boundary need changes before a maker can build them reliably.

Read at Daoris `460b6d47c`, with provider sources unchanged from the design’s `23cc27254`; Lyntai `9a41435c`, plus AG20 at `2fb2dc7e` and its unchanged source replay on `daoris/s-d553ab68`. I checked the design citations relied on below against their targets. Nothing changed; no builds, tests or agent processes ran.

Paths use the design’s aliases: `Driver/` and `Tests/` denote the desktop driver projects; `Lyntai/Core/` and `Lyntai/Basic/` denote its two library projects. **Design** means `docs/2026-10-30-agent-lyntai-design.md`.

1. **Blocking — joining the fourth view does not establish the claimed parity.**  
   Design `:517–520` assumes the registry-over-adapters view remains an independent old implementation. It is reconstructed from the current `AdapterSet.Built()` (`Tests/Agents/ProviderConformance.cs:99–113`), which already obtains registered providers through `LegacyDoor.Over(Providers.Built())` (`Driver/Agents/Adapters.cs:2543–2547`). After binding a door, both views can reach the replacement and regress together. Moreover, pending verdicts accept any exception (`ProviderConformance.cs:290–308`); matching “pending” outcomes need not mean matching failures. The live cases apply only to stand-ins (`ProviderConformanceLive.cs:59–61`), not every production provider.

   **Change:** make the preceding implementation or its captured observable results an explicit comparison oracle. Preserve the case, door, applicability and run-kind matrix; compare outputs and records as well as verdicts. Plant a regression confined to the replacement and demonstrate that the comparison fails. Keep the fourth view, but remove the claim that its pending rule proves migration parity by itself.

2. **Blocking — usage parity requires substantially more than LQ2’s version policy.**  
   Design `:131`, `:599` and `:660–668` describe the usage source as needing only a version-policy answer. Today’s readers retain information its result cannot express. Claude keeps scoped weeks, server limit rows, partial observations, decimal precision and reset provenance (`Driver/Agents/ClaudeUsage.cs:109–163`; `Tests/Agents/ClaudeUsageTests.cs:149–179`). Codex keeps other limit scopes and fractional percentages (`Driver/Agents/CodexUsage.cs:169–200`; `Tests/Agents/CodexUsageTests.cs:140–155`).

   Lyntai exposes an integer percentage and no scope or precision on `AgentAllowanceWindow` (`Lyntai/Core/Agents/AgentUsageSource.cs:20–27`). Its Claude reader examines only two windows (`Lyntai/Basic/ClaudeCli/ClaudeUsageDialect.cs:70–94`); its Codex reader examines only `rateLimits`, requires integer use, and omits `rateLimitsByLimitId` (`CodexCli/CodexUsageDialect.cs:68–93`). Daoris writes the richer history before updating its walk’s windows (`Driver/Agents/HarnessRoster.cs:2339–2358`).

   **Change:** request sufficient neutral allowance observations, retaining scope, nullable values and precision. Keep Daoris’s naming and inferred-reset policy in the adapter. Require the existing history fixtures to compare completely before deleting either reader. Mapping the two headline windows is insufficient.

3. **Blocking — LQ2 passes the generic-library test as a configurable validation seam, but its present acceptance condition is ambiguous.**  
   Design `:599` permits “a statement it can act on.” Lyntai already provides precisely that: `VersionNotValidated`, without starting the server (`Lyntai/Basic/CliUsageSource.cs:75–80`; `Lyntai/Core/Agents/AgentUsageSource.cs:54–55`). That answer does not preserve Daoris’s existing readings after an agent update. Conversely, choosing an arbitrary executable version cannot establish that its response is understood. D222 explicitly requires validation per vendor version (`Lyntai/docs/DECISIONS.md:6353–6359`).

   **Change:** distinguish the library’s supported claim from a host’s explicitly configured validation evidence or probing policy, preserving today’s conservative default. This fits Lyntai’s seams and deployment-policy rules (`Lyntai/.agents/knowledge/generic-library.md:49–61`). State what evidence closes R-c; returning today’s unknown reason cannot alone close it.

   I would retain **usage before standalone failure decoding**, subject to findings 2 and 3. Avoiding a new public decoder seam remains a reasonable advantage. The source does not support the stronger claim that usage is the smaller move merely because its interface is public; its relative implementation cost remains **unmeasured**.

4. **Blocking — the normalized failure has no specified route through the existing reader.**  
   Design `:397–404` converts an already classified `AgentFailure` into `DoorFailure`, then asks `AgentErrors.Read` to classify it without Daoris tables. But `DoorFailure` carries raw code, kind-word, status, words and phase, with no normalized result, restriction or delivery (`Driver/Agents/AgentErrors.cs:47`). `AgentErrors.Read` interprets those fields through tables and defaults to fatal (`:133–204`). The two enums naming equivalent categories does not provide that missing route.

   **Change:** specify a trusted ingress for an already normalized outcome, preserving its named kind, `By`, recovery and conservative delivery reading without classifying its words again. Keep raw legacy failures on the existing reader. Require all four kinds, stated and unknown recovery, and harness-source negative cases through both paths. This is adapter work, not a reason to reinstate Q-F.

5. **Blocking — `UsagePart` crosses the boundary the design itself enforces.**  
   Design `:289` says the contract’s `UsagePart` holds `IAgentUsageSource`; `:492–505` forbids Lyntai symbols outside `Agents/Layer/` and provider folders. `UsagePart` lives in `Agents/Contract/ProviderParts.cs:90` and is consumed through host records. Implementing that mapping literally fails the proposed structure rule.

   **Change:** keep a Daoris-owned usage port or opaque host handle in the contract. Its implementation in `Agents/Layer/` owns the library source and translates its result. Specify its replacement for `IUsageQuestion` before retiring that interface.

6. **Blocking for stage B — LQ12 needs generation-safe transitions, beyond an atomic store and revision equality.**  
   The general request at Design `:609` is right: a planner needs restriction evaluation without another selector. Exposing the current reducer is insufficient. `Hold` derives a stable id from scope and cause and restarts its revision at 1 after removal (`Lyntai/Core/Agents/AgentPoolReducer.cs:156–161`). An old clear for revision 1 can therefore match a later, distinct restriction recreated at revision 1 (`:166–170`). Separately, success checks the hold revision without checking ownership of its current lease (`:146–151`); leasing changes the token without advancing that revision (`:125–131`).

   **Change:** request generation-safe restriction identity and lease ownership, with admission and reservation committed against the complete applicable state. Include removal/recreation followed by stale clear, lease expiry followed by takeover and late completion, and a new covering hold inserted during admission. These are source-derived counterexamples, **not executed measurements**.

   An isolated home does **not** establish one writer. AGENTINFRA1 excludes legacy clients and competing loops (`docs/2026-10-27-provider-infrastructure-design.md:273–284`); it does not define a universal single-process storage guarantee. Existing probes explicitly coordinate terminal and desktop processes (`Driver/Agents/ProbeLock.cs:14–18`), and cooling holds a cross-process lock (`AccountCooling.cs:238–247`). Keep LQ12’s multiprocess requirement. The actual isolated runtime’s writer inventory remains **unmeasured**.

7. **Non-blocking — re-pointing pending lines is better than retaining abandoned row identifiers.**  
   I accept Design `:521–523` over my proposal’s original-row rule. The reader validates `row` against a closed list (`Tests/Agents/ProviderConformance.cs:240–245`, `:359–374`); retaining a row this design no longer builds misstates the closing obligation. Existing native and protocol frame cases explicitly name k and i (`Tests/fixtures/providers/claude-code/pending.json:23–26`).

   **Change:** provide the concrete replacement table, including driven native versus conversation cases. Split wildcard door entries where their closing rows differ. Preserve the former obligation in the recorded amendment, while the active line names the row that now closes it. Re-pointing must not enlarge the failing-case set or substitute for finding 1’s comparison.

8. **Non-blocking — D22 needs no normative amendment for the chosen policy.**  
   D22 already requires released dependencies and directs awkward seams back to the library (`docs/decisions/D22.md:12–19`). Design `:568–578` keeps that rule for every landing and gate. Lyntai’s D235 describes its development-pack route; it does not change Daoris’s dependency policy (`Lyntai/docs/DECISIONS.md:6753–6767`, AG20).

   **Change:** remove the mandatory D22-note dependency from row 1a. Record exploratory-pack mechanics in the development procedure; an explanatory cross-reference in D22 is optional. Also avoid an automatically imported untracked override in ordinary project props: its mere presence must not let a normal gate consume the exploratory pack.

9. **Non-blocking — dsh first is defensible, but “fewest abilities” does not establish the lowest risk.**  
   Design `:134` has a sound product distinction: dsh refuses conversations. Its spawn nevertheless carries essential boot-time behavior: `--profile acp` and `DSH_PERMISSION_MODE=workspace-write` (`Driver/Agents/Adapters.cs:1983–2014`). Its home-variable and resume behavior remain explicitly unmeasured in Design `:806`. The claim that fewer sessions are exposed has no measured usage evidence here.

   **Change:** make dsh-first conditional on a complete recorded protocol corpus and preservation of those spawn facts. If Codex has the first complete comparison, move Codex first. Neither the maker’s daily use nor dsh’s smaller product surface should decide the order alone.

10. **Non-blocking — keep the native run-kind split and per-start composition, with explicit lifetime proof.**  
    The split survives the requested source check. Driven native words wait until the process ends and continuation resumes the conversation; they do not require that process’s held stdin (`Driver/Loop/Driver.cs:3815–3838`). Native start and resume pass their prompt in argv (`Driver/Agents/Adapters.cs:2188–2196`, `:2224–2232`), while chat uses framed stdin (`:2274–2286`). Moving driven starts first is reasonable.

    Private containers also isolate the registrations’ fixed environments: the CLI registration captures its environment in singleton factories (`Lyntai/Basic/ClaudeCli/ClaudeCliBuilderExtensions.cs:88–107`).

    **Change:** name the container’s disposal owner, and retain it for the lifetime of any future long-lived run. Add overlapping-start cases with distinct identities, accounts and cancellation, plus driven-word continuation across the move. Container cost and concurrent behavior remain **unmeasured**.

Package closure, offline restore, published layout, installed parity, real held-permission waiting and provider latency remain **unmeasured**. This read establishes source findings and required proofs only.

## The Opus maker, round 2

*The Opus maker's read of round 2 at Daoris `d53705dda` (round 2 merged into the batch line) and Lyntai `master` `9a41435c` with AG20
`2fb2dc7e` (read only, AG20's lines through `git show`); nothing run. Condensed by the steward from its hand-back in layout only: every
finding, citation and change is kept.*

**Result:** 10 findings, 4 blocking (O2-F1 to O2-F4). **Verdict: another round, kept narrow.** Round 3 need re-read only §5.3 to §5.5,
§6.3, §8, §9.2's m-i and §12's m-i, 1b-i and 1d rows.

### 0. Round-1 findings

All sixteen O1 findings taken; no blocking finding left open. The three changes (O1-F1, O1-F6, C1-8) hold. F1 and F6 open new findings
(O2-F2, O2-F6). C1-8: a pack is repository scratch, so it belongs under `_fixtures/`, not `local/`. F2 → O2-F1; F3 → O2-F3, O2-F9; F4 →
O2-F10; F5 and F8 → O2-F5 (the lifetime across resumes is wrong); F9 → O2-F3; F11 → O2-F8. F10: `ProcessRunException` is public
(`ProcessRunner.cs:48`). F13: binding the epoch into the subject key passes Lyntai's rule 7. F14: `AccountCooling.cs:236-246`,
`ProbeLock.cs:14-18` hold. F16: the pending files match §6.4.

### 1. Citations found wrong

- `HarnessRoster.cs:2299-2305` (§3.1 row e, §3.2, §9.2) is the method's head; the key skip, probe lock and busy check are `:2308-2313`.
- `HarnessRoster.cs:2321-2325` (LQ15, §9.2) is the command line; the `usage-read` folder is `:2326-2332`.
- §5.5's `DriverContinuations.cs:1190-1193` for "go-ons and resumes" are `GoOnAsync`'s: go-ons only.

### 2. §14's items

1. The oracle: its observation list is incomplete; rule 1 needs a seam the design does not name (O2-F1, O2-F7).
2. m-i: not with `AgentRun` as declared; the words' receipts belong to `DoorInput`, which is m-ii's (O2-F2).
3. A door bound per run kind: holds for the home variable; treat the account key the same; the field is needed under another name (O2-F9).
4. The ingress: one answer type over two entries is still D210's one reading, but `NormalizedFailure` lacks what cooling and the retry
   read (O2-F3).
5. 1s-i is rightly not ⚖; 1s-ii must run per mode (O2-F10).
6. LQ13, LQ14 and LQ15 pass Lyntai's rules 1, 5 and 7; LQ13 needs a clause (O2-F6); LQ15 carries the client's version beside its name.
   LQ2's closing condition is right (`CliUsageSource.cs:56`; turning it off also skips the probe, `:77-82`). LQ12's five operations are
   complete.
7. `IUsageRead` does not yet replace `IUsageQuestion` whole; 1c's table misses one difference (O2-F8).
8. The containers and the clock hold only with O2-F5.

### 3. Findings

**O2-F1 (blocking). The oracle's observations are incomplete, and "equal is the only pass" cannot hold on fields that name the path.** The
native capture hands each line to the lineage hook (`HarnessCapture.cs:153`), the harness's own words the ending reads (`:145`,
`:159-161`), the beside streams, the conversation id (`:169`), the account-window readings (`:189`) and the live console. Each run also
keeps the downloads it is handed (`DriverContinuations.cs:392`, read by `DownloadClearing.cs:398-415`), the watch's hearing stamps
(`Driver.cs:4292`), machine-log lines (`TransientRetries.cs:28-30`, `:43-46`), the service's account lines (`Driver.cs:3366-3380`) and the
weekly windows file (`HarnessRoster.cs:291-295`). Fields differ by construction: `AgentError.By` (`own:…` today, `layer:…` by §5.3), the
usage history's `Source` and `Reader` (`HarnessRoster.cs:2354`), the log's `by`. *Change:* list the observations by the capture's consumers
and the run's side effects; in 1b-i declare a fixed projection for the fields that name the path, each compared by a correspondence
recorded per door (the layer's entry against the Daoris entry it replaces); the projection never grows inside a move.

**O2-F2 (blocking). m-i cannot be written against `AgentRun` as declared, and its cut from m-ii is not clean.** `AgentRun` has eight
Task-returning operations (`AgentRun.cs:9-31`) and no run end, records, carriers or process. Today's executors use go-ons as a
`ProcessStartInfo` factory (`DriverContinuations.cs:1159-1182`), `KeepHandedAsync(info)` (`:392`), settings and servers handed onto the
start (`:1176-1177`), and the ending's `Transient`, `ModelRefused` and own words (`HarnessEnding.cs:49`; `Driver.cs:2954-2960`). Words lose
their receipts: `SendAsync(string)` has no id, while today's retry marks a word attempted before the wire and takes it off by its id once
written (`TransientRetry.cs:42-47`); D213 gives that to `DoorInput`, m-ii's. The proof drops `HandOverTests` (D213's row m names it) and
names no suite of the helpers', intakes', opinions' and partners' starts (`DriverHelpers.cs:1071`, `DriverIntakes.cs:342`,
`DriverOpinions.cs:265`, `DriverPartners.cs:701`). *Change:* m-i names what it adds to `AgentRun` (the run's end, its records until l, the
carriers' handing, the process for containment and the watch) and which of them l or m-ii later replace; either `DoorInput` moves into m-i,
or words stay off `AgentRun` until m-ii and the design says so; the proof adds `HandOverTests` and the four executors' suites; m-i re-points
`live.word-before-wire` to m-ii and adds m-i and m-ii to the closed row list (`ProviderConformance.cs:240-245`).

**O2-F3 (blocking). The ingress reaches neither cooling nor the retry as they read today.** Cooling is a second path: `AccountLimited` →
`HarnessRoster.Limited`, from seven callers each handing the sentence, read through Daoris's `Limits` table; a limit read by code is taken
only when `Stated: false`, its end replaced (`HarnessRoster.cs:279-280`), so a layer limit with a stated reset is dropped or read again, and
once 1f-i deletes the tables nothing reads it. The window is lost: `LimitSeen.Window` decides the weekly windows kept
(`HarnessRoster.cs:291-295`) and LIMITRESUME1b's not-lifted check (`Course/LimitResume.cs:88`); the layer's restriction has no window
(`AgentFailure.cs:100-104`, AG20), and its reader folds `rateLimitType` into `Cause` (`StreamJsonFailure.cs:88-94`). D125's margin, grace
and furthest bounds are unstated for the layer's instant (`AccountLimits.cs:82`, `:88`, `:94`). "Uncertain holds" changes the retry: today
a transient failure goes on in the same conversation whether or not a tool ran, and only words met before any update of the turn are held
uncertain and never resent (`TransientRetry.cs:110-125`), which is the layer's `NeverSent`; the layer's `Taken` and `Uncertain` are both
today's *taken*. *Change:* the ingress builds a whole `LimitSeen` applying D125's bounds over the layer's instant; cooling takes it through
a new entry that reads no sentence, each of the seven callers routed there for a bound kind; a request (new, or LQ14 widened) asks for the
restriction's window; delivery is mapped onto today's dispositions as above.

**O2-F4 (blocking for 1d's request set). The native driven door relies on readings the layer drops.** The live window frames (TOOL6c): the
`allowed` `rate_limit_info` frames feed the walk's windows (`StructuredOutput.cs:11-15`; `HarnessCapture.cs:182-193`), and the layer never
keeps them (`StreamJsonAgentReader.cs:55-62`). The harness's own words: lines outside the protocol (`HarnessCapture.cs:159`) are discarded
by the layer (`:37-40`). The refused model: D205's pipe exception (`Driver.cs:2954`; `HarnessCapture.cs:161`) is AG12's (Lyntai
`TASKS.md:320`), which R-d does not carry. *Change:* §5.4's table and §8 gain all three; R-d gains AG12 and a request for the standing
frames; the outside lines are kept from `LauncherRunner`'s own lines as the harness's words, never by a second frame decoder.

**O2-F5 (not blocking; before 1b-i). The containers and the clock.** A resume is a start of its own: `ContinueAsync` takes the walk's pick,
possibly another account (MSG1g; `DriverContinuations.cs:161-162`, `:176-178`), and prepares its own spawn (`:391`), possibly in a later
driver process; only MSG1b's go-ons run under the start's spawn (`:1191`), so a resume builds its own container. No clock on a turn is safe
only with the watch: the watch must see a layer turn as it does today (registered, stamped on every stdout line, the job's CPU read for
busy; `SessionWatch.cs:63-85`); the layer's reader drops lines, so `LauncherRunner` stamps the raw lines, not the layer's events.
`LauncherRunner` feeds `KeepHanded` from its spawn.

**O2-F6 (not blocking; before LQ13 is sent). As worded, LQ13 makes every clean `stub` run fatal.** A stream that ends with no terminal
becomes `no-terminal-event` Fatal even on exit 0 (`CliAgentLoop.cs:147-155`); `stub`'s plain lines have no terminal, and its success is its
exit (`Adapters.cs:1680-1683`). *Change:* LQ13 also asks that the host's reader be told when the stream ends and with which exit code, so it
can yield its terminal there.

**O2-F7 (not blocking; before 1b-i). The old path needs a seam the design does not name.** `DriverWatch` builds each look's `Driver` over
`AdapterSet.Built()` (`DriverWatch.cs:656-658`), which a test cannot inject; only the roster can be (`:77`). `ChatRunner` reads the
registry directly (`:1133`, `:1286`, `:1315`, `:1440`). *Change:* make the watch's adapter set injectable for the oracle, and say that each
static read on the path reads names alone.

**O2-F8 (not blocking; before 1c). 1c's table and its port.** A fifth spawn-table difference: today's reads wait for `initialize`'s answer
and give up on its error (`ClaudeUsage.cs:197-203`; `CodexUsage.cs:240-250`), while the layer writes every request at once
(`CliUsageSource.cs:133-134`, AG20). Codex's `clientInfo` version, "0" today against "1" (`CodexUsage.cs:244`; `CodexUsageDialect.cs:35`),
belongs in LQ15 beside the name. `IUsageRead` asks with the account alone, while today's read also uses the pinned command resolved against
the configuration (`HarnessRoster.cs:2322-2323`), the toolchain composition, the patience each `Kind` selects, and the `Source`, `Reader`
and `Plan` the history writes (`:2354`). *Change:* the port says where each of these comes from on the layer path.

**O2-F9 (not blocking; before 1b-i). Run-kind naming.** `provider.json`'s door `kind` already means native or protocol, in the same folder
and the same tool (`tools/stand-ins.mjs:32-39`); name the pending field `run`. The reader's closed field list, its twice-held rule and
`Unmatched` all read the new field. §2.3 rule 2 reads the account key from the descriptor for both kinds once one is bound, as it does the
home variable.

**O2-F10 (not blocking; before 1s-i and 1s-ii). The proof cases.** 1s-i: add a non-ASCII prompt; argv carries UTF-16, but the driven spawn
sets no stdin encoding (`Adapters.cs:1536-1541`), where the usage read sets BOM-less UTF-8 (`CodexUsage.cs:303-306`); whether the launcher
and shell pass it intact is unmeasured. 1s-ii: measure each tool under each of the three driven modes and on a resume, since a tool refused
under `default` may run under another mode.

**Still unmeasured:** stdin encoding through the launcher; what the three tools do per mode; whether
`Microsoft.Extensions.DependencyInjection` already comes in transitively through `Microsoft.Extensions.Http` (if not, 1a pins it so that
1a's closure proof covers it).

## Codex, round 2

*Codex's read of round 2, read-only, at `d53705dda`, written without seeing the Opus maker's; kept verbatim, its headings one level down.*

Read at Daoris `d53705dda`, Lyntai `9a41435c`, and AG20 `2fb2dc7e`, also present as `0cbd84c1` on `daoris/s-d553ab68`. Nothing changed; no builds, tests or agent processes ran.

References: **P** = `docs/2026-10-30-agent-lyntai-design.md`; **D/** = `src/Daoris.Desktop/Daoris.Desktop.Driver/`; **T/** = `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/`; **L/Core/** and **L/Basic/** = Lyntai’s `src/Lyntai.Core/` and `src/Lyntai.Providers.Basic/`. References below use the current checkout unless marked **AG20**. I checked the cited targets I rely on; finding C2-6 records inaccurate AG20 document references.

### 0. My round-1 findings against §16

| Finding | Disposition |
|---|---|
| **C1-1 — independent parity oracle** | **Open.** P:674–692 supplies independent paths and compares observations, addressing the shared-registry defect. Its literal equality rule still cannot compare the different provenance and identities it deliberately introduces; C2-1 below. |
| **C1-2 — complete allowance observations** | **Closed.** P:816 requests scope, missing values, precision, partial windows, server rows and stated-reset provenance. P:894–895 requires complete history-fixture comparison before deleting either reader. This covers the information preserved by `D/Agents/ClaudeUsage.cs:109–163` and `D/Agents/CodexUsage.cs:169–200`. |
| **C1-3 — LQ2’s closing condition** | **Closed.** P:600–606 distinguishes library validation from explicitly configured host policy, keeps validated-only as the default, and requires the pinned versions’ recorded answers to retain today’s readings. Returning `VersionNotValidated` alone cannot close it. |
| **C1-4 — normalized failure ingress** | **Open.** P:440–456 correctly separates raw classification from trusted ingress. However, its `Where = layer` loses a phase existing policy reads, and the downstream delivery carrier remains unspecified; C2-2 below. |
| **C1-5 — usage boundary** | **Closed.** P:320 defines the Daoris-owned `IUsageRead`, its ask and answer, and both implementations. P:627–628 explicitly rejects the former cross-boundary mapping. |
| **C1-6 — generation-safe restriction transitions** | **Closed as requested.** P:814 requires generation-safe identity, lease ownership and reservation against complete applicable state; P:434–436 names the counterexamples. P:421–427 keeps credential epochs in the host’s subject binding. C2-4 identifies a separate missing recovery operation. |
| **C1-7 — concrete pending-row replacement** | **Closed.** P:649–659 adds run kind and the twin reader; P:708–728 supplies the replacement table, including the native split and wildcard splitting. The current pending files support that inventory. |
| **C1-8 — D22 and exploratory packs** | **Closed with a change I accept.** P:767–783 removes the normative amendment and row dependency, requires an explicit property, and makes gates refuse it. The explanatory cross-reference in D22 is acceptable. |
| **C1-9 — evidence before dsh-first** | **Closed.** P:152 and P:845–846 condition ordering on the complete corpus and preserved spawn facts. The narrower historical `DSH_HOME` measurement is supported by `docs/2026-09-22-acp3-probe-evidence.md:22`, :103–106; later versions and resume remain unmeasured. |
| **C1-10 — container ownership and continuation proof** | **Closed at design level.** P:526–535 names the disposal owner, lifetime and overlapping-start cases; P:399 requires continuation of a record started before the move. Runtime lifetime, cost and concurrency remain unmeasured. |

### Verdict: another round

The package direction and ownership split hold. The remaining blocking questions concern the oracle’s comparison contract, failure policy across the ingress, and recovery operations required by stage B.

### 1. New findings

#### C2-1 — Blocking: the oracle needs a defined equivalence relation

P:681–685 compares failure sources, complete spawns and records, with equality as the only pass and no allowance list. Yet the design deliberately changes an old table source into `layer:` plus the library’s `By` (P:447–451). Those strings cannot be equal: today’s source can be `owner:kind:rate_limit` (`D/Agents/AgentErrors.cs:164–169`), while the library supplies entries such as `claude.assistant.error=rate_limit` (`L/Basic/ClaudeCli/StreamJsonFailure.cs:112–118`).

The initial comparison also pairs **different doors**: `alt-stub` against `acp-stub` (P:694–696), whose provider identities, home variables and command arguments intentionally differ. Independently generated times and identifiers add another source of unequal observations. The usage source itself reads its clock when producing the answer (`L/Basic/CliUsageSource.cs:89–90`).

**Change:** define the comparison projection before implementing the oracle. Fix clocks and generated identities where possible; otherwise name explicit identity correspondences. Compare semantic outcomes and ordering exactly, while checking each path’s provenance against its own expected source. These correspondences must not permit dropped readings, changed recovery or reordered writes. Retain the replacement-only sabotage cases.

This is a source-derived construction problem, not an executed failure.

#### C2-2 — Blocking: the trusted ingress loses policy-bearing phase and does not finish the result contract

P:450–451 assigns the logged `DoorFailure` a phase of `layer`. Existing consumers use that field for policy. A refused resume’s open applies account cooling or access handling only when `Failure.Where == DoorWhere.Open` (`D/Loop/DriverContinuations.cs:668–675`). The transport currently preserves that distinction explicitly (`D/Agents/Acp.cs:1497–1512`).

`By` is also more than a log label today: `OpeningAccount` distinguishes refused credentials from sign-in failures by its suffix (`D/Loop/DriverContinuations.cs:617–630`). Replacing it with arbitrary library provenance does not preserve those decisions.

Finally, the existing `AgentError` carries kind, raw failure, source and optional `LimitSeen`, but no delivery (`D/Agents/AgentErrors.cs:58–66`). P:449 says delivery is translated, without specifying how it leaves `NormalizedFailure` and reaches retry policy.

**Change:** define the complete Daoris outcome crossing this boundary: semantic phase, access disposition, recovery and delivery, with provenance stored separately. Keep open versus prompt failures distinguishable; migrate consumers from interpreting provenance strings to typed decisions. Where the library cannot supply a required distinction, name a request and make the affected move wait. Add refused-open account-policy cases to the ingress proof.

#### C2-3 — Blocking: “Uncertain holds” needs separation from D210’s same-conversation continuation

P:461–465 says `Taken` resumes and `Uncertain` holds. The layer stamps a failed turn uncertain whenever a tool call or result occurred (`L/Basic/CliAgentLoop.cs:34–42`, :129–145).

Today, a transient prompt failure enters the bounded episode without that exclusion (`D/Agents/Acp.cs:1476–1492`). Its next attempt sends a **new go-on**, rather than repeating the original prompt (`:1846–1852`, :1888–1890). That go-on explicitly tells the agent to check whether an interrupted effect already happened (`D/Agents/TransientRetry.cs:67–75`).

As written, the new rule can therefore stop a continuation that today proceeds after tool activity, inside a move that promises no behavioral difference.

**Change:** distinguish disposition of the original words from permission to send D210’s new continuation in the same conversation. Preserve today’s guarded go-on policy while forbidding replay of uncertain original words. If holding the whole run is intended instead, make it a separately reviewed correction before the move. Add a tool-call → transient failure → go-on comparison case.

#### C2-4 — Blocking for stage B: LQ12’s five operations omit independent positive evidence

P:814 now covers failure observation, admission/reservation, leasing, reporting an attempt by token, and revision-bound clear. None explicitly represents a positive status or credit observation obtained **without an admitted agent attempt**.

The accepted infrastructure contract requires an authoritative newer report to discharge the matching restriction, and permits a positive credit check to resolve `OwnerAction(refill)` (`docs/2026-10-27-provider-infrastructure-design.md:176–185`). A one-attempt clear is deliberately different.

Lyntai already has a separate `Lift` operation, but it handles only stated-time recoveries and explicitly excludes owner-action and unknown recovery (`L/Core/Agents/AgentPoolReducer.cs:173–190`). Merely exposing that operation would not satisfy the accepted contract either.

**Change:** extend LQ12 with an evidence-observation operation over host-owned snapshots, identifying the exact restriction generation, revision, subject/binding and credential epoch. Specify which positive evidence can discharge each recovery kind, independently of an attempt token. Keep explicit one-attempt release separate. Prove stale, wrong-scope and old-epoch observations cannot discharge the replacement.

#### C2-5 — Blocking for 1c: its exchange table misses Codex’s initialization dependency

P:901–908 inventories the version probe, working directory, Claude’s `-p` and Codex’s client name. Another difference already exists:

- Daoris sends `initialize`, **awaits its answer**, then sends `initialized` and the allowance request (`D/Agents/CodexUsage.cs:241–250`).
- Lyntai queues every dialect request before reading any answer (`L/Basic/CliUsageSource.cs:129–142`, **AG20**); the three requests are listed at `L/Basic/CodexCli/CodexUsageDialect.cs:33–38`.

Thus equal outgoing lines alone do not establish equal exchange behavior. LQ15 currently asks for working directory and client identity, not this dependency.

**Change:** add this difference to 1c’s table and request a response-dependent usage exchange in the library, included in R-c’s closing condition. Use a fake server that withholds initialization’s reply and detects premature later messages. Keep this sequencing inside the library; the runner should not parse Codex’s protocol.

Whether the installed server tolerates eager sending is **unmeasured**.

#### C2-6 — Non-blocking: several AG20 document citations point to the wrong lines

P:76 cites D234’s standing at `Lyntai/docs/DECISIONS.md:6711–6712`; P:770 cites D235 at :6753–6767. At the named AG20 commit, D234 starts at **:6650**, its standing is **:6652–6654**, and D235 starts at **:6688**, with its pack procedure at **:6693–6708**. The document ends at :6711.

The contracts hold, but these references cannot substantiate them as written. P:91’s claimed correction of the proposal’s earlier line numbers is itself incorrect for that commit.

**Change:** repair the AG20 document references against the exact cited commit, keeping them distinct from master’s line numbers.

### 2. The other priority checks

- **m-i against row m:** the split is defensible. P:269 retains native retry, `DoorInput`, conversation-home handling and consolidation in m-ii; P:857–865 gives m-i the transport wrapper and executor cut first. D213’s original row m remains the broader obligation (`docs/2026-10-26-agent-provider-design.md:1137`). No implementation feasibility proof has run.
- **Binding per run kind:** P:311 preserves the native conversation’s spawn and framing while driven work is bound. That matches the actual distinction in `D/Agents/Adapters.cs:2188–2196`, :2224–2232 and :2274–2286. Reading the home variable once through the descriptor is coherent.
- **1s-i and 1s-ii:** I accept their separation before 1d. Moving the same prompt to stdin is appropriately distinct from narrowing tools. The latter remains subject to the measurement and both-makers decision at P:922–926. Neither measurement has run here.
- **LQ13–LQ15:** each passes Lyntai’s generic-library test. They request reusable turn machinery, allowance observations and host-controlled server context. LQ2’s closing condition is now sufficient; LQ12 and 1c need the additions above.
- **`IUsageRead`:** its ownership and answer shape resolve the boundary defect. The retained key skip, probe lock and busy-account check correspond to `D/Agents/HarnessRoster.cs:2307–2313` at this checkout.
- **Containers and clocks:** separate registry and run containers, with the run owning disposal, are coherent. Declining the runner’s inactivity bound preserves Daoris’s session policy; cancellation, abandonment cleanup and explicit stops must still work. P:504–509 and :530–535 name the relevant proofs. A partial or failed stdin write must remain uncertain unless the runner can prove no write began.

Package closure, offline restore, publish layout, container cost and concurrency, installed parity, latency, actual isolated-runtime writers and real held-permission waiting remain **unmeasured**. This read establishes source findings and required proofs only.
