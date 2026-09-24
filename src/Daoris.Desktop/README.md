# Daoris.Desktop — the local driver

**Status: built — the driver, its headless host, and the shell all exist, and the person's controls
landed.** The brief was rewritten 2026-09-19 for D45; **the design is settled:
`docs/2026-09-19-driver-design.md` (D46) is the contract.** `Daoris.Desktop.Driver` (the loop, the
adapter seam with its `interactive` capability (D49 §3), its `Toolchain` (D49 §4 — where a harness's
binary, version question, configuration-home variable and own install/update/login flows are declared)
and, since D53/ACP1, its **`Wire`** — the door the driver holds a session over: the original pipe, or
**ACP** (`AcpSession`, JSON-RPC on the process's stdio, with `session/request_permission` refused by
construction and the record still concluded from the exit code and the quest) — the stub, the
`acp-stub` that proves the protocol door with no model in it, the `claude-code` adapter,
`ChatRunner` for conversations, `HarnessRoster` — one judgement for both spawn doors: is the harness
here, and which named credential profile does this run as — and since D47 the machine's **remote sync** —
`RemoteSync` rides the tick, feeding joined registrations, session records and opted-in knowledge up
and mirroring the remote's quests and foreign registrations down, once **per workspace** since D48 §5,
because one shared deployment serves one circle) and `Daoris.Desktop.Driver.Host`
(`daoris-driver`) are driven end to end by the family rehearsal's driver, two-machine remote and
remotes-map phases.

**`Daoris.Desktop.Modules`** is the shell's **head**, split out from its window 2026-09-20: the loop,
the host supervisor, and every IPC module the platform page talks to (`DAORIS.DRIVER`,
`DAORIS.REGISTRY`, `DAORIS.REMOTES`) — plus `Refusals`, where a refusal is declared once as a code the
page translates. Plain `net10.0`, because none of it is WinForms; it had been Windows-only by accident
of where it was written, and that accident cost it every test it should have had. **The contract
between the page and this machine was asserted on neither side** — the page's suite mocks the bridge,
this half had no test project — which is how five written refusals reached people as a blank failure
for as long as they existed (`docs/FIX-LOG.md`). It now carries 39 tests and its own gate.

**`Daoris.Desktop.App`** (`daoris-desktop`, on Shenora.Windows 0.16.0 — released, D22) is the
window, and now only the window. **It is frameless since SURF7** (`OptimizedForm` +
`FramelessChrome`): there is no OS title bar, the platform's own app strip is the title bar — it
drags, double-click maximizes, a sliver above it resizes from the top — and the room that strip
reserves is handed to the OS as real caption buttons, which the **window** paints
(`NativeCaptionButtons`, D56 as amended) from `ChromePalette`'s copy of D41's tokens. 🔴
`AppPlacement` is the truth about maximized, never `Form.WindowState`, which lies about a window that
maximizes by hand. `WindowCommandModule` is mapped **late**, from the form's constructor, because it
needs a live form.

**It notifies** (SURF5b/D55 §4), which closes driver design open question 5: a session that parks, or
ends without the person asking, raises an OS balloon from the shell's own `NotifyIcon` — and stays
quiet while the window is on screen and focused, because the page's own toast has it. It decides
nothing: `AttentionWatch` in the driver library does, so `daoris-driver` on a machine with no screen
prints the same judgement as a line. `daoris driver notify on|off` and the Machine view's checkbox
are two doors onto one `driver.json` field (D50).

**It is no longer the only window** (SURF8/D55 §b). `SecondaryWindows` opens named ones on their own
STA pumps — `monitor` and `session:<id>`, asked for over `DAORIS.WINDOWS` — each a `SecondaryForm`
carrying the **same bundle at its own route** (`?window=<name>`), so they are the platform's own
components and not a second frontend. Three things about them are load-bearing: they keep their
**native frame** (`WindowCommandModule` targets one form and its module name is reserved and
singular, so frameless chrome is the main window's alone); each builds its **own WebView2
environment** (🔴 `UseSharedEnvironment = false` — a `CoreWebView2Environment` is affine to the thread
that created it, and sharing the main window's opens the window and then fails its bring-up); and each
follows the **OS theme directly** through `SystemEvents`, because it has no `SET_THEME` channel of its
own. They are disposed — not abandoned — on shutdown, since their threads are background and an
unwaited exit kills them before their geometry saves run.

Beyond the frame it brings up the local HTTP host — adopting one already running, spawning and owning one
otherwise, a dev build run from its project so the bundle serves — carries the platform in its WebView
(`ProductionUrl`, the same bytes a browser gets), runs the driver's shared watch loop in-process with
`driver.json` re-read every tick, forwards tick reports over the IPC bridge (`DAORIS.DRIVER`, consumed
by the page — drivable and hold per repository, stop a running session, and since D49 §2 **the live
console**: `TAIL_SESSION` for a session's backlog and batched `SESSION_OUTPUT` events for what it says
next, fed by the capture pump's tee into a bounded per-session buffer that never leaves this machine;
since D49 §3 **conversations** too — `START_CHAT`, `SESSION_INPUT`, `END_CHAT` and a `SESSION_ENDED`
event over the same bridge, and `daoris-driver chat --repository <name>` for a machine with no screen; since SURF6 **the
review** — `SESSION_DIFF` returns a session's landed work as a bounded diff, measured from the
`base_commit` the spawn records, read-only by construction and desktop-only for the console's
reason (🔴 it confirms `rev-parse --show-toplevel` names the tree it was given, because git
searches UPWARD and would otherwise answer for the repository above it);
since D76 **the conversation** — `SESSION_HISTORY` reads a session's record a page at a time
(newest, `before`, or `after` for a gap) and batched `SESSION_EVENTS` carry what it does next, both
from the typed events the driver keeps as `sessions/<id>.events.jsonl` beside each transcript, mapped
from the door's own wire (ACP's `session/update`, Claude Code's `stream-json`) and never parsed from a
console line; since CONV4a **the turn** — `CANCEL_TURN` stops a conversation's turn and keeps the
session, answering what it withdrew, and `SESSION_QUEUE` with live `SESSION_QUEUED` events says what
the person sent that has not reached the harness yet (on a terminal, Ctrl+C during a turn);
and since D49 §4 **the toolchain** — `HARNESSES` for the roster this machine has and `HARNESS_ACTION`
for the person's install, update or login, each spawning that harness's own mechanism and relaying it
through the console under `<harness>:<action>`, never a session id, because it is not a session),
edits the machine's wiring
over `DAORIS.REMOTES` (the Machine view, over the same `remotes.json` the CLI edits — a key
goes in and only its audit prefix comes back), and takes the loop and its
owned host down with it on close, in-flight sessions ended and recorded `stopped`. The one designed
control not yet wired page-side is **start-now**: the host answers `NUDGE`, and no page surface calls
it yet.

## What it is

The desktop application a person runs to **drive the family**: it hosts the local service, carries the
platform UI, and **controls the repositories and their agent sessions** — spawning, monitoring and
coordinating development sessions (claude/codex, through an adapter seam) one per domain-owning
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
`daoris plugin list|add|remove|enable|disable` is the terminal door; the Machine view's Plugins
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
| `shot [name] [--theme light\|dark] [--window <name>]` | capture the window into `_fixtures/desktop/screenshots/` (PrintWindow + `PW_RENDERFULLCONTENT`, so the WebView2 composition is in it). `--theme` photographs the OTHER theme without touching the machine's setting — a media-query emulation over the debug port, which makes the page push `SET_THEME` and the **main** window repaint its native chrome (DWM border, caption buttons) for real: the only way to see that chrome in both. 🔴 A **secondary** window has no `SET_THEME` channel and follows the OS directly, so in a `--theme` capture its title bar stays in the machine's own theme — a light title bar over a dark monitor page there is the instrument, not a defect. 🔴 `--window monitor` or `--window session:<id>` since SURF8: without it the capture takes whichever window **Windows** calls main, which with a secondary window open is not the caller's choice |
| `eval [--window <name>] "<js>"` | evaluate inside one of the running shell's pages — **the only instrument that sees the bridge-attached half** (the Machine view, the driver controls, the console, chat). `--window` picks a secondary window's page (SURF8); without it, the application's own |
| `click [--window <name>] "<css>"` | click exactly one element, and say what it clicked; a selector matching none or several is a refusal, not a first match |

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
3. **The adapter seam** is D23 one layer up: claude-code supported, codex explicit second, unknown
   adapters error naming what exists; an adapter names a harness, never a model. Since D49 §4 it also
   declares that harness **as a tool** — and managing a tool is a different question from spawning
   sessions on it, which is why `codex` is manageable from `daoris agent` while no adapter spawns it.
4. **The service holds state, the driver holds action**: session records and the machine-local
   repository root go to the service; the scheduler, process control, adapters, driver config and
   notifications live here.
5. **The desktop sibling is consumable at a released version** — v0.16.0 (`Shenora.Windows` +
   `@shenora/react`), checked 2026-09-19 — so there is no coordination blocker.
