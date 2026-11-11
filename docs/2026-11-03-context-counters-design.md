# CONTEXTDRIVE1: the design. A line's behaviors counted in its own context, and a supervisor, an appointed agent or the person's rules, acting on them

Status: proposed 2026-11-03 (D229 reserved for it), for its reads under D191; revised the same day after its Opus read, whose findings
§12 settles, with the owner's two answers in §10. **Accepted 2026-11-03** by the steward after the Opus reader's second read, whose
two new findings (a preset's moves on a reached home, §6.4; the next start's walk as an executor, §4.5) and per-role inheritance (§4.4)
are written in and row e proves them. The other maker's read was taken as an Opus read (`2026-11-06-context-drive-read.md`), and
parts of this text are superseded: §6.2-§6.4 (the conversion door's `rules` part, reached homes, `convert-first`, the census and its
kept reads) are D232's migration step and row l's deletions; §6.1's policy moves are passed over; one seed serves a fresh home and the
migration; a count takes one rule per place and roles, so §4.5's ladder and its decide are gone; §2.2's shown counts that no reader asks
for (`tokens`, `minutes`, `quests-handed`, `results-waited`, `refusals.*`, `over-budget`, `posts`, `starts`) are not read; and the
backlog's letters differ from §9's: its installed proof is §9's m. It answers `archive/2026-11-03-context-counters-brief.md`, whose §2 binds every section:
each piece joins a structure that exists, and none runs beside one. Each claim about the code was read at `6bcd5cce9`. Driver paths
start at `src/Daoris.Desktop/Daoris.Desktop.Driver/`, the modules' at `src/Daoris.Desktop/Daoris.Desktop.Modules/` (written `Mod/`),
the web's at `src/Daoris.Web/src/`, the service's at `src/Daoris.Service/Daoris.Service.Core/`.
Read for it: D188, D200, D205, D208 and the supervisor design §2-§6, D210, D212's course decision, D225 with its note of 2026-11-03
and the COMMS1 design §5.3, §10, §11, §13, D226 and the staff-policy design, D228 and the ONEWAY1 design §1-§4 and §10, D189 and the
session-context design §3, the foundation design §2.5 and §2.7, and `.claude/knowledge/compatible-builds.md`.

## 0. In short

- **The census** (§1). Twelve behaviors are counted or bounded today, each by its own reader: four have a number that holds or
  moves a line (answers alone, failed sessions, transient tries, moves a day), the rest are counted by the policy's conditions or
  counted nowhere. Machine load, a shared host's protocol and Daoris's own nudges are bounded too; they are not a line's behavior and
  stay as they are.
- **The counts** (§2). One shape, one pure read, `LineCounts.Of`, each count read from the one record that already carries it, and
  one line key, `LineKeys.Of`, which the moves, the counts and COMMS1g's admission all read. Nothing new stores a number. A count is
  *exact*, *at least* or *unknown*, with why.
- **The session sees them** (§3) in its brief and in a go-on's words, in Daoris's words, as facts. `line_read` gains none.
- **The rule supervisor** (§4). A rule is *when this count reaches n here, do this*: the actions are the doors that exist, each hold
  released by the door it has today. It is a holder kind of D208's supervisor role, kept in the desktop store, set through the role
  door, inherited by place, and read at the executor that acts on its count today, inside D212's one course decision with no step
  added. It grants nothing, so it is set as D226's policy is, not appointed. STAFFPOLICY1's applied moves and `most` become rules.
- **When an agent and a rule both apply** (§4.6), the rule is read first and its act stands; an appointed agent is asked only where a
  rule says `refer`, once per point, and its answer is refused where a rule would hold the line.
- **Defaults** (§6), the owner's answers (§10): today's numbers are seeded as rules on every home, so nothing changes until the person
  changes them, and the cap stays a setting of the machine. An existing home converts through a `rules` part of ONEWAY1's conversion
  door; on a converted or seeded home the old doors are refused in the row that makes the rules decide.
- **Rows** (§9): the counts and the rules' record on main now; the brief and the agent's reading after COMMS1's wave 1; the door, the
  deciding, the part, the editors, the holds and the words as one wave in ONEWAY1's wave 2; a conversation's course after its runner
  moves; `refer` with the supervisor's activation; the deletions with ONEWAY1's wave 6.

## 1. The census

### 1.1 Counted, and acted on

| Behavior | What it counts | Kept in | Reset by | What acts on it today | Becomes |
|---|---|---|---|---|---|
| **Answers alone** | Go-ons whose prompt took only solicited answers (a helper's, an opinion's findings, the supervisor's), in a row on a line | Nowhere: read again from the line's event files each time (`Course/Domain/AnswersAlone.cs:45-90`), its parts walked by `from` (`Course/GoOn/AnswersAloneHold.cs:97-123`), so a trimmed log resets it | The person's word, Daoris's own word, the person's *Go on with this answer* (`AnswersAlone.cs:100-103`) | `Bound = 2` (`:21`). The hold is `AnswersAloneHold.Holds` (`Course/GoOn/AnswersAloneHold.cs:27-46`), which marks the answers `answers-alone` and says it once; it runs in the look's `alone` stage (`Loop/Driver.cs:1153-1162`), an ended conversation's go-on (`Course/Chat/ChatGoOn.cs:619-623`) and an open conversation's helper answer (`Course/Chat/ChatHelperWords.cs:99-102`). Every reader leaves a marked record waiting (§4.5). `CourseDecision.AloneHeld` (`Course/Domain/LineCourse.Decide.cs:210-222`) only tells the hold the mark made. The supervisor's route refuses `supervisor-bound` at the keeping and the release (`Mod/Bridge/SupervisorDoor.cs:305-306`, `Roles/QuestionSupervisorDecisions.cs:175`, `:284-286`) | The count `answers-alone`, its durable source COMMS1g's admission; a waited quest's result a cause (COMMS1 §11); the bound a seeded rule judged inside the admission |
| **Failed sessions** (the code's strikes) | This machine's sessions on a quest that failed or were cut off, never a limit, a refused account, a transient end, a model not offered, an update's close or a teammate's | The service's session records, all time (`Service/ServiceClient.cs:2433-2458`), into `Snapshot.Strikes` (`WorkOrders/SnapshotReads.cs:40`) | *Try again*, which marks `forgiven` at the records' count (`Settings/DriverConfig.cs:52-53`, `:86-97`); the person's words go on past it (`WorkOrders/Domain/Planner.cs:1360-1362`) | `strikes` in `driver.json`, 3, 0 never (`Settings/DriverConfig.cs:38-45`), written on every save (`:1116`), set by `SET_STRIKES` (`Mod/Bridge/Routes/DriverRoutes.cs:272`) and `driver strikes` (`Settings/DriverChoicesCommand.cs:81`, `:460-465`); the planner parks the quest (`Exhausted`, `Planner.cs:1357-1372`), row `parked-quest` (D58) | The count `failures`; the home's number a seeded rule |
| **Failures in a row on one choice** | The trailing failures of a driven line on one agent and asked model | The service's records (`Service/ServiceClient.cs:2466-2494`), `Snapshot.FailedRuns` (`Planner.cs:213`), by quest | Any other end, or another choice | D226's `failed` move with its `after` (`WorkOrders/StaffCourse.cs:117-141`) | The count `failures-on-choice`, a driven line's; the move a rule |
| **Moves to another choice** | A line's moves, less the person's approvals | `role_moves`, store step 17, by the quest's id (`Roles/StaffMoves.cs:15-67`) | 24 hours; a yes releases one (`:36-38`, `:54-67`) | `most`, 3 once any move is set, 1 to 8 (`Roles/StaffPolicy.cs:163-167`, `:297`); the next move becomes an ask (`Roles/StaffJudge.cs:239-241`), held `moves-bound` (`LineCourse.Decide.cs:58-62`) | The count `moves`; `most` a rule |
| **Transient tries** | D210's episode: a try after 1, 2, 4, 8 and 16 minutes | The run's memory (`Agents/TransientRetry.cs:24-38`); the end past it noted `ended.transient` with its count and minutes (`Events/NoteCodes.cs:189`) | A prompt that answers | A driven run ends past `Limit`, D226's `after` (`TransientRetry.cs:34-38`, set at `WorkOrders/StaffCourse.cs:14`); the line holds `TransientBound`, reached by the planner's `TransientHolding` (`Planner.cs:878-887`) into `CourseDecision.PastBound` (`LineCourse.Decide.cs:452-458`) and by a go-on's check (`:123-126`), no strike (`ServiceClient.cs:2446-2448`). A conversation keeps five and its turn ends `error` for the person's next word (`Loop/Chat/ChatRunner.cs:3726-3738`) | The count `tries`; the hold a seeded rule that cannot be removed (§4.5); the schedule stays D210's |
| **Quiet** | A running session unheard and not busy | The watch, marking at Q, ten minutes, and bringing it to the person at three times Q (`Events/SessionWatch.cs:89-90`, `:141`); a run it ended noted `ended.quiet` (`Events/NoteCodes.cs:278`) | Heard again | Row `quiet`; nothing ends but a dead process and a headless `--quiet-end` (D200); D226's `quiet` move once the run has ended (`StaffCourse.cs:99`) | The count `quiet`, whether this part's end was quiet; Q stays the watch's (§1.2) |
| **Limits and cooled accounts** | An account's limit read from its door's failure (`Agents/AccountLimits.cs:72-82`) | The account's own records (D197), the walk's `cooling` pass (`Roles/StartPlan.cs:26`), a curve move held 30 minutes (`Agents/Domain/SwitchDecision.cs:68`) | The account's reset | The walk (D206, one attempt per cool-off); D226's `limit` move (`StaffCourse.cs:178-220`) | The count `limits`, whether this part's end was a limit; the cool-offs stay the account's |
| **Refusals** | A model refused at the open, a sign-in refused, each moved once inside one start | `role_moves` rows for the in-start moves (`Loop/Driver.cs:941-950`, `:3042-3046`, `:4512`); a harness's denied tool call is an event, counted nowhere | The next start | The walk, which moves on by the role's rule (`Roles/StaffPolicyDoor.cs:20-31`: the walk's, no field) | The counts `refusals.model`, `refusals.sign-in`, shown; the act stays the walk's (§1.2) |
| **Tokens** | Turns' reported input and output, by binding and choice | The turn lines (`Agents/UsageTurns.cs:19-36`); the local day summed by choice (`Roles/StaffLedger.cs:418-433`), an unreported turn holding its choice | The day | D226's `budget.tokens` passes a choice at a start (`Roles/StaffJudge.cs:160`); the accounts' curves (D179) | The count `tokens`, a part's and a line's, shown and handed, ruled by no rule (§4.1); the role's day stays the budget's |
| **Minutes** | A part's and a line's working time | The records' times and the turns' | None | Nothing for a session (D200; `timeoutMinutes` kept only to be said, `Settings/DriverConfig.cs:55-61`); an ask's own minutes bound its run (`Participants/Domain/HelperInstruction.cs:34`, `Participants/Domain/PartnerInstruction.cs:28`, the service's `Review/Opinions.cs:274`, at most 120) | The count `minutes`, shown and handed, ruled by no rule; an ask's minutes stay its asker's |
| **Quests handed, results waited** | Quests a line's sessions published; a quest result the line went on by (D79's resume, a new run of the quest with no `from`) | The service's quests; the records' `awaits` | None | Nothing; the result counts toward answers alone (COMMS1 §11) | The counts `quests-handed`, `results-waited`, shown and handed |
| **Hand-overs** | A line's parts by why they began | `from_why` (`Course/Domain/CourseAnswers.cs:235`), the hand-over box (`Course/Domain/HandOverRecords.cs:33`, store step 18) | None | The hand-over rule's consent for an agent's own ask, per place (`Course/Domain/HandOverRules.cs`), which is no count | The count `hand-overs`, shown and handed |

### 1.2 Bounded, but not a line's behavior: they stay

| Bound | Where | Why it stays |
|---|---|---|
| The cap, sessions running at once | `Settings/DriverConfig.cs:32`; `SET_CAP` (`Mod/Bridge/Routes/DriverRoutes.cs:321`); `Planner.cs:1498` | It bounds the machine's load, which no line shows, and it is the person's setting already. The owner's answer (§10, question 2) |
| Helpers at once, two per asker, a lead's open pieces, process starts, claims at once | `Loop/DriverHelpers.cs:457`, `Participants/Domain/HelperJudge.cs:54`, `Participants/Domain/PartnerAdmission.cs:34`, `Platform/ProcessStarts.cs:51`, `Loop/Driver.cs:2027` | Concurrency, a slot waited for, never a line held |
| A part's channel posts, ten, three urgent | The service's `Asks/Channel.cs:15`, `:24` (`ChannelBounds`) | The shared host keeps it and reads no machine-local rule (D208 point 1: *this machine alone*). Shown as the count `posts`, never ruled |
| Daoris's carry-ons for an uncommitted tree, two, and one after a go-on | `Agents/TurnCarry.cs:28`, `Loop/Driver.cs:4255` | Daoris's own words, not the session's behavior |
| The walk's passes: a choice moved off held 30 minutes, the weighted window of 20, budgets, shares, a refused model or sign-in walked on | `Roles/StaffJudge.cs:119-122`, `:157-168`; D226 | Staffing: which choice a start takes, not what a line does. A budget's and a refusal's move are the walk's own (`Roles/StaffPolicyDoor.cs:20-31`) |
| The quiet window Q | `Events/SessionWatch.cs:89-90` | How quiet is observed (D200), not a limit on it |
| Sizes and shapes: the brief's caps, a chain's five steps | `Context/Domain/ContextBrief.cs:94-139`, the service's `Quests/QuestExchange.cs:537` | Bounds of a record |

### 1.3 Read against the brief

- The answers-alone bound is D188 **point 4** (`docs/decisions/D188.md:43`, the code's own summary at `AnswersAlone.cs:4`, D225's note
  and COMMS1 §5.3). The brief calls it point 5, which is the delivery rule. D229 amends point 4.
- The cap and the quiet window are named among the caps to convert. The census keeps both (§1.2): the cap is a setting of the
  machine's load, as the owner chose, and D200 already decided that no quiet run is ended by Daoris.

## 2. The counts

### 2.1 One shape

```json
{ "count": "answers-alone", "of": "line", "line": "412", "window": "in-a-row", "n": 2, "known": "exact",
  "since": "2026-11-03T09:12:00Z", "from": "admission" }
```

`count` is a code of one closed vocabulary (§2.2); `of` is `session`, `line`, `quest` or `role`; `line` is the line's key (§2.3);
`window` is the one the count offers; `since` is its reset or its window's start; `from` names its source; `known` is `exact`,
`at-least` with `why`, or `unknown` with `why` and no `n`.

### 2.2 One read, each count from its one source

`LineCounts.Of` (`Course/Domain/LineCounts.cs`) is pure: it takes what `LineCountsRead` (`Course/LineCountsRead.cs`) read once
for the line and answers every count. Its readers are the executors (§4.5), the brief (§3), the head and *Details* (§7.4), the
supervisor's print (§5) and Ask Daoris's room (§7.6). No count is a stored number: a total kept beside the record it counts would
be a second way to count it, and would drift from it.

| Count | Of | Window | Read at (its point) | Its one source | Reset | Ruled |
|---|---|---|---|---|---|---|
| `answers-alone` | line | in a row | a go-on by answers alone | COMMS1g's admission rows; until that row, `AnswersAlone.Count` over the events | the person's word, Daoris's fact word, *Go on with this answer* | yes |
| `failures` | quest | since *Try again* | a quest's next start | the service's records less `forgiven` | *Try again*; the person's words | yes |
| `failures-on-choice` | driven line | in a row, one choice | a part's end | the service's records, by quest | another end or choice | yes |
| `tries` | session | the episode | the run's start (its limit), the episode's end | the run's episode; `ended.transient`'s `count` | a prompt that answers | yes |
| `quiet` | line | this end | a part's end | `ended.quiet` on the part's record | the next part's end | yes |
| `limits` | line | this end | a part's end | the record's limit flag | the next part's end | yes |
| `moves` | line | 24 hours | a move | `role_moves` | the window; a yes releases one | yes |
| `refusals.model`, `refusals.sign-in` | line | the start | the start | `role_moves`' in-start rows | the next start | no: the walk's |
| `over-budget` | line | the start | the walk's pass (`Roles/StaffJudge.cs:160`) | the pass's code in the start's walk | the next start | no: the walk's |
| `tokens` | session, line | the part; the line | a go-on, a start | the turn lines | none | no |
| `minutes` | session, line | the part; the line | a go-on, a start | the records' and turns' times | none | no |
| `hand-overs` | line | the line | a hand-over's admission | the parts' `from_why` | none | no |
| `quests-handed` | line | the line | a go-on | the service's quests by publishing session | none | no |
| `results-waited` | line | the line | a go-on, as a cause of `answers-alone` | the records' `awaits` | none | no |
| `posts` | session | the part | shown only | the channel's rows | none | no |
| `starts`, `tokens.day` | role, by choice | the local day | a start, the budget's | `role_starts`, the turn lines | the day | no: the budget's |

A count that never resets takes no rule: a hold on it would hold every go-on after its release, and a release record of its own
would be a fifth way beside the four that exist (§4.2). Those counts are shown, handed to the session and read by an appointed
agent (§5).

### 2.3 One line key, durable across a hand-over and a restart

Three keys name a line today, and they disagree:
- `role_moves` by the quest's id (`WorkOrders/StaffCourse.cs:116`, `Loop/Driver.cs:928`; every writer hands a quest's id,
  `Loop/Driver.cs:3042-3046`, `:4512`, `StaffCourse.cs:133-135`, `:197`);
- the answers-alone walk by each record's `from`, at most `ContextBriefs.Depth` (50) parts (`Course/GoOn/AnswersAloneHold.cs:97-105`);
- COMMS1 §13 point 8's admission, by the service's walk with its quest hop (`Service.Core/Sessions/Lines.cs:41-63`).

They part on D79's resume, a new run of the quest with no `from` (`docs/decisions/D188.md:369-372`): the `from` walk starts the count
again there, the quest hop does not. **`LineKeys.Of`** (`Course/Domain/LineKeys.cs`, row a) is the one key, pure over the service's
records every executor already reads (`ServiceClient.ReadRecords`):
- it walks back from the part by `from` over this machine's records, at most `Lines.Bound` (256) hops, the service walk's own bound;
  a teammate's record or a part not this machine's ends the walk before it;
- the key is the quest's id where any part reached is a driven run of a quest (the oldest such part's quest where two are reached),
  since every run of that quest here is on the line, as the service's quest hop joins them; else the first part's session id;
- a walk that meets a cycle or the bound keys the line by the part itself, and its line counts read *at least* (§2.4).

It re-keys nothing stored: `role_moves` holds quest ids alone today, since a conversation writes no move yet (D226's interim
refusal, ONEWAY1 §4 M5). COMMS1g keys its admission by it, the same line the service's walk reaches; D229 restates COMMS1 §13 point
8's key so. D79's resume is on its quest's line, so its count carries, as COMMS1 §11 settled that a waited quest's result counts.
`ContextBriefs.Depth` stays the brief's bound and the legacy count's, which COMMS1g keeps to seed its admissions.

Every source in §2.2 is durable: the desktop store, the service's records, the session's event files and the turn lines. A
hand-over keeps the key, so it carries every line count to the new part, and a restart reads every count again equal. The one
in-memory source is a running episode's tries, whose end is written with its count before the part ends. Counts are read on this
machine alone, a teammate's record left out as the strikes leave it (`ServiceClient.cs:2441`; D178: sessions never move between
machines).

### 2.4 *At least* and *unknown*

- **At least** where history before a point is missing: a trimmed event log (D195's retention), a walk cut by a cycle or the bound,
  a store before the step that keeps the source, a turn that reported no tokens (*not reported*, never zero). The count is a lower
  bound, said with what is missing.
- **`answers-alone` is never unknown** (COMMS1 §13 point 8 withdrew it): its admission answers, and a line with no admission is
  seeded from the legacy count, which counts zero where no events read (D188's steward choice 2, `AnswersAloneHold.cs:16-17`). A seed
  read over a trimmed or cut history is *at least*, with why.
- **Unknown** where the source does not answer: the service down for `failures`, `failures-on-choice` and `quests-handed`, a store
  that does not read for `moves`.
- **A rule acts on an exact or at-least count** that reaches its `at`, and **never on an unknown one**: the line's head and *Details*
  say *cannot count: the service did not answer*. A failure of Daoris's own store or service already holds the next start by D226's
  fixed rule (the staff-policy design §4.1), so an unknown count never lets a start through that a known one would hold.
- **A count a line does not have is absent**, never unknown: `failures-on-choice` is a driven line's, since no record counts a
  conversation's failures by choice, so a conversation's brief, head and print leave it out, and a rule on it watches driven lines,
  said beside the rule in the editor (§7.1).

### 2.5 Whose counts

A **session's** (one part): `tries`, `tokens`, `minutes`, `posts`. A **line's** (across its parts): `answers-alone`,
`failures-on-choice`, `moves`, `limits`, `quiet`, `refusals`, `over-budget`, `hand-overs`, `quests-handed`, `results-waited`, and the
line's `tokens` and `minutes`. A **quest's**: `failures`. A **role's**: `starts` and `tokens.day` by choice, which a policy's budget
reads (D226) and the room shows; no rule watches a role's count, since that is the budget's job and one job has one way.

## 3. What the session sees

1. **The brief** (`Context/Domain/ContextBrief.cs`, D189 point 4, the session-context design §3.1) gains an item, *The line's
   course, as Daoris counts it*, under its own cap of 1,000 characters inside the 40,000: one line per count that is not zero or that
   a rule watches, with the rule as a fact. *Answers alone in a row: 2. The person's rule for this repository: Daoris waits for them
   after 2.* *Failed sessions on this quest: 1 of 3.* *This part: 34 minutes, 118,000 tokens reported.* An at-least or unknown count
   says so. It is accounted in the instruction's `InstructionAccount` with a cut code of its own, as every section is.
2. **A go-on's words.** The resume's appendix (`Continuations.AfterWords`, `Course/Domain/Continuation.cs:166-168`, composed at
   `Loop/DriverContinuations.cs:399-400` and `Course/Chat/ChatGoOn.cs:785`) gains one line where a count changed since the part's last
   go-on or a rule is one short of acting: *Daoris's count for this line: 2 go-ons by answers alone in a row; the next waits for the
   person.* A start carries the brief's item where any count is not zero (a quest that failed here before).
3. **`line_read` gains no counts.** It is the service's reader of this machine's event files (`Daoris.Service.Mcp/LineTools.cs:15-38`),
   and the service never opens the desktop store, where the rules, the moves and the admissions are kept. The counts reach a session
   through the two doors that already hand it Daoris's words, its start's brief and a go-on's words, which are also the only points
   at which a rule reads them: a rule never acts mid-turn (§4.9), so nothing a session could learn mid-turn changes what Daoris does.
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
| `count` | the behavior watched | the ruled counts of §2.2: `answers-alone`, `failures`, `failures-on-choice`, `tries`, `quiet`, `limits`, `moves` |
| `window` | which of the count's windows | the one it offers |
| `at` | the threshold: the rule acts at the count's point once the count stands at `at` | `answers-alone` 1-20, `failures` 1-20, `failures-on-choice` 1 to the failures rule's less one, `tries` 1-5, `moves` 1-8, `quiet` and `limits` 1 |
| `do` | the action (§4.2) | only those its count allows |
| `upTo` | for `wait`, the most minutes it waits before the role's rule walks on | 5-600, as D226's |
| `place` | where it watches: `machine`, `workspace:<id>`, `repository:<id>` or `line:<key>` | a place Daoris drives; a line on this machine |
| `roles` | the working roles whose lines it watches; absent is every role | ids the catalogue holds |

A place's rules are written whole, with the place's revision, as a role's policy is (D226); a press against a moved revision is
refused.

### 4.2 Its actions: the doors that exist

| `do` | The window says | What happens | Counts it allows |
|---|---|---|---|
| `hold` | *Wait for you* | The line does not go on; it waits on *What needs you* (§7.5) until its count's own release door (below) | `answers-alone`, `failures`, `tries` |
| `ask` | *Ask before the next* | Held, offering *Go on now with {next}* (D206 point 7); one yes releases one move | `tries`, `failures-on-choice`, `limits`, `moves` |
| `wait` | *Wait for its reset* | Waits for the choice's moment back, at most `upTo`, then walks by the role's rule | `limits` |
| `next`, `weighted` | *Go to the next choice*, *… by weight* | A move to another choice: a new part handed D189's brief at a part's end, counted in `moves` | `tries`, `failures-on-choice`, `quiet`, `limits` |
| `refer` | *Ask the supervisor* | The appointed agent decides (§4.6); with none appointed, held for the person behind the count's release door below (`quiet`'s, `limits`' and `failures-on-choice`'s an `ask`'s), said | every ruled count; offered once SUPERVISOR1c is |
| `off` | *None here* | This place takes no rule on this count, so an inherited one does not apply | every ruled count but `tries` (§4.5) |

**Each hold is released by the door it has today, and kept where it is kept today.** A rule adds no release:

| Count held | Its release door | Where the release is kept | What the count does after it |
|---|---|---|---|
| `answers-alone` | *Go on with this answer* (`SESSION_ANSWER_ALONE`, `AnswersAloneWord.GoOn`, `Course/GoOn/AnswersAloneHold.cs:159-165`), or a word of the person's; *Not now* keeps the answers for one (`:172-178`) | the note `answers.passed` (or `answers.kept`) in the part's events, read by COMMS1g's admission as the person's act | resets |
| `failures` | *Try again* (`RETRY`, `daoris driver retry`, RETRY1b), or the person's words to its session (`Planner.cs:1360-1362`) | `forgiven` in `driver.json` (`Settings/DriverConfig.cs:52-53`, `:86-97`) | counts from the mark |
| `tries` | *Try again* (AGENTERRORS1a2), or a word of the person's said after the end (`LineCourse.Decide.cs:452-458`) | `transientReleased` in `driver.json` (`Settings/DriverConfig.cs:140-163`), by quest, session and end | the released end goes on, the next end held again |
| an `ask` on any count, the moves bound | *Try again* or *Go on now with {next}* (`Mod/Bridge/Routes/DriverRoutes.cs:697`, `WorkOrders/RetryVerb.cs:113`) | an `approved` row in `role_moves` (`Roles/StaffMoves.cs:36-38`) | one move released |

The brief's *hand over* is `next` or `weighted`: every move to another choice is a new part handed the brief. Its *stop* is `hold`: a
rule acts only between turns, where not going on and stopping are one state, and the person's *Stop* stays the person's. Its *go on*
is the default course, which needs no rule; D226's `retry`, which is the default course on a failure or a quiet end, is therefore no
rule's action. ONEWAY1j's press is no release of a hold: it is a go-on through a hand-over into a new part, for words that could not be
taken (the ONEWAY1 design §3 C5).

### 4.3 A holder of the supervisor role

D208 §2.2's table gains a row:

| Holder | Who | Its acts reach Daoris by | What it may do | Offered |
|---|---|---|---|---|
| `rule` | The person's rules for the place (§4.4) | Daoris's own executors (§4.5), never the pipe nor a connector | §4.2's actions at its count's point; never an answer to a question, since a rule reads no meaning (D24, D157) | From CONTEXTDRIVE1e, on no flag of D208's |

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
  The step adds two tables and leaves the writer stamp at 3 (`Store/DesktopStore.cs:65-74`), as COMMS1g's step does (COMMS1 §13
  point 8). A raise would refuse only an older build's writes, never its driving (`.claude/knowledge/compatible-builds.md:51`): an
  older build drives by the numbers compiled into it whatever the rules say. What keeps an older build off a converted home is the
  conversion's rollback refusal (ONEWAY1 §1.1, §1.3's floor); a fresh home rolled back drives by the numbers it was seeded with and
  does not read a rule the person changed since. **Order**: CONTEXTDRIVE1c takes step 19 on main; COMMS1g, whose wave merges main
  first, takes step 20. The machine's revision row is the mark that this home's rules are **reached** (§6). Machine-local, never on
  a sync wire (D208 point 1).
- **Set** through `SET_ROLE { id: "supervisor", place, rules, rulesRevision }` and `SET_ROLES`, judged by `SupervisorRules.Judge`
  before anything is written, in one transaction; by `daoris role rules …` (§7.6); proposed by `role_propose`'s `rules`.
- **Inherited by place** as D205's scopes are, with no layering, as D226 refused it: for each count **and each role**, a line's
  rules come from the most specific place holding a rule on that count that applies to the line's role (one naming it, or naming
  none), and that place's rules replace every less specific place's. A repository holding only a Build rule on a count therefore
  leaves a Chat line there under the workspace's or the machine's rule, never under none. Within one place, rules naming the line's
  role replace those naming none. `off` ends the
  inheritance for that count. Several rules on one count at one place make a ladder (*at 2, ask; at 4, hold*); the highest `at`
  reached acts.
- A line's own rules (`line:<key>`) are removed with the line's records by the retention pass (D195).

### 4.5 The judge, and where it decides

`Roles/SupervisorRules.cs` holds the record, `Judge`, `Effective` (§4.4) and `Decide`, pure: given the counts and the effective rules
at a point, the act or none. Where two rules on different counts act at one point, the narrowest wins: `hold`, then `ask`, then
`refer`, then `wait`, then a move.

`Judge`'s refusals are a closed set: `count`, `window`, `at`, `do`, `upTo`, `place`, `roles`, `moves-unbounded`, `tries-unbounded`,
`refer-not-offered`, `revision`, `convert-first`. The two bounds are judged **on every line's effective set**, not at the rule's own
place: on each write, for every place holding a rule and for every role in the catalogue and for none, `Effective` is read as it
would stand, and the write is refused where a set holds a move rule and no `moves` rule (`moves-unbounded`, D226's bound against a
line moving back and forth), or holds no `tries` rule (`tries-unbounded`: a transient end strikes nothing, `ServiceClient.cs:2446-2448`,
so a line with no hold past the episode would start again without end). So a wider place's write that would leave a narrower
place's move unbounded is refused, naming the narrower place.

**It decides at the executor that acts on its count today**, each handed the effective rule where it reads a fixed number or the
role policy's move now. D212's one course decision gains no step and keeps its order: `NeverOf`, `Reserved`, `Policied`, `Course`
(`LineCourse.Decide.cs:21`); `Policied` still answers only automatic triggers (`:52-55`), and `AloneHeld` still runs inside `GoOn`
after the transient-bound hold (`:117`, `:123-128`).

| Count | Its executor | What the rule sets there | Its hold, and who honours it |
|---|---|---|---|
| `answers-alone` | `AnswersAloneHold.Holds` (`Course/GoOn/AnswersAloneHold.cs:27-46`), called by the look's `alone` stage (`Loop/Driver.cs:1153-1162`), an ended conversation's go-on (`Course/Chat/ChatGoOn.cs:619`) and an open conversation's helper answer (`Course/Chat/ChatHelperWords.cs:99`). From COMMS1g it reserves the go-on inside COMMS1g's serialized admission, handing it the `at`, and the admission refuses in the same transaction a reservation that would pass it, so two answers queued together cannot both pass; until COMMS1g, the three paths' own serialization stands (`:11-14`) | the `at` compared, today `AnswersAlone.Bound` (`Course/Domain/AnswersAlone.cs:21`, `:35`, `:42`) | the mark `answers-alone` and its note, unchanged; honoured by the planner (`WorkOrders/Domain/Planner.cs:828-830`, `:981-982`), `WorkOrders/SessionGroups.cs:636`, `:1029`, `Course/Chat/LimitWaits.cs:167`, `:358`, `Course/Chat/ChatHelperWords.cs:88` and `Mod/Loop/AttentionStateSlice.cs:485` |
| — the supervisor's answer | the keeping (`Mod/Bridge/SupervisorDoor.cs:305-306` into `Roles/QuestionSupervisorDecisions.cs:175`) and the release (`:284-286`, through `Loop/Driver.cs:1147-1148`) | the same `at`, and every other rule at the point (§4.6) | `supervisor-bound`, `rule-holds` |
| — the course's telling | `CourseDecision.AloneHeld` (`LineCourse.Decide.cs:210-222`), handed `CourseAlone` after the hold by the conversation's two executors (`ChatGoOn.cs:119-136`, `ChatHelperWords.cs:106-113`) | `CourseAlone.Bound` (`Course/Domain/CourseFacts.cs:192-195`) | none: it says the hold the mark made. `DrivenCourse.GoOn` hands no `Alone` (`Course/GoOn/DrivenCourse.cs:60-65`), the driven hold being the look's mark |
| `failures` | the planner's start verdict (`Planner.cs:1357-1372`), before any condition's move (`StaffCourse.cs:105-107`) | the number `config.Strikes` is today | `Exhausted`, row `parked-quest` |
| `tries` | a driven run's start, which sets `TransientRetry.Limit` (`Agents/TransientRetry.cs:34-38`) where `StaffCourse.cs:14` sets it today | `Limit`, the lowest `at` of the effective `tries` rules | — |
| — past the episode | the planner's `TransientHolding` (`Planner.cs:878-887`) into `CourseDecision.PastBound` (`LineCourse.Decide.cs:452-458`), and a go-on's check (`:123-126`); a move through `StaffCourse.Look` (`WorkOrders/StaffCourse.cs:89-156`) into `Policied` | `hold`, `ask` or a move | `TransientBound`, its row as today |
| `failures-on-choice`, `quiet`, `limits` | `StaffCourse.Look` (`:89-156`), `LimitWait`, `LimitHolds` and `LimitMoved` (`:178-220`), into `Policied` (`LineCourse.Decide.cs:52-79`) | the `StaffAction` read from the effective rule where `StaffJudge.OnCondition` (`Roles/StaffJudge.cs:228-242`) reads the role policy's move today | `policy-ask`, `moves-bound` |
| `moves` | `StaffMoves.Decided`'s check (`StaffCourse.cs:133-135`) and `OnCondition`'s `MovesBound` (`StaffJudge.cs:239-241`) | `most` | `moves-bound` |
| — the next start's walk | `StaffJudge.Arrange` (`Roles/StaffJudge.cs:163-170`), which skips an agent marked not answering, and a choice moved off within thirty minutes, where a move on `transient`, `failed` or `quiet` stands: on a reached home it reads that from the line's effective `tries`, `failures-on-choice` and `quiet` rules whose `do` is a move, as it reads the policy's moves on a home not reached | whether those skips apply | none: a start's order, said by `chosen` |

A conversation's line reads `answers-alone` from row e and its other ruled counts from CONTEXTDRIVE1n, which moves its episode's limit
and its limit's `wait` and `ask` onto the rules (STAFFPOLICY1e3's conversation half; §9). *Not now* stays the person's mark
(`GoOnMarks.NotNow`, `LineCourse.Decide.cs:214`): no count, and no rule writes or clears it.

A rule never acts mid-turn (D205 point 5), never ends a running or quiet session (D200), never moves a pick or a handed agent
(D226), never acts on an unknown count, and never answers a question.

### 4.6 When an agent and a rule both apply

1. **The rule is read first**, at every point, with no model. An act other than `refer` stands: the agent the owner appointed acts
   within the rules the person set, as every appointed role acts within the policy (D228's latest structure).
2. **The agent's answer to a question** is refused where releasing it would make a go-on a rule holds: `supervisor-bound` stays the
   code for the answers-alone rule, since a stored code is never renamed, and `rule-holds` is new for every other count. It is
   checked at the keeping and again at the release, where `supervisor-bound` is (§4.5's table).
3. **`refer`** hands the point to the appointed agent through the supervisor's third act, *course* (§5), decided, held for the
   overrule window and released as D208 §3.4-§3.6 keep a decision. **One point is referred once**: where a question decision of the
   holder on the same line is open (held or released, not final), `refer` asks nothing new and the point waits for that decision; the
   *course* act is asked only where none is. Its release reads the rules again, as `supervisor-bound` is read again at a release
   (`Roles/QuestionSupervisorDecisions.cs:284-286`): one a rule would now hold, or whose count moved past the referred point, is
   moved stale with `rule-holds`. With no agent appointed, a dormant appointment or the actor not offered, the point is held behind its
   count's release door (§4.2) and says why: *your rule asks the supervisor; none is appointed here*.
4. **With neither**, where the person removed every rule on a count, the count is shown and nothing holds a line on it (D225's
   note), but for `tries`, which cannot be removed (§4.5).

### 4.7 Said on the line, kept in the decision record

- **On the line**, a note of code `supervisor.rule` naming the count, its value and known state, the rule, its place and the act:
  *Waits for you: this line went on by answers alone 2 times in a row. Your rule for this repository waits for you after 2.* A move
  writes what a policy's move writes today, `role_moves`, `chosen` with its condition, `actor.moved` and `opening.moved-choice`
  (`Loop/Driver.cs:923-935`), its reason naming the rule.
- **In the decision record**, beside the questions' decisions: `<home>/sessions/<session>.decisions/<decision>.json` with
  `holder: { kind: "rule", rule, revision, place }`, the counts read, the point, the act and its time, written once, atomically, with
  **no `question` and no state lines**: it is final as written. The questions' reader returns nothing for a file with no `question`
  (`Roles/QuestionSupervisorDecisions.cs:745-753`), so the start's sweep (`SupervisorQuarantine.DecideAsync`, `Loop/Driver.cs:1149-1152`)
  and an older build never read, release or settle one. The history reads it by its holder kind (`SupervisorRuleActs`, row e) for
  *Details*' Supervisor line and the supervisor's print. Like every decision file, it is never read to decide (the supervisor design
  §3.4).
- **The machine log**, `supervisor.rule` `{ line, session, rule, count, n, known, do }`.
- **No notification**: a hold is on *What needs you* (§7.5), and no move is a notification (D226).

### 4.8 STAFFPOLICY1's conditions and `most`, taken up

Every move an **applied** policy sets on a condition is a rule naming its role at the role's place: `transient after 2: weighted` is
`tries at 2: weighted`, `failed after 2: next` is `failures-on-choice at 2: next`, `limit: wait upTo 60` is `limits at 1: wait upTo 60`,
`quiet: next` is `quiet at 1: next`, and `most 3` is `moves at 3: ask`. A `retry` move is the default course and no rule; *as today*
(`StaffJudge.Today`, `Roles/StaffJudge.cs:249-255`) is no rule. A stale policy's moves, which apply to nothing today (the staff-policy
design §2.1; `StaffCourse.cs:103` reads `Applied` alone), become no rule. The policy keeps what a start chooses: its rule, weights,
shares, budget amounts and works. D226's fixed rules (the staff-policy design §4.1) stay fixed. The editor writes the `moves` rule at 3
with a role's first move rule, and the conversion writes it for a policy that sets a move and no `most` (D226's `MostByDefault`), so
the bound `moves-unbounded` judges is there.

### 4.9 What a rule never does

It grants nothing, answers no question, reads no text, starts no run Daoris would not start, ends no turn, moves no account (D197
point 8: a cool-off is the account's), and changes no other machine. Its record is in the home's ordinary reach, as `driver.json`'s
`strikes` and D226's policy are: an agent that writes the home can change a rule, as it can change those today (D156 point 7);
converting the answers-alone bound from code to a rule brings it to that same reach, said here and in the conversion's review.

## 5. The agent supervisor's reading

- **Handed.** The fenced print of `sessions supervise <id>` (`Roles/SessionsSuperviseCommand.cs`) and the actor's prompt
  (SUPERVISOR1c) gain a part, *The line's course*: every count with its known state and source, the rules in force on the line with
  their place and `at`, and the line's recent course (its parts with why each began, its last ten go-ons and who answered each, the
  rules' acts), composed by Daoris from records and fenced as Daoris's facts, never the asking agent's claims (the supervisor design
  §4.4).
- **Cited.** A reason's `source` may name a count, `count:answers-alone=2 (exact)`. The decision keeps the counts it was handed as
  it keeps the question's snapshot, and the route refuses a `count:` source that names none of them (`no-source`), as it refuses a
  reason with no source (D135 point 4).
- **Deferred.** `SUPERVISOR_DEFER` gains the bound `course`: the line's course is the owner's to judge, with the counts as reasons.
- **Referred** (§4.6): the third act, `course`, in the form of the supervisor design §3.1's acts, `{ id, line, rule, generation,
  proof, do, reasons }`, `do` one of `go-on`, `hold` and the moves the rule's count allows. It is a new act of the role, so D229 amends
  D208 point 1's list; its route follows the act door as it then stands (the foundation design §2.5's one act door).

## 6. Defaults and conversion

### 6.1 The seeded rules

Every home holds rules equal to today's numbers, at the machine's place, for every role (the owner's answer, §10):
- `answers-alone at 2: hold` (D188 point 4);
- `failures at 3: hold`, or at the home's own `strikes`, and none where it is 0 (D58);
- `tries at 5: hold` (D210 point 3), which a person may change but not remove (§4.5);
- for each role with an applied D226 policy, its moves and its `most` as §4.8 maps them.

A fresh home's first start writes them, as ONEWAY1 §2 writes each role's seed, so every home reads its rules from one place and none
from code. **Why:** nothing changes on any home until the person changes it, as D226's default changed no spending; no number is
fixed in the product, since each is a rule the person sees on the supervisor's row and changes or removes with one press (D225's
note); a fresh and a converted home read the same rules; and removing a hold is a widening the person chooses, never one an update
makes for them.

### 6.2 The `rules` part of the conversion door

A part of ONEWAY1 §1.1's door, in its contract:
- **Census**: `driver.json`'s `strikes` (absent reading 3); each `role_policy` record with `moves` or `most`; the machine's revision
  row. `Ended` once the machine's place holds a revision row, no policy holds `moves` or `most`, and `driver.json` holds no `strikes`.
  It can end because, from row e, the configuration's save writes `strikes` only where the loaded file held it or a press set it on a
  home not reached, kept as stored as `timeoutMinutes` is (`Settings/DriverConfig.cs:55-61`), never from the default it writes on every
  save today (`:1116`); and once the part removes the key, no door writes it again (§6.4).
- **Plan**: each rule written, said once (*Waits for you after 2 answers alone in a row, as today*); each seeded rule a row choice,
  ticked, an unticked one said under what widens; each applied policy's moves and `most` as rules (§4.8); a stale policy's moves and a
  `retry` move listed under *removed: they decide nothing today*; each policy re-keyed without its moves; the key removed. It applies
  after `staffing` in the door's order.
- **Effects**: the rules and the machine's revision in one store transaction; each policy written again, a new revision; the key
  removed. `forgiven`, `transientReleased` and the approvals stay where they are: they are the holds' releases (§4.2), not caps.
- **Undo** removes the rules it wrote, puts back each policy's revision and the key, and is refused where a rule or a policy has
  been written since.
- **Never touched**: the cap, the account cool-offs, the channel's bounds, the walk's passes, any repository's files.

On a home not reached, the old readers decide and the door refuses a rule with `convert-first`, naming the review.

### 6.3 The kept reads and their ends

Each is an entry in `Transitional/Register.cs` with its marker, added by row e (after the workflow wave, so no marker lands in a file
it changes), ending when the `rules` part's census reads `Ended`, deleted by `CONTEXTDRIVE1l`:

| Entry | Kept, on a home not reached |
|---|---|
| `answers-alone-bound` | `AnswersAlone.Bound` and its reads (`Course/Domain/AnswersAlone.cs:21`, `:35`, `:42`), and the conversation's `CourseAlone(AnswersAlone.Bound)` (`Course/Chat/ChatGoOn.cs:132`, `Course/Chat/ChatHelperWords.cs:112`) |
| `strikes-key` | `DriverConfig.Strikes` (`Settings/DriverConfig.cs:38-45`) and its writing (`:1116`), read by the planner (`WorkOrders/Domain/Planner.cs:1362`) and `WorkOrders/StaffCourse.cs:106-107` |
| `policy-moves` | the policy record's `moves` and `most` (`Roles/StaffPolicy.cs:158-167`), `StaffJudge.OnCondition` (`Roles/StaffJudge.cs:228-263`), their reads in `WorkOrders/StaffCourse.cs:14`, `:89-220`, and the walk's passes on them (`Roles/StaffJudge.cs:165-168`) |
| `transient-bound` | the five tries read where no rule is: `StaffJudge.Today`'s hold (`Roles/StaffJudge.cs:252`), `TransientRetry.Bound` as the run's limit (`Agents/TransientRetry.cs:28`, `:34-38`), and the hold `CourseDecision.PastBound` says without a rule (`Course/Domain/LineCourse.Decide.cs:452-458`), reached by the planner's `TransientHolding` (`Planner.cs:881-885`) and a go-on's check (`LineCourse.Decide.cs:123-126`) |

### 6.4 The old doors, and when each goes

- **On a reached home, in row e**, the row that makes the rules decide: `SET_STRIKES` (`Mod/Bridge/Routes/DriverRoutes.cs:272`),
  `driver strikes` (`Settings/DriverChoicesCommand.cs:81`, `:460-465`), and the policy door's `moves.*` and `most`
  (`Roles/StaffPolicyDoor.cs:18`) through `SET_ROLE`, `role policy --on` and `--most` (`Roles/StaffPolicyFlags.cs`) and
  `role_propose`'s policy moves, each refused naming `role rules`. No press is then written that no reader honours. On a home not
  reached they write as today, and their readers decide there.
- **A preset on a reached home.** *Spread evenly* and *Spread by weight* carry a limit move and a bound (`Roles/StaffPolicy.cs:195-211`).
  On a reached home a preset is neither refused nor stripped: the one `SET_ROLES` press writes its choices and rule into the policy
  and its limit move and bound as rules naming the role, at the place the press names, in the same transaction. The role editor,
  `role policy --preset` and `role_propose` take the same path.
- **The rules wave reaches main whole** (§9): d's door, e's deciding and refusals, f's part, g's editors, h's holds and k's words land
  in one merge, so the window's *When a choice cannot go on* fold (`MovesFold`, `agents/roles/RolePolicy.tsx:158`) writes rules on a
  reached home in the same merge that refuses its moves there (D228: a retirement and its surface in one chunk).
- **At `CONTEXTDRIVE1l`**, once the census has ended on the install: the old doors themselves, the four kept reads, their usage,
  catalogue keys and the room's lines.

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
│   [Answers alone in a row ▾]         [ 3 ▾]    [Wait for you ▾]                [Every role ▾]   here             ✕
│     replaces This Daoris's: at 2, wait for you
│   [Failed sessions on a quest ▾]     [ 3 ▾]    [Wait for you ▾]                [Every role ▾]   This Daoris      Set here instead…
│   [The service did not answer ▾]     [ 2 ▾]    [Go to the next by weight ▾]    [Build ▾]        here             ✕
│     driven lines; a conversation keeps its five tries until CONTEXTDRIVE1n
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
    reaches [3 ▾]  then [Wait for you ▾]
    for [Every role ▾] · here                      ✕
    replaces This Daoris's: at 2
  When [Failed sessions on a quest ▾]
    reaches [3 ▾]  then [Wait for you ▾]
    for [Every role ▾] · This Daoris   Set here instead…
  + Add a rule
  Always: between turns only; …
[Save] [Cancel]
```

The ⓘ says what a count counts, what resets it and which press releases its hold. **Each rule set here says what it replaces**, the
less specific place's rules on that count it shadows (§4.4), and a rule whose count is a driven line's says so. An inherited rule is
drawn under its place's name with *Set here instead…*, which copies that count's rules here. `tries` offers no ✕ at the machine and no
*None here* (§4.5). `Ask the supervisor` is offered only once the actor is (§4.6). On a home not reached, the fold shows the seeded
rules it would hold and *Convert first…*, opening the review.

### 7.2 A role's editor

`MovesFold` (`agents/roles/RolePolicy.tsx:158-193`) keeps its rows and words and, on a reached home, writes them as rules naming the
role (§4.8); its *At most [3] moves* is the `moves` rule. A rule naming the role set at another place reads there with *Edit in
Supervisor…*. On a home not reached it writes the policy's moves as today.

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

Each hold keeps the row it has today and its release door (§4.2), since a stored kind is never renamed in place and each door is
the one the person already knows: the answers-alone hold the `holding` row `ANSWER_WAITS` lists, with *Go on with this answer* and
*Not now*; the strikes' park `parked-quest`, with *Try again*; the transient bound's hold its row, with *Try again*; an `ask` and the
moves bound `policy-hold`, with *Try again* and *Go on now with {next}*. Each row a rule decided gains the rule's words and *Change the
rule…*:

```
Waits for you  #412 Build the policy store · it went on by answers alone 2 times in a row; your rule for daoris waits after 2   [Go on with this answer] [Not now] [Change the rule…]
```

Every rule's hold joins the session waits' union and the head's and foot's wait (`work/waits/waits.ts`, `WaitStrip.tsx`,
`SessionHead.tsx`), the part STAFFPOLICY1e3 left open, so one hold is counted once on the head, the foot and *What needs you*
(COMMS1j).

### 7.6 Ask Daoris and the terminal

- **Ask Daoris**: `role_propose` gains `rules` with the place and its revision, judged by the same judge, applied only by the
  person's press, never through a `settings_*` grant; the card says *Supervisor, daoris: wait for you after 2 answers alone → after
  3. Lines here hold later.* The room's Roles section says each place's rules and a line's counts, so *why is this waiting?* is
  answered from records.
- **The terminal**, in ONEWAY1's tree (`daoris driver role …` until `ONEWAY1ai`):

```
$ daoris role supervisor --place repository:daoris
supervisor  daoris (repository) · agent: not appointed · rules revision 4
  answers-alone       at 3   hold       every role   here (replaces This Daoris's at 2)
  failures            at 3   hold       every role   This Daoris
  tries               at 2   weighted   build        here (driven lines)
  moves               at 3   ask        build        here
$ daoris role rules --place repository:daoris --set answers-alone=3:hold
$ daoris role rules --place line:412 --set answers-alone=1:hold
$ daoris role rules --place repository:daoris --off failures
```

### 7.7 Words

The window says *supervisor rule* (主管规则) where it must tell it from the glossary's *rule*, a doctrine's and an access rule's;
a count is *count* (计数); a hold reads *waits for you* (等你处理, the glossary's *waiting on you*), never *hold*, which the glossary
keeps for the person's hold of a repository (暂停); failed sessions are never *strikes* on the window (the glossary's *strike*).

## 8. What changes in the decisions

D229 states each; no older decision's text is edited.
- **D188 point 4**: *at most twice in a row* becomes the seeded rule `answers-alone at 2: hold`, which the person changes, judged
  inside COMMS1g's admission.
- **D58** and DRV6's `strikes`: the number becomes the seeded `failures` rule; `driver.json`'s key and `SET_STRIKES` are refused on
  a reached home and retire at the census's end.
- **D208**: points 1-2 gain the `rule` holder, set through the role door since it grants nothing; point 4 gains the `course` act, the
  defer bound `course` and a rule's act kept with no question; point 6's one count stays one count, read through §2.
- **D210 point 3**: the hold past the episode's tries is the `tries` rule, which cannot be removed; the schedule stays.
- **D225's note of 2026-11-03**: its supervisors are §4's; COMMS1g keeps the count durably, keyed by `LineKeys.Of` (restating COMMS1
  §13 point 8's key), judging the `at` it is handed inside its admission, its store step 20; the rule and the agent's reading are this
  design's rows.
- **D226**: its applied moves and `most` become rules (§4.8); a `retry` move and a stale policy's moves become none; the policy keeps
  rule, weights, shares, budgets and works; `MostByDefault` is the companion rule the editor writes; a refusal's and a budget's move
  stay the walk's.

## 9. Rows, in waves

| Row (lanes; after) | Contract | Proof | When |
|---|---|---|---|
| **CONTEXTDRIVE1a** the counts and the line key, read (driver) | §2 | `LineKeysTests`: a driven line across a hand-over, a carry-on and D79's resume with no `from`; a conversation continued from a driven part; a conversation alone; a teammate's part; a cycle; the bound. `LineCountsTests` per count over `fixtures/line-counts.json`: equal across a hand-over and a restart; at least for a trimmed log, a cut walk and a store before its step; unknown for a service that does not answer; `failures-on-choice` absent for a conversation; a teammate's record left out; every reader unchanged | now, on main |
| **CONTEXTDRIVE1c** the rules: record, store and judge, dark (driver; a) | §4.1, §4.4, §4.5's judge | `SupervisorRulesTests` over `fixtures/supervisor-rules.json`: every refusal code; `Effective` by place, role and `off`; a ladder; the narrowest act; `moves-unbounded` and `tries-unbounded` judged on a narrower place's effective set after a wider place's write; `DesktopStoreTests`' step 19 at stamp 3 | now, on main, after a |
| **CONTEXTDRIVE1b** the session sees its course (driver; a, COMMS1's wave 1) | §3 | `ContextBriefTests` golden with the item and its cut code; a go-on's appendix golden, driven and conversation; a start's; a planted label in a count's source never reaches the text; `InstructionAccount`'s sums; `HandedCodesTests` and `handed.test.ts`; `LineToolsTests` unchanged | after COMMS1's wave 1 reaches main |
| **CONTEXTDRIVE1i** the agent supervisor's reading, dark (driver, modules; a, c, COMMS1's wave 1) | §5 but `course` | the print's golden with its fenced part; a `count:` source handed and one not, refused at the keeping (`Mod/Bridge/SupervisorDoor.cs`); the decision's counts snapshot | after COMMS1's wave 1 reaches main |
| **The rules wave**, `wave/contextdrive1`, reaching main whole | | | ONEWAY1's wave 2: after `wave/workflowclean1` reaches main, ONEWAY1ae and ONEWAY1a |
| **CONTEXTDRIVE1d** the door (driver, modules, service; c, ONEWAY1a) | §4.3, §4.4, §7.6 | `SupervisorRuleDoorTests` (a moved revision, `convert-first`, `appointed-not-configurable` for any other field); `RoleVerbTests`; `HelpRoleProposalsTests` and the service's `HelpRoleProposalTests`; `McpToolDescriptionTests`; full access's terminal refusal | the rules wave, first |
| **CONTEXTDRIVE1e** the rules decide (driver, modules; d) | §4.5-§4.7, §6.1's seed, §6.3's entries, §6.4's refusals | `LineCourseTests`' pinned table unchanged, each seeded rule answering as today's reader did; `SupervisorRuleTickTests` on stand-ins: answers alone held at 2 then one *Go on with this answer*, two answers queued together one held, *Not now* kept; failures held at 3 and *Try again*; tries at 2 weighted, at 5 held, and *Try again*; moves at 3 asked and one yes; a quiet end moved once and the next part's end not; an unknown count never acting; `rule-holds` at the keeping and the release; the rule's decision file unread by the sweep; the note; a fresh home seeded at its first start; each old door refused on a reached home naming `role rules` and writing on one not reached; a preset on a reached home writing its limit move and bound as rules in the same press; after conversion, a `tries at 2: weighted` rule keeping the next start off the choice it left (`StaffJudge.Arrange` reading the rules); a Chat line under the machine's rule where its repository holds only a Build rule; the save writing no `strikes` the file did not hold; the transitional gate green with the four entries; a real quiet tick (STAFFPOLICY1e3's) | the rules wave, after d |
| **CONTEXTDRIVE1f** the `rules` part (driver, modules; e, ONEWAY1ae) | §6.2 | a fixture home with `strikes` 5, Build's applied policy `transient after 2: weighted`, `most` 2, and a stale policy with a `failed` move: plan, Apply, a start answering as before, the stale moves removed with no rule, Undo, Undo refused after a rule is edited; `strikes` 0 writing no failures rule; the census `Ended` after Apply | the rules wave, after e |
| **CONTEXTDRIVE1g** the editors (web, modules; d, f) | §7.1-§7.3 | page tests en and zh; stories at 80, 39 and 25 rem, both themes; keyboard order; `RolePolicy.test.tsx` on rules, reached and not; each rule's *replaces* line; `HelpCoverageTests`' rows for each new hook | the rules wave |
| **CONTEXTDRIVE1h** the head, *Details* and *What needs you* (web, modules, driver; e) | §7.4, §7.5 | page tests en and zh; stories at 80, 39 and 25 rem, both themes; `AttentionStateSliceTests`, `attention.test.ts`; each hold with its own door; one hold counted once on the head, the foot and *What needs you*; the rehearsals' whole-answer comparisons checked for `counts` | the rules wave |
| **CONTEXTDRIVE1k** the room and the words (help, web locales, service; d, g) | §7.6, §7.7 | `HelpRoomRolesTests` and the room's goldens; the doors table's `role rules`; glossary *supervisor rule* and *count*; `names:check --strict`; parity | the rules wave |
| **CONTEXTDRIVE1n** a conversation's course reads the rules (driver; e, ONEWAY1h-iii) | §4.5's conversation, STAFFPOLICY1e3's conversation half | a stand-in conversation's provider failing: at 2 tries moved by weight and counted in `moves`, at 5 its turn ending as today; its limit's `wait` and `ask` and the `moves` rule read; the policy door's interim refusal of a conversation's moves gone with its register entry (ONEWAY1 §4 M5) | after ONEWAY1h-iii, which moves the conversation's runner |
| **CONTEXTDRIVE1j** `refer` (driver, modules, web; SUPERVISOR1a-iii, SUPERVISOR1c, i, e) | §4.6 point 3, §5's `course` | a referred point decided `go-on`, overruled inside the window, and deferred; one point with an open question decision referred once; a `course` act a rule would now hold moved stale at its release; none appointed and dormant both falling to the count's hold, said; the actor's attempt counted | with the supervisor's activation |
| **CONTEXTDRIVE1l** the kept reads and old doors deleted (driver, modules; the `rules` part's census ended on the install) | §6.3, §6.4 | the build with the four reads and the old doors gone; an unconverted fixture home holding its starts with the conversion as its door | ONEWAY1's wave 6 |
| **CONTEXTDRIVE1m** the installed proof (the steward with the owner; the rules wave, n) | §6.2, §7 | the owner's press converting the install; a line waiting on the answers-alone rule and gone on; a rule changed in the window and at the terminal; dated shots at 1546 and 680 px, en and zh | last |

**What can be built on main now, and the exact files.** None of these is a file `wave/workflowclean1` or `wave/comms1` changes:
- **CONTEXTDRIVE1a**: new `Course/Domain/LineKeys.cs`, `Course/Domain/LineCounts.cs`, `Course/LineCountsRead.cs`, `fixtures/line-counts.json`,
  and their tests in the driver's test project. No existing file changes: it reads `StaffMoves.Of`, the events and the service's
  records through their present doors.
- **CONTEXTDRIVE1c**: new `Roles/SupervisorRules.cs` and `fixtures/supervisor-rules.json`; `Store/DesktopStore.cs` (step 19 alone,
  appended to `Steps`); their tests and `DesktopStoreTests`.

**After COMMS1's wave 1 reaches main**: `b` (its files `Context/Domain/ContextBrief.cs`, beside COMMS1f's item,
`Context/Events/ContextBriefs.cs`, `Loop/DriverContinuations.cs`, `Course/Chat/ChatGoOn.cs`) and `i` (`Roles/SessionsSuperviseCommand.cs`,
which COMMS1c changed, `Roles/QuestionSupervisorDecisions.cs`, `Mod/Bridge/SupervisorDoor.cs`). **COMMS1g** builds inside that wave
after `a` and `c` reach main: it keys its admission by `LineKeys.Of`, takes the `at` as an argument (`AnswersAlone.Bound` until `e`),
and takes store step 20.

**The rules wave** waits for the workflow wave, since its files are that wave's: `Settings/DriverChoicesCommand.cs` (`driver strikes`)
and `Settings/DriverConfig.cs` (the save's `strikes` and its marker, which ONEWAY1 §10.1 defers to `ad-ii` on main),
`Help/HelpProposals.cs` and the room's `Knowledge/Room/HelpRoomDoors.cs`, `HelpRoomMayPropose.cs`, the modules' `Loop/NeedsAPerson.cs`
and `Loop/AttentionStateSlice.cs`; and for ONEWAY1ae's conversion door and ONEWAY1a's one staffing door, which its part and its door
join. **CONTEXTDRIVE1n** waits for ONEWAY1h-iii, since the conversation's episode is in `Loop/Chat/ChatRunner.cs`, which the workflow
wave changes and ONEWAY1 §10.1 keeps out of its now-rows.

**COMMS1g and STAFFPOLICY1e3 take it up.** COMMS1's wave 1 builds the answers-alone admission (its §5.3), the durable source of
`a`'s `answers-alone` count and a waited quest's result as a cause, and builds no rule, no hold of its own and no supervisor's reading:
those are `c`, `e` and `i`. STAFFPOLICY1's moves and `most` are rules (§4.8) from `e` and `f`, and the open parts of STAFFPOLICY1e3 are
built here: a conversation's transient move is `n`'s, the hold in the waits' union and the head is `h`'s, and a real quiet tick is
`e`'s proof. ONEWAY1i, STAFFPOLICY1e3 as ONEWAY1 filed it, is taken up by `n`, and ONEWAY1 §4 M5's entry for the conversation's
refused moves is deleted by `n`.

## 10. The owner's answers (2026-11-03)

1. **Today's numbers are seeded as rules on every home.** Every home, fresh or converted, holds *wait for you after 2 answers alone in
   a row*, the failures rule at its own `strikes`, the five tries and each applied policy's moves, so no line runs differently until
   the person changes a rule (§6.1). The conversion offers each seeded rule ticked.
2. **The cap stays a setting of the machine, outside the rules.** It bounds how many sessions the machine runs at once, which no line
   shows, and the person already sets it: `SET_CAP` and Settings stay as they are (§1.2).

## 11. Rejected

- **A table of counts.** A stored total beside the record it counts is a second way to count it and drifts from it.
- **A feed to the session, or counts in `line_read`.** The brief and a go-on's words carry the counts already; the service reading
  the desktop store would be a second reader across the machine-local line.
- **Rules inside each role's policy record.** A place's rule spans roles; one record in two stores.
- **Setting a rule through *Appoint* alone.** It grants nothing, and D226's moves are set through the role door already.
- **A model judging every go-on.** A rule asks the agent only where the person wrote `refer`.
- **A rule that ends a running or quiet session**, against D200 and D205 point 5; a rule over a role's day, which is the budget's.
- **A rule over a count that never resets** (minutes, tokens, hand-overs, quests handed, results waited): its hold would hold every
  go-on after its release, and a release record for it would be a fifth way to release a hold.
- **One hold row and one release press for every rule.** Each hold keeps its row and its door: unifying them would rename stored
  kinds and lose *Not now*.
- **A new course step for the rules.** It would move `AloneHeld` before the transient-bound hold and judge presses `Policied` leaves
  alone; each rule is read where its count's executor reads its number today.
- **Raising the store's writer stamp.** It refuses an older build's writes, never its driving.
- **Removing the `tries` rule.** A transient end strikes nothing, so a line with no hold past the episode starts again without end.
- **An unknown count read as zero or as the bound.** Zero lets a line run past a rule; the bound holds it for Daoris's own outage.
- **A notification for a rule's act**, as D226 refused one for a move.
- **The cap, the channel's bounds, Daoris's carry-ons and the walk's passes as rules** (§1.2).

## 12. Read and settled (2026-11-03)

The Opus read found nine blocking findings and ten others. Each was checked against the code at `6bcd5cce9`.

| # | Finding | Settled |
|---|---|---|
| 1 | The answers-alone hold acts in `AnswersAloneHold.Holds`, not in the course; a second hold path would stay; `Policied` and `AloneHeld` sit at different steps; *Not now* is the person's; the rule must be judged inside COMMS1g's admission | §4.5: each rule decides at its count's executor, named per count; the hold stays the mark every reader honours; the course gains no step and keeps its order; *Not now* kept; the `at` judged inside COMMS1g's serialized admission. §1.1's row corrected |
| 2 | `hold`'s release door misnamed ONEWAY1j; counts that never reset have no release | §4.2's release table: each hold's own door and the record it is kept in; ONEWAY1j said to be no release. §2.2: counts that never reset take no rule; `quiet` and `limits` read per part's end; §11 |
| 3 | Three line keys | §2.3: `LineKeys.Of`, one key for the moves, the counts and COMMS1g's admission, D79's resume on its quest's line; §2.4 settled against COMMS1 §13 point 8: `answers-alone` never unknown, *at least* only for a cut or trimmed seed |
| 4 | `line_read` is the service's and never opens the desktop store | §3 point 3: `line_read` gains no counts; the brief and a go-on's words are the doors; §11 |
| 5 | The old doors stay live while seeded rules decide | §6.4: refused on a reached home in row e, the row that makes the rules decide; d, e, f, g, h and k land as one wave |
| 6 | `driver.json` writes `strikes` on every save, so the census never ends | §6.2: from row e the save writes `strikes` only as the file held it or a press set it on a home not reached |
| 7 | Conversion changes behaviour: stale moves converted; `quiet` over the line; *Not now* lost | §4.8 and §6.2: applied policies only, stale moves and `retry` become no rule; §2.2: `quiet` per part's end; §7.5: each hold keeps its row and *Not now* |
| 8 | `moves-unbounded` judged at the rule's place, not on each line's effective set | §4.5: both bounds judged on every line's effective set, a wider write refused naming the narrower place; §7.1: each rule says what it replaces |
| 9 | Now-rows touch the open waves' files or collide | §9: on main now only `a` and `c`, their files named; `b` and `i` after COMMS1's wave 1; d, e, f, g, h, k as the rules wave after the workflow wave; the conversation's move `n` after ONEWAY1h-iii; store step 19 for `c`, 20 for COMMS1g; ONEWAY1 §4 M5's entry and ONEWAY1i re-pointed to `n` |
| — | `transient-bound` missed `LineCourse.Decide.cs:452-458` | §6.3's entry names `PastBound` and the planner's path |
| — | `over-budget` is the walk's pass | §1.1, §1.2 and §2.2: shown, the act the walk's (`StaffJudge.cs:160`); refusals likewise |
| — | Row i's check is in the modules | §9: `i` takes the modules lane and `SupervisorDoor.cs` |
| — | A stamp raise refuses only an older build's writes | §4.4: no raise; what an older build does said; §11 |
| — | `tries` below 5 does nothing for a conversation; `tries: off` leaves a transient loop | §4.5: `tries-unbounded`, no `off`; §7.1 says *driven lines* until `n` |
| — | `failures-on-choice` has no source for a conversation | §2.4: a driven line's count, absent for a conversation, never unknown |
| — | How the sweep and history read a rule's decision | §4.7: no `question` and no state lines, final as written; the questions' reader skips it; the history reads it by holder kind |
| — | `refer` could ask the holder twice; rules re-read at the `course` act's release | §4.6 point 3 |
| — | §4.8's example dropped `upTo` | §4.8 |
| — | D229 and §1.3 mis-cited D225's note and COMMS1 §5.3 | §1.3 and D229: both say point 4; only the brief says point 5 |

**Owed under D191**: the other maker's read, kept beside this design, each finding settled here before the decision is accepted.
