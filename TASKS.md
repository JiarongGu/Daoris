# Daoris (道衍) — Active Task Backlog

> **This file holds OPEN tasks only.** A task that is fully done (implemented, tested, committed,
> verified) is **removed from here and appended to [`docs/task-archive.md`](docs/task-archive.md)** with
> its completion date and a one-line outcome — never left checked off in place. `CHANGELOG.md` is the
> release-facing log; the archive is the per-task record. The contract is
> `docs/2026-08-04-daoris-design.md`; the forward sequence is [`ROADMAP.md`](ROADMAP.md).

**Goal:** one canonical set of agent-facing rules and knowledge, materialized into every repository in
the family, kept from drifting, and improved from wherever the improvement was found.

---

## Active backlog

_**`Daoris.Cli` is built and proven** — nine commands, 120 tests, a canon of 8 core rules, 4 core
knowledge documents, 5 core skills and 6 packs, adopted into Lyntai with its 1563 tests still green.
`npm run rehearse` drives the whole consumer lifecycle through the packaged artefact, 52/52. `Daoris.Service` ingests, stores and
searches the family's knowledge and holds its quests and registry — 70 tests, reachable over MCP. `Daoris.Devkit` runs the gates — 57
tests, one binary._

_**All four artefacts exist** (`docs/DECISIONS.md` D20). `Daoris.Devkit` is one 2.7 MB AOT binary with
five universal gates; `Daoris.Web` is the knowledge UI, convergence-first and read-only, served by the
service's HTTP host. Only `Daoris.Desktop` — the shell that hosts the same web build — is unbuilt, and
it carries a brief rather than code._

_**Pushed 2026-08-05** to `main`, after auditing every commit and object against every pattern.
**Nothing is published to npm**, deliberately: the surface moved a great deal in one day, and a version
published now would pin decisions that are three hours old._

## Part 1 — asking other repositories (not yet)

_**Nothing is asked of another repository until Daoris is properly finished.** It is `0.0.x`, nothing is
published, and the surface moved a great deal in a single day — `domain`, `connect`, quests and the
registry are all hours old. Asking a sibling to adopt or to declare anything now spends their time on a
shape that is still moving, and it is Daoris's job to be stable first._

_The Shenora adoption is rehearsed and the notes keep: 6 collisions, 2 twins to retire, local mechanics
drafted at `docs/adoption/shenora-repo-mechanics.md`, budget 40,000, `check` clean at 38,782 bytes, and
`web-webview` + `durable-jobs` ready for it. None of that expires. It waits._

- [ ] **ASK1 — revisit once Daoris is released and stable.** Then, and only then, publish the Shenora
  invitation and ask Lyntai to declare a domain. Gate: a tagged release, a surface that has held still
  for more than a session, and a running service (SVC1).

## Part 2 — other agent harnesses (deliberately not built)

_Daoris targets Claude Code (D23). The others are detected and reported; a second implementation gets
written the day a repository actually adopts one. Detection exists so the gap is loud rather than
silent — installing this tree for a harness that reads a different file leaves every document present,
correct, and never loaded._

- [ ] **HARNESS1 — a second layout, when one is needed.** `src/harness.ts` holds the signals and the
  contract checks; a second implementation slots in beside the Claude one. Do not start until a real
  repository wants it: the layout, the always-loaded semantics and the trigger mechanism all differ,
  and guessing at them produces doctrine nobody chose in a format nobody verified.

## Part 3 — canon growth

- [ ] **CANON2 — one pack candidate left.** `web-webview`, `durable-jobs` and `desktop-app` are done
  (see the archive). Remaining: **`desktop-winforms`** — one 11 KB source, one repository. **Below the
  two-repository bar**, and the bar is the whole reason the canon is trustworthy. Leave it local until a
  second repository needs the same thing.

## Part 4 — tool follow-ups

- [ ] **REH1 — the release rehearsal intermittently reports 45/52.** Seen **twice**, and narrowed:

    - **Always exactly 7 failures**, never a different count. Phase 6 — the canon-upgrade path — has
      exactly 7 checks (`status` reports an update · names the changed file · prints why · quotes the
      changelog · `sync` applies it · the new wording is on disk · `check` is clean). So the whole
      phase fails, which is a broken precondition rather than a flaky assertion.
    - **Both times it ran immediately after canon files were edited and synced** in the same shell
      command.
    - **Not reproducible on demand.** Tried `verify` then `rehearse` chained, three back-to-back runs,
      and a standalone run: 52/52 every time. The obvious hypothesis — that editing the real
      `canon/CHANGELOG.md` interferes — does not hold, because phase 6 writes its own changelog into a
      `canon-v2` fixture and never reads the real one.

  Next time it fails, capture the log *before* re-running anything: the phase-6 output names which of
  the seven went first, and that is the piece still missing. Do not tag a release while this is open.
- [ ] **SVC1 — the service has no persistent deployment.** Everything is verified by starting it,
  driving it, and stopping it. Quests and registrations live in SQLite and survive, but nothing runs
  between sessions, so nothing can be published or pulled. Until then the quest ledger above holds what
  would have been sent — the work is not blocked, only undelivered.

---

## How to work a task

- **TDD, every task:** failing test → run it fail → minimal implementation → run it pass → commit.
- **Commit per task.** **Never commit without the user's approval.** Describe the change structurally.
- **`npm run verify` before claiming done** — every test, then `daoris check` against Daoris's own
  doctrine.
- **A canon file is project-agnostic.** No product names, no build commands, no repository-specific
  layouts. State the principle and the reason; the mechanism belongs in the adopting repository's local
  documents. See `.claude/knowledge/canon-authoring.md`.
- **When a task completes, archive it:** move its entry into `docs/task-archive.md` with the completion
  date and a one-line outcome, and delete it from here.
