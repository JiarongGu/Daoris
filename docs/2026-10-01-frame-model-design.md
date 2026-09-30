# One frame for every view — the layout model (FRAME1)

**Carried by:** FRAME1 in `TASKS.md`. **Status:** design (FRAME1a), written before the build, and **D118**
records what it settles. The audit it answers is `docs/2026-10-01-frame-audit.md`, where every row cites
the file and line that decides it today.

**What it amends**, each marked where it is amended:
- the desktop frame design's §3 (D56);
- the dock design's §3 and §5;
- the components plan's §3a, for FRAME6's strip rule;
- the platform language's §2 and §4 *Drawer*.

> *"why only session has more layout option we do need to make everything consitant"* — the owner,
> 2026-10-01.

The owner's sentence is the brief. Sessions has the whole frame: a list that collapses and resizes, the
main area, the right side bar with views that move, the panel, the strip's toggles, and each of them
remembered. DOCK1a gave every view the side bar and the panel. The rest stayed Sessions' own (audit A1–A12).

## 1. The regions

```
┌──────────────────────────────────────────────────────────────────┐
│ ▦ Daoris  Workspace  Agents  View    [ command center ]  ◧ ▤ ◨ ─□✕│ strip
├──┬────────────┬──────────────────────────────┬───────────────────┤
│▣ │ list pane ⟨│ main area                    │ right side bar    │
│▤ │ ＋  ⋯      │ the item chosen in the list, │ the frame's:      │
│▥ │ the view's │ or the view's page           │ Ask Daoris, the   │
│◈ │ own list   │ the view's                   │ attended session's│
│⌕ │            ├──────────────────────────────┤ views             │
│⚙ │            │ panel — the frame's          │                   │
├──┴────────────┴──────────────────────────────┴───────────────────┤
│ status bar                                                       │
└──────────────────────────────────────────────────────────────────┘
```

**Every view has the same regions, and they have two owners.**

- **The view owns the list pane and the main area.** They change when the view changes, and each view
  remembers its own (§3f).
- **The frame owns the activity bar, the strip, the status bar, the right side bar and the panel.** They
  are the same on every view (DOCK1a), so what is open, chosen and sized in them survives a change of view.

The panel stays under the main area and never runs under the list, as it does now (audit F5). The strip's
three toggles belong to the three movable regions. From left to right they are the list, the panel and the
side bar, in VS Code's order.

## 2. What each view puts in them

| View | List pane, and its `＋` | Main area | A closed list's strip | Drawers (forms only) |
|---|---|---|---|---|
| **Sessions** | The session rail as it is (`＋`: start a session) | The attended session | Each running session's initial and mark, as today | Starting a session |
| **Overview** | None (§4) | Its page as it is | — | — |
| **Quests** | The asks, then the quests by state. *Addressed to* and *include closed* sit in the list's ⋯ (`＋`: *Ask* first, then *New quest*) | The chosen quest's record, with its acts in its header and the next step the loud one (U31), or an ask's record | Its controls | Composing a quest or an ask |
| **Projects** | The adopted repositories, then *Registered, not adopted* (`＋`: *Add a repository*; ⋯: *Import a folder*) | The repository's page: what its card says now, its driving row, its line, its unlanded branches, the join steps for one not adopted, *Manage* in its header, and a door to its code map | Its controls | Adding, importing, managing a declaration |
| **Map** | None (§4) | The canvas, with the detail beside it by the main area's own width | — | — |
| **Convergence** | The threshold, then the findings: method, similarity, repositories | The chosen finding: the service's sentence, then its entries read whole | Its controls | — |
| **Search** | The query box, *local only*, and the hits | The chosen entry, read as the reader reads it now | Its controls | — |
| **Settings** | Its domains. There is no `＋`: Settings makes nothing | The chosen domain, at the part a door named | Its controls | The forms its rows open, as today |
| **Plugins** (PLUGUI1) | Installed plugins by state, then the offers the install carries (`＋`: make one with the kit, ask for one, install from a folder) | The plugin's page (§7) | Each plugin's initial and state mark | Making one with the kit |

**With nothing chosen, the main area says how to choose** and offers the list's `＋`, as *Nothing attended*
does on Sessions.

## 3. The same behaviour everywhere

### 3a. The list pane

- **Its header** holds the list's name (the words are NAME1's), then `＋` where the view makes something,
  ⋯ for the list's own filters and menu, and close. `＋` is one control (D56 §2, rule 4). Where a view
  makes two kinds of thing, `＋` offers both, the view's primary first.
- **Open, it sits beside the main area.** It is resized by its right edge within the view's bounds, by
  `Splitter` as it is: arrows step, Home and End go to either end, and a double-click resets. The bounds
  are one table in `layout.ts`. Sessions keeps FRAME6's 264–420 px, 280 to start, and every other list
  takes the same, except Settings: 176–320 px, 176 to start, since its rows are single names (today's
  11 rem).
- **Closed by the person, it is a 56 px strip.** The strip holds the header's controls (open, `＋`) and the
  view's strip items where the view has them. Nothing but the person opens it again (FRAME6's rule).
- **It is a strip by itself when there is no room beside it.** That happens when, with the list at its
  width, the main area would fall below its 400 px floor (`CENTRE_FLOOR`), with the side bar as it stands:
  its 32 px strip while closed, its 300 px floor while open. The list gives way first, because its strip
  keeps its doors. The side bar then gives way to its floor, and past that it asks to be closed (FRAME6's
  *cramped*). Room returning gives the list back.
- **The strip the window drew opens the list over the main area**, beside the strip. It closes on a
  choice, on Escape, and on a press outside it. *This amends FRAME6*, under which a strip the window drew
  offered no open. That rule left Sessions' ended sessions and its search out of reach below 1024 px
  (audit SE2), and on a view whose list is its only way in, such as Settings, it would leave the whole
  view out of reach. The strip stays in view, so nothing hides behind a hamburger (platform language §2).
- **Four doors toggle it**: its toggle on the strip, the View menu's item, Ctrl+B, and **pressing the
  current place on the activity bar**, as VS Code's activity bar does. A view with no list has none of the
  four: they are absent, never disabled.
- **Inside it**, ↑ and ↓ move between rows, Home and End go to either end, and Enter opens the row. A row
  with a menu of its own keeps the menu, reached by Tab. A list with a search clears it on Escape, as the
  rail does.

### 3b. The main area

- **A page header** carries the chosen item's title, one line and its acts: a quest's *take* or *done*, a
  repository's *Manage*, a plugin's switch. On a view with no list it is the view's own header, as now.
- **It scrolls by itself.** Neither the list nor the frame's regions scroll with it.
- **It lays out by its own width, never the viewport's.** A split inside it follows the main area's
  container width: Overview's two columns, Projects' cards, Map's detail beside the canvas. The reason is
  that the side bar and the list narrow the main area without the window changing (audit A4). USE1's
  container query on Overview's repository rows is the rule's first instance. The main area keeps its
  400 px floor, and the regions beside it give way to it.
- **Its states:**
  - nothing chosen: the empty state that says how to choose;
  - the chosen item loading: skeleton rows, never the empty state (audit SE11);
  - an item that has gone (retired, deleted, no longer on this machine): a state that says so, as a
    detached window's *gone* does;
  - an error: the last answer kept, with one toast, as now.

### 3c. The side bar and the panel

- **Their behaviour does not change** (dock design §4, TABS1).
- **They hold only what is the same on every view**: Ask Daoris; the attended session's timeline, review,
  console and preview; the terminal. **A view's own detail never goes there**, whether a quest's record or
  a map node's detail. DOCK1a made the side bar the frame's so that what is open in it survives a change
  of view, and a region whose content changed with the view would lose exactly that.
- **Off Sessions, a session's view names the session it speaks for** (DOCK1a). With nothing attended, it
  says so without naming a rail (audit SE11).

### 3d. A record is the main area; a form is a drawer

- **A record opens in the main area of the view whose list holds it**: a quest, an ask's record, a
  repository, a knowledge entry, a finding, a domain, a plugin.
- **A form stays a drawer**, as D41 made it: starting a session, composing a quest or an ask, adding or
  importing a repository, managing a declaration, trusting a folder, About.
- **A door from another view goes to the owning view with its item chosen**: Overview's rows, the map's
  quest, the status bar's conflicts, a session's chain. Quests already works this way (`questFocus`), and
  so does Sessions (`openInWork`). §3i makes it one opener.
- *This amends the platform language's §4 Drawer* (*a quest's detail with its actions*, and the reader).
  The drawer stays the single form surface, and a record leaves it once its view has a list. The reason:
  since DOCK1a the drawer lies over the side bar and the panel, which hold Ask Daoris and the attended
  session, and its scrim dims them (audit F10). Reading a record beside Ask Daoris is the arrangement the
  owner asked the frame for.
- **Every overlay opens above a full side bar, never under it** (audit F10).

### 3e. Keys

| Key | Does | Where |
|---|---|---|
| Ctrl+B | Toggles the view's list | Every view with a list (Sessions only, today) |
| Pressing the current place | Toggles its list | Every view with a list |
| Ctrl+J · Ctrl+Alt+B | Toggle the panel · the side bar | Every framed view, unchanged |
| ↑ ↓ · Home End · Enter | Move in the list · go to its ends · open the row | Every list |
| Escape | Clears the list's search; closes a list laid over the main area | Every list |
| F6 · Shift+F6 | Focus the next · the previous region (VS Code's *Focus Next Part*) | Every view. Measured on the window first, since the engine may keep F6 for itself; dropped if it does |

### 3f. What is remembered

| What | Remembered for | Where it is kept |
|---|---|---|
| The list's closing and width | Each view | `daoris.list.<view>.closed`, `.width`. Sessions keeps `daoris.railClosed` and `daoris.railWidth`, so nothing reopens on the upgrade |
| The chosen item | Each view | `daoris.list.<view>.chosen`. Sessions keeps `daoris.attending` and Settings `daoris.settings` |
| The list's filters | Each view | `daoris.list.<view>.filters`: Quests' *addressed to* and *include closed*, Search's *local only*, Convergence's threshold. A search's query is not kept: it is what the person is typing now |
| The panel and the side bar: closings, height, share, where each view stands | Every view | Unchanged (audit F8) |
| Not kept | — | *Fill the frame*, the panel's shown view, the side bar's tab per session (all as now), and a list laid over the main area |
| Which view a launch opens on | — | Unchanged: Overview, or Sessions when the person was there (D40, D56) |

**Per view, because each view's list differs in kind and in width.** A person who closes Sessions' rail
to give a conversation room has not asked to lose Quests' list, and on Quests the list is how the view is
used. VS Code keeps one side bar for every activity because its editor is shared between them. Here the
main area is the view's own, so its list is too.

### 3g. A narrow window

| Width of the window | List pane | Main area | Right side bar |
|---|---|---|---|
| Room for all three | Beside, as chosen | The rest, at least 400 px | Beside, as chosen |
| No room for the list | A strip by itself, which opens over the main area | The rest | Beside, as chosen; cramped, it asks to be closed |
| Below 768 px | As above | The rest | Open, it covers the frame (unchanged) |

The main area's splits follow its own width (§3b). Every overlay sits between the strip and the status bar
(platform language §4), and above a full side bar (§3d).

### 3h. Each region's states

- **The list pane.**
  - A first load shows skeleton rows. The strip meanwhile shows its controls, where today it shows
    nothing (audit SE11).
  - An empty list shows the empty state, with the `＋`'s action.
  - An error keeps the last answer and shows one toast. With no answer ever, the list says the sentence in
    place and is never blank.
- **The main area:** as §3b says.
- **Settings' machine domains** show skeleton rows while they load, where today they draw nothing (audit
  ST11).
- **The side bar and the panel:** unchanged.

### 3i. Every door names its item

A door into a view says what it opens, and the view's chosen item takes it. `openInWork(session)` and
`questFocus` already do this. **The application holds one opener, `open(view, item?)`**, and every door
calls it: the palette, a status item, a row of *What needs you*, a map's detail, a notification, and Ask
Daoris's *go*. Ask Daoris's *go* is the `StarterDoor`, which gains `item`. PLUGUI1's go anchors are the
opener's first new callers.

## 4. Where a view differs, and why

- **Overview has no list.** It is a page of summaries, and each of its rows is a door into the view that
  owns the thing (D40; platform language §5). A list of those doors would be a second copy of *What needs
  you*. Its list toggle is absent.
- **Map has no list.** Its canvas is its navigator, with a search of its own past twelve repositories
  (MAP4a), and a list of repositories beside the canvas would be Projects twice. Its detail stays in the
  main area beside the canvas, laid out by the main area's width (§3b). It does not go to the side bar
  (§3c).
- **Strip items.** Sessions keeps its strip items, the running sessions, and Plugins gains its own. Every
  other list closes to its header's controls, until a look asks for more.
- **Settings makes nothing**, so its list has no `＋`.
- **A browser keeps the list and the main area**, which hold nothing machine-local. It never has the side
  bar or the panel (D47 §4), and its strip holds the list's toggle alone. *This amends DOCK1a*, under
  which a browser keeps the view alone, for the half that is not machine-local. Playwright holds the
  absence of the other half, as it does now.
- **The monitor and a detached session's window keep their native frame, without a strip** (D55 §b).
  - The monitor's rail is a `ListPane`: a strip when there is no room, never hidden (audit MO2), with its
    width remembered for the monitor.
  - A detached window's console is the `OutputPanel`, resized, hidden and remembered for detached windows
    (audit DE6).
  - Each window answers Ctrl+B or Ctrl+J for the region it has.

## 5. The components

The components plan's method holds: a story first, **a molecule imports no hook** (no `./queries`, no
`./shell`), and a component's layer is what it imports.

**Atoms** (`ui.tsx`)
- `StripMark`: one item on a closed list, its initial and its mark, named by its tip. It is
  `SessionStripRow` made general, and Plugins is its second user. Stories: working, waiting, off, a
  中文 name.

**Pure helpers**
- `work/layout.ts`: `LIST_BOUNDS` per view, and `frameLayout` taking the view's list choices and returning
  `list: { mode: 'open' | 'strip' | 'over', width, auto }` beside the side bar's mode. The room rule of
  §3a lives here and nowhere else. Unit tests hold every width.
- `work/listKeys.ts`: `nextRow(count, current, key)`, pure, with a small React hook binding it to a list.
  The hook reaches no data, so a molecule may use it.

**Molecules** (`work/`)
- `ListPane`: the header (name, `＋` with its kinds, a ⋯ slot, close), the body, the strip (controls and
  strip items), the list laid over the main area, and its `Splitter`. Every state is reachable by props.
  Stories: open, closed, strip by the window, laid over, at its least and its most, a `＋` of two kinds, a
  中文 name, empty, loading.
- `ViewMain`: the main area's scroll box as a container (`@container`), its page header's slot, and its
  four states (chosen, nothing chosen, loading, gone). A story for each.
- `LayoutToggles`: the `rail` region becomes the view's list, named by the view, and the stale comment
  and the unused `names` prop go (audit F6).
- `ActivityBar`: `onToggleCurrent`, for a press on the current place.
- `OutputPanel`, `Splitter` and `RightDock` do not change.

**Organisms and pages**
- `WorkFrame` takes a `ViewLayout` in place of `content`: `{ list?: ListSpec; main: ReactNode }`. It draws
  the list pane for every view that hands one, and Sessions' rail becomes one such list.
  `work/listPanes.ts`, beside `closings.ts`, holds each view's closing, width, chosen item and filters,
  keeping Sessions' and Settings' keys.
- A browser draws the same `ViewLayout` without the side bar and the panel.
- Each view's page splits into a list organism and a main organism:
  - `QuestList` + `QuestPage`, and `AskPage` (from QuestsView's cards and its drawer's body, and from
    `AskRecord`);
  - `ProjectList` + `ProjectPage`;
  - `HitList` + `EntryPage` (the reader's body);
  - `FindingList` + `FindingPage`;
  - `DomainList` + the domain;
  - `PluginList` + `PluginPage` (PLUGUI1).

## 6. The build

These rows are ready for `TASKS.md`.

**Every row's proof:**
- the stories of what it adds, each smoke-tested by `composeStories`;
- vitest over the mocked bridge;
- `npm run verify`;
- after the merge, the parent's look on the window in both themes and both languages, at 1280 px and
  680 px.

**Order.** FRAME1b, then FRAME1c, then FRAME1d–g, then FRAME1h and FRAME1i. FRAME1d, e and f share the
Web shell lane, so they go one at a time. FRAME1g is in the Web settings lane, so once FRAME1c has landed
it can run beside one of them. PLUGUI1 can start once FRAME1c has landed.

- [ ] **FRAME1b — the list pane, Sessions' first.** Covers audit A1, A5, A7, A11, A12 and F1's missing
  door.
  - **What:** `ListPane`, `StripMark`, `listKeys`, and `layout.ts`'s per-view bounds with the room rule
    and the laid-over mode (§3a). Sessions' rail moves onto `ListPane`. A press on the current place
    toggles its list. `LayoutToggles`' list region and the View menu's item are named for the view.
  - **Lanes:** Web shell.
  - **Proof:**
    - `layout.test.ts` over every width, with the list giving way before the side bar;
    - the `ListPane` stories;
    - `listKeys` tests;
    - `WorkFrame.test.tsx`: the rail at 900 px beside a closed side bar, the strip's open laying the rail
      over, and Sessions' old keys still read;
    - `LayoutToggles.test.tsx`, and `chrome.test.tsx` for the activity bar's press.
  - **Look:** Sessions at 1280 and 680 px, and at 900 px with the side bar open and then closed. The rail
    open, closed and laid over. ↑ and ↓ in the rail.
- [ ] **FRAME1c — the main area and the view contract.** Covers audit A4, A6 (Sessions), A10 and F10.
  - **What:**
    - `ViewMain`; `ViewLayout` in place of `content`; `listPanes.ts` with its per-view memory;
    - the opener `open(view, item?)` and `StarterDoor.item` (§3i);
    - container queries in place of the viewport's in Overview (`OverviewView.tsx:98`), Projects
      (`ProjectsView.tsx:140`) and Map (`MapView.tsx:83, 155`);
    - the browser's frame of list and main area;
    - Sessions' main area loading as skeleton rows, and the timeline off Sessions saying *nothing
      attended* without a rail;
    - the drawer above a full side bar;
    - `LinesMenu` on `lib/stored.ts`.
  - Overview and Map take their final shape here.
  - **Lanes:** Web shell.
  - **Proof:**
    - the `ViewMain` stories;
    - `App.test.tsx`: each door naming its item;
    - `WorkFrame.test.tsx`: a view with a list and a view without one;
    - vitest for Overview and Map at a narrow container;
    - `test:web`: the browser's list and main area, and still no side bar and no panel.
  - **Look:** Overview, Projects and Map at 1280 px with the side bar open and then closed, and at 680 px.
- [ ] **FRAME1d — Quests on the frame.** Covers audit QU1–QU4 and QU9; A2 for quests and asks.
  - **What:**
    - the list: asks, then quests by state, with the filters in its ⋯, remembered; `＋` *Ask* first,
      then *New quest*;
    - the main area: the quest's record with its acts in its header, and an ask's record;
    - the composers stay drawers;
    - doors from Overview, the Map, the status bar and a session choose the quest.
  - **Lanes:** Web shell (`QuestsView`, `asks/`, their catalogues).
  - **Proof:**
    - QuestsView's suites (`QuestsView.test.tsx`, `.shell`, `.held`) and `asks.test.tsx` moved from the
      drawer to the page;
    - stories for `QuestList`, `QuestPage` (open, taken, sitting, held, a chain, a 中文 title) and
      `AskPage`;
    - `test:web`, whose quest's whole life moves from the drawers to the page, in this same row.
  - **Look:** a quest beside Ask Daoris at 1280 px, and the list laid over at 680 px.
- [ ] **FRAME1e — Projects on the frame.** Covers audit PR1–PR4 and PR9.
  - **What:** the list: adopted, then *Registered, not adopted*; `＋` *Add a repository*; ⋯ *Import a
    folder*. The main area: the repository's page, with *Manage* in its header and a door to its code map.
    The chosen repository remembered.
  - **Lanes:** Web shell.
  - **Proof:** `ProjectsView.test.tsx` moved to the page; stories for `ProjectList` and `ProjectPage`
    (adopted, not adopted, driven, held, a line, unlanded branches, no root here, a 中文 name); `test:web`'s
    registry pages.
  - **Look:** 1280 and 680 px, a repository chosen.
- [ ] **FRAME1f — Search and Convergence on the frame.** Covers audit SR1–SR4, SR9, SR11, CO1–CO4 and
  CO9; A2 for the reader.
  - **What:**
    - Search: the list is the box, *local only* and the hits, with ↑ and ↓; the main area is the entry
      read;
    - Convergence: the list is the threshold and the findings; the main area is the finding, its entries
      read whole under the service's sentence;
    - the reader drawer leaves both doors;
    - *local only* and the threshold remembered;
    - Search's loading decided by the look (audit SR11).
  - **Lanes:** Web shell.
  - **Proof:** `SearchView.test.tsx`, `ConvergenceView.test.tsx` and `Reader.test.tsx` moved to the pages;
    stories for `HitList`, `EntryPage`, `FindingList` and `FindingPage`; `test:web`'s search.
  - **Look:** both at 1280 and 680 px, an entry and a finding chosen.
- [ ] **FRAME1g — Settings on the frame.** Covers audit ST1–ST3, ST10, ST11 and PL11.
  - **What:** the domain list becomes Settings' list pane (176–320 px), which never stacks. The anchor
    stays. The machine domains show skeleton rows while they load.
  - **Lanes:** Web settings (`SettingsView`, `settings/`), after FRAME1c for the frame's hand-off.
  - **Proof:** `SettingsView.test.tsx`, `SettingsView.shell.test.tsx`, `domains.test.ts`, and each machine
    domain's loading in its suite.
  - **Look:** Settings at 1280 and 680 px, English and 中文, and the first open of each machine domain.
- [ ] **FRAME1h — the secondary windows.** Covers audit A8 and A9.
  - **What:**
    - the monitor's rail on `ListPane`, a strip when there is no room and never hidden;
    - a detached window's console on `OutputPanel`;
    - each window's own memory, and Ctrl+B or Ctrl+J for its region;
    - the monitor's title on a token that exists, and `tokens.test.ts` failing on an undefined `text-*`
      class.
  - **Lanes:** Web shell.
  - **Proof:** `MonitorWindow.test.tsx`, `DetachedSession.test.tsx` and the tokens test, which must fail on
    `text-h3` first.
  - **Look:** `shot --window monitor` at 900 and 1400 px, and `shot --window session:<id>` with a long
    console and narrow, both themes.
- [ ] **FRAME1i — Ask Daoris knows each view's list and item.**
  - **What:**
    - `help/where.ts` tells the helper which lists are open and which item each view has chosen;
    - a *go* proposal may name an item;
    - the room's *The window* says that every view but Overview and Map has a list, and says the four
      doors.
  - Between FRAME1d and this row, the room names fewer lists than the window has, and this row closes the
    gap.
  - **Lanes:** Web shell (`help/`); Driver library (`HelpRoomWindow.cs`, `HelpGoProposals`, the room's
    golden files); Service only if the *go* kind's wire gains `item` (`HelpProposalBox`,
    `KnowledgeTools.Help.*`).
  - **Proof:** `where.test.ts`, `HelpRoomWindowTests` and its golden, `HelpGoProposalsTests`, and the
    service's go-proposal test where the wire changed.
  - **Look:** from Overview, Ask Daoris asked to open a named quest.

## 7. What PLUGUI1 takes from the frame

- **A view entry.** `VIEWS` (`commands.ts`) gains `plugins`, `shellOnly` since plugins are this machine's
  (D64; D47 §4), with its icon. The activity bar and the palette read it from there.
- **The list pane (FRAME1b).**
  - The installed plugins, grouped by state (running, off, refused), then the offers the install carries.
  - `＋` with three kinds: make one with the kit (PLUG8's form, a drawer); ask for one, which opens Ask
    Daoris on a first message as SETUP1b's `askSetup` does; install from a folder.
  - Strip items: each plugin's initial and state mark (`StripMark`).
- **The main area (FRAME1c).** The plugin's page in `ViewMain`:
  - its manifest, read as a person reads it;
  - what it said on its wire lately, from the machine log;
  - its data folder;
  - its trials and its own tests;
  - its switch, *Try*, *Update…* and *Remove* in its header.
  - With nothing chosen, the page offers the kit.
- **The memory (FRAME1c):** its chosen plugin, and its list's closing and width.
- **The door (FRAME1c, §3i):** `open('plugins', id)`, which Ask Daoris's plugin kind's go anchors call
  (FRAME1i).
- **What the frame need not give it:** a side bar view or a panel view of its own. A trial's output stays
  on the plugin's page (§3c), and the side bar keeps Ask Daoris for asking.
- **Settings keeps only what is a setting.** That half is PLUGUI1's own, in the Web settings lane.

*D119 (PLUGUI1a) is the view's contract: `docs/2026-10-01-plugins-screen-design.md`. It orders the list's groups
by what a plugin needs from the person (waiting on you, on, off, then the offers), and confirms that the side bar
and the panel gain nothing from the view.*

## 8. Not chosen

- **A shared main area with tabs across views** (VS Code's editor groups): a quest, a session and an entry
  open side by side as tabs. It is the editor's model: a second navigation beside the list, and files as
  the organising object, which D55 declined for a session. It is worth revisiting only if a person asks to
  hold two records of different views at once.
- **A view's own detail as a side bar view** (a quest's record, a map node's detail, a trial's output). The
  side bar would change with the view, and DOCK1a made it the frame's so that it would not.
- **Keeping a record in the drawer.** It is modal over the side bar and the panel, which since DOCK1a hold
  Ask Daoris and the attended session (audit F10).
- **One list state for every view** (VS Code's). §3f gives the reason.
- **A list on Overview; a list on Map.** §4 gives the reasons.
- **One viewport threshold for every list** (Sessions' 1024 px, today). It is simple to predict. But it
  turns Settings' 176 px list into a strip at widths where it fits beside the domain (768–1024 px). It
  also keeps Sessions' rail a strip at 900 px with the side bar closed and 540 px free for the
  conversation. The room rule is the side bar's own rule (*cramped*), applied one region further left.
- **Stacking a list above the main area when narrow** (Settings' today), or **hiding it** (the monitor's
  today). The first is the arrangement D56 rejected for the activity bar. The second leaves no way back.
- **Opening a strip the window drew beside the main area**, as a strip the person closed does. With no
  room beside, the main area would fall below its floor, which is the reason the list gave way.

## 9. What does not move

- **D55:** the session is the organising object, and there is no editor.
- **D56:** the strip, the activity bar, the status bar and the one denser scale.
- **DOCK1a–e and TABS1:** the side bar and the panel, their views, moves, drag, reset, Quick Ask, and a
  tab shown whole or as its icon.
- **D41's language:** drawers for forms, and overlays between the strip and the status bar.
- **D47 §4:** nothing machine-local reaches a browser.
- **D40:** a launch opens on Overview, or on Sessions when the person was there.
- **The names:** NAME1's. This design uses today's words and renames nothing.
