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
| **organisms** | `SessionRail`, `AttendedSession`, `DiffPane` | molecules + hooks (`queries.ts`, `shell.ts`) | vitest over the mocked bridge |
| **page** | `WorkView` | organisms | the existing `shell.test.tsx` shape |
| **shell** | `App.tsx` nav, the badges | pages | Playwright (the record half) · `npm run desktop` (the real window) |

**The rule that makes one-at-a-time possible: a molecule imports no hook.** Data arrives as props, so
every state is reachable by passing it — a parked session, a session with no quest, a 300-character
CJK title, a stream that dropped 12,000 lines. That is the whole trick, and it is **checkable**: a
test asserts that no file in the presentational set imports `./queries` or `./shell`. Same shape as
the gate-list and catalogue checks already here — a rule nobody can quietly stop following.

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
  nothing.
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
| `needsAPerson(sessions, quests, registry, asks)` | what is waiting, parked first (SURF5); asks waiting on a person since INT4d | one derivation for the band and the Work switch's count — two answers to "how many need me" disagree the first time either is edited |
| `sessionTimeline(session, quest?)` | the observed events, oldest first (SURF4c) | the record carries no event log, so what a timeline may honestly say is a derivation — and one place to change when the record grows one |
| `readEvidence(evidence)` | the driver's sentence, and the commits inside it (SURF4c) | reads the bundle's **shape**, never its words: no English literal is matched, so a reworded header is harmless rather than a silently empty list |

`ago`, `sittingDays` and `sessionTool` already exist and are reused.

### Molecules — `src/work/`

| Molecule | What | Story states |
|---|---|---|
| `SessionRow` | one session in the rail: dot, identity, state, **where it runs**, **elapsed**, age (D55); a conversation named by its first line, and a menu of its own window, its review and its id (RAIL1) | all nine states · driven vs chat · no quest · long + CJK title · selected · another machine · **its own tree** · running three hours |
| `RepositoryGroup` | the rail's group header | drivable, held, busy, not adopted, no root |
| `SessionHead` | the attended session's record: state, repo, tree, quest, tool + account, age | driven, chat, parked-with-analysis, ended, `--real` tree vs session tree |
| `TimelineEntry` | one observed event: state change, quest transition, commit landed | each kind · a long commit subject · an entry with no note |
| `SessionTimeline` | the observed layer over `sessionTimeline()` — props-only (SURF4c) | just queued · parked · whole · nothing landed |
| `Composer` | the chat input with its two endings, the turn's own stop (CONV4b), the files a message carries (CONV4c), `@` a file in the tree (CONV4d) and the context ring (CONV5) | idle, sending, session ended mid-typing, refused, turn running (queued messages, stop turn), stopping the turn, turn running on a text door, files attached, mentioning a file, with context |
| `MentionList` | the files an `@` could mean, over the composer's box, which keeps the focus (CONV4d) | offering · second chosen · listing · no match · empty tree · unlisted (the host's sentence) · bounded · long + CJK names |
| `ContextRing` | how full the session's context is, under the composer, with the high-water mark in its tip (CONV5) | measured · under one percent · filling (warn) · after compacting · not reported yet · a text door · door unknown |
| `AttentionRow` | one row of Overview's *what needs you* band | parked · quest nobody can take · a proposed ask · an ask whose intake asked · no door · no detail · long + CJK. ~~finished-unreviewed~~ — **not buildable before SURF6**: nothing records that anybody looked |
| `AwaitingPerson` | a parked session's analysis and the person's three moves (SURF5) | parked · nothing said · a move in flight · declining |
| `AwaitingIntake` | a parked intake's question, a door to its ask, and a stop that keeps the ask a proposal (INT4g) | asking · nothing can act |
| `RunningIntake` | a running intake: why it takes no messages, a door to look at its ask, and the stop its composer used to carry (INT4h) | running · nothing can act |
| `TrustAsk` | the agent's trust question for a folder the driver holds: the folder, what trusting means, what it holds, the one file written, and the grant on the press (D73) | asking · granting |
| `DiffFileRow` | one file in the review pane | added, modified, deleted, renamed, binary, truncated |
| `PatchView` | a file's patch: numbered, highlighted a side at a time, unified or side by side (REVIEW2) | unified · side by side · unknown language · rename only · long line |
| `RightDock` | the per-session surfaces beside the session, with its geometry handed in (FRAME6) | docked · cramped · full · full in a narrow window · closed |
| `SessionStripRow` | one running session in the rail's 56px strip (FRAME6) | working · attended · awaiting person · a 中文 repository |
| `Splitter` | the edge a column is resized by, keyboard-operable (FRAME6) | held by `chrome.test.tsx` |

### Organisms — `src/work/`

| Organism | What | Held by |
|---|---|---|
| `SessionRail` | sessions grouped by repository, selection, empty state | mocked-bridge vitest |
| `AttendedSession` | head + stream + timeline + composer, for one session | mocked-bridge vitest |
| ~~`SessionTimeline`~~ → **a molecule** (SURF4c) | the observed audit layer beside the stream | props-only vitest · a story per kind |
| `SessionConsole` | **exists** — promoted out of the drawer, otherwise unchanged | its current tests |
| `AttentionBand` | Overview's *what needs you* | the page suite over a stubbed service |
| `DiffPane` | the review surface, bounded and stating what it truncated | mocked-bridge vitest |

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
| `OutputPanel` | the stream, **growable, shrinkable, hideable** — `MonoWell` inside a region, not a well inside a card | collapsed · default · grown · no session attended |
| `StartSession` | repository, harness, account, own tree — moved out of Projects (SURF4d) | every choice · one harness and no accounts · nothing to talk in |
| `WorkFrame` | rail + attended + panel + status bar | the assembled frame |

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
