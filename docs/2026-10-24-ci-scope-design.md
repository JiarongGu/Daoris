# CISCOPE1 — a modular CI: a merge waits on what it reaches, the full set runs in the background

**Status: proposed (D207, 2026-10-24), round 3.** Written by Claude Opus under D191. It changes a foundation (the rule D115,
D180, CI1, GATESLIM1 and PROCREACH1 rest on: what a merge and a stage wait for), so under D191's amendments Codex reads it and an
independent Opus reader reads it as a foundation; the steward accepts after both.

**Round 3** (2026-10-24) answers round 2's two reads (`docs/2026-10-24-ci-scope-reads.md`: Codex's, 15 findings, 11 blocking,
*another round*; the Opus foundation read, 17 findings, 6 blocking, *accept after named changes*). Every finding was checked against
the code on main at `b0d68c641`, where every line the reads cite is where they cite it; §11.2 tables each. What moved:

- **The stage's waiver is deleted** (§5.3). The stage needs every gate of its plan passing, as `stageJudgement` has it today;
  known red waives only in the queue, and never by the census.
- **One rule for a failure already on the base** (§4.1, §5.2): inherited for that candidate, known red or not, filed as known red
  at once, never pinned to a member.
- **The queue's attribution and recovery** (§4.1): bisection by the failing unit's whole class or file over the candidate's
  prefixes, an outcome for each case it cannot pin, build failures apart, the retained candidate judged again before promotion;
  promotion recognised after a crash by ancestry; one conflict rule (set aside and handed back); a detached runner; and where a
  candidate's commit, message and records come from (the tool commits nothing the parent has not handed it).
- **Recall measured causally** (§3.5): a failure judged against its introducing commit alone, direct commits counted apart, an
  unsettled range counted as missed in a stated lower bound, deliberate deferrals apart from census misses, the selector pinned and
  run on historical trees.
- **The background run's schedule** (§5.1): a matrix of run kinds against CIFAST1; a sweep and a night satisfied by a run that
  finished, whatever its verdict; one admission policy for both runners, with the scoped run's worst wait stated, moved into
  CISCOPE1e with the memory reservation.
- **The stage's evidence** (§5.3): a durable record of background runs and of stages, apart from the gate tree's disposable
  record; the sweep's due record consulted by the stage; every stage entry point held to it.
- **D207's body and its Changes paragraph rewritten** to the design as it now stands.

**Round 2** (2026-10-24) answers round 1's two reads (`docs/2026-10-24-ci-scope-reads.md`: Codex's, 17 findings, 14 blocking;
the Opus foundation read, 21 findings, 8 blocking; both *another round*). Every finding was checked against the code on main at
`b61325e6c`, CIFAST1 merged (`0613afde2`); §11 tables each, and where round 2 answers it. What moved:

- **The steward's direction for round 2**: development never waits on the full set, since a merge waits only on its scoped run,
  in minutes. The owner's install is republished only on a commit the background run has passed: the sweep within its bound,
  CIFAST1's rehearsals paid, nothing known red that its change reaches (round 3 deletes that last waiver: every gate must pass,
  §5.3). It is never staged on scoped-green alone. Round 1's
  `--from-scoped`, its update-path list and its claim that the install is reversible are withdrawn (§5.3).
- **The merge queue** at the measured arrival rate: merges arriving while a scoped run is in flight join one candidate, run on the
  union of their reach, and are bisected on red (§2.7, §4.1).
- **What the scoped run runs**: every unit with no pass standing on the candidate's tree; a reached .NET fast half runs whole, since
  the name census is not a closure; unit scoping only where reach is a real closure (the TypeScript suites) and in the Process
  halves, which keep the sweep (§4.2).
- **A recall proof and a running miss count** (§3.5); known red keyed by test and failure fingerprint, confirmed on the base (§5.2).
- **The rows**, each landable alone, no backstop removed before its replacement (§8). The background run's schedule and known red
  come before the scoped run that relies on them.
- **A word**: what the scoped run leaves to the background run is **deferred**. *Owed* keeps CIFAST1's meaning, what a stage must
  pay (`ciPlan`'s `owed`, `tools/merge-branch.mjs:5109`).
- **Corrections** of the wrong claims both reads name (§2.1, §2.2, §2.5, §1).

Round 1's figures were measured on this machine on 2026-10-23/24 from the main checkout's own records (the CI's
`local/scratch/ci-<sha7>/status.json` and `.trx` files, `local/class-verdicts.json`, `local/gate-verdicts.json`) or from the tree at
`013b5d090`, by the methods §2.6 gives. Round 2's arrival rate (§2.7) is from main's history at `b61325e6c`. Round 3 read
`73c1fe0`'s and `f296996`'s `status.json` (§8 row 2). No full CI, Process half or rehearsal ran for any round.

The backlog rows are CISCOPE1 (this design), CIFAST1 (its first row, landed) and TESTVALUE1 (folded in, §7).

## 0. The direction and what this design reads it as

The owner's direction of 2026-10-24, as the quest gives it: CI must be properly scoped, running only what a change really needs,
module by module; the full run only in the background, never blocking development; and the tests are too many and too verbose.

Read here as five commitments, one per section: a map of modules and what every test reaches, with its recall measured (§3); a
scoped run per merge that a merge waits on, in minutes (§4); the full set in the background, never blocking a merge, the one run a
republish of the owner's install waits for (§5); the rehearsals out of the per-merge path and side by side (§6); and each test's
value measured, with a rule to prune or merge and a target size (§7). §8 gives the rows in build order; §9 what each changes of
D115, D180, CI1, GATESLIM1 and PROCREACH1; §10 the questions for the readers of round 3; §11 the reads of rounds 1 and 2,
answered.

What a merge waits for and what the install waits for are now two different things. A merge waits for its scoped run: minutes, and
exact only as far as the map is (§4.3). The install waits for the background run: the full set, as D180 point 4 and CIFAST1 have it,
because once a staged build confirms its predecessor is gone (§5.3).

## 1. Today, as the code has it

Line numbers are `tools/merge-branch.mjs` on main at `b61325e6c` unless named.

- **A merge** (the parent's integration tool) blocks on `BLOCKING` (`universal`, `code-map`, `orient-index`, `:933`) and on the
  path checks `web-types` and `web-rules` where their paths are reached (`PATH_CHECKS`, `:947`). Every other reached gate is listed
  and skipped, "in the CI beside" (GATESLIM1, D115's 2026-10-19 second part). The merge is made in the main checkout (`mergeNext`,
  `:4431`; `git merge --no-ff --no-commit`, `:4454`), and an open merge is read from its `MERGE_HEAD` (`mergeInProgress`, `:2572`),
  which `--continue`, `--rerun` and `--stale` dispatch on. **The tool commits nothing**: "Committing stays the parent's, after it
  reads the diff and writes the records" (`:69`), and `--continue` refuses a merge the parent has not committed (`:4517`). The
  parent's merge commit carries records beyond the branch's tree: `b61325e6c` adds the reads file, `TASKS.md` and the indexes. A
  recorded batch is checked by ancestry on resume (`:4505`) and its record dropped when it ends (`dropState`, `:4507`).
- **The CI** (`--ci`, `gateCi`, `:5388`) gates main's tip in `local/gate-tree` under its lock, running each gate whose verdict
  does not stand on the tip's tree (`ciPlan`, `:5109`): a pass stands while no path changed since reaches its gate by the lane
  table `REACH` (`:989`). Its watch gates the newest tip once the tree is idle, so one run covers every merge since the last, and it
  never gates a tip a run already gave a verdict on, failed or not (the header at `:179`).
- **CIFAST1** (landed, `0613afde2`): a plain `--ci`, and each run of the watch, runs every gate but the family and deployment
  rehearsals (`STAGE_REHEARSALS`, `:500`). One its verdicts lack is `owed`, neither failed nor passed, and the run is green on the
  rest. `--ci --stage [<commit>]`, the stage's path, owes none and runs them on the commit to stage. The stage's judgement is
  unchanged: `--from-gated` refuses a commit whose rehearsals have no pass standing on its tree.
- **A Process half** runs by class (PROCREACH1): `tools/process-reach.mjs` reads each class's reach as the *areas* the types its
  files name are defined in (`censusOf`, `process-reach.mjs:573`; `areaOf`, `:555`), one level of naming, not the named code's
  calls (`process-reach.mjs:27-29`). `classPlan` (`:5134`) keeps a class whose newest standing verdict passed and runs the rest; it
  runs the **whole** half when no class's pass stands (`:5175`), when its oldest kept verdict is past a day (the sweep, `:5178`,
  `SWEEP_BOUND_MS` at `:1898`), and when every class is kept but the half has no verdict of its own, for instance because it
  failed outside a test (`:5181`; `:2044`).
- **Retries** (FLAKE1, GATEORDER1): a Process half's failures are re-run alone once its lane is quiet; a failure outside the
  Process category is real and never re-run (`:3559`); D180 rejected retrying the Node suites in general.
- **The rehearsals** run beside the git half one after the other, release then family then deployment (`BESIDE_GIT_HALF`,
  `:490`; `laneSchedule`, `:628`; REHLANE2, REHLANE2b). A rehearsal waits for the whole light lane before it starts (`:647`,
  `component === null ? light`). The lane table sends every driver-lane source path to the family and deployment rehearsals
  (`DRIVER_READERS`, `:976`) and every modules-lane source path to the deployment rehearsal only (`MODULES_READERS`, `:974`: `modules`,
  `modules-process`, `deployment`; round 2 said the family rehearsal too, corrected, Codex r2 1, Opus r2 12); the driver's **tests**
  reach only the .NET suites (`:1101`). A watch run owes the family and deployment rehearsals (`STAGE_REHEARSALS`, `:500`; the plan's
  `owe`, `:5456`).
- **A republish of the owner's install** stages with `publish:desktop --stage --from-gated <commit>`, which asks `--passed` of the
  gate tree: every gate of the plan must have a passing verdict that stands on that commit's tree (`stageJudgement`, `:1926`), the
  swept rule included (`sweptOut`, `:1908`). The gate tree's record keeps the commit last staged (`staged`,
  `tools/desktop-publish.mjs:1020`), but it is no baseline: every CI run rewrites the record with `staged: null` (`:5486`, `:5536`)
  and a prune deletes it (`:3106`). `sweptOut` only expires a pass that kept classes; a whole pass never does (`:1909`). A plain
  `--stage` asks `--passed` of the checkout (`desktop-publish.mjs:1154`, `ungatedRefusal`, `:871`, `:1163`), `--from-gated`
  hands a child the same flags without its own (`:1002`, `:1137`), and `--force-ungated` is the owner's explicit override. A gate
  verdict records its gate, tree, commit and branch, not which kind of run gave it (`:4069`). Failing test names are read with a
  theory's case cut off (`:2023`, `:2119`).
- **Once a staged build confirms**, `InstallSwap.Finish` deletes `update/previous/`
  (`src/Daoris.Desktop/Daoris.Desktop.Driver/Setup/InstallSwap.cs:374-376`), and whatever the new build wrote into the home's
  stores stays (`.claude/knowledge/compatible-builds.md`). D139 excludes failures after confirmation from its rollback.
- **The install's own landing** (`trees land`) judges the review, the second opinion and the workflow (`TreeGates.GateAsync`,
  `Trees/SessionTrees/TreeGates.cs:41-56`) and runs none of this repository's gates. In this repository it lands a session's work as
  the branch `quest/{slug}-{quest}`, never merged by the install (DOGFOOD1, `TASKS.md`); main moves only by the parent's merge tool
  and the parent's own direct commits.

## 2. What was measured

### 2.1 The CI runs

From each run's `status.json` (minutes of wall; "whole" is `classPlan`'s reason):

| Run | State | Wall | Git half | Family | Deployment | Driver's other half | What made the wall |
|---|---|---|---|---|---|---|---|
| `73c1fe0` | failed | 59.5 | 32.8, whole: no class's pass stands | 22.4 | 16.7 | 7.4, whole: `examples/plugins/…` | the git half and the rehearsal chain beside it |
| `f296996` | green | 38.4 | 31.5, whole: **every class kept, but the half owes a verdict** | kept | kept | 0.4 (4 of 114 classes) | a git half that ran 89 classes it had just kept |
| `9977845` | failed | 62.7 | 43.6, whole: no class's pass stands | 18.3 | 21.8 | 10.0, whole: `examples/plugins/…` | the git half |
| `59a2db0` | failed | 44.7 | 24.2 (87 of 89 run) | 14.1, FAIL | 14.1 | 8.0 (90 of 115 run) | the git half, the chain beside it |
| `6ce6235` | green | 74.3 | 1.1 (1 of 89 run) | 23.4 | 20.7 | 0.7 (5 of 115 run) | the light lane, then the two rehearsals in turn |

Three facts follow. A run gates every merge since the last (96 to 183 paths changed between consecutive runs), so the union of
several merges reaches nearly every class. A run whose Process halves had almost nothing to do still took 74.3 minutes: **44.1 of
them** were the family and deployment rehearsals one after the other, and the rest the light lane each rehearsal waits for before
it starts (`:647`) and the tree's preparation, which `status.json` does not split (round 1 said "the rehearsals alone"; corrected,
Codex 1, Opus claim 3). CIFAST1 removed those 44.1 minutes from a plain run's critical path, not 74; a stage still pays them. And
`f296996` spent 31.5 minutes running a half whose every class had a standing pass.

The driver's other half has 114 classes in `f296996`'s run, 115 in `9977845`'s and `59a2db0`'s (their trx files), and 118 in the
census at `013b5d090` (§2.3), which reads a later tree. Each figure below names its source.

### 2.2 What a single merge reaches

The last 40 first-parent merges into main (to `42fbf1fb4`), each judged alone with the census at `013b5d090`: 31 reach the three
Process halves; the driver's other half runs whole for 6 of them, the git half for 5, the modules' for 4 (`package.json`,
`daoris.lanes.json`, a test project file, an example plugin's file, a test source the census at the newer tree no longer holds).
Six of the last 80 merges reach every gate through `REACH`'s first rule (`:989-997`).

Class time is summed per class from `9977845`'s trx files, the last run that ran the three halves whole. Round 1's cost model was
the class time a merge reaches divided by the half's width (8, 4, 1), never less than its slowest class. Mean per merge that
reaches the half:

| Half (classes) | Class time, whole | By today's reach (model) | By subject (§3.2, model) |
|---|---|---|---|
| driver's other half (115 by trx) | 38 min, 724 tests | 4.7 min | 2.9 min |
| git half (89) | 158 min, 746 tests | 36.8 min | 18.1 min (median 11.8, p90 36.9) |
| modules' half (20) | about 1 min, 167 tests | 1.3 min | 0.5 min |

**The model is low** (Opus claim 6). It puts the driver's other half, run whole, at 38/8 = 4.75 minutes; the same half ran whole in
7.4 and 10.0 (§2.1), since the `Alone` tail runs one class at a time (`docs/development.md`, the Process halves) and the build and
test-host starts are not class time. Measured over model is 1.6 to 2.1 for that half. §4.4 rebases on the measured walls: the
model's figures are multiplied by that ratio, and CISCOPE1e's proof measures the real ones.

Changes outside the driver reach Process classes too (Codex 2): the CLI's lock helpers and the dispatcher's seam reach
`driver-process` (`:1178`, `:1186`), and `REACH`'s first rule sends a gates, lanes, package or line-endings change to every gate.

### 2.3 Why a driver change reaches nearly every class

Every Process class of both driver halves names the `Loop` area, and nearly every one names `Platform` (driver's other half 118 and
113 of 118 in the census; git half 89 and 89 of 89). Inside them one file is named by all: `Loop/Driver.cs`, 4,103 lines, the
driver's one class the tests drive. A change to any one area reaches 57.9 of the other half's 118 classes on average, and 28.5 of
the git half's 89.

Three grains were compared over the same 40 merges (class-time floors summed over the merges, minutes):

| Grain | Driver's other half | Git half | Modules' half |
|---|---|---|---|
| the area (today's) | 145 | 1,141 | 40 |
| the changed file and the files of its area that name what it declares, closed within the area | 144 | 1,130 | 39 |
| the changed file alone | 106 | 786 | 28 |

So the area is not coarser than the module's own coupling. The file grain saves a third, but only by dropping the calls inside a
module. That is the reason PROCREACH1 gave for rejecting it (D180's PROCREACH1 note, *Rejected*), and the closure measured here
confirms it. The git half's weight is not about grain. 75 of its 89 classes are tests of `Trees` (their files sit in the test
project's `Trees/` folder, which mirrors the driver's), and `Trees` is the landing, the trees and the branches every git test drives.
A change to `Trees` therefore reaches the git half whole, at any grain.

**The area census is not a closure.** It follows what a class's files name, one level deep. D180's PROCREACH1 note records that a
full call closure reached 88% of the driver's files from every class, which is why it stops at names, and that the gap goes unseen
until the next sweep. A real miss of this kind is in the FIX-LOG (2026-10-21, *Three censuses on `main` broken by merges that passed
their own gates*): GENREGEN1's change to `Trees/SessionTrees/GeneratedWriters.cs` broke `NoConsoleWindowTests` and
`EveryChildIsHandedTheToolsTests`, which walk every driver source as text and name no `Trees` type. Reflection over an assembly
(`PipeCensusTests.cs:272`, `McpToolRegistrationTests.cs:76`), a directory scan (`SystemWiringTests.cs:574`) and another project's
source read as words (`DriverCommandTests.cs:103`) are the same blind spot. §4.2 runs a reached .NET fast half whole for this
reason.

### 2.4 The suites' size

| Suite | Tests | Test time | Test code |
|---|---|---|---|
| driver, fast half | 14,114 | 1 m 39 s | 846 files, 233,482 lines (the driver's own sources: 216,485) |
| driver, Process halves | 724 + 746 | 38 + 158 class-minutes | (in the above) |
| modules, fast half and Process half | 2,326 + 167 | 1 m 43 s + about 1 min | 209 files, 46,812 lines |
| service, core and HTTP | 2,687 + 328 | 1 m 38 s + 3 m 17 s | 194 files, 52,360 lines |
| devkit | 113 | 20 s | 13 files |
| CLI (`npm test -w daoris` within verify) | 929 | 239 s | 75 files, 17,759 lines |
| tool-tests | 270 | 441 s | 4 files, 9,208 lines |
| web unit (vitest) | 9,145 in 423 files | 418 s | 425 files, 100,931 lines (the page's sources: 116,463) |
| web end-to-end, measured box | 24, 110 | 1.1 min, 2.9 min | |

About 31,600 tests in all. Assertions that pin a literal of six words or more (an English sentence where the words are rarely the
subject): 2,081 in the driver's tests, 116 in the modules', 151 in the service's, 1,179 in the page's.

### 2.5 History and failures

`local/class-verdicts.json` keeps six verdicts a class (`CLASS_VERDICTS_KEPT`, `:1779`): the git half's 349 hold no failure,
the driver's other half's 434 hold 13, the modules' 77 hold one. `local/gate-verdicts.json` keeps 400 (`addVerdicts`, `:1758`),
about a day and a half of runs here (2026-10-22 07:07 to 2026-10-23 15:48). Neither is long enough to say what a test has ever
caught: §7 needs a history of its own. The FIX-LOG holds 307 entries dated 2026-09-24 to 2026-10-24, the month §3.5's recall proof
replays.

**The cheap checks, as measured, are not the cheap tier** (Opus claim 5). `partials`, `read-shares`, `optional-locks`,
`live-logins`, the four documentation tools, `release-prep --check`, `orient-index --check`, the one-word, home and CLI-usage
censuses and `daoris check` took 27.8 s together, one after another, on this tree. That excludes `universal` (1 to 2 minutes),
`code-map`, `web-types` (about a minute alone, MERGETSC1), the CLI's typecheck, the locale vitests (jsdom, with the page's setup
file, `src/Daoris.Web/vite.config.ts:29,38`) and the census rows of `docs/development.md` (*Choose the checks*) that need a .NET
build. §4.4 prices the tier at 2 to 4 minutes from those parts, unmeasured as a whole; CISCOPE1e measures it.

The machine has 22 logical processors and 63.7 GB of memory. MEMGATE1 asks 10 GB free for a rehearsal, 8 for a Process half and
6 for a heavy suite (`MEMORY_NEEDS`, `:2208`). It samples per gate and reserves nothing, and after its bound a waiting gate runs
anyway (`memoryGate`, `:2331`).

### 2.6 How it was measured

Scratch scripts under the repository's ignored `_fixtures/`, not kept, read the main checkout's records and this tree. They
imported `censusOf`, `classesReached`, `pathReach`, `typesIn`, `namesIn` and `readCs` from `tools/process-reach.mjs`, and
`pathsReaching`, `readLanes` and `slowestClasses` from the merge tool. A merge's paths are `git diff --name-only <merge>^1
<merge>`, and a class's subject is the first folder of its own file under the test project. Every figure uses today's census for
every merge, so it is an estimate for the older merges. CISCOPE1c's proof (§8) measures again with the tool the row builds, which
keeps the method. §2.7 is `git log --first-parent main` over the ten days, merges told from direct commits by their parent count.

### 2.7 The arrival rate (round 2)

Main's first-parent history, 2026-10-14 to 2026-10-23 (committer time, this machine's zone):

| Day | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 21 | 22 | 23 |
|---|---|---|---|---|---|---|---|---|---|---|
| Merges | 68 | 26 | 44 | 35 | 63 | 99 | 111 | 127 | 71 | 26 |
| Direct commits | 24 | 20 | 49 | 17 | 10 | 94 | 59 | 41 | 31 | 23 |

670 merges in ten days, a mean of 67 a day and a peak of 127. Between consecutive merges: a tenth within 1.5 minutes, a quarter
within 4.7, a median of 10.2, three quarters within 22.5, nine tenths within 39. Of the 181 hours that held a merge, 30 held six or
more; the busiest held 12 (2026-10-21, 02:00). Direct commits by the parent land on main between merges, 10 to 94 a day.

A scoped run of a driver merge is 15 to 28 minutes (§4.4). One merge per run serves at most two to four an hour, and the busiest
hours bring six to twelve: run one at a time, the queue grows through every busy hour (Opus 3; D180 rejected the merge as the
bottleneck). §4.1 therefore batches.

## 3. The modules and what each test reaches

### 3.1 Modules

A **module** is a source area: the first folder of a source project (the driver's 15 module folders, `Loop` to `Workflows`, D166;
the shell modules' folders; the service core's), a module folder of the CLI's sources (`src/Daoris.Cli/src/<module>`), a top
folder of the page's sources (`src/Daoris.Web/src/<folder>`, `work` to `test`), and a file of `tools/`. A source project's own
root files are their project's module. The lanes (`daoris.lanes.json`) stay what they are, domains that work is dispatched to.
A module is finer, and a lane holds many.

### 3.2 Two facts per test unit: its subject and its reach

A **test unit** is what a runner can select: a .NET class (either half), a `node --test` file, a vitest file, a Playwright spec, a
rehearsal. The census gives each unit two facts:

- **Its reach**: what it can see.
  - **.NET**: PROCREACH1's census unchanged, at the area grain (§2.3), one level of naming. It is **not a closure**, and round 2
    says so: §4.2 uses it to choose classes only in the Process halves, which keep the sweep, and runs a reached fast half whole.
  - **TypeScript**: a closure over the repository, not the package (Opus 14). Every relative import and every literal `import(…)`
    is followed wherever it leads, so `src/Daoris.Cli/tool-test/*.test.ts` reach the `tools/` files they import
    (`desktop-publish.mjs`, `deployment-rehearsal.mjs`, `rehearsal-kit.mjs`, `processes.mjs` and the rest, which `REACH`'s hand
    rows at `:999-1034` cover today). A string that names a repository path reaches that file; one that names a folder reaches
    every file under it; one that names a basename a twin resolves (`src/Daoris.Web/src/test/driverSource.ts:11-29`,
    `locales/note.test.ts:40`) reaches every tracked file of that name. A glob (`import.meta.glob`, the catalogues at
    `src/Daoris.Web/src/locales/index.ts:26`) reaches every file it matches, now and when one is added.
  - **Edges no string names** are declared, not guessed (Codex 4): a generator's inputs and outputs (the page builds into the
    HTTP host's `wwwroot`, `vite.config.ts:16`), an assembly a test reflects over, a directory a test scans, another project's
    source read as words. A .NET class declares them with `[Reaches(…, Why = …)]` as PROCREACH1's do; a TypeScript file with a
    `// reaches: <path or folder> — <why>` line the census reads. A .NET class that runs the CLI under node, which `REACH`'s rows
    at `:1178-1186` hand to `driver-process`, carries a `[Reaches]` for it; until it does, the hand row stays and the census check
    reports the disagreement (§3.4 check 3) rather than dropping the row.
  - **An unresolved read is wide**: a file read the census cannot place (a computed path, a read through a variable it cannot
    follow) reaches its whole suite, said by name.
  - **A rehearsal**: its tool's import closure and the programs and scripts it starts, which `REACH`'s hand rules for `tools/`
    list today.
- **Its subject**: the module it tests, read from where its own file sits. The test projects mirror the source modules: the
  driver's tests have `Trees/`, `Platform/`, `Loop/` and the rest; the page's tests sit beside their sources. A unit whose file
  sits at its project's root takes its project as its subject. The subject is used in one place only, the git half's budget
  (§4.2 point 5), where 3 of the 89 classes sit at the root.

Reach answers "can this change break this test". Subject answers "is this the test of what changed".

### 3.3 Fixtures and twins

A twin's table (`kept-names.json`, `hand-over-order.json`, `review-rules.json` and the rest of `REACH`'s fixture rules,
`:1038-1100`) reaches the units whose strings name it, in any suite. The census derives what those hand rows say today, and each
row becomes a check rather than a rule: the census's reading must agree with it (§3.4 check 3). A fixture no unit names reaches
none, as PROCREACH1's `pathReach` already says for the test project's files (`process-reach.mjs:744-781`).

### 3.4 The census's checks

`node tools/test-map.mjs census | reach <from> <to> | check` (CISCOPE1c), one tool over every suite, generalising
`tools/process-reach.mjs`, which it absorbs. Its checks test the **inventory** and the declared edges. None of them measures
recall; §3.5 does. `check` fails when:

1. **Inventory, bound to the tree it ran on** (Codex 5, Opus 18). In a scoped run, every unit a runner ran is in the candidate's
   census, and the candidate's census matches what the runners discover on that tree (`dotnet test --list-tests` per reached
   project, vitest's and `node --test`'s file lists), so a candidate that adds or removes a class compares with itself. The
   comparison with a whole run's reports (trx, vitest's JSON report, `node --test`'s report) runs in the background run, against
   its own reports of the same tree. Where discovery cannot be read, the suite runs whole and the run says why.
2. **Placement**: a tracked file is placed by no module and is not on the laneless list's reasons (LEFT1's rule, now for modules).
3. **Hand rows**: a row of `REACH` names a gate whose units the census finds no reader in, or the census finds a reader the row
   omits.
4. **Subjects**: a git-half class at its project's root carries a `Subject` declaration, since it is the one place a subject
   chooses (sized: 3 classes). Elsewhere a root file's subject is its project, and the check asks nothing (Opus 18 counts 70 root
   files in the driver's tests, 11 in the modules', all 37 of `Service.Http.Tests` and 4 of `Service.Tests`).
5. **Whole-suite files**: a file acts on every unit unnamed (a `global using`, an assembly attribute, a module initializer, a
   project or props file, a run settings file, vitest's `setupFiles`, the vite config, a `tsconfig`) and is not on the census's
   list of whole-suite files. Such a file runs its suite whole, as today, and the list makes that visible.
6. **Expected edges** (Codex 4): the census's own tests hold one fixture per edge kind (reflection over an assembly, a fixture
   or twin, a generated output, a source read as text, a census, a catalogue glob, another project's words, a folder scan with a
   member added) and fail when its reach misses that fixture's edge.

### 3.5 Recall: the proof and the running miss count (round 3)

The checks above say which units exist and that the declared edges hold. They cannot say whether a unit's real dependencies fall
inside its reach. Recall is measured, twice, and both measures judge **causally** (Opus r2 3, Codex r2 7): a failure is held
against the one commit that introduced it, never against selection somewhere in a range.

**What each failure is judged by.**

- **Its introducing commit**: the first-parent commit of main that turned the failing unit red. It is found by running the
  unit's whole class or file (never the test alone, §4.1 point 4) over main's first-parent commits between its last pass and its
  first failure, halving, as the queue's own bisection does. A commit the FIX-LOG entry names is checked by one run on it and on
  its first parent rather than trusted. A **range that cannot be settled** (the unit no longer builds at an end, a commit in it
  does not build, the failure does not reproduce) is kept explicit, with the reason.
- **Its verdict**, one of six, each counted apart:
  - *selected*: the introducing commit's own change reached the unit, by the census or by a suite §4.2 runs whole whose rule held
    there;
  - *missed*: it did not; the edge the census lacked is named where it is found;
  - *deferred*: it reached the unit, and §4.2 deliberately defers it (the git half past its budget, a browser spec, a rehearsal);
    a deliberate deferral is not a census miss;
  - *unscoped*: the introducing commit is a direct commit of the parent's, which no scoped run judged (§4.1, *every path that
    moves main*); its structural reach is still computed and shown, apart;
  - *execution*: a build, discovery or host failure, which names no unit to select;
  - *unsettled*: the range above, **counted as missed**.
- **Recall** is selected over selected plus missed plus unsettled, stated as a **lower bound**, with the unsettled count beside it
  and the figure it would be were each of them selected. Deferred, unscoped and execution failures are reported beside it, not in
  it.
- **One regression is counted once**: sightings that share the unit, the fingerprint (§5.2) and the introducing commit are one
  failure, however many runs and candidates saw it.

**The proof** (CISCOPE1c), over 2026-09-24 to 2026-10-24.

- **The selector is pinned** (Codex r2 8): CISCOPE1c's census tool, at its own commit, which the output records, reads each
  historical commit's tree, as `repository(root, commit)` already reads a commit's tracked files for PROCREACH1's census
  (`tools/process-reach.mjs:819-823`). The tool did not exist at those commits and is never run from them.
- **Structural reach only.** The proof replays whether the introducing commit's diff against its first parent reaches the failing
  unit. What §4.2 would then have *scheduled* depends on standing verdicts, class times, failure order and the budget, which the
  retained stores do not hold for that month (§2.5); that state is declared unknown, never assumed empty, and the scheduling
  decisions are proven by CISCOPE1e's tool-test fixtures instead. A reached unit of the git half is reported as *reached* and,
  apart, as *within today's budget* or not by the class times of §2.2, so a budget deferral is not counted as a census miss.
- **The failures and their spans** (Opus r2 17): the FIX-LOG's entries of the month whose symptom is a test or a check failing
  (the 2026-10-21 GENREGEN1, FABRIC-CLOSE1d3 and LANDBACKLOG1 entry among them), the only source that reaches back to 2026-09-24;
  each failed CI run's failing units (`local/scratch/ci-*/status.json` and its trx, from CI1's first run on 2026-10-22); each class
  failure in `local/class-verdicts.json` (six verdicts a class); `local/gate-verdicts.json` (about a day and a half). A batch's
  record is dropped when it ends (`dropState`, `:4507`), so a merge handed back by a gate is found only where a FIX-LOG entry or a
  CI run names it. The output states each source's span and count, and **the recall over the FIX-LOG's failures alone**, apart from
  the recall over all.
- **The first results are kept**: the first replay's list is written as it stands, before any repair. Each miss is then fixed in
  the census in the same row, by an edge, a declaration or a whole-suite entry, with an expected-edge fixture (§3.4 check 6); the
  replay runs again, and its list is reported beside the first. A miss the census cannot fix is named and stays in the count.

GENREGEN1's two driver censuses are the expected first miss of the class census, and *selected* under §4.2, since the change
reached the driver's fast half, which runs whole (§4.2 point 2).

**The running miss count** (CISCOPE1d's records, CISCOPE1e's scoped runs). It is fed from two places: a failure the background run
finds on a unit that passed on an earlier background run, and a failure a candidate finds inherited from its base (§4.1 point 4).
Each new failure, deduplicated as above, has its introducing commit found by the bisection above, run in the background at the
background's admission (§5.1). The verdict then reads the **scoped run of the candidate that reached main with that commit**
(`local/scope-runs.jsonl`, one line per candidate run: its base, its members, its tree, the units it selected, ran, deferred and
their verdicts): a run on a candidate that was abandoned, or bisected away, proves nothing about the promoted tree and is not read.
A unit that candidate selected, ran and passed is *selected, passed*: the census held, and the failure is a flake, an
order-dependence or an introduction found wrongly, listed apart for its row. Misses go to `local/ci-misses.jsonl` (the unit, the
introducing commit, the verdict, the edge if found), are filed with the failure's own row, and are fixed in the census as the
proof's are. The CI's status and `--ci --status` show the trailing thirty days: recall as a lower bound, each other verdict's
count, and the misses still unfixed. The count starts from the proof's.

**Before CISCOPE1e** a merge waits on `BLOCKING` alone, so no scoped run exists to read: the count then judges each introducing
merge's diff by the census structurally, as the proof does, and says that it measures what a scoped run *would* have selected, not
what one did.

## 4. The scoped run: what a merge waits on

### 4.1 The candidate and the merge queue

**Where.** The merge tool merges into a **candidate** in `local/scope-tree`, a detached worktree of its own with its own lock,
whose builds stay warm between candidates. The main checkout is not touched until promotion, so it stays free while a run goes
(D180's reason for keeping verification out of it). The merge is today's `git merge --no-ff --no-commit`, made in the scope tree
at the candidate's base, with the orientation index and the generated folders written into the merged tree (ORIENT1a) and the
union records resolved in the index (MERGETASKS1). Nothing moves a ref of main.

**Where a member's commit, message and records come from** (Opus r2 6). Today the tool commits nothing and the parent commits each
merge after reading its diff, with its records (§1); round 2's "the merge itself is today's" was wrong about the commit. In the
queue **the tool commits nothing the parent has not handed it**:

- **The parent reads the diff before it queues**, as it reads it before committing today: `merge-branch.mjs <branch> --preview`
  makes the merge in the scope tree's spare index against main's tip and prints its diff and its gates, committing nothing.
- **It queues the branch with its message and its records**: `merge-branch.mjs <branch> --message <file>`, the file under the
  repository's ignored `local/`, holding the merge commit's message; the records the merge carries beyond the branch's tree (the
  row moved to the archive, `TASKS.md`, a FIX-LOG entry) are a records commit the parent makes **on the branch** before queuing.
  The queue refuses a branch without a message.
- **The tool's merge commit holds exactly**: the branch's merge, its records commit included; what the tool writes into every
  merge today (the orientation index, the generated folders); and the union records it resolves today. Its message is the
  parent's file, verbatim, with the trailer lines the file carries. Nothing else enters it.
- **Records written after promotion** (a record the parent could only write once the merge landed) are a direct commit on main,
  and move the candidate in flight's base (point 3 below), by records only.

**The queue.** `merge-branch.mjs <branch> --message <file>` puts the branch on `local/merge-queue.json`, a durable record under the
tool's lock, says its place, and waits for its outcome; the parent may end the wait without affecting the queue, and `--queue`
reports it. **The runner is a detached process** (Opus r2 8), started by the first invocation that finds none, as `--ci --watch`
is; its process id is in the record, and an invocation that finds the record's runner gone starts another. The runner:

1. **Forms a candidate** from every branch queued, in arrival order, at most eight. Its base is main's tip. Each branch is merged
   on top of the last as its own merge commit, so history keeps one merge per branch as today. **One conflict rule** (Codex r2 4,
   Opus r2 8): a branch the tool's resolvers cannot merge is **set aside and handed back** with its conflict named, its merge
   aborted in the scope tree, and the rest go on; the parent resolves it on the branch (merging main into it in the branch's own
   tree) and queues it again. No conflict is left open in the scope tree, so `--continue` never resumes one there. A merge
   arriving while a candidate's run is in flight waits for the next candidate.
2. **Runs the scoped set** (§4.2) on the candidate's tip. Its basis is every unit with no pass standing on that tree, so it is the
   union of the members' reach, and of any direct commit on main since a unit last passed. Its record goes to
   `local/scope-runs.jsonl` (§3.5).
3. **On green, promotes.** The runner writes `promoting` with the candidate's tip to the record, then, in the main checkout, `git
   merge --ff-only <candidate>`, only if main's tip is still the candidate's base, then writes `promoted`. If main moved by a
   direct commit, the candidate is formed again on the new tip from the same branches, with the same messages, and the scoped set
   runs again; by standing it runs only the units the direct commit's paths reach. **A base moved by records only** (every path
   of the direct commits on the records lane's rule, `docs/**`, `CHANGELOG.md`, `CLAUDE.md`, `ROADMAP.md`, `AGENTS.md`,
   `daoris.json`, `daoris.lock`, `.claude/**`, `:1249`, with `TASKS.md` and `docs/index/**`) is re-formed and runs **only the
   cheap tier's record checks** (`daoris check`, the documentation tools, `release-prep --check`, `orient-index --check`); the
   `service` suite those paths reach (`:1234`) is deferred to the background run for that re-form, a give-way §4.3 names. If the
   checkout's state refuses the fast-forward (a file changed there that the candidate changes), the runner says so and keeps the
   candidate; `--continue` promotes it.
4. **On red, judges each failure, then bisects** (Codex r2 3, Opus r2 2 and 7):
   - **A failure already on the base is inherited, known red or not** (Opus r2 2): each failing unit's whole class or file is run
     on the candidate's base, and a test that fails there with the same fingerprint (§5.2) is **inherited for that candidate**,
     **filed to `ci-findings.json` at once as known red** (its expiry starting then), **never pinned to a member**, and does not
     count against it. A test already known red with the same fingerprint skips the base run only when the base is the commit it
     was last seen failing on.
   - **A build, discovery or host failure** names no unit to bisect. The base is built first: a base that does not build stops
     the queue, says so, and hands nothing back, since main itself is broken and the parent's to fix. Otherwise each prefix
     (the base plus its first *k* members) is built, halving, to the first prefix that fails; that member is handed back with the
     failure, and the members before it go on.
   - **A test failure** is bisected by the failing unit's **whole class or file**, never the test alone, so a test another
     polluted reproduces (FLAKE1 never re-runs such a failure alone, `:245-249`). The class or file runs on the candidate's
     prefixes, halving. The base passes it and the whole candidate fails it, so halving always finds two adjacent prefixes, the
     first passing and the next failing, whether or not the failure is monotonic over the prefixes: the member that turns it red
     is **pinned**, and where the members before it are needed (it passes alone on the base) it is handed back as **pinned with**
     those members named. Independent failing units are bisected side by side over the same prefix builds.
   - **Unresolved**: a failing unit whose class or file does not fail again on the whole candidate (a flake, an order across
     classes) is run once more as its gate's selection on the candidate; still red but not reproducible by class or file, the
     candidate's members are run **one at a time** as candidates of one. Nothing is handed back on a failure that cannot be
     placed.
   - **No unit verdict is written** from a bisection's runs; they are attempts in the history (§7.1), not passes.
   - **The retained candidate is judged again** before promotion: the members not handed back are formed anew on the base, and
     its scoped set runs on the new tree, whose standing basis runs everything the removed members' paths reach. It promotes only
     on its own green.
   - **The cost**: one reproducible failure over eight members is three halvings, each a warm incremental build and one class or
     file, minutes or less. Several failures, a build failure and an unresolved one cost more, and the queue's replay reports them.

**What the queue serves** (Codex r2 5, Opus r2 9). A merge waits for the run in flight and its own, plus re-forms and red
candidates. At the measured rate (§2.7) and §4.4's walls that is an **estimate**, not a bound: about two driver runs, 30 to 56
minutes, at the busiest hour, one in a quiet one. It is not bounded by capacity: a burst can queue more than eight, a union reaches
more than any one member, every direct commit re-forms the candidate in flight (10 to 94 a day, §2.7; a records-only one costs the
record checks), deferred git-half classes and known-red units run again in every candidate that reaches them until the background
run passes them, and each red candidate adds its bisection and a run of the retained members. CISCOPE1e's proof replays 2026-10-21's
127 merges and 41 direct commits at their timestamps against the queue, with each merge's union scope priced, the overflow past
eight, the re-forms, and red and restart cases injected, and reports the backlog over the day and each merge's wait, p50, p90 and
the worst.

**Durable state, restart and the verbs** (Codex r2 4, Opus r2 19). `local/merge-queue.json` holds the queue and the candidate: its
base, its members in order with their messages, each member's merge commit, its state (`forming`, `running`, `bisecting`, `green`,
`promoting`, `promoted`, `handed back`), the runner's process id and the run's directory. A runner that dies leaves it as it was;
the next runner reads it and goes on, **ancestry first** (as `resume` checks a batch today, `:4505`): a candidate in `promoting`
or `green` whose tip is an ancestor of main's tip **has landed**, and is recorded `promoted` before anything else is judged;
otherwise a candidate whose base is no longer main's tip is formed again, a run whose process is gone runs again (the units that
finished keep their standing verdicts), and a green candidate not yet promoted is promoted. The verbs act on the queue: `--continue`
promotes a candidate whose fast-forward the checkout refused; `--rerun` and `--stale` act on the candidate; `--batch` queues its
branches together; `--one-at-a-time` makes every candidate one member; `--drop-batch` takes queued branches off the queue, or ends
a candidate and hands its members back. The proof kills the runner before the fast-forward, between it and the record's write, and
after both, and shows each resumes without a second merge.

**Every path that moves main** (Codex 17). In this repository three do: the merge tool, which goes through the queue; the parent's
direct commits, which the next candidate's standing covers (point 3 above) and which are never staged without the background run
(§5.3); and nothing else, since the install lands as a branch (§1). A repository whose landing form merges into its default branch
gates it by its own rules; the queue does not claim it.

**The scope tree is apart from the gate tree**, so the background run (§5) never holds up a merge, and a merge never ends a
background run.

### 4.2 What it runs

The basis is **every unit with no pass standing on the candidate's tree** (Opus 4): a unit's newest verdict counts on this tree, or
on one whose changes since reach it not (GATE6's standing, per unit, as `classPlan` judges classes). A failure runs until it passes.
Of those, the scoped run runs:

1. **The cheap tier, always**: `universal` (the sensitive scan), `code-map`, `orient-index`, the census check (§3.4), the censuses
   `docs/development.md` lists (*Choose the checks*) whose files are reached, the records' checks (`daoris check`, the
   documentation tools, `release-prep --check`), and the words: the catalogue tests (`src/Daoris.Web/src/locales/*.test.ts`),
   `web-types`, `web-rules` and the CLI's typecheck where reached. `BLOCKING` and `PATH_CHECKS` are folded in. Priced at 2 to 4
   minutes (§2.5), measured by CISCOPE1e.
2. **A reached .NET fast half runs whole** (Opus 1). "Reached" is the lane table's gate-level reach (`REACH`), as today's CI
   judges the `driver`, `modules`, `service` and `devkit` gates. The name census is not a closure (§2.3), and a fast half's tests
   cost under two minutes (§2.4: 1 m 39 s, 1 m 43 s, 1 m 38 s, 20 s; the HTTP host's 3 m 17 s) beside a build that dominates. Run
   whole, it keeps the net that caught GENREGEN1's two censuses.
3. **The TypeScript suites by file**, where reach is a real closure (§3.2): the CLI's and the tool-tests' `node --test` files,
   the `tools/*.test.mjs` files `npm run verify` runs (16 on main at `b0d68c641`; Opus r2 16), and the page's vitest files that
   the candidate's paths reach. A whole-suite file (§3.4 check 5) or an unresolved read runs its suite
   whole.
4. **The driver's other Process half and the modules' Process half by class**, as PROCREACH1 runs them. The census there is the
   name census, not a closure, and they **keep the sweep**: each half is run whole at least once within `SWEEP_BOUND_MS` by the
   background run (§5.1), and the install refuses a commit whose kept verdicts are past it (`sweptOut`, §5.3). The scoped run never
   sweeps.
5. **The git half by reach within a budget** (Codex 13). The reached classes' predicted wall is their measured class times laid on
   four lanes, never less than the longest class, plus the half's measured overhead (build, host starts); a class with no measured
   time counts as the half's longest. When the whole reached set's predicted wall fits the budget, it all runs, whatever its
   subject. When it does not, the classes whose subject is a changed module come first, then the rest, each group in order of
   recent failures (`class-verdicts.json`) and then shortest first, until the budget is spent; CISCOPE1g's value order replaces
   that once it has a history. The budget is declared on the gate in `daoris.gates.json` (`"scope": { "budgetMinutes": 8 }`). It
   chooses classes; it never stops one. The run says which classes ran, which reached classes are **deferred** to the background
   run, and any overrun of the prediction.
6. **No browser spec and no rehearsal** (Codex 6). `web`, `web-measured` and their production build, and the three rehearsals,
   are deferred to the background run whenever reached. A merge never waits on them; the install does (§5.3).

**Retries are today's** (Codex 3): a Process half's failures are re-run alone once (FLAKE1); a fast .NET half's failure is real and
never re-run; the Node suites are not retried (D180). A build, discovery or host failure fails the run.

**Verdicts** (Opus r2 10, Codex r2 12). Each unit's verdict goes to a per-unit store, `local/unit-verdicts.json`, keyed by tree,
six kept a unit, which absorbs `class-verdicts.json` (Opus 16). The gate store keeps gate verdicts only, and every gate verdict
now records **the kind of run that gave it** (`scoped`, `background`, `stage`, `release`), which it does not today (`:4069`). A
scoped run writes a gate verdict **only for a gate it ran whole** (a fast .NET half, the cheap tier's gates), or for a Process half
run by class with nothing deferred and every class it kept standing; a suite run by file and a git half with deferred classes write
none. The background run's plan counts a scoped gate verdict as standing like any other, so it does not run again on the promoted
tip what a scoped run just passed whole there. What the stage counts is §5.3's: a background receipt on the commit, then the gate
verdicts of its plan, either kind. A scoped run also records its own run, `scoped`, green or red, with its deferred units, in
`local/scope-runs.jsonl`; the stage never reads that.

### 4.3 Where exactness gives way

The scoped run is as exact as its map, and round 2 says where the map gives way rather than claiming it does not (Opus 1):

- **The .NET Process halves**: the name census misses what a class reaches through code it does not name. Bounded by the sweep,
  within a day, and measured by the miss count (§3.5).
- **The git half**: the budget defers reached classes whenever the reached set's wall does not fit, which today is most `Trees`
  changes (§2.3).
- **The browser specs and the rehearsals**: deferred whenever reached.
- **The TypeScript suites**: exact as far as their closure, the declared edges and the wide fallback reach; the miss count measures
  what they miss.
- **The .NET fast halves**: run whole when reached, so a miss there is a gate-level reach the lane table lacks, the same as today.
- **A base moved by records only**: the re-formed candidate runs the record checks, and the `service` suite's scan of the
  documents is deferred to the background run (§4.1 point 3).

None of these reaches the install, which waits for the background run's own passes (§5.3), with no waiver. What a miss costs is
attribution: a later candidate finds it inherited, or the background run finds it; either files it as known red, the miss count
judges it against its introducing commit (§3.5), and it is fixed forward, as D180 accepts.

### 4.4 The expected wall

Rebased on measured walls (§2.2's ratio of 1.6 to 2.1 for a Process half), not on class time over width. The Process halves run
one after another in the Process lane, as GATEORDER1 has them. Per category of merge, before the queue's wait:

| A merge that changes… | Runs | Expected |
|---|---|---|
| records and documents only | the cheap tier; the `service` suite whole, which the records lane reaches (`:1234`, `:1249`): its build and about 1 m 38 s of tests | 5 to 9 min |
| the page | the cheap tier, the reached vitest files, `web-types`; the `service`, `driver` and `modules` fast halves whole, which the page lanes reach (`PAGE_READERS`, `:969`, `:1205`) | 8 to 14 min |
| the CLI or `tools/` | the cheap tier, the reached `node --test` files; the driver's fast half whole, which the CLI lane reaches (`CLI_READERS`, `:972`); `driver-process`'s reached classes where the CLI's lock helpers or the dispatcher's seam change (`:1178`, `:1186`) | 7 to 12 min; 13 to 20 with `driver-process` |
| the driver | the cheap tier; the driver's and the modules' fast halves whole (warm builds); the driver's other half by class (4.7 min by the model, 7 to 10 measured); the git half within its 8; the modules' half by class (1 to 2) | 15 to 28 min |
| a whole-suite file (`REACH`'s first rule, 6 of 80 merges) | everything above, every Process half whole but the git half's budget | about 30 min |

Round 2's first three rows left out the fast halves §4.2 point 2 runs (Codex r2 2, Opus r2 9); the figures above add a warm
incremental build of each reached project, about one to three minutes, and the half's own test time (§2.4). They are unmeasured as
a whole. **Carried between candidates**: a git-half class deferred, and a known-red unit a candidate reaches, run again in every
later candidate that reaches them until a pass stands, so a candidate after a deferral costs more than its members alone; a re-form
after a direct commit adds the units that commit reaches (§4.1). CISCOPE1e's proof times the next real merges per category, p50 and
p90, with the queue's wait apart, and the walls replace these.

### 4.5 No stage from the scope tree

Round 1's `--from-scoped` is withdrawn. A scoped verdict lets a merge into main; it never stages the install, and the stage keeps
`--from-gated`, which reads the gate tree's verdicts (§5.3). A publish build never holds the scope tree, so a stage never holds a
merge (Opus 9).

## 5. The full set in the background

### 5.1 Its schedule

`--ci --watch` is the background run. It runs in `local/gate-tree` as CI1 built it, a guest on the machine (CIPRIO1), its Process
lane at normal priority (FAMILYFAIL1c). Its schedule (Codex 7, Opus 17):

- **After each promotion**, main's tip, once the gate tree is idle, as the watch does today: one run covers every candidate since
  the last. This is the run a republish waits for.
- **The sweep**, from a durable record, `local/ci-sweep.json`: for each Process half, its last sweep and when its next is due
  (`SWEEP_BOUND_MS` after it). A half due is run whole by the next run, whatever its reach.
- **Nightly, unconditional**, at a set hour, on main's tip: every gate whole, the git half, the browser specs and the three
  rehearsals included, so a commit's deferred units are paid within the day even on a day of constant merges, when the idle window
  CI1 waits for rarely holds (Opus, the read's §2). A night missed (the machine asleep or off) is run at the next start, before any
  other background run.

**What each kind of run runs** (Codex r2 14, Opus r2 12). Round 2's "wherever a promotion reaches them" contradicted CIFAST1; the
matrix is:

| Run | On | Runs | The family and deployment rehearsals |
|---|---|---|---|
| ordinary (after a promotion) | main's tip | every gate with no pass standing on its tree, a due half whole | **owed**, as CIFAST1 has it (`:5456`); the release rehearsal runs where reached, as today |
| a due sweep with no promotion to ride | main's tip | the due half whole, and nothing else | not run |
| nightly | main's tip | every gate whole | **run**: the one mode that overrides CIFAST1's owing, amended in D180's CIFAST1 note on acceptance |
| stage (`--ci --stage <commit>`) | the commit to stage | what its verdicts lack, as today | run, as CIFAST1 has it |
| public release | the release workflow's own run (`.github/workflows/release.yml:234-321`) | every gate, itself | its own |

Round 2's local `--ci --release <commit>` is dropped (Opus r2 14): the public release's gate is the workflow's run of every gate,
unchanged.

**What satisfies the sweep and the night** (Opus r2 5). Today a failed whole run records a fresh verdict for every class, so
`classPlan`'s sweep is satisfied for a day whatever the half's verdict (`:5176-5180`). That stays: **a half's sweep is satisfied by
a run that gave every class of the half a verdict, pass or fail**, and **a night by a whole run that finished, its failures filed**.
**Only a run ended or killed before its classes ran** (a stop, a crash, a build failure that left the half unrun) leaves the sweep
or the night due. The watch still never gates a tip a run already gave a verdict on, so a red main is not run whole again and again;
its failures are known red (§5.2) and its next promotion's run runs what changed. **Overdue** is reported: the CI's status and the
parent's watch say a half whose sweep is past its bound, and a night that did not finish.

**The bound, stated honestly.** A change the scoped run missed is found within the sweep's bound plus a whole run's runtime (a day
and about one to two hours) **provided a whole run finishes**; when one does not, the overdue report says so. The stage reads the
same record (Codex r2 11): `sweptOut` alone expires only a pass that kept classes (`:1909`), so §5.3 point 2 adds the due record
and an age on a whole pass that counts by standing. The bound is on development's attribution, never on what the owner runs.

**Sharing the machine: one admission policy for both runners** (Opus r2 4, Codex r2 13 and 15). Round 2 said only that a background
group never starts while a scoped Process tier runs, and left the converse case: a class group is a whole half (D180's CI1 note, its
point 6) and a rehearsal cannot yield partway, so a scoped tier due while a background git half or rehearsal is already running
could only wait, end it, or run beside it. Round 3 chooses **run beside it**, and states it with the memory reservation in one
record, `local/admission.json` in the main checkout's `local/`, under its own lock, which both runners ask before every gate:

- **The Process lane is one resource for both runners.** While a scoped Process tier is running **or queued**, the background starts
  no Process-lane gate and no rehearsal.
- **The converse case.** A scoped Process tier due while a background Process-lane gate or rehearsal is already running **runs
  beside it**; its FLAKE1 re-run alone does not wait for that gate either. The background gate is never ended for it.
- **The scoped run's worst wait** on the background is therefore **none**. What it pays instead is overlap: at worst its whole
  Process tier beside one background gate already running, the longest being a git half run whole (31.5 to 43.6 minutes, §2.1),
  never a second, since none starts while it runs or waits. Its wall under that load, and any timing flake it raises, is what
  CISCOPE1e's proof measures; a flake there is reported as FLAKE1's are, never hidden.
- **Memory is reserved, not sampled.** Each gate reserves its MEMGATE1 need (`MEMORY_NEEDS`, `:2208`) in the record before it starts
  and releases it when it ends, fails or is killed; a holder whose process is gone is freed at the next read. A gate is admitted
  when the free memory sampled, less the part of every live reservation its holder's processes do not yet use (reserved minus
  their working set, never below zero), covers its need, so a reservation already in use is not counted twice. **The background
  also leaves 14 GB of foreground headroom** (a Process half's 8 and a heavy suite's 6) unreserved. A scoped gate past MEMGATE1's
  bound runs anyway, as today (`:2365`), and says so; a background gate never does: it waits.
- **Background progress.** A background Process-lane gate that has waited more than 60 minutes on scoped tiers starts at the next
  moment the scope runner is between tiers, and thereafter is the gate already running of the converse case. A night under
  continuous merges therefore finishes, later, and the report says how long it waited.
- **Where it lands**: in CISCOPE1e, with the scoped run that brings the second heavy runner (§8 row 5), not in CISCOPE1f.

The background run never holds the scope tree, never ends for a merge, and never blocks one.

### 5.2 Its failures, and known red

A background failure is filed rather than waited on. The run writes `local/ci-findings.json` (the commit, the unit, the test, its
fingerprint, the first lines of its failure, the scoped verdict that unit last had), and the parent files each as a row or a FIX-LOG
entry and fixes it forward (D180 point 3).

**Known red is keyed by test and fingerprint** (Codex 8, Opus 5), and has **one rule** (Opus r2 2, Codex r2 6):

- **How a test becomes known red.** Two ways, the same record: the background run finds it failing on main's tip; or a candidate
  finds it failing on its base with the same fingerprint (§4.1 point 4), which files it to `local/ci-findings.json` at once. The
  base is main's tip, so a failure a direct commit or a promoted miss introduced is known red from the first candidate that meets
  it, not from the next background run.
- **The fingerprint** keeps what tells two failures apart (Codex r2 6): the test's full name **with its theory case or data row**
  (today's readers cut the case off, `:2023`, `:2119`, and CISCOPE1d keeps it for the fingerprint), the exception type, **the
  assertion's location** (the first stack frame in the test's own file, file and line), and the first line of its message, numbers
  and paths normalised. For vitest and `node --test`: the full test path through its `describe` blocks and `each` row, and the
  assertion's location. Two cases of one theory, and two assertions with the same first line, are two fingerprints. A failure whose
  location cannot be read keeps its whole message, unnormalised.
- **Where it waives: only in the queue, and only on the base.** A candidate's failing test is inherited, and does not count against
  the candidate, only when the same fingerprint fails on the candidate's base. That run is the evidence, never the census's reach,
  so the exclusion holds whatever the census misses (Codex r2 10). A known-red test skips the base run only when the base is the
  commit it was last seen failing on. **The stage never waives** (§5.3).
- **What counts against a candidate**: a new failing test in the same class, a changed fingerprint, a build, discovery or host
  failure, and a known-red test that passes on the base.
- **Clearing**: a pass of the test on any run clears it.
- **Expiry, a deliberate exception** (Opus r2 11): a test known red for more than 72 hours is no longer inherited, and every
  candidate that reaches it is red, bisection pinning no member since its base fails. That is the blocking D207's *Rejected* list
  says known red exists to avoid, chosen here on purpose and recorded so in D207: a waiver must not outlive the fix-forward D180
  expects within a working day. The way out short of a fix is a **skip that names its row**: `[Fact(Skip = "<ROW>: <why>")]` or
  `test.skip('<ROW>: <why>', …)`, and the census check (§3.4) fails on a skip whose row is not open in `TASKS.md`.

### 5.3 What a republish of the owner's install stages

**The steward's direction for round 2**: the owner's install is republished only on a commit the background run has passed. It is
never staged on scoped-green alone.

The stage's judgement is `--passed` on the merge tool (`stageJudgement`, `:1926`), and **every ordinary stage entry point asks
it** (Codex r2 12): a plain `publish:desktop --stage` through `ungatedRefusal` (`desktop-publish.mjs:871`, `:1163`), and
`--stage --from-gated <commit>` through the child it hands the same flags (`:1002`, `:1137`). The rules below go into `--passed`,
so both paths meet them; `--force-ungated` stays the owner's explicit override, said in its output and recorded as forced in the
stage history below. A commit is staged only when:

1. **A background run has passed it, on that commit** (a *receipt*). Every background run appends its receipt at its end to
   `local/ci-runs.jsonl` in the main checkout's `local/`: the commit, its tree, the run's kind (§5.1's matrix), when it started and
   ended, whether it finished or was ended, and each gate's verdict with the kind of run that gave it. The file is durable, apart
   from the gate tree's record, which every run rewrites. The stage needs a receipt of a run that **finished** on that exact
   commit, and then **every gate of its plan passing with a verdict that stands on its tree**, as `stageJudgement` asks today: the
   git half's deferred classes, the browser specs and the cheap checks included. A verdict counts whichever kind gave it, under
   §4.2's rule for what a scoped run may write (a gate run whole, or a Process half by class with nothing deferred), since a gate
   run whole on the same tree is the same evidence; a commit no background run finished on is refused, named, with the run to wait
   for. **There is no waiver**: a known-red test fails its gate here as any failure does (Opus r2 1, Codex r2 10).
2. **The sweep is within its bound** (Codex r2 11): `sweptOut` stays; the stage also refuses while `local/ci-sweep.json` holds a
   Process half overdue; and a whole Process-half pass that counts only by standing (given on another tree) counts only when it
   was given within `SWEEP_BOUND_MS`. A pass given on the commit's own tree counts at any age.
3. **CIFAST1's rehearsals are paid**: the family and deployment rehearsals pass on its tree (`--ci --stage <commit>`), as today.
   Every driver-lane source reaches both, and every modules-lane source the deployment rehearsal (`:974`, `:976`), so every change
   to the install's update path pays the deployment rehearsal, and round 1's hand list of update-path files stays withdrawn (Codex
   10, Opus 10).
4. **Its census was read**: a commit whose census could not be read ran whole, and its stage waits for that.

Round 2's point 4, which let a gate whose only failures were known red count when the change since the commit last staged reached
none of them by the census, is **deleted** (Opus r2 1, Codex r2 9 and 10): it let an install out without a green run, which D180
rejects (D180.md:52), and it judged by the very census whose misses the background run backstops.

**The stage history** (Codex r2 9). The gate tree's `staged` field is no baseline (§1). Each successful stage appends to
`local/stage-history.jsonl` in the main checkout's `local/` (the commit, its tree, where it went, when, forced or not), which no CI
run rewrites and no prune deletes. With point 4 deleted no judgement reads it; the stage's report reads it to say what the stage
brings since the last (the commits, the gates' verdicts that changed). Where it cannot say (no history yet, a history that does not
read, a last staged commit that is not an ancestor of this one), the report says the range is unknown, and nothing the stage
decides depends on it.

**Why not sooner.** Round 1 justified staging on scoped-green by D139's rollback. That is withdrawn (Codex 9, Opus 2): once a staged
build confirms, `InstallSwap.Finish` deletes `update/previous/` (`InstallSwap.cs:374-376`), D139 excludes failures after
confirmation, and what the build wrote into the home's stores stays. A regression found after that is fixed forward on the
install the owner develops with (DOGFOOD1), so the full set is the gate, as D180 point 4 and CIFAST1 already have it. Development
does not wait for it: merges go on through their scoped runs, and only the republish waits.

**A public release** keeps its own gate: the release workflow runs every gate itself on the exact commit
(`.github/workflows/release.yml:234-321`), unchanged by this design (§5.1).

## 6. The rehearsals

### 6.1 Out of the per-merge path

No scoped run runs a rehearsal. The background run runs all three on every nightly pass; an ordinary run after a promotion runs
the release rehearsal where reached and owes the family and deployment rehearsals, as CIFAST1 has it (§5.1's matrix; round 2 said
"wherever a promotion reaches them", corrected, Opus r2 12, Codex r2 14); and the stage pays CIFAST1's two on the commit it stages.
The lane table keeps sending paths to them; that reach decides what a stage's `--ci --stage` must run and what the background run
runs, never what a merge waits for.

### 6.2 Side by side

REHLANE2 and REHLANE2b keep the three apart, one after another, for one reason each: the release and deployment rehearsals both pack
the CLI's folder in place (`npm pack`, `tools/release-rehearsal.mjs:58`; the stager's `--clean`, `:96`). The family and deployment
rehearsals both run the CLI's bin, which prefers a staged `dist/` (D180's REHLANE2 note). That is state in **the tree**, so each
rehearsal gets a tree of its own: `local/gate-tree-rehearse-<name>` worktrees at the commit, kept between runs and reset as the gate
tree is, their dependencies installed from the lockfiles when a lockfile changes (GATEWT1). CISCOPE1f takes over REHPAIR1, which
CIFAST1 filed for the family and deployment pair (Opus 21).

What separate trees do not solve, and what CISCOPE1f adds before any two run together (Codex 16):

- **Ports**: `takePorts` picks free ports within one call and reserves none (`tools/rehearsal-kit.mjs:258-279`; `portRefusal`
  asks again before a host starts). Two runs beside each other can pick the same free port. CISCOPE1f adds a machine-wide claim:
  each port taken is written, with the run's process id, to a claims file under the main checkout's `local/` under a lock, and a
  claim whose process is gone is freed.
- **Memory**: MEMGATE1 samples per gate, reserves nothing, and runs a gate anyway after its bound (`memoryGate`, `:2331`). Three
  rehearsals ask 30 GB, a background Process half 8, a scoped run's Process half 8 and a heavy suite 6: 52 of 63.7 GB before five
  workers and the owner's install (Opus, the read's §2). The admission record of §5.1, which CISCOPE1e brings, reserves each
  rehearsal's need like any gate's; CISCOPE1f only lets two rehearsals hold reservations at once, where today they never do.
- **Homes, pipes, mutexes, builds**: apart already. Each rehearsal sets `DAORIS_HOME` under its own scratch
  (`tools/family-rehearsal.mjs:69`, `tools/deployment-rehearsal.mjs:486`); the instance pipe is named by the home's id
  (`PipeName.For`, `Daoris.Desktop.Modules/Platform/InstancePipe.cs:244`) and the host's mutex by its service address
  (`HostSupervisor.cs:505`); each builds .NET into an output of its own with build servers of its own
  (`tools/rehearsal-kit.mjs:50`, `:76`).

**The fallback stays until it is proven**: the serial chain stays the default, and side by side is turned on only once CISCOPE1f's
collision tests pass and five timed background runs on working days, beside the scope runner's merges, hold every bound
(FAMILYFAIL1, REHLANE2b's note) and show the scoped runs' walls unchanged. Expected on a quiet machine: the rehearsal lane's wall
falls from the chain's sum (31 to 53 minutes) to its longest member, about 23 minutes; on a working day §5.1's admission record will
serialise some of it, and the proof states the measured wall. Side by side, a background rehearsal is still one Process-lane gate
for §5.1's admission: none starts while a scoped Process tier runs or waits. The three trees' first installs and cold builds are not priced here;
CISCOPE1f measures them once, and they are paid again only when a lockfile changes. With the rehearsals side by side, a stage's
`--ci --stage` waits for the longer rehearsal, not their sum.

## 7. Each test's value: TESTVALUE1 folded in

### 7.1 What is measured, per unit

- **Runtime**: its own time from the runner's report (trx durations per class and test, vitest's JSON report, `node --test`'s),
  kept per run.
- **Attempt-level history** (Codex 14): `local/test-history.jsonl`, append-only, one line per **test attempt** per run (tree,
  commit, run kind, attempt: first or alone, verdict, fingerprint, time), written by every scoped and background run. It is kept
  for ninety days and compacted beyond to one line per test per day (Opus 16).
- **A catch** is a failure whose fingerprint is new, that the scoped run's bisection or the base re-run attributes to one commit,
  and that is cleared by a later commit linked to it by its row or FIX-LOG entry, that commit changing a non-test file. A
  **recurring flake** (a failure that passed alone, twice or more in the window) counts toward catches, since a real race first
  shows as a flake (Opus 8).
- **Overlap**: the lines and branches each unit covers, recorded in the background, and recorded again when anything the unit
  reaches changes (its own file, its reach, its fixtures, by the census). For .NET: one filtered run per class with
  `--collect "Code Coverage;Format=cobertura"`, whose collector `Microsoft.NET.Test.Sdk` already brings (`Microsoft.CodeCoverage`).
  For vitest: one run per file with `@vitest/coverage-v8`, a new development dependency, so CISCOPE1g's lockfile change. For the
  `node --test` suites: Node's own coverage (`--experimental-test-coverage`) per file. **Excluded, named**: the Playwright specs
  and the rehearsals, which this rule never prunes. A unit's **unique coverage** is the lines and branches it covers that no other
  unit does.
- **The cost, estimated** (Opus 20), for one whole recording: about 2,000 .NET classes, each a test-host start of a few seconds
  beside its tests; 423 vitest files, each its own jsdom; the git half's 158 class-minutes four wide. Roughly five to six hours of
  machine time, spread over nights and re-recorded only for units whose reach changed. CISCOPE1g measures it on one suite first and
  states the real figure before recording the rest.

### 7.2 The rule

- **The report nominates; a person or a session decides** (Codex 15). Value is unique coverage and catches per minute of runtime.
  The scoped run's git-half order uses it once CISCOPE1g has a history (§4.2 point 5).
- **Nothing is pruned before the window matures**: 30 days or 20 runs that reached the unit, whichever is longer.
- **Remove** a nominated unit with no unique coverage and no catch over the window, **one at a time**, uniqueness recomputed after
  each (a pair that covers only each other is not removed together; Opus 8). Each removal records which retained assertions or
  cases keep each obligation the removed one checked (its outputs, ordering, persistence, refusals), beside the line-and-branch
  guard.
- **Coverage counts within its currency** (Opus 8): a Process unit counts as covered only by other Process units. A unit that
  starts a program or reads a file in another language (a twin, a census, a golden, a vitest parsing `NoteCodes.cs`) may be merged
  into a table, never removed or moved down a tier.
- **Merge** units that cover the same lines with different data into one table-driven test (a theory, a `test.each`), every data
  row kept.
- **Move down a tier**: a Process class whose unique coverage a fast-half stand-in can reach becomes a fast-half test, one
  representative real-process case staying, and only when the stand-in checks the same obligations, recorded as for a removal.
- **Sentences**: a test that pins an English sentence where the words are not its subject asserts the catalogue key or the code
  instead; the golden and catalogue tests keep the words (TESTVALUE1's own clause).
- **Never** remove the last unit covering a line or a branch. Each pass carries the suites' coverage before and after.

### 7.3 The target

- **The git half: its whole reached set within the budget's predicted wall** (§4.2 point 5), so a `Trees` change runs whole in a
  scoped run. At four wide and eight minutes that is under 32 class-minutes before overhead, about 28 after it (round 1's 40 did
  not fit; Codex 13, Opus 7 and 13). There is no measured basis yet that 158 can come down to 28 (Opus); CISCOPE1g's report states
  what the rule can reach, and TESTVALUE1 sets the figure from it. Missing the target costs attribution, not safety, since the
  install waits for the background run.
- **The driver's other half and the modules': no target on time.** They fit already. The rule runs on them for upkeep.
- **The fast suites: the redundant set removed, and no sentence pinned outside the golden and catalogue tests.** The count is
  whatever §7.1's measure finds redundant, not a figure chosen before it. The planning figure is a quarter of the driver's 14,114 and
  the page's 9,145, and the proof states the measured one.

## 8. Rows, in build order

Each lands alone, with its proof, and **no backstop is removed before its replacement exists** (Codex 12, Opus 6). Until
CISCOPE1e, a merge blocks as today and the watch gates every merge's tip. The install's stage is today's until CISCOPE1d, which
only adds refusals to it; no row loosens it.

1. **CIFAST1** (landed, `0613afde2`): a plain `--ci` runs every gate but the family and deployment rehearsals, owed for a stage,
   and `--ci --stage` runs them on the commit it stages. It removed 44.1 minutes from `6ce6235`'s critical path (§2.1); a stage
   still pays them in turn. Proof: its own.
2. **CISCOPE1b — a half whose classes all stand passes without running, when nothing was hidden.** `classPlan`'s third whole rule
   (`:5181`) composes the half's verdict from its kept classes **only** when the half's newest gate-level verdict is a pass that
   went stale by reach (Opus 15, Codex 11): it ran to its end, its results are complete, and the census matched the runner's
   classes. A half that failed outside a test, a run with missing results and a discovery disagreement still run whole. The sweep
   (`:5178`) and `sweptOut` are unchanged. Proof: `classPlan`'s tool-tests, the refusal cases among them (a half that failed
   outside a test with every class kept; a trx missing; a class the census lacks), and `f296996`'s case replayed with zero classes
   run (31.5 minutes there). That replay holds (Opus r2 15): the run before it, `73c1fe0`, failed on `cli` and `driver-process`
   (`DriverHostTests.A_bare_invocation_prints_the_usage_and_starts_nothing`, failing again alone), and its git half ran whole and
   **passed**, so at `f296996` the half's newest gate verdict was a pass gone stale by reach, which is the case the narrowed rule
   composes.
3. **CISCOPE1c — the census of every test unit, and its recall** (§3): `tools/test-map.mjs`, reach and subject for every suite,
   absorbing `tools/process-reach.mjs`; the declared edges, the wide fallback and the expected-edge fixtures; its `check`, run in
   the CI's run and as a cheap check of a merge; the fixture rules of `REACH` derived and held. It **adds** `check` to the CI's run
   and to a merge's blocking checks, and changes nothing else either runs (round 2 said it changed nothing the CI runs, corrected,
   Opus r2 16). Proof: the inventory matching runner discovery on the same tree; §2.2 replayed with the tool, its figures within a
   tenth of these; **the recall proof** of §3.5 over 2026-09-24 to 2026-10-24, with the selector pinned and run on the historical
   trees, each failure's introducing commit and its verdict, the recall as a lower bound overall and over the FIX-LOG apart, each
   source's span and count, and the first list kept beside the list after repair.
4. **CISCOPE1d — the background run's schedule and its safeguards** (§5.1, §5.2, §5.3): `local/ci-sweep.json`, the nightly run
   with its catch-up, the run-kind matrix, a sweep and a night satisfied by a run that finished whatever its verdict, the overdue
   report; `local/ci-findings.json`; known red by test and fingerprint, with the fingerprint's case and location, clearing, expiry
   and the census check's hold on a skip that names its row; the run kind on every gate verdict, `local/ci-runs.jsonl`'s receipts,
   `local/stage-history.jsonl`, and `--passed`'s new rules at every stage entry point (the receipt, the due record, a standing whole
   pass's age); `local/unit-verdicts.json` and `local/test-history.jsonl` as attempt-level records; the miss count, structural until
   CISCOPE1e. The sweep stays in `classPlan` until the record replaces it in the same row, and the stage keeps `sweptOut`. Proof:
   tool-tests for the due record (a night missed and caught up; a red whole run satisfying the sweep; a run stopped before its
   classes ran leaving it due; an overdue report), for known red (a second failing test in a known-red class counts; a changed
   fingerprint counts; another theory case and another assertion with the same first line are new fingerprints; an unfiled failure
   on the base filed at once; expiry; a skip naming a closed row refused), and for the stage (a direct `--stage` and a delegated
   `--from-gated` child each refused on scoped-only evidence, with no receipt, with a ci-sweep record overdue, with an old whole pass
   standing by reach and an older one behind it, with a known-red failure, with rehearsals unpaid; stage, then a prune and another
   CI run, then a later stage reading its baseline from the history; a first stage and a non-ancestor reporting an unknown range).
5. **CISCOPE1e — the scoped run, the merge queue and the admission between the runners** (§4, §5.1): `local/scope-tree`, the queue
   and its candidates, `--preview` and `--message`, the records commit on the branch, the detached runner, batching, the base
   re-run, the build failure apart, bisection by class or file with its outcomes, the retained candidate judged again, promotion
   recorded by ancestry, the records-only re-form, the verbs, `local/scope-runs.jsonl`; the scoped set of §4.2 with the git half's
   budget in its conservative order and the deferred list; `BLOCKING` and `PATH_CHECKS` as its cheap tier; **`local/admission.json`,
   the one admission policy for both runners and the memory reservation** (§5.1), which lands here because this row starts the
   second heavy runner (Opus r2 4, Codex r2 15). GATESLIM1's "listed and skipped" becomes "run if reached, else deferred". The
   background run keeps gating every promoted tip, and the stage's rules are CISCOPE1d's, so nothing that guards the install
   changes. Proof: tool-tests for the queue (two branches in one candidate; a conflict set aside mid-batch and queued again; a red
   candidate of four bisected to one member; a failure two members cause together, pinned with the first; two independent
   failures; a build failure; an unresolved failure run one member at a time; an inherited failure filed and pinned to none; main
   moved by a direct commit and by records only before promotion; the runner killed before the fast-forward, between it and the
   record, and after both, resuming without a second merge; a checkout refusing the fast-forward; a branch queued without a
   message refused); for admission (a background half already running when a scoped tier starts, and the scoped tier's FLAKE1
   re-run beside it; both acquiring at once; a background gate starting after its 60-minute wait under sustained scoped load; a
   reservation counted once against the memory its holder already uses; a reservation released after a failure and after its
   holder is killed; restart); 2026-10-21's 127 merges and 41 direct commits replayed against the queue at their timestamps, the
   backlog and each merge's wait reported, p50, p90 and the worst; the next real merges timed per category against §4.4, p50 and
   p90, **with a background git half already running for some of them**; a red candidate leaving main where it was; the cheap
   tier's wall measured.
6. **CISCOPE1f — the rehearsals side by side** (§6.2, REHPAIR1 taken over): the rehearsals' trees and the port claims, with two
   rehearsals allowed to hold admission reservations at once. The serial chain stays the default until the proof holds. Proof:
   collision tests (two runs given the same port start); five timed background runs on working days with the three side by side,
   every bound holding and the scoped runs' walls beside them unchanged; the trees' first install and build priced.
7. **CISCOPE1g — the value measures** (§7.1): the history's report, per-unit coverage in the background for every runner but the
   named exclusions, a value report (`tools/test-value.mjs`), the coverage pass's cost measured on one suite first. It can land
   before CISCOPE1e. Proof: the report over every suite, its figures reproducible from the history.
8. **TESTVALUE1** (re-scoped by §7.2 and §7.3): after CISCOPE1g and once the window has matured, the passes, the git half first.
   Proof: TESTVALUE1's own, each removal's recorded obligations, and the git half's class time against the target §7.3 sets from
   CISCOPE1g's report.

## 9. What it changes

- **D115** (the gates of a landing, GATE3, GATESLIM1's second part): a merge waits on its scoped run, in a tree of its own, through
  the queue. GATESLIM1's "listed and skipped, in the CI beside" becomes "run if reached, else deferred to the background run".
  D115's "full set before a stage" holds for the install's stage and for a public release, unchanged.
- **D180**: point 1's blocking set grows from the cheap checks to the scoped set, still never in the main checkout. Point 2's "every
  other gate runs beside" becomes the background run. Point 4's three lines become the builder's own tests, the scoped run, and the
  background run, the install's stage reading the third, as today. Its rejection of "letting an install out without a green run"
  stands. The budget note's "a full set under thirty minutes" applies to the background run, and the per-merge wait is §4.4's.
- **CI1**: `--ci --watch` is the background run with its schedule (§5.1). It still gates each promoted tip, coalescing the
  candidates since its last run.
- **GATESLIM1**: `BLOCKING` and `PATH_CHECKS` fold into the scoped run's cheap tier. `--gated` is the scoped run, and `--full` is
  unchanged.
- **PROCREACH1**: its census becomes CISCOPE1c's for every suite, at the same area grain (§2.3), and its class choice applies in the
  Process halves only. Its sweep moves from the CI's per-run plan to the background schedule's due record (CISCOPE1d), the stage
  keeping `sweptOut`. Its third whole rule is narrowed, not removed (CISCOPE1b). A subject and a budget join reach for the git half.
- **CIFAST1**: unchanged for an ordinary run, which still owes the family and deployment rehearsals, and for the stage, which
  pays them. One mode overrides it: the nightly run runs them (§5.1's matrix), recorded in D180's CIFAST1 note on acceptance.
  *Owed* keeps its meaning; the scoped run's word is *deferred*.
- **The stage** (`stageJudgement`, `--passed`): stricter, never looser. It adds a background run's receipt on the commit, the
  sweep's due record and a standing whole pass's age, at every stage entry point (§5.3); it waives nothing.
- **The public release**: unchanged; the release workflow's own run of every gate.

Until D207 is accepted, none of these changes anything. On acceptance, each of D115 and D180 gets a dated note pointing here.

## 10. For the readers of round 3

1. **The queue's attribution** (§4.1 point 4): does bisection by class or file over prefixes, with *pinned with*, the build failure
   apart, *unresolved* run one member at a time and the retained candidate judged again, place every failure a candidate can
   produce, or hand back none wrongly?
2. **Where a member's commit comes from** (§4.1): is a records commit on the branch with a `--message` file a workable shape for
   the parent, and is deferring the `service` suite on a records-only re-form an acceptable give-way?
3. **Admission** (§5.1): is running a scoped Process tier beside a background gate already running, with none started while a
   scoped tier runs or waits and a 60-minute progress bound, the right choice over short background class groups?
4. **The stage's evidence** (§5.3): is a receipt of a finished background run on the exact commit, with verdicts of either kind
   under §4.2's writing rule, what "the background run has passed it" should mean?
5. **Recall** (§3.5): is a lower bound counting unsettled ranges as missed, with deferred, unscoped and execution failures apart,
   the measure the foundation needs?

## 11. The reads

### 11.1 Round 1's reads, answered in round 2

Both reads are verbatim in `docs/2026-10-24-ci-scope-reads.md`. Every finding was checked against the code on main at `b61325e6c`
(CIFAST1 merged) and held; none was answered by disagreeing with its facts. *Taken* means the read's named fix, or the fix both
reads agree on; *taken, narrowed* or *answered* says how round 2 differs. Where the steward's direction for round 2 settles a
finding, the verdict says so.

**Codex (17 findings, 14 blocking; verdict another round).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | The 74-minute saving does not follow from the table | Taken: 44.1 of 74.3 were the rehearsals; the rest the light lane a rehearsal waits for (`:647`) and preparation; CIFAST1's saving stated as 44.1; D207 corrected | §2.1, §8 row 1, D207 note |
| 2 | Page, CLI and tools changes can reach Process classes; the estimate has no upper bound | Taken: the CLI's lock helpers and the dispatcher's seam reach `driver-process` (`:1178`, `:1186`), the first rule reaches all; estimates per category, the whole-suite case among them, rebased on measured walls; the proof reports p50 and p90 | §2.2, §4.4, §8 row 5 |
| 3 | Retrying every unit is not FLAKE1's policy | Taken: today's retry classes kept (a Process half's failures alone once; a fast .NET failure never, `:3559`; no Node retry, D180) | §1, §4.2 |
| 4 | The map is not complete and the census cannot certify it | Taken: the TypeScript reach as a repository closure with folder, basename and glob edges; declared edges for reflection, scans, generators and cross-project words; an unresolved read wide; expected-edge fixtures per kind; recall measured separately (§3.5), and the .NET fast halves run whole since the name census is not a closure | §3.2, §3.4 checks 5-6, §3.5, §4.2 point 2 |
| 5 | Comparing with the last whole run rejects added tests | Taken: a scoped run compares the candidate's census with runner discovery on the same tree; the whole-report comparison runs in the background against its own reports; unreadable discovery runs the suite whole | §3.4 check 1 |
| 6 | Browser checks and builds have no scoped disposition | Taken: `web`, `web-measured` and their build are deferred whenever reached, said as a place exactness gives way | §4.2 point 6, §4.3 |
| 7 | Nightly does not establish the detection bound | Taken: a durable due record, an unconditional nightly run with catch-up, a failed run satisfying nothing, overdue reported; the bound stated as conditional on a whole run completing, with its runtime | §5.1 |
| 8 | Known red can mask a new regression | Taken: keyed by test and fingerprint, confirmed on the base; new tests, changed fingerprints, build and discovery failures count; clearing and a 72-hour expiry | §5.2, §4.1 point 4 |
| 9 | Staging overstates rollback | Taken, by the steward's direction: "reversible" withdrawn; the install stages only on the background run's pass; `InstallSwap.Finish`'s deletion stated | §1, §5.3 |
| 10 | The update-path list is incomplete | Answered: the list is withdrawn, since every stage now needs the background run and CIFAST1's rehearsals, and every driver-lane and modules-lane source reaches the deployment rehearsal (`:974`, `:976`) | §5.3 point 3 |
| 11 | Composing class passes can erase a gate-level failure | Taken: compose only when the half's newest gate-level verdict is a pass gone stale by reach, results complete, discovery matching; the refusal cases in the proof | §8 row 2 |
| 12 | Earlier rows consume later safeguards | Taken: the background schedule and known red (row 4) before the scoped run (row 5); the sweep kept until its record replaces it in the same row; no `--from-scoped`; a conservative git-half order until the value measures land | §8 |
| 13 | The git target does not fit the budget, and selection is never exact | Taken: the whole reached set runs when its predicted wall fits, whatever its subject; the prediction uses measured times, the longest class and overhead, an unknown time counting as the longest; the budget never stops a class; the target restated as about 28 class-minutes | §4.2 point 5, §7.3 |
| 14 | The value measures omit attempt-level and Node evidence | Taken: attempt-level history with fingerprints, catch attribution through bisection and the fix's link, Node's coverage per file, named exclusions, invalidation by reach, no pruning before the window | §7.1, §7.2 |
| 15 | Equal coverage does not prove a test adds nothing | Taken: the report nominates; each removal records the obligations kept; data rows kept when folding | §7.2 |
| 16 | Rehearsal concurrency assumes isolation the code lacks | Taken: a machine-wide port claim and one memory ledger across both runners; the serial chain the default until collision tests and five loaded runs pass | §6.2, §8 row 6 |
| 17 | The candidate assumes an exclusive landing | Taken, with one fact corrected: in this repository the install lands as a branch and never moves main (DOGFOOD1), so the paths are the queue and the parent's direct commits, both covered (standing; main-moved re-forming); the candidate and base durable; restart, cancellation, conflict and the verbs specified; the cases in the proof | §1, §4.1, §8 row 5 |

**Opus foundation read (21 findings, 8 blocking; verdict another round).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | The map is not exact for .NET | Taken as named: §4.3 says the .NET reach is one level of naming bounded by the sweep; a reached fast half runs whole; unit scoping in the TypeScript suites and the Process halves | §2.3, §3.2, §4.2, §4.3 |
| 2 | The install's floor is too low | Taken, by the steward's direction, which goes past the read's floor: the background run's pass, the sweep in bound, CIFAST1's rehearsals paid, nothing known red its change reaches; "reversible" dropped; no `--from-scoped` | §5.3 |
| 3 | No queue model for merges | Taken as named: the measured arrival rate; one candidate for merges arriving during a run, on the union of their reach, bisected on red | §2.7, §4.1 |
| 4 | The scoped set's basis, and direct commits | Taken: every unit with no pass standing on the candidate's tree; the refusal of an unjudged ancestor is answered by the stage reading only the background run, which judges every commit | §4.2, §5.3 |
| 5 | Known red keyed by unit | Taken: re-run on the base; only a test that fails there with the same fingerprint is inherited | §4.1 point 4, §5.2 |
| 6 | Row order | Taken: CISCOPE1b only the third rule, narrowed; the sweep's move with the background schedule (row 4); no `--from-scoped`; the git half ordered by recent failures then shortest first until the value measures | §8 |
| 7 | No proof of recall | Taken as named: the month's replay at each introducing commit, the GENREGEN1 entry among the failures; the running miss count | §3.5, §8 row 3 |
| 8 | TESTVALUE1's currency | Taken: Process covered only by Process; cross-language and program-starting units merged, never removed or moved down; one removal at a time; recurring flakes count | §7.1, §7.2 |
| 9 | What a stage waits for, and where | Taken: one rule (§5.3); the stage builds from the gate tree as today and never holds the scope tree | §4.5, §5.3, §6.1 |
| 10 | The update-path list is incomplete | Answered as Codex 10: the list is withdrawn | §5.3 point 3 |
| 11 | `owed` collides with CIFAST1 | Taken: *deferred* | §4.2, §9 |
| 12 | Claims 3 to 6, 8 and 9 are wrong | Taken: each corrected (claim 3 §2.1, 4 §1, 5 §2.5, 6 §2.2 and §4.4, 8 §2.1, 9 the D207 note) | as named |
| 13 | The budget arithmetic | Taken: the target restated against the budget's predicted wall | §7.3 |
| 14 | TypeScript reach stops too early | Taken as named | §3.2 |
| 15 | The composed verdict can hide a failure | Taken as named | §8 row 2 |
| 16 | The verdict store overflows | Taken: a per-unit store apart from the gate store; the history's retention | §4.2, §7.1 |
| 17 | Background schedule and sharing | Taken: an unconditional night with a stated bound; no background Process group while a scoped Process tier runs | §5.1 |
| 18 | Census checks 1 and 4 | Taken: check 1 bound to its tree; check 4 sized and narrowed to the git half's root classes | §3.4 |
| 19 | The candidate breaks the tool's verbs | Taken: each verb on the scope tree and the queue; main moved re-forms the candidate | §4.1 |
| 20 | Coverage runs are not priced | Taken: estimated, measured on one suite first | §7.1 |
| 21 | REHPAIR1 and the side-by-side cost | Taken: CISCOPE1f takes REHPAIR1; the trees' cost measured once; the working-day wall the proof's | §6.2, §8 row 6 |

**The Opus read's claims in §1** answered beside its findings: claim 1 is finding 1; claim 2 is finding 2; claim 7 is finding 13.
Its §2 note that coalescing lowers total work is taken in D207's note: per-merge runs buy attribution and a short wait, not less
work, and the queue's batches recover some of it.

Round 2 rewrote §4.1, §4.2, §5.3 and §8, which both round-1 reads named as design work, and asked both readers again.

### 11.2 Round 2's reads, answered in round 3

Both reads are verbatim in `docs/2026-10-24-ci-scope-reads.md` (*Codex, round 2*; *Opus (foundation), round 2*), both at
`aceced648`. Every finding was checked against the code on main at `b0d68c641`, where each line they cite is unchanged, and each
held; none is answered by disagreeing with its facts. The Opus read's six blocking findings are taken as it names them. The round-1
findings each read left open (Codex's §1 *Open* rows, the Opus read's *incomplete* rows) point to findings below and are answered
there.

**Codex, round 2 (15 findings, 11 blocking; verdict another round).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | The modules lane does not reach the family rehearsal | Taken: corrected; `MODULES_READERS` is `modules`, `modules-process`, `deployment` (`:974`) | §1, §5.3 point 3 |
| 2 | The non-driver cost rows omit the whole .NET halves | Taken: `service` for records, `service`, `driver`, `modules` for the page, `driver` for the CLI (`:969`, `:972`, `:1205`, `:1234`); repriced, unmeasured as a whole | §4.4 |
| 3 | Failing-test bisection does not cover the failures a scoped run produces | Taken: build failures bisected by prefix builds, a broken base stopping the queue; bisection by class or file; *pinned with* for members that fail together; independent failures side by side; *unresolved* run one member at a time; the retained candidate judged again; the three-step claim limited to one reproducible failure over eight | §4.1 point 4, §8 row 5 |
| 4 | Recovery is not idempotent across promotion; conflict ownership contradicts itself | Taken: `promoting` written before the fast-forward, ancestry checked first on restart (as `:4505`), crash probes on both sides; one conflict rule, set aside and handed back, none left open in the scope tree | §4.1, §8 row 5 |
| 5 | Batching does not establish a two-run bound | Taken: called an estimate; the replay adds direct commits, union costs, overflow, red and restart cases, and reports backlog and tail waits | §4.1, §8 row 5 |
| 6 | Known-red eligibility is inconsistent; the fingerprint merges failures | Taken: one rule, a base failure inherited and filed at once; the fingerprint keeps the theory case and the assertion's location, against today's readers (`:2023`, `:2119`); the three proofs named | §5.2, §8 row 4 |
| 7 | Replay and counter use selection in a range as coverage | Taken: judged against the introducing commit alone, found by running; only the promoted candidate's run read; deferred, unscoped, execution and unsettled counted apart; one regression counted once | §3.5 |
| 8 | The proof lacks the inputs to replay scheduling | Taken: the selector pinned and run on historical trees (`process-reach.mjs:819`); structural reach replayed apart from scheduling, whose state is declared unknown and proven by fixtures; the first results kept | §3.5, §8 row 3 |
| 9 | `staged.commit` is not a durable baseline | Taken, and its use removed: the gate tree's record is rewritten by every run (`:5486`, `:5536`) and pruned (`:3106`); the stage history is a file of its own; with the waiver deleted no judgement reads it, and the unknown cases are reported; the prune-and-run proof named | §1, §5.3, §8 row 4 |
| 10 | The install's known-red exception trusts the incomplete map | Taken: the stage waives nothing; known red waives only in the queue, by a base run, never by the census | §5.2, §5.3 |
| 11 | `sweptOut` does not enforce the overdue promise | Taken: a whole pass never expires (`:1909`), so the stage reads the due record and ages a whole pass that counts only by standing; the two-pass proof named | §5.1, §5.3 point 2, §8 row 4 |
| 12 | Background proof and every stage consumer not specified together | Taken: a receipt of a finished background run on the exact commit; a run kind on every gate verdict (`:4069` has none); the rules in `--passed`, which both plain `--stage` and the `--from-gated` child ask; `--force-ungated` the owner's override, recorded | §4.2, §5.3, §8 row 4 |
| 13 | One-sided admission guarantees neither non-overlap nor progress | Taken: symmetric admission with the converse case run beside, no background start while a scoped tier runs or waits, a 60-minute progress bound, the scoped run's worst wait stated as none and its overlap measured; the five proofs named | §5.1, §8 row 5 |
| 14 | The background rehearsal policy contradicts CIFAST1 | Taken: a matrix of run kinds; only the nightly run overrides CIFAST1's owing, recorded as an amendment | §5.1, §6.1, §9 |
| 15 | Cross-run memory admission comes after cross-run execution | Taken: the admission record and the reservation move to CISCOPE1e; pairing and its five-run proof stay in CISCOPE1f; accounting without double counting, release after failure and a killed holder proven | §5.1, §6.2, §8 rows 5-6 |

**Opus foundation read, round 2 (17 findings, 6 blocking; verdict accept after named changes).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | §5.3 point 4 lets an install out on a run that is not green | Taken as named: the waiver deleted; every gate passing, as `stageJudgement` (`:1926-1956`); known red waives only in the queue | §5.3, §9, D207 |
| 2 | A failure on the base not yet known red has no rule | Taken as named: inherited for that candidate, known red or not, filed at once as known red with its 72 hours starting, never pinned to a member; *known red* only decides whether the base run can be skipped | §4.1 point 4, §5.2 |
| 3 | The miss count credits a miss to a later scoped run | Taken as named: judged against the introducing commit alone, found by running over main's range; fed from inherited failures as well as the background; direct commits apart as unscoped; an unsettled range missed, in a stated lower bound; before CISCOPE1e the count said to measure `BLOCKING`-only merges structurally | §3.5 |
| 4 | Machine sharing contradicts itself when a background gate already runs | Taken as named, the first option: a scoped tier runs beside a background Process-lane gate already running, its FLAKE1 re-run not waiting; the background starts none while a scoped tier runs or is queued; the worst wait stated; the coordination and the memory reservation moved into CISCOPE1e, whose proof times scoped runs beside a running background half | §5.1, §8 row 5 |
| 5 | A failed run satisfying nothing runs the background whole through every red period | Taken as named: a sweep satisfied by a run that gave every class a verdict, a night by a whole run that finished, failures filed; only a run ended before its classes ran leaves them due | §5.1, §8 row 4 |
| 6 | Who commits a candidate, and where the message and records come from | Taken as named: "the merge itself is today's" corrected (`:69`, `:4517`); the parent reads the diff by `--preview`, hands a message file and a records commit on the branch; the tool commits nothing it was not handed; records after promotion are a direct commit, and a records-only base move runs only the record checks | §1, §4.1, §4.3 |
| 7 | Bisecting tests alone cannot place a failure that needs its suite | Taken: the whole class or file on the base and on each prefix, members one at a time as the fallback; no unit verdict from these runs (FLAKE1, `:245-249`) | §4.1 point 4 |
| 8 | The runner and its conflicts | Taken: a detached runner as `--ci --watch`; one conflict rule, handed back | §4.1 |
| 9 | §4.4 omits the fast halves and the carry-over | Taken: the halves added, the carry-over and the re-forms stated | §4.1, §4.4 |
| 10 | Which verdicts a scoped run writes to the gate store | Taken: a gate verdict only for a gate run whole or a Process half by class with nothing deferred; a run kind on every verdict; the background counts them as standing; "passed" means a receipt plus standing passes of either kind | §4.2, §5.3 point 1 |
| 11 | The 72-hour expiry blocks every merge that reaches the test | Taken: recorded in D207 as a deliberate exception; the skip names its row, held by the census check | §5.2, D207 |
| 12 | Two wrong claims | Taken: both corrected (`MODULES_READERS`; the watch owes the two rehearsals) | §1, §6.1 |
| 13 | D207's body and Changes are still round 1 | Taken: both rewritten to round 3 | D207 |
| 14 | The public-release rule has no row | Taken, the second option: the release workflow's own run of every gate (`release.yml:234-321`) is the release's gate; the local `--ci --release` dropped | §5.1, §5.3, §9 |
| 15 | Row 2's proof may contradict its own rule | Answered, checked: `73c1fe0` failed on `cli` and `driver-process`, its git half ran whole and passed, so `f296996`'s half held a pass gone stale by reach, which the rule composes | §8 row 2 |
| 16 | Two gaps in what the rows run | Taken: the 16 `tools/*.test.mjs` files run by file; row 3 says it adds `check` | §4.2 point 3, §8 row 3 |
| 17 | The proof's sources do not span its month | Taken: each source's span and count stated in the output; the recall over the FIX-LOG's failures given apart | §3.5 |

**The Opus read's §2 claims** are its findings 6, 12 and 9, answered above. Its note that a merge waiting on its scoped run is
strictly more than today's, which waits on `BLOCKING` alone, holds and is kept.

### 11.3 Which reads round 3 asks

Round 3 takes the Opus read's six named changes, which it said the steward can check without another read, and makes concrete the
five things Codex's read asked another round for: the queue's attribution and recovery (§4.1), causal recall (§3.5), the stage's
durable evidence (§5.3), a conservative known-red exclusion (§5.2) and one admission policy (§5.1). Codex's verdict was another
round on exactly those, so Codex reads round 3. Whether the Opus foundation reader reads it again, or the steward checks its six
changes against the text as that read offered, is the steward's call under D191. D207 stays proposed until the steward accepts.
