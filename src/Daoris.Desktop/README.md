# Daoris.Desktop — the local driver

**Status: built — the driver, its headless host and the shell all exist, and the family rehearsal
drives them end to end.** `docs/2026-09-19-driver-design.md` (D46) is the contract; how each part
got its shape is in `docs/DECISIONS.md` and `docs/task-archive.md`, not here.

| Project | What it is |
|---|---|
| `Daoris.Desktop.Driver` | The library: the loop and the planner; the adapter seam, whose `Toolchain` declares a harness's binary, version question, configuration home and install, update and login flows (D49 §4), and whose `Wire` is the door a session is held over — the pipe, or ACP (`AcpSession`, with a permission request refused by construction); `ChatRunner` for conversations; `HarnessRoster` (is the harness here, and which account does this run as); and `RemoteSync`, which rides the tick once per workspace |
| `Daoris.Desktop.Driver.Host` | `daoris-driver`, the headless door onto the same library: the tick, chat, ask, trees and sync |
| `Daoris.Desktop.Modules` | The shell's head: the loop, the host supervisor, every IPC module the page talks to, and `Refusals`, where a refusal is declared once as a code the page translates. Plain `net10.0`, with its own tests and gate |
| `Daoris.Desktop.App` | `daoris-desktop`, the window and only the window, on Shenora.Windows 0.16.0 (D22) |
| `Daoris.Desktop.Browser` | `daoris-browser`, Daoris's own browser (D85, CHR3): Chromium through CefSharp, in a process of its own because the engine's debug port reaches every page in its process. It shows the engine's own window, which it opens over the engine's own port, and answers `${browser}` with a relay (`CdpRelay`) that calls a new tab a page where the engine says `other`. It ends when its last window closes or the shell does. The shell starts it; an install carries it under `app/daoris-browser/` with two locales |

The adapters are the stub, `acp-stub` (the protocol door with no model in it), `claude-code`, and the
protocol door's configurations `claude-code-acp`, `codex-acp` and `dsh`. A session's record is
concluded from its exit code and its quest, never from what it said.

## What the page can ask this machine

| Module | What it carries |
|---|---|
| `DAORIS.DRIVER` | The driver: its state and tick reports; the drivable set, holds, trees, strikes, notifications and the intake harness; trust and retry; a session's live console (`TAIL_SESSION`, batched `SESSION_OUTPUT`) and the streams it runs beside itself, a subagent or a background task each (`SESSION_STREAMS`, and a `SESSION_STREAMS` event when one opens or ends), its record a page at a time (`SESSION_HISTORY`, batched `SESSION_EVENTS`, from the typed events kept beside each transcript), a conversation (`START_CHAT`, `SESSION_INPUT` with files, `END_CHAT`, `CANCEL_TURN`, `SESSION_QUEUE`), Ask Daoris's conversation in its room (`START_HELP`, the one running or a new one), stop and resolve, review (`SESSION_DIFF`, merge or discard a tree), the files a composer's `@` offers (`SESSION_FILES`), and a conversation's first line and a search of what sessions said (`SESSION_OPENINGS`, `SESSION_SEARCH`); the toolchain (`HARNESSES`, `HARNESS_ACTION` relayed under `<harness>:<action>`, its input and cancel); plugins, permission rules and proposals, usage; `NUDGE` after a publish or an ask, and `SYNC_NOW` |
| `DAORIS.REGISTRY` | A folder picked and inspected; an existing manifest's declaration written, uncommitted, for that repository's review |
| `DAORIS.REMOTES` | The machine's `remotes.json`, the file `daoris remote` edits: a key goes in, and only its audit prefix comes back |
| `DAORIS.BROWSER` | Daoris's browser's two files under the home, as Settings → Browser: `favorites.json` (`ADD_FAVORITE`, `REMOVE_FAVORITE`), shown in a Daoris folder on its bookmarks bar, and `settings.json` (`SET_EXTENSIONS`: other software's Chrome extensions offered or refused). The same files `daoris browser` edits, read by `daoris-browser` at each start |
| `DAORIS.WINDOWS` | Named secondary windows: `monitor` and `session:<id>`; and `OPEN_BROWSER`, Daoris's own browser (D78, D85): `daoris-browser` started, or its window brought forward. Its profile is under the home at `browser/engine`, with no bridge, and a loopback CDP port that a plugin's browser MCP attaches to |

These doors are the shell's alone: machine-local facts never reach a browser (D47 §4), so none of
them is an HTTP route. A session's *record* is on the host; its console, events and diff are here.

## What the window must keep

- **It is frameless** (SURF7, D56): the platform's app strip is the title bar, and the room it
  reserves is handed to the OS as real caption buttons, which the window paints from
  `ChromePalette`'s copy of D41's tokens. 🔴 `AppPlacement` is the truth about maximized, never
  `Form.WindowState`, which lies about a window that maximizes by hand. `WindowCommandModule` is
  mapped **late**, from the form's constructor, because it needs a live form.
- **Secondary windows** (SURF8) carry the same bundle at their own route, so they are the platform's
  own components and not a second frontend. They keep their **native frame**; each builds its **own
  WebView2 environment** (🔴 an environment is affine to the thread that created it, and sharing the
  main window's fails the bring-up); each follows the OS theme directly, having no `SET_THEME`
  channel; and they are disposed on shutdown rather than abandoned, because their threads are
  background and an unwaited exit kills them before their geometry is saved.
- **It notifies, and decides nothing** (SURF5b): a session that parks, or ends without the person
  asking, raises an OS balloon unless one of its windows has focus. `AttentionWatch` in the library
  makes the judgement, so `daoris-driver` prints the same one as a line.
- **It brings up the local host**, adopting one already running or spawning and owning one, and runs
  the driver's watch loop in-process with `driver.json` re-read every tick. On close it takes the
  loop and its owned host down, with in-flight sessions ended and recorded `stopped`.
- 🔴 **A diff confirms the tree it was given** (`rev-parse --show-toplevel`): git searches upward, and
  would otherwise answer for the repository above it.

## What it is

The desktop application a person runs to **drive the family**: it hosts the local service, carries the
platform UI, and **controls the repositories and their agent sessions** — spawning, monitoring and
coordinating development sessions (Claude Code, Codex, dsh, through an adapter seam) one per domain-owning
repository. Built on the family's desktop runtime sibling, consumed at a released version (D22).

## Installing it (2026-09-22)

`npm run publish:desktop -- --to <folder> --service` publishes the application: one
`daoris-desktop.exe` at the folder's root, the service host with its bundle under `app/`, the
install's own `data/` once it has run, and an `INSTALLED.md` saying so. **`data/` is the Daoris
home** (D63): on first start the shell sets `DAORIS_HOME` to it for its own process — every host and
session it spawns inherits it — and, once, for the account when it has none, so a terminal's `daoris`
meets the same machine. Nothing of Daoris's lives under the user profile; a `~/.daoris` from before
the decision moves in on that first start, `bin/` excepted, and the shell says so once. **Starting it
starts the driver loop.** The publish refuses a folder it did not
write; `--beside` installs next to whatever is there — the repositories it drives, typically — and
still refuses to write over a name it did not write. `npm run desktop -- run --install <folder>`
starts that install with the debug port attached, so the instruments below reach it.
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
`daoris plugin list|add|remove|enable|disable` is the terminal door; the Settings page's Plugins
card is the other — the same rows, the same `plugins.json`, Remove naming what a plugin kept.
**No plugin code ever runs inside the shell, the host or the page.**

## The dev loop — `tools/desktop.mjs` (2026-09-21)

Everything else here has a loop that can see it. The shell had none: Playwright cannot reach it (the
bridge is absent in a browser, by construction) and the vitest suite drives a *mock* of this machine,
so the only way to look at the real window was to open it by hand. `npm run desktop -- <command>` is
that instrument. **It is not a gate** — it starts nothing in CI and asserts nothing — it is how a
person or an agent starts the shell and sees what it actually rendered.

**The gate is `npm run rehearse:deploy`** (D60), and it is a different question: this loop runs what
the workspace built, and that gate publishes the shell to a scratch folder and drives the **artefact**
— a window that finds its host without being told where it is, and a session transcript compared as
bytes. Everything this loop provides is what hid two of the first deployment's four defects.

| command | what |
|---|---|
| `doctor` | what is built, what is running, what a scratch run would use — and whether an installed host would be adopted instead of this workspace's |
| `build [--release]` | the platform bundle into the host's `wwwroot`, then the host, then the shell. That order is the dependency order: a host built before the bundle serves the previous one |
| `run [--real] [--fresh]` | start the shell **on a machine of its own**, with the debug port attached |
| `run --install <dir>` | start the **DEPLOYED** shell in that folder, on its own `data/` home, with the debug port attached. 🔴 The published application opens no port — this launch does, through the environment, which is the deliberate opt-in the first deployment asked for and did not build (case study 2d). `shot`, `eval`, `click` and `kill` then address that install, because they follow the run file rather than this checkout |
| `restart` · `kill` | stop the shell **this checkout built** — matched by executable path, never by process name |
| `shot [name] --page [--size WxH]` | capture the **page** over the debug port instead of the window (SESS1): a minimized window photographs as its 314 × 50 caption, and restoring an installed one puts it in front of its owner. `--size` lays the page out at that size for the capture and puts it back. The native frame is not in it |
| `shot [name] [--theme light\|dark] [--window <name>]` | capture the window into `_fixtures/desktop/screenshots/` (PrintWindow + `PW_RENDERFULLCONTENT`, so the WebView2 composition is in it). `--theme` photographs the OTHER theme without touching the machine's setting — a media-query emulation over the debug port, which makes the page push `SET_THEME` and the **main** window repaint its native chrome (DWM border, caption buttons) for real: the only way to see that chrome in both. 🔴 A **secondary** window has no `SET_THEME` channel and follows the OS directly, so in a `--theme` capture its title bar stays in the machine's own theme — a light title bar over a dark monitor page there is the instrument, not a defect. 🔴 `--window monitor`, `--window browser` (D78) or `--window session:<id>` since SURF8: without it the capture takes whichever window **Windows** calls main, which with a secondary window open is not the caller's choice |
| `eval [--window <name>] "<js>"` | evaluate inside one of the running shell's pages — **the only instrument that sees the bridge-attached half** (the Settings page, the driver controls, the console, chat). `--window` picks a secondary window's page (SURF8); without it, the application's own |
| `click [--window <name>] "<css>"` | click exactly one element, and say what it clicked; a selector matching none or several is a refusal, not a first match |

**A native window's controls are pressed through UI Automation, not typed at** (BRW4, 2026-09-28).
Keystrokes and clicks synthesized from an agent's terminal (`SendKeys`, `keybd_event`,
`mouse_event`) did not reach the shell's windows at all, not even the address bar. UI Automation's
Invoke, scoped to the scratch shell's process id, pressed every button. A shortcut over a page is
then read from the WebView2 control's own code rather than pressed. Scope anything that presses or
types to the scratch shell's pid, never to a caption: the owner's install opens windows of the same
names.

**A dev run gets its own machine, and that is a safety property rather than a convenience.** The shell
runs the driver loop, and the driver spawns **real agent sessions in real repositories**. So `run`
redirects the home and every machine-local file under it, clears the remote environment pair
(inherited, it would feed a real deployment from a scratch store), takes a port of its own, passes
`--app-root` so the WebView2 profile and window state are its own too, and copies `examples/` to work
over. `--real` is the person's own Daoris — the home their `DAORIS_HOME` names — and is spelled out
for that reason. A test asserts the redirect list against the sources that build paths under the
home, because a name missing there does not fail — it edits the person's real config.

Two couplings the tool holds that nothing else does, both found by running it: the debug port needs
**`DOTNET_ENVIRONMENT=Development`** as well (the runtime sets `AdditionalBrowserArguments`, which
makes WebView2 ignore the environment variable, so it re-appends it itself — only in dev mode, which
is why a shipped window has nothing to attach to), and the scratch port needs **`ASPNETCORE_URLS`** as
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
