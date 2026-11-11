# STAFFPOLICY1: a role's staffing as a policy of weighted choices and the conditions that move a line to another choice

Status: accepted by the steward 2026-11-02 (D226), the synthesis of both sealed proposals; the read runs beside the first rows.
It answers `archive/2026-11-02-staff-policy-brief.md` from `archive/2026-11-02-staff-policy-proposal-opus.md` and
`archive/2026-11-02-staff-policy-proposal-codex.md`, each claim about today's code checked at `2b28fdbaa` before it is relied on. It
amends D205, D206, D197, D210, D200, D163, D189 and D212 (§7). Web paths start at `src/Daoris.Web/src/`, driver paths at
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, the bridge's at `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/`.

## 1. What both proposals decided alike

- **No policy is today's walk:** D205 point 4's order; installing this changes no staffing and no spending (D205 point 7's rule).
- **One judge inside the one walk:** composed into `StartPlan` (`Roles/StartPlan.cs:153-308`), no second router; D197's account walk
  stays inside each choice; `CourseDecision` keeps what never goes on (`Course/Domain/LineCourse.Decide.cs:18-24`).
- **A weight shares new starts,** never tokens, time or outcomes; a continuation keeps its asked choice; a good turn rebalances nothing.
- **The weighted pick is deterministic,** ties by order; a pending start counts; a preview reserves nothing and asks no account.
- **Inheritance is the whole role's:** the most specific scope's role (`Roles/RoleCatalog.cs:167-191`) with its own policy, nothing layered.
- **A choice may be limited** to the works it takes and to a daily budget (Codex's `works`, `dailyAdmissions`; Opus's `takes`, `budget`).
- **No price and no inferred success:** Daoris holds no price (`Agents/Usage.cs:44`) and its use line counts starts, not outcomes
  (`Roles/RoleUse.cs:18-23`).
- **A move to another choice is a new part handed D189's brief,** never mid-turn, after the outgoing process ended; never a replay.
- **The failure classes are D210's** (`Agents/AgentErrors.cs:6-19`), never an answer's text; the transient schedule stays as it is
  by default (`Agents/TransientRetry.cs:21-28`); a policy never ends a quiet run (D200, `Events/SessionWatch.cs:63-66`).
- **Rules say who, weights say which alternative:** D155's maker passes and the helper's and partner's rules decide the agent; a
  brainstorm's voices stay frozen (`Loop/DriverBrainstorms.cs:396-408`).
- **One editor and the person's press:** `RoleEditor` everywhere, presets that fill a draft, `SET_ROLE`'s authority, `role_propose`
  proposing, D222's words.
- **Automatic moves are bounded,** three a line by default where the person sets any; **the record says the rule and the why,**
  machine-local, the asked, acknowledged and reported tiers apart (`work/actors.ts`).

Where they differed, the steward settled: the policy apart from the choices (§2.1), two rules (§2.3) and the conditions as course
facts (§3.3), each Opus's; a choice's identity as its tuple (§2.2, Codex's stable ids); a conversation's five tries and budgets at a start
only (§4.2).

## 2. The policy's shape, store and defaults

### 2.1 Stored apart from the choices

- The policy is a row of `role_policy`, one per role id, beside `role_staffing` in the desktop store; never a field of a choice.
- Both of today's writers of choices write only `agent`, `model` and `effort` (`Settings/ActorKey.cs:143-165`,
  `Roles/RoleStaffing.cs:701-720`), and both readers read only those (`Settings/ActorKey.cs:93-138`, `Roles/RoleStaffing.cs:723-743`):
  a weight beside `agent` vanishes at any build's next write, so Codex's single `staffing` value inside the role set is refused.
- Store step 16 adds three tables at the stamp step 15 left (`Store/DesktopStore.cs:65`, `:512`), raising none; an older build sees
  none of them (`.claude/knowledge/compatible-builds.md`).

```sql
CREATE TABLE role_policy (role TEXT PRIMARY KEY, policy TEXT NOT NULL,   -- §2.2's record
  staffing TEXT NOT NULL,                       -- sha256 of RoleStaffing.Write(choices) it was set for
  revision INTEGER NOT NULL, since TEXT NOT NULL, at TEXT NOT NULL);   -- since: the last save changing the rule or a weight
CREATE TABLE role_policy_revisions (role TEXT NOT NULL, revision INTEGER NOT NULL, policy TEXT, staffing TEXT,
  at TEXT NOT NULL, PRIMARY KEY (role, revision));
CREATE TABLE role_starts (session TEXT PRIMARY KEY, role TEXT NOT NULL, choice TEXT NOT NULL,   -- a row per new start, at its open
  by TEXT NOT NULL, condition TEXT, at TEXT NOT NULL);
```

- **The digest binds the policy to the staffing it was set for.** `RoleStaffing.Set` and `SetAdded` (`Roles/RoleStaffing.cs:325-375`),
  which `SET_ROLE`, `SET_ROLES`, `SET_ACTOR`, `role set` and `actor set` all reach, re-key it in the choices' lock and transaction;
  `Commit` itself never does. The catalogue's record keeps today's separate write (`Roles/RoleDoor.cs:50-73`, Codex §8).
- **Any other writer leaves it stale:** an older build's write the check finds, *Take it* (`Roles/RoleStaffing.cs:435-442`), the
  check's turn-off and a projection repair. A stale policy is not applied: the role runs in order, every move as today, and says so.

### 2.2 The record

```jsonc
{
  "rule": "weight",                                    // "order" | "weight"
  "choices": [                                         // keyed by the tuple; their order is the staffing's
    { "agent": "claude-code", "model": "sonnet", "effort": "high", "weight": 3 },
    { "agent": "claude-code", "model": "opus", "effort": "high", "weight": 1, "share": 40, "budget": { "starts": 6 }, "takes": ["design"] }
  ],
  "moves": { "limit": "weighted", "transient": { "do": "weighted", "after": 2 }, "quiet": "next" },
  "most": 3
}
```

- **A choice's identity is its tuple,** agent, model and effort as `RoleStaffing.Write` writes them, unique in a role since
  `ActorKey.Judge` refuses two alike (`Settings/ActorKey.cs:55-80`): Codex's stable ids, with no id written into a choice.
- Re-keying keeps each entry whose tuple is still listed, gives a new choice weight 1 and no limits, drops a removed choice's entry,
  sets the digest and keeps one revision. `role_starts.choice` keeps the tuple, so a reorder never moves the spread's counts.

| Field | Range | Meaning | Kind |
|---|---|---|---|
| `weight` | integer 0–10, under `weight` only, one above 0; default 1 | aims at `w / Σw` of the role's new starts; 0 runs only when no other can | — |
| `share` | 1–99 % | at most this share of the window (§3.2); over it, walked after every choice under its share | soft: runs when nothing else can |
| `budget` | `{ starts: n }` or `{ tokens: n }` a local day, in the machine's zone as D125 reads a reset | over it, passed `over-budget` before any account is asked | hard, at a start only |
| `takes` | a non-empty subset of the role's works | the works the choice serves: a role taking build and design can give design to one choice | filter |

- An edit never refills a budget: the day's starts and tokens stay counted (Codex §2).
- A `tokens` budget counts the day's reported tokens on its choice, the usage turn line gaining `choice` beside `actor`
  (`Agents/UsageHistory.cs:637`); missing usage is unknown, never zero, so an unreported turn holds the choice as over (Codex §5). The
  editor says *reported tokens*, and that a turn can pass it before its usage arrives: an admission guard, not a cap.
- `moves` maps a condition to an action (§4), an absent condition *as today*. `most`, 1 to 8, is a line's moves to another choice in 24
  hours: absent while every move is *as today*, 3 once the person sets any other (the steward's settlement of Opus's open point 1).

### 2.3 The rules

- `order` is the stored order, D205 point 4's walk: the default, and the rule of every role without a policy or with a stale one.
- `weight` is §3.2's deterministic pick; *Spread evenly* is every weight 1. By cost and by measured success are not built (§9 row h).

### 2.4 Defaults and presets

- **No row** reads `order`, no limits, every move as today and no `most`: every role today. Nothing is migrated.
- **A home whose staffing is not in the store,** or one `DAORIS_DRIVER_CONFIG` names, keeps no policy, as `SetAdded` refuses there
  (`Roles/RoleStaffing.cs:358-361`); the editor says *Policies are kept in this machine's store, which this home does not use yet.*
- *Restore role defaults*, *Clear staffing* and removing a role delete the row, its revisions kept. A brainstorm role keeps none: its
  entries are voices that run together (D205 §4.4). A preset fills the form and never saves, in Daoris's words keyed in every catalogue.

| Preset | en / zh | Fills |
|---|---|---|
| `first` | *First that can run* / 第一个能运行的 | `order`, nothing else |
| `even` | *Spread evenly* / 平均分配 | `weight`, every weight 1, `limit: weighted`, `most: 3` |
| `weighted` | *Spread by weight* / 按权重分配 | `weight`, the person's weights, `limit: weighted`, `most: 3` |

- *Cheapest first* is no preset: the order already is the person's cost preference, and *First that can run*'s ⓘ says *Put the
  lighter choice first; Daoris knows no prices.*

### 2.5 The policy's judge

- `StaffPolicy.Judge(role, choices, policy)` runs at every door before anything is written, as `RoleDoor.Judge` does, adding the
  code `policy` to the role refusals' closed set (`Roles/RoleCatalog.cs:255`).
- It refuses a weight outside 0–10 or all 0 under `weight`; a share outside 1–99; a budget not a positive integer; `takes` not a
  subset of the role's works, or a work no choice takes; an action its condition does not allow (§4); `after` or `most` out of range;
  an entry whose tuple the choices do not list; any policy on a brainstorm role; a policy revision that moved since it was read
  (*the policy changed since you read it*, the draft kept).

## 3. The judge

### 3.1 `StaffJudge.Arrange`: pure, values in

- `StaffJudge.Arrange(policy, work, order, facts)` answers the entries in walk order, each with its `by` (`first`, `weight`, `share`,
  `budget`, `work`) and its sentence; nothing is read inside. Its exemplar is the curve's turn-end judge (`Agents/Domain/SwitchDecision.cs`).
- Its facts: the window's starts by choice and this look's reservations; today's starts and reported tokens by choice; the agents
  marked not answering (§4.2); the choices this line moved off within 30 minutes; now.
- In order: entries whose `takes` excludes the work leave; entries over today's budget are passed `over-budget`; where the role moves
  on `transient`, a choice moved off within 30 minutes is passed `moved-off`, then an agent marked not answering `not-answering`, each
  while another entry remains (`SwitchDecision.Hold`'s 30 minutes, `Agents/Domain/SwitchDecision.cs:68`); then the rule orders the rest.
  The moved-off pass is first (§8's codes, REVIEW2 L4): where one choice is left of the two, the line does not walk back onto the one
  it has just left.
- Only the role's own entries (`From` 1 to 4) are arranged; a pick, a handed agent, *as before* and `rule-default` walk as given;
  `StaffJudge.OnCondition(policy, condition, facts)` answers a met condition's action (§4), pure, for the course decision.

### 3.2 The weighted pick

- The window is the role's last 20 new starts since `role_policy.since`.
- Each entry above weight 0 scores `deficit = w / W × (N + 1) − n`, `n` its starts among the window's `N`: the highest first, ties by
  the stored order; entries over their share after those under it; weight-0 entries last, in order.
- From an empty window 3:1 goes 1, 1, 2, 1, 1, 1, 2, 1, six to two over eight starts: a fixture row.
- The window of 20 bounds catch-up for a choice back after a day; counting starts, not time, keeps the spread off the clock. A save
  changing the rule or a weight sets `since`, so a new spread has no burst to make up (Codex §3); a reorder, a limit or a move does not.
- A new start's open writes its `role_starts` row (role, choice, `by`, the condition where it moved); a continuation writes none.
- **Two starts in one look** (PARALLEL1c): `Arrange` reserves its top pick under the ledger's lock, the settled selection moves it to
  the entry taken, and a refusal gives it back in the `HarnessRoster.Release` that returns the account's count
  (`Agents/HarnessRoster.cs:1982`); reservations live in memory, so a restart drops them and keeps `role_starts`.

### 3.3 Where it is decided

| Moment | Today's seam | With a policy |
|---|---|---|
| A quest's first start | `WalkBuildPassesAsync` → `BuildPlan` (`Loop/Driver.cs:796-799`, `:891-914`) | `BuildPlan` hands the policy and facts; `Arrange` runs before `LoopAsync` |
| A driven line at a limit (WALKAGENT1) | `limitWalks` (`Loop/Driver.cs:2276-2293`, `:2358-2366`) | the `limit` move: a walk by `next` or `weighted`, `wait`, or `ask`; D206's own resume first |
| An in-start move: a refused model, a sign-in | MODELREFUSE1 and LIMITPICK2 (`Loop/Driver.cs:1477-1512`) | the same once-each mechanism; its walk is arranged and counts toward `most` |
| A new conversation | `ChatRunner.WalkAsync` through `StartPlan` (`Loop/Chat/ChatRunner.cs:1036-1060`) | arranged |
| A conversation at its agent's limit (LIMITGOON2) | `LimitMove.ElsewhereAsync` (`Course/Chat/LimitMove.cs:873-899`) | the bound role's arranged order; `wait` waits as a pick does |
| A briefed press, a pending conversation | `BriefedMove` (`Course/Chat/BriefedMove.cs:281`), `ChatPending.Recovering` (`Course/Chat/ChatPending.cs:200-219`) | the bound role's arranged order |
| A hand-over offer, an agent's ask under `on` | `HandOverOrder.Steps` (`Course/Domain/HandOverAsks.cs:141-159`), the page's `nextChoices` (`work/start/handOverOffer.ts:140`) | the arranged order, both held to `hand-over-order.json` |
| A helper's, partner's or reviewer's pass | `HelperChoice.Order` (`Participants/Helpers/HelperChoice.cs:48-62`), `PartnerChoice`, the reviewer's loop (`Participants/Domain/ReviewerChoice.cs:337`) | the rule and the maker passes decide the agent; `Arrange` orders its alternatives |
| A brainstorm's pass | `Loop/DriverBrainstorms.cs:396-408` | none: voices are read once and frozen, and the judge refuses a policy |
| A continuation | `StartPlan.ContinuedOn` (`Roles/StartPlan.cs:351-361`) | its record's asked choice; only a condition moves it |

- **The conditions are facts of the one course decision** (D212 point 6): `CourseFacts.Policy` carries the met choice, the action
  `OnCondition` answered and the line's moves in 24 hours; `CourseDecision.Decide` stays the one decision, as today with no policy.
- **D197's account walk is unchanged inside a choice;** no account is ever named or assigned (D205; D197 points 1, 2 and 8).
- **Predictable:** the policy and facts are read once per decision, as the order is; `ROLES` answers each role's `next`, a probe that
  reserves nothing and asks no account. **Testable:** `Daoris.Desktop.Driver.Tests/fixtures/staff-policy.json` holds policy and facts
  in, arrangement and action out; a census holds every reader of a role's order to `RoleCatalog.Arranged` or Brainstorm's exemption.

## 4. The conditions and their actions

The actions: `next` walks the stored order from its top, the met choice passed (WALKAGENT1's walk); `weighted` is that walk arranged
by weight; `wait` holds for the met choice's earliest moment back (its accounts' reset, the budget's day, the health mark's end),
with an optional `upTo` of 5 to 600 minutes after which the line walks by the role's rule; `ask` holds, offering *Go on now with
{next}* (D206 point 7's press); `hold` holds with today's doors (*Try again*, the person's words); `retry` is the same choice again,
today's carry-on. Every move to another choice is a new part handed D189's brief, at a part's end.

| Condition | How Daoris observes it today | Actions allowed | *As today*, the default |
|---|---|---|---|
| `limit`: every account of the choice limited or cooling | the conclusion's limit (`Loop/Driver.cs:3321-3366`); the walk's `cooling` pass (`Roles/StartPlan.cs:25-26`); not on Claude Code's native door until TOOL4n (`TASKS.md`) | `next`, `weighted`, `wait`, `ask` | `next` where the role lists more than one choice or another agent, else `wait`; D206's own resume first, always |
| `signed-out`: a door's access refusal | `AccountSignedOut`; LIMITPICK2's in-start account move (`Loop/Driver.cs:1477-1494`) | `next`, `weighted`, `ask`, `hold` | the in-start account move once, then `next` at the walk |
| `model-refused` | `ended.NotOffered` and the pipe's `ModelRefused` (`Loop/Driver.cs:1496-1512`, `:3353`) | `next`, `weighted`, `ask`, `hold` | `next` within the same start (MODELREFUSE1) |
| `transient`: the service does not answer | D210's episode; past its bound the part ends `ended.transient` | `next`, `weighted`, `ask`, `hold`; a driven line's `after` 1 to 5 tries | `hold` after five tries (31 minutes), for the person (`WorkOrders/Domain/Planner.cs:845`) |
| `failed`: a fatal error again | D210's fatal, a strike; three strikes park (`Settings/DriverConfig.cs:38`) | `next`, `weighted`, `ask`, `hold`, `retry`; `after` 1 to strikes − 1 on one choice | `retry` until the strikes park |
| `quiet`: past the inactivity window | `SessionWatch`: marked at 10 minutes, brought to the person at three times it, ended only by a press or a headless `--quiet-end` (`Events/SessionWatch.cs:63-90`) | `next`, `weighted`, `retry`, for the carry-on after the run ended | `retry` |
| `budget`: the choice's daily budget reached | `role_starts` by day; reported tokens by the turn line's `choice` | at a start: `next`, `weighted`, `wait` (to the day's turn), `ask` | `next`, a pass like any |

### 4.1 The fixed rules, said as fixed in the editor

- Passed before any account, never configurable: `unavailable`, `no-posture`, `no-connector` (`Loop/Driver.cs:896-902`), and D205's
  `not-admitted` and `not-listed`. The cap waits for a slot. A pick and a handed agent never walk; a role *as before* waits.
- Nothing moves mid-turn (D205 point 5); a turn that ended in the agent's own words is never a condition (D205 §4.3); an uncertain
  send is never replayed, the brief carrying it by its word ids (D212 point 9).
- The outgoing process is confirmed ended before the next part opens, else the line holds (Codex §6); a failure of Daoris's own store
  or service holds the next start and never moves the line to another agent (Codex §5).

### 4.2 Bounds, health and budgets

- A conversation keeps D210's five tries: `after` shortens only a driven line's episode, and a conversation's `transient` move acts
  past the bound (the steward's settlement of Opus's open point 2). `failed`'s strikes count across choices, so the park bounds a spin.
- `most` counts a line's moves to another choice in 24 hours from its parts' `from_why` codes, never moves between accounts of one
  choice; reaching it makes the next action `ask`, held as `held.moves-bound`; nothing renews it (Codex §5). *First that can run* sets
  no `most`: today's bounds stand (D206's one attempt per cool-off, the in-start moves once each, the strikes).
- A transient episode past its bound, or a driven line's `after`, marks its agent not answering for 15 minutes or until a prompt on it
  answers: in memory, the agent's, never an account's cool-off (D197 point 8), read only by roles that move on `transient`.
- A budget passes a choice at a start and never moves a running session; its mid-session move at a turn's end is cut and filed (§9 row
  h), the steward's settlement of Opus's open point 3.

## 5. How a choice and a move are said

- **Said exactly where the policy changed the outcome:** a role on *First that can run* says what it says today, and `ActorLine`
  (`Roles/ActorOpenings.cs:57`) gains *by weight*, *its share*, *the only choice taking design* and *moved*.

| Where | What |
|---|---|
| The run | the new part's opening note `opening.moved-choice`: *Moved here from choice 1, Claude Code · opus · high: its service did not answer after 2 tries (3 minutes). Build's policy moves on that; 1 of 3 moves today.* Agent and model, never an account, as `SayMovedOffModel` says it (`Roles/ActorOpenings.cs:43`); the old part's `moved.on` names the choice it went to |
| The head | the chip extends `choiceChip` (`work/actors.ts:27`): *choice 2 · after a limit*, *· service didn't answer*, *· after 2 failures*, *· after a quiet stop*, *· over its budget*, *· by weight*; *Details* gains *Chosen*, the sentence |
| The role | its use line counts moves by condition beside *fell to a later choice* (`agents/roles/Roles.tsx:146`; `Roles/RoleUse.cs` reads a new `by` on `actor.chose`); *Next new start: choice 1 (by weight: 3 of the last 4 were choice 1, aim 3 in 4)* |
| The record | `chosen` `{ rule, by, condition?, from? }`, machine-local through every place of the session record's chain (`.claude/knowledge/session-record-fields.md`), off the wire; `from_why` gains `transient`, `failed`, `quiet`, `budget` (`Course/Domain/CourseAnswers.cs:223-282`); the machine log gains `actor.moved` `{ role, line, from, to, condition, action, n }`, and `actor.chose` gains `rule` and `by` |

- The asked, acknowledged and reported tiers stay three (D205 §9.3); an absent reported model stays *not reported* (Codex §7). `ask`
  and `held.moves-bound` hold as a quest holds today: *What needs you* gains no kind (D205 point 4), and no move is a notification.
- The word is *move to another choice* (改用其他选择), D205's own zh (`locales/zh/settings.ai.json:60`), never *switch*, D197's
  *Switch accounts* (切换账户, `locales/en/harness.json:105`). *Ready again* stays 重新可用 and *limited* 额度用尽 (`locales/glossary.json:712-713`).
- The glossary gains *weight* (权重), *share* (占比) and *daily budget* (每日预算).

## 6. The person's surfaces

### 6.1 The role editor (SETUPUX1l's `RoleEditor`)

- One control and two folds join the editor; its rows stay `FormGrid` rows (`agents/roles/RoleEditor.tsx:92-152`); `Draft` gains
  `weight`, `share`, `budget` and `takes`, `Editing` gains `policy` (`:12-18`); it stays hook-free.
- *Weight* and *Aim* show only under a weight rule; *Limits ▸* opens the share, the budget (starts or reported tokens) and the works.
- The ⓘ follows the rule: under `order` today's sentences (`:76-80`), under weight *Choices take new work by their weights; a choice
  that cannot run passes to the next by weight.*; `settings.ai.actors.later` and `settings.ai.roles.choicesNote` become the rule's.
- Weights are numeric inputs with keyboard steps, never drag-only; a refusal stays under *Save* with the draft kept (Codex §7). A
  stale policy opens with its fields kept: *Its policy was set for other choices; it runs them in order until you save it again.*

At 1546 px (Agents → Roles → Working roles, the main area about 80 rem):

```
Build   build, design · this machine · Claude Code · sonnet · high +1 · spread 3:1 · on a limit: next by weight      [Edit]
        14 starts this week · choice 1 ×10, choice 2 ×4 · moved to another choice 2 (limit ×1, service ×1)
┌ Build: its choices
│ How it chooses   ( ) First that can run   ( ) Spread evenly   (•) Spread by weight                                ⓘ
│   #  Agent             Model         Effort      Weight   Aim      Limits
│   1  [Claude Code ▾]   [sonnet ▾]    [high ▾]    [ 3 ]    3 in 4   ▸ none                                   ↑ ↓ ✕
│   2  [Claude Code ▾]   [opus ▾]      [high ▾]    [ 1 ]    1 in 4   ▸ design only · 6 starts a day           ↑ ↓ ✕
│   + Add a choice (at most 4)
│ ▾ When a choice cannot go on
│   It reaches its limit             [Go to the next by weight ▾]                         as today: the next choice
│   Its service does not answer      [Go to the next by weight ▾]   after [2 ▾] tries     as today: hold for you
│   It fails again and again         [Try it again ▾]                                     as today
│   It went quiet and was stopped    [Go to the next choice ▾]                            as today: try it again
│   It is over its daily budget      [Go to the next by weight ▾]                         at a new start only
│   At most [3 ▾] moves to another choice a day for one quest or conversation, then it waits for you.
│   Always: a refused model or sign-in goes to the next choice; your pick and a handed agent wait; nothing moves mid-turn.
│ Next new start: choice 1 (by weight: 3 of the last 4 were choice 1).                         ▸ Edit role
└ Saving puts this to work at once: new work takes these choices; running work keeps its own.          [Save] [Cancel]
```

At 680 px (the list closed to its strip), rows stack by `FormField when="stacked"` and each move is label over select:

```
How it chooses  [ Spread by weight ▾ ]       ⓘ
Choice 1  Agent [Claude Code ▾]         ↑ ↓ ✕
  Model [sonnet ▾]  Effort [high ▾]  Weight [3]  aim 3 in 4  ▸ Limits: none
Choice 2  … Weight [1]  aim 1 in 4      ↑ ↓ ✕   ▸ Limits: design only · 6 starts a day
▾ When a choice cannot go on
  It reaches its limit          [Go to the next by weight ▾]
  Its service does not answer   [Go to the next by weight ▾]  after [2 ▾] tries
  …  At most [3 ▾] moves a day, then it waits for you  ·  Always: …
Next new start: choice 1 (by weight)       [Save] [Cancel]
```

### 6.2 The place's Roles fold

- `roleSummary` and `actorSummary` (`agents/roles/Roles.tsx:104-140`) gain the rule, its proportions, a move other than *as today*
  and the source, probing nothing (`ROLE_STRUCTURE`'s rule, `Bridge/Routes/DriverRoutes.cs:406`); `PlaceRoles` says which role applies
  there, its own or the one it inherits, and *Give this place its own role…* copies the choices and the policy.

```
▾ Roles here                                                         (a repository's or a workspace's Setup)
  Build    this repository's own · Claude Code · opus · high +1 · first that can run                    Edit
  Chat     this machine's · Claude Code · account default · spread evenly · at most 3 moves             Give this place its own…
  ⚠ Intake its policy was set for other choices; it runs them in order until you save it again          Edit
```

### 6.3 The run's head

- The chip extends `choiceChip` by `chosen.by`, within the `status` budget (16 and 5) by putting the condition in the ⓘ at 680 px.

```
1546:  #412 Build the policy store · daoris · Claude Code · opus · high   [choice 2 · after a limit ⓘ]   Working
 680:  #412 Build the policy store          [choice 2 ⓘ]
Details ▾  Role    Build (this machine)
           Choice  2: Claude Code · opus · high
           Chosen  Moved here from choice 1 at 14:05: every account of choice 1 reached its limit (ready again at 16:40).
                   Build's policy goes to the next by weight on a limit; 1 of 3 moves today.
           Asked model · effort / Acknowledged / Model that ran   (unchanged, D205 §9.3)
```

### 6.4 The terminal twin

- The twin lives in `RoleVerb` (`Roles/RoleVerbs.cs:12-17`) under the same judge and `SET_ROLE` door; `role <id>` probes nothing.

```
$ daoris driver role build
build  Build · build, design · this machine · revision 7 · policy revision 3 · spread by weight 3:1
  1  claude-code sonnet high   weight 3  aim 3 in 4
  2  claude-code opus high     weight 1  aim 1 in 4  budget 6 starts a day  takes design
  moves  limit=weighted  transient=weighted after 2  failed=retry (as today)  quiet=next  budget=next (as today) · most 3 a day
  next new start: choice 1, by weight, 3 of the last 4 were choice 1 · last 7 days: 14 starts, moved: limit ×1, transient ×1
$ daoris driver role policy build --rule weight --weight 1=3 --weight 2=1 --budget 2=6/day --takes 2=design \
      --on limit=weighted --on transient=weighted:2 --on quiet=next --most 3
$ daoris driver role policy build --preset first        # back to today's order
```

### 6.5 Ask Daoris

- `role_propose` gains `policy`, a preset's name or the record (`Daoris.Service.Mcp/HelpRoleTool.cs`,
  `Help/Proposals/HelpRoleProposals.cs`), judged by the same judge, carrying the policy revision it read, applied only by the person's
  press in both modes (D205 point 9); no `settings_*` grant reaches a policy.
- The card says it in the fold's words: *Build: First that can run → Spread by weight (3:1); on a limit: next by weight; at most 3
  moves a day. This can spend differently.* [Apply] [Not now] The room's facts gain each role's policy and its 7-day moves, so *why
  did this run on Opus?* is answered from records.

## 7. What changes in the decisions, and how older records read

- **D205.** Point 4's walk becomes the policy's arrangement, `order` reading as written; point 5 stands. §4.3 narrows: a part a
  policy-named class ended (transient past its bound or `after`, fatal after `after` strikes, a quiet stop) may be followed by a new
  part on another choice, handed the brief, never a replay. §12's rejections of a router, an *Auto* pool and cheapest-first stand.
- **D206.** Point 6's walk takes the `limit` move; the own resume comes first; a pick's, a handed agent's and *as before*'s waits stand.
- **D197.** Unchanged inside a choice: the policy reads readings and cool-offs and writes none; no account is assigned to a role.
- **D210.** Point 7's *It never walks* narrows: past its bound, or a driven line's `after`, a line may move to another choice in a new
  part; the retries up to there are unchanged, and a conversation's episode keeps five tries.
- **D200.** Unchanged: `quiet` acts only on a run already ended, by the person's press or a headless quiet end.
- **D163 and D189.** The hand-over offer's *next choice* is the arranged order's; agents' asks are not widened and Daoris's offer is
  still the person's press; no automatic move is taken from a running session; every move is D189's briefed new part.
- **D212.** `CourseDecision.Decide` stays the one decision (point 6), the policy its fact; the start plan stays the one walk (point 4).

How older records and roles read: a role with no row, or a stale one, runs in order. A record with no `chosen` reads as today, its
chip from `choice` and *Details* saying *the selection policy was not recorded* (Codex §8). A new `from_why` read by an older build
falls to none (`Course/Domain/CourseAnswers.cs:270-282`). An older build sees none of the tables and runs every role in order; the
editor's footnote says *An older Daoris runs these choices in order, without weights or moves.*

## 8. File by file

| File | Today | Change | Row |
|---|---|---|---|
| `Store/DesktopStore.cs:512` | step 15 is the last, at `WriterStamp` 2 | step 16: the three tables, at the stamp step 15 left | a |
| `Roles/StaffPolicy.cs`, `StaffJudge.cs`, `StaffLedger.cs` (new) | — | the record, tuple keys, digest, presets and `Judge`; `Arrange` and `OnCondition`, pure; `role_starts`, the reservations under one lock, the day's counts, the health mark | a |
| `Roles/RoleCatalog.cs:255`, `:428-444` | the refusal codes; `Order` returns `Effective` | the `policy` code; `Arranged(work, place, config, facts)` beside `Order` | a, c |
| `Roles/RoleStaffing.cs:325-375` | `Set` and `SetAdded` write the choices | re-key or write the policy in the same lock and transaction | b |
| `Roles/RoleDoor.cs:31-73` and its batch | rows, apply and batch carry choices | the policy and `policyState` too; `Apply(…, policy)`; a batch refused whole | b |
| `Roles/RoleVerbs.cs:12-17` | `role set …` | `role policy …`; `role <id>` shows the policy and the next start | b |
| `Bridge/Routes/DriverRoutes.cs:379`, `:541`, `:556` | `ROLES`, `SET_ROLE`, `SET_ROLES` | answer and read `staffPolicy`, `policyState`, `next` and the presets, in the `Person` class, no new route | b |
| `Help/Proposals/HelpRoleProposals.cs`, `Daoris.Service.Mcp/HelpRoleTool.cs` | `role_propose` without a policy | `policy`, its description, its row in `.claude/knowledge/setting-doors.md` | b |
| `Roles/StartPlan.cs:35`, `:153-166`, `:240-273` | the order read once and walked in order | takes the policy and facts and arranges before `LoopAsync`; one entry's fast path stays unless it has a budget or `takes`; the codes gain `over-budget`, `moved-off`, `not-answering` | c |
| `Roles/BuildWalk.cs`, `Roles/ActorOpenings.cs:43-63` | the passes' words; a later choice said with its passes | the new codes' sentences; weight, share, work and moves said; `opening.moved-choice` | c, e |
| `Roles/RoleUse.cs`, `Loop/Driver.cs:891-914`, `Agents/HarnessRoster.cs:1982` | sums `actor.chose`; `BuildPlan` without a policy; `Release` returns the account's count | reads `by`, moves per condition; hands the policy and facts; also returns the ledger's reservation | c |
| the session record's chain; `Settings/ActorKey.cs` | its machine-local fields; the choices' shape | `chosen`, off the wire; `ActorKey` unchanged, on purpose (§2.1) | c |
| `Course/Chat/LimitMove.cs:873-899`, `Course/Chat/BriefedMove.cs:245-282`, `Course/Chat/ChatPending.cs:200-219` | `ActorChoices.Effective` of the seeded id | the bound role's `Arranged`; `wait` as a pick's | d |
| `Course/Domain/HandOverAsks.cs:141-159`, `work/start/handOverOffer.ts:140` | the stored order | the arranged order; `hand-over-order.json` gains rows | d |
| `Participants/Helpers/HelperChoice.cs:48-62`, `Participants/Partners/PartnerChoice.cs`, `Participants/Domain/ReviewerChoice.cs:337` | the fence, then the entries | arranged within one agent's alternatives | d |
| `Loop/DriverBrainstorms.cs:396-408` | voices read once and frozen | unchanged, the census's named exemption | d |
| `Loop/Driver.cs:2276-2293`, `:2358-2366`, `:3321-3366` | the limit's walk or hold; the conclusions | the `limit` move (`held.policy-waits`); a transient end by a driven line's `after` | e |
| `Agents/TransientRetry.cs:21-28`, `Agents/UsageHistory.cs:637`, `WorkOrders/Domain/Planner.cs:845` | a bound of five tries; the turn line names `actor`; `TransientHolding` holds | a driven run's bound is the role's `after` where it moves on transient; the line adds `choice`; the carry-on planned where the course answers a move | e |
| `Course/Domain/CourseFacts.cs:264`, `CourseAnswers.cs:74-118`, `:223-282`, `LineCourse.Decide.cs`, `Course/GoOn/DrivenCourse.cs` | facts, waits, holds and from-why | `CoursePolicy? Policy`; `ChoiceReset`, `BudgetDay`, `MovesBound`, `PolicyAsks`; `Transient`, `Failed`, `Quiet`, `Budget`; with no policy, today's answers | e |
| `agents/roles/RoleEditor.tsx`, `RolesView.tsx`, `Roles.tsx:104-160`, `agents/setup/PlaceRoles.tsx` | order only | §6.1 and §6.2; `RolesView` owns the policy's draft and its revisioned save | f |
| `work/actors.ts:27-64`, `work/head/ChoiceChip.tsx`, `locales/*/`, `locales/glossary.json` | the choice chip | `chosen.by`, *Chosen* in *Details*; §5's words in every catalogue and its terms | f |

The bridge's field is `staffPolicy`, since `ROLE_STRUCTURE`'s work already answers a `policy`, its rule (`Bridge/Routes/DriverRoutes.cs:467`).

## 9. Rows, in build order

Every row writes its own line of D226's note and its own translations; a row that fixes a defect writes its fix-log entry.

| Row (lanes; after) | Contract | Proof |
|---|---|---|
| **STAFFPOLICY1a** the record, store and judges, dark (driver) | step 16's three tables; `StaffPolicy` with its shape, tuple keys, digest, presets and `Judge`'s refusals; `StaffJudge.Arrange` and `OnCondition`, pure; `StaffLedger`; nothing reads them at a start | the judge's table both ways, a row per refusal; `staff-policy.json`'s arrangement rows: 3:1 gives 1, 1, 2, 1, 1, 1, 2, 1, ties by order, share, budget, takes, the 30-minute hold; a choice back after a day takes no more than the window allows (Codex); a save of a weight starts the window afresh; a default policy answers *as today* for every condition; an older build's staffing statements over a policy leave it stale (the compatible-builds test shape); the store opens on an older stamp |
| **1b** the doors (driver, modules, service; a) | `SET_ROLE`, `SET_ROLES`, `role policy …` and `role_propose` carry the policy, judged once by `RoleDoor`; `ROLES` answers `staffPolicy`, `policyState` (`default`, `set`, `stale`) and `next`; `Set` and `SetAdded` re-key in the choices' transaction; a home with no store refuses it in words | door tests per refusal code; re-keying across a reorder, an added choice and a removed choice; *Take it* and the check's turn-off leave it stale; a moved policy revision refused with nothing written and the draft kept (Codex); a kill before the commit leaves the policy and the choices as they were (Codex); `SET_ROLES` refused whole on one refusal (Codex); `next` writes nothing and asks no account; the pipe census unchanged (`Person`); `HelpCoverageTests`, `McpToolRegistrationTests`, the tool's description test |
| **1c** the start's walk reads it (driver, service, web type; b) | `StartPlan` arranges for every caller; the ledger's reservation under the roster's gate, given back in `Release`; `role_starts` at the open; `chosen` through the record chain; `actor.chose` gains `rule` and `by`; the opening note says the why; `budget.starts` and `takes` enforced | real ticks on the protocol stand-in: 3:1 over eight new quests lands 6:2; two starts in one look under 1:1 land on different choices; a weighted pick whose accounts all cool falls to the next by weight; a third start past a budget of 2 starts is passed `over-budget`, or waits naming the day's turn; a design quest takes the only choice taking design; a refused open gives its reservation back (Codex); a restart keeps `role_starts` and drops reservations (Codex); `WalkAgentTickTests`, `ModelRefusalTickTests`, `PressModelTickTests`, `LimitResumeTickTests`, `AccountRotationTickTests`, `SignInRefusalTickTests`, `ChatPendingProcessTests` and `LimitGoesElsewhereProcessTests` pass unchanged with no row and with an explicit `first` row |
| **1d** the other walkers (driver, web; c) | `LimitMove.ElsewhereAsync`, `BriefedMove` and `ChatPending.Recovering` read the bound role and its arranged order, which fixes the seeded-role read; the helper, partner and reviewer passes arranged within one agent; `HandOverOrder.Steps` and `nextChoices` take the arranged order; Brainstorm refused; the census holds every order reader | a scoped added role's conversation at its agent's limit walks that role's order, red first; a briefed press on one the same; hand-over fixture rows for an arranged order on both sides; another maker outranks a weight in a second opinion (Codex); a named partner never becomes another agent (Codex); an agent the helper's rule does not list is never walked (Codex); brainstorm voices stay frozen; the census fails on a new direct `ActorChoices.Effective` caller |
| **1e** the conditions in the course (driver; c) | `CourseFacts.Policy` and `LineCourse.Decide` for §4's conditions and actions; `after` for a driven line's episode; the 30-minute hold; `most` and `held.moves-bound`; the not-answering mark; `budget.tokens` from the turn line's `choice`; the new `from_why` codes, notes and log lines; the confirmed end before the next part opens | one real tick per condition on stand-ins with held clocks (`.claude/knowledge/held-clocks.md`): two failed tries move to choice 2 after 3 minutes, briefed, while a conversation's episode still takes five; two fatal ends move to choice 2 and the strikes still park at three; a headless quiet end carries on with choice 2, and no quiet run is ended by a policy; `wait` holds for choice 1's reset while choice 2 is ready, and with `upTo 30` moves at 30; a fourth move holds; a day's reported tokens past the budget pass the choice, and an unreported turn does too; each existing class passes unchanged with no policy. Codex's cases: a wire failure against the same text as an answer, only the wire a condition; a fatal unknown stays `failed`; a busy or unknown quiet session moves nothing; a stop not confirmed holds; the moved words keep their ids, asserted with the brief and not only the agent; a kill at each move phase recovers by the move record |
| **1f** the page (web; b, c) | §6.1 to §6.3 and §5's words; the ⓘ by rule; the preview from `ROLES.next`; the stale and no-store states | page tests in en and zh; stories of the editor, the fold and the head at the widths of 1546 and 680 px windows, measured, both themes; keyboard and IME through weights and limits; a refused save keeps the draft; `i18n:check` across every catalogue, `names:check -- --strict`, the web's rules |
| **1g** Ask Daoris's room (driver, service; b) | the room's facts gain each role's policy and its 7-day moves; the card's before and after; the room's paragraph | the room's goldens; a stale policy revision refused at *Apply*; under full access the verb meets the window's card (D205 point 9); `HelpCoverageTests`' policy row |
| **1h** filed behind measurements, not built | *by cost*: the choice whose accounts report the most allowance left first, comparable only where both makers report windows, or the person's own cost rank (Codex); *by measured success*: once records give a comparable outcome per choice and work, its round starting from Codex's `(accepted + 1) / (decided + 2)` over 28 days after 20 decided outcomes per choice; the budget's mid-session move at a turn's end, through the curve move's seam (`Course/Chat/CurveMove.cs`) | each needs its measurement and a D191 round |
| **1i** the installed proof (the owner's; all) | the owner's press sets Build to a spread and a move on the install | the window at 1546 and 680 px, en and zh, shows the chip, *Chosen* and the use line; one real move to another choice observed with its terminal reading; a policy hold and its press; it spends accounts, so it is the owner's call |

## 10. Rejected

- **Weights inside the choice objects, and Codex's single `staffing` value in the role set:** both of today's writers drop unknown
  fields (`Settings/ActorKey.cs:143-165`, `Roles/RoleStaffing.cs:701-720`), so a weight is lost at the next write.
- **Codex's older-build guard and a stamp raise:** tables an older build cannot see need neither; it runs in order and leaves the policy stale.
- **A random pick** (not predictable, not testable); **a time-window spread** (it starves a choice over quiet nights); **round-robin
  as a rule of its own** (it is equal weights); **a price-sorted *cheapest first*** (no price, D98; D205's rejection stands).
- **Codex's `maxDailyShare` over a role-wide allowance** (`share` over the window has its denominator) and **its failed-open bound**
  (the in-start moves are once each, and the strikes bound failures).
- **A logical run bound to its policy snapshot (Codex §8):** the order is read at each decision today; a save reaches the line's next
  decision, and a running part keeps its choice.
- **Per-account weights or a choice naming an account** (D205 assigns none); **layering a place's policy over another's** (D208's one
  effective record per place); **a model choosing the choice, or an *Auto* pool** (D205).
- **A move mid-turn, a replay, moving a pick or a handed agent, a policy ending a quiet run** (D205 point 5 and §4.3, D200); **a new
  *What needs you* kind or a notification per move** (D205 point 4); **the word *switching*** (D197's account switch).
- **The budget's mid-session hand-over** and **measured success before its measurement:** filed (row h).
- **A separate reads round before building:** both makers' reads run beside the first rows and are settled on main.

## 11. Settled after Codex's read

Codex's read (`docs/archive/2026-11-02-staff-policy-reads.md`) is taken whole; these amend §2 to §9, and each row carries its part.

1. **Older builds** (row a): the store step that adds the policy raises the writer stamp, so an older Daoris on the home reads the
   store and writes nothing, and starts no choice past a policy it cannot read; a write that leaves the choices as they were keeps
   the policy valid (only a change to them makes it stale). The rejection of the stamp raise in §10 is withdrawn.
2. **Publication is atomic** (row b): the role, its staffing and its policy are judged at one revision and published in one
   transaction (today `RoleDoor.Apply` commits the staffing before the catalogue, `Roles/RoleDoor.cs:52-69`, and the catalogue may
   refuse after); the crash proof crosses that boundary.
3. **The terminal's `role policy`** (row b) joins `DriverChoicesCommand.Changes` and its route census, reaching `SET_ROLE` with the
   person's authority, from a session's terminal too.
4. **A hand-over starts the choice approved** (row d): the offered (agent, model, effort) travels through the approval, the durable
   request and the start, revalidated, never re-picked (today `HandOverRequest` carries neither model nor effort,
   `Course/Domain/HandOverRecords.cs:31`); a move between one agent's models, which today's offers exclude, is its own defined press.
5. **The move count is durable** (row e): a count kept by the line's identity covers in-start model and sign-in replacements (which
   write no part link, `Loop/Driver.cs:1485`, `:4404`), survives a restart, and is tested with mixed refusals and condition moves
   against `most`.
6. **Nothing is offered before it is enforced** (rows b–f): the doors refuse a field whose enforcement has not landed (a capability
   list the driver answers), and the full editor follows rows d and e; the reviewer's maker-partitioned walk keeps its fence.
7. **A policy's hold is typed and answerable** (rows e–f): `ask` and `held.moves-bound` extend the session waits' union and the
   attention read (`Modules/Loop/NeedsAPerson.cs`, `work/waits/waits.ts`) with their own answer; one approval at the bound releases
   one move, never an unbounded renewal.
8. **Smaller** (rows a, e, f): the judge stays pure and the ledger reserves; the editor shows the sign-in and model-refusal actions
   the table allows; `TransientRetry`'s bound becomes per run (both ACP retry paths), a driven and a conversation episode tested side by
   side; a budget's attribution goes through the turn's binding (`Agents/UsageTurns.cs`), with last-slot, reorder and continuation
   cases.
9. **The weighted pick keeps a balance, not a window** (row c, amending §3.2; found building row a). A deficit over a sliding window
   replays the window: the start that drops out leaves its choice behind by one, so that choice is taken again, and a catch-up's
   block of ten repeats in every later window. Each entry above weight 0 holds a balance: at each new start every entry gains its
   weight and the entry taken loses `W`; the highest balance first, ties by the stored order, entries over their share after those
   under it. Each balance is held within `−W` and `2W` after every step, so a choice back after a day takes a few extra starts, never
   a window's worth, and one that ran alone pays back as few. The balances are replayed from `role_starts` since `since`, in order,
   handed to the judge as the sequence taken, so it stays pure, no table is added and a restart keeps them; a row naming a choice
   the policy no longer holds is skipped. From zero 3:1 still goes 1, 1, 2, 1, repeating. The window of 20 remains for `share`.
