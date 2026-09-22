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
**274 CLI tests, 262 service, 294 driver, 77 desktop modules, 429 web unit, 14 Playwright**, 57
devkit, 56/56 release rehearsal, **180/180 family rehearsal** (it names its own phases when you run
it), 9 devkit gates. Canon: 8 core rules, 5 knowledge documents, 5 skills, 6 packs. Always-loaded
core is **21,817 of 26,000 bytes** — a span in `AGENTS.md` since D59 — and **advisory rather than
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
*because* of something the workspace provides.

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

**What is open**, in the Backlog below, in the order worth doing: **DEPLOY2** (nothing gates the
deployed artefact — it would have caught two of the four deployment defects), **SURF11** (layout
toggles; 🔴 needs the panel and dock state hoisted out of `WorkFrame` before it needs designing),
then HELP3, TOOL5, DEVKIT3, CANON5. **Two are decisions, not work**: PLUG2 reopens D4's *"core
installs with no opt-out"*, and DEPLOY4 asks whether per-install UI state joins `~/.daoris` — and
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
- **Verify before claiming done, always:** `npm run verify` (typecheck + CLI 274 + `check` + doc
  budgets + version agreement),
  `dotnet test src/Daoris.Service/Daoris.Service.Tests` (262),
  `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests`
  (294), `dotnet test src/Daoris.Desktop/Daoris.Desktop.Modules.Tests` (77), `npm run rehearse:family` (180/180), `npm run test:web` (429 vitest + 14 Playwright).
  🔴 **Stop a running shell and its host first** — an orphaned `daoris-knowledge-http` holds the
  build's own assemblies, which reads as a broken gate and is a lock (`npm run desktop -- kill`). If a `bin`-driven gate is red
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

- [x] **ACP3 — dsh and codex as configurations.** ✅ **done 2026-09-22**, and **HELP2 and HARNESS2
  close with it**. Both adapters, both toolchain entries, the profile Daoris owns, and the skills
  root. 🔴 **The finding that shaped it: the D37 posture lives in three different places.** Claude
  Code names it `acceptEdits` and Codex names it `agent` — both ACP modes, different ids — and dsh's
  `session/new` carries **no modes at all**, so its posture is `DSH_PERMISSION_MODE=workspace-write`
  in the environment. The mode was a constant inside `AcpSession`; it is the adapter's now, and null
  means *this wire carries no posture* rather than a licence to guess a neighbouring one.
  `docs/2026-09-22-acp3-probe-evidence.md` is the record — every claim established keylessly against
  an installed artefact at an exact pin, and it says which facts came from a live wire and which from
  a shipped bundle. **What waits is a driven run per harness**, exactly as ACP2's does.

- [x] **ACP4 — the MCP servers the door hands over.** ✅ **done 2026-09-22.** The composed target
  tells every session to claim and close its quest over its own connector; the pipe door leans on the
  repository’s own `.mcp.json`, which an adopted repository may not have and which the driver may
  never reach in and write. The protocol carries the wiring on `session/new`, so a driven session is
  handed its voice with **nothing written anywhere**. A machine with no host still drives and says so.
  Gated in the family rehearsal from the AGENT’s own side (180/180).

- [x] **HELP2 — skills reach one harness only.** ✅ **done 2026-09-22 with ACP3.** `customSkillDirs`
  naming `.claude/skills` in the patch layer Daoris writes into a dsh home it created — a root, not a
  conversion, and nothing on an adopter's disk changed. 🔴 **The path is relative and that is
  load-bearing**: dsh resolves the default project roots per session `cwd` but resolves
  `customSkillDirs` **once, at construction, against the process's own cwd**. The driver spawns one
  process per tree (D51), so relative lands right and an absolute path in a shared home would pin
  every session to whichever tree was first.

- [ ] **HELP3 — one guard, every harness.** `dsh-hooks-claude-code` runs an existing `hooks.json` in
  Claude Code's dialect and `dsh-hook-protocol` makes the Codex bridge behave identically, so a guard
  written **once** — refuse a write outside the session's tree (D51), refuse a push (D37) — runs on
  all three. Probe 4 found the Windows trap. Held behind ACP4 and CANON8: a guard is worth less than
  the doctrine it enforces arriving at all. (HELP1 became CANON8; D59 has it.)

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

### The first deployment — what running outside a checkout found (owner, 2026-09-22)

> *"setup and deploy the desktop version to \<install\> and we can drive and log it properly for some
> real case study and testing"* — and then, looking at it: *"currently it just bit messy"* about the
> install folder, and *"the application topbar you can take more example from application like
> vscode"*.

**Done and in the archive**: the deployment itself, `tools/desktop-publish.mjs`, the install layout,
the command center, the scrim, and four defects only deploying found.
`docs/2026-09-22-first-deployment-case-study.md` is the record. **What is left is one decision and
one gate.**

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

- [ ] **DEPLOY2 — nothing gates the deployed artefact.** `rehearse` installs and drives the CLI
  *package*; both rehearsals otherwise run inside the workspace, where the workspace build and the
  console's own encoding paper over exactly the two defects this deployment found (the host the
  locator could not see, the transcript that was not UTF-8). The work is a gate that **publishes the
  shell to a scratch folder and runs it from there** — the desktop's `rehearse`. It needs no model
  and no account: bringing the window up, finding its host and writing one non-ASCII line to a
  transcript would have caught both.

- [x] 🔴 **DEPLOY3 — there is no credential management surface.** ✅ **done 2026-09-22.** The Machine
  view could list a harness's profiles and log into one, and could not **make, choose or un-point**
  one — those three verbs existed only in the CLI, so the owner's *"there is no credential management
  location"* was literally true. **D50 violated in the direction nothing checks**: the rule is written
  "whatever a screen can set, a terminal can" and the converse had no test anywhere.
  `profile-add|remove|default` over `HARNESS_ACTION`, and the roster's own form. 🔴 **"Forget", not
  "delete"** — it stops this machine pointing at a profile and removes nothing, because the directory
  holds a credential the harness put there; the word on the button is the word for what happens, in
  both doors. Daoris manages directories and names, never secrets.

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

### Plugins — what the study left (owner, 2026-09-22 → ARCH1)

`docs/2026-09-22-plugin-design-study.md` is the contract. **Two standing decisions are NOT reopened
by any of these**: no adapter registry (D23/D24/TOOL5 — the ACP door is already that answer, and a
protocol beats a binary API), and no plugin runtime for the surface (D52).

- [x] 🔴 **PLUG1 — a pack cannot say which canon it needs.** ✅ **done 2026-09-22.** `apiVersion` on
  every `pack.json`, read **before a single file is planned** — taken from the neighbouring
  application's plugin manifests, which have carried an integer all along. A pack from a newer canon
  is refused **naming both numbers**, because "incompatible" alone sends a person to guess which side
  is behind. **Absent means 1**, so every pack written before the field keeps working: the field is
  how a pack opts into saying something, never a wall in front of one that never spoke. A non-integer
  is a malformed manifest rather than an old one, and errors. Raise the number only when a pack
  written for the new shape **cannot work** on the old one — one that goes up on every change teaches
  people to ignore it.

- [ ] **PLUG2 — a pack cannot disable or override what core installs.** dsh composes profiles as
  ordered layers where a layer may switch a row off (`- id: x` / `disabled: true`); Daoris's manifest
  `packs: []` is a flat set with no precedence. ⛔ **Decide before building**: this reopens **D4's
  "core installs with no opt-out"**, which was a deliberate choice about doctrine rather than a
  limitation. The owner's call, and the study says so rather than assuming it.

- [x] **PLUG3 — three extension points and nobody is told.** ✅ **done 2026-09-22.** The README has
  an *Extending it* section naming all three — a pack, a gate row, and speaking ACP — what each may
  add, and what is deliberately not extensible (the views, D52), pointing at the study for the
  reasoning. 🔴 **It cost its own budget lesson**: the section put the README 225 words over, and the
  answer was to relocate detail the design docs already hold (the harness paragraph, the `--force`
  one) rather than raise the ceiling or shave the new section to uselessness (D28).

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
  correct the devkit README's "it runs this repository's own gates", which today it does not.- [x] **ARCH1 — dsh's domain separation and plugin design as the example for Daoris's own
  structure.** ✅ **done 2026-09-22**, widened by the owner to include a second reference
  (*"you might check how yaorin did"*). `docs/2026-09-22-plugin-design-study.md` is the note.
  🔴 **It reordered its own question**: Daoris already has **three** extension systems — canon packs,
  the declared gate list, and the ACP door — and none of them is called one. So the finding is not
  "Daoris needs plugins" but "two of the three are undocumented and one manifest is missing a
  version field". The Cordis runtime is **declined again** on the evidence already gathered, and
  *registrations are effects* is adopted as the rule for anything ever loaded at runtime. PLUG1–3
  below are what it left.

- [ ] **CANON5 — i18n en/zh parity as canon: the two-repository bar is met, and the budget no longer
  blocks it** (unparked by CANON7, 2026-09-21). The bilingual sibling carries the rule and the gate;
  Daoris carries the same gate (`scripts/i18n-check.mjs`, adopted from it deliberately — D42). Two
  repositories, one lesson: a missing translation "works" in English and is discovered by the first
  reader it fails. There is now room for roughly one substantial rule, **which is exactly the budget
  this would spend** — so the question it must answer first is the one the room does not settle:
  whether this belongs in the always-loaded core at all, or as **pack knowledge for web
  repositories**, which is where it most likely belongs. A rule every repository loads on every task
  to govern a concern only some of them have is what the pack tier exists to prevent.

- [x] **HARNESS2 — a `codex` session adapter.** ✅ **done 2026-09-22 with ACP3**, as
  `@agentclientprotocol/codex-acp` on the protocol door rather than a hand-built `ISessionAdapter`.
  The one thing the ACP route did not settle for free — **the D37 boundary in codex's own
  vocabulary** — was **established, not guessed**: the wire offers `read-only`, `agent` and
  `agent-full-access`, and `agent` is the posture. 🔴 It is **stricter** than `acceptEdits` rather
  than equivalent, because it runs with `networkAccess: false`. A driven run still proves the
  harness. **Not HARNESS1** (a second harness *layout*, a doctrine question), which stays open.

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
