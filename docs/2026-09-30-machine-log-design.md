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

## 5. What is never logged

**Anyone's words**: a message, a prompt, what an agent said, a tool call's input or output, a quest's
title or body, a search. **A file's contents. Any secret**: a key, a token, a header, a sign-in. **An
address**: a URL's query, a page the browser visited. Words are already kept where they belong (the
session's record and transcript), and a log that held them would be a second copy with none of the
record's rules. Paths do appear (a stack, a repository's tree): the log never leaves the machine, like
the transcript beside it.

## 6. Reading it — two doors, and a report

- **At a terminal**: `daoris-driver logs [--since <1h|2d>] [--source <name>] [--event <name>]
  [--json]` prints the lines across every source, merged by time.
- **On the screen**: a *Logs* domain in Settings (desktop only) shows the recent lines with the same
  filters, and opens the folder.
- **For improving Daoris**: `tools/usage-report.mjs --install <dir> [--days 7]` reads an install's
  logs and prints what was used most, what was refused, what failed and what was slow. A development
  session starts from it instead of from a guess.
- **Never over HTTP.** A browser, and a remote, see none of it (D47 §4). The host writes its file and
  serves no route onto any of them.

## 7. Build order

1. **LOG1a**: the writers and the lifecycle events: the desktop's (`MachineLog` in the driver
   library, used by the shell and the headless driver), the host's own, `app.started`/`app.stopped`,
   `error` from every unhandled exception, and `log` from each logging framework's warnings and
   errors. Retention and the size cap.
2. **LOG1b**: what the person does: the driver's session events and their timings, refusals, and the
   page's events over the bridge, with the module's filter.
3. **LOG1c**: the two doors to read it.
4. **LOG1d**: the report.

## 8. Not chosen

- **One file for every process.** Appending from several processes is not safe on Windows without a
  lock the artefacts would have to share, and they share no code.
- **Logging the words to make the report richer.** The record already holds them, under its own rules;
  the report counts and times, and a person who wants the words opens the session.
- **A logging library.** Four event kinds and a file a day are a hundred lines of each artefact's own
  code, and a library's configuration would be one more thing an install carries.
- **Sending anything anywhere.** There is no telemetry. What improves Daoris is the owner's own log,
  read on their own machine, when they choose.
