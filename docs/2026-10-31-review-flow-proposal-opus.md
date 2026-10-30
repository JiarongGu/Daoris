# REVIEWFLOW1 and LINESTOGETHER1: the Opus maker's sealed proposal (2026-10-31)

**Standing:** a sealed proposal under D191, written from `2026-10-31-review-flow-brief.md` alone.

**Read** on `parent/batch-1031b`, under `src/Daoris.Web/src/`: `work/AttendedSession.tsx:309-311`; `work/WorkFrame.tsx:1903-1932`, `:2440-2700`; `work/review/EntryAccept.tsx`, `DiffPane.tsx`, `LandingSync.tsx`; `work/waits/WaitStrip.tsx`; `work/head/PartsFold.tsx`, `PartsStrip.tsx`, `relations.ts`, `SessionRelations.tsx`; `work/conversation/line.ts`; `work/say.ts`, `entries.ts`, `groups.ts`; `projects/integration/IntegrationTab.tsx`. The driver's `Workplace/EntryLands.cs`, `SessionEntries.cs`, `IntegrationLines.cs`; `WorkOrders/SessionEntryReader.cs`, `Work/WorkOverlaps.cs`, `Domain/PathOverlap.cs`, `Domain/Planner.cs:1343-1352`. D150, D167 and its 2026-10-14 amendment, D168, D209; the integration design §5.8, §8, §9, §17.4; the switch-context design §2.7; the UI contract's R2-R7; `translation-parity.md`; the glossary.

**Words.** The brief's *line* is the page's *session* (会话, D167). The page's *line* stays 主线. A session's *head* becomes a glossary term (en *head*, zh 续段: the part it goes on in).

**Three facts.**
1. The part's *Accept…* is a D168 wait: one owner, drawn at the head and in the foot strip (`WaitStrip.tsx:298`). *Accept all* stands only under *Parts* (`AttendedSession.tsx:311`). So an ended session's foot offers the part's press but never the session's, and its head offers both.
2. A row opens on its leading part, the newest of the first group any part is in (`SessionEntryReader.cs:53-54`). Where the head has ended and a dispatched part waits in *Resumes later*, the row opens on the part the cap holds.
3. INTEGRATE1l compares quests only (`WorkOverlaps.cs:81`) and tells the agents, never the page. A chat's head, holding the shared records, is never compared.

## 1. The flow of a session's page

**Choice: five steps, each led by one line saying where the work stands and what comes next, each with at most one press.**

| Step | Shows; where it stands | Its press |
|---|---|---|
| Head | a new *Now · next* line from the driver's facts (*working*, *waits on you*, *n parts to land*, *landing*, *integrating* by INTEGRATE1h's line, *landed*); *next* names the press and is a door to its step (WAITFOOT2b's *To the wait*) | *Stop* |
| Session | the *Parts*, chain, relations and channel folds, led by *Part 6 of 7 is the head · 1 beside it · 1 other session changes the same files* | *Bring them together…*, on the head only |
| Milestone | result, clean-up, recovery as facts (R4) | none |
| Conversation | the parts as one conversation (SWITCHCTX1c-i), each wait anchored | none |
| Foot | the strip, then the box, whose first line says which part takes a word and when | each wait's press; *Send* |

**Accept all lives at the foot of the changes.** It joins D168's table as the session's *work to land*. Wherever more than one part offers work, the session's press replaces the part's everywhere one is drawn (the foot strip, the head's wait, the Review view's foot in `DiffPane`), one owner in the frame, its ask open in one place at a time (D168 point 3). A part lands alone when the others are left out: the driver already leaves out any part named (`EntryLands.cs:128`), so the plan offers *Leave part n out* on every part with its own tree, except a clash (ACCEPTCONFLICT1). It stands where reading stops: under the conversation, or under the files.

**Why:** D168 answered "I have to scroll back to the top" for every other wait, but ACCEPTALL1 never joined its table; one press per session ends the two Accepts side by side at the head. **Rejects:** the block under *Parts*; a part's *Accept* beside the session's; a review page of its own (R5); the milestone drawn as a progression (R4).

## 2. A line and its parts

**Choice: the head is the newest part of the chain the first part began, the part holding the session's own tree.** A part that began *asked*, alongside in its own tree, is *beside* the head. In Lyntai, the chat's part 6 is the head, the dispatched quest is beside it, and the other conversation is another session.
- **The row opens on the head**, unless a part waits on the person; the reader answers `head` beside `leading`.
- **Before the person speaks**, the box's first line says where a word goes and when, from `SESSION_QUEUE`'s reach and the cap: *To part 6, the head · at its turn's end*, or *To this part, beside the head · after a running session here ends*, with *To the head*.
- **A word given to a held part stays with it**, in *Resumes later*. One press, *Say it to the head instead*, moves the words, which stay the person's.

**Why:** the leading rule answers *what needs you*, not *what goes on*; the person's words go where they said them (D137). **Rejects:** Daoris re-addressing the person's words; refusing words to a part beside the head; a head chosen by recency across trees.

## 3. Related sessions of one repository

**Choice: the driver compares every session of a repository with a tree here, chats included, and the Session step names the overlap** (*Beside it in Lyntai: 2 sessions change `TASKS.md` · …*), each a door to its head, declared paths told apart from changed ones. A session on another machine is *not read here* (D137). The Integration tab lists the same read, read-only.

**Who decides: the person**, by *Bring them together…* on the head's page. The head's agent never decides, though it may say so. **The steward**, once appointed, makes the same press in a fixed form (row g).

**Why:** INTEGRATE1 §9's rule is one after the other; an overlap is no verdict that two sessions are one, and no route reads a model's judgement (D192 point 1). **Rejects:** merging on overlap by itself; the head taking work other sessions own (D167 point 7); comparing quests only.

## 4. The one door

**Choice: the head merges the others into its tree, then one Accept.** The press freezes each other session at its tip, then says Daoris's fixed words through D137's say door, shown before the press as LANDSYNC1w's rebase words are: *merge `<branch>` at `<tip>`, then the next; settle each conflict by what its session meant; commit; say when done.*

**It refuses**, said where pressed (R3): another repository's session (a quest is the way); a session with a live part or a start waiting; uncommitted work; a session already landed, or with an integration request open; a head that takes no words, runs on another machine, or has lost its tree.

**While it runs**, R2's phase line stands on the head and each brought session: *asked*, *merging* (the head's turn, live, timed), *checked*, *ready to accept*. At *checked*, Daoris reads whether each tip is an ancestor of the head's `HEAD`, never the agent's claim; a tip not contained fails its phase, naming it, with *Ask again*. Each brought session's Accept holds meanwhile, as ACCEPTALL1h holds a part. Once the head's Accept lands, each reads *contained* (已包含) and offers its held discard (SQUASHTIDY1f).

**Why:** one review and one Accept; the head holds the shared records' context and changes only its own repository. **Rejects:** INTEGRATE1's step as the door (it needs three Accepts first and accepted gates; it follows the one Accept instead); Daoris merging in the head's tree itself (a conflict of meaning is a session's to settle).

## 5. What the person presses

| Press | The steward |
|---|---|
| *Stop*, *Hand over…*, *Try again* | yes (D209 point 3) |
| a park's *Finish…* / *Decline…* | where D209 allows / no |
| a park's answer, *Send*, *Send back…*, *Say it to the head instead* | no: free words |
| go-aheads, *Allow…*, a permission's answer | no: widening |
| *Accept…*, *Leave part n out*, a review's or opinion's verdict | no: acceptance (D209 §3.2) |
| *Discard tree*, *Discard branch…* | no: deletion |
| *Ask to rebase*, *Bring them together…* | yes, fixed forms (row g) |
| Integration: *Drop*, *Check again*, *split*, *hand back*, *order*, *request* | yes (integration design §8); *land* no |

## 6. The rows

a. **REVIEWFLOW1a, Accept at the foot** (web). Proof: `WaitStrip` and `DiffPane` stories at wide, half and narrow, en and zh, both themes; a `WorkFrame` test that a press at the foot disables the head's; shots at 1546 and 680 px.
b. **REVIEWFLOW1b, *Now · next*** (web, modules). Proof: a story per phase; page tests from the driver's facts.
c. **LINESTOGETHER1a, the head** (driver, web): `head` beside `leading`; the row opens on it; the glossary term. Proof: a reader test with parts carried on, asked and alongside.
d. **LINESTOGETHER1b, where a word goes** (driver, web). Proof: a driver test moving held words; a page test of a held part, en and zh.
e. **LINESTOGETHER1c, related sessions** (driver, web): `WorkOverlaps` over every session with a tree, read by the Session step and the Integration tab. Proof: a driver test on a git fixture of three branches.
f. **LINESTOGETHER1d, *Bring them together…*** (driver, modules, web). Proof: a driver test on a git fixture of three branches (the head, a part beside it, another session), clean and conflicting; the install's case in a story; the installed window.
g. **LINESTOGETHER1e, the steward's fixed forms** (after STEWARDROLE1d; amends D209 point 3). Proof: `ROLE_ACT` tests refusing another repository, a pushing landing and an owner-held session.
