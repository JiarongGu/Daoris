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

_**`Daoris.Cli` is built and proven** — eight commands, 118 tests, a canon of 7 core rules, 3 core
knowledge documents, 5 core skills and 5 packs, adopted into Lyntai with its 1563 tests still green.
`npm run rehearse` drives the whole consumer lifecycle through the packaged artefact, 52/52. `Daoris.Service` ingests, stores and
searches the family's knowledge — 58 tests, reachable over MCP. `Daoris.Devkit` runs the gates — 45
tests, one binary._

_**Three of the four artefacts exist** (`docs/DECISIONS.md` D20); `Daoris.Web` does not. `Daoris.Devkit`
is built — one 2.7 MB AOT binary, 45 tests, four universal gates, running this repository's own gate set._

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

- [ ] **CANON4 — the `doc-*` maintenance family, deliberately not canonized yet.** `doc-update-technical`
  / `-reference` / `-guide`, `doc-optimize`, `doc-monitor`, `doc-cleanup` appear together in 3
  repositories, which is a real signal. It is **held** rather than deferred by accident: these skills all
  automate hand-maintaining documents that a generated-wiki tool would own outright (D16), so canonizing
  them would install doctrine for a workflow that may be about to change. Decide by trying a generator on
  one repository first. If the generated route wins, what stays canonical is much smaller — the *review*
  of generated output, not its production. `post-feature` and `fix-log` are done.
- [ ] **CANON2 — the last pack candidates.** `web-webview` and `durable-jobs` are **done** (see the
  archive). Two remain, and both are held for the reason CANON2 has always given — a pack nobody
  installs is unvalidated doctrine:
    - **`desktop-app`** — the material is real but lopsided: one repository has 11 KB on desktop testing
      plus screen-capture hygiene, DPI scaling and UI layering, while the second contributes only capture
      tooling. Closer to one repository with a rich document than to two agreeing. Write it when a
      second desktop repository is actually adopting.
    - **`desktop-winforms`** — one 11 KB source, one repository. **Below the two-repository bar**, and
      the bar is the whole reason the canon is trustworthy. Leave it local until a second repository
      needs the same thing.

- [ ] **CANON3 — apply the adoption to the real tree.** _Fully rehearsed 2026-08-05 against a scratch
  consumer carrying that repository's real doctrine, copied out rather than waiting for its tree — which
  still holds 12+ files of in-flight work. Every decision is now determined, so the real run is
  mechanical:_
    - _**6 collisions**, canonical supersedes each: `persist-working-state`, `sensitive-info`,
      `skills-workflow`, and the `doc-loader`, `fix-log`, `pattern-finder` skills._
    - _**2 twins to retire**: local `windows-dev-gotchas` (47% against canonical `windows-machine`) and
      local `doc-claims` (49% against canonical `claims-need-checks`, which now merges it)._
    - _**Local mechanics to preserve** in a new `repo-mechanics.md`: the hook-install requirement, the
      WebView2 browser-arguments trap, the WinForms STA/OLE handle trap, and the desktop verification
      tooling. **The draft is `docs/adoption/shenora-repo-mechanics.md`**, tracked — it was written in
      the rehearsal fixture, which is gitignored scratch and no place for something a later task needs._
    - _**Budget 40,000.** Core plus three packs plus the repository's own rules lands at 38,782 bytes;
      the 24,000 default cannot cover core + two packs + a repository's own material, which is worth
      knowing before the next adoption._
    - _Result: `check` clean, exit 0._

  What remains is applying it there, which needs that repository's work committed first.

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
