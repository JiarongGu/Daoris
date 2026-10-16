# The Daoris folder: what each top folder holds, and how an existing one moves into it

The design under D172, for HOMELAYOUT1 and the rows HOMELAYOUT2–5. Written 2026-10-16 from a census of the code at
`892059f2` (every writer of a path under the home, cited by file and line) and a listing of the owner's install, names
and sizes only. The page that shows it is WIRING1a's (`docs/2026-10-16-this-daoris-design.md`).

## 0. The owner's direction

> *"we also need to shape the Daoris folder into a better separation since we will have more files now (data, app, res, …)"*

One ask: a person opening the Daoris folder sees **which part is theirs, which holds a sign-in, and which Daoris can
fetch again**, by folder, and each folder means one thing for backup, moving and clean-up.

## 1. What is there today

- **The install** (D93, D139): `Daoris.exe`, `INSTALLED.md`, `app/` (the application on its Chromium, the hosts, the CLI,
  the launchers, the plugin offers, `resources.json`), `data/` (the home, D63) and `update/` (a staged build and the swap's
  journal). `tools/desktop-publish.mjs:73-127, 194-303, 350, 486` states the layout once; `installedNote()` (`:635`)
  writes `INSTALLED.md`.
- **The home is one variable** (D63): `DAORIS_HOME`, three twins sharing no code, the driver's `DaorisHome.cs`, the
  service's `DaorisHome.cs` and the CLI's `src/platform/home.ts`. An install's application sets it to its own `data/`
  (`InstallHome.Establish`, `InstallHome.cs:113-167`); a program under `app/` takes the same (`DaorisHome.Adopt`, FINDDAORIS1,
  `DaorisHome.cs:80-89`). The per-file overrides still win (`tools/desktop.mjs:63-72`).
- **Five callers ask the home for a path** (`DaorisHome.Require`: `DriverConfig.cs:503`, `HarnessSettings.cs:85`,
  `RemotesModule.cs:36`, `BrowserModule.cs:44`, `LogsConsole.cs:30`); **132 joins in 92 C# files make one by hand**
  (`Path.Combine(home, …)`; `"sessions"` alone in 30 files), and the CLI joins its own (`toolchain.ts:546, 573, 675`).
- **The UI kit has a data area of its own.** The kit anchors at the install's root (`Program.cs:73-88`) and keeps the
  window's engine profile (`chromium/`) and its geometry and theme copy (`config/`, `Program.cs:257, 268`,
  `SecondaryWindowHost.cs:71`) under `<root>/data`, which in an install is the home. Its `ShenoraPathsOptions.DataDirectory`
  names that folder outright, and its `res/` is read-only resources a build ships (the kit's own documentation).
- **The dev loop has two roots**: the home in its scratch folder's `home`, and the kit's root in its `app`
  (`tools/desktop.mjs:154-167, 734, 751`), so its window's data is not where an install's is.

**The owner's install, 2026-10-16** (`du`, rounded): `app/` 565 MB; `data/` 5.1 GB, of which session trees 1.7 GB,
the agents' downloaded versions 1.4 GB, Daoris's browser's profiles 1.1 GB, the service's installed copy (`bin/`) 369 MB,
the agents' account homes 221 MB, the window's engine profile 189 MB, a WebView2 profile from before D92 84 MB, the
knowledge store 48 MB, a plugin 48 MB, the conversations 23 MB, the logs 3 MB, and 67 KB of settings files. Laid out as
§2 says, `data/` would hold about 120 MB.

## 2. The categories and their top folders

```
Daoris.exe      the one thing to run (D93)
INSTALLED.md    what this folder is, written by the publish
app/            the program
update/         a build staged, the one before it, and the swap's journal (D139)
data/           your settings and records
profiles/       sign-ins
work/           where agents work, and the files records keep
cache/          what Daoris fetched and can fetch again
run/            what a running Daoris holds
logs/           the machine log
```

A folder exists once something is written there; an empty install has `app/` and `data/`. A development or scratch root
has the same folders less `app/` and `update/`. The window's words for each are WIRING1a's §8; `profiles/` is the code's
word, and the window says *Sign-ins* (the glossary keeps *profile* off the window).

**The tests that place an entry**, in order:
1. A build ships it: `app/`.
2. It holds a key, a token, a cookie or an agent's own sign-in: `profiles/`.
3. An agent works in it, or a record keeps it as a file an agent is handed: `work/`.
4. Daoris downloaded it, or a publish installed it, and the next use can do so again: `cache/`.
5. It means something only while a process runs, or expires by itself within a day: `run/`.
6. It is the machine log: `logs/`.
7. Anything else Daoris writes is the person's choice or record: `data/`.

## 3. Every current path, placed

The old place is under the old home: an install's `data/`, or a scratch root itself (§5). **Same** means the new place is
the old one, so nothing moves. **Moves** is a rename at a start (§7). **Stays** means the entry is never moved and is read
where it is, while one made after the change is made at its new place (D172 point 6).

### 3.1 `data/`: your settings and records

| Old place | Written by | New place |
|---|---|---|
| `driver.json` | `DriverConfig.cs:497, 503`; twin `driverconfig.ts`; `DAORIS_DRIVER_CONFIG` wins | same |
| `harnesses.json` | `HarnessSettings.cs:85`; `DAORIS_HARNESS_CONFIG` wins | same |
| `accounts.json`, `cooling.json`, `windows.json`, `reads.json` | `AccountNames.cs:65`, `AccountCooling.cs:48`, `AccountWindows.cs:41`, `AccountReads.cs:39`; the CLI's `accountnames.ts:30`, `cooling.ts:29`, `windows.ts:36`, `accountreads.ts:43` | same |
| `permissions.json`, `proposals/` | `Permissions.cs:91`, `RuleProposals.cs:81`; the service's `RuleProposals.cs:50`; the CLI's `permissions.ts:29`, `ruleproposals.ts:32`; `DAORIS_RULES_HOME` names the home they go under | same |
| `tools.json` | `Tools.cs:79`; the CLI's `tools.ts:35` | same |
| `plugins.json`, `plugins/` (with `.data/` and `.trials/`) | `PluginState.cs:11`, `PluginCatalog.cs:39, 42`, `PluginTrials.cs:15`; the CLI's `plugins.ts:63, 66` | same. A plugin's folder may be its only copy (D101: one copied in by hand), and `.data/` is what it keeps |
| `plugin-approvals.json`, the person's approval of what a plugin hands Ask Daoris (HELPPLUG1a, placed by HOMELAYOUT2's merge) | `HelpGrants.cs` | same |
| `home.json`, the home's id (HOMEID1, placed by HOMELAYOUT2's merge): a new one would be another home to every terminal | `HomeIdentity.cs` in the driver and the service; the CLI's `home.ts` (`ensureHomeId`) | same |
| `usage.json` | `Usage.cs:50` | same |
| `landings.json`, `session-branches.json`, `lines-moved.json`, `registry-followed.json`, `abandoned.json` | `LandedBranches.cs:163`, `SessionBranches.cs:41`, `RegistryFollowing.cs:30, 32`, `AbandonRecord.cs:24` | same |
| `update.json`, the person's word on a staged build | `StagedBuild.cs:37` | same: D139 §7's request under the home |
| `knowledge.db` with its `-wal` and `-shm` | `HostComposition.cs:46`; `DAORIS_KNOWLEDGE_DB` wins | same |
| `sessions/`: conversations, transcripts, marks (`archived.json`, `held-words.json`, `auto-landings.json`) | `SessionHomeFiles.cs:46, 88, 259`, `SessionArchive.cs:23`, `AutoLanding.cs:239` and the history design's §2.2 | same, less the rows below marked for `work/` and `run/` |
| `helpers/`, `handed/`, `setup/`, `workflows/` (with `runs/`) | `HelperFiles.cs:15` and the service's `HelperBox.cs:25`; `HandOverQuest.cs:19`; `WorkspaceSetupFile.cs:25`; `WorkflowStore.cs:24`, `WorkflowRunBindings.cs:36` | same |
| `help/proposals/`, Ask Daoris's proposals | the service's `HelpProposalBox.cs:65`; `HelpProposals.cs:437` | same |
| `opinions/gates/`, `opinions/due.json` | `OpinionGates.cs:21`, `OpinionDues.cs:21` | same |
| `browser/settings.json`, `browser/favorites.json` | `BrowserSettings.cs:58`, `BrowserFavorites.cs:29` | same |

### 3.2 `profiles/`: sign-ins

| Old place | Written by | New place | |
|---|---|---|---|
| `keys.json`, the API keys | `HarnessSelection.cs:35`; the CLI's `toolchain.ts:675` | `profiles/keys.json` | moves |
| `remotes.json`, each workspace's deployment and key | `RemoteTarget.cs:40`, the service's `RemoteConfig.cs:35`, the CLI's remote map; `DAORIS_REMOTE_CONFIG` wins | `profiles/remotes.json` | moves |
| `harnesses/<agent>/<account>/`, an agent's own home | `HarnessSettings.cs:492, 501`; the CLI's `toolchain.ts:546, 622` | `profiles/agents/<agent>/<account>/` | stays: the tool's folder (D49 §4), which records paths inside it |
| `browser/engine/`, the shipped Chromium's profile | `EngineBrowser.cs:48` | `profiles/browser/engine/` | moves |
| `browser/edge/`, `browser/edge-session-cookies.bin` | `EdgeBrowser.cs:23`, `BrowserSessionCookies.cs:52` | `profiles/browser/edge/`, `profiles/browser/edge-session-cookies.bin` | moves |
| `chromium/`, `config/`, the kit's data area: the window's engine profile, geometry, theme copy and secondary windows | the kit; `Program.cs:257, 268`, `SecondaryWindowHost.cs:71`, `KeptScheme.cs:24` | `profiles/window/chromium/`, `profiles/window/config/` | moves, as one (§7) |

### 3.3 `work/`: where agents work, and the files records keep

| Old place | Written by | New place | |
|---|---|---|---|
| `trees/<workspace>/<repository>/<tree>`, with `.tidied/` | `TreeContext.cs:42`, `TreeSetAside.cs:32`, `WorkAbandoning.cs:574`, `OpinionTree.cs:130` | `work/trees/…` | stays, per tree: git's worktree links and an agent's conversation name it |
| `help/`, Ask Daoris's room (its rendered `AGENTS.md` and `CLAUDE.md`) | `HelpRoom.cs:27` | `work/rooms/help/` | stays: every Ask Daoris conversation ran in it. Its old place counts as there while it holds the rendered `CLAUDE.md`, since `data/help/` also holds the proposals |
| `intake/<workspace>/`, an intake's room | `IntakeRoom.cs:29` | `work/rooms/intake/<workspace>/` | stays, per workspace |
| `opinions/<id>/`, a reviewer's packet | `OpinionPackets.cs:44`, `OpinionDeliveries.cs:15` | `work/opinions/<id>/` | stays, per opinion |
| `quests/<id>/`, `asks/<id>/`, the attachments | the service's `QuestFiles.cs:30, 58` and `Asks.cs:880`; `HistoryHome.cs:13-14` | `work/files/quests/<id>/`, `work/files/asks/<id>/` | stays, per record: handed to sessions as `DAORIS_QUEST_ATTACHMENTS` |
| `sessions/<id>/files/`, a conversation's attachments | `ChatFiles.cs:37` | `work/files/sessions/<id>/` | stays, per session |
| `proof/<session>/`, kept proof | the landing design's `${proof}` (`2026-10-04-landing-and-proof-design.md:222, 273`); no writer found in the code at this commit | `work/files/proof/<session>/` | stays, per session |
| `usage-read/`, where the tool's own sign-in's usage read runs (found by HOMELAYOUT2's census) | `HarnessRoster.cs` (`usage-read` under the home where no account is asked) | `work/usage-read/` | moves: an empty folder an agent runs in |

### 3.4 `cache/`: what can be fetched again

| Old place | Written by | New place | |
|---|---|---|---|
| `toolchain/<agent>/<version>/` | `HarnessSettings.cs:447`; the CLI's `toolchain.ts:573` | `cache/toolchain/…` | moves |
| `tools/<tool>/<version>/`, `tools/locations/` | `Tools.cs:82`, `ToolInstall.cs:70-75`, `ToolResources.cs:107, 274` | `cache/tools/…` | moves |
| `bin/`, the service's installed copy | `publish:service --install` (`tools/service-publish.mjs:7`); read by `ServiceHostLocator.cs:62`, `KnowledgeConnector.cs:153` | `cache/bin/` | stays: a repository's `.mcp.json` names it by the snippet `--install` printed |

### 3.5 `run/`: what a running Daoris holds

| Old place | Written by | New place |
|---|---|---|
| `home.lease`, the home's one writer (HOMELEASE1, placed by HOMELAYOUT2's merge) | `HomeLease.cs`, compiled into the launcher; the CLI's `lease.ts` | `run/home.lease` |
| `driver.lock`, read as an older build's lease while its holder lives | a build from before HOMELEASE1; read by `HomeLease.cs` and `lease.ts` | `run/driver.lock` |
| `running.json`, the address a terminal finds (D171) | `RunningAddress.cs:30`; the CLI's `service.ts:62` | `run/running.json` |
| `locks/`, `spawn/` (with each session's `<session>.plugins/`, HELPPLUG1b), `starts/`, `hooks/` | `TreeLock.cs:32`, `SpawnServers.cs:21`, `Permissions.cs:467` and `SpawnSkills.cs`, `StartRequests.cs:24`, `TreeGuard.cs:45` | `run/locks/`, `run/spawn/`, `run/starts/`, `run/hooks/` |
| `harnesses/.probing/`, the usage read's locks | `ProbeLock.cs:29, 44-53`; the CLI's `probelock.ts:41-50` | `run/probing/` |
| `sessions/<id>.pid`, `sessions/requests/` | `SessionProcesses.cs:389`, `SessionRequests.cs:140` | `run/sessions/<id>.pid`, `run/requests/` |
| `browser/edge.json`, what Daoris started its Edge with | `EdgeBrowser.cs:26` | `run/edge.json` |

Each moves at a start where nothing holds it (§7); one held is read where it is.

### 3.6 `logs/`, and what stays as found

| Old place | Written by | New place | |
|---|---|---|---|
| `logs/` | the driver's `MachineLog.cs:32`, the service's `MachineLog.cs:27`, `LogsConsole.cs:30` | `logs/` | moves |
| `webview2/` | nothing since D92; `InstallHome.cs:89` names it only so it is not taken for state | — | stays: left over, shown with its size (WIRING1a) |
| `salvage/` | nothing in Daoris (the history design's §2.2) | — | stays: the person's |
| `browser/profile/` | no current code | — | stays: an earlier build's |
| any other name | — | — | stays |

## 4. Backup, move and clean-up, per folder

| Folder | Backup | Moving it | Clean-up |
|---|---|---|---|
| `app/`, `update/` | never: a publish or an update lays it again | with the folder | an update replaces `app/` and removes `update/previous/` once confirmed; `update/failed/` is kept for the person to read (D139) |
| `data/` | **always**: it is the only copy of every choice and record. Copy `knowledge.db` with Daoris closed, or its `-wal` and `-shm` with it | with the folder; a record names a repository's checkout where it is, which a move of the Daoris folder does not change | only Daoris's acts: clear (D153), archive and delete (D126), a plugin's removal (D119); never by hand while Daoris runs |
| `profiles/` | only somewhere as private as this machine, or not at all: signing in again restores it | with the folder; a tool's home that recorded a path of its own may need its sign-in again (HOMELAYOUT5 measures) | removing an account (D125) or a key; deleting the folder signs every account out and the window forgets its place and its page's choices |
| `work/` | for work in progress: a tree's commits are already in its repository, its uncommitted changes only here | with the folder, then each tree's link repaired (§7.4); a conversation resumed in a moved folder is HOMELAYOUT5's question | trees clean and land (D88, D102), clear (D153); a tree deleted by hand leaves its branch and a worktree entry in its repository |
| `cache/` | never | freely | anything, with Daoris closed: the next use downloads again; `bin/` comes back with `publish:service --install` |
| `run/` | never | never while Daoris runs | each entry expires by its own rule (a start a day, a spawn file at its session's end, a request a minute, a lock nobody holds taken over); deleting it with Daoris closed is safe |
| `logs/` | optional: it is evidence, no one's words (D94) | freely | the machine log keeps 30 days |

## 5. The Daoris folder and the variables

- **`DAORIS_HOME` names the Daoris folder.** For an install it is the folder beside `Daoris.exe`; `InstallHome.Establish`
  and `DaorisHome.Adopt` set it to that folder, and set the account's once where the account names none, as D63 says.
- **A value naming an install's `data/` names that install** (a folder named `data` whose parent holds `INSTALLED.md`).
  Every account that met D63 holds one; the desktop wrote it, so it is read, never rewritten. The same rule gives the
  old home: for that install `data/`, for any other root the root itself. `InstallHome.AccountOf` compares the two
  after the rule, so an account naming `<install>/data` is `same`.
- **Still winning**, as D63 and D105 say: `DAORIS_HOME` named for one start; the per-file overrides
  `DAORIS_DRIVER_CONFIG`, `DAORIS_HARNESS_CONFIG`, `DAORIS_REMOTE_CONFIG`, `DAORIS_KNOWLEDGE_DB` and `DAORIS_RULES_HOME`;
  the programs' `DAORIS_HTTP_HOST` and `DAORIS_MCP_HOST`; `DAORIS_SERVICE_URL` and D171's order for it.
- **`--app-root` names the kit's root** and so its single-instance scope; the window's own memory is
  `<kit root>/profiles/window/`, handed to the kit as `DataDirectory`, by §6.2's rule for an old one.
- **The refusal changes its words, not its rule**: no home, no default; it says to set `DAORIS_HOME` to the Daoris
  folder, the one holding `Daoris.exe` (`DaorisHome.Sentence` in both C# twins and the CLI's).
- **D171's resolution names the folder**: a program under an install's `app/` takes the install (`DaorisHome.OfInstall`),
  and reads the address at `run/running.json`.
- **D124's `app/bin/`** is the home's own `app/bin/` (WSSETUP3's "beside the home" becomes "in the home"); a root with no
  `app/` adds nothing to a child's `PATH`, as before.

## 6. One shape

### 6.1 Where each root is

| Run | `DAORIS_HOME` | the kit's root | the window's memory |
|---|---|---|---|
| An install | the install's folder (or its `data/`, read as it) | the install's folder | `profiles/window/` |
| The dev loop's scratch run | its scratch root | `--app-root` naming the same folder | that root's `profiles/window/` |
| `npm run desktop -- run --real` | the account's, a real home | the build's folder | the build's own, never the install's |
| A rehearsal | its install under `_fixtures` | that install | its `profiles/window/` |
| A test | a scratch folder it made | none | none |

### 6.2 An entry's place, for every reader

For each entry the table (§3) holds its category, its new path and its old path. A reader asks for the entry, never for a
path:

1. its new place, if it holds what the entry is (a file that is not empty and, for JSON, parses; a folder that is not empty;
   or, for an entry known by the names it holds, one of them), so an empty or half-made new place never shadows a populated
   old one;
2. else its old place, if it is there, read even where it does not parse, for its reader to say so;
3. else its new place, where a writer makes it.

Where both places hold the entry, neither is chosen: the reader is refused with a line naming both, and says so (D172's
HOMELAYOUT2 note).

An entry kept per item (a tree, an account home, a quest's files, a session's files) is asked per item, so an old one is
read where it is and a new one is made in the new place; a list of them reads both places. The window's memory is one
entry of two folders: `profiles/window/` if it is there, else the old home itself where `chromium/` or `config/` is there.

## 7. The move (HOMELAYOUT3)

1. **When.** At the desktop's start, before anything resolves a path, only when no other process of the folder runs (the
   driver's lock is free and nothing else runs from `app/`), and only after a confirmed swap: never while
   `update/previous/` is there, since D139 may still put the build before back, which reads only old places.
2. **What.** Each entry §3 marks *moves*, whose new place is not there and whose old place is. Nothing else.
3. **How.** A rename, file or folder, on the one volume the folder is on; never a copy. One that fails leaves the entry
   where it was, read there by §6.2 and tried again at the next start. Nothing is deleted. The window's memory moves as one:
   both folders into `profiles/window.moving/`, which takes its name once both are in, or both go back.
4. **Trees after the whole folder moved.** A start whose trees' links name another folder runs `git worktree repair` from
   each registered repository for its trees, and says which repository refused. A tree is never copied.
5. **What is said.** A machine log line per entry, `home.moved` (`entry`, `from`, `to`) or `home.move-failed` (`entry`,
   `reason`), added to the machine log design's §4; the start's notice names what moved and what could not, once, as D63's
   move-in does (`HomeEstablished`).
6. **Order with D63's move-in.** A `~/.daoris` of before D63 is moved into the old places first, then this move runs.

## 8. Hazards, read from the code

1. **A conversation keyed by its folder.** A tree, Ask Daoris's room and an intake's room are where an agent's
   conversation ran, and a harness may find a conversation to resume by that folder (Claude Code keeps its transcripts
   under its home by the folder's path). Nothing moves them until HOMELAYOUT5 measures each harness.
2. **A tool's own home records paths.** An agent's account home may name its own folder inside it (its installed
   plugins, its trust of a tree by path, `ClaudeTrust.cs:78`); moving it is the tool's question, measured first.
3. **A record that kept an absolute path.** HOMELAYOUT2's census finds any; such an entry stays, as point 6 of D172 says.
4. **An older reader.** A program from before HOMELAYOUT2 reads only old places. The install's programs are swapped
   together (D139); a `daoris` from elsewhere on a `PATH`, older than this build, sees a moved entry as absent.
5. **Another process holding a file.** A host adopted from an earlier start may hold `logs/` open; its rename fails and
   is tried at the next start.

## 9. Not chosen

- **Categories inside `data/`**, **`DAORIS_HOME` naming `data/` with the other folders beside it**, **`res/` for
  downloads**, **a record of the moves** and **moving everything at once**: D172's *Rejected* says why.
- **A variable per category**: a gate would have seven things to name to keep a run off a real machine; the per-file
  overrides already name what a gate needs.
- **A link left at each old place** (a directory junction): it would keep an old path working only on Windows, hide which
  place is real, and make a backup copy everything twice.
- **Splitting `remotes.json`** into its addresses and its keys: the file is three twins' contract (WSP3); it moves whole.

## 10. Build rows

- **HOMELAYOUT2**: the table of §3 in each home twin, each entry asked by name through §6.2, and every caller converted,
  the tests' fixtures included, with nothing moved yet (every new place still the old one). Proof: a census test that no
  code joins a home path by hand, and each twin's table held cell for cell (`twins.md`).
- **HOMELAYOUT3**: the new places switched on and §7's move. Proof as its row says.
- **HOMELAYOUT4**: the publish's `INSTALLED.md` saying what each top folder is, the dev loop's one root (§6.1), and
  `engineLogOf` reading the window's memory. What a build ships stays in `app/` (D172, *Rejected*).
- **HOMELAYOUT5** (proposed): measure, per harness, whether a conversation resumes from a moved folder and an account home
  works moved; then move what §3 marks *stays*, where measured safe.

## 11. What this design's gate does not cover

`npm run verify` checks this document's links and the router. Whether every writer is in §3 is held by HOMELAYOUT2's
census, not here: the census above is by search at `892059f2`, and a writer added since is not in it. The sizes in §1 are
one machine's on one day.
