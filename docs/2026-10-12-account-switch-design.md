# Accounts switch on their own, by a curve, on each account's real use

The design under D165. Written 2026-10-12 from the install's readings, the account-use design
(`docs/2026-10-02-account-use-design.md`, D130 and its §16), the limit-signals evidence
(`docs/2026-10-02-limit-signals-evidence.md`) and the pinned Claude Agent SDK's declaration.

## 0. The owner's direction

> *"usage should be auto switch by some config (we can have a smart switch for default and custom curve by user if they
> really want to have the control) instead what we currently have as continue with other account? which is kind confusing
> and we currently do have an issue with the real usage check"*

Three asks: **switching is automatic**, by a setting rather than a press (§3, §4); **a smart default, and the person's own
curve** for whoever wants control (§2); and **the usage check reads the real use** (§1).

## 1. What is wrong with the usage check today

Daoris knows an account's standing only from what a session of its own says while it runs (D130 §5.2): Claude Code's
`rate_limit_event` on either door, Codex's app-server read at a press and at a start over a list (CODEXUSE1). The
install's `windows.json`, read 2026-10-12, shows four faults:

| Fault | Seen on the install | Why |
|---|---|---|
| **The agent's pace warning is read as *near*** | `account-1`'s week *near* at 60% used, with 22% of its week gone | Claude Code's `allowed_warning` is the CLI's own judgement: a window at or over a threshold of use while the time elapsed in it is at or under that threshold's share (evidence §1.3). It says *ahead of pace*, not *near the limit*. `AccountReadings.NearWindow` takes it as near (`NearBy.Word`), so the walk passed an account with 40% of its week left |
| **A reading is only as fresh as the last session there** | `account-1`'s readings 15 hours old; its session window's reset passed, so that window is unknown | No read is made of an account no session runs on (D130 §5.3, *no probe*), so the walk ranks idle accounts on old words or none |
| **The person's own use is unseen** | — | One allowance covers every surface (C4): the person's terminal and the maker's apps spend it, and Daoris learns of that only when its next session there speaks |
| **Codex's own sign-in is never read** | its readings 5 days old, though a Codex conversation ran on 2026-10-12 | `codex-acp` forwards none of Codex's limits (evidence §4), and CODEXUSE1's read runs only at a press and at a start over a list, which the own sign-in never is |

The walk then ranked by those words: with `account-1` *near* by the word and another account *near* at 99% of its week,
a conversation opened on the 99% one. ACCTWALK3 ranks near accounts by their most used window, which mends that order;
it does not mend the reading.

### 1.1 The real read, at no model spend

Claude Code answers its plan's windows on request, without a model call: the SDK's `get_usage` control request, the
structured data behind `/usage` (SDK 0.3.284, the install's pin: `sdk.d.ts:4195-4260`). Its answer carries
`rate_limits.five_hour`, `.seven_day` and the per-model weeks, each `utilization` 0 to 100 and `resets_at` in ISO 8601,
*"from the claude.ai usage endpoint"*, and `subscription_type`; `skip_behaviors: true` skips its scan of local
transcripts. It is declared *experimental, may change*. Codex's app server answers `account/rateLimits/read` the same
way, already read by CODEXUSE1.

So **Daoris reads each account's real use, which includes the person's own**, by asking the agent under that account's
home, with no prompt and no model request. This amends D125 §1.4 and D130 §5.3: a start made only to ask was refused
because it spends allowance; a read that spends none is not that start. USAGEREAD1 measures it first: the answer's shape
on the pinned CLI, the time it takes, and that no model request is made (no assistant message, a session cost of 0, and
the five-hour use unchanged over repeated reads).

### 1.2 When it reads

- **At a start over a list**, for each candidate whose newest reading is older than five minutes, in parallel, bounded
  at ten seconds. A failed read keeps the older reading, its age shown; absent is never zero (D57).
- **At a running session's turn's end**, for the other accounts of its scope, only where its own account is over its
  curve or within ten points of it, so idle accounts are read when a switch is near and not otherwise.
- **On the Accounts screen**, as it opens and at its *Read now*, where a reading is older than a minute.
- **Never** more than once a minute per account, nor for an account cooling until a reset it knows.

A session's own frames still write as they come; each reading records its source (`read`, the maker's answer, or `said`,
a session's frame) and when, and the newer wins.

### 1.3 The pace warning is kept, and is not near

Claude Code's `allowed_warning` is recorded as the agent's own warning (`warned`) and shown on the account's row in its
own words, *Claude Code says it is ahead of its pace*. It no longer makes an account near alone: near is the account over
its curve (§2). A refusal stays a refusal, and an account drawing on usage credits stays passed (D130 §6).

## 2. The curve

**Per window, the most of it an account should have used by each point of the window's time.** An account under its
curve on every window has room; over it on any window it is *over*. The person chooses, per agent, for the machine or one
workspace, in the scope's account settings (`rotationUse`, D130 §16.6):

| `switch` | What it does |
|---|---|
| `smart` (default) | Daoris's curve, below |
| `curve` | the person's own points, per window |
| `off` | no switch while a session runs; the walk at a start still passes an account over the smart curve; a limit still carries a session on (TOOL4f) and the offer stands (§4) |

**The smart curve:**

- **Five-hour window: 90%, flat.** It recovers in hours, so it is spent, and a margin is kept so a long turn is not cut
  off (D130 §6's 90, the higher of the rotating tools' thresholds, T1, T2).
- **Weekly window: its pace plus 20 points, never above 95%.** At the week's start 20% may be used before the account
  counts as over; at half the week 70%; from three-quarters on 95%. The week weighs more than five hours (D130 §16.2): an
  account far ahead of its week is spared while another has room, and none is run into its weekly limit while another
  could carry the work. At most 5% of a week lapses at its reset by this curve; the person's curve can spend it.
- **Per-model weeks** (`seven_day_opus`, `seven_day_sonnet`) take the weekly curve where the agent reports them.

**The person's curve** is a list of points per window, `[elapsed %, used %]`, joined by straight lines: elapsed from 0 to
100 and rising, used from 0 to 100 and never falling. One point is a flat line. For example
`"weekly": [[0, 30], [50, 70], [100, 100]]`. A curve that breaks those is refused with what is wrong, on both doors.

**Room needs a margin, and a move holds.** A switch goes only to an account at least ten points under its curve on every
window, not cooling, not refused, in the scope's list. A session that moved does not move again within thirty minutes,
unless its account refuses it. Both stop a session flip-flopping between two accounts near their curves (claude-swap's
cooldown and margin, T1).

## 3. The walk at a start

D130 §16.3's walk keeps its steps, with two read from the curve:

- **Step 2, near last**, becomes **over its curve last**: an account over its curve on any window goes to the end; among
  those, the one least over first (ACCTWALK3's order, measured against the curve).
- **Step 5, furthest behind its week's pace**, becomes **most room under its curve first**, on its tightest window.
- **Step 4, its week lapsing**, weighs what would lapse and not only when (USAGERESET1, D165's note of 2026-10-19): within the
  last day, the most weekly room under the curve per hour to its reset first, from a reading only; and the smart week's tail
  rises evenly from 95 to 100 at its reset, amending §2's cap.

With `switch: off`, step 2 reads the smart curve and step 5 stands as D130 wrote it.

## 4. A running session moves at its turn's end

Under `smart` or `curve`, when a turn ends and a reading says the session's account is over its curve, and an account of
the same agent in the scope has room (§2), **the session goes on there, on its own**:

- **A quest's session** concludes and its carry-on starts on that account at once, in the same tree, as TOOL4f's carry-on
  after a limit does (Daoris's record: the request, last plan, last words, what is uncommitted). `account.rotated` says why
  (`curve`).
- **A conversation** goes on in a new conversation on that account, by the hand-over's machinery with its agent unchanged
  (MIXAGENT1a: the box, asked by `daoris`, why `curve`). The person's next words go to it.
- **Never mid-turn, never during a wait on the person**, and never to an account outside the scope's list. A conversation
  the person started on a named account moves too: the setting is the person's standing word, and `off` is its door.
- **Said where it happened**: the old session's last line and the new one's first, *moved to `account-3` at the end of its
  turn: `account-1` had used 62% of its week, over its curve (45% now); `account-3` has room*, as coded notes in both
  languages; the head's *continues …* line (MIXAGENT3a).

**Where no account of the agent has room**, the session stays. Where the `handOver` rule (D163) is `on`, it goes on with
another agent's account that has room; where it is `ask`, the hand-over offer appears (MIXAGENT1b-ii and 1b-iii). So the
offer is the fallback, no longer the way an account is switched.

**What a move costs** is the agent's own context: a carry-on is a fresh conversation from Daoris's record (D125 §3.5,
point 5), as every hand-over is. SWITCHCTX1 measures whether a Claude Code conversation resumes on another account's home
with its transcript carried; if it does, an amendment may let a move keep the context. Until then, the smart curve's
margins keep moves rare.

## 5. What the person sees

- **Accounts**: each account's windows as used against its curve now, *62% of its week · its curve allows 45% now ·
  read 2 min ago*, the source (*read from Claude Code* or *said by a session*), the agent's own pace warning where it gave
  one, and *Read now*.
- **The switching setting**, beside *Use accounts*: *Smart* (default), *My own curve*, *Off*. A small chart draws the
  curve for each window with each account's point on it, and *My own curve* edits the points. Both languages.
- **A session's head and timeline**: the move, why, and where it went (§4).
- **The terminal**: `daoris agent profile use <agent> --switch smart|curve|off [--curve weekly=0:30,50:70,100:100]
  [--curve session=0:90] [--no-curve] [--workspace W]`, and `daoris agent list` prints each reading's source and age. A
  curve's windows are named as `windows.json` names them: `session` (the five-hour window) and `weekly`.
- **Ask Daoris**: its `use` door takes `switch` and `curve`.

## 6. The settings

`switch` and `curve` join `use` and `keep` in each agent's entry of `rotationUse` and `workspaceRotationUse`
(`harnesses.json`), and replace `early` and `near`:

```json
{
  "rotationUse": { "claude-code": { "switch": "curve", "curve": { "weekly": [[0, 30], [50, 70], [100, 100]] } } }
}
```

- Absent `switch` is `smart`. A window the person's curve leaves out takes the smart curve's. A curve is kept whatever
  `switch` says and applies only under `curve`; `--no-curve` removes it. Its points are whole numbers, one to twelve of
  them; before the first point the curve is the first point's use, after the last the last's.
- **Read on, never lost**: `early: false` reads as `switch: off`; `near: N` other than 90 reads as `switch: curve` with
  the `session` window flat at N (under `off` the curve is kept). A writer writes `switch` and `curve`, and drops `early`
  and `near`. Until SWITCH1b moves the walk to the curve, the walk keeps reading `early` and `near`, derived from them.
- Both twins write the same bytes, from one table (`fixtures/`), as D130 §14 says.

## 7. What this amends, and what it rejects

**Amends**: D130 §6 and §16.3 steps 2 and 5 (near is the curve; the agent's warning is kept, not acted on alone);
D130 §16.6 (`early` and `near` become `switch` and `curve`); D130 §5.3 and D125 §1.4 (a read that spends nothing is
made); D163's *automatic hand-overs by default* rejection, for another account of the same agent (another agent stays
the person's rule); TOOL6d's press, superseded by the automatic move and MIXAGENT1b's press.

**Rejected**:

- **Switching mid-turn**: a turn cut off loses its work in flight (D130 §16.1's stalls).
- **A fixed percentage alone**: the week needs time in it; 60% on day one and 60% on day six are different accounts.
- **Taking the agent's warning as near**: it measures pace by the maker's thresholds, which are unpublished (evidence
  §7), and the person cannot set them.
- **Reading the usage endpoint with Daoris's own client**: it would hold the account's credentials, which D125 never
  reads; the agent asks under its own home.
- **A start made to ask**: it spends allowance; the read here spends none (USAGEREAD1 proves it or the row stops).

## 8. The rows

| Row | What | Lanes |
|---|---|---|
| USAGEREAD1 | Measure `get_usage` on the pinned CLIs and Codex's read on the own sign-in: shape, time, no model request. Evidence document | evidence |
| USAGEREAD2 | Read each account's real use (§1.1, §1.2): per agent's table entry, `source` and age in `windows.json`, the schedule, the bounds; `daoris agent list` | driver |
| USAGEREAD3 | The pace warning is kept as `warned`, never near alone (§1.3) | driver |
| SWITCH1a | The `switch` and `curve` settings, both twins, read on from `early` and `near` (§2, §6) | driver, cli |
| SWITCH1b | The walk by the curve (§3) | driver |
| SWITCH1c | A running session moves at its turn's end (§4): quests' carry-on and conversations' hand-over to an account with room, the margin and the hold, coded notes | driver, modules |
| SWITCH1d | The view and the doors (§5): Accounts against the curve, the switching setting with its chart and editor, Ask Daoris's keys | web, modules |
| SWITCH1e | The offer becomes the fallback (§4) | web |
| SWITCHCTX1 | Measure carrying a conversation's own transcript to another account (§4); it spends two short turns, the owner's call | evidence |
