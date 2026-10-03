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
the protocol door (D53), the toolchain (D57, bar TOOL5; TOOL4 designed as D125), the instruction file (D59), the
first deployment (D60–D63), plugins (D64), the remote as a git remote (D68), permission scopes
(D72–D74), the menus (D75), and the conversation (D76, bar its held file tools), closed by UX5's
screen-by-screen pass. [`docs/README.md`](docs/README.md) names each arc's contract. Two
reviews read the code rather than re-running it, REV2 and REV3, and CLEAN1 settled REV3's cleanup
lists.

## State

**Counts, and this is their one home:** seventeen commands, **1091 CLI tests, 1068 service and 62 HTTP host, 3620 driver,
592 desktop modules, 80 devkit, 2736 web unit, 24 Playwright**, 114/114 release rehearsal, **349/349
family rehearsal** (it names its own phases when you run it), **84/84 deployment rehearsal** (D60),
and 5 universal devkit gates over this repository (DEVKIT3). Canon: 8 core rules, 6 knowledge
documents, 6 skills, 7 packs. The always-loaded core is **20,067 of 26,000 bytes** — a span in
`AGENTS.md` since D59 — and **advisory rather than enforced** (D54: a fact gates, a judgement
reports).

Nothing is published; development runs at `0.0.x`. **Adoption by other repositories is the owner's call
and happens when Daoris is ready** — it is not tracked here. Lyntai stepped off the tool 2026-08-17
(owner-requested), so the live consumer count is zero and Lyntai is not quest-addressable until it
re-adopts. The Shenora rehearsal keeps and does not expire: 6 collisions, 2 twins to retire, local
mechanics drafted at `docs/adoption/shenora-repo-mechanics.md`, budget 40,000, `check` clean at
38,782 bytes, with `web-webview` and `durable-jobs` ready for it.

## Handover — where a fresh session picks up

🔴 **Where 2026-10-01 left it.** The owner's direction: *Daoris is the master development tool, doctrine
and knowledge sharing*; their ticket work runs through the local Daoris, and Ask Daoris should reach
everything. **The owner's round of 2026-10-01 is being built in parallel** from the designs below (D115–D122):
one frame for every view, plugins with a view and a workshop of their own, Daoris.Plugins as the default plugin
repository published to NuGet, self-managed tools, the development-documents standard and fewer asks. Each row
is built by a subagent through the `dispatch-subagent` skill, gathered into an integration branch, and merged
with `tools/merge-branch.mjs`; the archive has every landed row. Daoris.Plugins' work is asked of it through
the platform and taken by its own sessions (PLUGREPO2c, PLUGREPO2d). The kit's own relay leaves a pump
unobserved as Daoris's did: a request for the kit's owner, not Daoris's to change (LOG2b).
- **The owner's ticket AR-2203** was asked through the window (2026-10-01, the lumachain workspace), and its intake
  session is proposing the quests; follow it there.
- **The owner's ticket AR-2201 is done on Daoris's side**: both follow-ups merged by the owner's pull
  requests, and the post-merge step ran through *Bring up to date* on the window, which proved the landed
  work on the line and deleted the branch. What is left is the owner's production config update. Everything
  deleted earlier was backed up first, and the private notes say where.
- **The install** runs main at TASKBAR1 (`34f8602`, republished 2026-09-30 night: the splits, READ1, MOD8, TASKBAR1; the taskbar is the owner's to look at); republishing is the session's own call, never while a session
  on it runs. Start it the normal way, not through the dev tool, unless the instruments are needed
  (USE1g, confirmed: a start from Git Bash hands sessions a `PATH` their shell cannot read).
- **Load makes flakes**: FLAKE1's real-tick classes fail under parallel builds and pass alone. Run the
  rehearsals when nothing else builds.
- **Open and the owner's:** BUDGET1, TRUST2, AGT2c, FG5, BRW3, UNBLOCK4c, LAYOUT2, PLUGDIST1f; on a trigger: TOOL5,
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

**Eighty-three rows are open** (most of them the builds of the designs below) (triaged 2026-09-30, when the owner asked to go faster; 2026-10-01 after the night's merges, the look, and the owner's round):
- **Merging next:** SESSUX1b with SESSUX1b2, SESSUX1c, SESSOPT1a/b/c. **Building:** SESSUX1e, WSSETUP14's design (D128).
  The pilot (WSSETUP12) ran; its branch waits for WSSETUP14.
- **Designed, to build in order:** DEV2–DEV11 (D115); FRAME1b–i (D118) after NAME1b; PLUGUI1 after FRAME1c;
  LAYOUT3–LAYOUT10 (D117) after LAYOUT2's evidence.
- **Waiting on the owner (five):** BUDGET1 (their call), TRUST2 and AGT2c (a grant, two
  downloads), FG5 and BRW3 (the owner present).
- **Parked on a trigger (eight):** TOOL5, PLUG7, SEM2, CANON9, REH1, D76's held file tree,
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

- [ ] **COST1 — what a long driven turn costs** (found with METER1): that turn ran about a thousand tool calls,
  and its context grew from 48K to 679K tokens on a 1M window, every call re-reading it from the cache. Nothing in
  Daoris sets an agent's window or when its harness compacts. Measure first (each driven session's context
  high-water and cache reads per turn, from the usage record and the turn events, over a week), then offer a
  per-agent ceiling, a smaller window or the harness's own compaction setting, on both doors. The owner's call,
  since it trades cost for what a session remembers.
- [ ] **NAME2b — seven names, looked at on the window** (NAME2's hand-back; 888 px, both languages): `help.setup`
  (Ask Daoris at its 300 px floor before setup is done), `scope.every` (the status bar with *Every workspace*),
  `signin.titleNew` (Settings → Agents, signing in to another account, the side bar at its floor) and
  `work.group.noCheckout` (English: a repository with no checkout here); each renamed or accepted after the look.
- [ ] **PLUGUI1c — Settings keeps only what is a setting** (after b): the domain retires, its anchors repoint,
  the folder row joins Settings → Driver; crosses five lanes on purpose.
- [ ] **PLUGUI1f — the page whole** (after c and e): health on the window, Points, Agents, Servers, Activity
  live, Data folder, Source, *Install from a folder…*.
- [ ] **PLUGUI1g — a plugin's checks** (after f): the last trial kept, its own tests run in a copy under the home.
- [ ] **PLUGUI1h — Ask Daoris reaches the Plugins view** (after c and FRAME1i).
- [ ] **FRAME1i — Ask Daoris knows each view's list and item**: `where.ts`, `go` naming an item, the room.
  Order: b, c, then d–g (d, e, f one at a time; g beside one), then h and i. PLUGUI1 starts after FRAME1c.

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


### Knowledge that sessions actually use (owner, 2026-10-03) — D135

KNOWUSE1 found the sessions do read their knowledge: of 46 items put to the owner, 25 were truly the owner's (13 asks for
3 prod acts), 10 answerable from ticket or code, 6 drift, 3 required by the repository's own docs, 2 knowledge-answered.

- [ ] **KNOWUSE1a2 — a park shows its go-aheads, and answering one goes on with it** (web, driver, modules; after
  KNOWUSE1a). The session page of a park that asked go-aheads shows them with *Approve* and *Refuse*, and answering
  there also answers the park: one press, not two. Contract: D135 §2, its KNOWUSE1a note. Proof: vitest; a driver test.
- [ ] **KNOWUSE1c — a closing note says what needs you apart from readings, each citing its source** (driver). Ticket line,
  doc line or code path beside every item; the look names the repository's own indexes. Contract: D135. Proof:
  `AskAndWaitPromptTests`; a canary turn.
- [ ] **KNOWUSE1d — the owner's words quoted second-hand are a reading** (driver; beside DRIFT1). A quote in a doc, a closed
  note or a commit is attributed only if the ask's record holds it. Contract: D135. Proof: prompt goldens.
- [ ] **KNOWUSE2 — a bench for checking a question before it reaches the owner** (tools). Replays the 46 recorded
  questions through the no-model floor (word search, labelled *words only*) and a model tier on the deployment's own
  harness, scored against their classes. Contract: D135. Proof: script tests; the model tier is the owner's run.
- [ ] **KNOWUSE3 — the review beside each item** (web, driver; held on KNOWUSE2). Says which tier found each hint and never
  answers in the owner's place. Contract: D135. Proof: stories; the look.
- [ ] **KNOWUSE4 — a request the owner may publish to the work repository** (the owner's). Its comparison document
  records a misread answer as *the owner also settled the calculation*; correct it, and reconcile its *fix the config,
  not the shared component* rule with the owner's *add it into the common-report module*. Contract: the evidence §4.


### The desktop's own surfaces (owner, 2026-10-03)

- [ ] **CTX1 — the desktop's right-click menu** (owner, 2026-10-03: *"we do need to utilize the right click menu of daoris
  desktop"*). Today a right-click shows the engine's default menu, or nothing; give each surface (a session row, a quest,
  a repository, a plugin, selected text, a link) its own acts there, the same acts its ⋯ offers, from one owner. Contract:
  a short design (D41's interaction rules amended). Proof: route and vitest tests; the look in both themes and languages.
- [ ] **PLUGUI2 — the plugins page and a plugin's icon** (owner, 2026-10-03: *"plugin page design (and plugin icon)"*). The
  page reads as a list of manifests; design it as a catalogue (installed, Daoris's own, available) with each plugin's
  icon, declared in its manifest with a generated fallback. Contract: a short design (D64, D120 amended). Proof: stories,
  the manifest's twin tables, the look.
- [ ] **LAYOUT11 — content that does not size with its pane** (owner, 2026-10-03: *"some page content still not auto
  sizing with the outer like quest body and plugin detail"*). A quest's body and a plugin's detail keep a fixed measure
  inside a wider or narrower pane. Find every such surface by looking at 1280, 888 and 680, and size each by its pane
  (D41 §3's measure kept for prose only). Contract: D41 §3, the working-surface components. Proof: the look before and after.

### Sessions that are easy to manage (owner, 2026-10-02) — D126

> *"so there is no way to easily managed sessions in daoris rn and it's not really smooth for ui/ux lets also set this
> improve task too"*

Read from the code the same day: a session row's ⋯ offers only *Review* and *Copy session ID*; a quest parked on its
strikes is started again only by *重试* on the quest's page, under its "sitting" fact (the 1 October limit left the
owner's AR-2203 work there); a parked session is answered in its conversation; what ended folds under one heading,
with no view by state (needs you, failed, parked, waiting for an account) and no clearing; titles run long.

The contract is `docs/2026-10-02-session-management-design.md`; §9 carries each row's full text, proof and look.
Order: SESSUX1a ∥ SESSUX1j; the driver lane a → b → g; the web shell lane c → d → e → i; f after e; h after d–g and
FRAME1i; k after TOOL4c, TOOL4d and c; then the look (l). b alone ends the silent stop; i alone would have told the
owner on 1 October.

- [ ] **CARRY1 — a cut-off's carry-on checks the take** (service; found by SESSUX1b2): D80 opens a carry-on after a
  cut-off without asking whose take the quest is, so a start that failed before its take carries on over someone else's.
  Contract: D80, D126's SESSUX1b2 note. Proof: a ledger row refused, seen failing first.
- [ ] **CHATTAKE1 — a chat's take is marked on its record** (service; found by SESSUX1f). `MarkTookAsync` ignores a chat, so a
  chat that took and worked a quest reads as serving none, and SESSUX1f's delete would remove that work's record.
  Contract: D126 §5.4, its SESSUX1f note. Proof: a ledger test refusing to delete a chat that took a quest, failing first.
- [ ] **SESSUX1h — Ask Daoris reaches sessions** (§7.3; driver, service, web-shell; after d–g and FRAME1i).
- [ ] **MSG1 — session messages, one model for every door** (owner, 2026-10-03: *"we do need a way to set message between
  runs so it does the resume so that close the gap for codex and it does look like in the same session, and this should
  be properly designed native/daoris managed session messages"*). A person's words to a session reach it at the next
  step where the door allows (STEER1), else at its turn's end by resuming the harness's own conversation (ANSWER1's
  resume), and after it ended by reopening the record, so every door reads as one session. Contract: D137 (in flight).
  Proof: the design, the doors measured, then its rows.
- [ ] **MSG1a — the record keeps the person's words and reopens** (service). `said` replaces a single `answer`, and the
  ledger's one move out of an ended state takes words waiting, on this machine's record only. Contract: §2.3, §2.4,
  §5.3's `say`. Proof: `SessionLedgerTests` (stood-down, a teammate's and no words refused), `LocalHostTests`, a pushed
  reopened record in `SessionSyncTests`.
- [ ] **MSG1b — a driven session goes on with words, on both doors** (driver). The planner's `continuing` verdict takes
  a record with words waiting, the judgement gains §2.2's rows and `elsewhere`, and a native run resumes while words
  are held. Contract: §2.1, §2.2, §2.3. Proof: `ContinuationTests`, the plan tests, `NativeResumeTests`, a `Process`
  tick (the parent's).
- [ ] **MSG1c — a chat keeps its conversation and goes on** (driver). A chat keeps its id on both doors, an ended chat
  reopens with the words, and a `promptQueueing` chat takes words at the next step. Contract: §2.1, §2.2, §4.2. Proof:
  `HarnessConversationsTests`, chat tests on a protocol stub, the stop's *not kept* line.
- [ ] **MSG1d — every door's words reach the record** (modules). `SESSION_INPUT` and `SESSION_QUEUE` answer for every
  state, the loop is nudged, said, taken and went events are written, and the log gains its lines. Contract: §3, §5.3.
  Proof: `DriverModuleConversationTests`, `DriverModuleAddedTests`, `SessionEventsTests`, `SessionLogTests`.
- [ ] **MSG1e — `daoris-driver sessions say`** (driver). The verb through the request folder, its one line and its exit
  codes, and Ask Daoris's exemption. Contract: §5.2, §5.4. Proof: `SessionsCommandTests`, `HelpCoverageTests`, the
  room's goldens.
- [ ] **MSG1f — the box on every session that takes words** (web-shell). The box and its sentences per reach, the
  line where none, *going on*, the went link, *Start a conversation…*, and *Send back…* opening the box, in both
  catalogues. Contract: §3.1, §3.2, §5.1. Proof: stories, vitest, the glossary check, the look.
- [ ] **MSG1g — a resume asks for its own account** (driver). The selection names the record's account, a cool-off
  holds the words with *Go on in a new session*, and an account that cannot run there carries on at once. Contract:
  §2.2's account paragraph. Proof: plan tests, `AccountRotationTickTests` (the parent's).
- [ ] **MSG1h — Codex hears words at its next step** (driver; after STEER3's Codex turn, which needs Codex installed).
  The next-step door for `codex-acp` over `_session/steering`, steering only while a turn is live and waiting for a
  turn it started. Contract: §1.3, §2.1. Proof: STEER3's measurement, then `AcpSteerTests` rows.
- [ ] **MSG1i — the native door hears words at its next step** (driver; after STEER3's native turn, with
  `--replay-user-messages`). Driven and chat runs that take words on stdin, which removes the argument's bound.
  Contract: §2.1, §2.4. Proof: STEER3's measurement, then native tests.
- [ ] **MSG1j — the canary on the install** (the parent's, after a republish carrying a–f). A session in *To review*
  written to goes on in its own row; a stopped one too; a chat from yesterday goes on. Contract: §9. Proof: the run,
  `session.reopened` with `resumed` true.
- [ ] **MSG1k — a terminal conversation forked into Daoris** (held until a real use asks). Contract: §4.3. Proof: a
  protocol stub that speaks `session/list` and `session/fork`.
- [ ] **STEER2 — *Send now* on the next-step door, and a box that knows its door** (driver, modules, web; after STEER1).
  *Send now* has nothing to act on where words already reach the next step; send the draft through `_session/steering`
  and let the queue say when words arrive. Contract: D136 §4. Proof: driver, modules and page tests; one steer measured.
- [ ] **STEER3 — the other doors** (driver; after STEER1). A native driven session on `--input-format stream-json`, and
  `codex-acp`'s `turn/steer`, each measured for one turn before it is built. Contract: D136 §5. Proof: the measurement.
- [ ] **ANSWER1 on the install — the plan** (owner, 2026-10-03: *"you need to fix the ANSWER1 bug too"*). The fix is on
  main (ANSWER1a–c, 2026-10-02/03): an answer keeps its session and resumes the agent's own conversation. The install
  still runs the build before it, because a session has been running at every chance to republish. In order:
  1. **Republish** at the first moment no session runs (the watcher on the drill-down quest says when), carrying
     ANSWER1a–c, DRIFT1, TOOL6c and TOOL4g; or, on the owner's word, stop the running one and republish now.
  2. **ANSWER1d**, below: answer the next park on the install and see the same row go on.
  3. **ANSWER1e**, below, and **UPDATE1**, so the next fix never waits on a free moment again.
- [ ] **ANSWER1d — the canary on the install** (the parent's, after the republish). One park answered on the install shows
  one row, the conversation going on, and `session.answered` with `resumed` true. Contract: design §6. Proof: the run.
- [ ] **ANSWER1e — the map and Ask Daoris read an answered park** (web-shell; found by ANSWER1c). The map's *parked* mark
  (`map/topology.ts`) and Ask Daoris's waiting count (`help/machine.ts`) still count an answered park for up to one look.
  Contract: answer-continues design §5. Proof: `topology.test.ts` and the help machine test read `answeredPark`.
- [ ] **UPDATE1 — the install updates when its work allows** (found 2026-10-03: four republishes waited on running
  sessions). *Update when idle*: the desktop starts no new session, lets the running ones end or park, then installs the
  new build and starts again, saying so; a session cut by an update is carried on, never lost. Contract: a short design
  (D62, D93 amended). Proof: the deployment rehearsal's update phase; the look.
- [ ] **DRIFT1c2 — the family rehearsal drives a requirement** (tools; after DRIFT1c). Teach the stub intake to publish
  one quote the person said and to see one they never said refused, through the real host and the sync wire. Contract:
  D133's DRIFT1c note. Proof: the family rehearsal's intake phase.
- [ ] **DRIFT1d2 — the quest page shows the answers and takes the yes** (web-shell, service, driver; after DRIFT1d). Each
  answer and departure with the words it quotes, and *Accept the departure* on a held quest, both languages; Ask Daoris
  proposes the yes as its own card. Contract: D133 §4, its DRIFT1d note. Proof: stories, both catalogues,
  `HelpProposalKindsTests`, `HelpCoverageTests` turning `AcceptDoor` into a door, the look.
- [ ] **DRIFT1e — a follow-up checks against the ask, and a correction goes back to the work** (design first). A closing note
  is the build's account, not the requirement; a correction reopens the parent quest instead of being built under
  *Verify*. Contract: D133 §5. Proof: the design, then its rows.
- [ ] **PAUSE1d — abandon** (driver, modules; after b and c). One listed press that declines the quests with the person's
  reason and discards only what nothing else holds, keeping and naming the rest; `abandoned.json`; its declines send
  `whileOpen` (PAUSE1c), and D132 §5.2 and §13's older-remote sentence takes PAUSE1c's reading. Contract: D132 §3, §4,
  §7.2–§7.3. Proof: `AbandonTests`; a Process case on real git; a family rehearsal phase.
- [ ] **PAUSE1e — on the screen** (web-shell; after b and d). *Pause…*, *Resume*, *Abandon…* with its list and reason on
  the ask's and quest's pages and a session's acts. Contract: D132 §6, §7.1, §8. Proof: stories, vitest, the look.
- [ ] **PAUSE1h — the ask's page shows its work** (web-shell; found by PAUSE1e). Each quest of `WORK_PLAN` with its state
  and sitting reason, the questions its sessions asked, and their sessions as doors into Sessions, so the person sees
  what a pause or abandon reaches before pressing. Contract: D132 §7.1. Proof: stories, `AskPage` vitest, the look.
- [ ] **PAUSE1f — Ask Daoris reaches pause** (driver, service, web-shell; after b and SESSUX1h). The `pause` kind; abandon
  stays the person's. Contract: D132 §7.4. Proof: proposal and coverage tests; the room's golden files.
- [ ] **PAUSE1g — looked at on the install** (the parent's, after a–f). An ask paused mid-session and resumed in its tree;
  one abandoned with a landed session kept. Contract: D132 §12. Proof: a ledger at every width, both themes and languages.
- [ ] **SESSUX1j — a quest's short title** (§6; service, driver, web-shell; any time).
- [ ] **SESSUX1k — waits for an account, in the list** (§2.2; driver, web-shell; after TOOL4c, TOOL4d and c).
- [ ] **SESSUX1l — looked at on the install** (the parent's, after a republish carrying a–i): real sessions by state, a
  parked quest carried on from its session, a stop's hold and Try again, Archive what ended; every width, both themes and
  languages, with a ledger like SESS1's.

### What a session reads and writes (owner, 2026-10-02) — D127

> *"this is not about how we split its about how we optimize the session and this should also belong to doctrine too"*

Found the same day: the archive is 9,400 lines and 125,000 words because its outcomes run 150–300 words where the
canon's `task-lifecycle` asks for **a one-line outcome**, and they repeat the decisions record's notes; this
repository's own `dispatch-subagent` skill asks each subagent for "an outcome paragraph", against the canon. The
backlog, which every session reads whole, is 7,600 words of its 6,600, its rows pasted long. What a session pays for
is what it reads and what it is led to read.

- [ ] **SESSOPT1d — the backlog, trimmed by the rule** (the steward's, after b and c). Each over-long line moves to §4.3's
  home; FLAKE1, TEST1 and REH1 get open fix-log entries. Proof: `doc-budgets` ≤ 5,300 words; no row over 60.
- [ ] **DOC7 — what sessions read, measured** (folded into D127 §6.1): `session.read` and `session.skill` in the machine
  log; the usage report gives reads per role, whole or not.

### A workspace that knows itself (owner, 2026-10-01) — D124

> *"since all is known knowledge it should be able to figure out itself"* … *"its more like leaking of knowledge for
> workspace, so the repo should be registered and apply the doctrine and also initialize the knowledge"*

Measured the same day: the owner's work workspace holds 29 drivable repositories, none adopted, none registered
(`connect` refuses one with no `domain`), 23 with no indexed knowledge at all; the intake router said none declares
what it owns, and a driven session asked the person what its own notes and code could answer.

The contract is `docs/2026-10-01-workspace-setup-design.md`; §8 carries each row's full text and proof. Order: WSSETUP9
first, then WSSETUP11 early (a week of *before*); WSSETUP4, WSSETUP8, WSSETUP10 and WSSETUP2 any time; WSSETUP3 after
TOOLS5 and WSSETUP2. The driver lane runs WSSETUP9 → WSSETUP11 → LAYOUT7 → WSSETUP5 → WSSETUP6; WSSETUP7 after LAYOUT8,
FRAME1e and WSSETUP6. Then the owner's two runs.

- [ ] **WSSETUP7 — the workspace on the screen and in Ask Daoris** (§4.4, §4.5; modules, web-shell, service, driver;
  after LAYOUT8, FRAME1e and WSSETUP6), both languages.
- [ ] **WSSETUP12 — the pilot** (the owner's run, after a republish carrying WSSETUP2, 3, 5, 6, 9 and LAYOUT7): two
  repositories; what each wrote, its review, its cost, its registration, and a canary: a neighbour's session finds
  the answer by search and does not park.
  **Ran 2026-10-02 on the report repository** (quest `#b466447ce4d3`, one turn: 52.3M cached reads, 464K new, 170K out):
  done on its own branch, 7 commits, not pushed; its three knowledge documents cite every fact by file and line. Not
  mergeable as it stands, by three faults of Daoris's own (WSSETUP14): CI and 218 links still read the moved
  `.claude/` paths, which the quest's bounds forbade it to fix; `AGENTS.md` grew to 61 KB because the index lists all
  169 knowledge documents in the always-loaded region (budget raised to 54,000; one agent reads only 32 KB); 166 old
  documents have no frontmatter. The second repository waits for WSSETUP14.
- [ ] **WSSETUP14b — a knowledge folder declared in place** (after 14a). `documents.knowledge` names a folder the index lists
  and the service indexes, which `sync` never writes. Contract: §1.2, §1.3. Proof: the twin tables matched.
- [ ] **WSSETUP14c — a document without frontmatter, by its heading** (after 14a). Contract: §3.1, §3.2. Proof: `node --test`.
- [ ] **WSSETUP14d — the set-up keeps the checks green** (after 14b). Checks first and at the close; knowledge kept in place;
  only a moved path rewritten; never `done` red. As D129 amends it: the close names each hand-written index, deletes none,
  and new documents are named by their subject. Contract: §1.1, §1.4–§1.6, §3.3; D129's review §4.5. Proof:
  `SetupBriefTests`, the playbook twin.
- [ ] **WSSETUP14e — the follow-up for a set-up's branch** (after 14d). *Finish setting up this repository*, with the exact
  merge rule. Contract: §4.2. Proof: composer and press tests; the family rehearsal's set-up phase.
- [ ] **WSSETUP14f — the pilot, finished** (the owner's run, after a republish). Checks green before and after, knowledge
  declared, the root file under 32,768 bytes, the budget back to the default. Contract: §4. Then WSSETUP13.
- [ ] **KNOW3a — the bench at 169 documents, with opaque names** (KNOW3's hand-back). Whether D128's index is still read
  whole at 54 KB, whether a harness shortens a long skill listing, and how the control fares when names say nothing.
  Contract: the bench results §5, §6.1. Proof: a re-run with `tools/knowledge-bench.mjs`; its tests join `verify`.
- [ ] **KNOW2a — the service's own recall, probed** (needed: KNOW3's push ranked by BM25, 5 of 12 in its top 5). Paraphrase
  probes through searches of `INDEX.md` and through `knowledge_search`, recall at 3 and 5. Contract: D129's review §2.G,
  §4.6. Proof: an evidence note and the script's tests; the meaning half is the owner's run (D24).
- [ ] **KNOW2b — headlines in the driven prompt** (after KNOW2a and DOC7). Up to five recalled headlines in the target
  prompt, a slot reserved per tier, nothing when the service is silent. Contract: review §4.6 items 1–3, 5. Proof:
  driver tests; DOC7's knowledge reads before the first edit, before and after.
- [ ] **KNOW2c — a prompt hook for chat sessions** (after KNOW2b and a probe). The composed Claude Code settings carry a
  `UserPromptSubmit` hook adding the same headlines, failing open. Contract: review §4.6 item 4. Proof: the probe on
  both doors; the composed settings' tests.
- [ ] **DOC8a — the decisions record, one file per decision** (the parent's; no branch in flight on the record). The record
  becomes `docs/decisions/D<n>.md`, `docs/DECISIONS.md` a fixed page, and the attributes, the manifest, the dogfood rule
  and the lines naming the record move with it; D119's glued note gets its blank line after the proof. Contract: D134
  §3.1–§3.5, §4. Proof: the concatenation check in the commit body; `verify`; the service suite and family rehearsal.
- [ ] **DOC8c — a folder's record titled by its heading** (service; after DOC8a). The scanner titles a record in a declared
  folder by its first heading, so search shows *D130 — …*. Contract: D134 §3.5. Proof: `RepositoryScannerTests`.
- [ ] **GATE1 — the docs gate is blind at the merge** (found 2026-10-02): the devkit's `docs` gate reads committed dates, so
  a merge that changes the CLI's source without the root README passes the merge tool and fails `verify` once committed
  (TOOL4e did). Proof: the merge tool runs the gate as of the commit it would make, seen failing first.
- [ ] **WSSETUP13 — the rest** (the owner's run): the plan resumed with the pilot's numbers; parks per week before and
  after.

### Plugins: the workshop, Daoris.Plugins and a catalogue (D120) — the build

The contract is `docs/2026-10-01-plugin-distribution-design.md`; §7 carries each row's full text and proof.
Every task in Daoris.Plugins is an ask to it, taken by its own session (the owner's wish: Daoris develops them).

- [ ] **PLUGREPO2e — this repository lets them go** (after PLUGDIST1g): the three plugins Daoris.Plugins now holds leave
  `examples/`, `landing-plugins.test.ts` retires, and the offers are laid out from packages.
- [ ] **WORKSHOP1a — the workshop setting** (§2.1): where Daoris develops a plugin, the home by default, both doors.
- [ ] **WORKSHOP1b — the workshop and its sessions** (§2.2); **WORKSHOP1c — the workshop on the view; Ask Daoris
  makes plugins there** (§2.3, §2.5); **WORKSHOP1d — hand-over and a named source** (§2.4).
- [ ] **PLUGDIST1b — the pack and release workflow** (an ask to Daoris.Plugins); **PLUGDIST1c — a package source over HTTP** (§5.3–§5.8);
  **PLUGDIST1d — the host answers**; **PLUGDIST1e — Find plugins** (§6, after PLUGUI1f).
- [ ] **PLUGDIST1a's leftovers** (its hand-back, 2026-10-01): the modules' plugin page calls a package record
  `folder` with no folder (PLUGDIST1d says `package`); `install` is routed in the host's `Program.cs` ahead of the
  `plugins` catch-all and moves into `PluginsCommand` once PLUGUI1d is on main; extraction has no size bound, so
  PLUGDIST1c bounds it before a package arrives over HTTP.
- [ ] **PLUGDIST1f — the first publish** (the owner's: the nuget.org account, trusted publishing, the prefix), then
  **PLUGDIST1g — the offers from packages**. ⏸ **PLUGDIST1h — the repository signature checked** (held).
- [ ] **INIT1 — `init` writes line endings it can keep** (found setting up Daoris.Plugins): a `.gitattributes`
  with `* text=auto eol=lf` when the repository has none, since Daoris measures its files byte for byte and a
  machine with `core.autocrlf` would check them out as CRLF, as drift.

### Development documents and fewer asks (D122) — the build

The contract is `docs/2026-10-01-development-documents-design.md`; §6 carries each row's full text and proof.
Order: UNBLOCK5 first (a week of *before*); then DOC2, DOC3 and UNBLOCK2 side by side; the driver lane runs
UNBLOCK5 → UNBLOCK2 → UNBLOCK4 → UNBLOCK3 → DOC7. Only DOC2 changes the canon.

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
- [ ] **DOC6 — the example family keeps the standard** (DOC7 is folded into D127).
- [ ] **UNBLOCK6 — the screen and Ask Daoris for a declaration**, both languages; **UNBLOCK7 — codex and dsh
  measured before anything is handed**; **UNBLOCK8 — the first real declaration** (the owner's: asks a week
  before and after).

### Self-managed tools (D121) — the build

The contract is `docs/2026-10-01-tools-design.md`; §7 carries each row's full text and proof. Order: TOOLS2 →
TOOLS3 → TOOLS4, then TOOLS5 ∥ TOOLS6, then TOOLS7 ∥ TOOLS8, then TOOLS9; TOOLS10 before any session runs a
managed git; TOOLS11 last.

- [ ] **TOOLS6 — what Daoris's git carries** (§2.5): the allow-list from `core.sshCommand`, `GIT_CONFIG_GLOBAL` with
  includes, the version floors; and `SessionTrees.Sync.cs`'s sentence "the git Daoris runs (the one on the
  path)" points at Settings → Tools, as the page's copy already does (TOOLS7's hand-back).
- [ ] **TOOLS8 — Ask Daoris's `tool` kind** (§4.3).
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
- [ ] **LAYOUT10 — the first real set-ups** (the owner's run; since D124 no longer after the first publish, and its
  unadopted half is WSSETUP12): one unadopted
  repository, and one with instruction files of its own.

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

- [ ] **TOOL4 — rotation** (owner, 2026-10-01/02: *"this is also good to test for account switch"* … *"a chance to check
  daoris continue"*): **designed as D125** (`docs/2026-10-01-account-rotation-design.md`), which lifts D57 §b's hold:
  five observed limits (design §0.2) answer its three questions. A limit is read from the door's failure by a table
  that grows only with recorded sentences; its account cools until the reset the agent names (60 minutes when it names
  none); a limit is never a strike; the next start runs on the next ready account of the person's order; rotation needs
  accounts of Daoris's own (§3.7). The driver lane runs TOOL4a → TOOL4d → TOOL4e → TOOL4f. **TOOL4d alone ends the
  waste seen on 1 October**: a carry-on started twice into the same refusal and parked its quest in seconds.
- [ ] **TOOL6d — a conversation continues on another account** (driver, modules, web-shell; after TOOL6b). A refused turn
  offers *Continue on* another account, handed the last plan and last words. Contract: D130 §8, §9. Proof: driver, route
  and vitest tests; the look in both languages.
- [ ] **TOOL4l — Ask Daoris's account doors, the service's half** (service, driver; found by TOOL4g). `agent_propose`
  writes `use` (with `use`, `keep`, `early`, `near`), `order` and `ready`, and the setting writer lists `cooloff`, so the
  cards the screen's controls owe can be offered. Contract: D125 §6, D130 §9, §16.6. Proof: the proposal-kind tests with
  the doors listed; `HelpCoverageTests`' owed rows become doors.
- [ ] **TOOL4m — the rest of TOOL4g's screen** (web-shell; found by TOOL4g). *What needs you*'s row for a start waiting for
  an account with *Let … run …* (D130 §3.3), the conversation picker's split (§3.2), and a session head's *your own
  sign-in*. Contract: D130 §3.2–§3.3. Proof: vitest, stories, the look.
- [ ] **TOOL4h — the rehearsal and the report** (tools; §8, §2.2; after TOOL4j): two stub accounts; the usage report's
  limits section per account and window, *Daoris's sessions only*; *in parallel* over N stub accounts (D130 §5.4).
- [ ] **TOOL4i — a real rotation on the install** (the owner's run). *One by one* seen 2026-10-02: a spend limit on the
  work account cooled it and the carry-on opened on the next account (`account.limited`, `account.rotated`), and it found
  the time-of-day reset defect (FIX-LOG). Still owed: *in parallel* over every account, and what one limit cuts off (D130 §11).
- [ ] **AGT3c — two readings that trust what a session printed** (found designing D125): AGT3b's 401 detection reads the
  transcript's last lines, so a session whose own output ends with `API Error: 401` holds its account; and the ACPEND1
  note carries the agent's sentence, zone included, to every machine (`SessionNote.ForAnotherMachine` removes no zone).
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

- [ ] **BRW11 — a page's file reaches the room** (found on AR-2203, 2026-10-01): the intake session read a ticket
  whose mock-up is an attachment, and could not save it into its room; it fell back to a viewport capture,
  cropped. The engine's downloads are its own (CHR3 §3.2), so an agent driving the browser over MCP has no way to
  bring a signed-in page's file to the quest. Design first: where a download lands for a session, and how the
  quest's attachments take it.
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

  **Sighted 2026-10-02** (merging WSSETUP3/5, four branches building at once): `DriverModulePluginsTests.The_kit_makes_a_plugin_where_the_person_names_and_tries_it_or_an_installed_one`
  failed in the modules' Process half and passed alone. And `PseudoConsoleTests.Closing_it_ends_a_child_the_shell_started_too`
  failed twice, alone too: its read of a heartbeat file the child rewrites every 100 ms asked to share only reading and
  met the writer's handle; fixed in that merge (TEST4: read sharing read and write, retried briefly), three runs green.
  **And merging TOOL4e and SESSUX1a**: `HostSupervisorTests.A_host_that_ignores_its_input_ending_is_killed_after_the_bound`
  failed in the modules' Process half and passed alone, under four branches' load.
  **And merging TOOL6b, SESSUX1i and WSSETUP14a** (2026-10-02): `HelpChatTests.Only_Ask_Daoris_loads_its_tools_up_front_and_only_where_the_harness_says_how`
  failed in the driver's Process half and passed alone, with three branches building beside it.
  **And merging ANSWER1b, PAUSE1a and DRIFT1a** (2026-10-03): `ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked`
  failed in the driver's Process half and passed alone, with four branches building beside it.
  **And merging ANSWER1c, DRIFT1b and TOOL4g** (2026-10-03): `AccountGoalTickTests.One_look_spreads_K_starts_over_N_accounts_and_a_limit_cuts_off_only_its_own`,
  `IntakeTests.An_ask_with_an_intake_harness_is_answered_by_a_session_that_publishes_onto_it` and the modules'
  `DriverModulePluginsTests.The_kit_makes_a_plugin_where_the_person_names_and_tries_it_or_an_installed_one` failed in the
  full runs and passed alone, with three branches building and two real sessions running on the install.
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
