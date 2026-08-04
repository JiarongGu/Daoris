# Task archive

Completed work, with the date it closed and what shipped. `TASKS.md` holds only open items; this file is
the per-task record. Entries preserve their original wording so the archive stays faithful.

---

## Part 1 — v0.1: the CLI (2026-08-04)

Executed from `docs/2026-08-04-daoris-v0.1-plan.md`, one task per commit, each a failing test first.

- [x] **T1 — Package skeleton, error type, CLI dispatch**
  ✅ done 2026-08-04 — `package.json` with the `bin`, `bin/daoris.mjs`, `src/errors.mjs`. Exit codes
  established as the contract. Caught immediately that the inherited `.gitignore` carried `**/bin/`,
  which in a Node package silently excludes the CLI entry point; scoped it to the tools directory.

- [x] **T2 — Normalized IO, atomic writes, hashing**
  ✅ done 2026-08-04 — `src/fsx.mjs`. Hashing normalizes first, so a checkout that converted line endings
  is never mistaken for a local edit. The CJK and em-dash round-trip is asserted.

- [x] **T3 — Document envelope: header + frontmatter**
  ✅ done 2026-08-04 — `src/document.mjs`. A frontmatter block missing any required field is treated as
  absent, so the index marks the file rather than half-listing it.

- [x] **T4 — Canon reader and pack selection**
  ✅ done 2026-08-04 — `src/canon.mjs`. The directory is the tier (D7). Also added `captureError` to the
  shared fixture: `assert.throws` returns `undefined`, so the plan's pattern for asserting on exit codes
  could not have worked in any suite that used it.

- [x] **T5 — Manifest and lockfile**
  ✅ done 2026-08-04 — `src/config.mjs`. Lock entries sorted by target and the shape fixed, so a reviewer
  sees what moved rather than a reshuffle.

- [x] **T6 — Index generation and the `index` command**
  ✅ done 2026-08-04 — `src/indexgen.mjs`. Generated from disk, so the index can never point at a missing
  file; a file without frontmatter is marked, never dropped.

- [x] **T7 — `sync`: materialize, retire, write the lock**
  ✅ done 2026-08-04 — `src/materialize.mjs`. Plan and apply separated so a plan can be printed
  (`--dry-run`) or asserted without touching disk. Retirement is the capability copy-paste cannot have.

- [x] **T8 — `check`: offline drift, staleness, budget, index**
  ✅ done 2026-08-04 — `src/drift.mjs`. Asserted by deleting the canon entirely and requiring exit 0.

- [x] **T9 — `upstream`: promote a local edit to the canon**
  ✅ done 2026-08-04 — `src/upstream.mjs`. The return half of 衍 (D9).

- [x] **T10 — `init` and `status`**
  ✅ done 2026-08-04 — `src/commands.mjs`. `init` reports rather than guessing which packs a repository
  wants: a wrong guess installs the wrong always-loaded core.

- [x] **Adoption collisions — refuse rather than clobber**
  ✅ done 2026-08-04 — unplanned, found while preparing to dogfood. Drift detection only guarded files
  already in the lock, so a first sync silently overwrote a rule the repository had written itself.
  Separated by provenance; see `docs/DECISIONS.md` D12.

- [x] **T11 — Seed the core canon and dogfood**
  ✅ done 2026-08-04 — five core rules written project-agnostic; Daoris adopted them itself and checked
  clean. A test asserts it stays that way.

## Part 2 — v0.1: the canon and the first adoption (2026-08-04)

- [x] **T12 — Survey sibling doctrine, decide pack taxonomy**
  ✅ done 2026-08-04 — surveyed all six repositories and counted how often the same rule (or its renamed
  twin) appears. `skills-workflow` in five, `sensitive-info` and `no-global-memory` in four,
  `no-tmp-for-repo-files` in three. Taxonomy decided from that frequency rather than from taste.

- [x] **T13 — Reconcile the five core rules against sibling variants**
  ✅ done 2026-08-04 — four of the five confirmed by frequency; `no-global-memory` added as the sixth on
  the strength of four independent authorings.

- [x] **T14 — Write the stack packs**
  ✅ done 2026-08-04 — `windows-machine`, `dotnet-library`, `storage-sql`, each chosen because the first
  adopter would exercise it. Tests assert every canon file has complete frontmatter whose name matches
  its filename, every pack declares a description, and no canon file contains a machine path.

- [x] **T15 — Adopt into Lyntai and prove the collision path**
  ✅ done 2026-08-04 — four collisions surfaced and were resolved deliberately, with everything
  repository-specific preserved in a new local `repo-mechanics.md`. Lyntai's 1337 tests stayed green and
  `daoris check` exits 0. Three findings recorded in `TASKS.md` Part 2 rather than silently fixed: the
  `dev-conventions` overlap, renamed twins being invisible to the tool, and the budget overage.

- [x] **T16 — Release prep**
  ✅ done 2026-08-04 (partial) — `CHANGELOG.md` written, sensitive scan clean across every tracked file,
  final verify green. The two items needing an owner decision — the GitHub account and the LICENSE
  holder — remain open as `TASKS.md` REL1 and REL2.
