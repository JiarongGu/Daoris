# What the native door carries for a subagent and a background task (CONSOLE3c)

**Status: evidence** (2026-09-30), and what it found is built (CONSOLE3c, `ClaudeStreams`). `tools/console3-probe.mjs`, one small turn on the machine's own
signed-in account, Claude Code 2.1.284, with the pipe door's own arguments: `-p <prompt>
--permission-mode acceptEdits --output-format stream-json --verbose --include-partial-messages
--settings <rules>`. The turn ran a foreground command, started a background one that ticks for 40
seconds, and delegated one read to a subagent. The protocol door's twin is
`docs/2026-09-28-console2-streams-evidence.md`.

## A subagent's lines ride the session's stream, marked with the call that spawned it

Every line the subagent produced arrived on the session's own stdout with **`parent_tool_use_id`**
set to the `Agent` call's `tool_use_id`: its `assistant` tool call (a `Read`), the `user` line with
that call's result, its `thinking`, and its reply text. Four lines, and none of them were partial
`stream_event`s: partials are the root's alone. A line without `parent_tool_use_id` (null) is the
root's, as every line was before.

The `Agent` call itself answered at once, `tool_use_result.isAsync: true` and `status:
async_launched`, with an `agentId`: at 2.1.284 the subagent ran in the background without being asked
to.

## Background work is a task, announced on `system` lines

Both the backgrounded command and the async subagent were **tasks**, on five `system` subtypes:

- **`task_started`**: `task_id`, `tool_use_id` (the call that started it), `description`,
  `is_backgrounded: true`, and `task_type`: `local_bash` for the command, `local_agent` for the
  subagent, which also carries `subagent_type`, `spawn_depth` and its `prompt`.
- **`task_progress`**, the subagent's only: `description` (*"Reading README.md"*), `usage` and
  `last_tool_name`.
- **`task_updated`**: `patch.status` and `patch.end_time`: `completed` for the subagent, **`killed`**
  for the command.
- **`task_notification`**: `status` (`completed`, `stopped`), `output_file`, a `summary`, and for the
  subagent its `usage`.
- **`background_tasks_changed`**: the whole live set, `task_id`, `task_type` and `description` each,
  after every change.

**The command's output is a file, and its path is on the wire twice.** At the start only inside the
`Bash` result's TEXT, the harness speaking to the model: *"Command running in background with ID: …
Output is being written to: `<temp>\claude\<project slug>\<session id>\tasks\<task id>.output`. You will
be notified when it completes…"*; the structured `tool_use_result` carries `backgroundTaskId` and no
path. At the end in `task_notification.output_file`. So a live view of a dev server on this door reads
the path out of the result's words, which is the harness's prose and could change without notice,
where the protocol door names it structurally (`async_task_progress.outputFilePath`).

## When the turn ends, the binary ends its background work

The `result` line came at 22 seconds. Five seconds later the command was `killed` (`task_updated`) and
`stopped` (`task_notification`), and the binary exited at 29 seconds. No process of the command was
running at the exit or 25 seconds after. So on this door **nothing a driven session starts outlives
its turn**, the same outcome ORPHAN1's job object gives the protocol door, reached by the harness
itself. The async subagent finished before the result; the turn did not end while it ran.

## What Daoris takes

- **Route a line by `parent_tool_use_id`.** Null is the session's, as today. A set one is the
  subagent's that the `task_started` naming that `tool_use_id` announced, and its words and tool calls
  go to that subagent's stream, not the session's record or transcript, as on the protocol door.
- **A task is a stream.** `task_started` opens it, `local_agent` as a subagent and `local_bash` as a
  task, named by its `description`; the last of `task_updated` and `task_notification` ends it, with
  the wire's status. The command's file is read from the result's words while it runs, and the final
  read from `task_notification.output_file`; a path that cannot be read out of the words leaves the
  stream saying it started and how it ended, and nothing between.
- **No stop.** This door has no request to stop one task. A task here is not `canStop`.
- **Not taken:** `task_progress` and `background_tasks_changed` add nothing the others do not say.

## What this does not establish

- One binary version, one async subagent, one backgrounded command. A synchronous subagent (with
  `parent_tool_use_id` but no task), a nested one, and a failed one were not seen.
- A conversation on this door (`--input-format stream-json`), which keeps its process between turns,
  was not probed: whether its background work outlives a turn there is open, and D105 bounds it: it
  ends with the session, as a driven session's does.
