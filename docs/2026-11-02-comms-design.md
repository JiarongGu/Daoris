# COMMS1: one model for how sessions, agents and the person talk and hand work to each other

Status: accepted by the steward 2026-11-02 (D225), the synthesis of both sealed proposals; both makers' reads run beside wave 1's
first rows. It answers `2026-11-02-comms-brief.md` from `2026-11-02-comms-proposal-opus.md` and
`2026-11-02-comms-proposal-codex.md`, each claim this design relies on checked at `16cc8107b`. Paths: `Svc/` is
`src/Daoris.Service/Daoris.Service.Core/`, `Mcp/` `src/Daoris.Service/Daoris.Service.Mcp/`, `Drv/`
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Mod/` `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Web/` `src/Daoris.Web/src/`.

## 1. What both proposals decided alike

The census is Opus's §1.1 (26 ways, each with its record, delivery, answer and evidence) read beside Codex's §1; it is not
repeated here. Both reached the same model:

1. **Every store is kept.** Words to a line wait on its `said` (D188); work between repositories is a quest (D32). Helpers, pieces,
   opinions, questions, go-ahead, hand-overs and the channel keep their own records. Nothing new stores a message's body twice.
2. **Communication is a read over those records**, never a store of its own (COWORKVIEW1 §2): who sent what to whom, what it expects
   back, and where it stands.
3. **Who said a word is read from the marks it carries** and the door it came through, never from a supplied field or its text. A
   partner's word is a partner's, though it carries the helper mark today (`Svc/Sessions/PartnerDesk.cs:172`).
4. **An answer goes to the line, never to a record**: the line's head at keeping, so after a hand-over it reaches the part that
   continues the line.
5. **A hand-over's brief says what the line waits on**, so the new part is not blind to its own open asks
   (`Drv/Context/Domain/ContextBrief.cs:638-684`).
6. **One message is drawn whole once, at its home**; everywhere else it is a reference that opens it.
7. **D188's delivery rule stands**: kept before the sender is told, reached at the earliest point its level allows, a message never
   stops a turn, *now* is the person's alone.

## 2. Where they differed, settled

| Point | Opus | Codex | Settled |
|---|---|---|---|
| The kinds | four by what each expects: message, question, request, decision | three acts (say, request, hand over) with an `expects` | **Codex's acts, Opus's expectations as `expects`** (§3.1): an act says what moves, `expects` what comes back |
| One state, or two | one `state` per exchange | delivery and outcome kept apart | **Codex's**: a turn's end is not an answer; *answered* means different things to a channel, a piece and an opinion (`docs/decisions/D188.md:55`, `Svc/Asks/Channel.cs:972`) |
| The answers' hands | one door under the three desks | adapters that keep each protocol | **Opus's one door** (§4.2): it is what fixes the three disagreements found in the code, and each desk keeps its own words and box |
| A question's answer | unchanged | immutable question revisions in a new service record | **Smaller than Codex's**: the question file's `id` is already new per question (`Svc/Sessions/QuestionBox.cs:135`); a press names it and is refused where the session's current question is another (§5.2). No new record |
| Parts hearing each other | the channel widened to every origin and to a part | `message_send`, `message_read`, `message_reply` in a new family | **One set of tools, a new table** (§4.4): the channel's tools are widened, so an agent's tools stay few and the co-working skill keeps its shape; a row addressed to a part is kept where no older build reads it (Codex's compatibility point, `.claude/knowledge/compatible-builds.md:9`) |
| The answers-alone bound | the D79 resume left the owner's question | count it, in a durable admission record | **Durable, serialized count** (Codex), built in wave 1; **whether a waited quest's result counts is the owner's**, recommended yes (§11) |
| A lead asking a piece | `partner_ask` built (row 1h) | not until the partner's confinement is measured | **Codex's**: production admission has no measurement rows (`Drv/Participants/Domain/PartnerMeasurements.cs:28`); `partner_ask` stays COAGENT1d's, and the read says *not available* rather than drawing it |
| The steward's desk | not addressed | its request history persisted, no grant revived | **Taken as a wave 3 row** (§10 row l) |
| A reviewer in the team | through the read | new opinions write `serves` (D203's intent) | **Codex's**, in row b |
| Wave shape | three waves; 1h and 1i after both reads | F1–F4 one foundation wave | **Three waves** (§10): the foundation built and merged as one wave (the owner's direction of big chunks), the surfaces, then the rows that add a record or a door |

## 3. The model

### 3.1 Acts, and what each expects back

| Act | Expects | Today's cases |
|---|---|---|
| **say** | nothing | a channel post; Daoris's words; a notice; the person's words (a turn by the person's own reach, D137) |
| **request** | `answer` (words), `work` (a done or a decline with a result), `piece`, `opinion`, or `decision` (a yes or a no from an authority) | a helper's question; a question to the person; a quest and its lanes; a piece; an opinion; a go-ahead; a hand-over ask; a rule proposal; a held call; a third answer alone; a terminal's press |
| **hand over** | a successor admitted | the move to another agent, account or access (D189) |

A reply names its request; a result names its request. Neither opens another request. A handback is a quest's result, a helper's
answer a reading's, a frozen commit a piece's, findings an opinion's: they stay different result types (Codex §2). A brainstorm and
a meeting stay protocols of their own, with their barriers (D176, D177); a native child has no address.

### 3.2 Addresses

| Address | Names | Resolved |
|---|---|---|
| **a line** | a session id | to its line's head inside the keeping step (`Svc/Sessions/Lines.cs:41-63`), and again before dispatch; held where the head cannot be read |
| **a part** | a part of one entry, by its session | that part only, never its siblings or the entry (wave 3, §4.4) |
| **an origin** | `ask:<id>`, and from wave 3 the entry of a chat or of a quest's session | the channel's audience; `to` names everyone, a repository, or a part |
| **a repository** | `repo` or `repo:lane+lane` | the exchange's judgement (D32, D115); a lane constrains the work and is no mailbox |
| **a work** | `help`, `partner`, `review`, `brainstorm` | Daoris picks the agent under the person's rule (D163, D199, D155, D176) |
| **the person** | this machine's owner | an appointed role answers within its grant only; a role is never an address of its own (D208, D209) |

Agents never address another line: only an answer the line asked for reaches it (D188 point 4). Helpers, partners, reviewers and a
brainstorm's passes keep their result-only tools (`Mcp/CallTable.cs:95`); no message tool is a back channel around them.

### 3.3 The sender, read

`Senders.Of` names who said a word from the marks it carries, the first that holds: `From = supervisor` → the supervisor;
`From = daoris` → Daoris; `From = agent` → a claim the person passed on; `By` → a reviewer; a `partner:` key → a partner; `Helper`
→ a helper; `Channel` with a target origin → another session's claim; no mark → the person; a mark this build does not know →
unattributed. Three twins (`Svc/Sessions/Senders.cs`, `Drv/Service/Domain/Senders.cs`, `Web/work/conversation/sender.ts`) held by
one fixture, as `participant-work.json` holds the work's (`Drv/Participants/Domain/ParticipantWork.cs:73`). Nothing is stored.
Authorship comes from the door: a supplied `from`, a role's name or a quoted instruction grants nothing; an agent's answer the person
passes on stays the agent's, passed on.

### 3.4 The exchanges, read

`Exchanges.Of` (`Drv/WorkOrders/Domain/Exchanges.cs`; in WorkOrders because it reads quests, which the structure gate keeps out of
Participants) composes every request a line's parts sent, and the requests that asked the line, from snapshots the look already
holds: the helper, piece, question and hand-over files, the quests and sessions slices, the opinions on a working line, the
go-aheads on the line's ask. Nothing is read from a conversation's text.

```ts
type Exchange = {
  id: string;                  // `helper:h7`, `piece:p1`, `opinion:o2`, `quest:q9`, `question:<id>`, `go-ahead:<ask>/<n>`, `hand-over:<session>`
  act: 'request' | 'hand-over';
  expects: 'answer' | 'work' | 'piece' | 'opinion' | 'decision' | 'successor';
  from: Sender;  to: Address;  at: string;
  delivery: 'kept' | 'attempted' | 'sent' | 'taken' | 'uncertain' | 'not-reached' | 'elsewhere';  // what Daoris observed
  outcome: string;             // the protocol's own: asked, answered, approved, refused, done, declined, produced, taken-in, disputed, stale, left…
  holds: boolean;              // the line's turn ended on it
  by?: { session?: string; agent?: string; role?: 'supervisor'; elsewhere?: true };
  answer?: { at: string; word?: string; on?: string };   // the word that carried it, and the record it reached
  available: 'yes' | 'not-available';                    // a case whose door is not built or not measured says so
  source: 'read' | 'unread' | 'stale';                   // a source not read is said, never drawn as empty
};
```

It is the reader COWORKUX1a's parked exchange read was to be (`docs/2026-10-23-coworker-design.md:490-497`), widened from helpers
to every case, and the co-workers COWORKVIEW1 waits on (R11.4). `HelperWaits.Open`, `Observation.Conclude`'s wait input, the strip's
waits, *At work*, the head's *Waiting on*, the brief and `sessions at-work` read it.

## 4. Delivery

### 4.1 The rule

D188 point 5 stands word for word. It gains four clauses:

1. **To a line, never to a record.** Every keep resolves the line's head in its step, the opinion's included.
2. **A solicited answer goes through one door, once,** at its request's level: a helper's takes its ask's level; a piece's, an
   opinion's and a quest's outcome are ordinary (D188 point 2). It is taken by today's helper table, made everyone's (§4.2).
3. **To the person, by what stands still.** A request addressed to the person is in *What needs you*: in *Holding work* where a
   turn or a start stands on it, else *Waiting for your word* (`Web/work/attention/attention.ts:113-117`). The sender states no
   urgency to the person. A passive unread message is never a count there (Codex §6).
4. **Nothing this machine owns travels**, except as a quest or a channel message; their receipts stay here.

### 4.2 One door for solicited answers

`SolicitedAnswers.KeepAsync` (`Svc/Sessions/`), inside the say door's exclusive step: it walks the line's head with
`Lines.WalkAsync` (the helper's own 16-link walks retire, `Svc/Sessions/HelperDesk.cs:243`, `Drv/Loop/DriverHelpers.cs:851-876`),
answers a retry from the settlement before any refusal but the record's absence, keeps the word under `<case>:<id>` with its level,
and takes it by this table:

| The head is | ordinary | urgent |
|---|---|---|
| working, driven by this loop | at its turn's end | at its next step |
| a conversation between turns | its next turn | its next turn |
| running anywhere else | at its turn's end | at its turn's end |
| parked, unanswered | waits, said once | waits, said once |
| parked, answered | beside the person's answer, one prompt | the same |
| ended | reopens once, counted by the answers-alone bound | the same |
| stood down, an intake, serving, a reviewer | refused: the answer goes to the person, with its code | the same |

This settles three disagreements found in the code: an opinion's findings went to the working record named
(`Svc/Review/OpinionDesk.cs:255-261`), so after a hand-over they landed on a part the line had left; a partner's and an opinion's
answer were refused by an answered park that takes a helper's (`Svc/Sessions/PartnerDesk.cs:163-164`,
`Svc/Review/OpinionDesk.cs:580-581`, against `Svc/Sessions/HelperDesk.cs:207-209`); and five receipt mechanisms kept one *once*. The
desks keep their routes and their own words; the driver's one `SolicitedHand` writes an attempt mark before the door and the door's
answer settles it (kept, already waiting, settled), replacing `HandedBefore`, the helper's line walk and the partner's receipt file,
whose old files are still read. The service answers a mark, `answers: 1`; a driver that sees none keeps today's hand against an
older host. New opinions write `serves` (D203's intent; today the opener writes `opinion` alone, `Svc/Review/OpinionDesk.cs:128`).

### 4.3 One origin rule

Which ask a line is on is the line's entry origin (`SessionLedger.WithEntryAsync`), as the channel reads it
(`Svc/Asks/Channel.cs:1174`). The go-ahead takes it: today it reads the session's ask or its quest's sender
(`Svc/Sessions/SessionGoAheads.cs:75`), so a session on a quest a sibling of an ask published has the ask's channel and no go-ahead.

### 4.4 A channel for every origin, and parts that hear each other (wave 3)

Today a line on no ask has no channel (`Svc/Asks/Channel.cs:1177-1181`), and a part's post never reaches its own entry
(`Channel.cs:1052-1058`), so a lead and its lane part alongside cannot say *the contract changed*. The channel's tools are widened:
`channel_post` may post on the entry's own origin (a chat's, a quest session's) and `to` may name a part of the entry; a row
addressed to a part is kept in a new table (`channel_addressed`) with its per-part receipts, which no older build reads, so it is
never broadcast to an audience it was not for; an exact recipient a build does not support is refused, never widened. Parts of one
entry are on one machine, so an addressed row never travels. A replacement part of the same line inherits that line's pending
delivery; an unrelated sibling does not. The tools say information and optional questions; a request for work stays a quest.

## 5. Waiting and answering

### 5.1 How a sender waits

| Way | When | Woken by |
|---|---|---|
| **holds** | its turn ends on the request (`ended.awaits`, `ended.awaits-helper`), or it parked (a question, a go-ahead) | the answer's word on its line's head, reopening once; D79's resume; the person's answer |
| **carries on** | it asked and kept working | an urgent answer at its next step, an ordinary one at its turn's end |
| **never waits** | a chat on a quest; a post | it reads `quest_list` or the channel at its own point (COWORKVIEW1 R11.2 point 5) |

Resource rules stand: a helper wait keeps its tree and frees the slot; a quest wait frees both, the lead committing first; a question
to the person outranks a helper wait (`Drv/Participants/Domain/HelperWaits.cs:10-28`).

### 5.2 A question's answer names the question

A press of an offered choice carries the question's `id`; the say door keeps it on the word (the column the supervisor's answer
already writes, `Svc/Sessions/Sessions.cs:391-395`) and refuses a press whose `id` is not the session's current question, beside its
control, with the current question to read. The person's own words stay their intervention and are never read as a choice.

### 5.3 The answers-alone bound, kept durably

The count of a line going on by answers alone is read from events today (`Drv/Course/Domain/AnswersAlone.cs:28`), so a trimmed or
absent event resets it. It becomes one serialized admission per line in the desktop store (source key, line, epoch, reserved,
attempted, taken, cancelled, uncertain), shared by driven and chat executors, so two answers queued together cannot each think they
are the second. Only the person's own words or an act the person authorized reset the epoch; Daoris's wrapper, a brief, a retry or a
hand-over never do; a role's answer counts. Where the history is incomplete the count is *unknown* and the next automatic
continuation holds for the person. Whether a waited quest's result is one of its causes is the owner's (§11).

### 5.4 Restart, hand-over, machines

- **Restart:** words and solicited answers are keyed and attempted before dispatch; an uncertain attempt is never sent again and
  waits for the person (`.claude/knowledge/session-words.md:55-59`); the exchanges are a read, composed again; a held tool call
  lived in memory and ends with its turn, said by the run's end.
- **Hand-over:** an exchange belongs to its line. The door reaches the head, so answers reach the continuing part; the brief gains
  *What this line waits on* (the request's id, its owner, where its answer will go, its delivery), bounded and in Daoris's words.
- **Machines (D178):** sessions never move between machines; takes do. Quests with their waits and handbacks travel, and the
  channel's ask messages; `said`, the boxes, opinions, questions, go-ahead, admissions and role requests stay. A mirror says
  *delivery not observed on this machine*. A take this machine lost keeps its exchanges with its kept session: *Bring mine forward*
  goes on with them, *Drop mine* marks them `left`.
- **Unanswered:** no automatic resend. Each case keeps its bound (a quest's strikes and no clock; a helper's minutes; a piece's
  minutes; an opinion's recheck; a question until answered or replaced; a channel message expiring *not reached*). An expired
  deadline is an unanswered outcome, never permission to choose for the recipient.

## 6. Co-working

Opus's §4 table stands (who joins, what it may send, how its work returns), with Codex's limits: a helper answers once and its
follow-up is a fresh reading; a partner's door for an agent is not available until its confinement is measured; a native child is
shown only as observed; a role holder is never a co-worker and its appointment never survives a restart. D203's team stands (the line
and every record serving it); parts, native children, other repositories' sessions and role holders are drawn beside it, none
gaining `serves` to fit the drawing. The exchanges read is the one reader of co-workers.

## 7. The surfaces

One rule: a message is drawn whole once, at its home; elsewhere a reference opens it. The homes: a word in the line's
conversation at the turn that took it; a channel message in the channel (a sender chip in the conversation that took it); an
exchange as its card at the call in the asker's conversation, the answer folded into the card and the taking turn holding a line
that opens it; a decision to the person in its row, the card stating it.

The wireframes at 1546 and 680 px are Opus's §5.1 to §5.3 (the head's *Waiting on*, the exchange card before and after its
answer, the sender chips, *At work*'s *Co-workers*, the channel of a line on no ask) and Codex's §6 *What needs you* and Ask
Daoris. Settled on them:

- The head's *Waiting on* is *At work*'s line, the same words and read; it opens that view at the exchange.
- *The target Daoris composed* is never drawn for a solicited answer (`Web/work/conversation/conversation.ts:511-513` today).
- A receipt says what Daoris saw: a turn ended after the word was taken reads *taken, turn ended*, never *answered*; old
  `answered` receipts are drawn as they were.
- *What needs you* keys each obligation by its exchange id, so the head, the foot and *Notifications* count one wait once.
- Ask Daoris reads the exchanges as a fact about what waits on what, and proposes only acts that exist.
- The terminal's twin is `sessions at-work`, gaining the co-workers and the exchanges in text and JSON; `ask channel` gains the
  addressed rows in wave 3. No new verb.
- Words: catalogue keys in every shipped catalogue (D222); *exchange* and *sender* are code and prose only, never window terms; the
  window says *asked*, *answered*, *waiting on*, *taken*.

## 8. What changes in the decisions, and how older records read

| Decision | Changes | Stands |
|---|---|---|
| D32, D115, D65 | — | the quest is the one request across repositories; a lane is how a repository asks itself; the intake publishes as its ask |
| D83, D135 | a choice's press names its question (§5.2) | the park, one go-ahead per act, the person's answer |
| D155 | findings keyed `opinion:<id>` to the line's head; an answered park takes them; new opinions write `serves` | the gate, the recheck, no agent summoning a reviewer |
| D163, D199 | their hands go through the one door; the 16-link bound retires | one turn, two at once, the clone, the frozen commit, the merge; `partner_ask` stays COAGENT1d's |
| D167 | wave 3: every origin has a channel; a part's post may be addressed to a sibling part | ownership local; a message never starts a run |
| D188 | point 5 gains §4.1's clauses; delivery apart from outcome; the answers-alone count kept durably | the levels, who sends each, no level stops a turn |
| D189 | the brief gains *What this line waits on* | the brief's other contents, no model |
| D203 | point 7's card per exchange is read from the exchanges; COWORKUX1a's exchange read is this design's | points 1 to 6 |
| D208, D209 | wave 3: the steward desk's request history persists; a live request is *interrupted* at restart | an appointed role answers within its grant and is never an address |
| D212 | its read gains the sender beside the work | the move record, the store, the map |
| D176, D177, D178, D89, D158, D170, D219 | — | their own cases |

Older records: no stored author changes, so old words read by their old marks; an unkeyed opinion word is still guarded by
`opinion.Handed`; helper and partner state files keep being read; channel rows with no addressed recipient read as today; a driver
against a host without `answers: 1` keeps today's hand; an old question word with no `question` id is a press of an unknown question,
drawn as before.

## 9. File by file

Opus's §7 table is the map for rows a to f and h to i, with these changes: `Exchanges.cs` replaces `LineExchanges.cs`; the question
row is `Svc/Sessions/QuestionBox.cs` and the say door's choice check, not a new record; `channel_addressed` replaces
`channel_part_acks`; the partner's files are not touched. Codex's §7 adds `Drv/Course/ContinuationAdmissions.cs`,
`Drv/Course/Domain/AnswersAlone.cs:28` kept as the legacy reader, `Drv/Store/DesktopStore.cs` (the admission family, a store step
with its writer stamp), `Mod/Loop/NeedsAPerson.cs` keyed by obligation, and `Mod/Platform/StewardDesk.cs:106`'s history.

## 10. Rows, in build order

Wave 1 changes the foundation (who a word is from, a protocol between processes, what a wait is read from, a store family): it is
built and merged as one wave, after REVIEW2 by the chunks' order. Each row writes its own line of D225's note.

| Row (lanes; wave) | Contract | Proof |
|---|---|---|
| **COMMS1a** the sender, read (service, driver, web; 1) | §3.3; three twins; `Persons`, `Words` and the page's author read it | `fixtures/senders.json` held row for row by both `SendersTests` and `sender.test.ts`; a partner's word drawn as a partner's; no solicited answer drawn as *The target Daoris composed*; en and zh |
| **COMMS1b** one door for solicited answers (service, driver; 1) | §4.2; the opinion keyed to the head; `serves` on new opinions; `answers: 1`; `SolicitedHand` | a table test (case × the head's state × level) through each desk; a hand-over between ask and answer for each case, the answer reaching the new part; a lost answer retried and settled, no second word; an older host keeping today's hand; a Process class: a stand-in lead asks a helper, is handed over to another agent, the new part takes the answer once |
| **COMMS1c** one origin rule (service; 1) | §4.3 | a session on a quest a sibling of an ask published holds its go-ahead on that ask; the channel and the go-ahead agree |
| **COMMS1d** the exchanges, read (driver, modules; 1) | §3.4, delivery and outcome apart, `available` and `source`; `HelperWaits`, `Observation`'s input and `SessionGroups` read it with no behaviour change | `ExchangesTests` per case, delivery and outcome; `ObservationTests`, `HelperWaitsTests`, `SessionGroupsTests` unchanged; the read equal across a restart; after a hand-over the head's read holds its predecessor's open exchanges; an unread source said, never empty |
| **COMMS1e** a question's answer names the question (service, driver, web; 1) | §5.2 | a question replaced while the person answers: the older press refused beside its control, the newer one shown; a double press taken once; free words never read as a choice |
| **COMMS1f** the brief says what the line waits on (driver; 1) | §5.4's piece, bounded, Daoris's words | `ContextBriefTests` golden; a hand-over while a helper reads tells the new part |
| **COMMS1g** the answers-alone count, kept (driver; 1) | §5.3; a store step and its writer stamp; the legacy reader kept | a mixed helper, opinion and supervisor sequence; two answers queued together; a hand-over and a restart keep the count; missing history holds as *unknown*; no reset by a wrapper; the chat's no-wake rule unchanged; the waited quest's result per §11's answer |
| **COMMS1h** the conversation draws each message once (web, modules; 2) | §7: the exchange card, the sender chips, the reference line, the receipts' words | page tests in en and zh; stories at 80, 39 and 25rem and the 300 px dock, both themes; keyboard order |
| **COMMS1i** *Waiting on* and *Co-workers* (web, modules, driver; 2) | §7; *At work*'s co-workers; `sessions at-work`; Ask Daoris's room reads the exchanges | `AtWorkView` and `stripVersusView` tests; the verb's text and JSON; a story per member state; `HelpCoverageTests`' row |
| **COMMS1j** *What needs you* by obligation (modules, web; 2) | §4.1 clause 3; one key per obligation | the same wait on the head, the foot and *Notifications* counted once; an unread message makes no count; a supervisor's held decision is no wait, its deferral is |
| **COMMS1k** a channel for every origin, and parts that hear each other (service, driver, web; 3) | §4.4; `channel_addressed`; `ask channel`'s addressed rows | two alongside parts posting to each other; an exact recipient refused by a build without it, never broadcast; a replacement part inheriting its line's pending row; `ChannelReachTests`; stories |
| **COMMS1l** the steward desk's history (modules; 3) | its requests persist, a live one *interrupted* at restart; no grant revived | restart marks interruption; a revoke between admission and act; a role's name in content grants nothing |
| **COMMS1m** the doctrine (canon; after k) | the co-working skill: the acts, the sender's words, posts to sibling parts, what is not available; project-agnostic | the canon's tests; `daoris check` here and in both examples; `npm run verify` |
| **COMMS1n** the installed proof (the steward; last) | a helper across a hand-over; a lane part's post to its lead; a stale question's press refused; a third mixed continuation held | dated shots at 1546 and 680 px, en and zh; the records read back |

## 11. Open

- **The owner's:** whether a quest's result that a line waited on (D79's resume) counts toward the answers-alone bound. Recommended
  yes: a line that hands a row on, waits, resumes and hands on again is otherwise bounded only by drive, cap and strikes
  (COWORKVIEW1 R11.4). Row g builds the count with the waited quest's result as a cause where the answer is yes, and without where
  it is no.
- **Unmeasured:** a real agent's helper answer across a hand-over; two alongside parts posting to each other; zh at 300 px in the
  dock; whether agents use sibling posts as the doctrine says.
- **Model tiers:** every judgement here is the no-model tier, .NET and TypeScript over records. The model-dependent acts are an
  agent's choices to ask, post or wait, and the agent, account and model that ran are on its binding.

## 12. Rejected

- **One inbox table for every kind** (D188 rejected a store for every message); **a stored `sender`** (a copy that drifts and older
  builds never write); **answers through the channel** (a claim never starts a run; a solicited answer must).
- **Messages in place of quests**; agents addressing lines; *now* for anyone but the person; urgency judged by a model.
- **A new message tool family beside the channel's** (Codex): the same act under two names doubles what an agent must be taught.
- **Immutable question revisions in a new record** (Codex): the file's per-question `id` gives the correlation the stale press needs.
- **`partner_ask` in this design** (Opus): it would offer a start whose confinement is unmeasured.
- **Retiring the boxes into one file**: each box has one writer per file and its own admission (`Svc/Sessions/HelperBox.cs:6-10`).
- **A chat waiting on a quest** (R11.2 point 5 stands); **an agent summoning a reviewer** (D155 point 5).
