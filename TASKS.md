# Daoris (道衍) — Active Task Backlog

> **This file holds OPEN tasks only.** A finished task is **removed from here and appended to
> [`docs/task-archive.md`](docs/task-archive.md)** with its date and outcome — never ticked in place.
> `CHANGELOG.md` is the release-facing log; the archive is the per-task record. The contract is
> `docs/2026-08-04-daoris-design.md`; the forward sequence is [`ROADMAP.md`](ROADMAP.md).

**Goal:** one canonical set of agent-facing rules and knowledge, materialized into every repository in
the family, kept from drifting, and improved from wherever the improvement was found.

## State

All four artefacts exist; only `Daoris.Desktop` is a brief. Nine commands, 120 CLI tests, 70 service,
57 devkit, 52/52 release rehearsal, 8/8 devkit gates. Canon: 8 core rules, 4 knowledge, 5 skills,
6 packs. Always-loaded core is **23,568 of 24,000 bytes** — the next canon addition fails the gate.

Nothing is published; development runs at `0.0.x`. **Adoption by other repositories is the owner's call
and happens when Daoris is ready** — it is not tracked here. The Shenora rehearsal keeps and does not
expire: 6 collisions, 2 twins to retire, local mechanics drafted at
`docs/adoption/shenora-repo-mechanics.md`, budget 40,000, `check` clean at 38,782 bytes, with
`web-webview` and `durable-jobs` ready for it.

## Backlog

- [ ] **SVC1 — no persistent service.** Everything is verified by starting it, driving it, stopping it.
  Quests and registrations live in SQLite and survive, but nothing runs between sessions, so nothing can
  be published or pulled.

- [ ] **REH1 — the release rehearsal intermittently reports 45/52.** Seen twice, **always exactly 7
  failures** — precisely the canon-upgrade phase's 7 checks, so a whole phase fails on a broken
  precondition rather than a flaky assertion. Both times it ran straight after canon files were edited
  and synced. Not reproducible: chained, three back-to-back, and standalone runs all pass.
  **Next failure, capture the log before re-running** — the phase-6 output names which check went first,
  and that is the missing piece. Do not tag a release while this is open.

- [ ] **CANON2 — `desktop-winforms`, the last pack candidate.** One 11 KB source, one repository —
  below the two-repository bar, which is the whole reason the canon is trustworthy. Leave it local until
  a second repository needs the same thing.

- [ ] **HARNESS1 — a second harness layout.** `src/harness.ts` holds the signals and contract checks; a
  second implementation slots in beside the Claude one. Do not start until a repository actually wants
  it — the layout, always-loaded semantics and trigger mechanism all differ, and guessing produces
  doctrine nobody chose in a format nobody verified.

## How to work a task

- **TDD:** failing test → run it fail → minimal implementation → run it pass → commit.
- **Commit per task.** **Never commit without the user's approval.**
- **`npm run verify` before claiming done.**
- **A canon file is project-agnostic** — the principle and the reason, never the mechanism. See
  `.claude/knowledge/canon-authoring.md`.
- **When a task completes, move it to `docs/task-archive.md`** with the date and outcome.
