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
| **Repositories** | repositories and workspaces, their ownership, setup and driver controls; workspace pages carry wiring, lines, landing and permissions (D150). A repository's Setup and a workspace's Defaults show and set each one's process (DOORS1b, D164): *Commits* beside how work lands (as it goes, each after your yes, or once at the end, and the person's convention quoted as written), the branch rule's `{ticket}` key and squash, sent whole so a save keeps what the terminal or Ask Daoris set, and *Helpers* on Sessions (wait for your yes, start at once, or not offered, and the agents in order), each saying what it inherits and where it was set. In the desktop a repository's Branches lists its branches by kind and History draws its line's graph a page of 200 at a time (*Show more*), or over every local branch and origin's from its ⋯ *All branches* (page state; at most 256 tips, the head says when it drew the first of more); a workspace's Branches groups its repositories' branches above the clean-up, its repositories holding none folded apart once, the include serving the groups and *Updates*' look (D147, D150) |
| **Knowledge** | Search and Convergence are two modes of one view: find entries or compare independently learned lessons |
| **Agents** | installed agents and accounts: names, sign-in state, each usage window as a cell in the same place on every row (its share, reset and reading's age, or *unknown*), workspace lists and actions; desktop only. An account's window cell (in the warning's hue), its said line and a start's note say an agent's own pace warning as *ahead of its pace*, never as near its limit, and the warning alone never makes a scope's near or the hand-over offer (D165, USAGEREAD3b). A start's note says an account passed for its use as *over its curve* with the window, its use and the curve's value now, and the account chosen for its room as *the most room under its curve* (D165, SWITCH1b-ii). The next start's reason and holds say which passed an account, its limit reached, usage credits or its use over its curve on a window, and its walk ranks by the most room under the curve, by the week's pace with the switch off (D165, SWITCH1b-iii). A conversation moved to another account by the curve says so at both ends, each account by the name the person gave it, with the window, its use and the curve's value, and a hand-over's why says *its account was over its usage curve* (D165, SWITCH1c-i) |
| **Plugins** | this machine's plugins and offers; desktop only |

**Sessions** is the working surface — the agent sessions this machine runs, attended one at a
time (D55, `docs/2026-09-21-working-surface-design.md`) — and exists only in the desktop, because a
stream never leaves its machine. **Map** is how a workspace's repositories are wired (MAP2).
The session's Workflow view names its bound saved version, or Current for live declarations.
Unreadable bindings show a problem without a completed diagram. Both the head and diff pane
show workflow holds before Accept, preserving the driver's explanation and available terminal doors.
A second opinion's reviewer is a row of its own in Sessions' list, and says whose work it reads (XAGENT1g4c): the rail
joins it to the working session through `GET /api/opinions`, asked only when the list holds a reviewer, and an unanswered
read claims nothing. A session that continues another, and a helper, say so (MIXAGENT3a-i): the head's facts name the agent
continued and why, in the hand-over's words, or *helping* and the session's title; *Details* holds *Continues* and *Helping*
with a door to that session, or its id where the list holds none; the row says *continues* or *helps* and the title, from
the record's own `from` and `serves` joined in the rail; and a helper is titled *Helper* and, as a reviewer is, offered
no box. Review reads a session's work against its line (REVIEWBASE1): the head says, in one line, when the latest line the
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
(D152). Help → Update opens the update card in Driver.

Each Settings domain is `src/settings/<Name>Domain.tsx`, with its tests beside it, and
`src/settings/domains.ts` lists them: the one place a domain is added, since the frame
(`settings/SettingsView.tsx`) renders whichever one is chosen and names none itself (MOD4).
*This Daoris* (`WiringDomain.tsx`) draws one read, `SYSTEM_WIRING`, as cards (`Wiring*.tsx`, molecules over props); its card
*Left in your own tool home* (`WiringToolHome.tsx`, OWNSPACE2) lists the folders Daoris's session records show its sessions
ran in, read-only: Daoris removes none of them yet, and the card says a person can by hand.

**An account is named by one rule, wherever it is said** (`src/agents/agents.ts`, D125's ACCTNAME1 and ACCOUNTID1b notes):
`accountName` leads a row, `accountWho` sits beside it, and `accountLabel` is the one line a chip, a pill, a note or the
roster's namer (`settings/namer.ts`, so a session's header) says. Where two of an agent's sign-ins share an email, each
says what tells it apart, after the email and once. A surface that names an account goes through these, never its own.

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
| Server state | **TanStack Query** — deduped fetches, refetch-on-focus, invalidation after every mutation; a driver press's is the driver's `STATE_CHANGED`, asked again at one place in every window (`bridge/stateHub.ts`, D169), so a hook asks nothing again itself and a test that presses a declared route mounts the hub over a mocked shell that publishes what the route declares (`test/publishing.ts`); a press under way is read by every view through `useUnderWay(route, session)`, never one component's `isPending`, and the hub reads the driver's list as it mounts, so a window opened mid-press says it too (STATEHUB1c2); Ask Daoris's open *What it has* is asked again on the `help` scope, so a receipt recorded meanwhile shows (HELPWIREDLIVE1) |
| i18n | **react-i18next**, `en` + `zh`, flat dotted keys, **one file per area** (`src/locales/<language>/<area>.json`, merged at load by `src/locales/index.ts`): a key lives in the file named by its longest dotted prefix that has one, so `settings.rules.add` is in `settings.rules.json` and `nav.quests` in `nav.json`. `scripts/i18n-check.mjs` fails the build when an area's two files diverge, a key is in two files, or a key is not in its home (MOD2); `scripts/names-check.mjs --strict` beside it fails the build when a label breaks the glossary (`src/locales/glossary.json`), its kind's form or its door, and reports the budgets without failing (D116, NAME1b) |
| Shell bridge | **@shenora/react** (0.22, with the shell's packages: SHEN4, D22) — in the desktop, `DAORIS.DRIVER` carries the person's controls and `DRIVER_TICK` pushes the loop's reports into toasts and refetches (`app/ShellSignals.tsx`, which says no toast for a conversation's answer when that conversation is the one attended in a window in front, NOTIFY1c), which mounts the state hub (the running call each session makes arrives as its `calls` slice and is laid over the pulled `calling` on the rail and the head where live, WAITSAY1c2); in a browser none of it mounts, by design. **One file per domain** (MOD3): each domain's types and calls are `src/bridge/<domain>.ts` (driver, sessions, conversation, console, terminal, trees, lines, agents, plugins, rules, help, browser, windows, remotes, registry, log), `src/bridge/shell.ts` is only the barrel re-exporting them, and `bridge/bridge.test.ts` holds that it stays one. The desktop answers each route in a handler class per domain, `<Domain>Routes.cs` in `Daoris.Desktop.Modules` (D166), which is the file a comment here names for a route's refusals |
| Design tool | **Storybook** (`npm run storybook`) — the component states and the token gallery, on the shipped code |
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
read through the driver's `CHANNEL_READ`, `CHANNEL_POST` and `CHANNEL_MARK` (`bridge/asks.ts`), so a browser draws no *Channel*, no
unread count and no composer. The ask's page, each session's panel under its head and the unread counts on the ask's row and in
*Group by ask* are molecules over those answers (`asks/ChannelSection.tsx`, `work/channel.ts`), with Daoris's lines worded from
their codes in the reader's language and the messages shown as written. **A teammate's ask** (SESSONE2h) has a row in the Asks list's
*Teammates' asks* group, from the driver's `ASKS_ELSEWHERE` (`useElsewhereAsks`), and a page of its channel alone, read-only
(`asks/TeammateAskPage.tsx`): this machine holds no record of it, so it has no title and none of an ask's sections.

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
organism by name in `design/presentational.test.ts`.

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
`prefers-color-scheme` follow an explicit light or dark, and *System* still follows the OS. The main window's page
also tells its frame Overview's *What needs you* count (`useTaskbarCount` in `bridge/windows.ts`, `SET_BADGE`,
NOTIFY1b), which the frame wears on its taskbar button and in its title; a secondary window's page never does. That
count includes a plugin step's attempt held for the person, which `WORK_STATE` lists as `held` and its row settles with
their word (`useResolveHeld` in `bridge/lines.ts`, `WORK_HELD`, FABRIC-PERFORM1g).

The build outputs into `../Daoris.Service/Daoris.Service.Http/wwwroot`, so the page and the API share one
origin. That is what makes CORS unnecessary in a real deployment — the `DAORIS_WEB_ORIGIN` variable
exists only for the development server on another port, and it names an origin rather than wildcarding.

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
