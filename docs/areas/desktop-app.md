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

`Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `App/` is `src/Daoris.Desktop/Daoris.Desktop.App/`, `Driver/` is
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, and web paths are under `src/Daoris.Web/src/`.

| Unit | File | Its job |
|---|---|---|
| `Program` | `App/Program.cs` | Composes the shell: the browser argument first, home, log, host start, engine, modules, dispatcher middleware |
| `MainForm`, `SecondaryForm` | `App/MainForm.cs`, `App/SecondaryForm.cs`, `App/SecondaryWindowHost.cs` | The frameless main window, and another main window (`main:<n>`) for a later launch; framed secondary windows (`monitor`, `session:<id>`) |
| `Pickers`, `UiExceptions` | `App/Pickers.cs`, `App/UiExceptions.cs`, `tools/cet-relax.mjs` | The one place a system file or folder picker is shown (a census holds it), marshalled onto the asking window's own STA pump thread; a window thread's exception is written to the machine log and the pump goes on. The executable is the engine's launcher, built CET-enforced, and the build and the publish set its relaxed context-validation flag, since a shell extension's handled exception under a picker's right-click menu otherwise fail-fasts the process (`0xc0000409`, code 0x30) |
| `DriverModule` and the route classes | `Modules/Bridge/DriverModule.cs`, `Modules/Bridge/Routes/<Domain>Routes.cs` | `DAORIS.DRIVER`: one handler class per domain, each route `[DriverRoute]`, each press `[Publishes]` |
| Route-class helpers | `Modules/Bridge/Routes/GitRefDesk.cs`, `GitAnswers.cs`, `LinesWires.cs`, `WorkWires.cs`, `SweepWires.cs`, `TreesTidyDesk.cs`, `WorkflowAnswers.cs`, `DriverStateAnswer.cs`, `HelpScreenDoors.cs`, `HelpMachineDesk.cs`, `HelpProposalFacts.cs`, `TreesLandDesk.cs`, `LandPresses.cs`, `TreesReviewWires.cs`, `SessionsMoveDesk.cs`, `SessionsAskDesk.cs`, `SessionsWaitDesk.cs`, `SessionsRecordDesk.cs`, `SessionsGroupsDesk.cs`, `SessionsGroupsWires.cs` | A route class keeps its marks and one-line doors (so callers and the page's bridge file are unchanged); the bodies and wire shapes of git's ref acts, the lines, work and sweep answers, the clean-up and sync, the workflow answers, the driver's state, Ask Daoris's Apply doors, room judges and proposal facts, the landing press, answered once per session and held in words while it goes on (`LandPresses`), with the review and preview wires, and a session's moves, asks, waits, record and groups sit in types of their own beside it (D231) |
| `SessionWords` and its parts | `Modules/Loop/SessionWords.cs`, `HeldWordsQueue.cs`, `HeldWordsDisk.cs`, `HeldSay.cs`, `WordsAnswer.cs`, `WordsRead.cs`, `WordsReach.cs`, `WordsQueueEdits.cs`, `WordsShown.cs` | The person's words to a session: `SessionWords` judges and says them; the words held for a record to end wait in `HeldWordsQueue` (its pump, which a failure no read expects ends with the words still held, the record's next move or the next word held starting it again) and in the held file `HeldWordsDisk` writes whole; the record read, the reach a word would have, the take-back and move of a waiting word and the lines shown in the conversation each have a type of their own |
| `AttentionStateSlice` helpers | `Modules/Loop/AttentionWaits.cs`, `AttentionFacts.cs`, `AttentionSessionRows.cs`, `AttentionMachineRows.cs`, `AttentionJson.cs` | The waits read off files and memory, the judge's input record, and the session and machine groups of `NeedsAPerson`'s rows, in the order it judges them; the slice and `NeedsAPerson` keep their doors |
| `StatePublisher` | `Modules/Platform/StatePublisher.cs` | Innermost middleware: `STATE_CHANGED` after a press answers; `UNDER_WAY` while presses run |
| `StateSlices` and the 16 slices | `Modules/Platform/StateSlices.cs`, `Modules/Loop/*StateSlice.cs` | Machine state judged in .NET and pushed (`STATE_PUSH`, `STATE_MANIFEST`, `STATE_SLICE`) |
| `DriverLoop`, `HostSupervisor` | `Modules/Loop/DriverLoop.cs`, `Modules/Loop/HostSupervisor.cs` | The in-process loop that owns the slices; the host started or adopted, handed the person key |
| `ProcessStarts`, `OwnedProcesses` | `Driver/Platform/ProcessStarts.cs`, `OwnedProcesses.cs`, `ProcessJob.cs` | The start gate, a person's start first; the one management every start is handed to under its purpose: its job, its end and its census (rule 63) |
| `DriverLoop`'s parts | `Modules/Loop/LoopState.cs`, `LoopRun.cs`, `LookTold.cs`, `LoopComeUp.cs`, `LoopAddress.cs`, `HomeHold.cs`, `TickShown.cs`, `TickSignatures.cs`, `TickErrors.cs`, `AttentionTold.cs`, `OutputTold.cs`, `PipeDecisionAdmission.cs` | What the run brings up and the loop's doors read, in one state type written whole with no lock; the address and pipe named in the home under their own lock with the stop's cancel; one run per loop, declaring what it brings up in the order it is let go in reverse; what each look keeps and tells; what the come-up hears; the home's lease waited for; the tick's shapes, signatures and errors; the attention and console events; the supervisor's decisions' admission. The loop keeps one-line doors, the start and the stop (D231) |
| `PersonRoutes` | `Modules/Bridge/Routes/PersonRoutes.cs` | `PERSON_KEY` and the person's confirmations, answered to the page |
| `InstancePipe`, `PipeCensus`, `RouteMarks`, `PipeConfirmations` | `Modules/Platform/` | A terminal's verbs into the running Daoris; each route classed `Open`, `Person`, `Window` or `Role` by its mark, read at start |
| `InstancePipe`'s parts | `Modules/Platform/PipeState.cs`, `PipeWaitingKeep.cs`, `PipeListener.cs`, `PipeConnection.cs`, `PipeAdmission.cs`, `PipeAsking.cs`, `PipeTakeBacks.cs`, `PipeRestore.cs`, `PipeReserved.cs`, `PipeLimits.cs`, `PipeHolding.cs`, `PipeHold.cs`, `PipeForwarding.cs` | The presses and connections under one gate; the waiting records under their own lock, never taken inside it; the listening, a connection, a request's admission, a person's press from its reservation to its ending, `CANCEL`/`RESULT`/`WITHDRAW`, the restart's restore; a terminal's hold of a conversation (`HOLD`, `LET_GO`) and the bounded forwarding of its lines. The pipe keeps the wire's words, the serve, the close and the run |
| `WindowsModule` | `Modules/Windows/WindowsModule.cs` | `DAORIS.WINDOWS`: `OPEN`, `OPEN_BROWSER`, `SET_THEME`, `SET_BADGE`, `SET_TITLE`, `SET_NOTICE_WORDS` |
| Notifier | `App/SessionNotifier.cs`, `Modules/Windows/SessionBalloon.cs`, `Modules/Windows/MainWindowScopes.cs` | OS balloons in the window's language, posted to the window's thread through `WindowPost`; which window a click brings forward |
| Taskbar | `App/TaskbarBadge.cs`, `Modules/Windows/WaitingCount.cs`, `WaitingBadge.cs` | Each main window's count on its button and in its title |
| Browser | `Modules/Browser/` (`BrowserModule`, `EngineBrowser`, `EdgeBrowser`, `InAppBrowser`), `App/EngineBrowserHost.cs`, `App/BrowserProcess.cs` | Daoris's own browser, its profile, favorites and CDP endpoint |
| Browser's seams | `Modules/Browser/EngineBringUp.cs`, `ProcessBrowserChild.cs`, `EngineProfilePrepare.cs`; `Modules/Windows/WindowPost.cs`, `App/ControlPost.cs` | The logic the executable's wiring calls, tested in the modules: the bring-up (a process that shows no window in its time, stops as it starts or is cancelled is ended and forgotten), the process joined to the `Browser` job, the profile's preparation (a file another program holds is read as absent, said once, never written over), and the one guarded post to a window's thread that every window's caller shares |
| `TerminalModule`, `LogModule` | `Modules/Windows/TerminalModule.cs`, `Modules/Platform/LogModule.cs` | The person's own shells; the page's report into the machine log |
| `App` | `app/App.tsx` composes; `app/shell/` holds its hooks (state, feeds, figures, opener, commands, shortcuts, doors, views, attention doors, context doors) and `app/shellHelpers.ts` its constants | The window's frame: places, strip, menus, views, dock, drawers |
| Frame regions | `work/frame/` (`frame.tsx`, `ViewFrame.tsx`, `ListPane.tsx`, `ViewMain.tsx`, `RightDock.tsx`, `placements.ts`) | Activity bar, strip, status bar, list pane, main area, side bar and panel, and where views stand |
| `WorkFrame` | `work/WorkFrame.tsx` (30 KB: outline only) | Sessions: the rail, the attended session's page, its head, waits and review, composed from its hooks |
| `WorkFrame`'s hooks | `work/frame/useFrameLayout.ts`, `useDockViews.ts`, `layoutMemory.ts`; `work/head/useAttendedSession.ts`, `useAttendedRecord.ts`, `useHeadAsks.ts`, `useAttendedHead.tsx`; `work/conversation/useAttendedBox.ts`, `useWords.ts`, `useBoxWords.ts`; `work/start/useNewStart.ts`, `StartPane.tsx`; `work/review/useSessionLanding.ts`; `work/waits/useAttendedWaits.ts` | One part each of what the frame reads and presses (D231): the columns and regions, the attended session and its record, its box and words, a new conversation, the header and its questions, the landing, and what waits on the person; each that reaches the bridge is a named organism of the presentational boundary |
| `WorkFrame`'s composed parts | `work/frame/regionSurface.tsx`, `OutputPanel.tsx`; `work/rail/useSessionsList.tsx`; `work/start/agentRoster.ts`; `work/head/AttendedPane.tsx`, `partRows.ts`, `HeadNote.tsx`, `headRows.tsx`; `work/conversation/AttendedConversation.tsx`, `AttendedBoxes.tsx`, `useSeekPart.ts`, `NoteLines.tsx`, `BlockLines.tsx`, `saidDoors.ts`; `work/attention/AskOnce.tsx`, `rowRules.ts` | What the frame composes from those hooks (D231): each region's surface and the streams, Sessions' list, the agent roster, the record, the conversation and its boxes; the frame holds the hooks and the layout, each part takes the hooks' results as typed objects. `ConversationView`, `AttentionRow`, `frame.tsx` and `SessionHead` each keep their component and hand their leaf lines, rules and panel to files of their own, all under 40 KB |
| A session's note | `work/Note.tsx`, `work/noteLines.ts`, `work/notes/noteCodes.ts`; the home migration's `note-records` step | Drawn from its coded parts only (D142): a record with no parts draws nothing, a part the page cannot word is shown marked as recorded, and the migration clears the note of a finished session record that has no parts; `Note`, `Said`, the waiting cards and `NoteRecord` take parts alone, no `note` prop |
| Command table | `menus/commands.ts`, `menus/shortcuts.ts`, `menus/ContextMenu.tsx`, `work/strip/` | Every verb once; the menu bar, palette, command center, keys and the right-click menu |
| Notifications | `notifications/`, `work/attention/` | The bell, the view and its four groups; *What needs you* |
| Bridge and state hub | `bridge/<domain>.ts` (or `bridge/<domain>/` for `agents`, `repositories`, `sessions`), `bridge/state/stateHub.ts`, `app/ShellSignals.tsx`, `data/api.ts` | The page's calls; scope invalidation; pushed slices fed to the query cache; the person key on writes |
| Design language | `tokens.css`, `design/ui.tsx`, `design/atoms/`, `design/theme.ts`, `design/phases.tsx` | Tokens as the Tailwind theme; the atoms, one file per family under `design/atoms/` and re-exported by `design/ui.tsx` (`Pill`, `Drawer`, `Toasts`, `SettingList`, `FormGrid`); `PhaseList` is `design/phases.tsx`'s |
| Flows and canvas | `design/canvas/LifeFlow.tsx`, `design/FlowNodeBox.tsx`, `design/RankedFlow.tsx`, `design/flowTypes.ts`, `design/flowParts.tsx`, `design/flowLayout.ts`, `design/canvas/` | The flowchart engine (ranked layout, ink, folds, rail) and one viewport for every graph (`Canvas`, `PositionedCanvas`, `SuppliedCanvas`); the landing, quest, parts and session life-flows and a workflow's routes draw through `LifeFlow` and the chart through `FlowChart` (`workflow/chart/FlowChart.tsx`), both in the canvas's viewport |
| Translation | `locales/<lang>/<area>.json`, `locales/i18n.ts`, `locales/languages.ts`, `locales/authored.ts`, `locales/glossary.json`; `scripts/i18n-check.mjs`, `scripts/names-check.mjs` | Catalogues per shipped language, authored text, the glossary and its checks |
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
   already; a person's press says its grounds (`[Ground…]` or `[Timeless]`; `[GroundRefusal]` the route's own refusal of the state it acts on, answered at the ask before any Confirm) and the values its card names (`[Says]`). Another module's routes are marked on its class (`[ModuleRoute]`, `[WindowModule]`). The pipe's census and owner
   rows are read from the marks at start, and a route whose marks are incomplete fails that first use. (from D169, D182, D231)
4. A press declares the scopes it changed with `[Publishes]`. `StatePublisher` raises `STATE_CHANGED {scopes, sessions?}`
   after a successful answer, including a refusal answered as data. No handler raises it by hand. A read declares nothing. (from D169)
5. The dispatcher runs `RefusalLog`, `UiStallWatch`, `OffTheWindow`, then `StatePublisher`. A route leaves the window's
   thread at its first wait, so no git fan-out runs on it. (from D169, D56)
6. .NET judges each slice of machine state once and pushes it with its data. There are 16 slices. A partition carries
   `{epoch, seq, status, data}`, a delta names its `baseSeq`, and a periodic manifest repairs a dropped push. (from D182)
7. A window subscribes, asks its snapshot (`STATE_SLICE`), then applies newer pushes. A late push never moves it back.
   A pushed entry is read from its slice alone, waited for until its owner judges it, with no pull beside it; a failed
   snapshot is read again at the next manifest. A plain browser, the stories' and e2e's harness, pulls the service's
   lists. A test stands a shell up with `test/sliceShell.ts`. More windows never mean more judges or more git. (from D182, D139)
8. A pushed projection is allow-listed. It carries no credential, no arbitrary payload, and a machine path only in a
   desktop-only slice. Diffs, transcripts, graphs and search results stay on demand. (from D182)
9. The domain's verdicts live in .NET: grouping, attention, eligibility, milestones, usage curves. The page keeps
   rendering, translation, selection, sorting and view state. Its store is the query cache fed by pushes. (from D182, D169)
10. Each declared press is said `UNDER_WAY` from dispatch to answer, in every window. A plain browser has no bus and
    polls. (from D169)

**The person's doors and their key**

11. The person key is a random 256-bit value minted for each host start, handed on the host's first input line and held in
    memory; never in a file, an environment, a URL, page storage or the log. The window's `HostSupervisor` mints one at each start of
    the host it starts (`Mints`) and asks every readiness probe for the host's proof of it. `ProvedKey` is that key only once
    the host proved it, and is what the loop's clients (`ServiceClient`, the index doors, the sync's local client) present as
    `Daoris-Person` to an address of this machine, never to a remote (`PersonHeader`), so the host enforces it. (from D156)
12. The page asks `PERSON_KEY` and sends `Daoris-Person` on writes only. No session ever holds the key. `PERSON_KEY` answers
    the proved key, and none for a host the shell adopted or whose proof failed; the window says so, and the host's own held asks
    are then not read into *Notifications* by the window. A terminal's verb that posts a person's door straight to the host
    carries no key and is refused there until the verbs present one. (from D156)
13. Reads are open. An agent's doors are judged as an agent's. The person's doors need the key. An unclassed write is
    refused. A refusal is a 403 with a code, never a 500. (from D156)
14. A terminal's press of a `Person` route and a request the host holds for the person are one request row in
    *Notifications*, waiting with no clock for the person's Confirm, default no, until the person answers or the asker
    takes it back. The route's name and raw values sit under *Details*. A start sent with
    `hold: true`, or the pipe's `HOLD { session }`, is carded saying so; confirmed, it
    binds that one connection to that one conversation, in memory alone. The connection's words in the verb's form, turn stops
    and finish for it then run with no card, and the pipe forwards it the conversation's lines, events and end, bounded, with a
    `GAP`. `LET_GO`, the head's *Let go of the terminal* (`SESSION_LET_GO`), the conversation's end and the connection closing
    end the hold, each told and logged `person.hold`. A `HOLD` card still waiting when the stop begins is no press under way, so
    the stop ends it lost on its own step (`PipeHolding.CloseAsks`): its line is answered, and the drain does not wait out its bound
    on it. A lone surrogate escape is no text (`JsonElement.GetString()` throws on it, on .NET 10 too): a line holding one in its id
    or key is no request and closes its connection, as any such line does; one in a string or name of its payload is refused `shape`
    under its id before any card, the connection kept (`PipeConnection`); and a page report to the log drops the value holding one,
    the rest of its line kept (`LogModule`). The log's writer writes a lone surrogate already in a string as U+FFFD. The host's own held asks (`PersonConfirmations`, in `src/Daoris.Service/Daoris.Service.Http/`) are bounded: five for asks naming a
    session, one more for the terminal's, two to an asker; an ask naming no session is bound by its process, read from the loopback
    connection (`AskerProcess`, Windows), one place each, so an agent leaving its session off cannot hold both of the terminal's. The
    page lists what waits at the service only where it holds the person key, as the shell tells it (`SERVICE_CONFIRMATIONS`); a browser
    holds none, so it lists none and never polls (`useServiceConfirmations`).
    (from D156, D196, D238)
15. A terminal's stop, finish, hold and resume with no card are the steward's acts alone: the person appoints the steward to the
    terminal and grants its acts in the window, and its terminal's press proved by the role's key makes them, and its other
    acts, outside Daoris's agents. Each use is logged and said on the steward's line in *Notifications*, and taking an act back
    holds from the next press, which asks as the person's own. There is no grant of the terminal's own beside the role door, and
    a stored one an older build left is removed at the start. (from D201, D209)

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
    frame, and a press in one disables the other. One pure rule (`work/waits/waits.ts`) says which waits stand. (from D168, D185)
39. There is no text editor. A file's preview is a read-only side-bar tab from the session's tree, or from the branch it landed on once its tree is gone
    (`SESSION_FILE`). It shows at most 256 KiB, and a path outside the tree is refused. (from D55, D111)
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

45. One primitive draws every flow (`RankedFlow`, which `LifeFlow` and `FlowChart` draw through). It is laid out in layers, deterministically, from the
    record and never guessed. By the pane's width it runs left to right, top to bottom, or as a rail. (from D186)
46. Edges are ink, never a status hue. The nodes are one ordered list with one Tab stop. The edges are one
    `aria-hidden` drawing. (from D186)
47. Every graph stands on one canvas: a viewport with pan and zoom, a minimap only past twice the viewport, and a list
    offered at every width, standing below 36rem where nothing else fits. No feature moves nodes or saves positions. The
    maps keep the viewer's view; one kept under a changed graph says the selection is gone. (from D224)
48. A placed world is HTML that CSS lays out and the canvas measures. A positioned world arrives with coordinates: past
    400 nodes only those near the view and the focused and selected are mounted, and below 4 px of `text-meta` the rest
    are dots. One engine, Daoris's own, serves every feature, chosen by measurement over React Flow. A step of a pan or a zoom
    draws again only what it changed: each card and edge is one memoized unit (`WorldNode.tsx`) held to primitive props, so a
    new scale redraws a card only where its counter-scaled word follows it, and the toolbar draws again only when the size it
    says, its bounds, its words or its tools change (`useStableActs`). The measured frame of 100 and 1,000 nodes holds its
    budget with that (`measured/large.spec.ts`). (from D224)

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
    process of its own, on a profile at `<home>/profiles/browser/engine`. The shell joins it to the `Browser` job as it starts, so it ends
    with the shell however the shell ends, and ends it where its bring-up fails. The engine
    is the kit's codecs package (`Shenora.Chromium.Codecs.win-x64`, its own build of CEF), so the browser plays H.264,
    AAC and HEVC, HEVC through the machine's decoder. The codecs are patent-licensed, and the package carries no
    licence. The modules' test project builds this application on Windows, and `CodecPlaybackProcessTests` plays a
    clip of each on that build, its page held shown: Chrome loads no media in a hidden page. (from D99, D85, D234)
55. Sessions drive it through an MCP server a plugin declares with `${browser}`, its loopback CDP endpoint. The
    person's Edge on a Daoris profile stays an option. Daoris keeps sign-ins across a restart itself. (from D78, D84)

**The setup guide and the views**

56. A first start opens Settings → Get started while an agent with an account, Daoris's own agent or a repository is
    missing. Each step reads its state off the machine and names its screen and its command. (from D97, D50)
57. Settings has eleven domains, each what a person comes to set, in this order: Get started; Appearance (look and
    language); Driver (how sessions run: its dials, this machine's access, the plugins' settings); AI features; Git; Tools;
    Browser; Disk space (the folders and the keep rules); Updates (the install's update, then its versions); This Daoris
    (the terminal, the service and the facts behind them); Machine log. A domain is a `<Name>Domain.tsx` and one row in
    `settings/domains.ts`, which names the rows the palette finds the domain by, each row's words in the reader's language.
    A row's label column holds its name and one line: a terminal twin or a variable name is in its ⓘ help (`SettingRow`'s
    `twin`), never under the label. Disk space's folders are one list, a row each: the name, the path on one line cut in the
    middle (`MiddlePath`, whole on hover and copy), the size and one *Open folder*; the home is shown there alone.
    A text of the driver or the service that names a Settings place says the domain's catalogue name (*Settings → Disk space*,
    *Settings → Updates*, *Settings → Machine log*) and never one domain inside another; `HelpRoomSettingsPlacesTests` holds it
    against `domains.ts`.
    (from D150, D75) This Daoris reads the wiring and
    sets it up from the page (WIRING1e): *Open* on a folder or file row by its id, *Put on my PATH* / *Remove from my PATH* on the
    account's `PATH` alone, *Copy a terminal line* (the page's clipboard, for PowerShell, Command Prompt or Git Bash) and *Change the
    service's address* (the account's `DAORIS_SERVICE_URL`, this machine's addresses only), each said first inside its row, each
    refusal data worded in both languages, and the wiring read again through the `wiring` scope.
58. Agents has two modes, Agents and Setup. Setup is the tree of places (machine, workspace, repository), each a page
    with a head and three folded sections. The tree (`setup/SetupList.tsx`) is a WAI tree: one row in the tab order, ↑ ↓ Home
    End walk it, → and ← open, close and climb, Enter chooses; a workspace opens by its chevron or →, apart from being chosen.
    A row's second line is a need (⚠ and its first reason), *Held*, *Adoption running*, or how it lands; the *Needs attention*
    chip carries the count of places in hand that need a look. The chosen place is kept across searches and pages
    (`setup/useKeptPlace.ts`). (from D221, D150)
59. An agent's page heads in one line. Each account is one row with one act by its state. An account is read only when
    the person presses or something has run, never on a timer. (from D152, D150)
60. Repositories lists by workspace. A repository's tabs are Details, Branches, History, Changes, Integration and Files (the files git does not manage there, with the person's rules and permissions); a
    workspace's are Details, Branches and Workflow. Their setup and rules are the place's, in Agents → Setup. (from D150, D147)
61. Knowledge is one place with two modes, Search (the default) and Convergence. The Map is a read-only view of the
    workspace's repositories and what moved between them, plus the code map, drawn on the canvas. The code map holds
    its modules in their folders, folded together or one at a time (each open folder's header has its *Fold*); a cycle, across folders too, is one card in the
    nearest folder holding it; its lines run around its cards, within a work budget of grid cells rather than a count of
    lines or cards. The cards are one list whose items say their place among all (`aria-setsize`, `aria-posinset`). Each zoom says less about the same cards: a repository,
    module or folder has its words at the full tier, its name alone at the chip tier and nothing inside at the dot
    tier, and the chosen and the focused keep their name beside the card at every tier. One choice stands forward on
    both levels (its lines and neighbours at full ink, the rest at 40%), is said once in one polite line under the
    drawing, and a double press counts as one press. A repository's detail has a door to its page; *Back to the map*
    from a code map chooses and reveals the repository and returns the focus. Find is offered from 13 items on both
    levels: it matches a repository's name and then what it says it owns and accepts, or a module's id and then its
    folder's path, says which in the result, and steps nothing while an input method composes. The list is one tree on
    both levels (`map/list/MapTree.tsx`, the WAI pattern of the set-up tree): one row in the tab order, each row's count and
    state in words, a code folder a group and a cycle a group under it. Below 36rem the list is the reading and the
    detail opens in place under the chosen row. *Which quests* is first the last 30 days, a choice kept stays, and with
    nothing chosen the line under the drawing says why no connection shows (all hidden, none in the window with what
    *All of them* shows, no work yet); a service that does not answer leaves a drawn map saying so, or the failed
    state, and a workspace with no repository has a door to Repositories. (from D150, D67, D224, D239)
62. Doctrine is never written from the window; it shows the command to run. (from D31)
63. Nothing the desktop starts outlives the process that started it. Every start through the gate names its purpose and is joined, as
    it returns, to that purpose's kill-on-close job, held for the process's life; a process ended early is ended by
    `OwnedProcesses.End`; the census reads each job by process id and stops nothing. (from D249)

### The processes Daoris starts

One management, `OwnedProcesses`, behind the start gate; the purposes and where each start is held:

| Start | Purpose | Held by | After a crash of its owner |
|---|---|---|---|
| An agent, plugin process or piece through the launcher (`AgentStarts`) | `Launched` | the agents' job, from its first instruction; a session's own job beneath | ended |
| The same where no launcher took it (a test's home, a refused job) | `Agent`, `Plugin` | its kind's job | ended |
| git's runs (`GitRuns`) and what git starts | `Git` | the git job | ended |
| A question read to its end (`ChildProcess`: git's, a version probe, a plugin tool's check), a usage read (`CodexUsage`) | `Read` | the read job | ended |
| An agent's install or sign-in, a plugin tool's set-up, a key made, the doctrine CLI, a server's package | `Tool` | the tool job (npm's run also in a job of its own) | ended |
| Daoris's own browser (`EngineBrowserHost`, started through the window kit) | `Browser` | the browser job | ended |
| The HTTP host (`HostSupervisor`) | `EndsItself` | none: it stops when its input ends | it stops itself |
| The update's launcher, the recorded Edge | `Outlives` | none, by design | running |
| The file manager; a terminal's shell | gate's `RunAsync` | the system's; the shell's own job from its first instruction | the shell ended |
| A heavy run's gate, a git act's step | direct, suspended | a named job of their own | ended |

A child is joined as its start returns, so what it starts in that instant is outside the job; a start that cannot accept that starts
suspended in a job of its own. Off Windows there are no jobs. The proof is `OwnedProcessesProcessTests` (an owner killed, its child and
grandchild found gone, against a control held by no job) and `ProcessStartsCensusTests` (each file's purpose, the starts and kills
outside the management with why).

The census is read once, as the application stops: `RunningProcessesLine` writes a `processes.running` line naming each purpose that
still holds a process, by id, and nothing where none does (`RunningProcessesLineProcessTests`). The tests' own direct starts are named
in `TestProcessStarts`, each file under a kind (a stand-in program, a Daoris program run whole, a kept host, an agent's command run bare,
a probe, git through a counting seam, a source that only names a start, a process object never started); git past `TestGit.Past` or
`TestGit.Ceiling` is `GitCeilingCensusTests`'s. Both desktop test projects hold it, and a start in an unlisted file fails.

## Where a change goes

- **A new page piece goes in its place's folder** (`overview/`, `quests/`, `projects/`, `map/`, `knowledge/`, `agents/`,
  `plugins/`, `settings/`, `help/`, `work/`, `workflow/`). Its test and story sit beside it. Frame regions go in
  `work/frame/`, the strip in `work/strip/`, atoms in `design/ui.tsx`, flows in `design/` and graphs in `design/canvas/`.
  A folder past twenty sources splits into named scopes (`test/structure.test.ts` with `structure.json`).
- **A new driver route** follows `.claude/knowledge/bridge-presses.md`. It needs the handler and its `[Publishes]`, the
  desktop README's row, the hook in `bridge/<domain>.ts`, its class, drain and owner in its marks (and its wording and grounds
  if `Person`), and its words in both catalogues. The page's route names, `PUBLISHES` and the person's presses' keys are
  generated from the marks into `bridge/generated/routes.ts`, which is never committed.
- **A new pushed slice** follows `.claude/knowledge/pushed-slices.md`: the `StateSlice` owner, its wakes, the
  `DriverLoop.Slices` entry, the page's `SliceSpec` and `ShellSignals`' `PUSHED`.
- **A wait on the person** follows `.claude/knowledge/person-waits.md`; **new words** `translation-parity.md`, plus a
  glossary row for a name. **A movable view** goes in `work/frame/placements.ts`; **a verb** in `menus/commands.ts`.
- **Tests that pin it.** Modules: `DriverModuleRoutesTests`, `StatePublisherTests`, `StateSlicesTests`,
  `PipeCensusTests`, `PipeWordingTests`, `RefusalCatalogueTests`, `ChromePaletteTests`, `WindowsModuleTests`,
  `WaitingBadgeTests`, `SessionBalloonTests`, `EngineBrowserTests`. Web: `test/structure.test.ts`, `design/{tokens,presentational,siblingKeys}.test.ts`,
  `design/pageMeasure.test.tsx`, `locales/{i18n,names,note,glossary,
  shippedNames}.test.ts`, `menus/commands.test.ts`, `menus/ContextMenu.test.tsx`, `bridge/state/{stateHub,slices}.test.*`,
  `app/pushedState.test.tsx`; `e2e/platform.spec.ts`; `measured/*.spec.ts`.
- **The gates that bite:** `web-types`; `web-rules` (tokens, structure, provider census, presentational, page measure);
  `web-unit`; `web`, whose build runs `i18n-check`, `names-check --strict`, `tsc` and `vite build` before Playwright
  over the HTTP host; `web-measured`; `modules`, `modules-process` and `censuses`; `driver` for Ask Daoris's coverage.
  A story or a mocked bridge is no proof of the installed window.

## Contradictions

1. D55 (two frames, Manage ⇄ Work) vs D66 §1 (one frame): D66 is right; `menus/commands.ts` has one list of views (`VIEWS`).
2. D56 (six domains in the bar) and D66 §1 (a six-item bar with Convergence and Search) vs D150 §2 (nine places): D150 is right, per `VIEWS` in `menus/commands.ts`.
3. D75 §1 (menus Daoris · Workspace · Agents · View) vs D152 §1: D152 is right, per `MENUS` in `menus/commands.ts`.
4. D150 §2 (refresh and language move to "the Daoris menu") vs D152 §1 (there is no Daoris menu): D152 is right.
5. D75 §2 and D150 §2 (seven Settings domains, without This Daoris or Git) vs `SETTINGS_DOMAINS` in `settings/domains.ts` (nine): the code is right.
6. Code vs code: Ask Daoris's twins (`PLACE_DOMAINS` in `help/places.ts`, `HelpPlaces.Domains` in `Driver/Help/Proposals/HelpGoProposals.cs`) omit `tools`, so no go opens Settings → Tools. Intent unclear.
7. D150 §6 (*What needs you* leads Overview) vs D196 §5 (Overview keeps a summary): D196 is right (`overview/OverviewView.tsx`, which draws a summary line per group, and `app/shell/useViewLayouts.tsx`).
8. D30 (convergence is the knowledge half's primary view) vs `knowledge/modes.ts` (`readKnowledgeMode` falls back to Search) and D40 (Overview is the landing): the code is right.
9. D41 (a right drawer is the single detail-and-form surface; global state sits in the sidebar) vs D118 §3 (a record opens in the main area) and D56 (the status bar): D118 and D56 are right.
10. D41 and D42 (toasts carry the service's sentence verbatim) vs D142, D182 §2 and D185 R3 (coded verdicts worded by the page, said where the action started): the later ones are right. An unknown code still shows the driver's sentence.
11. D42 (TanStack invalidation after every mutation) vs D169 §6 and D182 §7 (one state hub by declared scope, plus pushed slices): D169 and D182 are right (`bridge/state/stateHub.ts`).
12. D169 §3 (an event never says what a thing is) vs D182 §2 (pushed with its data): both run. D182 governs the 16 slices, and `STATE_CHANGED` covers the scopes not yet moved.
13. D78 (a framed WebView2 window of the shell) and D84 (Daoris's UI stays on WebView2; the engine is fetched like a harness) vs D85, D92 and D99: D99 is right (`App/Program.cs` hands a browser start to `BrowserProcess` first; the kit pins CEF in `App/Daoris.Desktop.App.csproj`).
14. D85 (the browser renders inside Daoris's windows under Shenora's frame) vs D99 (a process of its own in Chromium's windows): D99 is right (`App/Program.cs` registers `EngineBrowserHost`, a process of its own).
15. D186 §2 and §9 (no pan, no zoom, laid out by hand) vs D224 (one canvas with pan, zoom and a minimap): D224 is right (`design/canvas/Canvas.tsx`, `design/FlowCanvas.tsx`, which draws `RankedFlow` in the canvas's viewport).
16. D185's and D186's heads say "proposed"; their notes record acceptance on 2026-10-20: the notes are right.
17. D97 (six steps, with "what is driven" and "how work lands" apart) vs `SetupStepId` in `help/setup.ts` (five steps; both are one `setUp` step): the code is right.
18. D116 (names and budgets in English and Chinese only) vs D222 §3 (any number of catalogues): D222 is right. `names:check` holds every catalogue and each `term.<tag>`.

## Open

- **CANVAS1g**: the canvas in the installed window, with FLOWCHART2h.
- **SETUPUX1x**: the set-up experience seen in the installed window, and its fixes.
- **AGENTSPLACE1l**: the Agents place seen in the installed window.
- **FLOWCHART2h**: the workflow editor seen in the installed window.
- **MAPUX1**: the Map's UI and UX (`docs/2026-11-06-map-ux-design.md`). Built: the tiers' content, the choice forward, its
  line and the detail's door to the repository's page, *Back to the map* choosing and revealing the repository, find on
  the level shown, the list as one tree, the empty and failing cases, and *Which quests* opening on the last 30 days.
  The Tab order is measured on both levels (`measured/mapTab.spec.ts`: the drawing one stop, the list one row). Open: the
  installed window (MAPUX1g).
- **MEETUX1a–d**: meetings and brainstorms in Sessions: started, followed, read, and in *What needs you*.
- **PERSONDOOR2** (no row filed): the boundary between the person and their agents that D156 §7 names, and the machine
  key moved off a session's connector.
- **OPTIMIZE1b**: D249 is accepted and built, every test start named with its purpose and the census read as the process stops
  (`RunningProcessesLine`). No door reads the census while the window is open.
