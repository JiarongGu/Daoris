# The desktop app and its window — how it works now

## What it does

The desktop is one .NET process on Shenora's kit. It sets up the home and the machine log, starts or adopts the local
host, runs the driver loop in-process, and opens windows on the kit's embedded Chromium. Every window shows the HTTP
host's own web bundle, so the desktop and a plain browser run one UI. The page reaches the machine only through bridge
modules. .NET judges the machine's state once and pushes it, with its data, to every window; the page draws it, words
it in the viewer's language and sends the person's presses back. The frame is the same on every view: places in an
activity bar, a menu strip, a list pane, a main area, a right side bar, a bottom panel and a status bar. The same
executable, started with a browser argument, is Daoris's own browser, which sessions drive over CDP.

## Its parts

`Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `App/` is `src/Daoris.Desktop/Daoris.Desktop.App/`, `Driver/` is
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, and web paths are under `src/Daoris.Web/src/`. Each type is in a file of its name;
`docs/index/README.md` says where a route, slice, hook or fixture is. Which folder owns which job:

| Where | Its job |
|---|---|
| `App/` | The composition (`Program`), the frameless main window and the framed secondary windows on the kit's `ChromiumWindowHost`, the notifier, the taskbar's wear, the browser's host and process |
| `Modules/Bridge/` | `DAORIS.DRIVER`, one route class per domain in `Routes/` with its bodies and wire shapes in types beside it, `PersonRoutes` |
| `Modules/Platform/` | `StatePublisher`, `StateSlices`, the instance pipe and its `Pipe*` parts, `LogModule` |
| `Modules/Loop/` | `DriverLoop` and its parts, `HostSupervisor`, the 16 `*StateSlice` owners, the attention judge's helpers, the person's words to a session (`SessionWords`, `HeldWords*`, `Words*`) |
| `Modules/Windows/`, `Modules/Browser/` | `DAORIS.WINDOWS`, the theme order, balloons, the waiting badge, `WindowPost`, `TerminalModule`; Daoris's browser |
| `Driver/Platform/` | `ProcessStarts` and `OwnedProcesses`: the start gate and the one management of what starts (rule 63) |
| web `app/`, `work/frame/` | The window's frame (`App.tsx`, its hooks in `app/shell/`), the regions and where views stand (`placements.ts`) |
| web `work/` | Sessions: `WorkFrame`, its hooks and parts by folder (`head/`, `conversation/`, `start/`, `review/`, `waits/`, `rail/`, `attention/`), a session's note |
| web `menus/`, `notifications/` | The command table, keys, the right-click menu; the bell, *Notifications* and *What needs you* |
| web `bridge/`, `app/ShellSignals.tsx`, `data/api.ts` | The page's calls by domain, the state hub, pushed slices into the query cache, the person key on writes |
| web `design/`, `tokens.css` | Tokens, the atoms (`design/atoms/`, re-exported by `design/ui.tsx`), the flows and the canvas (`design/canvas/`) |
| web `locales/`, `scripts/` | Catalogues per language, authored text, the glossary, `i18n-check` and `names-check` |
| web `overview/`, `quests/`, `projects/`, `map/`, `knowledge/`, `agents/`, `plugins/`, `settings/`, `help/setup.ts` | Each place's view, list and pages; the setup guide's steps |

## Its rules

**The host and the bridge**

1. Every window renders the HTTP host's `wwwroot` bundle on the kit's Chromium at `https://daoris.localhost/`, and the same
   bytes serve a plain browser. There is one UI and no second frontend. (from D38, D85, D92)
2. The page reaches the machine only through bridge modules: `DAORIS.DRIVER`, `REGISTRY`, `REMOTES`, `BROWSER`,
   `WINDOWS`, `TERMINAL`, `UPDATE`, `LOG`, the shell's alone. No machine-local fact becomes an HTTP route. (from D47, D50)
3. A driver route is a `[DriverRoute]` handler in `Routes/<Domain>Routes.cs`, named like the page's `bridge/<domain>.ts`
   that sends it, its bodies and wire shapes in types beside it. A refusal the person can act on is answered as data with a
   code the page words. Its marks carry its pipe class and, for a person's or a role's press, its drain; what it marks
   owner-set (`[Owner]`); a payload's look form (`[ClassedBy]`); why a setting is never already (`[NotAlready]`); a person's
   press's grounds (`[Ground…]`, `[Timeless]`, or `[GroundRefusal]`, the route's own refusal answered at the ask before any
   Confirm) and the values its card names (`[Says]`). Another module's routes are marked on its class (`[ModuleRoute]`,
   `[WindowModule]`). The pipe's census and owner rows are read from the marks at start, and a route with incomplete marks
   fails its first use. (from D169, D182, D231)
4. A press declares the scopes it changed with `[Publishes]`. `StatePublisher` raises `STATE_CHANGED {scopes, sessions?}`
   after a successful answer, including a refusal answered as data. No handler raises it by hand; a read declares nothing. (from D169)
5. The dispatcher runs `RefusalLog`, `UiStallWatch`, `OffTheWindow`, then `StatePublisher`. A route leaves the window's
   thread at its first wait, so no git fan-out runs on it. (from D169, D56)
6. .NET judges each slice of machine state once and pushes it with its data. There are 16 slices. A partition carries
   `{epoch, seq, status, data}`, a delta names its `baseSeq`, and a periodic manifest repairs a dropped push. (from D182)
7. A window subscribes, asks its snapshot (`STATE_SLICE`), then applies newer pushes; a late push never moves it back. A
   pushed entry is read from its slice alone, waited for until its owner judges it, with no pull beside it; a failed snapshot
   is read again at the next manifest. A plain browser, the stories' and e2e's harness, pulls the service's lists. A test
   stands a shell up with `test/sliceShell.ts`. More windows never mean more judges or more git. (from D182, D139)
8. A pushed projection is allow-listed. It carries no credential, no arbitrary payload, and a machine path only in a
   desktop-only slice. Diffs, transcripts, graphs and search results stay on demand. (from D182)
9. The domain's verdicts live in .NET: grouping, attention, eligibility, milestones, usage curves. The page keeps
   rendering, translation, selection, sorting and view state; its store is the query cache fed by pushes. (from D182, D169)
10. Each declared press is said `UNDER_WAY` from dispatch to answer, in every window. A plain browser has no bus and
    polls. (from D169)

**The person's doors and their key**

11. The person key is a random 256-bit value minted at each start of the host the window's `HostSupervisor` starts, handed on
    the host's first input line and held in memory: never in a file, an environment, a URL, page storage or the log. Every
    readiness probe asks the host's proof of it. `ProvedKey` is the key only once proved, and the loop's clients present it as
    `Daoris-Person` to an address of this machine, never to a remote (`PersonHeader`). (from D156)
12. The page asks `PERSON_KEY` and sends `Daoris-Person` on writes only; no session ever holds the key. `PERSON_KEY` answers
    the proved key, and none for an adopted host or a failed proof, which the window says, reading none of the host's held
    asks. A terminal's verb never posts a person's door to the host itself (a quest's or an ask's person acts, a repository's
    retire, wire and import, a plugin's replace, a run's node acts, a git identity or access change): where Daoris runs it is
    the person's press of a `Person` route (rule 14), made by the in-process client that holds the key; with none running the
    verb acts alone or refuses. (from D156, D253)
13. Reads are open. An agent's doors are judged as an agent's, and are any caller's: an ask's close and delete, a quest's, a
    session's and a lost take's bring-forward or dismissal ended its keyless refusal (D284), the key now only saying whose
    press it is. The remaining person's doors (a verdict, a yes, a go-ahead's answer, an ask's or a quest's workflow and
    review choice, their words, the registry's) need the key until they are workflow waits or opened in turn. An unclassed
    write is refused. A refusal is a 403 with a code, never a 500. (from D156, D284)
14. A terminal's press of a `Person` route and a request the host holds for the person are one request row in
    *Notifications*, waiting with no clock for the Confirm, default no, until answered or taken back (under the tier `act`,
    rule 15, with no row); a main window in front says it by a toast, and one heard out of focus is said once the window comes
    in front. A start with `hold: true`, or the pipe's `HOLD { session }`, once confirmed binds that one connection to that one
    conversation in memory: its words, turn stops and finish run with no card, and the pipe forwards it the conversation's
    lines, events and end, bounded, with a `GAP`, until `LET_GO`, *Let go of the terminal*, the conversation's end or the
    connection closing (`person.hold`); a `HOLD` card still waiting at the stop ends lost. A lone surrogate escape is no text:
    in a line's id or key it closes the connection, in a payload it is refused `shape`, and a page's report to the log drops
    it. The host's own held asks (`PersonConfirmations`) are bounded: five naming a session, one more for the terminal's, two
    to an asker, and one naming no session bound by its process (`AskerProcess`). The page lists what waits at the service
    only where it holds the person key; a browser lists none and never polls. (from D156, D196, D238)
15. A terminal's press runs with no card in two ways. The steward's acts: the person appoints the steward to the terminal
    and grants its acts in the window; its press, proved by the role's key, makes them, each use logged and said on the
    steward's line in *Notifications*, and an act taken back asks from the next press. And the terminal's tier, a machine
    setting read at each press (`driver.json`'s `terminal`, `daoris settings terminal ask|act`): `ask`, the default, cards
    every press; under `act` a person's press proving no session's key runs at once as the person's own, logged
    `terminal.pressed`, whatever its route and wherever the pipe places its process, a steward's act that would ask the
    person included (`SESSION_INPUT` too). The verb alone, with no
    Daoris running, writes `ask` and refuses `act`. `data/terminal-acts.json` keeps the newest 20 tier changes with the door
    each came by and the newest 100 presses under `act` with their ends; while the tier is `act` or a press is kept,
    *Notifications* heads the presses' group with who turned it on and when, and *Back to ask*. (from D201, D209, D271)

**The windows**

16. The main window is frameless. The app strip is its title bar, and the caption buttons are painted from
    `ChromePalette`, a copy of the tokens. Secondary windows keep a native frame and load the same bundle. (from D56, D55)
17. The viewer picks System, Light or Dark. The page sets it on the engine, and the shell keeps it (`KeptScheme`) for the
    next start's first frame. The theme is decided on the window's thread (`PageThemeOrder`), so a page's word that lands
    beside the OS's change is the one worn. Daoris's browser follows the OS. (from D66, D41)
18. A later launch opens a new main window of the running instance. The application ends with its last main window.
    One driver loop runs per home. (from D171, D104)
19. Each main window wears its own workspace's waiting count on its taskbar button (in the page's red, *9+* past nine) and
    in its title, and the install's identity Daoris chooses. A notification's click brings forward the window that holds its
    subject. A press waiting unshown flashes the button of the main window last in front while none is in front; a closed
    main window is forgotten there, its flash going to the first. (from D196, D108)
20. The shell notifies and decides nothing. A balloon shows when no window has focus, worded from the words the page
    hands it in its language, with the English catalogue as fallback. (from D196, D142)
21. A render that throws is caught by `PageBoundary`. It offers a reload of the page alone and logs `page.error` with
    the view's name and a five-frame stack. (from D217)

**The frame**

22. Nine places, in order: Overview, Sessions, Quests, Repositories, Map, Knowledge, Agents, Plugins, and Settings at
    the foot. Sessions, Agents and Plugins are shell-only. A tenth place needs its own decision. (from D150, D66)
23. There is one frame and no mode switch. The list pane and main area belong to the view; the activity bar, strip,
    status bar, right side bar and panel belong to the frame. (from D66, D118)
24. A record opens in the main area of the view whose list holds it, through one opener. A form is a drawer, and every
    overlay opens above a full side bar. (from D118)
25. One list pane on every view: a header with ＋ and ⋯, per-view resize, a 56 px strip when closed, Ctrl+B. Its closing,
    width, chosen item and filters are remembered per view. (from D118)
26. The side bar and panel hold movable views: *Notifications*, Timeline, Review, Workflow, At work and Ask Daoris on
    the right, Console and Terminal in the panel, until the viewer moves them. The side bar's body always holds its
    scrollbar's width (`DOCK_BODY`), since a chart placed by the pane's width otherwise flips as a scrollbar comes and goes.
    (from D118, D196, D96)
27. A page is one column. `ViewMain` caps nothing, no block keeps a measure of its own, and the main area lays out by
    its own width, never the viewport's. A cap, if ever set, is one `em` value on `ViewMain`. (from D141, D118)
28. The menus are *Workspace · Edit · View · Go · Run · Terminal · Help*. One table, `menus/commands.ts`, names every
    verb once; the menus, palette, keys and shortcuts drawer all read it. (from D152)
29. A record's act in a menu is enabled exactly when its header offers it. Keys follow VS Code where the meaning is the
    same. No act that ends or removes something has a key. Ctrl+1–8 open the places. (from D152)
30. The right-click menu is the page's own, offering the surface's acts from their one owner plus text, link and code
    groups. The engine's menu stays in text fields and is suppressed elsewhere. (from D138)
31. *Notifications* is one view, opened by the title bar's bell: presses waiting, *What needs you*, messages, and *Done and
    failed*. There is no *Confirm all*; a *What needs you* row is answered, never dismissed. *Done and failed* holds a
    session's end, none for a part whose line goes on (a later part or a co-worker that has not ended, `LineGoesOn`), and each
    watched step after a run's review once reached or failed, one line by run, node and attempt
    (`RunOutcomeNotices`, claimed by the look's last pass, so it is kept once). Beside the presses waiting, counting toward no
    group or the bell, stand the steward's line and Ask Daoris's presses in full access with no card this run
    (`HelpPressLine`: the place, *outside its focus* where it was, the outcome, its conversation, a pause's *Resume*), its
    restricted mode's cards apart under *Asked you*; a press that opened a place says what it made under its row, as the
    conversation does (`PressReceiptLine`); a restart empties it. (from D196, D253)
32. .NET judges *What needs you*, and its rows are every workspace's, whatever scope the window looks at, the same rows
    `daoris notices` lists; a declaration owed acceptance leaves it at the press, judged from the choices each time it is asked
    (D283). A session's row there is its line's (`LineNeeds`): none for a part whose line works through a later part or needs
    the person through another session, so a line is listed once. A row acts where one press is safe; no row starts a process to find a fact out. *Ready for
    you* holds a review row for each session Sessions' list places in Needs you for a run's Accept, except one of a quest
    whose run the person stopped with its review among its steps (`StoppedRuns`). Overview keeps one summary line per group.
    (from D150, D181, D196)
33. What announces depends on what waits and how urgent it is. An agent's message never reaches the desktop. The notify
    switch governs every announcement except a press's. (from D196, D188)
34. Every surface is reached from the frame and adds no new place. It needs a door from *What needs you* where it waits
    and a verb in the command table; an Ask Daoris door and a terminal twin only where one is wanted. (from D185, D150, D233)
35. A setting has one home, on the thing it is about. Elsewhere it is read-only with a door. Settings holds only the
    machine's and the person's domains. (from D150)
36. The scope is always named in the command center and status bar. The word is *workspace* / 工作区 everywhere. (from D75)

**A session's page**

37. A head shows the state, the title and two facts; a session's title takes one line, a quest's or an ask's two. The acts
    hold still; the id moves to ⋯ and *Details*. *Started from*, on *Details*' folded line cut with an ellipsis, names as
    presses the quest, the ask, the session a hand-over or go-on continues, a conversation's repository or Ask Daoris's room
    (`work/head/startedFrom.tsx`). *Details* adds *Started with*, the requirement it began with, whole (the quest's
    requirements, else its words, else the ask's, else a conversation's first message), beside the *Handed brief* its start
    was given. A rail row of a line holding several quests names its own session's quest. (from D152, D162)
38. Every wait shows in the head and in the foot's panel above the composer. Both places get the same press from the
    frame, and a press in one disables the other. One pure rule (`work/waits/waits.ts`) says which waits stand. (from D168, D185)
39. There is no text editor. A file's preview is a read-only side-bar tab from the session's tree, or from the branch it
    landed on once its tree is gone (`SESSION_FILE`). It shows at most 256 KiB, and a path outside the tree is refused. (from D55, D111)
40. *New conversation* starts from the person's words in a repository they pick, with tree, agent and account chips.
    `START_CHAT` carries a request id, and one id never starts twice. Its repository picker and Ask Daoris's focus picker are
    one combobox (`design/atoms/ChipCombobox.tsx`). *Make it a task with Daoris…* hands Ask Daoris the words as typed with the
    repository as the conversation's focus. (from D162)

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

45. One primitive draws every flow (`RankedFlow`, which `LifeFlow` and `FlowChart` draw through), laid out in layers,
    deterministically, from the record and never guessed. By the pane's width it runs left to right, top to bottom, or as a
    rail. (from D186)
46. Edges are ink, never a status hue. The nodes are one ordered list with one Tab stop; the edges are one `aria-hidden`
    drawing. (from D186)
47. Every graph stands on one canvas: a viewport with pan and zoom, a minimap only past twice the viewport, and a list
    offered at every width, standing below 36rem where nothing else fits. No feature moves nodes or saves positions. The
    maps keep the viewer's view; one kept under a changed graph says the selection is gone. (from D224)
48. A placed world is HTML that CSS lays out and the canvas measures. A positioned world arrives with coordinates: past
    400 nodes only those near the view, the focused and the selected mount, and below 4 px of `text-meta` the rest are dots.
    One engine, Daoris's own, serves every feature, chosen by measurement over React Flow. A pan or zoom step draws again only
    what it changed (each card and edge a memoized unit, `WorldNode.tsx`; the toolbar by `useStableActs`), and
    `measured/large.spec.ts` holds 100 and 1,000 nodes to their budget. (from D224)

**Translation**

49. The chrome translates through `locales/<lang>/<area>.json`. The languages offered are the folders shipped (`en`,
    `zh`), with `en` as the source; `i18n:check` holds every catalogue to it. Keys are structural. (from D222, D42, D61)
50. Authored words need no language. Each is a string or a tag-to-text map, shown in the viewer's language, else the
    base language, else as written (`locales/authored.ts`, twin of the driver's `AuthoredText`). (from D222)
51. Shipped data keeps one English text. Its words are catalogue keys by id, and a census holds every id in every
    catalogue. (from D222)
52. A name is a UI element, designed in each language. `glossary.json` is the authority. `names:check --strict` runs in
    the web build for conformance, form and doors; the budgets only report. (from D116, D54)
53. Daoris's own lines in a record are codes the page words in the reader's language. A session's note is drawn from its
    coded parts only (`work/Note.tsx`): a record with no parts draws nothing, and a part the page cannot word shows marked as
    recorded. Someone's words show as written. The window's language is the viewer's; the terminal stays English. (from D142)

**Daoris's browser**

54. The browser is this executable started with `--daoris-browser` first, in Chromium's own windows, in a process of its
    own, on a profile at `<home>/profiles/browser/engine`, joined to the `Browser` job as it starts so it ends with the shell;
    a bring-up that shows no window in its time is ended, and a profile file another program holds is read as absent, never
    written over. The engine is the kit's codecs package (`Shenora.Chromium.Codecs.win-x64`, its own CEF build), so it plays
    H.264, AAC and HEVC (through the machine's decoder); the codecs are patent-licensed and the package carries no licence.
    `CodecPlaybackProcessTests` plays each with its page shown, since Chrome loads no media in a hidden page. (from D99, D85, D234)
55. Sessions drive it through an MCP server a plugin declares with `${browser}`, its loopback CDP endpoint. Daoris's own
    plugin runs it `--isolated`: each session, Ask Daoris included, drives a context of its own, closed with the session, never
    the person's tabs or another session's; each new tab is given the cookies of the browser's own context, where the person
    signs in (`share-sign-in.cjs`, its `--init-page`), and nothing goes back. Measured: on shared tabs a second session's snapshot
    broke the first one's clicks (5 s each); apart, about 0.6 s. The person's Edge on a Daoris profile stays an option. Daoris
    keeps sign-ins across a restart itself. A plugin whose manifest says `hooks.browser` is handed the endpoint as `DAORIS_BROWSER`
    and works in a tab of its own in the signed-in profile, which a fresh context does not carry. (from D78, D84, D277, D281)

**The setup guide and the views**

56. A first start opens Settings → Get started while an agent with an account, Daoris's own agent or a repository is
    missing. Each step reads its state off the machine and names its screen and its command. Every door to a screen, in the
    guide, in Ask Daoris's starters and in its way back to the guide, is one `StarterDoorButton`. (from D97, D50)
57. Settings has eleven domains, in this order: Get started; Appearance; Driver; AI features; Git; Tools; Browser; Disk
    space; Updates; This Daoris; Machine log, each a `<Name>Domain.tsx` and a row in `settings/domains.ts` naming the rows the
    palette finds. A row's label holds its name and one line; a terminal twin or a variable is in its ⓘ help. Disk space's
    folders are one list (the name, the path cut in the middle, the size, *Open folder*), the home shown there alone. A text
    naming a Settings place says the domain's catalogue name (`HelpRoomSettingsPlacesTests`). This Daoris reads and sets up
    the wiring from the page (*Open*, *Put on my PATH*, *Copy a terminal line*, *Change the service's address* to one of this
    machine's), each said first inside its row. (from D150, D75)
58. Agents has two modes, Agents and Setup. Setup is the tree of places (machine, workspace, repository), each a page with a
    head and three folded sections; the tree (`setup/SetupList.tsx`) is a WAI tree with one row in the tab order. A row's
    second line is a need (⚠ and its first reason), *Held*, *Adoption running* or how it lands; the *Needs attention* chip
    counts the places to look at. The chosen place is kept across searches and pages. (from D221, D150)
59. An agent's page heads in one line. Each account is one row with one act by its state. An account is read only when
    the person presses or something has run, never on a timer. (from D152, D150)
60. Repositories lists by workspace. A repository's tabs are Details, Branches, History, Changes, Integration and Files
    (the files git does not manage there, with the person's rules and permissions); a workspace's are Details, Branches and
    Workflow. Their setup and rules are the place's, in Agents → Setup, where each place's sessions fold holds one
    row, *New repositories and rooms* (`settings/repository/Growth.tsx`, `SET_GROWTH`, `driver.json`'s `growth` and `workspaceGrowth`): whether an agent's
    press that opens a repository or a room runs as the access mode says, waits for the person's Confirm even in full access, or is refused. A
    workspace's row is its own value, a repository's its own over its workspace's (`LINES`' `growth` resolves it); the press is the person's alone, never
    an agent's in any mode (roles-and-supervision 56). (from D150, D147, D276)
61. Knowledge is one place with two modes, Search (the default) and Convergence. The Map is a read-only view of the
    workspace's repositories and what moved between them, plus the code map (modules in their folders; a cycle one card in
    the nearest folder holding it), drawn on the canvas. Each zoom tier says less about the same cards, the chosen and the
    focused keeping their name; one choice stands forward (its lines and neighbours at full ink, the rest at 40%), said in a
    polite line under the drawing, its detail with a door to the repository's page and back. Find, from 13 items, matches a
    name and then what it owns, or a module's id and then its folder. The list is one tree on both levels
    (`map/list/MapTree.tsx`), and below 36rem it is the reading. *Which quests* starts at the last 30 days; an empty,
    unanswered or repository-less map says why, with its door. (from D150, D67, D224, D239)
62. Doctrine is never written from the window; it shows the command to run. (from D31)
63. Nothing the desktop starts outlives the process that started it. Every start through the gate names its purpose and is
    joined, as it returns, to that purpose's kill-on-close job, held for the process's life; a process ended early is ended by
    `OwnedProcesses.End`; the census reads each job by process id and stops nothing. (from D249)
64. A system file or folder picker is the kit's `IFileDialogs`, owned by the window that asked and shown on a thread of its
    own; a census holds that no source shows one itself. A window thread's exception reaches the machine log and the pump goes
    on. The executable is built with its context validation relaxed, since a shell extension's handled exception under a
    picker's right-click menu otherwise fail-fasts it (`0xc0000409`). Every caller posts to a window's thread through one
    guarded `WindowPost`.

### The processes Daoris starts

One management, `OwnedProcesses`, behind the start gate; the purposes and where each start is held:

| Start | Purpose | Held by | After a crash of its owner |
|---|---|---|---|
| An agent, plugin process or piece through the launcher (`AgentStarts`) | `Launched` | the agents' job, from its first instruction; a session's own job beneath | ended |
| The same where no launcher took it | `Agent`, `Plugin` | its kind's job | ended |
| git's runs and what git starts | `Git` | the git job | ended |
| A question read to its end, a usage read | `Read` | the read job | ended |
| An install or sign-in, a plugin tool's set-up, a key made, the doctrine CLI, a server's package | `Tool` | the tool job | ended |
| Daoris's own browser | `Browser` | the browser job | ended |
| The HTTP host | `EndsItself` | none: it stops when its input ends | it stops itself |
| The update's launcher, the recorded Edge | `Outlives` | none, by design | running |
| The file manager; a terminal's shell | gate's `RunAsync` | the system's; the shell's own job from its first instruction | the shell ended |
| A heavy run's gate, a git act's step | direct, suspended | a named job of their own | ended |

A start that must not let what it starts in its first instant escape starts suspended in a job of its own. A terminal's
`daoris gate run` is the running application's (`RUN_HEAVY`): it runs the gates as its children under the machine's admission,
with the terminal's folder, environment and own standard handles, so the gates write where the terminal does rather than through
the pipe, and answers the exit code. The run is held to its connection (a verb killed or its session ended stops it; Ctrl+C asks
`RUN_STOP`); a terminal whose rights are not the application's (`TerminalRights`) is refused, and where no Daoris takes the run
the verb says why and runs it itself. A session's `check_run` is the same run asked through the broker (`CheckBroker`) and queued
one at a time, its verdict the session's next word. Off Windows there are no jobs. The census is read once, as the application stops
(`RunningProcessesLine`); the proofs are the `*ProcessTests` and `*CensusTests` of each start, the tests' own starts named in
`TestProcessStarts`, and `node tools/process-tree.mjs` lists each Daoris process outside the application's tree.

## Where a change goes

- **A new page piece goes in its place's folder**, its test and story beside it: frame regions in `work/frame/`, the strip in
  `work/strip/`, atoms in `design/atoms/`, flows in `design/` and graphs in `design/canvas/`. A folder past twenty sources
  splits into named scopes (`test/structure.test.ts` with `structure.json`).
- **A new driver route** follows `.claude/knowledge/bridge-presses.md`: the handler and its `[Publishes]`, the desktop
  README's row, the hook in `bridge/<domain>.ts`, its marks, and its words in both catalogues. The page's route names,
  `PUBLISHES` and the presses' keys are generated from the marks into `bridge/generated/routes.ts`, never committed.
- **A new pushed slice** follows `.claude/knowledge/pushed-slices.md`; **a wait on the person** `person-waits.md`; **new
  words** `translation-parity.md`, plus a glossary row for a name. **A movable view** goes in `work/frame/placements.ts`;
  **a verb** in `menus/commands.ts`.
- **Tests that pin it:** the modules' `DriverModuleRoutesTests`, `StatePublisherTests`, `StateSlicesTests`, `PipeCensusTests`,
  `RefusalCatalogueTests`, `WindowsModuleTests`; the web's `test/structure.test.ts`, `design/`, `locales/`, `menus/` and
  `bridge/state/` tests, `e2e/platform.spec.ts` and `measured/*.spec.ts`.
- **The gates that bite:** `web-types`; `web-rules`; `web-unit`; `web`, whose build runs `i18n-check`, `names-check --strict`,
  `tsc` and `vite build` before Playwright over the HTTP host; `web-measured`; `modules`, `modules-process` and `censuses`;
  `driver` for Ask Daoris's coverage. A story or a mocked bridge is no proof of the installed window.

## Contradictions

- D55, D56, D66 §1, D75 §1–2 and D150 §2 (two frames, a six-item bar, other menus, a Daoris menu, seven Settings domains) vs
  D150's nine places, D152's menus and `settings/domains.ts`: the later and the code (`VIEWS`, `MENUS`, `SETTINGS_DOMAINS`).
- Code vs code: Ask Daoris's twins (`PLACE_DOMAINS` in `help/places.ts`, `HelpPlaces.Domains`) omit `tools`, so no go opens
  Settings → Tools. Intent unclear.
- D150 §6 (*What needs you* leads Overview) vs D196 §5 (a summary): D196. D30 (convergence first) vs `knowledge/modes.ts`
  (Search) and D40: the code.
- D41 (a right drawer for every detail and form) vs D118 §3 and D56: D118 and D56. D41 and D42 (toasts carry the service's
  sentence) vs D142, D182 §2 and D185 R3 (coded verdicts worded where the action started): the later; an unknown code still
  shows the driver's sentence. D42 (invalidation after every mutation) vs D169 §6 and D182 §7: the later (`stateHub.ts`).
- D169 §3 (an event never says what a thing is) vs D182 §2 (pushed with its data): both; D182 governs the 16 slices,
  `STATE_CHANGED` the scopes not moved.
- D78, D84 and D85 (the browser a WebView2 window, or inside Daoris's windows) vs D99 (a process of its own in Chromium's
  windows): D99 (`BrowserProcess`, `EngineBrowserHost`).
- D186 §2 and §9 (no pan or zoom) vs D224: D224. D185's and D186's heads say "proposed"; their notes record acceptance on
  2026-10-20: the notes.
- D97 (six steps) vs `SetupStepId` (five): the code. D116 (two languages) vs D222 §3 (any number): D222.

## Open

- **CANVAS1g**, **FLOWCHART2h**: the canvas and the workflow editor in the installed window.
- **SETUPUX1x**: the set-up experience seen in the installed window, and its fixes.
- **AGENTSPLACE1l**: the Agents place seen in the installed window.
- **MAPUX1g**: the Map seen in the installed window (`docs/2026-11-06-map-ux-design.md`).
- **MEETUX1a–d**: meetings and brainstorms in Sessions: started, followed, read, and in *What needs you*.
- **OPTIMIZE1b**: a door that reads the process census while the window is open.
- Decided, with no row: PERSONDOOR2, the boundary between the person and their agents that D156 §7 names, and the machine key
  moved off a session's connector.
