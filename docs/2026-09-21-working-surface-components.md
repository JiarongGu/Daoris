# The working surface — the component plan

> Written 2026-09-21, from the owner's direction the same day: **this is a large UI/UX as a whole, so
> develop it component by component — closer to an atomic design pattern — so each part can be tested
> one by one.** The contract is `docs/2026-09-21-working-surface-design.md` (D51/D52) and nothing here
> amends it: this is *how it gets built*, not *what it is*. The design language stays D41's and the
> tooling stays D42's — no second visual language, no second stack.

## 1. Why the method changes here, and only here

Every view in the platform was built whole and is tested whole: `shell.test.tsx` drives Projects,
Quests and Settings end to end through a mocked bridge, and that is the right shape for them. Each is
a list, a drawer and a form — one sitting to write, one file to review.

The Work view is not that. It is a rail, a head, a live stream, a timeline, a composer, an attention
band and a diff pane, several of them live at once (design §3–§5). Built the same way it would be one
six-hundred-line file where **the first thing that renders is the last thing** — nothing is reviewable
until everything works, nothing is testable in isolation, and a state that real data rarely shows
(nine session states, a parked session, a 4,000-line diff) is only reachable by arranging the world
that produces it.

So the surface is built **upward from parts that each pass their own loop**, and the backlog items
below are cut along that line.

## 2. The layers — a dependency rule, not a folder chart

| Layer | Examples | May import | What proves it |
|---|---|---|---|
| **tokens** | `tokens.css` | — | D41's computed palette; unchanged by this work |
| **atoms** | `Button`, `Pill`, `Dot`, `MonoWell` | tokens, `cn` | a story per state · `ui.test.tsx` |
| **molecules** | `SessionRow`, `SessionHead`, `TimelineEntry`, `Composer` | atoms, i18n, `format.ts` | props-only vitest · a story per state |
| **organisms** | `SessionRail`, `AttendedSession`, `DiffPane` | molecules + hooks (`queries.ts`, `shell.ts` and the `bridge/` domains it re-exports) | vitest over the mocked bridge |
| **page** | `WorkView` | organisms | the existing `shell.test.tsx` shape |
| **shell** | `App.tsx` nav, the badges | pages | Playwright (the record half) · `npm run desktop` (the real window) |

**The rule that makes one-at-a-time possible: a molecule imports no hook.** Data arrives as props, so
every state is reachable by passing it — a parked session, a session with no quest, a 300-character
CJK title, a stream that dropped 12,000 lines. That is the whole trick, and it is **checkable**: a
test asserts that no file in the presentational set imports `./queries` or `./shell`. Same shape as
the gate-list and catalogue checks already here — a rule nobody can quietly stop following.

**A door deep in a molecule is a context the frame provides** (BRW7's link opener in `links.tsx`,
PREVIEW1's file opener in `work/preview.ts`): the molecule reads it with React's own `useContext`,
which reaches no data, and rendered with no provider the door is plain text. Threading the callback
instead would make every level between the frame and a tool card carry a prop it does not use.

**Deliberately not adopted: the folder taxonomy.** `atoms/ molecules/ organisms/` scatters one feature
across three directories and starts a taxonomy argument on every file ("is a composer a molecule?").
The surface's own components live together in **`src/Daoris.Web/src/work/`**, colocated with their
stories and tests; shared atoms stay in `ui.tsx`, where the other four views already find them. The
layer is the dependency rule above, not the directory. This is a reversible call — if the folders
later earn their keep, moving files is mechanical; unpicking a hook out of twelve molecules is not.

## 3. The three loops each part passes, in order

1. **A story first.** Storybook is where states are designed, reviewed and kept (D42 §5), and a story
   for a component that does not exist yet is the cheapest possible failing test. Every state the
   design names gets one — including the ones real data rarely shows. **An empty machine is one of
   them, and the fixture never shows it:** the example family always holds a repository, so a
   surface that reads the registry meets "none" in no story, test or rehearsal unless one is written
   for it. POLISH2 found a blank Projects and two composers that could never send that way.
2. **Vitest.** Props for a molecule; the mocked bridge for an organism. This is the inner loop that
   owns every shell-attached surface, because a browser structurally cannot reach them
   (`docs/2026-09-19-frontend-architecture.md` §4).
3. **The real window.** Once a region is assembled: `npm run desktop -- run`, then `eval` to ask the
   real shell what it rendered and `shot` to look at it (DEV1). This is the loop that did not exist
   when the platform's other views were built, and it is the one that sees the bridge-attached half.

Plus one cheap multiplier: **every story is also a smoke test.** `composeStories` renders each story
inside the existing vitest run, so a story that throws fails `npm run test:web` — no new gate row, in
a repository where the declared gates are a list two places must agree on. (`@storybook/react` is
already in the tree as the framework's own dependency; SURF4a makes it explicit.)

## 3a. The reference console: deepseek-harness (owner, 2026-09-21)

Set by the owner mid-arc: **take the UI/UX structure and design of
[deepseek-harness](https://github.com/deepseek-ai/deepseek-harness) (MIT) for the working surface;
our base stays claude/codex.** Read before SURF4b–d: it is the one field console whose *structure* we
adopt rather than merely survey, because it is a working, shipped answer to exactly this product — a
local web console holding live agent sessions — and its licence permits taking it whole.

**What is adopted — structure and geometry, not code and not identity:**

- **The three-column frame.** Left rail (sessions) · center (the attended session, with a protected
  minimum width) · **right dock** (per-session surfaces). Their field-tested geometry becomes our
  defaults: sidebar 264–420px (280 default) collapsing to a 56px rail, auto-collapse under 1024px;
  center floor 400px; the right panel opens at 45% of the viewport, keeps the person's pixel
  preference, caps at 70%, compresses to a 300px floor and then **asks its occupant to close rather
  than squeezing the center further**. Deterministic close: widening the window never auto-reopens a
  panel the person closed.
- **One docking surface per session.** The right dock belongs to the *attended* session and its tabs
  are keyed per session — which is where our **Diff** (SURF6) and **Timeline** (SURF4c) live, leaving
  the center to the stream and composer. Fullscreen presentation shares the content tree (switching
  never remounts a tab), and below 768px the dock opens fullscreen automatically.

**Built (FRAME6, 2026-09-26).** The numbers above are `work/layout.ts`, one pure function from the
window's width and the person's choices, and the frame only measures and renders. Four choices the
reference did not make for us:
- **A closed dock leaves a 32px strip of its tabs**, since nothing but the person may open it again
  and a closed panel needs a door back.
- **The rail's 56px strip is each running session's initial and its mark**, with the title, the
  repository and the state as its name and tip. The strip has no room for the words, and a mark is
  never hue alone (D41 §6).
- **A strip the window drew offers no "open"**: only widening undoes it, so the button would do
  nothing. *Amended by D118 (FRAME1a; built as FRAME1b, on `ListPane`):* a strip the window drew opens its
  list over the main area. The rail becomes a strip when the main area has no room beside it, rather than below
  1024 px. Without an open, below 1024 px the rail left an ended session and its search out of reach, and
  a view whose list is its only way in would leave the whole view out of reach
  (`2026-10-01-frame-model-design.md` §3a).
- **Each session's dock tab is kept in memory, not across launches**, as the one dock tab was.
- **Widths and closings are remembered** per viewer, like the panel's height; fullscreen is not.

~~The dock opens beside the session at 45%, as adopted, and ours is open by default where the
reference's opens on demand. On a 1400px window that leaves the conversation 442px. UX5 carries the
question.~~ **Answered by UX5 (D76 as amended): the dock opens on demand**, as the reference's does.
A viewer who never chose sees its strip, and it opens at 45% when a tab, a row's review or the
palette asks, remembered either way.
- **Trajectory beside conversation, never inside it.** Their `ui-trajectory`/`ui-chat` split is our
  timeline-beside-stream decision, independently arrived at — adopted with two of its rules: the view
  **follows the tail until the person scrolls up**, and an in-flight record **shows a start marker
  without inventing elapsed time**.
- **Approvals and questions as first-class cards** (`ui-approval`, `ui-user-questions`) — the shape
  SURF5's `AwaitingPerson` surface takes: the analysis renders as a card with the person's moves on
  it, in the flow, not in a modal.
- **The package boundaries validate the component cut.** Their `ui-session`, `ui-conversation`,
  `ui-trajectory`, `ui-approval`, `ui-sidebar-*` map one-to-one onto our rail / attended session /
  timeline / attention / dock organisms — independent confirmation that §4's inventory is cut along
  real seams.
- **The session row's anatomy, and its one priority rule.** Their row is 34px: status dot, title,
  relative time, actions menu — ours adds the kind, per D52's derived identity — and the dot obeys
  **"pending user interaction outranks own activity"**: a session that needs the person wears the
  attention state even while its process is busy. That is SURF5's rule stated as a pixel, and
  `SessionRow` adopts it outright.

A checkout is available locally for the SURF4 build sessions — read-only, like any repository that is
not this one; its location is machine wiring and lives in the untracked `local/` notes, not here.

**What is deliberately not adopted:**

- **The plugin runtime (Cordis).** Their everything-is-a-plugin architecture is what makes their UI a
  set of slot-registered packages; our components plan is the *static* equivalent of that
  modularity, and a runtime plugin system is an architecture Daoris has not chosen and does not need
  for one surface. The owner's framing holds here: their predefined plugins show which surfaces
  matter, and **we build those surfaces natively against claude/codex through the adapter seam**
  (D23) instead of generically against a provider registry.
- **Their visual identity.** D41's language — the paper character, the tokens, the validated status
  palette — does not move. Structure and geometry transfer; pixels do not.
- **Model-provider settings** (D24: no model named — the harness owns the model) and their workspace
  picker (the registry and workspaces already exist here, D48).

**Licence.** Structure and geometry are design learning and carry no notice obligation; **if any of
their code is ever ported, the MIT notice lands in the same commit** (a third-party-notices file,
which this repository does not yet have and gains at that moment, not before).

## 4. The inventory

Everything the design (§3–§5) needs, with the states its story must carry. Nothing here is speculative
— each row cites the thing that asks for it.

### Atoms — added to `ui.tsx`

| Atom | What | Story states |
|---|---|---|
| `Dot` | live / attention indicator beside an identity | live, parked, ended, idle — **always beside a label** (D41 §6: never hue alone) |
| `MonoWell` | verbatim monospace region with a "what fell out" footer | empty, short, scrolled-to-bottom, 500 lines with 12k dropped |
| `MetaLine` | the label · value pairs of a session head | one pair, six pairs, a missing value, a long path |
| `StripMark` | one item on a list closed to its strip: an initial and its mark, named in words (FRAME1b, D118); a `face` in the initial's place, a plugin's icon (PLUGUI2, D140) | working · chosen · waiting · idle · unmarked · off · a 中文 name |
| `Menu` | every dropdown menu, as `SelectField` is every select: its content capped at the room on its side and scrolled inside, one row density with the bars' raised highlight or the lists' accent one, the tick's column, a label and a rule (MENU1). `primitives.test.ts` holds that no file but the atoms imports a Radix primitive. `Menu.Acts` draws a list of `MenuAct`s, a glyph column then each name, which a surface's ⋯ and its right-click both draw (CTX1, D138) | a menu of sixty rows (`LongMenu`) · ticked, reserved, no tick column · a checkbox and a radio item |
| `QuickPanel` | a box at the palette's place, between the frame's bars: the command palette and Quick Ask (MENU1) | held by `ui.test.tsx` and the two boxes' own tests |
| `CodeText` | a command or a name set as code, breaking only between its words, one box per word (LOOK5): every span `Inline` sets, and a command a screen sets itself. A test finds a span of several words by `test/code.ts`, since its words are no longer its own text | `CodeSpans`: three widths, 中文, a word wider than its line |

`Pill`, `Button`, `Chip`, `Card`, `EmptyState`, `SkeletonRows`, `Tip` are reused unchanged.

### Pure helpers — `format.ts` / `work/identity.ts`

| Helper | What | Why it is not inline |
|---|---|---|
| `SESSION_TONE` | session state → pill tone, **exhaustive at compile time** | the twin of `QUEST_TONE`, and the reason that one exists: nine states, and a tenth cannot ship half-toned. ~~Today a session pill is `live ? taken : neutral` — eight states wearing two tones~~ — **wrong when written** (SURF4a): `QuestsView.tsx` already held the exhaustive map. The work was to **move** it, and the reason to is the better one: the rail, the head and the quest card are three readers, and three copies of a nine-row map disagree eventually |
| `SESSION_DOT` | session state → liveness mark (SURF4b) | the pill says *which* state, the dot says *whether anything is happening*. It is where the reference console's one priority rule lives — and where it costs nothing, because `awaiting-person` is a state and has no busier state to lose to. No "is a process alive" input: the driver's running list is one machine's |
| `SESSION_ACTIVE` | the four states that still hold a repository (SURF4b) | `Session.Active`'s wire half. `QuestsView` held it privately; the rail asks it too, and a set spelled out twice acquires a tenth state in one of them |
| `sessionTitle(session, quest)` | the derived identity: repository · kind · what it is for | design §3 — identity is derived and never invented; one implementation, or the rail and the head disagree |
| `sessionOrigin(session)` | which machine holds it, or null for this deployment's own (SURF4b) | D55 asks a row for *where it runs* and says it is already in the record: the feed keys a mirrored record by `origin/id` (D47 §6). Reading that is a derivation, not a component's business |
| `treeName(tree)` | a session tree's last segment (SURF4b) | Daoris owns where trees live (D51 §2), so the last segment is the branch — meaningful rather than a guess. The rail has no room for the path, and the path is machine-local besides |
| `elapsed(from, to?)` | a span, where `ago` is a point (SURF4b) | D55's second added fact. A start in this machine's future reads as brand new: records travel between machines and clocks do not |
| `needsAPerson(sessions, quests, registry, asks, …)` | what is waiting, parked first (SURF5); asks waiting on a person since INT4d; a quest parked on its failed sessions, from the tick's verdicts, since SESSUX1i | one derivation for the band and Overview's count — two answers to "how many need me" disagree the first time either is edited |
| `sessionTimeline(session, quest?)` | the observed events, oldest first (SURF4c) | the record carries no event log, so what a timeline may honestly say is a derivation — and one place to change when the record grows one |
| `readEvidence(evidence)` | the driver's sentence, and the commits inside it (SURF4c) | reads the bundle's **shape**, never its words: no English literal is matched, so a reworded header is harmless rather than a silently empty list |
| `work/groups.ts` | Sessions' list by state as the driver's one reader answers it (`SESSION_GROUPS`), by repository as it was, a long *Ended* group's cut, the strip's order, a row's shown word, and the list's filters as kept (SESSUX1c, D126 §2.1, §4); what *Archive what ended…* would take, and what stays because it needs the person (`endedToArchive`, SESSUX1e) | the list, the strip and the rows read one answer; a record the reader has not answered for yet is placed by its record alone, never by a second copy of the reader's rule |
| `work/acts.ts` | which acts a session is offered on its row and in its page header, in §3.1's order (`offeredActs`), each act's name and glyph (`ACT_LOOK`), the header's loud act (`primaryAct`), the folder it worked in (`folderOf`), and what its stop says by what it is (`stopAsk`) (SESSUX1d, D126 §3.1–§3.5); *Delete…* where the reader says `deletable` (SESSUX1f, §5.4) | both doors read one rule, so a row and a header never offer a session different acts; the molecules read it, and `sessionActs.ts` carries each act out |
| `listPanes.ts` | each view's list memory: its closing, its width, its chosen item, its filters, keeping Sessions' and Settings' keys (FRAME1c, D118 §3f); and the monitor window's rail's, apart from the main window's (FRAME1h) | per view, beside `closings.ts`, which keeps what the frame remembers for every view, and a detached session's console for every detached window (`useDetachedPanel`, FRAME1h) |
| `opener.ts` | the opener's plan: a view, the item its list chooses, and the form a door opens (FRAME1c, D118 §3i); since FRAME1d a record is only the chosen item, which the view's main area shows; since FRAME1e the code map a door opens the Map on, which is no chosen item | one opener for every door, planned as a value so each door's effect is an assertion |
| `plugins/catalog.ts` | a plugin's state on today's answers, the catalogue's sections in the person's reading order (PLUGUI2, D140 §2), a source's word and whether an update waits, and what the chosen item names (`offer:<id>` for an offer) (PLUGUI1b, D119 §3.1) | the list, the strip and the page read one answer |
| `plugins/icon.ts` | a plugin's monogram (its name's first character, its id's hue by FNV-1a) and which declared icon the page may draw (only an SVG's or a PNG's bytes) (PLUGUI2, D140 §3) | the list, the strip and both pages draw one face per plugin; `icon.test.ts` pins the hash and computes the hues from `tokens.css` |
| `asks/workTree.ts` | an ask's work as a tree (each quest, its sessions, and the questions they published beneath it) and why each quest sits, from `WORK_PLAN` and the tick (PAUSE1h, D132 §7.1) | the page lists what a pause or an abandon reaches by one rule; held by `workTree.test.ts` |
| `quests/records.ts` | Quests' filters as kept (`{ to, closed }`), its groups by state, the freshest attempt per quest, the record a page shows (the list's copy or the door's last answer), and the question a quest waits on (FRAME1d, D118 §2, §3f) | the list, the page and the memory read one answer |
| `menus/press.ts` | what a right-click is on (a field, an open menu, selected words, a link, a code span or path), what the nearest surface offered (`contextOffer`, spread on a surface's root), and the groups the menu draws, most specific first (CTX1, D138, `docs/2026-10-03-context-menu-design.md` §3) | one rule for every surface, so a surface only says its acts; held by `press.test.ts` |
| `knowledge/records.ts` | Search's and Convergence's filters as kept (`{ localOnly }`, `{ threshold }`), the similarity's slider and its floor, a finding's name (its entries, since the service names none) and title, the tier an answer was made by, and an entry as a page reads it (read, on its way, gone, or failed) (FRAME1f, D118 §2, §3f) | the lists, the pages and the memory read one answer |

`ago`, `sittingDays` and `sessionTool` already exist and are reused.

### Molecules — `src/work/`

| Molecule | What | Story states |
|---|---|---|
| `SessionRow` | one session in the rail: dot, identity, state, **where it runs**, **elapsed**, age (D55); a conversation named by its first line, and a menu of its own window, its review and its id (RAIL1); the reader's word and its line's facts, and its repository first on its line by state (SESSUX1c); *Archive* on an ended row's menu, *Unarchive* on an archived one's, and *archived* on its line where no heading says it (SESSUX1e); since SESSUX1d its menu draws the acts it is handed (`acts`, by `offeredActs`) and reports each (`onAct`), and a stop that holds its quest says so on its line | all nine states · driven vs chat · no quest · long + CJK title · selected · another machine · **its own tree** · running three hours · parked · awaiting reply · to review · uncommitted to review · queued · archivable · archived · running with its acts · waiting with its acts · parked with *Try again* · stopped holding its quest · a deletable conversation |
| `SessionList`, `SessionStrip`, `ArchiveEndedAsk` | Sessions' list by state (*Waiting on you*, *To review*, *Working*, *Resumes later*, *Ended*, and *Archived* where shown, saying when nothing is) or by repository, *Show N more* for a long *Ended*; its strip, what waits on the person first (SESSUX1c, D126 §2.1, §2.5, §4); and *Archive what ended…*'s first press, holding what it listed for its second (SESSUX1e, §5.3) | by state · by repository · one group · a long Ended · archived shown · archived empty · archived by repository · archive what ended · nothing to archive · empty · loading · 中文 · no reader · the strip · laid over |
| `RepositoryGroup` | the rail's group header | drivable, held, busy, not adopted, no root |
| `SessionHead` | the attended session's record: state, repo, tree, quest, tool + account, age; under a page header (`headed`, SESSUX1d) its state and id are the header's | driven, chat, parked-with-analysis, ended, `--real` tree vs session tree |
| `SessionPageHead`, `StopAsk`, `DeleteAsk` | Sessions' page header (SESSUX1d, D126 §3.2), pinned: the title on one line, the shown word, the id, the loud act, *Stop…* while live and ⋯ with the rest, its acts on their own line below 560 px; a stop's ask under it, saying what follows by what the session is (§3.3); and a delete's, saying what goes and that nothing brings it back (SESSUX1f, §5.4) | running · waiting on you · parked · stopped holding its quest · to review · a teammate's record · each stop's ask (holding its quest, before the take, waiting on you, a chat, an intake) · a deletable conversation · its delete's ask · narrow · 中文 |
| `TimelineEntry` | one observed event: state change, quest transition, commit landed | each kind · a long commit subject · an entry with no note |
| `SessionTimeline` | the observed layer over `sessionTimeline()` — props-only (SURF4c) | just queued · parked · whole · nothing landed |
| `Note` | a session's note from its parts, in the reader's language: Daoris's lines from `note.json`, someone's words quoted as written, an unknown code or an old note marked *shown as recorded*; compact for a row (LANG1b, D142) | coded en · coded 中文 · a park's lead-in and the agent's question · unknown code · old note · compact |
| `HandedAccount` | beneath a driven session's target, what it was handed, section by section, from the account its event keeps: folded to its size and sections; each section's size, source and what its bound left out; the rules handed beside it; what was not handed and why; a target from before saying not kept; a newer driver's code marked *shown as recorded* (CONTEXT1, D143) | folded · a claim open · 中文 · dark · every bound cut · 中文 · dark · words unread · carries nothing · not kept · 中文 · a newer driver · narrow |
| `Composer` | the chat input with its two endings (the stop only where it is handed one: Ask Daoris's panel, since Sessions' is its page header's, SESSUX1d), the turn's own stop (CONV4b), the files a message carries (CONV4c), `@` a file in the tree (CONV4d) and the context ring (CONV5) | idle, sending, session ended mid-typing, refused, turn running (queued messages, stop turn), stopping the turn, turn running on a text door, files attached, mentioning a file, with context |
| `MentionList` | the files an `@` could mean, over the composer's box, which keeps the focus (CONV4d) | offering · second chosen · listing · no match · empty tree · unlisted (the host's sentence) · bounded · long + CJK names |
| `ContextRing` | how full the session's context is, under the composer, with the high-water mark in its tip (CONV5) | measured · under one percent · filling (warn) · after compacting · not reported yet · a text door · door unknown |
| `AttentionRow` | one row of Overview's *what needs you* band | parked · a quest parked on its failed sessions (SESSUX1i) · quest nobody can take · a proposed ask · an ask whose intake asked · no door · no detail · long + CJK. ~~finished-unreviewed~~ — **not buildable before SURF6**: nothing records that anybody looked |
| `AwaitingPerson` | a parked session's analysis and the person's moves (SURF5): *Finish* and *Decline…*, and *Answer and carry on…* where no process is left; its stop is the page header's (SESSUX1d) | parked · nothing said · a move in flight · declining |
| `AwaitingIntake` | a parked intake's question and a door to its ask (INT4g); its stop, and the sentence that the ask stays a proposal, are the page header's (SESSUX1d) | asking · nothing can act |
| `RunningIntake` | a running intake: why it takes no messages, and a door to look at its ask (INT4h); its stop is the page header's (SESSUX1d) | running · nothing can act |
| `TrustAsk` | the agent's trust question for a folder the driver holds: the folder, what trusting means, what it holds, the one file written, and the grant on the press (D73) | asking · granting |
| `DiffFileRow` | one file in the review pane, and its door into the file's preview (PREVIEW1) | added, modified, deleted, renamed, binary, truncated, with its preview, deleted with no preview |
| `PatchView` | a file's patch: numbered, highlighted a side at a time, unified or side by side (REVIEW2) | unified · side by side · unknown language · rename only · long line |
| `RightDock` | the per-session surfaces beside the session, with its geometry handed in (FRAME6), and a file's preview as a tab after them (PREVIEW1, D111) | docked · cramped · full · full in a narrow window · closed · with a preview · a preview at the floor · closed with a preview |
| `FilePreview` | one file, read-only, from the session's tree: its path, numbered and highlighted lines, the lines a read named, and the review's patch for it where the review has one (PREVIEW1, D111) | read · marked lines · with its changes · bounded · binary · empty · refused · reading · a 中文 path at the floor |
| `SessionStripRow` | one running session in the rail's 56px strip (FRAME6), a `StripMark` since FRAME1b | working · attended · awaiting person · a 中文 repository |
| `Splitter` | the edge a column is resized by, keyboard-operable (FRAME6) | held by `chrome.test.tsx` |
| `ListPane` | a view's list pane: its header, the list and its keys, its edge, its strip, and the list laid over the main area (FRAME1b, D118); its ⋯ is `ListMore`, and an empty list offers each of a two-kind `＋` by name (PLUGUI1b); named by its list as a landmark, `ListMore` holding a list's filters as ticked items, and `ListGroup` and `ListRowDoor`, a group of rows and a row's door, for every list (FRAME1d); a rule setting an act apart from the filters above it (SESSUX1e) | open · closed · strip by the window · laid over · at its least and its most · a `＋` of two kinds · 中文 · empty · empty with two kinds · with its ⋯ · with its filters · loading |
| `QuestList` | `src/quests/`: Quests' list, the asks (`AskRow`) then the quests by state, a held repository's resume beside its row's door, the receiver filter said at its head (FRAME1d, D118 §2) | asks then quests · an ask chosen · closed included · filtered · held with its resume · awaiting your yes (DRIFT1d2) · only asks · empty · empty for a receiver · loading · an error with no answer · 中文 · the strip · laid over |
| `QuestPage`, `QuestsMainNotice` | `src/quests/`: a quest's page in the main area, its acts in its header, *Decline…* and *Delete…* asking under it; and the main area with no record (FRAME1d, D118 §3d); *Try again* for a quest parked by its strikes or held by the person's stop, and its session's record with its door into Sessions and no stop of its own (SESSUX1d, D126 §3.6); what the person required and how its done answered, and *Accept the departure* on a held one (DRIFT1d2, D133 §4) | open · taken · sitting · held for trust · parked by strikes · held by a stop · waiting on a question · a conflict · lanes · a chain · its session · in a browser · declined · done · deletable · 中文 · its requirements · requirements met · held for your yes · accepting · accepted · held in 中文 · nothing chosen · gone · an ask gone · loading |
| `RequirementItem`, `QuestRequirements` | `src/quests/Requirements.tsx`: one requirement, its number, the person's words quoted, its check and how the done answered it, met or departed with the reason and the words it relied on; and a quest's whole list of them, a held one's sentence and its yes (DRIFT1d2, D133 §3–§4). Ask Daoris's accept card draws its departures with `RequirementItem` | held by `QuestPage.requirements.test.tsx` and `ProposalCard.test.tsx`, drawn in `QuestPage`'s stories |
| `QuestComposer` | `src/quests/`: the quest composer, a form, so a drawer still (D118 §3d) | held by `QuestsView.test.tsx` |
| `AskRow`, `AskPage` | `src/asks/`: an ask as a row of Quests' list, and its page in the main area with *Close ask* and *Delete…* in its header (INT4c; FRAME1d, where they were a card and a drawer) | every state a machine reaches · with its intake · in a browser · 中文 |
| `AskWork` | `src/asks/`: an ask's work on its page, from `workTree`: each quest with its state, route and why it sits, its sessions as doors into Sessions, and the questions they asked beneath it, listed the same way (PAUSE1h, D132 §7.1) | drawn in `AskPage`'s stories: listed · paused · with no doors |
| `ProjectList`, `RepositoryMarks` | `src/projects/`: Repositories' list, the adopted then *Registered, not adopted*, a row the repository and its one line, its standing on this machine in the session list's words (FRAME1e, D118 §2) | adopted then registered · nothing chosen · one not adopted chosen · only adopted · in a browser · empty · loading · an error with no answer · 中文 · the strip · laid over |
| `ProjectPage`, `ProjectsMainNotice` | `src/projects/`: a repository's page in the main area, what its card said, with *Open code map* and *Manage* in its header; and the main area with no page (FRAME1e, D118 §2) | adopted · driven · held · a line · unlanded branches · no root here · undeclared · not adopted · on a direct door · not adopted with no root · in a browser · 中文 · nothing chosen · gone · loading · an error with no answer |
| `HitList` | `src/knowledge/`: Search's list, the box, *each repository's own only* and the hits, ↓ from the box into them; a first search's skeleton rows and a newer one's last hits held, dimmed (FRAME1f, D118 §2; audit SR11) | hits · nothing typed · capped · words only · a first search · searching again · no matches by each tier · nothing answered · an error with no answer · 中文 · everything · the strip · laid over |
| `EntryPage`, `EntryMainNotice` | `src/knowledge/`: an entry's page in Search's main area, read as it is written, where it was the reader drawer; with `EntryText` and `EntryPills`, which a finding's page reads its entries with; and the main area with no entry (FRAME1f, D118 §3d) | a knowledge document · a canonical rule · long lines · 中文 · nothing chosen · gone · loading · an error with no answer |
| `FindingList` | `src/knowledge/`: Convergence's list, the similarity and its tier's note, then the findings, a row its entries' titles, its likeness and the repositories that reached it (FRAME1f, D118 §2) | findings · nothing chosen · capped · comparing · comparing again · nothing converges · at the floor · an error with no answer · semantic · 中文 · the strip · laid over |
| `FindingPage`, `FindingMainNotice` | `src/knowledge/`: a finding's page in Convergence's main area, the service's sentence and then each entry read whole; and the main area with no finding (FRAME1f, D118 §3d) | a restatement · convergent · identical · an entry loading · gone · failed · 中文 · nothing chosen · gone · loading · an error with no answer |
| `PluginList`, `PluginStrip` | `src/plugins/`: the Plugins view's list, a catalogue since PLUGUI2 (D140 §2): the installed, waiting on you first, then Daoris's own plugins, a row its icon, name, state, what it gives and its meta line; and its strip's marks, each plugin's icon (PLUGUI1b, D119 §3.1) | the catalogue · light · dark · a refused row · empty · loading · an error with no answer · 中文 · the strip · by the window · laid over |
| `PluginIcon` | `src/plugins/`: a plugin's icon at the strip's, a row's and a page's size, its own drawn as an image of its bytes or its monogram, faint for off (PLUGUI2, D140 §3) | light · dark · not drawn |
| `PluginPage`, `PluginMainNotice` | `src/plugins/`: a plugin's page on today's answers, its header's four acts and *Remove…* asking once; and the main area with no page (PLUGUI1b, D119 §3.2); its icon at its head, *update available*, and why its icon is not drawn, every block at the pane's width (PLUGUI2, D140 §2) | running · off · refused · landing · agents only · no source · an update's plan · a refused plan · a trial passed · failed · *Remove…* asking · icon and update, light · dark · a monogram in dark · icon not drawn · off with an icon · nothing chosen · gone · loading |
| `OfferPage` | `src/plugins/`: one of Daoris's own plugins not installed, with *Install* (PLUGUI1b, D119 §3.2), its icon at its head (PLUGUI2) | with needs · handing a server · with a problem · a monogram, light · an icon, dark |
| `ViewMain` | a view's main area: the scroll box that is the container named `main`, which its page's splits follow, its page header's slot, and its four states (FRAME1c, D118); with `PageHead` and `PageSection`, every page's header and sections, moved here from the plugin's page (FRAME1d), a header's id line absent where a name is its id (FRAME1e); `menu`, what a right-click on the page offers while it shows its record (CTX1); the page's one column, the one place a line's length is set, which caps no block and keeps no `measure` on `PageHead` (LAYOUT11, D141) | chosen · nothing chosen · loading · gone · narrowed · a view's own page · Sessions' gutters |
| `ContextMenu`, `ContextMenus` | `src/menus/`: the right-click menu, MENU1's at a point with its groups between rules; and the window's one handler, mounted once per window and handed the frame's doors, which leaves a text field the engine's menu, suppresses it where nothing is offered, and opens on the menu key and Shift+F10 (CTX1, D138) | a session's row · a quest's page · selected words · a link · a link in a browser · a code span · a link on a page · opened by its key · 中文 |
| `ViewFrame` | a browser's frame: the view's list and its main area, never the side bar or the panel; and `ViewListPane`, the one path by which a frame draws a view's `ListSpec` (FRAME1c, D118 §4); the monitor window's rail and tiles too (FRAME1h) | held by `ViewFrame.test.tsx` and `MonitorWindow.test.tsx` |

### Organisms — `src/work/`

| Organism | What | Held by |
|---|---|---|
| `SessionRail` | Sessions' list and its search: the records, the reader's groups (`useSessionGroups`), and what each row needs, handed to `SessionList`; by repository where no driver answers (SESSUX1c); each row's acts by `offeredActs`, pressed through `useSessionActs` and the frame's doors, *Archive what ended…*'s second press among them (SESSUX1e, SESSUX1d) | mocked-bridge vitest |
| `useSessionActs` (`work/sessionActs.ts`) | the one owner of each act on a session (SESSUX1d, D126 §3.1): *Try again*, *Open folder*, the window, the archive marks, the id, and the stop's and the delete's second presses by their routes (SESSUX1f); what only the frame can do (attend and answer, attend and ask to stop or to delete, review, a terminal there) through the frame's doors | mocked-bridge vitest (`sessionActs.test.tsx`) |
| `AttendedSession` | head + stream + timeline + composer, for one session | mocked-bridge vitest |
| ~~`SessionTimeline`~~ → **a molecule** (SURF4c) | the observed audit layer beside the stream | props-only vitest · a story per kind |
| `SessionConsole` | **exists** — promoted out of the drawer, otherwise unchanged | its current tests |
| `AttentionBand` | Overview's *what needs you* | the page suite over a stubbed service |
| `DiffPane` | the review surface, bounded and stating what it truncated | mocked-bridge vitest |
| `usePluginsView` (`src/plugins/PluginsView.tsx`) | the Plugins view as it hands the frame its list and its page (PLUGUI1b): a hook, since one view's two regions are drawn where the frame decides; held on every view, asking nothing until in front; and the kit's drawer | mocked-bridge vitest (`PluginsView.test.tsx`) |
| `useProjectsView` (`src/ProjectsView.tsx`) | Repositories as it hands the frame its list and its page (FRAME1e): held on every view, its errors said only in front, on queries the frame already holds; and the add, import and manage drawers | mocked-bridge vitest (`ProjectsView.test.tsx`) |
| `useSearchView` (`src/SearchView.tsx`), `useConvergenceView` (`src/ConvergenceView.tsx`) | Search and Convergence as each hands the frame its list and its page (FRAME1f): held on every view, asking the service nothing until in front, since a comparison over a real index takes seconds; each holds its answer and the entries its page reads | stubbed-service vitest (`SearchView.test.tsx`, `ConvergenceView.test.tsx`) |
| `useQuestsView` (`src/QuestsView.tsx`), `useAsksPart` (`src/asks/AsksPart.tsx`) | Quests as it hands the frame its list and its main area (FRAME1d): held on every view, its errors said only in front; the asks' half holds the asks' queries and acts, so the row, the page and the composer hold none; and the two composers' drawers | stubbed-service and mocked-bridge vitest (`QuestsView.test.tsx`, `.shell`, `.held`) |

**Two of these turned out not to be organisms** (SURF4c), and the dependency rule decided it rather
than this table. `sessionTimeline(session, quest)` needs only the record and its quest, both of
which the attended session already holds — so `SessionTimeline` takes props and stays inside the
presentational boundary. `AttendedSession` reaches no data either: it is handed its session, and
the one hook in the region belongs to `SessionConsole`, which already held it. A component's layer
is what it imports, not what the plan guessed before the derivation existed.

### Page and shell — **revised by D55**, then by **D66**

*D66 (2026-09-23): one frame. `ModeSwitch` is gone and Work is the **Sessions** view of the activity
bar; the rest of the table below stands.*

~~`WorkView` (the full-bleed layout, rail + attended), the sixth nav item with its two counts, and the
last-view memory.~~ Work is the **second frame**, not a sixth nav item
(`docs/2026-09-21-ide-reference-study.md` §4, answered B). The page-and-shell layer is therefore:

| Piece | What | Story / test states |
|---|---|---|
| `ModeSwitch` | *Manage* ⇄ *Work*, peers | either mode · Work absent (a browser) |
| `StatusBar` | ambient truth: driver, session count, workspace, remote | running · stopped · no shell · a remote wired |
| `OutputPanel` | the stream, **growable, shrinkable, hideable** — `MonoWell` inside a region, not a well inside a card; a detached session's console too, with no views menu where it holds one view and has nowhere to move it (FRAME1h) | collapsed · default · grown · no session attended · in its own window |
| `StartSession` | repository, harness, account, own tree — moved out of Projects (SURF4d) | every choice · one harness and no accounts · nothing to talk in |
| `WorkFrame` | rail + attended + panel + status bar; since FRAME1c it takes each view's `ViewLayout`, its list and its main area, and Sessions' rail is one such list | the assembled frame |

The remembered mode replaces the remembered view, and is a per-browser preference like the language
and the workspace scope — never machine wiring, never a tracked file.

## 5. The build order, cut along the layers

SURF4 becomes four session-sized items; SURF5 and SURF6 keep their numbers and their UI halves follow
the same method. Each item is TDD, gates green, archived on completion.

| Item | Lands | Proven by |
|---|---|---|
| **SURF4a** ✔ | the three atoms, `SESSION_TONE`, `sessionTitle`, the presentational-import check, stories-as-smoke-tests | stories + `ui.test.tsx`; the import check sabotage-tested |
| **SURF4b** ✔ | `SessionRow`, `RepositoryGroup`, and `SessionRail` over them | props-only vitest for the two molecules; mocked bridge for the rail |
| **SURF4c** ✔ | `SessionHead`, `TimelineEntry`, `SessionTimeline`, and the stream promoted into `AttendedSession` | props-only vitest + the bridge for the promoted stream; ~~the first `npm run desktop -- shot`~~ — **moved to 4d**: nothing mounts a region until the frame exists |
| **SURF4d** ✔ | `Composer`, `StartSession`, then the **frame** (D55): `WorkFrame`, `ModeSwitch`, `StatusBar`, `OutputPanel`, remembered mode | the page suite; Playwright asserting Work's **absence** in a browser; the arc's first real-window pass, which found two real bugs |

**Why this order.** Each item renders something a person can look at: 4a puts every state in
Storybook before a view exists, 4b makes the rail real, 4c makes one session attendable, 4d makes it a
view. Nothing waits on everything.

## 6. What does not move

- **D41's language** — tokens, the validated status palette, the motion budget, the accessibility
  rules. A new atom is an arrangement of existing tokens or it is a mistake.
- **D42's stack** — headless primitives, the tokens as the theme, Storybook wired to the shipped
  components. Stories import the product; a divergence is a build error, not a discovery.
- **The i18n parity gate** — every new string lands in both catalogues, keyed structurally. A
  component with an English literal in it fails the gate, which is the point.
- **A refusal is three things** — a code in `Refusals`, an entry in both catalogues, a throw site
  (REV2). Anything the shell refuses from this surface inherits all three.
- **The disclosure boundary** — the stream, the diff and a tree path ride the bridge and are absent in
  a browser (D47 §4). A component that renders one is a shell component, and Playwright asserts the
  absence rather than the presence.

## 7. Two things to settle when SURF4a starts — **both settled** (2026-09-21)

- **`composeStories` explicitly.** ~~It is present as the framework's own dependency; if making it a
  direct devDependency is more than a line, the stories-as-smoke-tests half is dropped rather than
  worked around — it is a multiplier, not a requirement.~~ **Kept, at zero cost:**
  `@storybook/react-vite` is already a direct devDependency and exports it. The roster is an
  `import.meta.glob` rather than a list, so a new `*.stories.tsx` is covered by the act of existing —
  and the suite asserts the glob matched something, because a glob that matches nothing passes every
  assertion under it.
- **Where the import check lives.** ~~Preferred: a vitest test in the web package…~~ **A vitest test
  in the web package** (`src/presentational.test.ts`), as preferred. `src/work/` is covered by
  default and an *organism* is exempted by name: a list of what may reach the data is one someone must
  justify appending to, where a list of what may not is one someone forgets to append to. Sabotaged
  twice — against a fabricated source, which proves the matcher, and against a real file dropped into
  `src/work/`, which proves the glob.
