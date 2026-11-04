# The desktop app and its window — how it works now

## What it does

The desktop is one .NET process on Shenora's kit. It sets up the home and the machine log, starts or adopts the local
host, runs the driver loop in-process, and opens windows on the kit's embedded Chromium. Every window shows the HTTP
host's own web bundle, so the desktop and a plain browser run one UI. The page reaches the machine only through bridge
modules. .NET judges the machine's state once and pushes it, with its data, to every window. The page draws it, words
it in the viewer's language and sends the person's presses back. The frame is the same on every view: places in an
activity bar, a menu strip, a list pane, a main area, a right side bar, a bottom panel and a status bar. The same
executable, started with a browser argument, is Daoris's own browser, which sessions drive over CDP.

## Its parts

`Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `App/` is `src/Daoris.Desktop/Daoris.Desktop.App/`, and
web paths are under `src/Daoris.Web/src/`.

| Unit | File | Its job |
|---|---|---|
| `Program` | `App/Program.cs` | Composes the shell: the browser argument first, home, log, host start, engine, modules, dispatcher middleware |
| `MainForm`, `SecondaryForm` | `App/MainForm.cs`, `App/SecondaryForm.cs`, `App/SecondaryWindowHost.cs` | The frameless main window; framed secondary windows (`monitor`, `session:<id>`) |
| `DriverModule` and the route classes | `Modules/Bridge/DriverModule.cs`, `Modules/Bridge/Routes/<Domain>Routes.cs` | `DAORIS.DRIVER`: one handler class per domain, each route `[DriverRoute]`, each press `[Publishes]` |
| Route-class helpers | `Modules/Bridge/Routes/GitRefDesk.cs`, `GitAnswers.cs`, `LinesWires.cs`, `WorkWires.cs`, `SweepWires.cs`, `TreesTidyDesk.cs`, `WorkflowAnswers.cs`, `DriverStateAnswer.cs`, `HelpScreenDoors.cs`, `HelpMachineDesk.cs`, `HelpProposalFacts.cs`, `TreesLandDesk.cs`, `TreesHandDesk.cs`, `TreesReviewWires.cs`, `SessionsMoveDesk.cs`, `SessionsAskDesk.cs`, `SessionsWaitDesk.cs`, `SessionsRecordDesk.cs`, `SessionsGroupsDesk.cs`, `SessionsGroupsWires.cs` | A route class keeps its marks and one-line doors (so callers and the page's bridge file are unchanged); the bodies and wire shapes of git's ref acts, the lines, work and sweep answers, the clean-up and sync, the workflow answers, the driver's state, Ask Daoris's Apply doors, room judges and proposal facts, the landing and hand-off presses with the review and preview wires, and a session's moves, asks, waits, record and groups sit in types of their own beside it (D231) |
| `SessionWords` and its parts | `Modules/Loop/SessionWords.cs`, `HeldWordsQueue.cs`, `HeldWordsDisk.cs`, `HeldSay.cs`, `WordsAnswer.cs`, `WordsRead.cs`, `WordsReach.cs`, `WordsQueueEdits.cs`, `WordsShown.cs` | The person's words to a session: `SessionWords` judges and says them; the words held for a record to end wait in `HeldWordsQueue` (its pump) and in the held file `HeldWordsDisk` writes whole; the record read, the reach a word would have, the take-back and move of a waiting word and the lines shown in the conversation each have a type of their own |
| `AttentionStateSlice` helpers | `Modules/Loop/AttentionWaits.cs`, `AttentionFacts.cs`, `AttentionSessionRows.cs`, `AttentionMachineRows.cs`, `AttentionJson.cs` | The waits read off files and memory, the judge's input record, and the session and machine groups of `NeedsAPerson`'s rows, in the order it judges them; the slice and `NeedsAPerson` keep their doors |
| `StatePublisher` | `Modules/Platform/StatePublisher.cs` | Innermost middleware: `STATE_CHANGED` after a press answers; `UNDER_WAY` while presses run |
| `StateSlices` and the 16 slices | `Modules/Platform/StateSlices.cs`, `Modules/Loop/*StateSlice.cs` | Machine state judged in .NET and pushed (`STATE_PUSH`, `STATE_MANIFEST`, `STATE_SLICE`) |
| `DriverLoop`, `HostSupervisor` | `Modules/Loop/DriverLoop.cs`, `Modules/Loop/HostSupervisor.cs` | The in-process loop that owns the slices; the host started or adopted, handed the person key |
| `DriverLoop`'s parts | `Modules/Loop/LoopState.cs`, `LoopRun.cs`, `LookTold.cs`, `LoopComeUp.cs`, `LoopAddress.cs`, `HomeHold.cs`, `TickShown.cs`, `TickSignatures.cs`, `TickErrors.cs`, `AttentionTold.cs`, `OutputTold.cs`, `PipeDecisionAdmission.cs` | What the run brings up and the loop's doors read, in one state type written whole with no lock; the address and pipe named in the home under their own lock with the stop's cancel; one run per loop, declaring what it brings up in the order it is let go in reverse; what each look keeps and tells; what the come-up hears; the home's lease waited for; the tick's shapes, signatures and errors; the attention and console events; the supervisor's decisions' admission. The loop keeps one-line doors, the start and the stop (D231) |
| `PersonRoutes` | `Modules/Bridge/Routes/PersonRoutes.cs` | `PERSON_KEY` and the person's confirmations, answered to the page |
| `InstancePipe`, `PipeCensus`, `RouteMarks`, `PipeConfirmations` | `Modules/Platform/` | A terminal's verbs into the running Daoris; each route classed `Open`, `Person`, `Window` or `Role` by its mark, read at start |
| `InstancePipe`'s parts | `Modules/Platform/PipeState.cs`, `PipeWaitingKeep.cs`, `PipeListener.cs`, `PipeConnection.cs`, `PipeAdmission.cs`, `PipeAsking.cs`, `PipeTakeBacks.cs`, `PipeRestore.cs`, `PipeReserved.cs`, `PipeLimits.cs` | The presses and connections under one gate; the waiting records under their own lock, never taken inside it; the listening, a connection, a request's admission, a person's press from its reservation to its ending, `CANCEL`/`RESULT`/`WITHDRAW`, the restart's restore. The pipe keeps the wire's words, the serve, the close and the run |
| `WindowsModule` | `Modules/Windows/WindowsModule.cs` | `DAORIS.WINDOWS`: `OPEN`, `OPEN_BROWSER`, `SET_THEME`, `SET_BADGE`, `SET_TITLE`, `SET_NOTICE_WORDS` |
| Notifier | `App/SessionNotifier.cs`, `Modules/Windows/SessionBalloon.cs`, `MainWindowScopes.cs` | OS balloons in the window's language; which window a click brings forward |
| Taskbar | `App/TaskbarBadge.cs`, `Modules/Windows/WaitingCount.cs`, `WaitingBadge.cs` | Each main window's count on its button and in its title |
| Browser | `Modules/Browser/` (`BrowserModule`, `EngineBrowser`, `EdgeBrowser`, `InAppBrowser`), `App/EngineBrowserHost.cs`, `App/BrowserProcess.cs` | Daoris's own browser, its profile, favorites and CDP endpoint |
| `TerminalModule`, `LogModule` | `Modules/Windows/TerminalModule.cs`, `Modules/Platform/LogModule.cs` | The person's own shells; the page's report into the machine log |
| `App` | `app/App.tsx` (100 KB: outline only) | The window's frame: places, strip, menus, views, dock, drawers |
| Frame regions | `work/frame/` (`frame.tsx`, `ViewFrame`, `ListPane`, `ViewMain`, `RightDock`, `placements.ts`) | Activity bar, strip, status bar, list pane, main area, side bar and panel, and where views stand |
| `WorkFrame` | `work/WorkFrame.tsx` (68 KB: outline only) | Sessions: the rail, the attended session's page, its head, waits and review, composed from its hooks |
| `WorkFrame`'s hooks | `work/frame/useFrameLayout.ts`, `useDockViews.ts`, `layoutMemory.ts`; `work/head/useAttendedSession.ts`, `useAttendedRecord.ts`, `useHeadAsks.ts`, `useAttendedHead.tsx`; `work/conversation/useAttendedBox.ts`, `useWords.ts`, `useBoxWords.ts`; `work/start/useNewStart.ts`, `StartPane.tsx`; `work/review/useSessionLanding.ts`; `work/waits/useAttendedWaits.ts` | One part each of what the frame reads and presses (D231): the columns and regions, the attended session and its record, its box and words, a new conversation, the header and its questions, the landing, and what waits on the person; each that reaches the bridge is a named organism of the presentational boundary |
| Command table | `menus/commands.ts`, `menus/shortcuts.ts`, `menus/ContextMenu.tsx`, `work/strip/` | Every verb once; the menu bar, palette, command center, keys and the right-click menu |
| Notifications | `notifications/`, `work/attention/` | The bell, the view and its four groups; *What needs you* |
| Bridge and state hub | `bridge/<domain>.ts`, `bridge/state/stateHub.ts`, `app/ShellSignals.tsx`, `data/api.ts` | The page's calls; scope invalidation; pushed slices fed to the query cache; the person key on writes |
| Design language | `tokens.css`, `design/ui.tsx`, `design/theme.ts`, `design/phases.tsx` | Tokens as the Tailwind theme; the atoms (`Pill`, `Drawer`, `Toasts`, `SettingList`, `FormGrid`, `PhaseList`) |
| Flows and canvas | `design/Flow.tsx`, `design/RankedFlow.tsx`, `design/flowLayout.ts`, `design/canvas/` | The flowchart primitive; one viewport for every graph (`Canvas`, `PositionedCanvas`, `SuppliedCanvas`) |
| Translation | `locales/<lang>/<area>.json`, `locales/i18n.ts`, `languages.ts`, `authored.ts`, `glossary.json`; `scripts/i18n-check.mjs`, `scripts/names-check.mjs` | Catalogues per shipped language, authored text, the glossary and its checks |
| Views | `overview/`, `quests/`, `projects/`, `map/`, `knowledge/`, `agents/` (`setup/`, `roles/`), `plugins/`, `settings/domains.ts` | Each place's view, list and pages |
| Setup guide | `help/setup.ts` | Get started's steps, read off the machine |

## Its rules

**The host and the bridge**

1. Every window renders the HTTP host's `wwwroot` bundle on the kit's Chromium at `https://daoris.localhost/`. The same
   bytes serve a plain browser. There is one UI and no second frontend. (from D38, D85, D92)
2. The page reaches the machine only through bridge modules: `DAORIS.DRIVER`, `REGISTRY`, `REMOTES`, `BROWSER`,
   `WINDOWS`, `TERMINAL`, `UPDATE`, `LOG`. They are the shell's alone. No machine-local fact becomes an HTTP route. (from D47, D50)
3. A driver route is a `[DriverRoute]` handler in `Routes/<Domain>Routes.cs`, named like the page's `bridge/<domain>.ts`
   that sends it. A refusal the person can act on is answered as data with a code the page words. The mark carries the
   route's pipe class and, for a person's or a role's press, its drain. Beside it, `[Owner]` says what a person's or a
   window's route marks owner-set, `[ClassedBy]` names a payload's look form, and `[NotAlready]` says why a setting is never
   already; a person's press says its grounds (`[Ground…]` or `[Timeless]`) and the values its card names (`[Says]`). Another module's routes are marked on its class (`[ModuleRoute]`, `[WindowModule]`). The pipe's census and owner
   rows are read from the marks at start, and a route whose marks are incomplete fails that first use. (from D169, D182, D231)
4. A press declares the scopes it changed with `[Publishes]`. `StatePublisher` raises `STATE_CHANGED {scopes, sessions?}`
   after a successful answer, including a refusal answered as data. No handler raises it by hand. A read declares nothing. (from D169)
5. The dispatcher runs `RefusalLog`, `UiStallWatch`, `OffTheWindow`, then `StatePublisher`. A route leaves the window's
   thread at its first wait, so no git fan-out runs on it. (from D169, D56)
6. .NET judges each slice of machine state once and pushes it with its data. There are 16 slices. A partition carries
   `{epoch, seq, status, data}`, a delta names its `baseSeq`, and a periodic manifest repairs a dropped push. (from D182)
7. A window subscribes, asks its snapshot (`STATE_SLICE`), then applies newer pushes. A late push never moves it back.
   More windows never mean more judges or more git. (from D182)
8. A pushed projection is allow-listed. It carries no credential, no arbitrary payload, and a machine path only in a
   desktop-only slice. Diffs, transcripts, graphs and search results stay on demand. (from D182)
9. The domain's verdicts live in .NET: grouping, attention, eligibility, milestones, usage curves. The page keeps
   rendering, translation, selection, sorting and view state. Its store is the query cache fed by pushes. (from D182, D169)
10. Each declared press is said `UNDER_WAY` from dispatch to answer, in every window. A plain browser has no bus and
    polls. (from D169)

**The person's doors and their key**

11. The person key is a random 256-bit value minted for each host start. The shell hands it on the host's first input
    line and holds it in memory. It is never in a file, an environment, a URL, page storage or the log. (from D156)
12. The page asks `PERSON_KEY` and sends `Daoris-Person` on writes only. No session ever holds the key. A host the shell
    adopted has none, and the window says so. (from D156)
13. Reads are open. An agent's doors are judged as an agent's. The person's doors need the key. An unclassed write is
    refused. A refusal is a 403 with a code, never a 500. (from D156)
14. A terminal's press of a `Person` route waits on its card in *Notifications* for the person's Confirm, default no.
    Its two minutes start when the card is shown. The route's name and raw values sit under *Details*. (from D156, D196)
15. A standing confirmation the person grants in the window lets a terminal outside Daoris's agents stop or finish a
    session and hold or resume a repository with no card. Each use is logged and can be revoked at once. (from D201)

**The windows**

16. The main window is frameless. The app strip is its title bar, and the caption buttons are painted from
    `ChromePalette`, a copy of the tokens. Secondary windows keep a native frame and load the same bundle. (from D56, D55)
17. The viewer picks System, Light or Dark. The page sets it on the engine. The shell keeps it (`KeptScheme`) for the
    next start's first frame. Daoris's browser follows the OS. (from D66, D41)
18. A later launch opens a new main window of the running instance. The application ends with its last main window.
    One driver loop runs per home. (from D171, D104)
19. Each main window wears its own workspace's waiting count on its taskbar button and in its title. A notification's
    click brings forward the window that holds its subject. (from D196, D108)
20. The shell notifies and decides nothing. A balloon shows when no window has focus, worded from the words the page
    hands it in its language, with the English catalogue as fallback. (from D196, D142)
21. A render that throws is caught by `PageBoundary`. It offers a reload of the page alone and logs `page.error` with
    the view's name and a five-frame stack. (from D217)

**The frame**

22. Nine places, in order: Overview, Sessions, Quests, Repositories, Map, Knowledge, Agents, Plugins, and Settings at
    the foot. Sessions, Agents and Plugins are shell-only. A tenth place needs its own decision. (from D150, D66)
23. There is one frame and no mode switch. The list pane and main area belong to the view. The activity bar, strip,
    status bar, right side bar and panel belong to the frame. (from D66, D118)
24. A record opens in the main area of the view whose list holds it, through one opener. A form is a drawer, and every
    overlay opens above a full side bar. (from D118)
25. One list pane on every view: a header with ＋ and ⋯, per-view resize, a 56 px strip when closed, Ctrl+B. Its closing,
    width, chosen item and filters are remembered per view. (from D118)
26. The side bar and panel hold movable views: *Notifications*, Timeline, Review, Workflow, At work and Ask Daoris on
    the right, Console and Terminal in the panel, until the viewer moves them. (from D118, D196, D96)
27. A page is one column. `ViewMain` caps nothing, no block keeps a measure of its own, and the main area lays out by
    its own width, never the viewport's. A cap, if ever set, is one `em` value on `ViewMain`. (from D141, D118)
28. The menus are *Workspace · Edit · View · Go · Run · Terminal · Help*. One table, `menus/commands.ts`, names every
    verb once. The menus, palette, keys and shortcuts drawer all read it. (from D152)
29. A record's act in a menu is enabled exactly when its header offers it. Keys follow VS Code where the meaning is the
    same. No act that ends or removes something has a key. Ctrl+1–8 open the places. (from D152)
30. The right-click menu is the page's own, offering the surface's acts from their one owner plus text, link and code
    groups. The engine's menu stays in text fields and is suppressed elsewhere. (from D138)
31. *Notifications* is one view, opened by the title bar's bell. Its groups are presses waiting, *What needs you*,
    messages, and *Done and failed*. There is no *Confirm all*. A *What needs you* row is answered, never dismissed. (from D196)
32. .NET judges *What needs you*. A row acts where one press is safe. No row starts a process to find a fact out.
    Overview keeps one summary line per group. (from D150, D181, D196)
33. What announces depends on what waits and how urgent it is. An agent's message never reaches the desktop. The notify
    switch governs every announcement except a press's. (from D196, D188)
34. Every surface is reached from the frame. It adds no new place. It needs a door from *What needs you* where it
    waits and a verb in the command table; an Ask Daoris door and a terminal twin only where one is wanted. (from D185,
    D150, D233)
35. A setting has one home, on the thing it is about. Elsewhere it is read-only with a door. Settings holds only the
    machine's and the person's domains. (from D150)
36. The scope is always named in the command center and status bar. The word is *workspace* / 工作区 everywhere. (from D75)

**A session's page**

37. A head shows the state, the title and two facts. A session's title takes one line; a quest's or an ask's takes two.
    The acts hold still. The id moves to ⋯ and *Details*. (from D152, D162)
38. Every wait shows in the head and in the foot's panel above the composer. Both places get the same press from the
    frame, and a press in one disables the other. One pure rule (`work/waits.ts`) says which waits stand. (from D168, D185)
39. There is no text editor. A file's preview is a read-only side-bar tab from the session's tree (`SESSION_FILE`).
    It shows at most 256 KiB, and a path outside the tree is refused. (from D55, D111)
40. *New conversation* starts from the person's words in a repository they pick, with tree, agent and account chips.
    `START_CHAT` carries a request id, and one id never starts twice. (from D162)

**The design language**

41. Warm paper, thin ink, one bronze accent. The accent marks what is interactive and is never a status. The status
    palette is computed and validated in both themes, and a status is never shown by colour alone. (from D41)
42. One dense scale of seven named type steps, with a 13 px body. Every size is a step and every colour a declared
    token, held by `tokens.test.ts`. Hit targets are at least 28 px. No region varies by viewport. (from D56, D41, D185)
43. The libraries are headless, under the language: Tailwind v4 themed by `tokens.css`, Radix, lucide, TanStack Query,
    react-i18next, Storybook and Playwright. (from D42)
44. A card's rows share one label track. A refusal is said where its action started; a toast is used only where that
    place is gone. A wait lists its phases with their times. An unjudged slice draws as loading. (from D185)

**Flowcharts and the canvas**

45. One primitive draws every flow (`Flow` over `RankedFlow`). It is laid out in layers, deterministically, from the
    record and never guessed. By the pane's width it runs left to right, top to bottom, or as a rail. (from D186)
46. Edges are ink, never a status hue. The nodes are one ordered list with one Tab stop. The edges are one
    `aria-hidden` drawing. (from D186)
47. Every graph stands on one canvas: a viewport with pan and zoom, a minimap only past twice the viewport, and a list
    below 36rem. No feature moves nodes or saves positions. (from D224)
48. A placed world is HTML that CSS lays out and the canvas measures. A positioned world arrives with coordinates.
    One engine, Daoris's own, serves every feature, chosen by measurement over React Flow. (from D224)

**Translation**

49. The chrome translates through `locales/<lang>/<area>.json`. The languages offered are the folders shipped (`en`,
    `zh`), with `en` as the source. `i18n:check` holds every catalogue to it. Keys are structural. (from D222, D42, D61)
50. Authored words need no language. Each is a string or a tag-to-text map, shown in the viewer's language, else the
    base language, else as written (`locales/authored.ts`, twin of the driver's `AuthoredText`). (from D222)
51. Shipped data keeps one English text. Its words are catalogue keys by id, and a census holds every id in every
    catalogue. (from D222)
52. A name is a UI element, designed in each language. `glossary.json` is the authority. `names:check --strict` runs in
    the web build for conformance, form and doors; the budgets only report. (from D116, D54)
53. Daoris's own lines in a record are codes the page words in the reader's language. Someone's words show as written.
    The window's language is the viewer's; the terminal stays English. (from D142)

**Daoris's browser**

54. The browser is this executable started with `--daoris-browser` first. It runs in Chromium's own windows, in a
    process of its own, on a profile at `<home>/browser/engine`. The shell stops it once the shell is gone. (from D99, D85)
55. Sessions drive it through an MCP server a plugin declares with `${browser}`, its loopback CDP endpoint. The
    person's Edge on a Daoris profile stays an option. Daoris keeps sign-ins across a restart itself. (from D78, D84)

**The setup guide and the views**

56. A first start opens Settings → Get started while an agent with an account, Daoris's own agent or a repository is
    missing. Each step reads its state off the machine and names its screen and its command. (from D97, D50)
57. Settings has nine domains: Get started, Appearance, AI features, This Daoris, Driver, Tools, Git, Browser, Machine
    log. A domain is a `<Name>Domain.tsx` and one row in `settings/domains.ts`. (from D150, D75)
58. Agents has two modes, Agents and Setup. Setup is the tree of places (machine, workspace, repository), each a page
    with a head and three folded sections. It stays dark until `daoris.agents.setup` is `on`. (from D221, D150)
59. An agent's page heads in one line. Each account is one row with one act by its state. An account is read only when
    the person presses or something has run, never on a timer. (from D152, D150)
60. Repositories lists by workspace. A repository's tabs are Details, Branches, History, Changes, Integration, Workflow
    and Setup; a workspace's are Details, Branches, Workflow and Setup. (from D150, D147)
61. Knowledge is one place with two modes, Search (the default) and Convergence. The Map is a read-only view of the
    workspace's repositories and what moved between them, plus the code map, drawn on the canvas. (from D150, D67, D224)
62. Doctrine is never written from the window; it shows the command to run. (from D31)

## Where a change goes

- **A new page piece goes in its place's folder** (`overview/`, `quests/`, `projects/`, `map/`, `knowledge/`, `agents/`,
  `plugins/`, `settings/`, `help/`, `work/`, `workflow/`). Its test and story sit beside it. Frame regions go in
  `work/frame/`, the strip in `work/strip/`, atoms in `design/ui.tsx`, flows in `design/` and graphs in `design/canvas/`.
  A folder past twenty sources splits into named scopes (`test/structure.test.ts` with `structure.json`).
- **A new driver route** follows `.claude/knowledge/bridge-presses.md`. It needs the handler and its `[Publishes]`, the
  desktop README's row, the hook in `bridge/<domain>.ts`, its class, drain and owner in its marks (and its wording and grounds
  if `Person`), and its words in both catalogues. The page's route names, `PUBLISHES` and the person's presses' keys are
  generated from the marks into `bridge/generated/routes.ts`.
- **A new pushed slice** follows `.claude/knowledge/pushed-slices.md`: the `StateSlice` owner, its wakes, the
  `DriverLoop.Slices` entry, the page's `SliceSpec` and `ShellSignals`' `PUSHED`.
- **A wait on the person** follows `.claude/knowledge/person-waits.md`; **new words** `translation-parity.md`, plus a
  glossary row for a name. **A movable view** goes in `work/frame/placements.ts`; **a verb** in `menus/commands.ts`.
- **Tests that pin it.** Modules: `DriverModuleRoutesTests`, `StatePublisherTests`, `StateSlicesTests`,
  `PipeCensusTests`, `PipeWordingTests`, `RefusalCatalogueTests`, `ChromePaletteTests`, `WindowsModuleTests`,
  `WaitingBadgeTests`, `SessionBalloonTests`, `EngineBrowserTests`. Web: `test/structure.test.ts`, `design/tokens.test.ts`,
  `presentational.test.ts`, `siblingKeys.test.ts`, `pageMeasure.test.tsx`, `locales/{i18n,names,note,glossary,
  shippedNames}.test.ts`, `menus/commands.test.ts`, `ContextMenu.test.tsx`, `bridge/state/{stateHub,slices}.test.*`,
  `app/pushedState.test.tsx`; `e2e/platform.spec.ts`; `measured/*.spec.ts`.
- **The gates that bite:** `web-types`; `web-rules` (tokens, structure, provider census, presentational, page measure);
  `web-unit`; `web`, whose build runs `i18n-check`, `names-check --strict`, `tsc` and `vite build` before Playwright
  over the HTTP host; `web-measured`; `modules`, `modules-process` and `censuses`; `driver` for Ask Daoris's coverage.
  A story or a mocked bridge is no proof of the installed window.

## Contradictions

1. D55 (two frames, Manage ⇄ Work) vs D66 §1 (one frame): D66 is right; `menus/commands.ts:28` has one list of views.
2. D56 (six domains in the bar) and D66 §1 (a six-item bar with Convergence and Search) vs D150 §2 (nine places): D150 is right, per `menus/commands.ts:28-48`.
3. D75 §1 (menus Daoris · Workspace · Agents · View) vs D152 §1: D152 is right, per `menus/commands.ts:57-66`.
4. D150 §2 (refresh and language move to "the Daoris menu") vs D152 §1 (there is no Daoris menu): D152 is right.
5. D75 §2 and D150 §2 (seven Settings domains, without This Daoris or Git) vs `settings/domains.ts:51` (nine): the code is right.
6. Code vs code: `settings/domains.ts:49` says "eight"; Ask Daoris's twins (`help/places.ts:46`, `Help/Proposals/HelpGoProposals.cs:341`) omit `tools`, so no go opens Settings → Tools. Intent unclear.
7. D150 §6 (*What needs you* leads Overview) vs D196 §5 (Overview keeps a summary): D196 is right (`overview/OverviewView.tsx:79`, `app/App.tsx:1491`). The comment at `work/attention/AttentionBand.tsx:41` is stale.
8. D30 (convergence is the knowledge half's primary view) vs `knowledge/modes.ts:21` (Search is the default) and D40 (Overview is the landing): the code is right.
9. D41 (a right drawer is the single detail-and-form surface; global state sits in the sidebar) vs D118 §3 (a record opens in the main area) and D56 (the status bar): D118 and D56 are right.
10. D41 and D42 (toasts carry the service's sentence verbatim) vs D142, D182 §2 and D185 R3 (coded verdicts worded by the page, said where the action started): the later ones are right. An unknown code still shows the driver's sentence.
11. D42 (TanStack invalidation after every mutation) vs D169 §6 and D182 §7 (one state hub by declared scope, plus pushed slices): D169 and D182 are right (`bridge/state/stateHub.ts`).
12. D169 §3 (an event never says what a thing is) vs D182 §2 (pushed with its data): both run. D182 governs the 16 slices, and `STATE_CHANGED` covers the scopes not yet moved.
13. D78 (a framed WebView2 window of the shell) and D84 (Daoris's UI stays on WebView2; the engine is fetched like a harness) vs D85, D92 and D99: D99 is right (`App/Program.cs:48`; the kit pins CEF, `App/Daoris.Desktop.App.csproj:40`).
14. D85 (the browser renders inside Daoris's windows under Shenora's frame) vs D99 (a process of its own in Chromium's windows): D99 is right (`App/Program.cs:242-247`).
15. D186 §2 and §9 (no pan, no zoom, laid out by hand) vs D224 (one canvas with pan, zoom and a minimap): D224 is right (`design/canvas/Canvas.tsx`, `design/FlowCanvas.tsx:1-3`).
16. D185's and D186's heads say "proposed"; their notes record acceptance on 2026-10-20: the notes are right.
17. D97 (six steps, with "what is driven" and "how work lands" apart) vs `help/setup.ts:5` (five steps; both are one `setUp` step): the code is right.
18. D116 (names and budgets in English and Chinese only) vs D222 §3 (any number of catalogues): D222 is right. `names:check` holds every catalogue and each `term.<tag>`.
19. D221 (Agents → Setup is the one set-up experience) vs `agents/modes.ts:40` (dark unless `daoris.agents.setup` is `on`): the code is today's truth, and the light-up is still open.

## Open

- **CANVAS1g**: the canvas in the installed window, with FLOWCHART2h.
- **CANVAS1e/f remainders**: culling and the dot layer, the map's list view, the selection-gone notice; for the code
  map, lines routed around boxes, a cycle across folders condensed, one folder folded alone.
- **SETUPUX1x, SETUPUX1y**: the set-up experience seen in the installed window, then Agents → Setup lit by default.
- **AGENTSPLACE1**: the place pages carry the Roles, Workflow and Sessions sections, which `setupLit` waits on.
- **FLOWCHART2h**: the workflow editor seen in the installed window.
- **COMPANYVIEW1**: the agents' structure configured and seen, as a list and as a diagram (remaining rows).
- **MAPUX1**: the Map's own UI and UX design.
- **MEETUX1a–d**: meetings and brainstorms in Sessions: started, followed, read, and in *What needs you*.
- **PERSONDOOR2** (no row filed): the boundary between the person and their agents that D156 §7 names, and the machine
  key moved off a session's connector.
