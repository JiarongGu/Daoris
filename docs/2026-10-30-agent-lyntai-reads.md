# AGENTLYNTAI1: the reads of rounds 1, 2, 3, 4 and 5 (D191)

The two makers' reads of each round of `docs/2026-10-30-agent-lyntai-design.md`, each written read-only and apart, neither seeing the other's
before its own was written: for each round the Opus maker's first, and Codex's beside it, added verbatim when it lands. Round 2's reads
follow round 1's, round 3's follow round 2's, round 4's follow round 3's, and round 5's, the acceptance reads, follow round 4's, below;
round 4's both read at Daoris `42304cfea`, round 5's Opus maker at `fbc336353` and Codex at the *Read at* offsets, both accepting (the Opus maker after four named changes, none
blocking, taken in the design's §20). Each finding names the line it rests
on in Daoris or in Lyntai, in the design's aliases: a bare path is under the driver library, `Tests/` is its tests, `Lyntai/Core/` and
`Lyntai/Basic/` are Lyntai's `src/Lyntai.Core/` and `src/Lyntai.Providers.Basic/`, and a Lyntai line marked *(AG20)* is its commit
`2fb2dc7e`'s, any other its `master` `9a41435c`'s in rounds 1 and 2; round 3's reads cite `master` at `bbbe285b`, which merged AG20. The
steward checks each finding against the code before it is taken (D191 point 2), and the next round answers every finding of both, one row
each.

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

## The Opus maker, round 3

*The Opus maker's read of round 3, read-only, at Daoris `34c7168d8` (the batch-1030q line; its `src/` identical to `9405b458f`) and Lyntai
through `git show` at `master` `bbbe285b`, at `9a41435c` and at AG20's `2fb2dc7e`/`0cbd84c1`; nothing run. Kept as written, its headings
one level down.*

**Result:** 11 findings, 6 blocking (O3-F1 to O3-F6). **Verdict: another round, kept narrow.** Round 4 need re-read only §4.1's `AgentRun` row, §5.3 with §5.2's entry, §5.4's tables, §8 (LQ11, LQ16, one new request, LQ3's delivery half), §9.1 and §9.2's m-i. Nothing found touches 1a.

### 0. Round-2 findings against §17

| Finding | Round 3 | Shown at |
|---|---|---|
| §0 (where packs go) | closed | design :894 |
| §1 (three citations) | closed; `HarnessRoster` moved between `f638a07c1` (`:2300`, `:2323`) and `d53705dda` (`:2308`, `:2331`), as §17.3 says; `GoOnAsync` taken | :1432 |
| F1 the oracle | closed, the relation needs repairs (O3-F7, O3-F8) | :772-798 |
| F2 m-i | partly: four members and `HandOverTests` in; open: the conversation runner's words, and brainstorm passes (O3-F5) | :334, :983-988, :1186 |
| F3 the ingress | closed for limits only; access and a refused model still have second readings (O3-F1, O3-F2); on the native door the delivery mapping is wrong (O3-F3) | :432-440, :485-533 |
| F4 (with a change) | the fold into LQ16 accepted: both halves come from the same `rate_limit_info` frame (`StreamJsonFailure.cs:21-35`; `StructuredOutput.cs:167-169`); LQ16 must ask for the frame "as stated" (O3-F9), and the readings list is still incomplete (O3-F6) | :555-561, :938 |
| F5 containers and clock | closed | :586-590, :614-619 |
| F6 LQ13 | closed | :935 |
| F7 the seam | closed | :765-768, :987 |
| F8 1c's port | closed | :338, :1039-1045 |
| F9 the `run` field | closed (`tools/stand-ins.mjs:32-40`; `ProviderConformance.cs:312`, `:370`) | :736-738, :246 |
| F10 the proof cases | closed | :1058-1067 |

**C2-2 (with a change), tested.** Widening LQ11 passes Lyntai's rules 1 and 7. The phase and `By` claims hold: the only reads of `Where` are `AgentErrors.cs:146` and `DriverContinuations.cs:672`; the only policy reads of `By` are `:617` and `:623`. The typed disposition has three values (refused; signed out by words; signed out by code: `DriverContinuations.cs:617-630`); LQ11 asks for two reasons. The third records how the failure was read, not why; its effect differs from sign-in only through the hold for the tool's own sign-in (`RefusedAccounts.cs:26-29`). The design should say which layer reason maps to it. The same change misses the conclusion's readers (O3-F1).

**C2-6, declined: the decline holds.** At `2fb2dc7e`, `0cbd84c1` and now `master`, `docs/DECISIONS.md` has 6770 lines: D234 at `:6709`, standing `:6711-6713`; D235 at `:6747`, standing `:6749-6750`, procedure `:6752-6763`. `9a41435c` has 6704 lines. Codex's numbers match neither.

### 1. Citations found wrong

- §5.2's "seven callers" lists eight places; `Driver.cs:3341` is `AccountLimited` itself. The seven: `Driver.cs:2957`, `DriverContinuations.cs:612` and `:761`, `DriverHelpers.cs:1071`, `DriverIntakes.cs:342`, `DriverOpinions.cs:265`, `DriverPartners.cs:701`.
- §5.3 names three `DoorWhere` values; there are four: `Result` is the pipe door's (`AgentErrors.cs:30-31`).
- `LyntaiBuilder.cs:559` (`AddAgentRetry`) holds only at `9a41435c`; it is `:593` at AG20 and `master`. With `master` moved, "an unmarked line is master's" no longer names one commit (O3-F11).

Every other line relied on holds, including the capture's `:145-196`, `AgentRun.cs:9-31`, the `HarnessRoster` lines, `AccountLimits.cs:82`/`:88`/`:94`, `CliAgentLoop.cs:34-43`/`:129-145`, `CliUsageSource.cs:84`/`:90`/`:133-134`, `StreamJsonAgentReader.cs:37-40`/`:55-62`.

### 2. §14's items

1. The oracle: the observation list is complete by its own method but lacks the launcher's containment arguments (`AgentStarts.cs:30-31`); the one-to-one correspondence cannot be satisfied as written; two never-projected fields come from different sources on the two paths (O3-F7, O3-F8); no correspondence can hide a dropped reading, since semantic fields compare exactly.
2. m-i: the four members cover the driven executors, not the conversation runner, whose whole job is words; brainstorm passes are missing from the executor list (O3-F5).
3. Ingress and cooling: one reading per path holds for limits only (O3-F1); D125's bounds can be one function, but Grace applies only to a dated moment (`AccountLimits.cs:218-271`), so the design must say an instant counts as dated (O3-F8); the phase map is wrong for the native door (O3-F3).
4. Delivery: native door, the mapping would add D210's episode where today there is none (O3-F3); protocol door, the layer's stamp is coarser than today's per-prompt reading (O3-F4).
5. 1d's waits: AG12, LQ16 and LQ11's access half each pass the generic-library test; LQ16 keeps the window name as the vendor's opaque string (rule 3). Native readings still missing: denials, the context window, the split by model (O3-F6).
6. LQ12 and LQ15: both library needs; LQ15's order is a protocol fact and in practice a defect of the library's own; LQ12's table is narrower than the infrastructure design's (O3-F10).

### 3. Findings

**O3-F1 (blocking; 1b-i's ingress, 1d). The conclusion still reads access and a refused model a second time.** Each of the six conclusion sites calls `AccountRefused`, `AccountSignedOut`, `ModelRefused` and `AccountLimited` (`Driver.cs:2950-2960`, `DriverContinuations.cs:756-763`, `DriverHelpers.cs:1069-1071`, `DriverIntakes.cs:338-342`, `DriverOpinions.cs:263-265`, `DriverPartners.cs:699-701`). `AccountRefused` matches the harness's own lines against the toolchain's `Refused` words (`Driver.cs:3422-3432`); `AccountSignedOut` re-reads the sign-in table (`RefusedAccounts.cs:17-20`); on a bound kind no such table exists, and 1f-i deletes them. On the native door today there is no `AgentError`: a retry is built only for protocol doors (`adapter.Wire == SessionWire.Acp`, `Driver.cs:3909`), so for that door these readers are the one reading. *Change:* each of the four readers gets a trusted entry as `Limited` does (typed disposition → `RefuseAccount` or signed out; AG12's restriction → `ModelRefused`; `LimitSeen` → `Limited`); each site routes a bound kind there; 1b-i's proof adds a first run's access failure on both paths, not only a resume's refused open.

**O3-F2 (blocking; the ingress's map, 1d). AG12's shape would read as a credit refusal.** AG12 makes a refused model `Fatal` with a restriction of subject `Model` and recovery `OwnerAction` (Lyntai `TASKS.md:321-324`); §5.3 maps "`OwnerAction` today's credit case" by recovery alone (:483-484). Today a refused model is no strike and no cooling (`Driver.cs:3473-3488`). *Change:* the map reads `Subject` and `Cause` first: `Model` → `HarnessEnding.ModelRefused`, never cooling; `Credit` → today's credit case.

**O3-F3 (blocking; 1d). The native door has no D210 episode and no open phase.** §5.3 says the go-on "is permitted after each of the three, as today, inside D210's one budget" (:530-531); on the native door "today" means no episode (the native retry is m-ii's). Stamping `Open` for "a resume" mislabels a native resume turn, which is a single process; today's native path stamps nothing. *Change:* on a native bound kind, a transient failure ends as it does today until m-ii's native retry lands; the phase is `Result`; a resume is never `Open`; 1d's proof holds this.

**O3-F4 (blocking for 1e). The layer stamps delivery per spawn; today's protocol door reads it per prompt.** Lyntai's loop counts every event, `SessionStarted` included (`CliAgentLoop.cs:126-140`); the ACP reader yields `SessionStarted` before it sends the prompt (`AcpAgentReader.cs:147-152`), so a prompt met by an error before any update reads `Taken`. Today the same case is unacknowledged and said `lost` (`Acp.cs:2649-2656`; `DriverContinuations.cs:768-774`). The oracle would fail 1e, and no request waits for it. *Change:* a request (LQ3's ACP half widened, or a new LQ) that a door stamps delivery from what its turn showed after the prompt was written; 1e waits on it.

**O3-F5 (blocking for m-i). The conversation runner speaks outside `AgentRun`.** It frames and writes words itself (`ChatRunner.cs:2304-2309`, via `SessionProcesses.Send`) and calls `AcpSession.SayAsync`/`PromptAsync` (`Acp.cs:2210-2251`); with words kept off `AgentRun`, it cannot speak through it. Brainstorm passes start agents too (`DriverBrainstorms.cs:760`, `BrainstormProcessTests`) and are not in the executor list. *Change:* m-i names these word paths as kept in the executor beside `AgentRun` until m-ii, and adds brainstorms and their suites; §9.1 orders m-ii before 1e, 1f-i and 1f-ii.

**O3-F6 (blocking for 1d's request set). Three native driven readings are still missing.** The layer reads none of `permission_denials`/`permission_denied`, `modelUsage` or `contextWindow` (a `git grep` over its Claude door and `Lyntai.Core/Agents` finds none). Permission denials (UNBLOCK5): a call ends `refused` with its reason (`StructuredOutput.cs:319-327`, `:372-408`). The context window behind the high-water mark (`:329-334`, `:435-445`). Tokens split by the models that answered, subagents and `<synthetic>` excluded (`:220-227`, `:349-363`); `UsageFinal` carries one model, from init (`AgentStreamEvent.cs:30`). *Change:* add these to §5.4's table and as a request R-d carries; unknown content blocks (`:262-270`) go with LQ7's diagnostic.

**O3-F7 (not blocking; before 1b-i). The relation's repairs.** (a) One-to-one cannot hold: Daoris's `own:words:limit` and `unread` each stand for several layer entries (`AgentErrors.cs:157`, `:204`; `StreamJsonFailure.cs:88-128`); make the correspondence a total function from layer entries to Daoris entries, and keep the plant pairing one layer entry with two Daoris entries. (b) `LimitSeen.Hit` has no layer source: it is written to the service's account lines (`AccountCooling.cs:408`, `:429`) and read by `IsWeekly` when `Window` is null (`HarnessRoster.cs:310-311`); name its source or project it. (c) A library clock no seam reaches decides a limit's end: the Claude reader is built with no clock (`ClaudeAgentSession.cs:101`; `StreamJsonAgentReader.cs:25`) and a sentence's reset is read against it; a limit's end is never projected, so a request should hand that reader the host's clock. (d) Add the containment arguments to the spawn's observation.

**O3-F8 (not blocking; before 1d). Two paths read two sources.** The layer reads a Claude limit's end and window from the rejected frame (`StreamJsonFailure.cs:21-35`, `:88-94`); today's path reads the sentence (`AgentErrors.cs:155-158`). Equality of the end and the window, never projected, holds only where the frame and the sentence agree to the minute: unmeasured. *Change:* measure it on recorded frames; where they differ, land a correction on today's path before 1d; say an instant counts as dated, so Grace applies.

**O3-F9 (not blocking; before LQ16 is sent). LQ16's second half must ask for the frame as stated.** Today's walk reads `unifiedWindows` with each window's `utilization` and precision, the standing, and the overage and credit state (`AccountReadings.cs:123-180`, `:310-314`); LQ16 lists these as LQ14 does, or the move blanks windows (round 2 found the same of the usage server's headline windows).

**O3-F10 (not blocking; before LQ12 is sent). LQ12's evidence table is narrower than the infrastructure design's.** That design lets a bounded status probe discharge an `Unknown` recovery (`docs/2026-10-27-provider-infrastructure-design.md:180`); LQ12 confines a newer report to "a stated recovery" (:934).

**O3-F11 (not blocking). AG20 has landed on Lyntai's `master`.** `bbbe285b` merges `0cbd84c1`, the layer's sources identical to `2fb2dc7e`'s; D234's standing text unchanged. Refresh §0.2 row 1, §4.2's "two answers", §11 item 2 and R-a's "if accepted"; re-base unmarked Lyntai lines on `bbbe285b`. Whether the merge counts as the owner's acceptance is Lyntai's to record: unmeasured.

**Still unmeasured:** whether the vendors' fields tell a refused credential from a signed-out account (LQ11's access half); whether the frame's `resetsAt` and the sentence agree (O3-F8); whether the installed servers tolerate the eager order; everything §14 already lists.

## Codex, round 3

*Codex's read of round 3, read-only, at `34c7168d8`, written without seeing the Opus maker's; kept verbatim, its headings one level down.*

Read at Daoris `34c7168d8` and Lyntai `bbbe285b` (`master`), including both named AG20 commits. I read §17 first and the sections §14 names whole. Nothing changed; no builds, tests or agent runs ran.

References: **P** = `docs/2026-10-30-agent-lyntai-design.md`; **D/** = `src/Daoris.Desktop/Daoris.Desktop.Driver/`; **T/** = its sibling `Daoris.Desktop.Driver.Tests/`; **L/Core/** and **L/Basic/** = Lyntai’s `src/Lyntai.Core/` and `src/Lyntai.Providers.Basic/`. Cited source targets were checked. Runtime counterexamples below are source-derived, not executed.

### 0. My round-2 findings against §17

| Finding | Disposition |
|---|---|
| **C2-1 — oracle equivalence** | **Open.** P:776–805 supplies the observation list, projection, clock and identity handling, expected provenance and sabotage. Its required one-to-one source correspondence cannot represent the readers’ actual relationship; C3-1. |
| **C2-2 — complete trusted ingress** | **Open.** I accept widening LQ11 instead of adding another request. P:486–503 restores phase and adds typed access disposition. However, the account handlers still require another sentence reading, including one called by `OpeningAccount`; C3-2. |
| **C2-3 — continuation after tool activity** | **Open, narrowed.** P:519–533 correctly withdraws *Uncertain holds* and preserves the guarded go-on. Its delivery-to-receipt mapping is still wrong for protocol session creation, and its blanket retry wording conflicts with distinct opening and native-resume rules; C3-3 and C3-5. |
| **C2-4 — independent positive evidence** | **Closed.** P:463–471 and :934 add the sixth operation, matching identity/revision/subject and keeping one-attempt release separate; R-B includes it at P:872. This addresses the accepted infrastructure contract at `docs/2026-10-27-provider-infrastructure-design.md:176–185`, beyond Lyntai’s stated-recovery-only `Lift` at L/Core/Agents/AgentPoolReducer.cs:174–190. |
| **C2-5 — initialization dependency** | **Closed.** P:1039–1045 adds response-dependent sequencing, withheld-answer and initialization-error cases; LQ15 at P:937 keeps sequencing inside the library. The cited old readers wait at D/Agents/ClaudeUsage.cs:197–203 and CodexUsage.cs:241–250; L/Basic/CliUsageSource.cs:133–142 eagerly queues requests. |
| **C2-6 — AG20 citations** | **Closed; decline accepted. I withdraw C2-6.** Raw Git-blob LF counts at **both** `2fb2dc7e` and `0cbd84c1` are **6,770**. D234 starts at `docs/DECISIONS.md:6709`, standing :6711–6713; D235 starts at :6747, standing :6749–6750, procedure :6753–6763. §17’s counts hold. AG20 is now on `master`; the earlier master-status statements are historical. |

### Verdict: another round, kept narrow

The package direction and ownership split hold. Five blocking findings remain in the oracle, trusted account handling, delivery, reset evidence and retry mapping.

### 1. New findings

**C3-1 — Blocking: the provenance correspondence cannot be one-to-one.**

P:790–805 requires each layer entry to pair one-to-one with the Daoris entry it replaces, and deliberately rejects one layer entry paired with two Daoris entries.

The existing readers need precisely that relationship. For a Claude ACP failure carrying `data.errorKind: rate_limit`:

- Daoris chooses `owner:words:limit` when its recorded limit sentence matches, before examining the kind; otherwise it chooses `owner:kind:rate_limit` (D/Agents/AgentErrors.cs:154–164, :213–219; Claude’s declaration at :288).
- Lyntai chooses `acp.data.errorKind=rate_limit` in either case. A matching sentence may fill restriction details without changing that `By` (L/Basic/Acp/AcpFailure.cs:65–70; ClaudeCli/StreamJsonFailure.cs:67–76).

Thus legitimate parity requires one layer source paired with two old sources. This is independent of whether their semantic outcomes match.

**Change:** declare a fixed relation of permitted provenance pairs, with each scenario checking both expected sources. Permit genuine differences in source granularity; keep outcomes, recovery, delivery and ordering exact. Replace the blanket bijection sabotage with an undeclared or incorrect-pair sabotage. Do not solve this by dropping provenance.

**C3-2 — Blocking: typed access stops before the handlers that change account state.**

P:491–495 migrates `OpeningAccount` from suffix interpretation to a typed disposition. P:507 prohibits tables behind a bound kind. But normal conclusions still call:

- `AccountRefused`, which requires `Toolchain.Refused` and reads `HarnessEnding` against those words (D/Loop/Driver.cs:3422–3431).
- `AccountSignedOut`, which calls `RefusedAccounts.SignInRefused` (Driver.cs:3491–3495). That helper requires another `SignInRefusals.Read` before writing account state (D/Agents/RefusedAccounts.cs:17–33).

Even `OpeningAccount`’s signed-out branch calls that sentence-reading helper (D/Loop/DriverContinuations.cs:623–625). Replacing its suffix test alone therefore does not finish the trusted path.

P:510–511’s “a prompt’s refusal touching no account” also needs qualification: ordinary failed prompt conclusions do apply account policy today (D/Loop/Driver.cs:2950–2951).

**Change:** give refusal and signed-out handling trusted entries consuming the typed access outcome, and route bound-kind conclusions through them across all executors. Retain sentence entries for Daoris-run kinds. Distinguish “does not enter the refused-open branch” from “never changes account state.” Prove both normal prompt conclusions and refused opens, including the tool’s own account.

**C3-3 — Blocking: a session-start event does not acknowledge the prompted words.**

P:525–530 maps Lyntai `Taken` to today’s acknowledged words. Its prerequisite is false on the protocol door.

Lyntai emits `SessionStarted` after opening the session and then queues the first prompt (L/Basic/Acp/AcpAgentReader.cs:140–152). `CliAgentLoop` counts that event toward `Taken` (L/Basic/CliAgentLoop.cs:37–42, :139–140). A first prompt failing before any turn update consequently reads `Taken`.

Daoris instead records a failed prompt as unacknowledged when that prompt’s own turn was not heard; a resume’s opening words set `OpeningUnacknowledged` (D/Agents/Acp.cs:2649–2655). Hearing is set by the relevant agent/tool/plan updates, explicitly excluding a meter or mode alone (:2984–2996).

The proposed mapping can therefore turn uncertain resumed words into acknowledged words.

**Change:** map receipts from evidence about the particular prompt, excluding session creation and unrelated metering. Request the necessary observation through LQ3 or a companion request, without host-side frame parsing, and include it in the affected release labels. Add comparisons for a first/resumed prompt failing before updates, a metering-only turn, and tool activity followed by failure. Keep the guarded go-on after the latter.

**C3-4 — Blocking for the affected moves: D125 cannot be preserved from an already-resolved instant alone.**

P:496–501 proposes applying D125’s bounds through one function to Lyntai’s instant. But Lyntai’s `RestrictionRecovery` retains only kind and instant (L/Core/Agents/AgentFailure.cs:81–97), after resolving information D125 still needs.

For example, Lyntai accepts a time-only reset up to one minute behind its clock as today’s instant (L/Core/Inference/StatedRecovery.cs:66–73). Daoris deliberately treats a time-only reset just past as tomorrow’s; only dated resets receive grace (D/Agents/AccountLimits.cs:223–228, :239–271).

At 16:00:30, `resets 4pm` therefore resolves differently before any common bounds function runs. The layer also loses stated-versus-assumed zone provenance, needed by `LimitSeen.AssumedZone` (AccountLimits.cs:50–57). Existing dated-without-year fixtures expose another grammar difference (T/Agents/AccountLimitsTests.cs:43–59 versus L/Core/Inference/StatedRecovery.cs:22).

**Change:** widen LQ16 or add a request for sufficient decoded reset evidence—or a policy seam before omitted dates are resolved—to preserve date/time/weekday precision and zone provenance. Keep D125’s policy in Daoris and avoid reparsing sentences there. Compare time-only, weekday and dated resets, zone fallbacks, configured unknown cool-off, grace and furthest bounds. An instant plus a window name is insufficient.

**C3-5 — Blocking: the delivery paragraph needs separate opening, prompt and native-resume retry rules.**

P:527–531 says written original words are never resent and permits the go-on after each delivery value. That needs an explicit phase and door scope:

- A transient protocol **open** retries the raw `session/new`, `session/resume` or `session/load`, with no go-on; the original prompt goes once after opening succeeds (D/Agents/Acp.cs:1515–1559). Existing tests assert that exchange (T/Agents/AcpTransientTests.cs:694–732).
- A transient protocol **prompt** sends the new guarded go-on, as round 3 now correctly states.
- A structured native **resume ending before it names its conversation** retries the same resume once, using the same start and prompt (D/Loop/Driver.cs:3972–3979; DriverContinuations.cs:1319–1345). The design expressly retains that obligation in m-ii (P:287; `docs/2026-10-26-agent-provider-design.md:1137`).

The universal no-resend wording and the retained native retry cannot both describe unchanged behavior.

**Change:** state the retry table by phase and door. Make LQ3’s closing contract include control sufficient to repeat the raw opening operation without repeating the handshake or sending a go-on. Either preserve the accepted native pre-name retry explicitly, or remove it through a named correction before 1d, reviewed against D189/D213. The move must not silently choose between those contracts.

### 2. The other priority checks

- **m-i:** the four additions at P:334 and :983–988 cover the identified executor needs. Keeping word operations beneath `TransportRun` until m-ii preserves their identities and attempt receipts. The expanded proof at P:1186 is appropriate. Implementation feasibility remains unmeasured.
- **Cooling and phase:** the trusted `Limited` entry and routing of seven callers address the second limit reading. Typed access and preserving semantic phase are the right shape, subject to C3-2 through C3-5.
- **1d’s waits:** AG12, LQ16 and LQ11’s access half are correctly added at P:868–871 and :1069–1079. They address the three newly inventoried native readings; reset evidence needs C3-4’s addition.
- **1s-i and 1s-ii:** the UTF-8/non-BMP proof and per-mode, start-and-resume measurements at P:1052–1068 are appropriate.
- **Library needs:** LQ12’s pure evidence operation and LQ15’s response-dependent exchange pass Lyntai’s generic-library rules. Authority, credential epochs and spending policy remain host-owned; protocol sequencing remains library-owned.

Package closure, offline restoration, publish layout, container cost/concurrency, installed parity, actual isolated-runtime writers, stdin passage, tool behavior per mode, usage-server tolerance and real held-permission waiting remain **unmeasured**.

## The Opus maker, round 4

*The Opus maker's read of round 4, read-only and apart; kept as written, its headings one level down.*

*Read-only at Daoris `42304cfea` and Lyntai `master` `bbbe285b`; nothing built or run. The checkout moved to `01b6a6240` while it read (FOUNDATION1d-ii part 5 and others touching `Loop/Driver.cs`, `Loop/DriverContinuations.cs`, `Loop/Chat/ChatRunner.cs`); every driver line below was re-read at `42304cfea` with `git show`, and the design's lines hold there. Unprefixed paths are under the driver library; `design:` lines are the design's own.*

**Result:** 8 findings, 3 blocking (O4-F1 to O4-F3), all three for 1e and its request set.

**Verdict: accept after named changes.** O4-F1 to O4-F3 taken as named, both makers checking the changed lines before 1e is dispatched. Nothing here blocks m-i, 1a, 1b-ii, 1c or 1d. O4-F4 and O4-F7 change text in 1b-i and m-i.

### 0. Round-3 findings against §18

| Finding | Round 4 | Shown at |
|---|---|---|
| O3-F1 | **Closed, change accepted.** `ModelRefused` is called at two sites only (`Driver.cs:2954`; `DriverContinuations.cs:760`); the helper, intake, opinion and partner sites call the other three readers. The native conclusion is handed no failure: `turnFailed` is null at `Driver.cs:3963` and passed as is at `:4002-4004`, so `AccountLimited` returns at `:3345` and `SignInRefused` at `RefusedAccounts.cs:19`. Two corners open: O4-F5 | design:466-490 |
| O3-F2 | Closed for the native door; on the protocol door the subject map sends AG11's refusal the wrong way: O4-F1 | design:535-540 |
| O3-F3 | Closed | design:545-550, :634-635 |
| O3-F4 | Closed; LQ3's wording is O4-F8 | design:619-627, :1057 |
| O3-F5 | Closed; lines hold at `42304cfea` (`ChatRunner.cs:2308`, `:3289`, `:3400`, `:3410`). How the runner reaches its `AcpSession` is unnamed: O4-F7 | design:356, :1124-1127 |
| O3-F6 | Closed for 1d; the protocol door's context figures are not covered: O4-F3 | design:661-672, :1071 |
| O3-F7 | (a) closed, the relation accepted; (b) the other way from what was asked: `Hit` does not belong in the projection, O4-F4; (c), (d) closed | design:913-926, :895 |
| O3-F8 | **Closed, change accepted**, though it owes a D125 note (§2 item 4) | design:568-581 |
| O3-F9, F10, F11 | Closed | design:1070, :1066, :82-85 |

### 1. Citations found wrong

- `Agents/AccountReadings.cs:310-314` (design:485-486 and LQ16 at :1070), cited for the walk reading a rejected frame: the `rejected` standing is at `:308`, read as near at `:209`; `:310-314` hold the credits word and a recorded `allowed` frame; `:319` records `rejected` as "declared, not seen" (`:302`).

Every other line relied on holds: `Driver.cs:2875`, `:2938`, `:2950-2960`, `:2977`, `:3341-3383`, `:3422-3448`, `:3473-3495`, `:3886-3907`, `:3909`, `:3963-3981`, `:4002-4014`, `:4143-4146`; `DriverContinuations.cs:460`, `:607-631`, `:646-660`, `:756-780`, `:1155`, `:1326-1345`, `:1388`; `HarnessRoster.cs:272-311`; `RefusedAccounts.cs:17-33`; `DriverBrainstorms.cs:770-814`; `ChatRunner.cs:1613-1638`, `:2640`, `:2668-2672`; `Acp.cs:1476-1565`, `:2649-2656`, `:2984-2996`; `StructuredOutput.cs:163`, `:229-232`, `:367`; `AccountLimits.cs:82-94`, `:118-136`, `:223-271`; `HarnessEnding.cs:9-12`. Lyntai: `CliAgentLoop.cs:37-43`, `:126-140`; `AcpAgentReader.cs:140-152`; `AgentFailure.cs:81-97`, `:113-120`; `StatedRecovery.cs:22`, `:40-73`; `StreamJsonFailure.cs:21-35`; `LyntaiBuilder.cs:593`; `TASKS.md:310-324`.

### 2. §14's six items

1. **The native door's readers: confirmed.** No account is cooled or read signed out at a native conclusion; native conversations do not either (`ChatRunner.cs:1613`: the limit and sign-out readers given to `ProtocolChat` alone), nor a native brainstorm pass (`DriverBrainstorms.cs:786` reads `turnFailed`). What a person sees: the session ends `failed` with a strike, not `limit` (D125 point 3 not met); its words wait as a first failure, not limit-held (`Observation.cs:492-497`); the next look passes that account only if a window reading stands `refused` or a usage read puts it over its curve, and Daoris's table records the stream's `rejected` frame as never seen. So a person can see the same account picked again into the same refusal until strikes park the quest: D125's 1 October incident, on the native door. Which signal arrives first is unmeasured. Keeping it at 1d is right: D125's recorded state, not new (O4-F6). Not the last readers: the conversation's sign-out reader (O4-F5), the open's model-refusal writes (O4-F1).
2. **The trusted entries** carry every write the six sites make after their readings (Refuse with SignedOut; SignedOut with the own-sign-in hold and its line, the code disposition writing SignedOut alone, `DriverContinuations.cs:630`; the offers reading with its line; cooling, `_limitSaid`, the weekly `Told`, the three account lines, `limit:` and the limit-held words). Two writes missing: the conversation's sign-out (O4-F5), the protocol open's model refusal (O4-F1).
3. **The retry table** distinguishes doors and phases but not run kinds (O4-F2). LQ3's per-prompt delivery and its raw repeat of the open pass Lyntai's rules 1 and 5; the wording fails rule 7 (O4-F8). The native resume's retry can be built over `LayerRun`'s end if `LauncherRunner` reports the exit, which the layer's `SessionEnded` does not carry; its restart from the `ProcessStartInfo` is a use m-i does not name (O4-F7).
4. **The reset as stated.** Precision, the zone's provenance and the host's clock are necessary, not sufficient (O4-F8). *An absolute moment counts as dated* fits D125's reasons (the grace exists "for clocks that disagree", `D125.md:18-19`; the 2026-10-02 fix removed it only where a date was left out, `:274-278`), but D125 never states it and no path today cools from a field's moment (`byCode` is `Stated: false`, `HarnessRoster.cs:280`): a new reading, which 1b-i should land as a D125 note.
5. **The pair relation.** Each scenario names its pair and both sources are checked against it, so no wrong source passes in a recorded case and the plants catch a swap. `Hit` does not belong in it (O4-F4).
6. **LQ17 and m-i.** LQ17 passes rules 1 to 3 (neutral counts and denials in Core, `<synthetic>` and `modelUsage` adapter-side); its context half is a protocol need too (O4-F3). m-i's cut from m-ii is clean in order (design:1100-1103), provided the runner's reach to its session is named as `TransportRun`'s alone (O4-F7).

### 3. Findings

**O4-F1 (blocking for 1e). Subject `Model` is mapped to the pipe exception on every door.** Design:538-540 sets `ModelRefused` whenever "the turn showed no model's words and no tool". AG12 covers "the ACP door's AG11 refusal" (Lyntai `TASKS.md:321`), and AG11 ends "before the prompt" (`:314-315`), so that condition always holds there. On the protocol door today a refused model at the open takes D205's path: the re-ask on the same account with the offers write (`Driver.cs:3886-3906`); else `choice-not-offered` with no strike and its count released (`:2938-2939`, `:2977`); on a resume it is put back (`DriverContinuations.cs:646-651`). Under the map it would end `failed` with `account.refused-model`. *Change:* map `Model` by door and phase: at a protocol open, today's `refusedAt` and `NotOffered` path (with LQ8); `ModelRefused` only on the native bound kind after a prompt. 1e's proof gains a model refused at the open, on both paths, for a start and a resume.

**O4-F2 (blocking for 1e). The retry table has no run kind.** The episode, the raw repeat of the open and the per-prompt disposition exist only where a retry is handed (`Acp.cs:1482`, `:1536`, `:2651`). Today a retry is handed to driven quest starts and continuations (`Driver.cs:3909`, the only two `transientWanted` sites, `:2875`, `DriverContinuations.cs:460`) and to conversations (`ChatRunner.cs:1634`); a helper's, intake's, opinion's, partner's or brainstorm's protocol pass has none, its transient failure ending it as it came. *Change:* a fifth row, "protocol, a run handed no retry": no episode, no repeat of the open, no per-prompt disposition, its failure ending as today; 1e's proof gains such a pass failing transiently at its open and at a prompt, on both paths.

**O4-F3 (blocking for 1e's request set). The layer drops the protocol door's context figures.** `AcpAgentReader.cs:20` says `usage_update`'s figures are "no event"; `AcpAgentDoor.cs:35` declares the ability unsupported. Daoris reads `used` and `size` (`Acp.cs:3549`, `:3618`) and records them on every conclusion (`Driver.cs:2878-2882`). LQ17 is carried by R-d alone (design:998-1004), and §5.4 has no table for the protocol door. *Change:* ask LQ17's context half for the protocol door too, in R-e, and add §5.4's protocol-door readings beside the native door's.

**O4-F4 (not blocking; before 1b-i fixes its projection). `Hit` is a payload, not a path name.** It is written to the service's `account.limited` and `account.not-lifted` lines (`AccountCooling.cs:408`, `:429`), taken from the sentence's marker (`AccountLimits.cs:118`: "weekly", or the whole clause). Filling it "from the window as the vendor names it" (design:584) changes it (e.g. to `seven_day`), and the projection lets that through, though §6.3's own rule says no changed reading may pass (design:911-912). *Change:* withdraw `Hit` from the projection and compare it exactly; fill it from Daoris's own window names (`AccountReadings.cs:305`) where today's sentence names that window, and where it names something else ("individual spend"), from LQ16 widened to state what was hit, or from a correction on today's path first.

**O4-F5 (not blocking; before 1e and 1f-i). Two account readers are outside the rule.** The conversation's sign-out reader (`ChatRunner.cs:1624`, `:2668-2672`, through `SignInRefused`) is not in §5.2's list. "The native door as today" is stated for 1d alone (design:486-489), yet a native conversation reads no limit or sign-in (`ChatRunner.cs:1613`; `D125.md:530-531`), and a native brainstorm pass none. *Change:* route the conversation's sign-out through the sign-out entry; state that 1f-i's native conversation, and a native brainstorm pass, call no sign-out or limit entry.

**O4-F6 (not blocking). The native door cooling nothing is decided state, not a new find.** D125 records it: "The native door's failed result is not handed to the conclusion … It waits for TOOL4b's evidence" (`D125.md:163-166`), and TOOL4d's reason "stands" (`:296-297`). *Change:* cite that note at design:482 and in §18.3; say any fix is D125's own row (a native limit sentence recorded, since `PipeModelRefusalTests.cs:111-112`'s is authored, not recorded; then the ending's failure handed to the sign-out and limit readers), independent of 1d, whose proof follows whichever path is today's when 1d is dispatched; mark "reaches the walk by the stream's rejected frame" unmeasured (§1).

**O4-F7 (not blocking; m-i). Two uses m-i does not name.** `AgainAsync` starts the same `ProcessStartInfo` again (`DriverContinuations.cs:1326-1335`); none of the four new members carries it (the go-on replaces `GoOnWith`'s factory, `:1155`, not this). The conversation runner's words need its `AcpSession` (`ProtocolChat`, `ChatRunner.cs:3085`), which `TransportRun` would now create. *Change:* name the restart (a fifth member: the same start once more, transcript appended, opening not said again; or a second `AgentRun` opened from the same plan); name the session accessor as `TransportRun`'s alone, deleted at m-ii, with `LayerRun` refusing a conversation before m-ii.

**O4-F8 (not blocking; before LQ3 and LQ16 are sent). The request wording.** LQ16: Lyntai drops a whole reset whose zone it cannot resolve (`StatedRecovery.cs:46` returns null), while D125 reads such a reset in the machine's zone, flagged as assumed (`AccountLimits.cs:130-131`); ask for the stated parts back with the zone marked unresolved, and for the zone itself beside its provenance, so the host's policy for an omitted date can resolve it again. LQ3: "heard" as Daoris defines it (words, thinking, a tool step, a plan; never a meter or a mode) is the host's classification, which rule 7 keeps out of the library; ask for the observation instead (whether the prompt's write completed, and which events of its turn came after it), and say this changes `Taken`'s documented meaning, which counts a session start (`AgentFailure.cs:118`).

### Still unmeasured

Whether Claude Code's native stream sends a `rejected` frame when an account hits its limit; whether a usage read reaches the walk before the next start; whether a Claude frame's `resetsAt` and its sentence agree to the minute; the vendors' access fields (LQ11); whether the installed usage servers tolerate the eager order; stdin passage and the tools per mode (1s-i, 1s-ii); the isolated runtime's actual writers; everything else §14 already lists.

## Codex, round 4

*Codex's read of round 4, read-only, at `42304cfea`, written without seeing the Opus maker's; kept verbatim, its headings one level down.*

Read at Daoris `42304cfea` and Lyntai `bbbe285b` (`master`). I read §18 first, the sections named by §14 whole, D191, D213’s direction and amendments, and the relevant Lyntai contracts and source. Nothing changed; no builds, tests or agent runs ran. Counterexamples below are source-derived, **not executed**.

References: **P** = `docs/2026-10-30-agent-lyntai-design.md`; **D/** = `src/Daoris.Desktop/Daoris.Desktop.Driver/`; **T/** = its sibling `Daoris.Desktop.Driver.Tests/`; **L/Core/** and **L/Basic/** = Lyntai’s `src/Lyntai.Core/` and `src/Lyntai.Providers.Basic/`. Source references supporting this read were opened and checked.

### 0. My round-3 findings against §18

| Finding | Round-4 disposition |
|---|---|
| **C3-1 — provenance correspondence** | **Closed.** P:913–926 defines permitted pairs and requires each scenario to check its particular expected pair. P:930–934 tests an undeclared pair and an unexpected declared pair. This preserves provenance without demanding an impossible bijection. |
| **C3-2 — complete trusted account ingress** | **Open.** P:466–490 adds the missing trusted entries and correctly distinguishes native conclusions. I accept preserving their present absence of sign-out and cooling. But a single winning access disposition still cannot reproduce the different readers’ writes; **C4-1**. |
| **C3-3 — delivery per prompt** | **Closed as a requested contract.** P:619–639 and LQ3 at P:1057 require evidence from the particular prompt, excluding session creation and unrelated metering, with the requested comparisons. Whether the implementation supplies that evidence remains unmeasured. |
| **C3-4 — reset evidence before resolution** | **Closed with a change I accept.** P:568–580 and LQ16 at P:1070 retain stated precision and zone provenance and leave D125’s resolution policy with Daoris. Treating an absolute timestamp supplied by a field as dated is a reasonable extension of D125’s distinction; it is not a claim that today’s sentence parser already accepts Unix timestamps. |
| **C3-5 — retry by phase and door** | **Open.** P:630–639 correctly separates raw opening retries, prompted go-ons and native pre-name retries. However, retry availability also depends on the executor, and `SessionStarted` does not precisely reproduce today’s native “named” evidence; **C4-2 and C4-3**. |

**Verdict: another round, kept narrow.** The remaining blockers concern account-write evidence, retry applicability, native naming evidence, model restrictions and the release prerequisites of 1b-i.

### 1. New findings

#### C4-1 — Blocking: the winning access reason and basis cannot preserve every account reader

P:551–561 maps LQ11’s reason and basis to three dispositions. P:473–480 then routes bound outcomes through the trusted account entries. That still compresses evidence that today’s readers use independently.

For a declared authentication-required code accompanied by `Authentication required`:

- `AgentErrors.Read` selects the **code**, before checking sign-in words (`D/Agents/AgentErrors.cs:168–176`).
- A refused resume’s `OpeningAccount` consequently takes its code branch, writing only the signed-out reading (`D/Loop/DriverContinuations.cs:617–630`).
- An ordinary failed conclusion independently reads the **words**, then writes the signed-out reading, holds the tool’s own sign-in where applicable, and returns its account note (`D/Loop/Driver.cs:3491–3495`; `D/Agents/RefusedAccounts.cs:17–33`).

Change the sentence to one the sign-in table does not recognize: the code branch still applies at the refused open, but the ordinary conclusion’s sentence reader writes nothing. A single “sign in, stated by field” disposition cannot distinguish these ordinary conclusions.

There is another caller beyond the newly listed limit readers: conversations invoke that same sentence helper through `SignedOut` (`D/Loop/Chat/ChatRunner.cs:2668–2678`), both at a failed open (`:3046–3048`) and a failed prompt (`:3547–3548`).

**Change:** retain sufficient independently decoded access evidence, then define the writes by executor and phase. LQ11 must preserve a matching sign-in sentence alongside a winning field where both matter, without reparsing it in Daoris. Explicitly route the conversation’s sign-in reader through the trusted path. Compare code-only and code-plus-matching-words failures at refused opens, ordinary conclusions and conversation opens/prompts, including the tool’s own sign-in.

#### C4-2 — Blocking: the retry table needs executor applicability

P:632–633 describes protocol opening and prompt retries as universal behavior. Today they require a retry owner:

- `AcpSession` retries an opening refusal only when `_retry` exists (`D/Agents/Acp.cs:1536–1540`); its prompt retry has the same condition (`:1482–1492`).
- `HoldAsync` constructs that owner only for the qualifying protocol quest run (`D/Loop/Driver.cs:3909–3913`).
- The conversation runner constructs its `AcpSession` without that owner, opens it once, and handles an opening failure as an ended open (`D/Loop/Chat/ChatRunner.cs:2962–2973`, `:2994–2996`, `:3036–3063`). Its prompt retry is separately owned by `ChatRetry` (`:1634–1642`).

Consequently, a transient conversation **open** currently does not repeat the raw operation. Applying P:632 to it would change behavior.

**Change:** add executor/run applicability to the table. Preserve the distinction between a driven run handed `TransientRetry`, a conversation’s separately retried prompts, and protocol runs handed no retry. LQ3 should expose the opening control; the host decides whether to repeat it. Add paired cases for a transient conversation open and a protocol run without a retry owner.

#### C4-3 — Blocking for 1d: `SessionStarted` is broader than today’s native “named” evidence

P:356 says `LayerRun` derives the native resume’s “named” flag from `SessionStarted`.

Today Daoris names the native conversation only from `system/init` (`D/Agents/StructuredOutput.cs:163–164`, `:179–183`). The existing test explicitly rejects a `system/status` frame carrying a session id (`T/Loop/NativeResumeTests.cs:80–92`).

Lyntai emits `SessionStarted` from **any** `system` frame with a new string session id, without requiring `init` (`L/Basic/ClaudeCli/StreamJsonAgentReader.cs:66–79`).

Thus a status frame naming an id followed by a failed exit suppresses the proposed retry, while today’s path remains unnamed and retries (`D/Loop/Driver.cs:3975–3980`). The proposed public event alone cannot distinguish those frames.

**Change:** obtain decoded opening evidence that preserves today’s initialization boundary, or land a separately justified correction to that boundary before 1d. Test a status-only failed resume, an actual init, and repeated/different ids. Preserve the first accepted conversation identity as today’s mapper does.

#### C4-4 — Blocking: model scope alone does not mean a refused model

P:535–540 says subject `Model` sets `HarnessEnding.ModelRefused`. A restriction’s scope does not establish that refusal.

Lyntai already produces:

- a **limit** scoped to a model for sentences such as a usage limit “for” a model (`L/Core/Inference/ProviderVerdictClassifier.cs:116–131`, `:237–240`);
- a **transient capacity** restriction scoped to a model (`:135–140`).

Neither means `model_not_found`. Today Daoris sets its native refusal flag only from the synthetic message’s explicit `model_not_found`, on a failed, unstopped turn that ran nothing (`D/Agents/StructuredOutput.cs:223–232`, `:365–367`). That flag writes a refused-model offer record (`D/Loop/Driver.cs:3476–3486`).

The proposed broad mapping can therefore turn a model’s quota or capacity restriction into a persistent refused-model reading.

**Change:** identify the specific AG12 model-refusal outcome, including its failure kind and recovery/reason, before setting the flag; retain today’s no-work guard. Keep model-scoped quota and capacity on their respective paths. Add negative cases for both beside the positive `model_not_found` case.

#### C4-5 — Blocking for 1b-i: its new ingress proof requires releases absent from its prerequisite

P:1160 makes 1b-i depend on R-a. R-a carries AG1–AG9, while LQ11’s access evidence and LQ16’s stated reset are placed in later labels (`P:996–1004`).

But 1b-i now implements those mappings (`P:1150–1154`) and must prove all access dispositions and stated reset forms through the ingress (`P:1339`). The current public `AgentFailure` supplies kind, provenance, restriction and delivery, without the new access evidence (`L/Core/Agents/AgentFailure.cs:127–134`); recovery supplies only kind and resolved instant (`:81–97`).

This conflicts with the requirement to build against released public members (`P:1011–1014`). Constructing a Daoris normalized value directly would test downstream policy, not the promised translation from the layer.

**Change:** split 1b-i’s immediately buildable adapter/oracle foundation from the ingress additions awaiting LQ11/LQ16, or give the latter an explicit release prerequisite. Separate tests of host policy from tests proving that a released layer supplies its inputs. Keep the completed ingress proof before the affected moves.

#### C4-6 — Non-blocking: `UsageFinal`’s model is not necessarily the startup model

P:671 and P:1071 describe the current model as “from the start.” The cited event declaration establishes only that it carries one model (`L/Core/Agents/AgentStreamEvent.cs:30`).

The Claude reader initializes that value from system frames, then updates it from assistant messages (`L/Basic/ClaudeCli/StreamJsonAgentReader.cs:69–70`, `:124–126`) and emits the current value (`:263–264`).

**Change:** say it carries one remembered model, updated by assistant frames, rather than a per-model token split. LQ17’s need remains valid.

### 2. The six priority checks

- **Native conclusion:** confirmed from source. `turnFailed` is null on the native path (`D/Loop/Driver.cs:3963`) and reaches the conclusion unchanged (`:4002–4004`). `AccountLimited` rejects it (`:3345`); sign-out also reads nothing. Refused-credential and refused-model handling remain separate. Preserving this at 1d is consistent with a move stating no difference; adding cooling should be a separate correction.
- **Trusted entries:** the four entry points are the right direction, but their inputs and caller matrix remain incomplete under **C4-1**.
- **Delivery and LQ3:** per-prompt evidence and separately repeatable opening operations pass Lyntai’s generic-library test: both remain useful without Daoris’s names or retry policy. The retry table needs **C4-2**, and native retry evidence needs **C4-3**.
- **Reset:** the widened request addresses C3-4. I accept an actual absolute field timestamp receiving dated-reset grace, with the same margin and furthest bound. D125 excludes grace specifically for omitted-date time-only and weekday forms (`docs/decisions/D125.md:274–278`, `:329–336`); the field-first rule is at `:13–17`. Frame-versus-sentence agreement remains **unmeasured**.
- **Pair relation and `Hit`:** the declared relation does not admit a wrong source merely because another scenario permits it: P:920–922 also checks the scenario’s expected pair. `Hit` belongs in the comparison because account log lines retain it (`D/Agents/AccountCooling.cs:408`, `:429`). Its correspondence must remain scenario-specific.
- **LQ17 and m-i:** LQ17 states reusable reporting needs; vendor-specific decoding belongs in the adapter. Keeping conversation word paths beside `AgentRun` until `DoorInput` preserves m-i’s cut, and adding brainstorm passes closes the identified executor omission (`P:356`, `:1124–1127`). Implementation feasibility remains **unmeasured**.

Package closure, offline restore and publication layout, container cost/concurrency, installed parity, actual isolated-runtime writers, stdin passage, tool behavior by mode, usage-server tolerance and real held-permission waiting remain **unmeasured**.

## The Opus maker, round 5

*The Opus maker's acceptance read of round 5, read-only and apart; kept as written, its headings one level down. Its `design:` lines are round 5's as it read them, before the named changes (the design's §20).*

Read-only at Daoris `fbc336353` and Lyntai `master` `bbbe285b`; nothing built or run. Unprefixed paths are under the driver library, `design:` is the design's own line, `Lyntai/` the Lyntai checkout.

**Lines at this tip.** The *Read at* offsets hold: `Loop/Driver.cs` +24 past :2783, `Loop/DriverContinuations.cs` +66 past :1135, `Loop/Chat/ChatRunner.cs` +3 between :693 and :1852 and +61 past :1915. Between `01b6a6240` and the tip, FOUNDATION1j part iii touched only `Course/Chat/`, `Roles/ActorChoices.cs` and `Workplace/`; nothing under `Agents/`, `Loop/`, `Platform/` or `Events/` moved. Lines re-read at their shifted places include `Driver.cs:2962-2963`, `:3001`, `:3910-3937`, `:3500`, `:3515-3519`, `:3996-4005`; `DriverContinuations.cs:460`, `:617-651`, `:1221`, `:1392-1401`; `ChatRunner.cs:1616-1645`, `:2729-2739`, `:3023-3034`, `:3079-3124`, `:3146`, `:3608-3609`.

### Verdict: accept after named changes

None blocks. O5-F1 before 1b-i fixes its host policy; O5-F2 to O5-F4 before 1d, 1e-i and 1f-i respectively. Nothing blocks m-i or 1a.

### 0. Round-4 findings against §19

| Finding | Round 5 | Shown at |
|---|---|---|
| O4-F1 | **Closed, change accepted.** A refusal identified by its whole outcome (Fatal, Model, OwnerAction, the door's entry); the native guard kept; a protocol open takes the D205 path, never `ModelRefused`; a conversation's open moves its words to the pending start (`ChatRunner.cs:3079-3096`); negative cases in 1b-i and 1d. Two loose ends: O5-F1 | design:591-605, :1359, :1463, :1469, :1471 |
| O4-F2 | **Closed, change accepted.** Rows by run; the conversation's open handed no retry (`ChatRunner.cs:3023-3034`; `Acp.cs:2253-2261`); a pass with no retry ends as it came; the only `transientWanted` sites `Driver.cs:2899`, `DriverContinuations.cs:460`. Native conversation row missing: O5-F4 | design:710-726, :1471 |
| O4-F3 | **Closed.** The protocol-door table, LQ17's context half, R-e and §8's footer present (`AcpAgentReader.cs:20`; `AcpAgentDoor.cs:35`; `Acp.cs:3549`, `:3618`; `Driver.cs:2902-2906`). R-e1's label does not list it: O5-F3 | design:762-769, :1102-1103, :1174, :1184 |
| O4-F4 | **Closed.** `Hit` out of the projection, compared exactly, filled from Daoris's window names (`AccountReadings.cs:305`), else LQ16 | design:654-662, :1021-1022, :1173 |
| O4-F5 | **Closed.** The conversation's sign-out routed by the words' evidence; a native conversation and a native brainstorm pass call no account entry (`ChatRunner.cs:1616-1627`; `DriverBrainstorms.cs:786`) | design:533-540, :1363-1364, :1472 |
| O4-F6 | **Closed.** D125's note cited (`D125.md:163-166`, `:296-297`); any fix D125's own row, independent of 1d; the rejected frame's arrival unmeasured | design:504-519 |
| O4-F7 | **Closed, the first way.** m-i's fifth member, the restart (`DriverContinuations.cs:1392-1401`); the accessor `TransportRun`'s alone, deleted at m-ii; `LayerRun` refuses a conversation before m-ii and reads the exit from `LauncherRunner` | design:378, :1231-1233, :1461 |
| O4-F8 | **Closed.** LQ3 asks for the observation and states the change to `Taken`; LQ16 asks for an unresolved zone's parts back | design:705-709, :648-650, :1160, :1173 |
| §1 citation | Closed (`AccountReadings.cs:302`, `:308`, `:319`) | design:511-512 |
| §2 item 4 | Closed: 1b-iii writes the D125 note | design:646-648, :1464 |

### 1. New findings

**O5-F1 (not blocking; before 1b-i fixes its host policy). Round 4's refused-model rule still stands beside round 5's.** (a) Two sentences still give the rule round 5 withdrew: §5.2's "a refused model sets the ending's `ModelRefused` from the layer's restriction" (design:498-499) and §5.3's "subject `Model` sets the ending's `ModelRefused`" (design:588-590), neither marked withdrawn (as `Hit` is at design:657, :1021). Read alone, design:588 takes a credit limit scoped to one model as a refusal, which Lyntai's classifier produces (Limit, Credit, Model, OwnerAction: `Lyntai/src/Lyntai.Core/Inference/ProviderVerdictClassifier.cs:118-127`). (b) Design:600-601 says a refusal at a protocol open "takes today's D205 path … the re-ask on the same account"; today that re-ask is on the same session, no respawn (`Acp.cs:727-760`), while AG11 ends the turn before the prompt (`Lyntai/TASKS.md:314-315`), so the re-ask depends on LQ3's "a model re-asked on the same session", and AG12's Fatal outcome should reach Daoris only where no re-ask answers. *Change:* (a) mark both sentences "withdrawn by round 5 (O4-F1, C4-4)"; (b) add at design:600 that the re-ask is LQ3's same-session re-ask, made before the run ends, and a respawn is a difference the oracle's spawn observation fails.

**O5-F2 (not blocking; before 1d). The native guard's "ran nothing" has no named evidence on the layer path.** Today `_ran` is set only by a non-synthetic `assistant` message with blocks (`StructuredOutput.cs:229-232`, `:367`). The layer emits text only from stream deltas, keeps an assistant message's text blocks as fallback text, and updates `_model` from any assistant message including `<synthetic>` (`Lyntai/src/Lyntai.Providers.Basic/ClaudeCli/StreamJsonAgentReader.cs:125-126`, `:140-146`). Both spawns pass `--include-partial-messages` (`Agents/Adapters.cs:2212`; `ClaudeCli/ClaudeAgentArgs.cs:53`). Unmeasured: whether the CLI streams a `<synthetic>` message's text as deltas; if it does, a layer path reading "ran" from `TextDelta` never sets `ModelRefused`. *Change:* at design:597-599 name the evidence (no `TextDelta`, `Thinking` or `ToolCall` of the session's own in the turn); hold 1d's refused-model case on a frame stream recorded with the spawn's own flags; if the deltas exist, the case goes into LQ17's request to keep the harness's own messages apart.

**O5-F3 (not blocking; before 1e-i). R-e1's label leaves out LQ17's context half.** §5.4 says it is "carried … by R-e1 where dsh's recorded frames carry it" (design:769); §7.2's R-e1 lists only LQ3's ACP half and the conditional LQ7 (design:1099-1100). *Change:* add "LQ17's context half only where dsh's frames carry `usage_update`'s figures" to R-e1, mirroring LQ7's conditional.

**O5-F4 (not blocking; before 1f-i). The retry table has no native conversation row.** `ChatRetry` is handed only on the `ProtocolChat` branch (`ChatRunner.cs:1637`); `NativeChat` is built with no retry (`:1678-1691`); 1f-i binds that kind, but the table's native rows cover only a driven start or go-on and a resume (design:718-719). *Change:* add the row "native, a conversation: no episode, no go-on, the layer's delivery not read for the original words, phase `Result` or `Harness`", and a native conversation's transient failure, ending as today, to 1f-i's proof.

### 2. The three owner choices

D191 point 3 leaves none of these with the owner (none spends, signs in, publishes or loses data); all three are sound as stated.

1. **m-i's fifth member, the restart: sound.** It matches today's `AgainAsync` (the same `ProcessStartInfo`, the same keep list, `append: true`, `prompt: null`, `resume.Again()`). A second `AgentRun` would hand the records and carriers again at a second open, risking a second opening said and a second capture writer. The fifth member is narrower and dies at m-ii; `LayerRun` implements it at 1d, which does not wait for m-ii; 1d's proof holds "tried once and then `exited`" on both paths (design:1469).
2. **Asking Lyntai for the native conversation's naming (LQ17 widened) rather than correcting the `init` boundary: sound.** Today's boundary is D189's and tested (`Tests/Loop/NativeResumeTests.cs:80-92`); Lyntai keys on the id deliberately because "not every emitter sets" `init` (`StreamJsonAgentReader.cs:74`), so the need is a host's. The fallback is named: a correction reviewed against D189 before 1d. Risk: Lyntai may answer it cannot tell initialization apart for every emitter; then 1d's path is that correction. Unmeasured: whether any Claude CLI emitter announces a session by a frame other than `init`.
3. **D125's own row for native-door cooling: sound to keep it out of this design and 1d; file the row.** The defect is real (the same account picked into the same refusal until strikes park the quest); D125 gates it on evidence TOOL4b did not record (the native limit result "read and not measured", `D125.md:296-297`); no open row names it. File it as a D125 correction waiting on a recorded native limit (the recording spends an account: the owner's yes). 1d's proof follows whichever path is current (design:518), so its order against 1d is free.

### 3. Unmeasured

Whether a `<synthetic>` message streams deltas (O5-F2); whether dsh's frames carry `usage_update`'s figures (O5-F3); whether Lyntai can supply initialization evidence for every emitter (LQ17); whether the native stream sends a `rejected` frame at a limit; whether a usage read reaches the walk before the next start; whether a frame's `resetsAt` and its sentence agree to the minute; the vendors' access fields (LQ11); whether the installed usage servers tolerate the eager order; stdin passage and the tools per mode; the isolated runtime's writers; everything else §14 lists.

## Codex, round 5

*Codex's acceptance read of round 5, read-only, written without seeing the Opus maker's; kept verbatim (it has no headings of its own). Its `P:` lines are round 5's as it read them.*

`P` = `docs/2026-10-30-agent-lyntai-design.md`; `D/` = `src/Daoris.Desktop/Daoris.Desktop.Driver/`; `T/` = its sibling `Daoris.Desktop.Driver.Tests/`. Lyntai references use `L/Core/` for `src/Lyntai.Core/` and `L/Basic/` for `src/Lyntai.Providers.Basic/`.

| Round-4 finding | Acceptance disposition |
|---|---|
| **C4-1 — access evidence and account writes** | **Closed.** §19’s answer (`P:1925`) is carried into `P:521–540`, `P:627–629` and LQ11 (`P:1168`). Field evidence and the words’ readings remain separate; each reader retains its own precedence and writes. Conversation opens and prompts are included, with code-only and code-plus-matching-words cases. |
| **C4-2 — retry applicability** | **Closed.** `P:1926` is implemented in the table at `P:714–719`: driven quest runs, conversations and passes handed no retry are distinguished. LQ3 leaves repetition of the opening operation to the host (`P:1160`); the paired proofs are at `P:724–726` and `P:1471`. |
| **C4-3 — native naming evidence** | **Closed with a change I accept.** The design chooses the requested-evidence option (`P:1927`). LQ17 now distinguishes initialization from a later frame carrying an id and preserves the first accepted identity (`P:1174`). R-d includes it (`P:1098`), and 1d explicitly tests status-only failure, initialization and repeated/different ids (`P:1469`). |
| **C4-4 — model scope versus refusal** | **Closed.** `P:1928` points to the whole-outcome rule at `P:591–605`: `Fatal`, model subject, `OwnerAction` and the door’s refusal entry, then mapping by door and phase under the native no-work guard. Model-scoped limit and capacity negatives are required beside `model_not_found` (`P:1463`, `P:1469`). |
| **C4-5 — release prerequisites** | **Closed with a change I accept.** The split at `P:1261–1272` resolves `P:1929`: 1b-i uses R-a’s public members and explicitly labels tests of unavailable inputs as host-policy tests. Each later translation belongs to 1b-iii, tested through the layer before its first consuming move. The matching proof rows are `P:1463–1464`. |
| **C4-6 — remembered model** | **Closed.** `P:1930`, `P:758` and LQ17 (`P:1174`) correctly describe one remembered model updated by assistant messages, rather than necessarily the startup model or a per-model split. |

**Verdict: accept.** No named changes are required, and I have no new C5 findings.

I read the acceptance portions named by §14 whole and checked their source evidence using the *Read at* offsets. The account distinction holds: `AgentErrors.Read` selects the authentication code before sign-in words (`D/Agents/AgentErrors.cs:168–176`), while `OpeningAccount` distinguishes the resulting writes (`D/Loop/DriverContinuations.cs:617–630`). The ordinary conclusion and conversation independently invoke the sentence-based sign-out reader (`D/Loop/Driver.cs:3515–3518`; `D/Loop/Chat/ChatRunner.cs:2729–2741`). Its writes include the signed-out reading, the tool’s own sign-in hold where applicable, and its note (`D/Agents/RefusedAccounts.cs:25–33`). Round 5 preserves these differences.

The retry rows also match the source. Opening retries require an owner (`D/Agents/Acp.cs:1536–1540`); conversations receive that owner only after opening (`D/Loop/Chat/ChatRunner.cs:3255–3260`). Native naming remains initialization-only, with the first nonempty id retained (`D/Agents/StructuredOutput.cs:164`, `:179–183`; `T/Loop/NativeResumeTests.cs:80–92`), unlike Lyntai’s broader `SessionStarted` (`L/Basic/ClaudeCli/StreamJsonAgentReader.cs:66–79`). LQ17 addresses that difference without silently changing D189’s retry boundary.

The whole-outcome model rule correctly excludes Lyntai’s model-scoped limit and capacity outcomes (`L/Core/Inference/ProviderVerdictClassifier.cs:116–140`). It also preserves the separate protocol-open path. The 1b-i / 1b-iii split keeps host-policy proof separate from proof that released layer members supply the evidence; `P:1271–1272` makes each consuming move wait. This remains consistent with D213’s package direction.

Lyntai’s checkout reports `master` at `bbbe285b`. Nothing changed; no builds, tests or agent runs ran. The requested APIs’ implementation and live evidence remain **unmeasured**, including independent access evidence, emitter initialization behavior, frame-versus-sentence agreement, package closure/offline restore, container cost/concurrency and installed parity. The other runtime measurements listed in §14 remain **unmeasured in this read**. Acceptance settles the design; its release, translation and move proofs remain required.
