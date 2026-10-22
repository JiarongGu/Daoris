# No time limit on a working session; a watcher supports one that is stuck (SESSWATCH1, 2026-10-23)

**Status: proposed (D200), round 3.** Written by Claude Opus at `360b27bc6`; revised after Codex's read (D191), whose fourteen findings
were each checked against the code and taken, and after Fable's foundation read, whose twelve findings (four blocking) were checked and
taken (§10). The steward accepts after reading this round. Nothing is built.

This design makes three parts of D193's company work at the desk: **staff** (part 4: a seat is held by work under way, and a worker is
never sent home by the clock), **operations** (part 15: no failure loses the work, and every stop says how to go on) and
**management** (part 18: a manager's cycle looks in on the team and helps the one who is stuck). Part 9's rhythm keeps its rule: no
message stops a turn, and only a stop does; the watcher's end of a quiet run takes the place the timeout held among Daoris's stops.

## 0. The case, and what is true today

### 0.1 The direction, in substance

On 2026-10-22 the owner set the direction: a working session never has a time limit. A watcher supports a session that gets stuck,
designed properly and light on resources. It is read as a cycle of support in the company picture: a manager checking in on the team,
not a kill switch.

### 0.2 What happened

On 2026-10-22 three driven sessions doing real work (a work-workspace session, POOL1b and TREELOOK1) were killed mid-work by the wall
clock (90 minutes on the install) and carried on, paying to read their context again. One was killed about 40 seconds after its last
tool call. One was killed while a long test run it had started in the background still ran. These facts are the steward's account of
the install; the install's own records were not read for this design.

### 0.3 The wall clock

- **The kill.** `Driver.WaitAsync` (`Loop/Driver.cs:3399-3418`) cancels after `minutes ?? config.TimeoutMinutes` (:3401-3403) and kills
  the process tree (:3413), answering a null exit for a timeout. Three sites call it:
  - `HoldAsync` (:3243-3245), which every door and every kind of run goes through;
  - the native door's go-on with held words (`DriverContinuations.cs:999`), a fresh bound for each run;
  - SESSCTX1c's one retry of a native resume (`DriverContinuations.cs:1035`).
- **The bound is a run's, not a turn's.** It starts after the spawn. On the protocol door the whole conversation runs inside that one wait
  (`Agents/Acp/Acp.cs:617-671`), so the person's words never reset it. A running session keeps the timeout it started under, since each
  look builds a new `Driver` from that look's configuration (`Loop/DriverWatch.cs:593-594`).
- **It also unblocks a stuck capture.** After the wait, `HoldAsync` awaits the capture before it lets the job go (`Driver.cs:3246`), so a
  descendant holding the harness's pipe after its root exited holds the run until the timeout kills the tree.
- **Which runs it bounds, and with what:**

  | Run | Site | Bound | What a timeout concludes |
  |---|---|---|---|
  | A driven quest session: a first start, a D80 carry-on, a reopen's go-on | `Driver.cs:2334` | `TimeoutMinutes` | `Observation.AfterTimeout` (`Driver.cs:2390`) |
  | An answered park resumed (D131), words to an ended record (D137 §2.3) | `DriverContinuations.cs:318` | `TimeoutMinutes` | `AfterTimeout` (:537) |
  | An intake | `DriverIntakes.cs:239` | `TimeoutMinutes` | `failed`, `TimedOut` (:266) |
  | A helper's pass | `DriverHelpers.cs:575`, bound :534 | min(15, `TimeoutMinutes`) (`HelperPass.cs:47`) | `TimedOut(bound)` (:598) |
  | A reviewer's pass | `DriverOpinions.cs:194`, bound :137 | min(its rule's minutes, 20 by default, `TimeoutMinutes`) (`OpinionPass.cs:103`) | `TimedOut(bound)` (:217) |
  | A partner's pass | `DriverPartners.cs:495`, bound :452 | min(45, `TimeoutMinutes`) (`PartnerInstruction.cs:76`) | `TimedOut(bound)` (:516) |
  | A brainstorm's or meeting's turn | `DriverBrainstorms.cs:692-708` | its own minutes, never `TimeoutMinutes` | `timed-out` |
  | A person's conversation, Ask Daoris's included | `ChatRunner.cs:2030`, `:2253` | none | none |

- **What it writes.** `failed` with `ended.timeout` {minutes} (`Sessions/Domain/Observation.cs:302-303`, `Platform/NoteCodes.cs:444`;
  en and zh `note.json:26`, "Timed out after {{minutes}} minutes and was killed."), or `completed` where the run brought its quest to done
  (`Observation.cs:320-323`). On the protocol door the door is told first (BGWAIT2: `Driver.cs:3244`, `Acp.cs:446-460`), so the stream's end
  is said as the timeout's. The native door says nothing before the kill. A stop made through the processes' registry concludes otherwise: a
  stop with Daoris's reason is `stood-down`, the person's `stopped` (`Observation.cs:9-17`), and an intake reads any stop asked for as
  `stopped` (`DriverIntakes.cs:261-266`).
- **Where it is set and said.** `DriverConfig.cs:35`, default 30 (:60), read with a floor of 1 and no ceiling (:1422). A configuration built
  in memory writes it (:980); a loaded file's save keeps every value no edit changed as it stands (`JsonKeeping.Kept`, :910). `daoris driver
  timeout <minutes>` (`Platform/DriverChoicesCommand.cs:497-513`) and `driver list` (:942-944). Ask Daoris's `setting` door `timeout`
  (`Help/Proposals/HelpSettingProposals.cs:37`, :213-214; the service's `HelpSettingProposalWriter.cs:25`, :92-95), its granted tool
  `settings_timeout` (`Daoris.Service.Mcp/ChosenSettingsTools.cs:46-51`), the help room's lines (`Help/Room/HelpRoomMayDo.cs:318`,
  `Help/Room/HelpRoomDoors.cs:170` and its goldens), the grant's label `help.grants.op.settings.timeout` (en and zh `help.json:275`,
  `help/access/grants.ts:52`), and `settings.checks.why`, which says a light session ends "not at its timeout". No page has a field for it.
  The CLI keeps only its usage line (`cli/driver.ts:156`). The install set it to 90.
- **Reasons that lean on it.** A held permission request is refused after 15 minutes, "half the session's default 30-minute run"
  (`Sessions/Events/HeldPermissions.cs:93-96`). BGWAIT1 says work that never ends, a dev server, "holds the session until the timeout fails
  it" (D83), and `AfterBackgroundAsync` is "bounded by the session's own bounds: the person's stop and the timeout" (`Acp.cs:729-731`). D188
  point 1 and D193 part 9 list "Daoris's own timeouts" among the stops.
- **A defect it carries.** A reviewer is told its rule's minutes (`DriverOpinions.cs:154`, `OpinionInstruction.cs:242`) and killed at the
  smaller of those and `TimeoutMinutes`, so under a timeout below 20 it is told more time than it gets.
- **Tests that lean on it.** About fifty sites in about thirty-four test files set `TimeoutMinutes` to 1 to 3 (for example
  `DoneEndTickTests.cs:92`, `HelperProcessTests.cs:68`): a backstop so a hung stand-in fails in minutes. None waits a timeout out; the
  timeout's path is held in pieces (`ObservationTests.cs:46-57`, `AcpBackgroundTests.cs:848-957`).

### 0.4 What a running session already says

- **Its events.** One JSON line per event in `<home>/data/sessions/<id>.events.jsonl`, stamped `at` as it is appended, one at a time, never
  batched (`Sessions/Events/SessionEventLog.cs:39-50`), after which `Evented` is raised in-process (:36, :68). The kinds are `user`,
  `message`, `thought`, `tool`, `plan`, `usage`, `turn`, `note`, `raw` and `receipt` (`Sessions/Domain/SessionEvents.cs:13-48`). No
  in-memory "last event at" exists anywhere.
- **Not everything the harness says becomes an event.** On the pipe door, each stdout line is read in `HarnessCapture`'s loop
  (`Agents/Streams/HarnessCapture.cs:148`), where `HarnessWords.Heard` sees it where a `HarnessWords` was handed (:153), but a subagent's
  lines are taken to its own console before the mapper (:154, `Agents/Streams/ClaudeStreams.cs:73-81`), the mapper keeps only text and thinking deltas of the partial
  stream (`Agents/Streams/StructuredOutput.cs:176-198`), and stderr is pumped apart (`HarnessCapture.cs:144`). On the protocol door a
  subagent's frames go to its console (`Agents/Acp/AcpStreams.cs:95-102`, `Acp.cs:1712-1725`). A text-only door writes no events at all
  (`HarnessCapture.cs:54-77`), only its transcript.
- **Per door, while a turn runs:**
  - **claude-code (native)** runs with `--include-partial-messages` (`Agents/Adapters/Adapters.cs:2085-2108`), so the model's output streams
    as lines. A tool call is one `tool` event at its start and nothing until its result (`StructuredOutput.cs:229-242`, :258-288). Its
    background commands are ended by the harness at the turn's end (`docs/2026-09-30-console3-native-streams-evidence.md` :46-52), which
    BGWAIT1c says as `ended.background-cut` and carries on. Words reach it only at its run's end (`Driver.cs:3176-3194`).
  - **claude-code-acp** streams message and thought chunks. A tool call's input and output arrive in a burst at its start and nothing until it
    completes (`docs/2026-09-28-console2-streams-evidence.md` :111-120; the task archive's lines 6790-6796). Background work is announced and
    ended by `async_task_*` frames, kept in memory by `AcpBackground` (`Agents/Acp/AcpBackground.cs:94-109`, `Acp.cs:425`) and never as events
    (`Acp.cs:2335-2338`). Words reach it at its next step (`Acp.cs:951-972`, D136).
  - **codex-acp** is a turn-end door (`Acp.cs:962-965`); its background work is unknown (console2 evidence :168) and its tool frames unmeasured.
  - **dsh** sends committed facts only: a `tool_call` in progress, then completed, and `usage_update` (`docs/2026-09-21-dsh-evaluation.md`
    :101-102, :115-118), so its model's step is silent.
- **In memory, per running session.** Its process, joined to a kill-on-close job as it is tracked (`Sessions/SessionProcesses.cs:372-378`,
  ORPHAN1), best effort: joined after an ordinary start, refused where the system makes no job, empty off Windows (`Agents/Consoles/ProcessJob.cs`
  :35-66), and held only by the disposer `Track` returns, never by the registry's entry (:43, :577). The job's basic accounting (read for
  `ProcessJob.Active`, :98-108, :224-231) also carries the CPU time of every process in it. Its open calls with their `Since`, and `Waiting`
  where a call is held for the person (`Sessions/Events/RunningCalls.cs:14-26`, `Calling` :124). Its background work on the protocol door. Its
  held permission requests (`HeldPermissions.cs`).
- **`LongCalls`, the closest exemplar** (`Sessions/Heavy/LongCalls.cs:32-216`). Under light checks, a call or a background task past 20
  minutes is told to its session once, urgent, as Daoris's word, only where the door takes words at its next step, and never ended. It runs a
  30-second timer per session (:63, :134-148), wired at `Driver.cs:3613-3621`.

### 0.5 The loop and what a look costs

- `DriverWatch.RunAsync` (`DriverWatch.cs:334-817`) makes a look `PollSeconds` (15 by default, a floor of 1 and no ceiling,
  `DriverConfig.cs:1423`) after the last whole look, or sooner on an ending, a nudge or a ledger change: every move of a session record wakes
  a plan look (`Service/LedgerChanges.cs:10`, :70). Each look builds a `Driver` sharing the running sessions, the processes and the hooks
  (:593-609) and runs `TickAsync` (`Driver.cs:631-650`), whose stages `LookStages` times; `watch.looked` writes the look's milliseconds to
  the machine log (`DriverWatch.cs:921-937`).
- **The headless host.** `--until-idle` repeats looks (`Driver.cs:1465-1486`); `--once` makes one look and then waits for what it started
  (`RunOnceAsync`, :1503-1525), its passes beside the sessions running only while a remote syncs (`SettleAsync`, :1554-1614, :1578), and
  never plans again.
- A look reads four snapshot GETs (`Service/ServiceClient.cs:135-148`), one claim per running quest session where a remote syncs
  (`Driver.cs:1404-1437`), and one channel read per running part on an ask (`Loop/ChannelReach.cs:76-108`). The desktop's report adds three
  GETs (`Modules/Loop/DriverLoop.cs:898-919`). The planner is pure. No look reads git or a file per running session.
- Costs paid per session outside the look: the timeout's cancel timer; DONEEND1's quest read every minute for each session waiting on its own
  background work, each a whole quest list (`Driver.cs:3754`, `Acp.cs:852-868`); `LongCalls`' timer; the output tail every 500 ms for each
  background task whose console is kept (`Agents/OutputTail.cs:23`).
- Liveness is not polled: each run awaits its own process (`Driver.cs:3406`), and its ending wakes the watch (`Sessions/RunningSessions.cs:621-649`).

### 0.6 The carry-on, the strikes, words and the other stops

- A timed-out record ends `failed`. The look its ending wakes (`DriverWatch.cs:780-788`) carries its taken quest on (`Quests/Domain/Planner.cs`
  :1329-1343, `CarriesOn` :1910-1919, `CarryOn` :1504-1538) in a new record and a new conversation in the same tree, handed Daoris's brief
  (`Driver.cs:2067-2084`, :2164-2168, :2210-2290; `started.cut-off` :3022), its instruction saying what cut the last session off
  (`Adapters.cs:532-563`, :632-646). An ended record holds no slot, so the carry-on takes one like any start (`Planner.cs:1451-1453`).
- **Words on an ended record.** Any word waiting on it, Daoris's included, makes the look go on in that record (`WordsOn`,
  `Planner.cs:1442-1449`; `GoOn` :1454-1468), resuming its own conversation and moving it back to `working` (`DriverContinuations.cs:109-160`,
  :156-158); a word of Daoris's alone opens nothing where that resume falls back (`Driver.cs:2007-2015`). A driven inbox that closes on an
  end that is not the person's keeps its words (`Conversations/DrivenInbox.cs:838-888`); the come-up settles only words a shell attempted
  (`Conversations/GoOn/UncertainWords.cs:61-65`); Daoris's word keys are read for two kinds, a reopen and a gone-on
  (`Conversations/Domain/DaorisWords.cs:137-151`).
- **The strikes** count `failed` records with a quest, a limit's and a refused account's aside (`ServiceClient.cs:2013-2032`); at the strike
  limit, 3 by default and none at 0, the quest parks (`Exhausted`, `Planner.cs:1776-1791`), a `parked-quest` row in *What needs you* with *Try
  again* (`Modules/Loop/NeedsAPerson.cs:259-266`).
- **An intake** is one turn with no inbox (`Driver.cs:3171-3176`), takes no words (`DriverIntakes.cs:45-49`), and an ask an intake served is
  never served by another (`Daoris.Service.Core/Sessions/SessionLedger.cs:319-333`): a failed intake leaves its ask to the person.
- **A hand-over** the person presses stops a live quest session at once, as the person's stop, and goes on with the named agent
  (`Loop/DriverHandOvers.cs:77-136`); an agent's own ask, once allowed, is taken at its turn's end (:210-292), read only after a clean exit
  (`Driver.cs:2398-2402`), concluding `stopped` with `ended.handed-over`. A pause stops at once as the person's stop
  (`Sessions/Work/WorkPausing.cs:151-212`).
- **The cap** counts active driven records (`Planner.cs:823-836`, `SlotsHeld` :870-877): a working session, one waiting on its own background
  work and a park each hold a slot; an ended record and a person's conversation hold none (CAPCOUNT1).

## 1. What "stuck" means, per door and per state

### 1.1 The rule

A running session is **working** while it shows a sign of work, **waiting** while a wait its record names holds it, and **quiet** once
neither has been true for its state's bound. Quiet is not yet stuck: it is the watcher's reason to look closer and offer help. Silence is
never read as success (BGWAIT2) nor as failure.

The signs can show work; they can never prove its absence. A session with no line from its harness and no CPU in its job may still be
waiting on remote work: a model's first token, a tool that calls a server, a subagent's own model. So **quiet licenses a word and the
person's attention, and an end only where the record shows such quiet is not work** (§2.2).

### 1.2 Signs of work, read only from what Daoris already holds

- **Heard.** The time the door last read anything from the harness: on the pipe door each stdout line at the top of the capture's loop
  (`HarnessCapture.cs:148`), before a subagent's line is taken aside (:154) or a partial delta is dropped, and each stderr line in its pump
  (:144); on the protocol door each frame its reader takes, a subagent's included; on a text-only door each line its capture reads
  (:54-77). The stamp is a parameter of its own on each capture, never `HarnessWords`, which is handed only where a run ends on what its
  harness said (:153). Only the time is kept, in memory, never the line; nothing reads a file, and a subagent's content never enters its
  parent's conversation.
- **Busy.** The session's job used CPU above the idle floor (§1.5) over the last window: one accounting read of the job, counting the
  harness and every command it started, background ones included. The accounting is cumulative over every process that ever joined the job
  (`ProcessJob.cs:222-231`), so busy is the CPU between the newest read and the read before it, over the time between them, a window of at
  least a minute; the first read, once a session has gone unheard for half of its bound, is only the window's start. Past work never
  counts. The registry's entry holds the job, and says whether its accounting can be trusted: the root joined, on Windows. Where it cannot
  (the join failed, off Windows), busy is **unknown**, which never authorizes an end.
- **Moving work.** For a session waiting on its own background work: that work's output grew, where its console keeps the tail in memory,
  or the job is busy.
- **Alive.** Its root process has not exited: the run's own wait on its exit (`Driver.cs:3406`) stamps the time it returns, so nothing
  polls a process. On the pipe door the root's exit is the turn's end, its exit code already held (`Driver.cs:3243-3245`); what can still
  hang is the capture a stray descendant holds open (:3246), which state E bounds.

What "heard" cannot tell: a harness that repeats a meter, an unchanged tool update or a raw frame is heard though nothing moves (§2.4).

### 1.3 Waits the record names

A session is never quiet while one of these holds:
- a call held for the person's answer to its permission request (`RunningCall.Waiting`), which keeps its own 15-minute bound (§9);
- its call waiting for the heavy-run slot behind another session's run: the waiter's record beside the slot names it
  (`Sessions/Heavy/HeavySlot.cs:36-45`, `HeavyLook` :214), read only once the session has gone unheard for half its bound. Such a wait past 3Q
  brings the row of §2.3 all the same, since the holder may be a run from a terminal with no session to help.

A session holding the heavy-run slot is not waiting but busy: its gates run in a job of their own, named on the holder's record
(`HeavyHolder.Job`, `HeavySlot.cs:19-20`), whose accounting the watcher reads as it reads the session's.

Two things a quiet session may carry change its steps rather than excuse it, since either could otherwise hold a hung session forever:
- **The person's words on their way to it.** Nothing ends a run while one is (D136 point 4), so where one waits, the row of §2.3 comes in
  place of any end: the person, whose words they are, decides.
- **A hand-over approved and waiting for its turn's end** (`DriverHandOvers.cs:210-292`). A hung turn never ends, so an end the watcher makes
  is taken as that turn's end and the hand-over goes on (§2.2).

A go-ahead (`go_ahead_ask`) and a helper (`helper_ask`) are asked by tools that answer at once and tell the agent to end its turn
(`Daoris.Service.Mcp/GoAheadTools.cs:12-19`, `HelperTools.cs:18-27`), and a question (`question_ask`) parks it; a park (`awaiting-person`),
a pause and a quest waiting on another repository (D79) have no process, so nothing runs to be quiet.

### 1.4 The states, per door

| State | What says it | native claude-code | claude-code-acp | codex-acp | dsh | text-only |
|---|---|---|---|---|---|---|
| **A.** A turn runs, no call open (the model's step) | heard | lines stream | chunks stream | unmeasured | silent until a fact | transcript lines |
| **B.** A turn runs, a call open (a command, a tool, a subagent) | heard (a subagent's lines), busy | silent until the result, but for a subagent | a burst, then silent, but for a subagent | unmeasured | silent until the result | transcript lines |
| **C.** No turn runs; it waits on its own background work (BGWAIT1-3) | moving work | never: the harness ends the work with the turn | yes | none known | none known | none |
| **D.** A woken turn whose end is never said (BGWAIT2's remainder) | heard | never | yes | none known | none known | none |
| **E.** Its root exited, its capture still held open | alive | a stray holding stdout | a stray holding stdout | a stray holding stdout | a stray holding stdout | a stray holding stdout |

A call in state B that waits on input, a lock or the network is idle; so is one that waits on a server. A test run or a build keeps its job
busy. Claude Code's own Bash tool ends a foreground command at its timeout, two minutes by default and ten at most, as its tool's
description says; Codex's bound on a command was not read.

### 1.5 The bounds (first values; judgements SESSWATCH1d measures)

- **Quiet, Q: 10 minutes** unheard, in states A, B and D, with the job not busy (or busy unknown). The person sets it (§3), from 5 to 120.
  Ten minutes is longer than the longest foreground command Claude Code allows; the longest quiet of a working turn on the install is
  unmeasured (SESSWATCH1d).
- **The idle floor: 0.2 seconds of CPU a minute** (1 second in 5, a third of a percent of one core), over the window between the two newest
  reads, a minute at least. Measured on node stand-ins (§4.2): the shapes of a stall used at most 31 ms a minute, one parsing a stream of
  small chunks 500 ms a minute, and a test run's shape 52 s a minute. The real harnesses' floor is SESSWATCH1d's to measure.
- **Unheard, whatever the CPU: 3Q** (30 minutes). A session busy but unheard that long is brought to the person (§2.3), since a stray loop
  or a spinning child is busy and stuck at once, and on the Claude doors a foreground call is ended by the harness within ten minutes. It
  gets no word and no end.
- **State C: 3Q** (30 minutes) with no moving work before the word, and 2Q more before an end where ends are enabled: 5Q, 50 minutes, in all.
- **The capture, G: 60 seconds** from the root's exit, or its stream's end, to the capture's end in state E: the door's drain and
  `EndedLingeringAsync`'s ten seconds fit inside it.
- **The clock stops while the machine sleeps.** Quiet is counted on the machine's unbiased interrupt time (Windows'
  `QueryUnbiasedInterruptTime`, behind the handed `TimeProvider`), which does not advance in sleep or hibernation, so a wake reads no session as
  quiet, and a slow look or a long `PollSeconds` loses nothing: the count is time, not looks. Elsewhere, a monotonic clock, and a gap between
  looks longer than three paces and at least two minutes is not counted.

## 2. The support steps, each bounded and said

**Where it lives.** One watcher per shell, held beside `RunningCalls` (`Modules/Loop/DriverLoop.cs:100`): for each running driven session, its
heard time (stamped by the door's reader), its job and whether its accounting holds, its episode and its step. A stage of every look judges it
(`watch`, timed by `LookStages` with the others), so it shares the look's pace and its log line. The attention slice reads its rows from memory, as it reads the
hand-over's.

**The headless host.** `--until-idle` judges it at each of its looks; `--once` judges it while it waits for its sessions, its wait woken at
the pace whether or not a remote syncs. Neither has a bound of its own once `WaitAsync`'s cancel is gone (`RunOnceAsync` awaits
`SettleAsync`, `Driver.cs:1503-1525`, :1575-1604; `RunUntilIdleAsync` returns only when idle, :1465-1486), and it has no *What needs you*:
it is the person's terminal, so the row is its console line (each step already writes one), and the operator's Ctrl-C is the stop, the
driver's shutdown, whose interrupted record D104 carries on. A run nobody watches can be given its end: `--once` and `--until-idle` take
`--quiet-end <minutes>`, which enables step 2's end for that run alone: a session of it unheard that long, busy or not, is ended and
carried on as step 2 says, for a rehearsal or a scheduled run that must return. Without it the host waits, saying so on its console.

The steps run per **quiet episode**: from the look a session is first quiet to the look it is heard again, shows moving work or busy CPU,
begins a wait of §1.3, ends, or reaches the person. Each step writes a coded note on the record, a console line and a machine-log line
(`session.quiet` {session, adapter, kind, state, minutes, step, outcome}), and the episode's end writes how it ended: heard again, a press,
or an end. That record is what SESSWATCH1d reads.

| Step | When | What Daoris does | For |
|---|---|---|---|
| **1. A word** | at Q quiet (3Q in state C) | marks the session quiet on its head and rail, writes `watch.quiet`, and keeps one word of its own for the agent, urgent | quest sessions |
| **2. An end and a carry-on** | 2Q after step 1, quiet and never busy since, busy known | ends the run, says why (`ended.quiet`), and the next look carries the line on in the same tree | quest sessions, **only in the cells enabled** (§2.2), or under `--quiet-end` |
| **3. The person** | at step 2's time where step 2 is not enabled or not allowed; at 3Q unheard, busy or not; or when a carried-on part is quiet before it was ever heard, or cannot start | a row in *What needs you*, whose presses include, where the session is quiet, the end and carry-on | every driven session and intake |

**State E is not a step but a bound on the capture.** A run whose root has exited is concluded by what it said, within G: after G with its
capture still held open, the run's job is terminated, the capture abandoned as `AbandonedAsync` abandons one (`Driver.cs:3236`), and on the
pipe door the run concludes by its exit code and the result already captured (`Observation.Conclude`, `Driver.cs:2372-2390`: done is
completed, a clean exit holding the quest parks, a crash fails), the root's own writes being in the pipe and only its end withheld;
`ended.gone` is only a note line saying the strays were ended. On the protocol door the root's exit ends the stream, which `StreamEnded`
already reads (`Acp.cs:446-460`), so state E there is only a stdout a stray holds: the run concludes `failed` with the watcher's note unless
its prompt was answered. A strike only where the conclusion is `failed` by the exit itself. So a session that finished its turn and left a dev
server holding its pipe, ORPHAN1's own case (`ProcessJob.cs:10-13`), ends as it finished, never as a cut-off.

**Never a silent end, and no end that is not earned.** A run the watcher judged quiet is ended only at step 2, in a door and state whose
record shows that quiet held that long is not work, or by the person's press, or under a headless run's `--quiet-end`. Everywhere else the
person decides, with the same end one press away where the session is quiet.

### 2.1 Step 1: a word, under D188

The word is Daoris's, under a key of its own kind (`watch/<session>/<episode>`, read by `DaorisKeys` as a third kind beside a reopen and a
gone-on), kept on the record's `said` through the session's one dispatcher before anything holds it (`DrivenInbox.KeepOrHoldAsync`, the
session-words knowledge), urgent, as `LongCalls` keeps its word. An intake takes no words, so it gets the mark and no word. The word
reaches the agent as its door allows:

| Door and state | Where the word reaches |
|---|---|
| claude-code-acp, states A, B, D | at the next step (D136 point 1): when a hung call ends by the harness's own bound, the agent reads it before its next step |
| claude-code-acp, state C | at once, as a turn of its own: no turn runs, and the session goes on only by Daoris's prompt, as BGWAIT2's go-on does |
| native claude-code, codex-acp, dsh, a text-only door, in a turn | nowhere during the turn: it waits for the turn's end, and is withdrawn when the session is heard again or its run ends, so it never makes a turn for a fact no longer true |

**A word not yet on the wire never outlives its episode.** It is taken off `said` when its episode ends, and before any conclusion of its
run is written; it is never counted among the words that make a look go on in an ended record (`WordsOn`); and a `watch` word found on an
ended record (a crash, a withdrawal that failed) is settled at the come-up with the uncertain words, before the first look plans, and said.
So a word can never turn D80's carry-on into a resume of the failed record. A word already on the wire cannot be called back: on
claude-code-acp it is sent as a queued prompt at once (D188 point 6) and read at the harness's next step, which on a hung call comes after
the call ended, so its words say the call *ran*, true whenever they are read.

**It resets no count.** A `watch` word is excluded from `AnswersAlone.Resets` (`Conversations/Domain/AnswersAlone.cs:89-91`), which today
counts any taken word of Daoris's with no reach: the state-C word, a prompt of its own, would otherwise give a line going on by answers
alone two more answers after every quiet. D199 settled the same for a partner's notices.

The words, Daoris's one English: *"Daoris: nothing came from this session for {minutes} minutes{, while your call `{title}` ran | ,
while your background work `{names}` showed no progress}. If a command waits on input, a lock or the network, stop it and go another way;
run anything long in the background and read its output; if something only the person can give blocks you, ask with `question_ask`. Daoris
has stopped nothing."* `question_ask` turns a stuck session into a park with a question, the person's door for what only they can give
(D83, D124). Where `LongCalls` already told the same call, its word is the episode's word and no second is kept.

### 2.2 Step 2: an end and a carry-on (D80, D189), where enabled

- **Where it is enabled.** An end is automatic only in a cell (a door and a state) that SESSWATCH1d's record supports: over the install's
  quiet episodes in that cell, none that stayed quiet and idle for the step-2 bound was later heard again or pressed on as working, and a
  D200 note lists the cells. Until a cell is listed, step 3 comes at step 2's time. Busy unknown, the person's words on their way, and any
  door whose quiet states are unmeasured (codex-acp and dsh today) are never enabled. A headless run's `--quiet-end` enables it for that run
  alone (§2).
- **The end's own cause.** The watcher ends a run through the run's own wait, as the timeout did: a cancellation it holds per run takes the
  place of `CancelAfter` (`Driver.cs:3402-3403`), so the exit is null and no registry stop (`stood-down`, `stopped`) is read. Every conclusion
  path reads the cause, `EndedBy.Watcher` with its minutes and what it waited on: a quest session concludes `failed` with `ended.quiet`, or
  `completed` where its run brought the quest to done (`AfterTimeout`'s rule); an intake `failed` with `ended.quiet`, as a timed-out intake
  failed. The protocol door is told first (`AcpEnding`), so its stream's end is said as the watcher's.
- **The capture let go.** The end terminates the run's job, so a descendant holding the harness's pipe ends with it and the capture drains;
  the drain after an end is bounded by G, after which the capture is abandoned and said, as state E abandons one.
- **The carry-on is the timeout's.** The look the ending wakes carries a quest on by D80 in a new part in the same tree, handed Daoris's brief
  (D189), whose instruction says what cut the last part off from its note: `ended.quiet` names the minutes and the call that hung or the work
  that showed nothing (`Adapters.cs:632-646` reads the note). The brief carries the part's last tool calls and plan. The ended record holds no
  slot, so the carry-on starts behind the cap like any start (§6). A failed intake leaves its ask to the person, as today.
- **Counted.** The ended record stays `failed`, so Daoris's own end is a strike, as a timeout was: at the strike limit (3 by default; none
  at 0) the quest parks on the person (D80, `Planner.cs:1776-1791`), its sitting sentence naming the watcher's ends. The strikes bound what
  restarts with nobody between; the person's own press is not one (§2.3).
- **A hand-over waiting is taken.** Where a hand-over is approved and waits for the turn's end, the end is taken as that end: the conclusion
  reads the hand-over after the watcher's end as it does after a clean exit (`Driver.cs:2398`), concludes `stopped` with `ended.handed-over`
  and a note that the quiet ended the turn, counts no strike (the person or their rule chose the move), and the next part runs on the agent
  named. Daoris itself never chooses another agent: an automatic hand-over is rejected by default (D163 point 3). COAGENT1g's rule (a
  hand-over waits for the outgoing part's background work and children) holds for this one too: the end terminates the job, so the part's
  descendants are ended before the next part starts, never handed over beneath it.
- **A lead's pieces.** An end of a session leading open pieces (D199) leaves them as any end of the lead does: each piece's answer moves
  along `from` to the part that carries the line on (D199 point 8).
- **Not the resume, for now.** Resuming the quiet session's own conversation would keep the agent's exact memory, but a resumed ended record
  goes back to `working` (`DriverContinuations.cs:156-158`), so its strike would go and a line that hangs the same way could loop at a whole
  context each time; a word of Daoris's alone opens nothing where the resume falls back (`Driver.cs:2007-2015`); and the conversation may hold
  what made it hang. SESSWATCH1d measures what a resume and a brief each cost, and a note may bring the resume back with a count of its own.

### 2.3 Step 3: the person

A row in *What needs you* (§5.2), and Daoris ends nothing more: the session keeps running, its tree and its slot held, until the person
presses. Where it comes:
- at step 2's time wherever step 2 is not enabled, not allowed (busy unknown, the person's words on their way) or not a quest session's;
- when a part the watcher carried on is quiet before its agent was ever heard (Q from its start), or the carry-on cannot start;
- for a call waiting on the heavy-run slot past 3Q (§1.3);
- at 3Q unheard whatever its CPU: a session busy but silent that long is worded *busy, nothing heard*, and offered no end, since busy is a
  sign of work and the person judges it. Without this a spinning child or a loop in the harness would hold its seat and tree for ever, where
  the timeout once ended it.

Its presses, each where it applies:
- **End and carry on** (quest sessions, quiet only): the person's stop released at once, *Stop* and *Try again* in one press (D126 §3.4):
  the run ends as the person's stop ends one (read before any exit, `Driver.cs:2366-2371`), the record concludes `stopped` as the person's
  with `ended.quiet-pressed`, the stop is released in the same press, and the next look carries the line on in a new part handed the brief,
  as a released stop is carried on (`Planner.cs:1910-1919`). It counts no strike: a person's stop
  never does (D58), nor a hand-over they choose (D163), and the strikes bound what restarts with nobody between. The person's words waiting
  to reach the session are not lost: the press moves them off the ended record to the carry-on's start as the person's words, as a
  conversation's move hands its words to its new part (D189's SESSCTX1c part B note), and the row says so before the press.
- **Hand over…** (quest sessions): D163's hand-over to another agent, which stops the run and goes on with the agent chosen in the same tree.
  The page refuses a quest session's hand-over today (`Conversations/HandOver/HandOver.cs:52-60`, :145; `work.json:45`, `work/say.ts:176`),
  which `daoris-driver sessions hand-over` answers through `QuestHandOver` (`Conversations/HandOver/HandOverQuest.cs:67`); SESSWATCH1a
  builds the page's press through that judge, in every place of the bridge-presses knowledge, so the row's press is real from its first row.
- **Stop**: the person's stop, the quest held behind *Try again*, an intake's ask left to the person;
- **Leave it running**: the watcher stands down for this session until it is heard again (`watch.left`), and the row goes.

**What the cap means here.** A hung session keeps its slot and its tree until the person presses: with the cap at two, two hung sessions
stop all driven work until the person acts. That is why the row counts on the bell. The old timeout freed the seat at 90 minutes; this shape
frees it by a press, or, once SESSWATCH1e is built, by an end the record allows.

### 2.4 What the watcher does not watch, and what it cannot see

- **A person's conversation** (a chat, Ask Daoris) is the person's (CAPCOUNT1's reason): past Q its head says it is quiet, and nothing more.
  No word, no end and no row; its turn's stop is the person's *Stop turn*.
- **A pass with minutes of its own** (a helper's 15, a reviewer's rule's 20, a partner's 45, a brainstorm's and a meeting's turn) keeps
  them, as D163, D155, D199, D176 and D177 give them: each is an answer owed to an asker by a time, and none of the kills of 0.2 was one.
  They are no longer capped by `timeoutMinutes`, so a reviewer is told the minutes it gets. At the code's default of 30 a partner's pass
  grows from 30 to 45 minutes, a reviewer's grows from 30 to its rule's only where its rule sets more than 30 (its default of 20 stays),
  and a helper's stays at 15; at the install's 90 nothing moves unless a reviewer's rule sets more than 90. Whether each becomes the time its asker is told it is late, watched
  rather than ended, is SESSWATCH1f's question, asked of each decision.
- **What heard cannot tell.** A harness that repeats a meter, an unchanged tool update or a raw frame is heard though nothing moves, and busy
  work that goes in circles while saying something (the same failing command again and again) is working by every sign here; telling either
  needs a reading of the work, which no step makes. Such a line spends until its account's limit, the strikes or the person stop it; the
  watcher does not bound it. Busy work that says nothing is brought to the person at 3Q (§2.3).

## 3. What replaces `timeoutMinutes`

- **`quietMinutes`** in `driver.json`, 10 by default, from 5 to 120: how long a session may say nothing, with nothing it runs using the
  machine, before Daoris looks in. It is the one number the person sets; step 2's bound is 2Q after step 1 and state C's word comes at 3Q. It
  is written only when set, as `cooloff` is (`DriverConfig.cs:983-984`), so `driver list` tells the default from a choice.
- **Its doors**, each a place of the setting-doors knowledge: `daoris driver quiet <minutes>`; `driver list`'s line *quiet 10 minutes before
  Daoris looks in on a session*; Ask Daoris's `setting` door `quiet` and its granted tool `settings_quiet`, with the help room's rows and
  goldens; a field on Settings → Driver beside the cap, the strike limit and the cool-off (CAPSET1's card).
- **`timeoutMinutes` is kept, never read for a bound.** The field leaves the record. A loaded file's save already keeps every value no edit
  changed as it stands (`JsonKeeping.Kept`, `DriverConfig.cs:910`), so a file that holds the key keeps it through every save, and a file
  without it gains none; a configuration built in memory, which writes every field (:980), no longer writes it. The builds before
  SESSWATCH1a read the key and, after a rollback (D139), find the person's value again: the compatible-builds knowledge's rule, a key this
  build owns beside the one older builds read.
- **An install that set it** (the owner's 90) moves by doing nothing: its value stays for an older build, this build ends no run by it, and
  its quiet bound starts at the default. 90 is not mapped onto `quietMinutes`: it was a run's whole length, never a silence, and a person who
  raised it asked for longer runs, which a working session now has without limit. `driver list` says it once: *timeout 90 (no longer limits a
  session; kept for an older Daoris)*.
- **The old doors are retired, saying so** (SESSWATCH1a). `daoris driver timeout <minutes>` changes nothing and answers that sessions are no
  longer limited by time and that Daoris looks in on a quiet one (exit 1); from SESSWATCH1c it also names `daoris driver quiet`. Ask Daoris's
  `timeout` door and `settings_timeout` refuse with the same words, for a helper reading an older room. The help room, the grant's label and
  `settings.checks.why` lose the timeout.

## 4. The cost

### 4.1 Today's look, measured

The watch's look has never been timed on the install (`docs/2026-10-16-fabric-events-design.md` :99-100, :262-264); `watch.looked` writes its
milliseconds there, which the parent reads in SESSWATCH1d. For this design a look was timed in-process: `Driver.TickAsync` over the driver
tests' stand-in ledger (an in-process handler for the service, no port, no remote, no git), with N active driven sessions seeded and the cap
full, 40 looks after 5 to warm, on this machine (22 logical processors, .NET 10.0.13):

| Active driven sessions | 1 | 4 | 16 | 64 | 256 |
|---|---|---|---|---|---|
| A look, median | 3.1 ms | 4.6 ms | 7.3 ms | 14.8 ms | 33.5 ms |
| A look, 95th percentile | 5.0 ms | 7.2 ms | 10.1 ms | 23.5 ms | 43.3 ms |

So today's look already grows by about 0.12 ms a session in-process, before its HTTP: four snapshot GETs, one claim per running quest
session where a remote syncs, one channel read per running part on an ask, and the desktop's three GETs after it (§0.5). An installed look
is wider than this measure, which has no HTTP, no remote and no landing.

### 4.2 What the watcher adds, measured

The watcher's primitives, timed on the same machine (median, 95th percentile):

| Work | Cost | Paid for |
|---|---|---|
| The judgement over every running session, from memory | 0.3 µs for 100 sessions, 2.3-2.9 µs for 1,000 | every look |
| The stamp of a heard time | one in-memory write on the reading thread | every line or frame the harness sends |
| One job's CPU, by its accounting | 13 µs (24 µs) | each session unheard for half its bound or more, once a look |
| The heavy slot's records (files and process start times) | a directory's records, as `HeavyLook` reads them | each repository with heavy gates and a session unheard for half its bound, once a look |
| Step 1's word, kept through the say door | one HTTP call to the service | each quiet episode, once |
| Each step's coded note | one append to the session's event file | each step, once an episode |
| Whether a held process has exited | 7-17 µs | never: the run's own wait stamps its exit |
| A file's length and last write | 4-13 µs | never: background output is read from the tail a kept console holds, else the job's CPU judges |
| The whole machine's processes with their parents and times | 20-45 ms over about 780 processes | never: rejected |

So the watcher adds about 3 ns a look for each session heard within half its bound, against about 120 µs a session the look already spends,
and for those it makes no system call, no HTTP call and no file read. A session unheard for longer costs one job read a look (13 µs) and,
where its repository runs heavy gates, a look at the slot's records, whether it is stuck or a silent build that is working: the reads grow
with the sessions that are silent, not with the sessions watched. It starts no process and asks no model. It takes away the timeout's timer
per session and, once `LongCalls`' look rides the watcher's (SESSWATCH1b), that 30-second timer per light session. DONEEND1's quest read per
waiting session could ride the same look as one read; that is left for a row of its own.

The idle floor of §1.5 was measured the same way: node processes (24.19), each in a job of its own, its accounting read five seconds after
the start, past node's own start-up, and again 60 s later:

| Shape | CPU in 60 s |
|---|---|
| Idle on a one-second timer | 0 ms |
| Waiting on a socket nobody writes to | 0 ms |
| Waiting on a child that waits on its stdin (a hung command) | 16 ms |
| With a child serving HTTP that nobody calls (a dev server) | 31 ms |
| Parsing about 50 small JSON chunks a second (a harness reading a stream) | 500 ms |
| A child computing for the whole minute (a test run) | 52,078 ms |

A first run that read the job at the start counted node's own start-up, about 100 ms, in every shape; it was discarded. A harness waiting on
a server shows the same 0 ms as one that hung, which is why no idle reading alone enables an end (§2.2).

### 4.3 How it was measured

Three scratch programs, kept in the main checkout's ignored scratch and never in the repository: the primitives above; the look over the
stand-in ledger, built against the driver library and the tests' `StandInLedger.cs`; and the CPU floor. The numbers are this machine's, read
once; the design rests on their orders of magnitude, not their digits.

## 5. What the person sees and presses

### 5.1 On the session

- **The rail's line and the head's facts** say *quiet · 14 min* while an episode lasts, neutral: it waits on nobody. A heavy run's line
  (the UI/UX design §3.5) comes first where both hold. The mark goes when the session is heard again.
- **The conversation** shows each step as a coded note, and the step-1 word as Daoris's, as `LongCalls`' word is shown.

| Key | English | 中文 |
|---|---|---|
| `note.watch.quiet` | Nothing came from its agent for {{minutes}} minutes{{what}}. Daoris looked in. | 它的智能体 {{minutes}} 分钟没有任何输出{{what}}。Daoris 已查看。 |
| `note.watch.told` | Daoris told it so. | Daoris 已提醒它。 |
| `note.watch.what.call` | , while `{{call}}` ran | ，期间 `{{call}}` 在运行 |
| `note.watch.what.work` | , while its background work {{works}} showed no progress | ，期间后台工作 {{works}} 没有进展 |
| `note.ended.quiet` | Nothing came from its agent for {{minutes}} minutes{{what}}, and nothing it ran used the machine. Daoris ended this run, and the work goes on in the same tree. | 它的智能体 {{minutes}} 分钟没有任何输出{{what}}，它运行的程序也没有占用机器。Daoris 结束了这次运行，工作会在同一工作树中继续。 |
| `note.ended.quiet-pressed` | You ended this run after {{minutes}} minutes with nothing from its agent, and the work goes on in the same tree. | 它的智能体 {{minutes}} 分钟没有任何输出，你结束了这次运行，工作会在同一工作树中继续。 |
| `note.ended.gone` | Its agent's process had ended, but something it started still held its output; Daoris ended what was left. | 它的智能体进程已经结束，但它启动的程序仍占着输出；Daoris 结束了剩下的部分。 |
| `note.watch.left` | You left it running. Daoris looks in again once it is heard from. | 你让它继续运行。它再次有输出后，Daoris 才会重新查看。 |
| `work.rail.quiet` | quiet · {{time}} | 无动静 · {{time}} |

`note.ended.timeout` stays in both catalogues for the records that carry it.

### 5.2 In *What needs you*

A row of kind `quiet`, its place Sessions, its group *Holding work*, scoped as its session (`SessionScope`), counted on the bell, written
in .NET (`NeedsAPerson`) from the watcher's state read from memory, in every place the person-waits knowledge lists. Its words say only
what happened, so they hold before the word and the end are built (SESSWATCH1a) and after:

| Key | English | 中文 |
|---|---|---|
| `work.attention.quiet` | quiet, nothing running | 无动静，也没有在运行 |
| `work.attention.quietWhy` | {{session}} has said nothing for {{minutes}} minutes, and nothing it runs uses the machine. | {{session}} 已 {{minutes}} 分钟没有任何输出，它运行的程序也没有占用机器。 |
| `work.attention.quietTold` | Daoris told it once. | Daoris 提醒过它一次。 |
| `work.attention.quietCarried` | Daoris carried it on once, and it is still quiet. | Daoris 让它接着做过一次，它仍无动静。 |
| `work.attention.quietBusy` | busy, nothing heard | 在运行，但没有任何输出 |
| `work.attention.quietBusyWhy` | {{session}} has said nothing for {{minutes}} minutes, while something it runs keeps the machine busy. | {{session}} 已 {{minutes}} 分钟没有任何输出，而它运行的程序一直占用着机器。 |
| `work.attention.quietWords` | Your words wait to reach it. Ending it hands them to the work's next part. | 你的话还在等它接收。结束它，你的话会交给这项工作的下一段。 |
| `work.attention.quietHeavy` | It waits for the heavy run behind {{holder}}. | 它在 {{holder}} 之后等待重型检查。 |
| `work.attention.act.carryOn` | End and carry on | 结束并接着做 |
| `work.attention.act.leave` | Leave it running | 让它继续运行 |

Its other presses are *Hand over…* (转交…), the page's press for a quest session built in SESSWATCH1a (§2.3), and the existing *Stop…*
(停止…), and its door opens the session. The *busy, nothing heard* row offers *Leave it running*, *Stop…* and *Hand over…*, never *End and
carry on*. *Waiting on you* (等你处理) is a park's and is not used.

### 5.3 Settings and Ask Daoris

| Key | English | 中文 |
|---|---|---|
| `settings.driver.quiet.label` | Look in on a quiet session after | 会话无动静多久后查看 |
| `settings.driver.quiet.unit` | minutes | 分钟 |
| `settings.driver.quiet.why` | A session is never stopped for running long. One that says nothing and runs nothing for this long gets Daoris's help: a word, then your choice or a fresh start of the same work. | 会话不会因为运行时间长而被停止。如果它这么久既没有输出、也没有在运行任何程序，Daoris 会协助它：先提醒它，再由你决定，或让同一项工作重新开始。 |

Ask Daoris answers *how long may a session run?* with: as long as it works; Daoris looks in on one that has gone quiet.

## 6. The cap, accounts and spending

- **A quiet session keeps its slot while its run lasts**, as CAPCOUNT1 counts it today: through step 1 and the row, its record is active, its
  tree and quest held. **An end frees it**: the ended record holds nothing (`Planner.cs:1451-1453`), and its carry-on starts behind the cap,
  the person's hold, a busy tree and the strikes like any start (D80), waiting its turn where the cap is full. No slot is reserved.
- **How a session is known to be working** is §1.2's signs. The cap reads none of them: it counts records, as before.
- **Accounts.** The carry-on runs on the account the walk gives, as any carry-on does (D125, D165, D197). The watcher reads no account. A
  session whose account reached its limit is ended by the limit's own words where its door says them, which also count as heard; a limit
  that a door does not say looks like any other quiet.
- **No spending budget is added.** No wall-clock limit remains, and none replaces it. The spend of a stuck line is bounded by its account's
  allowance (D165, D197), by the strikes where the limit is set (D80: Daoris's own ends count), by one end per episode, and by the cap. A
  busy line that says nothing reaches the person at 3Q; one that says something while it circles is what §2.4 leaves.
- **A hung session holds its seat until a press.** With the cap at two, two hung sessions stop all driven work until the person acts
  (§2.3); the row counts on the bell for that reason. Any budget a later design adds parks the line at its next start for the
  person, and never ends a turn.

## 7. Proof

**A stand-in that can be told what to do.** No reusable stand-in can today be told to stream for a while, go silent, hang a call, background
work or exit without an end (each test writes its own script). SESSWATCH1a adds one, for the pipe door and the protocol door, told by its
target: stream a line every *k* seconds; go silent after its first line; write only to stderr; run a subagent that streams while its parent
says nothing; open a call and hang on a child that waits on its stdin; background a child that writes and computes, or one that sleeps;
hold a permission request; exit leaving a grandchild holding the pipe. The DONEEND1 stand-in (`DoneEndTickTests.cs:107-143`) and the family
rehearsal's pipe stub's cues (`tools/family-rehearsal.mjs:757-971`) are its exemplars.

**Clocks.** The watcher reads every bound off a handed `TimeProvider` and the job's CPU through a seam: the fast half moves the clock and
hands the CPU it wants, as the held-clocks knowledge asks; the `Process` half runs real jobs. No test waits a bound out on the machine's clock.

| # | Case | Row | Half |
|---|---|---|---|
| 1 | Streaming past any old bound left alone: lines every 5 s, the clock moved three hours; no mark, word, note or end. The same for a parent silent while its subagent streams, and for a harness writing only to stderr | 1a | fast; Process with real stand-ins and a moved clock |
| 2 | A silent quest session in a cell not enabled: at Q the mark and (1b) the word kept with its key; at step 2's time the row, never an end; *End and carry on* ends it `stopped` as the person's with `ended.quiet-pressed`, released, no strike, and the next look carries it on in a new part handed the brief; the person's waiting words go to that part as theirs, never to a resume of the one that hung | 1a, 1b | fast; Process for the press's end |
| 3 | The same in an enabled cell: the end at step 2's time, the word withdrawn before the conclusion, `ended.quiet`, a strike; the carried-on part silent from its start brings the row; at the strike limit `parked-quest`, and with the limit at 0 no park | 1e | fast; Process |
| 4 | A busy foreground call: no line, the job busy; no mark and no word; at 3Q unheard the *busy, nothing heard* row with no *End and carry on*. Busy read by window: a session that computed silently for ten minutes and then hung reads idle within a minute of hanging, its past CPU never counted. A session whose job could not be joined: busy unknown, the row at step 2's time, never an end | 1a | fast; Process with a computing child |
| 5 | Waiting left alone: a held permission request; a call waiting for the heavy-run slot until 3Q, then the row naming the holder | 1a | fast |
| 6 | The person's words on their way at step 2's time bring the row, never an end, in an enabled cell too | 1e | fast |
| 7 | An approved hand-over waiting on a hung turn goes on at the watcher's end: `stopped`, `ended.handed-over`, no strike, the next part on the agent named | 1e | fast; Process |
| 8 | A background command left alone: a protocol stand-in backgrounds a child that writes and computes and ends its turn; past 5Q nothing; the child then sleeps: at 3Q the word as a turn of its own | 1a, 1b | fast; Process |
| 9 | State E: the root exits, a grandchild holds the pipe; within G the job ended, the capture abandoned, `ended.gone` as a note line, and the run concluded by its exit: a clean exit with its quest done is `completed`, one holding its quest parks, with no strike and no carry-on; a crash is `failed` and carried on. On the protocol door, a stdout a stray holds after the prompt was answered concludes as answered | 1a | Process |
| 10 | A watcher word not yet on the wire never outlives its episode: withdrawn when heard; withdrawn before the conclusion; a word left on an ended record by a crash settled at the come-up before the first plan; never a go-on; a state-C word taken resets no answers-alone count | 1b | fast |
| 11 | The clock: a sleep of the machine (the unbiased clock held still) counts no quiet; `PollSeconds` at 180 and looks slower than the pace still count quiet; off Windows, the gap rule | 1a | fast |
| 12 | The headless host: each step's console line; Ctrl-C ends a quiet run as the driver's shutdown, carried on by D104; `--once` and `--until-idle` with `--quiet-end` end a session unheard that long, busy or not, and return, with and without a remote; without it they wait, saying so | 1a | fast |
| 13 | An intake: the mark and the row with *Stop* and *Leave it*; no word, no carry-on, no strike; gone, it fails as a timed-out intake failed | 1a | fast |
| 14 | The cost is flat: 1, 10, 100 and 1,000 running sessions; for those heard within half their bound a look makes no system call, HTTP call or file read (counted through the seams); one job read a look for each silent one; the in-memory time recorded beside, never asserted | 1a | fast |
| 15 | The settings: a loaded file keeps `timeoutMinutes` as stored through every save and never reads it for a bound; a configuration built in memory writes none; an older build's read of the file still finds 90; `quietMinutes` set, refused out of range and listed; the retired verb, door and tool answer as §3 says | 1a, 1c | fast; the setting-doors checks |
| 16 | The page: the notes, the rail's line, the row in both its variants and its presses, *Hand over…* on a quest session through `QuestHandOver`, in English and 中文 | 1a, 1b | vitest, stories, `i18n:check`; the bridge-presses checks |
| 17 | On the install: the quiet episodes by door and state, the real harnesses' idle CPU, and a session past 90 minutes left alone | 1d | the parent |

**The backstops.** The tests that set `TimeoutMinutes` as a backstop (§0.3), about fifty sites in thirty-four files, keep one in one place:
`WaitAsync` keeps its `minutes` parameter for the passes, so a test-only bound through the same parameter (a `TestBound` on the driver,
never read from `driver.json`) keeps the fast suites from hanging on a dead stand-in without rewriting each site. A case that is the
watcher's uses the moved clock instead.

## 8. The rows

Each row is a feature a person can see working, built end to end (D187). Nothing ends a run the watcher judged quiet until SESSWATCH1e, and
SESSWATCH1e only where SESSWATCH1d's record allows.

1. **SESSWATCH1a — no wall clock on a working session; a quiet one seen and brought to the person** (driver, modules, web; first).
   `WaitAsync` no longer ends a driven session, an answered park's resumed run, a native go-on or an intake on time; the passes keep their own
   minutes, uncapped. The watcher's state and look (heard stamped by each capture's own parameter at every line and frame, busy by window
   with its validity, alive, the waits of §1.3, the unbiased clock), riding every look and both headless paths; the quiet mark;
   `session.quiet` with each episode's outcome; the `quiet` row at step 2's time and the *busy, nothing heard* row at 3Q, with their presses:
   *End and carry on* as the person's released stop, their waiting words handed on, and *Hand over…* built on the page for a quest session
   through `QuestHandOver`; state E as a bound on the capture, concluding by the exit, which takes the place of the timeout's one other job;
   the headless host's console line and `--quiet-end`; `timeoutMinutes` kept and unread, the old verb, door and tool retired with §3's
   words; the stand-in of §7; the backstops kept as one test-only bound. Proof: cases 1, 2 (without the word), 4, 5, 8 (the first half), 9,
   11-15 and 16. **Why first, and safe:** it removes the kill at once, nothing quiet is ended by Daoris, a hung capture still ends (state E)
   without turning a finished run into a cut-off, and the safety the timeout gave becomes a question to the person after thirty minutes of a
   session that said nothing, busy or not.
2. **SESSWATCH1b — Daoris's word** (driver, web; after 1a). Step 1 for quest sessions by door, the `watch` key kind, its withdrawal and its
   settlement at the come-up, its exclusion from `AnswersAlone.Resets`, the row's *told* line; `LongCalls`' look folded into the watcher's.
   Proof: cases 2 (the word), 8 and 10.
3. **SESSWATCH1c — the quiet setting** (driver, service, web-settings; after 1a, beside 1b). `quietMinutes`, its verb, `driver list`, Ask
   Daoris's `quiet` door and `settings_quiet`, the help room and its goldens, the Settings → Driver field, the retired doors naming it.
   Proof: the setting-doors checks and case 15.
4. **SESSWATCH1d — measured on the install** (the parent with the owner; after 1a and 1b have run there for at least two weeks of driven
   work). From `session.quiet`: each door and state's episodes, how long each lasted, and how it ended; the real harnesses' idle CPU; the
   longest quiet of a working turn; the cost of a resume and a brief. A D200 note lists the cells whose record allows an automatic end, and
   reads the bounds again; a session past 90 minutes is seen left alone.
5. **SESSWATCH1e — the automatic end and carry-on, where the record allows** (driver; after 1d). Step 2 in the cells 1d listed: the end, its
   strike, the word withdrawn first, the approved hand-over taken, the person's words bringing the row instead, the carried-on part's quiet
   bringing the row. Proof: cases 3, 6 and 7.
6. **SESSWATCH1f — the passes' minutes, asked** (consider; D155, D163, D176, D177, D199). Whether a helper's, a reviewer's, a partner's or a
   meeting turn's minutes become the time its asker is told it is late while the watcher watches it, rather than an end.

## 9. What D200 amends, and what it rejects

**Amends:**
- **D80.** A cut-off's *timed out* becomes the watcher's end of a quiet run, in a cell the record allows or under `--quiet-end`, counted as
  a strike as before; *the session timeout is set from a terminal* is retired. The carry-on is unchanged: a new part in the same tree handed
  the brief (D189), its instruction naming the quiet. A run whose root exited is concluded by its exit, never as a cut-off; the person's *End
  and carry on* is their stop released at once (D58, D126 §3.4), carried on with no strike.
- **D188 point 1 and D193 part 9.** Among Daoris's own stops, *timeouts* becomes *the watcher's end*. **D188 point 2 and D193 part 9.**
  Daoris's urgent word gains a second reason: the watcher's one word to a quest session seen quiet, once per quiet episode, never outliving it.
- **D83's BGWAIT notes.** A background wait is bounded by the watcher's state C, not the timeout: work that never ends holds the session until
  it shows no progress for 3Q, then gets a word, then the person or, where allowed, an end.
- **D131 point 5.** *A carry-on after a time-out* reads *after the watcher's end*.
- **D163.** An approved hand-over waiting for a turn's end is taken at the watcher's end of that turn.
- **The settings.** `daoris driver timeout` (D80), the `timeout` door (D110) and the `settings_timeout` grant (D183's HELPMODE1c3 note) are
  retired; `quietMinutes` and its doors are added.
- **D155, D163, D199.** Their passes' minutes are no longer capped by `timeoutMinutes`.
- **D184's held permission bound** keeps its 15 minutes, its reason now its own: a request nobody answers is refused so the turn goes on.

**These stand:** D46 §4, D51, D58 (a stop is no strike), D104's sweep and its interrupted carry-on, D105 §3, D124, D126 §3.4, D131 points
1-4, D132, D136, D137, D165, D167, D184, D189, D199 point 8 and COAGENT1g, D24 and D130's CAPCOUNT1 (an ended record frees its slot, as
today).

**Rejected:**
- **A longer timeout, or one per repository.** D80 rejected it for a cut-off, and it still ends a working session at its bound.
- **An idle timeout, or ending on silence and low CPU alone.** A foreground command, a model's step on a door that streams nothing, a call to
  a server, a subagent and a background test run can each be unheard; a harness waiting on a server uses no more CPU than one that hung.
  Only the record of what such quiet turned out to be, per door and state, earns an automatic end (Codex's read, finding 1).
- **A model judging a session's health.** No model judges the work (D24, D54, D193), and its cost would grow with the sessions.
- **A process or a heartbeat per session.** A process costs per session; a heartbeat the agent must send is a self-report (D46 §4).
- **The keeping stop (`session/cancel`) as Daoris's end.** It can land on the person's words on their way (D136 point 4, BGWAIT2's rejection),
  the native driven run has none (D188), a hung harness may not answer it, and D188 keeps it the person's press. STEER3 may bring it back.
- **Ending through the processes' registry.** Its stops conclude `stood-down` or `stopped` and an intake reads any as the person's; the
  watcher's end has a cause of its own, read by every conclusion path.
- **Resuming the quiet session's own conversation at the end** (§2.2).
- **Ending a run whose root exited as a cut-off** (Fable's read, finding 1). Its exit is its turn's end; only the capture a stray holds is
  bounded, and the run concludes by what it said.
- **Busy as a rate from one baseline** (finding 2). The job's CPU is cumulative, so past work would read busy for hours after a hang.
- **Leaving busy silence unbounded** (finding 3). It is the one case the timeout once ended that nothing else reaches; the row answers it,
  with no end.
- **Counting the person's *End and carry on* as a strike.** A person's stop is none (D58), nor their hand-over (D163); the strikes bound
  what restarts with nobody between.
- **A hand-over chosen by Daoris.** It changes the maker unasked; D163 point 3 rejects automatic hand-overs by default.
- **A second end at step 3.** Daoris would decide the work is lost.
- **Reserving a carried-on line's slot.** CAPCOUNT1 counts records; an ended record frees its slot and the carry-on starts behind the cap.
- **A token or spend budget now** (§6).
- **A machine-wide process query, a file read per session or the event file's tail per look** (§4.2). The one exception is the heavy slot's
  records, read a look only for a session unheard for half its bound in a repository with heavy gates.
- **Stamping heard from recorded events alone.** A subagent's lines, the partial stream's other deltas and stderr never become the parent's
  events (§0.4).
- **A reset of the count when looks are far apart.** A long `PollSeconds` would never count quiet; the clock that stops in sleep does instead.
- **Mapping the old 90 onto the quiet bound** (§3).

**What is left.** codex-acp's tool frames and background work, dsh's long steps, and the real harnesses' idle CPU are unmeasured, so no end
is automatic on them. A quiet conversation is said and never helped. A harness that repeats a meter, and busy work that circles while it
says something, are never quiet (§2.4). A call waiting for the heavy slot is the person's after 3Q, and the holder's own run is not the
watcher's where no session holds it. Until SESSWATCH1e a hung session frees its seat only by a press; the headless host without
`--quiet-end` waits until its operator's Ctrl-C.

## 10. The reads

**Codex's read, round 1** (`codex exec`, read-only, on this branch at `d921205ac`, 2026-10-23). Fourteen findings, ten blocking; the
reader also checked and confirmed the inventory of the timeout's readers and callers, the shared `SessionEvents` instance, a joined job's
accounting, D80's carry-on and D189's brief, the strike's exclusions, D188's amendment and DONEEND1's precedence. Each finding was checked
by the steward at the lines it cites; all fourteen held and were taken. Two revisions made by the steward during the read (the person's
words on their way and the approved hand-over, §1.3) were read in it.

| # | Finding | Checked at | Verdict | What changed |
|---|---|---|---|---|
| 1 | Blocking: no line and low CPU cannot establish that work stopped (a model's first token, a server call, a subagent, dsh's silent step) | `Acp.cs:1270`, `StructuredOutput.cs:176-196`, dsh evaluation :101-118 | taken | An automatic end only in cells SESSWATCH1d's record allows; everywhere else the row, with *End and carry on* as the person's press; measuring moved before any automatic end (§1.1, §2.2, §8) |
| 2 | Blocking: `Evented` misses subagents, dropped deltas and stderr | `HarnessCapture.cs:144-154`, `ClaudeStreams.cs:73-81`, `AcpStreams.cs:95-102`, `Acp.cs:1712-1725` | taken | Heard is stamped at the door's read of every line and frame, the time only (§1.2) |
| 3 | Blocking: a job missing or joined late must not read as idle | `SessionProcesses.cs:369-377`, :577; `ProcessJob.cs:35-66`, :98-108 | taken | The registry's entry holds the job and its validity; busy unknown never authorizes an end; the old "what is left" line that allowed one is gone (§1.2) |
| 4 | Blocking: "as the timeout ended it" needs its own cause, and a capture held by a descendant must be let go | `Driver.cs:3243-3246`, :3399-3418; `Observation.cs:9-17`; `DriverIntakes.cs:261-266` | taken | The watcher's cancellation in `CancelAfter`'s place, `EndedBy.Watcher` read by every conclusion path, the job terminated and the drain bounded (§2.2) |
| 5 | Blocking: an intake takes no words, no hand-over, no carry-on and no strike | `Driver.cs:3171-3177`, `DriverIntakes.cs:45-49`, `SessionLedger.cs:319-333` | taken | The word, the carry-on and the strike are quest sessions'; an intake gets the mark, the row with *Stop* and *Leave it*, and state E's end (§2) |
| 6 | Blocking: a durable watcher word can outlive its episode and reopen the failed record | `DrivenInbox.cs:838-888`, `UncertainWords.cs:61-65`, `DaorisWords.cs:137-151`, `Planner.cs:1441-1468` | taken | A `watch` key kind; withdrawn before any conclusion; never counted for a go-on; settled at the come-up (§2.1) |
| 7 | Blocking: a failed record holds no slot | `Planner.cs:1451-1458`, :1882-1895 | taken | §6 says the slot is freed at the end and the carry-on starts behind the cap; reserving one rejected |
| 8 | Blocking: `--once` has no next look to carry on | `Driver.cs:1503-1525`, :1554-1614 | taken; refined in round 3 by Fable's findings 1 and 4 | `--once` ends a gone run and reports it for the next run; its wait is woken at the pace with or without a remote; `--until-idle` carries on (§2, case 12) |
| 9 | Blocking: a two-minute reset disables the watcher under a long `PollSeconds` | `DriverConfig.cs:1423`, `DriverWatch.cs:641`, :780, :827 | taken | Quiet counted on a clock that stops in sleep; the gap rule only off Windows, relative to the pace (§1.5) |
| 10 | Blocking: the approved hand-over's conclusion and strike were unsettled | `Driver.cs:2398-2402`, `DriverHandOvers.cs` | taken | It concludes `stopped`, `ended.handed-over`, no strike, the agent named going on (§2.2) |
| 11 | A heavy-slot waiter is exempt for ever, and meters can refresh heard | `HeavySlot.cs:19-20`, :172-202; `Acp.cs:2296-2340` | taken | The heavy wait brings the row after 3Q; both named as what heard cannot tell (§1.3, §2.4) |
| 12 | The save does not write 30 into a loaded file | `DriverConfig.cs:901-955`, `JsonKeeping.cs:266`, :296 | taken | §3 says the loaded file's merge keeps the key; only a configuration built in memory stops writing it |
| 13 | "Healthy" meant "recently heard"; the slot's records are files | §4.2, `ProcessJob.cs:98-108`, `HeavySlot.cs:214-240` | taken | §4.2 restated: reads grow with silent sessions, working or not, and the slot's records are counted |
| 14 | The rows and bounds needed reconciling: 1a's row words, 1a's retired door, state C at 5Q, the strike limit at 0 | §§2, 5.2, 7, 8; `Planner.cs:1781`, `DriverConfig.cs:79` | taken | The row says only what happened; the retired door names `quiet` from 1c; state C stated at 5Q; the strike limit qualified; the destructive row gated last |

**Fable's foundation read, round 2** (the parent's, under D191, at `d536e7966`, 2026-10-23). Verdict: the shape is right, accept once the
four blocking findings are answered, none a change of shape. Twelve findings, four blocking; the reader confirmed every citation but two
slips (finding 8), and found holding the timeout's inventory, the captures' seams, the job's best effort, the words' and strikes' paths, the
cap, the pause, the stop and the intake. Each finding was checked by the steward at the lines it cites; all held and were taken.

| # | Finding | Checked at | Verdict | What changed |
|---|---|---|---|---|
| 1 | Blocking: state E concluded a cleanly ended pipe-door run as a cut-off, a strike and a context re-read | `Driver.cs:3236`, :3243-3246, :2372-2390; `Acp.cs:446-460`; `ProcessJob.cs:10-13` | taken | State E is a bound on the capture: after G the job ended, the capture abandoned, the run concluded by its exit and what it said, `ended.gone` a note line, a strike only where the exit fails (§2, case 9) |
| 2 | Blocking: busy from one baseline is cumulative, so past work reads busy for hours after a hang | `ProcessJob.cs:222-231` | taken | Busy is the CPU between the two newest reads over their window, a minute at least (§1.2, §1.5, case 4) |
| 3 | Blocking: a busy, silent stall escaped every step, the one regression against the timeout | §1.2, §2.4, §6 | taken | At 3Q unheard, whatever the CPU, the *busy, nothing heard* row with *Leave it running*, *Stop…* and *Hand over…*, no word, no end (§1.5, §2.3, §5.2) |
| 4 | Blocking: the headless host never returns on a session that hangs alive | `Driver.cs:1465-1486`, :1503-1525, :1575-1604 | taken | Its console line is the row, Ctrl-C the stop (D104's interrupted carry-on), and `--quiet-end <minutes>` gives a run nobody watches its end, on `--once` and `--until-idle` (§2, case 12) |
| 5 | The state-C word reset the answers-alone count | `AnswersAlone.cs:89-91` | taken | The `watch` kind is excluded from `Resets` (§2.1, row 1b) |
| 6 | *Hand over…* does not exist on the page for a quest session | `HandOver.cs:52-60`, :145; `HandOverQuest.cs:67`; `work.json:45`; `say.ts:176` | taken | SESSWATCH1a builds the page's press through `QuestHandOver` (§2.3, §5.2, case 16) |
| 7 | The person's press counted a strike unsaid, and lost their waiting words | `DriverContinuations.cs:1004`; D58; D163 | taken | The press is the person's stop released at once, no strike; their waiting words go to the carry-on as theirs, and the row says so (§2.3, §5.2, case 2) |
| 8 | Two citation slips; the stamp must not ride `HarnessWords`; say what moves for the passes | `HarnessCapture.cs:152-154`, :54-77; `DriverOpinions.cs:154`, `OpinionPass.cs:103` | taken | Citations fixed; the stamp is each capture's own parameter (§1.2); what moves at 30 and at 90 said (§2.4) |
| 9 | A word already on the wire cannot be withdrawn and reads stale | `Acp.cs:951-972`; D188 point 6 | taken | "A word not yet on the wire never outlives its episode"; the word says the call *ran* (§2.1) |
| 10 | Name D199's lead, COAGENT1g, and what the cap means for hung sessions | D199 point 8; COAGENT1g; §6 | taken | A lead's pieces follow `from`; the hand-over's descendants end with the job; two hung sessions at a cap of two stop all driven work until a press (§2.2, §2.3, §6) |
| 11 | Two per-episode costs missing from §4.2; §9's rejection needed the heavy slot's exception | §4.2, §9 | taken | Both rows added; the exception said |
| 12 | Build note: one test-only bound replaces the backstops | `WaitAsync`'s `minutes` | taken | `TestBound` through `minutes`, never from `driver.json` (§7) |
