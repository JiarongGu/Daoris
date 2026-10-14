# Daoris (道衍) — open work

Open work only; move finished rows to `docs/task-archive.md`. Release changes belong in `CHANGELOG.md`.
This handover keeps every outstanding identifier. Detailed acceptance criteria, owner quotations and
earlier evidence remain at `38065d97:TASKS.md` in Git and in each section's contract; read the matching
row before implementation. The earlier condensation's supporting record is `a3b20973:TASKS.md`.

Start with `AGENTS.md`, `CLAUDE.md`, `.claude/INDEX.md`, `docs/README.md` and `docs/index/README.md`.
Use discovery skills, then read the contract. Integrate through `tools/merge-branch.mjs`; run rehearsals
serially. Owner-only work, downloads, account spending, installation, publication and decisions retain
their existing grants and holds. A dependency named below must be complete before its dependent starts.

Development remains at `0.0.x`, unpublished, with no live consumers. Adoption is the owner's call.
Current recovery, review coverage and verification: `docs/2026-10-10-integration-review.md`.
Historical installation receipts and adoption mechanics remain in their evidence records.

## Workspace cleanup

- [ ] **DOCSCRATCH1 — remove blocked verification scratch** (command review). Deletion of ignored `_fixtures/doc-system-preview-git/` metadata remains rejected after explicit owner authorization and repository-local defaults. Inspect the exact target, then native removal when permitted; proof: absence. Restriction and retry receipt: `docs/2026-10-10-code-and-docs-review.md`.

- [ ] **CLEANUP1 — remove the empty harness-held directory** (command review). `.claude/worktrees/agent-a3b92da3b8bb159c9` is empty and unregistered; backups are preserved. Native removal remains rejected after explicit authorization and repository-local defaults. Proof: recheck emptiness/registration, remove when permitted and verify absence; retry receipt: the maintenance review.

## UI/UX first

Start with NEW1 and HEAD2 (the owner's, 2026-10-12), then the place-name inconsistencies (UX6i2a/b, UX6i3).
Contracts: platform UX §4/§6, `docs/2026-10-05-ux7-design.md` (D152), `docs/2026-10-12-entry-and-heads-design.md` (D162),
`docs/2026-10-04-ux6-redesign.md` (D150, §12 proofs). UI work needs stories, behavior tests and
installed-window evidence in both themes/languages; Storybook alone does not close an installed proof.

- [ ] **UXFIX2bi — installed confirmations look** (parent; after republish). Retire right on 2026-10-11's second look (its own question; focus back to the press with a pointer-like click); the rest need data the machine lacks: tool, plugin, pause, abandon, decline, close, finish, archive, discard, both themes/languages.
- [ ] **UX7b — installed account UI** (web). Verify recovered account rows and add/name/join flow at 1546/680 px, both themes/languages. D152 §4.
- [ ] **ACCTEDIT1b — installed refused edits** (parent; after republish). Verify rename, workspace use and add refusals retain drafts, including dark borders. D152 §4; dated shots.
- [ ] **UX7e — installed menus and heads** (parent; after republish). Verify keyboard menus, shortcuts, composer editing, head geometry and titles at 1546/680 px. D152 §6.
- [ ] **ENTRY1bi — installed go to what waits** (parent; after republish). A go to `sessions/waiting`, `quests/held` and `quests/asks`, and the waiting starter, each with a group to show: its heading at the top, focused. D161's ENTRY1b note.
- [ ] **UX6i2i — installed go cards** (parent; after republish). Ask Daoris answering "where do I turn a plugin on?" and "where is Convergence?": a go card reading *Open Plugins.* or *Open Knowledge → Convergence.* that opens the right place and mode.
- [ ] **PLUGTOOL1c — tools on their plugin** (web/modules/driver; after PLUGUI1c). Tools retains Daoris's own. UX6 §7.5.
- [ ] **COWORK1 — agents working together** (design; owner's direction given 2026-10-12). Define progress sharing, questions and handoffs. D32/D65/D145/D149; decision before implementation. The owner, 2026-10-12, after a Codex-driven task neared its usage limit: *"I just used a codex for main drive for a new task but I don't see a way to mix the use of both agents (like codex to claude) so currently when usage is almost full I'm kind of stuck on that task, also it should be able to run subagents to use other agents to support the task and we need a clear view to show those behaviours (and should be decided by the agent itself or triggered by user)"* Hand-overs and helpers are designed (D163, `docs/2026-10-12-mixed-agents-design.md`) and built by the MIXAGENT rows below; this row keeps what D163 leaves: progress shared and questions asked between agents working at once.
- [ ] **SESSFIELD1 — the session record's field chain, written down** (docs). A field answered on this machine only is added in a fixed set of places that no document lists: the record's property, `EnsureAsync`, `CreateAsync` with its INSERT and reader, the ledger's opens, the request and response records, `ToSession` on loopback, the driver's `ServiceClient` write, `Prior` and `PriorSession`, and the three stand-ins (`StandInLedger`, `ChatGoOnTests`' `ChatLedger`, `AskAndWaitTickTests.StandIn`). A knowledge row the next such field cites. D163's MIXAGENT1a-iii note.
- [ ] **MIXAGENT1c — the agent asks to hand over** (service/driver; after 1a). `handover_propose` (its own note, kept as its claim) into the box, settled by the `handOver` rule (`ask` default, `on`, `off`) in Settings and `driver.json`. Design §2, §5.
- [ ] **MIXAGENT2b — *Ask another agent…*** (modules/web; after 2a). The person's press on a session's page; the helper's rows. Design §3.
- [ ] **HELPERACTS1 — a helper's and a reviewer's page offer no word-taking acts** (web). `offeredActs` (`work/acts.ts`) still offers *Go on with another agent…* and other acts that take words on a helper's or reviewer's conversation, whose records refuse words and resume (D163's MIXAGENT2a-i note); narrow them by `isHelper`/`isOpinion`. Proof: acts.test cases for both.
- [ ] **NAMEZH1 — 本机 in a helper's note** (web). `note.helper.why.asker-gone` says 这台机器 where the glossary's name is 本机; correct it and drop its entry from `names.test.ts`'s list. D163's MIXAGENT3a-i note.
- [ ] **MIXAGENT3a-ii — helpers and reviewers under the work they serve** (web; after 3a-i). The rail and lists nest a helper under the session it serves (`serves`) and a reviewer under its work (the opinions join), each staying its own place where its parent is not listed; groups.ts pure and tested. Closes XAGENT1g4d. Design §4.
- [ ] **MIXAGENT3a-iii — cost per row, and a helper's answer as its block** (web/driver; after PROCESS1d-ii). The rail's rows show what a session used (`useUsage`, shell only; *not measured*, never zero, where it is absent); the conversation shows a helper's answer as that agent's block (a driver event field beside `Opinion`, read by `conversation.ts`). Design §4.

## Accounts and browser

Contracts: toolchain (D57 §3), account rotation (D125), account use (D130), browser (D78/D84).
Original row proofs remain in the frozen backlog; measurements precede declarations.

- [ ] **ACCTUX4i — installed account cells, and the row budget** (parent). 中文 dark and light at 1546 and 680 seen 2026-10-11; English, a key and Codex rows remain. The *Now* header stands over an empty column, the cells starting under *Used for*. Settle D152's row budget against D125's ACCTUX4 measures.
- [ ] **ACCTPLAN1 — plan, spend and reset credits** (driver/modules/web; design first). Nothing reads them yet (D125:898-899); show them in a fold, and never redeem a credit without `InlineConfirm`. Second-opinion review lines 132-133.
- [ ] **CODEXUSE2 — installed Codex reading** (parent/owner). Pins are verified; account sign-in remains unavailable. Use CODEXUSE3's own-sign-in reading meanwhile. D57/D63/D125.
- [ ] **CODEXKEY1 — Codex API-key account** (measure first; owner supplies throwaway key). Determine spawn variable for both tools before declaring support. D67/D125; real variable-start proof.
- [ ] **CURVEROOM1 — "both are over their curves; it is the least over"** (driver/web; if the window shows it unclear). `WalkStep.Room` is said today by the other account's own reason; a coded reason of its own, en/zh. D165's SWITCH1b-ii note.
- [ ] **SWITCHMODEL1 — a model family's week** (driver; measure first). The per-model weeks (`seven_day_opus`, `seven_day_sonnet`) take the weekly curve where the agent reports them; `get_usage` answered them null on every account read (`docs/2026-10-12-usage-read-evidence.md`). Read them when one is reported. Design §2.
- [ ] **HANDWORDS1 — words said to a conversation handed over or moved go to the one that continues it** (driver/modules/web). Today the person's words to the old conversation are kept on its record and can restart it (go on) in the tree its successor holds, and a word said during a take lands there too. Words held while a take is in flight go to the latest successor once it opens; after the take, words to the old id are refused with a new never code `continued` naming the successor ("went on in conversation {to}; say it to that one"), mirroring `superseded`; the old page draws "This conversation went on in {title}" with *Open session*; GoOn and come-up never restart a record that has a successor. Split: (i) driver and modules with the code's table entry and catalogue lines, (ii) the page's line and door. D163 point 1; D165 point 3; design §4 ("the person's next words go to it"); the scout's pack in the SWITCH1c-ii merge's notes.
- [ ] **SWITCH1c-iii — a quest's carry-on says the curve moved it** (driver; after 1c-i). A quest's next session already starts on the account the curve ranks first (SWITCH1b-ii); where that moves it from the last session's account by the curve, its record's `from` says `curve`, by `daoris`, and its opening line says why. Design §4.
- [ ] **SWITCH1d — the switch in view** (web/modules; after 1a, USAGEREAD2a). Accounts' windows against the curve with source, age and *Read now*, read as the screen opens where older than a minute; the curve's refusal code and its en/zh sentence; *Smart*, *My own curve* and *Off* with the curve's chart and point editor; Ask Daoris's `use` keys (`switch`, `curve`), its card's words in the curve's terms (today "counts an account near its limit at 85%": `HelpAgentProposals.cs`, the service's `KnowledgeTools.Help.Agent.cs`, `HelpProposalBox.Agent.cs`); the page's said line marks a window over its curve as the terminal's does, held by a page twin of `usage-curves.json`; the *near its limit* pill and *Near at* label in the curve's words. Both languages, stories, installed proof. Design §5.
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
- [ ] **XAGENT1g4d — the reviewer beside its work** (web; consider). The row says whose work it reads (XAGENT1g4c); group it under that session in the rail, or give the line a press to it. §9; D155's XAGENT1g4c note.
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

Landing in sync with the line (the owner, 2026-10-15: *"the merge fix needs a rebase (I think we kind of do need this logic when checking for branch)"*, *"we just have too many work branches … verify why we have this in-sync issue"*, *"I cannot accept for 2 sessions/quests at the same time"*). The install's case: a pull request squash-merged a session's first commit; the session went on in its tree on the old base; Accept landed all three commits, so the next pull request carried a duplicate of merged work and conflicted. Older parts of the same ask kept trees offering Accept, and each press made another branch. Design first: amend D87 (a landing keeps the session's commits as made) and D167 (an entry's work).

- [ ] **LANDSYNC0 — the design: a landing in sync with its line, and an entry's one Accept** (decision; first). Amend D87 as D109 did: where the line holds some of a tree's commits by content (patch id, as `git cherry` says `-`), the landing builds its branch from the line with only the commits it lacks, by WSR6's replay in a tree of Daoris's own; what it left out, and why, is said before the press. Amend D167: an entry lands as one, its parts' work in order on one branch per repository, and *Group by ask* lands the ask's entries in one press. Settle: a replay conflict (refuse naming the files, and offer the session the words to rebase), a part whose work another part superseded (the install's `a0c7ed37`), moving the session's own branch to what landed, and the Daoris git that cannot fetch (judged against the last fetch, said).
- [ ] **LANDSYNC1 — Accept lands only what the line lacks** (driver/modules/web; after 0). `LANDING`'s plan and `LAND_SESSION_TREE`'s press per LANDSYNC0; SQUASHTIDY1f's whole-tree proof stays for a tree the line holds entirely. Proof: a git-fixture Process case where a squash-merged first commit is left out and the next two land on the line cleanly; the plan's words in both languages.
- [ ] **LANDSYNC2 — a session going on in an old tree starts from the line** (driver; after 0). A resume, words going on, a carry-on and SESSONE1d's taken-up tree: where the line holds some of the tree's commits by content, REVIEWBASE2-i's one-tree replay brings it up to date first; a conflict leaves the tree and tells the session in its instruction. Proof: a Process case per door.
- [ ] **LANDSYNC3 — *Bring up to date* counts what the line holds** (driver). WSR6's plan said *replays 3 commit(s)* for `daoris/s-906da175`, whose first is squash-merged, and *replays 1* for `daoris/s-6062dcc3`, whose only one is: say the commits the line holds by content and drop them, and a branch it holds entirely moves onto the line as *holds nothing of its own*. Proof: `TreeSyncTests`.
- [ ] **ACCEPTALL1 — one Accept for an entry, and for an ask** (driver/modules/web; after 0, 1). The entry's head offers Accept over every part with work to land, and *Group by ask* over every entry; each repository's rule and gate apply, each answer said per repository, a refusal holding only its own. Proof: modules and web tests; the window with the owner's ask.

- [ ] **REVIEWBASE2-ii — Review's *Bring up to date*** (modules/web; after 2-i, REVIEWBASE4). A route beside `SESSION_DIFF` (`SESSION_UPDATE`, a refusal as an answer, as `DiscardTreeAsync` is) reading the conversation's turn from the chat runner; the review head's behind line gains the press, its words no longer sending the person to Branches; both languages, stories. D164's REVIEWBASE2 note.
- [ ] **REVIEWBASE5 — the session head offers no *Accept…* where Review says nothing is beyond the line** (web). REVIEWBASE4 took Accept out of Review's panel for a tree that holds nothing beyond its line; the session head's own *Accept…* (SessionHead) reads the landing plan alone and may still offer it. Read the review's `behind.ahead` there too. Proof: a SessionHead test with `ahead` 0; the window. D164's REVIEWBASE4 note.
- [ ] **REVIEWBASE3 — a chain step that merged the line reviews its own work only** (driver). Where the record's base (the parent's tip) and the line's merge-base are incomparable, the review shows the parent's commits; measure against a merge of the two (`git merge-tree --write-tree`). A Process case in `ReviewBaseTests`. D164's REVIEWBASE1 note.

Contracts: D143/D145/D148/D149; plugin-hooks design;
`docs/2026-10-04-built-in-git-design.md` (D147 amended by D150).

- [ ] **TREEFOLLOWWEB1 — the page names the branch a moved tree is on** (web; en/zh). The head's "its work" line matches its sweep row by the layout's `daoris/<folder>` (`work/diff.ts:47`), so once the tidy removes that branch it finds nothing: read the session row's real branch instead; and the Accept line says *lands `feature/…`, the session's own branch* from the `LANDING` answer's `own`. D164's TREEFOLLOW1 notes; proof: a window shot of the head and the Accept line.
- [ ] **PROCESS1d-iii — a session told its squash, and a conversation's process proven** (driver). The landing plan carries the rule's `squash`, and `ProcessWords` tells both a driven session and a conversation "It lands as one commit carrying your commit messages, so write each so it reads alone."; a Process case on both doors where a chat in its own tree (a git fixture) hears the process once ahead of its first words and records one `chat.told-process`, and one in the checkout hears none; `InstructionAccount.cs`'s "this machine's landing rule" remark. D164's PROCESS1b-ii and PROCESS1d-ii notes.
- [ ] **KEYPARITY2 — a key's class subtraction** (driver/cli). .NET reads `[a-z-[aeiou]]` as a class minus a class, JavaScript as a class then `]`: both compile, so each twin finds a different ticket. Refuse `-[` inside a class on both twins with one sentence. D164's PROCESS1b-i note.
- [ ] **TWINWORDS1 — the cards' copied sentences, held to the terminal's** (driver/cli). Ask Daoris's landing, helpers and commits cards say the CLI's sentences by hand (`TicketSays`, the squash lines, the mode sentences in `HelpSettingProposals`); move them beside the rules' twins on a shared table both suites read. D164's DOORS1a note; `.claude/knowledge/twins.md`.
- [ ] **PROCESS1e — a session's Details say its process** (web/modules; after 1a, 1b). The commit rule, the landing and their source on the session's page. D164's amendment point 3.
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

- [ ] **AWAITSTALE1 — a closed quest no longer names the question it waited on** (service, web). The service keeps a quest's `awaits` after it closes (the install's `711d252413bb` and `3068f74791db`, done, still naming theirs), which held the person's words (FIX-LOG 2026-10-15; the driver now reads it only while taken, `QuestView.Awaits`) and can read *awaiting reply* on the quest page: answer `awaits` only while taken, or clear it at the close, the history kept where it is read.
- [ ] **TICKSHOWN1 — a refused start is said on the session** (driver/web). Words planned to go on and refused at the start (the FIX-LOG 2026-10-15 case) showed only *going on* in the sessions list, at every look; the refusal lived in the tick's events alone. A start the service refuses for a record the person wrote to puts the refusal on that record's row, as a hold's reason is, so the person reads why their words wait. Proof: a SessionGroups case; the window.

Co-working (D167, `docs/2026-10-14-co-working-design.md`; the owner's two tasks are the canary):

- [ ] **SESSONE1c-look — co-working on the installed window** (parent). Seen 2026-10-14 (中文): three sessions with parts and the strip. Left: acts on a part, archive per entry, *Group by ask*, the ask page, English; WAITSHOWN1, QUEUEEDIT1b, USERSHOWN1 on live turns; the ask's *Channel* on a live ask (post, *To*, *Reply to*, unread, a session's panel, a claim turn), republished 2026-10-15 on 3be8d382b.
- [ ] **SESSONE2g — a teammate's ask's channel, read here** (service/web). The sync mirrors every message of the workspace, but `GET /api/asks/{id}/channel` answers only an ask held on this machine: a channel of an ask held elsewhere, readable by its id. D167's SESSONE2d note.
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
- [ ] **MODELROLE1 — a model per session role** (design; after SUBLOAD1c). If the split holds, managed sessions orient, build and review with a model configured per role, on every door. D160; decision before build.
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
- [ ] **MOVETOOL1 — a tracked move tool** (tools). A `tools/` script that moves files with `git mv`, respells relative imports, and checks its counts and that every import still resolves; the CLI's and the page's moves each built one in scratch. Contract: `file-tool-discipline`; proof: its own test on a fixture tree.

**Duplication** (the owner: *"we more focus on modularize the code to reduce duplication and easier to manage"*). Evidence: `docs/2026-10-14-duplication-survey.md`, by section. After FLAT1; DEDUP1 to 3 touch the same files, so one at a time.

- [ ] **DEDUP7c — the short ids left** (service, driver). The Mcp host's three `Math.Min(7, …)` cuts cannot see the core's internal `Clipped.ShortCommit`; four unguarded `[..7]`/`[..8]` cuts (`NoteCodes.cs:942`, `LayoutGit.cs:36`, `RegistrationWorld.cs:84`, `PluginTrials.cs:290`) throw on a short id: decide what a short id should do, then one helper each side. D166's DEDUP7b note.

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
- [ ] **LAYOUT8 — setup UI/proposals**. Repository Setup and setup kind. §6.1/§6.5/UX6f; installed bilingual proof.
- [ ] **LAYOUT9 — lane rooms** (after DEV6). §2.5/§7.
- [ ] **BUDGET1 — budget scope** (owner decides). Align region-only behavior, README/design and analyze after decision. D59/REV3 F10.
- [ ] **TRUST2 — measured trust** (owner grant). Written-key honoring and parent/child trust. D73/evidence; exact-folder hold remains. Seen 2026-10-12 on the install: a driven session's transcript opened with *Ignoring 7 permissions.allow entries from .claude/settings.json: this workspace has not been trusted*.
- [ ] **AGENTS2a — DeepSeek key probe** (driver/CLI). No-key/invalid-key dsh measurement before twin declaration. D57/D67; ACP3 evidence.
- [ ] **PROC1 — Process suite below ten minutes** (tests/tools). Measure/isolate/reuse fixtures; three serial-equivalent green timings. MOD8/FLAKE1; preserve measurements.
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
- [ ] **FLAKE1 — diagnostic waits** (driver/modules tests). Instrument repeating failures; three loaded serial greens, named slow step. MOD8/PROC1/FIX-LOG.
- [ ] **FLAKE2 — the person-door host's stop under load** (service tests). `PersonDoorProcessTests`' host missed its 15 s stop once under load (2026-10-14); find what the stop waits on.
- [ ] **FLAKE3 — a SQLite handle disposed under parallel tests** (service tests). `SearchTests` threw `ObjectDisposedException` once (2026-10-14) while ten classes call `ClearAllPools()`; prove the race, then isolate pool clearing.
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
