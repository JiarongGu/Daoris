# REVIEWFLOW1 and LINESTOGETHER1: a session's page as one flow, and its lines brought together

**Standing:** a synthesis, 2026-10-31; nothing built; accepted only on both makers' reads (D191). The steward's, from the brief
(`2026-10-31-review-flow-brief.md`) and the sealed proposals of the Opus maker (O) and Codex (X), each kept claim checked at
`fe6631ac8`. Paths: the web's under `src/Daoris.Web/src/`, the driver's and modules' under `src/Daoris.Desktop/`, the service's under
`src/Daoris.Service/`.
**Governing:** D167 (its 2026-10-15 amendment), D168, D137, D150, D209, D212, D215, D191.

**Words** (O). The brief's *line* is the page's *session* (会话, D167 point 1); *line* stays 主线. A session's **head** is the part it
goes on in (glossary: *head*, 续段).

## 0. Where the two proposals agree

- One Accept per session, one owner, its ask open in one place (D168 point 3), no part's Accept beside it; no review page of its own
  (UI contract R5); the milestone stays facts (R4).
- Words go where they were said and a held word stays held; Daoris never re-addresses it, raises the cap or releases an owner's hold.
- An overlap is no verdict that two sessions are one: nothing merges by itself; a failed read says *not read*, never *no overlap*.
- The head's agent never decides for its peers; every acceptance stays the person's; the steward makes only fixed forms within D209.
- Relocation first, coordination after, each row usable alone.

## 1. The flow of a session's page

**Facts.** *Accept all* (`EntryAccept`) is drawn only under *Parts* (`work/AttendedSession.tsx:310-311`), where more than one part
offers work (`work/WorkFrame.tsx:1902-1934`). The part's *Accept…* is D168's *work to land*, drawn at the head, in the foot strip
(`work/waits/WaitStrip.tsx:298-320`) and in the review's foot (`work/review/DiffPane.tsx:461-475`). So the foot and the review never
offer the session's.

**Choice (O, with X's door).** Five steps, each led by one line saying where the work stands and what comes next:

| Step | Its line | Its presses |
|---|---|---|
| Head | the facts line (`work/head/headFacts.ts`) leads with *now · next*: *working*, *waits on you*, *3 parts to land*, *landing*, *landed*; *next* is a door to its step (WAITFOOT2b's *To the wait*) | the waits that stand (D168), the work's being the session's *Accept…* |
| Session | its folds (parts, chain, relations, channel) led by *Part 6 of 7 is the head · 1 beside it · 1 other session changes the same files* | *Bring them together…*, on the head only (§4) |
| Milestone | result, clean-up, recovery as facts (R4) | none |
| Conversation | the earlier parts above (SWITCHCTX1c-i), each wait anchored (D168 point 4) | none |
| Foot | the strip, then the box, whose first line says where a word goes (§2) | the standing waits' presses, the head's own (one on screen, WAITFOOT2); *Send* |

**Accept all is the session's work to land.** Where more than one part offers work, the session's *Accept…* takes the part's
*Accept…* place wherever D168 draws it (the head's wait, the foot strip, the review's foot), and the block under *Parts* goes. One
owner, the frame's `entryAccept`, its ask in one place (`WorkAsk`). A part's gates (review, second opinion, rebase words) stay
its own lines in `Lands`. The ask names each part with a door to its review, and offers *Leave part n out* on every part with a tree of
its own but a clash (ACCEPTCONFLICT1): the driver leaves out any part named (`Daoris.Desktop.Driver/Workplace/EntryLands.cs:119-131`),
the page offers it only to a superseded part (`work/review/EntryAccept.tsx:169-181`). So a part lands alone with no second press; the
press stands where reading stops, under the conversation or the files. *Integrating* joins *now* once INTEGRATE1
says it in the session's head (integration design §5.8), unsaid until then (D137).

**Why.** D168 answered "scroll back to the top" for every wait; ACCEPTALL1 never joined its table. **Rejects:** the block under
*Parts*; a part's Accept beside the session's (both); X's Accept in the review's foot alone behind a *Review changes* door, since an
ended session opens at its head and a live one is read at its foot (D168, Rejected); a new line in the head (HEADFIT1's budget).

## 2. A session's head, and where a word goes

**Facts.** The service walks forward to a record's head (`Lines.HeadOf`, `Daoris.Service.Core/Sessions/Lines.cs:41-67`: the newest
record continuing it by `from`, across kinds, or a driven part's quest's last run here). The say door refuses a word to a record the
walk passes as `continued`, naming the head (`SessionLedger.cs:1097`), which callers follow (`Course/GoOn/WordsHead.cs:66`). A row opens
on its *leading* part, the newest of the first group any part is in (`WorkOrders/SessionEntryReader.cs:53-54`; groups ordered *you,
review, working, later, ended*, `Workplace/Domain/SessionGroup.cs:28`). In Lyntai the chat's head had ended, so the row opened on the
quest's part, held by the cap in *Resumes later*, where the person's word waited.

**Choice (both: O's head, X's reader).**
- **The head** is where the service's walk from the session's first part ends. Every other part is earlier on that walk, or
  **beside** it, its own walk ending elsewhere (a dispatched quest, a sibling's request; `PartBegan.Asked`,
  `SessionEntryReader.cs:110`). In Lyntai the chat's part 6 is the head, the quest beside it, the filing another session.
- **The service answers it**, one walk per session, beside the `entry` key it writes; the driver's entry carries `head` beside
  `leading`. A walk ending on a cycle or the bound answers none, and the row opens as today; otherwise **on the head**, unless a part
  waits on the person (*you*).
- **A word goes where it was written, to the end of that part's walk**, as the say door judges today; Daoris never moves it.
- **Before the person speaks**, the box's first line says where and when, from `SESSION_QUEUE`'s reach (`work/say.ts:15-26`) and the
  part's sitting fact (`work.sitting.AtCapacity_cap`): *To part 6, the head · at its turn's end*; *To part 7, beside the head · when
  the cap frees a seat*, with *To the head*, which attends it carrying the draft (X). Unknown *when* is unsaid.
- **A held word stays with its part.** Beside the head, *Say it to the head instead* takes it back by the queue's edit
  (`SESSION_QUEUE_EDIT`, `Daoris.Desktop.Modules/Bridge/Routes/ConversationRoutes.cs:346-355`), keeps its text as the head's draft,
  and says it there. One already taken answers *taken*, moving nothing; a refused say stays in the head's box (R3). A word with files
  is offered no move.

**Why.** The leading rule answers *what needs you*, not *where it goes on*; one head, the service's, serves the say door and the page.
**Rejects:** a head re-derived on the page (X) or by which tree a part holds (O's gloss: alongside and TREEFOLLOW1 parts hold their
own); Daoris re-addressing words; refusing words to a part beside the head.

## 3. Related sessions of one repository

**Facts.** INTEGRATE1l compares running quests only (`WorkOrders/Work/WorkOverlaps.cs:80-81`) and tells their agents (`:51-73`); the
page says an overlap only for a start it holds (`data/signals.ts:258-259`). A chat, holding the shared records, is never compared.

**Choice (both).** The driver compares every session of a repository with a tree here whose work is not on the line, chats included:
one `git diff --name-only` against its base per tree, kept by the tree's commit. The Session step's line names them (*Beside it: 2
sessions change `TASKS.md`*), each a door to its head, by kind (X): *continues*, *asked by*, *declared overlap* (`paths`), *changed
overlap*, with when it was read. An unreadable tree is *not read*; a teammate's, *not read here* (D137).

**Who decides they become one: the person**, by *Bring them together…* on the head's page; **the steward**, once appointed, by the
same press in a fixed form (§5), so the person need not. The head's agent never decides.

**Why.** Integration's rule is one after the other (integration design §9); an overlap is evidence, and no route reads a model's
judgement (D192 point 1). **Rejects:** merging on overlap (both); comparing quests only; the head taking authority over its peers (X);
a second list on the Integration tab, integration's queue (§5.8; D150 point 1).

## 4. The one door

**Choice (O, with X's record): the head merges the others into its tree; then one Accept.** The ask lists the members the person ticks
(parts beside the head, related sessions of the repository), each with its branch and tip, and Daoris's fixed words, shown before the
press as the rebase words are (`work/review/LandingSync.tsx:52-55`): *merge `<branch>` at `<tip>`, then the next; settle each conflict
by what its session meant; commit; say when done.* The press records an operation in the desktop store (Workplace's family, §5.6)
binding the head and each member at its tip, then says the words through D137's say door.

**It refuses**, said where pressed (R3): another repository's session (a quest is the way); a teammate's or another machine's; a member
with a live part, a start waiting, uncommitted work, a tip not read, a landing made or an integration request open; a head that takes no
words, runs elsewhere or lost its tree.

**While it runs**, R2's phase line stands on the head's page and each member's: *asked*, *merging* (the head's turn,
live, timed), *checked*, *ready to accept*. At *checked* Daoris reads in the head's tree whether each member's tip is an ancestor of
`HEAD`, never the agent's claim; one not contained fails its phase, named, with *Ask again*. Each member's own Accept holds, as
ACCEPTALL1h holds a part (`DiffPane.tsx:83-84`), and the head's Accept names each member and tip it carries. A head the walk has
passed, or a member whose tip moved before *checked*, ends the operation *stale*, holding nothing (X). *Let them go* releases the holds
before the head's Accept; what the head merged stays, and its plan says so. A member's commits past its recorded tip are its own Accept's.
Once the head's Accept lands, each member reads *brought into* that landing, never *landed* (X); its tree goes by SQUASHTIDY1b's
discard once the line holds its work (`docs/decisions/D102.md:304-310`). Where the rule carries `integrate`, INTEGRATE1's step takes
the one landing after (§17.4).

**Why.** One review and one Accept; the head holds the shared records' context and changes only its own repository, through doors
already built. ACCEPTCONFLICT1 left this door to LINESTOGETHER1 (`docs/decisions/D167.md:447-448`). Integration deferred a landing
agent because a new session needs a record, authority and a re-review (§5.4); the head is an existing session, the press attributed,
the one Accept the re-review. **Rejects:** X's door through INTEGRATE1: a unit not yet landed is requested only through its own
landing (D215 point 4), so three sessions need three Accepts or a new Accept across sessions; its rows n, g, h and i are unbuilt
(`TASKS.md:28`); it hands a conflict of meaning to each member apart (§5.4); and it lands three branches where the session lands one.
Also Daoris merging for the head (a conflict of meaning is a session's); acceptance inside the door (X).

## 5. What the person presses

| Press, where | The steward |
|---|---|
| Page header: *Stop*, *Hand over…*, *Try again* | yes, on a quest's session, in D209 point 3's forms |
| Waits: a park's *Finish…*; an agent's ask to hand over | yes, where D209 point 3 allows |
| Waits: *Decline…*, a park's answer, go-aheads, *Allow…*, a permission's or a helper's answer | no: the owner's word, free words or a widening |
| *Accept…*, *Leave part n out*, the review's and the second opinion's verdicts | no: acceptance |
| *Discard tree*, *Discard branch…* | no: deletion |
| *Ask to rebase*; *Bring them together…*, *Ask again*, *Let them go* | yes, fixed forms (LINESTOGETHER1e; *Let them go* its own operation only) |
| Box: *Send*, *Send back…*, *Say it to the head instead* | no: free words |
| Integration: *Drop*, *Check again*, *split*, *hand back*, *order*, *request* of a records branch | yes (integration design §8); *land* no |
| Folds, doors, *To the head*, *To the wait*, a part's press | the viewer's own |

With a steward appointed, Lyntai's case needs one press of the person's, *Accept…*; without one, *Bring them together…* first.
**Rejects:** an appointment as blanket approval; a steward's act recorded as the person's (X).

## 6. The rows

Smallest first, each proof seen failing first; the first three can start on acceptance.

- **REVIEWFLOW1a — the session's Accept is its work to land** (web). Dated shots of today's page first; then §1's placement and
  *Leave part n out*. Proof: `WaitStrip` and `DiffPane` stories at the frame's widths, en and zh, both themes; a `WorkFrame` test: one
  *Accept…* on screen, a press at the foot disabling the head's and the review's, no part's beside it; an `EntryAccept` test landing
  one part, the others left out; shots at 1546 and 680 px.
- **REVIEWFLOW1b — *now · next*** (web). The facts line's lead fact and its door. Proof: a story per phase; `headFacts` tests from the
  frame's facts, an unread fact unsaid, en and zh.
- **LINESTOGETHER1a — the head** (service, driver, web). `head` answered and carried beside `leading`; the row opening on it; the Parts
  strip marking the head and the parts beside it; the glossary's *head*. Proof: a service test that the answer equals `Lines.HeadOf`
  for a chat of three parts, a driven part gone on as a chat, a quest's retry, a dispatched quest beside it (its own head, never the
  session's) and a planted cycle (none); a page test opening on the head, and on a part waiting on the person.
- **LINESTOGETHER1b — where a word goes** (web). §2's box line and two presses. Proof: page tests per reach, en and zh; a held word
  moved once; one taken in between answered *taken*, nothing said; a refused say left in the head's box.
- **LINESTOGETHER1c — related sessions** (driver, web). §3's read and line. Proof: a driver test on a git fixture of three branches (a
  chat's tree, a quest beside it, another conversation's): declared and changed paths told apart, an unchanged tree not read again, an
  unreadable one *not read*; page tests, en and zh.
- **LINESTOGETHER1d — *Bring them together…*** (driver, modules, web). §4 whole. Proof: a driver test on a git fixture of three
  branches (the head, a part beside it, another session), clean and conflicting; a tip moved before *checked*, *stale*; a restart
  mid-merge resuming its phase; each refusal said where pressed; the install's case in a story.
- **LINESTOGETHER1e — the steward's fixed forms** (modules, driver; after STEWARDROLE1d, `TASKS.md:84`; amends D209 point 3 by its
  note). `ROLE_ACT` forms for the rebase words and §4's door. Proof: `ROLE_ACT` tests refusing another repository, a landing that
  pushes, an owner-held session and the owner's operation; the record naming the steward and its generation; a revoke cancelling an
  operation not yet said.
- **REVIEWFLOW1c — the installed window.** Lyntai's case (a six-part chat, its quest, a conversation's filing): opened on the head, a
  held word moved, brought together, one *Accept…* from the foot; dated shots at 1546 and 680 px, en and zh, both themes.
