# Account use: how switching works, and how several accounts are used well (TOOL6)

> The owner, 2026-10-02, asked which account should run a workspace whose account was spend-limited: *"there are
> multiple accounts we should be able to set option how switch works and how to optimize the account use since there
> are 3 accounts"*. Asked the open questions, they answered: *"yes all three accounts can run the work workspace this
> really depends on how many token left since all 3 are sub based and how accounts been used like one by one or 3
> paralle or other logic should be configable"*, and then: *"3 is becau I only have 3 not limited to account
> numbers"*. D125 (`docs/2026-10-01-account-rotation-design.md`) has one policy: walk the person's order when the
> default is not ready. This is the contract for what the person sets beyond that, and for what Daoris does to use
> the accounts well, for any number of accounts. Its decision is **D130**, which amends D125. Status: **designed;
> nothing built.** 🔴 **§16, *the rules around the goal* (2026-10-02), re-decides the defaults** on the owner's goal:
> the most work from the accounts' combined allowance, the fewest stalls, and no allowance left unused at a reset. By
> default every listed account is used toward it; *one by one, in order* is an override. Where §16 and §2–§10
> disagree, §16 wins, and each place it changes says so. Read with **D125** and its TOOL4a–TOOL4j notes, **D57**,
> **D58**, **D48 §2a**, **D49 §4**, **D50**, **D54**, **D110** and **D116**.

Accounts are named as Daoris names their directories (`account-1`, `account-2`, …, `account-N`: D66 §3). **An agent
may have any number of accounts, one or more, and nothing here counts them**: where an example needs a number, it is
N for the accounts and K for the driver's cap (the aims of §1 are lettered A to E). Nobody's account, address or time
zone is named, and a sentence an agent printed is quoted only in a shape D125's notes, its §0.2 or the fix log already
recorded.

- §0 is what is true today, read from the code at `7cca0e1`, and what the makers document.
- §1 is what *well* means: five aims that pull apart. §2 is the decision in one table.
- §3–§7 answer the brief: which accounts may run a workspace, how a list is used, what Daoris knows of what is left,
  switching before a limit, and sessions at once. §8 is what each choice costs, §9 the doors.
- §10 is the build, as rows. §11–§15 are what only a real run proves, what was rejected, what this amends, the twins,
  and what this document's gate does not cover.
- §16 is the rules around the goal: the defaults re-decided, each proposal weighed, the overrides, how the goal is
  judged, and TOOL6a's vocabulary as it changes.

## 0. What is true today

### 0.1 The code, after TOOL4f

| What | Today |
|---|---|
| Which account a start runs as | `HarnessSettings.ResolveFrom`: the person's pick, the workspace's default, the machine's, then none, which is the tool's own sign-in (D49 §4). Driven starts and intakes pass no pick; a conversation passes the person's; Ask Daoris passes none and no workspace, so it reads the machine's |
| The order | `rotation` and `workspaceRotation` in `harnesses.json`, one list per agent, of any length. `ResolveRotationFrom` takes the workspace's list, else the machine's, else none, **whichever rung named the account** (D125 §3.1) |
| The walk | `AccountRotation.Candidates`: the resolved account, and only where a default named it and the applicable list holds it, the rest of the list after it, wrapping. `SelectAsync` runs the first ready one: not cooling, not refused (AGT3b), not signed out. A pick, the tool's own sign-in and an account outside the list are the one account. No list is D125's *none*, byte for byte |
| 🔴 A workspace that names its own default and no list | Takes the machine's list. A work workspace whose default is the work account, on a machine whose list also holds personal accounts, rotates onto them when the work account cools. Nobody said it may |
| A workspace that has a list and no default | Starts on the machine's default, or on the tool's own sign-in, even when its list does not hold it, and then does not rotate. The list it stated is not where it runs |
| How many run at once | `cap` in `driver.json`: one number for the machine, 2 when absent; the install runs 4. `Planner` spends a slot per start. Nothing counts sessions per account, and `SessionView` names no account |
| What a limit does | Cools its account until the reset the agent named (TOOL4d). Every session running on that account meets the same refusal at its next call: observation 2 was **two** driven sessions on one account refused with one reset (D125 §0.2) |
| What is measured | `usage.json`: per session, its **context** at its high-water (`used`, `size`), totals per account derived (TOOL3). That is how full a session's window got, not what the account spent: a long session re-sends its context every turn. Nothing anywhere says what an account has left |
| What arrives and is not read | The native door's `rate_limit_event` frames, mapped to nothing (`ClaudeStreamJson`, *"its limits"*). One came on an ordinary turn, far from any limit (`docs/2026-09-25-stream-json-evidence.md`), so the frame is sent before a limit, not only at one; its fields were not recorded. The native `result` also carries the turn's token counts (input, cache creation, cache read, output). The protocol door's `usage_update` carries `used` and `size` and nothing about a limit. TOOL4b, which records the native frame's shape, is open |
| The log | `account.limited` (hit, window, until, stated, turn, used), `account.rotated` (from, to, carries), `starts.waiting` (D94 §4 as D125 amended it). Every limit an account met, and when, is already on this machine |

### 0.2 The evidence on this machine

D125 §0.2 has the five observations; this design adds no sentence. What they say about *using* accounts, rather than
reading a limit:

- **A limit takes every session on its account at once.** Observation 2: two driven sessions, one account, one reset
  (*… · your session limit resets 7:50am (<zone>)*), both refused. With K sessions on one account, one limit is K
  cut-offs, and each carry-on starts a fresh conversation from Daoris's record (TOOL4f).
- **One account is spent by Daoris and by the person alike.** Observations 4 and 5 named one reset (*your weekly limit
  resets Oct 3, 4pm (<zone>)*): Daoris's sessions and the person's own agents, outside Daoris, on one account. Daoris
  sees only its own share of what an account spent.
- **Two kinds of window, and a spend limit on a team seat.** *Your session limit* resets within hours; *your weekly
  limit* in days. *You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit*
  names an administrator: at least one of the accounts is a seat on a team plan.
- **The work moved by hand.** After observation 5 the owner added another account and resumed the stopped agents on
  it. Which account should carry which work was then a question put to the owner, which is this design's brief. They
  had three accounts that day; nothing here depends on how many.

### 0.3 What the makers document

Fetched on 2026-10-02, keylessly. The code.claude.com pages came back as their own markdown, so those quotes are
exact; every other page passed through a summarising reader, so a quote from it may differ from the page by a word.
support.claude.com shows only relative dates. help.openai.com and openai.com refused every fetch, so nothing from them
was read. *Read* means read on the maker's own page or repository; *not read* means a search snippet or a third party.
Nothing here was measured on this machine. The sources are listed at the end of this section.

**Claude.**

| What | The maker's words | Source | Standing |
|---|---|---|---|
| The session window | *"Your session-based usage limit will reset every five hours."* A team or enterprise seat's allowance *"resets on a rolling five-hour window and a weekly window."* | C1, C2 | read |
| When a window starts | that it starts with the first message | third parties only; the maker's article that may have said so is gone | **not read** |
| The weekly window | *"The weekly limit resets at a fixed time each week that is assigned to your account."* *"Your reset day and time stay the same regardless of when you start using Claude…"* | C1 | read |
| Limits by model family | *"The session and weekly limits are shared across all models… The Opus and Sonnet limits each apply only to requests to that model family."* *"Usage counts against the session and weekly allowances at the same time."* | C3 | read |
| One allowance across surfaces | *"Your usage of all different Claude product surfaces (claude.ai, Claude Code, Claude Desktop) counts towards the same usage limit."* | C4 | read |
| A team member's spend limit | *"Owners and Primary Owners can also set individual monthly spend limits for each member…"* *"Once a user reaches their defined spend limit, this will automatically pause their usage credits until the end of the month."* Credits begin *"as soon as you reach your seat's usage limit."* | C5 | read |
| What the member is told | *"You've hit your individual spend limit · ask your admin for a higher limit"*; *"… your session limit resets 3:45pm"* when a window is what ran out; a warning, *"You've used 85% of your session limit · resets 3:45pm"* | C3 | read |
| Usage credits | *"Usage credits are billed at standard API rates"*, for conversations and Claude Code alike | C6 | read |
| What the CLI shows a person | `/usage`: *"Show session cost, plan usage limits, and activity stats"*; *"Run `/usage` to see your plan limits and when they reset."* `/status` shows the version, model, account and connectivity | C7, C3 | read |
| The status line | `rate_limits.five_hour.used_percentage` and `rate_limits.seven_day.used_percentage` (*"from 0 to 100"*), `rate_limits.*.resets_at` (*"Unix epoch seconds"*); it *"appears only for claude.ai Pro and Max subscribers… and only after the first API response"* | C8 | read |
| `rate_limit_event` | The Agent SDK's `RateLimitInfo`: `status` (`allowed`, `allowed_warning`, `rejected`), `resets_at`, `rate_limit_type` (`five_hour`, `seven_day`, `seven_day_opus`, `seven_day_sonnet`, `overage`), `utilization`, `overage_status`, `overage_resets_at`, `overage_disabled_reason`. *"The CLI emits this whenever the rate limit status transitions (e.g. from `allowed` to `allowed_warning`)."* | C9 (Python SDK 0.2.163) | read: the Python declaration; the TypeScript one was not |
| The TypeScript SDK | a usage-limit wait emits `rate_limit_event` (`rejected`, `resetsAt`) (0.3.280); it is re-sent about every 30 seconds during an exceeded window (0.3.260) | C10 | read |
| `utilization` | sent *"only when a threshold is crossed"*; a request to send it always was closed as not planned | C11 | read: the maker's issue tracker |
| The CLI's own wire | camelCase: `rateLimitType`, `resetsAt`, `status`, `isUsingOverage`, `overageStatus` | a user's issue on the maker's repository (2026-02-20) and a third party's (2026-09-28) | **not read** as the maker's |

**Codex.**

| What | The maker's words | Source | Standing |
|---|---|---|---|
| Windows | *"The estimates below show local messages per five-hour period for Plus and Standard Business."* *"Pro plans currently have no five-hour limit."* *"Weekly limits may also apply."* | X1 | read |
| One allowance | *"Local messages and cloud chats share your plan's usage allowance."* | X1 | read |
| A limit mid-turn | *"If you reach your usage limits during an active turn, the agent will be able to continue working on that turn, subject to fair use limits."* | X1 | read |
| When a window starts | *"A new window starts when you send your first message in Work or Codex after the previous window ends."* | help.openai.com, a search snippet | **not read** |
| The protocol's field | `TokenCountEvent { info, rate_limits }`; `RateLimitSnapshot` with `primary`, `secondary`, `credits`, `individual_limit`, `spend_control_reached`, `plan_type`, `rate_limit_reached_type`; `RateLimitWindow { used_percent, window_minutes, resets_at }` | X2 | read |
| The app server | a request, `account/rateLimits/read`, and a notification, `account/rateLimits/updated` | X3 | read |
| `codex exec --json` | no rate-limit field: token counts only | X4 | read |
| The limit's words | *"You've hit your usage limit."*, a plan's own next step, then *"Try again at {formatted}."*; the error carries `resets_at` and `limit_window_minutes` | X5 | read, in source: not a sentence recorded on a door |
| Credits | *"Flexibly extend usage with ChatGPT credits"* | X1 | read |

**Tools that rotate accounts.** All are third parties' and none is a maker's; each was read on its own page.

| Tool | How it switches | Source |
|---|---|---|
| claude-swap | *"When the active account's 5-hour or 7-day window reaches the threshold (default 90%), it switches to the account with the most quota left."* A 5-minute cooldown and a margin stop it flip-flopping | T1 |
| cc-account-switcher | a hook before each tool call compares a usage cache with 80% of the five-hour window and *"switches to the next account"*, under an exclusive lock | T2 |
| codex-rotator | a probe every 60 seconds; *"If the quota is exceeded, it swaps the active auth file to the first working account in your pool."* | T3 |
| CLIProxyAPI | round robin, *"Rotates through accounts evenly"*, or fill first, *"Uses first account until quota exceeded"*; a 429 cools a credential with backoff from 1 second to 30 minutes | T4 |
| LiteLLM, a gateway | least busy, *"the deployment with the least number of ongoing calls"*; usage-, latency- and cost-based; a priority, *"Lower values = higher priority"*; fallbacks *"in-order"*; a cooldown after failures | T5 |
| OpenRouter, a gateway | providers *"that have not seen significant outages in the last 30 seconds"*, weighted by the inverse square of price; an `order` and fallbacks | T6 |

**The makers' terms.**

| What | The maker's words | Source | Standing |
|---|---|---|---|
| What a limit assumes | *"Advertised usage limits for Pro and Max plans assume ordinary, individual usage of Claude Code and the Agent SDK."* | L1 | read |
| What is permitted | *"an end user… signing in to the unmodified Claude Code binary with their own Claude subscription"*. Not permitted: third-party developers who *"route requests through Free, Pro, or Max plan credentials on behalf of their users"*, or *"collect, store, or intermediate Claude.ai credentials or session tokens"* | L1 | read |
| Sharing an account | *"You may not share your Account login information… You also may not make your Account available to anyone else."* | L2 (effective 2025-10-08) | read |
| Automated access | access *"through automated or non-human means, whether through a bot, script, or otherwise"* is not allowed except through an API key *"or where we otherwise explicitly permit it"* | L2 | read |
| Several accounts | *"Coordinate malicious activity across multiple accounts to avoid detection or circumvent product guardrails"* is a prohibited use. No text was found on one person holding several paid accounts | L3 (effective 2025-09-15) | read |
| OpenAI | not to *"circumvent any rate limits or restrictions"*, nor share account credentials | openai.com, a search snippet | **not read** |

**What it means for this design.**

- **Windows reset on the maker's clock**: every five hours, and weekly at a time fixed per account. Nothing read says an
  allowance left at a reset is kept. So spending allowance that would otherwise lapse is a real gain (§4.4, §4.5), and
  a weekly reset, once stated, is a fact about that account until it changes.
- **One allowance covers every surface** (C4), so Daoris sees only its own share of an account, as observations 4 and
  5 showed. That is the maker's word now, not only this machine's.
- **The team seat's sentence reads as C5 says**: the seat's windows ran out, usage credits began, the member's monthly
  spend limit paused them, and the seat's own window still resets at the time the agent named. D125's cool-off to that
  reset stands.
- **The agent has its own word for what is left**: `allowed`, `allowed_warning` and `rejected`, sent on a change of
  status, with `utilization` only past a threshold and `resets_at` per window (C9, C11). Codex's protocol gives a
  percentage and a reset per window (X2). Neither is known to reach a door Daoris reads: Claude Code's arrives as
  `rate_limit_event` on the native door, unrecorded (§0.1), and whether its protocol door's adapter forwards it, or
  `codex-acp` forwards Codex's, is TOOL4b's to find (§10).
- **A Codex limit does not cut a turn off** (X1), so on Codex a limit refuses the next turn rather than taking the
  sessions running on the account mid-turn.
- **The rotating tools learn quota by asking and switch by replacing a credential**, where their pages say how:
  claude-swap polls, cc-account-switcher keeps a usage cache, codex-rotator probes every 60 seconds and swaps the active
  auth file, and CLIProxyAPI holds the credentials itself. Daoris does neither: one directory per account, chosen at
  spawn, nothing read or moved inside it (D49 §4, D66 §3), and no request made only to ask (D125 §1.4). Their
  thresholds are 80% and 90%; their shapes are fill first, round robin, least busy and most quota left, which §4 weighs.
- **On the terms**: Daoris runs the unmodified tool, signed in by the person through the tool's own sign-in, and never
  holds or intermediates a credential (unchanged). It uses only accounts the person holds and listed, for that
  person's own work, never between people. Whether a person's use of several accounts fits each maker's terms is
  theirs to judge, and Daoris claims nothing about it: the account rows say *each account's own plan and terms apply*,
  and nothing presents rotation as a way past a maker's limits.

**Sources.**

- C1: support.claude.com, *What is the Max plan?* <https://support.claude.com/en/articles/11049741-what-is-the-max-plan>
  (the Pro plan's article, 8325606, says the same)
- C2: *Manage costs effectively* <https://code.claude.com/docs/en/costs>
- C3: *Error reference* <https://code.claude.com/docs/en/errors>
- C4: <https://support.claude.com/en/articles/11647753-how-do-usage-and-length-limits-work>
- C5: <https://support.claude.com/en/articles/12005970-manage-usage-credits-for-team-and-seat-based-enterprise-plans>
- C6: <https://support.claude.com/en/articles/12429409-extra-usage-for-paid-claude-plans>
- C7: <https://code.claude.com/docs/en/commands>
- C8: <https://code.claude.com/docs/en/statusline>
- C9: `src/claude_agent_sdk/types.py` in <https://github.com/anthropics/claude-agent-sdk-python>, and
  <https://code.claude.com/docs/en/agent-sdk/python>
- C10: `CHANGELOG.md` in <https://github.com/anthropics/claude-agent-sdk-typescript> (npm's latest, 0.3.287)
- C11: <https://github.com/anthropics/claude-code/issues/50518>
- X1: <https://learn.chatgpt.com/docs/pricing> (redirected from `developers.openai.com/codex/pricing`)
- X2–X5: `codex-rs/protocol/src/protocol.rs`, `codex-rs/app-server-protocol/src/protocol/common.rs`,
  `codex-rs/exec/src/exec_events.rs` and `codex-rs/protocol/src/error.rs` in <https://github.com/openai/codex>
- T1: <https://github.com/realiti4/claude-swap>; T2: <https://github.com/fairy-pitta/cc-account-switcher>;
  T3: <https://github.com/PhanTrongGiap/codex-rotator>;
  T4: <https://router-for-me-cliproxyapi.mintlify.app/concepts/authentication>
- T5: <https://docs.litellm.ai/docs/routing>, <https://docs.litellm.ai/docs/proxy/reliability>
- T6: <https://openrouter.ai/docs/guides/routing/provider-selection>,
  <https://openrouter.ai/docs/guides/routing/model-fallbacks>
- L1: <https://code.claude.com/docs/en/legal-and-compliance>; L2: <https://www.anthropic.com/legal/consumer-terms>;
  L3: <https://www.anthropic.com/legal/aup>

## 1. What *well* means: five aims that pull apart

No single policy is best, because a person with several accounts wants different things on different days, and Daoris
can see only part of each account (§0.2). These are the aims a setting serves:

| Aim | What serves it | What it costs |
|---|---|---|
| **A. Keep my own accounts fresh.** The person also works on these accounts, at their terminal and in the maker's own apps (observations 4 and 5) | Work stays on the first account until it is spent; the others are touched only then | When the first account meets its limit, every session on it is cut off together |
| **B. Lose as little work as possible to a limit.** A cut-off loses the agent's own conversation (TOOL4f hands on the last plan and last words, not the context) | Sessions running at once on different accounts, so a limit cuts off only its own; and a start that passes an account about to meet its limit | Windows opened on accounts the work did not yet need |
| **C. Get the most done in a week.** Windows reset on the maker's clock, and nothing read says what a window leaves is kept (§0.3) | Starting on the account with the most left, or on the one whose allowance lapses first | Every account is in use, and a quest's sessions run on several |
| **D. Keep work readable.** A quest's sessions read on one account; a workspace's spend sits on its own account | One account per workspace, and a quest's carry-ons on the account it started on | The workspace waits when its account cools |
| **E. Keep work where it belongs.** A work repository may run only on the work account, or on it first | The person's list for that workspace, and nothing else | Work waits rather than moving to an account the person did not list |

**E is never Daoris's call** (the brief's own words): it is stated by the person, shown, and never inferred from an
address, a name, a plan or where work last ran. A, B, C and D are traded against each other by settings the person
chooses, and the default is D125's: A and D first.

## 2. The decision in one table

> **Amended by §16 (2026-10-02).** *Start on* and *sessions at once* become one setting, *use accounts*, whose
> default, `goal`, is §16.3's walk, and whose other value, `order`, is D125's. *Switch before the limit* is on by
> default. The JSON is §16.6's. The list, rules 1–6 and the kept account stand.

Per agent, in each **scope** (the machine, or one workspace), the person states:

| Setting | Values | Absent | What it answers |
|---|---|---|---|
| **The list** (D125's order) | the accounts that scope's starts may run on, in order; one or more, any number | none: D125's behaviour, byte for byte | E: which accounts may run this work, and in what preference |
| **Start on** (`prefer`) | *List order* (`list`), *Most left first* (`left`), *Soonest reset first* (`soonest`) | `list`: D125's walk | A and D against C (§4.3–§4.5) |
| **Sessions at once** (`parallel`) | *One by one* (off), *In parallel* (on) | one by one | A and D against B (§4.2, §7) |
| **Keep for conversations** (`keep`) | one account of the list | none | that driven work never spends the last account the person talks on (§4.6) |
| **Switch before the limit** (`early`, `near`) | on or off; a percentage for *near* where the agent gives only a number | off; 90 | B: a start passes an account its agent said is near its limit (§6) |

*List order, one by one* is D125's rotation as built. *In parallel* is the owner's *parallel*; *Most left first* and
*Soonest reset first* are their *other logic*, with *Keep for conversations* beside them. The two choices compose:
*Most left first, in parallel* spreads the sessions running at once and, among accounts equally busy, starts on the
one with the most left.

The rules that bind them:

1. **A start reads one scope**: its workspace's, when that workspace names a default or a list of its own for the
   agent; else the machine's. A scope with no list is its one account, D125's *none*, byte for byte. A workspace's list
   comes with its own settings, never mixed with the machine's.
2. **The list is the whole set** (§3). An account a scope does not list never carries one of its starts. The person's
   pick is the one exception: it is their own act, each time.
3. **The default says where a start begins within the list**; a scope that lists accounts and names no default in it
   begins at its first.
4. **Every choice is made at a start, never inside a running session or conversation** (D125 §3.2).
5. **Nothing here spends more than D125 already may**: every account any setting reaches is one the person listed,
   and no setting starts work that would not have started anyway.
6. **Nothing counts accounts.** Every rule reads the list it is given, of whatever length; with one account each
   setting falls back to that account, and a door never refuses a list for its length.

In `harnesses.json`, beside D125's two lists, which keep their shape (`account-N` stands for however many accounts
the agent has):

```json
{
  "rotation": { "claude-code": ["account-1", "account-2", "account-N"] },
  "rotationUse": { "claude-code": { "prefer": "left", "parallel": true, "keep": "account-N" } },
  "workspaceRotation": { "work": { "claude-code": ["account-2", "account-1", "account-N"] } },
  "workspaceRotationUse": { "work": { "claude-code": { "early": true, "near": 85 } } }
}
```

## 3. Which accounts may run a workspace

### 3.1 How it is stated

**By the workspace's own default and list.** *Only the work account*: the workspace's default is `account-2` and it
lists nothing more (or lists `account-2` alone). *The work account first, personal accounts after*: it lists
`account-2`, then the others. *Whatever this machine uses*: it names neither a default nor a list, and the machine's
apply. Three statements, one list, no second register (D125 §9: *a do-not-rotate list beside the order*). On this
machine the owner has said every account may run the work workspace (2026-10-02): stated as that workspace's list
naming every account, or by leaving it on the machine's accounts when the machine's list names every account.

This amends D125 §3.1 in two places:

- **A workspace that names its own default and no list rotates nowhere.** Today it takes the machine's list
  (§0.1 🔴). That was an inference: the person said which account runs the workspace, and nothing about others.
- **A workspace that has a list starts within it.** Its default, when it names one in the list; else the list's first.
  Neither the machine's default nor the tool's own sign-in runs a workspace whose list names accounts.

The same holds for the machine's scope, so one rule reads both: a scope's list is the accounts its starts may run on,
and its default, or else its first, is where they begin. The tool's own sign-in is never in a list (D125 §3.7), so it
runs only where no default and no list name an account, which is D48 §2a's *none*, untouched.

**A default outside its scope's list is refused** at both doors: `profile default --workspace W` refusing an account
W's list does not hold, and `profile order --workspace W` refusing a list without W's default, each naming the other
and the fix. A file edited by hand that holds one is read with the list winning (the start begins at its first), and
`daoris agent list` names the conflict.

### 3.2 How it is shown

- **Settings → Agents, under each workspace**, where the screen sets that workspace's default account (D125 §6): a
  choice, *This machine's accounts* or *Its own accounts*, and under the second the workspace's list with up, down and
  *Use* on each row, and its own settings. A workspace on the machine's accounts shows which those are.
- **Each account's row** names the workspaces that may run on it, as `harness.profile.workspaceUses` names the
  workspace that defaults to it today.
- **The session's head** names the account it ran on (D125 §3.6), and a start that began somewhere other than its
  default says why in its record's first line (TOOL4f), now with the scope and the setting: *opened on `account-N`:
  `work` lists it, and `account-2` is cooling until 07:52 (<zone>)*, or *opened on `account-1`: most left first, and
  it said it is clear*.
- **The conversation picker** lists the scope's accounts first and every other account under *not in this
  workspace's list*, so a pick outside it is never made by accident.
- **`daoris agent list`** prints each workspace that names a default or a list, beneath the machine's, with its
  settings.

### 3.3 When the question comes up: the wait that asks

The owner's question arose because a workspace's only account was spent and others were not. Under §3.1 that
workspace waits (D125 §4), and the wait names what the person could do:

> *every `claude-code` account `work` may use is cooling; the first ready, `account-2`, at Oct 3, 16:02 (<zone>), as
> the agent said. Not cooling, and not in `work`'s list: `account-1`, `account-N`. Daoris starts nothing on them until
> then.*

with the doors that answer it once: *Let `account-1` run `work`…* on the *What needs you* row (it adds the account to
the workspace's list, after a confirm naming what that lets Daoris spend), `daoris agent profile order claude-code
account-2 account-1 --workspace work`, and Ask Daoris's `order` card. The accounts are named from the cool-offs and
AGT3b's refusals alone, since a waiting look starts no process (D125 §4); whether one is signed in is the probe's,
asked when the person presses. The row is said once per wait, as D125 §4's attention is. **Daoris never takes the
answer itself.**

## 4. How a list is used

> **Amended by §16 (2026-10-02).** The default walk is §16.3's, not §4.1's. Its step 5, *a quest decides once*, is
> gone: a carry-on carries no conversation, so its account buys only readability (§16.2). In parallel (§4.2) is the
> default's step 3. Most left first (§4.4) folds into §16.3's step 5, pace, which is what is left measured against the
> week's time; soonest reset first (§4.5) folds into its step 4, bounded to a week's last day. List order (§4.3) is
> `order`'s walk and the goal's last word. Round robin by start, rejected in §4.7, returns as step 6, least recently
> started, for §16.2's reason. The kept account (§4.6) stands.

### 4.1 The walk, in order

Every start the list may carry is chosen by one pipeline, each step stable (it keeps the order it was handed where it
has no reason to change it):

1. **The sequence**: the scope's list, begun at the start's account (§3.1) and wrapping, as D125's walk is.
2. **Keep**: a driven start drops the kept account (§4.6).
3. **Start on**: `list` leaves the sequence as it is; `left` orders it by what each account's agent last said is left
   (§4.4); `soonest` by the earliest reset each account's agent last named (§4.5).
4. **Sessions at once**: *in parallel* orders by how many of Daoris's sessions run on each account now, fewest first,
   so step 3's order breaks ties among accounts equally busy (§4.2).
5. **A quest decides once**: in every setting but *list order, one by one*, a carry-on (D80) or a resume (D79) moves
   the account its last session ran on to the front, when that account is still in the list, ready, and not near its
   limit. So a quest's sessions read on one account (D). A limit makes that account not ready, so the carry-on after a
   limit still moves. *List order, one by one* keeps D125's rule, byte for byte: back to the default once it is ready.
6. **Switch before the limit**: with it on, an account its agent said is near goes to the end (§6).
7. **The first ready account runs**: not cooling, not refused, signed in (D125 §3.3). None ready is D125 §4's wait.

### 4.2 One by one, or in parallel

**One by one** (`parallel` off, the default) is the sequence as step 3 leaves it: the first ready account carries
every start until it is not ready. Under *list order* it is D125's walk, unchanged, and serves A and D; its cost is B,
since one limit cuts off every session running on that account (§0.2).

**In parallel** (`parallel` on) puts each start on the ready account running the fewest of Daoris's sessions: a
gateway's *least busy* (T5), with the person's list as its priority. One session at a time runs exactly as *one by one*
does; only sessions running at once go to different accounts, so the sessions spread across every account the list
allows, up to the cap (§7).

- **Counted, not guessed.** The count is the live sessions whose records name the account (loopback, D47 §4),
  conversations and Ask Daoris included, plus the starts this look has already chosen. The person's own use outside
  Daoris is not counted, because Daoris cannot see it (§0.2), and the screen says *Daoris's sessions*.
- It serves B and C. Its cost is A: a second session opens a second account's window while the first account could
  have carried it.

**This answers D125 §9's rejection of spreading** (*it spends windows the work did not need, and makes a quest's
sessions harder to read*) on both counts, without reversing it: spreading is never the default; it spreads only what
runs at once, which is what a limit cuts off together; and a quest stays on its account (step 5).

### 4.3 List order

The person's list, as written, is the preference: the account they put first is the account they would rather spend.
It is the default because it is the only order Daoris can follow knowing nothing about any account, and because it is
D125's.

### 4.4 Most left first

The owner's *how many token left*. `left` orders the sequence by what each account's agent last said about its
windows (§5.2), in four standings, the sequence's own order breaking every tie:

1. **Clear, with how much is used**: lowest first, by the window most used (Codex's `used_percent`, X2).
2. **Clear, said without a number** (Claude Code's `allowed`, which gives `utilization` only past a threshold, C11).
   How a clear word with no number ranks against a clear number is the agent's own table's (§5.2), since each agent
   states its numbers differently; one agent's accounts are never ranked against another's, as a list is one agent's.
3. **Nothing said** since its window last reset: unknown (§5.3).
4. **Near**: its agent's warning word, a number at or over *near*, or its word that it is drawing on usage credits
   (§6).

A cooling account is not ready and is never ranked. **With nothing said by any account, `left` is list order**, and the
field says so (§5.3). It serves C. Its cost is D: work moves to whichever account has the most left, and with *one by
one* every start goes to that account until its agent says otherwise.

### 4.5 Soonest reset first

The account whose allowance lapses first is spent first: what a window leaves at its reset is not kept (§0.3), so
spending it before it goes is C at its sharpest. `soonest` orders the sequence by the earliest reset each account's
agent last named for a window that still has room (§5.2), sooner first; accounts with no reset known come after, in
the sequence's order. A weekly reset, once an agent has stated it, is kept as that account's (the maker fixes it per
account, C1), so the weekly window is known for every account that has ever stated one; a five-hour window's reset is
known only from the agent's field, since when it starts was not read on any maker's page (§0.3).

**With no reset known for any account, `soonest` is list order**, and the field says so. Its cost is that an account
nearly spent and resetting soon is chosen first: with *switch before the limit* off, a long session started there may
be cut off before the reset. That is the pairing §6 is for.

### 4.6 Keep for conversations

One account of the list on which **driven work** (a driven start, a carry-on, a resume, an intake, a set-up) never
starts. A conversation and Ask Daoris walk the list as before, with the kept account in its place, so they reach it
only when the accounts before it are not ready: it is the account left when driven work has spent the others.

- **Refused when it would stop driven work**: keeping a list's only account. The door says so and names the fix.
- **A driven start whose only ready account is kept waits**, and its sentence says so: *`account-N` is kept for
  conversations*.
- **Kept and the default together** put conversations and Ask Daoris on that account first, and driven work on the
  rest of the list: the way to say *conversations run here*, with no setting of its own.
- **Not the tool's own sign-in.** Keeping an account away from Daoris altogether is already one act: sign it in at the
  terminal only, as the tool's own sign-in, and give Daoris accounts of its own (D125 §3.7). Once the machine's default
  or list names an account, Daoris never spends that sign-in.

### 4.7 What is not offered, and why

- **Round robin by start** (CLIProxyAPI's *round robin*, T4). It moves a lone session across accounts for no limit and
  no load, which costs A and D and buys nothing B needs.
- **A share per account** (*account-1 carries half*). Daoris measures neither an account's allowance nor all of its use
  (§5.1), so a share would be of Daoris's sessions only, while the limit is on everything.
- **Ranking by Daoris's own measured use** (§5.1): an account the person spent outside Daoris would look fresh.
- **A probe to refresh an idle account's word** (§5.3).

## 5. What Daoris knows of what is left

> **Amended by §16 (2026-10-02).** What Daoris knows, and that absent is never zero, stand. Where this section says
> `left` and `soonest`, read §16.3's steps 5 and 4; with nothing said, the default falls back as §16.4 says, not to
> list order alone.

### 5.1 Each source, and what it can say

| Source | What it says | Of what is left | Used by |
|---|---|---|---|
| The agent's own field about its windows, on the door its session runs on: Claude Code's `rate_limit_event` (C9), Codex's `rate_limits` (X2) | per window: clear, near or refused, or how much is used; and when it resets; said as the agent works, at no extra call | **The only word on what is left**, as the agent says it, as of when it said it | `left`, `soonest`, *switch before the limit*, once a door is recorded carrying it (TOOL4b) |
| The resets the agents name | a limit sentence's reset (D125 §2); the field's `resets_at`; a weekly reset fixed per account (C1) | when a window turns over, never how much it holds | the cool-off; `soonest`; the anchor for Daoris's measured share |
| Where past limits struck (`account.limited`, `cooling.json`) | which account met which window's limit, when, at which turn and context | history: nothing about the current window | the report (§5.4) |
| Measured usage per window: `usage.json`, and the native `result`'s token counts | Daoris's sessions' context and, on the native door, tokens per turn, summed per account over a window whose start a stated reset anchors | Daoris's share only, a floor on what the account used: no allowance is published to subtract it from (§0.3), and one allowance covers every surface (C4) | the account rows and the report; never a ranking |
| The agent's usage command, `/usage` (C7) | what is left, as text, to a person | the same as the field, unreadable | nothing: a door with text alone is never read (D125 §1.2), and a process started only to ask spends one |
| Claude Code's status line (C8) | `rate_limits`' percentages and resets, handed to a command the person configures | the same | nothing: it runs in the person's interactive terminal, which a driven session is not, and only for some plans |
| Codex's app server, `account/rateLimits/read` (X3) | the same, on request | the same | nothing: Daoris speaks the protocol door to Codex (`codex-acp`), not its app server, and a request made only to ask is D125 §1.4's refusal |

**What Daoris cannot know**: the size of any allowance, which no maker publishes as an amount (Codex's are estimates
in messages, X1); the person's own use of an account, at their terminal or in the maker's apps (C4); when a five-hour
window started, which no maker page read says; what a session will spend before it starts; and anything about an
account no session of Daoris's has run on since its window last reset.

### 5.2 The readings

**What an agent says about its windows is kept per account**, in `windows.json` beside `cooling.json` under the home:
per agent and account, per window the agent names, its standing (clear, near or refused, and how much is used where it
says), its reset, when it was seen, and the session it was seen on. Never the agent's words, a key or who signed in.

- **Written by the driver** as a session's door carries the field, atomically, the newest reading of a window
  replacing the older. Missing or unreadable is nothing said. No HTTP route.
- **A reading is a floor, as of when it was seen.** Within a window use only grows, and the person may have added to
  it since; so a reading says *at least this much used*, and the account's row shows its age (*clear, said 3 h ago*).
- **Gone at its reset**: once a window's reset passes, its reading is dropped and the account is unknown for that
  window until a session says again. A weekly reset is kept beyond that, moved on by a week, since the maker fixes it
  per account (C1).
- **Read by a table per agent**, as D125's limits are: which field means clear, near, refused and how much, and how a
  clear word without a number ranks. **An entry grows only from a frame recorded on Daoris's door** (TOOL4b), and an
  agent with no entry says nothing, so `left`, `soonest` and *switch before the limit* fall back as §5.3 says.

### 5.3 When Daoris does not know

**Absent is never zero** (D57), and never full either:

- **An account with nothing said is unknown**: ranked after every account said to be clear and before every account
  said to be near (`left`), and after every account with a reset known (`soonest`). It is neither treated as spent nor
  as fresh.
- **With nothing said by any account the list may use**, `left` and `soonest` are list order, exactly. The *Start on*
  field says so under the choice: *no account has said what it has left yet; until one does, Daoris starts in list
  order*. `daoris agent list` says the same.
- **No probe is made to find out.** A start to ask would spend the account's allowance to learn it, would not see the
  person's own use anyway, and the word arrives at no cost with the next session that runs there. An idle account's
  standing stays unknown until then, and an unknown account is used in its list place, which is how it gets said.
- **Today every agent says nothing**: no door is recorded carrying the field (§0.1, §0.3). So until TOOL4b and TOOL6c,
  `left` and `soonest` can be chosen, and start in list order, and say so.

### 5.4 Learning an account's capacity: reported, never acted on

The brief asks whether Daoris could learn where an account's limit lies from where its limits struck. **It reports
it, and acts on none of it.** A fact gates, a judgement reports (D54), and a capacity learned here is a judgement on
four counts: Daoris sees only its share of what the account spent, since one allowance covers every surface (C4);
what it measures is context and Daoris's tokens, not the account's spend (§5.1); the makers publish windows, not
amounts, and the windows differ by plan and model family (§0.3); and a limit sentence says when, not how much. Acting
on it would cool an account that is not spent, or miss one that is: D125 §1.4's reason for never guessing from token
counts.

So TOOL4h's usage report gains, per account and window kind: the limits met, and before each, how many of Daoris's
sessions had started on that account since its previous reset, for how long they ran, and the tokens the native door
counted. Labelled *Daoris's sessions only*, and never a capacity, a share or a price.

## 6. Switching before a limit

> **Amended by §16 (2026-10-02).** On by default, under both values of *use accounts*. It is inert until a door
> carries the agent's word, so on costs nothing today; the switch is shown on and says it is waiting for that word.

**Switch before the limit reads one thing: what the agent says about its own windows, on the door its session already
runs on** (§5.2). It is D125 §1.2's *a field beats a sentence*, read before the limit instead of at it.

- **What counts as near**: the agent's own word for it, where its field has one (Claude Code's `allowed_warning`, C9);
  where the field carries only how much of a window is used (Codex's `used_percent`, X2), at or over **90%**, settable
  per scope, the higher of the two thresholds the rotating tools use (T1, T2). The agent's word wins over the number
  wherever both exist. So does the agent's word that the account has begun drawing on usage credits, which are billed
  (C5, C6): its included allowance is spent, and another account's may not be.
- **At a start**, with it on, an account said to be near goes to the end of the candidates, whatever *Start on* and
  *Sessions at once* say: **passed while another account is ready, run when none is.** Near is not spent. It never
  makes a start wait, never cools an account, and never stops a running session.
- **Said**: the start's first line (*opened on `account-2`: `account-1` said it is near its weekly limit*), the
  account's row (*near its limit*, with the window and its reset), `daoris agent list`, and `account.near` and
  `account.rotated`'s `why` in the log (§13).
- **Until a door is recorded carrying the field, nothing is read** and the switch is shown off and disabled, saying
  *Claude Code's sessions here do not say how near their limits are*. That is today for every agent (§5.3).

**What it costs**: another account's allowance, spent while the first still had some. That is the price of not
cutting a long session off mid-turn: observation 1's turn was twenty minutes and 370k tokens of context when it was
refused.

## 7. Sessions at once

> **Amended by §16 (2026-10-02).** Spreading what runs at once is the default (`goal`, step 3); one by one is `order`.
> The arithmetic below stands for each.

**The cap stays one number for the machine, K, and sessions split across accounts only *in parallel*.** With N
accounts the list lets a start use:

- **One by one**: up to K sessions run on the first ready account, and one limit cuts off all of them.
- **In parallel**: no account runs more than ⌈K ÷ N⌉ at once. When N is at least K, every running session has an
  account of its own, the first K of the sequence; when N is 1, *in parallel* is *one by one*. One limit cuts off at
  most ⌈K ÷ N⌉. On the install, K is 4: with N accounts, a limit takes at most ⌈4 ÷ N⌉ sessions instead of 4.
- **A kept account** leaves N − 1 accounts for driven work, and the same arithmetic holds for them.

Three more rules:

- **No cap per account.** A second number would have to agree with K and with N, and *in parallel* already splits what
  runs at once by the one count that matters. None of the pages read documents a limit on sessions at once per account
  (§0.3), so a cap per account would answer no stated constraint.
- **Starts in one look are counted as they are chosen**, so a look that starts several quests *in parallel* does not
  put them all on the account that was emptiest when it began.
- **On Codex the cost differs**: a limit lets the running turn finish (X1), so what one limit takes is the next turn
  of each session on the account, not the turn in flight. *In parallel* still splits what the next limit refuses.

## 8. What each choice costs

> **Amended by §16 (2026-10-02).** A carry-on no longer stays on its quest's account (§16.2); its first line says which
> account and why. *In parallel*, *most left first* and *soonest reset first* are now steps of `goal`, whose cost is
> aims A and D; `order` costs aim B (one limit stops every session on its account). The other rows stand.

| Choice | What it costs | What keeps the cost visible |
|---|---|---|
| A conversation on a spent account | It cannot move mid-turn, nor between turns without losing the agent's conversation, which lives in the first account's home and is never copied (D125 §3.5) | The conversation's refused turn says the account is cooling and offers *Continue on `account-2`*: a new conversation, opened on the next ready account of its scope and handed the last plan and last words as a carry-on is (TOOL6d). The person's press, never automatic |
| A carry-on on another account | A fresh conversation from the tree and Daoris's record; the agent's own context is lost (TOOL4f) | Its first line names the cut-off session and why it moved; outside *list order, one by one* a quest stays on its account while that account is ready (§4.1) |
| *In parallel* | Windows opened on accounts the work did not yet need; the person's other accounts touched sooner | The account rows show Daoris's running sessions per account |
| *Most left first* | Work moves to whichever account has the most left, so a workspace's spend and a quest's first start may land anywhere in the list | The start's first line names the account and why; with nothing said, it is list order and says so |
| *Soonest reset first* | An account nearly spent and resetting soon is chosen first | The start's first line; the pairing with *switch before the limit* (§4.5) |
| *Keep for conversations* | One account's allowance is out of driven work's reach, so driven work waits sooner | The wait names the kept account |
| *Switch before the limit* | Another account's allowance spent while the first had some left | The start's first line, `account.near`, and `account.rotated`'s `why` |
| A workspace's list | Its work waits rather than moving to an account it does not list | The wait names the accounts outside the list that are not cooling, with the door to add one (§3.3) |
| The tool's own sign-in | Shared with the person, moved when they sign in elsewhere, unreadable in a record (D125 §3.7) | Never in a list, never kept, never switched to; §3.7's line while starts run on it |

## 9. The doors (D50, D110), in both languages (D116)

> **Amended by §16 (2026-10-02).** *How a list is used* is §16.6's: on the screen *Use accounts*, *Make the most of
> them* or *One by one, in order*, beside *Keep for conversations* and *Switch before the limit*; on the terminal
> `daoris agent profile use <agent> [goal|order] [--keep <account>|--no-keep] [--early on|off] [--near <percent>]
> [--workspace W]`, with `--prefer` and `--parallel` gone; Ask Daoris's `use` door takes `use`, `keep`, `early` and
> `near`. The names *Start on*, *List order*, *Most left first*, *Soonest reset first*, *Sessions at once*, *One by
> one* and *In parallel* are withdrawn; §16.6 proposes theirs. Everything else in this section stands.

| What | The screen (TOOL4g's) | The terminal | Ask Daoris |
|---|---|---|---|
| A scope's list | the account rows in order, up, down and *Use* (D125's *Rotate*, renamed: the switch now means *may run this scope's work*) | `daoris agent profile order <agent> <account>… [--workspace W]`, `--clear` (built, TOOL4e; §3.1's refusals added) | the agent kind's `order` door (TOOL4g) |
| A workspace on the machine's accounts or its own | *This machine's accounts* / *Its own accounts*, under the workspace | a default or a list with `--workspace W` is its own; clearing both returns it to the machine's | the `order` door, and the default's |
| How a list is used | *Start on* (*List order* / *Most left first* / *Soonest reset first*), *Sessions at once* (*One by one* / *In parallel*), *Keep for conversations* on one account's row, *Switch before the limit* with its *near* | `daoris agent profile use <agent> [--prefer list\|left\|soonest] [--parallel on\|off] [--keep <account>\|--no-keep] [--early on\|off] [--near <percent>] [--workspace W]`; with no flag it prints the scope's settings and what each account last said | the agent kind's `use` door, with the same fields |
| What each account is doing | its row: running now, cooling until, what it last said and when, kept; and, under the agent's accounts, *each account's own plan and terms apply* (§0.3) | `daoris agent list` | the room's machine facts |
| The wait that asks | *Let `account-1` run `work`…* on *What needs you* | the `order` command the wait's sentence names | the `order` card |
| A conversation cut off by a limit | *Continue on `account-2`* on the refused turn | `daoris-driver` has no conversation door; none is added | a reason: the person's press, as answering is |
| The cap | none yet, as the room's doors table says (unchanged) | `daoris driver cap <n>` (unchanged) | the setting kind's `cap` (D110) |

- **Each change that widens what Daoris may spend is a card the person applies** (D89): adding to a list, *in
  parallel*, *most left first*, *soonest reset first*, *switch before the limit*. Each screen control is held to its Ask
  Daoris answer by `HelpCoverageTests` (D110), and each terminal door is a row of the room's doors table
  (`HelpRoomDoors`). `daoris driver` gains no verb: the cap is the one machine-wide dial, and every new choice is per
  agent and per scope.
- **No door, screen or terminal, assumes a number of accounts**: a list editor holds as many rows as the agent has
  accounts, and every sentence names the accounts it means rather than counting them.
- **The names, proposed** (TOOL4g designs them against the kinds' budgets and enters them in the glossary, which the
  names check holds):

| Concept | Kind | English | Chinese |
|---|---|---|---|
| a list's switch on an account's row | choice | *Use* | 使用 |
| which account a start begins on | field | *Start on* | 起始账户 |
| `list` | choice | *List order* | 列表顺序 |
| `left` | choice | *Most left first* | 余量优先 |
| `soonest` | choice | *Soonest reset first* | 最早重置优先 |
| sessions at once | field | *Sessions at once* | 同时会话 |
| one by one | choice | *One by one* | 逐个用满 |
| in parallel | choice | *In parallel* | 并行使用 |
| the kept account | button, status | *Keep for conversations*, *kept for conversations* | 留作对话 |
| switch before the limit | field | *Switch before the limit* | 提前切换 |
| near | status | *near its limit* | 接近上限 |
| a workspace's scope | choice | *This machine's accounts*, *Its own accounts* | 本机账户、自有账户 |
| the wait's door | button | *Let {{account}} run {{workspace}}* | 允许 {{account}} 运行 {{workspace}} |
| a conversation's door | button | *Continue on {{account}}* | 换到 {{account}} 继续 |

The agent's own sentences, wherever shown, are content and are not translated (`translation-parity`). The account is
账户, never 账号 (glossary).

## 10. The build

Rows ready for `TASKS.md`. D130 decides all of it; a row takes a decision number only if building it finds something
D130 did not decide. A row with a `Process`-half case, a rehearsal or a look in its proof is proven by the parent at
merge. Four rows are new; four of D125's rows are amended rather than duplicated.

> **Amended by §16 (2026-10-02).** TOOL6a takes §16.6's vocabulary (`use` in place of `prefer` and `parallel`;
> `early` on when absent). TOOL6b builds the goal's walk with nothing said (§16.3's steps 1, 3, 4, 6 and 7, and
> `order`), and starts `windows.json` with the weekly resets limits tell. TOOL6c adds the agents' word: steps 2 and 5,
> and step 4 from readings. TOOL4g, TOOL4h and TOOL4i follow. The rows below are the amended ones.

| Row | What and why | Contract | Proof |
|---|---|---|---|
| **TOOL6a** | **The settings and their terminal door** (cli; driver). `use` (`goal` or `order`), `keep`, `early` and `near` per scope in `harnesses.json`, both twins; `daoris agent profile use`; §3.1's refusals on `profile order` and `profile default`. So the person's overrides have a file and a terminal before the walk reads them | §2, §3.1, §4.6, §14, §16.6 | Twin tables both sides; each writer keeps the other's sections; absent `use` is `goal`, absent `early` on; the refusals, none by a list's length; `agent list`'s lines; the doors table; `node --test`; the golden usage |
| **TOOL6b** | **The goal's walk, with nothing said** (driver; after TOOL6a). §16.3's steps 1, 3, 4, 6 and 7, and `order`'s walk; a weekly reset a limit tells, kept in `windows.json` and carried a week where the agent's table says fixed; each start's first line and `why`. So every listed account is used toward the goal before any agent says what it has left | §3, §7, §16.2–§16.4; §13's log fields | `AccountRotation` tables per step, one account and many, failing first. `Process` tick: cap K, N stub accounts, at most ⌈K ÷ N⌉ each; a limit cuts off only its own; a learned weekly reset ranks first in its last day |
| **TOOL6c** | **What the agents say, read** (driver; after TOOL4b records a frame, and TOOL6b). The readings table per agent, `windows.json`'s readings, steps 2 and 5, step 4 from readings, and `account.near`. So a start weighs each account's week by the agent's own word and passes one near its limit | §5.2, §5.3, §6, §16.3 | Fast-half tables: near by word and by percent, pace by used and elapsed share, unknown between behind and ahead, a reading gone at its reset; a `Process` tick replaying the recorded frame |
| **TOOL6d** | **A conversation continues on another account** (driver; modules; web-shell; after TOOL6b). A refused turn offers *Continue on `account-2`*, a new conversation in its scope handed the last plan and last words, so a person's conversation on a spent account is not a dead end | §8, §9 | Driver tests for the opening and what it is handed; modules route tests (MOD5); vitest over a mocked bridge; the look in both languages |
| **TOOL4b**, amended | Also: `rate_limit_event`'s fields as Claude Code sends them, on an ordinary turn and at a warning; whether its protocol door's adapter forwards them; and whether `codex-acp` forwards Codex's `rate_limits` and its limit sentence. Read keylessly from shipped code, labelled unmeasured, then the first real frame of each recorded | §0.3, §5.1, §5.2 | The evidence document. TOOL6c, and a Codex entry in D125's table, build only on what it records |
| **TOOL4g**, amended (after TOOL6b) | The screen also carries §3.2, §9 and §16.6: a workspace's *This machine's accounts* / *Its own accounts* and its list; *Use* for *Rotate*; *Use accounts*, *Keep for conversations* and *Switch before the limit*; §16.4's line while no account has said; each row's running count, week reset, pace or near, and its age; the terms line; the picker's split; the wait's *Let … run …*; Ask Daoris's `use` door | D125 §2.4, §3.7, §4, §6; §3.2, §9, §16.4, §16.6 here | As D125's TOOL4g, plus the new doors in `HelpCoverageTests` and the names check; the look with one account and with many, both themes and both languages |
| **TOOL4h**, amended | The report carries §16.7's measures per account and week: sessions landed, hours waited, limit cut-offs, allowance left at each weekly reset where the agent's word shows it, the limits met, the starts each step moved, and the cap's share; labelled *Daoris's sessions only* where so. The rehearsal adds the goal's walk over N stub accounts | §5.4, §16.7 here; §8, §2.2 of D125 | The report's parse table; the rehearsal phase |
| **TOOL4i**, amended | The owner's run is under `goal` over all their accounts, for at least one of each account's weeks, recorded against §16.7's measures: which account each start took and why, what each limit cut off, and what each agent said about its windows | §11, §16.7 | The evidence, with the owner present |

**Order.** TOOL6a, then TOOL6b, in the driver lane after TOOL4f (landed). TOOL4g after TOOL6b, so the screen is built
once. TOOL6d after TOOL6b. TOOL4b any time; TOOL6c only on its frame. **TOOL6b alone puts every account to work toward
the goal**: each workspace's list says which accounts may run it, the walk spreads and rotates across them, and a
weekly reset learned from a limit is spent before it lapses. **TOOL6c answers *how many token left*.**

## 11. What a rehearsal can prove, and what only a real run can

**A rehearsal proves the mechanism**, with no model and no account: a workspace's list holding its work off an account
it does not list, the wait naming that account, *in parallel* putting sessions at once on different stub accounts, a
limit cutting off only its own, a carry-on staying on its account, a kept account untouched by driven work, a replayed
reading ordering `left` and `soonest`, every door, each with one account and with many.

**Only a real run proves the rest:**

1. **Whether one limit cuts off every session on its account at once**, as observation 2 suggests for two.
2. **What the agent's field about its windows carries on each door**, and whether the protocol door forwards it
   (TOOL4b). The maker's declaration (C9) is not a frame recorded on Daoris's door.
3. **Whether *near* comes early enough** to keep a long session from being cut off: when `allowed_warning` arrives
   relative to the limit, and whether 90% is the right number where the agent gives no word.
4. **Whether *most left first* or *in parallel* gets more done in a week than *one by one*** on the owner's accounts,
   from the report.
5. **Whether a team seat's window comes back at the reset its spend-limit sentence names** while its credits stay
   paused to the month's end, as C5 reads observations 1 and 2.

## 12. Considered and rejected

> **Amended by §16 (2026-10-02).** Three rejections below are reversed or replaced on the goal: *spreading as the
> default* (it is the default now), *round robin by start* (least recently started is the goal's step 6, with no
> readings), and *one choice of four modes* (the modes are one setting with two values, `goal` and `order`). §16.5
> gives each reason, and §16.9 adds what the goal rejects.

- **Inferring which accounts may run a workspace**: from the signed-in address, the account's name, the workspace's
  name, a team plan, or where its work last ran. The brief says the decision is the person's.
- **A workspace that names a default inheriting the machine's list**, today's reading (§0.1 🔴): it moved work to
  accounts nobody listed for it.
- **Mixing scopes**: a workspace's list with the machine's settings. One scope per start, so the screen shows one block.
- **One choice of four modes** in place of two settings: it cannot say *most left first, in parallel*, which the
  owner's *one by one or parallel or other logic* reads as two questions.
- **Spreading as the default**: it costs A for every person who never asked for B.
- **Round robin, a share per account, a cap per account, ranking by Daoris's own use** (§4.7, §7).
- **Treating an account that said nothing as spent or as fresh** (§5.3): absent is never zero, and never full.
- **Probing an idle account to learn what it has left**: it spends the allowance it measures, misses the person's own
  use, and the word comes free with the next session (§5.3). **Polling for quota**, as the rotating tools do (§0.3): a
  request made only to ask (D125 §1.4).
- **Asking the agent's usage command before a start**: a text door, and a process spent to ask (§5.1).
- **Switching by replacing the active sign-in or its credential file**, as the rotating tools do: it moves the
  person's own terminal with it (D125 §3.7) and reaches inside an account's store (D49 §4, D66 §3).
- **A status line Daoris configures to catch `rate_limits`** (C8): it runs in the interactive terminal, which a driven
  session is not, and only for some plans.
- **Learning capacity and acting on it** (§5.4); **any price**, OpenRouter's price weighting included (D57, D24).
- **A cooldown with backoff after a refusal**, as CLIProxyAPI's: the agent states the reset (D125 §2).
- **Near as a wait or a cool-off**: near is not spent; holding a start on it would leave allowance unused and the
  queue stopped.
- **Switching a running session or conversation** to another account: the account is set at spawn, and the agent's
  conversation is not Daoris's to copy (D125 §3.2, §3.5).
- **A kept account as a list beside the list**: one account named of the list, refused when not a member.
- **Keeping or listing the tool's own sign-in**: Daoris neither chooses it nor sees it change (D125 §3.7).
- **Any rule or door that counts accounts**: the owner's three is how many they hold today, not a bound.
- **A new `daoris driver` verb**: every choice here is per agent and per scope; the cap stays the one machine dial.

## 13. What this amends

> **Amended by §16 (2026-10-02).** D125 §3.3 and §3.4's walk is §16.3's by default and D125's own under `order`.
> `account.rotated`'s `why` is `cooling`, `refused`, `signedOut`, `kept`, `near`, `fewest`, `lapsing`, `pace`,
> `leastRecent` or `list`, and it is written whenever a step put a start on an account other than the first ready one
> of the list. The owner's note under D130 that a quest's carry-on stays on its last account is amended: under `goal`
> a quest's next session goes where the walk sends it (§16.2).

- **D125 §3.1**: a start reads one scope; a workspace's list is the whole set of accounts that may run it; a workspace
  that names a default and no list rotates nowhere; a scope with a list begins at its default or its first; a default
  outside its scope's list is refused at both doors.
- **D125 §3.3 and §3.4**: the walk is §4.1's pipeline: it skips a kept account for driven work, orders by *start on*
  and *sessions at once*, keeps a quest on its account outside *list order, one by one*, and, with *switch* on, moves
  a near account to the end. The tool's own sign-in runs only where no default and no list name an account.
- **D125 §5.1 and §9**: spreading is a person's choice, never the default, and spreads only sessions at once.
- **D125 §6**: *Rotate* becomes *Use*; the `use` door joins `order`, `ready` and `cooloff`.
- **D94 §4**: `account.rotated` gains `why` (`cooling`, `refused`, `signedOut`, `kept`, `parallel`, `left`,
  `soonest`, `near`, `list`) and `scope`; `account.near`, once per account and window.
- **D110**: the `use` door.
- **Unchanged, and restated**: D57 (no price, no provider's console; absent is never zero), D58 as D125 amended it (a
  limit is no strike), D48 §2a (no account named is the tool's own sign-in), D49 §4 and D66 §3 (nothing read inside an
  account's directory; who signed in kept nowhere), D125 §1–§2 (the signal and the cool-off), §3.2 (only at a start),
  §3.7, §4 and §5.

## 14. The twins this creates

> **Amended by §16 (2026-10-02).** The `rotationUse` twin's keys are §16.6's: `use` (`goal` or `order`, else
> `goal`), `keep`, `early` (absent or `true` on, `false` off) and `near`. `prefer` and `parallel` are gone.

Added to `.claude/knowledge/twins.md` by TOOL6a, with their test tables:

- **`rotationUse` and `workspaceRotationUse` in `harnesses.json`**: `toolchain.ts` and `Harnesses.cs`. `prefer` is
  `list`, `left` or `soonest`, anything else `list`; `parallel` and `early` true or absent; `keep` an account of that
  scope's list, anything else none; `near` a whole percent from 50 to 99, anything else 90. A scope's settings are read
  only with its list. Each writer keeps the other's sections; both write the same bytes for the same wiring.
- **D125's order twin gains §3.1's rules**: a scope's default must be in its list when it has one; the refusals name
  both; a file that breaks the rule is read with the list winning; no list is refused for its length.
- **Not a twin**: `windows.json`, written and read by the driver alone; `daoris agent list` reads it as it reads
  `cooling.json`, and is held by the driver's own table for the file's shape.

## 15. What this document's gate does not cover

This change is documents only, and nothing is built.

- **Read from the code at `7cca0e1`**: `AccountRotation.cs`, `Harnesses.cs` (`SelectAsync`, `Before`, `Unready`,
  `ResolveFrom`, `ResolveRotationFrom`, `OrderProblem`), `AccountCooling.cs` (the log lines), `Usage.cs`, `Acp.cs`
  (`Measure`), `StructuredOutput.cs` (`rate_limit_event`), `Planner.cs` (the cap and `SessionView`),
  `DriverConfig.cs` (`Cap`, `CoolOff`), `ChatRunner.cs` (a conversation's and Ask Daoris's selection),
  `HelpRoomDoors.cs`, and the CLI's `cli/agent.ts`; and `docs/2026-09-25-stream-json-evidence.md` for the native
  frame that came on an ordinary turn.
- **The evidence is D125 §0.2's**, as given to that branch; no transcript or record was read here. That observation 2's
  two sessions shared one account rests on their one reset. The owner's answers were relayed to this branch by the
  parent.
- **The makers' pages** were read on 2026-10-02 and may change; §0.3 says which lines were read on the maker's own page
  and which were not. Not read on any maker's page: when a five-hour window starts, the date weekly limits began,
  the TypeScript SDK's declaration of `rate_limit_event`, the CLI's own field names on the wire, whether a model
  family's weekly limit resets with the overall one, every help.openai.com article (when a Codex window starts, its
  weekly reset, whether Codex shares ChatGPT's allowance), Codex's app server field names, OpenAI's terms, and any
  maker's rule on one person holding several accounts.
- **Found, and the owner's to weigh, not this design's**: the consumer terms' sentence on access *"through automated or
  non-human means"* (L2) bears on driven sessions as a whole, not on how accounts are chosen. It is named here so the
  owner reads it, and nothing in this design answers it.
- **The install's cap of 4** is the brief's; the default in the code is 2.
- **Not measured**: every item of §11.
- **`verify` checks** this document's links, the decision log's shape, the router's row, the budgets and the
  duplicates, and none of these words.

## 16. The rules around the goal (2026-10-02)

> The owner, 2026-10-02, after §1–§15 were written, in order: *"a quest can be running on different account if one
> account runs out of the limit"*; *"so the goal is to optimize the limit and usage of multiple accounts"*; *"you can
> design the rules around this purpose"*. This section designs the rules and the defaults around that goal. Where it
> and §2–§10 disagree, it wins; each place it changes carries a note. D130's note *The rules around the goal* records
> the decision.

### 16.1 The goal, stated so it can be judged

Over every account a scope lists, together:

1. **The most work done from their combined allowance**: the sessions that land.
2. **The fewest stalls**: the least time work waits because every account it may use is cooling, and the fewest
   sessions a limit cuts off mid-turn, each of which loses its work in flight and starts again from Daoris's record
   (TOOL4f).
3. **No allowance left unused at a reset**: what a window still held when it turned over.

So aims B and C of §1 become the defaults. Aims A (keep my own accounts fresh) and D (readable work) are no longer
defaults; the person can still choose them (§16.6). Aim E, which accounts may run a workspace, stays the person's
alone and is never inferred (§3).

**Since §1–§15 were written**: the first real rotation on the install moved a refused start to the next account, as
TOOL4f designed (D125's notes; the fix log, 2026-10-02). Its weekly sentence named the reset as a time of day once it
was under a day away, *your weekly limit resets 4pm (<zone>)*, where the same account had said *resets Oct 3, 4pm* the
day before: one account, one fixed weekly reset, as C1 says, told by a limit.

### 16.2 What the goal implies

- **Never hold a start the accounts could carry.** Rule 5 of §2 stands (nothing starts that would not have started
  anyway), and its other half is made a rule: no start is held while an account it may use is ready and a slot is
  free. So allowance lapses only where there was no work or no slot for it, and no rule here slows work down to save
  allowance.
- **Spread what runs at once.** Let R be what one account's five-hour window allows and L the load in five hours
  (neither is known to Daoris, and neither is claimed: they are for the argument). One by one meets a limit as soon as
  L is above R, while every other account sits idle. Spreading, which gives each account about L ÷ N, meets none while
  L is at most N × R, the person's own use of the accounts aside. When a limit does
  strike, one by one cuts off every session running on that account, up to K; spreading cuts off at most ⌈K ÷ N⌉. Under
  a load beyond all accounts together both meet limits and both carry the same work per window, and spreading still
  cuts off fewer. So spreading is the default.
- **The week weighs more than the five hours.** A five-hour window recovers in hours, a week in days (C1). A week
  spent early leaves its account gone for days; a week that turns over with allowance left has lost it for good. So
  the week's two risks, lapsing and running ahead, rank above any five-hour ordering. The five-hour window is honoured
  as a gate: an account near its limit is passed (§6), and spreading keeps each window below its limit.
- **Spread over time, too.** With one session at a time, spreading what runs at once does nothing. Starting each
  session on the account Daoris started on least recently spreads the load over the five-hour windows, which is the
  same protection over time, and puts every account's week to use.
- **A quest's account buys nothing for the goal.** A carry-on or a resume is a fresh conversation from the tree and
  Daoris's record, whatever account it runs on (D125 §3.5, TOOL4f); no context stays with an account. So a quest's next
  session goes where the walk sends it, and its record's first line says which account and why. That is the owner's
  *a quest can be running on different account*, taken past a limit to every start.

### 16.3 The walk, by default (`use: goal`)

Over the ready accounts of the start's scope (§2's rules stand: one scope, its list the whole set, chosen at a start,
nothing spent that would not have started, nothing counting accounts), each step stable, keeping the order it was
handed where it has no reason to change it:

1. **Keep.** A driven start drops the kept account. A conversation or Ask Daoris takes it last, or first where it is
   also the scope's default (§4.6).
2. **Near last.** An account whose agent said it is near any window's limit, or drawing on usage credits, goes to the
   end (§6). On by default.
3. **Fewest running.** Fewest of Daoris's sessions running on it now, counting this look's starts (§4.2, §7).
4. **Its week lapsing first.** An account whose weekly reset is known and falls within the next day, sooner first.
   Known from the agent's field, or from a weekly limit it already met. A reset a limit told is carried forward a week
   at a time where the agent's table says the maker fixes it per account, as Claude's does (C1); where that was not
   read (Codex), it is dropped at its reset.
5. **Furthest behind its week's pace.** Where the agent said how much of its week is used: the account whose used share
   is furthest below the share of its week already elapsed first, and an account ahead of pace after every account
   that is not. An account that said nothing ranks between the two, neither behind nor ahead.
6. **Least recently started.** The account Daoris last started a session on longest ago first; one never started on
   first of all.
7. **List order**, begun at the scope's default: the person's own preference has the last word.

The first account still ready after the probe runs (D125 §3.3); none ready is D125 §4's wait, unchanged. While every
account is ready, no account runs more than ⌈K ÷ N⌉ at once (step 3).

**Why a day in step 4.** A week is spent over many five-hour windows, and a day holds about five of them. Before an
account's last day, what it has left can still be spent at any position in the order; within it, what is left is at
risk of lapsing, and an order that waited longer could not spend it. Ranking every week by its reset all week long
would pile one account's whole week of work onto it while the others' weeks went by unused, and would leave each of
them a leftover too large for the windows left when its turn came. The day is a constant, not a setting, and TOOL4h's
report says whether it is right: allowance left at weekly resets, where the agents' word shows it (§16.7).

**Why pace below the lapsing week.** Pace alone can pass a small leftover about to lapse: an account ahead of pace
with a tenth of its week left and a day to go ranks after one behind pace with days to go, and its tenth lapses. Step
4 spends it first. Before the last day, pace keeps every account's week moving, so none is spent on day one and none
arrives at its reset under-used.

### 16.4 With nothing said (today)

Steps 2 and 5 need the agent's word, which no door of Daoris's yet carries (§5.3). Until TOOL4b records it and TOOL6c
reads it:

- the walk is keep, fewest running, a week lapsing (known only from limits already met), least recently started, then
  list order;
- Daoris learns only from limits as they strike: each cools its account (D125 §2), and a weekly one tells that
  account's weekly reset for the weeks after (step 4);
- no number stands in for a reading: an account that said nothing is ranked by the steps that do not need its word,
  never as spent and never as fresh (D57);
- it says so: under *Use accounts* on the screen and in `daoris agent list`, *No account has said what it has left
  yet. Daoris spreads starts across them by its own sessions, and learns each account's weekly reset from the limits
  it meets.* Each start's first line names the step that chose its account: *opened on `account-2`: it runs the
  fewest of Daoris's sessions*, *…: its week resets first, at Oct 3, 16:02 (<zone>)*, *…: Daoris started on it least
  recently*.

### 16.5 Each proposal weighed

| Proposal | Kept, changed or rejected | Why |
|---|---|---|
| 1. Use it before it resets: among ready accounts with allowance left, the one whose five-hour window resets soonest | **Changed**: kept for the week (step 4), rejected for the five-hour window as an order | The week's leftover is lost for good at its reset, and its reset is a fact the agent states and, for Claude, fixes per account. An open five-hour window's reset is unknown without the agent's word (its start was read on no maker's page, §0.3). Ranked above the week, it would pull work onto an account ahead of its weekly pace: spending a week, which recovers in days, to save room that recovers in hours. The five-hour window is a gate instead (step 2), and spreading keeps it below its limit (step 3) |
| 2. Pace the weekly allowance: furthest behind pace first | **Kept**, as step 5, below the lapsing week | It keeps every account's week moving. It needs the used share, which only the agent states, so it waits for TOOL6c. Below step 4 for the reason in §16.3 |
| 3. Spread what runs at once | **Kept, and made the default** (step 3) | No limit while the load fits all accounts together, and a limit cuts off ⌈K ÷ N⌉ rather than K (§16.2). This reverses D130's *spreading as the default* rejection, whose cost, aim A, the goal outranks |
| 4. Switch before the limit | **Kept, and on by default** (step 2) | It is inert without the agent's word, so on costs nothing today; with it, a session does not start where it would be cut off. A quest still moves when its account is spent: TOOL4f's carry-on, unchanged |
| 5. A kept account | **Kept as an option**, off by default | It takes one account's allowance out of driven work, which the goal alone would not do; a person may still want a reserve. What it leaves at its resets is in the report |
| 6. No readings yet | **As §16.4** | Spread by Daoris's own sessions and over time, learn from limits, the list as the last word, and nothing made up |
| 7. What the person overrides | **One default with overrides** (§16.6), replacing *start on* and *sessions at once* | The goal decides which account first and how many at once together. Two settings would let a person pick a combination that works against the goal without saying what it costs. The overrides the goal cannot decide stay: the list, `order`, keep and switch |
| 8. How it is judged | **§16.7** | The report and the owner's run, never a number Daoris made up |
| A quest's carry-on on its last account (D130 point 6, and the owner's note on *in parallel*) | **Rejected as a default** | It buys readability only, which the record's first line already gives (§16.2) |
| Most left first, soonest reset first (§4.4, §4.5) | **Folded** | Most left without the week's time pulls a just-reset account ahead of one about to lapse; pace (step 5) is most left measured against time. Soonest reset is step 4, bounded to the last day |
| Round robin by start (§4.7) | **Reversed**, as step 6 | It spreads load over the five-hour windows when nothing is said; its cost, aims A and D, the goal outranks |

### 16.6 What the person sets

Per agent, for the machine or for one workspace, for any number of accounts:

| Setting | Values | Absent |
|---|---|---|
| The list (§3) | the accounts that may run the scope's work, in order | none: the one account, D48 §2a |
| **Use accounts** (`use`) | *Make the most of them* (`goal`), *One by one, in order* (`order`) | `goal` |
| Keep for conversations (`keep`) | one account of the list | none |
| Switch before the limit (`early`, `near`) | on or off; the percentage that counts as near where the agent gives only a number | on; 90 |

- **`goal`** is §16.3.
- **`order`** is D125's walk: the list, begun at the default, the first ready account carrying every start, plus
  switching before the limit unless it is off. It serves aims A and D for a person who wants their accounts used in
  their order, and the screen says what it costs: *one limit stops every session on that account*.
- 🔴 **A machine with an order written under D125 or D130 changes when TOOL6b lands**: its starts follow the goal
  unless the scope says `order`. The owner asked for the goal; `daoris agent profile use <agent> order` keeps the old
  walk. A machine with no list is untouched.

**How TOOL6a's vocabulary changes.** A branch is building TOOL6a from §2 and §14. `rotationUse` and
`workspaceRotationUse` keep their names and shape; within each agent's entry:

| Key | §2 and §14 said | §16 says |
|---|---|---|
| `prefer` | `list`, `left` or `soonest`; else `list` | **gone**. Nothing released wrote it: a reader skips it and a writer drops it |
| `parallel` | `true` or absent (off) | **gone**, as `prefer` |
| `use` | — | **new**: `"goal"` or `"order"`; absent or anything else is `goal` |
| `keep` | an account of the scope's list; else none | unchanged |
| `early` | `true` on; absent off | **absent or `true` on; `false` off** |
| `near` | a whole percent from 50 to 99; else 90 | unchanged |

- **The terminal door** is `daoris agent profile use <agent> [goal|order] [--keep <account>|--no-keep] [--early
  on|off] [--near <percent>] [--workspace W]`. With the agent alone it prints the scope's settings, what each account
  last said and the step the next start would follow. `--prefer` and `--parallel` are gone.
- **Ask Daoris's `use` door** takes `use`, `keep`, `early` and `near`.
- **Both twins write the same bytes** for the same wiring, as §14 says, and a scope's settings are read only with its
  list.

```json
{
  "rotation": { "claude-code": ["account-1", "account-2", "account-N"] },
  "rotationUse": { "claude-code": { "keep": "account-N" } },
  "workspaceRotation": { "work": { "claude-code": ["account-2", "account-1", "account-N"] } },
  "workspaceRotationUse": { "work": { "claude-code": { "use": "order", "early": false } } }
}
```

Here the machine makes the most of every account but the last, which it keeps for conversations, and the work
workspace takes its accounts one by one in its own order, with no early switch.

**Names, proposed for TOOL4g** (D116; the glossary and the names check decide):

| Concept | Kind | English | Chinese |
|---|---|---|---|
| how a scope uses its accounts | field | *Use accounts* | 账户用法 |
| `goal` | choice | *Make the most of them* | 充分利用 |
| `order` | choice | *One by one, in order* | 按顺序逐个用 |

The other names of §9 stand: *Use*, *Keep for conversations*, *Switch before the limit*, *near its limit*, *This
machine's accounts*, *Its own accounts*, and the wait's and the conversation's doors.

### 16.7 How the goal is judged

TOOL4h's report, per account and per week, labelled *Daoris's sessions only* wherever it is:

- **work done**: the sessions that landed;
- **stalls**: the hours starts waited with every account they may use cooling (`starts.waiting`), and the sessions a
  limit cut off mid-turn (records saying `limit`, TOOL4c);
- **allowance left at each weekly reset**, from the last reading before it, where the agent's word shows it, and *not
  measured* where it does not: absent is never zero;
- **the limits each account met**, per window;
- **the starts each step moved**, from `account.rotated`'s `why`;
- **the cap's share**: allowance left at a reset while every slot was full says the cap, not the accounts, was the
  limit, and raising it is the person's (`daoris driver cap`).

Weeks under `goal` and weeks under `order` sit side by side where a machine has both. TOOL4i is the owner's run under
`goal` over all their accounts, for at least one of each account's weeks, recorded against these measures. No target
is set: the measures are compared and read, never scored against a number Daoris made up.

### 16.8 What only a real run proves, added to §11

1. **Whether spreading meets fewer limits than one by one** on real work, from the report's limits and cut-offs.
2. **Whether a day is the right horizon for a lapsing week** (step 4), from allowance left at weekly resets once the
   agents' word is read.
3. **Whether least recently started keeps the five-hour windows below their limits** with one session at a time.
4. **Whether a model family's weekly limit** (C3) is told by its own sentence, and whether it should cool only that
   family's starts: D125 cools the whole account, and no such sentence is recorded.

### 16.9 Considered and rejected, added to §12

- **Holding work to pace an account's week**: a held start leaves allowance unused that a ready account and a free
  slot could have spent, which is the goal's third measure.
- **Ranking every week by its reset all week**: it piles one account's work onto it while the others' weeks go by
  (§16.3).
- **The five-hour window's reset as an order above the week** (§16.5, proposal 1).
- **A quest kept on its account**: no context stays with an account; readability is in the record.
- **A cap per account, or raising the cap to use more windows**: the cap is the person's dial for the machine and for
  what they can review; the report says when it, not the accounts, was the limit.
- **Keeping `prefer` and `parallel` beside `use`**: two ways to say one choice, and combinations that work against the
  goal without saying so.
- **A cached prompt as a reason to keep a quest's account**: no page read says a cached prompt counts less against a
  subscription's windows.
