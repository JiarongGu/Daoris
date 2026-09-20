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
   design names gets one — including the ones real data rarely shows.
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
| `SESSION_TONE` | session state → pill tone, **exhaustive at compile time** | the twin of `QUEST_TONE`, and the reason that one exists: nine states, and a tenth cannot ship half-toned. Today a session pill is `live ? taken : neutral` — eight states wearing two tones |
| `sessionTitle(session, quest)` | the derived identity: repository · kind · what it is for | design §3 — identity is derived and never invented; one implementation, or the rail and the head disagree |

`ago`, `sittingDays` and `sessionTool` already exist and are reused.

### Molecules — `src/work/`

| Molecule | What | Story states |
|---|---|---|
| `SessionRow` | one session in the rail: dot, identity, state, age | all nine states · driven vs chat · no quest · long + CJK title · selected |
| `RepositoryGroup` | the rail's group header | drivable, held, busy, not adopted, no root |
| `SessionHead` | the attended session's record: state, repo, tree, quest, tool + account, age | driven, chat, parked-with-analysis, ended, `--real` tree vs session tree |
| `TimelineEntry` | one observed event: state change, quest transition, commit landed | each kind · a long commit subject · an entry with no note |
| `Composer` | the chat input with its two endings | idle, sending, session ended mid-typing, refused |
| `AttentionRow` | one row of Overview's *what needs you* band | parked, finished-unreviewed, quest nobody can take |
| `DiffFileRow` | one file in the review pane | added, modified, deleted, renamed, binary, truncated |

### Organisms — `src/work/`

| Organism | What | Held by |
|---|---|---|
| `SessionRail` | sessions grouped by repository, selection, empty state | mocked-bridge vitest |
| `AttendedSession` | head + stream + timeline + composer, for one session | mocked-bridge vitest |
| `SessionTimeline` | the observed audit layer beside the stream | mocked-bridge vitest |
| `SessionConsole` | **exists** — promoted out of the drawer, otherwise unchanged | its current tests |
| `AttentionBand` | Overview's *what needs you* | mocked-bridge vitest |
| `DiffPane` | the review surface, bounded and stating what it truncated | mocked-bridge vitest |

### Page and shell

`WorkView` (the full-bleed layout, rail + attended), the sixth nav item with its two counts, and the
last-view memory.

## 5. The build order, cut along the layers

SURF4 becomes four session-sized items; SURF5 and SURF6 keep their numbers and their UI halves follow
the same method. Each item is TDD, gates green, archived on completion.

| Item | Lands | Proven by |
|---|---|---|
| **SURF4a** | the three atoms, `SESSION_TONE`, `sessionTitle`, the presentational-import check, stories-as-smoke-tests | stories + `ui.test.tsx`; the import check sabotage-tested |
| **SURF4b** | `SessionRow`, `RepositoryGroup`, and `SessionRail` over them | props-only vitest for the two molecules; mocked bridge for the rail |
| **SURF4c** | `SessionHead`, `TimelineEntry`, `SessionTimeline`, and the stream promoted into `AttendedSession` | mocked-bridge vitest; the first `npm run desktop -- shot` of the assembled region |
| **SURF4d** | `Composer`, `WorkView`, the nav item, last-view memory, one home for the stream | the page suite; Playwright's record half; a real-window pass |

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

## 7. Two things to settle when SURF4a starts

- **`composeStories` explicitly.** It is present as the framework's own dependency; if making it a
  direct devDependency is more than a line, the stories-as-smoke-tests half is dropped rather than
  worked around — it is a multiplier, not a requirement.
- **Where the import check lives.** Preferred: a vitest test in the web package, because the gate list
  is a list two places must agree on and this needs no new row. A devkit gate is the alternative if it
  ever needs to hold across artefacts.
