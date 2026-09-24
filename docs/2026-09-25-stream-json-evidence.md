# Claude Code's structured output, checked against the binary (CONV3, D76)

**Date:** 2026-09-25 · **Binary:** Claude Code 2.1.281, the machine's own on `PATH` · **Cost:** one
real two-turn session, a word each, under the owner's authorisation of 2026-09-24.

D76 §1 lets each door map its own documented wire into Daoris's event vocabulary. Before the native
door's adapter does that for Claude Code, this is what the binary actually does. Every line below was
read off its output; nothing is taken from documentation alone.

## The probe

A scratch folder, and two user messages as JSON lines on stdin:

```
claude -p --input-format stream-json --output-format stream-json --verbose \
       --include-partial-messages --permission-mode acceptEdits
```

with each line `{"type":"user","message":{"role":"user","content":[{"type":"text","text":"…"}]}}`.

## What came back

- **Two full turns in one process.** Each message was its own turn, answered in order, before stdin
  closed. So one process can hold a **conversation**: the chat door writes each message as a line,
  and ending input ends the session. That is the same two endings the chat door already keeps.
- **Per turn**, in this order: `system/init` (the session's setup: cwd, tools, MCP servers, the
  permission mode, the version), `system/status`, then the model's stream as `stream_event`s
  (`message_start`, `content_block_start`, `content_block_delta` with `text_delta`,
  `content_block_stop`, `message_delta`, `message_stop`), then the whole `assistant` message, then
  one `result`. A `rate_limit_event` came once.
- 🔴 **The words arrive twice** with partial messages on: as `text_delta`s, and again whole in the
  `assistant` message. A mapper that took both would show every sentence twice. `message_start`
  carries the message id the `assistant` message repeats, so the whole message's text is skipped
  when its deltas already streamed.
- **Tool calls** are `tool_use` blocks in the `assistant` message (`id`, `name`, `input`), and their
  outcomes are `tool_result` blocks in a following `user` message (`tool_use_id`, `content`,
  `is_error`). Not exercised by this probe's one-word turns; the shape is the SDK's published one,
  and the adapter's mapping treats every field as optional.
- **`result`** carries `subtype` (`success` here), `is_error`, the final text, `num_turns`,
  `duration_ms`, and `usage` (input, cache creation, cache read, output tokens). **`modelUsage`**,
  keyed by model, carries **`contextWindow`** (1,000,000 here), which is the size a context meter
  needs, and a `costUSD`.

## What Daoris takes, and what it leaves

- **Takes**: the words (from the deltas), thinking, tool calls and their results, a `TodoWrite`
  call's list as the plan, the turn's end, and context as *used / window*. Used is the last
  assistant message's input, cache-creation and cache-read tokens together; the window is
  `contextWindow`.
- **Leaves**: the model's name (D24: the harness owns the model), and the cost (TOOL3: no price is
  claimed; a list price is not what a person pays). Both are on the wire and neither is recorded.
- **Unknown lines** stay in the raw view, and an event of a type this build does not know is kept
  raw in the record rather than dropped.

## Stopping a turn (CONV4a)

**Probed 2026-09-25**, same binary (2.1.281), two real sessions of two turns each. Each probe sent a
turn, then an interrupt, then a one-word turn. Process launched as above; one probe used
`bypassPermissions` in a scratch folder so a shell command could run long enough to be interrupted.

The stop is a **control request** on stdin, beside the user lines:

```
{"type":"control_request","request_id":"<id>","request":{"subtype":"interrupt"}}
```

- **No `initialize` is needed first.** The interrupt was answered on a session that had sent none.
- **It is answered** with
  `{"type":"control_response","response":{"subtype":"success","request_id":"<id>","response":{"still_queued":[]}}}`,
  in about half a second. `system/init` advertises it as `capabilities: ["interrupt_receipt_v1", …]`.
- **The turn ends with a `result`** of `subtype: "error_during_execution"`, `is_error: true`, and a
  `terminal_reason` that says what was cut:
  - `aborted_streaming` when the interrupt landed while the model was writing;
  - `aborted_tools` when it landed while a tool ran. The tool's card is answered by a `tool_result`
    with `is_error: true` and the harness's own sentence (*"The user doesn't want to proceed with
    this tool use…"*).
- **A synthetic `user` line** follows, `[Request interrupted by user]` or `[Request interrupted by
  user for tool use]`, as plain text rather than a tool result. The partial `assistant` message
  carries `aborted: true` at its top level.
- **The process keeps the session.** The next user line was an ordinary turn, answered, `success`.

**What Daoris takes from it.** A turn whose `terminal_reason` is `aborted_streaming` or
`aborted_tools` ends as **`cancelled`**, which is what the protocol door calls the same ending (ACP's
stop reason). So the person's stop reads the same on both doors and never as a failure. A
`control_response` is the driver's answer, not the conversation's, and stays out of the record.

**Why a message sent mid-turn is held by Daoris, not written at once.** The SDK's own declarations say
a user line that arrives while a turn runs may be *folded into* that turn rather than answered after
it (`user_message_uuid`, `still_queued`). That is **bundle evidence** (`@anthropic-ai/claude-agent-sdk`
0.3.274, its `sdk.d.ts`). It was not measured. Daoris does not rely on it either way: the chat door
holds a mid-turn message until the turn's `result`, then writes it. Both doors then take one turn at a
time, and the record says when each message was sent.
