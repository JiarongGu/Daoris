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

**All five artefacts exist and are built, and all three parts of D45 with them.** Fifteen commands,
**446 CLI tests, 473 service, 646 driver, 126 desktop modules, 948 web unit, 21 Playwright**, 73
devkit, 66/66 release rehearsal, **279/279 family rehearsal** (it names its own phases when you run
it), **39/39 deployment rehearsal** (D60), 5 universal devkit gates over this repository (DEVKIT3). Canon: 8 core rules, 5 knowledge documents, 5 skills, 7 packs. Always-loaded
core is **22,171 of 26,000 bytes** — a span in `AGENTS.md` since D59 — and **advisory rather than
enforced** (D54: a fact gates, a judgement reports). The answer to a full budget is still splitting
principle from detail (D28).

**The remote exists** (D47/DRV5) and **the driver drives** (D45/D46) — both built, both proven end
to end, and both described in full in `docs/task-archive.md` rather than here. The shapes that still
bind: what may leave a machine is two manifest declarations, silence meaning local; the quest state
machine is the only lock and outside sessions stay first-class; and a session's record is concluded
from its exit code and its quest's state, never from what it said about itself (D46 §4).

The service is **deployable** (D36) and **ships as executables** (D43): `npm run publish:service --
--install` lands both hosts self-contained under the home's `bin/` and prints the ready `.mcp.json`
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
configurations), D57 (the toolchain), CANON8/D59 (the instruction file), ARCH1 (the plugin
study), and D68/D69 (the remote as a git remote, SYNC0d–SYNC6c). Nothing is pushed or published,
and a release is still blocked on REH1.

🔴 **The live arc is D76, the conversation** (owner, 2026-09-25), taken in the order under *The
reference gap* below.
- **Landed:** CONV1 (the record), CONV2 (the view), CONV3a (Claude Code's `stream-json`), CONV3b
  (chats on the protocol door), CONV4a (stopping a turn, one queue on both doors), CONV4b (the
  composer's turn, queue and drafts) and CONV4c (attachments), with the fixes the looks found. All
  are in the archive under 2026-09-25.
- 🔴 **Next: REV3, the full review of code and docs the owner asked for (2026-09-25)**, under
  *The review the owner asked for* below. It comes before anything else.
- **Then:** **CONV4d, `@` a file in the session's tree** — measured with CONV4c: both doors expand
  `@path` text themselves, so the work is the completion list and the bridge call behind it.
- **How each landing is checked:** TDD, the gates, then a look on the window with a real session.
  Real sessions on this machine's Claude Code account are authorised (2026-09-24).
- **The scratch machine:** its `driver.json` points `claude-code-acp` at the ACP adapter the dsh
  probe installed under `_fixtures/dsh/npm` (0.79.0), which is how a protocol-door chat is looked
  at without installing anything.
- **Open beside it:** FLAKE1 (an intake test, about 1 run in 20), DEPLOY3 (the artefact gate
  holding a chat at close) and TEST1 (the Playwright worker's `0xC0000409`, seen a second time).

🔴 **Daoris is DEPLOYED and running as an installed application** (2026-09-22), which is new and
changes what "works" means. `npm run publish:desktop -- --to <folder> --service` installs it: one
`daoris-desktop.exe` at the root, binaries under `app/`, and **the Daoris home in `data/`** (D63,
2026-09-23: `DAORIS_HOME` is the one seam, set by the install for itself and once for the account;
nothing of Daoris's lives under the user profile, and a writer with no home refuses). **Starting it
starts the driver loop.**
`docs/2026-09-22-first-deployment-case-study.md` is the record, and it is the first document to read
before touching the desktop — four defects were invisible from inside the workspace, three of them
*because* of something the workspace provides. 🔴 **The second deployment (2026-09-23) found
thirteen more, every one in `docs/FIX-LOG.md` under that date** — three that hid the page itself
(an older host spawned over the install's own, a stale page from the WebView2 cache, an adopted
host serving another page), then what looking at each surface on real data turned up. **Republish
and LOOK after every surface change, in both themes** — that is how all of them were found, and
`document.scripts` names which bundle is live.
🔴 **Plugins exist** (D64, 2026-09-23, `docs/2026-09-23-plugin-design.md`): a folder under the
home's `plugins/` that declares harnesses on the ACP door, **hands every session its MCP servers**
(INT1 — `examples/plugins/browser` declares the Playwright MCP, beside the knowledge host, never in
its place), and may speak from a process of its own; `daoris plugin` and the Machine view's Plugins
card are the two doors, and the family rehearsal's phase 18 drives all three. No code from a plugin
loads into any host, ever.

🔴 **A harness action's end is news** (2026-09-23, `HARNESS_ENDED`): a login waits on a person in a
browser and an install on a network, longer than the bridge's thirty-second request — the request
that waited with it had timed out and closed the panel on a login still running. The request now
answers once the process has started; `HARNESS_INPUT` answers its prompt and `HARNESS_CANCEL` stops
it. Signing in sits on the account's row (`SignIn`), tooltips follow the editor's rules (`Tip`), and
no spawn opens a console window — all three were the owner's asks that day, all three seen on the
installed shell, all three in `docs/FIX-LOG.md` or `docs/2026-09-19-platform-ux.md` §4.

**The pipe door's trust flag is settled** (DEPLOY1 and ACP2, both in the archive, 2026-09-24): the
driver holds an untrusted folder and says so, Daoris asks per folder and then writes the flag (D73),
and the protocol door was proven on a real login (17/17). What D73 leaves unmeasured is TRUST2.

**The testbed is live and wired** (`tools/testbed.mjs --root <folder>`): three repositories in
workspace `testbed`, each with its own `.mcp.json` and trust settings, registered against the running
host. Quest `#7786da` is open and retried (three strikes behind it), and every tick **holds** it on
the trust flag, which the Overview row now says under the quest. Two branches there hold what the
failed sessions produced.

🔴 **The direction is the DESKTOP, and development happens against the install** (owner, 2026-09-22
→ **D62**): the shell carries the platform, runs the driver loop, hosts the machine's service, and
is the only surface that can reach a machine-local fact at all — most of what Daoris can do, only it
can do. **Look at the real thing, not the fixture**: `npm run desktop -- run --install <dir>` starts
the published application with a debug port so `shot`, `eval` and `click` reach it (the published
app opens none by itself). The scratch loop has one circle, one account and two example
repositories, so every UI judgement made against it is a judgement about a machine nobody has.

**The looking pass is complete** (2026-09-22/23): every surface read once on the real machine, each
finding what the fixture could not show, every one in `docs/FIX-LOG.md` (the widest: 中文 search had
never worked in the SQLite index, now cut into bigrams). The next pass is the same pass after the
next change.

**The decisions that were waiting are made and archived**: ACP2 and DEPLOY1 (D73), PLUG2 (D71),
HELP3 (folded into PERM1) and DEPLOY4 (D63). TOOL5 is a trigger, not work (its row says why). Three
**held** items sit at the bottom; do not pick one up until its trigger has arrived.

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
- **Every landing is a local commit, unpushed by the owner's standing call**, and `git log` is the
  reviewable record. Work left behind by an arc goes in the backlog as an item, never as a
  handover sentence — that is how work quietly stops being work.
- **Every capability has TWO doors, and anything new inherits both.** The desktop's IPC and
  `daoris-driver chat` run the same `ChatRunner`; `daoris agent` and `daoris driver` do from a
  terminal what the roster and the checkboxes do from a screen. What is shell-only is the STREAM, not
  the capability (D47 §4 protects transcript-class material, and D50 forbids stranding a capability on
  a screenless machine).
- **The toolchain's rules, in one place** (SES3, interactive design §4): silence means the harness's
  own configuration home, so the feature is purely additive; login state is asked of the harness, has
  three values, and only a definite *out* refuses; a cached refusal is re-asked before it is given; the
  profile NAME never leaves the machine while the harness VERSION does; a profile IS a directory, made
  by signing in and named by who signed in, and removing one deletes it (D66 §3). The CLI's *managed*
  set is deliberately not the driver's *adapter* set.
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
- **The owner's newest direction, the menus by domain (D75), is built and closed** (FRAME1–FRAME5).
  Beyond it, surface work is whatever the next look at the install finds (POLISH2–POLISH4 and the
  FRAME items are in the archive). 🔴 **The install is empty, so look with data too**: POLISH4 read
  the scratch shell over the example family (`npm run desktop -- run`), where a parked session,
  intakes, asks and an unadopted repository exist, and found nineteen things the install could not
  show. 🔴 **The owner authorized real sessions on this
  machine's Claude Code account, 2026-09-24**. ACP2 used seven of them to reach 17/17, then INT4f
  (16/16), INT4j (17/17) and PERM2b (14/14) one each. **The owner's to spend or attend**: TRUST2's first grant (the permission check refused an
  agent's write to the account's `.claude.json`), AGT2c (two downloads, not authorized) and INT6.
  **Six wait on a trigger**: TOOL4 on TOOL3's transcripts; TOOL5, CANON2 and HARNESS1 on a
  repository naming what it wants; REH1 on a captured recurrence; PLUG7 on a plugin asking for a
  service-side point. TEST1's second sighting arrived (2026-09-25), so it is open. **A new direction from the owner outranks all of them.**
- **The shell has a dev loop (DEV1):** `npm run desktop -- doctor|build|run|shot|eval|click` — not a
  gate, and `eval` is the one instrument that reaches the bridge-attached half.
- **Verify before claiming done, always:** `npm run verify` (typecheck + CLI 446 + `check` + doc
  budgets + version agreement + the devkit's universal gates),
  `dotnet test src/Daoris.Service/Daoris.Service.Tests` (473),
  `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests`
  (646), `dotnet test src/Daoris.Desktop/Daoris.Desktop.Modules.Tests` (126), `npm run rehearse:family` (279/279), `npm run test:web` (948 vitest + 21 Playwright),
  and — when the desktop, the publish or the locator moved — `npm run rehearse:deploy` (39/39), which
  publishes the shell to `_fixtures/` and drives **that**. 🔴 **Phase 4 asserts the started host is
  the INSTALL's own** — the gate plants a decoy under the scratch home's `bin/` itself since D63, so
  every machine can express the defect.
  🔴 **Stop a running shell and its host first** — an orphaned `daoris-knowledge-http` holds the
  build's own assemblies, which reads as a broken gate and is a lock (`npm run desktop -- kill`). If a `bin`-driven gate is red
  while `npm test` is green, suspect a stale gitignored `dist/` first (FIX-LOG) — though `postpack
  --clean` and the rehearsal's leftover check now remove and assert that case away.

## Backlog

**The D48/D49/D50 arc is closed** — all eight items are built and in the archive, and REV2 reviewed
them. **SURF1 designed the next direction and is in the archive too**, and **DSH1 evaluated dsh** (in
the archive; its decision is **D53, accepted**). **Twenty rows are open**: REV3, the owner's
review, first; D76's six and its held file tools; three leftovers (FLAKE1, DEPLOY3, TEST1); three on
the owner (TRUST2, AGT2c, INT6); and six on a trigger (see *Handover*). Every closed one is in `docs/task-archive.md`, and this
file holds no ticked rows, by the `task-lifecycle` rule it also ships.

🔴 **D68's SYNC arc, D67's buildable items and the regular task's buildable items are closed**
(2026-09-24, in the archive). Each landing is one session-sized item, TDD, gates green, moved to the
archive.

### The review the owner asked for (owner, 2026-09-25) — FIRST next session

> *"next session let's do a full code review include docs"*

- [ ] **REV3 — a full review, code and docs.** REV2 is the precedent (archive, 2026-09-20): by
  *reading* rather than re-running, it found two defects no gate could see. Gates prove what they
  assert; a review looks for what nothing asserts.
  - **Scope, every artefact:** `src/Daoris.Cli`, `src/Daoris.Service`, `src/Daoris.Devkit`,
    `src/Daoris.Web`, `src/Daoris.Desktop` (driver, modules, app, headless host), `tools/`, `canon/`
    and `examples/`.
  - **Scope, docs:** `CLAUDE.md`, `AGENTS.md`'s local rows, `README.md` and each artefact's README,
    `TASKS.md`, `ROADMAP.md`, `CHANGELOG.md`, `docs/DECISIONS.md`, `docs/FIX-LOG.md`, and the
    design documents under `docs/`.
  - **Code, for correctness first:** concurrency, error paths, what crosses a boundary (D47 §4), a
    refusal that reaches nobody, a twin set that drifted. Weigh the newest code most:
    - D76's landings, CONV1–CONV4c: `ChatTurns`, `ChatRunner`, `ChatFiles`, the stream-json mapper,
      the event record, and the page's conversation;
    - the D72–D75 permission, proposal and menu work.
  - **Docs, by `claims-need-checks`:** behavioural prose checked against the code, never against the
    design. Look for stale counts and claims, *as built* notes that diverge, duplicated rules,
    status in prose, and history outside the records that hold it (the `post-feature` prose pass).
  - **Method:**
    1. Run the discovery skills, then scope by artefact.
    2. Verify every finding before reporting it: a concrete failure, or the line that is untrue.
    3. Land each confirmed defect as a fix (TDD, its own commit, a FIX-LOG entry when it is a
       defect), or as a backlog row when it is not session-sized.
    4. The archive gets the findings, as REV2's entry has them.
  - A fan-out, one reviewer per artefact and one for the docs, needs the owner's go-ahead for its
    scale.
  - **Already known, so not re-found:** TEST1, FLAKE1, DEPLOY3, REH1 and UX1's written findings.

### The protocol door — ACP (D53, accepted 2026-09-21)

**Settled by DSH1's evidence** (`docs/2026-09-21-dsh-evaluation.md`): the adapter seam grows a door
that holds a session over the **Agent Client Protocol** beside today's pipe, and every harness reached
that way is a *configuration* of the door. **ACP1 has landed** (2026-09-21, in the archive): the seam
carries a `Wire`, `AcpSession` speaks the protocol, and the rehearsal drives a quest to done over it
with no model in the gate — including the permission refusal from both sides. **ACP4 and ACP3 have
landed too**, so the door now carries four configurations — `acp-stub`, `claude-code-acp`, `codex-acp`
and `dsh`. **ACP2 is proven too** (2026-09-24, 17/17 on a real login, in the archive), so every ACP
item is closed.

🔴 **The posture is the adapter's, in that adapter's own words** (ACP3): `acceptEdits` to Claude Code,
`agent` to Codex, and to dsh not a wire concept at all — `DSH_PERMISSION_MODE=workspace-write` in the
environment, because its `session/new` carries no modes. A new configuration of this door states its
own, and **null means the wire carries none** rather than a licence to guess a neighbouring mode.
`docs/2026-09-22-acp3-probe-evidence.md` is how each was established, and which are bundle rather than
wire evidence.

### What an agent may do — permission scopes (owner, 2026-09-24 → D72, D73)

PERM1–PERM4, PERM2b and DEPLOY1's second half are in the archive, with HELP3 and INT3b folded in.
Measured: rules and hooks Daoris hands over at spawn reach an untrusted session on both doors, and a
repository's own allow-list does not (`docs/2026-09-24-deploy1-acp-trust-evidence.md`).
- [ ] **TRUST2 — what D73 leaves unmeasured.** Whether Claude Code honours a trust key Daoris wrote
  as its own; whether a trusted parent covers a child, since the hold matches the exact folder; and
  whether the screen should offer a grant before any hold exists (INT6's onboarding). The first
  needs one grant, and a grant is the owner's.

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

**TOOL2 and TOOL3 are in the archive**: the pin (*explicit command → managed pin → `PATH`*; a pin
nobody installed refuses) and usage at each session's high-water mark (🔴 absent is never zero).

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
`docs/2026-09-22-first-deployment-case-study.md` is the record. **DEPLOY1 is closed** (D73, in the
archive); what it left unmeasured is TRUST2.

### Agents, their accounts, and the map (owner, 2026-09-23 → D67)

**D67** holds the owner's answers; `docs/2026-09-23-agents-direction.md` the asks and what was
checked. AGT1, AGT2a, AGT2b, AGT3, AGT3b, AGT4, AGT6, AGT7, MAP2, MAP1a, MAP1b, MAP3a, MAP3c,
MAP3d and MAP3e are archived.

- [ ] **AGT2c — one real vendor-channel pin of each, observed:** that Claude Code stays at its version
  under `DISABLE_UPDATES`, and that a pinned Codex outside its own layout takes no update action (the
  evidence says so; nothing measured it). Spends two real downloads — the owner's call.

### The regular task — an ask becomes quests (owner, 2026-09-23 → D65)

`docs/2026-09-23-intake-design.md` is the contract. A sentence with files and links enters at the
workspace; an **intake session** (the harness carries the model, D24) reads the ticket, decides
the owning repository from the declarations, and publishes quests; a plugin-declared **browser
server** puts testing in the session's hands; **`then`** chains quests, and the driver is the
engine. Nothing new runs a model and nothing new orchestrates. **INT1, INT2, INT3, INT4a, INT4b,
INT4c, INT4d, INT4f, INT4g, INT4h, INT4j and INT5 are in the archive.** INT4 was split in three (D65 as amended): the ask and its floor, the session, the
screen.

- [ ] **INT6 — onboarding the named workspace.** `import`, a declaration per repository, the first
  real ask — owner present, adoption playbook.

### The remote as a git remote (owner, 2026-09-23 → D68)

**Closed 2026-09-24.** `docs/2026-09-23-sync-design.md` is the contract: every verb commits locally,
and sync is fetch, rebase, push. SYNC0d and SYNC1 through SYNC6c are in the archive. Nothing here is
open; a new sync item is built against that design, not against this heading.

### Plugins — a folder that declares, and may speak (owner, 2026-09-23 → D64)

`docs/2026-09-23-plugin-design.md` is the contract; `docs/2026-09-22-plugin-design-study.md` the
study it grew from (PLUG1 and PLUG3 are in the archive). **Two standing decisions are NOT reopened**:
no adapter registry (D23/D24/D57 — a declared harness is a file on one machine, and the ACP door is
the seam), and no plugin runtime for the surface (D52). **No code loads into a host, ever.**

- [ ] ⏸ **PLUG7 — service-side points** (held): the same wire reaches the knowledge service when a
  plugin somebody writes asks for a point there. PLUG4–6 are in the archive.

### The menus are the setup domains (owner, 2026-09-24 → D75)

> *"I think we can use the topbar menu to have more different domain of setup, this is closer to ide
> logic, and I dont see workspace anymore?"*

**Closed 2026-09-24.** `docs/2026-09-24-menus-design.md` is the contract, the owner chose each shape
from options (D75), and FRAME1–FRAME5 are in the archive. Nothing here is open; a new menu or
Settings domain is built against that design, not against this heading.

### The reference gap (owner, 2026-09-24)

> *"lets keep push the ui/ux design and I still think this does not meet the reference projects
> capbility"*

`docs/2026-09-24-reference-gap-study.md` measured it: the attended session's centre is a record, not
a conversation, because ACP's structure is flattened to text lines before the bridge, a chat does not
use a structured wire at all, and nothing is kept for the page across a restart. A chrome pass cannot
close that. **The owner chose the conversation model and every extra → D76** (2026-09-25), with
*"you should check screen by screen and all ui ux logic"*. Take them in order; each is one landing,
TDD, looked at on the window, and the ones that touch a real session use one (authorised
2026-09-24).
**CONV4 was split (2026-09-25)**; CONV4a, CONV4b and CONV4c are in the archive.
- [ ] **CONV4d — `@` a file in the session's tree.** Measured: both doors expand `@path` text
  themselves, so the wire needs nothing. The work is the completion: a bridge call listing the
  tree's files, and the composer offering them after `@`.
- [ ] **CONV5 — meters.** A context ring under the composer and per-turn usage, from the usage the
  wire reports; absent is never zero.
- [ ] **FRAME6 — the frame.** A resizable, collapsible rail (264–420px, 56px strip) and a resizable
  dock (45% default, 70% cap) with tabs per session, deterministic close (components §3a).
- [ ] **RAIL1 — the list.** Search sessions by name and by content, and a row menu.
- [ ] **REVIEW2 — review.** Highlighted diffs, split or unified.
- [ ] **UX1 — screen by screen.** Every surface, every state (empty, loading, error, long, 中文,
  dark), every piece of interaction logic (keys, focus, what a click opens, what survives a reload),
  against the reference and D41. Written down as it is found, and fixed. **Already found, to settle
  there:** a session waiting on the person wears declined's red (`Dot tone="parked"`) in the rail,
  the band and the ask card, while the map uses the warn tone for the same fact; one hue for
  "waiting on you" everywhere. The monitor's tiles are console-only and say *Nothing said yet* for a
  session whose conversation is kept. The protocol door's console writes each streamed chunk of a message as its own line
  (`…document` / `, named in the README.`), so the raw view breaks words across lines, driven
  sessions included. The native door renders a whole message once, and this one should too. An
  agent's one-item-per-line answer renders as one paragraph, because Markdown makes a single
  newline a space (seen on the window, CONV4b).
- [ ] ⏸ **Held, after the conversation:** a file tree and document preview in the dock (when a tool
  card wants to open a file); a terminal (design §6's trigger).

### Open — the arc's leftovers, in the order they are worth doing

- [ ] **FLAKE1 — an intake test failed once in about 20 full driver runs.**
  `IntakeTests.An_ask_with_an_intake_harness_is_answered_by_a_session_that_publishes_onto_it`: the
  intake opened, and its stub agent published nothing onto the ask (2026-09-25, under a loaded full
  run). It passed 10 runs in a row after. Its assertion now carries the session's transcript and the
  tick's events, so the next failure says why: a `fetch` to the stand-in that failed, an exit, or
  something else. A gate that fails one run in twenty is a gate people learn to re-run, which is how
  a real failure gets waved through.

- [ ] **DEPLOY3 — the artefact gate holds a chat open at close.** The fix for a chat left `working`
  when the shell closes (FIX-LOG, 2026-09-25) is held by driver tests at the runner. But the defect
  that survived them lived in the shell's own shutdown order, and only the window saw it. The
  deployment rehearsal closes the installed shell (§6) with no chat open, and it has no way to open
  one: a chat starts over the bridge, and the rehearsal has no debug port. Give it one (the
  `run --install` loop already does), open a stub chat, close the shell, and read the record for
  the close's note, never the sweep's.

- [ ] **TEST1 — the Playwright suite aborts a worker with `0xC0000409`: seen twice now.** The
  second sighting was its trigger. Both runs died with `worker process exited unexpectedly
  (code=3221226505)`, Windows `__fastfail`: no output, no stack, no WER entry.
  - 2026-09-21, during SURF2, mid-suite. The identical run passed after.
  - 2026-09-25, during CONV4b's gate, at test 4 (*a chain moves on when its quest closes done*),
    0 ms in, after three passed; the other 17 did not run. CONV4b changed no e2e path or host code.

  **A family sibling documents the same abort** at about 1.5% of e2e runs, with a standing
  reproducer (spawn a server, poll it, kill it: about 1 in 300 rounds, 4-way concurrent). The row
  used to say *capture the Playwright HTML report*, but the config runs the list reporter, so no
  such report exists. The next step is a capture that can exist: an HTML or JSON reporter in
  `playwright.config.ts`, then the comparison with the sibling's notes. Do not tune timeouts on two
  data points either.

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
