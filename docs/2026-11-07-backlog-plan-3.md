# The backlog's rest, planned a third time (2026-11-07)

The third plan for D240's direction: the whole of `TASKS.md` built at pace, then the code and the documents reviewed whole. The second
plan ([`2026-11-06-backlog-plan-2.md`](2026-11-06-backlog-plan-2.md)) cut BL31 to BL57; most of them are merged. This plan reads the 76
open rows again against main at `ef960feee` and puts each in one of four classes: **a** buildable now, as a chunk (§3); **b** waits only
on the staged install's proof (§5); **c** waits on a named condition (§4, §7); **d** done in fact, closed by §2's archive blocks. A row
with parts in two classes is counted by the part built first, and its tail is named.

Its rules are the second plan's: a builder takes a whole chunk, reads its area first, adds new behaviour in a unit of its own, writes its
tests and runs none, compiles clean and hands back once at about 150 tool calls (the process area's rule 10); at most five run at once;
web chunks add catalogue keys in their own namespace files; a generated contract file in conflict is regenerated, never merged by hand;
the help room's goldens are regenerated at the verification. Paths are short: the driver's under
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, the modules' under `src/Daoris.Desktop/Daoris.Desktop.Modules/`, the service's under
`src/Daoris.Service/`, the web's under `src/Daoris.Web/src/`.

**In flight when this was read.** A fix branch giving a driven start's and a conversation's spawn their own home, with a census of what
sets the test process's environment (no row): merge it before wave 1. A GITAUTH1e-f branch from 2026-11-04 is superseded by G5
(`ccd62d530`); it is the steward's to remove. AGENTLYNTAI1 1b-iii's ready parts merged at `046670144` and await the owner's acceptance.

## 1. Every open row, classified

| Row | Class | What is left | Where |
|---|---|---|---|
| SCOPEVERIFY1 | c | run 5 on main's tip once waves 1 to 3 have merged; its reds fixed together | §6 |
| COMMENTGATE1 | c | the gate, then the targeted pass | after SCOPEVERIFY1: §6 |
| MERGETOOL1 | c | MT1 to MT8, FR1 to FR3; MT9 | after a green CI: §6 |
| KNOWCENTRAL2 | b | 2i | I-3 |
| HOMELAYOUT5 | c | each harness's home moved and measured | the owner's sign-in per harness (Q6) |
| SESSWATCH1 | a | 1b's rest: the row's *told* line, `LongCalls`' look folded into the watcher's | BL78 |
| SESSWATCH1d | c | quiet episodes recorded | two weeks of driven work on the install |
| SESSWATCH1e | c | the automatic end where 1d allows | 1d's D200 note: BL62 |
| SESSCTX1 | c | 1e, *Go on fresh* as a hand-over to the same agent | SESSCTX1a measured (I-6): BL61 |
| RPC1g | b | g5 (the register entry is moot: no register is kept, D232) | I-4 |
| AGENTPROVIDER1 | a, b tail | l (BL71); t's rest (BL72); r, COAGENT1e (BL82); h's `containment.not-widened` and t's proof (I-6); f is built, its suites run whole at the verification | BL71, BL72, BL82 |
| AGENTPROVIDER1j | c | each protocol door's own extensions | AGENTLYNTAI1e-iv: BL68+ |
| AGENTLYNTAI1 | c, b tail | the moves after 1b-iii; s-ii and 1a's installed proof | §7 conditions 1, 2; I-1, I-6 |
| AGENTLYNTAI1a | d | — | §2 |
| AGENTLYNTAI1b-i | d | — | §2 |
| AGENTLYNTAI1b-ii | d | — | §2 |
| AGENTLYNTAI1b-iii | c | LQ16 (AG41, AG42 whole), LQ11 (AG40), LQ3's opening half (AG39) | §7 conditions 1, 2: BL68 |
| AGENTLYNTAI1c | d | — | §2 |
| AGENTLYNTAI1d | c | Claude Code's driven sessions on the layer | s-ii measured, AG39 to AG42, 1b-iii's R-d parts |
| AGENTLYNTAI1e-i | c | dsh on the layer | 1b-iii's part, AG39 |
| AGENTLYNTAI1e-ii | c | `codex-acp` and `acp-stub` | as e-i, and AG38, AG40, AG41 |
| AGENTLYNTAI1e-iii | c | `claude-code-acp` | e-ii |
| AGENTLYNTAI1e-iv | c | a plugin's protocol door; `AcpSession` goes | e-iii |
| AGENTLYNTAI1f-i | c | Claude Code's conversations | 1d, AG39 |
| AGENTLYNTAI1f-ii | c | the native stand-ins; the native transport retires | f-i, AG45 |
| AGENTERRORS1 | c | 1c's measured `--resume`, then 1e | I-6: BL65 |
| LIMITRESUME1 | b | f with TOOL4n: one native session to its limit | I-6 |
| BGWAIT2b | b | the probe, once | I-6 |
| HELPMODE1a3 | b | full access, SESSCTX1a, FABRIC-C2b | I-6 |
| CODEXTOOLS1 | b | a fresh and a resumed Codex quest | I-6 |
| MODELROLE1c4 | b | c4 and d3 | I-6 |
| COAGENT1 | a | b2's rest; a quest's hand-over waits for what its part left running | BL75 |
| STAFFPOLICY1 | b, c tail | i (d's and f's rests are built, `4086dce47`, `2eb0186db`; e3 went with CONTEXTDRIVE1); h parked behind its measurements | I-6 |
| MODELROLE1e2 | d | — | §2 |
| MODELROLE1l | d | — | §2 |
| MODELROLE1i | c | an exact build on its own actor | a records batch measuring D160's share: BL63 |
| MODELROLE1j | c | a scout's orientation as a start | i: BL63 |
| MODELROLE1k | c | cool-offs scoped to a model | SWITCHMODEL1's read of the per-model weeks: BL63 |
| CONTEXTDRIVE1 | b, c tail | l's and m's installed proof; j waits on the supervisor's activation (SUPERVISOR1a-iii, parked) | I-6 |
| TERMTRUST1 | b | part (i), §8 case 4 on every door, activation, case 14 | I-4 |
| CONFIRMWAIT1 | b | a real restart and an hour's wait | I-4 |
| CHATFLOW1 | b | the installed look | I-2 |
| FABRIC-MILESTONE1c | b | the milestone on the window | I-2 |
| FABRIC-SQUASHCHAIN1 | a | the keeper's proof across passes; a pass to `monitor` and `tidy` end to end; four web readers checked | BL77 |
| NODEKIT1 | b | the installed look | I-2 |
| SETUPUX1 | b | x and its fixes | I-2 |
| WORKSETUP1 | c | G10, then the first pull request and its tail | Q5 |
| INTEGRATE1 | b, c tail | k and h on the install; m is Q4; i parked | I-1 |
| FABRIC-PIPELINE1 | a, b and c tails | `failed: ask`, the sessions named in the fix ask, its terminal verb, `paths` (BL73); g on the install (I-1); h after FABRIC-PERFORM1l | BL73 |
| FABRIC2 and LANDNODE1 | b | WORKFLOWEDIT1c and LANDNODE1j, the installed window | I-2 |
| REVIEWFLOW1 and LINESTOGETHER1 | b | REVIEWFLOW1c; LINESTOGETHER1e parked | I-2 |
| COMMS1 | b | n with SESSONE1c-look | I-5 |
| COWORKUX1 | a, c tail | the terminal's `--after`, `--to-me`, `sessions pass`; Ask Daoris's `session` cards; three dialog doors; two glossary words; i waits on Q9 | BL81 |
| CHANNELJOIN1 | b | the installed look | I-2 |
| MSG1 | b | h, i spend; j with COMMS1n | I-5, I-6 |
| STEER3 | b | two measurements | I-6 |
| COWORKVIEW1 | b | a's rest; f parked | I-3 |
| GITDESIGN1 | a, c tail | G9's leftovers named in the git area (BL79); G10 is Q5; G8-CONVERT1 waits on a home past G8 (BL66) | BL79 |
| GITAUTH1 | a, b and c tails | b's rest and e's advice words (BL79); f's look and h (I-2); c and g's real host with G10 | BL79 |
| GITAPP2 | a | Stash Drop's terminal twin; a race test for its store-back | BL80 |
| GITAPP1 | d | — | §2 |
| CANVAS1 | b | g | I-2 |
| MAPUX1 | b | g | I-2 |
| FLOWCHART2 | b | h | I-2 |
| AGENTSPLACE1 | b | 1l | I-2 |
| LANDPRESS1 | b | the press reproduced; a fix if it names code (BL67) | I-1 |
| PERF1 | a, b tail | the plans' reads, a landing's line commit and head, the stash plan's second read (BL80); start-up in the window (I-3) | BL80 |
| MCPMEM1 | b | a real long chat | I-3 |
| EVENTHOOK1 | a, b and c tails | 1b's rest (BL70); 1c's proofs (BL76); 1h (I-5); steps 2 and 3 across UAC on a machine with UAC on | BL70, BL76 |
| EVENTHOOK1d | a | `Settled` through the shell's pipe; each source told end to end | BL76 |
| EVENTHOOK1e | c | 1e-i measured, then 1e-ii or 1e-iii | 1h's two weeks: BL85 |
| EVENTHOOK1f | c | Daoris's example copy removed | Daoris.Plugins' Accept: BL83 |
| ONEWAY1 | a, b and c tails | r's rest (BL74, then BL84); x's census and y (I-1); j after SESSCTX1a; w with HOMELAYOUT5; ai's comments in the sweeps | BL74 |
| WIRING1e | b | the installed window | I-2 |
| DOGFOOD1 | b | rows through `dev-line` on the install | I-5 |
| OPTIMIZE1 | a, c tail | b's rest (BL82); a's rest and d read from SCOPEVERIFY1's CI | BL82 |

**Counts:** a 14, b 31, c 24, d 7: 76.

## 2. Closes: the archive blocks, ready to apply

Each block is appended to `docs/task-archive.md` and its row removed from `TASKS.md`; the AGENTLYNTAI1 row's clauses on 1a, 1b-i,
1b-ii and 1c shorten to one line each.

```markdown
## AGENTLYNTAI1a, 1b-i, 1b-ii and 1c (2026-11-07)

- [x] **AGENTLYNTAI1a** the desktop builds on Lyntai's agent layer and nothing changes; **1b-i** the layer's adapter, the fourth view
  and the oracle; **1b-ii** `alt-stub`, a third stand-in, by its folder alone; **1c** Claude Code's and Codex's account windows read
  through the layer, as before (driver; D213, D240 point 8, D248). → `docs/2026-10-30-agent-lyntai-design.md` §9.2, §12

  **Outcome** 2026-11-07: all four accepted by the owner. 1a (`6935a7865`): the reference holds only where this machine's local Lyntai
  build is in use, a build without it is today's driver, the closure restores offline; its installed proof (the published app started
  with feeds unreachable) stays on AGENTLYNTAI1. 1b-i (`8bbd98df0`, `9a2de82e5`, `50c20a25a`): the typed access failure, the host policy
  on Daoris's own types, `Agents/Layer/`'s ingress, translations, containers, `LauncherRunner`, `LayerRun` and layer view, the suite's
  fourth view and the parity oracle, on the tests' provider. 1b-ii: met by AP-H's folder (`1415b9626`); its own-vocabulary cases wait
  on 1f-ii and AGENTPROVIDER1j. 1c (`78167b30d`, `55e372916`): `IUsageRead` read through the layer where the local build is in use,
  today's reader the read without it; Claude Code's bound to the layer's read again once Lyntai read a null-id model.

## MODELROLE1e2 and MODELROLE1l (2026-11-07)

- [x] **MODELROLE1e2** what MODELROLE1e left: row d's hand-over and row f's editor; **MODELROLE1l** design and decision work starts on
  its own role (driver, service, web; D205). → `docs/2026-10-23-actors-design.md`

  **Outcome** 2026-11-07: e2 built with BL7 (`395b875ad`): a hand-over keeps its model and effort, a handed start takes the role's
  choice, and the four fenced roles are edited in the one role editor (D233 choice 5); its partner case goes with COAGENT1. l built
  (`a94212935`): Design is a seeded role on `opus` · high that design work takes with no model named. Both installed proofs are in the
  installed batch.

## GITAPP1 (2026-11-07)

- [x] **GITAPP1 — Daoris's git as comprehensive as a git management app, and Review fast** (design; D202). → `docs/2026-10-23-git-app-design.md`

  **Outcome** 2026-11-07: part one built (D202). Part two was measured as OPTIMIZE1a (BL54, `3453472de`): a review asks two or three
  object questions of its 23 git starts, so a held reader is not worth adding and none is built.
```

**Stale text found on the way** (the steward's, with the closes):

- `TASKS.md`: RPC1g's "register entry once ONEWAY1ad-i's register lands" (no register is kept, D232); ONEWAY1's h-iii and WIRING1d
  (built, BL4 and BL25); STAFFPOLICY1's d's, e3's and f's rests (built); AGENTPROVIDER1's "f the s-ix runs alone" (f is built; its
  suites run whole at the verification).
- `docs/areas/agents-and-harnesses.md`, the layer paragraph: Claude Code's usage "stays on today's reader" (bound again, `55e372916`).
- `docs/areas/desktop-app.md`, OPTIMIZE1b: "D249 awaits the owner's acceptance" (accepted).
- `docs/areas/plugins.md`, EVENTHOOK1b's rest: "the driver's own conversations … told by their marks" (D244's note stopped telling
  them; the row's rest is the kit's entry and an ending's adapter).
- `docs/areas/roles-and-supervision.md`: the MODELROLE1l/e2 line goes with the close; STAFFPOLICY1's `HandsOver` line is checked by BL75.
- `docs/areas/workflows-review-landing.md`: the GITAPP1 part two line goes with the close.

## 3. Build chunks (a)

Ids start at BL70, clear of the second plan's BL58 to BL68, which keep their meaning in §4 and §6. No chunk takes a decision number; one
that must reject an alternative asks the parent for one.

### 3.1 The chunks

| Id | Rows | Files | Exemplar on main | Proof | Model | Not beside |
|---|---|---|---|---|---|---|
| **BL70** | EVENTHOOK1b's rest | driver `Plugins/PluginKit.cs`, `Plugins/HookPeer.cs`, `Plugins/PluginsCommand.cs` (`Cost`), `Plugins/WorkEventTeller.cs`, `Knowledge/Domain/PluginVocabulary.cs`, `Knowledge/Room/HelpRoomMakingAPlugin.cs`; the kit's sample and template; web plugins page, `locales/*/plugins*.json` | an existing point's kit entry in `HookPoints.All`; `8776c18db` (1b's wiring) | `work/event` in `HookPoints.All` with its sample (`run: "sample"`), the template's answer, the trial's case, `Cost`, Ask Daoris's sentence and the page's gloss in en and zh (`PluginKitTests`, `HelpRoomMakingAPluginTests`); an ending's adapter by the event-hook design §9 | Opus | anything in `Plugins/`; BL76 |
| **BL71** | AGENTPROVIDER1l | driver `Participants/ChildLineage.cs`, `Participants/LineageWiring.cs`, `Participants/Domain/ChildBindings.cs`, `Agents/LineageStreams.cs`, `Loop/Session/LineageBrackets.cs`, `Loop/Run/Hold/SessionHold.cs` and `Loop/Chat/ChatWatch.cs` (composition only), the readings in `Agents/Contract/` | `Loop/Session/NativeLimits.cs` (a sink part both compose); AP-C `18b2e5b48` (readings on one sink) | `ChildSeen` and `ChildEnded` readings from both transports, a process-token stamp on readings, the run's lineage writer replaced by a sink part; the AP design §12's l line: each fact read once, a fixture session's lineage byte for byte; the lineage classes unchanged | Opus | `Participants/`, `Loop/Run/Hold/`, `Loop/Chat/` (BL75, BL78) |
| **BL72** | AGENTPROVIDER1t's rest; SCOPEVERIFY1's quick line | `Driver.Tests/fixtures/provider-vocabulary.json`, `fixtures/provider-census.json`; the censuses `Driver.Tests/Agents/ProviderCensusTests.cs`, `Service.Tests/ProviderCensusTests.cs`, web `test/providerCensus.test.ts`, `src/Daoris.Cli/tool-test/provider-census.test.ts`; `tools/contracts.mjs`; root `package.json` (`quick`); the fixture's mention in `tools/merge-branch.mjs` and its test | NODEKIT1 3 (`84c1d0782`) with FIX9's move of `NODE_KINDS` into the generated service file | a vocabulary table generated from the registrations, every census reading it, the hand-kept fixture gone; each record naming an old seam's name read by today's names or its reader named with its end (D232); `quick` writes the ignored generated files before `contracts.mjs --check`, green in a fresh worktree | Opus | `tools/contracts.mjs`, `package.json`, generated contract files, `tools/merge-branch.mjs` |
| **BL73** | FABRIC-PIPELINE1's rest | driver `Workflows/VerifyFixAsks.cs`, `Workflows/VerifyRuns.cs`, the workflow command; the modules' route for the say-so; web the place's *Your part* (`workflow/said.ts`) and the workflow catalogue; root `daoris.gates.json` (`paths`) | BL45 (FABRIC-PIPELINE1f and j: *Ask for a fix…*, one ask per gate) | the pipeline design §6 and §7: `failed: ask` publishes with no press only where the person set it; the fix ask names the sessions whose landings carried the batch; a terminal verb for the ask; this repository's gates declare `paths` and a batch runs only the gates its paths reach | Opus | `daoris.gates.json` (COMMENTGATE1, MERGETOOL1, the verification), `Workflows/Kinds/Check/` |
| **BL74** | ONEWAY1r's rest | driver `Workplace/ContentHolds.cs` (`KeepDiscardedAsync`) and its callers (`TreeWorkReads.cs`, `IntegrationSettles.cs`, `TreeTidies.cs`, `TreeSweeps.cs`, `TreeRemovals.cs`, `LandedWithdrawals.cs`, `Workflows/Kinds/Tidy/TidyDoor.cs`), `Workplace/RecoveryRetention.cs`, `Workplace/RecoveryDropping.cs`, `Workplace/DeletedBranches.cs`; web the git page's deletes | G7 and BL53 (`DeletedBranches`, `GIT_DELETES`: a delete's journal record and its Undo) | each door's recovery ref kept through the journal's act and dated from it; a census counting refs under `refs/daoris/discarded/` with no journal record (read on the install, I-1); the old reader and press stay until it reads zero (BL84) | Opus | `Workplace/`, `Workflows/Kinds/Tidy/` (BL77, BL80) |
| **BL75** | COAGENT1b2's rest and a quest's hand-over; MODELROLE1e2's partner case; STAFFPOLICY1's `HandsOver` check | driver `Participants/Partners/`, `Participants/Domain/PartnerPieces.cs`, the landing doors (`Workflows/Kinds/Land/`, `Workplace/Landing*`), `Course/HandOver/`, the quest session's hand-over in `Loop/`; service `Sessions/SessionLedger.cs` | BL46 (`HandOverLeftProcessTests`), BL47 (`PartnerLandingTests`) | the landing doors refuse a piece's record; the not-taken-in verdict where the lead's tree is gone; retention through tidy, clear and sync; the partner's real-process case; a quest's hand-over waits for what its part left running, its carry-on at a later look; a conversation's `HandsOver` takes the arranged order, or the roles area's line is rewritten | Opus | `Participants/`, `Course/HandOver/`, `Workflows/Kinds/Land/`, `Loop/Chat/ChatRunner.cs` |
| **BL76** | EVENTHOOK1c's proofs; EVENTHOOK1d's rest | `Driver.Tests/Plugins/` (the relay), the modules' host tests, the example plugin's `node --test` | `6f14c0a7b` (EVENTHOOK1c's tests) | the lease's wait on another act's `what` through the host; an unwritable lease and a `bin` entry at both places; a staged update swapping `app/` while a relay runs; two `--where` at once; a version manager's shim; `drive --once` starting the plugin into its job; `Settled` through the shell's pipe and each source told end to end; a gap found is fixed in the chunk | Sonnet | `Plugins/` (BL70 first) |
| **BL77** | FABRIC-SQUASHCHAIN1's rest | the tests of `Workplace/EndKeeping.cs`, `Workflows/Kinds/Monitor/`, `Workflows/Kinds/Tidy/`; web `workflow/current.ts` (`KNOWN_KINDS`), `workflow/graphFlow.ts`, `workflow/runSaid.ts`, `workflow/chart/stepKinds.ts` | NODEKIT1 1c (`monitor` and `tidy` in their folders), `TidyGitCountTests` | a tidied part ended by the record with equal git starts across passes; a run's pass reaching `monitor` and `tidy` end to end; each web reader of the built-in `pull-request` kind reads `NODE_KINDS` or goes | Sonnet | BL74 (Tidy), BL80 (the count classes) |
| **BL78** | SESSWATCH1b's rest | driver `Review/Heavy/LongCalls.cs`, `Loop/Run/Conclude/QuietWord.cs` and the watcher; web `work/rail/SessionRow.tsx`, the work catalogue | BL35 (`884594d51`) | the session row's *told* line in en and zh stories; `LongCalls`' look folded into the watcher's, its tests moved onto it | Sonnet | `Loop/Run/`, `Review/Heavy/` |
| **BL79** | GITAUTH1b's and e's rests; GITDESIGN1's G9 leftovers | `Driver.Tests/Workplace/PinnedMinGit.cs`, `GitHttpsProcessTests`; web `locales/*/settings.sync.json` (`notFetched.ssh`, `.https`); the tools page's fixtures, stories, tests and its two `why` words; the glossary's `gh` and `az` line; `Driver.Tests/fixtures/tools-text.json` and the plugin tool checks | G6 (`370f5e57a`); G9's tool removal | which TLS backend the pinned MinGit trusts a test certificate through measured, `GitHttpsProcessTests` run on it or b closed with why in the git area; the advice names an identity (Settings → Git), not a key or a credential helper; no `gh` or `az` left as a sample tool | Sonnet | `src/Daoris.Web/src/locales/glossary.json` (BL81) |
| **BL80** | PERF1's rest; GITAPP2's rest | driver `Workplace/` (the plans' reads, a landing's line reads, the stash plan, `StashDrops.cs`, `StashDropSteps.cs`), the count classes (`TidyGitCountTests`, `OfferGitCountTests`, the landings'), the terminal's git verbs; the modules' git verbs | `5dd5f9828` (PERF1's cuts), `22e6070e0` (Stash Drop) | the plans' reads and a landing's line commit and head read once where no other process can move them, else kept with why; the stash plan's second `refs/stash` read gone; budgets lowered; Stash Drop's terminal twin; a race in which git names another commit stores it back | Opus | `Workplace/` (BL74, BL77) |
| **BL81** | COWORKUX1's rest but i | driver `Participants/Helpers/SessionsHelperCommand.cs`, `WorkOrders/SessionsCommand.cs`, `Help/Proposals/HelpSessionProposals.cs`; web `work/atWork/` and its dialogs; the glossary; the catalogues | BL48 (`ae4c96c37`) | `sessions helper --after --to-me`, `sessions pass`; Ask Daoris's `session` cards for the person's helper doors; *Write a piece*, *Second opinion*, *Work alongside*; *co-worker* and *lead* in the glossary; en and zh; `names:check -- --strict` | Sonnet | the glossary (BL79); `Participants/` (BL75) |
| **BL82** | OPTIMIZE1b's rest; SCOPEVERIFY1's `StatePublisherTests` case; AGENTPROVIDER1r (COAGENT1e); CONTEXTDRIVE1's owed coverage rows | `Driver.Tests/Platform/ProcessStartsCensusTests.cs`, `Modules.Tests/Platform/ProcessStartsCensusTests.cs` and the tests' direct starts; a reader of the census; `Modules.Tests/Platform/StatePublisherTests.cs`; `tools/family-rehearsal.mjs`, its Stub Two phase only; the help coverage test the hooks' rows go in | `ca22ddb95` (OPTIMIZE1b, D249); AP-R `953296828` (the Stub Two declaration) | every test start named with its purpose or why; the census read by a log line or a door; a refused call and a read publishing nothing, on a route that exists; the Stub Two partner exchange in the rehearsal (run at the verification); the coverage rows written or the area's note dropped | Sonnet | MERGETOOL1's FR chunks (after the verification) |

### 3.2 The waves

Each wave's five touch disjoint files. A wave starts when its slots free, not when the whole wave before it has merged, except where
"not beside" names an earlier chunk, which merges first.

| Wave | Chunks | Why first |
|---|---|---|
| **1** | BL70, BL71, BL72, BL73, BL74 (Opus, all five) | BL70 starts EVENTHOOK1's longest chain (1h on the install, then 1e's two weeks); BL71 and BL72 close AGENTPROVIDER1's buildable rest, the seam Lyntai's moves build over, and BL72 makes `quick` pass in every fresh worktree; BL73 comes before INTEGRATE1k's flip, which runs this repository's pipeline; BL74 starts the census whose zero ends ONEWAY1r |
| **2** | BL75 (Opus); BL76, BL77, BL78, BL79 (Sonnet) | COAGENT1's last code; the proofs after BL70; the small rests on files wave 1 has left |
| **3** | BL80 (Opus); BL81, BL82 (Sonnet); two slots | the slots take a second sitting of BL71, BL72 or BL75 if one hands back unfinished, else a conditional chunk whose condition has come (§4) |

Then SCOPEVERIFY1 (§6). About 13 chunks and two second sittings, 7 Opus and 6 Sonnet: three waves of about three hours each at five wide.

## 4. Conditional chunks, each when its condition comes

| Id | Rows | Condition | Model |
|---|---|---|---|
| BL61 | SESSCTX1e, with ONEWAY1j's *Go on fresh* | SESSCTX1a measured (I-6) | Sonnet |
| BL62 | SESSWATCH1e | 1d's D200 note naming the cells | Opus |
| BL63 | MODELROLE1i, j; MODELROLE1k | the records batch; SWITCHMODEL1's read of the per-model weeks | Opus |
| BL64 | COAGENT1d, then COWORKUX1i | Q9 files it | Opus |
| BL65 | AGENTERRORS1e | 1c's measured `--resume` (I-6) | Opus |
| BL66 | G8-CONVERT1: `PushedGraphs` and the `push-steps` step deleted | the install's home has started on a build after G8 | Sonnet |
| BL67 | a LANDPRESS1 fix | the reproduction names code (I-1) | Opus |
| BL68+ | AGENTLYNTAI1b-iii's rest, then 1d, 1e-i to e-iv, 1f-i, 1f-ii, AGENTPROVIDER1j, one move at a time, each accepted before the next | §7 conditions 1 and 2, then each move's needs in §1 | Opus for each ⚖ move, Sonnet for 1b-iii's parts |
| BL83 | EVENTHOOK1f: Daoris's example copy of `event-relay` removed | Daoris.Plugins lands `plugins/event-relay/` by its person's Accept | Sonnet |
| BL84 | ONEWAY1r's end: `RecoveryRetention`, `RecoveryDropping` and their callers' own refs deleted (ONEWAY1af) | BL74's census reads zero on the install | Sonnet |
| BL85 | EVENTHOOK1e: 1e-i's probe, then 1e-ii or 1e-iii | 1h's two weeks of use | Opus |
| — | FABRIC-PIPELINE1h, a stage after the check | FABRIC-PERFORM1l, in WORKSETUP1's tail | Opus |
| — | WORKSETUP1's tail: AUTOTIDY1c, SQUASHTIDY1d, PLUGHOOK1d, FABRIC-PERFORM1l | the first pull request (G10, Q5) | per row |

## 5. The installed batch (b)

Run on the build staged from SCOPEVERIFY1's green run, the app closed and restarted rather than staged under running sessions, in this
order. Each step that spends runs once at the least that proves it, its spend reported (D240 point 7).

| Sitting | Row | Its steps |
|---|---|---|
| **I-1** landing and integration (the steward) | LANDPRESS1 | press *Land* on a finished driven session in the kit's repository, 0 behind; it lands or is refused in words; if dropped, read `bridge.asked`, the driver's log and `trees land --plan`, and file BL67 |
| | INTEGRATE1 | k: flip this repository's integrator and the merge in place's flag (ONEWAY1y) from the terminal; h: a landed line integrated by the fabric workflow (integration design §17, §18) |
| | FABRIC-PIPELINE1 | g: this repository's pipeline on the install, a failed gate making its fix ask (after BL73) |
| | ONEWAY1, AGENTLYNTAI1 | x's install census; BL74's census of discarded refs (releases BL84); 1a's proof: the published app started with feeds unreachable |
| **I-2** the window (1546 and 680 px, en and zh, both themes) | SETUPUX1 | x: the place page's tree and kept selection, its sections lit; each fix filed as found |
| | FLOWCHART2, CANVAS1, MAPUX1 | compose and read a workflow in the editor; folds, routing and culled cards; the Map's tiers, *Back to the map*, the Tab order on both levels |
| | NODEKIT1 | the picker's kinds; a person's task pressed *Done*; an approval pressed *Approve* and *Reject* with a reason; the monitor's outcomes worded |
| | FABRIC2 and LANDNODE1 | WORKFLOWEDIT1c: a workflow composed and run with decisions, roles and defaults; LANDNODE1j: a performed `land` |
| | CHATFLOW1, FABRIC-MILESTONE1c, REVIEWFLOW1 | a conversation's work in its own tree runs its repository's workflow; the milestone on the window; REVIEWFLOW1c, a session's steps read as one flow |
| | AGENTSPLACE1 | 1l: the Agents place's pages with their Roles, Workflow and Sessions sections |
| | CHANNELJOIN1 | an outside agent joins a line's channel under the Confirm, reads *outside Daoris*, is removed; a quiet member expires |
| | WIRING1e | a real PATH put on and removed; a changed address after a restart; *Open folder* on each row; the copied line pasted into each shell |
| | GITAUTH1 (tail) | f's look: a session's *Git access* fact and *Tighten…*; h, the owner's look |
| | MODELROLE1l (closed) | a design quest starts on Design |
| **I-3** measures, no spend | PERF1 | start-up in the window, measured against its budget |
| | MCPMEM1 | a real long chat: the browser server held under 512 MB and restarted between calls |
| | KNOWCENTRAL2 | 2i: the coordinator measured, repeated or conflicting entries about one fact looked for |
| | COWORKVIEW1 | a's rest: rule 1's measure, the stories measured in a browser, the person's first use (22) |
| **I-4** the terminal | CONFIRMWAIT1 | a terminal's request waits across a real restart and an hour |
| | TERMTRUST1 | 1a's part (i), §8 case 4 on every door, then activation and case 14 |
| | RPC1g | g5: `daoris chat` held by the running Daoris, refused with none, Ctrl+C at rest lets go |
| **I-5** co-working | COMMS1, MSG1 | COMMS1n with SESSONE1c-look; MSG1j, the message canary |
| | EVENTHOOK1 | 1h: *Install from a folder*, the switch for `event-relay`, a steward session woken by events (after BL70 and BL76; starts 1e's two weeks) |
| | DOGFOOD1 | from here on, rows through `dev-line`, each measured against a builder's run, the figures kept in `docs/` |
| **I-6** the spend sitting (Q8's cap) | AGENTLYNTAI1 | s-ii first, since 1d waits on it: a driven Claude Code start denies or keeps the three tools a headless turn cannot answer |
| | HELPMODE1a3 | full access on the installed pins; SESSCTX1a, resume and a fresh part on each door (releases BL61); FABRIC-C2b, the two-harness canary |
| | AGENTPROVIDER1 | h: `containment.not-widened`, a door's process born in the agents' job; t: a Codex and a dsh session start as before without Claude Code's seam (s-v) and with no other provider's credential (s-vi), one resume each, and one on the native door |
| | LIMITRESUME1, AGENTERRORS1 | one native session run to its limit: f with TOOL4n (the account cooled, no strike) and 1c's measured `--resume` (releases BL65) |
| | CODEXTOOLS1 | a fresh and a resumed Codex quest whose first turn uses the connector |
| | MODELROLE1c4 | c4: a Build whose first model one account lacks runs on the other, a silent turn's refusal passes with no strike; d3: a chat's model by its composer and in *Details* across a restart, a refused model going on in one press |
| | STAFFPOLICY1 | i with LIMITGOON1's look: Build set to a spread, one real move to another choice seen with its reading |
| | CONTEXTDRIVE1 | l and m: the rules' acts on the install, *Details*' Supervisor line and counts |
| | BGWAIT2b, STEER3, MSG1 | the woken-turn probe once; Codex's `turn/steer` and Claude Code's stream-json input; MSG1h and i, next-step words on Codex and the native door |

## 6. The verifications and the sweeps

| Step | Where it sits | What it needs |
|---|---|---|
| **SCOPEVERIFY1** | after waves 1 to 3 have merged | `quick` green in a fresh checkout (BL72); run 5 of the full CI on main's tip; its reds fixed together in Opus fix chunks; the help room's goldens regenerated (`DAORIS_GOLDEN_WRITE`); the ssh door's push relay proven by the process suites; BL82's `StatePublisherTests` case; AGENTPROVIDER1f's suites whole and the deployment rehearsal; OPTIMIZE1a's other git test classes counted beside a `Process` run and d's wall time read from the run. The install is staged from the green run and §5 runs on it |
| **COMMENTGATE1** | beside the installed batch, after SCOPEVERIFY1 is green | **BL58** first (Sonnet; exemplar `tools/size-ratchet.mjs`): a changed file's comment share may not rise and no row id, decision number or history note is accepted in a comment; proof the gate refusing a fixture that adds a `D99` comment. Then the sweeps, Sonnet, one per project and nothing else in that project beside it: **S1** the driver's product code, **S2** its tests, **S3** the modules and the service, **S4** the web, **S5** the CLI, the devkit and the tools outside MERGETOOL1. Each is COMMENTGATE1's targeted pass and ONEWAY1ai's rest (about 100 comments spelling `daoris driver …`, each to its group). S5 waits until MT8 has merged, or skips the merge tool's files |
| **MERGETOOL1** | built beside the batch on their own branches; merged one at a time on main after a green CI (SCOPEVERIFY1), in the design's order (`2026-11-05-merge-tool-design.md` §4) | MT1 (Opus) then MT2 to MT5, MT7, MT8 (Sonnet) and MT6 (Opus), each proven by `npm run tooltests` and one `--ci` with the merged tool before the next merges; MT6 and MT7 change behaviour, each after a green CI on the moved tool, MT7's first removal on the main checkout after the parent reads `--prune --plan`; FR1 (Opus), FR2, FR3 (Sonnet) beside the MT chunks, after BL82's Stub Two phase has merged; MT9 waits on INTEGRATE1k's flip and Q3 |
| **SCOPEVERIFY2** | after the sweeps, the MT and FR chunks and any conditional chunk merged by then | one light CI; the install staged again; the batch's steps that a later chunk changed run again; then the owner's review of the code and the documents |

## 7. The conditions (c) waits on

1. **Lyntai's person accepts AG38 to AG45** on its own line (built on its session's branch, quest `237ea250317c`). It opens 1b-iii's
   rest and, with 1s-ii, AGENTLYNTAI1d.
2. **The owner accepts 1b-iii's ready parts** (`046670144`); each move lands and is accepted before the next.
3. **Daoris.Plugins' person accepts `plugins/event-relay/`** (quest `5dfde9903578`): BL83.
4. **The owner's questions still standing from the second plan** where unanswered: Q3 (INTEGRATE1i before the flip, MT9), Q4
   (INTEGRATE1m), Q5 (G10: the work host's SSH identity, one API token, the Apply over the 29 repositories, which opens WORKSETUP1),
   Q6 (HOMELAYOUT5's sitting, which decides ONEWAY1w), Q8 (the spend sitting's cap) and Q9 (COAGENT1d: BL64 or retire it with
   COWORKUX1i).
5. **Measurements on the install:** SESSCTX1a, 1s-ii and AGENTERRORS1c's `--resume` (I-6); about two weeks of driven work for
   SESSWATCH1d; 1h's two weeks for EVENTHOOK1e; MODELROLE1i's records batch and SWITCHMODEL1's read; a home past G8 for BL66; BL74's
   census at zero for BL84; EVENTHOOK1c's steps 2 and 3 on a machine with UAC on.
6. **Parked rows they wait on:** CONTEXTDRIVE1j on SUPERVISOR1a-iii; INTEGRATE1i and LINESTOGETHER1e with STEWARDROLE1d; COWORKVIEW1f
   on BRAINSTORM1d and AGENTROOM1b; STAFFPOLICY1h on its measurements.
