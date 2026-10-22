# A terminal's standing confirmation: the person's one press for named management presses (TERMTRUST1, 2026-10-23)

**Status: proposed (D201), round 2.** Written by Claude Opus from the code at `f506863e7`; revised after Codex's read (D191), whose twelve
findings (eight blocking) were each checked against the code and taken, and after the author's own check found one more and measured
one fact (§11). Nothing is built. The design changes a decided rule other designs rest on (D156 point 4, as RPC1c and CONFIRMSEEN1 built
it) and adds a containment every agent start rests on, so it asks for Fable's foundation read (D191's amendment).

This design serves two parts of D193's company: **management** (the steward runs the owner's install for development and testing, and
should not stop at the owner's desk for each routine act) and **the owner's attention** (D37: the person sets the target and verifies
the outcome; a card per routine stop spends their attention where no judgement is asked, and teaches them to press Confirm unread).

## 0. The case, and what is true today

### 0.1 The direction, in substance

On 2026-10-23 the owner set the direction: the steward, an agent the owner runs by hand, drives the owner's local install for
development and testing, and stopping or finishing sessions from a terminal should not need the owner to press Confirm in the window
each time. The steward set the shape: an agent never bypasses the confirmation; the person grants a standing yes once, in the window,
with their own press; it names management presses only (stop or finish a session, hold or resume driving, close a quest as done); it
never covers words said as the person, go-aheads, sign-ins, access or permission widenings, a landing's push, or a setting that widens
what an agent may do; each use is logged and said on the record it acted on; it is revocable at once; it holds on this machine alone and
no sync carries it.

**Its name.** The glossary's *trust* (信任) is an agent's permission to work in a folder (`glossary.json:622`), so the window calls this
the **standing confirmation** (长期确认): a confirmation the person gives once for a named set of presses, as a *standing answer*
(长期答复, `glossary.json:483`) is an answer given once for every session of a repository. TERMTRUST stays the rows' id. The glossary's
*terminal* (终端) means the panel's shell today (`glossary.json:641`); this design also means a terminal outside Daoris, and the row
widens its meaning (§6.3).

### 0.2 How a terminal's press reaches the window today

- **The verb sends its route over the pipe.** `daoris-driver sessions stop|finish …` sends `STOP_SESSION` or `RESOLVE_SESSION` with
  the route's fields beside `said: true` (`Sessions/SessionsCommand.cs:416-424`; `InstanceDoor.Said` at `Platform/InstanceDoor.cs:328-338`);
  `daoris driver hold|resume <repository>` sends `SET_HOLD` `{ repository, held }` as it is (`Platform/DriverChoicesCommand.cs:371-402`,
  `ChoiceRoutes.Hold` at :76, `InstanceDoor.PressAsync` at :314). Each is named in `PersonPresses` (`Platform/InstanceRoutes.cs:156-305`)
  or `ChoiceRoutes`, held to the census by `PipeCensusTests`.
- **The census classes it.** `PipeCensus.Driver()` (`Platform/PipeCensus.cs:182-275`, modules) makes `STOP_SESSION`, `CANCEL_TURN`,
  `WORK_PAUSE` and `WORK_RESUME` (:208), `RESOLVE_SESSION` (:218) and `SET_HOLD` (:236) the person's; a route a route-by-route module
  does not name is the person's too (:131).
- **The pipe asks the window.** `InstancePipe.Accept` (`Platform/InstancePipe.cs:498-609`) reads the class (:519), lists every value a
  person's press carries or refuses it `shape` (:538-543), binds the id, then `Confirmations.Ask` (:586) holds it in memory, tells the
  terminal `DAORIS.PIPE` `CONFIRMING` (:606, :612-620), and runs it only when the person's answer is *Confirm* (`ConfirmThenRunAsync`,
  :626-668), through the journal and inside `PipeDoor.Enter()` (`RunAsync`, :688-723; `PipeDoor` at `Platform/RefusalLog.cs:78-100`).
  Five wait at once; each waits ten minutes to be found and two once shown (`PipeConfirmations.cs:116-133`, :187-224).
- **The window alone answers.** `PERSON_CONFIRM`, `PERSON_CONFIRM_SHOWN`, `PERSON_CONFIRMATIONS` and `PERSON_KEY` are `Window` in the
  census (`PipeCensus.cs:250-260`), so a terminal is refused before dispatch; the two that answer also refuse the terminal's door in their
  handlers (`Bridge/Routes/PersonRoutes.cs:45-48`, :63-66). The card sits in *Notifications* (D196, NOTIFY2b).
- **The terminal says it waits.** `InstanceDoor.Told` (`InstanceDoor.cs:422-439`) prints *waiting for you to confirm this in Daoris's
  window…* on the error stream at `CONFIRMING`; a refusal or the two minutes' end is exit 1.
- **The asker** is read once as a connection is taken: its process's name and id (`InstancePipe.cs:838`, `AskerOf` at :1025-1047), no
  handle kept.
- **The log** says `person.confirmation` with `state`, the route and `door: terminal`, never a value or the asker
  (`PipeConfirmations.cs:387-388`).

### 0.3 The presses in question, and what each does today

| Press | Verb | Route and form | What it changes | Undone by |
|---|---|---|---|---|
| Stop a session | `sessions stop <id>` | `STOP_SESSION { id, said: true }` | its process ended as the person's stop: a driven session's record `stopped`, its quest held here until *Try again* (`SessionsCommand.cs:877-923`, `SessionMoves.cs:18-27`; D126 §3.3; `Planner.cs:254` reads the state and `interrupted`, never the note); a parked one with no process ended unanswered through the ledger; an orphan's record ended. A stopped run whose quest its agent already closed done, or submitted under a closure graph, is due for an automatic landing under the repository's rule exactly as its own end would have made it (`AutoLanding.cs:202-207`, `Driver.cs:2373`, :2436) | the person's *Try again* (D126 §3.4), which the work's tree waits for |
| Finish a session | `sessions finish <id>` | `RESOLVE_SESSION { id, state: "completed", said: true }` | the verb's path reads the record, refuses one not `awaiting-person` and any intake (`SessionsCommand.cs:925-940`), then either asks the loop through the request folder, which does not read the state again (`SessionRequestWatch.cs:331-345`), or advances the ledger, which lets `working` become `completed` too (`SessionLedger.cs:1726-1738`); *The person finished this at a checkpoint* (`SessionMoves.cs:42-61`); its quest stays as it was (D126's QUESTCLOSE1 note) | nothing: the record is ended, and its quest sits until the person marks it done |
| Hold a repository | `driver hold <repository>` | `SET_HOLD { repository, held: true }` | `driver.json`'s `holds` gains it: still drivable, no new start (`DriverRoutes.cs:54-62`, `DriverConfig.cs:1384-1387`), with no record of who held it | `resume` |
| Resume a repository | `driver resume <repository>` | `SET_HOLD { repository, held: false }` | the hold gone: the driver may start that repository's quests again, under every other standing choice | `hold` |
| Close a quest as done | `quest done <id>` | not the pipe: the service's `POST /api/quests/{id}/done` (`QuestDoneCommand.cs:45-51`; `Program.cs:703-718`) | the quest `Done` on the person's word, its requirements answered by none, a chain's next step published, a quest waiting on it resumed (`QuestExchange.cs:2233-2305`); under a closure graph it is the person's override, its pending nodes stopped, and a step a source's reopen held closes too, since only an agent's done is refused there (`Quests.cs:1742-1752`; D173's notes); it travels as a done (D68) | nothing: a closed quest does not move (D46 §3) |

**`quest done` waits for nothing today**: it is one of the verbs that stay alone at the service's door (`InstanceRoutes.cs:138`), and the
service gates that door only once a starter hands it a key, which no shell does before PERSONDOOR1g (D156's PROVED1 note; `TASKS.md`
PERSONDOOR1d, 1g, 1i open). Any terminal, a session's among them, closes a quest as the person today.

### 0.4 Where a standing choice lives, and who can write it

- **`data/driver.json`** holds the person's standing choices (`DaorisHome.cs:326`). Its writers are the window's `SET_*` routes, the
  terminal's `daoris driver` verbs (alone or through the pipe), Ask Daoris's `setting` doors and its `settings_*` grants (D183), and
  any process of the account editing the file; `DAORIS_DRIVER_CONFIG` lets those verbs read and save any file named (`DriverConfig.cs:738`).
- **`data/configured-access.json`** is the precedent for a grant of the person's: Ask Daoris's configured access, written only by
  `SET_HELP_GRANT`, a `Person` route, with no terminal door (D183's HELPMODE1c2b note; `DaorisHome.cs:353`).
- **No file under the home is the person's alone.** D156 rejected a file with an ACL or sealed with DPAPI for the key, since any
  process of the account reads it, and leaves the home's files to the account's reach (D156 point 7).

### 0.5 What an agent reaches already, without any grant

- **The request folder.** Every loop on the home takes a stop, a finish, a decline or words from `run/requests` and records the move
  as the person's (`SessionRequestWatch.cs:3-37`, `SessionRequests.cs:11-58`); any process of the account can write that folder.
- **The home's files.** `driver.json`'s `holds`, and every other standing choice, are a file edit away (D156 §7).
- **The service's doors.** Ungated until PERSONDOOR1g (§0.3).

The pipe's card defends against an agent running Daoris's own verbs, its ordinary reach (D156 §1). It never defended against the
account's reach, which PERSONDOOR2 designs. Nothing here relies on that gap staying open, and nothing here widens it.

### 0.6 Where an agent runs, and what a job tells

- **One start gate.** Every process the driver library starts goes through `ProcessStarts.Shared.StartAsync`
  (`Platform/ProcessStarts.cs:48`): the driver's one run, `Driver.HoldAsync` (`Driver.cs:3127-3147`), which the driven sessions,
  intakes, helpers, reviewers, partners, brainstorm passes and carry-ons go through; the carry-ons' own starts (`DriverContinuations.cs:970`,
  :1023); the conversations (`ChatRunner.cs:1180`); a plugin's process (`HookProcess.cs:144`, `PluginTrialProcess.cs:43`); a sign-in or an
  install (`HarnessActions.cs:479`, :546); a usage read (`CodexUsage.cs:334`); git (`WorkingTree.cs:1106`, `GitIdentityActs.cs:431`). A
  partner's launcher starts its harness itself, inside its piece's named job (`PieceLauncher.cs:84`, `PieceJobs.cs:17`).
- **Joined after it starts.** An agent's process is joined to a kill-on-close job that allows no breakaway (`ProcessJob.cs:6-27`) by
  `SessionProcesses.Track` (`SessionProcesses.cs:372-378`), after the start: at `Driver.cs:3147`, four lines on, and at `ChatRunner.cs:1980`,
  after the conversation's handshake. A child the harness starts before the join is outside the job, which `HeavyStep.cs:13-17` names as
  the reason a heavy gate is started suspended and joined first; the terminal panel's shell is started the same way (`PseudoConsole.cs:18-21`).
- **Every process here is in some job already.** Measured on the install's machine on 2026-10-23 with `IsProcessInJob(process, NULL)`:
  `explorer.exe`, Windows Terminal, and the steward's own process chain each answered *in a job*. So "in no job at all" tells nothing; only
  membership of a job Daoris made for an agent does.
- **One home's view.** The registry knows its own sessions' jobs and, by markers, sessions another process on the same home runs
  (`SessionProcesses.cs:389-397`); it knows nothing of another home's agents, whose instance serves a pipe of its own name
  (`InstancePipe.cs:234`).

## 1. The decision in one paragraph

The person turns on a **standing confirmation** in the window, with their own press, on Settings → This Daoris. From then, while
Daoris runs, a terminal's press that the confirmation names, sent in exactly its verb's form, from a process that is in **no job any
Daoris on this Windows session made for an agent**, runs without a card: the pipe admits it as a confirmed press, tells the terminal so,
writes `person.confirmation` with `state: standing`, and the record it acted on says it was sent from a terminal under the standing
confirmation. Every agent any Daoris starts is held in such a job from its first instruction (TERMTRUST1a's first part), so a session's
own shell and everything it starts never ride the grant. It names four presses now: stop a session, finish a parked session with no
process here and no words of the person's, hold a repository, and resume a repository a terminal held under it during this run;
TERMTRUST1b adds closing a quest as done in a narrow form, once that press is on the pipe. Everything else asks as today. The person turns
it off at once from the same row or from *Notifications*. Its record is a file of its own under `data/`, bound to this machine and
account, written only by a route the census makes the window's, never carried by a sync.

## 2. The presses it names

### 2.1 The table

Each press is a route and a **form**: the payload's every name, as the bridge reads names (ignoring case, each once: `FieldsOf` already
refuses a name twice, `PipeConfirmations.cs:250-303`), is one the form allows, of the form's type and value. Any other name or value, a
later field included, is not the form, and the press asks as today. Each form is held to its verb's own payload by a test that sends the
verb through the stand-in instance (§8).

| Name | Route | Form and conditions | Why it is safe to name | Taken back by |
|---|---|---|---|---|
| `stop` | `STOP_SESSION` | `{ id: string, said: true }` | It only ends work: the record concludes `stopped` as the person's stop, which holds its quest until *Try again* (D126 §3.3), never a strike (D58), a done or a carry-on. It makes no work due for landing that the agent's own done did not already make due at its end (§0.3); one not done is never due | the person's *Try again*, the tree kept |
| `finish` | `RESOLVE_SESSION` | `{ id: string, state: "completed", said: true }`, no `note`; and no process for the session on this machine (`AliveOnThisMachine` false) | It ends a session that already waits on the person, with nothing of it running, and writes none of their words. `said: true` runs the verb's path, which refuses an intake and a record not waiting; the move is then the ledger's alone, made **only from `awaiting-person`**: the advance gains an expected state that the store compares in the same transaction (a resume that moved it to `working` meanwhile is refused, nothing ended). A parked session whose process still lives here asks | not by a press: the quest sits until the person marks it done (QUESTCLOSE1), the tree and the conversation kept for *Start from* |
| `hold` | `SET_HOLD` | `{ repository: string, held: true }` | It only stops new starts in one repository; what runs runs on | `resume` |
| `resume` | `SET_HOLD` | `{ repository: string, held: false }`, the repository one a covered `hold` held during this run of Daoris and no other press has touched since | It undoes only a hold a terminal made under the confirmation in this run; a hold the person made, one made alone, and every hold from before this start ask. It changes no permission, cap, strike count, landing rule or drivability | `hold` |
| `done` (TERMTRUST1b) | `QUEST_DONE`, a new route (§9) | `{ quest: string, said: true }`, no `note`; judged **in the service's own transaction**, not by a snapshot: the quest open or taken, bound to no graph version and under no closure graph, neither held nor withdrawn by a source's reopen, no session of it queued, starting, working or waiting on the person on this machine, and its last session here the one the relay names, ended `completed` by a finish | It closes what a finish left sitting, in the one case QUESTCLOSE1 built the person's done for. It writes none of the person's words; its note says it came from a terminal under the confirmation. A graph's override and a reopen's hold stay the person's per press (D173) | nothing: a closed quest does not move |

**Why `done` waits for its own row.** Its door is the service's (§0.3), which no pipe judge sees and which waits for nothing today. To be
judged where every other terminal press is judged (its terminal's process placed, §3.3), TERMTRUST1b moves the verb onto the pipe as a
`Person` route the instance relays with its key; until then the verb keeps acting alone, and the standing confirmation changes nothing
for it. Moved onto the pipe, a `quest done` that is not covered (with a note, say) waits for the card where today it waits for nothing:
stricter, as D156 meant. It is the one press in the set that is not a narrowing: it ends a quest for good and lets a chain go on.

### 2.2 What it never names

| Press | Why not |
|---|---|
| `sessions say`, `take-back`, `reword`, `channel post`, `ask`, a finish or done with `--note`, `decline` (its reason) | words said as the person (D156 §3) |
| `sessions stop --turn` (`CANCEL_TURN`) | it sends a driven session's held words as its next prompt now (`ConversationRoutes.cs:286-291`): a choice of when the person's words are read, not a stop |
| a go-ahead's answer, a helper's or a hand-over's yes or no, a held tool call's answer, an answer alone let go, `WORK_HELD`, a workflow node's skip or stop | answers the person gives to someone (D133, D135, D163, D184, D188) |
| *Try again* (`RETRY_QUEST`), `WORK_RESUME` | each releases the person's own stop or forgives strikes (D58, D126 §3.4); this confirmation undoes only what it did |
| `WORK_PAUSE`, `WORK_ABANDON` | an ask's whole work across repositories, and an abandon discards: not in the steward's set, and the second destroys |
| landings, hand-offs, discards, sweeps, clears, deletes, archives, `TREES_SYNC`, `SYSTEM_SPACE_FREE` | a yes to work, a push through a plugin, or a delete (D37, D145) |
| every `SET_*` but `SET_HOLD`, `SET_DRIVABLE`, a trust, a rule's answer, an account, a tool, a plugin, `SETUP_PRESS` | standing choices one direction of which widens what an agent may do (D72, D74; the census's own reason, `PipeCensus.cs:75-81`) |
| sign-ins and account actions (`HARNESS_ACTION`, `ACCOUNT_USE`, `HARNESSES_REFRESH`), `ASK_OPINION` | a sign-in, or work on the person's accounts |
| `END_CHAT`, `STOP_TASK`, `STOP_OPINION` | no terminal verb sends the first two; a reviewer's stop is the person's word on a second opinion |
| the standing confirmation itself | §4.1 |

### 2.3 A grant names its presses

The record keeps the names the person granted (§4.2). A press is covered only where the build's table names it **and** the record names
it, so a later build that adds a press (TERMTRUST1b's `done`) covers nothing until the person grants it; the window offers it then with
the others. The person may grant fewer than the build offers: each press is a tick in the grant's ask, all ticked at first.

## 3. Who it covers: the threat model

### 3.1 What the card defends today

A session runs as the person's account, so it can run `daoris-driver`, open the pipe and send any press; nothing a binary reads tells
whose run it is (D156 §4.1; the terminal RPC design §4.2). The card makes every such press the person's decision, its default no. That
holds against an agent's ordinary reach (its commands, its tools, what its environment and tree hand it), not against the account's reach
(§0.5).

### 3.2 What the standing confirmation lets through

Exactly the presses of §2.1 in their forms and conditions, from a process in no job any Daoris on this Windows session made for an agent,
while the record says it is on. An agent the person runs by hand outside Daoris is such a process: the steward is one, and that is the
purpose. The grant's ask says so in plain words (§6.1).

### 3.3 What bounds it

1. **The presses.** Each only ends work, ends a session that already waits on the person with nothing of it running, stops new starts,
   or undoes a hold the confirmation made in this run. None says words as the person, answers anyone, makes work due that was not,
   pushes, deletes, widens what an agent may do, releases the person's own stop or hold, or starts work the person's standing choices did
   not already allow. `done`, from TERMTRUST1b, is the exception §2.1 says, its conditions judged where the move is made.
2. **The forms.** Only the verb's own payload; anything more asks.
3. **The caller: never an agent any Daoris runs.**
   - **Contained from the first instruction** (TERMTRUST1a's first part). `ProcessStarts` gains a contained start: the process created
     suspended, joined to its jobs, then let go, as `HeavyStep` and `PseudoConsole` already start theirs. Every agent start (§0.6's driven
     run, carry-ons and conversations), a plugin's process (a performed step runs a repository's own commands) and a partner's piece use
     it; a start that cannot be contained is not made, as a heavy gate that cannot join is not run (`HeavyStep.cs:15-17`).
   - **One named job per Daoris process that runs agents.** Each such process (a desktop instance, a headless loop, a terminal's
     `chat`) makes `Local\daoris-agents-<home id>-<process id>` at its start, empty, and every agent it starts is joined to it first, then
     to its own kill-on-close job, which nests inside. The judge finds every job of that prefix in this Windows session's object
     namespace, its own home's and every other home's, and asks each whether the client's process is in it (`IsProcessInJob` with that
     job's handle, never `NULL`, §0.6).
   - **The client, by a handle.** The pipe opens the client's process as it takes the connection (`GetNamedPipeClientProcessId`, then a
     handle with its creation time) and keeps that handle for the connection's life; a process that would not open, or was created
     after the connection was taken, is unknown.
   - **The answer**: in any of those jobs, not covered (it asks, and the card names the terminal as today); in none, with every job
     found readable and every agent this process runs contained from its start, covered; anything the judge cannot tell (a job it cannot
     open or read, the namespace not read, an agent of its own whose containment failed, the client unknown, not Windows), not covered,
     and said why.
   This only ever adds a card. Being in none of those jobs never makes a caller the person (D156 §2.4 rejected an OS identity): the
   person's grant covers the press, and the jobs only take the cover from an agent.
4. **A session's own record cannot be stopped into anything.** A stop of itself concludes `stopped` as the person's stop, which holds its
   quest for the person's *Try again*: no strike escaped into a done, no carry-on, no landing its own done had not made due. A finish needs
   a record waiting on the person with no process here, moved only from that state. A hold stops starts. And by point 3, a session's own
   process tree, its background work included, is not covered at all.
5. **Admitted once.** The judgement is made under the judge's lock and admits the press there; a revoke after it does not recall it, and
   a covered `hold`'s memory of what it held is written under the same lock, so no press restores an earlier grant (§4.3).
6. **Said, every time** (§7): the log, the record, the terminal and the window's line.
7. **Revocable at once, and named** (§4.3, §2.3).

### 3.4 What it does not bound

- **An agent the person runs by hand outside Daoris** rides it, by design; the grant's ask says so.
- **Code an agent wrote that Daoris itself runs outside an agent's job.** A repository's git hooks run under Daoris's own git acts (a
  landing's commit in a session's tree; no `--no-verify` or `core.hooksPath` is set, read at `f506863e7`), and a sign-in's or a usage read's
  process: these are Daoris's starts, not an agent's, and are in no agent job. A plugin's process is contained (point 3). What holds there
  is the presses' own bounds (points 1, 2 and 4).
- **An older build.** A Daoris from before TERMTRUST1a makes no named job, so its agents are in none: while one runs agents on this
  Windows session, its agents are covered like any terminal. Said, not detected.
- **The account's reach** (D156 point 7, PERSONDOOR2): an agent that writes the record while Daoris is down is read at the next start
  (§4.2); one that starts a process outside its job through a system service (the task scheduler, WMI, a COM server) is a caller in no
  agent job; one that makes a job of the prefix and puts the person's terminal in it takes the person's cover away, which asks; UI
  automation of the window, a development run's debug port and the request folder (§0.5) reach the same acts with no grant at all. The
  standing confirmation adds no act the account's reach lacks today.
- **Denial.** A covered caller can stop or finish the person's sessions and hold repositories. Nothing is lost by it: trees, branches
  and conversations are kept, a stopped quest waits for *Try again*, a finished one sits, and each use is said where the person looks.
- **The service's doors** stay ungated until PERSONDOOR1g, whatever this says.

## 4. Granting and revoking

### 4.1 Only in the window

- **One route, the window's**: `SET_STANDING_CONFIRMATION { presses: [...] }`, the presses ticked, `[]` to turn it off. The census
  classes it `Window` (`PipeCensus.cs:250-260`'s group), so a terminal is refused `window-only` before it is dispatched, and the handler
  refuses `PipeDoor.Terminal` as `PERSON_CONFIRM` does (`PersonRoutes.cs:45-48`). A terminal cannot even ask for a card that grants it: a
  person used to confirming a terminal's presses would confirm that one too.
- **No door of Daoris's.** No verb has a word for it; Ask Daoris has no `setting` door for it and no `settings_*` grant;
  `HelpCoverageTests` holds the page's hook `Exempt` with this reason (bridge-presses row 7). A `daoris driver` verb pointed at its file
  by `DAORIS_DRIVER_CONFIG` loads and saves it as a `driver.json` (`DriverConfig.cs:738`), which keeps `presses` as it found it and can set
  nothing this record is read for; that is the account's reach over the file, not a door.
- **The grant asks once, inline** (the platform language's widening ask, as full access asks, the Ask Daoris modes design §5): it
  lists the presses with their ticks and says who can use them (§6.1). Revoking asks nothing.
- **Never from a card.** No confirmation card offers *don't ask again*: the grant is made where the person sets things, never in front
  of a press a terminal raised, which may be an agent's (D196's rejection of *Confirm all* stands).

### 4.2 The record

`data/standing-confirmation.json`, a new entry `standing-confirmation` in D172's home table (`DaorisHome.cs:324-399` and its two twins,
the service's `HomeEntry` and the CLI's `home.ts`), category `Data`, its keep rule *kept* in D195's inventory:

```json
{ "schemaVersion": 1, "presses": ["stop", "finish", "hold", "resume"], "since": "2026-10-23T06:05:00Z",
  "machine": "<SHA-256 hex of this machine's name and the account's SID>" }
```

- **Written only by the grant's route**, atomically, as the home's records are. The CLI's twin names the path and never reads it.
- **Read** by the instance as its loop takes the home, before the pipe serves (`InstancePipe.Serve`), and kept in memory; written through
  on each grant and revoke. A write by another process while Daoris runs is not read until the next start.
- **Bound to this machine and account.** A record whose `machine` differs (a `data/` folder copied elsewhere) is not honoured, is said,
  and is left; the window's next grant writes anew. Absent, unreadable or of a newer `schemaVersion`: off, said, never written over by an
  older build (as `Notices.cs` treats a newer record).
- **What `resume` reads is memory, never this file.** A covered `hold` that changed a repository from not held to held is remembered by
  the instance, with that hold's moment; any other change of that repository's hold (the window's press, a confirmed press, the alone
  path, a file edit read at a look) forgets it, and a start remembers nothing. A file could not say who held a repository after a person's
  `resume` and `hold` made alone while Daoris was down, since both files would read as before (Codex's finding 5).
- **Never carried.** The sync carries the store's operations (D68), never a home file, and no code but the instance's reads this one; the
  grant itself is on no quest, ask or session record (the note part of §7.1 says a use, never the grant). Copying `data/` to another
  machine carries the file, which that machine does not honour.

### 4.3 Revoke at once

Turning it off clears the instance's memory first, under the judge's lock, then writes the record: a press judged after it asks; one
admitted before it runs. A revoke whose write fails still turns it off for this run and says so (*turned off; its record could not be
written, so the next start may read it as on*); a grant whose write fails turns nothing on. A press waiting on a card when the grant
changes keeps its card: a grant confirms no press already asked.

## 5. A press under it, step by step

1. The verb sends its route over the pipe as today (`InstanceDoor.PressAsync`).
2. `InstancePipe.Accept` classes it `Person`, lists its values (:538-543) and binds its id, as today.
3. **New, before `Confirmations.Ask` (:586):** `StandingConfirmation.Judge(module, route, payload, connection)` (modules, `Platform/`),
   under its lock, answers *covered*, or *asks* with why: `off`, `not-named` (no press of the table or the record), `form`,
   `not-held-here` (a resume of a hold this run's memory does not hold), `running-here` (a finish of a session with a process here),
   `in-agent-job`, `untold`.
4. **Covered:** the pipe writes `person.confirmation` `{ state: "standing", route, door: "terminal" }`, tells the terminal `DAORIS.PIPE`
   `STANDING` `{ id }`, and runs the press through `RunAsync` (journal, `RESULT`, the dispatcher) inside `PipeDoor.EnterStanding()`, a door
   the handlers read as `PipeDoor.Standing`. The route writes the record's mark (§7.1) and answers its `said` lines as today; a finish
   sends the ledger its expected state. Every window is told `STANDING_USED` `{ seq, at }`, which the line in *Notifications* counts.
5. **Asks:** as today, the card; `CONFIRMING` gains `standing` (the why, where the grant is on and names the route), so the terminal says
   *waiting for you to confirm this in Daoris's window (its standing confirmation does not cover a press from inside an agent Daoris
   runs)*; the `asked` line gains `standing` with the same code.
6. Once the shell holds the person key (PERSONDOOR1g), a route the pipe runs under the standing confirmation presents it to the service
   as a confirmed press does (the terminal RPC design §4.3).

## 6. What the person sees

### 6.1 Settings → This Daoris → *A terminal*

A row beside the launchers and the address file (the This Daoris design §3.7):

| Part | en | zh |
|---|---|---|
| Label | Standing confirmation | 长期确认 |
| Hint | Some presses from a terminal run without asking you. | 终端的部分操作无需问你即可执行。 |
| Off | Off: a terminal's presses that need you wait for your confirmation | 关闭：终端中需要你确认的操作会等你确认 |
| On | On since {{since}}: {{presses}} (the granted ones only) | 自 {{since}} 起开启：{{presses}}（仅已允许的） |
| `stop` | stop a session | 停止会话 |
| `finish` | finish a session that waits on you, without your words | 完成一个在等你的会话（不附你的话） |
| `hold` | hold a repository | 暂停一个仓库 |
| `resume` | resume a repository a terminal held | 恢复由终端暂停的仓库 |
| Buttons | *Turn on…*, *Turn off* | *开启…*，*关闭* |

*Turn on…* asks once, under the row (`InlineConfirm`), with each press ticked:

- **en:** *Let a terminal on this machine do these without asking you each time. Daoris cannot tell whether you ran a terminal's press
  or an agent did, so an agent you run yourself outside Daoris can do them too; the agents Daoris runs cannot. Everything else still asks
  you. Each one is logged and said on what it acted on. Turn it off here or in Notifications at any time.* Buttons **Turn on** and
  **Never mind**.
- **zh:** *允许本机的终端无需每次问你即可执行这些操作。Daoris 无法分辨终端的操作是你运行的还是智能体运行的，所以你自己在 Daoris
  之外运行的智能体也能执行；Daoris 运行的智能体不能。其他一切仍会问你。每一次都会记入日志，并写在它作用的对象上。你可以随时在这里或在通知中关闭。*
  Buttons **开启** and **取消**.

A record not honoured is said under the row: *Off: its record is from another machine* / *关闭：它的记录来自另一台机器*; *Off: its
record could not be read* / *关闭：它的记录无法读取*; *Off: its record is from a newer Daoris* / *关闭：它的记录来自更新版本的 Daoris*.

### 6.2 Where it is used

- **Notifications**, at the head of the presses' group while it is on (NOTIFY2b's view): *These presses from a terminal run without
  asking you: {{presses}}. Standing confirmation since {{since}}, used {{count}} times since Daoris started.* with **Turn off** and *Open
  This Daoris* / *终端的这些操作无需问你即可执行：{{presses}}。自 {{since}} 起长期确认，本次启动以来使用 {{count}} 次。* **关闭**，*打开本机*.
  `{{presses}}` names the granted presses only. It is a line, not a row of *What needs you*: nothing waits on the person.
- **A session's record** (its page, the trace and the terminal's `sessions` words) shows the note part of §7.1 in the reader's language.
- **A repository's hold** made under it says *held from a terminal, under your standing confirmation* / *由终端按长期确认暂停* beside the
  hold, where the hold is shown and released, for as long as this run remembers it.
- **The terminal** says, on the error stream, *ran under your standing confirmation; nothing was asked in Daoris's window* (English, as
  the terminal speaks).

No balloon, toast or badge is raised for a covered press: the person granted it so as not to be asked, and the line and the record say it.

### 6.3 The glossary

A term *standing confirmation* / 长期确认 (*the person's one grant in the window that lets named presses from a terminal outside Daoris's
agents run without a card, on this machine, until they turn it off*), and *terminal*'s meaning widened from the panel's shell to *a shell
of the person's own, in the panel or outside Daoris*.

## 7. The record and the log

### 7.1 On the record it acted on

- **A session.** One coded note part, `standing.terminal`, appended after the move's own note (a stop's *the person stopped it*, a parked
  stop's, an orphan's, a finish's *The person finished this at a checkpoint*): en *Sent from a terminal and run under the person's standing
  confirmation, without asking.* zh *由终端发出，按长期确认执行，没有逐次询问。* The move's own note stays: it is the person's stop or finish,
  by their grant, as D156 point 5 counts a confirmed grant. The part is a fact with no value (`NoteCodes.cs`, a new code; `note.json` in
  both catalogues). The flag reaches each path as the route's `PipeDoor.Standing` handed into the verb's ask and its `SessionRequest`, so a
  stop the loop takes from the folder writes it too.
- **A quest** (TERMTRUST1b): the service's done note keeps *The person marked this done.* and adds the coded part `person-done.standing`
  with the same words, from the relay's standing form; before PERSONDOOR1g any caller could send that form, and it only weakens the claim
  while the service's own conditions still bind it.
- **A hold**: the instance's memory of it (§4.2), shown with the hold.

### 7.2 The machine log

| Event | Fields | When |
|---|---|---|
| `person.confirmation` | `state: standing`, `route`, `door: terminal` | a covered press, as it is admitted |
| `person.confirmation` | `state: asked`, `route`, `door`, `standing` (`form`, `not-held-here`, `running-here`, `in-agent-job`, `untold`) | a press the grant names that asked anyway; no `standing` field while it is off or names no such press |
| `person.standing` | `state` (`granted`, `revoked`, `found`, `unread`, `newer`, `other-machine`, `unwritten`), `presses` | a grant or revoke in the window; at the start, what the record said |
| `agent.contained` | `kind` (`agent`, `plugin`, `piece`), `contained` (`true`, `false`), `why` | a start the contained start could not contain, which is not made (TERMTRUST1a's first part) |

Codes only, as D156 §5.1 has it: never a value, an id, a repository, the asker's name or its process.

## 8. Proof

Each case seen failing first, as `held-clocks.md` and `bridge-presses.md` ask. Fast halves unless named.

1. **The judge** (`StandingConfirmationTests`, modules): each press in its form from a caller in no agent job is covered; each near miss
   asks with its code: a finish with `note`, without `said`, with `state: "declined"`, of a session with a process here; a stop with an
   extra field; a name twice in two casings; `CANCEL_TURN`, `WORK_RESUME`, `RETRY_QUEST` and every other `Person` route (a census sweep);
   a resume of a repository this run did not hold, of one held before the start, of one the window held and released since; the grant
   off; a grant naming fewer presses; a build press the record does not name.
2. **Containment** (`ProcessStartsContainedTests`, driver, Windows, `Process` category): a stand-in harness that starts a child in its
   first instruction leaves that child in its session's job and in the named job; a start that cannot join is not made and logs
   `agent.contained`; every start site of §0.6 classed (contained, or Daoris's own with its reason), a census that fails on a new site.
3. **The caller** (`StandingCallerTests`, modules, Windows): a child of a contained stand-in asks (`in-agent-job`); a process outside
   every job of the prefix is covered; a job of another home's prefix found and read (two homes, two named jobs); a job of the prefix
   that cannot be opened, a namespace not read, an own agent whose containment failed, a client gone and a client process created after
   the connection each ask (`untold`); the terminal panel's job is no agent's.
4. **Over the real pipe** (`PipeConfirmTests`): a covered stop runs with no `CONFIRMING` and the terminal hears `STANDING`; its journal
   answers `RESULT`; a revoke between the judgement and the run lets the admitted press run and makes the next ask; a press asked before a
   grant keeps its card; the log lines of §7.2 and none of a value.
5. **The forms against the verbs** (`StandingFormTests`, driver and modules): `sessions stop`, `sessions finish` and `driver hold|resume`
   sent through `InstanceStandIn` are covered as they send them, so a verb that grows a field fails here.
6. **The finish's race** (`SessionLedgerTests`, service; `SessionsCommandTests`, driver): an advance with an expected `awaiting-person`
   refused once the record is `working`; a covered finish of a session resumed between the verb's read and the move ends nothing.
7. **The landing** (`AutoLandingRulesTests`, driver): a covered stop of a session whose quest is `Done`, and of one whose closure graph
   took its submission, is due exactly as its own completion would be; one not done is never due.
8. **The grant's route** (`DriverModulePersonTests`): `window-only` over the pipe; the handler's own refusal of `PipeDoor.Terminal`; the
   record written and read back; a press the build does not name refused as data; the revoke's memory cleared before a failed write;
   `PipeCensusTests` holds it `Window`, `StatePublisherTests` its mark.
9. **The record** (`StandingRecordTests`, driver): another machine's, an unreadable one, a newer one each off and left; `DaorisHomeTests`
   in the three twins.
10. **The note** (`SessionsCommandTests`, `SessionRequestWatchTests`, `NoteCodesTests`): a covered stop of a live, a parked and an
    orphaned session, and a covered finish, each keep the move's note and add `standing.terminal`; `Planner` still holds the stopped
    quest (`PersonStopped`); the code worded in en and zh.
11. **The page** (web): the row off, on and not honoured; the inline ask's ticks and words in en and zh; *Turn off* in the row and in
    *Notifications*; the granted presses only named; the hold's mark; `i18n:check` and `names:check -- --strict` with §6.3's terms;
    `HelpCoverageTests`' `Exempt` row.
12. **TERMTRUST1b**: `QUEST_DONE`'s places (bridge-presses rows 1 to 11), the verb's client path held to the alone path's words; the
    service's standing form and note part (`QuestPersonDoneTests`); each condition of §2.1 refusing on its own inside the transaction: a
    graph-bound quest, a closure graph, a source reopen's hold and withdrawal, a session started or resumed between the relay's read and the
    move, a last session other than the one named.
13. **The parent's look on the install**: the grant made in the window; the steward's `sessions stop` and `driver hold` run with no card and
    say so; a press from inside a driven session still raises the card, naming why; the session's page and the hold say it in 中文 too; the
    line in *Notifications*; *Turn off*, then the next press asks.

## 9. The rows

1. **TERMTRUST1a — a terminal's standing confirmation for stop, finish, hold and resume** (driver, modules, service, web; the row to
   build). In two parts a dispatch may split, the first landing first: **(i) containment**: `ProcessStarts`' contained start, the named
   agent job per Daoris process, every agent start, plugin process and piece through it, the census of start sites, `agent.contained`;
   **(ii) the confirmation**: the judge and its table, the caller's handle and the job search, the ledger's expected state for a finish,
   `SET_STANDING_CONFIRMATION` (`Window`) and `STATE`'s `standingConfirmation`, the record and its home entry in the three twins, the
   hold's memory, `STANDING` and `CONFIRMING`'s `standing` on the wire and in `InstanceDoor.Told`, the note part, the log lines and the
   machine log design's rows, This Daoris's row with its ask, the line in *Notifications*, the glossary, both catalogues. Until (i) has
   landed, (ii)'s judge finds its own agents uncontained and covers nothing. Proof: §8 cases 1 to 11 and 13. Contract: §1 to §8.
2. **TERMTRUST1b — closing a quest as done under it** (driver, modules, service, web; after 1a). `QUEST_DONE` over the pipe in every
   bridge-presses place, the verb a client of it, the instance relaying to the service's door with the expected last session, the
   service's standing form judged in its transaction with §2.1's conditions and its note part, the press `done` in the judge, the grant's
   ask offering it. Amends the terminal RPC design §7.1 for `quest done`. Proof: §8 case 12, and case 13 for a done.

## 10. What D201 amends, what stands, and what it rejects

**Amends, once accepted:**
- **D156 point 4 and RPC1c's note.** A terminal's press of the person's waits for the window, unless the person's standing confirmation
  names it in its verb's form and conditions and the terminal's process is in no job any Daoris made for an agent.
- **D156 §5.1** (*no field says the act came from a terminal*). An act under the standing confirmation carries one coded note part saying
  so, on that act's record alone; nothing is added to every operation.
- **ORPHAN1's join** (`ProcessJob.Hold` after the start): an agent's process is contained from its first instruction, and a start that
  cannot be contained is not made, as a heavy gate's is not.
- **The session ledger's advance** gains an expected state, which a finish under the confirmation sends.
- **The terminal RPC design §4.3** (the key presented only after Confirm): or under the standing confirmation; **§7.1** for `quest done`
  (TERMTRUST1b).
- **The This Daoris design §3.7** (a row), **D196's view** (a line at the head of the presses' group), **D172's layout table and D195's
  inventory** (a `data/` entry, kept), **the machine log design §4** (§7.2's rows), **the glossary** (§6.3).

**These stand:** D52 and D37 (no approval surface widens autonomy: the grant is the person's, and an agent's own presses never ride it);
D72 and D74 (no widening without the person: nothing in the set widens); D58 and D126 §3.3-3.4 (a stop is no strike and holds its quest;
*Try again* stays the person's); D133, D135, D154 point 8, D163, D173's override and reopen choices (each yes, go-ahead, verdict, answer,
override and reopen's choice is the person's per press); D145 (a landing is due by the agent's done under the person's rule); D156 points 1
to 3, 5 to 7 and the service door's two minutes; D196 and CONFIRMSEEN1 for every press that still asks.

**Rejected:**
- **A terminal verb or Ask Daoris door to grant it.** A session can run any verb and reach any door a helper proposes through; the grant
  would be one command away from an agent.
- **A card for the grant, or *don't ask again* on a press's card.** A terminal could raise the first, and the second offers the grant in
  front of a press that may be an agent's (D196's *Confirm all*).
- **Covering every `Person` press**, or a class of them by a word (*management*): §2.2's rows are each the person's for a reason a class
  would lose.
- **Covering a caller by who it is** (its parent process, its name, its environment): D156 §2.4. The jobs only remove cover.
- **"In no job at all" as the caller's test.** Measured: Windows puts explorer, Windows Terminal and the steward's own process in jobs
  (§0.6), so it would cover no one.
- **The home's own jobs only** (Codex's finding 2): an agent another home runs is in none of them, and the steward's own development runs
  are other homes.
- **The join after the start, as `ProcessJob.Hold` makes it** (Codex's finding 1): a child started before it is outside the job.
- **A process id for the caller** (Codex's finding 8): one read again later may name another process.
- **Persisted provenance for `resume`** (Codex's finding 5): no file can tell a person's hold made alone while Daoris was down from the
  terminal's it replaced.
- **A finish judged by the verb's read alone** (Codex's finding 4): the request folder and the ledger act later and read no state.
- **Keeping it in memory only, granted at each start.** The steward's updates restart Daoris many times a day, and each restart would
  bring the card back until the owner pressed again, which is the owner's report again. The record's weakness is said (§3.4).
- **A key in `driver.json`.** Its writers include every terminal verb that sets a choice, Ask Daoris's doors and grants, and the CLI's
  twin: a grant beside writers a terminal reaches by design.
- **Sealing the record** with an ACL or DPAPI: any process of the account unseals it (D156's rejection for the key).
- **Watching the record and turning it off on an outside write.** It only moves a forgery to a moment Daoris is down.
- **An expiry.** Each use is said, and *Turn off* is in the view the bell opens; a timer would bring the card back on a day the person
  forgot, which is the report again. Read again after a week on the install.
- **A notice per use** in *Done and failed*: the steward's day would bury the person's own news; the record, the line's count and the log
  say each.
- **`decline`, the keeping stop, a work's pause and resume, *Try again* and `SET_DRIVABLE`**: §2.2.
- **Closing a quest as done at the service's door** by the window confirming the service's own confirmation: that door names no process,
  so the caller's jobs could not be read, and the record could not say the act was the standing confirmation's.
- **Stopping the automatic landing after a covered stop** (Codex's finding 3's other remedy): the landing is the agent's done under the
  person's rule (D145 point 2), which the stop did not make and would only delay.

**What is left.** The account's reach, Daoris's own runs of an agent's code, and an older build's agents (§3.4). A finish cannot be taken
back by a press. Off Windows there is no pipe, so nothing here applies.

## 11. The reads

**Codex's read, round 1** (`codex exec`, read-only, on this branch at `a4ca9c859`, 2026-10-23). Twelve findings, eight blocking; the
reader also confirmed the verbs' payloads against the forms, `said: true` choosing the verb's path and the page's form completing a working
record, the planner's reading of a stop by state, the established jobs' kill-on-close with no breakaway, the census refusing a `Window`
route before dispatch, the sync carrying records and not home files, the journal and `RESULT`, waiting cards independent of a later grant,
and today's `quest done` bypassing requirement answers. Each finding was checked by the author at the lines it cites; all twelve held and
were taken.

| # | Finding | Checked at | Verdict | What changed |
|---|---|---|---|---|
| 1 | Blocking: a harness starts before it is joined, so a child it starts at once escapes its job | `Driver.cs:3143`, :3147; `ChatRunner.cs:1180`, :1980; `HeavyStep.cs:13-17` | taken | Every agent start, plugin process and piece contained from its first instruction by a contained start in `ProcessStarts`, a start that cannot be contained not made; TERMTRUST1a part (i) (§3.3 point 3, §9; case 2) |
| 2 | Blocking: another home's agents are in none of this home's jobs | `SessionProcesses.cs:389`, `InstancePipe.cs:234` | taken | One named job per Daoris process that runs agents, `Local\daoris-agents-<home id>-<pid>`, found by prefix in the session's namespace and asked by handle; an older build said as a limit (§3.3, §3.4; case 3) |
| 3 | Blocking: a covered stop can leave work due for an automatic landing | `AutoLanding.cs:202-207`, `Driver.cs:2373`, :2436 | taken as a correction | The claim was wrong as worded. A stopped run is due only where its agent's own done (or a closure's submission) already made it due, exactly as its end would have; the stop adds none. Stated in §0.3, §2.1 and §3.3, held by case 7; preventing the landing rejected (§10) |
| 4 | Blocking: a finish's state is read before the move, which neither the folder nor the ledger reads again | `SessionsCommand.cs:935`, `SessionRequestWatch.cs:331-345`, `SessionLedger.cs:1726` | taken | A covered finish only of a session with no process here, moved by the ledger only from `awaiting-person` through an expected state compared in the store's transaction (§2.1; case 6) |
| 5 | Blocking: persisted `holds` cannot tell who holds a repository after a restart | `DriverChoicesCommand.cs:371`, `DriverConfig.cs:1384` | taken | The provenance is the instance's memory for its run, forgotten at a start and at any other change of that hold; the file keeps none (§4.2) |
| 6 | Blocking: a person's done overrides a closure graph and closes a step a reopen held | `Quests.cs:1742-1752`; D173's notes | taken | `done` excludes graph-bound quests, closure graphs and a reopen's hold or withdrawal (§2.1) |
| 7 | Blocking: `done`'s conditions were a snapshot before an unconditioned write | `QuestExchange.cs:2261`, `Program.cs:706` | taken | The relay names the expected last session and the service judges every condition in its own transaction (§2.1; case 12) |
| 8 | Blocking: the asker is a cached process id | `InstancePipe.cs:838`, :1025-1047 | taken | The pipe keeps a handle to the client's process opened as the connection is taken, with its creation time; a client it cannot hold is unknown, which asks (§3.3) |
| 9 | Revocation needs an admission point | `InstancePipe.cs:688` | taken | Admitted once under the judge's lock; a revoke after it does not recall it; the hold's memory written under the same lock (§3.3 point 5, §4.3; case 4) |
| 10 | `DAORIS_DRIVER_CONFIG` lets a verb load and save the record | `DriverConfig.cs:738` | taken as a correction | §4.1 says it: the account's reach over the file, which can set nothing the record is read for now that `holds` left it |
| 11 | The UI's off sentence overstates, the on sentences name ungranted presses, and *terminal* means the panel's shell | `PipeCensus.cs:199`, `glossary.json:641` | taken | §6.1's off words; the granted presses only, on the row and in *Notifications*; §6.3's glossary change |
| 12 | §0.2 said all four window routes refuse the terminal in their handlers | `PersonRoutes.cs:20`, :26, :45, :64 | taken | §0.2: the census refuses all four; the two that answer also check the door |

**The author's own check**, with the same read:

| # | Finding | Checked at | What changed |
|---|---|---|---|
| S1 | Daoris itself runs code an agent wrote outside every agent job: a repository's git hooks under its own git acts, and a plugin's performed step | `ProcessStarts` callers (§0.6), `HookProcess.cs:144`; no `--no-verify` or `core.hooksPath` in the driver library | A plugin's process is contained with the agents; Daoris's own git acts are said as a limit, the presses' own bounds holding there (§3.4) |
| S2 | Whether a terminal outside Daoris is in no job | measured: `IsProcessInJob(…, NULL)` on explorer, Windows Terminal and the steward's process chain, each in a job | The judge asks only the jobs Daoris made for agents, by handle; "no job at all" rejected (§0.6, §10) |

**Fable's foundation read** is the parent's, under D191's amendment: the design changes D156 point 4, a rule others rest on, and the
containment every agent start rests on.
