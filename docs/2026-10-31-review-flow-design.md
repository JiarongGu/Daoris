# REVIEWFLOW1 and LINESTOGETHER1: a session's page as one flow, and its lines brought together

**Standing:** a synthesis, 2026-10-31, revised that day on both makers' reads (`2026-10-31-review-flow-reads.md`): REVIEWFLOW1a accepted
alone by the steward's settlement and building; the rest answers every finding (§7), to be read again (D191). The steward's, from the brief
and the sealed proposals of the Opus maker (O) and Codex (X); claims checked at `5c40345f7`. Paths: the web's under `src/Daoris.Web/src/`,
the driver's and modules' under `src/Daoris.Desktop/`, the service's under `src/Daoris.Service/Daoris.Service.Core/`.
**Governing:** D167 (its 2026-10-15 amendment), D168, D87, D137, D150, D209, D212, D215.

**Words** (O). The brief's *line* is the page's *session* (会话); *line* stays 主线. A session's **head** is the part it goes on in
(续段); a member whose work its head carried into the head's landing is **brought into** it (已并入). Both join the glossary; *contained*
(已包含) stays the line's.

## 0. Where the two proposals agree

One Accept per session (D168 point 3); words go where they were said; overlaps merge nothing; acceptance stays the person's.

## 1. The flow of a session's page

**Facts.** *Accept all* (`EntryAccept`) is drawn only under *Parts* (`work/AttendedSession.tsx:310-311`), only where two or more parts offer work
(`work/WorkFrame.tsx:1902-1907`). The work wait stands only on the attended part's `lands` (`work/waits/waits.ts:181-183`), drawn at the
head, in the foot strip (`work/waits/WaitStrip.tsx:298-320`) and the review's foot (`work/review/DiffPane.tsx:458-475`). So the foot never
offers the session's, and a head with no work beside a part with work offers no Accept.

**Choice (O).** Each step led by a line saying where the work stands and what comes next:

| Step | Its line | Its presses |
|---|---|---|
| Head | facts led by *now · next* (*working*, *waits on you*, *3 parts to land*, *integrating*, *landed*), *next* a door | the waits (D168), the session's *Accept…* among them |
| Session | *Part 6 of 7 is the head · 1 beside it · 1 other session changes the same files* | *Bring them together…* (§4) |
| Milestone, Conversation | facts (R4); earlier parts, waits anchored | none |
| Foot | the strip, then the box saying where a word goes (§2) | the waits' presses; *Send* |

**The session's Accept is its work to land (REVIEWFLOW1a, accepted; web only).** Where a session has more than one part and one or more
offer work, its *Accept…* is the work wait wherever D168 draws one, and the block under *Parts* goes: one owner, the frame's `entryAccept`,
its ask in one place. With one part offering, it lands that part as the entry's would (D167's amendment point 2); a record alone keeps its
part's Accept. A part's gates stay its own lines in `Lands`; the leave-out stays as built (`work/review/EntryAccept.tsx:169-181`).
*Integrating* is INTEGRATE1h's fact already (`work/head/headFacts.ts:66-67`, `:163`), which *now · next* reads.

**Leave part n out, per tree (REVIEWFLOW1d).** The driver leaves a part out only where it shares no earlier part's tree
(`Daoris.Desktop.Driver/Workplace/EntryLands.cs:128`), and a continued part keeps its directory, so *Leave part 1 out* of a chat would
leave out all its parts while `Outcome` (`:430`) reported the rest `same-tree`, and a part a kept later part grew from (`:246-247`) lands
anyway. So the offer is per tree, on its first part, naming every part it takes; `Sequence` marks each part of a left-out tree `left-out`;
it stands on any tree of its own but a clash, and on no part a kept later part carries, which the driver refuses in words. D167 points 2
and 3 change by a note.

**Why.** D168 answered "scroll back to the top" for every wait but ACCEPTALL1's. **Rejects:** a part's Accept beside the session's; X's
Accept in the review's foot alone, since an ended session opens at its head (D168); a new head line (HEADFIT1); O's leave-out on every tree.

## 2. A session's head, and where a word goes

**Facts.** The service walks forward to a record's head (`Sessions/Lines.cs:41-67`), following only `from` and a driven part's quest
(`:49-55`). A chain step names a parent quest, no `from` (`Daoris.Desktop.Driver/WorkOrders/SessionEntryReader.cs:111`); an ask's sibling
quests share its entry for a repository (`ask:<id>/<repository>`). The say door names the head (`Sessions/SessionLedger.cs:1097`). A row
opens on its *leading* part (`SessionEntryReader.cs:53-54`): in Lyntai, the quest's part in *Resumes later*, where the word waited.

**The head (LINESTOGETHER1a; O's head, X's reader).**
- A part's **line** is the walk, plus the hop it lacks: a chain step continues its parent's line, so a chain's head is its newest step's.
  The session's head is where its first part's line ends.
- A line begun by a quest a part published (`PartBegan.Asked`, `SessionEntryReader.cs:110`) is **beside** it. In Lyntai part 6 is the
  head, the quest beside it, the filing another session.
- An ask's sibling quests each begin a line none continues: the session names **no head**, each part marked with its own line's.
- The service answers it beside the `entry` key; the driver carries `head` beside `leading`. A walk ending on a cycle or the bound
  answers none. The row opens on the head unless a part waits on the person or is working.

**Where a word goes (LINESTOGETHER1b).**
- A word goes where it was written, to the end of that part's walk; Daoris never moves it by itself.
- Before the person speaks, the box's first line says where and when, from `SESSION_QUEUE`'s reach (`work/say.ts:15-26`): *To part 6,
  the head · at its turn's end*; *To part 7, beside the head · when the cap frees a seat*, with *To the head*, carrying the draft (X).
- **A held word moves by one press, in one step.** The take-back removes the word (`SessionLedger.cs:1512`) and answers only done or
  *taken* (`Daoris.Desktop.Modules/Bridge/Routes/ConversationRoutes.cs:405`): a take-back then a say loses it to a crash between. *Say it
  to the head instead* is one service door, `SESSION_QUEUE_MOVE`: inside one `ExclusiveAsync`, a SQLite `BEGIN IMMEDIATE` transaction
  (`Sessions/Sessions.cs:839-853`), it walks to the head, judges it as the say door does, takes the word and keeps it there through
  `KeepOwnersWordAsync` (`SessionLedger.cs:963`): both or neither. Its identity is its producer key, `move:<part>:<word>`: a retry finds
  it waiting (`:1115-1116`) or settled (`said_settled`, `Sessions/SaidKeys.cs:10-14`) and answers *moved*, never a second word. Otherwise
  *taken* or a refusal, the word unmoved.
- It is offered on a word with no files, on the record of a part beside the head that is not running (`SessionLedger.cs:1495-1502`),
  while the head keeps words on its record (ended, parked or winding up, `:1101-1104`). A word in a driver's queue behind a running turn
  (`ConversationRoutes.cs:386-395`) is offered none: no transaction spans it.
- **Uncertain delivery.** A move whose answer never came is *not known*, never *moved*: the page reads both queues and asks again under
  the same key, which says what happened.

**Why.** The leading rule answers *what needs you*, not *where it goes on*. **Rejects:** a head re-derived on the page (both; X rejected
rebuilding it in React) or by tree; an ask's first sibling as its head; re-addressing words; a move as a take-back and a say.

## 3. Related sessions of one repository

**Facts.** INTEGRATE1l compares running quests only (`Daoris.Desktop.Driver/WorkOrders/Work/WorkOverlaps.cs:80-81`) and tells their
agents (`:51-73`); the page says an overlap only for a start it holds (`data/signals.ts:258-259`). Its read,
`git diff --name-only <base>` (`:41`), compares the working tree, uncommitted edits included.

**Choice (both).** The driver compares every session of a repository with a tree here whose work is not on the line, chats included. A
tree's read is kept by base and commit only while it has no uncommitted path and its session is not running; any other is read at each
look. A read failing after a good one says *not read*, naming the earlier read's time. The Session step names them (*Beside it: 2
sessions change `TASKS.md`*), each a door to its head, by kind (X): *continues*, *asked by*, *declared overlap*, *changed overlap*; a
teammate's *not read here* (D137). **The person decides** they become one, by *Bring them together…*; the steward once its form is built.

**Rejects:** merging on overlap (integration design §9); a read kept by commit alone; the head deciding for its peers (X); a second list
on the Integration tab.

## 4. The one door (LINESTOGETHER1d)

**Choice: the head replays the others onto its branch; then one Accept.**

**The words.** The ask lists the members the person ticks (parts beside the head, related sessions), each with branch, base and tip, and
Daoris's fixed words, shown before the press as the rebase words are (`work/review/LandingSync.tsx:50-55`): *replay `<branch>`'s commits
from `<base>` to `<tip>` onto your branch, oldest first; settle each conflict in the commit that meets it, by what its session meant; make
no merge commit; say when done.* A landing judges commits merges aside (`Workplace/LineHolds.cs:296`) and replays them by a plain
`rebase --onto` (`Workplace/BranchReplays.cs:517`) once the line has moved (`Workplace/TreeLands.cs:574`): a resolution in a merge
commit is dropped; one in a replayed commit travels.

**The record.** The press records an operation in the desktop store (Workplace's family): the head's line and record, each member's
session, tree, base and tip, the phase; then says the words through the say door under the key `together:<operation>`. A session is in at
most one open operation, judged under the store's lock.

**It refuses**, where pressed (R3): another repository's or a teammate's session; a member with a live part, a start waiting, uncommitted
work, an unread tip, a landing or an integration request; a member whose workflow steps on past its landing, its unit's request coming
only from its own landing (`Workplace/IntegrationRequests.cs:10`); a head that takes no words, runs elsewhere or lost its tree.

**The head's line.** An ended head keeps the word for its next part (`SessionLedger.cs:1103-1104`), so the words open a successor by
`from`, in the head's tree. The operation is bound to the head's line and moves to the successor that took its keyed word
(`said_settled`). It ends *stale*, holding nothing (X), only on a move it did not make: another word, hand-over or stop moving the line,
or a member's tip moving before *checked*.

**Phases**, on the head's page and each member's (R2): *asked*, *replaying* (live, timed), *checking*, *ready to accept*. Checking reads
the head's tip, never the agent's claim nor ancestry (integration design §5.7): no merge commit since the press; then for each member,
what the tip holds of its commits by content or patch (`LineHolds.JudgeAsync`, `LineHolds.cs:73`), the rest replayed over trees onto the
tip (`TrialTreeAsync`, `:135`). An empty replay is contained; a conflict is contained only where the head's commits since the press
changed each conflicted file, its settlement, which the Accept's plan names. Anything else fails, named, with *Ask again*. *Let them go*
ends it before the Accept; what the head made stays, said.

**Guards at every landing door.** Every landing passes `TreeLands.LandAsync` and its tree's hold (`TreeLands.cs:405`): a part's Accept
(`Daoris.Desktop.Modules/Bridge/Routes/TreesRoutes.cs:621`), an entry's or an ask's (`EntryLands.cs:312`), the terminal's
(`Daoris.Desktop.Driver.Host/TreesConsole.cs:470`, `Review/ChatLandCommand.cs:64`) and an automatic acceptance
(`Workplace/AutoLander.cs:337`). There, before anything is made, a tree bound to an open operation refuses, naming it, unless the
landing is its combined Accept: another window, the terminal and automatic acceptance are refused in the driver, not by the page's hold
(`DiffPane.tsx:83-84`). A bound tree's discard refuses too.

**The combined Accept** is the head entry's, in `EntryAccepts` (one code for `LAND_ENTRY`, `LAND_ASK` and `trees land --entry|--ask`,
`Review/EntryAccepts.cs:4-6`), which reads the operation bound to the entry's head:
- **Members' gates.** Each is read as its own Accept reads it, by its tree, quest and session (`EntryAccepts.cs:114`), pinned to its
  bound tip; its result and submission likewise (`:119-131`). The preflight that holds an entry on one part's gate (`EntryLands.cs:279`)
  reads members too, so a clear head with a member held by review, opinion or workflow lands nothing.
- **Recheck.** Under the head tree's hold, before anything is made (`judged`, `TreeLands.cs:416`): the head stands at its checked tip,
  pinned as a gate's tip pins (`:593-597`, `:618`); each member is still bound and read contained again, its result judged as its own
  part's is (`EntryLands.cs:310-315`), and again before the push. A head moved since lands nothing and is checked again.
- **Never landed twice.** `PartsOf` lists every part with work (`EntryAccepts.cs:26-30`), members beside the head among them;
  `Sequence` marks a member `brought-into`, a step that does not land (`EntryLands.cs:100-104`).
- **Inclusion records.** The head's landing record keeps each member as a `LandedWork` (session, bound tip, quest, unit, submission;
  `Daoris.Desktop.Driver/Knowledge/Domain/WorkplaceFacts.cs:301-311`) with its gate's proof, as an advance keeps its own
  (`TreeLands.cs:636`), read by `Work()` (`WorkplaceFacts.cs:166`), so the push's judgement (FABRIC-CLOSE1h11) covers members; each
  member's conversation keeps a note, as a left-out part's does (`EntryAccepts.cs:81-84`).
- **Workflow settlement.** A member's landing step reads the landing that names its session
  (`Daoris.Desktop.Driver/Workflows/Domain/WorkflowRun.cs:986`; `Names`, `WorkplaceFacts.cs:225`); `Names` reads members too, and the step
  says *brought into `<branch>`*. The rule's `integrate` is the head's landing's (integration design §17.4): one request for all.

Each member then reads *brought into* that landing, never *landed* (X). Its work to the bound tip offers no Accept; settled commits need
not read on the line, so the inclusion record offers its discard, keeping its recovery ref (SQUASHTIDY1c). Later commits are its own.

**Why.** One review, one Accept, one landing, by the session holding the shared records' context; the price is this section's contracts.
ACCEPTCONFLICT1 left this door here (`docs/decisions/D167.md:447-448`). **Rejects:** X's door through INTEGRATE1, needing an acceptance
spanning sessions (D215 point 4) and landing three branches; a merge and ancestry (B2); Daoris replaying for the head; acceptance inside
the door (X).

## 5. What the person presses

D209 §3.1 rows 1 to 8 are built dark (`Daoris.Desktop.Modules/Bridge/StewardActRows.cs:154-163`) until STEWARDROLE1d (`TASKS.md:84`).

| Press | The steward |
|---|---|
| *Stop*; a park's *Finish…*; *Try again* | dark: rows 6 to 8 (a retry only after its own stop) |
| *Hand over…*; an agent's hand-over ask | later: STEWARDROLE1e (`TASKS.md:85`) |
| *Ask a helper*, pause or resume, archive | no: not a role act |
| *Delete*, *Discard tree*, *Discard branch…* | no: deletion |
| *Decline…*, a park's answer, go-aheads, *Allow…*, a permission's or helper's answer | no: the owner's word or a widening |
| *Accept…*, *Leave part n out*, verdicts | no: acceptance |
| *Ask to rebase*, *Bring them together…*, *Ask again*, *Let them go* | later: LINESTOGETHER1e |
| Box: *Send*, *Send back…*, *Say it to the head instead*, urgency, *Stop turn*, a held word's reword or removal, model and effort (`work/WorkFrame.tsx:2655-2674`) | no: the owner's conversation |
| Integration: *Drop*, *Check again*, *split*, *hand back*, *order*, *request* | later: INTEGRATE1i (`TASKS.md:28`); *land* never |
| Folds, doors, *Review*, folder, terminal, detach, copy id (`work/acts.ts:31-52`) | the viewer's own |

**Rejects:** an appointment as blanket approval; a steward's act recorded as the person's (X); a later act listed as available.

## 6. The rows

Each proof seen failing first. REVIEWFLOW1a, 1d, 1b and LINESTOGETHER1a's page half share the frame's files and run one after
another, in that order.

- **REVIEWFLOW1a — the session's Accept is its work to land** (web; accepted, building). Proof: dated shots of today's page first;
  `WaitStrip` and `DiffPane` stories, en and zh, both themes; a `WorkFrame` test: one *Accept…*, a foot press disabling the others, a
  head with no work beside a part with work offering it; shots at 1546 and 680 px.
- **REVIEWFLOW1d — *Leave part n out*, per tree** (driver, web; D167's note). Proof: `EntryLandingsTests`: a left-out tree's parts all
  `left-out`; one under a kept later part refused; an `EntryAccept` test naming the parts taken, en and zh.
- **REVIEWFLOW1b — *now · next*** (web). Proof: a story per phase; `headFacts` tests, en and zh.
- **LINESTOGETHER1a — the head** (service and driver now, web after 1b), the glossary's *head*. Proof: service tests over a chat of
  three parts, a driven part gone on as a chat, a retry, a chain, a dispatched quest beside it, an ask's siblings (none), a cycle (none);
  page tests opening on the head, a waiting and a working part.
- **LINESTOGETHER1b — where a word goes** (service, modules, web). Proof: a move in one transaction; a fault after the take, the word
  unmoved; a keyed retry *moved*, one word on the head; *taken*; a running head refusing; page tests, en and zh, a lost answer *not known*.
- **LINESTOGETHER1c — related sessions** (driver, web). Proof: a git fixture of three branches (a chat's tree, a quest beside it, another
  conversation's): declared and changed paths apart; a clean ended tree not read again; an uncommitted edit seen; a failed read *not read*;
  page tests, en and zh.
- **LINESTOGETHER1d — *Bring them together…*** (driver, modules, web), the glossary's *brought into*. Proof: a git fixture of three
  branches (the head, a part beside it, another session): clean, and a conflict settled in a replayed commit, landed by branch, merge and
  squash rules on a line moved after the head's turn; a head that merged failing *checking*; a member held by review, opinion or workflow;
  another window's, the terminal's and an automatic landing of a member refused; the head reset after *checked*; carried to its
  successor, stale on another word; *Let them go*; a restart; a member part landed once.
- **LINESTOGETHER1e — the steward's fixed forms** (modules, driver; after STEWARDROLE1d and 1d; D209 point 3's note). `ROLE_ACT` forms for the rebase
  words and §4's presses. Proof: refusals of another repository, a pushing landing, an owner-held session and the owner's operation; the
  steward's generation recorded; a revoke cancelling an operation not yet said.
- **REVIEWFLOW1c — the installed window**, last: Lyntai's case opened on the head, a held word moved and a lost answer recovered, brought
  together, one *Accept…* from the foot; shots at 1546 and 680 px, en and zh, both themes.

## 7. How each finding was resolved

| Finding (read) | Resolution | Where |
|---|---|---|
| Uncombined gates (X, blocking) | Members' own gates and submissions, pinned, rechecked; `LandedWork`; the landing step; `brought-into` | §4 |
| Replay drops merge resolutions (X, blocking; O, B2) | No merge commit; checked by content, patch and trial replay | §4 |
| Page holds bind nothing (X, blocking) | A guard under every landing hold; one operation per session; a recheck at the Accept | §4 |
| The word's two-call gap (X, blocking) | One transaction, a stable key, *not known* until answered | §2 |
| Leave-out widening (O, B1) | Per tree; `left-out` throughout; D167's note; its own row | §1 |
| The door staling itself (O, B3) | Bound to the head's line, carried to its successor | §4 |
| Chain and sibling heads (O, N1) | The newest step; none for siblings; opening rule | §2 |
| No Accept beside the head (O, N2) | The wait on the session's offer | §1 |
| *Integrating* (O, N3) | INTEGRATE1h's fact | §1 |
| Overlap cache (X; O, N4) | Clean ended trees only | §3 |
| Press inventory (X) | Dark, later, never; conversation controls | §5 |
| Glossary (O, N5) | *Head*, *brought into* | Words |
| Misattributed rejection (X) | Corrected | §2 |
| Rows (both) | 1d split out; shared files in sequence | §6 |
