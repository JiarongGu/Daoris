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

_**The tool is built and proven** — seven commands, 72 tests, 6 core rules + 3 packs, adopted into Lyntai
with its 1337 tests still green. Nothing is published, so development runs at **`0.0.x`** and the first
release is **`0.1.0`**; the skills layer below lands in it rather than behind a later version boundary.
Two items need an owner decision before any tag._

## Part 1 — release blockers (both need an owner decision)

- [ ] **REL1 — the GitHub owner.** `OWNER` is a literal placeholder in the places that ship:
  `src/commands.mjs:36` (what every future `daoris init` writes into a consumer's manifest),
  `daoris.json:2`, `README.md` (install lines + ecosystem links), and Lyntai's `daoris.json:2`. Test
  fixtures may keep the placeholder. Once decided: replace, add the remote, tag the release, and verify
  `npx github:<owner>/daoris#<tag> --version` works from a clean directory — the install story is
  untested until that command runs. `test/version.test.mjs` holds the four live refs in step, so only the
  owner half is manual.
- [ ] **REL2 — the LICENSE.** `package.json` declares MIT but there is no `LICENSE` file, and MIT
  requires a named copyright holder. Naming a person in a tracked file is the owner's call.

## Part 2 — findings from the Lyntai adoption (2026-08-04)

_The first real adoption surfaced three things no synthetic test could. Each is recorded rather than
silently fixed, because each is editorial work on someone's doctrine._

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

- [ ] **CANON4 — the discovery trio is two-thirds canonized; the rest of the frequent skills are not.**
  From the twelve-repository survey (`local/`, untracked): `post-feature` appears in 4, `fix-log` in 3,
  and a six-strong `doc-update-*` / `doc-optimize` / `doc-monitor` / `doc-cleanup` family in 3. Each needs
  the same treatment `doc-loader` got — extract the invariant procedure, leave the routing content local.
  `fix-log` is the strongest candidate: its three copies are within 100 bytes of each other, so the
  invariant is nearly the whole file.
- [ ] **CANON5 — `caveman` is a core skill candidate, at five repositories.** _Re-analyzed 2026-08-04;
  the first reading, that it belonged in global memory, was wrong._ It is not a taste — it is an output
  protocol with a measurable goal (token cost) and **safety carve-outs earned the hard way**: never
  compress a destructive-action warning or an irreversible-action confirmation, never compress a
  multi-step sequence where fragment order risks a misread, never abbreviate an identifier, a path, or a
  commit message. Those carve-outs are the canonical part, and they are precisely the "trap that succeeds
  wrongly" that canon-authoring prizes — a compressed security warning renders perfectly and is still
  wrong. Global memory would strip exactly them: a fresh clone would get terseness without the guardrails.
  The five copies split into a conservative variant (explicit invoke only, gate preserved) and a
  persistent one (intensity levels, classical-Chinese modes); canonize the protocol and the carve-outs,
  leave intensity presets local. `no-global-memory` has been sharpened with the distinguishing test —
  would a fresh clone be defective without it — and promoted back into the canon.
- [ ] **CANON6 — `scripts-live-in-repo` is a merged renamed twin of two core rules.** Present in 3
  repositories; its first half is canonical `no-tmp-for-repo-files` and its second half is canonical
  `file-tool-discipline`. No new core rule — but those repositories will collide on adoption, and the
  genuinely local mechanics it also carries (allow-list rules, a `cd` prefix defeating them) must be
  preserved locally rather than dropped. `daoris doctor` should flag it; verify that it does.
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

- [ ] **TOOL4 — the many-developers axis is thinner than the many-repositories one.** Daoris answers
  "multiple developers" by never becoming a service: doctrine is a tracked file, so a clone carries it,
  review touches it, and a move preserves it — the substrate solves the problem, which is exactly why
  a hosted shared workspace would be worse here (accounts, outside review, does not survive the tool).
  What is genuinely thin is **coordination**: when a canon change lands, a consuming repository learns
  about it only by someone running `status`, and there is no per-consumer account of *what* changed or
  why. Options, cheapest first: have `status` name the changed files rather than just reporting that a
  newer canon exists; let `sync` print the canon's changelog entries between the locked version and the
  new one. Neither needs a network — the canon ships in the package (D11). Do not build notification. Retirement removes a file and creation adds one, so a canonical
  file that is *renamed* upstream lands in consumers as a delete plus an add — losing nothing, but also
  telling the consumer nothing about why. A `renamedFrom` field in `pack.json` would let the plan say
  "renamed" instead.

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
