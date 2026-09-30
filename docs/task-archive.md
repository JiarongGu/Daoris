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

## REV2 — the arc reviewed, and the shell's head made testable (2026-09-20)

> Asked for by the owner after the arc closed: "we really need to do review and e2e tests."

✅ done 2026-09-20 — a review of the eight landings, and the two things it found. Neither was visible
from any gate, which is the point of having reviewed rather than re-run.

**Finding one: the declared gates and the release workflow were two lists that disagreed.** The
workflow ran exactly one `dotnet test` — the devkit's. `dotnet test src/Daoris.Service` (248 tests)
was DECLARED in `daoris.gates.json` and never run; the driver's 130 were in neither, including the
planner, the adapter seam, the whole SES3 toolchain and the guard that stops a credential profile name
crossing a machine boundary. `CLAUDE.md` claimed the workflow "runs all four gates", which it did not.
What hid it is worth keeping: **both rehearsals drive the service and the driver end to end**, so the
workflow looked thorough and every release rehearsal passed. An end-to-end pass is not a substitute
for the judgement underneath it. Fixed in the workflow, the declared set and the prose, with a check
that every declared gate appears in the workflow — watched failing against the pre-fix file, where it
names the service gate exactly.

**Finding two: 1,128 lines of shell with no tests, and a contract asserted on neither side.** The
three IPC modules are the whole surface between the platform page and this machine. The page's own
suite mocks the bridge; this half had no test project. **A mock agreeing with a mock proves the two
mocks agree** — and underneath that, every deliberate refusal these modules make was reaching people
as a generic failure, because the host maps an unhandled exception to `UNKNOWN_ERROR` carrying only
the exception type. Five written sentences, plus every `DriverException`, dropped on the floor for as
long as they had existed (`docs/FIX-LOG.md`).

**What was built.** `Daoris.Desktop.Modules` — plain `net10.0`, holding the loop, the host supervisor
and the three modules; `Daoris.Desktop.App` keeps only what needs a window. None of it was ever
WinForms: it was Windows-only by accident of where it was written, and the accident was the reason it
had no tests. The refusals became declared codes thrown as `ShenoraException`, with `DriverException`
mapped once at the module boundary so the driver's own sentences travel verbatim — the same class as
the service's, which this platform has always rendered word for word. The page gained one `sentence()`
helper and every `onError` goes through it.

**Three of my own mistakes, each caught by the thing being built.** A redaction assertion read raw
JSON, where the ellipsis is `…` — it would have passed on a leak of a differently-shaped key. A
second test class turned two passing tests red by trampling process-global environment variables:
these modules resolve every path from the environment, so the suite is serialized, and a suite whose
result depends on scheduling is worse than a missing one. And `HARNESS_UNMANAGED` was unreachable —
`Toolchain()` throws for an unknown adapter before it can return null — which is what led to mapping
`DriverException` once instead of duplicating its judgement.

Gates: a new `modules` gate (39 tests), declared and in the workflow. CLI 193, service 248, driver
130, modules 39, devkit 57, web 55 + 9 e2e, family rehearsal 154/154.

## CANON6 — doctrine must not hard-require Daoris (2026-09-20)

> **CANON6 — doctrine must not hard-require Daoris (coexistence, D48).** Audit the 8 core rules
> for instructions only Daoris can perform (the known case: `repository-owns-its-work`'s "publish a
> quest"); every named mechanism gains the tool-absent path in the same breath ("…and file the request
> with that repository's owner where the quest system does not exist"); the principle lands in
> `.claude/knowledge/canon-authoring.md`. Under the byte budget's discipline — a carve-out that does
> not fit is a D28 split, not a raised limit — and a canon change re-syncs `examples/` in the same
> commit, as ever. Workspace design §2a.

✅ done 2026-09-20 — the audit found **one** case in the whole canon, and why it is only one is the
part worth keeping. **What `sync` writes is committed.** A generated index, a lock file and every
vendored rule are all still *there* for a contributor who never installed anything, so naming them
costs a non-user nothing. A **service** is the exception: publishing a quest needs a process running,
and it is the only thing the canon instructs that does. The test is therefore **file or service, not
family vocabulary** — and that correction was earned, because the first pass "fixed"
`skills-workflow` for naming the generated index, which needed nothing at all. The mis-fix is written
into `canon-authoring.md` as the example, so the next reader inherits the sharpened test rather than
the instinct that produced it.

`repository-owns-its-work` and its companion `reaching-in` now name the alternative in the same
breath — *a quest where a request system exists, a message to that repository's owner where none
does* — and the rule's vocabulary moved from "quest" to "request" throughout, because the principle is
about the request travelling rather than about the mechanism carrying it.

**The carve-out paid for itself, which was not the expected outcome.** The rule had been restating in
always-loaded text what its own on-demand document already said better: the revert-mechanics sentence
duplicated two bullets of `reaching-in.md`, and a "moving target" paragraph re-argued what that
document's own analysis says at length. Splitting those out freed **126 bytes** against a carve-out
costing far less — the core went from 23,988/24,000 (12 bytes of headroom) to **23,862** (138). The
D28 discipline did not merely permit the change; it found the fat.

**A gate holds it**, beside the machine-path scanner it is modelled on: no canon file may instruct a
quest without also naming what to do where no quest system exists. Watched failing against the
pre-CANON6 wording, with the message that names the fix. It is deliberately narrow — one
service-shaped mechanism, three accepted phrasings — because the broad version is exactly the mistake
the audit made by hand. One trap for whoever changes the canon next: `npm run rehearse:family` checks
`git status` for the examples, so it fails until the re-sync is **in the commit** — which reads as a
broken gate the first time and is the gate working.

A premise worth surfacing rather than silently resolving: **D28 moved the default budget to 30000
precisely because 24000 "fired on the canon rather than on a repository's own material", and Daoris's
own manifest still carries a pre-D28 24000.** By D28's own reasoning that number is wrong for the one
repository whose always-loaded material *is* the doctrine. The owner's direction says to split rather
than raise, so this landing split — and the split was genuinely right here. Whether Daoris's own
manifest should move to the D28 default is the owner's call, not this task's. Tests grew 192 → 193 CLI.

## SES3 — the toolchain (2026-09-20)

> **SES3 — the toolchain.** Adapter `Probe` (locate + version + per-profile login state, run at
> startup and on demand); the platform's roster (harness, version, present/absent, profiles);
> install/update and login on the person's explicit action via each harness's own mechanism, streaming
> through the console; **credential profiles** — named, isolated harness config homes selected at
> spawn via the environment seam, machine default per harness, optional default per workspace,
> per-session picker; Daoris stores directories and names, never secrets; the record carries harness
> version + profile name at spawn; spawn-on-missing and spawn-on-logged-out refuse naming the action;
> CLI parity (D50): `daoris harness list|install|update|login|profile ...` and `daoris driver ...`
> (drivable/hold/cap over `driver.json`) — a headless machine sets all of this from the terminal.
> Interactive design §4; workspace design §2b.

✅ done 2026-09-20 — Daoris manages the harnesses, and never a credential. **Every mechanism was
verified against the real binaries before a line was written**, because each is a claim about somebody
else's program: `claude auth status` answers JSON with a `loggedIn` boolean, `codex login status`
answers a sentence, `CLAUDE_CONFIG_DIR` and `CODEX_HOME` genuinely isolate an account, and `codex`
refuses to start when its home names a path that does not exist (so Daoris creates a profile home as
part of selecting it). Both binaries **exit 0 either way**, so the output is the answer and the exit
code is never consulted — and `claude auth status` volunteers an email, an org and a subscription
tier, of which Daoris reads one boolean and keeps nothing else.

**The additive rule is the load-bearing one: silence means the harness's own configuration home.**
With no profile named anywhere the environment seam is not set at all and a spawn is what it always
was — pointing someone who never asked for profiles at a fresh directory would log them out of their
own tool, which is the loudest available way to break "Daoris works alone" (D48 §2a). An adapter that
declares no toolchain is checked for nothing, for the same reason.

**Login state has three values and unknown is permissive.** Only a definite *logged out* refuses; an
answer this build cannot read is no evidence, the same judgement WSP4 made about an undeclared
canonical line. The first `codex` pattern was unanchored and `"Not logged in"` contains `"logged
in"` — it reported every logged-out profile as logged in, and a test found it. **A cached refusal is
re-asked before it is given**, so doing what the sentence said releases the queue on the next tick
with nothing restarted.

**One narrowing of the design, deliberately: the profile NAME stays on the machine that ran the
session; the version travels.** A name a person quite possibly chose after themselves is machine
wiring in D48 §2's sense, so it is guarded exactly as the transcript is — stripped for a non-loopback
caller, absent from the feed's shape, and written as a literal NULL by the store's mirror. Three
guards for one rule. A harness *version* is a fact about a tool, and it crosses.

**A profile is a directory, and the directory is the contract** — no register to disagree with the
disk, the same argument that made the registry the authority over a scan. `profile remove` clears the
wiring and says out loud that it deleted nothing: the directory holds a credential the harness put
there. The CLI and the driver share **a file and a layout, not code** (`~/.daoris/harnesses.json` plus
`harnesses/<harness>/<profile>/`), the twin arrangement WSP3 established, with three rules asserted in
both test tables. Two asymmetries its successors inherit: the CLI's *managed* set is not the driver's
*adapter* set (`codex` is manageable while no adapter spawns it), and the CLI's `driver` verbs edit a
file the C# side owns, so **every edit preserves the fields it has no verb for** — an editor that
rewrote `driver.json` from its own idea of the shape would delete the command the stub adapter runs.

**Two surfaces, one truth**: `daoris harness …` and `daoris driver …` for a machine with no screen,
the desktop's roster and per-conversation picker over the same files. The CLI *inherits the terminal*
for install/update/login — a login flow asks questions and waits for a code, and capturing the stream
to pretty-print it would turn a working login into a hung one — while the desktop relays the same
process through SES1's console under `<harness>:<action>`, never a session id, because it is not a
session and must not look like one.

Three defects the tests found: the unanchored `codex` pattern above; `daoris driver list` returned
early when nothing was drivable, hiding exactly the inert hold a person would most likely believe was
the problem; and the roster crashed a mocked bridge answering another shape — the same lesson SES1
recorded, restated on a new surface. The rehearsal gates all of it with no account, credential or
model: the stub became a fake *binary* as well as a fake session, so a spawn under profile `alpha` is
asserted to carry alpha's configuration home **from the session's own output** rather than from the
record's word for it.

**The outer loops were the part that had to be asked for.** A first pass left the Playwright suite at
7/7 — the roster and the picker are shell-only, so a browser cannot reach them, and it is easy to read
"cannot reach" as "nothing to test". Two things were reachable and both are now held: the record's new
fields rendered over the real bundle and the real host (the whole chain — request contract, ledger, two
store columns, response shape, formatter, DOM — where a nullable field quietly stops arriving and no
mock would notice), and the *negative* guarantee that **a browser learns nothing about this machine's
harnesses**, which is precisely the surface where that must hold. The cross-machine half went into the
family rehearsal: machine b drives under a named account, its own record says so, the fed record
carries the tool version and never the name, and the remote's store is scanned byte-level for it —
"three guards drop it" is a claim about code, and this is a claim about the artefact. One of the two
new e2e assertions was written against the page heading ("This machine") instead of the sidebar label
("Machine") and could never have failed; watching both sabotages is what found it. Tests grew 192 CLI
(+28), 248 service (+3), 130 driver (+32), 50 web vitest
(+6), 154/154 family rehearsal (+19); release rehearsal 53/53 unchanged, `test:web` 7→9.

## SES2 — chat sessions (2026-09-20)

> **SES2 — chat sessions.** `Session.Kind: driven | chat`, quest optional; the adapter seam grows
> `interactive` (stub first, scripted exchange in the gate; `claude-code` supported, `codex`
> explicit); one-session-per-repository holds for chats; `SESSION_INPUT` over IPC; chats may
> take/publish quests through their own connector; the rehearsal drives a chat to `completed` and the
> repository-busy refusal. Interactive design §3.

✅ done 2026-09-20 — a conversation is a session, not a second kind of thing. **The record gained a
`Kind` and lost its required quest**: `quest` became nullable by rebuilding the table and COPYING the
rows, because a session record is the trace of work that happened and nothing can re-derive it — the
entry store's discard-and-rebuild rule does not transfer. **The ledger judges chats in the same
place** with the same lock: one active session per repository, in both directions, and the refusal
names *what* holds it ("a chat" or the quest) because a person stops a conversation differently from
the way they wait out a driven run. A chat also has the one failure mode driven work cannot — an
unregistered repository — since it names its tree directly rather than inheriting it from a quest.
**The seam grew `interactive`**, default false: an adapter that has not been wired for turn-taking
refuses in the harness's own terms rather than spawning something that will never answer. A chat's
process has stdin; a driven one structurally does not, and the UI follows that rather than restating
it. **`ChatRunner`** spawns, relays the person's lines, keeps the transcript and console exactly as
the driven path does, and concludes from what it observes — Daoris pipes text and makes no model
calls; the harness carries the model and the conversation. **Two endings, two meanings**: end of
input lets the harness wind up (`completed`), `stop` is the person's interrupt (`stopped`). **Two
doors**: the desktop's (`START_CHAT`, `SESSION_INPUT`, `END_CHAT`, and a `SESSION_ENDED` event) with
a drawer over SES1's console, and `daoris-driver chat --repository <name>` for a machine with no
screen — the same runner, a different reporting half, and what lets the gate drive a whole
conversation with no model in it. The rehearsal holds one from a terminal: it answers what it hears,
**publishes** the work that came up rather than editing across, ends on end-of-input as a record with
no quest and a transcript of its own, and the tree stays the unit of exclusion in both directions.
A flaw the tests caught: the chat button matched any live session, so it would have opened an input
box in front of a driven session that has no channel to listen on. Tests grew 235→245 service,
89→98 driver, 40→44 web vitest, 124→135 family rehearsal.

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
> `docs/archive/2026-09-20-post-redesign-review.md`, grouped by disposition, each checked off as its commit
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

## SURF1 — design the working surface (2026-09-21)

> A design session producing a contract and a numbered decision, the way D41 →
> `2026-09-19-platform-ux.md` and D48/D49 → their two contracts did. **The research is already done and
> written down: read `docs/2026-09-20-working-surface-research.md` first** — it records what the field
> has settled on, what Daoris already has that the field does not, and the questions the design must
> answer. Three things from it govern the session: the platform today is an operations console and this
> asks for a working surface, which are different products; **settle the isolation model FIRST, and
> record it as a decision, because it is not a layout question**; and mid-run visibility is
> load-bearing, not decoration. Boundaries restated in the contract: D38, D31, D37, D24. Expected
> output: a design document, a decision entry, and a build order of session-sized items — not code.

✅ done 2026-09-21 (design; the build continues as SURF2–SURF6) —
`docs/2026-09-21-working-surface-design.md`, recorded as **D51** (the isolation model) and **D52** (the
surface). Every question of research §5 is answered, and the collision of §4 is settled first.

**The isolation model (D51): the tree is the unit of exclusion, and a repository may have more than
one.** "One active session per repository" turned out to be two claims welded together — *two agents in
one working tree corrupt each other's git state*, which is the reason and does not move, and *a
repository has one working tree*, which is not a fact about git at all but about how the registry was
built. Unwelding them keeps the guarantee and drops the incidental cap. **D46 §9 survives intact**: the
planner keeps one *driven* session per repository, because pacing a domain and preventing corruption
are different jobs and a shared mechanism would make one answer the other; what the second tree buys is
the person and the driver coexisting, and a person is not a second workstream. Nine rules make it safe,
each with its reason — the registered root is the only tree that feeds (WSP4 untouched); Daoris owns the
location and git the contents (the credential-profile arrangement, D49 §4); a tree exists only on
request, so silence is today's behaviour byte for byte; **a fresh tree holds nothing git does not
track, and the sentence that creates it says so**, because the missing dependencies are this
decision's real price; the clean-tree rule stays on the root and is vacuous in a fresh tree, which is
how a person's work in flight stops holding the driver without a session being entangled with it;
nothing merges itself and nothing deletes itself; `connect` from a linked worktree is refused naming
the main one, because a re-pointed registration keeps working right up until that tree is removed; and
a tree path is machine-local material carrying the transcript's three guards. Rejected with reasons:
keeping the repository as the unit (coherent and free, and it caps the product permanently — with
nothing deployed, the unit of exclusion is the worst thing here to retrofit), a container per session
(isolates the toolchain too, so every harness, profile and gate must live in an image somebody
maintains), and one directory switching branches (serialises exactly what must run at once).

**The surface (D52).** Work is a **sixth view, not a second application** — D41's language stays whole
and only Work's shape is new, because inverting the shell around sessions produces a second visual
language inside one app within a week. Sessions are **grouped by repository with derived identity**
(tab overload is the field's named anti-pattern; hand-naming waits until two real sessions cannot be
told apart). The **stream is promoted out of the drawer and gets one home**, with a **timeline of what
was observed** beside it — state and quest transitions, tool and account, commits landing — and
**parsing the stream into steps is rejected by name**: the field's activity panel assumes structured
events, Daoris has none, and the only way to get them is to screen-scrape another program's stdout,
which is the coupling D23/D24 exist to prevent. **`AwaitingPerson` finally has a surface** — its
analysis at the top of the head and exactly the three moves the ledger already allows, no new states —
beside Overview's *what needs you* band, two sidebar counts, and an **OS notification on park and on
end, never for an ending the person caused**, which closes driver design open question 5 as the shell's
own code. **Review becomes a diff**, computed where the tree is and carried over the bridge
(machine-local, desktop-only, D47 §4), bounded and stating what it truncated, with merge and discard as
the person's explicit acts — merge stays a press because it is where D37's verification lands. **No
PTY**: SES2's transcript-and-input is kept, the escape hatch is the person's own terminal via
`daoris-driver chat`, and the open question carries its trigger. D37 is restated in the form this
surface needs — **a better approval surface must not widen autonomy** — and the research's progressive
delegation is declined by name, because approval fatigue trains the reviewer and a surface that learns
from a trained reviewer learns the wrong thing.

The build order is five session-sized items, SURF2–SURF6, in `TASKS.md`: the lock keys on the tree
(behaviour identical, proven by the rehearsal), session trees, the Work view, attention, review. No
code landed; the verification plan names what each loop owns, including the browser's negative
guarantee — a browser sees records and never a stream, a diff, a tree path or a notification setting.

## DEV1 — a dev loop for the desktop shell (2026-09-21)

> Asked for by the owner the same day: "you can create devtools for desktop development too" — with a
> family sibling's `devtools/` named as the example, and the instruction to **review it and take only
> the tools needed**.

✅ done 2026-09-21 — `tools/desktop.mjs` (+ `tools/cdp.mjs`, `tools/shot-window.ps1`),
`npm run desktop`, 9 tests in the CLI suite, documented in `src/Daoris.Desktop/README.md`.

**The gap it closes.** Every other surface here has a loop that can see it; the shell has none.
Playwright cannot reach it by construction (no bridge in a browser) and the vitest loop drives a
*mock* of this machine — so the Machine view, the driver controls, the console and chat had never
been seen by anything but a person opening the window. REV2 is what that costs. Seven commands:
`doctor`, `build`, `run`, `restart`, `kill`, `shot`, `eval`, `click`. **It is deliberately not a
gate** — it declares nothing in `daoris.gates.json` and asserts nothing about the product; it is the
instrument, and the seventh gate it would otherwise have become is a seventh row two lists must agree
on.

**What was taken from the sibling, and what was left.** Taken: the zero-dependency CDP client (Node's
global `WebSocket` is all it needs — a dev tool that drags in a driver library cannot run on a fresh
clone), the PrintWindow capture with `PW_RENDERFULLCONTENT` (the plain flag captures the chrome and
leaves the page blank), and three lessons each bought by an incident there — discriminate the process
by **executable path** rather than name, **identify the page before reporting its answer**, and prune
the capture folder by policy rather than by memory. Left: its 55 KB dispatcher (the hand-copied
`dev.mjs` is the pathology `Daoris.Devkit` exists to remove), everything mobile, media, mac, iOS or
LAN, and every static sweep — `check-sensitive`, `doc-claims`, `dead-i18n`, the layout audits — because
Daoris's devkit already owns the universal gates and `Daoris.Web` already owns its i18n parity gate.
Left with a trigger: the native background-input tool (CDP already drives the page, so it earns its
place only if a check needs input the page cannot receive) and the runtime's `__shenora` dev
interceptor (it would let `eval` call IPC modules directly, but it needs a production-reachable enable
path, which is a product change rather than tooling).

**A dev run gets its own machine — a safety property, not a convenience.** The shell runs the driver
loop, and the driver spawns real agent sessions in real repositories. So `run` redirects every
`~/.daoris` file, **clears** the remote environment pair (inherited, the hermetic-rehearsal mechanism
would feed a real deployment from a scratch store), takes its own port, passes `--app-root` so the
WebView2 profile and window state are its own, and copies `examples/` to work over. `--real` is
spelled out. A test asserts the redirect list against every source that builds a `~/.daoris` path,
because a missing name does not fail — it edits the person's real config — and both that test and the
process-name pairing were sabotage-checked.

**Two couplings found by running it, which is the argument for the tool in one line.** The debug port
needs `DOTNET_ENVIRONMENT=Development` as well: the runtime sets `AdditionalBrowserArguments`, which
makes WebView2 ignore `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS`, so it re-appends that variable itself
and only in dev mode — which is exactly why a shipped window has nothing to attach to. And a scratch
port needs `ASPNETCORE_URLS` as well: the shell *probes* `DAORIS_SERVICE_URL` while the host *binds*
`ASPNETCORE_URLS`, with nothing passing one to the other, so moving only the probe leaves the window
on its splash forever. Proven end to end on the real window: started on a scratch machine, read back
its six nav items **including `Machine`**, clicked through to Projects, and captured a 2560×1600 PNG
of the WebView2 composition. Nothing in the shipped app changed — the whole debug surface is two
environment variables a dev run sets.

## SURF1a — the working surface's component plan (2026-09-21)

> Asked for by the owner after the design landed: "because this is a large UI/UX as a whole you
> probably need to develop this component by component — more of an atomic design pattern — so that
> you can test each part one by one (you can setup a development plan for this)."

✅ done 2026-09-21 — `docs/2026-09-21-working-surface-components.md`, recorded as **D52's amendment**;
SURF4 is now SURF4a–d in the backlog, and SURF5/SURF6's UI halves name their molecules.

**What it changes: the build, not the design.** Every existing view was built whole and is tested
whole (`shell.test.tsx` drives three views end to end), which is right for a list and a drawer. The
Work view is a rail, a head, a live stream, a timeline, a composer, an attention band and a diff
pane — assembled as one file, **the first thing that renders is the last thing**, and the states that
matter most (nine session states, a parked session, a stream that dropped 12,000 lines) are reachable
only by arranging the world that produces them.

**The layer is a dependency rule, not a folder chart.** Atoms → molecules → organisms → page → shell,
with the one load-bearing rule: **a molecule imports no hook**, so every state is reachable by passing
props — and it is *checkable*, so a test asserts that no presentational file imports `./queries` or
`./shell`, in the same shape as the gate-list and catalogue checks already here. Three loops per part,
in order: a story first (D42 — and a story for a component that does not exist is the cheapest failing
test), vitest (props for a molecule, the mocked bridge for an organism), then the real window via
DEV1's `npm run desktop -- run|eval|shot`, which is the loop that did not exist when the other views
were built. Plus one multiplier with no new gate row: `composeStories` renders every story inside the
existing vitest run.

**Deliberately not adopted: the `atoms/molecules/organisms/` folder taxonomy.** It scatters one
feature across three directories and starts a taxonomy argument on every file; the surface's
components sit together in `src/work/` and shared atoms stay in `ui.tsx`, where the other four views
already find them. Recorded as reversible — moving files later is mechanical, unpicking a hook out of
twelve molecules is not.

**The inventory is derived, not invented** — every row cites the design section that asks for it: three
atoms (`Dot`, `MonoWell`, `MetaLine`), two pure helpers, seven molecules, six organisms, one page. The
helper worth naming is **`SESSION_TONE`**: `QUEST_TONE` exists because a fifth quest status must not
ship half-toned, and a session pill today is `live ? taken : neutral` — eight states wearing two tones.

## SURF2 — the lock keys on the tree (2026-09-21)

> The session record names the **tree** it runs in; `SessionStore.ActiveForAsync` and `SessionLedger`'s
> two refusals key on that instead of the repository name, and both sentences name the tree that holds
> it. **Nothing creates a tree yet** — every session's tree is the registered root, so behaviour is
> identical and the family rehearsal proves it, which is the whole point of landing this separately.
> The session store **adds a column** and preserves its rows. The tree path is **machine-local
> material and inherits the transcript's three guards** — stripped for a non-loopback caller, absent
> from the feed's shape, a literal NULL in the store's mirror — and the rehearsal's byte-level scan of
> the remote store grows a third string to look for, beside the root and the profile name.

✅ done 2026-09-21 — D51 in code. Service 248 → 258, family rehearsal 154 → 156, everything else
unchanged and green.

**What moved.** `Session` gains `Tree`; the store gains a `tree` column (ALTER, rows preserved, and
the D49 rebuild path carries it); `ActiveForAsync` takes the tree; the ledger resolves and keys on it;
`ServiceClient`, `Driver` and `ChatRunner` state the root they are about to spawn into; the HTTP
request and response carry it, guarded. **The planner is untouched** — one *driven* session per
repository, oldest quest first — because pacing a domain and preventing corruption are different jobs
(D51), and the whole point of SURF2 is that it changes no behaviour at all.

**Three choices the building settled.**

- **Unknown means "possibly yours", on either side.** A row with no tree — anything from before D51,
  or a record mirrored from a machine that rightly sent no path — holds *every* tree in its
  repository, and an ask that names no tree is answered by any active session there. The lock errs
  toward refusing, because a wrong refusal is a sentence naming what holds the tree and the other way
  round is two agents in one working tree. It is one SQL clause (`$tree IS NULL OR tree IS NULL OR
  tree = $tree`) and it is the reason the upgrade needs no backfill the store could not honestly make.
- **Convergence comes from one source, not from clever comparison.** Both doors resolve an unstated
  tree through the registration's root, so a caller that names the root and one that says nothing land
  on the same key by construction. `Trees.Normalize` (trim, drop a trailing separator, keep a bare
  root path whole) is the safety net, not the mechanism — and it deliberately does **not** fold case,
  because a deployment may run where paths are case-sensitive and two real trees must not collapse.
- **The refusal names the holder, never the holder's PATH.** A message is the one surface with no
  strip on it: composed in Core and rendered verbatim wherever it lands, including a browser over a
  keyed remote. So the sentence became "`x`'s working tree already has an active session — `<id>`
  (working, quest `#…`). One session per working tree: two agents in one tree corrupt each other's git
  state." Tested both ways — the wording, and the absence of the path.

**Proven, not asserted.** Ten new tests in the service suite (two trees in one repository running at
once; a stated root and an unstated one colliding; a trailing separator not being a second tree; a
chat opening beside a driven session in a tree of its own; the conservative rule; the mirror dropping
the tree; a pre-D51 record reading with none). The rehearsal gained two checks over the real host —
**a second tree of the same repository is not blocked and the record names it**, and the remote store
scanned specifically for a tree path — and its existing chat/driven lock checks passed unchanged,
which is the claim that behaviour did not move.

**Two things found by doing it.** `tools/desktop.mjs` was orphaning a service host on every restart
(`docs/FIX-LOG.md`) — found because the orphans locked the assemblies this very build had to
overwrite. And the Playwright suite aborted one worker with `0xC0000409` on the first run and passed
clean on the next, which is a known family shape rather than anything this change touched; it is now
**TEST1** in the backlog, held, rather than folklore.

## SURF3 — session trees (2026-09-21)

> `git worktree` under the trees home, created **only on request** and opt-in per repository, with
> both editors (D50). Branch named per session, never reused, based on the canonical line as WSP4
> resolves it. Four rules carry the risk: the creating sentence states that a fresh tree holds
> nothing git does not track; `connect` from a linked worktree is refused naming the main one;
> removal refuses to destroy work; and the feed still reads only the registered root. The clean-tree
> rule stays on the root and is vacuous in a fresh tree.

✅ done 2026-09-21 — D51's worktree half, in code. CLI 203 → 209, driver 130 → 141, modules 39 → 40,
family rehearsal 156 → 166; service, devkit and web unchanged and green.

**What landed.** `SessionTrees` (open/list/remove over real git — `WorkingTree.GitAsync` went internal
so there is one process-spawning implementation); `DriverConfig.Trees` + `OpensOwnTree`, round-tripping
through the same file both editors share; the driver growing a tree **before the record exists** (the
same place every other refusal is asked) and skipping the root's clean check for opted-in repositories
— which is the whole point; `ChatRunner`'s `ownTree` and `daoris-driver chat --own-tree`;
`daoris-driver trees list|remove [--force]`; `daoris driver trees <repo> on|off` in the CLI, stating
the price where the choice is made; `SET_TREES` + the Projects checkbox + both catalogues (245 keys);
and `connect` refusing from a linked worktree **without a spawn** — git marks one itself (`.git` is a
FILE naming `.git/worktrees/<name>`), so reading one file answers it and a submodule's
`.git/modules/<name>` marker correctly does not match.

**The split that placed the verbs.** The standing opt-in is a file edit, so it lives in the `daoris`
CLI; the tree lifecycle is git spawned against real checkouts, so it lives on `daoris-driver`, which
already owns git — D50's parity is between *surfaces*, not between packages, and the CLI's no-spawn
discipline (only `toolchain.ts` spawns) stays intact.

**The incident that became a guard.** The first run of the new test suite created worktrees and
branches **on the Daoris repository itself**: git resolves a repository by walking UP, so a fixture
folder that merely sat inside this checkout grew `daoris/s-*` branches on it. `OpenAsync` now proves
the root IS the top of its own working tree (`rev-parse --show-toplevel`, compared normalized) and
refuses a root that is inside some other repository — naming both paths — and the test that found the
bug now pins it. The strays were pruned the same minute; nothing reached the index.

**Proven over the real loop** (rehearsal §16): the root deliberately DIRTY — the very state that held
the driver in §7 — and the session ran anyway in its own tree, `completed`, with the person's file
untouched, no commit on the root's HEAD, and the record naming a tree under the driver's home;
`trees list` named it; removal **refused** while the stub's commit sat unmerged, quoting the commit,
and `--force` was the person meaning it — tree and branch both gone; `connect` from inside the tree
refused naming the main one; and the tree fed nothing — no registry row, no search hit — because a
session tree is a place to work, not a repository (D51 rule 1).

## DSH1 — evaluate dsh, and decide what Daoris hands it (2026-09-21)

> One session, next. Run the plan's eight probes against the local checkout and an installed `dsh`
> over a scratch repository — headless run, the ACP session and its event vocabulary against the
> timeline's needs, claude-code's ACP story, the hook-config bridge, subagent delegation to real
> claude-code, the approval map onto D37, the breaking-change price, and (only if still live) the
> one-trivial-plugin build cost. DOCS1 runs as a strand of this session. Output: an evidence note, a
> numbered decision naming the chosen and rejected options with reasons — the owner decides; option D
> reopens D1 and says so — and a build order. `deepseek-harness` is at developer preview: pin exactly,
> vendor nothing, price the churn (probe 7).

✅ done 2026-09-21 — `docs/2026-09-21-dsh-evaluation.md` (the evidence), **D53 proposed** (the
decision, for the owner to confirm or amend), and a build order of three items, ACP1–ACP3, plus DOCS2
from the strand. Nothing in the codebase changed; the probe instruments are tracked under
`tools/dsh-probes/`.

**How it ran.** `@deepseek-ai/dsh@0.1.6-alpha.2` pinned exact into a gitignored scratch prefix (260
packages, 561 MB), a scratch harness home, a scratch git repository, telemetry off — and, because no
model key was supplied for the session, a **scripted provider**: a small OpenAI-compatible server
answering a fixed plan of tool calls, declared to dsh as a custom route in its own `settings.yaml`. The
note says at every probe what that tier proves (the harness's mechanics) and what it cannot (a real
model finishing a quest-shaped task — DRV4's shape for dsh waits for a key).

**What the probes found.** Probe 1: the headless loop dispatched `write` and `pwsh`, landed a commit,
exited 0 in 3.7 s and wrote a 30-event Zstandard-framed session log — after teaching four things on the
way: the default route speaks an Anthropic-style Messages protocol; **dsh makes model calls the loop did
not ask for** (the session-title generator ate a plan step); the Windows sandbox's restricted token fails
git's ownership check on an admin-owned checkout; and **dsh's credential scrub strips any child
environment name containing KEY, TOKEN or SECRET**. Probe 2: an ACP session over stdio streamed
`tool_call → tool_call_update → agent_message_chunk` with usage, `end_turn` in 276 ms — the timeline's
structured source with nothing parsed. Probe 3: `claude` speaks no ACP and `codex` speaks its own
`app-server` protocol; the ACP project's Apache adapters (`claude-agent-acp` 0.79.0, `codex-acp`
1.12.0) do, and the Claude one **ran the machine's managed `claude` through `CLAUDE_CODE_EXECUTABLE`
under an empty `CLAUDE_CONFIG_DIR`** — session created, state written to the scratch profile only, no
model called, Claude Code's permission modes exposed as ACP modes. Probe 4: the repository's own
`PreToolUse` hook fired and blocked a push — **but only in the structured-deny form; exit code 2 was
collapsed to 1 by Windows PowerShell 5.1**, which dsh's executor falls back to, and hooks run *inside*
the sandbox. Probe 5: the Claude subagent bundle on npm is six weeks stale and not a profile layer, yet
composed and advertised `subagent_claude_code`; the delegation itself was **held** because it would
spend the person's own account. Probe 6: a write outside the workspace was denied by the ACL sandbox,
the escalation asked for approval and got `unavailable`, the file does not exist — fail-closed, verified
against the world. Probe 7: 26 prerelease tags in five and a half weeks, 1,687 commits in the last one;
bind to the standard, not the product. Probe 8: not run — C did not survive the boundary reading.

**The proposal.** B with A folded in: an ACP door on the adapter seam; dsh and codex as configurations;
the surface stays `Daoris.Web`; C and D rejected, D on D1's terms. Observation (D46), no model named
(D24), streams on the machine (D47 §4), the tree as the unit (D51) and the D37 line all stand; D23
evolves, with "on proof" now meaning ACP2's real driven run.

## DOCS1 — study deepseek-harness's documentation system, and take what converges (2026-09-21)

> Runs as a strand of DSH1, not separately. Its `docs/AGENTS.md` is a documentation *standard with
> gates*: a one-home-per-fact tier taxonomy, per-document word budgets in a manifest enforced by
> `verify-doc-budgets` with a relocate → condense → raise discipline, fenced `ts` blocks that must
> compile, generated reference regions that are freshness-gated, a notes lifecycle, and a per-package
> "Model Experience" section. Half of this converges with what Daoris already holds. Study first, one
> session; the deliverable is a comparison note naming what is adopted and what is already covered.

✅ done 2026-09-21 — the comparison is §3 of `docs/2026-09-21-dsh-evaluation.md`. Converged, and
therefore D17-grade evidence for what Daoris already holds: one home per fact (D7), the byte budget's
split-not-raise discipline (D28), the decision log with its rejected alternatives, generated and
freshness-gated indexes. Taken, as one backlog item (**DOCS2**): a doc-budget manifest and a link
check as devkit gates for this repository's own always-read prose, the "Rejected" line asserted on new
decision entries, and the slop checklist folded into `post-feature`. Left, with the reason stated:
compile-checked doc fences (few fences here yet — the shape is noted for the day a design document
pastes a declaration), one-line paragraphs (Daoris hard-wraps on purpose), and bilingual doctrine (the
platform already holds the rule where it matters). No code ported, so no notice owed.

## ACP1 — the protocol door (2026-09-21)

> `ISessionProtocol` beside `ProcessStartInfo` on the adapter seam (D23 evolves, not breaks): a
> JSON-RPC client over the spawned process's stdio; `session/new` on the tree (D51); the composed
> target as `session/prompt`; `session/cancel` then stdin EOF as the stop shape; updates teed into a
> **structured** console stream beside the verbatim one — desktop-only, console-class (D47 §4);
> `session/request_permission` answered by the D37 posture, **failing closed and never wider** (D52).
> **Records still move on exit code + quest state** (D46). Proven by a **stub ACP agent** in the
> family rehearsal with no model.

✅ done 2026-09-21 — the first half of D53, and the door a real harness rides in ACP2. Driver tests
141 → 149, service 258 → 259, family rehearsal 166 → **173/173**.

**What landed.** `AcpSession` — a JSON-RPC 2.0 peer over newline-delimited frames that takes *streams,
not a process*, because spawning stays the driver's (D46 §5) and a class owning a process could not be
tested without one. Every rule it holds is proved against two in-memory streams and a fake agent.
`SessionWire` on the seam, defaulting to `Pipe`, so every adapter that existed before the door behaves
exactly as it did — the silence-preserves rule the toolchain and the session trees already follow.
`AcpStubAdapter` (`acp-stub`), the same fake-binary trick one door over, so the protocol is gated with
no model, no account and no credential. `Driver.CaptureAcpAsync`, where stdout belongs to the protocol,
stderr still reaches the transcript, and the **rendered** updates are what a person reads.

**The rule the door exists to hold.** A permission request is **refused, always, and by the option's
kind rather than its position** — and with no refusal offered the answer is `cancelled`, never the
first option that happens to be there. A request reaching the driver means the repository's own
checked-in posture did not already cover the action, and the driver is a component, not a party to the
work; widening at runtime is exactly what D52 forbids. The harness's standing posture is set where it
belongs, at session creation, so ordinary reversible work never reaches that path. The rehearsal proves
it end to end: the agent asks to push, the driver refuses, and **the agent is told** — the refusal is in
the transcript from both sides.

**What the wire is not allowed to do.** It flattens `aborted | blocked | error` to `end_turn`, so its
stop reason cannot tell a refusal from a success. The record still moves on the exit code and the
quest's state, and the wire's ending is written into the transcript as a self-report *saying so*. A
rehearsal check reads that sentence back, because the tempting simplification is to believe the wire.

**Three failures the building found, each now pinned.** A cancellation callback re-entered the write
lock on the thread that held it and deadlocked the whole run — `SemaphoreSlim` is not reentrant, and
the fix that lasts is having no path that can re-enter rather than a lock that tolerates it, so the
cancel is sent after the await unwinds and every write is async. A courtesy `session/close` that the
agent never answered hung a run that was already over — now bounded, with a test. And the stub agent
awaited its permission answer *inside* its own read loop, so it could never read the reply: the driver's
two-minute timeout is what reported it, and the agent now handles frames without blocking its reader.

**Two things found by running the gate, unrelated to the door** (both in FIX-LOG): the rehearsal's
"which commit speaks" phase carried a fixture with an expiry date and aged out mid-morning, and the
stale-feed refusal printed local wall-clock time and appended `Z`. The ordering was right and its
explanation was wrong, which is the worst shape for a sentence whose job is to convince a person that
being refused is fine.

## CANON7 — the core budget's number, and what a budget is for (2026-09-21)

> Decide whether this repository's own `coreBudgetBytes` moves to the D28 default. An owner decision
> first, then a one-line change and the prose that cites it. Daoris's manifest carries `24000`,
> written before D28 moved the default to 30000; the always-loaded core now sits at 23,862, so 138
> bytes remain and the next canon addition fails the gate. Do not decide this by building it. Bring
> the owner the two readings. **CANON5 is parked behind this.**

✅ done 2026-09-21 — the owner took the recommendation (**26000**, not the 30000 default) and then
changed the question, which turned a one-line edit into **D54**.

**The briefing, and why 26000.** The pre-D28 number was wrong here by D28's own argument: it fires on
the canon rather than on a repository's own material, and this is the one repository whose
always-loaded material *is* the canon — it declares no packs and owns no always-loaded rule, so the
field measures the core exactly. The counter-argument had kept it for six weeks and was good: a tight
self-imposed limit is a forcing function, and CANON6 proved it working. What ended it is that **138
bytes is not a forcing function, it is a wall** — at that margin every candidate rule fails on
arithmetic before anyone weighs whether it is good. 26000 leaves room for about one substantial rule;
30000 would have handed over 6,138 bytes and retired a constraint that had just worked.

**Then the owner moved the real question: "I don't really think the budget should be a hard cap."**
That is D54, and it is the more interesting half. Both budgets now report and never fail. The line it
draws is the keeper: everything else `check` reports is a **fact the tool established** — a hash that
no longer matches, a missing file, a pack never synced, a stale index — while a budget is a
**judgement**, and one byte past a number somebody chose is not wrong. A gate that stops a build over
a judgement gets its number raised rather than read, which is D28's own warning about noise arriving
from the other direction. **A fact gates; a judgement reports.** A stale ceiling — one naming a
document that moved — still fails, because that is a defect in the manifest rather than an opinion
about length, and it looks from the outside exactly like a document comfortably under budget.

**What it cost to be consistent.** The doc-budget gate built hours earlier as a hard gate was softened
the same day, rather than leaving one budget blocking and the other not — an inconsistency somebody
re-litigates later. Watched in both directions afterwards: over a ceiling warns loudly, quantified,
and exits 0; a stale ceiling exits 1.

**A measurement worth keeping, found while deciding.** The generated rules index is 5,246 bytes — 22%
of the core — and grows with the *count* of documents, local knowledge and skills included, neither
of which is itself always-loaded. Part of the pressure on this budget is the index of the doctrine
rather than the doctrine. Not acted on: an index nobody loads is a tier nobody reads (D7).

**CANON5 is unparked**, and it is now the question it always should have been: whether an i18n parity
rule belongs in the always-loaded core at all, or as pack knowledge for web repositories. The budget
is no longer answering that on its behalf. CLI 210 → 211.

## DOCS2 — what the docs strand takes from dsh (2026-09-21)

> A doc-budget manifest with ceilings for this repository's always-read prose as a declared gate with
> the *relocate → condense → raise* discipline — D28's principle extended from the canon to the
> repository's own standing orders; a markdown link check beside it; the "Rejected" line asserted on
> every new decision entry; and dsh's slop checklist folded into `post-feature` as a documentation
> pass. Nothing ported, so no notice owed.

✅ done 2026-09-21 — three of the four landed, the fourth was struck on contact with the code, and the
search for somewhere to put a gate turned up a finding that became its own item. CLI 209 → 210.

**The link check was already built.** `LinksGate.cs`, eight tests, relative targets only because no
gate may touch the network. The evaluation note had proposed building one, because that half of the
study was written from dsh's inventory rather than from Daoris's own source — `claims-need-checks`
catching its author. The note's §3 row now carries the correction rather than the proposal.

**The budget gate went to `tools/`, not the devkit, and the reason is the finding.** Nothing runs the
devkit *binary* over this repository: the workflow runs its test suite and then each declared gate by
name, and there is no `.githooks/`. So the universal gates this repository configures in
`daoris.gates.json` are configuration nothing reads, and a budget gate added there would have been an
unchecked claim. Run by hand the devkit exits 1 on six sensitive findings, all in test fixtures, each
needing its own judgement — **DEVKIT3** now carries that. The mechanism is universal and the ceilings
are not, so the graduation rule is stated where it will be read: the day a second repository wants
one, it moves into the devkit as a declared option, which is CANON2's two-repository bar applied to
tooling.

**What the budget is, and what it refuses to cover.** Five documents, the ones a session reads
*whole*: the standing orders, the backlog, the consuming story, the forward sequence, the contract.
The append-only records — the decision log, this archive, the fix log, both changelogs — deliberately
have none, because they are read by lookup, they grow by design, and a ceiling on one is a rule that
eventually says to delete history. That distinction is most of the tool's content; the counting is
fifty lines. Ceilings are set with headroom over today, and the run reports the *tightest* document
rather than a total, because "which one is about to go red" is the only question the report can
usefully answer. Watched failing in both shapes: a document over its ceiling, and a ceiling naming a
document that moved — the second because a stale ceiling silently stops applying and looks exactly
like a document comfortably under budget.

**The rejected-alternatives assertion binds new entries only, and that is the honest version.** 41 of
53 entries carry no such line. Backfilling them would mean inventing alternatives nobody weighed —
dsh's own rule is *alternatives are recorded, never invented*, and it grandfathers its pre-rule notes
for exactly this reason. So the line is the number: every entry from D51 on, which is where the
practice already is. **The guard caught a bug in the test before the test caught anything**: the
obvious regex for "this heading to the next" needs an end-of-input anchor, JavaScript has none —
`\Z` is a literal `Z` — and the first version silently dropped the *newest* entry, the one the rule
most exists for. The `found N, so it proved nothing` assertion is what turned that from a green test
into a red one, which is the fourth shape in `claims-need-checks` catching itself in the act.

**The prose pass is a skill, which is why it was free.** Nine shapes from dsh's slop checklist,
rewritten project-agnostic for a canon skill: the same rule in two places, history outside the record
that holds it, status annotations that rot, a hand-restated catalogue, the path taken to the answer
rather than the answer, rationale repeated beside each sibling, the paragraph carrying four rules,
emphasis everywhere, intent where the record should state fact. A skill's body costs nothing unasked —
only its `description` is loaded — so the always-loaded core did not move, and the `description` was
left untouched on purpose. Canon change, so the examples re-synced in the same commit.

## WSP5 — the platform's workspace switcher (2026-09-21)

> The one thing §8's Web row promised that the WSP arc did not land, and it was left deliberately:
> Projects shows each repository's circle and its fed commit, the Machine view shows the wiring, but
> **no view filters by workspace**. Workspace design §4 states the shape — "one more filter, not a
> new view" — over the `workspace` argument the search, registry and convergence doors already take.
> The honest scope question to answer first: whether the switcher is a global chrome control (one
> circle at a time, like a git branch) or a per-view filter; §4's "scoped to one workspace per query"
> argues for the first, and the second is what a filter usually becomes. Web-only; no service change.

✅ done 2026-09-21 — **global chrome, not a per-view filter.** Web only, as promised: every
cross-repository door already took `workspace`, and nothing in the service moved. Vitest 55 → 65,
Playwright 9 → 10, both catalogues 245 → 248 keys.

**The scope question, answered — and the four choices that followed.** The switcher sits in the
sidebar's foot with the rest of the global state (the platform language's rule that global state lives
in exactly one place), because §4's rule is that a query names one circle and five per-view filters
can disagree — Overview counting one circle while Quests lists another is exactly the silent mixing
the design forbids. It is **absent while the deployment holds one workspace** (silence is today's
behaviour byte for byte, the same rule as session trees). **"every workspace · N" is the stated
default** — the D24 shape, report the scope that ran — where the door itself never invents one. The
choice is **remembered per browser** like the language, never machine wiring and never in a tracked
file, and a remembered circle the deployment no longer holds **falls back to every, out loud**. And the
scope **rides the query layer**: every cross-repository hook reads it and carries it in its cache key,
so no view changed a call and no circle's answers can serve another's from the cache.

**What landed.** `scope.tsx` (the context and provider); `WorkspaceSwitcher.tsx` (props-only — no hook
from `./queries` or `./shell`, the component plan's rule — with a story per state including the
absence); `queries.ts` (the scope in every key, `useWorkspaces` as the registry unscoped — the one
reader that must see every circle — and invalidation moved to the `all*` prefixes); `api.ts` (one
query-string helper that omits what is unset, so a door asked with no workspace is asked for every
circle it holds); the shell's foot and the provider around the shell; both catalogues; and two jsdom
shims Radix Select needed — pointer capture and scroll-into-view — because no test had opened a select
before.

**Proven in three loops.** Props: the four states. The query layer: a chosen circle rides quests,
registry and sessions; nothing chosen names none; a circle called 工作区 travels encoded. The shell in a
browser over two circles: the stated default, one choice scoping the badge's quests, the foot's count
and the landing view's registry, a remembered-but-gone circle falling back. And Playwright over the
real host: the control absent while the family is one circle, present with its count once the newborn
is re-wired into a second through the same door the desktop's form uses, one circle chosen scoping
Overview and Projects, the choice surviving a reload, and every again — re-wired back at the end so the
tests after it inherit the family they were written against.
## SURF4a — the parts everything else is made of (2026-09-21)

**What it was.** The first of the four cuts the component plan makes through SURF4
(`docs/2026-09-21-working-surface-components.md` §4–§5): three atoms with a story per state, two pure
helpers, the presentational-import check, and stories wired as smoke tests. Taken on the owner's
direction to *"polish the ui/ux into a more usable state"* before any walkthrough testing — the
designed path, and the one that puts every state in front of a reviewer before a view exists to hide
them in.

**The two questions §7 said to settle, settled.** `composeStories` is exported by
`@storybook/react-vite`, which is **already a direct devDependency** — so the stories-as-smoke-tests
half cost no dependency at all, not even the one line the plan budgeted for it, and was kept rather
than dropped. The import check lives as **a vitest test in the web package**, as preferred: the gate
list is two lists that must agree (`daoris.gates.json` and the release workflow, and they silently did
not once), so a check that needs no new row is worth more than one that reads tidier.

**A correction to the plan, found by reading the code it describes.** §4 justifies `SESSION_TONE` with
*"today a session pill is `live ? taken : neutral` — eight states wearing two tones"*. That was already
false: `QuestsView.tsx` held an exhaustive nine-state map. So the work was to **move** it, not write it
— and the reason to move it is the better one anyway: the rail, the head and the quest card are three
readers, and three copies of a nine-row map disagree eventually. The doc has been corrected rather than
quietly satisfied.

**What landed.** In `ui.tsx`: `Dot` — the mark is `aria-hidden` and `label` is **required**, so a dot
without its word is not a state the component can reach (D41 §6 held by construction rather than by
review); `MonoWell` — the verbatim well extracted from `SessionConsole`, with tail-following, the tall
variant, and the "what fell out" line moved from the header to a **footer**, where it reads as what the
window cost rather than as a property of the label; `MetaLine` — `label · value` pairs where **an
absent value omits the pair**, never renders it blank, because on a session record every absence means
something real and a dash in the value slot reads as a bug in all three cases (`format.ts`'s
`sessionTool` already argued this for its own three fields). And `SESSION_TONE`, moved.

`src/work/identity.ts` is the new home for derived identity, with `sessionTitle(session, quest)`: the
quest's title, else the quest **reference** rather than an invented name, else the kind's word. The
design also names "the conversation's first line" as a chat's identity; the record does not carry one
(`Note` is what the driver observed), so a chat wears its kind — and the doc comment says so, because
this function existing is exactly what makes that a one-place change when the composer lands (SURF4d).

`SessionConsole` is now a six-line organism over the atom: it holds the hook so the well holds none.
Three catalogue keys moved out of `quests.session.*` into `console.*` — the well is no longer the quest
drawer's, and leaving a generic atom reaching for a view's key is how a namespace stops meaning
anything. Two new keys for the derived names, both languages.

**Proven.** 99 web unit tests, up from 65. The boundary check was sabotaged twice: once inside the test
against a fabricated source (which proves the matcher), and once for real — an offending file dropped
into `src/work/`, the suite watched to fail naming it, then removed (which proves the *glob*, the half
a fabricated string cannot reach). It also asserts it is looking at files at all, because a glob that
matches nothing passes every assertion under it. All fifteen stories across three story files now
render in the inner loop, found by a glob rather than a list, so a new `*.stories.tsx` is covered by
the act of existing. `src/vite-env.d.ts` was added for `import.meta.glob`'s types — the standard Vite
scaffold file this package had gone without.

**Not done here, by design.** No view, no molecule, no real-window pass: the plan puts the first
`npm run desktop -- shot` of an assembled region at SURF4c, and there is nothing assembled yet to
shoot. The console's footer move is the one visible change in the running app.

## SURF4b — the rail (2026-09-21)

**What it was.** The second cut of SURF4 (`docs/2026-09-21-working-surface-components.md` §4–§5):
`SessionRow` and `RepositoryGroup` as props-only molecules, `SessionRail` as the organism over them,
plus the two facts **D55** adds to a row. The first item of this arc that renders something a person
would recognise as the working surface.

**The row's anatomy, and the fact each part exists for.** The mark and its word, what the session is
*for*, how long it has been going, and a line of secondary facts. `elapsed` is the one D55 asked for
by name: "moved 4m ago" reads identically for a session three minutes old and one three hours deep,
and those are different situations. `sessionOrigin` is the other — the session feed keys a mirrored
record by `origin/id` (D47 §6) and the origin is the key's own identity, *whose key on which
machine*, so **where a session runs was already in the record** and nothing had ever read it.

**Silence means here, and it means the root.** Every secondary fact is absent when it has nothing to
say — the machine while the session is this deployment's own, the tree while it is the registered
root — which is `MetaLine`'s rule (SURF4a) applied one layer up, and the reason a rail of a dozen
rows stays readable at 18rem. The typical row says `driven · moved 4m ago`; the interesting one says
`driven · in streaming-budget · on person@machine-a · moved 4m ago`.

**The reference console's one priority rule needed no code.** deepseek-harness spends a rule on
*pending user interaction outranks own activity* — a session that needs its person wears the
attention mark even while its process is busy. Here `awaiting-person` **is a state**, so there is no
busier state for it to lose to, and `SESSION_DOT` holds it by construction. The assertion that would
fail if someone folded it into the running set is in `ui.test.tsx`, because the rule is only free
while the shape stays this way. There is deliberately **no "is a process alive" input** to the mark:
the driver's `running` list is this machine's, and a mirrored session working on another machine is
working.

**What a group header carries, and what it refuses to claim.** Drivable, held, and which tree is
busy — the repository's own facts (design §3), so a row never repeats them. Held outranks drivable,
which it suspends. **Unknown is not false**: every fact is optional, and an absent one asserts
nothing, because the driver answers only where a shell is attached (D46 §6) and a header that read
silence as "not adopted" would invent news out of a query that had not returned. `hasCheckout` is
only *asked* where a driver answered, for a sharper version of the same reason: a root is answered
only to a caller on the machine that holds it (D48 §7), so over a remote every registration would
otherwise look like a teammate's.

**Two decisions inside the rail worth keeping.** It lists what is still running **plus the attended
session, whatever state it reached** — a session that finishes while its person is reading it must
not vanish out from under them, and that is the one thing a list of running things must never do.
And **selection is not held here**: the rail is told which session is attended and reports a choice,
because one selection binds every region of the Work frame (IDE study §3) and a frame cannot bind a
selection its rail keeps to itself.

**Three helpers, each because two readers would have disagreed.** `elapsed` in `format.ts`, with a
start in this machine's future reading as brand new rather than as a negative span — records travel
between machines and clocks do not, and `-4m` in a rail is a bug report nobody can act on.
`sessionOrigin` and `treeName` in `work/identity.ts`, the latter because Daoris owns where trees
live (D51 §2), which is what makes the last segment the branch rather than a guess. And two moves
into `ui.tsx` beside `SESSION_TONE`: `SESSION_ACTIVE`, which `QuestsView` held privately and the
rail needed too, and `SESSION_DOT`.

**The per-session "own tree" control is NOT here, and that is the finding.** The backlog filed it
under this item. A tree is cut at spawn (D51 §2) and a running session cannot be moved into one, so
there is no control a row can offer — what a row can carry is the *fact*, and it now does. The
*choice* belongs to the surface that starts sessions, which SURF4d already names; the backlog entry
moved there rather than being dropped.

**A gap found while landing it.** `stories.test.tsx` globbed `./*.stories.tsx` — top level only. The
components plan says a new story file is covered "by the act of existing", and every file this item
adds lives in `src/work/`, so the roster would have silently excluded all of them while still
reporting a passing suite. The glob now reaches down. SURF4a sabotage-tested the *boundary* glob's
reach with a real file in `src/work/` and that proof said nothing about the second glob beside it —
so the lesson went into the canon (`claims-need-checks`, the "runner quietly saw fewer inputs"
shape): the hard half is the count that never **rose**, because files a pattern has stopped reaching
have no earlier count to fall from.

**Proven.** 168 web unit tests, up from 99 — twelve props-only for the row, nine for the group,
eleven for the rail over a mocked bridge, fourteen for the new pure helpers, three for the two new
maps, and twenty more stories rendering as smoke tests (that suite: 36 tests, up from 16).
`SessionRail` was added to
the presentational boundary's organism list and the boundary was watched to fail without it, naming
both imports. The web gate is green end to end: i18n parity at 272 keys, `tsc --noEmit`, the vitest
suite, the production build, and the ten Playwright specs over the example family. The canon change
re-synced both examples in the same commit, as D39 requires, and the family rehearsal holds it.

**Not done here, by design.** Nothing mounts the rail: there is no Work frame until SURF4d, so the
rail is reachable only from the inner loop and the molecules only from Storybook. No real-window
pass either — the plan puts the first `npm run desktop -- shot` at SURF4c, where a region is
assembled enough to look at.

## SURF4c — the attended session (2026-09-21)

**What it was.** The third cut of SURF4: `SessionHead`, `TimelineEntry`, `SessionTimeline`, and the
console promoted out of the quest drawer into `AttendedSession`. The region a person actually looks
at while a session runs.

**`AwaitingPerson` has a surface at last.** It has meant "only the person can clear this" since D46
and had never been rendered anywhere. Its analysis sits **above** the record, because it is the
reason the person is looking, and it renders verbatim —`autonomous-development` asks it for options,
a recommendation and a reason, and none of those survive rewording. The three moves the ledger
allows are SURF5's and attach here.

**The timeline had to be invented as a derivation, because the record has no event log.** What the
design asks for — state transitions, quest transitions, commits as they land — is not stored as
events anywhere; the session record carries `created`, `updated`, `state`, `note` and `evidence`,
and the quest carries its own. So `sessionTimeline(session, quest)` derives what those fields can
*honestly* say: it opened, the quest moved, it reached a state, and this is what came out. A session
that has only just been queued has exactly **one** entry, and that is correct rather than
incomplete — the alternative is inventing the steps nobody observed, which is the thing D52 rejected
by name. When the record grows a history, one function changes.

**A quest's move is shown only when it happened after the session opened.** Before that it is the
quest's own history, which the quest view already holds, and every timeline would otherwise open
with news that predates the thing it describes.

**`readEvidence` reads the bundle's shape, never its words.** The evidence string is built on the
driving machine (`WorkingTree.CommitsSinceAsync`) and arrives here as data. Matching `commits
landed:` would have made a C# literal and two catalogues into three things that must agree, and a
reworded header would have emptied the list silently. Instead: anything shaped like a `git log
--oneline` line is a commit and everything else is the driver's sentence, rendered verbatim like
every other system sentence. The accepted cost — a sentence opening with seven hex characters reads
as a commit — is stated in a test rather than hidden.

**Two components turned out not to be organisms, and the rule decided it rather than the plan.**
`SessionTimeline` needs only the record and its quest; `AttendedSession` is handed its session and
reaches no data at all — the one hook in the region belongs to `SessionConsole`, which already held
it so that `MonoWell` holds none. Both therefore stay inside the presentational boundary and every
state of both is reachable in a story. The components plan is corrected rather than quietly
satisfied, which is now the second time that table has been wrong in the useful direction.

**One shared atom changed:** `SectionTitle` gained a heading `level`. Inside the attended session the
head is already an `h2`, and a second `h2` under it would flatten the region's outline for anyone
navigating by headings. Default unchanged, so no other view moved.

**The console moved and was not rewritten**, which is what the plan asked for and what SURF4a's
extraction bought. In Storybook it renders nothing — there is no bridge — and that absence is the
disclosure guarantee working rather than a hole in the story, so the stories say so out loud.

**Proven.** 216 web unit tests, up from 168: ten for the derivation, eight for the head, ten for the
timeline and its entry, three for the assembled region over a mocked bridge, and seventeen more
stories (that suite: 53 tests, up from 36). The test that would fail first if anyone started
step-parsing is in `SessionTimeline.test.tsx` by name.

**Not done here, and it is a move rather than a cut.** The real-window pass the plan put at the end
of this item cannot happen yet: nothing mounts `AttendedSession` until the Work frame exists, so
`npm run desktop -- shot` would photograph the console it already had. The pass moved to SURF4d,
where the frame makes it possible, and both the backlog and the plan say so.

## SURF4d — the frame (2026-09-21)

**What it was.** The last cut of SURF4, and the one that makes the surface a place rather than a set
of parts: `Composer`, `StartSession`, `ModeSwitch`, `StatusBar`, `OutputPanel`, `WorkFrame`, the
remembered mode, and **one home for the stream**. After this, `npm run desktop -- run` and a click
on *Work* is a working surface.

**Work is a frame, and the application is a window.** *Manage* and *Work* are peers behind a mode
switch (D55); the remembered **mode** replaces the remembered view, per browser like the language
and the scope. Over a keyed remote the switch is **absent rather than disabled** — the same rule the
Machine tab follows — and a browser that remembers `work` still gets Manage, which Playwright now
holds by reloading with the preference set.

**One home for the stream, which meant deleting something.** `ChatDrawer.tsx` is gone: its console
and composer are the frame's, and starting a conversation moved out of Projects to where its result
appears. Quests keeps the record summary and **gains a door** that names the session it opens —
which is why the selection lives in `App` rather than inside the frame. Nine tests moved from
`shell.test.tsx` into a new `work/WorkFrame.test.tsx` rather than being rewritten, and the two
catalogue namespaces the move emptied (`chat.*`, `projects.chat*`) were retired, thirteen keys in
both languages.

**The composer's two endings stayed two.** Finishing lets the harness wind up (`completed`);
stopping is the person's interrupt (`stopped`). A draft the session ended underneath is **kept**,
disabled, with the ending said out loud — a box that swallows the paragraph somebody was halfway
through gives them no way back. And "nothing is listening" lands **on the composer** rather than in
a toast, because that is where the person is looking.

**The panel is a region the person owns.** Growable, shrinkable, hideable, remembered, and
**keyboard-resizable** — a resize that needs a mouse is a resize some people do not have. Closing is
deterministic: nothing reopens it but the person. `MonoWell` gained a `fill` mode for it, because a
well with a maximum of its own can be put in a taller box and simply not use it.

**Two bugs the real window caught, and neither was reachable from a test that did not exist yet.**

1. **`tree` being set is not the same as having a tree of one's own.** The ledger resolves an
   unstated tree to the registered root before recording it (D51: a caller that names the root and
   one that says nothing must land on the same lock key), so an ordinary conversation carries a
   tree — the root's path. The rail read "has a tree" as "has its own tree" and printed `busy ·
   engine` under the heading `engine`, and `in engine` on the row. `ownTree(session, root)` asks the
   right question; with no root in hand — a browser is told neither path — it claims nothing.
2. **The application was a page and needed to be a window.** The frame grew past the viewport, so
   the status bar sat below the fold and the output panel scrolled away exactly when a session was
   producing output. The shell is now `h-screen` with every region scrolling inside it, which is
   what every workbench in the reference study does and what D55 means by a frame.

Both are the argument for DEV1's instrument made concrete: 245 unit tests and 11 Playwright specs
were green, and a person looking at the window for four seconds saw both.

**What was deliberately not built.** The **right dock** — the reference frame's third column
(components plan §3a) — arrives with its second occupant, the diff (SURF6). One tab in a dock is a
pane with extra chrome, so the timeline stays in the attended column and moves when it has company.
And `SectionTitle` gained a heading `level` in SURF4c for the same region; nothing else moved.

**Proven.** 245 web unit tests, up from 216, and 11 Playwright specs — the new one holds the
**negative**: no mode switch, no rail, no console height, no composer, and a remembered `work`
falling back, all over the shipped bundle in a real browser. Then the arc's first real-window pass:
`npm run desktop -- run` on a scratch machine, a seeded record, `eval` for what the shell actually
rendered and `shot` for what it looks like. The frame's start form was driven end to end there too —
the harness roster's refusal reached the person verbatim, which is the whole path from the form
through the bridge to the ledger's own sentence.

## SURF5a — attention: the parked session gets its answer (2026-09-21)

**What it was.** The attention half of SURF5 (design §4): `AwaitingPerson`'s surface with the three
moves the ledger allows, Overview's **what needs you** band, and the count on the Work switch. The
notification half is SURF5b and is still open.

**The hole this closed.** `awaiting-person` has meant "only the person can clear this" since D46
and, until SURF4c, had never been rendered anywhere. SURF4c drew the analysis; a person still could
not *do* anything about it. Nothing in the driver produces the state either — a session parks
itself through its own door — so this is the surface arriving before its producer, deliberately and
as the design asks.

**Three moves, not the four the ledger allows.** From `awaiting-person` the ledger permits
`working`, `completed`, `declined` and `stopped`. The fourth is the **driver observing a session
that carried on**, which a person causes by *answering* it — so the surface says that out loud
("answering it in the box below lets it carry on — that is a message, not one of these") instead of
offering a second way to do the same thing. The narrowing lives on the surface's host half, not in
the ledger: which verbs belong to a person is a surface rule, and whether a move is legal at all
stays the ledger's (D36), whose refusal reaches the person verbatim.

**They go through the driver, not the service.** The obvious wiring — the page advancing the record
over `/api/sessions/{id}/state` — is wrong in one specific way: a record that says `completed`
beside a process this machine still holds is exactly the lie the observed lifecycle exists to
prevent. So `RESOLVE_SESSION` lets the process go **first** and advances the record after, and
`DriverLoop` exposes its `ServiceClient` for it, the same nullable-until-ready shape `Chat` already
had.

**A move with no note still writes one.** Found in the real window: the store keeps the previous
note when a move carries none — deliberately, so a later move cannot erase what an earlier one
recorded — so a session finished at a checkpoint read *reached completed* beside the analysis it
was **parked with**, which says the opposite of what happened. The host now stamps a sentence, and
it carries the fact the state cannot: `completed` normally means the session closed its own quest,
and this one means a person decided it was done.

**Declining needs a reason**, the same rule the quest door holds and for the same reason — asked
for in place, as a second step, because a decline that slipped out on one click would routinely
carry nothing.

**The band, and the one category that is not in it.** Design §4 names three: parked, then
finished-and-unreviewed, then quests nobody can take. The middle one is **not buildable** —
nothing records that anybody looked at anything, so every row would be a guess. It arrives with
SURF6's *viewed* mark, and the band says so in a line rather than leaving a silent gap. The other
two are real: a parked session, and an **open quest addressed to a repository this deployment has
no registration for** — which the publish door prevents at the time and which comes to exist
afterwards, when a receiver retires. No agent will ever pull it, and nothing else said so.

**The band renders nothing when nothing is waiting.** Not an empty card: a band that always says
all-clear is a band people stop reading, and "is anything sitting" is what the tiles beneath it
already answer.

**Two counts, and D55 moved where they go.** Design §4 put both in the sidebar. After D55 the
quantity's home is the status bar (`N session(s)`) and the status's home is the **door into the
thing**, so *how many need you* is a badge on the Work switch — visible from Manage, which is the
point, and the only badge wearing a status hue. Both the badge and the band read `needsAPerson`,
because two answers to "how many need me" disagree the first time either is edited.

**A duplication the real window exposed.** The head rendered the driver's note and the timeline
rendered the same note with its time, one above the other. The head now leaves it to the timeline —
strictly more informative — except for a park, where the analysis *is* the reason the person is
looking; there the timeline drops the note the card is showing.

**Proven.** 277 web unit tests, up from 245, and 46 in the desktop modules, up from 40 — including
the three refusals asserted from the host side (a move that is not the person's, named with the
state it refused; a decline with nothing in it; a move before the service answers). Two new refusal
codes, both translated in both catalogues, both held by the catalogue test that exists because a
refusal with no translation renders as a bare identifier. And a real-window pass: a seeded parked
session cleared from the surface, the record moving to `completed` with the stamped note, the
status bar dropping to zero, and the toast naming what happened.

**A dev-loop wrinkle worth knowing.** `npm run desktop -- build` fails while the shell is running —
the window holds `Daoris.Desktop.Modules.dll` open. `kill`, then `build`, then `run`.

## SURF10 — the desktop application's own design (2026-09-21)

> 🔴 **SURF10 — the desktop application's own design** (owner, 2026-09-21, after SURF5a: *"I still
> dont see good design for the desktop app itself"*). **The next thing to do, and it comes before
> SURF5b, SURF6 and SURF7** — SURF7 is a piece of the answer, not the whole of it. Every region
> SURF4–5 built works; what is missing is the *application* they sit in. The brief is
> **`docs/archive/2026-09-21-desktop-design-brief.md`** … **Take it as a direction and confirm the reading
> with the owner before building**, the way D53 and ARCH1 were. Deliverable: a reference pass on the
> *window*, a decision recorded before the build, and a real-window loop.

✅ done 2026-09-21 — the contract is `docs/2026-09-21-desktop-frame-design.md`; the decision is
**D56**, recorded before any code, after the owner confirmed the reading against three stated forks
(the left column → an activity bar in both frames; density → one denser scale app-wide; scope →
design plus the page-side frame, with SURF7 after).

**The diagnosis was measured, not eyeballed, and that is what made it a decision someone could
disagree with.** `run` → `shot` → `eval` against the real window, which is the only instrument that
can see any of this. On 1267 × 765 CSS px: Manage's nav held **240px of content in a 738px column**,
`StartSession` was a **permanent 287 × 200 form**, and the attended session — the organising object
of the whole application (D55) — read in a **365px scroll box**. **42% of the width was navigation
and a form; the session got about 28% of the window.** The brief was written from screenshots and had
named the empty column and the permanent form; it did not have the 365px, the 42%, the doubled verbs
or the unremembered selection. A capture shows what is wrong and `eval` says by how much.

**Two defects nobody had recorded.** The same three verbs rendered **twice, 400px apart** — the
attention band's *finish it · decline… · stop it* and the composer's *send · finish · stop*, both on
screen at once for a parked session. And **the mode survived a restart while the selection did not**,
so relaunching into Work landed on *Nothing attended* with a session sitting parked, which is the one
arrangement SURF5a's whole attention half exists to prevent.

**What shipped.** A 36px **app strip** (wordmark, mode switch, workspace scope, and the caption room
SURF7 fills — reserved now so the strip's contents do not shift when it arrives). A 48px **activity
bar**, identical in both frames, replacing the 15rem labelled sidebar in *both* rather than hiding it
in one; its foot holds the actions and the state it used to carry went to the status bar, where the
tier now lives — D24's *stated on every screen* is better served by a bar that is on every screen by
construction. The rail's permanent form went behind a **`＋`** into D41's drawer. The verbs got **one
owner at a time**, decided by state. The attended selection is remembered beside the mode.

**Afterwards, same instrument, same window:** navigation 527px → **288px (42% → 23%)**; the attended
session 739 × 365 → **979 × 709, from ~28% of the window to ~72%**; body type 15.2px → 13px.

**The type scale became tokens, which was not in the plan and should have been.** The sweep found the
literal form had already drifted with nothing reporting it: **fifteen** distinct `text-[…rem]` values
across 203 sites where D41 named eight, four of them (0.7, 0.78, 0.82, 0.85) in no scale at all. All
203 now name one of **seven steps**, and `tokens.test.ts` fails on a raw size anywhere in the
platform — sabotaged in three shapes (`rem`, `px`, `em`) and watched failing against a real file in
`work/`, because the glob reaching down into the subdirectory is the half that fails silently.

**One number the design did not predict, recorded rather than fixed.** The attended column gained the
whole width and **no height** — its scroll region is 341px against 365, because the strip took 36px
and the output panel still holds 236. Its *content* fell from 442px to 371px on density alone, so it
now nearly fits where it used to scroll by a fifth. The vertical budget is **SURF6's to relieve**:
the right dock takes the timeline out of this column, which is what the components plan specified all
along. Raising it here would have meant changing the panel's default height — a behaviour this
landing said it would leave alone.

**Proven.** 303 web unit tests (285 → 303: 11 for the chrome molecules, 4 for the scale, 3 for the
drawer and the verb rule), 11 Playwright over the shipped bundle — including *a browser has one
frame, and it is Manage* and the workspace scope — `npm run verify` green with the canon untouched,
and three real-window passes. The verb rule was sabotaged and watched fail before being believed.

**A regression the gate caught that no unit test could**, in `docs/FIX-LOG.md`: moving the workspace
scope into the app strip made its **tooltip** open downward over its own options list and swallow
every click, because Radix gives the popper wrapper pointer events for hoverable content. Fixed once
in `Tip` with `disableHoverableContent`. `pointer-events-none` on the content was tried first and did
nothing — the class lands on the content and the wrapper is a different element. The trap:
**moving a control changes which way its popup opens**, and the defect lives in the geometry of the
assembled window, which is exactly what Playwright over the real bundle is for.

**Known interim, recorded so it is not read as a regression:** until SURF7 lands the window shows the
OS title bar **and** the app strip — two bars. That was the owner's call with the cost stated.

## SURF7 — the window is part of the frame (2026-09-22)

> 🔴 **SURF7 — the window is part of the frame. Next.** (D55 §a has the traps and the reasons; **D56
> and `docs/2026-09-21-desktop-frame-design.md` §5 have the design**, and SURF10 built everything
> page-side.) `MainForm` becomes an `OptimizedForm` with `FramelessChrome`. **The strip already
> exists** … this item fills that room and makes the strip draggable. Map `WindowCommandModule`
> **late, from where the window is created**, and wire `SET_THEME` and `SET_CAPTION_BUTTONS`.
> 🔴 Read `IAppMaximizable`, never `Form.WindowState` — verify the existing `WindowStateHostOptions`
> stack does, rather than assume it. **Until this lands the window wears two bars.**

✅ done 2026-09-22 — the OS title bar is gone and the app strip **is** the title bar. `MainForm` is an
`OptimizedForm` with `FramelessChrome`; `WindowCommandModule` is mapped late from the form's own
constructor; the strip drags the window, double-click maximizes, a 4px sliver above it resizes from
the top, and the room SURF10 reserved is handed to the OS as real caption buttons.

**The design changed once while building, and D56 carries the amendment: the WINDOW paints the
caption buttons.** D56 had said the page would draw them and report their rectangles. Reading the
framework first showed the cost: **claiming the hit-test makes Windows treat those rectangles as
non-client**, so the page stops receiving every mouse event in them — CSS `:hover` never fires,
clicks never reach React, and hover state has to arrive over a channel that exists for no other
reason. `NativeCaptionButtons` inverts it — the window cuts the rectangles out of the WebView2 and
paints there — and **the reservation SURF10 already built is exactly what that needs**, so nothing in
the strip moved. The colours stay Daoris's (`CaptionButtonColors` from D41's tokens), which keeps
"structure may come from a reference, identity may not".

**The red trap was verified, not assumed.** D55 §a said to check that the existing
`WindowStateHostOptions` stack reads `IAppMaximizable`; the shipped 0.16.0 XML says
`WindowStateManager` prefers it over the WinForms properties, so `Program.cs` needed no change. Then
the failure mode itself was driven end to end on the real window: maximize → **1920×1152** (the work
area, no edge gap), restore → **1268×794 exactly**; the persisted state after closing maximized is
`Placement: 1` with the **windowed** geometry `1280×800` — not the work-area rect, which is what would
have made restore a permanent no-op; and after a relaunch the window came back maximized and restore
still returned 1268×794.

**Two colour values now live in two places, and a test holds them together.** The DWM border, the form
fill and the caption buttons are painted natively and cannot read a stylesheet, so `ChromePalette`
copies five of D41's tokens. That is the duplicated-theme shape this family refuses elsewhere
(frontend architecture §2), so it is **not** left to a "keep in sync" comment: the palette lives in
`Daoris.Desktop.Modules` — plain `net10.0`, so it is testable at all, unlike the window — and
`ChromePaletteTests` parses `tokens.css` and fails when either side moves. Watched failing on a
one-digit change, and it asserts its own parser is still reading rather than silently matching
nothing.

**What the two-bar interim bought back:** the window went from 1267×765 to **1268×794** — exactly the
29px title bar that is gone — and the attended column that SURF10 left scrolling in a 341px box now
fits its content with no scrollbar at all. That was the one number SURF10 measured and deliberately
did not fix.

**The dev loop grew the only instrument that can see this.** The native chrome is invisible to every
existing check: the page cannot observe what it does not paint, and a CSS assertion says nothing about
a DWM border. `npm run desktop -- shot --theme <light|dark>` emulates the media query, lets the page
push `SET_THEME`, and photographs the result. 🔴 **The emulation is scoped to the CDP session and
reverts when it closes** — the first probe set it, disconnected, then captured, and reported dark
while photographing a light window. The capture now happens with the connection still open, and the
tool says why.

**Proven.** 312 web unit tests (the caption geometry, the drag-target guard sabotaged and watched
failing, the browser case), **12** Playwright, 51 desktop modules (up from 46), 152 driver, `npm run
verify` green — and the real window in both themes, with maximize, restore, persistence and relaunch
all driven rather than reasoned about.

**And then the browser half was opened, which nobody had done.** Playwright asserts the disclosure
boundary but nothing had *looked* at it, and looking found a regression SURF10 had introduced:
`App.tsx` still carried `max-md:flex-col`, written for the 15rem sidebar that answered it with
`max-md:flex-row`. The 48px activity bar that replaced it answers nothing, so under 768px the rail
became a **48×276 column above the content** and the page got **105px**. Fixed — one layout at every
width — with a Playwright case at a 680px viewport, sabotaged and watched fail.
`docs/FIX-LOG.md` has the trap: **a responsive rule is half a pair**, and retiring the other half
leaves it pointing at nothing. The boundary itself held exactly as designed: no caption slots, no
mode switch, no Machine domain, no resize strip, no `remote` segment, and `driver · none here`.

## SURF6a — review: seeing what a session landed (2026-09-22)

> **SURF6 — review: the diff** (design §5, D52; same method — `DiffFileRow` as a molecule with every
> file state in its story, `DiffPane` over it). The session's landed work as a diff, computed by git
> where the tree is and carried over the bridge — desktop-only for the console's reason (D47 §4),
> measured from the `HEAD` the driver already records, **bounded and saying what it truncated**. …
> **D55 reshapes the surface**: a **multibuffer** … with **accept**, or **send it back as a quest**.

✅ **read-only half done 2026-09-22.** A person can now see what a session did. The two ACTS — accept
into the canonical line, discard the tree, send it back as a quest — are **not** in this landing and
remain as SURF6b; the split is along the risk boundary, and the reason is below.

**The range was not a fact, and the design assumed it was.** Design §5 says "the driver already
records `HEAD` before spawning, so the range is a fact rather than a guess". It recorded it into a
local variable and spent it on the evidence *string*; nothing persisted it. So the first work was
making the sentence true: a `base_commit` column on the session store (additive, like workspace, kind,
harness_version, profile and tree before it), written once at spawn through **both** doors — the
driven one and the conversation one. Two places would have dropped it silently: the quest-relaxation
rebuild lists its columns by hand, and the remote feed is an allowlist. The rebuild was updated; the
allowlist was deliberately **not**, so the base stays on the machine that can use it.

**🔴 git walks UP, and it nearly showed the wrong repository's work.** `DiffAsync` pointed at a
directory that is not a repository returns a clean exit code and the ENCLOSING repository's diff. The
desktop's example family are plain directories under this repository's `_fixtures/`, so a review of an
`engine` session showed **Daoris's own last commit** as what that session did. Caught by a test in the
same hour, guarded by confirming `rev-parse --show-toplevel` names the path being diffed — which also
rejects a subdirectory, where git would have silently narrowed the diff instead. `docs/FIX-LOG.md` has
it; the general form is that **a process which searches upward has no failure mode visible in its exit
code**, and the damage is the `reaching-in` one in read-only disguise: attributing work to whoever did
not do it.

**Committed work only, and the docstring says why.** The range is `before..HEAD`, exactly what the
evidence string counts, so the two can never disagree. Uncommitted changes are deliberately absent: a
chat may open on a dirty tree (D49 §3), so what is uncommitted is not knowably the session's.

**The bound is the host's and it is stated** (design §5): whole patches are dropped rather than one
cut mid-hunk, the **file list survives intact** so a person always learns that a file changed, and the
sentence names where the rest is. A binary file reports `null` counts rather than zero — "not counted"
and "counted nothing" are different answers, and the row says *binary*.

**The right dock exists now, because it finally has a second occupant** (components plan §3a). The
timeline moved into it, which is what gives the attended column back the height SURF10 measured and
SURF7 could not fix. Below `lg` the dock is not rendered and the timeline stays in the column, so
nothing is unreachable on a laptop.

**Unreviewable is INFORMATION, not a fault** (D48 §6's class), and there are three different facts:
no tree on this machine (a record that travelled here), no base recorded (a session older than the
column), and git could not read the range (the tree moved or was discarded). One refusal code,
`SESSION_NOT_REVIEWABLE`, with the sentence saying which — plus its entry in **both** catalogues,
which the refusal-catalogue test demanded before it would go green.

**Why the acts are not here.** They are the destructive half and `reaching-in` governs them: *never
revert a file you do not own*, *treat "it was clean when I looked" as expired*, *assume concurrency*,
and *a tool that enforces a rule is the most likely thing to break it*. Merging into a canonical line
and discarding a tree deserve their own landing with those guards written first, not the tail of one
that was already large. The read-only half is complete and useful on its own: the person can see the
work, which is what D37 puts them at.

**Proven.** 334 web unit tests (up from 312: the row's every state, the dock, the review over a mocked
bridge, the three refusals), 12 Playwright including *a browser sees no Review tab and no dock*, 259
service, **157 driver** (up from 152 — five against real git, because a mock agreeing with a guess
about `--name-status` proves only that the guess is self-consistent), 53 desktop modules, `npm run
verify` green, and **173/173 family rehearsal** — which matters here because the schema moved.
Seen in the real window: the dock, the tabs, and the information refusal rendering verbatim.

## SURF6b — review: the two acts (2026-09-22)

> 🔴 **SURF6b — review: the two acts.** … **Accept** — merge the session's tree into the canonical
> line: local and reversible, and a press because that is where D37's verification lands. **Discard
> the tree** — destructive: it confirms, names what would be lost, and refuses where work would
> vanish unasked. **Send it back as a quest** — the one move Daoris has that an editor does not.
> 🔴 **Read `.claude/knowledge/reaching-in.md` before writing a line of it.**

✅ done 2026-09-22. The review can now be acted on, and SURF6 is closed.

**`SessionTrees.MergeAsync` is the one place Daoris writes into a checkout it did not create**, so
every guard `reaching-in` names is in it and each refuses rather than repairing: the tree must be
under the trees home (a checkout is never ours to merge *from*); the **root must be clean**, because
somebody's work in flight is exactly what that document was written from; the root must already be
**on** the canonical line, because switching a branch in a checkout we do not own is the same trespass
smaller; the session tree itself must be clean, or a merge would say work moved while leaving it
behind; and every check runs **immediately before** the merge in one call, because "it was clean when
I looked" expires the moment you look away. `--no-ff --no-edit`, so what lands is one commit a person
can read and revert as a unit. **A conflict aborts and reports**, leaving the checkout exactly as it
was — nothing here resolves anything.

**Eight tests against real git, and most of them are about the merge NOT happening.** Each asserts the
refusal left the checkout untouched, because a guard that refuses after doing half the work is not a
guard. The dirty-root one was sabotaged and watched fail.

**Discard reuses `RemoveAsync`, which already implemented D51 rule 7** — its refusal even named
SURF6's surface as where merging happens, written before this existed. The surface's contribution is
asking **twice**: the first press is unforced, which is what *produces* the sentence naming what would
be lost, and only then is the destructive press offered. **A test asserts no `force` on a first
press**, sabotaged and watched fail — it is the one guarding against real destruction.

**Send it back is a door, not a second publish path.** It switches to the platform's own quest
composer with the repository pre-filled; the person writes the ask and the reason, because that is the
part that has to travel (`repository-owns-its-work`). Never keep/reject per hunk: the session already
committed, and reaching in to fix what you are reviewing is what D32 forbids.

**Two things the real window changed.** A session that landed **nothing** still has a tree holding a
slot, so the acts had to survive the empty state rather than returning before it. And the acts are
gated on the **tree the record names**, not on the diff succeeding — a record that travelled from
another machine names no tree here, so offering to merge or discard one would be offering something
that can only ever refuse, and one of those is destructive.

**Proven.** 341 web unit (up from 334), 12 Playwright, 259 service, **165 driver** (up from 157 —
eight against real git), 53 modules, `npm run verify` green, 173/173 family rehearsal.

## SURF9 — a command palette (2026-09-22)

> **SURF9 — a command palette.** The only affordance that scales past roughly seven top-level
> domains, and Daoris is about to have Manage's five plus Work plus Review. It is much cheaper before
> the count grows than after, because the expensive half is the *discipline* — every action
> addressable by name — not the widget.

✅ done 2026-09-22. Ctrl/Cmd+K, and a door in the app strip because a shortcut nobody is told about is
a shortcut nobody uses.

**It pays a debt SURF10 incurred.** Retiring the labelled 15rem sidebar for a 48px icon rail cost
discoverability, and that landing named the palette as where it would be paid back. This is the
payment.

**The expensive half was built as a pure function, which is what made it cheap.** `commands(world)`
takes what is true — is a shell here, which frame, what to run — and returns the list. So "what can I
do right now" is a **value a test asserts** rather than a screen somebody has to arrange: a browser's
list, a shell's list in Manage and the same in Work are three arguments, not three fixtures. The
widget is handed the result and knows nothing about where it came from.

**The disclosure boundary is enforced by OMISSION** (D47 §4), and that is the rule this surface makes
sharpest: *a palette is a promise that what it lists can be done*, so a shell-only action listed in a
browser would be the palette lying, and present-but-disabled would be the same lie with extra steps.
Asserted twice — in the registry's own tests, and by Playwright against the shipped bundle, because
the registry being right does not prove the omission reached the page.

**Matching is by subsequence, ranked.** "cnv" finds *Convergence* and "sas" finds *Start a session*;
a match at a word boundary outranks one buried mid-word, so "se" offers *Search* before *Convergence*.
Hidden keywords carry the words people will actually type — "settings" finds *Machine*, "diff" finds
*Review* — and 中文 matches, because the console is bilingual and so is this.

**🔴 It found a claim that had been false since it was written.** D41 §6 says drawers are
`role="dialog"` **with `aria-modal`**; Radix writes the role and traps focus and **never writes that
attribute at all** — `grep -c aria-modal` in the installed package returns 0. So the sentence
described the design and not the page, for as long as it had existed, with every gate green, because
no gate reads prose. Both modal surfaces now set it explicitly and `ui.test.tsx` asserts every one of
them, sabotaged and watched fail. The general form is `claims-need-checks` exactly: the claim and its
enforcement were written at different moments and only the claim was easy.

**Two smaller things the building settled.** The selection is clamped to the filtered list, or typing
past the end of a shorter list and pressing Enter would run a stale index — tested directly. And the
palette closes **before** it runs, so a command that opens a drawer or switches frames never has to
think about the palette still being over it.

**Proven.** 369 web unit tests (up from 341: twelve for the registry, ten for the widget, four
stories, two for the modal claim), **13 Playwright** including the browser's shortened list and the
keyboard path, 259 service, 165 driver, 53 modules, `npm run verify` green — and the real window,
where Ctrl+K opens it and the shell's list carries eleven commands including the two Work actions.

## POLISH1 — a screenshot-driven pass over the console (2026-09-22)

> **"we also need to keep polish the ui/ux you can use screenshot tool to confirm and keep going"**
> — owner, 2026-09-22. A standing direction rather than a backlog item: the surface is looked at, not
> reasoned about.

✅ first pass done 2026-09-22. Five changes, every one found by opening the real window and reading
it rather than by inspecting code. `docs/2026-09-19-platform-ux.md` §4 carries them as an amendment,
because each is a rule and not a one-off.

**Prose had no measure.** D41 §2 caps the content *column* at 72rem, which is right for cards and
rows — and every explanatory paragraph inherited it, running to about **190 characters a line** at
the D56 scale against the 45–75 the eye tracks. The Machine view was a wall of them. `Prose` (65ch,
font-relative so it survives the next scale change) now carries the console's own explanations. It is
deliberately *not* applied to content: a quest's body, a repository summary and a search excerpt are
what the family wrote, and content is shown as it is.

**The dark theme had invisible form fields**, and the light theme hid it completely. Inputs were
`--line` on `--raised` inside a `--raised` card; in dark that is very nearly no border at all. All 18
field declarations now use `--line-strong`, which gives the rule: **a field is outlined with
`line-strong`, a container with `line`** — an input has to read as somewhere you can type.

**A form was sized to its container.** Three equal thirds of a 72rem card gave a 570px box to the word
"default". The wire form is now proportioned to what each field holds and capped.

**Every card carried 24px of dead space above its own heading.** `SectionTitle` applied `mt-6`
unconditionally, including when it *was* the first thing in the card — which it usually is. `first:mt-0`.

**Quests contradicted D41 §5.** That section specifies `pill · title · route · how long`, and Overview
follows it; Quests had pushed the status pill to the far edge, about a thousand pixels from the title
it described, so the eye had to cross the whole card to connect them. Status leads now; the secondary
marks (sat-a-week, live session) stay right, because they are exceptions rather than identity.

**The method is the point.** Four of these five are invisible in the source — they are properties of
the assembled page at a real width, in a real theme. The same class as the two defects the previous
landings found by looking (the stacked icon rail, the tooltip swallowing clicks). `npm run desktop --
shot [--theme dark]` is the instrument, and it earned its keep again.

**Proven.** 369 web unit, 13 Playwright, `npm run verify` green, and before/after captures of
Overview, Quests and Machine in both themes.

## SURF8 — the monitor window (2026-09-22)

> **SURF8 — the monitor window** (D55 §b). `SecondaryWindows`: `monitor` (rail plus live streams,
> read-only, for a second screen) and `session:<id>` (one attended session, detached) — routes into
> the same bundle, so the components are SURF4b/4c's unchanged, and the native frame stays. The thing
> to prove is **a second reader on the console pump**: SES1's bounded per-session buffer was written
> for one.

✅ done 2026-09-22. Both windows exist, both are reachable by name, and the claim they rest on is
asserted rather than assumed.

**A secondary window is a ROUTE, not a second frontend.** `?window=monitor` / `?window=session:<id>`
into the same bundle the main window and a browser are served — which is the whole reason the monitor
cost the rail, the tile and the console rather than a second application to keep in step. One name is
read by three things — the page parses it out of its own URL, the shell navigates to the address made
from it, and the geometry store writes the file named after it — so the derivations live in one place
(`SecondaryWindow` in Modules, `work/window.ts` in the page) and both are tested against the same
strings. The escape does both jobs: `Uri.EscapeDataString` is injective over the alphabet a session id
may use, so two windows can never share a geometry file. A `-` substitution would have collided
`session:laptop/a1b2` with `session:laptop-a1b2`.

**The name is refused, never sanitised.** It becomes an address AND a filename, so `session:../../x`
is not "nearly a window name" — it is not one, and `WINDOW_UNKNOWN` says so in both catalogues. The
refusal-catalogue test caught the missing translations on the first run, which is the three-part
refusal rule (REV2) doing exactly its job.

**Read-only, in both windows, and that is D56's rule holding ACROSS windows rather than only within
one.** Two windows offering the same three moves on one parked session is precisely the arrangement
"one owner for the verbs at a time" exists to prevent. `SessionHead` already had the state and the
sentence for it — *nothing here can act, so the analysis is shown and the moves are not; half a
control is worse than none* — so the detached window reuses it by passing no `onResolve`. The one act
either window has is opening another window, which is a window command and not a move on a session.
**If the owner wants a detached conversation to be typed into, that is a deliberate reopening of this**
— it would need the composer, the refusals and an answer to who owns the verbs, and it is not a gap.

**The second reader turned out to be already true, and is now held.** `SessionOutput.Tail(id, after)`
keeps **no cursor** — "a reader says what it has not seen", which SES1 wrote down and nothing
asserted — and `Lined` is a multicast event rather than a handler. So a second window is a second
*subscriber*, not a second buffer. Three tests hold it now: two readers at their own positions in one
session, a late reader told the same `Dropped` as an early one, and a second relay taking nothing from
the first. The monitor's own suite holds the other end — two tiles, two consoles, each its own lines.
The reason to assert something already true is that the natural "optimisation" here is to remember a
reader's position, and that would break it silently.

**Absent in a browser, by falling back rather than by hiding.** The route is in the bundle a browser
is served, so the URL is reachable — and both windows are a rail and a live stream, and a stream has
no HTTP route at all (D47 §4). A pasted link therefore lands on the platform, the same fallback the
Work frame makes for a remembered mode it cannot honour. Playwright asserts it, and the palette's two
new commands are absent there like every other shell-only one.

**🔴 Three defects the real window found, none of them visible in the source** — the mechanisms are in
`docs/FIX-LOG.md`. A **thread-affine** WebView2 environment, shared into a window with its own STA
pump, which opened the window and then failed its bring-up. A secondary window with no `SET_THEME`
channel, wearing a light title bar over a dark page. And a silent console rendering as an empty
bordered box, which in a read-only window reads as a field to type in — `SessionConsole` gained a
`quiet` sentence, which is also the more honest shape: *nothing was said* and *the console is showing
you nothing* are different claims.

**The instruments could not see what this landing built, so they were taught to.** `shot` used
`Process.MainWindowHandle`, which answers for exactly one window and lets **Windows** choose which —
with the monitor open, a capture silently photographed whichever the OS called main, and two
consecutive runs returned different windows. `pickPageTarget` had the same assumption written in a
comment: *"the shell has exactly one page"*. Both now take `--window <monitor|session:ID>` — one flag
for both, though one reaches a CDP page and the other an OS window, because a tool needing a URL
parameter for one and a caption for the other is a tool people get wrong. A name that matches nothing
is refused rather than falling back, since the point of asking is that the main window is not the one
wanted. **Considered and not done: promoting any of the three traps to canon.** All three are
framework-specific — a WebView2 environment's thread affinity, a reserved singular IPC module, an
instrument's window handle — and the canon installs into repositories that have none of those. The
general form ("an instrument encodes the shape of what it measures, and answers about something else
rather than failing when that shape changes") is real but narrower than what the core already holds,
and the core has about one rule of headroom left. The fix log is the right home, and it is indexed.

**Two smaller things.** `auto-fit` rather than `auto-fill`, with `grid-auto-rows: minmax(15rem, 1fr)`
— measured on the real window, where `auto-fill` left one session sitting in a sixth of a wide screen
looking like a rendering failure. And the rail's minimum size is stated in `WindowStateOptions`
because those values are **also** applied as the form's `MinimumSize` and outrank anything the form
sets for itself; a utility pinned to a main window's floor cannot be parked narrow beside something
else, which is most of what a second screen is for.

**Proven.** 398 web unit (up from 391: seven for the monitor over a mocked bridge, six for the tile,
seven for the route parser, eight stories), 14 Playwright, 169 driver (three new on the pump), 66
modules (thirteen new), `npm run verify` green — and the real window, where the monitor opens from the
palette, the tile detaches a session into its own window, and both were photographed in light and dark.

## SURF5b — the notification, and the terminal's half (2026-09-22)

> **SURF5b — the notification, and the terminal's half** (design §4). **An OS notification on park and
> on end**, never for an ending the person caused, per machine and off in one click — the shell's own
> code over WinForms, which **closes driver design open question 5**. The terminal's half is
> `daoris-driver` on the machine that holds the sessions, because a headless machine has no screen to
> notify and still needs the answer (D50). The setting is machine-local and the same file a terminal
> can edit.

✅ done 2026-09-22. **This closes driver design open question 5** and finishes the SURF arc.

**A park is SEEN and an end is KNOWN, and that asymmetry is the design.** A park is a state change
nothing local performs — the session asks, through its own connector — so it is found by diffing the
tick's active sessions, which is why `SessionView` finally carries `State`: the planner had never
needed it, because "is this repository busy" is the only question it asks. An end is different: the
driver concludes every session it ran and already knows **whose decision it was**
(`_processes.WasStopRequested`, whose own comment says only that flag knows).

**That is what makes "never for an ending the person caused" structural rather than bookkeeping.** An
end the driver did not conclude is one the person performed *here* — a resolve, an ended chat, a stop
— so it never reaches the watch at all and there is nothing to suppress. The first design had a
`Ignore(id)` list for this; it was deleted once the asymmetry was noticed, because a suppression list
is a thing to keep in step and this is a thing that cannot go wrong.

**The judgement is in the driver library, and only the delivery is per-door.** `AttentionWatch` is
pure and tested; the shell turns an event into a balloon and `daoris-driver` prints it as a line
marked `!`. That is what lets a headless machine answer the same question (D50) — and it is also the
only reason any of this is testable, since a balloon is not.

**Two rules the watch holds that a person would otherwise learn to hate it for.** It says a thing
**once** — a parked session is parked on every tick until somebody answers it, and saying so every
fifteen seconds is how notifications get turned off. And **the first look is a baseline, never a
backlog**: a machine that has just started has no previous view, so a session already parked then is
one the person was told about on an earlier run.

**The sentence is composed once, in the library.** It is the driver's own, rendered verbatim rather
than translated — the boundary D24 already set and the reason `errors.DRIVER_REFUSED` is `{{message}}`.
Re-authoring it in two languages would mean two that drift, and a native balloon has no catalogue to
read from anyway.

**Silence means ON, on both sides.** Every machine that already has a `driver.json` predates this
field, so reading its absence as "off" would ship the feature switched off on exactly the machines
that have been driving longest — where a parked session sitting unnoticed costs most. Asserted in the
driver's parser and in the CLI's reader, which are two artefacts that must agree about one file.

**Two doors, as D50 requires** — `daoris driver notify on|off` and a checkbox in the Machine view,
over the same `driver.json` field. The surface names the other door, so somebody who finds this on a
machine they reach over ssh does not go looking for a second setting.

**The balloon stays quiet while the person is looking**, because the same event reaches the page and
becomes the platform's own toast — two notices for one thing is how the feature earns a reputation.
🔴 **Focused is not enough for that test; it must also not be minimized** — a minimized window can
still report as the foreground window for a moment (measured), and minimized is the most obvious case
of nobody looking, so a foreground test alone would have swallowed the balloon in exactly the
situation the feature exists for.

**A notification is a door** (design §4: every attention row is one). Clicking the balloon brings the
window forward *on that session* rather than on whatever was last open — the shell asks over the
bridge, because which session is attended is the page's state to hold. The activation sequence is
written out rather than reached for: `WindowActivation` is internal to the framework, and its
documentation names the order everyone gets wrong (un-minimize **before** activating, then
`SetForegroundWindow`).

**Daoris now has a tray icon**, because `ShowBalloonTip` needs a visible `NotifyIcon` and the
framework's `TrayIcon` carries a menu and no balloon. It is **not** close-to-tray: this window still
closes when it is closed, and changing that would be a lifecycle change nobody asked for. D55 §b
sanctioned exactly this — it rejected a tray-*only* monitor while noting the icon was "worth keeping
for SURF5's notifications, where the question really is the first one".

**It found a regression from the previous landing** (in `docs/FIX-LOG.md`): `desktop kill` closed
whichever window Windows called main, which since SURF8 could be the monitor — so the shell was
force-killed before it could stop the HTTP host it owns, and seven orphaned hosts eventually failed a
build. Third disguise of one fact, and the first one that reported success while failing.

**A conversation's end is deliberately not reported, and a conversation that PARKS is.** Parks come
from the tick and are blind to how a session was started; endings come from the driver's own
conclusion, and `ChatRunner` concludes outside the tick. The intent matches the mechanism rather than
merely permitting it: this exists because nobody should have to watch an *unattended* session, and a
conversation is something the person is in, whose ending appears in the stream they are looking at.
Written down on `AttentionWatch` so the next reader can tell the decision from the accident — and if
real use shows a chat left running and crashing silently, feeding its `onEnded` in is the fix, since
the repository is already remembered there.

**What was not visually confirmed, and why.** The balloon's own rendering. Verifying it needs a
capture of the whole screen rather than of a window, and the one I took showed the owner's unrelated
work — so it was deleted and not retaken. What *is* proven: the driver emits the event with the right
kind, session and sentence (read off the live bridge on the real machine), the page raises its toast
from it, and the suppression rule is the code above. The balloon call itself is five lines and is
stated here as unverified rather than claimed.

**Proven.** 216 CLI (five new on the notify verb), 184 driver (fifteen new: twelve for the watch,
three for the setting), 68 modules (two new), 404 web unit (six new), 14 Playwright, 259 service,
`npm run verify`, `npm run test:web` and `rehearse:family` (173/173) all green — and the real window,
where a park driven through the service reached the page as a toast within one tick.

## TOOL1 — the toolchain and its accounts, designed (2026-09-22)

> **"I still cannot see a proper credential management since we need this for both claude/codex, and
> other llm if possible (this is kind more from deepseek harness) and also we need to be able managed
> multiple account with usage management (for example multiple claude accounts) and currently we
> still don't have managed cli (still reading from the machine)."** — owner, 2026-09-22.

✅ designed 2026-09-22 → `docs/2026-09-22-toolchain-design.md`, accepted as **D57**. Build items
TOOL2–TOOL5 are in the backlog.

**It began by measuring, and the measurement corrected doctrine.** CLAUDE.md said *"the toolchain is
Daoris's"*. What is actually Daoris's is the **accounts**: `~/.daoris/harnesses/<harness>/<profile>/`
is a real per-account configuration home wired through `CLAUDE_CONFIG_DIR` / `CODEX_HOME`, with a
machine default, a per-workspace default and a probed login state — multiple Claude accounts already
work. The **binary** is the machine's: `install` runs `npm install -g` into the global prefix and
`binary: ['claude']` resolves off `PATH`. The owner's "still reading from the machine" was exactly
right, and the overclaim is what let it hide — `claims-need-checks` in its usual shape, where the
claim and the mechanism were written at different moments and only the claim was easy. Corrected in
CLAUDE.md before anything was proposed.

**The one signal already arriving and being thrown away.** ACP's `usage_update` carries context
used/size per turn, and `Acp.Render` was flattening it into a transcript line. That is the whole
measurement source, and finding it is what made "measurement first" cheap enough to offer.

**The reference was read before deciding, and its names mislead.** `deepseek-harness`'s `credentials/`
group **stores secrets itself** — configuration names a `CredentialRef`, a private local store holds
the value — which is the opposite of D49 §4. Its `llm/token-meter` measures **per-session context
pressure and message price** by replaying the session log. Neither package answers "which of my three
Claude accounts is spent", which is the thing the owner actually asked for; saying so was more useful
than adopting the shape by name.

**Two questions were put to the owner rather than guessed, because each changed the work.** *What is
usage?* — two features hide under the phrase, per-session cost (measurable, already arriving) and
per-account quota (not readable without a credential). Answer: **both, measurement first**, so the
rotation rule gets written against observed behaviour. *How far does breadth go?* — a provider
registry was rejected by the components plan, and reversing it is the owner's call. Answer: **"more
native support (so its more match its api and interfaces) also with ACP door"**, which is D23 applied
more times rather than a registry — so the rejection stands and D24 stands with it.

**The outcome is that nothing had to be reopened.** Both candidate reversals — D49 §4 (never see a
credential) and D24/the registry rejection — survive, because measurement needs no credential and
native adapters need no catalogue. That was not the expected answer when the gap was first read, and
it is the reason the arc is cheap.

**One boundary inherited rather than invented.** Per-account usage names a profile, and a profile name
is already served only over loopback (`ToSession`: `Profile: loopback ? s.Profile : null`). So usage
is machine-local by the same rule as the transcript, the tree path and the profile name — no new
disclosure argument, which is what kept §4 short.

**Rotation is designed and deliberately not built**, with the three questions measurement must answer
first written down: what exhaustion looks like in a harness's own output, how long a cool-off is, and
whether a rotated session stays reproducible. A string match on somebody else's error text is the
fragile part, and guessing it is how it gets written wrong.

## TOOL2 — the managed CLI (2026-09-22)

> **TOOL2 — the managed CLI** (design §3). A managed harness under
> `~/.daoris/toolchain/<harness>/<version>/`, installed by the harness's own installer aimed there
> rather than at the machine. Selection at spawn is **explicit command → managed pin → `PATH`**, and
> the pin resolves exactly as a credential profile does. **Absent means `PATH`, which is today's
> behaviour byte for byte.**

✅ done 2026-09-22. `daoris harness pin <harness> <version> [--workspace W]` and `unpin`, the Machine
view's half, and the resolution that makes a pin decide what a session actually spawns.

**It is the fourth rule of a twin contract that already had three.** The CLI and the driver share no
code — the file and the layout are the contract — so profiles were already asserted in both artefacts
as three numbered rules. The pin is the fourth, written the same way in both: *the binary is the
explicit command, then the managed pin, then `PATH`*. Reusing the shape meant reusing its safety
properties too, including the directory-name refusal (a version becomes a directory, so `../escape`
is refused rather than normalised) and the resolution order (pick → workspace → machine → none).

🔴 **Absent means `PATH`, and that is the rule that must never regress** — the exact twin of "no
profile means the harness's own configuration home". A machine that installed `claude` itself, and a
contributor who never ran Daoris, both keep working (D48 §2a). Asserted in both artefacts, in the
words of the rule, so a later change that made absence mean an empty managed directory fails a test
rather than a person's machine.

🔴 **A pin nobody installed REFUSES the spawn rather than falling back.** Quietly running `PATH`
would run a different tool than the one the person asked for, report success, and record the pinned
version beside work the pinned version did not do. The refusal names both ways out (`pin` to install
it, `unpin` to go back), which is the shape every refusal in this class already has.

**The explicit command still outranks the pin.** A `commands` entry in `driver.json` is a person
naming exactly what to run; a standing pin quietly replacing it would be a surface overruling a
specific instruction. Tested directly, because the precedence is three-deep and invisible.

**One line governs both doors.** The pin is applied in `HarnessProbe.Apply` — the same place the
credential profile's environment seam is — rather than inside each adapter's `Prepare`. An adapter
that forgot it would spawn the wrong binary *and* record the pinned version beside it, which is a
worse failure than not supporting pins at all.

**The write half was the dangerous half, and a test was written for it first.** `HarnessSettings.Save`
in the driver wrote only `defaults` and `workspaces`. Both artefacts write this one file, so a save
that knew nothing about `versions` would have **silently deleted** a pin the CLI put there — it
compiles, it passes every profile test, and it loses data. That is counterpart-set rot in its purest
form, and the round-trip test names it.

**Two defects the real window found, both invisible in the source.** The `stub` adapter was offered a
*pin it* button although it declares no package, so pressing it could only produce a refusal — half a
control, which the roster now answers (`pinnable`) so the control is **absent** instead. And the
rationale for pinning was repeated beside every harness row; it is stated once in the card's body
now, which is the "rationale beside each sibling" smell caught by looking rather than by reading.

**`npm run verify` earned its ordering again.** `node --test` strips types, so the existing
profile-resolution tests — which build a settings literal — only failed at `tsc`. Typecheck-first is
why that surfaced before `npm pack` rather than after.

**Proven.** 227 CLI (twelve new: the paths, the resolution, the verbs, and what `list` says about a
pin that is not installed), 195 driver (ten new, including the round trip that would have caught the
deleted pin), 68 modules, 410 web unit (six new), 14 Playwright, 259 service, `npm run verify`,
`test:web` and `rehearse:family` (173/173) green — and the real window, in both themes, where
`claude-code` offers the control and the harness that cannot be pinned does not.

## TOOL3 — measurement (2026-09-22)

> **TOOL3 — measurement** (design §4). Parse ACP's `usage_update` structurally instead of rendering
> it to a line; record per session and aggregate per account. 🔴 **Machine-local, inheriting an
> existing boundary rather than inventing one.** A pipe-door session records nothing and **says so
> rather than showing a zero**. No model named, no price claimed.

✅ done 2026-09-22. The owner's "measurement first" is built: what each account has carried, from
numbers the harness itself reported.

**The source was already arriving and being thrown away.** `Acp.Render` turned `usage_update` into
`context 1200/200000` and dropped the structure. That line was the whole reason "measurement first"
was cheap enough to offer, and finding it is what made the ordering honest rather than a delay.

🔴 **The high-water mark, not the last reading.** Context drops when a session compacts, so the final
number would report a session that nearly filled its window as having used very little — exactly
backwards for the person deciding whether to split work. Held within a turn by the wire and across
turns by the store, with the same rule stated in both.

**Absent is not zero, in three places.** An agent that reports nothing leaves `Usage` null; a
pipe-door session is recorded not at all; a machine that has measured nothing renders no card
whatsoever. "Nothing was measured" and "it used nothing" are different claims, and only one of them
is ever true here — SES1's rule about a stated bound, applied to a different number.

🔴 **Machine-local by an inherited rule, which is the point.** Per-account usage names a credential
profile, and `ToSession` already serves a profile name only over loopback. So usage joined the
transcript, the tree path and the profile name behind the shell's bridge without a new disclosure
argument — re-deciding that boundary per field is how a boundary erodes, so it was not re-decided.

**No price, and the test says so.** Daoris does not know what a token costs (D24), so the surface
counts what the harness reported and stops. A vitest case asserts no currency symbol renders, because
"we will never add a price table" is the kind of intention that quietly stops being true.

**A bug the wiring exposed.** `CaptureAcpAsync` returns `Task<AcpOutcome?>` and was being assigned to
a bare `Task` — which compiles and **silently discards the outcome**. It had cost nothing until now
because nothing downstream read it; the moment measurement needed it, the same line would have
returned null forever with every test green. Held as its own type now, with the reason beside it.

**A trap worth inheriting, found in a test.** A lambda parameter named `_` is **in scope**, so
`out _` inside that lambda binds to the parameter rather than to a discard — and the error surfaces
twenty lines away as a type mismatch on an unrelated call. Renamed, with the reason written down.

**One thing found by looking.** The per-account figure rendered as a bare `169,500` in a column,
which says nothing on its own. It names its unit now — and the unit is **context**, not tokens,
because the number is in the harness's own units and calling them tokens would be a claim Daoris
cannot make.

**What this unblocks, and what it does not.** TOOL4 (rotation) can now be written against observed
behaviour rather than a guess — which was the whole reason for this ordering. It stays held: nothing
here yet answers what a harness's exhaustion looks like in its own output, how long a cool-off should
be, or whether a rotated session stays reproducible.

**Proven.** 205 driver (ten new: three on the wire's parse, seven on the store's bounds, high-water
rule and unreadable-file reading), 70 modules (two new on the route), 412 web unit (two new), 227
CLI, 14 Playwright, 259 service, `npm run verify`, `test:web` and `rehearse:family` (173/173) green —
and the real window in dark, where a seeded machine totals two accounts correctly and a machine with
none shows no card at all.

## ACP2 — `claude-code` over the protocol door, built to the edge of a login (2026-09-22)

> **"can you try to develop verify logic and develop it until a state that I can support?"** — owner,
> 2026-09-22, answering that ACP2's closing run needs a login only they can supply.

✅ built and keyless-proven 2026-09-22. **Not closed**: D23's "on proof" is the real driven run, and
that run is the owner's to start. What is left is one command.

**Everything but the login.** The `claude-code-acp` adapter (`Wire = Acp`, its own toolchain entry,
pinned exact), `acceptEdits` as a **mode on the wire** rather than a command-line flag, both §1a seams
reaching a real spawn through the one line that governs both doors, and the pipe door untouched beside
it.

**The posture moved vocabulary, not meaning.** The pipe door passes `--permission-mode acceptEdits`;
this door sets a mode, which is what the evaluation observed Claude Code offering on `session/new`.
🔴 Three rules guard it: a mode the agent does not offer is **not substituted** (guessing a
neighbouring one is how a permission boundary widens without a decision), an agent offering no modes
is left **alone**, and `bypassPermissions` is never asked for although the wire offers it.

**`tools/acp2-proof.mjs` is the deliverable the owner asked for.** Readiness, then the keyless half,
then — behind `--drive` — the real run. **Readiness is not a failure**: a machine that is not set up
yet gets the exact commands that set it up and exit 0. The first version got this wrong and printed
`FAIL` beside "nothing above failed", which is worse than either; readiness items are now `todo` and
never touch the failure count.

**The keyless half runs against the real adapter and passes six checks**: `initialize` answers,
`session/new` succeeds on a tree, `acceptEdits` is offered as a mode, the scratch `CLAUDE_CONFIG_DIR`
receives its own `.claude.json` while the machine's real profile is untouched, and `claude auth
status` still reads logged in afterwards — because `session/prompt` is deliberately never sent. No
model was called and nothing was spent.

**Three real findings, each from actually running it.**

- 🔴 **`daoris harness install` has never worked on Windows** (`docs/FIX-LOG.md`) — `npm` is
  `npm.cmd`, and Node refuses to spawn a `.cmd` without a shell. TOOL2's `pin` inherited it and was
  the first person-action anybody ran. The trap to inherit is general: *a mechanism no gate runs is a
  mechanism nobody has run*, and these three are kept out of the gates deliberately.
- 🔴 **The binary is `claude-agent-acp`, not `claude-code-acp`.** I guessed the binary name from the
  adapter's Daoris name, which is exactly what this module's own documentation warns against — every
  field is a claim about somebody else's program. It was caught by TOOL2's own honesty: `harness
  list` reported a pin that was installed as *NOT INSTALLED*.
- 🔴 **A harness with no login check is not the same as one that cannot be asked.** The declaration
  test refused the new entry, correctly: an unanswerable login question is **permissive** (SES3), so
  a harness that merely omitted the check would quietly widen what may spawn. The adapter now
  **declares** `accountOf: 'claude-code'` — it runs `claude` and reads the home `claude` logged into
  — and the test asserts that a harness either asks the question or names whose account it borrows,
  through the same seam.

**What is left, and it is one command.** `node tools/acp2-proof.mjs --drive` on a machine with the
adapter pinned and an account logged in. It builds a scratch repository, publishes a real quest, runs
one driven tick over the protocol door, and asserts DRV4's shape: the session ran on
`claude-code-acp`, ended completed, the record names the adapter and the harness version, the evidence
carries a commit, and the session closed its own quest through its own connector. That run is D23's
"on proof", and until it passes **the pipe door stays what a machine drives with**.

**Proven.** 227 CLI, 221 driver (sixteen new: three on the mode, eight on the adapter and its seams,
three on the selection, two on the pipe door's untouched spawn), 70 modules, `npm run verify` and
`rehearse:family` (173/173) green — and the keyless proof itself, 6/6 against the real adapter at
0.79.0, installed by Daoris into a directory Daoris owns.

## DRV6 — the strike limit (2026-09-22)

> 🔴 **DRV6 — a driven quest has no strike limit, and each attempt spends a login.** Measured in the
> same run: the session ended without touching its quest, the driver picked the same quest on the next
> tick, and **18 sessions ran on one quest** before it was stopped by hand. `Driver.cs` and `Planner.cs`
> contain no notion of an attempt, a strike or a backoff. For an unattended loop holding a real account
> that is not a rough edge — it is the failure mode that costs money while nobody is watching, and
> SURF5b's notification tells the person only after the loop has already run. A quest that has failed
> *n* times is parked with what each attempt did, and the person restarts it deliberately. Decide *n*
> and whether a **held** tick counts (it must not: a dirty tree is a wait, not a failure).

✅ **done 2026-09-22 — D58.** `StartVerdict.Exhausted`, ahead of busy and capacity because those are
waits and this is a stop. `strikes: 3` in `driver.json`, `0` for the old behaviour; `daoris driver
strikes <n>` and `daoris driver retry <quest>` are the second door (D50), and the parked reason
carries the count and the verb.

**Both doors, all the way through.** `SET_STRIKES` and `RETRY_QUEST` on `DriverModule`, `strikes` and
`forgiven` in what `STATE` reports, the hooks, and a card on *This machine* beside Notifications —
they answer the same worry from opposite ends, one asking to be told when a driver stops and one
bounding what it spends before anyone is told. The module test is the one that earns its place: a
setting added to the terminal and not reported over the bridge compiles perfectly and leaves the
screen unable to show what the machine is doing.

**Three defects the source could not show**, found by looking at the real window (owner, 2026-09-22):
a 550px box holding one digit — the lesson the wiring fields above it had already been re-cut for; a
190-character measure on the terminal note; and `text-status-warn`, which `tokens.test.ts` refused
because D56's scale has seven named steps and `status` is not one of them. The token test caught its
own case before a person did.

**The count is derived, which was the design's whole question.** Not a tally the driver keeps — the
`failed` session records it already writes, counted. So there is no second register to drift, and the
two things that have bitten this family (a `Save` that deleted the harness pin; profiles before it)
cannot happen to it. **Only `failed` counts**: a stand-down is the race resolving as designed, a
decline is a real answer, a stop was the person. The backlog's open question — *does a held tick
count?* — answered **structurally**: a held repository spawns nothing, so there is no record to count,
and a rule nobody has to remember cannot be forgotten.

**A restart is a mark, not a reset.** `retry` records the failure count it was restarted at, so the
next three park it again and the records still read true. Resetting would make the derived count
disagree with what it was derived from.

**Gated with no model** — `rehearse:family` 177/177. The stub grew one branch: a session that dies
**before** taking its quest, which is the shape that loops (after taking, the quest is `Taken` and
stops itself). Four checks: bounded at three, the park names its verb, the quest stays `Open` to
anyone else because parking is this machine's decision about spending rather than a quest state, and
a retried quest runs again and parks again at the same count.

**One asymmetry recorded on purpose:** absent `strikes` means the **default**, where absent `notify`
means **on**. Opposite readings, same reasoning — a `driver.json` predating the field belongs to the
machine that has been driving unattended longest, and that is the machine that needs the protection.

## CANON8a–c — the always-loaded tier moves into `AGENTS.md` (2026-09-22)

The measurement is `docs/2026-09-21-dsh-evaluation.md` §6.5 and the contract is
`docs/2026-09-22-instruction-file-design.md`; **D59** carries the four rejected alternatives.
`.claude/rules/` was read by exactly one of the three harnesses this family drives, so Daoris was
shipping an always-loaded tier whose always-loaded-ness belonged to one agent harness.

✅ **done 2026-09-22.** The tier is a **span** in `AGENTS.md`, with `CLAUDE.md` carrying `@AGENTS.md`
for the one harness that reads the other name and follows imports. This repository and both examples
migrated in the same commit. **Always-loaded core 23,862 → 21,817 bytes**, because eight frontmatter
blocks and a separate roster's preamble went away.

**The descriptor absorbed it**, which is what that seam was built for: `tiers.rules.region` and
`pointer` replace `indexPath`, and a tier is now a directory **or** a span. D7's *the tier is the
directory* becomes **the tier is the location**, and its better half survives — no `tier:` field, and
a region has a byte count exactly as a directory did, so CANON7's budget means the same thing.

**Four things that would have failed silently, and were caught by looking for them:**

- **An edited retirement of a span.** The guard stats a path; a span has none, so a retired rule the
  repository had improved would have been deleted without a word — the fourth bug D19's last row was
  written from, arriving again through the tier moving. Span-aware now, and asserted.
- **Rename detection** went quiet for the same reason: the old text lives in the region, not at a
  path. `previousAt` serves both, and a migrating rule never pairs with itself.
- **The legacy `RULES_INDEX.md`.** Generated files were never in the lock, so no retirement rule
  reaches one — it would have sat in the emptied directory looking authoritative and frozen.
- **`doctor` lost its canonical side.** With the rules in a region, every local rule was compared
  against nothing — and that is exactly the case the move creates, since a repository's own
  `.claude/rules/x.md` is no longer a collision.

**Two limits, stated rather than discovered.** A span carries **prose**: `upstream` replaces the
body and keeps the canon's frontmatter, so improving an `enforces` line is a canon edit. And
`check`'s staleness comparison covers the **on-demand** rows only — the rules rows come from
frontmatter the span strips, so nothing offline can rebuild them; a canon change is `status`'s report.

**A rule can no longer collide**, because the canon no longer claims a path a repository could
already own. D12's refusal still guards every tier that is still files.

## CANON8e — the service stopped indexing the rules tier (2026-09-22)

> 🔴 **CANON8e — the knowledge service stopped indexing the rules tier, and I did not notice.**
> Measured after the migration: `RepositoryScanner.Scan` reads `{target}/rules` as a directory, which
> is now empty, so the eight canonical rules fell out of the index — silently, and every gate stayed
> green because no gate asserts that a canonical RULE is searchable.

✅ **done 2026-09-22.** `DoctrineRegion` is `region.ts`'s C# twin — two artefacts, no shared code, the
**file and the layout as the contract**, exactly as the harness profiles are. `daoris.lock` is what
says where each rule lives: an entry carrying `in` is a span, and the scanner splits the region by
provenance line so **each rule is its own entry**, the same reason a decisions log is split at its
headings. A repository still on files keeps working, and the adopter's text around the region is never
swept in. Measured on the example family: **11 → 19 entries** per repository.

**One correction to the report that opened this.** The evidence sentence — *"searching returns only
that repository's own local rule"* — was misleading. Search defaults to `localOnly`, deliberately:
canonical content is identical in every adopting repository, so returning it per repository would
produce a dozen copies of one rule and call that a corpus. So a **default** search never returned
canonical rules, before the move or after. What actually broke was that the rules stopped being
**indexed at all**, which costs the repository counts, every `localOnly=false` search, and anything
reading the corpus whole. Smaller than first stated, and still a silent regression.

**The gate that would have caught it** is two checks in the family rehearsal (179/179): a canonical
rule is searchable, and it is found in the file that actually holds it. A count alone would not do —
an entry indexed under the wrong kind, or with an empty body, passes a count and fails a reader.

🔴 **The lesson is the shape rather than the bug.** D59 moved a tier and I traced every consumer
inside `Daoris.Cli`, because that is where the type errors were. The service reads the same layout
from another language, where **nothing breaks at compile time** — which is precisely what a twin
contract is. Anything that moves a layout now asks: which other artefact reads this, and in what
language?

## ACP4 — the MCP servers the door hands over (2026-09-22)

> 🔴 **ACP4 — the MCP servers the door hands over. This BLOCKS ACP2, and the driven run measured it.**
> `Acp.cs:113` sent `mcpServers = Array.Empty<object>()`, and the composed target instructs the
> session to *"respond to `#<id>` with `take`"*. So the session came up, set `acceptEdits`, streamed
> *"I'll start by taking the quest"*, called `take`, had no such tool, and ended its turn having
> touched nothing.

✅ **done 2026-09-22.** `AcpMcpServer` on `session/new`, and `KnowledgeConnector` finds the machine's
own MCP host the way `ServiceHostLocator` finds the HTTP one.

**Why this is the right shape rather than a workaround.** The pipe door works because an adopted
repository's own `.mcp.json` wires the knowledge tools — *"that wiring is the connector's job at
adoption, not something the driver may reach in and write"*. A repository that has not wired one
therefore cannot be driven over the pipe at all, and the driver may not fix that. The protocol carries
the wiring itself, so the session gets its voice **with nothing written anywhere** — which is
`reaching-in` satisfied rather than worked around.

**Three details that are decisions.** The environment is **passed through, never invented**: a scratch
run overrides `DAORIS_KNOWLEDGE_DB` and friends, and a session writing to the machine's real store
because the overrides did not travel is the failure hardest to see. A machine with **no host** offers
an **empty array, never an absent field** — it still drives, the session simply has no connector, and
an agent reading `mcpServers.length` must not meet `undefined`. And the env is an **array of
`{name,value}`**, read from the adapter's own source (`Object.fromEntries(env.map(...))`) rather than
guessed.

**Two gates caught things.** The family rehearsal asserts it from the **agent's own side** — the stub
reports what it was offered and the check reads it back out of the transcript (180/180), because a
driver asserting what it sent proves only that it sent it. And this repository's own
`desktop-tool.test.ts` refused the new `DAORIS_MCP_HOST` until the scratch environment pinned it:
the locator prefers an *installed* binary, so an unnamed host means a scratch run silently hands the
session the real machine's.

**One trap, twice now:** a C# lambda parameter named `_` shadows the discard in
`TryGetProperty("id", out _)`, and the error names neither.

**The driven ACP2 run that measured this found four more defects, all fixed** (moved here from the
backlog's ACP2 entry, 2026-09-23): the presence probe asked about a different binary than the spawn
would run, so a working pin reported absent (FIX-LOG — it hid `codex` too); a scratch host with no
root of its own indexed the machine's whole family (FIX-LOG); `connect` has no `--service` flag and
an unknown flag is ignored in silence; and a driver that refuses a dirty tree was right while the
fixture, which left the adoption uncommitted, was wrong.

## CANON8d — say the new thing (2026-09-22)

> **CANON8d — say the new thing.** Four sentences D59 made false, each verified stale on 2026-09-22
> and each one edit: `analyze`'s `AGENTS.md` verdict (*"what it installs will be invisible to them"*,
> now backwards — that file is exactly where it lands); `docs/2026-08-04-daoris-design.md:71`, **D7**,
> *"The tier is the directory, not metadata"*; `README.md:178`, *"The tier is the directory"* in the
> three-layer story a consumer reads first; and `.claude/knowledge/canon-authoring.md:69`,
> *"`rules/` is always-loaded"*.
>
> A stale sentence is believed for exactly as long as it survives, and the confident one is what tells
> a reader not to go and look (`claims-need-checks`).

✅ **done 2026-09-22.** All four corrected — and reading around each one turned up as many again in the
same class, including the biggest: **the release-facing changelog had no entry for D59 at all.** CLI
**267** (was 265), `check` clean at 21,817 of 26,000, service 262/262.

**The `analyze` verdict is now split, and it has a check for the first time.** It was printed prose
with nothing asserting it, which is the failure `claims-need-checks` ends on — *a claim is only worth
what invokes it*. Two tests over `commandAnalyze`'s real output now hold both halves: a repository on
the `AGENTS.md` convention is told the always-loaded tier **lands in its own file**, and one showing
only Cursor is still told what daoris installs is invisible to it. The split keys on the **evidence
file**, not on the signal's id — it is `AGENTS.md` that decides reach, so a future signal detected by
that same file inherits the right sentence instead of the wrong one. The assertions read a
whitespace-collapsed copy, because the claim is the sentence and not the column it wraps at.

**What reading around the four turned up:**

- 🔴 **`CHANGELOG.md` did not mention the move at all.** The single most user-visible change in the arc
  — what `sync` writes, and into whose file — was absent from the release-facing record while the
  backlog, the archive and four design documents all discussed it. Added under *The canon*. Two more in
  the same file: `check` was still described as *gating* on the always-loaded budget (**D54** retired
  that), and *"syncs core into its own `.claude/`"* under **Proven**.
- **`docs/DECISIONS.md` D7 carried no amendment at all.** D59 says *"D7 is amended"* and D7 said
  nothing back, so the amendment existed only in the decision that made it — the one place a reader
  checking D7 would not be. It now names D59 for the location half and **D54** for the *gate* half,
  which a separate decision had already reversed. The back-reference convention holds everywhere else
  in that file; this was its one miss, so it is a lapse rather than a gap needing a gate.
- **`docs/2026-08-04-daoris-design.md` §11 still listed *"Owning regions of `CLAUDE.md`"* as out of
  scope**, reasoning that *"partial ownership of a hand-written file is where sync tools start
  fighting their users"*. Daoris owns a region of `AGENTS.md` **and** an import region in `CLAUDE.md`
  today. Struck through and answered rather than deleted: the fear was right, and the answer was not
  avoidance but **refusing with a line number** instead of guessing at a damaged marker.
- **`README.md`'s `sync` row said it materializes into `.claude/`** — which no longer names the file
  a consumer most needs to know gets written. And the lock is described as *one entry per materialized
  file*; it is one per **document**, the always-loaded ones carrying `in` because they are a span
  inside `AGENTS.md`. Both written from the lock on disk rather than from the design (`claims-need-checks`).
- **`.claude/knowledge/adoption.md` told an adopting agent to expect the budget to *fail*** and to look
  for a deep dive *"sitting in `rules/`"*. Both stale, from two different decisions, in the document
  that governs the flow this most matters in.
- **`canon-authoring.md` also claimed the core budget is *"measured and gated"***, which **D54**
  retired before D59 existed. Corrected in the same edit: it is measured and **reported**.
- **`CLAUDE.md` claimed 211 CLI tests**, against 265 before this item. Byte-neutral fix, which matters
  at 3,724 of 3,750 words.

**The follow-up pass, the same day: every countable claim in the two status files was wrong, and now
two of them are gated.** `CHANGELOG.md` said *"Twelve commands"* while fourteen shipped — `harness`
and `driver` landed with TOOL2 and SES3 and the sentence never moved, so it was wrong for two whole
landings with every gate green. `CLAUDE.md` had the service at 259 (262), the driver at 165 (239), the
family rehearsal at 173/173 (180/180) and the decision log at D1–D57 (D59). `TASKS.md` disagreed with
*itself*: its state line said 239 driver and 71 modules while its verify checklist said 152 and 40.
All corrected against a run rather than against memory.

🔴 **The count was the symptom; the counterpart set was the disease.** The dispatcher and the two
documents that enumerate it must stay in step and nothing made them, so `version.test.ts` gained two
checks — the README's table must list **exactly** the dispatcher's commands, and the changelog must
state the right number **and name every one**. Asserted as a **set**, not a count: a count agrees with
itself while naming the wrong command, and a renamed verb is precisely the change that keeps the
number right. Watched fail in three shapes before being believed — the old *"Twelve"*, a command
silently unnamed in the prose, and a new command added to the dispatcher that no document mentions.
The test counts themselves are deliberately **not** gated: they change on every landing, so a gate
would be a chore that teaches people to update a number without reading it.

🔴 **The backlog's own note on the fourth item was wrong, and that is the reusable part.** It read
*"a **canon** file, so it must stay project-agnostic and re-sync `examples/` in the same commit"*.
`.claude/knowledge/canon-authoring.md` is this repository's **local** document — it has no provenance
header, no row in `daoris.lock`, no counterpart under `canon/`, and the generated index marks it
`(local)`. So nothing re-syncs and the project-agnostic constraint never applied. It is a document
*about* authoring canon, and its subject matter read as its status. **The lock is the authority**
(D5) — the check is one `grep` of `daoris.lock`, and the filename is not evidence.


## ACP3 — dsh and codex as configurations of the door (2026-09-22)

> **ACP3 — dsh and codex as configurations.** `dsh --profile acp` with `DSH_HOME` as the profile seam
> (it isolates credentials, settings and sessions as one directory), the model in the profile's own
> `settings.yaml` (Daoris names none — D24), the two outbound rows (`session-telemetry-otel`,
> `session-log-deepseek`) patched off in the profile Daoris owns the location of, and no login
> question to ask (permissive `unknown`, SES3's rule); `@agentclientprotocol/codex-acp` likewise.
> **HARNESS2 closes into this.** dsh pinned exact and vendored nowhere.

✅ **done 2026-09-22**, with **HELP2 and HARNESS2** closing into it. Driver 270 (was 239), CLI 269,
modules 71. `docs/2026-09-22-acp3-probe-evidence.md` is the evidence note, written before any code.

🔴 **The finding the item was built around: the D37 posture lives in three different places.** Claude
Code names it `acceptEdits`, Codex names it `agent` — both ACP modes, **different ids** — and dsh's
`session/new` carries **no `modes` key at all**, only a `configOptions` whose single entry is the
model catalogue D24 forbids Daoris to touch. So on dsh the wire offers exactly one knob and it is the
one that must not be turned; its posture is `DSH_PERMISSION_MODE=workspace-write`, read at boot.

**What that changed in the code.** The mode id was a `const` inside `AcpSession`, correct for exactly
as long as one harness rode the door. It is now `ISessionAdapter.AcpPosture`, and **null means this
wire carries no posture** rather than a licence to guess a neighbouring one. The safe direction is
the default: every observed default is equal to or stricter than what Daoris would set, so a
forgotten posture stalls a session rather than widening it. The old constant would have set
`acceptEdits` on any agent that happened to offer one — a posture nobody chose, through a harness
nobody asked.

**Everything was established keylessly, against installed artefacts at exact pins**, and the note
says which facts came from a live wire and which from a shipped bundle. Both adapter pins were still
current a day after DSH1 recorded them, which is the first evidence that the ACP project's cadence is
not dsh's 1,687-commit week.

**Three findings that would each have failed in a way that reads as something else:**

- 🔴 **`CODEX_HOME` must already exist.** Pointed at a path that is not there, codex-acp exits 1
  before `initialize` completes — where the Claude adapter **creates** `CLAUDE_CONFIG_DIR`. The
  driver already created profile homes unconditionally, so this was true by luck: `ProfileMustExist`
  now names which harnesses depend on that line, and a test asserts it for every harness that
  declares it. Watched fail by deleting the `CreateDirectory` call.
- 🔴 **`agent` is stricter than `acceptEdits`, not equivalent** — it runs with
  `networkAccess: false`. Recorded beside the constant, because a session that cannot reach the
  network fails in ways that look like anything but a permission mode.
- 🔴 **`customSkillDirs` resolves at the wrong moment.** dsh resolves its default project roots per
  session `cwd` but resolves `customSkillDirs` **once, at construction, against the process's own
  cwd**. The driver spawns one process per tree (D51), so a **relative** `.claude/skills` lands on
  the right repository — and an absolute path written into a shared home would pin every session in
  it to whichever tree happened to be first. HELP2 as filed said "naming `.claude/skills`" and did
  not say why it must stay relative; now it does.

**The profile Daoris owns, and the line it will not cross.** `DshProfile` writes
`$DSH_HOME/cordis.patch.yml` — dsh's own home-level user patch layer, so one write reaches every
profile in that home. It disables both outbound rows and adds the skills root. 🔴 **Only where Daoris
made the directory.** With no named profile, `DSH_HOME` is unset and dsh uses the person's own
`~/.dsh`, which SES3 puts out of reach — so the run proceeds and **says** that two rows are sending
session material off the machine and that `daoris harness profile add dsh <name>` is the fix.
Refusing would strand a capability over somebody else's default; writing there would be reaching in.
A home already holding a **hand-written** patch layer is refused and reported rather than
overwritten, decided by a provenance header — the same shape as the canon's region markers, for the
same reason: what is on the other side of the guess is somebody's own configuration.

**The bytes are pinned because the real dsh composed them.** Written into a scratch `DSH_HOME`,
`dsh --profile acp --dump-config` answered with all three rows patched and **annotated each with the
file that patched it**. The test asserts that exact text: every other test there checks a property,
and a property-checked file can drift into something that satisfies every property and no longer
composes.

**A third login state, declared rather than inferred.** The CLI's harness invariant — *a harness
answers the login question itself, or names whose account it uses* — refused the dsh entry, correctly:
dsh has neither. "Has no account" and "nobody wrote the check yet" are indistinguishable from
outside and must not be, so `noAccount: true` is a declaration, silence is still a failure, and a
harness that declares it and then describes a login is rejected as saying two things. The invariant
caught this on its first run, which is what an invariant is for.

**What waits: a driven run per harness**, exactly as ACP2's does — and for codex it is the same step
that confirms the mode list on a live wire rather than from a bundle.

## The first deployment — the desktop as an installed application (2026-09-22)

> *"setup and deploy the desktop version to \<install\> and we can drive and log it properly for some
> real case study and testing"*, then *"currently it just bit messy"* about the folder, *"the
> application topbar you can take more example from application like vscode"*, and *"the backdrop
> should not cover the topbar? because we do have the hole for the 3 buttons"*.

✅ **done 2026-09-22.** `docs/2026-09-22-first-deployment-case-study.md` is the record. Driver 283,
CLI 269, modules 71, web 420, family rehearsal 180/180.

**The first time any of Daoris ran outside its own workspace**, and that is the whole value: four
defects, and three of them were invisible *because* of something the workspace provides.

- 🔴 **`ServiceHostLocator` never looked where the installer puts the HTTP host.** The workspace-build
  fallback meant the shell always found *a* host and nobody asked which; a deployed machine has no
  workspace to fall through to. FIX-LOG.
- 🔴 **A session transcript was decoded as the machine's ANSI codepage** — an em-dash recorded as
  `e9 88 a5 3f`. Silent and permanent: the file afterwards is valid UTF-8. A platform that ships
  简体中文 would have lost every Chinese character in a record. FIX-LOG.
- **There was no way to deploy at all.** `tools/desktop-publish.mjs` is now `service-publish.mjs`'s
  sibling. Its guard is a marker file rather than an extension allowlist — the first version
  allowlisted what a publish emits and refused its own second run over three `.xml` docs.
- **`testbed.mjs` claimed idempotence and died on its second run**, and never wrote the two files a
  driven repository needs.

**The install folder, taken from the machine's own neighbours** (the owner pointed at one): a single
launcher at the root, supporting binaries under `app/`, state in `data/`. A single-file publish got
it from **24 entries to 3** — `daoris-desktop.exe` at 2.8 MB, framework-dependent because the shell
already needs a Windows desktop runtime and WebView2 and would carry neither. The locator gained the
`app/` shape in the same change, because the layout and that list are a counterpart set.

🔴 **The finding the driving produced: a repository cannot grant its own trust.** Three real driven
runs, all recorded `failed`, and the driver was right every time — it concluded from exit code, quest
state and absent commits (D46 §4) while each session said a great deal about itself. The cause is one
line the harness prints: *"Ignoring 9 permissions.allow entries from .claude/settings.json: this
workspace has not been trusted."* Daoris can wire the connector, the allow-list and the posture and
still be one machine-level flag short — in the person's own `~/.claude.json`, which SES3 puts out of
bounds. **DEPLOY1** carries the decision, and it is the owner's: the flag *is* the grant.

**DRV4 did not see this** because its scratch repository had been opened by hand that session, and
the dependency lived in one parenthetical of its archive entry. Same shape as the locator: it worked
everywhere it had been tried, because everywhere it had been tried had what the untried case lacks.

**The sessions themselves are the other result.** Both followed the doctrine Daoris had synced four
hours earlier, unsupervised: refusing to grant themselves the missing permission and **citing
`file-tool-discipline`**; refusing to commit on ungreen gates, **citing `autonomous-development`**;
writing claims from the implementation and **naming which no run had confirmed**; and working out
unprompted that `.claude/knowledge/` is generated and lock-tracked, so their own documents belonged
in `docs/`. Both trees are preserved on branches.

**Two screens, both found by photographing the window rather than reasoning about it.** The app strip
held **~1,400px of nothing** with the palette reduced to a 14px glyph beside the caption buttons —
now a centred command center that says where you are and prints its shortcut, taken from the shape VS
Code settled on. And a full-bleed scrim dimmed the title bar while the **natively painted** caption
buttons stayed bright, punching a white block through it; a scrim now starts below the strip, held by
`tokens.test.ts` as a rule about every overlay, because the next one would have inherited `inset-0`
without anyone thinking about it.

## DEPLOY2 — the deployed artefact gets a gate (2026-09-22)

- [x] **DEPLOY2 — nothing gates the deployed artefact.** `rehearse` installs and drives the CLI
  *package*; both rehearsals otherwise run inside the workspace, where the workspace build and the
  console's own encoding paper over exactly the two defects this deployment found (the host the
  locator could not see, the transcript that was not UTF-8). The work is a gate that **publishes the
  shell to a scratch folder and runs it from there** — the desktop's `rehearse`. It needs no model
  and no account: bringing the window up, finding its host and writing one non-ASCII line to a
  transcript would have caught both.

✅ **done 2026-09-22** — `tools/deployment-rehearsal.mjs`, `npm run rehearse:deploy`, **29/29**. The
seventh declared gate, and the first that runs on Windows: `daoris.gates.json` names it and the
release workflow gains a `windows-latest` job, because the two lists are checked against each other.
**D60** carries the decision. No model, no account, no credential.

**Both defects were watched failing**, which is the only thing that makes a new check worth anything:

- Removing the locator's nested and `app/` candidates — 2a, exactly — landed the **deployed** shell
  on `src/Daoris.Service/Daoris.Service.Http/bin/Debug/net10.0/`, and the gate named that path. That
  is the masking agent itself: on a machine with no workspace beneath the install, the same
  regression is *no host at all*.
- Removing `StandardOutputEncoding` from the one place every adapter spawns through wrote
  `stub: 閬撹 鈥?the unfolding of the way` into the transcript — `e9 88 a5 3f` for the em-dash, byte
  for byte what the case study recorded.

**Seven phases, and each check is a claim somebody had written down and nobody had read back.** The
publish says one launcher at the root and no symbols beside it; it says the host travels under
`app/daoris-knowledge-http/` with its bundle; `INSTALLED.md` says `data/` is the install's own state;
`--service` says the folder is self-sufficient. Then the artefact: the **deployed** window comes up
with `DAORIS_HTTP_HOST` deliberately absent and no `--app-root`, finds a host that is not this
workspace's build, and its own driver loop carries a quest to done through the session's door —
with the transcript compared **as bytes**, because mojibake is valid UTF-8 and a decoded comparison
cannot tell.

🔴 **What it cannot control is stated rather than implied.** `~/.daoris/bin` outranks a deployed copy
by design and the profile is not redirectable (.NET resolves it from the OS token), so phase 4 prints
which host it located instead of pinning one — on this machine, the nested installed one, which is
the very candidate 2a could not see. And a machine whose ANSI codepage is already 65001 cannot fail
phase 5; the check still goes red on every machine that can express the defect.

**Two traps it walked into on its way in.** The module ran its phases *on import*, so the unit suite
published a folder and started a window — the same trap `tools/desktop.mjs` documents at its own
foot, now guarded the same way. And the first version identified "the host the shell started" by
diffing **paths**, which reports nothing new when two processes share one binary; it diffs **pids**.
Both are in the file, where the next person meets them.

## ARCH1, PLUG1, PLUG3, DEPLOY1, DEPLOY3 — moved out of the backlog (2026-09-22)

These five closed on 2026-09-22 and were **ticked in place rather than moved**, which is the failure
`task-lifecycle` names: three of them had no per-task record anywhere, and the backlog carried eight
`[x]` rows over four sections while claiming to hold open work only. The entries below are their
backlog text, preserved word for word; ACP3, ACP4, HELP2 and HARNESS2 were ticked in place too and
already had entries of their own, so those rows were simply removed.

- [x] **ARCH1 — dsh's domain separation and plugin design as the example for Daoris's own
  structure.** ✅ **done 2026-09-22**, widened by the owner to include a second reference
  (*"you might check how yaorin did"*). `docs/2026-09-22-plugin-design-study.md` is the note.
  🔴 **It reordered its own question**: Daoris already has **three** extension systems — canon packs,
  the declared gate list, and the ACP door — and none of them is called one. So the finding is not
  "Daoris needs plugins" but "two of the three are undocumented and one manifest is missing a
  version field". The Cordis runtime is **declined again** on the evidence already gathered, and
  *registrations are effects* is adopted as the rule for anything ever loaded at runtime. PLUG1–3
  below are what it left.

- [x] 🔴 **PLUG1 — a pack cannot say which canon it needs.** ✅ **done 2026-09-22.** `apiVersion` on
  every `pack.json`, read **before a single file is planned** — taken from the neighbouring
  application's plugin manifests, which have carried an integer all along. A pack from a newer canon
  is refused **naming both numbers**, because "incompatible" alone sends a person to guess which side
  is behind. **Absent means 1**, so every pack written before the field keeps working: the field is
  how a pack opts into saying something, never a wall in front of one that never spoke. A non-integer
  is a malformed manifest rather than an old one, and errors. Raise the number only when a pack
  written for the new shape **cannot work** on the old one — one that goes up on every change teaches
  people to ignore it.

- [x] **PLUG3 — three extension points and nobody is told.** ✅ **done 2026-09-22.** The README has
  an *Extending it* section naming all three — a pack, a gate row, and speaking ACP — what each may
  add, and what is deliberately not extensible (the views, D52), pointing at the study for the
  reasoning. 🔴 **It cost its own budget lesson**: the section put the README 225 words over, and the
  answer was to relocate detail the design docs already hold (the harness paragraph, the `--force`
  one) rather than raise the ceiling or shave the new section to uselessness (D28).

- [x] 🔴 **DEPLOY1 — a repository cannot grant its own trust.** ✅ **the detection half is done
  2026-09-22**, which was option (a) and the only half that is not the owner's to give. The driver now
  reads the harness's own record before spawning and **holds** with the sentence that fixes it, instead
  of spending nine minutes and a real login on a session that could never close its quest. Proven
  against the real untrusted tree. `ClaudeTrust` reads and never writes: unknown is permissive (no
  file, unreadable, a shape this build does not know), and only a definite *no* refuses.
  **What is still open is the owner's:** whether adoption should ever *ask* and write the flag
  (option b), or whether the pipe door stays documented as needing a human's first visit (option c).
  🔴 **Never (d), silently.** And **measure the ACP door**: it runs the Agent SDK rather than the
  CLI's trust flow, so it may not have this problem at all — one driven run answers it.

  🔴 **The decision half stays open and is the owner's** — it is carried in the backlog's deployment
  section, not here, because a question nobody has answered is not finished work.

- [x] 🔴 **DEPLOY3 — there is no credential management surface.** ✅ **done 2026-09-22.** The Machine
  view could list a harness's profiles and log into one, and could not **make, choose or un-point**
  one — those three verbs existed only in the CLI, so the owner's *"there is no credential management
  location"* was literally true. **D50 violated in the direction nothing checks**: the rule is written
  "whatever a screen can set, a terminal can" and the converse had no test anywhere.
  `profile-add|remove|default` over `HARNESS_ACTION`, and the roster's own form. 🔴 **"Forget", not
  "delete"** — it stops this machine pointing at a profile and removes nothing, because the directory
  holds a credential the harness put there; the word on the button is the word for what happens, in
  both doors. Daoris manages directories and names, never secrets.

## DEVKIT3 — the devkit runs over its own repository (2026-09-22)

- [x] **DEVKIT3 — the devkit is not run over its own repository, and its scan has six findings
  waiting.** Found by DOCS2 while looking for somewhere to put a gate
  (`docs/2026-09-21-dsh-evaluation.md` §3a). The release workflow runs the devkit's *test suite* and
  then each declared gate by name; nothing runs `daoris-devkit verify` here, there is no `.githooks/`,
  and so the universal gates this repository configures in `daoris.gates.json` — sensitive, version,
  docs, links, doctrine — are configuration nothing reads. Run by hand it exits 1 on **six sensitive
  findings, all in test fixtures**: Unix home paths and private-range addresses, the same shape as the
  one object already acknowledged by sha. The work is to read and judge each — acknowledge it by sha
  or neutralise the fixture, never a path ignore (the devkit's own asymmetry argument) — then wire
  `daoris-devkit verify` into the gate list and the release workflow **as one row in both**, and
  correct the devkit README's "it runs this repository's own gates", which today it does not.

✅ **done 2026-09-22.** Five universal gates now run here, declared as `universal` and run by the
release workflow. Devkit 59 (was 57), service 262, driver 294, CLI 281. **Nine findings, every one
judged, none silenced by path.**

**Eight sensitive, all fixtures, and the item under-counted because the repository grew.** Two
Windows placeholders (`C:\Users\<a name with a space>\`) keep the point they were making — a profile
path with a space breaks `shell: true` — and stop looking like a real one, using the angle-bracket
convention the deployment case study already set. Four Unix home paths became `/srv/…` and
`/profile`: a home directory was never what those tests were about, and the locator's one is a
don't-care argument. Two LAN addresses became **TEST-NET-3** (RFC 5737) — a range that exists to be
written down, which is strictly better for a fixture than a real private subnet.

**The ninth was the docs gate**, and it was stale in a way that mattered: `src/Daoris.Service/README.md`
claimed **170 tests** where there are 262, and said nothing about the doctrine region (D59) or the
tree-keyed ledger (D51). A gate nobody ran is how a number stays wrong for two months.

🔴 **`reviewedObjects` is consulted in HISTORY scope only**, so the item's "acknowledge it by sha"
half does not exist for a tree finding. That is the devkit's own asymmetry argument working as
written — a working-tree ignore silences a file and the next secret written to it is silent too — and
it means the tree route is always *change the file*, which is exactly what the acknowledged `04801cb`
did in its day: the fixture was fixed in the tree, and the sha covers the immutable copy behind it.

🔴 **The wiring could not be what the item said, and the reason is worth keeping.** `verify` runs the
declared gates *after* the universal ones, so a row naming it reaches itself; and since DEPLOY2 one
declared row is Windows-only and cannot pass in a Linux job at all. The devkit gains
**`--universal-only`**: the universal half is by definition the half a declaration does not contain,
which makes it well defined where the whole is not. It **names the rows it did not run**, for the
reason a disabled gate is printed — a run that quietly checked half of a declaration reads as
coverage it is not.

**CI adds `--allow-builtins-only`, and that is the designed path rather than a weakening.** The
private pattern list cannot live in the repository being scanned, so on a runner it is always absent
and the gate **fails closed** — proven both ways here by hiding the file: without the flag it refuses
and says how, with it the scan runs on 6 structural patterns instead of 15. The dogfood test still
matches, because the workflow line contains the declared row as a substring.

**Watched failing**, as a new check must be: a Unix home path put back into a fixture turns the run
red naming the file. **What DEVKIT3 did not do**: `.githooks/` is still absent — `install-hooks`
writes tracked hooks and sets `core.hooksPath`, which changes how a person's own commits behave, so
it is theirs to run. The diagnosis mentioned it; the stated work did not include it.

## The toolchain twins cover every shared name (2026-09-22)

Found by reading **TOOL5** against what is built rather than by a failure. CLI 282 (was 281), driver
296 (was 294).

`TOOLCHAINS` (TypeScript) and `AdapterSet` (C#) are twins whose risk is **not membership** — they
differ on purpose, because managing a tool and spawning sessions on it are different questions (D23).
The risk is a shared NAME whose descriptors disagree: the CLI probing one binary while the driver
spawns another is a `harness list` reporting on a program nothing runs, and it reads as correct from
both sides. The binary is the field that has **already been wrong once** (`claude-agent-acp`, guessed
as the adapter's Daoris name, caught by a pin reporting as not installed).

🔴 **The check covered three names and left out the one a machine actually drives with.** The table
was written during ACP3 and pinned the three arrivals; `claude-code` — the oldest entry — was
asserted on neither side. Nothing was wrong with it, and nothing would have said so: this is the
half of `claims-need-checks`' counting trap that has **no earlier number to fall from**, because the
count never rose.

Fixed by deriving membership rather than remembering it. Each side now iterates its own set and
requires a pinned row for every entry that names a real binary — the stubs excluded by the only
honest test there is, that they name none (D46 §8). Both halves were watched failing: removing
`claude-code` from the C# table and `codex` from the TypeScript one each turns the suite red naming
the harness and saying what the divergence would look like.

## CANON5 — translation parity enters the canon as a pack (2026-09-22)

- [x] **CANON5 — i18n en/zh parity as canon: the two-repository bar is met, and the budget no longer
  blocks it** (unparked by CANON7, 2026-09-21). The bilingual sibling carries the rule and the gate;
  Daoris carries the same gate (`scripts/i18n-check.mjs`, adopted from it deliberately — D42). Two
  repositories, one lesson: a missing translation "works" in English and is discovered by the first
  reader it fails. There is now room for roughly one substantial rule, **which is exactly the budget
  this would spend** — so the question it must answer first is the one the room does not settle:
  whether this belongs in the always-loaded core at all, or as **pack knowledge for web
  repositories**, which is where it most likely belongs. A rule every repository loads on every task
  to govern a concern only some of them have is what the pack tier exists to prevent.

✅ **done 2026-09-22** as **`localized-ui`**, the canon's seventh pack, holding one on-demand
document — `translation-parity`. **D61** carries the tier decision and the four it rejected. Daoris
adopts it (`packs: ["localized-ui"]`), which is what validates it: a pack nobody installs is a draft
that looks like doctrine.

**The question it was parked on answered itself once it was measured.** Adopting the pack moved the
always-loaded core from 21,817 to **22,171 bytes — 354 for the index row**, against roughly 3,800
had the same content gone into core. The pack tier is a factor of ten here, not a filing preference,
and that is the whole of the argument the item asked for.

**It is not `web-webview`** — the nearest existing pack, and wrong: that one is about hosting a web UI
inside a native shell. A desktop application with resource files has this exact parity problem and no
webview. The concern is a shipped interface in more than one language, never the transport.

**What the document carries**, all of it from what two repositories actually learned rather than from
the shape of the subject: parity is compared **both ways**, because a key present only in a
translation is one somebody deleted from the default and left behind; keys are **structural**, since
with English-as-key a missing translation renders flawless English and the check cannot even exist;
and **three kinds of text reach a person and only one belongs in a catalogue** — chrome, stored
content, and a message whose exact wording *is* the contract. That third row is the expensive one: a
refusal naming what to run is an instruction, and a translated instruction is a second instruction.
Where it must still reach a reader in their own language it passes **through** an entry that is
nothing but the placeholder — and the placeholder then needs its own assertion, because a translation
that drops it replaces every such message with one fixed sentence.

🔴 **What it deliberately does not carry.** The encoding traps — text that renders correctly and is
then destroyed by a console or a redirected stream — are real and cost this repository a defect the
same week, and they already live in `windows-machine`. The document names the concern and points away
from itself. Two copies of a rule become two different rules.

## DEPLOY4 — the application folder is the home (2026-09-23)

- [x] **DEPLOY4 — Daoris writes in two places, and one of them is per-install.** Everything the tool
  owns is under `~/.daoris` (registry, `driver.json`, `harnesses.json`, the index, sessions, `bin/`,
  `toolchain/`, and profiles when they exist) — **except** the desktop's own `data/` beside the
  install, which holds the WebView2 profile and the window's geometry. Two homes for one application.
  🔴 **`~/.daoris` being machine-wide is load-bearing, not an accident**: it is what makes the CLI and
  the desktop two doors onto one machine (D50), and moving it inside an install would give two
  installs two registries and leave `daoris` on a terminal unable to see either. Owner's call;
  measure before moving anything.

✅ **done 2026-09-23 — decided by the owner, the other way from the recommendation.** The item was
measured on 2026-09-22 (472 MB under the profile against 21 MB and five integers under the install)
and the recommendation was *leave it*. The owner read the profile directory and said what the
measurement had not asked: *"we should manage files within the app folder instead put it in
shared"* — and, to the pointer file proposed so a terminal could still find the home, *"you should
not keep dump thing into user folder"*. **D63** carries the decision and the three shapes rejected.

**What landed.** One seam, `DAORIS_HOME`, held by three twins sharing no code (the CLI's `home.ts`,
the service's and the driver's `DaorisHome`), and every default in every artefact derived from it —
the driver's config, the harness wiring and profiles, the remotes map, the index, session records,
usage, trees, the installed service binaries. With no home there is **no default**: the management
class and both hosts refuse in one sentence naming the variable, and doctrine commands never needed a
home. The installed desktop establishes the home first thing — `data/` beside the executable, set on
its own process before any module captures a path, and once on the account's environment when it has
none — and a `~/.daoris` from before the decision moves in on the first start with an empty home,
`bin/` excepted, one entry at a time so a file something still holds open costs that file and not
the start. The shell says what it did once, on the channel a person acts on. The publish scripts,
the testbed's connector and the `.mcp.json` snippet follow the home; the deployment gate plants its
own decoy under the scratch home's `bin/`, so the locator's order is asserted on every machine rather
than only on one that happened to have run `publish:service --install`.

**The load-bearing sentence in the original item was wrong about what carried the load.** Two doors
meet at a *location both can find*, and a variable is one: the desktop names it, the terminal reads
it. What was being defended was the profile directory, and the profile directory was only ever the
place the variable had not been invented yet.

## PLUG4 — the catalogue and a declared harness (2026-09-23)

- [x] **PLUG4 — the catalogue and a declared harness.** `<home>/plugins/<id>/plugin.json` read by
  both twins (the driver's `PluginCatalog`, the CLI's `plugins.ts`): `apiVersion` before anything
  else, `plugins.json`'s disabled list, a conflict refused naming both sides. `daoris plugin
  list|add|remove|enable|disable`. A plugin's `harnesses` row becomes a configuration of the ACP
  door in `AdapterSet`, so `harness list` shows it and a session can run on it.

✅ **done 2026-09-23** — the first slice of D64. Two twins sharing no code read the same folder by
four rules each carries a test for: the version before anything else (a newer one refused naming
both numbers, a non-integer malformed rather than old), a broken manifest a named problem never a
crash, a conflict refused before anything loads naming both sides (a name this build carries, or one
an earlier plugin by id declared), and disabled a row never a rename. `AdapterSet.WithPlugins` adds a
`DeclaredAcpAdapter` per contributing harness — the ACP door configured from a file, with the
manifest's posture or null (ACP3), the profile variable that makes an account, and the toolchain rows
the roster needs. 🔴 **A declared harness with no version question is probed by presence, never by
running it**: an ACP agent started bare waits on its stdin, and the probe's twenty-second patience
per roster refresh would have been a stall — `HarnessToolchain.ProbeByPresence` asks the file or
`PATH` instead. `${plugin}` in a command is the install folder, because a plugin cannot work that out
for itself (the neighbour's SDK says so in its own words). `add` replaces the install wholesale and
leaves `.data/<id>` alone; `remove` names the data folder rather than deleting it. No code from a
plugin loads anywhere. Gates: verify (312), driver 325, modules 91, family 181/181.

## PLUG5, PLUG6 — a plugin may speak, and the Plugins card (2026-09-23)

- [x] **PLUG5 — the hook wire.** A hook process per enabled plugin, started and stopped with the
  loop; `initialize`, `quest/consider` as a fail-closed waterfall whose hold is the consideration's
  reason, `session/ended` contained. Proven by a stub hook plugin in the family rehearsal — one
  quest held with a sentence, one ending observed, the plugin stopped with the loop.
- [x] **PLUG6 — the Plugins card and the roster's provenance.** The Machine view lists plugins as
  rows (name, what it declares, its problem if refused, its switch), and a declared harness carries
  the plugin it came from.

✅ **done 2026-09-23** — the second and third slices of D64. `HookPeer` is the wire (JSON-RPC over
the plugin process's stdio, the `AcpSession` framing with the roles reversed): a versioned
handshake that takes the points the process *actually* listens on and refuses one beyond its
manifest, `quest/consider` answered as a typed decision, `session/ended` as an acknowledgement,
`shutdown` as the notice before the process is ended. `HookProcess` starts the plugin's program in
its own folder with its id, folder and data folder in the environment — told, never guessed — and
relays its stderr under `plugin:<id>`. `HookSet` is shared across ticks like the process registry:
reconciled against the catalogue each tick (started, stopped, restarted on a manifest change; a
start that failed is a line and is retried), asked as a **fail-closed waterfall** before any start
costs anything — the first hold in catalogue order is the quest's own sitting reason, and a plugin
that answers late, wrongly or not at all holds too, naming itself and `daoris plugin disable` as
the way out — and told of endings, contained. Both hosts own one and stop it with the loop. The
family rehearsal's phase 18 drives all of it with the ACP stub as a *declared* harness. 🔴 **It found
the stub agent's own bug on the way**: a fixed answer file with fixed content left a second session
with nothing to commit, and a turn that failed was never answered, so the driver waited on its
timeout — a hang dressed as a session. Per-quest files and an error reply now.

The Machine view's Plugins card is a `SettingRow` per plugin — name, version, running or off, what
it declares and speaks on, the folder — with the driver's own sentence beneath a refused one, Turn
on/off as the row `plugins.json` holds, and Remove naming what the plugin kept. A declared door
wears a chip naming its plugin. `PLUGINS` and `PLUGIN_ACTION` on the bridge, two refusal codes in
both catalogues, and the driver state's `plugin` per harness. Gates: verify (313), driver 339,
modules 93, web 507 + 14, family 192/192, deploy 36/36.

## INT1 — plugins declare MCP servers handed to every session (2026-09-23)

- [x] **INT1 — plugins declare MCP servers handed to every session.** `servers` in `plugin.json`
  (name, command, arguments, environment, `${plugin}` expanded); the driver offers them beside the
  knowledge host over ACP (`session/new`), and over the pipe door by `--mcp-config` pointed at a
  file written under the home per spawn — never the repository. The example plugin declares the
  Playwright MCP. Proven by the family rehearsal's stub reporting every server offered.

✅ **done 2026-09-23** — the first slice of D65, and the one that puts *test it in a browser* in a
session's hands without a browser extension or a line of Daoris code. `PluginServer` on the
manifest in both twins, read by the same rules: a name in the id shape, a command, an optional
`env` of strings, the placeholder expanded in both; the knowledge host's name refused naming it,
and one name two plugins claim kept by the first id — a refused plugin contributes no server, as it
contributes no harness and no hook. `PluginCatalog.Servers` is what a tick hands: the driver now
reads the catalogue **every** tick (it had been read only when hooks were owned), offers the servers
after the connector on `session/new`, and over the pipe door writes `SpawnServers` — the harness's
own `mcpServers` shape under `<home>/spawn/<session>.mcp.json`, handed by the adapter that takes a
file (`--mcp-config`, verified against `claude --help`; the default adapter takes nothing) and
removed when the session ends. `examples/plugins/browser` is the tracked example; the family
rehearsal installs it beside the other two and reads `daoris-knowledge, browser` back from the
agent's own account of what it was offered. What a session may *call* stays the repository's
allow-list (D37). Gates: verify (317), driver 346, modules 93, family 195/195, deploy 39/39.

## INT2 — links and attachments on a quest (2026-09-23)

- [x] **INT2 — links and attachments on a quest.** `links` travel with the quest; `attachments`
  are copied under `<home>/quests/<id>/attachments/` by content hash, machine-local; the compose
  drawer takes drops and pastes; the composed target names them and `DAORIS_QUEST_ATTACHMENTS`
  points at the directory. HTTP and MCP surfaces carry both.

✅ **done 2026-09-23.** This is the second slice of D65, and the one that lets an ask carry the
ticket and the screenshot. `Quest` gains `Links` and `Attachments` (name, sha256, size), held as
two columns the store adds in place. `QuestFiles` is the only author of the layout
(`quests/<id>/attachments/<hash12>-<name>`); a name is made safe, so a kept file can never land
outside its directory. `QuestExchange` judges what a quest carries in one place for every door:
http(s) links only, at most 10 files and 20 MB, the same content once. It keeps the bytes only after
the record exists, and says what a re-publish did not add. The relay's signature carries names and
hashes and has no field for bytes. The HTTP door takes content at a local host and names at a
shared one, refusing the other shape. The MCP `quest_publish` takes paths, and `quest_list` names
what each quest carries. A new local, loopback-only route serves a kept file sandboxed.

**One design change, recorded as D65's amendment.** The driver does not derive the directory. Its
home is wherever `driver.json` lives, and the family rehearsal is a case where that differs from the
host's. So the host answers this machine each kept file's path, only when the bytes are here, and
the driver sets `DAORIS_QUEST_ATTACHMENTS` and the target's list from that answer. A file kept on
another machine is said to be elsewhere.

**The platform:** the composer takes links one per line, and files by drop (the whole composer, with
stray drops absorbed so the webview never navigates to a file), paste or *choose files…*. The drawer
shows links as links and files by name and size, a picture as a picture, and never the path; the
card counts both.

**Seen on the scratch shell in both themes.** Building it found two event props consumed during
render: the composer's opening draft (SURF6b's *send it back…* opened nothing on the window) and the
Work frame's palette intent. Both are fixed and held by tests, with the rule in frontend
architecture §4b.

**Proven by** the family rehearsal: phase 7's session reads the link and the file's bytes from what
it was handed, and phase 11's machine b, with a home of its own, is told the file is elsewhere. The
remote refuses content and its store holds no file's bytes. Playwright proves the real bundle keeps,
shows and sandboxes a file.

Gates: verify (317), service 311, driver 354, modules 96, web 530 + 15, family 203/203, deploy
39/39.

## INT5 — `then` on a quest (2026-09-23)

- [x] **INT5 — `then` on a quest.** Published by the exchange at the moment of `done`, atomically;
  the driver picks it up at its next look. Linear in v1.

✅ **done 2026-09-23.** The workflow D65 asked for, as data on the quest: `then` is an ordered list
of steps (`to`, `title`, `body`, at most five). The store's close to `done` publishes the first
step **in the same transaction** as the guarded update, so a close another host wins publishes
nothing here and there is no moment at which the work is done and the chain is lost. Each step is
asked on behalf of the same asker, carries the rest, and names its `parent`; `{parent}` in its words
becomes that id.

**A step's id derives from its parent as well as its words.** Ids are content-derived, and a step
called "Verify in the browser" would otherwise have joined an older quest of the same words; an
unchained quest's id is unchanged.

`QuestExchange` judges the whole chain when it is composed:
- every step addressable from the asker;
- no step back to the asker;
- no step without its words;
- one home per chain (D47 §5): a chain straddling the remote and this machine is refused naming
  both, since each step is published where the one before it closes.

A decline stops the chain. The relay carries the chain to the remote, and the remote's own close
publishes each step; the mirror carries `then` and `parent` both ways. The HTTP door takes and
answers the list, and `quest_publish` takes it as `ChainStep[]`. **The MCP door has C# tests for the
first time**, which also covered INT2's paths-to-bytes. They include its schema: an agent that
cannot see a chain described cannot compose one.

A driven session is told what its quest follows and what closing it will publish. The platform shows
the steps still to come and a step's parent, and the composer offers one next step behind a press.

**Seen on the scratch shell:** closing the chained quest in the drawer published *Verify #c7c4de in
the browser*, marked *follows #c7c4de*, and a step asking the chain's own asker was refused in its
own sentence. The look also found the drawer's scrollbar painted light in dark, because the page
declared no `color-scheme`; it now follows the theme.

**Proven by** the family rehearsal: develop → verify, both driven to done by one `--until-idle`, the
second existing only once the first closed. Playwright proves the real host's close publishes the
step, and the drawer shows it.

Gates: verify (317), service 330, driver 356, modules 96, web 534 + 16, family 206/206, deploy
39/39. *(The modules count was from a run made before the scrollbar edit, which turned the
palette test red; found and fixed by INT4a — FIX-LOG 2026-09-23.)*

## INT4a — the ask, and its floor (2026-09-23)

- [x] **INT4 — the intake** *(split 2026-09-23 into INT4a, INT4b, INT4c; this is the first
  third)*. The ask record; the room under `<home>/intake/<workspace>/` seeded from the registry;
  `daoris-driver ask --workspace … [--file …] [--url …] "…"` and the desktop composer at workspace
  scope; `intakeAdapter` in `driver.json`; the declarations-only tier that proposes and reports
  itself. Proven with no model by the rehearsal; a real ask is the owner's.

✅ **INT4a done 2026-09-23.** It covers the ask record, the declarations-only tier and the terminal
door; the room and `intakeAdapter` are INT4b, and the desktop composer is INT4c. The split exists
because the row named three landings, and `model-decoupling` says which comes first: the floor that
works with no provider.

**The ask.** `AskStore` and `AskDesk` hold a sentence entered at a workspace: its links, the files it
keeps under `<home>/asks/<id>/`, who asked, and what became of it (`open → proposed | published →
closed`). Every record names the tier that answered.

**The tier.** `DeclarationsTier` ranks the circle's adopted repositories by the words their summary,
`owns` and `accepts` share with the sentence, using the index's tokenizer, CJK bigrams included. It
drops a sentence's glue, folds a plural, and counts an owned word double. It proposes the top three
with the matched words as evidence and **publishes nothing**.

**Named receivers.** `--to` publishes at once through the exchange, asked by `ask #<id>` in the
ask's circle (`QuestAsk.Workspace`, new); a refused `--to` keeps the ask with its proposal.
`daoris-driver ask` has three forms (ask, `--publish <id> --to`, `--close <id> --reason`), with
exit codes 0 answered, 1 refused, 2 tool error. The HTTP door is local-mode only.

**Proven by** the family rehearsal: an ask with no harness proposes the engine and says *by
declarations only; no intake harness ran*, publishing nothing. A person closes it. A refused `--to`
keeps the ask. A named `--to newcomer` with a link and a file becomes a quest the driver drives to
done, whose session read the file.

**Two defects found on the way, both fixed** (FIX-LOG):
- **The planner's crash** (DRV2 × D51): the planner keyed active sessions by repository, and D51's
  two conversations in one repository broke every tick.
- **INT5's palette test**: INT5's scrollbar edit had turned the desktop palette test red, after its
  modules run.

**The map for INT4b** found eight traps in the conversation plumbing, all written into INT4b's row.

Gates: verify (317), service 348, driver 357, modules 96, web 534 + 16, family 212/212, deploy
39/39.

## UX2, UX3, UX4 — one bar, a settings page, circular counts (2026-09-23)

> *"2. we dont really have a setting page, so there is no way to change theme 3. since its all tab
> based so there probbaly no need for manage/work? mostly just overview/monitor?? 4. and we should
> keep polish the ui/ux currently the notification number is not even circle border"* — the owner,
> looking at the desktop; for 3 the owner chose *one bar, Sessions is a view* from three shapes.

- [x] **UX2 — a settings page, and the theme is one of its settings.**
- [x] **UX3 — one frame, not Manage ⇄ Work.**
- [x] **UX4 — the activity bar's count is a circle.**

✅ **done 2026-09-23 (D66).**

**UX3.** The mode is gone from the strip, the bar, the palette and the stories. The activity bar is
one list: Overview, Sessions, Quests, Projects, Convergence, Search. Settings takes the foot, after
the refresh and language actions, and wears a gear instead of sliders. *Sessions* is what the Work
frame was. A remembered Sessions reopens it (`daoris.view`), and a browser has none, so a remembered
one falls back to Overview. The palette offers the same list, minus the view you are on.

**UX2.** `theme.ts` holds the viewer's choice (*system · light · dark*) as `data-theme`, applied in
`main.tsx` before the first paint and pushed to the window's native chrome with the OS's own changes.
`tokens.css` gains forced light and dark blocks that outrank the media query, and `tokens.test.ts`
holds each equal to its system twin, proven by a sabotaged copy it catches. Settings is Appearance
(theme and language, each a new `Segmented` radiogroup with arrow keys) and, on the desktop only,
*This machine* beneath it. In a browser it is appearance alone, which Playwright asserts on the page
itself.

**UX4.** `CountBadge` is one fixed height with the same minimum width and no line-height of its
own. On the window it measured 14 × 17.2 before, then 16.45 × 16 after the first try (the padding
alone overran), then 16 × 16.

**Seen on the scratch shell:** the bar in light, Settings in light, Dark chosen from the page
(caption buttons included), and Sessions from the bar.

## UX1 — an account is made by signing in, and removing it removes it (2026-09-23)

> *"1 forget account does not delete the account (we probably should just allow to login and create
> account based on login? this ui/ux need to be updated)"* — the owner, looking at the desktop.

- [x] **UX1 — an account is made by signing in, and removing it removes it** (D66 §3).
  `claude auth status` names who signed in; `daoris harness` is the terminal twin (D50).

✅ **done 2026-09-23 (D66 §3, "as built").**

**Signing in makes the account.** `HARNESS_ACTION login-new` opens the next free `account-N`
(`HarnessSettings.NextAccount`), runs the tool's own login into it, and keeps it only when the tool
exits 0 and does not call that home signed out (`HarnessRoster.LoginOfAsync` asks the one home).
Otherwise the directory goes: failed, stopped, or never started. `HARNESS_ENDED` carries `account`
and `kept`. The terminal twin is `daoris harness login <harness> --new` (`signInNew`, with the
spawn injected so the judgement is tested with no account).

**Who, not the directory's name.** `LoginQuestion.Account` is a pattern whose first group is who is
signed in: the email, for `claude auth status`. It is read only on a yes, on every probe, and
written nowhere. `ProfileReport.Account` and `HarnessReport.OwnAccount` carry it, and the roster,
the start form and `harness list` show it. The directory is never renamed, because a harness may key
its credential to the home's path.

**Remove removes.** `HarnessSettings.RemoveProfile` and the CLI's `removeProfile` delete the
directory, sign-in included, clearing read-only files first. A name that points elsewhere is refused
before anything is touched. A delete that fails says so and leaves the wiring standing. On the page,
the first press only asks; the second deletes.

**Seen on the scratch shell:** the tool's own account named by its email; the confirm in both
themes; *Remove it* on the fixture's `owner` took the directory and the row. Seen there, and fixed:
the removal's console line sat under the *direct* door as a live console. Only install, update and
pin stream under *Ways in* now, and a test that was red under the old gate holds it. The real sign-in
was not run on the window. It would open a browser and spend a login, so the stub harness covers the
flow in the module tests.

## AGT2a — does a pinned spawn update itself? It could (2026-09-23)

- [x] **AGT2 — a managed install from the vendor's channel.** 🔴 First measure whether a pinned
  spawn updates itself: Daoris sets no `DISABLE_AUTOUPDATER`.

✅ **done 2026-09-23 — the measurement half; AGT2b (the vendor's channel) stays open.**

Measured with no login and no model: Claude Code 2.1.270, pinned into a scratch toolchain
directory by TOOL2's own `npm install --prefix`, answered `claude doctor` with *Auto-updates:
enabled* and called itself *npm-global*. With `DISABLE_UPDATES=1` it read disabled, refused `claude
update`, and stayed 2.1.270. So a toolchain now declares what a pinned binary runs with
(`PinnedEnvironment` / `pinnedEnv`), and every spawn of the managed binary carries it: sessions and
chats on the pipe door, the SDK's `claude` on the ACP door, and the probe. A binary off `PATH` gains
nothing. FIX-LOG has the entry. Driver 369, CLI 323, modules 97, family 212/212, deploy 39/39.

## AGT1 — one word a person reads: *agent* (2026-09-23)

> *"I have no idea what harness is (so its for deepseek?)"* — the owner.

- [x] **AGT1 — one word a person reads: *agent*,** with the maker named beside each tool.

✅ **done 2026-09-23.**

**The verb.** `daoris harness` is `daoris agent`, the same verbs under it. The old verb is not kept
as a second name: typed, it fails like any unknown command and says `it is \`daoris agent\` now`
(`MOVED` in `cli.ts`). The help, the CLI's own sentences, the driver's refusals (install, login,
pin, *that agent declares no…*, trust, chat) and seven catalogue strings say *agent*. `harness` and
`adapter` stay the code's words, and so do the file keys: a plugin still declares `harnesses`, and
the home still holds `harnesses/`.

**What it is, and whose.** Each toolchain declares `Product` and `Maker` (`product`/`maker` in the
CLI twin, held equal by both twin tables): Claude Code (Anthropic), Codex (OpenAI), dsh (DeepSeek).
The Settings card leads with them, the new-account sign-in names the product, and `agent list`
prints them above the version. A tool that declares neither, a plugin's, keeps its id.

**Seen on the scratch shell**, both themes: *Claude Code* Anthropic, *Codex* OpenAI, *dsh* DeepSeek.

## AGT3 — an account that is an API key (2026-09-23)

> *"daoris can keep the key"* — the owner (D67 §1).

- [x] **AGT3 — an account that is an API key** (D67 §1). Measure each tool first.

✅ **done 2026-09-23 for Claude Code; `docs/2026-09-23-api-key-accounts.md` is the design.**

**Measured first**, on 2.1.280 with an invalid key and nothing spent. `ANTHROPIC_API_KEY` alone
makes `auth status` answer logged in by `api_key`, with no email, and a `-p` run takes it with no
prompt. **Neither checks it**: the first request is a 401 and the tool retries ten times with
growing delays. The measurement's own process outlived its shell, still retrying, and was stopped by
hand.

**Built.** `keys.json` under the home holds the key beside the account, never in the tool's
directory (`HarnessKeys` / `keys` helpers, twin rule 6, both sides reading one literal). The
toolchain's `KeyVariable` carries it at spawn on the one line both spawn paths take
(`HarnessSelection.Environment` → `Apply`). The probe asks with it. The key is shown only as its last
four characters, and reads *unchecked* rather than the tool's "logged in". Removing the account
removes the key. The doors are `key-add` over the bridge, answered by the handle, and `daoris agent
key <agent>`, which reads stdin and refuses a key given as an argument. The rehearsal drives the
terminal door (213/213).

**Seen on the scratch shell**: the form in dark; an invalid key saved; the row read *API key …wxyz*,
then *unchecked* once the pill was corrected; the page's HTML never held the key; *Remove it* took
the key and the directory. **Found and left as items**: the protocol door resolves accounts under
its own name, not its owner's (AGT7), and a bad key costs a session its retries (AGT3b). Codex is
not measured, so it takes no key yet.

## AGT7 — a door runs as its owner's accounts (2026-09-23)

- [x] **AGT7 — a door runs as its owner's accounts**: the driver resolves a door's accounts,
  defaults and keys under the door's own name, not `accountOf`'s.

✅ **done 2026-09-23.** Found while designing AGT3. `accountOf` had been read for grouping and for
a refusal's wording, and never for the account itself, so a Claude Code account never reached
`claude-code-acp` and a Codex account never reached `codex-acp`. `HarnessToolchain.Owner` and
`ownerOf` (twin rule 7) now route the selection, the probe and every account action on a door to
its owner. The selection asks the owner's login question and takes the owner's key variable when
this build carries the owner; the door's pin stays its own. FIX-LOG has the entry. Driver 385,
modules 101, CLI 43 in the toolchain file.

## AGT3b — an account its provider refused is not spent again (2026-09-23)

- [x] **AGT3b — a refused key is news at the first 401,** not after ten retries.

✅ **done 2026-09-23, reshaped by a measurement.** A text-mode `claude -p` with an invalid key was
**silent for 189 s**, then printed `Failed to authenticate. API Error: 401 API key is invalid.` and
exited 1, so the first 401 is not visible from the direct door. Built instead: the toolchain
declares those words (`Refused`, Claude Code's measured phrase, mirrored by the stub). A failed
session whose last lines carry them ends naming the account and the fix, and `HarnessRoster.Refuse`
holds further starts on that account, on either door, until an account action or *look again*
clears it. The rehearsal proves it end to end in one run: one session, then a held start (214/214).
Driver 387.

## AGT4 — an API-driven agent: closed by the owner's decision (2026-09-23)

- [x] ⛔ **AGT4 — an API-driven agent.** Recommended as a plugin's ACP agent; a loop of Daoris's
  own reopens D24, the owner's call.

✅ **closed 2026-09-23 by D67 §2, with nothing to build.** The owner: *"daoris already should have a
loop but not more like higher level, and agent itself should remine its own and access other things
from daoris via mcp"*. Daoris runs no model loop. An API-driven agent is an existing agent on an
API-key account (AGT3), or an agent a plugin declares (D64), and it reaches Daoris over MCP. The same
answers turned AGT5 (the wiring) into the roadmap's MAP arc (MAP1–MAP3).
Dated studies and evidence (the dsh evaluation, the plugin study, the ACP3 probe) keep the old verb,
because they record what was run. CLI 324, driver 369, modules 97, web 555 + 16, family 212/212,
deploy 39/39.

## MAP2 — the workspace topology (2026-09-23)

- [x] **MAP2 — the workspace topology**, a view of its own (`docs/2026-09-23-map-design.md` §1).

✅ **done 2026-09-23.** *Map* on the activity bar and in the palette. It draws the circle's
repositories on a ring, the quests between them as one directed arrow per direction (solid while
any is open, faint once all are closed), and a shared finding as a dashed line. It reads only what
the service already serves (registry, quests, convergence, sessions), so it needs no model and shows
no machine path. `map/topology.ts` holds the model and layout as pure functions. `MapCanvas` and
`MapDetail` are molecules, and the presentational test now covers `map/`. A quest with an end off
the map (an ask, an outside repository) is counted rather than drawn.

**Found on the window, in both themes**: names always below were crossed by the arrow into the
top node, so they now sit outward (`placeLabel`, tested). Arrowheads that scaled with the line
made the chosen line's head twice the size of the others, so they are now sized in the map's units.
Found by the browser gate: a two-pixel curve could not be pressed, so every line now has a wide
invisible hit stroke and a count disc on the line. Web 571 + 17 (the e2e presses the game → engine
line and reads its quests), CLI 331.

## MAP1a — the chain strip (2026-09-23)

- [x] **MAP1 — the workflow**, its first half: how one piece of work was carried, in the quest
  drawer and in Sessions (`docs/2026-09-23-map-design.md` §2).

✅ **done 2026-09-23.** `map/chain.ts` builds the chain from records the page already has: the ask
(by its handle), the parents, the published steps, what the last one will still publish, and every
session under its quest with its agent, version and account. `ChainStrip` is a molecule with a
story. It replaced the drawer's *follows* row and *then* list. The drawer's session section still
shows where things stand now, and is a named region so its tests say which view they mean. The
browser gate walks a real chain: from the step to the quest it follows, and back.

**Found on the window**: a dashed seven-pixel mark was invisible in dark, so it is now a solid
hollow ring. "No session yet" under a closed quest promised a session that would never come, so it
now reads *closed without a session*, with a test. The earlier quest's title was a door with no sign
of one, so it is now faintly underlined. **Split off as MAP1b**: the wiring panel, which needs the
driver. Web 591 + 17.

## MAP1b — what a start runs on (2026-09-23)

- [x] **MAP1b — the wiring a start would take** (`docs/2026-09-23-map-design.md` §2): per workspace
  and job, the agent, account and version, and where each came from, through the driver's own
  `SelectAsync`; desktop-only, on Settings. The intake job joins with INT4b.

✅ **done 2026-09-23.** `HarnessSettings.ResolveFrom` and `ResolveVersionFrom` say which rung
answered, and `Resolve` and `ResolveVersion` are now those, through one shared order.
`HarnessRoster.WiringAsync` reads the account from the first and asks `SelectAsync` itself whether
the start happens and at which version. A test holds the two to the same answer across four shapes
of the wiring file. The bridge route is `STARTS`, named so the page's *Wiring* (the remotes map)
keeps its name. It answers as names only, and a test checks that the answer holds neither the key
nor the home. The answer's type lives in `map/wiring.ts`, beside what draws it, as `work/diff.ts`
does: a molecule may not import the shell module, types included.

**Seen on the window**: ready and held, both themes. *Look again* refreshes the card too. A held
start's sentence ran 190 characters a line across the card, and is now capped at a reading measure.
Driver 396, modules 105, web 601 + 17, family 214/214, deploy 39/39.

## MAP3a — a repository's code map, read and drawn (2026-09-23)

- [x] **MAP3a — a repository's code map, read and drawn** (design §3): `docs/code-map.json` judged
  whole, read from the checkout in local mode, opened from a MAP2 node, laid out in layers; the
  example family carries one.

✅ **done 2026-09-23**, after the contract was written into design §3 (`63ec3ea`). `CodeMapReader`
takes the candidates in order and judges the file whole. It refuses naming the first break: not
JSON, a version other than 1, an id missing, one-line text broken, an id named twice, a dangling
dependency, a path that is not repository-relative, or past a bound. `KnowledgeService.CodeMapAsync`
reads the registered checkout on each ask, and `GET /api/code-map/{repository}` serves it. The page
opens it from a node's detail. `layerModules` lays it out longest-path, and `codeEdge` bows an arrow
that skips a layer out past the column. The example engine keeps a map and the game keeps none.

**Found**: the browser gate found the game's missing map drawing nothing, because the host leaves
a null field out and the view tested `=== null` (FIX-LOG). The window showed the skip-layer arrow
hidden behind the box between, and lit arrows ending in grey heads. Service 361, web 619 + 18,
family 214/214.

## SYNC0d — a newly wired remote syncs without a restart (2026-09-23)

- [x] **SYNC0d — a newly wired remote takes effect without a restart** (`RemoteSyncSet` and
  `RemoteQuestRoutes` are built once, though `RemotesModule` says the loop re-reads the map).

✅ **done 2026-09-23**, the first of D68's build. `RemoteSyncSet.Watching` reads the map on every
pass. A circle whose url and key are unchanged keeps its sync, a changed one is rebuilt with its new
key, a removed one stops, and an empty map is an empty set rather than a null the loop held for good.
Both hosts (the shell's loop and the headless driver) take it through `FromEnvironment`. The two new
tests failed with the per-pass read removed. **Left to SYNC2**: the service's quest relay
(`RemoteQuestRoutes`) is still built once, and SYNC2 replaces that relay rather than mending it.
Driver 398, modules 105, family 214/214.

## SYNC1 — quests as history, locally (2026-09-24)

- [x] **SYNC1 — quests as history, locally**: the operation log, replayed through the transition
  table; the status table as its cache; 48-bit ids.

✅ **done 2026-09-24**, the second of D68's build. Every verb now appends to `quest_log`: the kind,
this store's machine, that machine's next sequence number, the time, and a payload. A publish
carries the whole ask, so a replay can rebuild the quest anywhere, and a move carries its note.
`QuestLog.Replay` folds a history through `QuestTransitions`, the one table, which moved out of the
SQL `WHERE` clause into code. An operation the table refuses changes nothing, so no order of
operations can reach a forbidden state. SYNC2's rebase stands on that. Publish and move take the
write lock before they read (`BEGIN IMMEDIATE`), judge the replayed history, append, and rewrite the
`quests` row from the replay, so the row is a cache by construction. A chain's next step is
published in the same transaction, one sequence number after the close. A refused move writes
nothing. Ids are 12 hex characters with the same hash, so a quest from before keeps its 6 characters
and still answers its own ask.

**Decided while building** (design §2 and §7 now say so): the machine id lives **in the store**,
next to the sequence it numbers. A file of its own would outlive a deleted store, and a restarted
sequence under the same id would reuse numbers a remote already holds. A store from before the log
is **migrated, not rebuilt**, because nothing else holds quests. Each local quest gets its history
from its row, once, however many hosts open the store. Mirror rows get none, since they are their
home's record until SYNC2 replaces them.

**Found, and fixed before landing**: a host holds one connection for all its stores and answers
requests at once, and SQLite does not nest transactions. Once every quest write became a
transaction, 24 publishes and 24 takes run at once failed with *cannot start a transaction within a
transaction*. Before SYNC1 only a chain's close and an index refresh opened one. `ConnectionGate`
now allows one transaction at a time per connection, shared by the quest store and the index store.
A source scan fails any Core file that opens a transaction without it, and it was seen failing on a
planted file. A second probe showed that on this driver, a statement run *outside* a transaction
while one is open **joins it** rather than failing. So every path that wrote nothing commits instead
of rolling back, which would have undone another request's write, and a test pins that driver
behaviour. Service 388, driver 398, modules 105, web 619 + 18, family 214/214, verify green.

## SYNC2 — fetch, rebase, push for quests (2026-09-24)

- [x] **SYNC2 — fetch, rebase, push for quests**, conflicts recorded; replaces the mirror and the
  write-through (fixes SYNC0a, SYNC0e); the rehearsal's two-machine phase rewritten.

✅ **done 2026-09-24**, the third of D68's build. **Every verb commits locally now**: the exchange
lost its relay, `HomeUnreachable` is gone from both refusal enums, and `IRemoteQuestClient`,
`RemoteQuestRoutes` and `HttpRemoteQuests` went with it. The store gained both halves of the sync.
A machine has a cursor, `IntegrateAsync` (which keeps what was fetched, then rebases) and
`PendingAsync`/`AcceptedAsync`. A remote has `OperationsSinceAsync` and `ReceiveAsync`, which judges
each quest on its own: behind, refused or accepted. The remote's number is its log position. A
history replays in the remote's order, then this machine's. A pending move that no longer applies is
rewritten into a `conflict`: what was attempted and its note, carried on the quest in
`Quest.Conflicts`, and pushed like anything else. The replay learned `Applies` and `Step` so the
rebase and the remote judge exactly as it does. The driver moves bytes between five doors, three on
the machine and two on the remote. It copies each operation field by field, goes round at most three
times when a quest is behind, and reports conflicts and refusals as notes. A tick syncs again after
anything concluded. The contract is sync design §8.

**Decided while building** (design §8 says each): **what is pushed follows the receiver.** A quest
leaves a machine only when its `to` is joined in that circle, locally or as a teammate's rootless
row. That is D47's *home follows the receiver*, kept as a disclosure rule, and it fixes SYNC0e,
because an ask has no row and pushes by its receiver. **Neither door carries a workspace**: the
receiving side files a publish by its own wiring, which dissolves SYNC0a. **Two things a rebase
drops**: a second publish of an ask the remote already holds, and a follow-up that only a lost close
published. **The mirror is gone**: its rows are deleted on open and `home` is dropped, so the first
fetch rebuilds them from cursor zero. **The chain rule stays under a new reason**: a chain is all
shared or all local, because a step is published on whichever machine closes the one before it.

**Found**: the table allows done from taken whoever took the quest. A machine whose take lost can
still close the quest over the winner's take, and the remote accepts that as a fast-forward. It is
SYNC3's ("a losing session stopped"), and the SYNC3 row now says so. Phase 12 of the rehearsal also
leaned on the relay (a deployment holding a quest the instant it was published), and now syncs
first. **The rehearsal's phase 11** proves the online race (a machine seeing a take first spawns
nothing), the offline race (the same ask published on both machines is one quest, and the second
take becomes a conflict every machine holds), and a remote that is down (verbs commit, the tick
names the wall, and the next tick pushes). It also proves the closure reaches the remote within the
tick that made it, which is the only check on the tick's second sync. Service 381 (28 relay and
mirror tests removed, 21 added), driver 400, modules 105, web 619 + 18, family 220/220, deploy 39/39,
verify green.

## SYNC3 — claim by push (2026-09-24)

- [x] **SYNC3 — claim by push**, unconfirmed takes offline, a losing session stopped.

✅ **done 2026-09-24**, the fourth of D68's build. It is built as **D69**, which the owner chose over
the design's first wording. The sync design's §4 had the driver take the quest before spawning, which
is the alternative D46 rejected: Taken has no way back to Open, so a spawn failing after the take
would strand the quest. **The take itself claims by push instead**:
- A take on a shared quest commits locally, runs one sync pass, and answers from this machine's
  claim afterwards (`QuestStore.ClaimAsync`).
- Held: *the remote confirmed the claim*.
- Lost: the existing *already taken — stand down*, before any work.
- Anything else: taken, **UNCONFIRMED**, with the wall named.

So the session is still the claimant, driven or not.

**The quest sync moved into the hosts** so the take and the tick run one implementation:
- `QuestSync` is the pass. `IQuestRemote`/`HttpQuestRemote` is its transport, and the service opens
  this one socket and still spawns nothing.
- `QuestWire` is the single wire, spoken by the remote's doors and by the host's client.
- The driver's quest pass is one call to `POST /api/quests/sync?workspace=`. The three byte-moving
  local doors from SYNC2 are gone.
- **A losing session is stopped.** The tick syncs beside running sessions every `pollSeconds`, asks
  `GET /api/quests/{id}/claim` for each, and stops one whose take lost. The session ends
  `stood-down`, with the reason on the record and `ByPerson` false.
- **The rebase learned D69's rule**: once a machine's take has lost, its later pending moves on that
  quest are conflicts too. That closes the gap SYNC2 found, where an offline loser's finished close
  fast-forwarded over the winner's take. A test failed before the rule and passes after it.

**Found**:
- The rehearsal's stub stood down on the words "already taken", which the lost-claim sentence does
  not contain. It now reads the 409, because wording is written for a person.
- The lingering-session proof was cut off twice by restarting machine b's host while the stub's
  take was still in flight. First the gate waited only for the session to spawn. Then it waited for
  the take to commit, but the host answers only after its failed push. The gate now waits for the
  stub's own transcript to say it is lingering.
- One `HookTests` failure appeared in a run under heavy load (a 2m20s suite that normally takes
  28s). It did not recur in five runs and SYNC3 does not touch hooks. It is recorded here as one
  sighting, per TEST1's rule.

Service 387, driver 399, modules 105, web 619 + 18, family 225/225 (three runs), deploy 39/39,
verify green.

## SYNC4 — session records both ways, by cursor (2026-09-24)

- [x] **SYNC4 — session records both ways, by cursor.**

✅ **done 2026-09-24**, the fifth of D68's build. **Up by cursor.** Every write to the session
store takes the next `revision`, computed inside the one statement so SQLite's write lock keeps it
monotonic. A push sends this machine's own records (no `origin`) changed since the workspace's
`pushed` cursor, for joined repositories only. So a record crosses when it changes, not every tick.
**Down by cursor.** The remote's new `GET /api/sessions/since` returns records after a revision,
leaving out the caller's own (its key says which), and a machine files them in its sync's circle,
keyed `origin/id`. **The pass moved into the host** beside the quests (D69's reasoning):
`SessionSync` in Core, `SessionWire` as the one shape for the feed and the fetch, and the transport
renamed `IRemote`/`HttpRemote` now that it carries both. The host's door is `POST /api/sync`. The
driver no longer feeds records, and its payload builder went with the feed.

**Read-only, and never this machine's lock** (D47 §6):
- The ledger's `ActiveForAsync` ignores records with an origin. A mirrored record has no tree, and
  the lock reads a treeless record as holding every tree, so a teammate's working session would have
  locked the repository here.
- The driver's snapshot and strikes skip `origin/id` records, so a teammate's session spends none
  of this machine's cap and parks none of its quests.
- The Work view offers no moves and no composer on a teammate's session. That test failed before the
  fix, which I confirmed by stashing it.

**An upgrade derives what it can**: a mirrored row's origin from its id, and each row's revision
from its insertion order. So a store from before the sync pushes its own records and knows whose is
whose.

**Found**: `sessionOrigin` already existed in the web (`origin/id` means *elsewhere*), so the id is
the one rule the web and the driver share. Not looked at on the window: the scratch machine has no
remote, so no teammate records.

Service 395, driver 397, modules 105, web 620 + 18, family 226/226, deploy 39/39, verify green.

## SYNC5a — knowledge and the code map by ancestry (2026-09-24)

- [x] **SYNC5a — knowledge and the code map by ancestry**, a content digest for the same commit
  (fixes SYNC0c; carries MAP3b). Design §8.

✅ **done 2026-09-24**, the sixth of D68's build. **Ancestry is asked where the checkout is.** Before
it feeds, the driver reads what the remote holds (`GET /api/feed/held`) and asks git how its commit
stands to it (`WorkingTree.RelationAsync`: `cat-file -e`, then `merge-base --is-ancestor` both
ways):
- It descends: the feed names the held commit as its `base`.
- Diverged: no base, and commit time decides at the door.
- Behind, or the held commit is not in this checkout: nothing is fed, and a note says a pull or a
  fetch.

**The door checks the base the way a compare-and-swap is checked.** `FeedOrder.Judge` in Core takes
a feed whose base is still held, whatever its clock says, and answers *moved* when another machine
fed in between. The judgement and the write run under one gate.

**The same commit is compared by digest** (`FeedDigest`, SHA-256 over the normalized entries in
identity order, every field length-prefixed). It is computed at the deployment, never taken from the
wire. Same content: *already held*, and the first feeder stays credited. Other content: the first
reading stands, as information (SYNC0c). A row from before digests takes the same commit once.

**A feed speaks for a commit, so only a clean checkout feeds.** The host's index and map describe
the working tree, and a dirty one would send work in flight under the commit's name. The rehearsal's
own fixture was that case: `declareJoin` edited three manifests and never committed them. It commits
now, as a reviewed declaration is.

**MAP3b rides the same judgement.** `POST /api/feed/code-map` takes the file's text, judges it whole
again with `CodeMapReader`, and keeps it in canonical form (`CodeMapReader.Write`) in
`fed_code_maps`, at its own commit. A newer commit with no map keeps the row with no body, so an
older checkout cannot bring the map back. `CodeMapAsync` answers from the fed store for a repository
with no checkout here. Retire removes the fed map with the provenance. The remote's held value
becomes a git argument, so the driver takes it only as a hex commit id.

**Left open**: a teammate's fed map lives at the remote only, so a machine without the checkout
still answers "no map" for it (MAP3e).

Service 408, driver 410, modules 105, web 620 + 18, family 235/235, deploy 39/39, verify green.

## SYNC5b — registrations updated and removed, retire as a tombstone that travels (2026-09-24)

- [x] **SYNC5b — registrations updated and removed, retire as a tombstone that travels** (fixes
  SYNC0b). Design §8.

✅ **done 2026-09-24**, the seventh of D68's build. **Up, by ancestry.** A registration names the
commit its manifest stands on, and the held commit git said it descends from, exactly as a feed does.
It names a commit only while `daoris.json` itself is unmodified (`WorkingTree.UnmodifiedAsync`),
whatever else is in flight, because the declaration is the manifest. A shared deployment holds it in
`registration_provenance` and judges it with `FeedOrder`, the digest over the declaration
(`FeedDigest.Of(Registration)`). Three rules are the registration's own
(`KnowledgeService.RegisterFedAsync`):
- The first registration is taken from any line and without a commit, because nothing else of a
  repository can travel until it is registered.
- After that, only the line the arriving declaration calls canonical is taken, so a renamed default
  branch does not lock the repository out.
- One naming no commit does not replace one that did. That is the new refusal `Unordered`, reported
  as information.
A local host's registration is this machine's own and is never ordered. The held door answers
`registration`. The driver asks git once per held commit, and one sentence names everything waiting
on it: "registration, knowledge and code map are held at …".

**Down, updated and removed.** `RemoteSyncPayloads.Mirror` replaces the once-only copy. A team row is
written when it is new or its declaration changed. A copy this circle no longer lists is retired
through the host's own door. A row held with a root in any circle is never touched, and neither is a
copy another circle's sync keeps. A pass now runs while this machine holds a joined checkout, a
teammate's copy, or a retire it owes, so a machine whose last checkout left still hears from its
circle.

**A retire is a tombstone that travels.** SQLite triggers on `registrations` write `registry_retired`
in the same statement that ends the row, so no door can retire a joined checkout and forget the
circle:
- A joined row with a root is retired, re-wired to another circle, or re-registered unjoined.
- Joining that circle again deletes the tombstone.
- A rootless (teammate's) row records none.
The driver reads `GET /api/registry/retired`, retires the repository at the circle only where the
circle still lists it, and clears the tombstone. One owed for a repository joined here again is void.
The deployment keeps no tombstone of its own, so another machine that still holds the checkout and
the join registers it again. That is correct. The retire door's sentence used to say only "on this
machine", which was no longer the whole truth. On a local host with a remote for that circle, a
joined checkout's retire now adds that it leaves the circle's deployment too, on the next sync.

**Found**: the triggers name three columns an old store only gains through the migration, so they
are created after it. SQLite fires `AFTER UPDATE` triggers on an upsert's `DO UPDATE` path, which
the re-wire and unjoin tests prove. **The rehearsal's two-machine phase proves it end to end**:
- a revised declaration goes up at its commit and down to machine a's copy;
- a declaration from an older commit is information;
- a retire undone before any pass owes nothing;
- a retire that stands leaves the remote and machine a;
- joining again registers the repository afresh.

Service 420, driver 424, modules 105, web 620 + 18, family 243/243, deploy 39/39, verify green.

## SYNC6a — where a circle stands, and the terminal door (2026-09-24)

- [x] **SYNC6a — where a circle stands, and the terminal door.** The first of SYNC6's three
  landings; SYNC6 was split into a, b and c on 2026-09-24 (design §9).

✅ **done 2026-09-24**, the eighth of D68's build. **Every pass records how it ended**, in the store's
`quest_passes`. `QuestSync.RunAsync` writes it however the pass ended, so a take's pass and a tick's
are both recorded. A pass that reached the remote moves `synced`. One that hit a wall keeps
`synced`, moves `tried`, and names the wall. Being in the store, it survives a host restart.

**Where a circle stands** (`QuestStore.StandingAsync`):
- ahead: the pending operations, read through the same *what may leave* predicate the pass uses, so
  a quest to a local receiver is never ahead of anything;
- behind: the quests the last pass could not bring level;
- the quests carrying a conflict, from the cached `conflicts` column;
- the last pass.
A local host answers it at `GET /api/sync?workspace=` without reaching the remote. A circle with no
remote here answers `wired: false` and nothing else.

**The terminal door** (`SyncConsole`):
- `daoris-driver sync [--workspace <name>]` runs the tick's own pass: the feed up, the retires, the
  team's rows, then the host's pass. It then prints each circle's standing, and exits 2 when a pass
  hit a wall.
- `sync status` prints the standing, and reaches no remote.
- Naming a circle with no remote is refused by name (`RemoteSyncSet.RunOnceAsync(workspace)`).

**Decided while building**, both in design §6:
- *Behind* is the quests the last pass could not bring level. A pass fetches and rebases in one
  step, so nothing is ever "fetched, not yet seen", and the time of the last sync says how old that
  knowledge is.
- There is no `pull` or `push`: the tick runs the whole pass within seconds, so holding back either
  half would be undone at once.

**Found**: the web renders no conflict anywhere, although every quest answer carries them. SYNC6b
shows them on the quest as well as counting them. **The rehearsal's two-machine phase proves the
door**: with the remote down, `sync` exits 2 naming the wall and `sync status` shows work ahead and
the wall as the last try; the losing machine lists the conflicted quest; `sync` pushes a quest
before any tick does; and a circle with no remote is refused by name.

Service 424, driver 427, modules 105, web 620 + 18, family 248/248, deploy 39/39, verify green.

## SYNC6b — the screen door (2026-09-24)

- [x] **SYNC6b — the screen door**: the status bar's sync item, read from `GET /api/sync`; *Sync
  now* over the bridge, running the pass `daoris-driver sync` runs; the conflict list, each quest
  linked. Design §6, §9.

✅ **done 2026-09-24**, the ninth of D68's build.

**The sync item** (`work/SyncStatus.tsx`, a molecule with props only) takes the remote item's place
in the status bar when the circle is wired. It is icon and number:
- ↑ for work waiting to go up, ↓ for quests left behind, ⚠ for quests in conflict;
- `synced` when level, `not synced yet` before any pass, `unreachable` after a wall.
Pressing it opens a non-modal menu above the bar. It shows when the circle last synced, then the
host's wall verbatim, then the quests in conflict (each opens its drawer, with its title where the
page holds it), then *Sync now* and *Remotes…*. `STATUS_PRESSABLE` is exported from `frame.tsx`, so
this item and the plain ones are one control.

**Wiring**:
- The standing is `useSyncStanding` over HTTP from this machine's host, so a browser here reads it.
  Its `wired` field now decides the remote item.
- The circle is the one chosen, or the only one there is. "Every circle" with several has no single
  standing, so nothing is asked. `scope.workspace ?? 'default'` asked a door for a scope nobody had
  chosen, and named the wrong circle in a family whose one circle has another name.
- *Sync now* is `DAORIS.DRIVER` `SYNC_NOW`, which runs `DriverLoop.SyncNowAsync` through the loop's
  own set. A browser is not given it.
- Every tick invalidates the standing.
- `RemoteSyncSet` takes one pass at a time, because the tick and *Sync now* share it and a pass
  re-reads the map into its own dictionary.
- The quest drawer shows each conflict, with the machine, what it attempted, and its note verbatim.
  `QuestsView` takes a `focus` door, consumed by identity (frontend §4b).

**Found by looking at the real window** (FIX-LOG 2026-09-24), against a throwaway shared host
wired into the scratch machine and then stopped:
- A wall in the driver's feed returned before the host was asked for its pass, and the host is where
  a try is recorded. So the bar said `synced` while *Sync now* was failing. The host's pass now runs
  whatever the feed met.
- A circle with nothing joined got no pass at all, so *Sync now* answered Clean without contacting
  anyone. Every wired circle now gets its pass (design §6).
- A glyph alone did not say the remote was unreachable, and the lead-in repeated the host's sentence.
  Both are fixed and recorded in platform UX §4.
- My own trap: the throwaway remote runs the same host build, so it held the build's assemblies. A
  rebuild was blocked, and a filtered build log hid it.

**Found in the tests**: Radix opens a `defaultOpen` menu only once per file under jsdom, and the
shared `userEvent` carries pointer state between tests. The tests use AppMenu's harness (a fresh
`setup()`, then the keyboard). The stories keep `defaultOpen` for review.

Service 424, driver 427, modules 106, web 639 + 18, family 248/248, deploy 39/39, verify green.

## SYNC6c — dismissing a conflict (2026-09-24)

- [x] **SYNC6c — dismissing a conflict**: the conflict list's action, an operation that travels and
  changes no status. Design §5, §9.

✅ **done 2026-09-24**, the last of D68's build. **The arc is closed.**

**A conflict has a name every machine knows.** When the rebase turns a move into a conflict, the
move keeps its machine and sequence. So `QuestConflict` carries the `Sequence`, and the quest answer
carries it too.

**A dismissal is an operation.** `QuestOperationKind.Dismissed` names its conflict
(`QuestOperationRef`), and it rides the log, the payload and `QuestWire` like any other. On the wire,
a dismissal that names nothing is half-made and does not cross. `QuestLog.Applies` takes it for any
quest there is, and `Step` removes the named conflict and moves no status. So two people dismissing
one conflict make one dismissal, never a refusal at the remote or a new conflict in the rebase. The
rebase's rule for moves after a lost take reads only status moves, so it leaves a dismissal alone.

**The doors**:
- `QuestStore.DismissAsync` dismisses the conflict named, or every one the quest carries when none
  is named.
- `POST /api/quests/{id}/conflicts/dismiss` works in both modes, beside `respond`. It is not a
  `respond` action, because every one of those moves the status.
- The drawer shows *Dismiss* beside each conflict, and stays open on the quest as it now stands.
- `daoris-driver sync dismiss <quest>` is the terminal form (D50).

**An old cache gains its names.** Conflicts cached before this carry no sequence, so they could not
be named. The store replays those quests from the log as it opens (`RecacheUnnamedConflictsAsync`),
because the log has always kept the sequence.

**The rehearsal proves it end to end.** Machine b dismisses the offline race's conflict from a
terminal. After one pass on each machine, it is gone on b, on the remote and on a, and the take
stands. **Looked at on the real window**, in both themes. One conflict was seeded into the scratch
machine's store, which was backed up first and restored after. The section sits above the body with
the "needs a person" edge, and *Dismiss* toasted the service's sentence and removed the section.
The quest's status did not move.

Service 430, driver 427, modules 106, web 640 + 18, family 249/249, deploy 39/39, verify green.

## MAP3c — a tool producer (2026-09-24)

- [x] **MAP3c — a tool producer**: the devkit writes the file from project references and package
  dependencies.

✅ **done 2026-09-24**, built by a parallel agent in its own worktree and integrated here.
**`daoris-devkit map`** writes `docs/code-map.json` from what the project files declare, with no
compiler:
- every tracked `*.csproj` is a module, with its file name as the id, and each `ProjectReference` is
  a `project` dependency;
- every tracked `package.json` below the root is a module named by its package, and a dependency on
  another of the repository's packages is a `package` one;
- the root `package.json` is the repository itself;
- a reference the repository does not track is reported, not drawn.

**What a person writes is kept.** A summary is the project's own description, or otherwise the line
already in the map. A dependency of any other kind (`http`) stays while both its ends are modules.
The tool refuses to write what the reader would refuse (an id twice, the bounds), and to rewrite a
map it cannot read. It writes where the reader reads, the same way every time.

**`map --check` gates as a declared gate** (D54: a fact). It is not a universal gate, because a map
another producer wrote is not the devkit's to judge. Daoris declares it, the release workflow runs
it, and Daoris keeps its own `docs/code-map.json` this way, with summaries and three `http` edges
written by hand and kept.

**The twin.** The devkit restates the reader's rules. A devkit test holds them to `CodeMapReader`'s
source, and a service test judges Daoris's own map with the reader. Both were watched failing.

**Found**: the devkit's universal `sensitive` gate is already red on main, over placeholder home
paths in five test fixtures. That predates MAP3c and is left to a follow-up once the parallel work
has merged.

Devkit 73 (was 59), service 431, the code-map gate fresh, verify green. Nothing here binds a port,
so no rehearsal moved.

## MAP3e — a teammate's code map on this machine (2026-09-24)

- [x] **MAP3e — a teammate's code map on this machine.** MAP3b holds a fed map at the remote only, so
  a machine without the checkout answers "no map" for a teammate's repository that keeps one. Bring
  it down with the sync, or say where it lives.

✅ **done 2026-09-24**: the map is brought down, not pointed at. It was built by a parallel agent in
its own worktree and integrated here.

**The host's pass carries it** (`CodeMapSync`), beside the quests and the records. It covers each
repository of the circle this machine holds only a teammate's copy of. Pulling a map needs no git, so
it is the host's (D69), and the host is what answers the page.

**The remote's holding is the order.** A shared deployment already took each map by ancestry, so this
machine holds what the remote holds, at the commit it holds it:
- a newer commit replaces the map here;
- a commit that keeps none is held with none;
- a map the remote no longer holds is forgotten.

The commit is asked first (`GET /api/feed/held`), so an unmoved map is not fetched again. A checkout
here is the authority on its own map and is never asked for, and another circle's copy is that
circle's.

**One wire.** The code-map door answers in Core's `CodeMapWire`, which the host writes and the pass
reads, as the quest doors have `QuestWire`. It leaves out what it does not have, as the host always
did, and carries `fed` (the commit, its line, the key that fed it) when no checkout was read. The pass
judges what it reads whole again before holding it. The page says where a teammate's map came from,
and a held commit that keeps none says so by that commit.

**Rejected**: saying where the map lives instead of bringing it. A machine would stop answering for
its circle whenever it is offline, while the team's rows and quests already come down.

**The rehearsal** gives the newcomer a code map and checks that machine b answers it, fed at the
commit the remote holds, with no path in the answer. The attribution line on the page is covered by
its story and its unit tests. It was not looked at on the real window, because the scratch machine
has no teammate.

Service 439, web 643 + 18, family 250/250, deploy 39/39, verify green.

## INT4c — the desktop door for asks (2026-09-24)

- [x] **INT4c — the desktop door.** The screen twin of `daoris-driver ask` (D50): a composer at the
  status bar's workspace scope, with the quest composer's drop, paste and links, and the ask's
  record (the proposal, the tier, its quests, *publish to…* and *close*).

✅ **done 2026-09-24**, built by a parallel agent in its own worktree and integrated here. Quests now
leads with *Ask*, and the palette offers *Ask the circle…*.
- **The door is in Quests, not on the status bar.** The bar states what is true, and an ask is an
  action. The ask is made in the circle the bar names: the scoped one, or the only one held. With
  several and none chosen, the composer asks which, and never assumes `default`.
- **The asks sit above the quests** as their own group, because quests come from asks and a
  proposal waits on a person.
- **One carry for both composers.** The quest composer's links, drop, paste and chooser became one
  shared molecule (`compose/carry`), so the two cannot drift on how a file arrives.
- **The record** says which tier answered and offers each proposal, and any other adopter in the
  circle, as a publish. It opens the quests the ask became, names its files without their path, and
  closes with a reason.
- **A browser on this machine has the same door**, because an ask goes through the local host's
  HTTP, not the bridge.

**Found while building**:
- A scoped page asked for the whole registry, which the scope test caught.
- A just-published quest's door opened nothing until the list refetched. It is now a plain `#id`
  until the page holds the quest, seen failing first.

**Looked at on the real window**, light and dark: the header's *ask* primary, the composer (*Asked
in default*), a real ask proposed by declarations (the service's sentence as the toast), the card in
the new group, the record with both proposals and *publish*, and the close flow with its reason.

**Left for later**:
- If the palette's *Ask* is used while the quest composer is open, two drawers can stack.
- Overview does not list a proposed ask, although one waits on a person. That is filed as INT4d.

Web 684 + 19 Playwright, verify green.

## INT4b — the intake session (2026-09-24)

- [x] **INT4b — the intake session.** The room under `<home>/intake/<workspace>/`, seeded with an
  `AGENTS.md` of the circle's declarations; `intakeAdapter` in `driver.json`; an ask with a harness
  runs a session there that decides from the declarations, publishes the quests (chains included)
  onto INT4a's ask, and asks the person rather than guessing. Proven by a stub intake agent; a real
  ask is the owner's.

✅ **done 2026-09-24**, built by a parallel agent in its own worktree and integrated here. The design's
§1b *as built* and D65's amendment carry the reasoning.
- **Off until a harness is named.** `daoris driver intake <adapter>|off`, the bridge's `SET_INTAKE`,
  and `intakeAdapter` are modelled in all three places a driver setting lives, so no other toggle
  deletes it. Absent means the declarations tier alone. This departs from the design's "defaulting
  to the machine's adapter": each intake spends a login, and an upgrade should not start doing that
  silently.
- **A chat for the ask.** `POST /api/sessions/intake` goes to `SessionLedger.OpenIntakeAsync`. The
  session's repository is `ask #<id>`, its circle is the ask's, its tree is the room, and it has no
  base commit. There is one intake per ask, and the room is held only while a process runs.
- **The room** is `<home>/intake/<circle>/`, holding `AGENTS.md` of the circle's declarations,
  `CLAUDE.md` (`@AGENTS.md`), and a `.claude/settings.json`. That file allows only the family's read
  tools, `quest_list`, `quest_publish` and `WebFetch`: no shell, and no writes except through the
  service. It is under Daoris's home, never in a repository (D32).
- **It publishes as the ask.** The spawn and its connector carry `DAORIS_ASK_ID` and
  `DAORIS_SESSION_ID`. The MCP host's `quest_publish` and the HTTP publish door take the intake's
  draft and chain. The tier becomes `intake` only for the ask's own session. A drafted body puts the
  intake's words first and quotes the asker's beneath, and the ask's links and files always travel.
- **It asks the person by parking.** A clean exit that published nothing parks the session
  `awaiting-person`, with a note naming both answers. The question stays on the transcript and never
  travels. The person's publish or close of the ask ends the session at the next tick.
- **The loop** takes asks after the quest plan, in the slots the quests leave, the oldest per circle
  and one per circle per tick.

**The rehearsal's intake phase**, six checks, was written in the worktree and first run here, and
passed. Covered: an ask answered by a session in the room, the room holding the declarations, the
publish made as the ask with its chain driven, the park when the declarations do not settle it, and
the person's answer ending it. **Not covered by any gate**: a real harness running an intake, the
MCP host publishing as the ask under a real harness's environment, and the room's trust hold. Those
are INT4f, the owner's. The screen's words for the `intake` tier and the record's link to its
session are INT4d.

Service 448, driver 447, modules 107, CLI 334, web 684 + 19, family 256/256, deploy 39/39, verify
green.

## AGT2b — a managed install from the vendor's channel (2026-09-24)

- [x] ~~**AGT2b — a managed install from the vendor's channel:** Claude Code's release bucket against
  its signed manifest; Codex's package at an exact version, by its published hashes. The channels
  were checked on 2026-09-24: `docs/2026-09-24-agt2b-channel-evidence.md`.~~
✅ **done 2026-09-24** — `agent pin` fetches Claude Code from the release bucket (a manifest verified
against its detached OpenPGP signature under the pinned key, its signed version read, then the
binary's SHA-256) and Codex from its release package (the SUMS file against the metadata, two
published hashes that must agree, unpacked whole); npm stays for the ACP adapters and dsh. Before
2.1.89 is refused by name with no npm fallback. The verifier and the tar reader have no dependency and
are tested against the vendors' own files. The desktop pins Claude Code the same way. Design as built:
toolchain design §3a. Not covered: a real download (no test touches the network) — AGT2c.

**Built in a parallel worktree and cherry-picked.** The order of trust is the point: the signature
under the key Daoris carries, then the version the manifest signed (a real old manifest served as a
new one fails here), and only then the hash the manifest names. Codex has no signature to check, so
its two published hashes must agree with each other before either is trusted for the download. An
install is staged beside its version and moved into place once verified, so `bin/<binary>` existing
is the proof and a re-pin downloads nothing. The network stays in `service.ts`: the dispatcher hands
`agent pin` a fetcher, and a dogfood test holds that nothing judging a download imports a network
module. Codex gets no update switch because the evidence says a binary outside its own layout takes
no update action — measured by nobody yet, which is AGT2c.

🔴 **Its red phase ran the real installer**: before the channel route existed, the failing tests sent
`pin claude-code|codex` down the old npm route, which ran `npm install` three times from a unit test
(~0.9 GB in `_fixtures/`, deleted; the packages remain in the machine's npm cache). FIX-LOG has the
trap. Integrated here: CLI 382 (was 334), driver 462 (was 447), modules 107, release rehearsal 56/56,
family 256/256, deploy 39/39, and the universal gates with the private list saw the five vendor files
clean.

## SEN1 — the devkit's sensitive gate was red, and nothing a session runs ran it (2026-09-24)

- [x] ~~**SEN1 — the devkit's `sensitive` gate is red on this repository.** `daoris-devkit verify
  --universal-only` flags placeholder home paths (`/home/…`, `/Users/…`) in five test fixtures:
  `home.test.ts`, `AdapterTests.cs`, `HookTests.cs`, `QuestExchangeTests.cs` and
  `QuestsView.test.tsx`. Use the neutral `C:/somewhere/…` convention, then find which gate list let a
  red universal gate pass, since the release workflow is meant to run them. Found by MAP3c,
  2026-09-24. Held until the parallel INT4b/INT4c branches merge, because both may touch these files.~~
✅ **done 2026-09-24** — six fixtures, not five: INT4b's `IntakeTests.cs` had joined them. Every
placeholder is `C:/somewhere/…` now, and the Daoris home's is `C:/somewhere/data`, which is where
D63 put it. `npm run verify` runs the universal gates last, and a dogfood test holds the row there.

**Which list let it pass: none of the ones a session runs.** The universal gates were declared in
`daoris.gates.json` and run by the release workflow (DEVKIT3), which is dispatched by hand and has
not been dispatched since the fixtures landed (four commits, 2026-09-23). The workflow's comment
said the private half "runs locally and in the pre-commit hook". No hook is installed here: DEVKIT3
left `install-hooks` to the owner, because it changes how the owner's own commits behave. So the
scan ran only when someone remembered to. Three of the six were not Unix homes at all. They were
`D:/home/…` standing for the Daoris home, and the pattern `/(?:home|Users)/…` has no way to know
that. That is the same reason `C:/somewhere` is the convention.

**`verify` carries `--allow-builtins-only`**, which is not a weakening here. The flag only matters
when the private list is absent. Locally the list is present and the scan runs all 15 patterns. On a
runner it is absent, and the workflow's own step already passes the same flag. Without the flag, the
workflow's *Verify gate* step would fail closed on every run. **Watched failing**: one fixture
restored to its old Unix-home placeholder turned `npm run verify` red (exit 1), naming the file. The workflow
comment now says what is true. Installing the hook remains the owner's call.

CLI 383 (was 382), driver 462, service 448, web 684, and the universal gates are 5/5 with the private
list.

## INT4d — an ask that waits on a person is outstanding, and its record says who answered (2026-09-24)

- [x] ~~**INT4d — an ask that waits on a person is outstanding, and its record says who answered.**
  Overview's *What needs you* lists sessions and quests only. A proposed ask, or one whose intake
  parked asking, is the person's to settle, and neither shows there. On the ask's record, the tier
  INT4b added (`intake`) has no words in either catalogue (`asks.tier.intake`,
  `asks.tierShort.intake`), and the intake session is not shown or linked. Found by INT4c and INT4b.~~
✅ **done 2026-09-24** — `needsAPerson` takes the asks. A live ask waits unless its intake is busy,
either as a *proposal* or as *its intake asked you*. A parked intake is counted once, as its ask, and
stays a parked session only while its ask is not in hand. Asks sit between parked sessions and
quests nobody can take, and the Sessions badge follows them. A band row is a door only where its
destination exists: a parked session opens in Sessions (desktop only), an ask opens its record in
Quests, and a quest nobody can take opens its drawer, which a browser now has too. A row with
nowhere to go is text. The record gains an intake-session section: a door into Sessions on the
desktop, named without one in a browser, and by id when unloaded. The tier has words in both
catalogues, held from the service side (`AskTierCatalogueTests`). Decisions are in intake design
§1h and platform-ux §4.

**Built in a parallel worktree and cherry-picked. Its Playwright check was first run here, and it
passed. The window then found two defects.** The band went stale: with the intake off, a tick never
reads asks, so an ask made by the other door moved nothing a tick reports. The page was never told,
and the ask's parked intake read as a bare session. The shell now forwards a tick when
`Asks.Signature` changes, and the page refetches the asks on every tick (FIX-LOG). The record also
said "no intake harness ran" directly above the intake that ran, because a parked intake published
nothing and the tier stays the declarations'. It now says so in its own words. Looked at in both
themes and 中文.

**Not done: INT4g.** Sessions offers a parked intake the parked-session moves, and each one ends
the record without answering the ask. Web 684 → 733 with AGT6, service 448 → 451, driver 464,
Playwright 21.

## AGT6 — Daoris's own AI on the Settings page (2026-09-24)

- [x] ~~**AGT6 — Daoris's own AI on the Settings page:** each job, its tier, and how to change it.
  The intake is one job since INT4b. `daoris driver intake <adapter>|off` and the bridge's
  `SET_INTAKE` both exist, but no screen control does (D50).~~
✅ **done 2026-09-24**, built in a parallel worktree and integrated here. The agents direction §3
*as built* and platform-ux §4 carry the reasoning.
- **A card for everyone**, *Daoris's own AI*, between *Appearance* and *This machine*. Search and
  convergence shows `/api/status`'s tier and note verbatim. How to change it is a sentence: the
  service reads `DAORIS_EMBED_MODEL`/`DAORIS_EMBED_URL` when it starts. **Rejected**: a settable
  home file, which would be a second source for the deployment's choice.
- **The intake is the desktop's**: the screen's half of `daoris driver intake` over `SET_INTAKE`. It
  offers *Off* and each way in the machine has installed, always shows the agent in effect, and gives
  one line per circle naming the account an intake there runs as.
- **One answer, not two**: `STARTS` answers an `intake` row after each circle's `work` row, from the
  same `WiringAsync`. An unknown agent is a held row in the driver's words. *What a start runs on*
  names each row's job once there are two.
- The status bar's tier leads to the card.

**The window found the control missing** while the intake was off. The bridge leaves a null out,
and the page tells an older shell by the field's absence, so off read as older. Off is `""` on the
wire now (FIX-LOG). Both test doubles had kept the null, the module helper's serializer and the
page's mock, which is why nothing failed. Looked at in both themes with the intake off: the tier
pill, the hint, the select and its options, and the status-bar door. **The on state was not looked
at**, because choosing the machine's one agent with asks waiting spawns a real session on a real
account. Its rows are held by vitest. Modules 109, web 733 + 21, and the Playwright check (a browser
is told the tier verbatim, and nothing of the intake) passed on its first run here.

## INT4g — a parked intake in Sessions leads to its ask (2026-09-24)

- [x] ~~**INT4g — a parked intake's moves in Sessions answer nothing.** Sessions offers a parked
  intake the three parked-session moves (finish, decline, stop). Each ends the record without
  publishing or closing its ask, which falls back to a proposal. The answer is on the ask, so the
  surface should say so or lead there. Found by INT4d.~~
✅ **done 2026-09-24**. A parked intake offers **answer ask #id**, a door to the ask's record in
Quests (App's `openAsk`, the band's own door), and **stop it**, which says the ask then stays a
proposal. Finish and decline are gone: finish wrote `completed`, which §1b reserves for an intake
that published, and decline had nothing to decline. A parked intake gets no composer, because an
intake is one turn with no process left. Sessions names it for what it serves: *intake for ask #id*,
the rail kind *intake*, *ask* and *room* in the head, and no busy claim on the rail for a parked
one. The driver needed nothing: its tick already ends a parked intake whose ask was answered, and
`IntakeTests` hold that. Decisions are in intake design §1h and platform-ux §4.

Built in a parallel worktree and cherry-picked. **Looked at on the real window** in both themes: the
rail, the card, the head and no composer. *Answer ask #0fda18* opened Quests on that ask's record,
which also showed the window fix's tier line (*by declarations; an intake read it and has not
published*). *Stop it* was not pressed; it is the existing parked-session move. **Filed: INT4h**, a
*running* intake still offers a message box whose words reach a one-turn process. Web 733 → 751,
Playwright 21, verify green.

## MAP3d — the agent producer: a code map kept current by the session prompt (2026-09-24)

- [x] ~~⛔ **MAP3d — the agent producer.** The owner's call first (design §3): canon skill, or the
  session prompt (recommended).~~
✅ **done 2026-09-24** — the owner chose the session prompt. A driven quest's session is asked to
keep the repository's code map current exactly when its tree keeps one. The map is found where the
reader looks (`CodeMapFile`, whose candidates a test holds to the reader's source). One paragraph
after take-work-close names the file and asks for the map to move in the same change when a module
is added, removed, moved or rewired: with the repository's own tool where it has one, otherwise by
hand in the reader's shape. A repository with no map is not asked to start one, and a conversation
and an intake are never asked. With no map the prompt is unchanged byte for byte. The map is still
written by that repository's own session (D32). Rejected: a canon skill (a Daoris feature is not
doctrine that two repositories learned) and a pack (the same bar). Decision in the map design §3;
no D-number, following the precedent that the owner's map answers live in the design.

Built in a parallel worktree and cherry-picked. Driver 464 → 470. The family rehearsal reads the
stub's own transcript both ways, and its two new checks passed on their first run here (256 → 258).

## INT3 — registered is drivable over the protocol door (2026-09-24)

- [x] ~~⛔ **INT3 — registered is drivable over the ACP door.** The exchange and the planner stop
  requiring a manifest for a target with a root; the pipe door keeps its own requirements. **The
  owner's yes first** — it amends the letter of D34/D46.~~
✅ **done 2026-09-24** — the owner said yes, and **D70** records it: registered is addressable,
adopted is disciplined. `Registration.Addressable` (adopted, or a root on this machine) is the one
judgement. The exchange and its chain steps read it, and so do the MCP registry,
`/api/registry`'s new `addressable` field and every receiver list on the page. The planner is told
the machine's door. Over the protocol door, an unadopted repository with a root plans like an
adopter. Over the pipe door it sits, naming the door that could carry it. The publish tells the
asker that only a protocol-door session can answer. Found on the way: the register door stored every
row as adopted, so the desktop's add of a folder with no manifest would have been driven over the
pipe door with no connector. It now sends what the shell found. A shared deployment still takes
quests only for adopters. D70 says what such a session is given (the quest and its boundary, the
connector, the repository's own configuration, the D52 refusal) and what it is not (the canon).

Built in a parallel worktree and cherry-picked. The family rehearsal's §17b drives one to done over
the ACP stub door with nothing written into its tree, and holds it over the pipe door. It passed on
its first run here (265/265, with MAP3d's two). Service 454, driver 477 with INT4h, web 766.
**Looked at on the window**: Projects lists an unadopted repository under *Registered, not adopted*,
and offers it no *drive on this machine* control (**INT3c**). **Unproven with a real agent**: the
stub never asks permission, while a real Claude Code agent would ask before each connector call in a
repository with no allow-list, and D52 refuses. DEPLOY1's measurement adds that the door ignores an
untrusted room's allow-list. D70's last paragraph says so, and **INT3b** carries it.

## INT4h — a running intake takes no messages, on either door (2026-09-24)

- [x] ~~**INT4h — a running intake offers a message box whose words go nowhere.** An intake is one
  turn, framed as one prompt, so a message typed into its composer reaches a process that never
  reads it. Say so or offer no box, as INT4g did for a parked one. Found by INT4g.~~
✅ **done 2026-09-24**. It was verified in the driver first, and on one door it was worse than
*nowhere*. `SESSION_INPUT` wrote the line into the process's stdin. On the pipe door an intake has
none, so the page was told `sent: false`, which it read as *it ended*. On the protocol door its stdin
carries the driver's own JSON-RPC frames, so the line landed mid-stream, the page was told `sent:
true`, and *finish* closed that stream. A session is now tracked with whether it takes a person's
line. An intake does not, so `Send` and `CloseInput` refuse before anything is written, and the
bridge answers `SESSION_INPUT` and `END_CHAT` with the driver's sentence (`DRIVER_REFUSED`) saying
where the answer goes. The screen gives a running intake no composer. Its head (`RunningIntake`)
says why, opens the ask to look at, and carries the stop the composer used to (`STOP_SESSION`). Intake
design §1h and platform-ux §4 (FIX-LOG).

Built in a parallel worktree and cherry-picked. Driver +3, modules 109 → 111, web +12. **Looked at on
the window** in both themes: no composer, *open ask #id* as a plain button (a running intake has
asked nothing yet), *stop it* with its line, and the running intake's room shown busy. **Filed:
INT4i**, a driven quest session on the protocol door is still tracked as taking a person's line.

## INT3c — an unadopted repository can be opted into driving from Projects (2026-09-24)

- [x] ~~**INT3c — Projects offers an unadopted repository no *drive on this machine* control.** D70
  makes one drivable over the protocol door once opted in, but only the adopters' cards carry the
  control. The terminal's `daoris driver` can opt it in; the screen cannot (D50). Found at INT3's
  integration.~~
✅ **done 2026-09-24**. The driving row (drive, hold once driven, own tree per session) is one
molecule, `projects/DriverChoices`. It sits on an adopter's card and on an unadopted repository with
a root here (`canBeAsked`), because the planner treats the two alike once past the door. One with no
root gets nothing. *Manage* stays off the unadopted row, because it writes a declaration into the
repository. On a machine whose adapter rides the direct door, the row says a quest there sits until
it drives on a protocol agent. The control is kept rather than hidden: an opt-in outlives an adapter
change, and hiding it would leave the screen unable to set what the terminal can. The door is read
from the roster (`doorOf`: the adapter and its entry's `wire`). When the roster cannot say, nothing is
said. The group's paragraph now claims only what is proven (INT3b). Built in a parallel worktree.
**Looked at on the window** in both themes. Web 766 → 781.

## INT4i — a driven quest session takes no person's line, on either door (2026-09-24)

- [x] ~~**INT4i — a driven protocol-door session still takes a person's line.** Its stdin carries the
  driver's JSON-RPC frames, and it is tracked as taking input (`Driver.cs`, where a quest session is
  tracked), so a `SESSION_INPUT` for it would write into the protocol stream. The page offers it no
  box. INT4h fixed the intake half. Found by INT4h.~~
✅ **done 2026-09-24**. Measured by a real tick before the fix. On the protocol door, `Send` for a
driven session answered `true`: the line was written into the stdin carrying the driver's frames. On
the pipe door it answered `false` with no reason, which the bridge turns into *it ended*. The executor
now tracks a driven session with the driver's sentence (`Driver.TakesNoMessages(quest)`), so `Send`
and `CloseInput` refuse before anything is written. The bridge's existing refusal carries that
sentence for `SESSION_INPUT` and `END_CHAT`. Conversations are untouched, and only they take turns
with a person. The test INT4h named as missing now exists: a driven quest through a real tick on both
doors (`DrivenSessionInputTests`). Driver design §4. Built in a parallel worktree. Driver 477 → 479;
with INT3c: modules 111, web 781 + 21, family 265/265, deploy 39/39, verify green.

## PLUG2 — a pack may switch a core row off (2026-09-24)

- [x] ~~**PLUG2 — a pack cannot disable or override what core installs.** dsh composes profiles as
  ordered layers where a layer may switch a row off (`- id: x` / `disabled: true`); Daoris's manifest
  `packs: []` is a flat set with no precedence. It reopens **D4's "core installs with no opt-out"**.
  🔴 **The owner reopened it, 2026-09-24**: a pack may switch core rows off. Designed first, then
  built, as D71.~~
✅ **done 2026-09-24** — **D71**. A pack offers (`switchesOff` in `pack.json`: core row → reason,
checked when the canon is read), and the repository confirms (`switchedOff` in `daoris.json`: row →
pack). Only a confirmed row goes off. An unconfirmed offer leaves it on and is reported. A
confirmation that no selected pack offers is a tool error, so a repository alone still cannot drop
core. The lock records `switchedOff` so the offline `check` can name it. It is never silent: `sync`,
`check` (every run; it fails only on a manifest/lock mismatch), `status` (pack and reason), the
region's roster, `init` and `analyze` all say what is off or offered. D19 gains the cells. An edited
row being switched off refuses like drift, because `upstream` still reaches the canon file, and a
switch is never reported as a rename. Two selected packs shipping one target are now refused. There
is no `apiVersion` bump, because an older CLI keeps core on.

**The confirmation is the fork's default, not the owner's words.** The owner said a pack *may*
switch a row off, and did not say whether the repository gets a say. D71 takes the safer reading and
names it as the owner's to reverse (one check in `resolveSelection`). Built in a parallel worktree and
cherry-picked. CLI 383 → 403, and the release rehearsal's switch-off phase 5(e) passed on its first
run here (56 → 66/66). Service 454, family 265/265, verify green.

## PERM1 — what an agent may do: Daoris's rules in scopes, handed over at spawn (2026-09-24)

- [x] ~~**HELP3 — one guard, every harness.** `dsh-hooks-claude-code` runs an existing `hooks.json`
  in Claude Code's dialect and `dsh-hook-protocol` makes the Codex bridge behave identically, so a
  guard written **once** — refuse a write outside the session's tree (D51), refuse a push (D37) — runs
  on all three. ⛔ Where does the guard live?~~ and ~~⛔ **INT3b — a real protocol agent in an
  unadopted repository cannot use its connector.** An unadopted repository has no allow-list for the
  connector's tools, so a real Claude Code agent asks before each call, and D52 refuses.~~ Both were
  folded into PERM1 on the owner's answer (2026-09-24): *"so we should be able to do just like how
  claude code scopes configured by rules in daoris (which daoris can also use llm to update those too
  or modified by user)"*.
✅ **done 2026-09-24 (phase 1)** — **D72**, design in `docs/2026-09-24-permission-scopes-design.md`.
The rules are Claude Code's own, in three Daoris scopes (machine, circle, repository), kept in one
file under the home. They are unioned with Daoris's defaults: `connector` allows the connector's quest
and knowledge tools, and `no-push` denies a push. The union is handed to the harness as its
command-line tier. On the pipe door that is `--settings` (quests, intakes and conversations); on the
protocol door it is `session/new`'s `_meta.claudeCode.options.settings`, read from the adapter's
0.79.0 source. The harness's own precedence decides, so a repository's deny still wins. There are two
doors: `daoris agent rules …` and Settings → *What agents may do*. It covers Claude Code only; other
agents are handed nothing. "Refuse a write outside the tree" has no rule form, so the harness's
working-directory boundary holds it and a path-checking hook is PERM3. An agent updating the rules is
PERM2, designed and not built.

**Measured on real sessions at integration**, which changed the picture. The command-line tier is
honoured in an **untrusted** folder on both doors: the probe's third room over ACP, and `claude -p
--settings` on the pipe (`docs/2026-09-24-deploy1-acp-trust-evidence.md`). So the `connector` default
alone lets a real agent take and close its quest untrusted, which is what INT3b asked for, and ACP2's
real run showed it. **Looked at on the window** in both themes: the card was right, and the page had
grown a second scrollbar. The rules card's `sr-only` labels sat far down a column that was not a
containing block, so they stretched the document to 3,284px in a 919px window. The column is
`relative` now (`77c60c8`). Built in a parallel worktree and cherry-picked. CLI 403 → 415, driver
479 → 515, modules 111 → 114, web 781 → 794, family 265 → 268.

## DEPLOY1 — the second half: trust asked, then written (2026-09-24)

- [x] ~~🔴 **DEPLOY1's second half — should adoption ever ASK for the trust flag?** The detection half
  shipped 2026-09-22: the driver reads the harness's own record and **holds**. What is left is the
  owner's: whether adoption should *ask* and write the flag (option b), or whether the pipe door stays
  documented as needing a human's first visit (option c). 🔴 **Never (d), silently** — that flag *is*
  the grant. 🔴 **The owner chose (b), 2026-09-24**: Daoris asks per folder, then writes the flag. For
  managed repositories *"we should follow what claude code does"*.~~
✅ **done 2026-09-24** — **D73**. Daoris writes the agent's trust flag only on the person's explicit
act naming the folder. From the terminal that is `daoris agent trust <agent> <folder> [--profile P]
--yes`; without `--yes` it is only the question and writes nothing. On the screen it is *trust this
folder…* on a hold the driver is showing: in the quest's drawer, and as a `trust` row in *What needs
you*, which also carries an intake's room. One flag moves, in the agent's own file: written beside,
renamed, then read back, and a file this build cannot read is refused. `ClaudeTrust.Grant` and
`trust.ts` are twins held by the same cases. The bridge grants only a pair the last tick held.

Since PERM1, which the fork cherry-picked on the parent's message, the driver holds for trust only
where the connector allowance would not reach the session. Trust now decides whether a repository's
own allow-list counts, not whether it can be driven. **Looked at on the window** in both themes, with
the connector default switched off on the scratch machine so a hold would show: the row and the
question it opens, which names the folder, the quest held and the file and flag it would write. *Trust
this folder* was not pressed, because it writes the account's `.claude.json` and the permission check
refused an agent that write. Nit: the drawer's title and its card's heading repeat one sentence.
Unmeasured: whether Claude Code honours a key Daoris wrote, and whether a trusted parent covers a
child (TRUST2). The fork studied the real `.claude.json`'s key form once, which echoed some folder
names into its own session output; nothing reached a tracked file. CLI 415 → 431, driver 515 → 530,
modules 114 → 116, web 795 → 809.

## ACP2 — `claude-code` over the protocol door, proven on a real login (2026-09-24)

- [x] ~~**ACP2 — `claude-code` over ACP.** `@agentclientprotocol/claude-agent-acp` pinned exact as a
  managed toolchain entry; `acceptEdits` set as the ACP **mode**, not a flag; the permission answerer
  against Claude Code's real requests; the record naming adapter, harness version and profile as
  today. Closes with **the real driven run** (DRV4's shape) — the owner supplies the login. **This is
  D23's "on proof".**~~
✅ **done 2026-09-24** — `node tools/acp2-proof.mjs --drive`: **17/17**, on the owner's authorization
for real sessions. A real Claude Code session over the protocol door took a real quest, made the
change, committed it and closed its quest through its own connector, in a scratch folder nobody had
trusted. It took seven sessions to get there, and each stop taught something:

1. **Held on trust** (no login spent), until PERM1 carried the connector allowance and D73 made the
   hold conditional on it.
2. **Three sessions died at their first tool call.** A real `tool_call` carries `content` as a list,
   and the reader read it as an object. The throw ended the reader behind the false sentence "the
   stream ended" (FIX-LOG, `cba1be8`). The strike limit parked the quest after three, as designed.
3. **Three sessions declined honestly.** They took the quest, made the edit, and were refused `git
   add`/`git commit`. The repository's own allow-list does not apply untrusted, and the proof's rules
   were in the wrong home: the driver's home is the folder its `driver.json` sits in. The proof now
   writes the person's commit rule there.

**What it leaves for the owner: PERM4.** Without a rule allowing it, a real driven session in an
untrusted repository cannot commit, so it declines. Whether Daoris should ship that as a default is
the owner's call. Driver 530 → 532.

## INT4f — a real ask through a real intake harness (2026-09-24)

- [x] ~~**INT4f — a real ask through a real intake harness.** INT4b is proven by a stub. What is
  unseen: the MCP host publishing as the ask under a real harness's environment.~~
✅ **done 2026-09-24** — `node tools/int4f-proof.mjs --drive`: **16/16**, on the owner's
authorization for real sessions. A real Claude Code intake over the protocol door read a
two-repository circle's declarations in its room, a folder nobody had trusted. It chose
`proof-ledger` for "the iron sword costs 40 gold… should cost 25", which the declarations name, and
published through the MCP host's `quest_publish` as the ask. The ask reached Published with tier
`intake`, and its quest `#0283fd2f220b` was asked by `ask #158421` and carried the ask's link and
file. The session ended completed, observed from the ask rather than its own account. Nothing was
written into either repository (D32). Daoris's `connector` default carried `quest_publish` into the
untrusted room (D72), and the hold stayed lifted where it does (D73).

The script was written in a parallel worktree. Its first drive died before any session, on the
temporal-dead-zone trap `acp2-proof.mjs` already warns about (a `const` declared below the run that
reads it), and it was fixed here. **Two refused requests on the way**, both `Read` of the ask's kept
file, which lives under the home and outside the room. The sentence settled this ask. One whose
substance is in its file would be decided blind, which is INT4j.

## PERM3 — the tree guard as a hook (2026-09-24)

- [x] ~~**PERM3 — the tree guard as a hook.** Refuse a write outside the session's tree structurally,
  with a PreToolUse hook Daoris ships (`permissionDecision: deny`, never an exit code). A rule cannot
  say "outside" (design §3).~~
✅ **done 2026-09-24** — D72 as amended, design §3b. `tree-guard` is a default that is a hook rather
than a rule, handed in the same settings file on both doors. It runs in exec form (`node`, the script
and the session's own tree as one argument each) on `Edit|Write|MultiEdit|NotebookEdit`. A path is
resolved through links and compared with the tree's real path, case-folded on Windows. A write
outside is denied on stdout with exit 0. Inside, the hook says nothing, because an allow would lift
the harness's asking. An unreadable call, or a tree the hook was not told, is refused. The script is
carried in the driver's assembly and written under the home, and the person switches it by id from
either door. It does not cover a shell command's writes, where the working-directory boundary stands,
and a hook that fails or times out does not block, so it is defence beside that boundary.

**Proven on real sessions at integration**, in a fresh untrusted room: a `Write` one folder up was
refused with the guard's own sentence on the pipe door (`--settings`) and on the ACP door
(`session/new`'s settings), with no file made. A write inside was allowed as the control. Recorded in
the DEPLOY1 evidence document. Built in a parallel worktree, together with PERM4. Driver 532 → 547,
CLI 431 → 433, web 809 → 810; service 454, family 268/268, deploy 39/39.

## PERM4 — a driven session may commit, by default (2026-09-24)

- [x] ~~**PERM4 — should a driven session be allowed to commit by default?** ACP2's real run showed
  a session in an untrusted repository taking its quest, making the edit and being refused `git
  add`/`git commit`, so it declined honestly.~~
✅ **done 2026-09-24** — the owner said yes. A `commit` default allows `Bash(cd:*)`, `Bash(git
add:*)` and `Bash(git commit:*)`. `cd` is there because the agent prefixes its commit with one, and
every part of a compound command must be allowed. `no-push` still refuses the push, since deny beats
allow in the harness. The person switches it off by id from either door, and it shows on Settings →
*What agents may do* beside the others. The CLI and driver tables are held together, and a test holds
that it is handed and that switching it off removes it. Built in the same commit as PERM3.

## INT4j — a session reads its own quest's or ask's kept files (2026-09-24)

- [x] ~~**INT4j — a real intake cannot read its ask's files.** INT4f's real run was refused `Read`
  twice on the ask's kept file, because it lives under the Daoris home, outside the intake's room, and
  D52 refuses every request. The sentence settled it there. An ask whose substance is in its file
  would be decided blind. A read-only allowance for that ask's own files, carried at spawn like
  PERM1's rules, is the likely answer. Found by INT4f.~~
✅ **done 2026-09-24** — D72 as amended, design §4. The executor adds one rule to the session's
composed settings, `Read(//<folder>/**)`, for the folder its own quest's or ask's files are kept in:
not the home, not another's, and nothing when none is kept here. A driven quest session gets the
same, since its files are kept the same way (D65 §2). The form is Claude Code's own, read from its
2.1.281 bundle: `oLn` resolves `//` as absolute, and `rR` normalises a Windows target to `/c/x` before
`Na` compares it, case-sensitively for an allow. The tree guard judges writes only and is untouched.
Rejected: `additionalDirectories`, which under `acceptEdits` would also let edits there through
unasked. Built in a parallel worktree. Driver 547 → 551.

**Proven on a real session at integration**: `tools/int4f-proof.mjs --drive` ran 17/17 with **zero
refused requests**, where INT4f's run had two. The intake read the ask's kept file without being
asked and published as the ask again.

## PERM2 — an agent updates the rules (2026-09-24)

- [x] ~~**PERM2 — an agent updates the rules.** A connector tool, `permission_propose`. Narrowing
  applies at the next tick, widening waits for the person's yes, and every change is recorded with
  who made it (design §6). 🔴 **The owner answered, 2026-09-24**: a widening never applies without
  the person.~~
✅ **done 2026-09-24** — **D74**. `permission_propose` is in the `connector` default and writes one
file per proposal under `<home>/proposals/`, never the store, so a machine-local fact stays local
(D47 §4). The driver names the session and its rules home on every connector it hands over. The tick
applies a narrowing before it spawns, so that tick's sessions already carry it, and holds a widening
as `waiting`. A change the rules already hold is `unchanged`, and one they cannot take is `refused`.
The person answers from `daoris agent rules proposals|accept|decline` or from Settings' *Proposed by
agents*. A waiting widening is also a `rule` row in *What needs you*, and settling records who and
when. Rejected: a service-store row, the MCP host applying narrowings itself, classifying at the door,
any widening without the person, and deleting settled proposals. Built in a parallel worktree and
cherry-picked. One conflict, in a test file where INT4j and PERM2 added tests at the same spot, was
resolved by keeping all four.

**Looked at on the window** with seeded proposals. A widening (`allow Bash(dotnet test:*)`) became a
band row naming the session and its reason, and opened Settings at its proposal. A narrowing (`deny
Bash(rm -rf:*)`) applied itself on the tick and showed under *Earlier proposals*. Pressing *accept*
recorded "the person" and put the rule in the machine's rules. **Not yet**: a real agent choosing to
propose after a refusal, which is PERM2b. Service 454 → 468, driver 551 → 584, modules 116 → 118,
CLI 433 → 443, web 810 → 820 + 21, family 268 → 271, deploy 39/39.

## PERM2b — a real session proposes after a refusal (2026-09-24)

- [x] ~~**PERM2b — a real session proposes after a refusal.** PERM2's door is proven with a seeded
  proposal on the window: the tick held a widening, the person accepted it, and it landed. Whether a
  real agent reaches for `permission_propose` after being refused is the model's behaviour, not the
  door's. One real session answers it; its prompt may need to say the tool exists.~~
✅ **done 2026-09-24** — `node tools/perm2-proof.mjs --drive`: **14/14**, on the owner's
authorization for real sessions. The quest target had never named the tool, so it now does,
conditionally, in one paragraph (D74 as amended; driver 584 → 586). A real Claude Code session over
the protocol door took a quest asking for a commit and an annotated tag. It committed under the
`commit` default and was refused the tag (D52). It then proposed `allow Bash(git tag:*)` scoped to
**that one repository**, the narrowest scope, which it chose itself. Its reason was that the commit
had landed and the tag stays local and reversible, since pushing is still refused. The tick held the
proposal `waiting`, nothing reached the rules without the person, and the session declined honestly.
The script was written in a parallel worktree. Family 271/271, deploy 39/39, verify green.

## POLISH2 — the installed application, read on an empty machine (2026-09-24)

> **"we also need to keep polish the ui/ux you can use screenshot tool to confirm"** — owner,
> 2026-09-22. The standing direction, taken up when every other open row waited on the owner.

✅ done 2026-09-24. The owner's install was republished at `main` (61 commits behind) and read surface
by surface, in 中文 as the owner runs it and in English, in both themes. It drives nothing, so no
session started. Every registration had been retired on 2026-09-23, so the install showed what a new
installation shows, and the example family cannot: it always holds a repository.

Ten defects, each found by looking. `docs/2026-09-19-platform-ux.md` §4 (POLISH2) carries the rules
and `docs/FIX-LOG.md` the root causes.

- **An empty machine.** Projects was a blank page. The ask and quest composers offered empty choices,
  so a request written in full could never be sent. The map said "this circle" when scoped to every
  circle.
- **Backticks printed raw**, in fourteen catalogue strings and many of the service's and driver's
  sentences, in both languages. `Inline` sets them as code and changes no word.
- **The rules card (PERM1), the first time it was looked at.** Its heading was repeated as its
  first row, its four defaults ran together, and it explained them in English under 中文. The zh
  catalogue had drifted to 代理 for "agent" (智能体 elsewhere), 驱动器 for "the driver" (a disk
  drive), and 道衍 in running text. The plugins card repeated its heading too.
- **A failed session wore success's green dot** in the rail.
- **Chinese in italic was slanted by synthesis.**
- **An ended session's console was an empty bordered well**, which reads as a field.

Seen and not fixed, filed as POLISH3: two account rows with one name, the drawer scrim cutting the
status bar, Convergence's seven-second load on the real index, and 中文 spacing durations and ages
differently. Web unit 820 → 834, Playwright 21/21 (seven toast assertions matched raw backticks),
verify green. The install was left in 中文 as found, and stopped.

## POLISH3 — what POLISH2 saw and left (2026-09-24)

- [x] ~~**POLISH3 — what POLISH2 saw and left.** Two account rows with one name; the drawer's scrim
  cutting the status bar; Convergence waiting about seven seconds on the real index behind a bare
  "comparing…"; 中文 spacing durations and ages differently.~~
✅ **done 2026-09-24**, each fixed and looked at on the republished install.

- **Convergence is three times faster, with byte-identical answers.** The lexical pass compared every
  ordered pair of local entries, so each unordered pair twice, and counted shared tokens by walking
  the first set whether or not it was the larger. It now walks each pair once and the smaller set.
  Measured on the install: 4.5–10.4s a call (median about 7) before, 2.1–2.5s after. The answers at
  0.5, 0.75 and 0.9 were saved before the change and compared as bytes after it. A new test pins the
  grouping a chain of restatements makes, so comparing once cannot change it. The page also shows
  skeleton rows while it compares, with "comparing…" on the line the count takes.
- **The frame is three bars, and an overlay sits between them.** The drawer, its scrim and the
  palette's scrim stop above the status bar, as they already left the strip and the activity bar
  alone. `tokens.test.ts` holds it for every overlay, beside the two rules it joins.
- **A name that repeats says which it is.** When the tool's own home and an account made in Daoris
  are signed in as the same person, the own row says *this machine's own* beside the name.
- **中文 sets a number apart from its unit** in an age as it already did in a span (`1 天前`, not
  `1天前`), and a template holding an age sets it apart too. That is the catalogue's own convention:
  126 placeholders spaced against 14 tight.

Service 468 → 469, web unit 834 → 838.

## FRAME1 — one word in the interface (2026-09-24)

- [x] ~~**FRAME1 — one word in the interface.** *workspace* / 工作区 in both catalogues, where the
  interface said *circle* 26 times and 圈子 24. The tests that read the old words move with them.~~
✅ **done 2026-09-24** (D75 §4). 24 English and 24 Chinese strings changed, by a scripted sweep over
values only. It asserted its counts, and the first run stopped itself on a count that was wrong,
before writing anything. A key or a placeholder named `circle` stays, because nobody reads it.
`i18n.test.ts` now holds that no catalogue value says *circle* or 圈子, and it was seen failing
against the old catalogue. Eight vitest and four Playwright assertions moved with the words. Looked
at on the install: the top bar, the status bar and the ask composer all say 工作区. The sentences the
other doors print are FRAME5. Web unit 838 → 839.

## FRAME2 — Settings by domain (2026-09-24)

- [x] ~~**FRAME2 — Settings by domain.** One page, the domains in a list at its left, one shown at a
  time and reachable by name (design §3). Every domain is cards the page already holds.~~
✅ **done 2026-09-24** (D75 §2). The seven domains sit in a list that stays put while a long one
scrolls. A browser is offered Appearance and Daoris's own AI, and nothing it may not know. The chosen
domain is App's to hold and is remembered per viewer. Every way in now names its domain: the
status bar's tier opens Daoris's own AI, its remote and the sync item's wiring open Workspace, the
driver opens Driver, a waiting proposal's row opens Permissions, and the menu's items their own.
The machine's settings split into a Driver card and a Wiring card. The home is the Driver card's
first row, where it had floated unlabelled once the "This machine" heading went. Looking found one
more repetition: a card alone in its domain carried the domain's name as its title (外观 beside
外观), so four titles and their keys went. 51 of the Settings tests now open the domain they are
about; the one that read the intake's account "in both places it is drawn" reads it in both
domains; and a browser has its own test. Looked at on the install in 中文, light and dark. Web unit
839 → 842.

## FRAME3 — the menus by domain (2026-09-24)

- [x] ~~**FRAME3 — the menus by domain.** *Daoris · Workspace · Agents · View*, each setup item
  opening its domain, *Workspace* choosing the scope, and a browser's menus holding only what it may
  know (design §2).~~
✅ **done 2026-09-24** (D75 §1). `work/appMenus.ts` builds the three setup menus as data, and an
item's id is the act it names (`menuAction`), so a desktop's menus and a browser's are asserted
without mounting the application over a mocked bridge. *Daoris* holds Settings, Driver, Plugins,
refresh, language and About. *Workspace* lists every workspace with its repository count and ticks
the scope. It offers *every workspace* among several, names the one there is, and says *No
workspace yet* with none. It also holds *Add repository…*, which opens Projects' drawer as an event,
*Import a folder…*, *Wire to a remote…* and the workspace's settings. *Agents* holds tools and
accounts, what agents may do, proposals with the count waiting, usage, and Daoris's own AI.
**Import gained a screen door** (D50): the folder picker, then the same `POST
/api/registry/import` the CLI uses, with the service's sentence as the toast. 🔴 **Not pressed on the
window**: pressing it registers repositories into the owner's install, and the owner connects the
real workspace themselves. The route is the CLI's; the page's path to it is not exercised
end-to-end. Two things were found while building it. The zh catalogue said 账户 15 times and 账号
11, and is now 账户 throughout, held by a test. And a `check` icon beside the menu's check column
read as "ticked", so what agents may do wears a shield. Looked at on the install in 中文, light and
dark. Web unit 842 → 850.

## FRAME4 — the workspace always named (2026-09-24)

- [x] ~~**FRAME4 — the workspace always named.** The command center and the status bar name it in
  every state, none included, and the Workspace domain lists each workspace with its repositories
  (design §4).~~
✅ **done 2026-09-24** (D75 §3). App words the scope once, as the chosen workspace, the one there is,
*every workspace · N* among several, or *no workspace yet*. The command center and the status bar
both show that. WSP5's switcher still appears only with a choice to make: the rule hides the
control and no longer hides the fact. The Workspace domain opens with every workspace, its
repository count and its repositories, from the same unscoped registry answer the Workspace menu
reads. It is offered in a browser too, with none of the machine's wiring. Seen on the empty install:
all three say 还没有工作区. A body that began by repeating its own title lost the repetition. Web
unit 850 → 853.

## FRAME5 — one word at the other doors (2026-09-24)

- [x] ~~**FRAME5 — one word at the other doors.** The sentences the CLI, the driver and the service
  print, which three artefacts' tests assert verbatim.~~
✅ **done 2026-09-24** (D75 §4), which closes the D75 arc. 23 printed sentences now say
*workspace*: 10 in the CLI (permissions, remotes, the toolchain's pins, the intake's hint), 9 in the
driver and its host (the intake room's own words to its agent, the rule-scope refusal, the sync's
lines and usage), 3 in the service (the ask refusal, the ledger, the retire message), and one in
the MCP tool description an agent reads for `permission_propose`'s scope. Identifiers, SQL comments and `{circle}` variables
stay, because nobody reads them. `one-word.test.ts` in the CLI suite scans the string literals of
all three artefacts and was seen failing on the service's old ask refusal. Three assertions moved
with the words: a CLI permissions test, a driver sync test and a family-rehearsal check. CLI 443 →
445, driver 586, modules 118, service 469, verify green.

## POLISH4 — the surfaces an empty install cannot show, read with data in them (2026-09-24)

- [x] ~~**POLISH4 — the surfaces an empty install cannot show, read with data in them.** The
  scratch shell over the example family, which holds a parked session, a running and a parked
  intake, five asks and an unadopted repository. Read surface by surface in English and 中文, both
  themes. Nineteen findings, in three landings: **a**, what the words claim; **b**, the asks; **c**,
  layout.~~
✅ **done 2026-09-24**, in three commits, each looked at on the rebuilt window.

The owner's install is empty (every registration retired), so it shows a new installation and
nothing else. What POLISH2 and the FRAME items could not look at was everything with data in it.

- **a — what the words claimed.** Five sentences had outlived the decision under them: Overview's
  note that the index scans the family's folder and only an adopter is addressable (WSP2, D70); the
  band's promise that finished work would join it once review existed (review exists, looking is not
  recorded); Projects' "who cannot be asked yet" and "not yet proven" about the connector in an
  unadopted repository (PERM1 measured it); and the intake room's "Not addressable: nothing there
  can see a quest", which now says one with a root can be published to when the person names it.
  The default rules explained themselves with decision numbers, in the CLI's table, the driver's and
  the zh catalogue; a test on each side holds that they do not.
- **b — the asks.** A card named its place as a bare `default`, which beside `game → engine` read
  as a repository. It never said an intake was reading it or had asked the person, though the band
  above it did. It says both now, the second with the band's own warn mark. The group was counted
  *Asks · 5* beside *Open … (5)*. The record read "answered by by declarations", repeated a one-line
  ask as its own body, and offered *any repository in default*.
- **c — layout.** A project card's wrapped chip fell under its label; the labels are a column now.
  The join steps were monospace prose; the commands are code in a sentence. The map's node number
  was in no legend, and a parked session ringed its repository *working now*: it reads *waiting on
  you* in the warn tone. Convergence's tier note ran about 180 characters a line and its empty
  answer was a bare line; it has a measure and an empty state that lowers the similarity on a press.
  A search snippet opened with the entry's raw frontmatter; the service's excerpt is taken from the
  prose now, and the frontmatter still matches. And 中文 said 任务 for a quest in nineteen strings,
  held by a catalogue test now as 工作区 and 账户 are.

`docs/2026-09-19-platform-ux.md` §4 (POLISH4) has the rules and `docs/FIX-LOG.md` the mechanisms,
with one trap: `max-w-prose` on a flex item caps its `basis-full`. CLI 445 → 446, driver 586 → 587,
service 469 → 470, web unit 853 → 866, Playwright 21/21, family 271/271, verify green. The card's
two new states have a story (`CardsWithTheirIntake`).

## POLISH5 — retiring the last repository left every retired one indexed (2026-09-24)

- [x] ~~**POLISH5 — retiring the last repository leaves every retired one indexed.** Found
  republishing the install after POLISH4: no registration, and 1,052 entries from 17 repositories
  still charted, searched and compared. The workspace design §7 says a retired repository "stops
  being addressable **and indexed** here".~~
✅ **done 2026-09-24.** The ghost rule pruned only when a refresh's scan saw something, a guard
written when the source was a folder (seeing nothing meant a mis-set path). Since WSP2 a local host
reads the registered roots, so the last retire leaves a scan that sees nothing by construction. The
guard could not simply go, because a shared host reads an empty source for the opposite reason: it is
fed. So the local host, and only it, lets the registry decide what is a ghost, whatever the scan saw;
the guard still keeps a registered repository whose checkout cannot be read. Overview's note now
claims only that the others have not adopted. Three service tests, one for each case, including the
fed host the guard exists for. On the republished install, *rebuild index* took it from 17
repositories to none. The empty index then showed Overview's repositories card as a heading over a
footnote, so it has an empty state too. `docs/FIX-LOG.md` has the mechanism. Service 470 → 473,
web unit 866 → 867, Playwright 21/21, family 271/271.

## CONV1 — the event record (2026-09-25)

- [x] ~~**CONV1 — the event record.** Daoris's event vocabulary (D76 §1); the ACP door maps
  `session/update` into it where `Acp.cs` flattens it today; events appended to
  `sessions/<id>.events.jsonl`; a live bridge event and a paged history read; the console keeps its
  lines. No new rendering yet: a test reads a session back after a restart.~~
✅ **done 2026-09-25**, the first landing of D76.

- **The vocabulary** (`SessionEvents.cs`): user (the person's, or the driver's composed target),
  message, thought, tool (with its ACP kind, status, places, input, output and content: text, diff
  or terminal), plan, usage, turn (the wire's stop reason, a self-report), note (the driver's own
  sentence) and raw (anything this build does not know, kept rather than dropped).
- **The protocol door keeps its structure.** `AcpSession` takes an `onEvent` beside `onLine`, and
  `Map` turns each update into an event with every read shape-checked (the ACP2 lesson: a real
  `tool_call` carries its content as a list). The console still gets its lines, unchanged. A
  refused permission is a note, an unreadable frame is raw, and the turn's end is an event.
- **The record** is `sessions/<id>.events.jsonl` under the home, one event per line, numbered per
  session and carried on across a new instance. It is read a page at a time: newest, `before`, or
  `after` for a gap. A torn line costs itself, a large field is cut and says how long it was, and an
  id that is not an id names no file. The driven session and the intake both write it; the headless
  host keeps it too, with nobody watching. The pipe door stays text until CONV3.
- **The bridge**: `SESSION_HISTORY` for a page and batched `SESSION_EVENTS` live. The batching
  moved into `BatchRelay<T>`, which the console's relay and the new event relay now share, rather
  than being copied.
- **The page** has `useSessionEvents`: the newest page on open, live batches merged by sequence,
  a batch that skips ahead closed by asking `after`, earlier pages on request. Nothing renders it
  yet; that is CONV2.

On the way, the modules suite showed POLISH4a had left one assertion looking for "D37"; fixed in its
own commit (`75ae868`). Driver 587 → 604, modules 118 → 120, web unit 867 → 874, family 271/271,
deploy 39/39.

## CONV2 — the conversation view (2026-09-25)

- [x] ~~**CONV2 — the conversation view.** The centre renders the events: the person's and the agent's
  messages, Markdown, code with copy, thinking folded, tool calls as cards (generic, then read, edit,
  shell, search), a turn's work folded, follow-the-tail with *back to bottom*, history a page at a
  time. The console becomes its raw view. Adds the renderer and highlighter (D76 §5).~~
✅ **done 2026-09-25**, the second landing of D76, and the first a person sees.

- **The fold** (`work/conversation.ts` `toTurns`, pure): an ask opens a turn and the wire's turn end
  closes it; chunks join; a tool call is one card carrying the latest of every field its updates set;
  a plan is its latest entries; usage is a meter (the latest reading and the high-water mark), never
  a block.
- **The view** (`ConversationView`, a molecule): the ask (the person's as written, the driver's
  composed target folded to two lines and named as the driver's), the agent's words as Markdown, a
  thought folded to its first line, a tool call as a row (`ToolCard`: kind glyph, title, place,
  an edit's `+n −m` from a line diff, status; closed unless it failed, and an edit's diff open), a
  plan, the driver's note, an unknown update kept raw. A finished turn folds its work into one
  counted row and keeps its last message open; a running one says *working…*.
- **Code** (`CodeBlock`): highlight.js, coloured from D41's tokens in `work/code.css`, the language
  named, a copy button; never guessed when no language is named. **Markdown** (`Markdown`):
  react-markdown with GFM, no raw HTML, links opening outside the window. The stack addition is in
  the frontend architecture's table, with why not Shiki.
- **The organism** (`SessionConversation`) holds the record (`useSessionEvents`, now saying when it
  has loaded) and the scroll (`useFollowTail`: follow until the person scrolls up, *back to
  bottom*, a new session opening at its tail). The main window's centre and the detached window both
  scroll the head and the conversation together; the console stays below as the raw view.
- **Looked at** on the scratch window with a session driven over the protocol door by a scripted
  agent (thinking, a plan, reads, an edit with a diff, a test run, a Markdown answer with code and a
  table), in English and 中文, light and dark. Three defects only the window showed, all fixed: a
  plan's done steps were struck through and read as cancelled; a parked pipe chat said *Nothing said
  yet*, which is never true of an empty record; and the detached window's console said *Nothing
  said yet* under the conversation. On the way, the scratch family's checkouts turned out not to be
  repositories, so git answered for Daoris's own tree and the driver rightly refused twice.

UX1 gained two findings to settle there: one hue for *waiting on you* (the rail and the band wear
declined's red, the map the warn tone), and the monitor's console-only tiles. Web unit 874 → 902,
Playwright 21/21.

## CONV3a — the native door on the structured wire (2026-09-25)

- [x] ~~**CONV3 — conversations and native sessions on the structured wire.** Claude Code's
  `stream-json` for driven sessions and chats on the native door (the adapter's own mapping, checked
  against the binary), ACP turns for a chat on the protocol door, the person's message in the
  record.~~
✅ **the native half done 2026-09-25.** It was split when a chat on the protocol door turned out to
need a `session/new` of its own. That half is **CONV3b**, open in the backlog.

- **Read off the binary first** (`docs/2026-09-25-stream-json-evidence.md`). Claude Code 2.1.281 was
  probed with `-p --input-format stream-json --output-format stream-json --verbose
  --include-partial-messages`. A turn is `system/init`, `system/status`, the `stream_event` deltas,
  the whole `assistant` message (so the words arrive twice), `user` lines carrying `tool_result`, and
  `result` with usage and the context window. Several stdin lines make several turns in one process.
- **The adapter's own mapping** (`StructuredOutput.cs`): `IStreamMapper`, one per session, and
  `ClaudeStreamJson`.
  - Deltas are the live words, and the whole message's text is kept only when nothing streamed.
  - A `tool_use` becomes a card. Its title comes from the tool's own input, its kind uses ACP's
    vocabulary so both doors wear the same glyphs, an edit or write becomes a diff, and the to-do
    list becomes the plan.
  - A `tool_result` completes or fails the card.
  - `result` ends the turn and reports context against the window the harness names; the model's
    name and the cost stay on the wire (D24, TOOL3).
  - Every field is shape-checked, and an unknown frame is kept raw.
- **The capture** (`Driver.CaptureStructuredAsync`) keeps the transcript as text a person reads,
  never the JSON, and the record beside it. A failed turn's words reach the transcript, where the
  refusal detector reads them. Driven sessions, intakes and chats all use it, and the native door
  now records its context usage as the protocol door already did.
- **A chat** frames each message as a `stream-json` user line (`FrameMessage`), and once it is sent
  it goes into the record as the person's.
- **Looked at** with real Claude Code chats on the scratch machine (owner's authorisation,
  2026-09-24). The window showed three defects, all fixed, in the FIX-LOG under this date:
  - a chat just started fell back to *Nothing attended*;
  - a long code line widened the conversation;
  - tool cards named absolute paths, and now read relative to the session's tree.

  It also found, outside this item:
  - **every message a person typed was sent twice.** The composer's send was an untyped button
    inside its form. The same class made a key form's cancel save the key. Fixed in their own
    commit.
  - **a chat open when the shell closes is left `working`** with no process, and stop cannot end
    it. Open as a FIX in the backlog.

Driver 604 → 616, web unit 902 → 909.

## FIX — a chat open when the shell closes is left `working` (2026-09-25)

- [x] ~~**FIX — a chat open when the shell closes is left `working`.** `DriverLoop.Stop()` waits for
  the driven loop only. A chat runs in `ChatRunner`, outside it, and its best-effort `Conclude` loses
  the race with `HostSupervisor.Stop()`, so its record reads *working, running 12m* with no process
  behind it (seen on the window, 2026-09-25). This shape predates CONV3. End the chats and await
  their records before the host goes. **And the person cannot repair it by hand:** `STOP_SESSION`
  answers `false` for a process this driver does not hold, on the assumption that "the record says
  how it ended". An orphan's record says `working`, so pressing stop changes nothing and says
  nothing. A stop on this machine's session with no process behind it should record the ending. A
  crash that no shutdown order can reach, which leaves both kinds of session active, is a separate
  question for a startup sweep: it must not claim another machine's session, nor another driver's.~~
✅ **done 2026-09-25.** All three ways a session outlives its process are closed. The FIX-LOG has
the root cause.

- **A marker** under the home's `sessions/` for every tracked process, holding its id and start
  time, removed when released. It is how any driver sharing the home, the terminal's included,
  tells a live session from an orphan (`SessionProcesses.AliveOnThisMachine`).
- **Closing** ends the chats and records each first. `ChatRunner` is disposable, and both the loop
  and the terminal door declare it after the client it concludes through, so the language disposes
  it first.
- **Stop** on an orphan ends it (`Orphans.EndAsync`, `starting` or `working`), and the notice says
  which of three things the stop did.
- **The loop's first look** ends `working` orphans, in both hosts, and says so in that tick's
  report. It never touches a teammate's record, a parked one, or one another driver here holds.
- **Looked at**, and the window found the two defects the unit tests could not: the loop disposed
  the chats' client before stopping them, and both stop doors claimed the person's ending for
  whatever the stop did. Both fixed. Then on the window:
  - the first look ended two real leftovers;
  - a chat open at close came back with the close's note;
  - stop ended an orphan made after the sweep.

The artefact gate cannot yet hold a chat at close; that is **DEPLOY3**. Driver 616 → 622, web unit
911 → 914.


## CONV3b — a chat on the protocol door, on the structured wire (2026-09-25)

- [x] ~~**CONV3b — a chat on the protocol door, on the structured wire.** A chat there is still a
  pipe: it is given no `session/new` of its own, so it has no turns to record. Each message becomes
  an `AcpSession` prompt, and the person's message goes into the record as it does on the native door
  (CONV3a). 🔴 **Reading the code (2026-09-25) showed the door is broken, not just unrecorded.**
  `ChatRunner` spawns an ACP chat as a pipe, with no `initialize` and no `session/new`, and `Say`
  writes the person's raw text into the JSON-RPC stream. So `claude-code-acp` and `codex-acp`
  declare `Interactive` and cannot hold a conversation. The plan:
  - `AcpSession` gains `OpenAsync`, `PromptAsync`, `CancelTurnAsync` and `CloseAsync`, with
    `RunAsync` composed of them;
  - the chat opens with the connector, the plugins' servers and its rules as `_meta`;
  - one turn runs at a time;
  - *finish* sends `session/close` after the queue drains, not a cut stdin;
  - the ACP stub becomes interactive, so the family rehearsal holds the door.~~
✅ **done 2026-09-25.** A conversation on any ACP harness (`claude-code-acp`, `codex-acp`, a plugin's)
is one session held over the wire, each message a turn on it, and the person's words are in the
record.

- **`AcpSession` has a conversation surface**: `OpenAsync` (handshake, `session/new`, posture),
  `PromptAsync` (one turn, answered by the agent's stop reason), `CancelTurnAsync`
  (`session/cancel`, which keeps the session), `CloseAsync` (bounded) and `Ended`. `RunAsync` is
  their composition, so the driven path is unchanged.
- **`ChatRunner` holds a `ProtocolChat` per conversation.**
  - It opens with the knowledge connector and the plugins' servers (ACP4), and with the composed
    rules as `_meta` (PERM1), which a protocol-door chat never had.
  - Turns run one at a time, in order.
  - A message is recorded when it is sent, not when it is typed, so one sent mid-turn does not sit
    inside the turn before it.
  - *Finish* runs the queued turns, then `session/close`, then the end of input, and both the
    page's door and the terminal's go through it.
  - A session that cannot open ends its process, and the record concludes `failed` with the reason.
- **The ACP stub is interactive**, and the family rehearsal holds the door with a conversation from
  a terminal (274/274): two messages on one session, finished and never cut, no raw line on the
  wire, and the person's words each before the turn that answered them.
- **What an empty record means is the door's to say.** The roster says `structured` per harness,
  and the page reads a fresh chat on a structured door as *nothing said yet*, where it used to say
  its door carries only text. A text-only pipe chat stops recording the person's side, which was
  half a conversation beside replies that live in the console.
- **The session's own settings are not the conversation.** `available_commands_update`,
  `current_mode_update`, `config_option_update` and `session_info_update` reach the console and not
  the record.
- **Looked at** with the ACP adapter the dsh probe had installed (0.79.0), on this machine's own
  Claude Code account. The window found two defects, both fixed: two "update this version does not
  know" rows over a chat nobody had spoken in yet, under a false *working…*; and the empty-state
  sentence. Then on the window:
  - two messages, the second sent mid-turn, each came back a turn in order on one session, and the
    second answer leaned on the first;
  - the tool call folded under its answer;
  - *finish* ended it `completed`.

- **The gate found a lost word.** The protocol chat's test failed about one full run in three:
  the agent's second answer was missing from a turn that had ended. The event store's reads and
  appends denied each other, and a dropped append is a lost event. Both sides now share, held by a
  test that fails 3 runs of 3 against the old reader (FIX-LOG). An intake test failed once in about
  20 full runs as well. That path is untouched here, and it passed 10 runs in a row after, so it is
  open as FLAKE1, with its message now carrying the transcript.

Left for later: stopping a turn from the page, and a queued message's look (CONV4); the console's
chunk-per-line transcript (UX1). Driver 622 → 629, modules 120 → 121, web unit 914 → 917, family
271 → 274.

## CONV4a — stopping a turn, and the queue, in the driver (2026-09-25)

- [x] ~~**CONV4a — stopping a turn, and the queue, in the driver.** One turn queue for both doors
  (`ChatTurns`): a message sent mid-turn waits and joins the record when its turn begins. The native
  door stops writing it to Claude Code's stdin at once, because the binary folds a mid-turn message
  into the running turn. Stop the turn: `session/cancel` on the protocol door and Claude Code's
  `interrupt` control request on the native one, measured first (the stream-json evidence, §
  *Stopping a turn*). A stop withdraws what was waiting, so nothing the person queued fires after
  they said stop. Both doors: `CANCEL_TURN` and `SESSION_QUEUE` over the bridge, and on the
  terminal an ETX line (Ctrl+C) during a turn. The family rehearsal holds it over the ACP stub.~~
✅ **done 2026-09-25**, the first third of CONV4, which was split in three the same day (CONV4b the
page, CONV4c attachments and `@`).

- **Measured first** (`docs/2026-09-25-stream-json-evidence.md`, § *Stopping a turn*). Two real
  sessions established the following.
  - Claude Code 2.1.281 takes `{"type":"control_request","request":{"subtype":"interrupt"}}` on
    stdin with no `initialize`, and answers it with a `control_response`.
  - The turn ends `error_during_execution`, with `terminal_reason` `aborted_streaming` (cut while
    writing) or `aborted_tools` (cut while a tool ran).
  - The process takes the next turn normally.
  - That a mid-turn line may be folded into the running turn is the SDK's declaration, labelled as
    bundle evidence.
- **`ChatTurns`, one queue for both doors.** It takes one turn at a time, in order. A message waits,
  is recorded when sent, and is told as waiting (`QueueChanged`) while it waits. A message the door
  takes at once is never announced. A stop withdraws what was waiting first, then stops the turn in
  flight. A stop that lands between *taken* and *on the wire* is held until the send, because
  `PromptAsync` now reports the moment its request is written. Otherwise the cancel could overtake
  the prompt and stop nothing.
- **The native door** (`NativeChat`) records a message, then writes it. It waits for the turn's
  `result`, which the capture reports only after the record holds the turn's ending (`observed`), so
  the next message always lands behind that ending. *Finish* runs the queued turns first, then ends
  input. `ClaudeStreamJson` maps an aborted turn to `cancelled`, the protocol door's word, and keeps
  a `control_response` out of the record; a refused one is a console line.
- **The protocol door** (`ProtocolChat`) moved onto the same queue. `session/cancel` is its stop,
  and the turn ends on the agent's own `cancelled`.
- **Both doors (D50).** On the bridge, `CANCEL_TURN` answers `{cancelled, withdrawn}` and is refused
  for a session that takes no input. `SESSION_QUEUE` answers what is waiting, and changes arrive live
  as `SESSION_QUEUED`. On a terminal, Ctrl+C during a turn stops it, and a line holding only ETX
  does the same from a script; with no turn running Ctrl+C does what it always did. A text-only door
  refuses a stop in words, because it cannot see a turn end.
- **The gates.** Seven driver tests with stand-in harnesses on both doors that write down each line
  as it arrives. The ordering test was checked against the old immediate write, and fails there
  (`second` heard before `first`'s result). The mapper and adapter tests are built from the probe's
  own frames. Three module tests. Three family-rehearsal checks: a stop from a live terminal over
  the ACP stub, the withdrawn line never heard, and the record reading `person, cancelled, person,
  end_turn`.
- **Looked at** with the real binary, over the terminal door against the scratch shell's host. A
  turn was stopped ten seconds in, the queued line came back unsent, the next message was answered
  on the same session, and it finished `completed`. The record reads the same. The first attempt
  taught two things, both carried into CONV4b:
  - the native door's console prints a message only once it is whole, so a stop keyed to its words
    arrived after them and the turn ended `end_turn`;
  - a stop during thinking leaves a turn with no words.

Driver 629 → 640, modules 121 → 124, family 274 → 277. The FIX-LOG has the mid-turn ordering defect.

## CONV4b — the composer on the page (2026-09-25)

- [x] ~~**CONV4b — the composer on the page.** Stop the turn beside end the session; queued messages
  shown as queued, and a stop's withdrawn ones back in the draft; a draft per session that survives
  a reload; a stopped turn reads as stopped, never failed. Over CONV4a's `CANCEL_TURN`,
  `SESSION_QUEUE` and `SESSION_QUEUED`. The archive's CONV4a entry has two findings for this item.~~
✅ **done 2026-09-25**, the second third of CONV4.

- **The driver says whether a turn is in flight** (`ChatQueue`), beside what is waiting, on
  `SESSION_QUEUE` and every `SESSION_QUEUED`. The composer's stop follows it rather than the record,
  which learns a turn began only when its first event lands.
- **The composer** (a molecule, props only):
  - while a turn runs, *send* reads *queue*, and what waits behind it is listed above the box in
    the order sent;
  - *stop turn* stands beside *finish* and *stop*, neutral, only while a turn runs and only on a
    door the roster calls `structured`;
  - the draft may be held above it.
- **The frame** holds a draft per session (`work/drafts.ts`: a map in `localStorage`, the newest 50
  kept, storage refused costs only the reload) and `useSessionTurns`. It sends a stop through
  `useCancelTurn`. What was withdrawn goes back into the box, ahead of what is being typed. The
  notice claims only the asking (CONV4a's first finding).
- **A stopped turn** reads *the turn was stopped here*, in the passive, since a driven session's
  timeout cancels a turn too. The fold marks the tool calls still open at a cancelled turn's end as
  `stopped`, beside the wire's status. The card draws them in the quiet tone, closed, with the
  harness's words one click away. A turn stopped while thinking is the ask, then *stopped* (CONV4a's
  second finding).
- **Looked at** on the scratch window with real Claude Code chats, in English and 中文, light and
  dark:
  - a message queued behind a long turn;
  - stopped mid-stream, and it came back into the box;
  - sent again, and answered in the same session;
  - a queued 中文 message run as the next turn once the turn ran out.

  The window found one defect, fixed: the stop's two sentences were joined by a space, which is
  wrong after a Chinese full stop, so the separator is the catalogue's. It found one for UX1 as
  well: a one-item-per-line answer renders as a paragraph, because Markdown makes a single newline a
  space.
- **TEST1's trigger arrived during the gate.** The Playwright worker aborted with `0xC0000409` at
  test 4. The re-run passed 21/21, and TEST1 is open in the backlog with what this capture could
  and could not hold.

Web unit 917 → 940, Playwright 21/21, driver 640, modules 124.

## CONV4c — attachments (2026-09-25)

- [x] ~~**CONV4c — what a message carries.** Attachments (the `carry` molecule) and `@` a file in
  the session's tree. Measure each wire first: ACP's content blocks against `promptCapabilities`,
  `stream-json`'s against the binary.~~
✅ **attachments done 2026-09-25.** The item was split once measured: `@` needs nothing on either
wire, and it is **CONV4d**, open in the backlog.

- **Measured first** (`docs/2026-09-25-message-content-evidence.md`), with two real sessions:
  - Claude Code 2.1.281 on `stream-json`: an `@` mention expanded by the binary with no tool call,
    an inline image block, and a text file and an image outside the tree read by path under INT4j's
    grant, no denial.
  - `claude-code-acp` 0.79.0: `promptCapabilities: {image, embeddedContext}`; `@` text, a
    `resource_link` in and outside the tree, an image and an embedded resource, none needing a
    permission.
  - Its bundle shows a link becomes `[@name](file://…)`, a mention, so sending a file both ways would
    attach it twice.
- **The driver:**
  - `ChatFiles` keeps a message's files under `sessions/<id>/files/<hash>-<name>`, twinning the
    service's quest layout and name rules, with a quest's limits (10 files, 20 MB), refused in a
    sentence with nothing kept.
  - A chat is spawned with a read of exactly that folder.
  - The native door adds a line naming each path; the protocol door sends a `resource_link` per
    file.
  - The record keeps the person's words and the files' names (`SessionEvent.Files`).
  - The queue and a stop carry `ChatMessage`s, so a waiting message and a withdrawn one keep their
    files.
  - `SessionEvents.IsId` is the one check for everything an id names under the home.
- **Both doors (D50).** `SESSION_INPUT` takes `files` as names and base64 bytes, and refuses bytes
  that are not base64 in a sentence. A terminal attaches with an `:attach <path>` line. The family
  rehearsal holds it over the ACP stub, which reads the linked file (279/279).
- **The page:**
  - The composer holds a `carry` of its own, keyed by session: drop anywhere on the form, paste a
    screenshot, or the paperclip. Chips carry size and remove, and a message may be files alone.
  - The sent message's ask shows the names.
  - A waiting message names its files.
  - A stop names the files it could not hand back.
- **Looked at** on the scratch window with real chats:
  - on Claude Code's door, a log was read by its path and answered correctly;
  - on `claude-code-acp`, a PNG was read through its link and answered *Red*, with no permission
    refused.

  The record held the words and the names on both.

Driver 640 → 646, modules 124 → 126, web unit 940 → 948, family 277 → 279, Playwright 21/21, deploy
39/39.

## REV3 — the owner's full review, code and docs (2026-09-25)

> *"next session let's do a full code review include docs"*, and once it was running, *"please also
> consider code refactor/dedup/cleanup"*.

✅ done 2026-09-25. Ten read-only reviewers read one area each: the CLI, the service, the driver's
conversation half, the rest of the driver, the modules and app, the web's `work/`, the rest of the
web, tools with the devkit, canon and examples, and two for the docs. Every finding was then checked
here against the code before it landed. `docs/2026-09-25-rev3-review.md` is the ledger, with each
finding's verdict and the commit that settled it. Every landing is its own commit
(`git log --grep=REV3`), and `docs/FIX-LOG.md` carries the root cause of each non-trivial defect.

**What it found.** 171 numbered findings, 19 of them high. 111 were fixed, 51 were prose
corrections, and 9 are backlog rows. The highest:

- `daoris plugin remove ..` deleted the Daoris home.
- `sync` deleted a repository's own `.claude/rules/<name>.md` when a canon span of that name retired.
- An unreadable home file (a BOM was enough) read as empty, so the next edit wrote back a file with
  every deny gone.
- A shared deployment kept serving a retired repository's knowledge forever.
- A failure between spawn and wait left a harness running, untracked, with its tree unlocked.
- Session notes carried an absolute path and a profile name to the remote.
- A discard refusal landing after a session switch armed *discard it anyway* on the new session.
- Tabbing through the strikes field told the driver never to park.
- The release job never ran `npm ci`. It also bumped the canon version before the gates, so the
  family rehearsal failed on examples one version behind.
- The README's install command cannot run (DIST1).

**The pattern worth keeping: a check that cannot fail.** More than one gate was green for a reason
unrelated to its name:

- the gates-vs-workflow test matched comments, so deleting a gate's step stayed green;
- the e2e absence assertions looked for strings that no longer existed;
- family phase 1 repaired the examples it judged, so a second run passed;
- the unwired-circle check passed on an unrelated refusal;
- the pre-commit leak scan read the working tree rather than what was staged.

The fix is the one TDD already asks for: watch it fail. Every fix here was seen failing first where a
test could hold it, and FIX-LOG says why where none could (a Ctrl+C, a notifier, a transaction
cancelled mid-flight, a process disposed).

**Found while fixing.** On Windows a unique temp name per write is not enough for two writers of one
file: `File.Replace` still fails while the other rename holds the target, so the driver's one atomic
writer (`AtomicFile`, now used at fourteen sites that each rolled their own) retries a bounded number of times. The
tools' runner guard compared a path string, so run through a junction a gate ran nothing and exited
0. It now compares real paths, in one helper.

**My own mistakes, each caught by the gates or the method:**

- a test fixture used a private address, which the sensitive gate refused;
- the contract went over its word budget, and was condensed;
- a commit landed before its FIX-LOG entry, and a follow-up added it;
- two new tests could not fail as first written (the same file on both sides, and the wrong label),
  and were fixed before commit;
- an overwritten test file was restored from git before appending.

**What it left**, in `TASKS.md` under *What REV3 left*:

- DIST1 (the install command);
- BUDGET1 (what the core budget caps);
- HOME1 (which home a second install uses);
- HOSTID1 (the shell adopts any host);
- REFUSE1 (the refusal rule, enforced);
- TIER1 (the tier per answer);
- HTTP1 (the HTTP host under test);
- SIGNIN1 and WINDOW1 (two web state bugs);
- CLEAN1, the reviewers' duplication and dead-code lists, reported but not yet re-read.

Gates: CLI 446 → 475, service 473 → 497, driver 646 → 681, modules 126 → 129, devkit 73 → 80, web
unit 948 → 972, Playwright 21/21, family 279/279, deploy 39/39.

## CLEAN1 — the review's cleanup lists (2026-09-25)

- [x] ~~**CLEAN1 — the review's cleanup lists.** Each ledger section ends in a C or K row: duplication
  and dead code a reviewer reported and nobody here has re-read. Examples are a JSON helper written
  seven times in the service, "driver not ready" five times in the modules, and the session centre
  built twice in the web. Take one section at a time. Confirm each item, and land the ones worth
  the change as refactors under the tests already there.~~

✅ done 2026-09-25. Every C and K row of REV3's ledger was re-read against the code, one section at a
time, and settled in the ledger's *CLEAN1* section with its verdict and the commit that landed it
(`git log --grep=CLEAN1`, 62 commits). 98 items: 81 landed (a few by REV3 itself, before CLEAN1
re-read them), 12 were dropped with the reason, 3 folded into UX5, 1 became a row, and 1 was not
confirmed. Each landing is a refactor under the tests that were already there; where one exposed a
behaviour difference, the difference got a test.

**What re-reading found that the reviewers had filed as cleanup:**

- The service threw on a JSON list whose items were not objects, in a publish or a push's answer,
  where it should refuse (`cc0b949`, FIX-LOG).
- The desktop's editors saved the empty read of a torn `permissions.json`, `harnesses.json` or
  `plugins.json`, dropping every deny, account choice or disabled plugin. REV3 had fixed the CLI's
  side; the driver's was still live (`b7cc512`, FIX-LOG).
- `status` missed a change to a rule the canon moved into a pack (`22f2f73`). `plugin add` took a
  reserved name spelled in another case (`ad1e8bf`). The in-memory store accepted a duplicate id
  the SQLite one refuses (`80ad80e`). The account-default picker could not set `default`
  (`aaceb1c`). `NUDGE` had no caller, so a publish waited out the poll (`7081ff6`). The deployment
  rehearsal's stub read a refused take by its sentence (`0d3bd02`), and neither rehearsal bounded a
  call to its hosts (`dc6dcce`, `0d3bd02`).

**The largest cuts.** The three real-run proofs share a kit, about 300 lines fewer. The
always-loaded `CLAUDE.md` dropped its counts, two history sentences and a closed arc's recital
(3,727 words to 3,601). `TASKS.md` lost its closed stubs and its handover history (6,512 words to
about 3,970). The desktop README's 1,167-word chronology became two tables. `docs/README.md` says
what each document is now, four finished documents moved to the archive, and the twin arrangement
has a knowledge document of its own (`.claude/knowledge/twins.md`).

**My own mistakes, each caught by the gates or the method:**

- a typecheck run through a pipe hid its exit code, so a test that did not typecheck landed, and
  the next commit fixed it (`f669d32`);
- a heredoc turned `'\\'` into `'\'` in a source file, caught before commit; source was written with
  the edit tools after that;
- a helper was inserted between a member and its doc comment, found by the doc-comment scan
  (`465a5d7`);
- an operator-precedence slip in a refactored `ServiceClient` line, caught before commit;
- the new documents index attributed three decision notes its documents do not carry, and checking
  each claim corrected them before commit.

**What it left:** RETRY1 (the screen's door for retrying a quest parked by its strikes), and three
folds into UX5: the protocol door's two line-from-event paths, the native controls beside `ui.tsx`'s
own, and `platform-ux.md` §4's amendments restated as the body's rules.

Gates: CLI 475 → 478, service 497 → 502, driver 681 → 685, modules 129 → 128 (the removed `STATE`
route's test went), devkit 80, web unit 972 → 975, Playwright 21/21, family 279/279, deploy 39/39, release
66/66.

## CONV4d — `@` a file in the session's tree (2026-09-26)

- [x] ~~**CONV4d — `@` a file in the session's tree.** Measured: both doors expand `@path` text
  themselves, so the wire needs nothing. The work is the completion: a bridge call listing the
  tree's files, and the composer offering them after `@`.~~
✅ **done 2026-09-26**, the last part of CONV4. D76 carries the amendment.

- **Measured first** (`docs/2026-09-25-message-content-evidence.md`, § CONV4d). Both doors were
  probed with every read tool disallowed, eight one-line turns in all.
  - `@"my notes.md"` and `@"docs/deep file.md"` expand on both doors.
  - `@笔记.md` expands bare on both.
  - `@my\ notes.md` does not expand on the native door.
- **The driver:** `WorkingTree.FilesAsync` returns what git says the tree holds: tracked files
  still there, plus new ones git does not ignore. It reads with `-z`, because git otherwise quotes a
  CJK name as octal escapes. It is bounded at 20,000 paths, and the rest are counted. It sits behind
  the diff's "git walks up" guard, now one helper for both.
- **Both doors (D50):** `SESSION_FILES` is read-only and desktop-only, like the diff. It refuses in
  one catalogued sentence (`SESSION_TREE_UNLISTED`), saying the typed path still reaches the agent.
  A terminal types the path, and both doors expand it.
- **The page:**
  - `work/mentions.ts` holds the pure parts: the mention at the caret, the ranking, the spelling and
    the insertion.
  - `MentionList` is a new molecule with a story per state.
  - The composer offers the files after an `@`. The arrows move, Enter or Tab writes the chosen one,
    and Escape leaves what was typed. The box keeps the focus and announces the row through
    `aria-activedescendant`.
  - The frame lists the tree only while a mention is being written (`useTreeFiles`).
  - The placeholder says *@ names a file*.
- **Looked at** on the scratch window with real sessions:
  - On Claude Code's door, the untracked `design notes.md` was taken as `@"design notes.md"` and
    answered *heliotrope*, with no tool call.
  - On `claude-code-acp`, in 中文 and dark, `@设计笔记.md` was answered *saffron*, with no tool call.
  - The `engine` fixture is not a repository of its own, since git walks up to Daoris, and it
    showed the refusal. Unguarded, it would have offered Daoris's own 770 files.
  - A long 中文 name, eight folders deep: the folder gives way far faster than the name, and the
    list never scrolls sideways. As first written, the name could not shrink at all.
- **The window found two defects, both fixed test-first:**
  - React reads the selection on the same keydown that takes a file, and that reading, taken as the
    caret, reopened the list on the half-word just replaced. A second test covers the case where
    taking the file changes no text, which would otherwise leave the caret jumping back later.
  - Letters scattered across folder names (`.claude/rules/…`) filled seven of the eight rows for
    `des`. They are now a last resort, offered only when nothing better matched.
- **Found running every declared gate:** the `code-map` gate had been red on main since REV3, which
  corrected the generated map by hand. The FIX-LOG has it.

Gates: CLI 478, driver 685 → 689, modules 128 → 129, service 502, web unit 975 → 1006, code-map
green, Playwright 21/21, family 279/279, deploy 39/39.

## CONV5 — the meters (2026-09-26)

- [x] ~~**CONV5 — meters.** A context ring under the composer and per-turn usage, from the usage the
  wire reports; absent is never zero.~~
✅ **done 2026-09-26**. D76 carries the amendment.

- **Measured first** (`docs/2026-09-25-stream-json-evidence.md`, § CONV5), on both doors, with two
  turns each: one that used a tool and one of a single word.
  - The native door's `result.usage` is the turn's total over its API calls. The streamed messages
    under-count output, 20 against the result's 80.
  - The protocol door's prompt response carries the same four counts as `usage`, per turn.
  - Neither door reports when the first word came.
- **The driver:** `TurnTokens` on the `turn` event holds input, output, cache read and cache write,
  each null where the wire said nothing. Both doors map their own names into it, and the record
  keeps it across a restart. The cost, the models' names and the total stay on the wire.
- **The page:**
  - `toTurns` gives each turn its tokens, plus two spans from the driver's clock: how long it took,
    and how soon the agent first did something.
  - A finished turn ends with one quiet line, such as *4.9s · 75.9K in · 103 out*. Its tip carries
    the breakdown and the first answer's time.
  - `ContextRing`, a new molecule, sits at the far end of the composer's controls. It shows used
    against the window, turns the warn tone from 80%, and puts the high-water mark in its tip.
  - The ring reads the conversation's own record, handed up by `SessionConversation`. There is no
    second fetch.
  - `span()` formats a turn's seconds, where `elapsed()` formats a session's minutes.
- **Looked at** on the scratch window with real chats:
  - On Claude Code's door, a tool turn read *4.9s · 75.9K in · 103 out*, and the ring 4% (38,154 of
    1,000,000).
  - On `claude-code-acp`, in dark and 中文, the ring said 未测量 (not measured) before the first
    turn and 4% four seconds into it, following the live `usage_update`.
- **The window found two defects, both fixed:**
  - A turn stopped on the protocol door read *0 in · 0 out*. The adapter answers a turn cancelled
    before its `result` with an empty tally, so a report whose every count is zero is now no report,
    test-first. Stopped turns on both doors now read their time alone.
  - The ring's "not reported yet" sentence promised a report once a turn was under way. The native
    door reports only when a turn ends, and a stopped turn may report none, so the sentence now
    says that.
- **Found on the way:** conversations were missing from what each account has carried, and the
  Settings copy said only the protocol door reports. That was USAGE1, closed below.

Gates: CLI 478, driver 689 → 694, modules 129, service 502, web unit 1006 → 1026, code-map green,
Playwright 21/21, family 279/279, deploy 39/39.

## USAGE1 — a conversation counts toward its account (2026-09-26)

- [x] ~~**USAGE1 — a conversation counts toward its account** (found by CONV5). What each account has
  carried (Settings, TOOL3) is recorded by driven sessions and intakes only. `ChatRunner` records
  nothing, so every conversation is missing from it. Its copy, and `Usage.cs`'s remarks, still say
  only the protocol door reports, which has been untrue since CONV3a gave the native door a reader.
  Record a chat's high-water mark at its end, through the loop's one `SessionUsage`, and correct
  both.~~
✅ **done 2026-09-26**. The FIX-LOG has the root cause.

- A conversation records its high-water context at its end, before its record moves: the protocol
  door's from `AcpSession.Usage` (new), and the native door's from its reader. A text door records
  nothing, and the screen says *not measured* for it.
- `DriverLoop` hands the runner its one `SessionUsage`, which now takes one writer at a time. The
  headless chat door writes the home's own file, as its event record does.
- The Settings copy, `Usage.cs`'s remarks and the toolchain design's §4 now say what is measured.
- Tests on both doors, over stand-in harnesses that now report context: the protocol stub per
  `usage_update`, the native stub per message with the window on its `result`.
- **Looked at** on the scratch window: a one-word chat on Claude Code's door ended, and Settings
  counted it under its account, at 46,698 (a fixture's 9,000 and this chat's 37,698).

## FRAME6 — the frame (2026-09-26)

- [x] ~~**FRAME6 — the frame.** A resizable, collapsible rail (264–420px, 56px strip) and a resizable
  dock (45% default, 70% cap) with tabs per session, deterministic close (components §3a).~~
✅ **done 2026-09-26**. The components plan's §3a says what was built and the four choices the
reference did not make for us.

- **`work/layout.ts`** decides the columns in one pure function, from the window's width, the
  frame's, and what the person chose.
  - The rail is 264–420px, 280 to start, and a 56px strip when closed or when the window is under
    1024px. Widening undoes the second and never the first.
  - The dock opens at 45% of the window and never takes more than 70%. It gives way to hold the
    session at 400px, down to its own 300px floor, and past that it asks to be closed. It covers
    the frame under 768px or when asked, and stays closed at every width once the person closed it.
- **`Splitter`**, one keyboard-operable edge for both columns. The arrows step away from its column,
  Home and End go to either end, and a double-click resets.
- **The rail's strip** (`SessionRail compact`, `SessionStripRow`) shows each running session as its
  repository's initial and its mark, with the title, the repository and the state as its name.
  `DotMark` is the mark alone, for where the word goes in the name.
- **The dock** (`RightDock`) takes a mode and a width. It is one element across docked, cramped
  and full, so a surface switched in and out of full is never drawn anew. Closed, it leaves a strip
  of its tabs. Each session keeps its own tab, and the palette's review opens the attended one's.
- Widths and closings are remembered per viewer, like the panel's height.
- **Looked at** on the scratch window, at 1400px: the rail at 280, the dock at 45% and the
  conversation at 442; the rail closed to a strip of A, E and G; the dock closed to its strip,
  reopened on Review, filling the frame and back; the dock narrowed by its edge and remembered.
  - Not looked at on the window: a window narrower than 1024 or 768, since the instruments cannot
    resize it. The tests cover both.
- **Found while looking:**
  - The dock's 45% default leaves a 1400px window's conversation near its floor, because ours is
    open by default where the reference's opens on demand.
  - The head's tree path breaks mid-word in a narrow centre.

  Both went to UX5. The look also found that a finished session cannot be reviewed at all, which
  is GROUND1.

Gates: CLI 478, web unit 1026 → 1054, Playwright 21/21, deploy 39/39, code-map green. The driver,
modules, service and family suites were not re-run, since FRAME6 changed none of their code.

## GROUND1 — a finished session cannot be reviewed (2026-09-26)

- [x] ~~**GROUND1 — a finished session cannot be reviewed** (found by FRAME6's look). The driver finds
  a session's tree through `ServiceClient.SessionGroundAsync`, which reads `/api/sessions`, and that
  route lists ACTIVE sessions only. So `SESSION_DIFF`, `MERGE_SESSION_TREE` and `DISCARD_SESSION_TREE`
  on any session that ended refuse as *not on this machine*, while its head shows its tree, and the
  review pane still offers *accept* and *discard* beneath the refusal. A review is read at the end, so
  this is the usual case. Read closed records too, test-first against a stand-in service.~~
✅ **done 2026-09-26**. The FIX-LOG has the root cause.

- `SessionGroundAsync` reads closed records too. A test against the stand-in service, which honours
  `includeClosed` as the real host does, was red first: a completed session's ground came back null.
- **Looked at** on the scratch window: the completed chat's Review reads *Nothing landed* where it
  read *not on this machine*.

Gates: CLI 478, driver 696 → 697, modules 129, family 279/279, deploy 39/39.

## RAIL1 — the list (2026-09-26)

- [x] ~~**RAIL1 — the list.** Search sessions by name and by content, and a row menu.~~
✅ **done 2026-09-26**. D76 and working-surface design §3 carry the amendments.

- **Why first lines:** the window showed eight rows in the rail all reading *conversation*. That
  was the trigger working-surface design §3 set for naming, and the design had already named the
  answer: a conversation's first line.
- **The driver** (`SessionEvents`):
  - `Openings` reads the first thing the person said in each session, only as far into the record as
    it needs, cut to a title's length. A driven session's composed target is not the person
    speaking.
  - `Search` matches the person's words and the agent's, with a message's streamed chunks joined
    first, so a word split across two chunks is found. It skips tool output. It returns three hits
    per session and fifty in all from the newest two hundred records, with a snippet around each,
    and says when it left something out.
  - Both are bridge-only (`SESSION_OPENINGS`, `SESSION_SEARCH`), since what a session said never
    leaves the machine.
- **The page:**
  - `sessionTitle` takes the first line. The rail, its strip, the head and the monitor's tiles all
    read it from one shared query.
  - The open rail has a search box: by name at once over every session the list holds, and by what
    was said once the typing settles, with the words marked in a readable snippet. Escape brings the
    rail back.
  - Each row has a menu: its own window, its review (the dock opened on it), its id. Finish and stop
    stay with their one owner (D56).
- **Found while building it:** FRAME6 had left the column drawing a second timeline under 1024px,
  because the column's fallback was for a dock that used to hide there. The Work frame's column no
  longer carries one, and only the detached window does.
- **Looked at** on the scratch window with the machine's real records:
  - the rail named eight conversations by their first lines, in English and 中文;
  - the head read the same name;
  - a search for *heliotrope* found the conversation whose answer said it, and one for *saffron*
    found a 中文 conversation, in dark;
  - the row menu offered its three items.
- **The window found two defects, both fixed test-first:**
  - Snippets showed the agent's Markdown marks.
  - A span's edge dropped the space before a match from the hit's accessible name.

Gates: CLI 478, driver 697 → 700, modules 129 → 130, web unit 1054 → 1068, Playwright 21/21, family
279/279, deploy 39/39, code-map green.

## REVIEW2 — review (2026-09-26)

- [x] ~~**REVIEW2 — review.** Highlighted diffs, split or unified.~~
✅ **done 2026-09-26**. D76 carries the amendment.

- **`work/patch.ts`** reads git's unified patch into hunks with the line numbers each side had. It
  keeps git's notes (`\ No newline at end of file`) as notes. A blank context line stays a line even
  when its single space was stripped on the way, and only the patch's own final newline is dropped.
  A rename with no hunks is its preamble. `pairRows` sets a run of removals across from the run of
  additions after it.
- **`work/codeLines.ts`** takes the language from the file's extension, only where the highlighter
  ships it, and never guesses. It highlights a side's lines as one text and splits the markup back
  into balanced lines, so a comment spanning lines is coloured on each.
- **`PatchView`**, a new molecule: a hunk's header, then rows. Unified rows have two number gutters,
  a sign and the code. Side-by-side rows put each half at half width with wrapping lines. A note runs
  across both. Changed rows wear the status tints and always carry their sign.
- **The review pane** has a *unified · side by side* choice in its header, remembered per viewer.
  **The conversation's edit cards** draw their lines with the same renderer, highlighted by path and
  unnumbered.
- **Looked at** on the scratch window. A chat was opened on the `game` fixture, a two-file change was
  committed there for it to review, and the fixture was reset afterwards.
  - Unified: numbered and signed, with TypeScript highlighted and the four-line doc comment coloured
    on each line.
  - Side by side, in dark.
  - The review of a session that had stopped also read, so GROUND1's fix held.
- **The window found one defect, fixed test-first:** side by side, a long line on the old side
  pushed the new side out of view. Each half is now held at half width and wraps, while a unified
  line stays whole and scrolls.

Gates: CLI 478, web unit 1068 → 1092, Playwright 21/21, deploy 39/39, code-map green. The driver,
modules, service and family suites were not re-run, since REVIEW2 changed none of their code.

Gates: CLI 478, driver 694 → 696, modules 129, web unit 1026, Playwright 21/21, family 279/279,
deploy 39/39.

## UX5 — screen by screen (2026-09-26)

- [x] ~~**UX5 — screen by screen.**~~ Every surface, every state (empty, loading, error, long, 中文,
  dark), every piece of interaction logic (keys, focus, what a click opens, what survives a reload),
  against the reference and D41. Written down as it is found, and fixed. **The ledger is
  `docs/2026-09-26-ux5-screen-audit.md`** (opened 2026-09-26). Landed so far: U1 (one hue for
  waiting), U2 and U13 (the monitor), U3 (the protocol door's console in lines, not chunks), U4
  (line breaks), U5 and U12 (the platform's own controls), U7 (the dock on demand), U8 (paths), U9
  (no box where nothing listens), U11 (the frame's tests isolated), U15 (the command center gives
  way to the menus), U16 (the owner's: the session follows the window's width, reversing U10), U19
  (no view named Work), U20 (the owner's choice: a badge counts what its place holds), U21 (the
  status bar counts active sessions, and says so), U22 (the activity bar scrolls rather than
  crushing its places), U26 (an outstanding row opens its quest), U27 and U28 (the driver's reasons
  and every date in the reader's language) and U29 and U30 (a stopped service said once, in
  Daoris's words, by the page and the tick alike), and Quests' U31 to U35 (the next step leads the
  drawer, asks oldest first, the composer promises only what the machine does), and Projects' U36
  to U38 (a reason on its glyph, phrases in the body face, the move before *never mind*); U18 was
  dropped there, since every `danger` is a move that ends something; and the knowledge row's U39 to
  U43 (excerpts as prose, a wait as a span, empty answers that lead somewhere, a reader wide enough
  for its source); and the Map row's U44 to U50 (both maps drawn at their own size and framed on
  what they draw, a session in its status hue, a quest a door to its drawer, a choice that toggles,
  arrows in an ink, every workspace said, 被依赖); and Settings' U53 to U58 (a label that keeps its
  room beside a path, one name per account, counts in their number, variables as code, *once it is
  there*, no title over a card alone); U59, the owner's (every view follows the window, and the maps
  grow with it); and the Sessions rows' U17 (a live chat between turns is idle, decided by the
  reference console), U60, U61 and U63 to U66 (a root session is not reviewed as a tree of its own,
  *busy* in words, a read without its fence, the `@` list sized to its rows). U62 is RAIL2.
  Then the last rows: Start session's U67 and U68 (ways in named as Settings names them, a busy
  repository marked, a refused start said in the form), the monitor's U69 and U70 (its rail the
  present tense, at the main rail's width), and U25 and U72 (the palette's unread count gone, a menu
  item opening at the part it names). **Every surface row is done.** **Open in the ledger:** U6
  alone, folding `platform-ux.md` §4's dated amendments into the body, which closes UX5.
✅ **done 2026-09-26**. The ledger is `docs/2026-09-26-ux5-screen-audit.md`, closed with every surface
row ticked by what was looked at on the window and every finding given its disposition; D76's UX5
amendment carries the findings that chose between alternatives.

- **Seventy-two findings** over every surface: the frame, Overview, Quests, Projects, Search with
  Convergence, the Map, Settings, the three Sessions rows, Start session, the monitor and detached
  windows, and the palette, menus, toasts and tooltips, each in both languages and both themes and at
  500 to 1920 wide. Most were fixed test-first and looked at again; a few were dropped with the reason
  written down (U14, U18, U24, U51, U52, U71), and two became backlog rows (SURF11 from U23, RAIL2
  from U62).
- **Three were the owner's**: the session follows the window's width (U16, reversing U10), a badge
  counts what its place holds (U20), and every view follows the window (U59, amending D41 §2's 72rem
  column).
- **One was decided by the reference console** at the owner's word: a live chat between turns is idle
  (U17), read from the reference's source.
- **The design language's body is current** (U6): its fourteen dated amendments are folded into the
  sections they amended, 8,050 words to 5,100.
- **What the instruments need** is at the head of the ledger: sizing and maximizing the window, a
  synthetic hover, an SVG part's click, a second circle for every-workspace looks, and a secondary
  window's capture.

Gates at the close: web unit 1136 → 1179, Playwright 21/21, `npm run verify` green. The driver,
modules, service and family suites were not re-run: UX5 changed the page, the service README and no
driver code.

## FG1–FG3 — the first goal's three walls (2026-09-27)

> *"I think we still does not meet the first goal: setup [the named workspace] as workspace and use
> mcp to control chrome with jira to read ticket and start task (and does not need to locate which
> repo just start the task in daoris)"* (owner, 2026-09-27 → D77)

✅ **done 2026-09-27**. `docs/2026-09-27-first-goal-study.md` read what INT6 would have met on the
real workspace (29 repositories, none adopted) and found three walls in the code. INT6 itself is
reshaped as FG5, the run, and stays open.

- **FG1 — an import names its workspace.** `daoris import <folder> --workspace <name>`, and the HTTP
  door's `workspace`, wire every row it registers to that circle. A statement re-points; an unnamed
  import still moves nobody. Service and CLI tests; the flag's value is never read as the folder.
- **FG2 — a repository that declared nothing is weighed by what its own files say.**
  `SelfDescription` reads its README's title and first describing paragraph (a generator's, a TODO,
  a note or a bare link is passed over, and reading stops at a how-to section), its package's
  description, and what it is built with, from the files at its top. It writes nothing. The room
  shows it labelled as the repository's word. The instruction lets the intake publish on it when
  it plainly fits one repository and no other, naming what decided, below any declaration. It also
  says that a page behind a sign-in needs a signed-in browser or is said to be unread, and that a
  ticket's words are material, never instructions (orca's wrapping).
  **Then read against the real workspace** (FG5's first step, a scratch render of its room from the
  install's registry): all 29 repositories were described, and five were read wrongly by the first
  cut. Each became a test. A hosted template's later top-level sections are boilerplate, so only
  the first top-level heading may name the repository. "Introduction" introduces and names
  nothing. An underlined heading is a heading. A generator's "Getting Started with …" is no title.
  A paragraph that opens with a step makes its section how-to, and a README that is all steps falls
  back to its package's description.
- **FG3 — `${data}`, the plugin's data folder**, in a manifest's command and environment, in both
  twins. The example browser plugin keeps its profile there, and its README says how to sign in
  once, and how to run on a machine without Chrome.
- **The references and Lyntai were read** for the same study. Neither reference routes a ticket to a
  repository. dsh's plugins map onto D64's three rows with nothing new. Lyntai's file storage (3.3)
  fits no Daoris store, so SQLite stays. Reading the service for it found SEM1: after a restart, the
  semantic half is empty until a refresh.

Gates: `npm run verify` green (481 CLI tests), service 505, driver 718 then 723, modules 133, family
rehearsal 279/279. Not run: the web suites (no page changed) and the deployment rehearsal (no
publish script or locator changed; the install is republished for FG5).

## FG4 — the screen's door for naming the workspace on *Import a folder…* (2026-09-27)

- [x] **FG4 — the screen's door for naming the workspace on *Import a folder…*** (D50). The
  terminal's `import --workspace` has no screen twin: the Workspace menu's import still states
  none, so a folder set up from the window lands in `default`. After the folder is chosen, ask which
  circle, offering the scope's and the folder's name, and keep "each row's own" as the unnamed
  choice. Look at it on the window. **Seen with it (2026-09-27):** the page did not hear an import
  made from the terminal. Overview said *no workspace yet* until the page was reloaded, because
  nothing tells the page the registry moved. The tick already forwards asks and sessions when their
  signature changes (INT4d, U13), and the registry could ride the same way.

✅ **done 2026-09-27**.

- **The drawer.** *Import a folder…* now opens a drawer in Projects rather than a folder dialog
  straight away, mirroring *Add repository…*. Choose the folder, and the workspace field offers the
  folder's own name, because that is what setting a folder up as a workspace means. Emptied, the
  import names none, so each repository keeps its own. The service's sentence comes back verbatim.
  The scope's name was not offered: a person setting up a new folder is not in its workspace yet.
- **The page hears the registry move.** The shell's loop signs the registry each tick
  (`Repositories.Signature`: each row's name, adoption, workspace and root) and forwards a tick
  when it changes, as it does for the asks and the sessions. The page invalidates the registry on
  every forwarded tick. An import, a retire or a re-wire from a terminal now reaches an open window.
- **Looked at** on the scratch machine, in English light and 中文 dark. That caught the body saying
  *"the workspace named below"* before anything was below it, which was reworded in both catalogues.
  The chosen state sits behind a native folder dialog the instruments cannot drive. Its markup is
  the add drawer's, and the vitest loop covers its behaviour.

## SEM1 — the semantic half after a restart (2026-09-27)

- [x] **SEM1 — the semantic half after a restart** (study §3, read and not run). The vectors are
  held in memory (`InMemoryVectorStore`), and with an index already on disk,
  `KnowledgeService.EnsureIndexedAsync` skips the refresh. So after a restart the hybrid search
  answers lexically until someone refreshes, while the tools still say `lexical + semantic` (TIER1's
  half of the same lie). Measure it first. Then either re-embed on the first search, or keep vectors
  in Lyntai.Storage.Sqlite's store in `knowledge.db`, which needs Lyntai 3.5 and its version
  floors (Microsoft.Data.Sqlite 10.0.12, SQLitePCLRaw 3.0.5).

✅ **done 2026-09-27**. **Measured first**: a service over a store that already holds an entry, with
a deterministic embedder and an empty vector store (a new process), found nothing for a word only
the entry's vector shares. The test failed as the reading predicted. **Fixed by re-embedding on first
use**: when the index is already on disk, the first search embeds what is there, once, under the
refresh's own lock, without re-reading the disk. A failing embedder leaves the lexical half whole, as
a refresh does. **Not the persistent store**: that is SEM2, held until the per-process cost is
measured on a machine that runs an embedder. TIER1 is untouched: a search still reports the
configured tier, not the one that answered.

## TIER1 — which tier answered, per answer (2026-09-27)

- [x] **TIER1 — which tier answered, per answer** (service F13, D24). `HybridKnowledgeSearch`
  swallows either half's failure, and the tools report the configured tier (`SemanticEnabled`)
  rather than the one that answered. With the embedder down, a search still says `lexical +
  semantic`, and if both halves fail, nothing matching and nothing answering look the same. The
  search result has to carry its tier.

✅ **done 2026-09-27**, after SEM1, in the same code.

- **The answer carries its tier.** `SearchAnswer` holds the hits, whether each half answered, and
  why one did not, in its own words. Its token is `lexical+semantic`, `lexical`, `semantic` or
  `none`. The hybrid answers through `IAnsweringSearch`. A half that threw did not answer, and
  degrading still keeps the other half's hits. `KnowledgeService.AnswerAsync` is the door every
  door reads. A composition that is not the hybrid is the lexical search alone, so it answers
  `lexical`.
- **The agent's door** (`knowledge_search`): its footnote follows the tier that answered. A
  configured meaning half that did not answer is named beside the results. *Nothing answered* is
  its own sentence, never *no matches*. An empty answer by meaning no longer claims to have
  searched by word overlap.
- **The HTTP door** keeps its array body and says the tier in `x-daoris-tier`, ASCII only, since the
  failure's own words cannot ride a header. The family rehearsal holds it, because the HTTP host has
  no suite of its own (HTTP1).
- **The page** reads the header. Its empty state is worded by the tier that answered. *Nothing
  answered* is its own state. A words-only answer on a deployment that matches meaning too says so
  beside the list. A host older than the header falls back to the configured tier.
- **Not changed:** convergence still words its footnote from the configured tier. It embeds through
  its own detector and is not a search answer.
- **Looked at** on the scratch machine, started with an embedder pointed at a closed port. The
  status door said `lexical + semantic` (the configuration), the search's header said `lexical` (what
  answered), and the words-only note stood above the one result. The status bar still shows the
  configured tier, which is what it claims to show.

## WINDOW1 — a secondary window follows scope and theme (2026-09-27)

- [x] **WINDOW1 — a secondary window follows scope and theme** (web-rest F8). Both are read from
  `localStorage` once, and nothing listens for a `storage` event, so a detached session keeps the
  workspace and theme it opened with.

✅ **done 2026-09-27**. The browser tells every other same-origin document of a storage write, and
both halves now hear it. The scope provider re-reads the remembered workspace, unless it is pinned
by a test or a story. The theme module applies another window's choice and tells its listeners,
without writing it back. Every secondary window shares the main one's WebView2 user-data folder,
and so its storage (`SecondaryForm`). **Looked at**: with the monitor open, the main window's store
was set to dark, and the monitor's page turned dark. **Its native caption did not**: a secondary
window follows the OS theme directly, because it has no channel to be told. That was so before this
change too, whenever a window opened on a chosen theme. It is WINDOW2.

## RETRY1 — the screen's door for retrying a parked quest (2026-09-27)

- [x] **RETRY1 — the screen's door for retrying a parked quest** (CLEAN1, D50). A quest parked by its
  strikes is retried from a terminal (`daoris driver retry`), and the page has the other half ready
  (`useRetryQuest`, and the session's `forgiven`) with nothing rendering it. Put the retry where the
  parked quest is shown, with its test.

✅ **done 2026-09-27**. A quest the driver considers `Exhausted` offers *try it again* in its Quests
drawer, beside the sentence that says why it sits, as the trust grant sits beside its hold. The press
is `RETRY_QUEST`, the same mark `daoris driver retry` writes: counted from where the quest stands,
so the next failures park it again. The confirmation names the quest and how many failures park it
again, from both catalogues, since the driver answers only its state. A quest sitting for any other
reason offers no retry. Not looked at on the window: no machine here holds a parked quest, and the
vitest loop drives the drawer over the mocked bridge.

## BRW1 — the browser window (2026-09-27)

> *"so instead rely on things like claude extension we can have our own built-in browser system
> (or use plugin to support this)"* (owner, 2026-09-27 → D78)

- [x] **BRW1 — the browser window.** Its own WebView2 environment and profile under the home, no
  bridge, a loopback CDP port picked free, an address bar with back, forward and reload, and View →
  *Browser* with the palette's door. Module tests for the pure parts, and a look on the window with
  Playwright MCP attached from outside.

✅ **done 2026-09-27**. **Measured first**: Playwright MCP 0.0.82, attached with `--cdp-endpoint` to
the scratch shell's WebView2 debug port, listed the tab, navigated, snapshotted, and opened a second
tab. It also navigated the app's own page away, which is why the browser is its own environment.

- **The window** (`BrowserForm`, `BrowserHost` in the shell) is one framed window among the shell's
  windows, on its own pump like the monitor. Its WebView2 environment has its own user-data folder,
  `<home>/browser/profile`, and builds no bridge. It listens for CDP on a loopback port picked free
  once per process, so a reopened window joins the same browser with the same options. An agent
  bringing it up gets it without focus, and the person's press brings it forward. A link asking
  for a new window opens in the same one.
- **The judgements** (`InAppBrowser` in the modules, 18 tests): the profile folder, the debug
  arguments, the endpoint, a free port, and the address bar's rules. A host without a scheme is
  HTTPS unless it is this machine. A file, a script, data, or a credential in the address goes
  nowhere.
- **The doors:** View → *Browser*, and the palette's *Open Daoris's browser*, shell-only like the
  monitor, through `DAORIS.WINDOWS` `OPEN_BROWSER`. Its icon is a compass, because the globe
  already means a fetch.
- **Looked at on the scratch machine.** Two browser processes, each on loopback only: the app's
  page on the dev loop's port in its own folder, and the in-app browser on its own port in
  `home/browser/profile`. The dev loop's `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` reaches every
  environment, so for this one creation it names the browser's port and is put back at once.
  Playwright MCP attached from outside drove the in-app browser, and the window showed what it
  did, its caption following the page's title. Three things the look caught were fixed: glyph
  buttons sized in pixels were slivers at 200%, and are now sized from their font; an empty
  address bar now says what it is for; and `about:blank` and `data:` pages report no address,
  where an http page does.
- **Also found:** a scratch run started while the previous shell's WebView2 process was still
  winding down chose the next debug port and failed with `0x8007139F`, the same folder under
  different options. That is the dev loop, not the browser. A restart once the old process had gone
  was clean.

## BRW2 — `${browser}` at hand-over (2026-09-27)

- [x] **BRW2 — `${browser}` at hand-over.** A plugin server's `${browser}` is expanded when a session
  is handed its servers, from the shell's answer. The driver brings the browser up first, and
  withholds the server where no shell answers, saying so on the transcript. The example plugin moves
  to it. Driver tests with a stand-in host, and twin tests that the placeholder survives the read.

✅ **done 2026-09-27**.

- **`InAppBrowserServers`** (driver library, 6 tests) resolves a session's plugin servers. A server
  naming `${browser}` in its command, arguments or environment gets the endpoint the shell answers.
  Where none answers, or the browser would not come up, that server is withheld by name, the rest
  are handed, and the sentence says why. The shell is asked only when a server needs it, so a machine
  with no such plugin never has a window opened for it.
- **Every hand-over resolves once and hands both doors the same list**: a driven session, an intake
  (the one that reads a ticket), and a conversation. The pipe door's server file and the protocol
  door's `session/new` offer both carry the resolved list. The notice is the ACP door's harness
  notice and the pipe door's preamble, and a note on a conversation. The shell passes its
  `BrowserHost` through the loop, the watch and the chat runner. The headless host passes none.
- **The placeholder survives the read** in both twins, so only the hand-over fills it.
- **The example did not move; a second one joined it.** The family rehearsal installs `browser` and
  asserts its stub was offered that server, and a headless gate has no in-app browser. So
  `examples/plugins/in-app-browser` attaches with `--cdp-endpoint ${browser}` (Playwright MCP pinned
  at 0.0.82, the measured version), and `browser` stays for machines without the shell. Both name
  their server `browser`, so one allow rule covers either.
- **Not looked at end to end on the window**: a real session brought up by the driver opening the
  browser window. The conversation test runs a real stub process and reads the file it was handed.
  The real harness through the real window is BRW3.

## INT7 — an intake is handed its room's allow-list (2026-09-27)

Found on the first real ask (FG5), and fixed in the same sitting rather than filed. The intake's
`WebFetch` was refused. The room's own settings file allows it, but over the protocol door a
permission request is refused by construction, and only the rules handed at spawn count. Those were
the machine's and the circle's, not the room's. `HandRules` now takes the session's job, and an
intake passes `IntakeRoom.Allowed`, so the room's list rides with the person's rules. A deny the
person wrote still wins. The intake found the browser on its own anyway and read the ticket through
it, so nothing was lost on that run. A public page would have been lost. Driver 738 (a test mirrors
INT4j's, over the recorded settings).

## ASK1–ASK3 — ask and wait (owner, 2026-09-27 → D79)

> *"so the issue here is it should request to [the backend]"*

Found on FG5's development session, which needed the backend's note contract, tried to read the
backend's code, was refused, and guessed. `docs/2026-09-27-ask-and-wait-design.md` is the contract.
All three landed together on 2026-09-27.

- **ASK1 — `wait` in the exchange.** `quest_respond wait on:<quest>` appends a `Waited` operation,
  so the move is logged, replays and syncs (D68). The quest stays **Taken** with `awaits` set: a
  column on the store, a field on both doors' reads, and "waits on" in `quest_list`. It refuses a wait
  with no question, on itself, on an unknown or closed quest, or on a quest that is not taken. The
  ledger opens a session on a taken quest only when the quest it awaits has closed. Service 522,
  including a test that no tool description names a decision number; `(D79)` had slipped into
  `permission_propose`'s description.
- **ASK2 — the driver.** The planner considers a taken quest that waits, but only where this
  machine's records hold a session on it other than a stand-down (`Snapshot.LastRun`). While its
  question is open it sits, as `Waiting`, naming the question. Once the question closes it is a start
  like any other, with the hold, strikes, busy and cap all in front of it, and it carries the session
  that asked. The resume runs in that session's tree when it still stands, and its instruction says
  the quest is already the session's own and quotes the answer. The asking session concludes
  `completed`. A resumed one that ends with the old wait still standing concludes `failed`, so the
  strikes bound it instead of the quest resuming every tick. Driver 753, including two real-tick
  runs over a stand-in service: ask, sit, resume in the same tree with the answer, done; and the
  resume that stops short.
- **ASK3 — the words and the page.** The driven instruction tells the session to ask what another
  repository knows, commit, wait, and end its turn. `quest_publish`, `quest_respond` and
  `permission_propose` say the same. The card says *waits on #q* while the question is open, and
  the drawer names the question with a door to it, says *answered* once it closes, and drops the
  driver's sitting sentence, which only repeated it. en and zh. Web 1189. The family rehearsal gained
  the HTTP half: wait, the refusals, the resume opening once answered, and the wait surviving a
  restart.
- **Built differently from the first draft.** The design first sent a waiting quest back to *Open*.
  That made it anyone's to take fresh, without the tree that asked, so it stays *Taken* (D79 as
  written, and the design's §1).
- **Not looked at on the window with a real session.** The install still runs the build before D79,
  so FG5's quest cannot use it yet. That run is FG5's. The page was looked at on the scratch shell,
  light and dark, with a quest parked on a question and then with the question answered.

## ACPEND1 — a turn the agent refused is not a clean exit (2026-09-27)

Found on FG5's run. The account's spend limit refused the development session's turn mid-edit, and
the adapter exited 0 once the driver closed stdin. With the quest still taken, the record concluded
`stood-down: someone else has it`, which was false and hid the account limit. The protocol door's
failure now reaches both conclusions. A refused turn with the quest taken or open concludes `failed`
in the agent's words. So does an intake that published nothing, which would otherwise have parked as
"asking you". A close, a wait or a publish that landed first keeps its ending. FIX-LOG has the root
cause (ACP1's rule that the exit code concludes, on a door where the driver causes the exit). Driver
758, including a real tick over the protocol door with the measured error. The run is also TOOL4's
first real observation of exhaustion, recorded on its row.

## ASKAGAIN1 — the same words after a close ask anew (2026-09-27)

Found asking FG5's ticket again after its first run was cut off. An ask's id is made from its circle
and its words, so the same words answered with the first ask for good, including one the person had
closed ("already asked … closed: …"). The only way through was rewording. Now a closed ask moves the
same words on to a fresh id, and the closed record stays as it was. While an ask is open, the same
words are still that ask, which is what makes a retry or a repeat safe. The first id keeps its old
form, so no existing ask moves. Service 523.

## CARRY1 — a session cut off after its take is carried on in its tree (2026-09-27 → D80)

Found on FG5's second run. The development session took its quest, changed 45 files, and was running
its repository's gates when the thirty-minute timeout killed it. The quest stayed taken with its work
in a tree nothing would open again.
- **The ledger** opens a session on a taken quest, waiting on nothing, whose last record on this
  machine failed. A stand-down, a teammate's record or no record refuses as before.
- **The driver** reads each quest's last run with its state and note. It plans a cut-off like a
  start, behind the hold, the strikes, busy and the cap, in the cut-off session's tree.
- **The instruction** says the quest is already its own and what cut the last session off. It lists
  the changes left uncommitted, read by the driver, since a session here may not run `git status`.
- **The ending:** a carried-on session that ends with the quest still taken concludes `failed`, so the
  third cut-off parks it behind `driver retry`.
- **The timeout from a terminal:** `daoris driver timeout <minutes>`, shown in `driver list`, written
  only when set, and read as the driver reads it.

Service 525, driver 765 (a real tick on the protocol door: refused after the take, then carried on in
the same tree to done), CLI 483, and two family-rehearsal checks over HTTP.

## ORPHAN1 — whatever a session starts ends with it (2026-09-27)

Found on FG5's verify step. The session's dev servers, started from its harness's background shell,
were still listening an hour after its record concluded, on 4200 and 4288. They were stopped by hand.
The driver's reaper walks a tree from a live root, and the agent had already exited on its own. Now a
tracked harness joins a Windows job object that kills on close, and so does everything it starts. The
untrack at the session's end closes it. This covers quest sessions, intakes and conversations, and is
a no-op off Windows. FIX-LOG has the mechanism. Driver 766.

## POSTURE1 — a driven session in its harness's own `auto` mode (2026-09-27 → D81)

The owner, asked whether the verify session might apply a config to dev only: *"not dev only this
should be able to do a much as it can just like regular claude code or codex"*. The Claude Code ACP
adapter names `auto|acceptEdits`, and the session sets the first posture the agent offers. It is
never `bypassPermissions`. The rules handed at spawn keep what the doctrine keeps (no push, the tree
guard). The pipe door is unchanged until `auto` is measured there. Driver 768 with the posture tests.

## CONSOLE1 — the console's `?`s (2026-09-27)

The owner: *"we have a lot '?' display in the console rn"*. Measured in FG5's transcripts, each tool
call was followed by five to eight status-less updates (its input and output arriving in pieces).
Each printed as `toolu_… → ?`, and the ending printed the id. Now the console remembers each call's
title per session. Progress and status-less updates print nothing, and the ending reads `✓ <title>`
or `✗ <title> failed`, on both doors. A `usage_update` without numbers prints nothing either. The
record keeps every update as an event, as before.

## CHAIN2 — a chain's next step starts on the step before's branch (2026-09-27 → D82)

Found on FG5's verify step, which grew from the canonical line and could not see the develop step's
unmerged work. The owner chose *"On the parent's branch"*. The driver reads each quest's last run
with its repository. For an open step whose parent's last run was in the same repository, the plan
carries that run, and the tree grows from its branch (`SessionTrees.OpenAsync(from:)`). A branch
that is gone falls back to the canonical line and says so. The instruction says the parent's work
is in the tree and there is no merge to make. Driver 773, including a real-git test that the grown
tree holds the parent branch's commit.

## PAR1 — sessions run side by side, one per tree (2026-09-27)

The owner: separate repositories and sessions exist *"to have clean domain separation and parallel
running for sessions"*. The planner started one session per repository even where every session opens
its own tree. D51 made the tree the lock, and the ledger already locks per tree. For a repository
with trees on, an active session no longer holds it, and several of its quests start in one tick,
oldest first, up to the cap. A resume or a carry-on still waits while a live session holds the tree
it goes back into. A repository without trees keeps one session at a time in its root, and the reason
now says trees are how to run them side by side. Driver 776.

## STANDDOWN2 — a session that holds its quest and stops waits on the person (2026-09-27 → D83)

Found on FG5's verify step. The session took its quest, did what it could, and ended its turn with
three questions for the person, and the record read `stood-down: someone else has it`.
- **The take is written.** A take through a session's own connector is on its record (`took`), and
  the ledger allows it only for that session's own quest while it runs.
- **The ending.** A session holding its quest that ends cleanly parks `awaiting-person`, quoting its
  last words from the transcript. That covers one that took it, and a resume or a carry-on. A park
  is not an ending, and nothing resumes it by itself.
- **The answer.** A door on the service, the page's *answer and carry on* on the parked card, and
  `daoris-driver answer <session> "…"`. The record ends `completed` with the words.
- **The carry-on.** The planner then carries the quest on in its tree, and the prompt hands the
  session the person's words and what was asked.

Service 528, driver 780, web 1191, and two family-rehearsal checks over HTTP. Also: the
`DrivenSessionInputTests` start wait is 60 seconds, after the load flake recurred (FLAKE1).

## CONSOLE2 — the console has a tab for each thing that is running (2026-09-28)

The row as filed: *(the owner, 2026-09-27: "the console display should be able to have multiple tabs
so we dont miss any console (like different subsessions and console that runs by the session)").*
Today it is one stream per session. The design wants tabs: the session itself, each sub-session it
spawns (a harness's own subagents), and each process it starts (the dev servers FG5's verify session
left running were visible nowhere but their ports). Needs the adapters to say which frames belong to
which sub-session, and the driver to capture a started process's output.

Landed in three parts.
- **2a, the evidence** (`docs/2026-09-28-console2-streams-evidence.md`, `tools/console2-probe.mjs`).
  Two real turns over `claude-agent-acp` 0.79.0. A subagent is a session of its own under AIR's
  `nativeSubagentSessions`. The protocol's own `subagents` key is stripped by the ACP SDK's schema at
  1.4.0, so it alone switches nothing on. A background task's output is a file the harness writes,
  and none of it is on the wire. `terminal_output` streams nothing. And the agent speaks after its
  turn has ended, when its background work finishes.
- **2b, the driver.** The protocol door asks for both, routes each update by `params.sessionId`, and
  keeps each subagent and task as a console stream under the session (`AcpStreams`, `OutputTail`,
  `SessionOutput`'s streams). A subagent's words stay out of the session's transcript, because the
  driver reads a transcript's last plain lines as what the session said to the person. The rules are
  in the interactive design's §2.
- **2c, the tabs.** `SESSION_STREAMS` lists a session's streams, and an event of the same name says
  when one opens or ends. The output panel has a tab for the session and each stream, each named by
  how it stands, in both languages. Looked at on the window with a real chat on the protocol door: a
  background ticker and a subagent each got a tab, the ticker's tab read its file as it grew, and
  both ended `completed`.

Driver 797, modules 152, web 1198, Playwright 21, family 289, deployment 39. What it left is CONSOLE3.
The look found two conversation defects, both fixed the same day and in the fix log: two messages in
a row read as one (*DONESubagent finished*), and a *working…* mark stayed under words the agent said
after its turn.

## BRW10 — a sign-in survives the application restarting (2026-09-28)

The row as filed: *(FG5, 2026-09-27).* The owner signed in to a dev identity in the in-app browser,
then the application restarted twice (republishing), and the next session found an empty cookie jar.
An identity server's session cookie ends with the browser process, and WebView2 does not restore
session cookies. Keep them across a restart, encrypted to the account (DPAPI) under `<home>/browser/`,
restored before the first page loads. Until then, restarting the application signs the browser out.

Built as filed. `BrowserSessionCookies` (modules) keeps only session cookies, since the profile keeps
the rest, in a versioned file under the home sealed by an `ICookieSeal`. It writes nothing when the set
is unchanged. It puts back only what the browser does not already hold, so a reopened window never
overwrites a value the site rotated. A file this account cannot open, or one that is not what was
written, restores nothing and says why. The shell's seal is DPAPI to the current user, named for its
purpose. The window restores before its first page and keeps after every page and every 30 seconds.
The why shows as a strip under the bar, not the bar's placeholder, since a focused box shows none
(seen on the window).

Proven on the window against a stand-in site that sets a cookie with no expiry. Signed in, the shell
restarted, and the same session came back on a new browser process. The control, the same restart
with the kept file deleted, came back signed out. The file holds no cookie value in plain text, and a
file of garbage showed the strip. Modules 158, deployment 39. The install still runs without it, so
its next republish signs out once more.

## BRW4 — tabs (2026-09-28)

The row as filed: Several pages, a strip to switch, `Ctrl+T` and `Ctrl+W`, and a link that asks for a
new window opens a tab. Decide which tab an agent drives. CDP exposes each as a target, and the
default should be the one in front, said in the strip.

- **The order** is `BrowserTabs` (modules, 8 tests): a tab the person asks for goes last, a page's
  new window sits beside that page, either comes to the front, and closing the one in front brings its
  right neighbour forward, or its left at the end. The last tab closing closes the window.
- **The window** holds a WebView2 per tab on the one environment, so each is a CDP target on the
  same port. A page's new window is a tab handed back to it as its window, so `window.opener` works
  (a `target=_blank` link has none, as Chromium has made the default). The strip's front tab wears the
  bar's colour. Tabs narrow as they are added, with a middle click to close. Ctrl+T, Ctrl+W,
  Ctrl+Tab and Ctrl+L.
- **Decided: which tab an agent drives is the agent's.** Measured: Playwright MCP, attached, listed
  every tab and took the first page it found as current, not the one in front. So the row's default
  is not Daoris's to set, and the strip says only which tab is in front. Also measured: **a tab an
  agent opens over CDP has no window**, and nothing shows it. That finding went to BRW8.
- **Looked at on the window.** New-window links and `window.open` made tabs beside their opener, a
  page's `window.close()` closed its tab, and the strip's buttons opened, brought forward and closed
  tabs, pressed through UI Automation. Keystrokes synthesized from the terminal did not reach the
  window. So the shortcuts over a page rest on the WebView2 control's IL (its accelerator handler
  raises `KeyDown` and carries `Handled` back), not on a key pressed. **Found and fixed on the way:**
  the bar's three glyphs were lost to a rewrite, being private-use characters no view shows. A tab's
  entry as a `PageTab` had no pattern, so nothing that reads the window could press it. A blank tab's
  close button had no name.

Modules 166, deployment 39.

## BRW5 — favorites (2026-09-28)

The row as filed: A star in the address bar, a favorites bar, and a menu. They are kept in
`<home>/browser/favorites.json` (D63), with a terminal twin (`daoris browser favorite
add|list|remove`, D50). They are the person's, never a session's.

- **The file is a twin**, and its rules are the in-app browser design's §3a: no file is no favorites;
  a file that cannot be read shows none and an editor refuses to write over it; a row's address
  follows the bar's rule, and one that fails it is skipped by a reader and kept by an editor; a title
  is its own, or the host; the file's order is the bar's; adding an address already kept keeps its
  place; an editor keeps what it has no field for. `BrowserFavorites.cs` (modules) and `browser.ts`
  (the CLI) each carry the same table, fifteen address cases answered alike.
- **The terminal's door** is `daoris browser`, the sixteenth command, a management verb that edits one
  file under the home.
- **The window's**: a star beside the address, filled when the page in front is kept; a favorites bar
  under the address, a press to go there and a middle press for a new tab, which appears with the
  first favorite; and a list button whose menu holds them all. Read again whenever the window comes
  forward, since the terminal may have changed them.
- **Looked at on the window**, pressed through UI Automation: the star kept a page with its title; the
  terminal added a second and the menu showed it; a favorite navigated the front tab; unstarring
  removed it, and `daoris browser favorite list` agreed. **Found and fixed on the way:** disposing the
  bar's old buttons moved the focus and read the favorites again mid-read, and every favorite showed
  twice. The bar is now replaced in one step, and a read inside a read is dropped.

CLI 495, modules 191, deployment 39.

## BRW6 — history and completion (2026-09-28)

The row as filed: The address bar completes from history and favorites. History stays in the
profile, and there is a clear.

- **The history is Daoris's own record**, because WebView2 keeps one in the profile and offers no
  way to read it. Each page a tab finishes loading goes into `<home>/browser/history.json`, on this
  machine only: counted again when it is there, most recent first, and at most 500 pages. The rules
  are the in-app browser design's §3b. Reading and clearing are a twin with the CLI's `daoris browser
  history list|clear` (the same reading table in `BrowserHistory.cs` and `browser.ts`). Recording and
  completing are the window's.
- **Completion** is a list under the address box as the person types: a favorite first, then history,
  each page once, matched in the address or the title. A host that starts with what was typed comes
  first, then how often, then how lately. Up and Down choose, Enter goes, Escape closes, a press goes.
- **The clear** is in the favorites-and-history menu, under the ten most recent pages. It empties
  Daoris's history and asks WebView2 to forget its own; sign-ins and favorites stay.
- **Looked at on the window**, through CDP and UI Automation: four loads recorded three pages with
  their titles and counts; typing `chi` offered the child page; the menu listed the favorites, then
  the recent pages most recent first; its clear emptied the file, and the terminal's list agreed.
  **Found on the way:** a second scripted edit matched the line the first had just written, and the
  window read its history twice on activation. It was put right with the edit tool.

CLI 500, modules 203, deployment 39.

## BRW9 — the rest of a browser's basics (2026-09-28, superseded by D84)

The row as filed: Find in page (`Ctrl+F`), zoom, devtools (`F12`), downloads to
`<home>/browser/downloads` with a shelf, and the theme once WINDOW2 gives the window a channel.

**Not built: superseded.** D84 makes Daoris's browser a Chromium Daoris ships (or the person's Edge),
and a real browser has find, zoom, devtools, downloads and its own theme already. Measured on Edge 154
(`docs/2026-09-28-managed-edge-evidence.md`). Rebuilding them on the WebView2 window, which D84
retires once the engine lands, would be work thrown away.

**Amended by D85 (2026-09-28).** The engine is embedded, under Daoris's own chrome, so a browser's
basics are Daoris's to draw again. They come back after CHR3, on the new engine, not on WebView2.

## BRW11 — the engine: a Chromium Daoris ships and keeps (2026-09-28, superseded by D85)

The row as filed: *(D84). A probe of the builds first (Chrome for Testing, a snapshot, another:
banner, codecs, licence, size). Then it is managed like a harness (D57): a pin, fetched from its
maker's channel, verified, and updated deliberately. Daoris starts it on a profile under the home with
a debug port and **no account and no sync**. The endpoint is `${browser}`, as today. Sign-ins are kept
over CDP (the evidence's §4, BRW10's rule), and favorites and history come from the engine's own. The
terminal twin is `daoris browser`.*

**Not built: superseded.** D85 answered D84's open form: the engine is embedded, and it hosts
Daoris's own page as well as the browser. The row assumed a separate browser program started and kept
like a harness. CHR1–CHR4 replace it (`docs/2026-09-28-chromium-host-design.md`), and CHR3 carries
what stands of it: the profile under the home, no account and no sync, `${browser}`, and a sign-in
kept across a restart.

## CHR1 — measure an embedding first (2026-09-28)

The row as filed: *🔴 Does the debug port reach every page in the process? The answer also goes to the
owner for Shenora's entry.* The contract's §5 carried the rest: a tab a CDP client opens, a session
cookie across a restart, Playwright MCP driving, a page-to-host round trip, and the runtime's size,
banner, codecs and licence.

**Outcome.** `tools/chromium-probe.mjs` with its host `tools/chromium-probe/` (CefSharp 152, Chromium
152), recorded in `docs/2026-09-28-chromium-embedding-evidence.md`:
- **The port reaches every page in the process**, and the app page's bridge was callable over it. So
  the browser runs in a process of its own, which the contract's §2.3 now says.
- An agent's CDP tab escapes into an engine window the person sees and the app is not told of.
  Playwright MCP 0.0.82's `browser_tabs new` fails against the engine.
- `PersistSessionCookies` keeps a sign-in across a restart.
- A tab shares the sign-in only in the global request context.
- The round trip is 0.2–0.3 ms median.
- Size: 352 MB on disk with two locales, and 166 MB compressed.
- No H.264, AAC or HEVC.
- The credits page names FFmpeg and LGPL text.

It left CHR3's form to the owner: Daoris's chrome around the control, or the engine's own window.
**Found on the way:** a reply through the message's own frame never arrived and said nothing (the
evidence's §6). The driver's first runs read a restarted host's old log and a stale report, and each
was put right before its number was kept.

## CHR3 — the browser on the same engine (2026-09-28)

The row as filed: *(supersedes BRW11). The owner's call first: Daoris's own chrome around the control
(BRW9 back), or the engine's own Chromium window (the contract's §4).* The owner's call: *"lets do it
now please download"*, on the recommendation of the engine's own window.

**Outcome.** `daoris-browser` (`src/Daoris.Desktop/Daoris.Desktop.Browser`) is CefSharp 152 with no
control, in a process of its own:
- It opens the engine's own window over its own port, and ends when its last window closes or the
  shell does.
- The shell starts it behind `IInAppBrowser` (`EngineBrowserHost`), with the profile at
  `<home>/browser/engine` and `PersistSessionCookies`. The driver, the plugins and `${browser}` are
  unchanged.
- The pure half is `EngineBrowser`, `EngineBrowserOptions` and `EngineCdp` in the modules. They cover
  the arguments the shell writes and the browser parses, the locale, where the executable is found
  (the install first) and which targets are windows. There are 19 tests, one watched fail by
  sabotage.
- The publish carries it under `app/daoris-browser/` with two locales, and
  `deployment-rehearsal.test.ts` holds that folder against `EngineBrowser.InstallHome`.
- The dev loop builds it, and `shot --window browser` photographs it.

**Proven by:**
- The deployment rehearsal: the deployed shell's own driver brought up the install's browser for a
  session's `${browser}` server, on the home's profile, handed the server, and the browser went with
  the shell.
- A look on the scratch shell: a second press brings the window forward, a press after closing starts
  another, and `kill` takes it with the shell.

**Found on the way** (evidence §11–§15):
- No agent can open a tab, because the engine announces a new target as `other` (CHR6).
- The machine's Chrome extensions reach the profile (CHR7).
- Focus is Windows' to give.
- Daoris's favorites and history are read by no window now (CHR5).

**Then the WebView2 window went**, as D84 said it would once the engine landed (the same day, its own
commit): the form and its host, the sealed-cookie store and its DPAPI seal, the tab strip, and
`InAppBrowser`'s WebView2 members, with their 15 tests. The favorites and history files, their module
code and `daoris browser` stay until the owner's call on CHR5.

## CHR6 — an agent opens a tab (2026-09-28)

The row as filed: *The engine announces a new target as `other`, so no browser MCP takes it up
(evidence §12): a CDP proxy at `${browser}`, or a report to CEF (the owner's to file).* The owner:
*"yes allow"*.

**Outcome.** The relay:
- `daoris-browser` keeps the engine on a port of its own and answers `${browser}` with `CdpRelay`, in
  the modules.
- The relay passes every HTTP request and socket message through, and says a target the engine calls
  `other` is a `page` when its address is a tab. That covers a web page, a blank tab and the new-tab
  page, in an announcement, an attachment or a target list.
- An HTTP answer names the relay's port, not the engine's, so a client never goes around it.
- It answers a closing socket before passing the close on.

**Proven by:**
- 15 tests, including one through a real socket and a stand-in engine; the correction was watched
  failing, 8 of 15 red.
- Playwright MCP's `browser_tabs new` and Chrome DevTools MCP's `new_page` both opened a tab in the
  person's window (evidence §17–§19).

**Found on the way:**
- The engine's windows use `<root>/Default` whatever cache path they are given. `daoris-browser` names
  it now (§16).
- A first sabotage bound as `(false && …) || …` and broke one case of 15; it was read, not believed.
- No report was sent to CEF; filing one stays the owner's.

## CHR5 — Daoris's favorites on the browser's bar (2026-09-28)

The row as filed: *(owner, 2026-09-28). They stay Daoris's: `favorites.json`, `daoris browser
favorite`, and a Settings screen. Each start puts them in a Daoris folder on the engine's bookmarks
bar. History is the engine's own, so `daoris browser history` is retired.*

**Outcome.**
- `daoris-browser` writes `favorites.json` into the engine's `Default/Bookmarks` before the engine
  starts, as a *Daoris* folder (`EngineProfile.WithFavorites`). The folder is found by a fixed id
  wherever the person moved it, and with no favorites it goes. The person's own bookmarks are never
  touched, and a file it cannot read is left as it is.
- The bar is shown once, when the folder first appears, and after that it is the person's to hide.
- The screen door is Settings → Browser (`BrowserModule`, `DAORIS.BROWSER`), alongside the terminal's.
- `daoris browser history` is retired with a sentence saying where the history is now, and
  `BrowserHistory.cs` went with it.

**Proven by:**
- 22 `EngineProfileTests`.
- 6 `BrowserModuleTests`: the file afterwards, a refusal by code, an unreadable file left alone.
- 4 page tests over the mocked bridge.
- Measured on the engine (evidence §21–§22): a bookmarks file written from nothing, with no checksum,
  is taken.
- A look on the scratch shell, in both themes: the domain, and the folder on the browser's bar from
  the same file.

**Found on the way:** the add form's rule stopped partway across its card, and now runs the card's
width.

## CHR7 — other software's Chrome extensions, a setting (2026-09-28)

The row as filed: *(owner: "configurable"): offer them for approval, as the engine does, or refuse
them. Refusing seeds the profile's `external_uninstalls` (evidence §20). It has two doors: `daoris
browser extensions` and the same Settings screen.*

**Outcome.**
- `<home>/browser/settings.json` is a twin: `browser.ts` and `BrowserSettings.cs`, with one test table
  on both sides. Offer is the default.
- On *refuse*, `daoris-browser` reads what other software registered for Chrome and Chromium (the
  machine's and the account's keys), and seeds them into the profile before the engine starts. It
  records which ids it refused in `<profile>/daoris.json`.
- On *offer*, it takes back only those, so a refusal the person made in the browser survives.

**Proven by:**
- 11 twin tests, and the refusal rule watched failing.
- End to end on a scratch home (evidence §22): refused, offered again, and the person's own refusal
  kept.

## LAYOUT1 — a window's content keeps a small width when the window grows (2026-09-28)

The row as filed: *(the owner, 2026-09-28: "accept window box content does not auto resize with the
outer window so it always stay as a small width, (might be more having the same issue you do need to
do a verifiy later)"). Seen where accept is pressed: most likely the review pane (`DiffPane`, whose
accept merges), or a rule proposal's card in Settings. Find which, fix it, then sweep every surface
and window at a narrow, a middle and a wide size, in both themes. List each one that holds a fixed or
capped width where it should follow its container, and say why for one that caps on purpose.*

**Outcome.**
- It was the Work frame's right dock, where the review's *accept* is, and not its content. A dragged
  dock was kept in pixels (FRAME6), so it stayed 389 while the window went from 1518 to 1923.
- It is kept as a share of the window now (`FramePrefs.dockShare`), and a pixel width from an
  earlier build is converted once.
- `docs/FIX-LOG.md` has the root cause.

**The sweep:**
- *By code:* every capped or fixed width in the page's source. Each is a reading measure on prose
  (`max-w-prose`), a form sized to its fields, an overlay (tooltip, toast, dialog, the palette, a
  popover), the monitor's 17.5rem sidebar, or a table column that shrinks to its numbers. None holds a
  container narrow that should follow the window.
- *By looking:* Overview, Sessions with the dock open, Quests, Projects, Map, Convergence and Search,
  each at the wide size (1923), where a width that does not grow shows. All fill it.
- *Not photographed:* the narrow and middle sizes of every view, the dark theme (a theme changes no
  width), and the monitor and a detached session, whose code holds no such cap.

**Proven by:**
- The layout test watched failing, and two frame tests.
- The measure on the shell, before and after the fix.

## BRW12 — the person's Edge, as an option (2026-09-28)

The row as filed: *(D84). A machine setting, with its terminal twin: Daoris's engine, or Edge on a
profile under the home. The Edge option says what it brings: the person's Microsoft account, signed in
on its own (the evidence's §5), and with sync on if they want their own sign-ins and extensions.
Driving the person's default profile is not possible, since Chromium refuses a debug port there, and
the screen says so.*

**Outcome.**
- `settings.json` gains `browser: daoris | edge`, a twin like the rest of the file.
- Its doors are `daoris browser use [daoris|edge]` and a *Which browser* card at the head of
  Settings → Browser. Both say what Edge brings: the Microsoft account, signed in on its own; sync and
  extensions that are the person's to turn on; and a default profile that cannot be driven.
- The card warns when this machine has no Edge.
- `EngineBrowserHost` reads the choice at each bring-up. For Edge it starts `msedge` on
  `<home>/browser/edge` with a debug port, records the port in `<home>/browser/edge.json`, and adopts a
  recorded Edge that still answers as one, so a second start never hands off to an Edge on a port
  nobody knows.
- Edge needs no relay, because it announces its own tabs as pages.

**Proven by:**
- 13 `EdgeBrowserTests`.
- The twin tables on both sides.
- Module tests for the choice and its refusal code.
- 3 page tests.
- On the scratch shell, with a real Edge: chosen, opened on the scratch profile answering as
  `Edg/154`, and adopted, not doubled, by a second press and by a restarted shell.

**Left open:** Edge still drops a session cookie at a restart, and carrying it is BRW13.

**Found on the way:** a port recorded as a string read as one until the kind was checked.

**Privacy:** the scratch Edge profile, which Edge signs in to the Windows account on its own, was
deleted after the check, and nothing of it was photographed.

## BRW13 — Edge keeps a sign-in across a restart (2026-09-28)

The row as filed: *(D84: "whatever the engine, Daoris keeps a sign-in across a restart itself, over
CDP"). BRW12 landed the Edge option, and Edge still drops a session cookie when it restarts (the Edge
evidence, §3). Carry them over CDP as the evidence's §4 measured: read them while Edge runs, seal them
to the account, and put them back before its first page. BRW10's sealed carry is in history
(`7480b97^`) to start from.*

**Outcome.**
- BRW10's sealed store came back from history, pointed at Edge (`<home>/browser/edge-session-cookies.bin`),
  and so did its DPAPI seal.
- New is `CdpCookies`: CDP's cookie shape to Daoris's and back.
- The shell's host keeps Edge's session cookies at each bring-up and every 30 seconds while the
  recorded port answers as an Edge, since the person's closing it is unannounced.
- When Daoris starts a fresh Edge, the host puts back the kept ones it does not hold, before anyone
  navigates.
- Daoris's own browser needs none of this: its engine keeps them itself.

**Proven by:**
- The restored sealed-store tests (the file under the home, sealed, only session cookies, a foreign
  seal opening nothing).
- 3 `CdpCookiesTests`.
- On the scratch shell with a real Edge 154 and a stand-in identity site: signed in, kept, Edge
  closed, opened again from Daoris on a new port, and the same session came back. That Edge alone
  loses it was measured on this Edge (the evidence, §3).
- The scratch Edge profile and its kept cookies were deleted after.

## WSR2 — the default branch is the repository's, and a person can set it (2026-09-28)

The row as filed: *(study §3). Today it is `origin/HEAD`, else `main`, else `master`, and cannot be
set. A per-repository setting with a workspace default wins over the guess. Everything that reads the
line reads it from one place.*

**Outcome** (D86).
- `driver.json` gains `lines` and `workspaceLines`, written only when set.
- `CanonicalLine` resolves in this order: the repository's own line, then its workspace's (a
  repository in no workspace takes `default`'s), then the checkout's guess. It always says which one
  answered.
- Every door reads it:
  - a session tree's start, and through it a chain's next step;
  - the merge door, whose refusal names the set line;
  - a tree's removal;
  - sync's feed.
- A line only the remote has is grown from `origin/<line>`. One that is nowhere is refused before
  anything is created.
- The merge door and a tree's removal compare against the line where git has it. When git cannot
  compare, the answer is a refusal, not "nothing unmerged". Before this, a tree whose line only origin
  had was removed as if its work had landed (FIX-LOG).
- Two doors (D50): `daoris driver line <repo>|--workspace <name> <branch>|--clear`, and Settings →
  Workspace → *Lines* (the `SET_LINE` and `LINES` routes). Projects shows each repository's line and
  what said so.
- The branch-name rule is a twin, with one table on both sides.

**Proven by:**
- 29 `CanonicalLineTests`, with real git for the open, workspace, origin, refusal, merge and removal
  cases. Sabotaging the reader turned 5 red, and sabotaging the comparison turned its 2 red.
- 3 driver-module tests: the file, the refusal sentences, and the cold start.
- 4 CLI tests, including the branch-name table.
- 5 molecule tests and 3 stories.
- On the scratch shell, both themes:
  - set from the screen, and it landed in the file;
  - a workspace default set from the terminal was shown on the screen;
  - cleared from the screen, and the repositories fell back to the workspace's line.
- The screenshots found two defects, now fixed: a row with *Clear* moved its field out of the column,
  and in dark an inherited line read as a set one.

## WSR1 — workspace rules: how work lands (2026-09-28)

The row as filed: *(study §1). An integration rule per workspace, with a repository override: merge
into a named line, carry the work onto a feature branch named by a pattern for the person to push, or
push and open a pull request. Set in Settings and from a terminal, and carried to the review screen,
chains and the session's instruction. **The owner's call first:** whether Daoris may ever push and open
a pull request (D37 keeps both human).*

**The owner's call** (D87): *"this should be configurable, and lets say no push or open pr on default
but we should be able to support later for plugin to control since there will be different platform
for pr"*. The push form is WSR4, a plugin's.

**Outcome.**
- `driver.json` gains `landings` and `workspaceLandings`, written only when set. A rule is `merge` or
  `branch` with a pattern. `LandingRules.Choose` picks the repository's rule, then its workspace's,
  then merge, and says which.
- The pattern can say `{quest}`, `{session}`, `{slug}` (the title's words, up to 40 characters) and
  `{repository}`. It must name one branch per session, and git must take the result. The same rule
  and table are on both sides of the twin.
- **The branch form** (`SessionTrees.LandAsync`) makes one branch from the session's branch. It moves
  no checkout, merges nothing and pushes nothing, and it refuses a branch that already exists rather
  than moving it. The merge form is the merge door as it was.
- **Before the press:** the review says where accepting sends the work (the `LANDING` route).
  *Accept* presses `LAND_SESSION_TREE`. A session in its own tree under the branch form is told its
  work goes through review, and not to merge or push it. Chains are unchanged (D82): a later step's
  branch holds the earlier step's work, so landing the last step carries the chain.
- Two doors (D50): `daoris driver landing <repo>|--workspace <name> merge|branch <pattern>|--clear`,
  and Settings → Workspace → *How work lands* (`SET_LANDING`).

**Proven by:**
- 23 `LandingTests`: the pattern table, the slug, precedence, the file, and on real git the branch
  form leaving a dirty checkout on another branch as it was, an existing branch refused and unmoved,
  nothing to land, uncommitted work, merge as the default, the plan, and the instruction. Sabotage
  turned the existing-branch guard's and the instruction's tests red.
- 3 driver-module tests; 4 CLI tests; 6 molecule tests and 3 stories; 1 work-frame test for the
  sentence before the press, and the two accept tests moved to the land route.
- On the scratch shell: a branch rule set from the screen landed in the file and in the terminal's
  listing. Screenshots found two defects, now fixed: the card's bold marks showed as asterisks, and an
  inherited pattern read as a set one.
- Not seen on the window: the review's sentence on a real session. The scratch machine's one session
  tree was past the rail's first page. Vitest holds the pane's rendering, and the driver tests hold the
  plan and the press on real git.

## WSR3 — session branches cleaned up after they land (2026-09-28)

The row as filed: *(study §2). After work lands, its tree and branch go, under the workspace's rule,
only when git proves the work is on its target. A bulk clean of every landed or empty `daoris/s-*`
branch, listed before it is pressed, with a terminal twin. A branch whose commits never landed is
shown as such.*

**Outcome** (D88).
- **The proof**, `SessionTrees.UnlandedAsync`: the commits on a branch that no local branch outside
  `daoris/` and no remote-tracking branch holds. Zero is landed. It serves a tree's removal without
  `--force`, which now also takes a tree whose work is on a feature branch, the tidy, and the clean-up.
  The branch is deleted with `-D` once the proof clears it, because git's `-d` asks only about the
  checkout's HEAD.
- **The tidy**: a landing rule may say `tidy`, and the tree and branch go as soon as a press lands the
  work (`--tidy`, and *tidy once landed* on the landing card).
- **The clean-up**: `SweepPlanAsync` lists every `daoris/` branch in each repository with a checkout
  here, as empty, landed (and where), unlanded (with its commits), holding uncommitted work, or in
  use by a running or waiting session. `SweepAsync` removes the empty and landed ones, only those the
  person saw listed, each judged again right before it goes.
- Two doors (D50): `daoris-driver trees clean [--yes]`, and Settings → Workspace → *Session branches*
  (`SWEEP_PLAN`, `SWEEP`). Projects names a repository whose session branches hold unlanded work.

**Proven by:**
- 9 `SweepTests` on real git: landed by merge, by feature branch and by remote; a chain step's work
  counted as unlanded; removal without force; every kind in the plan; the clean-up; work committed
  after the plan kept; the tidy; the file. With the person's local branches dropped from the proof,
  7 went red.
- 2 driver-module tests, 1 CLI test, 5 molecule tests and 4 stories, and 1 landing-card test for the
  tidy.
- The family rehearsal gains two checks: an unlanded branch is listed and kept by `trees clean --yes`,
  and an empty one is listed to go, then removed.
- On the scratch shell: the card listed the one real session tree's branch as holding nothing beyond
  `main`, the press removed it and its tree, and git then showed no session branch and only the root's
  worktree.

## HELP1 — ask Daoris (2026-09-29 → D89)

The row as filed: *(study §4; the design note is `docs/2026-09-29-ask-daoris-design.md`, its two calls
made as D89). A chat about Daoris itself, from a button on the strip, the palette and `F1`, in the right
dock. It knows the view and the selection. It helps by proposing Daoris's own terminal commands (D50),
each confirmed by the person, and turns start something into an ask. Starter prompts come from what the
machine lacks. It is a harness session in a room of its own, allowed only `daoris` and `daoris-driver`.
The owner's calls: its harness and account, and whether a confirmation may be remembered.*

**The owner's calls** (D89): its own agent under Settings → *Daoris's own AI*, off until named; and
always confirm, nothing remembered.

**Outcome**, in five landings, each looked at on the scratch window with real conversations:
- **Its agent** (`af014aa`): `helperAdapter`, `daoris driver helper <agent>|off`, `SET_HELPER`.
- **HELP1d, the panel** (`9759d83`): at the application's right edge rather than a tab of Sessions'
  dock, which closes with a change of view; its starters from what the machine lacks, each with its
  screen and command.
- **HELP1a, the conversation** (`a42219e`): a room under the home written from the driver's own
  answers, a chat in it recorded in `daoris:help`, one running per room. **Its session runs in the
  agent's own asking mode, the one exception to D81**: under `auto` the first real conversation ran
  shell commands over a checkout, and a helper able to run a command could run `daoris driver`
  unconfirmed. The strip's door became a named button, the owner having found no easy way in.
- **HELP1b, where the person is** (`c818525`): one agent-facing line ahead of the words, when it
  changed, kept in the record as a note and never as the person's; the attended session found among
  ended ones too. The rail's group says Ask Daoris.
- **HELP1c, proposals**: `setting_propose` and `ask_propose` write a file each; the driver judges
  one with the route's own code and names, shows the card or hands the refusal back to the
  conversation, and *apply* makes the screen's edit. Never `permission_propose`.
- **Not built**: a refusal the screen shows is not carried in the preface (nothing holds one in a
  shape the page can hand over), and "the machine as it stands" is read once, at each open.

**Proven by:** the service's help-session and proposal-box tests; the driver's room, chat (the
protocol door's mode, the connector, the preface) and proposal tests; the modules' routes; the
page's panel, conversation, preface and card tests; the family rehearsal, the deployment rehearsal
and Playwright, unchanged and green.

## WSR5 — a chain lands named after its first quest (2026-09-29)

The row as filed: *(found landing AR-2202, 2026-09-29). A chain lands from its last step (WSR1), so a
branch pattern's `{quest}` and `{slug}` are that step's: AR-2202's six commits landed as
`feature/verify-in-prod-that-ar-2202-s-empty-381807d1f7bd`, named for its verify step. The chain's
first quest, the one the ask became, should name the branch.*

**Outcome.** `LandingRules.SubjectAsync` walks up each step's `Parent` to the chain's first quest (the
service answers for closed quests), stopping where a parent is no longer answered for and after twenty
steps, and names the landing for it; a chat keeps its opening line. The review's press and the new
terminal door, `daoris-driver trees land <session> [--plan]`, both use it. AR-2202 itself landed
before this, under its verify step's name.

**Proven by:** `LandingTests.A_chain_lands_named_after_its_first_quest` (a three-step chain, a lone
quest, a parent the service no longer answers for, a chat); the family rehearsal's `trees land --plan`
check; the real chain's verify step reads `parent: 34a9d57b8fd5` on the owner's install.

## SURF11 — the layout toggles, reachable from the strip (2026-09-29, as DOCK1c)

The row as filed: *Filed on 2026-09-22 in a sentence of `platform-ux.md` §4 and never made a row,
which UX5 found (U23). VS Code's title bar holds the toggles that show and hide the panel, the sidebar
and the secondary bar, left of the window controls. Daoris has the three regions in Sessions (the rail,
the output panel, the dock), and each is toggled only inside it. Since D66, Sessions is one view among
seven, so the question is where the toggles belong before how they look: the strip, which is every
view's, or the View menu, which VS Code also carries them in. The state lives in `WorkFrame` and needs
hoisting either way.*

**Outcome** (DOCK1c, `docs/2026-09-29-dock-design.md`): both. The closings are the application's
(`work/closings.ts`, the same stored keys, so nothing reopens on the upgrade); `LayoutToggles` puts VS
Code's three pictures at the strip's right, pressed while shown, and only the right one away from
Sessions, where it is Ask Daoris's region; the View menu carries them ticked, with `Ctrl+B`, `Ctrl+J`
and `Ctrl+Alt+B`, and menu items gained a shortcut. Ask Daoris lost its own strip button at the owner's
word, and the dock's tabs shrink as a browser's do.

**Proven by:** `LayoutToggles.test.tsx`, the strip's trailing-slot test, the frame's suite unchanged
over the lifted state (527), and the scratch window: `Ctrl+B` closed the rail and its toggle followed,
the dock's close stayed inside its edge at its 300px floor. Looked at in light theme only; dark was
looked at with DOCK1b the same day, and reads.

## HELP2 — Ask Daoris's room knows the window (2026-09-29)

The row as filed: *(found by DOCK1d's look, 2026-09-29). Asked what the panel held, the helper guessed
that the View menu names each view's region, which it does not: its room describes the machine and its
setting doors, and nothing of the window. The room could say the regions, how a view moves (the tab
list, a drag, Reset view locations) and the layout keys, and the preface could carry where the views
stand now, so a question about the screen is answered rather than guessed (`Help.cs`
`HelpRoom.Render`, `help/where.ts`).*

**Outcome.** Both. The room gains *The window*: the activity bar's views, Sessions' four regions, the
four views that move and the three ways they move, *Reset view locations*, the toggles and every
layout key, Ask Daoris's and Quick Ask's keys, and that it cannot see the window and should ask rather
than guess. Each sentence was checked against the code that does it. On Sessions the preface now says
which views each region holds and whether it is showing, so a move is told with the next message.

**Proven by:** `HelpRoomTests.The_room_says_how_the_window_is_laid_out_and_how_a_view_moves`,
`where.test.ts`'s layout case, the driver's 901 and the web's 1345.


## SESS1 — the session view: its log and its working relationships (2026-09-28 → 2026-09-29)

The row as filed: *(study §5; in progress, its ledger `docs/2026-09-28-sess1-session-view.md` says
what the real sessions showed and what each finding became: all ten landed or were dropped. **Still
open**, the ledger's *not reached*: what a session waits on at the top, a tool's exit and duration,
and the relationships beyond the chain). One reading order with the console one press away. Long runs
folded, with jumps to the first failure and the last words, and search within a session. What it
waits on, at the top. Where it came from and what it caused, drawn as a thread from the ask to the
last step. Looked at on the real workspace's sessions.*

**Outcome.** The ledger holds each finding and what it became; in brief:
- **The log** (`c908ced`, `5716ba7`): a long run opens with its ask and keeps the agent's words in
  view with each run of work folded to a count; a turn the session ended inside reads *stopped*;
  *first failure*, *last words* and *find in this session*; the attended session marked on the chain.
- **What it left** (S10): the branch its own tree left and whether that landed, in the head.
- **A tool's output** (`1393ad1`): how long a call ran, by the driver's clock, and folded, how many
  lines it carried. An exit code is not a field on the wire, so it is not shown.
- **What it waits on**: already at the top with its one moving action; nothing changed.
- **Who it worked with** (this change): a quest now records the session that published it
  (`publishedBy`, from the `DAORIS_SESSION_ID` the driver hands every connector and now every
  native-door quest spawn), kept in the store, the log and the sync's wire. The head says who asked for
  its quest, the answer it carried on after (D79), and what it asked of other repositories with each
  one's status and answer; the chain strip's quests became doors in Work.

**Proven by:** the ledger's looks; `McpToolsTests.A_quest_says_which_session_published_it`, the sync
test's `publishedBy` across two machines, `IntakeTests.A_quests_spawn_names_its_session_beside_its_quest`,
`relations.test.ts`, `SessionRelations.test.tsx` and the frame's *asked of another repository* case;
on the scratch window, a quest published through the machine's real connector came back over HTTP
naming its session, and that session's head showed it in both themes. Not seen on a real session yet:
*Asked by* and *Carried on*, since nothing had published with the field before this.

## SESS3 — tell a running session something (2026-09-29 → D90)

The row as filed: *(owner, 2026-09-29: "there is no way to send additional info in middle of the
session"). A driven session takes no person's line (INT4i, `docs/2026-09-19-driver-design.md`): it is
handed its whole quest in one turn, the pipe door gives it no stdin, and on the protocol door its stdin
carries the driver's own frames. Only a session parked on the person is answered (STANDDOWN2). A design
note first, amending INT4i: a message box on a running driven session whose words are held and handed
over where the door allows. On the protocol door, as the next prompt of the same session when its turn
ends, instead of the driver closing it, or by stopping the turn and prompting again with them (the
conversation's stop, CONV4a). On the pipe door, as the opening of a carry-on session, the parked
answer's path. Kept on the record as the person's words. Nothing is ever written into a stdin that
carries frames.*

**Outcome** (D90, the driver design's INT4i paragraph amended). On the protocol door a driven quest
session has an inbox (`DrivenInbox`, in the process registry, since a session outlives its tick).
`SESSION_INPUT` holds the words; when the turn ends, `AcpSession.RunAsync` prompts each one in the same
session before closing it, recording it as the person's; `CANCEL_TURN` becomes *send now*, stopping the
turn and withdrawing nothing; `SESSION_QUEUE` says whether the session listens, and the page offers a
box only then, with the queue and *send now* as a conversation's composer has them. A late word is
refused and the person told; a failed session says how many never reached it. INT4i still refuses
anything written into the stream and a finish; an intake and a pipe-door session keep no box. The pipe
door's carry-on path was not built: its process has no stdin, and a carry-on per sentence loses the
session's context.

**Proven by:** `DrivenInboxTests` (six), `DrivenSessionInputTests.A_driven_protocol_session_hears_what_the_person_adds_as_its_next_prompt`
both ways against a stand-in agent that logs every prompt (the second prompt, one session, the record's
person event), INT4i's own test unchanged, the module's route test, the frame's two cases, the family
rehearsal's protocol phase. On the scratch window, a stand-in agent held its turn: the box queued a
message, *send now* stopped the turn, and the conversation read the stop, the person's words, and the
agent's reply to them in the same session.

## DOCK1 — panels that dock, as VS Code's do (2026-09-29)

The row as filed: *(owner, 2026-09-29: "we should be able to dock panels like vscode did", said beside
"there is no easy way to open the daoris chat"). Today each region has one place: the rail and the
right dock inside Sessions, the output panel under them, Ask Daoris at the application's edge. VS Code
lets a view move between the primary side bar, the secondary side bar and the panel, by drag or Move
to, and remembers where. A design note first: which views move (Ask Daoris, the console, the dock's
tabs, the rail), which places exist on every view rather than only in Sessions, and whether Move to
comes before drag. SURF11, the layout toggles, is the same question from the other end, so the two are
designed together.* Widened by the owner the same day: *"the design language we using in session
screen (dockable, right tool bar, top layout setup) should be apply to all screens (for example
overview) so this is more like the design language of vscode"*.

**Outcome** (`docs/2026-09-29-dock-design.md` §4, each step as built):
- **DOCK1c** (`0c1b708`): the region toggles on the strip and in the View menu with VS Code's keys.
- **DOCK1b** (`b3721e0`): the four views move between the side bar and the panel from a region's tab
  list or a tab's right-click, remembered, with *Reset view locations*; the tab list names every tab
  whole; the regions are *the side bar* and *the panel* in every label.
- **DOCK1e** (`a41f8f6`): a tab dragged to the other region moves there.
- **DOCK1d** (`b89aa8a`): Quick Ask, and the palette's last row asking what was typed.
- **DOCK1a** (this change): the frame on every view in a shell — one element, Sessions' centre or the
  view handed in, the side bar and the panel holding what they hold on Sessions, no rail elsewhere,
  Ask Daoris with one host, the toggles and the View menu on every view; the session views off
  Sessions say whose they are.

**Proven by:** each step's tests (the placements, the views menu, the drag, Quick Ask, the no-rail
layout, the frame with another view's content), the web's 1361 and Playwright's 21, and the scratch
window in both themes at each step: Overview and Quests framed, the side bar keeping its tab across a
change of view, the rail back on Sessions.

## SESS2 — the session's top section, again (2026-09-29)

The row as filed: *(owner, 2026-09-29: "we still need to improve the session ui/ux (currently the top
section still not good enough)"). The head today: title and state, the id, the repository, the tree's
path, the tool, started, running and moved; a parked session's card with its three moves; then How
this work ran and Who it worked with above the conversation. Look before changing: read the owner's
real sessions' heads read-only with SESS1's instruments, write what is wrong as numbered findings in a
ledger as SESS1's was, and fix by what was found. To judge, not decided: the tree's whole machine path
in the head, the chain and the relations pushing the conversation down, where the parked card sits,
and what is primary (what it is doing, what it needs from the person) against what is reference (ids,
paths, tool, times).*

**Outcome.** The ledger, `docs/2026-09-29-sess2-session-head.md`, holds seven findings from the
installed application's real heads, all fixed. An ended session opens at its head, not its tail. The
head reads in order of use: title and state, the record's note saying how it stands (a failure's
reason, how it ended; reversing the rule that left it to a timeline that had moved into a closed side
bar), what its tree left with **review** beside unlanded work, and one quiet line of reference with
the tree's path on hover. The chain is one line of stops, the attended quest as *this quest*, whole on
a press and remembered. The parked card keeps its place above: it is the reason the person is there.

**Proven by:** `SessionHead.test.tsx` (the note, its clamp and press, silence while running, the work
line and its review, the path on hover), `ChainLine.test.tsx`, `followTail.test.tsx`, the web's 1367;
and the installed application after, in 中文: the completed session's conversation starting about
450px down where it had been past 620, and the failed one saying the spend limit it failed on.

## MAP4 — the map, finished (2026-09-29 → 2026-09-30, D91)

The row as filed: *(study §6). A layered layout, pan, zoom and search for a big circle (the real one
has twenty-nine repositories). Asks, chains and what depends on what, each its own kind of line, from
a declared source and never guessed. Open-only or a time window. Sessions live on their nodes. Looked
at with twenty-nine nodes in both themes.*

**Outcome.** Five parts, each committed and each looked at on the window (map design §1a–§1e):
- **a:** past twelve repositories, columns by who asks whom, a waypoint slot for a line that skips a
  column, lanes for a pair asked both ways, the loose grid wrapped to the view, pan, zoom, search, and
  the size as a menu.
- **b:** asks and chains as lines of their own, switched from a *Lines* options menu.
- **c:** which quests: all, open only, or 7 or 30 days.
- **d:** a line's count ringed while a session is on one of its quests, and the ring's asks placed
  clear of every count.
- **e:** *says it uses*, from the repository's own `domain.uses` (D91), carried as twins by the CLI,
  the service and the desktop.

Along the way the owner asked for no arrows standing for what is not a direction (the zoom became a
sizing menu, a region's views menu "⋯", the scrollbars lost their arrows) and for a flatter look
(radii one step down, flat bars).

**Proven by:** `layers.test.ts`, `LayeredMap.test.tsx`, `topology.test.ts`, `MapView.test.tsx` (the
web's 1401), the service's `Declared.Uses` table and store round trip beside the CLI's `usesOf` table,
the desktop's payload and form tests, and the family rehearsal's two new checks (294/294). Looked at on
the real twenty-nine (dark) and a scratch circle of twenty-three (both themes). The one mark not seen
on the window is a live ring on a line: no session was on a quest during the looks.


## RAIL2 — a live chat's last move is its last turn (2026-09-30)

The row as filed: *(UX5 U62). The rail and the head say *moved* from the session record, which moves
on state changes only, so seconds after an answer a chat read *idle · moved 4m ago*. A per-turn record
write is the wrong fix: records sync, and it would carry a chat's activity to a teammate's machine
(D47 §4). The driver already knows when each chat's turn ended (`ChatRunner`); its `SESSION_QUEUE`
answer and `SESSION_QUEUED` event could carry it, machine-local, and the page could show the later of
the two. The driver, the bridge and the page change together, each with its test.*

**Outcome.** As filed. `ChatQueue` carries `LastTurnEnded`, stamped when a turn that reached the
harness ends and published even when a waiting message starts the next at once (which leaves `taking`
unchanged). The queue answer and event carry it as `lastTurn`. The rail row and the head say *moved*
from the later of it and the record, compared as times (the driver writes an offset, the record Z).
Nothing is recorded, so nothing syncs.

**Proven by:** `TurnStopTests` (two turns, two stamps, each told), the module's empty-queue answer
(`lastTurn` null, nothing claimed), `useChatTurns` carrying it from the answer and each change, and
`SessionRow.test.tsx` (the later of the two, in words). Not looked at on the window: neither machine
held a live chat mid-conversation during the work.

## SIGNIN1 — a sign-in outlives leaving the Agents domain (2026-09-30)

The row as filed: *(web-rest F4). The running action is the domain component's state, and
`HARNESS_ENDED` is heard only while it is mounted. Leaving mid-login loses the code panel and the end
notice. Lift the running action above the domain.*

**Outcome.** As filed. `HarnessRuns` (`src/harnessRuns.tsx`) holds a tool's running action — which
action, whether it is still in flight, which account a sign-in is for — with the mutation that starts
it and the one listener for its end, and says the ending sentence. The application mounts it above
every view; the Agents domain reads it through `useHarnessRun`. Coming back mid-sign-in finds the code
panel on its row, and the end is said wherever the person is. Settings rendered alone wraps itself in
its own (`WithHarnessRuns`), never a second one beside the application's. The mutation moved too:
a callback passed to `mutate` does not fire after its component unmounts, so *in flight* would never
have been set.

**Proven by:** `shell.test.tsx` — a sign-in started, the domain left and re-entered with its panel
still there, and its end said while away — beside the existing sign-in tests, and the web's 1404. The
presentational boundary test placed the provider at the root, beside `shell.ts`, rather than under
`settings/`.

## WINDOW2 — a secondary window's caption follows the chosen theme (2026-09-30)

The row as filed: *(found closing WINDOW1, 2026-09-27). Since WINDOW1 the page inside a monitor or a
detached session follows the viewer's choice live, and its native title bar stays on the OS theme, so a
dark choice on a light OS shows a dark page under a light caption. That was seen on the window.
`SecondaryForm` follows the OS directly, because `WindowCommandModule`, the main window's `SET_THEME`
channel, targets one form, and its module name is reserved and singular (D55 §b). The same caption
showed before WINDOW1, when a window opened on a chosen theme. The fix needs a channel for the
secondary window's own frame: the page's `setTheme` there, and a handler bound to that form. That is
the shell's code, and Shenora's command module may need to grow.*

**Outcome.** A channel of Daoris's own, so Shenora did not need to grow: `DAORIS.WINDOWS` answers
`SET_THEME { name, dark }`, the name checked as `OPEN` checks it, and `SecondaryWindowHost` keeps each
open form by name and paints the theme on its thread. The page in a secondary window tells it on
arrival and on every change (`useSecondaryWindowTheme`). Once the page has spoken, the form stops
following the OS, so an explicit choice is not undone by the OS turning. Shenora 0.17 routes a
second window's other commands to that window, but still answers its `SET_THEME` with NO_ROUTE.

**Found by looking**: the route alone changed nothing on the window. `OptimizedForm.ApplyChromeTheme`
sets the DWM caption only for a frameless form (0.16 and 0.17 alike), so a framed secondary window's
caption had never followed any theme, the OS's included. `SecondaryForm` now sets
`DWMWA_USE_IMMERSIVE_DARK_MODE` and the border colour on its own handle and asks the frame to repaint.
A request for Shenora's owner: `OptimizedFormOptions.ImmersiveDarkMode` and `ApplyChromeTheme` are
silently ignored on a framed form; they could apply there too, or say they do not.

**Proven by:** `WindowsModuleTests` (a window's theme reaches the host by name; an unknown name is
refused), `shell.test.tsx` (the page tells its frame on arrival and on a change of choice), and the
scratch window on a light OS: the monitor's caption dark under a dark choice, and light the moment the
main window chose light.

## CHR2 — the main and secondary windows on `ChromiumView` (2026-09-30, D92)

> - [ ] **CHR2 — the main and secondary windows on `ChromiumView`** (Shenora 0.17, D92; the contract's
>   §4a). **CHR2a:** the page on the app origin, reaching its host at a loopback address the shell
>   gives it, which the host allows in local mode (a browser keeps its own origin). **CHR2b:** the shell
>   on `UseChromiumEngine` and `ChromiumView`, the app assembly renamed so `daoris-desktop.exe` is CEF's
>   launcher, the splash and the trouble message on the new host. **CHR2c:** the tools: the dev loop's
>   instruments on the development DevTools port, and `run --install` starting the install as development.

**Outcome.** CHR2a landed first (the page's `?host=`, accepted only when loopback and only in the
shell; the host's CORS allowing `https://daoris.localhost` in local mode). CHR2b put the shell on
`UseChromiumEngine` and a `ChromiumView` per window: `DesktopPage.PathFor` gives each window its path
on the app origin, `DesktopPage.BundleOf` finds the bundle the host serves, and the WebView2 runtime
check went with WebView2. CHR2c taught the instruments the new page: `isShell` tells this run's shell by
the host its page reaches (every shell's page has the same origin), and a development run passes
`DAORIS_DEVTOOLS_PORT`. The application's name became D93's.

**Found by looking**: the bundle a development host serves is its project's, not `bin/`'s; a Radix
menu needs real pointer input over CDP; and the native caption buttons are read with `WM_NCHITTEST`,
which answered minimize, maximize and close from the rectangles the page reported
(`docs/2026-09-28-chromium-host-design.md` §5a).

**Proven by:** `DesktopPageTests`, `WindowsModuleTests`, `host.test.ts`, the family rehearsal's origin
checks, `desktop-tool.test.ts` (`isShell`), and the scratch window on Chromium: the main window, the
monitor and a detached session, in both themes, the secondary caption following dark.

## CHR4 — the install carries it (2026-09-30, D93)

> - [ ] **CHR4 — the install carries it**, and the WebView2 path goes: `publish:desktop` lays out CEF, and
>   the deployment rehearsal starts the published shell on it and asserts which engine answered.

**Outcome.** An install is a launcher, `app/` and `data/` (D93), the structure the owner pointed to: a
framework-dependent single-file `Daoris.exe` (about 220 KB, Daoris's icon) that starts
`app/Daoris.Desktop.exe` and exits; the application beside its Chromium in `app/`, its names recorded
in `app/shell-files.txt` so a republish removes the last engine's files; the browser and the host in
folders of their own under `app/`. `InstallHome.RootOf` finds the install above `app/`, and the kit is
handed the same root, so the home and the engine's profile stay in the install's `data/`. The build
stamps Daoris's icon and name onto CEF's launcher (`StampIdentity.targets`), both engines keep two locales, and the
process helpers tell the application from Chromium's own processes (`--type=`), which a blind stop had
walked for fifteen seconds each and then killed.

A first cut put the application at the root beside its engine, as a Chromium application's root; it
passed 52/52 and was set aside for the launcher layout on the owner's direction.

**Proven by:** `npm run rehearse:deploy` 57/57: the root holds only what makes an install, the launcher
is small and hands over and exits, the page renders in a renderer from the install's executable with no
WebView2 under the shell, the home is the install's `data/` and none is made in `app/`, and a republish
removes the retired `daoris-desktop.exe` and a recorded engine file it no longer ships while leaving a
neighbour's file alone. `InstallHomeTests`, `desktop-publish.test.ts` (the launcher, the publish and
the app's assembly name agree), `desktop-tool.test.ts` (the engine-process filter, an install found by
either layout).

## CONSOLE3a — stop a background task from its tab (2026-09-30)

> **stop a background task from its tab**, which the adapter takes as `_session/async_task/stop`
> (`canStop: true`) — the first of CONSOLE3's four.

**Outcome.** A task keeps its harness's `canStop` (`SessionStream.CanStop`), and the streams list says
whether each can be stopped now: running, and its harness said so. `AcpSession.StopTaskAsync` sends
`_session/async_task/stop` with the session and the task and reads `{ stopped }`; each door registers it
in `SessionProcesses` beside the inbox for as long as its session is open, and `STOP_TASK` reaches it by
the stream's key, refusing a key that is not one of that session's tasks. On the page the task in view
carries its stop after the tabs, as VS Code's panel carries *Kill Terminal*: not inside the tab list,
which owns tabs only. The stream ends on the wire's word after the request, and the session's console
says `■ background: … stopped`.

**Found by looking**: an ended stream's console still said *live*, because a stream's end reaches the
page only as its session's streams changing. The console tailing a stream now asks again on that event.
The frame tests' event mock kept one handler per event name, so the console's new listener silenced the
tabs' one: every listening hook hears an event now, as in the shell.

**Proven by:** `AcpTests` (a stoppable task stopped over the wire, ending on the wire's word; a stop
before the session opens sends nothing), `TaskStopsTests`, `DriverModuleTests` (listed stoppable; the
stop reaches its session by the task's id; a subagent's key and another session's are refused),
`streams.test.ts`, `WorkFrame.test.tsx` (the stop in view, none where the harness gave none; the console
stops calling an ended stream live), and the scratch window with a stub agent whose dev server ticked
into its tab until stopped. The real adapter's answer to the stop is unseen.

## CONSOLE3b — tabs in the detached session window (2026-09-30)

> **tabs in the detached session window**, whose console is still the session's alone — the second of
> CONSOLE3's four.

**Outcome.** A session in a window of its own carries its streams as the main window's panel does: the
session's console, then a tab for each subagent and task, the picked one shown, one it no longer lists
falling back to the session. With no stop: nothing in that window acts (D56's one owner across windows).

**Found by looking**: the detached window headed a chat `conversation · working` beside a main window
heading it `start the dev server · idle`, because it passed its head neither the chat's opening nor its
turn state. It passes both now, as the monitor does. And the command palette's *Open this session in
its own window* only seemed to do nothing: pointer presses on its rows over CDP did not land, and the
same command by keyboard opened the window.

**Proven by:** `DetachedSession.test.tsx` (the tabs, a picked stream, no stop; the head by the opening,
idle between turns), and the scratch window: a stub agent's dev server ticking in the detached window's
own tab, headed as in the main window.

## CONSOLE3c — the native door's streams (2026-09-30)

> **the native door's streams**, since only the protocol door asks for them and no probe has read
> `stream-json`'s `parent_tool_use_id` or its task messages — the third of CONSOLE3's four.

**Outcome.** Probed first (`tools/console3-probe.mjs`, one small real turn,
`docs/2026-09-30-console3-native-streams-evidence.md`): a subagent's lines carry the `parent_tool_use_id`
of the call that spawned it; background work is announced on `system` lines (`task_started`,
`task_updated`, `task_notification`); a backgrounded command's file is named only in its result's words
while it runs; and the binary kills its background work when its turn ends. Then built: the mapper
offers a `Beside` reader (`IStreamsReader`), and `ClaudeStreams` takes the lines that are a stream's
before the session's reader sees them: a subagent's lines to its own stream, a `local_bash` task to a
stream that reads its file, each ended in the wire's word and said on the session's console. No stream
here is `CanStop`: this door has no request for it. Both the driven and the chat capture use it.

**Found by looking**: a chat watched to its end kept saying *live*. The relay sends lines a window after
they are written, so a session's last lines went out after it closed, and the page read every batch as
live. A batch now says whether its console still runs (`SessionOutput.IsLive`), and a console asks again
when its session's ending is told.

**Proven by:** `NativeStreamsTests` (a task and a subagent as their own streams, ending in the wire's
word; one open at the end ends with the session; without a console, the lines are the session's), the
console's tests (`SessionOutputTests`, `ProtocolChatTests`, `WorkFrame.test.tsx`), and the scratch
window with a stand-in `claude` replaying the probe's frames: a tab for the command reading its file,
one for the subagent with its `Read` and its reply, and a finished chat no longer called live.

## REFUSE1 — the refusal rule, enforced (2026-09-30)

> - [ ] **REFUSE1 — the refusal rule, enforced** (modules F10). `Refusals.All` is kept by hand, so a
>   code left out of it escapes the catalogue test, and nothing checks that a throw site uses a
>   declared code. Enumerate the constants by reflection and scan the throw sites.

**Outcome.** `Refusals.All` is read off the declared constants by reflection, so a new code is walked by
the translation check the moment it is declared. A scan over the modules and the shell holds every
`Refusals.Because(…)` to a declared `Refusals.<name>`, refuses a `ShenoraException` built anywhere but
the catalogue, and proves it saw the throw sites (more than ten), so a moved helper cannot make it pass
by matching nothing. No throw site needed changing.

**Proven by:** `RefusalCatalogueTests` (every declared code is walked; every throw site names one),
modules 305.

## HOSTID1 — the shell adopts whatever answers `/api/status` (2026-09-30)

> - [ ] **HOSTID1 — the shell adopts whatever answers `/api/status`** (modules F7). `HostSupervisor`
>   takes any process answering on the service port as this machine's host, and hands its page the
>   full bridge. The shell needs a way to tell its own host from another process, such as a token it
>   passes at spawn, or the install path the host reports.

**Outcome.** The supervisor adopts an answer only when it is a Daoris host's: a status object naming its
search tier (`tier`), which every version of the service gives. Anything else on the port is refused
with a sentence naming the address and what to do (stop what holds it, or move the service), and no
host is started beside it, where it could only fail to bind. Since D92 the page is the install's own
bundle, so what a foreign answer could have reached was the page's data, not the page.

**Not taken:** a spawn token or a reported install path. Adoption of another Daoris host (a terminal's,
another shell's) stays the rule, and two Daoris hosts are already told apart by the page each serves
(the adoption notice). The status reporting an install path would tell a browser about the machine
(D47 §4).

**Proven by:** `HostSupervisorTests` (something else answering is refused and said; a Daoris answer is
adopted as before), and the deployment rehearsal's adoption of the installed host.

## HELP4 — what the first real repository question found (2026-09-30)

> *"I think I just found some issue in ask daoris (you might check the log) 1. its failed for user
> permission automaticly, also my input does not reflect in the chat directly (showed by a huge delay)
> this state need to be updated 3. and I ask to cleanup the git tree just leave a clean branch for
> [a ticket's] pr but I think it confused"*
>
> - [ ] **HELP4 — what the first real repository question found.** (a) Its first move was a shell
>   command, refused by construction (D52), because the room says *you read* and never that it has no
>   shell; the refusal then read as a failure. (b) Unable to look, it rebuilt a repository's branches from
>   the quest ledger and presented the guess as a table; the room never says to route a repository's own
>   work there (an ask, or a conversation in that repository) or to name what it could not see, and its
>   own doors carried the cleanup the person wanted (*Session branches*). (c) With none running, the
>   person's first words appeared about six seconds late: the conversation opens first, and nothing
>   shows the words meanwhile. Whether the room should read checkouts at all (git status, a file) is a
>   widening of D89, so it is the owner's call and not taken here.

**Outcome.** (a) The room says it has no shell and reads no checkout, so a command is never tried; and
a call the driver refused is recorded as `refused`, not `failed`, so the conversation reads *not
allowed here* in a quiet tone instead of an error. (b) The room says a repository's tree is that
repository's to read and change: propose an ask, or send the person to a conversation there, name
Daoris's own door first where one exists (*Settings → Workspace → Session branches*), and say what
it could not see rather than build the tree from quests. (c) The first words show the moment they
are sent, under *opening Ask Daoris…*, and stay until the driver's queue answers for the new
conversation, so they never vanish while the session list catches up. The driver's queue now says
`opening` beside `taking`: words held for a door still opening had been counted as a turn in flight,
so every conversation's first message read *waiting for this turn to end* when nothing had answered.
The composer follows `opening`, on Sessions as in Ask Daoris.

**Not taken:** letting the helper read a checkout. That widens D89's allow-list, so it is held as
HELPREAD1 for the owner. The branch cleanup itself belongs to that repository, through a
conversation there or the Session branches door.

**Proven by:** `HelpRoomTests` (the room says it has no shell and routes a repository's own work
there), `AcpTests` (a call the driver refused is recorded as refused), `TurnStopTests` (words sent
while the door opens are told as waiting for it), `DriverModuleTests` (the queue answer carries
`opening`), vitest (`ConversationView`: a refused call reads *not allowed here*; `Composer`: the label
follows `opening`; `AskDaoris`: the first words show at once and stay through the list's gap, shown
once), and a look at the window with a slow stand-in agent: the words under *opening Ask Daoris…*,
then in the conversation as *you*.

## LOG1a — the machine log's writers and lifecycle (2026-09-30, D94)

> - [ ] **LOG1 — a log of what happens on this machine, to improve Daoris from.** … (a) **One log,
>   machine-local**: JSON lines under the home's `logs/`, one file a day, kept for a bounded number
>   of days, written by every process (the shell, the driver loop, the host, the browser), each line
>   naming its source, its event and its fields. Unhandled exceptions from every process land there.

**Outcome.** Every Daoris process now writes `logs/<date>.<source>.jsonl` under the home: the shell
(`desktop`), the headless driver (`driver`), Daoris's browser (`browser`), the HTTP host (`host`) and,
beyond the row, the knowledge host each session starts (`mcp`), whose standard error belongs to the
agent that started it. Two writers share the format and no code: the driver library's `MachineLog`
(the shell, the headless driver and the browser) and the service's (both hosts), each with the same
line table in its tests. Each process writes `app.started` (with its version and mode) and
`app.stopped` when it ends itself, every unhandled exception as `error`, and the logging frameworks'
warnings and errors as `log`; the HTTP host adds `request.failed` for a 5xx or a request over two
seconds, by route pattern and never its query. Thirty days are kept, a file stops at 20 MB saying so
once, and no home or a failing folder writes nothing and throws nothing. Seen on the scratch window:
the desktop's start and stop and the host's start, one file each.

**Not covered:** the HTTP host is ended by force when the shell closes, so its `app.stopped` is not
written; the desktop's stands for both. The events of what the person does are LOG1b.

**Proven by:** `MachineLogTests` (driver, 11: the shared line table, a file per source per day,
retention, the cap, no home, a failing folder, many threads, a reader beside the writer), the
service's `MachineLogTests` (8, the same table), `MachineLogProviderTests` (2: warnings and errors
from any category, nothing below), and every gate: driver 935/936 with FLAKE1's known case passing
ten runs alone, modules 308, service 561, verify, family 297/297, deploy 57/57, web 21 Playwright.

## WORK1 — the first real workspace's landed ticket, cleaned up; its next ticket started (2026-09-30)

> - [ ] **WORK1 — the first real workspace's landed ticket, cleaned up; then its next ticket started**
>   (owner, 2026-09-30). The landed ticket's two tasks left their work across session branches in the
>   repository that owns it; the owner wants one clean branch to open a pull request from and merge,
>   and then the next ticket, already a quest there (twice: two asks made the same quest), started by
>   the local Daoris. …

**Outcome.** The owner merged the pull request themselves while this was read: the follow-up task's
branch, built on the first task's six commits, carried both. Before anything was removed, both
leftover branch tips were confirmed ancestors of the merged branch and every file it changed was
confirmed identical in the target, since a squash merge leaves git unable to call them merged. The
leftover session tree and its branch went through Daoris's own door (`daoris-driver trees remove`).
The duplicate quest was declined naming the one kept, the repository's hold was released with
`daoris driver resume`, and the driver took the next ticket's quest on its next tick.

**Not done, and why:** deleting the two landed feature branches (`git branch -D`, since a squash merge
makes `-d` refuse) was refused by the session's permission policy as destructive, so they are the
owner's to delete; the private notes name them and their tips. Nothing was pushed.

## SHEN1 — the kit's 0.18.0 (2026-09-30)

> - [ ] **SHEN1 — the kit's 0.18.0** (owner, 2026-09-30: *"shenora is updated to 0.18.0"*). Move every
>   Shenora package from 0.17 to 0.18.0, read its changelog for what the shell relies on (the Chromium
>   engine, the frame, the IPC modules, the window state), and run the deployment gate on the result.

**Outcome.** `Shenora`, `Shenora.Windows`, `Shenora.Chromium` and `@shenora/react` are at 0.18.0, and
nothing in the shell had to change to build or run on it. Two of Daoris's own workarounds became the
kit's: 0.18 lays CEF's launcher out wearing the app assembly's icon, title, product and version, so
`StampIdentity.targets` (270 lines of an inline MSBuild task) is gone and the app project names
`Daoris` as its title and product; and `ShenoraChromiumLocales` lays out only the engine's zh-CN and
en-US, which the publish script still trims for the browser's own engine until CHR8. The kit's
`ChromiumBrowserProcess` is the browser-only engine CHR8 waited for, so CHR8 is unblocked; its CEF
154.0.32 fixes a browser-process crash when a debugging client opened a tab in an existing window.
Proven on a clean build: the executable read `Daoris` / `Daoris` with Daoris's icon after the old one
was deleted.

**Proven by:** driver 936, modules 308, web 1419, verify, deployment rehearsal 57/57 (the launcher,
the engine, the identity), Playwright 21, and the family rehearsal (see the commit for its run).

## USE1b, USE1d, USE1e — the Quests view fits the window, a held quest says so, and the side bar keeps its size (2026-09-30)

> - [ ] **USE1 — what the owner met on the installed window, 2026-09-30** … (b) **The Quests view
>   overflows the window.** … (d) **A new request does not start on its own.** … (e) **the right side
>   bar's size breaks after Ask Daoris is moved to the panel and back** …

**Outcome.** (b) and (e) were one defect. Every view but Sessions is drawn inside the work frame, and
the frame's root was a flex item with no `min-w-0`, so it could be no narrower than its content: a
long quest title that should have truncated widened the frame past the window, and the right side
bar was placed against that wider frame. Measured on the scratch window with a long title: without
the fix the side bar sat at 1687–2224px in a 1193px window, and where it landed after moving Ask
Daoris to the panel and back depended on what the centre held; with it, the page is the window's
width, the title truncates (449px of 1481), and the side bar stays at 537px through the round trip.
A browser draws the view outside the frame, which is why it fitted there. (d) was a hold: both of the
ticket's quests were addressed to a held repository, which the quest's drawer said and its card did
not. The card now carries the driver's reason for any quest it leaves waiting and, for a hold, a
*resume* that lifts it where it is read, without opening the quest.

**Proven by:** vitest 1421 (a structural test walks every flexible box between a framed view and the
frame's root, and fails naming the root without the fix; a held quest's card says why and resumes
without opening the drawer), Playwright 21, verify, and the scratch window's measurements above.

## LOG1b — what the person does, in the machine log without their words (2026-09-30, D94)

> - [ ] **LOG1** … (b) **What the person does, without their words**: the page reports views opened,
>   commands run, sessions started, stopped and finished with their durations, messages sent (their
>   length, never their text), proposals applied or dismissed, refusals shown by code, errors caught;
>   the driver reports each start's cost …

**Outcome** (built by a subagent in its own worktree, merged and re-tested on main). The service client
grew an `Opened`/`Moved` seam raised only once the ledger says yes, and `SessionLog` turns it and the
conversation record's stamped events into `session.started`, `session.opened`, `session.ended`,
`turn.answered` and `turn.ended`, for the shell's loop (`desktop`) and the headless tick (`driver`);
anything it cannot time is null, never zero. Every refusal the bridge answers is written by code and
request through `RefusalLog`, a middleware in the kit's dispatcher; its sentence never is. The page
reports over `DAORIS.LOG` · `EVENT` with a fire-and-forget `logEvent` (silent in a browser), and
`LogModule` enforces D94: only the catalogue's events and fields, each of its declared kind, a
message kept only as its length and file count, text cut at 120 characters. The design's §4 says what
each line measures, including that a driven session's `openMs` is the wait to spawn (its target is
recorded as capture starts).

**Not covered:** the terminal conversation (`daoris-driver chat`) writes no session lines yet.
**Proven by:** driver 949, modules 327 (the filter broken three ways turned its tests red each time),
vitest 1426, on main after the merge.

## HELP5 — Ask Daoris answers sooner (2026-09-30)

> - [ ] **HELP5 — Ask Daoris answers sooner** (owner, 2026-09-30: *"and why there is a really long
>   wait for "ask daoris""*). … (a) **The knowledge tools in the first request** … (b) **The room says
>   it has no web either** … (c) **Open the conversation when the panel opens** …

**Outcome** (built by a subagent in its own worktree, merged and re-tested on main). On the owner's real
conversation the first word came about 30 s after the words reached the agent, over five model round
trips, three of them Daoris's to remove. (a) The help room's spawn carries the adapter's own switch
for loading tools up front (`ISessionAdapter.ToolsUpFront`: `ENABLE_TOOL_SEARCH=false` on both Claude
Code doors, empty elsewhere); a repository's conversation keeps the harness's default. (b) The room
says it has no web fetch or search, in the same sentence as the shell. (c) The page opens the
conversation when the panel is shown, so the spawn and `session/new` are done before the person
types: once per showing, again after *New conversation*, never beside a running one, silent when
refused; the pre-opened one stays hidden until spoken in, so starters and an ended conversation stay
in front, and words sent while it opens wait for it.

**Not covered:** that the variable reaches `claude` through the adapter's Agent SDK and removes the
search step rests on the binary's reading; a real conversation on the install confirms it. **Open:** an
unspoken pre-opened conversation still leaves a session record, which can be the newest after a
restart. The model's own latency (the account runs a 1M-context model at effort `xhigh`) is the
owner's, and AGT6 gives it a door.
**Proven by:** driver 961, vitest 1430 on main after the merge.

## USE1a, USE1f — Update does what it says, and the desktop finds an agent npm put on PATH (2026-09-30)

> - [ ] **USE1** … (a) **Update on a door with no updater** … (f) **the desktop does not find an
>   npm-installed agent on `PATH`** …

**Outcome** (built by a subagent in its own worktree, merged and re-tested on main). (a) On a pinned
door with a package or a channel, Update finds the newest release as one exact version (`npm view`,
or the channel's `latest` pointer, which only picks the version: it is then verified like one typed),
installs and pins it through the pin's own path, writing the pin only once that version is installed;
it says `0.79.0 → 0.84.0`, or that the pin is already newest and nothing was fetched, and never moves
a pin backwards. An unpinned door with its own updater runs it; a door with neither shows no Update,
which the roster's new `updates` field (`pin`, `tool`, null) decides. `daoris agent update <agent>
[--workspace W]` is the terminal's side. (f) The probe and both doors' spawns resolve a bare command
through the one PATHEXT resolver the plugin door uses, so an agent npm installed as a `.cmd` is found
and started; a prompt a shim's `cmd.exe` would reinterpret (the pipe door's multi-line target) is
refused in a sentence naming the fix (pin a version, or name a path in `commands`).

**A side effect, owned:** while proving its new tests fail, the subagent briefly disabled the new
branch and two tests fell through to the tool's own updater, running this machine's real `claude
update` and `codex update` (Claude Code moved from 2.1.284 to 2.1.285). The tests now run with an
empty PATH or a no-op updater and cannot start a real one.

**Not covered:** the real `npm view` and the real channel pointers are stand-ins in every test.
**Proven by:** driver 991, modules 329, vitest 1433, CLI 535, on main after the merge.

## USE1c — an ask whose work is finished leaves the list (2026-09-30)

> - [ ] **USE1** … (c) **A completed quest is not cleared** from the view. … *Read:* an ask stays
>   *published* after every quest it became has closed, so the list never empties.

**Outcome** (built by a subagent in its own worktree, merged and re-tested on main). An ask is done when
it became at least one quest and none of the quests asked by it, chain steps included (a step waiting
on another repository is taken, so open), is open or taken. It is derived on every read, never stored,
so a quest closed on another machine counts the moment the sync brings it in, and the terminal, the
MCP host and the page give one answer. The default list hides a done ask as it hides a closed one;
*include closed* shows it with a *done* pill after the live ones. A closed ask stays closed, the same
words asked after an ask is done make a new ask, and a parked intake whose ask is done ends answered.
D65 is amended with the rule and why storing it was rejected.
**Proven by:** eight desk tests, a driver test and vitest, then service 602 and vitest 1447 on main.

## QUEST1 — delete a quest or an ask made by mistake (2026-09-30, D95)

> - [ ] **QUEST1 — clear or delete a quest** (owner, 2026-09-30: *"and we do need way to clear or
>   delete quest"*). … Deleting is new: a quest or an ask made by mistake … removed from the ledger, on
>   both doors (D50). …

**Outcome** (built by a subagent in its own worktree, merged and re-tested on main). A quest goes only
while it is open, no session record names it, and no taken quest waits on it; a taken, done or
declined quest keeps its record, and the refusal says to decline it or leave it closed. An ask goes
with every quest it became, or the whole delete is refused. A delete is a `deleted` operation in the
quest's history: the remote keeps it, so no later sync (a new machine syncing from zero included)
brings the quest back, and a quest that never left the machine is simply removed. A shared quest is
deleted by push: confirmed, lost to a take that got there first, or unconfirmed while offline. Doors:
*Delete* in the quest drawer and on the ask's record, each confirmed once and shown only where the
service's `deletable` allows; `daoris-driver quest delete <id>` and `daoris-driver ask --delete
<id>`; `DELETE` routes on a local host only, and no MCP delete (deleting a record is a person's act).

**Known gap:** when a take loses to a delete, the driver's sentence still says another machine's
take won. **Proven by:** the quest log, sync (including no resurrection), exchange and desk tests,
service 602, vitest 1447, driver, and the family rehearsal's new delete checks.

## CHR8 — Daoris's browser onto the kit's engine, for one Chromium (2026-09-30, D99)

> - [ ] **CHR8 — the browser onto the kit's engine, for one Chromium** (D92). … **Unblocked by Shenora
>   0.18.0 (SHEN1)**: `ChromiumBrowserProcess` is that engine … Moving `daoris-browser` onto it drops
>   CefSharp and the install's second CEF.

**Outcome** (built by a subagent in its own worktree, merged, then looked at and rehearsed on main). One
Chromium in the install: Daoris's browser is `Daoris.Desktop.exe` started with `--daoris-browser`
first, which `Main` hands to Shenora 0.18's `ChromiumBrowserProcess.Run` before anything else; the
shell starts it with `ChromiumBrowserProcess.Start`, never `Process.Start` (which on Windows handed the
browser a pipe of Chromium's and kept the app from exiting). The CefSharp project, Daoris's own
`CdpRelay` (the kit's relay replaces it) and `app/daoris-browser/` are gone, and a republish removes
that folder by name. Its options keep their four fields, spelled `--daoris-<name>=<value>` because the
command line is Chromium's too. The tools tell the browser from the application and the engine's
`--type=` processes by its first argument, so a stop never walks it. Seen on the scratch window: the
palette's *Open Daoris's browser* opened Chromium's own window with its tabs, address bar and the
Daoris favorites folder, and the browser closed with the shell.

**Open (D99):** the Edge path (BRW12) still starts Edge with `Process.Start` and was not measured for
the same exit hang. **Proven by:** deployment rehearsal 58/58 (no second engine, the retired folder
removed, one browser process from the app's executable, gone with the shell), modules 320, driver
994, CLI 538.

## SETUP1 — a first-use setup guide (2026-09-30, D97)

> - [ ] **SETUP1 — a first-use guide.** A fresh install opens on a guide rather than an empty window,
>   and the guide stays reachable later (the Daoris menu, the palette). …

**Outcome** (built by a subagent in its own worktree, merged, then looked at and adjusted on main).
**SETUP1a:** *Get started* is Settings' first domain: six steps in setup order, each with its state
read off the machine, the screens that do it, and the terminal commands that do the same, copyable.
One pure reading (`readMachine`) serves the guide and Ask Daoris's starters, and a table test holds a
step and a starter to one answer. Step 1 counts a sign-in as the roster does, so *unknown* is not *to
do*; step 5 is done only when something is driven and every driven repository has a line and an
explicit landing rule; step 6 is optional and never done. A browser sees step 3 and a sentence. Doors:
the Daoris menu's and the palette's *Set up Daoris*, and a last button in the starters. **SETUP1b:** a
machine missing an agent, Daoris's own agent or a repository opens on it at start, decided once after
the machine is read; *Don't open at start*; *setup: n of 5* in the status bar; *Set up with Ask
Daoris*, which opens the side bar on a first message listing the steps left. A question handed to Ask
Daoris is now let go once sent, where each redraw had asked it again (FIX-LOG); the room names the
guide.

**Looked at** on the scratch window with an emptied home (light, dark, 中文): the guide opened at
start on *2 of 5*. Beside the side bar the card is narrow, and the rows' fixed two-column grid broke a
command mid-word and a door's label onto two lines; the rows now wrap, the doors drop under the text,
and the page never scrolls sideways (measured: scroll width equals the column's).
**Proven by:** vitest 1491, Playwright 21, verify, and the window.

## CONSOLE4 — a terminal in the console panel (2026-09-30, D96)

> - [ ] **CONSOLE4 — the console takes input: a terminal, PowerShell by default.** … (a) The terminal …
>   (b) More than one, and a choice of shell … (c) Desktop-only (D47 §4) …

**Outcome** (built by a subagent in its own worktree, merged, then looked at on main). **4a:** a Windows
pseudo-console in the driver library: the shell is started suspended and joined to the session
processes' job before it runs, so closing a terminal ends everything it started; the pseudo-console is
told the shell has no standard handles (measured: a shell started from a process with redirected
handles wrote to those instead); output is batched every 16 ms, since here it is a keystroke's echo.
Shells: `pwsh`, else `powershell`, then `cmd` and Git Bash (the bash beside git, never WSL's), found
through the plugin door's resolver. `DAORIS.TERMINAL` carries `SHELLS`, `OPEN`, `INPUT`, `RESIZE`,
`CLOSE` and two events; every terminal ends with the app, and nothing typed or printed reaches the
machine log. **4b:** a *Terminal* view beside the console, on `@xterm/xterm`, held by the frame so it
survives a move; it starts in the attended session's tree, else the first repository in scope, else
the home; colours come from the tokens; Ctrl+C copies or interrupts, and the frame's own keys stay the
frame's. **4c:** tabs, each its own shell, named by shell and folder, *+* offering only the machine's
shells.

**Looked at** on the scratch window: the tab read *Windows PowerShell · game*, in the attended
session's repository; `git status` and `Get-ChildItem` ran with PowerShell's own colours, legible in
light and in dark. **Not covered:** a page reload leaves shells running until the app exits (no
re-adoption yet), and xterm is not lazy-loaded (the bundle grew about 350 KB).
**Proven by:** driver (pseudo-console, shells), modules 336, vitest 1529, and the window.

## AGT6 — the agent's own settings, from Daoris: model, effort and the rest (2026-09-30, D98)

> - [ ] **AGT6 — the agent's own settings, from Daoris: model, effort and the rest** (owner, 2026-09-30:
>   *"we also need way to adjust the claude setup (since we have command to setup model effort or other
>   setting in console but no way in daoris rn)"*). …

**Outcome** (built by a subagent in its own worktree, merged, then looked at and fixed on main). **6a:**
an account's `model`, `effortLevel` and per-model `modelSettings.<model>.effortLevel`, in Claude Code's
own `settings.json` in the account's folder (read from the ACP adapter's settings reader and the Agent
SDK's schema; `max` is session-only there, so it is refused as a default). Two doors, twins sharing no
code: *Model & effort* on each account in Settings → Agents & accounts, and `daoris agent settings
<agent> [--account] [model] [effort [--for]]`. A write moves only the keys named and keeps every other
key; a malformed file is refused; the tool's own configuration home is never written; Codex and dsh
are offered nothing. **6b:** a conversation on the protocol door offers its model and effort beside the
composer, as its agent offered them on `session/new`, changed with `session/set_config_option` and
followed through the agent's own `config_option_update`; the mode stays the posture's (D37, D81), and
each change is a note in the record. D98 records the reversal of D49 §7.

**Looked at, and fixed:** opening *Model & effort* on an account whose file sets nothing blanked the
whole page. The bridge leaves a null field out, so `model` arrived missing, the form read it as a model
id and called `.trim()` on nothing; the machine log's new `page.error` (LOG1b) named it. Settings are
now read defensively where the roster is read, with a test that sends them as the wire does. The
account row's actions also ran past the card with the new button; they wrap now.
**Not seen:** the composer's controls in a live conversation (a real account); covered by the test agent.
**Proven by:** driver 1040, modules 345, vitest 1556, CLI 558, and the window.

## DEPLOY5 — the deployment rehearsal holds a chat open at close (2026-09-30)

> - [ ] **DEPLOY5 — the artefact gate holds a chat open at close.** … The deployment rehearsal closes the
>   installed shell (§6) with no chat open, and it has no way to open one … Give it one …

**Outcome** (built by a subagent in its own worktree, merged, then run on main). The deployment
rehearsal starts the installed application with the dev loop's own debug port (from 9433, never
9333), the host it starts pinned to production so a development host cannot serve a project's
bundle. Phase 6 finds the install's own page over that port, opens a conversation on the protocol
stub over the bridge, checks it `working` with its harness marked and alive, closes the shell the way
it always has, and checks the harness and its marker went with it. Phase 7 starts the install's host
alone, which runs no orphan sweep, and passes only on the close's own note: the sweep's note is the
same `stopped`, which is what the window saw on 2026-09-25. The family rehearsal's protocol stub moved
word for word into the rehearsal kit, so both gates run one copy.
**Proven by:** the deployment rehearsal, 67/67 on main (58 before); CLI tests for its helpers.

## HTTP1 — the HTTP host under test (2026-09-30)

> - [ ] **HTTP1 — the HTTP host under test** (service F19). … The fix is a `WebApplicationFactory` suite
>   over shared mode's doors.

**Outcome** (built by a subagent in its own worktree, merged, then run on main). `Daoris.Service.Http.Tests`
starts the host's real `Program` in-process through `WebApplicationFactory`, over a scratch home, index,
repositories and web root per class; no port is bound, and the host needed no change. Local mode: a
root is answered to loopback only; a quest nobody took is deleted, a taken one refused with 409 and the
service's sentence word for word. Shared mode: the host's own route table is enumerated, every route
refused without a key and answered with a valid one, expired and revoked keys named by prefix and never
repeated, no page served, no machine path in any answer, no delete routes; a local host asked to bind
beyond loopback does not start. A 500 is one `request.failed` line by route pattern. A dogfood test now
holds that every .NET test project is run by a declared gate. On main, its LAN example became a
documentation address: the sensitive scan refuses a private network's.
**Proven by:** 30 tests, each seen failing against a deliberately broken host; the service gate 632.

## HELP6 — Ask Daoris reaches everything (2026-09-30)

> - [ ] **HELP6 — Ask Daoris reaches everything** (owner, 2026-09-30: *"we need to have a good ui/ux or
>   easy access for everything use ask daoris"*). … updating or pinning an agent (USE1a), deleting a
>   quest or an ask made by mistake (QUEST1), an account's model and effort (AGT6), opening the setup
>   guide at a step (SETUP1), and taking the person to any screen (*go*) …

**Outcome** (built by a subagent in its own worktree, merged, then tried with a real helper on main).
Four more connector tools, `agent_propose`, `delete_propose`, `agent_settings_propose` and `go_propose`,
write the same proposal file as HELP1c, and the driver judges each by the rules of the screen that makes
the same change: an update needs the roster's `updates` and a pin one exact release; a delete needs the
service's own `deletable`, so a taken, done or declined quest never reaches a card; an account's
settings need a tool whose settings Daoris knows, and `max` is refused; a *go* must be a place the
page knows (`HelpPlaces`, twin of `help/places.ts`). Apply goes through each screen's own code (the
harness action's start and the settings write are now one method each, shared by route and door; a
delete is the drawer's `DELETE` route). The room names each kind's rule, the asks by id and every place.

**Tried on the scratch window with the real helper:** *"take me to step 2 of the setup guide"* → the
helper called `go_propose` at once (no tool search, HELP5's switch confirmed with a real agent), the
turn took 10 s, and *go there* opened Settings → Get started at step 2 with the application recorded
in the conversation. **Nit:** its words said *press Apply* where the card's button reads *go there*.
**Proven by:** service 619, driver 1090, modules 351, vitest 1569, and the window.

## LOG1c, LOG1d — the machine log read back, and a report to improve Daoris from (2026-09-30, D94)

> - [ ] **LOG1** … (c) **Two doors to read it** (D50): `daoris-driver logs` at a terminal, and a Settings
>   domain that shows the recent lines and opens the folder. (d) **A report for improving Daoris**:
>   `tools/usage-report.mjs --install <dir>` summarises a period …

**Outcome** (built by a subagent in its own worktree, merged and rehearsed on main). **1c:** one reader,
`MachineLogReader` in the driver library, merges every source's files by time with the same filters
at both doors (a span, a source, an event, a level as a floor); a line it cannot read is skipped and
counted. `daoris-driver logs` prints one readable line an event, or the raw lines with `--json`;
Settings → Logs (desktop only) shows the newest lines over `DAORIS.LOG` · `LINES`, capped, with a count
per level, and *Open the folder* opens the home's `logs/` through the kit's shell launcher. **1d:**
`tools/usage-report.mjs --home <dir> | --install <dir> [--days 7] [--json]` summarises the lifecycle,
what was used most, conversations and sessions (open and first-answer times, turns by ending),
refusals and failures; a time the log could not know is never counted as zero. Run on a copy of the
owner's log, it found the HTTP host never writes `app.stopped` and the browser leaves a task's
exception unobserved (LOG2).

**On main:** Ask Daoris's places (HELP6, merged just before) lacked the new *Logs* domain, and the page's
test that every Settings domain is a place caught it; both twins gained it.
**Proven by:** driver, modules 361, vitest 1588, CLI 581, family 301/301, deployment 67/67, Playwright 21.

## BRW7, BRW8 — the browser's door and link routing, and who is driving it (2026-09-30)

> - [ ] **BRW7 — start it from the app, and route links to it.** … - [ ] **BRW8 — who is driving.** …

**Outcome** (built by a subagent in its own worktree, merged, then looked at on main). **BRW7:** Daoris's
browser has a door on the app strip, a compass before the region toggles, on a shell only; not on the
activity bar, whose items are places in this window (D66; the in-app browser design §3c has the
reasoning). `links` in the browser's settings (`system` by default, or `daoris`) has two doors, `daoris
browser links` and Settings → Browser; every link on the page opens through one `ExternalLink`, held by
a test against hand-written anchors, and where a shell is here and `links` is `daoris` a click opens
the page as a tab in the chosen browser. A sign-in link always opens in the system's browser. **BRW8:**
who is driving is read from what the driver handed (a `${browser}` server), kept beside each session's
process for driven quests, intakes and conversations, and answered as `drivingBrowser`; one session is a
chip beside the door that opens it, several a count with a menu, and Settings → Browser names them.
**Looked at:** the compass on the strip and the Browser domain's new rows, no overflow; not yet a live
driving chip or a routed click (both need a session with the browser plugin).
**Proven by:** driver 1140, modules 376, vitest 1625, CLI 584.

## HELP7 — the room names each card's own button (2026-09-30)

> - [ ] **HELP7 — the room names each card's own button.** Tried with the real helper (HELP6): asked to go
>   to a setup step, it said *press Apply* while the card's button reads *go there*. …

**Outcome.** The room says each proposal reaches the person as a card with two buttons, named as the
card names them: every card but a go reads *apply* and *not now*, a go card *go there* and *not now*,
each with its 中文 label beside it, so the helper points at the button the person sees.
**Proven by:** `HelpRoomTests` (the phrases pinned, each on one line), 13 room tests.

## LOG2 — what the first real log showed (2026-09-30)

> - [ ] **LOG2 — what the first real log showed** (2026-09-30, read by LOG1d's report on a copy of the
>   owner's log). (a) **The HTTP host never writes `app.stopped`** … (b) **The browser leaves a task's
>   exception unobserved** when a WebSocket to its debug port closes without a handshake …

**Outcome** (built by a subagent in its own worktree, merged, then rehearsed on main). **(a)** The host
never wrote `app.stopped` because `HostSupervisor.Stop` killed the host the shell started. The
supervisor now starts it with standard input redirected and `DAORIS_STOP_ON_INPUT_END=1` (a twin, in
`twins.md`), stops it by closing that input, waits up to 5 s and kills only a host still running then
(`HostStop`). The host's `InputEndStop`, active only when asked, reads the input to its end and calls
`StopApplication`, so it writes `app.stopped` and exits 0; a terminal's host never opens its input, an
adopted host is left running, and there is no stop route. A force-killed shell's host now stops too
(observed by hand, not gated). **(b)** The log's `error` came from the pre-CHR8 relay, which CHR8 had
already removed. The browser's two remaining let-go tasks now go through `MachineLog.Observe`, which
writes a failure as an `error` line naming its place and never faults; the first window moved to the
modules as `EngineCdp.FirstWindowAsync`. The kit's own relay (Shenora.Chromium 0.18.0) has the same
unobserved `WhenAny`, which is a request for the kit's owner.
**Proven by:** service 619 + HTTP host 45, modules 384, driver 1144 (the ProcessJob real-tick test once
under a parallel build, green alone thrice), CLI 584, deployment 67/67.

## WSR4 — a plugin lands work: push and open a pull request (2026-09-30)

> - [ ] **WSR4 — a plugin lands work: push and open a pull request** (D87). The landing rule names a
>   plugin, and the plugin, for its platform, pushes the branch and opens the pull request over its
>   wire (D64). Off by default, and configurable per workspace. …

**Outcome** (built by a subagent in its own worktree, merged, then rehearsed on main): **D100**, amending
D87. A branch landing rule can name an installed plugin (`--plugin <id>`, or the *who pushes it* chooser
in Settings → Workspace → How work lands). Both doors refuse a plugin that is missing, off, refused or
does not speak on `work/land`, and the review's plan and the press refuse it again before anything is
made. After the branch is made, and only then, the landing starts the plugin for one `hook/work/land`
frame (repository, root, branch, base, title, quest, session, commits) and reads back `{pushed,
pullRequest, message}`: said in the review's Accept result with the pull request as a link, and by
`trees land`, and kept as a note in the conversation's record. A plugin that fails leaves the branch
standing and says how to push by hand; Daoris runs no push itself. Two inert examples are tracked,
`github-pull-request` (gh) and `azure-devops-pull-request` (az repos), tested against a bare repository
and fake CLIs, never a network, and never run against a real platform. Ask Daoris cannot yet propose a
rule naming a plugin (HELP8).
**Proven by:** driver 1174, modules 385, CLI 592, vitest 1631, Playwright 21, family 301/301,
deployment 67/67.

## HELP8 — Ask Daoris proposes a landing rule that names a plugin (2026-09-30)

> - [ ] **HELP8 — Ask Daoris proposes a landing rule that names a plugin** (WSR4 left it, D100). Its
>   parser takes `branch <pattern>` and `--tidy` and reads the rest as the pattern. …

**Outcome.** The proposal's landing value is read as `daoris driver landing` reads it: the form, a
branch's pattern, and `--tidy` and `--plugin <id>` in either order. A plugin that is not installed, is
switched off, is refused or lands no work is refused in the landing route's own sentence
(`LandingRules.PluginProblem`, against the catalogue the route reads), as is a merge naming one and a
`--plugin` with no id; the card says the plugin pushes it and opens the pull request. The room names the
plugins that can land work here, from the same check, or says none is installed and that installing
one is the person's; the service twin's hint and the MCP tool's description name `--plugin <id>`.
**Proven by:** `HelpProposalsTests` (both orders, six refusals), `HelpRoomTests` (the list and its
source), the service twin's shape test; driver 1184, modules 385, service 620 + 45, CLI 592, family
301/301.

## PLUG9 (a) and (b) — Ask Daoris makes a plugin by asking, and installs one by a press (2026-09-30)

> - [ ] **PLUG9 — Ask Daoris makes one by asking for it, and installs it by a press.** (a) The room says a
>   plugin is made as an ask at the workspace holding the plugins repository … (b) A new proposal kind,
>   `plugin` … *(c) and (d) stay open in the backlog.*

**Outcome** (built by a subagent in its own worktree, merged, then rehearsed on main). The room says a
plugin is made as an ask at the workspace of the repository that holds plugins, naming the point it
speaks on from `HookPoints`, and made there with its tests; with no such repository, where plugins live
is the person's call. The helper never writes a plugin and never proposes adding one that has not
landed. A seventh kind, `plugin` (`plugin_propose`), adds a landed plugin from its folder in a
registered checkout, or switches one installed here. The driver judges it with the catalogue's own
reader and refuses an unsound manifest, a harness this build carries, a folder in or around the home or
outside its checkout, and an id already installed. The card shows the id, the command with `${plugin}`
as written, and the points, agents and servers before Apply. Apply is `PluginInstall.Add`, the driver's
twin of `plugin add` that adds and never replaces, or `PLUGIN_ACTION`'s own switch; neither starts
anything. Whether the folder's content has landed on the repository's line is not checked: the
checkout is copied as it stands, and the card says so.
**Proven by:** service 632 + 45, driver 1221, modules 387, vitest 1635, CLI 592, Playwright 21, family
301/301 (a first run exited 127 before its transcript while three worktrees built; the rerun alone
passed), deployment 67/67.

## PLUG8 — the kit a session makes a plugin with (2026-09-30)

> - [ ] **PLUG8 — the kit a session makes a plugin with.** (a) `daoris plugin new <id> --point <p>…
>   [--harness]` … (b) `daoris plugin try <folder> --point <p> [--frame <file>]` … (c) The authoring
>   knowledge a session reads …

**Outcome** (built by a subagent in its own worktree, merged, then looked at and rehearsed on main):
**D101**. `daoris-driver plugins new` and Settings → Plugins → Make a plugin write a plugin's folder: a
manifest, a wire script already answering each point, a README with the wire and its rules, and a wire
test needing only Node. So `node --test` is a plugins repository's whole gate with no Daoris on its
PATH; the published install carries neither CLI, and a driven session gets none. `plugins try` and the
Try buttons start a plugin with the driver's own start, reader and frames (`HookFrames`, now shared by
the loop, the landing and the kit), and report each check in its own sentence with exit 0/1/2. One
22-row table holds `try` and the wire test to the same verdicts. The sample frames name no repository:
their root is an empty folder with no git behind it, proven inside a real repository. The CLI's `plugin
new|try` points at the kit; no spawn was added to the CLI. `--harness` and servers were left out,
because they are declarations rather than code. A non-frame JSON line no longer ends a plugin's wire.
**Found on the window and fixed in the merge:** the hook wire read and wrote a plugin's streams in the
console's code page, so on this machine every non-ASCII word a plugin said came back garbled (FIX-LOG);
every redirected stream in the desktop now names UTF-8, held by a source scan. Also the kit card's
body showed raw `**` (a catalogue test now refuses markdown bold), and its folder field shrank to two
characters with the side bar open. Try's scratch folder is under the system's temporary folder,
removed within the call; D101 records it.
**Proven by:** driver 1294, modules 388, service 632 + 45, vitest 1653, CLI 593, family 301/301, Playwright 21,
deployment 67/67.


## USE1 (f) and (g) — the last of what the owner met on the installed window (2026-09-30)

> - [ ] **USE1 — what the owner met on the installed window, 2026-09-30** (*"since I tried to use update
> but got error message no updater"*; *"委托 screen box is overflowing the window, also completed quest
> not been cleared, also new request is not auto firing"*). (a) **Update on a door with no updater**: …

**Outcome.** (a)–(f) were archived as they landed. (g) is answered: the first driven session on a normal
start with `claude-code-acp` 0.84.0 (the ticket's follow-up, 05:47 UTC) had a POSIX `PATH` in Claude
Code's shell snapshot and met no *command not found*. The session that lacked `git`, `tr` and `head`
ran under the dev tool's `run --install` started from Git Bash: its snapshot held `PATH` in Windows form
(`C:…;C:…`), which bash could not read. So the cause is that start's environment, not the adapter's
release. The handover already says to start the install the normal way; it now says why.
**Proven by:** the two snapshots, side by side, and the session's own record.


## WSR5 — what a landed branch needs after its pull request (2026-09-30)

> - [ ] **WSR5 — what a landed branch needs after its pull request** (owner, 2026-09-30: *"we still
> have so many branch need to clean up"*; *"if you want to open pr is the azure plugin ready"*). …

**Outcome** (built by a subagent in its own worktree, merged, then rehearsed on main): **D102**, amending
D88 and D100. **(a)** A landing now records the branch it makes in `<home>/landings.json`.
Settings → Workspace → Session branches and `daoris-driver trees clean` list those branches in their own
group, and the same press removes each one whose work reached the line. The proof is by content: every
file the branch changed since leaving the line reads the same on the line or on `origin/<line>`,
deletions and both paths of a rename included, compared as blobs; a branch inside another that passes
goes too. Checked-out branches, pushed-then-moved ones, ones a kept session branch still needs, and
anything uncertain are kept and named. Only recorded branches are judged, so landings from before the
record, and people's own branches, are never touched: the owner's two AR-2202 branches still go by hand,
once. **(b)** A recorded branch can be handed to a landing plugin after its landing: `daoris-driver trees
hand`, the review's *hand it to <plugin>*, or an Ask Daoris `hand` card. The plugin gets D100's frame for
the branch as it stands; a hand-off whose plugin does not push changes nothing. Not yet seen on the window,
and no platform's squash merge or push was exercised (a local squash and a bare origin stand in).
**Proven by:** driver 1331, modules 390, service 639 + 45, vitest 1662, CLI 593, family 301/301,
Playwright 21, deployment 67/67 (a first run failed publishing: a long-lived MSBuild server carried a
broken environment, and `dotnet build-server shutdown` cleared it).


## PLUG9 (c) and (d) — a plugin remembers where it came from, and the install offers Daoris's own (2026-09-30)

> - [ ] **PLUG9 — Ask Daoris makes one by asking for it, and installs it by a press.** … (c) Installing
>   from a repository's folder remembers the source, so the plugin can be updated when its repository
>   lands a change. (d) The install carries Daoris's own example plugins as offers …

**Outcome** (built by a subagent in its own worktree, merged, then rehearsed on main): **D103** (it took
D102 in its worktree, which WSR5 had landed with; renumbered on merge). **(c)** Every add records where
the plugin came from in `.daoris-source.json` inside its install folder (the folder, or the install's
offer), written into the staged copy before the swap, so it moves and goes with the install. A plugin added
earlier or copied in by hand says it has no record, and is never given a guessed source. `daoris plugin
update <id> [--yes]`, Settings → Plugins' Update… / Update now and Ask Daoris's `update` card re-read the
source with the catalogue's own reader, refuse in one order and wording on both twins, show what changes
before the press, and swap the folder whole with `.data` untouched, stopping the hook first. A file, not a
`plugins.json` row, because every writer of that file, older builds included, drops a field it does not
know. **(d)** The publish lays `github-pull-request`, `azure-devops-pull-request` and `in-app-browser` out
in the install's `app/plugin-offers/`, never under `data/plugins/`; Settings → Plugins lists the uninstalled
ones with their README's *What it needs*, and Install copies one in with the offer recorded. `plugin list`
shows them, and Ask Daoris names them by id. Installing runs nothing; no offer has run against a real
platform. The merge with WSR5 took eleven conflicts, all in the files the parallel-development design names.
**Proven by:** driver 1376, modules 393, service 646 + 45, vitest 1683, CLI 613, family 301/301, Playwright
21, deployment 70/70 (three new checks: the offers carried, none installed, the deployed driver finds them).
