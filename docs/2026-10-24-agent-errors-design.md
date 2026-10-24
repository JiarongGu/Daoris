# Every agent's errors read into one set of kinds at the agent interface, and one policy acting on each kind (AGENTERRORS1, 2026-10-24)

**Status: accepted (D210) 2026-10-24 by the steward under D191 (D210's acceptance note; Codex's read of this text owed), round 3, with its read's named changes.** Written by Claude Opus from the code at `64059110a`, for
Codex's read and an independent Opus reader's foundation read under D191 (its amendment of 2026-10-23: the design changes what every
adapter answers at the agent interface, and the decided rules TURNFAIL1, D206 and D205 rest on).

**Round 3's named changes** (2026-10-24) take the Opus foundation reader's read of round 3, read at `3086e44f1` and kept verbatim in
`docs/2026-10-24-agent-errors-reads.md`: 11 findings, 1 blocking, *accept after named changes* the steward checks without another
read. Codex's read of round 3 stopped when its credits ran out and is owed on this text. Each finding is taken as the read names it,
checked against the code on main at `ffcf42926`, where no file the read cites changed since its `8cc3936b0`. Each section a finding
changes marks it *(round 3's read, R3-On)*, `R3-O1` being the read's finding 1, and §16's table says where each went. The blocking
one first: an answered settling turn, or a turn the harness woke, ends the wait and never the episode, so Daoris's go-on follows
(R3-O1, §5.1). Written in before row 1a: the uncertain word is D188's COWORK1c `lost` note (R3-O2); a hand-over from an Open quest's
held record carries it on in its tree (R3-O3); Daoris's go-on leaves the record once on the wire (R3-O5). *Try again*'s keyed
release moves to row 1a2 beside its press (R3-O11).

**Round 3** (2026-10-24) answers round 2's two reads, read at `f23020f80` and kept verbatim in
`docs/2026-10-24-agent-errors-reads.md`. Codex asked another round (11 findings, 8 blocking), keeping the four-kind interface, the
conservative limit reading and the ACP driven-session first delivery. The Opus foundation reader would accept after named changes
(11 findings, 2 blocking, and 3, 5 and 6 to write in before row 1a is built). The code is read again on main at `aa16658e7`. Of the
files this design cites, only `Conversations/Chat/ChatRunner.cs` changed since `fb3eb89eb` (HELPNEWCHAT1, 20 lines added near
`:1740`), so its citations past that point move by 20 lines (`:2909-2942`, `:2905`, `:2991`, `:3068`), and every other line number
holds. §15's table takes, answers or defers every finding of both reads. Each section a finding changes marks it *(round 3, …)*:
`R2-O` is the Opus read of round 2 and `R2-C` is Codex's, so `R2-O1` is the Opus read's finding 1 of round 2. New in round 3:
- §5.1's three dispositions of a word, kept across every exit from the episode (R2-C1, R2-C2, R2-O8);
- the release keyed to the end it releases (R2-O1, R2-C3);
- the hold's own word rule (R2-C4, R2-O2);
- the Open quest's way back to its record and the act's case (R2-O3, R2-C5);
- the composition of the tables (R2-O5, R2-C6);
- the driven owner's check and the runs row 1a retries (R2-O6);
- the intake's check from its authoritative endings (R2-C8).

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

Every claim about the code below names its file and line at `64059110a`, which are main's at `aa16658e7` but for
`ChatRunner.cs`, cited at `aa16658e7`. Paths are under
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
| Pipe | `claude-code` (`Adapters.cs:2166-2410`) | The same; `system`/`api_retry` frames on stdout, not read | The `result` frame: `is_error`, `subtype`, `result`'s text, `api_error_status` (the HTTP status); the harness's synthetic `assistant` frame before it (`model: "<synthetic>"`) carries `error`. *(Round 2, O14.)* Measured by MODELROLE1a: `error: "model_not_found"` with `api_error_status: 404`, and a limit as `error: "rate_limit"`, `api_error_status: 429`, *You've hit your weekly limit · resets …* (`docs/2026-10-23-model-effort-doors-evidence.md:186-204`). *(Round 3, R2-C11.)* Only the 404 frame is held in a test (`Driver.Tests/Agents/ClaudeStreamJsonTests.cs:191-212`); the failed 429 result is the evidence document's alone, and row 1c adds its fixture | `is_error` and the text alone, as `StreamMapped.Failed` (`Agents/Streams/StructuredOutput.cs:292-349`) kept as `HarnessWords.Failed` (`Sessions/Domain/Observation.cs:629-632`). No `turnFailed` on this door (`Driver.cs:3730`) |
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
  native conversation reads no failed result at all (`NativeChat`, `Conversations/Chat/ChatRunner.cs:2991`). Nothing in the driver
  reads `529`, *Overloaded*, a 5xx, `api_error_status` or `codexErrorInfo`: every match in `src/` is a test's negative fixture
  (`AccountLimitsTests.cs:258`, `ObservationTests.cs:147`).
- **A conversation's turn** (`ChatRunner.cs:2909-2942`): any `DriverException` becomes `door.turn-not-taken`, read for a limit
  (`_limited`) and a sign-in (`ReadSignIn`), and the turn ends `error`. Nothing tries again.
- **A word refused mid-turn** is a note and nothing else: *a word the agent would not take costs that word, … never the session's
  work* (`Acp.cs:879-883`). That is shape 2's path: the refusal ended the turn the word had opened, and the run read it as clean.
  The steering lane's refusal is caught apart (`SteeredAsync`, `Acp.cs:1396-1400`), and so is a conversation's (`ChatRunner.cs:2857`;
  *round 3, R2-C11*: round 2 cited `:3048`, the native door's `TurnEnded`).

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
one prompt of the person's words never written to the wire and then Daoris's go-on word; a word once on the wire is never sent again,
whatever the run's end. It is never a strike, a park, a cool-off or a walk. Past the bound the record ends `failed` under
`ended.transient`, which no strike counts, and **the planner holds its quest**, Open or Taken. A new word of the person's (MSG1b's
go-on) releases it, and so does *Try again* (D206's step-1 resume, row 1a2's), released for that end alone, so the same conversation goes on.
No other word releases it, and nothing loops without the person. A limit is D206's, unchanged, and a limit's code with no sentence
read is a limit with D125's unstated reset. An access error marks the account as today and goes to the person; the line walks to
another account where one is ready. A fatal error fails with its reason, a strike, as today. Every kind of run takes the same retry inside its run, within its own minutes, and differs only past the bound.

## 3. The kinds, and what decides each

### 3.1 The four kinds

| Kind | What it is | Decided by (any of) | The policy |
|---|---|---|---|
| `limit` | The account's usage is spent: it comes back at a reset | The adapter's `Limits` table reads a sentence and a reset (`AccountLimits.Read`, unchanged). *(Round 2, O6, C2.)* Also a code or kind the door names as a limit, `rate_limit`, an HTTP 429 or Codex's `usageLimitExceeded`, with no sentence the table reads: a limit with no stated reset, cooled for D125's unstated default (`AccountLimits.DefaultCoolOff`, 60 minutes, `AccountLimits.cs:79`), the same `LimitSeen` `Read` makes for a marker with no time (`:119`). Never transient | D206 (§5.2) |
| `access` | The account or its credential cannot be used until a person acts: signed out, refused, not permitted, not verified, no credit | *(Round 2, C1, O7.)* ACP's `-32000` **only where the adapter's own `ErrorWords` declares that it means authentication required**, recorded from that adapter's source: `claude-code-acp` (*Please run /login*, `acp-agent.js:4170-4171`) and `codex-acp` (`index.js:30803-30807`). Nowhere else: the protocol stub answers every failed turn `-32000` (`rehearsal-kit.mjs:889`) though it declares a `SignIn` (`Adapters.cs:1731`), and dsh and a plugin declare nothing; there `-32000` is fatal, as today's strike. The SDK's `authentication_failed`, `oauth_org_not_allowed`, `account_on_hold`, `verification_required`, `cloud_credential_error`, `billing_error`; an HTTP 401 or 403 the door names; Codex's `unauthorized`; the adapter's `SignIn` and `Refused` words, as today | To the person (§5.3) |
| `transient` | The provider or the way to it failed for a moment: nothing about the account, the request or the work | The SDK's `overloaded` or `server_error`; an HTTP status the door names from 500 to 599 (529 among them); Codex's `serverOverloaded`, `internalServerError`, `httpConnectionFailed`, `responseStreamConnectionFailed`, `responseStreamDisconnected`, `responseTooManyFailedAttempts`; the adapter's recorded transient sentences (§4.4). *(Round 2, O6, C2; round 3, R2-C9.)* Never a 429 or `rate_limit`: a failure that carries one is a limit (§3.2's step 1 reads it before any transient sentence), and round 2's exception for a temporary rate limit's recorded sentence is withdrawn for this delivery. A later row may add it only as an adapter's positively recorded reading placed before the code's limit and after the table's limit sentences, held both ways | Tried again, bounded (§5.1) |
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
is today's strike. *(Round 3, R2-C9.)* Round 2's *no code alone makes a transient* contradicted §3.1's own codes and is corrected:
only the transient codes a table names (`overloaded`, `server_error`, a 5xx status, Codex's transient keys) make a transient; an
absent, unknown or unlisted code makes none. `AgentErrorsTests` holds both sides: `overloaded` alone reads transient, and `unknown`
alone, a 400 and no code read fatal.

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
stderr **and its stdout lines outside its protocol** (*round 3, R2-C11*: `HarnessCapture.cs:159` hands an outside line to
`Observation.Said`, `Observation.cs:618`), never the agent's message, and they are read for access alone, never for transient.

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
  access codes the adapter declares), then transient. `AccountRefused`'s missing fallback (§1.2) is closed by going through it.
- **How the tables compose** *(round 3, R2-O5, R2-C6)*. `Limits`, `SignIn` and `Refused` resolve as today, the adapter's own entry or
  else its owner's (`LimitsOf`, `SignInOf`, `HarnessRoster.cs:120-144`). `ErrorWords` does **not** resolve that way: under *own or
  owner*, `claude-code-acp`'s own entry (its `-32000` declaration, §4.4) would hide `claude-code`'s codes and sentences, and the
  install's 529 would read fatal. The reader reads **both, own first**: the adapter's own `ErrorWords`, then its owner's
  (`AccountOf`, `Adapters.cs:1930` for `claude-code-acp`), each step of §3.2's order over both before the next step, a door's own
  entry winning where the two name the same code. The provider's entries (the SDK's kind words, statuses and recorded sentences) sit
  on the owner, `claude-code`, so both of its doors read them. The `-32000` declaration is its own field, `AuthRequiredCode`, read
  from the adapter's own `ErrorWords` alone and never inherited: a JSON-RPC code means what that server says, and an owner's
  declaration, or a plugin's owner's, says nothing about another door. `By` names the table it came from (`own:code:-32000`,
  `owner:kind:overloaded`), so the machine log shows the provenance.
- **`ErrorWords`** (new, on `HarnessToolchain`, beside `Limits` and `SignIn`): the adapter's own map from its codes, kind words and
  statuses to a kind, with its recorded transient sentences and, where its source shows it, `AuthRequiredCode`. Its shape is
  `LimitWords`' (`AccountLimits.cs:296-361`): anchored, case-blind patterns under a timeout, each recorded entry with its source.
  *(Round 3, R2-O9.)* A plugin's harness declares none of its own (`DeclaredAcpAdapter.cs:46-59`). A plugin that declares
  `accountOf` reads its owner's `ErrorWords` through the same composition (`:52`), but never its `AuthRequiredCode`. A plugin
  with no owner reads nothing by code, and its failures stay fatal, as today.

`turnFailed` stays the string every reader already takes, so nothing that reads it changes in row 1a. The conclusion gains the
`AgentError` beside it, and `AccountRefused`, `AccountSignedOut` and `AccountLimited` each become a reading of its kind. *(Round 2,
O9.)* Each reader §1.2 names learns the kind on its own, in the row that reaches it: `Observation.Conclude`, `Observation.Resumed` and
`WentOn` (`ended.transient`), `FailedTurnWords.Judge` and D206's waiting set (*round 3, R2-C2*: after an episode, handed only the words
still owed, whatever the end's kind, §5.1), `IntakeObservation.Conclude` (row 1d) and `ReadHelper` (row 1d).

### 4.2 Each door's answer

| Door | Where its `DoorFailure` is made | What it fills |
|---|---|---|
| Protocol, every adapter | *(Round 2, O1.)* Inside `AcpSession.RunAsync`, at each of its prompt sites (`Acp.cs:823-825`, `:871`, `:877`, `:1048`), where a `session/prompt`'s `error` becomes `AcpRefusal.Of` (`Acp.cs:224-239`, `:2019-2024`). The run reads it there, through the reader the capture hands it (§5.1), and keeps the last one for the conclusion's capture (`Driver.cs:4312-4326`) and the conversation's (`ChatRunner.cs:2909-2942`), where they keep `error.Message` today | `Code`, `Kind` (`data.errorKind`, or the key of `data.codexErrorInfo`, a string or a one-key object), `Status` (`codexErrorInfo`'s `httpStatusCode`), `Words` (`AcpRefusal.Words`), `Where: prompt`. `-32000` reads as §3.1 says, by the adapter's declaration |
| Protocol, shape 2 | *(Round 2, O3, C3.)* The mid-turn word's own prompt, awaited at `Acp.cs:877`. Its refusal is the turn's prompt failing **where the word was taken** (its `Taken` mark, set when the prompt before it answered, `Acp.cs:1667-1671`): the turn that failed is the one the word opened. A word refused before it was taken (the turn before it still running, its `Taken` false) is that word's refusal, today's note, and the running turn goes on. The steering lane (`SteeredAsync`, `Acp.cs:1396-1400`) answers `injected` at once and never carries the provider's error, so its refusal stays today's note | The same fields, `Where: prompt`. The word was taken: it is never sent again (§5.1) |
| Protocol, the open | *(Round 2, O8, C12.)* `OpenAsync` and `ResumeAsync` (`Acp.cs:793-794`, `:2817-2824`), read before `ContinueWhy.Refused`, after D205's `AcpChoiceNotOffered` (`:796-803`) and the `Gone` and `Elsewhere` codes. *(Round 3, R2-O2.)* Row 1a2's: until it lands, the open makes no `DoorFailure` and a failure there keeps today's end | `Where: open` (§5.6) |
| Pipe, `claude-code` | `ClaudeStreamJson.Result` (`StructuredOutput.cs:292-349`), which reads `is_error` and the text today. It also reads `api_error_status`, and keeps the `error` field of the harness's synthetic `assistant` frame before it | `Status`, `Kind` (the harness's `error`), `Words` (`result`), `Where: result`. It goes to the conclusion as the pipe door's `turnFailed`, which closes D206 row 1f's gap for limits and sign-ins in the same reader |
| Every door, the harness's lines | *(Round 2, C11; round 3, R2-C11.)* The harness's last lines, its stderr and its stdout outside its protocol (`HarnessCapture.cs:159`, `Observation.cs:618`), behind today's gate: a failed result or a nonzero exit (`Observation.cs:457-462`) | `Words`, `Where: harness`, read for `Refused` alone |
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
  (`ChatRunner.cs:2909-2942`) is right as round 1 wrote it, since its process lives across turns (row 1b). The pipe door's is row
  1c's (§11).
- **Which runs are handed one** *(round 3, R2-O6)*. Every kind of run shares the capture (`Driver.cs:3685-3693`). A run handed no
  `TransientRetry` keeps today's end, whatever the reader says. Row 1a hands one to **driven quest sessions alone**: a start's
  `HoldAsync` (`Driver.cs:2683`) and a continuation's (`DriverContinuations.cs:431`). Intakes, helpers, opinions, partners and
  voices get theirs in row 1d, and conversations theirs in row 1b.
- **The backoff and the bound** *(round 2, C6)*. The bound is **five tries**. Before each the run waits 1, 2, 4, 8 and 16 minutes on
  its clock: 31 minutes of waits between tries, `TransientRetry.Delays`, one constant set for every door and every kind of run. A
  try's own time is the harness's and is not counted. *(Round 3, R2-C11.)* What bounds a try that never answers differs by the
  run, and the bound is a count of tries, never a promise of when the episode ends:
  - a driven session's and an intake's try is a running turn under the session watch (`Driver.cs:3566`), raised as a hung turn is
    today;
  - a pass's try is bounded by its own minutes (§7);
  - a conversation awaits its process with no watch (`ChatRunner.cs:2425`), so its hung try is the person's to stop, as a hung turn
    of a conversation is today (row 1b).

  A word the person says during a wait goes at once as the next try and
  counts as one; the wait after it is the schedule's next. Why so slow at first: the harness already retried before it gave up
  (Claude Code's `api_retry`, Codex's `willRetry`; on shape 1 the error came three and a half minutes after the last tool result). A
  retry within seconds of that only meets the same outage. Why the bound: an outage past half an hour of waits, after the harness's
  own retries, is one the person should know of. Waiting spends no tokens, but it holds a slot under the cap. A try that succeeds
  after the 8- or 16-minute wait likely re-reads the whole context past the provider's prompt cache (219,284 tokens in shape 1):
  that is the price of keeping the conversation (§12).
- **The wire settles first** *(round 2, C4, O3; round 3, R2-O8)*. After a transient failure the run waits until no prompt is open
  on the wire (`_prompts` empty, as `Answered` reads it, `Acp.cs:1660-1674`). On `claude-code-acp` a word queued behind the failed
  turn is already in the SDK's input queue (`promptQueueing`, `acp-agent.js:1257`), and the adapter rejects only the active turn, so
  *later turns proceed* (`:2750-2778`). Settling therefore runs each queued word's own turn, the **settling turns**:
  - *(round 3's read, R3-O1; round 3's first clause corrected)* a settling turn that answers **ends the wait, not the episode**. It
    is the queued word's own turn, so its answer shows only that the agent answered that word, never that it went on with the turn
    the provider failed. Ended there, the run loop would find nothing more (`Acp.cs:841`) and close clean, and the conclusion would
    park a taken quest on the person (`Observation.cs:173-174`) or fail an open one `ended.untouched`, a strike (`:205-206`). So the
    owner's check (§5.5) is asked, and where another turn is still wanted Daoris's go-on goes at once, with no wait, and counts as a
    try;
  - a settling turn that fails transient is that failure and counts as a try;
  - one that fails with another kind leaves the episode for that kind (below).
- **What a word's disposition is** *(round 3, R2-C1, R2-O8; round 2's two dispositions corrected)*. `Taken` is Daoris's own
  bookkeeping (`Acp.cs:429`), set by the order of answers (`:1667`) or by an update after an error (`TakeLate`, `:1697`, `:1944`).
  A `false` mark is no negative acknowledgement, since a JSON-RPC error does not say whether the server appended the word before it
  failed. So each of the person's words is in one of **three** dispositions:
  - **held**: never written to the wire, the inbox's words (said during a wait, below), and the words a resume would have prompted and did
    not, at an open that failed (§5.6);
  - **taken**: on the wire, and its turn showed it taken: an update of that turn came (`TakeLate`), or its prompt answered. Shape
    2's word is taken (the adapter streamed the error's message in its turn before the prompt's error, event 2300), and so are a resume's or a start's blocks
    (`resume.Prompted`, `DriverContinuations.cs:696-700`) and a try's words, each once its turn shows it;
  - **unacknowledged**: on the wire, its prompt answered with an error before any update of its turn. It may or may not be in the
    agent's conversation. *(Round 3's read, R3-O2.)* This is read from the turn, never from Daoris's `Taken` mark alone: a head word
    whose mark the order of answers set (`:1667`) and a try's blocks that met the error before any update are just as uncertain.

  The episode reads each word's disposition by its prompt's request id **before** `Answered` removes the prompt (`:1662`), and keeps
  it with the episode. **Only held words are ever sent by a try.** A taken or unacknowledged word is never sent again, on a try or
  after the episode. An adapter may later declare proven nonacceptance, with a recorded frame held both ways (an error that says
  the word was not appended), and only then would its unacknowledged words count as held. No adapter declares it today, and none is
  assumed. *(Round 3's read, R3-O2: round 3's own note withdrawn.)* An unacknowledged word is D188's COWORK1c **uncertain** word,
  and it is said as COWORK1c says one: each word of the person's left unacknowledged gets the `lost` note under its id
  (`SessionEventCodes.Lost`, `Sessions/Domain/SessionEvents.cs:70-75`: *the agent may have read it, and its answer was not kept*),
  as `UncertainWords.SettleOneAsync` keeps it (`Conversations/GoOn/UncertainWords.cs:84-90`), and the page offers *Say it again*
  under it as under any uncertain word of the person's (D188's COWORK1c note). Daoris never sends it again. No new note code is
  made.

  What the agent did before the failure (its files, its commits, its tools' effects) stays in its tree and its conversation, and the
  go-on word says to go on from there: nothing is replayed.
- **The dispositions outlive the episode** *(round 3, R2-C2)*. The episode hands the conclusion the disposition of every word the
  run carried, whatever kind ends it: a try that answers, a limit, access, fatal, past the bound, the person's stop or a shutdown.
  At that conclusion every taken or unacknowledged word comes off the record by its id, as taken words do today
  (`service.TakenAsync`, `DriverContinuations.cs:719`). Only the held words are left to `FailedTurnWords.Judge`, D206's
  `LimitWait` and TURNFAIL1 (`:699-717`, `Observation.cs:503`), so a limit's resume at the reset, a briefed move or TURNFAIL1's
  next look carries **only words still owed**. A run in which no episode opened keeps today's judgement unchanged, TURNFAIL1's
  included.
- **Words said during the wait are held** *(round 2, O1)*. While the episode waits, the inbox holds every word: its mid-turn route
  (`Acp.cs:809-812`) and the steer send nothing, so no word goes alone, and none is cut by a go-on sent after it as a second prompt
  (the adapter would end the person's turn at the go-on's echo). The person's word ends the wait at once (above).
- **Each try is one prompt** of blocks *(round 2, O1; round 3, R2-C1)*: first **the person's held words**, verbatim and in the order
  said; then **Daoris's go-on word** (`retry.go-on`): *The last request failed on the provider's side ({{words}}). Go on where you
  stopped, after anything said above.* *(Round 3's read, R3-O8.)* Its last clause keeps a held word that redirected the agent ahead
  of the old path. The go-on is kept through the say door under `DaorisKeys.TransientRetry(session, episode)`, as D206's
  `limit.reset` is kept under `DaorisKeys.LimitReset`, once per episode, before the first try. The episode is named by when its
  first failure was read. *(Round 2, O10; round 3, R2-C10.)* Its `user` event is the first try's alone; a later try carries the same
  text as a block that is neither kept nor shown again (row 1a).
  *(Round 3's read, R3-O5.)* **The go-on leaves the record once it is on the wire**, as D188 point 6 has any word leave `said`
  (`docs/decisions/D188.md:52`): it goes through the run's one dispatcher like any kept word (`SaidDoor.Dispatched`,
  `Conversations/SaidDoor.cs:69`), marked attempted before the wire and taken off by its id once its first try is written. It never
  waits on `said` through the episode. The conclusion's take-off stays as the fallback at **both** conclusions, a first run's and a
  continuation's (below).
- **Through the wake gate** *(round 2, O4)*. A session whose turn failed while its background work runs is woken by its harness when
  that work ends (BGWAIT2/3, `Acp.cs:924-949`), and a prompt sent into that woken turn is never answered (BGWAIT1's 48-minute hang,
  `:936-939`). So each try goes through `_wake` as `AfterEndedAsync` does (`:1037-1055`), and Daoris prompts only where none woke
  within `AcpWake.Quiet`. *(Round 3's read, R3-O1.)* A woken turn's end during an episode **ends the wait, not the episode**: the
  door cannot say whether a woken turn answered or met the outage, since `AfterEndedAsync` records every woken end as `end_turn`
  (`:1039-1046`, `WokenStop`). So, as after an answered settling turn, the owner's check is asked and, where another turn is still
  wanted, Daoris's go-on goes at once, after the woken turn has ended, and counts as a try. A stream that ended is the harness's end
  (§3.1), not a try.
- **The owner's check** *(round 2, C8)*. Before each try the run asks its owner whether another turn is still wanted (§5.5). Where
  not, no try is sent: the episode ends and the run closes as at a turn's end, and the conclusion reads its outcome as it does today.
  *(Round 3, R2-O6.)* A check that cannot be read answers yes, as `QuestMovedAsync`'s unread look does: a try costs a turn, and an
  unread look must not end the episode as though the work were done.
- **Said once.** The conversation gains one line at the episode's first failure, `agent.transient-retrying`: *{{agent}}'s provider
  failed ({{words}}). Daoris tries again in this conversation: up to 5 times, after 31 minutes of waits between them, the next at
  {{time}}.* No line is said per try, and none for a try that works, since the turn going on says it. *(Round 2, O10, C13.)* Each
  try and each wait is a quiet event, `agent.transient` (`try`, `next`), never a line: the session's head reads it and shows
  *Trying again at {{time}}* while a wait runs and nothing while a try runs. The streamed `API Error` agent message each failed try
  repeats is dropped by the episode's events, as `toTurns` drops it beside `door.failed` today (D137's TURNFAIL1 note).
  *(Round 3, R2-C10.)* This drop rule and the re-sent go-on kept off the record are row 1a's, the minimum the person must see right
  on the first delivery. Row 1a's proof asserts the **rendered** conversation (`toTurns` over the record's events): the provider's
  error once, the go-on once, and one `agent.transient-retrying` line, not only the count of notes. The head's *Trying again at* is
  row 1a2's. The machine log says each try (`agent.transient`: session, adapter, account, `By`, try, next).
- **What the waiting run is** *(round 2, O1, O7, C6)*. The run waits on Daoris's own timer, never on its agent. The door exposes
  `WaitsOnTransient` beside `WaitsOnOwnWork` (`Sessions/Watch/SessionWatch.cs:13-16`), wired where that one is (`Driver.cs:4223`), and
  the watch reads it as it reads a call held for the person's permission (`SessionWatch.cs:333-338`): a wait that is not the agent's,
  opening no episode and ending nothing, `--quiet-end` included (`:340-342`), since the wait is bounded by the episode itself. A try
  is a running turn, read by the watch as any. Round 1 cited `background.waits` (`Platform/NoteCodes.cs:912`): the watch reads no note.
  *(Round 3, R2-O7.)* `Heard` is stamped only by the capture's lines and appends (`SessionWatch.cs:173-176`), and a later try's
  prompt is neither kept nor shown, so when `WaitsOnTransient` falls after the 16-minute wait the session would already read 16
  minutes unheard. A headless `--quiet-end` shorter than that would end the run and carry it on at the try's first moment
  (`:342-348`), which is shape 1's loss. So **the wait's end stamps the session heard**, and a try's quiet counts from its prompt.
- **What ends an episode early.** A try that answers ends it; the count starts again at the next failure. The person's stop or pause
  ends it as theirs, with no failure said. *(Round 2, C6.)* Daoris's shutdown or an update's close cancels the wait with the run,
  which ends as that stop does today (D104's interrupted stop, D206 §3.9's update word); the episode's count is not kept across the
  restart, so a line carried on afterwards starts a fresh count. A try that fails with another kind leaves the episode for that
  kind's policy, once: a limit concludes as a limit, its account cooled once by `AccountLimited`, and goes to D206's planner (a driven
  line) or LIMITGOON1/2 (a conversation) (§6.1); access for §5.3's; fatal for §5.4's. *(Round 3, R2-C2.)* Each takes the episode's
  dispositions with it (above): what D206 resumes at the reset or moves briefed, and what TURNFAIL1 retries, is only what was held.
- **Past the bound** *(round 2, O2, C5; round 3, R2-C10)*. The run ends. Its record is `failed` with `ended.transient`: *Its provider
  kept failing after {{count}} tries, with {{minutes}} minutes of waits between them ({{words}}). It waits for your words, which go
  on in the same conversation.* *(Round 3's read, R3-O11.)* Row 1a2 widens its last sentence to *It waits for you: your words, or Try
  again, go on in the same conversation*, with the press that offers *Try again*: each row's sentence says only what its page offers.
  `{{count}}` and `{{minutes}}` are the tries made and the minutes actually waited, never the schedule's five and 31: a word of the
  person's sends a try early, and the waits between are then shorter. A pass whose minutes end inside an episode says that in its
  own words (§7), never this sentence.
  - **What waits on the record.** Every taken or unacknowledged word comes off at the conclusion (above), so after a prompt failure
    nothing of the person's waits from the run. *(Round 3's read, R3-O5; round 3's citation corrected.)* Daoris's go-on left the
    record when its first try was written (above). Where that take-off did not land, the conclusion takes it off by its id as the
    fallback, at **both** conclusions: a continuation's, beside its take-off of the resume's own Daoris words
    (`DriverContinuations.cs:702-707`, which reach only `LimitResume.WordIds(resume.Words)`, never a word kept mid-run), and a first
    run's, which takes no Daoris word off today (its only take-off is the refused step 1's, `Driver.cs:2304`). So no go-on waits on
    a record the run then parked, and none reaches a new part. Round 1's `GoOnMarks.Transient` stays withdrawn for this
    case: a mark stops the planner's `GoOn` alone (`Planner.cs:1494-1506`), never a carry-on (`:1373-1387`, `:2005-2006`) or an open
    quest's fresh start (`:1355-1366`), and `GoOnMarks.Judged` needs a word waiting (`Conversations/GoOn/GoOnMarks.cs:131-134`), which
    shape 1 never has. Words the person says after the last try failed wait on the record as today (`inbox.Close()`,
    `Driver.cs:4325`); *(round 3's read, R3-O9)* one said after the record ended is a new word of the person's and releases the hold
    below, and one said in the last try's moments, before the end, waits with the others and goes with that next word.
  - **Nothing prompted: the open's end** *(round 3, R2-O2)*. Where the run ends `ended.transient` without its words ever going on
    the wire (an open that failed past its bound, §5.6, row 1a2), the resume's blocks are held words, and today's conclusion would
    leave them, and Daoris's go-on, on `said` (`DriverContinuations.cs:699-707`, `:717`). `WordsOn` would then take the record up
    ahead of the hold at every look, with no strike to end it. So at such an end:
    - **Daoris's go-on comes off by id**, whatever `words` reads;
    - *(round 3's read, R3-O9: round 3's `transient-held` mark withdrawn)* **the person's carried words wait for a word of theirs
      said after the record ended**, by the hold's word rule (below), which reads each word's `At` against the record's `Updated`
      and so needs **no write** beside the conclusion. A mark written there would fail open on a failed write: the word rule
      would read the unmarked words as a new word of the person's and retry the open round the hold, with no strike. The carried
      words were said before the end, so they never release it; the person's next word does, and takes them with it.

    **Until row 1a2 lands, a failure at the open makes no `DoorFailure`** (§4.2) and keeps today's end, a strike and the briefed
    fallback, so row 1a never meets this case.
  - **The planner holds the quest** *(round 3, R2-O4)*. A quest, Open or Taken, whose last record here ended `failed` with
    `ended.transient` is held: `TransientHolding(quest)` reads the note's code, as `StoppedByReopen` reads its own
    (`Planner.cs:2020-2022`), never a mark, so no write beside the conclusion can fail it open. It sits directly after `WordsOn`
    (`:1325-1329`) and `StopHolding` (`:1334`), and before every other path:
    - an Open quest's `RetriedFrom`, `LimitedFrom` and fresh `Consider` (`:1345-1366`);
    - a Taken quest's awaiting resume (`:1367-1371`, D79's, which `WordsOn` skips, `:1496`);
    - a Taken quest's `CarriedOnFrom` (`:1373-1387`).

    It passes a record the person released (below), and a hand-over made from the held record, as `StopHolding` passes one
    (`HandedFrom`, `:1455-1456`, `:1465`; `DriverHandOvers.cs`), so *Go on now with…* (§6.2) stays the person's way out. *(Round 3's
    read, R3-O3.)* Passing the hold is not enough for an Open quest: after it the planner would reach `Consider`'s fresh start in a
    new tree (`:1355-1366`), with no brief. So the hand-over's record is carried on in its tree, as D206's step 0 carries a limited
    one (below, `TransientReleasedFrom`). Its verdict
    is `StartVerdict.TransientHeld`, with the record as its `HeldBy` as a stop's verdict names its stop; it is said `held.transient`
    and takes no slot.
  - **The hold's word rule** *(round 3, R2-C4, R2-O2)*. `WordsOn` comes first, and its `Waits` (`Planner.cs:1503-1505`) passes any
    word waiting on the record: Daoris's, a helper's answer handed as the next turn (`DriverHelpers.cs:776-810`), a second opinion's
    findings, another session's message. So, for a record `TransientHolding` would hold and no release passes, `Waits` also needs a
    waiting word that is **the person's** (`SaidWordView.Persons`, `Planner.cs:675`: no `By`, no `Helper`, no `From`), is not
    judged by a mark, and *(round 3's read, R3-O9)* was **said after the record ended**: its `At` later than the record's `Updated`,
    both read from the look's snapshot. Without one, `WordsOn` answers nothing for it and the hold holds. With one, `GoOn` takes every
    waiting word, the person's first and the others with them, so a helper's answer, a Daoris word or a word of the person's said
    before the end travels once with the person's release and starts nothing on its own.
  - **Released by the person, for that end alone** *(round 3, R2-O1, R2-C3)*:
    - **their word**: `WordsOn`'s `GoOn` (MSG1b, `ContinueAsync`) resumes the record's own conversation with their words, as any
      word to an ended record does. *(Round 3's read, R3-O8.)* It sends **their words alone**, as MSG1b does for any ended record:
      the person's word is itself the go-on, and a go-on after it could countermand a word that redirected the agent. No go-on of
      Daoris's is kept for this release;
    - ***Try again*** *(round 3's read, R3-O11: row 1a2's, beside its press)*: the press writes a **transient release**,
      `DriverConfig.TransientReleased`, quest → the record's session and
      **the end it releases**, which is the record's `Updated` as the press read it (`PriorSession.Updated`, `Planner.cs:333-336`:
      for an ended record, when it ended). It is not the stop's `Released` (`DriverConfig.cs:94-123`). That one is keyed by session
      alone, and its own rationale is that a later stop has a different session, which a resume of the same record never has. The
      hold passes a released record only while its session **and** its `Updated` both equal the release's. The released start
      continues the same record (`Driver.cs:2267-2272`, `DriverContinuations.cs:226`, `:272-274`), which moves it to `working` and
      later to its next end, so its `Updated` moves past the release. If that run fails past its bound again, its new
      `ended.transient` is held again, with no second write, across a driver restart (the release is on disk), and with nothing to
      consume. A release whose `Updated` is unsaid passes nothing: the check fails closed. *(Round 3's read, R3-O4; round 3's
      sentence corrected.)* A word kept meanwhile leaves `Updated` as it is, so the release still passes, and the start goes on with
      that word under the person's release, by MSG1b's `GoOn` rather than step 1; only the record's own end moves it. `KeepSaidAsync` writes `updated` only for a keep that carries a note, which only
      an answer's line does (`src/Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:1554-1566`), and `TakeSaidAsync` writes
      none (`:1590-1596`). That outcome is benign: the word is one the record was waiting with. The person's word needs no release
      written, since it is a new word each time.

    The next look plans the line's carry-on:
    - **An Open quest** *(round 3, R2-O3, R2-C5)*: `RetriedFrom` answers only with commits (`Planner.cs:1391-1397`) and `LimitedFrom`
      only with the `limit` flag (`:1400-1405`), so a commitless Open quest would fall to `Consider`'s fresh start. A sibling,
      `TransientReleasedFrom(quest)`, answers the `ended.transient` record **ahead of `RetriedFrom`'s commit rule**, and the quest is
      planned `CarryOn(quest, last)` whatever its tree holds. *(Round 3's read, R3-O3, R3-O11.)* It answers two records: in row 1a,
      the held record a hand-over was made from (`HandedFrom`, `Planner.cs:1455-1456`), so *Go on now with…* is step 0 as D206's
      is and goes on in the quest's tree; and from row 1a2, the record *Try again* released. The person's word needs neither: an
      Open quest's waiting words are `WordsOn`'s, ahead of the Open path (`:1325-1329`), and resume the record itself.
    - **A Taken quest**: `CarriedOnFrom` already answers it (`CarriesOn` is true for `failed`, `:2005-2006`).
    - **An awaiting Taken quest**: *Try again* is its release too (row 1a2), and its next start is D79's resume of that record.

    *Try again*'s start (row 1a2) takes **D206's step-1 resume**. `LimitResume.Line` (`LimitResume.cs:26-32`) also answers a released `ended.transient`
    record, Daoris's go-on is kept just before `ContinueAsync` under `DaorisKeys.TransientRelease(session, ended)`, keyed by the same
    end, since `TransientRetry(session, episode)` names no episode across runs. A refused resume takes the word off and goes
    briefed, as D206's does. The act: `StandingChoices.Retry` (`StandingChoices.cs:108-131`) refuses anything not `Exhausted`,
    `Stopped` or `Paused` today, and gains the case `TransientHeld` → `RetryDecision.Released` with
    `config.WithTransientReleased(quest, session, ended)`.
  - **Step 1 is the protocol door's** *(round 3, R2-C7, R2-O3's note)*. `LimitResumes` refuses every adapter that is not ACP
    (`DriverContinuations.cs:58`), since the native door's `--resume` of a missing conversation is not recorded to fail closed.
    Row 1a's `ended.transient` is the protocol door's alone. For the native door, row 1c first measures `--resume` of a captured
    id that is gone, on the real door. Where it fails closed, 1c widens the guard for this release and proves both the resume of
    the existing id and the fallback when the id is gone. Where it does not, **the native *Try again* is not offered as the same
    conversation**: it goes briefed, and the native door's `ended.transient` says so in its last clause (§8). *(Round 3's read,
    R3-O7.)* That is *Try again*'s release alone. The person's word goes by MSG1b's `ResumesOwn` (`DriverContinuations.cs:40-45`),
    which has no such guard and resumes the same native conversation today, so the native clause splits the two releases.
  - **The release is the person's**, so a resume that meets the account, the conversation or the choice gone is the person's
    authorized carry-on, by D206's step-1 fallback, and D205's walk inside it is the start's, not an escape from the bound. A
    released run that fails past its own bound ends `ended.transient` at a new `Updated` and is held again: an outage never cycles
    without the person.
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
| A driven session | *(Round 3, R2-O6.)* Its own predicate, `TransientRetry.Wanted`: its quest is done, submitted, awaiting another repository, stood down, closed or deleted, read from the quest as the ledger answers it now (`FindQuestAsync`). **An Open quest goes on**, and so does one Taken by this session. Round 2 cited DONEEND1's `QuestMovedAsync`, which reads `Observation.Parks` (`Driver.cs:3445-3460`, `Observation.cs:216-219`): that needs a quest `Taken` by this session, so it answers *no* for an Open quest, and a first run that failed before its take (a likely moment for an overload) would get no try |
| A conversation | never: a person's turn has no outcome of its own to outrank it |
| A helper | its answer is stored (`files.ReadAnswer`, `DriverHelpers.cs:652`) |
| A second opinion | its opinion is recorded |
| An intake | *(Round 3, R2-C8.)* `IntakeObservation.Conclude` itself (`Intake.cs:295-330`), asked of the ask as read now against the run's own start baseline (`before`, the quests it held when the intake started): **no try wherever it answers an authoritative ending** rather than its turn-failed path. That covers an ask deleted (`:298-303`), published by this intake (`:309-317`), closed by the person (`:320-324`) or answered by somebody else (`:326-331`). Round 2's two cases omitted the last two |
| A partner | its piece is ending or ended |

The person's stop, a delete and completion keep their precedence: a check that answers *no* ends the episode with no failure said,
and the conclusion reads the run as it does today. A check that cannot be read answers yes (§5.1).

### 5.6 A transient failure at the open *(round 2, O8, C12)*

An open the agent refused has delivered nothing: there may be no session id, and the target or the resume's words were never sent.
So the open has its own phase. A `DoorFailure` with `Where: open` is read before `ContinueWhy.Refused` (`Acp.cs:2817-2824`), after
D205's `AcpChoiceNotOffered` (`:796-803`, unchanged) and the `Gone` and `Elsewhere` codes. Where it reads transient, the run waits on
the same schedule and repeats **the failed opening operation itself** (`session/new`, `session/resume` or `session/load`), then sends
the run's own first prompt once, as it would have: no go-on word, since nothing was asked. Anything else goes as today: a refused
resume to ANSWER1a's and D206's briefed fallback. Past the bound the start ends `failed`, `ended.transient`, and the line is held as
§5.1 says: never briefed onto a provider that is down.

*(Round 3, Codex's row note on 1a2.)* The try repeats the **raw** operation on the wire. It never calls `OpenAsync` or `ResumeAsync`
again, since their handshake runs once per session (`Acp.cs:1159`). *(Round 3, R2-O2; round 3's read, R3-O9.)* Past the bound
nothing was prompted, so §5.1's *nothing prompted* end applies: Daoris's go-on comes off by id, and the person's carried words,
said before the end, wait for a word of theirs said after it, by the word rule's time, with no mark written. Row 1a2 builds this,
and until it does a failure at the open keeps today's end.

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
  try ends the episode as a limit, cooled once: D206 point 3 from there. *(Round 3, R2-C2.)* That limit's conclusion takes the
  resume's words off as taken (§5.1's dispositions outlive the episode), so D206's `LimitWait` at the next reset, or its briefed
  move, carries only words still held, never the resume's original set again (`DriverContinuations.cs:699-706`). `limit.not-lifted` reads only a limit, so a transient
  failure within the margin after a reset is not *not lifted*.
- **Conversations.** A conversation waiting on a limit (`ChatTurns.WaitForLimit`) is not running a turn, so nothing here applies to
  it until its wake. Its wake's turn is a turn like any other. A limit read on a conversation's try goes to LIMITGOON1/2 once.
  *(Round 3, R2-C2.)* `LimitHeld.Of` collects the person's words since the last turn that ended otherwise
  (`Conversations/Domain/LimitHeld.cs:17-45`), and row 1b's episode ends no turn between tries. So each try's quiet `agent.transient`
  event names the ids of the words that went on the wire with it, and `LimitHeld.Of` leaves out words such an event names.
  *(Round 3's read, R3-O6.)* That alone is not enough: `LimitHeld.Of` collects every person's `User` event since the last turn that
  ended otherwise (`LimitHeld.cs:27-30`), the failed turn's own ask among them, and no try's event names it. So **the episode's first
  `agent.transient` event also names the ids of the failed turn's own words**, and a limit after a transient episode holds only the
  words still owed.
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
  TURNFAIL1's path: its words a try carried are taken (§5.1), and past its bound the planner holds the line (§5.1). *(Round 3,
  R2-C2.)* A fatal failure on a try, after an episode opened, hands TURNFAIL1 only the held words, so a resume's original set is never
  retried at the next look once it went on the wire in that run. A run with no episode keeps TURNFAIL1 as it is.
- APIREFUSAL1's `refusal` stop reason is a `fatal` kind's reading (§3.1), unchanged in its note and its strike.

## 7. What each kind of run does

Every kind of run takes §5.1's retry inside its run, since every kind's turn goes through a door's capture: driven sessions
(`Driver.cs:4126`), conversations (`ChatRunner.cs:2905`), and helpers, opinions, intakes and partners through `HoldAsync`'s capture
(`DriverHelpers.cs:907-1037`, `DriverOpinions.cs:34-259`, `DriverIntakes.cs:367-419`, `DriverPartners.cs:620`). Each asks its owner's
check before each try (§5.5). What differs is the end past the bound.

*(Round 2, O5, C9.)* **A pass with minutes of its own** (helpers 15, `Conversations/Domain/HelperInstruction.cs:34`; opinions 20,
`Review/Domain/OpinionRules.cs:101`; partners 45, `Conversations/Domain/PartnerInstruction.cs:28`; a brainstorm's turn) is killed at
them and concluded `Observation.TimedOut(bound)` today (`Driver.cs:3919-3924`; `DriverHelpers.cs:978`, `:993`; `DriverOpinions.cs:202`,
`:217`; `DriverPartners.cs:603`, `:617`). Its minutes are not extended: the waits are spent from them, and **no delay is waited past
the pass's deadline**, as round 1 said of a brainstorm alone. A pass whose minutes end inside an episode (in a wait, or in a try after
a transient failure) ends with the transient reason, not *timed out*. So a helper's or an opinion's *past the bound* is usually its
minutes: a helper has room for its first three tries. *(Round 3, R2-C10.)* Each kind's sentence below names the tries it made, and
says where its minutes ran out (`{{count}}` tries, *before its minutes ran out*), never the schedule's five.

| Kind of run | During the retry | Past the bound, or its minutes inside an episode |
|---|---|---|
| **A driven session** | Its record stays `running`; the conversation says `agent.transient-retrying` once; the head says *Trying again at {{time}}* during a wait | `failed`, `ended.transient`, no strike, every word that went on the wire off as taken, Daoris's go-on off, the quest held for the person until a new word of theirs or *Try again* for that end (§5.1); *Ended* raised once (`AttentionWatch`, `Quests/Domain/Attention.cs:248-254`, worded by its code); *Go on now with…* on its header, and *Try again* from row 1a2 |
| **Ask Daoris and a repository's chat** | The turn does not end `error`: the runner waits and tries again on the same process. Words said meanwhile are kept on `said` and shown queued; they go as the next try at once (§5.1). *(Round 2, C13.)* The line is `agent.transient-retrying`, one code for both | The turn ends `error` with `chat.transient-stopped`: *{{agent}}'s provider kept failing ({{words}}): your next words go on in this conversation.* The conversation stays; the person's next word is a turn as today. No record state changes, as a chat's failed turn changes none today |
| **A helper** | Its pass waits and tries again inside its run, within its minutes | Settled `refused` with a reason of its own, `HelperWhy.Transient`, said `helper.transient` on the asker (*Helper {{agent}} could not answer: its provider kept failing ({{words}})*), not `no answer` and not *timed out*. A lead waiting on it goes back to the person as a refused helper's lead does (`BackToPersonAsync`, `DriverHelpers.cs:363-386`), now with the reason |
| **A second opinion** | Its pass waits and tries again inside its run, within its minutes | The pass is `failed` with why `transient` (`Opinions` gains it beside `limited`); the gate is `Unavailable` with code `transient` and no `Until`, so the look does not re-ask it (`OpinionGateLook.cs:220-221` re-asks only a cooling gate) and the person's *Try again* does |
| **An intake** | Its run waits and tries again, unless `IntakeObservation.Conclude` answers an authoritative ending for its ask (§5.5) | `failed`, `intake.transient`. The ask is unbound once (`unbindAsk`, as a limited intake's is, `DriverIntakes.cs:405-409`), so the next look picks it again with a fresh intake. An ask whose last intake already ended `intake.transient` stays bound, and its *Ended* is raised, so an outage costs at most two bounds before the person hears. *(Round 2, C8.)* The note no longer says *before it published anything*: a transient error does not establish that, and an ask that gained quests concluded on its quests (§5.5) |
| **A partner** | Its run waits and tries again, within its minutes | The piece goes to `Ending` and the lead is told `partner.transient`, the work kept, as `partner.limited` is (`DriverPartners.cs:620-623`, `PartnerNotes.cs:34`) |
| **A brainstorm voice** | Tries only while its pass's minutes allow | The voice is dropped `Unavailable` with the reason, as a voice that cannot run is (D205 keeps voices frozen) |

*(Round 2, C7.)* **The native door's runs differ in their process, and row 1c and 1d settle each apart:**
- a **live native conversation** keeps one process across its turns (`Adapters.cs:2262-2266`, `ChatRunner.cs:3068`), so its try is
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
- **The hold** reads `ended.transient` by its code (`TransientHolding`, §5.1). *(Round 3, R2-O1, R2-C3; row 1a2's, round 3's read,
  R3-O11.)* *Try again*'s release is its own,
  `DriverConfig.TransientReleased`, quest → session and the end it releases (the record's `Updated`), written only when set, apart
  from the stop's `Released` (`DriverConfig.cs:94-123`), which keeps its meaning. `WithoutQuest` (`:129-139`) drops it with the
  stop's when the quest closes. An older build does not read it, so it carries the record on as today (above).
- **No mark.** *(Round 3's read, R3-O9.)* Round 3's `GoOnMarks.TransientHeld` (`transient-held`) is withdrawn: at an end where
  nothing was prompted, the word rule's time (a word of the person's said after the record ended) holds the carried words with no
  write (§5.1).
- **No new uncertain note.** *(Round 3's read, R3-O2.)* An unacknowledged word is said by COWORK1c's `lost` note under its id
  (§5.1); round 3's `agent.transient-unacknowledged` is withdrawn.
- **Note codes** (kebab case, as `NoteCodesTests` holds; en and zh in the note catalogue, `src/Daoris.Web/src/work/noteLines.ts`).
  *(Round 2, O12.)* The zh says 重试, the glossary's *try again* (`src/Daoris.Web/src/locales/glossary.json:652`), never 再试, which it
  lists to avoid. *(Round 3, R2-O10.)* The product's name in running zh text is *Daoris*, as the glossary's `Daoris` entry keeps 道衍
  for the wordmark and 问道衍 (`locales/zh/agents.json` says *Daoris 驱动…*). *(Round 3, R2-C10.)* `{{count}}` and `{{minutes}}` in
  an end's sentence are the tries made and the minutes waited. In `agent.transient-retrying` they are the schedule's, said as
  *up to*:

  | Code | Where | English | 中文 |
  |---|---|---|---|
  | `agent.transient-retrying` | the conversation, once per episode, a driven session's and a chat's | *{{agent}}'s provider failed ({{words}}). Daoris tries again in this conversation: up to {{count}} times, after {{minutes}} minutes of waits between them, the next at {{time}}.* | *{{agent}} 的服务方出错（{{words}}）。Daoris 会在本对话中重试：最多 {{count}} 次，各次之间共等待 {{minutes}} 分钟，下一次在 {{time}}。* |
  | `ended.transient` | the record's note | Row 1a: *Its provider kept failing after {{count}} tries, with {{minutes}} minutes of waits between them ({{words}}). It waits for your words, which go on in the same conversation.* From row 1a2 (round 3's read, R3-O11), its last sentence: *It waits for you: your words, or Try again, go on in the same conversation.* *(Round 3, R2-C7; round 3's read, R3-O7.)* On the native door until row 1c records that its resume fails closed, the last sentence reads *It waits for you: your words go on in the same conversation; Try again goes on in a new part, briefed from this one.* | Row 1a: *它的服务方在 {{count}} 次重试后仍然出错，各次之间共等待 {{minutes}} 分钟（{{words}}）。它在等你的话，你的话会在同一对话中继续。* From row 1a2: *它在等你：你的话或"重试"会在同一对话中继续。* The native door's: *它在等你：你的话会在同一对话中继续；"重试"会在一个从这里简述而来的新部分中继续。* |
  | `held.transient` | the planner's sentence for a held quest | Row 1a: *Its last session's provider kept failing: it waits for your words.* From row 1a2: *Its last session's provider kept failing: it waits for your words or Try again.* | Row 1a: *它上一个会话的服务方一直出错：它在等你的话。* From row 1a2: *它上一个会话的服务方一直出错：它在等你的话或"重试"。* |
  | `chat.transient-stopped` | a conversation's line | *{{agent}}'s provider kept failing ({{words}}): your next words go on in this conversation.* | *{{agent}} 的服务方一直出错（{{words}}）：你接下来的话会在本对话中继续。* |
  | `helper.transient` | the asker's record | *Helper {{agent}} could not answer: its provider kept failing ({{words}}).* | *助手 {{agent}} 没能回答：它的服务方一直出错（{{words}}）。* |
  | `intake.transient` | the intake's record | *the agent's provider kept failing while it worked on ask `#{{ask}}` ({{words}}).* | *智能体处理请求 `#{{ask}}` 时，它的服务方一直出错（{{words}}）。* |
  | `partner.transient` | the lead's record | *Partner {{agent}} stopped: its provider kept failing ({{words}}). Its work is kept.* | *搭档 {{agent}} 停下了：它的服务方一直出错（{{words}}）。它的工作已保留。* |
  | `retry.go-on` | Daoris's word to the agent, on a try and on *Try again*'s release, never on the person's word (round 3's read, R3-O8) | *The last request failed on the provider's side ({{words}}). Go on where you stopped, after anything said above.* | the agent's word, English only, as `limit.reset` is |

  `{{words}}` is the door's own sentence, said as a program's (`NoteBy.Program`), never translated: authored content stays as written.
  The glossary's *try again* / 重试 widens its `means` to a record past its retries (row 1a2), beside D206's *Try now*, which is an
  account's.
- **The machine log** says `agent.error` once per failure read (session, adapter, account, kind, `By`) and `agent.transient` once per
  try (try, next). Neither is a note. *(Round 2, O10.)* The record's quiet `agent.transient` event (§5.1) is what the head reads.
- **The page.** A failed record with `ended.transient` reads *failed* in its dot, worded by its note, with no red: it waits on the
  person's word, not on a fault in the work. *(Round 3, R2-C10.)* `toTurns`' drop of the repeated `API Error` message and the
  re-sent go-on kept off the record are row 1a's (§5.1). *(Round 2, O10, O11, C13.)* The head's *Trying again at {{time}}* is drawn from the
  latest `agent.transient` event's `next` while it is in the future and no turn event follows it, so a try that works clears it and
  a later wait sets it again. *Try again* is offered on a failed record whose note is `ended.transient` and which is its quest's last
  record here (`work/acts.ts:157` offers `retry` today only to a parked record or a stopped one holding its quest), in en and zh, and
  its act is §5.1's release.

## 9. What it amends, each by a dated note on acceptance

- **D137's TURNFAIL1 note**: a transient failure is retried in its run and never takes the look's retry; a word once on the wire is
  never sent again, and after an episode TURNFAIL1 and D206 are handed only the words still held; past its bound the planner holds
  the line for the person, and only a word of the person's releases it.
- **D188's COWORK1c note** *(round 3's read, R3-O2)*: its rule is *only a restart makes a word uncertain*, and uncertain words leave
  by the take-back door under the `lost` note (`docs/decisions/D188.md:267-270`). A transient episode adds a second cause within
  one shell: a word of the person's whose prompt met the provider's error before any update of its turn, a head word and a try's
  blocks included, is uncertain. It is said by the same `lost` note under its id, offered as *Say it again*, and never sent again,
  so within one shell a word kept after a **transient** failed turn does not go again as TURNFAIL1's does. No new note is made.
  D188 point 6's *a word leaves `said` by id once it is on the wire* holds for Daoris's go-on too (R3-O5).
- **D125 point 1** and **D206 point 1** (§3.1's pipe door): the limit is read in `AgentErrors.Read`, first, from every door's
  failure, the pipe door's included; the table and the reset are unchanged; a limit's code with no sentence read is a limit with the
  unstated reset.
- **D80** (strikes, carry-on): `ended.transient` is no strike, and its quest is held, not carried on.
- **D126 §3.3** (the person's stop's hold and its *Try again*): *Try again* also releases a transient hold, by a release of its own
  keyed to the end it releases (`TransientReleased`, §5.1), never the stop's session-keyed one.
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
- **The stop's session-keyed release for the transient hold** *(round 3, R2-O1, R2-C3)*: the resume continues the same record, so a
  second exhaustion would still read released, and the line would cycle with no strike (§5.1).
- **Clearing the release as its start continues the record** *(round 3, R2-O1's other way)*: it needs a second write between the
  plan and `ContinueAsync`, and the order of that write against the resume decides whether a failure fails open. Keying the release
  to the end needs no write and survives a restart (§5.1).
- **Reading `Taken` false as nonacceptance** *(round 3, R2-C1)*: the mark is Daoris's bookkeeping, and an error does not say whether
  the word was appended. A word on the wire is never sent again (§5.1).
- **Any waiting word releasing the transient hold** *(round 3, R2-C4)*: a helper's answer or Daoris's own word would resume an
  exhausted line with nobody between (§5.1).
- ***Own or owner* for `ErrorWords`** *(round 3, R2-O5, R2-C6)*: an adapter's own entry would hide its owner's provider entries, and
  the install's 529 would read fatal (§4.1).
- **A temporary rate limit read transient by its sentence, in this delivery** *(round 3, R2-C9)*: §3.2 reads a 429 or `rate_limit`
  as a limit before any transient sentence, and no such sentence is recorded (§3.1).
- **An answered settling turn or a woken end ending the episode** *(round 3's read, R3-O1)*: neither shows that the agent went on
  with the failed turn, and the run would then park or strike (§5.1).
- **A note of its own for an unacknowledged word** *(round 3's read, R3-O2)*: it is COWORK1c's uncertain word, said by the `lost`
  note (§5.1, §9).
- **A mark for words an open never sent** *(round 3's read, R3-O9)*: a failed write beside the conclusion fails it open; the word
  rule's time needs no write (§5.1).

## 11. Rows, in build order, with their proofs

Every row runs its own tests and the classes covering the code it changes, filtered, and the census checks `docs/development.md`
names (*Choose the checks*). Each stand-in agent is a script under the tests, never the agent's binary (`node tools/live-logins.mjs`).
The backoff's tests hold the clock (`.claude/knowledge/held-clocks.md`). A fixture replayed from an adapter's source is marked
source-derived (§3.3). *(Round 2, O16, C14.)* Round 1's 1a is split: 1a is the smallest safe delivery, the protocol door's prompt
failure for a driven session with its hold; 1a2 carries shape 2, the open and the page. *(Round 3.)* Row 1a now also carries:
- the dispositions and their hand-over to every conclusion (R2-C1, R2-C2);
- the keyed release and the hold's word rule (R2-O1, R2-C3, R2-C4);
- the Open quest's way back and the act's case (R2-O3, R2-C5);
- the composed tables (R2-O5, R2-C6);
- the driven check, handed to driven sessions alone (R2-O6);
- the heard stamp (R2-O7);
- the settling turns (R2-O8);
- the hand-over passing the hold (R2-O4);
- the minimum suppression (R2-C10).

A failure at the open keeps today's end until 1a2 (R2-O2).

*(Round 3's read.)* Its named changes move these:
- *Try again*'s keyed release (`TransientReleased`, `TransientReleasedFrom` for a released record, `Retry`'s case, `LimitResume.Line`
  and `DaorisKeys.TransientRelease`) and their proofs go to 1a2 beside its press (R3-O11). 1a's release is then the person's word
  and the hand-over, both loop-free, and every sentence of 1a's names only those.
- 1a carries the settling and woken ends that send the go-on (R3-O1), the `lost` note in place of a new one (R3-O2), the hand-over
  carried on in an Open quest's tree (R3-O3) and the go-on through the dispatcher (R3-O5).
- 1a2's open end needs no mark (R3-O9), 1b's limit leaves out the failed turn's own words (R3-O6), and 1c's native note splits the
  two releases (R3-O7).

| Row | What | After | Proof |
|---|---|---|---|
| **AGENTERRORS1a** | The kinds and the reader at the interface (`DoorFailure`, `AgentError`, `AgentErrors.Read` taking the adapter set, `ErrorWords` read own then owner with `AuthRequiredCode` never inherited, §4.4's first entries on `claude-code` and `claude-code-acp`); the protocol door's prompt failure (`Where: prompt`) for a **driven session**, the `TransientRetry` handed at `Driver.cs:2683` and `DriverContinuations.cs:431` alone; §5.1's episode inside `AcpSession.RunAsync` (the seat, the settled wire and its settling turns, an answered settling turn or a woken end sending the go-on at once as a try, the three dispositions read before `Answered` removes a prompt and handed to every conclusion, an unacknowledged word said by COWORK1c's `lost` note, words held during the wait, one prompt a try of held words, the wake gate, the driven check `TransientRetry.Wanted`, an unread check a yes, `WaitsOnTransient`, the wait's end stamped heard); the limit's code with the unstated reset (§3.1); `ended.transient` with the actual tries and waits, `ReadStrikes`, every word on the wire off at every conclusion, Daoris's go-on through the run's dispatcher, off once on the wire, with the take-off at both conclusions, a first run's and a continuation's, as the fallback; the planner's hold (`TransientHolding`, `StartVerdict.TransientHeld`, `held.transient`) after `WordsOn` and `StopHolding` and before every other path, passing a hand-over from its record and carrying an Open quest's on in its tree (`TransientReleasedFrom` for the hand-over's record, step 0), its word rule (a person's unmarked word said after the record ended), the person's word sending their words alone; the minimum suppression (`toTurns`' drop and the re-sent go-on off the record); `agent.transient-retrying`, `ended.transient` and `held.transient` in 1a's words, and `retry.go-on`, in en and zh. Access and fatal keep today's ends; a failure at the open keeps today's end; *Try again*'s keyed release is 1a2's | — | The reader's table both ways (`AgentErrorsTests`, a fast class), `-32000` read only where declared: an authentication refusal and an unrelated failure with the same code on the stub. A stand-in ACP agent answering its prompt `-32603` *Internal error: API Error: 529 Overloaded. …* with `data.errorKind: "overloaded"` (source-derived) twice, then working: retried in the **same** conversation (one record, the same session id, no `moved.on`), it goes on and ends as its work does, with one `agent.transient-retrying` line, no strike and no `cooling.json` entry, red first against today's `ended.turn-failed-taken`. A stand-in that records the person's words and makes a visible effect, then fails: the stand-in receives each word **once** (one `user` event, one receipt), and neither the instruction nor the effect is replayed. A word said during a wait goes at once, in one prompt with the go-on after it. A woken turn during a wait ends the wait, not the episode: no prompt is sent into it, and the go-on follows once after it ends. The quest done before a try: no try. The watch during a wait: no episode, no quiet end. The same agent erring every try: past the bound `failed`, `ended.transient`, no word waiting, the go-on off, no strike, no cool-off, no park; then **the next looks** (a tick each, with and without commits in the tree) start nothing for an Open and for a Taken quest, until the person's word resumes the **same conversation id** by MSG1b's go-on, with their words alone and no go-on after them. A limit on a try: cooled once, D206 from there. A weekly limit's sentence under `errorKind: "rate_limit"`: limited, never retried; `rate_limit` with no sentence: limited an hour. Shutdown during a wait ends as today's shutdown. *(Round 3.)* **The tables compose**: `claude-code-acp`, with its own `-32000` declaration, still reads its owner's `overloaded`, 529 sentence and `rate_limit` through the real adapter set; a plugin with `accountOf: claude-code` reads them too but never the `-32000` declaration. **Nonacceptance**: a stand-in that appends a queued word to its conversation and then answers its prompt with the error, with no update first: the word is never sent again, and the `lost` note is said once under its id; so is a head word whose `Taken` mark the order of answers set, its prompt meeting the error before any update. *(Round 3's read.)* **A settling turn**: a queued word's settling turn answers `end_turn` on a taken quest, the go-on follows once, and the run ends as its work does, never `awaiting-person`; on an Open quest, never `ended.untouched`. One that fails transient counts as a try. **The go-on off**: a try answers and the run then parks, and the next look starts nothing, on a first run and on a continuation. **Dispositions across kinds**: transient → limit → the account's reset resumes its own conversation; transient → limit → a briefed move; transient → fatal → TURNFAIL1's next look. Each asserts one receipt of each original instruction. **The release**: exhausted → the person's word → the resumed run exhausted again → several looks, then a driver restart and more looks: nothing starts. **The word rule**: a helper's answer, a Daoris word and a word of the person's said before the end, kept on the exhausted record, start nothing at the next looks, then travel once with the person's word said after it. **The hand-over**: from the held record of an **Open quest with no commits**, it passes the hold and goes on in the quest's tree, never a fresh start. **The driven check**: an Open quest that failed before its take is tried; a quest submitted just before a try is not; an unreadable quest is tried. A run that is not a driven session's (an intake's stand-in) keeps today's end. **The watch**: a silent try after the 16-minute wait under a held clock opens no quiet end until its own quiet has passed from its prompt. **The rendered conversation** (`toTurns` over the record): the provider's error shown once, the go-on once, and one `agent.transient-retrying` line, through three failed tries. **The words**: `ended.transient` names the tries made and the minutes waited after an early try sent by the person's word. `NoteCodesTests`, `i18n:check`, `names:check --strict` |
| **AGENTERRORS1a2** | Shape 2 (§4.2: a mid-turn word's prompt refused after it was taken is the turn's prompt failing); the open (§5.6), by the raw operation, never `OpenAsync`/`ResumeAsync` again (`Acp.cs:1159`), its *nothing prompted* end (Daoris's go-on off, the person's words held by the word rule's time, no mark); *(round 3's read, R3-O11)* *Try again*'s keyed release beside its press (`DriverConfig.TransientReleased` keyed to the end, `TransientReleasedFrom` widened to a released record, `StandingChoices.Retry`'s case, `LimitResume.Line` and `DaorisKeys.TransientRelease` for its step-1 start), and `ended.transient` and `held.transient` widened to name it; the page: the head's *Trying again at {{time}}* from `agent.transient`, *Try again* offered on an `ended.transient` record (`acts.ts`) in en and zh, the glossary's *try again* widened | 1a | Shape 2 both ways round: a mid-turn word refused with the 529 after the prompt before it answered (taken: retried, never `awaiting-person`, the word received once), and one refused before it was taken while the running turn then ends well (the word's note, the turn clean). A refused steer (`_session/steering`) stays its note. An unrelated update after the refusal changes nothing. The open: a transient `session/new` and `session/resume` failure repeated, then the original prompt sent once, before and after a session id exists; D205's model refusal unchanged. *(Round 3, R2-O2; round 3's read, R3-O9.)* A resume whose open fails past the bound: the go-on off, no mark written, then the next looks start nothing, until a new word of theirs, said after the end, goes on with the carried ones; the same with the conclusion's every side write failing. *(Moved from 1a, R3-O11.)* **The release**: exhausted → *Try again* once → the resumed run exhausted again → several looks, then a driver restart and more looks: nothing starts. An **Open quest with no commits** released resumes its original conversation id, with no fresh tree and no replay of its target. *Try again* offered and pressed on the page, in en and zh. The head through several waits, a try that works, a stop and the bound, in en and zh (`limitWait`'s and the head's tests, a story) |
| **AGENTERRORS1b** | §5.1 and §7 for **conversations**, Ask Daoris and a repository's chat, on the protocol door: the wait, words said meanwhile, `chat.transient-stopped` | 1a | A stand-in conversation's turn failing transient twice then answering: one turn (failed tries neither release the queue, `ChatTurns.TurnEnded`, nor move the curve, notify or move a limit), the same process, the person's words once; a word said during the wait goes at once and first, one prompt with the go-on; a limit on a try goes to LIMITGOON1/2 once, and `LimitHeld.Of` holds only the words no try carried (round 3, R2-C2) and never the failed turn's own (round 3's read, R3-O6), each original word received once at the limit's wake or move; erring every try ends the turn `error` with `chat.transient-stopped`, and the next word is a turn of the same conversation (`ChatGoesOnProcessTests` or a class of its own); the page's line in en and zh |
| **AGENTERRORS1c** | The **pipe door**: `ClaudeStreamJson.Result` reads `api_error_status` and the harness's `error` field into a `DoorFailure`, the conclusion's `turnFailed` on this door (D206 row 1f's gap: limits and sign-ins read here too), D205's model refusal read first; the **driven native run**'s retry by `--resume` with `--model` and effort; *(round 3, R2-C7)* first, a real-door measurement of `--resume` of a captured id that is gone, and on its answer either the step-1 guard (`DriverContinuations.cs:58`) widened for the transient release or the native *Try again* briefed and said so, the note's clause splitting it from the person's word, which goes on in the same conversation by `ResumesOwn` (§5.1, §8; round 3's read, R3-O7); and, after 1b, the **live native conversation**'s retry on its process and `NativeChat`'s reading of a failed result | 1a; 1b for `NativeChat` alone | The measured 404 frame (`ClaudeStreamJsonTests.cs:191-212`) and the failed 429 result, whose fixture this row adds from `model-effort-doors-evidence.md:186-204` (round 3, R2-C11); the missing-conversation measurement, recorded; after exhaustion and the release, the existing id resumed, and a gone id taking the named fallback, neither replaying an accepted instruction; a 5xx frame recorded on the real door, or replayed from the SDK's declaration marked source-derived. A stand-in native agent erring twice then working is resumed with `--resume` and the same `--model` in the same record and goes on; one that keeps erring ends `ended.transient` and is held; its limit sentence cools its account; `model_not_found` on a silent turn stays D205's (`NativeResumeTests`, a native tick class). A missing id, and an end before `init`: no try, today's end. A live native chat retries on its own process, never spawning `--resume` beside it |
| **AGENTERRORS1d** | §7's **other kinds of run** past the bound and at their minutes: helper (`HelperWhy.Transient`), opinion (`transient` why and gate), intake (one unbind, then bound), partner, brainstorm; each owner's check (§5.5); the one-turn native pass's episode-local id | 1a; 1c wherever it promises the native door | Each kind's stand-in erring every try ends as §7 says, its minutes ending inside an episode with the transient reason, never *timed out*; erring twice then working goes on in its run; a helper's answer stored, an intake's quests published and an opinion recorded just before the provider failed: no try, today's end; *(round 3, R2-C8)* an ask closed without new quests, and an ask published by somebody else, each just before the failure: no try, the original stand-down (`HelperLookTests`, `OpinionLookTests`, `IntakeTests`, the partners' and brainstorms' classes) |
| **AGENTERRORS1e** | **Access** on every door (§5.3): `-32000` where declared, `AccountRefused` through the reader so `claude-code-acp` reads its owner's `Refused`, the SDK's access kinds, Codex's `unauthorized` tied to its holder's accounts, the harness's lines kept as a source | 1a; 1c for the pipe door | A stand-in of each door refusing with its access error: the account marked, no strike, no retry, the next start walks past it, and with none ready the start is held in today's sentence (`SignInRefusalsTests`, `AccountRotationTickTests`). A stderr-only credential refusal still marks the account, and so does one said on stdout outside the protocol (round 3, R2-C11); an ordinary stderr failure does not; an agent message quoting the credential's sentence does not |
| **AGENTERRORS1f** | **`codex-acp`'s** codes (§3.1, §4.3): record its frames on the real door (a usage limit, a dropped stream with `willRetry`, a terminal overload, the typed `sessionFailure` marker once declared), then read `codexErrorInfo` and `category` | 1a | The recorded frames, held both ways; a stand-in `codex-acp` erring twice then working retried in the same conversation. By the typed marker: a direct terminal failure, retries that recovered, a successful empty ending, a cancel, retries exhausted, and its access and request categories, each read as its kind. *(Round 3, Codex's row note.)* 1a's disposition and release proofs again on this door |
| **AGENTERRORS1g** | The install's proof | 1a-1f | On the installed window, a **controlled** stand-in agent installed as a plugin harness declaring `accountOf: claude-code` (round 3, R2-O9: a plugin with no owner reads nothing by code), so it reads its owner's table, that answers transient twice then works, and one that keeps erring, read as §5.1 says: one line, the same conversation, no strike, the hold and its release; the head's line seen in en and zh. Then the next real provider error the install's machine log shows, as later evidence: its date is open, and the backlog says so |

The first row gives the most: it is the install's door, its measured shape 1 and every driven session.

## 12. What the checks do not cover

- A document. The code was read at `64059110a`, again on main at `fb3eb89eb`, where no cited file changed, and for round 3 on main
  at `aa16658e7`, where only `ChatRunner.cs` moved (its citations are main's); the adapters'
  behaviour is read from their source in the install's toolchain cache, `claude-agent-acp` 0.84.0 and `codex-acp` 2.1.1, and an
  adapter's update can move it. Shapes 1 and 2 are measured, from the install's records; `data.errorKind` on shape 1 is read from the
  adapter's source, since the record keeps only the message.
- *(Round 2, O14.)* The pipe door's `api_error_status` and the harness's `error` field are measured for a model refusal (404) and a
  limit (429) by MODELROLE1a; a 5xx on that door is not.
- Codex's terminal errors are unmeasured, and until row 1f a Codex turn ended on one reads as it does today: with its quest taken it
  parks quoting the error, as shape 2 did.
- A dsh harness, and a plugin with no owner, read nothing by code; their errors stay fatal until they declare a table. *(Round 3,
  R2-O9.)* A plugin that declares `accountOf` reads its owner's provider entries, never its owner's `-32000` declaration.
- *(Round 3, R2-C7, R2-O3's note.)* D206's step 1 is the protocol door's (`DriverContinuations.cs:58`). Until row 1c measures the
  native door's `--resume` of a missing conversation, a native `ended.transient` release goes briefed, and its note says so.
- *(Round 3, R2-C1.)* Whether an adapter appended a word whose prompt answered an error is not known on any door. Daoris never
  sends such a word again and says it under COWORK1c's `lost` note (round 3's read, R3-O2). If the agent never read it, it is lost to the agent until the person says it
  again, which is the cost of never repeating an instruction.
- *(Round 3, the Opus read's note on R2.)* A request-bound 5xx read transient loses today's self-healing briefed carry-on. It now
  needs the person after the bound, whose way out is their word or *Go on now with…*, and from row 1a2 *Try again*; the hold passes a
  hand-over (§5.1).
- *(Round 3's read, R3-O10.)* Every 429 or `rate_limit` is now a limit (§3.2). A provider's per-minute rate limit therefore cools its
  account for D125's hour (`AccountLimits.DefaultCoolOff`) and the line moves or waits, where today the same failure is a strike and
  a briefed carry-on a minute later. That holds until a later row records a temporary reading (§3.1).
- How long real outages last is not measured. The bound's 31 minutes of waits is a reading of the two records (each overload passed
  within the half hour: the person's go-on at 01:02 worked, and `63d56294` ran at 00:31) and of the harness's own retries before it.
- The prompt cache: a try after the 8- or 16-minute wait likely re-reads the whole context (219,284 tokens in shape 1), unmeasured.

## 13. The reads

Round 1 was read at `6f089fe1f` by Codex (*another round*: 14 findings, 12 blocking) and by the independent Opus reader as a
foundation (*accept after named changes*: 16 findings, 1-3 blocking, 4-6 to take before acceptance), each kept verbatim in
`docs/2026-10-24-agent-errors-reads.md`. §14 answers every finding.

Round 2 was read at `f23020f80` by Codex (*another round*: 11 findings, 8 blocking, keeping the four-kind interface, the
conservative limit reading and the ACP driven-session first delivery) and by the independent Opus reader as a foundation (*accept
after named changes*: 11 findings, 2 blocking, and 3, 5 and 6 to write in before row 1a is built), kept verbatim in the same file.
§15 answers every finding.

Round 3 was read at `3086e44f1` by the independent Opus reader alone as a foundation (*accept after named changes* the steward
checks without another read: 11 findings, 1 blocking, and 2, 3 and 5 to write in before row 1a is built), kept verbatim in the same
file. Codex's read of round 3 is owed, its credits having run out. §16 says where each finding went.

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

## 15. Round 3: round 2's reads answered

Every finding of both reads of round 2, checked against the code on main at `aa16658e7`. `R2-O` is the Opus foundation read's
numbered finding and `R2-C` Codex's; *taken*, *answered* and *deferred* mean what §14 says. Where the two reads agree, one row
takes both. Both reads' tables of round 1's findings are answered by the rows their open entries point to (last rows below).

| Finding | Taken, answered or deferred | Where |
|---|---|---|
| **R2-O1**, blocking, with **R2-C3**, blocking, agreed: one *Try again* releases every later exhaustion of the same record | Taken, keyed to the end: `DriverConfig.TransientReleased`, quest → session and the record's `Updated` as the press read it, passed only while both still equal the record's. The released resume continues the same record, so its next end moves `Updated` and a second `ended.transient` is held again, with no write to consume, across a restart; an unsaid `Updated` fails closed. Daoris's go-on for the release keyed by the same end, `DaorisKeys.TransientRelease(session, ended)`. The stop's `Released` stays its own. Clearing the release at the start is rejected (§10). Checked: `Released` and its rationale (`DriverConfig.cs:94-123`, `WithoutQuest` `:129-139`), `StopHolding` (`Planner.cs:1459-1467`), the step-1 start continuing the record (`Driver.cs:2267-2288`, `DriverContinuations.cs:226`, `:272-274`), `PriorSession.Updated` (`Planner.cs:333-336`). Row 1a's proof: exhaust, press once, exhaust again, several looks and a restart, nothing starts | §5.1, §8, §9, §10; row 1a |
| **R2-O2**, blocking: words an open never sent go on ahead of the hold | Taken as named: at an `ended.transient` end with nothing prompted, Daoris's go-on comes off by id and the person's carried words are marked `GoOnMarks.TransientHeld`, which `Waits` honours and their next word releases; until row 1a2 lands, the open makes no `DoorFailure` and a failure there keeps today's end. Its *say also* is taken by R2-C4's word rule: a non-person word does not release the hold. Checked: `DriverContinuations.cs:699-707`, `:717`; `WordsWaiting` (`Planner.cs:355`); `WordsOn` before the hold (`:1325-1329`); `Waits` (`:1503-1505`); `GoOnMarks.Judged` (`GoOnMarks.cs:131-134`). Row 1a2's proof: an open failing past the bound, then the next looks start nothing | §4.2, §5.1, §5.6; rows 1a, 1a2 |
| **R2-O3**, with **R2-C5**, blocking: an Open quest's release has no way back to its record, and the act no case | Taken (to write in before 1a): `TransientReleasedFrom(quest)` answers the released record ahead of `RetriedFrom`'s commit rule, planned `CarryOn(quest, last)` with or without commits; `StandingChoices.Retry` gains `TransientHeld` → `RetryDecision.Released` with `WithTransientReleased`. Its note taken: step 1 is the protocol door's, so a native release goes briefed until row 1c (§12). Checked: `RetriedFrom` (`Planner.cs:1391-1397`), `LimitedFrom` (`:1400-1405`), `Consider` (`:1355-1366`), `Retry`'s cases (`StandingChoices.cs:108-131`), `LimitResumes` (`DriverContinuations.cs:54-62`). Row 1a's proof: a commitless Open quest resumes its original conversation id, no fresh tree, no replay | §5.1, §12; row 1a |
| **R2-O4**: *Go on now with…* and the awaiting resume pass the hold unsaid | Taken: the hold passes a hand-over from its record as `StopHolding` does (`HandedFrom`, `Planner.cs:1455-1456`, `:1465`); it sits directly after `WordsOn` and `StopHolding`, before the awaiting resume (`:1367-1371`) as well; for an awaiting quest *Try again* is the release | §5.1; row 1a |
| **R2-O5**, with **R2-C6**, blocking, agreed: `ErrorWords`' owner fallback hides `claude-code`'s table | Taken (to write in before 1a), both of R2-O5's ways at once: the reader reads the adapter's own `ErrorWords` and then its owner's, own first, and the `-32000` declaration is its own field, `AuthRequiredCode`, never inherited. Provider entries sit on `claude-code`, and `By` names the table. Checked: `LimitsOf`/`SignInOf` *own or owner* (`HarnessRoster.cs:120-144`), `ClaudeAcpAdapter`'s `AccountOf: "claude-code"` (`Adapters.cs:1930`; R2-O5 cited `:1912`). Row 1a's proof: `claude-code-acp` with its own declaration still reads the inherited `overloaded`, 529 sentence and `rate_limit` | §4.1, §4.4; row 1a |
| **R2-O6**: the driven owner's check, and which runs 1a retries | Taken (to write in before 1a) as named: `TransientRetry.Wanted`, its own predicate, under which an Open quest or one this session holds goes on; an unreadable check is a yes; a run handed no `TransientRetry` keeps today's end, and 1a hands one only at `Driver.cs:2683` and `DriverContinuations.cs:431`. Checked: `QuestMovedAsync`'s delegate is `Observation.Parks` (`Driver.cs:3445-3460`, `Observation.cs:216-219`), `Taken` by this session; the shared capture (`Driver.cs:3685-3693`) | §5.1, §5.5; row 1a |
| **R2-O7**: the quiet clock at a try after a long wait | Taken: the wait's end stamps the session heard; 1a's held-clock proof covers a silent try after the 16-minute wait. Checked: `Heard` (`SessionWatch.cs:173-176`), the quiet end (`:342-348`) | §5.1; row 1a |
| **R2-O8**: the settling step's turns are unplaced | Taken as named: an answered settling turn ends the episode with no try; one failing transient is that failure and counts as a try; the wording of an unacknowledged word no longer says *the agent never read it*. Checked against the adapter's source as R2-O8 read it (`acp-agent.js:1257`, `:2750-2778`) and `TakeLate` (`Acp.cs:1697-1712`) | §5.1; row 1a |
| **R2-O9**: row 1g's plugin stand-in cannot read transient | Taken: 1g's stand-in declares `accountOf: claude-code`; §4.1 and §12 say *a plugin with no owner* reads nothing by code, and a plugin with an owner never inherits its `-32000` declaration. Checked: `DeclaredAcpAdapter.cs:46-59`, `:52` | §4.1, §12; row 1g |
| **R2-O10**: the zh product name in running text | Taken: *Daoris 会在本对话中重试*; 重试 kept, never 再试 | §8 |
| **R2-O11**: D210 point 3's wording of the release | Taken: *the person's word (MSG1b's go-on) or Try again (D206's step-1 resume)*, with the release keyed to its end | D210 point 3 |
| **R2-C1**, blocking: settling responses does not prove nonacceptance | Taken: three dispositions, `held`, `taken` and `unacknowledged`, read by request id before `Answered` removes the prompt; only held words are ever sent by a try; an unacknowledged word is never sent again, and the person is told once (`agent.transient-unacknowledged`); adapter-proven nonacceptance only by a recorded frame, declared by none today. Checked: `Taken` is bookkeeping (`Acp.cs:429`), set by order (`:1667`) or update (`:1697`, `:1944`), and removed at `:1662`. Row 1a's proof: a stand-in that appends a queued word, then errs with no handoff: never sent again | §5.1, §8, §10, §12; row 1a |
| **R2-C2**, blocking: a later limit or fatal end resurrects accepted words | Taken: the episode hands every conclusion its dispositions, whatever the kind, and every taken or unacknowledged word comes off as taken; `FailedTurnWords.Judge`, D206's `LimitWait` and TURNFAIL1 see only held words; a run with no episode keeps today's judgement. For conversations, each try's quiet event names its words and `LimitHeld.Of` leaves them out (row 1b). Checked: `DriverContinuations.cs:699-725`, `Observation.cs:503`, `LimitHeld.cs:17-45`. Proofs in 1a: transient → limit → own-account reset, → briefed move, transient → fatal, one receipt each; 1b the conversation's | §4.1, §5.1, §6.1, §6.3, §9; rows 1a, 1b |
| **R2-C3**, blocking: one *Try again* releases every later exhaustion | Taken: with R2-O1 | §5.1 |
| **R2-C4**, blocking: `WordsOn` releases the hold for another agent's or Daoris's words | Taken: for a record the hold would hold and no release passes, `Waits` also needs a waiting word of the person's (`SaidWordView.Persons`, `Planner.cs:675`) not judged by a mark; with one, `GoOn` takes every waiting word, so a helper's answer or a Daoris word travels once with it. Checked: `Waits` (`Planner.cs:1503-1505`), a helper's answer handed as the next turn (`DriverHelpers.cs:776-810`). Row 1a's proof: a helper's answer and a Daoris word after exhaustion start nothing, then travel once with the person's word | §5.1, §10; row 1a |
| **R2-C5**, blocking: a commitless Open quest's release does not select its old conversation | Taken: with R2-O3 | §5.1 |
| **R2-C6**, blocking: the first tables and the inheritance rule do not compose | Taken: with R2-O5 | §4.1 |
| **R2-C7**, blocking: native recovery after exhaustion meets the ACP-only guard | Taken: row 1c owns it, preceded by a real-door measurement of `--resume` of a captured id that is gone. Where that fails closed, 1c widens the guard for this release and proves the resume of the existing id and the fallback for a gone one. Where it does not, the native release is **stated as not offered** in the same conversation: it goes briefed, and its note says so. Checked: `LimitResumes` (`DriverContinuations.cs:54-62`), `Driver.cs:1970` | §5.1, §8, §12; row 1c |
| **R2-C8**, blocking: the intake check omits authoritative endings | Taken: the check is `IntakeObservation.Conclude` itself against the run's start baseline, so a deleted, published, closed or otherwise answered ask stops the try. Checked: `Intake.cs:295-331` (closed `:320`, answered by somebody else `:326`). Row 1d's proof: closure without new quests, and publication by somebody else, each just before the failure | §5.5, §7; row 1d |
| **R2-C9**: the precedence contradicts the temporary-rate-limit exception | Taken, its first way: the exception is withdrawn for this delivery, and a later row may add it only as a positively recorded reading placed after the table's limit sentences and before the code's limit; the *no code alone* sentence corrected, and both sides held in `AgentErrorsTests` | §3.1, §3.2, §10 |
| **R2-C10**: row 1a's proof does not establish once-only display | Taken: the minimum suppression (the drop of the repeated `API Error` message and the re-sent go-on off the record) moves into 1a, whose proof asserts the rendered conversation shows the error once, the go-on once and one line; the head's display stays 1a2's; the end's wording uses the tries made and the minutes waited, and a pass's deadline is worded apart | §5.1, §7, §8; rows 1a, 1a2 |
| **R2-C11**: citations and source descriptions overstate | Taken: the conversation's ACP catch is `ChatRunner.cs:2857` (at `f23020f80` and on main; Codex cited `:2837`); the failed 429 fixture is the evidence document's, added in 1c; trusted harness lines include stdout outside the protocol (`HarnessCapture.cs:159`, `Observation.cs:618`), proved in 1e; watcher claims scoped to driven sessions and intakes (`Driver.cs:3566`), conversations (`ChatRunner.cs:2425`) and passes said apart | §1.1, §1.2, §3.3, §4.2, §5.1; rows 1c, 1e |
| Codex §1's claims table (`Taken` false; `:3048`; the 429 fixture; stderr only; *watched as any turn*) | Taken: R2-C1 and R2-C11 | as those rows |
| Codex §2: classification, disposition and release collapse together | Taken: dispositions outlive the kind (R2-C2), the release is keyed to its end (R2-C3), and machine words never release (R2-C4); conversations' `LimitHeld` (R2-C2); stored answers and opinions stay authoritative (§5.5); an intake closed or answered elsewhere stops (R2-C8); five tries an attempt bound, never an elapsed promise, said per kind of run (§5.1) | §5.1, §5.5 |
| Codex §3's row notes | Taken: 1a carries the disposition contract, the release lifecycle, the minimum suppression, a second exhaustion and a machine word while held; 1a2 repeats the raw operation, never `OpenAsync`/`ResumeAsync` again (`Acp.cs:1159`); 1b's `LimitHeld`; 1c's missing-conversation measurement and native release proof; 1d's intake closure and foreign publication; 1e both stderr and outside-protocol stdout; 1f 1a's disposition and release proofs again; 1g the stand-in's owner | §5.6, §11 |
| Opus §2's wrong claims and new holes; *rows whose proof cannot pass* | Taken: each is one of R2-O1 to R2-O9, answered above; D210's body corrected with them | as those rows; D210 |
| Codex §0 and Opus §1, round 1's findings still open (C3, C4, C5, C7, C8, C14, O2, O3, O14, O16; C4's *different reason*, C8's Open quest, Codex §2's composition) | Taken by the round-3 rows they point to: R2-C1 and R2-O8 (C3, C4, O3), R2-O1, R2-O2, R2-O3, R2-C4 and R2-C5 (C5, O2, Codex §2), R2-C7 (C7), R2-C8 and R2-O6 (C8), R2-C10 and R2-O9 (C14, O16), R2-C11 (O14) | as those rows |

## 16. Round 3's read: where each finding went

Each finding of the Opus foundation read of round 3 (`R3-O1` is its finding 1), taken as the read names it and checked against the
code on main at `ffcf42926`, whose cited files are the read's `8cc3936b0`'s. Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`
unless they say otherwise.

| Finding | Where it went, and the sentence | Checked |
|---|---|---|
| **R3-O1**, blocking: an answered settling turn or a woken turn ends the episode with no go-on | §5.1, *The wire settles first* and *Through the wake gate*: *a settling turn that answers ends the wait, not the episode*, and likewise *a woken turn's end during an episode*; the owner's check is asked, and *where another turn is still wanted Daoris's go-on goes at once, with no wait, and counts as a try*. D210 point 3's settling clause; row 1a's proof: *a queued word's settling turn answers `end_turn` on a taken quest, the go-on follows once, and the run ends as its work does, never `awaiting-person`*, and the woken turn the same; §10 | `Acp.cs:841` (`NextOrClose` finds nothing and the loop breaks); `Acp.cs:1039-1046` (every woken end emitted as `WokenStop`); `Observation.cs:173-174` (a clean exit with the quest taken parks, `awaiting-person`); `:205-206` (an open quest's clean exit is `ended.untouched`) |
| **R3-O2**: *unacknowledged* reinvents COWORK1c's uncertain word | §5.1, *What a word's disposition is*: *an unacknowledged word is D188's COWORK1c uncertain word*, said by the `lost` note under its id, read from the turn and never from the `Taken` mark alone, so a head word and a try's blocks are included; `agent.transient-unacknowledged` withdrawn (§8); D188's COWORK1c note added to §9 and to D210's amendments; §10; row 1a's proof | `SessionEvents.cs:70-75` (`Lost`); `Conversations/GoOn/UncertainWords.cs:84-90` (`SettleOneAsync` keeps the note under the unseen words' ids); `docs/decisions/D188.md:267-270` (*only a restart makes a word uncertain*) |
| **R3-O3**: the hold passes an Open quest's hand-over into a fresh start | §5.1, *The planner holds the quest* and *The next look plans the line's carry-on*: `TransientReleasedFrom` *answers the held record a hand-over was made from (`HandedFrom`)*, planned `CarryOn`, *so Go on now with… is step 0 as D206's is*; row 1a's proof uses a commitless Open quest | `Quests/Domain/Planner.cs:1355-1366` (`Consider`'s fresh start), `:1400-1405` (`LimitedFrom`), `:1455-1456` (`HandedFrom`) |
| **R3-O4**: a word kept between the press and the start does not move `Updated` | §5.1, *Try again*: *a word kept meanwhile leaves `Updated` as it is, so the release still passes, and the start goes on with that word …; only the record's own end moves it* | `src/Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:1554-1566` (`updated` written only with a note), `:1590-1596` (`TakeSaidAsync` writes none) |
| **R3-O5**: Daoris's go-on waits on `said` for the episode, and no first run takes it off | §5.1, *Each try is one prompt*: *the go-on leaves the record once it is on the wire … through the run's one dispatcher (`SaidDoor.Dispatched`)*; *What waits on the record*: the conclusion's take-off *as the fallback, at both conclusions*, a first run's included; §9's D188 note; row 1a's proof: *a try answers and the run then parks, and the next look starts nothing* | `docs/decisions/D188.md:52`; `Conversations/SaidDoor.cs:69`; `Loop/DriverContinuations.cs:702-707` (only `LimitResume.WordIds(resume.Words)`); `Loop/Driver.cs:2304` (the first run's only take-off, the refused step 1's); `Conversations/DrivenInbox.cs:908-913` |
| **R3-O6**: a conversation's limit after an episode re-sends the failed turn's own words | §6.1, *Conversations*: *the episode's first `agent.transient` event also names the ids of the failed turn's own words*; row 1b's proof | `Conversations/Domain/LimitHeld.cs:27-30` (every person's `User` event collected) |
| **R3-O7**: the native release note conflates the two releases | §5.1, *Step 1 is the protocol door's*: *That is Try again's release alone*; §8's `ended.transient` native clause: *your words go on in the same conversation; Try again goes on in a new part, briefed from this one*; §12; row 1c; D210 point 7 | `Loop/DriverContinuations.cs:40-45` (`ResumesOwn`, no wire guard), `:54-62` (`LimitResumes`, ACP alone) |
| **R3-O8**: the go-on, placed last, can countermand the person's words | §5.1, *their word*: *It sends their words alone, as MSG1b does for any ended record*; *Each try is one prompt* and §8's `retry.go-on`: *Go on where you stopped, after anything said above* | `Planner.cs:1325-1329` (`WordsOn`'s `GoOn` takes the record's waiting words) |
| **R3-O9**: 1a2's `transient-held` mark fails open on a failed write | §5.1, *Nothing prompted* and *The hold's word rule*: the person's word is one *said after the record ended: its `At` later than the record's `Updated`, both read from the look's snapshot*, and no mark is written; §5.6; §8 (*No mark*); §10; row 1a2's proof with the side writes failing | `Planner.cs:1503-1505` (`Waits`), `:675` (`Persons`); `SaidWordView` carries `At` (`Conversations/Chat/LimitWaits.cs:231`); `Conversations/GoOn/GoOnMarks.cs:131-134` |
| **R3-O10**: the cost of reading every 429 as a limit is unstated | §12: *a provider's per-minute rate limit therefore cools its account for D125's hour … where today the same failure is a strike and a briefed carry-on a minute later* | `AccountLimits.DefaultCoolOff` (`Accounts/Usage/AccountLimits.cs:79`) |
| **R3-O11**: row 1a carries *Try again*'s release without its press | **Moved to 1a2** (§11's note, rows 1a and 1a2; §5.1, §8): `TransientReleased`, `TransientReleasedFrom` for a released record, `Retry`'s case and their proofs go beside the press; 1a's `ended.transient` and `held.transient` say *It waits for your words*. Why this way and not the offer into 1a: the person's word and the hand-over already release a held record loop-free, the word by `WordsOn`'s `GoOn` (`Planner.cs:1325-1329`) and the hand-over by `HandedFrom`, so 1a strands nothing and stays the fastest safe delivery; the release and its press are one feature with one proof, and in 1a the release would be reachable only from the CLI and the bridge, an act the page does not offer | `src/Daoris.Web/src/work/acts.ts:157` (`retry` offered only to a parked or a stopped record); `Platform/DriverChoicesCommand.cs:1150`; `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/DriverRoutes.cs:424` |
