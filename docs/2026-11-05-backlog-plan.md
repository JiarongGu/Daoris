# The backlog, planned to completion (2026-11-05)

The owner asked for all of `TASKS.md` done quickly, then a full code and document review. This plan is for the steward dispatching
it. It follows D180's big-scope note: builders take whole chunks, write tests and run none, compile, and hand back at about 150 tool
calls. At most five run at once. Sonnet takes chunks that follow an exemplar on main; Opus takes designs, first exemplars and
judgement. Every check runs once, at the scope's end (SCOPEVERIFY2, §5). Each claim below was checked against main at `b9c9d77f1`.

**First**, the steward rewrites `TASKS.md` from §1: the closes archived, the merges folded, the parked rows moved. Builders then read
rows that say what is open.

## 1. Close or fold

### Done or superseded on main. Archive with this outcome line

| Row | Checked | Outcome line |
|---|---|---|
| **CODESHAPE1** | `tools/size-ratchet.json` lists seven non-test files over 40 KB: `Events/NoteCodes.cs`, `e2e/platform.spec.ts` and five tools | 2026-11-05: each route and command declared once and generated (A1-A9, B1-B8, C1, D1); the four seams registered (E1-E5); every non-test product file under 40 KB except `NoteCodes.cs` (BL1). F became COMMENTGATE1 (D235); the tools over 40 KB are MERGETOOL1's. |
| **CONSOLIDATE1** | 13 area documents; `canon/core/skills/change/SKILL.md`; `docs/decisions/README.md`'s generated table (211 live); the index leftovers gone from `.gitattributes` and the gates | 2026-11-04: the current truth in `docs/areas/`, the choices settled (D233 point 4), `change` in the canon, the decisions history behind a status table. `docs/development.md`'s records step (line 269) is BL27's. |
| **NOCOMPAT1** (the parent) | no `HomeFloor`, `OlderBuild`, `FloorNeeded` or `transitional-markers.mjs`; `Transitional/` holds only `Migration/` (ten steps); `tools/desktop-publish.mjs` names no `daoris-driver` | 2026-11-05: a (the register, its gate and the floor deleted; one migration, `HomeMigration`), b3 to b8, c (the launcher gone) and d merged. b2 stays its own row (BL6, BL11). |
| **RULESCONVERT1** | `Transitional/Migration/RulesStep.cs` | 2026-11-05: built as the migration's rules step: the caps become the machine's rules, a policy's moves become rules at their place, and `strikes` is taken out. The `convert-first` refusal it leaves goes in BL3. |
| **POOL1d** | `Transitional/Migration/HarnessesStep.cs` folds `harnesses.json` at the first start | Superseded by D232: the migration folds the file once, and its backup is the undo. D232 rejected a dry run the person applies. ONEWAY1v closes with it. |
| **CONTEXTONE1** | rows a to e built | 2026-11-05: built. Codex's read of round 2 waits on its allowance (Q2). The older shapes went with NOCOMPAT1. |
| **FOUNDATION1** | Parts A and B built; `HeldWordsStep` in the migration | Every row built. n is EVENTHOOK1's own row. v's retirements became the migration's steps under D232. |
| **WORKFLOWCLEAN1** (the parent) | `agents/modes.ts` is `['agents', 'setup']`, with no `daoris.agents.setup` flag; NOCOMPAT1b7 left no work cycle outside the migration | Waves 1 and 2 are built. y was lit by NOCOMPAT1b3. j is not needed, because no work cycle survives the migration. z is NOCOMPAT1b2's. i and x stay SETUPUX1's. |
| **SETUPUX1y** (part of SETUPUX1) | as above; the opener's `roleSection` names appointed or all roles only, and `placeOf` resolves (`app/opener.ts:202-238`) | Built with NOCOMPAT1b3. SETUPUX1 keeps i (BL19) and x (installed). |
| **FABRIC-CUTOVER1h** | it came with WORKFLOWCLEAN1j | Not needed: no work cycle survives the migration. It reopens on a sighting. |
| **COMPANYVIEW1** | every row built but 1h | Built. 1h, the steward's marks, folds into the parked STEWARDROLE1. |
| **CODECS1** | its proof is `CodecPlaybackProcessTests` | Closes with SCOPEVERIFY1's green run and the staged install. |
| **ONEWAY1** (the parent) | `DAORIS_DRIVER_CONFIG`, `SET_ACTOR`, `ACTORS` and the timeout doors are gone; the migration holds the staffing, harnesses, requests and session-records steps; the register and the conversion door are gone; the launcher is gone | Rewrite the row as its open sub-rows. c-i, a, b, s, v, l, m, ad, ae, af, ag and ai-i are done or superseded by D232. Open: j (BL13), q and r (BL5), h-iii (BL4), n-ii and ab-ii (BL26), WIRING1d (BL25), aa (BL18), ai-ii and ai-iv, plus checks of c-ii and d (BL29), x (BL23), ac (BL28), w (Q3) and y (Q5). h, g and ah are parked (Q1). |

**SCOPEVERIFY1 is not closable. It is in progress.** Run 4 on `7688f1a` (`local/scratch/ci-7688f1a/`) was read while its Process
halves were still running:

- **Light-gate reds.** service 6 (`ReturningLockTests` 5, `ConnectionGateTests` 1); driver 2 (`ProviderCensusTests`); modules 12
  (`DriverModuleHelpStartTests` 9, `DriverModuleHelpTests`, `DriverModuleHelpHistoryTests`, `DriverModuleBusyServiceTests`); web-unit 1
  (`names.test.ts`).
- **Infrastructure and unnamed failures.** web-rules (the contracts generator's output locked by a concurrent build); cli and
  tool-tests (failures not tied to a named test).
- **Run 5 is owed.** Main has moved on (PERF1e).
- **The row's text is stale.** It says "Not now" and lists run 1's known reds. Rewrite it as run 4's state.

This is wave 0 (BL0).

### Duplicates to merge (D228: one way)

| Fold | Into | Why |
|---|---|---|
| WORKFLOWCLEAN1z, FABRIC-CLOSEBIND1 | NOCOMPAT1b2 | The same deletion: the landing rules, the handed node and the plain bind, with their tests rebound onto bound runs. |
| GITREADONLY1 | GITAUTH1f | GITAUTH1f already names "the doors passing a read-only identity". |
| QUEUEHANG1 | MERGETOOL1 | The queue's runner is one of the tools MERGETOOL1 designs. |
| GITAPP1 part two | OPTIMIZE1a | The row itself says "measured first as OPTIMIZE1a". |
| OPTIMIZE1c (start-up) | PERF1 | PERF1 already lists "start-up measured in the window". |
| FLAKE1 | SESSOPT1d | SESSOPT1d gives FLAKE1 its evidence home. FLAKE1 becomes that home's standing line. |
| SESSONE2i; MIXAGENT3a-iii; SESSONE1c-look | COMMS1k; COMMS1h; COMMS1n | Each is "with" that row. |
| AUTOTIDY1c, SQUASHTIDY1d, PLUGHOOK1d, FABRIC-PERFORM1l | WORKSETUP1 | Each waits on WORKSETUP1's first pull request. They become its tail. |
| WIRING1d inside ONEWAY1 | the WIRING1d row | It is listed twice. |
| TOOL4n | LIMITRESUME1f | Both wait on one recorded native limit. |
| HELPMODE1a3, SESSCTX1a, FABRIC-C2b | one installed spend sitting | One staged build and one bounded yes (Q8). |
| MODELROLE1c4, MODELROLE1d3 | one installed sitting | Both need the install carrying MODELROLE1c3 and d2. |

### Park (stay frozen, or wait on a condition)

- **Fold into the parked, frozen STEWARDROLE1 or SUPERVISOR1** (each waits on STEWARDROLE1d or SUPERVISOR1a-iii): ROLEPERM1,
  XAGENT1f2, PERSONDOOR1 c to i (the triage reads them as the steward's boundary; j goes to BL26), TERMTRUST1b, QUESTIONUI1b, COMPANYVIEW1h,
  LINESTOGETHER1e, INTEGRATE1i, CONTEXTDRIVE1j.
- **Wait on the frozen BRAINSTORM1d and AGENTROOM1b:** COWORKVIEW1f.
- **D233 point 3's frozen host rows:** AGENTPROVIDER1 d to u, with ONEWAY1h, g and ah. Row t removes a second way, but it needs
  d to r first. Only m-i part 4 (BL4) builds now (Q1).
- **A sighting:** TREEOPENERR1.
- **A measurement:** AUTOTIDY1b, STAFFPOLICY1h.
- **A release:** DEVKITAUDIT1 (SDK 10.0.402 still carries the 10.0.13 runtime) and AGENTLYNTAI1's non-release rows (Q9).
- **OPTIMIZE1 b and d:** held, as the row says.

## 2. Needs the owner

1. **Q1. AGENTPROVIDER1.** Park d to u and ONEWAY1h, g and ah as D233's frozen host rows, and build only m-i part 4? Yes parks about 15 rows.
2. **Q2. Codex's owed reads** (CONTEXTONE1 round 2, CONTEXTDRIVE1, the direction review). Close these rows without the reads while
   Codex's spend cap holds?
3. **Q3. HOMELAYOUT5 and ONEWAY1w.** Two options:
   - measure moving each harness's account home on the install (this needs your sign-in per harness); or
   - park both, and name D172's old-place reads (`DaorisHome.cs:119-128`, `HomeLease.cs:128`) as D232's one exception.
4. **Q4. WORKSETUP1.** When will you Apply the work workspace's plan over its 29 repositories and add Daoris's key on the host
   (GITAUTH1c)? That opens the first pull request, which its four folded rows wait on.
5. **Q5. INTEGRATE1k (ONEWAY1y).** A sitting to switch this repository's integration to Daoris's own integrate workflow. It unblocks
   FABRIC-PIPELINE1e to j and INTEGRATE1h's proof. Also needed: Lyntai's declared integration rule on the install (INTEGRATE1m).
6. **Q6. GITAUTH1g.** Is the rest of the transport judge still wanted? It covers Dangerous settable on the page; tag, mirror and
   prune judged; and a grant bound to its ref. If not, cut it, and GITAPP2n pushes a tag on the person's press with a grant, as
   GITAPP2g pushes a branch.
7. **Q7. GITAUTH1b.** May the steward install a local sshd, `gh`, `az` and git-lfs to measure the doors? After that, `--git-doors`
   becomes the default.
8. **Q8. Account spend.** A bounded yes, with a cap, for:
   - one native Claude Code session run to its limit (LIMITRESUME1f with TOOL4n, AGENTERRORS1c's measured `--resume`);
   - BGWAIT2b's probe and AGENTLYNTAI1s-ii's measure;
   - the installed spend sittings in §3.
9. **Q9. Lyntai.** Publish the release that AGENTLYNTAI1's rows (and the parked KNOWFRESH1b) wait on, or park AGENTLYNTAI1 whole?

## 3. Needs the installed window

These wait for the install staged from SCOPEVERIFY1's green run. Batch them against that one build. Run them again on the build staged
from SCOPEVERIFY2 where a later chunk changes what they prove.

| Proof | What it proves |
|---|---|
| **LANDPRESS1** (first) | The window's landing press lands, or says in words why not. Reproduce on the staged build. If the press is still dropped, the read path it names becomes an Opus fix on the landing track. |
| **PERF1** start-up, with OPTIMIZE1c | Start-up time in the window, and the session list and the look at the home's real size after BL2. |
| **MCPMEM1** | The browser server stays under 512 MB in a real long chat. |
| **KNOWCENTRAL2i** | The memory coordinator measured, including repeated or conflicting entries about one fact. |
| **CODEXTOOLS1** (spends) | A fresh and a resumed Codex quest whose first turn uses the connector. |
| **MODELROLE1c4 and d3** | A Build whose first model one account lacks runs on the other, and a silent pipe refusal passes that account with no strike. A chat's chosen model survives a restart, and a refused model goes on in one press. |
| **CONFIRMWAIT1** | A terminal's Confirm request waits across a real restart and an hour. |
| **TERMTRUST1a** | Part (i)'s proof and §8 case 4 on every door. Then the grant is offered and case 14 runs. |
| **The setup and workflow sitting** | SETUPUX1x and its fixes, FLOWCHART2h, CANVAS1g, AGENTSPLACE1l, FABRIC-MILESTONE1c, CHATFLOW1's look, WORKFLOWEDIT1c, LANDNODE1j, REVIEWFLOW1c: workflows composed, run and read, and the place pages, at 1546 and 680 px, in en and zh. Once now, and once after BL14, BL16 and BL19. |
| **The spend sitting** (Q8) | HELPMODE1a3 (full access on the installed pins), SESSCTX1a (resume and a fresh part on each door; it releases SESSCTX1e), FABRIC-C2b (the two-harness canary), STAFFPOLICY1i with LIMITGOON1's look. |
| **EVENTHOOK1a** (the steward) | The hook measurements on the installed Claude Code. They release EVENTHOOK1b and c; 1h is their installed proof. |
| **COMMS1n**, with SESSONE1c-look | The co-working canary, after BL10, BL17 and BL21. |
| **CONTEXTDRIVE1m** | The rules on the install, after BL3 and BL12. |
| **COWORKVIEW1a's rest** | Rule 1's measure, the stories in a browser, the person's first use. |
| **GITAUTH1h**, **INTEGRATE1h** | The git doors seen by the owner; the integration's proof (Q5). |
| **DOGFOOD1** | Rows run through `dev-line` on the install, each measured against a builder's run. |

## 4. Buildable now, as chunks

**Every chunk:**
- reads its area first;
- adds new behaviour in a unit of its own;
- compiles clean and hands back.

**Hubs.** No two chunks in one wave share a hub file. Three rules keep it so:
- BL1 merges first in wave 1, because every chunk adds note codes. The others merge main before their hand-back.
- Web chunks add catalogue keys in their own namespace files.
- The help-room goldens are regenerated at SCOPEVERIFY2, never by hand.

**Wave 0, now (the steward).**

| Id | Rows | Area, key files | Exemplar | Model | After | Done |
|---|---|---|---|---|---|---|
| BL0 | SCOPEVERIFY1 run 4/5's reds | code-structure; the failing classes above | the SCOPEVERIFY1 fix merges | Opus | none | A green CI on main's tip and the install staged from it |

**Wave 1.**

| Id | Rows | Area, key files | Exemplar | Model | After | Done |
|---|---|---|---|---|---|---|
| BL1 | CODESHAPE1's last file: `Events/NoteCodes.cs` (70 KB) split by domain | code-structure | `45ae68249` (the DriverLoop split), `985aacd79` (the web's note codes in a scope folder) | Sonnet | BL0 | `NoteCodes.cs` off the ratchet; merged first |
| BL2 | PERF1's rest: the tidy's end judgement, the land offer's judgement (`cat-file --batch`), repeated counts when origin's line is missing | driving; `Workplace` tidy and offer, the service's session counts | `e20cc9b6f`, `604148d70` (benchmark home and budgets) | Opus | BL0 | Budgets in `Process` tests show fewer git starts per branch, with the figures in the area |
| BL3 | CONTEXTDRIVE1e's rest (`rule-holds`, the note, the decision file, `SupervisorRuleActs`, a workspace as a repository's wider place) and l (`convert-first` and the old doors deleted); SESSWATCH1a, b (no wall clock; a quiet session brought to the person, with Daoris's word) | roles-and-supervision, driving; `Roles/SupervisorRule*`, `Loop/Run/Conclude/` | `RulesStep`, `SupervisorRules.Judge` | Opus | BL0 | The seeded rules decide as today's readers did, with no `convert-first` |
| BL4 | ONEWAY1h-iii (AGENTPROVIDER1m-i part 4): the conversation's runner holds `AgentRun` | agents-and-harnesses; `Loop/Chat/ChatRunner.cs`, `ChatRun.cs`, `NativeChat.cs` | m-i parts 1 to 3 (the driven executor on `AgentRun`) | Opus | BL0 | No `ISessionAdapter` in `Loop/Chat/` |
| BL5 | GITAUTH1f (+GITREADONLY1): the session's access record with *Tighten…* and `git access --session`, the head's access facts, a read-only identity refused at the doors; ONEWAY1q (push in git access) and r (every delete through the journal) | git-and-remotes; `Workplace/GitDoors.cs`, `GitAccessRules.cs`, modules `GitRoutes.cs`, web head | the unmerged `worktree-agent-a4dce1699fabacfa0` foundation, redone as named types | Opus | BL0 | A push through a read-only identity is refused with its words; every delete goes through `GIT_REF_DELETE` |

**Wave 2.**

| Id | Rows | Area, key files | Exemplar | Model | After | Done |
|---|---|---|---|---|---|---|
| BL6 | NOCOMPAT1b2, production (+WORKFLOWCLEAN1z, FABRIC-CLOSEBIND1): `LandingRule`, `LandingKey`, `LandingRules`, `AutoLanding`, the plain bind and `landings` and `workspaceLandings` deleted (74 product files); a migration step takes the keys out; web `settings/repository/Landings.tsx` | workflows-review-landing; `Workplace/`, `Workflows/`, `Review/`, modules Trees, Lines and Work routes | the local branch `nocompat1b-landing-rules-wip` (`16681de22`), read and not merged; NOCOMPAT1b3 to b7 | Opus | BL2 | Production builds with no landing rule read, and the migration takes the keys |
| BL7 | STAFFPOLICY1d's rest (an agent's own ask and a quest's hand-over take the policy) and f's rest (`Roles/RoleUse.cs`'s moves by condition); MODELROLE1l (a seeded Design role, `opus` · high, named by a kind's route); MODELROLE1e2 (rows d and f, after checking them against D233 choice 5) | roles-and-supervision; `Roles/`, `Defaults/roles.json`, web `agents/roles/` | STAFFPOLICY1f (`RolePolicy.tsx`); `RoleSuggestion.Designer` | Opus | BL3 | A design quest starts on the Design role with no model named |
| BL8 | CONTEXTDRIVE1n (a conversation's course reads the rules); AGENTERRORS1d (the retry for intakes, helpers, opinions, partners and voices) and e (point 8's rest); HELPWIRED1b | conversations-and-context, agents-and-harnesses; `Course/`, `Loop/Chat/`, `Help/HelpWired.cs` | BL4 | Opus | BL4 | A conversation's failures counted and moved by its rules; one retry policy at every executor |
| BL9 | GITAUTH1e (the sign-in's `PhaseList` and Cancel; the sync card's screen door); GITAPP2g (push one branch on the person's press with a grant) | git-and-remotes; web-shell sign-in, `Workplace/ActRuns.cs`, `GitBranchesCommand.cs` | GITAPP2's built acts (`ActRuns.cs:67`) | Sonnet | BL5 | A push on the press, refused without a grant |
| BL10 | COMMS1h (the conversation draws each message once) + MIXAGENT3a-iii (cost per row; a helper's answer as its block) | co-working; web `work/conversation/`, modules | COMMS1 wave 1's exchanges read | Opus | BL0 | One exchange card per message, en and zh, stories at 80, 39 and 25 rem |

**Wave 3.**

| Id | Rows | Area, key files | Exemplar | Model | After | Done |
|---|---|---|---|---|---|---|
| BL11 | NOCOMPAT1b2's tests: about 80 driver test files rebound from seeded landing rules onto bound graph runs | workflows-review-landing; `Driver.Tests/Workplace`, `Workflows`, `Review` | BL6; SCOPEVERIFY1 fixes P3 and P4b (`09f9218f8`, `12246afb7`) | Sonnet | BL6 | The test projects compile with no landing rule seeded |
| BL12 | CONTEXTDRIVE1g (the rules' editors), h (the head, *Details* and *What needs you*), k (the room's and glossary's words) | roles-and-supervision; web `agents/roles/`, modules `NeedsAPerson.cs`, `AttentionStateSlice.cs`, help room | `RolePolicy.tsx`, `PlaceSupervisor.tsx` | Sonnet | BL3, BL7 | Rules edited on the page and held once on head, foot and *What needs you* |
| BL13 | ONEWAY1j: one press per go-on case through `SESSION_HAND_OVER`; `SESSION_GO_ON_NEW` and its route deleted. SESSCTX1e's fresh press joins once SESSCTX1a is measured | conversations-and-context; `Course/HandOver/`, modules `SessionsRoutes.cs`, web conversation presses | TREEGONE2's go-on (`LineCourse.Decide.cs:148-158`) | Sonnet | BL8, BL10 | One route per go-on case, on the work page and in Ask Daoris |
| BL14 | CANVAS1e and f's rest: culling and the dot layer, the map's list view, the selection-gone notice, code-map lines routed around boxes, a cross-folder cycle condensed, one folder folded alone | desktop-app; web `design/canvas/`, the maps | CANVAS1d's `PositionedCanvas` | Opus | BL0 | Each remainder seen in stories |
| BL15 | MERGETOOL1's design (+QUEUEHANG1): what the merge tool, the queue and the rehearsals each do, against the product's own landing; one way between them; run folders under `local/scratch/` bounded by age and size | code-structure; `tools/merge-branch.mjs`, `merge-queue.mjs`, `family-rehearsal.mjs` | KEEPSIZE1's keep | Opus, **D237** | BL0 | A design with build chunks, and D237 written |

**Wave 4.**

| Id | Rows | Area, key files | Exemplar | Model | After | Done |
|---|---|---|---|---|---|---|
| BL16 | CHATFLOW1g (`ChatLandCommand` lands through the run's land); PROCESS1d-iii; TREENAME1 (`TreesRoutes.cs:240`, `TreesReviewWires.cs:103`, `TreesLandDesk.cs:53`); LANDHOLD2b; FABRIC-SQUASHCHAIN1; LANDNODE1's combined-head test; INTEGRATE1p (*Send* for `start: you`) | workflows-review-landing; `Review/`, `Workplace/`, modules Trees routes, web landing | BL6 | Opus | BL11 | No in-place landing of a bound chat; each named sentence fixed |
| BL17 | COMMS1i (*Waiting on* and *Co-workers*, `sessions at-work`) + j (*What needs you* by obligation) | co-working; modules `NeedsAPerson.cs`, `AttentionStateSlice.cs`, web `work/atWork/` | BL12's attention keys | Opus | BL12 | One wait counted once in every place |
| BL18 | ONEWAY1aa: the life-flows and the workflow chart on the canvas; `design/Flow.tsx` deleted | desktop-app; web `design/`, `workflow/chart/` | BL14 | Sonnet | BL14 | `Flow.tsx` gone; stories en and zh |
| BL19 | SETUPUX1i: the place page's tree | desktop-app, workflows; web `agents/setup/` | SETUPUX1's built place pages | Sonnet | BL0 | The tree drawn and keyboard-ordered, en and zh |
| BL20 | RPC1g's design (a terminal's conversation is the running Daoris's); MAPUX1's design (the Map's UI and UX) | conversations-and-context; desktop-app | the oneway design's §3 | Opus, **D238** (RPC1g), **D239** (MAPUX1) | none | Two designs with rows, the decisions written where a choice rejected an alternative |

**Wave 5.**

| Id | Rows | Area, key files | Exemplar | Model | After | Done |
|---|---|---|---|---|---|---|
| BL21 | COMMS1k (a channel for every origin; `channel_addressed`) + SESSONE2i; COMMS1l (the steward desk's history) | co-working; service `Asks/ChannelDesk.cs`, driver `ChannelReach.cs`, web `atWork/` | COMMS1 wave 1 | Opus | BL17 | Two alongside parts post to each other, and the page draws a line's own channel |
| BL22 | FABRIC-PIPELINE1d: the `check` performed and a person's verification of a commit or branch, its verdict recorded and shown | workflows-review-landing; `Workflows/Nodes/`, `Loop/Watch/WatchNodes.cs`, a web view | the wired check executor | Opus | BL16 | A verdict on the record and on the page |
| BL23 | ONEWAY1x: a closed quest's remover, tombstoned where it travelled; closed six-character quests cleared; the legacy lookups gone | knowledge-memory-records; service `Quests/`, a migration step | `RequestsStep.cs` | Sonnet | BL0 | No legacy lookup; one migration step |
| BL24 | CALLTABLE2 (no `helper_ask` for Ask Daoris or an intake: `CallTable.cs:70`, `fixtures/call-table.json:39,43`); EVENTHOOK1g (the messaging variables removed at each entry); MCPMEM1's stale ref (answered with a fresh snapshot after a restart) | knowledge; plugins; service `Mcp/CallTable.cs`, `GitWiring.Removed`, `ServerKeeper` | the census `fixtures/call-table.json` | Sonnet | BL0 | The census pins the table to the box; each defect is fixed |
| BL25 | WIRING1d (every setting in one list: `SYSTEM_SETTINGS` and the page's catalogue); SESSWATCH1c (`quietMinutes`, the one quiet setting, in every door) | code-structure, driving; modules settings, web `settings/`, the driver's quiet read | ONEWAY1 §9 | Opus | BL3 | A setting with two keys fails the census |

**Wave 6.**

| Id | Rows | Area, key files | Exemplar | Model | After | Done |
|---|---|---|---|---|---|---|
| BL26 | ONEWAY1ab-ii (the older-shell branches, about 109 marks in 41 web files; first, a shared test shell answering `STATE_SLICE` snapshots); n-ii (the host's cards as request rows); PERSONDOOR1j checked | desktop-app; web `bridge/`, `data/`, `plugins/` | ONEWAY1ab-i | Opus | BL21 | No branch for an older shell; the pulls beside a slice gone |
| BL27 | COMMS1m (the co-working skill) and SESSCOMMIT1 in the canon, synced here and to both examples; SESSOPT1d (+FLAKE1): the evidence homes for FLAKE1, TEST1 and REH1, the backlog within budget; `docs/development.md`'s records step rewritten | doctrine-and-cli, code-structure; `canon/`, `docs/` | `.claude/knowledge/canon-authoring.md` | Sonnet | BL21 | `daoris check` clean here and in both examples |
| BL28 | ONEWAY1ac: the readers of notes from before codes and parts, their stories and driver twins gone; finished note records cleared | desktop-app; web `work/noteLines.ts`, `Note.tsx`, `attention/attention.ts` | `EventLogsStep.cs`, D142's coded notes | Sonnet | BL17 | One rule draws an unreadable record |
| BL29 | ONEWAY1ai-ii and ai-iv's rest: about 29 `daoris-driver` words in 23 driver files, `UpdateCommand.cs:21-23`, `ToolsCommand.cs:91`, the doc comments and guides; c-ii and d checked (no *actor* in the window or the room); the stale comments the areas' contradictions name | doctrine-and-cli; driver words, `docs/` | `CommandWords.cs` | Sonnet | BL13 | No taught `daoris-driver` |
| BL30 | COMMENTGATE1's gate: a changed file's comment share may not rise, and no row id, decision number or history in a comment | code-structure; `tools/` | the size ratchet's gate | Sonnet | BL0 | The gate refuses a fixture that adds a `D99` comment |

**Conditional, on the wave after their condition.**
- EVENTHOOK1b and c, after EVENTHOOK1a's measurements (Opus).
- SESSCTX1e, after SESSCTX1a (Sonnet, beside BL13's press).
- A LANDPRESS1 fix, if the reproduction names code (Opus, landing track).

**Wave 7 and 8: the sweeps and the merge tool.**
- **The sweeps (Sonnet).** One sweep per project; nothing else runs beside them. Each does COMMENTGATE1's targeted pass (D235
  point 3) and, in tests, REVIEW2-LEFT1's homes: today 547 test files root a home under the OS temp.
  - S1: the driver's product code.
  - S2: the driver's tests.
  - S3: modules and service.
  - S4: the web.
  - S5: the CLI, the devkit and the tools outside MERGETOOL1.
  - Exemplar: BL30's gate and REVIEW2's fixed test homes (`docs/2026-11-02-review2-findings.md`).
- **The merge tool.** MT1 (Opus) builds D237's first exemplar. MT2 and MT3 (Sonnet) follow it. They are built on their own branch,
  and the tool switches only after SCOPEVERIFY2, since it merges the scope.
- **Wave 7:** S1, S2, S3, S4, MT1. **Wave 8:** S5, MT2, MT3.

**Then:**
1. SCOPEVERIFY2: the full CI once on main's tip, its reds fixed together, the help-room goldens regenerated.
2. The install staged from it, and the installed batch again.
3. The owner's full code and document review.

## 5. Sizing

| Step | Chunks | Opus / Sonnet | Builder-hours |
|---|---|---|---|
| Wave 0 | 1 (two or three rounds) | 1 / 0 | 4-6 |
| Waves 1-6 | 30, plus about 6 second sittings (BL3, BL4, BL6, BL11, BL17, BL26) | 17 / 13 | 50-58 |
| Waves 7-8 | 8 | 1 / 7 | 10-14 |
| SCOPEVERIFY2 | 2 to 4 CI runs, 6 to 12 fix chunks | mostly Opus | 12-20 |
| **Total** | about 45 sittings | | **76-98** |

**Wall time.** Nine waves at about 3 hours each, then one to two days of verification: **four to five days at five wide**, if each
wave is merged as it hands back. The installed proofs run beside the waves on the staged build, and wait on Q8 and the owner's
presses.

**Risks.**
1. **The red at the end.** No chunk runs tests. SCOPEVERIFY1 took about 25 fix merges over a smaller scope. If the steward wants a
   cheaper end, one light CI after wave 4 (no Process halves) would catch drift mid-scope; this is the owner's call against D180's note.
2. **NOCOMPAT1b2 is the largest piece** (74 product files, 42 test files; the WIP branch is stale at 280 errors). It sits on the
   landing hub, so BL2, BL6, BL11, BL16 and BL22 form one serial track. A slip there slips the landing track's last wave.
3. **Shared hubs.** `NoteCodes.cs` (BL1 merges first), the en and zh catalogues (one namespace file per chunk), and the help-room
   goldens (regenerated at the end, never by hand). BL9 must leave `GitBranches.cs` to BL6.
4. **The merge tool merges the scope.** MERGETOOL1 builds on its own branch and switches after SCOPEVERIFY2.
5. **Stale rows mislead builders.** SCOPEVERIFY1, ONEWAY1, WORKFLOWCLEAN1 and SETUPUX1 say more is open than is. The `TASKS.md`
   rewrite comes before wave 1.
6. **Owner-gated work.** About 15 rows sit behind Q1 to Q9 and the frozen roles. If the owner unfreezes STEWARDROLE1, nine of them
   come back as a wave of their own.
