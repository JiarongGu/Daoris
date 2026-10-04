# Daoris (道衍) — Active Task Backlog

> **This file holds OPEN tasks only.** A finished task is **removed from here and appended to
> [`docs/task-archive.md`](docs/task-archive.md)** with its date and outcome — never ticked in place.
> `CHANGELOG.md` is the release-facing log; the archive is the per-task record. The contract is
> `docs/2026-08-04-daoris-design.md`; the forward sequence is [`ROADMAP.md`](ROADMAP.md).

**Goal:** one canonical set of agent-facing rules and knowledge, materialized into every repository in
the family, kept from drifting, and improved from wherever the improvement was found — and, as of D45,
**Daoris as the driver**: the workflow manager that triggers and coordinates the agent sessions doing
the family's work.

Every closed arc is in the archive, and [`docs/README.md`](docs/README.md) names each arc's contract.

## State

**Counts, and this is their one home:** seventeen commands, **1170 CLI tests, 1124 service and 67 HTTP host, 4626 driver,
704 desktop modules, 80 devkit, 3798 web unit, 24 Playwright**, 114/114 release rehearsal, **373/373
family rehearsal** (it names its own phases when you run it), **110/110 deployment rehearsal** (D60),
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

The owner's direction: *Daoris is the master development tool, doctrine and knowledge sharing*. Each row is
built by a subagent through the `dispatch-subagent` skill and merged with `tools/merge-branch.mjs`; Daoris.Plugins'
work is asked of it through the platform and taken by its own sessions.
- **The install** takes staged updates (`publish:desktop --stage`, applied when no session works; UPDATE1). Start
  it the normal way, not through the dev tool, unless the instruments are needed: a start from Git Bash hands
  sessions a `PATH` their shell cannot read.
- **Load makes flakes** (FLAKE1): run the rehearsals when nothing else builds.
- **Rows marked *the owner's*** wait on a grant, a download, a run or a call only the owner makes. **A new
  direction from the owner outranks every row.**

**Start by reading the contract the item cites**; `docs/README.md` says which documents are current, and
`CLAUDE.md` carries the standing rules and the dev loop.

## Backlog

The owner's two asks of 2026-10-04 lead: *Faster development* and *Simpler to use*, each in its own order. The
other headings are designs being built, each with its order line. Rows marked *the owner's* wait on the owner, and
the rows under *Held* are not work until their trigger arrives. A heading below holds open rows only.

### Faster development (owner, 2026-10-04: *"I think we do need to speed up the development of Daoris, currently we have too many rounds of tests?"*)

Measured on the day's three integrations: the driver's real-process half took 30, 48 and 126 minutes (a hang);
everything else together took about 25. Each merge ran all 13 gates whatever it touched, and a fixed failure meant
running them again.

- [ ] **GATE3 — a merge runs the gates its lanes reach; the full set runs before the install is staged** (tools). The
  merge tool maps each lane to the gates that can see it (docs: universal, code-map, verify; web: those and the web;
  driver and modules: their suites and halves and the family rehearsal; the desktop and publish scripts: the deployment
  rehearsal), and `publish:desktop --stage` refuses a commit the full set has not passed. The "web only batch broke main"
  lesson holds through the staging gate, the same day. Contract: MOD8, MOD9, D60. Proof: the plan's gate list per lane
  set; the stage refusing an ungated commit.
- [ ] **GATE4 — a fixed gate re-runs alone** (tools). `merge-branch --rerun <gate>…` re-runs only the named gates on the
  merge in place and keeps the rest's verdicts, where `--continue` runs every gate again. Proof: a plan test; the
  summary naming which verdicts were kept and from when.
- [ ] **PROC1 — the driver's real-process half under ten minutes** (driver tests, tools). Measure per class first (the
  merge tool keeps a trx with durations), then run independent Process classes in parallel workers, each with its own
  scratch home and ports, and move cases that need no real process onto fakes. Contract: MOD8, FLAKE1 (load is the
  flakes' common factor, so parallel workers need their own homes, not shared ones). Proof: three serial-equivalent
  runs green and the timing.
- [ ] **WEBFAST1 — the web's unit tests build jsdom once per worker** (web-shell). Vitest reports jsdom created 225
  times, 27% of its time; `pool: 'vmThreads'` keeps per-file isolation and creates it once. Proof: the suite green and
  its time before and after.
- [ ] **GATE1 — the docs gate is blind at the merge** (tools; found 2026-10-02): the devkit's `docs` gate reads committed
  dates, so a merge that changes the CLI's source without the root README passes the merge tool and fails `verify` once
  committed (TOOL4e did). Proof: the merge tool runs the gate as of the commit it would make, seen failing first.
- [ ] **TESTGIT1b — the last landing fixtures call the shared git runner** (driver tests). `LandingPluginTests` and
  `LandingTidyTests` still read git's stdout before its stderr; `LandedFixture` (fixed by PLUGHOOK1a), `LandingTests`
  and `AutoLandingTests` keep their own both-streams copies. Move them onto `GitFixture`. Proof: the classes' serial
  Process run; no `FileName = "git"` left in them.

### Simpler to use, and agents that work together (owner, 2026-10-04 → D150)

The contract is `docs/2026-10-04-ux6-redesign.md`; its §12 carries each row's full text and proof. Order: UX6a, b, c;
d and e; f, g; h after GIT1d; i, j; PLUGTOOL1a–c beside them, c after PLUGUI1c.

- [ ] **UX6a — the counter, and the baseline on the install** (tools; §9.1–§9.3).
- [ ] **UX6b — a remembered choice ends with its item; the side bar follows only live work** (web-shell; §1, §2.5).
- [ ] **UX6c — What needs you leads Overview and settles what it can** (web-shell; §6).
- [ ] **UX6d — accounts on What needs you, from what is known** (web-shell, modules; takes TOOL4m's row; §6.2–§6.3).
- [ ] **UX6e — Agents is a place: one account list per product** (web-shell, web-settings, modules; §5, §2.4), with
  TOOL4m's rest: the conversation picker's split (D130 §3.2) and a session head's *your own sign-in*.
- [ ] **UX6f — a repository's setup on its page** (web-shell, web-settings; §4.2, §3.1).
- [ ] **UX6g — a workspace's page; Settings → Workspace and Permissions retire** (web-shell, web-settings; §4.1, §4.3).
- [ ] **UX6h — Git inside Repositories** (web-shell; after GIT1d; §4.4), taking GIT1e: D147's list by kind and a
  branch's graph (a pure `graphLanes.ts`) in the Branches tab; no Git place on the bar.
- [ ] **UX6i — Knowledge: Search and Convergence as one place** (web-shell; §2.2).
- [ ] **UX6j — the bar's foot and Settings' seven** (web-shell, web-settings; §2.1, §2.3, §2.4).
- [ ] **PLUGTOOL1a — a plugin's manifest declares its tools** (cli, driver; §7.2–§7.3).
- [ ] **PLUGTOOL1b — the two pull-request plugins declare their CLI** (cli, examples; §7.4).
- [ ] **PLUGTOOL1c — a plugin's page manages its tools; Tools keeps Daoris's own** (web-shell, web-settings, modules,
  driver; after PLUGUI1c; §7.5).
- [ ] ⏸ **COWORK1 — agents that work together** (design; held until the owner shapes it). Today agents cooperate
  only through a quest (a request one way, a done back) and an ask's intake (one ask split into quests). What
  working together should add (seeing each other's progress on related work, asking mid-work, handing off, one
  branch shared by related work as LAND2c began) is the owner's to shape. Start from D32, D65, D145 and D149, and
  the quest's life as built. Proof: the design and its decision.

### One product, not a set of screens (owner, 2026-10-01 → D118, D119)

Order: PLUGUI1c, then f and g; FRAME1i, then PLUGUI1h. Each is looked at on the install in both themes and languages.

- [ ] **NAME2b — four names, looked at on the window** (NAME2's hand-back; 888 px, both languages): `help.setup`
  (Ask Daoris at its 300 px floor before setup is done), `scope.every` (the status bar with *Every workspace*),
  `signin.titleNew` (signing in to another account, on the Agents place once UX6e lands) and
  `work.group.noCheckout` (English: a repository with no checkout here); each renamed or accepted after the look.
- [ ] **PLUGUI1c — Settings keeps only what is a setting** (after b): the domain retires, its anchors repoint,
  the folder row joins Settings → Driver; crosses five lanes on purpose.
- [ ] **PLUGUI1f — the page whole** (after c and e): health on the window, Points, Agents, Servers, Activity
  live, Data folder, Source, *Install from a folder…*.
- [ ] **PLUGUI1g — a plugin's checks** (after f): the last trial kept, its own tests run in a copy under the home.
- [ ] **PLUGUI1h — Ask Daoris reaches the Plugins view** (after c and FRAME1i).
- [ ] **FRAME1i — Ask Daoris knows each view's list and item**: `where.ts`, `go` naming an item, the room.
- [ ] **FRAME2b — ask the window kit to restore a maximized window under the pointer** (the owner's to send; found by
  FRAME2). The kit's commands take no position, so a dragged maximized window restores at its saved place, not under
  the pointer as a native caption does. The request goes to the kit's repository, never an edit from here (D32).
  Contract: D56's FRAME2 note. Proof: the kit's answer, then FRAME2's handler using it.

### Knowledge that sessions actually use (owner, 2026-10-03) — D135

KNOWUSE1 found the sessions do read their knowledge: of 46 items put to the owner, 25 were truly the owner's (13 asks for
3 prod acts), 10 answerable from ticket or code, 6 drift, 3 required by the repository's own docs, 2 knowledge-answered.

- [ ] **KNOWUSE2b — the 46 through the bench** (the owner's). The 46 go through the floor against the work repository's
  own knowledge and through the model tier on the install's harness, with the account the owner chooses. KNOWUSE3 waits
  until the count answered in the person's place is zero. Contract: D135 §6 and its KNOWUSE2 note, whose command lines
  run it. Proof: the report's numbers, in a note under D135.
- [ ] **KNOWUSE3 — the review beside each item** (web, driver; held on KNOWUSE2b). Says which tier found each hint and never
  answers in the owner's place. Contract: D135. Proof: stories; the look.
- [ ] **KNOWUSE4 — a request the owner may publish to the work repository** (the owner's). Its comparison document
  records a misread answer as *the owner also settled the calculation*; correct it, and reconcile its *fix the config,
  not the shared component* rule with the owner's *add it into the common-report module*. Contract: the evidence §4.


### The horizon after: work that proves itself (an outside analysis, reviewed 2026-10-03)

The review is `docs/2026-10-03-future-directions-review.md`; ROADMAP's horizon section points to it. Its evidence
rows have a heading of their own below, and AFTER1 is held.

- [ ] **TRACE1c — the links the trace still cannot read** (driver, service, web-shell; found by TRACE1b). The rules
  handed past a run's end, one quest's operations at a local door, a merge landing's merge commit, a carry-on's link,
  and a LAND2c advance under its session; and TRACE1b's two doors left, the detached window's section and the commit
  kind from the page. Contract: D143's TRACE1b note. Proof: `TraceChainTests`, route and service tests, vitest,
  stories, the look.
- [ ] **STORY2 — a 中文 story leaves the next story load in 中文** (web-shell; found by TRACE1b). A module-scope
  `i18n.cloneInstance({ lng: 'zh' })` writes `zh` under `daoris.language`, so the next load in that browser profile
  shows English stories in 中文; a story's clone keeps its language off the store. Proof: a vitest that a 中文 story's
  module leaves `daoris.language` as found; story shots in both languages from one profile.

### Git, built in (owner, 2026-10-04 → D147, as D150 amends it)

- [ ] **GIT1d — the routes** (modules, web-shell; after GIT1b). `GIT_BRANCHES`, `GIT_LOG`, `GIT_COMMIT`, `GIT_HISTORY`,
  `GIT_BLAME`, `GIT_COMPARE`, on the shell's bridge only; each refusal a code in both catalogues. Contract: design §4,
  §6. Proof: `DriverModuleGitTests`, catalogue parity.
- [ ] **GIT1f — a commit, a file's history and blame, a compare** (web-shell; after UX6h, in its tabs). Reusing
  `DiffFileRow` and `PatchView`, with the trace's chain; the blame gutter; a compare's two sides. Contract: design
  §2.4–§2.6. Proof: stories, vitest, the look.
- [ ] **GIT1g — fetch, create and delete** (driver, modules, web-shell; after UX6h). One judge for both doors, plan
  then apply at the commit judged; `daoris-driver git fetch|branch|delete`, the presses, the console and the machine
  log. Contract: design §3.1, §3.3–§3.4. Proof: `GitActsTests` with a bare origin (the parent's), vitest.
- [ ] **GIT1h — push, and the pull request press** (driver, modules, web-shell; after GIT1g). `git push origin
  <commit>:refs/heads/<name>` with its refusals (the line, a session branch, a held branch, a non-fast-forward; never
  forced), kept in the landing entry, and *Open its pull request* as D102's hand-off. Contract: design §3.1–§3.2, D147
  amending D87, D100 and D109 to "never pushes on its own". Proof: fixtures with a bare origin and a pre-push hook,
  vitest, the look.
- [ ] **GIT1i — Git from a session** (web-shell; after UX6h). The review's head line (the branch against its line,
  where it landed, pushed, its pull request), *Show in Git*, the preview's *History and blame*. Contract: design §2.7,
  §6. Proof: vitest, stories, the look.
- [ ] **GIT1j — managed Git offered** (web-settings, web-shell, driver; after TOOLS6 and TOOLS10). The setup guide's
  first step *Git*, Repositories naming the git that answered, *Use Daoris's own Git…*; nothing switches without
  a press. Contract: design §5. Proof: the setup facts' tests, vitest, `HelpCoverageTests`.
- [ ] **GIT1k — Ask Daoris's `git` kind** (service, driver, web-shell; after GIT1h). Fetch, branch, push and delete as
  proposals the person confirms. Contract: design §3.3, D110. Proof: the kinds tables on both sides; coverage with no
  owed row.

### Landing and pull requests (owner, 2026-10-04 → D145, D148, D149)

- [ ] **LAND2e — work after a landing, and the advance on the page** (driver, modules, web-shell; found by LAND2c). A
  session that kept committing after its landing reads as landed (`ReadsAsLanded`), so with *Accept automatically* off
  its new commits cannot land at either door; accept them as an advance, show a gone tree's chain session only its own
  part, and let the review's note and Ask Daoris read an advance. Contract: D149 point 2, D113 §3. Proof: the review and
  `trees land` advancing it; vitest; the proposals' tables.
- [ ] **PLUGHOOK1b — the GitHub plugin answers `work/state`** (examples; after PLUGHOOK1a). The same query through
  `gh pr view` and `gh pr list --head`, so a GitHub repository's squash-merged branches go too. Contract: design §2.7,
  D148 point 7. Proof: `landing-plugins.test.ts` with a fake `gh`.
- [ ] **PLUGHOOK1c — the terminal reads and asks it** (driver; after PLUGHOOK1a). `trees state`, the state on
  `trees land --plan`, the clean-up's codes and `git branches` (GIT1a's list reads `pullRequestState`), and bringing up
  to date asking after its fetch, where a merge commit first arrives. Contract: design §2.1, §2.5. Proof:
  `DriverCommandTests`, `HelpCoverageTests`, `GitBranchesReadTests`, the sync's git fixtures.
- [ ] **PLUGHOOK1d — the page reads and asks it** (modules, web-shell, web-settings; after PLUGHOOK1c). The review's
  note with *Ask again*; the codes on the session-branch and sync rows (a workspace's Branches tab after UX6g) in both
  catalogues; `Sweep.tsx`'s kinds `pull-request` and `carried`; `plugin.kit.kind.query`'s words; a failing
  `work/state`'s cost line. Contract: design §2.4–§2.5. Proof: route tests, vitest, stories, catalogue parity,
  `names-check`, the look.
- [ ] **LAND2d — the page says who accepted a landing, and the rehearsal lands one itself** (web-shell, tools; found by
  LAND2b). The review's note words `acceptedBy` and the conversation a note event's `parts`; the family rehearsal drives
  an `--auto-accept` rule with a stub plugin and a bare origin through `drive --until-idle` (the branch, `acceptedBy:
  auto`, the trace's line, *To review* without it), and an uncommitted file stays to review until committed. Contract:
  D145's LAND2b note. Proof: vitest and stories; the rehearsal's phase.
- [ ] **LANDSVC1 — Ask Daoris's tool names `--auto-accept`** (service; found by LAND2a). The `setting_propose`
  description and the landing refusal sentence in `HelpProposalBox.Setting.cs` do not name it, so the agent learns the
  flag from the room only. Contract: D145 point 1. Proof: `HelpSettingProposalTests`.
- [ ] **LAND3b — a failed session's branch removed from the screen** (modules, web-shell, web-settings; found by LAND3).
  Only `trees remove … --force` reaches a branch whose tree is gone; offer it on the session's page and beside an
  unlanded session-branch row (a workspace's Branches tab after UX6g). Contract: D102's LAND3 note. Proof: modules
  tests; vitest over a mocked bridge.

### Evidence Daoris checks, and proof that outlives the environment (D144, D146)

Order: EVID1a, b and c in turn, each EVID2 row after its EVID1 row; EVID1d after DEV5 and DEV7.

- [ ] **EVID1a — a requirement names a path, and a met answer waits for its reading** (service; after DRIFT1d). Add
  `evidence` at the three publish doors (a `gate` is refused, naming EVID1d). A met answer on one holds its done, and
  `POST /api/quests/{id}/evidence` keeps an `Evidenced` verdict: found lifts the hold and publishes the step, missing
  keeps it. Contract: the evidence design §2–§3, §6, D144. Proof: `QuestEvidenceTests`, `QuestSyncTests`,
  `LocalHostTests`, `McpToolsTests`, `SharedHostTests`.
- [ ] **EVID1b — the driver reads the evidence when the session ends** (driver; after EVID1a). In `conclude`, read each
  path at the tree's `HEAD`, post the verdict and keep it in the record. The instruction names each path, the intake
  asks for them, and the sweep and `daoris-driver quest check <id> [--commit]` read the rest. Contract: design §2–§3,
  §5. Proof: `EvidenceCheckTests` over git fixtures, `RequirementsHandedTests`, the goldens, `TraceTests`,
  `DriverCommandTests`, the family rehearsal's phase 7.
- [ ] **EVID1c — the quest page shows the evidence and the hold's cause** (web-shell, driver; after EVID1b and
  DRIFT1d2). Each requirement's evidence with its result and commit, whether a met answer was read or rests on the
  session's word, and *Check again* beside the yes, both languages; Ask Daoris gets a check card. Contract: design
  §5–§6. Proof: stories, vitest, both catalogues, `HelpCoverageTests`, the look.
- [ ] **EVID1d — a gate as evidence, read from the queue** (driver, service; after DEV5, DEV7 and EVID1b). Accept
  `gate`: the queue's gate set gains every gate a requirement names, and the check reads the landing's verdict per gate
  and path evidence at the landed merge; a repository off the queue reads `no-queue`. Contract: design §4. Proof: the
  queue's tests with a stand-in gate, the family rehearsal's queue phase.
- [ ] **EVID2a — a requirement may require captured proof, and a done names it** (service; after EVID1a).
  `screenshot` and `answer` evidence at every publish door, `proof` on `quest_respond`'s done, `Evidenced` items for
  captures with their codes; no bytes, address or path cross machines. Contract: design §9–§10, §12, D146. Proof:
  `QuestEvidenceTests`, `QuestSyncTests`, `McpToolsTests`, `LocalHostTests`, `SharedHostTests`.
- [ ] **EVID2b — the driver keeps what the session captured** (driver, examples; after EVID1b and EVID2a). `${proof}`
  made at spawn and handed to the browser plugins as `--output-dir`; at session end each named capture checked,
  redacted, copied under the home with `proof.json`, and the verdict posted; the instruction and the intake ask for
  captures. Contract: design §9–§11. Proof: `ProofKeepTests`, `RequirementsHandedTests`, the goldens, the in-app
  browser server tests, the family rehearsal.
- [ ] **EVID2c — proof on the pages and at the terminal** (web-shell, modules, driver; after EVID1c and EVID2b). A
  *Proof* section on a session's page, its review and the quest's page, *Capture proof* through D137's reopen,
  *Remove proof*, and `daoris-driver proof [--save|--remove]`, both languages. Contract: design §11–§12. Proof:
  stories, vitest, both catalogues, `HelpCoverageTests`, `DriverCommandTests`, the look.

### Sessions that are easy to manage (owner, 2026-10-02) — D126, D131, D132, D136, D137

The contract is `docs/2026-10-02-session-management-design.md`; §9 carries each SESSUX row's full text, proof and
look. Order: SESSUX1j any time; SESSUX1h after FRAME1i; the looks (MSG1j, ANSWER1d, PAUSE1g, SESSUX1l) once their
rows are on the install.

- [ ] **CARRY2 — a cut-off's carry-on checks the take** (service; found by SESSUX1b2): D80 opens a carry-on after a
  cut-off without asking whose take the quest is, so a start that failed before its take carries on over someone else's.
  Contract: D80, D126's SESSUX1b2 note. Proof: a ledger row refused, seen failing first.
- [ ] **SESSUX1h — Ask Daoris reaches sessions** (§7.3; driver, service, web-shell; after d–g and FRAME1i).
- [ ] **CHATTAKE1b — a pipe-door chat is handed its session** (driver; found by CHATTAKE1). `Spawning.ChatInRoot` sets no
  `DAORIS_SESSION_ID`, so a repository chat on the pipe door speaks for no session: its take marks nothing and its
  publish names no session. Contract: D126's CHATTAKE1 note. Proof: a chat spawn's environment carries its session id.
- [ ] **MSG1d2 — a chat's waiting words are record events** (driver, web-shell; with MSG1c). A queued chat word is
  written with an id and its reach, taken under that id, and a withdrawn word says so, so a restart loses nothing.
  Contract: §3.1, D137's MSG1d note. Proof: `ChatTurns` tests, `conversation.test.ts`.
- [ ] **MSG1g3 — the cooling held note gets a code, and the new-session codes a twin test** (driver, web-shell; found
  by MSG1g2). The driver's "— it does not go on yet: …" line carries no code, so the page shows it in English beside
  its own sentence, and no test holds `newSessionSaid`'s codes to `GoOnNew.cs` and `WordsNever`. Contract: D137's MSG1g
  and MSG1g2 notes, D142 point 1. Proof: the `NoteCodes`/`CONVERSATION_CODES` twin rows; a parsed-declarations test.
- [ ] **MSG1f3 — the start-from's terminal door, and what the listing cannot see** (driver; found by MSG1f2).
  `daoris-driver sessions start-from <id>` through `StartFrom`, the terminal twin D50 asks of *Start a conversation with
  these words*; and MSG1f2's two gaps, a chat with waiting words grouped as ended and a cool-off the listing's fresh
  plan cannot see. Contract: D137's MSG1f2 note. Proof: `SessionsCommandTests`, `HelpCoverageTests`, the listing's cases.
- [ ] **MSG1h — Codex hears words at its next step** (driver; after STEER3's Codex turn; session-messages design §8).
- [ ] **MSG1i — the native door hears words at its next step** (driver; after STEER3's native turn; that design §8).
- [ ] **MSG1j — the canary on the install** (the parent's; that design §8 and §9; the install carries MSG1a–f since
  2026-10-03).
- [ ] **STEER2 — *Send now* on the next-step door, and a box that knows its door** (driver, modules, web; after STEER1).
  *Send now* has nothing to act on where words already reach the next step; send the draft through `_session/steering`
  and let the queue say when words arrive. Contract: D136 §4. Proof: driver, modules and page tests; one steer measured.
- [ ] **STEER3 — the other doors** (driver; after STEER1). A native driven session on `--input-format stream-json`, and
  `codex-acp`'s `turn/steer`, each measured for one turn before it is built. Contract: D136 §5. Proof: the measurement.
- [ ] **ANSWER1d — the canary on the install** (the parent's; the install carries ANSWER1a–c since 2026-10-03; owner,
  2026-10-03: *"you need to fix the ANSWER1 bug too"*). One park answered on the install shows
  one row, the conversation going on, and `session.answered` with `resumed` true. Contract: design §6. Proof: the run.
- [ ] **DRIFT1e — a follow-up checks against the ask, and a correction goes back to the work** (design first). A closing note
  is the build's account, not the requirement; a correction reopens the parent quest instead of being built under
  *Verify*. Contract: D133 §5. Proof: the design, then its rows.
- [ ] **PAUSE1f — Ask Daoris reaches pause** (driver, service, web-shell; after b and SESSUX1h). The `pause` kind; abandon
  stays the person's. Contract: D132 §7.4. Proof: proposal and coverage tests; the room's golden files.
- [ ] **PAUSE1g — looked at on the install** (the parent's, after a–f). An ask paused mid-session and resumed in its tree;
  one abandoned with a landed session kept. Contract: D132 §12. Proof: a ledger at every width, both themes and languages.
- [ ] **SESSUX1j — a quest's short title** (§6; service, driver, web-shell; any time).
- [ ] **SESSUX1k — a session ended on a limit, in the list** (§2.2; driver, web-shell; after UX6e). MSG1f2 and TOOL4g
  built *Resumes later* with a cooling account's reset for held words; what is left is a limit-ended session whose
  carry-on waits, and its ⋯ door to the account on the agent's page.
- [ ] **SESSUX1l — looked at on the install** (the parent's, after a republish carrying a–i): real sessions by state, a
  parked quest carried on from its session, a stop's hold and Try again, Archive what ended; every width, both themes and
  languages, with a ledger like SESS1's.

### What a session reads and writes (owner, 2026-10-02) — D127

The contract is `docs/2026-10-02-session-economy-design.md`.

- [ ] **COST1 — what a long driven turn costs** (the owner's call; found with METER1). One driven turn ran about a
  thousand tool calls while its context grew from 48K to 679K tokens, each call re-reading it from the cache, and
  nothing in Daoris sets an agent's window or compaction. Measure each driven session's context high-water and cache
  reads per turn for a week, then offer a per-agent ceiling, window or compaction setting on both doors. Contract:
  METER1, D127.
- [ ] **SESSOPT1d — the backlog, trimmed by the rule** (the steward's, after b and c). Each over-long line moves to §4.3's
  home; FLAKE1, TEST1 and REH1 get open fix-log entries. Proof: `doc-budgets` ≤ 5,300 words; no row over 60.
- [ ] **DOC7 — what sessions read, measured** (folded into D127 §6.1): `session.read` and `session.skill` in the machine
  log; the usage report gives reads per role, whole or not.

### A workspace that knows itself (owner, 2026-10-01) — D124

> *"since all is known knowledge it should be able to figure out itself"* … *"it's more like leaking of knowledge for
> workspace, so the repo should be registered and apply the doctrine and also initialize the knowledge"*

The contracts are `docs/2026-10-01-workspace-setup-design.md` (§8) and, for WSSETUP14, D128's pilot-lessons design.
Order: WSSETUP14b–e in turn, then the owner's WSSETUP14f and WSSETUP13; WSSETUP7 after LAYOUT8.

- [ ] **WSSETUP7 — the workspace on the screen and in Ask Daoris** (§4.4, §4.5; modules, web-shell, service, driver;
  after LAYOUT8; on a workspace's page once UX6g lands), both languages.
- [ ] **WSSETUP14b — a knowledge folder declared in place** (after 14a). `documents.knowledge` names a folder the index lists
  and the service indexes, which `sync` never writes. Contract: §1.2, §1.3. Proof: the twin tables matched.
- [ ] **WSSETUP14c — a document without frontmatter, by its heading** (after 14a). Contract: §3.1, §3.2. Proof: `node --test`.
- [ ] **WSSETUP14d — the set-up keeps the checks green** (after 14b; takes SETUP2, owner, 2026-10-04: *"also found a bug
  for CI pipeline after the merge of knowledge update … so this is also a good check when building Daoris"*). Checks
  first and at the close, each named with its result; knowledge kept in place; only a moved path rewritten, every
  reader of it repointed or reported; never `done` red. As D129 amends it: the close names each hand-written index,
  deletes none, and new documents are named by their subject. Contract: §1.1, §1.4–§1.6, §3.3; D129's review §4.5.
  Proof: `SetupBriefTests`, the playbook twin; the family rehearsal's set-up phase with a script reading a moved path.
- [ ] **WSSETUP14e — the follow-up for a set-up's branch** (after 14d). *Finish setting up this repository*, with the exact
  merge rule. Contract: §4.2. Proof: composer and press tests; the family rehearsal's set-up phase.
- [ ] **WSSETUP14f — the pilot, finished** (the owner's run, after a republish; WSSETUP12 ran its first turn 2026-10-02).
  The report repository: checks green before and after, knowledge declared, the root file under 32,768 bytes, the budget
  back to the default; then the pilot's second repository. Contract: §4.
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
- [ ] **WSSETUP13 — the rest** (the owner's run; takes LAYOUT10's other half): the plan resumed with the pilot's
  numbers, one repository with instruction files of its own among them; parks per week before and after.

### Plugins: the workshop, Daoris.Plugins and a catalogue (D120) — the build

The contract is `docs/2026-10-01-plugin-distribution-design.md`; §7 carries each row's full text and proof.
Every task in Daoris.Plugins is an ask to it, taken by its own session (the owner's wish: Daoris develops them).

- [ ] **PLUGREPO2e — this repository lets them go** (after PLUGDIST1g): the three plugins Daoris.Plugins now holds leave
  `examples/`, `landing-plugins.test.ts` retires, and the offers are laid out from packages.
- [ ] **WORKSHOP1a — the workshop setting** (§2.1): where Daoris develops a plugin, the home by default, both doors.
- [ ] **WORKSHOP1b — the workshop and its sessions** (§2.2); **WORKSHOP1c — the workshop on the view; Ask Daoris
  makes plugins there** (§2.3, §2.5); **WORKSHOP1d — hand-over and a named source** (§2.4).
- [ ] **PLUGDIST1b — the pack and release workflow** (an ask to Daoris.Plugins); **PLUGDIST1c — a package source over HTTP** (§5.3–§5.8);
  **PLUGDIST1d — the host answers**; **PLUGDIST1e — the catalogue's Available** (§6 as D140 amends it, after PLUGUI1f).
- [ ] **PLUGDIST1a's leftovers** (its hand-back, 2026-10-01): the modules' plugin page calls a package record
  `folder` with no folder (PLUGDIST1d says `package`); `install` is still routed in the host's `Program.cs` ahead of
  the `plugins` catch-all and moves into `PluginsCommand` now that PLUGUI1d is on main; extraction has no size bound,
  so PLUGDIST1c bounds it before a package arrives over HTTP.
- [ ] **PLUGDIST1f — the first publish** (the owner's: the nuget.org account, trusted publishing, the prefix), then
  **PLUGDIST1g — the offers from packages**. ⏸ **PLUGDIST1h — the repository signature checked** (held).
- [ ] **INIT1 — `init` writes line endings it can keep** (found setting up Daoris.Plugins): a `.gitattributes`
  with `* text=auto eol=lf` when the repository has none, since Daoris measures its files byte for byte and a
  machine with `core.autocrlf` would check them out as CRLF, as drift.

### Development documents and fewer asks (D122) — the build

The contract is `docs/2026-10-01-development-documents-design.md`; §6 carries each row's full text and proof.
Order: UNBLOCK2 (after DEV5) → UNBLOCK3 → UNBLOCK6–8.

- [ ] **UNBLOCK4c — the push canary** (the owner's to allow): one turn per form against a local bare remote in
  scratch, by UNBLOCK4's procedure (its archive entry and hand-back): `git -C . push`, `-c`, `--no-pager`, a quoted
  subcommand, an alias, on both doors; the remote's tip must not move, and what refused each is recorded.
- [ ] **UNBLOCK2 — the declaration and its judge** (§3.1–§3.3, after DEV5): `safe` beside `gates`, read from the line.
- [ ] **UNBLOCK3 — the person's one yes** (§3.4, §3.5, after UNBLOCK2 and a week of UNBLOCK5): the `declare`
  proposal, exact rules on both Claude Code doors.
- [ ] **DOC6 — the example family keeps the standard** (DOC7 is folded into D127).
- [ ] **UNBLOCK6 — the screen and Ask Daoris for a declaration**, both languages; **UNBLOCK7 — codex and dsh
  measured before anything is handed**; **UNBLOCK8 — the first real declaration** (the owner's: asks a week
  before and after).

### Self-managed tools (D121) — the build

The contract is `docs/2026-10-01-tools-design.md`; §7 carries each row's full text and proof. Order: TOOLS6 ∥
TOOLS8, then TOOLS9; TOOLS10 before any session runs a managed git; TOOLS11 last.

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
detail and the proof. Order: DEV5 → DEV6 → DEV7 (one lane, in sequence), then DEV8 ∥ DEV9, then DEV10 and
DEV11.

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

The contract is `docs/2026-10-01-agent-layout-design.md`; §7 carries each row's full text and proof. Until
LAYOUT2 measures, the layout's entry points are the design's reading, not the harnesses'.

- [ ] **LAYOUT2 — the canary turn** (the owner's to allow). The keyless half is done and archived
  (`docs/2026-10-01-entry-point-evidence.md`: the ACP adapter loads project instructions). One turn per
  harness, by §6's fixtures and prompt, shows a maker-side flag is off for the account and confirms the
  predicted cells.
- [ ] **LAYOUT5 — this repository's doctrine moves** (§4.1, §4.3, §4.4), run alone: `git mv`, the manifest,
  `sync`; `.gitattributes`' union line moves in the same commit; `examples/engine` moves. The lane map is
  `records`' (DEV2), so the branch touches the steward's records.
- [ ] **LAYOUT6 — the brief moves** (§4.2): `CLAUDE.md` into the root `AGENTS.md` (about 1,300 words, under
  codex's documented 32 KiB) and eight rooms; `CLAUDE.md` keeps the import alone.
- [ ] **LAYOUT8 — the layout on the screen, and its Ask Daoris door** (§6.1, §6.5): Repositories' rows and a
  repository's Setup tab (UX6f), *Set up for agents*, the `setup` kind; both languages, looked at on the window.
- [ ] **LAYOUT9 — a lane names its rooms** (§2.5), after DEV6.

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
- [ ] **TRUST2 — what D73 leaves unmeasured** (the owner's: one grant). Whether Claude Code honours a trust key Daoris
  wrote as its own, and whether a trusted parent covers a child, since the hold matches the exact folder.

### The toolchain and its accounts (owner, 2026-09-22 → D57)

`docs/2026-09-22-toolchain-design.md` is the contract, and its §3 is the home of the resolution rule
(*explicit command → managed pin → `PATH`*; *pick → workspace → machine → none*). TOOL1–TOOL3 are in
the archive: **usage is measured before it is managed**, and breadth is **more native adapters plus
the ACP door, not a registry**.

- [ ] **TOOL6d — a conversation continues on another account** (driver, modules, web-shell; after TOOL6b). A refused turn
  offers *Continue on* another account, handed the last plan and last words. Contract: D130 §8, §9. Proof: driver, route
  and vitest tests; the look in both languages.
- [ ] **TOOL6h — an account that signs out after its last probe** (driver; found by TOOL6g). One that read signed in
  stays ready until a start on it fails or the person presses refresh; read a session's refusal on it as a reason to
  ask that one account once. Contract: D125's TOOL6g note. Proof: a tick where a start is refused on an account last
  read signed in.
- [ ] **TOOL4l — Ask Daoris's account doors, the service's half** (service, driver; found by TOOL4g). `agent_propose`
  writes `use` (with `use`, `keep`, `early`, `near`), `order` and `ready`, and the setting writer lists `cooloff`, so the
  cards the screen's controls owe can be offered. Contract: D125 §6, D130 §9, §16.6. Proof: the proposal-kind tests with
  the doors listed; `HelpCoverageTests`' owed rows become doors.
- [ ] **TOOL4h — the rehearsal and the report** (tools; §8, §2.2; after TOOL4j): two stub accounts; the usage report's
  limits section per account and window, *Daoris's sessions only*; *in parallel* over N stub accounts (D130 §5.4).
- [ ] **TOOL4i — a real rotation on the install** (the owner's run). *One by one* seen 2026-10-02: a spend limit on the
  work account cooled it and the carry-on opened on the next account (`account.limited`, `account.rotated`), and it found
  the time-of-day reset defect (FIX-LOG). Still owed: *in parallel* over every account, and what one limit cuts off (D130 §11).
- [ ] **AGT3c — two readings that trust what a session printed** (found designing D125): AGT3b's 401 detection reads the
  transcript's last lines, so a session whose own output ends with `API Error: 401` holds its account; and the ACPEND1
  note carries the agent's sentence, zone included, to every machine (`SessionNote.ForAnotherMachine` removes no zone).

### Agents, their accounts, and the map (owner, 2026-09-23 → D67)

**D67** holds the owner's answers; `docs/archive/2026-09-23-agents-direction.md` the asks and what
was checked. Every AGT and MAP item is archived but this one.

- [ ] **AGT2c — one real vendor-channel pin of each, observed:** that Claude Code stays at its version
  under `DISABLE_UPDATES`, and that a pinned Codex outside its own layout takes no update action (the
  evidence says so; nothing measured it). Spends two real downloads — the owner's call.

### The in-app browser — Daoris's own, driven over MCP (owner, 2026-09-27 → D78, D84)

`docs/2026-09-27-in-app-browser-design.md` is the contract.

- [ ] **BRW14 — a page's file reaches the quest** (design first; found on the owner's ticket, 2026-10-01). An intake
  reading a ticket whose mock-up is an attachment could not save it and fell back to a cropped capture, since the
  engine's downloads are its own (CHR3 §3.2). Design where a session's download lands and how the quest's attachments
  take it. Contract: D78, CHR3.

### Tests that fail under load

- [ ] **FLAKE1 — real-process tests that fail under load and pass alone** (driver, modules tests). Every sighting
  since 2026-09-25 shares load (parallel builds or live sessions beside a full run); the repeaters are
  `DriverModulePluginsTests.The_kit_makes_a_plugin…` (four times), `DrivenSessionInputTests`' acp-stub case and
  `ProcessJobTests`. Make each repeater's wait name its slow step and bound it, rather than re-run. Sightings: the
  fix log's FLAKE1 (open). Contract: MOD8, PROC1. Proof: each repeater's failure names its step; three loaded serial
  runs green.
- [ ] **DEV3a — a stop is reported in the run that made it** (driver; found merging, 2026-10-01): the family
  rehearsal's lost-claim check failed once under load (311/312) because sessions outlive their tick (DEV3) and the
  stop's report can land after the run's last print. Make `--once`/`--until-idle` wait for and print every report it
  caused. Proof: a driver test that a stop caused by the run is printed before it exits.
- [ ] **TEST1 — a Node process on Windows aborts with `0xC0000409`** (web e2e, tools). Three sightings with no output,
  two under Playwright and one in the deployment rehearsal closing its shell; the common factor is a Node process
  ending while its children are killed, which a family sibling reproduces at about 1 in 300 rounds. Capture first (a
  JSON reporter in `playwright.config.ts`, the rehearsal's exit kept); no timeout tuning on three points. Sightings:
  the fix log's TEST1 (open). Proof: the next sighting captured.

### Held — each waits on a trigger that has not arrived

- [ ] **REH1 — the release rehearsal's canon-upgrade phase failed whole, twice** (tools). Seen 2026-09-18 straight
  after canon edits and syncs, as exactly that phase's 7 checks, and never since; every run keeps a transcript in
  `_fixtures/rehearsal-logs/`. No tag while it is open: it closes on a captured failure, or by the owner's call after
  clean runs that follow canon edits. Sightings: the fix log's REH1 (open).
- [ ] **CANON9 — `desktop-winforms`, the last pack candidate.** One 11 KB source, one repository —
  below the two-repository bar, which is the whole reason the canon is trustworthy. Leave it local until
  a second repository needs the same thing.
- [ ] **TOOL5 — more native adapters** (design §5; D23, D24, D57). Every sentence of §5 is realised (the seam, the ACP
  door, a tool being both, no registry), so what remains is a trigger: an adapter when a repository names its tool,
  since each field is a claim about someone else's program. The reading is D57's TOOL5 note.
- [ ] **SEM2 — vectors that persist** (service; after SEM1). The MCP host is one process per session, so with an
  embedder each session's first search embeds the corpus; keep vectors in `knowledge.db` through
  Lyntai.Storage.Sqlite, with Dapper, FluentMigrator and a re-embed when the model changes. Trigger: a machine running
  an embedder whose first search somebody notices (the install says `lexical only`).
- [ ] **PLUG7 — service-side points** (D64): the same wire reaches the knowledge service when a plugin somebody writes
  asks for a point there.
- [ ] **A file tree in the dock** (D76): when a person asks to browse a tree rather than open a named file. The
  preview (PREVIEW1, D111) and the terminal (CONSOLE4, D96) are built.
- [ ] **AFTER1 — a chain step after several quests** (service, driver; until an ask needs it). A step names the quests
  it starts after, and the planner holds it saying which are open, as data on the quest. Contract: the review §3,
  amending the intake design §1g's linear chain. Proof: the trigger, then service and planner tests.
- [ ] **MSG1k — a terminal conversation forked into Daoris** (until a real use asks). Contract: the session-messages
  design §4.3. Proof: a protocol stub that speaks `session/list` and `session/fork`.
- [ ] **PLUGHOOK2 — a pull request's review threads back to its session** (driver, examples, web-shell). Read at a press
  through `work/review`, sent only as the person's words or a new ask. Trigger: the first change request carried over
  by hand. Contract: plugin hooks design §3.1.


## How to work a task

`CLAUDE.md` carries the dev loop and the conventions. **Work an arc leaves behind goes in the backlog as a row**,
never as a handover sentence — that is how work quietly stops being work.
