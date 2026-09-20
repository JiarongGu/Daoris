# Task archive

Completed work, with the date it closed and what shipped. `TASKS.md` holds only open items; this file is
the per-task record. Entries preserve their original wording so the archive stays faithful.

---

## Part 1 — v0.1: the CLI (2026-08-04)

Executed from `docs/archive/2026-08-04-daoris-v0.1-plan.md`, one task per commit, each a failing test first.

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

- [x] **REL1 — the GitHub owner**
  ✅ done 2026-08-05 — `JiarongGu`, confirmed against the public siblings rather than taken on trust:
  `github.com/JiarongGu/Shenora` and a release URL in Sonora both name it. The repository is
  `JiarongGu/**D**aoris` — capitalised, while the npm package is `daoris` — so a test now pins the exact
  reference and asserts the placeholder cannot ship. A lower-cased ref is the kind of mistake that works
  on a case-insensitive checkout and fails for everyone else. Version stamped `0.1.0` across all four
  live places at the same time.

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

- [x] **ADOPT5 — the `library-api-design` vocabulary cluster**
  ✅ done 2026-08-05 — read rather than retuned, and the answer was that the detector is right and the
  pack rule is not too broad. Of the four flagged documents exactly **one** is a genuine twin, and its
  own text gives it away: it says it was "adopted from the family's other library, where it's proven",
  and its headline restates the canonical rule's `enforces` line almost verbatim. The other three are
  different subjects — wire contracts, hosting invariants, mobile targets — that share library
  vocabulary because they belong to a library. A repository in one domain will always cluster around
  the canonical rule for that domain; that is a signal to read, not noise to tune away.

- [x] **ADOPT4 — the generated index was the largest always-loaded file**
  ✅ done 2026-08-05 — measured on the second-adoption rehearsal at 6,793 bytes, larger than any rule,
  with the skills table 46% of it. The cause was paying for a skill's `description` twice: it is the
  harness's **trigger** text, long by necessity because it must match however a person phrases a
  request, and the index was copying it whole into a file loaded on every session. The index is a
  *roster* — it answers "what is this skill", not "should this skill fire" — so it now carries a
  capped summary and the trigger stays in the skill. This repository's own core fell 19,291 → 18,308
  bytes on six skills; a repository with more saves proportionally more.
  Shipped with a bug and fixed in the same change: the first version cut on sentence boundaries and
  truncated a row at "e.g.", which reads as a complete thought that stops making sense rather than as
  a visible cut. A plain word-boundary cap has no such trap.

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

## `Daoris.Devkit` — the shared dev toolkit (2026-08-05)

- **DEV1 — decide how a repository declares its gates.**
  ✅ done 2026-08-05 — a separate `daoris.gates.json` the CLI never reads, recorded as **D26**. The
  manifest stayed data-only because every field in it is a noun; gates are verbs, and putting command
  strings into the file the CLI parses on every invocation makes the next reasonable-sounding step
  "since we already parsed them, let `daoris verify` run them". The manifest still pins the devkit
  version, so there is one place for *which* toolkit and one for *what it does*.
- **DEV2 — decide how the binary reaches a repository without losing the offline guarantee.**
  ✅ done 2026-08-05 — release assets, hash-pinned, explicitly acquired, recorded as **D27**. It stopped
  being a design question once the offline guarantee got its test: nothing under `src/Daoris.Cli` may
  touch the network, so the CLI *cannot* fetch a binary. Verification is a local digest compared against
  a local record — the same shape as `daoris.lock`. Distributing through npm was rejected because the
  artefact exists partly so a .NET repository need not carry a Node dependency for tooling alone.
- **DEV3 — extract the universal gates from the eleven copies.**
  ✅ done 2026-08-05 — four gates: `sensitive`, `version`, `docs`, `doctrine`. The sensitive scan was
  canonized from the one copy that had survived a real incident, keeping all four properties it had
  earned the hard way (paths scanned as well as content; fails closed without the private pattern list;
  renames counted; commit messages scanned) and adding redaction, because a gate that prints what it
  caught writes the secret to a build log. `doctrine` **delegates to `daoris check`** rather than
  reimplementing drift — a second answer to a question that already has one would be this project's own
  pathology committed by the tool built to remove it. `knowledge.mjs` needed no extraction at all: it is
  superseded outright by `daoris check` and `daoris index`.
- **DEV4 — mine the inherited devtools copy, then remove it.**
  ✅ done 2026-08-05 — 31 MB removed. Everything universal became a gate; the rest was the desktop
  sibling's capture and input tooling plus built binaries. Verified first that every copy still exists
  in the siblings, so the deletion lost nothing unique.
  Removing it also exposed a live bug: `.gitignore` listed .NET build output per tree by name, so the
  devkit's **397 build artefacts staged on its first build**. Now every tree is listed — deliberately
  not a blanket `bin/`, because `src/Daoris.Cli/bin` is the CLI's published entry point.

**Dogfooded end to end.** `daoris-devkit verify` runs 7 gates against this repository — the four
universal ones plus its declared `npm run verify`, `dotnet test src/Daoris.Service` and
`dotnet test src/Daoris.Devkit` — and exits 0.

Two things the first real run found, both fixed with tests:
- the `docs` gate compared **instants**, so a README and the code it describes committed hours apart on
  the same day failed the build. Technically correct and useless: that is what every working session
  looks like, and a gate that fires on normal work is a gate people route around. It compares days now.
- the `doctrine` gate assumed `daoris` was on `PATH`. A repository can now declare how to invoke it,
  which is what let this one point the gate at its own CLI in the source tree.

## The Lyntai adoption, closed out (2026-08-05)

- **ADOPT3 — Lyntai's changes were uncommitted, pending owner review.**
  ✅ done — found already committed as `a5009e9`, "adopt the canonical rule set via daoris; keep
  repo-specific mechanics local". The backlog entry had gone stale; the work had landed.
- **REL4 — Lyntai's manifest still names the `OWNER` placeholder.**
  ✅ done 2026-08-05 — `github:OWNER/daoris#v0.1.0` → `github:JiarongGu/Daoris#v0.0.1`. It was worse than
  a placeholder by then: the version reframe to `0.0.x` meant the lock pinned a canon version that no
  longer exists, and Lyntai had drifted seven files behind — the `skills-workflow` rule, the
  `model-decoupling` knowledge document and five skills had never reached it. Synced 17 files, no
  collisions, no drift, nothing retired.
- **ADOPT1 — `dev-conventions.md` substantially duplicated canonical `dotnet-package-layout`.**
  ✅ done 2026-08-05 — retired. **The budget gate forced it and was right to:** bringing Lyntai current
  pushed its always-loaded core to 40,517 against a 40,000 limit, and the 8.3 KB file that `doctor`
  had been reporting at 58% shared vocabulary was the largest thing in the tier.

  Retired rather than trimmed, after checking every section survived elsewhere: package structure,
  naming and variation points had become canonical outright; the LLM seam, storage, scorer and testing
  sections were already in `extending-lyntai.md`, `llm-and-router.md`, `storage.md` and `pitfalls.md`.
  Only two things existed nowhere else, and both moved into local `repo-mechanics.md` — the dev loop
  with its e2e discovery convention, and the **zero-`Dto`-identifiers** invariant, which is still true.

  Result: **40,517 → 33,596 bytes, a 17% cut**, `check` clean, 1563 tests green, and `doctor`'s 58%
  duplicate gone. The budget was tightened 40,000 → 36,000 to match the new reality rather than leave
  6.4 KB of silent growth allowed — the same reasoning that set it to 40,000 in the first place.

**Left uncommitted in Lyntai, deliberately.** That repository's own `CLAUDE.md` says "Never commit
without explicit user approval", and a rule does not stop applying because a different repository's task
list wanted the work done.

## CANON6 — the merged renamed twin (2026-08-05)

- **CANON6 — `scripts-live-in-repo` is a merged renamed twin of two core rules.**
  ✅ done 2026-08-05 — closed as **no new core rule**, with the finding written into the adoption
  playbook, which is where it is actually needed.

  The investigation had already concluded: both halves are canonical (`no-tmp-for-repo-files` and
  `file-tool-discipline`), and `doctor` cannot find it — 24% and 23%, inside the unrelated band, because
  it reaches both principles through a different vocabulary entirely. No threshold separates that from
  an unrelated pair (D17).

  What was still open was the *instruction*. The adoption document said "delete the twin", which is
  right for a reworded rule and **actively wrong here**: that file is also the only place documenting
  which allow-list entries exist and how a `cd` prefix defeats them, and deleting it would cost the
  adopting repository something it knew. §4 now covers both merged shapes, and states the real test —
  not "is this canonical now" but "is every line of it somewhere else."

  Immediately load-bearing: the Lyntai close-out on the same day retired an 8.3 KB rule by exactly that
  test, and the two things it found nowhere else were preserved instead of lost.

## REL3 — first push (2026-08-05)

- **REL3 — push, then decide the branch name first.**
  ✅ done 2026-08-05 — renamed `master` → `main` and pushed to `origin`. The remote was empty, so the
  rename rewrote nothing and broke no clone; after a first push it would have broken every one.
  `main` because GitHub creates new repositories with it and this repository's own notes already
  treated it as the default, leaving `master` the odd one out.

  **History was audited before the push, not just the working tree.** That is the one-way door: an edit
  hides a leak from the current checkout and does nothing about the copy in history, and after a push
  there are copies you do not control. All 15 patterns — 6 structural and 9 private — run against every
  commit message, every path that ever existed, and all 932 reachable objects across 73 commits. Clean
  on every axis.

  This is the audit the sensitive-scan's own documentation describes as "run at moments, not routinely:
  before making a repo public". `daoris-devkit` does not implement `--history` yet; it was done by hand
  here, which is itself the argument for adding the mode.

  Nothing is published to npm — the tag and the release workflow remain deliberately unrun while
  `Daoris.Web` does not exist.


## DEVKIT1 — `scan --history` (2026-08-05)

- **DEVKIT1 — `daoris-devkit scan --history`.**
  ✅ done 2026-08-05 — the audit mode, plus the acknowledgement mechanism its first run turned out to
  need. 39 devkit tests.

  Covers the three things the other scopes cannot: every reachable blob, every commit message, and every
  path any file ever had — a name can be the leak on its own, and deleting a file does not delete the
  name it had. One `git cat-file --batch-all-objects` process streams the object database rather than one
  process per object; this repository audits 788 objects and paths in about two seconds. Objects are read
  as **bytes**, not through a StreamReader: the stream interleaves binary blobs with the record framing,
  and decoding them as text desynchronizes the pipe, after which every byte is scanned as if it were
  something else. A malformed record throws rather than continuing, because a desynchronized scan reports
  nonsense findings.

  **Its first run found a real thing, here.** Blob `04801cb` — `SensitiveGateTests.cs` as it stood in
  `1d331cd`, before `469cc2d` assembled those fixtures at runtime — still carries a literal Windows
  user-home path and a `ghp_` token. Both are placeholders written to prove the scanner catches that
  shape, so there is no secret and no incident. But the working-tree scan is clean and the history is
  not, which is exactly the gap the mode exists to close, and it had already been pushed.

  That also showed the mode is useless without a way to say "read this, it is fine" — a permanently red
  audit is an ignored audit. So `sensitive.reviewedObjects` acknowledges an object **by sha**, and is
  **consulted for `--history` only**. That asymmetry is the entire safety argument, and the reason this
  is not the ignore-list rejected earlier the same day: a path-based ignore silences a *file*, so the
  next secret written into it is silent too, whereas a content hash cannot cover anything that does not
  already exist — a new leak is a new object with a new sha. Three tests pin it, including one asserting
  an acknowledgement does **not** silence the working tree.

  Acknowledged rather than rewritten: a history rewrite is the right answer to a real secret and a
  disproportionate one to a test fixture, and it would have broken a remote pushed an hour earlier.

## DEVKIT2 — the version pin, made real (2026-08-05)

Not a backlog item; found immediately after closing DEVKIT1 by checking whether the devkit's own claims
were enforced. The `devkit` field in `daoris.gates.json` was parsed and never read, under a doc comment
of mine claiming "the launcher enforces it" — describing a launcher that does not exist.

That is the same defect class as the two corrected in the morning's tidy-up: a documented guarantee with
nothing behind it, which is worse than no guarantee because it reads as verified and so nobody checks.
Three in one day is a pattern worth naming — **the claim and the enforcement are written at different
times, and only the claim is easy.**

✅ done 2026-08-05 — `VersionPin.Require`, checked **before** any gate runs rather than alongside them:
if the toolkit is the wrong one the gate results are not trustworthy, and reporting a mismatch after
printing seven confident lines is backwards. Exit 2, not 1 — the devkit could not run as configured,
which is not the same as a gate finding something. Verified by pinning a version this binary is not and
watching it refuse. Optional by design: an unpinned declaration is allowed and silent, the same
reasoning that lets the manifest default its harness.

## `claims-need-checks` — new core knowledge (2026-08-05)

Not a backlog item; written because the same defect appeared three times in one day — the offline
guarantee that no test asserted, the "the launcher enforces it" comment describing a launcher that did
not exist, and a config field parsed and never read. Each read as verified and none was.

✅ done 2026-08-05 — and **the budget gate decided its tier**, for the second time. Filed as a core rule
it put a realistic adopter (core plus one pack) at **24,061 bytes against the 24,000 default — 61 over**.
Shaving 61 bytes to squeak under would have been gaming the gate; raising the default would have hidden
what it was reporting, which is that core is full.

Two things pointed the same way. Its trigger is *writing a claim about behaviour*, not every task — the
same distinction that demoted `model-decoupling`. And three instances in **one** repository is short of
the bar core is held to: canonical rules are the ones several repositories reached independently. So it
is knowledge, read on demand, costing nothing always-loaded.

`doctor` then flagged it against local `adoption.md` at **31%**, just over the 0.3 threshold. A false
positive — an adoption playbook and a documentation-discipline note — and a useful one: D17 records that
word overlap misses real twins written in different vocabulary, and this is the same limitation from the
other side. Both documents were written the same day, which is the whole explanation for the shared
words. The tool hedges correctly ("advisory only… if they genuinely differ, ignore this").

## The Shenora rehearsal, and what it sent back into the canon (2026-08-05)

Unblocked by copying two siblings' `.claude/` trees into a gitignored scratch consumer rather than
waiting for their working trees to be clean. That turned the adoption into what it was always supposed
to be — **a source of canon improvements, not only a consumer of them.**

- **CANON2 — `web-webview`, the first of the pack candidates.**
  ✅ done 2026-08-05. Written from two applications that derived the same invariants from different
  symptoms: one profiling a window that froze under load, one debugging thumbnails that were merely
  slow. The rule carries the four that fail *silently* — answer requests off the UI thread with a
  response object rather than materialized bytes, the browser object is thread-affine, publish anything
  served from disk atomically, fail closed on init and health checks. The knowledge document holds the
  rest.

  **Validated by adoption**, which is what CANON2 asked for: installed into the rehearsal consumer,
  `doctor` reports that repository's own 21.6 KB hosting document as **64%** covered by the canonical
  pair. A pack nobody installs is unvalidated doctrine; this one is not.

- **The convergence that proved D17 on a live pair.** `claims-need-checks`, written that morning from
  three instances in *this* repository, had already been derived by a sibling from the opposite end —
  auditing shipped API documentation against its own source. The two drafts shared **25% vocabulary**
  against a 30% threshold, so neither could ever have found the other. That is D17's claim — word
  overlap detects restatement, not convergence — demonstrated rather than argued, and it is exactly the
  case the service's semantic pass exists for. After merging both, the canonical document matches the
  sibling at 49%: **canonizing a convergence is what makes it findable afterwards.**

  A cost came with it. The merged document draws on two vocabularies and now matches two *unrelated*
  documents at 46% and 31% — false positives that did not exist before. Breadth buys recall and pays in
  precision, which is the same trade D17 records from the other side.

- **`leak-repair`** — new core knowledge, assembled from three siblings' versions including one written
  during a real purge. The traps that cost the most: **the backup bundle taken first is itself a
  complete copy of the leak**, the rewrite tool usually strips the remote and tags need pushing
  separately, and a clean scan deserves the same suspicion as a passing test.

- **`windows-machine` gained two traps** that pass silently: PowerShell 5 unwrapping a nested array of
  exactly one element (so a single-pair find-and-replace rewrites one *letter* everywhere, while two or
  more pairs behave — which is what hides it), and a working directory past the path-length limit
  failing as *corrupt input*.

**A finding about the default budget.** The 24,000 default cannot cover core plus two packs plus a
repository's own rules — the rehearsal needed 40,000 for core, three packs and its own material, of
which the generated index alone is 5,956 bytes. Two repositories now run well above the default, which
suggests the default is sized for a repository with no packs rather than a realistic adopter.

**Also noticed, not acted on:** two siblings independently keep a `TEMPLATE.md` inside the always-loaded
rules directory. It is scaffolding for authoring a *new* rule, not a rule, and it is paid for on every
task. Worth raising at their next adoption rather than editing from here.

## CANON2 — `durable-jobs`, the second pack (2026-08-05)

✅ done 2026-08-05 — written from the strongest agreement the family survey has produced: **three
applications built a durable job system independently, and two named the file identically.** A fourth
signal sits underneath it — `background-task-tracking.md` exists under that exact name in two of them.

The rule carries what all three converged on: dispatch rather than await, checkpoint so resume is cheap,
bound capacity **per lane** rather than globally, and add a kind as a handler plus a registration. The
knowledge document holds the shape underneath — one consumer loop over a mailbox instead of a task per
item, a container job that is bookkeeping and is never dispatched, and backing off from measured
pressure rather than a guessed constant.

**The crash-loop guard is the best evidence in the pack.** One repository hit it — a GPU-heavy job that
killed the process, retried on the next start, and killed it again. Another had already written the same
gap down as an open risk *before* it happened to them. A prediction and an incident, in two repositories
that could not see each other, is about as strong as this evidence gets.

**Honest about its status: not validated by adoption.** No repository has installed it. `web-webview`
was — `doctor` measured it at 64% coverage of a real repository's own document during the rehearsal —
and this one is doctrine argued from evidence rather than proven in use. Packs are opt-in, so an unused
one costs nobody anything, but the distinction is worth keeping rather than blurring.

**A measurement worth recording.** Containment against the three sources runs 26–45%, far below
`web-webview`'s 64%, and that is the intended outcome rather than a weakness. Those documents are dense
with class names, commit hashes and specific job kinds; the canonical version strips all of it, so the
shared vocabulary drops even where the principles match exactly. **Word-overlap coverage is a poor proxy
for whether a pack captured the right ideas** — the third time in two days that this measure has been
right about restatement and wrong about meaning (D17).

## CANON4 — the `doc-*` family, decided (2026-08-05)

✅ done 2026-08-05 — **not canonized**, recorded as D29, and the half of it that was worth keeping is now
enforced rather than available.

It had been held on the argument that these skills automate hand-maintaining documents a generated wiki
would own outright, so canonizing them would install doctrine for a workflow about to change; the
backlog predicted that if the generated route won, "what stays canonical is much smaller — the *review*
of generated output, not its production." Reading the six settles it, and that prediction was right.

Production is repo-specific or superseded: one skill writes into two documents belonging to a single
repository, and the shrink/cleanup pair maintains hand-written prose — the work a generator removes
rather than automates. Review had already become gates here without anyone connecting it: of
`doc-monitor`'s four checks, redundancy is `daoris doctor`, index and skill staleness is `daoris check`,
and version disagreement is the devkit's `version` gate.

**The fourth was a genuine gap** — nothing verified that a link between documents resolves. That is the
cheapest documentation check there is and the one most worth having, because a link to a renamed file is
*silently* wrong: nothing compiles it, the page still renders, and the reader concludes the target was
never important. Now the devkit's `links` gate, 8 tests, running as the fifth universal gate. 21 links
across 60 documents here, all resolving. Verified by adding a broken link, watching it fail, and removing
it.

**A note on the evidence.** The backlog recorded this family as appearing in three repositories. It is
two with the identical six skills, plus a third with two differently-named ones. Worth writing down
because the two-repository bar is what makes canonical content trustworthy, and a count that drifts
upward in the retelling is how a bar gets quietly lowered.

## The service, validated end to end (2026-08-05)

Not a backlog item — the opportunity appeared during the Shenora rehearsal and was too good to leave.

Canonizing `claims-need-checks` turned up a sibling that had derived the same principle independently,
from the opposite end. Word overlap scores the two at **25%**, under the 30% duplicate threshold, so
`doctor` cannot see them and no retuning would help: at 25% a real twin is indistinguishable from an
unrelated pair. That is D17's claim, and until now it had only ever been argued from a survey.

Indexed into `Daoris.Service` against a local embedding endpoint, the semantic pass reports **exactly
that pair at 0.785**, labelled *Convergent — same lesson, different words*. It discriminates too:
nothing at a 0.82 threshold, only the true pair at 0.70, and an unrelated storage document joining at
0.60 — the precision/recall curve behaving as it should, and support for the threshold having no clever
default.

**This is the first end-to-end evidence that the service does the thing it was built for**, and it is
not a constructed test. The convergence was found by hand during an adoption; the tool then found the
same pair without being told what to look for. It confirms D24 from both sides as well — convergence
returns copies and restatements with no model at all, and the model adds only the class that text
comparison provably cannot reach.

Run through the MCP server over stdio against a two-repository fixture, with two unrelated documents
included so that a detector flagging everything would have been caught.

## `Daoris.Web` — the fourth artefact (2026-08-05)

✅ done 2026-08-05 — a React application over the service, served by a new `Daoris.Service.Http`, with
both of the brief's open questions settled as **D30** and **D31**.

**Convergence is the landing view, not search.** The brief suspected search was the obvious answer and
the wrong one; a day of use settled it. The finding that mattered most all session was a convergence
between two documents sharing 25% vocabulary, and no search could have surfaced it — to search for it
you must already know it exists. Search is the second tab, for when you do.

**It reads and proposes a command.** No editing from the browser: `upstream` routes an improvement
through the repository that found it, where review happens, and a web editor would beat that path for
the wrong reason. The convergence detector already says this for itself (D21), and a UI that could apply
its own suggestions would contradict the component it is built on.

**A browser cannot speak stdio MCP**, so the HTTP host is new. Adding it meant the composition was about
to be written twice, so `ServiceFactory` now owns it and the MCP host was rewired onto it — two copies of
"which tier is active" would drift, and one would end up quietly lexical-only while reporting otherwise.
Writing that inside this project would be worse than finding it anywhere else. The provider stays in the
hosts: Core holds `IEmbedder` and nothing that implements one, and the build caught me breaking that.

**Verified in a browser, not asserted.** 449 entries from 11 repositories; real groups — `phase-review`
across two at 0.947, `test-coverage-priorities` at 0.940, `doc-loader` across three at 0.913.

Two defects the live run found, both real and neither visible from the code:

- **Convergence took 31 seconds, every single call.** `KnowledgeService` constructed a new
  `ConvergenceDetector` per request, throwing away the vectors it had just computed. Since the
  interesting interaction is moving a threshold and looking again, that made the re-embed the common
  path rather than the rare one. One detector, held: **31s → 5.3s warm.** A content-keyed memo inside
  the detector was added first and did nothing at all until the per-request construction was fixed —
  a fix that could not work, on a cause I had not yet found.
- **`ComposedService.Convergence` was a detector nobody called.** The factory built one and the service
  built its own; the field looked like the answer and was not connected to anything. Removed. That is
  the "parsed and never read" case `claims-need-checks` names, found in code written the same day as
  the rule.

## The quest system, and a rule I had already broken (2026-08-05)

Formalized from the way the family was already working: repositories are not developed across, and a
change one needs from another is posted to that repository's backlog. `daoris quest post`, then `take`,
`done`, `decline`, `list`. Recorded as **D32**, with `repository-owns-its-work` as the canon rule.

**The rule found a violation in this same session's work.** Earlier today Daoris edited Lyntai directly —
corrected its manifest, synced 17 files, retired an 8.3 KB rule — and reported it as done-but-uncommitted.
Every one of those changes was correct and every one was made by the party that knew that codebase least.
The right shape was a quest with the evidence in it, letting whoever works there decide. Quest `#7a82cc`
now says exactly that, including that `git checkout -- .` is a perfectly good answer.

**CANON3 was never blocked either.** The backlog said it was waiting on Shenora's tree to be clean. It was
waiting on Daoris not having a way to ask. Quest `#ee8994` carried the entire rehearsal — 6 collisions, 2
twins, the local mechanics to preserve, the budget, `check` clean — so taking it was mechanical.

**⚠ Both quests were withdrawn hours later**, and CANON3 reopened. Writing them into those repositories'
backlogs was itself the violation the quest system exists to prevent; the corrected design holds quests
in the service and has repositories pull them. See the entry below, and D32's amendment.

Two things worth keeping from building it:

- **The name does work.** Every backlog here is already full of tasks, so "task" or "request" would be
  ambiguous in the one file where the distinction matters. "Quest" cannot be confused with local work, and
  it is *taken* rather than assigned — which is the property that keeps declining a real answer.
- **The shape has to be boring.** A quest is an ordinary checklist item: the checkbox is the coarse state
  every backlog already reads, and the italic line carries asker, date, status and reason. A repository
  that knows nothing about Daoris handles one correctly, which is the only way this spreads.


## Quests corrected — a service responsibility, not an agent one (2026-08-05)

The first implementation had `daoris quest post <path>` write the quest straight into the receiving
repository's `TASKS.md`. **That is the very thing the rule it shipped with forbids.** An outside edit is
still an outside edit when it is one file and uncommitted, and it still arrives from the party that
knows that codebase least. The tooling for the rule broke the rule — the most embarrassing way to find a
design error and the most convincing.

It was also incompatible with **D8**. Reaching a central store means the network, and nothing under
`src/Daoris.Cli` may open a socket, enforced by a test added the same morning. The CLI could not have
been the client for this even if writing into a sibling had been acceptable.

**Corrected.** Quests live in the service and are *pulled*: an agent publishes through `quest_publish`,
the receiving repository's own agent reads what is addressed to it via `quest_list` and answers with
`quest_respond`. Whether it becomes a line in that repository's backlog is that repository's decision,
made by that repository. The CLI has no quest command and stays the offline doctrine tool it was.

**Adoption is the gate.** Only a repository the index knows has adopted can be addressed — one without
the client cannot see the quest, and an unread quest is indistinguishable from an ignored one.

Stored beside the index in the same database. Quests are service state as the index is service state,
and two files would be two things to back up and two that can disagree about which repositories exist.

**The two quests I had already written into siblings were removed**, and I am not touching those
repositories again — which is the rule, applied to myself. Removing one of them, my own script
over-deleted and I restored the file from HEAD rather than trying to repair it by hand.

## The registry — what makes a quest addressable (2026-08-05)

Quests alone were not enough. Without knowing what a repository owns, an agent publishing one is
guessing what the other side does — the same *"the knowledge does not travel"* problem the arrangement
exists to solve, moved one step earlier.

Each repository now declares a `domain` in its manifest: a summary, the areas it **owns**, the kinds of
quest it **accepts**. The service reads those while indexing and serves them as a registry, recorded as
**D34**.

**Search answers "has anyone solved this"; the registry answers "whose problem is this."** Only the
second tells you where a change belongs, which is what the quest system needed.

Declared in the manifest because it is data — nouns, what the repository *is* — so it belongs where
D26 already put the inert half. Next to the thing it describes, reviewed by the people it describes; a
central list would drift the moment a repository changed.

Three properties, each with a reason:

- **Adoption gates addressing, declaration does not.** Publishing to a non-adopter is refused and names
  who *is* addressable. Publishing to an adopter that declared nothing succeeds with a warning — refusing
  until a form is filled in would make adoption a chore, and this all rests on adoption being easy.
- **Non-adopters are listed and marked.** "Who cannot be asked yet" is the same question as "who can".
- **An unparseable manifest still appears.** That is the repository's own problem and its own tooling
  will say so; it is not a reason to drop it off the map.

**Driven end to end against the real family**: 458 entries from 11 repositories, Daoris registered with
its own domain, Lyntai shown as adopted-but-undeclared, ten non-adopters listed as unaddressable, a
publish to one refused by name, and a publish to Lyntai accepted with the caution. **Nothing was written
into any repository** — which was the whole point of the correction.

One thing to watch: the release rehearsal reported 45/52 on a single run and 52/52 on the two after it,
with nothing changed in between. Recorded rather than explained; a gate that fails once and passes twice
is a gate worth watching before it is trusted.

## Hardening the day's own failures into doctrine (2026-08-05)

The point of Daoris is codifying how the work is done, so the session's own mistakes are the material
rather than a postmortem. Three went into the canon and one into a gate.

**`repository-owns-its-work` became absolute.** It said "narrow exceptions: initialization, and a change
so coupled that splitting it would leave neither side working". With a formal quest system neither
survives — a coupled change is two changes and a quest. It now reads **never write into another
repository**, with a request to do otherwise being the user's call to make explicitly rather than a
judgement to reach alone.

**`reaching-in` is new core knowledge**, kept because the failure was not the first edit but everything
after it. Feedback from the affected repository's own session said it plainly: its backlog was rewritten
three times, once silently discarding a commit-ready edit.

The lessons are about reasoning, not tooling. A tree-state observation **expires the moment you look
away** if anyone else is working there — the agent checked "clean" at session start and relied on it an
hour later. The repair is **another outside edit** with less information against a tree that has moved.
And **never revert a file you do not own**: reverting is not an undo, it discards uncommitted work you
cannot see, and that rule was already canonical elsewhere and known.

**`claims-need-checks` gained "how a check passes without checking"** — the four shapes hit for real
this session: the sabotage that did not apply, the sabotage in a form the check does not look for, the
artefact that was not the thing that ran, and the runner that quietly saw fewer inputs. Every one was
green first.

**And a gate, because doctrine that only describes a violation is weaker than one that prevents it.**
`containment.test.ts` runs every command against one repository with a sibling beside it and requires
the sibling byte-for-byte unchanged. Verified by sabotage twice: the first attempt also broke the
command under test, so its 10 failures proved nothing about containment; a surgical version failed
exactly one test, which is the proof.

**The general form, worth carrying:** a tool that enforces a rule is the most likely thing to break it,
because whoever builds it is thinking about the mechanism rather than the principle. This edit was made
by the feature that shipped the rule against it, in the same change.

**A note on the budget gate.** The hardened rule reached 5,191 bytes — nearly double the next-largest
core rule — and `check` refused it. The fix was to split principle from incident, not to raise the
limit: the rule is 2,610 bytes and the narrative is on-demand knowledge. The gate did exactly what D28
said it was for.

## CANON2 — `desktop-app`, the third pack (2026-08-05)

✅ done 2026-08-05 — written from the convergence the tool found rather than the one a filename search
missed. Three applications had arrived at the same practice independently, under names no glob would
have matched.

The rule carries what they agreed on, and the sharpest of them is **synthetic input does not prove an
interaction**: dispatching a click bypasses hit testing, focus, z-order and pointer capture, which is
exactly where interaction bugs live. A synthetic event proves a handler runs, which is rarely the thing
in doubt. Alongside it: capture before and after, verify against the real backend at least once, and
**know which instance you are attached to** — a stale bundle, an orphaned process from the last run, or
a second window with no debugging target each give a real answer about the wrong thing, and nothing in
the output says so.

The knowledge document holds the loop: one long-lived instance rather than a relaunch per change, a
**randomized** debugging port because a fixed one does not fail on collision but attaches to the wrong
process, and selectors that survive a rebuild since bundler-generated class names are hashed.

**Not validated by adoption**, the same honest status as `durable-jobs`. Containment against the three
sources runs 19–41%, well below `web-webview`'s 64%, and for the same reason: those documents are dense
with ports, paths, class names and commands that the canonical version strips. Word-overlap coverage
keeps being a poor proxy for whether a pack captured the right ideas.

Only `desktop-winforms` remains, and it stays below the two-repository bar.

## CANON3 — reclassified, not completed (2026-08-06)

Moved out of the backlog into the quest ledger at the top of `TASKS.md`, because it was never a Daoris
task. It is work for whoever maintains that repository, and Daoris's part — the rehearsal that makes
taking it mechanical — has been done for a day.

Keeping it as a numbered backlog item made it look like something Daoris would eventually do, which is
exactly the confusion `repository-owns-its-work` exists to prevent. `REG1` folded in the same way: it
was Lyntai's work described from the outside, and it is now stated as the quest it actually is.

**The ledger exists because the quest system is built and not deployed.** Nothing runs between sessions,
so nothing can be published or pulled. Rather than calling the work blocked, this repository holds it in
its own backlog — the one file it is always allowed to write — until there is somewhere to send it.

The ledger says so explicitly, because the tempting shortcut is the one already taken once: an outbound
quest waits to be published, it does not get hand-delivered into a sibling.

---

# Handover — end of 2026-08-05

**Start with `CLAUDE.md`, then `TASKS.md`.** This section is orientation: what changed, what is load-
bearing, and what the open items are actually waiting on.

## What exists

All four artefacts. `Daoris.Cli` (TypeScript, nine commands, zero runtime dependencies),
`Daoris.Service` (index, convergence, quests, registry — MCP and HTTP), `Daoris.Devkit` (one AOT binary,
five universal gates), `Daoris.Web` (convergence-first, read-only). Only `Daoris.Desktop` is a brief.

**Gates:** `npm run verify` · `npm run rehearse` · `dotnet test src/Daoris.Service` ·
`dotnet test src/Daoris.Devkit` · the devkit's own `verify`. All green at 120 / 70 / 57 / 52-of-52 /
8-of-8, with zero type errors in both TypeScript projects.

## What changed most, and where the reasoning is

The day reshaped the model three times. Read these before touching the corresponding area:

- **D32 (+ its amendment) — cross-repository work is a quest, and quests belong to the service.** The
  first implementation wrote into the sibling's backlog, which is the violation the rule exists to
  prevent. Quests are now published to the service and *pulled*.
- **D34 — a repository registers what it owns.** Declared in its manifest's `domain`; the service reads
  it. Search answers "has anyone solved this", the registry answers "whose problem is this".
- **D35 — `connect` is the one online command, and D8 was over-broadened.** D8 is about `check` working
  offline; it had been widened to "nothing in the CLI may open a socket", which would have made a client
  impossible.
- **D33 — the CLI is TypeScript.** Dev loop needs no build (Node strips types); only publishing
  compiles, because a consumer's Node may be 22.

## Three constraints that are easy to trip

- **Never write into another repository.** Absolute (D32). `containment.test.ts` enforces it: every
  command runs with a sibling beside it and the sibling must be byte-for-byte unchanged.
- **The core budget has 432 bytes left** (23,568 / 24,000). Split principle from detail; do not raise
  the limit (D28).
- **Edit files with the edit tool, not scripts.** Seven corruption events in one session came from
  scripted rewrites — mangled escapes, a stray control byte, an eaten declaration, and twice a pattern
  that silently matched nothing.

## The open items, and what each waits on

**`TASKS.md` opens with a quest ledger**, because the quest system is built and not deployed. Two
outbound quests are prepared there — the Shenora adoption and Lyntai's domain declaration — waiting on
somewhere to publish them. They are *not* Daoris tasks, and they are not to be hand-delivered.

| Item | Waiting on |
|---|---|
| `SVC1` | Nothing runs between sessions, so nothing can be published or pulled. The ledger holds what would have been sent. |
| `REH1` | Catching the rehearsal failing again — **capture the log before re-running.** |
| `CANON2` | A second repository needing `desktop-winforms`. Below the bar until then. |
| `HARNESS1` | A repository that actually wants a second harness. |

## The habit that paid best

**Watch a check fail before trusting it.** It caught a memo that changed nothing, an import-walk that
missed bare imports, a build emitting to the wrong directory while silently running sources, and a test
glob that dropped a file on Windows. Every one of them was green first.

And the pattern behind most of the day's mistakes: **a tool that enforces a rule is the most likely
thing to break it**, because whoever builds it is thinking about the mechanism rather than the
principle. It happened three times — the quest writer, the over-broadened guarantee, and a detector
nobody called. Each was found by *using* the thing, never by reading it.

---

## SVC1 — the service is deployable (2026-09-18)

✅ done 2026-09-18 — closed without a daemon, recorded as **D36**, because "nothing runs between
sessions" turned out to be two different gaps and a daemon would have fixed neither.

**State that did not survive:** registrations pushed by `daoris connect` lived in a dictionary, so a
service restart silently dropped every repository that had ever connected — and for a remote service,
pushed registrations are the only registrations there are. `RegistrationStore` now persists them in
the same SQLite file as the index and the quests, and the factory loads them before the first read.

**A door that did not exist:** quests could only be moved over MCP stdio, so a deployment anywhere
else was a read-only mirror. The HTTP host now carries `POST /api/quests` and
`POST /api/quests/{id}/respond`, and `DAORIS_SERVICE_KEY` gates every `POST /api/*` when set (absence
means local trust, D21). The publish/respond judgement — who is addressable, what a refusal says,
what declining requires, including the message text — moved into one `QuestExchange` used by both
hosts, so the same ask cannot be deliverable through one door and refused at the other.

**Local mode needed no daemon at all**, only a launch story: the MCP host is spawned per session and
the database persists, so every repository's session on this machine shares one store. `.mcp.json`
now registers it here; a sibling adds the same entry to its own file.

Verified on the artefact, not only in the 11 new tests (81 service total): host driven live with a
key — unauthorized write answered 401, `daoris connect` registered through the real endpoint, the
host was killed and restarted and both the pushed registration and a taken quest were still served,
and a publish to a non-adopter was refused naming who *is* addressable. The live run also confirmed
Lyntai listing as `adopted=false` — the de-adoption, observed at runtime.

**Consequence:** the quest-ledger pattern (outbound quests held in `TASKS.md` "until a service runs")
ends, because the service runs. Addressing still gates on adoption, so nothing is publishable *to
Lyntai* until it re-adopts — held as `LYN1` rather than delivered anywhere.

## D37 — automation-first, and the setup reworked for an agent operator (2026-09-18)

✅ done 2026-09-18 — set by the owner: the family moves to fully automated development via code
generation, the human at the initial target and the final verification. Recorded as **D37**; shipped
as canon core knowledge **`autonomous-development`** (knowledge, not a rule — the same demotion
reasoning as `model-decoupling`, and the budget said so: the core sits at 23,988 of 24,000 after its
index row). The adoption playbook was rewritten around the two human checkpoints — "adopt Daoris
here" at the start, the uncommitted diff at the end — with everything between executed by the
adopting repository's own agent from `analyze --json`. The carve-outs moved nowhere: destructive,
irreversible, cross-repository, publishing and committing stay explicitly human, and "never commit
without approval" *is* the final checkpoint.

Canonized on the owner's direction rather than two-repository convergence, and the changelog entry
says so — the evidence bar matters (D29), and an owner setting the target for the family is the model
in action.

**Amended the same day:** the owner moved the commit gate too — commits land automatically per task
once gates are green, and the human checkpoint is the review of the landed history plus the outward
boundary (push, publish, release, history rewrites, cross-repository writes, destructive actions).
See D37's amendment; the sentence above that made the commit itself the checkpoint is superseded.

## TOOL5 — `status --json`, the agent operator's last prose surface (2026-09-18)

✅ done 2026-09-18 — under D37 an agent drives setup end to end; `analyze --json` already covered
adoption and `check` speaks in exit codes, which left `status` as the one surface an agent still
parsed as prose. The facts — packs, sync state, budget, drift, locals, and any pending canon update
with its changed/new/retired lists and changelog notes — are now **computed once and rendered twice**,
as JSON or as the unchanged text, because two paths that compute separately are two paths that can
disagree about whether an update exists (the same reasoning that gave the service one `QuestExchange`
for its two hosts). 121 CLI tests; the human output is byte-identical to before.

## WEB1 → D38 — the platform: Quests and Projects join the UI (2026-09-19)

✅ done 2026-09-19 — WEB1 asked for a quests view; the owner's direction widened it into **the
platform**: a task / knowledge / setup window over the service, designed before it was built
(`docs/2026-09-19-platform-design.md`, D38). `Daoris.Web` gained **Quests** (outstanding first,
publish, take / done / decline — through the same key-gated endpoints and the same `QuestExchange`
judgement as every other door, refusals shown verbatim; the form cannot offer the mistakes the service
refuses) and **Projects** (the registry with the non-adopters marked rather than hidden, join steps
proposed as text, never a button). Doctrine stays unwritable from every view — D31's reason was never
about quests, and D38 says so precisely. Convergence keeps the landing (D30 stands). Desktop remains
the same build in the desktop sibling's shell, later, unchanged.

## The example family, and the family rehearsal (2026-09-19)

✅ done 2026-09-19 — `examples/engine` and `examples/game`, two complete miniature adopters tracked in
full (manifests with declared domains, synced doctrine, a local document each, READMEs), and
`tools/family-rehearsal.mjs` driving the **routing** lifecycle the release rehearsal never covered,
through the real artefacts: doctrine current and clean in both — a canon change that forgets to
re-sync the examples fails the gate — the HTTP host over a scratch store rooted at `examples/`, both
registered through the real `daoris connect`, a quest published `game → engine`, refused toward a
stranger naming who *is* addressable, declining refused without a reason, taken, finished, still there
after a restart, and `game`'s own knowledge answering a search made from outside it. **22/22 on the
first full run**, transcript captured like every rehearsal. `release-prep` now rewrites and checks the
example manifests' pins, so a version bump cannot leave an example teaching a stale ref.
`examples/README.md` is the setup story for the next real family — recorded as **D39**.

## D40 — the management UX: Overview lands, and the review caught two real defects (2026-09-19)

✅ done 2026-09-19 — the owner's direction: the platform is mostly for the person, so it gets a real
management UI. Landing moved to **Overview** (recorded as **D40**, with D30 standing for the knowledge
half): family health as stat tiles, the outstanding quests oldest-first with sitting time, the
repositories as single-hue bars — built to the visualization discipline (one series one hue, values in
ink, proportional figures on tile values, status never color-alone). Quests regrouped by life stage
with the publish form behind a deliberate action; Projects turned declarations into scannable chips
with the local/canonical split; the nav carries an outstanding-count badge.

**Reviewed in the browser over the real family, and the review paid twice.** Launching the host the
documented way exposed that both its defaults were untrue — port and family root — fixed so the
one-command story holds with no environment at all. And the Overview itself served a **ghost**: a
repository renamed weeks earlier still in the index, because refresh replaced what it saw and never
retired what it did not. Fixed red-first (`RefreshTests`), guarded against the mis-set-root case, and
proven on the real store: 16 → 14 repositories, only what is on disk. Both fixes opened
`docs/FIX-LOG.md`, which the service now indexes like every sibling's.

# Handover — end of 2026-09-19

**The next session starts at `TASKS.md` DRV1** — designing the driver. The direction changed at the end
of this stretch and is recorded as **D45**: Daoris becomes the main driver for all projects —
centralized workflow management, triggering and coordinating the agent sessions (claude/codex,
adapter-based) that take the quests, one session per domain-owning repository. Three parts: the
per-repo **connector** (built: CLI, canon, MCP, join lifecycle), the **local driver** (`Daoris.Desktop`
re-scoped — brief rewritten, design first), the **remote server** for teams (later; old SVC2 folded into
DRV3). Read D45, D37, D32/D33 and `src/Daoris.Desktop/README.md` before designing.

**State at handover.** Everything committed, tree clean, ~34 commits ahead of origin (push is the
owner's). All 9 devkit gates green: CLI 121, service 83, devkit 57, web 16 unit + 6 Playwright;
release rehearsal 52/52; family rehearsal **29/29** including a project born mid-run and joined through
the real CLI (D44). The service ships as executables — `npm run publish:service -- --install` lands
both hosts in `~/.daoris/bin` and prints the ready `.mcp.json` snippet (D43); the platform the owner
uses runs from that installed copy, which also ends the build-lock dance with running hosts. The
platform (D38/D40/D41/D42) speaks en + 简体中文, is built on Tailwind v4/Radix/Query/i18next with
Storybook as the design tool, and this repository's own sessions reach the service over MCP via the
committed `.mcp.json`.

**Constraints easy to trip.** The always-loaded core is **23,988 of 24,000 bytes** — the next canon
addition fails the gate even as an index row; split, don't raise (D28; CANON5 waits on exactly this).
A canon change must re-sync `examples/` and this repo's own `.claude/` in the same commit — the family
rehearsal enforces the first, `verify` the second. Do not tag a release while REH1 is open. Never edit
the version or stamp a changelog heading by hand. And never write into another repository — the driver
direction makes this *more* load-bearing, not less: orchestration is central, work never is.

## D43 + D44 — the server ships as executables, and the loops create their consumer (2026-09-19)

✅ done 2026-09-19 — both raised by the owner in one message: no published server executable, and no
e2e that *creates* its example project. **D43:** `tools/service-publish.mjs` publishes both hosts
self-contained single-file (`IncludeNativeLibrariesForSelfExtract`, because "single file" otherwise
leaves `e_sqlite3` beside the exe — the installed copy died on first store open, recorded in the fix
log); `--install` lands them in `~/.daoris/bin` and prints the `.mcp.json` snippet with the family
root filled in; the release workflow ships them per platform with sha256s beside the devkit. The hosts
became safe to run from anywhere — the HTTP host resolves its content root beside its executable, the
MCP host warns on stderr when no root is named instead of silently indexing the wrong tree. Verified
by running the installed binaries from a neutral directory, asserting on behaviour rather than on the
process staying alive (a stdio host under a null stdin exits immediately and *cleanly*).
**D44:** both loops moved onto a scratch copy of the examples — tracked examples stay a currency gate,
never dirtied — and both now include the birth: the family rehearsal grew to **29 checks** (init →
declare → sync → check clean on first contact → connect → the registry knows three → a first quest
reaches the newcomer and is answered → the newcomer survives the restart), and the Playwright suite to
**6 tests** (the project created mid-test appears as a member in Projects and is quest-addressable
from the compose drawer, 1.2 s for the whole birth).

## The pyramid's inner loop — Vitest under the Playwright suite (2026-09-19)

✅ done 2026-09-19 — the outer loop (Playwright over `examples/`) proves the flows but needs builds;
the inner loop answers in milliseconds. Vitest + Testing Library in jsdom, inline in the vite config
with the bilingual sibling's three proven shims (the Node ≥ 22 localStorage shadow, ResizeObserver for
Radix positioning, matchMedia). Sixteen tests over what the e2e cannot cheaply pin: the format helpers
under fake timers in both languages, runtime en/zh key parity (guarding anyone who runs tests without
the build gate), the primitives' contracts (a pill never color-alone; a warned tile wears the warning
on its note, never its value; publish disabled until the ask is complete), and the drawer pattern's
point — the list surviving behind the open detail. `npm run test:web` now runs the whole pyramid as
one gate: unit layer → bundle + host build → Playwright over the example family.

## D42 — the properly-tooled front end: headless libraries, i18n, the design tool, and the UI's test loop (2026-09-19)

✅ done 2026-09-19 — the owner's direction: a long-term project needs real UI tooling and library, a
design tool, i18n, and the example project wired into testing with sub-agents and test-in-loop.
Designed first (`docs/2026-09-19-frontend-architecture.md`, D42, checked read-only against the
bilingual sibling — its i18n library and two hard-won practices, flat dotted keys and an en/zh parity
build gate, adopted; its styled-framework-plus-duplicated-theme stack deliberately not). Then
re-platformed with the design language unchanged: **Tailwind v4** with the validated tokens as its
theme, **Radix** primitives under the same pixels, **lucide** icons, **TanStack Query** with
invalidation after every mutation (the badge and the Quests view now share one deduplicated fetch),
**react-i18next** with `en` and **简体中文** — the zh catalog drafted by a sub-agent against a fixed
glossary (委托, not 任务 — the family's quest/task distinction preserved in Chinese) and reviewed line
by line. **Storybook** stands as the design tool over the shipped components and tokens; **Playwright**
boots the real host over `examples/` and drives the shipped bundle — members visible, a quest through
its whole life in the drawers, the verbatim refusal, the language switch — **5/5**, declared in
`daoris.gates.json` and the release workflow, so a release whose UI cannot do its job over the example
family does not ship. The loop earned its keep immediately: it caught the e2e host serving no page
(content root ≠ bundle location), an aria-label overriding a control's visible name, and Radix's
toast announcer double-render. UI chrome translates; data and the service's sentences stay verbatim.

## D41 — the platform's design language, built and reviewed (2026-09-19)

✅ done 2026-09-19 — the owner's direction sharpened: "design the UI/UX properly, because this is used
by a human." Designed before built (`docs/2026-09-19-platform-ux.md`), then shipped: a **console
shell** — sidebar with wordmark (Daoris · 道衍), icon navigation with the outstanding badge, global
state (tier, index size, refresh) stated once at its foot — a **page header** per view with its one
primary action, a right **drawer** as the single detail-and-form surface (knowledge entry, quest
detail with its actions, the compose form), **toasts** carrying every outcome and refusal verbatim,
designed **empty states**, static skeletons, and hold-at-reduced-opacity refetches. A quest card is
now something you read; the acting moved to the drawer, where there is room to act deliberately.

**The status palette was computed, not tasted.** The first candidate failed the visualization
validator exactly where taste would not have noticed — red↔green at deutan ΔE 3.9, and the bronze
accent below the chroma floor inside a status set. Four iterations later both themes pass all six
checks (light: worst deutan 13.3, normal 21.0; dark: 9.2 / 17.5), the red/green pair separated by
lightness as well as hue, the accent kept as interactive identity and never a status, and every pill
carrying its text label. Reviewed in Chrome over the real family, every view, zero console messages.
No new dependencies: a dozen hand-drawn stroke icons and one stylesheet.

Partial, deliberately: the flake is not explained, so `REH1` remains in the backlog. What closed is
its precondition — "capture the log before re-running" no longer depends on anyone remembering.
`tools/release-rehearsal.mjs` now writes every run's full transcript (plus exit code) to
`_fixtures/rehearsal-logs/<timestamp>.log`, outside the scratch tree a passing run deletes. Three
runs on 2026-09-18 — two of them straight after a canon edit and sync, the twice-observed trigger —
all passed 52/52, each leaving a transcript.

## DRV1 — design the driver (2026-09-19)

> The direction is D45; the mechanism is deliberately undesigned. Write `docs/<date>-driver-design.md`
> before any code, settling: how a quest triggers a session (spawn vs wake; queue semantics; what
> "Daoris should have started this" means); the session lifecycle attached to a quest (started,
> working, gates-green, done/declined, failed) and where it is stored; the agent adapter seam
> (claude-code first, codex second — one supported, others explicit, per D23's lesson); how the D37
> carve-outs surface in the loop (the person's checkpoints in the platform); and what part 2 needs from
> the service (a trigger/session surface) versus what stays in the desktop host. Read D45, D37, D32/D33
> and the Desktop brief first.

✅ done 2026-09-19 (see the DRV2 entry below for the build) — `docs/2026-09-19-driver-design.md`,
recorded as **D46**. All five questions
settled: **spawn fresh** (one non-interactive session per quest, one active session per repository,
clean tree only, oldest open first; wake held as an adapter capability); the **session lifecycle** is
nine observed states stored in the service beside the quests behind a shared judgement class, with
processes and transcripts staying in the desktop host and gates-green kept as outcome evidence rather
than a state; the **adapter seam** is D23 one layer up (claude-code supported, codex explicit, unknown
errors naming what exists, a harness named but never a model); the **D37 checkpoints** surface as the
platform's session-control surface (drivable per repository, hold, stop, start-now; `awaiting-person`
arrives with its analysis; the driver has no push/publish capability at all); and the **service/desktop
split** gives the service passive session records plus a machine-local repository root on the
registration, while the trigger action stays the driver's alone. The design's governing principle is
the owner's, set the same day: **driving is additive, never exclusive** — outside sessions stay
first-class, their connector flows unchanged, their quests trigger driver sessions elsewhere, and the
spawned session claiming its own quest is what makes driven and outside work indistinguishable at the
quest layer. Verification: a stub-adapter phase in the family rehearsal, so the mechanism is
gate-verified with no model in the gate. Bonus finding: the desktop runtime sibling is released and
consumable (v0.16.0), so DRV2 has no coordination blocker.

## DRV2 — build the local driver: `Daoris.Desktop`, re-scoped (2026-09-19)

> Hosts the local server, carries the platform UI, controls repositories and agent sessions per the
> settled design — `docs/2026-09-19-driver-design.md` (D46) is the contract.

✅ done 2026-09-19, in one driven day, as five landings. **The service's session surface**: a
`sessions` table beside the quests behind a `SessionLedger` mirroring `QuestExchange` (one judgement
for every door; the ledger never writes quest state), `GET /api/sessions` plus key-gated writes, and
the registration's machine-local root — sent by `connect` to loopback services only, answered only to
loopback callers, migrated in place for existing stores. **The driver**:
`src/Daoris.Desktop/Daoris.Desktop.Driver`, a pure planner (every sitting quest carries its reason),
an observation table (exit code × quest state — the two signals outside work also produces), clean
trees only, transcripts captured, timeouts killed, shutdown concluding in-flight sessions as
`stopped`; headless as `daoris-driver`; gate-verified by the family rehearsal's driver phase — 43/43,
no model in the gate: quest → session → commit → done, a dirty-tree hold, a reasoned decline, outside
work left entirely alone, records surviving restart. **The adapters**: the stub (a test double with
real mechanics) and `claude-code` — headless mode, the composed claiming instruction as the prompt,
edits auto-accepted, everything else under the repository's own checked-in permissions, no model ever
named. **The platform's session surface**: the record beside its quest, read-only in a browser, en +
简体中文. **The shell**: `daoris-desktop` on released Shenora.Windows 0.16.0 — brings up the HTTP host
(adopt or own; a dev host runs from its project so the bundle serves), carries the platform in its
WebView (the same bytes a browser gets), runs the loop in-process with `driver.json` re-read every
tick, and lands the person's controls through the `DAORIS.DRIVER` IPC module: drivable and hold per
repository, stop through a shared process registry with the end recorded as the person's, live
updates over `DRIVER_TICK`. Smoke-verified live twice — up, page served, closed whole, nothing
orphaned. Two traps recorded where the next person meets them: a dev host spawned from its `bin`
answers every API call and serves no page (the locator carries a working directory now), and a
MessageBox owned by the window disables it — trouble is a page, never a modal. The first REAL driven
run is deliberately left to the person as DRV4: the adapter is a deployment choice on a proven loop,
and witnessing it is the person's checkpoint, not a gate's.

## LYN1 — the Lyntai pin, and the one-sweep migration (2026-09-19)

> `Daoris.Service` deliberately pins the cognition sibling at released 2.1.0 (D22): upstream has a
> renaming major sitting unreleased (its `Llm*` call types become `Text*`, and `Providers.Default` is
> already retired from its 3.x package roster), so the coordinated move is **one** migration when that
> major ships, not two.

✅ done 2026-09-19 — 3.2.0 shipped that morning and the owner said go; the held single sweep paid off
exactly as designed. The move was larger than the rename LYN1 anticipated and still landed green on
the first build: the embedder SEAM is gone upstream (its D129/D153) — `IEmbedder.EmbedAsync` became
`IVectorProvider.CallAsync(VectorRequest) → VectorResponse`, where failure is a **verdict beside empty
vectors, never a throw**. One new place reads that verdict (`Core/Embedding.cs`): this service routes
over a single configured backend, so non-Ok is terminal and joins the existing "semantic tier did not
answer" path — the degradation test now holds that contract shape directly. Queries now embed under
the QUERY role, which the old seam could not even express. `HttpEmbedder` →
`HttpModelProvider`/`OllamaProvider` with `Produces = Vector` declared (leave it and you get a chat
backend posting /chat/completions); the hosts replicate the sibling's own Ollama-root judgement (its
D160, internal there) because these composition roots build by hand — and our default embed URL IS an
Ollama root, so skipping that check would have silently broken the default deployment. Package:
`Providers.Default` 2.1.0 → `Lyntai.Core` + `Lyntai.Providers.Basic` 3.2.0. The `Llm*`→`Text*` rename
touched nothing here — this service never made a text call. 114/114 service, 43/43 family rehearsal
over the real 3.2 packages, verify clean. The Lyntai adoption half of the old note is unchanged:
de-adopted, not quest-addressable, and nobody is asked to adopt until Daoris is finished.

## DRV4 — the first real driven run (2026-09-19)

> The `claude-code` adapter is a deployment choice on a gate-proven loop (design §8) and is verified
> by use. Two claims to confirm in that run: the driven session's connector tools load under the
> harness's non-interactive mode, and the IPC payload casing of `DRIVER_TICK`/`STATE`.

✅ done 2026-09-19 — a real session drove a real quest to done in 71 seconds, and both flagged claims
are confirmed. The setup was the loops' own shape: a scratch-born project (`init` → domain → `sync` →
`check` → `connect`, git-initialized, its own `.mcp.json` naming the knowledge host and its own
`.claude/settings.local.json` trusting it and allowlisting git), a scratch HTTP host, quest `#c2ce87`
("Leave a first note") published through the door, and `daoris-driver --until-idle` with
`adapter: claude-code`. The driver spawned a real headless session in the newborn's tree; the session
**claimed its own quest over its own connector** — the MCP tools loaded under
`-p --permission-mode acceptEdits` with only the repository's own trust settings, which was claim one —
wrote `NOTES.md`, committed it (`add NOTES.md — the repository's first note`), and closed the quest
`done` with a note naming the commit. The driver observed all of it: session record `completed`,
"the quest reached done.", evidence carrying the exact commit, tree clean after. Claim two fell to
reading rather than running: the desktop runtime's one frozen IPC serializer is
`JsonNamingPolicy.CamelCase` (its `IpcJson`), so the page's `events`/`configPath` reads are correct by
construction. The scratch world remains under `_fixtures/real-drive` (gitignored) with the session
transcript, for the owner's own look. One nested-run note for whoever repeats this from inside an
agent session: strip the `CLAUDE*` environment before starting the driver, so the spawned session
starts as cleanly as a real deployment's would.

## DRV3 — design the remote server (2026-09-20)

> The remote server, for teams. Multi-user sharing of knowledge and quests across machines, **fed via
> the local desktop app** (local-first; the remote is fed, not authored — D21's "shared may be a sync"
> finally lands). Folds in the old SVC2 hardening: per-person expiring keys and OIDC per the service
> design §5, and whatever relay the local↔remote sync needs. Write `docs/<date>-remote-design.md`
> before any code, settling at least: what syncs and what never leaves a machine; whether the remote is
> a deployment of the existing HTTP host or a store the local hosts sync against; how quests flow
> across machines without breaking "the quest state machine is the only lock" when two machines'
> drivers watch one quest; person-auth (OIDC) vs machine keys (§5a); and what the driver/platform need
> to say about a remote's sessions (records sync, processes never — D46 §4).

✅ done 2026-09-20 (design; the build continues as DRV5) — `docs/2026-09-20-remote-design.md`, recorded
as **D47**. All five questions settled. **What syncs**: two manifest declarations (join; share
knowledge), silence meaning local; the strip is structural — the feed's DTOs carry no field for roots,
transcripts, private content, or unjoined repositories, and knowledge feeds as content, never vectors
(each deployment embeds with its own provider; nothing is lost — vectors are not persisted even
locally). **What the remote is**: a deployment of the existing HTTP host in shared mode — git-as-store
was priced as §8.1 asked and declined, because D45 turned quests into an execution queue and a queue
two machines race needs an arbiter that refuses the second `take` before work starts, which git only
surfaces at push time, after the duplicate session already ran. **The lock**: one home per quest,
decided at publish by whether the receiver is joined; transitions write through synchronously or fail
plainly while records sync eventually; and the build hardens the convention into code — a quest
transition table with an atomic Open→Taken in the shared judgement class, because today
`SetStatusAsync` moves any quest anywhere on an existence check alone, which two watching drivers turn
into a duplicated-work generator. The race resolves by DRV2's existing stand-down, no new states.
**Identity**: service design §5 as specified — per-person per-machine expiring keys, OIDC for people
with the dev scheme inert outside Development, every route gated in shared mode, and a host binding
beyond loopback without that model refuses to start. **Sessions**: records feed keyed by origin + id,
transcripts stay home (today's unguarded `transcript` field on `GET /api/sessions` is a named gap the
build closes), controls act only where the driver is attached, and even the cross-machine stop request
is held as an open question. Verification is a remote phase in the family rehearsal: two simulated
machines, one raced quest, the strip proven by scanning the remote store, keys refused and never
leaked, no model in the gate.

## DRV5 — build the remote server: shared mode, the sync loop, the hardened lock (2026-09-20)

> The remote server, for teams — D45's part 3. The contract is `docs/2026-09-20-remote-design.md`
> (D47): shared mode with per-person keys, the desktop sync loop, and the quest lock hardened into
> code, all gate-proven by a two-machine phase in the family rehearsal with no model.

✅ done 2026-09-20, in six landings. **1 — the quest lock becomes code**: `MoveAsync` inlines the
transition table into the UPDATE's WHERE (Taken only from Open — the atomic take; closed quests
immovable), in the shared `QuestExchange` so local mode is hardened too; the second take loses in the
store, proven by a two-connections-over-one-file test, and the exchange names the state that refused
(already-taken → stand down; closed → a new ask is a new title), 409 at the HTTP door. **2 — the
transcript guard**: `GET /api/sessions` gained the root's loopback guard; evidence still travels,
the machine path does not. **3 — shared mode**: `DAORIS_MODE=shared` gates every route with minted
per-person per-machine keys (SHA-256 hash + non-secret audit prefix, shown once, expiring, redacted
on every path), administered on the binary (`keys mint|list|revoke`); no page served, no machine path
answered; a non-loopback bind in local mode refuses to start (the fail-safe inversion). **4 — the
manifest's two declarations** (join; share knowledge), silence meaning local, carried by `connect` as
explicit booleans and read by both service readers, narrowed identically (knowledge needs join).
**5 — the sync loop**: `RemoteSync` on the desktop feeds stripped registrations, session records
keyed by origin + id, and sharing repositories' knowledge content (never vectors) up; mirrors
remote-homed quests and foreign registrations down; quest verbs on remote-homed quests write through
via a Core relay seam (`IRemoteQuestClient`/`HttpRemoteQuests`) both local doors compose, an
unreachable remote refusing plainly rather than queueing. **6 — the rehearsal's remote phase**: two
simulated machines and a shared host — a quest published on A drove to done on B, the closure crossed
back, a raced take left the losing driver observing the lock, knowledge crossed only where declared,
and the remote store was scanned byte-level for no root, no transcript, and nothing kept home; 74/74.

Recorded as **D47** with two amendments made during the build on the owner's redesign grant (nothing
deployed): D36's interim single-key write gate was **retired** rather than carried — two trust shapes
only, local-loopback and shared-keys; and the sync mirrors the remote's registry down as **foreign
rows only**, because the machine holding a checkout is the authority on its own registration and root.
Two traps landed in `docs/FIX-LOG.md`: a stale gitignored `dist/` silently shadowed the CLI sources
in every `bin`-driven gate (green `npm test`, red rehearsal), and an omitted driver mode falls through
to watch-forever (the rehearsal's drive helpers default `--once` with a kill-timeout backstop now).
Final: CLI 128, service 158, driver 49, family rehearsal 74/74, verify clean. All three parts of D45
are built.

## SES1 — the console (2026-09-20)

> **SES1 — the console.** The capture pump tees to a bounded per-session ring buffer; the shell's
> IPC gains `TAIL_SESSION` + `SESSION_OUTPUT`; the session drawer renders the stream verbatim,
> desktop-only (transcript-class material never leaves the machine, D47 §4). Interactive design §2.

✅ done 2026-09-20 — a person can watch a session say things, instead of reading a file afterwards.
**The pump tees**: one loop, two destinations, and the file is written first — the durable copy must
never lose a line to an in-memory reader's problem. `SessionOutput` holds 500 lines per session and
16 sessions, lives beside `SessionProcesses` in the driver (transcript-class material has no HTTP
surface, structurally, not by policy), and is **optional** — the headless host constructs none, so a
buffer exists only where something reads it. **Sequence numbers make two sources one stream**: the
page asks `TAIL_SESSION` once on open, lives on batched `SESSION_OUTPUT` events after that, drops
what it has seen, and closes a gap by asking rather than by rendering one. **Everything bounded says
so** — dropped lines are counted and shown, because a console that silently skipped the middle of a
log is a worse lie than one that showed nothing. **Eviction never takes a live session's buffer**,
and an ended one is kept because how a session finished is what someone most wants to read.
**Batched on a ~120ms window** in a shell-side `ConsoleRelay`: per-line events would turn a chatty
session into a stalled window. **The console degrades to absent** — found by a test, where a mocked
bridge answering the wrong shape crashed the whole drawer, which is exactly what a console must never
do to a record. The gate reaches the durable half only: the rehearsal's stub now says something, and
the transcript is read back to prove the pump still writes it — the in-memory half has no headless
door by design and is held by unit and platform tests. Tests grew 74→89 driver, 36→40 web vitest,
123→124 family rehearsal.

## WSP4 — knowledge sync semantics (2026-09-20)

> **WSP4 — knowledge sync semantics.** Feeds carry git provenance stamped by the driver (commit,
> committedAt, branch; `WorkingTree` reads HEAD); default-branch-only knowledge (records/quests travel
> from any checkout); monotonic replacement by commit time, refused plainly and reported as
> information; provenance served on `/api/repositories` and shown on Projects. Rehearsal: a stale feed
> refused, a branch feed refused, a newer feed replacing. Design §6.

✅ done 2026-09-20 — two checkouts of one repository are two points in its history, and the deployment
now decides which one speaks. **The feed carries its position**: `WorkingTree.ProvenanceAsync` asks git
for HEAD's commit and committer date in one call (so the two can never come from different commits)
plus the branch, naming a detached HEAD rather than sending a blank; `DefaultBranchAsync` reads the
canonical line from `origin/HEAD`, then `main`, then `master`. **The judgement is Core's**, at the door
the disclosure judgement already lives at, in three parts: a feed naming no commit is refused (a
replacement that cannot be compared is not safe), a feed from a line that is not the declared canonical
one is refused naming both, and a feed older than what is held is refused naming the commit it is
behind — same-commit re-feeds stay idempotent, equal times are unorderable and take. Wholesale
replacement then keeps *delete* correct for free, and the rehearsal proves it: the driver's own entry
is gone after a newer view arrives without it. **A refusal can be INFORMATION** — the first in this
system — carried as a flag on the wire rather than a sentence to match, so the sync reports "the
deployment kept a newer view" as a `held` line and a wall still fails loudly. **Provenance is served**
on `/api/repositories` and rendered on Projects (commit, relative time, and a tip naming the branch and
the machine that fed it), because the index is a claim about a commit and staleness someone can see
beats freshness they must assume. **A checkout with no git history feeds no knowledge**, and the driver
says so itself rather than sending a doomed feed — only the side with the tree knows why git could not
answer. Two storage choices are structural rather than careful: provenance is its own table (nothing
but the feed writes it, so no unrelated write can erase it) and the declared default branch is a
registration field that null PRESERVES (like the workspace, for the same reason). The rehearsal's
circle members became real git checkouts on a deterministic `main`, which is what the rules presume.
Tests grew 225→235 service, 65→74 driver, 35→36 web vitest, 112→123 family rehearsal.

## WSP3 — remotes become a map (2026-09-20)

> **WSP3 — remotes become a map.** `~/.daoris/remotes.json` (workspace → url/key; env pair kept
> for one workspace via `DAORIS_REMOTE_WORKSPACE`, both twins' test tables moving together); the sync
> loop runs per workspace; the shared host gains its `DAORIS_WORKSPACE` identity and refuses feeds/
> registrations naming another, plainly; the quest relay resolves its remote by the quest's workspace;
> CLI parity (D50): `daoris remote list|add|remove` over the same file, offline, key prompted or from
> env and echoed redacted, plus `status --machine` reporting the wiring; the desktop's settings
> surface edits the same file. Design §5/§2b.

✅ done 2026-09-20 — "the machine's remote" became "the machine's remotes", one per circle. **The map**
is `~/.daoris/remotes.json`, read by three deliberate twins — `RemoteConfig` (service), `RemoteTarget`
(driver), `remotemap.ts` (CLI) — because the three artefacts share no code and the FILE is the
contract; each carries the same table, and the rules are unchanged except in scope: the environment
pair replaces the file **for the whole machine** (a merge would let a real map leak into a process
that believed it had named its only remote, which the gate's hermetic guard rests on), a half-set pair
is no remote anywhere, an entry missing half its pair is one unwired circle rather than a machine with
none, and the pre-workspace flat shape is read as nothing — rebuilt, not migrated. **The sync runs per
workspace** (`RemoteSyncSet`): one circle's wall names itself and leaves the others' pass alone, the
joined rows are filtered to the circle while the *names* guard stays machine-wide (it is what stops a
foreign row overwriting a local registration that shares a name in another circle, root and all), and
mirrored-down rows are filed in the syncing workspace — the receiver's own wiring deciding, never the
feed naming itself. **A shared host is a workspace's host**: `DAORIS_WORKSPACE` is its identity, every
row it takes lands in that circle, a registration declaring another is refused with a sentence naming
both sides, and a LOCAL host given the variable refuses to start rather than ignoring it. **The relay
resolves by the quest's workspace**; a quest this machine has never mirrored names no circle, so it is
tried only when there is exactly one it could mean and otherwise refused plainly — guessing would post
a `take` at a deployment that never held it. **Two editors over one file** (D50): `daoris remote
list|add|remove` — file-local, offline, the key from a flag, the environment, or typed in, and never
printed back beyond its audit prefix — plus `status --machine`; and the desktop's new Machine view
over a `DAORIS.REMOTES` module, shell-only because the service deliberately has no route onto machine
wiring with a credential in it. Both say which source is live, since with the env pair set the file's
rows are not the wiring. **The gate found a real defect in existing code**: `keys mint` composed the
whole service and so bootstrapped a registry from the server's own disk, machine paths included
(FIX-LOG; key administration now opens the key store alone). Tests grew 147→164 CLI, 201→225 service,
53→65 driver, 30→35 web vitest, 94→112 family rehearsal.

## WSP2 — the registry becomes managed (2026-09-20)

> **WSP2 — the registry becomes managed.** Registry-as-authority (explicit list: name, workspace,
> declaration, machine-local path); the folder scan becomes `import` (first run imports the old root,
> once, and says so); refresh reads registered paths and names absences; the desktop's add/update/
> remove over the loopback host (registration lifecycle only — never deletes files, doctrine
> unwritable, manifest edits land as uncommitted diffs); CLI parity (D50): a retire verb beside
> `connect`, and `import` callable from the terminal. Design §3/§7/§2b.

✅ done 2026-09-20 — the scan stopped being an authority and became a verb. **The registry** is an
explicit list: `Registry` holds what was registered and nothing else, `RegistryImport` is the old
manifest-reading scan demoted to a proposal, and adoption became a stored column because a row for a
folder with no manifest still belongs on the map. **The index reads registered paths**, resolved per
read, so a repository added or retired a moment ago is in or out of the very next refresh — and a
folder nobody registered contributes nothing, which is the whole of §3 in one assertion. **Absences
are named**: a registered checkout that is no longer where the registry says it is is reported by
every refresh door, while a row that never named a path — a teammate's mirrored registration — is not
an absence, because it has no checkout here by construction. **The bootstrap** imports the configured
root exactly once per store, marked in a `registry_meta` row and announced on stderr; without it a
machine that had been running on `DAORIS_KNOWLEDGE_ROOT` would come up to an empty family, and an
empty family is indistinguishable from a broken one. It runs only where the deployment reads local
checkouts at all — the same sentence that keeps a shared host off its own disk, made one variable so
the two cannot drift. **Two CLI verbs**: `daoris retire [name]` (the registration only; the service's
own "nothing was deleted" sentence reaches the person verbatim, and retiring what is already retired
exits clean) and `daoris import [folder]` (absolute paths, because the service may sit in a different
directory; safe to re-run because an import states no workspace). **Three doors**: `DELETE
/api/registry/{name}`, `POST /api/registry/{name}/workspace` (re-wiring only, kept apart from the
declaration on purpose), and `POST /api/registry/import`, refused on a shared deployment for the same
reason refresh is. **The desktop manages repositories**: a `DAORIS.REGISTRY` module supplies the one
thing a page cannot — a folder pick and what is true about it — while registering, re-wiring and
retiring go through the ordinary loopback door rather than a second IPC path onto the same judgement;
the declaration form merges into `daoris.json` property by property so nothing it does not know about
is silently dropped, and leaves the diff uncommitted. **The offline guarantee became a class**: the
network moved into one `service.ts`, so the test still reads as one sentence, and the transitive half
now walks from every doctrine command rather than only `check`. The Playwright suite's D44 newcomer
gained its `connect` — it had been joining by being scanned, which is exactly the silence this item
removed, and the test was reading it as a pass. Tests grew 139→147 CLI, 193→201 service, 27→30 web
vitest, 87→94 family rehearsal.

## WSP1 — the workspace exists (2026-09-20)

> **WSP1 — the workspace exists.** Membership is wiring, never tracked (D48 as amended — the git
> shape): the registry row carries the workspace, `connect --workspace <name>` sets it (preserved on
> upsert, defaulting to the existing row then `default`), **the manifest is untouched**; every
> cross-repo entity (registration, entry, quest, session) carries its workspace; search/convergence/
> registry/quest scoping with the same-workspace clause in `QuestExchange` (refusal names both sides);
> MCP tools gain `workspace` with the ambient default resolved from the registry by path; the family
> rehearsal grows the two-workspace phase (a search and a quest refused across the boundary, with the
> sentence). Design §2/§2a/§4.

✅ done 2026-09-20 — the first landing of the D48 arc, and the foundation the rest stands on. **The
name** is one place: `Workspaces` holds `Default`, `Normalize` (silence is `default`) and `Same`
(trimmed, case-insensitive) — a boundary nobody can see is worse than none, because it produces a
refusal with no explanation in it. **The wiring** is a registry column whose preservation is decided
in SQL: `COALESCE($workspace, workspace, 'default')` in the upsert, so a statement wins, an existing
row survives silence, and `default` closes it — one atomic statement rather than a read-modify-write
two doors would race on. `UpsertAsync` returns the row as it now stands and `RegisterAsync` serves
*that*, because serving the incoming record would re-point every repository to `default` in memory on
the next ordinary sync tick while the store kept saying otherwise. **Every cross-repo entity carries
it**: entries (stamped at ingest from the wiring, never from a file — schema bumped to 2, which
rebuilds rather than migrates), quests (decided by the exchange, so both sides share it by
construction), sessions (derived from the quest, never passed beside it, so the two can never
disagree). Both feed doors stamp from the **receiving** deployment's wiring: a feed that could name
its own workspace could write itself into someone else's. **Scoping** reaches search (a SQL clause,
browse included), convergence (two circles stating the same lesson have not converged), the registry,
the repository summary, and the quest and session lists. **The quest clause** refuses across the
boundary naming both sides, both workspaces and what to do about it, offers only the asker's own
circle as addressable, and wears 409 at the HTTP door — a state conflict, not a malformed ask.
**The ambient scope** (`AmbientWorkspace`) resolves a session's own circle from the registry by
working directory — segment-wise, innermost-first, separator- and case-insensitive — so an agent never
has to know wiring it has no business knowing; no match answers null and every MCP tool *says* it
spanned everything, the D24 shape. **The CLI** gained `connect --workspace <name>`, omitted entirely
when unstated (an absent field preserves; `""` would reset), refused with a name when the flag is
bare, and reporting back the workspace that actually took. **The platform** shows each project's
circle rather than presenting two as one family. Along the way the rehearsal caught a real defect:
`refresh` re-read the repositories but never the folder, so a repository born after startup was
invisible to the index while being fully registered and addressable — `docs/FIX-LOG.md` has it.
Tests grew 135→139 CLI, 170→193 service, 75→87 family rehearsal; release rehearsal 53/53, driver
53/53, web 27+7 unchanged. Deferred by scope, not by omission: the platform's workspace *switcher*
(WSP2/WSP3's surfaces), and per-workspace remotes (WSP3) — until then a shared host still serves one
circle because it holds one store.

## REV1 — the post-redesign review sweep: fix, dedup, and re-document the D45–D47 arc (2026-09-20)

> Five parallel audits over the arc's ~94 files; the consolidated findings live in
> `docs/2026-09-20-post-redesign-review.md`, grouped by disposition, each checked off as its commit
> lands. Headline: the remote sync could wipe a teammate's shared knowledge (mirror-down rows feed back
> up empty), shared mode scans the server's disk through `EnsureIndexedAsync`, `postpack --clean` was
> never implemented (the FIX-LOG's stale-`dist/` trap is still manufacturable), and `CLAUDE.md` names a
> `daoris request` command that does not exist.

✅ done 2026-09-20, in nine commits, every finding fixed or deferred with its reason in the review
record. **Bugs**: the mirror-down feed-back (feed-up now requires a root — the checkout is the
authority) and the shared-scan back door (shared mode composes `EmptyKnowledgeSource`; the rehearsal
plants a decoy and asserts it stays unserved — watched red under sabotage) both landed in
`docs/FIX-LOG.md`; the ledger's numeric-state hole closed onto `Session.TryParse`; session-door
conflicts wear 409 like the quest door; `/api/feed/quests` validates every bound field; a corrupt
manifest or lock fails as exit 2 naming the file; the desktop splash completes `HostReady` on every
path; `postpack --clean` exists, removes `dist/`, and the release rehearsal asserts nothing staged
outlives a pack. **Dedup/refactor**: Http `Program.cs` 745→486 (contracts, keys console, and a
two-host `HostComposition` out; route groups declined as indirection); `ParseKinds` and the session
feed judgement moved to Core (D36); `RemoteSync` split three ways with a tested ordering contract;
`DriverHttp` and `DriverWatch` replaced three transports and two loops; the rehearsal kit
(`tools/rehearsal-kit.mjs`, `tools/fsx.mjs`) replaced five `copyTree`s and two diverged harnesses; the
web platform gained `QUEST_TONE`, keys-factory invalidations, a cached Reader and `useDebounced`, and
its new surfaces stopped failing silently. **Docs**: `daoris request` excised, "81 tests"/"Eight
commands"/"Three packs"/"Four artefacts" corrected, the Desktop brief's stale "Remaining" list
replaced, `domain` + `remote` documented in the README with `status` reporting the declaration, the
git-as-store prescription closed per D47, D36/D31 gained their supersession notes, and the remote
design's second amendment is marked in place. Tests grew 128→135 CLI, 158→170 service, 49→53 driver,
21→27 web vitest, 52→53 release rehearsal, 74→75 family rehearsal — the new checks are the guarantees
the arc claimed in prose.