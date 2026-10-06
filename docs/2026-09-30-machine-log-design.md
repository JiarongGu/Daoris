# The machine log: what happens on this machine, kept to improve Daoris from (LOG1)

**Status: the contract for LOG1a–d, recorded as D94 before any code.** The owner, 2026-09-30:
*"also setup proper logging system to moniter my use in local daoris and we can improve the system by
this way"*. It extends the interactive design's rule that what a machine produces stays on it
(`2026-09-20-interactive-design.md` §2, D47 §4).

## 1. What was there

Measured on the installed application, 2026-09-30:

| Process | Where its own lines went |
|---|---|
| The shell (`app/Daoris.Desktop.exe`): the window, the modules, the driver loop | A logger with no provider that writes anywhere a person can read, and the console panel's in-memory buffers, gone at exit |
| The HTTP host (`daoris-knowledge-http`) | Its standard output, which the shell does not keep |
| The browser (`daoris-browser`) | The engine's own `engine.log` in its profile, and nothing of Daoris's |
| The headless driver (`daoris-driver`) | Its console, when someone started it from one |
| The knowledge host each session starts (`daoris-knowledge`, MCP) | Its standard error, which the agent that started it owns |

Only the session transcripts (`sessions/<id>.log`) and the records survived. An unhandled exception
left no trace, and a question such as "how long does a conversation take to open?" (HELP4: about six
seconds, found by a person noticing) had nowhere to be answered from.

## 2. The files

- **Under the home, in `logs/`**: `<date>.<source>.jsonl`, one file per process kind per day, the
  date in UTC (`2026-09-30.desktop.jsonl`, `2026-09-30.host.jsonl`). One file per source because two
  processes appending to one file can overwrite each other's lines, and because the artefacts share
  no code (the twins rule): each writes its own file with its own writer, and **this format is the
  contract between them**.
- **Sources**: `desktop` (the shell, its modules and its driver loop), `host` (the HTTP host),
  `mcp` (the knowledge host each session starts, whose standard error belongs to its agent),
  `browser` (Daoris's browser), `driver` (the headless driver).
- **Kept for 30 days**, then deleted by the next writer to start. A file stops growing at 20 MB, with
  a last line saying so, so a loop that logs without end cannot fill a disk.
- **No home, no log.** A process with no `DAORIS_HOME` writes nothing and starts anyway: the log is
  evidence, never a reason for Daoris not to run. A write that fails is dropped and never thrown.

## 3. A line

One JSON object per line, UTF-8, LF:

```json
{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"info","event":"session.opened","data":{"session":"76cdd5db","adapter":"claude-code-acp","openMs":5840}}
```

`time` is UTC with milliseconds; `level` is `info`, `warn` or `error`; `event` is a name from the
catalogue (§4); `data` is an object whose values are strings, numbers, booleans or null, never
nested. A reader skips a line it cannot parse and a field it does not know.

## 4. The events

| Event | Source | Data | Why it is kept |
|---|---|---|---|
| `app.started` / `app.stopped` | every | version, installed, uptime on stop | a period of use, and a version to blame |
| `error` | every | where, type, message, stack | an unhandled exception, which left no trace |
| `log` | every | category, message, exception | the framework's own warnings and errors |
| `session.started` | desktop | session, kind, adapter, repository, workspace; setup, only for a set-up's session | what the person runs, on what, and where (WSSETUP11) |
| `session.opened` | desktop | session, adapter, openMs | spawn to ready: what a person waits through |
| `turn.answered` | desktop | session, firstAnswerMs | send to the first word back |
| `turn.ended` | desktop | session, stopReason, turnMs, input, cacheRead, cacheWrite, output, calls, used, size | how long a turn takes, how it ends, and what it consumed: its tokens as METER1 splits them, its tool calls, and its context against the window (WSSETUP11) |
| `session.parked` | desktop, driver | session, kind, repository, workspace | a session that stopped to ask the person (D83): the owner's complaint, counted per week by workspace (WSSETUP11, D124 §7.3) |
| `session.ended` | desktop | session, state, seconds | how it finished |
| `session.answered` | desktop, driver | session, adapter, resumed, why | an answer to a parked session taken up (D131 §2): whether its own conversation resumed, and where not, why by a code (`account`, `adapter`, `unkept`, `tree`, `unable`, `offered`, `gone`, `refused`, `ended`), never the answer |
| `session.deleted` | desktop, driver | session, kind, door | a conversation that served no quest deleted (SESSUX1f, D126 §5.4): its record and what this machine kept of it are gone, so the log keeps that it happened, never a word of it; `kind` is the door's (`chat`, `help`), `door` `screen` or `terminal` |
| `sessions.archived` | desktop, driver | count, door | sessions archived (SESSUX1g, D126 §7.4): an archive changes no work, so it is counted, never listed; `door` `screen` or `terminal`, and an archive that took nothing writes nothing |
| `history.cleared` | desktop, driver | scope, quests, asks, sessions, forgotten, bytes, kept, door | finished history cleared from this machine by a press (HIST1c, HIST1d, D153; the history-clearing design §6.5): `scope` `quest`, `ask`, `failed` or `workspace`; how many quests, asks and session records went (a teammate's copies counted), how many of the quests a remote keeps and this machine forgot, the bytes the home freed, how many listed units changed since the list and stayed, and `door` `screen` or `terminal` (`daoris-driver history clear`, `quest clear`, `ask --clear`); never an id, a title or a word. A clear that took nothing writes nothing |
| `work.paused` / `work.resumed` | desktop, driver | scope, stopped; or scope, released; door | an ask's or a quest's work paused or resumed on this machine (PAUSE1b, D132 §4.3): `scope` `ask` or `quest`, how many sessions the pause stopped or the resume released, `door` `screen` or `terminal`; never the id. A pause leaves nothing in `driver.json` once resumed, so these and the stopped records are its whole trace, and a pause with nothing to hold writes nothing |
| `work.abandoned` | desktop, driver | scope, declined, discarded, branches, archived, kept, lost, door | an ask's or a quest's work abandoned on this machine (PAUSE1d, D132 §4.3): how many quests it declined, trees it discarded with their branches, branches it deleted alone, sessions it archived, pieces it kept (by design, changed since the list, or not reached), shared declines another machine's take beat to the remote, and `door` `screen` or `terminal`; never an id, a branch or the reason, which `<home>/abandoned.json` keeps. An abandon with nothing listed writes nothing |
| `registry.followed` | desktop, driver | repository, outcome | a registration followed from a repository's line, by a word from a fixed list (`registered`, `unchanged`, `declares-nothing`, `not-set-up`, `no-line`, `unreadable`, `lanes-unreadable`, `worktree`, `no-checkout`, `not-on-registry`, `refused`), never the sentence its row says: whether set-ups reach the registry once they land (WSSETUP5, D124 §3.4) |
| `landing.auto` | desktop, driver | session, repository, workspace, code, commits, uncommitted, plugin, pushed | each try to land a done session under a rule that accepts automatically (LAND2b, D145 point 6), by its code (`landed`, `nothing`, `held`, `uncommitted`, `exists`, `plugin-unready`, `plugin-failed`, `already`, `superseded`, `gone`, `undone`, `off`, `refused`; `advanced` is LAND2c's): the commits it carried, the paths that held it, the plugin's id and whether it pushed, never a sentence, a branch or a path |
| `setup.planned` | desktop, driver | workspace, repositories, atOnce, pilot | a workspace plan written by its press: how many it set out to set up, at what pace (WSSETUP6, D124 §4.1) |
| `setup.published` / `setup.skipped` | desktop, driver | workspace, repository, quest; or refusal, by the press's code | each set-up a plan asked, and each repository its press refused at its turn, by a word, never the sentence |
| `setup.paused` / `setup.resumed` / `setup.stopped` | desktop, driver | workspace; `by` when paused (`person`, `pilot`, `tool`) | how a plan was steered: whether the pilot's pause is taken up, and how often a plan is stopped |
| `account.limited` | desktop, driver | session, adapter, account, hit, window, until, stated, assumedZone, turn, used | an account's limit met (TOOL4d, D125 §5.4): which account, which window, until when, said or defaulted, at which turn and context |
| `starts.waiting` | desktop, driver | adapter, account, workspace, until, quests, signedOut | every account a start may use was cooling or not signed in, written once per wait; `signedOut` the accounts it passed not signed in, profile names joined by commas, or null; `until` null where none cools (TOOL6g) |
| `account.rotated` | desktop, driver | session, adapter, from, to, carries, why, scope, said, fromSaid, toSaid | a start that ran on another account of its scope's list than the one the scope begins at (TOOL4f, D125 §5.4; TOOL6b, D130 §13 as §16 amends it); `carries` is the cut-off session a carry-on carries on, or null; `why` the step that moved it, `scope` the workspace whose list it was, `said` whether any account had said what it has left, `fromSaid` and `toSaid` what each of the two said, by Daoris's word (TOOL6c) |
| `refused` | desktop | code, request | a refusal the person met, by its catalogue code (REFUSE1) |
| `permission.refused` | desktop | session, adapter, tool, kind, by | a permission a session's harness would have asked a person for, refused because nobody is at the prompt (UNBLOCK5, D122 §3.10): asks per session, before and after a repository declares its safe work |
| `preview.opened` | desktop | session, path | a file read for its preview (D111), whether the side bar's reading room is used (LEFT2) |
| `view.opened`, `command.run`, `panel.moved` | desktop, from the page | the view, the command, the region | what is used, and what never is |
| `message.sent` | desktop, from the page | session, kind, length, files | how the conversations are used |
| `proposal.settled` | desktop, from the page | applied | whether Ask Daoris's proposals help |
| `page.error` | desktop, from the page | where, message | a render or request failure the page caught |
| `request.failed` | host | method, route, status, ms | a request that failed, or took over two seconds |
| `tool.download.started` / `.verified` / `.refused` / `.stopped` | the process that runs the download (D121 §3.6, TOOLS4) | tool, version, check when refused | a managed version fetched, and the check that refused it |
| `tool.location.fetched` / `.failed` | the process that looks (D121 §3.7, TOOLS4) | position in the list, versions or check | a resource location looked at, by its place and never by the address the person typed |
| `tool.used` | desktop (D121 §3.6, TOOLS7) | tool, way, version when managed | a tool's way set from Settings → Tools, never the path of a file the person named |
| `plugin.started` | desktop, driver | plugin, points, ms, by | a plugin's process up: what it listens on, how long its handshake took, and whose it is (D119) |
| `plugin.stopped` | desktop, driver | plugin, why, by | why a plugin's process went: off, removed, updated, changed, or its owner ended |
| `plugin.called` | desktop, driver | plugin, point, answer, ms | each answer at a point, by its word, and how long it took |
| `plugin.failed` (warn) | desktop, driver | plugin, where, kind, code, ms, by | a plugin that could not start, exited, or answered late, unreadably or with an error |
| `plugin.served` | desktop, driver | plugin, server, session, handed | which sessions each plugin's server was handed to, or withheld from |
| `plugin.tried` (warn when failed) | desktop, driver | plugin, passed, checks, failed, ms, door | a trial of an installed plugin with the kit, from the screen or a terminal |
| `plugin.tested` (warn when failed) | desktop, driver | plugin, passed, code, ms, door | a run of a plugin's own tests (its runner is PLUGUI1g's; the shape is reserved) |
| `update.staged` / `update.draining` | desktop | build, version / build, driven, turns | a build staged beside the install, and the drain it began with what still ran (UPDATE1, D139) |
| `update.requested` | desktop, driver | mode, door, build | the person's word on it: `when-idle`, `now` or `not-now`, from the `screen` or the `terminal` |
| `update.applying` | desktop | build, version, by, waitedSeconds | the application closing for it, `by` `idle` or `now`, and how long the drain waited |
| `update.installed` / `update.rolled-back` (warn) / `update.refused` (warn) | desktop | build, version, confirmed / build, reason | how the swap ended, said once at the start after it; a refusal by the application's own check is said as it happens, its reason the check's code |

**The page reports through the bridge**, one request (`DAORIS.LOG` · `EVENT`), and the module takes
only the page's events above with only their fields: anything else is dropped. That is where the
list of what is never logged is enforced, since the page is the one writer that could pass a word
through by mistake.

**As built (LOG1b)**, measured against the code and its tests:

- **Where the session lines come from.** The service client says when a session record opens and moves
  (every session on the machine is opened through it), and the conversation record says when each event
  lands, with its stamp; `SessionLog` in the driver library turns the two into lines. The shell's driver
  loop writes them to `desktop`, the headless driver's watch, `--once` and `--until-idle` to `driver`;
  `daoris-driver chat` writes none yet.
- **`session.started`**'s `kind` is the door's: `driven`, `chat`, `intake` or `help` (the record calls the
  last two chats); `adapter` and `repository` are the record's.
- **`session.opened`**'s `openMs` is the open to the session's first prompt in its record. Where the
  first words wait for the protocol door to open (Ask Daoris, a message sent while a chat opens), that
  is the wait to ready.
  Where the person types only after the chat opened, it includes their pause. A driven session's target
  is recorded as its process starts, before the protocol is ready, so its `openMs` is the wait to spawn
  and its first `turn.answered` carries the handshake.
- **`turn.answered`** is a prompt to the first message, thought or tool call after it; **`turn.ended`** is
  a prompt to its turn's end, a turn opened by two prompts (a target and the person's answer) timed from
  the first. **`session.ended`** is written on a closed state (completed, declined, stood-down, failed,
  stopped), `seconds` from the open this process saw. A time that cannot be known (an open never seen, a
  turn's end with no prompt before it) is null, never zero.
- **`refused`** comes from a middleware in the dispatcher's application slot, so every module's answer
  passes it: `code`, and `request` as `MODULE.TYPE`. A Daoris refusal is `info`; the kit's own codes (an
  unexpected exception, a type or a module the shell does not have) are `warn`; a cancelled request is
  not written. The refusal's parameters and sentence never are.
- **`preview.opened`** (LEFT2) is written by the shell's `SESSION_FILE` route when a file was read for its
  preview, never by the page, so the module's filter below does not list it. `path` is the file relative
  to the session's tree, with forward slashes, as the preview names it: never the tree's own path, and
  never the file's words. A preview that was refused writes nothing; its refusal is `refused`'s. A file
  read from a landed branch once the tree is gone (D113) writes the same line, its path relative to the
  repository, and never the branch's name, which carries a title's words.
- **The page's fields** each have a kind: a name (`view`, `command`, `region`, `session`, `kind`,
  `where`: an identifier, never a sentence), a text (`message`), a count (`length`, `files`) or a flag
  (`applied`). A value of another kind is dropped, and a string is cut at 120 characters with an
  ellipsis. `message.sent`'s `kind` is `chat`, `steer` (a word to a driven session) or `help`, and a help
  message that opens a conversation has no `session` yet. `proposal.settled` is the person's Apply or Not
  now once it landed and settled the proposal. A sync card's look (HELP10) settles nothing: its answer says the
  card `stands` for the press that does, and it writes no line (LEFT3); a look that found nothing to do settled
  the card, and is written as any Apply is. `page.error` is `error`-level, `where` `window` or `promise`.

**As built (UNBLOCK5): `permission.refused`**, measured against the code and its tests:

- **One line per refused call, the first time.** `SessionLog` writes it when the conversation record marks
  a call `refused`. A repeated update to that call is not a second ask, and a call that merely failed is
  none. The protocol door marks a call `refused` when the agent reports as failed a call whose permission
  request the driver refused (D52, HELP4). The native door's mapper marks it from the harness's `system`
  `permission_denied` frame or the result's `permission_denials`, whichever says it first, and then reads
  that call's failed result as `refused` too, so the conversation says it the same way on both doors.
- **The fields.** `kind` is ACP's tool kind, from the refusal or else from the call's first event. `tool`
  is the tool's name (`Bash`) and `by` the wire's `decision_reason_type` (`rule`, `mode`, `classifier`,
  `asyncAgent`); only the native door carries them, and the protocol door's are null, since its wire names
  neither. Each is written only if it is an identifier, so a command or a sentence put where a name goes
  is null. `adapter` is null for a session whose open this process never saw.
- **Not counted.** A subagent's refusal: its calls run beside the session (CONSOLE3c), so the native door
  says it on the session's console and makes no card, and on the protocol door its call's update is the
  subagent's stream's. A refused request whose call the agent never reports on.
- **Not measured.** The native door's two frames are written from the maker's reference (the Agent SDK's
  TypeScript reference, read 2026-10-01: emitted by a `-p` run with no permission host since 2.1.223,
  best-effort, the result's list authoritative) and labelled so in `ClaudeStreamJsonTests` until a turn
  shows them. What an auto-mode classifier block looks like on the protocol wire is the canary's
  (D122 §3.10).

**As built (WSSETUP11): parks, set-ups and what a turn consumed** (D124 §7.3), measured against the code and its
tests (`SessionLogTests`, `AttentionTests`, `SetupQuestsTests`):

- **`session.parked`, where the park is made.** `SessionLog` writes it when the service client moves a record into
  the state `SessionStates.IsParked` names. Only the driver moves a record there (a driven session's conclusion,
  D83, and an intake's), always through that client, so the line is written in the shell and the headless host
  alike, in every mode. The attention watch says the same park from the active list a look later; it is not the
  writer, since its first look is a baseline that would drop a park made just before a restart, and the headless
  `--once` and `--until-idle` run none. Both read the one predicate, and their tests hold the same rows. `kind`,
  `repository` and `workspace` are what the session's open said; each is null for a session whose open this process
  never saw. A park is no ending, and neither is the answer that keeps it (ANSWER1b, D131): the record ends, and
  `session.ended` is written, when its resumed conversation does, or when the driver ends the park to carry the
  answer on in a new session.
- **`session.started`'s `workspace`** is the record's, the circle its repository is wired into on this machine
  (D48). **`setup` is true** only for a driven session whose quest is a set-up (`SetupQuests`: a title the set-up
  press composes, D117 §6.2 and D124 §2.1–§2.2, with or without its day), and absent on every other line. The
  driver passes it with the open, and the ledger is not told.
- **`turn.ended`'s counts.** `input`, `cacheRead`, `cacheWrite` and `output` are the turn's tokens as its wire
  reported them for the whole turn (CONV5), each null where it said none. `calls` is the tool calls first seen since
  the last turn ended: a call's later updates are the same call, and a call with no id is one each. `used` and
  `size` are the context at its high-water within the turn and the window, from the turn's usage reports, null where
  it had none.
- **Not covered by a fast test**: the driver's open passing `setup` is reached only by a real tick (the `Process`
  half). Its decision, `SetupQuests.IsSetup`, and the client and log beneath it are held in the fast half; the
  first real set-up is LAYOUT7's, whose family rehearsal runs one.

**As built (WSSETUP5): `registry.followed`** (D124 §3.4), measured against the code and its tests (`SessionLogTests`,
`RegistrationFollowTests`): every follow goes through the service client's registry door, which raises what it came
to, and `SessionLog` writes it in the shell and the headless loop alike; `daoris-driver register` and a terminal's
`trees land` or `trees sync` write it into the host's own log the same way. One line per repository per follow: its
name and the outcome's word, never the sentence its row says, a path, or a commit.

**As built (WSSETUP6): the `setup.*` lines** (D124 §4.1), measured against the code and its tests (`SessionLogTests`,
`WorkspaceSetupTests`): a workspace plan reads its facts through the service client, which raises each line, and
`SessionLog` writes it in the shell and the headless loop alike; `daoris-driver setup --workspace` writes its press, pause,
resume and stop into the host's own log the same way. The catalogue is `SetupLine`'s, so every writer writes the same
fields: names (the workspace, the repository, the quest's id), words (a refusal's code from the press's list or
`service-refused`, a pause's `by`) and counts, never a sentence or a path. A pilot's pause and a tool's are written once,
when the plan pauses itself; a resume only when it lifted a pause.

**As built (TOOL4d): the account lines** (D125 §5.4), measured against the code and its tests (`AccountLimitHoldTests`):
the driver says each through the service client (`AccountSaid`), and `SessionLog` writes it in the shell and the headless
loop alike, the catalogue `AccountLine`'s. `account.limited` is written at the conclusion of a driven session or an
intake whose door's failure the agent's table read as a limit, and at a conversation's refused turn: `turn` is the turns
its record ended plus one, `used` its context where the door reported one, `hit` and `window` the marker's own words,
cut to nothing past 40 characters. `starts.waiting` is written the first look a start is held on a cooling account, and
not again until a new cool-off on that account; `quests` counts the starts it held then, an ask's intake among them, and
`account` was added beside the design's fields. An account is its profile name, null for the tool's own home, and a
name that is not an identifier is null: never a key, a key's handle, who signed in or the agent's sentence. A
conversation's line is written by the shell, whose chat runner shares the loop's client; `daoris-driver chat` writes
none, as it writes no session lines.

**As built (TOOL4f): `account.rotated`** (D125 §5.4), measured against the code and its tests (`AccountRotationTests`):
written once a rotated start's record is open, through the same client and the same writer, by one helper
(`RotatedOpening.Say`) that a driven start, a carry-on, an intake, a conversation and Ask Daoris's opening all call.
`from` is the account the start's default named, `to` the one it ran on, each a profile name or null where it is not
an identifier; `carries` is the cut-off session's id for a carry-on and null for any other start. A start that waits
writes `starts.waiting` and no `account.rotated`, and over an order its `account` is the one whose reset ends first.

**As built (TOOL6b): `account.rotated`'s `why`, `scope` and `said`** (D130 §13 as §16 amends it), measured against
`AccountRotationGoalTests` and `AccountRotationTests`: `from` is where the start's scope begins, its default or else its
list's first, and the line is written whenever the start ran elsewhere, under `use: goal` as under `order`. `why` is the
step that moved it, by its word: `kept`, `cooling`, `refused`, `signedOut`, or the goal's `fewest`, `lapsing` and
`leastRecent` (`near` and `pace` arrive with TOOL6c). `scope` is the workspace whose list it was, null for the machine's
or where it is not an identifier. `said` is false while no door carries the agent's word about its windows, which today
none does. A start the goal chose on the account its scope begins at writes no line; its record's first line names the
step.

**As built (TOOL6c): `account.rotated`'s `near`, `pace`, `said`, `fromSaid` and `toSaid`** (D130's TOOL6c note), measured
against `AccountRotationTests`, `AccountRotationGoalTests` and, in the `Process` half, `AccountReadingTickTests`: `why` is
also `near`, an account its agent said is near was passed, or `pace`. `said` is true where any account of the list had a
reading in `windows.json` still before its reset. `fromSaid` and `toSaid` are what the account the start was moved off and
the one it ran on said, by Daoris's word only: `refused`, `near` (by the agent's word, its credits, or a window at or over
the scope's *near*), `clear`, or null where it said nothing; never a number, a reset or the agent's own words. No
`account.near` line is written: near by number is each scope's own threshold.

**As built (PLUGUI1d): the `plugin.*` events** (D119 §4.2), measured against the code and its tests
(`HookSetLogTests`, `PluginHealthTests`):

- **One writer.** `PluginLog` in the driver library writes every line and feeds the loop's record of the
  plugin's health (`PluginHealth`) from the same call. The hook set writes the loop's, `LandingPlugins` a
  landing's (`by` `landing`) and a hand-off's (`hand`), the driver the servers it hands a driven or intake
  session, and the terminal's `plugins try <id>` an installed plugin's trial. A folder's trial is never
  written, since its id may name an installed plugin it is not.
- **Names, counts, flags and times only.** The answer is its word (`allow`, `hold`, `answered`, `pushed`,
  `not-pushed`), never its reason or message. A value that is not shaped like a name is written as null.
  `points` is what the process answered the handshake with, joined by commas. `ms` is the start to the
  handshake, or a call to its answer or failure.
- **`by` on `plugin.stopped` and `plugin.failed` too**, beside `plugin.started`'s: the health record needs it
  to tell the loop's process from a landing's one frame.
- **Why a stop.** The catalogue decides: gone is `removed`, switched off is `off`, another version or a
  replaced install folder is `updated`, anything else is `changed`, and the loop's end or a landing's frame
  done is `ended`. A stop that a removal or an update asks for first (`HookSet.StopAsync`) is written at the
  loop's next look, which the route asks for at once, because only then does the catalogue say which.
- **A failure's kind** is marked where the wire throws it (`PluginFailures`), never read from its sentence:
  `unstartable` for a start with no mark, `errored` for a call with none. A process found gone at a look is
  `where` `process`, `kind` `exited`, with its exit code. A start that fails the same way at every look is
  one line until it starts or fails differently. A plugin whose start was failing and which is then switched
  off or removed gets a `plugin.stopped`, so its record starts afresh.
- **Not yet written**: a conversation's servers, and a trial from the screen's door. Both wait for the shell
  to hand its log to the chat runner and to `PLUGIN_TRY`, a modules change PLUGUI1d did not make.
  `plugin.tested` has its shape (`PluginLog.Tested`) and no writer until PLUGUI1g's runner.

## 5. What is never logged

**Anyone's words**: a message, a prompt, what an agent said, a tool call's input or output, a quest's
title or body, a search. **A file's contents. Any secret**: a key, a token, a header, a sign-in. **An
address**: a URL's query, a page the browser visited. Words are already kept where they belong (the
session's record and transcript), and a log that held them would be a second copy with none of the
record's rules. Paths do appear (a stack, a repository's tree): the log never leaves the machine, like
the transcript beside it.

## 6. Reading it — two doors, and a report

- **At a terminal**: `daoris-driver logs [--since <30m|2h|3d>] [--source <name>] [--event <name>]
  [--level <warn|error>] [--json]` prints the lines across every source, merged by time.
- **On the screen**: a *Logs* domain in Settings (desktop only) shows the recent lines with the same
  filters, and opens the folder.
- **For improving Daoris**: `tools/usage-report.mjs --install <dir> [--days 7]` reads an install's
  logs and prints what was used most, what was refused, what failed and what was slow. A development
  session starts from it instead of from a guess. Since LEFT3 what was used most includes the side bar's
  previews (`preview.opened`): how many, in how many sessions, and of what kind by extension, never the path.
- **Never over HTTP.** A browser, and a remote, see none of it (D47 §4). The host writes its file and
  serves no route onto any of them.

**As built (LOG1c)**, measured against the code and its tests:

- **One reader for both doors**: `MachineLogReader` in the driver library, which the headless driver's
  `logs` and the shell's `DAORIS.LOG` · `LINES` both call, so a filter means the same at each. It reads
  only the log's own files (`<date>.<source>.jsonl`), leaves a day's file older than `--since` unopened,
  and merges every source's lines by time. **A line is kept** when it is a JSON object whose `time` is
  UTC in the §3 shape (a time with no `Z` is skipped, because the report's JavaScript would read it as
  local time) and whose `source`, `level` and `event` are strings; `data` that is absent or no object is
  read as none, and a field nobody knows is ignored. **Anything else is skipped and counted**, a torn
  last line included; a blank line is neither. The table of those cases is a twin's: the usage report
  (LOG1d) reads the same lines with its own code, and `usage-report.test.ts` holds the same rows as
  `MachineLogReaderTests.Parsing`.
- **The filters**: `--since` is a span back from now in minutes, hours or days (`30m`, `2h`, `3d`);
  `--source` one of the five; `--event` a name; `--level` a floor (`warn` is warnings and errors). A flag
  the reader cannot use is a sentence and exit 2, never read as no filter; no home is the driver's usual
  sentence. The terminal prints the lines oldest first, as `2026-09-30 07:10:00.123Z  host     warn
  request.failed  method=GET status=500` (a value with a space, a quote, an `=` or a line break quoted),
  or as written with `--json`; that nothing matched, and how many lines were skipped, go to standard
  error, so standard output stays the lines.
- **The screen**: Settings → Logs, a machine domain, so a browser is offered none. `LINES` answers the
  newest lines first, 200 unless the page asks otherwise and never more than 1000, with the folder, how
  many matched, a count per level (taken before the level filter, so a person narrowed to errors still
  sees the warnings) and the period's events (before the event filter). The domain opens on the last
  day of every source; its periods are the last hour, day, 7 days and 30 days. **Open the folder** is
  `OPEN_FOLDER`: the shell opens the home's `logs/` through the window kit's shell launcher, making it
  first if nothing has written there, and the page never names a path. A filter the reader cannot use
  is refused as `LOG_FILTER_UNKNOWN`, a folder the system would not open as `LOG_FOLDER_NOT_OPENED`.

**As built (LOG1d)**, measured against the code and its tests:

- `node tools/usage-report.mjs --home <dir> | --install <dir> [--days 7] [--json]`, where `--install`
  is the install's `data/` home. It reads the period's lines with its own code (the twin table above)
  and prints six parts: the **lifecycle** (starts, stops, the uptime `app.stopped` says, the versions
  each process ran); **used most** (views, commands, panel moves, most first); **conversations and
  sessions** (started, by kind and adapter; the median and slowest `openMs` and `firstAnswerMs`, with the
  slowest session's id; turns ended, by stop reason, and the median and slowest `turnMs`; how sessions
  ended; messages sent by kind; proposals applied and not); **refused** (by code, most first, with the
  requests that met each); and **failed**, grouped, most first: `error` by type and where, `page.error`
  by where and message, `log` at `error` by category, and `request.failed` by method, route and status
  with its slowest time. A time the log could not know is left out of the timings, never counted as
  zero. `--json` is the same summary as data.
- **Nobody's words**: the one free text it shows, a caught error's message, is its first line cut at
  the log's own 120 characters. It writes nothing; a missing `logs/` is a sentence and exit 2.

**As built (UNBLOCK5)**, measured against the code and its tests (`usage-report.test.ts`):

- **Asks**: every session `session.started` names in the period, with how many `permission.refused` lines
  it has, spread as the mean, the median (the two middles' mean), the 90th percentile (the nearest rank)
  and the share with none: overall, by adapter and by repository. What was refused is counted over every
  ask in the period, by kind, by tool and by what decided it (`(unsaid)` where the line has none). An ask
  whose session did not start in the period is counted apart, as `unstarted`, and in no session's spread.
  No session is no statistic: null, never zero.
- **Rule proposals**: the home's `proposals/`, read by the rules the file's other readers keep
  (`RuleProposals.cs`, `ruleproposals.ts`): a file with no id or no `change` object is none, a state they
  do not know is `proposed`, a proposal with no readable time takes its file's. Only the id, the time and
  the state are read, so the rule and the reason cannot be printed. It says how many wait for the person
  now, whenever made, and those made in the period by the week they were made in (Monday, UTC) and the
  state each stands in.

**As built (WSSETUP11)**, measured against the code and its tests (`usage-report.test.ts`):

- **Set-ups**: every session whose `session.started` says `setup` as the boolean true, in the order they started,
  with how it stands (its end's state; `awaiting-person` when it parked and has not ended; else `not ended`), how
  many times it parked, the seconds its end says, and its turns summed: tool calls, the tokens read anew (new input
  and what was written to the cache, METER1's split), read from the cache and written out, and the context at its
  largest against the window. A total line sums what was measured and gives the largest context. A count is a number
  at or above zero; anything else is unsaid, and what no line said is null, never zero. No price is claimed. A
  set-up that ran in several sessions (a cut-off carried on, a park answered) is one row per session.
- **Parks**: every `session.parked` in the period, by kind (`(unsaid)` where the line has none), and by the week it
  happened in (Monday, UTC) and its workspace (`(unnamed)` where the line has none), beside the sessions started
  that week in that workspace by a door that can park (driven and intake; a conversation never parks). A week with
  sessions and no park is shown, since a week of *before* is the point.

**As built (OUTCOME1)**: with `--service <url>`, a loopback address only, the report also reads the local host's quests,
session records and asks over three GETs and says each quest's outcome. From the log it takes only the parks of sessions
whose start the period holds. The future-directions review's *OUTCOME1, built* note says what it reads and what no store
keeps.

## 7. Build order

1. **LOG1a**: the writers and the lifecycle events: the desktop's (`MachineLog` in the driver
   library, used by the shell and the headless driver), the host's own, `app.started`/`app.stopped`,
   `error` from every unhandled exception, and `log` from each logging framework's warnings and
   errors. Retention and the size cap.
2. **LOG1b**: what the person does: the driver's session events and their timings, refusals, and the
   page's events over the bridge, with the module's filter. **Landed** (2026-09-30): `SessionLog` over
   the service client's new `Opened`/`Moved` seam and the conversation record's events, `RefusalLog` in
   the dispatcher's pipeline, `LogModule` with its typed filter, and the page's `logEvent`; §4's
   *As built* says what each line measures.
3. **LOG1c**: the two doors to read it. **Landed** (2026-09-30): `MachineLogReader` in the driver
   library, `daoris-driver logs`, and Settings → Logs over `DAORIS.LOG` · `LINES` and `OPEN_FOLDER`;
   §6's *As built* says what each reads and refuses.
4. **LOG1d**: the report. **Landed** (2026-09-30): `tools/usage-report.mjs`, its summarising helpers
   tested over a fixture home (`usage-report.test.ts`); §6's *As built* says what it prints.
5. **LOG2**: what the first real log showed, when the report was run on the owner's machine. **Landed**
   (2026-09-30); the two defects' mechanisms are in `docs/FIX-LOG.md`.

**As built (LOG2a): the host's stop is written.** The shell starts the host with its standard input
redirected and `DAORIS_STOP_ON_INPUT_END=1` (`HostSupervisor.StartInfo`; a twin of the host's
`InputEndStop.Variable`). Asked, the host reads that input to its end on a background thread, reading
nothing from it, and then stops through its lifetime as on Ctrl+C: `app.stopped` is written with the
uptime, and it exits 0. The shell's stop closes the input, waits up to five seconds (`StopWithin`), and
kills only a host still running then. A host started without the variable (from a terminal) never opens
its input; a host the shell adopted (HOSTID1) is not the shell's to stop. Held by `InputEndStopTests`
(the watcher, the variable table, and the real executable closed as the shell closes it) and
`HostSupervisorTests` (stand-in hosts: one that honours the input is let go, one that ignores it is
killed after the bound, an adopted one is left). **Not gated**: a force-killed shell's host stops too,
since the pipe's writer dies with the shell; observed by hand once, and no gate kills a shell to see it.

**As built (LOG2b): the browser's own tasks are observed.** `MachineLog.Observe(task, where, failed)`
writes a failed task as the `error` event with its place and then runs the reaction; a cancellation is
no failure, and the observation never faults. The browser hands it every task it starts and does not
await: its first window (`the browser's first window`, made by `EngineCdp.FirstWindowAsync`, which
throws whatever went wrong and says a client timeout as a `TimeoutException`), whose failure also stops
the browser, and its watch on the shell (`the browser's watch on the shell`). Held by `MachineLogTests`
(an observed failure is a line and never reaches the finalizer, where the same one left alone does),
`FirstWindowTests` (an engine that takes the socket and drops it is one line and a stopped browser) and a
source test over `BrowserProcess`. **Still possible**: the kit's relay (Shenora.Chromium 0.18.0,
`Shenora.Chromium.Host.CdpRelay.RelaySocketAsync`) awaits `Task.WhenAny` of its two pumps and observes
neither, as Daoris's own relay did before CHR8, so a CDP client still connected when the browser closes
can leave an `error` from *an unobserved task* here. That is a request for the kit's owner, not an edit
from this repository.

## 8. Not chosen

- **One file for every process.** Appending from several processes is not safe on Windows without a
  lock the artefacts would have to share, and they share no code.
- **Logging the words to make the report richer.** The record already holds them, under its own rules;
  the report counts and times, and a person who wants the words opens the session.
- **A logging library.** Four event kinds and a file a day are a hundred lines of each artefact's own
  code, and a library's configuration would be one more thing an install carries.
- **Sending anything anywhere.** There is no telemetry. What improves Daoris is the owner's own log,
  read on their own machine, when they choose.
- **A route to stop the host** (LOG2a). An HTTP door that stops the host is one any caller on the
  machine could press; the host's standard input is held by the one process that started it, and
  closes by itself if that process dies.
