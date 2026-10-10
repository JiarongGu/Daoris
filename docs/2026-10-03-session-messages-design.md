# Session messages: one model for every door (MSG1)

**Standing:** contract under D137. Its dated notes record implementation and door-specific limits;
remaining steering and installed proofs are in `TASKS.md`. Harness observations below retain their
named baseline and measurement status.

> The owner, 2026-10-03: *"we do need a way to send messages between runs so it does the resume, so that closes the gap
> for codex and it does look like the same session, and this should be properly designed: native/Daoris-managed
> session messages"*. This is the contract for what a person's words to a session do, whatever its door and its state.
> Its decision is **D137**. Original status: **design before implementation.** Read with **D46 §4**, **D52**, **D76**, **D80**,
> **D83**, **D90**, **D126**, **D131**, **D132**, **D133** and **D136**.

Statements about today were read from the code at `14338bff` (main at `2de3bee2` with STEER1 merged). Statements about
the harnesses carry a label: **read** (in shipped code or a binary's own help, at the version named), **not read**, or
**measured**. Nothing here is measured: no turn was run for this design (§1.5).

## 0. What is true today

| The session | The door | What a person's words do today | Where |
|---|---|---|---|
| driven, its turn running | protocol, `claude-agent-acp` | sent at once, read at its next step (D136) | `DrivenInbox.cs`, `Acp.cs` (`promptQueueing`) |
| driven, its turn running | protocol, any other agent | held, prompted in the same session when the turn ends (D90) | `DrivenInbox.cs` |
| driven, its turn running | native (`claude -p`) | refused in the driver's words: no inbox is opened off the protocol door | `Driver.cs:1296`, `Driver.TakesNoMessages` |
| driven, winding up | protocol | refused: *it stopped taking messages as you sent this* | `DrivenInbox.NextOrClose`, `work.steer.gone` |
| a chat, its turn running | both | held until the turn ends, shown as queued by the page, recorded when sent (CONV4a); a protocol chat does not use `promptQueueing` | `ChatRunner`, `ChatTurns.cs` |
| parked (`awaiting-person`), driven | either | the answer (D131): kept on the record, resumed at the driver's next look; a second answer replaces the first | `SessionLedger.AnswerAsync`, `Driver.Continue.cs` |
| ended: completed, declined, failed, stopped, stood down | either | nothing takes them: the page offers no box. *Try again* is a fresh session (D131 §4); *Send back…* opens the quest composer | `App.tsx:789`, D126 §3.4 |
| an intake | either | refused (INT4h): an intake is answered through its ask | `Driver.Intake.cs` |
| a teammate's record | — | nothing: it is read-only here (D47 §6) | `SessionSync.cs` |

Two more facts the model builds on. **Only a quest's session keeps its harness conversation id**
(`HarnessConversations`; `Driver.cs:1293`, `:1382`, `:1521`): a chat keeps none, so no chat can resume today. **A
finished record does not move** (the ledger's `Terminal` refusal, `SessionLedger.Allowed`): D131 kept an answered park
parked rather than reopen one.

## 1. What the harnesses offer

### 1.1 `codex-acp` 2.1.1, npm's latest and not pinned by Daoris (read)

`npm pack @agentclientprotocol/codex-acp@2.1.1` into an ignored scratch folder; `dist/index.js` sha256 `4b763103…`. It
asks for `@openai/codex` `^0.159.1`, which resolves to 0.159.3. Daoris pins no version of either (entry-point evidence
§0).

| Finding | Where |
|---|---|
| `initialize` answers `agentCapabilities.loadSession: true` and `sessionCapabilities` `{resume, list, close, delete, fork, additionalDirectories, subagents}` | `dist/index.js:37427-37453` |
| A new session's ACP `sessionId` is Codex's thread id | `:34084-34105` (`sessionId: response.thread.id`) |
| `session/resume` calls Codex's `thread/resume` with `excludeTurns: true`, the request's `cwd` and servers: it sends no history. `session/load` calls the same, then streams the thread's history as updates | `:33986-34009`, `:34022-34060`, `:37855-37907` |
| Codex writes a thread's rollout on its first user message, so a thread never prompted resumes only while its app-server lives | `:33931-33980` (`resumeThread`'s own comment) |
| A thread another Codex client holds is refused `invalidRequest` (-32600) with `data.reason: "thread_active_writer"` and the sentence *"This Codex session is in use by another Codex client (the Codex app, the CLI or an IDE extension)…"* | `:33553-33561`, `:33947-33951` |
| A thread Codex does not have is refused with Codex's own error, not ACP's `resource_not_found`: only `deleteSession` reads *no rollout found* | `:33537-33551`, `:37598-37620`, `:34115-34124` |
| `session/list` lists Codex's threads from the account's home, of every source (`cli`, `vscode`, `exec`, `appServer`), filtered by `cwd` | `:34421-34463` |
| `_session/steering` with a live turn calls `turn/steer` on it; with none it **starts a new turn**, and it has no `promptRequired` idle behaviour | `:38484-38538`; no `idleBehavior` in the bundle |

### 1.2 Codex's native `codex exec`, at the tag `rust-v0.159.3` (read)

Read from the maker's public source: `codex-rs/exec/src/cli.rs`, `lib.rs`, `exec_events.rs` and
`event_processor_with_jsonl_output.rs`.

| Finding | Where |
|---|---|
| `codex exec resume [SESSION_ID] [PROMPT]`, with `--last` (the newest recorded session in the `cwd`, or anywhere with `--all`), images, and the prompt read from stdin when it is `-` | `cli.rs:150-213` |
| `codex exec fork <SESSION_ID> [PROMPT]` forks a session into a new one | `cli.rs:154-180`, `lib.rs:1026-1090` |
| `--json` prints `thread.started {thread_id}` first, for a start and a resume alike: *"Can be used to resume the thread later"* | `exec_events.rs:11-43`; `event_processor_with_jsonl_output.rs:403-407`, `:606-613`; `lib.rs:1116` |
| An id that parses as a UUID is resumed as given (`thread/resume`); one Codex has no rollout for fails the run | `lib.rs:1838-1843`, `:993-1016` |
| **A name that matches no thread silently starts a new one**: `resolve_resume_thread_id` answers none, and the run calls `start_thread` | `lib.rs:1017-1025`, `:1844-1908` |
| The prompt is taken once, at the start: nothing can be added during the run | `lib.rs:890-921` |
| `--ephemeral` runs without persisting the session, so nothing can resume it | `cli.rs:35-37` |

### 1.3 What Codex does with `turn/steer` (read, partly)

STEER1 left this open (`docs/2026-10-03-steer-evidence.md` §5). At `rust-v0.159.3`: `turn/steer` appends the words to
the running turn's pending input (`app-server/src/request_processors/turn_processor.rs:1024-1080`;
`core/src/session/input_queue.rs:321-346`), and the turn's loop drains pending input at the top of each iteration,
before its next model request (`core/src/session/turn.rs:424-447`). So by default the words reach Codex **at its next
step**, as a second prompt reaches Claude Code. A step can also carry a `preempt` token that cancels a model request in
flight when user input arrives (`step_context.rs:23-24`, `turn.rs:1602-1609`); where it is set was **not read**, so
whether a steer ever cuts a step short is not settled. A review or compaction turn refuses a steer
(`ActiveTurnNotSteerable`, `turn_processor.rs:1101-1133`).

### 1.4 Claude Code's native door, 2.1.287 (read), and its protocol adapter 0.84.0 (read)

From `claude --help` on this machine, 2.1.287, and from `@agentclientprotocol/claude-agent-acp@0.84.0`
(`dist/acp-agent.js` sha256 `118db041…`, the build STEER1 read):

| Finding | Where |
|---|---|
| `-r, --resume [value]`: a conversation by session id. `-c, --continue`: *the most recent conversation in the current directory* | help |
| `--session-id <uuid>`: the caller names a new conversation's id. `--fork-session`: when resuming, a new id instead of the original | help |
| `--input-format stream-json` and `--replay-user-messages` (*re-emit user messages from stdin back on stdout for acknowledgment*) work only with `--print`; nothing in the help keeps `--resume` from riding with them | help |
| `--no-session-persistence`: nothing saved, nothing resumable | help |
| The adapter advertises `sessionCapabilities` `{additionalDirectories, close, delete, fork, list, resume}` and `loadSession` | `acp-agent.js:1243-1281` |
| A new ACP session's id is Claude Code's own session id (`options.sessionId`), so the id Daoris keeps is the one `claude --resume` takes. The one path where they part is a plan accepted with a fresh context, which is a permission's answer, and Daoris refuses every permission request (D52) | `:6386-6412`, `:6748-6752`; `exit-plan.js:91-131` |
| `session/list` asks the Agent SDK for the account's transcripts, newest first; a resume reads the transcript from any project folder, so a worktree's path need not match | `:1366-1389`; `resumed-session.js:37-53` |
| A resume of a conversation the account's home lacks is refused `resource_not_found` (-32002) | `:6767-6776` |

Neither the adapter nor the help says anything of two processes writing one conversation at once; the binary was not
read for it.

### 1.5 What was not measured

- **No turn ran.** Codex and `codex-acp` are not installed on this machine, so the brief's one short Codex turn was not
  taken. Claude Code is installed; reading its help made no model call, and no conversation was resumed.
- **Codex's steer** cutting a step short (§1.3), **Codex's lock** across processes (the adapter's sentence is read, not
  Codex's code), and **the native door's stream-json input** folding at the next step (STEER1 §4) are each one turn to
  measure, STEER3's, before the row that needs it is built (§8: MSG1h, MSG1i).
- **A resumed conversation in a tree other than its own.** The adapter allows it (§1.4); the model never asks for it
  (§2.3).

## 2. The model

**A person's words are said to a session, whatever its door and its state.** Daoris delivers them at the earliest
point the session's door allows, the session's record shows them the moment they are said with when they reach it,
and where it took them is recorded again under the same id (D136 §3). There are three reaches.

| Reach | When | Delivered | The conversation says, while it waits |
|---|---|---|---|
| `next-step` | its turn is running, on a door that takes words during a turn | at once, read at the agent's next step | *Held: it reads this at its next step.* (built) |
| `turn-end` | its turn is running, on a door that does not | held, then the next prompt of the same conversation when the turn ends | *Held: it reads this when its turn ends.* (built) |
| `resume` | it is parked or has ended | held; its record reopens and its harness conversation resumes with them as its next prompt | *Held: the same session goes on with this at the driver's next look.* (§5.1) |

### 2.1 While a turn runs

| Door | Reach | Status |
|---|---|---|
| protocol, an agent whose `initialize` says `promptQueueing: true` (`claude-agent-acp`) | `next-step`, a second `session/prompt` | built for a driven session (D136); a chat gains it (MSG1c) |
| protocol, `codex-acp` | `turn-end` today; `next-step` over `_session/steering` once one turn measures §1.3's open point (MSG1h). Never a second `session/prompt`, which resets Codex's turn (STEER1 §5) | |
| protocol, any other agent (dsh, the stub, a plugin's) | `turn-end`; only the agent's own `true` counts (D136 §2) | built |
| native, driven (`claude -p`) | `turn-end`: held while the process runs, then the conversation resumed with them before the record concludes (MSG1b); `next-step` once `--input-format stream-json` is measured on a driven run (MSG1i) | |
| native, a chat (stream-json stdin) | `turn-end`, as CONV4a holds it; `next-step` with MSG1i, where `--replay-user-messages` would say when a word was taken | |

**A session never concludes while words are held.** On the protocol door the held words are prompted in the same
process (D90, built). On the native door the process has exited, so the driver resumes the conversation with them
(`claude -p <words> --resume <id>`, ANSWER1a's arguments) under the same record, which stays `working`, and concludes
only when a run ends with nothing held. A word said as a session winds up is no longer refused: it waits for the
record's end and reopens it at once (§2.2), so *it stopped taking messages* (`work.steer.gone`) retires.

**A chat's *Stop turn* on the next-step door** stops the turn even while a word is on its way, since a person's stop is
never refused. That word may still be acted on with its answer dropped (STEER1 §3), so the conversation says of it
*it may have read this; its answer was not kept*. *Send now* on a driven session stays as D136 §4 has it.

### 2.2 Between runs: the record reopens and the conversation resumes

**Words to a parked or ended session reopen its record and resume its harness conversation, with every word held as
its next prompt**, in order, in one prompt: each its own text block on the protocol door, joined by a blank line on the
native door's argument. Nothing is composed around them: the conversation already holds its quest and its instruction
(D131 §1). Words said while the resumed run opens follow its door's reach (§2.1) after that prompt. This is ANSWER1's
mechanism (`Continuations.Judge`, `AcpSession.ResumeAsync`, `PrepareResume`), extended from an answered park to any
session of this machine the person writes to.

| The session | What its words do |
|---|---|
| **parked** (`awaiting-person`), driven | the answer, as D131 has it. A second word joins the first instead of replacing it (§2.4) |
| **completed or declined**: its quest closed | reopens. The quest stays as it closed: the session goes on in its tree, and its commits join its review (the main case: a session in *To review* the person writes to) |
| **stopped by the person**, its quest held by the stop | reopens, and releases the stop's hold as *Try again* does (`released`, D126 §3.4). *Try again* stays beside it: a fresh session |
| **failed, or cut off** (D80, D104) | reopens before the carry-on would start; like *Try again*, it forgives the failure it reopens (RETRY1's `forgiven`), and a parked quest's last session written to does what *Try again* does |
| **a chat that ended**, finished or stopped | reopens: a chat process on its own conversation, with the words as its first message |
| **stood down** | never. *It stood down: `#q` is someone else's, so it has nothing to go on with.* |
| **an intake** | never: an intake is answered through its ask (INT4h) |
| **a teammate's record** | never: its process and its conversation are on their machine and account. The page offers no box, and says whose machine |
| **Ask Daoris's own conversation** | not in this design: its panel opens a new one (HELP1). A row may extend it once the panel asks |

**The judgement, in order** (`Continuations.Judge`, extended). First what refuses before anything: a teammate's record,
an intake, a stood-down session. Then D131's codes, each known before a spawn: `ended` (a parked record a service from
before ANSWER1b already ended), `adapter`, `account`, `tree`, `unkept`, `unable`. Then the wire's: `offered`, `gone`,
`refused`, and a new code, **`elsewhere`**: the agent refused because another client holds the conversation, read from
`codex-acp`'s `data.reason: "thread_active_writer"` (§1.1), which is the agent's structured data and never its
sentence. A `codex-acp` thread Codex no longer has reads `refused`, since its error is Codex's own and not ACP's (§1.1).

**Where it cannot resume, what follows depends on what the session served.**
- **A taken quest**: D80's carry-on in a new session in the same tree, handed the words as D131 hands an answer and as
  DRIFT1b hands the ask's words, its note saying why (D131 §2). This is today's fallback.
- **An open quest**: the next start of it, handed the words.
- **A closed quest, or a chat**: nothing carries on by itself. The record stays ended with its words kept as said, and
  the conversation says *It cannot go on in this session, because …*, with one press, *Start a conversation with these
  words*: a new chat in the same repository whose first message is the words, its opening line naming the session they
  were written to. A new conversation has none of the old one's context, so it is the person's press.

**What holds the words rather than refusing them.** A driven session's words are taken up by the planner's look (the
`continuing` verdict, generalised from an answered park to a record with words waiting), and the shell nudges the
loop the moment they are said (`DriverLoop.Nudge`). Whatever holds a start holds a reopen, and the conversation names
it: the work's pause (D132), the repository's hold, the cap (a reopen goes before every start the driver planned
itself), and an account cooling (MSG1g). The words go when it lifts. A chat's words are taken up at once by the chat
runner, as a chat starts today.

**The account.** A conversation lives in the configuration home of the account it ran on (D131 §1), so a resume asks
the selection for its record's own account, as a chat's picker names one. Where that account is cooling, the words
wait for the reset its limit named (D125), and *Go on in a new session* is offered beside the sentence, which carries
on as above. Where it can no longer run there at all (signed out, or off the workspace's list, D130), the `account`
line applies at once. This amends D131, whose starts took whichever account the selection chose (MSG1g).

### 2.3 The record moves out of an ended state

The ledger gains **one move out of an ended state: to `working`, with the person's words waiting**, on the local host,
for a record this machine owns. No other move leaves an ended state, and nothing else can make it (`SessionLedger`).
`stood-down` never moves. What follows from it:

- **The record's history stays.** Its note gains *Went on with your words at …* and keeps what ended it; its events
  keep the run that ended, then the new one. The review counts from the record's own base commit, so its range is the
  whole conversation's (ANSWER1a's rule).
- **The strikes.** A reopened `failed` record no longer counts against its quest unless it fails again, which is what
  *Try again* does to a parked quest. A person's stop is no strike (D58), and reopening one holds that.
- **A reopened run on a closed quest** ends as its process does: in the state it had before its reopen when it exits
  cleanly, `failed` otherwise. Its quest does not move. It cannot park, since it holds no quest (D83); a question in its
  last words is answered by writing to it again. On a taken or open quest it concludes as any start does (D46 §4, D83).
- **The archive.** Reopening clears the record's archive mark: the person wrote to it, so it stays in view when it
  ends again. While live, a mark would move it nowhere anyway (D126 §5.2, ANSWER1c's note).
- **It travels as a move does.** The record's next revision goes up with the sync (SYNC4), and a teammate sees it move
  from ended back to working. Whether the remote's push route refuses a record that moves out of an ended state is
  MSG1a's to check, in a test.
- **The machine log** writes `session.reopened` once per reopen taken up, and `session.ended` again when it ends
  again; `session.started` is never written twice (§3.3).

### 2.4 The words are kept until taken: `said`

- **On the record.** The record's `answer` generalises to `said`: the person's words waiting for a parked or ended
  record to go on, a list in order, each with its id, when, and its files' names. It is answered to this machine only,
  as `answer` is (ANSWER1b), and never travels. A second word joins; it never replaces. `answer` stays on the wire as
  the words joined, for a client from before, and `POST /api/sessions/{id}/answer` stays as the parked case of
  `POST /api/sessions/{id}/say`. A move into `awaiting-person` clears `said`, as it clears `answer` today. Taken words
  leave `said` when the resumed run's first prompt goes.
- **On the ask** (D133): each word is kept on its ask once the session took it, as DRIFT1a2 keeps an added message,
  with a new kind, **`reopened`**: said to a session after it ended, which went on with it. Words a fallback handed to a
  new session are kept once that session took them. DRIFT1b's handing reads the new kind (*added after it ended*).
- **A bound on the native door.** A resumed native run takes its words as one argument, and Windows caps a command line
  at 32,767 characters (DRIFT1b). Until MSG1i hands them on stdin, the box refuses longer words on that door before
  sending, naming the bound, rather than cut them.

## 3. How it reads

### 3.1 One record, one conversation

Every word of the person's is in the record in order, where the session took it:
- **Said**: the moment it is said, a `user` event with `reaches` and an id (D136 §3), now on every door and state. A
  chat's waiting words, which today only the page holds as queued, become record events, for D136's reason: a restart
  loses what only the page holds. Words held by a pause, a hold, the cap or a cool-off carry a `why` beside `reaches`.
- **Taken**: the same words again under the same id, as that turn's ask, where the session took them: a resumed run's
  first prompt included (`ResumeAsk.Opening` gains the ids).
- **Went**: where a fallback handed them to a new session, a driver's note naming the words' ids and that session.

The page shows a said word **at once, in the turn it was said in**: during a running turn, inside it (D136); after the
record ended, as a new turn waiting at its foot. Once taken, it is that turn's ask and no longer waiting. Once gone
elsewhere: *Your words went to session `<id>`, because …*, the id a link. A word a record never took and never handed
on says *It ended before reading this* (built), and that now happens only where the person declines every press §2.2
offers.

### 3.2 The list

A record with words waiting and its run opening shows **going on** (继续中) under *Working*, as an answered park shows
*answered* (ANSWER1c); one whose words a pause, a hold, the cap or a cool-off holds shows under *Resumes later*, its line
naming what holds it. Both are `SessionGroups.Read`'s, so the screen and `daoris-driver sessions` agree (D126 §2.4).

### 3.3 The machine log (D94 §4)

| Event | Data | Why |
|---|---|---|
| `session.reopened` (new) | session, kind, adapter, from, resumed, why, door | an ended session the person wrote to, taken up: from which state, whether its own conversation resumed, and where not why by code (D131's, with `elsewhere`); `door` `screen` or `terminal`. Never the words |
| `message.sent` (gains a field) | session, kind, length, files, **reach** | how often people write mid-turn, at a turn's end, and between runs |
| `session.answered` | unchanged | a park's answer stays its own line |

A reopened record writes `session.ended` once per run that ends; a report counting sessions counts `session.started`.

## 4. Native and Daoris-managed

### 4.1 Two things

| | The harness's conversation (native) | Daoris's record (managed) |
|---|---|---|
| What it is | the agent's own history, in the account's configuration home: Claude Code's transcript, Codex's thread and rollout | the service's record, the events, the transcript and the kept id under the Daoris home |
| Who writes it | the harness, whoever runs it: Daoris's process, or the person's own terminal | the driver, from what its own runs saw |
| What it holds | every turn any client ran on it | the turns Daoris ran, and the person's words to them |
| Its id | the wire's: ACP's `sessionId`, the native `init` line's `session_id`, Codex's `thread.started` | the record's short id |
| Who reads it | the harness, on a resume | the page, the terminal, the review, a teammate (the record only) |

### 4.2 How they map

**One record per harness conversation** (D131 §3). A record keeps at most one conversation id, with the adapter that
opened it, kept the moment the wire says it and never read from the account's home (D131 §1). **A chat keeps its id
too** (MSG1c), on both doors, which is what lets an ended chat go on. A conversation Daoris opened belongs to the record
that opened it; a fallback is a new record because it is a new conversation.

**Daoris resumes only by the id it kept.** Never `claude --continue`, which takes the newest conversation in the
folder and may be the person's own; never `codex exec resume --last`; never a name, which `codex exec resume` turns
into a new thread when nothing matches (§1.2). On a native Codex door, should one be built, the resume is
`codex exec resume <thread-uuid> <words> --json` with the id from `thread.started`, and its words wait for the run's
end, since `codex exec` takes no input during a run.

### 4.3 The person's own terminal

**Opening Daoris's conversation in a terminal.** The person may, with the conversation's id and the account's home, as
with any harness conversation; Daoris neither offers nor prevents it. Turns run there are in the harness's conversation
and not in the record. A later resume carries them to the agent, and the record never shows them. Codex refuses a
second writer (`elsewhere`, §2.2); Claude Code was not read to (§1.4), so Daoris says nothing of a second writer it
cannot see.

**Bringing a terminal conversation into Daoris.** Only as a fork, never in place: `session/fork`, which both adapters
advertise, or `--fork-session` and `codex exec fork` on the native doors, opens a new conversation that a new record
owns, its first line saying which conversation it was forked from, and leaves the person's own untouched. In place, two
clients would write one conversation: Codex refuses that, and Claude Code would interleave two histories in one
transcript. Finding the conversation is the agent's `session/list`, never a read of the account's home (D66 §3). It is
on the same account, whose home holds it. It is held until a real use asks for it (MSG1k).

### 4.4 What is never claimed

- That the record holds every turn of the harness's conversation: it holds what Daoris ran.
- That the agent remembers a thing: a resume hands the agent its own history, which it may have compacted.
- That a conversation can resume before the agent says so: its refusal is the answer (D131).
- That a word reached the agent before the wire said it was taken: `reaches` is when it should; *taken* is the fact.
- That a native resume is the same process: it is a new run on the same conversation.
- That a fork, or a carry-on, is the same conversation.
- That a word on its way when a turn was stopped was not acted on (STEER1 §3).
- That a teammate's session can be written to.

## 5. The doors

### 5.1 The screen

**The box** (the composer at the foot of a session's page) is offered on every session that takes words, and absent on
the rest, with a line saying why (D119 §3.2). Its placeholder says what the words will do: the running door's sentence
(built), the parked answer's (built), or, on a session that ended, *write to it — the same session goes on with your
words*. On a parked session the box is the answer, as *Answer…* already opens it (D126 §3.1). **Send back…** in a
review (SURF6) opens the box on that session instead of the quest composer, which is what the glossary's *send back*
already means: *return a session's work to it with a note, so it carries on*. A quest that needs reopening is
DRIFT1e's, not the box's.

**Words.** *Resume* stays the code's word and the press that undoes a pause or a hold (恢复, D132); on the window a
conversation *goes on* (继续) or is *continued* (接续), never resumed.

| Key | Kind | en | zh |
|---|---|---|---|
| `work.say.placeholder` | placeholder | write to it — the same session goes on with your words | 写给它——同一个会话会带着你的话继续 |
| `work.conversation.held.resume` | sentence | Held: the same session goes on with this at the driver's next look. | 已暂存：同一个会话会在驱动的下一轮带着这条继续。 |
| `work.conversation.held.reopen` | sentence | Held: the same conversation goes on with this as it opens again. | 已暂存：同一个对话重新打开时会带着这条继续。 |
| `work.conversation.held.paused` | sentence | Held: it goes on with this once you resume its work. | 已暂存：你恢复它的工作后，它会带着这条继续。 |
| `work.conversation.held.hold` | sentence | Held: it goes on with this once {{repository}} is no longer held. | 已暂存：{{repository}} 不再暂停后，它会带着这条继续。 |
| `work.conversation.held.cap` | sentence | Held: it goes on with this when a running session here ends. | 已暂存：这里有正在运行的会话结束后，它会带着这条继续。 |
| `work.conversation.held.cooling` | sentence | Held: its account is cooling until {{time}}; it goes on with this then. | 已暂存：它的账户冷却到 {{time}}，届时会带着这条继续。 |
| `work.conversation.went` | sentence | Your words went to session {{id}}, because {{why}}. | 你的话转给了会话 {{id}}，因为{{why}}。 |
| `work.conversation.lost` | sentence | It may have read this; its answer was not kept. | 它可能已读到这条，但它的回复没有保留。 |
| `work.say.cannot` | sentence | It cannot go on in this session, because {{why}}. | 它无法在这个会话里继续，因为{{why}}。 |
| `work.say.startChat` | button | Start a conversation with these words | 用这些话开始对话 |
| `work.say.goOnNew` | button | Go on in a new session | 在新会话中继续 |
| `work.say.teammate` | sentence | This session ran on another machine, where its conversation is. | 这个会话在另一台机器上运行，它的对话在那里。 |
| `work.say.stoodDown` | sentence | It stood down: #{{quest}} is someone else's, so it has nothing to go on with. | 它已让位：#{{quest}} 归别人处理，它没有可继续的工作。 |
| `work.say.tooLong` | sentence | This agent's door takes at most {{count}} characters at once; shorten it, or send it in parts. | 这个智能体的接入方式一次最多接收 {{count}} 个字符；请缩短，或分几次发送。 |
| `work.shown.goingOn` | state | going on | 继续中 |

**A reason is chrome.** The page reads the code and says its own sentence (`work.say.why.<code>`), since the note's line
is English text that travels: `account` *its conversation stays with the account it ran on, and this start runs on
another* / 它的对话留在它运行时的账户里，而这次启动用的是另一个账户; `adapter` *it ran on {{from}}, and starts here
now run on {{to}}* / 它运行在 {{from}} 上，而这里现在用 {{to}} 启动; `unkept` *Daoris kept no id for its conversation* /
Daoris 没有保存它的对话编号; `tree` *its tree is gone* / 它的工作树已不在; `unable` *{{adapter}} cannot continue a
conversation* / {{adapter}} 无法接续对话; `offered` *the agent offers no way to continue a conversation* / 智能体不提供接续
对话的方式; `gone` *the agent no longer has its conversation* / 智能体已没有它的对话; `refused` *its conversation could not
be continued* / 它的对话无法接续; `elsewhere` *its conversation is open in another client of {{agent}}* / 它的对话正在
{{agent}} 的另一个客户端中打开. Each key enters the glossary's kinds and is held by its check (D116); the build owns the
budgets.

### 5.2 The terminal (D50)

```
sessions say <id> "…" [--file <path>]…
    say something to a session of this machine's: read at its next step or when its turn ends while it works,
    or, parked or ended, the same session goes on with it. Prints where the words stand.
```

- **It reaches the loop that runs the session** through the request folder D126 §7.1 built
  (`<home>/sessions/requests/<id>.json`, a request of kind `say`); the loop holds the words as the screen's box does and
  removes the request. Where no loop runs, a parked or ended driven session's words are written to the record's `said`
  for the next loop to take up, and a chat's are refused, since nothing here can open it.
- **It waits up to 10 seconds** and prints one line: *held: it reads this at its next step* (or *when its turn ends*),
  *going on: the same session took it*, *held: …* with what holds it, *went to session `<id>`, because …*, or the
  refusal.
- **Exit codes**: 0 taken or held, 1 refused, 2 could not. `daoris-driver answer` stays, as the parked case.

### 5.3 The routes

| Route | Status | Payload | Answers |
|---|---|---|---|
| `SESSION_INPUT` | extended | `{ id, text, files?, preface? }` | `{ sent, reaches, why? }` for every state: `sent` true whenever the words are held or taken; a refusal for §2.2's *never* rows, by its code |
| `SESSION_QUEUE` | extended | `{ id }` | adds `reaches` (what a word said now would do, or null) and `why` when nothing takes words, which is when the page draws the line instead of the box |
| `SESSION_START_FROM` (new) | new | `{ id }` | the *Start a conversation with these words* press: a chat in the session's repository, its first message the said words |
| `POST /api/sessions/{id}/say` | new, local mode | `{ text, files? }` | the service's half: keeps `said` on a parked or ended record of this machine; refuses a teammate's record, an intake and a stood-down session |

Each route has MOD5's three things: a handler, its row in the Desktop README, and a call from `bridge/sessions.ts`.

### 5.4 Ask Daoris (D110)

**Exempt, with its reason**: the words are the person's (D133 §1), so Ask Daoris composes none in their name. Its room
says where the box is and names `daoris-driver sessions say`; `HelpCoverageTests` holds the door as exempt.

## 6. What it amends

- **D131**: *reopening a finished record* moves from rejected to decided for the person's words alone (§2.3); a second
  answer joins the first (§2.4, amending ANSWER1b); a resume asks for its record's account and waits out a cool-off
  (§2.2); `elsewhere` joins the codes. Its points 1, 3 and 4 stand.
- **D90 and D136**: a session never concludes while words are held, on the native door too; a word said as a session
  winds up waits for the reopen instead of being refused; the next-step door reaches a chat.
- **D76's CONV4a**: on a next-step door a chat's words go at once; a chat's waiting words are record events.
- **D46 §4**: an ended state has one way out (§2.3). **D83**: a parked session's answer is its said words.
- **D80, D104 and D126 §3.4**: words to a stopped or failed session reopen it, releasing a stop and forgiving a failure
  as *Try again* does; *Try again* stays the fresh session.
- **D133 §1**: the `reopened` kind. **D94 §4**: `session.reopened`, and `message.sent`'s `reach`.
- **INT4i and D52 stand**: nothing is written into a stream that carries the driver's frames, every permission request
  is refused, and an intake takes nothing.

## 7. Not chosen

- **A new record that continues the old conversation, linked to it.** D131's rejected shape again: every reader learns
  a fold, one conversation has two homes, and the log counts two sessions where the person sees one. The cost of the
  chosen shape is one move out of an ended state, its strikes rule and its travel, each stated in §2.3.
- **Refusing words to an ended session**, which is today. It makes the person compose a quest, or start a fresh chat,
  to tell a session that just finished one more thing.
- **A fresh session for every message.** D90's rejection: it rebuilds from a summary what the conversation holds.
- **`--continue`, `--last` or a name** to find the conversation (§4.2).
- **Adopting a terminal conversation in place** (§4.3).
- **Reading the account's home** to see whether a conversation is there, before a resume or to list them (D66 §3,
  D131): the agent's refusal and its `session/list` are the answers.
- **A second `session/prompt` to `codex-acp`** (STEER1 §5), and **`_session/steering` on it before a turn measures it**:
  §1.3 leaves open whether a steer cuts a step, and a steer that lands after the turn ended starts a turn of its own
  (§1.1), so the run must know the turn is live.
- **Replacing the first answer with the second** (ANSWER1b as built): the person's words are all kept (D133), and a
  correction is read by the agent as a correction.
- **Waiting for the look without a nudge.** The look's interval is a countdown the person would watch.
- **Reopening the quest** when a done quest's session is written to. A correction to what was built reopening its quest
  is DRIFT1e's (D133 §5); the box only goes on with the session.
- **Keeping a reopened driven session open between words**, as a chat stays idle. Ending and resuming on the next word
  costs a spawn and a resume, and keeps one rule for when a driven record ends.

## 8. The build

Order: MSG1a, then MSG1b ∥ MSG1c, then MSG1d, then MSG1e ∥ MSG1f; MSG1g after MSG1b; MSG1h and MSG1i each after
STEER3 measures its turn; MSG1j last. Each row is its own branch. STEER2 (*Send now* over the steering extension) is
D136's and stands beside these.

- [ ] **MSG1a — the record keeps the person's words and reopens** (service). `said` replaces a single `answer`, and the
  ledger's one move out of an ended state takes words waiting, on this machine's record only. Contract: §2.3, §2.4,
  §5.3's `say`. Proof: `SessionLedgerTests` (stood-down, a teammate's and no words refused), `LocalHostTests`, a pushed
  reopened record in `SessionSyncTests`.
- [ ] **MSG1b — a driven session goes on with words, on both doors** (driver). The planner's `continuing` verdict takes
  a record with words waiting, the judgement gains §2.2's rows and `elsewhere`, and a native run resumes while words
  are held. Contract: §2.1, §2.2, §2.3. Proof: `ContinuationTests`, the plan tests, `NativeResumeTests`, a `Process`
  tick (the parent's).
- [ ] **MSG1c — a chat keeps its conversation and goes on** (driver). A chat keeps its id on both doors, an ended chat
  reopens with the words, and a `promptQueueing` chat takes words at the next step. Contract: §2.1, §2.2, §4.2. Proof:
  `HarnessConversationsTests`, chat tests on a protocol stub, the stop's *not kept* line.
- [ ] **MSG1d — every door's words reach the record** (modules). `SESSION_INPUT` and `SESSION_QUEUE` answer for every
  state, the loop is nudged, said, taken and went events are written, and the log gains its lines. Contract: §3, §5.3.
  Proof: `DriverModuleConversationTests`, `DriverModuleAddedTests`, `SessionEventsTests`, `SessionLogTests`.
- [ ] **MSG1e — `daoris-driver sessions say`** (driver). The verb through the request folder, its one line and its exit
  codes, and Ask Daoris's exemption. Contract: §5.2, §5.4. Proof: `SessionsCommandTests`, `HelpCoverageTests`, the
  room's goldens.
- [ ] **MSG1f — the box on every session that takes words** (web-shell). The box and its sentences per reach, the
  line where none, *going on*, the went link, *Start a conversation…*, and *Send back…* opening the box, in both
  catalogues. Contract: §3.1, §3.2, §5.1. Proof: stories, vitest, the glossary check, the look.
- [ ] **MSG1g — a resume asks for its own account** (driver). The selection names the record's account, a cool-off
  holds the words with *Go on in a new session*, and an account that cannot run there carries on at once. Contract:
  §2.2's account paragraph. Proof: plan tests, `AccountRotationTickTests` (the parent's).
- [ ] **MSG1h — Codex hears words at its next step** (driver; after STEER3's Codex turn, which needs Codex installed).
  The next-step door for `codex-acp` over `_session/steering`, steering only while a turn is live and waiting for a
  turn it started. Contract: §1.3, §2.1. Proof: STEER3's measurement, then `AcpSteerTests` rows.
- [ ] **MSG1i — the native door hears words at its next step** (driver; after STEER3's native turn, with
  `--replay-user-messages`). Driven and chat runs that take words on stdin, which removes the argument's bound.
  Contract: §2.1, §2.4. Proof: STEER3's measurement, then native tests.
- [ ] **MSG1j — the canary on the install** (the parent's, after a republish carrying a–f). A session in *To review*
  written to goes on in its own row; a stopped one too; a chat from yesterday goes on. Contract: §9. Proof: the run,
  `session.reopened` with `resumed` true.
- [ ] **MSG1k — a terminal conversation forked into Daoris** (held until a real use asks). Contract: §4.3. Proof: a
  protocol stub that speaks `session/list` and `session/fork`.

## 9. What only a real run and the window can prove

- That `claude-agent-acp` resumes a conversation that had **ended**, not only a park (ANSWER1d is the park's canary),
  with Daoris's rules and servers handed again.
- That `codex-acp` resumes a thread across a process, on a machine with Codex (§1.5).
- That the native door's `--resume` continues a driven conversation (ANSWER1a held it by its arguments).
- The box, the held sentences, *going on* and the went link on the window, at every width, in both themes and
  languages.

## 10. What this document's gate does not cover

Documents only, and nothing is built. The harness findings were read from the shipped packages and the maker's source
at the versions in §1, and from one binary's help; no turn was run, and Codex is not installed here. The statements
about today were read from the code at `14338bff`. `verify` checks this document's links and D137's place and shape,
and none of their words.
