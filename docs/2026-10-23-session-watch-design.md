# No time limit on a working session; a watcher supports one that is stuck (SESSWATCH1, 2026-10-23)

**Status: proposed (D200), round 1.** Written by Claude Opus at `360b27bc6`. Codex's read is owed (D191), and Fable's, since the
design amends decided rules other designs rest on (D80's carry-on trigger, D188 point 1's list of stops, D193 part 9). Nothing is
built. §10 keeps the reads and their verdicts.

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
  the process tree (:3413), answering null for a timeout. Three sites call it:
  - `HoldAsync` (:3243-3245), which every door and every kind of run goes through;
  - the native door's go-on with held words (`DriverContinuations.cs:999`), a fresh bound for each run;
  - SESSCTX1c's one retry of a native resume (`DriverContinuations.cs:1035`).
- **The bound is a run's, not a turn's.** It starts after the spawn. On the protocol door the whole conversation runs inside that one wait
  (`Agents/Acp/Acp.cs:617-671`), so the person's words never reset it. A running session keeps the timeout it started under, since each
  look builds a new `Driver` from that look's configuration (`Loop/DriverWatch.cs:593-594`).
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
  is said as the timeout's. The native door says nothing before the kill.
- **Where it is set and said.** `DriverConfig.cs:35`, default 30 (:60), written on every save (:980), read with a floor of 1 and no
  ceiling (:1422). `daoris driver timeout <minutes>` (`Platform/DriverChoicesCommand.cs:497-513`) and `driver list` (:942-944). Ask Daoris's
  `setting` door `timeout` (`Help/Proposals/HelpSettingProposals.cs:37`, :213-214; the service's `HelpSettingProposalWriter.cs:25`, :92-95),
  its granted tool `settings_timeout` (`Daoris.Service.Mcp/ChosenSettingsTools.cs:46-51`), the help room's lines
  (`Help/Room/HelpRoomMayDo.cs:318`, `Help/Room/HelpRoomDoors.cs:170` and its goldens), the grant's label `help.grants.op.settings.timeout`
  (en and zh `help.json:275`, `help/access/grants.ts:52`), and `settings.checks.why`, which says a light session ends "not at its timeout".
  No page has a field for it. The CLI keeps only its usage line (`cli/driver.ts:156`). The install set it to 90.
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
  batched (`Sessions/Events/SessionEventLog.cs:39-50`), after which `Evented` is raised in-process (:36, :68). No in-memory "last event at"
  exists anywhere. The kinds are `user`, `message`, `thought`, `tool`, `plan`, `usage`, `turn`, `note`, `raw` and `receipt`
  (`Sessions/Domain/SessionEvents.cs:13-48`); `note`, the words said to it and `receipt` are written by Daoris, the rest by the door from the
  harness's stream.
- **Per door, while a turn runs:**
  - **claude-code (native)** runs with `--include-partial-messages` (`Agents/Adapters/Adapters.cs:2085-2108`), so the model's text and thinking
    stream as deltas (`Agents/Streams/StructuredOutput.cs:176-198`). A tool call is one `tool` event at its start and nothing until its result
    (:229-242, :258-288). Its background commands are ended by the harness at the turn's end (`docs/2026-09-30-console3-native-streams-evidence.md`
    :46-52), which BGWAIT1c says as `ended.background-cut` and carries on. Words reach it only at its run's end: it is a turn-end door
    (`Driver.cs:3176-3194`).
  - **claude-code-acp** streams message and thought chunks. A tool call's input and output arrive in a burst at its start and nothing until it
    completes (`docs/2026-09-28-console2-streams-evidence.md` :111-120; the task archive's line 6790-6796). Background work is announced and ended
    by `async_task_*` frames, kept in memory by `AcpBackground` (`Agents/Acp/AcpBackground.cs:94-109`, `Acp.cs:425`) and never as events
    (`Acp.cs:2335-2338`). Words reach it at its next step (`Acp.cs:951-972`, D136).
  - **codex-acp** is a turn-end door (`Acp.cs:962-965`); its background work is unknown (console2 evidence :168) and its tool frames unmeasured.
  - **dsh** sends committed facts only: a `tool_call` in progress, then completed, and `usage_update` (`docs/2026-09-21-dsh-evaluation.md`
    :101-102, :115-118), so its model's step is silent.
  - **A text-only door** writes no events at all (`Agents/Streams/HarnessCapture.cs:54-77`), only its transcript.
- **In memory, per running session.** Its process, joined to a kill-on-close job as it is tracked (`Sessions/SessionProcesses.cs:372-378`,
  ORPHAN1), whose basic accounting (read for `ProcessJob.Active`, `Agents/Consoles/ProcessJob.cs:98`, :224-231) also carries the CPU time of
  every process in it, background shells included. Its open calls with their `Since`, and `Waiting` where a call is held for the person
  (`Sessions/Events/RunningCalls.cs:14-26`, `Calling` :124), fed by `Evented` (:92). Its background work on the protocol door. Its held
  permission requests (`HeldPermissions.cs`).
- **`LongCalls`, the closest exemplar** (`Sessions/Heavy/LongCalls.cs:32-216`). Under light checks, a call or a background task past 20
  minutes is told to its session once, urgent, as Daoris's word, only where the door takes words at its next step, and never ended. It runs a
  30-second timer per session (:63, :134-148), wired at `Driver.cs:3613-3621`.

### 0.5 The loop and what a look costs

- `DriverWatch.RunAsync` (`DriverWatch.cs:334-817`) makes a look `PollSeconds` (15) after the last whole look, or sooner on an ending, a nudge
  or a ledger change: every move of a session record wakes a plan look (`Service/LedgerChanges.cs:10`, :70). Each look builds a `Driver`
  sharing the running sessions, the processes and the hooks (:593-609) and runs `TickAsync` (`Driver.cs:631-650`), whose stages
  `LookStages` times; `watch.looked` writes the look's milliseconds to the machine log (`DriverWatch.cs:921-937`). The headless `--once` and
  `--until-idle` run looks without the watch (`Driver.cs:1465-1525`).
- A look reads four snapshot GETs (`Service/ServiceClient.cs:135-148`), one claim per running quest session where a remote syncs
  (`Driver.cs:1404-1437`), and one channel read per running part on an ask (`Loop/ChannelReach.cs:76-108`). The desktop's report adds three
  GETs (`Modules/Loop/DriverLoop.cs:898-919`). The planner is pure. No look reads git or a file per running session.
- Costs paid per session outside the look: the timeout's cancel timer; DONEEND1's quest read every minute for each session waiting on its own
  background work, each a whole quest list (`Driver.cs:3754`, `Acp.cs:852-868`); `LongCalls`' timer; the output tail every 500 ms for each
  background task whose console is kept (`Agents/OutputTail.cs:23`).
- Liveness is not polled: each run awaits its own process (`Driver.cs:3406`), and its ending wakes the watch (`Sessions/RunningSessions.cs:621-649`).

### 0.6 The carry-on, the strikes and the other stops

- A timed-out record ends `failed`. The look its ending wakes (`DriverWatch.cs:780-788`) carries its taken quest on (`Quests/Domain/Planner.cs`
  :1329-1343, `CarriesOn` :1910-1919, `CarryOn` :1504-1538) in a new record and a new conversation in the same tree, handed Daoris's brief
  (`Driver.cs:2067-2084`, :2164-2168, :2210-2290; `started.cut-off` :3022), its instruction saying what cut the last session off
  (`Adapters.cs:532-563`, :632-646).
- A session's own conversation is resumed only where words wait on its record (`Driver.cs:1956-1967`, `DriverContinuations.cs:109-160`),
  an ended record then going back to `working` (`DriverContinuations.cs:156-158`); a word of Daoris's alone never opens a new session where
  that resume falls back (`Driver.cs:2007-2015`).
- The strikes count `failed` records (`ServiceClient.cs:2013-2032`); the third parks the quest (`Exhausted`, `Planner.cs:1776-1791`), a
  `parked-quest` row in *What needs you* with *Try again* (`Modules/Loop/NeedsAPerson.cs:259-266`).
- A hand-over the person presses stops a live quest session at once, as the person's stop, and goes on with the named agent
  (`Loop/DriverHandOvers.cs:77-136`); an agent's own ask waits for its turn's end (:210-292). A pause stops at once as the person's stop
  (`Sessions/Work/WorkPausing.cs:151-212`).
- **The cap** counts active driven records (`Planner.cs:823-836`, `SlotsHeld` :870-877): a working session, one waiting on its own background
  work and a park each hold a slot; a person's conversation holds none (CAPCOUNT1).

## 1. What "stuck" means, per door and per state

### 1.1 The rule

A running session is **working** while it shows a sign of work, **waiting** while a wait its record names holds it, and **quiet** once
neither has been true for its state's bound. Quiet is not yet stuck: it is the watcher's reason to look closer and offer help. Silence is
never read as success (BGWAIT2) nor as failure; it starts the support steps of §2, each said.

### 1.2 Signs of work, read only from what Daoris already holds

- **Heard.** The time of the newest event the door recorded from its harness, of the kinds `message`, `thought`, `tool`, `plan`, `usage`,
  `turn` and `raw`, stamped in memory as `SessionEvents.Append` raises `Evented`, so nothing reads the file. For a text-only door, the newest
  line of its transcript. Daoris's own notes, the words said to the session and their receipts are Daoris speaking, never a sign.
- **Busy.** The session's job used CPU above the idle floor (§1.5): one accounting read of the job every driven session already joins,
  counting the harness and every command it started, background ones included. It is read first as a baseline once a session has gone
  unheard for half of its bound, then at each look while it stays unheard, so a session that is heard is never read.
- **Moving work.** For a session waiting on its own background work: that work's output grew, where its console keeps the tail in memory, or
  the job is busy.
- **Alive.** Its root process has not exited: the run's own wait on its exit (`Driver.cs:3406`) stamps the time it returns, so nothing
  polls a process.

### 1.3 Waits the record names, left alone

A session is never quiet while one of these holds, whatever the time:
- a call held for the person's answer to its permission request (`RunningCall.Waiting`); that wait keeps its own bound (§9);
- its call waiting for the heavy-run slot behind another session's run: the waiter's record beside the slot names it
  (`Sessions/Heavy/HeavySlot.cs:36-45`, `HeavyLook`, :214).

Two things a quiet session may carry change its steps rather than excuse it, since either could otherwise hold a hung session forever:
- **The person's words on their way to it.** Nothing ends a run while one is (D136 point 4), so where one waits at step 2's time the row of
  step 3 comes in its place: the person, whose words they are, decides.
- **A hand-over approved and waiting for its turn's end** (`DriverHandOvers.cs:210-292`). A hung turn never ends, so step 2's end is taken as
  its turn's end and the hand-over goes on: the conclusion reads the hand-over today only after a clean exit (`Driver.cs:2398`), and the
  watcher's end joins it.

A session holding the heavy-run slot is not waiting but busy: its gates run in a job of their own, named on the holder's record
(`HeavyHolder.Job`, `HeavySlot.cs:19-20`), whose accounting the watcher reads as it reads the session's. The heavy slot's records are read
only for a session already past its bound, one look of its repository's slot.

A go-ahead (`go_ahead_ask`) and a helper (`helper_ask`) are asked by tools that answer at once and tell the agent to end its turn
(`Daoris.Service.Mcp/GoAheadTools.cs:12-19`, `HelperTools.cs:18-27`), and a question (`question_ask`) parks it; a park (`awaiting-person`),
a pause and a quest waiting on another repository (D79) have no process, so nothing runs to be quiet.

### 1.4 The states, per door

| State | What says it | native claude-code | claude-code-acp | codex-acp | dsh | text-only |
|---|---|---|---|---|---|---|
| **A.** A turn runs, no call open (the model's step) | heard | deltas stream | chunks stream | unmeasured | silent until a fact | transcript lines |
| **B.** A turn runs, a call open (a command, a tool) | busy | silent until the result | a burst, then silent | unmeasured | silent until the result | transcript lines |
| **C.** No turn runs; it waits on its own background work (BGWAIT1-3) | moving work | never: the harness ends the work with the turn | yes | none known | none known | none |
| **D.** A woken turn whose end is never said (BGWAIT2's remainder) | heard | never | yes | none known | none known | none |
| **E.** Its process gone without an end | alive | every door | every door | every door | every door | every door |

A call in state B that waits on input, a lock or the network is idle, and so quiet; a test run, a build or a long generation parsed by the
harness keeps its job busy, and so working. Claude Code's own Bash tool ends a foreground command at its timeout, two minutes by default
and ten at most, as its tool's description says; Codex's bound on a command was not read.

### 1.5 The bounds (first values; judgements SESSWATCH1d measures)

- **Quiet, Q: 10 minutes** not heard, in states A, B and D, with the job not busy. The person sets it (§3), from 5 to 120. Ten minutes is
  longer than the longest foreground command Claude Code allows; the longest quiet of a working turn on the install is unmeasured
  (SESSWATCH1d). Step 1 at Q is a mark and one short word, the most a misread working session pays; only step 2, at 3Q, ends anything.
- **The idle floor: 0.2 seconds of CPU a minute** (1 second in 5, a third of a percent of one core), measured from the baseline read at
  half of Q to each later read. Measured on node stand-ins (§4.2): the
  shapes of a stall used at most 31 ms a minute (0.16 s in five), one parsing a stream of small chunks 500 ms a minute (2.5 s in five), and a
  test run's shape 52 s a minute. The real harnesses' floor is SESSWATCH1d's to measure before the value is kept.
- **Background, 3Q: 30 minutes** with no moving work in state C. A test run writes and computes; a dev server neither.
- **Gone, G: 60 seconds** from the root's exit, or its stream's end, to the run's end in state E: the door's drain and `EndedLingeringAsync`'s
  ten seconds fit inside it.
- **A look is counted only from the look before it.** A look made more than two minutes after the one before (the machine slept, or the loop
  was held) starts every session's quiet over, so a wake never reads every session as quiet at once.

## 2. The support steps, each bounded and said

**Where it lives.** One watcher per shell, held beside `RunningCalls` (`Modules/Loop/DriverLoop.cs:100`) and, like it, hearing `Evented`
(`RunningCalls.cs:92`): for each running driven session, its heard time, its job, its episode and its step. A stage of every look judges it
(`watch`, timed by `LookStages` with the others), so it shares the look's pace and its log line; the headless `--once` and `--until-idle`
judge it as they wait for their sessions. The attention slice reads its rows from memory, as it reads the hand-over's.

The steps run per **quiet episode**: from the look a session is first quiet to the look it is heard again, shows moving work, begins a wait
of §1.3, ends, or reaches the person.

| Step | When | What Daoris does | Bound |
|---|---|---|---|
| **1. A word** | at Q quiet (3Q in state C) | marks the session quiet on its head and rail, writes `watch.quiet` on its record, and keeps one word of its own for the agent, urgent | once per episode |
| **2. An end and a carry-on** | 2Q after step 1, quiet and never busy since; at once in state E | ends the run, says why (`ended.quiet`), and the next look carries the line on in the same tree | once per episode; the strikes bound the line |
| **3. The person** | the carried-on part is quiet before it was ever heard, or it cannot start; or, in step 2's place, the person's words wait on their way | a row in *What needs you*; Daoris ends nothing more | until the person acts |

**Never a silent end.** Every step writes a coded note on the record, a console line and a machine-log line (`session.quiet` {session,
adapter, state, minutes, step}); a run is ended only at step 2, and only after the word of step 1 and twice the quiet bound with nothing
busy: thirty minutes, at the defaults, of a session that neither said nor ran anything.

### 2.1 Step 1: a word, under D188

The word is Daoris's (`DaorisWord`, key `watch/<session>/<episode>`), kept on the record's `said` through the session's one dispatcher
before anything holds it (`DrivenInbox.KeepOrHoldAsync`, the session-words knowledge), urgent, as `LongCalls` keeps its word. It reaches the
agent as its door allows:

| Door and state | Where the word reaches |
|---|---|
| claude-code-acp, states A, B, D | at the next step (D136 point 1): when a hung call ends by the harness's own bound, the agent reads it before its next step |
| claude-code-acp, state C | at once, as a turn of its own: no turn runs, and the session goes on only by Daoris's prompt, as BGWAIT2's go-on does |
| native claude-code, codex-acp, dsh, a text-only door, in a turn | nowhere during the turn: it waits for the turn's end, and is withdrawn when the session is heard from again or at step 2, so it never makes a turn for a fact no longer true |

The words, Daoris's one English: *"Daoris: nothing has come from this session for {minutes} minutes{, while your call `{title}` runs | ,
while your background work `{names}` shows no progress}. If a command waits on input, a lock or the network, stop it and go another way;
run anything long in the background and read its output; if something only the person can give blocks you, ask with `question_ask`. Daoris
has stopped nothing."* `question_ask` turns a stuck session into a park with a question, which is the person's door for what only they can
give (D83, D124). Where `LongCalls` already told the same call, its word is the episode's word and no second is kept.

### 2.2 Step 2: an end and a carry-on (D80, D189)

- **The end.** The run is ended as the timeout ended it: the job's tree, with the protocol door told first (`AcpEnding`), so the stream's end
  is said as the watcher's. The record concludes `failed` with `ended.quiet` {minutes, what}, or `completed` where the run brought its
  quest to done (the timeout's rule, `Observation.AfterTimeout`, renamed). The step-1 word, if not taken, is withdrawn.
- **The carry-on is the timeout's, unchanged.** The look the ending wakes carries the line on by D80 in a new part in the same tree, handed
  Daoris's brief (D189), whose instruction says what cut the last part off from its record's note: `ended.quiet` names the minutes and the
  call that hung or the work that showed nothing (`Adapters.cs:632-646` reads the note). The brief carries the part's last tool calls and
  plan, so the new part learns where the work stood without the conversation that hung. An intake ended so goes as a timed-out intake goes
  today.
- **Counted.** The ended record stays `failed`, so the end is a strike, as a timeout was, and a line that keeps going quiet parks on the
  person at its third (D80, `Planner.cs:1776-1791`); the `parked-quest` row's sitting sentence names the watcher's ends.
- **Not the resume, for now.** Resuming the quiet session's own conversation with a word of Daoris's would keep the agent's exact memory,
  but a resumed ended record goes back to `working` (`DriverContinuations.cs:156-158`), so its strike would go and a line that hangs the same
  way each time could loop, paying a whole context at each go; a word of Daoris's alone opens nothing where the resume falls back
  (`Driver.cs:2007-2015`); and the conversation may hold what made it hang. SESSWATCH1d measures what a resume and a brief each cost, and a
  note may bring the resume back with a count of its own.
- **Not a hand-over.** Step 2 never changes the agent: an automatic hand-over is rejected by default (D163 point 3). The hand-over is the
  person's press at step 3.

### 2.3 Step 3: the person

When the part step 2 carried on is quiet before its agent was ever heard (Q from its start), or the carry-on cannot start, Daoris writes a
row in *What needs you* (§5.2) and ends nothing more: a second end without the person would be Daoris deciding the work is lost. The part
keeps running, its tree and its slot held, until the person presses:
- **Hand over…**, D163's hand-over to another agent: it stops the part and goes on with the agent chosen, in the same tree;
- **Stop**, the person's stop: the quest held behind *Try again*;
- **Leave it running**: the watcher stands down for this session until it is heard again (`watch.left` on its record), and the row goes.

### 2.4 What the watcher does not watch

- **A person's conversation** (a chat, Ask Daoris) is the person's (CAPCOUNT1's reason): past Q its head says it is quiet, and nothing more.
  No word, no end and no row; its turn's stop is the person's *Stop turn*.
- **A pass with minutes of its own** (a helper's 15, a reviewer's rule's 20, a partner's 45, a brainstorm's and a meeting's turn) keeps
  them, as D163, D155, D199, D176 and D177 give them: each is an answer owed to an asker by a time, and none of the kills of 0.2 was one.
  They are no longer capped by `timeoutMinutes`, so a reviewer is told the minutes it gets. Whether each becomes the time its asker is told
  it is late, watched rather than ended, is SESSWATCH1e's question, asked of each decision.
- **Busy work that goes in circles** (the same failing command again and again) is working by every sign here; telling it needs a reading of
  the work, which no step makes (§9).

## 3. What replaces `timeoutMinutes`

- **`quietMinutes`** in `driver.json`, 10 by default, from 5 to 120: how long a session may say nothing, with nothing it runs using the
  machine, before Daoris looks in. It is the one number the person sets; step 2's bound is twice it and state C's three times. It is written
  only when set, as `cooloff` is (`DriverConfig.cs:983-984`), so `driver list` tells the default from a choice.
- **Its doors**, each a place of the setting-doors knowledge: `daoris driver quiet <minutes>`; `driver list`'s line *quiet 10 minutes before
  Daoris looks in on a session*; Ask Daoris's `setting` door `quiet` and its granted tool `settings_quiet`, with the help room's rows and
  goldens; a field on Settings → Driver beside the cap, the strike limit and the cool-off (CAPSET1's card).
- **`timeoutMinutes` is kept, never read for a bound.** The save writes every field it holds (`DriverConfig.cs:980` writes this one on every
  save, 30 where the file had none), so the record keeps the value it read, as a number or none, and writes it back only where the file had
  it. The builds before SESSWATCH1a read it, and after a rollback (D139) would otherwise find the 30 a save wrote; that is the
  compatible-builds knowledge's rule, a key this build owns beside the one older builds read. A file without it gains none.
- **An install that set it** (the owner's 90) moves by doing nothing: its value stays for an older build, this build ends no run by it, and
  its quiet bound starts at the default. 90 is not mapped onto `quietMinutes`: it was a run's whole length, never a silence, and a person who
  raised it asked for longer runs, which a working session now has without limit. `driver list` says it once: *timeout 90 (no longer limits a
  session; kept for an older Daoris)*.
- **The old doors are retired, saying so.** `daoris driver timeout <minutes>` changes nothing and answers that sessions are no longer limited
  by time, naming `daoris driver quiet` (exit 1). Ask Daoris's `timeout` door and `settings_timeout` refuse with the same words, for a helper
  reading an older room. The help room, the grant's label and `settings.checks.why` lose the timeout.

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
session where a remote syncs, one channel read per running part on an ask, and the desktop's three GETs after it (§0.5).

### 4.2 What the watcher adds, measured

The watcher's primitives, timed on the same machine (median, 95th percentile):

| Work | Cost | Paid for |
|---|---|---|
| The judgement over every running session, from memory | 0.3 µs for 100 sessions, 2.3-2.9 µs for 1,000 | every look |
| The stamp of a heard time | one in-memory write on the thread that already appends the event | every event |
| One job's CPU, by its accounting | 13 µs (24 µs) | each session unheard for half its bound or more, once a look |
| Whether a held process has exited | 7-17 µs | never: the run's own wait stamps its exit |
| A file's length and last write | 4-13 µs | never: background output is read from the tail a kept console holds, else the job's CPU judges |
| The whole machine's processes with their parents and times | 20-45 ms over about 780 processes | never: rejected |

So the watcher adds about 3 ns a look for each healthy session, against about 120 µs a session the look already spends. For a healthy
session it makes no system call, no HTTP call and no file read; for one unheard for half its bound, one job read a look and, once quiet
where its repository runs heavy gates, one look at the slot's records; the number of those is the number of silent sessions, not of the
sessions it watches. It starts no process and
asks no model. It takes away the timeout's timer per session and, once `LongCalls`' look
rides the watcher's (SESSWATCH1b), that 30-second timer per light session. DONEEND1's quest read per waiting session could ride the same
look as one read; that is left for a row of its own.

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

A first run that read the job at the start counted node's own start-up, about 100 ms, in every shape; it was discarded.

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
| `note.watch.quiet` | Nothing came from its agent for {{minutes}} minutes{{what}}. Daoris looked in and told it. | 它的智能体 {{minutes}} 分钟没有任何输出{{what}}。Daoris 已查看并提醒了它。 |
| `note.watch.what.call` | , while `{{call}}` ran | ，期间 `{{call}}` 在运行 |
| `note.watch.what.work` | , while its background work {{works}} showed no progress | ，期间后台工作 {{works}} 没有进展 |
| `note.ended.quiet` | Nothing came from its agent for {{minutes}} minutes{{what}}, and nothing it ran used the machine. Daoris ended this run, and the work goes on in the same tree. | 它的智能体 {{minutes}} 分钟没有任何输出{{what}}，它运行的程序也没有占用机器。Daoris 结束了这次运行，工作会在同一工作树中继续。 |
| `note.watch.left` | You left it running. Daoris looks in again once it is heard from. | 你让它继续运行。它再次有输出后，Daoris 才会重新查看。 |
| `work.rail.quiet` | quiet · {{time}} | 无动静 · {{time}} |

`note.ended.timeout` stays in both catalogues for the records that carry it.

### 5.2 In *What needs you*

A row of kind `quiet`, its place Sessions, its group *Holding work*, scoped as its session (`SessionScope`), counted on the bell, written
in .NET (`NeedsAPerson`) from the watcher's state read from memory, in every place the person-waits knowledge lists.

| Key | English | 中文 |
|---|---|---|
| `work.attention.quiet` | quiet after Daoris's help | 协助后仍无动静 |
| `work.attention.quietWhy` | {{session}} has said nothing for {{minutes}} minutes. Daoris told it once and carried it on once, and it is still quiet. | {{session}} 已 {{minutes}} 分钟没有任何输出。Daoris 提醒过它一次，也让它接着做过一次，它仍无动静。 |
| `work.attention.act.leave` | Leave it running | 让它继续运行 |

Its other acts are the existing *Hand over…* (转交…) and *Stop…* (停止…), and its door opens the session. *Waiting on you* (等你处理) is a park's
and is not used.

### 5.3 Settings and Ask Daoris

| Key | English | 中文 |
|---|---|---|
| `settings.driver.quiet.label` | Look in on a quiet session after | 会话无动静多久后查看 |
| `settings.driver.quiet.unit` | minutes | 分钟 |
| `settings.driver.quiet.why` | A session is never stopped for running long. One that says nothing and runs nothing for this long gets Daoris's help: a word, then a fresh start of the same work, then you. | 会话不会因为运行时间长而被停止。如果它这么久既没有输出、也没有在运行任何程序，Daoris 会协助它：先提醒，再让同一项工作重新开始，然后交给你。 |

Ask Daoris answers *how long may a session run?* with: as long as it works; Daoris looks in on one that has gone quiet.

## 6. The cap, accounts and spending

- **A quiet session keeps its slot** through every step, as CAPCOUNT1 counts it today: its line is still under way, its tree and quest held,
  and lending its slot would start a line the person's cap did not allow. At step 3 the slot stays held until the person's press; *Stop*
  frees it.
- **How a session is known to be working** is §1.2's signs. The cap reads none of them: it counts records, as before.
- **Accounts.** The carry-on runs on the account the walk gives, as any carry-on does (D125, D165, D197); a resume needs the same account
  (D131 point 2), and where that account is cooling the brief's new part takes the next. The watcher reads no account. A session silent
  because its account reached its limit is ended by the limit's own words, heard before Q.
- **No spending budget is added.** No wall-clock limit remains, and none replaces it. The spend of a stuck line is bounded by its account's
  allowance (D165, D197), by the strikes (D80: the watcher's ends count), by the watcher's one end per episode, and by the cap. A busy line
  that spends without progress is what §2.4 leaves. Any budget a later design adds parks the line at its next start for the person, and
  never ends a turn.

## 7. Proof

**A stand-in that can be told what to do.** No reusable stand-in can today be told to stream for a while, go silent, hang a call, background
work or exit without an end (each test writes its own script). SESSWATCH1a adds one, for the pipe door and the protocol door, told by its
target: stream a line every *k* seconds; go silent after its first line; open a call and hang on a child that waits on its stdin; background
a child that writes and computes, or one that sleeps; hold a permission request; exit leaving a grandchild holding the pipe. The DONEEND1
stand-in (`DoneEndTickTests.cs:107-143`) and the family rehearsal's pipe stub's cues (`tools/family-rehearsal.mjs:757-971`) are its exemplars.

**Clocks.** The watcher reads every bound off a handed `TimeProvider` and the job's CPU through a seam: the fast half moves the clock look by
look and hands the CPU it wants, as the held-clocks knowledge asks; the `Process` half runs real jobs. No test waits a bound out on the
machine's clock.

| # | Case | Half |
|---|---|---|
| 1 | A stand-in streaming past any old bound is left alone: lines every 5 s, the watcher's clock moved three hours in looks; no mark, word, note or end | fast; Process with a real stand-in and a moved clock |
| 2 | A silent one supported step by step: at Q the mark, `watch.quiet` and the urgent word kept on `said` with its key; at 3Q, never busy, the run ended `failed` with `ended.quiet`, the word withdrawn, a strike counted; the next look carries it on in a new part handed the brief, its instruction naming the quiet; that part silent from its start: at Q the `quiet` row, and each of its three presses; a line ended three times parks as `parked-quest` | fast; Process for the end and the carry-on |
| 3 | A background command left alone: a protocol stand-in backgrounds a child that writes and computes and ends its turn; past 3Q nothing; the child then sleeps: at 3Q the word as a turn of its own, then step 2 | fast; Process |
| 4 | A session waiting left alone: a held permission request, a call waiting for the heavy-run slot; the clock past every bound; nothing. And beside it: the person's words on their way at step 2's time bring the row, never an end; an approved hand-over waiting for a hung turn goes on at step 2's end | fast |
| 5 | A busy foreground call left alone: no event for 3Q, the job busy; nothing | fast; Process with a computing child |
| 6 | A process gone without an end: the root exits, a grandchild holds the pipe; within G the run ended, said and carried on | Process |
| 7 | A look more than two minutes after the one before starts the count over | fast |
| 8 | The watcher's cost is flat: 1, 10, 100 and 1,000 running sessions in its state; for the healthy ones a look makes no HTTP call, reads no file and no job (counted through the seams), one job read for each quiet one; the in-memory time recorded beside, never asserted | fast |
| 9 | The settings: `timeoutMinutes` kept as written through every save and never read for a bound (the keeps fixture), an older build's read of the file still finding 90; `quietMinutes` set, refused out of range and listed; the retired verb, door and tool refused with the pointer | fast; the setting-doors checks |
| 10 | The page: the notes, the rail's line and the row, in English and 中文 | vitest, stories, `i18n:check` |
| 11 | On the install: the real harnesses' idle CPU, the longest quiet in a working turn, and a session past 90 minutes left alone | the parent, SESSWATCH1d |

The tests that set `TimeoutMinutes` as a backstop (§0.3) take their own patience instead: a cancellation token on `Poll.Patience`, or the
watcher's moved clock where the case is the watcher's.

## 8. The rows

Each row is a feature a person can see working, built end to end (D187).

1. **SESSWATCH1a — no wall clock on a working session, and a quiet session seen** (driver, modules, web; first). `WaitAsync` no longer
   ends a driven session, an answered park's resumed run, a native go-on or an intake on time; the passes keep their own minutes, uncapped.
   The watcher's state (heard, busy, alive, the waits of §1.3) and its look, riding every look and `--once`'s wait for its sessions, on a
   handed clock; the quiet mark; the `quiet` row at 3Q quiet and never busy, with its three presses, so a stuck session still reaches someone;
   `timeoutMinutes` kept and unread, the old verb, door and tool retired with the words of §3; the stand-in of §7; the backstops replaced.
   Proof: cases 1, 4, 5, 7, 8, the settings half of 9, and 10 for the mark and the row. **Why first, and safe:** it removes the kill at
   once, and the safety the timeout gave (an end at 90 minutes) becomes a question to the person at 30 minutes of a session that neither
   said nor ran anything; until 1b the person, not Daoris, ends such a session.
2. **SESSWATCH1b — Daoris's support: the word and the carry-on** (driver, web; after 1a). Steps 1 and 2 of §2 in every state, state E's end,
   `ended.quiet` and its strike, the carry-on's instruction naming the quiet, an approved hand-over taken at the watcher's end, the row
   moved to step 3's condition and brought in step 2's place where the person's words wait; `LongCalls`' look folded into the watcher's. Proof: cases 2, 3 and 6; the notes in both languages.
3. **SESSWATCH1c — the quiet setting** (driver, service, web-settings; after 1a, beside 1b). `quietMinutes`, its verb, `driver list`, Ask
   Daoris's `quiet` door and `settings_quiet`, the help room and its goldens, the Settings → Driver field. Proof: the setting-doors checks and
   case 9.
4. **SESSWATCH1d — measured on the install** (the parent with the owner; after 1b and 1c). The real harnesses' idle CPU and the longest
   quiet in a working turn, read from the install's records; the bounds read again in a D200 note; a session past 90 minutes seen left alone.
5. **SESSWATCH1e — the passes' minutes, asked** (consider; D155, D163, D176, D177, D199). Whether a helper's, a reviewer's, a partner's or a
   meeting turn's minutes become the time its asker is told it is late while the watcher watches it, rather than an end.

## 9. What D200 amends, and what it rejects

**Amends:**
- **D80.** A cut-off's *timed out* becomes the watcher's end of a quiet run (step 2), counted as a strike as before; *the session timeout is
  set from a terminal* is retired. The carry-on is unchanged: a new part in the same tree handed the brief (D189), its instruction naming
  the quiet.
- **D188 point 1 and D193 part 9.** Among Daoris's own stops, *timeouts* becomes *the watcher's end of a quiet run*. **D188 point 2 and
  D193 part 9.** Daoris's urgent word gains a second reason: the watcher's one word to a session seen quiet, once per quiet episode.
- **D83's BGWAIT notes.** A background wait is bounded by the watcher's state C, not the timeout: work that never ends holds the session until
  it shows no progress for 3Q, then gets a word, then an end.
- **D131 point 5.** *A carry-on after a time-out* reads *after the watcher's end*.
- **The settings.** `daoris driver timeout` (D80), the `timeout` door (D110) and the `settings_timeout` grant (D183's HELPMODE1c3 note) are
  retired; `quietMinutes` and its doors are added.
- **D155, D163, D199.** Their passes' minutes are no longer capped by `timeoutMinutes`.
- **D184's held permission bound** keeps its 15 minutes, its reason now its own: a request nobody answers is refused so the turn goes on.

**These stand:** D46 §4, D51, D104's sweep, D105 §3, D124, D131 points 1-4, D132, D136, D137, D163, D165, D167, D184, D189, D24 and D130's
CAPCOUNT1.

**Rejected:**
- **A longer timeout, or one per repository.** D80 rejected it for a cut-off, and it still ends a working session at its bound.
- **An idle timeout** (an end after so many minutes with no event). A foreground command, a model's step on a door that streams nothing and a
  background test run are each silent and working; only the job's CPU tells them from a stall.
- **A model judging a session's health.** No model judges the work (D24, D54, D193), and its cost would grow with the sessions.
- **A process or a heartbeat per session.** A process costs per session; a heartbeat the agent must send is a self-report (D46 §4).
- **The keeping stop (`session/cancel`) as step 2.** It can land on the person's words on their way (D136 point 4, BGWAIT2's rejection), the
  native driven run has none (D188), a hung harness may not answer it, and D188 keeps it the person's press. STEER3's measures may bring it back.
- **Resuming the quiet session's own conversation at step 2** (§2.2): the strike would go with the reopened record, a line that hangs the
  same way could loop at a whole context each time, and a word of Daoris's alone opens nothing where the resume falls back.
- **A hand-over by Daoris at step 2.** It changes the maker unasked; D163 point 3 rejects automatic hand-overs by default.
- **A second end at step 3.** Daoris would decide the work is lost.
- **A token or spend budget now** (§6).
- **A machine-wide process query, a file read per session or the event file's tail per look** (§4.2).
- **Mapping the old 90 onto the quiet bound** (§3).
- **Telling busy work that goes in circles**, which needs a reading of the work. What is left: a busy line spends until its account, the
  strikes or the person stop it.

**What is left.** codex-acp's tool frames and background work, and the real harnesses' idle CPU, are unmeasured (SESSWATCH1d). A quiet
conversation is said and never helped. A session whose job could not be joined (best effort, `ProcessJob.Hold`) reads as never busy, so it is
judged by heard alone, which can end a silent working call at step 2; the row counts such sessions.

## 10. The reads

Codex's read (D191) is owed; this section gains its findings, each checked against the code, and the verdicts.
