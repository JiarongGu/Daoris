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

**Counts, and this is their one home:** sixteen commands, **514 CLI tests, 546 service, 910 driver,
292 desktop modules, 80 devkit, 1399 web unit, 21 Playwright**, 66/66 release rehearsal, **292/292
family rehearsal** (it names its own phases when you run it), **45/45 deployment rehearsal** (D60),
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

🔴 **The owner set the direction again on 2026-09-27: the first goal (D77).** A real workspace, a
ticket read through a browser, and a task started with no repository named. FG1–FG4 landed the
code changes the study and the setup found. **FG5, the first run, is next**, and waits on the owner.
🔴 **Then the owner asked for Daoris's own browser (D78)**, so the sign-in happens in a window Daoris
owns and every harness can drive: **BRW1–BRW2 landed, and BRW3, the owner's sign-in and first ticket through it, comes first**, and FG5's ticket is read through them.
🔴 **The first run showed a session guessing what another repository knew, so D79 landed: ask and
wait** (ASK1–ASK3, in the archive). A session publishes its question, parks its taken quest on it,
and is resumed in the same tree with the answer. **ACPEND1 landed with it**: the same run's account
limit had been recorded as a stand-down. **The second run then met the first goal**: a ticket read,
routed, developed, gated and closed done with no repository named. That needed ASKAGAIN1 and D80 (a
cut-off carried on), both in the archive. The chain's verify step found three more. ORPHAN1 is fixed
(in the archive). **CHAIN2 has the owner's answer**: a same-repository step starts on the parent's
branch. The owner then set two more directions, **D81**: a session works in its harness's own `auto`
mode, "as much as it can just like regular claude code". And **parallel sessions (PAR1)** and **a
console tab for everything running (CONSOLE2)**. CHAIN2 landed as D82, PAR1 with it, STANDDOWN2 as
D83, and CONSOLE2 and BRW10 (a sign-in that survives a restart) on 2026-09-28, all in the archive.
**Then the browser as a browser** (BRW4–BRW6), until **D84 and D85** (2026-09-28): Daoris ships a
Chromium for its browser and, once Shenora's host is released, for its page (CHR2 waits on it).
**CHR1, CHR3, CHR5, CHR6 and CHR7 landed: the browser is `daoris-browser`, the engine's own window.**
Next is one republish, once the owner has closed Daoris (it adds about 350 MB, and the person signs in
again). 🔴 **The owner also
filed six rows from the first real workspace** (2026-09-28, *After the first real workspace*, "lets
start them later"): workspace rules for how work lands, the default branch, cleaning up session
branches, the session view, the map, and asking Daoris for help. Ask which comes first.
🔴 **Where the 2026-09-27 session stopped (the owner: "lets continue in next session").** The first
goal's loop is met (FG5's sixth leg). 🔴 **Republishing is the session's own call** (the owner,
2026-09-29: *"you can always republish if you need"*): the install keeps its sign-in across a
restart (BRW10). The one rule left is the app's: a republish closes it, so never while a session on
it is running; a parked one has no process and survives. **The owner has five open calls:**
the verify step's camera-GUID defect, sent to the front end as a quest before any camera note is
saved; whether the daily view gains a note chip; the single-select answer said back on the ticket;
merging four docs commits left on session branches, in both repositories; and READACROSS1. The
private notes name the branches and ids.
D76's round is done, and its leftovers (RAIL2, SURF11) stand behind these. Nothing is pushed or
published, and a release is still blocked on REH1.
- **Landed:** CONV1 (the record), CONV2 (the view), CONV3a (Claude Code's `stream-json`), CONV3b
  (chats on the protocol door), CONV4a (stopping a turn, one queue on both doors), CONV4b (the
  composer's turn, queue and drafts), CONV4c (attachments), CONV4d (`@` a file in the tree), CONV5
  (the meters), FRAME6 (the frame), RAIL1 (the list) and REVIEW2 (review), with the fixes the looks
  found. All are in the archive, under 2026-09-25 and 2026-09-26.
- **REV3, the owner's full review, is closed**, and so is **CLEAN1**, its cleanup lists (both in
  the archive). The ledger is `docs/2026-09-25-rev3-review.md`, and what they left is under *What
  REV3 left* below.
- **UX5, screen by screen, is closed** (2026-09-26, in the archive). Its ledger,
  `docs/2026-09-26-ux5-screen-audit.md`, holds every finding with its disposition and, at its head,
  what the instruments need to look at the window; the rules it settled are in the body of
  `docs/2026-09-19-platform-ux.md`. **The next direction is the owner's to set.**
- **How each landing is checked:** TDD, the gates, then a look on the window with a real session.
  Real sessions on this machine's Claude Code account are authorised (2026-09-24).
- **The scratch machine:** its `driver.json` points `claude-code-acp` at the ACP adapter the dsh
  probe installed under `_fixtures/dsh/npm` (0.79.0), which is how a protocol-door chat is looked
  at without installing anything.
- **Open beside it:** FLAKE1 (an intake test, about 1 run in 20), DEPLOY5 (the artefact gate
  holding a chat at close) and TEST1 (the Playwright worker's `0xC0000409`, seen a second time).
- **The owner's to spend or attend:** TRUST2's first grant, AGT2c (two downloads, not authorized)
  and FG5's sign-in and ticket. **Six wait on a trigger:** TOOL4 on TOOL3's transcripts; TOOL5, CANON9 and HARNESS1 on a
  repository naming what it wants; REH1 on a captured recurrence; PLUG7 on a plugin asking for a
  service-side point. **A new direction from the owner outranks all of them.**

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

**Thirty-two rows are open**: the two the first real workspace showed (WSR4 and MAP4; WSR2, WSR1
and WSR3 landed as D86, D87 and D88, HELP1 as D89, SESS3 as D90, and SESS1, SESS2 and DOCK1 are in
the archive); D85's two (CHR2, CHR4); the in-app browser's three (BRW3, BRW7, BRW8); the first goal's three (FG5,
READACROSS1 the owner's, and SEM2 on a trigger); D76's held file tools;
the eight REV3 left, WINDOW2 among them; five leftovers (RAIL2, CONSOLE3, FLAKE1, DEPLOY5, TEST1); two on the owner
(TRUST2, AGT2c); and six on a trigger (see *Handover*). Every closed one is in `docs/task-archive.md`, and this file holds no
ticked rows, by the `task-lifecycle` rule it also ships. A heading below holds open rows only.

### After the first real workspace (owner, 2026-09-28) — to start later

> *"I also found issues after the [first workspace] work, which points out things that daoris leak
> of … you can analyze and put those into TASKS.md and lets start them later"*

`docs/2026-09-28-after-the-first-workspace.md` is the study: what each part has today, the gap, the
requirements, and the owner's calls. Its recommended order is the order below.
- [ ] **WSR4 — a plugin lands work: push and open a pull request** (D87). The landing rule names a
  plugin, and the plugin, for its platform, pushes the branch and opens the pull request over its
  wire (D64). Off by default, and configurable per workspace. WSR1's branch form is what it starts
  from: `LandingRules.Problem` refuses any other form today, and says so.
- [ ] **MAP4 — the map, finished** (study §6). MAP4a (the layers, pan, zoom, search and the sizing
  menu; map design §1a), MAP4b (asks and chains as lines of their own, switchable; §1b), MAP4c
  (open only, or what moved in 7 or 30 days; §1c) and MAP4d (a line live while a session is on one
  of its quests; §1d, its ring not yet seen on the window) are built. Still open: **e** what
  depends on what, from a declared source and never guessed (a new declaration, so the CLI and the
  service change as twins). MAP4a was looked at on the real
  twenty-nine (dark, the install) and a scratch circle of twenty-three (both themes).
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
- [ ] **HOSTID1 — the shell adopts whatever answers `/api/status`** (modules F7). `HostSupervisor`
  takes any process answering on the service port as this machine's host, and hands its page the
  full bridge. The shell needs a way to tell its own host from another process, such as a token it
  passes at spawn, or the install path the host reports.
- [ ] **REFUSE1 — the refusal rule, enforced** (modules F10). `Refusals.All` is kept by hand, so a
  code left out of it escapes the catalogue test, and nothing checks that a throw site uses a
  declared code. Enumerate the constants by reflection and scan the throw sites.
- [ ] **HTTP1 — the HTTP host under test** (service F19). The service suite references Core and the
  MCP host only, so the shared gate, the key check and path stripping are exercised by one
  rehearsal route each. The fix is a `WebApplicationFactory` suite over shared mode's doors.
- [ ] **SIGNIN1 — a sign-in outlives leaving the Agents domain** (web-rest F4). The running action
  is the domain component's state, and `HARNESS_ENDED` is heard only while it is mounted. Leaving
  mid-login loses the code panel and the end notice. Lift the running action above the domain.
- [ ] **WINDOW2 — a secondary window's caption follows the chosen theme** (found closing WINDOW1,
  2026-09-27). Since WINDOW1 the page inside a monitor or a detached session follows the viewer's
  choice live, and its native title bar stays on the OS theme, so a dark choice on a light OS shows a
  dark page under a light caption. That was seen on the window. `SecondaryForm` follows the OS
  directly, because `WindowCommandModule`, the main window's `SET_THEME` channel, targets one form,
  and its module name is reserved and singular (D55 §b). The same caption showed before WINDOW1,
  when a window opened on a chosen theme. The fix needs a channel for the secondary window's own
  frame: the page's `setTheme` there, and a handler bound to that form. That is the shell's code,
  and Shenora's command module may need to grow.

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
- [ ] **BRW7 — start it from the app, and route links to it.** A door as visible as the monitor's:
  the activity bar or the strip, not only View → *Browser*. A setting for whether links on the page
  (a ticket in a quest, a URL in the conversation) open in Daoris's browser or the system browser,
  orca's link routing. It opens whichever browser the machine uses (CHR3, BRW12), on that
  browser's own start page.
- [ ] **BRW8 — who is driving.** Say when a session is driving the browser, name the session, and
  link to it, so the person knows whose hands are on the page before they type into it. **Measured
  with BRW4 (2026-09-28):** an agent's current tab is the first page it found, not the one in front.
  In a real browser its own tabs are visible (D84's evidence). Neither the engine's window (CHR3) nor
  Edge's is Daoris's to draw in, so it is said in Daoris (the session, the strip) or by an extension.

### Chromium under the windows (owner, 2026-09-28 → D85)

> *"to shift to chromeiun, because we mostly build the ui itself in react and the only missing part
> is the shenora currently dont support this … we can start the work here and also file the new task
> to shenora for this"*

`docs/2026-09-28-chromium-host-design.md` is the contract, and its §5 carries each row's detail.
Shenora's backlog carries the request (filed 2026-09-28 at the owner's say-so, uncommitted there).
CHR1, CHR3, CHR5, CHR6 and CHR7 are in the archive: **the browser is `daoris-browser`, the engine's
own window in a process of its own**. Agents open tabs in it through a relay, and Daoris's favorites
and the extensions setting are in Settings → Browser and `daoris browser`
(`docs/2026-09-28-chromium-embedding-evidence.md`).
- [ ] **CHR2 — move onto Shenora's Chromium host** (`Shenora.Windows.Chromium`, which the kit builds:
  the contract's §3). **Waits on its release.**
- [ ] **CHR4 — the install carries it**, and the WebView2 path goes.

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


- [ ] **RAIL2 — a live chat's last move is its last turn** (UX5 U62). The rail and the head say
  *moved* from the session record, which moves on state changes only, so seconds after an answer a
  chat read *idle · moved 4m ago*. A per-turn record write is the wrong fix: records sync, and it
  would carry a chat's activity to a teammate's machine (D47 §4). The driver already knows when each
  chat's turn ended (`ChatRunner`); its `SESSION_QUEUE` answer and `SESSION_QUEUED` event could carry
  it, machine-local, and the page could show the later of the two. The driver, the bridge and the
  page change together, each with its test.

- [ ] **CONSOLE3 — what the console's tabs left** (CONSOLE2, 2026-09-28; the evidence is
  `docs/2026-09-28-console2-streams-evidence.md`). Four, each its own landing: **stop a background
  task from its tab**, which the adapter takes as `_session/async_task/stop` (`canStop: true`);
  **the native door's streams**, since only the protocol door asks for them and no probe has read
  `stream-json`'s `parent_tool_use_id` or its task messages; **tabs in the detached session window**,
  whose console is still the session's alone; and **whether a driven session waits for its own
  background work** before it closes, since today that work ends with the session.

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
  passed 862/862. The cleanup deletes before the process has let go. **Seen again 2026-09-29**: one
  failure in a full run of 898 during HELP1c, not named because only the summary line was kept; the
  next three full runs passed 898/898. Keep a full run's whole output, so the next one names itself.

- [ ] **DEPLOY5 — the artefact gate holds a chat open at close.** The fix for a chat left `working`
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
