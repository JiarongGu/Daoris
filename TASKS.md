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

**Counts, and this is their one home:** sixteen commands, **592 CLI tests, 620 service and 45 HTTP host, 1184 driver,
385 desktop modules, 80 devkit, 1631 web unit, 21 Playwright**, 66/66 release rehearsal, **301/301
family rehearsal** (it names its own phases when you run it), **67/67 deployment rehearsal** (D60),
and 5 universal devkit gates over this repository (DEVKIT3). Canon: 8 core rules, 5 knowledge
documents, 5 skills, 7 packs. The always-loaded core is **22,492 of 26,000 bytes** — a span in
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
by subagents in their own worktrees and merged, rehearsed and looked at on main. **Next:** LOG2 and
HELP7 (what the log and the helper showed), then the owner's calls.
- **The owner's ticket AR-2201 is landed** on its feature branch in the repository that owns it, not
  pushed; the pull request is theirs, and so are two calls the verify step raised (a rounding change
  in a shared helper, and a production config update). Two landed branches of the ticket before it
  wait for the owner to delete (the permission policy refused a forced branch delete). The private
  notes name them.
- **The install** runs the latest main; republishing is the session's own call, never while a session
  on it runs. Start it the normal way, not through the dev tool, unless the instruments are needed
  (USE1g is a session shell's PATH seen once under a dev-tool start).
- **Load makes flakes**: FLAKE1's real-tick classes fail under parallel builds and pass alone. Run the
  rehearsals when nothing else builds.
- **Open and the owner's:** READACROSS1, HELPREAD1, TRUST2, AGT2c, DIST1, BUDGET1, HOME1; on a trigger:
  TOOL4, TOOL5, PLUG7, CANON9, HARNESS1, REH1. **A new direction from the owner outranks all of them.**

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

**Twenty-four rows are open**: the day's three (USE1's last part to confirm, PLUG8, PLUG9),
first; the three REV3 left (DIST1, BUDGET1 and
HOME1, the owner's); the in-app browser's one (BRW3); D85's one (TASKBAR1); the first
goal's four (FG5, READACROSS1 and HELPREAD1 the owner's, and SEM2 on a trigger); D76's held file tools;
three leftovers (CONSOLE3, FLAKE1, TEST1); two on the owner (TRUST2, AGT2c); REH1; and the rest on a
trigger (TOOL4, TOOL5, PLUG7, CANON9, HARNESS1). Every closed one is in `docs/task-archive.md`, and
this file holds no ticked rows, by the `task-lifecycle` rule it also ships. A heading below holds open rows only.

### Watch it, type into it, set it up (owner, 2026-09-30) — in this order

> *"we also need to make input line for console too, so we can control console just like regular
> console window (more into powershell style), and also we will need a setup guide for first use
> daoris, so setup agent and agent for "daoris" (the main agent for daoris system) and other rules
> … also setup proper logging system to moniter my use in local daoris and we can improve the
> system by this way"*

Logging went first (LOG1a landed); then the owner's later asks the same day came before the rest of
it: their own workspace's work (WORK1), then the kit (SHEN1), both archived; then what they met on the window.
Each row's parts land and are archived one by one (*"for all my request you can set them into
TASK.md and complete one by one"*).
- [ ] **USE1 — what the owner met on the installed window, 2026-09-30** (*"since I tried to use update
  but got error message no updater"*; *"委托 screen box is overflowing the window, also completed quest
  not been cleared, also new request is not auto firing"*). (a) **Update on a door with no updater**:
  the Agents surface offers *Update* on every present door, and `claude-code-acp` (an npm package
  Daoris pins) declares none, so the only outcome was the refusal. Update should do what it says:
  the tool's own updater where it has one, the pin moved to the newest release where Daoris pins it,
  and no button where neither applies. (b) **The Quests view overflows the window.** (c) **A completed
  quest is not cleared** from the view. (d) **A new request does not start on its own.** Each is read
  off the install before it is fixed. *Read, 2026-09-30:* (b) is the desktop only: every view other
  than Sessions is drawn inside the work frame, whose root is a flex item with no `min-w-0`, so a long
  title that should truncate widens the frame past the window; a browser draws the view outside the
  frame and fits. (c) is the asks: an ask stays *published* after every quest it became has closed,
  so the list never empties. (d) is a hold: both quests were addressed to a held repository, and
  nothing on the quest's card says so (the drawer does). (e) *Added the same day:* **the right side
  bar's size breaks after Ask Daoris is moved to the panel and back** (*"after dock "ask daoris" from
  right to bottom and back to right the sizing of right panel is broken"*) — reproduced on the scratch
  window before it is fixed; (b)'s frame overflow is the first suspect. *(b), (d) and (e) landed
  2026-09-30 (one defect for (b) and (e)), then (a), (f) and (c), all in the archive; (g) is to confirm.* (f) *Found the same
  day, holding the ticket's verify step:* **the desktop does not find an npm-installed agent on
  `PATH`.** The owner updated `claude-code-acp` with npm (0.84.0, `claude-agent-acp.cmd` in the Node
  folder) and unpinned it; the CLI's `agent list` found it, and the desktop's driver held every start
  with *"claude-code-acp is not installed on this machine"*, because it starts the bare command and
  Windows then looks for an `.exe` only. The plugin door already resolves `PATHEXT` (`Plugins.cs`);
  the harness door must too, for the probe and the spawn. Unblocked for now by pinning 0.84.0.
  (g) *Seen once, to confirm:* **a driven session's own shell had no `git`, `tr` or `head` on its
  `PATH`** (the ticket's verify session on `claude-code-acp` 0.84.0, the install started by
  `desktop -- run --install` from a Git Bash shell): its agent worked round it by exporting Git's
  folders in each command. The session before it, on 0.79.0 and a normal start, committed with git.
  Check on a normal start before anything else, then decide whether the shell's environment or the
  adapter's release is the cause. *Narrowed, 2026-09-30:* Claude Code's own shell snapshot for that
  session holds `PATH` in **Windows form** (`C:\…;C:\…`) in a bash `export`, so no entry resolved,
  not even System32; the two sessions before it hold POSIX form. The driver sets no `PATH`
  (`Harnesses.Apply`). Still confounded: the start (Git Bash hands on `PATH` upper-case with MSYS's
  variables; a normal start hands on `Path`) and the bundled Claude Code (2.1.274 in 0.79.0, 2.1.284
  in 0.84.0). The next session on the normal start answers it: read its snapshot's `export PATH`.

### Plugins Daoris makes (owner, 2026-09-30)

> *"do we need a Daoris.Plugins repo? … I think Daoris should have ability to create plugin by itself,
> say for example via "ask Daoris""*

The reading, recorded before anything is built. A plugin is code that runs on this machine as the
person: WSR4's pushes with their git and platform sign-in. So **making one is work, not a setting**.
Written by a session in a repository, with tests, reviewed as a diff and landed; installed only by the
person's press, with the card naming what it runs and where it speaks. Ask Daoris is the door in, not
the author: it has no shell and changes nothing itself (HELP4), and a plugin it wrote in one tool call
would run untested with the person's credentials. **A plugins repository** is then the natural home for
what Daoris makes: versioned, reviewed, shareable with a team, owned by its own agent like any
repository. It is not a registry (D64) and not a home for Daoris's own examples: those are the wire's
contract and the rehearsals' fixtures, so they stay here. Whether to make one, and where, is the owner's.
- [ ] **PLUG8 — the kit a session makes a plugin with.** (a) `daoris plugin new <id> --point <p>…
  [--harness]`: a folder with a manifest, a wire script answering the handshake and the named points,
  and its test, on both doors (Settings → Plugins → New). (b) `daoris plugin try <folder> --point <p>
  [--frame <file>]`: starts it as the driver would, speaks the handshake, one frame and the shutdown,
  and checks the answer against the point's result shape (for `work/land`: `pushed`, `pullRequest`,
  `message`). A gate a session can run before a person reviews. (c) The authoring knowledge a session
  reads: the points, their frames and answers, the rules (no stdout but frames, data in `${data}`, a
  `.cmd` tool's quoting on Windows). It lives with the kit, in the plugin design document or a
  knowledge document, not the canon: it is Daoris's, not every repository's.
- [ ] **PLUG9 — Ask Daoris makes one by asking for it, and installs it by a press.** (a) The room says a
  plugin is made as an ask at the workspace holding the plugins repository, and proposes that ask; it
  never writes one. (b) A new proposal kind, `plugin`: add a landed plugin from its folder, enable or
  disable one. It is judged by the catalogue's own reader, and the card shows the id, the command it
  starts, and its points, harnesses and servers, so the person sees what will run before Apply. (c)
  Installing from a repository's folder remembers the source, so the plugin can be updated when its
  repository lands a change. (d) The install carries Daoris's own example plugins as offers in
  Settings → Plugins and in the room, never installed until a press (D87: off by default).

### What REV3 left (2026-09-25)

REV3 is in the archive, and `docs/2026-09-25-rev3-review.md` is its ledger. These rows are what it
found that is not session-sized, or is the owner's call. Each one was confirmed in the code.

- [ ] **DIST1 — how a consumer installs Daoris** (docs F1; the owner's call). The README's
  `npx github:JiarongGu/Daoris#v0.0.1 …` cannot run: the root package is a private workspace with no
  `bin`, and no tag exists. Choose npm's `daoris@X` (the release workflow already publishes it) or a
  git ref with a root `bin`; the README, `init`'s written `source`, `release-prep` and
  `version.test.ts` then move together.
- [ ] **BUDGET1 — what the core budget caps** (CLI F10; the owner's call). Since D59 `inspect` counts
  only the body of Daoris's `AGENTS.md` region. The repository's own always-loaded material is not
  counted: the rest of `AGENTS.md`, `CLAUDE.md`, a local `.claude/rules/` file. The README and the
  instruction-file design still say the budget guards it, and `analyze` projects the pre-D59
  quantity (`config.ts` now says so). Decide what the number caps, then move all three together.
- [ ] **HOME1 — which home a second install uses** (modules F4; the owner's call).
  `InstallHome.Establish` defers to a `DAORIS_HOME` already in the environment, and the first
  install set one for the account. So a moved or second install runs on the first one's `data/`
  and says nothing. Either the install's own `data/` wins, or the inherited home wins and the shell
  says so.
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
### Chromium under the windows (owner, 2026-09-28 → D85)

> *"to shift to chromeiun, because we mostly build the ui itself in react and the only missing part
> is the shenora currently dont support this … we can start the work here and also file the new task
> to shenora for this"*

`docs/2026-09-28-chromium-host-design.md` is the contract, and its §5 carries each row's detail.
Shenora's backlog carries the request (filed 2026-09-28 at the owner's say-so, uncommitted there).
CHR1–CHR7 are in the archive: **the browser is `daoris-browser`, the engine's own window in a
process of its own**, and **the window renders on the Chromium the install carries** (CHR2, D92), in
an install that is a `Daoris.exe` launcher, the application under `app/` and the home in `data/`
(CHR4, D93). Agents open tabs in it through a relay, and Daoris's favorites
and the extensions setting are in Settings → Browser and `daoris browser`
(`docs/2026-09-28-chromium-embedding-evidence.md`).
- [ ] **TASKBAR1 — one taskbar button for a pinned Daoris** (D93). The window belongs to
  `app/Daoris.Desktop.exe` and a person pins `Daoris.exe`, the launcher, and Windows groups a button by
  its process's executable unless the window names an application id. Expected, not yet seen: a pinned
  launcher beside a second button for the running window, and pinning the running window pins the app
  in `app/`, which the next publish moves under it. The window's property store can name the id and
  the relaunch command (`System.AppUserModel.ID`, `RelaunchCommand` pointing at the root launcher, its
  display name and icon); look on the owner's machine before and after.
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
- [ ] **READACROSS1 — may a session READ another repository?** (the owner's call; FG5, 2026-09-27).
  Under D81's `auto` mode the apply session read the backend's controller, its roles and its
  service wiring to diagnose a 403, cited them by file and line, and wrote nothing there. D79 says
  what another repository knows is asked of it, and the owner said then *"it should request to
  [the backend]"*. The owner has since said a session should do *"as much as it can just like
  regular claude code"*. Decide: reads across are fine and writes never, or reads are asked too. The
  answer is a handed rule, and a sentence in the instruction.
- [ ] **HELPREAD1 — may Ask Daoris read a checkout?** (the owner's call; HELP4, 2026-09-30). The help
  room reads the family's knowledge and quests and nothing else (D89): its first real repository
  question (clean up a repository's branches) opened with a shell command, refused by construction.
  HELP4 made the room say so and route such work to that repository. Decide whether it may also read
  a registered tree: `git status`, a branch list, a file. That widens D89's allow-list, and the
  question is READACROSS1's, asked of a helper that belongs to no repository.
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
- [ ] ⏸ **Held, after the conversation:** a file tree and document preview in the dock (when a tool
  card wants to open a file); a terminal (design §6's trigger).

### Open — the arc's leftovers, in the order they are worth doing


- [ ] **CONSOLE3 — what the console's tabs left** (CONSOLE2, 2026-09-28; the evidence is
  `docs/2026-09-28-console2-streams-evidence.md`, and for the native door
  `docs/2026-09-30-console3-native-streams-evidence.md`). One left (CONSOLE3a–c are in the archive):
  **whether a driven session waits for its own background work** before it closes. Today it does not
  on either door: the protocol door ends that work with the session (ORPHAN1's job object), and the
  native door's binary kills it itself when its turn ends (the probe). Waiting is a choice about how
  long a session holds its tree, so it is the owner's to make. A stop against the real adapter is
  unseen: the request's shape is from its source.

- [ ] **FLAKE1 — an intake test failed once in about 20 full driver runs.**
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
  green alone three times running.

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

- [ ] **CANON9 — `desktop-winforms`, the last pack candidate.** One 11 KB source, one repository —
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
- **Work an arc leaves behind goes in the backlog as a row**, never as a handover sentence — that is
  how work quietly stops being work.
