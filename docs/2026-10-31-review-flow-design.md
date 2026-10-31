# REVIEWFLOW1 and LINESTOGETHER1: a session's page as one flow, and its lines brought together

**Standing:** a synthesis, 2026-10-31, revised that day on both makers' reads (`2026-10-31-review-flow-reads.md`): accepted under D191
for every row but LINESTOGETHER1d (REVIEWFLOW1a built); the door's section (§4, rows 1d, 1e, REVIEWFLOW1c) revised once more on
Codex's second read, to be read again. The steward's, from the brief
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
from `<base>` to `<tip>` onto your branch, oldest first; settle each conflict in the commit that meets it, by what its session meant,
naming the commit it settles in a `Supersedes: <commit>` trailer; make no merge commit; say when done.* A landing replays commits
merges aside (`Workplace/LineHolds.cs:296`, `Workplace/BranchReplays.cs:517`) once the line moves (`Workplace/TreeLands.cs:574`): a
merge commit's resolution is dropped; a replayed commit's travels.

**The record.** The press records an operation in the desktop store (Workplace's family): the head's line and record, each member's
session, tree, base and tip, the phase; then says the words through the say door under the key `together:<operation>`. A session is in at
most one open operation.

**One lock order, across processes.** The store's lock alone misses a landing past its guard under its tree's hold
(`TreeLands.cs:405-416`). Binding, landing and discarding take these in this order, each a file whose handle is the lock across
processes, let go by a dead one (`Workplace/LandingHolds.cs:27-30`):
1. each bound tree's landing hold (`:79`), ascending by its file (`:103`);
2. the repository's tree lock, shared as a start takes it (`Workplace/TreeLock.cs:50`);
3. the operation store's record lock (`Store/RecordLock.cs:42`).

The press, *Let them go* and an end take all three; the press judges its refusals and pins tips under them. A landing keeps
(1) to its end and reads the bindings under (3) as soon as it holds it, before `judged` (`TreeLands.cs:416`): whichever binds or lands
first, the other refuses, naming it. A discard holds (2) alone (`Workplace/TreeRemovals.cs:52`) and reads them under (3). A holder of
the head's (1) writes the phases under (3). Nothing holding (2) or (3) takes (1), as nothing does today; every wait is bounded.

**It refuses**, where pressed (R3): another repository's or a teammate's session; a member with a live part, a start waiting, uncommitted
work, an unread tip, a merge commit, a landing or an integration request; a member whose workflow steps on past its landing, its unit's
request coming only from its own landing (`Workplace/IntegrationRequests.cs:10`); a head that takes no words, runs elsewhere or lost its
tree.

**The head's line.** An ended head keeps the word for its next part (`SessionLedger.cs:1103-1104`), so the words open a successor by
`from`, in the head's tree. The operation is bound to the head's line and moves to the successor that took its keyed word
(`said_settled`). It ends *stale*, holding nothing (X), only on a move it did not make: another word, hand-over or stop moving the line,
or a member's tip moving before *checked*.

**Phases**, on the head's page and each member's (R2): *asked*, *replaying* (live, timed), *checking*, *ready to accept*, *landing*.
*Let them go* ends it before *landing*; what the head made stays, said.

**Checking accounts for every member change** from the head's tip, never the agent's claim nor ancestry (integration design §5.7):
- **No merge commit** in the range a landing replays, the line's tip to the head's (`LineHolds.cs:82`, `:296`).
- What the tip holds of a member's commits by content or patch (`LineHolds.JudgeAsync`, `:73`) is *held*.
- Each other commit is tried alone onto the tip as `TrialTreeAsync` tries a step (`:146-188`), past any conflict, where that trial
  stops (`:191-195`). Git writes a tree even in conflict, so each path the commit changes is *contained* (as the tip has it), *missing*
  (changed cleanly) or *unproven* (in conflict).
- An unproven path is *superseded*, never contained, only where a head commit since the press changed it and names its commit in a
  `Supersedes:` trailer; the Accept's plan shows both diffs. Any other unproven or missing change fails, named, with *Ask again*.

**Guards.** Every landing door passes `TreeLands.LandAsync`: a part's Accept
(`Daoris.Desktop.Modules/Bridge/Routes/TreesRoutes.cs:621`), an entry's or ask's (`EntryLands.cs:312`), the terminal's
(`Daoris.Desktop.Driver.Host/TreesConsole.cs:470`, `Review/ChatLandCommand.cs:64`) and automatic acceptance
(`Workplace/AutoLander.cs:337`). There a bound tree refuses, naming its operation, unless this is its combined Accept; so does its discard.

**The combined Accept** is the head entry's, in `EntryAccepts` (`Review/EntryAccepts.cs:4-6`), which reads the operation bound to the
entry's head:
- **Members' gates.** Each read as its own Accept reads it (`EntryAccepts.cs:114`), pinned to its bound tip, with its result and
  submission (`:119-131`); the preflight (`EntryLands.cs:279`) reads members too, so a member held by review, opinion or workflow lands
  nothing.
- **Recheck.** Under the head tree's hold, before anything is made (`judged`, `TreeLands.cs:416`): the head pinned at its checked tip
  (`:593-597`, `:618`); the range and every member, still bound, accounted again, each result judged as its own part's is
  (`EntryLands.cs:310-315`), and again before the push. A moved head lands nothing.
- **Never landed twice.** `PartsOf` lists every part with work (`EntryAccepts.cs:26-30`), members beside the head among them;
  `Sequence` marks a member `brought-into`, a step that does not land (`EntryLands.cs:100-104`).

**Inclusion, in every form.** A merge keeps no landing record (`TreeLands.cs:771-772`) and its workflow step reads the conversation's
note (`Daoris.Desktop.Driver/Workflows/Domain/WorkflowRun.cs:995`), so inclusion cannot rest on `Work()` or `Names()`
(`Daoris.Desktop.Driver/Knowledge/Domain/WorkplaceFacts.cs:166`, `:225`). The operation is the inclusion record:
- Before anything is made, the Accept writes *landing* under (3): the form, the head's pinned tip, and each member's session, bound tip,
  quest, unit, submission, accounting and gate proof, as an advance keeps its own (`TreeLands.cs:633-643`).
- Once landed, *brought into* names the branch and tip, or the line. A branch's or squash's record or advance (`:633-676`) lists the
  members in the same write, so `Work()` and `Unpushed()` carry them to the push's judgement (`:717-718`) and an integration request
  reads their proofs (`IntegrationRequests.cs:22-24`).
- A member's workflow landing step reads that operation before `facts.Accepted`: *brought into `<branch>`* or *the line*; `integrate`
  stays the head's landing's (integration design §17.4). Its conversation's note, as a left-out part's (`EntryAccepts.cs:81-84`), is
  never read as an acceptance.
- **Restart.** The next holder of the head's tree settles *landing* first, from records made since: the head session's landing record
  or advance (branch, squash), or a merge's integration request for its branch (`TreeLands.cs:297-301`,
  `Workplace/IntegrationPresses.cs:22-34`). Settled, *brought into*; open, still *landing*; none or closed unsettled, *ready to accept*.

Each member then reads *brought into*, never *landed* (X): no Accept for its work to the bound tip, its discard offered with its
recovery ref kept (SQUASHTIDY1c); later commits are its own.

**Why.** One review, one Accept, one landing, by the session holding the shared records' context; the price is this section's contracts,
met in every form, so no narrower scope is needed. ACCEPTCONFLICT1 left this door here (`docs/decisions/D167.md:447-448`). **Rejects:**
X's door through INTEGRATE1, needing an acceptance spanning sessions (D215 point 4) and landing three branches; a merge and ancestry
(B2); containment by the files the head touched (X); inclusion by landing record alone (X); Daoris replaying for the head; acceptance
inside the door (X).

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
- **LINESTOGETHER1d — *Bring them together…*** (driver, modules, web), the glossary's *brought into*. Proof: a three-branch git
  fixture (the head, a part beside it, another session) landed by branch, squash and merge rules on a line moved after the head's turn:
  clean; a conflict superseded by a named commit; a conflicting first commit with an omitted later clean one, and a merge commit in the
  replayed range, failing; a member held by review, opinion or workflow; a landing paused past its guard while the press binds, and a
  press paused bound while another window, the terminal's process, an automatic landing and a discard try a member, each refused; the
  head reset after *checked*; carried to its successor, stale on another word; *Let them go*; inclusion, the workflow step and the push's
  judgement per form, a merge waiting on its line's run among them; a restart at *landing* settled each way; a member landed once.
- **LINESTOGETHER1e — the steward's fixed forms** (modules, driver; after STEWARDROLE1d and 1d; D209 point 3's note). `ROLE_ACT` forms for the rebase
  words and §4's presses, through §4's lock order. Proof: refusals of another repository, a pushing landing, an owner-held session and
  the owner's operation; the steward's generation recorded; a revoke cancelling an operation not yet said, as *Let them go* does.
- **REVIEWFLOW1c — the installed window**, last: Lyntai's case opened on the head, a held word moved and a lost answer recovered, brought
  together, a superseded commit shown, one *Accept…* from the foot, the member *brought into*; a bound member's terminal landing
  refused; shots at 1546 and 680 px, en and zh, both themes.

## 7. How each finding was resolved

| Finding (read) | Resolution | Where |
|---|---|---|
| Uncombined gates (X, blocking) | Members' own gates and submissions, pinned, rechecked; `brought-into` | §4 |
| Replay drops merge resolutions (X, blocking; O, B2) | No merge commit in the replayed range; content, never ancestry | §4 |
| Page holds bind nothing (X, blocking) | A guard under every landing hold; a recheck at the Accept | §4 |
| Containment by touched files (X, round 2) | Every member commit and path accounted; *superseded*, never contained | §4 |
| Binding races landing (X, round 2) | One lock order across processes | §4 |
| Merge keeps no record (X, round 2) | The operation as inclusion record; restart from records | §4 |
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
