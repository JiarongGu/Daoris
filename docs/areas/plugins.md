# Plugins — how it works now

## What it does

A plugin is a folder with a `plugin.json` manifest. It extends Daoris without loading code into any Daoris process.
It may declare harnesses (agents on the ACP door), MCP servers, skills, tools, workflow steps and an icon. It may also
speak: its own process answers JSON-RPC frames on stdio at named points. The points are a decision (`quest/consider`),
observations (`session/ended`, `state/changed`), a query (`work/state`, a pull request's state)
and the work wire (`work/watch`, `work/step`, `work/perform`); an adapter of a system reached only through its console also answers
the system points (`plan/read`, `apply/perform`). Plugins live in `<home>/plugins/<id>/`, with
`.data/<id>` beside them. A person installs one from a folder, from the install's offers (*Daoris's own plugins*) or
from a `.nupkg` file, and updates it from its recorded source. The kit writes a new plugin folder and tries it as the
driver would. The Plugins view is a catalogue with a page per plugin. All plugin code is the driver's.

## Its parts

| Unit | File | Its job |
|---|---|---|
| Catalogue | `Plugins/Domain/PluginCatalog.cs` | Reads every manifest in `<home>/plugins/`, API version first; names problems and conflicts; hands harnesses and servers on |
| Manifest | `Plugins/Domain/PluginManifest.cs` | The manifest's records: harnesses, hooks, servers, skills, steps, resources, icon, tools |
| Points and frames | `Knowledge/Domain/PluginVocabulary.cs` (`HookPoints`), `Plugins/Domain/HookPoints.cs` (`HookFrames`) | The point names, and the one builder of each point's frame |
| Wire | `Plugins/IHookChannel.cs` (the port), `HookPeer.cs`, `HookChannels.cs`, `HookSet.cs`, `Plugins/HookProcess.cs` | Handshake and answer reading; the loop's running hooks, reconciled each look |
| Landing and query | `Plugins/LandingPlugins.cs`, `Workplace/PullRequestAsks.cs`, `Workplace/PullRequestMoments.cs` | Whether a plugin performs pull-request steps; `work/state` at its occasions; a pull request's address is written from its `pull-request` step's evidence (`PullRequestRecords`) |
| Work wire | `Plugins/WorkPlugins.cs`, `Plugins/PlacedWork.cs`, `Plugins/PlaceHand.cs`, `Plugins/HandRefusals.cs`, `Plugins/PluginCycles.cs`, `Plugins/Domain/WorkWire.cs` | Performer and observer processes, on the lease's loop only (the workflows area owns the meaning) |
| Harness | `Plugins/PluginDoors.cs`, `Plugins/PluginAgentSkills.cs` | A manifest's harness converted to Agents' `DeclaredDoor` (its named fields alone, its `posture`, `fullPosture` and `askingPosture` among them), which Agents' `DeclaredDoors` makes a protocol-transport contract door and the registry places by its `accountOf`; the sound skills each contributing plugin hands a destination, as `SkillSource` values a door's skill layout lays out at spawn |
| Install | `Plugins/PluginInstall.cs` | Add, add an offer, plan and apply an update, replace, remove; every swap staged beside |
| Source record | `Plugins/Domain/PluginSource.cs` | `.daoris-source.json` (folder, offer with digest, or package); an update's change rows |
| Folders | `Plugins/PluginFolders.cs` | All home I/O for plugins; `Digest`, `HoldsDigest`, placeholders |
| Offers | `Plugins/PluginOffers.cs`, `tools/desktop-publish/lay.mjs` (`OFFERED_PLUGINS`, `layOffers`) | `app/plugin-offers/` in the install, and where each door finds it |
| Shipped judgement | `Plugins/Domain/PluginShipped.cs` | An offer's installed copy: `older`, `changed`, or older and unjudged |
| Package | `Plugins/PluginPackage.cs`, `Plugins/PluginPackageFile.cs` | Reads and installs a `.nupkg` offline |
| Kit | `Plugins/PluginKit.cs`, `PluginKitSystems.cs`, `PluginKitStorage.cs`, `PluginKitInterfaces.cs`, `PluginKitFiles.cs`, `PluginTrials.cs`, `PluginTrialProcess.cs`, `PluginTrialInterfaces.cs`, `plugin-kit/*.template` | `new` and `try`; the record store `storage.mjs` every scaffold carries |
| Tools and sign-in | `Plugins/Domain/PluginTools.cs`, `Plugins/PluginToolChecks.cs`, `Plugins/PluginToolSetUp.cs` | Declared tools, their checks on a press, a fix in a console the person sees |
| Ask Daoris grants | `Plugins/HelpGrants.cs` | `<home>/plugin-approvals.json`: a server's tools the person approved |
| Health and log | `PluginHealth.cs`, `PluginLog.cs`, `PluginActivity.cs`, `Domain/PluginEvents.cs` | `plugin.*` lines; the loop's record of each plugin's health |
| Work events' sources | `Plugins/WorkEventTeller.cs`, `Events/WorkEvent.cs`; `Workplace/WorkEnds.cs`; `Modules/Platform/PipeConfirmations.cs` (`Settled`) | The kinds read from records, told at `work/event` (EV-F): the teller first in both lease feeds, `Settled` wired in `LoopRun.ServePipe`, `WorkEnds.Listen` in the hook set from the point's first use (`WorkEventPlugins.Made`); the grammar at version 3 with each kind's fields (`confirmation` 32 hex, `end` its five codes, a quest's `state` by its kind; `channel.posted`'s `by` of four classes, `message` 8 hex, `poster` a session's or a member's id, `urgent`) and the relay's sentence for each, the relay's default `--kinds` still the first four: the journal's `channel` message rows as `channel.posted` (`Plugins/ChannelPosts.cs`; the relay's `--not-by <id>` moves the watermark past that poster's own frames and prints none); the journal's `quests` rows as `quest.published`, `quest.closed` and `quest.close-lost` with one `events.missed` at the teller's first place and at each incomplete batch; each end the end keeping writes, whichever caller asked; a press no terminal waits on, as it settles |
| Work events told | `Plugins/WorkEventPlugins.cs`, `Plugins/WorkEventNews.cs`, `HookSet.Events` and `Tell`, `LandPerformer.Told` | `work/event` wired (EVENTHOOK1b, D244's note on the session kinds): the point is `HookPoints.Event`, last in `HookPoints.All` with the kit's entry (an observation; its sample is the driver's own frame under `run: "sample"`, the wire test's `ANSWERS` row, the trial's case on a handshake whose run is the sample's, `Cost`'s and the health sentence's words, Ask Daoris's sentence and the page's gloss `plugin.kit.kind.workEvent`), accepted at the handshake; a plugin on it is wanted only by the loop holding the lease, its queue joined before its start so `initialize` carries `events` (`grammar`, the last `seq` minted, `build`, `RelayBuild.Of` of the install's `app/daoris-driver/` once a process), started again whenever the lease moves; the reconcile's lease read keeps its last answer while the file is busy; three late answers stop it as `late`, an exit of its own held to the work wire's wait; `session.parked` and `session.ended` told as the lease's news teller (the shell's lease teller, the headless host's) claims each piece, with the record's quest and ask (an ending also its adapter, `SessionView.Adapter`, kept by the frame where it is one of this build's), `quest.parked` from the look's news in both loops, `events.missed` at that teller's first place with no cursor and at each incomplete batch; `work.landed` as the landing keeps a try coded landed or advanced; `event.unsent` in the machine log |
| The events relay's words and batch | `Plugins/Relay/RelaySaid.cs`, `RelayChecks.cs`, `RelayBatch.cs` | Every line the relay may write, a closed list of fixed sentences over checked ids, codes and counts, the one naming a path being `--where`'s command, which says to read the batch from the output file the end names; the first line's closed schema (512 bytes, five fields, never `sample` or twelve zeros), `--since`'s bracket, a frame checked by the grammar (4 KB, its run never the sample); the watermark, the first line's gap or another build's update line at once, one gap line a batch at the first's place, repeats and frames outside the grammar tallied, a kind left out moving the watermark, at most 50 event lines and a count, and every batch closing on `read up to` |
| The events relay's verb | `Plugins/Relay/RelayCommand.cs` (`events relay`), `RelayAsk.cs`, `RelayCopy.cs`, `RelayPipe.cs`, `RelayRun.cs`; `Instance/UserPipe.cs` | EVENTHOOK1c: routed at the host's entry after the role terminal's gate (which refuses it, being no verb of `RoleTerminal.Runs`) and before the home's adoption, the generic lease and the machine log, one catch saying every failure as `the relay stopped (<code>)`; `--where` adopts the install's home, holds its lease by `HomeLease.ForVerb` (refused as `HeldByAnother`), lays the running build out at `bin/events-relay/<RelayBuild.Of>` staged and moved whole with its `in-use` file, removes a build none holds by an exclusive open and a rename first, deletes a stopped removal, follows no junction, and prints the copy's command; `--once` drops the holder key the entry took and strips the service, remote and messaging variables, runs only from a whole copy whose `in-use` it holds shared for reading, removes a dead relay's subscriber file, serves `daoris-event-<32 hex>` by `UserPipe.Security` for two instances (a Unix socket in the subscribers' folder elsewhere), puts its `<pid>.json` in place whole, refuses a client whose holder is no instance or whose agents' job (`ProcessJob.NamedHolds`) answers no, takes one the job cannot answer untold, checks the first line within 5 s, ends on a second client, three bad frames, the connection's end, the 30 s gathering, `--session-pid`'s exit (printing nothing) or the 15-minute bound read off its clock while an instance holds the home or the lease is a doubt (`HomeLease.HeldBy(home, out doubt)`), and prints its tallies, then its batch; `work/event`'s handshake names the set's `run`, which the plugin's first line carries. The plugin is Daoris.Plugins' `event-relay` (§5 of the design); Daoris holds none of it, and the relay's Process tests run their own stand-in for it, `Driver.Tests/Plugins/RelayStandIn/` (the hook wire and the relay's first line and frames, no more) |
| Work events' frame and teller | `Plugins/Domain/WorkEventGrammar.cs`, `HookFrames.Event` (`Plugins/Domain/HookPoints.cs`), `Plugins/WorkEventPoint.cs`, `Plugins/RelayBuild.cs` | `work/event`'s parts: the grammar (version 3: `session.parked`, `quest.parked`, `session.ended`, `work.landed`, `events.missed`, the version 2 kinds `quest.published`, `quest.closed`, `quest.close-lost`, `confirmation.ended`, `work.ended`, and version 3's `channel.posted`, each kind's closed list of ids, codes, counts and a UTC time); the frame built by it, an optional field that fails left out and an event whose required field fails told as `events.missed` in its place; the teller, which mints every `seq` under one lock whether or not it tells, tells only while `Held`, keeps a queue of 256 and a teller per plugin that joined (the oldest dropped and counted in the next frame's `missed`), and stops a plugin after three late answers in a row; a build's hash over its whole bundle, 64 zeros where there is none |
| Systems | `Plugins/Domain/PluginSystems.cs`, `Plugins/Systems/` (`SystemAdapter`, `SystemAdapters`, `SystemReadBack`, `SystemPlanning`, `SystemAdd`), `Service/Domain/SystemPlace.cs`, `HookPoints.System`, `HookPeer.SystemAsync` | A manifest's `systems` read; a system's adapter started from the catalogue and asked to plan and apply; a system department's registration composed from the plugins that adapt it (`SystemAdd`, which the registry door sends whole), a system guarded by `none` read before and after each write (the driving area keeps the plans, the workflows area the apply) |
| Switch | `Plugins/PluginState.cs`, `Plugins/PluginSwitches.cs`, `Plugins/Domain/PluginSwitchTable.cs`, `Plugins/Domain/PluginPlace.cs`, `Plugins/WorkSwitched.cs`, `Plugins/PluginSwitchVerb.cs`; `Modules/Bridge/Routes/PluginSwitchRoutes.cs` | `<home>/plugins.json`: the disabled rows, and `switches`, the person's word on each interface and point per place, read by one resolver at every door; `daoris plugin switch` and `PLUGIN_SWITCH` |
| Family | `Plugins/Domain/PluginFamily.cs`, `Plugins/PluginKitFamily.cs`, `Plugins/PluginTerminalWord.cs` | An id's family and tail; the kit's refusal of an id with no family or in Daoris's own; the plugin a terminal's word names |
| Rename | `Plugins/Domain/PluginFormerly.cs`, `Plugins/PluginRenames.cs`, `Plugins/PluginRenamedAttempts.cs` (the port), `Plugins/PluginsAtStart.cs`, `Workflows/PluginRenamedAttemptsWiring.cs`, `Workflows/PluginFormerKinds.cs` | A manifest's `formerly` read; the old id's copy, switches, data, storage, approvals and attempts in flight moved to the new id once, before each loop's plugins start; a saved graph's former kinds read and drawn as the new id's |
| Interfaces | `Plugins/Domain/PluginInterfaces.cs` (`PluginCommandReader`, `PluginViewReader`, `PluginInterfaceFields`), `PluginInterfaceChanges.cs`, `PluginInterfaceLines.cs`, `Plugins/Domain/CommandWire.cs`, `Plugins/Domain/CommandCall.cs`, `Plugins/Domain/CommandArgs.cs`, `Plugins/PluginCommandRun.cs`, `Modules/Bridge/Routes/PluginCommandRoutes.cs`, `Plugins/PluginCommandConsole.cs`, `Plugins/PluginPrivate.cs`, `Plugins/PluginTokenAdoption.cs` | A manifest's `interfaces` read entry by entry; an update's rows per entry; what is not reached, said; a command's rounds at `command/run`; `daoris <plugin> <command>`; the private storage handed; a git identity's API token adopted once |
| Agents' tools | `Plugins/PluginToolList.cs`, `Plugins/PluginToolCall.cs`, `Plugins/PluginToolsServer.cs`, `Plugins/PluginToolsHand.cs`; `Agents/SessionServers.cs` | A session's `daoris-plugins` server: the tool commands at its place, a call as one round `from: agent`, `daoris plugin tools`, the entry a session is handed |
| Doors | `Plugins/PluginCommand.cs`, `PluginsCommand.cs`, `PluginKitCommand.cs`, `PluginTryAgain.cs`; `Modules/Bridge/Routes/PluginsRoutes.cs` | `daoris plugin` and `plugins`; the `PLUGIN*` routes |
| Page | `src/Daoris.Web/src/plugins/`, `bridge/plugins.ts`, `settings/plugins/PluginSettings.tsx` | The view, list, plugin and offer pages, icon; Settings' read-only list |
| Page's interfaces | `plugins/interfaces.ts`, `PluginSetup.tsx`, `RoundCard.tsx`, `usePluginCommands.tsx`, `commandAsks.ts`; `bridge/pluginCommand.ts`, `bridge/pluginSwitch.ts`; `menus/pluginCommands.ts` | The *Set up* tab, the round card over `PLUGIN_COMMAND`, the switches over `PLUGIN_SWITCH`, the palette's plugin commands |

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

## Its rules

1. A plugin is a folder in `<home>/plugins/<id>/` with `plugin.json`. Its code runs only as its own process, never
   inside Daoris. (from D64)
2. `apiVersion` is read before anything else; a newer one is refused naming both numbers. The hook wire's
   `protocolVersion` is 1. (from D64, D148)
3. A broken manifest is a named problem on its row, never a crash. A harness name this build carries, or one an
   earlier plugin declared, refuses the later plugin before anything loads, naming both; a server name declared twice in one manifest refuses that plugin. A plugin never replaces. (from D64)
4. A problem in `icon`, `tools` or an `interfaces` entry never refuses the plugin; a problem in `steps` or `resources` does. (from D140, D150, D173, D274)
5. Off is a row in `plugins.json`, never a rename. An installed plugin is on until the person switches it off; a
   remove clears its row and its switches and leaves `.data/<id>`. (from D64, D120)
5a. `plugins.json`'s `switches` holds, by plugin, by what it switches (`commands`, `tools`, `views`, `storage`, or a point of
    `HookPoints.All`), by place (`machine`, `workspace:<name>`, `repository:<id>`), the person's word, `true` or `false`. Read for a
    place, the repository's word decides, else its workspace's, else the machine's, else it is on; `storage` reads the machine's word
    alone and is written nowhere else; `on` at the machine is the default and leaves no word; a plugin in `disabled` is off over all
    of it, and an unreadable file switches nothing off and refuses an edit. One resolver (`PluginSwitches`, its table
    `PluginSwitchTable`) is read at every door: a command's run at its place (`PluginCommandRun.Find`), the storage handed, the
    adoption, `quest/consider` at the quest's repository and workspace, `session/ended` at the session's repository, and the work
    wire, whose units at a place where `work/watch` or `work/perform` is off are not told (`WorkSwitched`), so that place's process is
    idle. `daoris plugin switch <id> <what> on|off [--place <place>]` and `PLUGIN_SWITCH` {id, what, place?, on} (Person) write one
    word, the loop looking at once; `PLUGINS` answers each plugin's `switches`, its words by what and place. The migration's
    `plugin-switches` step moves an older home's `off` lists once into machine words. (from D274)
6. A process may listen only on points its manifest declares, or it is refused. A point this build lacks is named
   once and never asked. (from D101, D148)
7. `quest/consider` is a waterfall in catalogue order that fails closed. The first hold ends it; a late, wrong or
   missing answer is a hold naming the plugin. (from D64)
8. `session/ended` and `state/changed` are contained. `state/changed` is queued off the press's path and cannot hold
   it. (from D64, D169)
9. Daoris pushes through its own push act and opens no pull request; a plugin's `pull-request` step on `work/perform` does, as the
   person. No plugin is started for a landing, and a `work/land` point is no longer spoken. A decision waits 10 s. (from D100, D101, D241)
10. `work/state` asks one plugin: the one that pushed the branch, else the landing rule's, and only if installed, on,
    sound and listening. A frame waits 30 s, no frame starts after 60 s, an entry is asked once a minute at most. A landing with no
    answer (its plugin never started, or no address was kept) that the keeper proved its line holds reads as its pull request merged,
    so a run's monitor goes on and its pull-request step not started yet is passed over (`PullRequestReading.OnTheLine`). (from D148)
11. It is asked at LAND3's tidy, the clean-up's look, bringing up to date's look, *Ask again*, origin's line moving,
    after Daoris pushes, and window focus while a pull request is open. Never at a press that removes, a read or a timer. (from D148)
12. A `completed` answer clears a branch only where git confirms: the target is the line, the merge commit is held
    here, and the recorded tip is the source commit or its ancestor. No answer removes nothing; a failure never
    overwrites a kept answer. (from D148)
13. Every install door writes `.daoris-source.json` into the staged copy before the swap: `{folder}`,
    `{offer, digest}` or `{package, version, sha512, source}`. No record is said, never guessed; an unreadable one is named; the list, the page and `plugin show` say the source by its kind: folder, offer, package, none or unread. (from D103, D119, D120)
14. The window's add adds and never replaces. Only the terminal's `daoris plugin add` replaces an installed plugin,
    wholesale. A source inside or holding the home is refused. (from D103, D194)
15. An update shows its plan first: version, command, points, harnesses, servers, skills, steps and resources, then an
    `interfaces` row per entry added, removed or changed, compared whole by its name or id (`PluginInterfaceChanges`), each with its
    interface and name and marked `asksMore` where it asks more of the person: a command gaining `tool`, `writes`, `adopts` or an
    argument, a view gaining `context`, storage newly declared. It applies only on a press or `--yes`. The installed folder is moved aside whole, the copy renamed in, `.data/<id>`
    untouched, and nothing starts at the press. A rename that fails puts the installed folder back; where that fails
    too, the refusal names the `.removing-*` folder the plugin now lies in. (from D103)
16. A plugin from a package is never updated in place: it is removed and a newer package installed. (from D120)
17. Offers are `app/plugin-offers/` in the install, laid out each publish from `examples/plugins/`: Daoris's own
    `daoris-github`, `daoris-azure-devops` and `daoris-in-app-browser`. None installs without a press. The driver looks beside the
    application first, then beside the home. An offer is listed as installed where its id or one it was formerly is
    (`PluginOffers.For`). (from D103, D274)
17a. Daoris's GitHub and Azure DevOps plugins (3.2.0) declare a `goal` on their `pull-request` step and carry `create-pull-request`,
    `pull-request-status` and `complete-pull-request` as tools, so the loop's step session performs it (workflows area, rule 31a); an
    attempt delivered on `work/perform` before stays theirs to settle by their `check`, and they answer `work/state`. The Azure DevOps one's step
    sets the pull request to auto-complete by the token's own identity where its options say `autoComplete: true` (with `mergeStrategy`,
    `deleteSourceBranch`, `transitionWorkItems`, `mergeCommitMessage`), its evidence saying whether it was set and a refusal never failing it,
    so a `monitor` reads the merge Azure DevOps makes; its second step, `release`, finds the merged commit's build on the target branch and
    the classic release made from it, or the pipeline run's own stages, and watches each awaited stage to its end, starting one by `deploy`
    where nothing started it, a pending approval said and waited on. With no options it serves a repository whose build several release
    definitions take: it watches the releases made from that build by the definitions whose CD trigger takes its branch (a release whose
    stages all start by hand left alone), and a merge no pipeline builds (every CI trigger off or its branch and path filters, a YAML
    pipeline's read at the merge commit, leaving it out) reaches saying nothing is to release. Both plugins call their
    service's REST API with the token their own `login` command keeps in their private storage (rule 29), and say a refused one at
    `status`. A place whose hand is refused (no origin, the identity serving it read only, Write denied: `PlaceHands`) is not started:
    the refusal is logged as `refused`, never as a start that failed (`unstartable`), counts as no exit toward unsound, and the hand is
    asked again a minute later (`HandRefusals`), so access allowed later starts the process with no press. `plugin try` asks the same
    question of the checkout it is run in (`PluginTrialHand`, the step `place-hand`); with no origin to ask it says so and passes. They run no git, `gh` or `az`, and declare
    node alone (22 or newer for the Azure DevOps one). Where no token is kept, or Azure DevOps refuses the kept one, the Azure DevOps
    plugin makes its calls (the step, `work/state`, the release watch, the tools) from a tab of its own in the person's signed-in
    browser: its manifest says `hooks.browser`, so each of its processes is handed the in-app browser's DevTools address as
    `DAORIS_BROWSER` while a shell carries one (`PluginBrowser`, asked for the address only), and each call is a page-side `fetch` that
    reads no cookie or token; a lapsed sign-in is renewed once, then fails `signed-out`; every answer's evidence ends `Way: the token.`
    or `Way: the browser.` (D281). A plugin that declares a `pull-request` step has one work process per place (repository and workspace) that awaits
    it, never one shared: `PlacedWork` splits the awaited units by place under a key beneath the plugin's id, so each process's waits,
    unsound mark (three unasked exits within an hour, after which it is not started until the person's *Try again*: `try-again` on
    `PLUGIN_ACTION`, `daoris plugin try-again <id>`, and the button on its page while `PLUGINS` says `unsound`, clearing every place's
    mark through `HookSet.TryAgain`) and idle stop are its place's own, and `PlaceHands` (given to `HookSet` by `PluginsAtStart`) reads
    that place's origin and judges that process's start. An attempt goes to the place its unit was told for; the runtime
    sees the plugin's id, its generations unique across the places (`GenerationOf(plugin, held)` fences against the process that took
    the attempt). A refused place has no process, and the others are served. (from D241, D242)
17b. `work/perform`'s params carry `options`, an object of the performed node's graph options (`NodeDeclaration.Options`) by name, each
    its JSON value, or its text where that is not JSON; none are written where the node has none. `WorkPerformer` fills them at every
    delivery from the declaration it performs and never keeps them on the attempt's record, so an edited graph reaches a redelivery;
    the kit's `work.mjs` hands them to `act()` as `call.options`, `{}` when absent.
17c. A manifest's `systems` lists the systems the plugin adapts, each `{ id, name, preconditions }`: its id lowercase letters, digits
    and dashes, at most 40, once; its name its id where unwritten; `preconditions` `etag` (the default), `serial`, `changeset` or `none`.
    A plugin declaring one must name `plan/read` and `apply/perform` in `hooks.points`; any problem refuses the plugin, as a step's does.
    The two are `HookPoints.System`, last in `All` with their kit entries (`PluginKitSystems`): accepted at the handshake and asked only
    by a system's plan and apply, never by the loop's set. `plan/read` takes `{ system, changes: [{ resource, value } | { resource, remove }] }`
    and answers `{ operations: [{ resource, op: create | update | delete, before, after, precondition: { kind, version } }] }`, a version
    null where the resource must still be absent and a change the system already holds left out; `apply/perform` takes
    `{ system, plan, operations }` and answers `applied` with the resources applied, `unchanged` with none where the system already holds
    what every operation asks and wrote nothing, `stale` with the first `refused` and each stale resource's `present` value and version,
    or `failed` with its `message`. An answer of another shape is refused, nothing kept. A system guarded by `none` is applied by
    `SystemReadBack`: each operation's resource planned alone is its read, a version other than the plan's is `stale` with nothing of it
    written, the operation is sent alone, and a read-back that still has something to change is `changed` while applying, Daoris's own
    outcome and never an adapter's answer. `examples/plugins/stand-in-system` is the proof's adapter, a key-value store with an etag per
    key in its data folder, declared twice (`stand-in` by `etag`, `stand-in-unguarded` by `none`), offered nowhere. The Plugins view's
    `PLUGINS` names each system's `preconditions`, from which the repository list says a system department of `none` is *unguarded*.
18. An offer's installed copy is judged against the shipped one. Same version, other files: `changed`. Older, files
    matching the record's digest: `older`. Older, digest not matching: `changed`. Older, no digest: `older`,
    unjudged. Newer: the person's, said nothing. A pre-release tag (`1.2.0-beta.1`) is older than the same numbers without one, and its dotted parts compare number by number (`beta.2` before `beta.10`). (from D119)
19. A `changed` copy is never overwritten by an update. A copy with no record, or one that does not read, may take
    the shipped copy (`PLUGIN_UPDATE` with `shipped: true`), which records the offer and keeps `.data/` and the switch; the route also takes one recorded as a folder or an offer, never a package's (rule 16). (from D119)
20. `plugins new` writes a self-contained folder (manifest with `tools: []` and `interfaces` declaring the sample command `hello`,
    `storage` and the view stub `overview` on its page; `plugin.mjs`, answering `command/run` for `hello`, which keeps a count in its
    private storage, and `view/read` with a one-section tree; `plugin.test.mjs`; README; `storage.mjs`, the kit's record store, with
    `storage.test.mjs` (`PluginKitStorage`), which writes one record of a JSON file at a time under a `<file>.lock` held by
    exclusive create, reading the file fresh inside it and renaming the whole into place, takes over a lock whose holder's pid has
    gone or that is 30 s old, and refuses and keeps a file that does not read; `work.mjs` for a plugin on a work point, keeping
    `work-journal.json` through that store record by record, so the work processes of a plugin's places, which share `.data/<id>`,
    keep each other's attempts, cleanups and observations, and for one that performs, `plugin.contract.test.mjs` and `cycle.json`; for one on a system point,
    both system points, one system `system` guarded by `etag`, and `system.mjs`, the kit's answers to both over the author's `read()`
    and `write()` with a stand-in store in the kit's world) and refuses rather than overwrites. It asks for the family (rule 30): an id
    naming none, and one in Daoris's own `daoris-` family anywhere but this repository's `examples/plugins/` (the checkout holding
    `canon/` and the desktop's source), are refused, each saying why and naming `<your-family>-<name>` (`PluginKitFamily`). It installs nothing and scaffolds no harness. Its samples come from `HookFrames` and `SystemWire`. (from D101, D150)
21. `plugins try` starts the plugin as the driver would, in scratch under `<home>/plugins/.trials/`, one sentence per
    check; exit 0, 1 or 2. Its `interfaces` step (`PluginTrialInterfaces`), for a plugin that declares any, fails on each entry
    refused, an interface this build does not know and a forget not run, and otherwise says the commands, storage and views read;
    it runs no command, since a command reaches its platform. It fails a declared point the process does not listen on. A system point's sample names the plugin's first
    system; `apply/perform`, like `work/perform`, goes only to a plugin that took the kit's stand-in world. (from D101)
22. A sample frame names no repository: an empty scratch root and a `GIT_DIR` that is not there. (from D101)
23. A tool is found and checked only at a trial or on a press, never at a load or on a timer. A check's `fix` (such
    as `az login`) runs only on the person's press, in a console they see. Daoris never signs in or downloads. (from D150)
24. A server goes to sessions by default. One handed to Ask Daoris reaches it only once the person approved its exact
    tools; a write only as an accepted write; a generic execution tool never. A server that runs a package through `npx`,
    declared so or handed on after a node start's `--` (`node server.mjs -- npx …`), names the package and its version; a
    session is handed node and that package's script in npx's place, installed once into the toolchain, never `npx` itself,
    and where the server drives Daoris's browser that package's start stays under a 512 MB ceiling by a restart between
    calls (agents area, rule 13). An approval is the person's and stands while the plugin is switched off: its page and the list read
    it (`HelpGrants.Read(withSwitchedOff)`), the page's press takes it, and a switched-off plugin's grant is handed
    nothing. (from D170)
25. What a plugin did is kept without its words: `plugin.*` lines hold names, counts, flags and times. Its words go
    to the console under `plugin:<id>` for this run. (from D119, D94)
26. An icon is a path inside the folder to an SVG or PNG of at most 32 KiB, a PNG at most 512 px a side. The page gets its bytes as a data URI,
    drawn in an `<img>`, never a path; else a monogram on a hue hashed from the id. (from D140)
27. The page is a catalogue: installed (refused while on, then on, then off), then Daoris's own not installed. Name,
    description and version stay as declared. No plugin adds a view or runs code in the page. (from D119, D140, D52)
27a. A plugin's page has the tabs *Set up* and *About* where it declares an interface or a point (*Set up* first where it declares
    an interface; *About* holds today's sections), and no tab row otherwise. *Set up* lists each command (its authored title in the
    viewer's language, its name, *asks you*, *changes things*, *for agents*, *takes a git identity's token*, and *Run*, disabled with
    the reason where the plugin is off or refused or its commands are off at the window's place), each entry refused alone drawn with
    *not read* and the reader's sentence and nothing to press, the storage's `for` and whether a removal signs it out, the views as
    declared, the interfaces this build does not know, and *Where it is on*: a box per interface and point and per place (the machine,
    each place holding a word, each the person adds), read as the resolver reads it, `storage` at the machine alone, a press sending
    `PLUGIN_SWITCH`. An offer's page lists the same commands, storage and views before *Install*; the update plan names an interface
    row by its interface and entry and marks *asks more of you*. A command runs in the round card under the page's header, at the
    window's scope's workspace else the machine: its arguments and its `confirm` asked first where it has either, then each round's
    `said` in order, a page to open in the person's own browser with its host, a code with *Copy*, a field per ask (a password field
    where masked, its value sent with the answers so far and never drawn back), *Go on* and *Cancel*; its end says done or the exit,
    `data` as handed back, and `then` (`reload`, the default, asks `PLUGINS` again; `back` closes the card; `stay` does nothing).
    The palette lists each command of a plugin on, sound and with commands on at the machine (`run.pluginCommand:<id>/<command>`,
    `PLUGINS` asked only while the palette is open), and a row opens the plugin's page at its card. *Remove…* where `storage.forget`
    names a command offers *Ask it to sign out … first*, ticked, which runs that command one round from the window before
    `PLUGIN_ACTION` removes it, saying whether the plugin confirmed; a disabled `forget` says why. (from D274)
28. Every plugin press is the person's: a session's terminal is refused. With Daoris running a verb takes the route,
    else it acts alone under the home's lease. (from D194)
29. A manifest's `interfaces` names what the plugin implements that Daoris reaches it through besides its points: `commands` (each
    `{ name, title, about, interactive, tool, writes, confirm, on, args, adopts }`), `storage` (`{ for, forget }`) and `views` (each
    `{ id, title, icon, where, places, context, body }`), every word authored (D222). A command's `title` is its name where unwritten;
    `args` are `{ name, type: text | number | boolean | choice, title, required, choices }`, `choices` only and always for a `choice`;
    `on` lists `session`, `quest`, `run`, `repository` or `workspace`; a `tool` never asks the person; one command at most adopts. A
    view's `where` is `side` and `page` (`page` unwritten), `places` `machine`, `workspace` and `repository` (`machine` unwritten),
    `context` `session` and `quest`, `icon` a path inside the folder, and `body` `drawn` alone. A problem refuses that entry alone,
    never the plugin, and is kept in `Refused` by interface and name; an interface this build does not know is kept in `Unknown` and
    skipped; an interface needs `hooks`, else each is refused. `forget` names a declared command that runs alone, and one naming a
    refused, missing or asking command is kept and disabled with the reason. `daoris plugin list` says each (`PluginInterfaceLines`),
    and `PLUGINS` answers a plugin's `interfaces` whole.
    A command runs one round per process at `hook/command/run` (`CommandCall`: `{ command, run, args, answers, from, place, language,
    session? }`, never a point claimed at the handshake): `run` 32 hex, one per run and the same across its rounds; `args` an object by
    name, each value of its declared type (`CommandArgs`); `from` `terminal`, `window`, `adoption`, `agent` or `removal`, always
    Daoris's word; `place` `machine` or `{ kind: repository | workspace, id }`; `language` the viewer's, `en` at the terminal;
    `session` where an agent calls. It is answered `{ said, ask: { name, prompt, masked }, open, code, then, data, exit }`, `said` at
    most 40 lines of 2,000 characters and 8 KiB in all (`CommandWire.SaidBytes`, under the work wire's 64 KiB frame), `then` `stay`,
    `reload`, `back` or `{ go: { view, args } }`, `data` an object of at most 32 KiB for an agent, at most 8 rounds, 60 s each, the
    answers so far sent whole. `daoris <plugin> <command> [args] [--stdin]` is the word no group owns that names an installed plugin
    running commands (rule 30); its words are the arguments as `--<name> <value>` (a boolean's flag alone true) or in declared order,
    a required one not given asked by its title; it asks each value at the terminal, hidden where `masked`, and refuses a session's
    terminal. The window's door is `PLUGIN_COMMAND` (`Window`, `PluginCommandRoutes`): one round per call, `from: window`, at the
    place it names, an argument missing refused before any process starts, the answer handing back the run's id, and the plugins slice pushed after each round, since a command may move a plugin's health or state. `storage` is `profiles/plugins/<id>/`,
    limited to the account, handed as `DAORIS_PLUGIN_PRIVATE` to that plugin's processes alone, removed with the plugin; Daoris reads
    nothing in it. Each interface is on until the person switches it off, per place (rule 5a). A command declaring `adopts`
    (`hosts`, `answer`) is sent, once at the loop's start, the API token of the first identity serving one of those hosts, which then
    keeps none; `plugin.token.adopted` names ids alone. Daoris names no plugin in any of it: every word comes from the manifest and the
    plugin's answers. (from D274)
29a. A command marked `tool` reaches an agent through one MCP server per session, `daoris-plugins`: `SessionServers` hands a quest's
    session, at its start and its going on, the entry its caller built (`PluginToolsHand.OfQuest`, which `Agents` cannot name): the desktop's program as `daoris plugin tools --session <id> [--repository <id>]
    [--workspace <name>] --language en|zh`, the home in the entry's own variables, only where its place lists a tool and the program is
    found (the install's `app/daoris-driver/`, else the running program where it is that one; `PluginToolsHand`). It lists
    (`PluginToolList`) each command marked `tool` and not `interactive` of each installed plugin that is on, not refused and speaks,
    where its `commands` and `tools` are both on at the session's place, read again at each list and call; each is named
    `<plugin>_<command>` (an id and a name hold no underscore, and the model APIs' tool names take no dot), its description the
    command's `about` and its title its `title` in the session's language, its input schema from `args` (`text` and `choice` strings,
    a choice's `enum`, the required ones), annotated read-only unless it `writes`. A call (`PluginToolCall`) is one round at
    `command/run` `from: agent` with the session, its place and language: `said` is the text, `data` the structured content, an exit
    other than 0 an error; a round that asks is refused and never answered, since no person answers a tool call; an argument missing or
    not of its type is refused before any process starts; a tool not listed there is a `-32602` error. Every round is a `plugin.command`
    line by names alone (plugin, command, from, session, exit, ms). No call asks an approval: the switch `tools` per place is the
    opt-in. Ask Daoris, a reviewer, a helper and a partner are handed no such server. Started for an agent-performed step's session
    (`--plugin <id> --step <invocation>`, `PluginToolsHand.StepEntry`), it lists that plugin's tools alone and `step_answer`, which
    keeps the step's answer (workflows area, rule 31a). (from D274)
30. A plugin's id is `<family>-<name>`, lowercase with dashes: Daoris's own plugins are the `daoris-` family (`daoris-github`,
    `daoris-azure-devops`, `daoris-in-app-browser`), and anyone else's uses its own family, never `daoris-`. A manifest's `formerly`
    names the ids the plugin was, each a plugin id other than its own, once; a problem refuses the plugin. The family is the word before
    the id's first dash and its tail the rest (`PluginFamily`). A terminal names a plugin running commands by its id, or by its tail
    where no other installed plugin running commands has that tail (`daoris azure-devops login` for `daoris-azure-devops`), an id named
    whole coming first and a tail two of them share refused with each named (`PluginTerminalWord`); the tail is the id's own, never a
    second name a manifest declares. An installed plugin declaring `formerly` takes over the old id once, at each loop's start, the
    desktop's and the headless host's (`drive`, `--once` among them), before its plugins start (`PluginsAtStart`), and after any install
    door that put it in (`PluginRenames`): the copy installed under the old id is taken out, and
    its switches (`disabled`, `switches`), `.data/<old>`, `profiles/plugins/<old>/` and its approvals move to the new id, what the new id
    already keeps never overwritten and said as left, and each attempt not settled that names the old id names the new one with its
    step's kind (`PluginRenamedAttempts`, which Workflows installs since the node records are its own), so the new process is asked after it at its next look, written only by the process holding
    the home's lease, else at the next start; `plugin.renamed` names the ids and the parts moved and left, and a later start
    finds nothing to move. A copy recorded from an offer updates from the offer that was formerly it, its plan's `id` row naming the
    rename, into the new id's folder (`PluginUpdatePlan.Next`). A saved workflow's `<old>/<step>` is read as `<new>/<step>` by the
    store (`PluginFormerKinds`), and the graph the editor and `workflow show` are handed is drawn with the new kind, no save asked
    (`PluginFormerKinds.Drawn`): the file stays as written and each version keeps the digest it was saved with, so a run pinned to it
    reads at the identity it was bound to and goes on, its pull-request and later steps performed by the new id; a landing's recorded
    plugin is asked at `work/state` by its new id. A schema keeps its name across a rename. The old kind ends when no installed plugin
    declares the id it names, after which it binds as any step no plugin performs does. (from D274)
31. Settings sets nothing about a plugin. Settings → Driver lists the installed plugins read-only, each opening its
    page. (from D119, D183)

## Where a change goes

- A new point or frame field: `HookPoints`, `HookFrames` and the kit's entry in `PluginKit.Points`. A point with no kit
  entry fails `PluginKitTests`.
- A manifest field: `PluginCatalog` and `PluginManifest`, the update's rows in `PluginChange.Between`, the page's `catalog.ts`.
- Install, update, the source record: `PluginInstall`, `PluginSource`, `PluginFolders`; the route in `PluginsRoutes`,
  the terminal in `PluginCommand`.
- Offers: `OFFERED_PLUGINS` in `tools/desktop-publish/lay.mjs` and `PluginOffers`.
- A shipped plugin's behaviour: its folder in `examples/plugins/`, its version and `changes` line, its README's
  `## What it needs`. The kit's files (`work.mjs`, `storage.mjs` and the three tests) change in every working example
  (`local-preview`, `local-release`, `daoris-azure-devops`, `daoris-github`) in the same commit, which `PluginKitExampleTests` holds.
- Tools and sign-in: `PluginTools`, `PluginToolChecks`, `PluginToolSetUp`.
- The page: `src/Daoris.Web/src/plugins/`, chrome through both catalogues.
- Steps, resources and the work wire: the workflows area.

**Tests.** `Daoris.Desktop.Driver.Tests/Plugins/` (`PluginCatalogTests`, `PluginKitTests`, `PluginKitExampleTests`,
`PluginKitInterfacesTests`, `PluginInterfacesTests`, `PluginInterfaceChangesTests`, `PluginCommandRunTests`, `PluginInstallTests`,
`PluginSourceTests`, `PluginShippedTests`, `PluginOfferTests`, `HookTests`, `HookStateTests`,
`PluginToolsTests`, `PluginToolSetUpTests`, `RefusedHandTests`, `PluginIconTests`, `PluginPackageTests`, `HelpGrantsTests`, `WorkEventTellerTests`,
`WorkEventGrammarTests`, `WorkEventPointTests`, `RelayBuildTests`, `WorkEventWiringTests`, `PluginSystemsTests`, `PluginKitSystemTests`,
`PluginRenamesTests`, `PluginSwitchesTests`, `HookChangedTests`, `PluginToolsServerTests`, `PluginToolsProcessTests`); `Install/PluginSwitchesStepTests`; `Workflows/PluginFormerKindsTests`; `Departments/` (`SystemPlanTests`,
`SystemReadBackTests`, the stand-in's process proofs `SystemPlanProcessTests` and `SystemReadBackProcessTests`);
`Workplace/` (`PullRequestAskTests`, `PullRequestBoundsTests`, `PullRequestMomentsGitTests`, `WorkEndsGitTests`); `RelayHostProcessTests`; `Daoris.Desktop.Modules.Tests/Platform/PipeConfirmationSettledTests`; the service's
`Quests/QuestJournalTests`;
`Daoris.Desktop.Modules.Tests/Bridge/` (`DriverModulePlugins*Tests`, `PluginsBothWaysTests`, `PluginRecordWiringTests`,
`HelpPluginApprovalDoorTests`); the page's `plugins/*.test.ts(x)` and `settings/plugins/PluginSettings.test.tsx`; each
example's `node --test`.

**Gates.** The driver lane: the desktop suites, `rehearse-family`, `deployment`. The modules lane: `modules`,
`censuses`, `modules-process`, `deployment`. `examples/plugins/**`: `service`, `driver`, `driver-process`,
`rehearse-family`, `web`, `deployment`. The page: `web-unit`, `web-types`, `web-rules`, `web`.

## Contradictions

- D103, D140, D150 and D64's PLUGINRESERVE1 note make the CLI and the driver twin readers; D194 retired the CLI's plugin code, and the driver reads alone (`PluginCatalog`'s remarks). D194 reads right; stale twin comments remain (`PluginCatalog.Reserved`, `PluginManifest`'s `OwnHome`, `PLUGIN_OFFERS` in `tools/desktop-publish/lay.mjs`).
- D120 §3 and §5 move the landing plugins and `in-app-browser` to the plugins repository and pin offers to packages; the publish lays them out from this repository (`OFFERED_PLUGINS` in `tools/desktop-publish/lay.mjs`, its `layOffers` call in `tools/desktop-publish.mjs`). The code reads right.
- D119 §2 names five states, ready and failing among them; the page draws four (`pluginState` in `src/Daoris.Web/src/plugins/catalog.ts`), though `PLUGINS` answers health (`PluginsAsync` in `PluginsRoutes.cs`). The code reads right; the decision is unbuilt.
- D119 §5 and §6 promise `PLUGIN_TEST`, `plugins test` and kept checks in `<home>/plugins/.checks/<id>.json`; none exists (`PluginsCommand.Usage`; the `PLUGIN*` rows of `docs/index/routes.md`). The code reads right.
- D119 §7 refuses a go to Settings → plugins; its PLUGUI1h note keeps it as a moved place that opens Plugins. The note reads right.
- D119 §8 says Settings keeps nothing of a plugin; D183's PLUGSET1 adds a read-only list (`settings/plugins/PluginSettings.tsx`). D183 reads right, since it sets nothing.
- D103 names five update rows; the code compares nine and a row per interface entry (`PluginChange.Between` in `Domain/PluginSource.cs`). The code reads right; neither shows a change to `tools` or `icon`.
- D101 puts the kit's New in Settings → Plugins; it is the Plugins view's (`PLUGIN_NEW`), and `PluginKit`'s remarks still say Settings. The view reads right.
- Ask Daoris's room says a plugin with no record cannot be updated (`Knowledge/Room/HelpRoomMayPropose.cs`, the plugins sentence); D119's LANDPIN3 lets an offer's copy take the shipped one. Both hold for their door; the room offers no shipped take.

## Open

- PLUGHOOK1d: ask a pull request's state again once the first pull request opens.
- FABRIC-PERFORM1l: keep a pull request's fields in the work wire's evidence (`WorkEvidence` in `WorkWire.cs`).
- EVENTHOOK1b is built whole: the kit's entry and the ending's adapter are in, and an intake's, a helper's and a reviewer's park are
  not told (D244's note). EVENTHOOK1c's proofs through the built host are `RelayHostProcessTests` (another act's lease
  held for the whole wait, an unwritable lease, a `bin` entry at both places, two `--where` at once, a newer build laid out while a relay
  runs, a plugin through a shim, and the host's own `drive --once` starting the plugin into its job; the swap is a build laid out beside
  the relay's, not the launcher's own rename of `app/`); steps 2 and 3 across real elevations stay unmeasured (UAC off here), `PipeAcross`
  kept to run on a UAC machine.
  The measurements both rest on: `docs/2026-11-06-event-hook-measurements.md`.
- EVENTHOOK1d's rest: `Settled`'s wiring proved through the shell's pipe, and each source told by no `--share` loop proved end to end (`docs/2026-11-06-event-channel-design.md` §7, EV-F's proof).
- EVENTHOOK1e: a relay that lives for the session (the same design §4; chunks EV-C to EV-E).
- No row: D120's workshop (`<home>/plugins/.workshop/`, `plugin-sources.json`, `daoris plugin workshop`, *Send to a repository…*).
- No row: D120's NuGet source (find, show, install by id, update from a source, *Available*, another publisher's installing off, pinned `plugin-offers.json`).
- No row: PLUGUI1f, the ready and failing states on the page from the loop's health.
- No row: D119 §5's run of a plugin's own tests, and its kept trial and test results.

