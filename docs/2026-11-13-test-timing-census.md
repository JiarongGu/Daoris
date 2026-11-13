# Census: where the test time goes (TESTTIME1)

A fact sheet, 2026-11-13. The owner's budget: a unit test runs in seconds, a Process-category case finishes under 2 minutes, a gate runs
under 5 minutes, a whole suite under 10. TESTDIET1's census (`2026-11-12-test-diet-census.md`) cut lines; this one counts minutes.

## 0. Where the numbers come from

Nothing was re-run for this sheet except the classes named in §5. The owner ruled out re-running the long suites, so each figure is
one of two kinds:

- **From the logs of the full run of 2026-11-12** (the gates' own logs and the `.trx` the merge tool's Process gates write):
  every Process half's per-case durations (driver, driver git half, driver pinned-git half, modules, service); the node tool
  tests' per-test durations; each .NET suite's total `Duration`; Vitest's total, and its per-file time only for the files that failed
  (a passing file prints no time in the default reporter); every failed .NET test with its duration.
- **Still to be collected by the gates** (§4 turns it on): per-test durations of the passing tests of the fast .NET halves
  (driver 20,801 cases, modules 3,409, service 3,538, devkit 148, censuses 2,062), Vitest per file and per test for the passing files,
  and the node tool tests per file. Until a merge or a CI run has written them, those rows read "not measured" and the plan's minutes
  for them are the gate totals, not a per-class split.

A case's duration in a trx is its own wall time under the load of the run (eight classes at once), so a class's sum is cost under
contention; §5 gives two classes alone.

## 1. Each suite, before and after

Gate wall time is the owner's measurement of 2026-11-12; "run" is the test runner's own `Duration`, so wall minus run is build, restore
and start-up. No suite moved by a measurable amount from §5's changes (they remove seconds from classes that run beside seven others), so
"after" equals "before" except where §5 says.

| Suite (gate) | Wall | Test run | Cases | Notes |
|---|---|---|---|---|
| driver-process | 48 min (log: 41 m 52 s) | 41 m 52 s | 1,698 | sum of case times 153 min over eight threads; 15 min of it is the `Alone` tail (§2) |
| driver-process-git | 1 h 20 m in the logged run | 1 h 20 m | 1,586 | sum of case times 309 min; 15 failed cases waited 120 s each on a bound, about 30 min of the figure |
| driver-process-git-pinned | 2 min | 2 m | 21 | fine |
| modules-process | 4 min | 3 m 57 s | 204 | fine, near the 5-minute gate budget |
| service-process | 5 min | 4 m 51 s | 3 | two benches are 282 of the 289 case-seconds |
| driver | 11 min | 11 m 15 s | 20,801 | build is small; not split by class (not measured) |
| modules | not in the table | 2 m 28 s | 3,409 | |
| service | 7 min | 5 m 36 s (Http.Tests) and 6 m 25 s (Tests), run side by side | 3,538 | |
| devkit | not in the table | 1 m 21 s | 148 | |
| censuses | 5-11 min | 1 m 56 s and 22 s | 2,062 | the rest is two builds in turn; the merge tool already reuses a build |
| tool-tests | 20 min | about 20 min | 765 | 33,000 test-seconds counting nested subtests twice; see §3 |
| cli (`npm run verify`'s node half) | part of verify | about 2 min | 803 | 135 test-seconds, 4 over 5 s |
| web-unit | 11-12 min | 11 m 31 s | 12,919 in 695 files | tests 72% of tracked time, transform 11%, setup 7%, import 7% |
| web-rules | 47 s | 47 s | 5 files | fine |
| reached | 10-21 min | not logged by test | n/a | it runs Process classes by reach; its cost is those classes' (§2) |

## 2. The driver's Process halves

**The wall is two numbers, not one.** xUnit runs a class's cases one after another and eight classes at once, so a half takes at least
the longer of (all case time / 8) and its longest class. Driver-process: 153 min of case time over eight threads is 19 min; the
`Alone` collection (33 classes: they plant environment variables, measure process handles, or hold a bound of a few seconds; each
class's own remarks say which) then runs one at a time after everything else and its cases sum to 15 min (DependencyStoreTests
279 s, SessionWatchProcessTests 204 s, LookWorkBenchProcessTests 190 s, DependencyAbsenceTests 64 s, the rest under 35 s each). Together
with contention that is the logged 42 minutes. The longest single class (SessionMessagesTickTests, 26 cases) is 628 s under load, 267 s
alone, so it is not the bound yet; it will be once the rest is cut.

**The 30 slowest classes of driver-process, case seconds under load (cases):**

| s | n | class | what costs the time |
|---|---|---|---|
| 628 | 26 | SessionMessagesTickTests | each case a real tick: a node stand-in agent started per run, a git seed copied, a stand-in service; 10 s a case alone |
| 366 | 17 | DrivenMoveDowngradeProcessTests | kill a shell at each boundary, start an older build, start this build: three node starts and a settling `Delay(1000-1500)` per step |
| 365 | 17 | GitSshProcessTests | a real ssh server stand-in per case |
| 294 | 17 | GitSshPinnedProcessTests | the same cases as the line above with a pinned git |
| 279 | 14 | DependencyStoreTests (Alone) | a real `npm ci`, a commit per case |
| 275 | 6 | StartInSyncTickTests | real ticks, 66 s a case at worst |
| 212 | 10 | GitDoorsPinnedProcessTests | the pair of the next row |
| 204 | 5 | SessionWatchProcessTests (Alone) | waits on real watcher events, up to 79 s a case |
| 196 | 95 | PluginKitTests | 2 s a case, a node plugin started per `try` |
| 194 | 7 | CheckoutLockProcessTests | child processes holding a lock |
| 192 | 5 | ChatLimitCarryProcessTests | one case failed after its 159 s bound |
| 190 | 4 | LookWorkBenchProcessTests (Alone) | a 145-second bench |
| 177 | 10 | GitDoorsProcessTests | |
| 174 | 18 | DrivenMoveCrashProcessTests | |
| 165 | 12 | ReviewTreeTests | |
| 162 | 5 | LimitGoesElsewhereProcessTests | one case failed after its 151 s bound |
| 142 | 6 | GitPushActProcessTests | a 73 s case |
| 141 | 18 | AccountRotationTickTests | |

Below those, 38 classes over 60 s hold 5,893 of the 9,172 case-seconds. Families: the three `*Move{Crash,Downgrade,OlderBuild}ProcessTests`
across six kinds (Curve, Driven, EndedLimit, HandOver, Limit, QuestHandOver) are 17 classes, 203 cases, 1,420 s; the `*TickTests`
are 46 classes, 176 cases, 1,980 s; every class named `*ProcessTests` is 136 classes, 926 cases, 5,556 s.

**The git half (driver-process-git), the 30 slowest by class** (case seconds under load): HelpTreesReadTests 835 (6 cases, one is 786 s: §5),
IntegrationPromoteGitTests 670 (42), TreePoolGitTests 661 (19), PullRequestStateTests 546 (12, one case 161 s), PoolUndoGitTests 490 (16),
TreePoolFinishGitTests 429 (20), CleanupGitTests 406 (25), KeepSizeGitTests 383 (20), IntegrationFormGitTests 377 (25), LandingTests 361 (38),
ResolveDoorGitTests 352 (14), GraphRunLandingTests 343 (18), LandBacklogLandingTests 330, KeepSizeStopsGitTests 309, StageActsGitTests 287,
TidyEffectGitTests 286, LandRebaseLandingTests 268, IntegrationSettleGitTests 265, BranchTidyingProcessTests 264, TreePoolStopsGitTests 258,
GitStreamEndsTests 242 (two cases failed at their 120 s bounds), GitHistoryEditTests 227, DevLineLandingTests 224, ContentProofLossTests 220,
TreeUpdateTests 218, ReviewGraphTodayTests 201, LandDeliveryGitTests 198, StashActsGitTests 197, JudgedDeleteGitTests 193, LineHoldsLandingTests 188.
85 classes are over 60 s and hold 15,929 of the 18,550 case-seconds. What costs: git starts, 50-150 ms each on this machine and a case makes
dozens to hundreds (a worktree open alone is about ten); a repository seed copied per case; and, in `HelpTreesReadTests`, 145 worktrees made in turn.

**The failing path, not the passing one.** In the logged run 26 cases ended at a 120-160 s bound (GitStreamEndsTests, ChatLimitCarryProcessTests,
LimitGoesElsewhereProcessTests among them). Those cases failed on something else; their time is the bound, `Poll.Patience` (two minutes), and it is
right that a test waits that long for a signal that never comes. They matter for the gate's wall time only while red.

**Modules-process** (204 cases, 200 case-seconds, 4 min): DriverModuleGitTests 36 s (15), DriverModuleAgentsTests 28 s (21), the longest
case is 14 s (`HomePlacingProcessTests`). Nothing is over the case budget. **Service-process** is three benches (181 s, 101 s, 6 s): a home of
three thousand records read through every route, and a start over kept readings; they are measurements, kept.

## 3. The node tool tests

765 tests, 33,000 test-seconds counting a parent and its subtests together (a `describe`'s time contains its cases); wall about 20 min
because `node --test` runs files at once and one file's tests run in turn. 129 tests are over 5 s, 149 over 1 s. The slow ones are all the
merge tool's and the merge queue's end-to-end tests in scratch repositories (`merge-branch.test.ts`, 6,044 lines, and `merge-queue.test.ts`, 3,459 lines),
each 400 to 1,100 s with its subtests: "the queue, end to end in a scratch repository" 1,106 s, "the tool, end to end in a scratch repository" 735 s,
INTEGRATE1b (two cases, 794 s each), QUEUEDROP1 651 s, QUEUEWAIT1 649 s, then about 25 more over 400 s. Those two files are the critical path of the
gate: nothing else in the 28 files comes near them. They belong to the merge tool (MERGEFLOW1 is changing it), so they were not touched.

## 4. What the gates now record

So that the next census needs no re-run, each gate keeps per-test durations when it runs (`daoris.gates.json`, `package.json`,
`src/Daoris.Cli/package.json`), written under the gitignored `_fixtures/test-timing/`:

- every `dotnet test` gate that is not a Process half (service, devkit, driver, modules, and each of the censuses' two commands) writes
  `<name>.trx` there; a Process half already gets its trx from the merge tool;
- `npm run test:web:unit` writes `web-unit.json` (Vitest's json reporter, every file and every test with its duration);
- `npm run tooltests` writes `tool-tests.xml` (the node junit reporter: each test, its time, its file), beside the unchanged spec report.

The merge's timing record reads these files; their durations are what §0's "still to be collected" waits for.

## 5. The time limits, the tiers and the quick wins

**No test-code quick win was proven, and none is claimed.** The candidates the brief names were searched in the sources:
- Fixed sleeps. The `Task.Delay` and `Thread.Sleep` calls of the test projects (the driver's alone name 100 ms, 200 ms, 20 ms, 50 ms and 300 ms most often) are mostly 20-500 ms; the 1-4 s ones
  are settle waits after a restart (the `*Move*ProcessTests` families, 1.0-1.5 s, three to five a case) and negative proofs
  (`ChildProcessTests` waits 4 s to show a grandchild did not outlive a kill). They are not what the classes spend: a
  `DrivenMoveDowngradeProcessTests` case is 21 s of which about 4 s is a delay. The shared `Poll.Until` already polls every 50 ms.
- Serialised collections. The 33 classes in `Alone` each say why (an environment variable, a process-wide seam, a few-second bound);
  `CategoryTests` polices the reason, so none is serialised without shared state. The lever is removing the shared thing (§7).
- Fixtures per case. `GitSeed` already copies one seed per class's case; `SessionMessagesTickTests` builds its seed per case on purpose
  (every case has its own home).
- Timeouts waited out on the passing path. None found; the long bounds are failing-path ones.
- `HelpTreesReadTests` (703 s alone, 11 m 43 s of its class's 12 m 21 s): opening the 145 trees four at a time was tried and did
  not finish inside the five-minute limit it ran under, which proves nothing about the gain, so the change was withdrawn. What
  costs is 145 `OpenAsync` calls at about 4.8 s each (about ten git starts apiece, a hook stage, a record); the plan has it.

What the branch changes instead, so the next census needs no re-run and nothing runs unbounded or unnamed:

1. **Durations are recorded** by every gate (§4).
2. **Time limits, once each, in config** (rule 22 of the code-structure area):
   - .NET: `VSTestBlameHangTimeout` in the root `Directory.Build.props`, 1 min for a half with no run-settings file and 15 min for a Process
     half (a run-settings file is how a half is known as one). A test past its limit ends the run, which says so and names the test
     (checked: the run of `HelpTreesReadTests` ended this way at a 5-minute trial limit, naming the case). Chosen from the logs'
     durations as **ceilings over what passes today**, since a lower limit would end the whole half on a case that is slower today: the
     owner's budget is 2 minutes for a Process case and 5 for a gate, and these Process cases pass above it and are the plan's first rows:
     `HelpTreesReadTests` 145 trees (703 s alone), `PullRequestStateTests` clean-up (161 s), `LookWorkBenchProcessTests` (145 s),
     `RouteBudgetProcessTests` (181 s), `StartReadingProcessTests` (101 s, under the budget but over a gate's share). Cases over 30 s
     in a fast half were seen failing in the log (`TreePoolSharesGitTests` 26 s, `GitOwnInputScopedGitTests` 17 s, under load), so
     30 s would have ended the driver's fast half; the limit there is 1 minute until the durations the gate now records say which pass.
   - Vitest: `testTimeout` and `hookTimeout` 60 s in `vite.config.ts` (it was 120 s, with no hook limit of its own). A minute, not a few
     seconds, because `WorkFrame`'s free-answer test took 8 s alone and 47 s under load on 2026-10-28; `locales/names.test.ts`'s audit
     says its own 90 s. The limit comes down to seconds once `web-unit.json` shows which files need what.
   - Node tool tests: `--test-timeout=1800000` stays (it was already there). A limit under 20 minutes ends "the queue, end to end in a
     scratch repository" (1,106 s with its subtests) and its neighbours, which are the merge tool's (MERGEFLOW1).
3. **Dependency traits.** Every Process class of the driver's and modules' test projects (about 470 classes in 459 files; the 14 files over the size budget wait for their split, which the plan's item 4 and the size
   ratchet bring, and `NeedsTraitTests` names them) carries
   `[Trait(Category.Needs, Needed.X)]` for each dependency its file names: process on every one (a Process class starts one), git
   on about 340 (it names a git seed, fixture or helper), harness on about 90 (a stand-in agent), network on about 33 (a listener or a
   stand-in host), browser on 9; filesystem is in the vocabulary and used by no Process class. This is a first tagging by what the file
   names, not a reading of each case: a class's owner refines it, and `NeedsTraitTests` holds that no Process class goes without. Vitest
   files and node tests are not yet tagged (§7).

## 6. Tests by tier

A test with no `Needs` trait is a unit test: milliseconds, no process, git, clock wait or network. By that definition the Process classes
above are the dependency tier; the unit halves are not clean. Files of each fast half that name a dependency (a text scan, so a file
may name one it only stubs; the durations the gates now record settle which):

| Half | Files with tests | Name a git helper | Start a process | A real listener | A wait of 500 ms or more |
|---|---|---|---|---|---|
| driver fast | 1,377 | 349 | 70 | 5 | 17 |
| modules fast | 302 | 14 | 12 | 4 | 3 |
| service | 222 | 1 | 7 | 1 | 9 |
| service http | 47 | 0 | 1 | 5 | 6 |
| devkit | 13 | 0 | 1 | 0 | 0 |

The driver's git figure is mostly `GitSeed`, which copies a seed folder and is a filesystem copy, not a git start; `WorkingTree.Started`
counts real starts per case and a census that counts them per class would split the 349 (§7). The clear cases, by name:
- **A wait of 500 ms or more in a fast half**, each to become an injected clock or a signal, or to move to the Process half:
  driver `AcpBackgroundTests`, `AgentSettingsInterleaveTests`, `PermissionChangeTests`, `PermissionWritersInterleaveTests`,
  `ReadShareAccountTests`, `HomeWatchTests`, `StagedBuildTests`, `HeldChatTests`, `AskDaorisReadingNeverWaitsTests`, `BrainstormBoundsTests`,
  `LookJudgesTests`, `LookStagesTests`, `MeetingTests`, `SessionsOutliveTheirLookTests`, `LedgerChangesTests`, `PersonFilesTests`,
  `SyncBoundsTests`; modules `RoleTerminalTests`, `SessionPressAllowTests`, `UpdateWatchTests`; service `CheckoutFreshnessTests`,
  `KnowledgeStandingTests`, `NewerIndexTests`, `StoreCompactionTests`, `StoredReadingTests`, `StoreJournalTests`,
  `ConnectorStartFailureTests`, `McpMemoryProposalTests`, `ReturningLockTests`; service http `ChangeStreamProcessTests`,
  `ConfirmationTests`, `InputEndStopTests`, `NewerIndexHostTests`, `KeyedHost`, `StartFailureTests`.
- **A started process in a fast half**: driver `AccountRotationTests`, `ChatAgentTests`, `CodexOwnHomeTests`, `DrivenSpawnHomeTests`,
  `EveryChildIsHandedTheToolsTests`, `HarnessSettingsTests`, `ClaudeDrivenLayerRunTests`, `LayerEnvironmentTests`, `ProvidersBuiltTests`,
  `ServerHandingTests`, `SessionServersTests`, `SpawnSkillsTests`, `SpawnTableTests`, `ToolsAtTheStartTests`, `GitGlobalFileGitTests`,
  `GitPlaceExitTests`, `ProcessStartsTests`, `PlaceHandTests` and 50 more; modules `DriverModuleGitTests`, `DriverModuleFilesTests`,
  `DriverModuleWorkflowRunTests`, `DriverModuleRunReviewFilesTests`, `EngineBrowserTests`, `ProcessBrowserChildTests`, `UpdateRelaunchGateTests`;
  service `CitationRecheckTests`, `OneCheckoutTests`, `GitForWindowsTests`, `IgnoredPathsTests`, `ConnectorStartFailureTests`;
  devkit `GitStartTests`. (A start-counting census class such as `ProcessStartsTests` may name the call without making it; each
  needs a reading.)
- **A listener**: driver `LoopbackListensTests`, `RunningServiceTests`, `DrivenAccessTests`, `AskSignatureTests`, `CodexAccountTests`; modules
  `InAppBrowserTests`, `FirstWindowTests`; service http `StartupTests`, `StartFailureTests`, `ConfirmationTests`, `NamelessAskTests`.

Vitest (695 files) and the node tests (28 files) are not scanned for tiers: a unit test there runs in jsdom or in process; the
tool tests are all dependency tests (git repositories and child node processes), the CLI's `test/` mostly so.

## 7. Thin-down candidates, and the plan for the rest, by minutes saved

Minutes are the gate's wall time, from §1 and §2's sums over the width a half runs (eight for the driver's, four for its git half).
"Split" is a file made into classes or files that run side by side; "cut" is a test removed with the rule kept elsewhere; "speed
up" is fewer processes or git starts for the same claim; "background" is the CI, which never blocks a merge.

| # | Item | Kind | Minutes |
|---|---|---|---|
| 1 | **driver-process-git** (1,586 cases, 309 case-minutes over four threads, about 80 min): count git starts per class (`WorkingTree.Started`), then give the 20 classes over 300 s a shared, once-built tree pool and fewer starts per case. `HelpTreesReadTests` alone is 12 min of a thread: build the 145 trees once from a copied worktree set, or measure the claim (one start per unchanged tree) at 30 and 145 only in the background | speed up, split | 25-40 |
| 2 | **driver-process Alone tail** (15 of 42 min): `DependencyStoreTests` and `DependencyAbsenceTests` (343 s) are alone for a process-wide install seam; make the seam per-instance so they run beside the rest. `SessionWatchProcessTests` (204 s) and `LookWorkBenchProcessTests` (190 s) are bounds and a bench; the bench goes to the background | speed up, background | 6-8 |
| 3 | **tool-tests** (20 min): `merge-branch.test.ts` (6,044 lines) and `merge-queue.test.ts` (3,459) are the gate's critical path; node runs files at once, so splitting each by theme into five or six files (the queue's per-row cases) divides their time. MERGEFLOW1's files | split | 8-10 |
| 4 | **driver-process by class**: `SessionMessagesTickTests` (26 cases, 267 s alone) and `StartInSyncTickTests`, `AccountRotationTickTests` into classes of five to eight cases, so no class is the wall and eight threads stay busy | split | 3-5 |
| 5 | **Pinned reruns** `GitDoorsPinnedProcessTests`, `GitSshPinnedProcessTests` and `GitHttpsPinnedProcessTests` re-prove the base classes under a pinned git (506 s of case time and the https pair): run them when the pin or the door code moves, in the background | background | 1-2 |
| 6 | **Move matrix**: `*Move{Crash,Downgrade,OlderBuild}ProcessTests` are 17 classes, 203 cases, 1,420 s, the same boundary list (`Cuts.All`) over six kinds. A merge runs one kind's full matrix and every kind's first and last boundary; the full matrix in the background. No rule's row is removed, only when each is run | background | 2-3 |
| 7 | **web-unit** (11.5 min, 12,919 tests): `maxWorkers` is 25% on purpose (timeouts when it was all); read `web-unit.json` for the slowest 30 files, cut what is repeated inside them (`sessionsBadge.test.tsx` 80 s and `names.test.ts` 33 s are the two slow files the log shows) | speed up | 3-5 |
| 8 | **driver fast** (11 min, 20,801 cases): read `driver.trx`; the 17 files with a wait over 500 ms and the 70 that start a process move to the Process half or take an injected clock (§6); a theory of hundreds of rows (`VerbLeasesTests` 219 inline rows, `PluginStepsTests` 116, `ToolResourcesTests` 111, `AccountRotationWalkTests` 98) is cut to the rows each rule needs | speed up, cut | 3-5 |
| 9 | **service-process benches** (`RouteBudgetProcessTests`, `StartReadingProcessTests`: 282 s of 289): measurements, not proofs; run in the background, so the gate is the 6-second case | background | 4 |
| 10 | **censuses** (5-11 min for 2.3 min of tests): two builds in turn; chain the second `dotnet test` with `--no-build`, or build both once | speed up | 2-4 |
| 11 | **service** (7 min): the 9 files with a 500 ms or longer wait get a signal or an injected clock; `Http.Tests` and `Tests` already run side by side | speed up | 1-2 |
| 12 | **reached** (10-21 min) falls as items 1, 2, 4 and 6 land: it runs the classes a change reaches, nearest first | follows | 3-6 |
| 13 | Passing cases over the budget today, one by one: `PullRequestStateTests` clean-up (161 s), `GitPushActProcessTests` (73 s), `DependencyStoreTests` (98 s), `StartInSyncTickTests` (66 s), `LandingTests` advance (74 s) | speed up | with the rows above |

The Process limit comes down from 15 to 5 minutes once items 1 and 9 land, to 2 once 13 does. The fast half's from 1 minute to 30
seconds once item 8 has the measured file times. The Vitest limit from 60 s to 10 once `web-unit.json` is read.

**Thin-down candidates read from the sources, none cut here:** the pinned reruns (item 5), the Move matrix (6), the 145-tree scale
of one case whose claim is a count per tree (1), theories of 100-219 inline rows (8). A test of a removed thing was not found beyond
TESTDIET1's list; the proofs of a live rule were left whole.

**Still to be collected by the gates** (§0): per-test durations of every fast .NET half, Vitest per file for passing files, node per
file. The next census reads `_fixtures/test-timing/` and ranks those; the items above marked "read" wait on it.

## 7. The slowest classes by suite, and the reason each has

The owner's rule (2026-11-13): a test that takes a long time has a reason, or is made faster, because slowness means an issue. The time
check (`tools/time-ratchet.mjs`) holds each listed test to `{ ms, why, row }` and prints a reason for every test over budget;
`node tools/time-ratchet.mjs --classes` prints the 15 classes of each record with the most total time and the reason or "no reason" for
each. It reads `_fixtures/test-timing/`, so the other suites' tables come from the next merge's records (TESTSLOW1); the census gate,
run alone on a branch with the shared source reading, gave (driver, 1,788 cases, 2 m 42 s of run; seconds of case time inside the gate,
which runs its cases in parallel, so a class that waits on a shared binding shows that wait):

| s | n | class | reason |
|---|---|---|---|
| 148 | 207 | ReviewGraphCensusTests | 207 combinations of a real git graph; 7 s alone, the rest is the gate's load (TESTSLOW1) |
| 134 | 52 | StructureTests | the first case pays the shared analysis of the driver's tree; listed with its reason |
| 109 | 17 | DeclarationBoundaryTests | no reason: TESTSLOW1 |
| 102 | 15 | DeclarationLiteralsCensusTests | the first case pays the shared binding of every shipping project; listed |
| 97 | 3 | ShellStructureTests | the shared analysis of the driver's tree; listed |
| 47 | 18 | ProviderCensusTests | waits on the shared binding (TESTSLOW1) |
| 47 | 3 | DeclarationKeepersCensusTests | waits on the shared binding (TESTSLOW1) |
| 43 | 517 | ProviderConformanceTests | no reason: TESTSLOW1 |
| 32 | 9 | CheckoutWritersCensusTests | 3 s alone; the gate's load (TESTSLOW1) |
| 24 | 27 | DriverModuleLinesTests (modules) | no reason: TESTSLOW1 |

The three say-door censuses and `CheckoutWritersCensusTests` read sources through `SourceTexts` and `SayDoorSources`, once a run; alone
`ContinuedReadersCensusTests` fell from 6.4 s to 0.7 s, `SayKeysCensusTests` from 7.8 s to 0.6 s and `SettledReadersCensusTests` from
10.6 s to 0.4 s. The time ratchet's list went from 8 entries with no reason to 9 entries each with one; 7 classes of this table are
over budget with no reason (TESTSLOW1).
