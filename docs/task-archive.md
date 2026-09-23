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
> **`docs/2026-09-21-desktop-design-brief.md`** … **Take it as a direction and confirm the reading
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
