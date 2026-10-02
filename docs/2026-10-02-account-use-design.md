# Account use: how switching works, and how several accounts are used well (TOOL6)

> The owner, 2026-10-02, asked which account should run a workspace whose account was spend-limited: *"there are
> multiple accounts we should be able to set option how switch works and how to optimize the account use since there
> are 3 accounts"*. D125 (`docs/2026-10-01-account-rotation-design.md`) has one policy: walk the person's order when
> the default is not ready. This is the contract for what the person sets beyond that, and for what Daoris does to use
> the accounts well. Its decision is **D130**, which amends D125. Status: **designed; nothing built.** Read with
> **D125** and its TOOL4a–TOOL4f notes, **D57**, **D58**, **D48 §2a**, **D49 §4**, **D50**, **D54**, **D110** and
> **D116**.

Accounts are named as Daoris names their directories (`account-1`, `account-2`, `account-3`: D66 §3). Nobody's account,
address or time zone is named here, and a sentence an agent printed is quoted only in a shape D125 §0.2 already
recorded.

- §0 is what is true today, read from the code at `7cca0e1`, and what the makers document.
- §1 is what *well* means: five aims that pull apart. §2 is the decision in one table.
- §3–§6 answer the brief: which accounts may run a workspace, how a list is used, switching before a limit, and
  sessions at once. §7 is what each choice costs, §8 the doors.
- §9 is the build, as rows. §10–§14 are what only a real run proves, what was rejected, what this amends, the twins,
  and what this document's gate does not cover.

## 0. What is true today

### 0.1 The code, after TOOL4f

| What | Today |
|---|---|
| Which account a start runs as | `HarnessSettings.ResolveFrom`: the person's pick, the workspace's default, the machine's, then none, which is the tool's own sign-in (D49 §4). Driven starts and intakes pass no pick; a conversation passes the person's; Ask Daoris passes none and no workspace, so it reads the machine's |
| The order | `rotation` and `workspaceRotation` in `harnesses.json`, one list per agent. `ResolveRotationFrom` takes the workspace's list, else the machine's, else none, **whichever rung named the account** (D125 §3.1) |
| The walk | `AccountRotation.Candidates`: the resolved account, and only where a default named it and the applicable list holds it, the rest of the list after it, wrapping. `SelectAsync` runs the first ready one: not cooling, not refused (AGT3b), not signed out. A pick, the tool's own sign-in and an account outside the list are the one account. No list is D125's *none*, byte for byte |
| 🔴 A workspace that names its own default and no list | Takes the machine's list. A work workspace whose default is the work account, on a machine whose list also holds two personal accounts, rotates onto them when the work account cools. Nobody said it may |
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
  (*… · your session limit resets 7:50am (<zone>)*), both refused. With four sessions on one account, one limit is
  four cut-offs, and each carry-on starts a fresh conversation from Daoris's record (TOOL4f).
- **One account is spent by Daoris and by the person alike.** Observations 4 and 5 named one reset (*your weekly limit
  resets Oct 3, 4pm (<zone>)*): Daoris's sessions and the person's own agents, outside Daoris, on one account. Daoris
  sees only its own share of what an account spent.
- **Two kinds of window, and a spend limit on a team seat.** *Your session limit* resets within hours; *your weekly
  limit* in days. *You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit*
  names an administrator: at least one of the accounts is a seat on a team plan.
- **Three accounts, and the work moved by hand.** After observation 5 the owner added a third account and resumed the
  stopped agents on it. Which account should carry which work was then a question put to the owner, which is this
  design's brief.

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
  allowance left at a reset is kept. So spreading spends allowance that would otherwise lapse (§4.2), and a weekly
  reset, once stated, is a fact about that account until it changes.
- **One allowance covers every surface** (C4), so Daoris sees only its own share of an account, as observations 4 and
  5 showed. That is the maker's word now, not only this machine's.
- **The team seat's sentence reads as C5 says**: the seat's windows ran out, usage credits began, the member's monthly
  spend limit paused them, and the seat's own window still resets at the time the agent named. D125's cool-off to that
  reset stands.
- **The agent has its own word for near**: `allowed_warning`, sent on a change of status, with `utilization` only past
  a threshold (C9, C11). Codex's protocol gives a percentage per window (X2). Neither is known to reach a door Daoris
  reads: Claude Code's arrives as `rate_limit_event` on the native door, unrecorded (§0.1), and whether its protocol
  door's adapter forwards it, or `codex-acp` forwards Codex's, is TOOL4b's to find (§9).
- **A Codex limit does not cut a turn off** (X1), so on Codex a limit refuses the next turn rather than taking the
  sessions running on the account mid-turn.
- **The rotating tools learn quota by asking and switch by replacing a credential**, where their pages say how:
  claude-swap polls, cc-account-switcher keeps a usage cache, codex-rotator probes every 60 seconds and swaps the active
  auth file, and CLIProxyAPI holds the credentials itself. Daoris does neither: one directory per account, chosen at
  spawn, nothing read or moved inside it (D49 §4, D66 §3), and no request made only to ask (D125 §1.4). Their
  thresholds are 80% and 90%; their shapes are fill first, round robin and least busy, which §4 weighs.
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

No single policy is best, because a person with three accounts wants different things on different days, and Daoris
can see only part of each account (§0.2). These are the aims a setting serves:

| Aim | What serves it | What it costs |
|---|---|---|
| **A. Keep my own accounts fresh.** The person also works on these accounts, at their terminal and in the maker's own apps (observations 4 and 5) | Work stays on the first account until it is spent; the others are touched only then | When the first account meets its limit, every session on it is cut off together |
| **B. Lose as little work as possible to a limit.** A cut-off loses the agent's own conversation (TOOL4f hands on the last plan and last words, not the context) | Sessions running at once on different accounts, so a limit cuts off only its own; and a start that passes an account about to meet its limit | Windows opened on accounts the work did not yet need |
| **C. Get the most done in a week.** Windows reset on the maker's clock, and nothing read says what a window leaves is kept (§0.3) | Starting on whichever account has allowance left, and spending allowance that is about to lapse first | Every account is in use, and a quest's sessions run on several |
| **D. Keep work readable.** A quest's sessions read on one account; a workspace's spend sits on its own account | One account per workspace, and a quest's carry-ons on the account it started on | The workspace waits when its account cools |
| **E. Keep work where it belongs.** A work repository may run only on the work account, or on it first | The person's list for that workspace, and nothing else | Work waits rather than moving to an account the person did not list |

**E is never Daoris's call** (the brief's own words): it is stated by the person, shown, and never inferred from an
address, a name, a plan or where work last ran. A, B, C and D are traded against each other by a setting the person
chooses, and the default is D125's: A and D first.

## 2. The decision in one table

Per agent, in each **scope** (the machine, or one workspace), the person states:

| Setting | Values | Absent | What it answers |
|---|---|---|---|
| **The list** (D125's order) | the accounts that scope's starts may run on, in order | none: D125's behaviour, byte for byte | E: which accounts may run this work, and in what preference |
| **Use** | *one after another* (`fill`), or *side by side* (`spread`) | `fill`: D125's walk | A and D against B and C (§4) |
| **Keep for conversations** | one account of the list | none | that driven work never spends the last account the person talks on (§4.3) |
| **Switch before the limit** | on or off | off | B: a start passes an account its agent said is near its limit (§5). Offered only where a door carries that word |

The rules that bind them:

1. **A start reads one scope**: its workspace's, when that workspace names a default or a list of its own for the
   agent; else the machine's. A scope with no list is its one account, D125's *none*, byte for byte. A workspace's list
   comes with its own *use*, *keep* and *switch*, never mixed with the machine's.
2. **The list is the whole set** (§3). An account a scope does not list never carries one of its starts. The person's
   pick is the one exception: it is their own act, each time.
3. **The default says where a start begins within the list**; a scope that lists accounts and names no default in it
   begins at its first.
4. **Every choice is made at a start, never inside a running session or conversation** (D125 §3.2).
5. **Nothing here spends more than D125 already may**: every account any setting reaches is one the person listed,
   and no setting starts work that would not have started anyway.

In `harnesses.json`, beside D125's two lists, which keep their shape:

```json
{
  "rotation": { "claude-code": ["account-1", "account-2", "account-3"] },
  "rotationUse": { "claude-code": { "use": "spread", "keep": "account-3" } },
  "workspaceRotation": { "work": { "claude-code": ["account-2", "account-1"] } },
  "workspaceRotationUse": { "work": { "claude-code": { "use": "fill", "early": true, "near": 85 } } }
}
```

## 3. Which accounts may run a workspace

### 3.1 How it is stated

**By the workspace's own default and list.** *Only the work account*: the workspace's default is `account-2` and it
lists nothing more (or lists `account-2` alone). *The work account first, personal accounts after*: it lists
`account-2`, `account-1`, `account-3`. *Whatever this machine uses*: it names neither a default nor a list, and the
machine's apply. Three statements, one list, no second register (D125 §9: *a do-not-rotate list beside the order*).

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
  *Use* on each row, its *use*, *keep* and *switch*. A workspace on the machine's accounts shows which those are.
- **Each account's row** names the workspaces that may run on it, as `harness.profile.workspaceUses` names the
  workspace that defaults to it today.
- **The session's head** names the account it ran on (D125 §3.6), and a start that began somewhere other than its
  default says why in its record's first line (TOOL4f), now with the scope: *opened on `account-3`: `work` lists
  `account-2` and `account-3`, and `account-2` is cooling until 07:52 (<zone>)*.
- **The conversation picker** lists the scope's accounts first and every other account under *not in this
  workspace's list*, so a pick outside it is never made by accident.
- **`daoris agent list`** prints each workspace that has a list, beneath the machine's, with its settings.

### 3.3 When the question comes up: the wait that asks

The owner's question arose because a workspace's only account was spent and others were not. Under §3.1 that
workspace waits (D125 §4), and the wait names what the person could do:

> *every `claude-code` account `work` may use is cooling; the first ready, `account-2`, at Oct 3, 16:02 (<zone>), as
> the agent said. Not cooling, and not in `work`'s list: `account-1`, `account-3`. Daoris starts nothing on them until
> then.*

with the doors that answer it once: *Let `account-1` run `work`…* on the *What needs you* row (it adds the account to
the workspace's list, after a confirm naming what that lets Daoris spend), `daoris agent profile order claude-code
account-2 account-1 --workspace work`, and Ask Daoris's `order` card. The accounts are named from the cool-offs and
AGT3b's refusals alone, since a waiting look starts no process (D125 §4); whether one is signed in is the probe's,
asked when the person presses. The row is said once per wait, as D125 §4's attention is. **Daoris never takes the
answer itself.**

## 4. How a list is used

### 4.1 One after another (`fill`, the default)

D125's walk, unchanged: the start's account, then the rest of the list after it, wrapping; the first ready runs. Work
stays on one account until that account is not ready, and goes back to it once it is (D125 §3.3). It serves A and D.
Its cost is B: with four sessions on one account, one limit cuts off four (§0.2).

### 4.2 Side by side (`spread`)

The same sequence, ordered by **how many of Daoris's sessions are running on each account now**, fewest first, the
sequence's own order breaking ties: a gateway's *least busy* (T5), with the person's list as its priority. So one
session at a time runs exactly as `fill` does, and only sessions running at once go to different accounts. Two more
rules:

- **A quest decides once.** A carry-on (D80) or a resume (D79) runs on the account its last session ran on whenever that
  account is still in the list and ready, so a quest's sessions read on one account (D). A limit makes that account not ready, so the
  carry-on after a limit still moves.
- **Counted, not guessed.** The count is the live sessions whose records name the account (loopback, D47 §4),
  conversations and Ask Daoris included, plus the starts this look has already chosen. The person's own use outside
  Daoris is not counted, because Daoris cannot see it (§0.2), and the screen says *Daoris's sessions*.

It serves B and C. Its cost is A: a second session opens a second account's window while the first account could have
carried it. **This answers D125 §9's rejection of spreading** (*it spends windows the work did not need, and makes a
quest's sessions harder to read*) on both counts, without reversing it: spreading is never the default; it spreads
only what runs at once, which is what a limit cuts off together; and a quest stays on its account.

### 4.3 Keep for conversations

One account of the list on which **driven work** (a driven start, a carry-on, a resume, an intake, a set-up) never
starts. A conversation and Ask Daoris walk the list as before, with the kept account in its place, so they reach it
only when the accounts before it are not ready: it is the account left when driven work has spent the others. With
`spread`, a conversation's count includes it like any other.

- **Refused when it would stop driven work**: keeping the only account of a list. The door says so and names the fix.
- **A driven start whose only ready account is kept waits**, and its sentence says so: *`account-3` is kept for
  conversations*.
- **Kept and the default together** put conversations and Ask Daoris on that account first, and driven work on the
  rest of the list: the way to say *conversations run here*, with no setting of its own.
- **Not the tool's own sign-in.** Keeping an account away from Daoris altogether is already one act: sign it in at the
  terminal only, as the tool's own sign-in, and give Daoris accounts of its own (D125 §3.7). Once the machine's default
  or list names an account, Daoris never spends that sign-in.

### 4.4 What is not offered, and why

- **The account whose window resets soonest** (C at its sharpest: spend the allowance about to lapse). It needs each
  ready account's current window and when it resets, and Daoris holds a reset only for an account that has already
  met its limit (TOOL4d). The five-hour window's start was not read on any maker's page (§0.3), and the person's own use
  opens it unseen. Ordering ready accounts by a reset Daoris has not been told would order them by a guess; absent is
  never zero (D57). **Held**, with its trigger: a door recorded carrying each window's reset while the account is still
  ready, as the agent's `resets_at` may (C9; §5.2). Then it is a third value of *use*, `soonest`, by a decision of its
  own, which may also keep a weekly reset once stated, since the maker fixes it per account (C1).
- **Round robin by start** (CLIProxyAPI's *round robin*, T4). It moves a lone session across accounts for no limit and
  no load, which costs A and D and buys nothing B needs.
- **A share per account** (*account-1 carries 50%*). Daoris measures neither an account's allowance nor all of its use
  (§0.2), so a share would be of Daoris's sessions only, while the limit is on everything.

## 5. Switching before a limit

### 5.1 What is measured, and what each can carry

| Source | What it says | Can it move a start before a limit? |
|---|---|---|
| `usage.json` (TOOL3) | each session's context at its high-water | **No.** Context is not spend (§0.1), and it is Daoris's share alone |
| The native `result`'s token counts | what one turn consumed | **No.** What was used is not what is left (D125 §1.4), the maker states no allowance in tokens (§0.3), and it is Daoris's share alone |
| The limits met (`account.limited`, `cooling.json`) | which account met which window's limit, when, at which turn | **No, and it is reported** (§5.4) |
| The window the sentence names | *session* or *weekly* | **Already used**: the reset it names is the cool-off (D125 §2) |
| The agent's own field about its windows, on the door its session runs on: Claude Code's `rate_limit_event` (C9), Codex's `rate_limits` (X2) | its status (`allowed`, `allowed_warning`, `rejected`) or how much of each window is used, and when it resets, said by the agent as it works, at no extra call | **Yes**, once a door is recorded carrying it, and only on that door |
| The agent's usage command, `/usage` (C7) | the same, as text, to a person | **No.** A door with text alone is never read (D125 §1.2), and a process started only to ask spends one |
| Claude Code's status line (C8) | `rate_limits`' percentages and resets, handed to a command the person configures | **No.** It runs in the person's interactive terminal, which a driven session is not, and only for some plans |
| Codex's app server, `account/rateLimits/read` (X3) | the same, on request | **No.** Daoris speaks the protocol door to Codex (`codex-acp`), not its app server, and a request made only to ask is D125 §1.4's refusal |

### 5.2 The agent's own word

**Switch before the limit reads one thing: what the agent says about its own windows, on the door its session
already runs on.** It is D125 §1.2's *a field beats a sentence*, read before the limit instead of at it.

- **What counts as near**: the agent's own word for it, where its field has one (Claude Code's `allowed_warning`, C9);
  where the field carries only how much of a window is used (Codex's `used_percent`, X2), at or over **90%**, settable
  per scope, the higher of the two thresholds the rotating tools use (T1, T2). The agent's word wins over the number
  wherever both exist. So does the agent's word that the account has begun drawing on usage credits, which are billed
  (C5, C6): its included allowance is spent, and another account's may not be.
- **Kept per account**, in `windows.json` beside `cooling.json` under the home: per agent and account, per window the
  agent names, how near, its reset and when it was seen, and the session it was seen on. Never the agent's words, a key
  or who signed in. Written atomically by the driver as a session's door carries the field; an entry whose reset has
  passed is gone. Missing or unreadable is nothing near. No HTTP route.
- **At a start**, with *switch before the limit* on, an account whose entry says near goes to the end of the
  candidates: **passed while another account is ready, run when none is.** Near is not spent. It never makes a start
  wait, never cools an account, and never stops a running session.
- **Said**: the start's first line (*opened on `account-2`: `account-1` said it is near its weekly limit*), the
  account's row (*near its limit*, with the window and its reset), `daoris agent list`, and `account.rotated`'s
  new `why` (§12).
- **Until a door is recorded carrying the field, nothing is read** and the setting is not offered for that agent: the
  screen's switch is shown off and disabled, saying *Claude Code's sessions here do not say how near their limits
  are*. That is today for every agent (§0.1): the native door's frame is TOOL4b's to record, and the protocol door's
  adapter may not forward it at all (§0.3).

### 5.3 What switching early costs

It spends another account while the first still had some allowance. That is the price of not cutting a long session
off mid-turn: observation 1's turn was twenty minutes and 370k tokens of context when it was refused.

### 5.4 Learning an account's capacity: reported, never acted on

The brief asks whether Daoris could learn where an account's limit lies from where its limits struck. **It reports
it, and acts on none of it.** A fact gates, a judgement reports (D54), and a capacity learned here is a judgement on
four counts: Daoris sees only its share of what the account spent, since one allowance covers every surface (C4);
what it measures is context, not spend (§0.1); the makers publish windows, not amounts, and the windows differ by plan
and model family (§0.3); and a limit sentence says when, not how much. Acting on it would cool an account that is not
spent, or miss one that is: D125 §1.4's reason for never guessing from token counts.

So TOOL4h's usage report gains, per account and window kind: the limits met, and before each, how many of Daoris's
sessions had started on that account since its previous reset and for how long they ran. Labelled *Daoris's sessions
only*, and never a capacity, a share or a price.

## 6. Sessions at once

**The cap stays one number for the machine, and sessions split across accounts only by `spread`.** Under `fill`, all
four of the install's sessions run on one account until it is not ready, and one limit cuts off all four; under
`spread`, four sessions on three accounts run two, one and one, and a limit cuts off at most two.

- **No cap per account.** A second number would have to agree with the cap and the list's length, and `spread`
  already splits what runs at once by the one count that matters. None of the pages read documents a limit on sessions
  at once per account (§0.3), so a cap per account would answer no stated constraint.
- **Starts in one look are counted as they are chosen**, so a look that starts three quests under `spread` does not
  put all three on the account that was emptiest when it began.
- **On Codex the cost differs**: a limit lets the running turn finish (X1), so what one limit takes is the next turn
  of each session on the account, not the turn in flight. `spread` still splits what the next limit refuses.

## 7. What each choice costs

| Choice | What it costs | What keeps the cost visible |
|---|---|---|
| A conversation on a spent account | It cannot move mid-turn, nor between turns without losing the agent's conversation, which lives in the first account's home and is never copied (D125 §3.5) | The conversation's refused turn says the account is cooling and offers *Continue on `account-2`*: a new conversation, opened on the next ready account of its scope and handed the last plan and last words as a carry-on is (TOOL6d). The person's press, never automatic |
| A carry-on on another account | A fresh conversation from the tree and Daoris's record; the agent's own context is lost (TOOL4f) | Its first line names the cut-off session and why it moved; under `spread` a quest stays on its account while that account is ready (§4.2) |
| `spread` | Windows opened on accounts the work did not yet need; the person's other accounts touched sooner | The account rows show Daoris's running sessions per account |
| *Keep for conversations* | One account's allowance is out of driven work's reach, so driven work waits sooner | The wait names the kept account |
| *Switch before the limit* | Another account's allowance spent while the first had some left | The start's first line, and `account.rotated`'s `why` |
| A workspace's list | Its work waits rather than moving to an account it does not list | The wait names the accounts outside the list that are not cooling, with the door to add one (§3.3) |
| The tool's own sign-in | Shared with the person, moved when they sign in elsewhere, unreadable in a record (D125 §3.7) | Never in a list, never kept, never switched to; §3.7's line while starts run on it |

## 8. The doors (D50, D110), in both languages (D116)

| What | The screen (TOOL4g's) | The terminal | Ask Daoris |
|---|---|---|---|
| A scope's list | the account rows in order, up, down and *Use* (D125's *Rotate*, renamed: the switch now means *may run this scope's work*) | `daoris agent profile order <agent> <account>… [--workspace W]`, `--clear` (built, TOOL4e; §3.1's refusals added) | the agent kind's `order` door (TOOL4g) |
| A workspace on the machine's accounts or its own | *This machine's accounts* / *Its own accounts*, under the workspace | a default or a list with `--workspace W` is its own; clearing both returns it to the machine's | the `order` door, and the default's |
| How a list is used | *One after another* / *Side by side* | `daoris agent profile use <agent> fill\|spread [--workspace W]` | a `use` door |
| Keep for conversations | *Keep for conversations* on an account's row, one per scope | `daoris agent profile keep <agent> <account>\|--clear [--workspace W]` | a `keep` door |
| Switch before the limit | a switch, disabled with its reason until a door carries the field | `daoris agent profile early <agent> on\|off [--near <percent>] [--workspace W]` | an `early` door |
| What each account is doing | its row: running now, cooling until, near its limit, kept; and, under the agent's accounts, *each account's own plan and terms apply* (§0.3) | `daoris agent list` | the room's machine facts |
| The wait that asks | *Let `account-1` run `work`…* on *What needs you* | the `order` command the wait's sentence names | the `order` card |
| A conversation cut off by a limit | *Continue on `account-2`* on the refused turn | `daoris-driver` has no conversation door; none is added | a reason: the person's press, as answering is |
| The cap | none yet, as the room's doors table says (unchanged) | `daoris driver cap <n>` (unchanged) | the setting kind's `cap` (D110) |

- **Each door that widens what Daoris may spend is a card the person applies** (D89): adding to a list, `spread`,
  *switch before the limit*. Each screen control is held to its Ask Daoris answer by `HelpCoverageTests` (D110), and
  each terminal door is a row of the room's doors table (`HelpRoomDoors`). `daoris driver` gains no verb: the cap is
  the one machine-wide dial, and every new choice is per agent.
- **The names, proposed** (TOOL4g designs them against the kinds' budgets and enters them in the glossary, which the
  names check holds):

| Concept | Kind | English | Chinese |
|---|---|---|---|
| a list's switch on an account's row | choice | *Use* | 使用 |
| how a list is used | field | *Use them* | 使用方式 |
| `fill` | choice | *One after another* | 依次用满 |
| `spread` | choice | *Side by side* | 分开并行 |
| the kept account | button, status | *Keep for conversations*, *kept for conversations* | 留作对话 |
| switch before the limit | field | *Switch before the limit* | 提前切换 |
| near | status | *near its limit* | 接近上限 |
| a workspace's scope | choice | *This machine's accounts*, *Its own accounts* | 本机账户、自有账户 |
| the wait's door | button | *Let {{account}} run {{workspace}}* | 允许 {{account}} 运行 {{workspace}} |
| a conversation's door | button | *Continue on {{account}}* | 换到 {{account}} 继续 |

The agent's own sentences, wherever shown, are content and are not translated (`translation-parity`). The account is
账户, never 账号 (glossary).

## 9. The build

Rows ready for `TASKS.md`. D130 decides all of it; a row takes a decision number only if building it finds something
D130 did not decide. A row with a `Process`-half case, a rehearsal or a look in its proof is proven by the parent at
merge. Four rows are new; four of D125's rows are amended rather than duplicated.

| Row | What and why | Contract | Proof |
|---|---|---|---|
| **TOOL6a** | **The settings and their terminal doors** (cli; driver). *Use*, *keep* and *switch* per scope in `harnesses.json`, both twins, `daoris agent profile use\|keep\|early`, and §3.1's refusals on `profile order` and `profile default`, so the person's choices have a file and a terminal before anything reads them | §2, §3.1, §4.3, §8, §13 | Twin tables both sides; each writer keeps the other's sections; the refusals; `agent list`'s lines; the room's doors table; `node --test`; the golden usage |
| **TOOL6b** | **The walk reads one scope, its use and its keep** (driver; after TOOL6a). The list as the whole set, `spread` by live sessions per account, a quest deciding once, driven work off the kept account, and the wait naming accounts outside the list, so a workspace runs only where the person said | §2, §3, §4, §6; §12's log fields | `AccountRotation` tables per rule, failing first. A `Process`-half tick: `spread`, cap 2, two accounts, two quests one on each; a limit on one cuts off only it; the next look counts the live |
| **TOOL6c** | **Switching before a limit** (driver; after TOOL4b records a frame, and TOOL6b). `windows.json`, the door's field read into it, the soft pass and `account.near`, so a long session does not start on an account its agent said is near its limit | §5.2 | Fast-half tables: near by word and by percent, the pass, the run when none else is ready, an entry gone at its reset; a `Process`-half tick replaying the recorded frame |
| **TOOL6d** | **A conversation continues on another account** (driver; modules; web-shell; after TOOL6b). A refused turn offers *Continue on `account-2`*, a new conversation in its scope handed the last plan and last words, so a person's conversation on a spent account is not a dead end | §7, §8 | Driver tests for the opening and what it is handed; modules route tests (MOD5); vitest over a mocked bridge; the look in both languages |
| **TOOL4b**, amended | Also: `rate_limit_event`'s fields as Claude Code sends them, on an ordinary turn and at a warning; whether its protocol door's adapter forwards them; and whether `codex-acp` forwards Codex's `rate_limits` and its limit sentence. Read keylessly from shipped code, labelled unmeasured, then the first real frame of each recorded | §0.3, §5.1, §5.2 | The evidence document. TOOL6c, and a Codex entry in D125's table, build only on what it records |
| **TOOL4g**, amended (after TOOL6b) | The screen also carries §3.2 and §8: a workspace's *This machine's accounts* / *Its own accounts* and its list; *Use* for *Rotate*; *One after another* / *Side by side*; *Keep for conversations*; *Switch before the limit*, disabled with its reason; each row's running count and state; the terms line; the picker's split; the wait's *Let … run …*. Ask Daoris gains `use`, `keep` and `early` | D125 §2.4, §3.7, §4, §6; §3.2, §8 here | As D125's TOOL4g, plus the new doors in `HelpCoverageTests` and the names check; the look, both themes and both languages |
| **TOOL4h**, amended | The report also counts, per account and window kind, the limits met and Daoris's sessions and their running time before each, labelled *Daoris's sessions only*; and the starts passed by `spread`, *keep* and *switch*. The rehearsal adds `spread` over two stub accounts | §5.4 here; §8, §2.2 of D125 | The report's parse table; the rehearsal phase |
| **TOOL4i**, amended | The owner's run also sets `spread` with the three accounts, and records which account each of the sessions running at once took, and what one limit cut off | §10 | The evidence, with the owner present |

**Order.** TOOL6a, then TOOL6b, in the driver lane after TOOL4f (landed). TOOL4g after TOOL6b, so the screen is built
once. TOOL6d after TOOL6b. TOOL4b any time; TOOL6c only on its frame. **TOOL6b alone answers the owner's question**:
each workspace's list says which accounts may run it, and the wait asks when it matters.

## 10. What a rehearsal can prove, and what only a real run can

**A rehearsal proves the mechanism**, with no model and no account: a workspace's list holding its work off an account
it does not list, the wait naming that account, `spread` putting sessions at once on different stub accounts, a limit
cutting off only its own, a carry-on staying on its account, a kept account untouched by driven work, every door.

**Only a real run proves the rest:**

1. **Whether one limit cuts off every session on its account at once**, as observation 2 suggests for two.
2. **What the agent's field about its windows carries on each door**, and whether the protocol door forwards it
   (TOOL4b). The maker's declaration (C9) is not a frame recorded on Daoris's door.
3. **Whether *near* comes early enough** to keep a long session from being cut off: when `allowed_warning` arrives
   relative to the limit, and whether 90% is the right number where the agent gives no word.
4. **Whether `spread` gets more done in a week than `fill`** on the owner's three accounts, from the report.
5. **Whether a team seat's window comes back at the reset its spend-limit sentence names** while its credits stay
   paused to the month's end, as C5 reads observations 1 and 2.

## 11. Considered and rejected

- **Inferring which accounts may run a workspace**: from the signed-in address, the account's name, the workspace's
  name, a team plan, or where its work last ran. The brief says the decision is the person's.
- **A workspace that names a default inheriting the machine's list**, today's reading (§0.1 🔴): it moved work to
  accounts nobody listed for it.
- **Mixing scopes**: a workspace's list with the machine's *use*. One scope per start, so the screen shows one block.
- **`spread` as the default**: it costs A for every person who never asked for B.
- **Round robin, a share per account, a cap per account** (§4.4, §6).
- **Soonest reset now** (§4.4): an order by guesses. Held with its trigger.
- **Learning capacity and acting on it** (§5.4); **any price**, OpenRouter's price weighting included (D57, D24).
- **Asking the agent's usage command before a start**: a text door, and a process spent to ask (§5.1). **Polling or
  probing for quota**, as the rotating tools do (§0.3): a request made only to ask (D125 §1.4).
- **Switching by replacing the active sign-in or its credential file**, as the rotating tools do: it moves the
  person's own terminal with it (D125 §3.7) and reaches inside an account's store (D49 §4, D66 §3).
- **A status line Daoris configures to catch `rate_limits`** (C8): it runs in the interactive terminal, which a driven
  session is not, and only for some plans.
- **A cooldown with backoff after a refusal**, as CLIProxyAPI's: the agent states the reset (D125 §2).
- **Near as a wait or a cool-off**: near is not spent; holding a start on it would leave allowance unused and the
  queue stopped.
- **Switching a running session or conversation** to another account: the account is set at spawn, and the agent's
  conversation is not Daoris's to copy (D125 §3.2, §3.5).
- **A kept account as a list beside the list**: one account named of the list, refused when not a member.
- **Keeping or listing the tool's own sign-in**: Daoris neither chooses it nor sees it change (D125 §3.7).
- **A new `daoris driver` verb**: every choice here is per agent and per scope; the cap stays the one machine dial.

## 12. What this amends

- **D125 §3.1**: a start reads one scope; a workspace's list is the whole set of accounts that may run it; a workspace
  that names a default and no list rotates nowhere; a scope with a list begins at its default or its first; a default
  outside its scope's list is refused at both doors.
- **D125 §3.3 and §3.4**: the walk orders by *use*, skips a kept account for driven work, and, with *switch* on, moves
  a near account to the end. The tool's own sign-in runs only where no default and no list name an account.
- **D125 §5.1 and §9**: spreading is a person's choice, never the default, and spreads only sessions at once.
- **D125 §6**: *Rotate* becomes *Use*; the `use`, `keep` and `early` doors join `order`, `ready` and `cooloff`.
- **D94 §4**: `account.rotated` gains `why` (`cooling`, `refused`, `signedOut`, `kept`, `spread`, `near`, `list`) and
  `scope`; `account.near`, once per account and window.
- **D110**: the `use`, `keep` and `early` doors.
- **Unchanged, and restated**: D57 (no price, no provider's console), D58 as D125 amended it (a limit is no strike),
  D48 §2a (no account named is the tool's own sign-in), D49 §4 and D66 §3 (nothing read inside an account's directory;
  who signed in kept nowhere), D125 §1–§2 (the signal and the cool-off), §3.2 (only at a start), §3.7, §4 and §5.

## 13. The twins this creates

Added to `.claude/knowledge/twins.md` by TOOL6a, with their test tables:

- **`rotationUse` and `workspaceRotationUse` in `harnesses.json`**: `toolchain.ts` and `Harnesses.cs`. `use` is `fill`
  or `spread`, anything else `fill`; `keep` is an account of that scope's list, anything else none; `early` true or
  absent, and `near` a whole percent from 50 to 99, anything else 90. A scope's settings are read only with its list.
  Each writer keeps the other's sections; both write the same bytes for the same wiring.
- **D125's order twin gains §3.1's rules**: a scope's default must be in its list when it has one; the refusals name
  both; a file that breaks the rule is read with the list winning.
- **Not a twin**: `windows.json`, written and read by the driver alone; `daoris agent list` reads it as it reads
  `cooling.json`, and is held by the driver's own table for the file's shape.

## 14. What this document's gate does not cover

This change is documents only, and nothing is built.

- **Read from the code at `7cca0e1`**: `AccountRotation.cs`, `Harnesses.cs` (`SelectAsync`, `Before`, `Unready`,
  `ResolveFrom`, `ResolveRotationFrom`, `OrderProblem`), `AccountCooling.cs` (the log lines), `Usage.cs`, `Acp.cs`
  (`Measure`), `StructuredOutput.cs` (`rate_limit_event`), `Planner.cs` (the cap and `SessionView`),
  `DriverConfig.cs` (`Cap`, `CoolOff`), `ChatRunner.cs` (a conversation's and Ask Daoris's selection),
  `HelpRoomDoors.cs`, and the CLI's `cli/agent.ts`; and `docs/2026-09-25-stream-json-evidence.md` for the native
  frame that came on an ordinary turn.
- **The evidence is D125 §0.2's**, as given to that branch; no transcript or record was read here. That observation 2's
  two sessions shared one account rests on their one reset.
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
- **Not measured**: every item of §10.
- **`verify` checks** this document's links, the decision log's shape, the router's row, the budgets and the
  duplicates, and none of these words.
