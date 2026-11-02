# CONTEXTDRIVE1: the design. A line's behaviors counted in its own context, and a supervisor, an appointed agent or the person's rules, acting on them

Status: proposed 2026-11-03 (D229 reserved for it), for its reads under D191. It answers `2026-11-03-context-counters-brief.md`, whose
§2 binds every section: each piece joins a structure that exists, and none runs beside one. Each claim about the code was read at
`07fc4e09d`. Driver paths start at `src/Daoris.Desktop/Daoris.Desktop.Driver/`, the modules' at
`src/Daoris.Desktop/Daoris.Desktop.Modules/`, the web's at `src/Daoris.Web/src/`, the service's at
`src/Daoris.Service/Daoris.Service.Core/`.
Read for it: D188, D200, D205, D208 and the supervisor design §2-§6, D210, D212's course decision, D225 with its note of 2026-11-03
and the COMMS1 design §5.3, §10, §11, D226 and the staff-policy design, D228 and the ONEWAY1 design §1-§2 and §10, D189 and the
session-context design §3, the foundation design §2.5 and §2.7.

## 0. In short

- **The census** (§1). Twelve behaviors are counted or bounded today, each by its own reader: four have a number that holds or
  moves a line (answers alone, failed sessions, transient tries, moves a day), the rest are counted by the policy's conditions or
  counted nowhere. Machine load, a shared host's protocol and Daoris's own nudges are bounded too; they are not a line's behavior and
  stay as they are.
- **The counts** (§2). One shape, one pure read, `LineCounts.Of`, each count read from the one record that already carries it, keyed
  by the line as `role_moves` is. Nothing new stores a number. A count is *exact*, *at least* or *unknown*, with why.
- **The session sees them** (§3) in its brief, in a go-on's appendix and at its start, in Daoris's words, as facts.
- **The rule supervisor** (§4). A rule is *when this count reaches n here, do this*: the actions are the doors that exist. It is a
  holder kind of D208's supervisor role, kept in the desktop store, set through the role door (`SET_ROLE`, `SET_ROLES`, the terminal's
  role verb, `role_propose`), inherited by place, read inside D212's one course decision with no model. It grants nothing, so it is
  set as D226's policy is, not appointed. STAFFPOLICY1's moves on a condition and its `most` become rules.
- **When an agent and a rule both apply** (§4.6), the rule is read first and its act stands; an appointed agent is asked only where a
  rule says `refer`, and its answer to a question is refused where a rule would hold the line.
- **Defaults** (§6). Today's numbers become seeded rules, so nothing changes until the person changes them; an existing home converts
  through a `rules` part of ONEWAY1's conversion door, and the four old readers are deleted with their register entries.
- **Rows** (§9): the read, the brief, the record and the supervisor's print on main now; the door and the decision after them; the
  part, the page and the words with ONEWAY1's wave 2; `refer` with the supervisor's activation; the deletions with ONEWAY1's wave 6.

## 1. The census

### 1.1 Counted, and acted on

| Behavior | What it counts | Kept in | Reset by | What acts on it today | Becomes |
|---|---|---|---|---|---|
| **Answers alone** | Go-ons whose prompt took only solicited answers (a helper's, an opinion's findings, the supervisor's), in a row on a line | Nowhere: read again from the line's event files each time (`Course/Domain/AnswersAlone.cs:45-90`), so a trimmed log resets it | The person's word, Daoris's own word, the person's *Go on with this answer* (`:100-103`) | `Bound = 2` (`:21`): `CourseDecision.AloneHeld` holds the next (`Course/Domain/LineCourse.Decide.cs:210-221`), through `Course/GoOn/AnswersAloneHold.cs:32` for a driven line and `Course/Chat/ChatGoOn.cs:132`, `Course/Chat/ChatHelperWords.cs:112` for a conversation; the supervisor's route refuses `supervisor-bound` (`Roles/QuestionSupervisorDecisions.cs:175`) | The count `answers-alone`, its durable source COMMS1g's admission; a waited quest's result a cause (COMMS1 §11); the bound a seeded rule |
| **Failed sessions** (the code's strikes) | This machine's sessions on a quest that failed or were cut off, never a limit, a refused account, a transient end, a model not offered, an update's close or a teammate's | The service's session records, all time (`Service/ServiceClient.cs:2433-2458`), into `Snapshot.Strikes` (`WorkOrders/SnapshotReads.cs:40`) | *Try again*, which marks `forgiven` at the records' count (`Settings/DriverConfig.cs:52-53`, `:86-97`); the person's words go on past it (`WorkOrders/Domain/Planner.cs:1360-1362`) | `strikes` in `driver.json`, 3, 0 never (`Settings/DriverConfig.cs:38-45`), set by `SET_STRIKES` and `driver strikes` (`Settings/DriverChoicesCommand.cs:81`); the planner parks the quest (`Exhausted`, `Planner.cs:1357-1372`), row `parked-quest` (D58) | The count `failures`; the home's number a seeded rule |
| **Failures in a row on one choice** | The trailing failures on one agent and asked model | The service's records (`Service/ServiceClient.cs:2466-2494`), `Snapshot.FailedRuns` (`Planner.cs:213`) | Any other end, or another choice | D226's `failed` move with its `after` (`WorkOrders/StaffCourse.cs:117-141`) | The count `failures-on-choice`; the move a rule |
| **Moves to another choice** | A line's moves, less the person's approvals | `role_moves`, store step 17 (`Roles/StaffMoves.cs:26-166`) | 24 hours; a yes releases one (`:54-67`, `:119`) | `most`, 3 once any move is set, 1 to 8 (`Roles/StaffPolicy.cs:163-167`, `:297`); the next move becomes an ask (`Roles/StaffJudge.cs:239-241`), held `moves-bound` (`LineCourse.Decide.cs:58-62`) | The count `moves`; `most` a rule |
| **Transient tries** | D210's episode: a try after 1, 2, 4, 8 and 16 minutes | The run's memory (`Agents/TransientRetry.cs:24-38`); the end past it noted `ended.transient` with its count and minutes (`Events/NoteCodes.cs:189`) | A prompt that answers | The run ends past `Limit`, D226's `after` for a driven line (`TransientRetry.cs:34-38`); the line holds `TransientBound` (`LineCourse.Decide.cs:123-126`, `Planner.cs:885`), no strike (`ServiceClient.cs:2448`); a conversation keeps five | The count `tries`; the hold a seeded rule; the schedule stays D210's |
| **Quiet** | A running session unheard and not busy | The watch, marking at Q, ten minutes, and bringing it to the person at three times Q (`Events/SessionWatch.cs:89-90`, `:141`); its episodes written with their end; a run it ended noted `ended.quiet` (`Events/NoteCodes.cs:278`) | Heard again | Row `quiet`; nothing ends but a dead process and a headless `--quiet-end` (D200); D226's `quiet` move once the run has ended | The count `quiet`; Q stays the watch's (§1.2) |
| **Limits and cooled accounts** | An account's limit read from its door's failure (`Agents/AccountLimits.cs:72-82`) | The account's own records (D197), the walk's `cooling` pass (`Roles/StartPlan.cs:26`), a curve move held 30 minutes (`Agents/Domain/SwitchDecision.cs:68`) | The account's reset | The walk (D206, one attempt per cool-off); D226's `limit` move | The count `limits`, a line's limit ends on its choice; the cool-offs stay the account's |
| **Refusals** | A model refused at the open, a sign-in refused, each moved once inside one start | `role_moves` rows for the in-start moves (D226's 1e2 note); a harness's denied tool call is an event, counted nowhere | The next start | D226's `model-refused` and `signed-out` moves; the in-start moves | The counts `refusals.model`, `refusals.sign-in` |
| **Tokens** | Turns' reported input and output, by binding and choice | The turn lines (`Agents/UsageTurns.cs:19-36`); the local day summed by choice (`Roles/StaffLedger.cs:418-433`), an unreported turn holding its choice | The day | D226's `budget.tokens` passes a choice at a start; the accounts' curves (D179) | The count `tokens`, a part's and a line's; the role's day stays the budget's |
| **Minutes** | A part's and a line's working time | The records' times and the turns' | None | Nothing for a session (D200; `timeoutMinutes` kept only to be said, `Settings/DriverConfig.cs:55-61`); an ask's own minutes bound its run (`Participants/Domain/HelperInstruction.cs:34`, `Participants/Domain/PartnerInstruction.cs:28`, the service's `Review/Opinions.cs:274`, at most 120) | The count `minutes`; an ask's minutes stay its asker's |
| **Quests handed, results waited** | Quests a line's sessions published; a quest result the line went on by (D79's resume, `Loop/Driver.cs:4245-4252`) | The service's quests; the records' `awaits` | None | Nothing; the result counts toward answers alone (COMMS1 §11) | The counts `quests-handed`, `results-waited` |
| **Hand-overs** | A line's parts by why they began | `from_why` (`Course/Domain/CourseAnswers.cs:235`), the hand-over box (`Course/Domain/HandOverRecords.cs:33`, store step 18) | None | The hand-over rule's consent for an agent's own ask, per place (`Course/Domain/HandOverRules.cs`), which is no count | The count `hand-overs` |

### 1.2 Bounded, but not a line's behavior: they stay

| Bound | Where | Why it stays |
|---|---|---|
| The cap, sessions running at once | `Settings/DriverConfig.cs:15`, `:32`; `SET_CAP` (`Settings/DriverChoicesCommand.cs:83`); `Planner.cs:1498` | It bounds the machine's load, which no line shows, and it is the person's setting already. The supervisor judges a line (question 2) |
| Helpers at once, two per asker, a lead's open pieces, process starts, claims at once | `Loop/DriverHelpers.cs:457`, `Participants/Domain/HelperJudge.cs:54`, `Participants/Domain/PartnerAdmission.cs:34`, `Platform/ProcessStarts.cs:51`, `Loop/Driver.cs:2027` | Concurrency, a slot waited for, never a line held |
| A part's channel posts, ten, three urgent | The service's `Asks/Channel.cs:15`, `:24` (`ChannelBounds`) | The shared host keeps it and reads no machine-local rule (D208 point 1: *this machine alone*). Shown as the count `posts`, never ruled |
| Daoris's carry-ons for an uncommitted tree, two, and one after a go-on | `Agents/TurnCarry.cs:28`, `Loop/Driver.cs:4255` | Daoris's own words, not the session's behavior |
| The walk's passes: a choice moved off held 30 minutes, the weighted window of 20, budgets, shares | `Roles/StaffJudge.cs:119-122`; D226 | Staffing: which choice a start takes, not what a line does |
| The quiet window Q | `Events/SessionWatch.cs:89-90` | How quiet is observed (D200), not a limit on it |
| Sizes and shapes: the brief's caps, a chain's five steps | `Context/Domain/ContextBrief.cs:94-139`, the service's `Quests/QuestExchange.cs:537` | Bounds of a record |

### 1.3 Read against the brief

- The answers-alone bound is D188 **point 4** (`docs/decisions/D188.md:43`, and the code's own summary at `AnswersAlone.cs:4`). The
  brief, D225's note of 2026-11-03 and COMMS1 §5.3 call it point 5, which is the delivery rule. D229 amends point 4.
- The cap and the quiet window are named among the caps to convert. The census keeps both (§1.2): the cap is a setting of the
  machine's load, and D200 already decided that no quiet run is ended by Daoris.

## 2. The counts

### 2.1 One shape

```json
{ "count": "answers-alone", "of": "line", "line": "412", "window": "in-a-row", "n": 2, "known": "exact",
  "since": "2026-11-03T09:12:00Z", "from": "admission" }
```

`count` is a code of one closed vocabulary (§2.2); `of` is `session`, `line`, `quest` or `role`; `window` is the one the count
offers; `since` is its reset or its window's start; `from` names its source; `known` is `exact`, `at-least` with `why`, or
`unknown` with `why` and no `n`.

### 2.2 One read, each count from its one source

`LineCounts.Of` (`Course/Domain/LineCounts.cs`) is pure: it takes what `LineCountsRead` (`Course/LineCountsRead.cs`) read once
for the line and answers every count. Its readers are the course's facts (§4.5), the brief (§3), the head and *Details* (§7.4), the
supervisor's print (§5) and Ask Daoris's room (§7.6). No count is a stored number: a total kept beside the record it counts would
be a second way to count it, and would drift from it.

| Count | Of | Window | Read at (its point) | Its one source | Reset |
|---|---|---|---|---|---|
| `answers-alone` | line | in a row | a go-on by answers alone | COMMS1g's admission rows; until that row, `AnswersAlone.Count` over the events | the person's word, Daoris's fact word, the person's go-on |
| `failures` | quest | since *Try again* | a quest's next start | the service's records less `forgiven` | *Try again*; the person's words |
| `failures-on-choice` | line | in a row, one choice | a part's end | the service's records | another end or choice |
| `tries` | session | the episode | the run's start (its limit), the episode's end | the run's episode; `ended.transient`'s `count` | a prompt that answers |
| `moves` | line | 24 hours | a move | `role_moves` | the window; a yes releases one |
| `limits` | line | the part, its choice | a part's end | the record's limit flag | the next part |
| `refusals.model`, `refusals.sign-in` | line | the start | the start | the record's notes, `role_moves` | the next start |
| `over-budget` | line | the start | the start | the walk's `over-budget` pass | the next start |
| `quiet` | line | the line | a part's end, the run ended | `ended.quiet` and the watch's episodes in the events | none |
| `tokens` | session, line | the part; the line | a go-on, a start | the turn lines | none |
| `minutes` | session, line | the part; the line | a go-on, a start | the records' and turns' times | none |
| `hand-overs` | line | the line | a hand-over's admission | the parts' `from_why` | none |
| `quests-handed` | line | the line | a go-on | the service's quests by publishing session | none |
| `results-waited` | line | the line | a go-on, as a cause of `answers-alone` | the records' `awaits` | none |
| `posts` | session | the part | shown only | the channel's rows | none |
| `starts`, `tokens.day` | role, by choice | the local day | a start, the budget's | `role_starts`, the turn lines | the day |

### 2.3 Durable across a hand-over and a restart

Every source above is durable: the desktop store, the service's records, the session's event files and the turn lines. A line is
keyed as `role_moves` keys it (`Roles/StaffMoves.cs:15-18`): a quest's id or a conversation's. A line's parts are joined by their
`from` links (SESSCTX1b), so a hand-over carries every line count to the new part, and a restart reads every count again equal. The
one in-memory source is a running episode's tries, whose end is written with its count before the part ends. Counts are read on this
machine alone, a teammate's record left out as the strikes leave it (`ServiceClient.cs:2441`; D178: sessions never move between
machines).

### 2.4 *At least* and *unknown*

- **At least** where history before a point is missing: a trimmed event log (D195's retention), a part from before its `from`
  link, a store before the step that keeps the source, a turn that reported no tokens (*not reported*, never zero), and COMMS1g's
  admission seeded from the events' count (COMMS1 §13). The count is a lower bound, said with what is missing.
- **Unknown** where the source does not answer: the service down for `failures` and `quests-handed`, a store that does not read.
- **A rule acts on an exact or at-least count** that reaches its `at`, and **never on an unknown one**: the line's head and *Details*
  say *cannot count: the service did not answer*. A failure of Daoris's own store or service already holds the next start by D226's
  fixed rule (the staff-policy design §4.1), so an unknown count never lets a start through that a known one would hold.

### 2.5 Whose counts

A **session's** (one part): `tries`, `tokens`, `minutes`, `posts`. A **line's** (across its parts): `answers-alone`,
`failures-on-choice`, `moves`, `limits`, `refusals`, `over-budget`, `quiet`, `hand-overs`, `quests-handed`, `results-waited`, and
the line's `tokens` and `minutes`. A **quest's**: `failures`. A **role's**: `starts` and `tokens.day` by choice, which a policy's
budget reads (D226) and the room shows; no rule watches a role's count, since that is the budget's job and one job has one way.

## 3. What the session sees

1. **The brief** (`Context/Domain/ContextBrief.cs`, D189 point 4, the session-context design §3.1) gains an item, *The line's
   course, as Daoris counts it*, under its own cap of 1,000 characters inside the 40,000: one line per count that is not zero or that
   a rule watches, with the rule as a fact. *Answers alone in a row: 2. The person's rule for this repository: Daoris waits for them
   after 2.* *Failed sessions on this quest: 1 of 3.* *This part: 34 minutes, 118,000 tokens reported.* An at-least or unknown count
   says so. It is accounted in the instruction's `InstructionAccount` with a cut code of its own, as every section is.
2. **A go-on's words.** The resume's appendix (`Continuations.AfterWords`, `Course/Domain/Continuation.cs:166-168`) gains one line
   where a count changed since the part's last go-on or a rule is one short of acting: *Daoris's count for this line: 2 go-ons by
   answers alone in a row; the next waits for the person.*
3. **A start** carries the same item where any count is not zero (a quest that failed here before), and **`line_read`**, the
   line's history tool (the session-context design §3.5), answers the counts at its head, so a session can read its course mid-turn
   without a new door.
4. **Facts, never instructions** (D208 §4.4 read for the asker). The lines are Daoris's own English, composed from codes and
   numbers alone: no agent's text, no account's name and no path reaches them, so nothing another agent wrote travels through them.
   They state what was counted and what Daoris will do, never what the session must do. A session cannot lower a count by its words:
   only the person's word, Daoris's reset and the person's press move one back.

## 4. The rule supervisor

### 4.1 A rule's shape

```json
{ "id": "r-3", "count": "tries", "window": "episode", "at": 2, "do": "weighted", "upTo": null,
  "place": "repository:daoris", "roles": ["build"] }
```

| Field | Says | Bound |
|---|---|---|
| `count` | the behavior watched | §2.2's codes, never `posts`, `starts` or `tokens.day` |
| `window` | which of the count's windows | only one the count offers (`tokens` and `minutes`: the part or the line) |
| `at` | the threshold: the rule acts at the count's point once the count stands at `at` | per count: `answers-alone` 1-20, `failures` 1-20, `failures-on-choice` 1 to the failures rule's less one, `tries` 1-5, `moves` 1-8, `minutes` 10-1,440, `tokens` 10,000-10,000,000, the conditions 1 |
| `do` | the action (§4.2) | only those its count allows |
| `upTo` | for `wait`, the most minutes it waits before the role's rule walks on | 5-600, as D226's |
| `place` | where it watches: `machine`, `workspace:<id>`, `repository:<id>` or `line:<id>` | a place Daoris drives; a line on this machine |
| `roles` | the working roles whose lines it watches; absent is every role | ids the catalogue holds |

A place's rules are written whole, with the place's revision, as a role's policy is (D226); a press against a moved revision is
refused.

### 4.2 Its actions: the doors that exist

| `do` | The window says | What happens | Counts it allows |
|---|---|---|---|
| `hold` | *Wait for you* | The line does not go on; it waits on *What needs you* (§7.5). The line's one go-on press (ONEWAY1j) releases one go-on; for `answers-alone` that press is the person's word and resets the count, as *Go on with this answer* does | every count |
| `ask` | *Ask before the next* | Held, offering *Go on now with {next}* (D206 point 7); one yes releases one move | `moves` and the conditions |
| `wait` | *Wait for its reset* | Waits for the choice's moment back, at most `upTo`, then walks by the role's rule | `limits`, `over-budget` |
| `next`, `weighted` | *Go to the next choice*, *… by weight* | A move to another choice: a new part handed D189's brief at a part's end, counted in `moves` | `failures-on-choice`, `tries`, `quiet`, the conditions |
| `retry` | *Try it again* | The same choice again, the planner's carry-on | `failures-on-choice`, `quiet` |
| `refer` | *Ask the supervisor* | The appointed agent decides (§4.6); with none appointed, `hold`, said | every count; offered once SUPERVISOR1c is |
| `off` | *None here* | This place takes no rule on this count, so an inherited one does not apply | every count |

The brief's *hand over* is `next` or `weighted`: every move to another choice is a new part handed the brief. Its *stop* is
`hold`: a rule acts only between turns, where not going on and stopping are one state, and the person's *Stop* stays the person's.
Its *go on* is the default course, which needs no rule.

### 4.3 A holder of the supervisor role

D208 §2.2's table gains a row:

| Holder | Who | Its acts reach Daoris by | What it may do | Offered |
|---|---|---|---|---|
| `rule` | The person's rules for the place (§4.4) | Daoris's own course decision, never the pipe nor a connector | §4.2's actions at its count's point; never an answer to a question, since a rule reads no meaning (D24, D157) | From CONTEXTDRIVE1e, on no flag of D208's |

**Set, not appointed.** D208 point 1 keeps the appointment to the owner's own press in the window because its holder acts with the
owner's authority: it answers the owner's question. A rule holds a line, moves it within the choices the person staffed, or hands a
point to an agent the owner appointed in the window. Each of those the role door already sets (D226's moves through `SET_ROLE`,
`role policy` and `role_propose`), so a rule is a staffing-class press: the window, the terminal's role verb (refused from a
session's terminal under full access, D205 point 9) and Ask Daoris's card applied by the person's press. The agent holders
(`terminal`, `actor`) stay D208's, appointed through `SET_APPOINTMENT`; the foundation design §2.7 keeps every authority on a
`Window` route, and a rule carries none.

**Not the appointed half of the catalogue.** The supervisor's id is reserved (`Roles/RoleCatalog.cs:49`) and the appointed shape
refused (`appointed-not-configurable`). The role door takes, for the id `supervisor`, the place's `rules` and `rulesRevision`
alone, written to their own family, never a `roles` row: the appointed half stays the later design the foundation design §19.9
leaves it to.

### 4.4 Kept, set and inherited

- **Kept** in the desktop store, store step 19: `supervisor_rules` (one row per rule: id, place, roles, count, window, at, do,
  upTo, the revision it was written in) and `supervisor_rule_revisions` (place, revision, the set as written, when, by which door).
  The step raises the writer stamp, as D226's step 16 did: an older build reads and writes nothing, so it drives no line past rules
  it cannot read. A place's revision row is also the mark that its rules are reached (§6.2). Machine-local, never on a sync wire
  (D208 point 1).
- **Set** through `SET_ROLE { id: "supervisor", place, rules, rulesRevision }` and `SET_ROLES`, judged by `SupervisorRules.Judge`
  before anything is written, in one transaction; by `daoris role rules …` (§7.6); proposed by `role_propose`'s `rules`.
- **Inherited by place** as D205's scopes are, with no layering, as D226 refused it: for each count, a line's rules come from the
  most specific place holding any rule on it (the line, its repository, its workspace, the machine), and that place's rules on it
  replace every less specific place's. Within one place, rules naming the line's role replace those naming none. `off` ends the
  inheritance for that count. Several rules on one count at one place make a ladder (*at 2, refer; at 4, hold*); the highest `at`
  reached acts.
- A line's own rules (`line:<id>`) are removed with the line's records by the retention pass (D195).

### 4.5 The judge, and where it decides

`Roles/SupervisorRules.cs` holds the record, `Judge` (a closed set of refusals: `count`, `window`, `at`, `do`, `upTo`, `place`,
`roles`, `moves-unbounded`, `refer-not-offered`, `revision`, `convert-first`), `Effective` (§4.4) and `Decide`, pure: given the
counts and the effective rules at a point, the act or none. Where two rules on different counts act at one point, the narrowest
wins: `hold`, then `ask`, then `refer`, then `wait`, then a move, then `retry`.

It is read at the counts' points, inside the structures that decide today:
- **The course.** `CourseFacts` gains `Supervision`: the effective rules and the counts, read once by the executor as
  `CourseAlone` and `CoursePolicy` are (`Course/Domain/CourseFacts.cs:192-196`, `:271-285`). One step of `CourseDecision.Decide`
  reads it, replacing `Policied` and `AloneHeld` (`Course/Domain/LineCourse.Decide.cs:21`, `:52-79`, `:210-221`): the course stays
  D212's one decision.
- **A quest's next start**: the planner's place for the strikes (`WorkOrders/Domain/Planner.cs:1357-1372`) asks it for `failures`
  and `over-budget`.
- **A run's start** sets `TransientRetry.Limit` from the effective `tries` rule (`Agents/TransientRetry.cs:34-38`), and
  `StaffCourse.Look` reads rules where it reads the policy's moves today (`WorkOrders/StaffCourse.cs:117-215`).
- **The supervisor's release** reads it (§4.6).

A rule never acts mid-turn (D205 point 5), never ends a running or quiet session (D200), never moves a pick or a handed agent
(D226), never acts on an unknown count, and never answers a question.

### 4.6 When an agent and a rule both apply

1. **The rule is read first**, at every point, with no model. An act other than `refer` stands: the agent the owner appointed acts
   within the rules the person set, as every appointed role acts within the policy (D228's latest structure).
2. **The agent's answer to a question** is refused where releasing it would make a go-on a rule holds: `supervisor-bound` stays the
   code for the answers-alone rule, since a stored code is never renamed, and `rule-holds` is new for every other count. It is
   checked at the keeping and again at the release, as `supervisor-bound` is (`Roles/QuestionSupervisorDecisions.cs:175`, `:286`).
3. **`refer`** hands the point to the appointed agent through the supervisor's third act, *course* (§5), decided, held for the
   overrule window and released as D208 §3.4-§3.6 keep a decision. With no agent appointed, a dormant appointment or the actor not
   offered, the point waits for the person and says why: *your rule asks the supervisor; none is appointed here*.
4. **With neither**, where the person removed every rule on a count, the count is shown and nothing holds a line on it (D225's note).

### 4.7 Said on the line, kept in the decision record

- **On the line**, a note of code `supervisor.rule` naming the count, its value and known state, the rule, its place and the act:
  *Waits for you: this line went on by answers alone 2 times in a row. Your rule for this repository waits for you after 2.* A move
  writes what a policy's move writes today, `role_moves`, `chosen` with its condition, `actor.moved` and `opening.moved-choice`,
  its reason naming the rule.
- **In the decision record**, beside the questions' decisions: `<home>/sessions/<session>.decisions/<decision>.json` with
  `holder: { kind: "rule", rule, revision, place }`, the counts read, the point, the act and its time, written once, atomically,
  with one state line, `acted`. Like every decision file, it is history and never read to decide (the supervisor design §3.4).
- **The machine log**, `supervisor.rule` `{ line, session, rule, count, n, known, do }`.
- **No notification**: a hold is on *What needs you* (§7.5), and no move is a notification (D226).

### 4.8 STAFFPOLICY1's conditions and `most`, taken up

Every move a policy sets on a condition is a rule naming its role at the role's place: `transient after 2: weighted` is
`tries at 2: weighted`, `failed after 2: next` is `failures-on-choice at 2: next`, `limit: wait upTo 60` is `limits at 1: wait`,
and `most 3` is `moves at 3: ask`. `StaffJudge.OnCondition`'s *as today* is no rule, the default course. The policy keeps what a start
chooses: its rule, weights, shares, budget amounts and works. D226's fixed rules (the staff-policy design §4.1) stay fixed. D226's
bound against a line moving back and forth is kept by the judge: a move rule needs a `moves` rule at its place (`moves-unbounded`),
which the editor writes at 3 with the first move rule.

### 4.9 What a rule never does

It grants nothing, answers no question, reads no text, starts no run Daoris would not start, ends no turn, moves no account (D197
point 8: a cool-off is the account's), and changes no other machine. Its record is in the home's ordinary reach, as `driver.json`'s
`strikes` and D226's policy are: an agent that writes the home can change a rule, as it can change those today (D156 point 7);
converting the answers-alone bound from code to a rule brings it to that same reach, said here and in the conversion's review.

## 5. The agent supervisor's reading

- **Handed.** The fenced print of `sessions supervise <id>` (`Roles/SessionsSuperviseCommand.cs`) and the actor's prompt
  (SUPERVISOR1c) gain a part, *The line's course*: every count with its known state and source, the rules in force on the line with
  their place and `at`, and the line's recent course (its parts with why each began, its last ten go-ons and who answered each),
  composed by Daoris from records and fenced as Daoris's facts, never the asking agent's claims (the supervisor design §4.4).
- **Cited.** A reason's `source` may name a count, `count:answers-alone=2 (exact)`. The decision keeps the counts it was handed as
  it keeps the question's snapshot, and the route refuses a `count:` source that names none of them (`no-source`), as it refuses a
  reason with no source (D135 point 4).
- **Deferred.** `SUPERVISOR_DEFER` gains the bound `course`: the line's course is the owner's to judge, with the counts as reasons.
- **Referred** (§4.6): the third act, `course`, in the form of the supervisor design §3.1's acts, `{ id, line, rule, generation,
  proof, do, reasons }`, `do` one of `go-on`, `hold` and the moves the rule's count allows. It is a new act of the role, so D229 amends
  D208 point 1's list; its route follows the act door as it then stands (the foundation design §2.5's one act door).

## 6. Defaults and conversion

### 6.1 The seeded rules (recommended)

Every home holds rules equal to today's numbers, at the machine's place, for every role:
- `answers-alone at 2: hold` (D188 point 4);
- `failures at 3: hold`, or at the home's own `strikes`, and none where it is 0 (D58);
- `tries at 5: hold` (D210 point 3), a conversation's episode keeping its five as D226 settled;
- for each role with a D226 policy, its moves and its `most` as §4.8 maps them.

A fresh home's first start writes them, as ONEWAY1 §2 writes each role's seed, so every home reads its rules from one place and none
from code. **Why:** nothing changes on any home until the person changes it, as D226's default changed no spending; no number is
fixed in the product, since each is a rule the person sees on the supervisor's row and changes or removes with one press (D225's
note); a fresh and a converted home read the same rules; and removing a hold is a widening the person chooses, never one an update
makes for them. The seed for `answers-alone` reads against the letter of D225's note, which leaves a line with no supervisor
unheld: question 1.

### 6.2 The `rules` part of the conversion door

A part of ONEWAY1 §1.1's door, in its contract:
- **Census**: `driver.json`'s `strikes` (absent reading 3); each `role_policy` record with `moves` or `most`; the code's bounds.
  `Ended` once the machine's place holds a revision row, no policy holds `moves` or `most`, and `driver.json` holds no `strikes`.
- **Plan**: each rule written, said once (*Waits for you after 2 answers alone in a row, as today*); each seeded rule a row choice,
  ticked, an unticked one said under what widens; each policy re-keyed without its moves; the key removed. It applies after
  `staffing` in the door's order.
- **Effects**: the rules and the machine's revision in one store transaction; each policy written again, a new revision; the key
  removed. `forgiven` and `released` stay where they are: they are a count's reset and the person's release, not caps.
- **Undo** removes the rules it wrote, puts back each policy's revision and the key, and is refused where a rule or a policy has
  been written since.
- **Never touched**: the cap, the account cool-offs, the channel's bounds, the walk's passes, any repository's files.

On a home the part has not reached, the old readers decide and the door refuses a rule with `convert-first`, naming the review.

### 6.3 The kept reads and their ends

Each is an entry in `Transitional/Register.cs` with its marker, ending when the `rules` part's census reads `Ended`, deleted by
`CONTEXTDRIVE1l`:

| Entry | Kept |
|---|---|
| `answers-alone-bound` | `Course/Domain/AnswersAlone.cs:21`, `:34-42`, and its readers `Course/GoOn/AnswersAloneHold.cs:32`, `:90`, `Course/Chat/ChatGoOn.cs:132`, `Course/Chat/ChatHelperWords.cs:112` |
| `strikes-key` | `Settings/DriverConfig.cs:38-45`, `WorkOrders/Domain/Planner.cs:1362` |
| `policy-moves` | the policy record's `moves` and `most` (`Roles/StaffPolicy.cs:158-167`), `StaffJudge.OnCondition` (`Roles/StaffJudge.cs:228-263`), their reads in `WorkOrders/StaffCourse.cs:117-215` |
| `transient-bound` | the hold past five tries (`Roles/StaffJudge.cs:252`; `Course/Domain/LineCourse.Decide.cs:123-126`) |

### 6.4 What retires, and when

At `CONTEXTDRIVE1f`: `SET_STRIKES` and `driver strikes`; the policy press's `moves` and `most`, `role policy`'s `--on` and `--most`,
and `role_propose`'s policy moves, each refused naming `role rules`; their usage, catalogue keys and the room's lines. At
`CONTEXTDRIVE1h`: the hold rows' separate words (`parked-quest`, the answers-alone park, `policy-hold`) become one rule hold's. At
`CONTEXTDRIVE1l`: the four kept reads.

## 7. The person's surfaces

### 7.1 The supervisor's rules (Agents → Roles → Appointed roles)

The supervisor's row per place (COMPANYVIEW1e's, `agents/roles/AppointedRoles.tsx`) keeps its appointment and gains its rules,
drawn as the role editor's *When a choice cannot go on* fold is (`agents/roles/RolePolicy.tsx:117-193`, the exemplar): `FormGrid`
rows, selects with keyboard steps, a refusal under *Save* with the draft kept.

At 1546 px (the main area about 80 rem), a repository's group:

```
Supervisor   daoris · agent: not appointed · 4 rules (2 here, 2 from This Daoris)                              [Appoint…]
┌ Supervisor: its rules for daoris                                                                                  ⓘ
│   When                               reaches   then                            for              set
│   [Answers alone in a row ▾]         [ 2 ▾]    [Wait for you ▾]                [Every role ▾]   here             ✕
│   [Failed sessions on a quest ▾]     [ 3 ▾]    [Wait for you ▾]                [Every role ▾]   This Daoris      Set here instead…
│   [The service did not answer ▾]     [ 2 ▾]    [Go to the next by weight ▾]    [Build ▾]        here             ✕
│   [Moves to another choice a day ▾]  [ 3 ▾]    [Ask before the next ▾]         [Build ▾]        here             ✕
│   + Add a rule
│ Now here: 1 line waits for you on answers alone; none is one short of a rule.
│ Always: a rule acts between turns only; it never ends a running or quiet session and never answers a question.
└ Saving puts this to work at once for every line here; a line waiting now waits until you go on.      [Save] [Cancel]
```

At 680 px, each rule stacks label over select (`FormField when="stacked"`):

```
Supervisor · daoris · 4 rules                 [Appoint…]
▾ Its rules for daoris                             ⓘ
  When [Answers alone in a row ▾]
    reaches [2 ▾]  then [Wait for you ▾]
    for [Every role ▾] · here                      ✕
  When [Failed sessions on a quest ▾]
    reaches [3 ▾]  then [Wait for you ▾]
    for [Every role ▾] · This Daoris   Set here instead…
  + Add a rule
  Always: between turns only; …
[Save] [Cancel]
```

The ⓘ says what a count counts and what resets it. An inherited rule is drawn under its place's name with *Set here instead…*,
which copies that count's rules here. `Ask the supervisor` is offered only once the actor is (§4.6).

### 7.2 A role's editor

`MovesFold` (`agents/roles/RolePolicy.tsx:158-193`) keeps its rows and words and writes them as rules naming the role (§4.8); its
*At most [3] moves* is the `moves` rule. A rule naming the role set at another place reads there with *Edit in Supervisor…*.

### 7.3 A place's Roles fold

One line beside the roles: *Supervisor here: 4 rules (2 from This Daoris) · agent: not appointed · Edit…*.

### 7.4 The line's head and *Details*

```
1546:  #412 Build the policy store · daoris · Claude Code · opus · high   [waits for you · answers alone 2 of 2 ⓘ]
 680:  #412 Build the policy store     [waits for you ⓘ]
Details ▾  Course      Answers alone in a row   2 · your rule waits for you after 2 (daoris)
                       Failed sessions          1 of 3 (This Daoris)
                       Moves today              0 of 3 (Build)
                       Hand-overs 1 · quests handed 0 · results waited 1
                       This part                34 min · 118,000 tokens reported · no tries
           Supervisor  rules here 4 · agent: not appointed · 14:05 waited for you (answers alone, 2)
```

The chip shows only where a rule holds or the count is one short of a rule, within the head's `status` budget; *Details* draws
every count the line has. Their read is the session's existing one, with a field `counts` added.

### 7.5 *What needs you*

A rule's hold is one row kind, `policy-hold`, kept since a stored kind is never renamed in place and widened to every rule's hold;
the strikes' park and the answers-alone hold are written as it once their rules decide. The row names the count, the rule and its
place, with the line's one go-on and *Change the rule…*:

```
Waits for you  #412 Build the policy store · it went on by answers alone 2 times in a row; your rule for daoris waits after 2   [Go on] [Change the rule…]
```

It joins the session waits' union and the head's and foot's wait (`work/waits/waits.ts`, `WaitStrip.tsx`, `SessionHead.tsx`), the
part STAFFPOLICY1e3 left open, so one hold is counted once on the head, the foot and *What needs you* (COMMS1j).

### 7.6 Ask Daoris and the terminal

- **Ask Daoris**: `role_propose` gains `rules` with the place and its revision, judged by the same judge, applied only by the
  person's press, never through a `settings_*` grant; the card says *Supervisor, daoris: wait for you after 2 answers alone → after
  3. Lines here hold later.* The room's Roles section says each place's rules and a line's counts, so *why is this waiting?* is
  answered from records.
- **The terminal**, in ONEWAY1's tree (`daoris driver role …` until `ONEWAY1ai`):

```
$ daoris role supervisor --place repository:daoris
supervisor  daoris (repository) · agent: not appointed · rules revision 4
  answers-alone       at 2   hold       every role   here
  failures            at 3   hold       every role   This Daoris
  tries               at 2   weighted   build        here
  moves               at 3   ask        build        here
$ daoris role rules --place repository:daoris --set answers-alone=3:hold
$ daoris role rules --place line:412 --set minutes.line=180:hold
$ daoris role rules --place repository:daoris --off failures
```

### 7.7 Words

The window says *supervisor rule* (主管规则) where it must tell it from the glossary's *rule*, a doctrine's and an access rule's;
a count is *count* (计数); a hold reads *waits for you* (等你处理, the glossary's *waiting on you*), never *hold*, which the glossary
keeps for the person's hold of a repository (暂停); failed sessions are never *strikes* on the window (the glossary's *strike*).

## 8. What changes in the decisions

D229 states each; no older decision's text is edited.
- **D188 point 4**: *at most twice in a row* becomes the seeded rule `answers-alone at 2: hold`, which the person changes.
- **D58** and DRV6's `strikes`: the number becomes the seeded `failures` rule, `driver.json`'s key and `SET_STRIKES` retired.
- **D208**: points 1-2 gain the `rule` holder, set through the role door since it grants nothing; point 4 gains the `course` act and
  the defer bound `course`; point 6's one count stays one count, read through §2.
- **D210 point 3**: the hold past the episode's tries is the `tries` rule; the schedule stays.
- **D225's note of 2026-11-03**: its supervisors are §4's; COMMS1g keeps the count durably, and the rule and the agent's reading are
  this design's rows.
- **D226**: its moves and `most` become rules (§4.8); the policy keeps rule, weights, shares, budgets and works; `MostByDefault`
  is the companion rule the editor writes.

## 9. Rows, in waves

| Row (lanes; after) | Contract | Proof | When |
|---|---|---|---|
| **CONTEXTDRIVE1a** the counts, read (driver) | §2 | `LineCountsTests` per count over `fixtures/line-counts.json`: equal across a hand-over and across a restart; at least for a trimmed log, a part with no `from` and a store before its step; unknown for a service that does not answer; a teammate's record left out; every reader unchanged | now, on main |
| **CONTEXTDRIVE1b** the session sees its course (driver; a) | §3 | `ContextBriefTests` golden with the item and its cut code; a go-on's appendix golden; a start's; `line_read`'s head; a planted label in a count's source never reaches the text; `InstructionAccount`'s sums; `HandedCodesTests` and `handed.test.ts` | now, before COMMS1's wave 1 starts, else inside it beside COMMS1f |
| **CONTEXTDRIVE1c** the rules: record, store and judge, dark (driver; a) | §4.1, §4.4, §4.5 | `fixtures/supervisor-rules.json` held by `SupervisorRulesTests`: every refusal code; `Effective` by place, role and `off`; a ladder; the narrowest act; `DesktopStoreTests`' step 19 and the stamp | now, on main |
| **CONTEXTDRIVE1i** the agent supervisor's reading, dark (driver; a, c) | §5 but `course` | the print's golden with its fenced part; a `count:` source handed and one not, refused; the decision's counts snapshot; `rule-holds` at the keeping and the release | now, on main |
| **CONTEXTDRIVE1d** the door (driver, modules, service; c) | §4.3, §4.4, §7.6 | `SupervisorRuleDoorTests` (a moved revision, `convert-first`, `appointed-not-configurable` for any other field); `RoleVerbTests`; `HelpRoleProposalsTests` and the service's `HelpRoleProposalTests`; `McpToolDescriptionTests`; full access's terminal refusal | now, on main, after c |
| **CONTEXTDRIVE1e** the rules decide (driver; c, d) | §4.5-§4.7, §6.1's seed, §6.3's entries | `LineCourseTests`' pinned table with each seeded rule answering as today's reader did; `SupervisorRuleTickTests` on stand-ins: answers alone held at 2 then one go-on; failures held at 3 and *Try again*; tries at 2 weighted; moves at 3 asked; a minutes rule waiting at a go-on and never mid-turn; an unknown count never acting; the decision file and the note; a fresh home seeded at its first start; the transitional gate green with the four entries | now, on main, after d |
| **CONTEXTDRIVE1h** the head, *Details* and *What needs you* (web, modules, driver; e) | §7.4, §7.5 | page tests en and zh; stories at 80, 39 and 25 rem, both themes; `AttentionStateSliceTests`, `attention.test.ts`; one hold counted once on the head, the foot and *What needs you*; the rehearsals' whole-answer comparisons checked for `counts` | after `wave/workflowclean1` reaches main |
| **CONTEXTDRIVE1f** the `rules` part (driver, modules; ONEWAY1ae, e) | §6.2, §6.4 | a fixture home with `strikes` 5 and Build's policy `transient after 2: weighted`, `most` 2: plan, Apply, a start answering as before, Undo, Undo refused after a rule is edited; `strikes` 0 writing no failures rule; each retired door refused naming `role rules` | ONEWAY1's wave 2 |
| **CONTEXTDRIVE1g** the editors (web, modules; d, f) | §7.1-§7.3 | page tests en and zh; stories at 80, 39 and 25 rem, both themes; keyboard order; `RolePolicy.test.tsx` on rules; `HelpCoverageTests`' rows for each new hook | ONEWAY1's wave 2 |
| **CONTEXTDRIVE1k** the room and the words (help, web locales, service; d, g) | §7.6, §7.7 | `HelpRoomRolesTests` and the room's goldens; the doors table's `role rules`; glossary *supervisor rule* and *count*; `names:check --strict`; parity | ONEWAY1's wave 2 |
| **CONTEXTDRIVE1j** `refer` (driver, modules, web; SUPERVISOR1a-iii, SUPERVISOR1c, i) | §4.6 point 3, §5's `course` | a referred point decided `go-on`, overruled inside the window, and deferred; none appointed and dormant both waiting, said; the actor's attempt counted | with the supervisor's activation |
| **CONTEXTDRIVE1l** the kept reads deleted (driver, modules; the `rules` part's census ended on the install) | §6.3 | the build with the four reads gone; an unconverted fixture home holding its starts with the conversion as its door | ONEWAY1's wave 6 |
| **CONTEXTDRIVE1m** the installed proof (the steward with the owner; h, f, g) | §6.2, §7 | the owner's press converting the install; a line waiting on the answers-alone rule and gone on; a rule changed in the window and at the terminal; dated shots at 1546 and 680 px, en and zh | last |

**What can be built on main now, beside the open waves.** `a`, `b`, `c`, `i`, then `d` and `e`: their files are `Course/Domain/`,
`Course/GoOn/`, `Course/Chat/`, `Context/`, `Roles/`, `Store/`, `WorkOrders/StaffCourse.cs`, `WorkOrders/Domain/Planner.cs`,
`Agents/TransientRetry.cs`, `Loop/Driver.cs` outside the nine lines the workflow wave adds, the modules'
`Bridge/Routes/DriverRoutes.cs` and the service's role tool, none of which `wave/workflowclean1` changes; the room's doors table,
which it does change, waits for `k`. Before the `rules` part, `e` acts only on a home whose rules are seeded (a fresh one), every
other home keeping its old readers, registered. `h` waits for the wave (the web's `work/attention/`, the modules'
`Loop/AttentionStateSlice.cs` and `Loop/NeedsAPerson.cs` are its files); `f`,
`g` and `k` wait for ONEWAY1ae; `j` waits for the supervisor's activation.

**COMMS1g takes it up.** COMMS1's wave 1 builds the answers-alone admission (its §5.3), the durable source of `a`'s
`answers-alone` count and a waited quest's result as a cause, and builds no rule, no hold of its own and no supervisor's reading:
those are `c`, `e` and `i`. **STAFFPOLICY1's conditions take it up.** Its moves and `most` are rules (§4.8) from `e` and `f`, and
the open parts of STAFFPOLICY1e3 are built here: a conversation's transient move is a `tries` rule's (`e`), the hold in the waits'
union and the head is `h`'s, and a real quiet tick is `e`'s proof.

## 10. Questions for the owner

1. **Seed the answers-alone rule on every home?** Recommended: yes, *wait for you after 2 answers alone in a row*, shown on the
   supervisor's row and removable with one press. Yes keeps every line as it runs today; no leaves a fresh home's lines unheld on
   that count, as D225's note reads, and the conversion offers the rule unticked.
2. **Keep the cap as a setting?** Recommended: yes. It bounds how many sessions the machine runs at once, which no line shows, and
   the person already sets it. Yes keeps `SET_CAP` and Settings as they are; no would make it a rule over a machine count, a second
   kind of rule that holds every line at once.

## 11. Rejected

- **A table of counts.** A stored total beside the record it counts is a second way to count it and drifts from it.
- **A feed to the session.** The brief, a go-on's appendix and `line_read` carry the counts already.
- **Rules inside each role's policy record.** A place's rule spans roles; one record in two stores.
- **Setting a rule through *Appoint* alone.** It grants nothing, and D226's moves are set through the role door already.
- **A model judging every go-on.** A rule asks the agent only where the person wrote `refer`.
- **A rule that ends a running or quiet session**, against D200 and D205 point 5; a rule over a role's day, which is the budget's.
- **An unknown count read as zero or as the bound.** Zero lets a line run past a rule; the bound holds it for Daoris's own outage.
- **A notification for a rule's act**, as D226 refused one for a move.
- **The cap, the channel's bounds and Daoris's carry-ons as rules** (§1.2).

## 12. The reads

Owed under D191: an Opus read and the other maker's, each kept beside this design, each finding settled in a section of its own
before the decision is accepted.
