# The foundation: who acts, how work moves, what is said — Claude's proposal

> FOUNDATION1, phase 1 under D191 (2026-10-25): one of three sealed proposals, written by Claude Opus 5.5 from
> `docs/2026-10-25-foundation-brief.md` and `docs/2026-10-25-foundation-survey.md` alone, before any other proposal was read.
> Read from the code at `f280761cf`, whose `src/` is the survey's `0e195ee61` unchanged, so the survey's lines hold and are cited
> as it cites them; a line this proposal adds is cited at the same commit. Nothing is built, run, published or spent. Paths follow
> the survey's aliases (`Driver/`, `Modules/`, `Core/`, `Mcp/`, `Web/`). `Dnn` is `docs/decisions/Dnn.md`. *Unverified* marks what
> was inferred from the code and not seen in a test. A section cited with a row (*§1.3 row 8*) is a row of the survey's tables; a
> section cited bare is this proposal's, and the survey's is written *survey §n.m*.

## 0. The shape

The foundation is three concepts, one per area, each a type with one owner and a census, standing on stores Daoris already
has: no new authoritative store for anything that already has one, and no new word on the page. **Who acts** is the
*participant*: every kind that runs work is one type whose kind is its actor (stored on the session record by D205's rows,
derived once for a record from before), whose identity is a typed id resolved at the connector, admitted by one table in each
place admission runs instead of seven predicates, and started through one start plan instead of four walks and seven fallbacks
to `config.Adapter`. **How work moves** is the *course* and the *move*: one pure judgement, called by both loops, decides where a
line's next words or work go from the line's facts, the trigger, the failure's kind and one account rule; every course that opens
a new part writes one durable move record that owns its delivery and recovery; and words follow the line's head at the
service's say door, so no word depends on a path's care to land once. **What is said** is the *event*: a fact is minted once,
where it is recorded (the service's store, by trigger, for records; the lease holder's loop, for machine facts), in one envelope
whose kind is a closed code from the vocabulary the notes already carry, and every channel is a projection of it with its filter
written in one table. Together they are D173's Fabric (ADR-B's participant and binding, ADR-D's durable delivery, ADR-E's
hand-off), reached in rows that each land alone behind today's tests, the first of them LIMITSWITCH1, built as the account rule.

| Area | The concept | Its type and record | Its owner | What it replaces (survey) |
|---|---|---|---|---|
| Who acts | **participant** | `Participant` over the session record (kind = `actor`), a brainstorm pass, a lineage binding or an appointment; `RuntimeBinding` over the record's adapter, profile, tree and `harness.json` | the stores' owners, unchanged; the type and the start plan in the driver library; the admission table twinned in the service and the driver | five rule shapes, four walks, five re-asks, seven refusal predicates, 27 readers of the marks (§1.3 rows 1-16) |
| How work moves | **course** and **move** | `Course.Decide(CourseFacts, Trigger) → Course`, pure; `Move` at `<home>/sessions/<old record>.move.json` | the driver library: decided there, executed by the loop that runs the line, recovered by the lease holder's loop | fourteen paths deciding agent, account, conversation and words each its own way (§2.2, §2.4 rows 1-15), one durable move of five (§2.5) |
| What is said | **event** | `ledger_events`, a journal in the service's database minted by trigger; the loop's machine events under EVENTHOOK1's `run` and `seq`; one `EventCodes` vocabulary | the service for record events; the lease holder's loop for machine events; the page and the terminal word them | fifteen channels with four meanings of an end, five tellings of a park, six orders and two baselines (§3.4 rows 1-11) |

## 1. Who acts: the participant

### 1.1 The type and where each field lives (question 1)

`Participant` is a read, never a store (D173's first guard; its C0 audit's rejection of *a new session record per child*, `D173.md:91`;
COWORKVIEW1's rule 1). It is built in the driver library by one function, `Participants.Of`, from the stores that already hold
each kind (survey §1.5), and in the service, for the connector, by `Admission.Caller` from the ledger alone.

| Field | What it says | Read from | Owner of that store |
|---|---|---|---|
| `Id` | a typed id: `session:<record>`, `pass:<brainstorm>/<pass>`, `child:<record>/<harness>/<native id>`, `holder:<holder id>` | the record's id; the pass's file (`BrainstormFiles.cs:36-39`); the lineage fold (`ChildBindings.cs:128-266`); the appointment (`StandingConfirmation.cs:771-806`) | service; driver; driver; modules |
| `Kind` | one of D205's eight actors, or `subagent`, `holder`, `meeting` | the record's `actor` (`Sessions.cs:196`), else derived (§1.2); the pass's actor fields (`DriverBrainstorms.cs:701-709`); fixed for the other three | as above |
| `Serves` | the participant it works for: a helper's, partner's and (once D203 builds it) reviewer's lead; a child's parent part; nothing else | `serves`, `piece`, `opinion` (`Sessions.cs:135-165`); the lineage | service; driver |
| `Entry` | its line (D167), where it has one | `entry` (`Sessions.cs:237`) | service |
| `Binding` | `RuntimeBinding`: adapter, harness version, profile, tree, the kept conversation and its door | the record's columns (`Sessions.cs:101-117`) and `<s>.harness.json` (`GoOn/HarnessConversations.cs:31-44`) | service; driver library |
| `Authority` | none, or the roles it holds (D208, D209), never the person's key | the appointment | modules |

D173's ADR-B names `Participant` and `RuntimeBinding` (`D173.md:11-12`) and nothing in `src/` builds either (survey §1.4). This
builds both as types over today's stores, which is what the C0 audit found every primitive maps onto (audit §1, "each concept has
one store today"); the one store it found missing, a child's binding, is the lineage file C1a built. `WorkUnit`
(`Trees/Inventory/WorkUnit.cs:4-20`) keeps its kinds and reads participants as its executors.

**Identity at the connector** stays `DAORIS_SESSION_ID` for every participant with a record (`Core/Asks/IntakeScope.cs:28-33`), with
no prefix on the wire, so an older driver's spawn names the same caller. A brainstorm voice's synthetic id
(`brainstorm-<id>-<turn>`, `DriverBrainstorms.cs:664-668`) is resolved by the connector through the brainstorm box
(`Core/Sessions/BrainstormBox.cs:15-26`) as `pass:`, so it is a caller the filter can place (today it passes as *no record*,
`RoleFilter.cs:109-113`). A child is placed per call by its lineage, C2a's rule (`D173.md:140-145`). A holder has no connector
(`HolderTerminal.cs:44`) and so no identity there.

### 1.2 The kinds (question 1)

The participant's kind is its **actor** where Daoris starts it: the record stores `actor` for build, chat, intake and Ask Daoris
from the driver and for helper, partner and opinion from the desks (`ServiceClient.cs:648`, `:701`, `:779`, `:810`; `HelperDesk.cs:102`;
`PartnerDesk.cs:82`; `OpinionDesk.cs:131`; survey §1.3 row 8). A record from before is
answered its kind by **one rule in the service**, at the read, never written back, as the entry's rule key already is
(audit row 1: *answered the rule's key, which is never written back*):

```
ActorOf(record) = record.actor
  ?? ask      → intake
  ?? opinion  → opinion
  ?? serves and piece → partner
  ?? serves   → helper
  ?? repository == "daoris:help" → ask-daoris
  ?? kind == chat → chat
  ?? build
```

| Today's kind (survey §1.1) | Participant kind | Record | Distinct because |
|---|---|---|---|
| Driven session; a workflow's `claimed` and `look` nodes | `build` | the session record | the nodes are projections onto the quest's session (`WorkflowGraphRun.cs:312-314`), not participants |
| Repository chat | `chat` | the session record | — |
| Ask Daoris | `ask-daoris` | the session record | its room and mode (`HelpRoom.cs:65-76`; `Sessions.cs:193`) are its binding's, not its kind's |
| Intake | `intake` | the session record | — |
| Helper | `helper`, `Serves` its asker | the session record | — |
| Partner | `partner`, `Serves` its lead | the session record | — |
| Reviewer | `opinion`, `Serves` the candidate's session once D203's reviewer `serves` builds | the session record | — |
| Brainstorm voice | `brainstorm` (the synthesizer a voice that synthesizes) | the pass file | no session record by D176's design (`DriverBrainstorms.cs:658-660`) |
| Meeting participant | `meeting` | none until D177 starts one | nothing starts it (`Driver.cs:447-452`) |
| Co-worker | **not a kind**: a relation, `Serves` (D203 point 1) | — | a helper, partner or reviewer is one in a role, which its kind already says |
| Supervisor and steward holders | `holder`, with `Authority` the roles it holds | the appointment | outside the pool by D208's design (§1.5) |
| Native subagent | `subagent`, `Serves` its part | the lineage binding | C1a: bound to its part, never a record |

Every reader that asks *what kind* reads `Kind`, and only a reader that asks *whom it serves* reads `serves`. Today at least 27
lines outside tests, the page (`Web/work/identity.ts:70-79`) and the cap (`Planner.cs:872-885`) tell the kind from the marks
(survey §1.3 row 8), and the page reads every `serves` as a helper (`identity.ts:79`), so a partner is drawn as one. `actor` is
already pushed (`SessionsStateSlice.cs:46`), so the page reads the service's answer and judges nothing (D182 point 6).

### 1.3 The rule and the admission (question 2)

**One rule shape, read from today's keys.** `KindRule { mode: on | ask | off, agents?, minutes?, atOnce? }` per kind and scope, read
by one reader over the keys `driver.json` has now (`helpers`, `handOver`, `opinions`, `brainstorms`, the presence of `intakeAdapter`
and `helperAdapter`; survey §1.3 row 1), so no key moves and an older build reads its keys as before (`twins.md`'s rule on
`driver.json`'s writers). Each kind's defaults are written once in that reader's table, held by a census over every kind: the three
defaults of today (helpers `on`, hand-over `ask`, opinions none) stay as they are, and D163 point 3's text gets a note saying where
HELPERDEFAULT1 moved it (§1.3 row 1). A partner's rule stays absent until COAGENT1d gives it a key, which the table names as *not a
rule yet*, never as `on`. The brainstorm's `mode` is read by the agent's ask, BRAINSTORM1d's door (`BrainstormRules.cs:63-64`); the
census lists it as a rule whose reader is unbuilt, so the claim it makes is said.

**One admission, one table per place it must run.** Admission runs where it binds: the driver's start, the service's connector, and
the harness's own rules handed at the spawn. The first two are runtimes that share no code (`twins.md`) and the third is enforced by
the harness, so they stay three places; each becomes a table over `(Kind, act)` instead of a predicate per door:

| Judge | Where | Table | Replaces |
|---|---|---|---|
| The start's | the driver library, `Admission.MayStart(kind, asker, rule, facts)` | per kind: the rule's mode, `atOnce` (today's separate counters, §1.3 row 20), the ledger's busy tree (`SessionLedger.cs:1597-1601`), the launcher's containment (`AgentStarts.cs:30`, unchanged) | `HelperJudge.Judge` (`:61-85`), `PartnerJudge.Refusal` (`:15-28`), the intake's and Ask Daoris's gates (`DriverIntakes.cs:63-71`; `HelpRoutes.cs:52-57`) |
| The connector's | the service, `Admission.MayCall(caller, tool)` | every registered tool × every kind, derived from the registration so a new tool is refused until classed (C2a's point 3, `D173.md:150-151`); a child read-only by lineage; **a caller it cannot place refused every mutation and served every read** | `RoleFilter` (`:28-58`, `:109-141`) and the five per-tool predicates (`QuestionBox.cs:172-182`, `HandOverAskBox.cs:102-111`, `Channel.cs:1164`, `HelperBox.cs:131-138`, `OpinionDesk.cs:107-112`) |
| The harness's | the driver, at the spawn | the same table rendered as each harness's rules (`OpinionPermissions.cs:27-45`, `IntakeRoom.cs:49-58`, `HelpRoom.cs:65-76`, `BrainstormPermissions.cs:34-67`) | five hand-kept lists (§1.3 row 15) |

The connector's and the harness's tables are twins: each carries the same table of cases and a test compares them cell for cell
(`twins.md`). *May this session ask a helper?* is asked in five places today (§1.3 row 6); after, the page and the terminal ask the
driver (D182 point 6; D194's pipe), the driver's start judge answers, and the service's desk keeps its one check as the store's own
guard, both reading the same row of the table. The launcher stays the one containment (D201).

**What the person sees change.** Nothing on the page, and nothing for a person who sets nothing, with three exceptions each a decision
(§5.3): a partner is drawn as a partner (`identity.ts:79`); a reviewer's and a brainstorm voice's mutations are refused at the server,
as the harness's rules already refuse them where the adapter takes settings (`Driver.cs:3087`), so on Codex only the server binds them
(§1.3 row 15); and Ask Daoris's `helper_ask` is refused, as D203 point 10 decides and the partner's twins already refuse
(`PartnerPersonAsk.cs:71`; `PartnerJudge.cs:18`).

### 1.4 The choice: one start plan (question 3)

Every start resolves its agent, choice and account through one path, `StartPlan.Resolve(kind, trigger, record?, pick?, fence?)`:
D205's `ActorChoices.Effective` (`ActorChoices.cs:131-144`) for the order, one walk choice by choice for the entry (today four walks,
§1.3 row 3), D197's account walk inside each choice (`HarnessRoster.cs:908-934`), one set of pass codes (today two,
`ReviewerChoice.cs:89-111`), and one re-ask of a refused model (today five, §1.3 row 5). A continuation keeps its record's actor and
asked choice (D205 point 1), resolved by the plan from the record, never from the machine's `adapter`. The co-worker's choice stops
borrowing the opinion's rule type (`HelperChoice.cs:66-70`; `PartnerChoice.cs:32-33`), so a helper is never told *No second opinion*.

The starts that do not go through one path today, and what each becomes:

| Start | Today | After |
|---|---|---|
| A driven carry-on or retry | `handed ?? config.Adapter`, the last record's asked model (`Driver.cs:1917`, `:2020`), while a resume requires the record's own adapter (`DriverContinuations.cs:40-45`; §1.3 row 12) | the record's adapter and asked choice, or the handed agent's Build entry (`Driver.cs:2019`) |
| The look's report of a start | names `handed ?? config.Adapter` before the walk (`Driver.cs:1107`) | names the plan's answer, so the report and the start agree (§1.3 row 11) |
| A terminal's start-from | the first agent only (`SessionsVerbs.cs:32-33`) | Chat's order, as the route's (`ConversationRoutes.cs:171-174`; §1.3 row 13) |
| A reviewer's same-agent press | `record.Adapter ?? Config.Adapter` (`OpinionGateLook.cs:393`) | the record's adapter with Second opinion's entry for it |
| The other `config.Adapter` fallbacks | `ConversationRoutes.cs:51`, `:173`; `SessionGroups.cs:882`; `BriefedMove.cs:113`; `ChatGoOn.cs:526` | the record's actor through the plan; *as before* stays `Effective`'s own answer |
| A brainstorm voice | `SelectHandedAsync` with no workspace (`DriverBrainstorms.cs:682-683`), so no pinned version (`HarnessRoster.cs:899`) | the plan with the request's workspace |
| A role holder | a pinned `claude-code` on its own sign-in, outside the roster (`HolderTerminal.cs:42-49`) | unchanged and named in the census as the one start outside the plan, by D208's design, until a holder's actor runs (D208's title: *held by the steward from a terminal until its actor runs*) |
| A meeting turn | none (`DriverMeetings.cs:320-322`) | none; the census names it |

### 1.5 Roles and actors (question 4)

An **actor** says what kind of work a participant does and which agent, model and account it runs on (D205). A **role** says what a
participant may decide for the owner (D208, D209). They do not compete: a role is **held by** a participant and is never a kind of
participant or an actor's property. Today the holder is a participant of kind `holder`, started by the window outside the pool (D208's
round 4: a dedicated session doing only the role), so the role is held by a holder that plays no actor. When SUPERVISOR1c lets a holder
run as an actor (D208 point 2, refused today at `StandingConfirmation.cs:829-830`), the holder becomes a participant Daoris starts
through the start plan with that role's own actor, and its `Authority` is unchanged: so both, in sequence, and never at once for one
holder.

A role holder is a participant without the owner's authority by three rules that already exist and that the foundation states once:
its acts are its role's table's exact forms (`StewardActRows.cs:152-162`; `RoleActs.cs:47-63`), each recorded with `by: role:<name>`
and never as the person's word (D208 point 1, D209 points 1-3); the key never crosses the pipe (an HMAC proof, `SupervisorProof.cs:313-328`);
and attribution may narrow and never widen (D156 points 1-4). In the event vocabulary (§3.2) `role` is a cause of its own beside
`person`, `daoris` and `agent`, so no reader can mistake a role's act for the person's press (brief §6). The two doors (§1.3 row 22)
become one act form and one proof in a late row (§6, 1r), which grants authority and so waits for both makers' reads (D191's note of
2026-10-24).

### 1.6 Native subagents (question 5)

A harness's child is a participant of kind `subagent`, bound to its part by C1a's lineage (`LineageFile.cs:13-14`; `ChildBindings.Fold`,
`:128-266`), never a record. What is read of it is what the fold already holds: harness, native id, parent, depth, the signals that
named it, and active, finished, ended or lost (`D173.md:104-112`), with what no harness said left unknown (canary 9). Its first
production reader is COWORKVIEW1's row a (§4). At the connector it is placed per call by its lineage, and refused every mutation, with
an unreadable lineage read as a child (C2a's points 1 and 5, `D173.md:140-166`); the class table is the admission table's child column.
Until C2a is staged, a child calls with its parent's id and role (`CallLineage.cs:11-14`; `RoleFilter.cs:28-30`), which the census
names as the one known gap of the participant's identity.

### 1.7 Where today's parallel paths go

| Survey §1.3 row | Goes to |
|---|---|
| 1 rule shapes, 10 Intake and Ask Daoris off | `KindRule` (§1.3) |
| 2 a co-worker's choice by an opinion's rule, 3 four walks, 4 the account walk's call, 5 five re-asks, 11 the `adapter` fallbacks, 12 a carry-on's agent, 13 one start two doors | `StartPlan` (§1.4) |
| 6 may it ask a helper, 7 which kinds a tool refuses, 15 where tools are held, 16 a caller it cannot place | `Admission` (§1.3) |
| 8 stored or read from marks, 9 the eight ids twice (`ActorChoices.cs:37`, `ActorRun.cs:176-177`) | `Kind` (§1.2); the ids one list both read |
| 14 what a partner is handed | unchanged: the digest is D199's measured row's, and two computations that must agree for it stay held by `DriverPartners.cs:546-548` |
| 17 seeing native children | §1.6 |
| 18 who said a word | the event's `by` (§3.2): `person`, `daoris`, `agent`, `role`, `helper`, `partner`, `opinion`; `SaidWord`'s three fields stay as stored and are read through one function, so a partner is no longer written as a helper (`PartnerDesk.cs:94-96`) for new words |
| 19 a one-turn kind's time, 20 concurrency | `KindRule.minutes` and `atOnce` |
| 21 a holder's agent, 22 two doors | §1.5 |

## 2. How work moves: the course and the move

### 2.1 One decision (question 6)

One judge decides where a line's next words or work go: `Course.Decide(CourseFacts facts, Trigger trigger) → Course`, a pure
function in the driver library (`Driver/Sessions/Domain/`, beside `Observation` and `ActorRun`), for driven lines and conversations
alike. It runs where it is asked: the look asks it for a driven line, the chat runner for a conversation, and each executes the answer
with the code it has today. Nothing merges the two loops (§7).

**Inputs.**

| Input | What it holds | Read from today |
|---|---|---|
| `Head` | the line's current part: state, its end's note code, `limited`, the record's actor and asked choice, adapter, profile, tree and whether it stands, the kept conversation and whether its door resumes, teammate, intake, stood down, the words waiting and their authors | the record; `Continuation.Judge`'s inputs (`Continuation.cs:207-239`); the head by §2.4's rule |
| `Failure` | the last end's kind, where it ended on a failure | D210's reader (`AgentErrors.cs:133`) |
| `Accounts` | for the line's allowed choices: the own account's hold (`ResumeAccount.Judge`, `:25-45`), the first ready account by the walk's order, the earliest reset over exactly those accounts | the roster (`HarnessRoster.cs:962-1042`, `:1732`) |
| `Person` | a pick, *Go on in a new session* (`<s>.new-session.json`), a briefed press, a hand-over press with its agent and account, a stop or a pause | the presses' records |
| `Rules` | the hand-over rule (`HandOverRules.cs:13-16`), the actor's choices, the curve's decision (`SwitchDecision.Decide`, `CurveMove.cs:128-134`) | as today |
| `InFlight` | the line's move record, if one stands (§2.3) | §2.3 |

**Triggers.** The person's or Daoris's words, a solicited answer, a limit's end, a limit's wake, a transient end past its bound, the
curve, a hand-over (pressed or asked), a briefed press, a carry-on's reason, *Try again*, a workflow's send-back, the come-up.

**Outputs.**

| `Course` | Says | Executed by |
|---|---|---|
| `Resume` | the same part, its own conversation, on its own account, with the words that go and Daoris's word by its key (`LimitReset`, `TransientRetry`) | the go-on (`DriverContinuations.ContinueAsync`; `ChatGoOn`) |
| `Move` | a new part: `how` (`brief`, or `carry` once SWITCHCTX1 lands), the agent and choice, a named account or the walk, its `why` and `by`, the words it carries | the start (`Driver.RunAsync`; `ChatRunner.StartAsync`), with a move record (§2.3) |
| `Wait` | until a moment, with its reason code and the accounts it waits on | the hold (`Driver.cs:2041-2050`; `LimitWaits`) |
| `Hold` | for the person, with the reason code and the doors out | the hold's note |
| `Never` | a teammate's record, an intake, a stand-down, a record a later part supersedes | the say door's refusal (`SessionSay.cs:40-46`) |

The transient retry itself stays inside the run (D210 point 3); the course decides only what follows its bound.

| Path (survey §2.2) | Trigger | The course's answer, as today's behaviour |
|---|---|---|
| A driven go-on | words | `Resume`, else `Move`/`Wait` by §2.2 |
| B a conversation's go-on | words, a wake, an answer | the same as A, now (row 1a): today `CannotGoOn` where the own account cannot carry (`ChatGoOn.cs:581`) |
| C go on in a new session; start from these words | a press | `Move` (`retry`/`resume`), the person's words only; start-from a fresh part with a preface, unchanged |
| D the limit move | a limit's end | `Move` (`limit`, `daoris`) where an account of the allowed choices is ready, else `Wait` |
| E the limit's wait and wake | a wake | `Resume` on the own account, else `Move`, else `Wait` |
| F the driven limit resume | a limit's end, *Try again* | as E, step 0 the hand-over (`LimitResume.cs:29`) |
| G the curve | a turn's end | `Move` (`curve`) on the decision's account |
| H, I hand-over of a conversation, of a quest's session | a press, an ask | `Move` (`press`, `agent`); the brief only, none of the person's words (`HandOver.cs:271`) |
| J the briefed move | a press, a proven loss | `Move` (`resume`) |
| K the driven carry-on | the planner's reasons (`Planner.cs:1400-1442`) | `Move` (`carry-on`, `retry`, `limit`, …) |
| L a chat's workflow send-back | a press's send-back | `Resume` between turns, or B's go-on for an ended chat (`ChatRunner.cs:2347-2363`) |
| M SWITCHCTX1's carry | any same-agent move | `Move` with `how: carry` |
| N AGENTERRORS1's retry | a transient end past its bound | `Hold` until the person's word or *Try again* (`Planner.cs:1525-1532`) |

A census test lists every trigger site (the planner's carry-ons, the go-ons, the limit move and wake, the curve, the hand-overs, the
briefed move, go-on-new, start-from, the send-back) and fails on one that neither calls `Course.Decide` nor is named as not yet moved,
as the brief's §7.2 asks.

### 2.2 The account (question 7)

The account rule, stated once and read by every path through `Course.Decide`'s `Accounts` input:

1. **A resume of the agent's own conversation asks for the record's own account** (D137 point 8; D131): ready, it resumes there.
2. **The own account cannot carry the words** (cooling, limited, refused, signed out, gone, outside the set, kept for conversations, the
   asked model not offered; `ResumeAccount.cs:25-45`, `HarnessRoster.cs:975-977`) **and an account it may take is ready: the line goes
   on now** in a new part there (briefed today, carried once SWITCHCTX1 lands), whatever ended the record (LIMITSWITCH1; D206 point 3
   ii). The accounts it may take: the asked choice's other accounts, then the actor's later choices where a limit ended it or every
   account of its agent cools (D205 point 5, D206 point 6); **a pick** only its own account (D206 point 5); **a handed agent** and an
   actor *as before* only that agent's accounts (D206 point 6).
3. **None ready: it waits** until the earliest reset of exactly the accounts step 2 would take, one computation, said in D206 §3.7's
   words on its record and its rail; the wake is the look (D206 point 4).
4. **A new part's account** is the walk's (D197, D205 point 4), the named account first where a hand-over, a handed quest or the
   curve named one, and never the own account where its conversation could resume there (D206's rejection of *a briefed carry-on on
   the same account*, `D206.md:49`).

What it replaces (survey §2.4): row 1's one flag at four sites (`ResumeAccount.cs:78`; `HarnessRoster.cs:1008`; the waits at
`Driver.cs:2031-2039`, `ChatGoOn.cs:573-579`, `:674-680`, `LimitWaits.cs:185-186`); row 2's seven answers, which become three (own for a
resume, the walk for a new part, the named account first where one is named); row 5's pick, which the limit move's same-agent move and
the wake read nowhere today (`LimitMove.cs:85-104`; `LimitWaits.cs:188-191`); row 9's maker reading, asked by the roster for every own
account a limit cooled (`HarnessRoster.cs:1001-1006`), the wake included; row 11's two earliest resets (`HarnessRoster.cs:1732` against
`:251-256`). The limit move's first `SelectAsync`, counted and never released before the start walks again (`LimitMove.cs:85-86`,
`:141-145`; survey §2.4's note), becomes the course's named account, so the account judged is the account opened.

### 2.3 The move record (question 8)

Every course that opens a new part writes one durable **move record** before the old part ends, and the record owns the move's delivery
and recovery until the new part's first turn answers. It is SWITCHCTX1's carry record (`docs/2026-10-24-switch-context-design.md`
§2.3) without its copy phases, made the record of every move instead of the carry's alone:

- **Where**: `<home>/sessions/<old record>.move.json`, keyed by the record left, as D206's box and the hand-over's box are, in the
  session's file inventory under `hand-over` (`SessionHomeFiles.cs:53`, `:126`), so a delete and a clear take it with the record.
- **What it holds**: the phase (`decided`, `old-ended`, `opened`, and for a carry `copied`, `verified`; then `delivering`, `falling-back`),
  the moment each was written; the old record and, once opened, the new; `how`, `why`, `by`; the account named or *the walk*; the brief's
  recipe (the why and the event bound it is composed up to, `ContextBriefs.cs:59-95`); the delivery: each word's id and the move's key
  `move/<old record>/<n>`, the say door's answered ids. Never a word's text, a path or an account's key.
- **Who writes and recovers**: the process that executes the move writes it (the chat runner in the shell; the look in the shell or the
  headless host); the loop that holds the home's lease reads every move record at its come-up before it takes up any record, at the slot
  where the uncertain words are settled today (`DriverWatch.cs:739`) and D206's box is read (`LimitMove.cs:352`; `ChatGoOn.cs:189`). Each
  phase is safe to run again: an open whose answer was lost is found by the record `from` the old one created after the move record's
  moment, as SWITCHCTX1 §2.3 step 4 finds it.
- **Today's boxes become its adapters**: `<s>.limit-move.json` (`Chat/LimitBox.cs:31-46`), the conversation's hand-over box
  (`HandOver.cs:22-26`) and `data/handed/<quest>.json` (`HandOverQuest.cs:23-27`) are written beside it, from it, while a build in the
  update's rollback window (D139) could read them, and retired in the records-last row (§6, 1s). The hand-over's **ask**
  (`HandOverAskBox.cs:28`) is not a move: it is the agent's request through the connector, the service's, and a move record is written
  once the ask is approved.

Today one path of five keeps a move durable, the ended limit move's box; the live limit move, the curve and a driven carry-on keep none
(survey §2.5). The record lands first on those three (§6, 1j).

### 2.4 Words across a move (question 9)

The dispatcher's guarantee holds through every move by three rules, each in one place:

1. **Words follow the line's head, judged at the service's say door.** Today the head is judged by the driver and the shell from a
   snapshot of the records read before the say (`WordsNever.ContinuedIn`, `SessionSay.cs:54-71`, at eighteen sites; `WordsNever.LastHere`,
   `SessionSay.cs:90-91`), and a record that went on refuses with the head named (`SessionWords.cs:555`), so a part opened between the
   read and the say leaves the word on the old part. `SessionLedger.SayAsync` already reads, judges and writes in one exclusive step
   (`SessionLedger.cs:931-973`); it gains the head inside that step, by today's two rules (a conversation by its `from` chain, a quest's
   session by its quest's last run here, `ServiceClient.ReadLastRun`, `:2248`). It answers today's refusal, `continued` with the head
   named, so no caller reads anything new and each does with it what it does today (the page draws no box on a part that went on,
   `Web/work/say.ts:45`; findings go to the person, `OpinionDeliverer.cs:107`); what changes is that the judgement and the keep are one
   step, so no part can open between them. The driver's `ContinuedIn` stays the fallback for an older host.
2. **A move delivers after the open.** The new part's record opens in one write, so the head moves in the ledger at once. The move reads
   the old part's waiting words after that write, never before, and moves each by id (kept on the new part under the move's key, then
   taken off the old). A word kept before the open is on the old part and is read; a word said to the old part after it is refused there
   by rule 1 with the head named, and its caller passes it on to the head as the shell passes held words on today (`SessionWords.cs:838-855`).
   LIMITWORD1's lesson (`LimitMove.cs:266-270`'s second read) becomes the rule of every move.
3. **No word is held in memory alone during a move.** Under the chat dispatcher's `Hold()` a word is neither kept nor taken
   (`ChatTurns.cs:182`, `:458-466`), and the shell holds words for a hand-over's take in `held-words.json` (`SessionWords.cs:455-467`,
   `:901-973`), which drops a word's `Choice` and `Key` at a restart (survey §2.4's note). A move keeps and queues, as a limit's wait and a
   press already do (`ChatTurns.cs:492-500`, `:568-586`), and rule 2 delivers them.

Which words a new part carries is decided by the course alone, one predicate where five sites write it today (survey §2.4 row 14):
the person's waiting words and a limit's refused turns' words (`LimitHeld.Of`), each once; Daoris's words never carried and composed again
under their keys; a word once on the wire never sent again (D210's dispositions; `SaidDoor.cs:69-92`). Every producer passes its key,
the shell's record path included (`SessionWords.cs:701`, `:782`; survey §2.4 row 15).

### 2.5 The errors (question 10)

D210's four kinds are the course's `Failure` input, read by the one reader for every door and every kind of run:

| Kind | The course's answer | What changes |
|---|---|---|
| `limit` | §2.2's rule: resume own at its reset, go on now elsewhere, or wait; never a strike (D206 point 1) | a conversation's limit is read by the reader's code as well as its sentence (today the sentence alone, `ChatRunner.cs:2670-2675`, on the protocol door alone, `:1644`), a brainstorm's too (`DriverBrainstorms.cs:778`); a conversation's limited record is the ledger's flag, which travels already, written at its end instead of a scan of its events (`LimitHeld.cs:17-56`; survey §2.4 row 8) |
| `access` | `Hold` for the person, naming the account (D210 point 5) | — |
| `transient` | the retry inside the run (D210 point 3); past its bound `Hold`, released by the person's word or *Try again* | AGENTERRORS1's rows reach the other kinds of run and the pipe door; the course's answer is the same for each |
| `fatal` | the end with its reason; the planner's strikes as today (`Planner.cs:1985`) | — |

A limit or access refusal at a resume's open runs the same readers as every other end (AGENTERRORS1's C4-6), because the open's failure
reaches the course like any other. One try count per kind, stated in the course's table: five transient tries (`TransientRetry.cs:24-28`),
one native resume that exited before naming its conversation (`DriverContinuations.cs:1291-1316`; `ChatRunner.cs:2844`), three strikes
(survey §2.4 row 6).

### 2.6 Where today's parallel paths go

| Survey §2.4 row | Goes to |
|---|---|
| 1 wait or move, 2 which account, 5 a pick at a limit, 9 the maker's reading, 10 another agent at a limit, 11 the earliest reset | §2.2 |
| 3 words held during a move, 14 only the person's words, 15 the say door's client | §2.4 |
| 4 resume or brief | the course's `Resume` against `Move`, by `Continuation.Judge`'s reasons (`Continuation.cs:207-239`), one table; a pipe-door limited line lands briefed on another account, never the same (`Driver.cs:4629-4633`; D206's row 1f) |
| 6 how many tries, 8 which failure is a limit | §2.5 |
| 7 the limit's word | one key (`DaorisKeys.LimitReset`) and one sentence, the two English copies (`LimitMove.cs:565`; `LimitResume.cs:19`) one constant |
| 12 a running session's hand-over | one rule, today's, written once: the person's press ends the part now (a stop, D188 point 1), an agent's ask waits for the turn's end |
| 13 the door out of an account's wait | with §2.2 the wait happens only where nothing is ready; its door is D206 §3.7's *Go on now with…* for both kinds, the chat's fresh *Start a conversation* kept as the person's own press |

## 3. What is said: the event

### 3.1 The event, and which channels are projections (question 11)

**One envelope, two minters.**

| | Record events | Machine events |
|---|---|---|
| What | a record of the service's moved: a session opened, moved to a state, limited, continued by a new part; a word kept or taken; a quest operation; an ask or a channel message kept | a fact no record holds: an account's wait, a person's Confirm waiting (`PipeConfirmations.cs:338`), a tool call held (`CallsStateSlice.cs:57-66`), a session gone quiet (`SessionWatch.cs:50-61`), a landing tried (`AutoLander.cs:648-672`), a quest parked by the planner, the look's news (`LookSaid.cs:160-173`) |
| Who mints it | **the service's store, by trigger, in the statement that writes the record**, into `ledger_events`, so every writer mints it: the host, the driver through it, each connector, and a connector built before the table, as the words' and asks' revisions already are minted (`Sessions.cs:665-684`; `Asks.cs:301-317`) | **the lease holder's loop**, by one teller, `HookSet.Tell`'s order as EVENTHOOK1 designs it (`docs/2026-10-24-event-hook-design.md` §4.4) |
| Its id and order | `position`, an integer in commit order under the store's write lock, under the store's identity (`ChangeStream.cs:483-494`) as its epoch | `run` and `seq` (EVENTHOOK1 §4.1, §4.3) |
| Its kind | a code of §3.2's vocabulary, written by the trigger from the row: the new state, and the end's reason as the record's own note code that says why (`ended.*`, `account.cooling-*`), which the state's move writes in the same statement (`note_parts`, `Sessions.cs:899`) and the trigger reads with SQLite's JSON functions, as the words' trigger already does (`Sessions.cs:677-681`) | a code of the same vocabulary |
| Its scope | the record's workspace, so a reader filters per workspace by data (NEEDSSCOPE1) | this home (EVENTHOOK1 §4.2) |
| Kept | durable, machine-local, never on the sync wire; bounded by its own trigger (rows older than a fixed count behind the newest are deleted in the same statement), so an older build that never trims still bounds it | memory, one run |

The change stream's families stay its wake (`ChangeStream.cs:12-61`): the journal is what a woken reader reads, so a reader learns
*what* moved, in order, rather than *that* a family moved. Records stay the truth: the journal is never replayed into state, so this is
no event-sourced database (D182's rejection).

| Channel (survey §3.1) | After |
|---|---|
| Pushed slices | stay **state**, D182's: woken by events, never carrying news |
| `STATE_CHANGED`, `DRIVER_TICK` | retire slice by slice with D182's remaining rows; `DRIVER_TICK`'s data (considered, untrusted, waits) moves into the driver and attention slices, so no key is both pushed and invalidated (§3.4 rows 3, 4) |
| `UNDER_WAY`, the confirmations' lists | stay their own whole, numbered tellings (D169's STATEHUB1c notes), state rather than news |
| Held answers | stay the third shape for reads D182 keeps on demand; forgotten by the journal's families as well as by presses, so the first ask after a ledger move answers fresh (`DriverModule.cs:109-114`, `:238-250`) |
| `DAORIS.*` bus events | `SESSION_ENDED`, `SESSION_ATTENTION`, `NOTICE`, `MESSAGE` become projections of events through one table (§3.3); the rest (`SESSION_OUTPUT`, `SESSION_STREAMS`, `TERMINAL_*`, …) are streams, not news, and stay |
| The machine log | stays the machine's measurements; its `session.*` lines carry the same codes as the events, and its twin writers stay twins (`twins.md`) |
| The session's event file | stays the conversation's own record (D189 point 1), content rather than news |
| The lineage file | a store, the participant's binding (§1.6), not a channel |
| Hook points | `session/ended` and `state/changed` become projections; `quest/consider`, `work/land`, `work/step`, `work/perform`, `work/watch` and `work/state` are calls a plugin answers, not tellings, and stay |
| Fabric events, the watches and the chain | the sources of machine events and the wakes of judges; ranks 3 and 4, declared and bound nowhere (`Chain.cs:170-172`), are bound to the journal's `session.ended` and the publish or removed, by a decision (§5.3) |
| Notifications | the news projection (§3.3); `data/notices.json` its one durable record, written by one writer (today three, §3.4 row 10) |
| The ask's channel | stays its own store: D188's messages, D167's channel, which no event replaces; its quest lines use the quest log's operation codes (§3.4 row 8) |
| The quest log | is the quests' part of the journal: its insert mints the `quest.*` event |
| Presses told, `ReopenTelling`, `HeavyTold`, `AskMessages` | presses told stay the terminal's channel into the loop; the others become events where they tell news and stay where they tell an agent |
| EVENTHOOK1's relay | a projection of `work/event` (§4) |

### 3.2 The vocabulary (question 12)

The vocabulary exists already as the notes' codes: a record's note is coded parts with values, worded by the page from its catalogue in
the reader's language, its English the writer's text (`NoteCodes.cs:26-38`, `:200-268`), and `NoteCodesTests` holds every code worded in
both catalogues. The end's reasons are already about forty closed codes (`ended.stopped`, `ended.stopped-steward`, `ended.transient`,
`ended.handed-over`, `ended.driver-closed`, …; `NoteCodes.cs:401-506`). So an event's kind is a short code of its own, `EventCodes`, beside
`NoteCodes`, and its values name the note's code where one says why:

| Kind of change | Code | Values |
|---|---|---|
| a part opened | `session.opened` | `kind` (the participant's), `entry` |
| a line continued in a new part | `session.moved` | `from`, `why` (`press`, `near`, `limit`, `agent`, `curve`, `resume`, `carry-on`, `retry`), `by`, `how` (`brief`, `carry`) |
| a park | `session.parked` | `for` (`question`, `go-ahead`, …, the person-waits kinds, `person-waits.md`) |
| a park answered | `session.answered` | `by` |
| an end | `session.ended` | `state`; `reason`, the note's `ended.*` code; `cause`, derived from the reason by one table: `person`, `role`, `daoris`, `agent`, `limit`, `transient`, `choice`, `unknown` |
| a limit | `session.limited` and `account.waits` | the reset as a moment; never the account's name off this machine |
| a word | `words.kept`, `words.taken` | the word's id, `by`, `level` |
| a landing | `work.landed` | `code` (`landed`, `advanced`) |
| a wait on the person | `person.waits` | its kind, a confirmation's id |
| a quest's operation | `quest.<op>` | the quest log's operation names, which the channel's lines take too |

The four meanings of *ended* (survey §3.4 row 1) become one event and four filters written in one table (§3.3): `SESSION_ENDED` every end,
`session/ended` every end the loop drained (today's set), the log's `session.ended` every end, *Ended* the ends whose `cause` is
`agent`, `daoris` or `unknown` and no quest park says (today's suppressions at `Attention.cs:246-252`). The cause `unknown` is a record from
before codes, or a writer that wrote none: what nothing said stays unknown (D173's guard), and the *Ended* filter keeps today's reading
for it.

**Where a sentence is composed.** On the page from the catalogue, both languages, as notes are (D116; `translation-parity.md`); on the
terminal and in the log in Daoris's English, from the same table's text; to a plugin and to an outside agent **never a sentence**, the
code and ids alone (EVENTHOOK1 §4.3). No reader parses a sentence back into a code.

### 3.3 The readers (question 13)

| Reader | May read | Filter, written once | Never |
|---|---|---|---|
| The page (every window of this machine) | every slice; the news projection; records of this machine with their machine-only fields on loopback | the slices' allow-lists (D182 point 2) | a credential; a machine path outside a desktop-only slice |
| The person's notifications (bell, balloon, toast, taskbar) | today's news, from one table: `session.parked`, *Ended*, `session.answered` of a turn their words started, `account.waits`, a held call (*Holding*), a Confirm waiting (*Confirming*); a quiet session stays a row of *What needs you*, as today (survey §3.3) | the notify switch gates **news** alike; a Confirm waiting is a wait on the person, not news, and stays ungated (D196: its signal kept while unshown); a press the person made is never told back to them (`Attention.cs:124-127`) | — |
| A plugin on `session/ended` and `state/changed` | ids, codes and today's frame fields (`HookPoints.cs:115-125`), now with `reason` and `cause` | a plugin the lease's loop started, as today | words, notes, names |
| An outside agent's session | `work/event`'s frames | EVENTHOOK1's kinds, this home's scope, ids and closed codes (EVENTHOOK1 §4.3) | a name, a word, a path, an account |
| The terminal | its verbs' answers through the pipe, the records, the log | the pipe's census (D171) | another home's records |
| A teammate's mirror | the records that travel, their travelling fields (`session-record-fields.md`) | the sync wire | `said`, the marks, `actor`, `from`, the journal, move records, notices, the log, the lineage |

**Never leaves the machine:** the journal, move records, notices, the event file, the lineage, the log, `said`, a participant's
binding, every account's name, every path.

### 3.4 Durability and loss (question 14)

| What | Kept | A restart | How a reader knows it missed something |
|---|---|---|---|
| Records, the journal | durable, the service's database | nothing lost | a cursor below the journal's floor, or another store identity: the reader reads its records whole and says it did |
| News | its said-set durable in `data/notices.json`, and its journal cursor beside it | resumes from its cursor, so a park made just before a restart is told after it (today dropped by the first look's baseline, `Attention.cs:224`; `SessionLog.cs:38-41`) | the floor, as above |
| Machine events | memory, one run | a new `run` | a new `run`, a `seq` gap, `missed` (EVENTHOOK1 §4.4) |
| Slices | memory, rebuilt from the truth | a new epoch | the epoch and the manifest (D182 point 3) |
| Move records | durable, `<home>/sessions/` | recovered at the come-up | — |
| Held answers, `UNDER_WAY`, the confirmations' lists, the attention watch's memory, the hook queue | memory | rebuilt or empty | the read asked again; pipe-waiting is its own record (`PipeWaiting.cs:39-62`) |

**One rule for a first read** (survey §3.4 row 6): the baseline is replaced by the said-set. A piece of news is told once where the notices
record does not hold it as said, whether it is found at the first read or later, so a park or a held call standing across a restart is told
once and never twice (today the attention watch, the notices slice and `AskMessages` take their first read as a baseline,
`Attention.cs:224`, `NoticesStateSlice.cs:361`, `AskMessages.cs:86-103`, and *Holding* takes it as news, `HeldCallAnnouncements.cs:22-23`).
Only a home with no said-set yet, the first run of the build that adds it, takes what stands as said: today's first-look rule kept for
that one case.

### 3.5 Where today's parallel paths go

| Survey §3.4 row | Goes to |
|---|---|
| 1 what an end is, 8 one fact several codes | §3.2 |
| 2 who tells a park, 6 a first read | §3.4: the journal mints the park at the move, whoever moves it, so the two comments that disagree (`SessionLog.cs:34-37`; `Attention.cs:119-122`) are both retired by one source |
| 3 push or invalidate, 4 the look's facts twice | §3.1's slice rows |
| 5 the notify switch | §3.3 |
| 7 six orderings | two: `position` for records, `run`/`seq` for machine facts; slices keep `epoch`/`seq` as state's own version |
| 9 recorded and read by nothing | the lineage gets its reader (§1.6); the chain's ranks a decision; `said` pushed by no slice stays pull-only (D182's rejection of pushing bulky detail), its `words.*` events told |
| 10 two writers of one line | notices one writer; the log's twins stay twins |
| 11 where the shell is not the driver | machine events come from the lease holder, so a shell waiting on another loop's lease tells none, which is right; record events come from the journal, so a waiting shell's windows hear the ledger through the change stream it must then watch (today it builds none, `DriverLoop.cs:841-842`, *unverified* that its ledger-fed slices reconcile only at the manifest) |

## 4. The designs in flight

| Design | Becomes | How it folds in |
|---|---|---|
| **LIMITSWITCH1** (D137's note of 2026-10-25) | built as the account rule's first row (1a) | built inside the foundation, as designed |
| **AGENTERRORS1** (D210) rows 1b to g | the course's `Failure` input; its rows unchanged; the chat's and the brainstorm's limit reads join them (1d) | built as designed inside |
| **MODELROLE1** (D205), built | its `Effective` and walk become the start plan's (1m, 1n) | moved onto |
| **EVENTHOOK1** | its `work/event`, `WorkEventGrammar`, frame and relay are the first projection of events to an outside reader and the first instance of the closed vocabulary; its four kinds come first from the attention watch as it designs; then its session kinds read the journal (1l), so its `session.parked` and `session.ended` are minted at the move and survive a restart, while `work.landed` and `quest.parked` stay machine events under its own `run` and `seq` | built first as designed, then moved onto |
| **COWORKVIEW1** row a | the first production reader of the lineage, the participant's `subagent` half (§1.6) | built as designed inside |
| **COWORKVIEW1**'s routing half (rows e, b, c, d, f) | reads `Participants.Of` and the line's head (§2.4 rule 1) instead of its own current-executor selector (its §3.1, built on `LastRun`'s rule), and the event codes instead of reading waits from end codes | re-cut |
| **SWITCHCTX1**'s carry (rows a onward) | its carry record is the move record's `carry` phases (`copied`, `verified`); the record lands first, with no copy, on the curve's move, the carry's own first path (1j), so SWITCHCTX1a adds two phases and its read-back to a record that already owns delivery and recovery on every move; its line view (1c-i), accepted, is built as designed | re-cut |

## 5. Across the three

### 5.1 Records and owners (question 15)

Every record of the survey's §4, its one owner after, what travels, and what an older build reads.

| Record | Owner after | Travels | An older build |
|---|---|---|---|
| `sessions` (with `said`, the marks, `from`, `entry`, `limited`, the actor's fields) | the service, unchanged | as today: the record and its keys, never `said`, the marks, `actor` or `from` | reads it as today; `actor` answered for its records by §1.2's rule at the read, never written |
| `ledger_events` (new) | the service, by trigger | never | never reads it; its writes mint rows through the triggers, and the triggers bound it |
| `quests`, `quest_log`, `asks`, the channel, `opinions`, the revisions, `store_identity` | the service, unchanged | as today | unchanged |
| A helper's ask and answer, a question, an agent's hand-over ask, a brainstorm's request, a partner's answer | the service, unchanged | no | unchanged |
| A helper's state, capture and back; a hand-over ask's state | the driver library, unchanged (one concept split by owner, survey §4, kept: the ask is the service's, its handling the driver's) | no | unchanged |
| `<s>.harness.json` | the driver library, the binding's conversation | no | unchanged |
| `<s>.move.json` (new) | the driver library | no | never reads it; the old boxes it writes beside keep its moves recoverable |
| `<s>.limit-move.json`, the hand-over box, `data/handed/<quest>.json` | the driver library, written from the move record as adapters, then retired (1s) | no | reads them until retired |
| `<s>.new-session.json`, `<s>.cannot.json`, `<s>.reopened.json` | the driver library: the first a press, the others marks the course's executors write | no | unchanged |
| `held-words.json` | the modules, for words said while a record winds up only; a move holds nothing there (§2.4 rule 3); a word's `Choice` and `Key` kept | no | its older shape still read |
| `cooling.json`, `data/agent-offers.json`, `driver.json`'s rules and actors | the driver library, unchanged keys | no | unchanged |
| `data/notices.json` | **one writer**, the news projection in the lease holder's loop (today the slice, the look and the headless host) | no | unchanged shape |
| The event file, `data/sessions/<id>/lineage.jsonl` | the driver library (the lineage's four appenders are C1a's by design, one fold reading it) | no | unchanged |
| Appointments, holder rooms, supervisor decisions, the steward's acts, pipe-waiting, `running.json`, `.told` | the modules, unchanged until 1r merges the decisions and acts into one act record | no | unchanged |
| The machine log | its twin writers, unchanged | no | unchanged |

### 5.2 The names (question 16)

None of these is a new word on the page (D193's rejection; brief §4, *Simple*). They name code and records, and prose.

| Term | en | zh | Home | Code name | Changes |
|---|---|---|---|---|---|
| Fabric | Fabric | 协作织网 (prose only) | D173 | none | prose only: the three concepts together |
| actor | actor | 角色 | glossary | `Actor`, `ActorChoices` | none; the participant's kind is its actor |
| role | role | 职务 (prose only) | D208, D209; the page names each role (主管, 管家) | `Authority` on the participant | prose only; never 角色, which is actor's |
| participant | participant | 参与者 (prose only) | D173 ADR-B | `Participant`, `ParticipantId`, `RuntimeBinding` | new code names, never shown |
| line | line (of work) | 会话 on the page | D167; the glossary's *line* (主线) is the branch, so the page keeps *session* | `Entry` (`SessionEntries`), never `Line` (`LinesStateSlice` is the branch's) | prose only |
| part | part | 段 | glossary | unchanged | none |
| move | move | 接续 (as `note.moved.from` says it) | D189 | `Course`, `Move` | new code names, never shown |
| event | event | 事件 | the plugin kit's glosses (`plugin.kit.kind.workStep`) | `EventCodes`, `ledger_events` | new code names; the page shows notices, never *events* |

### 5.3 The disagreements this settles, each a decision

By the brief's §7.3 each unification states which answer stands, as a dated note on the decision it amends, in the row that makes it.

| # | Question (survey) | Today | The answer that stands | Note on |
|---|---|---|---|---|
| 1 | the own account cools: wait or move (§2.4 row 1) | four sites wait | go on now where an account is ready (LIMITSWITCH1) | D137 point 7, already noted |
| 2 | a pick at a same-agent limit move (§2.4 row 5) | read nowhere | it waits for its own account | D206 point 5, a conformance note |
| 3 | the earliest reset (§2.4 row 11) | two computations | over exactly the accounts the line may take | D206 point 2 |
| 4 | words during a move (§2.4 row 3) | box, hold or none | kept on `said`, delivered after the open | D188 point 6; D167 point 1 |
| 5 | a stored kind or marks (§1.3 row 8) | both | stored `actor`, derived once for records from before | D203 point 1 (proposed), which reads the role and never stores it: the stored actor is D205's, accepted |
| 6 | Ask Daoris asks a helper (§1.3 row 6) | allowed by the helper's copies, refused by the partner's | refused | D203 point 10 |
| 7 | a caller the connector cannot place (§1.3 row 16) | passes | mutations refused, reads served | C2a's settled rule; a note on D199 for the role filter |
| 8 | a reviewer's and a voice's mutations at the server (§1.3 rows 7, 15) | pass | refused by the table | D155, D176 |
| 9 | a park or a held call standing across a restart (§3.4 rows 2, 6) | a park dropped by the baseline, a held call told again | told once, by the durable said-set | D196, and the watch's SURF5b note |
| 10 | `SESSION_ENDED` for driven ends (survey §3.3) | never sent | sent for every end; the usage slice wakes at each (`UsageStateSlice.cs:47`) | D182, the usage slice's PUSH1e note |
| 11 | the notify switch and a Confirm (§3.4 row 5) | not gated | stays not gated: a wait, not news | D196 |
| 12 | the chain's ranks 3 and 4 (`Chain.cs:170-172`) | bound nowhere | bound to the journal's end and publish | D173's FABRIC-CHAIN1 note |

### 5.4 What is the owner's (question 18)

Nothing in this proposal needs D191 point 3's answer: it changes no direction (the owner's FOUNDATION1 direction, the brief §1, asks
for exactly this structure), spends no account beyond the development's own runs, needs no sign-in, publishes, pushes and rewrites
nothing, and loses no data (the journal's bound trims rows the records still hold; the old boxes are retired only once no build reads
them). Two points come near and are named so the steward can check them: row 1r grants authority (one act form for two roles), so it is
not accepted on one maker's read (D191's note of 2026-10-24); and §5.3's rows 6, 8 and 9 change what a person is told or what an
agent may call, each a narrowing or a fix, none a new direction.

## 6. The migration, in build order (questions 17 and 19)

Each row is its own branch, lands with every gate of the brief's §6 green, leaves a person who sets nothing seeing Daoris work as
today, and adds its own proof. A row that introduces a concept reads it beside today's mechanisms first and moves no reader; a row that
moves a reader moves one. **The first row removes the most defects for the least change**: one pure function and four call sites remove
§2.4 rows 1, 5 and 11, the class of LIMITSWITCH1, LIMITRESUME1b, LIMITRESUME1c and LIMITGOON1 (survey §2.6), and build a decision the owner
already made.

### The first three rows

**FOUNDATION1a — one account rule, LIMITSWITCH1 inside it** (driver, web; the account rule of §2.2).
- *What*: `CourseAccount.Decide(own hold, allowed accounts by the walk's order, pick, handed, limited, new session) → Resume | Elsewhere(account,
  choice) | Wait(earliest)`, pure, beside `ResumeAccount` (`Accounts/Rotation/`). `ResumeChoice.Waits` (`ResumeAccount.cs:78`) and its twin
  (`HarnessRoster.cs:1008`) read it; the four wait sites (`Driver.cs:2031-2039`, `ChatGoOn.cs:573-579`, `:674-680`, `LimitWaits.cs:185-186`)
  act on its answer; the earliest reset is one computation over the allowed accounts. A conversation's *elsewhere* goes on through the
  ended limit move's body (`LimitMove.MoveEndedAsync`, `LimitWaits.cs:189-191`), its precondition widened from a limited record to an own
  account that cannot carry, so a chat's words go on in one briefed part instead of `CannotGoOn` (`ChatGoOn.cs:581`). The pick waits in the
  limit move's same-agent move (`LimitMove.cs:85-104`).
- *Census*: a test over every caller of `ResumeAsync` and `CoolingOf`/`CoolingsOf` fails on one that decides wait or move itself.
- *Proof*: LIMITSWITCH1's (`TASKS.md`): a stand-in record whose own account cools with another ready goes on there in one briefed part, the
  words once, driven and conversation alike; with none ready it waits and says the earliest reset of any; the held row in both languages.
  A pick waits. The suites whole: `ContinuationTests`, `ChatGoOnTests`, `ChatGoesOnProcessTests`, `LimitResumeTickTests`,
  `LimitWaitsProcessTests`, `AccountRotationTickTests`, `SessionMessagesTickTests`. Installed: the next real cooling own account's words go
  on, read in the machine log's `session.moved why=account` and the record.

**FOUNDATION1b — words follow the line's head, at the say door** (service, driver; §2.4 rules 1 and 2).
- *What*: `SessionLedger.SayAsync` resolves the head inside its exclusive step (`SessionLedger.cs:931-973`), by the `from` chain for a
  conversation and the quest's last run here for a quest's session, and answers today's `continued` refusal with the head named (an
  optional last member of the answer, so an older driver reads the answer it read). The three moves that deliver words today read them
  after the new record's open: the ended and live limit moves (`LimitMove.cs:157-166`, `:249`, `:266-270`) and the driven carry-on, whose
  words are read from the park before its record opens (`Driver.cs:2386-2402`). `WordsNever.ContinuedIn` stays as the fallback for an
  older host.
- *Census*: the say door's twin tables (the service's and the driver's `ContinuedIn`) held cell for cell, a chain's cycle ending where it
  repeats, a teammate's record continuing nothing.
- *Proof*: first, the survey's *unverified* gap made a test: a word said to a live limit move between the old part's end and the new open
  lands once on the new part; LIMITWORD1's, HANDWORDS1-i's and SAIDBACK1e's cases kept green. The suites whole: `SessionLedgerTests`,
  `SessionStoreTests`, `DrivenInboxTests`, `ChatGoOnTests`, `HandOverTests`, `CurveMoveTests`. Measured on the install: the say door's
  latency before and after, since it is the dispatcher's hot path (brief §7.5).

**FOUNDATION1c — the participant's kind, read once** (service, driver, web; §1.2).
- *What*: the service answers `actor` for every record of this machine, stored or by `ActorOf` for a record from before, never written back;
  `ActiveSessions.HoldsSlot` (`Planner.cs:872-885`) reads one table, kind to *holds a slot*; the page's `identity.ts` reads `actor`, so a
  partner reads as a partner. The `Participant` type is introduced with its census and read by nothing else yet.
- *Census*: every reader of `ask`, `opinion`, `serves`, `piece`, `helpMode` or `daoris:help` outside tests is listed as reading a kind (moved
  or named not yet moved) or a relation; the eight actors' ids one list (`ActorChoices.cs:37`, `ActorRun.cs:176-177`).
- *Proof*: `SessionStoreTests` (an elder record answered its derived kind, a mirror none), `SessionsStateSliceTests`, `PlannerTests`'
  CAPCOUNT1 and SESSBADGE1 cases, ASKRAIL1's rail case, the page's identity tests with a partner record. `McpToolsTests`,
  `ActorChoicesTests` and `SessionLedgerTests` whole.

### The rest, in order

| Row | What | Proof |
|---|---|---|
| **1d** failures read once on every path | AGENTERRORS1's remaining rows; a conversation's limit and a brainstorm's read by the reader's code (`ChatRunner.cs:2670-2675`; `DriverBrainstorms.cs:778`); a conversation's limited record written as the ledger's flag at its end (`LimitHeld.cs:17-56` read only for the refused turns) | `AcpTransientTests`, `TransientRetryTickTests`, `LimitResumeTickTests` whole; a chat's limit by code alone moves it; AGENTERRORS1's C4-6 case on a conversation |
| **1e** the ledger's journal | `ledger_events` and its triggers on `sessions`, `quest_log`, `asks` and the channel; read by nothing | each ledger move once, in order, with its code; an older build's own insert and update statements, run in the test, mint rows (`compatible-builds.md`); the bound holds; the install's write latency and CPU before and after |
| **1f** one end | `session.ended {state, reason, cause}` and §3.3's filter table; `SESSION_ENDED` (`DriverLoop.cs:1228-1237`), `session/ended`, the log's `session.ended` and *Ended* each read their filter | `AttentionTests`, `NoteCodesTests`, `PluginKitTests`, `StatePublisherTests` whole; a driven end wakes the usage slice; LIMITRESUME1a's case; the cause table both ways against `NoteCodes`' `ended.*` |
| **1g** the course, beside | `Course.Decide` over paths A to N, reproducing today's answers, read by nothing; each place two paths differ on the same facts listed as a decision | the paths' own suites' cases ported as its table; the trigger-site census |
| **1h** conversations onto the course, one row each: the curve; the live and ended limit moves; the wake; the go-on; the briefed move and go-on-new; the hand-over | each path calls `Course.Decide`, its old decision the course's adapter | the path's suite whole (`CurveMoveTests`, `LimitWaitsProcessTests`, `ChatGoOnTests`, `ChatGoesOnProcessTests`, `HandOverTests`, `HandOverLookTests`); the census's *not yet moved* list shrinks by one |
| **1i** driven lines onto the course, one row each: the go-on and the limit resume; the quest's hand-over; the carry-on; the transient past its bound; the send-back | as 1h | `ContinuationTests`, `SessionMessagesTickTests`, `LimitResumeTickTests`, `PlannerTests`, `TransientRetryTickTests`; the family rehearsal |
| **1j** the move record | `<s>.move.json` with its phases and the come-up's recovery; first on the live limit move, the curve and the driven carry-on (no durable record today), then the hand-over and the ended limit move with their boxes as adapters; the chat's `Hold()` replaced by keep-and-queue for a move (§2.4 rule 3) | a shell killed at each phase comes up and delivers each word once (a Process case per phase); `HandOverProcessTests`, `LimitWaitsProcessTests` |
| **1k** news from the journal | the attention watch's park and end read the journal by a durable cursor; one writer of the notices; the said-set in place of every first-read baseline; the notify rule in one table | a park made just before a restart is told once after it, a held call standing across one told once; nothing told twice across restarts; `AttentionStateSliceTests`, `AttentionTests`, `LookSaidTests`; the look's time on the install before and after |
| **1l** EVENTHOOK1 onto the journal | after EVENTHOOK1's rows land as designed, its session kinds read the journal | its grammar census and relay proofs whole; a restart loses no session event |
| **1m** one start plan | §1.4's table, one start at a time | `ActorChoicesTests`, `HandOverTests`, `HelpCoverageTests`; a carry-on runs its record's agent and choice |
| **1n** one walk | four walks into one, one pass-code set, one re-ask (`OneTurnReask`'s shape) | `ActorChoicesTests`, `HelperLookTests`, `OneTurnReaskTests`, the intake's and the brainstorm's suites |
| **1o** one rule shape and one admission | `KindRule` over today's keys; the three admission tables and their twin test; §5.3's rows 6 to 8 | `McpToolsTests`, `McpRoleFilterTests` (every tool × every kind), `HelperJudgeTests`, `PartnerJudgeTests`, `DriverConfigKeepsTests` (no key moved) |
| **1p** the participants read | `Participants.Of(entry)`; COWORKVIEW1 row a, built as designed, reads its child half; its routing half re-cut onto it | `ChildLineageTests`, `LineageHookProcessTests`, COWORKVIEW1 row a's proofs |
| **1q** push only | with D182's remaining rows: `DRIVER_TICK`'s data into slices, `STATE_CHANGED` for unmoved scopes and plugins alone, held answers forgotten on the journal's families | `pushedState.test.tsx`, `bridge/state/*.test.ts*`, `StateSlicesTests`; bridge reads per minute on the install with two windows, before and after (D182 point 8) |
| **1r** one role door | the supervisor's and the steward's acts on one form and one proof (`SupervisorProof.cs:284-310` against `RoleActForm.cs:48-63`), the holder a participant of kind `holder`, decisions and acts one record; **both makers' reads before acceptance** | `PipeCensusTests`, `StandingConfirmationTests` whole; a role's act never read as the person's |
| **1s** records last | the old boxes, the hand-over hold in `held-words.json` and the driver's `ContinuedIn` fallback retired once no build in the update's rollback window reads them | an older build's statements over the new shapes; `SessionHomeFiles`' inventory and its kept names |

## 7. Rejected

| Rejected | Why |
|---|---|
| A `participants` table, or a session record per brainstorm voice or child | two authoritative stores for one concept (D173's first guard); C1a's audit rejected a record per child (`D173.md:91`) |
| Keeping the kind read from the marks, as D203 point 1 has it | 27 readers and the cap's predicate are where CAPCOUNT1, ASKRAIL1 and SESSBADGE1 came from (survey §1.6, §3.6); the actor is stored already by an accepted decision (D205) |
| One loop for driven lines and conversations | a rewrite of both loops at once; the decision is what disagrees, and a pure judge both call removes the disagreement without moving the executors |
| The service owning the move record | a move ends a process, spawns a harness and reads an account's home, all the desktop's (D47 §4); the service needs only the `from` link, which it has |
| Minting record events in the driver's `ServiceClient`, as `SessionLog` writes its lines | misses a connector's write and a second process's; a trigger mints for every writer, an older connector included, as the revisions do |
| An event-sourced database, or replaying the journal into state | D182 rejected it; records stay the truth and the journal says what moved |
| One bus for state and news, or events replacing pushed slices | state is pushed with its data (D182 point 2); news is said once; they are two jobs |
| Words or names in an event a plugin or outside agent reads | EVENTHOOK1 §4.3's rule: a name may be shaped like a path and a word is a prompt someone else wrote |
| A new key per kind in `driver.json` for the one rule shape | an older build reads today's keys; one reader over them changes nothing it reads |
| Moving the hand-over's ask into the move record | the ask is the agent's request through the connector, the service's store (`HandOverAskBox.cs:28`); the move begins at its approval |
| A message or channel post doing a move's or a quest's job | D167's rejection of replacing quests by messages; D188 point 4 |
| Showing *participant*, *move* or *event* on the page | D193's rejection of a new vocabulary on the page |
| A model deciding a course or classing an event | D24; D157's declared facts |
| A timer per wait, or a second loop for conversations | D206's rejection; the look is the wake |
| Building SWITCHCTX1's carry record as a record of its own beside the move record | two records of one move in flight would be two owners of one delivery |

## 8. What stays unmeasured

- **The triggers' cost** on the `sessions` table under the install's real write rate, and the journal's size at its bound: 1e measures
  write latency and CPU before and after. That every end's move writes a reason code a trigger can find in `note_parts` is read from the
  state's statement (`Sessions.cs:899`) and the codes' list, not proven for every writer; a move that writes none is told with `reason`
  absent and `cause` `unknown` (§3.2).
- **The head's cost at the say door**: a chain of `from` reads per word, with no index on `from_session` known (*unverified*); 1b measures
  it and adds the index if needed.
- **Whether the course reproduces every live combination**: it is proven on the cases the paths' suites hold and on the census, not on
  every state a line reaches on the install.
- **The live limit move's word gap** (survey §2.4 row 3, *unverified* that it loses one): 1b's first test.
- **News after a long downtime**: whether a cursor that resumes after a week tells too much at once; 1k bounds it by the notices' own week
  (`Notices.cs:32-35`) and measures it.
- **A brainstorm voice resolved at the connector** through its box in time for its first call (*unverified*).
- **C2a's lineage join on the protocol doors** (`D173.md:131-133`: not measured), which the admission's child column waits on.
- **How often each move happens on the install**, which sizes 1j's recovery cases; read from the machine log's `session.moved` lines
  before 1j.

## 9. What this proposal's checks do not cover

A document only: `doc-links`, `doc-system` and `orient-index --check` check its place, links and the index, never its claims. The code
was read, not run, at `f280761cf`; every line cited was read there or taken from the survey, whose sample the steward checked (survey §6).
The statements about the in-flight designs are read from their documents at this commit, and SWITCHCTX1's carry, COWORKVIEW1's routing
half and EVENTHOOK1 are proposed, not built.
