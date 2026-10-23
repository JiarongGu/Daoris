# Daoris.Web — the platform: the person's window over the family

React application over `Daoris.Service`, served by `Daoris.Service.Http`. It is the shared UI for
the browser and desktop. [The contributor guide](../../docs/development.md) gives dependency setup;
[the router](../../docs/README.md) locates the frontend, frame and UI contracts.

## What it is

One activity bar (D66), landing on management (D40). The views that read the family:

| View | What it answers |
|---|---|
| **Overview** | the landing: is anything sitting and for how long, the family's health as tiles, the repositories by what the index holds |
| **Quests** | what has been asked of whom, grouped by where it is in its life; publish, take, done, decline — and, beside a driven quest, its session **record** (D46): state, adapter, note and evidence, read-only in a browser, with stop offered only where a shell's driver actually holds the process |
| **Repositories** | repositories and workspaces, their ownership, setup and driver controls; workspace pages carry wiring, lines, landing and permissions (D150). A repository's Setup and a workspace's Defaults show and set each one's process (DOORS1b, D164): *Commits* beside how work lands (as it goes, each after your yes, or once at the end, and the person's convention quoted as written), the branch rule's `{ticket}` key and squash, sent whole so a save keeps what the terminal or Ask Daoris set, and *Helpers* on Sessions (wait for your yes, start at once, or not offered, and the agents in order), each saying what it inherits and where it was set. A repository's Setup also holds *Heavy gates* (HEAVYRUN1b2b, D180): this machine's builds and suites for a repository whose own `daoris.gates.json` marks none, each a name, a command and a folder, edited whole in the order they run and saved whole or cleared, the driver's refusal said under Save, and saying whether the repository's own file wins, as the driver read it (`HEAVY_MARKS`, HEAVYRUN1b2c: it marks some and this list is not run, it marks none and this list is used, or cannot tell, in the driver's words). In the desktop a repository's Branches lists its branches by kind and History draws its line's graph a page of 200 at a time (*Show more*), or over every local branch and origin's from its ⋯ *All branches* (page state; at most 256 tips, the head says when it drew the first of more); a workspace's Branches groups its repositories' branches above the clean-up, its repositories holding none folded apart once, the include serving the groups and *Updates*' look (D147, D150) |
| **Knowledge** | Search and Convergence are two modes of one view: find entries or compare independently learned lessons |
| **Agents** | installed agents and accounts: names, sign-in state, each usage window as a cell in the same place on every row (its share, reset and reading's age, or *unknown*), and actions; an agent's accounts of Daoris's are one set every workspace's work may run on, in one order with one set of use settings, its fold's receipts said above the rows until *Got it*, and your own sign-in a row of its own outside them, never asked (D197, POOL1c); desktop only. An account's window cell (in the warning's hue), its said line and a start's note say an agent's own pace warning as *ahead of its pace*, never as near its limit, and the warning alone never makes a scope's near or the hand-over offer (D165, USAGEREAD3b). A start's note says an account passed for its use as *over its curve* with the window, its use and the curve's value now, and the account chosen for its room as *the most room under its curve* (D165, SWITCH1b-ii). The next start's reason and holds say which passed an account, its limit reached, usage credits or its use over its curve on a window, and its walk ranks by the most room under the curve, by the week's pace with the switch off (D165, SWITCH1b-iii). A conversation moved to another account by the curve says so at both ends, each account by the name the person gave it, with the window, its use and the curve's value, and a hand-over's why says *its account was over its usage curve* (D165, SWITCH1c-i). The low-allowance hand-over offer is the fallback: none where the switch is on, the conversation's account is over its curve and another account of its agent has room, since the switch moves it at its turn's end (D165, SWITCH1e) |
| **Plugins** | this machine's plugins and offers; desktop only |

**Sessions** is the working surface — the agent sessions this machine runs, attended one at a
time (D55, `docs/2026-09-21-working-surface-design.md`) — and exists only in the desktop, because a
stream never leaves its machine. **Map** is how a workspace's repositories are wired (MAP2).
The session's Workflow view names its bound saved version, or Current for live declarations, and the route that
chose it (FABRIC-PIPELINE1a). Unreadable bindings show a problem without a completed diagram. Both the head and diff pane
show workflow holds before Accept, preserving the driver's explanation and available terminal doors. A repository's and a
workspace's Workflow tab draws its start node first, laid out by `Flow` (`workflow/WorkflowStartNode.tsx`): each route, the
default, each kind by its label and paths, and the landed work, wired to the workflow it names; the landed route says where its
gates stand against the line's tip and offers the person's *Accept the gates* (`WORKFLOW_LANDED`). A repository's Setup holds
the same route as a row of *Line and landing* (`settings/repository/LandedRoute.tsx`).
A second opinion's reviewer is a row of its own in Sessions' list, and says whose work it reads (XAGENT1g4c): the rail
joins it to the working session through `GET /api/opinions`, asked only when the list holds a reviewer, and an unanswered
read claims nothing. A session that continues another, and a helper, say so (MIXAGENT3a-i): the head's facts name the agent
continued and why, in the hand-over's words (a carry-on, a fallback and a lost conversation's new part among them since
SESSCTX1b, which the *Parts* strip and its flow read by the same why: carried on, retried, or *moved on*), or *helping* and the
session's title; a new part's first prompt is shown as Daoris's brief of the session it continues, folded with its account, and
another session's channel message is told by its event's `channel` mark before the claim's opening; *Details* holds *Continues* and *Helping*
with a door to that session, or its id where the list holds none; the row says *continues* or *helps* and the title, from
the record's own `from` and `serves` joined in the rail; and a helper is titled *Helper* and, as a reviewer is, offered
no box. Where a session's words wait for the person on a reason that leaves its tree to go on in (SESSCTX1c part B, `BRIEF_WAITS`),
the line saying they cannot go on offers *Go on with Daoris's brief* (`SESSION_GO_ON_BRIEF`) beside *Start a conversation with these
words*, on a session's page and in Ask Daoris's dock; the new part is attended once it opens, and a refusal is said by its code
(`GO_ON_BRIEF`, twinned with the driver's `GoOnBrief`). Words that went with a quest's resume to its new session say so on the
session they were said to, by the went line's `resumed` reason (`work.say.why.resumed`, SAYHELD1). Review reads a session's work against its line (REVIEWBASE1): the head says, in one line, when the latest line the
host knows has commits the work does not, naming *Updates* on the workspace's *Branches*; a tree holding nothing beyond the
line says so where *Nothing landed* would be false. Such a tree has nothing to land (REVIEWBASE4): the foot says so where
*Accept* would be, offers neither *Accept* nor *Send back…*, and *Discard tree* leads; the head names the branch the tree
stands on once the review answers, over the record's `daoris/` guess, and *detached HEAD* where it stands on none.
With nothing attended, Sessions asks what to work on (NEW1b, D162): *New conversation* takes the words, a repository, a
tree and an agent with its account, and opens the conversation on them. Ctrl+N and Workspace › *New conversation…* open it.
The repository list's foot adds one or imports a folder (NEW1c) through Repositories' own drawers, opened over Sessions with the
words kept; what is added is chosen, and the prompt takes the focus back when a drawer closes. Ask Daoris's go to *New
conversation* opens it with the repository chosen and the words in, above any already typed, sending nothing (NEW1d-b); the
composer's quiet *Make it a task with Daoris…* hands its draft, under a line naming the repository, to Quick Ask as a draft
the person sends. A browser has no composer: Ask Daoris is its door.
**Settings** is everywhere: Get started, Appearance and AI in a browser; the desktop also offers
Driver, Tools, Browser and Logs. Plugins has its own view. Agent accounts live in Agents; workspace controls live in
Repositories. The desktop's seven menus share one command table, with shortcuts and a command palette
(D152). Help → Update opens the update card in Driver. The desktop's zoom (ZOOM1, the UI/UX design §3.11) scales the content by
the page's root size in an editor's steps, from View's rows, their keys and Ctrl+wheel, while the title strip, the activity bar,
the status bar and what they open wear `frame-held` and keep their size; the level is `design/zoom.ts`'s, kept per machine as the
theme is, and said by `work/frame/ZoomReadout.tsx` and the status bar. Glyphs are drawn in rem (`Icon`, `glyphSize`) so they follow it.

Each Settings domain is `src/settings/<Name>Domain.tsx`, with its tests beside it, and
`src/settings/domains.ts` lists them: the one place a domain is added, since the frame
(`settings/SettingsView.tsx`) renders whichever one is chosen and names none itself (MOD4).
*This Daoris* (`WiringDomain.tsx`) draws one read, `SYSTEM_WIRING`, as cards (`wiring/Wiring*.tsx`, molecules over props); its card
*Left in your own tool home* (`wiring/WiringToolHome.tsx`, OWNSPACE2) lists the folders Daoris's session records show its sessions
ran in, read-only: Daoris removes none of them yet, and the card says a person can by hand. The *Downloads* row's clearing
(`wiring/WiringDownloads.tsx`, CACHECLEAR1) is the page's one act: `SYSTEM_DOWNLOADS` lists the agents' and tools' downloaded versions
with what keeps each, read only once the row opens it, and `SYSTEM_DOWNLOADS_CLEAR` removes those nothing uses, saying what it kept.
The list also carries the downloads' rule (`days`) and, for each version nothing uses, the day the rule takes it (`goes`), which the
row says beside it; a version a running session was handed is kept (`running`, KEEP1b).
*AI features* (`AiDomain.tsx`) draws Ask Daoris's *Configured access* (`help/access/ConfiguredAccess.tsx`) under its access row
while restricted: each grant the driver serves comes from `STATE`'s `helpGrants`, with the operations it acts on (`built`), and is
pressed over `SET_HELP_GRANT` one operation at a time; an operation not built is drawn disabled, and a grant the driver does not
serve yet is drawn from `help/access/grants.ts` alone and sends nothing (HELPMODE1c2b: housekeeping is served; HELPMODE1c3: the
chosen settings too; HELPMODE1c4: the checkouts, whose boxes are the readable checkouts the driver serves, each by its repository's
name, on until turned off, held on every agent since Daoris serves their read). The browser's grant is its plugin's approval, set on that plugin's tab alone: its row draws the driver's
read of it with no boxes, and its *Manage* opens the plugin's tab, or Plugins where no plugin offers it. A grant's *next turn*
mark is the driver's `pending`, while a conversation running was handed another choice; the panel keeps a conversation the driver
names in `helpSwapping` in view as it ends, so the next words resume it under the new access (ACCESSSWAP1).

**An account is named by one rule, wherever it is said** (`src/agents/agents.ts`, D125's ACCTNAME1 and ACCOUNTID1b notes):
`accountName` leads a row, `accountWho` sits beside it, and `accountLabel` is the one line a chip, a pill, a note or the
roster's namer (`settings/accounts/namer.ts`, so a session's header) says. Where two of an agent's sign-ins share an email, each
says what tells it apart, after the email and once. A surface that names an account goes through these, never its own.

**A step that waits says its phase and how long** (the UI/UX design R2, SIGNIN1): `src/design/phases.tsx`'s `PhaseList`
draws a process's phases, each named by its words and its state in words, the done ones with the time each took, the one
now with its time stepping by its own clock (`Elapsed`, seconds in the first minute and minutes after) and what it says, a
failed one with its sentence; standing open on its own surface, or as the line alone in a row. A sign-in's phases are the
driver's, lines of its relay read by `phaseOf` (`bridge/sessions/console.ts`), and `agents/SignInSteps.tsx` draws them
under `SignIn`, the link, the code and the ask read from the tool's lines by their shape; a host that writes none draws the
steps as before, with the time since the press. A sign-in that ends without signing in says so in its panel with *Try
again* and *Close*, and one the person cancelled closes saying nothing.

**The design language is written down** — `docs/2026-09-19-platform-ux.md` (D41): the console shell
(an activity bar, page headers, one primary action per view), the token system, the drawer as the single
detail-and-form surface alongside the frame's list and main panes (D118), toasts carrying the service's sentences verbatim, and a status palette that
was **computed, not tasted** — both themes pass all six checks of the visualization validator, and a
status pill never appears without its text label.

**An act that ends, removes or reaches outward asks once, in the page** (platform UX §4, UXFIX2):
`src/work/InlineConfirm.tsx` is the one ask. Its act takes an `Answered`, so the ask stays open while the act
runs, says a refusal inside itself rather than in a toast, and returns focus to the press. A question the agent
asks in the page (the trust question, a go-ahead) holds its own wait the same way and says a refusal beside what
was pressed (UXFIX2b3b). A search gate asks
`src/lib/searchable.ts`: two characters, or one Han character, as the driver's own rule does (D158). A page opened from a
press (a branch's page from its name, the code map from *Open code map*) goes back with the focus on that press, waited
for until it is drawn, else on the page's heading: `src/lib/returnFocus.ts` (UX6h3d).

## Built on (D42, `docs/2026-09-19-frontend-architecture.md`)

| Layer | Choice |
|---|---|
| Styling | **Tailwind v4** — the validated tokens are the theme (`src/tokens.css`), utilities live beside the markup |
| Primitives | **Radix UI** — dialog (the drawer), toast, select, tooltip, checkbox: behaviour without a look |
| Icons | **lucide-react**, tree-shaken |
| Server state | **TanStack Query** — deduped fetches, refetch-on-focus, invalidation after every mutation; a driver press's is the driver's `STATE_CHANGED`, asked again at one place in every window (`bridge/state/stateHub.ts`, D169), so a hook asks nothing again itself and a test that presses a declared route mounts the hub over a mocked shell that publishes what the route declares (`test/publishing.ts`); a press under way is read by every view through `useUnderWay(route, session)`, never one component's `isPending`, and the hub reads the driver's list as it mounts, so a window opened mid-press says it too (STATEHUB1c2); Ask Daoris's open *What it has* is asked again on the `help` scope, so a receipt recorded meanwhile shows (HELPWIREDLIVE1) |
| i18n | **react-i18next**, `en` + `zh`, flat dotted keys, **one file per area** (`src/locales/<language>/<area>.json`, merged at load by `src/locales/index.ts`): a key lives in the file named by its longest dotted prefix that has one, so `settings.rules.add` is in `settings.rules.json` and `nav.quests` in `nav.json`. `scripts/i18n-check.mjs` fails the build when an area's two files diverge, a key is in two files, or a key is not in its home (MOD2); `scripts/names-check.mjs --strict` beside it fails the build when a label breaks the glossary (`src/locales/glossary.json`), its kind's form or its door, and reports the budgets without failing (D116, NAME1b) |
| Shell bridge | **@shenora/react** (0.22, with the shell's packages: SHEN4, D22) — in the desktop, `DAORIS.DRIVER` carries the person's controls and `DRIVER_TICK` pushes the loop's reports into refetches, its lines never toasted (NOTIFY2h: they are the machine log's `look.said`, news a notice) (`app/ShellSignals.tsx`, which says no toast for a conversation's answer when that conversation is the one attended in a window in front, NOTIFY1c), which mounts the state hub (the running call each session makes arrives as its `calls` slice and is laid over the pulled `calling` on the rail and the head where live, WAITSAY1c2; the work milestones arrive as its `work` slice, which `useWorkState` reads live at its widest key and selects a unit's variant from, so a tick and a focus ask no `WORK_STATE`, PUSH1c2b; the session groups arrive as its `groups` slice, composed in the driver's reader's order, which `useSessionGroups` and `useSessionEntries` read live at its widest key and select some sessions' variant from, so a tick and a focus ask no `SESSION_GROUPS`, PUSH1c3b; where each wired circle stands arrives as its `sync` slice, one entry by circle, which `useSyncStanding` reads live and selects the asked circle from as `/api/sync` answers it, a circle with no partition `wired: false`, so a tick, a pass's scope and a focus ask no `/api/sync`, PUSH1a2; the clean-up's list arrives as its `sweep` slice, a partition per repository, which `useSweepPlan` reads live, so a tick and a focus ask no `SWEEP_PLAN` and the pull-request plugin is asked only at the Branches tab's *Look again*, the person's press of it, `useSweepLook`, PUSH1c4b); in a browser none of it mounts, by design, and the standing stays its HTTP pull. **One file per domain** (MOD3): each domain's types and calls are `src/bridge/<domain>.ts`, or `src/bridge/<scope>/<domain>.ts` in one of its scopes (*Where things are* below), `src/bridge/shell.ts` is only the barrel re-exporting them, and `bridge/bridge.test.ts` holds that it stays one. The desktop answers each route in a handler class per domain, `<Domain>Routes.cs` in `Daoris.Desktop.Modules` (D166), which is the file a comment here names for a route's refusals |
| Design tool | **Storybook** (`npm run storybook`) — the component states and the token gallery, on the shipped code. A main-area organism's stories are drawn at the frame's three widths by one helper, `design/storyWidths.tsx` (the UI/UX design R6, UIFIT1): wide, half and the 400 px floor, with the page's own gutters and no gap, cap or padding of the story's own; a session's page's parts by `sessionWidths`, in its own gutters; its test names the few stories of other regions (the list column, the side bar, the composer's row) that keep a frame of their own, each with where it is mounted, and fails on any other (STORYGAP1). What a story claims of geometry is measured in a real browser under `measured/` (Playwright over Vite, `npm run e2e:measured`): the flowchart's boxes and, since UIFIT1, every setting row's value against its card's label track (`SettingList` and `SettingRow` in `design/ui.tsx`, the cards spaced by `CardStack`) |
| Test loop | **Vitest + Testing Library** as the millisecond inner loop (view logic, primitives, catalogs — with the sibling's proven jsdom shims), **Playwright** as the outer loop — the real host over `examples/`, driving the shipped bundle. `npm run test:web` at the workspace root runs the whole pyramid |

**The i18n boundary:** UI chrome translates; **data does not**. Quest content, registry declarations,
knowledge bodies and the service's own sentences render verbatim — machine-translating a refusal would
break the contract that the service's sentence is the message. `zh` is 简体, in a console register
(委托 for quest — deliberately not 任务, keeping the family's own distinction).

## The landing is management; convergence leads the knowledge half (D30, D40)

The platform lands on **Overview**, because its first job is the person's first question — *is
anything sitting, and for how long* (D40). D30's measured finding stands inside the knowledge half:
**search must not lead it**, because the finding that mattered most was a convergence between two
repositories whose vocabulary overlapped by **25%**, and no search could have surfaced it — to search
for it you must already know it exists. So Convergence leads the knowledge views, and Search follows
for when you know what you are looking for.

The similarity threshold is a slider rather than a constant. Measured on this family, 0.82 returns
nothing, 0.70 returns the true pairs, and 0.60 begins pulling in unrelated documents — a default nobody
can move would be wrong for someone.

## Doctrine reads; service state writes (D31, D38)

No editing of doctrine from the browser, ever. Where a rule should change the UI shows what to run in
the repository that owns the file, because `upstream` deliberately routes an improvement through the
repository that found it, where it meets that repository's review. The convergence detector already
states this for itself: it proposes, a person disposes, and a candidate is a prompt to look rather than
a merge (D21).

Quests are a different kind of thing — service state (D32), already writable over the HTTP surface
(D36) — and under the automation-first model, **filing a quest is how a person sets a target** (D37).
So publish and respond are in the UI, through the same endpoints and the same `QuestExchange`
judgement as every other door, with refusals shown verbatim. A shared deployment (D47) serves no page
at all — the remote is an API until person-auth exists, and the person's window stays the desktop over
its local host; locally — the default — the full surface works.

**An ask's channel is the desktop's** (SESSONE2c, D167): where an ask's sessions and the person talk is read, posted to and marked
read through the driver's `CHANNEL_READ`, `CHANNEL_POST` and `CHANNEL_MARK` (`bridge/sessions/asks.ts`), so a browser draws no *Channel*, no
unread count and no composer. The ask's page, each session's panel under its head and the unread counts on the ask's row and in
*Group by ask* are molecules over those answers (`asks/ChannelSection.tsx`, `work/channel.ts`), with Daoris's lines worded from
their codes in the reader's language and the messages shown as written. **A teammate's ask** (SESSONE2h) has a row in the Asks list's
*Teammates' asks* group, from the driver's `ASKS_ELSEWHERE` (`useElsewhereAsks`), and a page of its channel alone, read-only
(`asks/TeammateAskPage.tsx`): this machine holds no record of it, so it has no title and none of an ask's sections. **What a
message reached** (COWORK1d, D188): an urgent message names its level in a word, each message lists its receipt per repository it
addresses, and where it waits there or never reached it, *Send to its session* (`useForwardChannel`, the driver's `CHANNEL_FORWARD`)
sends it on, the person's own press, on the ask's page and a session's panel alike; a teammate's channel offers none.

**Knowledge results identify the active tier** — a reader looking at results has no way to
know the semantic half was absent, and would read them as complete rather than as
complete-for-word-overlap (D24).

## Where things are in `src/` (STRUCT5c, D166)

The top holds the entries alone: `main.tsx`, which `index.html` runs, the theme `tokens.css` it and Storybook load,
and Vite's ambient types. Everything else sits in a folder by domain: `app/` (the frame, its windows and the opener),
`data/` (the service's API, the queries, the host, the workspace scope, the tick's news), `bridge/` (the shell's calls
by domain and their barrel), `design/` (the atoms in `ui.tsx`, the mark, the theme, the links, and the design's
tree-wide gates), `lib/` (helpers no domain owns), `locales/` (the catalogues, i18n and the formatters), `menus/`
(the command table, its shortcuts and the menus), each place's own folder (`overview/`, `quests/`, `asks/`,
`projects/`, `knowledge/`, `map/`, `agents/`, `plugins/`, `settings/`, `help/`, `work/` for Sessions, `workflow/`,
`compose/`, `update/`), and `test/` for the tests' support. A test or a story sits beside its source.
`src/test/structure.test.ts` holds this with its map, `structure.json`: a new file at the top, a folder no module
declares, a test apart from its source or a folder's top past twenty sources fails it, unless the map's `wide` list
names that folder until a row cuts it into scopes. A gate that reads the whole tree globs from the project's root and
names each path from the sources' top (`test/sources.ts`). A view in a folder the presentational boundary covers is an
organism by name in `design/presentational.test.ts`. Two children of one parent never share a key (RELSPAM1): React
matches keys across all of a parent's slots, and a copy left behind stays in the page at every draw. So a child keyed by
a record names its kind beside the id (`relations:${session.id}`), and `design/siblingKeys.test.ts` holds every shipped
parent to it.

A folder past twenty sources keeps its scopes in folders named for what they hold, its tests and stories beside them
(STRUCT5c-ii, D166):
- `projects/`: the place's view, its list, a repository's page and the drawers at its top; `git/` (a repository's
  Branches and History, a branch's page and the graph), `setup/` (a repository's and a workspace's Setup, its parts and
  rules, the standing answer) and `workspace/` (a workspace's page and its sections).
- `settings/`: the frame, the domains' list and each `<Name>Domain.tsx` at its top, with the parts one domain draws;
  `wiring/` (This Daoris's cards), `accounts/` (how each agent's accounts are used, the usage curve, the roster's namer)
  and `repository/` (what a repository's Setup, a workspace's and this machine's set, each where it was set, and the
  agents' rules).
- `bridge/`: the barrel, the domains' helper, the driver's standing state and the machine's own domains at its top;
  `sessions/` (a session's life, conversation, console, tree, entries and trace, an ask's or a quest's work, its clear,
  and an ask's channel), `repositories/` (a repository's line and rules, Git's reads, reading across, set-up, workflow,
  the registry's machine half and the remotes), `agents/` (the agents, their accounts, rules and tools) and `state/`
  (the state hub, the pushed slices and what the driver publishes).
- `work/` (Sessions): its view (`WorkFrame`), the attended session, the acts on a session and the model every scope
  and place reads (identity, the list's groups and entries, an ask's channel, a note's words, the one ask) at its top.
  A note's words come from one map per reason family (`noteLines.ts`), each held key for key to the driver's `NoteCodes` by
  `locales/note.test.ts`: a helper's, a hand-over's, and a partner's piece (COAGENT1b), whose notes on the lead and the
  partner are notes, never words, and reset nothing. A hand-over's note names who asked it (the person at a terminal, the
  window or the agent) and says one Daoris could not take as not taken, with its reason, never as refused (HANDOVERSAID1);
  `frame/` (the window's frame: regions, lists, the dock, a page's header, the status bar), `strip/` (the app strip:
  its menus, command center and palette, the browser's door), `rail/` (Sessions' list), `head/` (a session's head,
  timeline, trace and milestone), `conversation/` (the conversation, its composer and box), `review/` (a session's
  review: the diff, the patch, the landing, the file preview), `gates/` (a landing's review and second-opinion gates),
  `waits/` (what waits on the person, and its answers), `attention/` (*What needs you*), `start/` (starting a
  conversation, a hand-over or a helper), `console/` (a session's console and streams, the monitor, a detached
  session), `terminals/` (the person's terminals), `history/` (clearing finished work) and `pausing/` (pausing and
  abandoning it).

A take the person's own reopen made lose is said as that wherever a lost claim is read (FABRIC-CLOSE1c20): the hand-over's
`quest-reopened`, the abandon's `source-reopened` and the pause's `asks.work.reopenedTake` each have their own sentence,
never the one for a take on another machine.

## One UI, two shells

This app is the **only** UI. It is served over HTTP for the browser, and `Daoris.Desktop` carries the
same build in its embedded Chromium — the same bytes a browser gets, plus the shell's capabilities (the driver
controls) that only exist where a driver does. Two shells, one codebase; a second hand-written desktop
UI would be the same divergence problem in a new place.

The viewer's chosen theme reaches what the shell paints too. Each window's page tells its own frame the theme it is
in (`SET_THEME`, and `DAORIS.WINDOWS` for a secondary window, WINDOW2), and tells the engine the choice itself over
the kit's colour scheme (`useEngineTheme` in `bridge/windows.ts`, THEMESCHEME1), so Chromium's own UI and
`prefers-color-scheme` follow an explicit light or dark, and *System* still follows the OS. Each main window's page
also tells its own frame its workspace's *What needs you* count (`useTaskbarCount` in `bridge/windows.ts`, `SET_BADGE`,
NOTIFY1b, NOTIFY2c), another main window by its name, which that frame wears on its taskbar button and in its title; a
utility window's page never does. Each count carries a later `order` than the one before, so the frame never wears an
older count over a newer (SESSBADGE1). Its title carries the workspace it holds as data (`SET_TITLE`'s `workspace`),
from which the shell picks the window a notification opens, naming it on `ATTEND_SESSION` so that window alone attends.
A main window's page also hands the shell's notifier the notices' words in its language, at its load and at each change of
language (`useNoticeWords`, `SET_NOTICE_WORDS`, NOTIFY2d): every key under `notifications.system.` and `person.route.`,
placeholders kept, so a balloon behind the window speaks as the window does; the window's own toast for the same attention
is worded from the same keys (`attentionToast` in `notifications/noticeWords.ts`).
That count includes a plugin step's attempt held for the person, which `WORK_STATE` lists as `held` and its row settles with
their word (`useResolveHeld` in `bridge/repositories/lines.ts`, `WORK_HELD`, FABRIC-PERFORM1g), a node record that does not read,
which `WORK_STATE` lists as `unreadable` and its row's press opens the folder of (`useOpenWorkRecords`, `WORK_RECORDS_FOLDER`,
FABRIC-PERFORM1j), a plugin step's failure Daoris will not retry, which `held` lists too and its row's *Try again* restarts
(FABRIC-PERFORM1k), a close found closed only in part whose close node waits again, which `WORK_STATE` lists among its `ends` and
the same door stops (FABRIC-CLOSE1j), and a tool call a session holds for
their answer under asking (`usePermissionWaits` in `bridge/sessions/sessions.ts`, `PERMISSION_WAITS`, AUTONOMY1b3c), answered
with a choice its agent offered on the session's head and foot and on its row (`useAnswerPermission`,
`SESSION_PERMISSION_ANSWER`); neither route publishes, so the page asks again itself: as a pushed call's `waiting`
mark comes and goes where the calls are pushed, and every few seconds on a host that pushes none (D184's AUTONOMY1b3c
and AUTONOMY1b3b notes). The rail's row and the head say such a call waits for the person's answer, timed from the hold.
It includes, too, a third answer in a row with nobody between, which the answers-alone bound holds for the person (`useAnswerWaits`
in `bridge/sessions/sessions.ts`, `ANSWER_WAITS`, COWORK1e, D188 point 4), answered with *Go on with this answer* or *Not now* on the
session's head and foot and on its row (`useAnswerAlone`, `SESSION_ANSWER_ALONE`, which publishes the sessions and the driver).
A place of the activity bar counts only what waits on the person there (BADGEUX1, the UI/UX design §3.10): the bell's rows whose
door opens it (`placeCounts` and `placeOf` in `work/attention/attention.ts`, a unit of work by its own door), so each place's
number is the bell's for the same rows. Work under way is a mark, never a number: Sessions' while the driver's sessions run and
are not parked on the person (`useAttention`'s `running`, the `working` partition less the `sessions` partition's parks), Quests'
while a quest is taken. The status bar says the cap's count, the `working` partition (SESSBADGE1, D76's note).

The build outputs into `../Daoris.Service/Daoris.Service.Http/wwwroot`, so the page and the API share one
origin. That is what makes CORS unnecessary in a real deployment — the `DAORIS_WEB_ORIGIN` variable
exists only for the development server on another port, and it names an origin rather than wildcarding.

A quest's flowchart is the same in both shells (FLOWCHART1a2, D186): opened, its section reads the quest's moves from the
service's own `GET /api/quests/{id}/moves` (`useQuestMoves` in `data/queries.ts`), which either host answers, so a browser
draws the way a quest went as the window does; *How this came to be* stays the shell's, since its trace reads this machine's
records. A host from before the door answers it not found, and the flowchart draws the statuses alone and says so.

A session's states are drawn at the head of the side bar's *Timeline* view (FLOWCHART1b, D186): `work/head/sessionLife.ts` holds
the page's copy of the ledger's `SessionLedger.Allowed` and its nine states, which its test reads from the service's source,
and draws the four live states, the working ⇄ awaiting-person return on a lane of its own, one node of outcomes and going on
as a guarded edge (refused, with its reason, for a record that can never go on). The record keeps where a session stands and
not how it got there, so the drawing lights the state it is in, in the tone of the word the page shows it as, and draws no path
and no time.

A session's parts are a flowchart in the parts strip's place on its page (FLOWCHART1b): `work/head/partsLife.ts` hangs each part by
what it followed (`EntryPart.after`) and labels the edge by how it began, an asked part from a faint node for the repository that
asked it and a part with no `after` from the start, and draws Accept's plan, once its ask is open and read, as a dashed node of
where the parts *would* land. Parts are lineage, never a cause or a concurrency, and a part keeps no time, so its agent and when it
began are said only where the page holds that part's record. A move and a hand-over name the agent they left where it is another
than the part's own, and a hand-over its why by the part's `fromWhy` (SESSCTX1d); each part's row and node say what it spent, its
turns' tokens from this machine's record (`SESSION_SPENT`, `useSessionSpent`); and the head's facts name the agent the line's
leading part runs on where its parts ran on more than one (`leadingAgentOf`). The strip is the flow's rail, drawn where only the rail fits or the
person chose the list (`Flow`'s `rail`), and a press on a node, or Enter on it, shows that part as the strip's button did. The
measured check (`e2e:measured`) draws both flows' stories in a rendered browser, each frame with a ceiling of crossings.

A landed branch's page draws how it landed (FLOWCHART1c, D186): a section above its commits, folded until it is opened, then read
by the branch (`projects/git/RepositoryGitView.tsx`'s `useLandingDoor`): `TRACE` for the session its landing record names, whose
landing carries the session it was made for, each advance with its session, and the units it carries, and `WORK_STATE` for the
branch, whose units carry the end, the clean-up and the recovery. `projects/git/landingLife.ts` draws the due entry's tries by
their codes (`AutoLandingCode`, held to the driver's source with `EndBy`, `PullRequestStates` and `LandedKind`), the landing and
its advances, the push as a fact with no time and the pull request's latest answer as a snapshot, each hung from the landing.
`Flow` folds three or more like nodes in a chain into a stack (`flowLayout.ts`' `findStacks`), its boundary edges kept and the
edges between its members said in its description. The work's milestone and a run's landing step open the page through the
application's opener (`opening`'s `branch`), with the section open.

A session's record folds where its work stands (`work/head/WorkMilestone`, MILESTONEUX1, the UI/UX design §3.7 and R4): the
milestone's end, result, clean-up and recovery are four independent facts, each marked by `Dot`'s own tone for its value
(`done` for what is reached or kept, `waits` for what waits on another, the danger hue for a blocked entry, none for a
recovery with nothing kept), never a sequence and never a `PhaseList`. It is folded to `MilestoneLine`, which also says
*blocked* and *held for you* so nothing that waits on the person hides behind the fold, and opens in place; the fold is the
molecule's own state, and the page mounts it under the record's id so another record starts folded. The line is also a
tidied branch row's. The record head's budget is measured in a rendered browser (`e2e:measured`, `measured/head.spec.ts`
over `work/AttendedSession.head.stories.tsx`): how far down the main area the conversation starts, with every block of the
head present, at the three widths and in both languages; D185's MILESTONEUX1 note says what it measured. A blocked entry is
said by its code in the reader's language (`entryDetail` in `work/head/milestone.ts`, OVERVIEWNOISE1b), and a tree by its folder's
name; *What needs you*'s row of a unit is named by the session that recorded its tree and opens the workspace's Branches.

The parts and the relations fold likewise (HEADFIT1). `work/head/PartsFold` is one line (*Part 2 of 3*, how the newest began, *Show
the parts*) that gives way to the flowchart, or to the strip where only the rail fits, on a press, with *Hide the parts* back;
`SessionRelations` folds itself to what it asked and was asked, with *Show who it worked with*. Each keeps its state in the molecule;
the page mounts the relations under the session's id and the parts under the line of work's, since a press on a part shows another
session of the same line and the flow being read must stay open. Accept's plan is drawn in the opened flow only. The head with both
folded is still over UX7's 200 px, so `head.spec.ts` keeps the budget as an expected failure; D185's HEADFIT1 note says by how much and
what remains.

The budget binds a typical record (HEADFIT1b): the page head, the record's head and *Details*, the chain's line and the trace's line,
drawn as the `Typical*` stories beside the every-block ones. The record's blocks are 8 px apart, and so are the pinned page head and
the conversation from them (`SessionPageHead`'s margin, `AttendedSession`'s grid, the conversation's top in `WorkFrame` and
`DetachedSession`, and the stories' stand-in): one rhythm, which the measured check holds. Two blocks hold one line at every width:
the session's page head keeps its acts beside its title (`PageHead`'s `actsBeside`; at 640 px of the main area or less *Stop…* is
its square, named and tipped by its words), and `ChainLine` truncates its stops, whole in their tip, before it wraps its press. The
typical record keeps 200 px at every width, unmarked; the every-block head is a ratchet. D185's HEADFIT1b notes have the heights.

## Running it

The HTTP host requires `DAORIS_HOME` or an explicit `DAORIS_KNOWLEDGE_DB`. Use a development
store under `_fixtures/`; an installed application's home is live state. The
[service guide](../Daoris.Service/README.md) explains these environment variables and local mode.

```
# the service, with the UI it will serve
npm --prefix src/Daoris.Web run build
dotnet run --project src/Daoris.Service/Daoris.Service.Http     # http://localhost:5177

# or, developing the UI against a running service
npm --prefix src/Daoris.Web run dev                             # http://localhost:5178, proxies /api

# the loop (from the workspace root): unit layer, then build bundle + host, then drive the real thing
npm run test:web
npm --prefix src/Daoris.Web run test:watch                      # the inner loop, on save
npm --prefix src/Daoris.Web run e2e:ui                          # the outer loop, watch-and-poke mode
npm --prefix src/Daoris.Web run e2e:measured                    # the flowchart's, Settings' and the record head's stories in Chromium, measured (D185, D186)

# the design tool
npm --prefix src/Daoris.Web run storybook
```

`test:web`'s two halves are declared gates in `daoris.gates.json` (WEBSPLIT1): `test:web:unit`, the
vitests, which build nothing, and `test:web:e2e`, the bundle, the host and Playwright. So the devkit's
`verify` — and the release workflow — refuse a release whose UI cannot do its job over the example
family. Note the second rebuilds the host, so stop a locally running instance first, or the build
fails on the locked DLL.

Set `DAORIS_EMBED_MODEL` to turn the semantic tier on; without it the UI says so and convergence finds
copies and restatements only.

## Historical measurements

The original family corpus held **449 entries from 11 repositories**, convergence returning genuine groups —
`phase-review` across two repositories at 0.947, `test-coverage-priorities` at 0.940, `doc-loader` across
three at 0.913.

Convergence is the expensive call: about 31 seconds cold over that corpus, and **5 seconds warm** once
the detector holds its vectors. It was 31 seconds *every* time until the service stopped constructing a
new detector per request — which mattered because moving the threshold is the common interaction, not
the rare one.
