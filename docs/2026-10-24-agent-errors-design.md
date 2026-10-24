# Every agent's errors read into one set of kinds at the agent interface, and one policy acting on each kind (AGENTERRORS1, 2026-10-24)

**Status: proposed (D210), round 1.** Written by Claude Opus from the code at `64059110a`, for Codex's read and an independent Opus
reader's foundation read under D191 (its amendment of 2026-10-23: the design changes what every adapter answers at the agent
interface, and the decided rules TURNFAIL1, D206 and D205 rest on). No read has been made yet; §13 keeps their table when they are.

The owner's report and direction (2026-10-24, through the quest that asked for this design, and `TASKS.md`'s AGENTERRORS1 row):
SUPERVISOR1a-ii's session ended its turn on `API Error: 529 Overloaded` and waited on the person, and the driver reads no such error.
This is not a fix for one code. Every agent's errors, of every harness and door, are read into one set of kinds at the agent
interface, each adapter mapping its own words and codes, and one policy acts on each kind: a transient error is tried again with a
growing backoff and a bound in the same conversation, the words kept, said once, never a strike or a park; a limit as D206 has it; a
sign-in or permission error goes to the person; a fatal one fails with its reason.

Every claim about the code below names its file and line at `64059110a`. Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`
unless they say otherwise. What the adapters do is read from their own source, in the install's toolchain cache
(`claude-agent-acp` 0.84.0, `codex-acp` 2.1.1), and is marked *read, not measured* where no record shows it.

## 0. The case, from the install's records

Two records on the install met the same error on the same morning, both on `claude-code-acp` (the install's Build adapter,
`data/driver.json`'s `adapter`). They show the two shapes the error takes on that door.

- **Shape 1: the prompt fails** (session `61306f9f`, HELPNEWCHAT1's, 00:30 UTC). The adapter streamed the error as an agent message
  (event 464, `kind: message`: *API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment.
  If it persists, check https://status.claude.com.*), then answered the prompt with a JSON-RPC error. Daoris kept it as
  `door.failed`: *the ACP agent refused the call: Internal error: API Error: 529 Overloaded. …* (event 466). The record ended
  `failed`, `ended.turn-failed-taken`, which is a strike. At the next look the line was carried on in a **new session** (`63d56294`,
  `moved.on … why=carry-on`), handed Daoris's brief of 1308 characters. Its own conversation, at 219,284 tokens, was left behind.
  The error came three and a half minutes after the last tool result (events 463 and 464): Claude Code's own retries had run first.
- **Shape 2: a word the person added fails, and the prompt ends clean** (session `5bafda3b`, SUPERVISOR1a-ii's, 00:29 UTC). The
  same message streamed (event 2300), and then a word the person had added mid-turn was refused with the error (event 2302,
  `words.refused`: *the session would not take what the person added: Internal error: API Error: 529 Overloaded. …*). The prompt
  answered `end_turn` (the log: *the turn ended: end_turn, after 2300 update(s)*). With its quest taken and a clean exit, the run
  parked on the person. The next event is the person's own word 33 minutes later (event 2303, 01:02 UTC): *Go on where the API
  overload stopped you: …*. The run resumed its own conversation (`resumed.opening`, `why: answer`) and went on.

The person's word in shape 2 is what this design automates: the same conversation, told to go on, after the provider recovers. In
shape 1 nothing was asked of the person, but the work lost its conversation and counted a strike for an error that was not its own.

Both shapes are what the adapter's source says it does. A `result` with `is_error` fails the prompt with
`RequestError.internalError(errorKindData(lastAssistantError), message.result)`, where `lastAssistantError` is the SDK's `error`
on the assistant message (`claude-agent-acp/dist/acp-agent.js:4178-4180`, `:4588`). During a steer, an error-shaped result settles
the turn `end_turn` instead (`:4150-4153`), and the steered word's answer is the error. The error's `data` is `{ errorKind }`
(`:7249-7251`), so on shape 1 the wire carried `errorKind: "overloaded"`. Daoris read that field (`AcpRefusal.ErrorKind`,
`Agents/Acp/Acp.cs:206`) and dropped it at the conclusion, which takes `error.Message` alone (`Loop/Driver.cs:4325`).

## 1. Today, by the code

### 1.1 What each door hands Daoris when the provider fails

| Door | Adapter | The harness's own retries | What reaches Daoris at the end | Daoris reads |
|---|---|---|---|---|
| Protocol | `claude-code-acp` (`Agents/Adapters/Adapters.cs:1824-1939`) | Claude Code retries 5xx, 529 and dropped connections itself, reported as `system`/`api_retry` frames (`acp-agent.js:3600-3612`), which the adapter forwards only to a client that declared JetBrains AIR's `sessionFailure` capability (§10) | A JSON-RPC error on the prompt, `-32603` *Internal error: {result}*, `data.errorKind` the SDK's `authentication_failed`, `oauth_org_not_allowed`, `account_on_hold`, `verification_required`, `billing_error`, `rate_limit`, `overloaded`, `invalid_request`, `model_not_found`, `server_error`, `unknown`, `max_output_tokens` or `cloud_credential_error` (the SDK's declaration, `docs/2026-10-02-limit-signals-evidence.md:156`). *Please run /login* is `-32000` (`acp-agent.js:4170-4171`). On a steer, shape 2 | The message string: `turnFailed` (`Driver.cs:4312-4326`). `ErrorKind`, `Code` kept on `AcpRefusal` (`Acp.cs:192-240`) and dropped there |
| Pipe | `claude-code` (`Adapters.cs:2166-2410`) | The same; `system`/`api_retry` frames on stdout, not read | The `result` frame: `is_error`, `subtype`, `result`'s text, `api_error_status` (the HTTP status; `limit-signals-evidence.md:154`); the assistant message before it carries `error` | `is_error` and the text alone, as `StreamMapped.Failed` (`Agents/Streams/StructuredOutput.cs:292-349`) kept as `HarnessWords.Failed` (`Sessions/Domain/Observation.cs:629-632`). No `turnFailed` on this door (`Driver.cs:3730`) |
| Protocol | `codex-acp` (`Adapters.cs:2053-2115`) | Codex retries a dropped stream and 5xx itself, each attempt a notification with `willRetry: true`, forwarded as `session_info_update` with `_meta.codex.error` (`codex-acp/dist/index.js:30823-30834`) | A usage limit: a JSON-RPC error, `data` `{ message, codexErrorInfo, additionalDetails }` (`index.js:30836-30840`, `:30968-30979`). An unauthorised turn: `-32000` (`:30803-30807`). **Every other terminal error: the readable message as an agent text chunk, and the prompt answered `end_turn`** (`:30842-30844`) | The limit's sentence (`CodexLimits.Words`, `Accounts/Usage/AccountLimits.cs:346-361`). Not `codexErrorInfo`. Not sign-in: `SignInOf("codex-acp")` is null, since `codex` is a holder and not an adapter (`Accounts/Harnesses/HarnessRoster.cs:135-144`) |
| Protocol | `dsh` (`Adapters.cs:1960-2023`), plugins (`Plugins/DeclaredAcpAdapter.cs:21-63`), `acp-stub` | Unknown | A JSON-RPC error, or the stream's end | The message string; no limit, sign-in or refusal table (`DeclaredAcpAdapter.cs:46-59`); `acp-stub` reads the stub's (`Adapters.cs:1723-1731`, `:1785-1791`) |

*Read, not measured:* the Codex rows, and the pipe door's `api_error_status`. Measured: shapes 1 and 2 above, and TURNFAIL1's
`API Error: Connection dropped (ECONNRESET)` on the protocol door (`docs/decisions/D137.md:593`, the install's session `cbf2fe70`).

### 1.2 What Daoris reads of an ended turn

- **No member of the interface classifies a failure.** `ISessionAdapter` (`Adapters.cs:1321-1513`) declares the door, its postures,
  its frames and its `Toolchain`. The words a tool fails in are data on `HarnessToolchain` (`Accounts/Harnesses/Harnesses.cs:176-…`):
  `Refused` (a credential's refusal, a substring), `Limits`, `SignIn`, `Windows`. They are read in four places, each its own way.
- **The conclusion** (`Driver.cs:2710-2791`): `Observation.Conclude` makes a `turnFailed` run `failed`, `ended.turn-failed-taken` or
  `-open` (`Observation.cs:125-130`), and a `refusal` stop reason `failed`, `ended.provider-refused` (APIREFUSAL1, `:135-136`).
  Then three readers in order: `AccountRefused` (`Toolchain.Refused`, read from `ended.Failure` and the harness's last lines,
  `Driver.cs:3238-3255`, `Observation.cs:457-462`), `AccountSignedOut` (`SignInRefusals`, `Driver.cs:3271-3275`) and
  `AccountLimited` (`Driver.cs:3157-3199`, which returns at once without a `turnFailed`, `:3161`).
- **Gaps.** The pipe door passes no `turnFailed`, so its failed `result` is read neither for a limit nor a sign-in
  (`Driver.cs:3730`, `:3161`; D206's row 1f). `ClaudeAcpAdapter` declares no `Refused`, and `AccountRefused` has no owner's fallback
  as `LimitsOf` and `SignInOf` have (`HarnessRoster.cs:120-144`), so *API Error: 401* is never checked on the protocol door. The
  native conversation reads no failed result at all (`NativeChat`, `Conversations/Chat/ChatRunner.cs:2971`). Nothing in the driver
  reads `529`, *Overloaded*, a 5xx, `api_error_status` or `codexErrorInfo`: every match in `src/` is a test's negative fixture
  (`AccountLimitsTests.cs:258`, `ObservationTests.cs:147`).
- **A conversation's turn** (`ChatRunner.cs:2889-2922`): any `DriverException` becomes `door.turn-not-taken`, read for a limit
  (`_limited`) and a sign-in (`ReadSignIn`), and the turn ends `error`. Nothing tries again.
- **A word refused mid-turn** is a note and nothing else: *a word the agent would not take costs that word, … never the session's
  work* (`Acp.cs:878-882`). That is shape 2's path: the refusal named the provider's failure, and the turn read as clean.

### 1.3 What Daoris tries again today

- **Strikes** (D80): a `failed` record not limited and not refused, or an interrupted stop, counts one (`Service/ServiceClient.cs:2099-2121`);
  `Strikes = 3` parks the quest `Exhausted` (`Quests/Domain/Planner.cs:1860-1875`, `Platform/DriverConfig.cs:38`). A failed taken
  quest is carried on in its tree before that (`Planner.cs:1565-1573`), and a carry-on walks and is briefed (`Driver.cs:1731-1735`),
  which is shape 1's lost conversation.
- **TURNFAIL1** (D137's note): a resume that carried the person's words and failed leaves them on `said` (`ended.words-wait`), the
  next look goes on with them in the same conversation, and a second failure in a row marks them `GoOnMarks.FailedTwice` to wait for
  the person (`ended.words-wait-you`; `FailedTurnWords.Judge`, `Observation.cs:494-528`; `DriverContinuations.cs:696-716`). This is
  the only retry in the same conversation, and only for a resume carrying words, bounded at two, paced by the look.
- **SESSCTX1c**: one more run of a native resume whose harness exited before naming its conversation (`DriverContinuations.cs:1204-1237`).
- **D206**: a limited line resumes its own conversation at its account's reset (`Loop/LimitResume.cs`).
- Nothing else. The backoffs in the driver are a file watch's, a plugin hook's and a workflow node's (`Platform/ChangeWatch.cs:189-192`,
  `Plugins/Domain/WorkBounds.cs:22`, `Trees/Nodes/NodeContract.cs:201`).

## 2. The decision in one paragraph

Every door hands its failure to one reader at the agent interface as a **door failure**: the fields its wire carries (a JSON-RPC code
and `data`, a `result`'s status and the SDK's error field, Codex's error info) and the words, never the agent's own message. **One
reader** (`AgentErrors.Read`) answers it as one of four **kinds**, `transient`, `limit`, `access` or `fatal`, from the adapter's own
table of codes and recorded sentences, its owner's where it has none. A limit is read first, then access, then transient; anything
unread is fatal, as today. **One policy** acts on the kind. A transient error is tried again in the same conversation, on the same
account and choice, after 1, 2, 4, 8 and 16 minutes, five tries in 31 minutes, the person's unreached words first and Daoris's one
go-on word after, said once in the conversation, and it is never a strike, a park, a cool-off or a walk. Past the bound the record ends
`failed` under `ended.transient`, which no strike counts, and its words wait for the person. A limit is D206's, unchanged. An access
error marks the account as today and goes to the person; the line walks to another account where one is ready. A fatal error fails with
its reason, a strike, as today. Every kind of run takes the same retry inside its run, and differs only past the bound.

## 3. The kinds, and what decides each

### 3.1 The four kinds

| Kind | What it is | Decided by (any of) | The policy |
|---|---|---|---|
| `limit` | The account's usage is spent: it comes back at a reset | The adapter's `Limits` table reads a sentence and a reset (`AccountLimits.Read`, unchanged); Codex's `codexErrorInfo` `usageLimitExceeded` | D206 (§5.2) |
| `access` | The account or its credential cannot be used until a person acts: signed out, refused, not permitted, not verified, no credit | ACP's `-32000` *authentication required* (the protocol's own code, every protocol door); the SDK's `authentication_failed`, `oauth_org_not_allowed`, `account_on_hold`, `verification_required`, `cloud_credential_error`, `billing_error`; an HTTP 401 or 403 the door names; Codex's `unauthorized`; the adapter's `SignIn` and `Refused` words, as today | To the person (§5.3) |
| `transient` | The provider or the way to it failed for a moment: nothing about the account, the request or the work | The SDK's `overloaded` or `server_error`; an HTTP status the door names from 500 to 599 (529 among them); Codex's `serverOverloaded`, `internalServerError`, `httpConnectionFailed`, `responseStreamConnectionFailed`, `responseStreamDisconnected`, `responseTooManyFailedAttempts`; a 429 or the SDK's `rate_limit` that the `Limits` table reads no sentence in; the adapter's recorded transient sentences (§4.4) | Tried again, bounded (§5.1) |
| `fatal` | The request itself cannot succeed by repeating it, or Daoris cannot tell | The SDK's `invalid_request`, `model_not_found`, `max_output_tokens`; Codex's `contextWindowExceeded`, `badRequest`, `cyberPolicy`, `misalignmentPolicyViolation`, `sandboxError`; a `refusal` stop reason (APIREFUSAL1); **anything no table reads** | Fails with its reason (§5.4) |

**Not errors of the agent**, and outside this policy: the person's stop or pause; Daoris's own stop (a timeout, a quiet end, an
update's close, a hand-over); the harness's process ending before it answered (`StreamEnded`, `Acp.cs:1879-1881`), which is the
harness, not the provider, and stays `failed` with its reason and SESSCTX1c's one native retry; a model refused at the open, D205's
`choice-not-offered`; a tool call the posture refused, which is the turn going on, not ending.

### 3.2 The order

One failure can carry more than one reading: Claude's weekly limit came as `-32603` with `errorKind: "rate_limit"` and the limit's
sentence in the message (`docs/2026-10-23-model-effort-doors-evidence.md:116`). So the reader decides in a fixed order and stops at
the first that holds:

1. **`limit`** where the `Limits` table reads a limit in the words, whatever the code says. D206's reading stands first, so nothing
   this design adds can turn a limit into a retry that spends five tries on a cooling account.
2. **`access`**, since retrying a refused credential spends an attempt on a refusal that cannot lift.
3. **`transient`** by code, then by the recorded sentences.
4. **`fatal`** otherwise.

A code outranks a sentence within a kind, and the reader never reads a kind from a code the door does not carry: an absent
`errorKind` is no reading, never `unknown`.

### 3.3 What may decide a kind: the door's own failure, never the agent's message

D125 point 1.4 holds: a limit is read from the door's failure, never from the transcript's words, since an agent can write any
sentence. The same holds for every kind. On shape 1 the error is the prompt's JSON-RPC error. On shape 2 it is the steer's JSON-RPC
error (§4.2), also the door's own. The agent message that streams the same text before either (events 464 and 2300) is never read:
a model quoting a log line that says *529 Overloaded* must not stop its own turn.

This rule leaves one door with nothing to read. `codex-acp` says a terminal non-limit error only as agent text, under `end_turn`
(§1.1). Such a turn stays read as it is today until row 1f records the frame that marks it (§4.3).

An entry in any table grows only with a recorded sentence or frame held both ways, as D125's limit table does (a sentence that must
read as its kind and a near one that must not). The codes above are what the adapters' source and the SDK's declaration name; each
row records its door's frames before its table reads them (§11).

## 4. Where in the interface each adapter answers

### 4.1 The door failure and the reader

- **`DoorFailure`** (new, `Agents/Errors/`): what a door's wire carried when a turn ended on a failure, and nothing the agent wrote.
  It has `Door` (`pipe` or `protocol`), `Code` (the JSON-RPC code), `Kind` (the wire's own kind word: `data.errorKind`, the SDK's
  `error`, Codex's `codexErrorInfo` key), `Status` (an HTTP status the wire names: `api_error_status`, Codex's `httpStatusCode`),
  `Words` (the error's own sentence: `AcpRefusal.Words`, the `result`'s text) and `Where` (`prompt`, `steer` or `result`). Each is
  null where its wire carried none.
- **`AgentError`** (new): the reader's answer: `Kind` (§3.1), the `DoorFailure` it read, and `By`, the table entry that decided it
  (`code:-32000`, `kind:overloaded`, `status:529`, `words:transient-1`), which the machine log names.
- **`AgentErrors.Read(ISessionAdapter adapter, DoorFailure failure)`** (new): the one reader. It applies §3.2's order over the
  adapter's tables: `Limits` (through `AccountLimits.Read`, unchanged), then access (`SignIn`, `Refused` and the access codes), then
  transient. Like `LimitsOf` and `SignInOf` (`HarnessRoster.cs:120-144`), it falls back to the adapter's owner's tables, so
  `claude-code-acp` reads `claude-code`'s. `AccountRefused`'s missing fallback (§1.2) is closed by going through it.
- **`ErrorWords`** (new, on `HarnessToolchain`, beside `Limits` and `SignIn`): the adapter's own map from its codes, kind words and
  statuses to a kind, with its recorded transient sentences. Its shape is `LimitWords`' (`AccountLimits.cs:296-361`): anchored,
  case-blind patterns under a timeout, each recorded entry with its source. A plugin's harness declares none, and reads the protocol's
  own codes alone (§4.2).

`turnFailed` stays the string every reader already takes, so nothing that reads it changes in row 1a. The conclusion gains the
`AgentError` beside it, and `AccountRefused`, `AccountSignedOut` and `AccountLimited` each become a reading of its kind.

### 4.2 Each door's answer

| Door | Where its `DoorFailure` is made | What it fills |
|---|---|---|
| Protocol, every adapter | `AcpSession`, where a `session/prompt`'s `error` becomes `AcpRefusal.Of` (`Acp.cs:224-239`, `:2019-2024`); the conclusion's capture (`Driver.cs:4312-4326`) and the conversation's (`ChatRunner.cs:2889-2922`) keep it, where they keep `error.Message` today | `Code`, `Kind` (`data.errorKind`, or the key of `data.codexErrorInfo`, a string or a one-key object), `Status` (`codexErrorInfo`'s `httpStatusCode`), `Words` (`AcpRefusal.Words`), `Where: prompt`. ACP's `-32000` is `access` on every protocol door, a plugin's included |
| Protocol, shape 2 | `AcpSession`'s steer: the refused word's `AcpRefusal` (`Acp.cs:878-882`) is kept as the turn's **last refusal** where it carries a `Kind` or a `Code` other than a steer refused for its shape (`-32602`). Where the prompt then answers `end_turn` and no agent update came after that refusal but the error's own message, the turn ended on it | The same fields, `Where: steer`. The refused word goes back to the run as `Unreached`, as USERSHOWN1's words do (`Acp.cs:1087-1091`) |
| Pipe, `claude-code` | `ClaudeStreamJson.Result` (`StructuredOutput.cs:292-349`), which reads `is_error` and the text today. It also reads `api_error_status`, and keeps the `error` field of the last assistant frame before it | `Status`, `Kind` (the SDK's `error`), `Words` (`result`), `Where: result`. It goes to the conclusion as the pipe door's `turnFailed`, which closes D206 row 1f's gap for limits and sign-ins in the same reader |
| Protocol, `codex-acp` | Its limit, as today, and `-32000`. Its terminal non-limit error after row 1f records how it is marked (§4.3) | As the protocol row |

### 4.3 The Codex door's terminal error

`codex-acp` 2.1.1 marks a terminal non-limit error only for a client that declared AIR's typed session failures. It then answers the
prompt `end_turn` with `_meta.jetbrains.air.sessionFailure` (`index.js:39993-40004`), whose `category` is `service`, `connection`,
`limit`, `access` or `request` (`:30852-30870`). Otherwise it says the error as agent text. Two ways can mark it, and row 1f measures
both before it chooses:

- **Declare the capability**, for `codex-acp` alone, and read `category` (`service` and `connection` → `transient`, `access` →
  `access`, `limit` → the `Limits` table first, `request` → `fatal`).
- **Read the turn's own retry notices**: Codex's `willRetry` attempts reach the client in the default mode as
  `session_info_update` `_meta.codex.error` with `codexErrorInfo` (`index.js:30823-30834`). A turn that ended `end_turn` with no
  model output after such a notice ended on that error.

Until row 1f, a Codex turn ended this way reads as it does today, and §12 says so.

### 4.4 The tables, first entries

Recorded on the install, held both ways in row 1a:

- `claude-code`'s transient sentences, read in the error's own `Words`:
  - `API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment. …` (sessions `61306f9f`
    and `5bafda3b`, 2026-10-24);
  - `API Error: Connection dropped (ECONNRESET)` (session `cbf2fe70`, 2026-10-14; D137's TURNFAIL1 note).
- Its codes: `errorKind` `overloaded` and `server_error` → transient (shape 1 carried `overloaded`, by the adapter's source;
  row 1a's first act is to record it on the wire with a stand-in that replays the adapter's frame).
- A near sentence that must read as nothing: `API Error: 401 …` (access, by `Refused`), `You've hit your weekly limit · resets …`
  (limit), and `API Error: 400 …` (fatal).

The rest of §3.1's codes enter each table as its row records them.

## 5. The one policy

### 5.1 Transient: tried again in the same conversation

- **Where.** In the run, before its conclusion: the record stays `running`, its process, account, choice and slot under the cap
  are its own, and no record, part or brief is made. That is what keeps the conversation (shape 1 lost 219,284 tokens of it) and
  what keeps a retry from being a strike or a park: neither is reached while the run goes on.
- **The backoff and the bound.** After a transient failure the run waits 1 minute, then tries again; then 2, 4, 8 and 16 minutes.
  Five tries over 31 minutes. The delays are `TransientRetry.Delays`, one constant set for every door and every kind of run. Why so
  slow at first: the harness already retried before it gave up (Claude Code's `api_retry`, Codex's `willRetry`; on shape 1 the
  error came three and a half minutes after the last tool result). A retry within seconds of that only meets the same outage. Why the
  bound: an outage past half an hour, after the harness's own retries, is one the person should know of. Waiting spends no tokens,
  but it holds a slot under the cap.
- **What each try sends.** On the protocol door, a new `session/prompt` on the same live process and the same conversation. On the
  pipe door, whose run ended with its `result`, a new run of the same conversation with `--resume <id>`, as SESSCTX1c's `AgainAsync`
  runs it (`DriverContinuations.cs:1204-1237`), on the same account and record. The prompt is:
  1. **the person's words the door never took**, verbatim and in the order said: the turn's `Unreached` and `HandedBack` and the
     inbox's held words (TURNFAIL1, `Driver.cs:4322-4325`), and shape 2's refused word;
  2. then **Daoris's go-on word** (`retry.go-on`): *The last request failed on the provider's side ({{words}}). Go on where you
     stopped.* It is kept through the say door under `DaorisKeys.TransientRetry(session, episode)`, as D206's `limit.reset` is kept
     under `DaorisKeys.LimitReset`. It is kept once per episode and re-sent by its id at each later try, never kept again, so the
     conversation shows it once. Every end of the episode takes it off by id.

  Words the turn already took are in the agent's own conversation and are not sent again.
- **Said once.** The conversation gains one line at the episode's first failure, `agent.transient-retrying`: *{{agent}}'s provider
  failed ({{words}}). Daoris tries again in this conversation: up to 5 times over 31 minutes, the next at {{time}}.* No line is said
  per try, and none for a try that works, since the turn going on says it. The machine log says each try (`agent.transient`: session,
  adapter, account, `By`, try, next). The session's head shows *Trying again at {{time}}* while it waits, read from that line and the
  machine's clock.
- **What the waiting run is.** The run waits on Daoris's own timer, never on its agent, so the session watch (D200's watcher,
  `Sessions/Watch/SessionWatch.cs`) reads it as waiting, as it reads `background.waits` (`Platform/NoteCodes.cs:912`), and ends nothing
  for being quiet. The wait reads its delays off the clock it is handed (`.claude/knowledge/held-clocks.md`), which the tests hold.
- **What ends an episode early.** A try that answers ends it; the count starts again at the next failure. The person's stop or pause
  ends it as theirs, with no failure said. The person's word during a wait is sent at once as the next try, theirs first and Daoris's
  word after, and counts as a try. A try that fails with another kind leaves the episode for that kind's policy: a limit for D206's
  (§6.1), access for §5.3's, fatal for §5.4's.
- **Past the bound.** The run ends. Its record is `failed` with `ended.transient`: *Its provider kept failing after 5 tries over 31
  minutes ({{words}}). It waits for you: your words, or Try again, go on in the same conversation.* The go-on word and any unreached
  words stay on `said`, marked `GoOnMarks.Transient` so no look takes them (TURNFAIL1's `FailedTwice` does the same, and is passed
  over by every bound that reads a first failure). The person's next word, or *Try again* (D137's resume of words waiting on a
  record, `DriverContinuations.ResumesOwn`), resumes the same conversation, a new episode with its own bound. *Go on now with…*
  (D163's offer, D206's press) is offered beside it. The line is not parked: `awaiting-person` is never reached on this path, and
  `AttentionWatch` raises one *Ended* notice for it (`Quests/Domain/Attention.cs:248-254`), worded by its code.
- **Never.** No strike: `ReadStrikes` passes `ended.transient` by its code, as it passes `update.closed` (`ServiceClient.cs:2111`).
  No cool-off and no `limit` flag: the provider's outage is not the account's. No walk to another account or choice (§6.2). No park.

### 5.2 Limit: D206, unchanged

A `limit` is read by the same table and sentence as today (`AccountLimits.Read`). It cools its account, ends `failed` with the
ledger's `limit` flag and no strike, and is resumed by D206's planner (own account's conversation first, else briefed, else it waits).
What this design changes for a limit is where it is read: in `AgentErrors.Read`, first, from every door's `DoorFailure`. For the pipe
door that is new (D206's row 1f, which this design's row 1c builds). For shape 2's steer it is new as well: a limit that refused a
steered word now ends the turn as a limit instead of leaving it clean.

### 5.3 Access: to the person

- **The account** is marked as today: refused (`AccountRefused`, ROSTER1b) for a credential the provider refused or an account it
  holds, signed out (`AccountSignedOut`) for a sign-in that ended. No strike, as today (`ServiceClient.cs:2093-2097`). What changes
  is coverage: every door's access codes reach it, not only the sentences two adapters declare, and `-32000` reaches it on every
  protocol door.
- **The person** is told once, at the account: the attention band's account row (*`{{account}}` needs you to sign in again*, its
  existing words), and the record's note names the account's state, never the credential's words.
- **The line** walks past the account at its next start, as any start does past a signed-out or refused account. Where none is
  ready, the start is held with today's sentence (`held.wait-every`, `held.wait-first`, `held.cooling-account`). A conversation's
  next press is refused in it. Nothing retries a refused credential by itself.
- **Not tried again in the run.** A sign-in or permission is the person's to give (D191 point 3 keeps a sign-in theirs), so the run
  ends at the first access error.

### 5.4 Fatal: failing with its reason

As today: `failed` with the door's words (`ended.turn-failed-*`, `ended.provider-refused`), a strike, carried on as a failed run is,
and TURNFAIL1's words wait. What changes is that the note names the kind's reason where the reader has one (*the request was too
long for the model's context*), and the machine log names `By`. An error no table reads stays fatal: guessing transient would spend
five tries on a request that cannot succeed, and guessing access would stop a line for nothing the person can do.

## 6. With D206's limit resume and D205's walk

### 6.1 D206

- **The limit is read first** (§3.2), so a limit is never retried as transient, and a 429 with a reset sentence cools its account.
- **A transient failure never cools an account**, writes no `cooling.json` entry and sets no `limit` flag. So it never reaches D206's
  planner, its computed wait, its `limit.not-lifted` or its `LimitWait` words.
- **Inside D206's resume.** A transient failure on a limited line's own resume (D206 point 3.i, `ResumeAsync(…, limited: true)`) is
  tried again in that run as any is. `limit.reset` stays kept while it does, and the episode's go-on word goes after it. Past the bound
  the record ends `ended.transient`, and D206's word comes off as every conclusion takes it off. A limit read on a try ends the
  episode as a limit: D206 point 3 from there. `limit.not-lifted` reads only a limit, so a transient failure within the margin after a
  reset is not *not lifted*.
- **Conversations.** A conversation waiting on a limit (`ChatTurns.WaitForLimit`) is not running a turn, so nothing here applies to
  it until its wake. Its wake's turn is a turn like any other.

### 6.2 D205

- **A transient failure never walks.** D205 point 5 holds: *a running session never moves to another choice*, and *a refusal after a
  prompt was sent is never a fallback*. A provider's outage often holds every account of its maker, and the point of the retry is the
  same conversation. Moving the work across accounts would brief it into a new part (shape 1's loss); moving it across makers is the
  person's choice (D205 point 5, D206 point 6).
- **Past the bound**, *Go on now with…* names the actor's next choice, as D206's limit line does (`work/waits/LimitWaitLine.tsx`), and
  it is the person's press.
- **Access** passes the account in the next start's walk, which is D197's walk unchanged.
- **At the open.** A model refused before any prompt stays D205's `choice-not-offered`. A transient failure at the open, before any
  prompt was sent, is read by the same reader and retried by the same policy, inside the start.

### 6.3 TURNFAIL1 and APIREFUSAL1

- TURNFAIL1's look-paced retry of a resume's words (§1.3) is kept for a **fatal** failure, and for a transient one only past its
  bound, where §5.1 marks the words to wait for the person. A transient failure no longer takes TURNFAIL1's path at all.
- APIREFUSAL1's `refusal` stop reason is a `fatal` kind's reading (§3.1), unchanged in its note and its strike.

## 7. What each kind of run does

Every kind of run takes §5.1's retry inside its run, since every kind's turn goes through a door's capture: driven sessions
(`Driver.cs:4126`), conversations (`ChatRunner.cs:2885`), and helpers, opinions, intakes and partners through `HoldAsync`'s capture
(`DriverHelpers.cs:907-1037`, `DriverOpinions.cs:34-259`, `DriverIntakes.cs:367-419`, `DriverPartners.cs:620`). What differs is the end
past the bound.

| Kind of run | During the retry | Past the bound |
|---|---|---|
| **A driven session** | Its record stays `running`; the conversation says `agent.transient-retrying` once; the head says *Trying again at {{time}}* | `failed`, `ended.transient`, no strike, the go-on word and unreached words waiting for the person (§5.1); *Ended* raised once; *Try again* and *Go on now with…* on its header |
| **Ask Daoris and a repository's chat** | The turn does not end `error`: the runner waits and tries again on the same process (protocol) or by `--resume` (pipe, once row 1c lets `NativeChat` read a failure). Words said meanwhile are kept on `said` and shown queued; they go as the next try at once (§5.1). One line `chat.transient-retrying` | The turn ends `error` with `chat.transient-stopped`: *{{agent}}'s provider kept failing ({{words}}): your next words go on in this conversation.* The conversation stays; the person's next word is a turn as today. No record state changes, as a chat's failed turn changes none today |
| **A helper** | Its pass waits and tries again inside its run | Settled `refused` with a reason of its own, `HelperWhy.Transient`, said `helper.transient` on the asker (*Helper {{agent}} could not answer: its provider kept failing ({{words}})*), not `no answer`. A lead waiting on it goes back to the person as a refused helper's lead does (`BackToPersonAsync`, `DriverHelpers.cs:363-386`), now with the reason |
| **A second opinion** | Its pass waits and tries again inside its run | The pass is `failed` with why `transient` (`Opinions` gains it beside `limited`); the gate is `Unavailable` with code `transient` and no `Until`, so the look does not re-ask it (`OpinionGateLook.cs:220-221` re-asks only a cooling gate) and the person's *Try again* does |
| **An intake** | Its run waits and tries again | `failed`, `intake.transient`. The ask is unbound once (`unbindAsk`, as a limited intake's is, `DriverIntakes.cs:405-409`), so the next look picks it again with a fresh intake. An ask whose last intake already ended `intake.transient` stays bound, and its *Ended* is raised, so an outage costs at most two bounds before the person hears |
| **A partner** | Its run waits and tries again | The piece goes to `Ending` and the lead is told `partner.transient`, the work kept, as `partner.limited` is (`DriverPartners.cs:620-623`, `PartnerNotes.cs:34`) |
| **A brainstorm voice** | Tries only while its pass's minutes allow: a delay past the pass's deadline is not waited | The voice is dropped `Unavailable` with the reason, as a voice that cannot run is (D205 keeps voices frozen) |

## 8. The record and the notes

- **The ledger.** No new state, column or flag. A transient end is `failed` with its note code, as D205 kept `choice-not-offered`
  `failed` and D206 kept the `limit` flag, for the same reason: a teammate's mirror and an older build read the state set, and a new
  state is nothing they know (`.claude/knowledge/compatible-builds.md`). An older build reading this machine's ledger counts an
  `ended.transient` record as a strike, which is today's behaviour.
- **The strikes** pass `ended.transient` by its code (`ReadStrikes`, as `update.closed`).
- **Note codes** (kebab case, as `NoteCodesTests` holds; en and zh in the note catalogue, `src/Daoris.Web/src/work/noteLines.ts`):

  | Code | Where | English | 中文 |
  |---|---|---|---|
  | `agent.transient-retrying` | the conversation, once per episode | *{{agent}}'s provider failed ({{words}}). Daoris tries again in this conversation: up to {{count}} times over {{minutes}} minutes, the next at {{time}}.* | *{{agent}} 的服务方出错（{{words}}）。道衍会在本对话中再试：最多 {{count}} 次，共 {{minutes}} 分钟，下一次在 {{time}}。* |
  | `ended.transient` | the record's note | *Its provider kept failing after {{count}} tries over {{minutes}} minutes ({{words}}). It waits for you: your words, or Try again, go on in the same conversation.* | *它的服务方在 {{minutes}} 分钟内试了 {{count}} 次仍然出错（{{words}}）。它在等你：你的话或"再试一次"会在同一对话中继续。* |
  | `chat.transient-stopped` | a conversation's line | *{{agent}}'s provider kept failing ({{words}}): your next words go on in this conversation.* | *{{agent}} 的服务方一直出错（{{words}}）：你接下来的话会在本对话中继续。* |
  | `helper.transient` | the asker's record | *Helper {{agent}} could not answer: its provider kept failing ({{words}}).* | *助手 {{agent}} 没能回答：它的服务方一直出错（{{words}}）。* |
  | `intake.transient` | the intake's record | *the agent's provider kept failing before it published anything onto ask `#{{ask}}` ({{words}}).* | *智能体的服务方在它向请求 `#{{ask}}` 发布任何内容之前一直出错（{{words}}）。* |
  | `partner.transient` | the lead's record | *Partner {{agent}} stopped: its provider kept failing ({{words}}). Its work is kept.* | *搭档 {{agent}} 停下了：它的服务方一直出错（{{words}}）。它的工作已保留。* |
  | `retry.go-on` | Daoris's word to the agent | *The last request failed on the provider's side ({{words}}). Go on where you stopped.* | the agent's word, English only, as `limit.reset` is |

  `{{words}}` is the door's own sentence, said as a program's (`NoteBy.Program`), never translated: authored content stays as written.
  The glossary gains *try again* / 再试一次 (the press, beside D206's *Try now*, which is an account's).
- **The machine log** says `agent.error` once per failure read (session, adapter, account, kind, `By`) and `agent.transient` once per
  try (try, next). Neither is a note.
- **The page.** A failed record with `ended.transient` reads *failed* in its dot, worded by its note, with no red: it waits on the
  person's word, not on a fault in the work. The head's *Trying again at {{time}}* is drawn from `agent.transient-retrying` while no
  turn and no end follow it, as D206's `chatLimitHeldOf` reads its line (`work/waits/limitWait.ts`).

## 9. What it amends, each by a dated note on acceptance

- **D137's TURNFAIL1 note**: a transient failure is retried in its run and never takes the look's retry; past its bound its words wait
  for the person at once.
- **D125 point 1** and **D206 point 1** (§3.1's pipe door): the limit is read in `AgentErrors.Read`, first, from every door's
  failure, the pipe door's included; the table and the reset are unchanged.
- **D80** (strikes): `ended.transient` is no strike.
- **D200** (the session watch): a run waiting on a retry is waiting, not quiet.
- **APIREFUSAL1's FIX-LOG entry** and **ROSTER1b**: their readings become `fatal` and `access` kinds; their behaviour is unchanged.
- **D205 point 5** and **D206 point 6** are not amended: §6.2 keeps them.

## 10. Considered and rejected

- **A retry by the look**, as TURNFAIL1 retries: the look's next start of a failed record walks and is briefed (`Driver.cs:1731-1735`),
  which loses the conversation, and its pace is the poll's, not a backoff.
- **A new state, `retrying` or `transient`**: the same reason as D205 and D206 (§8).
- **Cooling the account for a transient error**: the outage is the provider's, and a cool-off would push the next start onto another
  account and brief it, which is shape 1's loss.
- **Walking to another account or choice at once**: D205 point 5, and the brief's loss (§6.2).
- **Reading the agent's streamed message**, which carries the same text first in both shapes: D125 point 1.4 (§3.3).
- **A transient guess for an unread error**: five tries on a request that cannot succeed, each holding a slot (§5.4).
- **Retrying within seconds**: the harness has already retried (§5.1).
- **No bound**, or a second tier the look resumes by itself an hour later: an outage past half an hour after the harness's own retries
  is the person's to know, and a later row can add a tier once the install shows outages that long.
- **A setting for the delays**: one constant set until a case asks for another. Zero, *off*, would be today's behaviour, which the owner
  directed away from.
- **Declaring AIR's typed session failures for every protocol door now** (`_meta.jetbrains.air.capabilities: ["sessionFailure"]`):
  it is one client vendor's extension. Declared, `claude-agent-acp` drops the error's words from the prompt's error
  (`internalErrorForClient`, `acp-agent.js:2369`) and moves its failures to `session_info_update`, which changes a door that already
  reads well (shape 1). Row 1f weighs it for `codex-acp` alone, where the default mode marks nothing (§4.3).
- **A table per kind of run**: the kinds are the adapter's, the policy the interface's; only what follows the bound is the run's (§7).

## 11. Rows, in build order, with their proofs

Every row runs its own tests and the classes covering the code it changes, filtered, and the census checks `docs/development.md`
names (*Choose the checks*). Each stand-in agent is a script under the tests, never the agent's binary (`node tools/live-logins.mjs`).
The backoff's tests hold the clock (`.claude/knowledge/held-clocks.md`).

| Row | What | After | Proof |
|---|---|---|---|
| **AGENTERRORS1a** | The kinds and the reader at the interface (`DoorFailure`, `AgentError`, `AgentErrors.Read`, `ErrorWords`, §4.4's first entries); the protocol door's prompt and steer failures (§4.2); §5.1's retry for a **driven session** on the protocol door; `ended.transient` and `ReadStrikes`; `agent.transient-retrying`, `ended.transient` and `retry.go-on` in en and zh; the head's line | — | The reader's table both ways (`AgentErrorsTests`, a fast class). A stand-in ACP agent answering its prompt `-32603` *Internal error: API Error: 529 Overloaded. …* with `data.errorKind: "overloaded"` twice, then working: retried in the **same** conversation (one record, the same session id, no `moved.on`), it goes on and ends as its work does, with one `agent.transient-retrying` line, no strike and no `cooling.json` entry, red first against today's `ended.turn-failed-taken`. The same agent erring every try: past the bound `failed`, `ended.transient`, the go-on word waiting marked, no strike, no cool-off, no park. Shape 2: a steered word refused with the same error and the prompt `end_turn`: retried, never `awaiting-person`. A weekly limit's sentence under `errorKind: "rate_limit"`: limited, never retried. `-32000`: access, no retry. `NoteCodesTests`, `i18n:check`, `names:check --strict` |
| **AGENTERRORS1b** | §5.1 and §7 for **conversations**, Ask Daoris and a repository's chat, on the protocol door: the wait, words said meanwhile, `chat.transient-stopped` | 1a | A stand-in conversation's turn failing transient twice then answering: one turn, the same process, the person's words once; a word said during the wait goes at once and first; erring every try ends the turn `error` with `chat.transient-stopped`, and the next word is a turn of the same conversation (`ChatGoesOnProcessTests` or a class of its own); the page's line in en and zh |
| **AGENTERRORS1c** | The **pipe door**: `ClaudeStreamJson.Result` reads `api_error_status` and the assistant's `error` into a `DoorFailure`, the conclusion's `turnFailed` on this door (D206 row 1f's gap: limits and sign-ins read here too), the retry by `--resume`, and `NativeChat`'s reading of a failed result | 1a, 1b | A recorded native `result` frame with `is_error` (row 1c's first act records one, or replays the SDK's declared shape marked unmeasured): a stand-in native agent erring twice then working is resumed with `--resume` in the same record and goes on; one that keeps erring ends `ended.transient`; its limit sentence cools its account (`NativeResumeTests`, a native tick class) |
| **AGENTERRORS1d** | §7's **other kinds of run** past the bound: helper (`HelperWhy.Transient`), opinion (`transient` why and gate), intake (one unbind, then bound), partner, brainstorm (the deadline) | 1a; 1c for this install's intakes and helpers, which run on `claude-code` (`data/driver.json`'s `intakeAdapter`, `helperAdapter`) | Each kind's stand-in erring every try ends past the bound as §7 says, and erring twice then working goes on in its run (`HelperLookTests`, `OpinionLookTests`, `IntakeTests`, the partners' and brainstorms' classes) |
| **AGENTERRORS1e** | **Access** on every door (§5.3): `-32000` on every protocol door, `AccountRefused` through the reader so `claude-code-acp` reads its owner's `Refused`, the SDK's access kinds, Codex's `unauthorized` tied to its holder's accounts | 1a | A stand-in of each door refusing with its access error: the account marked, no strike, no retry, the next start walks past it, and with none ready the start is held in today's sentence (`SignInRefusalsTests`, `AccountRotationTickTests`) |
| **AGENTERRORS1f** | **`codex-acp`'s** codes (§3.1, §4.3): record its frames (a usage limit, a dropped stream with `willRetry`, a terminal overload), choose between §4.3's two markings by what is recorded, then read `codexErrorInfo` | 1a | The recorded frames, held both ways; a stand-in `codex-acp` erring twice then working retried in the same conversation; a Codex turn that ends `end_turn` after its own retries, read as transient by the chosen marking |
| **AGENTERRORS1g** | The install's proof | 1a-1f | On the installed window, a session that meets a real provider error (the next one the install's machine log shows) reads as §5.1 says: one line, the same conversation, no strike; the head's line seen in en and zh |

The first row gives the most: it is the install's door, both of its measured shapes and every driven session.

## 12. What the checks do not cover

- A document. The code was read at `64059110a`; the adapters' behaviour is read from their source in the install's toolchain cache,
  `claude-agent-acp` 0.84.0 and `codex-acp` 2.1.1, and an adapter's update can move it. Shapes 1 and 2 are measured, from the
  install's records; `data.errorKind` on shape 1 is read from the adapter's source, since the record keeps only the message.
- The pipe door's `api_error_status` and the SDK's `error` field are the SDK's declaration, never seen on this machine.
- Codex's terminal errors are unmeasured, and until row 1f a Codex turn ended on one reads as it does today: with its quest taken it
  parks quoting the error, as shape 2 did.
- A dsh or plugin harness reads ACP's `-32000` alone; its other errors stay fatal until it declares a table.
- How long real outages last is not measured. The 31-minute bound is a reading of the two records (each overload passed within the
  half hour: the person's go-on at 01:02 worked, and `63d56294` ran at 00:31) and of the harness's own retries before it.

## 13. The reads

None yet. Codex's read and the independent Opus reader's foundation read are kept verbatim in `docs/2026-10-24-agent-errors-reads.md`
when they are made, and each finding is taken, answered or deferred here.
