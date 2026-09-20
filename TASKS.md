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
same day as D48/D49 with two contracts and seven build items in the backlog below.

## State

**All five artefacts exist and are built, and all three parts of D45 with them.** Nine commands,
135 CLI tests, 170 service, 57 devkit, 53 driver, 53/53 release rehearsal (52/52 across eight runs
2026-09-18, plus a no-staged-leftovers check since REV1), **75/75 family rehearsal** including the
driver, two-machine remote, and never-scans phases (2026-09-20), 9 devkit gates. Canon: 8 core rules,
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

**The next arc is designed and build-ready: workspaces and the interactive surface (D48/D49,
2026-09-20).** The owner set the direction in this backlog and granted structural redesign (nothing is
deployed); the design session turned it into two contracts and seven build items. **Start at WSP1.**

- **Read first:** `docs/2026-09-20-workspace-design.md` and `docs/2026-09-20-interactive-design.md`
  (the two contracts — every WSP/SES item cites its sections); `docs/DECISIONS.md` **D48/D49** (the
  direction and the argued rejections), with D45–D47 behind them for the driver and remote these
  extend; the REV1 entry in `docs/task-archive.md` and `docs/FIX-LOG.md`'s top entries (the
  mirror-down feed-back and shared-scan fixes — WSP4 builds directly on those lessons).
- **The build order is stated at the top of the backlog** — WSP1 is the foundation; do not start WSP3/
  WSP4 before it. Schema changes rebuild rather than migrate (the store's own rule, and nothing is
  deployed).
- **A release stays blocked on REH1** (below), and the new arc moves the target anyway — tagging waits
  until the owner calls it, as ever. **Nothing is pushed or published**; adoption stays the owner's
  call, and the live consumer count is zero.
- **The held items (REH1, CANON5, CANON2, HARNESS1) still wait on their external triggers** — pick one
  up only when its trigger has actually arrived.
- **Verify before claiming done, always:** `npm run verify` (CLI 135 + `check` + version agreement),
  `dotnet test src/Daoris.Service` (170), `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests`
  (53), `npm run rehearse:family` (75/75), `npm run test:web` (27 + 7). If a `bin`-driven gate is red
  while `npm test` is green, suspect a stale gitignored `dist/` first (FIX-LOG) — though `postpack
  --clean` and the rehearsal's leftover check now remove and assert that case away.

## Backlog

**The next arc: workspaces and the interactive surface (D48/D49, designed 2026-09-20).** The owner's
direction, designed under the standing redesign grant — the two contracts are
`docs/2026-09-20-workspace-design.md` and `docs/2026-09-20-interactive-design.md`; read them before
building anything below. Build order: WSP1 first (everything else stands on it), then WSP2–WSP4 in
order; SES1→SES2 can interleave after WSP1; SES3 is independent. Each item is one session-sized
landing, TDD, gates green, moved to the archive on completion.

- [ ] **WSP1 — the workspace exists.** The `workspace` manifest field end to end (normalized at read,
  absence = `default`; carried by `connect`, reported by `status`, scaffolded by `init`); every
  cross-repo entity (registration, entry, quest, session) carries its workspace; search/convergence/
  registry/quest scoping with the same-workspace clause in `QuestExchange` (refusal names both sides);
  MCP tools gain `workspace` with the ambient-repository default; the family rehearsal grows the
  two-workspace phase (a search and a quest refused across the boundary, with the sentence). Design §2/§4.

- [ ] **WSP2 — the registry becomes managed.** Registry-as-authority (explicit list: name, workspace,
  declaration, machine-local path); the folder scan becomes `import` (first run imports the old root,
  once, and says so); refresh reads registered paths and names absences; the desktop's add/update/
  remove over the loopback host (registration lifecycle only — never deletes files, doctrine
  unwritable, manifest edits land as uncommitted diffs). Design §3/§7.

- [ ] **WSP3 — remotes become a map.** `~/.daoris/remotes.json` (workspace → url/key; env pair kept
  for one workspace via `DAORIS_REMOTE_WORKSPACE`, both twins' test tables moving together); the sync
  loop runs per workspace; the shared host gains its `DAORIS_WORKSPACE` identity and refuses feeds/
  registrations naming another, plainly; the quest relay resolves its remote by the quest's workspace.
  Design §5.

- [ ] **WSP4 — knowledge sync semantics.** Feeds carry git provenance stamped by the driver (commit,
  committedAt, branch; `WorkingTree` reads HEAD); default-branch-only knowledge (records/quests travel
  from any checkout); monotonic replacement by commit time, refused plainly and reported as
  information; provenance served on `/api/repositories` and shown on Projects. Rehearsal: a stale feed
  refused, a branch feed refused, a newer feed replacing. Design §6.

- [ ] **SES1 — the console.** The capture pump tees to a bounded per-session ring buffer; the shell's
  IPC gains `TAIL_SESSION` + `SESSION_OUTPUT`; the session drawer renders the stream verbatim,
  desktop-only (transcript-class material never leaves the machine, D47 §4). Interactive design §2.

- [ ] **SES2 — chat sessions.** `Session.Kind: driven | chat`, quest optional; the adapter seam grows
  `interactive` (stub first, scripted exchange in the gate; `claude-code` supported, `codex` explicit);
  one-session-per-repository holds for chats; `SESSION_INPUT` over IPC; chats may take/publish quests
  through their own connector; the rehearsal drives a chat to `completed` and the repository-busy
  refusal. Interactive design §3.

- [ ] **SES3 — the toolchain.** Adapter `Probe` (locate + version, run at startup and on demand); the
  platform's roster (harness, version, present/absent); install/update on the person's explicit action
  via each harness's own mechanism, streaming through the console; spawn-on-missing refuses naming the
  install action; the session record gains the harness version at spawn. Interactive design §4.

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
