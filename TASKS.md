# Daoris (道衍) — active task backlog

Open work only. Finished rows move to `docs/task-archive.md`; `CHANGELOG.md` describes release changes. This backlog was condensed on 2026-10-06; the exact supporting detail and owner quotations remain in Git at `a3b20973:TASKS.md`, as recorded in `docs/2026-10-05-integration-review.md`.

## State

Development remains at `0.0.x`; nothing is published. Verified 2026-10-07: 1,371 CLI, 1,543 service, 87 HTTP host, 5,379 driver (4,662 fast, 717 Process), 758 modules (640 fast, 118 Process), 85 devkit, 4,505 web unit and 24 Playwright; rehearsals: release 114, family 400, deployment 110. The full set (14 gates) passed `d92a49c1` and that tree is on the install; earlier receipts and coverage are in `docs/2026-10-05-integration-review.md`. Canon: 8 core rules, 6 knowledge documents, 6 skills, 7 packs; always-loaded core 20,887 bytes, advisory budget 26,000.

Live consumer count is zero. Adoption is the owner's call. The existing desktop-runtime rehearsal remains evidence: 6 collisions, 2 twins to retire, budget 40,000, check at 38,782 bytes; supporting mechanics are in `docs/adoption/shenora-repo-mechanics.md`.

A section's contract supplies detailed acceptance criteria. Owner-marked rows retain their grants, downloads, runs or decisions; a row lacking proof in the source needs proof agreed before implementation.

## Start here

Prioritize UI/UX: retain refused account edits, quote terminal hints, finish the compact menu, verify recovered accounts and heads on the installed application, then complete the remaining screen consolidation. Review evidence and coverage: `docs/2026-10-05-integration-review.md`.

Read `AGENTS.md`, `CLAUDE.md`, `.claude/INDEX.md`, `docs/README.md`, then the row's contract. Use `doc-loader` and `dispatch-subagent`; integrate through `tools/merge-branch.mjs`. Rehearsals run serially without competing builds. Owner-marked rows retain their grants, downloads, runs or decisions; held rows wait for their triggers.

## UI/UX first

Contracts: `docs/2026-10-05-ux7-design.md` (D152) and `docs/2026-10-04-ux6-redesign.md` (D150, §12 has full rows and proofs). UI changes require stories and installed-window evidence in both themes and languages. Remaining UX6 order: baseline; attention; Git after GIT1d; Knowledge and Settings. Plugin tools follow PLUGUI1c.

- [ ] **UX7b — verify recovered account UI on the install** (web-shell). Account rows, add/name/join flow and shared naming are integrated; unit tests and English stories at 1546/680 px pass. Remaining proof: installed look in both themes/languages, including the add flow. Contract: D150 §5, D152 §4; review evidence: `docs/2026-10-05-integration-review.md`.
- [ ] **ACCTEDIT1b — verify refused account edits on the install** (parent; after republish). Rename…, Use in a workspace… and the add flow's last step, each refused: the draft kept and the sentence inside it, at 1546/680 px, both themes and languages; the refusal line's warn border in dark is the likeliest flaw. Contract: D152 §4. Proof: dated shots.
- [ ] **UXFIX1c — the tick and target leftovers** (web-shell). After UXFIX1 and UXFIX1b no product code passes `tick` to a plain `Menu.Item`, so `MenuRow`'s doc example ("the size a map is at") describes nothing; reword or drop the plain tick. The views menu's ⋯ trigger is `h-6 w-6` (24 px), under the 28 px floor. Contract: platform-ux §6, the second-opinion review. Proof: vitest on the trigger's minimum size; `ui.test.tsx`'s tick case updated.
- [ ] **UXFIX2b — the remaining asks on the one inline confirmation** (web-shell, web-settings). After UXFIX2, `PauseAsk` and `AbandonAsk` (`work/WorkAsks.tsx`), the quest's *Decline…* and the ask's *Close ask…*, `ArchiveEndedAsk`, `TrustAsk`, a plugin's *Remove…*, Tools' *Delete*, `AccountUse`'s confirm and the review's *Discard* (`DiffPane`) still close on the press or toast their refusal. Move each onto `work/InlineConfirm.tsx` (its `children` and `ready` exist for a reason field). Contract: platform-ux §4 as UXFIX2 amended it, the second-opinion review. Proof: a vitest per ask for focus on open, describedby, pending kept open, refusal inside, closing and focus returned.
- [ ] **UXFIX4b — punctuation stays with the code span it follows** (web-shell; after UXFIX4). A one-word code span that fills its line (a long branch name in a narrow sentence) pushes the punctuation after it onto a line of its own, because `CodeText` is inline-block: at the Sweep floor `…-quarter-closes` is followed by a line starting `, whose work…`. Keep trailing punctuation with the span. Also `work/SessionHead.tsx:322` still truncates a branch name in a session's header. Contract: platform-ux §3 (*a command breaks between its words*), §4's UXFIX4 bullet. Proof: the Sweep floor stories; a vitest on the span's following text.
- [ ] **BRSCOPE1a — the terminal looks at one workspace's branches** (driver; after BRSCOPE1). The screen's look and clean-up are now one workspace's, while `daoris-driver trees clean|sync` takes one repository or all of them, so D50's two doors differ. Add `--workspace <name>` to both, scoped as `DriverModule.Checkouts` scopes the routes. Contract: D150's BRSCOPE1 note, D50. Proof: a `TreesConsole` parsing test; a family-rehearsal check with two workspaces.
- [ ] **UXFIX5c — every status hue has an ink for words** (web-shell, web-settings). After UXFIX5 only danger has a text ink: open and done drawn as words read 3.0–3.8:1 in light (pills, ChainLine, +counts) and 4.1–4.4:1 in dark on soft fields over an overlay. Give each status hue an ink held to 4.5:1 as `--ink-danger` is, leaving the terminal's ANSI mapping alone. Contract: platform-ux §3. Proof: `tokens.test.ts` floors per hue; stories in both themes.
- [ ] **UX7d — remaining visual findings** (web-shell, driver). List absent agents with Install; two-letter strip marks; version without repeated product name; compact Chinese summaries; coded, properly formatted record-opening notes. Contract: D152, UX7 §1, §4.6. Proof: stories, note-code twins and installed look.
- [ ] **UX7e — verify menus and heads on the install** (parent; after republish carrying UX7a–c). Exercise Alt/F10, Alt+letter, table shortcuts, terminal Ctrl+N and composer editing; inspect 1546/680 px, themes/languages, 200 px head target, clipped quest title, Short title and landing branch. Contract: D152 §6 and UX7a/UX7c notes. Proof: dated shots and measurements under D152.
- [ ] **UX6g2 — guidance follows workspace pages** (driver, cli). Repoint help, registration, plugin, tree and setup wording and the driver's places table from old Workspace/Permissions settings. Contract: D150 §3.1. Proof: twin tables and room goldens; original row lists every producer.
- [ ] **UX6h — Git inside Repositories** (web-shell; after GIT1d; absorbs GIT1e). Branches tab gets kinds and a pure `graphLanes.ts` graph; remove the Git place. Contract/proof: UX6 §4.4 and D147.
- [ ] **UX6i2 — every door names the new places** (driver, service, cli, tools). After UX6i and UX6j, Ask Daoris's room and tables still say the bar holds Convergence and Search (`HelpRoomWindow.cs:20`, `HelpPlaces.Views` with its page twin `help/places.ts`), name *Settings → Setup* and *Settings → Plugins* (`HelpRoomDoors.cs`, its goldens, `HelpRoomLanding.cs:43`, `HelpPluginProposals.cs`, `HelpGoProposals.cs:100`, `PluginKitCommand.cs`, the plugin kit's README template, `Driver.Host/Program.cs:186`), as do the service's plugin and go proposals, `plugins.ts:1332`, and `desktop-publish.mjs`'s install README. Say Knowledge, Get started and the Plugins place. Contract: D150's UX6i and UX6j notes. Proof: each table's twin tests and the room goldens.
- [ ] **PLUGTOOL1c — tools live on their plugin** (web-shell, web-settings, modules, driver; after PLUGUI1c). Tools retains Daoris's own. Contract/proof: UX6 §7.5.
- [ ] **COWORK1 — agents working together** (design; held for owner's direction). Define progress sharing, mid-work questions and hand-offs beyond quests/intake and LAND2c's shared branch. Contract: D32, D65, D145, D149. Proof: design and decision.

## Reliability visible to the person

- [ ] **QUESTBACK1 — hand back a taken quest without a session** (service, driver, web-shell). Offer Hand back at both doors when no live session holds it, preserve its note and show who took it when. Contract: D32, D46 §3, D132. Proof: store transition, route and mocked-bridge press tests.
- [ ] **LANDNAME1 — person names the landing branch** (driver, web-shell). Review Accept and `trees land --branch` take an entered name; the existing default derives from the short title. Contract: D145, D149, D126 SESSUX1j note. Proof: `LandingTests` and mocked-bridge field tests.

## Clearing finished history

Today only an untaken open quest (D95) and a chat that served no quest (D126 §5.4) can be deleted; archive only hides. Nothing clears a done or declined quest, its ask, the sessions that served it (failed attempts included) or what the home kept of them. On 2026-10-07 the owner's install held 35 closed quests, 76 such sessions and 24 MB of transcripts; it was purged by hand, with a backup under `local/backups/`.

Contract: `docs/2026-10-07-history-clearing-design.md` (D153), §9 rows/proofs. Order: a → b (service); c (driver, modules) after b; d and e after c; f after e and SESSUX1h; g after d; h last. HIST1a alone closes H1–H2, which D95's and D126's deletes reach today, so it goes first.

- [ ] **HIST1f — Ask Daoris proposes a clear** (driver, service, web-shell; after e and SESSUX1h). A `clear` kind whose card is the first press and whose Apply sends what it listed; the room's doors and *The machine now*. Contract: §6.4. Proof: clear-proposal, kinds and coverage tests, the room's golden files, `ProposalCard.test.tsx`.
- [ ] **HIST1h — installed clear** (parent; after a republish carrying a–f). Clear a workspace's finished history on the install. Contract: §12. Proof: a dated ledger of counts and bytes before and after, `history` matching the page, both themes and languages at 1280, 888 and 680 px.
- [ ] **HIST1m — the service names open's and awaited's variants** (service, driver; after HIST1l). The driver still says *taken* and *chain* from a second read of the quest, the race HIST1l closed for `needs-you`; and HIST1l's five `waits` words are spelled by the service's `HistoryKept.Spell` and the driver's `HistoryWaits` with no shared table between them. Carry the two variants on the wire like `waits`, keeping the inference for older hosts, and hold every history word in one fixture both suites read. Contract: design §6.3, D153's HIST1l note. Proof: desk and route tests per variant; a driver test with records that disagree; the twin table.
- [ ] **SESSDEL1c — the kept names are one table** (driver, web-shell; after SESSDEL1b). SESSDEL1b's toast words each name `SESSION_DELETE`'s `stayed` carries through `work.delete.kept.<name>`, so the page keeps its own copy of the driver's `SessionHomeFiles` names, and no shared test holds the two lists together (`twins.md`). Put the names in one fixture both suites read. Contract: `.claude/knowledge/twins.md`, D126's SESSDEL1 note. Proof: the fixture read by a driver test and a vitest; a name added on one side alone fails.

## Consistent product screens

Contracts: `docs/2026-10-01-plugins-screen-design.md` (D119) and `docs/2026-10-01-frame-model-design.md` (D118). Order: PLUGUI1c → f → g; FRAME1i → PLUGUI1h. Look on the install in both themes/languages.

- [ ] **NAME2b — verify four labels**. At 888 px check `help.setup` at the 300 px Ask Daoris floor, `scope.every`, `signin.titleNew` on Agents and `work.group.noCheckout`; rename or accept each in both languages. Contract/proof: NAME2 hand-back and installed look.
- [ ] **PLUGUI1c2 — the plugin page's remainder** (web-shell, web-settings). After UX6j retired Settings → Plugins: move `PluginKit`/`PluginUpdate`/the offer type into `plugins/`, make Agents' chip and a landing rule's plugin doors to the plugin's page, put *Update now* first, and drop the kit drawer's doubled heading. Contract: plugin-screen design (D119). Proof: stories, vitest and bilingual look.
- [ ] **PLUGUI1f — complete plugin page** (after c/e). Health, Points, Agents, Servers, live Activity, Data folder, Source and Install from a folder. Contract/proof: plugin-screen design.
- [ ] **PLUGUI1g — plugin checks** (after f). Keep the last trial and run its own tests in a copy under the home. Contract/proof: plugin-screen design.
- [ ] **PLUGUI1h — Ask Daoris opens Plugins** (after c/FRAME1i). Contract/proof: plugin-screen design.
- [ ] **FRAME1i — Ask Daoris understands lists and items**. Update `where.ts`, item-aware `go` and the room. Contract/proof: frame-model design.
- [ ] **FRAME2b — restore maximized window under pointer** (owner sends request). The window kit lacks a position parameter; request support from its owner, then update the handler. Contract: D56 FRAME2 note, D32. Proof: kit's answer and handler using it.

## Faster development

- [ ] **MOD9d — the lane scan refuses a read no rule places** (cli, tools; after MOD9c). Four scanned reads that no REACH rule places reach every gate and so pass while holding nothing: three modules tests reading through a local rooted at `src/Daoris.Desktop` (`EveryModuleChildIsHandedTheToolsTests`, `RefusalCatalogueTests`, `EngineBrowserTests`) and the driver's `ServiceHostLocatorTests`; interpolated paths and expression-bodied helpers are still unseen. Fail the scan on an unplaced read until each resolves to a placed path. Contract: MOD9, parallel-development design §3, D115's MOD9c note. Proof: the scan fails on an unplaced read, then passes with each resolved.
- [ ] **AGENTS2a — measure DeepSeek API-key support** (driver, cli). Probe `dsh --profile acp` with invalid/no key before declaring `DEEPSEEK_API_KEY` in both twins. Contract: D57 AGENTS2, AGT3, D67 §1. Proof: evidence beside ACP3 and twin tables.
- [ ] **PROC1 — Process suite under ten minutes** (driver tests, tools). Measure classes, reuse/copy expensive git fixtures, isolate homes/ports for parallel workers and replace unnecessary processes with fakes. Contract: MOD8, FLAKE1. Proof: three serial-equivalent green runs with timings; retain original measured class durations in supporting evidence.
- [ ] **TESTGIT1b — finish shared GitFixture adoption** (driver tests). Move `LandingPluginTests`, `LandingTidyTests`, `LandedFixture`, `LandingTests`, `AutoLandingTests` off separate git runners. Proof: serial Process classes green; no `FileName = "git"` remains there.
- [ ] **TRYTOOLS1 — folder trials use machine tools** (driver). `TryFolderAsync`'s scratch home makes hook startup use PATH instead of the managed tool. Contract: D101, D121 §3. Proof: Process case with Node configured as a named file.

## Knowledge and orientation

Contracts: `docs/2026-10-04-orientation-everywhere-design.md` (D151) and D135. ORIENT2 has local tooling as its floor; owner runs remain owner runs.

- [ ] **ORIENT2c — repository index quest** (driver). Inventory, generator, two granted commands, check and declaration; change nothing else. Contract: §2, D151 §5. Proof: `SetupBriefTests` and family rehearsal.
- [ ] **ORIENT2d — prompts start at index ranges** (driver). Prefer declared `documents.index`; keep current fallback when absent. Contract: §3.3, D151 §6. Proof: failing-first `AskAndWaitPromptTests` goldens.
- [ ] **ORIENT2h5 — an index item's id does not move with its lines; the reading's cost measured** (service; after ORIENT2h2). ORIENT2h2 made every list item of a declared index an entry: an item whose label leads with line numbers (outline items like `695-731`, fixture items like `:284`) changes id whenever those lines move, and a file named for a question's word puts that word in all its items' titles, so its items can outrank a row; the declared index went from 573 entries to 6,629 (store 12 MB → 21 MB) and the semantic tier's cost for them is not measured. Key an item's anchor on its text past the numbers, and measure the semantic tier's refresh. Contract: D151's ORIENT2h2 note ("Not covered"). Proof: an item's id stable across an edit above it; the measured refresh.
- [ ] **ORIENT2h6 — the last fence toggles read as Markdown does** (cli, driver; after ORIENT2h4). After ORIENT2h3 and ORIENT2h4 the service and the tools read fences as CommonMark does, but the CLI's `src/Daoris.Cli/src/document.ts` and the driver's `SelfDescription.cs` still toggle on any three backticks or tildes, so a longer fence quoting a heading or a frontmatter line misreads there. Each implements the rule in its own language, held by the shared `decision-notes.json` fence rows or a fixture of their own. Contract: D151's ORIENT2h3/2h4 notes, `twins.md`. Proof: a four-backtick fence case seen failing in each.
- [ ] **ORIENT2f — clone works without Daoris** (examples, tools). Example owns its generator/check; move a line, fail, regenerate and pass without CLI/service. Contract: §4, D151 §2. Proof: family rehearsal phase.
- [ ] **ORIENT2i — the platform names an index entry** (web-shell). ORIENT2e gave the service's entries a kind `Index`, and the page's `kind` catalogue (`locales/*/kind.json`) has no row for it, so a knowledge search that lands on an index row shows the raw key. Add `kind.Index` in both languages and let a result name its line. Contract: D151's ORIENT2e note. Proof: a unit test rendering an index result; the catalogue parity test.
- [ ] **ORIENT2g — measure after adoption** (owner's install, read-only). Repeat ORIENT1e: halve pre-edit calls/characters/searches/dumps, quarter whole outlined-file reads, 80% index uptake, no extra parks. Contract: §5.3–§5.4, D151 §7. Proof: evidence note.
- [ ] **KNOWUSE2b — bench all 46 questions** (owner). Use repository knowledge and install harness/account chosen by owner. Contract: D135 §6/KNOWUSE2 command lines. Proof: report under D135; zero answers substituted for owner before KNOWUSE3.
- [ ] **KNOWUSE3 — review beside each item** (web, driver; held on KNOWUSE2b). Name hint tier and never answer for the owner. Contract: D135. Proof: stories and installed look.
- [ ] **KNOWUSE4 — correction request to repository owner** (owner may publish). Correct the comparison's misread calculation answer and reconcile the config-only rule with the requested shared-component change. Contract: knowledge-use evidence §4; no edit across repositories.

## Trace and Git

Contracts: `docs/2026-10-03-future-directions-review.md`, `docs/2026-10-04-built-in-git-design.md` (D147 amended by D150).

- [ ] **TRACE1c — missing trace links and doors** (driver, service, web-shell). Capture late rules, local quest operations, merge commits, carry-on links and LAND2c advances; finish detached-window section and page commit kind. Contract: D143 TRACE1b note. Proof: chain/route/service/vitest tests, stories and look.
- [ ] **GIT1d — bridge routes** (modules, web-shell; after b). Branches, log, commit, history, blame and compare stay shell-only; refusals have bilingual codes. Contract: §4, §6. Proof: `DriverModuleGitTests`, parity.
- [ ] **GIT1f — commit/history/blame/compare tabs** (web-shell; after UX6h). Reuse diff/patch views, trace chain, blame gutter and two compare sides. Contract: §2.4–§2.6. Proof: stories, vitest and look.
- [ ] **GIT1g — fetch/create/delete** (driver, modules, web-shell; after UX6h). Both doors share plan/apply judge at the judged commit, console and machine log. Contract: §3.1, §3.3–§3.4. Proof: parent-run bare-origin `GitActsTests`, vitest.
- [ ] **GIT1h — push and pull-request press** (same lanes; after g). Push explicit commit/ref only on press; refuse line/session/held branches and non-fast-forwards; keep landing entry and D102 hand-off. Contract: §3.1–§3.2, D147 amendments. Proof: bare-origin/pre-push fixtures, vitest, look.
- [ ] **GIT1i — Git from sessions** (web-shell; after UX6h). Review identifies comparison/landing/push/PR; Show in Git and preview History and blame. Contract: §2.7, §6. Proof: vitest, stories, look.
- [ ] **GIT1j — offer managed Git** (web-settings, web-shell, driver; after TOOLS6/10). Setup Git step, repository tool identity and explicit switch press. Contract: §5. Proof: setup facts, vitest, `HelpCoverageTests`.
- [ ] **GIT1k — Ask Daoris git proposals** (service, driver, web-shell; after h). Person confirms fetch/branch/push/delete. Contract: §3.3, D110. Proof: both kinds tables and no owed coverage row.

## Landing and pull requests

Contracts: D145, D148, D149; `docs/2026-10-04-plugin-hooks-design.md` for plugin-hook sections.

- [ ] **LAND2e — accept post-landing advances** (driver, modules, web-shell). Manual acceptance must allow new commits after landing; gone trees show only each chain session's part; review/Ask Daoris explain advances. Contract: D149 point 2, D113 §3. Proof: review/terminal advances, vitest, proposal tables.
- [ ] **PLUGHOOK1b — GitHub work/state** (examples; after a). Query `gh pr view`/`list --head` so squash-merged branches can clear. Contract: hooks §2.7, D148 point 7. Proof: fake-gh `landing-plugins.test.ts`.
- [ ] **PLUGHOOK1d — page reads/refreshes PR state** (modules, web-shell, web-settings; after c). Review Ask again, bilingual branch/sync codes, sweep kinds, query wording and failing-query cost. Contract: hooks §2.4–§2.5. Proof: route/vitest/stories/parity/names checks and look.
- [ ] **LAND2d — show automatic acceptance and rehearse it** (web-shell, tools). Render `acceptedBy` and event parts; drive auto-accept with stub plugin/bare origin, leaving uncommitted work for review. Contract: D145 LAND2b note. Proof: vitest/stories and family phase covering branch, trace and review state.

## Evidence and captured proof

Contracts: `docs/2026-10-03-evidence-design.md` (EVID1, D144) and `docs/2026-10-04-landing-and-proof-design.md` (EVID2, D146). Order EVID1a → b → c; each EVID2 follows corresponding EVID1; EVID1d also needs DEV5/7.

- [ ] **EVID1c — quest evidence and hold reason** (web-shell, driver; after b/DRIFT1d2). Show result/commit versus session assertion, Check again and Ask Daoris check card. Contract: §5–§6. Proof: stories/vitest/catalogues/coverage and bilingual look.
- [ ] **EVID1d — queue gate evidence** (driver, service; after DEV5/7/b). Add required gates to queue; read landing verdict and landed path evidence; report `no-queue` elsewhere. Contract: §4. Proof: stand-in gate tests and queue rehearsal.
- [ ] **EVID2a — captured proof requirement** (service; after EVID1a). Screenshot/answer kinds, done proof and capture verdicts; no bytes/address/path travel across machines. Contract: §9–§10, §12. Proof: evidence/sync/MCP/local/shared tests.
- [ ] **EVID2b — keep captured proof** (driver, examples; after EVID1b/EVID2a). Hand `${proof}` to browser plugins, check/redact/copy named captures and manifest, post verdict; prompts/intake request captures. Contract: §9–§11. Proof: proof/requirements/goldens/browser tests and family rehearsal.
- [ ] **EVID2c — proof pages and terminal** (web-shell, modules, driver; after EVID1c/EVID2b). Session/review/quest Proof sections, Capture through reopen, Remove, terminal save/remove. Contract: §11–§12. Proof: stories/vitest/catalogues/coverage/command tests and bilingual look.

## Session management

Contracts: `docs/2026-10-02-session-management-design.md` (D126, §9 rows/proofs), `docs/2026-10-03-session-messages-design.md` (D137), answer-continues (D131), pause-and-clean-up (D132), D136. Installed canaries follow their completed build rows.

- [ ] **CARRY2c — a quest taken elsewhere reads in Chinese** (driver, web-shell). `StartVerdict.TakenElsewhere` carries the ledger's English sentence, which the page shows as the sitting reason in both languages. Carry the machine and session as facts on the consideration and word it from both catalogues. Contract: D80's CARRY2b note, LANG1a. Proof: a planner facts test and a vitest rendering it in 中文.
- [ ] **WAITCLAIM3 — a remote refuses a move from a machine whose take lost** (service; after WAITCLAIM2). WAITCLAIM2 refuses such a done, decline or wait inside the store's write on the machine that makes it; a remote still judges a push by the transition table alone, so a machine on an older build can still push one. Refuse it at `ReceiveAsync` by the same `QuestLog.Claim`, answering the pusher as a lost claim. Contract: D69's WAITCLAIM2 note ("What the gates do not cover"). Proof: a two-machine sync test where an older machine's done after its lost take is refused at the remote.
- [ ] **QUESTREBASE1 — the rebase planned before it is applied** (service; consider). `RebaseAsync` (`Quests.cs:1395`) now replays, judges claims, classifies losses, deletes and rewrites operations, cleans follow-ups, amends conflict notes and replaces the cache in one method. A pure planner returning the final quest and the mutations, applied in the same transaction, would make WAITCLAIM1's and WAITCLAIM2's ordering testable without storage. Highest risk of the review's refactors: keep operation identity, accepted-before-pending order and the transaction. Contract: D69, D79, CLAUDE.md's plan/apply rule. Proof: the sync suite unchanged; planner tests for each loss.
- [ ] **SESSUX1h — Ask Daoris reaches sessions** (driver, service, web-shell; after d–g/FRAME1i). Contract/proof: session-management §7.3/§9.
- [ ] **MSG1d2 — queued chat words survive restart** (driver, web-shell; with MSG1c). Record ids/reach, take by id and record withdrawals. Contract: messages §3.1, D137 MSG1d. Proof: `ChatTurns`, conversation tests.
- [ ] **MSG1g3 — coded cooling notes and twin test** (driver, web-shell). Code the untranslated held line; test `newSessionSaid` against `GoOnNew.cs`/`WordsNever`. Contract: D137 MSG1g/g2, D142 point 1. Proof: note-code twins and parsed declarations.
- [ ] **MSG1h — Codex next-step messages** (driver; after STEER3 Codex turn). Contract/proof: messages §8.
- [ ] **MSG1i — native next-step messages** (driver; after STEER3 native turn). Contract/proof: messages §8.
- [ ] **MSG1j — installed canary** (parent). Contract/proof: messages §8–§9; a–f already installed per original record.
- [ ] **STEER2 — Send now matches the door** (driver, modules, web; after STEER1). Use `_session/steering` for the draft and explain queue arrival. Contract: D136 §4. Proof: driver/module/page tests and measured steer.
- [ ] **STEER3 — measure other steering doors** (driver; after STEER1). Probe native stream-json input and codex-acp turn/steer before implementation. Contract: D136 §5. Proof: one-turn measurements.
- [ ] **ANSWER1d — installed answer resumes one record** (parent; a–c installed). Answer a park; one row continues and `session.answered` has `resumed: true`. Contract: answer-continues §6. Proof: real run.
- [ ] **DRIFT1e — follow-ups retain the ask** (design first). Closing notes are not requirements; corrections reopen parent work instead of being implemented as Verify. Contract: D133 §5. Proof: design and resulting rows.
- [ ] **PAUSE1f — Ask Daoris pause proposal** (driver, service, web-shell; after b/SESSUX1h). Abandon remains person's act. Contract: D132 §7.4. Proof: proposals/coverage and room goldens.
- [ ] **PAUSE1g — installed pause/abandon** (parent; after a–f). Pause mid-session, resume in tree, abandon while keeping landed work. Contract: D132 §12. Proof: width/theme/language ledger.
- [ ] **SESSUX1k — limit-ended sessions in list** (driver, web-shell; after UX6e). Show waiting carry-ons and account menu link; held-word Resumes later already exists. Contract/proof: session-management §2.2/§9.
- [ ] **SESSUX1l — installed session management** (parent; after republish a–i). States, carried-on parked quest, stopped hold/Try again, Archive ended. Contract/proof: session-management §9; all widths/themes/languages, SESS1-style ledger.

## Session economy and documentation

Contract: `docs/2026-10-02-session-economy-design.md` (D127).

- [ ] **COST1 — measure long-turn cost** (owner's call). Measure context high-water/cache reads per turn for a week, then consider per-agent ceiling/window/compaction on both doors. Contract: METER1, D127. Retain original thousand-call/context-growth evidence.
- [ ] **SESSOPT1d — trim backlog by doctrine** (steward; after b/c). Move excess to §4.3 homes; FLAKE1/TEST1/REH1 get open fix-log entries. Proof: `doc-budgets` ≤5,300 words and every row ≤60. This draft's 6,600 ceiling alone does not complete it.
- [ ] **DOC7 — measure reading** (D127 §6.1). Log `session.read`/`session.skill`; report reads by role and whether whole. Contract/proof: session-economy design.

## Workspace setup and knowledge recall

Contracts: `docs/2026-10-01-workspace-setup-design.md` §8 (D124), `docs/2026-10-02-setup-pilot-lessons-design.md` (WSSETUP14, D128), `docs/2026-10-02-knowledge-design-review.md` (KNOW2, D129). WSSETUP14b–e precede owner runs f/13; WSSETUP7 follows LAYOUT8.

- [ ] **WSSETUP7 — workspace setup screen/Ask Daoris** (modules, web-shell, service, driver; after LAYOUT8). Workspace page, both languages. Contract/proof: D124 design §4.4–§4.5.
- [ ] **WSSETUP14b — knowledge stays in place** (after a). Declare `documents.knowledge`; index/service read it and sync never writes it. Contract: pilot §1.2–§1.3. Proof: twin tables.
- [ ] **WSSETUP14d — setup keeps checks green** (after b; absorbs SETUP2). Check before/after; preserve knowledge, repoint moved paths/readers, never finish red; list hand indexes without deletion and use subject names. Contract: pilot §1.1, §1.4–§1.6, §3.3; D129 §4.5. Proof: brief/playbook twins and moved-path family phase.
- [ ] **WSSETUP14e — finish setup branch** (after d). Exact merge rule in follow-up quest. Contract: pilot §4.2. Proof: composer/press tests and family setup phase.
- [ ] **WSSETUP14f — finish pilot** (owner; after republish). Both repositories: checks green, knowledge declared, root under 32,768 bytes, default budget restored. Contract: pilot §4. Proof: real pilot.
- [ ] **KNOW3a — 169-document opaque-name bench**. Measure index whole-reading and skill truncation at scale. Contract: bench results §5/§6.1. Proof: `knowledge-bench.mjs` rerun, tests in verify.
- [ ] **KNOW2a — probe service recall** (meaning half owner, D24). Paraphrases through index/search; recall at 3/5. Contract: review §2.G/§4.6. Proof: evidence and script tests.
- [ ] **KNOW2b — prompt headlines** (after KNOW2a/DOC7). At most five, reserve each tier, omit if service silent. Contract: review §4.6 items 1–3/5. Proof: driver tests and before/after knowledge reads.
- [ ] **KNOW2c — chat prompt hook** (after KNOW2b/probe). Failing-open `UserPromptSubmit` headlines in composed settings. Contract: review §4.6 item 4. Proof: both-door probe and settings tests.
- [ ] **WSSETUP13 — remaining repositories** (owner; absorbs LAYOUT10 remainder). Resume paced plan using pilot numbers, include existing instruction files and compare weekly parks. Contract/proof: workspace-setup design.

## Plugin distribution

Contract: `docs/2026-10-01-plugin-distribution-design.md` (D120); §7 carries full rows/proofs. Work belonging to the plugin repository is a request to its owner, never an edit from here.

- [ ] **PLUGREPO2e — remove migrated examples** (after PLUGDIST1g). Retire three plugin examples and `landing-plugins.test.ts`; lay out package offers. Contract/proof: §7.
- [ ] **WORKSHOP1a — workshop setting**. Home default and both doors. Contract/proof: §2.1/§7.
- [ ] **WORKSHOP1b — workshop sessions**. Contract/proof: §2.2/§7.
- [ ] **WORKSHOP1c — workshop view/Ask Daoris**. Plugin creation uses workshop. Contract/proof: §2.3/§2.5/§7.
- [ ] **WORKSHOP1d — hand-over and named source**. Contract/proof: §2.4/§7.
- [ ] **PLUGDIST1b — pack/release workflow** (request to plugin repository). Contract/proof: §7.
- [ ] **PLUGDIST1c — HTTP package source**. Bound extraction before accepting network packages. Contract/proof: §5.3–§5.8/§7.
- [ ] **PLUGDIST1d — host answers**. Package records say `package`, not `folder`. Contract/proof: §7.
- [ ] **PLUGDIST1e — Available catalogue** (after PLUGUI1f). Contract/proof: §6/§7 as D140 amends.
- [ ] **PLUGDIST1a leftovers — finish routing and package safety**. Move install from host Program into `PluginsCommand`; fix package kind through d and extraction bound through c. Contract: 2026-10-01 hand-back.
- [ ] **PLUGDIST1f — first publish** (owner). Account, trusted publishing and prefix. Contract/proof: §7.
- [ ] **PLUGDIST1g — offers from packages** (after f). Contract/proof: §7.
- [ ] **PLUGDIST1h — verify repository signature** (held). Contract/proof: §7; retain its trigger.

## Safe-work declarations

Contract: `docs/2026-10-01-development-documents-design.md` (D122), §6 rows/proofs. Order UNBLOCK2 after DEV5 → UNBLOCK3 → UNBLOCK6–8.

- [ ] **UNBLOCK4c — push canary** (owner grants runs). Both doors, local bare remote: `git -C . push`, `-c`, `--no-pager`, quoted subcommand and alias. Contract: UNBLOCK4 archive procedure. Proof: tip unchanged and each refusal recorded.
- [ ] **UNBLOCK2 — declaration/judge** (after DEV5). `safe` beside gates, read from line. Contract/proof: §3.1–§3.3/§6.
- [ ] **UNBLOCK3 — person's one acceptance** (after UNBLOCK2 and week of UNBLOCK5). Declare proposal, exact Claude Code rules on both doors. Contract/proof: §3.4–§3.5/§6.
- [ ] **DOC6 — examples keep document standard**. Contract/proof: §6; DOC7 belongs to D127.
- [ ] **UNBLOCK6 — declaration UI/Ask Daoris**. Both languages. Contract/proof: §6.
- [ ] **UNBLOCK7 — measure Codex/dsh first**. No grant handed before measurement. Contract/proof: §6.
- [ ] **UNBLOCK8 — first declaration** (owner). Measure asks for a week before/after. Contract/proof: §6.

## Managed tools

Contract: `docs/2026-10-01-tools-design.md` (D121), §7 rows/proofs. TOOLS6 and 8 may run together; then 9; 10 precedes any managed-Git agent session; 11 last.

- [ ] **TOOLS6 — Git carries intended configuration**. Allow-listed ssh command/global includes/version floors; tree-sync guidance names Settings → Tools. Contract/proof: §2.5/§7, TOOLS7 hand-back.
- [ ] **TOOLS8 — Ask Daoris tool kind**. Contract/proof: §4.3/§7.
- [ ] **TOOLS9 — managed-tool rehearsals**. Loopback list, stubs and tamper refusal. Contract/proof: §6/§7.
- [ ] **TOOLS10 — managed-Git probe** (owner allows one start per agent). Contract/proof: §7.
- [ ] **TOOLS11 — real downloads/install** (owner). Managed Git over SSH, plugin Node, terminal PowerShell and gh/az landing. Contract/proof: §7.

## Daoris develops Daoris

Contract: `docs/2026-10-01-self-development-design.md` (D115); sections carry proofs. Order DEV5 → 6 → 7; 8 and 9; then 10/11.

- [ ] **DEV5 — external landing queue**. Detached home tree, gate kinds/quiet rerun, locked fast-forward, terminal queue and commit check. Contract/proof: §4.2–§4.8.
- [ ] **DEV6 — concurrent lanes**. Locks, oldest-first reservation, default cap 3 on both doors, lanes in record/prompt. Contract/proof: §3.2–§3.4.
- [ ] **DEV7 — ready and verdict**. `session_ready`, answer door, three failures reach person; done means landed. Contract/proof: §4.1/§4.5–§4.6, D83.
- [ ] **DEV8 — queue screen and Ask Daoris**. Every new verb gets a door. Contract/proof: §4.9, D110.
- [ ] **DEV9 — steward**. Lane quests, decisions/records and rewritten dispatch; settle router ownership and new-path lane-map edits. Contract/proof: §5, DEV2.
- [ ] **DEV10 — first real user** (owner present). Single and cross-lane rows through steward/queue/records; revisit caps/strikes from evidence. Contract/proof: design.
- [ ] **DEV11 — second user and tool retirement**. Canon lane knowledge, example, then retire merge tool and change dev loop to queue. Contract/proof: design.

## Every agent reads the same repository

Contract: `docs/2026-10-01-agent-layout-design.md` (D117), §7 rows/proofs. Entry-point predictions remain unmeasured until LAYOUT2's canary.

- [ ] **LAYOUT2 — canary turns** (owner grant). One per harness confirms predicted cells and account flag using evidence §6 fixtures/prompt; keyless half archived. Contract: `docs/2026-10-01-entry-point-evidence.md`. Proof: turns.
- [ ] **LAYOUT5 — move doctrine** (alone; touches steward lane map). Move manifest/sync, union attributes and engine example together. Contract/proof: §4.1/§4.3–§4.4/§7, DEV2.
- [ ] **LAYOUT6 — move brief and create rooms**. About 1,300 words in AGENTS under 32 KiB; eight rooms; CLAUDE keeps import. Contract/proof: §4.2/§7.
- [ ] **LAYOUT8 — setup UI/Ask Daoris**. Repository Setup tab, Set up for agents, setup kind; bilingual installed look. Contract/proof: §6.1/§6.5/§7, UX6f.
- [ ] **LAYOUT9 — lanes name rooms** (after DEV6). Contract/proof: §2.5/§7.
- [ ] **BUDGET1 — decide what budget caps** (owner; REV3 CLI F10). Region-only inspect differs from README/design claim and analyze's pre-D59 quantity. Decide, then align all three. Contract: D59, `docs/2026-09-25-rev3-review.md`.
- [ ] **TRUST2 — measure trust behavior** (owner grant). Probe whether harness honours written key and parent trust covers child. Contract/proof: D73 and deployment trust evidence; exact-folder hold today.

## Accounts and browser

Contracts: toolchain design (D57, §3 resolution), account-rotation (D125), account-use (D130), in-app-browser (D78/D84).

- [ ] **TOOL6d — continue chat on another account** (driver, modules, web-shell; after b). Refused turn offers last plan/words to selected account. Contract: D130 §8–§9. Proof: driver/route/vitest and bilingual look.
- [ ] **TOOL4l — account proposal service doors** (service, driver). Support use/keep/early/near, order, ready and cooloff. Contract: D125 §6, D130 §9/§16.6. Proof: proposal kinds and no owed coverage rows.
- [ ] **TOOL4h — account rehearsal/report** (tools; after j). Two stub accounts, per-account/window limits labelled Daoris-only, parallel N-account run. Contract/proof: rotation §8/§2.2, D130 §5.4.
- [ ] **TOOL4i — installed rotation** (owner). Parallel every-account run and what one limit interrupts remain; sequential run recorded already. Contract: D130 §11. Proof: run; preserve earlier evidence in FIX-LOG.
- [ ] **AGT3d — a limit's note says what kind of limit it was** (driver, web-shell). Since AGT3c the travelling note carries the cooling line's `until` and `why` and never the agent's sentence, but neither the kind of limit (`window`: five-hour or weekly) nor whose account (`owner`), so a reader on another machine learns neither. The web adds `{{window}}` (worded by `harness.window.*`) and `{{owner}}` to `note.account.cooling` in both catalogues and `NOTE_VALUES`; the driver declares them on `NoteCodes.AccountCooling` and passes `CoolingEntry.Window`. Contract: D125's AGT3c note. Proof: `NoteCodesTests` and `note.test.ts` hold the entries; a limit-note test reads the window from the parts.
- [ ] **AGT2c — real vendor pins** (owner; two downloads). Verify Claude Code honours `DISABLE_UPDATES` and pinned Codex does not update outside native layout. Contract: D67/channel evidence. Proof: observed pins.
- [ ] **BRW14 — downloads become quest attachments** (design first). Intake currently cannot save a mock-up attachment; define session download destination and transfer to quest. Contract: D78, CHR3 §3.2. Proof: design before build.

## Flakes and held work

- [ ] **FLAKE1 — bounded diagnostic waits** (driver/modules tests). Instrument repeating plugin-kit, input and ProcessJob failures instead of rerunning blindly. Contract: MOD8, PROC1; sightings in FIX-LOG FLAKE1. Proof: named slow step and three loaded serial green runs.
- [ ] **DEV3b — the losing session's stop is said by the run that made it** (driver, tools). The family rehearsal's *its driver stops its own losing session* failed again after DEV3a (FIX-LOG's FLAKE1 sighting at ORIENT2h2's merge): the record stood down with the right note, but the run's print held no `stop  session` line, its one pass having failed to reach the remote, so the stop came from a pass the print never showed. Find which pass stopped it (the record's `updated` against the run's passes) and make the run that stops a session say so; have the rehearsal keep a failed run's log and transcript. Contract: D115's DEV3a note, FIX-LOG. Proof: a stand-in test where the first pass fails to reach the remote and a later one stops the session, the stop said; the rehearsal keeping its failed run's output.
- [ ] **TEST1 — capture Windows Node abort** (web e2e, tools). Preserve JSON reporter/rehearsal exit for `0xC0000409`; no timeout tuning from three sightings. Contract: FIX-LOG TEST1. Proof: next failure captured.
- [ ] **REH1 — canon-upgrade rehearsal failure** (held). Keep transcripts; no tag until captured failure resolved or owner closes after clean post-canon runs. Contract/evidence: FIX-LOG REH1, `_fixtures/rehearsal-logs/`.
- [ ] **CANON9 — desktop-winforms pack** (held). Keep local until a second repository needs it; two-repository bar remains. Contract: original pack candidate.
- [ ] **TOOL5 — native adapters** (held until a repository names its tool). Contract: toolchain §5, D23/D24/D57 TOOL5 note; measure each claimed field.
- [ ] **SEM2 — persistent vectors** (held; after SEM1). Trigger: noticeable first-search embedding cost. Persist in `knowledge.db` through existing SQLite stack and migrations; re-embed on model changes. Contract: original SEM2 row; no embedding need on lexical-only install.
- [ ] **PLUG7 — service plugin points** (held until plugin asks). Contract: D64; reuse wire at service.
- [ ] **A file tree in the dock** (held until browsing requested). Contract: D76; existing preview/terminal are PREVIEW1/D111 and CONSOLE4/D96.
- [ ] **AFTER1 — step after several quests** (held until an ask needs it). Store dependencies; planner names unfinished ones. Contract: future-directions review §3, amending intake §1g. Proof: trigger, service/planner tests.
- [ ] **MSG1k — fork terminal conversation into Daoris** (held until real use). Contract: messages §4.3. Proof: protocol stub with session/list and session/fork.
- [ ] **PLUGHOOK2 — PR review threads back to work** (driver, examples, web-shell; held until first manually carried request). Explicit work/review press sends person's words or new ask. Contract: plugin-hooks §3.1.
