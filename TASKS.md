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
per harness as named credential profiles without ever touching a credential. And **CANON6 has landed**:
the canon instructs exactly one thing that needs a service running — publishing a quest — and it now
names the alternative in the same breath, so an adopted repository stays workable for contributors who
do not run Daoris. **The whole D48/D49/D50 arc is built.** It was then **reviewed** at the owner's
request (REV2, in the archive), which found two things no gate could: the release workflow ran one of
the four declared gates, and the shell's 1,128 lines had no tests at all — so every refusal the desktop
made was reaching people as a blank failure. Both are fixed and gated, along with four smaller things
the same review noticed. **The owner then set the next direction (2026-09-20): the desktop becomes a
user-driven working surface — code sessions the way a terminal agent CLI holds them, but across
agents, repositories and concurrent sessions, designed with real UI/UX (researched in
`docs/2026-09-20-working-surface-research.md`).** That direction is now **designed** (SURF1 →
`docs/2026-09-21-working-surface-design.md`, 2026-09-21): **D51** settles the isolation model — the
tree is the unit of exclusion and a repository may have more than one — and **D52** settles the
surface, with a build order of five items, **SURF2–SURF6**. Beside them sit three smaller leftovers —
CANON7, WSP5, HARNESS2 — of which CANON7 is a decision for the owner that CANON5 is parked behind.

## State

**All five artefacts exist and are built, and all three parts of D45 with them.** Fourteen commands,
194 CLI tests, 248 service, 57 devkit, 130 driver, 39 desktop modules, 53/53 release rehearsal (52/52 across eight runs
2026-09-18, plus a no-staged-leftovers check since REV1), **154/154 family rehearsal** including the
driver, two-machine remote, never-scans, two-workspace, registration-lifecycle, remotes-map,
which-commit-speaks, conversation and toolchain phases (2026-09-20), 9 devkit gates. Canon: 8 core rules,
5 knowledge documents, 5 skills, 6 packs. Always-loaded core is
**23,862 of 24,000 bytes** — 138 bytes of headroom after CANON6's split, still far less than any new
rule, so the next canon addition fails the gate and the answer is splitting, not raising (D28).

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
family rehearsal (43 checks then; 154 today) and the Playwright suite (9 tests) each run over a scratch
copy of the examples and take a project born mid-run through init → declare → sync → check → connect →
its first quest. `Daoris.Web` is **the platform** (D38,
D40, D41, D42): five views landing on **Overview** — is anything sitting, the family's health, quests
grouped by state with sitting time, projects as scannable declarations — in a designed console shell
(sidebar, drawers, toasts, a validated status palette; `docs/2026-09-19-platform-ux.md`), built on
headless libraries (Tailwind v4 on the tokens, Radix, TanStack Query; `docs/2026-09-19-frontend-architecture.md`),
speaking **en + 简体中文** with a parity gate, with Storybook as the design tool and a test pyramid
declared in `daoris.gates.json` — a **55-test Vitest inner loop** (the shell-attached surfaces, over a
mocked bridge) and a **9/9 Playwright outer loop over `examples/`** (the real bundle over the real
host — including, since SES3, what a browser must NEVER learn about the machine it is not running on). Doctrine stays unwritable from every view. The **example family** under `examples/` is the router's proof and the setup story (D39) — a
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

**The arc is done: workspaces and the interactive surface (D48/D49/D50, 2026-09-20, each amended
the same day on the owner's corrections).** The owner set the direction in this backlog and granted
structural redesign (nothing is deployed); the design session turned it into two contracts and eight
build items, and **all eight landed 2026-09-20**.
**The workspace arc**: a
registry row carries the workspace, every cross-repository answer is scoped by it, the family is an
explicit list that `connect`, `retire` and `import` maintain, each circle syncs with the one deployment
its own entry names, and a feed carries the commit it speaks for so the newest canonical view is the
one that stands. **The interactive arc**: the console streams, a conversation is a session, and the
harnesses those sessions run on are Daoris's to find, install, update and hold many accounts for —
without ever touching a credential. **And coexistence**: the canon no longer instructs anything a
contributor without Daoris cannot do.

**The arc is closed and reviewed (REV2), the owner set the next direction — the desktop becomes a
working surface — and SURF1 has designed it (2026-09-21).** The contract is
`docs/2026-09-21-working-surface-design.md`; the decisions are **D51** (the isolation model, settled
first because everything follows from it) and **D52** (the surface). **The next work is SURF2** — the
lock keys on the tree, with behaviour deliberately unchanged — and then SURF3–SURF6 in order. Read the
contract and both decisions before picking any of them up; each item cites its sections.

Smaller and independent of them: **CANON7** (a decision to bring the owner rather than work to do — it
takes minutes, and **CANON5 is parked behind it**), then **WSP5** (the workspace switcher, web-only) or
**HARNESS2** (a `codex` session adapter). Four **held** items sit below those; do not pick one up until
its trigger has arrived. Nothing is pushed or published, and a release is still blocked on REH1.

- **Read first for SURF work:** `docs/2026-09-21-working-surface-design.md` (the contract) with
  `docs/2026-09-20-working-surface-research.md` behind it (the field, and what Daoris already has that
  it does not), and **D51/D52**. Two sentences from it carry the most weight: the planner keeps one
  *driven* session per repository even after the lock moves (D46 §9 survives — pacing a domain and
  preventing corruption are different jobs), and **a fresh tree holds nothing git does not track**,
  which is the price the creating sentence has to state out loud.
- **Read first for anything else** — the arc the SURF work stands on:
  `docs/2026-09-20-workspace-design.md` and `docs/2026-09-20-interactive-design.md`
  (the two contracts — every WSP/SES item cites its sections); `docs/DECISIONS.md` **D48–D50** (the
  direction, the argued rejections, and management parity — D50 binds every item), with D45–D47
  behind them for the driver and remote these extend; the REV1 entry in `docs/task-archive.md` and `docs/FIX-LOG.md`'s top entries (the
  key-console scan, the mirror-down feed-back and the shared-scan fixes — WSP4 builds directly on
  those lessons).
- **The gates are a list, and it is checked.** `daoris.gates.json` declares six; a CLI test asserts the
  release workflow runs every one, because they silently disagreed for eight landings and both
  rehearsals passing is exactly what hid it. Adding a test project means adding it to **both**.
- **Adding a refusal to a desktop module is three things**, and a test holds each: a code in
  `Refusals`, an entry in **both** locale catalogues, and a throw site using it. A thrown exception's
  message reaches nobody — the host maps it to a generic code carrying only the exception type, which
  is why every module sentence was invisible until REV2 (`docs/FIX-LOG.md`). `DriverException` is the
  one exemption and it is mapped once, at the module boundary, so the driver's own wording travels.
- **The modules tests are serialized on purpose** (`Parallelism.cs`): those modules resolve every path
  from process-global environment variables, so two test classes at once trample each other. Found by
  a second class turning two passing tests red.
- **Touching the canon? Read `.claude/knowledge/canon-authoring.md` first**, and know two things
  CANON6 settled. The coexistence test is **file or service**, never family vocabulary: what `sync`
  writes is committed, so a generated index, a lock file and every vendored rule survive the tool's
  absence — only a *service* (publishing a quest) is a dead end without Daoris, and it is the only
  one. And a canon change **must re-sync `examples/` in the same commit**: `npm run rehearse:family`
  fails on an uncommitted example diff, which reads as a broken gate and is the gate working.
- **The build order is stated at the top of the backlog** — every item is in. Schema changes rebuild rather
  than migrate (the store's own rule, and nothing is deployed); WSP1 bumped the entry store to schema
  2 on exactly that basis, while the registration store adds columns, because a registration that
  vanished on an upgrade is the failure that store exists to prevent — and the session store does the
  same, for the same reason (SES3 added two).
- **The arc's landings are local commits, unpushed by the owner's standing call** — `b70ac0e`
  (WSP1), `7333f74` (WSP2), `538bc03` (WSP3), `03ed5d9` (WSP4), `15ba288` (SES1), `a965feb`
  (SES2), `49da224` (SES3, with `aa24ea7` closing its outer loops) and `2487ec8` (CANON6), then the
  review: `6f45276` (the gate list) and `beaab13` (the shell's head). All
  gate-green; `git log` is the reviewable record.
- **What the arc left behind is now three backlog items, not prose** — CANON7, WSP5 and HARNESS2. They
  were carried as handover sentences for a while, which is how work quietly stops being work; the
  backlog is where something is still to do.
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
- **Verify before claiming done, always:** `npm run verify` (CLI 194 + `check` + version agreement),
  `dotnet test src/Daoris.Service/Daoris.Service.Tests` (248),
  `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests`
  (130), `dotnet test src/Daoris.Desktop/Daoris.Desktop.Modules.Tests` (39), `npm run rehearse:family` (154/154), `npm run test:web` (55 + 9). If a `bin`-driven gate is red
  while `npm test` is green, suspect a stale gitignored `dist/` first (FIX-LOG) — though `postpack
  --clean` and the rehearsal's leftover check now remove and assert that case away.

## Backlog

**The D48/D49/D50 arc is closed** — all eight items are built and in the archive, and REV2 reviewed
them. **SURF1 designed the next direction and is in the archive too**; its build order is the five
items below, in order. The three after them are the arc's leftovers, **actionable now**; the four
after those are **held**, each waiting on an external trigger that has not arrived. Each item is one
session-sized landing, TDD, gates green, moved to the archive on completion.

### The working surface — the build order (D51/D52, designed 2026-09-21)

The contract is `docs/2026-09-21-working-surface-design.md`; every item cites its sections. **Take
them in order**: SURF2 moves the lock without changing behaviour, which is what makes SURF3 safe.

- [ ] **SURF2 — the lock keys on the tree** (design §2, D51). The session record names the **tree** it
  runs in; `SessionStore.ActiveForAsync` and `SessionLedger`'s two refusals key on that instead of the
  repository name, and both sentences name the tree that holds it. **Nothing creates a tree yet** —
  every session's tree is the registered root, so behaviour is identical and the family rehearsal
  proves it, which is the whole point of landing this separately. The session store **adds a column**
  and preserves its rows (a record is the reviewable trace of work that happened; nothing can
  re-derive it). The tree path is **machine-local material and inherits the transcript's three
  guards** — stripped for a non-loopback caller, absent from the feed's shape, a literal NULL in the
  store's mirror — and the rehearsal's byte-level scan of the remote store grows a third string to
  look for, beside the root and the profile name.

- [ ] **SURF3 — session trees** (design §2, D51). `git worktree` under
  `~/.daoris/trees/<workspace>/<repository>/`, created **only on request** and opt-in per repository,
  with both editors (D50: the desktop, and a `daoris driver` verb). Branch named for the session,
  never reused, based on the canonical line as WSP4 already resolves it — falling back to the root's
  `HEAD` where none is declared, **saying which it used**. Four rules carry the risk: **the creating
  sentence states that a fresh tree holds nothing git does not track** (no dependencies, no build
  outputs — the price of D51, paid per repository); **`connect` from a linked worktree is refused
  naming the main one** (`--git-common-dir` against `--git-dir`; a re-pointed registration keeps
  working right up until the tree is removed, which is the worst failure available here); **removal
  refuses to destroy work**, naming what would be lost; and **the feed still reads only the registered
  root**, so WSP4's provenance model is untouched — assert it rather than assume it. The clean-tree
  rule stays on the root and is vacuous in a fresh tree, which is how a person's work in flight stops
  holding the driver.

- [ ] **SURF4 — the Work view** (design §3, D52). The sixth nav item: the sessions rail grouped by
  repository with **derived identity** (repository · kind · the quest's title or the conversation's
  first line · state · age), and the attended session — head, the **promoted** stream, the observed
  timeline, and the chat composer with its two distinct endings. It is the one view that breaks the
  reading-width cap. Starting a session moves here (repository, harness, profile, and whether it opens
  its own tree); **the stream gets one home** — Quests keeps the record summary and gains a door, and
  Projects keeps the registry's own controls. The shell remembers the last view. Both locale
  catalogues, stories for the states real data rarely shows, and the vitest inner loop over a mocked
  bridge; the Playwright loop holds the record half. **No step-parsing of the stream** — it was
  rejected by name (D52), and re-proposing it needs the D23/D24 argument answered first.

- [ ] **SURF5 — attention** (design §4, D52). Overview's **what needs you** band (parked first, then
  finished-and-unreviewed, then quests nobody can take); `AwaitingPerson`'s surface — its analysis at
  the top of the head, and exactly the three moves the ledger already allows, **no new states**; the
  sidebar's two counts, only one of which wears a status hue; and the **OS notification on park and on
  end**, never for an ending the person caused, per machine and off in one click — the shell's own
  code over WinForms, which **closes driver design open question 5**. The terminal's half is
  `daoris-driver` on the machine that holds the sessions (the `daoris` CLI's offline shape does not
  change), because a headless machine has no screen to notify and still needs the answer (D50).

- [ ] **SURF6 — review: the diff** (design §5, D52). The session's landed work as a diff, computed by
  git where the tree is and carried over the bridge — desktop-only for the console's reason (D47 §4),
  measured from the `HEAD` the driver already records, **bounded and saying what it truncated**. Merge
  into the canonical line and discard the tree are the person's explicit acts: merge is local and
  reversible and stays a press because it is where D37's verification lands; discard confirms and
  names what would be lost. The evidence string is unchanged for whoever reads the record from
  another machine. Playwright holds the negative guarantee: a browser sees the record and never a
  diff, a stream, a tree path or a notification setting.

### Open — the arc's leftovers, in the order they are worth doing

- [ ] **CANON7 — decide whether this repository's own `coreBudgetBytes` moves to the D28 default.**
  **An owner decision first, then a one-line change and the prose that cites it.** Daoris's manifest
  carries `24000`, written before D28 moved the default to **30000**; the always-loaded core now sits
  at **23,862**, so 138 bytes remain and the next canon addition fails the gate. The tension is not
  subtle: D28 raised the default *precisely because* 24000 "fired on the **canon** rather than on a
  repository's own material… That is backwards: the budget exists to constrain what a repository
  chooses to carry, not to cap what the doctrine may contain" — and this is the one repository whose
  always-loaded material IS the doctrine. The counter-argument is real too, and is why the number has
  not moved: every adopter pays for the core on every session, so a tight self-imposed limit here is a
  forcing function, and CANON6 shows it working (it found 126 bytes of genuine duplication rather than
  spending any). **Do not decide this by building it.** Bring the owner the two readings; if the answer
  is to move, it is `daoris.json` plus every place that quotes the number (`CLAUDE.md`, this file,
  `docs/DECISIONS.md` gets an amendment saying which way and why). **CANON5 is parked behind this.**

- [ ] **WSP5 — the platform's workspace switcher.** The one thing §8's Web row promised that the WSP
  arc did not land, and it was left deliberately: Projects shows each repository's circle and its fed
  commit, the Machine view shows the wiring, but **no view filters by workspace**. Workspace design §4
  states the shape — "one more filter, not a new view" — over the `workspace` argument the search,
  registry and convergence doors already take. The honest scope question to answer first: whether the
  switcher is a global chrome control (one circle at a time, like a git branch) or a per-view filter;
  §4's "scoped to one workspace per query" argues for the first, and the second is what a filter
  usually becomes. Web-only; no service change.

- [ ] **HARNESS2 — a `codex` session adapter.** **Not HARNESS1** (that is a second harness *layout*,
  a doctrine question; this is a second harness the driver can spawn). SES3 already made codex
  manageable as a TOOL — `daoris harness` knows its installer, its `CODEX_HOME` seam and how it
  reports a login, all verified against the real binary — so what is missing is only the
  `ISessionAdapter`: `Prepare`, `PrepareChat`, and `Interactive`. Two things make it more than a copy
  of `ClaudeCodeAdapter`, and both are why D23 says an adapter arrives deliberately and on proof: its
  non-interactive shape is `codex exec`, not a `-p` flag, and **its permission posture is the D37
  boundary in another tool's vocabulary** — the `acceptEdits` equivalent has to be established, not
  guessed. Land it the way `claude-code` was landed: the stub proves the loop, then a real driven run
  proves the adapter (DRV4's shape).

### Held — each waits on a trigger that has not arrived

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
  core has **138 bytes** of room (CANON6 freed 126 of them) and any new rule is an order of magnitude
  larger — so canonizing still waits on a D28-shaped split of principle from detail, or lands as pack
  knowledge for web repositories, which is where it most likely belongs anyway. **Its trigger is
  CANON7**: the budget question is the reason this is held, so settle that first.

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
