# STEER1: what a word to a running session does on each door (evidence note)

**Carried by:** D136. It records what each door does with words sent to a session while its turn runs, at the versions
in §0. It is a record of those versions, not a contract: a harness that moves is read again.

> Written 2026-10-03, **keylessly but for one session**: one real session over the protocol door, two short turns, on
> the machine's own sign-in, in a scratch folder with no instruction file in or above it, with the adapter's own
> options for `--setting-sources project --strict-mcp-config` (§6). Everything else was read from shipped code. No
> Daoris install, home or process was touched, and no Daoris door was driven. No account is named here.

## The answer

1. 🔴 **A second `session/prompt` sent while a turn runs reaches Claude Code at its next step**, on
   `claude-agent-acp` 0.84.0. The adapter puts the words on the Agent SDK's input at once, and Claude Code folds them
   into the running turn after the tool calls in flight finish, before its next model call. Nothing is cancelled and
   no work is lost. *Measured* (§6, turn A) and *read* (§1).
2. **The wire splits the turn where the words were taken.** The earlier `session/prompt` is answered `end_turn` at
   that moment, with every usage count zero, while the agent goes on working. The later prompt owns the rest of the
   work and is answered at the real end, with the whole turn's usage. Measured: answered 24 ms after the first tool
   call's result, with four more reads still to come.
3. **The adapter says it takes prompts during a turn**: `initialize` answers
   `agentCapabilities._meta.claudeCode.promptQueueing: true`. *Read and measured.*
4. **The steering extension, `_session/steering`, interrupts the step in flight.** It is answered `{"outcome":
   "injected"}` at once, and the turn's one prompt stays open to the end. The adapter delivers it at the SDK's priority
   `now`, which aborts the generation (or the tool) in flight. The measured steer cut the model mid-call: the tool call
   it had announced never got an update of any kind, and the agent issued the read again. *Measured* (§6, turn B) and
   *read* (§2).
5. 🔴 **A stop while words are still on their way loses their answer.** `session/cancel` settles every prompt the agent
   has not yet taken as `cancelled`, but Claude Code's interrupt keeps queued words by default (`still_queued`) and
   runs them next, and the adapter drops that run's output as an orphan's. So the agent may act on words whose answer
   never reaches the client. *Read* (§3), not measured.
6. **The native door's driven session cannot hear anything mid-turn today.** Daoris starts it with `-p <target>` and no
   stdin input. Claude Code's own stream-json input does the same fold as the adapter (a user line without a priority
   is queued at `next`), so a native driven session started with `--input-format stream-json` could. *Read* (§4).
7. **`codex-acp` must not be sent a second `session/prompt` during a turn.** Its `prompt()` resets the session's
   current turn and replaces the tracked prompt without waiting for the first. It advertises `_session/steering`, which
   calls Codex's `turn/steer` on the live turn, or starts a new turn when none runs. Whether Codex's `turn/steer`
   interrupts the step in flight was not read. *Read* (§5).

## 0. What was read, at which version

| Harness | Version | Where | What was read |
|---|---|---|---|
| Claude Code over ACP | adapter **0.84.0**, its SDK **0.3.284**, that SDK's CLI **2.1.284** | `npm pack` into an ignored scratch folder; `dist/acp-agent.js` sha256 `118db041…`, the install's pin as the entry-point and limit-signals evidence found it | `dist/acp-agent.js`, cited by line |
| Claude Agent SDK | **0.3.284** | `npm pack` | `sdk.d.ts`: `SDKUserMessage.priority?: 'now' \| 'next' \| 'later'`, undocumented there |
| Claude Code CLI | **2.1.284** (`VERSION:"2.1.284"` in the binary) | `npm pack` of `@anthropic-ai/claude-agent-sdk-win32-x64@0.3.284`; `claude.exe` sha256 `0416631e…` | the JavaScript embedded in the binary, cited by byte offset |
| codex over ACP | **2.1.1**, npm's latest | `npm pack` | `dist/index.js`, cited by line |

**Labels.** **measured**: a frame recorded on this machine (§6). **read**: read in shipped code, at the place cited.
**not read**: nothing here reached it. Every label but *measured* is unmeasured.

## 1. The protocol door: a second `session/prompt` during a turn

| Finding | Where | Label |
|---|---|---|
| `initialize` answers `agentCapabilities._meta.claudeCode.promptQueueing: true` | `acp-agent.js:1255-1258`; §6 | read, measured |
| `prompt()` owns no loop: it queues a turn, pushes the user message onto the SDK's streaming input with **no priority**, and awaits the turn's outcome | `acp-agent.js:1782-1816` | read |
| Claude Code queues a user command with no priority at **`next`** (a task notification at `later`) | `claude.exe` @209904095, @209905285 | read |
| Claude Code's main loop folds every queued command at `now` or `next` into the running turn after the tool results, unless a mid-turn fold is suspended | `claude.exe` @217560065, @217621951 (`getCommandsByMaxPriority("next")`, `isMidTurnFoldSuspended`) | read |
| A user frame from stdin is enqueued as soon as the admission barrier and the first turn's gate let it, not at the turn's end | `claude.exe` @228094292 (`acceptUserFrame`, `deliver`) | read |
| When the folded words' echo arrives, the adapter settles the turn that was running `end_turn` and makes the new prompt's turn the active one; output after the echo is the new turn's | `acp-agent.js:4453-4532` | read, measured |
| Several queued prompts folded at one step share one result, and are handed off in the order sent | `acp-agent.js:2504-2515`, `:2992-3000` | read |

## 2. The steering extension, `_session/steering`

| Finding | Where | Label |
|---|---|---|
| Advertised as the top-level `initialize._meta.steering.supported: true`, "per the existing ACP steering extension contract" | `acp-agent.js:1291-1307`; §6 | read, measured |
| With a turn in flight it pushes the words with priority **`now`** (or `later` while a permission or elicitation awaits the client), creates no turn, and answers `injected` | `acp-agent.js:2034-2130` | read, measured |
| "Pre-empting means ABORTING: the interrupted cycle emits a `result` of its own and the steered message runs as a second one" | `acp-agent.js:2052-2054` | read |
| Claude Code aborts the running drain with `interrupt` the moment a command at `now` is queued | `claude.exe` @228189598, @228189641 | read |
| With no turn running it starts a detached turn and answers `startedNewTurn`, unless the request's `_meta.steering.idleBehavior` is `promptRequired`, when it answers `promptRequired` and leaves the words to the client | `acp-agent.js:2078-2101`, `:226-232` | read |

## 3. A stop while words are on their way

| Finding | Where | Label |
|---|---|---|
| `cancel()` settles every queued prompt not yet taken (no echo seen) as `cancelled` at once, and tracks it as an orphan whose result is skipped | `acp-agent.js:4948-5008` | read |
| Claude Code's interrupt keeps queued user messages by default and runs them next: `interrupt_receipt_v1` lists them as `still_queued`; only `cancel_queued: true` (`interrupt_cancel_queued_v1`) drops them | `claude.exe` @204281095, @106107246 | read |
| The adapter reads `still_queued` to forget orphans that were dropped; the rest still run, under a session it has marked cancelled, so their output is not sent | `acp-agent.js:5111-5116`, `:4536-4553` | read |

So a *Send now* built on `session/cancel` must not run while a word is on its way: the agent may act on it, and its
answer would reach nobody.

## 4. The native door

| Finding | Where | Label |
|---|---|---|
| Daoris's driven session runs `claude -p <target>` with no `--input-format stream-json`: nothing can be written to it mid-turn | `Adapters.cs` (`ClaudeCodeAdapter.Prepare`) | read |
| A conversation on the native door runs `--input-format stream-json` and holds a mid-turn message until the turn's `result` (CONV3) | `Adapters.cs` (`PrepareChat`); `docs/2026-09-25-stream-json-evidence.md` | read |
| A stream-json user line with no `priority` is the same queued command the adapter pushes (§1), so it is folded at the next step | `claude.exe` @228094292, @209904095 | read |

## 5. `codex-acp` 2.1.1

| Finding | Where | Label |
|---|---|---|
| `prompt()` sets `currentTurnId = null` and replaces the session's tracked prompt (`activePrompts.set`) without awaiting the one in flight; nothing queues a second prompt behind the first | `dist/index.js:39527-39543`, `:39339-39390` | read |
| `initialize` advertises `_meta.steering.supported: true` | `dist/index.js:37467-37470` | read |
| `_session/steering` is serialised per session; with a live turn it calls Codex's `turn/steer` with that turn's id, else it starts a new turn | `dist/index.js:38484-38538`, `:34471-34477` | read |
| What Codex does with `turn/steer` (between steps, or an interrupt) | Codex's own source | not read |
| No `promptQueueing` marker: `initialize` answers at `dist/index.js:37437-37480`, and the word appears nowhere in the bundle | `dist/index.js` | read |

## 6. The session, frame by frame

**How it ran.** A scratch client spoke ACP to `claude-agent-acp` 0.84.0 (`node dist/index.js`), with
`CLAUDE_CODE_EXECUTABLE` naming the 2.1.284 binary above and no `CLAUDE_CONFIG_DIR`, so the machine's own sign-in
answered. `session/new` carried `_meta.claudeCode.options: { settingSources: ["project"], strictMcpConfig: true }`,
the adapter's form of `--setting-sources project --strict-mcp-config`, with no MCP servers, in a scratch folder
holding five one-word files and no instruction file in it or any folder above it. Permission requests would have been
refused; none came, since reading a file in the session's folder asks for none. Every frame both ways was kept in an
ignored scratch file.

**Turn A, a second `session/prompt`.** Asked to read five files one per call, then reply with their words. On the
first `tool_call`, the client sent a second prompt: *"in your final reply, put the word PINEAPPLE after the five
words."*

| ms | Frame |
|---|---|
| 3,695 | `session/prompt` #3 (the task) out |
| 6,825 | `tool_call` (read 1) in; `session/prompt` #4 (the note) out |
| 7,991 | read 1 `completed` |
| 8,015 | **#3 answered `end_turn`, every usage count 0** |
| 10,545 – 29,322 | reads 2 to 5, each `completed` |
| 30,832 | **#4 answered `end_turn`**, with the turn's usage |

The reply was `alpha bravo charlie delta echo PINEAPPLE`. No tool call failed. The adapter sent no
`user_message_chunk` for either prompt.

**Turn B, `_session/steering`.** The same task in reverse order. On the first `tool_call`, the client steered: *"in
your final reply, put the word MANGO after the words."*

| ms | Frame |
|---|---|
| 32,333 | `session/prompt` #5 (the task) out |
| 34,668 | `tool_call` (read 1, its input not yet streamed) in |
| 34,669 – 34,670 | `_session/steering` #6 out, answered `{"outcome":"injected"}` |
| 36,387 – 49,725 | five reads, each `completed`; **the call announced at 34,668 never received an update** |
| 52,313 | #5 answered `end_turn`, with the turn's usage |

The reply was `echo delta charlie bravo alpha MANGO`: six calls for five files, since the steer aborted the model
while it was writing the first.

## 7. What was not measured

- **A step that is a long tool.** Both turns' steps were reads of a few milliseconds. That a fold waits for a running
  tool to finish, and that a steer aborts one, is read (§1, §2), not seen.
- **A stop with words on their way** (§3): read only. Seeing it costs a turn whose words would act unseen.
- **The native door** (§4) and **`codex-acp`** (§5): read only. A native driven session on `--input-format
  stream-json`, and Codex's `turn/steer`, are each one turn to measure before either is built.
- **Words folded while a permission request waits.** Daoris refuses every request at once (D52), so the adapter's
  `later` priority for a steer should not arise; it was not seen.
