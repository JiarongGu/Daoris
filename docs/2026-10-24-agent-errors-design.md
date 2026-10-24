# Every agent's errors read into one set of kinds at the agent interface, and one policy acting on each kind (AGENTERRORS1, 2026-10-24)

**Status: proposed (D210), round 2.** Written by Claude Opus from the code at `64059110a`, for Codex's read and an independent Opus
reader's foundation read under D191 (its amendment of 2026-10-23: the design changes what every adapter answers at the agent
interface, and the decided rules TURNFAIL1, D206 and D205 rest on).

**Round 2** (2026-10-24) answers round 1's two reads, kept verbatim in `docs/2026-10-24-agent-errors-reads.md`: Codex's (14
findings, 12 blocking; *another round*, keeping the four-kind interface and the ACP driven-session first delivery) and the Opus
foundation read (16 findings, 1-3 blocking, 4-6 to take before acceptance; *accept after named changes*). The code is read again
on main at `fb3eb89eb`. No file this design cites changed since `64059110a` (the commits between touched the driver's platform
wiring, its supervisor's questions and the page's notifications), so round 1's line numbers hold and round 2 cites the same tree.
Every finding is taken, answered or deferred in §14's table, and each section a finding changes carries it marked *(round 2, …)*,
`O` for the Opus read's findings and `C` for Codex's. New in round 2: §5.1's seat, words and hold (O1-O4, C3-C6), §5.5 (the
owner's check, C8), §5.6 (the open, O8, C12) and row 1a2.

The owner's report and direction (2026-10-24, through the quest that asked for this design, and `TASKS.md`'s AGENTERRORS1 row):
SUPERVISOR1a-ii's session ended its turn on `API Error: 529 Overloaded` and waited on the person, and the driver reads no such error.
This is not a fix for one code. Every agent's errors, of every harness and door, are read into one set of kinds at the agent
interface, each adapter mapping its own words and codes, and one policy acts on each kind: a transient error is tried again with a
growing backoff and a bound in the same conversation, the words kept, said once, never a strike or a park; a limit as D206 has it; a
sign-in or permission error goes to the person; a fatal one fails with its reason.

Every claim about the code below names its file and line at `64059110a`, which are main's at `fb3eb89eb`. Paths are under
`src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they say otherwise. What the adapters do is read from their own source, in the
install's toolchain cache (`claude-agent-acp` 0.84.0, `codex-acp` 2.1.1), and is marked *read, not measured* where no record shows it.

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
- **Shape 2: a word the person added mid-turn fails, and the prompt before it ends clean** (session `5bafda3b`, SUPERVISOR1a-ii's,
  00:29 UTC). The same message streamed (event 2300), and then a word the person had added mid-turn was refused with the error
  (event 2302, `words.refused`: *the session would not take what the person added: Internal error: API Error: 529 Overloaded. …*).
  The run's turn read as ended `end_turn` (the log: *the turn ended: end_turn, after 2300 update(s)*). With its quest taken and a
  clean exit, the run parked on the person. The next event is the person's own word 33 minutes later (event 2303, 01:02 UTC): *Go on
  where the API overload stopped you: …*. The run resumed its own conversation (`resumed.opening`, `why: answer`) and went on.

The person's word in shape 2 is what this design automates: the same conversation, told to go on, after the provider recovers. In
shape 1 nothing was asked of the person, but the work lost its conversation and counted a strike for an error that was not its own.

Both shapes are what the adapter's source says it does. A `result` with `is_error` fails the prompt with
`RequestError.internalError(errorKindData(lastAssistantError), message.result)`, where `lastAssistantError` is the SDK's `error`
on the assistant message (`claude-agent-acp/dist/acp-agent.js:4178-4180`, `:4588`). The error's `data` is `{ errorKind }`
(`:7249-7251`), so on shape 1 the wire carried `errorKind: "overloaded"`. Daoris read that field (`AcpRefusal.ErrorKind`,
`Agents/Acp/Acp.cs:206`) and dropped it at the conclusion, which takes `error.Message` alone (`Loop/Driver.cs:4325`).

*(Round 2, O3.)* Shape 2 is STEER1's mid-turn `session/prompt`, not the `_session/steering` lane: that lane's request answers
`injected` at once (`acp-agent.js:2061-2132`) and never carries the provider's error. The adapter settles the running prompt
`end_turn` at the queued prompt's echo (`acp-agent.js:4455-4525`), after the SDK has taken the word, and Daoris's `Answered` then
hands the turn to that word (`head.Taken = true`, `Acp.cs:1667-1671`). The 529 answered **the word's own prompt**, which Daoris
caught as a refused word (`Acp.cs:879-883`). So the word was taken: it is in the agent's conversation, and the turn that failed was
the one it opened. Round 1's citation of `:4150-4153` named the steering lane's branch, and its *the refused word goes back as
`Unreached`* was wrong: the word's own answer removed it from `_prompts` (`:1662`), so `Untaken()` (`:1094-1102`) never lists it.

## 1. Today, by the code

### 1.1 What each door hands Daoris when the provider fails

| Door | Adapter | The harness's own retries | What reaches Daoris at the end | Daoris reads |
|---|---|---|---|---|
| Protocol | `claude-code-acp` (`Agents/Adapters/Adapters.cs:1824-1939`) | Claude Code retries 5xx, 529 and dropped connections itself, reported as `system`/`api_retry` frames (`acp-agent.js:3600-3612`), which the adapter forwards only to a client that declared JetBrains AIR's `sessionFailure` capability (§10) | A JSON-RPC error on the prompt, `-32603` *Internal error: {result}*, `data.errorKind` the SDK's `authentication_failed`, `oauth_org_not_allowed`, `account_on_hold`, `verification_required`, `billing_error`, `rate_limit`, `overloaded`, `invalid_request`, `model_not_found`, `server_error`, `unknown`, `max_output_tokens` or `cloud_credential_error` (the SDK's declaration, `docs/2026-10-02-limit-signals-evidence.md:156`). *Please run /login* is `-32000` (`acp-agent.js:4170-4171`). A mid-turn word's own prompt, shape 2 | The message string: `turnFailed` (`Driver.cs:4312-4326`). `ErrorKind`, `Code` kept on `AcpRefusal` (`Acp.cs:192-240`) and dropped there |
| Pipe | `claude-code` (`Adapters.cs:2166-2410`) | The same; `system`/`api_retry` frames on stdout, not read | The `result` frame: `is_error`, `subtype`, `result`'s text, `api_error_status` (the HTTP status); the harness's synthetic `assistant` frame before it (`model: "<synthetic>"`) carries `error`. *(Round 2, O14.)* Measured by MODELROLE1a: `error: "model_not_found"` with `api_error_status: 404`, and a limit as `error: "rate_limit"`, `api_error_status: 429`, *You've hit your weekly limit · resets …* (`docs/2026-10-23-model-effort-doors-evidence.md:186-204`; held in `Driver.Tests/Agents/ClaudeStreamJsonTests.cs:191-212`) | `is_error` and the text alone, as `StreamMapped.Failed` (`Agents/Streams/StructuredOutput.cs:292-349`) kept as `HarnessWords.Failed` (`Sessions/Domain/Observation.cs:629-632`). No `turnFailed` on this door (`Driver.cs:3730`) |
| Protocol | `codex-acp` (`Adapters.cs:2053-2115`) | Codex retries a dropped stream and 5xx itself, each attempt a notification with `willRetry: true`, forwarded as `session_info_update` with `_meta.codex.error` (`codex-acp/dist/index.js:30823-30834`) | A usage limit: a JSON-RPC error, `data` `{ message, codexErrorInfo, additionalDetails }` (`index.js:30836-30840`, `:30968-30979`). An unauthorised turn: `-32000` (`:30803-30807`). **Every other terminal error: the readable message as an agent text chunk, and the prompt answered `end_turn`** (`:30842-30844`) | The limit's sentence (`CodexLimits.Words`, `Accounts/Usage/AccountLimits.cs:346-361`). Not `codexErrorInfo`. Not sign-in: `SignInOf("codex-acp")` is null, since `codex` is a holder and not an adapter (`Accounts/Harnesses/HarnessRoster.cs:135-144`) |
| Protocol | `dsh` (`Adapters.cs:1960-2023`), plugins (`Plugins/DeclaredAcpAdapter.cs:21-63`), `acp-stub` | Unknown | A JSON-RPC error, or the stream's end. The protocol stub answers **every** failed turn `-32000` (`tools/rehearsal-kit.mjs:889`), as `Accounts/SignIns/AccountSignIns.cs:19` records | The message string; no limit, sign-in or refusal table for dsh or a plugin (`DeclaredAcpAdapter.cs:46-59`); `acp-stub` reads the stub's (`Adapters.cs:1713-1731`) |

*Read, not measured:* the Codex rows, and a 5xx frame on the pipe door. Measured: shapes 1 and 2 above; TURNFAIL1's `API Error:
Connection dropped (ECONNRESET)` on the protocol door (`docs/decisions/D137.md:593`, the install's session `cbf2fe70`); and the pipe
door's `model_not_found`/404 and `rate_limit`/429 frames (MODELROLE1a, above).

### 1.2 What Daoris reads of an ended turn

- **No member of the interface classifies a failure.** `ISessionAdapter` (`Adapters.cs:1321-1513`) declares the door, its postures,
  its frames and its `Toolchain`. The words a tool fails in are data on `HarnessToolchain` (`Accounts/Harnesses/Harnesses.cs:176-…`):
  `Refused` (a credential's refusal, a substring), `Limits`, `SignIn`, `Windows`. They are read in four places, each its own way.
- **The conclusion** (`Driver.cs:2710-2791`): `Observation.Conclude` makes a `turnFailed` run `failed`, `ended.turn-failed-taken` or
  `-open` (`Observation.cs:125-130`), and a `refusal` stop reason `failed`, `ended.provider-refused` (APIREFUSAL1, `:135-136`).
  Then three readers in order: `AccountRefused` (`Toolchain.Refused`, read from `ended.Failure` and the harness's last lines,
  `Driver.cs:3238-3255`, `Observation.cs:457-462`), `AccountSignedOut` (`SignInRefusals`, `Driver.cs:3271-3275`) and
  `AccountLimited` (`Driver.cs:3157-3199`, which returns at once without a `turnFailed`, `:3161`, and cools only through the sentence,
  `:3172`). *(Round 2, O9.)* The same end is read again, each its own way, by `Observation.Resumed` (`Observation.cs:303`, used at
  `DriverContinuations.cs:672-683`), TURNFAIL1's `WentOn` (`:330`), `FailedTurnWords.Judge` (`:503-508`),
  `IntakeObservation.Conclude` (`DriverIntakes.cs:395`) and `ReadHelper` (`DriverHelpers.cs:992`).
- **Gaps.** The pipe door passes no `turnFailed`, so its failed `result` is read neither for a limit nor a sign-in
  (`Driver.cs:3730`, `:3161`; D206's row 1f). `ClaudeAcpAdapter` declares no `Refused`, and `AccountRefused` has no owner's fallback
  as `LimitsOf` and `SignInOf` have (`HarnessRoster.cs:120-144`), so *API Error: 401* is never checked on the protocol door. The
  native conversation reads no failed result at all (`NativeChat`, `Conversations/Chat/ChatRunner.cs:2971`). Nothing in the driver
  reads `529`, *Overloaded*, a 5xx, `api_error_status` or `codexErrorInfo`: every match in `src/` is a test's negative fixture
  (`AccountLimitsTests.cs:258`, `ObservationTests.cs:147`).
- **A conversation's turn** (`ChatRunner.cs:2889-2922`): any `DriverException` becomes `door.turn-not-taken`, read for a limit
  (`_limited`) and a sign-in (`ReadSignIn`), and the turn ends `error`. Nothing tries again.
- **A word refused mid-turn** is a note and nothing else: *a word the agent would not take costs that word, … never the session's
  work* (`Acp.cs:879-883`). That is shape 2's path: the refusal ended the turn the word had opened, and the run read it as clean.
  The steering lane's refusal is caught apart (`SteeredAsync`, `Acp.cs:1396-1400`), and so is a conversation's (`ChatRunner.cs:3048`).

### 1.3 What Daoris tries again today

- **Strikes** (D80): a `failed` record not limited and not refused, or an interrupted stop, counts one (`Service/ServiceClient.cs:2099-2121`);
  `Strikes = 3` parks the quest `Exhausted` (`Quests/Domain/Planner.cs:1860-1875`, `Platform/DriverConfig.cs:38`). A failed taken
  quest is carried on in its tree before that (`Planner.cs:1373-1387`, `CarriesOn` true for every `failed` record, `:2005-2006`),
  and an open quest's failed record is started afresh (`:1355-1366`). A carry-on walks (`Driver.cs:1990-2001`, `SelectHandedAsync`)
  and is briefed (`:2502-2512`, `ContextBriefs.ComposeAsync`, `SayMoved`), which is shape 1's lost conversation. *(Round 2, O15:
  round 1 cited `:1731-1735`, carried over from D206's tree.)*
- **TURNFAIL1** (D137's note): a resume that carried the person's words and failed leaves them on `said` (`ended.words-wait`), the
  next look goes on with them in the same conversation, and a second failure in a row marks them `GoOnMarks.FailedTwice`
  (`ended.words-wait-you`; `FailedTurnWords.Judge`, `Observation.cs:494-528`; `DriverContinuations.cs:696-716`). This is the only
  retry in the same conversation, and only for a resume carrying words, bounded at two, paced by the look. *(Round 2, O2.)* The
  mark stops the planner's `GoOn` alone (`Planner.cs:1494-1506`): `HeldForPerson` passes only `Elsewhere`, `AnswersAlone` and
  `NotNow` (`:1411-1413`), so a `FailedTwice` record is still carried on, briefed.
- **SESSCTX1c**: one more run of a native resume whose harness exited before naming its conversation (`DriverContinuations.cs:1204-1237`).
- **D206**: a limited line resumes its own conversation at its account's reset (`Loop/LimitResume.cs`).
- Nothing else. The backoffs in the driver are a file watch's, a plugin hook's and a workflow node's (`Platform/ChangeWatch.cs:189-192`,
  `Plugins/Domain/WorkBounds.cs:22`, `Trees/Nodes/NodeContract.cs:201`).

## 2. The decision in one paragraph

Every door hands its failure to one reader at the agent interface as a **door failure**: the fields its wire carries (a JSON-RPC code
and `data`, a `result`'s status and the harness's error field, Codex's error info) and the words, never the agent's own message. **One
reader** (`AgentErrors.Read`) answers it as one of four **kinds**, `transient`, `limit`, `access` or `fatal`, from the adapter's own
table of codes and recorded sentences, its owner's where it has none. A limit is read first, then access, then transient; anything
unread is fatal, as today. **One policy** acts on the kind. A transient error is tried again in the same conversation, on the same
account and choice, inside the run: five tries, with 1, 2, 4, 8 and 16 minutes of waits before them (31 minutes of waits), each try
one prompt of the person's words the agent never took and then Daoris's go-on word, a word already taken never sent again. It is never
a strike, a park, a cool-off or a walk. Past the bound the record ends `failed` under `ended.transient`, which no strike counts, and
**the planner holds its quest**, Open or Taken, until the person's word or *Try again* resumes the same conversation. A limit is
D206's, unchanged, and a limit's code with no sentence read is a limit with D125's unstated reset. An access error marks the account
as today and goes to the person; the line walks to another account where one is ready. A fatal error fails with its reason, a strike,
as today. Every kind of run takes the same retry inside its run, within its own minutes, and differs only past the bound.

## 3. The kinds, and what decides each

### 3.1 The four kinds

| Kind | What it is | Decided by (any of) | The policy |
|---|---|---|---|
| `limit` | The account's usage is spent: it comes back at a reset | The adapter's `Limits` table reads a sentence and a reset (`AccountLimits.Read`, unchanged). *(Round 2, O6, C2.)* Also a code or kind the door names as a limit, `rate_limit`, an HTTP 429 or Codex's `usageLimitExceeded`, with no sentence the table reads: a limit with no stated reset, cooled for D125's unstated default (`AccountLimits.DefaultCoolOff`, 60 minutes, `AccountLimits.cs:79`), the same `LimitSeen` `Read` makes for a marker with no time (`:119`). Never transient | D206 (§5.2) |
| `access` | The account or its credential cannot be used until a person acts: signed out, refused, not permitted, not verified, no credit | *(Round 2, C1, O7.)* ACP's `-32000` **only where the adapter's own `ErrorWords` declares that it means authentication required**, recorded from that adapter's source: `claude-code-acp` (*Please run /login*, `acp-agent.js:4170-4171`) and `codex-acp` (`index.js:30803-30807`). Nowhere else: the protocol stub answers every failed turn `-32000` (`rehearsal-kit.mjs:889`) though it declares a `SignIn` (`Adapters.cs:1731`), and dsh and a plugin declare nothing; there `-32000` is fatal, as today's strike. The SDK's `authentication_failed`, `oauth_org_not_allowed`, `account_on_hold`, `verification_required`, `cloud_credential_error`, `billing_error`; an HTTP 401 or 403 the door names; Codex's `unauthorized`; the adapter's `SignIn` and `Refused` words, as today | To the person (§5.3) |
| `transient` | The provider or the way to it failed for a moment: nothing about the account, the request or the work | The SDK's `overloaded` or `server_error`; an HTTP status the door names from 500 to 599 (529 among them); Codex's `serverOverloaded`, `internalServerError`, `httpConnectionFailed`, `responseStreamConnectionFailed`, `responseStreamDisconnected`, `responseTooManyFailedAttempts`; the adapter's recorded transient sentences (§4.4). *(Round 2, O6, C2.)* Never a 429 or `rate_limit` by its code: a temporary rate limit is transient only by a recorded sentence of its own, held both ways | Tried again, bounded (§5.1) |
| `fatal` | The request itself cannot succeed by repeating it, or Daoris cannot tell | The SDK's `invalid_request`, `model_not_found`, `max_output_tokens`; Codex's `contextWindowExceeded`, `badRequest`, `cyberPolicy`, `misalignmentPolicyViolation`, `sandboxError`; a `refusal` stop reason (APIREFUSAL1); `-32000` on a door that declares no meaning for it; **anything no table reads**. *(Round 2, O13.)* On the pipe door, a `model_not_found` on a silent turn is D205's refused choice, read before this reader: no strike, the walk asked again (`docs/decisions/D205.md:104-107`) | Fails with its reason (§5.4) |

**Not errors of the agent**, and outside this policy: the person's stop or pause; Daoris's own stop (a timeout, a quiet end, an
update's close, a hand-over); the harness's process ending before it answered (`StreamEnded`, `Acp.cs:1879-1881`), which is the
harness, not the provider, and stays `failed` with its reason and SESSCTX1c's one native retry; a model refused at the open, D205's
`choice-not-offered` (`Acp.cs:796-803`); a tool call the posture refused, which is the turn going on, not ending.

### 3.2 The order

One failure can carry more than one reading: Claude's weekly limit came as `-32603` with `errorKind: "rate_limit"` and the limit's
sentence in the message (`docs/2026-10-23-model-effort-doors-evidence.md:116`). So the reader decides in a fixed order and stops at
the first that holds:

1. **`limit`** where the `Limits` table reads a limit in the words, whatever the code says; then where the code or kind is a limit's
   (§3.1), with the unstated reset. D206's reading stands first, so nothing this design adds can turn a limit into a retry that
   spends five tries on a cooling account. *(Round 2, O6.)* The SDK declares twelve usage-limit prefixes
   (`limit-signals-evidence.md:157`) against `ClaudeLimits`' one marker (`AccountLimits.cs:299`); a limit worded in one the table has
   not recorded is still a limit by its code, cooled an hour, so the walk stops choosing its account.
2. **`access`**, since retrying a refused credential spends an attempt on a refusal that cannot lift.
3. **`transient`** by code, then by the recorded sentences.
4. **`fatal`** otherwise.

A code outranks a sentence within a kind, and the reader never reads a kind from a code the door does not carry: an absent
`errorKind` is no reading, never `unknown`. Where a misreading is possible, it errs toward today: a transient error misread as fatal
is today's strike, and no code alone makes a transient.

### 3.3 What may decide a kind: the door's own failure, never the agent's message

D125 point 1.4 holds: a limit is read from the door's failure, never from the transcript's words, since an agent can write any
sentence. The same holds for every kind. On shape 1 the error is the prompt's JSON-RPC error. On shape 2 it is the mid-turn word's
own prompt's JSON-RPC error, also the door's own. The agent message that streams the same text before either (events 464 and 2300)
is never read: a model quoting a log line that says *529 Overloaded* must not stop its own turn.

*(Round 2, O14.)* On the pipe door the `error` is a field of the harness's own synthetic `assistant` frame (`model: "<synthetic>"`,
`is_api_error_message: true`), which the harness sets and no model writes. Its **field** is read, never the frame's text; the words
read are the `result`'s, as today.

*(Round 2, C11.)* The harness's own lines beside a failed result or a nonzero exit stay a source, as today: `AccountRefused` reads
`Toolchain.Refused` in them only behind that failure gate (`Observation.cs:457-462`, `Driver.cs:3238-3255`). They are the harness's
stderr, not the agent's message, and they are read for access alone, never for transient.

This rule leaves one door with nothing to read. `codex-acp` says a terminal non-limit error only as agent text, under `end_turn`
(§1.1). Such a turn stays read as it is today until row 1f measures the typed marker that marks it (§4.3).

An entry in any table grows only with a recorded sentence or frame held both ways, as D125's limit table does (a sentence that must
read as its kind and a near one that must not). The codes above are what the adapters' source and the SDK's declaration name; each
row records its door's frames before its table reads them (§11). *(Round 2, C14.)* A fixture a stand-in replays from an adapter's
source is marked *source-derived* in its test, apart from a frame captured on the real door, and neither stands for the other.

## 4. Where in the interface each adapter answers

### 4.1 The door failure and the reader

- **`DoorFailure`** (new, `Agents/Errors/`): what a door's wire carried when a turn ended on a failure, and nothing the agent wrote.
  It has `Door` (`pipe` or `protocol`), `Code` (the JSON-RPC code), `Kind` (the wire's own kind word: `data.errorKind`, the harness's
  `error` field, Codex's `codexErrorInfo` key), `Status` (an HTTP status the wire names: `api_error_status`, Codex's `httpStatusCode`),
  `Words` (the error's own sentence: `AcpRefusal.Words`, the `result`'s text) and `Where`. *(Round 2, C11, C12, O3, O8.)* `Where` is
  `open` (a `session/new`, `session/resume` or `session/load` the agent refused, §5.6), `prompt` (a prompt the agent answered with an
  error: the run's first, a held word's, a mid-turn word's once taken, or a try's), `result` (the pipe door's failed `result`) or
  `harness` (the harness's own lines beside a failed result or exit, §3.3, read for access alone). Each field is null where its wire
  carried none.
- **`AgentError`** (new): the reader's answer: `Kind` (§3.1), the `DoorFailure` it read, and `By`, the table entry that decided it
  (`code:-32000`, `kind:overloaded`, `kind:rate_limit`, `status:529`, `words:transient-1`), which the machine log names.
- **`AgentErrors.Read(AdapterSet adapters, ISessionAdapter adapter, DoorFailure failure)`** (new): the one reader. *(Round 2, O1.)*
  It takes the adapter set, since the owner's tables resolve through it (`HarnessRoster.cs:120-144`). It applies §3.2's order over the
  adapter's tables: `Limits` (through `AccountLimits.Read`, unchanged, then the limit codes), then access (`SignIn`, `Refused` and the
  access codes the adapter declares), then transient. Like `LimitsOf` and `SignInOf`, it falls back to the adapter's owner's tables, so
  `claude-code-acp` reads `claude-code`'s. `AccountRefused`'s missing fallback (§1.2) is closed by going through it.
- **`ErrorWords`** (new, on `HarnessToolchain`, beside `Limits` and `SignIn`): the adapter's own map from its codes, kind words and
  statuses to a kind, with its recorded transient sentences and, where its source shows it, its declaration that `-32000` means
  authentication required. Its shape is `LimitWords`' (`AccountLimits.cs:296-361`): anchored, case-blind patterns under a timeout,
  each recorded entry with its source. A plugin's harness declares none, and reads nothing by code: its failures stay fatal, as today.

`turnFailed` stays the string every reader already takes, so nothing that reads it changes in row 1a. The conclusion gains the
`AgentError` beside it, and `AccountRefused`, `AccountSignedOut` and `AccountLimited` each become a reading of its kind. *(Round 2,
O9.)* Each reader §1.2 names learns the kind on its own, in the row that reaches it: `Observation.Conclude`, `Observation.Resumed` and
`WentOn` (`ended.transient`), `FailedTurnWords.Judge` (a `WordsAfter` for the kind: the person's words a try carried are taken, §5.1),
`IntakeObservation.Conclude` (row 1d) and `ReadHelper` (row 1d).

### 4.2 Each door's answer

| Door | Where its `DoorFailure` is made | What it fills |
|---|---|---|
| Protocol, every adapter | *(Round 2, O1.)* Inside `AcpSession.RunAsync`, at each of its prompt sites (`Acp.cs:823-825`, `:871`, `:877`, `:1048`), where a `session/prompt`'s `error` becomes `AcpRefusal.Of` (`Acp.cs:224-239`, `:2019-2024`). The run reads it there, through the reader the capture hands it (§5.1), and keeps the last one for the conclusion's capture (`Driver.cs:4312-4326`) and the conversation's (`ChatRunner.cs:2889-2922`), where they keep `error.Message` today | `Code`, `Kind` (`data.errorKind`, or the key of `data.codexErrorInfo`, a string or a one-key object), `Status` (`codexErrorInfo`'s `httpStatusCode`), `Words` (`AcpRefusal.Words`), `Where: prompt`. `-32000` reads as §3.1 says, by the adapter's declaration |
| Protocol, shape 2 | *(Round 2, O3, C3.)* The mid-turn word's own prompt, awaited at `Acp.cs:877`. Its refusal is the turn's prompt failing **where the word was taken** (its `Taken` mark, set when the prompt before it answered, `Acp.cs:1667-1671`): the turn that failed is the one the word opened. A word refused before it was taken (the turn before it still running, its `Taken` false) is that word's refusal, today's note, and the running turn goes on. The steering lane (`SteeredAsync`, `Acp.cs:1396-1400`) answers `injected` at once and never carries the provider's error, so its refusal stays today's note | The same fields, `Where: prompt`. The word was taken: it is never sent again (§5.1) |
| Protocol, the open | *(Round 2, O8, C12.)* `OpenAsync` and `ResumeAsync` (`Acp.cs:793-794`, `:2817-2824`), read before `ContinueWhy.Refused`, after D205's `AcpChoiceNotOffered` (`:796-803`) and the `Gone` and `Elsewhere` codes | `Where: open` (§5.6) |
| Pipe, `claude-code` | `ClaudeStreamJson.Result` (`StructuredOutput.cs:292-349`), which reads `is_error` and the text today. It also reads `api_error_status`, and keeps the `error` field of the harness's synthetic `assistant` frame before it | `Status`, `Kind` (the harness's `error`), `Words` (`result`), `Where: result`. It goes to the conclusion as the pipe door's `turnFailed`, which closes D206 row 1f's gap for limits and sign-ins in the same reader |
| Every door, the harness's lines | *(Round 2, C11.)* The harness's last lines, behind today's gate: a failed result or a nonzero exit (`Observation.cs:457-462`) | `Words`, `Where: harness`, read for `Refused` alone |
| Protocol, `codex-acp` | Its limit, as today, and `-32000` by its declaration. Its terminal non-limit error after row 1f measures how it is marked (§4.3) | As the protocol row |

### 4.3 The Codex door's terminal error

`codex-acp` 2.1.1 marks a terminal non-limit error only for a client that declared AIR's typed session failures. It then answers the
prompt `end_turn` with `_meta.jetbrains.air.sessionFailure` (`index.js:39993-40004`), whose `category` is `service`, `connection`,
`limit`, `access` or `request` (`:30852-30870`). Otherwise it says the error as agent text.

*(Round 2, C10.)* The one marking is the typed one: declare the capability for `codex-acp` alone and read `category` (`service` and
`connection` → `transient`, `access` → `access`, `limit` → the `Limits` table first, then a limit by its category with the unstated
reset, `request` → `fatal`). Row 1f measures it on the real door before any table reads it. Round 1's second way, reading the turn's
`willRetry` notices, is withdrawn: a `willRetry` notice says an attempt failed and another follows, so a turn that recovered and then
ended empty would read as failed, and a terminal failure with no notice before it would read as nothing. Until row 1f, a Codex turn
ended this way reads as it does today, and §12 says so.

### 4.4 The tables, first entries

Recorded on the install, held both ways in row 1a:

- `claude-code`'s transient sentences, read in the error's own `Words`:
  - `API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment. …` (sessions `61306f9f`
    and `5bafda3b`, 2026-10-24);
  - `API Error: Connection dropped (ECONNRESET)` (session `cbf2fe70`, 2026-10-14; D137's TURNFAIL1 note).
- Its codes: `errorKind` `overloaded` and `server_error` → transient; `rate_limit` → limit (§3.1). Shape 1 carried `overloaded` by
  the adapter's source; row 1a's stand-in replays that frame, marked source-derived (§3.3).
- `claude-code-acp`'s declaration that `-32000` means authentication required (`acp-agent.js:4170-4171`).
- A near sentence that must read as nothing: `API Error: 401 …` (access, by `Refused`), `You've hit your weekly limit · resets …`
  (limit), and `API Error: 400 …` (fatal).

The rest of §3.1's codes enter each table as its row records them.

## 5. The one policy

### 5.1 Transient: tried again in the same conversation

- **Where.** In the run, before its conclusion: the record stays `running`, its process, account, choice and slot under the cap
  are its own, and no record, part or brief is made. That is what keeps the conversation (shape 1 lost 219,284 tokens of it) and
  what keeps a retry from being a strike or a park: neither is reached while the run goes on.
- **The seat on the protocol door** *(round 2, O1)*. The episode runs inside `AcpSession.RunAsync`, around its four prompt sites
  (`Acp.cs:823-825`, `:871`, `:877`, `:1048`): only there is a live session left to prompt. The capture's catch (`Driver.cs:4312-4326`)
  is too late: by then `RunAsync`'s `finally` has ended the reader (`Acp.cs:889-903`, `Release`, `:1800-1804`) and the capture's own
  `finally` closes stdin (`Driver.cs:4335-4338`). `AcpSession` has no adapter, table or clock, so the capture hands it a
  `TransientRetry`: the reader as a delegate (`AcpRefusal → AgentError`, closed over the adapter set and the adapter), the clock it
  waits on (`.claude/knowledge/held-clocks.md`), `TransientRetry.Delays`, and the run owner's check (§5.5). A conversation's seat
  (`ChatRunner.cs:2889-2922`) is right as round 1 wrote it, since its process lives across turns (row 1b). The pipe door's is row
  1c's (§11).
- **The backoff and the bound** *(round 2, C6)*. The bound is **five tries**. Before each the run waits 1, 2, 4, 8 and 16 minutes on
  its clock: 31 minutes of waits between tries, `TransientRetry.Delays`, one constant set for every door and every kind of run. A
  try's own time is the harness's and is not counted: a try is a running turn, watched as any turn is (below), so one that never
  answers is the watch's to raise, as a hung turn is today. A word the person says during a wait goes at once as the next try and
  counts as one; the wait after it is the schedule's next. Why so slow at first: the harness already retried before it gave up
  (Claude Code's `api_retry`, Codex's `willRetry`; on shape 1 the error came three and a half minutes after the last tool result). A
  retry within seconds of that only meets the same outage. Why the bound: an outage past half an hour of waits, after the harness's
  own retries, is one the person should know of. Waiting spends no tokens, but it holds a slot under the cap. A try that succeeds
  after the 8- or 16-minute wait likely re-reads the whole context past the provider's prompt cache (219,284 tokens in shape 1):
  that is the price of keeping the conversation (§12).
- **The wire settles first** *(round 2, C4, O3)*. After a transient failure the run waits until no prompt is open on the wire
  (`_prompts` empty, as `Answered` reads it, `Acp.cs:1660-1674`), so every word it sent has had its prompt answered before it is
  judged. Each of the person's words is then in one of two dispositions, and there is no third:
  - **taken**: a word whose prompt the adapter handed the turn to (its `Taken` mark, `Acp.cs:1667-1671`), shape 2's word among them,
    and the words of a resume's or a start's blocks once that prompt was on the wire (`resume.Prompted`, `DriverContinuations.cs:696-700`).
    They are in the agent's conversation and **are never sent again**, on a try or past the bound;
  - **never taken**: a word on the wire whose prompt answered with an error before the turn was handed to it (`Taken` false: the
    agent never read it, USERSHOWN1, `Acp.cs:1664-1666`), and every word the inbox held. They go in the next try, once; once that
    try is on the wire they are taken by the rule above.

  What the agent did before the failure (its files, its commits, its tools' effects) stays in its tree and its conversation, and the
  go-on word says to go on from there: nothing is replayed.
- **Words said during the wait are held** *(round 2, O1)*. While the episode waits, the inbox holds every word: its mid-turn route
  (`Acp.cs:809-812`) and the steer send nothing, so no word goes alone, and none is cut by a go-on sent after it as a second prompt
  (the adapter would end the person's turn at the go-on's echo). The person's word ends the wait at once (above).
- **Each try is one prompt** of blocks *(round 2, O1)*: first **the person's words never taken**, verbatim and in the order said;
  then **Daoris's go-on word** (`retry.go-on`): *The last request failed on the provider's side ({{words}}). Go on where you
  stopped.* The go-on is kept through the say door under `DaorisKeys.TransientRetry(session, episode)`, as D206's `limit.reset` is
  kept under `DaorisKeys.LimitReset`, once per episode, before the first try. *(Round 2, O10.)* Its `user` event is the first try's
  alone; a later try carries the same text as a block that is neither kept nor shown again.
- **Through the wake gate** *(round 2, O4)*. A session whose turn failed while its background work runs is woken by its harness when
  that work ends (BGWAIT2/3, `Acp.cs:924-949`), and a prompt sent into that woken turn is never answered (BGWAIT1's 48-minute hang,
  `:936-939`). So each try goes through `_wake` as `AfterEndedAsync` does (`:1037-1055`): after the wait, a woken turn that answers
  ends the episode as a try that answered, and Daoris prompts only where none woke within `AcpWake.Quiet`. A stream that ended is the
  harness's end (§3.1), not a try.
- **The owner's check** *(round 2, C8)*. Before each try the run asks its owner whether another turn is still wanted (§5.5). Where
  not, no try is sent: the episode ends and the run closes as at a turn's end, and the conclusion reads its outcome as it does today.
- **Said once.** The conversation gains one line at the episode's first failure, `agent.transient-retrying`: *{{agent}}'s provider
  failed ({{words}}). Daoris tries again in this conversation: up to 5 times, after 31 minutes of waits between them, the next at
  {{time}}.* No line is said per try, and none for a try that works, since the turn going on says it. *(Round 2, O10, C13.)* Each
  try and each wait is a quiet event, `agent.transient` (`try`, `next`), never a line: the session's head reads it and shows
  *Trying again at {{time}}* while a wait runs and nothing while a try runs. The streamed `API Error` agent message each failed try
  repeats is dropped by the episode's events, as `toTurns` drops it beside `door.failed` today (D137's TURNFAIL1 note). The machine
  log says each try (`agent.transient`: session, adapter, account, `By`, try, next).
- **What the waiting run is** *(round 2, O1, O7, C6)*. The run waits on Daoris's own timer, never on its agent. The door exposes
  `WaitsOnTransient` beside `WaitsOnOwnWork` (`Sessions/Watch/SessionWatch.cs:13-16`), wired where that one is (`Driver.cs:4223`), and
  the watch reads it as it reads a call held for the person's permission (`SessionWatch.cs:333-338`): a wait that is not the agent's,
  opening no episode and ending nothing, `--quiet-end` included (`:340-342`), since the wait is bounded by the episode itself. A try
  is a running turn, read by the watch as any. Round 1 cited `background.waits` (`Platform/NoteCodes.cs:912`): the watch reads no note.
- **What ends an episode early.** A try that answers ends it; the count starts again at the next failure. The person's stop or pause
  ends it as theirs, with no failure said. *(Round 2, C6.)* Daoris's shutdown or an update's close cancels the wait with the run,
  which ends as that stop does today (D104's interrupted stop, D206 §3.9's update word); the episode's count is not kept across the
  restart, so a line carried on afterwards starts a fresh count. A try that fails with another kind leaves the episode for that
  kind's policy, once: a limit concludes as a limit, its account cooled once by `AccountLimited`, and goes to D206's planner (a driven
  line) or LIMITGOON1/2 (a conversation) (§6.1); access for §5.3's; fatal for §5.4's.
- **Past the bound** *(round 2, O2, C5)*. The run ends. Its record is `failed` with `ended.transient`: *Its provider kept failing
  after 5 tries, with 31 minutes of waits between them ({{words}}). It waits for you: your words, or Try again, go on in the same
  conversation.*
  - **No word waits marked.** Every word of the person's a try carried is taken and comes off (above), so `FailedTurnWords.Judge`
    answers `WordsAfter.Taken` for this kind. Daoris's go-on word comes off by id, as `limit.reset` does at every conclusion
    (`DriverContinuations.cs:702-707`), so it never reaches a new part. Round 1's `GoOnMarks.Transient` is withdrawn: a mark stops
    the planner's `GoOn` alone (`Planner.cs:1494-1506`), never a carry-on (`:1373-1387`, `:2005-2006`) or an open quest's fresh start
    (`:1355-1366`), and `GoOnMarks.Judged` needs a word waiting (`Conversations/GoOn/GoOnMarks.cs:131-134`), which shape 1 never has.
    Words the person says after the last try failed wait on the record as today (`inbox.Close()`, `Driver.cs:4325`): they are the
    person's word, and release the hold below.
  - **The planner holds the quest.** A quest, Open or Taken, whose last record here ended `failed` with `ended.transient` is held:
    `TransientHolding(quest)` reads the note's code, as `StoppedByReopen` reads its own (`Planner.cs:2020-2022`), never a mark, so no
    write beside the conclusion can fail it open. It is judged after `WordsOn` (`:1322-1329`), which comes first, and before every
    other path: an Open quest's `LimitedFrom`, `RetriedFrom` (with commits or without) and fresh `Consider` (`:1350-1366`), and a Taken
    quest's `CarriedOnFrom` (`:1373-1387`). It says `held.transient` and takes no slot.
  - **Released by the person**, and by nothing else:
    - **their word**: `WordsOn`'s `GoOn` resumes the record's own conversation with their words, as any word to an ended record does
      (MSG1b), and Daoris's go-on is kept after their words, as D206 orders the person's first and Daoris's last;
    - ***Try again***: the press writes the same release as the person's stop's *Try again* (`config.Releases(quest, session)`,
      `DriverConfig.Released`, read by `StopHolding`, `Planner.cs:1459-1465`), and the hold passes a released record. The next look
      plans the line's carry-on, and its start takes **D206's step-1 resume**: `LimitResume.Line` (`LimitResume.cs:26-32`) also
      answers a released `ended.transient` record, Daoris's go-on is kept just before `ContinueAsync`, and a refused resume takes the
      word off and goes briefed, as D206's does.
  - **The release is the person's**, so a resume that meets the account, the conversation or the choice gone is the person's
    authorized carry-on, by D206's step-1 fallback, and D205's walk inside it is the start's, not an escape from the bound. A release
    that fails again past its own bound ends `ended.transient` and is held again: an outage never cycles without the person.
- **Never.** No strike: `ReadStrikes` passes `ended.transient` by its code, as it passes `update.closed` (`ServiceClient.cs:2111`).
  No cool-off and no `limit` flag: the provider's outage is not the account's. No walk to another account or choice (§6.2). No park.
  No carry-on and no fresh start until the person releases it.

### 5.2 Limit: D206, unchanged

A `limit` is read by the same table and sentence as today (`AccountLimits.Read`). It cools its account, ends `failed` with the
ledger's `limit` flag and no strike, and is resumed by D206's planner (own account's conversation first, else briefed, else it waits).
What this design changes for a limit is where it is read: in `AgentErrors.Read`, first, from every door's `DoorFailure`. For the pipe
door that is new (D206's row 1f, which this design's row 1c builds). For shape 2's mid-turn word it is new as well: a limit that
refused a taken word now ends the turn as a limit instead of leaving it clean. *(Round 2, O6, C2.)* A limit decided by its code or
kind with no sentence read (§3.1) cools its account for `DefaultCoolOff` with no stated reset (`Stated: false`), the `LimitSeen`
`AccountLimited` writes (`Driver.cs:3172`), so Codex's `usageLimitExceeded` and an unrecorded Claude wording both reach D206.

### 5.3 Access: to the person

- **The account** is marked as today: refused (`AccountRefused`, ROSTER1b) for a credential the provider refused or an account it
  holds, signed out (`AccountSignedOut`) for a sign-in that ended. No strike, as today (`ServiceClient.cs:2093-2097`). What changes
  is coverage: every door's access codes reach it, not only the sentences two adapters declare, and `-32000` reaches it on every
  protocol door **that declares its meaning** (§3.1). The harness's own lines stay a source (§3.3).
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

### 5.5 The owner's check before each try *(round 2, C8)*

A transient error says why a request ended. It does not say whether the work still needs another turn. Today a submitted quest, a
published intake and a helper's stored answer outrank a messy turn ending (`Observation.cs:100-118`, `Quests/Domain/Intake.cs:305-308`,
`DriverHelpers.cs:652-656`), so a try must not reopen what is already done. Each kind of run hands its own check with the reader, and
it is asked before each try:

| Kind of run | No try where |
|---|---|
| A driven session | its quest is done, submitted, awaiting another repository, stood down, closed or deleted (read as DONEEND1's `QuestMovedAsync` reads it, `Acp.cs:1069-1085`) |
| A conversation | never: a person's turn has no outcome of its own to outrank it |
| A helper | its answer is stored (`files.ReadAnswer`, `DriverHelpers.cs:652`) |
| A second opinion | its opinion is recorded |
| An intake | its ask has gained quests by it (`Intake.cs:305-308`) or was deleted |
| A partner | its piece is ending or ended |

The person's stop, a delete and completion keep their precedence: a check that answers *no* ends the episode with no failure said,
and the conclusion reads the run as it does today.

### 5.6 A transient failure at the open *(round 2, O8, C12)*

An open the agent refused has delivered nothing: there may be no session id, and the target or the resume's words were never sent.
So the open has its own phase. A `DoorFailure` with `Where: open` is read before `ContinueWhy.Refused` (`Acp.cs:2817-2824`), after
D205's `AcpChoiceNotOffered` (`:796-803`, unchanged) and the `Gone` and `Elsewhere` codes. Where it reads transient, the run waits on
the same schedule and repeats **the failed opening operation itself** (`session/new`, `session/resume` or `session/load`), then sends
the run's own first prompt once, as it would have: no go-on word, since nothing was asked. Anything else goes as today: a refused
resume to ANSWER1a's and D206's briefed fallback. Past the bound the start ends `failed`, `ended.transient`, and the line is held as
§5.1 says: never briefed onto a provider that is down.

## 6. With D206's limit resume and D205's walk

### 6.1 D206

- **The limit is read first** (§3.2), so a limit is never retried as transient, and a 429 or `rate_limit`, with a reset sentence or
  without one, cools its account.
- **A transient failure never cools an account**, writes no `cooling.json` entry and sets no `limit` flag. So it never reaches D206's
  planner, its computed wait, its `limit.not-lifted` or its `LimitWait` words.
- **Inside D206's resume** *(round 2, C4, O3)*. A transient failure on a limited line's own resume (D206 point 3.i,
  `ResumeAsync(…, limited: true)`) is tried again in that run as any is. `limit.reset` and the person's words were the resume's
  prompt, on the wire: they are taken and are never sent again; each try sends the episode's go-on alone, after any word said since.
  Past the bound the record ends `ended.transient`, and D206's word comes off as every conclusion takes it off. A limit read on a
  try ends the episode as a limit, cooled once: D206 point 3 from there. `limit.not-lifted` reads only a limit, so a transient
  failure within the margin after a reset is not *not lifted*.
- **Conversations.** A conversation waiting on a limit (`ChatTurns.WaitForLimit`) is not running a turn, so nothing here applies to
  it until its wake. Its wake's turn is a turn like any other. A limit read on a conversation's try goes to LIMITGOON1/2 once.
- **Past the bound**, the hold (§5.1) is released only by the person, and the release's resume is D206's step 1 (§5.1).

### 6.2 D205

- **A transient failure never walks.** D205 point 5 holds: *a running session never moves to another choice*, and *a refusal after a
  prompt was sent is never a fallback*. A provider's outage often holds every account of its maker, and the point of the retry is the
  same conversation. Moving the work across accounts would brief it into a new part (shape 1's loss); moving it across makers is the
  person's choice (D205 point 5, D206 point 6).
- **Past the bound**, *Go on now with…* names the actor's next choice, as D206's limit line does (`work/waits/LimitWaitLine.tsx`), and
  it is the person's press.
- **Access** passes the account in the next start's walk, which is D197's walk unchanged.
- **At the open.** A model refused before any prompt stays D205's `choice-not-offered`. A transient failure at the open is §5.6's.
- *(Round 2, O13.)* **The pipe door's model refusal on a silent turn** stays D205's one named exception (`D205.md:104-107`), read
  before this reader: a refused choice, no strike, the walk asked again.

### 6.3 TURNFAIL1 and APIREFUSAL1

- TURNFAIL1's look-paced retry of a resume's words (§1.3) is kept for a **fatal** failure only. A transient failure never takes
  TURNFAIL1's path: its words a try carried are taken (§5.1), and past its bound the planner holds the line (§5.1).
- APIREFUSAL1's `refusal` stop reason is a `fatal` kind's reading (§3.1), unchanged in its note and its strike.

## 7. What each kind of run does

Every kind of run takes §5.1's retry inside its run, since every kind's turn goes through a door's capture: driven sessions
(`Driver.cs:4126`), conversations (`ChatRunner.cs:2885`), and helpers, opinions, intakes and partners through `HoldAsync`'s capture
(`DriverHelpers.cs:907-1037`, `DriverOpinions.cs:34-259`, `DriverIntakes.cs:367-419`, `DriverPartners.cs:620`). Each asks its owner's
check before each try (§5.5). What differs is the end past the bound.

*(Round 2, O5, C9.)* **A pass with minutes of its own** (helpers 15, `Conversations/Domain/HelperInstruction.cs:34`; opinions 20,
`Review/Domain/OpinionRules.cs:101`; partners 45, `Conversations/Domain/PartnerInstruction.cs:28`; a brainstorm's turn) is killed at
them and concluded `Observation.TimedOut(bound)` today (`Driver.cs:3919-3924`; `DriverHelpers.cs:978`, `:993`; `DriverOpinions.cs:202`,
`:217`; `DriverPartners.cs:603`, `:617`). Its minutes are not extended: the waits are spent from them, and **no delay is waited past
the pass's deadline**, as round 1 said of a brainstorm alone. A pass whose minutes end inside an episode (in a wait, or in a try after
a transient failure) ends with the transient reason, not *timed out*. So a helper's or an opinion's *past the bound* is usually its
minutes: a helper has room for its first three tries.

| Kind of run | During the retry | Past the bound, or its minutes inside an episode |
|---|---|---|
| **A driven session** | Its record stays `running`; the conversation says `agent.transient-retrying` once; the head says *Trying again at {{time}}* during a wait | `failed`, `ended.transient`, no strike, the person's words a try carried taken, Daoris's go-on off, the quest held for the person (§5.1); *Ended* raised once (`AttentionWatch`, `Quests/Domain/Attention.cs:248-254`, worded by its code); *Try again* and *Go on now with…* on its header |
| **Ask Daoris and a repository's chat** | The turn does not end `error`: the runner waits and tries again on the same process. Words said meanwhile are kept on `said` and shown queued; they go as the next try at once (§5.1). *(Round 2, C13.)* The line is `agent.transient-retrying`, one code for both | The turn ends `error` with `chat.transient-stopped`: *{{agent}}'s provider kept failing ({{words}}): your next words go on in this conversation.* The conversation stays; the person's next word is a turn as today. No record state changes, as a chat's failed turn changes none today |
| **A helper** | Its pass waits and tries again inside its run, within its minutes | Settled `refused` with a reason of its own, `HelperWhy.Transient`, said `helper.transient` on the asker (*Helper {{agent}} could not answer: its provider kept failing ({{words}})*), not `no answer` and not *timed out*. A lead waiting on it goes back to the person as a refused helper's lead does (`BackToPersonAsync`, `DriverHelpers.cs:363-386`), now with the reason |
| **A second opinion** | Its pass waits and tries again inside its run, within its minutes | The pass is `failed` with why `transient` (`Opinions` gains it beside `limited`); the gate is `Unavailable` with code `transient` and no `Until`, so the look does not re-ask it (`OpinionGateLook.cs:220-221` re-asks only a cooling gate) and the person's *Try again* does |
| **An intake** | Its run waits and tries again, unless its ask has gained quests (§5.5) | `failed`, `intake.transient`. The ask is unbound once (`unbindAsk`, as a limited intake's is, `DriverIntakes.cs:405-409`), so the next look picks it again with a fresh intake. An ask whose last intake already ended `intake.transient` stays bound, and its *Ended* is raised, so an outage costs at most two bounds before the person hears. *(Round 2, C8.)* The note no longer says *before it published anything*: a transient error does not establish that, and an ask that gained quests concluded on its quests (§5.5) |
| **A partner** | Its run waits and tries again, within its minutes | The piece goes to `Ending` and the lead is told `partner.transient`, the work kept, as `partner.limited` is (`DriverPartners.cs:620-623`, `PartnerNotes.cs:34`) |
| **A brainstorm voice** | Tries only while its pass's minutes allow | The voice is dropped `Unavailable` with the reason, as a voice that cannot run is (D205 keeps voices frozen) |

*(Round 2, C7.)* **The native door's runs differ in their process, and row 1c and 1d settle each apart:**
- a **live native conversation** keeps one process across its turns (`Adapters.cs:2262-2266`, `ChatRunner.cs:3048`), so its try is
  the next stream-json message on that process, never a `--resume` beside a live writer;
- a **driven native run**, whose process exits at its `result`, tries by `--resume <id>` on the same account and record, as
  SESSCTX1c's `AgainAsync` runs it (`DriverContinuations.cs:1204-1237`), every run carrying `--model` and effort
  (`model-effort-doors-evidence.md:180-182`);
- a **one-turn native pass** (an intake, a helper, an opinion) keeps no conversation id (`keepAs` null, `Driver.cs:3605`). Its
  episode reads the id the harness named from the mapper (`KeepingAsync`, `Driver.cs:4064`) for its own tries alone, never kept on
  the record; where none was named (the harness ended before `init`), there is no try, and the run ends as today, never replaying the
  original target.

## 8. The record and the notes

- **The ledger.** No new state, column or flag. A transient end is `failed` with its note code, as D205 kept `choice-not-offered`
  `failed` and D206 kept the `limit` flag, for the same reason: a teammate's mirror and an older build read the state set, and a new
  state is nothing they know (`.claude/knowledge/compatible-builds.md`). An older build reading this machine's ledger counts an
  `ended.transient` record as a strike and carries it on, which is today's behaviour.
- **The strikes** pass `ended.transient` by its code (`ReadStrikes`, as `update.closed`).
- **The hold** reads `ended.transient` by its code (`TransientHolding`, §5.1), and its release is the person's stop's
  (`DriverConfig.Released`).
- **Note codes** (kebab case, as `NoteCodesTests` holds; en and zh in the note catalogue, `src/Daoris.Web/src/work/noteLines.ts`).
  *(Round 2, O12.)* The zh says 重试, the glossary's *try again* (`src/Daoris.Web/src/locales/glossary.json:652`), never 再试, which it
  lists to avoid:

  | Code | Where | English | 中文 |
  |---|---|---|---|
  | `agent.transient-retrying` | the conversation, once per episode, a driven session's and a chat's | *{{agent}}'s provider failed ({{words}}). Daoris tries again in this conversation: up to {{count}} times, after {{minutes}} minutes of waits between them, the next at {{time}}.* | *{{agent}} 的服务方出错（{{words}}）。道衍会在本对话中重试：最多 {{count}} 次，各次之间共等待 {{minutes}} 分钟，下一次在 {{time}}。* |
  | `ended.transient` | the record's note | *Its provider kept failing after {{count}} tries, with {{minutes}} minutes of waits between them ({{words}}). It waits for you: your words, or Try again, go on in the same conversation.* | *它的服务方在 {{count}} 次重试后仍然出错，各次之间共等待 {{minutes}} 分钟（{{words}}）。它在等你：你的话或"重试"会在同一对话中继续。* |
  | `held.transient` | the planner's sentence for a held quest | *Its last session's provider kept failing: it waits for your words or Try again.* | *它上一个会话的服务方一直出错：它在等你的话或"重试"。* |
  | `chat.transient-stopped` | a conversation's line | *{{agent}}'s provider kept failing ({{words}}): your next words go on in this conversation.* | *{{agent}} 的服务方一直出错（{{words}}）：你接下来的话会在本对话中继续。* |
  | `helper.transient` | the asker's record | *Helper {{agent}} could not answer: its provider kept failing ({{words}}).* | *助手 {{agent}} 没能回答：它的服务方一直出错（{{words}}）。* |
  | `intake.transient` | the intake's record | *the agent's provider kept failing while it worked on ask `#{{ask}}` ({{words}}).* | *智能体处理请求 `#{{ask}}` 时，它的服务方一直出错（{{words}}）。* |
  | `partner.transient` | the lead's record | *Partner {{agent}} stopped: its provider kept failing ({{words}}). Its work is kept.* | *搭档 {{agent}} 停下了：它的服务方一直出错（{{words}}）。它的工作已保留。* |
  | `retry.go-on` | Daoris's word to the agent | *The last request failed on the provider's side ({{words}}). Go on where you stopped.* | the agent's word, English only, as `limit.reset` is |

  `{{words}}` is the door's own sentence, said as a program's (`NoteBy.Program`), never translated: authored content stays as written.
  The glossary's *try again* / 重试 widens its `means` to a record past its retries (row 1a2), beside D206's *Try now*, which is an
  account's.
- **The machine log** says `agent.error` once per failure read (session, adapter, account, kind, `By`) and `agent.transient` once per
  try (try, next). Neither is a note. *(Round 2, O10.)* The record's quiet `agent.transient` event (§5.1) is what the head reads.
- **The page.** A failed record with `ended.transient` reads *failed* in its dot, worded by its note, with no red: it waits on the
  person's word, not on a fault in the work. *(Round 2, O10, O11, C13.)* The head's *Trying again at {{time}}* is drawn from the
  latest `agent.transient` event's `next` while it is in the future and no turn event follows it, so a try that works clears it and
  a later wait sets it again. *Try again* is offered on a failed record whose note is `ended.transient` and which is its quest's last
  record here (`work/acts.ts:157` offers `retry` today only to a parked record or a stopped one holding its quest), in en and zh, and
  its act is §5.1's release.

## 9. What it amends, each by a dated note on acceptance

- **D137's TURNFAIL1 note**: a transient failure is retried in its run and never takes the look's retry; the person's words a try
  carried are taken; past its bound the planner holds the line for the person.
- **D125 point 1** and **D206 point 1** (§3.1's pipe door): the limit is read in `AgentErrors.Read`, first, from every door's
  failure, the pipe door's included; the table and the reset are unchanged; a limit's code with no sentence read is a limit with the
  unstated reset.
- **D80** (strikes, carry-on): `ended.transient` is no strike, and its quest is held, not carried on.
- **D126 §3.3** (the person's stop's hold and its *Try again*): the same release also releases a transient hold.
- **D200** (the session watch): a run waiting on a retry is waiting, `WaitsOnTransient`, not quiet.
- **BGWAIT1** (D105's note): a try goes through the wake gate.
- **APIREFUSAL1's FIX-LOG entry** and **ROSTER1b**: their readings become `fatal` and `access` kinds; their behaviour is unchanged.
- **D205 point 5** and **D206 point 6** are not amended: §6.2 keeps them, and D205's pipe-door model refusal stays its exception.

## 10. Considered and rejected

- **A retry by the look**, as TURNFAIL1 retries: the look's next start of a failed record walks and is briefed (`Driver.cs:1990-2001`,
  `:2502-2512`), which loses the conversation, and its pace is the poll's, not a backoff.
- **The retry in the capture's catch** *(round 2, O1)*: no live session is left there (§5.1).
- **A new state, `retrying` or `transient`**: the same reason as D205 and D206 (§8).
- **A word mark to hold the line past the bound** *(round 2, O2, C5)*: a mark stops the `GoOn` alone, and shape 1 has no word to mark
  (§5.1). The hold reads the note's code.
- **Sending a taken word again** *(round 2, O3, C4)*: it repeats the person's words in the conversation, against the owner's
  direction.
- **Cooling the account for a transient error**: the outage is the provider's, and a cool-off would push the next start onto another
  account and brief it, which is shape 1's loss.
- **Walking to another account or choice at once**: D205 point 5, and the brief's loss (§6.2).
- **Reading the agent's streamed message**, which carries the same text first in both shapes: D125 point 1.4 (§3.3).
- **A transient guess for an unread error, or for a 429 by its code** *(round 2, O6)*: five tries on a request that cannot succeed,
  or on an account whose usage is spent, each holding a slot (§3.2, §5.4).
- **`-32000` as access on every protocol door** *(round 2, C1, O7)*: JSON-RPC leaves it to each server, and the protocol stub answers
  every failure with it (§3.1).
- **Reading Codex's `willRetry` notices as a terminal failure** *(round 2, C10)*: §4.3.
- **Retrying within seconds**: the harness has already retried (§5.1).
- **Extending a pass's minutes for its tries** *(round 2, O5, C9)*: its minutes are the person's rule for that pass (§7).
- **No bound**, or a second tier the look resumes by itself an hour later: an outage past half an hour of waits after the harness's
  own retries is the person's to know, and a later row can add a tier once the install shows outages that long.
- **A setting for the delays**: one constant set until a case asks for another. Zero, *off*, would be today's behaviour, which the owner
  directed away from.
- **Declaring AIR's typed session failures for every protocol door now** (`_meta.jetbrains.air.capabilities: ["sessionFailure"]`):
  it is one client vendor's extension. Declared, `claude-agent-acp` drops the error's words from the prompt's error
  (`internalErrorForClient`, `acp-agent.js:2369`) and moves its failures to `session_info_update`, which changes a door that already
  reads well (shape 1). Row 1f weighs it for `codex-acp` alone, where the default mode marks nothing (§4.3).
- **A table per kind of run**: the kinds are the adapter's, the policy the interface's; only the owner's check (§5.5) and what follows
  the bound are the run's (§7).

## 11. Rows, in build order, with their proofs

Every row runs its own tests and the classes covering the code it changes, filtered, and the census checks `docs/development.md`
names (*Choose the checks*). Each stand-in agent is a script under the tests, never the agent's binary (`node tools/live-logins.mjs`).
The backoff's tests hold the clock (`.claude/knowledge/held-clocks.md`). A fixture replayed from an adapter's source is marked
source-derived (§3.3). *(Round 2, O16, C14.)* Round 1's 1a is split: 1a is the smallest safe delivery, the protocol door's prompt
failure for a driven session with its hold; 1a2 carries shape 2, the open and the page.

| Row | What | After | Proof |
|---|---|---|---|
| **AGENTERRORS1a** | The kinds and the reader at the interface (`DoorFailure`, `AgentError`, `AgentErrors.Read` taking the adapter set, `ErrorWords`, §4.4's first entries); the protocol door's prompt failure (`Where: prompt`) for a **driven session**; §5.1's episode inside `AcpSession.RunAsync` (the seat, the settled wire, words held during the wait, one prompt a try, the wake gate, the owner's check for a driven session, `WaitsOnTransient` and the watch); the limit's code with the unstated reset (§3.1); `ended.transient`, `ReadStrikes`, `FailedTurnWords.Judge`'s kind, Daoris's go-on off at every conclusion; the planner's hold (`TransientHolding`, `held.transient`) and its release by the person's word and by the `retry` act; `agent.transient-retrying`, `ended.transient`, `held.transient` and `retry.go-on` in en and zh. Access and fatal keep today's ends | — | The reader's table both ways (`AgentErrorsTests`, a fast class), `-32000` read only where declared: an authentication refusal and an unrelated failure with the same code on the stub. A stand-in ACP agent answering its prompt `-32603` *Internal error: API Error: 529 Overloaded. …* with `data.errorKind: "overloaded"` (source-derived) twice, then working: retried in the **same** conversation (one record, the same session id, no `moved.on`), it goes on and ends as its work does, with one `agent.transient-retrying` line, no strike and no `cooling.json` entry, red first against today's `ended.turn-failed-taken`. A stand-in that records the person's words and makes a visible effect, then fails: the stand-in receives each word **once** (one `user` event, one receipt), and neither the instruction nor the effect is replayed. A word said during a wait goes at once, in one prompt with the go-on after it. A woken turn during a wait ends the episode, and no prompt is sent into it. The quest done before a try: no try. The watch during a wait: no episode, no quiet end. The same agent erring every try: past the bound `failed`, `ended.transient`, no word waiting, the go-on off, no strike, no cool-off, no park; then **the next looks** (a tick each, with and without commits in the tree) start nothing for an Open and for a Taken quest, until the person's word, or the `retry` release, resumes the **same conversation id** by D206's step 1. A limit on a try: cooled once, D206 from there. A weekly limit's sentence under `errorKind: "rate_limit"`: limited, never retried; `rate_limit` with no sentence: limited an hour. Shutdown during a wait ends as today's shutdown. `NoteCodesTests`, `i18n:check`, `names:check --strict` |
| **AGENTERRORS1a2** | Shape 2 (§4.2: a mid-turn word's prompt refused after it was taken is the turn's prompt failing); the open (§5.6); the page: the head's *Trying again at {{time}}* from `agent.transient`, the drop rule for the repeated `API Error` message, the re-sent go-on kept off the record, *Try again* offered on an `ended.transient` record (`acts.ts`) in en and zh, the glossary's *try again* widened | 1a | Shape 2 both ways round: a mid-turn word refused with the 529 after the prompt before it answered (taken: retried, never `awaiting-person`, the word received once), and one refused before it was taken while the running turn then ends well (the word's note, the turn clean). A refused steer (`_session/steering`) stays its note. An unrelated update after the refusal changes nothing. The open: a transient `session/new` and `session/resume` failure repeated, then the original prompt sent once, before and after a session id exists; D205's model refusal unchanged. The head through several waits, a try that works, a stop and the bound, in en and zh (`limitWait`'s and the head's tests, a story) |
| **AGENTERRORS1b** | §5.1 and §7 for **conversations**, Ask Daoris and a repository's chat, on the protocol door: the wait, words said meanwhile, `chat.transient-stopped` | 1a | A stand-in conversation's turn failing transient twice then answering: one turn (failed tries neither release the queue, `ChatTurns.TurnEnded`, nor move the curve, notify or move a limit), the same process, the person's words once; a word said during the wait goes at once and first, one prompt with the go-on; a limit on a try goes to LIMITGOON1/2 once; erring every try ends the turn `error` with `chat.transient-stopped`, and the next word is a turn of the same conversation (`ChatGoesOnProcessTests` or a class of its own); the page's line in en and zh |
| **AGENTERRORS1c** | The **pipe door**: `ClaudeStreamJson.Result` reads `api_error_status` and the harness's `error` field into a `DoorFailure`, the conclusion's `turnFailed` on this door (D206 row 1f's gap: limits and sign-ins read here too), D205's model refusal read first; the **driven native run**'s retry by `--resume` with `--model` and effort; and, after 1b, the **live native conversation**'s retry on its process and `NativeChat`'s reading of a failed result | 1a; 1b for `NativeChat` alone | The measured 404 and 429 frames (`ClaudeStreamJsonTests.cs:191-212`); a 5xx frame recorded on the real door, or replayed from the SDK's declaration marked source-derived. A stand-in native agent erring twice then working is resumed with `--resume` and the same `--model` in the same record and goes on; one that keeps erring ends `ended.transient` and is held; its limit sentence cools its account; `model_not_found` on a silent turn stays D205's (`NativeResumeTests`, a native tick class). A missing id, and an end before `init`: no try, today's end. A live native chat retries on its own process, never spawning `--resume` beside it |
| **AGENTERRORS1d** | §7's **other kinds of run** past the bound and at their minutes: helper (`HelperWhy.Transient`), opinion (`transient` why and gate), intake (one unbind, then bound), partner, brainstorm; each owner's check (§5.5); the one-turn native pass's episode-local id | 1a; 1c wherever it promises the native door | Each kind's stand-in erring every try ends as §7 says, its minutes ending inside an episode with the transient reason, never *timed out*; erring twice then working goes on in its run; a helper's answer stored, an intake's quests published and an opinion recorded just before the provider failed: no try, today's end (`HelperLookTests`, `OpinionLookTests`, `IntakeTests`, the partners' and brainstorms' classes) |
| **AGENTERRORS1e** | **Access** on every door (§5.3): `-32000` where declared, `AccountRefused` through the reader so `claude-code-acp` reads its owner's `Refused`, the SDK's access kinds, Codex's `unauthorized` tied to its holder's accounts, the harness's lines kept as a source | 1a; 1c for the pipe door | A stand-in of each door refusing with its access error: the account marked, no strike, no retry, the next start walks past it, and with none ready the start is held in today's sentence (`SignInRefusalsTests`, `AccountRotationTickTests`). A stderr-only credential refusal still marks the account; an ordinary stderr failure does not; an agent message quoting the credential's sentence does not |
| **AGENTERRORS1f** | **`codex-acp`'s** codes (§3.1, §4.3): record its frames on the real door (a usage limit, a dropped stream with `willRetry`, a terminal overload, the typed `sessionFailure` marker once declared), then read `codexErrorInfo` and `category` | 1a | The recorded frames, held both ways; a stand-in `codex-acp` erring twice then working retried in the same conversation. By the typed marker: a direct terminal failure, retries that recovered, a successful empty ending, a cancel, retries exhausted, and its access and request categories, each read as its kind |
| **AGENTERRORS1g** | The install's proof | 1a-1f | On the installed window, a **controlled** stand-in agent installed as a plugin harness that answers transient twice then works, and one that keeps erring, read as §5.1 says: one line, the same conversation, no strike, the hold and its release; the head's line seen in en and zh. Then the next real provider error the install's machine log shows, as later evidence: its date is open, and the backlog says so |

The first row gives the most: it is the install's door, its measured shape 1 and every driven session.

## 12. What the checks do not cover

- A document. The code was read at `64059110a`, and again on main at `fb3eb89eb`, where no cited file changed; the adapters'
  behaviour is read from their source in the install's toolchain cache, `claude-agent-acp` 0.84.0 and `codex-acp` 2.1.1, and an
  adapter's update can move it. Shapes 1 and 2 are measured, from the install's records; `data.errorKind` on shape 1 is read from the
  adapter's source, since the record keeps only the message.
- *(Round 2, O14.)* The pipe door's `api_error_status` and the harness's `error` field are measured for a model refusal (404) and a
  limit (429) by MODELROLE1a; a 5xx on that door is not.
- Codex's terminal errors are unmeasured, and until row 1f a Codex turn ended on one reads as it does today: with its quest taken it
  parks quoting the error, as shape 2 did.
- A dsh or plugin harness reads nothing by code; its errors stay fatal until it declares a table.
- How long real outages last is not measured. The bound's 31 minutes of waits is a reading of the two records (each overload passed
  within the half hour: the person's go-on at 01:02 worked, and `63d56294` ran at 00:31) and of the harness's own retries before it.
- The prompt cache: a try after the 8- or 16-minute wait likely re-reads the whole context (219,284 tokens in shape 1), unmeasured.

## 13. The reads

Round 1 was read at `6f089fe1f` by Codex (*another round*: 14 findings, 12 blocking) and by the independent Opus reader as a
foundation (*accept after named changes*: 16 findings, 1-3 blocking, 4-6 to take before acceptance), each kept verbatim in
`docs/2026-10-24-agent-errors-reads.md`. §14 answers every finding.

## 14. Round 2: round 1's reads answered

Every finding of both reads, against the code on main at `fb3eb89eb`. *Taken* means the change is made as the reader proposed, or
narrower; *answered* means the design keeps its text and says why; *deferred* names the row that settles it and why it waits. Where
the two reads agree, one row says so and both are taken by it.

| Finding | Taken, answered or deferred | Where |
|---|---|---|
| **O1**, blocking, with Codex §1's *`Unreached` as a retry checkpoint* and **C6**'s watcher: the retry's seat | Taken as named: the episode inside `AcpSession.RunAsync` around its four prompt sites (`Acp.cs:823-825`, `:871`, `:877`, `:1048`), the capture handing it the reader as a delegate (closed over the adapter set), the clock and the delays; the inbox holds every word during a wait (`:809-812`); each try one prompt, the person's words then the go-on; `WaitsOnTransient` beside `WaitsOnOwnWork`; the reader takes the adapter set. Checked: `RunAsync`'s `finally` releases the reader (`:889-903`) before the capture's catch (`Driver.cs:4312-4326`) and its stdin close (`:4335-4338`); `Unreached` is filled in that `finally` (`Acp.cs:1094-1102`) | §4.1, §4.2, §5.1 |
| **O2**, blocking, with **C5**, agreed: past the bound the line is carried on or started afresh | Taken as named: `TransientHolding` holds an Open or Taken quest whose last record here ended `ended.transient`, read by its note code, after `WordsOn` and before every other path, said `held.transient`, no slot; released by the person's word (`GoOn`) or *Try again* (the stop's release, `DriverConfig.Released`), whose start takes D206's step-1 resume (`LimitResume.Line` widened), the go-on kept just before `ContinueAsync`, a refused resume briefed; Daoris's go-on taken off at every conclusion; `GoOnMarks.Transient` withdrawn. Checked: `HeldForPerson` (`Planner.cs:1411-1413`), `CarriesOn` (`:2005-2006`), the Open path (`:1350-1366`), `GoOnMarks.Judged` (`GoOnMarks.cs:131-134`), `StopHolding` (`Planner.cs:1459-1465`), `LimitResume.Line` (`LimitResume.cs:26-32`), D206's word off (`DriverContinuations.cs:702-707`). C5's failed mark write: the hold reads the note, no mark | §5.1, §6.1, §8; row 1a |
| **O3**, blocking, with **C3** and **C4**, agreed: shape 2's word and a resume's prompted words sent twice; shape 2's attribution; word acceptance | Taken: a word taken (Acp.cs's mark, `:1667-1671`, and a resume's or start's blocks once on the wire) is never sent again; a mid-turn word's prompt refused after it was taken is the turn's prompt failing, `Where: prompt`; refused before it was taken, it is the word's own refusal; the steering lane stays its note (it answers `injected` at once, `acp-agent.js:2061-2132`; `SteeredAsync`, `Acp.cs:1396-1400`). C4's dispositions: the wire settles before judging, so a word is taken or never taken and there is no uncertain one; never-taken words go once in the next try; effects are not replayed. Checked: `Answered` (`Acp.cs:1660-1674`), USERSHOWN1's mark (`:1664-1666`) | §0, §4.2, §5.1, §6.1; rows 1a, 1a2 |
| **O4**: a woken turn during the wait | Taken: each try goes through `_wake` as `AfterEndedAsync` does (`Acp.cs:1037-1055`); a woken turn that answers ends the episode; Daoris prompts only where none woke within `AcpWake.Quiet` | §5.1; row 1a |
| **O5**, with **C9**, agreed: passes with their own minutes | Taken: no delay waited past a pass's deadline, its minutes not extended; minutes ending inside an episode end with the transient reason, not *timed out*. Checked: 15, 20 and 45 minutes (`HelperInstruction.cs:34`, `OpinionRules.cs:101`, `PartnerInstruction.cs:28`), killed and `TimedOut` at `DriverHelpers.cs:978`, `:993` | §7; row 1d |
| **O6**, with **C2**, agreed: `rate_limit` or 429 with no sentence read | Taken: a limit, cooled for `DefaultCoolOff` (60 minutes, `AccountLimits.cs:79`) with no stated reset, Codex's `usageLimitExceeded` the same; never transient by its code; a temporary rate limit is transient only by a recorded sentence. Checked: one marker in `ClaudeLimits` (`AccountLimits.cs:299`), twelve prefixes declared (`limit-signals-evidence.md:157`), `AccountLimited` cools by the sentence alone (`Driver.cs:3172`) | §3.1, §3.2, §5.2 |
| **O7**, with **C1**: `-32000` on every protocol door | Taken in C1's form, which is narrower than O7's: `-32000` is access only where the adapter's own `ErrorWords` declares it, from that adapter's source (`claude-code-acp`, `codex-acp`); fatal elsewhere. O7's test (*where the adapter declares a sign-in*) would still read the protocol stub's every failure as access, since it declares `SignIn` (`Adapters.cs:1731`) and answers `-32000` for any failure (`rehearsal-kit.mjs:889`, as `AccountSignIns.cs:19` records) | §3.1, §4.1; rows 1a, 1e |
| **O8**, with **C12**, agreed: a transient error at the open | Taken in C12's form: an open phase, read before `ContinueWhy.Refused` (`Acp.cs:2817-2824`), that repeats the failed opening operation and then sends the original prompt once, no go-on; D205's model refusal untouched (`:796-803`); past the bound held, never briefed | §5.6; row 1a2 |
| **O9**: the conclusion readers named as one | Taken: each named, with its row | §1.2, §4.1 |
| **O10**, with **C13**, agreed: the page cannot say what §5.1 promised | Taken: a quiet per-try `agent.transient` event the head reads, never a line; the drop rule keyed to the episode; the re-sent go-on kept off the record; the sentences say *31 minutes of waits between* tries; one code, `agent.transient-retrying`, for driven sessions and chats (§7 named `chat.transient-retrying`) | §5.1, §7, §8; row 1a2 |
| **O11**: *Try again* for a failed record does not exist | Taken: the act (the stop's release) in row 1a, the press on an `ended.transient` record in row 1a2, in en and zh. Checked: `acts.ts:157`; `ResumesOwn` (`DriverContinuations.cs:40-45`) is not its path | §5.1, §8; rows 1a, 1a2 |
| **O12**: the zh words clash with the glossary | Taken: 重试 throughout, never 再试; the glossary's `means` widened in 1a2. Checked: `glossary.json:652` | §8 |
| **O13**: `model_not_found` against D205's pipe-door exception | Taken: on the pipe door a `model_not_found` on a silent turn is D205's, read before this reader. Checked: `D205.md:104-107` | §3.1, §6.2; row 1c |
| **O14**: the pipe door's frames are measured, and the field is the harness's | Taken: §1.1, §12, D210 and row 1c corrected; the synthetic frame's field read, never its text. Checked: `model-effort-doors-evidence.md:186-204`, `ClaudeStreamJsonTests.cs:191-212` | §1.1, §3.3, §12; row 1c |
| **O15**: wrong citations | Taken: `Driver.cs:1990-2001` and `:2502-2512` for the walk and the brief; `SessionWatch.cs:13-16`, `:322-324` and `Driver.cs:4223` for the watch; `acp-agent.js:4455-4525` for shape 2 | §0, §1.3, §5.1, §10 |
| **O16**, with **C14**, agreed: the rows' order and proofs | Taken: 1a split into 1a and 1a2; 1a's next-look tick, words-once and effect proofs; 1a keeps today's ends for access and fatal; 1c after 1b for `NativeChat` alone; 1d after 1c wherever it promises the native door, whatever the install selects; 1e after 1c; source-derived fixtures marked apart from captured frames; 1g a controlled installed stand-in now and the real incident later, its open date in the backlog | §3.3, §11; `TASKS.md` |
| **C1**, blocking: blanket `-32000` | Taken: with O7 | §3.1 |
| **C2**, blocking: no limit sentence taken as transience | Taken: with O6 | §3.1, §5.2 |
| **C3**, blocking: shape 2's seams and attribution | Taken: with O3. Its two seams are answered by the code: a mid-turn `session/prompt` (`Acp.cs:877`) can carry the provider's error, and the steering lane cannot (`acp-agent.js:2061-2132`), so the steering lane keeps its note; attribution is by the word's taken mark, never by later updates, so an unrelated update after the refusal changes nothing | §4.2; row 1a2 |
| **C4**, blocking: words' acceptance and ownership | Taken: with O3 | §5.1 |
| **C5**, blocking: exhaustion does not hold the quest | Taken: with O2 | §5.1 |
| **C6**, blocking: 31 minutes is a delay sum | Taken: an attempt bound, five tries, named with its 31 minutes of waits; a try's own time is the watch's; an early try counts and the schedule goes on; a stop is the person's; shutdown and an update end as today, the count not kept across the restart; the watcher's explicit signal, `WaitsOnTransient`, read as a permission wait (`SessionWatch.cs:333-338`), its quiet end excluded. Proof in 1a with a held clock | §5.1; row 1a |
| **C7**, blocking: native lifecycles and one-shot ids | Deferred to rows 1c and 1d, with the contract settled here: a live native chat retries on its process, never `--resume` beside it; a driven native run by `--resume`; a one-turn pass by an episode-local id from the mapper, never kept, no try where none was named. Checked: `Adapters.cs:2262-2266`, `ChatRunner.cs:3048`, `keepAs` (`Driver.cs:3605`), `KeepingAsync` (`:4064`). Deferred because 1a, the first delivery, is the protocol door's | §7; rows 1c, 1d |
| **C8**, blocking: retry before authoritative completion | Taken: each run owner's check before each try (§5.5); completion, deletion and the person's stop keep their precedence; the intake's note no longer claims *before it published anything*. Checked: `Observation.cs:100-118`, `Intake.cs:305-308`, `DriverHelpers.cs:652-656` | §5.5, §7, §8; rows 1a, 1d |
| **C9**, blocking: helpers and opinions under their deadlines | Taken: with O5; the waits consume the pass's minutes, and the transient reason wins at expiry inside an episode | §7 |
| **C10**, blocking: a Codex retry notice cannot establish terminal failure | Taken: the `willRetry` inference withdrawn; the typed marker, measured in 1f before it is read, with each case C10 names in 1f's proof | §4.3; row 1f |
| **C11**, blocking: access routed only through the new shape drops the harness's lines | Taken: `Where: harness`, behind today's failure gate, read for access alone; 1e's three cases. Checked: `Observation.cs:457-462`, `Driver.cs:3238-3255` | §3.3, §4.1, §4.2; row 1e |
| **C12**, blocking: retry at the open | Taken: with O8 | §5.6 |
| **C13**: one immutable line cannot carry the live display | Taken: with O10 | §5.1, §8 |
| **C14**: dependencies and evidence overstated | Taken: with O16 | §3.3, §11 |
| Codex §1: *the native run ends with each result* | Taken: true of a driven native run alone; a native chat keeps its process (C7) | §7 |
| Codex §1: *the watcher reads `background.waits`* | Taken: with O15 and C6 | §5.1 |
| Codex §2: D206 composition | Taken: a transient failure during a limit resume sends neither `limit.reset` nor the person's taken words again (§6.1); a limit on a try leaves the episode, is cooled once and reaches D206's planner or LIMITGOON1/2 once (§5.1); exhaustion holds both word resumes and carry-ons (§5.1); a person-released resume that meets the account, conversation or choice gone takes D206's step-1 fallback as the person's, and a further failure is held again (§5.1) | §5.1, §6.1 |
