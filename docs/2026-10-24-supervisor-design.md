# The supervisor: the first role the owner appoints, answering a session's question on their behalf, and the question card it answers on (SUPERVISOR1, QUESTIONUI1, 2026-10-24)

**Status: proposed (D208), round 1.** Written by Claude Opus from the code at `478e6010b`, for the reads D191 asks: Codex's, and an
independent Opus reader's foundation read (D191's amendment of 2026-10-23), since it changes D156's person's door, D201's judge and
record, and the session record's authorship (D188's `SaidFrom`), which other designs rest on. Nothing is built. No read has been
taken yet; §13 keeps their table.

This design serves three parts of D193's company. **The owner** (part 1): each wait on the owner should be one only they could
answer, and today a session's every question waits for them, though most are choices the company's own records settle. **Management**
(part 18): the steward already settles what a session meets that is not the owner's (HEADFIT1b's, D193:17); here that settling gets
a door of its own, recorded as its, never as the owner's. **The records** (part 13): who decided, and on what, can be read and
overruled.

The direction (the owner, 2026-10-24, through the quest that asked for this design): a session's question needs a better option
selection, and a supervisor decides among the options and replies, as part of the workflow; until it is built the steward plays the
role. The frame is the company's (D193): the person is the owner and gives roles; a role acts on the owner's behalf within its scope, by
its holder's own decision; the one grant in the window is the owner appointing the role, not a confirmation of each act; each act is
recorded as the role's, on the owner's behalf, never as the owner's own words, so the owner can read who decided and overrule.

Read for it: D193 and `docs/2026-10-21-company-concept.md`'s frame; D188 and `docs/2026-10-23-coworker-design.md` (the co-work
roles); D205 and `docs/2026-10-23-actors-design.md`; D135 (KNOWUSE1-2, PARKVIEW1's note); D156 and
`docs/2026-10-09-person-door-design.md`; D185 with PARKVIEW1, PARKWORDS1 and WAITPANEL1, and `docs/2026-10-22-park-ask-design.md`;
PARKANSWER1 (`docs/FIX-LOG.md`, the archive); D201 and `docs/2026-10-23-terminal-trust-design.md`; D191; the UI/UX design §4;
`.claude/knowledge/person-waits.md`, `session-words.md`, `bridge-presses.md`, `translation-parity.md`; the glossary.

## 0. What is true today

### 0.1 A question, asked and answered

| Fact | Read at |
|---|---|
| A driven session asks the person through the connector's `question_ask`: `question` (1-1,000), `choices` (0-6, each `label` 1-80 and `does` 1-300, labels unlike in any case), `recommend` (a choice's number) and `by`. The recommendation's reason has no field: the tool asks the agent to say it in that answer's `does` | `Daoris.Service.Mcp/QuestionTools.cs:17-41`; bounds `Sessions/QuestionBox.cs:24-36` |
| It is kept as `<home>/sessions/<session>.question.json` `{ id, session, question, choices, recommend, by, at }`, one per session, machine-local, gone as each run of the record opens and kept at a run's end only where it parks | `QuestionBox.cs:50`, `:104-122`; `Driver/Sessions/Questions/QuestionFiles.cs:27-60`, `:115-137` |
| The page reads it through `SESSION_QUESTION`, none unless the record is parked; a read, `Open` in the pipe's census | `Modules/Bridge/Routes/SessionsRoutes.cs:365-387`; `Platform/PipeCensus.cs:192` |
| A press answers it through `SESSION_INPUT { id, text, question }`, `text` the label then the person's words after a blank line; refused `question-changed` where the file's id is not `question`; kept on the record's `said` as the person's words, the event it is shown under marked with the `choice` | `Bridge/Routes/ConversationRoutes.cs:116-139`; `Loop/SessionWords.cs:373-453`, `Word` at `:179` |
| The resumed run is handed `chose: <label>`, then the words | `QuestionFiles.cs:179-198` (`QuestionWords`); `Quests/Domain/Planner.cs:650-660` |
| A parked record with any word waiting goes on at the driver's next look | `Planner.cs:355` (`WordsWaiting`), `:1461-1472` (`WordsOn`) |

### 0.2 Who said it

A word on a record is the person's by the absence of an author: `Persons => By is null && Helper is null && From is null`
(`Daoris.Service.Core/Sessions/Sessions.cs:386-392`; the driver's view, `Planner.cs:663-666`). `From` names Daoris (`daoris`) or another
agent's claim passed on from a channel (`agent`), and the say door refuses any other (`SaidFrom.Known`, `Sessions.cs:395-408`). A
word that is not the person's goes on only in its own session: words that are all another's never carry a park on into a new one
(`Loop/Driver.cs:2144-2152`); a resume is handed the person's and Daoris's (`DriverContinuations.cs:784`), and says the person's as
theirs and Daoris's as Daoris's (`Agents/Adapters/Adapters.cs:724-735`). The ledger's note says whose words a record went on with
(`SessionLedger.cs:1625-1638`). Nothing names a word said on the owner's behalf by anyone else.

### 0.3 The terminal

`daoris-driver sessions say <id>` is `SESSION_INPUT` (`Driver/Platform/InstanceRoutes.cs:175-176`), sent without `question`
(`Sessions/SessionsCommand.cs:505-526`), so a terminal's answer is never a choice. The census makes it the person's
(`PipeCensus.cs:228-229`), so it waits for the window's Confirm (`Platform/InstancePipe.cs:603-640`; D156 point 4), and once
confirmed it is the person's words. A steward answering a session's question for the owner today either waits on the owner's press,
which is the act it meant to spare them, or is recorded as the owner.

### 0.4 D201's standing confirmation

`StandingConfirmation` (`Modules/Platform/StandingConfirmation.cs`) holds four presses (`StandingPresses.Built`, `:28`), judges a
terminal's press under one lock (`Judge`, `:399-440`), places the caller by its process outside every agent job a Daoris made
(`StandingCaller`), keeps its record at `data/standing-confirmation.json` bound to this machine and account (`:102-106`, `:241-269`,
`:521-535`), and is granted only by `SET_STANDING_CONFIRMATION`, `Window` in the census (`PipeCensus.cs:267-269`). The pipe asks it
before the card (`InstancePipe.cs:603-613`), and a covered press runs inside `PipeDoor.EnterStanding()`. It is **not offered yet**:
`OfferedInThisBuild` is false (`:136`) until part (i)'s installed proof (§8 case 4 of its design) is looked at. Its form for a press
is exact: every field named, of its type and value (`IsForm`, `:462-499`). It never names words said as the person (D201 point 3).

### 0.5 The card the person answers on

- **In the box** (`work/waits/ParkAnswer.tsx`): the question is a paragraph, then one `Button` per choice (`QuestionAsked`, `:19-55`),
  the recommended one `primary` with a `Chip` *recommended* beside it, outside the button, so a screen reader hears the label without
  it; `does` is soft text beside. No radio semantics, no field of its own: the words come from the box's text field below
  (`conversation/SessionBox.tsx:151-152`) and go with the press (`WorkFrame.tsx:1024-1051`). Choices are disabled while go-aheads are
  open (`ParkAnswer.tsx:107`). A press sends at once.
- **In the head's card** (`work/waits/AwaitingPerson.tsx`): the same `QuestionAsked`, its choices presses only once *Answer and carry
  on…* is open (`:136-141`), the words from the card's own text area.
- **Stories**: neither file has stories of its own. `ParkAnswer` is drawn through `conversation/SessionBox.stories.tsx`
  (*ParkedAsking* and its widths, Chinese and dark; *ParkedOnAProductionGoAhead*); `AwaitingPerson` only in
  `work/attention/attention.stories.tsx:823-841`, with no question.
- **The notice**: a park raises one `Parked` attention event (`Driver/Quests/Domain/Attention.cs:228-236`), worded from its note,
  never its recorded question; *What needs you*'s row `parked` likewise (`Modules/Loop/NeedsAPerson.cs:259-265`).

### 0.6 What earlier decisions say about a model answering

- D135 point 6: a model's review "never answers in the person's place until" a replay shows it classes questions as the person did;
  its rejection list holds *a model that answers parked questions from the knowledge, unmeasured*. Its evidence: of 46 items that
  reached the owner, 25 needed them (13 of those go-aheads on a production write) and 21 did not
  (`docs/2026-10-03-knowledge-use-evidence.md`, finding 2). The bench is
  `tools/question-bench.mjs` (KNOWUSE2), whose cases are classed K, R, B, O, D, S, with O *only the person*.
- D193 rejects *a hierarchy of agents directing agents*; the co-worker design rejects a coordinator agent for the same reason
  (`docs/2026-10-23-coworker-design.md` §3.4).
- D188: a solicited answer may reopen its asker once per ask, and a line goes on by answers alone at most twice before it waits for
  the person (COWORK1e).
- D191 point 3 and D193 point 1 name what is the owner's: direction, money beyond the company's own runs, sign-ins, publishing and
  pushing, history, and their data.

## 1. The decision in one paragraph

The owner **appoints a role** in the window, with their own press, for a scope; the role then acts on the owner's behalf within that
scope, by its holder's own decision, each act recorded as the role's and said where the owner reads it, and the owner overrules any
act before it takes effect. The **supervisor** (主管) is the first role: appointed per repository, off by default, it answers a
session's recorded question with one of the choices offered or with words, its reasons each naming its source. Its answer is kept on
the session as **the supervisor's**, never the owner's words, held for an **overrule window** in which the owner may take it back, and
then carries the park on. It never answers what is the owner's (§4): such a question waits for the owner, with the supervisor's
reason when it looked. Until the supervisor actor runs, the **steward holds the role from a terminal**, through D201's judge: a
press of its own, `SUPERVISOR_ANSWER`, covered only where the appointment names the session's repository and the caller is in no
agent's job, and never a card. The **question card** is rebuilt so each choice reads whole as a selectable row, the recommended one
with its reason, one *Send* with words added if wanted, and the supervisor's pick and reasons shown where it answered.

## 2. Roles: the appointment later roles reuse

### 2.1 What a role is

A **role** is a named scope of acts the owner hands to a holder. Each role keeps, and every later role inherits:

1. **Appointed by the owner's own press in the window**, per scope; no verb, Ask Daoris door, proposal card or `settings_*` grant
   appoints one (as D201 point 5 keeps the standing confirmation). Off by default.
2. **Its acts are named**, each a route and an exact form, as D201's presses are; a role covers nothing its table does not name.
3. **Its holder decides each act.** The appointment is not a per-act confirmation; no card is shown for an act it covers, and an act
   it does not cover is refused, never turned into a card (a card would make it the owner's confirmation of a role's act, which is
   the per-act grant the owner's frame removes, and the owner who wants to act acts as themselves).
4. **Each act is recorded as the role's**, on the owner's behalf: a mark of its own on what it wrote, never the owner's absence-of-author
   (§0.2), so no reader takes it for the owner's words, DRIFT never makes it a requirement, and the owner reads who decided.
5. **Each act is said** where the owner reads that kind of thing, and **overruled** by the owner's press while it has not taken effect.
6. **Bounded by what is the owner's** (D191 point 3, D193 point 1): no role decides it, whatever its scope.
7. **Revoked at once**, from where it was appointed and from *Notifications*; a revoke recalls no act already taken in, as D201's
   doesn't (§4.3 there).
8. **On this machine alone**, never carried by a sync, as D201's record is.

### 2.2 Its holder

A role's holder is **who decides its acts**, chosen at the appointment:

| Holder | Who | Its acts reach Daoris by | Offered |
|---|---|---|---|
| `terminal` | An agent the owner runs by hand outside Daoris (the steward today) | The role's routes over the pipe, judged by D201's judge: the caller in no job any Daoris made for an agent | Once D201's grant is offered (`OfferedInThisBuild`) |
| `actor` | Daoris's own actor for the role (D205), on the agent and model the owner chose | Daoris's own code, never the pipe: the driver starts it, reads its answer through a tool only it is offered, and records it by the same path | Once the actor is measured (§11, SUPERVISOR1d) |

The holder is named on the record of each act (`terminal` or `actor`, and for an actor its agent and the model that ran, D205's
`ran`), so a page can say *the supervisor (held by a terminal)*. A terminal holder is the steward by the owner's choice, never by
Daoris's reading: Daoris can tell only that the caller is outside its agents' jobs (D201 §3.3), and the appointment's ask says so.

### 2.3 Where an appointment lives

In D201's record, `data/standing-confirmation.json`, as an additive field beside `presses`:

```json
{ "schemaVersion": 1, "presses": ["stop"], "since": "…", "machine": "…",
  "appointments": [ { "role": "supervisor", "repository": "Daoris", "holder": "terminal", "since": "…" } ] }
```

One record, one judge, one lock, one machine binding, one log event family. `schemaVersion` stays 1: an older build's `Parse` reads
`presses` alone (`StandingConfirmation.cs:259-263`) and ignores the field, so it honours no appointment, which is the safe side. An
older build's own grant or revoke rewrites the record without the field (`Write`, `:370-380`), so a rollback that then changes its
presses drops every appointment: a narrowing, said in the row's notes. Written only by `SET_APPOINTMENT { role, repository, holder?,
on }`, `Window` in the census and refusing every door but the window's in its handler, as `SET_STANDING_CONFIRMATION`; answering
`{ done, why?, message? }` (`not-offered`, `unknown-role`, `unknown-repository`, `newer`, `unwritten`). Turning the standing
confirmation's presses off keeps the appointments, and dismissing an appointment keeps the presses: each its own tick. Read at the
instance's start and kept in memory, under the judge's lock.

A separate `data/roles.json` was weighed (§12): it avoids the rollback's drop, at a new home entry in the three twins, a second loader,
second log family and second lock. Row 1 is to be small, and a role's terminal holder is judged by exactly D201's caller look; the
record moves to its own file if a later role needs a shape D201's cannot carry.

## 3. The supervisor as an actor

### 3.1 Its acts

| Act | Route and form | What it does |
|---|---|---|
| **Answer** | `SUPERVISOR_ANSWER { id, question, choice?, words?, reasons }`: `id` a session; `question` the recorded question's id; `choice` one offered label exactly, or absent; `words` 1-4,000, or absent; at least one of the two; `reasons` 1-5, each `{ says: 1-300, source: 1-200 }` | Kept on the parked record as the supervisor's answer (§6), held for the overrule window, then carries the park on |
| **Leave to the owner** (SUPERVISOR1b) | `SUPERVISOR_DEFER { id, question, bound, reasons }`: `bound` one of §4.1's codes | Keeps nothing on the record; marks the question *left to you* with the bound and the reasons, which the card and the notice say (§4.2) |

Every field named, of its type and bound, and nothing else, as D201's forms are (`IsForm`). The route answers `{ kept, why?, goesOn? }`:
refused as data, keeping nothing, where the session is not parked here, is not on a quest, its repository is not appointed, its
question on file is not `question` (`question-changed`, as §0.1's press), a go-ahead on its ask is open (go-aheads are the owner's,
D135; §4.1), the question is marked the owner's (SUPERVISOR1b), a choice is not one offered, or the line's bound is met (§4.3).

### 3.2 What it is handed

The holder answers from the records, never a guess. Handed, in this order, each with where it came from:

1. **The question**: its words, each choice's label and `does`, the agent's recommendation and its reason (QUESTIONUI1a's field), its
   `by`.
2. **The session's evidence**: its last message whole (the closing report, its *Needs you* and *Readings*), its branch and commits
   since its base, its tree, read-only.
3. **The work order**: the quest's words, the owner's own words on its ask quoted verbatim and dated (`AskWords.ReadAsync`, as a
   session is handed them by `Driver.WithAskWordsAsync`, `Loop/Driver.cs:4141-4143`), and the standing answers for the repository
   (`StandingText.Beneath`, `Agents/RepositoryKnowledge/StandingAnswer.cs:25-48`, D135 point 3).
4. **The records**: the repository's decisions, knowledge, archive and fix log, through its own index and `knowledge_search`, and
   the code.

A terminal holder reads these itself: the question through `daoris-driver sessions supervise <id>` with no answer, which prints
it from `SESSION_QUESTION` (an `Open` read, §0.1; no terminal verb reads it today), the ask's words, the session's log under the home,
and the repository on disk. The actor (SUPERVISOR1c) is handed 1 and 3 as its prompt, and reads 2 and 4 with read-only tools in the session's tree, as a
helper does (D163: one turn, writes nothing, asks nobody).

### 3.3 What it answers with

**One offered choice, or words, or both; and its reasons, each naming its source**: a decision and its point (`D201 point 3`), a
document's path and line, a code path, the quest's line quoted, the owner's words on the ask quoted with their date, or a standing
answer. A reason with no source is refused (`no-source`), since D135 point 4 holds every item to naming its source; Daoris checks
that a source is named, never that it says what the reason claims, which the owner reads and overrules. Words, where given, are the
supervisor's own instruction to the session, as specific as a choice's `does`.

**When it does not answer.** The holder leaves the question to the owner (§4) where it is the owner's, where the records do not
answer it, where every answer would contradict a decision, or where its reasons would rest on a reading it cannot source. Not
answering costs one wait on the owner; a wrong answer costs a session's work and the owner's trust in every later one.

## 4. Its bounds

### 4.1 What it never decides

| Bound (code) | Covers | Why it is the owner's | Enforced by |
|---|---|---|---|
| `go-ahead` | A production or live-environment go-ahead, a push or a publication, an act outside the repository: every `go_ahead_ask` | D135 point 2, D191 point 3: go-aheads are held once on the ask and answered by the owner alone | Daoris: `SUPERVISOR_ANSWER` is refused while a go-ahead on the ask is open; `SESSION_GO_AHEAD` is never a role's act |
| `sign-in` | A sign-in, a credential, an account | D191 point 3; D37 | The holder's defer; the question's mark (below) |
| `access` | Widening what an agent may do: a permission, a rule, a trust, a write across, full access | D72, D74, D183; D201 §2.2 | The holder's defer; the question's mark |
| `spending` | Money or an account's use beyond the company's own runs | D191 point 3 | The holder's defer; the question's mark |
| `delete` | Deleting or losing the owner's data, rewriting history | D191 point 3; D37, D145 | The holder's defer; the question's mark |
| `preference` | The owner's own taste where nothing records it (a name they will see, a look, a priority between their targets) | D193 point 1: direction is the owner's | The holder's defer; the question's mark |
| `unrecorded` | A question the records do not answer | D135 point 5: an attributed reading is not a requirement; D193 part 13 | The holder's defer |
| `contradicts` | An answer that would contradict an accepted decision | D191 point 3: a reopened decision is the owner's to amend | The holder's defer |

**The question's mark** (SUPERVISOR1b): `question_ask` gains an optional `owner`, one of the codes `sign-in`, `access`, `spending`,
`delete`, `preference`, which the agent sets where it knows the question is the owner's; the tool's description says each. A marked
question is refused to the supervisor (`owner-question`) and its card says why. The agent's mark only narrows: an unmarked question is
still judged by the holder.

Daoris cannot read a question's meaning (D24, D157: no model judges a route); the structural bounds are what it can hold, the holder's
defer is the role's judgement, and the owner's overrule (§6.3) is the last word on both.

### 4.2 How such a question reaches the owner

As today: it stays the owner's question in the box and the head's card, and the park's notice is raised as today (§0.5). Where the
supervisor looked and left it (`SUPERVISOR_DEFER`), the card says, above the choices, *The supervisor left this to you*: the bound's
words (*a sign-in is yours*) and its reasons with their sources, so the owner starts from what it found. Where the question was marked
the owner's, the card says *Asked of you alone: a sign-in*, the agent's mark. A question nobody looked at says nothing extra.

### 4.3 How many in a row

A line goes on by the supervisor's answers alone **at most three times** before its next question waits for the owner, said why
(`supervisor-bound`): the owner's own word on the line, or their overrule, resets it. This is D188's answers-alone bound in the
supervisor's own count (COWORK1e counts a helper's answers), so a session and its supervisor never talk a line on unread. Three, not
D188's two, since each of the supervisor's answers is said to the owner and held for their overrule, which a helper's is not.

## 5. Where it is on

- **Per repository** (SUPERVISOR1a), on this machine: the appointment names a repository Daoris drives. **Per workspace**
  (SUPERVISOR1e): every repository of the workspace on this machine, a repository added later included, the appointment saying so
  in its ask. Whichever is narrower wins nothing: either covers a session of its repository.
- **Off by default**, everywhere; nothing turns it on but the owner's press.
- **Turned on** in the repository's Setup → Driving, beside its hold (where D201 shows a terminal's hold): a row *Supervisor* with
  *Appoint…*, which opens an inline ask (the UI design's `InlineConfirm`): what it answers (a session's recorded question, one choice
  or words with sourced reasons), what it never answers (§4.1, in one line each), that its answers are recorded as the supervisor's
  and held five minutes for the owner's overrule, and who holds it: *a terminal outside Daoris's agents (the steward, or any agent you
  run by hand)*, the actor once offered, with the ask's one plain sentence that any agent the owner runs by hand outside Daoris can
  answer as it (D201 §3.2's words). *Appoint* and *Cancel*.
- **Turned off** at once from the same row (*Turn off*, no ask) and from its line in *Notifications* (§6.4), its memory cleared under
  the judge's lock before the record is written. An answer already kept stays kept and overrulable.
- **Not offered** until D201's grant is (`OfferedInThisBuild`); until then the row says it is not offered yet and the route refuses
  `not-offered`, as D201's.

## 6. Recorded as the supervisor's, said, and overruled

### 6.1 On the record

- **The word.** `SaidFrom` gains `supervisor`; `Known` accepts it. The word is kept through the session's one dispatcher, as every
  word is (`session-words.md`), with `From: supervisor` and a key of the press's own. `Persons` stays false, so every reader that
  holds the owner's words apart (the ask's words, DRIFT, a park's answer as the owner's, the queue) keeps it out. Its text is the
  choice's label then the words, as a press composes them (`QuestionWords.Pressed`).
- **The event** it is shown under carries `choice`, `from: supervisor`, `holder`, `reasons` and `goesOnAt`, as PARKVIEW1's carries
  `choice` (`SessionEventLog.cs:399-419`), since the record keeps words alone.
- **What the session is handed**, at its resume: *The supervisor, appointed by the owner, answered your question on their behalf
  (not the owner's own words): chose `<label>`*, its words, then *Its reasons:* each with its source, then *The owner may still
  say otherwise; their words win.* Never `chose:` alone, which the agent reads as the owner's (`QuestionWords.Chose`).
  `HandedToResume` hands it (`DriverContinuations.cs:784`); the resume's text gives it a paragraph of its own beside the person's
  and Daoris's (`Adapters.cs:724-735`).
- **Only in its own session.** A park that cannot resume in its own conversation with only the supervisor's answer waiting does not
  carry it into a new session (`Driver.cs:2144-2152` stands): it goes to the owner as another's words do, marked, and the question
  waits for them. The answer was the supervisor's reading of that session's state.
- **The ledger's note** says the record went on with the supervisor's answer (`WentOnWith` gains a fifth, `SessionLedger.cs:1635`),
  never *your words*.
- **The machine log**: `supervisor.answered` (`session`, `question`, `holder`, `choice: true|false`, `words: true|false`, `reasons:
  n`), `supervisor.deferred` (`bound`), `supervisor.refused` (`why`), `supervisor.overruled`, `supervisor.went-on`, and
  `person.appointment` (`state: granted|revoked|unread…`, `role`, never a repository name): codes only, never a value, as D201 §7.2.

### 6.2 Said

- **On the session.** The conversation draws the answer under its own mark, *From the supervisor, on your behalf* (主管代你答复),
  never the owner's bubble: the choice, the words, the reasons with their sources, and while held *Goes on at 14:05* with
  **Overrule**. Once gone on, *Went on at 14:05*. The box (`ParkAnswer`) and the head's card show the same on the question
  (QUESTIONUI1b).
- **In *Notifications*.** A notice per answer, *The supervisor answered a question in Daoris* with the chosen label, its
  first reason and **Overrule** while held, then read; kept by the notices' store as every notice is (`Notices.Keep`,
  `NoticesStateSlice.cs:189`). It is a notice, never a row of *What needs you*: it waits on nothing (`person-waits.md`: listed only
  with an act; the overrule is optional). The bell counts it as news, not as a wait. A group line heads the role's notices, as D201's
  presses' line does (`notifications/StandingLine.tsx`): *Supervisor appointed for Daoris · 3 answers since Daoris started ·
  Turn off*.
- **To the terminal holder.** The route's answer and `InstanceDoor.Told`: *answered as the supervisor; it goes on at 14:05 unless the
  owner overrules it*.

### 6.3 The overrule

- **The window.** The kept answer is held for **five minutes** from its keeping (row 1's constant; SUPERVISOR1e lets the appointment
  set 0 to 60): the planner's `WordsOn` (`Planner.cs:1461-1472`) skips a record whose waiting words are only the supervisor's and whose
  newest is younger than the window, and the loop is nudged at the window's end. Words of the owner's beside it carry the record on
  at once, the supervisor's going with them, said as its.
- **The press.** **Overrule**, `SUPERVISOR_OVERRULE { id, word }`, a `Person` route (a terminal's waits for the window's card, as
  every person's press does): the word is taken back through the service's take-back door (SAIDBACK1a, `SessionLedger.TakeBackAsync`,
  `SessionLedger.cs:989-1030`, which takes any word waiting on a parked record by its id, whoever said it), its event marked
  *overruled by you*, and the question stands for the owner as it was, its file still
  on disk since no run opened (`QuestionFiles.Clear` runs only at a run's opening). The owner then answers as themselves.
- **Before the session acts, and only then.** The take-back holds until the ledger moves the record to working (SAIDBACK1e,
  `DriverContinuations.cs:247-258`), and the ledger refuses one on a record that runs again (`SessionLedger.cs:1016-1023`); after, the press answers `went-on` and the conversation offers the owner's words instead: a
  correction said to the session as the owner's, which the session takes at its next step (D188 urgent).

### 6.4 What the appointment shows

Setup → Driving's row: *Appointed since 22 Oct, held by a terminal · 3 answers this run · Turn off*; a record not honoured (unread,
newer, another machine's) says so under it, as D201's row does. *Notifications*' line as §6.2.

## 7. Where it sits in the co-work and the company

| Who | What it does with a question | Beside the supervisor |
|---|---|---|
| **The lead** (the session that asked) | Asks the owner through `question_ask`, the answer its next prompt | It reads the supervisor's answer as the supervisor's, may act on it, and may ask again where the answer misreads its state; it is never told the owner said it |
| **A helper** (D163) | Answers a lead's own question to the lead, as a claim | A different question: a helper's is the lead's, the supervisor's is the owner's. The supervisor asks no helper and is asked by none |
| **A partner** (D199) | Writes a piece of the lead's work | The supervisor writes nothing |
| **Ask Daoris** (D110, D183) | The front desk: explains, proposes | May say what the supervisor answered and why, and where to appoint it; never appoints it, never answers as it, never overrules (D201 point 5's reach) |
| **The steward** (D193 part 18) | Management: settles what is not the owner's | Holds the supervisor from a terminal until the actor runs; its answers are then on the record as the supervisor's, where today they are the owner's words or a waiting card |
| **The owner** | Sets direction | Appoints, reads, overrules, and answers what is theirs |

**Not a hierarchy** (D193's rejection, the co-worker design §3.4). The supervisor directs no department and starts no work: it
answers only a question a session asked of the owner, within the owner's appointment, and the session decides how it acts on the
answer. It is the owner's delegate, not the session's manager. **Not a co-worker** (the co-worker design §2.1): it leads no team and
is no lead's helper. **D188's rhythm holds**: its answer is a solicited answer to a park, carried on at the driver's look like any
park's, never cutting a turn; §4.3 is its bound on answers alone.

**What it costs.** Held by a terminal, nothing of Daoris's accounts: the steward's own. Held by the actor, **one turn per answered
question**, only in appointed repositories, only for recorded questions (never a go-ahead), at most three in a row per line, and
never for a question marked the owner's. Its handed prompt is bounded by §3.2's first and third parts (a question at most about 3,000
characters of chosen words, the ask's words, the standing answers); the rest is what it reads, as a helper's turn (D163's 15 minutes,
here 10). No measured figure exists: SUPERVISOR1d measures tokens per answer on the bench before the actor is offered, and the
answers are counted per actor over seven days on D205's row (`ran`, D205 point 10), never as a price (D179). Its model is the owner's
choice (D205 point 2); the actors design's `model-roles.md` suggestion for it is a reasoning tier, since a wrong answer spends a
session's work, said as a suggestion the owner presses.

## 8. QUESTIONUI1: the question card

### 8.1 Its parts

One molecule, `QuestionCard` (`work/waits/`), drawn in the box (`ParkAnswer`) and in the head's card (`AwaitingPerson`), from props
alone (D55: no hook from `./queries` or `./shell`), replacing `QuestionAsked`; built of the UI design's atoms (`Chip`, `Button`, `Dot`,
`Inline`, `CheckField`'s focus ring) and the UI design's §4.2 `Elapsed` for the overrule's time:

1. **The question**, the agent's words as written.
2. **The marks above the choices** (§4.2): *Asked of you alone* with its mark, or *The supervisor left this to you* with the bound
   and its reasons.
3. **The choices**, a single-select group (`role="radiogroup"`, labelled by the question), each **a row that reads whole**: a radio
   mark, the label (strong), and under it *If you choose this:* and the `does` words. The recommended row carries *Recommended* in
   the row's own accessible name and, under its `does`, *Why:* and the agent's reason (QUESTIONUI1a's `why`; absent where the agent
   gave none). No row is selected at first: the recommendation is marked, never preselected, so a press is never the agent's choice
   by default (D52: never an answer the asker did not offer, nor one the person did not choose).
4. **Words**, one text field in the card, *Add words if you want (they go with your choice, or alone as your answer)*.
5. **Send** (*Send answer*), one press: the selected choice and the words, or the words alone; disabled with neither. Where
   go-aheads are open first, the rows stay selectable and *Send* says *Answer the go-aheads above first* (§0.5's order holds).
6. **The supervisor's answer**, where it answered (QUESTIONUI1b): its row marked *The supervisor's pick* (主管的选择) with a mark of
   its own, never the *Recommended* chip's look; under the group, *The supervisor answered on your behalf*, its words, its reasons
   each with its source as a link where it is a path (`PathText`), and while held *Goes on in 4 min* (`Elapsed`) with **Overrule**;
   after, *Went on at 14:05*.

The box's own field below (`SessionBox`) stays the conversation's free answer; while a question is drawn above it, its placeholder
says the card's *Send* answers the question, and words sent from the box are a free answer, as today.

### 8.2 Keyboard and screen reader

- **Tab** reaches the group once, landing on the selected row or the first; **arrows** move between rows and select (the native radio
  group's behaviour, by real `<input type="radio">` inside each row's `<label>`, so no roving focus is hand-built); **Space** selects;
  **Tab** goes on to the words, then *Send*. **Ctrl+Enter** in the words sends, as the box does.
- Each row's accessible name is its label; its description (`aria-describedby`) is *If you choose this:* and `does`, then the
  recommendation and its why, then *The supervisor's pick* where it is.
- A send's outcome is said in place through the card's `role="status"` line (*Sent: <label>*, or the refusal, R3), never a toast
  alone; *question-changed* keeps the selection and the words.
- The supervisor's answer and the overrule's time are a `status` region read once as it arrives, never a ticking live region (the UI
  design's `Elapsed` rule).

### 8.3 Words, widths and themes

Every chrome string in both catalogues (`work.park.*`, `work.supervisor.*`), the agent's and the supervisor's words as written in
every language (`translation-parity.md`). Stories of their own for `QuestionCard`, `ParkAnswer` and `AwaitingPerson` (the quest's *with their stories*:
today they have none of their own, §0.5): no choices; three choices, one recommended with its why; six long choices; selected; sending;
refused as changed; go-aheads first; marked the owner's; left to you by the supervisor; the supervisor's pick held, and gone on;
each at the main area's three widths (R6), in English and 中文, light and dark.

## 9. Words

| English | 中文 | Note |
|---|---|---|
| supervisor | 主管 | The role. Not 监督 (oversight of people) nor 管理员 (an administrator). The host's `HostSupervisor` in code is another thing: the role's types are named `QuestionSupervisor…` there |
| appoint, appointment | 任命 | The owner's grant of a role; *turn off* stays 停用 (NAME2) |
| on your behalf | 代你 | *From the supervisor, on your behalf*: 主管代你答复 |
| overrule | 否决 | The owner taking back a role's act before it takes effect |
| left to you | 留给你 | A question the supervisor did not answer, with why |
| role | 职责 | The company's sense (an appointed scope); *actor* stays 角色 (D205), and a co-worker's *role* stays as the co-worker design has it |

A quest is 委托, never 任务. The glossary gains *supervisor*, *appointment*, *overrule* in SUPERVISOR1a, with `names:check --strict`.

## 10. What D208 amends, what stands

**Amends, once accepted:** **D156** point 4 (a role's covered act from a terminal runs without a card; a role's act is never a card) and
§3.2 (a session's recorded question may be answered by an appointed role as itself; the owner's words, go-aheads and every other
answer stay theirs); **D201** points 2, 5 and 6 (the record carries appointments; the judge judges a role's act; `SET_APPOINTMENT` is
the window's) and the census's classes (`Role` beside `Open`, `Person`, `Window`); **D135** point 6 and its rejection (an appointed
role's answer is not in the owner's place, since it is never recorded as theirs, and its actor is measured on the question bench
before it is offered, §11 row d); **D185** (the question card, its parts and its stories); **D188** (the supervisor's answers-alone
bound, §4.3; `SaidFrom.Supervisor`); **D193** part 1 (the owner gives roles) and part 18 (the steward holds a role until its actor
runs); **D205** (a ninth actor, *supervisor*, off until the owner appoints it with the actor as holder); the park-ask design §2's tool
(`owner`, `why`), §4's door table, and the glossary.

**These stand:** go-aheads are the owner's alone (D135 point 2); D201's four presses and its exclusions, none of which a role covers;
D156 points 1 to 3 and 5 to 7; D193's rejection of a hierarchy; D163, D199 and the co-worker roles; D24 and D157 (no model judges a
route); PARKVIEW1's `SESSION_INPUT` for every answer the owner gives.

## 11. Proof, and the rows in build order

Each row lands alone with its proof; each test by class, filtered.

**SUPERVISOR1a — the supervisor's door** (modules, driver, service, web; the first row, small). `SET_APPOINTMENT` (`Window`) writing
`appointments` in D201's record under its lock, refused `not-offered` until `OfferedInThisBuild`; `PipeClass.Role` and
`SUPERVISOR_ANSWER` judged by `StandingConfirmation` as a fifth press, `supervise`, covered where the record appoints the session's
repository with holder `terminal`, the form exact and the caller outside every agent job, otherwise refused with why and never a card;
its handler refusing every door but a covered terminal's, and the §3.1 refusals; `daoris-driver sessions supervise <id>` printing the
question, and `sessions supervise <id> --question <qid> [--choice <label>] [--words <text>] --reason <says> --source <where>…`
answering it; `SaidFrom.Supervisor`, the event's fields, the handed
paragraph, `WentOnWith`'s fifth; the five-minute hold in `WordsOn` and the nudge; `SUPERVISOR_OVERRULE` (`Person`) through the
take-back; the conversation's mark with reasons and **Overrule**; the notice and the line in *Notifications*; Setup → Driving's row;
the log lines; the glossary. **Proof:** `StandingConfirmationTests` (an appointment read, written, revoked apart from the presses; a
record of an older shape; a role's act covered only for its repository; an act from an agent's job refused), `PipeStandingTests`
(covered runs with no card; uncovered is refused, never asked), `SupervisorAnswerTests` (each refusal of §3.1, kept as the
supervisor's, `Persons` false), `PlannerTests` (held within the window, goes on after; the owner's words beside it go at once; only
the supervisor's words never carry into a new session), `DriverModuleSessionQuestionTests` (overrule before the move takes it back and
the question stands; after, `went-on`), the service's `SaidFrom` and take-back tests, `SessionsCommandTests` for the verb,
`PipeCensusTests`, and the page's tests for the row, the conversation's mark and the notice; `HelpCoverageTests` for the presses.

**SUPERVISOR1b — its bounds** (service, driver, web). `question_ask`'s `owner`; the route's `owner-question` and `supervisor-bound`
refusals and the line's count; `SUPERVISOR_DEFER` and the card's *left to you*. **Proof:** `McpQuestionToolTests`, the shared
`park-question.json` table read by both readers, `SupervisorAnswerTests`, the card's stories.

**QUESTIONUI1a — the question card** (service, web). `question_ask`'s `why` (1-300) for the recommendation, in the file and the
shared table; `QuestionCard` in the box and the head's card, §8.1 parts 1-5 and §8.2; the stories of §8.3. **Proof:**
`QuestionCard.test.tsx` (one send per press, the selected label and words in `SESSION_INPUT` with `question`; nothing preselected;
send disabled with neither; a refusal keeps both), the keyboard by testing-library's user events, an axe pass on each story, the
frame's `WorkFrame.test.tsx` cases kept, `names:check --strict`.

**QUESTIONUI1b — the supervisor's pick on the card** (web, after 1a and QUESTIONUI1a). §8.1 part 6 from the event's fields, the
overrule from the card. **Proof:** the card's stories held and gone on, the overrule's test, en and zh, both themes.

**SUPERVISOR1d — the supervisor measured** (tools; before 1c is offered). The question bench gains a supervisor tier: each recorded
case handed as §3.2 hands it, scored right where a choice or words match the case's settled answer, wrong where an O case is answered
rather than left, and tokens per case read from the harness's report. **Proof:** the bench's tests on a stub; a run on the install's
recorded questions kept as evidence, its O-answered count zero before 1c offers the actor.

**SUPERVISOR1c — the actor** (driver, service, web; after MODELROLE1's choices rows, D205). The ninth actor; the driver starts it at a
park whose question is recorded in a repository appointed with holder `actor`, read-only, one turn, ten minutes, its only acting tool
`supervisor_answer`, recorded by 1a's path with its `ran`; the appointment's holder choice. **Proof:** a stub harness answering,
deferring and timing out; the record's actor fields; usage per actor.

**SUPERVISOR1e — scope and window** (modules, web). A workspace's appointment; the overrule window set at the appointment.
**Proof:** the record's tests and the row's.

## 12. Considered and rejected

- **The supervisor's answer as the owner's words**, through `SESSION_INPUT` under a standing grant: it is the record D201 point 3 and
  the owner's frame forbid, and DRIFT would make it a requirement.
- **A card for each supervisor's answer**, uncovered: the per-act confirmation the frame removes; refused instead.
- **A press of D201's standing confirmation alone**, machine-wide, with no repository: the owner's direction is per repository, and
  the role is not a management press but an answer.
- **No overrule window**: the overrule would race the driver's next look, so it would exist only in name.
- **Overrule after the session acted**: it would take back words the agent has read; the owner's correction is their own words.
- **A model reading the question to decide whether it is the owner's** (D24, D157): Daoris holds only the structural bounds; the
  agent's mark narrows; the holder decides; the owner overrules.
- **Preselecting the recommended choice**: one press would send the agent's choice as the owner's.
- **A separate roles file now** (§2.3): its cost lands on row 1, which the quest asks to keep small.
- **The supervisor answering go-aheads**, even a repository's own: go-aheads are held on the ask for every session and are the owner's
  (D135 point 2).
- **Carrying a supervisor's answer into a new session**: it answered one session's state.

## 13. The reads

None yet. Codex reads it (D191 point 1); an independent Opus reader reads it as a foundation (D191's amendment of 2026-10-23), since it
changes D156, D201's record and judge, the census's classes and the record's authorship. Their findings, the steward's check of each
at its lines, and the verdicts go in a table here.
