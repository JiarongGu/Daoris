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

_**`Daoris.Cli` is built and proven** — seven commands, 96 tests, a canon of 7 core rules, 5 core skills
and 3 packs, adopted into Lyntai with its 1337 tests still green. `npm run rehearse` drives the whole
consumer lifecycle through the packaged artefact, 52/52._

_**It is one of four artefacts, and the only one that exists** (`docs/DECISIONS.md` D20). **Not
releasing yet:** publishing the CLI alone would invite adoption of a quarter of the project. The devkit
is next — eleven repositories carry the same hand-copied dev script at a 20× size spread, which is the
strongest evidence in the family._

## Part 1 — the next artefact: `Daoris.Devkit`

_The brief and its open questions are in `src/Daoris.Devkit/README.md`, written before any code._

- [ ] **DEV1 — decide how a repository declares its gates.** A `verify` block in `daoris.json`, or a
  file the devkit owns. The manifest is already the declaration point for packs and the target, so it is
  the obvious home — but gates are commands rather than documents, and the manifest has stayed data-only
  so far. Settle before writing the runner.
- [ ] **DEV2 — decide how the binary reaches a repository without losing the offline guarantee.**
  `check` never touches the network by construction (D8), and that must survive. A binary cannot ship
  inside the npm package the way the canon does, so this is genuinely new ground.
- [ ] **DEV3 — extract the universal gates from the eleven copies.** The survey says the shared set is a
  sensitive-content scan, doctrine drift, version authorship, and documentation freshness. Everything
  else is stack-specific and belongs in the repository's own declaration. Same method as the skills:
  read the extremes, keep only what they share.
- [ ] **DEV4 — the devtools copy in this repository is untracked and inherited.** 31 MB of a sibling's
  toolkit, including built binaries, sitting in `devtools/`. It is the raw material for DEV3 and should
  be read, mined, and then removed — not tracked.

## Part 2 — when a release is wanted

_Both prerequisites are done; neither is urgent while three artefacts are unbuilt._

- [ ] **REL3 — push, then decide the branch name first.** `origin` is configured and the remote is
  **empty**, while local history is on `master` and GitHub creates new repositories with `main`.
  Renaming after the first push breaks every clone, so choose before pushing.
- [ ] **REL4 — Lyntai's manifest still names the `OWNER` placeholder** from the trial adoption. It is
  part of that repository's uncommitted, pending-review changes (ADOPT3), so it is listed here rather
  than edited from this side.

## Part 3 — findings from the Lyntai adoption (2026-08-04)

_The first real adoption surfaced things no synthetic test could. Both remaining items are editorial work
inside Lyntai rather than changes here, which is why they are recorded rather than silently fixed._

- [ ] **ADOPT1 — Lyntai's `dev-conventions.md` now substantially duplicates canonical
  `dotnet-package-layout`.** _`daoris doctor` now detects this automatically and reports it at 58% shared
  vocabulary; what remains is the editorial work below._ That ~10 KB file is always-loaded and is why Lyntai's core sits at 34,851
  bytes; its budget was set to 40,000 to reflect reality rather than hide the overage. The overlap is
  package layout, naming, and DI variation points — all now canonical. What remains genuinely
  Lyntai-specific (the LLM provider seam, spawn hygiene, its testing setup) is a **knowledge** deep dive,
  not an always-loaded rule. Trimming it, and moving the remainder to `knowledge/`, would drop Lyntai's
  always-loaded core by roughly a quarter.
- [ ] **ADOPT3 — Lyntai's changes are uncommitted, pending owner review.** Four rules replaced, one twin
  deleted, seven files added, plus `daoris.json` / `daoris.lock`. Everything repo-specific that the
  generalized rules dropped is preserved in a new local `.claude/rules/repo-mechanics.md`.
  `git checkout .claude/` reverts all of it.

## Part 4 — canon growth

- [ ] **CANON4 — the `doc-*` maintenance family, deliberately not canonized yet.** `doc-update-technical`
  / `-reference` / `-guide`, `doc-optimize`, `doc-monitor`, `doc-cleanup` appear together in 3
  repositories, which is a real signal. It is **held** rather than deferred by accident: these skills all
  automate hand-maintaining documents that a generated-wiki tool would own outright (D16), so canonizing
  them would install doctrine for a workflow that may be about to change. Decide by trying a generator on
  one repository first. If the generated route wins, what stays canonical is much smaller — the *review*
  of generated output, not its production. `post-feature` and `fix-log` are done.
- [ ] **CANON6 — `scripts-live-in-repo` is a merged renamed twin of two core rules.** Present in 3
  repositories; its first half is canonical `no-tmp-for-repo-files` and its second half is canonical
  `file-tool-discipline`. No new core rule — but those repositories will carry both, and the genuinely
  local mechanics it also holds (allow-list rules, a `cd` prefix defeating them) must be preserved
  locally rather than dropped. _Verified 2026-08-05: `doctor` does **not** flag it, at 24% and 23%, and
  retuning cannot fix that — it reaches the same principle in an entirely different vocabulary (D17).
  **This one is a manual step at adoption**, and it is the reason the adoption document's "hunt renamed
  twins by hand" instruction stays._
- [ ] **CANON2 — pack candidates with two repositories agreeing.** From the survey: `desktop-app`
  (screenshot hygiene, desktop testing, UI layering — a private sibling pair), `web-webview` (WebView2
  hosting + IPC contracts, from Shenora), `desktop-winforms` (the WinForms shell, from Shenora),
  `durable-jobs` (two repositories). Write each only when a repository is ready to adopt it — a pack that
  no one installs is unvalidated doctrine.
- [ ] **ADOPT5 — a `library-api-design` vocabulary cluster.** After the tier fix, `doctor` reports four
  of that repository's knowledge documents against canonical `library-api-design` at 42–57%. Unlike the
  skill case these are same-tier and plausibly related — they are all library/API-shaped documents — so
  this needs a read rather than a code change. It may be a genuine signal that the pack rule is too
  broad, or simply what a shared domain looks like.
- [ ] **CANON3 — adopt Shenora.** _Rehearsed 2026-08-05 against a scratch consumer carrying its real
  doctrine, because its working tree had eight files of in-flight work. Result: **6 collisions**
  (`sensitive-info`, `persist-working-state`, `skills-workflow`, and the `doc-loader`, `fix-log`,
  `pattern-finder` skills), a clean sync once resolved, and a **48% budget overage** — 35,529 bytes
  against a 24,000 limit. The real adoption is the same steps against the real tree, once its work is
  committed._ The second adopter, and the one that validates the desktop and webview
  packs. Expect collisions on `sensitive-info` and `persist-working-state`, and a renamed twin in
  `windows-dev-gotchas` versus canonical `windows-machine` — the machine-level content is canonical, the
  WebView2/WinForms/devtools items are Shenora's own.

## Part 5 — tool follow-ups

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
