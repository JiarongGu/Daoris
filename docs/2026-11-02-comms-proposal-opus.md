# COMMS1 — Opus's proposal: Exchanges — one sender, one door for answers, one read of who asked whom

Status: sealed proposal, 2026-11-02, from docs/2026-11-02-comms-brief.md; Opus, read-only, kept as it came.

Evidence revision: `e0616aa57db6d12095b5bb1a147c512283515bdb` (main; the brief itself is untracked at it). Everything below was read, not run.

Paths are cited under the prefixes the cowork design already uses, each repository-relative: `Svc/` = `src/Daoris.Service/Daoris.Service.Core/`, `Mcp/` = `src/Daoris.Service/Daoris.Service.Mcp/`, `Http/` = `src/Daoris.Service/Daoris.Service.Http/`, `Drv/` = `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Mod/` = `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Web/` = `src/Daoris.Web/src/`, `Dn` = `docs/decisions/Dn.md`.

---

## 0. In one page

Daoris already has the right two stores. Words to a session wait on its record's `said`, the one durable queue D188 decided (`D188.md:44-54`). Work between repositories is a quest, the only record that travels (`D32.md:3-9`). Everything else that moves between participants is a **case** sitting beside those two: a box of files under the home with its own hand into `said` (a helper, a partner, an opinion), a column on the ask (go-aheads), a file per session (a question, a hand-over ask), or a table of its own (the channel and its acknowledgements).

The cases disagree in four places, each with evidence in §1.2:

- **who said a word**: four marks on a stored word, five on a message in flight, three authors on the page;
- **where an answer goes**: the line's head for some, the record named for others;
- **when a parked asker may take an answer**: an answered park takes a helper's answer but refuses a partner's and an opinion's;
- **how "once" is kept**: five receipt mechanisms for one job.

Two jobs have no way at all: parts of one line cannot talk to each other, and a line that is not on an ask has no channel and no go-ahead.

The proposal keeps every store. It adds two **reads**, one **door** and one **rule**:

1. **The sender, read.** One rule names who said a word from the marks it already carries (`person`, `daoris`, `supervisor`, `helper`, `partner`, `reviewer`, another session's claim, `unattributed`). Three twins, one fixture, nothing stored, like `ParticipantWork.Of` (`Drv/Participants/Domain/ParticipantWork.cs:73-81`).
2. **The line's exchanges, read.** Every request a line sent that expects something back is one value, composed from the box or record it already lives in: what it is, what it expects (an answer, a done or a decision), from whom, to whom, its state, whether the line holds on it, and the word that carried its answer. Today's waits, the strip's *awaits*, the co-workers COWORKVIEW1 still lacks (R11.4) and the brief after a hand-over all read it. It generalizes COWORKUX1's parked "line's exchanges" (`docs/2026-10-23-coworker-design.md:490-497`) from helper asks to every case.
3. **One door for solicited answers.** The helper's, partner's and opinion's hands share one service function inside the say door's exclusive step. It reaches the line's head, keeps the word under its producer key, answers a retry from the settlement and takes the answer by one table, D188 §5's (`docs/2026-10-20-message-urgency-design.md:304-305`). The driver gets one hand in place of three receipt mechanisms.
4. **One origin rule.** "Which ask, which conversation space, is this line in" is read in one way only: the line's entry origin (`D167.md:17-21`). The channel, the go-aheads and the brief all use it.

The delivery rule stays D188's, gaining four clauses (§2.5). The surfaces draw each message **once, at its home**, and draw a line that opens that home everywhere else (§5). The rows come in three waves: wave 1 is the foundation, built and merged as one wave (`TASKS.md:13`), wave 2 the surfaces, and wave 3 the two changes that grant authority or re-key a record, each after both makers' reads (D191's note of 2026-10-24, `D191.md:46-54`).

---

## 1. The census

### 1.1 Every way a message or a piece of work moves today

| # | Way | Between | Tool · route · door | Record | Delivered | Answered | Shown | Evidence |
|---|---|---|---|---|---|---|---|---|
| 1 | The person's words | person → a line | composer `SESSION_INPUT`, `sessions say`, say door | `said`, no author | D188's levels: steered, next step or the turn's end; an ended line reopens | its turn | conversation (held block, receipts), foot | `Http/Program.cs:1623-1670`; `Drv/Service/Domain/MessageLevel.cs:8-24` |
| 2 | A park's answer | person → a parked line | park box, `POST /api/sessions/{id}/answer`, `daoris-driver answer` | `said` + the ask's `answered` | the next look resumes it | its turn | head, foot, *What needs you* `parked` | `Http/Program.cs:2213`; `D83.md:3-11` |
| 3 | Daoris's words | Daoris → a line | `ReopenTelling`, the go-on | `said`, `from: daoris`, the fact's key | urgent: next step, else the turn's end | — | *Daoris's words* | `Drv/WorkOrders/ReopenTelling.cs:121`; `Svc/Sessions/Sessions.cs:405-408` |
| 4 | A channel message | a part or the person → the ask's other entries and the person | `channel_post`/`channel_read`, `ask channel`, `CHANNEL_POST`, *Send to its session* | `channel`; `channel_acks` and `channel_marks` (local) | each look polls each line and claims by id; ordinary rides, urgent goes at the next step; every tool's answer adds a line; the next part's packet | a reply by `re`; receipts | channel panel, ask page, *Notifications* | `Mcp/ChannelTools.cs:18-90`; `Drv/WorkOrders/ChannelReach.cs:50-80`; `Mcp/WaitingFilter.cs:13-74`; `Svc/Asks/Channel.cs:244`, `:263`, `:328` |
| 5 | A quest | a part, an ask, an intake or the person → a repository | `quest_publish`, the quest door | quest and its operations (synced) | the receiver's planner starts its own session | `done`/`ready`/`decline` with a handback; the asker's `wait` → D79 resume | quest page, at-work *Asked through quests*, the channel's line | `Mcp/QuestTools.cs:27-34`, `:406-418`; `Svc/Quests/QuestExchange.cs:1400-1403`; `Drv/Events/Domain/Observation.cs:71-78`; `Drv/WorkOrders/Domain/Planner.cs:912` |
| 6 | A lane's quest | a part → `itsrepository:lane` | `quest_publish` | quest with `Lanes` | a further part of the publisher's entry, alongside or after | handback; lands on the entry's one branch | Parts strip, at-work | `Svc/Quests/QuestExchange.cs:575-585`; `D167.md:383-397` |
| 7 | A chain's step | the asker → the next repository | `then` | quest with `Parent` | published by the close, in its transaction | as a quest | quest page | `D65.md:62-79` |
| 8 | A helper's question | an agent or the person → a helper | `helper_ask`, `sessions helper`, *ask another agent* | helper box `ask`/`state`/`answer` files | rule `helpers`; one turn; `POST /api/helpers/{h}/hand` → `said` (`Helper`, key `helper:`) | the asker's next turn (urgent: its next step) | head *Helpers*, rail *awaiting helper*, `helper` row | `Mcp/HelperTools.cs:17-41`; `Svc/Sessions/HelperBox.cs:6-10`; `Drv/Loop/DriverHelpers.cs:725-842`; `Svc/Sessions/HelperDesk.cs:135-240` |
| 9 | A partner's piece | the person only → a partner | *ask a partner*, `sessions partner` | partner box and a receipt file | the look's pass in a clone; a frozen ref; `/api/partners/{p}/hand` → `said` (`Helper` = the partner, key `partner:`) | the lead's merge takes it in | head *Pieces* | `Mcp/PartnerTools.cs:15-33`; `Drv/Loop/DriverPartners.cs:452-516`; `Svc/Sessions/PartnerDesk.cs:101-193` |
| 10 | A second opinion | an occasion or the person → a reviewer | the gate, `opinion` verb | opinion store | `/api/opinions/{id}/hand` → `said` (`By`), unkeyed | `opinion_answer`; a recheck goes to the person | gate, `opinion` row | `Svc/Review/OpinionDesk.cs:233-292`; `Mcp/OpinionTools.cs:16-60` |
| 11 | A question to the person | a driven agent → the person | `question_ask` | `<home>/sessions/<s>.question.json` | its turn ends: a park | a press (`choice`) on `said`, or the supervisor's word | foot card, head, `parked` | `Mcp/QuestionTools.cs:17-24`; `Svc/Sessions/QuestionBox.cs:60-61` |
| 12 | A go-ahead | an agent whose quest an ask asked → the person | `go_ahead_ask`, `ask --go-ahead` | the ask's `go_aheads` | its turn ends: a park | answered on the ask, handed beneath every later start | ask page, park box, `go-ahead` | `Mcp/GoAheadTools.cs:12-18`; `Svc/Sessions/SessionGoAheads.cs:23-98` |
| 13 | A hand-over ask | an agent → the person, for another agent | `handover_propose` | `<s>.hand-over-ask.json` and its state | rule `handOver` | a new part, briefed | head, `hand-over` | `Mcp/HandOverTools.cs:17-33`; `Svc/Sessions/HandOverAskBox.cs:9-30` |
| 14 | A hand-over, the move | a line → its new part | the hand-over box; the move record | the move record; the brief as the first event | the new part's first prompt | — | Parts strip, line view | `Drv/Context/Domain/ContextBrief.cs:638-684`; `D189.md:18-32`; `D212.md:90-99` |
| 15 | A handback | a part → whoever reads the quest later | `quest_respond`'s note | the quest's operation | read by `handback_read`, `GET /api/handbacks/{id}`, `sessions handback` | — | quest page | `Mcp/QuestTools.cs:415-418`; `Svc/Quests/QuestHandbacks.cs:93`; `D219.md:10-16` |
| 16 | A rule proposal | an agent → the person | `permission_propose` | rule-proposal box | a narrowing applies; a widening waits | the person's yes | `rule` | `Mcp/RuleProposalTools.cs:15-22` |
| 17 | A held tool call | an agent's call → the person | the harness's asking door | the loop's memory only | the turn stands still | the person's answer | `permission` | `Mod/Bridge/Routes/SessionsRoutes.cs:515-516`; `Mod/Loop/AttentionStateSlice.cs:427` |
| 18 | A third answer alone | a line → the person | `GoOnMarks` | the mark | held | *Go on with this answer* / *Not now* | `answer` | `D188.md:348-372` |
| 19 | A brainstorm | the person → voices → a document | `daoris-driver brainstorm`, `brainstorm_give`/`_synthesize` | brainstorm files | Daoris's fixed passes | a ranked document to the person | its page | `Mcp/CallTable.cs:97-98`; `Drv/WorkOrders/BrainstormFiles.cs:43`; `D176.md:12-33` |
| 20 | A meeting | the person and named agents | `meeting` verb | meeting files | relayed at the protocol's barrier | its transcript | parked (AGENTROOM1b) | `Drv/WorkOrders/MeetingFiles.cs:35`; `D177.md:12-24` |
| 21 | A native child | a lead's agent → its harness's subagent | the harness's own tool | the lineage file | inside the lead's process | inside it | at-work *Subagents* | `Svc/Sessions/LineageFile.cs:28`; `docs/2026-10-24-cowork-view-design.md:954` |
| 22 | Ask Daoris | the person ↔ Ask Daoris; Ask Daoris → the person | its panel; `*_propose`, `go_propose` | help record and proposal box (this machine) | a card | the person's Apply | its panel | `Svc/Help/HelpProposalBox.cs:32`; `D89.md:3-10`; `D158.md:9-12`; `D170.md:10-17` |
| 23 | An intake | an ask → quests | `quest_publish` as `ask #id` | ask record | — | the ask's quests | ask page | `D65.md:81-95` |
| 24 | The supervisor's answer | an appointed role → a parked line, for the person | `SUPERVISOR_ANSWER` | `said`, `from: supervisor`, with its decision | released after the overrule window | — | *From the supervisor, on your behalf* | `Svc/Sessions/SupervisorWord.cs:13`; `Svc/Sessions/Sessions.cs:416-420`; `D208.md:66-79` |
| 25 | The steward's act | an appointed role → repositories, sessions, quests | `daoris-driver steward …` | the act's own record | — | — | its record | `Drv/Roles/StewardCommand.cs:21`; `D209.md:38-60` |
| 26 | A terminal's press | a terminal → the person → a route | the pipe | the standing confirmation | Confirm card | Confirm | *Notifications* | `Mod/Platform/StandingConfirmation.cs:298`; `D196.md:21-27` |

### 1.2 One job, several ways

| # | The job | How the ways differ | Evidence |
|---|---|---|---|
| A | **Who said this word** | `SaidWord` names its author by four marks, the person being their absence: `By` (an opinion), `Helper`, `From` (`daoris`, `agent`, `supervisor`) and `Channel`. A message in flight carries five: `Daoris`, `Channel`, `Helped`, `Supervised` and `GoesOn`, with the event's origin chosen by a switch over them. The page knows three authors and an `opinion`. A **helper's and a partner's answers are drawn as *The target Daoris composed***, and a partner's word is marked `Helper` | `Svc/Sessions/Sessions.cs:392-402`; `Drv/Course/Domain/ChatMessage.cs:133-183`, `:220-236`; `Web/work/conversation/conversation.ts:361-370`, `:511-513`; `Svc/Sessions/PartnerDesk.cs:182`; `Drv/Loop/DriverPartners.cs:508-511` |
| B | **Where an answer goes** | The helper and the partner go to the line's head. A driven line's opinion goes to the working record it names, so after a hand-over its findings land on a part the line has left. The helper's walk stops at 16 links, twice over (once in the desk, once in the driver), where the say door's walk stops at 256 | `Svc/Sessions/HelperDesk.cs:152-158`, `:243`; `Svc/Sessions/PartnerDesk.cs:117-119`; `Svc/Review/OpinionDesk.cs:255-261`; `Drv/Loop/DriverHelpers.cs:851-876`; `Svc/Sessions/Lines.cs:31` |
| C | **When a parked asker takes an answer** | A helper's answer goes in beside the person's answer to an answered park. A partner's and an opinion's are refused by every park, against D188 §5's row *goes on beside the person's answer* | `Svc/Sessions/HelperDesk.cs:205-209`; `Svc/Sessions/PartnerDesk.cs:163-164`; `Svc/Review/OpinionDesk.cs:580-581`; `docs/2026-10-20-message-urgency-design.md:304-305` |
| D | **Once** | Five receipt mechanisms: the helper's `Helper` mark on `said`, its key, the driver's `Attempted` and a search along the line; the partner's key and a receipt file; the opinion's `Handed` in its own store, with no key; the channel's per-entry acknowledgement table; the words' `said_settled` | `Svc/Sessions/HelperDesk.cs:221-224`; `Drv/Loop/DriverHelpers.cs:776-800`; `Drv/Participants/Domain/PartnerPieces.cs:188-191`; `Drv/Loop/DriverPartners.cs:456-482`; `Svc/Review/OpinionDesk.cs:277-282`; `Svc/Asks/Channel.cs:328-437`; `D188.md:452-458` |
| E | **Which ask a line is on** | The channel answers by the entry's origin. A go-ahead answers by the session's own ask or its quest's sender. A session on a quest that another session of an ask published so has the ask's channel and no go-ahead | `Svc/Asks/Channel.cs:1174-1181`; `Svc/Sessions/SessionGoAheads.cs:75-84` |
| F | **What a line waits on** | It is read in `Observation.Conclude`'s inputs, `HelperWaits.Open`, the strip's `awaiting-reply`/`awaiting-helper`, the page's thirteen `Wait` kinds and twenty-nine attention kinds. What the line asked of others (outgoing) and what the person must answer (incoming) are derived apart in each | `Drv/Events/Domain/Observation.cs:63-141`; `Drv/Participants/Domain/HelperWaits.cs:20-28`; `Drv/WorkOrders/SessionGroups.cs:945-960`; `Web/work/waits/waits.ts:26-39`; `Web/work/attention/attention.ts:44-105` |
| G | **What a tool's answer says waits** | Only channel messages. An answered helper or a closed quest is said only at the next turn | `Mcp/WaitingFilter.cs:13-39` |

### 1.3 A job with no way

1. **A partner only the person can start.** No `partner_ask` is registered; the partner's tool file holds only `partner_answer` (`Mcp/PartnerTools.cs:15-33`). D199 point 2 decided the lead's ask (`D199.md:20-21`).
2. **Parts of one line cannot hear each other.** A part's post is never handed to its own entry (`D167.md:217-219`). A lead and its lane part alongside therefore have no way to say "the contract changed". CHANPART1 was left for a design of its own (`D203.md:66-68`).
3. **A line on no ask has no channel and no go-ahead.** That covers a chat, a quest one repository asked of another, and the sessions such a quest started (`Svc/Asks/Channel.cs:1177-1181`; `Svc/Sessions/SessionGoAheads.cs:78-84`). A chat and the session its quest started elsewhere can speak only by quest and handback.
4. **The co-working view shows no co-workers.** It waits on COWORKUX1a's exchange read, which is parked (`docs/2026-10-24-cowork-view-design.md:518-520`).
5. **A new part after a hand-over is told nothing of what its line waits on.** The brief carries turns, state, note, parts and what it left out, but no open asks (`Drv/Context/Domain/ContextBrief.cs:638-684`).
6. **By design, and kept:** a chat never waits on a quest (`docs/2026-10-24-cowork-view-design.md:426-429`). A native child has no address. An agent cannot summon a reviewer (D155 point 5).

---

## 2. The model

### 2.1 Who may address whom: five addresses

A participant is D212's read (`D212.md:45-49`). Communication adds one closed vocabulary, **the address**:

| Address | Names | Resolved | Kept |
|---|---|---|---|
| **a line** | a session id | to its line's head inside the keeping step (`Svc/Sessions/Lines.cs:41-63`), never the record named | D167, D189 point 6, D212 row d-ii |
| **an origin** | `ask:<id>`, and from row 1i `session:<id>` and `quest:<id>`; `to` names everyone, a repository, or (1i) a part | the channel's | D167 point 6, widened by 1i |
| **a repository** | `repo` or `repo:lane+lane` | the exchange's judgement | D32, D115 point 2 |
| **a work** | `help`, `partner`, `review`, `brainstorm` | Daoris picks the agent under the person's rule; the asker may name an agent | D163 point 3, D199 point 2, D155 point 4, D176 point 1 |
| **the person** | this machine's owner | an appointed role answers in their place only within its grant; a role is never an address of its own | D208 point 1, D209 point 1 |

| Sender ↓ · to → | a line | an origin | a repository | a work | the person |
|---|---|---|---|---|---|
| person | words, at any level | a post | a quest (the door) | a helper, a piece, an opinion, a brainstorm | — |
| Daoris | its own words, ordinary or urgent | lines on the quests' states | a chain's step, the intake's quest | an opinion's occasion, a brainstorm's passes | rows, notices |
| `build`/`design` (a lead or a part) | only the answers it is handed | a post (3 of 10 urgent) | a quest, its own lanes included | `help`; `partner` (1h) | a question, a go-ahead, a hand-over, a rule |
| `chat` | — | a post on its origin (1i) | a quest, its own lanes included | `help`; `partner` (1h) | a rule (it talks to the person in its turns) |
| `intake` | — | — | a quest as its ask | — | a park answered on the ask |
| `help`, `partner`, `review` | its one answer, to the line it serves | — | — | — | — |
| `ask-daoris` | — | — | — | — | proposals, which are decisions |
| a brainstorm's pass | — | — | — | — | its result, through Daoris |
| supervisor (appointed) | its answer, on the person's behalf | — | — | — | — |

Agents never address a line. Only an answer the line asked for reaches it, which is D188 point 4 (`D188.md:39-43`).

### 2.2 Four kinds, by what each expects back

| Kind | Expects | Today's cases | Bounded by |
|---|---|---|---|
| **message** | nothing | a channel post; Daoris's words; a notice; the person's words, which make a turn by the person's own reach (D137) | D188's levels; ten posts a part, three urgent; expiry as *not reached* |
| **question** | an answer, in words | a helper's ask; a question to the person; a channel question (`question: true`, answered by `re`, never waited on) | one turn; the minutes; two helpers at once; one question at a time |
| **request** | a done or a decline, with a result | a quest (its handback); a piece (a frozen commit and an answer); an opinion (findings, then the asker's answers); a brainstorm (a document) | strikes; minutes; two pieces a lead; one recheck |
| **decision** | a yes or a no, from an authority | a go-ahead; a hand-over ask; a held call; a rule proposal; a helper's or a piece's start under `ask`; a third answer alone; a terminal's press; Ask Daoris's card; a dispute | the person alone, or an appointed role within its grant |

**Not kinds:**
- *The hand-over* moves the line itself. What it sends is Daoris's brief, and nothing comes back (D189).
- *A meeting* is a protocol over messages with a barrier (D177).
- *A native child* has no address.

### 2.3 Where each lives: every record kept

| Case | Its record (unchanged) | Its answer's record | Travels |
|---|---|---|---|
| word to a line | `said` | receipts on the line's events | no (`D188.md:96`) |
| channel message | `channel` | `re`, `channel_acks` | the message; never its receipts (`D167.md:259-266`) |
| helper | helper box (`Svc/Sessions/HelperBox.cs:42`) | answer file → `said` `helper:<id>` | no |
| piece | partner box (`Svc/Sessions/PartnerBox.cs:34`) | answer file, frozen ref → `said` `partner:<piece>` | no |
| opinion | opinion store | findings → `said` `opinion:<id>` (key added in 1b) | no |
| quest | quest | its operations (done, decline, handback), `Awaits` | yes |
| question to the person | question file | the press on `said`, or `from: supervisor` | no |
| go-ahead | the ask's `go_aheads` | the answer on the ask | no (asks are this machine's, `D65.md:95`) |
| hand-over ask | hand-over ask and its state | the state, then the move record | no |
| rule, held call, alone hold, press | each its own | each its own door | no |
| brainstorm, meeting | their files | the document, the transcript | no |

**Nothing new stores a message.** D188 rejected one new store for every message (`D188.md:80`). The model reads these records.

### 2.4 What the model adds

**1. The sender, a read** (`Senders.Of`). The first rule that holds wins:

1. `From = supervisor` → the supervisor.
2. `From = daoris` → Daoris.
3. `From = agent` → a claim the person passed on.
4. `By` → a reviewer.
5. `Helper` with a `partner:` key → a partner.
6. `Helper` → a helper.
7. `Channel` with origin `target` → another session's claim.
8. No mark → the person.
9. An author this build does not know → unattributed.

The rule is written in three twins: `Svc/Sessions/Senders.cs`, `Drv/Service/Domain/Senders.cs`, and `Web/work/conversation/sender.ts`. The page's twin reads the named record's `work` from the sessions slice, where a page event carries no key (`Mod/Loop/SessionsStateSlice.cs:47-49`). The twins are held by `fixtures/senders.json`, as `participant-work.json` holds the work's twins. `SaidWord.Persons`, `ChatMessage.Persons`, `ChatMessage.Words`' origin and the page's `authorOf` all read it. It replaces disagreement **A**.

```ts
type Sender =
  | { person: true } | { daoris: true } | { role: 'supervisor' }
  | { helper: string } | { partner: string } | { reviewer: string }   // a record's id, an opinion's id
  | { claim: { session?: string; repository: string; message: string } } | { passedOn: string } | { unattributed: true };
```

**2. The line's exchanges, a read** (`LineExchanges.Of`, `Drv/WorkOrders/Domain/`).

- **What it holds.** Every question, request and decision any part of the line sent, plus the requests that asked the line (its *Asked by*).
- **What it reads.** Snapshots the look already holds: the helper, piece, question and hand-over files; the quests slice (`publishedBy`, `awaits`, `holder`); the sessions slice; the opinions the service lists for a working line; the go-aheads on the line's ask. Nothing is read from a conversation's text (`docs/2026-10-24-cowork-view-design.md:987-990`).
- **Who reads it.** `HelperWaits.Open` and `Observation.Conclude`'s wait input, the strip's waits, at-work's co-workers and *Asked through quests*, the head's *Waiting on*, the conversation's card, the brief, and `sessions at-work`.

```ts
type Exchange = {
  id: string;                    // `helper:h7`, `piece:p1`, `opinion:o2`, `quest:q9`, `question:<session>`, `go-ahead:<ask>/<n>`, `hand-over:<session>`
  kind: 'question' | 'request' | 'decision';
  case: 'helper' | 'piece' | 'opinion' | 'quest' | 'question' | 'go-ahead' | 'hand-over' | 'rule' | 'brainstorm';
  from: Sender;                  // a part of the line, or the person
  to: Address;                   // { repository, lanes? } | { work } | { person: true }
  at: string;
  state: 'asked' | 'yours' | 'waiting' | 'running' | 'answered' | 'handed' | 'taken' | 'refused' | 'no-answer' | 'uncertain' | 'left' | 'stale';
  why?: string;                  // a code where it waits, was refused or had no answer
  by?: { session?: string; agent?: string; role?: 'supervisor'; elsewhere?: true };
  answer?: { at: string; word?: string; on?: string };  // the word that carried it, and the record it reached
  holds: boolean;                // the line's turn ended on it
  travels: boolean;              // true for a quest alone
};
```

**3. One door for solicited answers** (`SolicitedAnswers.KeepAsync`, `Svc/Sessions/`).

- **Inside the store's exclusive step,** it walks the line's head with `Lines.WalkAsync`, so the 16-link bound goes. It answers a retry from the settlement (`SaidKeys.SettledOnLineAsync`) before any refusal but the record's absence. It keeps the word under `<case>:<id>` with its level, and it takes the answer by one table (§2.5).
- **The three desks call it.** `HelperDesk`, `PartnerDesk` and `OpinionDesk` each keep only their own words and their box's state. Their routes are unchanged.
- **The opinion changes most.** Its word is keyed `opinion:<id>` and goes to the line's head. CHATFLOW1e's `to` stays (`Http/Program.cs:1855-1860`).
- **In the driver,** one `SolicitedHand` (`Drv/Participants/Domain/`) writes an attempt mark before the door. The door's answer then settles it: kept, already waiting, or settled. That replaces `HandedBefore`, the helper's line walk and the partner's receipt file. Old state files are still read.
- **Older builds.** The service answers a mark, `answers: 1`. A driver that sees no mark keeps today's code against an older host (`.claude/knowledge/compatible-builds.md:1-5`). This replaces disagreements **B**, **C** and **D** for every case but the channel and the words, which keep their own receipts.

**4. One origin rule.** "Which ask, which space" means the entry's origin (`SessionLedger.WithEntryAsync`). The go-ahead takes it (row 1c), the brief's go-aheads take it, and the channel keeps it. This replaces disagreement **E**.

### 2.5 The delivery rule

**D188 point 5 stands word for word** (`D188.md:44-48`): kept in its path's store with level, sender and id before the sender is told; reached at the earliest point its level allows; ordinary otherwise, and said so. Four clauses extend it:

1. **To a line, never to a record.** Every keep resolves the line's head in its step, the opinion's included.
2. **A solicited answer goes through one door, once, at its exchange's level.** A helper's answer takes its ask's level; a piece's, an opinion's and a quest's outcome are always ordinary (D188 point 2). It is taken by this table, which is today's helper's (`Svc/Sessions/HelperDesk.cs:198-218`), made everyone's:

   | The head is | ordinary | urgent |
   |---|---|---|
   | working, driven, run by this loop | at its turn's end (handed then) | at its next step (`running`, held in its inbox) |
   | a conversation between turns | its next turn | its next turn |
   | running anywhere else | at its turn's end | at its turn's end |
   | parked, unanswered | waits, said once | waits, said once |
   | parked, answered | beside the person's answer, one prompt | the same |
   | ended | reopens once, counted by the answers-alone bound | the same |
   | stood down, an intake, serving, a reviewer, a teammate's | refused: the answer goes to the person, with its code | the same |

3. **To the person, by what stands still.** An exchange addressed to the person is listed in *What needs you*. It sits in *Holding work* where a turn or a start stands still on it, else *Waiting for your word* (`Web/work/attention/attention.ts:113-117`). That is its level: the person's urgency is *derived* from what stands still, never stated by the sender. An appointed role may answer within its grant. The answer returns through the row's door to the sender's line head.
4. **Nothing this machine owns travels.** An exchange travels only as a quest. A channel message travels; its receipts do not.

### 2.6 What stays its own case, and why

| Case | Why it stays its own |
|---|---|
| The quest | It is the only record of work across repositories and machines, with take, done, evidence and landing (`D167.md:56-57`). Messages never replace it |
| The channel | Its messages are claims that never start a run. Its acknowledgements are per entry, since an entry's parts are a sequence (COWORK1d). They are never routed through `said` (`.claude/knowledge/session-words.md:30`) |
| The person's words | Their reach is the person's (D137), and *now* is theirs alone (D188 point 2) |
| The brainstorm and the meeting | Daoris's fixed plan and a barrier. Their output is never a word to a session (`D176.md:24-28`; `D177.md:45-55`) |
| Decisions | Each keeps its own door and record. The read lists them; it judges none |
| Ask Daoris | It proposes, and the person applies (D89, D170). Its conversation is this machine's (D158 point 1) |

---

## 3. Waiting and answering

### 3.1 How a sender waits

| Way | When | The record | Woken by |
|---|---|---|---|
| **holds** | its turn ends on the exchange | `completed` with `ended.awaits` (a quest), `ended.awaits-helper`, or `ended.awaits-piece` (1h); or parked (a question, a go-ahead) | the answer's word on its line's head, reopening once (`D188.md:39-43`); D79's resume (`Drv/WorkOrders/Domain/Planner.cs:912`); the person's answer |
| **carries on** | it asked and kept working | working | an urgent answer at its next step; an ordinary one at its turn's end |
| **never waits** | a chat on a quest; a post | — | it reads `quest_list` or the channel at its own point (`docs/2026-10-24-cowork-view-design.md:426-429`) |

`holds` is the read's one answer. The places that today each derive it read that answer instead (`Drv/Participants/Domain/HelperWaits.cs:20-28`; `Drv/Events/Domain/Observation.cs:126-132`).

### 3.2 Across a restart

| What | Survives | How it is settled |
|---|---|---|
| a word on `said` | yes | attempted before the restart: *uncertain*, never sent again (`.claude/knowledge/session-words.md:55-59`) |
| a solicited answer half-handed | yes | the attempt mark plus the keyed door: kept, waiting or settled within the settlement's seven days; past that horizon, *uncertain* |
| a box's ask, state or answer | yes (files) | a helper or a piece cut mid-turn settles *no answer*, and its line comes to the person (`D188.md:418-424`) |
| a held tool call | no, it lived in memory | its turn died with the process, said by the run's end |
| the exchanges | nothing to recover | a read, composed again from the records |
| a channel claim | yes | its acknowledgement says how far it went |

### 3.3 Across a hand-over to another agent or account

**An exchange belongs to its line, not to its record.**

- The read walks the line by `from`.
- The door delivers to the head (clause 1), so a helper's, a piece's or an opinion's answer reaches the part that continues the line, never a left one (`D189.md:35-36`).
- The quest's wait follows the quest's newest run.
- The new part is **told** what its line waits on: the brief gains a piece, *What this line waits on* (row 1e).

### 3.4 Across machines (D178)

**Sessions never move between machines; takes do.**

- **What travels:** quests, with their waits and handbacks, and the channel's messages.
- **What never does:** `said`, the boxes, opinions, go-aheads, brainstorms, meetings and Ask Daoris.
- **A quest answered on another machine** resumes the asker here by D79.
- **A take this machine lost** keeps its machine-local exchanges with its kept session. *Bring mine forward* goes on with them, answers included (`D178.md:32-48`). *Drop mine* marks them `left`: said once in the read, each answer kept in its answerer's own conversation.
- **The read says it** wherever an exchange's other end is elsewhere: `by: { elsewhere }`.

### 3.5 Unanswered: said and bounded

| Exchange | Its bound | Said to the sender | Said to the person |
|---|---|---|---|
| quest | strikes; a decline; the person's abandon; no clock | a decline resumes it with the reason, and stops a chain | the strip's *awaits*, the quest page |
| helper | its minutes; two at once; the rule | refused or no answer: its line comes back to the person (HELPERWAIT1) | `helper-ended` where that move is refused |
| piece | its minutes; two open | `limited`, or no answer, as a note | *Pieces* |
| opinion | `minutes`; one recheck | unresolved becomes disputed, which is the person's | the gate |
| question, go-ahead | none by time; stale once nothing can use it | — | the row marked stale (`.claude/knowledge/person-waits.md:36-39`) |
| channel message | expires *not reached* | its receipt | the channel |
| terminal's press | two and ten minutes | — | a toast on refusal |

### 3.6 The person as a participant

| What reaches the person | Where | Level | How the answer returns |
|---|---|---|---|
| a question | foot card, head, `parked` | holding | the press → `said` with `choice` on the line's head; or the supervisor's word, as the role's |
| a go-ahead | park box, ask page, `go-ahead` | holding (stale: word) | `POST /api/asks/{id}/go-aheads/{n}`, handed beneath every later start |
| a hand-over ask | head, `hand-over` | word while its turn runs; holding once it ended | its state file, then the move |
| a helper's or a piece's yes | head, `helper` (`partner` from 1h) | word | the box's state `approved` |
| a held call | `permission` | holding | the harness's answer |
| a third answer alone | `answer` | holding | *Go on* or *Not now* |
| a dispute | gate, `opinion` | word | *Accept…* or *Go on anyway…* |
| a channel message | channel panel, the ask's unread, *Notifications*' messages | as D196 has it | a post with `re` |
| Ask Daoris's card | its panel only | — (the person is in the conversation) | Apply |
| a terminal's press | *Notifications*' presses | holding | Confirm |

The answer goes to **the exchange's sender's line head**, under the one door's rules. It is kept as the person's words only where it is words. The supervisor's answer is never the person's (`Svc/Sessions/Sessions.cs:397-401`).

---

## 4. Co-working: who joins, what it may send, how its work returns

| Who | Joins by | May send, to whom | Its work returns as | Seen in |
|---|---|---|---|---|
| lead (`build`/`design`) | its quest's start | quest (any repository, its own lanes); `helper_ask`; `partner_ask` (1h); `question_ask`; `go_ahead_ask`; `handover_propose`; `permission_propose`; a post on its origin | its quest's handback | rail, head, At work |
| a part (retry, resume, carry-on, lane, a sibling's request) | the entry's key | as a lead; a post to a sibling part (1i) | its quest's handback; lands on the entry's branch | Parts strip, *This session* |
| chat | the person's composer | as a lead, but never waits | its turns | its page |
| helper | `helper_ask` or the person | `helper_answer` alone (`Mcp/CallTable.cs:75`, `:80-81`, `:126-133`) | an answer → the asker's next turn | *Co-workers* (1g) |
| partner | `partner_ask` (1h) or the person | `partner_answer` alone (`Mcp/CallTable.cs:42-46`) | a frozen commit and an answer; the lead merges it | *Co-workers*, *Pieces* |
| reviewer | an occasion or the person | `opinion_give` (`Mcp/CallTable.cs:54-58`) | findings → the line; `opinion_answer` | *Co-workers*, the gate |
| native child | its lead's harness | nothing through Daoris of its own | inside its lead | *Subagents* |
| another repository's session | a quest | as a lead, in its own repository | its handback | *Asked through quests* |
| intake | the ask | quests as its ask | quests | the ask's page |
| Ask Daoris | the person's panel | proposals to the person | the person's Apply | its panel |
| brainstorm pass | Daoris's plan | its result tool alone (`Mcp/CallTable.cs:97-98`) | a document | the brainstorm |
| supervisor | the owner's *Appoint* | answers to questions addressed to the person, within its grant | a word `from: supervisor` | its chip; its record |
| steward | the owner's *Appoint* | acts | the act's records | its records |

D203's team stands: the line and every record serving it (`D203.md:22-24`). COWORKVIEW1's read is its view (`docs/2026-10-24-cowork-view-design.md:979-1012`). The exchange read is the one reader of co-workers that view was waiting for (`D203.md:222-225`).

---

## 5. The surfaces

**One rule.** A message is drawn whole once, at its home. Everywhere else it is a line that opens that home.

| Message | Its home | Elsewhere |
|---|---|---|
| a word | the line's conversation, at the turn that took it | — |
| a channel message | the channel | a sender chip in the conversation that took it |
| an exchange | its card at the call, in the asker's conversation | the answer folds into the card; the turn that took it shows a reference line |
| a decision to the person | its row | the card states it and opens the row |

Widths are W, the main area at 80rem (1546 px, the list open), and H, 39rem (680 px, the list at its strip) (`docs/2026-11-01-setup-ux-design.md:10-13`).

### 5.1 The session's head and conversation (wave 2: 1f, 1g)

```
1546 · W · a driven lead whose turn ended on its helper; its lane part runs alongside
┌ Sessions ──────────┐┌──────────────────────────────────────────────────────────────────────────────┐┌ At work ──────────────────┐
│ ▾ Resumes later    ││ #412 Build the policy store · daoris · Claude Code · opus  Waits on its helper ││ This session              │
│ ● #412 policy store││ Waiting on  Codex, as helper · reading your work as of 16:29 · 4 min  [Open] ││  Part 3 · you are here    │
│   ↳ Codex · helper ││             #q7 to daoris:web-shell · part 4 works alongside          [Open] ││   waits on its helper     │
│   ↳ part 4 · web   ││ ▸ Parts 4 · Part 3 of 4   ▸ Channel 2 new   ▸ How it came to be             ││  Part 4 · #q7 web-shell   │
│                    ││ ─────────────────────────────────────────────────────────────────────────── ││   alongside · working     │
│                    ││ Turn 6 · 14 min                                                              ││ Co-workers                │
│                    ││  ┌ Published #q7 to daoris:web-shell ────────── 16:27 · became part 4 ┐     ││  Codex, as helper         │
│                    ││  │ Draw the policy chip                                     [Open quest]│     ││   reading · 4 min  [Open] │
│                    ││  └──────────────────────────────────────────────────────────────────────┘     ││ Asked through quests      │
│                    ││  ┌ Asked Codex, as helper ─────────────── 16:29 · reading · 4 min ──────┐     ││  #q9 to repo-b · taken    │
│                    ││  │ Is the store's write lock held across the import?                    │     ││ Subagents  none           │
│                    ││  │ Its answer comes back to this session as its next turn.[Open thread] │     ││                           │
│                    ││  └──────────────────────────────────────────────────────────────────────┘     ││                           │
│                    ││  Asked Codex to check the lock; ending my turn so it can read the work.      ││                           │
└────────────────────┘└──────────────────────────────────────────────────────────────────────────────┘└───────────────────────────┘
```

```
1546 · W · the answer taken: the card folds it; the next turn holds a line that opens it
│ Turn 6                                                                                           │
│  ┌ Asked Codex, as helper ─────── 16:29 · answered 16:41 · taken as the next turn 16:42 ──────┐  │
│  │ Is the store's write lock held across the import?                                          │  │
│  │ ▾ Codex's answer · a claim to check, not your words · read as of 16:29, 2 paths uncommitted│  │
│  │   Yes: the write takes the lock at its start and the import runs inside it …   [Show all]  │  │
│  └────────────────────────────────────────────────────────────────────────── [Open thread] ──┘  │
│ Turn 7 · began with Codex's answer ↑ · 6 min                                                    │
```

```
1546 · W · each word's sender chip at the head of its turn, from the one rule
  You · 16:02                                                                urgent · sent · taken
  Daoris's words · the work #q3 grew from was reopened                                        taken
  A claim from repo-b's session (Codex), on the channel · message m4 ↗                       taken
  Codex, as helper · answers this session's question of 16:29 ↑                               taken
  A reviewer's findings · Codex · second opinion o2 · 3 findings ↗                          answered
  From the supervisor, on your behalf · chose "Use the staging copy"                          taken
```

```
680 · H · the same lead; the dock closed; chips wrap under the turn
┌──┐┌──────────────────────────────────────────────────────┐
│≡ ││ #412 Build the policy store      [Waits on helper ⓘ] │
│● ││ Waiting on  Codex, as helper · 4 min          [Open] │
│  ││             #q7 · part 4 works alongside      [Open] │
│  ││ ▸ Parts 4 · ▸ Channel 2 · ▸ At work                  │
│  ││ ──────────────────────────────────────────────────── │
│  ││ ┌ Published #q7 to daoris:web-shell · part 4 ──────┐ │
│  ││ └─────────────────────────────────────── [Open] ───┘ │
│  ││ ┌ Asked Codex, as helper · reading · 4 min ────────┐ │
│  ││ │ Is the store's write lock held across the import?│ │
│  ││ └──────────────────────────────── [Open thread] ───┘ │
└──┘└──────────────────────────────────────────────────────┘
```

- *Waiting on* in the head is the at-work view's line, the same words and read (`docs/2026-10-24-cowork-view-design.md:479`). It opens that view at the exchange.
- The person's incoming waits stay where `person-waits.md` places them (`.claude/knowledge/person-waits.md:15-28`). Each word avoids *waiting on you* (`:52-53`).
- *The target Daoris composed* is never drawn for a solicited answer (`docs/2026-10-23-coworker-design.md:510`).

### 5.2 At work: co-workers and their exchanges (wave 2: 1g)

```
680 · H · At work opened from the head (at 1546 it is the dock's tab shown above)
┌ At work · #412 ─────────────────────────────────────┐
│ This session                                         │
│  Part 3 · you are here · waits on its helper         │
│  Part 4 · #q7 web-shell · alongside · working        │
│  ▸ 2 earlier parts                                   │
│ Co-workers                                           │
│  Codex, as helper · answered 16:41 · taken   [Open]  │
│  Codex, as reviewer · o2 · 3 findings · answered     │
│  Codex, as partner · piece p1 · writing · 6 min      │
│ Asked through quests                                 │
│  #q9 to repo-b · taken by its session        [Open]  │
│ Asked by  #q4, of ask #a12                           │
└──────────────────────────────────────────────────────┘
```

### 5.3 A line's channel where the line is on no ask, and parts that hear each other (wave 3: 1i)

```
1546 · W · a chat in daoris with two rows handed to its lanes (parts 2 and 3 alongside)
┌ Channel · this line of work ───────────────────────────────────────────────────── 3 messages ┐
│ part 2 · daoris:web-shell (Codex) → part 3 · 16:20 · urgent                                   │
│   The chip's name is policyChip now; build on that.                         sent · taken      │
│ part 3 · daoris:driver (Claude Code) → everyone · 16:31                                       │
│   Store step 15 is in; nothing for the page.                                read by part 2    │
│ You → part 2 · 16:40                                                                          │
│   Keep the chip's label under 16 units.                                     taken             │
│ To [ everyone ▾ ]  [ Write a message…                                   ]  [ Ordinary ▾ ] [Post]│
└────────────────────────────────────────────────────────────────────────────────────────────────┘
```

```
680 · H · the panel stacked
┌ Channel · 3 ─────────────────────────────────────────┐
│ part 2 → part 3 · 16:20 · urgent                     │
│  The chip's name is policyChip now; …   sent · taken │
│ part 3 → everyone · 16:31                            │
│ You → part 2 · 16:40                         taken   │
│ To [everyone ▾]                                      │
│ [ Write a message…                ] [Ordinary ▾][Post]│
└──────────────────────────────────────────────────────┘
```

### 5.4 What needs you: a lead's piece (wave 3: 1h)

```
1546 · W · Waiting for your word
│ #412 Build the policy store · Claude Code asks Codex, as partner, to write a piece                 │
│   "Write the chip's stories at the three widths" · from 3f2a9c1 · at most 30 min                   │
│                                                         [Let it start]  [Not this]  [Open]         │
680 · H
│ #412 · asks Codex, as partner        │
│ "Write the chip's stories at the…"   │
│ [Let it start] [Not this] [Open]     │
```

### 5.5 Ask Daoris, *Notifications* and the terminal

- **Ask Daoris** is unchanged: its cards are decisions drawn once, in its own conversation. Its room may read the exchanges as a fact about what waits on what (row 1g's room row), and it proposes nothing new.
- ***Notifications*** is unchanged (D196). Its rows are *What needs you*'s, with the sender and the exchange in their words.
- **The terminal's twin** is `sessions at-work` (`Drv/WorkOrders/SessionsCommand.cs:356-360`), which gains the co-workers and the exchanges in text and JSON. No new verb.

```
$ daoris-driver sessions at-work 412
#412 Build the policy store · daoris · waits on its helper
This session   part 3 (attended) waits on its helper · part 4 #q7 daoris:web-shell alongside, working
Co-workers     helper h7 · Codex · asked 16:29 by the agent · answered 16:41 · handed to part 3 · taken 16:42
Asked          #q9 to repo-b · taken by its session
Holds on       helper h7
Not here: the channel's messages (`ask channel`), why a quest sits (the tick's reason).
```

---

## 6. The named decisions: what changes, what stands, how older records read

| Decision | Changes | Stands |
|---|---|---|
| D32, D115 | — | the quest is the one request across repositories and the only exchange that travels; a lane is how a repository asks itself |
| D65 | — | the intake publishes as its ask; asks are this machine's |
| D83, D135 | the park and the go-ahead are read as decisions owed to the person; KNOWUSE1a's derivation of the ask becomes the entry's origin (1c) | the park, `answer`, one go-ahead per act, the person's answer |
| D89, D158, D170 | — | Ask Daoris proposes and the person applies; its conversations are this machine's |
| D155 | XAGENT1e's delivery: findings are keyed `opinion:<id>`, kept on the line's head through the one door, and an answered park takes them (1b) | the gate, `opinion_answer`, the recheck to the person, no agent summoning a reviewer |
| D163 | point 2's hand goes through the one door; the 16-link bound retires (1b) | one turn, two at once, the rule |
| D167 | point 6: each origin has a channel; point 7: a part's post reaches the other live parts of its entry, by part (1i) | ownership local; a message never starts a run; the channel travels |
| D176, D177 | — | their own cases |
| D178 | a lost take's exchanges are `left` on *Drop mine*, and kept on *Bring mine forward* | the choices |
| D188 | point 5 gains §2.5's four clauses; point 7's receipts for a solicited answer are the one door's | the levels, who sends each, no level stops a turn, the answers-alone bound |
| D189 | point 4's brief gains *What this line waits on* (1e) | the brief's other contents, no model |
| D199 | point 8's hand goes through the one door; point 2's `partner_ask` is built (1h) | the clone, the frozen commit, the merge |
| D203 | point 7's card per exchange is read from the line's exchanges; COWORKUX1a's exchange read is this design's | points 1 to 6, the names |
| D208, D209 | — | an appointed role answers for the person within its grant and is never an address |
| D212 | point 1's read gains the sender beside the work; point 9's settlement serves the one door | the move record, the store, the map |
| D219 | — | the handback is a request's answer, read whole |

**How older records read:**
- No stored author changes, so every old word reads by its old marks, and the rule's last line keeps an author it does not know as *unattributed*.
- An unkeyed opinion word is still guarded by `opinion.Handed`.
- Helper and partner state files keep being read, and the new hand writes the same fields until a row-v-style retirement.
- Channel rows with no `origin` read as their ask's.
- Acknowledgements per part are a new table no older build reads (`.claude/knowledge/compatible-builds.md:23-30`).
- A driver against a host without `answers: 1` keeps today's hand.

---

## 7. Today's code, file by file

| File | Today, with evidence | Change | Row |
|---|---|---|---|
| `Svc/Sessions/Sessions.cs` | four marks, `Persons` as their absence (`:392-402`) | `Persons` reads `Senders.Of`; nothing stored | 1a |
| `Svc/Sessions/Senders.cs` (new) | — | the rule; fixture `Daoris.Service.Tests/fixtures/senders.json` | 1a |
| `Drv/Service/Domain/Senders.cs` (new) | — | the twin, read by `ChatMessage.Persons` (`Drv/Course/Domain/ChatMessage.cs:139`) and `Words` (`:220-236`) | 1a |
| `Web/work/conversation/conversation.ts` | three authors (`:361-370`); a helper's answer drawn as a target (`:511-513`) | `senderOf`; `Ask.sender` and `Ask.re` | 1a |
| `Svc/Sessions/SolicitedAnswers.cs` (new) | — | the door of §2.4 point 3 and the table of §2.5 | 1b |
| `Svc/Sessions/HelperDesk.cs` | its own walk, bound and take switch (`:135-243`) | calls the door; `LineBound` retires | 1b |
| `Svc/Sessions/PartnerDesk.cs` | every park refused (`:163-164`) | calls the door | 1b |
| `Svc/Review/OpinionDesk.cs` | the working record, no key (`:255-261`, `:277-282`) | the head and `opinion:<id>` through the door; `Handed` still written | 1b |
| `Http/Program.cs` | the three hand routes (`:1855-1901`) | unchanged; the `answers` mark | 1b |
| `Drv/Participants/Domain/SolicitedHand.cs` (new) | — | the attempt mark and the keyed door | 1b |
| `Drv/Loop/DriverHelpers.cs` | `HandedBefore`, `Attempted`, a 16-link walk (`:283-301`, `:776-800`, `:851-876`) | `SolicitedHand`; old states read | 1b |
| `Drv/Loop/DriverPartners.cs` | a receipt file (`:456-482`); `Helper = partner` on the event (`:508-511`) | `SolicitedHand`; the event unchanged, since the sender reads the record's work | 1b |
| `Drv/Loop/OpinionDeliverer.cs`, `Drv/Review/OpinionDoors.cs` | the hand (`OpinionDoors.cs:56-77`) | `SolicitedHand`, keyed | 1b |
| `Svc/Sessions/SessionGoAheads.cs` | the ask from the quest's sender (`:75`) | the entry's origin, as `Svc/Asks/Channel.cs:1174` | 1c |
| `Drv/WorkOrders/Domain/LineExchanges.cs` (new) | — | the read | 1d |
| `Drv/Participants/Domain/HelperWaits.cs` | `Open` over the helper box (`:20-22`) | a façade over the read | 1d |
| `Drv/Events/Domain/Observation.cs` | `awaitsHelper` input (`:126-132`) | from the read's `holds`; codes unchanged (`ended.awaits-piece` in 1h) | 1d |
| `Drv/WorkOrders/SessionGroups.cs` | `AwaitsHelper`, `awaiting-reply` (`:945-960`) | from the read | 1d |
| `Drv/Context/Domain/ContextBrief.cs` | no open asks (`:638-684`) | the piece *What this line waits on*, bounded, counted | 1e |
| `Mod/Loop/AtWorkRead.cs`, `Drv/Participants/Domain/AtWork.cs` | no co-workers (`docs/2026-10-24-cowork-view-design.md:518-520`) | *Co-workers* from the read, an input value with its health | 1g |
| `Drv/WorkOrders/AtWorkVerb.cs` | parts, waits and quests | co-workers and exchanges, text and JSON | 1g |
| `Web/work/conversation/ConversationView.tsx` | tool cards for `helper_ask` and the rest | the exchange card, the chips, the reference line | 1f |
| `Web/work/head/SessionHead.tsx` | *Helpers*, *Pieces* sections | *Waiting on*, from the read; the presses stay | 1g |
| `Web/work/atWork/AtWorkView.tsx`, `types.ts` | parts, subagents, quests, asked-by | *Co-workers* | 1g |
| `Mcp/PartnerTools.cs`, `Mcp/CallTable.cs`, `Svc/Sessions/PartnerBox.cs` | `partner_answer` alone (`PartnerTools.cs:15-33`; `CallTable.cs:100-106`) | `partner_ask`, served to `build`, `design` and `chat`; an agent's ask written once | 1h |
| `Mod/Loop/NeedsAPerson.cs`, `Web/work/waits/waits.ts`, `Web/work/attention/attention.ts` | no partner yes (`waits.ts:26-39`; `attention.ts:44-105`) | the `partner` wait in every place `person-waits.md` lists | 1h |
| `Svc/Asks/Channel.cs`, `Svc/Remote/ChannelSync.cs`, `Mcp/ChannelTools.cs` | refuses a line on no ask (`Channel.cs:1177-1181`); `to` names a repository (`ChannelTools.cs:29-30`) | any origin; `to` names a part; `channel_part_acks` (new table) | 1i |
| `Drv/WorkOrders/ChannelReach.cs` | no same-entry delivery (`D167.md:217-219`) | a part's post reaches its entry's other live parts by part | 1i |
| `Web/asks/ChannelSection.tsx` | an ask's panel | on a chat's page; part addressing | 1i |
| `canon/core/skills/co-working/SKILL.md` | the ask's channel, helpers, quests (`:6-12`) | the four kinds, the sender's words, `partner_ask`, posts to sibling parts | 1j |
| `Web/locales/{en,zh}/…`, `glossary.json` | — | keys under `work.conversation`, `work.atWork`, `work.head`, `asks.channel`; no new window term (*exchange* and *sender* are code and prose only, as D212 point 14 names its own); never *delivery*, which names a landing (`glossary.json:499-507`) | each |

---

## 8. Rows, in build order

The design changes a foundation: who a word is from, a protocol between processes, and what a wait is read from. Under D191 both makers read it. Wave 1 is built and merged as one wave with one D225 note (`TASKS.md:13`). Rows 1h and 1i grant authority or re-key a record, so they build only after both reads of their contract.

| Row | Wave | Contract | Proof |
|---|---|---|---|
| **COMMS1a** the sender, read (service, driver, web) | 1 | §2.4 point 1; three twins; `Persons`, `Words` and `authorOf` read it; no stored change | `fixtures/senders.json` held row for row by `SendersTests` (service and driver) and `sender.test.ts`; `ConversationView` never draws `work.conversation.target` for a helper's or a partner's word; en and zh |
| **COMMS1b** one door for solicited answers (service, driver) | 1 | §2.4 point 3, §2.5's table; the opinion keyed to the head; the `answers` mark; `SolicitedHand` | a table test (case × the head's state × level) through each desk; a hand-over between the ask and the answer for each case, the answer reaching the new part; a lost answer retried, answered settled, no second word; an older host keeping today's hand; Process: a stand-in lead asks a helper, is handed over to another agent, and the new part takes the answer once |
| **COMMS1c** one origin rule (service) | 1 | `SessionGoAheads` by the entry's origin | `GoAheadTests`: a session on a quest a sibling of an ask published holds its go-ahead on that ask; the channel and the go-ahead agree over the participant table |
| **COMMS1d** the line's exchanges, read (driver, modules) | 1 | §2.4 point 2; `HelperWaits`, `Observation`'s input and `SessionGroups` read it with no behaviour change | `LineExchangesTests` per case and state; `ObservationTests`, `HelperWaitsTests`, `SessionGroupsTests`, `HelperLookTests` green unchanged; the read equal before and after a restart; after a hand-over the head's read holds the predecessor's open exchanges |
| **COMMS1e** the brief says what the line waits on (driver) | 1 | `ContextBrief`'s new piece, Daoris's English, bounded, in its account | `ContextBriefTests` golden; a hand-over while a helper reads tells the new part |
| **COMMS1f** the conversation draws each message once (web, modules) | 2 | §5.1's card, chips and reference line, from the exchanges and the sender | page tests in en and zh; stories at 80, 39 and 25rem, both themes; keyboard order; installed at 1546 and 680 px |
| **COMMS1g** *Waiting on* and *Co-workers* (web, modules, driver) | 2 | §5.1's head line; §5.2; `sessions at-work`; Ask Daoris's room reads the exchanges | `AtWorkView` and `stripVersusView` tests; the verb's text and JSON; a story per member state; `HelpCoverageTests`' row; installed |
| **COMMS1h** a lead asks a piece (COAGENT1d; service, driver, web) | 3 | `partner_ask` under `partners`, `ask` by default; `ended.awaits-piece`; the `partner` wait in every place `person-waits.md` lists | the partner suites; `McpRoleFilterTests`' table; `AttentionStateSliceTests`; Process: a stand-in lead asks a piece, its yes, the piece merged |
| **COMMS1i** a channel for every origin, and parts that hear each other (CHANPART1; service, driver, web) | 3 | §2.1's origins; `to` names a part; `channel_part_acks`; sync | `ChannelTests`; `ChannelSyncTests` (an older remote); `ChannelReachTests` with two alongside parts; stories §5.3 |
| **COMMS1j** the doctrine (canon) | after 1h, 1i | the co-working skill's kinds, the sender's words, `partner_ask`, posts to sibling parts; project-agnostic | the canon's tests; `daoris check` here and in both examples; `npm run verify` |
| **COMMS1k** the installed proof (the steward) | last | a helper across a hand-over; a lane part's post to its lead; a lead's piece | dated shots at 1546 and 680 px, en and zh; the records read back |

---

## 9. Rejected

- **One new inbox table for every kind.** It would make two authoritative stores for each concept (`D188.md:80`). The read composes the stores that exist.
- **A stored `sender` field.** It would be a copy that drifts and that older builds never write. The rule reads the marks words already carry, as `ParticipantWork.Of` reads its own.
- **Answers through the channel.** A claim never starts a run (D167 point 7). A solicited answer must.
- **Messages in place of quests** (`D167.md:56-57`), agents addressing lines, *now* for anyone but the person, and urgency judged by a model (`D188.md:73-81`).
- **One route for every hand.** The desks' routes stay, so no wire changes. One function under them settles the disagreement.
- **Retiring the boxes into one file.** Each box has one writer per file and its own admission (`Svc/Sessions/HelperBox.cs:6-10`).
- **An agent asking for a review of its work in progress.** A helper reads work in progress (D199 point 5); a reviewer reads a candidate.
- **A chat waiting on a quest.** R11.2 point 5 stands.
- **Per-part acknowledgements as a nullable column.** An older build would write around it (`.claude/knowledge/compatible-builds.md:5`).
- **Telling an answered exchange on every tool's answer.** The answer arrives as a turn anyway, and no tool reads `said` for an agent.

## 10. Open, and not measured

- **Whether a D79 resume counts toward the answers-alone bound** stays the owner's question (`docs/2026-10-24-cowork-view-design.md:522-524`). The exchange read makes it one rule, `from` the line and `to` its own lanes, once decided.
- **Not measured:**
  - a real agent's helper answer across a hand-over;
  - two alongside parts posting to each other;
  - an origin channel crossing a shared host built before 1i;
  - zh at 300 px in the dock;
  - whether agents use `partner_ask` and sibling posts as the doctrine says.
- **Model tiers.** Every judgement here is the no-model tier: the reads and the door are .NET over records. The only model-dependent acts are an agent's choices to ask, post or wait, and the agent, account and model that ran are on its binding (D199 point 9).
- **Where this rests.** The facts come from the code at the revision above, read and not run. Line numbers move with main.