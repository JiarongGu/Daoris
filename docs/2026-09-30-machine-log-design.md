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
| `session.started` | desktop | session, kind, adapter, repository | what the person runs, on what |
| `session.opened` | desktop | session, adapter, openMs | spawn to ready: what a person waits through |
| `turn.answered` | desktop | session, firstAnswerMs | send to the first word back |
| `turn.ended` | desktop | session, stopReason, turnMs | how long a turn takes, and how it ends |
| `session.ended` | desktop | session, state, seconds | how it finished |
| `refused` | desktop | code, request | a refusal the person met, by its catalogue code (REFUSE1) |
| `view.opened`, `command.run`, `panel.moved` | desktop, from the page | the view, the command, the region | what is used, and what never is |
| `message.sent` | desktop, from the page | session, kind, length, files | how the conversations are used |
| `proposal.settled` | desktop, from the page | applied | whether Ask Daoris's proposals help |
| `page.error` | desktop, from the page | where, message | a render or request failure the page caught |
| `request.failed` | host | method, route, status, ms | a request that failed, or took over two seconds |

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
- **The page's fields** each have a kind: a name (`view`, `command`, `region`, `session`, `kind`,
  `where`: an identifier, never a sentence), a text (`message`), a count (`length`, `files`) or a flag
  (`applied`). A value of another kind is dropped, and a string is cut at 120 characters with an
  ellipsis. `message.sent`'s `kind` is `chat`, `steer` (a word to a driven session) or `help`, and a help
  message that opens a conversation has no `session` yet. `proposal.settled` is the person's Apply or Not
  now once it landed. `page.error` is `error`-level, `where` `window` or `promise`.

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
  session starts from it instead of from a guess.
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
5. **LOG2**: what the first real log showed, when the report was run on the owner's machine. Below.

**As built (LOG2a): the host's stop is written.** The first real log held a host `app.started` for
every start and no `app.stopped`: the shell ended the host it started by killing it, so nothing the
host does on a clean stop ran. Now `HostSupervisor.StartInfo` starts the host with its standard input
redirected and `DAORIS_STOP_ON_INPUT_END=1` (a twin of the host's `InputEndStop.Variable`); the host,
asked, reads that input to its end on a background thread (reading nothing from it) and then stops
through its lifetime, as on Ctrl+C, so `app.stopped` is written with the uptime and the host exits 0.
The shell's stop closes the input, waits up to five seconds (`StopWithin`), and kills only a host still
running then. A host started without the variable (from a terminal) never opens its input and is as it
was; a host the shell adopted (HOSTID1) is not the shell's to stop, and stays running. **Not a route**:
an HTTP door to stop the host is one any caller on the machine could press, where the input is held by
the one process that started the host. Held by `InputEndStopTests` (the watcher, the variable table, and
the real executable closed as the shell closes it) and `HostSupervisorTests` (a stand-in host that
honours the input is let go, one that ignores it is killed after the bound, an adopted one is left).
**Not gated**: a force-killed shell's host now stops too, because the pipe's writer dies with the shell
and the host's read ends; observed by hand once (the host wrote `app.stopped`), and no gate kills a shell
to see it.

## 8. Not chosen

- **One file for every process.** Appending from several processes is not safe on Windows without a
  lock the artefacts would have to share, and they share no code.
- **Logging the words to make the report richer.** The record already holds them, under its own rules;
  the report counts and times, and a person who wants the words opens the session.
- **A logging library.** Four event kinds and a file a day are a hundred lines of each artefact's own
  code, and a library's configuration would be one more thing an install carries.
- **Sending anything anywhere.** There is no telemetry. What improves Daoris is the owner's own log,
  read on their own machine, when they choose.
