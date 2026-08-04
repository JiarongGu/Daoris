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

- [x] **T17 — `status` reports an available canon update** _(was TOOL2)_
  ✅ done 2026-08-04 — `status` may reach the canon and `check` deliberately may not (D8), so "a newer
  canon exists" is reported where it informs rather than in the gate, where it would fail a build for a
  reason unrelated to correctness.

- [x] **T18 — `upstream --all` promotes every drifted file** _(was TOOL3)_
  ✅ done 2026-08-04 — a working session usually improves several rules at once, and one command per file
  was friction on exactly the direction that has to stay easy: a return path that is tedious stops being
  used, and then local editing wins.

- [x] **T19 — `daoris doctor` reports suspected renamed twins** _(was ADOPT2)_
  ✅ done 2026-08-04 — `src/twins.mjs`, containment over significant-word sets rather than Jaccard so a
  short local rule restating a long canonical one still scores as a twin. Advisory by construction,
  always exit 0. Validated against the real case rather than only its fixture: run against the first
  adopter it independently reports the `dev-conventions` / `dotnet-package-layout` overlap at 58%, which
  is the finding that previously required reading the generated index end to end.

## Part 3 — the skills layer (2026-08-04)

- [x] **T20 — Development versions are 0.0.x; drift is measured against the lock**
  ✅ done 2026-08-04 — nothing was ever published, so a version boundary between two unreleased states
  bought nothing; the skills layer moved into the first release. Bumping the version surfaced a real bug
  behind it: `sync` compared on-disk content to the *current canon* rather than to the lock, so an
  improved canonical rule could not propagate — every consumer would have exited 1 over an edit nobody
  made. `docs/DECISIONS.md` D13. A version test now holds the four live refs in step.

- [x] **T21 — Survey twelve repositories for skills, decide the parameterization question**
  ✅ done 2026-08-04 — twice the scope of the original survey (134 skills), and deliberately including a
  daily-work Angular/React application outside the family's stack. Three skills appear in six
  repositories each; their copies span up to 6.6×. Reading the extremes settled it: the shared procedure
  is ~15 lines and the whole spread is each repository's own routing content, which no substitution map
  could supply. Parameter-free, delegating to the generated index — `docs/DECISIONS.md` D14. Evidence in
  the untracked `local/` survey.

- [x] **T22 — Skills as a third tier**
  ✅ done 2026-08-04 — `skills/<name>/SKILL.md` installs, retires and drift-checks like anything else,
  and core was restructured to `core/rules/` + `core/skills/` so one code path reads core and packs
  alike. The provenance header had to move under the frontmatter: the harness parses `description` to
  decide whether to surface a skill, so a comment at byte 0 would have made every canonical skill
  silently unreachable. Confirmed live — the harness listed both new skills with descriptions intact.

- [x] **CANON1 — `skills-workflow` is the most-duplicated and most-diverged rule in the family**
  ✅ done 2026-08-04 — the wider survey strengthened the case rather than weakening it: 6 of 11
  repositories, tying `sensitive-info` for the strongest signal, with variants from a 5-line note to an
  81-line blocking protocol. Canonized with the roster left to the generated index, because one
  repository's version names four discovery skills where another names three — a hard-coded roster would
  have been wrong on arrival.

- [x] **ADOPT2/T19 follow-through — `skill-loader` is not canon at all**
  ✅ done 2026-08-04 — present in six repositories, so it looked like the strongest possible pack
  candidate. Its body is "which skills does this repository have", which is generated content, so it
  became a table in `RULES_INDEX.md` instead. The hardest parameterization case disappeared rather than
  being parameterized.

- [x] **CANON5 — `caveman` is a core skill candidate, at five repositories** _(was: mis-filed doctrine)_
  ✅ done 2026-08-05 — the first reading, that it belonged in the assistant's global memory as a
  communication preference, was **wrong**, and correcting it produced the better rule. It is an output
  protocol, and its **carve-outs** are the canonical part: never compress a destructive or irreversible
  action, a security finding, or an order-sensitive sequence; never write a durable artefact in the mode
  at all. Global memory would have stripped exactly those — a fresh clone would inherit the terseness
  without the guardrails. `no-global-memory` gained the distinguishing test that fell out of it (would a
  fresh clone be defective without this?) and was promoted back through `upstream`, which is what exposed
  the return path still demanding `--force`.

- [x] **CANON4 (part) — `fix-log` canonized**
  ✅ done 2026-08-05 — three copies within 100 bytes of one another, so the invariant really was nearly
  the whole file; only the log's location and the sibling references were local. Written to say that the
  value is the *mechanism*, which version control cannot supply: a diff shows `<` became `<=` and never
  shows that the boundary was wrong because the timestamp was inclusive.

- [x] **TOOL4 (part) — `status` names what a pending update would change**
  ✅ done 2026-08-05 — the idea came from reading how a generated-wiki tool stays fresh (it diffs commits
  since its last run). The lock is a better marker than commit history: it records a per-file hash, so
  the answer is exact and survives a shallow clone. Bodies are compared with the provenance header
  stripped, so a pure version bump reports "version only" instead of listing every file — a list that is
  always long is a list nobody reads. Also fixed `init`/`status` not reporting a repository's own skills
  as local.

- [x] **REL2 — the LICENSE**
  ✅ done 2026-08-05 — MIT, copyright the repository's git author. It was open only because MIT requires
  a *named* holder and putting a real name in a tracked file was the owner's call, not a default anyone
  else should pick. The rehearsal's check was strengthened at the same time: what matters is that the
  licence **ships**, not that it sits in the checkout — `files` is a whitelist, so a licence declared in
  `package.json` and absent from the tarball would leave recipients without the terms.

- [x] **T23 — Rehearse the release against the packaged artefact** _(unplanned; from "test how this will
  work" before tagging)_
  ✅ done 2026-08-05 — `tools/release-rehearsal.mjs`, wired as `npm run rehearse`. Everything else tests
  the source tree; this packs the tarball, installs it into a clean repository, and drives the whole
  consumer lifecycle through the `bin` entry — adopt, collide, sync, drift, promote, upgrade, rename,
  check. 46 checks. It found a real bug on its first run: after `upstream`, a canon that then ships as a
  **new version** rewrote the provenance header, so the promoted copy differed from both the lock and the
  new content and `sync` refused — advising the contributor to promote an edit they had already promoted.
  Drift now compares bodies rather than whole files. It also correctly refuses to pass while REL2 is
  open, which is the point: the release gate should block on the release blocker.

- [x] **CANON4 (part) — `post-feature` canonized**
  ✅ done 2026-08-05 — the four copies looked least alike of any skill surveyed: one is a stack-specific
  checklist (migrations, DI, translation parity, component layering), another a detection procedure over
  the diff. The shared shape is the whole value — audit the real diff rather than your memory of it,
  close the wiring chain, refresh the records the change made stale, capture any reusable pattern it
  revealed, and report before committing. Written around the observation that every item on it fails
  *silently*, except pattern-capture, which fails for the opposite reason: nothing breaks, and the next
  person pays.

- [x] **TOOL4 — coordination across many developers, not just many repositories**
  ✅ done 2026-08-05 — the many-*developers* half needed no feature: doctrine is a tracked file, so a
  clone carries it, review touches it, and a move preserves it. That is precisely why a hosted shared
  space would be *worse* here, and it is the same argument `no-global-memory` makes one level down.
  What was genuinely thin was coordination, closed in two parts: `status` now names which files a pending
  update would change (computed from the lock, the idea borrowed from how a wiki generator diffs since
  its last run), and `canon/CHANGELOG.md` now carries **why** — printed for exactly the versions being
  skipped. Both stay offline, because the canon ships in the package (D11). Notification was deliberately
  not built.

- [x] **TOOL1 — `sync` cannot rename**
  ✅ done 2026-08-05 — solved by **detection rather than declaration**. The task proposed a `renamedFrom`
  field in `pack.json`, which would not have covered core (it has no `pack.json`) and would have been a
  second source of truth able to claim a rename that never happened. Pairing the delete with the add by
  content cannot lie about what moved, which is why version control has always done it that way. Reuses
  the containment function written for `doctor`, at a deliberately conservative 0.6 — below that it stays
  an honest "retire plus create". Reporting only: the outcome is byte-for-byte what it was.

## Part 4 — earlier release prep

- [x] **T16 — Release prep**
  ✅ done 2026-08-04 (partial) — `CHANGELOG.md` written, sensitive scan clean across every tracked file,
  final verify green. The two items needing an owner decision — the GitHub account and the LICENSE
  holder — remain open as `TASKS.md` REL1 and REL2.
