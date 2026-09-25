# Claude Code's structured output, checked against the binary (CONV3, D76)

**Carried by:** D76, CONV3a and CONV5 (in the archive): the native door's `stream-json` mapping, and
a turn's usage on both doors. A record, not a contract.

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

## A turn's usage, on both doors (CONV5)

**Probed 2026-09-26.** Claude Code 2.1.282 on `PATH`, and `claude-code-acp` 0.79.0 (the dsh probe's
install). The probe ran two turns on each door: one that read a file with a tool, and one of a single
word.

- **The native door's `result.usage` is the turn's total**, summed over its API calls. The tool turn's
  two calls reported input 2 and 2, cache writes 16,605 and 112, and cache reads 17,228 and 33,833.
  Its `result.usage` said 4, 16,717 and 51,061. Output is the exception: the streamed `assistant`
  messages said 16 and 4, and `result.usage` said 80. So the result's figure is the one taken, never a
  sum of the messages. `usage.iterations` held the last call only.
- **The protocol door's prompt response carries the same four**, as `usage`: `inputTokens`,
  `outputTokens`, `cachedReadTokens`, `cachedWriteTokens`, and a `totalTokens` that is their sum
  (4 + 88 + 48,666 + 20,035 = 68,793). It is also the turn's own total: the second turn's figures
  are its own, not a running sum. `_meta.quota` repeats them by model and names the models, and a
  small model the harness called on its own (909 input tokens) is counted there and not in `usage`.
- **Context** arrives as `usage_update` (`used`, `size`) during the turn on the protocol door. The
  last one of a turn also carries a `cost`.
- **Neither door reports when the first word came.** `result.duration_ms` is the native door's alone.
- 🔴 **A stopped turn on the protocol door reports every count as zero** (seen on the window, a turn
  stopped eight seconds in). The adapter tallies a turn when its `result` arrives, so a turn cancelled
  before one answers with the empty tally, although it had read about 38,000 tokens of context.

**What Daoris takes.** Each turn's four counts, from `result.usage` on one door and the prompt's
`usage` on the other, kept on the turn's own event. A report whose every count is zero is kept as no
report, since no turn that ran read nothing. Time comes from the driver's clock, from the ask
to the turn's end, so it means the same on both doors. **Left on the wire:** the cost, the models'
names and the by-model split (D24, TOOL3), and `totalTokens`, which is only the sum.

**Why a message sent mid-turn is held by Daoris, not written at once.** The SDK's own declarations say
a user line that arrives while a turn runs may be *folded into* that turn rather than answered after
it (`user_message_uuid`, `still_queued`). That is **bundle evidence** (`@anthropic-ai/claude-agent-sdk`
0.3.274, its `sdk.d.ts`). It was not measured. Daoris does not rely on it either way: the chat door
holds a mid-turn message until the turn's `result`, then writes it. Both doors then take one turn at a
time, and the record says when each message was sent.
