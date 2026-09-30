# Daoris.Desktop — the local driver

**Status: built — the driver, its headless host and the shell all exist, and the family rehearsal
drives them end to end.** `docs/2026-09-19-driver-design.md` (D46) is the contract; how each part
got its shape is in `docs/DECISIONS.md` and `docs/task-archive.md`, not here.

| Project | What it is |
|---|---|
| `Daoris.Desktop.Driver` | The library: the loop and the planner; the adapter seam, whose `Toolchain` declares a harness's binary, version question, configuration home and install, update and login flows (D49 §4), and whose `Wire` is the door a session is held over — the pipe, or ACP (`AcpSession`, with a permission request refused by construction); `ChatRunner` for conversations; `HarnessRoster` (is the harness here, and which account does this run as); and `RemoteSync`, which rides the tick once per workspace |
| `Daoris.Desktop.Driver.Host` | `daoris-driver`, the headless door onto the same library: the loop by its verb, `drive [--once \| --until-idle] [--share]` (DRV8a, D104: with no verb it prints its usage and exits 2, and a loop refuses a home another live driver holds, naming it, unless `--share`), chat, ask, trees and sync, deleting a quest or an ask made by mistake (`quest delete`, `ask --delete`, D95), and the machine log read back (`logs [--since <30m\|2h\|3d>] [--source <name>] [--event <name>] [--level <warn\|error>] [--json]`, LOG1c): every source's files under the home merged by time, one readable line each or as written with `--json`, a line that cannot be read skipped and counted on standard error |
| `Daoris.Desktop.Modules` | The shell's head: the loop, the host supervisor, every IPC module the page talks to, and `Refusals`, where a refusal is declared once as a code the page translates. Plain `net10.0`, with its own tests and gate |
| `Daoris.Desktop.App` | `Daoris.Desktop.exe`, the window, on Shenora 0.18's own Chromium (`ChromiumView`, D92): CEF's launcher, laid out beside the engine by the build, wearing Daoris's icon and name, which the kit copies from the app's assembly (SHEN1). **And `daoris-browser`, Daoris's own browser** (D85, CHR3, since CHR8 D99): the same executable started with `--daoris-browser` first, which `Main` hands to the kit's `ChromiumBrowserProcess.Run` before anything else (`BrowserProcess`). A process of its own because the engine's debug port reaches every page in its process; it shows Chromium's own window, made over its port, and answers `${browser}` on that port, a relay of the kit's that calls a new tab a `page`. It ends when its last window closes or the shell does. The shell starts it with `ChromiumBrowserProcess.Start` (`EngineBrowserHost`), so an install carries one Chromium |
| `Daoris.Desktop.Launcher` | `Daoris.exe`, the one thing at an install's root (D93): it starts `app/Daoris.Desktop.exe` with its arguments and exits, its process carrying Daoris's taskbar id (D108). Framework-dependent, single-file, referencing nothing |

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
| `DAORIS.TERMINAL` | The person's own shells, for the terminal view (CONSOLE4, D96): `SHELLS` (what this machine has on PATH, `pwsh` the default, else `powershell`, then `cmd` and Git Bash), `OPEN` {shell?, cwd?, cols?, rows?} a shell under a Windows pseudo-console (`PseudoConsole`) with `DAORIS_HOME` in its environment, in the folder asked for or else the home, `INPUT`, `RESIZE` and `CLOSE`, with its output as batched `TERMINAL_OUTPUT` events and `TERMINAL_EXITED` when a shell ends on its own. Each shell's job ends everything it started with its tab, and every terminal ends with the application. Nothing typed or printed reaches the machine log |
| `DAORIS.LOG` | The page's report into the machine log (LOG1b, D94): `EVENT` with `{ event, data }`, kept only as the catalogue names it (a view opened, a command run, a view moved, a message's length and file count, a proposal settled, a caught error), each field in its kind, everything else dropped. Every refusal any module answers is logged beside it by its code (`RefusalLog`, a middleware in the dispatcher). And Settings → Logs reading it back (LOG1c): `LINES` {since?, source?, event?, level?, limit?} answers the newest lines first (200, at most 1000) with the folder, how many matched, a count per level, the period's events and the lines skipped, filtered here by the reader `daoris-driver logs` uses (`MachineLogReader`); `OPEN_FOLDER` opens the home's `logs/` in the file manager, a folder the page never names |

**`DAORIS.DRIVER` by domain.** A new door is a marked handler in its domain's partial and its name in
that domain's row here.

| Domain | What it carries |
|---|---|
| `driver` | The driver's state (`STATE`, with `drivingBrowser`, the running sessions handed a server that drives Daoris's browser, BRW8) and tick reports; the drivable set, holds and trees (`SET_DRIVABLE`, `SET_HOLD`, `SET_TREES`), notifications (`SET_NOTIFY`), strikes and a retry (`SET_STRIKES`, `RETRY_QUEST`), the intake harness and Ask Daoris's agent (`SET_INTAKE`, `SET_HELPER`), trust (`TRUST_FOLDER`); `NUDGE` after a publish or an ask, and `SYNC_NOW` |
| `lines` | Each repository's line and how its work lands, as set and as they resolve (`LINES`, `SET_LINE`, `SET_LANDING`), and the clean-up of session and landed branches (`SWEEP_PLAN`, `SWEEP`), and bringing each repository up to date after a pull request merged (`TREES_SYNC_PLAN`, which fetches, and `TREES_SYNC`, WSR6), taking the repositories that hold Daoris's branches and the ones the person includes (`TREES_SYNC_SCOPE` says which, never fetching, D112) |
| `across` | Reading and writing across repositories (D107): how each repository's checkout stands, read by agents outside it or not and what said so, and what its sessions may also write into (`ACROSS`); a repository's or a workspace's reading set or cleared (`SET_READ_ACROSS`), and a relationship declared or taken back (`SET_WRITE_ACROSS`), in the `driver.json` `daoris driver across` edits |
| `console` | A session's live console (`TAIL_SESSION`, batched `SESSION_OUTPUT`) and the streams it runs beside itself, a subagent or a background task each (`SESSION_STREAMS`, and a `SESSION_STREAMS` event when one opens or ends), and a task stopped from its tab (`STOP_TASK`) |
| `conversation` | A session's record a page at a time (`SESSION_HISTORY`, batched `SESSION_EVENTS`, from the typed events kept beside each transcript), and a conversation (`START_CHAT`, `SESSION_INPUT` with files, `END_CHAT`, `CANCEL_TURN`, `SESSION_QUEUE`, and its model and effort, `SESSION_OPTIONS` and `SET_SESSION_OPTION`) |
| `sessions` | Stop and resolve (`STOP_SESSION`, `RESOLVE_SESSION`), and a conversation's first line and a search of what sessions said (`SESSION_OPENINGS`, `SESSION_SEARCH`) |
| `trees` | Review (`SESSION_DIFF`, and a tree discarded, `DISCARD_SESSION_TREE`; the merge alone was retired, LEFT2, since landing merges where the rule says merge), the files a composer's `@` offers (`SESSION_FILES`), one file read for its preview in the side bar, only inside the tree (`SESSION_FILE`, D111), landing (`LANDING`, `LAND_SESSION_TREE`) and a landed branch handed on (`HANDOFF_PLAN`, `HANDOFF`) |
| `help` | Ask Daoris's conversation in its room (`START_HELP`, the one running or a new one), and its proposals with the person's Apply or Not now (`HELP_PROPOSALS`, `HELP_APPLY`, `HELP_DISMISS`) |
| `agents` | The toolchain (`HARNESSES`, `HARNESS_ACTION` relayed under `<harness>:<action>`, its input and cancel, `HARNESS_INPUT` and `HARNESS_CANCEL`), what a start would run on (`STARTS`), an account's own model and effort (`SET_AGENT_SETTINGS`), and usage (`USAGE`) |
| `plugins` | The catalogue and the install's offers (`PLUGINS`), enable, disable and remove (`PLUGIN_ACTION`), an update (`PLUGIN_UPDATE`), an offer installed (`PLUGIN_INSTALL`), and the kit's `PLUGIN_NEW` and `PLUGIN_TRY` |
| `rules` | Permission rules and agents' proposals about them (`RULES`, `RULE_ACTION`, `RULE_PROPOSAL`) |

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
browser too (CHR8: the `app/daoris-browser/` an older publish wrote is removed by name), and the
service host with its bundle under `app/daoris-knowledge-http/`; the
install's own `data/` once it has run, and an `INSTALLED.md` saying so. **`data/` is the Daoris
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
(DEPLOY5: the gate starts the install with `run --install`'s debug port to open it over the bridge).
Everything this loop provides is what hid two of the first deployment's four defects.

| command | what |
|---|---|
| `doctor` | what is built, what is running, what a scratch run would use — and whether an installed host would be adopted instead of this workspace's |
| `build [--release]` | the platform bundle into the host's `wwwroot`, then the host, then the shell (which is the browser too). That order is the dependency order: a host built before the bundle serves the previous one |
| `run [--real] [--fresh]` | start the shell **on a machine of its own**, with the debug port attached |
| `run --install <dir>` | start the **DEPLOYED** shell in that folder, on its own `data/` home, with the debug port attached. 🔴 The published application opens no port — this launch does, through the environment, which is the deliberate opt-in the first deployment asked for and did not build (case study 2d). `shot`, `eval`, `click` and `kill` then address that install, because they follow the run file rather than this checkout |
| `restart` · `kill` | stop the shell **this checkout built** — matched by executable path, never by process name, and never the engine's `--type=` processes or the browser (`--daoris-browser` first), which run from the same path and follow the shell out |
| `shot [name] --page [--size WxH]` | capture the **page** over the debug port instead of the window (SESS1): a minimized window photographs as its 314 × 50 caption, and restoring an installed one puts it in front of its owner. `--size` lays the page out at that size for the capture and puts it back. The native frame is not in it |
| `shot [name] [--theme light\|dark] [--window <name>]` | capture the window into `_fixtures/desktop/screenshots/` (PrintWindow + `PW_RENDERFULLCONTENT`, so the engine's composition is in it). `--theme` photographs the OTHER theme without touching the machine's setting — a media-query emulation over the debug port, which makes the page push `SET_THEME` and the **main** window repaint its native chrome (DWM border, caption buttons) for real: the only way to see that chrome in both. A **secondary** window's page tells its own caption the theme (WINDOW2), so `--theme` repaints that too. 🔴 `--window monitor`, `--window browser` (D78; found by its process id since CHR8, being the application's executable too) or `--window session:<id>` since SURF8: without it the capture takes whichever window **Windows** calls main, which with a secondary window open is not the caller's choice |
| `eval [--window <name>] "<js>"` | evaluate inside one of the running shell's pages — **the only instrument that sees the bridge-attached half** (the Settings page, the driver controls, the console, chat). `--window` picks a secondary window's page (SURF8); without it, the application's own |
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
