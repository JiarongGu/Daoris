# Plugins — how it works now

## What it does

A plugin is a folder with a `plugin.json` manifest. It extends Daoris without loading code into any Daoris process.
It may declare harnesses (agents on the ACP door), MCP servers, skills, tools, workflow steps and an icon. It may also
speak: its own process answers JSON-RPC frames on stdio at named points. The points are a decision (`quest/consider`),
observations (`session/ended`, `state/changed`), a query (`work/state`, a pull request's state)
and the work wire (`work/watch`, `work/step`, `work/perform`). Plugins live in `<home>/plugins/<id>/`, with
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
| Work wire | `Plugins/WorkPlugins.cs`, `Plugins/PlacedWork.cs`, `Plugins/PlaceHostTokens.cs`, `Plugins/PluginCycles.cs`, `Plugins/Domain/WorkWire.cs` | Performer and observer processes, on the lease's loop only (the workflows area owns the meaning) |
| Harness | `Plugins/PluginDoors.cs`, `Plugins/PluginAgentSkills.cs` | A manifest's harness converted to Agents' `DeclaredDoor` (its named fields alone), which Agents' `DeclaredDoors` makes a protocol-transport contract door and the registry places by its `accountOf`; the sound skills each contributing plugin hands a destination, as `SkillSource` values a door's skill layout lays out at spawn |
| Install | `Plugins/PluginInstall.cs` | Add, add an offer, plan and apply an update, replace, remove; every swap staged beside |
| Source record | `Plugins/Domain/PluginSource.cs` | `.daoris-source.json` (folder, offer with digest, or package); an update's change rows |
| Folders | `Plugins/PluginFolders.cs` | All home I/O for plugins; `Digest`, `HoldsDigest`, placeholders |
| Offers | `Plugins/PluginOffers.cs`, `tools/desktop-publish.mjs` (`OFFERED_PLUGINS`, `layOffers`) | `app/plugin-offers/` in the install, and where each door finds it |
| Shipped judgement | `Plugins/Domain/PluginShipped.cs` | An offer's installed copy: `older`, `changed`, or older and unjudged |
| Package | `Plugins/PluginPackage.cs`, `Plugins/PluginPackageFile.cs` | Reads and installs a `.nupkg` offline |
| Kit | `Plugins/PluginKit.cs`, `PluginKitFiles.cs`, `PluginTrials.cs`, `PluginTrialProcess.cs`, `plugin-kit/*.template` | `new` and `try` |
| Tools and sign-in | `Plugins/Domain/PluginTools.cs`, `PluginToolChecks.cs`, `PluginToolSetUp.cs` | Declared tools, their checks on a press, a fix in a console the person sees |
| Ask Daoris grants | `Plugins/HelpGrants.cs` | `<home>/plugin-approvals.json`: a server's tools the person approved |
| Health and log | `PluginHealth.cs`, `PluginLog.cs`, `PluginActivity.cs`, `Domain/PluginEvents.cs` | `plugin.*` lines; the loop's record of each plugin's health |
| Work events' sources | `Plugins/WorkEventTeller.cs`, `Events/WorkEvent.cs`; `Workplace/WorkEnds.cs`; `Modules/Platform/PipeConfirmations.cs` (`Settled`) | The kinds read from records, told at `work/event` (EV-F): the teller first in both lease feeds, `Settled` wired in `LoopRun.ServePipe`, `WorkEnds.Listen` in the hook set from the point's first use (`WorkEventPlugins.ListenEnds`); the grammar at version 3 with each kind's fields (`confirmation` 32 hex, `end` its five codes, a quest's `state` by its kind; `channel.posted`'s `by` of four classes, `message` 8 hex, `poster` a session's or a member's id, `urgent`) and the relay's sentence for each, the relay's default `--kinds` still the first four: the journal's `channel` message rows as `channel.posted` (`Plugins/ChannelPosts.cs`; the relay's `--not-by <id>` moves the watermark past that poster's own frames and prints none); the journal's `quests` rows as `quest.published`, `quest.closed` and `quest.close-lost` with one `events.missed` at the teller's first place and at each incomplete batch; each end the end keeping writes, whichever caller asked; a press no terminal waits on, as it settles |
| Work events told | `Plugins/WorkEventPlugins.cs`, `Plugins/WorkEventNews.cs`, `HookSet.Events` and `Tell`, `LandPerformer.Told` | `work/event` wired (EVENTHOOK1b, D244's note on the session kinds): the point is `HookPoints.Event`, last in `HookPoints.All` with the kit's entry (an observation; its sample is the driver's own frame under `run: "sample"`, the wire test's `ANSWERS` row, the trial's case on a handshake whose run is the sample's, `Cost`'s and the health sentence's words, Ask Daoris's sentence and the page's gloss `plugin.kit.kind.workEvent`), accepted at the handshake; a plugin on it is wanted only by the loop holding the lease, its queue joined before its start so `initialize` carries `events` (`grammar`, the last `seq` minted, `build`, `RelayBuild.Of` of the install's `app/daoris-driver/` once a process), started again whenever the lease moves; the reconcile's lease read keeps its last answer while the file is busy; three late answers stop it as `late`, an exit of its own held to the work wire's wait; `session.parked` and `session.ended` told as the lease's news teller (the shell's lease teller, the headless host's) claims each piece, with the record's quest and ask (an ending also its adapter, `SessionView.Adapter`, kept by the frame where it is one of this build's), `quest.parked` from the look's news in both loops, `events.missed` at that teller's first place with no cursor and at each incomplete batch; `work.landed` as the landing keeps a try coded landed or advanced; `event.unsent` in the machine log |
| The events relay's words and batch | `Plugins/Relay/RelaySaid.cs`, `RelayChecks.cs`, `RelayBatch.cs` | Every line the relay may write, a closed list of fixed sentences over checked ids, codes and counts, the one naming a path being `--where`'s command, which says to read the batch from the output file the end names; the first line's closed schema (512 bytes, five fields, never `sample` or twelve zeros), `--since`'s bracket, a frame checked by the grammar (4 KB, its run never the sample); the watermark, the first line's gap or another build's update line at once, one gap line a batch at the first's place, repeats and frames outside the grammar tallied, a kind left out moving the watermark, at most 50 event lines and a count, and every batch closing on `read up to` |
| The events relay's verb | `Plugins/Relay/RelayCommand.cs` (`events relay`), `RelayAsk.cs`, `RelayCopy.cs`, `RelayPipe.cs`, `RelayRun.cs`; `Instance/UserPipe.cs` | EVENTHOOK1c: routed at the host's entry after the role terminal's gate (which refuses it, being no verb of `RoleTerminal.Runs`) and before the home's adoption, the generic lease and the machine log, one catch saying every failure as `the relay stopped (<code>)`; `--where` adopts the install's home, holds its lease by `HomeLease.ForVerb` (refused as `HeldByAnother`), lays the running build out at `bin/events-relay/<RelayBuild.Of>` staged and moved whole with its `in-use` file, removes a build none holds by an exclusive open and a rename first, deletes a stopped removal, follows no junction, and prints the copy's command; `--once` drops the holder key the entry took and strips the service, remote and messaging variables, runs only from a whole copy whose `in-use` it holds shared for reading, removes a dead relay's subscriber file, serves `daoris-event-<32 hex>` by `UserPipe.Security` for two instances (a Unix socket in the subscribers' folder elsewhere), puts its `<pid>.json` in place whole, refuses a client whose holder is no instance or whose agents' job (`ProcessJob.NamedHolds`) answers no, takes one the job cannot answer untold, checks the first line within 5 s, ends on a second client, three bad frames, the connection's end, the 30 s gathering, `--session-pid`'s exit (printing nothing) or the 15-minute bound read off its clock while an instance holds the home or the lease is a doubt (`HomeLease.HeldBy(home, out doubt)`), and prints its tallies, then its batch; `work/event`'s handshake names the set's `run`, which the plugin's first line carries. The plugin is `examples/plugins/event-relay/` (§5 of the design), with its `node --test` |
| Work events' frame and teller | `Plugins/Domain/WorkEventGrammar.cs`, `HookFrames.Event` (`Plugins/Domain/HookPoints.cs`), `Plugins/WorkEventPoint.cs`, `Plugins/RelayBuild.cs` | `work/event`'s parts: the grammar (version 1: `session.parked`, `quest.parked`, `session.ended`, `work.landed`, `events.missed`, each kind's closed list of ids, codes, counts and a UTC time); the frame built by it, an optional field that fails left out and an event whose required field fails told as `events.missed` in its place; the teller, which mints every `seq` under one lock whether or not it tells, tells only while `Held`, keeps a queue of 256 and a teller per plugin that joined (the oldest dropped and counted in the next frame's `missed`), and stops a plugin after three late answers in a row; a build's hash over its whole bundle, 64 zeros where there is none |
| Switch | `Plugins/PluginState.cs` | `<home>/plugins.json`: the disabled rows |
| Doors | `PluginCommand.cs`, `PluginsCommand.cs`, `PluginKitCommand.cs`; `Modules/Bridge/Routes/PluginsRoutes.cs` | `daoris plugin` and `plugins`; the `PLUGIN*` routes |
| Page | `src/Daoris.Web/src/plugins/`, `bridge/plugins.ts`, `settings/plugins/PluginSettings.tsx` | The view, list, plugin and offer pages, icon; Settings' read-only list |

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

## Its rules

1. A plugin is a folder in `<home>/plugins/<id>/` with `plugin.json`. Its code runs only as its own process, never
   inside Daoris. (from D64)
2. `apiVersion` is read before anything else; a newer one is refused naming both numbers. The hook wire's
   `protocolVersion` is 1. (from D64, D148)
3. A broken manifest is a named problem on its row, never a crash. A harness name this build carries, or one an
   earlier plugin declared, refuses the later plugin before anything loads, naming both. A plugin never replaces. (from D64)
4. A problem in `icon` or `tools` never refuses the plugin; a problem in `steps` or `resources` does. (from D140, D150, D173)
5. Off is a row in `plugins.json`, never a rename. An installed plugin is on until the person switches it off; a
   remove clears its row and leaves `.data/<id>`. (from D64, D120)
6. A process may listen only on points its manifest declares, or it is refused. A point this build lacks is named
   once and never asked. (from D101, D148)
7. `quest/consider` is a waterfall in catalogue order that fails closed. The first hold ends it; a late, wrong or
   missing answer is a hold naming the plugin. (from D64)
8. `session/ended` and `state/changed` are contained. `state/changed` is queued off the press's path and cannot hold
   it. (from D64, D169)
9. Daoris pushes through its own push act and opens no pull request; a plugin's `pull-request` step on `work/perform` does, as the
   person. No plugin is started for a landing, and a `work/land` point is no longer spoken. A decision waits 10 s. (from D100, D101, D241)
10. `work/state` asks one plugin: the one that pushed the branch, else the landing rule's, and only if installed, on,
    sound and listening. A frame waits 30 s, no frame starts after 60 s, an entry is asked once a minute at most. (from D148)
11. It is asked at LAND3's tidy, the clean-up's look, bringing up to date's look, *Ask again*, origin's line moving,
    after Daoris pushes, and window focus while a pull request is open. Never at a press that removes, a read or a timer. (from D148)
12. A `completed` answer clears a branch only where git confirms: the target is the line, the merge commit is held
    here, and the recorded tip is the source commit or its ancestor. No answer removes nothing; a failure never
    overwrites a kept answer. (from D148)
13. Every install door writes `.daoris-source.json` into the staged copy before the swap: `{folder}`,
    `{offer, digest}` or `{package, version, sha512, source}`. No record is said, never guessed; an unreadable one is named. (from D103, D119, D120)
14. The window's add adds and never replaces. Only the terminal's `daoris plugin add` replaces an installed plugin,
    wholesale. A source inside or holding the home is refused. (from D103, D194)
15. An update shows its plan first: version, command, points, harnesses, servers, skills, steps and resources. It
    applies only on a press or `--yes`. The installed folder is moved aside whole, the copy renamed in, `.data/<id>`
    untouched, and nothing starts at the press. (from D103)
16. A plugin from a package is never updated in place: it is removed and a newer package installed. (from D120)
17. Offers are `app/plugin-offers/` in the install, laid out each publish from `examples/plugins/`: the GitHub and
    Azure DevOps pull-request plugins and `in-app-browser`. None installs without a press. The driver looks beside the
    application first, then beside the home. (from D103)
17a. The two pull-request plugins perform one step, `pull-request`, on `work/perform` and answer `work/state`, through their
    service's REST API with `DAORIS_HOST_TOKEN`: the API token of the identity serving origin (`PluginHostToken`), set by `HookProcess`
    around the one start `HookSet` makes for it and removed from every other environment. A plugin whose hand is refused (no identity
    serves origin, the identity is read only, Write denies, no API token) is not started. They run no git, `gh` or `az`, and declare
    node alone. A plugin that declares a `pull-request` step has one work process per place (repository and workspace) that awaits
    it, never one shared: `PlacedWork` splits the awaited units by place under a key beneath the plugin's id, so each process's waits,
    unsound mark and idle stop are its place's own, and `PlaceHostTokens` (given to `HookSet` by the loop and the headless host) reads
    that place's origin and hands its token to that process alone. An attempt goes to the place its unit was told for; the runtime
    sees the plugin's id, its generations unique across the places (`GenerationOf(plugin, held)` fences against the process that took
    the attempt). A refused place has no process, and the others are served. (from D241, D242)
18. An offer's installed copy is judged against the shipped one. Same version, other files: `changed`. Older, files
    matching the record's digest: `older`. Older, digest not matching: `changed`. Older, no digest: `older`,
    unjudged. Newer: the person's, said nothing. (from D119)
19. A `changed` copy is never overwritten by an update. A copy with no record, or one that does not read, may take
    the shipped copy (`PLUGIN_UPDATE` with `shipped: true`), which records the offer and keeps `.data/` and the switch. (from D119)
20. `plugins new` writes a self-contained folder (manifest with `tools: []`, `plugin.mjs`, `plugin.test.mjs`, README)
    and refuses rather than overwrites. It installs nothing and scaffolds no harness. Its samples come from `HookFrames`. (from D101, D150)
21. `plugins try` starts the plugin as the driver would, in scratch under `<home>/plugins/.trials/`, one sentence per
    check; exit 0, 1 or 2. It fails a declared point the process does not listen on. (from D101)
22. A sample frame names no repository: an empty scratch root and a `GIT_DIR` that is not there. (from D101)
23. A tool is found and checked only at a trial or on a press, never at a load or on a timer. A check's `fix` (such
    as `az login`) runs only on the person's press, in a console they see. Daoris never signs in or downloads. (from D150)
24. A server goes to sessions by default. One handed to Ask Daoris reaches it only once the person approved its exact
    tools; a write only as an accepted write; a generic execution tool never. A server declared through `npx` names the
    package and its version; a session is handed node and that package's script, installed once into the toolchain, never
    `npx` itself, and one that drives Daoris's browser stays under a 512 MB ceiling by a restart between calls (agents
    area, rule 13). (from D170)
25. What a plugin did is kept without its words: `plugin.*` lines hold names, counts, flags and times. Its words go
    to the console under `plugin:<id>` for this run. (from D119, D94)
26. An icon is a path inside the folder to an SVG or PNG of at most 32 KiB. The page gets its bytes as a data URI,
    drawn in an `<img>`, never a path; else a monogram on a hue hashed from the id. (from D140)
27. The page is a catalogue: installed (refused while on, then on, then off), then Daoris's own not installed. Name,
    description and version stay as declared. No plugin adds a view or runs code in the page. (from D119, D140, D52)
28. Every plugin press is the person's: a session's terminal is refused. With Daoris running a verb takes the route,
    else it acts alone under the home's lease. (from D194)
29. Settings sets nothing about a plugin. Settings → Driver lists the installed plugins read-only, each opening its
    page. (from D119, D183)

## Where a change goes

- A new point or frame field: `HookPoints`, `HookFrames` and the kit's entry in `PluginKit.Points`. A point with no kit
  entry fails `PluginKitTests`.
- A manifest field: `PluginCatalog` and `PluginManifest`, the update's rows in `PluginChange.Between`, the page's `catalog.ts`.
- Install, update, the source record: `PluginInstall`, `PluginSource`, `PluginFolders`; the route in `PluginsRoutes`,
  the terminal in `PluginCommand`.
- Offers: `OFFERED_PLUGINS` in `tools/desktop-publish.mjs` and `PluginOffers`.
- A shipped plugin's behaviour: its folder in `examples/plugins/`, its version and `changes` line, its README's
  `## What it needs`. A working plugin's kit files change in `examples/plugins/local-preview` in the same commit.
- Tools and sign-in: `PluginTools`, `PluginToolChecks`, `PluginToolSetUp`.
- The page: `src/Daoris.Web/src/plugins/`, chrome through both catalogues.
- Steps, resources and the work wire: the workflows area.

**Tests.** `Daoris.Desktop.Driver.Tests/Plugins/` (`PluginCatalogTests`, `PluginKitTests`, `PluginInstallTests`,
`PluginSourceTests`, `PluginShippedTests`, `PluginOfferTests`, `HookTests`, `HookStateTests`, `LandingPluginTests`,
`PluginToolsTests`, `PluginToolSetUpTests`, `PluginIconTests`, `PluginPackageTests`, `HelpGrantsTests`, `WorkEventTellerTests`,
`WorkEventGrammarTests`, `WorkEventPointTests`, `RelayBuildTests`, `WorkEventWiringTests`);
`Workplace/WorkEndsGitTests`; `Daoris.Desktop.Modules.Tests/Platform/PipeConfirmationSettledTests`; the service's
`Quests/QuestJournalTests`;
`Daoris.Desktop.Modules.Tests/Bridge/` (`DriverModulePlugins*Tests`, `PluginsBothWaysTests`, `PluginRecordWiringTests`,
`HelpPluginApprovalDoorTests`); the page's `plugins/*.test.ts(x)` and `settings/plugins/PluginSettings.test.tsx`; each
example's `node --test`.

**Gates.** The driver lane: the desktop suites, `rehearse-family`, `deployment`. The modules lane: `modules`,
`censuses`, `modules-process`, `deployment`. `examples/plugins/**`: `service`, `driver`, `driver-process`,
`rehearse-family`, `web`, `deployment`. The page: `web-unit`, `web-types`, `web-rules`, `web`.

## Contradictions

- D103, D140, D150 and D64's PLUGINRESERVE1 note make the CLI and the driver twin readers; D194 retired the CLI's plugin code, and the driver reads alone (`PluginCatalog.cs:27-31`). D194 reads right; stale twin comments remain (`PluginCatalog.cs:150`, `PluginManifest.cs:23`, `tools/desktop-publish.mjs:219`).
- D120 §3 and §5 move the landing plugins and `in-app-browser` to the plugins repository and pin offers to packages; the publish lays them out from this repository (`tools/desktop-publish.mjs:230,1397`). The code reads right.
- D119 §2 names five states, ready and failing among them; the page draws four (`src/Daoris.Web/src/plugins/catalog.ts:199`), though `PLUGINS` answers health (`PluginsRoutes.cs:85`). The code reads right; the decision is unbuilt.
- D119 §5 and §6 promise `PLUGIN_TEST`, `plugins test` and kept checks in `<home>/plugins/.checks/<id>.json`; none exists (`PluginsCommand.cs:27-33`, `docs/index/routes.md:121-133`). The code reads right.
- D119 §7 refuses a go to Settings → plugins; its PLUGUI1h note keeps it as a moved place that opens Plugins. The note reads right.
- D119 §8 says Settings keeps nothing of a plugin; D183's PLUGSET1 adds a read-only list (`settings/plugins/PluginSettings.tsx`). D183 reads right, since it sets nothing.
- D103 names five update rows; the code compares eight (`Domain/PluginSource.cs:188`). The code reads right; neither shows a change to `tools` or `icon`.
- D101 puts the kit's New in Settings → Plugins; it is the Plugins view's (`PLUGIN_NEW`), and `PluginKit.cs:23` still says Settings. The view reads right.
- D199 §3.4 (D64's COAGENT1 note) lets a plugin's harness declare `fullPosture` and `askingPosture`; the reader takes one `posture` (`PluginCatalog.cs:429`). The code reads right; D199 is unbuilt here.
- Ask Daoris's room says a plugin with no record cannot be updated (`Knowledge/Room/HelpRoomMayPropose.cs:64-65`); D119's LANDPIN3 lets an offer's copy take the shipped one. Both hold for their door; the room offers no shipped take.

## Open

- PLUGHOOK1d: ask a pull request's state again once the first pull request opens.
- FABRIC-PERFORM1l: keep a pull request's fields in the work wire's evidence (`WorkWire.cs:354`).
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
- No row: D199 §3.4's postures for a plugin's harness.
