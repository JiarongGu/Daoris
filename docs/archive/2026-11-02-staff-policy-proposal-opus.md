# STAFFPOLICY1 — the Opus maker's proposal

Status: sealed proposal, 2026-11-02, from `docs/archive/2026-11-02-staff-policy-brief.md`; kept as it came.

# STAFFPOLICY1: staffing as a policy (sealed proposal)

Maker: Claude (Opus). Written read-only on 2026-11-02, against main at `7568ecc89`. I read no `local/` folder and no other proposal. Evidence is given as repository-relative `file:line`.

## 0. The position, in short

1. **A policy belongs to the role, not to its choices.** It lives in a new store table that an older build cannot see. It is bound to the staffing it was set for by a digest. When anything outside Daoris's own doors changes the choices, the policy reads *stale* and the role runs in order. Weights never go into the choice objects, because both writers of choices drop unknown fields (`src/Daoris.Desktop/Daoris.Desktop.Driver/Settings/ActorKey.cs:144-165`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleStaffing.cs:701-720`).
2. **Two selection rules are built: `order` and `weight`.**
   - `order` is today's walk. It is the default, and every role without a policy reads as it.
   - `weight` is a deterministic smooth weighted pick over the role's last 20 new starts. It has no randomness, ties go by order, and every pick can say why in counts.
   - Round-robin is `weight` with equal weights (the *Spread evenly* preset).
   - *By cost* is not a rule: Daoris holds no price (D98; D205's design rejects cheapest-first, `docs/archive/2026-10-23-actors-design.md:1274-1275`). It is answered by a per-choice daily budget, and by a measured *most allowance left* rule filed behind a measurement.
   - *By measured success* is filed behind a measurement, not built.
3. **One judge chooses which choice: `StaffJudge.Arrange`.** It is called inside `StartPlan`, which is already the one walk for quests, conversations and reviewers (`Roles/StartPlan.cs:240-308`). The conditions become facts of the existing one course decision (`Course/Domain/LineCourse.Decide.cs:48-60`, D212). No new loop and no new decider.
4. **Every move to another choice is a new part handed Daoris's brief, at a part's end, never mid-turn** (D189 point 2). A running session still never moves (D205 point 5). The only move taken from a running session is opt-in: `hand-over` for a daily budget, at the turn's end, as the curve's account move already works (`Agents/Domain/SwitchDecision.cs:65-68`).
5. **Each condition has an action that reads "as today"** (§4's table). Only the person's press, through `SET_ROLE` (`Settings/DriverChoicesCommand.cs:98`), changes what a line does.
6. **It is said in four places, and only when the policy (not availability) decided:**
   - a new machine-local `chosen` on the record;
   - the opening note's actor line, with the why;
   - the head's chip (*choice 2 · by weight*, *choice 2 · after a limit*);
   - the role's use line, which counts moves by condition.

## 1. The ground: what today's code does, and what binds the design

**Roles and places.**
- A role is a record with a scope: machine, workspace or repository. `RoleFor` takes the most specific scope's role (`Roles/RoleCatalog.cs:167-191`, ranks at `Roles/RoleRecords.cs:43-51`).
- Its staffing is a separate row of `role_staffing`, keyed by role id (`Roles/RoleStaffing.cs:185-205`).
- Inheritance by place is therefore inheritance of a whole role. That matches D208's rule that a repository's own record wins over its workspace's (`docs/decisions/D208.md:309-310`).

**The walk.**
- `StartPlan.ResolveAsync` walks choice by choice. Each choice's accounts go through D197's walk (`HarnessRoster.SelectAsync`, `Agents/HarnessRoster.cs:906-913`) before the next choice is tried.
- An order of one entry takes a fast path with nothing said (`Roles/StartPlan.cs:245-249`).
- The pass codes are a closed set (`Roles/StartPlan.cs:35`).
- The wait sentence is `{role} waits: none of its choices can run now` (`Roles/StartPlan.cs:271`).
- Conversations walk through the same plan (`Loop/Chat/ChatRunner.cs:1028-1059`).

**The walk at a limit (WALKAGENT1).**
- A limited driven line walks Build's order only where the role lists more than one choice, or another agent (`Loop/Driver.cs:2270-2293`, `:2358-2366`).
- It holds on the earliest reset otherwise (`Loop/Driver.cs:2395-2405`). D206's step 1, the own account's resume, comes first (`Loop/Driver.cs:2337-2349`).
- A pick, a handed agent and a role *as before* wait (`docs/decisions/D206.md:37-38`).

**In-start moves.**
- LIMITPICK2 moves the same start to another account once on a sign-in refusal (`Loop/Driver.cs:1477-1494`).
- MODELREFUSE1 moves it once to the walk's next answer on a refused model (`Loop/Driver.cs:1496-1512`; `docs/task-archive.md:22513-22517`).
- Both hold the quest's reservation across the move.

**The hand-over offer.**
- `HandOverOrder.Steps` takes the role's entries on another tool in order (`Course/Domain/HandOverAsks.cs:141-159`).
- Its page twin is `nextChoices` (`src/Daoris.Web/src/work/start/handOverOffer.ts:140-160`). Both are held to `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/hand-over-order.json`.
- PRESSMODEL1: a quest the person's press handed to another agent asks that agent's own choice (`Loop/Driver.cs:2300-2314`; `docs/FIX-LOG.md:854-884`).

**Failures.**
- D210 reads every failure into Limit, Access, Transient or Fatal (`Agents/AgentErrors.cs:6-19`).
- Transient failures retry in the same conversation after 1, 2, 4, 8 and 16 minutes (`Agents/TransientRetry.cs:20-28`). Past that bound the part ends `ended.transient` (`Loop/Driver.cs:3326-3331`), and the planner holds the quest for the person (`WorkOrders/Domain/Planner.cs:845-879`).
- A Fatal error is a strike; three strikes park the quest (`Settings/DriverConfig.cs:38`).
- A quiet session is marked and brought to the person, never ended, except a dead process and a headless `--quiet-end` (`Events/SessionWatch.cs:63-66`, `:143-147`; D200).

**Gaps the design must close or respect.**
1. **Some walkers bypass the plan.** They read `ActorChoices.Effective` themselves:
   - `Course/Chat/LimitMove.cs:878-882`
   - `Course/Chat/BriefedMove.cs:281`
   - `Course/Chat/ChatPending.cs:206`
   - `Participants/Helpers/HelperChoice.cs:55-61`
   - `Loop/DriverBrainstorms.cs:406-408`

   `LimitMove.ElsewhereAsync` also reads the record's seeded role id (`prior.Actor`), not the role the start was bound to. So a scoped added role's conversation, at its agent's limit, walks the seeded role's order. That is a latent defect the policy would make visible; row d fixes it.
2. **The word *switch* is taken.** D197's account setting *Switch accounts* (切换账户) governs the curve's account move at a turn's end (`src/Daoris.Web/src/locales/en/harness.json:105-110`, `Agents/RotationUse.cs:35`). The policy's word is **move to another choice** (改用其他选择), which is D205's existing zh (`src/Daoris.Web/src/locales/zh/settings.ai.json:60-61`). The glossary already keeps *fallback* out of *choice* (`src/Daoris.Web/src/locales/glossary.json:526`).
3. **The native door reads no limit yet** (TOOL4n, `TASKS.md:105`). On Claude Code's pipe door the `limit` condition is unobservable until that row lands. The policy must not claim it.
4. **The look's record read carries no role or choice.** `SessionStarted` names the agent, account, time and running only (`Service/Domain/QuestViews.cs:117-126`). Weights need their own count source (§3.2).
5. **The editor's words assume order.** *Choice 2 runs only when no account of choice 1 can* (`src/Daoris.Web/src/locales/en/settings.ai.json:58`) and *Its choices run in order* (`:138`) are true only under `order`.

## 2. The policy's shape and its defaults

### 2.1 The record

There is one policy per role id, in the desktop store. It is a new migration step after step 15 (`Store/DesktopStore.cs:512`). It names the stamp step 15 left and does not raise it, since an older build names none of these tables (`.claude/knowledge/compatible-builds.md`).

```sql
CREATE TABLE role_policy (
  role      TEXT PRIMARY KEY,
  policy    TEXT NOT NULL,      -- the JSON below
  staffing  TEXT NOT NULL,      -- sha256 of RoleStaffing.Write(choices) it was set for
  revision  INTEGER NOT NULL,
  at        TEXT NOT NULL);
CREATE TABLE role_policy_revisions (
  role TEXT NOT NULL, revision INTEGER NOT NULL, policy TEXT, staffing TEXT, at TEXT NOT NULL,
  PRIMARY KEY (role, revision));
CREATE TABLE role_starts (            -- the weight's count, one row per new start at its open
  session TEXT PRIMARY KEY, role TEXT NOT NULL, choice TEXT NOT NULL,
  by TEXT NOT NULL, condition TEXT, at TEXT NOT NULL);
```

```jsonc
{
  "rule": "weight",                    // "order" (default) | "weight"
  "choices": [                         // aligned with the role's staffing, checked by the digest
    { "weight": 3 },
    { "weight": 1, "share": 40, "budget": { "starts": 6 }, "takes": ["design"] }
  ],
  "moves": {                           // condition → action; an absent condition is "as today"
    "limit":     { "do": "next" },
    "transient": { "do": "weighted", "after": 2 },
    "failed":    { "do": "next", "after": 2 },
    "quiet":     { "do": "next" },
    "budget":    { "do": "weighted" }
  },
  "most": 3                            // moves to another choice per line per 24 h; absent = none of its own
}
```

Per choice, every field is optional:

| Field | Range | Meaning | Hard or soft |
|---|---|---|---|
| `weight` | integer 0–10, `weight` rule only; at least one above 0 | its aim is `w / Σw` of the role's new starts; 0 means *only when the others cannot run* | — |
| `share` | 1–99 % | at most this share of the role's last 20 new starts. Over it, the choice is walked after every choice under its share | soft: it still runs when nothing else can |
| `budget` | `{starts: n}` or `{tokens: n}` per local day (the machine's zone, as D125 reads a reset) | over it, the choice is passed `over-budget` before any account is asked | hard |
| `takes` | a non-empty subset of the role's `takes` | the works this choice serves. A role taking `build` and `design` can give design to Opus alone | filter |

The digest is the reason weights live outside the choices.
- Daoris's own writes keep the policy in step. `SET_ROLE`, `SET_ACTOR`, `role set` and the terminal re-key it by the choice's (agent, model, effort) under the staffing's lock (`Roles/RoleStaffing.cs:325-338`). A new choice takes weight 1 and no limits.
- Any other writer leaves the digest wrong: an older build, a hand edit taken through *Take it* (`Roles/RoleStaffing.cs:435-442`), or a projection repair. The policy then reads **stale**: the role runs in order, and its row says so.
- So weights are never applied to choices they were not set for.

### 2.2 Defaults and how existing roles read

- **No `role_policy` row** means `order`, no limits, every move as today. This holds for every role today. Nothing is migrated and nothing spends differently.
- **A home whose staffing is not in the store yet**, or a `DAORIS_DRIVER_CONFIG` home, keeps no policy. This mirrors `SetAdded`'s refusal (`Roles/RoleStaffing.cs:358-361`). The editor shows the policy fields held, with the sentence *Policies are kept in this machine's store, which this home does not use yet.*
- **Restore role defaults** and **Clear staffing** delete the policy row. *As before* means order.

### 2.3 Presets

Presets are Daoris's own words, keyed in the catalogues (D222 point 2, `docs/decisions/D222.md:9-11`). A preset only fills the form; the press saves.

| Preset | en / zh | Writes |
|---|---|---|
| `first` | *First that can run* / 第一个能运行的 | `order`, nothing else (today's) |
| `even` | *Spread evenly* / 平均分配 | `weight`, every weight 1, `limit: weighted`, `most: 3` |
| `weighted` | *Spread by weight* / 按权重分配 | `weight`, the person's weights, `limit: weighted`, `most: 3` |

*Cheapest first* is not a preset. Daoris knows no price (D98), and the order already is the person's cost preference: the owner's Sonnet-then-Opus Build (`docs/model-roles.md:10-15`) is *First that can run*. The ⓘ of *First that can run* says *Put the lighter choice first; Daoris knows no prices.* A rule that sorts by measured allowance left is filed (row i).

### 2.4 Inheritance by place

There is one role per place and one policy per role. A scoped role (`workspace:x`, `repository:y`) has its own policy row, defaulting to `order`. It never layers over the machine role's policy.

The place's Roles fold says which role applies there: its own, or the workspace's or machine's it inherits. It offers *Give this place its own role…*, which copies the choices and the policy.

Layering is rejected: two records would decide one start, and a change at the machine would silently re-key a place's weights. D208 rules one effective record per place, a repository's own winning even while dormant.

### 2.5 The policy's judge

`StaffPolicy.Judge(role, choices, policy)` is called by every door before anything is written, as `RoleDoor.Judge` is (`Roles/RoleDoor.cs:131-135`). It adds one code, `policy`, to the closed set (`Roles/RoleCatalog.cs:255-256`). It refuses:
- a weight outside 0–10, or every weight 0 under `weight`;
- a share outside 1–99, or a budget that is not a positive integer;
- `takes` not a subset of the role's works, or a work of the role that no choice takes;
- an action a condition does not allow (§4);
- `after` outside its range, or `most` outside 1–8;
- any policy on a role that takes `brainstorm`, whose entries are voices (D205 round 2);
- a stale digest on save (*the choices changed since you read them*).

## 3. The judge: rule and inputs

### 3.1 `StaffJudge.Arrange`: pure, values in

The exemplar is the curve's turn-end judge, `SwitchDecision.Decide` (`Agents/Domain/SwitchDecision.cs:51-68`). Facts come in as values, and nothing is read inside.

```csharp
StaffArrangement Arrange(StaffPolicy policy, string work, IReadOnlyList<ActorEffective> order, StaffFacts facts)
// facts: the role's last 20 new starts by choice (role_starts) + this look's reservations; today's starts and
//        tokens by choice; agents marked not-answering; choices this line moved off within the hold; now.
// answer: the entries in walk order, each with By (first|weight|share|budget|work) and the sentence that says it.
```

1. **Work.** Entries whose `takes` excludes the start's work leave the order.
2. **Budget.** Entries over today's budget are passed `over-budget`. A new code joins `StartPasses.Codes` (`Roles/StartPlan.cs:35`) and `BuildPass` (`Roles/BuildWalk.cs:24-34`).
3. **Health and the hold** (only where the role's `transient` move is `next` or `weighted`). An entry whose agent is marked `not-answering` is passed. An entry this line moved off within 30 minutes is passed `moved-off`. Both apply only while another entry remains. The 30 minutes is `SwitchDecision.Hold`'s precedent.
4. **Rule.**
   - `order`: the stored order, as today.
   - `weight`: for each entry with weight above 0, `deficit = w/W × (N+1) − n`, where `n` is its starts among the window's `N`. Highest deficit first; ties by stored order. Entries over their share go after those under it. Weight-0 entries come last, in order.
5. **The walk is unchanged.** `StartPlan.LoopAsync` walks the arranged entries choice by choice, each choice's accounts by D197 (`Roles/StartPlan.cs:283-308`). Account selection, cool-offs, curves and the cap are untouched, and no account is ever named (D205, D197 points 1, 2 and 8: `docs/decisions/D197.md:21-29`, `:66-67`).

**Worked case.** Weights 3:1 from an empty window go 1, 1, 2, 1, 1, 1, 2, 1. That is 6:2 over eight starts. This sequence is a fixture row.
- A window of 20 bounds the catch-up: a choice that could not run for a day does not then take every start.
- A count window, not a time window, keeps the spread independent of the clock and of quiet nights.

### 3.2 The count, and two starts in one look

`role_starts` gets a row at a new start's open: its role, the choice taken, `by`, and the condition where it moved. Continuations write none, so they never move the spread. The count lives in the store, so it survives restarts and needs no service read.

Starts in one look run at once (PARALLEL1c). So `Arrange` **reserves its top pick under its own lock** as it answers. The settled selection moves the reservation to the entry actually taken, or gives it back. That happens in the same `Release` that gives back the account count (`Agents/HarnessRoster.cs:1982`; D205 round 4's one count per start). Two starts under 1:1 in one look land on different choices.

### 3.3 Where it is decided

| Moment | Today's seam | With the policy |
|---|---|---|
| A new quest's first start | `WalkBuildPassesAsync` → `BuildPlan` (`Loop/Driver.cs:796-799`, `:891-914`) | `BuildPlan` hands the role's policy and facts. `Arrange` runs before `LoopAsync` |
| A carry-on at a limit | `limitWalks` (`Loop/Driver.cs:2276-2293`) | the `limit` move decides: walk (`next` or `weighted`), `wait`, `ask` |
| An in-start move (refused model, sign-in) | `ClaimedAsync` (`Loop/Driver.cs:1477-1512`) | unchanged mechanism. The re-run's walk is arranged, and counts toward `most` |
| A new conversation | `ChatRunner.WalkAsync` through `StartPlan` (`Loop/Chat/ChatRunner.cs:1028-1059`) | arranged as above |
| A conversation at its agent's limit (LIMITGOON2) | `LimitMove.ElsewhereAsync` (`Course/Chat/LimitMove.cs:873-899`) | reads the **bound** role and its policy, and arranges. `wait` makes it wait, as a pick does |
| A hand-over offer, and an agent's ask under `on` | `HandOverOrder.Steps` (`Course/Domain/HandOverAsks.cs:141-159`) | names the policy's next. The twin reads the order the driver answers as `arranged` |
| A helper's, a partner's or a reviewer's pass | `HelperChoice` (`Participants/Helpers/HelperChoice.cs:55-61`), the reviewer loop over `StartPlan.LoopAsync` | the rule and D155's maker passes still decide who. Weights arrange only within one agent's alternatives (several models) |
| A brainstorm's pass | `DriverBrainstorms.cs:398-408` | no policy: voices run together and frozen (D205 round 2). The judge refuses one |
| A continuation (resume, go-on, carry-on) | `StartPlan.ContinuedOn` (`Roles/StartPlan.cs:351-361`) | keeps its record's asked choice. Only a condition moves it |

**Predictable.**
- The policy and facts are read once per start, as the order already is (D205 Opus N3).
- `Arrange` and the condition judge are pure.
- `ROLES` answers each role's `next`: a probe arrangement with no reservation.
- The role row says *Next new start: choice 1 (by weight: 3 of the last 4 were choice 1, aim 3 in 4)*.

**Testable.**
- One table fixture, `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/staff-policy.json`, holds policy and facts in, arrangement and action out.
- A census holds every reader of a role's order either to `RoleCatalog.Arranged` or to a named exemption (Brainstorm). This is D205 row b's "required first parameter" move.

## 4. The conditions and their actions

The actions:
- `next`: walk the stored order from its top, the met choice passed. This is WALKAGENT1's walk.
- `weighted`: the same walk, arranged by weight.
- `wait`: hold for the met choice's earliest moment back: its accounts' reset, the budget's day, or the health mark's end. An optional `upTo` (minutes), after which the next action applies.
- `ask`: hold, offering *Go on now with {next}*, which is D206 point 7's press.
- `hold`: hold with today's doors (*Try again*, words).
- `retry`: the same choice again. Today's carry-on.
- `hand-over`: at a running session's turn end, a new part on the next choice, briefed.

Every move to another choice is a new part handed D189's brief (`docs/decisions/D189.md:20-29`).

| Condition (code) | How Daoris observes it today | Actions allowed | As today (the default) |
|---|---|---|---|
| `limit`: every account of the choice is limited or cooling, at a start or at a part's end | the conclusion's `AccountLimited` and `SessionEnded.Limit` (`Loop/Driver.cs:3359-3362`); `cooling` passes (`Roles/StartPlan.cs:25-26`). Codex's out-of-credits sentence too (`docs/task-archive.md:22259-22263`). **Not the native door until TOOL4n** (`TASKS.md:105`) | `next`, `weighted`, `wait` (`upTo` 5–600), `ask` | `next` where the role lists more than one choice or another agent, else `wait`. D206 step 1 (own resume) first, always |
| `signed-out`: a door's access refusal | `AccountSignedOut` (`Loop/Driver.cs:3335-3343`); LIMITPICK2's in-start account move | `next`, `weighted`, `ask`, `hold` | the in-start account move (fixed), then `next` at the walk |
| `model-refused` | `ended.NotOffered` and the pipe's `ModelRefused` (`Loop/Driver.cs:3321-3357`) | `next`, `weighted`, `ask`, `hold` | `next` within the same start (MODELREFUSE1) |
| `transient`: the service does not answer | D210's episode (`Agents/TransientRetry.cs:20-28`). Past the bound, `Observation.Transient` | `next`, `weighted`, `ask`, `hold`; `after` 1–5 tries | `hold` after 5 tries (31 min): held for the person (`WorkOrders/Domain/Planner.cs:874-879`) |
| `failed`: a fatal error again and again | D210 Fatal, a strike; `Strikes = 3` parks (`Settings/DriverConfig.cs:38`) | `next`, `weighted`, `ask`, `hold`, `retry`; `after` 1 to strikes−1 on the same choice in this line | `retry` until the strikes park. Strikes keep counting across choices, so the park still bounds a spin |
| `quiet`: past the inactivity window | `SessionWatch`: marked at 10 min, brought to the person at 3×; ended only headless (`Events/SessionWatch.cs:80-90`, `:143-147`) | `next`, `weighted`, `retry`, **only for the carry-on after the run ended** (the person's *Stop* on the quiet row, or a headless quiet end) | `retry`. D200 stands: the policy never ends a quiet run |
| `budget`: the choice's daily budget is reached | new: role starts per day (`role_starts`), tokens per day (the usage turn line gains `choice` beside `actor`, `Agents/UsageHistory.cs:637`) | at a start: `next`, `weighted`, `wait` (to the day's turn), `ask`. Mid-session: `hand-over` at the turn's end, or nothing | at a start `next` (a pass like any). Mid-session nothing: running work never moves |

**Fixed: never configurable, said as such in the editor.**
- `unavailable`, `no-posture`, `no-connector`, `not-admitted`, `not-listed` are passed before any account (`Loop/Driver.cs:896-902`).
- A pick and a handed agent never walk.
- A role *as before* waits.
- The cap waits for a slot (`Loop/Driver.cs:1243`).
- A turn that ended in the agent's own words (a content refusal) is never a condition (D205 §4.3, `docs/archive/2026-10-23-actors-design.md:767-772`).
- An uncertain send is never replayed: the brief carries it, never the prompt again.
- Nothing moves mid-turn.

**Health.** When a run's transient episode ends past its bound, or by the policy's `after`, the agent is marked `not-answering` for 15 minutes, or until any prompt on that agent answers. The mark is in memory, never an account's cool-off (D197 point 8), and only roles that move on `transient` read it. One line's minutes of retries then spare the next start the same.

**The bound.** `most` counts a line's moves to another choice in 24 hours, from the parts' new `from_why` codes. Moves between accounts of one choice never count.
- When it is reached, the next condition's action is `ask`, held as `held.moves-bound`.
- *First that can run* has no `most` of its own: today's bounds stand (D206's one attempt per cool-off, the in-start move once each, the strikes).
- The other presets write 3.

**How a move is said.**

| Where | What |
|---|---|
| The run | the new part's opening note `opening.moved-choice`: *Moved here from choice 1, Claude Code · opus · high: its service did not answer after 2 tries (3 minutes). Build's policy moves on that; 1 of 3 moves today.* (agent and model, no account, as `SayMovedOffModel` does, `Roles/ActorOpenings.cs:43-49`). The old part's `moved.on` names the choice it went to |
| The session's head | the chip *choice 2 · after a limit*, *· service didn't answer*, *· after 2 failures*, *· after a quiet stop*, *· over its budget*, *· by weight*. *Details* gains *Chosen*, the sentence |
| The role | the use line counts moves by condition next to *fell to a later choice* (`src/Daoris.Web/src/agents/roles/Roles.tsx:146-160`; `Roles/RoleUse.cs:51-58` reads a new `by` on `actor.chose`) |
| The record | machine-local `chosen` `{ rule, by, condition?, from? }`. The new `from_why` codes are `transient`, `failed`, `quiet` and `budget` (`Course/Domain/CourseAnswers.cs:223-251`). Machine log: `actor.moved` {role, line, from, to, condition, action, n}, and `actor.chose` gains `rule` and `by` |

The why is said exactly when the policy changed the outcome, so a role on *First that can run* says what it says today. `ActorLine` today speaks only for a later choice with passes (`Roles/ActorOpenings.cs:57-63`). It gains *by weight*, *its share*, *the only choice taking design*, and *moved*.

## 5. The person's surfaces

### 5.1 The role editor (SETUPUX1l's `RoleEditor`)

The editor gains one control and two folds. The choice rows stay `FormGrid` rows (`src/Daoris.Web/src/agents/roles/RoleEditor.tsx:92-152`). `Draft` gains `weight`, `share`, `budget` and `takes`, and `Editing` gains `policy` (`:12-18`).

**At 1546 px** (Agents → Roles → Working roles, the main area about 80 rem):

```
Build      build, design · this machine · Claude Code · sonnet · high +1 · spread 3:1 · on a limit: next by weight      Edit
           14 starts this week · choice 1 ×10, choice 2 ×4 · moved 2 (limit ×1, service ×1)
┌ Build: its choices ───────────────────────────────────────────────────────────────────────────────────────────────┐
│ How it chooses   (•) First that can run   ( ) Spread evenly   ( ) Spread by weight                          ⓘ       │
│   #  Agent             Model          Effort        Weight   Aim     Limits                                         │
│   1  [Claude Code ▾]   [sonnet ▾]     [high ▾]      [ 3 ]    3 in 4  ▸ none                                ↑ ↓ ✕     │
│   2  [Claude Code ▾]   [opus ▾]       [high ▾]      [ 1 ]    1 in 4  ▸ design and build · 6 starts a day   ↑ ↓ ✕     │
│   + Add a choice (at most 4)                                                                                        │
│ ▾ When a choice cannot go on                                                                                        │
│   It reaches its limit            [Go to the next by weight ▾]                         as today: next choice        │
│   Its service does not answer     [Go to the next by weight ▾]  after [2 ▾] tries      as today: hold for you      │
│   It fails again and again        [Try it again ▾]                                      as today                    │
│   It went quiet and was stopped   [Go to the next choice ▾]                                                        │
│   It reaches its daily budget     [Go to the next by weight ▾]                                                     │
│   At most [3 ▾] moves to another choice a day for one quest or conversation, then it waits for you.               │
│   Always: a refused model or sign-in goes to the next choice; your pick and a handed agent wait; running work      │
│   moves only at a turn's end, handed Daoris's brief.                                                               │
│ Next new start: choice 1 (by weight: 3 of the last 4 were choice 1).                                               │
│ ▸ Edit role                                                                                                         │
│ Saving puts this to work at once: new work takes these choices; running work keeps its own.     [Save] [Cancel]    │
└────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- The *Weight* and *Aim* columns show only under a weight rule. *Limits ▸* opens share, budget (starts or tokens) and the works it takes.
- The ⓘ's prose follows the rule. Under `order` it keeps today's sentences (`src/Daoris.Web/src/agents/roles/RoleEditor.tsx:76-80`). Under weight it says *Choices take new work by their weights; a choice that cannot run passes to the next by weight.*

**At 680 px** (the list closed to its strip). Rows stack by `FormField when="stacked"`; the moves fold becomes label-over-select:

```
Build: its choices
How it chooses
[ First that can run ▾ ]                         ⓘ
Choice 1                                   ↑ ↓ ✕
  Agent   [Claude Code ▾]
  Model   [sonnet ▾]      Effort [high ▾]
  Weight  [3]  aim 3 in 4
  ▸ Limits: none
Choice 2                                   ↑ ↓ ✕
  …  Weight [1] aim 1 in 4
  ▸ Limits: design and build · 6 starts a day
+ Add a choice
▾ When a choice cannot go on
  It reaches its limit
  [Go to the next by weight ▾]
  Its service does not answer
  [Go to the next by weight ▾] after [2 ▾] tries
  …
  At most [3 ▾] moves a day, then it waits for you
Next new start: choice 1 (by weight)
[Save] [Cancel]
```

### 5.2 The place's Roles fold

`roleSummary` and `actorSummary` (`src/Daoris.Web/src/agents/roles/Roles.tsx:104-140`) gain the policy's fold words:

```
▾ Roles here                                                     (a repository's or workspace's Setup)
  Build   this repository's own · Claude Code · opus · high +1 · first that can run          Edit
  Chat    this machine's · Claude Code · account default · spread evenly · at most 3 moves     Give this place its own…
  ⚠ Intake   its policy was set for other choices; it runs them in order until you save it again   Edit
```

### 5.3 The run's head

The chip extends `choiceChip` (`src/Daoris.Web/src/work/actors.ts:27-31`) by `chosen.by`. It stays within the `status` budget (16/5) by keeping the condition in the ⓘ at narrow widths.

```
1546:  #412 Build the policy store · daoris · Claude Code · opus · high   [choice 2 · after a limit ⓘ]   Working
 680:  #412 Build the policy store          [choice 2 ⓘ]
Details ▾  Role    Build (this machine)
           Choice  2: Claude Code · opus · high
           Chosen  Moved here from choice 1 at 14:05: every account of choice 1 reached its limit (back at 16:40).
                   Build's policy goes to the next by weight on a limit; 1 of 3 moves today.
           Asked model · effort / Acknowledged / Model that ran   (unchanged, D205 §9.3: never judged against each other)
```

### 5.4 The terminal twin

The twin lives in `RoleVerb` (`Roles/RoleVerbs.cs:12-17`), under the same judge and the same `SET_ROLE` door.

```
$ daoris driver role build
build  Build · build, design · this machine · revision 7 · policy revision 3
  how it chooses   spread by weight 3:1
  1  claude-code sonnet high   weight 3  aim 3 in 4
  2  claude-code opus high     weight 1  aim 1 in 4  budget 6 starts a day  takes design, build
  moves            limit=weighted  transient=weighted after 2  failed=retry (as today)  quiet=next  budget=weighted
  most             3 a day
  next new start   choice 1: by weight, 3 of the last 4 were choice 1
  last 7 days      14 starts · choice 1 ×10, choice 2 ×4 · moved: limit ×1, transient ×1
$ daoris driver role policy build --rule weight --weight 1=3 --weight 2=1 --budget 2=6/day --takes 2=design,build \
      --on limit=weighted --on transient=weighted:2 --on quiet=next --most 3
$ daoris driver role policy build --preset first        # back to today's order
```

### 5.5 Ask Daoris

`role_propose` gains `policy`: a preset name, or the object above (`src/Daoris.Service/Daoris.Service.Mcp/HelpRoleTool.cs:19-32`; `Help/Proposals/HelpRoleProposals.cs:5-20`). It is judged by the same judge and carries the revision it read. The card says, in the fold's words:

*Build: First that can run → Spread by weight (3:1); on a limit: next by weight; at most 3 moves a day. This can spend differently.* [Apply] [Not now]

It is applied only by the person's press, in both modes (D205 point 9, `docs/decisions/D205.md:58-61`). No `settings_*` grant reaches a policy. The room's facts gain each role's policy and its moves over 7 days, so *why did this run on Opus?* is answered from records.

### 5.6 New words (catalogue keys, en and zh, glossary-checked)

| en | zh |
|---|---|
| How it chooses | 怎样选择 |
| First that can run / Spread evenly / Spread by weight | 第一个能运行的 / 平均分配 / 按权重分配 |
| weight · aim {{n}} in {{m}} | 权重 · 目标 {{m}} 次中 {{n}} 次 |
| share of starts · daily budget | 启动占比 · 每日预算 |
| When a choice cannot go on | 选择无法继续时 |
| Go to the next choice · Go to the next by weight · Wait until it is ready again · Ask me · Hold for me · Try it again | 改用下一个选择 · 按权重改用下一个 · 等它重新可用 · 问我 · 暂停等我 · 再试一次 |
| choice {{n}} · by weight / · after a limit | 选择 {{n}} · 按权重 / · 额度用尽后 |

Glossary notes:
- 改用 (move to another choice) avoids 切换, which is D197's account switch.
- *Ready again* stays 重新可用, never 恢复 (D206's LIMITRESUME1d note).
- *limit* stays 额度用尽 (`src/Daoris.Web/src/locales/glossary.json:712`).
- *weight* (权重), *share* (占比) and *daily budget* (每日预算) enter the glossary.

## 6. What changes in the decisions, what stays, and how older records read

- **D205.**
  - Point 4's walk becomes the policy's arrangement; `order` reads exactly as written.
  - Point 5 stands: no running session moves, and a move is a new part, said.
  - §4.3 is narrowed. A part a policy-named error class ended (transient past its bound or `after`, fatal after `after` strikes, a quiet stop) may be followed by a new part on another choice, handed the brief. Never a replay; never for a turn that ended in the agent's own words.
  - §7's "left out" admits two items: the *wait N minutes* (`wait.upTo`) and per-condition actions (`docs/archive/2026-10-23-actors-design.md:1041-1044`).
  - §12's rejections of a router, an *Auto* pool and cheapest-first routing stand.
  - The editor sentences that assume order become rule-dependent.
- **D206.** Point 6: the limit's walk takes the `limit` move. Step 1 (own resume) and the fixed waits of a pick, a handed agent and *as before* stand (`docs/decisions/D206.md:25-38`).
- **D197.** Unchanged inside a choice: the order and goal walk, cool-offs, curves, the set and the cap. The policy reads readings and cool-offs and writes none. The `not-answering` mark is an agent's, never an account's.
- **D210.** Its header's *never … a walk* is narrowed: after its bound, or a role's `after`, a line may move to another choice as a new part. The retries in the same conversation are unchanged up to that point.
- **D200.** Unchanged: `quiet` acts only on a run already ended.
- **D163 and D189.**
  - The offer's *next choice* is the policy's next.
  - Agents' asks are not widened. Daoris's own offer is still taken only by the person's press (`docs/decisions/D205.md:120`).
  - The one new automatic move from a running session is `budget → hand-over` at a turn's end. It is the person's standing choice, saved by their press under the editor's words, as the order itself already moves lines at a limit.
  - Every move is D189 point 3's briefed new part.
- **D212.** `CourseDecision.Decide` stays the one decision. The policy is a fact (`CourseFacts.Policy`).

**How older records and roles read.**
- **A record with no `chosen`** reads as today. Its chip comes from `choice`.
- **A new `from_why`, read by an older build**, falls to *none* (`Course/Domain/CourseAnswers.cs:270-282`). Its *moved on* line names no reason.
- **An older build** sees none of the three tables. It runs every role in order, and its writes to the staffing make the policy stale, never misapplied. The footnote becomes: *An older Daoris runs these choices in order, without weights or moves.*

## 7. What changes in today's code, file by file

All paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless named otherwise.

| File | Today | Change |
|---|---|---|
| `Store/DesktopStore.cs:512` | step 15 is the last step | step 16: `role_policy`, `role_policy_revisions`, `role_starts`, at the stamp step 15 left |
| `Roles/StaffPolicy.cs` (new) | — | the record, its JSON, the digest, `Judge`, presets |
| `Roles/StaffJudge.cs` (new) | — | `Arrange` and `OnCondition`, pure, after `SwitchDecision` |
| `Roles/StaffLedger.cs` (new) | — | `role_starts` and reservations under one lock; the day's starts and tokens; the health mark |
| `Roles/RoleStaffing.cs:325-375` | `Set` / `SetAdded` write the choices | re-key or write the policy in the same lock and transaction |
| `Roles/RoleCatalog.cs:428-444`, `:255-256` | `Order` returns `Effective` | `Arranged(work, place, config, facts)` beside it; the `policy` code |
| `Roles/RoleDoor.cs:31-73`, `:147-175` | rows, apply and read carry choices | also `policy` and `policyState` (`default`, `set`, `stale`); `Apply(…, policy)` |
| `Roles/StartPlan.cs:153-166`, `:240-273`, `:35` | order read once, walked in order | takes the policy and facts, arranges before `LoopAsync`. The fast path stays unless the one choice has a budget. Pass codes gain `over-budget`, `moved-off`, `not-answering` |
| `Roles/BuildWalk.cs:24-34` | `BuildPass` words | the new codes' `Said` |
| `Roles/ActorOpenings.cs:57-70` | says only a later choice with passes | says weight, share, work and moves; `opening.moved-choice` |
| `Roles/RoleUse.cs:23-58` | counts `actor.chose` | reads `by`; moves per condition |
| `Roles/RoleVerbs.cs:12-17` | `role set …` | `role policy …`; `role <id>` shows the policy and its next start |
| `Settings/ActorKey.cs` | the choices' shape | **unchanged**, on purpose (§2.1) |
| `Loop/Driver.cs:891-914` (`BuildPlan`) | plan without policy | hands the policy and facts |
| `Loop/Driver.cs:2276-2293`, `:2358-2366`, `:2395-2410` | limit walk or hold | the `limit` move: walk, `wait` (`held.policy-waits`), or `ask` |
| `Loop/Driver.cs:3321-3362` | conclusions | a transient end by the role's `after`; `role_starts` at the open |
| `Agents/TransientRetry.cs:24-28` | bound 5 | the run's bound is the role's `after` where it moves on transient |
| `Agents/UsageHistory.cs:637` | the turn line has `actor` | adds `choice` |
| `Agents/HarnessRoster.cs:1982` | `Release` | also releases the ledger's reservation |
| `WorkOrders/Domain/Planner.cs:845-879` | `TransientHolding` holds | where the course answers a move, plans the carry-on |
| `Course/Domain/CourseFacts.cs:264-267` | facts | `CoursePolicy? Policy`: the met choice, the action, moves in 24 h |
| `Course/Domain/CourseAnswers.cs:74-118`, `:223-251` | wait, hold and from-why enums | `ChoiceReset`, `BudgetDay`; `MovesBound`, `PolicyAsks`; `Transient`, `Failed`, `Quiet`, `Budget` |
| `Course/Domain/LineCourse.Decide.cs:409-429` | `PastBound`, `DrivenWhy` | read `facts.Policy`. With none, today's answers |
| `Course/GoOn/DrivenCourse.cs:73-103`, `:124-144` | builds facts | adds the policy fact |
| `Course/Chat/LimitMove.cs:873-899`, `BriefedMove.cs:281`, `ChatPending.cs:206` | `Effective` by the seeded id | the bound role's `Arranged`; `wait` as a pick's wait |
| `Course/Domain/HandOverAsks.cs:141-159` and `src/Daoris.Web/src/work/start/handOverOffer.ts:140-160` | the stored order | the arranged order. The fixture `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/hand-over-order.json` gains rows |
| `Participants/Helpers/HelperChoice.cs:55-61` and the partner and reviewer choices | fence then entries | arranged within each agent's alternatives |
| `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/DriverRoutes.cs:379-399`, `:541-557` | `ROLES`; `SET_ROLE` and `SET_ROLES` | answer `policy`, `policyState`, `next`, presets; read `policy`. Same `Person` class, no new route |
| The session record chain (`.claude/knowledge/session-record-fields.md`) | five machine-local fields | `chosen`, the sixth, kept off the wire |
| `src/Daoris.Web/src/agents/roles/RoleEditor.tsx`, `Roles.tsx`, `src/Daoris.Web/src/work/head/ChoiceChip.tsx`, `src/Daoris.Web/src/work/actors.ts:27-64` | order only | §5's surfaces |
| `src/Daoris.Web/src/locales/{en,zh}/*`, `glossary.json` | — | §5.6's keys and terms |
| `Help/Proposals/HelpRoleProposals.cs`, `src/Daoris.Service/Daoris.Service.Mcp/HelpRoleTool.cs:19-32` | `role_propose` without policy | `policy`, its description, the coverage row (`.claude/knowledge/setting-doors.md`) |

## 8. Rows in build order

**STAFFPOLICY1a: the record, store and judges, dark.**
- *Contract:* step 16's three tables. `StaffPolicy` with its shape, digest, presets and `Judge`'s refusals. `StaffJudge.Arrange` and `OnCondition`, pure. Nothing reads them at a start.
- *Proof:* the judge table both ways. `staff-policy.json`'s arrangement rows (3:1 gives 1,1,2,1,1,1,2,1; ties by order; share; budget; takes; hold). A default policy answers today's action for every condition. An older build's staffing statements over a policy leave it stale (the compatible-builds test shape). The store opens on an older stamp.

**STAFFPOLICY1b: the doors.**
- *Contract:* `SET_ROLE`, `SET_ROLES`, `role policy …` and `role_propose` carry `policy`, judged once (`RoleDoor`). `ROLES` answers it with `policyState` and `next`. Daoris's own staffing writes re-key it. A home with no store refuses it in words.
- *Proof:* door tests per refusal code. Re-keying across a reorder, an added choice and a removed choice. *Take it* makes it stale. Pipe census unchanged (`Person`). `HelpCoverageTests`, the room's goldens, `McpToolRegistrationTests`.

**STAFFPOLICY1c: the start's walk reads it.**
- *Contract:* `StartPlan` arranges for every caller. The ledger's reservation lives under the roster's gate. `role_starts` is written at the open. `chosen` goes through the record chain. `actor.chose` gains `rule`/`by`. The opening note says the why. `budget.starts` and `takes` are enforced.
- *Proof:* real ticks on the protocol stand-in.
  - 3:1 over eight new quests lands 6:2.
  - Two starts in one look under 1:1 land on different choices.
  - A weighted pick whose accounts all cool falls to the next by weight.
  - A third start past a budget of 2 starts is passed `over-budget`, or waits naming midnight.
  - A design quest takes the only choice taking design.
  - **Reads unchanged:** `WalkAgentTickTests`, `ModelRefusalTickTests`, `PressModelTickTests`, `LimitResumeTickTests`, `AccountRotationTickTests`, `SignInRefusalTickTests`, `ChatPendingProcessTests` and `LimitGoesElsewhereProcessTests` pass unchanged with no row and with an explicit `first` row.

**STAFFPOLICY1d: the other walkers.**
- *Contract:* LIMITGOON2's `ElsewhereAsync` reads the bound role and arranges (this fixes the seeded-id read). `BriefedMove`, `ChatPending`, the helper, partner and reviewer passes are arranged within an agent. `HandOverOrder.Steps` and the twin take `arranged`. Brainstorm is refused. The census holds every order reader.
- *Proof:* a scoped added role's conversation at its agent's limit walks that role's order (red first). Hand-over fixture rows for an arranged order on both sides. The census fails on a new direct `Effective` caller.

**STAFFPOLICY1e: the conditions in the course.**
- *Contract:* `CourseFacts.Policy` and `LineCourse.Decide` for `limit` (walk, `wait`/`upTo`, `ask`), `transient` (`after`, a move instead of the hold), `failed` (`after`), `quiet` (the carry-on after a quiet stop), `signed-out` and `model-refused` (counted). The 30-minute hold. `most` with `held.moves-bound`. The `not-answering` mark. New `from_why` codes, notes and log lines.
- *Proof:* one real tick per condition on stand-ins with held clocks (`.claude/knowledge/held-clocks.md`).
  - Transient: two failed tries move to choice 2 after 3 minutes, briefed.
  - Failed: twice moves to choice 2, and the strikes still park at 3.
  - Quiet: a headless quiet end carries on with choice 2.
  - Limit with `wait`: holds for choice 1's reset while choice 2 is ready; with `upTo 30` it moves at 30.
  - Bound: a fourth move holds.
  - Default: each existing class passes unchanged.

**STAFFPOLICY1f: budgets by tokens, and the budget's hand-over.**
- *Contract:* the usage turn line's `choice`; tokens per role and choice per local day; `over-budget` by tokens. `hand-over` at a running session's turn end through the curve move's seam (`Course/Chat/CurveMove.cs`), with its hold.
- *Proof:* a stand-in's reported tokens cross the budget mid-session: the session finishes its turn, then a briefed part opens on choice 2. Without `hand-over` nothing moves.

**STAFFPOLICY1g: the page.**
- *Contract:* §5.1–5.3 and §5.6. The rule-dependent ⓘ. The preview from `ROLES.next`.
- *Proof:* page tests in en and zh. Stories of the editor, the fold and the head at the frame's widths, 1546 and 680. `i18n:check`, `names:check -- --strict`, the web's rules.

**STAFFPOLICY1h: Ask Daoris's room.**
- *Contract:* the room's facts gain each role's policy and its 7-day moves. The card's before → after.
- *Proof:* the room's goldens; a proposal's stale revision refused at *Apply*.

**STAFFPOLICY1i: filed behind measurements, not built.**
- `room`: the choice whose accounts report the most allowance left first, comparable only where both makers report windows.
- `measured`: by per-choice outcomes, once records give a comparable success measure. D174 judges finished work.
- Each needs its measurement and a D191 round.

**STAFFPOLICY1j: the installed proof (the owner's).**
- The owner's press sets Build to a spread and a move on the install. The window shows the chip, *Chosen* and the use line. One move is observed. It spends accounts, so it is the owner's call.

## 9. Rejected

- **Weights inside the choice objects.** An older build's writes drop them silently (`Settings/ActorKey.cs:144-165`, `Roles/RoleStaffing.cs:701-720`).
- **A random weighted pick.** Not predictable for a person and not testable.
- **A time-window spread.** It starves a choice over quiet nights and depends on the clock.
- **Per-account weights or a choice naming an account.** D205 assigns no account to a role.
- **Layering a policy over the machine role's by scope.** Two records would decide one start (§2.4).
- **A price-sorted *cheapest first*.** There is no price (D98); `docs/archive/2026-10-23-actors-design.md:1274-1275` stands.
- **A model choosing the choice, or an *Auto* pool.** Unchanged from D205.
- **A move mid-turn, or replaying a refused turn elsewhere.** D205 §4.3.
- **Moving a pick or a handed agent.**
- **The policy ending a quiet run.** D200.
- **A new *What needs you* kind, or a notification per move.** D205 §4.5 and §7.
- **Calling the moves *switching*.** That word is D197's account switch.

## 10. What this proposal did not check, and what it leaves to the steward

- **Not run, and no window looked at.** Line numbers are as read at `7568ecc89`.
- **The native door's `limit` is unobservable until TOOL4n.** Whether `budget.tokens` should wait for LOGCOLLECTION1's model field is open; the budget reads tokens, not models.
- **For the steward to settle:**
  1. whether `most` should default to 3 rather than none for *First that can run* once a person adds any move;
  2. whether `transient.after` below 5 may also shorten D210's episode for conversations, which this proposal leaves at 5 since they are row 1b's own;
  3. whether `hand-over` for budgets is worth its widening of D163, or should be cut to start-time budgets alone. This maker would cut it if the read finds the curve's seam does not fit.
