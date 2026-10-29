# AGENTLYNTAI1: the reads of round 1 (D191)

The two makers' reads of round 1 of `docs/2026-10-30-agent-lyntai-design.md`, each written read-only and apart, neither seeing the other's
before its own was written: the Opus maker's first, and Codex's beside it, added verbatim when it lands. Each finding names the line it rests
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
