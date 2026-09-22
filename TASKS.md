# Daoris (道衍) — Active Task Backlog

> **This file holds OPEN tasks only.** A finished task is **removed from here and appended to
> [`docs/task-archive.md`](docs/task-archive.md)** with its date and outcome — never ticked in place.
> `CHANGELOG.md` is the release-facing log; the archive is the per-task record. The contract is
> `docs/2026-08-04-daoris-design.md`; the forward sequence is [`ROADMAP.md`](ROADMAP.md).

**Goal:** one canonical set of agent-facing rules and knowledge, materialized into every repository in
the family, kept from drifting, and improved from wherever the improvement was found — and, as of D45,
**Daoris as the driver**: the workflow manager that triggers and coordinates the agent sessions doing
the family's work.

**The arcs, in order, all closed and all in the archive.** **D45** — the driver: three parts, built,
with a real driven run behind part 2 (DRV4) and a two-machine crossing behind part 3 (DRV5).
**D48/D49/D50** — workspaces, the interactive surface, management parity: WSP1–4, SES1–3 and CANON6,
then reviewed at the owner's request (REV2, which found the release workflow running one of four
declared gates and the shell's 1,128 lines with no tests at all — both fixed and gated).
**D51/D52/D55/D56** — the desktop as a **code-gen-driven IDE**, designed by SURF1 after two reference
studies, built as SURF2–SURF10, closed 2026-09-22 when SURF5b also closed driver open question 5.
**D53** — the dsh evaluation the owner interposed, accepted the same day: *dsh is adopted as a
protocol, not a product*, which put the protocol door in front of the view.

**Two live directions, both the owner's and both measured before they were designed.** **The
toolchain** (2026-09-22 → **TOOL1/D57**): the measurement corrected a doctrine overclaim — the
accounts were Daoris's and the binary was still the machine's — and **TOOL2 and TOOL3 have landed**.
**The instruction file** (2026-09-22 → **CANON8/D59**): `.claude/rules/` is read by one harness of
three, so the always-loaded tier lives in `AGENTS.md` — **built and migrated**, this repository and
both examples with it. **The arc is closed** — CANON8a–e landed the move and CANON8d said the new
thing, in `analyze`, in D7, in the README and in `canon-authoring`.

## State

**All five artefacts exist and are built, and all three parts of D45 with them.** Fourteen commands,
**282 CLI tests, 275 service, 297 driver, 81 desktop modules, 483 web unit, 14 Playwright**, 59
devkit, 56/56 release rehearsal, **180/180 family rehearsal** (it names its own phases when you run
it), **36/36 deployment rehearsal** (D60), 5 universal devkit gates over this repository (DEVKIT3). Canon: 8 core rules, 5 knowledge documents, 5 skills, 7 packs. Always-loaded
core is **22,171 of 26,000 bytes** — a span in `AGENTS.md` since D59 — and **advisory rather than
enforced** (D54: a fact gates, a judgement reports). The answer to a full budget is still splitting
principle from detail (D28).

**The remote exists** (D47/DRV5) and **the driver drives** (D45/D46) — both built, both proven end
to end, and both described in full in `docs/task-archive.md` rather than here. The shapes that still
bind: what may leave a machine is two manifest declarations, silence meaning local; the quest state
machine is the only lock and outside sessions stay first-class; and a session's record is concluded
from its exit code and its quest's state, never from what it said about itself (D46 §4).

The service is **deployable** (D36) and **ships as executables** (D43): `npm run publish:service --
--install` lands both hosts self-contained in `~/.daoris/bin` and prints the ready `.mcp.json`
snippet; the release workflow ships them per platform with sha256s beside the devkit. Local sessions
spawn the MCP host over one persistent store; the HTTP host carries registrations and quests for a
remote deployment, key-gated, **no model required**. **The loops create their consumer** (D44): both
rehearsals run over a scratch copy of the examples and take a project born mid-run through init →
declare → sync → check → connect → its first quest. `Daoris.Web` is **the platform** (D38,
D40, D41, D42): five views landing on **Overview** — is anything sitting, the family's health, quests
grouped by state with sitting time, projects as scannable declarations — in a designed console shell
(sidebar, drawers, toasts, a validated status palette; `docs/2026-09-19-platform-ux.md`), built on
headless libraries (Tailwind v4 on the tokens, Radix, TanStack Query; `docs/2026-09-19-frontend-architecture.md`),
speaking **en + 简体中文** with a parity gate, with Storybook as the design tool and a test pyramid
declared in `daoris.gates.json` — a **Vitest inner loop** (the shell-attached surfaces, over a mocked
bridge) and a **Playwright outer loop over `examples/`** (the real bundle over the real host —
including what a browser must NEVER learn about the machine it is not running on). Doctrine stays
unwritable from every view. The **example family** under `examples/` is the router's proof (D39) — a
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

**Every prior arc is closed and in `docs/task-archive.md`** — D45 (the driver), D47 (the remote),
D48/D49/D50, D51–D56 (the working surface), D53/ACP1–ACP4 (the protocol door, all four
configurations), D57 (the toolchain), CANON8/D59 (the instruction file), and ARCH1 (the plugin
study). Nothing is pushed or published, and a release is still blocked on REH1.

🔴 **Daoris is DEPLOYED and running as an installed application** (2026-09-22), which is new and
changes what "works" means. `npm run publish:desktop -- --to <folder> --service` installs it: one
`daoris-desktop.exe` at the root, binaries under `app/`, state in `data/`. It runs against the real
`~/.daoris`, so **starting it starts the driver loop**.
`docs/2026-09-22-first-deployment-case-study.md` is the record, and it is the first document to read
before touching the desktop — four defects were invisible from inside the workspace, three of them
*because* of something the workspace provides. 🔴 **The second deployment (2026-09-23) found three
more**, all in `docs/FIX-LOG.md`: the shell spawned the machine's older host instead of the one
`--service` had published beside it (the locator now prefers what the install carries); a shell
that ADOPTS a running host serving another page now says so once, naming both bundles — as a
toast, which dismisses itself, and a notice that outlives one is a surface item nobody has filed;
and beneath both, the page was served from the WebView2 cache — `index.html` is `no-cache` now,
the hashed assets `immutable`, held by the deployment gate. **Republish and LOOK after every
surface change** — that is how all three were found, and `document.scripts` names which bundle is
live.

🔴 **The driven loop over the pipe door is blocked on a human step, and the driver now says so**
(DEPLOY1). Claude Code ignores a repository's `permissions.allow` until a person has accepted that
path in their own `~/.claude.json`; three real runs did the work and none could take its quest.
The driver reads that record and **holds** rather than spending nine minutes and a login. **What is
still the owner's**: whether adoption should ever *ask* and write that flag. 🔴 Never silently — the
flag is the person's grant. And **measure the ACP door first**: it runs the Agent SDK rather than the
CLI's trust flow, so it may not have the problem at all. One driven run answers it.

**Two things spend a real login and are the owner's to authorise**, both one command:

```
node tools/acp2-proof.mjs --drive      # ACP2 — D23's "on proof". DRV6 caps it at three attempts.
```

…and a driven run over `claude-code-acp` against the testbed, which would settle the trust question
above. Until ACP2 passes, `claude-code` over the pipe door remains what a machine drives with.

**The testbed is live and wired** (`tools/testbed.mjs --root <folder>`): three repositories in
workspace `testbed`, each with its own `.mcp.json` and trust settings, registered against the running
host. Quest `#7786da` sits open and parked after three strikes; `daoris driver retry 7786da` releases
it. Two branches there hold what the failed sessions produced.

🔴 **The direction is the DESKTOP, and development happens against the install** (owner, 2026-09-22
→ **D62**): the shell carries the platform, runs the driver loop, hosts the machine's service, and
is the only surface that can reach a machine-local fact at all — most of what Daoris can do, only it
can do. **Look at the real thing, not the fixture**: `npm run desktop -- run --install <dir>` starts
the published application with a debug port so `shot`, `eval` and `click` reach it (the published
app opens none by itself). The scratch loop has one circle, one account and two example
repositories, so every UI judgement made against it is a judgement about a machine nobody has.

**The looking pass is complete** (2026-09-22/23): every surface read once on the real machine —
20 registered repositories, 965 entries across 14, two workspaces, no named accounts. Overview,
Machine, Quests, Projects, Search, Convergence and Work each produced what the fixture structurally
could not show, and `docs/FIX-LOG.md` holds every one with its mechanism. 🔴 **The last of them is
the widest: 中文 search never worked in the SQLite index** — the tokeniser's two-character floor
dropped every Chinese term, and beneath it FTS5's `unicode61` keeps a run of ideographs as one token,
so even a kept term matched nothing. Every Chinese query had been returning the whole corpus. The
index is cut into bigrams now (schema 3, rebuilt on open). The next pass is the same pass after the
next change; what remains in this file is decisions and held rows.

🔴 **The old backlog is exhausted rather than abandoned**: every remaining row waits on the owner
(four) or on something arriving (seven), and the list is in the Backlog's own introduction. Surface
work now comes from looking at the deployed application and writing down what is wrong with it.

🔴 **TOOL5 was read against what is built and is a trigger, not work** — every sentence of the
toolchain design's §5 is realised, and what is waiting is a tool somebody names. It joins the held
set on CANON2's bar. The reading found one real gap and it is fixed: the toolchain twin tables
pinned the three ACP arrivals and left `claude-code` — what a machine actually drives with —
asserted on neither side.
🔴 **HELP3 is a design question before it is a build**, and the question is *where the guard lives*.
A hook that refuses a push lives in a repository's `.claude/settings.json`, and Daoris has exactly
two ways to put one there — through `sync`, which means the canon grows a fourth installable kind
and Daoris owns part of a JSON file an adopter also owns (D59's problem without comment markers), or
from the driver, which is **forbidden** (D32, and ACP4's whole point was carrying wiring so nothing
is written). The `claude-code` adapter already records the rule it has to respect: *"the repository's
own checked-in configuration governs"*. Probe 4's Windows finding constrains the guard itself — only
a **structured deny** blocks under a 5.1 executor, never exit 2. Settle where it lives before writing
one.
🔴 A *layout-toggles* item was carried in this paragraph for a while and **never existed in the
backlog** — it was prose pretending to be work, which is exactly what the backlog is for; a surface
item is written against `docs/2026-09-21-working-surface-design.md` when somebody wants one, and the
panel and dock state would need hoisting out of `WorkFrame` first. **Four are decisions, not work**:
ACP2 and DEPLOY1's second half both cost a real login to settle, PLUG2 reopens D4's *"core installs
with no opt-out"*, and DEPLOY4 asks whether per-install UI state joins `~/.daoris` — and
🔴 `~/.daoris` being machine-wide is load-bearing, since it is what makes the CLI and the desktop two
doors onto one machine. Four **held** items sit at the bottom; do not pick one up until its trigger
has arrived.

**Start by reading the contract the item cites** — every backlog row names one. The bullets below are
the traps that are not in any contract, because they were found rather than designed.

- **Read first for anything else** — the arc the SURF work stands on:
  `docs/2026-09-20-workspace-design.md` and `docs/2026-09-20-interactive-design.md`
  (the two contracts — every WSP/SES item cites its sections); `docs/DECISIONS.md` **D48–D50** (the
  direction, the argued rejections, and management parity — D50 binds every item), with D45–D47
  behind them for the driver and remote these extend; the REV1 entry in `docs/task-archive.md` and `docs/FIX-LOG.md`'s top entries (the
  key-console scan, the mirror-down feed-back and the shared-scan fixes — WSP4 builds directly on
  those lessons).
- **The gates are a list, and it is checked.** `daoris.gates.json` declares seven; a CLI test asserts the
  release workflow runs every one, because they silently disagreed for eight landings and both
  rehearsals passing is exactly what hid it. Adding a test project means adding it to **both**. The
  seventh (`deployment`, D60) is the one that runs on **Windows** — the shell is `net10.0-windows`,
  so the workflow carries a `windows-latest` job for it.
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
- **What the arc left behind is now backlog items, not prose** — CANON7 and HARNESS2 (WSP5 and DOCS2
  both landed 2026-09-21 and are in the archive; ARCH1 and DEVKIT3 joined the list the same day). They
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
- **Every open item now waits on something**, which is new and is the honest state rather than a
  lull. **Four are the owner's** (ACP2 and DEPLOY1's second half each cost a real login; DEPLOY4 and
  PLUG2 are decisions). **Seven wait on a trigger**: TOOL4 on TOOL3's transcripts, TOOL5 and CANON2
  on a repository naming what it wants, HARNESS1 on the same, REH1 and TEST1 on a captured recurrence,
  and HELP3 on where the guard lives being settled. Pick one up only when its trigger has actually
  arrived — and **a new direction from the owner outranks all of them**.
- **The shell has a dev loop now (DEV1):** `npm run desktop -- doctor|build|run|shot|eval|click`. It
  is **not a gate** — it starts the real window on a scratch machine of its own, and `eval` is the one
  instrument that reaches the bridge-attached half (the Machine view, the driver controls, the
  console, chat) that Playwright cannot reach and vitest only mocks. Reach for it when SURF4/SURF5
  land a surface: seeing the real thing is the step that had no tooling at all.
- **Verify before claiming done, always:** `npm run verify` (typecheck + CLI 282 + `check` + doc
  budgets + version agreement),
  `dotnet test src/Daoris.Service/Daoris.Service.Tests` (275),
  `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests`
  (297), `dotnet test src/Daoris.Desktop/Daoris.Desktop.Modules.Tests` (81), `npm run rehearse:family` (180/180), `npm run test:web` (483 vitest + 14 Playwright),
  and — when the desktop, the publish or the locator moved — `npm run rehearse:deploy` (36/36), which
  publishes the shell to `_fixtures/` and drives **that**. 🔴 **Its phase 4 asserts the host the
  deployed shell started is the INSTALL's own** — on this machine `~/.daoris/bin` holds a second
  host, which is the decoy that check exists for; a clean machine cannot express the defect.
  🔴 **Stop a running shell and its host first** — an orphaned `daoris-knowledge-http` holds the
  build's own assemblies, which reads as a broken gate and is a lock (`npm run desktop -- kill`). If a `bin`-driven gate is red
  while `npm test` is green, suspect a stale gitignored `dist/` first (FIX-LOG) — though `postpack
  --clean` and the rehearsal's leftover check now remove and assert that case away.

## Backlog

**The D48/D49/D50 arc is closed** — all eight items are built and in the archive, and REV2 reviewed
them. **SURF1 designed the next direction and is in the archive too**, and **DSH1 evaluated dsh** (in
the archive; its decision is **D53, accepted**). **Thirteen items are open** and every closed one is
in `docs/task-archive.md` — this file holds no ticked rows, by the `task-lifecycle` rule it also
ships. Four are **decisions** the owner has to make rather than work anyone can pick up (ACP2,
DEPLOY1's second half, DEPLOY4, PLUG2); one more (TOOL4) is held by D57 until TOOL3 has run; and the
last four are **held**, each waiting on an external trigger that has not arrived. Two more joined
that class by being read rather than by anyone deciding to defer them: 🔴 **HELP3 is a design
question first, not a build** (where the guard lives), and 🔴 **TOOL5 is a trigger** — the toolchain
design's §5 is realised and what waits is a tool somebody names.

🔴 **Which leaves nothing actionable**, as of 2026-09-22. That is the first time it has been true and
it is a result, not a stall: the D45, D47, D48–D50, D51–D56, D53, D57, D59, D60 and ARCH1 arcs are
all closed and in the archive. **The two ripest rows are DEPLOY4 and PLUG2** — both decisions, both
needing no login and no external event. Each item is one session-sized landing, TDD, gates green,
moved to the archive on completion.

### The protocol door — ACP (D53, accepted 2026-09-21)

**Settled by DSH1's evidence** (`docs/2026-09-21-dsh-evaluation.md`): the adapter seam grows a door
that holds a session over the **Agent Client Protocol** beside today's pipe, and every harness reached
that way is a *configuration* of the door. **ACP1 has landed** (2026-09-21, in the archive): the seam
carries a `Wire`, `AcpSession` speaks the protocol, and the rehearsal drives a quest to done over it
with no model in the gate — including the permission refusal from both sides. **ACP4 and ACP3 have
landed too**, so the door now carries four configurations — `acp-stub`, `claude-code-acp`, `codex-acp`
and `dsh`. **ACP2 is the one left, and it is the owner's**: its closing step spends a real login.

🔴 **The posture is the adapter's, in that adapter's own words** (ACP3): `acceptEdits` to Claude Code,
`agent` to Codex, and to dsh not a wire concept at all — `DSH_PERMISSION_MODE=workspace-write` in the
environment, because its `session/new` carries no modes. A new configuration of this door states its
own, and **null means the wire carries none** rather than a licence to guess a neighbouring mode.
`docs/2026-09-22-acp3-probe-evidence.md` is how each was established, and which are bundle rather than
wire evidence.

- [ ] **ACP2 — `claude-code` over ACP.** `@agentclientprotocol/claude-agent-acp` pinned exact as a
  managed toolchain entry (`daoris harness`: install, version, the executable seam
  `CLAUDE_CODE_EXECUTABLE` → the managed `claude`, the profile seam `CLAUDE_CONFIG_DIR` → the chosen
  credential profile — both verified keylessly in the evaluation's §1a); `acceptEdits` set as the ACP
  **mode**, not a flag; the permission answerer against Claude Code's real requests; the record naming
  adapter, harness version and profile as today. Closes with **the real driven run** (DRV4's shape) —
  the owner supplies the login. **This is D23's "on proof".** The pipe door stays supported until it
  passes.

  ⏸ **Driven for real 2026-09-22, and it got as far as the model.** `node tools/acp2-proof.mjs
  --drive` now spawns the pinned adapter, reaches Claude, sets `acceptEdits` as a mode, discovers the
  repository's synced skills and streams a real answer — **11 of 13 checks**. It stops at one thing,
  and it is a missing feature rather than a fault in the door: **ACP4**. The session is told to take
  its quest and the door hands it no MCP server, so it ends the turn having touched nothing.
  **ACP2 closes when ACP4 lands** and the same command passes 13/13.

  Four defects were found by running it, all fixed: the presence probe asked about a different binary
  than the spawn would run, so a working pin reported absent (FIX-LOG — it hid `codex` too); a scratch
  host with no root of its own indexed the machine's whole family (FIX-LOG); `connect` has no
  `--service` flag and an unknown flag is ignored in silence; and a driver that refuses a dirty tree
  was right while the fixture, which left the adoption uncommitted, was wrong.

- [ ] **HELP3 — one guard, every harness.** `dsh-hooks-claude-code` runs an existing `hooks.json` in
  Claude Code's dialect and `dsh-hook-protocol` makes the Codex bridge behave identically, so a guard
  written **once** — refuse a write outside the session's tree (D51), refuse a push (D37) — runs on
  all three. Probe 4 found the Windows trap. Held behind ACP4 and CANON8: a guard is worth less than
  the doctrine it enforces arriving at all. (HELP1 became CANON8; D59 has it.)
  ⛔ **Both holds lifted 2026-09-22, and reading it then surfaced the real one: where does the guard
  live?** It is a `.claude/settings.json` hook, so it belongs to the repository — the `claude-code`
  adapter already states the rule it has to respect: *"the repository's own checked-in configuration
  governs"*. Daoris can only put one there through `sync` (a fourth installable kind, and Daoris
  owning part of a JSON file the adopter also owns — D59's region problem with no comment markers to
  mark it) or from the driver, which **D32 forbids** and which ACP4 was built to avoid. **Settle that
  first; it is a decision, not a patch.** Whatever is written must block **structurally**
  (`permissionDecision: deny`) and never by exit 2 — Windows PowerShell 5.1 collapses a native exit
  code, so an exit-2 hook does not block at all (probe 4, run 2).

### The working surface — the build order (D51/D52/D55/D56)

**Closed 2026-09-22.** Every SURF item is built — SURF2, SURF3, SURF4a–d, SURF5, SURF5b, SURF6a,
SURF6b, SURF7 through SURF10 — and the surface is usable: `npm run desktop -- run`. The contracts are
`docs/2026-09-21-working-surface-design.md` (what it is),
`docs/2026-09-21-working-surface-components.md` (**how a screen gets built** — a story before the
component, its own test, and **a molecule imports no hook**, which a test asserts) and
`docs/2026-09-21-ide-reference-study.md` (what changed it). `docs/2026-09-19-platform-ux.md` carries
what each look-at-it pass settled, including the IDE layout of 2026-09-22. Two rules the building
established that still bind anything new: a timeline is **derived**, and **the stream has one home**.
Nothing here is open; a new surface item is built against those documents, not against this heading.

### The instruction file — where the always-loaded tier lives (owner, 2026-09-22 → D59)

**Closed 2026-09-22.** CANON8a–e moved the always-loaded tier into a region of `AGENTS.md` and
CANON8d said the new thing everywhere the old one was still written. The contract is
`docs/2026-09-22-instruction-file-design.md`, **D59** carries the four rejected alternatives, and the
archive carries each item's outcome. Nothing here is open; a *new* instruction-file item would be
built against that design document, not against this heading.

### The next direction — the toolchain and its accounts (owner, 2026-09-22)

> *"I still cannot see a proper credential management since we need this for both claude/codex, and
> other llm if possible (this is kind more from deepseek harness) and also we need to be able managed
> multiple account with usage management (for example multiple claude accounts) and currently we still
> don't have managed cli (still reading from the machine)."*

**TOOL1 designed it the same day** (in the archive) and the owner accepted **D57**:
`docs/2026-09-22-toolchain-design.md` is the contract. The measurement it started with corrected
doctrine — **the accounts are Daoris's and the binary is the machine's** — and the owner's two
answers narrowed the rest: **usage is measured before it is managed**, and breadth is **more native
adapters plus the ACP door, not a registry**. So **D49 §4 and D24 both stand**, which is why none of
the items below needs a credential or a model name. Take them in order.

**TOOL2 landed 2026-09-22** (in the archive): `daoris harness pin|unpin` plus the Machine view's
half, and a pin that decides what a session actually spawns. It became the **fourth rule of the twin
contract** — *the binary is the explicit command, then the managed pin, then `PATH`* — and 🔴 **absent
still means `PATH`, byte for byte**. A pin nobody installed **refuses** rather than falling back,
because running a different tool than the one that was pinned and recording the pinned version beside
it is worse than not supporting pins.

**TOOL3 landed 2026-09-22** (in the archive): ACP's `usage_update` is parsed structurally instead of
rendered away, recorded per session at its **high-water mark** (context drops when a session
compacts, so the last reading would report a nearly-full window as nearly empty) and totalled per
account. 🔴 **Absent is never zero** — an agent that reports nothing, a pipe-door session, and a
machine that has measured nothing are three different absences and none of them renders a 0. Machine-
local by the profile's own rule, no price claimed, and a test asserts no currency symbol renders.
**TOOL4 can now be written against observed behaviour**, which was the point of the ordering.

- [ ] **TOOL4 — rotation.** ⛔ **Held by D57 §b until TOOL3 has run long enough to answer three
  questions**: what a harness's exhaustion actually looks like in its output, how long a cool-off
  should be, and whether a rotated session stays reproducible. Exhaustion is **observed, never read**
  — Daoris cannot ask a provider what is left without a credential, and D49 §4 stands. Do not start
  this before there are real transcripts to write the signal against; a string match on somebody
  else's error text is the fragile part and guessing it is how it gets written wrong.

- [ ] **TOOL5 — more native adapters** (design §5). D23 applied more times: a native adapter per tool
  worth matching properly (its own flags, configuration home, login flow, version question), with the
  ACP door for tools that speak the protocol and a tool free to be both. **Not a registry** — the
  components plan's rejection stands and D24 with it. Largely folds into **ACP3**, which already
  brings dsh and codex as configurations of the door; this item is what remains once that lands.
  ⛔ **Read against what is built, 2026-09-22: what remains is a TRIGGER, not work.** Every sentence
  of §5 is realised — the adapter seam, the ACP door, a tool being both (`codex` is managed with no
  native adapter and its sessions ride `codex-acp`, which §5 names as the sanctioned shape), the
  declaration duplicated in two artefacts, no registry, no model named. **Nothing is waiting except a
  tool somebody actually wants**, so this is held on the same bar CANON2 and HARNESS1 use: start it
  when a repository names the tool, because an adapter's every field is a claim about somebody else's
  program and a guessed one fails in a person's terminal. The reading did find one real gap, and that
  is fixed rather than carried — the twin tables pinned the three ACP arrivals and left `claude-code`
  asserted on neither side.

### The first deployment — what running outside a checkout found (owner, 2026-09-22)

> *"setup and deploy the desktop version to \<install\> and we can drive and log it properly for some
> real case study and testing"* — and then, looking at it: *"currently it just bit messy"* about the
> install folder, and *"the application topbar you can take more example from application like
> vscode"*.

**Done and in the archive**: the deployment itself, `tools/desktop-publish.mjs`, the install layout,
the command center, the scrim, four defects only deploying found — and **DEPLOY2, the gate over the
deployed artefact** (`npm run rehearse:deploy`, D60).
`docs/2026-09-22-first-deployment-case-study.md` is the record. **What is left is two decisions, and
both are the owner's.**

- [ ] 🔴 **DEPLOY1's second half — should adoption ever ASK for the trust flag?** The detection half
  shipped 2026-09-22 and is in the archive: the driver reads the harness's own record and **holds**
  rather than spending nine minutes and a real login on a session that could never close its quest.
  What is left is the owner's: whether adoption should *ask* and write the flag (option b), or
  whether the pipe door stays documented as needing a human's first visit (option c). 🔴 **Never
  (d), silently** — that flag *is* the grant. And **measure the ACP door first**: it runs the Agent
  SDK rather than the CLI's trust flow, so it may not have the problem at all, and one driven run
  answers it.

- [ ] **DEPLOY4 — Daoris writes in two places, and one of them is per-install.** Everything the tool
  owns is under `~/.daoris` (registry, `driver.json`, `harnesses.json`, the index, sessions, `bin/`,
  `toolchain/`, and profiles when they exist) — **except** the desktop's own `data/` beside the
  install, which holds the WebView2 profile and the window's geometry. Two homes for one application.
  🔴 **`~/.daoris` being machine-wide is load-bearing, not an accident**: it is what makes the CLI and
  the desktop two doors onto one machine (D50), and moving it inside an install would give two
  installs two registries and leave `daoris` on a terminal unable to see either. So the question is
  narrower than it looks — whether per-install UI state should join it, and under what key, since a
  scratch run and a real one must not contend for a window's geometry. Owner's call; measure before
  moving anything.
  ✅ **Measured 2026-09-22, against the live deployment on this machine**, which is what the item
  asked for before anything moves. `~/.daoris` is **472 MB**: `toolchain/` 273 MB (one pinned
  adapter), `bin/` 182 MB (two self-contained hosts), `knowledge.db` 17 MB, `sessions/` 16 KB, and
  two config files of 1 KB each. The per-install `data/` is **21 MB and two things**: a WebView2
  user-data folder (21 MB of browser cache, cookies and local storage) and `window-state.json` —
  **five integers**, `Width Height X Y Placement`.
  🔴 **So the question is smaller than the item feared, and the two homes hold different KINDS of
  thing.** Everything in `~/.daoris` is a machine fact both doors read; everything in `data/` is one
  window's rendering state, and 99.99% of it by size is a browser profile that a scratch run and a
  real one must never share. **Recommendation: leave it**, and say so in the README rather than move
  it — D50's property is already satisfied, because nothing in `data/` is a *capability* a
  screenless machine would be denied. The five integers could move, and moving them alone would need
  a per-install-root key for more machinery than five integers are worth. Still the owner's call.

### Plugins — what the study left (owner, 2026-09-22 → ARCH1)

`docs/2026-09-22-plugin-design-study.md` is the contract. **PLUG1 and PLUG3 landed 2026-09-22** and
are in the archive — a pack declares the canon it needs, and the README names all three seams.
**PLUG2 is the one left, and it is a decision.** **Two standing decisions are NOT reopened by it**:
no adapter registry (D23/D24/TOOL5 — the ACP door is already that answer, and a protocol beats a
binary API), and no plugin runtime for the surface (D52).

- [ ] **PLUG2 — a pack cannot disable or override what core installs.** dsh composes profiles as
  ordered layers where a layer may switch a row off (`- id: x` / `disabled: true`); Daoris's manifest
  `packs: []` is a flat set with no precedence. ⛔ **Decide before building**: this reopens **D4's
  "core installs with no opt-out"**, which was a deliberate choice about doctrine rather than a
  limitation. The owner's call, and the study says so rather than assuming it.

### Open — the arc's leftovers, in the order they are worth doing

### Held — each waits on a trigger that has not arrived

- [ ] **REH1 — the release rehearsal intermittently reports 45/52.** Seen twice, **always exactly 7
  failures** — precisely the canon-upgrade phase's 7 checks, so a whole phase fails on a broken
  precondition rather than a flaky assertion. Both times it ran straight after canon files were edited
  and synced. **Every run now writes a transcript to `_fixtures/rehearsal-logs/` by construction**
  (2026-09-18), so the next failure is captured without anyone remembering to. Eight runs that day —
  several straight after canon edits and syncs, the suspected trigger — all passed 52/52, and a ninth
  ran clean 2026-09-20 after the whole DRV5 arc (no canon edits that session, which is the case that
  has always passed). Stays open until a captured failure explains it. Do not tag a release while this
  is open. **Not this**: on 2026-09-21 the rehearsal failed at `npm pack` before any check ran — a
  deterministic broken publish build, fixed and gated (FIX-LOG). REH1 is 45/52 with the canon-upgrade
  phase's 7 checks failing; a run that never reaches a check is a different animal.

- [ ] **TEST1 — the Playwright suite aborted a worker once with `0xC0000409`.** Seen once
  (2026-09-21, during SURF2): the run died mid-suite with `worker process exited unexpectedly
  (code=3221226505)` — Windows `__fastfail`, which produces no output, no stack and no WER entry — and
  the identical run passed 9/9 immediately after. The test it died on touches session records, which
  is why it was not dismissed on sight; the assertion it would have made passed on the re-run and the
  change it was suspected of is covered by 258 service tests and 156 rehearsal checks. **A family
  sibling has the same abort documented at ~1.5% of e2e runs with a standing reproducer** (spawn a
  server, poll it, kill it — it fires about 1 in 300 rounds, 4-way concurrent), so the shape is known
  and is not this repository's to diagnose from one occurrence. **Its trigger is a second sighting**:
  if it recurs, capture the Playwright HTML report and compare against the sibling's notes before
  changing anything. Do not tune timeouts on one data point.

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
