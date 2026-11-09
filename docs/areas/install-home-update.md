# Install, home and update — how it works now

## What it does

Daoris ships as an install: a folder with one program to run, `Daoris.exe`, the build in `app/`, and the person's files in
category folders beside it. The install's `data/` is the Daoris home. Every machine-local path is derived from `DAORIS_HOME`
through one table of entries, held by three twins (driver, service, CLI). The driver also decides which file each tool is (git,
Node, PowerShell), and downloads managed versions into the home. `publish:desktop` writes an install, or stages a build
in `update/staged/` beside a running one. The application drains its work, closes, and the launcher swaps `app/`, confirms the
new build, or rolls back. A release that changes a stored shape carries one migration, which converts the home once at its
first start; nothing reads an older shape after it.

Paths below use `Drv/` for `src/Daoris.Desktop/Daoris.Desktop.Driver/` and `Mod/` for `src/Daoris.Desktop/Daoris.Desktop.Modules/`.

## Its parts

| Unit | File | Its job |
|---|---|---|
| Launcher | `src/Daoris.Desktop/Daoris.Desktop.Launcher/Program.cs` | The root `Daoris.exe`: taskbar id, starts `app/Daoris.Desktop.exe`, runs a staged swap while nothing runs from `app/` |
| `InstallHome` | `Mod/Update/InstallHome.cs` | Finds the install above `app/`; sets the home, the account variable once, D105's override, D63's move-in; the working folder; the window's memory |
| `FolderMove` | `Mod/Update/FolderMove.cs` | A folder moved across volumes: renamed on one volume, else copied whole and the source removed; a failed copy removes what it made, so the target is as it was; a copy that landed is a move even where a held file keeps the source (`Move` returns whether it went) |
| `HomePlacing` | `Mod/Update/HomePlacing.cs` | The one-time rename of an install's entries into D172's places, journalled and recovered |
| `DaorisHome` | `Drv/Platform/DaorisHome.cs` | The home seam: `Resolve`, `InstallOf`, `Adopt`, `Entry`, `Places`, `FolderOf`, `InstallBin`, `HomeOf` |
| `ProgramHome` | `Drv/Platform/ProgramHome.cs` | The home a program names once at its entry (the window's and the host's): what carries no home of its own, Daoris's own git and a probe's tools, reads it, never the variable a process inherited; a unit that acts for a home (a fetch's holds, a squash's author) is handed it. `ProcessHomeCensusTests` names each library read of the variable left, with why |
| `HomeEntry` and its `Table` | `Drv/Platform/DaorisHome.cs`, beside `DaorisHome` | Every home file: name, category, new place, old place |
| Home twins | `src/Daoris.Service/Daoris.Service.Core/Platform/DaorisHome.cs`, `src/Daoris.Cli/src/platform/home.ts` | The same table and rules, cell for cell |
| `HomeLease`, `HomeIdentity` | `Drv/Platform/HomeLease.cs`, `HomeIdentity.cs` | One writer per home (`run/home.lease`); the home's id (`data/home.json`) |
| `StagedBuild` | `Drv/Install/StagedBuild.cs` | `update/` layout, manifest read and check, the swap journal, the request reader, what runs from a folder |
| `InstallSwap`, `HeldFiles` | `Drv/Install/InstallSwap.cs`, `HeldFiles.cs` | The swap, confirm, roll back, recover; who holds a file. Compiled into the launcher |
| `InstallUpdate` | `Drv/Install/InstallUpdate.cs` | `data/update.json` written and cleared; the drained config; the drain's hold words |
| `InstallUpdater` | `Mod/Update/InstallUpdater.cs` | The application's half: watch, drain, check, start the launcher, close, say the outcome; each build's refusal kept with the word it met (`UpdateRefusals.cs`) |
| `UpdateModule`, `UpdateLauncher` | `Mod/Update/UpdateModule.cs`, `UpdateLauncher.cs` | The page's `STATE`/`SET`; the launcher's start through the start gate |
| `UpdateCommand` | `Drv/Install/UpdateCommand.cs` | The terminal's door, `daoris update` |
| `StartConfirmation` | `Drv/Install/StartConfirmation.cs` | Whether this start can still be rolled back, read from the swap journal |
| `HomeLastBuild` | `Drv/Install/HomeLastBuild.cs` | `data/last-build.json`: the newest build that ran on the home; the launcher's refusal of an older one. Compiled into the launcher |
| `InstallRollback`, `DeliveryGoingBack` | `Drv/Workplace/InstallRollback.cs`, `Drv/Workplace/DeliveryGoingBack.cs` | Refuses going back to an older build while a delivery is unresolved; installs that refusal into Install's port |
| `UpdateRefusal`, `GoingBackRefusals` | `Drv/Install/UpdateRefusal.cs`, `Drv/Install/GoingBackRefusals.cs` | Why a staged build is not installed now, asked by the application and `daoris update --now` alike, and the version this build runs; the going back asked through the port Workplace fills, the deliveries being above Install |
| `DesktopStore` | `Drv/Store/DesktopStore.cs` | `data/desktop.db`: the connection, the writer stamp, settled-gated imports; its numbered steps are `DesktopStoreSteps.cs` beside it (`DesktopStore.Steps` is the door), its open options `DesktopStoreOptions.cs` |
| `HomeMigration` | `Drv/Transitional/Migration/HomeMigration.cs`, its steps beside it | The release's one migration: its steps, each file copied into `data/salvage/` first, each step recorded |
| `Tools` | `Drv/Install/Tools.cs` (records in `ToolWay.cs`, `ToolsRead.cs`) | `tools.json`: the declared tools, each one's way, the resolution, the children's environment |
| `ToolInstall`, `ToolResources`, `ToolsCommand` | `Drv/Install/` same names | Download, verify, lay out; the lists and locations; `daoris tool` |
| Host locators | `Drv/Service/ServiceHostLocator.cs`, `Drv/Knowledge/KnowledgeConnector.cs` | Which HTTP host and connector run: the install's, then the home's `bin` |
| Publish | `tools/desktop-publish.mjs`, `tools/service-publish.mjs` | Lay out or stage an install; build the two service hosts |
| Deployment rehearsal | `tools/deployment-rehearsal.mjs` | Publishes to scratch and drives the artefact, update phase included |

## Its rules

**Layout**

1. An install holds `Daoris.exe`, `app/`, `data/`, `update/` and `INSTALLED.md`, and beside them `profiles/`, `work/`, `cache/`,
   `run/` and `logs/`. `Daoris.exe` is the one thing a person runs. (from D93, D172)
2. `INSTALLED.md`, headed `# Daoris — installed desktop`, is the marker. It alone makes a folder an install, for the publish, the
   home and every program. (from D60, D93)
3. `app/` holds everything a build ships: `Daoris.Desktop.exe` beside its Chromium, both service hosts, the driver's program
   (`daoris-driver/`), the CLI (`cli/`), the launchers and Daoris's git doors (`bin/`), `resources.json` and `plugin-offers/`.
   An update replaces it whole. (from D93, D99, D103, D121, D124, D172)
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
13. Every home file is an entry of `HomeEntry`: a name, a category, a new place and an old place. The three twins hold the same
    rows in the same order. A reader asks `DaorisHome.Entry` by name and never joins a home path by hand. (from D172)
14. An entry is read at its new place where that holds it, else at its old place where that is there, else at its new place.
    "Holds" is a non-empty file that parses when JSON, a non-empty folder, or one of the entry's named files. (from D172)
15. An entry held in both places is refused, naming both, for the person to keep one. A list of items reads both places.
    Asking for a new place makes its category's folder. (from D172)
16. `data/` holds no secret: a file with a key is a sign-in and lives in `profiles/`. (from D172)
17. Back up `data/` (and `work/` for work in progress). `profiles/` is as private as the machine. `cache/` is fetched again,
    `run/` may be cleared while Daoris is closed, and logs keep 30 days or fewer. A category is cleaned by Daoris's own acts,
    never by deleting a folder while it runs. (from D172, D195)
18. One writer per home: `run/home.lease`. An instance holds it for its life; a verb, the first-start migration and the
    launcher's swap hold it for their act. A stale lease never blocks, and is removed by moving it aside and
    judging what moved, so a lease written meanwhile is put back. A live `driver.lock`, or a lease at the old place,
    still holds the home. (from D104, D171)
19. A home has one id, in `data/home.json`, read alike through every path to the folder. (from D171)
20. A home with no state, on a machine whose `~/.daoris` holds state, moves it in at the first start, `bin/` excepted, and
    says so once. (from D63)
21. At a start, an install moves once into D172's places only the entries Daoris alone names by a derived path: keys, remotes,
    the browsers' and window's profiles, the downloads, run files, logs. It runs only when the last update is at rest, no other
    process of the folder runs, and the `migration` lease is free at once; else the next start tries. (from D172)
22. The move renames on one volume and deletes nothing but an empty new folder. Each rename is journalled in
    `run/home-move.json` first, and a stopped move is put back at the next start. The machine log says `home.moved`,
    `home.move-recovered`, `home.move-failed`, `home.move-held`. (from D172)
23. Never moved: agents' account homes, session trees, rooms, kept files and `bin/`. A tree is one under either place of
    `trees`; new trees open at the new place. (from D172)

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
28. Every child the driver and the modules start gets the tools' environment: `app/bin/` first, then each managed or named
    tool's folders, and `GIT_CONFIG_GLOBAL` when git carries a setting. The application's environment and the account's `PATH`
    are never rewritten. (from D121, D124)
29. Agents stay on their makers' channels, each Install's own `IReleaseSource` (`ClaudeReleaseSource`, `CodexReleaseSource`) found
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

**Publishing**

33. `publish:desktop -- --to <folder>` has no default folder. It writes only its own names (`Daoris.exe`, `app`, `data`,
    `INSTALLED.md`, `update`). It refuses a non-empty folder with no marker; `--beside` writes next to other things, never over
    one of its names. (from D60, D93)
34. A republish removes exactly what `app/shell-files.txt` lists (a line naming a path, not one name, is no entry) and the retired names, then places its own. A publish into an
    install carrying a service host, without `--service`, is refused. (from D93)
35. It publishes only a tree the full gate set passed, unless the folder is under `_fixtures/` or `--force-ungated` is given,
    which it says. `--stage --from-gated <commit>` builds the gate tree's commit under its lock. (from D115, D180)
36. `--stage` builds the same build into `update/staged/`, with `build.json` (schema 1: id, version, commit, time, every file's
    size and SHA-256). It refuses a non-install, a swap under way, and a missing `--service` where the install carries its HTTP
    host. A running install is never refused. A publish in place removes anything staged. A build's version is the release's,
    `canon/canon.json`'s: the desktop's `Directory.Build.props` stamps it on every assembly and `build.json` names it, so the running
    build, the launcher's read of `app/` and a stage's manifest read one number. The commit follows it (`0.0.1+<commit>`) only where
    a publish builds: every publish recipe passes `-p:DaorisStamp=true`, which the root `Directory.Build.props`, imported by each
    tree's, reads. A development build reads no git, writes no source link and carries `+dev`, so the System page's build fact
    reads `dev`, and the instance hello and the store's recorded build (`store_version.build`) name one build for every development
    commit; neither decides on it. Measured on both desktop test projects: after a switch between two commits of one tree, all 12 projects compiled with
    the commit stamped (90 s) and none without it (20 s); an unchanged build compiles none either way (10 s); after 30 minutes
    and after 7 hours of main, 7 and 9 of the 12 (TREEPOOL1's R5.8). (from D139, D233)

**Updating**

37. The person's word on a staged build is `data/update.json`: when idle (the default, also when absent or unreadable), now,
    or not now. *Not now* holds for that build only. The banner and `daoris update --when-idle|--now|--cancel` both write it.
    (from D139, D50)
38. While it drains, the loop starts nothing new but still looks: endings are reported, plugins told, sync runs. A press that
    would add work waits for the restart; a stop, a take-back or an answer goes on. Once applying, every press waits. (from D139)
39. Idle is no driven session running and no conversation's turn in flight. A conversation between turns, a parked session
    and the terminal are idle. (from D139)
40. Before closing for a build, the application checks it: every listed file with its size and hash, nothing unlisted, the
    launcher, the application and its library, and the host where the install carries one. It refuses going back to an older
    version while a delivery is unresolved. `UpdateRefusal` is that one check, which `daoris update --now` asks too: refused, it says
    the sentence, names the deliveries, writes the word and exits 1. A refusal closes nothing, is said again at every look, and holds
    for its build until the person's next word, from the banner, Settings or the terminal, which checks again. (from D139, D215)
41. *Update now* is the person closing Daoris. It does not wait; driven sessions are recorded stopped and carried on at the
    next start. (from D139)
42. The application starts `Daoris.exe --update --after <pid>` and closes. The launcher waits up to three minutes for it and
    for everything running from `app/`. (from D139)
43. The launcher swaps only while nothing runs from `app/` and under the home's lease; held, it replaces nothing and says
    who held it. It checks the build again, moves `app/`, `Daoris.exe` and `INSTALLED.md` into `update/previous/`, then the
    staged three into place, journalling each move in `update/swap.json` before making it. (from D139, D171)
44. A held move is retried for up to two minutes, and the hold is journalled with what held it. (from D139)
45. The new application must confirm within 60 seconds of starting. One that exits or stays silent goes to `update/failed/`,
    and `previous/` comes back. A failed move undoes the journal in reverse; a launcher that died mid-swap is undone at the next
    start. Confirmed, `previous/` is removed. A build that failed is not tried again. (from D139)
46. A start that finds a build staged and nothing running from `app/` swaps first, unless the person said *Not now* of that
    build. Whatever happens, the launcher's last act is starting Daoris, unless that build is older than its home's last: a
    start nothing can roll back keeps its version in `data/last-build.json`, never lowered, and the launcher refuses a build
    below it in one sentence naming both, a swap onto one rolling back. It is the launcher's one check of an older build.
    (from D139, D232, D233)
47. The launcher writes no log. The next start says the outcome once (`update.installed`, `update.rolled-back`,
    `update.refused`), and every state carries `last` from the journal. Log lines name things and count them, never a path or
    a command line. (from D94, D139)
48. A build that confirms and fails later is not rolled back. A launcher refuses a `build.json` schema it does not read. (from D139)

**The store and the migration**

49. `data/desktop.db` is the desktop's SQLite store, opened by the driver library alone and never synced: one connection per
    process, write-ahead logging, `synchronous = FULL`, every write one immediate transaction. (from D212)
50. Store changes are numbered steps, forward only, one exclusive transaction each. The writer stamp (3) is never lowered. A
    build whose stamp is below the store's reads it and writes nothing. A stamp-raising step, and every import, waits until
    the start is settled. (from D139, D212, D226)
51. No code reads a record, key, file or layout in a shape an older build wrote. A release that changes a stored shape carries
    one migration, `HomeMigration`, run by the application's start once the home is known and before the staffing, the loop
    and the routes read it, under the home's lease; a lease held past its wait, or one that changed under every attempt to take it, runs nothing and says `home.migration-held`, and the next start runs it. A home the launcher's swap holds
    is not converted then, since a build that fails to confirm is rolled back onto the home it left: the application runs the
    migration again in the same process once the update has confirmed and the swap has let go. (from D232)
52. The migration is a list of steps, each converting its own records and idempotent. Before a file's first change it is
    copied into `data/salvage/<migration>/`, the Daoris folder's layout under it, a database with its journal files; a copy
    already there is kept. The copy is the undo. (from D232)
53. `data/migration.json` names the migration and each step as it finishes. A start whose record holds every step runs
    nothing; a step that throws, or whose record cannot be written, stops the run there, said in the machine log (`home.migrated`, `home.migration-held`,
    `home.migration-failed`), and the next start runs it and the steps after it. (from D232)
54. This release's migration is `nocompat1`, in the order of `HomeMigration.Steps`: the staffing into the store; the caps as
    the supervisor's rules; the old workflow records as saved graphs, then a push-landing workflow as steps; a run bound to a workflow version no graph reads, rebound to its place's choice; `driver.json`'s keys
    no code writes; `no-push` as git access; `harnesses.json`'s old names and pooled sections; a waiting say's `now`; unkeyed
    solicited attempts; held presses with no question; the event logs' `answered` receipts, `chat.told-routing` notes and old
    handed sections; the session records' `answer` column; finished records' English-only notes; closed quests with a
    six-character id; terminals' `*.turn` marks; the desktop store's names (store step 22: a binding's `door`, the staffing's
    projection tables dropped); the binding files into the store; the wired, usage, lineage and brainstorm files' door names;
    and a stored standing confirmation grant, removed. The
    next release that changes a shape replaces them. (from D232)
## Where a change goes

- **Install layout or publish:** `tools/desktop-publish.mjs`, with its twins in the same change: `StagedBuild`'s names,
  the launcher's `AppFolder`/`ShellExe`, `DaorisHome.InstallBinLayout`. `desktop-publish.test.ts` reads them all.
- **A new home file:** a `HomeEntry` row in all three twins, same order and cells, asked by name. Pick its category by
  rule 17. Add it to `HomePlacing.Moves` only if Daoris alone names it by a derived path.
- **Start-time home work:** `Mod/Update/InstallHome.cs`, `HomePlacing.cs`.
- **What the swap does:** `Drv/Install/StagedBuild.cs`, `InstallSwap.cs`, on the base library alone, since the launcher compiles
  them. **When to update:** `Mod/Update/InstallUpdater.cs`. **The request:** `Drv/Install/InstallUpdate.cs`.
- **A refusal to go back:** `Drv/Workplace/InstallRollback.cs`.
- **A changed stored shape:** a step of its own in `Drv/Transitional/Migration/`, one entry of `HomeMigration.Steps`, and the old
  reader deleted in the same chunk.
- **A store table or column:** a new numbered step in `DesktopStoreSteps.All` (`Store/DesktopStoreSteps.cs`). Raise the writer stamp only when older builds must
  stop writing.
- **A tool:** `Tools.Declared` and `Drv/resources.json`.

**Tests that pin the area.** Driver: `Install/StagedBuildTests`, `BuildVersionTests`, `InstallUpdateTests`, `UpdateDrainTests`,
`UpdateCommandTests`, `StartConfirmationTests`, `HeldFilesTests`, the `Tool*Tests`; `Platform/DaorisHomeTests`, `HomeLeaseTests`,
`HomeIdentityTests`; `Store/DesktopStore*Tests`; `Install/HomeMigrationTests`;
`Service/ServiceHostLocatorTests`; `PinnedUpdatesTests`. Modules: `Update/InstallHomeTests`,
`FolderMoveTests`, `HomePlacingProcessTests`, `AppWorkingFolderProcessTests`, `InstallUpdaterTests`, `UpdateRefusalRetryTests`, `UpdateModuleTests`, `UpdateRelaunchGateTests`,
`UpdateWatchTests`; `Windows/TaskbarIdentityTests`; `EveryModuleChildIsHandedTheToolsTests`. Service:
`Platform/DaorisHomeTests`. CLI: `test/platform/home.test.ts`, `home-census.test.ts`, `tool-test/desktop-publish.test.ts`,
`test/deployment-rehearsal.test.ts`.

**Gates that bite.** `driver`, `modules`, `driver-process`, `modules-process` and `censuses` for the C#; `service` for its twin;
`cli` (`npm run verify`: the home census, the size ratchet); `tool-tests` for the publish; and
`deployment` (`npm run rehearse:deploy`, Windows only, run at the merge), whose phase 9 stages beside a running install,
drains, swaps, rolls back and refuses.

## Contradictions

1. D27 (the devkit verifies its own hash) vs `VersionPin.Require` (`src/Daoris.Devkit/Daoris.Devkit.Core/VersionPin.cs`) and the devkit
   README's Distribution section (a version pin only; the installer verifies the digest). The code reads right.
2. D43 (`--install` lands the hosts in `~/.daoris/bin`) vs `tools/service-publish.mjs`'s `--install` (the home's `bin` entry). The
   code reads right; D43 itself carries no amendment.
3. D172 point 2 (`DAORIS_HOME` names the Daoris folder) vs `DaorisHome.InstallOf`/`OfInstall` (`Drv/Platform/DaorisHome.cs`) and
   `Mod/Update/InstallHome.cs`'s home setting (an install's home is `<install>/data`). The code and D172's HOMELAYOUT3 note read right.
4. D172 point 6 (the move runs "at a start after a confirmed update") vs the `HomePlacing` summary (`Mod/Update/HomePlacing.cs`: any start with the
   update at rest, never the first after a swap). The code reads right.
5. D139 §3 (`daoris-driver update`) vs `Drv/Install/UpdateCommand.cs` (the verb is `daoris update`). The tree reads right.
6. D121 §2 (`tools.json` twinned in the CLI; versions in `<home>/tools/`) vs `Drv/Install/Tools.cs` (`Tools`' remarks and `Declared`: the driver
   alone; the CLI's `tools.ts` is gone) and the `ToolVersions` entry (`cache/tools`). The code reads right.
7. D57 (managed agents under `~/.daoris/toolchain/`) vs the `Toolchain` entry (`cache/toolchain`). The code reads right.
8. D232 (no older reader) vs `HomeEntry.Old` places, `HomeLease.OlderIn` (old lease and `driver.lock`) and
   `InstallHome.Migrate` (D63's `~/.daoris` move-in), none of them in the migration. The code is today's
   behaviour until ONEWAY1w moves them into the migration.

## Open

- **ONEWAY1w**: the home's old places as a migration step: every entry at its old place moved, `driver.lock`, D63's move-in and
  the flat service host ended.
- **HOMELAYOUT5**: measure, per harness, whether account homes, trees, rooms and kept files can move safely.
- The trees' links repaired after a person moves the whole Daoris folder (the install layout design §7.4): designed, not built.
