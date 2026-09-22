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
both examples with it. **CANON8d** is what is left of it.

## State

**All five artefacts exist and are built, and all three parts of D45 with them.** Fourteen commands,
**265 CLI tests, 262 service, 239 driver, 71 desktop modules, 412 web unit, 14 Playwright**, 57
devkit, 56/56 release rehearsal, **180/180 family rehearsal** (it names its own phases when you run
it), 9 devkit gates. Canon: 8 core rules, 5 knowledge documents, 5 skills, 6 packs. Always-loaded
core is **21,817 of 26,000 bytes** — a span in `AGENTS.md` since D59 — and **advisory rather than
enforced** (D54: a fact gates, a judgement reports). The answer to a full budget is still splitting
principle from detail (D28).

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
D48/D49/D50 (workspaces, the interactive surface, management parity), D51–D56 (the working surface,
SURF2 through SURF10), D53/ACP1 (the protocol door), D57 (the toolchain, TOOL2 and TOOL3). Nothing is
pushed or published, and a release is still blocked on REH1.

**Two things are open and both are the owner's directions**, in the Backlog below: **CANON8/D59**, the
instruction file — designed 2026-09-22, nothing built, and the larger of the two; and the rest of the
toolchain and protocol work (ACP2's last step is ACP4). Four **held** items sit at the bottom; do not
pick one up until its trigger has arrived.

**Start by reading the contract the item cites** — every backlog row names one. The bullets below are
the traps that are not in any contract, because they were found rather than designed.

- **Read first for SURF work:** `docs/2026-09-21-working-surface-design.md` (the contract),
  `docs/2026-09-21-working-surface-components.md` (**how the screens get built** — the layers, the
  inventory, the three loops each part passes) with
  `docs/2026-09-20-working-surface-research.md` behind them (the field, and what Daoris already has
  that it does not), and **D51/D52**. Two sentences from the contract carry the most weight: the planner keeps one
  *driven* session per repository even after the lock moves (D46 §9 survives — pacing a domain and
  preventing corruption are different jobs), and **a fresh tree holds nothing git does not track**,
  which is the price the creating sentence has to state out loud.
- **Read first for the ACP work (D53, once confirmed):** `docs/2026-09-21-dsh-evaluation.md` — §1
  probe 2 is the wire the door speaks, §1a is the Claude adapter's two seams, §2 is what each standing
  decision demands, §5 is the build order; D53 itself; and `tools/dsh-probes/` — the ACP client there is
  the mirror of the stub agent ACP1's rehearsal phase needs. Three facts from the runs that will bite:
  dsh's credential scrub strips any child environment name containing KEY, TOKEN or SECRET; an exit-2
  Claude Code hook does not block under a Windows PowerShell 5.1 executor (the structured deny does);
  and the ACP wire flattens `aborted | blocked | error` to `end_turn`, which is why records keep moving
  on exit code + quest state.
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
- **The held items (REH1, CANON5, CANON2, HARNESS1) still wait on their external triggers** — pick one
  up only when its trigger has actually arrived.
- **The shell has a dev loop now (DEV1):** `npm run desktop -- doctor|build|run|shot|eval|click`. It
  is **not a gate** — it starts the real window on a scratch machine of its own, and `eval` is the one
  instrument that reaches the bridge-attached half (the Machine view, the driver controls, the
  console, chat) that Playwright cannot reach and vitest only mocks. Reach for it when SURF4/SURF5
  land a surface: seeing the real thing is the step that had no tooling at all.
- **Verify before claiming done, always:** `npm run verify` (typecheck + CLI 211 + `check` + doc
  budgets + version agreement),
  `dotnet test src/Daoris.Service/Daoris.Service.Tests` (259),
  `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests`
  (152), `dotnet test src/Daoris.Desktop/Daoris.Desktop.Modules.Tests` (40), `npm run rehearse:family` (173/173), `npm run test:web` (303 vitest + 11 Playwright). If a `bin`-driven gate is red
  while `npm test` is green, suspect a stale gitignored `dist/` first (FIX-LOG) — though `postpack
  --clean` and the rehearsal's leftover check now remove and assert that case away.

## Backlog

**The D48/D49/D50 arc is closed** — all eight items are built and in the archive, and REV2 reviewed
them. **SURF1 designed the next direction and is in the archive too**, and **DSH1 evaluated dsh** (in
the archive; its decision is **D53, accepted**). The protocol items come first; the SURF items
follow, in order; the three after them are the arc's leftovers,
**actionable now**; the four after those are **held**, each waiting on an external trigger that has
not arrived. Each item is one session-sized landing, TDD, gates green, moved to the archive on
completion.

### The protocol door — ACP (D53, accepted 2026-09-21)

**Settled by DSH1's evidence** (`docs/2026-09-21-dsh-evaluation.md`): the adapter seam grows a door
that holds a session over the **Agent Client Protocol** beside today's pipe, and every harness reached
that way is a *configuration* of the door. **ACP1 has landed** (2026-09-21, in the archive): the seam
carries a `Wire`, `AcpSession` speaks the protocol, and the rehearsal drives a quest to done over it
with no model in the gate — including the permission refusal from both sides. **ACP2 is next**, and it
is the one that needs the owner: its closing step spends a real login.

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

- [ ] **ACP3 — dsh and codex as configurations.** `dsh --profile acp` with `DSH_HOME` as the profile
  seam (it isolates credentials, settings and sessions as one directory), the model in the profile's
  own `settings.yaml` (Daoris names none — D24), the two outbound rows (`session-telemetry-otel`,
  `session-log-deepseek`) patched off in the profile Daoris owns the location of, and no login question
  to ask (permissive `unknown`, SES3's rule); `@agentclientprotocol/codex-acp` likewise. **HARNESS2
  closes into this.** dsh pinned exact and vendored nowhere: 561 MB per machine, and the
  `subagent-claude-code` bundle on npm was six weeks stale when measured — a harness's own packaging is
  its own problem, but the version the toolchain installs is asserted, not assumed.

- [x] **ACP4 — the MCP servers the door hands over.** ✅ **done 2026-09-22.** The composed target
  tells every session to claim and close its quest over its own connector; the pipe door leans on the
  repository’s own `.mcp.json`, which an adopted repository may not have and which the driver may
  never reach in and write. The protocol carries the wiring on `session/new`, so a driven session is
  handed its voice with **nothing written anywhere**. A machine with no host still drives and says so.
  Gated in the family rehearsal from the AGENT’s own side (180/180).

- [ ] **HELP2 — skills reach one harness only.** `dsh-skill-filesystem` scans `<project>/.dsh/skills`
  and `<project>/.agents/skills`, never `.claude/skills` — but its bundle format is `<name>/SKILL.md`,
  **exactly Daoris's layout**, so this is a root rather than a conversion: `customSkillDirs` naming
  `.claude/skills`, in the profile Daoris owns the location of (SES3). Nothing on an adopter's disk
  changes. Do it with ACP3, where the dsh profile is first written.

- [ ] **HELP3 — one guard, every harness.** `dsh-hooks-claude-code` runs an existing `hooks.json` in
  Claude Code's dialect and `dsh-hook-protocol` makes the Codex bridge behave identically, so a guard
  written **once** — refuse a write outside the session's tree (D51), refuse a push (D37) — runs on
  all three. Probe 4 found the Windows trap. Held behind ACP4 and CANON8: a guard is worth less than
  the doctrine it enforces arriving at all. (HELP1 became CANON8; D59 has it.)

### The working surface — the build order (D51/D52, designed 2026-09-21)

The contract is `docs/2026-09-21-working-surface-design.md`; every item cites its sections. **Take
them in order.** SURF2 and SURF3 are done (2026-09-21, in the archive): the lock keys on the tree,
and the trees exist — opt-in per repository, grown per session, refusing to die holding work.
**SURF4a–d are unblocked by D53** (accepted 2026-09-21): they build on `Daoris.Web` exactly as
designed — option C, the view as a dsh deployment, was rejected by the evidence — and SURF4c's
timeline gains ACP's structured source (tool lifecycle, turn boundaries, thoughts, usage) as the
protocol D52 said stdout parsing was not. **Take ACP1 first**: SURF4c is the timeline's consumer, and
building the consumer before the source means building it twice.
Everything with a screen in it also follows `docs/2026-09-21-working-surface-components.md` — the
surface is built **component by component**, each with its story and its own test, because a rail, a
head, a live stream, a timeline, a composer and a diff built as one view is a file where the first
thing that renders is the last thing.

**D55 re-positions the whole surface** (owner, 2026-09-21: *"the desktop is becoming more a dev ide
(but code gen driven)"*, with the method note *"you should reference more existing application for
designing the ui/ux"*). The evidence is `docs/2026-09-21-ide-reference-study.md`; five patterns
recur across its references and the platform has none of them, which is why Work is a **frame** and
not a nav item. **Nothing built is wasted** — atoms and molecules are frame-independent, which is
what "a molecule imports no hook" bought. One item the study argues for that nobody had filed:

**SURF4 is four items, cut along the layers** (owner, 2026-09-21: *"this is a large UI/UX as a whole,
so develop it component by component — more of an atomic design pattern — so each part can be tested
one by one"*). The method, the inventory and the dependency rule are
`docs/2026-09-21-working-surface-components.md`; read it before starting any of the four. Its one
load-bearing rule: **a molecule imports no hook**, which is what makes every state reachable by
passing props — and a test asserts it, so it cannot quietly stop being true. **The layout structure
is deepseek-harness's** (owner, 2026-09-21; components doc §3a, D52 as amended): the three-column
frame with its geometry, the right dock keyed to the attended session (timeline + diff), the
follow-the-tail stream rule — structure and geometry, never its pixels (D41) and never its plugin
runtime (its surfaces are built natively against claude/codex through the adapter seam, D23).

🔴 **Every SURF item is built** — SURF2, SURF3, SURF4a–d, SURF5, SURF5b, SURF6a, SURF6b, SURF7,
SURF8, SURF9 and SURF10, all landed by 2026-09-22. `docs/task-archive.md` carries each one's outcome,
and `docs/2026-09-19-platform-ux.md` §4 what each look-at-it pass settled. **The surface is reachable
and usable**: `npm run desktop -- run`, switch to *Work*. Nothing in this section is open; it is kept
only because the paragraphs above are the contract a *new* surface item would be built against.

### The instruction file — where the always-loaded tier lives (owner, 2026-09-22 → D59)

> *"this agent file design actually made a common management style for different agents which is good
> to take"* — on a candidate adopter whose `CLAUDE.md` is one line, `@AGENTS.md`, over 133 lines of
> its own doctrine.

**Designed and decided the same day**: `docs/2026-09-22-instruction-file-design.md` is the contract and
**D59** carries the four rejected alternatives. What forced it was a measurement — **`.claude/rules/`
is read by exactly one of the three harnesses** (evaluation §6.5). HELP1 is now this arc.

- [x] **CANON8a — the region, and the state space under it.** ✅ **done 2026-09-22.**
  `src/Daoris.Cli/src/region.ts`: pure, 22 tests, no disk. Design §4's table implemented as a table —
  absent, present, and the three damage states, each **refused with the line number**. Markers are
  matched as WHOLE LINES, so one quoted in a fenced code block (which this repository's own docs do)
  is prose rather than a boundary. `ensureImport` is the pointer file's smaller half, and it is
  **always a region even when Daoris creates the whole file** — a bare line is a line nothing can
  retire, because the lock describes a file or a span and never a stray sentence.

  🔴 **Two bugs the fixtures could not see, both found by running it over a real adopter's file.**
  Its `AGENTS.md` is 133 CRLF lines, and the body came back with a carriage return on every interior
  line because the read stripped one at the very end — correct for a one-line body and wrong for
  every longer one (FIX-LOG). And `hasImport` rejected *"Everything is in @AGENTS.md."*, the first
  sentence anybody would write, because it demanded whitespace after the target; the boundary is
  about **paths**, and `@AGENTS.mdx`, `@AGENTS.md.backup` and `@AGENTS.md/nested` are three other
  files.


- [x] **CANON8b/c — `sync`, `check`, `upstream`, the lock over a span, and the migration.**
  ✅ **done 2026-09-22.** 265 CLI tests green; this repository and both examples migrated in the same
  commit. Always-loaded core **23,862 → 21,817 bytes**, because eight frontmatter blocks and a
  separate roster's preamble went away. The archive carries the outcome.

- [ ] **CANON8d — say the new thing.** `analyze` already detects the `AGENTS.md` convention and says
  *"what it installs will be invisible to them"*, which stops being true; `init`'s adoption flow, the
  README's three layers, `canon-authoring`'s "`rules/` is always-loaded", and D7 in the contract all
  describe the directory. Each is a sentence, and a stale one is believed for exactly as long as it
  survives (`claims-need-checks`).

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

### Open — the arc's leftovers, in the order they are worth doing

- [ ] **DEVKIT3 — the devkit is not run over its own repository, and its scan has six findings
  waiting.** Found by DOCS2 while looking for somewhere to put a gate
  (`docs/2026-09-21-dsh-evaluation.md` §3a). The release workflow runs the devkit's *test suite* and
  then each declared gate by name; nothing runs `daoris-devkit verify` here, there is no `.githooks/`,
  and so the universal gates this repository configures in `daoris.gates.json` — sensitive, version,
  docs, links, doctrine — are configuration nothing reads. Run by hand it exits 1 on **six sensitive
  findings, all in test fixtures**: Unix home paths and private-range addresses, the same shape as the
  one object already acknowledged by sha. The work is to read and judge each — acknowledge it by sha
  or neutralise the fixture, never a path ignore (the devkit's own asymmetry argument) — then wire
  `daoris-devkit verify` into the gate list and the release workflow **as one row in both**, and
  correct the devkit README's "it runs this repository's own gates", which today it does not.- [ ] **ARCH1 — dsh's domain separation and plugin design as the example for Daoris's own structure**
  (owner, 2026-09-21, mid-session: *"the dsh is really a good example to take for design/develop and
  its domain separation and its plugin design also helps we develop our structure too"*). A design
  study, one session, in the shape DOCS1 took: read dsh's `packages/<group>/<pkg>` cut (the groups are
  capability seams — shell, subprocess, sandbox, fs, session, subagent, hooks, acp…), its **capability
  seam** rule (a seam is complete only with all three roles — Service Definition, Service Provider,
  Consumer — never one alone), **registrations are effects** (every contribution returns its disposer,
  so unloading unwinds cleanly), **plugins, not loop changes** (new behaviour goes on documented
  extension points; changing the loop updates the architecture map), and profiles/bundles as ordered
  composition layers. Then hold Daoris's own structure against it: the driver's adapter seam (about to
  gain a protocol door under D53 — is it a seam with three roles, or a provider with no definition?),
  the desktop's IPC modules, the service's judgement classes, the CLI's management class, and the
  three-twin files (remotes map, harnesses, driver.json). Deliverable: a comparison note naming what
  Daoris adopts as *pattern* — D52 as amended and D53 already decline the runtime (Cordis) — and where
  its current cut disagrees with its own seams; anything load-bearing becomes a D entry. **Read the
  owner's sentence as pattern, not runtime, until the owner says otherwise**; the reading is recorded
  in the handover for confirmation.

- [ ] **CANON5 — i18n en/zh parity as canon: the two-repository bar is met, and the budget no longer
  blocks it** (unparked by CANON7, 2026-09-21). The bilingual sibling carries the rule and the gate;
  Daoris carries the same gate (`scripts/i18n-check.mjs`, adopted from it deliberately — D42). Two
  repositories, one lesson: a missing translation "works" in English and is discovered by the first
  reader it fails. There is now room for roughly one substantial rule, **which is exactly the budget
  this would spend** — so the question it must answer first is the one the room does not settle:
  whether this belongs in the always-loaded core at all, or as **pack knowledge for web
  repositories**, which is where it most likely belongs. A rule every repository loads on every task
  to govern a concern only some of them have is what the pack tier exists to prevent.

- [ ] **HARNESS2 — a `codex` session adapter. Absorbed into ACP3 by D53**, and kept here only so the
  name resolves: it is `@agentclientprotocol/codex-acp` on the protocol door, not a hand-built
  `ISessionAdapter`. SES3 already made codex manageable as a TOOL (`daoris harness` knows its
  installer, its `CODEX_HOME` seam and how it reports a login, all verified against the real binary).
  The one thing the ACP route does not settle for free is **the D37 boundary in codex's own
  vocabulary** — its `acceptEdits` equivalent is established from what the adapter exposes as a mode,
  never guessed. It lands the way `claude-code` did: the stub proves the loop, a real driven run
  proves the harness. **Not HARNESS1** (a second harness *layout*, a doctrine question).

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
