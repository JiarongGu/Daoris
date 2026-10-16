# Daoris (道衍) — open work

Open work only; move finished rows to `docs/task-archive.md`. Release changes belong in `CHANGELOG.md`.
This handover keeps every outstanding identifier. Detailed acceptance criteria, owner quotations and
earlier evidence remain at `38065d97:TASKS.md` in Git and in each section's contract; read the matching
row before implementation. The earlier condensation's supporting record is `a3b20973:TASKS.md`.

Start with `AGENTS.md`, `CLAUDE.md`, `.claude/INDEX.md`, `docs/README.md` and `docs/index/README.md`.
Use discovery skills, then read the contract. Integrate through `tools/merge-branch.mjs`; run rehearsals
serially. Owner-only work, downloads, account spending, installation, publication and decisions retain
their existing grants and holds. A dependency named below must be complete before its dependent starts.

Development remains at `0.0.x`, unpublished. The owner runs an install of it every day on real work, so a change reaches a
live home through the staged update (D139); adoption by anyone else is the owner's call.
Current recovery, review coverage and verification: `docs/2026-10-10-integration-review.md`.
Historical installation receipts and adoption mechanics remain in their evidence records.

## Workspace cleanup

- [ ] **DOCSCRATCH1 — remove blocked verification scratch** (command review). Deletion of ignored `_fixtures/doc-system-preview-git/` metadata remains rejected after explicit owner authorization and repository-local defaults. Inspect the exact target, then native removal when permitted; proof: absence. Restriction and retry receipt: `docs/2026-10-10-code-and-docs-review.md`.

- [ ] **CLEANUP1 — remove the empty harness-held directory** (command review). `.claude/worktrees/agent-a3b92da3b8bb159c9` is empty and unregistered; backups are preserved. Native removal remains rejected after explicit authorization and repository-local defaults. Proof: recheck emptiness/registration, remove when permitted and verify absence; retry receipt: the maintenance review.

## UI/UX first

Start with the owner's reports of 2026-10-16 (below), then what the window freezes still owe (SWEEPFLIGHT1, PROCSTART2),
then the installed proofs, batched against one staged build (INSTALLPROOF1 first).
Contracts: platform UX §4/§6, `docs/2026-10-05-ux7-design.md` (D152), `docs/2026-10-12-entry-and-heads-design.md` (D162),
`docs/2026-10-04-ux6-redesign.md` (D150, §12 proofs). UI work needs stories, behavior tests and
installed-window evidence in both themes/languages; Storybook alone does not close an installed proof.

First of all, the owner's 2026-10-16 report (D170): Ask Daoris could read a repository but could not set it up, and said
it had no shell.

- [ ] **LAYOUT8c — the setup door on the install, in both languages** (parent; after republish). A registered, not-adopted repository's *Set up for agents*: closed, after *Show the plan*, refused where the machine gives one, then *Set it up* and what it published; Ask Daoris asked to include a repository, its card and Apply; en and zh, both themes, 1546 and 680 px. D117 §6.1/§6.5. Proof: dated shots and the quest it published.
- [ ] **RPC1b — the trees verbs are the running Daoris's clients** (driver host/driver; after RPC1a). `InstanceClient` finds the instance (the RPC design §7.4; a workspace build reads no file) and switches as §7.2's table does; of `trees land`, `remove`, `clean`, `sync` and `hand`, each whose route the census classes `open` sends it and prints its answer in today's words, a `person` one waits for RPC1c; the told notes go with the last of them. Proof: host tests per §7.2 row against a stand-in server, the lost answer never acted alone among them; each verb's text the same from the route's answer as alone.
- [ ] **RPC1c — a person's press from a terminal is confirmed in the window** (desktop/web; after PERSONDOOR1f; D156 §4.2). A `person` route over the pipe shows PERSONDOOR1f's card with the terminal named, runs once on **Confirm** within two minutes, and the client says it waits; refused or expired, nothing runs. Proof: modules tests (confirmed, refused, expired, five waiting); the card's page test in en and zh; on the install, a confirmed terminal press.
- [ ] **RPC1d — the sessions verbs are the running Daoris's clients** (driver host; after RPC1b). `sessions stop`, `finish`, `decline`, `say`, `take-back`, `reword`, `archive`, `unarchive`, `delete`, `go-on-new`, `start-from`, `hand-over` and `helper` send their routes while the desktop runs; the request folder stays for a headless loop. Proof: host tests per verb; the alone path's `SessionsCommandTests` unchanged.
- [ ] **RPC1e — the rest of the host's verbs with a window twin** (driver host; after RPC1d). Work, history, help, opinion, setup, register, sync, plugins and the channel send their routes; reads go through the instance where they have one. Proof: the census test with only `chat`, `help resume`, `drive`, `update` and the service's verbs off the pipe, each with its reason.
- [ ] **RPC1f — the CLI's edits through the running Daoris** (CLI; after RPC1a). `daoris driver …` edits a `SET_*` route answers, and the `agent`, `plugin` and `tool` verbs the census finds a route for, go over the pipe while the desktop runs, and under the lease (`lease.ts`) when none runs. Proof: CLI tests against a stand-in pipe; a `twins.md` row with both tables.
- [ ] **RPC1g — a terminal's conversation is the running Daoris's: the design** (design). `chat` and `help resume` start their turn in the instance and stream it by subscription, so every window shows it and the loop owns it; its streams' shape and its Ctrl+C. Proof: the design's note, then its rows.
- [ ] **SWAPLEASE2 — a crashed swap's recovery under the lease** (desktop launcher). `InstallSwap.Recover` rolls a crashed swap back outside the lease; only `Run` holds it. The RPC design §5. Proof: a source-reading row and a `StagedBuildTests` row.
- [ ] **LEASEHOME2 — one home for the loop's lease and a verb's** (driver). The headless loop takes its lease in the driver config's folder (`DriverConfig.HomeOf`), a verb in `DAORIS_HOME`; with `DAORIS_DRIVER_CONFIG` elsewhere the two never meet, and a verb run with another `DAORIS_HOME` is unseen by the swap. The RPC design §5. Proof: a `DriverHostTests` row with the config elsewhere.
- [ ] **MAINWINDOWS2b — the taskbar's count on every main window** (desktop). While the first main window is put away its button is gone, so the count shows on none; each `main:<n>` wears it. D171's MAINWINDOWS2 note. Proof: a modules test; on the install, the count on the second window with the first put away.
- [ ] **MAINWINDOWS2c — a picker owned by the window whose page asked** (desktop). `PickFolder` and `PickFile` are owned by the first main window, hidden when put away; own the dialog by the asking window. D171's MAINWINDOWS2 note. Proof: a test per route caller; on the install, *Add repository* from `main:2` with the first put away.
- [ ] **HELPPLUG1c — a plugin's skills for an intake's, a reviewer's and a helper's sessions** (driver). `DriverIntakes`, `DriverOpinions` and `DriverHelpers` spawn without them. D170 point 4. Proof: a spawn test per kind on both doors, the folder removed at the session's end.
- [ ] **HELPPLUGPROOF1 — plugin servers and skills proven on the install** (parent; after republish). Ask Daoris calls an approved read and is refused an unaccepted write; a session on each door invokes a plugin skill (the protocol door's `plugins` option was read from source only). D170's HELPPLUG1 notes.
- [ ] **SWITCHCLI1 — the terminal says the switch as the page does** (cli). `profile use` still says *switch before the limit* and only what passes an account at a start (`toolchain.ts` `useLines`). D165's SWITCH1d-c note. Proof: `toolchain.test.ts`' use lines in the new words.
- [ ] **CURVETERM1 — the usage curve in the glossary** (web). *curve*/曲线 has no glossary term, and "point" is a plugin's 挂点. D165's SWITCH1d-c note. Proof: `names:check --strict` holds the term.
- [ ] **LAYOUT8b — the rest of §6.5 on the screen** (web/driver). The per-agent table (what each agent reads), the layout and the agents it serves on the repository's row, *A set-up waiting* for a set-up branch not yet on the line, and *Move to the agents layout* for an adopter. D117 §6.5. Proof: a driver reader test, page tests in en and zh, the installed window.
- [ ] **HELPAPPLY1 — Apply lands what the card showed, once** (driver/modules; a design review's finding, 2026-10-16). `HELP_APPLY` reads and plans again before it applies (`HelpRoutes.cs` ~332), so a card can apply something other than what it showed, and two windows can apply one proposal together. Bind Apply to the shown plan's revision (a material change asks again, said on the card) and claim the proposal atomically. D89, D170. Proof: modules tests: a plan changed between card and Apply asks again; two Applies at once, one wins.
- [ ] **INSTALLPROOF1 — one installed regression pass over this week's changes** (parent; after republish). Against one staged build: an *Accept* across a tab switch and from a second window opened by a second launch; a `daoris-driver` press from the owner's own terminal reaching the open windows; Ask Daoris's setup card and Apply; a park answered in one box (no duplicated options or boxes); the taskbar count with a helper waiting. Dated shots, en and zh, both themes. Proof: the shots and the machine log's lines, in the archive entry.
- [ ] **UXFIX2bi — installed confirmations look** (parent; after republish). Retire right on 2026-10-11's second look (its own question; focus back to the press with a pointer-like click); the rest need data the machine lacks: tool, plugin, pause, abandon, decline, close, finish, archive, discard, both themes/languages.
- [ ] **UX7b — installed account UI** (web). Verify recovered account rows and add/name/join flow at 1546/680 px, both themes/languages. D152 §4.
- [ ] **ACCTEDIT1b — installed refused edits** (parent; after republish). Verify rename, workspace use and add refusals retain drafts, including dark borders. D152 §4; dated shots.
- [ ] **UX7e — installed menus and heads** (parent; after republish). Verify keyboard menus, shortcuts, composer editing, head geometry and titles at 1546/680 px. D152 §6.
- [ ] **ENTRY1bi — installed go to what waits** (parent; after republish). A go to `sessions/waiting`, `quests/held` and `quests/asks`, and the waiting starter, each with a group to show: its heading at the top, focused. D161's ENTRY1b note.
- [ ] **UX6i2i — installed go cards** (parent; after republish). Ask Daoris answering "where do I turn a plugin on?" and "where is Convergence?": a go card reading *Open Plugins.* or *Open Knowledge → Convergence.* that opens the right place and mode.
- [ ] **PLUGTOOL1c — tools on their plugin** (web/modules/driver; after PLUGUI1c). Tools retains Daoris's own. UX6 §7.5.
- [ ] **COWORK1 — agents working together** (design; owner's direction given 2026-10-12). Define progress sharing, questions and handoffs. D32/D65/D145/D149; decision before implementation. The owner, 2026-10-12, after a Codex-driven task neared its usage limit: *"I just used a codex for main drive for a new task but I don't see a way to mix the use of both agents (like codex to claude) so currently when usage is almost full I'm kind of stuck on that task, also it should be able to run subagents to use other agents to support the task and we need a clear view to show those behaviours (and should be decided by the agent itself or triggered by user)"* Hand-overs and helpers are designed (D163, `docs/2026-10-12-mixed-agents-design.md`) and built by the MIXAGENT rows below; this row keeps what D163 leaves: progress shared and questions asked between agents working at once.
- [ ] **MIXAGENT1c — the agent asks to hand over** (service/driver; after 1a). `handover_propose` (its own note, kept as its claim) into the box, settled by the `handOver` rule (`ask` default, `on`, `off`) in Settings and `driver.json`. Design §2, §5.
- [ ] **MIXAGENT3a-iii — cost per row, and a helper's answer as its block** (web/driver; after PROCESS1d-ii). The rail's rows show what a session used (`useUsage`, shell only; *not measured*, never zero, where it is absent); the conversation shows a helper's answer as that agent's block (a driver event field beside `Opinion`, read by `conversation.ts`). Design §4.

## This Daoris: its folders, its wiring and its settings

The owner, 2026-10-16: *"for every setting and how the server and data are wired we need some place to show it, or to set it
up, with good UI/UX"* and *"we also need to shape the Daoris folder into a better separation since we will have more files
now (data, app, res, …)"*. An install today is `Daoris.exe`, `app/`, `data/` and `update/`, and `data/` mixes the person's
settings, records, sign-ins, downloaded runtimes (`toolchain` 1.4 GB, `chromium`, `webview2`, `bin`), plugins, session trees
(1.7 GB), locks and logs at one level. Order: HOMELAYOUT1 decides the folders, WIRING1a designs the page; then each builds.

- [ ] **HOMELAYOUT1 — the install's folders: the decision and design** (design; amends D63). Separate by what a thing is and who may lose it: the program (`app/`, replaced by updates), the person's own (settings, records, the knowledge store, sign-ins and profile homes), the work (session trees, kept files), what can be fetched again (`res/` or a cache: toolchain, Chromium, WebView2, installed plugins' packages, `bin`), what a run holds (locks, spawn files, starts, salvage) and the logs. Say what each category's backup, move and clean-up mean, which variables still win (`DAORIS_HOME` and the dev loop's `--app-root`), and how a published install and a dev home share one shape. Proof: the decision and design document, every current path placed in it, reviewed against `DaorisHome`'s callers.
- [ ] **HOMELAYOUT2 — the code asks for a category, never a hand-made path** (driver/modules/desktop/service; after 1). `DaorisHome` gains the categories and every caller (`DaorisHome.Require("…json")` and the folders) asks by category, so a later move is one table. No path changes yet. Proof: a census test that no code joins a home path by hand outside `DaorisHome`.
- [ ] **HOMELAYOUT3 — an existing home moves to the new folders on start** (desktop/driver; after 2). The desktop moves an old home's files to their new places once, atomically per folder, with the old layout still read where a move failed, a line in the machine log per move, and nothing deleted; worktrees moved with `git worktree move` or repaired, never copied. The update (D139) carries it. Proof: a migration test over a copy of an old-layout home; the rehearsal's install upgraded; the owner's install upgraded with its sessions, trees and sign-ins working.
- [ ] **HOMELAYOUT4 — the publish lays the install out the same way** (tools/desktop; after 1). The publish and the bundled resources (`res/`) follow the decision; `INSTALLED.md` says what each top folder is. Proof: the deployment rehearsal's install tree checked against the design.
- [ ] **WIRING1a — the page that shows how this Daoris is wired: the design** (design; D171's companion). One place, Settings → *This Daoris* (本机), with every wire and its source: the folders and their sizes (HOMELAYOUT1's categories), the service (its address, hosted or adopted, version, whether it answers), the driver loop (who holds the home, its last tick), each settings file and what it holds, the variables in effect and where each came from (environment, file, default), the program's and the kit's versions, the update's state, the terminal launchers and whether they are on PATH, the knowledge store, the logs. Secrets never shown (a key's audit prefix at most). Contracts: platform UX, the translation contract. Proof: the design document with its screen states.
- [ ] **WIRING1b — what the page reads** (modules/driver/service; after 1a, FINDDAORIS1). A read route (`SYSTEM_WIRING`, BRIDGE-PRESSES' read places) answering the design's facts with their sources, and its terminal twin (`daoris-driver wiring`, D50). Proof: modules and host tests per fact and source; a secret never in the answer.
- [ ] **WIRING1c — the page** (web; after 1b). Settings → *This Daoris*, en and zh, stories, both themes, at 1546 and 680 px. Proof: tests, stories, the installed window.
- [ ] **WIRING1d — every setting in one list** (web/modules; after 1c). A searchable list of every setting across the domains: its current value, where it is set (default, `driver.json`, a workspace, a repository, the environment) and a door to the domain that edits it, so a person can find any setting without knowing its domain. Proof: a census test that every domain's setting appears; search tests en and zh.
- [ ] **WIRING1e — setting the wiring up from the page** (web/modules/desktop; after 1c). The page's actions: open a folder, copy a terminal line that sets the variables, put the launchers on PATH (the person's user PATH only, said first), point the service elsewhere, and FINDDAORIS1's address file shown as it is read. Each a press through BRIDGE-PRESSES' places, with its Ask Daoris door or exemption. Proof: tests per action; the installed window.

## Collaboration Fabric (D173)

The owner accepted `docs/DAORIS_UNIFIED_COLLABORATION_ARCHITECTURE_AND_PLAN_2026-10-16.md` as written (D173). Only its
C0–C2 slice is here (§12); C3–C7 wait for contract tests. Correctness rows elsewhere stay first (its §9).

- [ ] **FABRIC-C1a — a native child bound to its session part, from the harness's own lineage** (service/driver; after C0). Both harnesses name a child's lineage without Daoris's help (Claude: the `PreToolUse` hook's `agent_id` joined to `_meta`'s `claudecode/toolUseId` and its stream's `task_started`; Codex: `_meta`'s `x-codex-turn-metadata`), and neither gives the connector a child identity in its environment (the C0 audit §2). The connector reads each call's `_meta` and the driver records an observed binding (harness, native id, parent id, provenance) against the existing part, never a new session; it ends with the part; unsupported signals stay unknown; `_meta`'s paths are never stored or sent. D173's guards; proposal §3-§4, §11. Proof: real child calls on both harnesses tell a child, a grandchild and two siblings apart; a killed root marks its children's bindings lost; canaries 1, 2 and 9.
- [ ] **FABRIC-C2a — a child's calls read-only by their lineage** (service; after C1a). A native child calls with its parent's `DAORIS_SESSION_ID` on both harnesses (the C0 audit §2), so the connector decides a restricted binding per call from the harness-written lineage, never from an argument: read-only tools answer (`daoris.me`, `knowledge_search`, `knowledge_get`, ids and ranges as the first references), and every session-scoped mutation (`helper_ask`, `channel_post`, `quest_respond`, `go_ahead_ask`, `opinion_give`, `permission_propose`) refuses a child or a call whose lineage cannot be read. D173's second guard; D107; D89/D170. Proof: a stand-in harness's child is refused each mutation and served each read; a forged id in arguments changes nothing; the root's calls are unchanged; canaries 3 and 8.
- [ ] **FABRIC-C2b — the two-harness canary on the install's doors** (parent; after C2a, FABRIC-PROBE1). Each harness's native child, over the door Daoris really uses (Claude's pipe and ACP, Codex through codex-acp with its MCP approval answered by Daoris), reads the same permitted reference and is refused a mutation. Receipts: versioned calls, refusals, a person's stop and a parent's kill, setup steps, time and usage (or *not measured*); helpers and channels rechecked unchanged (canary 10). Proof: the receipts in D173's note.
- [ ] **FABRIC-PROBE1 — the harness probe kit as a tracked tool** (tools; before C2b). C0's stand-in MCP server (it logs every message), its hook logger and its launchers (each process its own `DAORIS_HOME`, the parent session's `CLAUDE*` variables removed) are scratch; C2b repeats the measurements. Keep them as `tools/` scripts guarded by `isMain`, with the receipt summariser. Proof: one plain run reproduces the audit's run A receipt on a scratch home.

## Accounts and browser

Contracts: toolchain (D57 §3), account rotation (D125), account use (D130), browser (D78/D84).
Original row proofs remain in the frozen backlog; measurements precede declarations.

- [ ] **ACCTUX4i — installed account cells, and the row budget** (parent). 中文 dark and light at 1546 and 680 seen 2026-10-11; English, a key and Codex rows remain. The *Now* header stands over an empty column, the cells starting under *Used for*. Settle D152's row budget against D125's ACCTUX4 measures.
- [ ] **ACCTPLAN1 — plan, spend and reset credits** (driver/modules/web; design first). Nothing reads them yet (D125:898-899); show them in a fold, and never redeem a credit without `InlineConfirm`. Second-opinion review lines 132-133.
- [ ] **CODEXUSE2 — installed Codex reading** (parent/owner). Pins are verified; account sign-in remains unavailable. Use CODEXUSE3's own-sign-in reading meanwhile. D57/D63/D125.
- [ ] **CODEXKEY1 — Codex API-key account** (measure first; owner supplies throwaway key). Determine spawn variable for both tools before declaring support. D67/D125; real variable-start proof.
- [ ] **CURVEROOM1 — "both are over their curves; it is the least over"** (driver/web; if the window shows it unclear). `WalkStep.Room` is said today by the other account's own reason; a coded reason of its own, en/zh. D165's SWITCH1b-ii note.
- [ ] **SWITCHMODEL1 — a model family's week** (driver; measure first). The per-model weeks (`seven_day_opus`, `seven_day_sonnet`) take the weekly curve where the agent reports them; `get_usage` answered them null on every account read (`docs/2026-10-12-usage-read-evidence.md`). Read them when one is reported. Design §2.
- [ ] **SWITCH1e — the offer is the fallback** (web; after 1c). The low-allowance hand-over offer only where `switch` is `off` or no account of the agent has room. Design §4.
- [ ] **SWITCHCTX1 — a move keeps the agent's own context** (owner: spends two short turns). Measure whether a Claude Code conversation resumes on another account's home with its transcript carried; if it does, an amendment to D125 §3.5 point 5 may let a move keep it. Design §4.
- [ ] **TOOL4h — account rehearsal/report** (tools; after TOOL4j). Stub window limits labelled Daoris-only; parallel account run. Rotation §8/D130 §5.4.
- [ ] **TOOL4i — installed rotation** (owner). Parallel every-account run and interruption proof remain. D130 §11; preserve sequential evidence.
- [ ] **AGT2c — vendor pins** (owner; two downloads). Verify update controls and native layout. D67/channel evidence; observed pins.
- [ ] **BRW14 — downloads as attachments** (design first). Define session download destination and quest transfer. D78/CHR3 §3.2.

## History and reliability visible to the person

Contracts: `docs/2026-10-07-history-clearing-design.md` (D153), D158,
D88/D102 cleanup notes, machine log §4. Installed clears require the owner's existing authorization.

- [ ] **ASKHIST1di — installed history paging** (parent). Seen 2026-10-11 in 中文: a search's count and coverage, no match, a hit cut around its words, one Han character. Remaining: over 200 conversations (*Show more* and its focus; this machine has 7), English, the dock at 300 and 430 px. D158's ASKHIST1d2 note.
- [ ] **AUTOTIDY1b — measure strict cleanup guards** (parent; installed AUTOTIDY1, after one week). Count ignored-file/unmoved-branch holds before deciding relaxations. D88 evidence.
- [ ] **AUTOTIDY1c — automatic squash cleanup** (driver; after AUTOTIDY1b; decide first). Consider content proof plus merged PR state, preserving recovery refs. D88/D102; Process guards.
- [ ] **QUESTBACK1 — hand back without a session** (owner's decision first; then service → driver + web). No hand back exists from anywhere: `QuestTransitions.Allows` lets nothing move back to open (D46 §3), and D126 ~:570-573 parks it. A new operation kind (quest-operations.md's eleven places), so a decision amending D46 §3 and D68 must say which taker facts survive (the claim, `Awaits`, set-ups, the ledger's carry-on, a remote's held claim). The label cannot be *Send back…* (DiffPane's); *Hand back…*/交还… to check against D116. After QUESTCLOSE1b1 (same files).
- [ ] **HIST1h — installed clear** (parent; republish a–f first). Verify counts/bytes, terminal/page agreement at 1280/888/680 px, themes/languages. D153 §12.

## Consistent screens

Contracts: plugin-screen design (D119), frame model (D118). Order: PLUGUI1c → f → g;
FRAME1i precedes PLUGUI1h. Installed looks cover both themes/languages.

- [ ] **NAME2b — verify four labels**. Inspect help.setup, scope.every, signin.titleNew and work.group.noCheckout at 888 px; rename or accept. NAME2 handback.
- [ ] **PLUGUI1f — complete plugin page** (after c/e). Health, Points, Agents, Servers, Activity, Data, Source and folder install. D119.
- [ ] **PLUGUI1g — plugin checks** (after f). Keep trial and run tests in a home copy. D119.
- [ ] **FRAME2b — maximized restore under pointer** (owner sends request). Wait for runtime position support, then update handler. D56/D32; owner response and handler proof.

## Workflows

Contract: `docs/2026-10-09-workflow-design.md` (D157), §14 order/proofs.
Foundation precedes editor/proposals/intake; waits/checks/stages follow the gate; installation last.

- [ ] **WORKFLOW1e2 — quest-specific choice** (service/driver; after e). Field, census, person's door and selection for quests without asks. §4.1/D156.
- [ ] **WORKFLOW1g — workflow editor** (web/modules; after d/e). Presets, save, insert/failure/move/remove, drawer, selection, terminal twins and keys; then Ask Daoris's workflow card gains *Open in the editor*. §6; 680 px/bilingual proof.
- [ ] **WORKFLOW1h2 — workflow proposal, changes and kinds** (after h). `changes` against a base (the CLI's change language twinned in C#), `kind` and `import`. §8.1, §4.7.
- [ ] **WORKFLOW1i2 — the intake sets a kind that lowers nothing** (driver/service; design first). §4.3 lets an agent set a kind on quoted words where `Involvement.Lowers` is empty in every repository the work reaches; the service cannot judge that, so it needs an agent-facing driver door. D157's WORKFLOW1i-a note.
- [ ] **WORKFLOW1j — wait on pull request** (driver/examples; after f/PLUGHOOK1b). Backoff work/state only for awaited runs. §3.2/§5.4; wait/occasion/plugin tests.
- [ ] **WORKFLOW1k — outside checks** (driver/examples; after j). Commit-specific checks, unknown/stale distinctions, failure policy and all-member joins. §3.3/§3.5/§3.7; fake-tool tests.
- [ ] **WORKFLOW1l — stages and go-aheads** (driver/examples/service; after k). Declared stages; production by person's press; check outcome before retry. §3.6–§3.7.
- [ ] **WORKFLOW1m — workflow rehearsal** (tools/examples; after k). Kind selection, path hold, failed check/send-back, staged person's press. §4.4/§5.
- [ ] **WORKFLOW1n — installed workflows** (parent; republish a–h). Current, save named workflow and follow run to PR at 1546/888/680 px, themes/languages. §6–§8.

## Person-only doors

Contract: `docs/2026-10-09-person-door-design.md` (D156), §10 sequencing/proofs:
a → b, c → d, e → f → g → h → i. Keep person and driver authorization distinct.

- [ ] **PERSONDOOR1c — publish attribution** (service; after a). byAgent everywhere the operation census requires; old records preserved. §5.1; sync/host/census tests.
- [ ] **PERSONDOOR1d — driver authorization** (driver; after b). Strip person key from every child; loopback-only client; confirm refused doors in window. §2.2/§4.
- [ ] **PERSONDOOR1e — CLI authorization** (CLI; after b). Loopback-only key and confirmation for connect/retire/import. §2.2/§4; shared-code fixture.
- [ ] **PERSONDOOR1f — page authorization** (web; after b/c). In-memory key, stale refresh, confirmation card, adoption notice and agent attribution. §2.2–§2.3/§4.2/§5.1.
- [ ] **PERSONDOOR1g — shell authorization** (modules; after d/f). Per-start key, input proof, bridge route; none for adopted host. §2.2–§2.3; squatter/adoption tests.
- [ ] **PERSONDOOR1h — gates carry authorization** (tools/web; after d/e). Stub unauthorized person acts refused; own doors accepted. §3/§4; family/web/deploy.
- [ ] **PERSONDOOR1i — every host enforces** (service; after h). Mint and report missing key once, enforce on bare hosts. §2.1/§4.3/§9.
- [ ] **PERSONDOOR1j — protect confirmation slots** (service; decide). Reserve person's terminal admission against five-slot agent exhaustion. §4.2; six agent asks cannot block reserved terminal.
- [ ] **PERSONDOOR2 — beyond HTTP doors** (design; after PERSONDOOR1). Store/home files, connector token and remote key boundaries. D156 §7; decision before build.

## Second opinions

Contract: `docs/2026-10-08-second-agent-design.md` (D155), section proofs.

- [ ] **XAGENT1f2 — person's opinion presses** (service; with PERSONDOOR1). Anyway/myself local-only; clear opinions with history. §8.5; store/host tests.
- [ ] **XAGENT1f5 — answers under steps rules** (driver/modules; after f4). Judge correct occasion; refresh stale declared-only comments. §8.1/§8.5; Process gate/look cases.
- [ ] **XAGENT1f3 — omitted gate behavior** (driver). Failure carry-on, safe verify commands, unfinished-work help and chain/ask choices. §8.4–§8.5; gate/look tests.
- [ ] **XAGENT1g2 — quest/ask/struck doors** (web; after f3). Chain opinions and failure help through useOpinionActs. §9; bilingual tests.
- [ ] **XAGENT1h — intake reviewer choice** (driver/service; after c). Quoted choice or proposal only. §2.4; golden/refusal/stub proof.
- [ ] **XAGENT1i — second-opinion rehearsal** (tools/examples; after f/plugin maker). Findings reach work; disputes/missing reviewer hold. §6/§8.
- [ ] **XAGENT1j — measured reviewer posture** (driver; owner authorizes account spending). Try writing through each door; declare only observed refusal. §5.2/D53.
- [ ] **XAGENT1k — canon second reader** (canon/examples; parent's decision). §10 doctrine, resync and canon changelog. Verify/example rehearsal.

## Review environments

Contract: `docs/2026-10-08-review-environment-design.md` (D154), section proofs.

- [ ] **REVIEWENV1d2 — live review_serve** (service/driver). Loopback serving wire reaches shell before session ends; tool waits for served answer. §2.3/§2.6.
- [ ] **REVIEWENV1e — separate review process** (driver/modules; after d). Procedure quote, Keep it, port/readiness/output and owned-tree stop. §2.3; stub/port/stop tests.
- [ ] **REVIEWENV1f — intake review choice** (driver/service; after b). Level-driven step; quoted choice or proposal; proposed off creates none. §1.5–§1.6/§2.1.
- [ ] **REVIEWENV1i — canon see-it-run** (canon/examples; parent's decision). §7 doctrine, resync and changelog. Verify/example rehearsal.
- [ ] **REVIEWENV1g2 — Stop showing** (modules/web; after d2). Route ends tab serving; press beside Show again. D154 point 8; route/gate tests.

## Landing, trace and Git

Landing in sync with the line (the owner, 2026-10-15: *"the merge fix needs a rebase (I think we kind of do need this logic when checking for branch)"*, *"we just have too many work branches … verify why we have this in-sync issue"*, *"I cannot accept for 2 sessions/quests at the same time"*). The install's case: a pull request squash-merged a session's first commit; the session went on in its tree on the old base; Accept landed all three commits, so the next pull request carried a duplicate of merged work and conflicted. Older parts of the same ask kept trees offering Accept, and each press made another branch. Designed in D87's and D167's amendments of 2026-10-15 (LANDSYNC0), with D109's note of the same day.

- [ ] **LANDSYNC1b — the landing's sync, its `Process` cases** (driver tests; after 1). LANDSYNC1's fast half holds the judgement; over real git still: TREEFOLLOW1's own branch replayed in place, an advance whose line holds some of the commits, and D87's amendment point 3 (the tree moved to what landed). Proof: `Process` cases beside `TreeSyncTests`.
- [ ] **ACCEPTALL1w — one Accept for an entry and an ask, on the window** (parent; after republish). A session whose entry has two parts with work: *Accept all 2 parts…* under the Parts strip and its plan (where they land, in order, a superseded part with *Leave part n out*); *Group by ask*: *Accept the ask's N parts…* under the ask's heading, each repository named; both in zh (采纳全部 N 段…). D167's ACCEPTALL1 note.
- [ ] **THEMESTART1w — the first frame and the splash on the install** (parent; after republish). With the OS light and Dark chosen, restart: the frame, border, caption and splash dark from the first frame, and Monitor's caption with it; the opposite way too; System follows the OS. During the start the marquee stays until Overview has its data, and Settings → Logs says `splash.lifted` `by:page` (a `by:bound` warning is a finding). Read `matchMedia('(prefers-color-scheme: dark)')` then for THEMESCHEME1b. D22's THEMESTART1 note, D56's SPLASHDATA1 note.
- [ ] **PRESSSTEP2 — a landing says merging, replaying or handing off** (driver/modules/web). PRESSSTEP1's steps stop at `gate` and `landing`: merge, replay and the plugin's hand-off run inside one `TreeLands.LandAsync`, so telling them needs a progress callback through the driver library that `PressSteps.Reach` hears. D169's PRESSSTEP1 note. Proof: a `StatePublisherTests` row per step; a `DiffPane` reading.

- [ ] **REVIEWBASE2-ii — Review's *Bring up to date*** (modules/web; after 2-i, REVIEWBASE4). A route beside `SESSION_DIFF` (`SESSION_UPDATE`, a refusal as an answer, as `DiscardTreeAsync` is) reading the conversation's turn from the chat runner; the review head's behind line gains the press, its words no longer sending the person to Branches; both languages, stories. D164's REVIEWBASE2 note.

Contracts: D143/D145/D148/D149; plugin-hooks design;
`docs/2026-10-04-built-in-git-design.md` (D147 amended by D150).

- [ ] **PROCESS1d-iii — a session told its squash, and a conversation's process proven** (driver). The landing plan carries the rule's `squash`, and `ProcessWords` tells both a driven session and a conversation "It lands as one commit carrying your commit messages, so write each so it reads alone."; a Process case on both doors where a chat in its own tree (a git fixture) hears the process once ahead of its first words and records one `chat.told-process`, and one in the checkout hears none; `InstructionAccount.cs`'s "this machine's landing rule" remark. D164's PROCESS1b-ii and PROCESS1d-ii notes.
- [ ] **PROCESS1f — Ask Daoris proposes a repository's process from its own instructions** (driver/service). Reading the repository's instruction files through its knowledge, a setting proposal for the commit rule and the landing (pattern, key, squash), which the person applies; never taken as the process itself. D164's amendment point 5; D110.
- [ ] **PROCESS1g — Review's press says the yes under `ask`** (web/modules; after REVIEWDIRTY1, 1a). Where the commit rule is `ask` and the tree holds uncommitted work, Review offers *Commit*, which says the person's yes in the conversation as their words; the agent commits by its repository's conventions. D164's amendment point 1.
- [ ] **SESSCOMMIT1 — the doctrine names the process's source** (canon; then sync; after 1d). A project-agnostic canon rule: *where Daoris tells you the commit and landing process at your start, follow it; otherwise your repository's own rules stand*. Synced into this repository and both examples (`.claude/knowledge/canon-authoring.md` first). D164's amendment point 4.

- [ ] **LAND2e — post-landing advances** (driver/modules/web). Accept later commits; gone-tree chain parts; review/proposal explanations. D149/D113; both-door tests.
- [ ] **PLUGHOOK1b — GitHub work/state** (examples). Query head/PR status for squash cleanup. Hooks §2.7; fake-gh tests.
- [ ] **PLUGHOOK1d — refresh PR state** (modules/web; after c). Ask again, branch/sync codes, sweep/query words and failure cost. Hooks §2.4–§2.5.
- [ ] **LAND2d — automatic acceptance UI/rehearsal** (web/tools). Show acceptedBy/events; stub plugin and bare-origin proof. D145 note.
- [ ] **TRACE1c — missing trace links** (driver/service/web). Late rules, local operations, merges, carry-ons, advances, detached section and commit kinds. D143 note.
- [ ] **GIT1f — commit/history/blame/compare tabs** (web; after UX6h). Existing patch/trace views, blame gutter, compare sides. §2.4–§2.6.
- [ ] **GIT1g — fetch/create/delete** (driver/modules/web; after UX6h). One plan/apply judge at commit, console and log. §3.1/§3.3–§3.4; bare-origin tests.
- [ ] **GIT1h — explicit push/PR** (after g). Person's commit/ref press, guarded branches and fast-forward; preserve handoff. §3.1–§3.2; pre-push tests.
- [ ] **GIT1i — session Git doors** (web; after UX6h). Review comparison/landing/push/PR; Show in Git/history/blame. §2.7/§6.
- [ ] **GIT1j — offer managed Git** (web/driver; after TOOLS6/10). Setup step, identity and explicit switch. §5; setup/coverage tests.
- [ ] **GIT1k — Git proposals** (service/driver/web; after h). Confirm fetch/branch/push/delete. §3.3/D110; kinds/coverage.

## Evidence

Contracts: evidence design (EVID1/D144), landing-and-proof design (EVID2/D146).
EVID1a → b → c; corresponding EVID1 precedes EVID2; EVID1d also needs DEV5/7.

- [ ] **EVID1c — quest proof and holds** (web/driver; after b/DRIFT1d2). Results versus assertions, Check again and check proposal. §5–§6.
- [ ] **EVID1d — queue proof** (driver/service; after DEV5/7/b). Required gates, landing/path verdicts and no-queue state. §4; queue rehearsal.
- [ ] **EVID2a — capture requirement** (service; after EVID1a). Screenshot/answer kinds and verdicts; machine-private bytes/paths never sync. §9–§10/§12.
- [ ] **EVID2b — keep captures** (driver/examples; after EVID1b/EVID2a). Proof variable, validation/redaction/copy, manifest, prompts/intake. §9–§11.
- [ ] **EVID2c — capture pages/terminal** (web/modules/driver; after EVID1c/EVID2b). Proof sections, reopen capture/remove and save/remove verbs. §11–§12.

## Sessions

Contracts: session-management (D126 §9), session-messages (D137), answer-continues (D131),
pause-and-clean-up (D132), D133/D136. Installed canaries follow completed build rows.

Found driving the installed home clean on 2026-10-15 (the owner: *"please drive daoris and please also clean up all unfinished task/session in this local daoris so we can start the next round of tasks; currently we find a lot of issues because this design is for as automated as possible"*):


Co-working (D167, `docs/2026-10-14-co-working-design.md`; the owner's two tasks are the canary):

- [ ] **SESSONE1c-look — co-working on the installed window** (parent). Seen 2026-10-14 (中文): three sessions with parts and the strip. Left: acts on a part, archive per entry, *Group by ask*, the ask page, English; WAITSHOWN1, QUEUEEDIT1b, USERSHOWN1 on live turns; the ask's *Channel* on a live ask (post, *To*, *Reply to*, unread, a session's panel, a claim turn), republished 2026-10-15 on 3be8d382b.
- [ ] **SESSONE2i — posting to a teammate's ask** (design first). A post to an ask held elsewhere is refused `held-elsewhere` today; say how its holder takes the message (the sync's direction, the holder's sessions reading it), then build. D167's SESSONE2g note.
- [ ] **QUESTREBASE1 — pure rebase planner** (service; consider). Preserve operation identity, accepted-before-pending order and transaction. D69/D79; unchanged sync suite and loss tests.
- [ ] **MSG1d2 — queued chat survives restart** (driver/web; with MSG1c). Record ids/reach/withdrawals; take by id. Messages §3.1.
- [ ] **MSG1h — Codex next-step words** (driver; after measured STEER3). Messages §8.
- [ ] **MSG1i — native next-step words** (driver; after measured STEER3). Messages §8.
- [ ] **MSG1j — installed message canary** (parent). Messages §8–§9; a–f installed per original record.
- [ ] **STEER2 — Send now matches door** (all doors; after STEER1). Steering draft and queue-arrival explanation. D136 §4; measured proof.
- [ ] **STEER3 — measure other steering** (driver). Native stream-json and Codex turn/steer before implementation. D136 §5; one-turn evidence.
- [ ] **ANSWER1d — installed same-record resume** (parent). One row and session.answered resumed=true. D131 §6; real run.
- [ ] **DRIFT1e — follow-ups preserve requirements** (design first). Reopen parent corrections; closing notes are not requirements. D133 §5.
- [ ] **PAUSE1g — installed pause/abandon** (parent; after a–f). Pause/resume same tree; abandon preserves landed work. D132 §12.
- [ ] **SESSUX1k — limit-ended list state** (driver/web; after UX6e). Waiting carry-ons and account door. D126 §2.2/§9.
- [ ] **SESSUX1l — installed management** (parent; republish a–i). States, carry-on, stop/try-again/archive across widths/themes/languages. D126 §9.

## Economy, setup and recall

Contracts: session-economy (D127), workspace-setup (D124), setup-pilot lessons (D128),
knowledge review (D129). WSSETUP14b–e precede owner f/13; WSSETUP7 needs LAYOUT8.

- [ ] **COST1 — measure long-turn cost** (owner's decision). Week of context/cache evidence before ceiling/window/compaction proposals. METER1/D127.
- [ ] **SUBLOAD1c — the model trial** (parent). Twenty rows built by scout and worker; the gates and columns are in D160's notes. Proof still owed: the installed window's shots (D161 §2); the guard's live refusal is seen (D160's fourth batch). D160; `docs/2026-10-10-subagent-load-evidence.md` §4.
- [ ] **MODELROLE1 — Daoris's own sessions spend as this development learned to** (design; after SUBLOAD1c holds its targets for three batches in a row; the owner, 2026-10-17: *"you can apply the result to Daoris itself too, if this is tested and good enough for the long term or recursion"*). Carry each lever D160's batches prove into the sessions Daoris drives: a model per session role (orient, build, review, help) on every door; a work's orientation handed in as a pack from the knowledge store rather than read afresh by the building session; a session sized by its turns and context, handing over to a fresh one with a compact brief before the cost of carried context dominates (D163's hand-over); and each session's cost measured and shown (MIXAGENT3a-iii), with its role's targets. For recursion (D173), the same accounting reaches a native subagent once FABRIC-C1a binds it to its part. D160; decision before build.
- [ ] **SESSOPT1d — complete documentation relocation** (steward). Maintain ≤5300 words and ≤60 per row; finish §4.3 diagnostic evidence homes for FLAKE1/TEST1/REH1.
- [ ] **DOC7 — reading measurements**. session.read/session.skill by role and whole/partial. D127 §6.1.
- [ ] **WSSETUP7 — workspace setup UI/proposal** (all doors; after LAYOUT8). Workspace page and bilingual coverage. D124 §4.4–§4.5.
- [ ] **WSSETUP14b — knowledge stays put**. Declare documents.knowledge; index reads, sync never writes. Pilot §1.2–§1.3; twins.
- [ ] **WSSETUP14d — setup stays green** (after b; absorbs SETUP2). Before/after checks, preserved knowledge, moved readers and hand-index inventory. Pilot/D129; family proof.
- [ ] **WSSETUP14e — finish setup branch** (after d). Exact follow-up merge rule. Pilot §4.2; composer/rehearsal tests.
- [ ] **WSSETUP14f — complete pilot** (owner; after republish). Two repositories, green checks, knowledge declaration, 32768-byte root and restored budget. Pilot §4.
- [ ] **KNOW3a — 169-document bench**. Opaque-name scale, whole index reads and skill truncation. Bench §5/§6.1; repeatable evidence.
- [ ] **KNOW2a — recall probe** (meaning judged by owner). Paraphrases at recall 3/5. Review §2.G/§4.6; script/evidence.
- [ ] **KNOW2b — prompt headlines** (after KNOW2a/DOC7). Five maximum, tier reservation, silent-service omission. Review §4.6.
- [ ] **KNOW2c — chat prompt hook** (after b/probe). Failing-open UserPromptSubmit headlines. Review §4.6; both-door probe/settings tests.
- [ ] **WSSETUP13 — remaining repositories** (owner). Pilot-paced rollout, existing instructions and weekly parks. D124; original LAYOUT10 remainder.
- [ ] **ORIENT2c — repository index quest** (driver). Inventory/generator/granted commands/check/declaration only. D151 §2/§5; brief/family tests.
- [ ] **ORIENT2d — prompts use index ranges** (driver). Declared index preferred, fallback retained. D151 §3.3/§6; goldens.
- [ ] **ORIENT2f — standalone clone index** (examples/tools). Move/fail/regenerate/pass without Daoris. D151 §4; family proof.
- [ ] **ORIENT2g — post-adoption measurement** (owner install; read-only). ORIENT1e targets unchanged: halve calls/characters/searches, quarter whole reads, 80% uptake, no extra parks. D151 §5.3–§5.4.
- [ ] **KNOWUSE2b — all 46 questions** (owner chooses harness/account). Repository knowledge, no substituted owner answers. D135 §6; report before KNOWUSE3.
- [ ] **KNOWUSE3 — item-local review** (after KNOWUSE2b). Hint tier, never answer for owner. D135; stories/installed proof.
- [ ] **KNOWUSE4 — correction request** (owner may publish). Correct comparison and config-only contradiction; no cross-repository edit. Knowledge-use evidence §4.

## Plugin distribution

Contract: plugin-distribution design (D120), §7 proofs. Plugin-repository work is a request to its owner.

- [ ] **PLUGREPO2e — retire migrated examples** (after PLUGDIST1g). Three examples/tests replaced by package offers. §7.
- [ ] **WORKSHOP1a — workshop setting**. Home default and both doors. §2.1/§7.
- [ ] **WORKSHOP1b — workshop sessions**. §2.2/§7.
- [ ] **WORKSHOP1c — workshop view/proposal**. Creation uses workshop. §2.3/§2.5/§7.
- [ ] **WORKSHOP1d — handover/source**. §2.4/§7.
- [ ] **PLUGDIST1b — pack/release workflow** (request to plugin owner). §7.
- [ ] **PLUGDIST1c — HTTP packages**. Bound extraction before acceptance. §5.3–§5.8/§7.
- [ ] **PLUGDIST1d — package host answers**. Records say package, not folder. §7.
- [ ] **PLUGDIST1e — Available catalogue** (after PLUGUI1f). §6/§7, amended by D140.
- [ ] **PLUGDIST1a leftovers — routing/package safety**. Move host installation to PluginsCommand; package kind through d, bounds through c. Original handback.
- [ ] **PLUGDIST1f — first publish** (owner). Account, trusted publishing and prefix. §7.
- [ ] **PLUGDIST1g — package offers** (after f). §7.
- [ ] **PLUGDIST1h — repository signature** (held). Original §7 trigger retained.

## Declarations and managed tools

Contracts: development-documents (D122 §6), tools (D121 §7). DEV5 → UNBLOCK2 → UNBLOCK3 →
UNBLOCK6–8; TOOLS6/8 precede 9, TOOLS10 precedes managed-Git sessions, 11 last.

- [ ] **UNBLOCK4c — push canary** (owner grants runs). Both doors/local bare remote, aliases/flags/quotes. Archived procedure; unchanged tip and refusals.
- [ ] **UNBLOCK2 — declaration judge** (after DEV5). Safe commands beside gates, read from line. §3.1–§3.3/§6.
- [ ] **UNBLOCK3 — person's acceptance** (after UNBLOCK2/week of UNBLOCK5). Exact rules on both doors. §3.4–§3.5/§6.
- [ ] **DOC6 — example document standard**. D122 §6; DOC7 is D127.
- [ ] **UNBLOCK6 — declaration UI/proposal**. Bilingual. D122 §6.
- [ ] **UNBLOCK7 — measure Codex/dsh**. No grant before measurement. D122 §6.
- [ ] **UNBLOCK8 — first declaration** (owner). Week before/after asks. D122 §6.
- [ ] **TOOLS6 — intended Git configuration**. Allow-listed ssh/includes/version floors and guidance. D121 §2.5; TOOLS7 handback.
- [ ] **TOOLS8 — tool proposals**. D121 §4.3/§7.
- [ ] **TOOLS9 — managed-tool rehearsals**. Loopback lists, stubs and tamper refusals. D121 §6/§7.
- [ ] **TOOLS10 — managed-Git probe** (owner permits one start per agent). D121 §7.
- [ ] **TOOLS11 — real installs** (owner). Managed SSH Git, plugin Node, terminal PowerShell, gh/az landing. D121 §7.

## Code structure

**First, before new feature rows** (the owner, 2026-10-13: *"I think we do need to move the full refactor mentioned before
into priority since that will make the code better to develop"*; D166's note). Narrowed on 2026-10-14: a folder per module, a
`Domain/` for its model and rules, the rest flat, and duplication reduced (D166's note of 2026-10-14). Contract:
`docs/2026-10-12-code-structure-design.md` as amended. Moves are behaviour-preserving; new driver files go into their module's
folder.

- [ ] **STRUCT3OPLOOP — Review's opinion logic out of the loop** (driver; optional). `Loop/DriverOpinions.cs`, `OpinionDeliverer.cs` and `OpinionGateLook.cs` hold the pass, the delivery and the gate's look: what needs none of the loop's state moves into `Review/`, with no new ports. D166's STRUCT2g note. After PART10m these are classes in `Loop/` holding the front for its run machinery; the structure gate's `loop` rule lets nothing outside `Loop/` name them, so moving them needs that machinery out of the front first.
- [ ] **STRUCT5c-ii — work, settings, bridge and projects cut into scopes** (web). Each holds more than twenty sources at its top: cut each into scope folders, one folder per branch, its entry dropped from the structure gate's `wide` list. Design §4–§6; proof: the structure gate with the folder off `wide`, and the web gate.

**Duplication** (the owner: *"we more focus on modularize the code to reduce duplication and easier to manage"*). Evidence: `docs/2026-10-14-duplication-survey.md`, by section. After FLAT1; DEDUP1 to 3 touch the same files, so one at a time.


## Development and agent layout

Contracts: self-development (D115), agent-layout (D117 §7), entry-point evidence.
DEV5 → 6 → 7, then 8/9, then 10/11. Predictions wait for LAYOUT2 measurement.

- [ ] **DEV5 — landing queue**. Detached tree, safe gates/quiet rerun, locked advance and commit check. §4.2–§4.8.
- [ ] **DEV6 — concurrent lanes**. Reservation/locks, default cap three, recorded/prompt lanes. §3.2–§3.4.
- [ ] **DEV7 — ready/verdict**. Three failures reach person; done means landed. §4.1/§4.5–§4.6/D83.
- [ ] **DEV8 — queue UI/proposal**. Every verb has a door. §4.9/D110.
- [ ] **DEV9 — steward**. Records, dispatch, router ownership and lane-map edits. §5/DEV2.
- [ ] **DEV10 — first real user** (owner present). Queue/steward/records, revisit measured caps/strikes. D115.
- [ ] **DEV11 — second user/retirement**. Canon lanes/example, then replace merge tool with queue. D115.
- [ ] **LAYOUT2 — canary turns** (owner grant). Harness predictions/account flag from evidence §6; keyless half archived. D117.
- [ ] **LAYOUT5 — move doctrine** (alone; steward lane map). Manifest/sync/attributes/example together. §4.1/§4.3–§4.4/DEV2.
- [ ] **LAYOUT6 — brief and rooms**. Eight rooms, approximately 1300-word AGENTS under 32 KiB; CLAUDE import. §4.2/§7.
- [ ] **LAYOUT9 — lane rooms** (after DEV6). §2.5/§7.
- [ ] **BUDGET1 — budget scope** (owner decides). Align region-only behavior, README/design and analyze after decision. D59/REV3 F10.
- [ ] **TRUST2 — measured trust** (owner grant). Written-key honoring and parent/child trust. D73/evidence; exact-folder hold remains. Seen 2026-10-12 on the install: a driven session's transcript opened with *Ignoring 7 permissions.allow entries from .claude/settings.json: this workspace has not been trusted*.
- [ ] **AGENTS2a — DeepSeek key probe** (driver/CLI). No-key/invalid-key dsh measurement before twin declaration. D57/D67; ACP3 evidence.
- [ ] **PROC1d-iv — memo `NamesAsync` and ancestry by pair** (driver; only on a measured need). `diff --no-ext-diff` (`LandedProofs.NamesAsync`, twice a form) needs trees, not refs, and `merge-base --is-ancestor` is asked per pair many times in one judgement: memoise both by their oid pair within one judgement. No "one walk" ancestry. Contract: UIFREEZE1. Proof: `TreeSyncStartsTests`' count after PROC1d-ii and -iii still shows them as the larger part.
- [ ] **PROC1e — ten minutes, or a new bar** (decide; after PROC1b-d). PROC1a projects b and c to about 47 minutes under a full set's usual load and 24 on a quiet machine, the node-hook classes joining to about 20 quiet, and d to about 15; the 66 serial classes (670 s loaded, about 340 quiet) run alone in every plan. Ten minutes needs them shortened (`DrivenSessionInputTests` 109 s, `PluginKitTests` 102 s, `BranchTidyingProcessTests` 94 s) or run beside the pool with the merge tool's alone-rerun taking the load's failures; else the owner sets the bar the measures reach. Contract: FIX-LOG FLAKE1, PROC1a's paragraph; development.md:64-66. Proof: the chosen bar met three times, recorded there.
- [ ] **TESTGIT1b — shared GitFixture** (driver tests). Landing plugin/tidy/fixture/landing/auto classes; eliminate independent git runners. Serial Process proof.
- [ ] **TRYTOOLS1 — trial tools** (driver). Folder trial uses configured machine tools. D101/D121 §3; named-Node Process case.

## Remaining regressions, evidence and held work

Contracts and complete reproductions: original backlog, FIX-LOG entries, named decisions.
Decision/trigger rows stay held; diagnostic captures are not permission to change timeout policy.

- [ ] **REALCASE1 — complete installed real case** (parent/owner). Remaining Codex device sign-in belongs to owner; preserve prior seven-step outcomes in frozen row. D62.
- [ ] **ANSWER2b — resumed run through second look** (driver Process). One resume/record while next look runs. D131 note.
- [ ] **BGWAIT1c — native background cutoff** (driver/web). Coded cutoff instead of park after harness-killed work. D83/D137/D142; stream fixture.
- [ ] **LAND4b — failed automatic landing** (driver; decide). Unify Done+failed rule with person's press. D145/D102; rule case.
- [ ] **QUESTCLOSE1b4 — a lost move's note keeps its parts** (service; consider). A quest's `conflicts` (`QuestConflict`) carry a lost move's note but not its parts, so a lost person's done reads in English. If the page should word it, `QuestConflict` and the answer gain parts. D142 point 2; a sync test.
- [ ] **FREEZE1b — capture next stalled bar** (modules/driver; trigger: ui.stalled). Move measured cause off UI thread; request runtime-owned changes. D56/log §4.
- [ ] **ASKNAME1c — named-proposal twin** (service/driver/web). One case-sensitive fixture across tier/intake/page; register twin. D77; three-suite proof.
- [ ] **GOAHEAD2c — rehearsal go-aheads** (tools). Stub asks twice; first answer stays parked, second resumes once naming both. D135/phase 17a.
- [ ] **SQUASHTIDY1d — recovery refs** (driver/web). List/clear at both doors by person's word. D102/D50; Process/bilingual group tests.
- [ ] **SQUASHTIDY1g — ancestry on another branch** (driver; decide). Carried versus landed-elsewhere; offer/refuse consistently. D88/D102; content-offer/landing Process rows.
- [ ] **SWEEPCARRIED1b — carried branch wording** (web/driver). Exhaustive session head, terminal reasons and registered twin. D148/D102; bilingual/golden tests.
- [ ] **MCPSDK2 — protocol revision** (service; decide before SDK upgrade). Choose negotiation semantics before taking 2.x. MCPDISCOVER1; end-to-end probe.
- [ ] **SWAP2b — swap evidence** (tools). Marker distinguishes same-commit builds; retain longest holds from five loaded runs. D139/D60; failing marker proof.
- [ ] **SWAP2d — rollback by phase** (web/driver). Busy/move/start sentences and shared code table. D139/D41; bilingual twin cases.
- [ ] **THEMESCHEME1b — the held scheme reaches the open page** (desktop; seen on the install 2026-10-15 at c65581d). With the viewer's choice dark and the OS light, the kit holds it (`SHENORA.COLOR_SCHEME` `GET` answers `dark`) but the running main window's `matchMedia('(prefers-color-scheme: dark)')` stays false: the engine's setting does not reach a renderer already open (THEMESCHEME1's one unproven step). Find whether a reload, a new window or the kit's own notification carries it; if the kit cannot, ask its owner (with THEMEBROWSER1). D22's THEMESCHEME1 note. Proof: the eval true after choosing dark, false after System, on the install.
- [ ] **THEMEBROWSER1 — Daoris's browser follows the chosen theme** (desktop; needs the kit). Its separate Chromium process has no colour-scheme option at Shenora 0.21.1 (`ChromiumBrowserProcessOptions`), so its own UI follows the OS whatever the viewer chose: ask the kit's owner for the option, then pass the choice at the browser's start. D22's THEMESCHEME1 note. Proof: a capture of the browser with the OS set opposite.
- [ ] **TWINSTOP1 — the shell's stop bound, held by both sides** (modules tests). The service's shell-started drain (`InputEndStop.Drain`, 3 s) assumes the shell kills at 5 s (`HostSupervisor.StopWithin`), and nothing ties them: pin `StopWithin` in `HostSupervisorTests` beside the service's test. FIX-LOG 2026-10-15 (FLAKE2).
- [ ] **REALHOSTPORT1 — a test host is its own before it is up** (service tests). `RealHost.StartAsync` takes a host as up once anything answers on its port, so a port taken between `FreePort` and the bind talks to another host: check the process first, or match a per-start nonce in `/api/status`. FIX-LOG 2026-10-15 (FLAKE2).
- [ ] **FLAKE4 — the ACP stub's fetch to a test's quest door** (CLI tests). `setup-kit.test.ts`'s ANSWER1b case failed once in `verify` (2026-10-15, as an installed window started beside it): the stub said `turn failed: fetch failed` against `questDoor()`'s local server, so prompt 3 answered no `stopReason`; alone it passes. Find what refused the fetch (port, keep-alive, the door's close), make the stub say the error's cause, and hold it in a loaded loop. FIX-LOG when found.
- [ ] **WEBWAIT1 — the page's tests wait on what they wait for, under load** (web tests). The web gate failed once on `df95f79` (2026-10-17) in `WorkFrame.test.tsx` *asks the plan again with an entered branch name…*: a `vi.waitFor` with its default 1 s patience gave up at 2993 ms under four workers' load; alone it passes. A fast-suite failure fails the gate and is never re-run. Find the page tests whose `vi.waitFor`/`findBy*` rely on the default patience while a mocked call or a timer stands between, and make each wait on its condition with a patience a loaded machine meets (one shared setting, not per-test widening), as FLAKE5 did for the driver. Proof: the class of failure named in FLAKE1's FIX-LOG entry; the web suite green beside a plain build of another project.
- [ ] **FLAKE1 — diagnostic waits** (driver/modules tests). Instrument repeating failures; three loaded serial greens, named slow step. MOD8/PROC1/FIX-LOG.
- [ ] **WEBPORT1 — independent web hosts** (web). Allocate per-run port and pass base URL. REHEARSEPORT1; simultaneous-host test.
- [ ] **DEPLOYCOUNT1 — rehearsal-owned hosts** (tools). Count processes under this run's scratch only. Outside-process stand-in test.
- [ ] **TEST1 — capture Windows Node abort** (web/tools). Preserve reporter/exit at next 0xC0000409. FIX-LOG; no unsupported timeout tuning.
- [ ] **REH1 — captured canon-upgrade failure** (held). No tag until resolved or owner closes after clean runs. FIX-LOG/rehearsal transcripts.
- [ ] **CANON9 — WinForms pack** (held until second repository). Preserve two-repository bar. Original candidate.
- [ ] **TOOL5 — native adapters** (held until named tool). Measure each field. Toolchain §5/D23/D24/D57.
- [ ] **SEM2 — persistent vectors** (held until noticeable embedding latency). Existing SQLite/migrations; re-embed on model change. No lexical-only need.
- [ ] **PLUG7 — service plugin points** (held until requested). Reuse wire. D64.
- [ ] **A file tree in the dock** (held until browsing requested). D76; existing preview/terminal remain.
- [ ] **AFTER1 — multi-quest dependency** (held until real ask). Store dependencies and show unfinished predecessors. Future-directions §3/D65; planner/service tests.
- [ ] **MSG1k — fork terminal conversation** (held until real use). Messages §4.3; list/fork protocol stub.
- [ ] **PLUGHOOK2 — PR threads to work** (held until first manually carried request). Explicit review press carries person's words/new ask. Hooks §3.1.
