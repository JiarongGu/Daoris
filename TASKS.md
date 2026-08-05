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

_**`Daoris.Cli` is built and proven** — nine commands, 130 tests, a canon of 8 core rules, 3 core
knowledge documents, 5 core skills and 5 packs, adopted into Lyntai with its 1563 tests still green.
`npm run rehearse` drives the whole consumer lifecycle through the packaged artefact, 52/52. `Daoris.Service` ingests, stores and
searches the family's knowledge — 58 tests, reachable over MCP. `Daoris.Devkit` runs the gates — 57
tests, one binary._

_**Three of the four artefacts exist** (`docs/DECISIONS.md` D20); `Daoris.Web` does not. `Daoris.Devkit`
is built — one 2.7 MB AOT binary, 57 tests, five universal gates, running this repository's own gate set._

_**Pushed 2026-08-05** to `main`, after auditing all 73 commits and 932 objects against every pattern.
**Nothing is published to npm**, deliberately: the tag and the release workflow stay unrun while a
quarter of the project does not exist._

## Part 1 — other agent harnesses (deliberately not built)

_Daoris targets Claude Code (D23). The others are detected and reported; a second implementation gets
written the day a repository actually adopts one. Detection exists so the gap is loud rather than
silent — installing this tree for a harness that reads a different file leaves every document present,
correct, and never loaded._

- [ ] **HARNESS1 — a second layout, when one is needed.** `src/harness.mjs` holds the signals and the
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

- [x] **CANON3 — adopt Shenora.** ✅ **Posted as quest `#ee8994`** on 2026-08-05, which is what the
  backlog should always have said: it was never blocked on that repository's tree, it was blocked on
  Daoris not having a way to ask. The quest carries the whole rehearsal — 6 collisions, 2 twins, the
  local mechanics to preserve, budget 40000, `check` clean — so taking it is mechanical. Whether it is
  taken, and when, belongs to whoever works there.

## Part 3 — tool follow-ups

_Nothing open here._

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
