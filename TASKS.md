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

_**`Daoris.Cli` is built and proven** — nine commands, 119 tests, a canon of 8 core rules, 3 core
knowledge documents, 5 core skills and 5 packs, adopted into Lyntai with its 1563 tests still green.
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

## Part 1 — other agent harnesses (deliberately not built)

_Daoris targets Claude Code (D23). The others are detected and reported; a second implementation gets
written the day a repository actually adopts one. Detection exists so the gap is loud rather than
silent — installing this tree for a harness that reads a different file leaves every document present,
correct, and never loaded._

- [ ] **HARNESS1 — a second layout, when one is needed.** `src/harness.ts` holds the signals and the
  contract checks; a second implementation slots in beside the Claude one. Do not start until a real
  repository wants it: the layout, the always-loaded semantics and the trigger mechanism all differ,
  and guessing at them produces doctrine nobody chose in a format nobody verified.

## Part 2 — canon growth

- [ ] **CANON2 — the last pack candidates.** `web-webview` and `durable-jobs` are **done** (see the
  archive). Two remain, and both are held for the reason CANON2 has always given — a pack nobody
  installs is unvalidated doctrine:
    - **`desktop-app`** — **above the bar after all, and the tool is what found that.** A by-hand survey
      globbing for `*desktop*`, `*capture*`, `*dpi*` concluded "one rich repository plus fragments". The
      service's semantic pass, run across all 11 repositories, returns a **0.890 group spanning three**:
      `desktop-testing-cdp.md`, `desktop-test/SKILL.md`, `devtools/SKILL.md` and `dev-loop.md` — names
      the glob could never have matched, which is exactly the failure the tool exists to fix. Ready to
      write; still wants an adopter to validate against, like `durable-jobs`.
    - **`desktop-winforms`** — one 11 KB source, one repository. **Below the two-repository bar**, and
      the bar is the whole reason the canon is trustworthy. Leave it local until a second repository
      needs the same thing.

- [ ] **CANON3 — the Shenora adoption, as a quest.** _Open again, and honestly so._ It was briefly
  marked done because a quest had been written into that repository's backlog — which turned out to be
  the violation the whole quest system exists to prevent, so it was removed (D32 amendment). Quests now
  live in the service and are pulled, and **no service is running persistently yet**, so nothing has
  been published.

  Everything needed is prepared: the rehearsal determined 6 collisions, 2 twins to retire, the local
  mechanics to preserve (`docs/adoption/shenora-repo-mechanics.md`), a 40,000 budget, and `check` clean.
  When a service is up, publish it with `quest_publish` — and note that Shenora must adopt first, since
  only an adopted repository can be addressed (D34).

## Part 3 — tool follow-ups

- [ ] **REG1 — Lyntai shows as adopted but undeclared.** It carries the canon and appears in the
  registry with no `domain`, so a sibling cannot tell what is worth asking of it. Filling that in is
  Lyntai's own work and belongs to Lyntai — this entry exists so the gap is visible from here, not so it
  gets fixed from here.
- [ ] **REH1 — the release rehearsal reported 45/52 once.** Two runs immediately after it, with nothing
  changed in between, reported 52/52, and it has passed every time since. Recorded rather than
  explained. A gate that fails once and passes afterwards is worth watching before it is trusted, and
  the cause is more likely to be found by catching it again than by reasoning about it now.
- [ ] **SVC1 — the service has no persistent deployment.** Everything is verified by starting it,
  driving it, and stopping it. Quests and registrations live in SQLite and survive, but nothing runs
  between sessions — so no quest can actually be delivered yet. This is what CANON3 waits on.

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
