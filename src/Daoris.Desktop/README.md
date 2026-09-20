# Daoris.Desktop — the local driver

**Status: built — the driver, its headless host, and the shell all exist, and the person's controls
landed.** The brief was rewritten 2026-09-19 for D45; **the design is settled:
`docs/2026-09-19-driver-design.md` (D46) is the contract.** `Daoris.Desktop.Driver` (the loop, the
adapter seam, the stub, the `claude-code` adapter, and since D47 the machine's **remote sync** —
`RemoteSync` rides the tick, feeding joined registrations, session records and opted-in knowledge up
and mirroring the remote's quests and foreign registrations down, once **per workspace** since D48 §5,
because one shared deployment serves one circle) and `Daoris.Desktop.Driver.Host`
(`daoris-driver`) are driven end to end by the family rehearsal's driver, two-machine remote and
remotes-map phases. **`Daoris.Desktop.App`** (`daoris-desktop`, on Shenora.Windows 0.16.0 — released, D22) is the
shell: it brings up the local HTTP host — adopting one already running, spawning and owning one
otherwise, a dev build run from its project so the bundle serves — carries the platform in its WebView
(`ProductionUrl`, the same bytes a browser gets), runs the driver's shared watch loop in-process with
`driver.json` re-read every tick, forwards tick reports over the IPC bridge (`DAORIS.DRIVER`, consumed
by the page — drivable and hold per repository, stop a running session), edits the machine's wiring
over `DAORIS.REMOTES` (the Machine view, over the same `~/.daoris/remotes.json` the CLI edits — a key
goes in and only its audit prefix comes back), and takes the loop and its
owned host down with it on close, in-flight sessions ended and recorded `stopped`. The one designed
control not yet wired page-side is **start-now**: the host answers `NUDGE`, and no page surface calls
it yet.

## What it is

The desktop application a person runs to **drive the family**: it hosts the local service, carries the
platform UI, and **controls the repositories and their agent sessions** — spawning, monitoring and
coordinating development sessions (claude/codex, through an adapter seam) one per domain-owning
repository. Built on the family's desktop runtime sibling, consumed at a released version (D22).

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
   adapters error naming what exists; an adapter names a harness, never a model.
4. **The service holds state, the driver holds action**: session records and the machine-local
   repository root go to the service; the scheduler, process control, adapters, driver config and
   notifications live here.
5. **The desktop sibling is consumable at a released version** — v0.16.0 (`Shenora.Windows` +
   `@shenora/react`), checked 2026-09-19 — so there is no coordination blocker.
