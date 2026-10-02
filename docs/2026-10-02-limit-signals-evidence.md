# TOOL4b: what each door says about an account's limits (evidence note)

**Carried by:** D125 (point 1, design §1.2) and D130 (TOOL4b as D130 amends it; design §0.3, §5.1, §5.2).
TOOL6c, which chooses by the allowance each account has left, and any Codex entry in D125's table build
only on what this note records. It is a record of the versions in §0, not a contract: a harness that
moves is read again.

> Written 2026-10-02, **keylessly but for one turn**: one short headless Claude Code turn ran on the
> machine's own sign-in, in a scratch folder with no instruction file in or above it, with
> `--setting-sources project --strict-mcp-config` (§1.1). Everything else was read from shipped code,
> the SDK's declarations and the makers' pages. No limit was met. No Daoris install, home or process
> was touched, and no Daoris door was driven. No account is named here, and every moment taken from a
> frame is written relative to when it was seen.

## The answer

1. 🔴 **Claude Code says how much of each window is used, on every frame, on an ordinary turn.**
   `rate_limit_event` carries `unifiedWindows`: per window (`five_hour`, `seven_day`), `utilization`
   as a fraction from 0 to 1, and `resetsAt` in Unix seconds. All 117 frames recorded on this machine
   carry it, and none of them was a warning or a limit. The SDK's TypeScript declaration does not list
   the field.
   D130 §0.3 (C11) read that `utilization` comes *only when a threshold is crossed*: that is the
   top-level `utilization`, which no recorded frame carried. So D130 §4.4's *clear, said without a
   number* does not describe Claude Code 2.1.287: it gives a number, as Codex does.
2. **The warning word is Claude Code's own judgement, and it had not come at 88%.** The CLI turns the
   server's `allowed_warning` into `allowed`, then derives its own warning from each window's readings,
   against thresholds of use and of time elapsed in the window. This branch's turn read `allowed` at
   88% of the five-hour window, with two hours of it left.
3. **A limit on the native door** is a `rate_limit_event` saying `rejected`, then a `result` with
   `subtype: "success"`, `is_error: true`, `api_error_status` (the HTTP status) and the limit sentence
   as `result`. The assistant message before it carries a category in `error`. This was read, not
   measured.
4. **The protocol door forwards the frame, and Daoris drops it.** `claude-agent-acp` 0.84.0 sends each
   `rate_limit_event` as a `usage_update` whose `_meta["_claude/rateLimit"]` is the frame's
   `rate_limit_info`, unchanged. It does so only once the turn has a real answer, so a turn refused
   before the model answered forwards none. A limit becomes the JSON-RPC error `Internal error:
   <sentence>`, with `data: { errorKind }`. Daoris's protocol door keeps the error's message alone, and
   reads only `used` and `size` from `usage_update`.
5. 🔴 **`codex-acp` forwards none of Codex's limits, and its limit error has no sentence in its
   message.** It keeps `account/rateLimits/updated` for its own `/status` text. A usage limit answers
   the prompt with JSON-RPC `-32603`, the message `Internal error` and nothing after it, and the
   sentence in `data.message` beside `codexErrorInfo: "usageLimitExceeded"`. The sentence also streams
   as the agent's own words. Daoris's protocol door keeps only the message, and D125's reader never
   reads the agent's words, so **a Codex limit cannot reach a table entry until the door reads the
   error's `data`**. Codex's grammar also differs from Claude Code's: *Try again at 4:05 PM.*, or a
   date with an ordinal and a year, and no zone.
6. **`/usage` and the status line are for a person.** `/usage` fetches the plan's windows from the
   maker's usage endpoint and shows them as text, with a structured twin the SDK calls experimental.
   The status line hands `rate_limits` to a command the person's settings name. Neither is a door a
   driven session already carries, so D125 §1.4 and D130 §5.1 stand.

## 0. What was read, at which version

| Harness | Version | Where | What was read |
|---|---|---|---|
| Claude Code, native | **2.1.287** | on `PATH`, the native build (sha256 `6d5be51f…`) | the JavaScript embedded in the binary, cited by byte offset (§1.3); one turn's output (§1.1); KNOW3's recorded streams (§1.1) |
| Claude Agent SDK | **0.3.284**, the adapter's pin, checked against **0.3.287**, npm's latest | `npm pack` into an ignored scratch folder | `sdk.d.ts`. `SDKRateLimitInfo` and the three prefix lists are the same in both |
| Claude Code over ACP | adapter **0.84.0**, its SDK 0.3.284, that SDK's CLI 2.1.284 | `npm pack`; `dist/acp-agent.js` sha256 `118db041…`, the install's pin as LAYOUT2 found it | the adapter's `dist/`, and `@agentclientprotocol/sdk` 1.5.1's `dist/jsonrpc.js`. The CLI 2.1.284's own frame was not read (§7) |
| codex over ACP | **2.1.1**, npm's latest, and **1.12.0**, LAYOUT2's | `npm pack` | `dist/index.js` of each. 2.1.1 is cited; 1.12.0 matches it on every row of §4 |
| codex | **0.159.3** (codex-acp 2.1.1 asks `^0.159.1`) | not installed | the maker's source at the tag `rust-v0.159.3`: `codex-rs/protocol/src/error.rs`, and `codex-rs/app-server-protocol/src/protocol/v2/account.rs` and `shared.rs` |
| The makers' pages | fetched 2026-10-02 | code.claude.com, as markdown | `errors`, `commands` and `statusline` |

**Labels.** **measured**: a frame recorded on this machine. **source**: read in shipped code, at the
place cited. **declaration**: the SDK's type declarations. **doc**: the maker's page. **not read**:
nothing here reached it. Every label but *measured* is unmeasured.

## 1. Claude Code's `rate_limit_event`, on the native door

### 1.1 The frames recorded

- **This branch's turn.** `claude -p "<one word>" --output-format stream-json --verbose
  --setting-sources project --strict-mcp-config`, on Claude Code 2.1.287 signed in with a subscription
  (`apiKeySource: none`). Daoris's native door also passes `--include-partial-messages`
  (`Adapters.cs`), which adds `stream_event` lines and nothing this note reads. Five lines came:
  `system/init`, `system/commands_changed`, one `assistant`, one `rate_limit_event`, then the
  `result` (`success`, `terminal_reason: completed`).
- **KNOW3's streams.** 103 headless sessions on 2026-10-02, on Claude Code 2.1.287: the bench's full
  run, its pilot and one probe, recorded for `tools/knowledge-bench.mjs`
  (`docs/2026-10-02-knowledge-bench-results.md`). They hold 116 frames, at least one in every
  session. The untrimmed streams are kept untracked; the trimmed copies under
  `tools/knowledge-bench-fixtures/` keep `status`, `rateLimitType` and the windows' use only.

The frame, as this branch's turn sent it, with `<+2.0 h>` meaning about two hours after the turn:

```
{"type":"rate_limit_event","rate_limit_info":{"status":"allowed","resetsAt":<+2.0 h>,
 "rateLimitType":"five_hour","overageStatus":"rejected","overageDisabledReason":"member_zero_credit_limit",
 "isUsingOverage":false,"unifiedWindows":{"five_hour":{"utilization":0.88,"resetsAt":<+2.0 h>},
 "seven_day":{"utilization":0.14,"resetsAt":<+105.3 h>}}},"uuid":"…","session_id":"…"}
```

All 117 frames have this one shape: the same keys, `status` always `allowed`, `rateLimitType` always
`five_hour`, and the top-level `resetsAt` always the five-hour window's. Five-hour use was 0.25 to 0.44
across the 103 sessions and 0.88 on this branch's turn; seven-day use was 0.04 to 0.07, and 0.14. The
accounts they ran on are not named here, and nothing says they were one account.

**When it came** (measured): the first frame directly followed the session's first `assistant` message
in 86 of the 103 sessions, and a later message in the other 17, always before the `result`. 11 sessions
sent two frames and one sent three. Each later frame differed from the one before only in one window's
`utilization`, by 0.01, with `status` unchanged.

### 1.2 The fields

| Field | What it says | When it comes | Source | Standing |
|---|---|---|---|---|
| `status` | `allowed`, `allowed_warning` or `rejected` | every frame | measured; `sdk.d.ts:5584` | `allowed` measured; the other two unmeasured |
| `resetsAt` | Unix seconds: the reset of the window `rateLimitType` names | every recorded frame | measured; declaration | measured |
| `rateLimitType` | `five_hour`, `seven_day`, `seven_day_opus`, `seven_day_sonnet`, `seven_day_overage_included` or `overage`: the window the server names as representative (its `anthropic-ratelimit-unified-representative-claim` header) | every recorded frame | measured; `sdk.d.ts:5586`; source `0xc900d8d` | `five_hour` measured |
| `unifiedWindows.<window>.utilization` | that window's use, a fraction from 0 to 1 | every recorded frame, for each window the server sent | measured; source `0xd00d644`, `0xda5593b` | measured. **Not declared** in SDK 0.3.284 or 0.3.287 |
| `unifiedWindows.<window>.resetsAt` | that window's reset, Unix seconds. A window whose reset has passed, or lies more than a year away, is left out; `seven_day_overage_included` joins the two when the server sends it | as above | measured; source | measured for `five_hour` and `seven_day` |
| `utilization` (top level) | the warned window's use | on a warning the CLI derived | `sdk.d.ts:5587`; source `0xc900b77` | unmeasured; on no recorded frame |
| `surpassedThreshold` | the server's threshold a window passed (`anthropic-ratelimit-unified-5h-surpassed-threshold` and the like) | on a warning | `sdk.d.ts:5594`; source | unmeasured |
| `overageStatus`, `overageResetsAt`, `overageDisabledReason` | usage credits: whether they may be drawn on, their reset, and why not (thirteen reasons, `sdk.d.ts:5591`). The wire renames `org_spend_cap_reached` to `org_level_disabled_until` | when the server says | measured (`rejected`, `member_zero_credit_limit`; `overageResetsAt` absent); source `0xd00d644` | measured as given |
| `isUsingOverage`, `overageInUse` | whether this request drew on usage credits | `isUsingOverage` on every recorded frame | measured (`false`); declaration | `overageInUse` unmeasured |
| `limitScope` | the spend limit that blocked, when it is not the member's own: `service`, `channel` or `group_pool` | on a refusal | `sdk.d.ts:5601` | unmeasured |
| `errorCode`, `canUserPurchaseCredits`, `hasChargeableSavedPaymentMethod` | from a refused request's error details, when they say `credits_required` | on that refusal | declaration; source (`the`, just after `Zge`) | unmeasured |
| `rateLimitGraceActive`, `overagePeriodMonthly`, `overagePeriodChannel` | a grace window, and credit use in a monthly or channel period | when the server says | source `0xd00d644`; not declared | unmeasured |

### 1.3 What the CLI does, read in 2.1.287

Cited by the byte offset where the function that holds it starts. The names are the minifier's and
change between builds; the offsets hold for 2.1.287 only.

- **Where it comes from.** Each API response's `anthropic-ratelimit-unified-*` headers are parsed
  (`Zge`, `0xc900d8d`) into the CLI's limit state (`extractQuotaStatusFromHeaders`, `0xc903485`), and
  so is a refused request's 429 (`extractQuotaStatusFromError`, in the same class). **Only for a
  claude.ai subscription, as read:** the parse returns before building anything when a check on the
  signed-in account fails (`0xc903485`; the check itself was not followed), and the declaration names
  the frame's audience, *"Rate limit information for claude.ai subscription users"*. So an account
  that is an API key (D67 §1) should send no frame. source; declaration; not measured.
- **When it is sent.** On every change of that state (`emitStatusChange`, `0xc902ed1`), which the
  first response of every process is, since the state starts empty; and when a window's shown use
  changes though the state did not (`lastEmittedWindowParts`, `0xc903485`). The headless host writes
  the frame from that change (`Qh`, `0xdaa7e55`). On a refused request it sends the same frame again
  at most every 30 seconds for one reset, until that reset passes (`Noo`, `0xda55a63`), behind a
  maker's flag, `tengu_gravel_chorus`, on by default. source; the frame sent again when a window's use
  moved one point is measured (§1.1).
- **`allowed_warning` is the CLI's own.** The status header's `allowed_warning` is turned into
  `allowed` (`0xc900d8d`). A warning is then derived from each window's readings: the server's
  surpassed threshold, or a window whose use is at or over a threshold while the time elapsed in it is
  at or under that threshold's share (`Ugn`, `0xc900b77`). The thresholds' values were not read (§7).
  source.
- **The builder** writes §1.2's fields (`Sa`, `0xd00d644`), and adds `unifiedWindows` from the
  windows' own readings (`ptt`, `0xda5593b`). source.

**Daoris today:** `ClaudeStreamJson` maps `rate_limit_event` to nothing (`StructuredOutput.cs`, *its
limits*). Nothing of it is kept, not even raw. source.

## 2. A limit's failed `result`, on the native door

| What | Value | Source | Standing |
|---|---|---|---|
| The `result`'s subtype | `success`, with `is_error: true`. The SDK: *"subtype "success" carries the final assistant text in result — or, with is_error true, the error text when the turn ended on an API error"* (`sdk.d.ts:5664`). The CLI sets `is_error` from the last assistant message's API-error mark (`0xd0d9264`, `0xd0dafc4`) | declaration; source | unmeasured |
| `result` | that message's text: the limit sentence | source `0xd0d9264` | unmeasured |
| `api_error_status` | that message's HTTP status, else `null` | `sdk.d.ts:5697`; source; `null` on this branch's turn | the 429 unmeasured |
| `api_error_code`, `api_error` | written beside it; not declared | source `0xd0dafc4` | unmeasured |
| The assistant message before it | `error`: `authentication_failed`, `oauth_org_not_allowed`, `account_on_hold`, `verification_required`, `billing_error`, `rate_limit`, `overloaded`, `invalid_request`, `model_not_found`, `server_error`, `unknown`, `max_output_tokens` or `cloud_credential_error` (`sdk.d.ts:3604`, `:3678`). Which one a usage limit carries was not read | declaration | unmeasured |
| The sentence's start | `USAGE_LIMIT_ERROR_PREFIXES`, *"Messages meaning 'a usage limit was genuinely reached'"*: `You've hit your`, `You've reached your`, `You're out of usage credits`, and nine more (`sdk.d.ts:9694-9702`) | declaration | unmeasured |
| A warning's words | `USAGE_WARNING_PREFIXES` (`You've used`, `You're close to`) and `USAGE_TRANSITION_PREFIXES` (now using usage credits): *"Footer/toast only; these never arrive as API errors"* (`sdk.d.ts:9704-9719`). The maker's page gives *You've used 85% of your session limit · resets 3:45pm* | declaration; doc | unmeasured |
| Retried | no: *"Claude Code fails at once when a standard-speed request gets a 429 that reports a spend limit or exhausted usage credits"* (the errors page, under `CLAUDE_CODE_RETRY_WATCHDOG`) | doc | unmeasured |
| Waiting it out | *Continue automatically at usage limit*, `autoContinueAtUsageLimit` in the settings file, waits for the reset and goes on (`sdk.d.ts:9119-9121`). The page says Claude Code can wait *"in an interactive session"* | declaration; doc | not read for a headless session |

**Sentences the maker documents that D125's table has not recorded** (doc, the errors page): *You've hit
your weekly limit · resets Mon 12:00am*, a weekday, which TOOL4a's grammar does not read; *You've hit
your Opus limit* and *Sonnet limit*; *You've hit your team's shared budget*; and *usage limit* in place
of *spend limit* on usage-based billing.

**Daoris today:** `ClaudeStreamJson.Result` writes *— the turn failed (success): {result}* to the
transcript and nothing reaches the conclusion (D125's TOOL4d note). `api_error_status` is not read.
source.

## 3. The protocol door: `claude-agent-acp` 0.84.0

| What | Value | Source (`dist/`) | Standing |
|---|---|---|---|
| A `rate_limit_event` | forwarded as a `usage_update`: `used`, the turn's context; `size`, the window; `_meta: { "_claude/rateLimit": <the rate_limit_info, unchanged>, "_claude/model": <the model> }` | `acp-agent.js:4772-4785`, `:2358-2368` | unmeasured |
| When it is dropped | when the turn has no context reading yet. The reading is cleared as a turn starts (`:2377`) and set by a real model frame or a compaction (`:3172`, `:4408`, `:4577`), never by a synthetic one (`:4573-4579`). So a turn refused before the model answered forwards no frame; one refused mid-turn, as observation 1 was (D125 §0.2), forwards it | `:2172`, `:4773` | unmeasured |
| A limit | the turn fails with `RequestError.internalError({ errorKind }, result)`: JSON-RPC `-32603`, the message `Internal error: <the sentence>`, and `data.errorKind` the SDK message's `error` where it set one (`:4178-4180`, `:4221-4223`, `:7249-7251`; `jsonrpc.js:1020-1022` in the protocol SDK). A client that declares AIR's `sessionFailure` gets a typed session failure instead (`:2780-2794`); Daoris declares AIR's `nativeSubagentSessions` and `asyncTasks` only (`Acp.cs`) | source. The message agrees with D125 §0.2's observations 1, 2 and 4 | `data` unmeasured |
| Recognising a limit | the adapter matches `USAGE_LIMIT_ERROR_PREFIXES` against a message from model `<synthetic>` and calls it `quota_exhausted`; the category `rate_limit` alone is `rate_limited` | `session-failure-extension.js:121-131`, `:329-360` | unmeasured |
| `/usage` | a prompt of exactly `/usage` runs through Claude Code, and the adapter renders the SDK's experimental usage method as markdown, behind a timeout | `acp-agent.js:101-145`, `:1777-1779`; `usage-markdown.js` | unmeasured |

**Daoris today:** `AcpSession.Complete` keeps the error's `message` and drops its `data`, and `Measure`
reads `used` and `size` and nothing of `_meta` (`Acp.cs`). The CLI under this door is the SDK's 2.1.284
(LAYOUT2's evidence, §0); whether its frame carries `unifiedWindows` was not read. source.

## 4. `codex-acp` and Codex

| What | Value | Source | Standing |
|---|---|---|---|
| Codex's own field | the app server's `account/rateLimits/updated`, a *"Sparse rolling rate-limit update"*: `rateLimits` with `limitId`, `limitName`, `primary` and `secondary` windows (`usedPercent`, a whole percent; `windowDurationMins`; `resetsAt`, Unix seconds), `credits` (`hasCredits`, `unlimited`, `balance`), `individualLimit` (`limit`, `used`, `remainingPercent`, `resetsAt`), `spendControlReached`, `planType` and `rateLimitReachedType` | `v2/account.rs:650-677`, `:752-758`, `:773-777`, `:792-798` | source |
| What `codex-acp` does with it | keeps it per `limitId` for its own `/status` text (percent left, reset, window) and sends nothing: the notification returns `null` | 2.1.1 `dist/index.js:30460-30462`, `:30998-31011`, `:36047-36066`; 1.12.0 `:24876` | source |
| Its `usage_update` | `used` and `size` only, from codex's token usage; no `_meta` | 2.1.1 `:30985-30996` | source |
| Asking for them | `account/rateLimits/read`, made when its `/status` is asked | 2.1.1 `:35248-35250`, `:36068-36075` | source |
| A usage limit | the app server's `error` with `codexErrorInfo: "usageLimitExceeded"` (`v2/shared.rs:77-80`), to which codex maps a usage limit reached, a quota exceeded and usage not included (`error.rs:458-465`). The error's message streams to the client as the agent's message text; the prompt then fails with `RequestError.internalError(data)`: JSON-RPC `-32603`, the message `Internal error` and nothing after it, `data: { message, codexErrorInfo, additionalDetails? }` | 2.1.1 `:30839-30846`, `:30968-30978`, `:39678-39680`; 1.12.0 `:25419-25422` | source |
| The sentence | `UsageLimitReachedError`'s text: *You’ve hit your usage limit.*, a plan's own next step, then *Try again at {time}.* or *… or try again at {time}.*, or *Try again later.* when no reset is known. A named model's limit: *You’ve hit your usage limit for {name}. Switch to another model now, or try again at {time}.* Four workspace sentences name no reset (*Your workspace is out of credits…*, *You hit your spend cap…*). `{time}` is the machine's local time with no zone: `4:05 PM` the same day, else `Oct 3rd, 2026 4:05 PM`. The apostrophe is `’` | `error.rs:676-822` | source |
| A limit mid-turn | the running turn may finish | D130 §0.3 (X1) | doc |

Whether the app server's `error` message is exactly that text was not read in the app server's code.

## 5. `/usage` and the status line

| Surface | What it exposes | How | Source | Standing |
|---|---|---|---|---|
| `/usage` (also `/cost`, `/stats`) | *"session cost, plan usage limits, and activity stats"* | text, with the plan's windows fetched from *"the claude.ai usage endpoint"* | the commands page; `sdk.d.ts:4192-4330` | doc; declaration |
| `usage_report` | `/usage`'s structured twin: `rate_limits.limits[]`, the server's rows as sent (`kind`, such as `session`, `weekly_all`, `weekly_scoped`; `group`; `percent`, 0 to 100; `resets_at`, ISO 8601; `scope`; `severity`, such as `normal`, `warning`, `critical`; `is_active`), and `extra_usage` | on the synthetic assistant message that delivers a `/usage` result | `sdk.d.ts:3654-3656`, `:6072-6140` | declaration |
| The SDK's `get_usage` control request | the same data with named windows (`five_hour`, `seven_day`, `seven_day_opus`, `seven_day_sonnet` and more; `utilization` 0 to 100; `resets_at`, ISO 8601), `subscription_type` and `rate_limits_available` | *"EXPERIMENTAL: this API is unstable and may change or be removed in any release without notice"* | `sdk.d.ts:3026-3044`, `:4192-4330` | declaration |
| The status line | `rate_limits.five_hour`, `.seven_day` and `.spend_limit`: `used_percentage`, 0 to 100, and `resets_at`, Unix seconds. It *"appears only for claude.ai Pro and Max subscribers, or behind a Claude apps gateway that sets a spend limit for you, and only after the first API response in the session"* | JSON handed to the command the person's settings name | the statusline page | doc |
| `/status` | version, model, account and connectivity; no limits | | the commands page | doc |

Three scales for one quantity: `rate_limit_event`'s fraction from 0 to 1; `/usage`'s and the status
line's percentage from 0 to 100; Codex's whole percent. Whether a headless session runs the status line
command was not read.

## 6. Read against D125 and D130

What each finding changes in what those decisions assumed. None of it is decided here.

| D125 or D130 assumed | This note finds |
|---|---|
| D130 §0.1: a native frame came on an ordinary turn, *its fields were not recorded* | Recorded (§1.1), 117 times, one shape |
| D130 §4.4, standing 2: Claude Code's `allowed` comes *without a number* | Each window's use comes on every frame, as a fraction (§1.2). A Claude Code reading has a number as a Codex one does, on its own scale |
| D130 §6: near is the agent's word where its field has one | The word is the CLI's own derivation, and read `allowed` at 88% of a window (§1.3). When it comes relative to a limit is unmeasured; a number is there on every frame |
| D130 §5.2: an entry grows only from a frame recorded on Daoris's door | The frames are the native wire, from the binary and flags the native door spawns, but none was written by Daoris's driver. None was recorded on the protocol door |
| D125 §1.2: a field beats a sentence on the native door, once recorded | The ordinary frame is recorded; a `rejected` one and a limit's failed `result` are read only (§2). TOOL4d's reason for not handing the native failure to the table stands |
| D125 §1.2: an ACP error's `data` *might* carry a field | `claude-agent-acp` puts `errorKind` there, and `codex-acp` puts `codexErrorInfo` and the sentence there (§3, §4) |
| D125 §1.3: a door's limits are its owner's | A Codex limit reaches Daoris's door as `Internal error` alone; no entry can match until the door reads `data` (§4) |
| D125 §2.1, as TOOL4a built it: the reset grammar | The maker documents a weekday reset (*Mon 12:00am*); Codex writes `4:05 PM` and `Oct 3rd, 2026 4:05 PM`, with no zone (§2, §4) |

## 7. What this note does not establish

- **No limit, warning or refusal was met.** Every `allowed_warning`, `rejected`, failed `result`, error
  `data` and Codex row is read, not measured.
- **Two reads of the CLI binary were not made**, refused by this session's permissions, and not
  pursued another way: the warning thresholds near `0xc900b77`, and where the CLI writes its limit
  sentence. So the thresholds' values, and which `error` category a usage limit carries, are not read.
- **The protocol door's forwarded frame was not recorded.** One turn was allowed, and it ran on the
  native door. The CLI under that door, 2.1.284, was not read.
- **Codex** is not installed and has no account here. Whether a headless session sends a warning as a
  `system/notification`, whether `autoContinueAtUsageLimit` acts in a headless session, and whether a
  headless session runs the status line were not read.
- The makers' pages were read on 2026-10-02 and may change.
- `verify` checks this note's links, its router row and the budgets, and none of these words.
