# Daoris.Desktop — the local driver

**Status: built — the driver, its headless host and the shell all exist, and the family rehearsal
drives them end to end.** `docs/2026-09-19-driver-design.md` (D46) is the contract; how each part
got its shape is in `docs/decisions/` and `docs/task-archive.md`, not here.

For an isolated development start, run these from the repository root:

```sh
node tools/desktop.mjs build
node tools/desktop.mjs run
```

The run uses its own home and copied examples. The [dev-loop section](#the-dev-loop--toolsdesktopmjs-2026-09-21)
explains captures and shutdown; [Installing it](#installing-it-2026-09-22) explains the published layout.

| Project | What it is |
|---|---|
| `Daoris.Desktop.Driver` | The library: the loop and the planner; the adapter seam, whose `Toolchain` declares a harness's binary, version question, configuration home and install, update and login flows (D49 §4), and whose `Wire` is the door a session is held over — the pipe, or ACP (`AcpSession`, with a permission request refused by construction); `ChatRunner` for conversations; `HarnessRoster` (is the harness here, and which account does this run as); and `RemoteSync`, which rides the tick once per workspace |
| `Daoris.Desktop.Driver.Host` | `daoris-driver`, the headless door onto the same library: the loop by its verb, `drive [--once \| --until-idle] [--share]` (DRV8a, D104: with no verb it prints its usage and exits 2, and a loop refuses a home another live driver holds, naming it, unless `--share`), chat, ask, trees and sync, the set-up press (`setup <repository> [--plan]`, LAYOUT7, D117 §6, D124 §2: the repository's layout read from its line as git objects, each refusal said with its door, and one ask to that repository, as yours, whose body is the adoption playbook in the canon's words; the press adds the doctrine tool's exact verbs to the repository's rules, taken back if the service refuses, and `--plan` prints what was read, the rule, the landing, the agent and the quest's text and publishes nothing), a whole workspace's plan (`setup --workspace <name> [--plan] [--at-once <n>] [--pilot <n>] [--first <repo>…] [--skip <repo>…]` and `--pause \| --resume \| --stop`, WSSETUP6, D124 §4: a press writes `<home>/setup/<workspace>.json` and adds the doctrine tool's verbs once to the workspace's rules, then the loop publishes one set-up at a time by default, never the cap's last slot, in the order other work touches the repositories, skipping a refusal with its reason and pausing once a pilot of two has closed), deleting a quest or an ask made by mistake (`quest delete`, `ask --delete`, D95), clearing finished history from this machine (`history [--workspace <name>] [--json]`, `history clear --workspace <name> [--yes]`, `quest clear <id> [--failed] [--yes]` and `ask --clear <id> [--yes]`, HIST1d, D153, the history-clearing design §6.2: what the home keeps of each workspace's finished work, or the one named, what a clear would take and what it keeps and why, `--json` printing `HISTORY_PLAN`'s answer field for field from the driver library's one projection; then each clear listed first, every kept unit in its keep's own sentence, and pressed with `--yes` through `HistoryClearing` at the door `terminal`; exit 1 where a quest, an ask or a quest's failed sessions named is kept), accepting a done's departure from what you required, so what it held goes on (`quest accept`, DRIFT1d), marking a quest done as yours, its record saying so with your words (`quest done <id> [--note "…"]`, QUESTCLOSE1; a finish at a checkpoint that left its quest taken names it), answering a go-ahead a session asked on an ask, which every session on it is handed and which sends a parked session on once none of its go-aheads waits (`ask --go-ahead <id> <n> approve\|refuse ["…"]`, KNOWUSE1a, GOAHEAD2), and the machine log read back (`logs [--since <30m\|2h\|3d>] [--source <name>] [--event <name>] [--level <warn\|error>] [--json]`, LOG1c): every source's files under the home merged by time, one readable line each or as written with `--json`, a line that cannot be read skipped and counted on standard error, and an install's update (`update [--install <folder>]`, `--when-idle \| --now \| --cancel`, UPDATE1, D139): what is staged and how the last swap ended, or the word on it, as the window's banner says it |
| `Daoris.Desktop.Modules` | The shell's head: the loop, the host supervisor, every IPC module the page talks to, and `Refusals`, where a refusal is declared once as a code the page translates. Plain `net10.0`, with its own tests and gate |
| `Daoris.Desktop.App` | `Daoris.Desktop.exe`, the window, on Shenora 0.19's own Chromium (`ChromiumView`, D92): CEF's launcher, laid out beside the engine by the build, wearing Daoris's icon and name, which the kit copies from the app's assembly (SHEN1). **And `daoris-browser`, Daoris's own browser** (D85, CHR3, since CHR8 D99): the same executable started with `--daoris-browser` first, which `Main` hands to the kit's `ChromiumBrowserProcess.Run` before anything else (`BrowserProcess`). A process of its own because the engine's debug port reaches every page in its process; it shows Chromium's own window, made over its port, and answers `${browser}` on that port, a relay of the kit's that calls a new tab a `page`. It ends when its last window closes or the shell does. The shell starts it with `ChromiumBrowserProcess.Start` (`EngineBrowserHost`), so an install carries one Chromium |
| `Daoris.Desktop.Launcher` | `Daoris.exe`, the one thing at an install's root (D93): it starts `app/Daoris.Desktop.exe` with its arguments and exits, its process carrying Daoris's taskbar id (D108). Framework-dependent, single-file, referencing nothing. It is also the one program that replaces the application (UPDATE1, D139): while nothing runs from `app/`, a build in `update/staged/` is checked and swapped in with a journal (`update/swap.json`), started, and rolled back when it does not confirm — asked for by the application as `--update --after <pid>`, or on a start finding a build staged. The check and the swap are the driver library's `StagedBuild.cs`, compiled in |

The adapters are the stub, `acp-stub` (the protocol door with no model in it), `claude-code`, and the
protocol door's configurations `claude-code-acp`, `codex-acp` and `dsh`. A session's record is
concluded from its exit code and its quest, never from what it said.

## What the page can ask this machine

| Module | What it carries |
|---|---|
| `DAORIS.DRIVER` | The driver, a partial per domain (MOD5): `DriverModule.<Domain>.cs`, named as the page's `bridge/<domain>.ts` is, each route a handler marked `[DriverRoute]` in it. Its domains' rows follow this table; `DriverModuleRoutesTests` holds that every route the page sends is answered, every route answered is asked by the page or a test, and each is marked in its domain's partial and named in its domain's row |
| `DAORIS.REGISTRY` | A folder picked and inspected; an existing manifest's declaration written, uncommitted, for that repository's review |
| `DAORIS.REMOTES` | The machine's `remotes.json`, the file `daoris remote` edits: a key goes in, and only its audit prefix comes back |
| `DAORIS.BROWSER` | Daoris's browser's two files under the home, as Settings → Browser: `favorites.json` (`ADD_FAVORITE`, `REMOVE_FAVORITE`), shown in a Daoris folder on its bookmarks bar, and `settings.json` (`SET_EXTENSIONS`: other software's Chrome extensions offered or refused; `SET_BROWSER`: Daoris's own or the person's Edge; `SET_LINKS`: whether the page's links open in the system's browser or Daoris's, BRW7). The same files `daoris browser` edits, read by `daoris-browser` at each start, and `links` by the page |
| `DAORIS.WINDOWS` | Named secondary windows: `monitor` and `session:<id>`; and `OPEN_BROWSER`, Daoris's own browser (D78, D85): `daoris-browser` (this executable with `--daoris-browser`, CHR8) started, or its window brought forward. With a `url`, a link on the page opened there in a tab of its own (BRW7), a web page only, by the favorites' address rule. Its profile is under the home at `browser/engine`, with no bridge, and a loopback CDP port that a plugin's browser MCP attaches to |
| `DAORIS.TERMINAL` | The person's own shells, for the terminal view (CONSOLE4, D96): `SHELLS` (what this machine has on the tools' PATH, `pwsh` the default, else `powershell`, then `cmd` and Git Bash beside the git the tools resolve, else the system's; D121, TOOLS5), `OPEN` {shell?, cwd?, cols?, rows?} a shell under a Windows pseudo-console (`PseudoConsole`) with `DAORIS_HOME` and the tools' `PATH` in its environment, in the folder asked for or else the home, `INPUT`, `RESIZE` and `CLOSE`, with its output as batched `TERMINAL_OUTPUT` events and `TERMINAL_EXITED` when a shell ends on its own. Each shell's job ends everything it started with its tab, and every terminal ends with the application. Nothing typed or printed reaches the machine log |
| `DAORIS.UPDATE` | The install's update (UPDATE1, D139), held by `InstallUpdater`: `STATE` (none, waiting, draining, applying or refused; the staged build by id, version and commit; the mode that holds for it; what still runs; a refusal's code; the last swap's outcome, said once), `SET` {mode: when-idle, now, not-now}, written to `$DAORIS_HOME/update.json` as `daoris-driver update --when-idle\|--now\|--cancel` writes it (`UPDATE_NOTHING_STAGED`, `UPDATE_MODE_UNKNOWN`), and `DISMISS`. Each change reaches the page as `UPDATE_STATE`; no path is named |
| `DAORIS.LOG` | The page's report into the machine log (LOG1b, D94): `EVENT` with `{ event, data }`, kept only as the catalogue names it (a view opened, a command run, a view moved, a message's length and file count, a proposal settled, a caught error), each field in its kind, everything else dropped. Every refusal any module answers is logged beside it by its code (`RefusalLog`, a middleware in the dispatcher). And Settings → Logs reading it back (LOG1c): `LINES` {since?, source?, event?, level?, limit?} answers the newest lines first (200, at most 1000) with the folder, how many matched, a count per level, the period's events and the lines skipped, filtered here by the reader `daoris-driver logs` uses (`MachineLogReader`); `OPEN_FOLDER` opens the home's `logs/` in the file manager, a folder the page never names |

**`DAORIS.DRIVER` by domain.** A new door is a marked handler in its domain's partial and its name in
that domain's row here.

| Domain | What it carries |
|---|---|
| `driver` | The driver's state (`STATE`, with `drivingBrowser`, the running sessions handed a server that drives Daoris's browser, BRW8, and `inReview`, the set-ups waiting for the person here, each with whether Daoris serves its tab now, REVIEWENV1d) and tick reports; *Show it again* on the strip's review chip, the newest set-up's build served to its tab again (`SHOW_REVIEW_AGAIN` {quest}, REVIEWENV1d); the drivable set, holds and trees (`SET_DRIVABLE`, `SET_HOLD`, `SET_TREES`), a repository's standing answer, handed to every session there (`SET_STANDING` {repository, says}, no words clearing it, the `standing` `daoris driver standing` edits, KNOWUSE1b), notifications (`SET_NOTIFY`), the cool-off a limit naming no time takes (`SET_COOLOFF` {minutes}, the `cooloff` `daoris driver cooloff` edits, TOOL4g), strikes and *Try again*, which marks a quest the last look parked at the strike limit or releases one the person's stop holds and says which, refusing any other (`SET_STRIKES`, `RETRY_QUEST` {quest}, SESSUX1b, D126 §3.4), and a quest a pause holds with `QUEST_PAUSED` (PAUSE1b), the intake harness and Ask Daoris's agent (`SET_INTAKE`, `SET_HELPER`), trust (`TRUST_FOLDER`); `NUDGE` after a publish or an ask, and `SYNC_NOW` |
| `lines` | Each repository's line and how its work lands, as set and as they resolve (`LINES`, `SET_LINE`, `SET_LANDING`), and the language its sessions write to the person in, as it resolves and where it was set (`LINES`' `languages`; `SET_LANGUAGE` {repository or workspace, language}, no language clearing it, the `languages` `daoris driver language` edits, LANG1c; `STATE` answers what is set and the driver's table), where its work is reviewed before it lands, as it resolves and where it was set (`LINES`' `reviews`; `SET_REVIEW` {repository or workspace, then `put` {name, kind, procedure, address, run} with `required`, `none`, `drop`, `required` alone, or `clear`}, judged by the twins' table and an environment's procedure looked for in the checkouts it reaches, a put answering `reviewed` {lacking, unchecked}, the `reviews` `daoris driver review` edits, REVIEWENV1a; `STATE` answers what is set; the landing's gate reads it since REVIEWENV1c), which other agent reads its work before it lands, as it resolves and where it was set (`LINES`' `opinions`; `SET_OPINION` {repository or workspace, then `set` {reviewers, on, required, verify, minutes, recheck}, `none`, or `clear`}, judged by the twins' table, each rule answered with the bound of a pass and `sameAgent`, the reviewers of the working agent's own family, the `opinions` `daoris driver opinion` edits, XAGENT1a; `STATE` answers what is set; declared only, nothing reads it yet), and the clean-up of session and landed branches (`SWEEP_PLAN`, `SWEEP`), beside it a failed or superseded attempt's branch discarded by its name, its tree with it where it is still here, as `daoris-driver trees remove <branch> --repository <name> --force` does, a branch whose tree a session in use names kept (`DISCARD_SESSION_BRANCH` {repository, branch, force}, LAND3b; `SWEEP_PLAN`'s rows say `discardable` where the driver offers it), and bringing each repository up to date after a pull request merged (`TREES_SYNC_PLAN`, which fetches, and `TREES_SYNC`, WSR6), taking the repositories that hold Daoris's branches and the ones the person includes (`TREES_SYNC_SCOPE` says which, never fetching, D112) |
| `across` | Reading and writing across repositories (D107): how each repository's checkout stands, read by agents outside it or not and what said so, and what its sessions may also write into (`ACROSS`); a repository's or a workspace's reading set or cleared (`SET_READ_ACROSS`), and a relationship declared or taken back (`SET_WRITE_ACROSS`), in the `driver.json` `daoris driver across` edits |
| `console` | A session's live console (`TAIL_SESSION`, batched `SESSION_OUTPUT`) and the streams it runs beside itself, a subagent or a background task each (`SESSION_STREAMS`, and a `SESSION_STREAMS` event when one opens or ends), and a task stopped from its tab (`STOP_TASK`) |
| `conversation` | A session's record a page at a time (`SESSION_HISTORY`, batched `SESSION_EVENTS`, from the typed events kept beside each transcript), and a conversation (`START_CHAT`, `SESSION_INPUT` with files, `END_CHAT`, `CANCEL_TURN`, `SESSION_QUEUE`, and its model and effort, `SESSION_OPTIONS` and `SET_SESSION_OPTION`). `SESSION_INPUT` answers `{sent, reaches, why}` for every state, judged by `SessionWords` (MSG1d, D137 §5.3): a running session hears the words at its door (`next-step` or `turn-end`), a parked or ended one of this machine's has them kept on its record by the service's say door, shown at once in its conversation, and the loop nudged (`resume`), and words said as one winds up wait in the shell for its record to end, kept in the home's `sessions/held-words.json` so a restart in that moment loses none (MSG1d4); what never goes on is `sent: false` with its code (`teammate`, `intake`, `stood-down`, `help`, `superseded`, `not-found`). `SESSION_QUEUE` adds `reaches` and `why`, what a word said now would do, and `reach` {reaches, why}, the same as an object, which the live `SESSION_QUEUED` carries too (MSG1f2): at once where the session runs here, and read from its record once its door closes, once it moves through the loop's client, and, for a quest's earlier sessions, once a later one opens (`superseded`), so the page follows the box it offers live; an object because the bridge leaves a null out, so an older shell, which carries none, is told apart. `SESSION_START_FROM` {id} is *Start a conversation with these words* (MSG1f2, D137 §2.2): the words a session cannot go on with, and that nothing carries on by itself, are a new conversation's first message in its repository, with the files said beside them and a preface naming the session, then leave its record naming that conversation, its conversation saying where they went (`started`); it answers `{sessionId, sent, why, message}`, refusing what never goes on, a session still running (`running`), no words (`no-words`) and a quest that carries them on by itself (`carried`) before anything starts. `SESSION_GO_AHEAD` {id, ask, number, approved, words?} is a go-ahead a parked session asked, answered on its page (KNOWUSE1a2, D135 §2) through the ask's own door with `goesOn: true`, so the service answers the park only once none of the go-aheads it asked is open, as on the ask's page (GOAHEAD2b); the person's words are the go-ahead's and are never said to the park; a refused go-ahead answers nothing else. It answers the go-ahead's sentence beside `{sent, reaches, why, waits}`, read off the record: whether the park goes on, and whether it still waits on the person |
| `sessions` | Stop and resolve (`STOP_SESSION`, `RESOLVE_SESSION`), a conversation's first line and a search of what sessions said (`SESSION_OPENINGS`, `SESSION_SEARCH`), where each row's work is now, its tree or its landing (`SESSION_WHERE`, LOOK2b), each session's group by state, the word its row shows and its line's facts (whether its stop holds its quest among them, SESSUX1b; and, for an ended record the person's words wait on, *going on* under Working where the planner starts it, else Resumes later with `holds` {why, reason, repository, until}, what holds the words by the planner's verdict and the cool-off the loop's last look held its start on, MSG1f2), read by the driver library's `SessionGroups` from the records, the quests, the planner's verdicts, D88's proof of each ended session's own tree and the archive marks (`SESSION_GROUPS` {ids?}, SESSUX1a, D126 §2.4), and the archive marks under the home's `sessions/archived.json`, refusing a live session, one waiting on you or to review, and an id no record has (`SESSION_ARCHIVE` {ids, archived}, D126 §5.2), and *Open folder*, the folder a session worked in, its own tree or its repository's checkout, opened through the window kit's launcher, the module naming it from the record and refusing one this machine no longer holds (`SESSION_OPEN_FOLDER` {id}, `SESSION_FOLDER_GONE`, SESSUX1d, D126 §3.5), and *Delete…* of a conversation that served no quest, the ledger judging its record and the driver library's `SessionDeletion` its tree and landing before removing what this machine kept of it, answering by name what went and what the disk would not let go of (`stayed`, SESSDEL1), each refusal a code (`SESSION_DELETE` {id}, `SESSION_NOT_OURS`, `SESSION_SERVED_QUEST`, `SESSION_NAMED`, `SESSION_TREE_HERE`, `SESSION_ON_REMOTE`, SESSUX1f, D126 §5.4). `daoris-driver sessions` is the terminal's door to all of it (SESSUX1g, D126 §7.1): the listing from the same reader, `--json` this route's answer field for field, and a stop, finish or decline of a session the shell's loop runs reaching it through a request in the home's `sessions/requests/`, which the loop watches every second and acts on as `STOP_SESSION` and `RESOLVE_SESSION` do. `sessions say` (MSG1e, D137 §5.2) is a say request of its own in the same folder, which the watch hands its `Say` and answers beside it: the driver library's `LoopWords` by default, holding the words at a running session's inbox or keeping them on a record nothing here runs; the shell's loop hands `SessionWords.HoldAsync`, the box's judgement at the door `terminal`, so a conversation the window runs hears a terminal and words said as a session winds up wait in the shell (MSG1e2). *Go on in a new session* (`SESSION_GO_ON_NEW` {id}, MSG1g, D137 §2.2): words a resume holds while the account their session ran on cools go on in a new session at the loop's next look, handed them, without that conversation; the driver library's `GoOnNew` keeps the person's choice under the home and the loop is nudged, and what cannot is `{sent: false, why, message}` by its code (`not-found`, `teammate`, `help`, `intake`, `stood-down`, `superseded`, `running`, `no-words`, `conversation`, `closed`, `not-cooling`). `daoris-driver sessions go-on-new` is its terminal door; the page's press is MSG1g2's |
| `work` | The work of an ask or a quest and a pause of it on this machine (PAUSE1b, D132 §7.3), read by the driver library's `AskWork` and acted on by its `WorkPausing`: every quest, session, tree and landing of the work with what a pause would do with each, a tree by its repository and branch, never its path (`WORK_PLAN` {ask or quest}); a pause, written to `driver.json` first, then every live session of the work this machine runs stopped as the person's stop, one another Daoris process runs through the request folder with `by: pause`, a parked session, a running intake and a teammate's session left and named (`WORK_PAUSE` {ask or quest}); and a resume, which releases each stop the pause made and names what still holds each quest by the planner's verdict, with the facts the page words its sentence from, as the tick's (CARRY2d: the stop's session, whose pause, how many failed, whose take) (`WORK_RESUME` {ask or quest}). `WORK_PLAN` also answers the abandon's half (PAUSE1d, D132 §3), the first press read by `WorkAbandoning`: each piece's key and what an abandon would do with it (decline, stop, end, archive, discard, delete, close) or why it keeps it, a tree with its tip, its counts and up to five uncommitted files, the keys the second press sends back, and the last abandon from `<home>/abandoned.json`. The second press sends exactly those keys with the person's reason (`WORK_ABANDON` {ask or quest, reason, pieces}): each piece judged again, then the scope paused, its sessions stopped and ended, each quest declined with the reason (an open one with `whileOpen`), the ask closed, one sync pass per wired workspace through the loop's own, each tree and branch discarded behind `SessionTrees.OnlyHereAsync`, the sessions archived, and what went, what stayed, what changed since the list and each shared decline's answer said as facts. `WORK_UNKNOWN` names an ask or a quest this machine does not have, and `WORK_REASON` an abandon with no reason. `daoris-driver ask --pause|--resume|--abandon` and `quest pause|resume|abandon` are the terminal's door |
| `history` | Clearing finished history from this machine (HIST1c, D153; the history-clearing design §6.3), the driver library's `HistoryClearing` for both doors. The first press (`HISTORY_PLAN` {workspace}, {quest, failed?} or {ask}) asks the service's `GET /api/history`, then judges this machine's half of each unit the service would clear (a process still running, an automatic landing still trying, a tree still here, a landing's branch still standing), and answers each unit's ids, its bytes on the disk and, where it stays, its code; a workspace's adds the reading: its closed quests and asks, their sessions and bytes by kind, what a clear would take, the units kept by code, its conversations that served no quest, and the home's left-over files and log, never a path or a title. The second press (`HISTORY_CLEAR` {the same scope, units}) sends exactly the units the list held: each judged again, cleared by `POST /api/history/clear`, then every file the home kept of each cleared session removed through `SessionHomeFiles`, the helper `SESSION_DELETE` calls too, and what names it tidied: a landing's trace, an abandon's entry, `driver.json`'s marks and pauses for the quest or ask, an empty tree folder; a workspace's also takes the left-over files of records no store has, untouched for an hour, and its intake's room once it keeps no ask. What went comes back in counts and bytes, only what the disk let go of: a file it kept, the home's or a kept file the service names in `failed`, stays, is counted as failed and frees none of its bytes (HIST1j); with what changed since the list and stayed; the machine log gains `history.cleared`, counts only. Each reason is a code (`HISTORY_UNKNOWN`, `HISTORY_OPEN`, `HISTORY_ASKED`, `HISTORY_LIVE`, `HISTORY_NEEDS_YOU`, `HISTORY_AWAITED`, `HISTORY_TREE_HERE`, `HISTORY_LANDING_STANDS`, `HISTORY_UNPUSHED`, `HISTORY_NOT_OURS`) with which of its sentences as `context`; a clear of one quest, one ask or one quest's failed sessions that stays is refused in it. Nothing clears by itself, and no tree or branch is touched. Both answers are the driver library's `HistoryAnswers`, whose codes `Refusals` declares as its own (HIST1d), and `daoris-driver history`, `history clear --workspace`, `quest clear [--failed]` and `ask --clear` are the terminal's door: `history --json` prints `HISTORY_PLAN`'s answer field for field, and a clear's log line says `terminal` |
| `trees` | Review (`SESSION_DIFF`, and a tree discarded, `DISCARD_SESSION_TREE`; the merge alone was retired, LEFT2, since landing merges where the rule says merge), the files a composer's `@` offers (`SESSION_FILES`), one file read for its preview in the side bar, only inside the tree (`SESSION_FILE`, D111), landing (`LANDING`, `LAND_SESSION_TREE`) and a landed branch handed on (`HANDOFF_PLAN`, `HANDOFF`). A landed session's review and preview read its landed branch in the repository's checkout once its tree is gone (D113) |
| `help` | Ask Daoris's conversation in its room (`START_HELP`, the one running or a new one), and its proposals with the person's Apply or Not now (`HELP_PROPOSALS`, `HELP_APPLY`, `HELP_DISMISS`); its history, kept on this machine only (ASKHIST1): listed and searched (`HELP_CONVERSATIONS`), named and pinned (`HELP_RENAME`, `HELP_PIN`), and a new conversation started from an earlier one's words (`HELP_START_FROM`). Going on in one is `SESSION_INPUT`'s, and deleting one `SESSION_DELETE`'s |
| `accounts` | How each agent's accounts are used (TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2, §9, §16.6): each scope's list and settings, each account's cool-off, what its agent last said per window and when, its learned week and Daoris's sessions running on it, read from the files and never probed (`ACCOUNTS`); and the screen's edits, each refused as the terminal refuses it (`ACCOUNT_USE` {action: `order` a list written whole, `use` how it is used, `ready` *Try now*, `inherit` a workspace back on this machine's accounts}), over the `harnesses.json` and `cooling.json` `daoris agent profile order\|use\|ready` edit |
| `agents` | The toolchain (`HARNESSES`, `HARNESS_ACTION` relayed under `<harness>:<action>`, its input and cancel, `HARNESS_INPUT` and `HARNESS_CANCEL`), what a start would run on (`STARTS`), an account's own model and effort (`SET_AGENT_SETTINGS`), and usage (`USAGE`) |
| `plugins` | The catalogue and the install's offers (`PLUGINS`, each plugin with its servers, hook, the points its process listens on, health and whether an update waits), a plugin's page and its activity (`PLUGIN`, `PLUGIN_ACTIVITY`), enable, disable and remove (`PLUGIN_ACTION`), an update (`PLUGIN_UPDATE`), an offer installed (`PLUGIN_INSTALL`), a folder read and installed from (`PLUGIN_READ`, `PLUGIN_ADD`), a plugin's folders or the plugins folder opened (`PLUGIN_OPEN_FOLDER`), and the kit's `PLUGIN_NEW` and `PLUGIN_TRY` |
| `rules` | Permission rules and agents' proposals about them (`RULES`, `RULE_ACTION`, `RULE_PROPOSAL`) |
| `trace` | How a session or a quest came to be (TRACE1b, D143): `daoris-driver trace`'s read as data (`TRACE` {kind, id}), from the stores the terminal reads, each link with its store and a missing one with why; a tree by its folder's name, and no machine path |
| `registry` | A repository registered from what its line declares, the row's *Refresh* (`REGISTRY_REFRESH` {repository}, WSSETUP5): `daoris.json` and `daoris.lanes.json` read on its line as git objects and sent as `connect` would send them, only where the row holds something else, answered with the outcome's word and the sentence the row says; a refusal is an answer. `daoris-driver register` is the terminal's door. The row and its button are WSSETUP7's |
| `tools` | Settings → Tools (TOOLS7, D121 §4.1): each tool's way, its file, the versions downloaded and those the lists offer for this machine, the locations and the list built in (`TOOLS_LIST`, with `ask` the version each file answers), a way written to the `tools.json` `daoris tool` edits (`TOOLS_USE`: the system's, a named file that answers a version, or managed), a download (`TOOLS_DOWNLOAD`) answered once started, relayed under `tools:<tool>` and ended by `TOOLS_ENDED`, and stopped (`TOOLS_STOP`), a version deleted (`TOOLS_DELETE`), the locations looked at, added and removed (`TOOLS_LOOK`, `TOOLS_LOCATION`), what a switch of git changes (`TOOLS_GIT`), and the system's file picker (`TOOLS_PICK`) |

These doors are the shell's alone: machine-local facts never reach a browser (D47 §4), so none of
them is an HTTP route. A session's *record* is on the host; its console, events and diff are here.

## What the window must keep

- **It is frameless** (SURF7, D56): the platform's app strip is the title bar, and the room it
  reserves is handed to the OS as real caption buttons, which the window paints from
  `ChromePalette`'s copy of D41's tokens. 🔴 `AppPlacement` is the truth about maximized, never
  `Form.WindowState`, which lies about a window that maximizes by hand. `WindowCommandModule` is
  mapped **late**, from the form's constructor, because it needs a live form.
- **Secondary windows** (SURF8) carry the same bundle at their own route, so they are the platform's
  own components and not a second frontend. They keep their **native frame**; each is a
  `ChromiumView` on the one engine the app registers (D92), and its caption follows the page's
  chosen theme through `DAORIS.WINDOWS SET_THEME` (WINDOW2), the kit's own having no route for a
  framed window; and they are disposed on shutdown rather than abandoned, because their threads are
  background and an unwaited exit kills them before their geometry is saved.
- **It notifies, and decides nothing** (SURF5b): a session that parks, or ends without the person
  asking, raises an OS balloon unless one of its windows has focus. `AttentionWatch` in the library
  makes the judgement, so `daoris-driver` prints the same one as a line.
- **It brings up the local host**, adopting one already running or spawning and owning one, and runs
  the driver's watch loop in-process with `driver.json` re-read every tick. On close it takes the
  loop and its owned host down, with in-flight sessions ended and recorded `stopped` and
  `interrupted`, so a take one held is carried on at the next start, as a cut-off is (D104).
- **One live driver per home** (DRV8a, D104). The loop takes `<home>/driver.lock` (`DriverLock`: its
  kind, process id and start time, and since when) before it ticks, as a terminal's `daoris-driver
  drive` does. Where a headless loop holds it, the window says so once on `DRIVER_ERROR` and its loop
  starts the moment that lock frees; the host, the page and the conversations carry on meanwhile.
- 🔴 **A diff confirms the tree it was given** (`rev-parse --show-toplevel`): git searches upward, and
  would otherwise answer for the repository above it.

## What it is

The desktop application a person runs to **drive the family**: it hosts the local service, carries the
platform UI, and **controls the repositories and their agent sessions** — spawning, monitoring and
coordinating development sessions (Claude Code, Codex, dsh, through an adapter seam) one per domain-owning
repository. Built on the family's desktop runtime sibling, consumed at a released version (D22).

## Installing it (2026-09-22)

`npm run publish:desktop -- --to <folder> --service` publishes the application (D93): `Daoris.exe`, a
small launcher, at the folder's root; the application in `app/` beside its Chromium (its files listed in
`app/shell-files.txt`, which the next publish removes before placing its own), which is Daoris's
browser too (CHR8: the `app/daoris-browser/` an older publish wrote is removed by name), and both
service hosts, each replaced whole by `service-publish`'s one recipe: the HTTP host with its bundle
under `app/daoris-knowledge-http/`, and **the knowledge connector** under `app/daoris-knowledge/`,
which the driver hands every protocol-door session ahead of the home's `bin/` copy (CONNECTOR1: an
install that carried none handed its sessions a `bin/` copy no republish refreshed, eight days old on
the install that found it, and it rebuilt the shared store at its own older schema); **the doctrine tool** (WSSETUP2,
D124 §1.2), the CLI package the release packs laid out as npm lays one out in
`app/cli/node_modules/daoris/`, with a launcher for each shell in `app/bin/` (`daoris` for Git Bash,
`daoris.cmd` for Command Prompt and PowerShell), run on the `node` a child's `PATH` finds; the
install's own `data/` once it has run, and an `INSTALLED.md` saying so. **Every process the driver and
the modules start finds that `daoris` first** (WSSETUP3, D124 §1.3): the tools' environment puts the
`app/bin/` beside the home ahead of the tools' folders, so a session, a conversation, a hook and the
terminal's shells run the install's own doctrine tool by its bare name. The account's `PATH` is never
touched, so a terminal of the person's own still runs whatever `daoris` they installed. **`data/` is the Daoris
home** (D63): on first start the shell sets `DAORIS_HOME` to it for its own process — every host and
session it spawns inherits it — and, once, for the account when it has none, so a terminal's `daoris`
meets the same machine. A second or moved install runs on its own `data/` even when the account's
variable names another folder, leaves that variable as it is, and says so on Settings' home row
(D105). Nothing of Daoris's lives under the user profile; a `~/.daoris` from before
the decision moves in on that first start, `bin/` excepted, and the shell says so once. **Starting it
starts the driver loop.** The publish refuses a folder it did not
write; `--beside` installs next to whatever is there — the repositories it drives, typically — and
still refuses to write over a name it did not write. `npm run desktop -- run --install <folder>`
starts that install with the debug port attached, so the instruments below reach it.
**Pin it from its running button** (D108): an install's windows name Daoris's taskbar id and a
relaunch command that starts `Daoris.exe` at the root, so that pin is the launcher and every later
window joins it. A pin made on `Daoris.exe` in Explorer carries no id, and Windows cannot relate the
window to it. A workspace build's window names nothing and keeps its own button.
`docs/2026-09-22-first-deployment-case-study.md` is what deploying found, and `docs/FIX-LOG.md`
what deploying again found.

## Plugins (D64, 2026-09-23)

A plugin is a folder under the home's `plugins/` with a `plugin.json` (`docs/2026-09-23-plugin-design.md`).
The shell reads the catalogue when it starts and on every tick: a **declared harness** joins the
roster and the adapter set as a configuration of the ACP door, and a plugin that **speaks** gets a
process of its own, started with the loop and stopped with it. The driver asks those processes at
its points — `quest/consider` before a start costs anything (a hold is the quest's own sitting
reason, and a plugin that cannot decide holds too, naming itself), `session/ended` after — and
every line one writes reaches the console under `plugin:<id>`, over the bridge and nowhere else.
`daoris plugin list|add|update|remove|enable|disable` is the terminal door; the Settings page's Plugins
card is the other — the same rows, the same `plugins.json`, Remove naming what a plugin kept. Since
PLUG9 (c, d; D103) each row carries its `source` (a folder, the install's offer, or none recorded, from
`.daoris-source.json` in its install folder); `PLUGIN_UPDATE` {id} answers what an update would change or
why it cannot, and `{id, apply: true}` swaps the folder in, the hook stopped first. `PLUGINS` also answers
`offers`, Daoris's own plugins the install carries in `app/plugin-offers/` (found beside this
executable, else beside the home), each with its README's requirement lines, and `PLUGIN_INSTALL`
{offer} copies one in. None runs at the press.
**No plugin code ever runs inside the shell, the host or the page.**

**The kit a plugin is made with** (PLUG8, D101, the plugin design's §9) is the driver library's
`PluginKit`: `daoris-driver plugins new <id> --point <p>… [--in <folder>]` writes a plugin's folder
(a manifest, a wire script, a wire test `node --test` runs with no Daoris, a README) from templates
embedded in the library (`plugin-kit/*.template`), and `daoris-driver plugins try <folder|id>` starts
a plugin as the driver would and checks every answer with the driver's own reader. On the page,
`DAORIS.DRIVER`'s `PLUGINS` answer carries `kit.points`, `PLUGIN_NEW` {id, points, folder} writes the
folder, and `PLUGIN_TRY` {id} or {folder} answers the trial's steps, summary and stderr lines.

**A plugin package** (PLUGDIST1a, D120) is read by the driver library's `PluginPackage`, with no NuGet
client and no network. `daoris-driver plugins install <file.nupkg>` judges the package before anything is
extracted: the type `DaorisPlugin` alone at a plugin API this build speaks, no dependencies,
`plugin/plugin.json`, and every `plugin/` entry inside the plugin's folder. It then extracts `plugin/`
alone into a stage under the home, reads it as the catalogue reads a plugin, and adds it under its id,
never over an installed one, with `{ package, version, sha512, source }` recorded in
`.daoris-source.json`. A package source over HTTP is PLUGDIST1c's.

**What a plugin did, and its health** (PLUGUI1d, D119). What the loop, a landing, a hand-off, the driver's
handing of servers and the trial at either door do with a plugin is a `plugin.*` line in the machine log, never its
words (`PluginLog`). The loop's `PluginHealth`, the shell's `DriverLoop.Health`, keeps each plugin's state and is
handed to the hook set and to every landing the routes build. `daoris-driver plugins show <id>` and
`plugins activity <id>` read through `PluginPage.Read` and `PluginActivity.Read`, the readers the page's routes
call; a terminal reads a plugin's health from the machine log's last word, and says so.

**The host answers the Plugins view** (PLUGUI1e, D119 §4.1). `PLUGINS` carries, per plugin, its `servers`, its
`hook` (the command as its manifest writes it, and its points), `listening`, `health` (`state`, `since`, `failure`)
from the loop's own record, and `update` (`waits`, `current`, or null with no record). `PLUGIN` {id} is
`PluginPage.Read` with the loop's health, and `PLUGIN_ACTIVITY` {id, since?} is `PluginActivity.Read` over a span
such as `1d`, `7d` or `30d` (7 days without one); both refuse an id no longer here as `PLUGIN_UNKNOWN`.
`PLUGIN_READ` {folder} answers what a folder's plugin would run, or the refusal as an answer, judged as Ask
Daoris's judge judges a folder and refusing an id already installed; `PLUGIN_ADD` {folder} copies it in, never
over one installed. `PLUGIN_OPEN_FOLDER` {id?, which: install|data|plugins} opens the folder the module names
through the window kit's launcher (`PLUGIN_NOTHING_KEPT` for a data folder never made, `PLUGIN_FOLDER_NOT_OPENED`
when the system will not). The screen's trial of an installed plugin is a `plugin.tried` line from the `screen`
door, and the loop's conversations write a `plugin.served` line for each plugin server they are handed or are
not, as its driven sessions do (`daoris-driver chat` writes none, as it writes no session lines).

## The dev loop — `tools/desktop.mjs` (2026-09-21)

Everything else here has a loop that can see it. The shell had none: Playwright cannot reach it (the
bridge is absent in a browser, by construction) and the vitest suite drives a *mock* of this machine,
so the only way to look at the real window was to open it by hand. `npm run desktop -- <command>` is
that instrument. **It is not a gate** — it starts nothing in CI and asserts nothing — it is how a
person or an agent starts the shell and sees what it actually rendered.

**The gate is `npm run rehearse:deploy`** (D60), and it is a different question: this loop runs what
the workspace built, and that gate publishes the shell to a scratch folder and drives the **artefact**
— a window that finds its host without being told where it is, a session transcript compared as
bytes, and a conversation open when the window closes, whose record must carry the close's own note
(DEPLOY5: the gate starts the install with `run --install`'s debug port to open it over the bridge),
and whose transcript must name the connector the install carries rather than the home's `bin/` copy
the gate plants beside it (CONNECTOR1).
Its phase 8 runs the doctrine tool the install carries (WSSETUP2): with the install's `app/bin/` first
on `PATH`, `daoris` by its bare name from Command Prompt, PowerShell and Git Bash where the runner has
one, prints the canon's version, and `init`, `sync` and `check` run clean in a scratch repository.
Everything this loop provides is what hid two of the first deployment's four defects.

| command | what |
|---|---|
| `doctor` | what is built, what is running, what a scratch run would use — and whether an installed host would be adopted instead of this workspace's |
| `build [--release]` | the platform bundle into the host's `wwwroot`, then the host, then the shell (which is the browser too). That order is the dependency order: a host built before the bundle serves the previous one |
| `run [--real] [--fresh]` | start the shell **on a machine of its own**, with the debug port attached |
| `run --install <dir>` | start the **DEPLOYED** shell in that folder, on its own `data/` home, with the debug port attached. 🔴 The published application opens no port — this launch does, through the environment, which is the deliberate opt-in the first deployment asked for and did not build (case study 2d). `shot`, `eval`, `click` and `kill` then address that install, because they follow the run file rather than this checkout |
| `restart` · `kill` | stop the shell **this checkout built** — matched by executable path, never by process name, and never the engine's `--type=` processes or the browser (`--daoris-browser` first), which run from the same path and follow the shell out |
| `shot [name] --page [--size WxH] [--theme light\|dark]` | capture the **page** over the debug port instead of the window (SESS1): a minimized window photographs as its 314 × 50 caption, and restoring an installed one puts it in front of its owner. `--size` lays the page out at that size for the capture and puts it back. The native frame is not in it |
| `shot [name] [--theme light\|dark] [--window <name>]` | capture the window into `_fixtures/desktop/screenshots/` (PrintWindow + `PW_RENDERFULLCONTENT`, so the engine's composition is in it). `--theme` photographs the OTHER theme without touching the machine's setting — a media-query emulation over the debug port, which makes the page push `SET_THEME` and the **main** window repaint its native chrome (DWM border, caption buttons) for real: the only way to see that chrome in both. The page follows the emulation only while the viewer's theme choice is System, so a viewer who chose light or dark has theirs set for the capture and put back after it, even when the capture fails; a page still not in the theme is refused, never photographed (LOOK1). `--page` takes `--theme` the same way. A **secondary** window's page tells its own caption the theme (WINDOW2), so `--theme` repaints that too. 🔴 `--window monitor`, `--window browser` (D78; found by its process id since CHR8, being the application's executable too) or `--window session:<id>` since SURF8: without it the capture takes whichever window **Windows** calls main, which with a secondary window open is not the caller's choice |
| `eval [--window <name>] ("<js>" \| --file <script.js>)` | evaluate inside one of the running shell's pages — **the only instrument that sees the bridge-attached half** (the Settings page, the driver controls, the console, chat). `--window` picks a secondary window's page (SURF8); without it, the application's own. `--file` evaluates a script file whole (UX6a): the screen counter's (`tools/ux-count.mjs --window`) is longer than the line `npm run` hands cmd.exe, and its quotes would not survive the shells between |
| `click [--window <name>] "<css>"` | click exactly one element, and say what it clicked; a selector matching none or several is a refusal, not a first match |

**A native window's controls are pressed through UI Automation, not typed at** (BRW4, 2026-09-28).
Keystrokes and clicks synthesized from an agent's terminal (`SendKeys`, `keybd_event`,
`mouse_event`) did not reach the shell's windows at all, not even the address bar. UI Automation's
Invoke, scoped to the scratch shell's process id, pressed every button. A shortcut over a page is
then read from the page's own code rather than pressed; a page's own controls take CDP input (`Input.dispatchMouseEvent`), which a Radix menu needs where a scripted `.click()` does nothing. Scope anything that presses or
types to the scratch shell's pid, never to a caption: the owner's install opens windows of the same
names.

**A dev run gets its own machine, and that is a safety property rather than a convenience.** The shell
runs the driver loop, and the driver spawns **real agent sessions in real repositories**. So `run`
redirects the home and every machine-local file under it, clears the remote environment pair
(inherited, it would feed a real deployment from a scratch store), takes a port of its own, passes
`--app-root` so the engine's profile and window state are its own too, and copies `examples/` to work
over. `--real` is the person's own Daoris — the home their `DAORIS_HOME` names — and is spelled out
for that reason. A test asserts the redirect list against the sources that build paths under the
home, because a name missing there does not fail — it edits the person's real config.

Two couplings the tool holds that nothing else does, both found by running it: the debug port needs
**`DOTNET_ENVIRONMENT=Development`** as well (the engine opens `DAORIS_DEVTOOLS_PORT` only in
development, which is why a shipped window has nothing to attach to — D78 §3.1 by construction), and the scratch port needs **`ASPNETCORE_URLS`** as
well (the shell *probes* `DAORIS_SERVICE_URL` while the host *binds* `ASPNETCORE_URLS`, and nothing
passes one to the other — move only the probe and the window waits on the splash forever).
And a port asked for is not a port opened (LOOK4): the engine runs on without its port when it cannot bind
it, as on a port Windows reserves, which answers nothing and so looked free. So `run` picks a port it has
bound on both loopbacks, waits for the port to answer, and says when the window started without it.

Captures and the scratch machine live under `_fixtures/`, which is gitignored: a window capture can
show real repository content, so it never enters a tracked file (`sensitive-info`). Every capture
prunes afterwards — newest 25, at most 150 MB — because a full-resolution PNG is megabytes and a
capture folder nobody measures grows until somebody does.

## The loop it exists to run (D45)

A target becomes a quest → the driver starts (or wakes) the owning repository's agent session with that
quest as its target → the session works inside its own repository, under its own gates (D37's middle) →
done or declined flows back through the service → the person verifies outcomes in the platform. The
quest queue becomes an execution queue; the Overview's "is anything sitting" becomes "is anything
sitting that the driver should have started".

## What it must preserve

- **`repository-owns-its-work` is the reason for the shape, not a constraint on it.** Sessions run
  *inside* each repository as that repository's own agent; the driver orchestrates and never reaches
  across. Quests remain the only transfer.
- **D37's carve-outs**: the person sets targets and verifies outcomes; destructive, irreversible,
  cross-repository and publishing actions stay human — and the driver's UI is where those checkpoints
  surface.
- **One UI.** The platform (`Daoris.Web`) is the interface; this shell hosts the same build and adds
  the session-control surface, not a second UI.

## The design, settled (DRV1, 2026-09-19)

`docs/2026-09-19-driver-design.md` answers the brief's five questions; the short form:

1. **Spawn, not wake** — a fresh non-interactive session per quest, one active session per repository,
   onto a clean tree only; the spawned session claims its own quest through its own connector, so the
   driver never writes quest state and outside sessions stay first-class (driving is additive, never
   exclusive — D46).
2. **The session lifecycle** is observed, not self-reported — process lifetime plus quest transitions —
   and its records live in the service beside the quests; processes and transcripts stay here.
3. **The adapter seam** is D23 one layer up: claude-code supported, unknown adapters error naming
   what exists; an adapter names a harness, never a model. Since D49 §4 it also declares that harness
   **as a tool** — and managing a tool is a different question from spawning sessions on it, which
   is why `codex` is manageable from `daoris agent` while only its protocol door, `codex-acp`,
   spawns sessions (D53).
4. **The service holds state, the driver holds action**: session records and the machine-local
   repository root go to the service; the scheduler, process control, adapters, driver config and
   notifications live here.
5. **The desktop sibling is consumable at a released version** — v0.16.0 (`Shenora.Windows` +
   `@shenora/react`), checked 2026-09-19 — so there is no coordination blocker.
