# Daoris (道衍) — Active Task Backlog

> **This file holds OPEN tasks only.** A finished task is **removed from here and appended to
> [`docs/task-archive.md`](docs/task-archive.md)** with its date and outcome — never ticked in place.
> `CHANGELOG.md` is the release-facing log; the archive is the per-task record. The contract is
> `docs/2026-08-04-daoris-design.md`; the forward sequence is [`ROADMAP.md`](ROADMAP.md).

**Goal:** one canonical set of agent-facing rules and knowledge, materialized into every repository in
the family, kept from drifting, and improved from wherever the improvement was found — and, as of D45,
**Daoris as the driver**: the workflow manager that triggers and coordinates the agent sessions doing
the family's work.

**Every arc before D76 is closed and in the archive**: the driver (D45), the remote (D47),
workspaces, the interactive surface and management parity (D48–D50), the working surface (D51–D56),
the protocol door (D53), the toolchain (D57, bar TOOL4 and TOOL5), the instruction file (D59), the
first deployment (D60–D63), plugins (D64), the remote as a git remote (D68), permission scopes
(D72–D74) and the menus (D75). [`docs/README.md`](docs/README.md) names each arc's contract. Two
reviews read the code rather than re-running it, REV2 and REV3, and CLEAN1 settled REV3's cleanup
lists.

## State

**Counts, and this is their one home:** fifteen commands, **478 CLI tests, 502 service, 696 driver,
129 desktop modules, 80 devkit, 1026 web unit, 21 Playwright**, 66/66 release rehearsal, **279/279
family rehearsal** (it names its own phases when you run it), **39/39 deployment rehearsal** (D60),
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

🔴 **The live arc is D76, the conversation** (owner, 2026-09-25), taken in the order under *The
reference gap* below. Nothing is pushed or published, and a release is still blocked on REH1.
- **Landed:** CONV1 (the record), CONV2 (the view), CONV3a (Claude Code's `stream-json`), CONV3b
  (chats on the protocol door), CONV4a (stopping a turn, one queue on both doors), CONV4b (the
  composer's turn, queue and drafts), CONV4c (attachments), CONV4d (`@` a file in the tree) and CONV5
  (the meters), with the fixes the looks found. All are in the archive, under 2026-09-25 and
  2026-09-26.
- **REV3, the owner's full review, is closed**, and so is **CLEAN1**, its cleanup lists (both in
  the archive). The ledger is `docs/2026-09-25-rev3-review.md`, and what they left is under *What
  REV3 left* below.
- 🔴 **Next: FRAME6, the frame.** A resizable, collapsible rail and a resizable dock with tabs per
  session, at the geometry components §3a adopted.
- **How each landing is checked:** TDD, the gates, then a look on the window with a real session.
  Real sessions on this machine's Claude Code account are authorised (2026-09-24).
- **The scratch machine:** its `driver.json` points `claude-code-acp` at the ACP adapter the dsh
  probe installed under `_fixtures/dsh/npm` (0.79.0), which is how a protocol-door chat is looked
  at without installing anything.
- **Open beside it:** FLAKE1 (an intake test, about 1 run in 20), DEPLOY5 (the artefact gate
  holding a chat at close) and TEST1 (the Playwright worker's `0xC0000409`, seen a second time).
- **The owner's to spend or attend:** TRUST2's first grant, AGT2c (two downloads, not authorized)
  and INT6. **Six wait on a trigger:** TOOL4 on TOOL3's transcripts; TOOL5, CANON9 and HARNESS1 on a
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

**Twenty-seven rows are open**: D76's four and its held file tools; the ten REV3 left, with RETRY1;
three leftovers (FLAKE1, DEPLOY5, TEST1); three on the owner (TRUST2, AGT2c, INT6); and six on a
trigger (see *Handover*). Every closed one is in `docs/task-archive.md`, and this file holds no
ticked rows, by the `task-lifecycle` rule it also ships. A heading below holds open rows only.

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
- [ ] **TIER1 — which tier answered, per answer** (service F13, D24). `HybridKnowledgeSearch`
  swallows either half's failure, and the tools report the configured tier (`SemanticEnabled`)
  rather than the one that answered. With the embedder down, a search still says `lexical +
  semantic`, and if both halves fail, nothing matching and nothing answering look the same. The
  search result has to carry its tier.
- [ ] **HTTP1 — the HTTP host under test** (service F19). The service suite references Core and the
  MCP host only, so the shared gate, the key check and path stripping are exercised by one
  rehearsal route each. The fix is a `WebApplicationFactory` suite over shared mode's doors.
- [ ] **SIGNIN1 — a sign-in outlives leaving the Agents domain** (web-rest F4). The running action
  is the domain component's state, and `HARNESS_ENDED` is heard only while it is mounted. Leaving
  mid-login loses the code panel and the end notice. Lift the running action above the domain.
- [ ] **WINDOW1 — a secondary window follows scope and theme** (web-rest F8). Both are read from
  `localStorage` once, and nothing listens for a `storage` event, so a detached session keeps the
  workspace and theme it opened with.
- [ ] **RETRY1 — the screen's door for retrying a parked quest** (CLEAN1, D50). A quest parked by its
  strikes is retried from a terminal (`daoris driver retry`), and the page has the other half ready
  (`useRetryQuest`, and the session's `forgiven`) with nothing rendering it. Put the retry where the
  parked quest is shown, with its test.

### What an agent may do — permission scopes (owner, 2026-09-24 → D72, D73)

Measured: rules and hooks Daoris hands over at spawn reach an untrusted session on both doors, and a
repository's own allow-list does not (`docs/2026-09-24-deploy1-acp-trust-evidence.md`).
- [ ] **TRUST2 — what D73 leaves unmeasured.** Whether Claude Code honours a trust key Daoris wrote
  as its own; whether a trusted parent covers a child, since the hold matches the exact folder; and
  whether the screen should offer a grant before any hold exists (INT6's onboarding). The first
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

### The regular task — an ask becomes quests (owner, 2026-09-23 → D65)

`docs/2026-09-23-intake-design.md` is the contract, and every INT item but this one is archived.

- [ ] **INT6 — onboarding the named workspace.** `import`, a declaration per repository, the first
  real ask — owner present, adoption playbook.

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
- [ ] **FRAME6 — the frame.** A resizable, collapsible rail (264–420px, 56px strip) and a resizable
  dock (45% default, 70% cap) with tabs per session, deterministic close (components §3a).
- [ ] **RAIL1 — the list.** Search sessions by name and by content, and a row menu.
- [ ] **REVIEW2 — review.** Highlighted diffs, split or unified.
- [ ] **UX5 — screen by screen.** Every surface, every state (empty, loading, error, long, 中文,
  dark), every piece of interaction logic (keys, focus, what a click opens, what survives a reload),
  against the reference and D41. Written down as it is found, and fixed. **Already found, to settle
  there:** a session waiting on the person wears declined's red (`Dot tone="parked"`) in the rail,
  the band and the ask card, while the map uses the warn tone for the same fact; one hue for
  "waiting on you" everywhere. The monitor's tiles are console-only and say *Nothing said yet* for a
  session whose conversation is kept. The protocol door's console writes each streamed chunk of a
  message as its own line (`…document` / `, named in the README.`), so the raw view breaks words
  across lines, driven sessions included. The native door renders a whole message once, and this one
  should too; the ACP session's two line-from-event paths (`Render` and `Map`) are where to make them
  one (CLEAN1). An agent's one-item-per-line answer renders as one paragraph, because Markdown makes a
  single newline a space (seen on the window, CONV4b). StartSession's three selects and checkbox, and
  DiffFileRow's checkbox, are native controls beside `ui.tsx`'s own (CLEAN1). And
  `docs/2026-09-19-platform-ux.md` §4 has grown a dated amendment per pass: state the rules they
  settled in the body they amend as each surface is settled (CLEAN1).
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
