# Daoris (道衍) — Active Task Backlog

> **This file holds OPEN tasks only.** A finished task is **removed from here and appended to
> [`docs/task-archive.md`](docs/task-archive.md)** with its date and outcome — never ticked in place.
> `CHANGELOG.md` is the release-facing log; the archive is the per-task record. The contract is
> `docs/2026-08-04-daoris-design.md`; the forward sequence is [`ROADMAP.md`](ROADMAP.md).

**Goal:** one canonical set of agent-facing rules and knowledge, materialized into every repository in
the family, kept from drifting, and improved from wherever the improvement was found — and, as of D45,
**Daoris as the driver**: the centralized workflow manager that triggers and coordinates the agent
sessions doing the family's work. The design is settled (D46, `docs/2026-09-19-driver-design.md`);
the next session starts at **DRV2**.

## State

All four artefacts exist; only `Daoris.Desktop` is a brief. Nine commands, 121 CLI tests, 83 service,
57 devkit, 52/52 release rehearsal (eight runs 2026-09-18), **29/29 family rehearsal** (2026-09-19),
9 devkit gates. Canon: 8 core rules, 5 knowledge documents, 5 skills, 6 packs. Always-loaded core is
**23,988 of 24,000 bytes** — 12 bytes of headroom, so the next canon addition fails the gate even as
an index row, and the answer is splitting, not raising (D28).

The service is **deployable** (D36) and **ships as executables** (D43): `npm run publish:service --
--install` lands both hosts self-contained in `~/.daoris/bin` and prints the ready `.mcp.json`
snippet; the release workflow ships them per platform with sha256s beside the devkit. Local sessions
spawn the MCP host over one persistent store; the HTTP host carries registrations and quests for a
remote deployment, key-gated, **no model required**. **The loops create their consumer** (D44): the
family rehearsal (29 checks) and the Playwright suite (6 tests) each run over a scratch copy of the
examples and take a project born mid-run through init → declare → sync → check → connect → its first
quest. `Daoris.Web` is **the platform** (D38,
D40, D41, D42): five views landing on **Overview** — is anything sitting, the family's health, quests
grouped by state with sitting time, projects as scannable declarations — in a designed console shell
(sidebar, drawers, toasts, a validated status palette; `docs/2026-09-19-platform-ux.md`), built on
headless libraries (Tailwind v4 on the tokens, Radix, TanStack Query; `docs/2026-09-19-frontend-architecture.md`),
speaking **en + 简体中文** with a parity gate, with Storybook as the design tool and a test pyramid
declared in `daoris.gates.json` — a **16-test Vitest inner loop** and a **5/5 Playwright outer loop
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

## Backlog

- [ ] **DRV2 — build the local driver: `Daoris.Desktop`, re-scoped (in progress).** The contract is
  `docs/2026-09-19-driver-design.md` (D46); read it and D45/D37 first. **Landed 2026-09-19:** the
  service's session surface (records + `SessionLedger`, `GET /api/sessions`, the machine-local root
  on the registration via `connect`, loopback-guarded) and the driver itself —
  `src/Daoris.Desktop/Daoris.Desktop.Driver` (pure planner, observation mapping, adapter seam, stub
  adapter) with its headless host `daoris-driver`, gate-verified by the family rehearsal's driver
  phase (43/43): quest → session → commit → done, dirty-tree hold, decline with reason, outside work
  untouched, records surviving restart. The **`claude-code` adapter** is in (design §5): headless
  mode, target as the prompt, edits auto-accepted and everything else under the repository's own
  checked-in permissions, no model ever named; it is the config default, and a real driven run is how
  it gets verified (a deployment choice on a proven loop, §8). The **platform shows the session
  surface, read-only** (design §6): a live session's state on its quest's card, the full record —
  state, adapter, note, evidence verbatim — in the drawer, en+zh, covered in both loops (18 Vitest,
  7/7 Playwright including a driven-record scenario). **The shell exists** (`Daoris.Desktop.App`,
  `daoris-desktop`, on released Shenora.Windows 0.16.0): brings up the local HTTP host (adopt or own;
  a dev build runs from its project so the bundle serves), carries the platform in its WebView, runs
  the driver loop in-process (`driver.json` re-read every tick — a control that needs a bounce is a
  control nobody trusts), and shuts down whole: loop stopped, in-flight sessions ended and recorded
  `stopped`, owned host killed — smoke-verified end to end 2026-09-19. **Remaining:** the person's
  session controls in the platform over the IPC bridge (drivable, hold, stop, start-now — a
  `DAORIS.DRIVER` module + `@shenora/react` detection, controls rendered only where a shell answers),
  and the page-side rendering of `DRIVER_TICK` notifications as toasts.

- [ ] **DRV3 — the remote server, for teams.** Multi-user sharing of knowledge and quests across
  machines, **fed via the local desktop app** (local-first; the remote is fed, not authored — D21's
  "shared may be a sync" finally lands). Folds in the old SVC2 hardening: per-person expiring keys and
  OIDC per the service design §5, and whatever relay the local↔remote sync needs. Later in the roadmap,
  by the owner's sequencing.

- [ ] **REH1 — the release rehearsal intermittently reports 45/52.** Seen twice, **always exactly 7
  failures** — precisely the canon-upgrade phase's 7 checks, so a whole phase fails on a broken
  precondition rather than a flaky assertion. Both times it ran straight after canon files were edited
  and synced. **Every run now writes a transcript to `_fixtures/rehearsal-logs/` by construction**
  (2026-09-18), so the next failure is captured without anyone remembering to. Eight runs that day —
  several straight after canon edits and syncs, the suspected trigger — all passed 52/52. Stays
  open until a captured failure explains it. Do not tag a release while this is open.

- [ ] **LYN1 — the Lyntai pin, and the one-sweep migration.** `Daoris.Service` deliberately pins the
  cognition sibling at released 2.1.0 (D22): upstream has a renaming major sitting unreleased (its
  `Llm*` call types become `Text*`, and `Providers.Default` is already retired from its 3.x package
  roster), so the coordinated move is **one** migration when that major ships, not two. Lyntai is not
  quest-addressable (de-adopted), and no repository is asked to adopt until Daoris is finished — so
  this is held here, not delivered anywhere.

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
