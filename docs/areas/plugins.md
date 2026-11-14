# Plugins — how it works now

## What it does

A plugin is a folder with a `plugin.json` manifest. It extends Daoris without loading code into any Daoris process. It may
declare harnesses (agents on the ACP door), MCP servers, skills, tools, workflow steps, commands, storage, views and an icon. It
may also speak: its own process answers JSON-RPC frames on stdio at named points: a decision (`quest/consider`), observations
(`session/ended`, `state/changed`, `work/event`), a query (`work/state`, a pull request's state), the work wire (`work/watch`,
`work/step`, `work/perform`), a command's rounds (`command/run`), and for a system's adapter the system points (`plan/read`,
`apply/perform`). Plugins live in `<home>/plugins/<id>/`, with `.data/<id>` beside them. A person installs one from a folder,
from the install's offers (*Daoris's own plugins*) or from a `.nupkg` file, and updates it from its recorded source. The kit
writes a new plugin folder and tries it as the driver would. The Plugins view is a catalogue with a page per plugin. All plugin
code is the driver's.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they name another root, each type in a file of its name;
`docs/index/README.md` says where a seam, route, verb or fixture is. Which folder owns which job:

| Where | Its job |
|---|---|
| `Plugins/Domain/` | The manifest and catalogue (`PluginManifest`, `PluginCatalog`), the points' frames (`HookFrames`), the source record, the switch table, families, renames, the interfaces' readers and the command wire, the work wire's records, `work/event`'s grammar |
| `Plugins/` | The hook wire and the loop's running hooks (`HookPeer`, `HookSet`), the work processes per place (`WorkPlugins`, `PlacedWork`), harnesses handed to Agents (`PluginDoors`), install and update (`PluginInstall`, `PluginFolders`, `PluginOffers`, `PluginPackage`), the kit and trials (`PluginKit*`, `PluginTrial*`, `plugin-kit/*.template`), tools and sign-in, Ask Daoris's grants (`HelpGrants`), health and log, switches, renames, commands and the agents' tool server, the work events' teller |
| `Plugins/Relay/`, `Plugins/Systems/` | `events relay`, its words and batch; a system's adapter, its plan, apply and read-back |
| `Workplace/PullRequest*.cs` | When `work/state` is asked and how its answer clears a branch |
| `Knowledge/Domain/PluginVocabulary.cs` | The point names (`HookPoints`) |
| modules `Bridge/Routes/Plugin*Routes.cs` | The `PLUGIN*` routes |
| `tools/desktop-publish/lay.mjs`, `examples/plugins/` | The offers laid out at each publish; Daoris's own plugins and the stand-in system |
| web `src/plugins/`, `settings/plugins/` | The catalogue, the plugin and offer pages, *Set up*'s round card and switches, the palette's commands; Settings' read-only list |

## Its rules

1. A plugin is a folder in `<home>/plugins/<id>/` with `plugin.json`. Its code runs only as its own process, never inside Daoris.
   (D64)
2. `apiVersion` is read before anything else; a newer one is refused naming both numbers. The hook wire's `protocolVersion` is 1.
   (D64, D148)
3. A broken manifest is a named problem on its row, never a crash. A harness name this build carries, or one an earlier plugin
   declared, refuses the later plugin before anything loads, naming both; a server name declared twice in one manifest refuses it.
   A plugin never replaces. (D64)
4. A problem in `icon`, `tools` or an `interfaces` entry never refuses the plugin; a problem in `steps` or `resources` does. (D140,
   D150, D173, D274)
5. Off is a row in `plugins.json`, never a rename. An installed plugin is on until the person switches it off; a remove clears its
   row and its switches and leaves `.data/<id>`. (D64, D120)
5a. `plugins.json`'s `switches` holds the person's word, `true` or `false`, by plugin, by what it switches (`commands`, `tools`,
    `views`, `storage`, or a point) and by place (`machine`, `workspace:<name>`, `repository:<id>`). For a place the repository's
    word decides, else its workspace's, else the machine's, else it is on; `storage` is the machine's alone; a plugin in
    `disabled` is off over all of it; an unreadable file switches nothing off and refuses an edit. One resolver is read at every
    door: a command's run at its place, the storage handed, the adoption, `quest/consider` at the quest's place, `session/ended` at
    the session's, and the work wire, whose units at a place with `work/watch` or `work/perform` off are not told.
    `daoris plugin switch <id> <what> on|off [--place <place>]` and `PLUGIN_SWITCH` write one word, the loop looking at once; `on`
    at the machine is the default and leaves no word, and `PLUGINS` answers each plugin's words. An older home's `off` lists became
    machine words once (`plugin-switches`). (D274)
6. A process may listen only on points its manifest declares, or it is refused. A point this build lacks is named once and never
   asked. (D101, D148)
7. `quest/consider` is a waterfall in catalogue order that fails closed. The first hold ends it; a late, wrong or missing answer is
   a hold naming the plugin. (D64)
8. `session/ended` and `state/changed` are contained. `state/changed` is queued off the press's path and cannot hold it. (D64, D169)
9. Daoris pushes through its own push act and opens no pull request; a plugin's `pull-request` step on `work/perform` does, as the
   person. No plugin is started for a landing, and no `work/land` point is spoken. A decision waits 10 s. (D100, D101, D241)
10. `work/state` asks one plugin, the one recorded on the landing, by the id it has now, and only if installed, on, sound and
    listening. A frame waits 30 s, no frame starts after 60 s, and an entry is asked once a minute at most. Unanswered, a landing
    the keeper proved its line holds reads as its pull request merged (workflows rule 31). (D148)
11. It is asked at the tidy, the clean-up's look, bringing up to date's look, *Ask again*, origin's line moving, after Daoris
    pushes, and at window focus while a pull request is open; never at a press that removes, a read or a timer. (D148)
12. A `completed` answer clears a branch only where git confirms: the target is the line, the merge commit is held here, and the
    recorded tip is the source commit or its ancestor. No answer removes nothing; a failure never overwrites a kept answer. (D148)
13. Every install door writes `.daoris-source.json` into the staged copy before the swap: `{folder}`, `{offer, digest}` or
    `{package, version, sha512, source}`. No record is said, never guessed, and an unreadable one is named; the list, the page and
    `plugin show` say the source by its kind. (D103, D119, D120)
14. The window's add adds and never replaces. Only the terminal's `daoris plugin add` replaces an installed plugin, wholesale. A
    source inside or holding the home is refused. (D103, D194)
15. An update shows its plan first: version, command, points, harnesses, servers, skills, steps and resources, and a row per
    `interfaces` entry added, removed or changed, marked *asks more* where it asks more of the person (a command gaining `tool`,
    `writes`, `adopts` or an argument, a view gaining `context`, storage newly declared). It applies only on a press or `--yes`: the
    installed folder moved aside whole, the copy renamed in, `.data/<id>` untouched, nothing started at the press; a failed rename
    puts the folder back, or names the `.removing-*` folder the plugin lies in. (D103)
16. A plugin from a package is never updated in place: it is removed and a newer package installed. (D120)
17. Offers are `app/plugin-offers/` in the install, laid out each publish from `examples/plugins/`: Daoris's own `daoris-github`,
    `daoris-azure-devops`, `daoris-in-app-browser` and `daoris-dev-report`. None installs without a press. The driver looks beside the application
    first, then beside the home. An offer is listed as installed where its id or one it formerly was is. (D103, D274)
17a. Daoris's GitHub and Azure DevOps plugins declare a `goal` on their `pull-request` step and carry its create, status and
    complete as tools, so the loop's step session performs it (workflows rule 31a), an attempt delivered on `work/perform` before
    staying theirs to settle by their `check`; they answer `work/state`, call their service's
    REST API with the token their own `login` keeps in their private storage (rule 29), say a refused or missing token at `status` (a read, which exits 0 whatever it answers, so it is never a failed command), run no
    git, `gh` or `az`, and declare node alone. Azure DevOps's step sets auto-complete where its options say so, and its `release`
    step acts only on the target its options declare by id (pipeline, release definition, stage, action, success), a repository's
    values over the shared workflow (workflows rule 3a), naming the deployable it releases and its `deploysOn` (rule 3b there):
    undeclared or unresolved (a pipeline or definition gone included), it fails `no-target` or `target-changed` naming the deployable
    and listing what it found, writes nothing and is held for the person; it starts the declared stage, or makes the release from the
    exact merged build (never twice), only where its action says so, as its README says. Its `discover-releases`, a tool that never
    writes, lists the same candidates (each definition's stages with what starts them, triggers, links and a proposed `deploysOn`) for
    the person or a planner to declare from, choosing none. (D288, D290) Where no token is kept or Azure
    DevOps refuses the kept one, Azure DevOps's plugin makes its calls from a tab of its own in the person's signed-in browser: its
    manifest's `hooks.browser` hands each process the in-app browser's DevTools address as `DAORIS_BROWSER` (`PluginBrowser`), the
    calls are page-side `fetch`es that read no cookie, and each answer's evidence ends `Way: the token.` or `Way: the browser.`
    (D281). A plugin with a `pull-request`
    step has one work process per place awaiting it, never one shared: its waits, its unsound mark (three unasked exits within an
    hour, then not started until the person's *Try again*, `daoris plugin try-again <id>`) and its idle stop are its place's own,
    its generations unique across places, and an attempt goes to the place its unit was told for. A place whose hand is refused (no
    origin, a read-only identity, Write denied) starts no process, logged `refused`, never a failed start nor an exit toward
    unsound, and is asked again a minute later, so access allowed later starts it with no press; the other places are served, and
    `plugin try` asks the same of its checkout. (D241, D242)
17b. `work/perform`'s params carry `options`, the performed node's graph options by name, each its JSON value, else its text,
    filled at every delivery from the declaration and never kept on the attempt, so an edited graph reaches a redelivery; the kit's
    `work.mjs` hands them to `act()` as `call.options`, `{}` when absent.
17c. A manifest's `systems` lists the systems the plugin adapts, each `{ id, name, preconditions }`: its id lowercase letters,
    digits and dashes, at most 40, once; its name its id where unwritten; `preconditions` `etag` (the default), `serial`,
    `changeset` or `none`, which `PLUGINS` names so the repository list calls a system of `none` *unguarded*. Such a plugin must name `plan/read` and `apply/perform`, asked only by a system's plan and apply, and any
    problem refuses it. `plan/read` answers the operations a set of changes needs, each with its precondition's version, leaving
    out what the system already holds; `apply/perform` answers `applied`, `unchanged`, `stale` (with the present values) or
    `failed`, and another shape is refused, nothing kept. A system guarded by `none` is applied one operation at a time between
    reads, a moved version `stale` and a read-back still differing `changed` while applying, Daoris's own outcome.
    `examples/plugins/stand-in-system` is the proof's adapter, offered nowhere.
18. An offer's installed copy is judged against the shipped one. Same version, other files: `changed`. Older, files matching the
    record's digest: `older`. Older, digest not matching: `changed`. Older, no digest: `older`, unjudged. Newer: the person's, said
    nothing. A pre-release tag is older than the same numbers without one, its dotted parts compared number by number. (D119)
19. A `changed` copy is never overwritten by an update. A copy with no record, or one that does not read, may take the shipped copy
    (`PLUGIN_UPDATE` with `shipped: true`), which records the offer and keeps `.data/` and the switch; a package's never (rule 16).
    (D119)
20. `plugins new` writes a self-contained folder: a manifest with `tools: []` and `interfaces` declaring the sample command `hello`,
    `storage` and a view stub; `plugin.mjs` answering them; its tests; a README; `storage.mjs`, the kit's record store, which writes
    one record of a JSON file at a time under a lock taken by exclusive create (taken over from a gone pid or after 30 s), reading
    fresh inside it and renaming the whole into place, and keeps a file that does not read; for a work point `work.mjs`, keeping
    `work-journal.json` record by record so a plugin's places keep each other's attempts, with `plugin.contract.test.mjs` and
    `cycle.json` where it performs; for a system point both points, a system guarded by `etag` and `system.mjs` over the
    author's `read()` and `write()`. It refuses rather than overwrites, refuses an id with no family or in the `daoris-` family
    outside this repository's `examples/plugins/` (rule 30), installs nothing and scaffolds no harness. Its samples come from
    `HookFrames`. (D101, D150)
21. `plugins try` starts the plugin as the driver would, in scratch under `<home>/plugins/.trials/`, one sentence per check; exit
    0, 1 or 2. It fails a declared point the process does not listen on and, for `interfaces`, each entry refused, an interface
    this build does not know and a forget not run; it runs no command, since a command reaches its platform. A system point's
    sample names the plugin's first system, and `work/perform` and `apply/perform` go only to a plugin that took the kit's stand-in
    world. (D101)
22. A sample frame names no repository: an empty scratch root and a `GIT_DIR` that is not there. (D101)
23. A tool is found and checked only at a trial or on a press, never at a load or on a timer. A check's `fix` (such as `az login`)
    runs only on the person's press, in a console they see. Daoris never signs in or downloads. (D150)
24. A manifest's optional `brief` is one authored line, trimmed, at most 1,000 characters with no control characters; an unusable
    value contributes nothing. `PluginBriefs` hands it once in the session's standing block only where the plugin contributes,
    its `tools` switch is on at that place and its tools are handed: a declared server or an eligible command on `daoris-plugins`
    (also subject to `commands`, and to a step's one-plugin restriction). An update plan shows a changed brief.
    A server goes to sessions by default. One handed to Ask Daoris in restricted mode reaches it only once the person approved its
    exact tools; a write only as an accepted write; a generic execution tool never. In full access Ask Daoris is handed every server
    whole and asks nothing, and the plugin's page and `plugin show` follow its mode: no server waits for an approval. A server run through `npx` names its package and version, and
    a session is handed node and that package's script, installed once into the toolchain, never `npx` itself; one driving Daoris's
    browser stays under a 512 MB ceiling by a restart between calls (agents area, rule 13). An approval is the person's and stands
    while the plugin is switched off, but a switched-off plugin's grant is handed nothing. (D170)
25. What a plugin did is kept without its words: `plugin.*` lines hold names, counts, flags and times. Its words go to the console
    under `plugin:<id>` for this run. (D94, D119)
26. An icon is a path inside the folder to an SVG or PNG of at most 32 KiB, a PNG at most 512 px a side. The page gets its bytes as
    a data URI, drawn in an `<img>`, never a path; else a monogram on a hue hashed from the id. (D140)
27. The page is a catalogue: installed (refused while on, then on, then off), then Daoris's own not installed. Name, description and
    version stay as declared. No plugin adds a view or runs code in the page. (D52, D119, D140)
27a. A plugin's page has the tabs *Set up* and *About* where it declares an interface or a point (*Set up* first where it declares
    an interface), and no tab row otherwise. *Set up* lists each command with what it asks and changes and *Run* (disabled with the
    reason where it cannot run at the window's place), each refused entry with the reader's sentence, the storage, the views, the
    interfaces this build does not know, and *Where it is on*: a box per interface, point and place, pressed through
    `PLUGIN_SWITCH`. An offer's page lists the same before *Install*. A command runs in the round card at the window's scope's
    workspace, else the machine: its arguments and `confirm` asked first, then each round's `said`, a page to open in the person's
    browser, a code with *Copy*, a field per ask (a masked value never drawn back), *Go on* and *Cancel*; its end says done or the
    exit and follows `then` (`reload`, `back`, `stay`). The palette lists the commands of each plugin on, sound and with commands on
    at the machine, asking `PLUGINS` only while open, a row opening the plugin's page at its card. *Remove…* where `storage.forget`
    names a command offers to ask it to sign out first, ticked; a disabled `forget` says why. A failed step's `needs: "<command>"` (on `work/perform`'s completion, a reconcile or `step_answer`) is kept on its record and, where its plugin declares that command, drawn on the run's step as a door to its round card; any other name is ignored and said (`RunNeeds`). (D274)
28. A plugin's presses and its command line are open to every caller, a session's terminal included. With Daoris running a verb
    takes the route, else it acts alone under the home's lease. (D194) The command tree hands `daoris plugin`'s own verbs
    (`PluginCommand.Verbs`: list, add, update, remove, enable, disable, try-again, switch) to `plugin`, and the kit's `new` and `try`,
    `install`, `show`, `activity`, `approve`, `withdraw`, `tools` and a bare `plugin` to the host's `plugins` door (`CommandTree`).
29. A manifest's `interfaces` names what Daoris reaches the plugin through besides its points: `commands` (each `{ name, title,
    about, interactive, tool, writes, confirm, on, args, adopts }`), `storage` (`{ for, forget }`) and `views` (each `{ id, title,
    icon, where, places, context, body }`), every word authored (D222). `args` are `{ name, type: text | number | boolean | choice,
    title, required, choices }`, `choices` only and always for a `choice`; `on` lists the records (`session`, `quest`, `run`,
    `repository`, `workspace`) whose menus offer the command with the record's id; a `tool` never asks the person; one command at
    most adopts. A view's `where` is `side` or `page` (the default), its `places` `machine` (the default), `workspace` or
    `repository`, its `context` `session` or `quest`, its `body` `drawn` alone. A problem refuses that entry alone, never the
    plugin; an interface this build does not know is kept and skipped; an interface needs `hooks`. `forget` names a declared command
    that runs alone, else it is kept disabled with why. `daoris plugin list` says each, and `PLUGINS` answers them whole.
    A command runs one round per process at `command/run` (`{ command, run, args, answers, from, place, language, session? }`,
    never a point claimed at the handshake): `run` one id across its rounds, `from` `terminal`, `window`, `adoption`, `agent` or
    `removal`, always Daoris's word; `place` `machine` or `{ kind, id }`; `language` the viewer's, `en` at the terminal; `session`
    where an agent calls. It is answered `{ said, ask, open, code, then, data, exit }`: `said` at most 40 lines of 2,000 characters
    and 8 KiB, `then` `stay`, `reload`, `back` or `{ go: { view, args } }`, `data` at most 32 KiB for an agent, at most 8 rounds of
    60 s each, the answers so far sent whole. `daoris <plugin> <command> [args] [--stdin]` is the terminal's word (rule 30), asking
    each missing value, hidden where masked, from a session's terminal as from a person's; the window's door is `PLUGIN_COMMAND`, one round per
    call, a missing argument refused before any process starts, the plugins pushed again after each round. *About*'s last runs are
    the newest 20 `plugin.command` lines, not drawn yet. `storage` is `profiles/plugins/<id>/`, limited to the account, handed as `DAORIS_PLUGIN_PRIVATE` to that plugin's
    processes alone and removed with it; Daoris reads nothing in it. A removal first runs the `forget` command once, bound to 10
    seconds, saying whether the plugin confirmed, and goes on whatever it says. A command declaring `adopts` is sent, at the
    loop's start and again whenever the plugins change (installed, updated, added or switched), the API token of the first identity
    serving one of its hosts, which then keeps none (`plugin.token.adopted` naming ids alone). Each interface is on until
    switched off per place (rule 5a). Daoris names no plugin in any of it: every word comes from the manifest and the answers.
    (D274)
29a. A command marked `tool` reaches an agent through one MCP server per session, `daoris-plugins`, handed a quest's session at its
    start and going on and an intake's and a repository's chat's at their start, only where its place lists a tool and the
    desktop's program is found. It lists each tool command, not `interactive`, of each installed plugin that is on, sound and not
    refused, where its `commands` and `tools` are on at the session's place, read again at each list and call; each is named
    `<plugin>_<command>`, described by its `about` and `title` in the session's language, its input schema from `args`, read-only
    unless it `writes`. A call is one round `from: agent`: `said` the text, `data` the structured content, an exit other than 0 an
    error; a round that asks is refused, since no person answers a tool call, an argument missing or mistyped is refused before any
    process starts, and a tool not listed is a `-32602` error. No call asks an approval: the `tools` switch per place is the opt-in. Every round by any door is a
    `plugin.command` line of names alone, and *Done and failed* notices the rounds nobody watched and any that failed, over the
    last day. Ask Daoris is handed the server only as the person approved its tools (rule 24; its rules
    `mcp__daoris-plugins__<plugin>_<command>`), whole in full access, at its room's workspace in English. A reviewer, a helper and a partner are handed none. For an agent-performed step's session it lists that
    plugin's tools alone and `step_answer` (workflows rule 31a). (D274)
30. A plugin's id is `<family>-<name>`, lowercase with dashes: Daoris's own are the `daoris-` family (`daoris-github`,
    `daoris-azure-devops`, `daoris-in-app-browser`), anyone else's its own family; the family is the word before the first dash and
    the tail the rest. A terminal names a plugin running commands by its id, or by its tail where no other such plugin shares it
    (`daoris azure-devops login`), a whole id first and a shared tail refused naming each; the tail is the id's own, never a second
    name a manifest declares. A manifest's `formerly` names the ids it was, each once and not its own. An installed plugin declaring one takes the
    old id over once, at each loop's start before its plugins start and after any install door: the old copy is taken out and its
    switches, `.data/<old>`, `profiles/plugins/<old>/`, approvals and unsettled attempts move to the new id, what the new id already
    keeps never overwritten and said, written only by the process holding the home's lease; `plugin.renamed` says what moved, and a
    later start finds nothing to move. A schema keeps its name across a rename. A copy
    from an offer updates from the offer that was formerly it. A saved workflow's `<old>/<step>` is read and drawn as
    `<new>/<step>` with no save, its file and digest as written, so a pinned run goes on with the new id performing its steps; a
    landing's recorded plugin is asked by its new id. The old kind ends when no installed plugin declares it. (D274)
31. Settings sets nothing about a plugin. Settings → Driver lists the installed plugins read-only, each opening its page. (D119,
    D183)
32. `work/event`, an observation, tells a plugin the work cycle's events, pushed, never polled. Its grammar (version 3) names
    `session.parked`, `quest.parked`, `session.ended`, `work.landed`, `events.missed`, `quest.published`, `quest.closed`,
    `quest.close-lost`, `confirmation.ended`, `work.ended` and `channel.posted`, each kind's fields a closed list of ids, codes,
    counts and a UTC time; an optional field that fails is left out, and an event whose required field fails is told as
    `events.missed`. Only the loop holding the lease tells it, the plugin's queue joined before it starts so `initialize` carries
    the grammar, the last `seq` and the build. The teller mints every `seq` under one lock, keeps 256 a plugin (the oldest dropped
    and counted in the next frame's `missed`), and stops a plugin after three late answers in a row. An intake's, a helper's and a
    reviewer's park are not told. (D173, D244)
33. `daoris events relay` is an outside agent's end of it. `--where` lays the running build out under `bin/events-relay/`, staged
    and moved whole, and prints the copy's command; `--once` runs only from such a copy, serves a pipe for two instances, refuses a
    client its agents' job does not answer for, and prints a batch of fixed sentences over checked ids, codes and counts (at most
    50 event lines, one gap line, closing on `read up to`), ending on a second client, three bad frames, 30 s of gathering, its
    session's exit or 15 minutes. The plugin is the plugins repository's `event-relay`; Daoris holds none of it, and its process
    tests run a stand-in. The design is `docs/2026-11-06-event-channel-design.md`. (D244)

## Where a change goes

- **A new point or frame field:** `HookPoints`, `HookFrames` and the kit's entry in `PluginKit.Points`; a point with no kit entry
  fails `PluginKitTests`.
- **A manifest field:** `PluginCatalog` and `PluginManifest`, the update's rows in `PluginChange.Between`, the page's `catalog.ts`.
- **Install, update, the source record:** `PluginInstall`, `PluginSource`, `PluginFolders`; the route in `PluginsRoutes`, the
  terminal in `PluginCommand`.
- **Offers:** `OFFERED_PLUGINS` in `tools/desktop-publish/lay.mjs` and `PluginOffers`.
- **A shipped plugin's behaviour:** its folder in `examples/plugins/`, its version and `changes` line, its README's `## What it
  needs`. The kit's files change in every working example in the same commit (`PluginKitExampleTests`).
- **A session's report to the development agent:** `examples/plugins/daoris-dev-report/`, whose only point is the observation `session/ended`, answered with nothing (`hooks` needs a point): `report` (a tool,
  not marked `writes` since it changes nothing outside the plugin; `on` session and run: `title`, `body`, `evidence`, `severity` note, defect or blocking) files one record, id, time, the round's
  session and place, into the plugin's private storage through the kit's `storage.mjs`, and answers the id in `said` and `data`;
  `inbox` (a tool and a terminal verb: `--unread`, `read <id>`, `mark <id>`) lists unread first, newest first. The person's own
  agent reads it with `daoris daoris-dev-report inbox` against the install's home.
- **The page:** `src/Daoris.Web/src/plugins/`, chrome through both catalogues. **Steps and the work wire:** the workflows area.
- **Tests:** `Daoris.Desktop.Driver.Tests/Plugins/` (`PluginCatalogTests`, `PluginKitTests`, `PluginInstallTests`, `HookTests`,
  `PluginSwitchesTests`, `PluginCommandRunTests`, `PluginToolsServerTests`, `WorkEventTellerTests`, `PluginSystemsTests`,
  `PluginRenamesTests` and their neighbours), `Workplace/PullRequest*Tests`, `RelayHostProcessTests`; modules
  `Bridge/DriverModulePlugins*Tests`; the page's `plugins/*.test.ts(x)`; each example's `node --test`.
- **Gates:** the driver lane's desktop suites, `rehearse-family`, `deployment`; `modules`, `censuses`, `modules-process`;
  `examples/plugins/**` adds `service`, `driver-process` and `web`; the page `web-unit`, `web-types`, `web-rules`, `web`.

## Contradictions

- D103, D140, D150 and D64's PLUGINRESERVE1 note make the CLI and the driver twin readers vs D194 (the driver reads alone): D194;
  stale twin comments remain (`PluginCatalog.Reserved`, `PluginManifest`'s `OwnHome`, `lay.mjs`).
- D120 §3 and §5 (the landing plugins and the browser in the plugins repository; offers pinned to packages) vs the publish laying
  them out from this repository: the code.
- D119 §2 (five states, ready and failing among them) vs the page's four (`pluginState`), though `PLUGINS` answers health: the
  code; the decision is unbuilt.
- D119 §5 and §6 (`PLUGIN_TEST`, `plugins test`, kept checks) vs none existing: the code.
- D119 §7 (no go to Settings → plugins) vs its PLUGUI1h note (a moved place opening Plugins): the note.
- D119 §8 (Settings keeps nothing) vs D183's read-only list: D183, since it sets nothing.
- D103 (five update rows) vs nine and a row per interface entry (`PluginChange.Between`): the code; neither shows `tools` or `icon`.
- D101 (the kit's New in Settings → Plugins) vs the Plugins view's `PLUGIN_NEW`, `PluginKit`'s remarks still saying Settings: the
  view.
- Ask Daoris's room (a plugin with no record cannot be updated) vs D119's LANDPIN3 (an offer's copy may take the shipped one): both,
  each for its door.

## Open

- **PLUGHOOK1d**: a pull request's state asked again once the first pull request opens.
- **FABRIC-PERFORM1l**: a pull request's fields kept in the work wire's evidence.
- **EVENTHOOK1**: 1a's M9 and M10 and 1c's steps 2 and 3 across real elevations, on a UAC machine; then 1h, the installed proof.
- **EVENTHOOK1d**'s rest: `Settled`'s wiring through the shell's pipe and each source told by no `--share` loop, end to end.
- **EVENTHOOK1e** (D244): a relay that lives for the session, measured first.
- **MCPMEM1**: `ServerKeeper`'s restart above 512 MB proven in a real long chat on the staged install.
- Decided, with no row: D120's workshop and NuGet source; PLUGUI1f's ready and failing states; D119 §5's plugin tests and kept
  results.
