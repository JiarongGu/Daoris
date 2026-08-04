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

_**The tool is built and proven** — seven commands, 90 tests, a canon of 7 core rules, 5 core skills and
3 packs, adopted into Lyntai with its 1337 tests still green. The skills layer is **complete**, and so
are the tool follow-ups. Nothing is published, so development runs at **`0.0.x`** and the first release is
**`0.1.0`**._

_**Everything remaining needs a decision or another repository, not more code here.** One item blocks the
tag and is the owner's call; the rest are editorial work inside a consuming repository, or gated on the
next adoption. `npm run rehearse` passes 51/51 against the packaged artefact, so the release works —
what is missing is the account to publish it under._

## Part 1 — releasing 0.1.0

_Everything in the repository is ready: `npm run verify` and `npm run rehearse` both pass, the owner is
resolved, the remote exists and is public, and the version is stamped. What is left is pushing, which is
the owner's to do._

- [ ] **REL3 — push and tag `v0.1.0`.** The remote (`origin` →
  `https://github.com/JiarongGu/Daoris`) is configured but **empty**, and the local branch is `master`
  while GitHub creates new repositories with `main` — decide which name the published history carries
  before the first push, because renaming afterwards breaks every clone. Then tag `v0.1.0` and verify
  `npx github:JiarongGu/Daoris#v0.1.0 --version` from a clean directory outside this repo. That last
  command is the only part of the install story `npm run rehearse` cannot cover, because it is the only
  part that depends on GitHub rather than on this package.
- [ ] **REL4 — Lyntai's manifest still names the placeholder.** Its `daoris.json` carries
  `github:OWNER/daoris#…` from the trial adoption. It is part of that repository's uncommitted,
  pending-review changes (ADOPT3), so it is listed here rather than edited from this side.

## Part 2 — findings from the Lyntai adoption (2026-08-04)

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

## Part 3 — canon growth

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
- [ ] **CANON3 — adopt Shenora.** The second adopter, and the one that validates the desktop and webview
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
