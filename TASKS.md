# Daoris (道衍) — Active Task Backlog

> **This file holds OPEN tasks only.** A finished task is **removed from here and appended to
> [`docs/task-archive.md`](docs/task-archive.md)** with its date and outcome — never ticked in place.
> `CHANGELOG.md` is the release-facing log; the archive is the per-task record. The contract is
> `docs/2026-08-04-daoris-design.md`; the forward sequence is [`ROADMAP.md`](ROADMAP.md).

**Goal:** one canonical set of agent-facing rules and knowledge, materialized into every repository in
the family, kept from drifting, and improved from wherever the improvement was found — and, as of D45,
**Daoris as the driver**: the centralized workflow manager that triggers and coordinates the agent
sessions doing the family's work. Parts 1 and 2 of D45 are **built and proven by a real driven run**
(D46, `docs/2026-09-19-driver-design.md`; DRV4 in the archive). The owner continued the sequence
2026-09-20: part 3 is now **designed (DRV3 → D47, `docs/2026-09-20-remote-design.md`) and built
(DRV5, six landings, in the archive)** — the remote server exists, gate-proven by the family
rehearsal's two-machine phase. **All three parts of D45 are built.** The owner set the next direction
2026-09-20: **workspaces as the unit of sharing, and Daoris as the working surface** — designed the
same day as D48/D49 (each amended the same day on the owner's corrections: membership is git-style
wiring, never tracked; coexistence with non-users is binding; harness accounts are named credential
profiles) with two contracts and eight build items. **All four WSP items are built** (2026-09-20, in
the archive): the workspace exists, the registry is the authority, the machine's remotes are a map —
one deployment per circle — and a feed carries the commit it speaks for, so the newest canonical view
is the one that stands. **All three SES items have landed too**: a session's console streams live, a
person can hold a conversation with an agent in any repository, and Daoris manages the harnesses those
sessions run on — installing and updating them through their own mechanisms, and holding many accounts
per harness as named credential profiles without ever touching a credential. **D49 is complete; one
item of the arc remains — canon coexistence (CANON6).**

## State

**All five artefacts exist and are built, and all three parts of D45 with them.** Fourteen commands,
192 CLI tests, 248 service, 57 devkit, 130 driver, 53/53 release rehearsal (52/52 across eight runs
2026-09-18, plus a no-staged-leftovers check since REV1), **151/151 family rehearsal** including the
driver, two-machine remote, never-scans, two-workspace, registration-lifecycle, remotes-map,
which-commit-speaks, conversation and toolchain phases (2026-09-20), 9 devkit gates. Canon: 8 core rules,
5 knowledge documents, 5 skills, 6 packs. Always-loaded core is
**23,988 of 24,000 bytes** — 12 bytes of headroom, so the next canon addition fails the gate even as
an index row, and the answer is splitting, not raising (D28).

**The remote exists** (D47/DRV5, built 2026-09-20): the same HTTP host in shared mode gates every
route with per-person per-machine minted keys (`keys mint|list|revoke`, hashed with an audit prefix,
expiring), refuses to bind beyond loopback in local mode, and never serves a machine path or a page;
the quest lock is code (an atomic guarded `Taken` transition, closed quests immovable) in the shared
judgement class, so local mode is hardened too; a quest homes at the remote when its receiver is
joined and verbs write through synchronously or fail plainly; the desktop's sync loop rides the driver
tick — feeding stripped registrations, session records keyed by origin + id, and opted-in knowledge
content (never vectors) up, mirroring the family's quests and foreign registrations down. What may
leave a machine is two manifest declarations (join; share knowledge), silence meaning local. Proven
by the rehearsal's remote phase: a quest crossed two machines and drove to done, a raced take left the
losing driver observing the lock, and the remote store was scanned to hold nothing machine-local.

**The driver drives** (D45/D46, built 2026-09-19): `Daoris.Desktop.Driver` (pure planner, observed
lifecycle, adapter seam — stub + `claude-code`), the headless `daoris-driver`, and the shell
`daoris-desktop` on released Shenora.Windows 0.16.0 — it brings up the local HTTP host, carries the
platform, runs the loop in-process, and lands the person's controls (drivable/hold per repository,
stop a running session) through the `DAORIS.DRIVER` IPC module, live-updating over `DRIVER_TICK`.
The quest state machine is the only lock; outside sessions stay first-class. **The loop is proven
with a real session** (DRV4, 2026-09-19): a scratch-born project, a real quest, a real `claude-code`
session spawned by the driver — it claimed its quest over its own connector under headless
`acceptEdits`, committed the work, closed the quest `done` with its note, and the driver's record
carries the commit as evidence, 71 seconds end to end.

The service is **deployable** (D36) and **ships as executables** (D43): `npm run publish:service --
--install` lands both hosts self-contained in `~/.daoris/bin` and prints the ready `.mcp.json`
snippet; the release workflow ships them per platform with sha256s beside the devkit. Local sessions
spawn the MCP host over one persistent store; the HTTP host carries registrations and quests for a
remote deployment, key-gated, **no model required**. **The loops create their consumer** (D44): the
family rehearsal (43 checks then; 75 today) and the Playwright suite (7 tests) each run over a scratch
copy of the examples and take a project born mid-run through init → declare → sync → check → connect →
its first quest. `Daoris.Web` is **the platform** (D38,
D40, D41, D42): five views landing on **Overview** — is anything sitting, the family's health, quests
grouped by state with sitting time, projects as scannable declarations — in a designed console shell
(sidebar, drawers, toasts, a validated status palette; `docs/2026-09-19-platform-ux.md`), built on
headless libraries (Tailwind v4 on the tokens, Radix, TanStack Query; `docs/2026-09-19-frontend-architecture.md`),
speaking **en + 简体中文** with a parity gate, with Storybook as the design tool and a test pyramid
declared in `daoris.gates.json` — a **27-test Vitest inner loop** and a **7/7 Playwright outer loop
over `examples/`**. Doctrine stays unwritable from every view. The **example family** under `examples/` is the router's proof and the setup story (D39) — a
canon change must re-sync it in the same commit, and `npm run rehearse:family` enforces that.
Development is **automation-first** (D37): the person sets the target and verifies the final diff;
agents execute and gates verify the middle. `docs/FIX-LOG.md` carries fix root causes, indexed by the
service like every sibling's.

Nothing is published; development runs at `0.0.x`. **Adoption by other repositories is the owner's call
and happens when Daoris is ready** — it is not tracked here, and no repository is asked to adopt until
Daoris is finished. Lyntai stepped off the tool 2026-08-17 (owner-requested; the synced files remain
there as local forks), so the live consumer count is zero and Lyntai is not quest-addressable until it
re-adopts. The Shenora rehearsal keeps and does not expire: 6 collisions, 2 twins to retire, local
mechanics drafted at `docs/adoption/shenora-repo-mechanics.md`, budget 40,000, `check` clean at
38,782 bytes, with `web-webview` and `durable-jobs` ready for it.

## Handover — where a fresh session picks up

**The arc is nearly done: workspaces and the interactive surface (D48/D49, 2026-09-20, each amended
the same day on the owner's corrections).** The owner set the direction in this backlog and granted
structural redesign (nothing is deployed); the design session turned it into two contracts and eight
build items, of which **seven landed 2026-09-20** — all four WSP items and all three SES items.
**The workspace arc is complete**: a
registry row carries the workspace, every cross-repository answer is scoped by it, the family is an
explicit list that `connect`, `retire` and `import` maintain, each circle syncs with the one deployment
its own entry names, and a feed carries the commit it speaks for so the newest canonical view is the
one that stands. **D49 is complete too**: the console streams, a conversation is a session, and the
harnesses those sessions run on are Daoris's to find, install, update and hold many accounts for —
without ever touching a credential. **Start at CANON6**, the one item left in the arc (coexistence,
independent of everything and constrained by the byte budget).

- **Read first:** `docs/2026-09-20-workspace-design.md` and `docs/2026-09-20-interactive-design.md`
  (the two contracts — every WSP/SES item cites its sections); `docs/DECISIONS.md` **D48–D50** (the
  direction, the argued rejections, and management parity — D50 binds every item), with D45–D47
  behind them for the driver and remote these extend; the REV1 entry in `docs/task-archive.md` and `docs/FIX-LOG.md`'s top entries (the
  key-console scan, the mirror-down feed-back and the shared-scan fixes — WSP4 builds directly on
  those lessons).
- **CANON6 specifically** (small, and constrained): read workspace design **§2a**. It is an audit of
  the 8 core rules for instructions only Daoris can perform — the known case is
  `repository-owns-its-work`'s "publish a quest" — each gaining the tool-absent path in the same
  breath, plus the principle in `.claude/knowledge/canon-authoring.md`. **The budget is the whole
  difficulty**: the always-loaded core is 12 bytes from its limit, so a carve-out that does not fit is
  a D28 split of principle from detail, never a raised limit. A canon change must re-sync `examples/`
  in the same commit, and `npm run rehearse:family` enforces that.
- **The build order is stated at the top of the backlog** — every WSP and SES item is in, so CANON6
  is the only one left in the arc. Schema changes rebuild rather
  than migrate (the store's own rule, and nothing is deployed); WSP1 bumped the entry store to schema
  2 on exactly that basis, while the registration store adds columns, because a registration that
  vanished on an upgrade is the failure that store exists to prevent — and the session store does the
  same, for the same reason (SES3 added two).
- **The last seven landings are local commits, unpushed by the owner's standing call** — `b70ac0e`
  (WSP1), `7333f74` (WSP2), `538bc03` (WSP3), `03ed5d9` (WSP4), `15ba288` (SES1), `a965feb`
  (SES2) and `49da224` (SES3). All gate-green; `git log` is the reviewable record.
- **What the arc left for its successors, deliberately:** the platform's workspace *switcher*
  (Projects shows each repository's circle, its fed commit and manages it, and the Machine view shows
  the wiring, but no view filters by one yet); and a **`codex` session adapter** — SES3 made codex
  manageable as a TOOL (`daoris harness` knows its installer, its home variable and how it reports a
  login) without giving the driver an adapter that spawns it, because a permission posture and an
  `exec` shape are things D23 says are added deliberately, on proof, never guessed.
- **Every capability has TWO doors, and anything new inherits both.** The desktop's IPC and
  `daoris-driver chat` run the same `ChatRunner`; `daoris harness` and `daoris driver` do from a
  terminal what the roster and the checkboxes do from a screen. What is shell-only is the STREAM, not
  the capability (D47 §4 protects transcript-class material, and D50 forbids stranding a capability on
  a screenless machine).
- **The toolchain's rules, in one place** (SES3, interactive design §4): silence means the harness's
  own configuration home, so the feature is purely additive; login state is asked of the harness, has
  three values, and only a definite *out* refuses; a cached refusal is re-asked before it is given; the
  profile NAME never leaves the machine while the harness VERSION does; a profile IS a directory, and
  removing one deletes nothing. The CLI's *managed* set is deliberately not the driver's *adapter* set.
- **Two endings that mean different things:** end of input lets the harness wind up (`completed`);
  `stop` is the person's interrupt (`stopped`). Anything that grows a third way out should say which
  of those it is.
- **Four things that will bite if forgotten:** being in a folder is no longer being a member (a
  repository joins by `connect` and leaves by `retire`); the bootstrap import runs **once** per store,
  and anything that re-ran it would resurrect every repository someone retired; **a feed never names
  its own workspace** — the receiving deployment's wiring decides where material lands; and **a
  checkout with no git history feeds no knowledge**, because a wholesale replacement the receiver
  cannot order against what it holds is not safe (the driver says so itself rather than sending it).
- **The remotes map has three twins, not two** (`RemoteConfig`, `RemoteTarget`, `remotemap.ts`): the
  three artefacts share no code, so the FILE is the contract and the three test tables move together.
  The environment pair replaces the file for the **whole machine**, which is what the rehearsal's
  hermetic guard rests on.
- **A refusal can be INFORMATION** (D48 §6) — the first of its kind here. The flag rides the wire
  (`{"error": …, "information": true}`); a client that classified by matching the sentence would turn
  every rewording into a silent behaviour change. Anything that grows a new "not taken, and that is
  fine" answer belongs in that class rather than in the failure one.
- **A release stays blocked on REH1** (below), and the new arc moves the target anyway — tagging waits
  until the owner calls it, as ever. **Nothing is pushed or published**; adoption stays the owner's
  call, and the live consumer count is zero.
- **The held items (REH1, CANON5, CANON2, HARNESS1) still wait on their external triggers** — pick one
  up only when its trigger has actually arrived.
- **Verify before claiming done, always:** `npm run verify` (CLI 192 + `check` + version agreement),
  `dotnet test src/Daoris.Service/Daoris.Service.Tests` (248),
  `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests`
  (130), `npm run rehearse:family` (151/151), `npm run test:web` (50 + 7). If a `bin`-driven gate is red
  while `npm test` is green, suspect a stale gitignored `dist/` first (FIX-LOG) — though `postpack
  --clean` and the rehearsal's leftover check now remove and assert that case away.

## Backlog

**The next arc: workspaces and the interactive surface (D48/D49, designed 2026-09-20; D50 binds every
item).** The owner's direction, designed under the standing redesign grant — the two contracts are
`docs/2026-09-20-workspace-design.md` and `docs/2026-09-20-interactive-design.md`; read them before
building anything below. **D50 — management parity — applies across the arc**: everything a person
manages gets a proper surface in the desktop or the `daoris` CLI (files and doors are the truth,
surfaces are editors; the offline-discipline test evolves to a named management class — design §2b).
Build order: **every WSP item, SES1 and SES2 are done** (2026-09-20, in the archive) — what remains
is SES3 and CANON6, independent of each other. Each item is one session-sized landing, TDD, gates
green, moved to the archive on completion.

- [ ] **CANON6 — doctrine must not hard-require Daoris (coexistence, D48).** Audit the 8 core rules
  for instructions only Daoris can perform (the known case: `repository-owns-its-work`'s "publish a
  quest"); every named mechanism gains the tool-absent path in the same breath ("…and file the request
  with that repository's owner where the quest system does not exist"); the principle lands in
  `.claude/knowledge/canon-authoring.md`. Under the byte budget's discipline — a carve-out that does
  not fit is a D28 split, not a raised limit — and a canon change re-syncs `examples/` in the same
  commit, as ever. Workspace design §2a.

- [ ] **REH1 — the release rehearsal intermittently reports 45/52.** Seen twice, **always exactly 7
  failures** — precisely the canon-upgrade phase's 7 checks, so a whole phase fails on a broken
  precondition rather than a flaky assertion. Both times it ran straight after canon files were edited
  and synced. **Every run now writes a transcript to `_fixtures/rehearsal-logs/` by construction**
  (2026-09-18), so the next failure is captured without anyone remembering to. Eight runs that day —
  several straight after canon edits and syncs, the suspected trigger — all passed 52/52, and a ninth
  ran clean 2026-09-20 after the whole DRV5 arc (no canon edits that session, which is the case that
  has always passed). Stays open until a captured failure explains it. Do not tag a release while this
  is open.

- [ ] **CANON5 — i18n en/zh parity as canon: the two-repository bar is met.** The bilingual sibling
  carries the rule and the gate; Daoris now carries the same gate (`scripts/i18n-check.mjs`, adopted
  from it deliberately — D42). Two repositories, one lesson: a missing translation "works" in English
  and is discovered by the first reader it fails. Held rather than written because the always-loaded
  core is 12 bytes from its budget — even an index row fails the gate — so canonizing waits on the
  D28-shaped split of principle from detail, or lands as pack knowledge for web repositories.

- [ ] **CANON2 — `desktop-winforms`, the last pack candidate.** One 11 KB source, one repository —
  below the two-repository bar, which is the whole reason the canon is trustworthy. Leave it local until
  a second repository needs the same thing.

- [ ] **HARNESS1 — a second harness layout.** `src/harness.ts` holds the signals and contract checks; a
  second implementation slots in beside the Claude one. Do not start until a repository actually wants
  it — the layout, always-loaded semantics and trigger mechanism all differ, and guessing produces
  doctrine nobody chose in a format nobody verified.

## How to work a task

- **TDD:** failing test → run it fail → minimal implementation → run it pass → commit.
- **Commit per task, automatically, once gates are green** (D37 as amended). Push, publish, release
  and history rewrites stay the owner's call.
- **`npm run verify` before claiming done.**
- **A canon file is project-agnostic** — the principle and the reason, never the mechanism. See
  `.claude/knowledge/canon-authoring.md`.
- **When a task completes, move it to `docs/task-archive.md`** with the date and outcome.
