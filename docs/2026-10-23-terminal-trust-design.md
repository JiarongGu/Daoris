# A terminal's standing confirmation: the person's one press for named management presses (TERMTRUST1, 2026-10-23)

**Status: proposed (D201), round 1.** Written by Claude Opus from the code at `f506863e7`; nothing is built and nothing was run. Codex's
read (D191) is §11. The design changes a decided rule other designs rest on (D156 point 4, as RPC1c and CONFIRMSEEN1 built it), so it
asks for Fable's foundation read too (D191's amendment).

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
(长期答复, `glossary.json:483`) is an answer given once for every session of a repository. TERMTRUST stays the rows' id.

### 0.2 How a terminal's press reaches the window today

- **The verb sends its route over the pipe.** `daoris-driver sessions stop|finish …` sends `STOP_SESSION` or `RESOLVE_SESSION` with
  the route's fields beside `said: true` (`Sessions/SessionsCommand.cs:416-424`); `daoris driver hold|resume <repository>` sends
  `SET_HOLD` `{ repository, held }` (`Platform/DriverChoicesCommand.cs:371-402`, `ChoiceRoutes.Hold` at :76). Each is named in the
  driver library's `PersonPresses` (`Platform/InstanceRoutes.cs:156-305`) or `ChoiceRoutes`, held to the census by `PipeCensusTests`.
- **The census classes it.** `PipeCensus.Driver()` (`Platform/PipeCensus.cs:182-275`, modules) makes `STOP_SESSION`, `CANCEL_TURN`,
  `WORK_PAUSE` and `WORK_RESUME` (:208), `RESOLVE_SESSION` (:218) and `SET_HOLD` (:236) the person's; a route a route-by-route module
  does not name is the person's too (:131).
- **The pipe asks the window.** `InstancePipe.Accept` (`Platform/InstancePipe.cs:498-609`) reads the class (:519), lists every value a
  person's press carries or refuses it `shape` (:538-543), binds the id, then `Confirmations.Ask` (:586) holds it in memory, tells the
  terminal `DAORIS.PIPE` `CONFIRMING` (:606, :612-620), and runs it only when the person's answer is *Confirm* (`ConfirmThenRunAsync`,
  :626-668), through the journal and inside `PipeDoor.Enter()` (`RunAsync`, :688-723; `PipeDoor` at `Platform/RefusalLog.cs:78-100`).
  Five wait at once; each waits ten minutes to be found and two once shown (`PipeConfirmations.cs:116-133`, :187-224).
- **The window alone answers.** `PERSON_CONFIRM`, `PERSON_CONFIRM_SHOWN`, `PERSON_CONFIRMATIONS` and `PERSON_KEY` are `Window` in the
  census (`PipeCensus.cs:250-260`) and refuse a terminal inside their handlers too (`Bridge/Routes/PersonRoutes.cs:39-72`). The card
  sits in *Notifications* (D196, NOTIFY2b); while a press waits unshown the bell wears the warning hue, a toast stays, and the taskbar
  button is lit (D156's NOTIFY1 note).
- **The terminal says it waits.** `InstanceDoor.Told` (`Platform/InstanceDoor.cs:422-439`) prints *waiting for you to confirm this in
  Daoris's window…* on the error stream at `CONFIRMING`; a refusal or the two minutes' end is exit 1.
- **The log** says `person.confirmation` with `state` (`asked`, `shown`, `confirmed`, `refused`, `expired`, `withdrawn`), the route
  and `door: terminal`, never a value or the asker (`PipeConfirmations.cs:387-388`).

So every person's press from a terminal waits for a card, which is the owner's report.

### 0.3 The presses in question, and what each does today

| Press | Verb | Route and form | What it changes | Undone by |
|---|---|---|---|---|
| Stop a session | `sessions stop <id>` | `STOP_SESSION { id, said: true }` | its process ended as the person's stop: a driven session's record `stopped`, its quest held here until *Try again* (`SessionsCommand.cs:877-923`, `SessionMoves.cs:18-27`; D126 §3.3; `Planner.cs:254` reads the state, never the note); a parked one with no process ended unanswered through the ledger; an orphan's record ended | the person's *Try again* (D126 §3.4), which the work's tree waits for |
| Finish a session | `sessions finish <id>` | `RESOLVE_SESSION { id, state: "completed", said: true }` | the verb's path refuses any record not `awaiting-person` and any intake (`SessionsCommand.cs:925-940`), lets a process go, and moves the record to `completed` with *The person finished this at a checkpoint* (`SessionMoves.cs:42-61`); its quest stays as it was (D126's QUESTCLOSE1 note) | nothing: the record is ended, and its quest sits until the person marks it done; the tree and the words stay |
| Hold a repository | `driver hold <repository>` | `SET_HOLD { repository, held: true }` | `driver.json`'s `holds` gains it: still drivable, no new start (`DriverRoutes.cs:54-62`, `DriverConfig.cs:1384-1387`) | `resume` |
| Resume a repository | `driver resume <repository>` | `SET_HOLD { repository, held: false }` | the hold gone: the driver may start that repository's quests again, under every other standing choice | `hold` |
| Close a quest as done | `quest done <id>` | not the pipe: the service's `POST /api/quests/{id}/done` (`QuestDoneCommand.cs:45-51`; `Program.cs:703-718`) | the quest `Done` on the person's word, its requirements answered by none, a chain's next step published, a quest waiting on it resumed; *The person marked this done* (`QuestExchange.cs:2233-2305`); it travels as a done (D68) | nothing: a closed quest does not move (D46 §3) |

Two facts matter below. **The page's form of `RESOLVE_SESSION` judges no state** (`SessionsRoutes.cs:72-108`): only the verb's form
(`said: true`, :95-99) refuses a session that does not wait on the person, and the ledger lets `working` become `completed`
(`SessionLedger.cs:1726-1738`). **`quest done` waits for nothing today**: it is one of the verbs that stay alone at the service's door
(`InstanceRoutes.cs:138`), and the service gates that door only once a starter hands it a key, which no shell does before PERSONDOOR1g
(D156's PROVED1 note; `TASKS.md` PERSONDOOR1d, 1g, 1i open). Any terminal, a session's among them, closes a quest as the person today.

### 0.4 Where a standing choice lives, and who can write it

- **`data/driver.json`** holds the person's standing choices (`DaorisHome.cs:326`). Its writers are the window's `SET_*` routes, the
  terminal's `daoris driver` verbs (alone or through the pipe), Ask Daoris's `setting` doors and its `settings_*` grants (D183), and
  any process of the account editing the file.
- **`data/configured-access.json`** is the precedent for a grant of the person's: Ask Daoris's configured access, written only by
  `SET_HELP_GRANT`, a `Person` route, with no terminal door (D183's HELPMODE1c2b note; `DaorisHome.cs:353`).
- **No file under the home is the person's alone.** D156 rejected a file with an ACL or sealed with DPAPI for the key, since any
  process of the account reads it (D156's rejections), and leaves the home's files to the account's reach (D156 point 7): a session
  that runs `daoris agent rules accept` widens its own rules.

### 0.5 What an agent reaches already, without any grant

Said so the design is weighed against today, not against a boundary nobody has built:

- **The request folder.** Every loop on the home takes a stop, a finish, a decline or words from `run/requests` and records the move
  as the person's (`SessionRequestWatch.cs:3-37`, `SessionRequests.cs:11-58`); any process of the account can write that folder.
- **The home's files.** `driver.json`'s `holds`, and every other standing choice, are a file edit away (D156 §7).
- **The service's doors.** Ungated until PERSONDOOR1g (§0.3).

The pipe's card defends against an agent running Daoris's own verbs, its ordinary reach (D156 §1). It never defended against the
account's reach, which PERSONDOOR2 designs. Nothing here relies on that gap staying open, and nothing here widens it.

## 1. The decision in one paragraph

The person turns on a **standing confirmation** in the window, with their own press, on Settings → This Daoris. From then, while
Daoris runs, a terminal's press that the confirmation names, sent in exactly its verb's form, from a process **outside every session
Daoris runs**, runs without a card: the pipe runs it as a confirmed press, tells the terminal so, writes `person.confirmation` with
`state: standing`, and the record it acted on says it was sent from a terminal under the standing confirmation. It names four presses
now: stop a session, finish a session that waits on the person with no words of theirs, hold a repository, and resume a repository a
terminal held under it; TERMTRUST1b adds closing a quest as done in a narrow form, once that press is on the pipe. Everything else asks
as today. The person turns it off at once from the same row or from *Notifications*. Its record is a file of its own under `data/`,
bound to this machine and account, written only by a route the census makes the window's, never carried by a sync.

## 2. The presses it names

### 2.1 The table

Each press is a route and a **form**: the payload's every name, as the bridge reads names (ignoring case, each once: `FieldsOf`
already refuses a name twice, `PipeConfirmations.cs:250-303`), is one the form allows, of the form's type and value. Any other name
or value, a later field included, is not the form, and the press asks as today. Each form is held to its verb's own payload by a test
that sends the verb through the stand-in instance (§8).

| Name | Route | Form | Why it is safe to name | Taken back by |
|---|---|---|---|---|
| `stop` | `STOP_SESSION` | `{ id: string, said: true }` | It only ends work: the record concludes `stopped` as the person's stop, which holds its quest until *Try again* (D126 §3.3), never a strike (D58), a done, a landing or a carry-on. `said: true` runs the verb's own path, whose three outcomes (stopped, an orphan ended, run elsewhere) it already says | the person's *Try again*, the tree kept |
| `finish` | `RESOLVE_SESSION` | `{ id: string, state: "completed", said: true }`, no `note` | It ends a session that already waits on the person, writing none of their words. Only the verb's form judges the state (§0.3), so the form requires `said: true`: a page-shaped payload, which would complete a working session, is not the form | not by a press: the quest sits until the person marks it done (QUESTCLOSE1), the tree and the conversation kept for *Start from* |
| `hold` | `SET_HOLD` | `{ repository: string, held: true }` | It only stops new starts in one repository; what runs runs on | `resume` |
| `resume` | `SET_HOLD` | `{ repository: string, held: false }`, the repository in the record's `holds` (§4.2) | It undoes only a hold a terminal made under this confirmation; a hold the person made in the window, or alone, asks. It changes no permission, cap, strike count, landing rule or drivability: work starts only as the person's other standing choices already allow | `hold` |
| `done` (TERMTRUST1b) | `QUEST_DONE`, a new route (§9) | `{ quest: string, said: true }`, no `note` | It closes what a `finish` left sitting: covered only where the quest is open or taken, no session of it on this machine is queued, starting, working or waiting on the person, and its last session here ended `completed` with the finish's code. It writes none of the person's words; its note says it came from a terminal under the confirmation | nothing: a closed quest does not move |

**Why `done` waits for its own row.** Its door is the service's (§0.3), which no pipe judge sees, and which waits for nothing today. To
be judged where every other terminal press is judged (its terminal named, its process placed, §3.3), TERMTRUST1b moves the verb onto
the pipe as a `Person` route the instance relays with its key; until then the verb keeps acting alone, and the standing confirmation
changes nothing for it. It is the one press in the set that is not a narrowing: it ends a quest for good and lets a chain go on.
Its conditions confine it to the case QUESTCLOSE1 built the person's done for, after a finish.

### 2.2 What it never names

| Press | Why not |
|---|---|
| `sessions say`, `take-back`, `reword`, `channel post`, `ask`, a finish or done with `--note`, `decline` (its reason) | words said as the person (D156 §3) |
| `sessions stop --turn` (`CANCEL_TURN`) | it sends a driven session's held words as its next prompt now (`ConversationRoutes.cs:286-291`): a choice of when the person's words are read, not a stop |
| a go-ahead's answer, a helper's or a hand-over's yes or no, a held tool call's answer, an answer alone let go, `WORK_HELD`, a workflow node's skip or stop | answers the person gives to someone (D133, D135, D163, D184, D188) |
| *Try again* (`RETRY_QUEST`), `WORK_RESUME`, `WORK_PAUSE`'s resume | each releases the person's own stop or forgives strikes (D58, D126 §3.4); this confirmation undoes only what it did |
| `WORK_PAUSE`, `WORK_ABANDON` | an ask's whole work across repositories, and an abandon discards: not in the steward's set, and the second destroys |
| landings, hand-offs, discards, sweeps, clears, deletes, archives, `TREES_SYNC`, `SYSTEM_SPACE_FREE` | a yes to work, a push through a plugin, or a delete (D37, D145) |
| every `SET_*` but `SET_HOLD`, `SET_DRIVABLE`, a trust, a rule's answer, an account, a tool, a plugin, `SETUP_PRESS` | standing choices one direction of which widens what an agent may do (D72, D74; the census's own reason, `PipeCensus.cs:75-81`) |
| sign-ins and account actions (`HARNESS_ACTION`, `ACCOUNT_USE`, `HARNESSES_REFRESH`), `ASK_OPINION` | a sign-in, or work on the person's accounts |
| `END_CHAT`, `STOP_TASK`, `STOP_OPINION` | no terminal verb sends the first two; a reviewer's stop is the person's word on a second opinion |
| the standing confirmation itself | §4.1 |

### 2.3 A grant names its presses

The record keeps the names the person granted (§4.2). A press is covered only where the build's table names it **and** the record
names it, so a later build that adds a press (TERMTRUST1b's `done`) covers nothing until the person grants it; the window offers it
then with the others. The person may grant fewer than the build offers: each press is a tick in the grant's ask, all ticked at first.

## 3. Who it covers: the threat model

### 3.1 What the card defends today

A session runs as the person's account, so it can run `daoris-driver`, open the pipe and send any press; nothing a binary reads tells
whose run it is (D156 §4.1; the terminal RPC design §4.2). The card makes every such press the person's decision, its default no.
That holds against an agent's ordinary reach (its commands, its tools, what its environment and tree hand it), not against the
account's reach (§0.5).

### 3.2 What the standing confirmation lets through

Exactly the presses of §2.1 in their forms, from a terminal outside every session Daoris runs, while the record says it is on. An
agent the person runs by hand, outside Daoris, is such a terminal: the steward is one, and that is the purpose. The grant's ask says
so in plain words (§6.1).

### 3.3 What bounds it

1. **The presses.** Each only ends work, ends a session that already waits on the person, stops new starts, or undoes a hold the
   confirmation made itself. None says words as the person, answers anyone, lands or pushes, deletes, widens what an agent may do,
   releases the person's own stop or hold, or starts work the person's standing choices did not already allow. `done`, from
   TERMTRUST1b, is the exception said in §2.1, and its conditions keep it to closing what a finish left sitting.
2. **The forms.** Only the verb's own payload; anything more asks.
3. **The caller: never a session Daoris runs.** The pipe names its client's process (`GetNamedPipeClientProcessId`,
   `InstancePipe.cs:1025-1051`). Every agent Daoris starts runs in a Windows job that kills on close and allows no breakaway, so every
   process it starts, detached or not, is in that job (`Agents/Consoles/ProcessJob.cs:6-27`): the registry joins each at its spawn
   (`SessionProcesses.Track`, `Sessions/SessionProcesses.cs:372-378`), called by the driver's one run, `Driver.HoldAsync`
   (`Driver.cs:3127-3147`), which the driven sessions, intakes, helpers, reviewers, partners, brainstorm passes and carry-ons go
   through, by the carry-ons' own starts (`DriverContinuations.cs:989`, :1026) and by the conversations (`ChatRunner.cs:1980`); a
   partner's piece also has a named job (`PieceJobs.cs:17`), and a heavy run's gates, which run what an agent wrote, have one too
   (`HeavyRun.cs:418`). Before a press is covered, the judge asks each of those jobs whether the client's process is in it
   (`IsProcessInJob`):
   - **in one**: not covered; it asks, and the card names the terminal as today. So a session's own shell, its background jobs, a
     script its tests run, Ask Daoris's shell under full access, a helper's, a reviewer's and a partner's never ride the grant;
   - **in none, and every agent this instance runs is held in a job it can read**: covered;
   - **cannot tell** (an agent whose job could not be made or joined, since `ProcessJob.Hold` is a best effort, `ProcessJob.cs:20-21`;
     a session another process on this home runs, which the registry's markers show alive and no job here holds,
     `SessionProcesses.cs:389-397`; the client's process gone or not opened; not Windows): not covered, and said why.
   The terminal panel's own shells (`PseudoConsole.cs:122`) are the person's and are not among them. This only ever adds a card: being
   in no job never makes a caller the person, which D156 §2.4 rejected for an OS identity; it is the person's grant that covers the
   press, and the job only takes it away from a session.
4. **A session's own record cannot be stopped into anything.** A stop of itself concludes `stopped` as the person's stop, which holds
   its quest for the person's *Try again*: no strike escaped into a done, no carry-on, no landing. A finish needs a record waiting on
   the person, which has no turn running, and leaves its quest as it was. A hold stops starts. And by point 3 a session's own process
   tree, its background work included, is not covered at all.
5. **Said, every time** (§7): the log, the record, the terminal and the window's line.
6. **Revocable at once, and named** (§4.3, §2.3).

### 3.4 What it does not bound

- **An agent the person runs by hand outside Daoris** rides it, by design; the grant's ask says so.
- **The account's reach** (D156 point 7, PERSONDOOR2): an agent that writes the record while Daoris is down is read at the next start
  (§4.2); one that starts a process outside its job through a system service (the task scheduler, WMI, a COM server) is a caller in no
  job; UI automation of the window, a development run's debug port and the request folder (§0.5) reach the same acts with no grant at
  all. The standing confirmation adds no act the account's reach lacks today.
- **Denial.** A covered caller can stop or finish the person's sessions and hold repositories. Nothing is lost by it: trees, branches
  and conversations are kept, a stopped quest waits for *Try again*, a finished one sits, and each use is said where the person looks.
- **The service's doors** stay ungated until PERSONDOOR1g, whatever this says.

## 4. Granting and revoking

### 4.1 Only in the window

- **One route, the window's**: `SET_STANDING_CONFIRMATION { presses: [...] }`, the presses ticked, `[]` to turn it off. The census
  classes it `Window` (`PipeCensus.cs:250-260`'s group), so a terminal is refused `window-only` before it is dispatched, and the
  handler refuses `PipeDoor.Terminal` as `PERSON_CONFIRM` does (`PersonRoutes.cs:45-48`). A terminal cannot even ask for a card that
  grants it: a person trained to confirm a terminal's presses would confirm that one too.
- **No verb, no door, no tool.** No `daoris` or `daoris-driver` verb reads or writes it; Ask Daoris has no `setting` door for it and no
  `settings_*` grant; `HelpCoverageTests` holds the page's hook `Exempt` with this reason (bridge-presses row 7).
- **The grant asks once, inline** (the platform language's widening ask, as full access asks, the Ask Daoris modes design §5): it
  lists the presses with their ticks and says who can use them (§6.1). Revoking asks nothing.
- **Never from a card.** No confirmation card offers *don't ask again*: the grant is made where the person sets things, never in front
  of a press a terminal raised, which may be an agent's (D196's rejection of *Confirm all* stands).

### 4.2 The record

`data/standing-confirmation.json`, a new entry `standing-confirmation` in D172's home table (`DaorisHome.cs:324-399` and its two
twins, the service's `HomeEntry` and the CLI's `home.ts`), category `Data`, its keep rule *kept* in D195's inventory:

```json
{ "schemaVersion": 1, "presses": ["stop", "finish", "hold", "resume"], "since": "2026-10-23T06:05:00Z",
  "machine": "<SHA-256 hex of this machine's name and the account's SID>", "holds": ["<repository>"] }
```

- **Written only by the grant's route** and by a covered `hold` or `resume` (its `holds`, below), atomically, as the home's records
  are. The CLI's twin names the path and never reads it.
- **Read** by the instance as its loop takes the home, before the pipe serves (`InstancePipe.Serve`), and kept in memory; written
  through on each grant, revoke and covered hold. A write by another process while Daoris runs is not read until the next start.
- **Bound to this machine and account.** A record whose `machine` differs (a `data/` folder copied elsewhere) is not honoured, is
  said, and is left; the window's next grant writes anew. Absent, unreadable or of a newer `schemaVersion`: off, said, never written
  over by an older build (as `Notices.cs` treats a newer record).
- **`holds`** is the provenance of `resume`: a covered `hold` that changed the repository from not held to held adds it; a covered
  `resume` removes it; any other change of that repository's hold (the window's press, a confirmed press, the alone path seen at the
  next read) removes it. The window shows a hold in the list as made from a terminal (§6.2).
- **Never carried.** The sync carries the store's operations (D68), never a home file, and no code but the instance's reads this one;
  the grant itself is on no quest, ask or session record (the note part of §7.1 says a use, never the grant). Copying `data/` to
  another machine carries the file, which that machine does not honour (`machine` above).

### 4.3 Revoke at once

Turning it off clears the instance's memory first, under the judge's lock, then writes the record: a press arriving after it asks,
a press already running runs. A revoke whose write fails still turns it off for this run and says so (*turned off; its record could
not be written, so the next start may read it as on*); a grant whose write fails turns nothing on. A press waiting on a card when the
grant changes keeps its card: a grant confirms no press already asked.

## 5. A press under it, step by step

1. The verb sends its route over the pipe as today (`InstanceDoor.PressAsync`).
2. `InstancePipe.Accept` classes it `Person`, lists its values (:538-543) and binds its id, as today.
3. **New, before `Confirmations.Ask` (:586):** `StandingConfirmation.Judge(module, route, payload, asker)` (modules, `Platform/`)
   answers *covered*, or *asks* with why: `off`, `not-named` (no press of the table or the record), `form`, `not-held-here` (a resume
   of a hold the record does not list), `in-session`, `untold`.
4. **Covered:** the pipe writes `person.confirmation` `{ state: "standing", route, door: "terminal" }`, tells the terminal `DAORIS.PIPE`
   `STANDING` `{ id }`, and runs the press through `RunAsync` (journal, `RESULT`, the dispatcher) inside `PipeDoor.EnterStanding()`, a
   door the handlers read as `PipeDoor.Standing`. The route writes the record's mark (§7.1) and answers its `said` lines as today; a
   covered `hold` or `resume` writes the record's `holds`. Every window is told `STANDING_USED` `{ seq, at }`, which the line in
   *Notifications* counts.
5. **Asks:** as today, the card; `CONFIRMING` gains `standing` (the why, where the grant is on and names the route), so the terminal
   says *waiting for you to confirm this in Daoris's window (its standing confirmation does not cover a press from inside a session
   Daoris runs)*; the `asked` line gains `standing` with the same code.
6. Once the shell holds the person key (PERSONDOOR1g), a route the pipe runs under the standing confirmation presents it to the service
   as a confirmed press does (the terminal RPC design §4.3).

## 6. What the person sees

### 6.1 Settings → This Daoris → *A terminal*

A row beside the launchers and the address file (the This Daoris design §3.7):

| Part | en | zh |
|---|---|---|
| Label | Standing confirmation | 长期确认 |
| Hint | A terminal's stops, finishes and holds run without asking you. | 终端的停止、完成和暂停无需问你即可执行。 |
| Off | Off: every press from a terminal waits for you | 关闭：终端的每个操作都等你确认 |
| On | On since {{since}}: {{presses}} | 自 {{since}} 起开启：{{presses}} |
| `stop` | stop a session | 停止会话 |
| `finish` | finish a session that waits on you, without your words | 完成一个在等你的会话（不附你的话） |
| `hold` | hold a repository | 暂停一个仓库 |
| `resume` | resume a repository a terminal held | 恢复由终端暂停的仓库 |
| Buttons | *Turn on…*, *Turn off* | *开启…*，*关闭* |

*Turn on…* asks once, under the row (`InlineConfirm`), with each press ticked:

- **en:** *Let a terminal on this machine do these without asking you each time. Daoris cannot tell whether you ran a terminal's press
  or an agent did, so an agent you run yourself outside Daoris can do them too; a session Daoris runs cannot. Everything else still
  asks you. Each one is logged and said on what it acted on. Turn it off here or in Notifications at any time.* Buttons **Turn on**
  and **Never mind**.
- **zh:** *允许本机的终端无需每次问你即可执行这些操作。Daoris 无法分辨终端的操作是你运行的还是智能体运行的，所以你自己在 Daoris
  之外运行的智能体也能执行；Daoris 运行的会话不能。其他一切仍会问你。每一次都会记入日志，并写在它作用的对象上。你可以随时在这里或在通知中关闭。*
  Buttons **开启** and **取消**.

A record not honoured is said under the row: *Off: its record is from another machine* / *关闭：它的记录来自另一台机器*; *Off: its
record could not be read* / *关闭：它的记录无法读取*; *Off: its record is from a newer Daoris* / *关闭：它的记录来自更新版本的 Daoris*.

### 6.2 Where it is used

- **Notifications**, at the head of the presses' group while it is on (NOTIFY2b's view): *A terminal's stops, finishes and holds run
  without asking you: standing confirmation since {{since}}, used {{count}} times since Daoris started.* with **Turn off** and *Open
  This Daoris* / *终端的停止、完成和暂停无需问你即可执行：自 {{since}} 起长期确认，本次启动以来使用 {{count}} 次。* **关闭**，*打开本机*. It
  is a line, not a row of *What needs you*: nothing waits on the person.
- **A session's record** (its page, the trace and the terminal's `sessions` words) shows the note part of §7.1 in the reader's
  language.
- **A repository's hold** made under it says *held from a terminal, under your standing confirmation* / *由终端按长期确认暂停* beside
  the hold, where the hold is shown and released.
- **The terminal** says, on the error stream, *ran under your standing confirmation; nothing was asked in Daoris's window* (English,
  as the terminal speaks).

No balloon, toast or badge is raised for a covered press: the person granted it so as not to be asked, and the line and the record say
it.

## 7. The record and the log

### 7.1 On the record it acted on

- **A session.** One coded note part, `standing.terminal`, appended after the move's own note (a stop's *the person stopped it*, a
  parked stop's, an orphan's, a finish's *The person finished this at a checkpoint*): en *Sent from a terminal and run under the
  person's standing confirmation, without asking.* zh *由终端发出，按长期确认执行，没有逐次询问。* The move's own note stays: it is the person's
  stop or finish, by their grant, as D156 point 5 counts a confirmed grant. The part is a fact with no value (`NoteCodes.cs`, a new
  code; `note.json` in both catalogues). The flag reaches each path as the route's `PipeDoor.Standing` handed into the verb's ask and
  its `SessionRequest`, so a stop the loop takes from the folder writes it too.
- **A quest** (TERMTRUST1b): the service's done note keeps *The person marked this done.* and adds the coded part
  `person-done.standing` with the same words, from a field `standing: true` the relay sends; before PERSONDOOR1g any caller could send
  it, and it only weakens the claim.
- **A hold**: the record's `holds` (§4.2), shown with the hold.

### 7.2 The machine log

| Event | Fields | When |
|---|---|---|
| `person.confirmation` | `state: standing`, `route`, `door: terminal` | a covered press, before it runs |
| `person.confirmation` | `state: asked`, `route`, `door`, `standing` (`form`, `not-held-here`, `in-session`, `untold`) | a press the grant names that asked anyway; no `standing` field while it is off or names no such press |
| `person.standing` | `state` (`granted`, `revoked`, `found`, `unread`, `newer`, `other-machine`, `unwritten`), `presses` | a grant or revoke in the window; at the start, what the record said |

Codes only, as D156 §5.1 has it: never a value, an id, a repository, the asker's name or its process.

## 8. Proof

Each case seen failing first, as `held-clocks.md` and `bridge-presses.md` ask. Fast halves unless named.

1. **The judge** (`StandingConfirmationTests`, modules): each press in its form from a caller in no job is covered; each near miss asks
   with its code: a finish with `note`, without `said`, with `state: "declined"`; a stop with an extra field; a name twice in two
   casings; `CANCEL_TURN`, `WORK_RESUME`, `RETRY_QUEST` and every other `Person` route (a census sweep); a resume of a repository the
   record does not list; the grant off; a grant naming fewer presses; a build press the record does not name.
2. **The caller** (`StandingCallerTests`, modules, Windows): a stand-in child started inside a `ProcessJob` the registry tracks asks
   (`in-session`); one outside is covered; a tracked session whose job is null, a marker alive with no job here, and a client process
   gone each ask (`untold`); a piece's named job and a heavy run's job are read; the panel's shell job is not counted.
3. **Over the real pipe** (`PipeConfirmTests`): a covered stop runs with no `CONFIRMING` and the terminal hears `STANDING`; its
   journal answers `RESULT`; a revoke between two presses makes the second wait for the card; a press asked before a grant keeps its
   card; the log lines of §7.2 and none of a value.
4. **The forms against the verbs** (`StandingFormTests`, driver and modules): `sessions stop`, `sessions finish` and `driver
   hold|resume` sent through `InstanceStandIn` are covered as they send them, so a verb that grows a field fails here.
5. **The grant's route** (`DriverModulePersonTests`): `window-only` over the pipe; the handler's own refusal of `PipeDoor.Terminal`;
   the record written and read back; a press the build does not name refused as data; the revoke's memory cleared before a failed
   write; `PipeCensusTests` holds it `Window`, `StatePublisherTests` its mark.
6. **The record** (`StandingRecordTests`, driver): another machine's, an unreadable one, a newer one each off and left; `holds` added
   by a covered hold that changed it, removed by a covered resume and by any other change of that hold; `DaorisHomeTests` in the three
   twins.
7. **The note** (`SessionsCommandTests`, `SessionRequestWatchTests`, `NoteCodesTests`): a covered stop of a live, a parked and an
   orphaned session, and a covered finish, each keep the move's note and add `standing.terminal`; `Planner` still holds the stopped
   quest (`PersonStopped`); the code worded in en and zh.
8. **The page** (web): the row off, on and not honoured; the inline ask's ticks and words in en and zh; *Turn off* in the row and in
   *Notifications*; the hold's mark; `i18n:check` and `names:check -- --strict` (the glossary gains *standing confirmation*, 长期确认);
   `HelpCoverageTests`' `Exempt` row.
9. **TERMTRUST1b**: `QUEST_DONE`'s places (bridge-presses rows 1 to 11), the verb's client path held to the alone path's words, the
   service's `standing` field and note part (`QuestPersonDoneTests`), and the judge's quest conditions each failing on its own.
10. **The parent's look on the install**: the grant made in the window; the steward's `sessions stop` and `driver hold` run with no card
    and say so; the session's page and the hold say it in 中文 too; the line in *Notifications*; *Turn off*, then the next press asks.

## 9. The rows

1. **TERMTRUST1a — a terminal's standing confirmation for stop, finish, hold and resume** (modules, driver, web; the row to build).
   The judge and its table, the caller's job check, `SET_STANDING_CONFIRMATION` (`Window`) and `STATE`'s `standingConfirmation`, the
   record and its home entry in the three twins, `STANDING` and `CONFIRMING`'s `standing` on the wire and in `InstanceDoor.Told`, the
   note part and the hold's provenance, the log lines and the machine log design's rows, This Daoris's row with its ask, the line in
   *Notifications*, the glossary term, both catalogues. Proof: §8 cases 1 to 8 and 10. Contract: this design's §1 to §8.
2. **TERMTRUST1b — closing a quest as done under it** (driver, modules, service, web; after 1a). `QUEST_DONE` over the pipe in every
   bridge-presses place, the verb a client of it, the instance relaying to the service's door, `standing: true` and its note part,
   the press `done` in the judge with its quest conditions, the grant's ask offering it. Amends the terminal RPC design §7.1 for
   `quest done`. Proof: §8 case 9, and case 10 for a done.

## 10. What D201 amends, what stands, and what it rejects

**Amends, once accepted:**
- **D156 point 4 and RPC1c's note.** A terminal's press of the person's waits for the window, unless the person's standing confirmation
  names it in its verb's form and the terminal is outside every session Daoris runs.
- **D156 §5.1** (*no field says the act came from a terminal*). An act under the standing confirmation carries one coded note part
  saying so, on that act's record alone; nothing is added to every operation.
- **The terminal RPC design §4.3** (the key presented only after Confirm): or under the standing confirmation; **§7.1** for `quest
  done` (TERMTRUST1b).
- **The This Daoris design §3.7** (a row), **D196's view** (a line at the head of the presses' group), **D172's layout table and D195's
  inventory** (a `data/` entry, kept), **the machine log design §4** (§7.2's rows).

**These stand:** D52 and D37 (no approval surface widens autonomy: the grant is the person's, and an agent's own presses never ride
it); D72 and D74 (no widening without the person: nothing in the set widens); D58 and D126 §3.3-3.4 (a stop is no strike and holds its
quest; *Try again* stays the person's); D133, D135, D154 point 8, D163 (each yes, go-ahead, verdict and answer is the person's per
press); D156 points 1 to 3, 5 to 7 and the service door's two minutes; D196 and CONFIRMSEEN1 for every press that still asks.

**Rejected:**
- **A terminal verb or Ask Daoris door to grant it.** A session can run any verb and reach any door a helper proposes through; the grant
  would be one command away from an agent.
- **A card for the grant, or *don't ask again* on a press's card.** A terminal could raise the first, and the second offers the grant in
  front of a press that may be an agent's (D196's *Confirm all*).
- **Covering every `Person` press**, or a class of them by a word (*management*): §2.2's rows are each the person's for a reason a
  class would lose.
- **Covering a caller by who it is** (its parent process, its name, its environment): D156 §2.4. The job check only removes cover.
- **Keeping it in memory only, granted at each start.** The steward's updates restart Daoris many times a day, and each restart would
  bring the card back until the owner pressed again, which is the owner's report again. The record's weakness is said (§3.4).
- **A key in `driver.json`.** Its writers include every terminal verb that sets a choice, Ask Daoris's doors and grants, and the CLI's
  twin: a grant beside writers a terminal reaches by design. A file of its own has one writer.
- **Sealing the record** with an ACL or DPAPI: any process of the account unseals it (D156's rejection for the key).
- **Watching the record and turning it off on an outside write.** It only moves a forgery to a moment Daoris is down; the account's
  reach is PERSONDOOR2's.
- **An expiry.** Each use is said, and *Turn off* is in the view the bell opens; a timer would bring the card back on a day the person
  forgot, which is the report again. Read again after a week on the install.
- **A notice per use** in *Done and failed*: the steward's day would bury the person's own news; the record, the line's count and the
  log say each.
- **`decline`, the keeping stop, pause and resume of a work, *Try again*, `SET_DRIVABLE`**: §2.2.
- **Closing a quest as done at the service's door** by the window confirming the service's own confirmation: that door names no
  process, so the caller's job could not be read, and the record could not say the act was the standing confirmation's.

**What is left.** The account's reach (§3.4). A session another process runs on this home suspends the cover while it runs. A finish
cannot be taken back by a press. Off Windows there is no pipe, so nothing here applies.

## 11. The reads

To be filled by round 2.
