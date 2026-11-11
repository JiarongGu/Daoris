# What the protocol door carries when the client asks for its streams (CONSOLE2a)

**Carried by:** CONSOLE2, the console's tab per thing that is running (the owner, 2026-09-27). A
record, not a contract.

**Date:** 2026-09-28 · **Adapter:** `claude-agent-acp` 0.79.0, the version the install and the scratch
machine pin, on ACP SDK 1.4.0 · **Harness:** the Claude Code 2.1.274 the adapter bundles ·
**Cost:** two real one-turn sessions on the machine's own account, under the owner's authorisation of
2026-09-24.

The console is one stream per session. The owner wants a tab for the session, each sub-session it
spawns, and each process it starts. Reading the adapter found all three on the wire, each switched on
by something the client declares at `initialize`. Daoris declares none of them. This is what the
adapter actually sends once they are declared. Every line below was read off its frames. Where a line
comes from its source instead, it says so.

## The probe

`tools/console2-probe.mjs`: a scratch git room and one turn, under the driver's own posture (`auto`,
set on `session/new`'s modes) and its rules carrier (a settings file allowing `Bash(node:*)`). The
enclosing agent session's `CLAUDE*` environment is stripped. The turn:

1. runs a three-line `node -e` in the foreground;
2. starts a `node -e` that ticks once a second for twenty seconds, **in the background**;
3. delegates "read `README.md` and reply with its first line" to a general-purpose subagent;
4. replies `DONE` without waiting.

The reader stays open for thirty seconds after the prompt's response, to see what arrives between
turns. Every frame, in both directions, goes to `_fixtures/console2-probe/frames.jsonl`.

The client capabilities it declares:

```json
{ "fs": { "readTextFile": false, "writeTextFile": false }, "terminal": false,
  "subagents": {},
  "_meta": { "terminal_output": true,
             "jetbrains": { "air": { "version": 1, "capabilities": ["asyncTasks", "nativeSubagentSessions"] } } } }
```

## 🔴 `subagents` alone switches nothing on

The first run declared `subagents: {}` and AIR's `asyncTasks`, not `nativeSubagentSessions`. The
adapter's check (`acp-subagents.js`) accepts either. No `subagent_spawned` arrived.

The ACP SDK's `zClientCapabilities` (1.4.0, `schema/zod.gen.js`) has no `subagents` key. The adapter
parses `initialize` with it, and zod drops the unknown key before the adapter reads it. The adapter's
own comment waits for the TypeScript SDK to publish it (*"until the TypeScript SDK publishes PR
#1992"*). `_meta` is in the schema, so the AIR spelling arrives. The second run declared AIR's
`nativeSubagentSessions`, and the subagent came through as a session of its own. **Declare both**:
AIR is what works today, and `subagents` is the protocol's own spelling for when the schema has it.

What the first run shows about **today**, with neither declared: the subagent's own tool calls arrive
**on the root session**, among the parent's, each stamped `_meta.claudeCode.parentToolUseId` with the
id of the `Agent` call that spawned it. So a driven session's console already mixes a subagent's work
into its own, and Daoris's mapper does not read the stamp.

## A subagent is a session of its own

With `nativeSubagentSessions` declared:

- **`subagent_spawned`**, on the parent session: `subagentSessionId`, `name` (the call's description,
  here *"Read README first line"*), `task` (its prompt), and `capabilities: {}`. The child's id is the
  harness's agent id, seventeen hex characters. A re-run of the same task gets `<id>:generation:<n>`,
  according to the source.
- **The child's updates are `session/update`s whose `params.sessionId` is the child's id**: its words,
  and its tool calls. The tool calls still carry `parentToolUseId`. Nothing else tells them apart
  from the parent's, so **a client that ignores `params.sessionId` merges the child into the parent**.
  `AcpSession` ignores it today.
- **`subagent_state_update`**, on the parent: the child's id and `state` (`completed` here). According
  to the source the states are `completed`, `failed`, `cancelled` and `disconnected`.
- **The `Agent` call itself leaves the parent's transcript.** The parent sees no `tool_call` for it.
  The adapter treats it as a control call and announces the child instead. So once this is declared, a
  parent record with no reading of `subagent_spawned` has **no trace that a subagent ran**.
- The subagent here ran **asynchronously**: the `Agent` call's response said `async_launched`. The
  adapter held the parent's turn open until the child ended. According to its source it defers settling
  a turn for the subagents that turn spawned. The prompt's response came 450ms after
  `subagent_state_update`.

## Background work is a task, and its output is a file

With AIR's `asyncTasks` declared, the backgrounded command became:

- **`async_task_spawned`**: `asyncTaskId`, `name` (the call's description), `taskType: "shell"`,
  `description`, `showInTranscript: false` (its tool call is its card; the task has none of its own),
  and `canStop: true`.
- **`async_task_progress`**, twice: first with the `toolCallId` of the Bash call that started it, then
  with the **`outputFilePath`**. The second arrived within 15ms of the spawn, in both runs.
- **`async_task_state_update`**: `stopped`, then `completed`, **in the same millisecond**, both times.
  The source explains the pair. The SDK's "which tasks are live" level reports the task gone first,
  and the authoritative ending corrects it. **The last state is the ending.**
- 🔴 **No line of the task's output is on the wire.** The output is the file the harness writes as the
  command runs, under the harness's own temporary directory:
  `<temp>\claude\<project slug>\<session id>\tasks\<task id>.output`. At the end it held the twenty
  ticks, a blank line, and the harness's own `[exited with code 0]`. The async subagent had a file
  there as well, which stayed empty, because its work came over the wire. The Bash call's result tells
  the model the same: *"Output is
  being written to: … To check interim output, use Read on that file path."* So the only live view of a
  dev server is tailing that file.
- The Bash call that started it completes at once. Its update carries
  `_meta.jetbrains.air.asyncTasks.backgrounded: true`, which says that the card has a task behind it.
- **Stopping one** is an AIR request, according to the source: `_session/async_task/stop` with
  `{ sessionId, asyncTaskId }`, answered `{ stopped }`. The probe did not call it.

## `terminal_output` streams nothing

The row that filed this said a command's output *streams* on its tool call under
`_meta.terminal_output`. It does not. With `terminal_output` declared, a Bash call is:

- a `tool_call` whose `_meta.terminal_info` names a terminal id (the call's own id), and whose
  `content` is `[{ "type": "terminal", "terminalId": … }]`;
- then, **when the command has finished**, three updates within 10ms of each other:
  `_meta.claudeCode.toolResponse` with the whole `stdout` and `stderr`; `_meta.terminal_output` with
  the whole output as `data`; and `_meta.terminal_exit` with `exit_code` and `signal`, which carries
  `status: completed`.

The foreground command's three lines arrived together, about eight seconds after its call opened, in
both runs. That is the moment its result arrives without the declaration too. Without it, according to the source
(`tools.js`), the output is the completed update's `content`, as a fenced `console` block, which is
where Daoris's record reads a tool's output today. **So the declaration gains an exit code, streams
nothing, and moves the output out of the field the record reads.**

## The agent speaks after its turn has ended

In both runs the prompt's response came first. Then, **with no prompt outstanding**, the agent spoke
again: twelve and ten message chunks, 1.6 and 2.4 seconds after the response. The completion of its
background work had woken it. In the first run the subagent's completion woke it once, then the ticking
command's did (*"The background ticker finished with exit code 0. All four probe steps are
complete."*). A `session_info_update` (a title) followed. The adapter's `initialize` answer also
declares `_meta.claudeCode.promptQueueing: true`.

So on this wire a turn is not the last thing a session says. A driven session today closes right
after its prompt's response. That cuts off the follow-up, and the process tree the adapter leaves
behind (the background command among it) goes with the session's job object (ORPHAN1). A chat keeps
its reader open between turns, so the words reach its console, and they belong to no turn.

## What Daoris takes

For 2b and 2c. Each is a landing of its own, TDD, and looked at on the window.

- **Declare `nativeSubagentSessions` and `asyncTasks` under AIR, and `subagents: {}`.** Do not declare
  `terminal_output`: it streams nothing and would take the output out of the record. The exit code
  alone does not pay for that.
- **Route every update by `params.sessionId`.** The root's updates are the session's, as today. A
  child's feed that child's own console stream, and not the parent's record. The parent's record
  gets the child as one card: `subagent_spawned` opens it, and `subagent_state_update` ends it. That
  card replaces the `Agent` call the declaration took away.
- **A task is a stream.** `async_task_spawned` opens it with its name. Once `async_task_progress`
  names the file, the driver tails it into that stream, and the last `async_task_state_update` ends
  it. The path stays in the driver. It is a machine path under the harness's temporary directory, and
  the console never leaves the machine (D47 §4). The task has no card in the record, because its Bash
  call is its card (`showInTranscript: false`).
- **A session's end ends its streams.** A child or a task still open when the session's reader ends is
  closed with it and says so. That is ORPHAN1's job object, said on the console.
- **The words after a turn are the session's**, on the console as they already are. Whether a driven
  session should wait for its own background work before it closes was a separate question, and D105
  answers it: it does not, and that work ends with the session.
- **Left:** stopping one task from its tab (`_session/async_task/stop`) until a tab exists to put it
  on, and the native door's equivalents (`stream-json`'s `parent_tool_use_id` and task messages),
  which no probe has read. **The stop landed as CONSOLE3a** (2026-09-30), against a stub agent that
  answers the request as the source says; the real adapter's answer is still unseen.

## What this does not establish

- One harness version, one adapter version, and one subagent that ran asynchronously. A synchronous
  subagent, a nested one, and a failed one were not seen. Their shapes here are from the source.
- A task stopped by the person, or one still running when the session closed, was not seen. In both
  runs the task finished on its own.
- Codex and dsh on the same door were not probed. Neither is known to carry either extension.
