# Install, home and update — how it works now

## What it does

Daoris ships as an install: a folder with one program to run, `Daoris.exe`, the build in `app/`, and the person's files in
category folders beside it. The install's `data/` is the Daoris home. Every machine-local path is derived from `DAORIS_HOME`
through one table of entries, held by three twins (driver, service, CLI). The driver also decides which file each tool is (git,
Node, PowerShell), and downloads managed versions into the home. `publish:desktop` writes an install, or stages a build
in `update/staged/` beside a running one. The application drains its work, holding new work for a bound at most, closes, and the launcher swaps `app/`, confirms the
new build, or rolls back. A build comes from `publish:desktop --stage`, or from Daoris's own GitHub releases, which `daoris update --fetch`
stages (rule 48a). A release that changes a stored shape converts nothing: the home's history is purged, and nothing
reads an older shape (rule 51). The build's own plugins (`app/plugin-offers/`) that are installed older update
at that start, before any hook runs (plugins area, rule 18a).

Paths below use `Drv/` for `src/Daoris.Desktop/Daoris.Desktop.Driver/` and `Mod/` for `src/Daoris.Desktop/Daoris.Desktop.Modules/`.

## Its parts

Each type is in a file of its name; `docs/index/README.md` says where a seam, verb or fixture is. Which folder owns which job:

| Where | Its job |
|---|---|
| `src/Daoris.Desktop/Daoris.Desktop.Launcher/` | The root `Daoris.exe`: taskbar id, starts `app/Daoris.Desktop.exe`, runs a staged swap while nothing runs from `app/` |
| `Mod/Update/` | The application's half: the install found and the home set (`InstallHome`), the update watched, drained, checked and said (`InstallUpdater`, `UpdateModule`, `UpdateLauncher`) |
| `Drv/Platform/` | The home seam (`DaorisHome`, its `HomeEntry` table), the home a program names once at its entry (`ProgramHome`, never the variable a process inherited; `ProcessHomeCensusTests` lists each read left), the lease and the id (`HomeLease`, `HomeIdentity`); twinned cell for cell by the service's `Platform/DaorisHome.cs` and the CLI's `platform/home.ts` |
| `Drv/Install/` | The staged build and its swap, confirm and roll back (`StagedBuild`, `InstallSwap`, `HeldFiles`, `HomeLastBuild`, compiled into the launcher), the person's word (`InstallUpdate`), `daoris update`, the start's confirmation, the refusals (`UpdateRefusal`, `GoingBackRefusals`), the tools (`Tools`, `ToolInstall`, `ToolResources`, `daoris tool`) |
| `Drv/Workflows/` | Going back refused below the saved workflows' floor and while a delivery is unresolved (`InstallRollback`, `DeliveryGoingBack`), installed into Install's port; the floor's rule is `Workflows/Domain/WorkflowRolesSince`, its file `WorkflowRolesSinceStore` |
| `Drv/Store/` | `data/desktop.db` and its numbered steps (`DesktopStore` over the port's engine, `DesktopStoreSteps`) |
| `Drv/Service/`, `Drv/Knowledge/` | Which HTTP host and connector run: the install's, then the home's `bin` |
| `Drv/Install/` (`DaorisReleases`, `DaorisReleaseDownloads`, `DaorisReleaseSource`, `UpdateFetch`) | Daoris's own releases as Install's source, and the fetch that stages the newest one |
| `tools/desktop-publish.mjs`, `service-publish.mjs`, `deployment-rehearsal.mjs` | Lay out or stage an install; build the two service hosts; publish to scratch and drive the artefact, update phase included |
| `.github/workflows/desktop-release.yml` | Run only by a version tag the owner pushes: zips the staged build and publishes the GitHub release with it and its checksum attached |

## Its rules

**Layout**

1. An install holds `Daoris.exe`, `app/`, `data/`, `update/` and `INSTALLED.md`, and beside them `profiles/`, `work/`, `cache/`,
   `run/` and `logs/`. `Daoris.exe` is the one thing a person runs. (from D93, D172)
2. `INSTALLED.md`, headed `# Daoris — installed desktop`, is the marker. It alone makes a folder an install, for the publish, the
   home and every program. (from D60, D93)
3. `app/` holds everything a build ships: `Daoris.Desktop.exe` beside its Chromium, both service hosts, the driver's program
   (`daoris-driver/`), the doctrine tool (`cli/`: one bundled file, `daoris.mjs`, with `canon/`, a `package.json` of its name and
   version (the build's own, point 36) and a `build.json` stamped with the build's id and commit, which `daoris --version` says beside the version; the driver
   and the set-up run the file by its own path, and the set-up refuses only where the file is not there), the command and Daoris's
   git doors (`bin/`), `resources.json` and `plugin-offers/`.
   An update replaces it whole. The command is `daoris.exe`, a program and never a batch file: it starts the driver's program with
   each argument whole, so words of several lines reach every verb as typed, and Command Prompt, PowerShell and Git Bash all find it.
   (from D93, D99, D103, D121, D124, D172, D291)
4. The launcher starts `app/Daoris.Desktop.exe` with its own arguments and environment, through the shell, standing in the
   install's root, then exits. It drops the role key and the inbox's two messaging variables from its environment, then names Daoris's taskbar id. (from D93, D108, D139)
5. The application finds its install above its own `app/`. It moves its working folder to the install's root at once, so no
   child stands in `app/`. (from D93, D139)
6. Daoris's browser is the application started with `--daoris-browser`; the install carries one Chromium. (from D99)
7. A development run is laid out as an install, less `app/` and `update/`: `_fixtures/desktop/daoris/`, named as both
   `DAORIS_HOME` and `--app-root`. A surface is judged on a published install (`npm run desktop -- run --install <folder>`). A
   published application opens no debug port by itself. (from D62, D172)

**The home**

8. `DAORIS_HOME` is the one seam, and every machine-local path derives from it. With none set, a management act refuses with
   one sentence; doctrine commands need no home. Nothing of Daoris's is written under the user profile. (from D63, D172)
9. An install's home is spelled `<install>/data`. The application sets it for itself and every child. It sets the account's
   variable once, only where the account has none. (from D63, D172)
10. An inherited home loses to the install's own. A process value equal to the account's variable was inherited and is
    replaced; a different value was named for this start and is kept whole. The account's variable is never rewritten, and
    Settings' home row says which case holds (`same`, `overridden`, `this-start`). (from D105)
11. A program the install carries, given no home, takes its install's `data/` as its home. (from D171)
12. The Daoris folder is found from the home: a folder holding the marker; a `data` folder whose parent holds it; else the home
    itself. Folders "beside the home" (`app/bin/`, plugin offers, the tool list) are read in the Daoris folder. (from D172)
13. Every home file is an entry of `HomeEntry`: a name, a category, a place and the names it holds, where it is known by them. The
    three twins hold the same rows in the same order. A reader asks `DaorisHome.Entry` by name and never joins a home path by hand:
    `HomeJoinsCensusTests` fails for a desktop source that joins a literal onto the home (`Path.Combine(home, "memo")`), so a writer's
    place is a row before it is written (the memo folder `data/memo` and a session's start record `run/sessions/*.agent` were not,
    HOMEPROOF2). (from D172, D323)
14. An entry has one place, and is read there. "Holds" (an entry known by its names) is one of its named files being there. (from D172, D323)
15. Asking for an entry's place makes its category's folder. A home laid out in an earlier build is purged, not read. (from D172, D323)
16. `data/` holds no secret: a file with a key is a sign-in and lives in `profiles/`. (from D172, D323)
17. Back up `data/` (and `work/` for work in progress). `profiles/` is as private as the machine. `cache/` is fetched again,
    `run/` may be cleared while Daoris is closed, and logs keep 30 days or fewer. A category is cleaned by Daoris's own acts,
    never by deleting a folder while it runs. A workspace's history clear sends its units to the service in presses of at most `HistoryDoors.PressUnits`, so each answers within the client's bound and one `daoris history clear --workspace <w> --yes` clears a history of any length. (from D172, D195)
17a. Nothing Daoris writes is kept forever unnoticed. The bounds that keep `work/` from growing without end are two. The dependency store
    (`work/deps`, the Disk space card's `deps` kind) evicts a copy not placed in a tree for 30 days, then the least recently placed past 10 GB,
    in the Keeping pass once a day; a placed tree holds its own names of the files, so evicting an entry frees nothing a tree uses. A session tree no run owns, that no session runs
    or waits in and that nothing has used for 14 days (`TreeReleases.Unused`: the later of the tree's top folder's write time and its HEAD's last move in
    git's reflog) is removed by the look's tidy with its branch standing and holding every commit; uncommitted work,
    a live run's tree and an open session's tree keep it. Each such removal and each eviction is said in the machine log (`tree.ended`,
    `deps.evicted`) with the bytes freed. A tree's files record and install logs go with the tree (the tidy's set-aside,
    a removal, the press, a failed open); a session's `.pid` marker whose process and session are gone is removed by the orphan
    sweep (`DeadMarkers`); each store limits its log to 32 MB and the knowledge store truncates it and returns its free pages at
    open (`StoreCompaction`). The tidy's Keeping pass also runs, once a day, the plugins' output prune (a file under `plugins/.data/<id>/output` not written in 14
    days, then an emptied folder, `PluginOutputKeeping`, handed to the tidy as its `IOutputKeeping` port by the loop's watch; no card
    row, the plugin's other data stays). `agent account list` names a
    folder beside an agent's accounts that is no account (a lock's) and a folder under the account homes that no agent names
    (`<agent>$a`, `StrayAgentFolders`), and `agent account remove <agent> <account>` or `remove <folder>` takes each.
18. One writer per home: `run/home.lease`. An instance holds it for its life; a verb and the launcher's swap hold it for
    their act. A stale lease never blocks, and is removed by moving it aside and judging what moved, so a lease written
    meanwhile is put back. No other file holds the home. A workflow verb that writes a run's record holds it (`NodeLease`:
    `keep`, `skip`, `stop`, `changes`, `accept`, `take`, `hand`, `give-back`, and `values` set or cleared); a look holds none.
    (from D104, D171, D323)
19. A home has one id, in `data/home.json`, read alike through every path to the folder. (from D171)
20. A start moves nothing into the home: no `~/.daoris` is moved in, and no entry is moved from one place to another. A
    home whose layout changed is purged, not moved. (from D323)
21. Agents' account homes, session trees, rooms and kept files are read at the place of their entry. (from D172, D323)
22. `bin/` is read at `cache/bin/` under the home. (from D172, D323)
23. A tree is one under the place of `trees`. (from D172, D323)

**Tools and pins**

24. Daoris runs three declared tools: git, Node.js (with npm and npx) and PowerShell. A list can offer versions of these and
    never adds a program. (from D121)
25. Each tool has one way in `data/tools.json`: the system's (the default, from `PATH`), managed (one exact version), or a file
    the person names. A managed version not downloaded, or a file that is gone, refuses; neither falls back to `PATH`. The
    driver is the file's one reader and writer. (from D121, D194)
26. A managed version downloads into a staging folder under `cache/tools/<tool>/`, is held to the list's size and SHA-256,
    unpacked, recorded in `tool.json`, then renamed whole. The record is the proof. Update moves to the newest, never back. A
    version in use is never deleted. (from D121)
27. Versions come from `app/resources.json`, built in and never rewritten, and from the person's locations (https, or loopback
    http), fetched only on a press and kept under `cache/tools/`. Lists that disagree on one download refuse that version. (from D121)
28. Every child the driver and the modules start gets the tools' environment (`Tools.Hand`, over `Tools.ChildPath`): `app/bin/`
    first, so an agent typing `daoris` gets the install's command, then each managed or named tool's folders, and
    `GIT_CONFIG_GLOBAL` when git carries a setting. The application's environment and the account's `PATH`
    are never rewritten. (from D121, D124)
29. Agents stay on their makers' channels, each Install's own `IReleaseSource` (`ClaudeReleaseSource`, `CodexReleaseSource`, and Daoris's own `DaorisReleaseSource`, rule 48a) found
    by the channel a program names in `ReleaseChannels.Sources`, never by a switch on ids. A pinned agent lives in `cache/toolchain/<agent>/<version>/` and runs with its own
    updates switched off. A session's server's package lives beside them, under its name with a scope's slash made `+`
    (`cache/toolchain/@playwright+mcp/0.0.82/`). The binary is the explicit command, else the pin, else `PATH`; a pin with nothing installed refuses.
    (from D57, D121)
30. The devkit is pinned by version (`devkit` in `daoris.gates.json`) and refuses a mismatch before any gate runs. Its binary
    is a release asset with a `.sha256` beside it. Whoever installs it verifies the digest; nothing downloads it implicitly.
    (from D27)

**The service as executables**

31. Both service hosts publish self-contained, single-file and untrimmed, with native libraries inside the file. (from D43)
32. The HTTP host and the knowledge connector an install carries under `app/` run first. Then the home's `bin` entry
    (`cache/bin/`, or an older home's `bin/`), where `publish:service --install` lands them. Then the workspace build. (from D43, D60, D63, D93)
    The connector is no process of the application's: each session's harness starts its own, located at every start and resume, and
    the shutdown before a swap ends each session's process tree, so the first turn after a swap reaches the new build's connector.
    That turn's model sees the tools the new build serves because Claude Code's doors turn its record of them off (D326).

**Publishing**

33. `publish:desktop -- --to <folder>` has no default folder. It writes only its own names (`Daoris.exe`, `app`, `data`,
    `INSTALLED.md`, `update`). It refuses a non-empty folder with no marker; `--beside` writes next to other things, never over
    one of its names. (from D60, D93)
34. A republish removes exactly what `app/shell-files.txt` lists (a line naming a path, not one name, is no entry) and the retired names, then places its own. A publish into an
    install carrying a service host, without `--service`, is refused. (from D93)
35. It publishes only a commit whose merge passed the fast tier and whose heavy run (`local/heavy-runs.jsonl`) is green, or red only
    on classes already filed as main's red, unless the folder is under `_fixtures/` or `--force-ungated` is given, which it says;
    a refusal names what the commit lacks (no landing run, a run still going, an error, an unfiled red) and the command that supplies it. `--stage --from-gated <commit>` builds the gate tree's commit under its lock. From a session's terminal
    (`DAORIS_SESSION_ID`, Ask Daoris's among them) it neither stages nor publishes outside `_fixtures/`, and `daoris update`'s
    verbs are refused there too: a stage drains the install, so it is the person's. Before the web bundle it regenerates the
    page's generated contracts where the checkout's are missing or stale (`tools/desktop-publish/contracts.mjs`, the merge's
    `contracts.mjs --if-stale`), never trusting the page's `prebuild` alone, and says whether they were fresh or it regenerated
    them; a generator that fails stops the publish before the bundle, so nothing is published. (from D115, D180)
36. `--stage` builds the same build into `update/staged/`, with `build.json` (schema 1: id, version, commit, time, every file's
    size and SHA-256). It refuses a non-install, a swap under way, and a missing `--service` where the install carries its HTTP
    host. A running install is never refused. A publish in place removes anything staged. Every publish takes its own version
    (`tools/desktop-publish/version.mjs`): the patch above the highest of the release's (`canon/canon.json`'s) and every version
    the install has carried, staged, tried or run (its doctrine tool's `package.json`, the stage's `build.json`, `update/swap.json`,
    `data/last-build.json`), so two publishes in a row say two versions, and one never falls below the newest that ran, which the
    launcher would refuse. It is passed to every publish recipe as `-p:Version=`, so every assembly (the window's System page and the
    machine log's `app.started`), the doctrine tool's package (`daoris --version`) and the stage's `build.json` name it; the canon
    beside the tool keeps the release's version, which every synced file's header names, and `daoris update` says the version and commit the
    target install's own `app/Daoris.Desktop.App.dll` carries (never the driver that asks) before what is staged. A development build carries the release's version (the desktop's
    `Directory.Build.props`). The running
    build, the launcher's read of `app/` and a stage's manifest read one number. The commit follows it (`0.0.2+<commit>`) only where
    a publish builds: every publish recipe passes `-p:DaorisStamp=true`, which the root `Directory.Build.props`, imported by each
    tree's, reads. A development build reads no git, writes no source link and carries `+dev`, so the System page's build fact
    reads `dev`, and the instance hello and the store's recorded build (`store_version.build`) name one build for every development
    commit; neither decides on it, and a switch between commits recompiles nothing for the stamp (measured in TREEPOOL1's R5.8).
    (from D139, D233)

**Updating**

37. The person's word on a staged build is `data/update.json`: when idle (the default, also when absent or unreadable), now,
    or not now. *Not now* holds for that build only. The banner and `daoris update --when-idle|--now|--cancel` both write it,
    and `--stage --not-now` writes *not now* for the build it stages once nothing is staged and before the build is, one act, so
    no idle install races a cancel. (from D139, D50, D301)
38. While it drains, the loop starts nothing new but still looks: endings are reported, plugins told, sync runs. A press that
    starts a process or changes a repository through git waits for the restart; one that starts no process goes on: a stop, a
    take-back, an answer, a word to a session, a workflow step's act, the drained look holding any quest it releases. Once applying,
    every press waits. A drain holds new work for the machine's `drain` setting at most (`drainMinutes` in `driver.json`, 10
    minutes by default, 1 to 120, at Settings → Agents and sessions, by `daoris settings drain <minutes>` or `SET_DRAIN`), which
    `InstallUpdater` reads at each look (`Drv/Settings/DrainSetting`). Past it the drain defers (`deferred`, said once as `update.deferred`): the
    loop's starts and the presses go on, the mode stays *when idle*, and the build installs at the first look that reads the work
    idle, or at the person's word; a newer build drains anew. The banner says until when a drain holds new work, and of a deferred
    build that it waits, on what and since when, with *Update now* and *Not now*. (from D139, D327)
39. Idle is no driven session with a turn running, no conversation's turn in flight and no run's plugin step in flight: an attempt a plugin
    performs that is recorded, sent or unknown on a run that goes on, which the runtime governs at a node its graph declares while
    its plugin's process is up, and whose step waits on no role's act (`PluginStepsInFlight`), so a release or a deploy is never
    cut off and left not known. The loop hands that count in its work beside the sessions and the turns; the updater reads no run
    record. A driven session of this loop that waits on its own background work, with no turn its harness woke running and no prompt
    open (`AcpSession.BetweenTurns`, counted out by `DriverWatch.RunsInTurn`), runs no turn and is idle too: a busy Daoris, with
    sessions always at some stage of a next turn, would otherwise never install. The close that follows is *Update now*'s: such a
    session is recorded stopped for the update and carried on at the next start (rule 41), and the background work it waited on, a
    check or a landing it ran itself, stops with its process; the carry-on words say so (`ShellClose.Words`). A turn its harness
    wakes between the last look and the close is cut too. A conversation between turns, a parked session, the terminal, a held or settled attempt, a step whose progress names
    the role it waits on (an approver, a deployer, whoever holds it), a watch whose plugin declares it acts on nothing
    (`perform.acts: false`), Daoris's own steps, an ended run's, and an attempt the runtime does not ask after (a run it does not
    govern, a node no graph declares, a plugin down) are idle, and an attempt not known is idle 15 minutes after the loop first
    counts it so (`PluginStepsInFlight.UnknownBound`); the banner names the steps it waits on. (from D139, D301, D363)
40. Before closing for a build, the application checks it: every listed file with its size and hash, nothing unlisted, the
    launcher, the application and its library, and the host where the install carries one. It refuses going back to an older
    version while a delivery is unresolved, and below the first build that saved a workflow whose step names a role other than its
    kind's own (`workflows`, `WorkflowRolesSince`); a role equal to a step kind's own is never written, so it closes nothing. `UpdateRefusal` is that one check, which `daoris update --now` asks too: refused, it says
    the sentence, names the deliveries, writes the word and exits 1. A refusal closes nothing, is said again at every look, and holds
    for its build until the person's next word, from the banner, Settings or the terminal, which checks again. A bare `daoris update`
    of a build asked for now asks the same refusal and says it with its words, never "installed now". The presses a close holds go on
    the moment the update is refused, a newer word (a cancel) is said, or the close has not taken within `InstallUpdater.CloseBound`
    (five minutes, past the launcher's three: refused as `close`, kept until the next word); a word said while the build is being
    checked withdraws the close before it is made. (from D139, D215, HOMEPROOF2)
41. *Update now* is the person closing Daoris. It does not wait; driven sessions are recorded stopped and carried on at the
    next start. (from D139)
42. The application starts `Daoris.exe --update --after <pid>` followed by its own start arguments, keeping any debug port
    through the swap and restart, and closes. The launcher waits up to three minutes for it and for everything running
    from `app/`. (from D139)
43. The launcher swaps only while nothing runs from `app/` and under the home's lease; held, it replaces nothing and says
    who held it. It checks the build again, moves `app/`, `Daoris.exe` and `INSTALLED.md` into `update/previous/`, then the
    staged three into place, journalling each move in `update/swap.json` before making it. (from D139, D171)
44. A held move is retried for up to two minutes, and the hold is journalled with what held it. A build refused `busy` (something
    still ran from `app/`, or the home was held) is set aside in `update/failed/`, and the launcher stages it again by itself at
    the next start that finds nothing running from `app/` (`StagedRestage`). `tools/desktop.mjs run` and `restart` with
    `--install` wait, up to six minutes and said, while the install's journal reads `swapping`, `started` or `confirmed` or a
    launcher waits with `--update`, and refuse to start past that. (from D139)
45. The new application must confirm within 60 seconds of starting. One that exits or stays silent goes to `update/failed/`,
    and `previous/` comes back. A failed move undoes the journal in reverse; a launcher that died mid-swap is undone at the next
    start. Confirmed, `previous/` is removed. A build that failed is not tried again. (from D139)
46. A start that finds a build staged and nothing running from `app/` swaps first, unless the person said *Not now* of that
    build. Whatever happens, the launcher's last act is starting Daoris, unless that build is older than its home's last: a
    start nothing can roll back keeps its version in `data/last-build.json`, never lowered, and the launcher refuses a build
    below it in one sentence naming both, a swap onto one rolling back. It is the launcher's one check of an older build.
    (from D139, D232, D233)
47. The launcher writes no log. The next start says the outcome once (`update.installed`, `update.rolled-back`,
    `update.refused`), and every state carries `last` from the journal. Log lines name things and count them; a path or a command line
    is never a field of its own. The application's `update.refused` carries its sentence on one line of at most 800 characters
    (`UpdateRefusalWords`): the install's and the home's paths are said as `<install>` and `<home>` where a separator or the path's end
    follows them, in either slash and in the long form, an email as `<email>`, and the rest as the build said it, which may name another
    path; so a refusal reads again from the log alone. A home proof that passes logs `update.probe.copied`, what its copy read and did not,
    counted. (from D94, D139, HOMEPROBECOPY1)
47a. A staged build starts clean on a copy of the home before it is swapped in (`HomeProbe`, the last of `UpdateRefusal.Of`'s checks, asked at the
    close by the application's `InstallUpdater.Proof` and by `daoris update --now`, so both answer one sentence). The running build copies the home
    into `update/probe/<id>/` (`HomeCopy`), and the copy is never vacuous: the Daoris folder, its `data` folder and every folder and record the
    proof's reading reads (`HomeProof.Reads`: the agents' account folders under `profiles`, each session's record, the desktop store, each
    workflow's file and each run's binding) are listed and read, or the copy is refused with a sentence naming the place. `data` is copied with
    its files at any depth, a file over 64 MB as an empty one unless the reading reads it, but for its content folders, left out by name and
    kept empty (a line's work trees `workflows/lines/trees`, each plugin's package and `plugins/.trials`), which hold most of a home's files and
    are what a running check changes; every other folder of the Daoris folder is copied as names, empty, as deep as the table's deepest
    place, within a budget, with no link followed but where the reading follows it; the source is read and never written. The home is in
    use while it is copied: a seat or lease a running service holds or takes (a `*.lock` file, the check broker's `checks/broker.lock` among
    them, and `home.lease`) is named as an empty file and never opened, since a copy holding a free seat open would fail the service's take of
    it; a file another holder lets no one read (a sharing or lock violation, nothing else) is an empty stand-in, unless the reading reads it,
    which refuses the copy, and a stand-in the copy cannot write is skipped; a file or folder gone by the time it is read or listed is left
    out; a folder nothing reads that cannot be listed is kept empty; any other fault refuses the copy. A read-only or hard-linked file is
    copied as its content with no mark. What the copy did not read (held stand-ins, what it left out, unlisted folders, content folders,
    seats) is counted: a pass logs it, a refusal's sentence says it. The staged build's own driver reads the copy (`daoris home proof`, one
    line of JSON; routed before the lease and the machine log, so it writes nothing), and then the running build reads the same copy
    (`HomeProof.Read`, over Workflows' `HomeProofReading` installed into Install's port: the agent accounts it lists, the sessions' records and runs' bindings it meets, each workflow's file and the runs that bind a
    workflow the home does not hold, which are its `problems`, and the paths no entry names, its `strays`); the copy that could not be made,
    the staged build's report that does not read and this build's reading that fails each have their own sentence. The swap is refused with
    `home` unless every account the running build lists of the copy is still listed, the staged build's report holds no problem the running
    build does not report of the copy too (`HomeProbe.Novel`: both read one copy, so a stand-in or a name-only folder is read alike and what
    differs is the builds), and the accounts the home lists now, a listing of folders (`HomeProof.Accounts`), are the ones the reading of the
    copy lists, a difference said as the copy's fault, not the build's; a build that gives no readable report (no driver, no such verb, past two minutes, a line without the report's fields) is refused as one that found the home amiss,
    a build older than the verb too, whichever way the version runs: waving through the unread build would let past the one the proof exists for. A path no entry
    names is said, never refused: the report lists it apart (`strays`), the start's log and `daoris home` list it, and the accounts a
    stray place hides are still caught by the account comparison (HOMEPROOF2, the row HOMEORPHAN1 first read it as a problem); the report's `accounts` and
    `problems` are the fields the running build's own update reads of a staged one, kept so an earlier build's proof still reads a newer
    build's; a run on Current and a run the bind held
    (its workflow not saved or unread) are records the product writes and are none. The proof runs off the caller's thread, since *Update now*'s SET would otherwise freeze the
    window for the copy and the staged build's reading.
    A refusal closes nothing, this build keeps running and the sentence says why; it is kept as rule 40's is. The copy is removed when the proof ends,
    and one whose proof no longer runs (a mark file beside it, held open by the proof) by the next, by the remover an account's home goes by
    (`FolderRemoval`: read-only marks cleared, links removed and never followed). The probe that judges an update is the running build's:
    0.0.39's copies the home's own folder whole, about 8 GB with the line trees, fails on the first file it cannot read or that vanishes, and
    logs no sentence, so an install running it is updated only while no check waits or runs and the line trees are idle, from the CLI
    (`daoris update --now`) with a long wait (`HomeProbeAt0039`, a test copy of it, proves a newer build passes it then, and is deleted once
    the install runs a build with this probe). (from UPGRADEPROOF1, HOMEPROBECOPY1, FIX-LOG 2026-11-20)
48. A build that confirms and fails later is not rolled back. A launcher refuses a `build.json` schema it does not read. (from D139)
48a. Daoris publishes its own releases: GitHub releases of its public repository, each carrying `Daoris-<version>-win-x64.zip`, one staged build
    (the launcher, `app/`, `INSTALLED.md`, `build.json`), and `Daoris-<version>-win-x64.zip.sha256` beside it. `desktop-release.yml` makes them and runs only
    on a `vX.Y.Z` tag the owner pushes (no dispatch, no schedule); it publishes the release only once both files are attached. `daoris update --fetch`
    (`UpdateFetch`, over `DaorisReleaseSource` in `ReleaseChannels.Sources`) reads the list of releases for the newest published one that carries both files
    (a draft, a prerelease and an incomplete release are passed over), refuses one that is not one exact `X.Y.Z` or is no newer than the build in the install's
    own `app/` (not the program asking), and fetches the package: the checksum file must list it, the asset's own digest must agree where the API states
    one, the download is held to the hash and unpacked into a staging beside `update/staged/` (`ToolArchives`' zip, which refuses a link, an escaping name and an
    encrypted entry), and `StagedBuild.Check` of a folder and the manifest's version, which must be the release's, hold the build there. Only a build that passed is
    moved into `update/staged/`, so the application never meets one that fails; a build staged meanwhile is kept, and nothing this fetch did not publish is removed. It
    stages and installs nothing: the application installs by the one path of rules 37 to 46. It refuses while a swap is under way or a build is staged,
    from a session's terminal (rule 35's), and on a machine that is not `win-x64`. (from D329)

**The store**

49. `data/desktop.db` is the desktop's SQLite store, opened by the driver library alone and never synced: one connection per
    process, write-ahead logging, `synchronous = FULL`, every write one immediate transaction. The engine that does this is
    `Daoris.Adapters.DesktopStore.Sqlite`'s `SqliteDesktopStoreEngine`, behind `IDesktopStoreEngine` (an `IDesktopUnitOfWork`) in
    `Daoris.Ports`, which the root hands the library in the ambient `PortSet`; the library's `DesktopStore` is the policy over it
    (the steps, the stamp-raising wait, the run, the imports), its record stores write against `DbConnection`, `DbTransaction`
    and `DbDataReader`, and its legacy-file steps connect through the same engines (`StoreEngineSource`). A unit test's root
    hands `MemoryUnitOfWork` instead. (from D212, D286)
50. Store changes are numbered steps, forward only, one exclusive transaction each. The writer stamp (3) is never lowered. A
    build whose stamp is below the store's reads it and writes nothing. A stamp-raising step, and every import, waits until
    the start is settled. (from D139, D212, D226)
51. No code reads a record, key, file or layout in a shape an older build wrote, and none converts one: a change of shape edits
    the store's one current shape, and a home whose shape moved is purged. A store an older build opens after a purge is not
    supported. No migration runs at a start. (from D323, `.claude/knowledge/compatible-builds.md`)

**Purging a home's history**

52. `daoris home purge --history --plan` lists every file, folder and store table the home holds as history, with sizes and row
    counts, and changes nothing; `--yes` does it. Everything it moves goes to `purged/<UTC stamp>/` in the Daoris folder (an
    install's root, a bare home's own folder, never in a place it purges), each path at its own place under the Daoris folder, with
    a `purge.json` listing it. A session's connector and a person's own agent write `knowledge.db` directly, so both stores take
    their write lock first (`BEGIN IMMEDIATE`) and are copied while held (`VACUUM INTO`, through a reader), so no row committed between
    the copy and the delete is lost; a holder that will not let go stops the purge before anything changes, saying which store; both
    stores commit together once both are emptied, and the files move after, so a store that refuses leaves every file in place. A
    file move that fails stops the purge there and says so; what moved is in the backup and the next purge moves the rest into a
    folder of its own. A knowledge store `DAORIS_KNOWLEDGE_DB` moved is the one it reads. Nothing else is touched: configuration is
    never listed. A home with no history says nothing to purge and changes nothing, so a second `--yes` does nothing. (from D322)
53. `--yes` refuses while Daoris runs on the home: a desktop, a `daoris drive` or another verb holding the home's lease, the
    service its address file (`run/running.json`) names still living, or a session whose process marker is alive. It names the
    holder and says to close Daoris, and moves nothing. It takes the lease for its act, so nothing starts on the home meanwhile. From a
    session's terminal (`DAORIS_SESSION_ID`) only `--plan` answers: a purge ends every session's record, so it is the person's. A
    service a person starts by hand that left no address file is not seen; the store's write lock is what then stops it.
    `daoris home account-ids --yes`, the one-time move of agent account ids to the hash form, refuses alike, from one unit
    (`HomeAtRest`). (from D322, D347)
54. What is history and what is configuration is `HomeHistory`'s lists, one side or the other for every `HomeEntry` and every table
    of the desktop store and the service store (`HomePurgeTests` and `HomePurgeProcessTests` fail a new one named on neither), and the
    place a new one is classified when it is added. Where it was unclear it is kept, and the table says so. (from D322)
55. The home names every path in it (`memo` and `session-starts` included: what Daoris writes of its own accord is a row). `HomeStrays` walks the Daoris folder down the folders an entry's place passes through, bounded and
    never into a junction, and names each path no `HomeEntry` matches (an entry's pattern covers its children; the install's `app/`,
    marker and `purged/` are the install's), with its bytes. A start says each in the machine log (`home.stray`), a bare `daoris home` (the
    group is `Bare`, so the tree routes it to the command, not the usage; `HomeStraysTests`) lists them under *Not in the table* after each
    entry's total size and changes nothing (the host routes it before the staffing's wiring, which would make a missing `data/`, and
    before the machine log opens, whose open prunes old logs, and `VerbLeases` classes it a read: no lease, no `home.json` written;
    `HomeBareProcessTests`), and the one walk (`HomeStrays.Read`, `Walked`) yields every path with its bytes and the
    entry it falls under, none for a stray, for any reader of sizes per entry; Disk space shows them (`strays` of `SYSTEM_SPACE`, kept five minutes). A reader of an older place is
    not kept for the files an install holds there (51): the table's place that goes is moved in `home-places.json`
    (`test/fixtures`) from `places` to `dropped` with its declared one-time move, and `home-census.test.ts` refuses a place gone with no
    move, and a place in the table not recorded. The move is run by the steward on the install, never by a build at a start.
    The same walk's places are weighed (`HomeWeights.Read`): each over its entry's size in `HomeBudgets` (a named one, else its category's;
    the census holds every entry to a bound), the largest files and the files of 1 MB or more that identical bytes hold twice (hashed only
    where sizes match; the names of one file record, as a tree's linked dependencies are, count once) are said at a start (`home.over`,
    `home.duplicate`, `home.largest`) and in Disk space (`weight`, counted at a start and again in the background, kept an hour, null until the first
    count ends). (from HOMEORPHAN1, HOMESIZE1, FIX-LOG 2026-11-20)

| Kind | History (moved or emptied) | Configuration (stays) |
|---|---|---|
| Entries, whole | `sessions` (events, frames, logs, lineage and `session-files`), `helpers`, `partners`, `checks`, `handed`, `rule-proposals`, `brainstorms` and `brainstorm-rooms`, `opinion-gates`, `opinion-dues`, `opinion-packets`, `intake` rooms, `quest-files`, `ask-files`, `lines-moved`, `overlaps-told`, `abandoned`, `keeping`, `notices`, `reads`, `files-found`, `pipe-waiting` and `pipe-sent`, `steward-desk`, `priority` (the classes set on asks and quests) | everything else in the table: `driver`, `harnesses`, `accounts`, `cooling`, `windows`, `permissions`, `tools`, `plugin-state`, `plugins`, `plugin-approvals`, `registry-followed`, `configured-access`, `browser-settings`, `browser-favorites`, `home-id`, `check-budgets`, `git-identities`, `agent-offers`, `memory`, `keys`, `remotes`, `agent-homes`, the browser, git and window profiles, `plugin-private`, `deps-secret`, `last-build`, `update`, the caches and the `run/` state |
| Entries kept where unsure | | `files`, `files-asks`, `usage`, `usage-history`, `setup`, `terminal-acts`, `ux-trail`, `act-files` (the act journal's bytes, kept with its table), `trees` and `tree-files` (a tree holds work not landed), `help-room(s)`, `role-rooms`, `proof`, `usage-read`, `places`, `plans`, `evidence`, `logs` (the machine log is open while the command runs); kept for what a restart settles: `place-receipts` (a half-done apply), `landings` and `session-branches` (the tidy never judges a branch with no record, and the trees stay) |
| `data/workflows/` | the folders `agent-steps`, `candidates`, `closures`, `lines`, `looks`, `rebases`, `resolves`, `resources`, `run-ends-owed`, `runs`, `tenures`, `units` and `quests.json` (run, unit, tenure and agent-step records) | each workflow's definition, `casts.json`, risk marks and the roles' floor file, which sit beside them |
| `desktop.db` | the move, integration, delivery, binding, continuation, role-move and role-start tables, `held_decisions`, `notices`, `news_said`, `journal_cursors`, `conversation_facts`, `session_event_offsets`, `session_event_heads` (named in `HomeHistory.DesktopTables`) | its own bookkeeping (`store_version`, `store_revisions`, `store_runs`, `imports`: an import's mark stays so a purged file is never imported again), `roles` and their revisions, staffing, `role_policy`, `supervisor_rules`, `repository_workspaces`, `kept_trees`, `tree_claims`, `tree_visits`, `line_ends`, `tree_hand_offs`, `git_acts`, `git_act_steps` (`HomeHistory.DesktopKept`) |
| `knowledge.db` | sessions and their opens, opinions, the channel and its marks, asks, quests and their log, closes and forwards (each quest id written into `quest_forgotten` first, so a remote's later operations on it make no half quest), work records, memory reads, the memory operations that finished (one still staged, awaiting, ready, applying or held stays), team plans, parts and decisions, and the event journal last (`HomeHistory.ServiceTables`) | the knowledge index (`index_*` tables), the registry and its provenance, the keys, `quest_forgotten` and `quest_retired` (a remote a person retired stays retired), the store's identity and every family's revision mark and cursor, so a revision never moves back (`HomeHistory.ServiceKept`) |
## Where a change goes

- **Install layout or publish:** `tools/desktop-publish.mjs`, with its twins in the same change: `StagedBuild`'s names,
  the launcher's `AppFolder`/`ShellExe`, `DaorisHome.InstallBinLayout`. `desktop-publish.test.ts` reads them all.
- **A new home file:** a `HomeEntry` row in all three twins, same order and cells, asked by name. Pick its category by
  rule 17.
- **Start-time home work:** `Mod/Update/InstallHome.cs`.
- **What the swap does:** `Drv/Install/StagedBuild.cs`, `InstallSwap.cs`, on the base library alone, since the launcher compiles
  them. **When to update:** `Mod/Update/InstallUpdater.cs`, over the loop's work (`Mod/Loop/DriverLoop.cs`, `Work`), its plugin
  steps in flight counted by `Drv/Workflows/Nodes/PluginStepsInFlight.cs` through `NodeRuntime`. **The request:**
  `Drv/Install/InstallUpdate.cs`.
- **A refusal to go back:** `Drv/Workflows/InstallRollback.cs`. **The proof on a copy of the home:** `Drv/Install/HomeProbe.cs` (the
  comparison), `HomeCopy.cs` (the copy and its removal), `UpdateRefusalWords.cs` (a refusal's logged sentence), `HomeProof.cs` (the report and its words) and `Workflows/HomeProofReading.cs` (what a build reads of a home: a new thing a start must still read goes here), the verb `home proof` in
  `HomePurgeCommand`.
- **A changed stored shape:** the shape, its writer, its reader and its tests in one change, with no reader of the old shape
  left (rule 51).
- **A store table or column:** a new numbered step in `DesktopStoreSteps.All` (`Store/DesktopStoreSteps.cs`). Raise the writer stamp only when older builds must
  stop writing.
- **A tool:** `Tools.Declared` and `Drv/resources.json`.
- **Daoris's own releases:** `Drv/Install/DaorisReleases.cs` (what the channel says), `DaorisReleaseDownloads.cs` (the fetch), `UpdateFetch.cs` (the staging) and the
  `desktop-release.yml`; the package's name is spelled in both `DaorisReleases.Package` and the workflow.

**Tests that pin the area.** Driver: the `Install/`, `Platform/` and `Store/` tests beside each unit (`HomeProofTests`, `HomeProbeTests` and `HomeProofProcessTests` for rule 47a; `DaorisReleasesTests` and `UpdateFetchTests` for rule 48a),
`ServiceHostLocatorTests`, `PinnedUpdatesTests`, `PluginStepsInFlightTests`. Modules: the `Update/` tests, `TaskbarIdentityTests`,
`EveryModuleChildIsHandedTheToolsTests`. Service: `Platform/DaorisHomeTests`. CLI: `test/platform/home.test.ts`,
`home-census.test.ts`, `tool-test/desktop-publish.test.ts` and `desktop-publish-not-now.test.ts`, `test/deployment-rehearsal.test.ts`.

**Gates that bite.** `driver`, `modules`, `driver-process`, `modules-process` and `censuses` for the C#; `service` for its twin;
`cli` (`npm run verify`: the home census, the size ratchet); `tool-tests` for the publish; and
`deployment` (`npm run rehearse:deploy`, Windows only, run at the merge), whose phase 9 stages beside a running install,
drains, swaps, rolls back and refuses.

## Adding to it

Each row names the place and the registration; the rule behind it is in *Where a change goes* above and rule 17.

| Kind | Place | Registered by | Fails until |
|---|---|---|---|
| a home file | a `HomeEntry` row in all three twins, in one order | its category by rule 17; asked by name | `home-census.test.ts`, `DaorisHomeTests` |
| a store table or column | a new numbered step in `DesktopStoreSteps.All` (`Store/DesktopStoreSteps.cs`) | the writer stamp raised only when older builds must stop writing | `DesktopStoreTests` |
| a tool | `Tools.Declared` and `Drv/resources.json` | the install layout's twins when it is published (`tools/desktop-publish.mjs`) | `desktop-publish.test.ts` |

## Contradictions

1. D27 (the devkit verifies its own hash) vs `VersionPin.Require` (`src/Daoris.Devkit/Daoris.Devkit.Core/VersionPin.cs`) and the devkit
   README's Distribution section (a version pin only; the installer verifies the digest). The code reads right.
2. D43 (`--install` lands the hosts in `~/.daoris/bin`) vs `tools/service-publish.mjs`'s `--install` (the home's `bin` entry). The
   code reads right; D43 itself carries no amendment.
3. D172 point 2 (`DAORIS_HOME` names the Daoris folder) vs `DaorisHome.InstallOf`/`OfInstall` (`Drv/Platform/DaorisHome.cs`) and
   `Mod/Update/InstallHome.cs`'s home setting (an install's home is `<install>/data`). The code and D172's HOMELAYOUT3 note read right.
4. D172 point 6 (the move runs "at a start after a confirmed update") vs rule 20 (a start moves nothing, D323). The rule reads right.
5. D139 §3 (`daoris-driver update`) vs `Drv/Install/UpdateCommand.cs` (the verb is `daoris update`). The tree reads right.
6. D121 §2 (`tools.json` twinned in the CLI; versions in `<home>/tools/`) vs `Drv/Install/Tools.cs` (`Tools`' remarks and `Declared`: the driver
   alone; the CLI's `tools.ts` is gone) and the `ToolVersions` entry (`cache/tools`). The code reads right.
7. D57 (managed agents under `~/.daoris/toolchain/`) vs the `Toolchain` entry (`cache/toolchain`). The code reads right.

## Open

- **The window redesign** (`docs/2026-11-19-window-design.md` §14): an update is never the owner's question, the window says its
  state in one place and the banner goes (D344, UISH11, UIST5, UIST12); the first release's go and the running app's own look
  for a release are UIST14's, after RELEASEFEED1 (D329). Today the rules below stand.
- The trees' links repaired after a person moves the whole Daoris folder (the install layout design §7.4): designed, not built.
- The running application looking for a newer Daoris release by itself (RELEASEFEED1's left): today a person runs `daoris update --fetch`.
