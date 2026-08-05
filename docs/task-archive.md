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
waiting on Daoris not having a way to ask. Quest `#ee8994` carries the entire rehearsal — 6 collisions, 2
twins, the local mechanics to preserve, the budget, `check` clean — so taking it is mechanical.

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
