# Test scope plan: where the test time goes, and the cuts that take it back

Written 2026-11-17 by the steward's planning agent, read-only, from main at `6e1cc5e23c`. Nothing was run but measuring
commands over records; no suite, merge or build was started. Every figure names its record. Figures from a run beside other
runs are load figures (code-structure rule 17), labelled so.

Records read:

- the queue's candidate runs of today, `local/scratch/scope-92590ea/` (07:14 to 07:47) and `scope-b34c44d/` (07:54 to 08:18), their
  trx files and logs, `local/scope-tree/_fixtures/test-timing/web-unit.json`, and `local/scope-runs.jsonl`;
- `local/merge-record.jsonl` (91 merge attempts, 2026-11-13 to 11-16) and the `reached.log` of the 44 merges since 11-15 that kept one;
- the last complete Process-half trx files, the full set of 2026-11-12 (`local/scratch/full-b739ed7/`), and the CI of 11-16
  (`local/scratch/ci-fd9baed/status.json`, its `modules-process.trx`);
- `_fixtures/test-timing/` in the main checkout (`censuses-driver.trx` of 11-16, `tool-tests.xml` of 11-15, the `reached-*` records),
  `node tools/time-ratchet.mjs --classes`;
- `local/heavy-runs.jsonl`, `local/metrics/landings.jsonl`, `tools/layers-census.json`;
- `git log --first-parent main --since=2026-11-03` for which driver subdomains each landing touched, and the driver's sources for a
  type-reference census between its subdomains.

## 1. Where the time goes

### 1.1 The three clocks a builder waits on

| Clock | Baseline | Record |
|---|---|---|
| A merge's fast tier (`merge-branch.mjs`) | wall median **12.8 min**, p90 28.8, max 55.2 (n=91); 38 of 91 attempts passed, 36 first time | merge record, 11-13 to 11-16 |
| its `reached` gate | median **9.5 min**, p90 23.8; over its budget in 70 of 83 runs | same |
| a queue candidate (the scope run: every fast gate whole, then the Process heads) | wall 8 to 76 min; today 33 and 24 min; 45 to 110 gate-minutes each | `scope-runs.jsonl`, the `scope-*` folders |
| the heavy run after a landing (background) | median 12.8 min, p90 40.5 (55 ended, 49 green) | `heavy-runs.jsonl` |
| the full set | 3 h 0 min, red (11-12); the CI of 11-16 killed seven gates at their 10-minute budget | FULLSET1, `ci-fd9baed/status.json` |
| a builder | median 41 min a row (p90 62); 75 to 90% inside builds and tests | `landings.jsonl` (n=28); teamwork design §1 |

On 11-15, 25 landings took 39 merge attempts and 533 minutes of merge wall: the merge is a serial queue, so its minutes are every
builder's minutes.

**The `reached` gate's time is mostly not tests.** Of the 44 merges since 11-15 whose `reached.log` survives, the gate took 407 minutes;
the test runners' own reported durations (dotnet's `Duration`, Vitest's, `node --test`'s `duration_ms`) sum to 130 minutes (**32%**).
The other **68%** (median 6.5 min a merge, 7.0 where a .NET group ran) is restore and build of up to three test projects, test discovery
over a 21,000-case assembly once per `dotnet test` (two to six a merge), the plan's census read (about a minute, MERGEFLOW1b), runner
start-up and re-runs alone. The split between those is not recorded; REACHCOST1 records it first.

**Why merges fail:** of 53 failed attempts, the failing gate was `reached` 29 times, the time ratchet 17, `cli` 16, `censuses` 8,
`web-rules` 3, `test-map` 2, the rest once each (a run can fail on several). The time ratchet's 17 are the timing breaches the owner's
rule now asks to explain (§2, TIMEWHY1).

### 1.2 Wall time per gate

Minutes. Merge: median (p90) from the merge record. Queue: today's two candidate runs. CI: the 11-16 run, where "killed" is the
10-minute budget.

| Gate | Merge | Queue 92590ea / b34c44d | CI 11-16 |
|---|---|---|---|
| universal | 0.8 (1.8) | 0.7 / 1.0 | 2.1 |
| code-map | 0.1 (0.2) | 0.2 / 0.3 | 0.3 |
| test-map | 0.8 (1.3) | 0.8 / 1.2 | 0.9 |
| cli (`verify:checks`) | 2.2 (6.6) | 8.3 red / 10.0 red | 1.5 red |
| web-types, web-rules | 1.2 (2.3), 0.8 (1.5) | kept | kept |
| reached | 9.5 (23.8) | 0.8 / 1.1 (nothing reached) | 2.2 |
| censuses | 2.7 (4.4), when reached (50 of 91) | 4.4 / 0.8 ended | kept |
| time-ratchet | 0.0 (3.0), max 8.2 | | |
| cli-tests | | 3.8 / 4.3 | 5.8 |
| tool-tests | | 15.3 red / 15.0 red, over budget | killed |
| comments | | 7.8 / 3.2 | 0.2 |
| service | | 5.8 / kept | 6.1 |
| service-process | | 2.3 ended | 11.2 killed |
| devkit | | 3.6 / kept | 0.8 |
| layers | | 5.2 ended (13.6 on 11-16) | killed |
| adapters-lock | | 0.2 / 0.6 | 2.5 red; each adapter suite 5 to 20 s |
| driver (unit half) | | 8.9 red / 15.8 red | 8.2 red |
| modules (unit half) | | 7.1 / kept | 2.5 |
| web-unit | | 10.2 red / 6.6 | killed |
| driver-process, driver-rehearsals, driver-process-git | | not reached | each killed |
| driver-process-git-pinned, modules-process | | | 2.4, 8.2 (memory) |

### 1.3 Per test project, and the Process against unit split

| Project, half | Cases | Classes | Case-min | Run wall | Record |
|---|---|---|---|---|---|
| Driver.Tests unit half | 21,427 | 1,485 | 90.3 | 6.5 min (gate 8.9) | today 07:30, quiet-ish |
| the same, loaded | 21,861 | 1,535 | 134.8 | 12.3 min (gate 15.8) | today 08:12 |
| Driver.Tests Process half | 1,698 | 252 | 152.9 | 42.0 min | 11-12 full set |
| Driver.Tests git half | 1,586 | 203 | 309.2 | 81.0 min (45 min on 11-14 after moves) | 11-12; TESTSLOW1 |
| Driver.Tests pinned git | 21 | 6 | 2.0 | 2.1 min | 11-12 |
| Driver.Rehearsals | 747 listed | 74 | not recorded | killed at 10 min | LAYERS1 §8 (11-14); CI 11-16 |
| Modules.Tests unit / Process | 3,556 / 204 | 319 / 33 | 5.0 / 5.3 | 5.5 / 6.3 min | today / 11-16 |
| Service (both projects) unit / Process | 3,099 / 38 | 237 / 9 | 19.9 / 1.8 | 5.0 / 0.7 min | today |
| Devkit | 148 | 14 | 5.7 | 3.4 min (`DocsGateMergeTests` 194 s) | today |
| driver censuses / modules censuses | 1,789 / 276 | 91 / 18 | 7.4 / 0.3 | 1.6 / 0.4 min | 11-16 |
| web unit (Vitest) | 13,150 in 742 files | | 29 | 6.3 min quiet, 9.9 loaded | today |
| tool tests (`node --test`) | 710 | 28 files | `merge-queue.test.ts` 104, the seven `merge-branch-e2e-*` 115 | 15 min, over budget | 11-15; today |
| tool unit (Vitest) | 326 | 43 files | 0.2 | | today |

**The driver's split:** the Process halves are 13% of its cases and **84%** of its case time (464 of 554 case-min, 11-12), and run
125 minutes one after another against the unit half's 6.5. The unit half is itself a slow tail: 93 cases of 10 s or more are 51% of its
case time, 107 classes of 10 s or more are 78%, and single cases of 224 s, 201 s and 117 s set its floor.

**The web:** `design/stories.test.tsx` is 160 s alone (3,483 tests in one file, one worker); the 30 slowest files are 56% of the time,
612 of 742 files run under 2 s. By area: work 448 s, design 214, app 203, workflow 150, agents 142, settings 128, projects 122.

### 1.4 The driver test project by the production subdomain it tests

Test files and traits are counted from the sources (a file's `[Trait]` lines); case-minutes from today's unit trx and the 11-12 Process
trx (a class mapped to the folder that declares it). The git half's classes since moved to `Driver.Rehearsals` are 215 case-min, 63 of
its 75 files under `Workplace/`.

| Subdomain | Test files | Process files | Needs git | Needs process | Needs harness | Unit case-min | Process case-min | Git-half case-min |
|---|---|---|---|---|---|---|---|---|
| Workplace | 355 | 141 | 136 | 141 | 0 | 19.0 | 44.7 | 82.5 (+ most of 215 moved) |
| Agents | 250 | 35 | 11 | 33 | 2 | 11.9 | 6.5 | 0 |
| Workflows | 201 | 17 | 16 | 17 | 1 | 9.9 | 0.3 | 7.0 |
| Loop | 167 | 82 | 61 | 77 | 47 | 11.7 | 51.6 | 0.1 |
| Course | 100 | 31 | 7 | 30 | 24 | 11.5 | 28.7 | 0 |
| WorkOrders | 98 | 5 | 3 | 4 | 0 | 1.8 | 0.3 | 1.0 |
| Plugins | 76 | 13 | 6 | 12 | 1 | 1.4 | 5.2 | 0 |
| Roles | 67 | 0 | 0 | 0 | 0 | 5.2 | 0 | 0 |
| Platform | 63 | 10 | 2 | 10 | 0 | 2.1 | 0.8 | 0 |
| Review | 59 | 14 | 10 | 13 | 1 | 1.1 | 7.8 | 0 |
| Participants | 55 | 3 | 2 | 3 | 0 | 0.6 | 0 | 0.2 |
| Install | 54 | 2 | 1 | 2 | 0 | 5.0 | 0 | 0 |
| Knowledge | 48 | 2 | 2 | 2 | 0 | 0.4 | 1.0 | 0 |
| (root) | 40 | 11 | 5 | 7 | 1 | 1.2 | 0.5 | 0 |
| Settings | 38 | 1 | 0 | 1 | 0 | 0.7 | 0 | 0 |
| Service | 31 | 4 | 0 | 3 | 0 | 0.2 | 0 | 0.6 |
| Events | 30 | 2 | 1 | 2 | 1 | 1.8 | 3.5 | 0 |
| Help | 28 | 5 | 0 | 5 | 5 | 1.6 | 0.4 | 0 |
| Setup | 26 | 3 | 3 | 3 | 0 | 0.3 | 0.4 | 1.5 |
| Departments | 22 | 7 | 2 | 7 | 0 | 0.2 | 0.2 | 0.5 |
| Context, Store, Sessions, Instance, Trees, Conversations, Quests | 17, 13, 10, 8, 5, 2, 1 | 0, 2, 1, 1, 3, 1, 1 | | | | 2.3 together | 0.4 | 0.6 |
| **Total** | **1,864** | **397** | **273** | **381** | **84** | 90.3 | 152.9 | 309.2 |

Beside the traits: network 28 files, browser 6, `Parallel=Git` 139, census 91. **What starts real git or a process:** every Process
file (397; the rehearsals' 75 besides); and, untagged in the unit half, 54 classes whose sources start git or a process (50 of them the
driver's), among them `TreePoolSharesGitTests` (one 224 s case, a pool over real git and links), `TreeSharesTests` (214 s),
`ClosedRunEndTests` (one 201 s case, `NodeRunFixture`), `ReviewCommitFilesTests`, `ReviewOutputsTests`. The layers census allows 93 process
starts in 63 files and 240 git sites in 129 files of the application and unit-test projects, and 1,983 wall-clock reads in 830 files.

### 1.5 The 30 slowest classes, and what makes each slow

Case-seconds under the run's own load. P = driver Process half and G = git half (11-12 full set), U = driver unit half (today).
"Now" is where the class lives at `6e1cc5e23c`.

| # | s | Cases | Half | Class | Now | What costs |
|---|---|---|---|---|---|---|
| 1 | 835 | 6 | G | HelpTreesReadTests | Rehearsals | 145 worktrees opened in turn, about ten git starts each; one case 786 s |
| 2 | 670 | 42 | G | IntegrationPromoteGitTests | Rehearsals | merges and promotions over real repositories |
| 3 | 661 | 19 | G | TreePoolGitTests | Rehearsals | pool slots opened over real git |
| 4 | 628 | 26 | P | SessionMessagesTickTests | Driver.Tests/Loop | real ticks: 30 to 70 git starts, a node stand-in agent and a loopback service each |
| 5 | 546 | 12 | G | PullRequestStateTests | Rehearsals | real git; a clean-up case 161 s |
| 6 | 490 | 16 | G | PoolUndoGitTests | Rehearsals | the journaled ref act over real git |
| 7 | 429 | 20 | G | TreePoolFinishGitTests | Rehearsals | real git (a memory twin, `TreePoolFinishMemoryTests`, exists) |
| 8 | 406 | 25 | G | CleanupGitTests | Rehearsals | real git |
| 9 | 383 | 20 | G | KeepSizeGitTests | Rehearsals | real git |
| 10 | 377 | 25 | G | IntegrationFormGitTests | Rehearsals | three-way merges and replays over real git |
| 11 | 366 | 17 | P | DrivenMoveDowngradeProcessTests | Driver.Tests/Course | kill a shell, start an older build, start this one: three node starts and two loop incarnations a step |
| 12 | 365 | 17 | P | GitSshProcessTests | Driver.Tests/Workplace | real clone, fetch, push over a real ssh client (network, git) |
| 13 | 361 | 38 | G | LandingTests | Rehearsals | real landings |
| 14 | 352 | 14 | G | ResolveDoorGitTests | Rehearsals | real git |
| 15 | 343 | 18 | G | GraphRunLandingTests | Rehearsals | real landings |
| 16 | 330 | 15 | G | LandBacklogLandingTests | Rehearsals | real landings |
| 17 | 309 | 16 | G | KeepSizeStopsGitTests | Rehearsals | real git |
| 18 | 294 | 17 | P | GitSshPinnedProcessTests | Driver.Tests/Workplace | row 12's cases again on the pinned git |
| 19 | 287 | 19 | G | StageActsGitTests | Rehearsals | real git |
| 20 | 286 | 13 | G | TidyEffectGitTests | Rehearsals | real git |
| 21 | 279 | 14 | P | DependencyStoreTests | Driver.Tests/Workplace (Alone) | a real `npm ci` and a commit per case |
| 22 | 275 | 6 | P | StartInSyncTickTests | Driver.Tests/Loop | two real ticks a case |
| 23 | 268 | 12 | G | LandRebaseLandingTests | Rehearsals | real rebases |
| 24 | 265 | 13 | G | IntegrationSettleGitTests | Rehearsals | real git |
| 25 | 264 | 14 | G | BranchTidyingProcessTests | Rehearsals | real git |
| 26 | 258 | 12 | G | TreePoolStopsGitTests | Rehearsals | real git (memory twin exists) |
| 27 | 236 | 11 | U | LineAccountTests | Driver.Tests/Agents | a grid with a scratch home built per case; no git |
| 28 | 224 | 1 | U | TreePoolSharesGitTests | Driver.Tests/Workplace | **untagged real git** in the unit half |
| 29 | 214 | 5 | U | TreeSharesTests | Driver.Tests/Workplace | **untagged real git** (a seed and seven git calls) |
| 30 | 212 | 10 | P | GitDoorsPinnedProcessTests | Driver.Tests/Workplace | the doors' cases again on the pinned git |

Just below: `SessionWatchProcessTests` 204 s (real watcher events, Alone), `ClosedRunEndTests` 202 s (unit half, untagged real git), and
the whole families: the `*TickTests` are 46 classes and 1,980 s, the Move matrix (`*Move{Crash,Downgrade,OlderBuild}ProcessTests`) 17
classes and 1,420 s (TESTTIME1 census).

**The unit halves' tests of a second or more, classed by what their source names** (driver, modules, service; a text reading by
precedence, process or git first, which TIMEWHY1's runtime ledger will replace): 367 classes, 1,029 cases, 5,439 s.

| Cause | Classes | Cases of 1 s or more | Seconds | Share |
|---|---|---|---|---|
| a wall-clock read or wait (`Task.Delay`, `Thread.Sleep`, `DateTime.UtcNow`) | 97 | 340 | 1,451 | 27% |
| starts a process or real git | 54 | 128 | 1,398 | 26% |
| none of the named causes: CPU, a grid, files under scratch, or load | 97 | 244 | 1,305 | 24% |
| a real store file or a file watcher | 71 | 236 | 960 | 18% |
| a source scan | 43 | 75 | 302 | 6% |
| a listener or pipe | 5 | 6 | 21 | 0% |

### 1.6 The censuses

The driver's 91 census classes are 1,789 cases and 442 case-seconds, 94 s of run (`censuses-driver.trx`, 11-16); the modules' 18 are 276
cases and 20 s. Their gate costs 2.7 min a merge (two builds and two runs), and the same 91 classes run again inside the driver's unit
half (456 case-s, 8% of it). The heaviest: `ReviewGraphCensusTests` 75 s (207 real git graphs), `StructureTests` 23 s (the driver
tree's analysis), `ProviderConformanceTests` 19 s (517 cases), `DeclarationBoundaryTests` 18 s, `ProviderCensusTests` 17 s. Every merge
also pays `test-map` (0.8) and `universal` (0.8), and the queue pays `layers` (5 to 14 min: it measures each slow unit class again
alone), which the CI killed at 10.

Since CENSUSDIFF1 the census classes leave the fast halves (`Category!=Process&Category!=Census`) and the CI's cover of the gate by them
goes; a merge names its changed paths in `CENSUS_SCOPE`, and a census marked diff-able judges only those. `ReviewGraphCensusTests` and
`ProviderConformanceTests` judge behaviour, not the sources, and left the category for the fast half (`CensusTraitTests.Behaviour`).

### 1.7 How far a component boundary would reach

`StructureTests` binds every driver file through Roslyn and holds the 24 subdomains of `fixtures/structure.json` in order with
`"edges": []`: no file names a type of a later subdomain. A text census of type references between subdomains, written for this plan,
agrees: 5,055 references downward, 40 upward (name collisions). The order is a DAG, so the driver can be cut into packages along it.

But a cut alone narrows little. Of the 636 first-parent landings on main since 11-03 that touched the driver, the lowest subdomain each
touched was Workplace 92, Agents 80, Platform 76, Service 60, Events 59, Workflows 39, Knowledge 38, Settings 30, Roles 21, Plugins 20,
Store 19, Instance 18, Course 17, WorkOrders 14, Install 12, Loop 9, the rest 1 to 7. Taking every subdomain above the lowest touched as
reached, a landing reaches on average **76%** of the unit half's case time and **79%** of the Process halves'. A package split saves
mostly build (a body change below a cut leaves the reference assembly unchanged, and nothing above recompiles), not test selection,
which class-level reach (`merge/class-reach.ts`) already does more finely.

## 2. The rule for slow tests, and the cuts

### 2.1 The owner's rule, as this plan applies it

A test or gate over its time has a known cause, and most tests are fast unit tests. A slow test usually means a package separation is
due; a fast one that still times out is reviewed for separation too. So:

1. **A breach report (TIMEWHY1).** Every time breach (the time ratchet, a gate killed at its budget, a blame-hang timeout, the layers
   census's one-second budget) is classed by what the test did in that run, each class with its fix path:

   | Cause | Read from | Fix path |
   |---|---|---|
   | starts a process or real git | the test host's start ledger (git funnel, process funnel, fixture spawns) | move it to the adapter's package (Machine, Git) with a twin-proved fake; the class stays real only in that package's suite or a rehearsal |
   | touches the disk beyond its scratch folder, or a real store file | file opens outside `ScratchFolders`' root; store opens | `MemoryUnitOfWork` (LAYERS1 b) for the store; the file's real guarantee to the adapter's suite |
   | the network | a listener, socket or pipe opened | `RoutedHandler` or `MemoryServiceDoors`; Network adapter (LAYERS1 g) |
   | sleeps or waits on a wall clock | a delay or poll on `TimeProvider.System` | `MovedClock` (LAYERS1 c) or a signal |
   | a full source scan | a compilation or source walk in the test | scope the scan to the diff at a merge, the whole tree in the background (CENSUSDIFF1) |
   | only load | TIMELOAD1's count of concurrent heavy runs; the class under its limit alone | nothing; TIMELOAD1 keeps it from failing a merge |
   | none of these, alone over its limit | CPU, a grid | cut to the rows each rule needs (TESTDIET1), or a heavy domain into its own package |

   Where several classes of one domain share a cause, the report names the domain as a separation candidate (a package of its own
   with its ports), not each class.
2. **A census that enforces it (UNITPURE1).** A test outside the Process category starts no process, opens no git and waits on no
   wall clock; the census goes red naming the test and what it did, so a slow unit test cannot be added silently.
3. **Over budget means a separation candidate, and the cuts are ordered by it.** The candidate list is TIMEWHY1's report; until it
   runs, §1.5's table is the order: Workplace's git (about 520 case-min across the git half, its moved rehearsals and the Process
   half), Loop's ticks (52 Process and 12 unit case-min), Course's Move matrix (29 and 12), then the unit halves' tail by cause.

### 2.2 The cuts, ranked by minutes saved per builder-day

**The model.** A builder-day is six landings (25 landings on 11-15 by about four builders at once; a row's builder median 41 min).
Per landing a builder runs its hand-back `reached-checks --base main` (about the merge's 9.5 min) and about three focused .NET runs (6
min each, the teamwork census), and the queue runs 1.6 merge attempts (39 for 25 on 11-15) of 12.8 min, then a 12.8 min heavy run in the
background. That is about 290 blocking test minutes a builder-day, plus about 77 of background machine time; the machine at 91% CPU
doubles every check (teamwork design §1), so machine minutes come back as blocking ones. Each cut below says blocking minutes saved,
and machine minutes in brackets. Estimates; each row's proof measures its own.

| Rank | Cut | Saves a builder-day | Size | Rows |
|---|---|---|---|---|
| 1 | (f) the `reached` gate spends its time on tests | about 50 (+ the queue's) | M | REACHCOST1 |
| 2 | (d) the unit halves' slow tail moved by cause, and kept out | about 30 (+20) | L in M waves | UNITPURE1, TESTSLOW1 |
| 3 | (f) time breaches explained, load never fails a merge | about 20 | M | TIMEWHY1, TIMELOAD1 |
| 4 | (c) censuses judge the diff at a merge | about 12 (+10) | M | CENSUSDIFF1, CENSUSSCAN1 |
| 5 | (f) the `cli` gate's steps by reach | about 10 | S | CLIREACH1 |
| 6 | (a) LAYERS1 e and f, the git port with its memory fake | about 10 (+40), and the full set's git half | XL in rows | LAYERS1 |
| 7 | (b) the driver's components as packages, Loop first | 10 to 25, unproven until its first cut measures | XL in rows | COMPONENT1 |
| 8 | (a, b) Loop's tick on ports | about 5 (+15), and 30 min of the full set | L | LOOPPORTS1 |
| 9 | (e) the web's unit tests by area and catalogue | about 5 (+5) | M | WEBREACH1 |
| 10 | (f) the Move matrix narrowed | about 0 (+10), and 15 min of the full set | S | MOVEMATRIX1 |
| 11 | (f) the merge tool's end-to-end tests | about 3 (+10 a queue run) | M | MERGETS1 |

**1. REACHCOST1, the `reached` gate spends its time on tests (f).**
What changes: the gate records a clock per step (plan, restore, each project's build, discovery, each run, the re-runs alone) into the
merge record; each tree's test projects are built once and every group and later gate runs `--no-build --no-restore` (rule 22 says so
for gates; `reached`'s groups each call `dotnet test` and so each build and discover); a project's reached classes run in one
`dotnet test` with the list in a generated run-settings filter, not in 120-class command chunks; the plan's census read is cached by
tree hash; the light gates run beside `reached`'s build, not before it. Tests that move or go: none. Selection: unchanged. What proves
nothing was lost: the same `reached-classes` counts (ran, replayed) for the same plan, before and after, on five replayed merges. Saves
about 3.5 of the 6.5 non-test minutes on each of about 16 reached-style runs a builder-day (six hand-backs, ten merge attempts). Depends
on nothing; closes MERGEFLOW1b's measure.

**2. UNITPURE1 then TESTSLOW1's waves, the unit tail by cause (d).**
What changes: UNITPURE1 first, so nothing new joins the tail; then TESTSLOW1 moves the 367 classes of §1.5's second table by cause, in
waves: store files to `MemoryUnitOfWork` (71 classes, 960 s: `EndedLimitMoveStoreTests` 170 s, `IntegrationStoreTests`, the
`*MoveStoreTests`), wall-clock waits to `MovedClock` or a signal (97, 1,451 s: `SessionsOutliveTheirLookTests` 143 s, `ChatGoOnTests` 101 s,
`StagedBuildTests` 97 s, `AcpBackgroundTests` 84 s), the untagged real git and processes to `ScriptedGit` or `MemoryRepository`, or
tagged Process and so out of the unit tier (54, 1,398 s: `TreePoolSharesGitTests`, `TreeSharesTests`, `ClosedRunEndTests`), and the grids
cut to the rows each rule needs with TESTDIET1 (`LineAccountTests` 236 s, `CurveMoveTests` 88 s, `RuleFileFirstFormCorpusTests` 81 s).
Domain logic is tested in memory; a real dependency is tested only in its adapter's suite or a rehearsal. Selection: unchanged; each
reached .NET run is shorter. Proof nothing was lost: each moved class keeps its assertions on the fake, and where the fake is
new its contract runs on both sides (§5); a class tagged Process is still reached by the heavy tier. Saves about 0.9 min on each reached
.NET run and about 1 min on each focused run; the unit gates in queue runs fall from 9 to 16 min to about 4 to 6. Depends on LAYERS1 b
and c (built) and UNITPURE1.

**3. TIMEWHY1 with TIMELOAD1, time breaches explained (f).**
What changes: §2.1's report, written at every merge and queue run, and read by the time ratchet so a breach whose only cause is load is
named and never fails the merge (TIMELOAD1), while every other breach names its cause, its fix path and its separation candidate. Saves
the failed attempts the time ratchet causes (17 of 53) and their fix rounds. Proof: planted breaches, one per cause and one under load.
Depends on nothing; its runtime ledger is shared with UNITPURE1.

**4. CENSUSDIFF1, censuses judge the diff at a merge (c).**
What changes: the semantic censuses bind one compilation and judge only the syntax trees the diff changed and the files that name a
changed declaration; the whole tree is judged in the heavy run after landing and in the full set; the census classes stop running a
second time inside the driver's unit half; the layers census's measure-alone moves to the heavy run. Selection: a merge's census set is
unchanged, each census reads less. Proof: a planted breach in a changed file is red at the merge; one planted in an unchanged file is
red in the heavy run; the full-tree and diff readings agree on a week of main's commits. Saves about 2 of the gate's 2.7 min on half
the merges, and the census classes' 8% of every driver unit run. Depends on CENSUSSCAN1 (one scan a run).

**5. CLIREACH1, the `cli` gate by reach (f).**
`verify:checks` is 2.2 min median, 6.6 p90, 8 to 10 min in today's queue runs and red 16 times: each of its steps (the CLI's and the
tools' typechecks, the doctrine check, contracts, the document checks) declares what it reads, and a merge runs a step only where a
path reaches it, the whole set staying in `npm run quick` and the CI. Proof: every step's reach declared and checked by `test-map`, a
planted break in each step's input red at a merge. Depends on nothing.

**6. LAYERS1 e and f, the git port with an in-memory fake (a).**
What changes: e makes `GitRuns` the `IGit` port with `ScriptedGit`; f moves the families' git asks to `IGitRepository` on
`MemoryRepository`, in order of their case-minutes: the pool's rest (undo, finish, stops: 490, 429, 258 s), Integration (promote, form,
settle: 670, 377, 265), Landing (landing, graph run, backlog, rebase, dev line: 361, 343, 330, 268, 224), Cleanup and keep size (406,
383, 309), Review's graph, Help's trees (835: the claim is one start per unchanged tree, provable on memory at 145 and real at 3).
Tests move: each family's scenario classes run on `GitWorld` with a memory backend in the unit tier; the twin run on real git and the
classes that prove git itself go to the git adapter's suite, run when its fingerprint moves. A merge's selection: unchanged (the git
half is already the heavy tier's); the heavy run and the full set lose most of their git half. Proof: `RepositoryContract` on both sides
for every ask a family moves (22 cases today), each moved family's real twin green once, and the git half's case count falling to the
adapter's. Saves little blocking time (builders working in Workplace run its Process classes at 12.4 s a case) and most of the heavy
run's and the full set's git minutes (45 to 81). Depends on d (built).

**7. COMPONENT1, the driver's components as packages, Loop first (b).**
The components, from `structure.json`'s order and §1.7's census:

| Component | Subdomains | Sources | Boundary below it |
|---|---|---|---|
| Kernel | Platform, Store, Settings, Service, Install, Instance, Events | 399 | the BCL, Ports, Contracts |
| Agents | Knowledge, Agents | 510 | Kernel |
| Workplace | Workplace | 616 | Kernel, Agents; git through `IGit` and `IGitRepository` |
| Work | Departments, Roles, Participants, Context, Course, WorkOrders, Review | 703 | the three above |
| Flows | Plugins, Workflows, Setup, Transitional | 682 | the four above |
| Loop | Help, Loop | 236 | everything; nothing names Loop (a `StructureTests` rule already) |

What changes, in steps each kept or retired by its measure: (a) Loop's tests as their own project, `Daoris.Desktop.Driver.Loop.Tests`
(176 files, 2.4 of the 20.5 MB of driver test code); (b) Loop's sources as their own assembly over the driver, its access through a friend
declaration, and every project built with reference assemblies so a body change below Loop does not recompile Loop or its tests and a
Loop change does not recompile the driver's 2,900 other files or its 18 MB of tests; (c) Kernel from the bottom the same way, which 43%
of landings touch and 57% never do; (d) a fingerprint verdict per component test project, only if class replay (REACHREPLAY1) leaves a
measured gap. Selection: a component's tests run where class reach reaches them, plus its boundary contract cases where a component it
names changed its reference assembly. Proof: `StructureTests` and a census of cross-component references (§5) hold the boundary; the
whole build and every test green after each step. Saves build minutes on every .NET run whose change sits above a cut; §1.7 says the
test-selection gain is small. Depends on REACHCOST1 (to measure build against tests), and for Loop's tests to be fast, LOOPPORTS1.

**8. LOOPPORTS1, Loop's tick on ports (a, b).**
The 46 `*TickTests` classes (1,980 s; `SessionMessagesTickTests` 628 s, `StartInSyncTickTests` 275 s) prove the loop's decisions through
real ticks: 30 to 70 git starts, a node stand-in agent and a loopback service each. The tick's git asks go to the repository port
(GITTICK3 counts them), its agent to the scripted provider over `IProcessHost`, its service to `MemoryServiceDoors`; each class's
decisions become unit cases; one real tick per family stays as a rehearsal. Proof: each family's real tick green once, its unit cases
asserting what the tick classes asserted. Depends on LAYERS1 f's tick families, d, g.

**9. WEBREACH1, the web's unit tests by area and catalogue (e).**
A merge reached 537 to 624 web files on wide landings and ran 80 (the bound); locales were touched by 272 of 864 landings since 11-03, and
a catalogue change reaches every component that imports a word. A catalogue is data: its change reaches the parity suites (`i18n`,
`catalogueKeys`, `glossary`, `names` and the rest), not each importer; `stories.test.tsx` (160 s, one file) splits by story group; the
area suites run in the shards of the whole run. Proof: a planted wrong word red through the parity suites; the whole run's wall under 5
minutes quiet (6.3 today). Depends on nothing.

**10. MOVEMATRIX1, the Move matrix narrowed (f).**
17 classes and 203 cases over the same boundary list for six kinds (1,420 s): the heavy tier runs one kind's whole matrix and every
kind's first and last boundary, the whole matrix in the full set (the timing census's item 6). No rule's row is removed. Proof: the
full set still runs every row; a planted break at a middle boundary of a kind not chosen is red there.

**11. MERGETS1's end-to-end cases (f).**
The tool tests' gate is 15 min and over budget in both of today's queue runs: `merge-queue.test.ts` (161 cases, 6,253 case-s) and seven
`merge-branch-e2e-*` files (6,900 case-s) drive the whole tool in scratch repositories. MERGETS1's steps move each behaviour to a typed
unit test and keep a few end-to-end cases on seeded fixtures; this plan only orders them by those files' minutes. TOOLTIER1 has
since moved those specs, the queue's split into six `merge-queue-*.e2e.ts`, to a gate of their own, `tool-e2e`, in the Process lane.

## 3. Targets, baselines and how they are measured

| Target | Baseline | Measured by |
|---|---|---|
| A merge's fast tier under 10 min | wall median 12.8, p90 28.8 (n=91, 11-13 to 11-16); `reached` 9.5 median, 68% of it not test execution; first pass 36 of 91 | `local/merge-record.jsonl`, `wallMs` median and p90 over the week's merges, with REACHCOST1's step clocks; first-pass rate beside it; `tools/dev-metrics.mjs` |
| One component's tests under 3 min | no component exists. Nearest: the driver's unit half 6.5 min of run and 8.9 of gate quiet (15.8 loaded), modules 5.5 and 7.1, service 5.0 and 5.8, devkit 3.4 and 3.6; Loop's unit classes 85, 861 cases, 11.7 case-min, its slowest 143 s | the component's test gate alone on a quiet machine (rule 17), build included, from its trx and the gate's record |
| The full set under an hour | 3 h 0 min, red (11-12): git half 1 h 20, driver Process half 42 min, deployment rehearsal about 35, family rehearsal about 28, unit half 11; the 11-16 CI killed seven gates at 10 min | `node tools/merge-branch.mjs --full` record's wall, replays counted apart (FULLSET1) |

Reaching them: the merge needs REACHCOST1 (about -3.5 min), the unit tail (-0.9), CENSUSDIFF1 (-1 on average) and CLIREACH1 (-1), and
its p90 needs the failed attempts gone (TIMEWHY1, TIMELOAD1, GATEPREFLIGHT1). One component under 3 min needs its unit tail moved first
(Loop's 143 s and 101 s classes are waits). The full set needs LAYERS1 e and f (the git half to its adapter's suite), LOOPPORTS1 and
MOVEMATRIX1 (the Process half's ticks and matrix), and FULLSET1's replay.

## 4. The order to build them in, as backlog rows

Two rows enforce and explain first (cheap, and they make the separation list); REACHCOST1 goes beside them (the most minutes, no
dependency); then the separations in the order of their over-budget minutes, LAYERS1 e and f continuing as they are. At most three
builders at once; GATEPREFLIGHT1 and REACHREPLAY1 apply to every builder of these rows.

1. **TIMEWHY1 — every time breach says why, from what the test did** (tools, driver tests; `code-structure-and-process.md` rule 22):
   each breach (a test over its ratchet limit, a gate killed at its budget, a blame-hang timeout, the layers census's one-second
   budget) is one line in a breach report the merge record and the queue's record carry: the test, its cause (process or git, disk
   beyond its scratch, network, wall-clock wait, source scan, only load, none of these) read from that run's ledger, the fix path of
   §2.1, and the separation candidate where a domain's classes share the cause; `time-ratchet --classes` prints it. First a text
   classifier (this plan's §1.5 table is its first output), then the runtime ledger. Proof: six planted breaches, one per cause and one
   under synthetic load, each classed right; the 17 time-ratchet failures of 11-13 to 11-16 classed. Size M. After: none; TIMELOAD1
   beside it, its load count the "only load" class.
2. **UNITPURE1 — a unit test starts no process, opens no git and waits on no wall clock** (driver, modules, service and devkit tests,
   tools; rule 23): the unit runs record per test, through the funnels a test host already has (`ProcessStarts.Within`, the git funnel,
   `GitFixture` and `FixtureSpawns`, the store's open, a delay or poll on `TimeProvider.System`), what it started, opened and waited on;
   the census reads that ledger and fails naming the test and the act, unless the test is on an allowance list that only shrinks, each
   entry with its row. It takes over the layers census's text allowances (93 process starts, 240 git sites, 1,983 clock reads) and makes
   `NeedsTraitTests` two-way: a tagged class needs its trait, and an untagged class with a start fails (LAYERS1 design §6 point 4).
   Proof: a planted unit test of each kind red, naming it; its twin tagged Process green; the allowance written from today's ledger
   (expect about 54 classes with starts). Size M. After TIMEWHY1's ledger.
3. **REACHCOST1 — the `reached` gate spends its time running tests** (tools; folds into MERGEFLOW1b): step clocks in the merge record;
   one build per tree reused with `--no-build --no-restore`; one `dotnet test` per project with a generated filter; the plan's census
   cached by tree hash; the light gates beside the build. Proof: on the next 20 merges, `reached`'s non-test time median under 3 min
   (6.5 today) and the merge's wall median under 10 (12.8), the same `reached-classes` counts for a replayed plan. Size M. After: none.
4. **LAYERS1 e and f — continuing, with these targets added** (driver; D286): f's families in the order of §2.2 item 6; the git half
   from 45 min (11-14) to its adapter's suite alone; real git in `Driver.Tests` from 274 classes and 1,401 cases to the adapter's; each
   family's unit cases under a second each on memory. Unchanged otherwise. After d (built).
5. **TESTSLOW1 — extended: the unit halves' tail moved by cause, in TIMEWHY1's order** (driver, modules, service tests): wave 1 the 71
   store-file classes to `MemoryUnitOfWork`; wave 2 the 97 wall-clock classes to `MovedClock` or a signal; wave 3 the 54 classes with
   untagged starts to a fake or to the Process tier; wave 4 the grids cut with TESTDIET1. Its existing left (the Process halves' classes
   with no reason) stays. Proof: the driver's unit half alone under 3 min of run (6.5 today) with at most 30 classes over 10 s (107
   today); no assertion dropped (each moved class's case count kept). Size L, four M waves. After UNITPURE1.
6. **CENSUSDIFF1 — the censuses judge the diff at a merge, the whole tree after landing** (driver and modules tests, tools; with
   CENSUSSCAN1): §2.2 item 4. Proof: the planted pair; the gate under 1 min at a merge (2.7 today); the census classes out of the unit
   half's run. Size M. After CENSUSSCAN1.
7. **CLIREACH1 — the `cli` gate's steps run where a merge's paths reach them** (tools): §2.2 item 5. Proof: each step's reach declared
   and checked by `test-map`; the gate's merge median under 1 min (2.2). Size S. After: none.
8. **LOOPPORTS1 — Loop's tick tested on ports** (driver, Loop; D286): §2.2 item 8. Proof: the tick families' unit cases on memory under a
   second each, one real tick per family green in the rehearsals; the Process half's tick minutes (1,980 s) to under 300. Size L. After
   LAYERS1 f's tick families and GITTICK3.
9. **COMPONENT1 — the driver's components as packages, Loop first** (driver, tools; a decision when b is taken, since it changes
   D286's single `Daoris.Application`): a, Loop's test project; b, Loop's assembly with reference-assembly builds; c, Kernel; d, a
   component fingerprint only if a measured gap remains. Proof per step below. Size XL in four rows. After REACHCOST1; b after a keeps.
10. **WEBREACH1 — the web's unit tests by area and catalogue** (web, tools): §2.2 item 9. Proof: the whole run under 5 min quiet, a
    catalogue change reaching the parity suites alone. Size M. After: none.
11. **MOVEMATRIX1 — the Move matrix narrowed outside the full set** (driver tests, tools; the timing census's item 6). Size S. After:
    none.
12. **TESTMEASURE1 — the three targets measured weekly** (the steward): §3's table read from the records each Monday with
    `tools/dev-metrics.mjs`, cold and warm labelled; a target met two weeks running closes its rows' measures (MERGEFLOW1b, FULLSET1,
    LAYERS1 z). Size S. After: the first of 1 to 3.

**The first cut to try on one component: COMPONENT1a, Loop's tests as their own project.**
Why Loop: it is the top of the order, so nothing must move first, and `StructureTests` already holds that nothing outside Loop names
its types; it is the driver's most changed folder (252 of 864 landings since 11-03); and it holds the heaviest Process cluster after
Workplace's git, the next separation by the owner's rule.

- Baseline: Loop's unit classes 85, 861 cases, 11.7 case-min (today's trx), its slowest `SessionsOutliveTheirLookTests` 143 s and
  `BackgroundWaitJudgementTests` 79 s; Loop's Process classes 79, 345 cases, 51.6 case-min (11-12); Loop's sources 198 files, its tests
  176 files and 2.4 MB of the driver's 20.5 MB of test code; the build share of a .NET run unrecorded until REACHCOST1.
- Keep if, two weeks after it lands: Loop's test project, its unit tier whole, runs under 3 min alone on a quiet machine with its build;
  a merge whose .NET change is Loop's alone has a `reached` under 5 min; the desktop's cold build is no more than 10% slower; and the
  census of cross-component references shows nothing new crossing into Loop.
- Retire (fold the tests back, one commit) if the cold build grows more than 10%, or Loop-touching merges save under 2 min median.

## 5. Risks, and how each is met

- **A test moved to a fake proves less.** Every fake is twin-proved: one contract class runs on the fake in the unit tier and on the real
  adapter in its suite (`RepositoryContract`'s 22 cases on both sides, green on its first real run; `UnitOfWorkContract`, `ProcessContract`,
  `SshKeyFormatContract`). The fake answers only the questions its port declares and fails any other, which is how `TreePoolMemoryTests`
  found each leaked git. A moved class keeps its assertions and case count; its real twin stays in the rehearsals until its family's
  contract covers what it asserted, and runs once green before the move lands. A divergence found later becomes a contract case.
  Planted check: a known defect put into the real adapter fails its contract, then is taken out.
- **A component boundary leaks.** `StructureTests` (semantic, `edges: []`) keeps the order; a census of cross-component references counts
  the types each component names across a line, per pair (5,055 downward and none upward today by the text census), as a ratchet that
  only tightens, and a component's surface gets an API baseline file as `Daoris.Ports` has, so a new crossing is a line a reviewer reads.
  A boundary contract case is written for each behaviour a component above relies on, in the component below's suite.
- **A fingerprint misses a dependency.** A component's or adapter's fingerprint covers its tree, the reference assemblies it names, the
  build files above it, its packages and pins, files linked into other projects (the launcher and GitDoor compile driver files by link),
  data read at run time (fixtures, `resources.json`, the canon) and any `[Reaches]`; each input kind is held by a planted test that moves
  it and expects the fingerprint to move (as `tools/merge/adapters.test.ts` does for adapters). The full set ignores verdicts once a week,
  and a red it finds that a fingerprint kept is a defect of the fingerprint, filed as a class row.
- **Load read as a cause.** TIMEWHY1 classes a breach "only load" only where TIMELOAD1's count shows concurrent heavy runs and the class
  passes its limit alone; anything else keeps its named cause.
- **UNITPURE1 refusing a test that needs the real thing.** The way out is explicit: the Process trait, which moves the test to the heavy
  tier and its adapter's or rehearsal's suite; the allowance list only shrinks.
- **A cut moving proofs out of a merge's reach.** A class tagged Process is still chosen by the heavy run after the landing for every
  path that reaches it (REACHGAP1's run stays until FULLSET1's measure holds), and its red is filed as main's.
- **Estimates that do not hold.** The minutes of §2.2 are a model; every row's proof is a measured figure against §3's baselines, and a
  cut that does not save what it says is retired, not defended.
