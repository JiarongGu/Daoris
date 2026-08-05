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

_**`Daoris.Cli` is built and proven** — eight commands, 118 tests, a canon of 7 core rules, 1 core
knowledge document, 5 core skills and 3 packs, adopted into Lyntai with its 1563 tests still green.
`npm run rehearse` drives the whole consumer lifecycle through the packaged artefact, 52/52. `Daoris.Service` ingests, stores and
searches the family's knowledge — 58 tests, reachable over MCP. `Daoris.Devkit` runs the gates — 30
tests, one binary._

_**Three of the four artefacts exist** (`docs/DECISIONS.md` D20); `Daoris.Web` does not. **Not releasing
yet:** publishing piecemeal would invite adoption of part of the project. `Daoris.Devkit` is built — one
2.7 MB AOT binary, 30 tests, four universal gates, running this repository's own gate set._

## Part 1 — when a release is wanted

_Nothing blocks a tag but the decision below._

- [ ] **REL3 — push, then decide the branch name first.** `origin` is configured and the remote is
  **empty**, while local history is on `master` and GitHub creates new repositories with `main`.
  Renaming after the first push breaks every clone, so choose before pushing.

## Part 2 — other agent harnesses (deliberately not built)

_Daoris targets Claude Code (D23). The others are detected and reported; a second implementation gets
written the day a repository actually adopts one. Detection exists so the gap is loud rather than
silent — installing this tree for a harness that reads a different file leaves every document present,
correct, and never loaded._

- [ ] **HARNESS1 — a second layout, when one is needed.** `src/harness.mjs` holds the signals and the
  contract checks; a second implementation slots in beside the Claude one. Do not start until a real
  repository wants it: the layout, the always-loaded semantics and the trigger mechanism all differ,
  and guessing at them produces doctrine nobody chose in a format nobody verified.

## Part 3 — canon growth

- [ ] **CANON4 — the `doc-*` maintenance family, deliberately not canonized yet.** `doc-update-technical`
  / `-reference` / `-guide`, `doc-optimize`, `doc-monitor`, `doc-cleanup` appear together in 3
  repositories, which is a real signal. It is **held** rather than deferred by accident: these skills all
  automate hand-maintaining documents that a generated-wiki tool would own outright (D16), so canonizing
  them would install doctrine for a workflow that may be about to change. Decide by trying a generator on
  one repository first. If the generated route wins, what stays canonical is much smaller — the *review*
  of generated output, not its production. `post-feature` and `fix-log` are done.
- [ ] **CANON2 — pack candidates with two repositories agreeing.** From the survey: `desktop-app`
  (screenshot hygiene, desktop testing, UI layering — a private sibling pair), `web-webview` (WebView2
  hosting + IPC contracts, from Shenora), `desktop-winforms` (the WinForms shell, from Shenora),
  `durable-jobs` (two repositories). Write each only when a repository is ready to adopt it — a pack that
  no one installs is unvalidated doctrine.
- [ ] **CANON3 — adopt Shenora.** _Rehearsed 2026-08-05 against a scratch consumer carrying its real
  doctrine, because its working tree had eight files of in-flight work. Result: **6 collisions**
  (`sensitive-info`, `persist-working-state`, `skills-workflow`, and the `doc-loader`, `fix-log`,
  `pattern-finder` skills), a clean sync once resolved, and a **48% budget overage** — 35,529 bytes
  against a 24,000 limit. The real adoption is the same steps against the real tree, once its work is
  committed._ The second adopter, and the one that validates the desktop and webview
  packs. Expect collisions on `sensitive-info` and `persist-working-state`, and a renamed twin in
  `windows-dev-gotchas` versus canonical `windows-machine` — the machine-level content is canonical, the
  WebView2/WinForms/devtools items are Shenora's own.

## Part 4 — tool follow-ups

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
