# MAPUX1: the Map's UI and UX

Status: proposed 2026-11-06 for the owner's yes (D239), read on main at `c24a9a7a5`. CANVAS1 (D224,
`docs/2026-11-02-canvas-design.md` §3.3, §3.4, §4) settled how the maps are drawn: the platform, the engine, the tiers, culling, the
list, find and the viewport. The map design (`docs/2026-09-23-map-design.md`, D67 §3) settled what the maps draw. This design settles
what the person comes to the Map for and what they see and do there: each zoom, choosing, finding, the list, the keyboard, and the
empty and large cases in English and Chinese. It changes no data the Map reads and adds no act. Web paths start at
`src/Daoris.Web/src/`. The reading was read-only; no test was run.

## 0. In short

- **Four questions** bring a person to the Map: where work is moving and where they are needed; what one repository is and what it
  touches; how a repository's code is put together; and where something is. Acting happens elsewhere; the Map has doors to those places.
- **Two levels, one place:** the workspace map and a repository's code map. The code map is drawn only here. The repository's page
  has a door to it, and *Back to the map* returns with that repository chosen.
- **Each zoom says less about the same cards.** At full size a card has its words; at the chip tier only its name; at the dot tier only
  its place. The chosen and the focused keep their name at every zoom.
- **One choice, read in one place.** A line under the drawing names the choice and is what a reader hears. The detail beside the
  drawing, or below it, holds the facts and the doors.
- **Find reaches the level shown**: names and what a repository says it owns on the workspace map, module ids and folders on the code
  map, from 13 items on either level.
- **The list is the drawing's equal**: one tree on both levels, the reading below 36rem, with the detail opening in place there.
- **The first *Which quests*** is the last 30 days, not all of them.

## 1. What the person comes for

| The question | What answers it | Where the act lives |
|---|---|---|
| Where is work moving, and where am I needed? | The workspace map at its first open: each repository's open count, a session working or waiting on the person (ringed, and said in words), the connections between repositories with their counts | the quest's page on Quests; the session in Sessions; *What needs you* |
| What is this repository, and what does it touch? | Choosing its card: its summary, what it owns, accepts and uses, who uses it, the quests into and out of it | its page on Repositories (a new door in the detail); each quest's page |
| How is its code put together? | The code level: folders, modules in layers by dependency, cycles condensed, the lines routed around the cards | the repository's own checkout; the Map never writes `docs/code-map.json` |
| Where is it? | Find, on the level shown | — |

The Map is not where a person sets work up (Agents → Setup), reads a workflow (a place's *Workflow*), follows a quest's chain (the
quest's page draws `map/ChainStrip.tsx`) or reads what a start runs on (the workspace page draws `map/StartWiring.tsx`). Those parts live
in `map/` and are drawn elsewhere; they are no part of the Map place.

## 2. Two levels, one place

- **The workspace level** is the Map's first: `map/MapView.tsx:127-207`, a ring up to twelve repositories, layers past it, a block per
  workspace when the scope is all of them.
- **The code level** is one repository's code map (`MapView.tsx:54-125`). It is reached by the detail's *Open code map*, or by the
  repository page's door, which opens the Map at this level (the shell's `mapCode`). Its head names the repository and holds the one way
  back, *Back to the map*.
- **Back returns to where the person was.** The workspace map comes back with that repository chosen and revealed, and the focus goes
  to the press that opened the code map, else to the repository's card. Today a code map opened from a repository's page comes back with
  nothing chosen.
- The code map is drawn nowhere else. A repository's page keeps its door and draws no map of its own.

## 3. What each zoom shows

The tiers are CANVAS1's (`design/canvas/tiers.ts`): full where the effective `text-meta` is 9 px or more, a chip from 4 px, a dot
below. At the window's 100% and a 16 px root the full tier holds from about 82% zoom and the chip from about 36%. Today a card draws
the same at the full and chip tiers (`map/MapWorld.tsx:61-87`, `map/code/CodeMap.tsx:204-237`); this table makes each tier its own.

| Tier | A repository | A connection | A module, folder or cycle | A workspace block |
|---|---|---|---|---|
| Full | its name, its open count, a session working or waiting said in words with its ring | its ink, its count at its midpoint | the module's id, cut at `FITS` (17); a folder's name and ×n; *A cycle of n*, and the folders it crosses | its title |
| Chip | its name on one line at the type scale, the ring kept, the count kept, the words dropped | its ink; a count only on the chosen line and the hovered card's lines | the id's last segment; a folder's name and ×n | its title |
| Dot | a dot; the ring kept as an outline | 1 px ink, no count | a dot; a folder's frame with its title | its title |

- **The chosen and the focused keep their name at every tier**, as a label at the type scale beside the card, so the person always
  sees where they are. They are never culled (CANVAS1 §2.6).
- **Status is never colour alone** (D41): at every tier a session's ring is a shape as well as a hue, and at the full tier also words.
- **The ring (twelve or fewer) does not zoom**: it is fitted and always at the full tier.

## 4. Choosing

- **One choice at a time**: a repository, a connection, a module or a code line. A press on a closed folder or a cycle opens it rather
  than choosing it, and puts the focus on its first card, as today.
- **A press chooses; a second press, Escape or the ground releases.** A double press counts as one press, so it never chooses and
  releases at once.
- **The choice stands forward on both levels**: its lines and its neighbours at full ink, the rest at 40%. The code level does this
  today (`CodeMap.tsx:132-140`). The workspace level stands the choice's lines forward (`map/MapLines.tsx:96-99`) but leaves every card
  at full ink; its cards that are not neighbours step back as the code level's do. Hovering or focusing still lights a card's lines
  without choosing it.
- **The choice's line.** Under the drawing, one line names the choice and its first fact: *repo-03 — 3 open quests; a session waits on
  you.* It is the Map's one polite live region, said once per choice. Nothing else on the Map is announced but find's count and the gone
  notice (`design/canvas/GoneNotice.tsx`). With nothing chosen it reads the hint (`map.detail.hint`), or the empty line of §8.
- **The detail** (`map/MapDetail.tsx`, `CodeMapDetail`) stays where it is: beside the drawing from the main area's `@4xl` width, below
  it otherwise, and in place in the list below 36rem (§6). Its head names the choice. Its doors are the repository's page (new), its
  code map, each quest's page and Convergence for a shared finding. It adds no act.
- **A choice lasts** through a zoom, a pan, a fold, the list and a refresh. It is released when its kind of connection is hidden. A
  choice a data change removed is said by the gone notice.

## 5. Finding

- **From 13 items on both levels**, more than a ring holds. The workspace level does so today (`MapWorld.tsx:145`); the code level
  uses the canvas's default and takes the same number.
- **What matches.** On the workspace level, a repository's name, then what it says it owns and accepts. A match by a declaration says
  so in the results (*owns* payments). On the code level, a module's id and its folder's path. Matching ignores case, on the text as
  written: an id is ASCII; a declaration is authored and may be Chinese, and is matched as written.
- **The canvas's find, unchanged**: *n of m*, the rest dimmed; Enter and Shift+Enter step the matches, opening their folds, choosing
  each and revealing it at the full tier, the focus kept in the field; ↓ hands the focus to the match shown; Escape clears and puts the
  view back. **Enter while an input method composes steps nothing** (`isComposing`), so a Chinese name can be typed in the field.
- **Find reaches only the level shown.** A module is found in its repository's code map, never from the workspace.

## 6. The list

- *Show as list* on both levels, kept per viewer as the canvas keeps it; below 36rem it is the reading (CANVAS1 §2.6).
- **One tree on both levels**, the WAI pattern the set-up tree uses (`agents/setup/SetupList.tsx`): one row in the tab order, ↑ ↓ Home
  End walk it, → and ← open, close and climb, Enter chooses, Escape releases. Group headings are rows that open and close.
- **The workspace list**: *Repositories*, each row *repo-03 — 3 open · a session waits on you*; then one group per kind of connection
  in reading order (quests, asks, chains, declared uses, shared findings), each row *game → engine · 4 quests, 1 open*. A hidden kind is
  not listed, as it is not drawn. This is `map/mapList.ts` with the counts and states added to each row's words.
- **The code list**: one group per folder by path, closed past 60 modules as the drawing is; each row *session — uses 3 · used by 2*; a
  cycle is a group of its own under the folder nearest it, *A cycle of 4 across 2 folders*.
- **Below 36rem the detail opens in place**, under the chosen row, so a person reading a long list never scrolls away from it to read a
  choice. From 36rem it stays beside or below the drawing.

## 7. The keyboard order

Tab reaches, in order:

1. at the code level, *Back to the map*;
2. the toolbar, left to right: find; the level's tools (the workspace's *Connections* and *Which quests*; the code map's *Show whole*,
   *Fold again*, and *Fold* the chosen module's folder where it can fold); *Show as list*; the size menu;
3. the region, one stop, where bare `+` `-` `0` and the arrows zoom and pan;
4. the world's one roving stop: ↑ ↓ the previous and next card in reading order, → from a card its first line out and then the card
   beyond, ← the line it came by, Home and End, Enter to choose or open, Escape to release; Shift+1 fits, Shift+2 goes to the choice,
   Shift+0 returns to 100% (CANVAS1 §2.4);
5. the detail's doors, in reading order.

The list, when shown, takes the region's and the world's place as one stop. The legend and the choice's line are no stops. The focus
moves by itself only where the thing focused went away: a folder opened by Enter puts it on its first card, *Fold* puts it on the
folder's card, and *Back to the map* puts it on the press that opened the code map or the repository's card.

## 8. Empty, failing and large

| Case | What the person sees |
|---|---|
| Loading | the skeleton rows, as today |
| The service does not answer | the error notice; a map already drawn stays, saying it could not be read again |
| No repository in scope | the empty state (`map.empty.*`), with a door to Repositories to add one |
| Repositories, no connection yet | the cards drawn; the choice's line reads *No work has moved between these repositories yet* |
| Every kind of connection hidden | the toolbar's *Connections* says 0 of 5; the choice's line says the menu shows them again |
| *Which quests* leaves none | *No quest changed in the last 30 days*, with the count that *All* would show |
| Quests off the map | the outside count under the drawing, as today (`map.outside`) |
| No code map, refused, or a teammate's commit with none | as today (`code.none.*`, `code.refused`) |
| One module and no dependency | the card alone; the choice's line reads *No dependency recorded* |

| Size | What changes |
|---|---|
| Up to 12 repositories | the ring, fitted, no zoom and no find |
| 13 to about 60 | layers, opened fitted and never below 80%; find |
| Several workspaces | a block per workspace by name, the lines running through the gaps |
| Past 400 cards (synthetic) | culling and dots; the list is the whole reading |
| Dense connections | past CANVAS1's path budget the tier drops; counts only on the chosen line and the hovered card's |
| Code past 60 modules | folders open closed; lines between closed folders counted ×n; the 500-module bound; synthetic thousands as CANVAS1 |
| A long name | cut on the card, whole in the detail, the title and the name a reader hears |

**English and Chinese.** Every new word goes into `locales/en/map.json` or `code.json` and its Chinese twin, under the glossary's names
(地图 *map*, 代码地图 *code map*, 连线 *connection*). Repository and module ids stay as written in both languages; a card is sized by
the id, never the chrome. The toolbar wraps to a second row before it cuts a tool's word, which the Chinese narrow stories hold.

## 9. What it opens on, and what it keeps

- **The scope in hand**, its view restored for an unchanged graph (CANVAS1 §4), else fitted (the ring) or at 80% or more (layers).
- ***Which quests* is first the last 30 days** (`map/LinesMenu.tsx:41-51` reads `all` today). A choice already kept stays.
- **All kinds of connection shown** until the viewer hides one, as today.
- **Live data redraws in place.** A count or a session changing never lays out again or refits; a new repository or a new connection
  lays out again, keeping the anchor (CANVAS1 §2.4).

## 10. Rejected

- **A *Code* tab on the repository's page.** A second place that draws one map. The page keeps its door.
- **A separate overview mode, or the minimap as the overview.** The tiers are the overview; the minimap stays CANVAS1's, past twice the
  viewport.
- **Hover cards or popovers as where a choice is read.** Neither the keyboard nor touch reaches them, and they would be a second detail.
- **Choosing several.** Nothing on the Map acts on many.
- **Find across every code map from the workspace.** It would read every repository's file to answer one name; a module is found where
  it is drawn.
- ***All* as the first *Which quests*.** It grows with every closed quest as a workspace ages, and the Map's first question is what
  moves now. *Open only* was rejected too: a quiet workspace would show no connection at all.
- **A double press opening the code map.** One way in, the detail's door, and no level changed by accident.
- **Announcing what changes live.** A status repaint is never said (CANVAS1 §2.6).

## 11. Proof

- **Vitest**: each tier's card on both levels; the chosen and focused labelled at the dot tier; a double press keeping the choice; the
  choice's line said once per choice in en and zh; *Back to the map* choosing and revealing the repository; find matching a
  declaration and saying so, and ignoring Enter while composing; the list's tree keys on both levels; the first *Which quests* for a
  viewer with nothing kept; each case in §8's first table.
- **Stories**, each in English and Chinese, at the wide, half and narrow widths, light and dark: the workspace and code levels at 25%,
  50% and 100%; a choice standing forward on the workspace level; the list with the detail in place, narrow; each empty case. The
  existing `map/MapWorld.stories.tsx`, `map/code/CodeMap.stories.tsx` and `design/canvas/SuppliedLarge.stories.tsx` gain them.
- **Measured** (`measured/`): the Tab order of §7 walked in Chromium on both levels.
- **The installed window**, with CANVAS1g: the keyboard order, the tiers at the window's 100% and 200%, the first real workspace's 29
  repositories, in Chinese.

## 12. Rows, in build order

- [ ] **MAPUX1a — each zoom says less about the same cards** (web; D239). The tiers' content on both levels as §3's table, the chosen
  and the focused labelled at every tier. Contract: §3. Proof: tier tests; stories at 25%, 50% and 100%, en and zh.
- [ ] **MAPUX1b — one choice, its line and its detail** (web). The choice standing forward on the workspace level, the choice's line as
  the one live region, a double press as one press, the detail's door to the repository's page. Contract: §4. Proof: tests; stories.
- [ ] **MAPUX1c — between the levels** (web). *Back to the map* chooses and reveals the repository and returns the focus. Contract:
  §2. Proof: `MapView` tests from the detail's door and from a repository page's door.
- [ ] **MAPUX1d — find on the level shown** (web). From 13 on both levels, declarations matched and said, composing ignored.
  Contract: §5. Proof: tests in en and zh.
- [ ] **MAPUX1e — the list as one tree** (web). The tree keys on both levels, each row's counts and states, the detail in place below
  36rem. Contract: §6, §7. Proof: tests; the list stories narrow in en and zh; the measured Tab order.
- [ ] **MAPUX1f — the empty and failing cases and the first *Which quests*** (web). Contract: §8, §9. Proof: a story for each case
  in en and zh; the default's test.
- [ ] **MAPUX1g — seen on the install** (with CANVAS1g; needs the install). §11's last item.

## 13. Where a read should press

1. **The tiers are set for frames, not for reading.** CANVAS1 fixed 9 px and 4 px for its performance targets. Whether a person reads
   a repository's name at the chip tier's size in the installed window, at 100% and 200%, is not measured, and §3's table depends on it.
2. **The first *Which quests* is a judgement.** Thirty days suits work that moves weekly; a workspace whose quests run for a quarter
   would open with its slow work missing. The read should say whether the owner's own workspace bears it out.
3. **One tree for a flat list.** The workspace list has groups but no folds deeper than one, so → and ← do little there; one pattern
   on both levels was chosen over a listbox for the flat case. The detail opening in place below 36rem is also a third placement of
   one component, by width.
