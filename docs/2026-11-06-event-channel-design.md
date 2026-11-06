# Events from records, a relay that lives for the session, and an outside agent on a channel (EVENTHOOK1d, EVENTHOOK1e, CHANNELJOIN1)

Design, accepted by the owner (D244). Written by Claude Opus 5.5 from the code on main at `df7f178bf`. It is the later round
the event hook design (`docs/2026-10-24-event-hook-design.md`) leaves to rows 1d and 1e (its §4.2, §6.2, §7, §9), and the design
CHANNELJOIN1 is named for in the integration design (`docs/2026-10-28-integration-design.md` §7.1). Read with that design's §4.3 (the
frame), §6.1 (the relay) and §8 (what a subscriber may do), the foundation design's §4 (the journal), and
`docs/areas/co-working-and-messages.md`. Nothing here changes the first build (1a, 1b, 1c, 1g, 1h). The sources of 1d's kinds rest on
none of it and come first; every telling comes after 1b (D244's notes).

Paths without a root are under `src/Daoris.Desktop/`: `Driver/` is `Daoris.Desktop.Driver/`, `Modules/` is `Daoris.Desktop.Modules/`,
`Host/` is `Daoris.Desktop.Driver.Host/`. `Service/` is `src/Daoris.Service/Daoris.Service.Core/`, `Http/` is
`src/Daoris.Service/Daoris.Service.Http/`, `Mcp/` is `src/Daoris.Service/Daoris.Service.Mcp/`, and `Web/` is `src/Daoris.Web/src/`.

## 1. What is asked

- **EVENTHOOK1d.** `quest.published` and `quest.closed` from the quests' recorded operations, never the look's snapshot, which keeps a
  held quest in the open list, misses one published and closed between looks and reads a reopened quest as new (Codex's C5.4);
  `person.answered` from the pipe's confirmation record; and the row that owns link 1's emission, which `work.ended` waits on.
- **EVENTHOOK1e.** The inbox adapter, with the token's custody and a rule for a refused batch; the knowledge server's channel, with a
  door that reads the install and a hand-over across the launcher's swap (C5.5); each measured first (M2 to M4 and M8; M6 and M7), on
  this machine, without the owner's own sessions.
- **CHANNELJOIN1** (with COMMS1k). An agent the owner runs outside Daoris joins a channel: who it is there, what it may post, how it is
  admitted, how it leaves, and what Daoris refuses.

The three meet at one question: how a session Daoris did not start hears Daoris and speaks to it. 1d says what it hears, 1e how it is
woken, and CHANNELJOIN1 how it speaks.

## 2. What the code holds today

Read at `df7f178bf`.

### 2.1 The journal

- **Record events are minted by trigger** into the service's `event_journal`, in the statement that writes the record
  (`Service/Sessions/EventJournal.cs:3-31`). A session's state move is one `state` row guarded on the state changing
  (`event_journal_state`, `:159-167`); a record kept is an `opened` row (`:149-157`). Rows hold ids and codes, never words or paths.
- **The quest log journals each operation kept** as a `quest-op` row naming the quest and the operation's kind, never its payload
  (`QuestLogSchema`, `:188-202`, run by `Service/Quests/QuestSchema.cs:299`). A rebase's rewrite in place journals nothing. **No code
  reads a `quest-op` row.** The channel journals each message kept (`ChannelSchema`, `:255-275`).
- **A process reads the journal from its come-up** (`Driver/Events/JournalFeed.cs:23-58`): one long wait on the change stream's
  `journal` family, each answer handed to every teller from its own cursor. A teller with no kept cursor takes the newest position at the
  first answer, through its `Keep`, and is told nothing before it (`:111-116`); a cursor below the floor or of another store is a batch
  marked `Missed` (`:153-165`).
- **The lease's loop already tells from it.** The shell starts the lease's journal feed only once it holds the home
  (`Modules/Loop/LoopRun.cs:92-99`), with a news teller of the driven work (`NewsTeller`, `Driver/Events/NewsTeller.cs:19-75`); the
  headless host runs its own (`Host/DriveConsole.cs:87-108`). The attention watch still observes each look (`Modules/Loop/LookTold.cs:39`).

### 2.2 The quests

- **A quest is its operations replayed** (`Service/Quests/QuestLog.cs:3-14`). An operation recorded need not move the quest: a decline
  made while open lands only on an open quest, a publish only begins one, a take loses to an earlier take (`Applies`, `:26-79`).
- **The `quests` table is the replay's cache**, written whole by the replay and nowhere else (`Service/Quests/QuestCache.cs:6-9`,
  `WriteAsync` `:84-93`), in the transaction that keeps the operation (`Service/Quests/Quests.cs:232`), whoever wrote it: this machine
  or a remote's operation integrated by sync.
- **A closed quest is never moved out of its close by a move**: the table allows `Done` and `Declined` only from `Open` and `Taken`
  (`Service/Quests/QuestTransitions.cs:14-18`). Only a replay changes it, where a rebase finds this machine's close lost (a take that
  lost carries its done with it).

### 2.3 The pipe's confirmations

- **Five ends**: `Confirmed`, `Refused`, `Changed`, `Withdrawn`, `Lost` (`Modules/Platform/PipeConfirmations.cs:43-62`).
- **The record says the ending before anyone is told** (CONFIRMWAIT1 §15.2 item 8): `PipeAsking.Persisted` writes `begun` for a
  Confirm whose grounds hold and `ended` with its answer for any other, and an ending it cannot write is no ending: the press waits on
  (`Modules/Platform/PipeAsking.cs:224-248`). `Settle` then writes the log line, tells the windows, keeps a notice and ends the press
  (`PipeConfirmations.cs:712-724`).
- **A confirmation's id is 32 lowercase hex characters** (`Modules/Platform/PipeWaiting.cs:47`). The terminal's request id is the
  terminal's own, bound to its key, and never on the card (`PipeConfirmations.cs:20-33`). The card says whether a terminal still waits
  on its line (`WaitedOn`).
- **The pipe is served only once the shell holds the home** (`LoopRun.cs:225-236`), so the process that answers a press is the
  lease's.

### 2.4 Link 1

- **Link 1 is the chain's judge**, declared to emit `work.step`, `work.held` and `work.ended` (`Driver/Events/Chain.cs:46-56`,
  `:159-162`). It emits `work.held` alone, once a repository, naming the repository (`Driver/Workplace/RefWatch.cs:340-375`).
- **`EndKeeping.KeepAsync` answers a count** of the entries it wrote (`Driver/Workplace/EndKeeping.cs:33-48`). *Ended* is derived at
  read from the kept fact and the cycle, never stored (`:23-25`). Only the lease's holder writes (`:13`, `:40`).

### 2.5 The channel

- **A speaker is a session record.** `ChannelDesk.SpeakerAsync` finds the session the connector names in the ledger, refuses a helper
  and a reviewer, and takes the channel from its entry's origin (`Service/Asks/ChannelDesk.cs:519-543`). The connector names its
  session by `DAORIS_SESSION_ID` (`Service/Asks/IntakeScope.cs:22-28`).
- **A message with no session is the person's** (`ChannelMessage.ByPerson`, `Service/Asks/Records/ChannelMessage.cs:25`), here and on
  every teammate's machine it travels to.
- **The terminal posts as the person.** `daoris-driver ask channel` posts through the person's door (`Driver/WorkOrders/ChannelCommand.cs:12-31`),
  a `Person` route (`Http/PersonDoors.cs:302`, `:304`), so a caller without the key waits on the person's Confirm and is then kept as the
  person's words. **An agent outside Daoris has no voice of its own on a channel today**: it speaks as the person behind a Confirm, or not.
- **The pipe judges where a terminal stands**: outside every agents' job, in one, or untold (`Modules/Platform/StandingCaller.cs:11-21`).

### 2.6 The knowledge server and its launcher

- In this repository a session's knowledge server is `tools/knowledge-server.mjs`, which serves a workspace build over a home of its
  own and never the install's (`:138-148`; D63). It moves a running session onto a newer build and passes on nothing the server it left
  writes (`relayServer`, `:283`, `:331`). The server builds against `Daoris.Service.Core` alone, so it holds neither the driver's grammar,
  the lease nor the pipe's rule (the event hook design §1.7). It answers Claude Code's 2026-07-28 probe so the client falls back to
  `initialize` (`Mcp/Program.cs:173-174`).

### 2.7 What else moved since the event hook design's round 4

- EVENTHOOK1g is built: both messaging variables are named once (`Driver/Platform/ChildMarks.cs:19`), stripped at each entry
  (`Driver/Roles/SupervisorProof.cs:46-51`) and from every child (`Driver/Workplace/GitWiring.cs:44`).
- The auto lander is deleted, so 1b's `work.landed` comes from the bound run's `land` (the backlog plan's note; BL59 reads it first).
- 1a to 1c are unbuilt: `work/event`, `HookFrames.Event`, `WorkEventGrammar`, `RelaySaid` and the relay are the event hook design's,
  and every row here builds on them as that design states them.

## 3. EVENTHOOK1d: the kinds read from records

### 3.1 The quest kinds come from the replay's moves

**The `quests` cache journals its state moves**, as `sessions` does. `EventJournal` gains `QuestsSchema`, run by `QuestSchema` after
`QuestLogSchema`, with two triggers of the sessions' shape:

- `event_journal_quest_opened`, `AFTER INSERT ON quests`: one row, family `quests`, change `opened`, `state_new` the status, `quest` and
  `record` the id, `workspace` the circle. An upsert that updates fires no insert trigger, so a row is new only when the replay begins
  the quest: a publish, or a publish after a delete (D95).
- `event_journal_quest_state`, `AFTER UPDATE OF status ON quests WHEN OLD.status IS NOT NEW.status`: one row, change `state`,
  `state_old` and `state_new`.

They hold no `RAISE`, carry ids and the status's closed code only, and are bounded and trimmed by the journal's own rules. **Why the
cache and not the operation**: the cache is what the replay says the quest is, written in the operation's transaction, so its move is
the move and nothing else. A `quest-op` row says an operation was kept, which may not move the quest (a decline that lands on a taken
quest, a second publish), and a rebase that loses this machine's close rewrites the log in place and journals nothing, so a teller of
`quest-op` rows would tell a close that never stood and never say it fell. The `quest-op` rows stay what they are, read by nothing.

| Journal row | Kind told | Fields |
|---|---|---|
| `quests` `opened` | `quest.published` | `quest` |
| `quests` `state`, into `Done` or `Declined` | `quest.closed` | `quest`, `state` (`done`, `declined`) |
| `quests` `state`, out of `Done` or `Declined` into `Open` or `Taken` | `quest.close-lost` | `quest`, `state` (`open`, `taken`) |
| `quests` `state`, `Open` to `Taken` | not told | |

- **`quest.close-lost`** says a quest told closed is open again: only a rebase does it (§2.2), and a subscriber told the close must hear
  it fell. It is not named `quest.reopened`, which is a quest log operation's name (the person's reopen of a submission) and would mean
  another thing in the foundation's `quest.<op>` vocabulary.
- **What C5.4 named is gone by construction.** A held quest closed is told closed at its move, whatever the open list keeps; a quest
  published and closed between two looks is two rows; and nothing reads membership of a list, so nothing new is inferred.
- **One scope, this home**, as every kind is (the event hook design §4.2): a remote's quest integrated here is this home's record, and
  it is told. A first sync of a large circle tells many `quest.published`; the plugin's queue and the relay's batch bound them and say
  the excess (`missed`, `<n> more events`).

### 3.2 One teller, the lease's, with no cursor kept

**`WorkEventTeller`** (`Driver/Plugins/WorkEventTeller.cs`, new) is a journal teller that maps rows to `WorkEvent`s handed to its
sink, which is 1b's `HookSet.Tell`. It is added to the feeds that already run in the lease's process: the shell's lease feed
(`LoopRun.cs:96-99`) and the headless host's (`DriveConsole.cs:104-108`). One wait serves every teller of a feed.

- **First among the feed's tellers.** The feed hands a batch to its tellers in turn, and the news teller's `show` reads the service for
  each piece it tells; the work-event teller's sink never waits, so placed first it tells before any of those reads.
- **Built before 1b, wired with it.** The mapping and its gap rule rest on nothing of 1b: the teller is built and proved with the sink
  its test hands it (EV-A), and joins the two feeds with 1b's `Tell` as its sink (EV-F).

- **No kept cursor.** An event is a wake, never a replay (the event hook design §4.4), so the teller holds its position in memory: what
  moved while no process read is the records' to say. Its `Load` answers its memory, and its `Keep` keeps it there.
- **Its place is a gap.** The feed hands a teller with no cursor the newest position through `Keep` and tells it nothing before
  (`JournalFeed.cs:111-116`). A quest closed between the plugin's handshake and that first answer would go unsaid, so **the teller tells
  one `events.missed` at the first `Keep` of its life**, as each loop tells one after the attention watch's baseline (the event hook
  design §4.2), and one for each batch marked `Missed`. A service restarted on the same store is caught up by position, and loses nothing
  the journal still holds.
- **Order.** Rows are told in position order; `Tell` mints `seq` as it does for every kind. `seq` orders what was told, never what
  happened, beside the look's kinds.
- **It is row n's exemplar.** The foundation design's row n moves the session kinds onto the journal; this teller is the first
  `work/event` source that reads it.

### 3.3 `confirmation.ended` comes from the confirmation's record

- **Named for what moved, never for who.** The kind is `confirmation.ended` and its field `end`: of its five ends, `withdrawn` is the
  terminal's own take-back and `lost` a restart's, and a kind named `person.answered` would tell either as an answer the person gave.
- **Told at the press's settling, after its record says so.** `PipeConfirmations` gains `Settled` (`Action<PipeConfirmation,
  PipeEnding>`), invoked in `Settle` beside `Noticed` (`:712-724`). `Settle` runs only after `Persisted` has written the ending, so an
  ending its record could not take (`Unrecorded`) and a Confirm the drain holds (`Draining`) are told nothing: they wait on, and are told
  when they end. The shell wires `Settled` to its hook set where it wires `Noticed` (`LoopRun.ServePipe`, `:225-236`; EV-F); the pipe is
  the lease holder's (§2.3), so `Tell`'s lease check passes.
- **Only where no terminal waits on its line.** A terminal waiting hears the ending there, which ends its verb and wakes whoever waits on
  it; telling it again is a second wake for one answer. `WaitedOn` false at the ending, a press whose verb gave up (`--wait 0`, its
  thirty minutes, an interrupt), is the case the steward cannot learn otherwise.
- **Fields.** `confirmation`, `^[0-9a-f]{32}$`, the pipe's own id; `end`, one of `confirmed`, `refused`, `changed`, `withdrawn`,
  `lost`. Never the terminal's request id: it is the asker's own string, of no shape Daoris mints, and with its key it reads the answer;
  the steward reads `daoris-driver result <id>` for each press it left waiting (§8 of the event hook design, a wake, never the answer).
- **A press lost at a restart** is counted by the restore and settled by no pipe of the new run, so it is not told: the run is new,
  which the relay says as a gap, and the steward reads `result` again. A keyed press lost as Daoris closes is settled `Lost` and told,
  heard only where a plugin still listens.
- **The service door's confirmations are not told.** Their record is the service's (`ServiceConfirmations`), and no subscriber asks for
  them; a later kind of the same frame where one does.

### 3.4 `work.ended`: the end keeping tells what it wrote ended, and row 1d-iii owns it

- **The one writer tells, whichever caller asked.** `EndKeeping` is written by link 1 (`RefWatch.JudgeAsync` through
  `SessionTrees.KeepEndsAsync`), by the look's tidy and Accept's tidy (`AutoTidies`), and by the retirement of a landing whose branch is
  gone (`KeepGoneAsync`, from `LandedCleanups`). Whoever writes an end first, the others find it holding and write nothing, so a kind told
  from link 1's answer alone would never say an end the tidy or a person's Accept wrote: the commonest case. So `KeepAsync` and
  `KeepGoneAsync` say each end they write through **`WorkEnds`** (`Driver/Workplace/WorkEnds.cs`, new), a per-home announcer of
  `RefMoves`' shape, and the lease's hook set hears it and tells `work.ended` with `session` and `quest?` (EV-F).
- **What is said.** An entry written with a fact (`EndKeeping.Next` answers one only for a verdict *ended*) that names a session: a
  landing its own session and quest; a session branch those of the landing that carries its unit, where one stands; each session once a
  pass. A fact that still holds is not written again, so nothing is said twice; a fact cleared, the line no longer holding the work, is
  not said: the kind says what was reached. An entry that names no session is not said.
- **Only the lease's holder writes** (`EndKeeping.cs:40`), so only it says. A verb alone, under the lease it took, writes and says, and
  nothing in its process listens, as an event minted while no plugin listens; a verb beside the instance writes nothing.
- **The chain's `work.ended` stays unemitted.** Link 1 emits `work.held` for every entry it writes, and the tidy wakes on it; a second
  chain event for the same write would wake the same link for nothing. `KeepAsync` keeps answering its count.
- **Why the end keeping and not the bound run's end.** A run ends when its workflow says (its tidy, a stop), and an end judgement says
  the line holds the work under its cycle. The event hook design's `work.ended` is the second, and a run's end has its own record.

### 3.5 The grammar's additions

`WorkEventGrammar.Version` is raised once by each chunk that adds kinds. Every field keeps §4.3's rules: a closed list per kind, built
in `HookFrames.Event` alone, an optional field that fails left out, a required one told as `events.missed` in its place.

| Field | Kinds (`?` optional) | Shape |
|---|---|---|
| `quest` | `quest.published`, `quest.closed`, `quest.close-lost`; `work.ended?` | §4.3's quest shapes |
| `state` | `quest.closed`: `done`, `declined`; `quest.close-lost`: `open`, `taken` | closed codes |
| `confirmation` | `confirmation.ended` | `^[0-9a-f]{32}$` |
| `end` | `confirmation.ended` | `confirmed`, `refused`, `changed`, `withdrawn`, `lost` |
| `session` | `work.ended` | `^[0-9a-f]{8}$` |

### 3.6 The relay's words

`RelaySaid` gains one fixed sentence per kind, each over its checked ids and codes, such as
`Daoris: quest 48f8ed9121ec closed, done [6c0f1a9e2b47 #31]`,
`Daoris: quest 48f8ed9121ec is open again: its close lost to another machine [6c0f1a9e2b47 #32]`,
`Daoris: confirmation <id> ended: refused [6c0f1a9e2b47 #33]` and
`Daoris: session 4b62a9fe's work ended: the line holds it (quest 48f8ed9121ec) [6c0f1a9e2b47 #34]`.
**`--kinds`' default stays the first build's four**: a quest kind floods at a sync, and the steward's procedure names the kinds it
wants. A kind the relay leaves out still moves its watermark (§4.4 of the event hook design).

## 4. EVENTHOOK1e: a relay that lives for the session

### 4.1 What either adapter would add, and whether to build one

The first build's relay prints one batch and ends, and its end is the wake (`--once`); the steward re-arms it in the same turn. Against
that, an inbox or a channel removes one thing for certain: **a batch that waits for the session's turn to end**, since a background
command's end reaches the session as a new turn and an inbox message is read between tool calls. It removes neither the per-build
approval of the copy's path (both start the copy) nor M5's limit on a relay killed before it speaks (both are processes that can be
killed). It adds a secret to keep (the inbox) or a research preview to rely on (the channel).

So **1e builds an adapter only where the first build's use shows that cost**: M11 (§4.4) reads 1h's measurements record over two weeks of
use. Where no batch was read late because its session was in a turn, 1e closes with that reading and builds nothing. Where some was,
**one adapter is built, the inbox first**, and the channel only where the inbox fails a measurement it rests on and the channel passes
its own. Never both: two live wakes for one relay is two ways to do one job (D228).

### 4.2 The inbox adapter: `events relay --inbox`

The relay's third form, beside `--where` and `--once`, keeping steps 0 to 5 of the event hook design's §6.1 unchanged: its copy, its
pipe, its checks, its grammar, its watermark and its fixed sentences. What differs:

- **It lives for the session's life.** The session starts it once as a background command from its Bash, by its copy's path, with
  `--session-pid` required. It ends when that process exits (waited on, never polled), when a plugin of another build connects (its update
  line, below), or on its refusal rule. Each batch, gathered as `--once` gathers it, is posted to the session's inbox instead of ending
  the relay.
- **The token's custody.**
  1. *Taken at the entry.* The host's entry takes both messaging variables into memory and removes them from its environment
     (`MessagingKey.Take`, beside `HolderKey.Take` in `Driver/Roles/SupervisorProof.cs`), where today it removes them unread. Only
     `events relay --inbox` reads what was taken; every other verb drops it (`MessagingKey.Drop`) before its own code runs, so the
     strip's effect on every verb but one is today's.
  2. *Never written.* Not to a file (the subscriber file holds its pipe and pid alone), not to either stream (`RelaySaid`'s census admits
     no value of that kind), not to an argument (the relay starts no process), not to a log (it opens none), not in an exception's
     message (its catch says a code).
  3. *Sent to one place.* Before its first post the relay asks the system which process serves the inbox's pipe
     (`GetNamedPipeServerProcessId`) and posts only where that is `--session-pid`; any other server is refused with nothing sent, one
     fixed sentence on stderr, and the relay ends as `--once` ends. So a pipe made in the inbox's name by another process never hears the
     token. That the inbox's server is the session's own process is measured (M2a).
  4. *Its shape.* The auth line `{"type":"auth","token":"…"}` first, as documented, then one message line a batch in the shape M2
     finds accepted, plain text: the batch's fixed lines as `--once` prints them, ending with its `read up to` line.
  5. *Held for the relay's life*, its bytes cleared as it ends.
- **A refused batch** is a post the inbox did not accept: the connection refused or closed, an error answered, no acceptance where M2
  finds one is given, or the 30 s line bound reached.
  - It is **never sent again as itself**: a repeat inside the inbox's window is dropped, and a burst is refused at the sender. The relay
    keeps the watermark of the last batch accepted and folds the refused batch into **one gap line** at the head of its next post,
    `Daoris: events may have been missed; read the sessions again [<run> #<seq>]`, naming the refused batch's last bracket. The session
    reads the truth again; nothing is lost unsaid.
  - The next post waits 30 s, then 2 minutes, then 10, whatever has gathered. **After three refusals in a row the relay stops posting
    and ends**, printing on stdout what it holds with the gap line and its `read up to` line, so its end wakes the session as `--once`'s
    does (M5). A refused inbox thus falls back to the first build's wake, never to silence.
  - A post the session holds for approval (an unverified message under a mode that bypasses prompts) is no refusal: it is delivered when
    approved. An own child's post is verified (the documentation, §2.1 of the event hook design), which M2 measures.
- **Its pace stays inside the inbox's bounds**: at most one post per gathered batch, batches at least 30 s apart, each different from
  every other by its brackets, far under 50 queued.
- **A build's change** ends it with the update line posted, as `--once` prints it; the session runs `--where` and starts a new `--inbox`
  by the new copy. The swap is never held: the relay runs from its copy outside `app/` (the event hook design §6.1 step 0).

### 4.3 The channel, should the rule choose it: the relay's own channel form

**Not the knowledge server's.** The knowledge server builds against the service alone, so it would need the driver's grammar, the
lease's read and the pipe's rule as twins (the event hook design §1.7, §6.2); in this repository its launcher serves a workspace's home,
never the install's (§2.6), so its tools cannot be the door that reads the install; and its launcher drops what a left server writes, so
a notification sent across its build swap is lost. A channel served by the relay itself needs none of the three.

- **`events relay --channel`** is the relay as a stdio MCP server: it answers `initialize` with
  `capabilities.experimental["claude/channel"]` and no tools, answers the 2026-07-28 probe *method not found* as the knowledge server
  does, answers `ping`, and refuses everything else. It keeps steps 0 to 5, and each batch becomes one
  `notifications/claude/channel` with `content` the batch's fixed lines and `meta` `{ "run", "seq" }`, the closing bracket's checked ids.
  It lives while its standard input is open. The JSON-RPC is written by hand in the driver: one handshake and one notification need no SDK.
- **Registered by the owner, never in a tracked file**, in the user settings of the session that subscribes, loaded with
  `--dangerously-load-development-channels server:daoris-events` in an interactive session, by a fixed command: `cmd /d /c` of the
  **front** `channel.cmd` that `--where` writes beside the copies under the `bin` entry's `events-relay/`. The front runs the install's
  `app/daoris-driver/daoris-driver.exe events relay --where --front`, which lays the copy out and writes `channel-run.cmd` naming it, and
  then calls `channel-run.cmd`. The front's own path and the install's `app/` path change with no build.
- **The door that reads the install** is the install's terminal, the door §8 of the event hook design already names
  (`daoris-driver sessions --json` and the reads beside it), never the knowledge server: a workspace's server must not open the install's
  store (D63). The channel's `instructions`, a fixed sentence, say to read through the install's terminal and that a wake grants nothing.
- **The hand-over across the swap.** A copy that meets a plugin of another build sends its update line as one notification, writes its
  closing bracket to `channel.since` beside the front, and exits. Claude Code starts the server again through the front (M7b), which lays
  out the new build's copy; the new copy reads `channel.since` once as its `--since`, removes it, and goes on. Nothing in the launcher of
  the install waits on it, since the copy runs outside `app/`. Where M7b finds that an ended channel server is not started again, the
  channel is not built: one that dies at every update is worse than `--once`.

### 4.4 Measured first, in a probe session, never in the owner's

**The probe session.** Each measurement runs in an interactive Claude Code session the probe itself starts and ends, never the owner's
own and never the steward's: a .NET file-based program, `src/Daoris.Cli/test/fixtures/event-hook/ProbeSession.cs` (`dotnet run` on the
file, as `tools/index.cs` is run), referencing the driver for its pseudo-console (`Driver/Platform/PseudoConsole.cs`), so no window
opens. It starts `claude` in a scratch git room under `_fixtures/event-probe/<when>/`, with the enclosing session's `CLAUDE*` and
messaging variables removed (DRV4), the room's own `.claude/settings.json` (its permission mode, a `SessionStart` hook) and `.mcp.json`
(the probe's channel servers), a stand-in Daoris home in the room, never the install's, and the cheapest model with a system prompt
asking for one-word answers. It types the session's input and reads its screen. The stand-in poster, pipe server and one-file channel
server are the fixtures §7 of the event hook design names, beside it. It runs in no gate. It spends on the machine's signed-in account,
about forty short turns in one sitting, which the owner authorises once (D240 point 7). The record names the scratch room, and the probe
removes it.

| # | Measures | Rests on it |
|---|---|---|
| M1 | `claude --version`; `/status`'s `Peer address` form | both |
| M2 | a child started from the session's Bash posts the auth line and one message line: the shapes accepted; whether an acceptance is answered; delivered, held or refused under `default`, `acceptEdits` and bypass modes | inbox |
| M2a | the inbox pipe's server process is the session's own (`GetNamedPipeServerProcessId` against the session's pid) | inbox |
| M3 | the same post to an idle session starts a turn, with the turn's first line | inbox |
| M4 | two identical posts; ten in two seconds; one every 30 s for ten minutes: dropped, refused, rate-limited | inbox |
| M5L | a background command from the session's Bash, and one from a `SessionStart` hook, lives while it waits for an hour, ends with the session, and reads the session's pid | inbox |
| M6 | the one-file channel server registered with the development flag registers and delivers on native Windows to an idle session; the startup notice's words | channel |
| M7 | a .NET stand-in of `--channel`'s handshake is registered after the revision fallback and delivers | channel |
| M7b | a channel server that exits is started again, and how soon | channel |
| M7c | a channel server registered as `cmd /d /c <front>` registers and delivers | channel |
| M8 | a process started from the session's Bash, and its child, hold both messaging variables; a Daoris executable started there holds neither past its entry (EVENTHOOK1g's confirmation) | inbox |
| M11 | from 1h's measurements record, two weeks of the first build's use: batches whose wake came after a turn that was running when the relay ended, re-arm prompts per build, relays killed unsaid | whether to build |

M11 reads the record the steward keeps, never the owner's transcripts. The evidence joins the event hook measurements record
(`docs/2026-10-xx-event-hook-measurements.md`, routed when 1a writes it), with no machine path, token, address or process id.

### 4.5 The decision rule

- **M11 shows no batch read late**: 1e closes on that reading; no adapter.
- **The inbox** is built where M2, M2a, M3, M4 (under the relay's pace), M5L and M8 pass. A failure that blocks it: no acceptance shape
  found, an own child's post held in the steward's mode, the server not the session's process, an idle session not woken, the relay's
  pace refused, or a command that does not live with the session.
- **The channel** is built only where the inbox is blocked and M6, M7, M7b and M7c pass. Organisation policy that blocks channels, or a
  Windows that does not deliver, blocks it.
- **Neither passes**: `--once` stays the one wake, and the measurements record says why.

## 5. CHANNELJOIN1: an outside agent on a channel

### 5.1 Who it is: a channel member, never a session and never the person

- **A member record**, `ChannelMember`, in the asks store beside `channel_addressed` (`Service/Asks/ChannelParts.cs`), in its own table
  `channel_members`, never synced: a membership is this machine's, as a message to one part is. It holds its id, minted as `o` and seven
  hex characters so it is never read for a session's; the channel's origin (`ask:<id>`, `session:<id>`, `quest:<id>`, or a teammate's
  ask's); its own entry, `<origin>/outside:<id>`, the key of its read mark; its agent, one of this build's harness names or `other`; its
  repository, one the registry holds, or none; the confirmation that admitted it; when it joined, when it last read or posted, and when and
  why it left.
- **Its messages name it.** A member's post is a `ChannelMessage` with `Session` its id, `Agent` and `Repository` its own, `Entry` its
  entry, and `Outside: true` (null on every other). So it is never `ByPerson`, here or on a teammate's machine, and an older build or a
  teammate's reads it as an agent's claim. Who said it is read by the one rule as `channel` (the co-working area's rule 17; the rules
  named below are that area's): the sender twins and
  `fixtures/senders.json` gain the member's row with no new sender kind.
- **Not a session record.** The ledger holds what the driver started and observes by its process (`SessionState`), which every look,
  slot, watch, tidy and limit reads; a record with no process would be read as a lost run by each of them.
- **No key.** The member is named by its id at its doors, as a part is named by its id at its connector. A key would guard nothing a
  process of the same user cannot read (the outside agent's own transcript holds what its tool returned), and it would be a second way
  to name a speaker. What holds is what a message is: a claim that asks nothing and starts nothing (rules 15 and 16).

### 5.2 What it may post and read

- **Post**, on its one channel: to everyone or one of the channel's repositories, ordinary or urgent, a `re` and a `question` as a part
  may; at most 10 messages and 3 urgent in its membership, a further urgent one kept ordinary and said, 4,000 characters (`ChannelTerms`).
- **Read** the channel as a part of the line reads it, its own mark moved by its own read; `--since` reads earlier.
- **What its post does**: a running part of the line takes it as another session's message, around that agent's claim, urgent at the
  next step where the door takes words and ordinary riding a prompt the run sends anyway; it never starts, reopens or extends a run
  (rules 14 and 15). The person reads it in the channel with its sender chip.
- **Nothing else.** A member id opens no other door: every session door looks its caller up in the ledger and finds none, so a say, a
  helper's ask, a quest's take, done or decline, a hand-over or a press made in a member's name is refused there. The agent publishes
  quests from its own repository as that repository does today, never as the member.

### 5.3 How it is admitted: its own press under the person's Confirm

- **The agent asks.** `daoris-driver ask channel join <ask>` or `--line <session>`, with `--agent <name>` and `--repository <name>`,
  sends the pipe's `CHANNEL_JOIN`, a `Person` route in `PipeCensus`. **A process in an agents' job is refused `inside` before any
  card** (`StandingPlace.InAgentJob`): an agent Daoris runs is already a part and speaks as itself. An untold place is shown on the card.
- **The person confirms**, on a card that names the channel as *At work* names it, the asking process as the system named it, its
  standing, the agent and the repository it claims, and what follows: *it reads this channel and posts as itself, never as you; it
  cannot start, stop or steer any session*. Its grounds (CONFIRMWAIT1 §5): the channel still open to posts and the bounds not reached.
- **On Confirm** the shell presses the service's `POST /api/channel/members`, a `Person` door recorded as the person's admission, and
  the terminal hears the member's id, which it prints with the two verbs it may run. The verb holds no lease (`VerbLeases`, with its
  reason: its one write is the service's).
- **Not a role and not a grant.** A role (D208, D209) acts on the owner's behalf; a member only talks. A standing grant would admit
  without the person's sight of each join; whether Confirms grow many, the steward's subagents each joining, is read from use first.
  The person does not admit first from the window: the id would then travel to the agent by hand, a second way to join.

### 5.4 Its doors

- `daoris-driver ask channel --member <id> ["…"] [--to <repository>] [--re <message>] [--question] [--urgent]` posts as the member, and
  with no words reads; `daoris-driver ask channel leave --member <id>` leaves. They are `ChannelCommand`'s, the one channel verb, and
  press `GET` and `POST /api/channel/members/{id}/messages` and `POST /api/channel/members/{id}/leave`, **agent-class doors**: answered
  without the key and judged as the member's (`Http/PersonDoors.cs`'s `DoorClass.Agent`).
- **The connector's `channel_post` and `channel_read` refuse a member id** `no-session`: the outside agent's connector is its own
  repository's, and one channel voice has one door until a row asks for a second.

### 5.5 How it leaves

- **By its own `leave`**, or **the person's *Remove*** on its row (`POST /api/channel/members/{id}/leave` with the key, recorded as
  theirs).
- **With its line**: the look ends a membership whose channel's claims expire (rule 15: its ask has no quest open and no part runs; for a
  line's own channel, the line's last part has ended and nothing it asked is open).
- **Quiet for a day**: the look ends one with no read or post for 24 hours, judged against its record at the look, never a timer.
- A member that left keeps its record, so its messages keep their poster; it reads and posts nothing (`not-member`), and joining again
  is a new Confirm and a new id. Each join and leave journals a `member` row (family `channel`), so *At work* redraws. A restart keeps
  every membership.

### 5.6 What Daoris refuses

| Refused | Code | Why |
|---|---|---|
| a member's word as the person's, Daoris's or the supervisor's | none: it cannot be asked | its message carries its id, and a supplied author grants nothing (rule 17) |
| a join from a process in an agents' job | `inside` | it is a part already |
| a join on a helper's or a reviewer's line | `helper` | they have no channel |
| a fourth member on a channel, a ninth on the machine | `too-many` | the bound |
| a post or read after it left, or before it was admitted | `not-member` | |
| a post to one part, or a part's post to the member | `bad-part` | a member is no part of the line |
| an 11th post; a fourth urgent kept ordinary; over 4,000 characters | `too-many`, `too-long` | the part's bounds |
| `now` | no such field | an agent never sends now (rule 4) |
| any door but the channel's two | `no-session` at each | §5.2 |
| a join while Daoris is closed | the verb's own, exit 2 | no pipe serves, and no Confirm can be asked |

### 5.7 How it is woken: `channel.posted`

A member woken by a poll is the poll EVENTHOOK1 removes. So the relay tells **`channel.posted`** from the journal's `message` rows,
through the teller of §3.2: `message?` (8 hex, a message kept here; a mirrored one has an id of another shape and is told without),
`ask?` (6 hex), `poster?` (a session's `^[0-9a-f]{8}$` or a member's `^o[0-9a-f]{7}$`), `by` (`person`, `part`, `outside`,
`teammate`), `urgent?`. The relay gains
`--not-by <id>`: a frame whose `poster` is that id moves the watermark and adds nothing, so a member is not woken by its own post. A
member subscribes with `--kinds channel.posted --not-by <its id>` and reads with its door.

### 5.8 What the person sees, and what the agent is told

- ***At work*** lists a line's outside agents beside its co-workers: the agent, the repository, when it joined and last read, and
  *Remove*. A member's message carries its sender chip with *outside Daoris*. The join's card is a pipe confirmation, counted where every
  confirmation is. The words go through the catalogues in English and Chinese, with the glossary's *outside agent*.
- **The co-working skill** (`canon/packs/co-working/skills/co-working/SKILL.md`) gains a short section for an agent outside Daoris: how
  to join, that its posts are its claims, never the person's, the two verbs and the wake, and that work for a repository is still a quest.

## 6. Rejected

Each with its reason; D244 keeps them.

- **The quest kinds from the `quest-op` rows**: an operation kept may not move the quest, and a lost close journals nothing (§3.1).
- **From the look's snapshot, or a read of the quest per row**: C5.4's three faults, and a read later than the move tells a later state.
- **A kept cursor for the work-event teller**: a replay across a restart, which an event is not.
- **The confirmation's kind with the terminal's request id**, or for every ending: an id of the asker's shape, and two wakes for one
  answer. **Named `person.answered`**: a withdrawal and a loss are no answer of the person's.
- **`work.ended` from the bound run's end**: a run's end is its workflow's, not the line holding the work. **From link 1's answer
  alone**: an end the look's tidy or Accept wrote first is found holding and never told.
- **Building both 1e adapters, or either before the first build's figures**: two ways to wake, and a cost nobody measured.
- **The knowledge server's channel**: twins of the grammar, the lease and the pipe rule, a workspace's home, and a launcher that drops a
  left server's notifications (§4.3).
- **Retrying a refused batch, or dropping it**: a repeat is dropped by the inbox, and a drop is round 2's silent loss.
- **Measuring in the owner's or the steward's session**: it spends their context and mixes their work with the probe's.
- **A member as a ledger session, as a message with no session, or with a key** (§5.1).
- **Admission by a role, a standing grant, or the person first** (§5.3).

## 7. The rows

Each chunk is one builder's, in the backlog plan's style: its tests written and seen failing first, run filtered, compiled clean.

| Id | Rows | Files, starts | Exemplar | Proof | Model | After |
|---|---|---|---|---|---|---|
| **EV-A** | EVENTHOOK1d-i, 1d-ii, their sources | service `Service/Sessions/EventJournal.cs` (`QuestsSchema`), `Service/Quests/QuestSchema.cs:299`; driver `Driver/Plugins/WorkEventTeller.cs` and `Driver/Plugins/Domain/WorkEvent.cs` (new); modules `Modules/Platform/PipeConfirmations.cs:712` (`Settled`) | the sessions' `event_journal_state` (`EventJournal.cs:159`); `NewsTeller` (`Driver/Events/NewsTeller.cs:19`); `Noticed` (`PipeConfirmations.cs:92`) | service: a publish journals one `opened`, a done one `state`, a decline that does not apply, a repeat publish and a cache write that updates an existing row no `opened`, a rebase that loses a done `Done` to `Taken`, a sync's integrated publish one; driver: the teller tells each kind of §3.1 and nothing for a take, `events.missed` at its first place and at a `Missed` batch, a quest published and closed between two looks as two events, a held quest's close once; modules: each ending settled once after its record, none for `Unrecorded`, `Draining` or a press a line waited on | Opus | none |
| **EV-B** | EVENTHOOK1d-iii, its source | `Driver/Workplace/WorkEnds.cs` (new), `Driver/Workplace/EndKeeping.cs:37`, `:211` (each end written said) | `RefMoves` (`Driver/Workplace/RefWatch.cs:20`) | a landed branch the line now holds says one end with its session and quest; a fact still holding writes and says nothing; a cleared fact says nothing; a process without the lease writes and says nothing; a gone landing retired with its end says it | Opus | none |
| **EV-F** | EVENTHOOK1d-i to iii, the telling | driver the teller first in the lease's feeds (`Modules/Loop/LoopRun.cs:96`, `Host/DriveConsole.cs:104`) with `HookSet.Tell` as its sink, `WorkEnds.Listen` in the lease's hook set, `WorkEventGrammar` and `HookFrames.Event`'s fields; modules `Settled` wired in `LoopRun.ServePipe` (`:225`); `RelaySaid`'s sentences with 1c | 1b's `work/event` kinds | the grammar's census over the new kinds, seen failing on a word-shaped `confirmation`; each source told once at `work/event` by the lease's loop and by no `--share` loop; the relay's sentences read against `RelaySaid` | Opus | 1b (BL59), EV-A, EV-B; its relay half 1c |
| **EV-C** | EVENTHOOK1e-i | `src/Daoris.Cli/test/fixtures/event-hook/` (`ProbeSession.cs`, the poster, the stand-in pipe and channel servers); the measurements record | `tools/console3-probe.mjs` (a probe's spend and stripped environment); `Driver/Platform/PseudoConsole.cs` | the record: each of §4.4's outcomes, or why it did not run, each a failure that blocks or a limit accepted; M11 from 1h's record; §4.5's verdict | Opus, one sitting, the owner's spend | 1h in use two weeks |
| **EV-D** | EVENTHOOK1e-ii (only on EV-C's verdict) | the host's entry (`MessagingKey.Take`, `Driver/Roles/SupervisorProof.cs:46`); the relay's `--inbox`; `RelaySaid` | the relay's `--once` (1c); `HolderKey.Take` | a stand-in inbox pipe: the token on neither stream, in no file, in no exception; a server other than `--session-pid` refused with nothing sent; a refused batch folded into one gap line and never sent twice; three refusals ending the relay with its batch on stdout; every other verb dropping both variables unread, held by the wiring census | Opus | EV-C, 1c |
| **EV-E** | EVENTHOOK1e-iii (only on EV-C's verdict, instead of EV-D) | the relay's `--channel`, `--where --front`, `channel.cmd`, `channel.since` | the relay's `--once`; `Mcp/Program.cs:173` (the revision probe) | a stand-in client: the handshake, one notification a batch with its `meta`, nothing but `ping` answered; the front laying out a new build and the watermark handed across an exit | Opus | EV-C, 1c |
| **CJ-A** | CHANNELJOIN1a | service `Service/Asks/ChannelMembers.cs` (new), `channel_members` in the asks store, `ChannelDesk` post and read as a member, `ChannelMessage.Outside`, `ChannelTerms` (`not-member`, the members' bounds), `Http/PersonDoors.cs` (the four doors), `EventJournal.ChannelSchema` (`member` rows); the senders' twins and `fixtures/senders.json` | `Service/Asks/ChannelParts.cs` (a channel kept apart, never synced); `Service/Asks/TeammateAskChannel.cs` | `ChannelMembersTests`: a member's post kept with its id, never `ByPerson`; every refusal of §5.6 the service judges; a member's message on a teammate's ask travelling as an agent's claim (`ChannelSyncTests`); the connector's tools and a session door refusing a member id; `PersonDoorTableTests`' rows; the senders' row on all three sides | Opus | none |
| **CJ-B** | CHANNELJOIN1b | modules `CHANNEL_JOIN` (`PipeCensus`, its card and grounds); driver `Driver/WorkOrders/ChannelCommand.cs` (`join`, `--member`, `leave`), `VerbLeases` | an existing `Person` pipe route with grounds (`Modules/Platform/PipeGrounds.cs`) | `PipeConfirmTests`: a join waits for the Confirm, its card's fields, `inside` refused before a card, untold shown; `ChannelCommandTests`: the member's post, read and leave, their words and exit codes; the pipe and verb censuses | Sonnet | CJ-A |
| **CJ-C** | CHANNELJOIN1c | web `work/team/CoWorkers.tsx`, `work/conversation/SenderChip.tsx`, `asks/ChannelSection.tsx`, `locales/{en,zh}`, the glossary; the look's end of a membership (`Driver/WorkOrders/ChannelMembersLook.cs`, new) | `CoWorkers.tsx`'s rows; rule 15's expiry of a claim never taken | stories in en and zh; *Remove* pressing the person's door; the look ending a membership with its line and after a quiet day; `i18n:check` and `names:check -- --strict` | Sonnet | CJ-A, CJ-B |
| **CJ-D** | CHANNELJOIN1d | `channel.posted` in `WorkEventTeller` and the grammar; the relay's `--not-by`; the co-working skill, synced to this repository and both examples | EV-A's teller; the relay's `--kinds` | the teller tells a message kept, a mirrored one without `message`, `by` per poster; `--not-by` moves the watermark and prints nothing; the canon synced | Sonnet | EV-A, CJ-B, 1c |

**Order.** EV-A and EV-B need nothing of the event hook and can run in any wave; EV-F after BL59 (1b) and both, its relay half after 1c.
CJ-A and CJ-B need nothing of the event hook and can run in any wave; CJ-C after them; CJ-D after EV-F and 1c. EV-C waits for 1h and two weeks of use; EV-D or EV-E, or neither, on its verdict.

## 8. The weakest points

- **`quest.published` at a sync.** A circle's first pull tells every quest it brings. The bounds say the excess, but a steward that asks
  for `quest.published` should expect one large batch after joining a circle.
- **The member is named by an id any process of the user can send.** As a part is by its connector's variable. The promise is what a
  member's message is (a claim that asks and starts nothing, never the person's), not who sent it.
- **The channel rests on a research preview** whose Windows support is unstated, and on M7b, which nothing in Claude Code's
  documentation promises. The rule builds it only where the inbox fails, so its weakness costs nothing while the inbox holds.
- **M11 is a judgement over two weeks** of one steward's use. A *no* closes 1e on today's evidence; a later steward with longer turns
  may open it again with the same rule.
- **`confirmation.ended` reaches only the lease holder's pipe.** A shell that does not hold the home serves no pipe, so there is nothing to
  tell; a headless host has no pipe at all.

## 9. Not decided here

- A standing grant for joins, read from the Confirms' count once members are in use.
- The connector as a member's second door, once an outside agent's connector is the install's.
- A role holder as a relay subscriber (the event hook design §6.2's first bullet), which stays that design's later round.
- The service door's confirmations as `confirmation.ended`, where a subscriber asks for them.
