# Daoris.Desktop — the local driver

**Status: not started. This document is the brief, rewritten 2026-09-19 for D45** — the owner's
direction that Daoris becomes the main driver for all projects. The earlier brief (a shell hosting the
web build) is a strict subset of this one.

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

## Open questions — DRV1's design settles these before any code

1. **Spawn or wake:** how a session is started per repository (headless CLI invocation? attached
   terminal? what carries the quest in as the target), and what "the driver should have started this"
   means for a quest sitting unclaimed.
2. **The session lifecycle** attached to a quest — started, working, gates-green, done/declined,
   failed — and where it is stored (the service already holds quests; sessions are state too).
3. **The adapter seam**: claude-code first, codex second — one supported, others explicit, never
   guessed (D23's lesson, one layer up).
4. **What lives in the service vs the shell**: the trigger/session surface probably belongs to the
   service (every client benefits); process control belongs to the desktop host.
5. **The desktop sibling**: consumable at a released version? If not, that is a coordination point
   like LYN1 — never a reason to reach across.
