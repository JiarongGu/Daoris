# FRAME1a — every view's frame, read from the code (2026-10-01)

> *"why only session has more layout option we do need to make everything consitant"* — the owner,
> 2026-10-01 (`TASKS.md`, *One product, not a set of screens*).

**What this is.** The audit half of FRAME1a. For every place the activity bar reaches, and for the two
secondary windows, it records which layout affordances the view has, how each behaves, and the file and
line that decides it. It was read from `src/Daoris.Web/src` at main `7a3fb5f`. Sessions is the reference,
because it is the one view the owner named as having the whole frame. The model this audit feeds is
`docs/2026-10-01-frame-model-design.md` (D118), and §4 says what each finding becomes there.

**Method.** UX5's (`docs/2026-09-26-ux5-screen-audit.md`), split in two. This pass read the source, and the
parent looks at the window. A row marked **code** is settled by the code. A row marked **to look at** can
only be settled by the window, and §5 lists those looks with the width, theme and language each needs. A
finding is:

- **same**: as Sessions;
- **choice**: it differs, and a reason is written where the row says;
- **drift**: it differs and no reason is written, or the reason written no longer holds;
- **gap**: no view has it, Sessions included.

Names are today's, because NAME1 is designing the words, and nothing here renames anything. Paths are
under `src/Daoris.Web/src/`.

**The affordances** carry the same number on every view, so a row is found as `<view><n>`:

| n | Affordance |
|---|---|
| 1 | A list or navigation pane |
| 2 | Its collapse |
| 3 | Its resize |
| 4 | Where an item opens: the main area, a drawer, or another view |
| 5 | The right side bar, and the views it offers |
| 6 | The panel |
| 7 | The strip's toggles and the View menu's (DOCK1c) |
| 8 | Keys |
| 9 | What is remembered, and whether it is remembered per view or for every view |
| 10 | A narrow window: below 1024 px and below 768 px |
| 11 | The empty, loading and error states of each region |

**The views:** Sessions `SE`, Overview `OV`, Quests `QU`, Projects `PR`, Map `MA`, Convergence `CO`, Search
`SR`, Settings `ST`, Plugins as it stands today `PL`, the monitor window `MO`, a detached session's window
`DE`, and a browser `BR`. **There is no *Knowledge* view today.** Knowledge is read through Search,
Convergence and the reader drawer, and their rows say so where it matters.

## 1. The frame every view shares

Since DOCK1a the application draws one Work frame on every view while a shell is attached
(`App.tsx:684–713`). On Sessions the centre is Sessions' own, and every other view is handed in as
`content` (`App.tsx:709`, `work/WorkFrame.tsx:216`). These rows are the same on every framed view, and each
view's table points at them.

| # | What it is and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| F1 | **Activity bar.** 48 px wide, with 36 px places, and it scrolls when the window is too short for them (U22). Settings sits at its foot and the current place is marked. Pressing the current place selects it again and does nothing else, where VS Code toggles its side bar on that press | `work/frame.tsx:170–229`; `App.tsx:644–678` (661) | same on every view | code |
| F2 | **App strip.** The mark, the menus, the command center, the browser's door, the region toggles and the caption room | `work/frame.tsx:42–138`; `App.tsx:555–633` | same | code |
| F3 | **Status bar.** The remote item gives way below 640 px and the index count below 768 px | `work/frame.tsx:252–394` (342–354, 368–379) | same | code |
| F4 | **Right side bar.** One element on every view with a shell. Timeline, Review and Ask Daoris stand there by default (`work/placements.ts:26–28`), followed by anything moved in and a file's preview (D111). Its modes are docked, cramped, full and closed (`work/layout.ts:37–83`), and closed leaves a 32 px strip of its tabs (`work/RightDock.tsx:100–139`). Docked, it resizes by its left edge from 300 px to 70% of the window and keeps its size as a share of the window (`work/WorkFrame.tsx:44–61, 227–231`; `work/RightDock.tsx:156–166`). It has *fill the frame* (258–269), a ⋯ list with *Move to* (247–257), and tabs that drag to the panel (DOCK1e). Its shown tab is kept per attended session, in memory only (`work/WorkFrame.tsx:194–196, 509–529`). It is closed until opened (`work/closings.ts:31–34`, U7) | as cited | choice (dock design §4, DOCK1a–e; U7) | code |
| F5 | **Panel.** One element under the centre column on every view (`work/WorkFrame.tsx:849, 984–1008`). It holds the console with its streams, the terminal, and whatever is moved in (`work/placements.ts:26–28`). *Hide* leaves its header (`work/frame.tsx:714–735`). It is 96–720 px high, 200 to start, set by drag or by keys (`work/frame.tsx:546–548, 638–658`; `work/WorkFrame.tsx:197`). It never runs under a list: on Sessions it sits under the centre and not under the rail | as cited | choice (D55, DOCK1a) | code |
| F6 | **Toggles.** On the strip: the panel and the side bar on every view, and the session list on Sessions only (`App.tsx:623–632`, 627). The View menu carries the same, ticked, with *Reset view locations* (`App.tsx:584–596`). A browser gets none of them. Two leftovers: `LayoutToggles`' comment still says *the rail and the panel are Sessions'* (`work/LayoutToggles.tsx:19–21`), which DOCK1a made false, and its `names` prop, which named the side bar *Ask Daoris* away from Sessions (26–27), is no longer passed (`App.tsx:626–630`) | as cited | choice (DOCK1a); the comment and the unused prop are drift | code |
| F7 | **Keys.** Ctrl+B for the session list, Ctrl+J for the panel and Ctrl+Alt+B for the side bar, anywhere, a field included (`shortcuts.ts:17–25`; `App.tsx:376–402`). Off Sessions, Ctrl+B does nothing and is not consumed (`App.tsx:220–227`). F1 and Ctrl+Alt+I open Ask Daoris, Ctrl+Shift+Alt+L opens Quick Ask, and Ctrl+K opens the palette outside a text field. A splitter steps 24 px per arrow press, Home and End take it to either end, and a double-click resets it (`work/frame.tsx:459–525`). The panel's edge steps 48 px (`work/frame.tsx:548, 648–655`) | as cited | choice (DOCK1c) | code |
| F8 | **Remembered for every view**, per viewer, through `lib/stored.ts`: the panel's and the side bar's closings (`work/closings.ts:24–43`), the side bar's share (`work/WorkFrame.tsx:47`), the panel's height (41), and where each view stands (`work/placements.ts:32, 86–101`). Not remembered: *fill the frame* (`work/WorkFrame.tsx:214`), the panel's shown view (531) and the side bar's tab (196). The last view is remembered only when it is Sessions (`App.tsx:82–85, 311–314`) | as cited | choice (FRAME6, DOCK1b; D40 and D56 for the view) | code |
| F9 | **Narrow.** Below 768 px an open side bar covers the whole frame, and only widening undoes it (`work/layout.ts:76`; `work/WorkFrame.tsx:1028–1029`). A view's gutters go from 24 to 12 px (`App.tsx:470`) | as cited | choice (components §3a) | to look at: 680 px |
| F10 | **The drawer.** It is modal. It runs from the strip to the status bar, and from the activity bar's edge to the window's right (`ui.tsx:602–661`, 622, 636–639), so it lies over the side bar and the panel, and its scrim dims them. A full side bar is `absolute z-20` inside the frame (`work/RightDock.tsx:150`) while the drawer's portal is `z-10`, so below 768 px with the side bar open, a drawer may open underneath it | as cited | choice for the drawer (platform-ux §4 *Drawer*). How it sits against the side bar and the panel was never weighed: drift | to look at: a quest's drawer at 1280 px with the side bar open; the same at 680 px with the side bar full |

## 2. View by view

### Sessions — the reference

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| SE1 | **The session rail**: sessions grouped by repository with the waiting first and the ended beneath, searched by name and by what was said. Its header names it and holds `＋` (start) and close | `work/WorkFrame.tsx:730–821`; `work/SessionRail.tsx` | reference | code |
| SE2 | It **closes** to a 56 px strip holding its open button, `＋`, and each running session as its initial and mark. Below 1024 px it is a strip by itself, and that strip offers no open, so only widening gives the rail back. **Below 1024 px an ended session and the rail's search are out of reach** | `work/layout.ts:5–6, 64–73`; `work/WorkFrame.tsx:735–762` (740) | choice (components §3a, FRAME6: *a strip the window drew offers no "open"*) | to look at: 900 and 680 px |
| SE3 | It **resizes** by its right edge, 264–420 px, 280 to start | `work/WorkFrame.tsx:202, 221–224, 809–819`; `work/layout.ts:6` | reference | code |
| SE4 | **A session opens in the main area**: the head, the conversation and the composer, with one selection binding every region. Starting one is a form in the drawer | `work/WorkFrame.tsx:849–980, 824–847` | choice (D55; D56 §3c–d) | code |
| SE5 | F4, opening on each session's timeline | `work/WorkFrame.tsx:520` | reference | code |
| SE6 | F5 | as F5 | reference | code |
| SE7 | Three toggles: the session list, the panel and the side bar | `App.tsx:584–596, 627` | reference | code |
| SE8 | F7, plus Ctrl+B for its list. Escape clears the rail's search (`work/SessionRail.tsx:162–173`). No key moves between rows, so the rail is walked by Tab | as cited | gap: no list in the application moves by arrow keys | code |
| SE9 | **Its own memory**: the attended session (`App.tsx:74, 108, 406–409`), the rail's closing and width (`work/closings.ts:24, 29, 40`; `work/WorkFrame.tsx:43, 202, 221–224`), and returning to Sessions at a relaunch (`App.tsx:82–85`). Also each session's draft (`work/drafts.ts`) and the chain's shape (`work/AttendedSession.tsx:93–97`) | as cited | reference | code |
| SE10 | Below 1024 px the rail is a strip by itself. Below 768 px an open side bar covers the frame (F9). The head, the conversation and the composer follow the centre's width (U16) | `work/layout.ts:65, 76` | reference | to look at: 680 px |
| SE11 | **Rail**: skeleton rows while the list first loads (`work/SessionRail.tsx:143`), and nothing at all in the strip meanwhile (122–123). An empty state when nothing ever ran (145–157). On an error, the last answer stays and one toast is shown (74). **Main area**: *Nothing attended* (`work/AttendedSession.tsx:99–107`). It says the same while the list first loads with a remembered session, because the frame finds the attended session in that list (`work/WorkFrame.tsx:255`). **Side bar off Sessions**: with nothing attended, the timeline's sentence is the main area's, *Choose a session in the rail and it opens here* (`work/WorkFrame.tsx:701`), on a view with no rail | as cited | the empty states are a choice (D56 §3d). *Nothing attended* while loading, and the rail sentence off Sessions, are drift | to look at: a relaunch into Sessions with a session attended; Overview with the side bar on Timeline and nothing attended |

### Overview

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| OV1 | **No list pane.** The page is the main area: the page header, *What needs you*, four tiles, then *Outstanding* beside *Repositories by index size* | `OverviewView.tsx:58–222`; no rail off Sessions: `work/WorkFrame.tsx:216–219, 730` | choice for the session rail (dock design §4, DOCK1a: *the session list is Sessions' own*). No decision asked whether Overview has a list of its own | code |
| OV2 | Nothing to collapse, and the toggle is absent | `App.tsx:627` | choice (*absent, never disabled*, frame design §3b) | code |
| OV3 | Nothing to resize. Its two columns switch at the **viewport's** 1024 px (`lg:`), but the side bar narrows the column without the window changing | `OverviewView.tsx:98` | drift: USE1 moved the repository rows onto a container query (`OverviewView.tsx:175–178`) and left the grid on the viewport | to look at: 1280 px with the side bar open |
| OV4 | An outstanding row opens its quest's drawer on Quests (U26). A row of *What needs you* opens where its thing lives | `OverviewView.tsx:122`; `App.tsx:453–463, 478` | choice (platform-ux §5; U26) | code |
| OV5 | F4. Off Sessions, a session's view says *Attending …* with *open in Sessions* | `work/WorkFrame.tsx:633–650` | choice (DOCK1a) | code |
| OV6 | F5, with the same attending line over the console | `work/WorkFrame.tsx:986` | choice (DOCK1a) | code |
| OV7 | The panel and the side bar | `App.tsx:584–596, 627` | choice (DOCK1a) | code |
| OV8 | F7. Ctrl+B does nothing | `App.tsx:224–225` | choice | code |
| OV9 | Nothing of its own. A relaunch lands here unless the last view was Sessions | `App.tsx:60–65, 82–85` | choice (D40) | code |
| OV10 | The tiles fit by count. The cards go to one column below the viewport's 1024 px, and the gutters narrow below 768 px | `OverviewView.tsx:67, 98`; `App.tsx:470` | the viewport rule is OV3's drift | to look at: 680 px |
| OV11 | Each card shows skeleton rows while its query loads (108, 165) and an empty state when its answer is empty (109–115, 168–174). An error is a toast (43). The band is absent when nothing waits (65) | `OverviewView.tsx` as cited | choice (platform-ux §4 *Loading*, *Empty states*) | code |

### Quests

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| QU1 | **No list pane: the list is the page.** A page header with *Ask* and *New quest* (356–371), then the filters *addressed to* and *include closed* (373–384), the asks group (387–399), and the quests grouped Open, In progress, Closed (418–426), all in the main column | `QuestsView.tsx` as cited | drift. The list is what a list pane holds on Sessions, and it cannot stand beside a quest. D41 §4's *the list stays a list* predates the frame, and no decision weighed Quests against Sessions' list pane | code |
| QU2 | Nothing to collapse, and the page scrolls | — | drift (follows QU1) | code |
| QU3 | Nothing to resize | — | drift (follows QU1) | code |
| QU4 | **A quest opens in the drawer**: a 32 rem modal over the side bar and the panel (F10), with its acts in the drawer's footer (428–786). An ask's record is a drawer too (`asks/AskRecord.tsx:65`). Composing a quest or an ask is a drawer (`QuestsView.tsx:790–799, 801`; `asks/AskComposer.tsx:61, 73`) | as cited | choice for the drawer (platform-ux §4 *Drawer*). Since DOCK1a the drawer covers the side bar that holds Ask Daoris and the attended session's views, and nothing weighed that: drift | to look at: a quest opened with the side bar on Ask Daoris, 1280 px |
| QU5 | F4, covered while a drawer is up (F10) | — | as F10 | to look at (QU4's) |
| QU6 | F5, covered likewise | — | as F10 | code |
| QU7 | The panel and the side bar | `App.tsx:627` | choice (DOCK1a) | code |
| QU8 | Nothing of its own. A card is a Tab stop that opens on Enter or Space (`ui.tsx:458–477`), and Escape closes the drawer | as cited | gap (SE8) | code |
| QU9 | **Nothing.** The filters reset on every visit (85–86), and the open quest is forgotten (87). A door that names a quest opens it (137–144) | `QuestsView.tsx` as cited | drift. D56 §3d's reason for remembering the attended session holds here: a relaunch lands on nothing while a quest waits | code |
| QU10 | The compose form's two columns stack below the viewport's 768 px (833). The drawer is `min(32rem, window − 3rem)` wide (`ui.tsx:639`). Below 768 px the side bar covers (F9) | as cited | choice | to look at: 680 px, a drawer with the side bar full (F10) |
| QU11 | List: skeleton rows (401), then an empty state with *Ask for something* (403–416). A refetch is drawn at reduced opacity (418). Errors are toasts (166). The drawer opens on a quest already loaded, so it has no loading state | `QuestsView.tsx` as cited | choice (platform-ux §4) | code |

### Projects

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| PR1 | **No list pane.** Adopted repositories are cards, in two columns from the viewport's 1024 px (140), then the *Registered, not adopted* group. The page header holds *Add a repository* (106–112) | `ProjectsView.tsx` as cited | drift (as QU1): a list of repositories, each a card that opens nothing but *manage* | code |
| PR2 | Nothing to collapse | — | drift (follows PR1) | code |
| PR3 | Nothing to resize | — | drift (follows PR1) | code |
| PR4 | **A repository has no page.** Its card holds its facts and its driving row. *Manage* opens the declaration in a drawer (116–118; `ProjectManage.tsx:263`), and adding and importing are drawers (114–115) | as cited | choice: a form is a drawer (platform-ux §5 *Projects*, §4 *Drawer*) | code |
| PR5 | F4 | — | same | code |
| PR6 | F5 | — | same | code |
| PR7 | The panel and the side bar | `App.tsx:627` | choice (DOCK1a) | code |
| PR8 | Nothing of its own | — | gap (SE8) | code |
| PR9 | Nothing | — | drift (as QU9) | code |
| PR10 | Two columns by the **viewport's** 1024 px. At 1280 px with the side bar open, the column is about 650 px and still holds two cards | `ProjectsView.tsx:140` | drift (as OV3) | to look at: 1280 px with the side bar open; 680 px |
| PR11 | Skeleton rows (120), then an empty state naming the circle, with *Add a repository* on a shell (125–138). Errors are toasts (55) | `ProjectsView.tsx` as cited | choice (POLISH2) | code |

### Map

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| MA1 | **No list pane.** The canvas is the navigator: a ring up to `RING_MAX`, and past it layers with a search | `MapView.tsx:156–165`; `map/LayeredMap.tsx` | choice (map design §1, MAP4a) | code |
| MA2 | Nothing to collapse. The detail beside the canvas cannot be closed | `MapView.tsx:155, 183–191` | drift: a second side column, drawn by the page | code |
| MA3 | The detail is a fixed 20 rem column beside the canvas from the **viewport's** 1024 px | `MapView.tsx:83, 155` | drift (as OV3) | to look at: 1280 px with the side bar open |
| MA4 | A node's or a line's detail opens in the card beside the canvas. A quest named there opens its drawer on Quests (U46). *Open its code map* replaces the page, one level in, with *Back* in the page header (49–108) | `MapView.tsx` as cited | choice (map design §1; U46) | code |
| MA5 | F4 | — | same | code |
| MA6 | F5 | — | same | code |
| MA7 | The panel and the side bar | `App.tsx:627` | choice (DOCK1a) | code |
| MA8 | Escape releases a choice, keyboard focus lights a node's lines (U47), and arrows pan the layered map | `map/LayeredMap.tsx:223` | choice (U47) | code |
| MA9 | The line kinds shown and which quests, through a storage guard of its own rather than `lib/stored.ts`, which REV3 CLEAN1 made the one guard. The chosen node and the open code map are not remembered | `map/LinesMenu.tsx:9–57`; `lib/stored.ts:1–9`; `MapView.tsx:40, 44` | a choice to remember (MAP4b). The second guard is drift | code |
| MA10 | Below the viewport's 1024 px the detail goes under the canvas. The drawing shrinks only as a last resort (U44) | `MapView.tsx:83, 155` | choice (U44) | to look at: 680 px |
| MA11 | Skeleton rows (59, 134–136) and an empty state (71–81, 138–150). A refused code map is a card with the service's sentence (60–68). Errors are toasts (47) | `MapView.tsx` as cited | choice | code |

### Convergence

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| CO1 | **No list pane.** The threshold slider and its hint (42–56), then the findings as cards (93–126) | `ConvergenceView.tsx` as cited | drift: a list of findings without a list pane | code |
| CO2 | Nothing to collapse | — | drift (follows CO1) | code |
| CO3 | Nothing to resize | — | drift (follows CO1) | code |
| CO4 | An entry opens **the reader**, a wide 52 rem drawer over the side bar | `App.tsx:863`; `Reader.tsx:13–28`; `ui.tsx:639` | choice (U43: wide enough for a line of source). Its cover over the side bar is F10's drift | to look at: an entry opened at 1280 px with the side bar open |
| CO5 | F4 | — | same | code |
| CO6 | F5 | — | same | code |
| CO7 | The panel and the side bar | `App.tsx:627` | choice (DOCK1a) | code |
| CO8 | The slider's own arrows | `ConvergenceView.tsx:45–49` | — | code |
| CO9 | Nothing. The threshold is 0.75 again on every visit | `ConvergenceView.tsx:30` | drift (as QU9) | code |
| CO10 | One column at every width. Below 768 px the side bar covers (F9) | — | same | to look at: 680 px |
| CO11 | *comparing…* with skeleton rows (58–66). An empty state that lowers the threshold, or says it is the floor (69–81). A refetch at reduced opacity (93). Errors are toasts (36) | `ConvergenceView.tsx` as cited | choice (U42) | code |

### Search

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| SR1 | **No list pane.** The query box, *local only*, and the hits | `SearchView.tsx:41–129` | drift (as CO1) | code |
| SR2 | Nothing to collapse | — | drift (follows SR1) | code |
| SR3 | Nothing to resize | — | drift (follows SR1) | code |
| SR4 | A hit opens the reader drawer | `SearchView.tsx:109`; `App.tsx:863` | as CO4 | to look at (CO4's) |
| SR5 | F4 | — | same | code |
| SR6 | F5 | — | same | code |
| SR7 | The panel and the side bar | `App.tsx:627` | choice (DOCK1a) | code |
| SR8 | The box takes focus on arrival. No key moves through the hits | `SearchView.tsx:49` | gap (SE8) | code |
| SR9 | Nothing. The query and *local only* reset on every visit | `SearchView.tsx:22–23` | drift (as QU9) | code |
| SR10 | One column | — | same | to look at: 680 px |
| SR11 | While fetching, a line of words (70) where every other list shows skeleton rows. An empty state in the answering tier's words, with the way to Convergence (74–84). The count, and whether it was capped (98–102). Errors are toasts (27) | `SearchView.tsx` as cited | the words while loading are drift from platform-ux §4 *Loading*, with no reason written. A search refetches as the person types, so a skeleton there may jump: the window decides | to look at: a search typed at 1280 px |

### Settings

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| ST1 | **Its domains as a list at the page's left**, 11 rem wide: ten on a shell and four in a browser. The list sits under the page header and stays in place while a domain scrolls, from 768 px up | `SettingsView.tsx:95–98, 111–133`; `settings/domains.ts:47–61` | a list at its left is a choice (D75; platform-ux §5 *Settings*). The page draws it inside the main area, so it has none of the rail's behaviour: drift | code |
| ST2 | Never collapses | `SettingsView.tsx:111` | drift | code |
| ST3 | Never resizes: 11 rem | `SettingsView.tsx:111` | drift | code |
| ST4 | A domain opens beside the list, one at a time. A door may name a part of the domain, brought into view once drawn (U72) | `SettingsView.tsx:74–92, 136–144` | same in shape as SE4 | code |
| ST5 | F4 | — | same | code |
| ST6 | F5 | — | same | code |
| ST7 | The panel and the side bar | `App.tsx:627` | choice (DOCK1a) | code |
| ST8 | Tab through the domains, and no arrow keys | — | gap (SE8) | code |
| ST9 | The domain, across visits and relaunches. Not the part a door named | `App.tsx:76–80, 109–110, 321–326` | same in shape as SE9 | code |
| ST10 | **Below the viewport's 768 px the list stacks above the domain** at the column's full width and stops staying in place: ten rows over the domain on a shell | `SettingsView.tsx:111, 113` | drift. It is the arrangement `App.tsx:635–639` records rejecting for the activity bar (*a 276px-tall column of icons ABOVE the content*) | to look at: 680 px, English and 中文 |
| ST11 | **A machine domain draws nothing until its first answer**: Agents & accounts (`settings/AgentsDomain.tsx:106`), Permissions (`settings/PermissionsDomain.tsx:41, 82`) and Plugins (`settings/PluginsDomain.tsx:44`). Errors are toasts | as cited | drift from platform-ux §4 *Loading*: a first load shows skeleton rows | to look at: each machine domain opened for the first time |

### Plugins, as it stands today

Plugins is one domain of Settings (D64, D101, D103), and PLUGUI1 makes it a view on this frame. Its rows
are Settings' except where they say otherwise.

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| PL1 | One row per plugin inside the Plugins domain, followed by the offers the install carries and the kit | `settings/PluginsDomain.tsx:27–227`; `settings/PluginOffers.tsx`; `settings/PluginKit.tsx` | a Settings domain, until PLUGUI1 | code |
| PL2–PL3 | Settings' list (ST2, ST3) | — | as ST2, ST3 | code |
| PL4 | **No page of its own.** A trial's report and an update's plan open under the plugin's row | `settings/PluginsDomain.tsx:36–40` | the reason PLUGUI1 exists | code |
| PL5–PL8 | As ST5–ST8 | — | as ST5–ST8 | code |
| PL9 | A trial's report and an update's plan last while the domain is shown | `settings/PluginsDomain.tsx:36–40` | no reason written | code |
| PL10 | As ST10 | — | as ST10 | to look at (ST10's) |
| PL11 | Nothing while the catalogue loads | `settings/PluginsDomain.tsx:44` | drift (ST11) | to look at (ST11's) |

### The monitor window

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| MO1 | **The rail**, live only with no ended section, at a fixed 17.5 rem | `work/MonitorWindow.tsx:82–104` | same organism as SE1 (U69, U70) | code |
| MO2 | **Hidden below the viewport's 1024 px, with no way back** | `work/MonitorWindow.tsx:88` (`max-lg:hidden`) | drift from SE2's strip. The running sessions are the tiles, so what is lost is the way to one of them | to look at: the monitor at 900 px |
| MO3 | Never resizes | `work/MonitorWindow.tsx:88` | drift | code |
| MO4 | Choosing a session scrolls to its tile, and a tile detaches its session into a window of its own | `work/MonitorWindow.tsx:100–101, 140` | choice (D55 §b: read-only) | code |
| MO5 | No side bar | `SecondaryWindowRoot.tsx:15–17` | choice (D55 §b: a secondary window is a utility with a native frame) | code |
| MO6 | No panel: each tile holds its stream | `work/MonitorWindow.tsx:158–196` | choice (D55 §b) | code |
| MO7 | No strip and no toggles | `SecondaryWindowRoot.tsx:15–17` | choice (D55 §b) | code |
| MO8 | None of the frame's keys, since the window has no key handler | `SecondaryWindowRoot.tsx:39–47` | no reason written | code |
| MO9 | The shell keeps the window's geometry (D55 §b). The page keeps nothing | — | choice | code |
| MO10 | The rail is hidden below 1024 px (MO2), and the tiles fit at 26 rem each | `work/MonitorWindow.tsx:88, 126–131` | as MO2 | to look at: 900 and 1400 px |
| MO11 | Skeleton rows (107–108), an empty state (109–116), and the driver stopped said in the header (76–78). Errors are toasts (50). **Its title wears `text-h3`, which no token defines** (`tokens.css:149–162`), so the heading is drawn at the body's size. `tokens.test.ts` scans for raw sizes only, so it does not catch this | `work/MonitorWindow.tsx:71` | drift | to look at: the monitor's header, both themes |

### A detached session's window

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| DE1 | No list: the window holds one session | `work/DetachedSession.tsx:37–119` | choice (D55 §b) | code |
| DE2–DE3 | — | — | n/a | code |
| DE4 | The attended session's components, read-only, with the timeline kept in the column | `work/DetachedSession.tsx:82–90` | choice (REV3: no dock in this window) | code |
| DE5 | No side bar | `work/DetachedSession.tsx:82` | choice (REV3) | code |
| DE6 | **The console and its streams sit in a fixed 14 rem well**, which cannot be resized or hidden | `work/DetachedSession.tsx:98–110` | drift from SE6. The `OutputPanel` molecule exists and is not used here | to look at: a detached conversation with a long console |
| DE7 | No strip and no toggles | `SecondaryWindowRoot.tsx:15–17` | choice (D55 §b) | code |
| DE8 | None | — | as MO8 | code |
| DE9 | The shell keeps the window's geometry. The page keeps nothing | — | choice | code |
| DE10 | No narrow rule | — | no reason written | to look at: a narrow detached window |
| DE11 | Skeleton rows (58–60). A session this machine no longer holds is a state of its own (62–72). A teammate's session says where it runs (112–116) | `work/DetachedSession.tsx` as cited | choice | code |

### A browser

| # | What it has and how it behaves | Decided at | Verdict | Look |
|---|---|---|---|---|
| BR1 | Whatever the view draws, with no frame: no Sessions on the activity bar, and each view alone in the column | `App.tsx:646, 684–713` | choice (D47 §4; DOCK1a: the regions hold this machine's sessions) | code (Playwright holds the absence) |
| BR2–BR3 | As each view | — | as the view | code |
| BR4 | The same drawers as a shell's | — | as the view | code |
| BR5–BR7 | No side bar, no panel, no toggles | `App.tsx:587, 623, 684` | choice (D47 §4) | code |
| BR8 | Ctrl+K opens the palette. F1, Quick Ask and the region keys do nothing | `App.tsx:221, 383` | choice | code |
| BR9 | The scope, the language, the theme and Settings' domain. Nothing of the frame | as SE9 and F8 | choice | code |
| BR10 | As each view | — | as the view | code |
| BR11 | As each view. Settings offers its four domains (`SettingsView.tsx:95`) | — | choice (D47 §4) | code |

## 3. What disagrees, in one list

Each finding names its rows. §4 says what the model makes of it.

- **A1 — Only Sessions has a list pane.** Quests, Projects, Search and Convergence draw their lists as the
  page. Settings draws its own list, which has none of the rail's behaviour (QU1–3, PR1–3, SR1–3, CO1–3,
  ST1–3).
- **A2 — A record opened from a list is a modal drawer on four views**, over the side bar and the panel
  that DOCK1a put on every view: a quest, an ask, a knowledge entry (QU4, SR4, CO4, F10). Below 768 px it
  may open under a full side bar.
- **A3 — Only Sessions remembers what was open in it.** Settings remembers its domain. Quests, Projects,
  Search and Convergence forget their selection and their filters (QU9, PR9, SR9, CO9).
- **A4 — Viewport breakpoints lay out a column that the side bar narrows**: Overview's and Projects' two
  columns, and Map's detail column (OV3, PR10, MA3).
- **A5 — Settings stacks its list above the domain below 768 px** (ST10), the arrangement D56 rejected.
- **A6 — Loading drawn as nothing, or as the empty state.** Settings' machine domains and the Plugins
  catalogue draw nothing (ST11, PL11). The rail's strip draws nothing (SE11). Sessions' main area says
  *Nothing attended* while the list loads. The timeline off Sessions points at a rail that is not on the
  view (SE11).
- **A7 — No list in the application moves by arrow keys** (SE8, QU8, ST8, SR8).
- **A8 — The secondary windows have fixed regions.** The monitor's rail disappears below 1024 px with no
  way back (MO2–3), and a detached window's console cannot be resized or hidden (DE6).
- **A9 — The monitor's title wears an undefined type token** (MO11).
- **A10 — Two storage guards** (MA9).
- **A11 — A stale comment and an unused prop in `LayoutToggles`** (F6).
- **A12 — A strip the window drew offers no open** (SE2). On Sessions, below 1024 px, it leaves an ended
  session and the search out of reach. On a view whose list is its only way in, such as Settings, it would
  leave the whole view out of reach.
- **Not drift, but a door VS Code has and Daoris does not:** pressing the current place on the activity
  bar does nothing (F1).

## 4. What each finding becomes

The model is `docs/2026-10-01-frame-model-design.md` (D118). Its §6 lists the rows that build it.

| Finding | Becomes |
|---|---|
| A1 | Model §2–§3a: one `ListPane` on every view that has a list, Sessions' rail included (FRAME1b; views in FRAME1d–g) |
| A2 | Model §3d: a record opened from a list opens in the view's main area, and the drawer keeps forms (FRAME1d, FRAME1f) |
| A3 | Model §3f: each view remembers its selection, its list's closing and width, and its list's filters (FRAME1c; each view's own in FRAME1d–g) |
| A4 | Model §3b: the main area lays out by its own width, never the viewport's (FRAME1c) |
| A5 | Model §3a, §3g: a list with no room beside the main area becomes a strip that opens over it, and never stacks (FRAME1b, FRAME1g) |
| A6 | Model §3h: every region draws its own loading, empty and error states, and never nothing (FRAME1c, FRAME1g) |
| A7 | Model §3e: ↑, ↓, Home, End and Enter in every list (FRAME1b) |
| A8 | Model §4: the monitor's rail is a `ListPane`, and a detached window's console is the `OutputPanel` (FRAME1h) |
| A9 | FRAME1h: a named token |
| A10 | FRAME1c: `lib/stored.ts` |
| A11 | FRAME1b, which rewrites the toggles |
| A12 | Model §3a: a strip the window drew opens its list over the main area, which amends FRAME6's rule, and a list becomes a strip by room rather than below a fixed 1024 px (FRAME1b) |
| F1's missing door | Model §3a: pressing the current place toggles its list (FRAME1b) |

## 5. For the parent to look at

The window settles these. Each is one look, taken with `npm run desktop -- shot` (`--window monitor` or
`--window session:<id>` for the secondary windows). A width is not an instrument yet (UX5's *What the
instruments need*), so the window is sized as UX5 sized it. Every look is in both themes, and in both
languages where the row names words.

1. **F9 / SE10 / OV10 / SR10 / CO10**: Sessions, Overview, Search and Convergence at 680 px, with the side
   bar closed and then open (full).
2. **F10 / QU4 / QU10**: a quest's drawer at 1280 px with the side bar open on Ask Daoris, then at 680 px
   with the side bar full. Does the drawer open under it?
3. **CO4 / SR4**: an entry in the reader at 1280 px with the side bar open.
4. **OV3 / PR10**: Overview and Projects at 1280 px with the side bar open at 45%: how wide is each card?
5. **MA3 / MA10**: Map at 1280 px with the side bar open (the canvas's width beside its 20 rem detail),
   and at 680 px.
6. **SE2**: Sessions at 900 px. Can an ended session be reached?
7. **SE11**: a relaunch into Sessions with a session attended. Does *Nothing attended* show first? Then
   Overview with the side bar on Timeline and nothing attended (the sentence names a rail).
8. **ST10**: Settings at 680 px, English and 中文. How far down does the domain start?
9. **ST11 / PL11**: the first open of Agents & accounts, Permissions and Plugins after a start. Is there a
   blank?
10. **SR11**: a search typed at 1280 px. Does the words-only loading line read well, and would a skeleton
    jump?
11. **MO2 / MO10 / MO11**: the monitor at 900 and 1400 px, both themes. The rail, and the header's type
    size.
12. **DE6 / DE10**: a detached conversation with a long console, and the same window narrow.
