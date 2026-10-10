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

Start with UXFIX2b3, ASKHIST1d2, ACCTUX1b and the remaining place-name inconsistencies (UX6i2a/b, UX6i3).
Contracts: platform UX §4/§6, `docs/2026-10-05-ux7-design.md` (D152),
`docs/2026-10-04-ux6-redesign.md` (D150, §12 proofs). UI work needs stories, behavior tests and
installed-window evidence in both themes/languages; Storybook alone does not close an installed proof.

- [ ] **UXFIX2bi — installed confirmations look** (parent; after republish). The UXFIX2b1/2a/2b asks in the window (tool, plugin, retire, pause, abandon, decline, close, finish, archive, discard), both themes/languages: focus on open, pending, refusal inside, returned focus.
- [ ] **UX7b — installed account UI** (web). Verify recovered account rows and add/name/join flow at 1546/680 px, both themes/languages. D152 §4.
- [ ] **ACCTEDIT1b — installed refused edits** (parent; after republish). Verify rename, workspace use and add refusals retain drafts, including dark borders. D152 §4; dated shots.
- [ ] **BRSCOPE1b — checkout scope and sync workspace** (modules/service/driver; after BRSCOPE1a). Share checkout-scope fixtures; carry workspace through sync proposals. D150 notes; three-door proof.
- [ ] **UX7d — remaining visual findings** (web/driver). Absent-agent Install, strip marks, version, compact Chinese summaries and coded opening notes. D152 §4.6.
- [ ] **UX7e — installed menus and heads** (parent; after republish). Verify keyboard menus, shortcuts, composer editing, head geometry and titles at 1546/680 px. D152 §6.
- [ ] **UX6g2b — the go twin reaches the workspace's page** (design first; driver + web twin). Retire the `workspace`/`permissions` domains into `Kept`; decide whether the page's tabs and sections become go parts. D150 §3.1/§4.3, UX6i2a note; twins.
- [ ] **UX6g3 — the page's own words name the workspace's page** (web). `projects.json:41-42` ("Settings → Workspace lists it", en/zh) and comments in `opener.ts`, `SettingsView.tsx`, `bridge/driver.ts`, `help/starters.ts`. D150 §3.1.
- [ ] **UX6h — Git inside Repositories** (web; after GIT1d; absorbs GIT1e). Branch kinds and graph; retire Git place. UX6 §4.4/D147.
- [ ] **UX6i2i — installed go cards** (parent; after republish). Ask Daoris answering "where do I turn a plugin on?" and "where is Convergence?": a go card reading *Open Plugins.* or *Open Knowledge → Convergence.* that opens the right place and mode.
- [ ] **UX6i3 — "Settings → Agents" leftovers** (driver/modules/service). UX6e retired it; `DriverModule.Accounts.cs:11`, its tests, room tests and the `agents` domain word in the service still name it. D150's UX6e notes.
- [ ] **PLUGTOOL1c — tools on their plugin** (web/modules/driver; after PLUGUI1c). Tools retains Daoris's own. UX6 §7.5.
- [ ] **COWORK1 — agents working together** (design; owner's direction required). Define progress sharing, questions and handoffs. D32/D65/D145/D149; decision before implementation.

## Accounts and browser

Contracts: toolchain (D57 §3), account rotation (D125), account use (D130), browser (D78/D84).
Original row proofs remain in the frozen backlog; measurements precede declarations.

- [ ] **ACCTUX4i — installed account cells, and the row budget** (parent; after republish). Shots at 1546/680, English light and 中文 dark: read, never-read, key and own sign-in, cool-off, Codex. Settle D152's row budget against D125's ACCTUX4 measures.
- [ ] **ACCTUX4b — Next start above the accounts** (web). One sentence naming the next account and why, with *first* marking `scope.next`. Second-opinion review lines 125, 128-133; D152 §4.2; `agents.test.ts`, `AgentsView.test.tsx`, stories at 52rem/37rem.
- [ ] **ACCTPLAN1 — plan, spend and reset credits** (driver/modules/web; design first). Nothing reads them yet (D125:898-899); show them in a fold, and never redeem a credit without `InlineConfirm`. Second-opinion review lines 132-133.
- [ ] **WINDOWNAME1 — `<n>-minute` windows in the reader's language** (web-settings). Codex's other windows show *90-minute* in 中文 too. D125's CODEXUSE1 note; a 中文 case in `accounts.test.ts`.
- [ ] **ACCTNAME1 — email names read once** (CLI/driver/web). Avoid duplicate email/name; size roster columns. D125 notes; long-name and equal-email cases.
- [ ] **KEYREPLACE1 — replace refused keys in place** (all doors; after ACCTUX1). Preserve account lists; clear refusal/cooling. D67/D125; key-file twins, route and row tests.
- [ ] **AGENTMARK1 — declared strip marks** (driver/modules; after ACCTUX2). Forward CC/Cx with each product. UX7 §4.6; roster/680 px proof.
- [ ] **CODEXUSE2 — installed Codex reading** (parent/owner). Pins are verified; account sign-in remains unavailable. Use CODEXUSE3's own-sign-in reading meanwhile. D57/D63/D125.
- [ ] **CODEXKEY1 — Codex API-key account** (measure first; owner supplies throwaway key). Determine spawn variable for both tools before declaring support. D67/D125; real variable-start proof.
- [ ] **TOOL6d — continue on another account** (driver/modules/web; after TOOL6b). Offer last plan/words to selected account. D130 §8–§9; both-door tests/look.
- [ ] **TOOL4l — account proposal doors** (service/driver). Use/keep/early/near, order, ready/cooloff. D125 §6/D130 §9/§16.6; kinds/coverage.
- [ ] **TOOL4h — account rehearsal/report** (tools; after TOOL4j). Stub window limits labelled Daoris-only; parallel account run. Rotation §8/D130 §5.4.
- [ ] **TOOL4i — installed rotation** (owner). Parallel every-account run and interruption proof remain. D130 §11; preserve sequential evidence.
- [ ] **AGT2c — vendor pins** (owner; two downloads). Verify update controls and native layout. D67/channel evidence; observed pins.
- [ ] **BRW14 — downloads as attachments** (design first). Define session download destination and quest transfer. D78/CHR3 §3.2.

## History and reliability visible to the person

Contracts: `docs/2026-10-07-history-clearing-design.md` (D153), D158,
D88/D102 cleanup notes, machine log §4. Installed clears require the owner's existing authorization.

- [ ] **ASKHIST1di — installed history paging** (parent; after republish). Over 200 conversations: *Show more* and its focus; a search's count and coverage; no match; a hit deep in a long line; one Han character; a found old conversation that goes on. Both languages, dock at 300 and 430 px. D158's ASKHIST1d2 note.
- [ ] **UXLOADMORE1 — the load-more convention** (contract). A list read a page at a time says "{shown} of {total}" with *Show more* under its rows, and the press moves focus to the first row it brought; Ask Daoris's history is the first. Platform UX §4; RAILSRCH1b follows it.
- [ ] **AUTOTIDY1a — show automatic cleanup** (modules/web). Branches lists recent tidied/kept log facts. D88; route and bilingual list tests.
- [ ] **AUTOTIDY1b — measure strict cleanup guards** (parent; installed AUTOTIDY1, after one week). Count ignored-file/unmoved-branch holds before deciding relaxations. D88 evidence.
- [ ] **AUTOTIDY1c — automatic squash cleanup** (driver; after AUTOTIDY1b; decide first). Consider content proof plus merged PR state, preserving recovery refs. D88/D102; Process guards.
- [ ] **QUESTBACK1 — hand back without a session** (service/driver/web). Both doors preserve note and taker facts. D32/D46/D132; transition/route/press tests.
- [ ] **LANDNAME1 — entered landing branch** (driver/web). Accept and terminal take an entered name. D145/D149/D126; landing and field tests.
- [ ] **HIST1f — Ask Daoris clear proposal** (driver/service/web; after HIST1e/SESSUX1h). Listed plan is confirmed on Apply. D153 §6.4; kinds, coverage, room/card tests.
- [ ] **HIST1h — installed clear** (parent; republish a–f first). Verify counts/bytes, terminal/page agreement at 1280/888/680 px, themes/languages. D153 §12.

## Consistent screens

Contracts: plugin-screen design (D119), frame model (D118). Order: PLUGUI1c → f → g;
FRAME1i precedes PLUGUI1h. Installed looks cover both themes/languages.

- [ ] **NAME2b — verify four labels**. Inspect help.setup, scope.every, signin.titleNew and work.group.noCheckout at 888 px; rename or accept. NAME2 handback.
- [ ] **PLUGUI1c2 — plugin-page remainder** (web). Move kit/update types, fix agent/landing doors, put Update now first, remove doubled heading. D119; stories/tests/look.
- [ ] **PLUGUI1f — complete plugin page** (after c/e). Health, Points, Agents, Servers, Activity, Data, Source and folder install. D119.
- [ ] **PLUGUI1g — plugin checks** (after f). Keep trial and run tests in a home copy. D119.
- [ ] **PLUGUI1h — Ask Daoris opens Plugins** (after c/FRAME1i). D119; matching coverage and screen proof.
- [ ] **FRAME1i — item-aware Ask Daoris**. Update where.ts, go and room for lists/items. D118.
- [ ] **FRAME2b — maximized restore under pointer** (owner sends request). Wait for runtime position support, then update handler. D56/D32; owner response and handler proof.

## Workflows

Contract: `docs/2026-10-09-workflow-design.md` (D157), §14 order/proofs.
Foundation precedes editor/proposals/intake; waits/checks/stages follow the gate; installation last.

- [ ] **WORKFLOW1c5 — shared run sentences** (driver/web; after c2). Hold terminal/catalogue words by a fixture or one English source. D157 note/twins; drift test.
- [ ] **WORKFLOW1e2 — quest-specific choice** (service/driver; after e). Field, census, person's door and selection for quests without asks. §4.1/D156.
- [ ] **WORKFLOW1e3 — clear run bindings with history** (driver; after e). Include binding in clear plan/count. D153/D157; clear test.
- [ ] **WORKFLOW1g — workflow editor** (web/modules; after d/e). Presets, save, insert/failure/move/remove, drawer, selection, terminal twins and keys. §6; 680 px/bilingual proof.
- [ ] **WORKFLOW1h — workflow proposal** (service/driver/web; after d/e). Judge validation/version/diff/person/outward needs; Apply/editor/Not now. §8; kinds/coverage/goldens/card tests.
- [ ] **WORKFLOW1i — intake kind proposal** (driver/service; after e). Set only from quoted requirements without lowering involvement. §4.3; prompt/table/refusal proof.
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
- [ ] **XAGENT1g3 — attention reads kept facts** (driver/modules). Cache look judgments; OPINION_WAITS runs no git. §9/UX6c; query tests.
- [ ] **XAGENT1g4 — coded answers and reviewer row** (driver/web). Reviewer beside work with measured usage; bilingual note parts. §6.4/§9.
- [ ] **XAGENT1h — intake reviewer choice** (driver/service; after c). Quoted choice or proposal only. §2.4; golden/refusal/stub proof.
- [ ] **XAGENT1i — second-opinion rehearsal** (tools/examples; after f/plugin maker). Findings reach work; disputes/missing reviewer hold. §6/§8.
- [ ] **XAGENT1j — measured reviewer posture** (driver; owner authorizes account spending). Try writing through each door; declare only observed refusal. §5.2/D53.
- [ ] **XAGENT1k — canon second reader** (canon/examples; parent's decision). §10 doctrine, resync and canon changelog. Verify/example rehearsal.

## Review environments

Contract: `docs/2026-10-08-review-environment-design.md` (D154), section proofs.

- [ ] **CANNOTSHOW1 — factual cannot-show state** (modules/web). Carry environment/address/reason instead of mixed-language sentence. D154 c2 note; bilingual tests.
- [ ] **REVIEWENV1d2 — live review_serve** (service/driver). Loopback serving wire reaches shell before session ends; tool waits for served answer. §2.3/§2.6.
- [ ] **REVIEWENV1e — separate review process** (driver/modules; after d). Procedure quote, Keep it, port/readiness/output and owned-tree stop. §2.3; stub/port/stop tests.
- [ ] **REVIEWENV1f — intake review choice** (driver/service; after b). Level-driven step; quoted choice or proposal; proposed off creates none. §1.5–§1.6/§2.1.
- [ ] **REVIEWENV1i — canon see-it-run** (canon/examples; parent's decision). §7 doctrine, resync and changelog. Verify/example rehearsal.
- [ ] **REVIEWENV1j2 — visible terminal twins** (web/driver). Draw existing choice twins in AskComposer/AskReview/ReviewGate; fix usage fallback. D50/D154; bilingual tests.
- [ ] **REVIEWENV1g2 — Stop showing** (modules/web; after d2). Route ends tab serving; press beside Show again. D154 point 8; route/gate tests.

## Landing, trace and Git

Contracts: D143/D145/D148/D149; plugin-hooks design;
`docs/2026-10-04-built-in-git-design.md` (D147 amended by D150).

- [ ] **LAND2e — post-landing advances** (driver/modules/web). Accept later commits; gone-tree chain parts; review/proposal explanations. D149/D113; both-door tests.
- [ ] **PLUGHOOK1b — GitHub work/state** (examples). Query head/PR status for squash cleanup. Hooks §2.7; fake-gh tests.
- [ ] **PLUGHOOK1d — refresh PR state** (modules/web; after c). Ask again, branch/sync codes, sweep/query words and failure cost. Hooks §2.4–§2.5.
- [ ] **LAND2d — automatic acceptance UI/rehearsal** (web/tools). Show acceptedBy/events; stub plugin and bare-origin proof. D145 note.
- [ ] **TRACE1c — missing trace links** (driver/service/web). Late rules, local operations, merges, carry-ons, advances, detached section and commit kinds. D143 note.
- [ ] **GIT1d — bridge Git routes** (modules/web; after b). Branch/log/commit/history/blame/compare, shell-only with coded refusals. Git design §4/§6.
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

- [ ] **QUESTREBASE1 — pure rebase planner** (service; consider). Preserve operation identity, accepted-before-pending order and transaction. D69/D79; unchanged sync suite and loss tests.
- [ ] **SESSUX1h — session proposals** (driver/service/web; after d–g/FRAME1i). Management §7.3/§9.
- [ ] **MSG1d2 — queued chat survives restart** (driver/web; with MSG1c). Record ids/reach/withdrawals; take by id. Messages §3.1.
- [ ] **MSG1g3 — cooling note and twins** (driver/web). Code held line; compare newSessionSaid with GoOnNew/WordsNever. D137/D142.
- [ ] **MSG1h — Codex next-step words** (driver; after measured STEER3). Messages §8.
- [ ] **MSG1i — native next-step words** (driver; after measured STEER3). Messages §8.
- [ ] **MSG1j — installed message canary** (parent). Messages §8–§9; a–f installed per original record.
- [ ] **STEER2 — Send now matches door** (all doors; after STEER1). Steering draft and queue-arrival explanation. D136 §4; measured proof.
- [ ] **STEER3 — measure other steering** (driver). Native stream-json and Codex turn/steer before implementation. D136 §5; one-turn evidence.
- [ ] **ANSWER1d — installed same-record resume** (parent). One row and session.answered resumed=true. D131 §6; real run.
- [ ] **DRIFT1e — follow-ups preserve requirements** (design first). Reopen parent corrections; closing notes are not requirements. D133 §5.
- [ ] **PAUSE1f — pause proposal** (driver/service/web; after b/SESSUX1h). Abandon remains person's act. D132 §7.4.
- [ ] **PAUSE1g — installed pause/abandon** (parent; after a–f). Pause/resume same tree; abandon preserves landed work. D132 §12.
- [ ] **SESSUX1k — limit-ended list state** (driver/web; after UX6e). Waiting carry-ons and account door. D126 §2.2/§9.
- [ ] **SESSUX1l — installed management** (parent; republish a–i). States, carry-on, stop/try-again/archive across widths/themes/languages. D126 §9.

## Economy, setup and recall

Contracts: session-economy (D127), workspace-setup (D124), setup-pilot lessons (D128),
knowledge review (D129). WSSETUP14b–e precede owner f/13; WSSETUP7 needs LAYOUT8.

- [ ] **COST1 — measure long-turn cost** (owner's decision). Week of context/cache evidence before ceiling/window/compaction proposals. METER1/D127.
- [ ] **SUBLOAD1b — probe scout and worker** (parent; after a harness restart). Agent definitions load at session start, so the first dispatch of each proves its tools, hand-back and startup context against 50K. D160 limits; `tools/subagent-usage.mjs`.
- [ ] **SUBLOAD1c — the model trial** (parent; after SUBLOAD1b). A batch built by scout and worker, Sonnet workers on narrow rows. Proof: first-run gates, review findings and columns against evidence §2. D160; `docs/2026-10-10-subagent-load-evidence.md` §4.
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
- [ ] **ORIENT2h7 — stable count-independent ids** (service/CLI). Ignore heading/label trailing counts; correct fence fixture note. D151 notes; two-commit measurement.
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
- [ ] **TRUST2 — measured trust** (owner grant). Written-key honoring and parent/child trust. D73/evidence; exact-folder hold remains.
- [ ] **AGENTS2a — DeepSeek key probe** (driver/CLI). No-key/invalid-key dsh measurement before twin declaration. D57/D67; ACP3 evidence.
- [ ] **PROC1 — Process suite below ten minutes** (tests/tools). Measure/isolate/reuse fixtures; three serial-equivalent green timings. MOD8/FLAKE1; preserve measurements.
- [ ] **TESTGIT1b — shared GitFixture** (driver tests). Landing plugin/tidy/fixture/landing/auto classes; eliminate independent git runners. Serial Process proof.
- [ ] **TRYTOOLS1 — trial tools** (driver). Folder trial uses configured machine tools. D101/D121 §3; named-Node Process case.

## Remaining regressions, evidence and held work

Contracts and complete reproductions: original backlog, FIX-LOG entries, named decisions.
Decision/trigger rows stay held; diagnostic captures are not permission to change timeout policy.

- [ ] **REALCASE1 — complete installed real case** (parent/owner). Remaining Codex device sign-in belongs to owner; preserve prior seven-step outcomes in frozen row. D62.
- [ ] **ANSWER2b — resumed run through second look** (driver Process). One resume/record while next look runs. D131 note.
- [ ] **BGWAIT1b — resumed background wait** (driver). Apply start judgment to Driver.Continue. D83; AcpBackgroundTests case.
- [ ] **BGWAIT1c — native background cutoff** (driver/web). Coded cutoff instead of park after harness-killed work. D83/D137/D142; stream fixture.
- [ ] **LAND4b — failed automatic landing** (driver; decide). Unify Done+failed rule with person's press. D145/D102; rule case.
- [ ] **QUESTCLOSE1b — bilingual person's done** (service/web/driver). Coded note and room door/exemption. D126/D142/D110; Chinese test.
- [ ] **FREEZE1b — capture next stalled bar** (modules/driver; trigger: ui.stalled). Move measured cause off UI thread; request runtime-owned changes. D56/log §4.
- [ ] **ASKNAME1c — named-proposal twin** (service/driver/web). One case-sensitive fixture across tier/intake/page; register twin. D77; three-suite proof.
- [ ] **GOAHEAD2c — rehearsal go-aheads** (tools). Stub asks twice; first answer stays parked, second resumes once naming both. D135/phase 17a.
- [ ] **SQUASHTIDY1d — recovery refs** (driver/web). List/clear at both doors by person's word. D102/D50; Process/bilingual group tests.
- [ ] **SQUASHTIDY1g — ancestry on another branch** (driver; decide). Carried versus landed-elsewhere; offer/refuse consistently. D88/D102; content-offer/landing Process rows.
- [ ] **SQUASHTIDY1e — landed content recovery** (driver). Preserve aggregate commit messages in recovery ref. D102; CleanLandedAsync test.
- [ ] **SWEEPCARRIED1b — carried branch wording** (web/driver). Exhaustive session head, terminal reasons and registered twin. D148/D102; bilingual/golden tests.
- [ ] **MCPSDK2 — protocol revision** (service; decide before SDK upgrade). Choose negotiation semantics before taking 2.x. MCPDISCOVER1; end-to-end probe.
- [ ] **SWAP2b — swap evidence** (tools). Marker distinguishes same-commit builds; retain longest holds from five loaded runs. D139/D60; failing marker proof.
- [ ] **SWAP2d — rollback by phase** (web/driver). Busy/move/start sentences and shared code table. D139/D41; bilingual twin cases.
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
