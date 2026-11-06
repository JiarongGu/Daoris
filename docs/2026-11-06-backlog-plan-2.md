# The backlog's rest, planned (2026-11-06)

The second plan for D240's direction: the whole of `TASKS.md` built at pace in big chunks, then the code and the documents reviewed
whole. The first plan ([`2026-11-05-backlog-plan.md`](2026-11-05-backlog-plan.md)) cut BL0 to BL30 and the sweeps; most of BL1 to
BL29, the git design's G1 to G9 ([`2026-11-05-git-design.md`](2026-11-05-git-design.md), D241, D242) and most of AGENTPROVIDER1 (the AP
chunks, D243) are built. This plan reads the 79 open rows again against main at `321928fc8`, and again at `992a2afbb` after AP-J and G8e
merged, says what is really left, and cuts the rest.
It follows the first plan's rules: a builder takes a whole chunk, writes its tests and runs none, compiles clean and hands back at about
150 tool calls; at most five run at once; Opus takes each wave's exemplar, the designs and the judgement, Sonnet the mirrors; every check
runs once, at the verification (§5.3).

**In flight, not planned again here:** RPC1g1 and BL26d (the web suite green on snapshot-only slices, ONEWAY1ab-ii). **Merged while
this was read:** G8e (`9fc8a65f7`) whole, and AP-J (`4ebf4bd90`) in part: m-ii, l and n moved, and d, e, f and h untouched, so their rests
are cut again here (BL38a to BL38c).

Paths below are short: the driver's under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, the modules' under
`src/Daoris.Desktop/Daoris.Desktop.Modules/`, the service's under `src/Daoris.Service/`, the web's under `src/Daoris.Web/src/`.

## 1. Every open row, classified

Classes: **a** buildable now, here, with no install and no spend; **b** needs the staged install, an installed window or an account's
spend (D240 point 7); **c** needs the owner's answer (§3); **d** done, closes or folds (§2); **e** needs a design first; **f** depends on
another repository. A row with a tail in another class says so; its count is its first class.

| Row | Class | What is really left | After |
|---|---|---|---|
| NOCOMPAT1b2 | a | `AutoLandingWorld`'s name; the exchange's `BindAsync`, once a bound run's close stops reading a bind recorded at the take; the workspace Setup tab, if its other rows have pages | none |
| SCOPEVERIFY1 | a | the scope's one verification, run once all of §5's waves have merged (§5.3) | every chunk |
| COMMENTGATE1 | a | the gate and its targeted pass (D235) | SCOPEVERIFY1 |
| MERGETOOL1 | a, c tail | designed (D237): MT1 to MT8, FR1 to FR3 built beside and switched after the verification; MT9 waits on Q3 | SCOPEVERIFY1 |
| KNOWCENTRAL2 | b | 2i, the installed measurement with repeated or conflicting entries | the install |
| HOMELAYOUT5 | b | each harness's home moved and measured with the owner's sign-in per harness; it decides ONEWAY1w | the install, Q6 |
| SESSWATCH1 | a | 1a is built (no wall clock; a quiet session brought to the person). Left: 1b's rest (the word on the native, codex text and dsh doors at the turn's end; `watch` kept out of `AnswersAlone.Resets`; the row's *told* line; `LongCalls`' look folded into the watcher's) and 1c's rest (the granted `settings_quiet` tool; Ask Daoris's answer to how long a session may run) | none |
| SESSWATCH1d | b | quiet episodes measured two weeks after 1a and 1b run on the install | the install |
| SESSWATCH1e | b | the automatic end, only in cells 1d allows; then an (a) chunk | 1d |
| SESSCTX1 | b, a tail | 1a in the spend sitting; then 1e, *Go on fresh* as a hand-over to the same agent, lifting `HandOver.SameAgent` | the spend sitting |
| RPC1g | a, b tail | g1 in flight; g2 to g4 buildable; g5 on the install | RPC1g1 |
| AGENTPROVIDER1 | a, b tail | after AP-J: h's rest (`SpawnRequest` composed in Loop, Platform's launcher) and n's rest (the asked choice from the whole request); d's rest (`RosterNext` taking the door's own home, s-ix o1 to o3), e's rest (door tables beside the provider's, the page's floor per agent) and f (the s-ix runs alone); l's rest (one stream-json reader, the lineage as Participants' part, `ChildBindings`' fold split) and m-ii's open choice (what `DoorInput` carries for a protocol prompt); r's rest (COAGENT1e); s-vii; u; then t (= ONEWAY1ah), whose installed proof spends | none |
| AGENTPROVIDER1j | f | each protocol door reads its own dialect, after the protocol doors move to the layer (AGENTLYNTAI1e); a measurement first (does Codex's or dsh's adapter read `jetbrains.air`) | 1e-iv |
| AGENTLYNTAI1 | f, b tail | the program; s-ii is a measurement that spends once | Lyntai, Q2 |
| AGENTLYNTAI1a | f, c | the reference to Lyntai's local library (D240 point 8), which contradicts the design's §7.3 rule that no merged branch builds on a pack | Q2 |
| AGENTLYNTAI1b-i | f | the adapter, the fourth view and the oracle, on the local library's public members | 1a |
| AGENTLYNTAI1b-ii | d | built as AGENTPROVIDER1c (§2) | — |
| AGENTLYNTAI1b-iii | f | one part per capability: LQ11, LQ16, LQ3 and LQ17 are not built in Lyntai; AG12 is | Lyntai |
| AGENTLYNTAI1c | f, b tail | Lyntai built R-c (its AG21 to AG24, unreleased), so this is buildable against the local library after 1b-i; one read per agent on a named account | 1b-i |
| AGENTLYNTAI1d | f | waits on LQ4, LQ5, LQ6, LQ16, LQ17 and LQ11's access half, none of them a Lyntai row yet | Lyntai |
| AGENTLYNTAI1e-i to e-iv (4 rows) | f | wait on LQ3 widened, LQ7, LQ8 and LQ17's context half; and m-ii's rest (BL38c) | Lyntai, BL38c |
| AGENTLYNTAI1f-i, f-ii (2 rows) | f | wait on LQ3 and LQ13 | Lyntai |
| AGENTERRORS1 | a, b tail | 1d's rest (an intake's ask bound again after a second `intake.transient`; an opinion's gate read from its code; each kind's authority its check) is (a); 1c's measured `--resume` spends; 1e after 1c | BL38c |
| LIMITRESUME1 | b | f with TOOL4n: the code is built (`Loop/Session/NativeLimits.cs:14`); one native session run to its limit confirms it | the spend sitting |
| BGWAIT2b | b | the probe, once | the spend sitting |
| HELPMODE1a3 | b | full access on the installed pins, with SESSCTX1a and FABRIC-C2b | the spend sitting |
| HELPWIRED1b | a | what the door says it loaded beside what was handed, read from the recorded init frames | BL38c |
| CODEXTOOLS1 | b | a fresh and a resumed Codex quest using the connector on the first turn | the spend sitting |
| MODELROLE1c4 | b | c4 and d3 on the install | the spend sitting |
| COAGENT1 | a | g (a hand-over waits for descendants), c (a conversation as the lead), b2 (a piece's later acts and landing proofs). d (`partner_ask`) is in the design's order before e but has no row (§4) | BL40 for c |
| STAFFPOLICY1 | b | d's and f's rests are built (BL7); e3 went with CONTEXTDRIVE1n; left i (installed) and h (parked behind its measurements). The agent's own hand-over in the terminal's runner goes with RPC1g4 | the install |
| MODELROLE1e2 | d | built with BL7 (§2) | — |
| MODELROLE1l | d | built (§2) | — |
| MODELROLE1i | b | a batch of row b's records measuring D160's share, then an (a) chunk | the install |
| MODELROLE1j | b | after i | i |
| MODELROLE1k | b | SWITCHMODEL1's read of the per-model weeks on the install's accounts (a usage read, no model spend), then an (a) chunk | the install |
| CONTEXTDRIVE1 | a | e's rest (the failures park and the tries hold record their acts through `SupervisorRuleActs`), g's rest, h's rest, k's words, l and m (`DriverConfig.Strikes` and its tests; the refused `moves` and `most`); the owed Opus read; m's installed proof; j parked | none |
| TERMTRUST1 | b | a's part (i) proof and §8 case 4 on every door, then activation and case 14; b parked | the install |
| CONFIRMWAIT1 | b | a real restart and an hour's wait | the install |
| PERSONDOOR1j | a | a nameless ask bound by its process or refused | none |
| CHATFLOW1 | b | the installed look | the install |
| FABRIC-MILESTONE1c | b | the milestone seen on the window | the install |
| FABRIC-SQUASHCHAIN1 | c | Q1 | Q1 |
| SETUPUX1 | a, b tail | i's kept selection across pages and searches; x on the install | none |
| WORKSETUP1 | b | G10 and its tail (AUTOTIDY1c, SQUASHTIDY1d, PLUGHOOK1d, FABRIC-PERFORM1l) | Q5 |
| INTEGRATE1 | b, c tail | k by the steward from the terminal; h's proof; m's rule for Lyntai is Q4; i parked | the install |
| FABRIC-PIPELINE1 | a, b tail | 1d's help row and its goldens; e, f, j buildable; g on the install; h after FABRIC-PERFORM1l | BL32 |
| FABRIC2 and LANDNODE1 | b | WORKFLOWEDIT1c and LANDNODE1j, the installed window | the install |
| REVIEWFLOW1 and LINESTOGETHER1 | b | REVIEWFLOW1c on the window; LINESTOGETHER1e parked | the install |
| COMMS1 | a, b tail | the look handing a teammate's ask's messages to a part here; the person posting on a line's own channel; n on the install | none |
| COWORKUX1 | a, e tail | b's *Back to lead*, d (*Ask again*, *Pass to the lead*), f (*Bring in another agent…*), the head's presses; i waits on COAGENT1d and a design of its own | none |
| CHANNELJOIN1 | e | no design; it shares EVENTHOOK1e's question (an outside agent's session hears and speaks to Daoris) | none |
| MSG1 | b | h and i measured per door (ONEWAY1g's parity measures), j with COMMS1n | the spend sitting |
| STEER3 | b | Codex's `turn/steer` and Claude Code's stream-json input measured | the spend sitting |
| COWORKVIEW1 | b | a's rest; f parked | the install |
| GITDESIGN1 | b | G8e is built (`9fc8a65f7`); left G10, the install (Q5), and G8-CONVERT1 once no home from before G8 can start, which is after the install's home has started on a later build | the install |
| GITAUTH1 | a, b tail | b (the pinned MinGit fetch the toolchain lacks), e (the sync card's screen door to *Add a git identity*), g's concurrent grants and a ref moved after judgement, on the stand-in hosts; c, f's look, g's real host and h are G10's | none |
| GITAPP2 | a | Ask Daoris's fetch card; the push's interruption and the person's `pre-push` hook proven; Stash Drop (§4) | none |
| GITAPP1 | d | folds into OPTIMIZE1a (§2) | — |
| CANVAS1 | a, b tail | fold for any folder; routing past 200 lines or 64 cards a route; `aria-setsize` on culled cards; g on the install | none |
| MAPUX1 | a, b tail | the Tab order measured in Chromium on both levels; g on the install | none |
| FLOWCHART2 | b | h, the installed window | the install |
| AGENTSPLACE1 | b | 1l, the installed window | the install |
| LANDPRESS1 | b | reproduced on the staged build; a fix if it names code | the install |
| PERF1 | a, b tail | `GET /api/sessions/{id}` and the whole-list readers are built (PERF1e `b9c9d77f1`, PERF1f `7b685ee6c`); left the tidy's end judgement (about 5 git starts a branch), the offer's judgement (about 12; `cat-file --batch`), repeated count queries when origin's line is missing; start-up in the window | none |
| MCPMEM1 | b | a real long chat on the install | the install |
| EVENTHOOK1 | b, a tail | 1a, the steward's measurement in its own session outside Daoris, runnable now; then 1b and 1c (a); 1h on the install | none |
| EVENTHOOK1d | e | a design round from the quests' recorded operations | 1b |
| EVENTHOOK1e | e | a design round: the inbox adapter and the knowledge server's channel | none |
| EVENTHOOK1f | f | a quest to the plugins repository | 1c |
| ONEWAY1 | a, b tail | j's rest (`SESSION_GO_ON_BRIEF` folded into `SESSION_HAND_OVER`); r's rest (the doors' recovery refs and undo records give way to the journal's; the page's list of deletes); ac's rest (the unused `note` prop); n-ii; ai's rest (about 100 comments, into the sweeps); x's census, w and y on the install. h, g and ah are aliases (§2) | none |
| WIRING1e | a | `SYSTEM_OPEN_FOLDER`, the copied terminal line, `SYSTEM_PATH`, `SYSTEM_SERVICE_ADDRESS`, a `system` scope (`docs/2026-10-16-this-daoris-design.md` §4) | none |
| REVIEW2-LEFT1 | a | 556 test files call `Path.GetTempPath()` (91 in the driver's `Loop` tests alone); the modules' `Bridge` home has no fixture; re-cut in §4 | none |
| DOGFOOD1 | b | rows run through `dev-line` on the install | the install |
| OPTIMIZE1 | a | a: git's starts counted per view, landing and test class, then a held reader (GITAPP1's part two); b and d parked (§4) | BL31 |

**Counts:** a 25, b 32, c 1, d 4, e 3, f 14: 79.

## 2. Closes, folds and stale text

### Archive with this outcome line

| Row | Checked | Outcome line |
|---|---|---|
| **AGENTLYNTAI1b-ii** | `Agents/Providers/AltStub/` and its line in `Providers.Built()` (AP-H, `1415b9626`) | 2026-11-05: built as AGENTPROVIDER1c: `alt-stub` through the contract alone, its own limit, window, sign-in grammar and home variable. Its layer cases go with 1b-i's fourth view; its own-vocabulary cases pend on j (protocol) and 1f-ii (native). |
| **MODELROLE1e2** | BL7 (`395b875ad`): a hand-over keeps its model and effort, a handed start takes the role's choice; the roles area: the four fenced roles are edited in the one role editor (D233 choice 5) | 2026-11-05: built with BL7. The partner's real-process case goes with COAGENT1b2 (BL47); the installed proof with the installed batch. |
| **MODELROLE1l** | `a94212935`: Design a seeded role (`opus` · high), `StartPlan.RoleOf("design")`, `Defaults/roles.json:4` | 2026-11-05: built: design work starts on the Design role with no model named. The installed proof, a design quest on Design, is in the setup sitting (§6). |
| **GITAPP1** | part one built; part two proposed and not designed | Part one built (D202). Part two, Review's answers kept across opens by a held reader, is OPTIMIZE1a's, measured first. |

### Folds and rewrites (the row stays, its text changes)

- **SESSWATCH1:** 1a is built; the row names 1b's and 1c's rests only, and drops "with STAFFPOLICY1e3 (now CONTEXTDRIVE1)".
- **STAFFPOLICY1:** d's rest (`4086dce47`) and f's rest (`2eb0186db`) are built; e3 went with CONTEXTDRIVE1n (BL8). The row keeps i and h,
  and the agent's own hand-over through the terminal's chat runner moves to RPC1g4.
- **PERF1:** "Left: `GET /api/sessions/{id}` and the callers that still read the whole session list" is built (PERF1e, PERF1f); the row
  keeps the three judgements and start-up.
- **ONEWAY1:** WIRING1d is built (BL25, `93a93fa53`), and there is no WIRING1d row; h-iii is built (BL4); i is CONTEXTDRIVE1n, built.
  h, g and ah are aliases: h is AGENTPROVIDER1 d to u, g the parity rows (LIMITRESUME1f, AGENTERRORS1c and e, BGWAIT2b, MSG1h and i, STEER3,
  HELPMODE1a3), ah AGENTPROVIDER1t. The row names them as those rows and keeps j's, r's, ac's and n-ii's rests, ai's (into the sweeps), x,
  w and y.
- **REVIEW2-LEFT1:** "about 48 tick classes" is 556 test files by the census of `Path.GetTempPath()`; the re-cut is §4's.
- **EVENTHOOK1b's design row** names `AutoLander.Tried` for `work.landed`; the auto lander is deleted (NOCOMPAT1b2). The landing event comes
  from the bound run's `land`, which BL59's builder reads in §4.2 of the event-hook design first.
- **The decisions' status:** D237 (the merge tool), D238 (RPC1g) and D239 (MAPUX1) still say *proposed* and "amends, once accepted",
  while MAPUX1a to f are built and RPC1g1 is in flight (Q7).
- **SCOPEVERIFY1's own line:** a fresh checkout's `npm run quick` fails at `contracts.mjs --check`, since the ignored generated files were
  never written. Every builder's worktree is fresh, so quick writing them first is fixed at the start of the verification, or sooner by
  the steward.

## 3. Questions for the owner

1. **Q1. FABRIC-SQUASHCHAIN1.** An earlier part of a squash chain, whose squashing landing ended by its content or its pull request, is
   ended by the keeper while the press's squash path keeps it, so it is judged at every pass and never goes. Either the press accepts a
   squashing landing ended by its own proof (the commits kept on a recovery ref), or the keeper does not end that part there. The first
   is suggested: one judgement, and the recovery ref is the undo. If no place squashes a chain any more, the row is retired instead.
2. **Q2. AGENTLYNTAI1 against Lyntai's local library.** D240 point 8 says the rows build against Lyntai's local library with no release;
   the Lyntai design's §7.3 says no merged branch, gate or CI build may consume a local pack. On main, every machine and the CI would need
   a pack made from a named Lyntai commit. Either main carries a property naming that pack (BL56 writes the note and the props), or the
   Lyntai rows live on one long branch, merged when Lyntai publishes. §4 weighs whether the moves are worth building at all.
3. **Q3. INTEGRATE1i before the flip** (the merge tool design §7). After INTEGRATE1k, each builder's branch is a card the person presses.
   Unpark INTEGRATE1i (the steward's acts) to land before the flip, or keep the tool's queue and hold MT9.
4. **Q4. INTEGRATE1m.** The install's rule for Lyntai's declared integration.
5. **Q5. G10 and WORKSETUP1.** The SSH identity for the work workspace's host and its key added there, one API token, and the Apply of
   the plan over the 29 repositories. The first pull request opens WORKSETUP1's four tail rows and FABRIC-PIPELINE1h.
6. **Q6. HOMELAYOUT5.** A sitting with the owner's sign-in per harness, which decides how ONEWAY1w ends D172's old places.
7. **Q7. D237, D238 and D239.** Accept them, so their amendments are written: MAPUX1 is built on D239, RPC1g1 is being built on D238 and
   the MT chunks will build on D237.
8. **Q8. The spend sitting's cap.** D240 point 7 allows each proof once and small; a number for the sitting (§6, step 7) lets the steward
   run it without coming back.
9. **Q9. COAGENT1d.** `partner_ask` (a lead asks for a piece itself) is in the co-agent design's order before COAGENT1e and has no row.
   File it (BL64 on the conditional list), or retire it, and COWORKUX1i with it.
10. **Q10. The proposals of §4** (retire Stash Drop; park OPTIMIZE1b and d; REVIEW2-LEFT1 re-cut; AGENTLYNTAI1's moves held).

## 4. Worth building: proposals

The rows are kept; each proposal is the owner's to take.

1. **GITAPP2's Stash Drop: retire.** Deferred since D204 and asked for by no one; a drop is a destructive act whose undo would need its own
   journal record, which costs more than the act is worth while Stash keeps every entry.
2. **OPTIMIZE1b and d: park.** Both are held by their own row and by the standing direction (optimization after the foundation); they are
   not part of completing the backlog. Move them to `docs/backlog-parked.md` and keep OPTIMIZE1a, which GITAPP1's part two needs.
3. **REVIEW2-LEFT1: re-cut as one way, not 556 edits.** Moving each test home by hand is about four Sonnet sittings of identical edits.
   The test runs can root the process's temporary folder in the repository instead (`TMP` and `TEMP` set in the test projects' run
   settings and in the web's test setup, to a folder under `_fixtures/`), with the census turned from "no call of `GetTempPath`" into
   "every test process's temporary root is the repository's". One small Sonnet chunk (in S5), and the rule holds for tests not yet written.
4. **AGENTLYNTAI1's moves (1d to 1f-ii, j, and the layer half of t): hold until Lyntai answers, or retire.** They move working doors onto
   the library with "nothing changes" as their proof, about eleven Opus sittings plus the two-path oracle, and they wait on about twelve
   capability requests Lyntai has not received (§8 of the Lyntai design; Lyntai's open rows list none of them). Building 1a, 1b-i and 1c
   now (Lyntai built R-c) proves the reference and the oracle cheaply. If the owner judges the shared library not worth the moves, the
   rows retire and Daoris's own transports stay the one way (D228), the layer half removed with them.
5. **FABRIC-SQUASHCHAIN1:** retire if no place's `land` squashes a chain (Q1).
6. **MERGETOOL1's FR1 to FR3 (the family rehearsal cut by phases):** keep, last. The rehearsal's 375 KB is read by every builder that
   touches it, so the cut pays back; it touches no product file.
7. **SESSWATCH1e:** keep as written, conditional on 1d; nothing is built unless the record shows quiet that is not work.
8. **WIRING1e:** keep; its `SYSTEM_PATH` and `SYSTEM_SERVICE_ADDRESS` change the person's account environment, so each says what it will
   do first and refusals come back as data, as the row already requires.

## 5. Build chunks

Each chunk reads its area first, adds new behaviour in a unit of its own, writes its tests without running them, compiles clean, merges
main before its hand-back and hands back at about 150 tool calls. Web chunks add catalogue keys in their own namespace files; the help
room's goldens are regenerated at the verification, never by hand; a generated contract file in conflict is regenerated, never merged by
hand.

**One serial track.** `Loop/Chat/ChatRunner.cs` and the executors beside it are the hub several chunks touch. They go one per wave, in
this order: BL38a (wave 9), BL52 (10), BL38c (11), BL39 (12), BL40 (13), BL46 (14), BL43 (15). A slip there slips the conversation track's
last wave, as the landing track did in the first plan.

### 5.1 The chunks

| Id | Rows | Files, starts | Exemplar | Proof | Model | After |
|---|---|---|---|---|---|---|
| **BL31** | PERF1's rest | `Workplace/EndJudgement.cs:222`, `Workplace/TreeOffers.cs:12` (`LandOffer`), `Workplace/GitRuns.cs:8`; the service's session counts | `e20cc9b6f` (the tidy, 32 to 6 starts), `604148d70` (benchmark homes and budgets) | `Process` budgets show fewer git starts per branch for the end judgement and the offer; the figures in the driving area | Opus | none |
| **BL32** | NOCOMPAT1b2's last traces | `Review/AutoLandingWorld.cs` and its five test files; service `Daoris.Service.Core/Quests/QuestExchange.cs:163` (`BindAsync`); `Loop/Look/BindRunsLook.cs`; web `projects/setup/WorkspaceSetup.tsx` | `3578d416c` (the plain bind retired) | no product name says the auto lander; a bound run's close reads its run, not a take's bind (a service test); the Setup tab gone or its condition named | Opus | none |
| **BL33** | CONTEXTDRIVE1 e's rest, l, m (driver) | `Settings/DriverConfig.cs:33`, `:73` (`Strikes`, about 35 test files set it); `Roles/StaffPolicy.cs:444`, `Roles/StaffPolicyDoor.cs:18` (`moves`, `most`); `Roles/SupervisorRuleActs.cs:24` | `0f4b400bc` (a rule's act said and saved), `84b0e6116` (`convert-first` deleted) | no `strikes` read; the failures park and the tries hold each record an act read back by *Details* and the supervisor's print | Opus | none |
| **BL34** | CONTEXTDRIVE1 g's rest, h's rest, k's words (web) | web `agents/roles/`, `work/head/headFacts.ts`, the machine's page; modules `HelpCoverageTests`; the dead `settings.ai.policy.*` keys | BL12 (`242234a89`) | the head's chip with its count, *Details*' counts, `onChangeRule` handed to head and strip; en and zh stories | Sonnet | BL33 |
| **BL35** | SESSWATCH1b's and 1c's rests | `Loop/Run/Conclude/QuietWord.cs:12`, `Course/Domain/AnswersAlone.cs:117`, `Review/Heavy/LongCalls.cs:29`, `Settings/QuietSetting.cs:10`; service `Daoris.Service.Mcp/ChosenSettingsTools.cs:46`, `ToolClasses.cs:65`, `CallTable.cs:101` | `879bb0957` (the protocol door's word); `settings_cap` | the session watch design's cases 2 (the word), 8 and 10 on each door; `settings_quiet` in the call table's census | Opus | none |
| **BL36** | PERSONDOOR1j; GITAUTH1g's concurrent grants and a ref moved after judgement | service `Daoris.Service.Http/PersonConfirmations.cs:135` (~142-148, ~451-462); the transport judge's tests on the stand-in hosts | G3 (`c6e86f40b`) | a nameless ask cannot hold both of a terminal's places; two grants at once and a moved ref each refused before any ref moves | Opus | none |
| **BL37** | WIRING1e | modules' system routes; web Settings → This Daoris; the design's §4 | WIRING1c (This Daoris's cards); `.claude/knowledge/setting-doors.md` | tests per action; each said first; refusals as data; each coverage row named | Sonnet | none |
| **BL38a** | AGENTPROVIDER1h's rest and n's rest (h ⚖) | `Agents/Contract/ProcessRecipe.cs:12` and the shell; a `SpawnRequest` composed by Loop's executor in place of `SessionTarget` and `ChatTarget`; Platform's launcher starting the completed recipe alone (D201); `Roles/Domain/ActorRun.cs:165` | AP-B2 (`3a0c71790`, each door's spawn a recipe the one shell completes); AP-J (`759f4fae0`) | the design's h line: each built-in door's spawn equal to today's in a table; `StructureTests` with no edge from Platform into Agents; asked, carried, acknowledged and reported kept apart | Opus | none |
| **BL38b** | AGENTPROVIDER1d's rest, e's rest, f | `Agents/RosterNext.cs:6` (the door's own home: s-ix o1 to o3, `provider-owner.json`'s `disagrees` cells); the `OwnerOf` and `ToolchainOf` classes; `Agents/Contract/ProviderParts.cs:111` (`DoorTablesPart`); `Agents/Domain/SwitchDecision.cs:11` and the page's floor per agent; `Agents/PinInstall.cs:29` | AP-A2 (`e981a7922`), AP-G (`6a1a5c88f`) | each s-ix cell's old and new cases paired; a door's own table beside its provider's; a note on D130 | Opus | none |
| **BL38c** | AGENTPROVIDER1l's rest; m-ii's open choice | the four `stream-json` readers into one; the lineage as Participants' part; `Participants/Domain/ChildBindings.cs:84`'s fold split; `Agents/DoorInput.cs:36`, `Agents/ProtocolWords.cs:10`, `Loop/Chat/ProtocolChat.cs:20` | AP-C (`18b2e5b48`, readings on one sink), AP-F2 (`7354152a8`, `DoorInput`) | the design's l line (each fact read once; a fixture session's events byte for byte); what `DoorInput` carries for a protocol prompt decided, **D246** reserved if it rejects an alternative | Opus | BL38a |
| **BL38** | AGENTPROVIDER1r's rest (COAGENT1e) | web `agents/agents.ts:551`, `:565`, `agents/AgentPage.tsx:157`, `agents/AgentsView.tsx:403`, `:454` (`doors[0]`); `agent list`; the room reading `IAgentFacts`; `Plugins/PluginCatalog.cs:429` (one `posture`); the family rehearsal's Stub Two phase | AP-R (`953296828`) | no `doors[0]` deciding an act; the sheet on the agent's page in en and zh; the two postures' fixtures | Sonnet | none |
| **BL39** | AGENTPROVIDER1s-vii and u (both ⚖) | `Roles/StartPlan.cs:123` (the walk; `not-opened`); `Agents/Usage.cs`; web `work/head/headFacts.ts`; the pick's carry in `Loop/Chat/ChatRunner.cs` | the AP design §11 rows s-vii and u | a pick carried across `claude-code` and `claude-code-acp` on one account and not between two providers sharing a pool; an `Opened` provider passed in the walk with its code; notes on D182 and D212 | Opus | BL38, BL38b |
| **BL40** | AGENTPROVIDER1t (ONEWAY1ah) | `Agents/ISessionAdapter.cs:11`, `Agents/AdapterSet.cs:18`, `Agents/Harnesses.cs:152` (`HarnessToolchain`), `Agents/Registry/LegacyDoor.cs:19`, the stand-in classes; `Driver.Tests/fixtures/provider-vocabulary.json`; `Agents/ProviderCensusTests.cs` | AP-H (`1415b9626`) | the census's exceptions only the permanent ones; an older build's records read (the design's t line); its installed proof spends (§6) | Opus, two sittings | BL38a, BL38c, BL39 |
| **BL41** | HELPWIRED1b; AGENTERRORS1d's rest | `Help/HelpWired.cs:168`; `WorkOrders/IntakeRoom.cs:26`; `Review/Domain/OpinionGate.cs:18`; `Agents/AgentErrors.cs:88` | `e440d8f0a` (one retry at every executor) | what the door said it loaded beside what was handed, from recorded init frames; the intake's second transient bound; the opinion's gate from its code | Opus | BL38c |
| **BL42** | RPC1g2, g3 | `Daoris.Desktop.Driver.Host/ChatConsole.cs:28`; the pipe client | RPC1g1 | the terminal conversation design's §5 host tests per row; `ask-daoris resume` gone from usage and Help coverage | Sonnet | RPC1g1 |
| **BL43** | RPC1g4; STAFFPOLICY1's terminal hand-over | `Loop/Chat/TerminalOwner.cs:9`, `Course/Requests/TurnMarks.cs:28`, `ChatConsole`'s runner path, the request folder's watch | BL42 | the deleted tests gone; the census; the register entry for leftover `*.turn` marks | Sonnet | BL42, BL46 |
| **BL44** | FABRIC-PIPELINE1d's help row; 1e | `Workflows/VerifyRuns.cs:29`, `Workflows/Nodes/CheckPort.cs:24`, `Loop/Watch/WatchNodes.cs:20`; the help room's workflow rows | BL22 (`2423aa9e7`) | a batch's run in its own folder to its verdict, an unknown held for the person, the failure in *What needs you*; the design's §7 read against G8's `land` first | Opus | BL32 |
| **BL45** | FABRIC-PIPELINE1f, j | as BL44; *Ask for a fix…*; the gates a batch's paths reach | BL44 | one ask per gate, the log attached; the reach for any repository | Sonnet | BL44 |
| **BL46** | COAGENT1g, c | `Course/HandOver/`; `Loop/Chat/ChatRunner.cs:23`; the head's ⋯ menu | COAGENT1b's built pieces | a `Process` case with a background command outliving its turn; `PartnerChatProcessTests` | Opus | BL40 |
| **BL47** | COAGENT1b2; MODELROLE1e2's partner case | `Participants/Domain/PartnerPieces.cs`; service `Sessions/SessionLedger.cs`; *Discard…*, *Hand over what it left* | COAGENT1b | `PartnerLandingTests`; the partner's real-process case | Sonnet | none |
| **BL48** | COWORKUX1 b's *Back to lead*, d, f, the head's presses | web `work/atWork/AtWorkView.tsx`, a member's thread; the service's box twins; the driver's `helper.person-asked` | BL17 (`b718abdc7`); the coworker design §6.2, §6.4, §7 | *Ask again* and *Pass to the lead* in en and zh with Ask Daoris's coverage; the dialog of §6.2 | Opus | none |
| **BL49** | COMMS1's two loose items | `WorkOrders/ChannelReach.cs:101`; service `Daoris.Service.Core/Asks/ChannelDesk.cs:13`; web `work/atWork/` channel | BL21 (`e5de3f180`) | a part here handed a teammate's ask's messages at its look; the person's post on a line's own channel | Sonnet | none |
| **BL50** | SETUPUX1i's kept selection; CANVAS1's three; MAPUX1's Tab order | web `agents/setup/` tree; `design/canvas/CanvasList.tsx:50` and the culled layer; `map/MapTree.tsx`, `map/MapView.tsx`; `measured/` | BL14 (`ffe3cee9d`), BL19 (`42b0c38e7`), `fa8552702` | stories in en and zh; the measured Tab order walked on both levels | Sonnet | none |
| **BL51** | GITAUTH1b, e; GITAPP2's fetch card and two proofs | the toolchain's pins (a MinGit fetch with its hash); `Help/Proposals/HelpSyncProposals.cs:27` (the screen door; the fetch card); the push act's tests | G6 (`370f5e57a`), G7 (`b61d50768`) | the doors' classes on the pinned MinGit; the card's door with the host filled; an interrupted push said unknown; a `pre-push` hook refusing | Sonnet | none |
| **BL52** | ONEWAY1j's rest | modules `Bridge/Routes/ConversationRoutes.cs:187`; `ChatRunner`'s `GoOnBriefedAsync`; web `bridge/sessions/conversation.ts`, `work/conversation/useWords.ts`, `help/ask/useHelpSend.ts`, `locales/*/person.json` | BL13 (`bc9d75f46`) | one route per go-on case on the work page and in Ask Daoris | Sonnet | BL38a |
| **BL53** | ONEWAY1r's rest, ac's rest, n-ii | `Workplace/RecoveryRetention.cs:19`, `Workplace/RecoveryDropping.cs:36`; the git page's list of deletes; `Note`'s callers | G7, BL28 (`3fd8f2c2b`) | a delete's recovery read from the journal alone, the old reader kept with its ending condition; the page lists deletes | Sonnet | none |
| **BL54** | OPTIMIZE1a's count | `Workplace/GitRuns.cs:8`; per view, landing and test class | BL31 | git's starts counted and recorded in the area; the held reader proposed or not by the figures | Opus | BL31 |
| **BL55** | design: EVENTHOOK1d, 1e and CHANNELJOIN1 | the event-hook design §4.2, §6.2; the integration design §7.1 | the event-hook design's rounds | one design with rows; **D244** reserved | Opus | none |
| **BL56** | AGENTLYNTAI1: the reference note, the requests, then 1a and 1b-i | `src/Daoris.Desktop/Directory.Packages.props` (the service's names `DaorisLyntaiReleased`); `Agents/Layer/` | the Lyntai design §7, §9.2 | the closure restored offline; 1b-i's host-policy tests; Lyntai's built AG rows read against §8 and what remains published as one quest; **D245** reserved if Q2 puts the pack on main | Opus, two sittings | Q2 |
| **BL57** | the owed Opus read of CONTEXTDRIVE1 | the context counters design | D240 point 9 | the read recorded beside the design | Opus, a read | none |

### 5.2 The waves

| Wave | Chunks (up to five at once) |
|---|---|
| 9, now | RPC1g1, BL26d (in flight); **BL38a**, BL31, BL32 |
| 10 | **BL52** (BL38a), BL38b, BL38, BL33, BL35 |
| 11 | **BL38c** (BL38a), BL42 (RPC1g1), BL34 (BL33), BL36, BL44 (BL32) |
| 12 | **BL39** (BL38, BL38b), BL41 (BL38c), BL45 (BL44), BL49, BL50 |
| 13 | **BL40** (BL38c, BL39), BL47, BL48, BL51, BL53 |
| 14 | **BL46** (BL40), BL54 (BL31), BL55, BL37, BL57 |
| 15 | **BL43** (BL42, BL46); BL56 in the first free slot once Q2 is answered |

The bold chunk of each wave is the conversation track's. Each wave's exemplar is its Opus chunk on a new shape: BL38a (the spawn's
request), BL33 (the rules' acts), BL44 (the pipeline), BL39 (the corrections), BL48 (the co-workers), BL55 (the design).

### 5.3 The verification, then what is built beside it

1. **SCOPEVERIFY1**, once wave 15 has merged: quick fixed to write the generated contracts first; the full CI once on main's tip; its reds
   fixed together (Opus fix chunks, mostly); the help room's goldens regenerated; the row's listed proofs (the ssh door's push relay, the
   `StatePublisherTests` case). The install is staged from the green run and §6 runs on it.
2. **Beside the installed batch, on their own branches** (they change no behaviour, or switch only after a green run):
   - **BL58** COMMENTGATE1's gate (Sonnet; exemplar the size ratchet, `tools/size-ratchet.mjs`): a changed file's comment share may not
     rise; no row id, decision number or history in a comment; proof the gate refusing a fixture that adds a `D99` comment.
   - **The sweeps** S1 (the driver's product code), S2 (its tests), S3 (modules and service), S4 (the web), S5 (the CLI, the devkit, the
     tools outside MERGETOOL1, and REVIEW2-LEFT1 as §4 re-cuts it): each COMMENTGATE1's targeted pass and ONEWAY1ai's rest (about 100
     comments spelling `daoris driver …`). Sonnet, one per project, nothing else beside them; after BL58.
   - **MERGETOOL1**: MT1 (Opus) to MT8 and FR1 (Opus) to FR3 in the design's order, one MT at a time on main after a green run (D237 §4.2);
     MT6 (Opus) and MT7 change behaviour, each after a green CI on the moved tool; MT9 waits on Q3.
3. **SCOPEVERIFY2**: one light CI after the sweeps and the MT moves, the install staged again, and the installed batch's steps that a
   later chunk changed run again. Then the owner's review of the code and the documents.

### 5.4 Conditional chunks, each on the wave after its condition

| Id | Rows | Condition | Model |
|---|---|---|---|
| BL59 | EVENTHOOK1b | 1a's measurements and decision rule | Opus |
| BL60 | EVENTHOOK1c | 1a's M5, M9, M10 and BL59 | Opus |
| BL61 | SESSCTX1e (and ONEWAY1j's *Go on fresh*) | SESSCTX1a measured | Sonnet |
| BL62 | SESSWATCH1e | 1d's D200 note naming cells | Opus |
| BL63 | MODELROLE1i, j; MODELROLE1k | the records batch; SWITCHMODEL1's read | Opus |
| BL64 | COAGENT1d, then COWORKUX1i's design | Q9 files it | Opus |
| BL65 | AGENTERRORS1e | 1c's measured `--resume` | Opus |
| BL66 | G8-CONVERT1 | the install's home has started on a build after G8 | Sonnet |
| BL67 | a LANDPRESS1 fix | the reproduction names code | Opus |
| BL68+ | AGENTLYNTAI1c, 1b-iii's parts, 1d, 1e-i to e-iv, 1f-i, 1f-ii, AGENTPROVIDER1j; EVENTHOOK1f's quest after BL60 | Lyntai builds each request (§4 point 4) | Opus for each ⚖ move, Sonnet for 1b-iii's parts |

## 6. The installed batch

Run against the build staged from SCOPEVERIFY1's green run, in this order; each step that spends runs once at the least that proves it,
its spend reported (D240 point 7).

| Step | Rows | What the owner gives |
|---|---|---|
| 0, now | **EVENTHOOK1a**: the steward's measurements in its own session outside Daoris (M1, M5, M8 to M10) | nothing; it needs no install |
| 1 | **LANDPRESS1**: the press reproduced; a fix becomes BL67 | nothing |
| 2 | **INTEGRATE1k** (ONEWAY1y) set up by the steward from the terminal; INTEGRATE1h's proof; FABRIC-PIPELINE1g's two pipelines | the press setting the pipelines; Q3, Q4 |
| 3 | **The setup and workflow sitting**: SETUPUX1x and its fixes, FLOWCHART2h, CANVAS1g, MAPUX1g, AGENTSPLACE1l, FABRIC-MILESTONE1c, CHATFLOW1, WORKFLOWEDIT1c, LANDNODE1j, REVIEWFLOW1c, MODELROLE1l's design quest on Design; 1546 and 680 px, en and zh | a look at the window |
| 4 | **PERF1**'s start-up; **MCPMEM1** in a real long chat; **KNOWCENTRAL2i** | nothing |
| 5 | **CONFIRMWAIT1** (a restart and an hour); **TERMTRUST1a** (part i, §8 case 4 on every door, then activation and case 14); **RPC1g5** | presses at the terminal |
| 6 | **COMMS1n** with SESSONE1c-look, **MSG1j**, **COWORKVIEW1a**'s rest | the person's first use (22) |
| 7 | **The spend sitting** (Q8): HELPMODE1a3, SESSCTX1a (releases BL61), FABRIC-C2b, STAFFPOLICY1i with LIMITGOON1's look, MODELROLE1c4 and d3, CODEXTOOLS1, LIMITRESUME1f with TOOL4n and AGENTERRORS1c's `--resume` (one native session to its limit; releases BL65), BGWAIT2b, STEER3, MSG1h and i, AGENTLYNTAI1s-ii, AGENTPROVIDER1t's proof (one session per protocol door and on the native door, one resume each), CONTEXTDRIVE1m | the cap |
| 8 | **G10** with GITAUTH1c, f's look, g's real host and h; **WORKSETUP1** and its first pull request | Q5: the identity, the token, the Apply |
| 9 | **HOMELAYOUT5** and ONEWAY1w; ONEWAY1x's census | Q6: a sign-in per harness |
| 10 | **MODELROLE1i**'s records batch; **MODELROLE1k**'s read of the per-model weeks (releases BL63) | nothing |
| 11 | **SESSWATCH1d** after about two weeks of driven work (releases BL62); **EVENTHOOK1h** once BL60 is merged and the install republished; **DOGFOOD1** throughout; **G8-CONVERT1**'s condition (releases BL66) | *Install from a folder* and the switch for `event-relay` |

## 7. Sizing

| Step | Chunks | Opus / Sonnet | Builder-hours |
|---|---|---|---|
| Waves 9 to 15 | 30 (BL31 to BL57 with BL38a to BL38c), with about four second sittings (BL33, BL40, BL44, BL56) | 18 / 12 | 56-68 |
| SCOPEVERIFY1 | 2 to 4 CI runs, 6 to 12 fix chunks | mostly Opus | 12-20 |
| Beside the batch | BL58, S1 to S5, MT1 to MT8, FR1 to FR3 | 3 / 14 | 26-34 |
| SCOPEVERIFY2 | one light CI, a few fixes | Opus | 4-8 |
| Conditional | BL59 to BL67 | 7 / 2 | 16-22 |
| **Total** without Lyntai's gated moves | about 71 sittings | | **114-152** |
| Lyntai's gated moves (BL68+) | about 11, as Lyntai answers | 9 / 2 | 25-32 more |

**Wall time.** Seven waves at about three hours each, then one to two days of verification, then the batch and the beside-work together:
**about six days at five wide**, if each chunk is merged as it hands back. The conversation track sets the pace of waves 9 to 15.

**Risks.**
1. **The red at the end.** Nothing runs tests until SCOPEVERIFY1; the first scope took about 25 fix merges. A light CI after wave 12 (no
   `Process` halves) would catch drift halfway, at the owner's call against D180's note.
2. **The conversation track.** Seven chunks on `ChatRunner.cs` and the executors in series, four of them AGENTPROVIDER1's. AP-J handed
   back with d, e, f and h untouched; if BL38a does the same, BL38c and BL40 slip a wave each.
3. **AGENTPROVIDER1t** deletes the seam every door still reads; it is the largest piece left and the last of the track before COAGENT1c.
4. **Lyntai.** Fourteen rows wait on another repository and on Q2; none of them gates a wave.
5. **Stale rows.** §2's rewrites come first, so builders read rows that say what is open.
