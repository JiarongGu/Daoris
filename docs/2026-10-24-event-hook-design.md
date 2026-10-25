# The work cycle's events reach an outside agent's session by a plugin, pushed, never polled (EVENTHOOK1; proposed, 2026-10-24)

**Standing:** proposed, round 4, under D191. Round 1 was written by Claude Opus 5.5 from the code at `5e9a563bd` and read at
`71e08c3c0` by the independent Opus foundation reader alone, its read verbatim in `docs/2026-10-24-event-hook-reads.md`: 16 findings,
7 blocking, another round. Codex's read was then owed, its credits out. Round 2 is written by Claude Opus 5.5 from the code on main at
`5d19f6b3a`, where nothing round 1 or the read cites has changed (an empty `git diff --stat 5e9a563bd` over `src/Daoris.Desktop`,
`src/Daoris.Service`, `tools/knowledge-server.mjs` and `examples/plugins`). It answers every finding in §12, each checked against that
code. Round 2 was read at `f360ab375` by the Opus foundation reader alone: 12 findings, 2 blocking, accept after named changes. Its named
changes are taken by Claude Opus 5.5 from the code on main at `4de8a5099` (an empty `git diff --stat` since `f360ab375` over
`src/Daoris.Desktop`, `src/Daoris.Service`, `tools` and `examples/plugins`); §13 says where each went. Codex read round 2 with those
changes at `972b17577`: 21 findings, 16 blocking, another round, its read recorded verbatim in the reads record. **Round 3** is written by
Claude Opus 5.5 from the code on main at `972b17577`, where each of Codex's findings was checked and every line §1 names was read again;
§14 answers each finding by number, and the body says what each changed. It adopts Codex's smaller shape as the first build. Both
readers read round 3 at `c685459f4`: the Opus foundation reader, 12 findings, 4 blocking, accept after named changes; Codex, 12 findings,
7 blocking, another round; both reads are recorded verbatim in the reads record. **Round 4** is written by Claude Opus 5.5 from the code
on main at `b4f096cc0` (merged into this branch), where each finding of both reads was checked; §15 answers each by number. Where the
two reads overlap it takes the stricter, and it takes the Opus reader's finding 1 on the subscriber: the first build's subscriber is the
owner's own Claude Code session outside Daoris, and a role holder waits for a later round. Every blocking finding of both reads is taken
with a change a reader can check line by line, and none is left open. The Opus foundation reader read round 4 at `5c93bf118`:
10 findings, 2 blocking, accept after named changes the steward checks without another read. **Its named changes are taken** by Claude
Opus 5.5 from the code on main at `deb766f20` (merged into this branch), each checked there; §16 says where each went. Codex read round
4 with those changes at `eced2688e`: 10 findings, 4 marked blocking, of which its §3.1 and §5.2 are one hole, accept after named changes;
its read is recorded verbatim in the reads record. **Its named changes are taken** by Claude Opus 5.5 from the code on main at
`9f48860ba` (merged into this branch), where nothing it cites has moved since `eced2688e` (an empty `git diff --stat` over
`src/Daoris.Desktop`, `src/Daoris.Service`, `tools` and `examples/plugins`), each finding checked there; §17 says where each went.
**Both makers' reads of round 4 have landed, each asking accept after named changes, and both readers' named changes are taken (§16,
§17): the design is ready for the steward's acceptance of rows 1a, 1b and 1c.** By D191's note of 2026-10-24 this design, which sends
Daoris's events outside it, is accepted only by the steward after both makers' reads: it stays proposed until then, and nothing is built
before it. Dated notes amend D173 (the fabric events design's decision), D119 and D120, each proposed with this, each round's changes
appended where they change it.

The row EVENTHOOK1 in `TASKS.md` carries the case. The steward watches the install by polling its sessions in a shell loop it
re-arms every half hour. A plugin hooked into the work cycle should push the fabric's events (a session ended, waiting on the person,
failed, landed; a quest published or closed; a Confirm asked) to a subscriber outside Daoris, the first being a Claude Code session.
This is the first real test of a plugin listening to Daoris's event pipeline.

**What round 2 changed, in short.** The session kinds come from the attention watch, where a park is seen, not from `session/ended`,
where it never arrives (F1). *Landed* is its own kind, told where the install's landing keeps a branch it made, not `work.ended` (F2).
The inbox's variables are removed from every environment Daoris hands on (F3). The relay is no longer plugin code: it is Daoris's own
verb, holding none of the terminal's keys and pressing nothing (F4). The pipe's direction is reversed: the relay serves a pipe with
Daoris's instance pipe's access rules, so it answers at either elevation, and the plugin connects to it. The relay takes a connection
only from a process the holding driver started, and validates every frame against the one grammar the driver's census holds (F5, F6).
Only the lease's loop starts the plugin, and a trial's process is marked and refused (F7).

**What round 2's named changes changed, in short** (§13). The relay's ancestry check refuses strangers and never an agent, said plainly;
what keeps an agent's words out of the steward's session is that every generated id is checked in its minted shape and that names
filter and are never posted (R1). The relay holds no lease and runs from a copy of its build outside `app/`, so it neither keeps the
desktop's loop waiting nor stops an update (R2). The relay says when events may have been missed, a closed connection is opened again,
a real value outside the grammar is left out by the driver, the frame's fields come from where they are carried, `Tell` checks the
lease, a late plugin's restart backs off, `quest.closed` reads the closed state, and `events relay --once`, a background command whose
end wakes the session, is the first adapter, the inbox its upgrade (R3-R12).

**What round 3 changed, in short** (§14; C*s.n* is Codex's finding *n* of its section *s*). The first build is Codex's smaller shape:
the point with the four kinds that wake the steward, the plugin, and the relay's `--once` form alone, which prints one batch and ends,
so the steward stops polling. The inbox, the channel, the quest kinds and `person.answered` wait for later rounds (C6.1). No name leaves
the driver: the frame carries generated ids and closed codes, and its one scope is this home, which the person grants by switching the
plugin on (C2.1, C4.1). The relay filters, not the plugin, so a gap in `seq` is a loss and nothing else. The plugin's first line says
where it joins, and a relay whose watermark that line passes says so at once (C3.2, C3.3). A relay has one connection in its life and
ends as that connection ends, saying the gap; a role's terminal may start it; it is routed before the machine log opens and finds its
home by the `--home` its `--where` prints. Its copy is laid out, used and removed under rules a junction or a second call cannot break
(C1.1, C2.3, C5.3). The promise is narrowed to what an intact relay holds against the frames it meets (C2.2).

**What round 4 changed, in short** (§15; O*n* is the Opus reader's finding *n* of round 3, K*s.n* Codex's finding *n* of its section
*s*). The subscriber is the owner's own session outside Daoris, and `events relay` stays out of a role's terminal (O1). The plugin's
first line has a closed schema, checked before anything is printed, and every line the relay writes, stdout or stderr, is one of a
closed list of fixed sentences (O2, O3, K2.1). An event the driver cannot tell is told as an `events.missed` frame in its place, and
`seq` is minted whatever a lease read says (K3.1, O6). The plugin reads its subscribers' folder again every five minutes, as D173's
rule for a watcher asks, and at once when a connection ends; a relay that no plugin has reached within fifteen minutes while Daoris
runs ends and says so, and one that meets a second plugin ends and says so (K4.1, K3.2, O4). A copy is named by the hash of its whole
bundle, and the plugin's first line carries the build, so a stale copy ends at the first line of any other build (K2.2, O5). Step 3
asks whether the client is in the agents' job of the lease's holder, which a test's host and a version manager's shim both pass (O8).
Every batch ends with the relay's own watermark (O7). The variables' strip is a row of its own, and the installed proof another (O9, O10).

**What round 4's named changes changed, in short** (§16; P*n* is the Opus reader's finding *n* of round 4). Where the holder's job or
the client will not open, as across elevations it does not, step 3 takes the client untold and says so in one fixed line (P1). A park
is left out where the session is a conversation the person holds, `!ActiveSessions.HoldsSlot`, so the driver's own intakes, helpers
and reviewers are told (P2). M5 measures whether the re-arm runs unprompted, and §6.1 names the owner's allow rule (P3); the relay
keeps a second pipe instance listening (P4); §8 gives the steward a procedure for every line, and the no-events line a reason (P5); a
busy lease read keeps the last look's answer (P6); `--where`'s lines are fixed sentences too (P7); a build is removed by a rename first
(P8); the Process-lane plugin is started by the host's own reconcile (P9); and five smaller lines are corrected (P10).

**What Codex's read of round 4 changed, in short** (§17; C4-*s.n* is its finding *n* of its section *s*). After the attention watch's
first look, its baseline, each loop tells one `events.missed` frame with its own `seq`, an empty baseline included, so a park that the
baseline takes in unsaid is said as a gap with no later event (C4-3.1, C4-5.2). `events relay` is routed at the host's entry before the
home's adoption and the generic lease, so every entry, home and lease failure of `--where` and of the relay is a fixed sentence that
echoes neither a lease's words nor a path, the lease's behaviour unchanged (C4-2.2, C4-5.3). A batch holds at most one gap line and the
relay's diagnostics are tallied (C4-2.3); the loss promise names M5's kill measurement as its one limit (C4-3.3); the measurements
record says which outcome blocks and which limit is accepted (C4-5.1); and D119 gains the note its round-3 paragraph needed (C4-4.1).

A path without a root is under `src/Daoris.Desktop/`; `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is
`src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Host/` is `src/Daoris.Desktop/Daoris.Desktop.Driver.Host/`, and `Mcp/` is
`src/Daoris.Service/Daoris.Service.Mcp/`.

---

## 1. What is true today

Each statement was read at `5d19f6b3a` at the lines named, and those round 2's read added (§1.7a among them) at `4de8a5099`. Round 3
read every one again at `972b17577`, round 4 at `b4f096cc0`, and round 4's named changes at `deb766f20`, and the lines are given as
they stand at `deb766f20`. Between `972b17577` and `b4f096cc0`, of what §1 and its readers cite, `Host/Program.cs` (four lines after
line 194), `DriverLoop.cs`, `InstancePipe.cs` (after line 1002), `PipeCensus.cs` (three lines after line 228) and
`StandingConfirmation.cs` (its steward flag from `:293` to `:336`) moved; between `b4f096cc0` and `deb766f20`, `DriverLoop.cs` (after
lines 765 and 1226) and `Planner.cs` (six lines after line 275) (P10). What Codex's reads and the later reads added is in §1.3, §1.4,
§1.7, §1.7a and §1.7b. Codex's read of round 4 read the code at `eced2688e`, and its named changes were checked at `9f48860ba`, where
nothing §1 cites differs from `eced2688e`. Between `deb766f20` and `eced2688e`, `Driver.cs` (seven lines after line 71, five more
after line 1974 and three more after line 2367) and `DriverLoop.cs` (three lines after line 920) moved, so §1.3's `DriverLoop.cs:942`
is `:945`, §1.4's `Driver.cs:1405-1412` is `:1412-1419` and §1.3's `Driver.cs:2883-2893` is `:2898-2908` (C4).

### 1.1 The kinds of point, and the points

`HookPoints` names every point once (`Driver/Plugins/Domain/HookPoints.cs:24-88`). By kind:

| Kind | Point | What it is |
|---|---|---|
| decision | `quest/consider` | a planned start, a waterfall that fails closed (`:26-27`; `Driver/Plugins/Hooks.cs:1027-1059`) |
| observation | `session/ended` | what a tick concluded; contained (`HookPoints.cs:29-30`; `Hooks.cs:1061-1088`) |
| act | `work/land` | a landing's branch handed on, spoken by the landing, never the loop (`HookPoints.cs:32-37`) |
| query | `work/state` | a landed branch's pull request, asked at an occasion that may remove (`:39-44`; D148) |
| observation | `state/changed` | what a press changed, told off the press's path (`:46-51`; D169 point 9) |
| work wire | `work/watch`, `work/step`, `work/perform` | the fabric's performed steps, the lease's loop alone (`:67-87`) |

- **A point is additive.** A new point goes last in `HookPoints.All` (`:53-58`), and a handshake naming a point a build lacks is said
  once and never asked there (`:11-13`; D148 point 8). `protocolVersion` is 1 (`Hooks.cs:108`).
- **What a new point costs** is listed on the class (`HookPoints.cs:15-23`): its frame in `HookFrames`, its call on `IHookChannel`,
  `HookPeer` and `HookProcess`, its kit entry and sample, the kit test's `ANSWERS` entry, its case in `PluginTrials`,
  `PluginsCommand.Cost`, Ask Daoris's `PointSaid`, the rows of `PluginKitTests` and the modules' `DriverModulePluginsTests`, the
  page's gloss in `src/Daoris.Web/src/plugins/PluginKit.tsx`, and `twins.md`'s answer row. So a point's lanes are the driver, the
  modules and the web.
- **The loop's points** are `quest/consider`, `session/ended` and `state/changed` (`HookPoints.Loop`, `:60-65`): a plugin on one of
  them is kept running beside the ticks in every loop, the lease's or not; a plugin on the work wire only where this loop holds the
  lease (`Hooks.cs:702-710`).
- **A plugin asks Daoris nothing.** The peer's reader takes one notification, `work/step`, and only from a process that declared it
  (`Hooks.cs:524-531`). A request from a plugin is neither answered nor errored, by design (D64 §7).
- **The handshake carries the home and the data folder** (`initialize`, `Hooks.cs:147-150`), and nothing of the loop's run.

### 1.2 The hook set: how the loop tells

`HookSet` (`Hooks.cs:617-1216`) keeps one process per wanted plugin, reconciled at each look (`:687-829`), and stops each with the loop.

- **`session/ended`.** `Driver.EndingsAsync` tells each concluded `SessionEnded` to the set (`Driver/Loop/Driver.cs:1617-1644`),
  `HooksAtOnce`, 2, at a time under the watch (`:1635-1639`, `:1697-1700`). Each call waits for `HookPeer.DefaultPatience`, 10 s
  (`Hooks.cs:116-117`), and its answer is never read.
- **`state/changed`.** `Tell` writes the change to a bounded queue and returns (`Hooks.cs:1096-1100`): at most `ChangesWaiting`, 64,
  the oldest dropped. **Its teller is started at the first change told, whether or not any plugin listens** (`:1099`). It tells each
  change to every listening plugin at once and awaits them all before the next (`:1109`), so a plugin slow to answer holds the others
  for its patience. A headless loop has no bus and tells this point nothing (`HookPoints.cs:61-64`).
- **A live plugin that never answers is never replaced.** Reconcile replaces a channel that is gone, changed or no longer wanted
  (`Hooks.cs:725-729`), never one that is alive and late. The work wire alone counts unasked exits and marks a plugin unsound after
  `WorkBounds.ExitsUnsound`, 3 (`Driver/Plugins/Domain/WorkBounds.cs:25`).
- **Events with no listener are neither told nor counted** (`Listening`, `Hooks.cs:1196-1206`).
- **Health without words.** Each start, call, failure and stop is a `plugin.*` line in the machine log (`Hooks.cs:612-615`; D119 point 5).

### 1.3 The attention watch: where a park is seen

- **A park is no ending.** Every conclusion that parks returns no `SessionEnded`: a driven start (`Driver.cs:2883-2893`, *A park is not
  an ending (STANDDOWN2)*), an intake (`Driver/Loop/DriverIntakes.cs:414-416`) and a resumed conversation
  (`Driver/Loop/DriverContinuations.cs:832-834`). Nor is a run swapped onto a changed setup, or one waiting on its own helper. So nothing
  of a park reaches `session/ended`.
- **`AttentionWatch.Observe`** (`Driver/Quests/Domain/Attention.cs:210-261`) is what sees one. It compares the active sessions with the
  last look's and says, once each: `Parked` (a session newly waiting on the person, a park the person answered seen apart,
  `:194-195`), `QuestParked` (a quest parked on its failed sessions, `:239-244`), and `Ended` for each concluded ending except the
  person's own stops, an account's limit, a refused choice and an ending its quest's park already says (`:246-258`). `Waiting` is an
  account's cool-off, which names accounts (`:227-228`). The first look is a baseline and says nothing (`:224`).
- **Both loops call it after each look**: the shell's (`Modules/Loop/DriverLoop.cs:942`) and the headless host's in its watch mode
  (`Host/Program.cs:1003`), whatever the notify switch says (the switch is about being told). The host's `drive --once` and
  `--until-idle` never call it (`Host/Program.cs:961-975`). Each holds the loop's `HookSet` (`DriverLoop.cs:830`; `Host/Program.cs:956`).
- **What the watch's events carry** (`AttentionEvent`, `Attention.cs:45-58`): a kind, a session, a repository, a state and a note;
  `Quest` and `Strikes` for `QuestParked` alone; `Workspace` for `Ended` alone. `Observe` builds `Parked` without `SessionView.Quest`
  (`:235-236`) and `Ended` without the ending's quest or adapter (`:253-257`), though `SessionEnded` carries both.
- **The workspace** of an `Ended` is the one the session was filed under at the last look that saw it (`Attention.cs:256`); a
  `Parked` session's is on its `SessionView` (`Driver/Quests/Domain/Planner.cs:792`), and an `Ended`'s may be none
  (`Attention.cs:256`, a `GetValueOrDefault`). A `QuestPark` names a quest, a repository, its last session and its strikes
  (`Driver/Trees/SessionGroups.cs:563-570`), no workspace.
- **An intake's repository is `ask #<id>`** (`DriverIntakes.cs:385`, `:416`, `:454`; `Planner.cs:757`). `SessionEnded` carries no
  workspace, and its `Adapter` may be null (`Driver/Sessions/Domain/SessionEnded.cs:14-16`).
- **A conversation's end** goes through the chat runner's `onEnded` (`DriverLoop.cs:918-926`), never `EndingsAsync`; the watch's
  `Answered` is a turn the person's own words started (`Attention.cs:32-36`). **A conversation's park is reported** all the same:
  parks come from the look's active list, blind to how a session was started (`Attention.cs:143-149`). A record's kind, `driven` or
  `chat`, is on its `SessionView` (`Planner.cs:794-798`), and `chat` is also the kind the driver's own intakes, helpers and reviewers
  keep (`Planner.cs:854-855`). What tells the driver's own sessions from a conversation the person holds is
  `ActiveSessions.HoldsSlot` (`Planner.cs:872-885`): a session marked as an intake (`Ask`), a helper (`Serves`) or a reviewer
  (`Opinion`) holds a slot, an unmarked session of Ask Daoris's room does not whatever its kind, and otherwise a `chat` does not
  (round 4's named changes, P2).

### 1.4 A landing: where the install keeps a branch it made

- **Only the lease's holder lands** (`Driver.LandDueAsync`, `Driver.cs:1405-1412`): a `--share` loop drops its wakes.
- **Under the watch the landing runs beside the look** (`Driver.cs:1424-1438`): the choosing in the landing's pass (`SelectBeside`), the
  landing itself on `Context.Runs.Beside`. So a try is kept after the look that chose it, in no fixed order with the attention watch's
  observation of that look or the next.
- **Each try is kept once, with its code**, in `AutoLander.Tried` (`Driver/Trees/Landing/AutoLander.cs:648-672`): the due list's try,
  the session's note and the machine log's `LandingLine`. `landed` is a branch made and recorded, pushed where the rule's plugin names
  one; `advanced` a chain's branch moved on (`Driver/Trees/Landing/AutoLanding.cs:39-46`).
- **The end judgement** calls a landed branch the line does not hold yet `Handed` (`Driver/Trees/SessionTrees/EndJudgement.cs:263`),
  and `Ended` only once the line holds the work (`:241-258`). No chain event names `Handed`. Link 1 emits `work.held` alone
  (`Driver/Trees/RefWatch.cs:373`), and no open row makes it emit `work.ended`.

### 1.5 The plugin's process and its environment

`HookProcess.StartInfo` (`Driver/Plugins/HookProcess.cs:55-114`) starts the manifest's command:

- **in the plugin's install folder** (`:88`), its first word resolved by Tools (`:61-83`);
- **stdin and stdout the wire, UTF-8 without a mark** (`:90-97`); stderr relayed line by line under `plugin:<id>`;
- **over the environment the process inherits**: `DAORIS_PLUGIN_ID`, `DAORIS_PLUGIN_FOLDER`, `DAORIS_PLUGIN_DATA`, `DAORIS_HOME` and
  the tools' environment are set on top of it (`:101-106`). `Tools.Hand` calls `GitWiring.Hand` (`Driver/Agents/Tools.cs:679`, `:693`),
  which removes seven names from every child, the holder key among them (`Driver/Platform/GitWiring.cs:40-41`); a session's start
  alone also loses the four token variables (`:47`, `:98`). **Nothing removes `CLAUDE_CODE_MESSAGING_SOCKET` or
  `CLAUDE_CODE_MESSAGING_TOKEN`**: a Daoris started from a Claude Code session's shell hands both to its plugins and its agents.
- **Stripped at entry**: the desktop removes the holder key from its own environment first (`Daoris.Desktop.App/Program.cs:34`,
  `HolderKey.Strip`), and `daoris-driver` takes it into memory and removes it (`Host/Program.cs:357`, `HolderKey.Take`;
  `Driver/Sessions/Questions/SupervisorProof.cs:50-60`): the pattern for a variable one verb may read and nothing may pass on. Its name
  is `DAORIS_ROLE_KEY` since STEWARDROLE1a-i (`:23`).
- **inside the agents' job.** Its start goes through `AgentStarts` as `ContainedKind.Plugin` (`HookProcess.cs:141-146`;
  `Driver/Platform/AgentStarts.cs:5-11`): the launcher, `daoris-piece-start.exe` in the install's `app/bin/`
  (`Driver/Conversations/Partners/PieceLauncher.cs:29`, project `Daoris.Desktop.PieceStart`), is the process answered and the plugin its
  child, in the same job as the agents (D201 point 4). `AgentStarts` is the one route every agent, plugin process and piece starts by,
  so **an agent's harness is a direct child of the same launcher under the same driver as the plugin**, and a plugin's process and an
  agent's are one user's, in one job. The install's root executable, `Daoris.Desktop.Launcher`, starts the application from `app/` and
  exits (`Launcher/Program.cs:8-12`); it is no plugin's parent.
- **A start that cannot be contained is made all the same.** With no launcher laid out, a job refused or a name held, the plugin is the
  driver's direct child (`Agents/Consoles/AgentJobs.cs:46-49`, `:119-123`); a test's home has no launcher
  (`Conversations/Partners/PieceStarts.cs:16-20`).

### 1.6 A second process of the same plugin

- **A Try** on an installed plugin starts it with the installed entry, so in the same data folder (`Driver/Plugins/PluginTrials.cs:58-78`;
  the page's route `Modules/Bridge/Routes/PluginsRoutes.cs:399`). It is told the kit's sample frames (`Driver/Plugins/PluginKit.cs:18-25`).
- **A `--share` loop** (`Host/Program.cs:20-27`) keeps every loop-point plugin running beside the lease's loop (§1.1).

### 1.7 Pipes, owners and elevation

- **The instance pipe** is the exemplar (`docs/2026-10-16-terminal-rpc-design.md:145-152`): the user's SID alone, the network denied,
  `FirstPipeInstance`, owned by the user's SID at either elevation (`Modules/Platform/InstancePipe.cs:744-762`). Its client takes a pipe
  owned by its user's SID or its token's default owner, and asks the system which process serves it (`GetNamedPipeServerProcessId`,
  `Driver/Platform/InstanceClient.cs:105-114`, `:196-211`).
- **PIPEOWNER1** (`docs/FIX-LOG.md:1810-1828`): a pipe left to the token's default owner is the Administrators group's under an elevated
  token, and a client that is not elevated was refused. The install has run elevated. A pipe made by Node's `net` gets the system's
  default descriptor; Node has no knob to set an owner or a user-only ACL.
- **The elevation split, for pipes and for named jobs** (round 4's named changes, P1). A pipe made with `InstancePipe.Security` is
  owned by the user's SID at either elevation, so a client at either elevation opens it (PIPEOWNER1's fix), and an elevated process
  opens what one that is not elevated made, since an elevated token holds the user's SID enabled. **A named job is not so**: the
  agents' job is made with no descriptor (`Driver/Agents/Consoles/ProcessJob.cs:83-87`, `CreateJobObject(IntPtr.Zero, name)`), so it
  takes the token's default DACL, and *a Daoris that is not elevated cannot open an elevated one's named job; the reverse works*
  (`docs/2026-10-23-terminal-trust-design.md:253-255`). D201's judge answers `untold` there for that reason
  (`Modules/Platform/StandingCaller.cs:121`). So a relay that is not elevated cannot ask an elevated Daoris's agents' job anything,
  which is the layout PIPEOWNER1 records (the install has run elevated).
- **The lease** names its holder's process and start (`Driver/Platform/HomeLease.cs:31`, `:165`), checked alive against the system.
  It is read from `run/home.lease` in the home (`HomeLease.cs:53-64`, `:120`), a file any process of the user can rewrite.
- **`InstancePipe.Security` is the modules'** (namespace `Daoris.Desktop`, `Modules/Platform/InstancePipe.cs:13`, `:744`), and
  `daoris-driver` builds against the driver alone (`Daoris.Desktop.Driver.Host.csproj:14`); `InstanceClient` is the driver's. The
  knowledge server builds against `Daoris.Service.Core` alone (`Daoris.Service.Mcp.csproj:14`), so it reaches neither the driver's
  types nor the lease.

### 1.7a A verb's lease, the key it takes, and the swap

- **An unclassed verb holds the home's lease for its run** (`Driver/Platform/VerbLeases.cs:10-12`, `:97`; `Host/Program.cs:396-412`).
  The verbs that hold nothing are listed with their reasons (`VerbLeases.cs:13-35`), among them `chat` and `run-heavy` (`:58`, `:60`).
  The desktop's loop waits for an act's lease until "that one ends" (`DriverLoop.cs:1278-1285`). A verb that holds waits up to thirty
  seconds where the launcher's swap or a home's move holds the home, and beside a running instance acts holding nothing
  (`Host/Program.cs:392-399`).
- **Every `daoris-driver` verb takes the holder key into memory at entry** (`Host/Program.cs:357`, `HolderKey.Take`), read by
  `sessions supervise` and by `steward`, which is handed `HolderKey.Taken` (`:660`; round 4, K1.1).
- **`daoris-driver.exe` runs from `app/daoris-driver/`** (`tools/desktop-publish.mjs:367-368`), and the launcher swaps nothing while
  anything runs from `app/`: "something still ran from app/ after the application closed, so nothing was replaced"
  (`Launcher/Program.cs:72`, `:82-87`, `:168-169`). The knowledge connector's copy in the home's `bin/` is the precedent of a build run
  from outside `app/` (`Driver/Service/KnowledgeConnector.cs:153`), and CONNECTOR1 its warning: a copy no republish refreshed opened the
  shared store at an older schema (`:127-133`).

### 1.7b A role's terminal, the host's entry, a copy's home, and the open list (rounds 3 and 4)

- **A role's terminal runs only its roles' verbs and the open reads.** `HolderKey.Take` records that `DAORIS_ROLE_KEY` was present at
  the entry, whatever its value (`SupervisorProof.cs:54-60`, `:70`), and `RoleTerminal.AtEntry` (`Host/Program.cs:372`) refuses, exit 2,
  every verb `RoleTerminal.Runs` does not name by its exact prefix (`SupervisorProof.cs:97-106`: `roles brief`, `sessions supervise`,
  `steward`, `sessions --json`, `notices`, `history`). The steward's holder is a tab of Daoris's own terminal panel whose environment
  carries that variable (D209 point 2), so a command its session starts from its Bash meets this gate. **The holder's harness has a gate
  of its own** (round 4, O1): its start line, stated whole, allows `Read`, `Grep`, `Glob` and those verbs alone, by fixed command names
  (`Modules/Platform/HolderTerminal.cs:41-48`), and in a role's terminal every alone path refuses `role-terminal` (`D209.md:77-78`). The
  steward role is not offered in this build (`StandingConfirmation.cs:336`): today's steward is the owner's own session outside Daoris.
- **What the host does before a verb's own code.** After the gate it adopts the install's home where none is named
  (`Host/Program.cs:390`), takes the verb's lease (`:400-416`), routes `trace`, `git`, `workflow` and `run-heavy` before the machine log
  opens, each with its own catch, since that open prunes old files (`:420-445`), and then opens it (`:449`; `MachineLog.Open` prunes,
  `Driver/Platform/MachineLog.cs:80-85`).
- **A program finds its install only under `app/`.** `DaorisHome.InstallOf` walks up from the program's folder to an `app/` whose parent
  holds `INSTALLED.md` (`Driver/Platform/DaorisHome.cs:62-80`), and `Adopt` takes that install's `data/` as the home (`:94-103`). A copy
  outside `app/` finds no install, and no home unless one is named. **The home's `bin` entry** (`:400`) is placed by `DaorisHome.Entry`
  (`:119-127`): for an install's home, under the install's own folder, its new place is `cache/bin` there and its old place `data/bin`,
  taken only where it holds something and the new place does not (`FolderOf`, `:170-183`; `Choose`, `:265-275`; round 4, O11, K1.2).
  That is how the knowledge connector's copy is found (`KnowledgeConnector.cs:153`).
- **An agents' job is named by its Daoris process** (round 4, O8). Each Daoris process makes its agents' job as
  `Local\daoris-agents-<its process id>` (`Agents/Consoles/AgentJobs.cs:77`; `PieceLauncher.AgentsPrefix`), and every launcher start,
  a plugin's among them, joins it (§1.5); a start joins the jobs of the process that makes it (`AgentStarts.cs:33`,
  `AgentJobs.Current`). `ProcessJob.NamedHolds` opens a job by its name for query alone and asks whether it holds a given process,
  answering null where the job or the answer will not open (`Agents/Consoles/ProcessJob.cs:121-137`); `OpenHolding` opens for query and
  terminate (`:140-155`). **Across elevations the open fails one way** (§1.7): a relay that is not elevated gets null for an elevated
  holder's job. A lease's holder says whether it is an instance, a loop that holds the home for its life (`LeaseHolder.IsInstance`,
  `HomeLease.cs:31-34`).
- **A lease read can be busy, and `HeldHere` answers it as not held** (round 4's named changes, P6). The lease's read tells a file that is
  absent from one that would not read this moment (`Read(path, out busy)`, `HomeLease.cs:360-377`), but `HeldHere` reads the file at
  every call and answers false on either (`:195-202`, `:303-304`). The reconcile reads it once per look (`Plugins/Hooks.cs:696`), and a
  plugin at a loop point that holds only where the lease is held is unwanted on a false read and stopped (`:705-709`, `:725-749`).
- **The work wire's exit bookkeeping is the work wire's alone** (round 4, O11). The hook set counts an unasked exit, holds the back-off
  and marks a plugin unsound only where `WorkPlugins.Speaks` is true (`Plugins/Hooks.cs:692-711`, `:786-791`).
- **A plugin's switch, and its install, are the person's while Daoris runs.** `PLUGIN_ACTION` and `PLUGIN_ADD` (*Install from a
  folder*, `Modules/Bridge/Routes/PluginsRoutes.cs:186-190`) are `Person` routes (`Modules/Platform/PipeCensus.cs:240`, `:289`); while
  Daoris is closed, `daoris plugin` acts alone (`VerbLeases.cs:92`). `event-relay` is no offer of the install's
  (`tools/desktop-publish.mjs:227`). An installed plugin's data folder is `plugins/.data/<id>` under the home's `plugins` entry, as the
  plugin catalogue reads it (`Plugins/Domain/PluginCatalog.cs:39-40`, `:286`), which the driver hands the plugin as `DAORIS_PLUGIN_DATA`
  (`HookProcess.cs:104`).
- **The open list keeps a held quest.** `/api/quests` lists Open, Taken, and any quest with `held = 1` whatever its state
  (`Daoris.Service.Core/Quests/Quests.cs:3990`), so a closed quest a departure holds stays in it; `/api/quests?includeClosed=true` is the
  whole list, any filtering done after it is read (`Driver/Service/ServiceClient.cs:157`).

### 1.8 The pipeline the window reads, and the fabric's events

- **`STATE_CHANGED` says what moved, never what it is now** (`Modules/Platform/StatePublisher.cs:99-106`; D169 point 3). Shell-only
  (D169 point 4).
- **NOBLOCK1** moved answers off the window's path and added no event a plugin hears (`Modules/Bridge/DriverModule.cs:501-512`).
- **The notices record** (`Driver/Platform/Notices.cs:6-26`) keeps the attention watch's events on the machine.
- **`ChainEvents`** (`Driver/Platform/Chain.cs:5-61`) holds the wakes (`refs`, `home`, `ledger`, `nodes`, `press`, `focus`,
  `plugin.step`, `harness`, `exit`, `timer`) and the end judgement's facts (`work.step`, `work.held`, `work.ended`, `work.tidied`).
  An event is a wake, never a fact (the fabric events design §A1 point 1, `docs/2026-10-16-fabric-events-design.md:50-52`).
- **§C4's `work/ended` point was never built** (`Chain.cs:170`; D173's FABRIC-CHAIN2 note, `docs/decisions/D173.md:399-401`;
  `docs/task-archive.md:15807-15811`).

### 1.9 What an event carries today

| Point | Frame (`HookFrames`, `HookPoints.cs:103-175`) | Free words or machine paths |
|---|---|---|
| `session/ended` | `session`, `quest`, `repository`, `state`, `adapter`, `account`, `byPerson`, `note` (`:114-125`) | `note` is the conclusion's sentence, on a failure an exception's message (`Driver.cs:2955`), which may name a path; `account` a credential profile's name |
| `state/changed` | `scopes`, `sessions` (`:166-174`) | none |
| `quest/consider` | the quest's id, title, from and to, `repository`, `workspace`, `root` (`:105-112`) | `root` is a checkout's path; `title` is authored |
| `work/land`, `work/state` | the branch, `base`, `title`, `quest` with its title, `session`, the commits' subjects, `pullRequest`, `acceptedBy`, `tip`, `root`, `origin` (`:127-164`) | `root`; titles and commit subjects |

The attention watch's events carry a `Note` (a sentence) and, for a park, `NoteParts` (`Attention.cs:236`).

**The ids as minted, the names as stored.** A session id is eight lowercase hex characters (`Daoris.Service.Core/Sessions/Sessions.cs:799`).
A quest id is twelve, or six for a quest from before the widening (`Daoris.Service.Core/Quests/Quests.cs:704-707`, `:1238-1245`). An
ask id is six (`Daoris.Service.Core/Asks/Asks.cs:403-408`). A pipe confirmation's id is thirty-two where the confirmations mint it
(`Modules/Platform/PipeConfirmations.cs:324`); one the pipe minted before its record is passed in (`:307-311`).
Names are not shaped: Ask Daoris's room is the repository `daoris:help` (`Driver/Help/HelpRoom.cs:48`), and a workspace name is only
trimmed (`Daoris.Service.Core/Registry/Workspaces.cs:26-28`), so it may hold a space, a colon, or the shape of a path.

### 1.10 The outside session today

The steward watches by polling (the row). Daoris pushes nothing to a process it did not start: the bus is the shell's (D169 point 4),
and the hook wire speaks only to the plugins the loop started.

## 2. What Claude Code documents for waking a session without a poll

Read 2026-10-24 from Claude Code's documentation (`https://code.claude.com/docs/en/cross-session-messaging`,
`https://code.claude.com/docs/en/channels` and `https://code.claude.com/docs/en/channels-reference`). Nothing here was run on this machine.

### 2.1 The cross-session inbox

- **The socket.** A session with messaging on binds an inbox: a Unix domain socket on macOS and Linux, **a per-session named pipe on
  native Windows**. Messaging is on with nothing to enable from v2.1.234 on native Windows (v2.1.224 elsewhere). `/status` shows it
  as `Peer address`.
- **Handed to the session's own children.** Claude Code exports `CLAUDE_CODE_MESSAGING_SOCKET` and a per-session
  `CLAUDE_CODE_MESSAGING_TOKEN` to its hooks and Bash commands, before any hook runs, `SessionStart` included. Whatever those children
  start inherits them unless something removes them (§1.5).
- **Posting.** A poster sends `{"type":"auth","token":"<token>"}` as its first line. **On native Windows the line is required**, and the
  token is the only verification. A connection that has not sent a complete line within 30 s is closed. The documentation gives the
  auth line's shape and not the message line's.
- **Delivery.** During a turn the message is read between tool calls; **an idle session starts a new turn with it**. Once delivered it
  counts toward usage as a typed prompt does.
- **Own-child messages.** A message verified as from the session's own child is delivered; unverified, it asserts no permission class,
  so a session that bypasses permission prompts holds it for approval.
- **Limits.** Plain text; about a million characters; a rapid burst is refused at the sender; identical repeats within a short window
  are dropped; repeated messages per sender are rate-limited; at most 50 accepted messages queue; at most 100 are held.
- **What a message may do.** It cannot approve anything, cannot change configuration, and a command in its text is not run.

### 2.2 MCP channels

- **Research preview.** A channel is an MCP server over stdio declaring `capabilities.experimental['claude/channel']` and emitting
  `notifications/claude/channel` with `content` and `meta` (identifier keys only). Claude Code acknowledges nothing.
- **Registration.** Only an allowlist's plugins with `--channels`; any other server needs `--dangerously-load-development-channels
  server:<name>`, which **works only in an interactive session**. Organization policy can block every channel.
- **Protocol.** Claude Code does not register a channel server that negotiates revision 2026-07-28; the knowledge server falls back
  to `initialize` (`Mcp/Program.cs:168-170`).
- **Windows support is not stated.**

### 2.3 What is unmeasured on this machine

Everything in §2, and the facts round 2 rests on: whether a process started from the session's Bash, and its children, hold the two
variables; whether the relay's pipe is reached from a plugin at either elevation, and the relay can read its client's process and
ancestry across elevations; the sustained per-sender rate at the relay's own pace; the launcher relay of the knowledge server.
**EVENTHOOK1a measures what the first build rests on before anything depends on it** (§7); the inbox's and the channel's
measurements wait for their own rounds (§6.2).

### 2.4 A background command's end: the wake already in use

A third way wakes a session without the inbox or a channel, and this machine already relies on it: a command the session started in
the background ends, and its end and its output reach the session as a new turn. The steward re-arms its shell loop every half hour
this way (the row EVENTHOOK1 in `TASKS.md`, cited by its name since its line moves with the backlog). A relay that prints its lines
and ends at its first batch (`events relay --once`, §6.1) wakes the session so, and depends on none of the inbox's undocumented line, its own-child class or its rates (M2-M4). It is the first build's
one adapter (round 3, C6.1); the inbox and the channel wait for later rounds (§6.2).

## 3. The shape

```text
 the loop (lease holder)                 the plugin `event-relay`                outside Daoris
 ───────────────────────                 ──────────────────────────               ───────────────────────────────────────
 AttentionWatch.Observe ─┐                                                       relay: `<copy> events relay --once`
 AutoLander.Tried ───────┴─▶ HookSet.Tell ─▶ its teller ─▶ hook/work/event         (a background command of the session,
                             one lock,        │ answers at once                     run from its copy outside app/, holding
                             per plugin       │                                     no Daoris key and no lease, no press)
                                              ▼                                    serves a user-owned pipe, writes
                              watches subscribers/ ─▶ connects ──▶ pipe ──▶ checks    subscribers/<pid>.json
                              in its data folder,     every frame              the client, validates and filters
                              no filter               unchanged                each frame
                                                                                  │ one batch of fixed lines
                                                                                  ▼
                                                                             stdout, and the relay ends ─▶ a new turn
```

1. **The driver tells one new observation point, `work/event`,** to the plugins that listen, off every path that answers anyone,
   from the lease's loop only. In the first build its kinds are the four that wake the steward, and a frame carries generated ids and
   closed codes, never a name (§4).
2. **A plugin, `event-relay`, carries every frame unchanged to each subscriber.** It watches a folder in its data folder where a
   subscriber says it is there, and connects to each subscriber's pipe as a client (§5). It filters nothing.
3. **The subscriber is Daoris's own verb, `events relay --once`,** started as a background command by the owner's own Claude Code
   session outside Daoris, today's steward (round 4, O1; a role holder is a later round's, §6.2). It serves the
   pipe, refuses a stranger (never an agent, which it cannot tell from the plugin), validates every frame against each field's minted
   shape, keeps the kinds it was asked for, prints one batch of fixed lines over ids, and ends; its end wakes the session (§6.1). The
   inbox and the knowledge server's channel wait for later rounds (§6.2).

**Why the subscriber serves and the plugin connects.** A pipe Node makes has the system's default descriptor, which an elevated
plugin's pipe gives to the Administrators group and a relay that is not elevated cannot open (PIPEOWNER1, §1.7). The relay is Daoris's
own .NET code and serves its pipe with the instance pipe's rules: the user's SID owner and alone, the network denied. A plugin at
either elevation holds that SID, so it connects to that pipe as the reverse case does (§1.7).

**Why the relay is Daoris's verb, not the plugin's script.** The outside session runs the relay outside every Daoris job, where D201
point 4 covers a terminal's presses with the standing confirmation, and with the terminal's environment. A script shipped in the plugin's
installed folder would be code that any package update rewrites, run uncontained, and Node cannot serve a pipe across elevations (F6).
The verb is the install's own build, the one every terminal verb already is, laid out from `app/` by `--where`, in no plugin's folder.
Any process of the user can still rewrite either file, which is why the relay's promise holds only while its files are intact (§6.1,
round 3, C2.2). It removes every Daoris key from
its environment at entry, drops the holder key the host took, opens no door of Daoris's, and makes no press (§6.1). It holds no lease
and runs from a copy of its build outside `app/`, so a relay that waits for hours neither keeps the desktop's loop waiting nor stops an
update (§1.7a, §6.1 step 0).

**Why it prints and ends** (round 3, C6.1). The steward re-arms a shell loop every half hour today. A background command whose end
wakes the session is the wake it already relies on (§2.4), and a relay that ends at its first batch needs none of the inbox's token,
line shape or rates, so it keeps no other tool's secret. Each end is one turn: the steward reads the truth through its own doors and
starts the next relay with `--since`, a wait re-armed at each wake, never a clock (§6.1 step 5). An inbox would let one relay live for
the session's life; that upgrade is a later round's to weigh (§6.2). The inbox's variables are still kept out of everything Daoris
starts (§6.3).

**Why a plugin at all.** It is D64's one way to extend the loop without code loaded into it, and D169 point 9's way a plugin observes the
pipeline: beside it, never able to hold, change or refuse anything. Carrying events to subscribers stays outside the driver, where a
person can switch it off on the Plugins page.

## 4. The hook point `work/event`

### 4.1 The point

- **An observation and a loop point, told by the lease's loop alone.** `work/event` joins `HookPoints.All` last and `HookPoints.Loop`,
  contained as `session/ended` is and told off the path as `state/changed` is. Unlike the other loop points, a plugin on `work/event` is
  wanted only where this loop holds the lease (the work wire's rule, `Hooks.cs:708`), so a `--share` loop starts no second process of
  it (F7). `protocolVersion` stays 1; an older build ignores a plugin's claim to the point (D148 point 8).
- **The lease is checked where the event is told, not only where the plugin is wanted** (R8). A plugin that declares `work/event`
  beside another loop point is wanted in a `--share` loop too (`Hooks.cs:705-708`), and both loops observe. So `Tell` at `work/event`
  tells nothing where the set does not hold the lease; a `--share` loop's process of such a plugin hears no event. **What says the set
  holds it is the reconcile's read of the lease, once per look** (`Hooks.cs:696`), kept on the set, never a read of the lease file per
  event, and **`Tell` mints the `seq` whether or not it tells** (round 4, O6). **A read that is busy changes nothing** (round 4's named
  changes, P6): `HeldHere` answers a busy read as not held (§1.7b), which today would make the plugin unwanted at that look and stop it
  (`Hooks.cs:705-709`, `:725-749`). So 1b asks the lease by a read that tells busy apart (the lease's own `Read(path, out busy)`), and
  a busy answer keeps the last look's: the plugin stays wanted and told, and only a read that answers *not held* moves either. Were a
  holding ever lost between looks, every `seq` is still minted, and the next event told shows the gap.
- **One point, many kinds.** The kinds are values of one frame's `kind`, and a kind is added as a value, never as a point.
- **The handshake says where the plugin joins.** For a plugin at `work/event`, `initialize` gains
  `{ "events": { "run", "seq", "grammar", "build" } }`: the set's `run`, the last `seq` it minted (0 before any),
  `WorkEventGrammar.Version`, an integer raised whenever a kind or a field's shape changes, and the build of the install's relay
  (`RelayBuild.Of`, §6.1 step 0; round 4, O5, K2.2), which the set computes once, before it starts its first plugin at the point, and
  which is 64 zeros where the set finds no install. A field a plugin does not read changes nothing (F8). A trial's handshake says
  `run: "sample"` (§5.4).
- **It replaces §C4's `work/ended`.** The unbuilt point becomes a later kind of this one, behind a row that owns link 1's emission (§4.2).

### 4.2 The kinds

Named as §C5 names events, `<subject>.<what>`. Each says what moved, never what it is now. The first build tells the four that wake the
steward (round 3, C6.1); the others wait for later rounds.

| Kind | Says | Source | Row |
|---|---|---|---|
| `session.parked` | a session newly waits on the person | the attention watch's `Parked` (`Attention.cs:231-237`), the shell's loop and the headless host's watch mode after each look | 1b |
| `quest.parked` | a quest parked on its failed sessions here | its `QuestParked` (`:239-244`) | 1b |
| `session.ended` | a session ended in a state the person did not ask for: `completed`, `failed`, `stopped`, … | its `Ended` (`:246-258`), which leaves out the person's own stops, an account's limit, a refused choice, and an ending its quest's park says | 1b |
| `work.landed` | the install landed a session's work as a branch the line does not hold yet, or moved a chain's branch on | `AutoLander.Tried` where its code is `landed` or `advanced` (`AutoLander.cs:648-672`), the lease's holder alone (`Driver.cs:1407`) | 1b |
| `quest.published`, `quest.closed` | a quest was published, or closed as `Done` or `Declined` | **not the look's snapshot** (C5.4): the open list keeps a held quest whatever its state (§1.7b), a quest published and closed between two looks is in neither list, and a reopened quest would look new. 1d is designed again from the quests' recorded operations before it is built | 1d, a later round |
| `person.answered` | a terminal's press the person was asked to Confirm was confirmed, refused or withdrawn | the shell's pipe, as a confirmation ends (CONFIRMWAIT1's record of it, its row in `TASKS.md`), its id's shape read again then (§1.9) | 1d, a later round |
| `work.ended` | the line holds a unit's work | link 1's emission of `work.ended`, which no row owns yet (§1.4) | a later row |

- **The row's six things.** *Waiting on the person* is `session.parked` (and `quest.parked`); *ended* and *failed* are `session.ended`
  with its `state`; *landed* is `work.landed`, a branch the install landed and nobody has merged yet (`Handed`). These are what the steward
  polls for today, so the first build ends the poll. *A quest published or closed* and *a Confirm asked* (as `person.answered`: a press
  only the person makes is no wake for the steward, its answer is) wait for 1d's round.
- **Told once.** The watch says each park, quest park and ending once, and its first look is a baseline (§1.3), so a restart floods
  nothing. `AutoLander.Tried` keeps each try once.
- **What the baseline takes in is said as a gap** (Codex's read of round 4, C4-3.1, C4-5.2). The plugins start at the first look's
  reconcile (`Driver/Loop/Driver.cs:886-889`), before that look reads its sessions (`:924`), and that look's `Observe` keeps the whole
  first observation as its baseline and says none of it (`Attention.cs:221-224`). So a relay can take the new `run` at once, the steward
  read the sessions again, and a session park before the snapshot and enter the baseline unsaid, with no `seq` minted and the
  connection healthy. So **after every `Observe` that was a baseline, its caller, each of the two loops, tells one `events.missed`
  frame** (§4.3) with its own `seq`, whether or not the baseline held anything, since no relay can tell an empty baseline from a full
  one. `AttentionWatch` says whether its last `Observe` was its baseline, so the caller never takes an empty answer for one. It is told
  after every baseline rather than once a `run`, so a watch made again is covered too. A relay meets it as a gap (§6.1 step 5), and the
  steward reads the truth again, now after the baseline; every later change is the watch's own event. The host's one-look runs take no
  baseline (§1.3) and tell no such frame.
- **One scope: this home** (round 3, C3.1, C4.1). Every event the lease's loop tells is of the home it drives, whatever its workspace or
  repository, so each kind's scope is known at its source, a `QuestPark` that names no workspace included, and none is unknown. The
  first build has no narrower scope (§4.3).
- **Not told:** the watch's `Waiting` (it names accounts), a conversation's end and the watch's `Answered` (the person's own, through
  the chat runner, §1.3), the person's own landing by *Accept* (their own press), and **the park or ending of a conversation the person
  holds** (round 4, O11; round 4's named changes, P2): a session for which `!ActiveSessions.HoldsSlot(session)` (`Planner.cs:872-885`),
  judged on the `SessionView` the watch last saw of it, `Observe` keeping that beside its workspace. That leaves out an unmarked session
  of Ask Daoris's room whatever its kind, and a `chat` no mark names; it keeps the driver's own intakes, helpers and reviewers, which
  are `chat` records too (`Planner.cs:854-855`), so an intake's park is told with its `ask` (§4.3) and a helper's and a reviewer's parks
  are told. Round 4 judged by the record's kind alone, which dropped those (P2). A conversation the person holds is something the person
  is in, the reason the watch gives for leaving its end unsaid (`Attention.cs:143-149`), and with no name in the frame a subscriber
  could not leave one out. The watch still says such a park in the window; `work/event` does not tell it. A later row may add a kind;
  none changes a point.
- **Latency and order, honestly.** Every kind but `work.landed` is told at the look's end, after the watch observes: up to
  `pollSeconds`, 15 s by default, after the fact, and sooner where a wake runs the look. `work.landed` is told where the landing keeps
  its try, which under the watch runs beside the look (`Driver.cs:1424-1438`), so before or after the watch's events of the same look
  (C1.2). `seq` orders what was told, never what happened. Pushing it is still no poll by the subscriber.
- **A headless one-look run tells `work.landed` alone.** The host's `drive --once` and `--until-idle` never observe (§1.3), so they tell
  no session or quest park and no ending; their landings are told as any lease holder's are.

### 4.3 The frame: generated ids and closed codes, never a name, a secret or a machine path

```json
{ "run": "6c0f1a9e2b47", "seq": 12, "missed": 0, "kind": "session.parked", "at": "2026-10-24T11:02:03Z",
  "session": "4b62a9fe", "quest": "48f8ed9121ec" }
```

- **A closed list of fields per kind**, built in `HookFrames.Event` and nowhere else, every name spelled there, and **one grammar**,
  `WorkEventGrammar`, which both the driver's census and the relay's check read (§6.1). Every field's type, kinds and need (C5.2):

| Field | Kinds (`?` optional) | Shape |
|---|---|---|
| `run` | every | `^[0-9a-f]{12}$`, minted by the set from the system's random source, never twelve zeros; `"sample"` in a trial (§5.4) |
| `seq` | every | an integer from 1, the set's count in this `run` |
| `missed` | every | an integer from 0 |
| `kind` | every | the four kinds of §4.2, and `events.missed` (below) |
| `at` | every | an ISO 8601 UTC time |
| `session` | `session.parked`, `session.ended`, `work.landed`; `quest.parked?` | `^[0-9a-f]{8}$` |
| `quest` | `quest.parked`; `session.parked?`, `session.ended?`, `work.landed?` | `^[0-9a-f]{12}$`, or the older `^[0-9a-f]{6}$` |
| `ask` | `session.parked?`, `session.ended?`: present only where the watch's repository is `ask #<id>` (§1.3) | `^[0-9a-f]{6}$` |
| `state` | `session.ended` | the ending states the ledger writes, a closed set the grammar names |
| `adapter` | `session.ended?`: absent where the ending names none (`SessionEnded.cs:16`) | this build's adapter names (`AdapterSet.Built().Names`) |
| `strikes` | `quest.parked?` | an integer from 0 |
| `code` | `work.landed` | `landed` or `advanced` |

- **What a value outside its shape does** (R5, C5.2; round 4, K3.1). `HookFrames.Event` checks each field as it builds the frame. An
  optional field whose value fails, or whose source holds none, is left out. **An event whose required field fails is told as an
  `events.missed` frame in its place**: its own `run`, `seq`, `at` and `missed`, kind `events.missed`, and no other field, and a new
  machine-log line, `event.unsent`, names the kind and the field, never the value. So the loss is said by its own frame at once, the
  last event of a run included, with no later event, disconnection or restart needed. A frame the relay meets that fails the grammar
  was written by something other than the driver. **The same frame, with no `event.unsent` line, follows the attention watch's
  baseline** (§4.2; C4-3.1): `events.missed` says *read the truth again* for either reason, and the relay need not tell them apart.
- **Each field from the source that carries it** (R6). `AttentionEvent` carries neither of what the frame names for a session (§1.3),
  so 1b has `Observe` carry them: `session.ended`'s `quest` and `adapter` from the concluded `SessionEnded`, and `session.parked`'s
  `quest` from `SessionView.Quest`. `quest.parked`'s `quest` and `strikes` are already on it. `work.landed`'s `session` and `quest` are
  the due entry's (`AutoLander.cs:648-672`).
- **No name** (round 3, C2.1, C7.1). `repository` and `workspace` leave the frame, and so do `to` and `from` of the later quest kinds. A
  name is free text the registry only trims (§1.9), so it may be shaped like a path or hold words, and round 2 sent it unchanged to
  the plugin and every subscriber, though no relay posted it. Kept in the driver, it crosses no boundary at all. The steward reads names
  through its own doors (§8).
- **No words.** `Note`, `NoteParts`, a title, a commit subject, a press's words: never. A sentence may name a path (§1.9), and authored
  words handed to an agent's session are a prompt someone else wrote.
- **No machine path, no secret.** `root`, `origin`, `account`, a home, a key: never. Every value is a generated id in its minted shape,
  a code from a closed set, a count or a UTC time. None of the id shapes holds a word, so no id carries a sentence such as
  `Merge-every-landed-branch.now` (R1). A frame is at most 4 KB. *Path-free* is said of the frame and of what the relay prints, not of
  all plugin traffic: the handshake hands every plugin the home and its data folder, as it always has (`Hooks.cs:147-150`; round 4,
  K2.3).
- **Who may hear it** (round 3, C4.1; round 4, O11). The first build's one scope is this home (§4.2). What rests the scope is what the
  frame holds: the opaque level of D173's ADR-B, an id and a code whose referent is read through the subscriber's own doors under that
  reader's own permissions (§8), so the frame tells a process of this user nothing the home's own files do not already hold. The
  person's switching `event-relay` on, whose page says its events go to programs on this machine (§5.1; D119's note), is the person's
  press while Daoris runs (`PLUGIN_ACTION`, a `Person` route, §1.7b); while Daoris is closed `daoris plugin` acts alone, so the switch
  is not what the scope rests on. A scope narrower than the home waits on a subscriber that needs one, and is then a grant the person
  makes for the plugin, enforced in the driver before the frame is built, never a subscriber's filter (§10).
- **Held by a test, seen failing**: a census over `HookFrames.Event` for every kind checks each frame against `WorkEventGrammar`, and
  fails on a `note` added, on a `repository` or `workspace` added and on an id outside its shape; it sees the park and ending of a
  conversation the person holds (`!ActiveSessions.HoldsSlot`: a help room's, an unmarked `chat`) not told, and an intake's, a helper's
  and a reviewer's parks told, each a `chat` record (P2); a word-shaped `session` told as an `events.missed` frame with its `seq`; and
  an `adapter` outside the build's names, or none, left out of a `session.ended` that is told.

### 4.4 Order, the rate bound, and a plugin that hangs

- **One order per plugin, under one lock** (round 3, C3.3). `Tell` is called from the look's end and from the landing beside it
  (§4.2), so at once. It takes one short lock, stamps the event with the set's `run` (random, minted when the set is made) and the next
  `seq`, writes it to **each listening plugin's own queue**, and lets go. A queue's write never waits (a full queue drops its oldest),
  so no caller waits on a plugin, and every queue holds its events in `seq` order. Each plugin's teller tells its events in that order
  and awaits each answer before the next. So every plugin hears every event in `seq` order, and a slow plugin holds only itself (F9).
- **A plugin joins at an exact `seq`** (C5.2). Its queue is attached, and its handshake's `seq` read, under the same lock, so the
  first event it hears is the one after that `seq`, and none minted between the two is lost unseen.
- **Bounded per plugin.** At most 256 events wait for one plugin (the work wire's `EventsWaiting`, `WorkBounds.cs:13`), the oldest
  dropped first. A drop is never silent: the next event told to that plugin carries `missed`, its own count dropped since its last.
- **A plugin that hangs is replaced, and its restart backs off** (R9). An answer not given within the patience (10 s) is late, and
  each is the per-call `late` line that already exists (`Hooks.cs:412-415`). After 3 late answers in a row at `work/event`, the set
  stops the plugin and logs it `plugin.stopped` with the reason `late`. That stop counts as an exit Daoris did not ask for, under the
  work wire's bounds (`WorkBounds.cs:21-27`): the next start waits 1, 5 and then 15 minutes (`Backoff`), and 3 such exits within an
  hour (`ExitsUnsound`, `ExitWindow`) mark the plugin unsound until the person tries it again. So a plugin late at every look is not
  restarted at every look. A restarted plugin is handed the current `run` and `seq` (§4.1). **That bookkeeping is the work wire's alone
  today** (§1.7b): 1b keeps it for a plugin at `work/event` too, its exits counted, its back-off held and its start refused while it
  waits, keyed by the plugin's id as the work wire's are, where `Hooks.cs:692-711` and `:786-791` now ask only `WorkPlugins.Speaks`
  (round 4, O11). **What is counted is the late stop and an exit of the plugin's own, and nothing else** (round 4's named changes, P6):
  a stop the reconcile makes because the plugin is no longer wanted (switched off, removed, updated, the lease no longer held) is no
  unasked exit, so it neither starts a back-off nor moves the plugin toward unsound.
- **What it is not.** No replay and no store: an event is a wake (§1.8). The `seq` counts every event the set minted; events minted
  while no plugin listened, and any still in a plugin's queue when it stopped, are counted by the next handshake's `seq`, so the last
  event before a stop is never lost unseen (F8, C5.2). The watch's first look is a baseline (`Attention.cs:224`), so what happened while
  Daoris was closed is no event at all: only the new `run` and the baseline's `events.missed` frame show it (§4.2; C4-3.1).
- **A loss is a loss, and nothing else** (round 3, C3.2, C3.3). The plugin filters nothing (§5.3), so a relay hears every frame of the
  `run` and judges loss by `seq` alone: a `seq` past the next, a `missed` above 0, an `events.missed` frame, a new `run`, or a plugin's
  first line whose `seq` passes the relay's watermark is a loss, which the relay says in one fixed line, and the steward reads the truth
  again (§6.1 step 5, R3). A kind the relay leaves out still moves its watermark, so filtering is never taken for a loss.

### 4.5 A loop with no plugin on the point

**The set makes no queue and starts no teller for `work/event` until a plugin listening at it is running**, and `Tell` with no listener
mints the `seq` and keeps nothing. This is a change from `state/changed`'s teller, which starts at the first change whatever listens
(§1.2), and 1b's proof holds it. So the loop's report, its log and its timings are what they were, and nothing is written to the home.

## 5. The plugin `event-relay`

### 5.1 What it is

A plugin of the existing shape: `plugin.json` with `hooks.points: ["work/event"]` and a command, `node plugin.mjs`; no steps, no
servers. It is written first as an example under `examples/plugins/event-relay/`, where this repository's tests run it, as `hold-by-title`
is the rehearsals' fixture (D120 point 3). Offering it from Daoris.Plugins is a quest to that repository, never an edit (§9). **Its folder
and its package hold no file any process outside Daoris runs**: the relay is Daoris's own verb (§6.1).

Its description, which the person reads before switching it on, says: *Passes Daoris's work events (ids and codes, never words or
names) for this whole Daoris to programs on this machine that ask for them, such as `daoris-driver events relay`.* The page's gloss for
`work/event` says the same of any plugin at the point (D119's note) (F16).

**How the owner adds it** (round 4, O10). It is no offer of the install's (§1.7b), and 1f's package comes later. Until then the owner
installs it with *Install from a folder* (`PLUGIN_ADD`, a `Person` route) from this repository's `examples/plugins/event-relay/`, and
switches it on; nothing starts at the press, and the loop starts it at its next look. The installed proof, row 1h, is made so.

### 5.2 Finding the subscribers: a folder watched, backed by a slow reconciliation

- **Where a subscriber says it is there.** `<DAORIS_PLUGIN_DATA>/subscribers/`. Each subscriber writes one file, `<its pid>.json`,
  `{ "pipe", "pid" }` and at most 1 KB, once its pipe is served, put in place whole (§6.1 step 1), and removes its own file and no other
  as it ends. One file per process: no subscriber's file is another's to overwrite (F7). The file names no kind, state or name: what a
  subscriber keeps is its own (§5.3).
- **The plugin watches the folder, then reads it** (round 3, C3.4). At its handshake it sets Node's `fs.watch` on the folder first and
  reads the folder after, so a file that appears between the two is seen by one or both, and each file is connected to once by its
  name, whichever saw it. A file removed closes its connection. It takes only a file named by digits and `.json`, of at most 1 KB, whose
  `pipe` is of the relay's name shape, `daoris-event-` and 32 lowercase hex characters on Windows, a socket path inside the subscribers'
  folder elsewhere; any other file is said once and left (R4).
- **A watch that fails is set again, and the folder read again** (C3.4). An error or an overflow from the watch closes it; the plugin
  sets it again and reads the folder once more, connecting to what it has not. A watch it cannot set again ends the plugin with an exit
  Daoris did not ask for, so its back-off starts it again (§4.4), and a start reads the folder whole.
- **The watch is backed by a slow reconciliation** (round 4, K4.1, K3.2). D173's rule is that a watcher is never trusted alone: a
  slow reconciliation stands behind it, minutes apart (D173's note of 2026-10-16 on events over polling, its point 2, `D173.md:196-198`;
  the fabric events design §A1 point 2, `docs/2026-10-16-fabric-events-design.md:52`). Round 3's *nothing is reread on a clock*
  broke that rule. So the plugin reads its subscribers' folder again **every five minutes**, and **at once whenever one of its connections ends** (a slot freed), connecting to every file it holds no
  connection to: one a watch's callback never reported, a ninth that now has a slot, and one whose pipe did not answer when last
  tried. A file whose pipe does not answer is said once on stderr and tried again at the next reconciliation, never sooner, so each
  file costs at most one try in five minutes. The reconciliation reads the plugin's own folder and nothing of Daoris's; it is no poll
  of the sessions, and no subscriber reads anything on a clock. A file's own change is no longer what retries it (§5.3).
- **One connection in a subscriber's life** (round 3, replacing R4's rewritten file). A relay takes one connection and ends as it ends,
  saying the gap (§6.1 step 6), so nothing reconnects and the next relay is a new file. A file a dead relay left is removed by the next
  relay at its start (§6.1 step 1); the plugin removes no file.
- **The plugin is a client.** It opens the subscriber's pipe with Node's `net.connect`, which needs no descriptor of its own. Its first
  line is `{ "plugin": "event-relay", "run", "seq", "grammar", "build" }`: its handshake's `run`, `grammar` and `build` (§4.1), and the
  last `seq` it has heard (its handshake's before any), those five fields and no other, in the schema §6.1 step 3 checks. It writes
  nothing to the pipe but that line and frames.
- **The plugin filters nothing** (round 3, C3.3). It sends each subscriber every frame the driver told it; the relay keeps the kinds it
  was asked for (§6.1 step 5), so its watermark is the source's `seq` and a gap in it is a loss. A filter in the driver would put a
  subscriber's choice into Daoris's records, which this keeps out; a filter in the plugin made a filtered frame look lost.

### 5.3 Ordering, bounds, and a subscriber gone or slow

- **In order, per subscriber.** One writer per connection, in `seq` order, newline-delimited JSON, each frame the driver's unchanged.
- **Bounded** (C3.4). At most 8 subscribers are connected at once; a ninth file is said once and connected at the reconciliation a
  freed slot runs (§5.2). At most 64
  frames wait for one subscriber, the oldest dropped first, and the `seq` it hears next shows the gap. A write not completed within 5 s
  marks the subscriber slow, and its connection is closed; the relay ends and says the gap (§6.1 step 6).
- **The driver is never held by a subscriber.** The plugin answers `hook/work/event` as soon as it has queued the frame for each
  subscriber; nothing waits on a write.
- **The plugin gone.** A relay connected to it ends and says the gap (§6.1 step 6). The lease's loop starts the plugin again at a later
  look (§4.4), and it reads the folder again, connecting to the relays waiting there, each on its own pipe. A plugin that does not come
  back (switched off, unsound, unable to start) leaves the relays unconnected, and each says so within its bound (§6.1 step 6).

### 5.4 A trial is never taken for the live plugin

- **A Try** starts the installed plugin in its own data folder (§1.6), told the kit's sample `work/event` frame, whose `run` is
  `"sample"`, and handshaken with `run: "sample"` (§4.1). The plugin, told a sample run, connects to no subscriber, and says so. A relay
  refuses any connection whose first line names `run: "sample"` and drops any frame whose `run` is `"sample"` (§6.1).
- **A second live process** cannot come from the loops: only the lease's loop starts one (§4.1). Were one to start anyway, it would
  connect as a second client, and a relay that meets a second client passing its checks while one is connected ends and says so
  (§6.1 step 2). Neither process removes a subscriber's file, so neither silences the other unsaid.

### 5.5 Its words and its log

Its stderr says each subscriber's connection, drop and departure, under `plugin:event-relay` on the console, naming the subscriber by
its pid, never its pipe. The machine log keeps the set's `plugin.called` for each `work/event` as it does for `state/changed`
(`Hooks.cs:1116`), with no subscriber in it.

## 6. The delivery adapter

### 6.1 The relay: `daoris-driver events relay --once`

**Its subscriber** (round 4, O1). The first build's subscriber is the owner's own Claude Code session outside Daoris, today's steward.
It starts the relay as a background command, by its copy's path and with the home its `--where` printed (step 0):
`<copy> events relay --once --home <home> [--since <run>:<seq>] [--kinds …] [--session-pid <pid>]`. It prints one batch and ends, and
the background command's end, with its output, wakes the session (§2.4). It is the first build's one adapter (round 3, C6.1). That its
end starts a turn in an idle session, and which start lives on Windows, is measured first (§7, M5). **The owner's allow rule**
(round 4's named changes, P3): each re-arm starts the copy by a path that changes with each build, so the owner's own Claude Code
settings (their user settings, never a tracked file) allow two commands by prefix: the install's
`app/daoris-driver/daoris-driver.exe events relay --where`, a path no build changes, and the `bin` entry's `events-relay/` folder that
`--where` prints under, the build's folder varying after that prefix. M5 records whether both run unprompted under the session's
permission mode with that rule, across a change of build; where a prefix cannot span builds, the session asks the owner once per
build, which only an update brings, and the measurements record says so. A role holder of D209 is no
subscriber of the first build: `events relay` stays out of `RoleTerminal.Runs`, so in a role's terminal it is refused `role-terminal`
at the host's entry, as every verb the list does not name is (§1.7b), and the role holder is a later round's (§6.2).

0. **It holds no lease and runs only from its copy** (R2). `events relay` is classed in `VerbLeases` with the verbs that hold nothing,
   beside `chat` and `run-heavy`, with its reason: its one write is its own file in a plugin's data folder, none of the home's records,
   and a lease held while it waits would keep the desktop's loop waiting (§1.7a). So a relay started while Daoris is closed leaves the
   lease to the desktop, which takes it as if no relay ran. It runs from a copy of its build outside `app/`, so the launcher's swap is
   never held by one: a relay started from anywhere but a folder named by its build under the `bin` entry's `events-relay/` refuses,
   exit 1, naming `--where`.
   - **A build is its whole bundle** (round 4, K2.2, O5). `RelayBuild.Of(folder)` is the SHA-256, in lowercase hex, of one line
     `<path>\t<size>\t<sha256>\n` for each file under the folder, its path relative with `/`, the lines in ordinal order: every file of
     the `daoris-driver` bundle, the driver library and every other dependency among them, as a staged build's manifest lists every
     file (`tools/desktop-publish.mjs`'s `stagedManifest`). A changed companion library is another build. The relay's own build is its
     folder's name; the set's is told in the handshake (§4.1) and in the plugin's first line (§5.2).
   - **`--where` lays the copy out** (round 3, C2.3, C4.2, C5.3). `daoris-driver events relay --where`, run from `app/`, finds its
     install and home as every verb does (§1.7b), and is classed as an ordinary verb: it holds the home's lease for its short run, so it
     waits on the launcher's swap or a home's move, and beside a running instance holds nothing (§1.7a). The host routes it before its
     own adoption of the home and its lease, and the verb's console takes both by the host's own calls, saying every failure in a fixed
     sentence (step 1; C4-2.2). Its place is
     `DaorisHome.Entry(home, HomeEntry.Bin, "events-relay", <build>)`, the home's `bin` entry and no second layout, `<build>` being
     `RelayBuild.Of` of the running build's folder, `app/daoris-driver/`, computed at every `--where`. Where that folder is absent it
     copies the running build's folder into a staging folder of its own beside it, checks each file's hash against the source, and moves
     the staging folder to `<build>` with no overwrite, so a copy appears whole or not at all; of two `--where` at once, the one whose
     move fails removes its own staging folder and uses the other's. The `in-use` file is made in the staging folder before the move,
     so every whole copy holds it: **a folder of the build's name without its `in-use` file is not whole** (round 4's named changes,
     P8), and `--where` removes it, by the rule below, and lays the copy out again. A folder of that name with its `in-use` file was laid
     out whole from the same bytes, its name being their hash, so `--where` uses it; one rewritten in place is outside what the relay
     promises (below). **Its lines are fixed sentences too** (P7): every line `--where` writes, on either stream, is one of `RelaySaid`
     and names no entry it met, but for its one line giving the copy's executable and `--home <home>`, which the session starts; that is
     the one line of this verb that names a path, and it reads nothing from a pipe.
   - **Each relay holds its build in use.** A relay opens its build's `in-use` file shared for reading alone, never for deletion, and
     holds it for its life, and refuses,
     exit 1, naming `--where`, where it cannot. **A build is removed by a rename first** (P8): a `--where` removes a build other than its
     own only once it has opened that build's `in-use` file alone (exclusive, so no relay holds it) and closed it, and then renamed the
     build's folder to a removal name of its own beside it; a relay that opened `in-use` between the two makes the rename fail, since a
     folder with a file open in it does not rename, and the build is kept. Only then are the renamed folder's files deleted, and a
     removal that stops partway (a file a scanner holds) leaves a removal name, never the build's, which the next `--where` deletes. So no relay runs from a build while it is removed, a relay starting as
     one is removed refuses rather than run from half of it, and no half-removed folder is ever taken for a build. What keeps a build is
     its `in-use` file, never an executable held open (C2.3).
   - **No junction is followed.** Before it writes, starts from or removes anything under `events-relay/`, `--where` checks that each
     folder from the home's `bin` entry down, and each entry it would remove, is a plain folder or file and not a reparse point (a
     junction or a symbolic link); one that is is said once, in a fixed sentence that names no entry (P7), and left, never followed and
     never removed through. A relay checks its own copy's folders the same way at its start.
   - **A stale copy ends itself.** CONNECTOR1's warning is a copy outliving its build: so a relay ends when a plugin's first line names a
     `build` other than its own (round 4, O5, K2.2), saying one fixed line, `Daoris: Daoris was updated; start the relay again
     [<run> #<seq>]`, from the first line's `run` and `seq` once step 3 has checked them, and its session starts the next by `--where`,
     from the new build. The build being the whole bundle, a change to anything the copy reads (the lease's file, the catalogue, the
     agents' job's name) ends it so, whether the grammar changed or not. A stale copy that refuses the new plugin before its first line is
     said by its bound (step 6). No copy opens a store: CONNECTOR1's harm, a store rebuilt at an older schema, has no place in it.
1. **What it takes, holds and touches** (R7, R12; round 3, C5.3; round 4, O3, O11).
   - **It is routed at the host's entry, before the home's adoption, the generic lease and the machine log** (round 3, C5.3; Codex's
     read of round 4, C4-2.2, C4-5.3), both forms, `--where` and the relay. The host's generic lease handling prints what no fixed
     sentence holds, before any route of its own: a refusal at the end of its wait is `HomeLease.WaitRefusal`, which names the home and
     `LeaseHolder.Named`, whose `What` is read unchecked from the lease file (`Host/Program.cs:404-409`; `HomeLease.cs:36-46`,
     `:210-212`, `:417`), and a `HomeLeaseException`'s message names the lease's path and an inner exception's message
     (`Host/Program.cs:411-415`; `HomeLease.cs:176-178`). The early routes come after both (`Host/Program.cs:420-445`), so no catch of
     their own could hold them. So `events relay` is routed after the role-terminal gate (`:372`) and before the home's adoption
     (`:390`). Only two sentences can come before it, both fixed and holding no value read from a file, a pipe or the environment:
     `InstanceDoor.TakeWait`'s problems with `--wait` (`InstanceDoor.cs:405`, `:418`) and the role terminal's refusal
     (`RoleTerminal.Refusal`, `SupervisorProof.cs:109`, `:115-120`), which `RelaySaid`'s census lists as the host's. Its console then
     does what the host does after that point, inside one catch whose every line is a `RelaySaid` sentence. `--where` adopts the
     install's home (`DaorisHome.Adopt`) and takes the home's lease by the generic path's own call, `HomeLease.ForVerb(home,
     VerbLeases.Name(args), …)`, so **the lease's behaviour is the generic path's**: the same thirty-second wait on another act, the
     home held for the run and let go as it ends, and beside an instance nothing held. Only what is said changes: a wait that ends
     refused says `Daoris: the home is held by another act; nothing was laid out; run --where again`, exit 1, never the refusal's text;
     a `HomeLeaseException`, an entry of the home at both its places (`HomeEntryTwiceException`, which names both,
     `DaorisHome.cs:105-109`), and any other exception of the home's resolution or of the lease's folders (`HomeLease.cs:135`, `:146`)
     say `Daoris: the relay stopped (<code>)`, the code from its closed list, exit 2. The relay takes no lease (step 0) and reads the
     holder by `HomeLease.HeldBy`, whose `What` it never prints. Neither form writes a machine-log line, and the log's open, which
     prunes the home's logs (§1.7b), never runs for either.
   - **Every line it writes is a fixed sentence** (round 4, O3, K2.1). Stdout and stderr alike, since a background command's output
     carries both: each line is one of `RelaySaid`, a closed list of fixed English sentences, and the only values in one are a `run` and
     a `seq` that step 3 or 4 checked, an id in its minted shape, a closed code or a count. Never a value read from the pipe before its
     check, a file's content, a path or an exception's message: the relay's own catch says `Daoris: the relay stopped (<code>)`, its
     code from a closed list, exit 2. A census over `RelaySaid` holds each sentence's values to those kinds. Whether stderr reaches the
     session is measured (M5).
   - **Its diagnostics are tallied** (C4-2.3). Wherever a step below says a refusal, a dropped frame, a repeat or the untold client on
     stderr, the relay counts it by its kind and says nothing then; as it ends it prints one line for each kind it met, with its count,
     before its batch, so stderr holds at most one line for each of `RelaySaid`'s diagnostic kinds whatever reaches the pipe. A
     background command's output reaches the session only at its end (§2.4), so saying them then loses nothing.
   - **Its home is the one it is told**: `--home`, else `DAORIS_HOME`; with neither it refuses, exit 2, naming `--where`, since a copy
     outside `app/` finds no install (§1.7b). It needs no install beyond the home: step 3 asks the home's lease and its holder's job
     (round 4, O8).
   - **No key.** The host has already taken the holder key into memory at entry, as for every verb (§1.7a), so the relay clears what was
     taken (`HolderKey.Drop`, beside `Take`; the entry's role-terminal marker stays) and holds no key for its life (R7). It removes
     `DAORIS_SERVICE_KEY` and `DAORIS_REMOTE_KEY` unread, and the two messaging variables too, which the first build reads nowhere (§6.3).
     It opens no door of Daoris's: not the instance pipe, not the service, not the remote. It starts no process. So it makes no press,
     and the standing confirmation D201 point 4 applies to a terminal has nothing of its to cover.
   - **What it touches in the home, said whole** (round 4, O11): it reads the home's lease (steps 3 and 6), reads the plugin
     catalogue's place for `event-relay`'s data folder, `plugins/.data/event-relay` under the home's `plugins` entry (§1.7b), holds its
     copy's `in-use` file (step 0), and writes and removes its own subscriber file (§5.2). `subscribers/` and the folders above it to that
     data folder must be plain folders, checked as step 0 checks the copy. The file is written under another name in the folder and
     moved in with no overwrite, and removed by its own name alone. At its start the relay removes a file in `subscribers/` whose name
     and `pid` agree on a process that is not alive, which a relay that died left; it touches no other.
2. **It serves one pipe and takes one plugin.** On Windows `daoris-event-` and 32 random hex characters, made with the instance pipe's
   rule (the user's SID owner and alone, the network denied) and `FirstPipeInstance`, so a plugin at either elevation reaches it (§1.7;
   F6). That rule is the modules' `InstancePipe.Security`, out of `daoris-driver`'s reach, so 1c moves it into the driver beside
   `InstanceClient`, and the modules' instance pipe calls it there (R7). Elsewhere, a Unix socket in a folder only the user reads.
   **It keeps one more instance listening for as long as it lives** (round 4's named changes, P4): the pipe is made for two instances
   (the count `InstancePipe`'s own create takes, `Modules/Platform/InstancePipe.cs:759-762`), and as one is taken by a client the other
   listens, so a second client reaches the relay instead of meeting a busy pipe. *Connected* means a client whose first line has passed
   step 3. It takes one connection in its life (§5.2). A connection whose first line does not come within 5 s, or that fails step 3,
   is closed as a refusal, said by a fixed sentence, and the relay goes on waiting; its bound says a relay that meets only refusals (step 6).
   **A second client that passes step 3 while one is connected ends the relay** (round 4, O4), with
   `Daoris: a second client claimed the events; read the sessions again [<run> #<seq>]`: two claim to be the plugin and at most one is,
   so a silent client holding the connection is said as soon as the real plugin's reconciliation reaches the relay (§5.2).
3. **It refuses the strangers it can see, checks the first line, and cannot refuse an agent** (R1; round 4, O2, O8, K2.1; round 4's
   named changes, P1). On Windows it asks the system which process is the client (`GetNamedPipeClientProcessId`), reads the home's
   lease, and asks whether the client is in the agents' job of the instance the lease names (`LeaseHolder.IsInstance`), alive and
   started when the lease says: `ProcessJob.NamedHolds(AgentJobs.NameOf(<its pid>), <the client>)`, the job opened by its name for
   query alone (§1.7b). Every launcher start of that instance joins that job, so the check passes the plugin whatever its process tree,
   `node` started through a version manager's shim included. It reads no parent and no image, so `HolderWalk`'s `SystemProcesses` stays
   in the modules (O8). A job that opens and answers that it does not hold the client is a refusal. **Where the job or the client will
   not open, the client is untold, and taken** (P1). Across elevations a relay that is not elevated cannot open an elevated holder's job
   (§1.7), and `NamedHolds` answers null; the relay then says one fixed line from `RelaySaid` on its stderr, `Daoris: the client could not
   be checked across elevations; it is taken unchecked`, and takes the client, its first line still checked. Nothing that keeps an
   agent's words out rests on this step (below), so an unchecked client costs only the filter of strangers, and in that layout the
   pipe's own rule (this user's processes alone) still stands. The reverse layout, a relay elevated and Daoris not, opens the job and
   checks. M10 confirms which answer each layout gives, and the choice does not wait on it (§7). **The first line has a closed schema**,
   checked before any of it is used: a JSON object of at most 512 bytes with exactly `plugin` (`"event-relay"`), `run`
   (`^[0-9a-f]{12}$`), `seq` (an integer from 0 to 2^53), `grammar` (an integer from 1) and `build` (`^[0-9a-f]{64}$`), and no other
   field. A `run` of `"sample"` fails it (§5.4). A first line that fails is refused, said by a fixed sentence that holds nothing of it,
   and none of it is ever printed. What this refuses: another user's process (the pipe's rule already does), a process the holder's
   agents' job answers it does not hold (one no launcher of the holder started, an uncontained plugin), a Daoris that does not hold
   the home (a `--share` loop's plugin, in that loop's own job; another home's), a trial, and a first line outside its schema. **What it does not
   refuse: any agent Daoris runs.** An agent's harness is started into the same job (§1.5), and the plugin's code is in a folder any
   agent can rewrite (§3), so an agent can connect as the plugin or speak through it. And the holder is read from `run/home.lease`, a
   file any process of the user can rewrite (§1.7). So the job check is a filter of strangers and nothing more, and **nothing that keeps
   an agent's words out rests on it or on any other file the relay did not write**: the first line's schema and steps 4 and 5 do. A
   plugin started uncontained (§1.5) is refused with `Daoris: refused a client that is not contained` on the relay's stderr, and the
   plugin says the refusal on its own (§5.5), which its Activity shows.
4. **It validates every frame against `WorkEventGrammar`** (§4.3) before it builds a word: a closed `kind`, its closed fields, every
   generated id in its minted shape, a line at most 4 KB, a longer one read no further. A frame that fails is dropped and said on
   stderr, and three in a row close the connection, which ends the relay (step 6). The driver never sends a value that fails (§4.3), so
   a failing frame was written by something else.
5. **Its watermark, its batch and its words** (R1, R3; round 3, C3.2, C3.3, C3.4; round 4, O7, K3.1).
   - **The watermark.** `--since <run>:<seq>` is the bracket of the last line of the last batch the steward read (below), checked as
     the first line's `run` and `seq` are; one that fails is refused, exit 2, before anything is served. The relay keeps the last `run`
     and `seq` it heard, starting from `--since`.
   - **The plugin's first line settles a loss at once.** Where it names another `run` than the watermark's or another `seq`, or where
     no `--since` was given, the batch is the gap line, printed at once and ending the relay:
     `Daoris: events may have been missed; read the sessions again [<run> #<seq>]`, naming the first line's `run` and `seq`. So a
     restart, a stop, and events no relay heard are each said by the next relay's first connection, with no later event needed, when
     the one missed is exactly the next `seq`, and whether any session was running or not (C3.2). A park that the first look's
     baseline takes in after that line is said by the baseline's `events.missed` frame, which follows it whatever it held (§4.2;
     C4-3.1).
   - **Each frame after it.** A `seq` past the next, a `missed` above 0, or an `events.missed` frame, whatever `--kinds` says, puts the
     gap line in the batch, **once**: a batch holds at most one gap line however many gaps it meets, at the place of the first (C4-2.3).
     A `seq` at or below the watermark in the same `run` is a repeat, dropped and counted in its tally (step 1), never a gap. A kind
     outside `--kinds` (by default the four kinds of §4.2) moves the watermark and adds nothing, so filtering is never taken for loss.
   - **The batch.** The first kept event or gap line opens it, gathered for 30 s, at most 50 event lines; past that the events are
     counted, and the batch says `Daoris: <n> more events; read the sessions again [<run> #<seq>]`. Each event is one line of fixed
     English words, at most 200 characters, built from its kind, its codes and its generated ids, such as
     `Daoris: session 4b62a9fe parked, waiting on the person (quest 48f8ed9121ec) [6c0f1a9e2b47 #12]`. **Every batch ends with one line,
     `Daoris: read up to [<run> #<seq>]`, the relay's watermark as it ends**, which a frame it left out moves too; that line's bracket is
     what `--since` takes, so a narrower `--kinds` never makes a false gap across relays (round 4, O7). So stdout holds at most 53 lines,
     fifty events, one gap line, the `more events` line and the closing line, and stderr one tally line for each diagnostic kind,
     whatever floods the pipe (C4-2.3). No name is in a frame (§4.3), so none is in the words. The words are a machine's to an agent, not the
     window's chrome, so they are not translated. The `run` and `seq` keep two batches from ever being identical (F10).
6. **It ends, and every end is said** (C3.2; round 4, O4, K3.2). It prints its batch on stdout, removes its subscriber file and exits 0:
   when the batch is gathered; when its connection ends for any reason (the plugin or Daoris gone, three bad frames, the plugin's slow
   mark), printing what it gathered and the gap line; when a plugin of another `build` connects, with its update line (step 0); and
   when a second client claims the events (step 2). With `--session-pid` it also ends, printing nothing, when that process exits, which
   it waits on and never polls.
   - **Its bound while no plugin reaches it.** Every 15 minutes with no connection taken, read off a clock it is handed
     (`.claude/knowledge/held-clocks.md`), it reads the home's lease once. Where an instance holds the home, Daoris runs, so it ends with
     `Daoris: no events reached this relay (<reason>); read the sessions again [<run> #<seq>]`, `<reason>` a closed code saying why
     (round 4's named changes, P5): `no-client` where no client came, `refused` where only clients step 3 refused came, and
     `not-contained` where one was refused as not contained; and its watermark (`000000000000 #0` where no
     `--since` gave one, a `run` the set never mints, so the next relay's first line says the gap); a lease it cannot read, a newer
     build's among them, counts as held, so a doubt ends in a wake, never in a wait. Where none holds it, Daoris is closed and
     no loop runs to make an event, so it goes on waiting, and the plugin's start reaches it with a new `run`, which says the gap then.
     So a plugin switched off, unsound or unable to start, a stale copy that refuses it before its first line, and a relay that meets only
     refusals are each said within 15 minutes while Daoris runs, and a closed Daoris costs one file read in 15 minutes and no wake. A
     plugin that does reach it does so within the plugin's five-minute reconciliation (§5.2), well inside the bound.
   - **A relay killed before it prints** ends the background command too, which wakes the session where M5 finds that a kill's end
     starts a turn, and the next relay's first line says the gap.

   So, once Daoris runs, no loss is left unsaid but one, which M5 measures: a frame, a first line or the baseline's `events.missed`
   says it at once, an end says it as it ends, and the bound says a relay no plugin reached (round 3's *no loss waits on a later
   event*, which a relay no plugin ever reached broke, O4). **The one is a relay killed before it prints** (C4-3.3). It is said at the
   kill only where M5 finds that a kill's end starts a turn; where M5 finds it does not, it is said only when the steward next starts a
   relay on its own (§7's rule), and the measurements record names that limit. The promise is no larger than M5's answer.

**What Windows does not draw, and what the relay promises** (round 3, C2.2; round 4, O4). Processes of one user at one integrity level
are not separated: such a process may read another's memory, rewrite another's files, or start a child under a parent of its choosing,
and every agent Daoris runs is such a process. Step 3 does not stand against them and does not claim to. Nor does anything here stand
against a process that rewrites the relay's copy in the home, or the session's own files: the copy sits where any process of the user
may write. So the relay promises less than round 2 said: **while its own files are intact, whatever reaches its pipe, it writes only
fixed sentences over ids in their minted shapes**, which no word fits in, and never a name. The shapes bound what the relay can be made
to say; they authenticate neither an event nor the record it names. What a process can do through the pipe is make the relay say a
false wake; hold its one connection, until the real plugin's reconciliation reaches the relay and it ends saying a second client
claimed the events; or keep any plugin from reaching it, until its bound says no events reached it. Each of these is said, and a wake
grants nothing: the session decides every act on its own reads and its own permissions (§8). **What is not promised: silence a process
of the user makes on purpose.** It can rewrite the plugin's code to pass nothing, kill the relay, or hold the connection while the
plugin is down, and nothing on one user's machine stands against that, as nothing stands against it rewriting the session's files.
Every loss that comes of a failure (a plugin down, a stale copy, a frame refused, a queue full, a lease read busy) is said, and a relay
killed is said within M5's answer (step 6; C4-3.3).

### 6.2 Deferred: the role holder, the inbox and the channel (rounds 3 and 4)

None is built from this design's first build, and each is designed again in a round of its own once the first build is in use. Each
would keep §4's point, §5's plugin and §6.1's steps 0 to 4, so none changes what the first build ships.

- **The role holder as a subscriber** (round 4, O1). A steward appointed under D209 runs in a role's terminal, whose harness allows
  `Read`, `Grep`, `Glob` and its roles' verbs alone, by fixed names (`HolderTerminal.cs:41-48`), and whose entry refuses every alone path
  (`D209.md:77-78`). Its round names the D209 amendment it needs: a fixed element in the holder's stated-whole start line for a fixed
  command name of the relay, held by its walk (`HolderWalk.cs`), and the copy laid out by the instance, which holds the home, rather than
  by `--where`, an alone path a role's terminal refuses.

- **The inbox adapter** (round 2's §6.1 steps 5 to 7): one relay living for the session's life, posting each batch into the session's
  Claude Code inbox with the session's token, held in memory, after the auth line (§2.1). It needs M2-M4 and M8, the part of M5 that
  says a process started by the session lives as long as it and knows its pid, the token's custody, and a rule for a batch the inbox
  refuses, which round 2 dropped (C3.2).
- **The knowledge server's channel** (round 2's §6.2): an MCP channel behind an opt-in variable, a research preview that needs the
  development flag in an interactive session, its Windows support unstated (§2.2). It needs M6 and M7, the grammar, the lease read and
  the pipe rule in the service as twins held by a census or a library both share (the server builds against `Daoris.Service.Core`
  alone, §1.7), and, for C5.5, two things round 2 lacked. A door that reads the install whose events woke the session: a workspace's
  knowledge server is handed a home of its own (`tools/knowledge-server.mjs:134-143`), so its tools read that home and not the install.
  And a hand-over across the launcher's build swap: the launcher passes on nothing from a server that no longer serves the session
  (`:322-327`), so a notification sent across the swap is lost.

### 6.3 The inbox's variables never pass through Daoris

`GitWiring.Removed` gains `CLAUDE_CODE_MESSAGING_SOCKET` and `CLAUDE_CODE_MESSAGING_TOKEN`, so every child Daoris starts through Tools
or a tree's git (`Tools.cs:679`, `:693`; `Driver/Trees/WorkingTree.cs:1010`) is handed neither: a plugin, an agent, a piece, a git.
And each Daoris executable removes both from its own environment at entry, beside `HolderKey.Strip` and `HolderKey.Take`
(`Daoris.Desktop.App/Program.cs:34`, `Host/Program.cs:357`), so no start that bypasses `Hand` copies them. In the first build nothing
reads them: `daoris-driver` removes them unread, and only the deferred inbox adapter would take them into memory there (§6.2). The
wiring census names both as removed. So a Daoris started from the session's shell passes the token to nothing it starts (F3). This
stays in the first build though no relay posts: it keeps the steward's inbox from every agent Daoris starts, a leak today (§1.5). It is
a row of its own, 1g, waiting on no measurement, since it rests on none of M5 (round 4, O9).

## 7. Measured first: EVENTHOOK1a

On the installed Claude Code on this machine, before any row depends on it. The probes are scripts under
`src/Daoris.Cli/test/fixtures/event-hook/` (a stand-in poster, a stand-in pipe server and client, a one-file channel server), run by
hand from a real interactive session. M9's and M10's probes are .NET or PowerShell, never Node, since Node can call neither
`InstancePipe.Security`, `OpenJobObject` nor `IsProcessInJob` (round 4's named changes, P10). No test runs them, and no test starts a
real console. **1a measures M1, M5, M8, M9 and M10**, what
the first build rests on (round 3, C6.1); M2-M4, M6 and M7 are the deferred adapters' (§6.2), kept here as their list and measured in
their rounds.

| # | Measures | Evidence kept |
|---|---|---|
| M1 | `claude --version`; `/status`'s `Peer address` form on native Windows | the version and the address's form, never the address |
| M2 | a child started from the session's Bash posts the auth line and one message line: the line shapes accepted; delivered, held or refused under `default`, `acceptEdits` and bypass modes | each mode's outcome; the accepted shape |
| M3 | the same post to an idle session: does a turn start | yes or no, with the turn's first line |
| M4 | two identical posts; ten in two seconds; one every 30 s for ten minutes: what is dropped, refused or rate-limited | the counts |
| M5 | in the session that will subscribe, the owner's own outside Daoris: a background command started from Bash by a path a first command printed (as `--where` does, §6.1 step 0), and, for the inbox's round, one from a `SessionStart` hook: does each live while it waits with nothing printed, for an hour at least, and past the session's end, and is the session's process id known to it; its end with lines on its stdout: does it start a turn in an idle session, with its output (§2.4); **round 4** (O3, O12, K5.1): do lines on its stderr reach the session with its end; does an end with nothing printed, and an end by a kill, start a turn; does a wait live across the session's compaction and a `/clear`, and does its end then still start a turn; **round 4's named changes** (P3): do `--where` and the re-arm run unprompted under the session's permission mode with §6.1's allow rule, and still after a change of build | each, with how long it lived and how it ended; the wake's outcome, stderr's, a silent end's and a kill's; the wait across compaction and `/clear`; prompted or not, before and after a build's change |
| M6 | a one-file channel server registered with `--dangerously-load-development-channels server:<name>`: registers and delivers on native Windows, to an idle session | the startup notice's words; delivered or not |
| M7 | the knowledge server's .NET MCP SDK: can it declare `experimental['claude/channel']` and send a raw notification; does Claude Code register it after the revision fallback, through `tools/knowledge-server.mjs` and across a build swap | a small check's result |
| M8 | a process started from the session's Bash, and its children: do they hold the two messaging variables; a stand-in Daoris child started there with the `Removed` list applied: does it hold them | present or absent, each |
| M9 | a pipe made with `InstancePipe.Security` by a process that is not elevated, opened by Node's `net.connect` from an elevated one, and the reverse; and a Node-made pipe opened across elevations, to confirm the failure | each opened or refused |
| M10 | from a relay that is not elevated, and the reverse: `GetNamedPipeClientProcessId` on a client across elevations, and (round 4, O8) the holder's agents' job opened by its name for query and `IsProcessInJob` asked of that client, across elevations. It confirms step 3's choice, not decides it (P1): the expected answer, from §1.7, is that a relay that is not elevated cannot open an elevated holder's job, so `NamedHolds` answers null and the client is taken untold with its fixed line; and that the reverse opens and checks | read or refused, each, against the expected answer |

**The decision rule: each adapter's own prerequisites** (round 3, C5.1). Nothing is built on a measurement it does not rest on.

- **The first build** (1b, 1c) rests on M5: a background command lives while it waits, and its end starts a turn in an idle session
  with its output. With that, and M9 and M10 read, 1b and 1c go on. If M5 answers *no*, 1b and 1c wait, the row says so in the backlog
  with the measurements, and this design takes another round that makes the inbox or the channel the first adapter, measured then.
  If M9 refuses, §6.1 step 2 is designed again before 1c. M10 confirms step 3's choice whichever way it answers, since a job that will
  not open is already taken untold (§6.1 step 3, P1); only an answer that is neither the expected one nor its reverse sends step 3 to
  another round. **If the re-arm prompts** (M5, P3), the first build still goes on, and the owner approves the copy's path once per
  build, as §6.1 says.
- **The inbox adapter** rests on M2-M4, M8 and M5's lifetime; **the channel** on M6 and M7. Neither rests on the background wake, and
  each would take 1b and the plugin of 1c as they are, since neither of those rests on M5 either. So a *yes* from either has a path to
  a build that a *no* from M5 does not close: its own round (§6.2).
- **1g**, the messaging variables kept out of everything Daoris starts (§6.3), rests on no measurement and waits on none (round 4, O9);
  M8 confirms it, and does not gate it.
- **If stderr does not reach the session** (M5), nothing changes: every line the relay writes is already a fixed sentence (§6.1 step 1).
  **If a silent end or a kill does not start a turn**, step 6's kill case is said only by the next relay the steward starts on its own,
  and the measurements record says so.

The evidence is a dated record, `docs/2026-10-xx-event-hook-measurements.md`, routed in `docs/README.md`: versions, the steps, each
outcome, and no machine path, token, address or process id. A probe that cannot run is recorded as such, and the rows that depend on it
wait. **Each outcome is recorded as one of two** (C4-5.1): a failure that blocks progress by the rule above (M5's lifetime or wake
answering *no*, M9 refusing, M10 answering neither the expected answer nor its reverse), with the rows it holds; or a limit accepted and
named, with what follows from it (stderr not reaching the session, a silent end or a kill starting no turn, the re-arm prompting once a
build, a client taken untold across elevations). So no limit is later read as a pass, and no pass as a limit.

## 8. What a subscriber may do with an event

- **Read it as a wake.** It says what moved. The truth is read again through the subscriber's own doors: its connector's tools
  (`quest_list`, the session reads), `daoris-driver sessions --json` and the CLI's reads.
- **Act through its own doors, as an agent.** What it may do after reading is what it could do before the event. An event grants
  nothing, and **a wake is never a confirmation, an approval, or evidence that what it names is so** (round 3, C2.4): the plugin's wire
  answers no request from a plugin (`Hooks.cs:532-534`) and the relay opens no door, so no act follows from an event but one the
  receiving session decides on its own reads, under its own permissions.
- **Never a press of the person's.** D156 stands: no process in this chain holds the person's key. D201 stands: the plugin is contained,
  and the relay, which runs where a terminal runs, holds no Daoris key and no lease, opens no Daoris door and makes no press (§6.1
  steps 0 and 1). What the
  outside session may press is what it could press before (D201 point 4, and STEWARDROLE1's roles where they land).
- **No instruction rides an event through an intact relay, and this rests on the words alone** (R1; round 3, C2.2; round 4, O2, O3).
  The relay's check of its client refuses strangers and not agents (§6.1 step 3), so the promise does not rest on who wrote to the
  pipe. It rests on the first line's schema and steps 4 and 5: every generated id, the first line's `run` and the `--since` watermark
  among them, is checked in its minted shape, which no word fits in; no frame carries a name; and every line the relay writes, on stdout
  or stderr, is a fixed sentence around those ids. So whatever an agent writes to the pipe, nothing authored reaches the receiving
  session through a relay whose files are intact. What it can do is said in §6.1 (*What Windows does not draw*), and a wake grants
  nothing. A process that rewrites the relay's copy or the session's own files is outside what any of this holds against.

### 8.1 The steward's procedure for each line (round 4's named changes, P5)

What the subscriber does next with each line the relay ends on, so that the steward stops polling unattended and no failure turns into
a loop. A knowledge document the steward reads will carry it once 1c builds the lines; until then this table is the procedure. Every
batch closes with its `Daoris: read up to [<run> #<seq>]` line (§6.1 step 5), and that bracket is the `--since` of the next relay.

| The relay's batch holds | The steward |
|---|---|
| event lines, a gap line, a `<n> more events` line, tally lines (C4-2.3), and nothing below | reads the truth through its own doors (§8 above), acts only as it could before, and starts the next relay with `--since` from the closing line |
| `Daoris: Daoris was updated; start the relay again [<run> #<seq>]` | runs `--where`, only after this line, since a `--where` from `app/` while a swap is pending stops that swap (`Launcher/Program.cs:72`, `:86`); starts the new copy with `--since` from the closing line; and where the new copy ends on this line again at once, does not re-arm and tells the owner |
| `Daoris: no events reached this relay (<reason>); …` | reads the truth and re-arms once; after two such batches in a row, reads `daoris-driver plugins show event-relay`, tells the owner what it says with the reason, and re-arms no more until the owner has acted, so a plugin off, unsound or refused never becomes a fifteen-minute loop |
| `Daoris: a second client claimed the events; …` | reads the truth and re-arms; after two in a row, tells the owner |
| `Daoris: the home is held by another act; nothing was laid out; run --where again` (`--where`'s, exit 1; C4-2.2) | runs `--where` once more; on a second such line, tells the owner and does not re-arm |
| `Daoris: the relay stopped (<code>)` (exit 2), or a refusal of the relay's own arguments | tells the owner, and does not re-arm |
| nothing, the relay killed or `--session-pid`'s process gone | reads the truth, and re-arms with the last bracket it read; the next relay's first line says any gap |

## 9. Rows, in build order, with their proofs

| Row | Lanes | What | Proof |
|---|---|---|---|
| **EVENTHOOK1a** | the steward, in the owner's own interactive session outside Daoris; first | M1, M5 with round 4's cases (stderr, a silent end, a kill, a wait across compaction and `/clear`) and the re-arm unprompted under §6.1's allow rule (P3), M8, M9, and M10 with the agents' job across elevations against its expected answer (P1), M9's and M10's probes in .NET or PowerShell (P10) (§7), and the decision rule | the measurements record, every probe's outcome or why it did not run, each outcome a failure that blocks, with the rows it holds, or a limit accepted and named (C4-5.1) |
| **EVENTHOOK1b** | driver (the host's `Program.cs` among it), modules, web; after 1a, by its decision rule (§7) | `work/event` in every place §1.1 lists; `HookFrames.Event` and `WorkEventGrammar` with §4.3's table, no name in any frame, the park or ending of a conversation the person holds (`!ActiveSessions.HoldsSlot`, judged on the `SessionView` the watch last saw) not told, an event whose required field fails told as `events.missed` in its place (§4.2, §4.3); `Tell` minting every `seq` whatever the lease says and under one lock, telling only where the reconcile's read of the lease found it held, a plugin's queue attached under the lock, a queue and teller per plugin made only once one listens (§4.1, §4.4); `initialize`'s `events` with `grammar`, the last `seq` minted, and `build`, `RelayBuild.Of` of the install's `app/daoris-driver/` computed once, in the driver library; a plugin on the point wanted only by the lease's loop; late answers counted and the plugin replaced, the work wire's exit bookkeeping kept for a plugin at `work/event` (`Hooks.cs:692-711`, `:786-791`), counting the late stop and the plugin's own exits alone; the reconcile's lease read telling busy apart, a busy answer keeping the last look's (P6); both loops tell the attention watch's `Parked`, `QuestParked` and `Ended`, `Observe` carrying a park's quest and an ending's quest and adapter; after every `Observe` that was a baseline, `AttentionWatch` saying so, each loop tells one `events.missed`, an empty baseline included (C4-3.1, C4-5.2); `AutoLander.Tried` tells `work.landed`; `event.unsent` and `plugin.stopped`'s `late` in `PluginEvents` and in the machine log design's catalogue, with the catalogue's test (C4.2); the kit's sample with `run: "sample"`; the page's gloss in `src/Daoris.Web/src/plugins/PluginKit.tsx` with its words in `src/Daoris.Web/src/locales/en/plugin.json` and `zh/plugin.json` and the glossary (`locales/glossary.json`), which `HookPoints.cs:15-23` does not list (P10) | (1) **no plugin, unchanged**: a loop over a stand-in run with no plugin on the point makes the same report and log as before, and no teller starts; (2) **a session that parks is told once**, with its quest, and one that ends, with its quest and its adapter where it names one; an intake's park names `ask`; a helper's park and a reviewer's park, `chat` records the driver started, are told; the park and ending of a conversation the person holds, a help room's or an unmarked `chat`, are not told (P2); a headless `--once` run tells `work.landed` alone; (3) **a landing is told once** with its code, by the lease's loop and not by a `--share` loop's, and a plugin that declares `work/event` beside `session/ended` hears nothing in a `--share` loop; (4) **once and in order**: three events heard once each, `seq` contiguous; **two producers at once**, the look's end and a landing beside it each telling many events, every `seq` minted once and every plugin's queue in `seq` order (C5.2); (5) a full queue drops the oldest and the next event says `missed`; (6) a plugin on `work/event` alone answering late three times is stopped, logged `plugin.stopped` with the reason `late`, and started again only after its back-off's wait, never at the next look, a third such stop within the hour marking it unsound; a second plugin is not held by it (O11); (7) **a plugin joining while events are told** hears first the event after its handshake's `seq`, and **the last event before a stop**, queued when its plugin stopped, is counted by the next handshake's `seq` (C5.2); (8) the census over `WorkEventGrammar`, seen failing on a `note`, a `repository` or a `workspace` added and on an id outside its shape; an `adapter` outside the build's names left out; (9) **the last event of a run told as `events.missed`**: its required field failing (a word-shaped `session`), it is told at once in its place with its own `seq`, and no later event, disconnection or restart is needed (K3.1, K5.2); (10) **a busy lease read changes nothing** (O6, P6): the lease file held busy at a look's reconcile, the plugin stays wanted and running, is told that look's events and counts no exit; a reconcile that reads *not held* stops it, and that stop counts no exit; (11) the handshake's `build` is `RelayBuild.Of` of a laid-out install's `app/daoris-driver/`, and a changed companion library changes it (K2.2); (12) the catalogue's test holds `event.unsent` and the `late` stop; (13) **the baseline said** (C4-3.1, C4-5.2): a plugin started at the first look's reconcile and a stand-in subscriber reconciling on its handshake before that look's snapshot, a session parked between the two and taken into the baseline: the next frame the plugin hears is one `events.missed` with the next `seq`, told after that look's `Observe`, with no later look, event, disconnection or restart; the same with no active session at all, an empty baseline; in both loops, the shell's and the headless host's watch mode; and a `drive --once` run telling none |
| **EVENTHOOK1c** | examples, driver, modules (`InstancePipe.Security` moved into the driver); after 1a's M5, M9 and M10, and 1b | `event-relay` (§5): the watch set before the read, a failed watch set again, the folder read again every five minutes and whenever a connection ends, at most 8 subscribers, no filter, the first line with `build`; `daoris-driver events relay --once` and `--where` (§6.1): out of `RoleTerminal.Runs`, routed at the host's entry before the home's adoption, the generic lease and the machine log, every entry, home and lease failure of both forms a `RelaySaid` sentence, `--where`'s lease taken by the generic path's own call (C4-2.2, C4-5.3), its home by `--home`, classed in `VerbLeases` (the relay holding nothing, `--where` holding the lease for its run), the copy at the home's `bin` entry named by `RelayBuild.Of`, staged and moved whole, its `in-use` file, removed by a rename first (P8), no junction followed, run only from its build's folder; `--where`'s lines fixed sentences (P7); every line from `RelaySaid`; step 3 by the holder's agents' job, a client it cannot check taken untold (P1), and the first line's schema; a second pipe instance listening (P4) and the second client ending it; the watermark, the first line's gap, `events.missed`, the batch's bounds, one gap line a batch and the diagnostics tallied (C4-2.3), and its closing watermark line; one connection; the 15-minute bound and its reason codes (P5); what it reads of the catalogue | `node --test` in the plugin's folder: a stand-in loop speaking the hook wire tells events to the real plugin process, which connects to a stand-in subscriber's pipe found by its file and sends every frame unchanged; a file written between the watch and the read connected to once; a watch's error answered by a new watch and a read; **a watch's callback suppressed, and the file connected at the next reconciliation** (K4.1), the test handing the plugin a short interval; **a ninth subscriber connected when a slot frees, and a file whose first try failed connected at the next reconciliation, neither file rewritten** (K3.2); a slow subscriber's connection closed while the plugin still answers at once; a file whose `pipe` is not of the relay's shape left; a sample run connecting to nothing. **Two relays with different `--kinds`**, across a restart, hear the same frames, each prints only its kinds, and neither says a gap for what it left out, `--since` taken from each batch's last line (C3.3, O7); **a relay that connects while events are in flight**, and one whose watermark the plugin's first line passes by exactly one, each print the gap line at once (C3.2); an `events.missed` frame said as a gap whatever `--kinds` says (K3.1). The verb's own classes: **in a role's terminal `events relay` is refused `role-terminal`** (O1), with the role terminal's one fixed sentence; the relay opens no machine log and prunes nothing (C5.3); **the entry's output** (C4-2.2, C4-5.3): `--where` while a stand-in verb's lease holds the home for the whole wait, its `what` written as `Merge-every-landed-branch.now` and once as a path, the wait shortened by a held clock: exit 1 with the one held line, and neither stream holds the `what`, the home or any path; the lease file made unwritable, and the `bin` entry present at both its places, each the relay-stopped line with its code, exit 2, nothing of an exception's message on either stream; and the lease's behaviour kept: `--where` waits on that act's lease, and once it lets go within the wait holds the home for its run and lets it go, and beside a stand-in instance holds nothing and lays the copy out; a copy with no `--home` and no `DAORIS_HOME` refused, naming `--where`; every Daoris key and both messaging variables stripped, and the holder key the host took cleared; what it touches is its pipe, two reads of the lease, the catalogue's place, its `in-use` file and its subscriber file; a dead relay's file removed at its start, a live one's kept; **a first line out of its schema** (`Merge-every-landed-branch.now` as `run`, a sixth field, a line of 513 bytes) refused, and nothing of it on either stream (O2, K2.1); **both streams of every case read against `RelaySaid`**, and its census seen failing on a sentence given a value read from the pipe (O3); a stranger, an uncontained plugin (said `not contained`), a client outside the holder's agents' job, a `--share` loop's plugin and a `run: "sample"` refused, a first line not sent within 5 s closed; **a plugin started through a version manager's shim, inside the job, taken** (O8); **what step 3 does not refuse**: a second launcher start in the holder's job, standing for an agent, taken, and its frame with `Merge-every-landed-branch.now` as `session` dropped and said; **a silent client holding the connection, and the relay ending with the second-client line when the plugin connects through its second, listening instance** (O4, P4); **the job not opening** (a stand-in for `NamedHolds` answering null), a client taken untold with its one fixed line and its first line still checked (P1); a `seq` gap and a `missed` above 0 said, a repeat dropped without a gap; a 51st event counted; **many gapped and repeated frames together** (C4-2.3): a stand-in plugin sending, in one batch, `seq` gaps, `missed` above 0, `events.missed` frames, repeats, refused clients and bad frames never three in a row, and the relay printing one gap line, at most 53 lines on stdout and one tally line for each diagnostic kind on stderr; the connection's end, three bad frames and the slow mark each ending the relay with the gap line; **a copy of another build ending at the first line with its update line, and a changed companion library giving `--where` a new copy** (K2.2, O5); `--session-pid`'s exit ending it with nothing printed; a relay run from anywhere but its build's folder refused, naming `--where`; **the bound**, its clock held: a relay no plugin reaches ending with the no-events line and its reason (`no-client`, `refused`, `not-contained`) while a stand-in instance holds the lease, and waiting while none does (O4, K3.2, P5). **The copy** (C2.3, C5.3): two `--where` at once leave one whole copy and no staging folder; a `--where` of a newer build keeps a build a relay holds in use and removes one none holds; a relay starting while its build is removed refuses; a junction under `events-relay/` pointing out of the home is said and left, and nothing it points at is removed; a `--where` waits on a lease another act holds; **a removal stopped partway** (a file held open in the renamed folder) leaves a removal name the next `--where` deletes, never the build's name, and a build folder without its `in-use` file is laid out again, never printed (P8); **`--where`'s lines read against `RelaySaid`**, an entry named like a sentence under `events-relay/` never printed (P7). **The lease and the swap** (R2): a relay started while no Daoris runs holds no lease, and the desktop's loop takes the home's lease while it runs; in a laid-out install, a staged update swaps `app/` while a relay runs from its copy. **A Process-lane class** lays out the built launcher in an install, with the example installed and switched on in that home and the stand-in service `drive --once` needs, and runs the built host's `drive --once` there as the lease's holder (O8); the host's own reconcile starts the plugin, so it joins the host's agents' job, never the test's (`AgentStarts.cs:33`; round 4's named changes, P9), and the run is kept alive until the relay prints; it sees step 3 pass by the job and one batch printed. Every script is under the tests, spawned hidden: no window, no console, no real agent |
| **EVENTHOOK1d** | a later round of this design; after 1b | the quest kinds and `person.answered`, designed again from the quests' recorded operations and the pipe's confirmation record, never the look's snapshot (§4.2, C5.4) | that round's reads |
| **EVENTHOOK1e** | a later round of this design; once the first build is in use | the inbox adapter and the knowledge server's channel (§6.2), each with its own measurements (M2-M4 and M8; M6 and M7), the channel with a door that reads the install and a hand-over across the launcher's swap (C5.5) | that round's reads |
| **EVENTHOOK1f** | a request; after 1c | a quest to Daoris.Plugins to offer `event-relay` as Daoris's own (D120 point 3), carrying the hook wire's `protocolVersion` and the `WorkEventGrammar.Version` the plugin speaks and the contract fixtures 1c's tests use, so the package stays held to the example (C5.6); its package carries the plugin alone, no relay | that repository's answer |
| **EVENTHOOK1g** | driver, modules (each executable's entry); any time, waiting on no measurement (round 4, O9) | §6.3: `GitWiring.Removed` gains `CLAUDE_CODE_MESSAGING_SOCKET` and `CLAUDE_CODE_MESSAGING_TOKEN`; each Daoris executable removes both at its entry; the wiring census names both | a child's start lacks both though the parent holds them; each executable's entry removes them; the wiring census row, seen failing on one name left out |
| **EVENTHOOK1h** | the steward, on the install; after 1c is merged and the install republished (round 4, O10) | the installed proof: the owner adds `event-relay` by *Install from a folder* and switches it on (§5.1); the owner's own session outside Daoris runs `--where`, then `--once`; a real session on the install parks | the measurements record gains it: the park's wake reached the session as one batch ending with its watermark, and the steward's shell loop is stopped; or why not |

`work.ended` is no row here: it waits on a row that owns link 1's emission (§1.4). Each row records its plans and findings as they land.
A row that builds the driver's half runs only its own classes, filtered. The first build is 1a, 1b and 1c, with 1g beside them and 1h
its installed proof, and with it the steward stops polling; 1d and 1e are designs, not builds.

## 10. Rejected

- **The driver posting into the inbox itself.** It needs the session's token, handed to the session's children only (§2.1).
- **The plugin starting the relay.** It is in the agents' job (§1.5) and no child of the session, so on Windows its posts are unverified.
- **The subscriber reading a file the plugin appends, or calling Daoris on a clock.** That is the poll this removes, moved. The relay's
  one read of the lease every 15 minutes while no plugin reaches it (§6.1 step 6) is not that: it asks whether Daoris runs, never what
  its sessions do, and wakes the session only to say a failure.
- **Forwarding the shell's bus.** D169 point 4: the bus is the shell's alone, and a headless loop has none (§1.2).
- **A point per kind.** About twelve places each (§1.1), for kinds a field distinguishes.
- **Carrying `note`, titles or commit subjects.** Paths and authored words (§1.9, §4.3).
- **A replay on reconnection.** An event is a wake; a store of events would be a second record of what the ledger already holds.
- **A filter in the driver.** It would keep a subscriber's choice in Daoris's records (§5.2). A scope the person grants the plugin is
  not a subscriber's choice, and is enforced there when one is needed (§4.3).
- **The channel first.** A research preview behind the development flag, interactive only, Windows support not stated (§2.2).
- **`session.ended` from `EndingsAsync`** (round 2): a park never reaches it (§1.3), and its frame would need the person's stops filtered
  again; the attention watch already says each once, leaves those out, and is a baseline at its first look (F1).
- **`landed` as `work.ended`** (round 2): `work.ended` is the line holding the work, after the steward's merge, and nothing emits it (F2).
- **The relay as a script in the plugin's folder, or a pinned copy of it with its hash checked** (round 2): a pinned copy keeps plugin
  code running uncontained, needs the hash kept where agents cannot rewrite it, which on one user's machine is nowhere, and leaves the
  pipe's elevation open (F6), since Node cannot set its owner. Daoris's own verb has none of those (§3).
- **The plugin serving the pipe** (round 2): Node's pipe takes the default descriptor, refused across elevations (PIPEOWNER1), and a
  subscriber finding it through a file the plugin writes can be pointed elsewhere by rewriting it (F5, F6).
- **A loopback socket gated by a key** (round 2): the key would sit in a file any process of the user reads, and the port is open to
  every user of the machine.
- **One address file shared by every process of the plugin** (round 2): a second process overwrites and removes it (F7).
- **A plugin on `work/event` in every loop** (round 2): a `--share` loop's second process would tell every event twice (F7).
- **Resting the relay's promise on its client check** (round 2's read): the check admits every agent and reads a file any process can
  rewrite (§6.1 step 3); the promise rests on the words (R1).
- **One pattern for every id and name** (round 2's read): `[A-Za-z0-9._-]{1,64}` holds a sentence and refuses `daoris:help` (R1, R5).
- **Ending the relay at an update, rather than running it from a copy** (round 2's read named both): the relay learns of an update only
  by a watch on the install's `update/` folder, a relay started again before the swap runs from `app/` again and holds it, and the
  session learns when Daoris is back only by a poll. A copy outside `app/` never holds the swap at all, and the grammar's change ends
  a stale one at the first connection of the new build (§6.1 step 0).
- **A relay that runs from `app/` and waits on its own copy** (round 2's read): the waiting process is itself in `app/`; and one that
  starts its copy and exits ends the background command at once, waking the session for nothing (§6.1).
- **Taking the holder key only for `sessions supervise`** (round 2's read named both): it moves the host's entry seam, which every verb
  passes, for one verb's sake; clearing it in the one verb that lives long is the smaller change (§6.1 step 1).
- **Names in the frame, used only to filter** (round 3, C2.1, C7.1): a name is free text the registry only trims, so it may be shaped
  like a path, and filtering at the relay still handed it to the plugin and to every subscriber. Kept in the driver, it leaves nowhere.
- **A filter in the plugin** (round 3, C3.3): a frame it left out looked lost to the relay, so every ordinary filter set off a reread.
  The relay filters, and its watermark is the source's (§5.2, §6.1 step 5).
- **A workspace filter by default, or a scope each subscriber names** (round 3, C3.1, C4.1): a subscriber's own filter grants nothing,
  and a quest's park names no workspace at its source. The first build's one scope is the home, the person's switch its grant (§4.3).
- **The relay rewriting its file to be connected again** (round 3, replacing R4): a reconnection with no bound on its tries, and slow
  marks that come back. A relay that ends with its one connection and says the gap is bounded by its shape, and wakes the steward to read
  the truth (§5.2, §6.1 step 6).
- **A help-room park told** (round 3, replacing R5's answer): with no name in the frame no subscriber could leave it out, and it is the
  person's own conversation with Ask Daoris (§4.2).
- **The quest kinds from the look's snapshot** (round 3, C5.4): the open list keeps a held quest whatever its state, and between two
  looks a quest can be published and closed, or closed and reopened. Their round reads recorded operations instead (§4.2).
- **Of Codex's smaller shape, a waiter that writes nothing** (round 3, C6.1): the plugin finds a subscriber only by the file it writes,
  since the plugin cannot serve a pipe that reaches across elevations (PIPEOWNER1), and a waiter must run from a copy so as not to hold
  the swap. Both writes are bounded and kept to their folders (§6.1 steps 0 and 1). The rest of the smaller shape is taken whole.
- **Dropping §6.3 with the inbox** (round 3): the two variables reach every agent Daoris starts from a Claude Code session's shell
  today, so keeping them out is a fix of its own, whatever adapter is built.
- **`events relay` in `RoleTerminal.Runs`** (round 3's, withdrawn by round 4, O1): it opened Daoris's half of a role holder's gate
  alone. The holder's harness allows fixed command names and nothing else (`HolderTerminal.cs:41-48`), and `--where` is an alone path
  D209 refuses in a role's terminal; the role holder takes its own round and its own D209 amendment (§6.2).
- **The client a direct child of the launcher, with the install's image** (round 4, O8): it refused a `node` started through a version
  manager's shim and the built host a test runs as the holder, and needed a reader of parents and images only the modules have. The
  holder's agents' job asks the one thing step 3 asks, whose start the client is (§6.1 step 3).
- **A liveness marker the plugin holds open** (round 4, the first of O4's two ways): Node opens no file that is deleted as its process
  closes, and a marker absent while Daoris is closed would end every relay at once, waking the steward in a loop while Daoris stays
  closed. The bound reads the lease only when it fires, and waits while Daoris is closed (§6.1 step 6).
- **A bound on a connected relay** (round 4): a plugin's quiet is normal, a silent client is said by the second-client rule, and a
  plugin rewritten to pass nothing is a process of the user's doing, which nothing here stands against (§6.1).
- **Staleness by `grammar` alone, or a build named by one assembly's hash** (round 4, O5, K2.2): either leaves an old copy running
  against a newer build's lease, catalogue or job, or reuses a copy whose companion library changed. The build is the whole bundle.
- **Nothing read again on a clock** (rounds 1 to 3, F11; round 4, K4.1): D173's rule puts a slow reconciliation behind every watcher.
  The plugin's five-minute read of its own folder is that reconciliation, and no subscriber polls the sessions.
- **A first line echoed, or a stderr line built from what was read** (round 4, O2, O3, K2.1): either put authored words into the
  session's turn past every check the frames meet. The first line has a schema, and every line the relay writes is a fixed sentence.
- **The agents' job given a descriptor that grants the user's SID query** (round 4's named changes, P1, the read's option (b)): step 3
  could then open an elevated holder's job from a relay that is not elevated. It changes the agents' job's descriptor, which D201's
  judge also opens (`StandingCaller.cs:121`; the terminal-trust design §3.4), so it would unblind that judge across elevations too, a
  change to D201 that needs its own read. Taking a client step 3 cannot check untold (option (a)) costs only a filter of strangers that
  nothing rests on (§6.1 step 3).
- **A catch around the relay's own code alone** (Codex's read of round 4, C4-2.2): the host's lease printers run before any verb's own
  code, so such a catch never holds what they print. The verb is routed before them, and its console takes the home and the lease by
  the host's own calls (§6.1 step 1).
- **The kill's wake as a gate of the first build** (C4-3.3, its second way): a relay is killed by a process of the user, the session's
  own end or the machine's, none a failure of Daoris's, and today's re-armed poll meets a kill the same way. The promise names the limit
  instead, and the measurements record says which answer M5 gave (§6.1 step 6, §7).

## 11. What the gates do not cover

This is a design and nothing is built. The code was read at `5d19f6b3a`, again at `4de8a5099` for round 2's read's named changes, and
again at `972b17577` for round 3 and at `b4f096cc0` for round 4, and each statement in §1 names its lines; the quick checks
(`doc-duplicates`, `dogfood`) check this document's shape and links, none of its words. Claude Code's behaviour in §2 is its
documentation as read on 2026-10-24, and none of it was run here (§2.3). The pipe's reach across elevations and the client's ancestry
read across them (§6.1 steps 2 and 3) are the instance pipe's precedent, not measured for this pipe (M9, M10). The background
command's wake (§2.4) is what the steward's loop relies on, not measured as a record (M5). The minted shapes in §4.3 are read from the
code that mints each id today; a change to a mint changes the grammar, and 1b's census reads none of the mints. The plugin, the relay,
its copy and every bound in §4 to §6 exist only as rows. Round 3's file custody (§6.1 step 0) rests on how .NET and Windows treat a
file opened exclusive and deleted on close and a reparse point met in a walk, which 1c's proofs exercise and nothing here has run.
Round 4's step 3 rests on a named job opened for query, which M10 measures across elevations against the answer §1.7 expects; its
five-minute reconciliation and fifteen-minute bound are chosen, not measured, and 1h's installed proof is the first to see them on a
real install. Round 4's named changes rest a build's removal on a folder with a file open in it refusing a rename (§6.1 step 0), which
1c's proofs exercise, and the re-arm's allow rule on how Claude Code matches a prefix across a changed path, which M5 measures. Codex's
read of round 4 was read at `eced2688e` and its named changes checked at `9f48860ba`: the baseline's frame rests on the order in which
a look reconciles its plugins and then reads its sessions today (`Driver.cs:886-889`, `:924`), and the relay's route on the host's
entry order (`Host/Program.cs:357-449`), each of which 1b's and 1c's proofs exercise and nothing here has run.

## 12. Round 2: the read's findings and their answers

Every finding of the Opus foundation read of round 1 (`docs/2026-10-24-event-hook-reads.md` §4), each checked against the code on main
at `5d19f6b3a`. *Taken* means the design now says it; *answered* means it is not taken as named, for the reason given; *deferred* means a
later row carries it.

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **1** | **A park is no `session/ended`** | `Driver.cs:2855-2865`; `DriverIntakes.cs:414-416`; `DriverContinuations.cs:786-788` (each returns no `SessionEnded` for a park); `Attention.cs:210-261` (`Observe`); `DriverLoop.cs:916`, `Host/Program.cs:972` (both loops call it) | **Taken** as named: the session kinds are the attention watch's `Parked`, `QuestParked` and `Ended`, as `session.parked`, `quest.parked` and `session.ended`, told by both loops after `Observe`, each event's `Note` dropped (§4.2). §1.3 says where a park is seen; §1.9's park line is removed; D173's note point 2 is rewritten. Proved by 1b's *a session that parks is told once*. |
| **2** | **"Landed" mapped to `work.ended`** | `EndJudgement.cs:241-258`, `:263` (`Handed`); `RefWatch.cs:373` (link 1 emits `work.held` alone); `Driver.cs:1403-1410`; `AutoLander.cs:648-672`; `AutoLanding.cs:39-46` | **Taken**: `work.landed`, told where `AutoLander.Tried` keeps a try coded `landed` or `advanced`, with the code, by the lease's holder alone (§1.4, §4.2); it means a branch the install landed and nobody has merged yet. `work.ended` waits behind a row that owns link 1's emission, and is no row of this design (§9). The read named `LandDueAsync`; `Tried` is the one place inside it where each try is kept once with its code, beside the look or in it. |
| **3** | **The inbox token can reach Daoris and everything it starts** | `HookProcess.cs:85-106` (set over the inherited environment); `GitWiring.cs:40-47`, `:98`; `Tools.cs:679`, `:693`; `WorkingTree.cs:973`; `App/Program.cs:34`, `Host/Program.cs:345` | **Taken**, and wider than named: both names join `GitWiring.Removed`, so every child is handed neither, and each Daoris executable removes both at entry as it does the holder key, `daoris-driver` taking them for `events relay` alone (§6.3). §1.5 says the plugin inherits. M8 measures that a process from the session's Bash holds them and a child with `Removed` applied does not. Proved by 1b's proof 8. |
| **4** | **The relay is plugin code running uncontained** | D201 point 4 (`docs/decisions/D201.md:31`); `SupervisorProof.cs:10-20`; `GitWiring.cs:38-41`; `AgentStarts.cs:5-11` (plugins and agents in one job) | **Taken, by another way than named**: the relay is not plugin code at all but Daoris's own verb, `daoris-driver events relay`, from the install's build, which no plugin folder or package carries. It removes every Daoris key at entry, opens no Daoris door and makes no press, so the standing confirmation has nothing of its to cover, and it holds only the two messaging variables (§3, §6.1 step 1). The read's pinned copy is rejected with its reason (§10): its hash has no place agents cannot rewrite, and Node leaves F6 open. 1c proves the verb strips and opens nothing but its pipe and the inbox. |
| **5** | **The relay trusts its input** | §5.2 round 1 (`subscribe.json`); `InstanceClient.cs:196-211` (`GetNamedPipeServerProcessId`); `HomeLease.cs:31`, `:165` | **Taken**: the relay reads no address file. It serves its own pipe, takes a client only where the system names a process that is a launcher's direct child under the lease's holder, and validates every frame against `WorkEventGrammar`, the grammar the driver's census holds, before building a word (§6.1 steps 3 and 4). So no process redirects or feeds it by rewriting a file. §6.1 says plainly what Windows does not separate (one user's processes), and that step 4 still bounds such a process to a false wake in fixed words. 1c proves the refusals and the dropped frames. |
| **6** | **The endpoint names no ACL, owner or elevation** | `InstancePipe.cs:400-410`; `docs/FIX-LOG.md:1506-1520` (PIPEOWNER1); `docs/2026-10-16-terminal-rpc-design.md:145-152`; `docs/2026-10-23-terminal-trust-design.md:253-255` | **Taken**, the read's reverse direction: the relay serves, with `InstancePipe.Security` (the user's SID owner and alone, the network denied, `FirstPipeInstance`), and the plugin connects as a client, which needs no descriptor; an elevated plugin holds the user's SID, and a relay at either elevation owns its pipe by that SID, which is PIPEOWNER1's fix (§1.7, §3, §6.1 step 2). M9 measures it from both elevations, and confirms that the Node-made pipe fails, before 1c. |
| **7** | **A second process in the same data folder breaks delivery** | `PluginTrials.cs:58-78`; `PluginsRoutes.cs:399`; `Hooks.cs:702-710`; `Host/Program.cs:20-27` | **Taken**, all three of the read's changes: one subscriber file per process, each removing only its own, written by the subscriber and none by the plugin (§5.2); the kit's sample and a trial's handshake say `run: "sample"`, which the plugin connects nowhere with and the relay refuses (§5.4); a plugin on `work/event` wanted only by the lease's loop (§4.1). And the relay takes one plugin at a time (§6.1 step 2). Proved in 1b (proof 3) and 1c. |
| 8 | A restart cannot be seen | `Hooks.cs:147-150` (`initialize` without run or seq); `:1196-1206` | **Taken**: `initialize` gains `events: { run, seq }` for a plugin at `work/event`, and `seq` counts events minted while none listened, so a gap shows to a plugin that comes back; a subscriber reads the truth again on a gap, a new run or a reconnection (§4.1, §4.4, §5.2). |
| 9 | A hung plugin holds every event and every other plugin | `Hooks.cs:725-729`; `:1109`; `WorkBounds.cs:25` | **Taken**, both of the read's options: a queue and teller per plugin, so one holds only itself; and three late answers in a row stop it, to be started again at the next look (§4.4). Proved by 1b's proof 6. |
| 10 | The relay's default wakes too much, too often | `Attention.cs:246-258` (the rule); §2.1's limits | **Taken**: the driver tells only what the attention watch says, so the person's stops, limits and refused choices never reach a plugin (§4.2); the relay's default is the four steward kinds in the workspace it was started for, 30 s batches and at most 20 messages an hour, the held-back count said (§6.1 step 5). M4 measures the sustained rate at that pace. |
| 11 | The relay's lifetime is underspecified; its reconnect is a poll | round 1 §5.3, §6.1 step 5 | **Taken**: `--session-pid`, a process waited on; the relay ends when that process exits or the inbox is gone, never on a refusal (§6.1 step 7). With the direction reversed the relay rereads nothing: it waits on its pipe, and the plugin watches the subscribers' folder (§5.2). M5 measures that the session's process id is known to the start. |
| 12 | §6.2 and M7 skip the knowledge server's launcher | `tools/knowledge-server.mjs:18-29` | **Taken**: `DAORIS_CHANNEL_EVENTS` names the install's home, and M7 measures through `tools/knowledge-server.mjs` and across a build swap (§6.2, §7). Row 1e stays last and opt-in. |
| 13 | Three frame details unsaid | `DriverIntakes.cs:385`, `:416`, `:454`; `SessionEnded.cs:14-16`; `Attention.cs:256`; `DriverLoop.cs:892-900` | **Taken**: an intake's frame carries `ask` and no `repository`; each kind's workspace source is named, a park's set by `Observe` from its `SessionView`; the point covers driven sessions and intakes as the attention watch sees them, and not a conversation's end, which is the person's own (§4.2, §4.3). |
| 14 | Smaller claim errors | `Chain.cs:30`; `HookPoints.cs:139-144`, `:15-23`; `Hooks.cs:1096-1100` | **Taken**: `nodes` in §1.8's wakes; the `work/land` row lists `base`, `title`, the quest's title, `session`, `pullRequest`, `acceptedBy` and `tip` (§1.9); §1.2 says the state teller starts whatever listens, and §4.5 makes *no teller without a listener* the change 1b builds and proves; 1b's lanes are driver, modules and web (§9). |
| 15 | Order and sources in the rows | `Driver.cs:917`; `TASKS.md:384` | **Taken**: 1b waits on M3 or M6 answering *yes*, with the decision rule in §7; the quest kinds name `service.SnapshotAsync` and its scope, and judge *closed* by the quest's state (§4.2); `person.asked` is replaced by `person.answered` (§4.2). |
| 16 | The manifest does not say the plugin serves an endpoint | `Hooks.cs:178-182` | **Taken**: the plugin's description and the page's gloss for `work/event` say its events may be passed to programs on this machine (§5.1; D119's note). Round 2 makes the plugin a client, so it serves no endpoint; the words say what it does. |

## 13. Round 2's read: where each named change went

Every finding of the Opus foundation read of round 2 (`docs/2026-10-24-event-hook-reads.md`, round 2 §4), taken as the read names it,
each checked against the code on main at `4de8a5099`. R*n* is the read's finding *n*. Blocking in bold.

| # | Finding | Where it went | Checked against |
|---|---|---|---|
| **R1** | **Step 3 admits agents, and step 4's ids carry words** | §4.3: each generated id in its minted shape (session eight hex, quest twelve or six, ask six, confirmation thirty-two, run twelve), names bounded and only filtering; §6.1 step 3 says it refuses strangers (another user's process, a process no launcher started, a Daoris that does not hold the home, a trial) and never an agent, and that nothing that keeps words out rests on it or on any file the relay did not write, `run/home.lease` among them; step 5 posts ids, never `repository` or `workspace`; *What Windows does not draw* and §8 rest the promise on the words; §1.5, §1.7, §1.9 say the facts; §10 rejects the old pattern; 1c's proof takes a launcher's second child standing for an agent and drops `Merge-every-landed-branch.now` | `AgentStarts.cs:5-11`; `HomeLease.cs:53-64`, `:120`; `Sessions.cs:799`; `Quests.cs:704-707`, `:1238-1245`; `Asks.cs:403-408`; `PipeConfirmations.cs:274` |
| **R2** | **The relay's long life holds the home's lease and stops every update** | §6.1 step 0: `events relay` classed in `VerbLeases` with the verbs that hold nothing, beside `chat` and `run-heavy`, with its reason; run from a copy of its build under the home's `bin/events-relay/<build>/`, laid out by `--where`, never from `app/`; a stale copy ended by the grammar's change (CONNECTOR1's warning); §1.7a says the facts; §10 says why the copy and not the end at an update; 1c proves the desktop's loop takes the lease while a relay runs, and a staged update swaps while one runs | `VerbLeases.cs:10-35`, `:58`, `:60`, `:97`; `Host/Program.cs:377-381`; `DriverLoop.cs:1246-1253`; `tools/desktop-publish.mjs:367-368`; `Launcher/Program.cs:72`, `:82-87`, `:168-169`; `KnowledgeConnector.cs:127-133`, `:153` |
| R3 | Nothing tells the steward to read the truth again | §6.1 step 5: one fixed line for a new `run`, a `missed` above 0 or a `seq` gap, inside the bounds; §4.4 says the baseline swallows what happened while Daoris was closed; §6.1a's `--since`; 1c proves each | `Attention.cs:224` |
| R4 | A closed or refused connection is never reopened | §5.2: the relay rewrites its file each time the connection it held ends, and the plugin's watch connects again; a refusal alone rewrites nothing; the plugin takes only a `pipe` of the relay's name shape; §5.3, §5.4 follow; 1c proves it | the design's round 2 §5.2, §5.3, §6.1 step 2 |
| R5 | The grammar meets real values it rejects | §4.3: names take 1-128 characters with no control character; `HookFrames.Event` leaves out a failing optional field, or the event with its `seq` counted in `missed`, logged `event.unsent` by kind and field; §4.2: a help-room park is a `session.parked`; 1b's census tells `daoris:help` and a workspace with a space | `HelpRoom.cs:48`; `Workspaces.cs:26-28` |
| R6 | The frame's fields are not on the watch's events | §1.3 says what `AttentionEvent` carries and that `--once` and `--until-idle` never observe; §4.3: `Observe` carries an ending's quest and adapter and a park's quest; §4.2: a headless one-look run tells `work.landed` alone; the example's ids in their minted shapes; 1b's proof 2 checks them | `Attention.cs:45-58`, `:235-236`, `:253-257`; `Host/Program.cs:930-945`; `Sessions.cs:799` |
| R7 | Claims and lanes in §6 | §1.5 and §6.1 step 3: the launcher is `daoris-piece-start.exe`; §6.1 step 2: `InstancePipe.Security` moves into the driver beside `InstanceClient`, and 1c's lanes add the modules; §6.2: the knowledge server's twins held by a census, or a shared library, and why a plugin's data folder is not the store `tools/knowledge-server.mjs` keeps away; §6.1 step 1: the relay clears what `HolderKey.Take` took; §4.2's latency line excepts `work.landed`; §6.1 step 3 says where an uncontained plugin's refusal is said | `PieceLauncher.cs:29`; `Launcher/Program.cs:8-12`; `InstancePipe.cs:13`, `:400`; `Daoris.Desktop.Driver.Host.csproj:14`; `Daoris.Service.Mcp.csproj:14`; `tools/knowledge-server.mjs:19-21`; `Host/Program.cs:345`; `Driver.cs:1403-1432`; `AgentJobs.cs:46-49`, `:119-123` |
| R8 | The lease rule is only a rule of which plugins are wanted | §4.1: `Tell` at `work/event` tells nothing, and mints no `seq`, where `_holdsHome()` is false; 1b's proof 3 includes a plugin that declares `work/event` beside `session/ended` | `Hooks.cs:640`, `:696`, `:705-708` |
| R9 | A plugin late at every look is restarted at every look | §4.4: a late replacement is an unasked exit under `WorkBounds.Backoff` and the *unsound* rule, logged `plugin.stopped` with the reason `late`, beside the per-call `late` line; 1b's proof 6; D119's note | `WorkBounds.cs:21-27`; `Hooks.cs:412-415`; `PluginEvents.cs:13` |
| R10 | `quest.closed` has no source in the snapshot | given a real source, not dropped: §4.2 reads `/api/quests?includeClosed=true` for the quests that left the open list, in that look alone, and judges by that state; 1d's row and proof | `ServiceClient.cs:139`, `:154-158`; `Quests.cs:3990` |
| R11 | The gates, and the cheaper first wake | §9: 1c after M5, its inbox adapter after M2 and M3, 1e after 1c; §6.1a: `events relay --once`, which prints and ends at its first batch, is 1c's first adapter, depending on none of M2-M4, the inbox its upgrade; §2.4 names the background command's wake; M5 measures it; §7's decision rule follows, so 1b no longer waits on M3 or M6 where M5's wake answers | `TASKS.md:459` |
| R12 | 1c's Process-lane proof cannot pass honestly as written | §9 1c: the class lays out the built launcher and takes the home's lease, as `AgentStartsProcessTests` and `PartnerProcessTests` do; §6.1 step 1 names the lease read and the file write among what the relay touches | `PieceStarts.cs:16-20`; `AgentJobs.cs:119-123`; `Driver.Tests/Agents/AgentStartsProcessTests.cs`; `Driver.Tests/Loop/PartnerProcessTests.cs` |

## 14. Round 3: Codex's read answered

Every finding of Codex's read of round 2 with its named changes (`docs/2026-10-24-event-hook-reads.md`, *Codex, round 2*), each checked
against the code on main at `972b17577`, the commit it read. C*s.n* is its finding *n* of section *s*; its comparison with the earlier
reads is C7.1 and C7.2. *Taken* means the design now says it, and where; *disputed* would name the code line that shows why not, and no
finding is disputed: each held where it was checked. Blocking in bold.

| # | Finding | Checked against | Answer |
|---|---|---|---|
| **C1.1** | **The steward's terminal cannot start the verb** | `Host/Program.cs:368` (the gate, before every verb); `SupervisorProof.cs:54-60`, `:70`, `:97-106` (`Runs` names no `events`); D209 point 2 (the steward's tab carries `DAORIS_ROLE_KEY`) | **Taken**: `RoleTerminal.Runs` gains `events relay` by its exact prefix, `--where` and `--once` alike, neither a press nor a door of Daoris's; every other verb is still refused in a role's terminal (§1.7b, §6.1 step 1). 1c runs both in a role's terminal and sees another refused. |
| C1.2 | Two descriptions overstate their sources | `ServiceClient.cs:157`, `Quests.cs:3987-3990` (the closed read is the whole list); `Driver.cs:1424-1438` (the landing beside the look) | **Taken**: §1.7b says the closed read is the whole list, filtered after it is read, and the quest kinds leave the snapshot altogether (C5.4). §4.2 no longer says `work.landed` comes before the watch: it is told where the landing keeps its try, before or after the watch's events of the same look, and `seq` orders what was told (§1.4, §4.2). |
| **C2.1** | **Names leave before the relay removes them** | `Workspaces.cs:26-28` (a name only trimmed); round 2's §4.3 (names of 1-128 characters) and §5.3 (each frame unchanged to subscribers) | **Taken, wider than named**: no name is in any frame. `repository`, `workspace`, `to` and `from` leave the grammar, so the projection is enforced where the frame is built, before any plugin or subscriber (§4.3). Who may hear what is defined: one scope, this home, granted by the person's switch, ids and codes only (§4.2, §4.3, §5.1); a narrower scope later is a grant the driver enforces. A help room's sessions are not told, since nothing could leave them out by name (§4.2). |
| **C2.2** | **The same-user threat claim is too strong** | round 2's §6.1 step 0 (the copy in the home's `bin`) and *What Windows does not draw*; `DaorisHome.cs:400` | **Taken**: the promise is narrowed to an intact relay. While its own files are intact, whatever frames reach it, it prints only fixed words over ids in their minted shapes; the shapes authenticate neither an event nor the record it names; a process that rewrites the relay's copy or the session's files is outside what any of this holds against; every act after a wake rests on the session's own reads and permissions (§6.1, §8). |
| **C2.3** | **Filesystem custody is unspecified** | round 2's §5.2 and §6.1 step 0; `DaorisHome.cs:119-127`, `:400`; `VerbLeases.cs:97` | **Taken**: `--where` holds the home's lease for its run, stages a copy and moves it in whole with no overwrite, so two at once leave one copy; each relay holds its build's `in-use` file, and a build is removed only while that file is held alone, so no build is removed under a relay; no reparse point is followed or removed through, under `events-relay/` or `subscribers/`; the subscriber file is moved in whole and removed by its name (§6.1 steps 0 and 1). 1c proves each, with the swap beside them. |
| C2.4 | §8 opens no answer-to-action path; a wake is never a confirmation | `Hooks.cs:532-534` | **Taken**: §8 says a wake is never a confirmation, an approval, or evidence that what it names is so. |
| **C3.1** | **The default workspace filter cannot handle its own events** | `SessionGroups.cs:563-570` (`QuestPark` names no workspace); `Attention.cs:256` (an `Ended`'s may be none) | **Taken, by another way**: there is no workspace filter. Each kind's scope at its source is the home the lease's loop drives, known for every kind, so none is unknown and none has to fail closed (§4.2). The quest kinds and `person.answered` wait for their round (C5.4). |
| **C3.2** | **Restart recovery still depends on another event arriving** | `Attention.cs:224` (the baseline); round 2's §6.1 steps 5 to 7 | **Taken**: the plugin's first line settles a loss at once. Another `run`, any `seq` but the watermark's, or no `--since`, and the relay prints the gap line and ends, with no later event needed, also when the one missed is exactly the next `seq` and when no session was running (§6.1 step 5). Every end of a relay is said or wakes the session itself: its connection's end prints the gap, a kill ends the background command, and the next relay's first line says the gap (§6.1 step 6). The inbox's refused batch is gone with the inbox (§6.2). |
| **C3.3** | **Filtered sequences are indistinguishable from loss** | round 2's §4.4, §5.2 and §6.1 step 5 | **Taken**: the plugin filters nothing and the relay filters, so its watermark is the source's `seq` and a gap is a loss; a kind it leaves out moves the watermark; a repeated or older frame is dropped without a gap; `Tell` mints and enqueues under one lock, so every queue is in `seq` order with two producers (§4.4, §5.2, §6.1 step 5). 1c proves two relays with different kinds, and one that connects while events are in flight. |
| **C3.4** | **Discovery and resource bounds are incomplete** | round 2's §5.2, §5.3 (64 a subscriber) and §6.1 step 5 (30 s batches) | **Taken**: the watch is set before the folder is read; a watch's error or overflow sets it again and reads again, and one that cannot be set ends the plugin into the work wire's back-off; nothing reconnects, since a relay has one connection; at most 8 subscribers, a first line within 5 s, a line of at most 4 KB, a batch of at most 50 events and 52 lines (§5.2, §5.3, §6.1 steps 2, 4 and 5). |
| **C4.1** | **Participant visibility has no subscription rule** | D173:11-19 (ADR-B, ADR-F); D173's EVENTHOOK1 note (`:5409-5417`); D182:18-23 | **Taken**: §4.3 says who may hear what: one scope, the home, granted by the person's switch, at ADR-B's opaque level, ids and codes whose referents are read through the reader's own doors and permissions (ADR-F); a narrower scope later is the person's grant, enforced in the driver. D182 point 2 keeps invalidations for plugins, so the wake is compatible with it, as Codex says. |
| C4.2 | Logging and home-layout work is missing from the rows | `docs/2026-09-30-machine-log-design.md:128-135`; `PluginEvents.cs:8-78`; `DaorisHome.cs:119`, `:400` | **Taken**: 1b adds `event.unsent` and `plugin.stopped`'s `late` to `PluginEvents` and to the catalogue, with the catalogue's test; the copy's place is `DaorisHome.Entry(home, HomeEntry.Bin, "events-relay", <build>)`, no second layout (§6.1 step 0, §9). |
| **C5.1** | **1a's decision rule contradicts the dependency graph** | round 2's §7 and §9 | **Taken**: each adapter's prerequisites are its own (§7). The first build rests on M5, M9 and M10; the inbox on M2-M4, M8 and M5's lifetime; the channel on M6 and M7. Neither of those rests on the background wake, and each would take 1b and the plugin as they are, so a *yes* from either has its own round to build in. 1a measures only the first build's. |
| **C5.2** | **1b lacks a complete frame contract** | `SessionEnded.cs:14-16` (`Adapter` nullable); round 2's §4.3 | **Taken**: §4.3's table gives each field its kinds, shape and need, and says once what a failing value does; `adapter` is optional and from a closed set; `to` and `from` leave with the quest kinds. 1b's proofs add two producers at once, a plugin joining while events are told, and the last event before a stop (§4.4, §9). |
| **C5.3** | **1c needs a complete host-entry and copied-build contract** | `Host/Program.cs:368`, `:416-441`, `:445`; `MachineLog.cs:80-85`; `DaorisHome.cs:62-80`, `:94-103` | **Taken**: the relay is routed before the machine log opens, as `trace` and `run-heavy` are, and writes no log. A copy finds no install, so `--where` prints `--home <home>` and the relay refuses without a home, its install read as that home's parent. Copying, removal and the swap are proved together (§1.7b, §6.1 steps 0 and 1, §9). |
| **C5.4** | **1d still misses legitimate closures** | `Quests.cs:3990` (`held = 1` keeps a closed quest in the open list) | **Taken**: the snapshot is dropped as their source; 1d becomes a later round that reads the quests' recorded operations (§4.2, §9, §10). |
| **C5.5** | **Delivery does not establish usable reconciliation, for 1e** | `tools/knowledge-server.mjs:134-143`, `:322-327` | **Taken as 1e's requirement**: the channel needs a door that reads the install and a hand-over across the launcher's swap. 1e is deferred, and §6.2 carries both. |
| C5.6 | 1f has the right boundary; include versions and fixtures | D120:24-31; D120's EVENTHOOK1 note | **Taken**: the request carries `protocolVersion`, `WorkEventGrammar.Version` and 1c's contract fixtures (§9). |
| C6.1 | Ship only the measured `--once` path first | round 2's §6.1a, §7 and §9 | **Adopted**, since the steward stops polling with it: the first build is 1a (M1, M5, M8, M9, M10), 1b (the point, four kinds, no name, one scope) and 1c (the plugin and `--once`) (§3, §4.2, §6.1, §9); the inbox, the channel, the quest kinds and `person.answered` are later rounds (1d, 1e). Not adopted, each with its reason in §10: a waiter that writes nothing, and dropping §6.3. |
| **C7.1** | **The driver frame was not already safe** (against both Opus reads) | Opus round 1 (reads `:50`) and round 2 (`:332`) called it bounded and path-free; round 2's §4.3 names rule; `Workspaces.cs:26-28` | **Decided for Codex, on the evidence**: a workspace name is only trimmed, so the grammar took path-shaped names, and the plugin passed them on unchanged. No name is in the frame now (C2.1). |
| **C7.2** | **Delivery corrections need another design read** (against the Opus round 2 verdict) | Opus round 2's verdict (reads `:515-522`) and *bounded at every hop* (`:389`) | **Decided for Codex, on the evidence**: C3.1 to C3.4 are protocol choices (where filtering happens, what a first line settles, how many connections a relay has, a batch's bound), not wording, and round 3 makes them. So round 3 is read again by both readers before it is accepted (Standing). |

## 15. Round 4: the reads of round 3 answered

Every finding of both reads of round 3 (`docs/2026-10-24-event-hook-reads.md`, *Opus (foundation), round 3* and *Codex, round 3*), each
checked against the code on main at `b4f096cc0`, merged into this branch. Both read `c685459f4`, whose code is `972b17577`'s; between it
and `b4f096cc0` only `Host/Program.cs` (four lines after line 194), `DriverLoop.cs`, `InstancePipe.cs` (after line 1002),
`PipeCensus.cs` (three lines after line 228) and `StandingConfirmation.cs` (its steward flag from `:293` to `:336`; added by round 4's
named changes, P10) moved of what either cites, so a line below is `b4f096cc0`'s where it differs from the
read's. O*n* is the Opus reader's finding *n*; K*s.n* is Codex's finding *n* of section *s*. Where the two overlap the stricter is taken;
on the subscriber the Opus reader's finding 1 is taken. No finding is disputed: each held where it was checked. Blocking in bold.

| # | Finding | Checked against | Answer |
|---|---|---|---|
| **O1** | **C1.1 is half-closed, and the design never says which session subscribes** | `Modules/Platform/HolderTerminal.cs:41-48` (the holder's fixed allowlist); `D209.md:77-78` (every alone path refused); `StandingConfirmation.cs:336` (the steward role not offered) | **Taken as named**: the first build's subscriber is the owner's own session outside Daoris; `events relay` stays out of `RoleTerminal.Runs`, and 1c proves it refused in a role's terminal; the role holder is a later round naming its D209 amendment (a fixed allowlist element for a fixed command name, the copy laid out by the instance) (§1.7b, §3, §6.1, §6.2, §10). Codex's K-table called C1.1 taken; the stricter, the Opus reader's, is taken. |
| **O2** | **The plugin's first line reaches stdout unvalidated** | design round 3 §6.1 steps 3 to 6 (the first line's `run` and `seq` printed, only `plugin` and `sample` checked) | **Taken**: the first line has a closed schema (`plugin`, `run` `^[0-9a-f]{12}$`, `seq` 0 to 2^53, `grammar` from 1, `build` 64 hex, no other field, 512 bytes), checked before any of it is used; one that fails is refused and none of it printed; `--since` checked the same; 1c sends `Merge-every-landed-branch.now` as `run` and sees nothing of it on either stream (§5.2, §6.1 steps 3 and 5, §9). |
| **O3** | **stderr is part of the wake, and its words are unbound** | design round 3 §6.1 steps 2 to 6 (refusals, drops and repeats "said on stderr") | **Taken**: every line on either stream is one of `RelaySaid`, fixed sentences whose only values are checked ids, closed codes and counts, never a value read, a file's content, a path or an exception's message; a census holds it; M5 records whether stderr reaches the session (§6.1 step 1, §7, §9). |
| **O4** | **A relay can wait forever with nothing said** | `WorkBounds.cs:21-27` (unsound until the person tries again); design round 3 §6.1 steps 2 and 6 | **Taken, by the bound**: both sentences are corrected (§6.1 step 6, *What Windows does not draw*); every 15 minutes with no connection, the relay reads the lease once and, where an instance holds the home, ends with the no-events line, and where none does waits; a second client passing step 3 while one is connected ends the relay with its own line; 1c proves a relay no plugin reaches and one held by a silent client. The marker, the first of the two ways named, is rejected with its reason (§10). |
| O5 | Staleness is keyed on `grammar` alone | design round 3 §6.1 step 0 | **Taken**, with K2.2: the handshake and the first line carry `build`, `RelayBuild.Of` of the whole bundle, and a relay of another build ends with its update line (§4.1, §5.2, §6.1 step 0). |
| O6 | A busy lease read drops an event with no gap | `Hooks.cs:640`, `:696`; `HomeLease.cs:195-202`, `:303-304` | **Taken**: `Tell` mints every `seq` whatever the lease says, and tells only where the reconcile's one read per look found it held; 1b's proof 10 (§4.1, §9). |
| O7 | A narrower `--kinds` makes a false gap across relays | design round 3 §6.1 step 5 (`--since` from the last printed line) | **Taken**: every batch ends with `Daoris: read up to [<run> #<seq>]`, the relay's own watermark, which `--since` takes; 1c's two-relay proof runs across a restart (§6.1 step 5, §9). |
| O8 | Step 3 and 1c's Process-lane proof | `AgentJobs.cs:77`; `PieceLauncher.cs:41`; `ProcessJob.cs:146-155`; `HomeLease.cs:31-34`; `HolderWalk.cs:96` | **Taken, by the agents' job** rather than the launcher's own, whose name needs the launcher's process id, which the relay has no honest way to learn, while the agents' job is named by the holder's, which the lease gives: the client must be in the lease holder's agents' job, which every launcher start of it joins, so a shim and a test's host pass and no parent or image reader is needed; the Process-lane class runs the built host's `drive --once` as the holder; M10 measures the job across elevations (§1.7b, §6.1 step 3, §7, §9). |
| O9 | §6.3 waits on M5 | design round 3 §7, §10 | **Taken**: §6.3 is row 1g, waiting on no measurement (§6.3, §7, §9). |
| O10 | No row installs the plugin or proves the stop on the install | `tools/desktop-publish.mjs:227` (`OFFERED_PLUGINS`); `PluginsRoutes.cs:186-190`; `PipeCensus.cs:289` | **Taken**: the owner adds it by *Install from a folder* (`PLUGIN_ADD`, a `Person` route) until 1f's package (§5.1); row 1h is the installed proof, recorded in the measurements record (§9). |
| O11 | Claims and records to correct | `VerbLeases.cs:92`; `DaorisHome.cs:170-183`, `:265-275`; `PluginCatalog.cs:39-40`, `:286`; `Hooks.cs:692-711`, `:786-791`; `Attention.cs:143-149`; `Planner.cs:788-792`; `TASKS.md` EVENTHOOK1 | **Taken, each**: the scope rests on what the frame holds, the switch being the person's only while Daoris runs (§4.3); the `bin` entry's place in an install (§1.7b); the catalogue read in what the relay touches (§6.1 step 1); the work wire's exit bookkeeping kept for `work/event` in 1b (§4.4, §9); a conversation's park is not told, by its record's kind, beside the help room's sessions (§4.2); the EVENTHOOK1 row no longer names the inbox as the first adapter or its proof. |
| O12 | M5 leaves out what step 6 rests on | design round 3 §6.1 step 6, §7 | **Taken**, with K5.1: M5 adds stderr, an end with nothing printed, an end by a kill, and a wait across compaction and `/clear`, run in the owner's own outside session (§7). |
| K1.1 | The holder key has another reader | `Host/Program.cs:660` (`StewardCommand` handed `HolderKey.Taken`) | **Taken**: §1.7a names `steward` beside `sessions supervise`. |
| K1.2 | The installed `bin` location is conditional | `DaorisHome.cs:170-183`, `:265-275`, `:400` | **Taken**, with O11 (§1.7b). |
| **K2.1** | **The first line bypasses the stated validation contract** | design round 3 §5.2, §6.1 steps 3 to 5 | **Taken**, with O2 and O3: a bounded schema checked before either output path, and fixed diagnostics that echo nothing (§6.1 steps 1 and 3). |
| **K2.2** | **One assembly hash does not identify the copied build** | `Daoris.Desktop.Driver.Host.csproj:14` (the driver library a separate file) | **Taken**: `<build>` is `RelayBuild.Of` of the whole `daoris-driver` bundle, every file by path, size and SHA-256, computed at every `--where`, so a changed companion library selects a new copy; a folder of that name is the same bytes, its name their hash; 1b proves the hash moves with a library, 1c that `--where` lays a new copy and an old relay ends (§6.1 step 0, §9). |
| K2.3 | The authority boundary is clearer; the handshake still carries paths | `Hooks.cs:147-150` | **Taken**: §4.3 says *path-free* is of the frame and of what the relay prints, not of every plugin's handshake. |
| **K3.1** | **A final unexportable event still disappears silently** | design round 3 §4.3 (an untold event counted only by the next) | **Taken**: an event whose required field fails is told as an `events.missed` frame in its place, with its own `seq`, so its loss is said at once; 1b's proof 9 makes it the last event of a run, with no later event, disconnection or restart (§4.3, §6.1 step 5, §9). |
| **K3.2** | **A subscriber can wait forever before obtaining a connection** | design round 3 §5.2, §5.3, §6.1 step 2 | **Taken**: the plugin reads its folder again whenever a connection ends (a freed slot) and every five minutes, retrying a file that did not answer at the next one; a relay that meets only refusals or no plugin is said by its bound; 1c proves a ninth subscriber and a failed first try connected without the file rewritten (§5.2, §5.3, §6.1 step 6, §9). |
| K3.3 | Recovery after a successful connection is coherent | `Attention.cs:224` | Agreed; nothing to change. |
| **K4.1** | **Subscriber discovery trusts a watcher without reconciliation** | `D173.md:196-198`; `docs/2026-10-16-fabric-events-design.md:52` | **Taken**: round 3's *nothing on a clock* broke D173's rule; the plugin's watch is backed by a reconciliation every five minutes, its own folder read again, and 1c proves a suppressed callback recovered by it (§5.2, §9, §10). |
| K5.1 | 1a's gate is usable; M5 should add an end before stdout | design round 3 §6.1 step 6, §7 | **Taken**, with O12 (§7). |
| **K5.2** | **1b still needs the final-rejection rule** | design round 3 §9 | **Taken**: 1b builds `events.missed` and proves the last event of a run said with no later event (§9 proof 9). |
| **K5.3** | **1c needs the remaining protocol choices before implementation** | design round 3 §9 | **Taken**: hello validation (O2, K2.1), failed and pending admission (K3.2, the second-client rule, the bound), silent watcher loss (K4.1) and the whole bundle's identity (K2.2) are each settled in §5 and §6.1 and each has a proof in 1c's row; with them, every failure path ends in recovery or an explicit wake, so 1a, 1b and 1c justify stopping the poll. |

## 16. Round 4's named changes, and where each went

Every finding of the Opus foundation reader's read of round 4 (`docs/2026-10-24-event-hook-reads.md`, *Opus (foundation), round 4*),
taken as the read names it, each checked against the code on main at `deb766f20`, merged into this branch. P*n* is the read's finding
*n*. Where the read offered a choice, the steward's is named. Codex's read of round 4 was owed then; it has landed since, and §17
places it. Blocking in bold.

| # | Finding | Where it went | Checked against |
|---|---|---|---|
| **P1** | **Step 3's job check fails in the layout the records name** | The steward's choice is **option (a)**: where the holder's job or the client will not open, step 3 takes the client untold, with one fixed `RelaySaid` line on stderr, its first line still checked (§6.1 step 3). The elevation split is stated for pipes and for named jobs in §1.7, in §1.7b's job bullet and in step 3. M10 confirms the choice against its expected answer rather than deciding it (§7). Option (b), a descriptor on the agents' job, is under *Rejected*: it reaches D201's judge and needs its own read (§10). 1c proves a client taken untold where the job will not open | `ProcessJob.cs:83-87` (no descriptor), `:121-137` (`NamedHolds`, null where it will not open); `docs/2026-10-23-terminal-trust-design.md:253-255`; `StandingCaller.cs:121` |
| **P2** | **The conversation rule drops the parks of the driver's own chats** | A conversation the person holds is `!ActiveSessions.HoldsSlot(session)`, judged on the `SessionView` the watch last saw; its park and ending are not told, and the driver's own intakes, helpers and reviewers are (§1.3, §4.2). 1b's census sees a helper's and a reviewer's parks told and a help room's and an unmarked `chat`'s not; 1b's list and proof 2 say so (§4.3, §9); TASKS 1b's row and D173's note follow | `Planner.cs:854-855` (`chat` is the driver's own kind too), `:872-885` (`HoldsSlot`); `DriverIntakes.cs:414-416` |
| P3 | The re-arm's permission prompt is not measured | §6.1 names the owner's allow rule, two prefixes in the owner's own settings, the second spanning the build's folder; M5 records whether `--where` and the re-arm run unprompted, before and after a build's change; §7's rule says what follows a prompt (§6.1, §7, row 1a) | design round 4 §6.1, §7 |
| P4 | The second-client rule needs a second listening instance | The relay keeps one more instance listening while it lives, and *connected* means a client whose first line has passed (§6.1 step 2); 1c proves the second client reaching it | `InstancePipe.cs:759-762` (the instance count) |
| P5 | The steward's half is unspecified, and the bound becomes a wake loop | §8.1, the steward's procedure for each line: re-arm with the closing bracket; `--where` only after the update line; no re-arm after an update line a new copy meets again; after two no-events lines in a row, `plugins show event-relay` and the owner told, and no more re-arms until the owner acts. The no-events line carries a closed reason (`no-client`, `refused`, `not-contained`, §6.1 step 6). The knowledge document the steward would also read waits for 1c | `WorkBounds.cs:24-25`; `Launcher/Program.cs:72`, `:86` |
| P6 | A busy lease read stops the plugin, and §4.4 could mark it unsound | §1.7b says what a busy read does today; §4.1: the reconcile reads the lease by a read that tells busy apart, and a busy answer keeps the last look's; §4.4: only the late stop and the plugin's own exits are counted. 1b's list and proof 10 (§9) | `HomeLease.cs:195-202`, `:360-377`; `Hooks.cs:705-709`, `:725-749` |
| P7 | `--where`'s own output is unbound | Every line of `--where` is a `RelaySaid` sentence naming no entry, but its one line with the copy's path and the home (§6.1 step 0); 1c reads them | design round 4 §6.1 step 0 |
| P8 | A half-removed build is reused as whole | A build is removed by renaming it first, after its `in-use` file was opened alone and closed, so a stopped removal leaves a removal name, never the build's; a build folder without its `in-use` file is not whole and is laid out again (§6.1 step 0); 1c proves both | design round 4 §6.1 step 0 |
| P9 | 1c's Process-lane class puts the plugin in the wrong job | The built host's own reconcile starts the plugin, installed and switched on in the laid-out home with the stand-in service `drive --once` needs, and the run is kept alive until the relay prints (§9 row 1c) | `AgentStarts.cs:33` (`AgentJobs.Current`) |
| P10 | Smaller corrections | `NamedHolds` is the query-alone open (§1.7b, §6.1 step 3); the lists of moved files name `StandingConfirmation.cs`, `InstancePipe.cs` and `PipeCensus.cs` (§1's header, §15); the two `TASKS.md` citations name their rows rather than their moving lines (§2.4, §4.2); 1b's web lane names the gloss's words in the en and zh `plugin.json` and the glossary (§9); M9's and M10's probes are .NET or PowerShell (§7) | `ProcessJob.cs:121-137`; `HookPoints.cs:15-23` |

## 17. Codex's read of round 4: its named changes, and where each went

Every finding of Codex's read of round 4 (`docs/2026-10-24-event-hook-reads.md`, *Codex, round 4*), each checked against the code on
main at `9f48860ba`, merged into this branch. Codex read `eced2688e`; between it and `9f48860ba` nothing under `src/Daoris.Desktop`,
`src/Daoris.Service`, `tools` or `examples/plugins` changed, so every line it cites stands where it cites it, and this design's text
was `eced2688e`'s until this section's changes, so its `Design:N` lines are that text's. Its `Driver.cs:888` and `:924` are of the file
as it moved after `deb766f20` (§1's header). C4-*s.n* is its finding *n* of its section *s*. Its §1 names no wrong claim about the
code, and its §0 holds every answer of §15, K3.3's startup gap becoming C4-3.1. Both named changes of its verdict are taken as named.
No finding is disputed: each held where it was checked. Blocking in bold.

| # | Finding | Checked against | Where it went |
|---|---|---|---|
| C4-2.1 | The principal boundary holds as a design contract | `Hooks.cs:145-149` (the handshake's `home` and `data`); `SupervisorProof.cs:97-106` (`Runs` names no `events`); `Launcher/Program.cs:72` (`StagedBuild.RunningFrom(app)`) | Agreed; nothing to change. |
| **C4-2.2** | **`--where`'s fixed-output rule misses the host's earlier lease printers** | `Host/Program.cs:399-416` (the lease, printing `leaseRefused` at `:407` and `error.Message` at `:413`), `:420-445` (the early routes after it), `:390` (the adoption); `HomeLease.cs:36-46` (`Named`, with `What`), `:176-178` (the exception's path and inner message), `:210-212` (`WaitRefusal`, with the home), `:417` (`What` read unchecked); `DaorisHome.cs:105-109`; `InstanceDoor.cs:405`, `:418`; `SupervisorProof.cs:109`, `:115-120` | **Taken as named**: both forms are routed between the role-terminal gate and the home's adoption; the verb's console adopts the home and takes the lease by the host's own calls, so the lease's behaviour is the generic path's, and says every failure in a `RelaySaid` sentence, exit 1 for the held home and 2 for a tool error; the two fixed host sentences before the route are in `RelaySaid`'s census (§6.1 steps 0 and 1); §8.1 says what the steward does with the held home's line; §10 rejects a catch around the verb's code alone; 1c's list and its proof of the entry's output, a lease's `what` written as a sentence and as a path, echoed on neither stream (§9) |
| C4-2.3 | The output bound needs coalescing rules | design §6.1 steps 2 to 5 at `eced2688e` (a gap line per gapped frame; refusals, drops and repeats said one by one) | **Taken**: a batch holds at most one gap line; every diagnostic is counted by its kind and said once, with its count, as the relay ends, so stdout holds at most 53 lines and stderr one line per diagnostic kind (§6.1 steps 1 and 5); §8.1's first row names the tally lines; 1c proves many gapped and repeated frames together (§9) |
| **C4-3.1** | **The first hello can reconcile before the source's baseline exists** | `Driver.cs:886-889` (the plugins reconciled and started), `:924` (the snapshot after them); `Attention.cs:221-224` (the first look's observation recorded and nothing said); `DriverLoop.cs:945`, `Host/Program.cs:1003` (the two callers of `Observe`) | **Taken as named**: after every `Observe` that was a baseline, `AttentionWatch` saying so, each loop tells one `events.missed` with its own `seq`, an empty baseline included, so a park the baseline takes in is said as a gap with no later event (§4.2, §4.3, §4.4, §6.1 step 5) |
| C4-3.2 | Later restart and relay-end recovery is coherent | design §6.1 steps 5 and 6, §4.4 | Agreed; nothing to change. |
| C4-3.3 | The unconditional loss promise exceeds M5's fallback | design §6.1 step 6 and §7's rule at `eced2688e` | **Taken, by qualifying**: the promise names its one measured limit, a relay killed before it prints, said at the kill only where M5 finds a kill's end starts a turn (§6.1 step 6, *What Windows does not draw*). Its second way, the kill's wake made a gate of the first build, is rejected with its reason (§10) |
| C4-4.1 | D119 keeps the superseded grant explanation | `D119.md:255-258` (round 3's paragraph: the switch the scope's grant); `D173.md:5460` (round 4's point 3); design §4.3 | **Taken**: a dated note of 2026-10-25 appended to D119's EVENTHOOK1 note, its round-3 paragraph kept as written |
| C4-5.1 | 1a is buildable first; its record must tell what blocks from an accepted limit | design §7, §9 row 1a | **Taken**: §7's record names each outcome a failure that blocks, with the rows it holds, or a limit accepted and named; row 1a's proof says so (§9) |
| **C4-5.2** | **1b lacks startup coverage** | as C4-3.1 | **Taken with C4-3.1**: row 1b's list and its proof 13, the subscriber reconciling between the reconcile and the snapshot, a park taken into the baseline, and the baseline's `events.missed` with no later look, event or disconnection, empty baseline included, in both loops (§9) |
| **C4-5.3** | **1c lacks the complete entry-output proof** | `Host/Program.cs:407`, `:413` | **Taken with C4-2.2**: row 1c's list and proof (§9). The 1a → 1b → 1c order holds; the steward's poll stops with 1h's installed proof, as §9 says |

**Against FOUNDATION1** (`docs/2026-10-25-foundation-design.md`, D212; Part A accepted and building its folder moves, read at its §10 and
§14). Part A moves the driver's files into twenty-one subdomains' folders, bottom up, one subdomain a row, each move landing when no open
branch holds its files (§14.6), and its structure gate, row mod-0, places every driver file and refuses a new upward edge (§14.5). Row 1a
writes no driver file. **Row 1b** writes in four moved places: `Plugins/` (the point, the hook set, `HookFrames.Event`, `PluginEvents`,
and the new `WorkEventGrammar` and `RelayBuild`), the Plugins subdomain's own folder at mod-xvii, where `work/event`'s outside
projection lands (§14.7); `Quests/Domain/Attention.cs`, whose `AttentionKind` and `AttentionEvent` split out into Events at mod-vii and
whose policy, `Observe` and its baseline flag, goes with `Quests/` to Work orders at mod-xv, so `AttentionEvent`'s new fields land in
Events' file and `Observe`'s change in Work orders' once those moves are in; `Trees/Landing/AutoLander.cs`, Workplace's at mod-x, which
tells `work.landed` through a delegate the loop hands it where it is made (`Loop/Driver.cs:1431-1434`), as it is handed `log` and
`PassAsked`, so Workplace names no type of Plugins, a subdomain above it, and mod-0 meets no new upward edge (§14.3 names the landing's
call into `work/land` as one to cut); and `Platform/HomeLease.cs`, whose busy-telling read 1b exposes, Platform's at mod-i and compiled
into the launcher, a linked consumer its move carries (§14.6). Its conversation rule reads `ActiveSessions.HoldsSlot`, which row
FOUNDATION1c makes read the participant's work, its census holding the two equal, so the rule answers the same whichever lands first.
**Row 1c** writes `Platform/VerbLeases.cs` (Instance's at mod-vi), `HolderKey.Drop` in `Sessions/Questions/SupervisorProof.cs` (Roles' at
mod-xi), `InstancePipe.Security` moved into the driver beside `InstanceClient` (so in Instance's folder once mod-vi is in), and the
relay's own new files, its console, `RelaySaid` and the copy's custody, which are Plugins' (§14.7), naming only Plugins' types and those
of subdomains below it. Row 1g's `Platform/GitWiring.cs` is among the Platform git files §14.2 gives Workplace (mod-x). The host's
`Program.cs`, the modules, the web and `examples/plugins/` are outside the map. So the builder writes each file in its new home, placed
in `structure.json`, where its move has landed, and where it has not, writes it where it is and leaves it to its move, which waits for
that row's branch to land (§14.6).
