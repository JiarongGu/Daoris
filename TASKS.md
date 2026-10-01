# Daoris (道衍) — Active Task Backlog

> **This file holds OPEN tasks only.** A finished task is **removed from here and appended to
> [`docs/task-archive.md`](docs/task-archive.md)** with its date and outcome — never ticked in place.
> `CHANGELOG.md` is the release-facing log; the archive is the per-task record. The contract is
> `docs/2026-08-04-daoris-design.md`; the forward sequence is [`ROADMAP.md`](ROADMAP.md).

**Goal:** one canonical set of agent-facing rules and knowledge, materialized into every repository in
the family, kept from drifting, and improved from wherever the improvement was found — and, as of D45,
**Daoris as the driver**: the workflow manager that triggers and coordinates the agent sessions doing
the family's work.

**Every arc is closed and in the archive**: the driver (D45), the remote (D47),
workspaces, the interactive surface and management parity (D48–D50), the working surface (D51–D56),
the protocol door (D53), the toolchain (D57, bar TOOL4 and TOOL5), the instruction file (D59), the
first deployment (D60–D63), plugins (D64), the remote as a git remote (D68), permission scopes
(D72–D74), the menus (D75), and the conversation (D76, bar its held file tools), closed by UX5's
screen-by-screen pass. [`docs/README.md`](docs/README.md) names each arc's contract. Two
reviews read the code rather than re-running it, REV2 and REV3, and CLEAN1 settled REV3's cleanup
lists.

## State

**Counts, and this is their one home:** sixteen commands, **784 CLI tests, 778 service and 47 HTTP host, 1786 driver,
466 desktop modules, 80 devkit, 1911 web unit, 21 Playwright**, 92/92 release rehearsal, **312/312
family rehearsal** (it names its own phases when you run it), **70/70 deployment rehearsal** (D60),
and 5 universal devkit gates over this repository (DEVKIT3). Canon: 8 core rules, 5 knowledge
documents, 5 skills, 7 packs. The always-loaded core is **22,672 of 26,000 bytes** — a span in
`AGENTS.md` since D59 — and **advisory rather than enforced** (D54: a fact gates, a judgement
reports).

Nothing is published; development runs at `0.0.x`. **Adoption by other repositories is the owner's call
and happens when Daoris is ready** — it is not tracked here. Lyntai stepped off the tool 2026-08-17
(owner-requested), so the live consumer count is zero and Lyntai is not quest-addressable until it
re-adopts. The Shenora rehearsal keeps and does not expire: 6 collisions, 2 twins to retire, local
mechanics drafted at `docs/adoption/shenora-repo-mechanics.md`, budget 40,000, `check` clean at
38,782 bytes, with `web-webview` and `durable-jobs` ready for it.

## Handover — where a fresh session picks up

🔴 **Where 2026-09-30 left it.** The owner's direction: *Daoris is the master development tool, doctrine
and knowledge sharing*; their ticket work runs through the local Daoris, and Ask Daoris should reach
everything. **The day's rows landed** (all in the archive): HELP4–HELP5 (Ask Daoris honest and faster),
LOG1 whole (the machine log, D94, read back at two doors and summarised by `tools/usage-report.mjs`),
SHEN1 (Shenora 0.18), USE1a–f (what the window showed, one defect for the overflow and the side bar),
QUEST1 (delete, D95), CONSOLE4 (a terminal, D96), SETUP1 (the setup guide, D97), AGT6 (model and
effort, D98), CHR8 (one Chromium, D99), BRW7–8 (the browser's door, link routing, who drives), HELP6
(Ask Daoris proposes every new door, tried with a real helper), WORK1, DEPLOY5 and HTTP1 — most built
by subagents in their own worktrees and merged, rehearsed and looked at on main. Then HELP7, LOG2
(the host writes its stop), WSR4 (D100: a landing plugin pushes and opens the pull request, two inert
examples), HELP8 (Ask Daoris proposes one), a narrow Settings row that stacks, PLUG8–9 (Daoris makes
and installs plugins, D101, D103), WSR5 (landed branches cleaned and handed to a plugin, D102), DRV8
(one driver loop per home, cut-off takes carried on, D104), the owner's calls (D105, READ1 D107), the
**parallel-development arc whole** (MOD1–MOD9: union-merged records D106, every god file split into
registries, a `Process` test category, `tools/merge-branch.mjs` and the `dispatch-subagent` skill), and
TASKBAR1 (D108). **The same night:** HELP9 (D110: Ask Daoris reaches every door, held by a coverage
test that caught WSR6's new control on its first merge), PREVIEW1 (D111: a file's preview in the side
bar), WSR6 (D109: one press brings a repository up to date after its pull request merges) and LEFT1,
each merged with every gate; then HELP10 (Ask Daoris's owed doors, under D110). **The look at the
republished install** (2026-10-01) found WSR7, REVIEW2 and TABS1 on the owner's real workspace. **In
flight:** LEFT3 (D114); WSR7 (D112), REVIEW2 (D113), LEFT2 and TABS1 are in. **Next:** republish, run the
owner's merged pull request through *Bring up to date* on the window, then the owner's round (NAME1, FRAME1,
PLUGUI1, DEV1, LOOK1); the rest below is theirs or parked. Dispatch through the `dispatch-subagent` skill and merge with `tools/merge-branch.mjs`. The
kit's own relay (Shenora.Chromium 0.18.0) leaves a pump unobserved as Daoris's did: a request for the
kit's owner, not Daoris's to change (LOG2b).
- **The owner's ticket AR-2201**: its first follow-up merged by the owner's pull request (squash). The
  second ("vs Last 7 Days Avg" as the average day, the total ÷7) and third (the seven days before the
  selection, a rolling window; shifts are not set up, by the owner's word) landed onto one branch holding
  both over the merged line. **The owner merged that pull request too (2026-10-01)**, and the post-merge
  step ran through Daoris's own door on the window: *Bring up to date* found the line already current,
  proved the landed branch's work on it, and deleted the branch; the review now says where the work
  landed. The repository keeps only its line. What is left is the owner's: the production config update
  (the summary's subtitle now reads slightly off). Everything deleted earlier was backed up first, and the
  private notes say where.
- **The install** runs main at TASKBAR1 (`34f8602`, republished 2026-09-30 night: the splits, READ1, MOD8, TASKBAR1; the taskbar is the owner's to look at); republishing is the session's own call, never while a session
  on it runs. Start it the normal way, not through the dev tool, unless the instruments are needed
  (USE1g, confirmed: a start from Git Bash hands sessions a `PATH` their shell cannot read).
- **Load makes flakes**: FLAKE1's real-tick classes fail under parallel builds and pass alone. Run the
  rehearsals when nothing else builds.
- **Open and the owner's:** BUDGET1, PLUGREPO1, TRUST2, AGT2c, FG5, BRW3; on a trigger: TOOL4, TOOL5,
  PLUG7, SEM2, CANON9, REH1. **A new direction from the owner outranks all of them.**

**Start by reading the contract the item cites** — every backlog row names one, and
`docs/README.md` says which documents are current. `CLAUDE.md` carries the standing rules and the
dev loop. The bullets below are the traps that are not in any contract, because they were found
rather than designed.

- **Three shapes from the driver and the remote still bind anything new** (D45–D47): what may leave a
  machine is two manifest declarations, silence meaning local; the quest state machine is the only
  lock and outside sessions stay first-class; and a session's record is concluded from its exit code
  and its quest's state, never from what it said about itself (D46 §4).
- **Adding a refusal to a desktop module is three things**, and a test holds each: a code in
  `Refusals`, an entry in **both** locale catalogues, and a throw site using it. A thrown exception's
  message reaches nobody — the host maps it to a generic code carrying only the exception type, which
  is why every module sentence was invisible until REV2 (`docs/FIX-LOG.md`). `DriverException` is the
  one exemption and it is mapped once, at the module boundary, so the driver's own wording travels.
- **Adding a `DAORIS.DRIVER` door is three things since MOD5**, and `DriverModuleRoutesTests` checks all
  three: a handler marked `[DriverRoute("NAME")]` in `DriverModule.<Domain>.cs` (the domain named as the
  page's `bridge/<domain>.ts` that calls it), its name in that domain's row of the Desktop README, and a
  call from the bridge or a test. A proposal kind, a room section, a CLI verb, a catalogue area and a
  Settings domain are each a file and a registry line the same way (MOD2–MOD7).
- **The modules tests are serialized on purpose** (`Parallelism.cs`): those modules resolve every path
  from process-global environment variables, so two test classes at once trample each other. Found by
  a second class turning two passing tests red.
- **A schema change rebuilds the entry store and adds columns everywhere else.** The knowledge
  entries are an index, rebuilt on a schema bump; the registration and session stores add columns
  (`SchemaColumns`), because a registration that vanished on an upgrade is the failure those stores
  exist to prevent.
- **Two endings that mean different things:** end of input lets the harness wind up (`completed`);
  `stop` is the person's interrupt (`stopped`). Anything that grows a third way out should say which
  of those it is.
- **Four things that will bite if forgotten:** being in a folder is no longer being a member (a
  repository joins by `connect` and leaves by `retire`); the bootstrap import runs **once** per store,
  and anything that re-ran it would resurrect every repository someone retired; **a feed never names
  its own workspace** — the receiving deployment's wiring decides where material lands; and **a
  checkout with no git history feeds no knowledge**, because a wholesale replacement the receiver
  cannot order against what it holds is not safe (the driver says so itself rather than sending it).
- **Two artefacts agreeing on a file are twins** (`.claude/knowledge/twins.md`): the remotes map has
  three, and the environment pair replaces the file for the **whole machine**, which is what the
  rehearsal's hermetic guard rests on.
- **A refusal can be INFORMATION** (D48 §6). The flag rides the wire (`{"error": …, "information":
  true}`); a client that classified by matching the sentence would turn every rewording into a silent
  behaviour change. Anything that grows a new "not taken, and that is fine" answer belongs in that
  class rather than in the failure one.
- **Verify before claiming done, always**, with the gates `CLAUDE.md`'s dev loop lists: `npm run
  verify`, the three .NET suites, `npm run rehearse:family`, `npm run test:web`, and — when the
  desktop, the publish or the locator moved — `npm run rehearse:deploy`, which publishes the shell to
  `_fixtures/` and drives **that**. 🔴 **Phase 4 asserts the started host is the INSTALL's own**; the
  gate plants a decoy under the scratch home's `bin/` itself, so every machine can express the
  defect. If a `bin`-driven gate is red while `npm test` is green, suspect a stale gitignored `dist/`
  first (FIX-LOG).

## Backlog

**Seventy-one rows are open** (most of them the builds of the designs below) (triaged 2026-09-30, when the owner asked to go faster; 2026-10-01 after the night's merges, the look, and the owner's round):
- **Merging next:** TOOLS2, TOOLS3 and FRAME1b. **Building:** FRAME1c, DOC3 (with DOC2), SHEN2 and LYN1; Daoris.Plugins' own session on PLUGREPO2d.
- **Designed, to build in order:** DEV2–DEV11 (D115); FRAME1b–i (D118) after NAME1b; PLUGUI1 after FRAME1c;
  LAYOUT3–LAYOUT10 (D117) after LAYOUT2's evidence.
- **Waiting on the owner (five):** BUDGET1 (their call), TRUST2 and AGT2c (a grant, two
  downloads), FG5 and BRW3 (the owner present).
- **Parked on a trigger (nine):** TOOL4, TOOL5, PLUG7, SEM2, CANON9, REH1, D76's held file tree,
  and FLAKE1 and TEST1, which wait for their next sighting (the Process category runs serially since MOD8,
  and the final serial run was clean). None is work until its trigger arrives.

Every closed row is in `docs/task-archive.md`, and this file holds no ticked rows, by the `task-lifecycle`
rule it also ships. A heading below holds open rows only.


### Keep going (owner, 2026-09-30: *"lets continue the development … you probabbly should start with subagents"*)



**Found looking at the republished install (2026-10-01)**, each on the owner's real workspace of 29
repositories, which no fixture has:


### One product, not a set of screens (owner, 2026-10-01) — after the night's in-flight work

> *"because plugin will be a big part of daoris so it need to have a panel/screen itself, and develop
> proper ui/ux also why only session has more layout option we do need to make everything consitant and
> also for naming (for example in settings) we do need to have properly named in both en/zh since this is
> not just translation this is part of the ui element (this also apply to other display too) … also we
> should be able to run sub-agents cross darois development … daoris itself need to have a proper
> develpment cycle too, this also need to be designed properly"*

Proposed order: NAME1's glossary first, since every screen's names follow it, the new one's included;
then FRAME1's audit and model, then PLUGUI1 on that frame. DEV1 is a design document and can run beside
them. Each starts with its contract document, is built in parts by subagents, and is looked at on the
install in both themes and both languages.

- [ ] **MANAGE1 — saving a declaration erases `uses`** (found by DEV4). Repositories → Manage → Save
  declaration re-registers with a `domain` that carries no `uses`, which erases the repository's `uses` from
  the registry until its next `connect`. Lanes avoid it by DEV4's preserve rule; `uses` needs the same, with a
  test that fails first.
- [ ] **NAME2 — the names after NAME1b, on the window**: the glossary gains *lane* / 泳道 (DEV4 named it; NAME1
  had no term); the 107 budget judgements `names:check` reports are looked at on the window at 888 px, both
  languages, and each is renamed or accepted.
- [ ] **PLUGUI1b — the Plugins view on the frame, on today's answers** (D119 §6; waits on FRAME1c): the view,
  its list and pages, `＋` and ⋯, the offer's page, the kit drawer, the opener. Look: 1280 and 680 px, both
  themes, both languages.
- [ ] **PLUGUI1c — Settings keeps only what is a setting** (after b): the domain retires, its anchors repoint,
  the folder row joins Settings → Driver; crosses five lanes on purpose.
- [ ] **PLUGUI1d — the machine log's plugin events, health, and `plugins show|activity`** (beside b): seven
  `plugin.*` events without words, `PluginHealth`, the two verbs.
- [ ] **PLUGUI1e — the host answers the page** (after d): `PLUGINS` extended; `PLUGIN`, `PLUGIN_ACTIVITY`,
  `PLUGIN_READ`, `PLUGIN_ADD`, `PLUGIN_OPEN_FOLDER`.
- [ ] **PLUGUI1f — the page whole** (after c and e): health on the window, Points, Agents, Servers, Activity
  live, Data folder, Source, *Install from a folder…*.
- [ ] **PLUGUI1g — a plugin's checks** (after f): the last trial kept, its own tests run in a copy under the home.
- [ ] **PLUGUI1h — Ask Daoris reaches the Plugins view** (after c and FRAME1i).
- [ ] **FRAME1b — the list pane, Sessions' first** (D118, model §6): `ListPane`, `StripMark`, `listKeys`,
  per-view bounds, strip by room, the laid-over mode; Sessions' rail moves onto it. Look: 1280, 900, 680 px.
- [ ] **FRAME1c — the main area and the view contract**: `ViewMain`, per-view memory, `open(view, item?)`,
  container queries in Overview, Projects and Map, loading states, drawers above a full side bar.
- [ ] **FRAME1d — Quests on the frame**: asks and quests in the list, a record in the main area, composers
  stay drawers; `test:web`'s quest-lifecycle spec moves to the page in the same row.
- [ ] **FRAME1e — Repositories on the frame** (the renamed Projects): adopted, then registered; the
  repository's page with Manage and its code map.
- [ ] **FRAME1f — Search and Convergence on the frame**: the hits and findings in the list, the entry in the
  main area; the reader drawer retires for these.
- [ ] **FRAME1g — Settings on the frame** (after FRAME1c): the domains as the list pane, never stacked; skeleton
  rows while machine domains load.
- [ ] **FRAME1h — the secondary windows**: the monitor's rail on `ListPane`, a detached console on
  `OutputPanel`, the monitor's title on a real token (`tokens.test.ts` fails on `text-h3` first).
- [ ] **FRAME1i — Ask Daoris knows each view's list and item**: `where.ts`, `go` naming an item, the room.
  Order: b, c, then d–g (d, e, f one at a time; g beside one), then h and i. PLUGUI1 starts after FRAME1c.

### The kits (owner, 2026-10-01: *"shenora 0.19 and lyntai 3.5.3 both ready"*)

- [ ] **SHEN2 — Shenora 0.19.0.** Move `Shenora`, `Shenora.Windows`, `Shenora.Chromium` (the desktop's
  `Directory.Packages.props`) and `@shenora/react` (the web) from 0.18.0 to 0.19.0; read the kit's changelog for
  what the shell relies on (the Chromium engine, the frame, the IPC modules, the window state, the debug port
  LOOK3 met) and retire any workaround the kit now carries, as SHEN1 did. The deployment rehearsal proves it.
- [ ] **LYN1 — Lyntai 3.5.3.** Move `Lyntai.Core` and `Lyntai.Providers.Basic` (the service's
  `Directory.Packages.props`) from 3.2.0 to 3.5.3; read the changelog for anything the service relies on or can
  now drop. The service suites and the family rehearsal prove it.

### Doctrine, plugins and tools (owner, 2026-10-01)

> *"remember daoris still have role for doctrine (and we do need to research a good development doc pattern
> for code generation and use it as standard for all repo setup so its good for sessions, and the goal is to
> unblock the repo as far as possible so less ask human permission during development) and I also created
> folder for plugins [Daoris.Plugins] … this repo will for our default plugin development and later will be
> release to nuget so we can use nuget as plugin site and we need to find a way to filter on nuget for daoris
> plugin (or we can use npm, you can decide …) … all tools that daoris using like git, [terminal] should all
> have a self managed option (and can be setup in settings) which can be download from locations … I perfer
> provide default download location and built into the app with a resouce json file that can be updated if
> need other resouce location"*


### Plugins: the workshop, Daoris.Plugins and a catalogue (D120) — the build

The contract is `docs/2026-10-01-plugin-distribution-design.md`; §7 carries each row's full text and proof.
Every task in Daoris.Plugins is an ask to it, taken by its own session (the owner's wish: Daoris develops them).

- [ ] **PLUGREPO2c — the two landing plugins** (asked: quest `#36351d45ac11`, its session working).
- [ ] **PLUGREPO2d — in-app-browser** (an ask), then **PLUGREPO2e — this repository lets them go** (after PLUGDIST1g).
- [ ] **WORKSHOP1a — the workshop setting** (§2.1): where Daoris develops a plugin, the home by default, both doors.
- [ ] **WORKSHOP1b — the workshop and its sessions** (§2.2); **WORKSHOP1c — the workshop on the view; Ask Daoris
  makes plugins there** (§2.3, §2.5); **WORKSHOP1d — hand-over and a named source** (§2.4).
- [ ] **PLUGDIST1a — the package and its reader, offline** (§5.1, §5.7); **PLUGDIST1b — the pack and release
  workflow** (an ask to Daoris.Plugins); **PLUGDIST1c — a package source over HTTP** (§5.3–§5.8);
  **PLUGDIST1d — the host answers**; **PLUGDIST1e — Find plugins** (§6, after PLUGUI1f).
- [ ] **PLUGDIST1f — the first publish** (the owner's: the nuget.org account, trusted publishing, the prefix), then
  **PLUGDIST1g — the offers from packages**. ⏸ **PLUGDIST1h — the repository signature checked** (held).
- [ ] **INIT1 — `init` writes line endings it can keep** (found setting up Daoris.Plugins): a `.gitattributes`
  with `* text=auto eol=lf` when the repository has none, since Daoris measures its files byte for byte and a
  machine with `core.autocrlf` would check them out as CRLF, as drift.

### Development documents and fewer asks (D122) — the build

The contract is `docs/2026-10-01-development-documents-design.md`; §6 carries each row's full text and proof.
Order: UNBLOCK5 first (a week of *before*); then DOC2, DOC3 and UNBLOCK2 side by side; the driver lane runs
UNBLOCK5 → UNBLOCK2 → UNBLOCK4 → UNBLOCK3 → DOC7. Only DOC2 changes the canon.

- [ ] **DOC2 — the standard as canon** (§2.1–§2.6, §4): core knowledge `development-documents`, core skill
  `set-up-documents` with templates; this repository and `examples/` re-synced in the same commit.
- [ ] **DOC3 — roles bound to paths** (§2.7, §2.8): `documents` in the manifest, `sync`'s table, `check`'s facts;
  no canonical skill carries `allowed-tools`.
- [ ] **UNBLOCK4c — the push canary** (the owner's to allow): one turn per form against a local bare remote in
  scratch, by UNBLOCK4's procedure (its archive entry and hand-back): `git -C . push`, `-c`, `--no-pager`, a quoted
  subcommand, an alias, on both doors; the remote's tip must not move, and what refused each is recorded.
- [ ] **DEV3a — a stop is reported in the run that made it** (found merging, 2026-10-01): the family rehearsal's
  lost-claim check failed once under load (311/312, green alone): the losing session was stood down with its
  reason, but the driver printed no `stop  session` line, since sessions outlive their tick (DEV3) and the stop's
  report can land after the run's last print. Make `--once`/`--until-idle` wait for and print every report it caused.
- [ ] **UNBLOCK2 — the declaration and its judge** (§3.1–§3.3, after DEV5): `safe` beside `gates`, read from the line.
- [ ] **UNBLOCK3 — the person's one yes** (§3.4, §3.5, after UNBLOCK2 and a week of UNBLOCK5): the `declare`
  proposal, exact rules on both Claude Code doors.
- [ ] **DOC4 — this repository declares its documents**; **DOC5 — the service reads declared records**;
  **DOC6 — the example family keeps the standard**; **DOC7 — what sessions read, measured** (§1.5).
- [ ] **UNBLOCK6 — the screen and Ask Daoris for a declaration**, both languages; **UNBLOCK7 — codex and dsh
  measured before anything is handed**; **UNBLOCK8 — the first real declaration** (the owner's: asks a week
  before and after).

### Self-managed tools (D121) — the build

The contract is `docs/2026-10-01-tools-design.md`; §7 carries each row's full text and proof. Order: TOOLS2 →
TOOLS3 → TOOLS4, then TOOLS5 ∥ TOOLS6, then TOOLS7 ∥ TOOLS8, then TOOLS9; TOOLS10 before any session runs a
managed git; TOOLS11 last.

- [ ] **TOOLS2 — `tools.json` and its resolution, twins** (§2.1–§2.3, §5): the three ways; `daoris tool list|path|use`.
- [ ] **TOOLS3 — the resource list and its merge, twins** (§3.1–§3.5): `resources.json` built in; the first entries
  read from the makers' own pages and sums into an evidence document.
- [ ] **TOOLS4 — download, verify, unpack, lay out** (§3.6, §3.7): staging, hashes, `daoris tool download|update|…`.
- [ ] **TOOLS5 — one answer for every child** (§2.4, §2.6, §2.7): git, hooks, npm, the tree guard's node, the terminal.
- [ ] **TOOLS6 — what Daoris's git carries** (§2.5): the allow-list from `core.sshCommand`, `GIT_CONFIG_GLOBAL` with
  includes, the version floors.
- [ ] **TOOLS7 — Settings → Tools** (§4.1, §4.4) and **TOOLS8 — Ask Daoris's `tool` kind** (§4.3).
- [ ] **TOOLS9 — the rehearsals** (§6): a loopback list server, stub tools, a tampered file refused.
- [ ] **TOOLS10 — the probe before a managed git meets an agent** (the owner allows one start of each agent).
- [ ] **TOOLS11 — the first real downloads, on the install** (the owner's run): a managed git bringing the owner's
  workspace up to date over SSH, a managed node running a plugin, a managed pwsh in the terminal, `gh`/`az` landing.

### Daoris develops Daoris (D115) — the build

The contract is `docs/2026-10-01-self-development-design.md`; each row names its sections, which carry the
detail and the proof. Order: DEV2 ∥ DEV3 ∥ DEV4, then DEV5 → DEV6 → DEV7 (one lane, in sequence), then
DEV8 ∥ DEV9, then DEV10 and DEV11.

- [ ] **DEV5 — the queue lands a branch from outside** (§4.2–§4.8): the `queue` form in a detached tree under
  the home, gates by declared `kind` with the `quiet` re-run, the fast-forward under TreeLock,
  `daoris-driver queue …`, `tools/commit-check.mjs`.
- [ ] **DEV6 — lanes side by side** (§3.2–§3.4): lane locks, oldest-first reservation, `laneCap` (default 3,
  both doors), `lanes` on the record and in the prompt.
- [ ] **DEV7 — a driven session readies, and the queue answers it** (§4.1, §4.5, §4.6): `session_ready`, the
  verdict through the answer door (D83), three failures to the person, done means landed.
- [ ] **DEV8 — the queue on the window** (§4.9), with Ask Daoris's doors for every new verb (D110).
- [ ] **DEV9 — the steward** (§5): the split into lane quests, decision numbers, record steps; the dispatch
  skill rewritten for the steward and the lane session. Two answers it owes, found merging: `docs/README.md`'s
  index rows (every branch edited them, and the union merge kept both versions each time), and a lane session
  that adds a path no lane owns (it must place it in the steward's `daoris.lanes.json`, DEV2).
- [ ] **DEV10 — the first user** (the owner present): a real row through steward → lane → queue → record, then
  one crossing two lanes; the cap and the strikes rule revisited from its evidence.
- [ ] **DEV11 — the second user, and the tools retire**: a canon knowledge document on lanes, a laned example,
  then `tools/merge-branch.mjs` retires and the dev loop says *queue*.

### One repository, every agent (D117) — the build

The contract is `docs/2026-10-01-agent-layout-design.md`; §7 carries each row's full text and proof.
LAYOUT2 first: until it measures, the layout's entry points are the design's reading, not the harnesses'.

- [ ] **LAYOUT2 — the canary turn** (the owner's to allow). The keyless half is done and archived
  (`docs/2026-10-01-entry-point-evidence.md`: the ACP adapter loads project instructions). One turn per
  harness, by §6's fixtures and prompt, shows a maker-side flag is off for the account and confirms the
  predicted cells.
- [ ] **LAYOUT5 — this repository's doctrine moves** (§4.1, §4.3, §4.4), run alone: `git mv`, the manifest,
  `sync`; `.gitattributes`' union line moves in the same commit; `examples/engine` moves. The lane map is
  `records`' (DEV2), so the branch touches the steward's records.
- [ ] **LAYOUT6 — the brief moves** (§4.2): `CLAUDE.md` into the root `AGENTS.md` (about 1,300 words, under
  codex's documented 32 KiB) and eight rooms; `CLAUDE.md` keeps the import alone.
- [ ] **LAYOUT7 — the layout facts and the set-up quest** (§6.1–§6.3): read from the line, `daoris-driver
  setup <repo> [--plan]`, the quest composer, the permission rule the press adds.
- [ ] **LAYOUT8 — the layout on the screen, and its Ask Daoris door** (§6.1, §6.5): Projects' rows, *Set up
  for agents*, the `setup` kind; both languages, looked at on the window.
- [ ] **LAYOUT9 — a lane names its rooms** (§2.5), after DEV2 and DEV6.
- [ ] **LAYOUT10 — the first real set-ups** (the owner's run, after the first publish): one unadopted
  repository, and one with instruction files of its own.

### Plugins Daoris makes (owner, 2026-09-30)

> *"do we need a Daoris.Plugins repo? … I think Daoris should have ability to create plugin by itself,
> say for example via "ask Daoris""*

PLUG8 and PLUG9 built the reading recorded here (in the archive): making a plugin is work done in a
repository with tests, and installing one is the person's press.

### What REV3 left (2026-09-25)

REV3 is in the archive, and `docs/2026-09-25-rev3-review.md` is its ledger. These rows are what it
found that is not session-sized, or is the owner's call. Each one was confirmed in the code.

- [ ] **BUDGET1 — what the core budget caps** (CLI F10; the owner's call). Since D59 `inspect` counts
  only the body of Daoris's `AGENTS.md` region. The repository's own always-loaded material is not
  counted: the rest of `AGENTS.md`, `CLAUDE.md`, a local `.claude/rules/` file. The README and the
  instruction-file design still say the budget guards it, and `analyze` projects the pre-D59
  quantity (`config.ts` now says so). Decide what the number caps, then move all three together.
### What an agent may do — permission scopes (owner, 2026-09-24 → D72, D73)

Measured: rules and hooks Daoris hands over at spawn reach an untrusted session on both doors, and a
repository's own allow-list does not (`docs/2026-09-24-deploy1-acp-trust-evidence.md`).
- [ ] **TRUST2 — what D73 leaves unmeasured.** Whether Claude Code honours a trust key Daoris wrote
  as its own; whether a trusted parent covers a child, since the hold matches the exact folder; and
  whether the screen should offer a grant before any hold exists (FG5's onboarding). The first
  needs one grant, and a grant is the owner's.

### The toolchain and its accounts (owner, 2026-09-22 → D57)

`docs/2026-09-22-toolchain-design.md` is the contract, and its §3 is the home of the resolution rule
(*explicit command → managed pin → `PATH`*; *pick → workspace → machine → none*). TOOL1–TOOL3 are in
the archive: **usage is measured before it is managed**, and breadth is **more native adapters plus
the ACP door, not a registry**.

- [ ] **TOOL4 — rotation.** ⛔ **Held by D57 §b until TOOL3 has run long enough to answer three
  questions**: what a harness's exhaustion actually looks like in its output, how long a cool-off
  should be, and whether a rotated session stays reproducible. Exhaustion is **observed, never read**
  — Daoris cannot ask a provider what is left without a credential, and D49 §4 stands. Do not start
  this before there are real transcripts to write the signal against; a string match on somebody
  else's error text is the fragile part and guessing it is how it gets written wrong.
  **The first real one (2026-09-27, FG5's run):** Claude Code over its ACP adapter answered
  `session/prompt` with a JSON-RPC internal error, 370k tokens into a twenty-minute turn. Its message
  was *"You've hit your individual spend limit · run /usage-credits to ask your admin for a higher
  limit · your session limit resets 7am (<zone>)"*, and the adapter then exited 0. One observation
  answers part of the first question and none of the other two.

- [ ] **TOOL5 — more native adapters** (design §5). D23 applied more times: a native adapter per tool
  worth matching properly (its own flags, configuration home, login flow, version question), with the
  ACP door for tools that speak the protocol and a tool free to be both. **Not a registry** — the
  components plan's rejection stands and D24 with it. Largely folds into **ACP3**, which already
  brings dsh and codex as configurations of the door; this item is what remains once that lands.
  ⛔ **Read against what is built, 2026-09-22: what remains is a TRIGGER, not work.** Every sentence
  of §5 is realised — the adapter seam, the ACP door, a tool being both (`codex` is managed with no
  native adapter and its sessions ride `codex-acp`, which §5 names as the sanctioned shape), the
  declaration duplicated in two artefacts, no registry, no model named. **Nothing is waiting except a
  tool somebody actually wants**, so this is held on the same bar CANON9 and HARNESS1 use: start it
  when a repository names the tool, because an adapter's every field is a claim about somebody else's
  program and a guessed one fails in a person's terminal. The reading did find one real gap, and that
  is fixed rather than carried — the twin tables pinned the three ACP arrivals and left `claude-code`
  asserted on neither side.

### Agents, their accounts, and the map (owner, 2026-09-23 → D67)

**D67** holds the owner's answers; `docs/archive/2026-09-23-agents-direction.md` the asks and what
was checked. Every AGT and MAP item is archived but this one.

- [ ] **AGT2c — one real vendor-channel pin of each, observed:** that Claude Code stays at its version
  under `DISABLE_UPDATES`, and that a pinned Codex outside its own layout takes no update action (the
  evidence says so; nothing measured it). Spends two real downloads — the owner's call.

### The in-app browser — Daoris's own, driven over MCP (owner, 2026-09-27 → D78)

> *"so instead rely on things like claude extension we can have our own built-in browser system
> (or use plugin to support this)"*

`docs/2026-09-27-in-app-browser-design.md` is the contract. It was measured first: Playwright MCP
attached over CDP drives a WebView2.

- [ ] **BRW3 — the owner's machine** (with FG5). The plugin, the one sign-in in the in-app browser,
  `mcp__browser` allowed, and the first ask whose ticket the intake reads through it.
  **2026-09-27: set up to the owner's step.** The install is republished at `b287580`. The
  `in-app-browser` plugin replaces the Edge one, `mcp__browser` stays allowed for the workspace, and
  the browser window was opened on the install for the sign-in. **Then the owner signed in and
  asked** (2026-09-27, 19:51 UTC), and the loop ran for real. The intake's `WebFetch` was refused
  (the protocol door refuses every request, and the room's allow-list is not the handed rules). It
  found `mcp__browser` itself, read the signed-in ticket in the in-app window, and published one
  quest to the right repository, with a browser-verify `then` step, tier `intake`. The development
  session's tree then failed on Windows' path limit and left a branch per tick (FIX-LOG,
  2026-09-27). Fixed, republished, and the session opened its tree and took the quest. Its outcome
  is FG5's report.

#### The browser as a browser (owner, 2026-09-27) — after BRW3 and FG5

> *"the browser itself should have similar features as regular browser (for example edge, favriate,
> and etc, which can help to managed the use) and also should be able to start from the daoris app
> … start them after you complate current task (which is more important as the core workflow)"*

🔴 **D84 changed the course (2026-09-28): Daoris ships a Chromium for its browser, and the person's
Edge is always an option** (`docs/2026-09-28-managed-edge-evidence.md`). The engine landed as CHR3
(below), in place of the WebView2 window, and the Edge option as BRW12 and BRW13, in the archive.
### The first goal — a real workspace, a ticket, a task started (owner, 2026-09-27 → D77)

> *"I think we still does not meet the first goal: setup [the named workspace] as workspace and use
> mcp to control chrome with jira to read ticket and start task (and does not need to locate which
> repo just start the task in daoris)"*

`docs/2026-09-27-first-goal-study.md` is the study. It found three walls in the code between D65 and
the real workspace: nothing unadopted could be chosen, an import stated no workspace, and a ticket
is behind a sign-in. FG1–FG4 are in the archive. **INT6 is reshaped as FG5.**

- [ ] **FG5 — the first run on the named workspace** (the owner present; INT6 as it now stands).
  The study's §5, in order: republish, import as a workspace, a browser plugin for this machine,
  the person's one sign-in, the browser allowed for the workspace, the loop (intake, the protocol
  door's adapter, drivable, own trees), then an ask with a real ticket. What it produces is the
  report: the room it rendered, whether the intake read the ticket and chose, and what the session
  did. Every part up to the ask can be run by an agent. The sign-in and the ticket are the owner's.
  **The loop is met** (2026-09-27): six legs, from the first ask to a chain that ran end to end, are
  the study's §8. **Still the owner's:** the verify step's camera-GUID defect and the daily view's note
  chip, both in that repository, and the branches its sessions left (WSR3).
- [ ] ⏸ **SEM2 — vectors that persist** (after SEM1, held). SEM1 embeds what is on disk once per
  process, on first use. The MCP host is one process per session, so with an embedder configured,
  each session's first search embeds the whole corpus. Lyntai.Storage.Sqlite's vector store would
  keep them in `knowledge.db`. It needs Lyntai 3.5, its version floors (Microsoft.Data.Sqlite
  10.0.12, SQLitePCLRaw 3.0.5), Dapper and FluentMigrator, and a re-embed when the model changes.
  **The trigger is a measurement**: a machine running with an embedder, and a first search whose
  cost somebody notices. No machine here runs with one yet (the install says `lexical only`).

### Plugins — a folder that declares, and may speak (owner, 2026-09-23 → D64)

`docs/2026-09-23-plugin-design.md` is the contract. **Two standing decisions are NOT reopened**: no
adapter registry (D23/D24/D57 — a declared harness is a file on one machine, and the ACP door is the
seam), and no plugin runtime for the surface (D52). **No code loads into a host, ever.**

- [ ] ⏸ **PLUG7 — service-side points** (held): the same wire reaches the knowledge service when a
  plugin somebody writes asks for a point there. PLUG4–6 are in the archive.

### The reference gap (owner, 2026-09-24)

> *"lets keep push the ui/ux design and I still think this does not meet the reference projects
> capbility"*

`docs/2026-09-24-reference-gap-study.md` measured it: the attended session's centre is a record, not
a conversation, because ACP's structure is flattened to text lines before the bridge, a chat does not
use a structured wire at all, and nothing is kept for the page across a restart. A chrome pass cannot
close that. **The owner chose the conversation model and every extra → D76** (2026-09-25), with
*"you should check screen by screen and all ui ux logic"*. Take them in order; each is one landing,
TDD, looked at on the window, and the ones that touch a real session use one (authorised
2026-09-24). **CONV4 was split (2026-09-25)**, and all four parts are in the archive.
- [ ] ⏸ **Held, after the conversation:** a file tree in the dock, when a person asks to browse a tree
  rather than open a named file. The rest of this row is built: the document preview (PREVIEW1, D111,
  opened from a tool card or the review) and the terminal (CONSOLE4, D96).

### Open — the arc's leftovers, in the order they are worth doing



- [ ] **FLAKE1 — an intake test failed once in about 20 full driver runs.** *Narrowed by MOD8
  (2026-09-30): every class that starts a real process or runs a real tick carries the `Process` category,
  subagents never run it, and the parent and the release run it serially; the final serial run on an idle
  machine was 543/543. Still live: `SessionProcesses.Stop` catching only one exception type (a kill mid-exit
  threw through a chat's cleanup; being fixed), `LandingPluginTests.A_plugin_that_never_answers…` (a 3 s
  patience against a slow `initialize`), and `DrivenSessionInputTests` (acp-stub, "the pipe is being
  closed", once in a parallel run). The history below stands.* *`SessionProcesses.Stop` is fixed (FIX-LOG,
  2026-09-30); the merge tool now re-runs any failure in a Process gate, whatever its class.*
  `IntakeTests.An_ask_with_an_intake_harness_is_answered_by_a_session_that_publishes_onto_it`: the
  intake opened, and its stub agent published nothing onto the ask (2026-09-25, under a loaded full
  run). It passed 10 runs in a row after. Its assertion now carries the session's transcript and the
  tick's events, so the next failure says why: a `fetch` to the stand-in that failed, an exit, or
  something else. A gate that fails one run in twenty is a gate people learn to re-run, which is how
  a real failure gets waved through. **A second of the same shape** (2026-09-27, with two more
  real-tick classes in the suite): `DrivenSessionInputTests`' `acp-stub` case failed once in a full
  run, and passed alone and on the next full run. Its wait for the session to start is 15 seconds,
  under a suite that spawns node in several classes at once. The assertion does not say which step
  was slow, so it needs the same treatment. **Seen again 2026-09-28**, beside a third: in consecutive
  full runs the `acp-stub` case failed once, then `HookTests.A_real_hook_process_is_started_in_its_folder_with_its_data_and_id_in_the_environment`
  failed in its cleanup, the plugin folder still held by the hook's process, and the next full run
  passed 862/862. The cleanup deletes before the process has let go. *Fixed 2026-09-30: the hook tests' cleanup now waits for the process to let go.* **Seen again 2026-09-29**: one
  failure in a full run of 898 during HELP1c, not named because only the summary line was kept; the
  next three full runs passed 898/898. Keep a full run's whole output, so the next one names itself.
  **Seen again 2026-09-30**, the output kept: `DrivenSessionInputTests`' `acp-stub` case, 935/936,
  the record `failed` where `stopped` was expected after the person's stop, during LOG1a with the
  rest of the chain queued behind it; ten runs alone passed. Unconfirmed reading: the stop lands while
  the stub's process is already ending, and the record takes the exit's word over the person's.
  **And a fourth class the same day**: `CanonicalLineTests.A_session_tree_grows_from_the_line_set_for_its_repository`
  failed once (*"the tree grew from main, not develop"*, 993/994) while five subagents built in
  parallel; its class passed three runs alone. Load is the common factor in every sighting. The driven-session assertion now carries the record and the tick's lines, so its next failure names
  itself; a stop landing mid-handshake was tested and is not the cause. **A fifth class, twice on
  2026-09-30** (LOG2's baseline, then its merge while WSR4 built beside it):
  `ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked`, 1144/1145,
  green alone three times running. **A seventh, the same night, caught by the merge tool as a FLAKE**
  (its first: the category rule, not the old list): `PseudoConsoleTests.Closing_it_ends_a_child_the_shell_started_too`,
  547/548 in a serial Process run loaded by a subagent's build and a live session, green alone. **A sixth class the same evening**, merging DRV8 while three
  worktrees built: `LandedBranchTests.A_branch_landing_records_the_branch_it_made_under_the_home`,
  1415/1416, green alone three times. **And a rehearsal the same day**: the family rehearsal exited 127
  straight after building the HTTP host, writing no transcript of its own, while three worktrees
  built beside it. It passed 301/301 run alone. **Since LEFT1 the merge tool runs a rehearsal that died**
  (a process-level exit, or nothing printed of its own) once more and reads FLAKE if it passes; one that
  reported a failed check has failed. The three merges after it (PREVIEW1, WSR6, LEFT1) needed no re-run.
  **Two more at LEFT3's merge (2026-10-01)**, with three subagents building beside it, each failing in the full
  serial run and passing alone: `TurnStopTests.A_conversation_on_the_native_door_is_handed_the_plugins_servers`
  (driver, 571/572) and `DriverModulePluginsTests.The_kit_makes_a_plugin_where_the_person_names_and_tries_it_or_an_installed_one`
  (modules, 113/114). Both spawn real processes; load is again the common factor. **And one at NAME1a's merge**:
  `PluginKitTests.A_silent_plugin_is_still_running_and_said_nothing_within_the_patience`, a timing test under
  three subagents' builds (the driver's Process half took 37 minutes), green alone. **And `PseudoConsoleTests` again**
  (its second sighting) at the designs integration's merge, under three builds, green alone.
  **And a rehearsal check, a new kind** (merging PLUG10, UNBLOCK4 and UNBLOCK5, three builds beside it): the family
  rehearsal's *its driver stops its own losing session* failed on its printed line (DEV3a), 311/312, green alone.

- [ ] **TEST1 — a Node process aborts with `0xC0000409`: seen three times now, once outside Playwright.** The
  second sighting was its trigger. Both runs died with `worker process exited unexpectedly
  (code=3221226505)`, Windows `__fastfail`: no output, no stack, no WER entry.
  - 2026-09-21, during SURF2, mid-suite. The identical run passed after.
  - 2026-09-25, during CONV4b's gate, at test 4 (*a chain moves on when its quest closes done*),
    0 ms in, after three passed; the other 17 did not run. CONV4b changed no e2e path or host code.
  - 2026-10-01, **outside Playwright for the first time**: the deployment rehearsal's own process exited
    `3221226505` in phase 6 (closing the deployed shell), merging TABS1 after a night of parallel builds.
    LEFT1's re-run rule ran it again, which then refused at its first step with EPERM on the scratch folder
    the dying run's processes still held; the rehearsal's removal now waits them out (`maxRetries`). Alone,
    straight after, it passed 70/70. So it is not the test runner's: the common factor is a Node process on
    Windows ending while its children are killed, as the sibling's reproducer says.

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

- [ ] **CANON9 — `desktop-winforms`, the last pack candidate.** One 11 KB source, one repository —
  below the two-repository bar, which is the whole reason the canon is trustworthy. Leave it local until
  a second repository needs the same thing.


## How to work a task

- **TDD:** failing test → run it fail → minimal implementation → run it pass → commit.
- **Commit per task, automatically, once gates are green** (D37 as amended). Push, publish, release
  and history rewrites stay the owner's call.
- **`npm run verify` before claiming done.**
- **A canon file is project-agnostic** — the principle and the reason, never the mechanism. See
  `.claude/knowledge/canon-authoring.md`.
- **When a task completes, move it to `docs/task-archive.md`** with the date and outcome.
- **Work an arc leaves behind goes in the backlog as a row**, never as a handover sentence — that is
  how work quietly stops being work.
