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

_**`Daoris.Cli` is built and proven** — eight commands, 118 tests, a canon of 7 core rules, 2 core
knowledge documents, 5 core skills and 3 packs, adopted into Lyntai with its 1563 tests still green.
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
- [ ] **CANON2 — pack candidates with two repositories agreeing.** From the survey: `desktop-app`
  (screenshot hygiene, desktop testing, UI layering — a private sibling pair), `web-webview` (WebView2
  hosting + IPC contracts, from Shenora), `desktop-winforms` (the WinForms shell, from Shenora),
  `durable-jobs` (two repositories). Write each only when a repository is ready to adopt it — a pack that
  no one installs is unvalidated doctrine.
- [ ] **CANON3 — adopt Shenora.** _Blocked only on that repository's tree: it carries 12+ files of
  in-flight work, including edits to the two `.claude/` files adoption touches. Its own work is not ours
  to commit._

  _**Re-analysed 2026-08-05** against the current canon with `daoris analyze`, which writes nothing and
  so is safe against a dirty tree. The picture is much better than the earlier rehearsal recorded:_
    - _**6 collisions** — `persist-working-state`, `sensitive-info`, `skills-workflow`, and the
      `doc-loader`, `fix-log`, `pattern-finder` skills._
    - _**Budget 14,478 → ~28,208 against a 24,000 limit — over by 4,208 (17.5%)**, not the 48% the
      earlier note recorded. Retiring the renamed twin below covers most of it._
    - _The predicted twin, local `windows-dev-gotchas` against canonical `windows-machine`, **is now
      found automatically at 47%** — it was invisible before D17 lowered the threshold to 0.3._
    - _`doctor` also flags three knowledge documents against canonical `library-api-design` at 44–57%
      (`ipc-contracts`, `generic-library`, `webview2-hosting`). Read them before adopting: on the Lyntai
      evidence, at least one is a rule that is mostly canonical plus a deep dive belonging in the
      on-demand tier._

  The second adopter, and the one that validates the desktop and webview packs (CANON2). Expect the
  WebView2/WinForms/devtools specifics to stay Shenora's own — apply the adoption document's merged-twin
  test: not "is this canonical now" but "is every line of it somewhere else."

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
