# CISCOPE1 — a modular CI: a merge waits on what it reaches, the full set runs in the background

**Status: proposed (D207, 2026-10-24), round 2.** Written by Claude Opus under D191. It changes a foundation (the rule D115,
D180, CI1, GATESLIM1 and PROCREACH1 rest on: what a merge and a stage wait for), so under D191's amendments Codex reads it and an
independent Opus reader reads it as a foundation; the steward accepts after both.

**Round 2** (2026-10-24) answers round 1's two reads (`docs/2026-10-24-ci-scope-reads.md`: Codex's, 17 findings, 14 blocking;
the Opus foundation read, 21 findings, 8 blocking; both *another round*). Every finding was checked against the code on main at
`b61325e6c`, CIFAST1 merged (`0613afde2`); §11 tables each, and where round 2 answers it. What moved:

- **The steward's direction for round 2**: development never waits on the full set, since a merge waits only on its scoped run,
  in minutes. The owner's install is republished only on a commit the background run has passed: the sweep within its bound,
  CIFAST1's rehearsals paid, nothing known red that its change reaches. It is never staged on scoped-green alone. Round 1's
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
`013b5d090`, by the methods §2.6 gives. Round 2's arrival rate (§2.7) is from main's history at `b61325e6c`. No full CI, Process
half or rehearsal ran for either round.

The backlog rows are CISCOPE1 (this design), CIFAST1 (its first row, landed) and TESTVALUE1 (folded in, §7).

## 0. The direction and what this design reads it as

The owner's direction of 2026-10-24, as the quest gives it: CI must be properly scoped, running only what a change really needs,
module by module; the full run only in the background, never blocking development; and the tests are too many and too verbose.

Read here as five commitments, one per section: a map of modules and what every test reaches, with its recall measured (§3); a
scoped run per merge that a merge waits on, in minutes (§4); the full set in the background, never blocking a merge, the one run a
republish of the owner's install waits for (§5); the rehearsals out of the per-merge path and side by side (§6); and each test's
value measured, with a rule to prune or merge and a target size (§7). §8 gives the rows in build order; §9 what each changes of
D115, D180, CI1, GATESLIM1 and PROCREACH1; §10 the questions for the readers of round 2; §11 the reads of round 1, answered.

What a merge waits for and what the install waits for are now two different things. A merge waits for its scoped run: minutes, and
exact only as far as the map is (§4.3). The install waits for the background run: the full set, as D180 point 4 and CIFAST1 have it,
because once a staged build confirms its predecessor is gone (§5.3).

## 1. Today, as the code has it

Line numbers are `tools/merge-branch.mjs` on main at `b61325e6c` unless named.

- **A merge** (the parent's integration tool) blocks on `BLOCKING` (`universal`, `code-map`, `orient-index`, `:933`) and on the
  path checks `web-types` and `web-rules` where their paths are reached (`PATH_CHECKS`, `:947`). Every other reached gate is listed
  and skipped, "in the CI beside" (GATESLIM1, D115's 2026-10-19 second part). The merge is made in the main checkout (`mergeNext`,
  `:4431`; `git merge --no-ff --no-commit`, `:4454`), and an open merge is read from its `MERGE_HEAD` (`mergeInProgress`, `:2572`),
  which `--continue`, `--rerun` and `--stale` dispatch on.
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
  `component === null ? light`). The lane table sends every driver-lane and modules-lane source path to the family and deployment
  rehearsals (`DRIVER_READERS`, `:976`; `MODULES_READERS`, `:974`); the driver's **tests** reach only the .NET suites (`:1101`).
- **A republish of the owner's install** stages with `publish:desktop --stage --from-gated <commit>`, which asks `--passed` of the
  gate tree: every gate of the plan must have a passing verdict that stands on that commit's tree (`stageJudgement`, `:1926`), the
  swept rule included (`sweptOut`, `:1908`). The gate tree's record keeps the commit last staged (`staged`,
  `tools/desktop-publish.mjs:1020`).
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

### 3.5 Recall: the proof and the running miss count (round 2)

The checks above say which units exist and that the declared edges hold. They cannot say whether a unit's real dependencies fall
inside its reach. Recall is measured, twice.

**The proof** (CISCOPE1c). Every failure recorded between 2026-09-24 and 2026-10-24 is replayed through the census at its
introducing commit:

- **The failures**: the FIX-LOG's entries of that month whose symptom is a test or a check failing (the 2026-10-21 GENREGEN1,
  FABRIC-CLOSE1d3 and LANDBACKLOG1 entry among them); each failed CI run's failing units (`local/scratch/ci-*/status.json` and its
  trx); each class failure in `local/class-verdicts.json`; and each merge handed back by a failing gate, from the merge tool's
  records.
- **The introducing commit**: the one the entry names, else the first commit on main between the unit's last pass and its first
  failure whose diff the fix touches; where neither settles it, every commit in that range, the failure counted selected only if
  some commit's scope selected it.
- **The replay**: the census tool at that commit, given that commit's diff against its first parent, says whether §4.2's scoped set
  would have run the failing unit, and through which edge. A unit §4.2 runs whole by suite counts as selected only when the rule
  that runs it whole held at that commit.
- **The output**: every failure listed with *selected* or *missed*, each miss with the edge the census lacked, and the recall
  (selected over all). Each miss is fixed in the census in the same row, by an edge, a declaration or a whole-suite entry, with an
  expected-edge fixture (§3.4 check 6), and the replay runs again until the list holds no miss the census can fix; one it cannot
  fix is named and stays in the count.

GENREGEN1's two driver censuses are the expected first miss of the class census, and selected under round 2, since the change
reached the driver's fast half, which runs whole (§4.2 point 2).

**The running miss count** (CISCOPE1d, then on every background run). The background run, finding a unit failing that passed on
an earlier background run, looks up the scoped runs of the merges and direct commits between the two. A failure that no scoped run
in that range selected, and that §5.2 does not hold to be inherited, is a **miss**: written to `local/ci-misses.jsonl` (the unit,
the range, the commit the range's bisection names, the edge if found), filed with the failure's own row, and fixed in the census as
the proof's misses are. The CI's status and `--ci --status` show the trailing thirty days' recall: failures the background run
found, misses among them, and the misses still unfixed. The count starts from the proof's.

## 4. The scoped run: what a merge waits on

### 4.1 The candidate and the merge queue

**Where.** The merge tool merges into a **candidate** in `local/scope-tree`, a detached worktree of its own with its own lock,
whose builds stay warm between candidates. The main checkout is not touched until promotion, so it stays free while a run goes
(D180's reason for keeping verification out of it). The merge itself is today's: `git merge --no-ff --no-commit` in the scope tree
at the candidate's base, the orientation index and the generated folders written into the merged tree (ORIENT1a), the union
records resolved in the index (MERGETASKS1), then `git commit` on the scope tree's detached head. Nothing moves a ref of main.

**The queue.** `merge-branch.mjs <branch>` puts the branch on `local/merge-queue.json`, a durable record under the tool's lock,
and waits for its outcome, saying its place. The invocation that finds no runner becomes the runner for as long as the queue holds
anything; the others wait on the record. The runner:

1. **Forms a candidate** from every branch queued, in arrival order, at most eight (bisection depth three; at the busiest hour's
   twelve and a run of 15 to 28 minutes a candidate holds three to six). Its base is main's tip. Each branch is merged on top of
   the last as its own merge commit, so history keeps one merge per branch as today. A branch that conflicts is resolved by the
   tool's resolvers as today; one that still conflicts is set aside before the run, handed back with its conflict named, and the
   rest go on. A merge arriving while a candidate's run is in flight waits for the next candidate.
2. **Runs the scoped set** (§4.2) on the candidate's tip. Its basis is every unit with no pass standing on that tree, so it is the
   union of the members' reach, and of any direct commit on main since a unit last passed.
3. **On green, promotes.** In the main checkout, `git merge --ff-only <candidate>`, only if main's tip is still the candidate's
   base. If main moved (a direct commit by the parent), the candidate is formed again on the new tip from the same branches, and
   the scoped set runs again on it, which by standing runs only the units the direct commit's paths reach. If the checkout's state
   refuses the fast-forward (a file changed there that the candidate changes), the runner says so and keeps the candidate;
   `--continue` promotes it.
4. **On red, bisects.** Each failing test is first judged inherited or not (§5.2): it is run on the candidate's base, and one that
   fails there with the same fingerprint is inherited, filed, and does not count. For the rest, the failing tests alone are run on
   the candidate's prefixes (the base plus its first half of members, then halving the failing side), until each failing test is
   pinned to one member. Each step is a warm incremental build and the failing tests, minutes or less, and at most three steps.
   The members pinned go back with their failing tests named; the rest form the next candidate, whose run keeps every unit whose
   pass stands, that is, it runs only what the removed members' paths reach.

**What the queue serves.** At the measured rate (§2.7), a merge waits at most for the run in flight and its own: two runs, 30 to
56 minutes for a driver merge at the busiest hour, one run in a quiet one, plus a bisection on red. A batch costs one run for many
merges, so the queue does not grow with the arrival rate; it grows only with red candidates, which bisection keeps to three extra
short runs each. CISCOPE1e's proof replays a busy day's arrivals (2026-10-21's 127) against the queue and reports each merge's
wait.

**Durable state, restart and the verbs** (Codex 17, Opus 19). `local/merge-queue.json` holds the queue and the candidate: its base,
its members in order, each member's merge commit, its state (`forming`, `conflict`, `running`, `bisecting`, `green`, `promoted`,
`handed back`) and the run's directory. A runner that dies leaves it as it was; the next invocation reads it and goes on: a
candidate whose base is no longer main's tip is formed again, a run whose process is gone runs again (the units that finished keep
their standing verdicts), a green candidate not yet promoted is promoted. The verbs read the scope tree where they read the
main checkout today: `mergeInProgress` asks the scope tree's `MERGE_HEAD`, so `--continue` resumes a conflict left open there
(the parent resolves it at the scope tree's paths, which the tool names), and `--rerun` and `--stale` act on the candidate.
`--batch` queues its branches together. `--one-at-a-time` makes every candidate one member. `--drop-batch` (today's, for a recorded
batch) takes the queued branches off the queue, or ends a candidate and hands its members back.

**Every path that moves main** (Codex 17). In this repository three do: the merge tool, which goes through the queue; the parent's
direct commits, which the next candidate's standing covers (point 2 above) and which are never staged without the background run
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
3. **The TypeScript suites by file**, where reach is a real closure (§3.2): the CLI's and the tool-tests' `node --test` files and
   the page's vitest files that the candidate's paths reach. A whole-suite file (§3.4 check 5) or an unresolved read runs its suite
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

**Verdicts.** Each unit's verdict goes to a per-unit store, `local/unit-verdicts.json`, keyed by tree, six kept a unit, which
absorbs `class-verdicts.json` (Opus 16). The gate store keeps gate verdicts only, so a scoped run never evicts what the stage reads.
A scoped run records its own verdict, `scoped`, green or red, with its deferred units. The stage never reads it (§5.3).

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

None of these reaches the install, which waits for the background run (§5.3). What a miss costs is attribution: a later candidate
or the background run finds it, and it is fixed forward, as D180 accepts.

### 4.4 The expected wall

Rebased on measured walls (§2.2's ratio of 1.6 to 2.1 for a Process half), not on class time over width. The Process halves run
one after another in the Process lane, as GATEORDER1 has them. Per category of merge, before the queue's wait:

| A merge that changes… | Runs | Expected |
|---|---|---|
| records and documents only | the cheap tier | 2 to 4 min |
| the page | the cheap tier, the reached vitest files, `web-types` | 4 to 8 min |
| the CLI or `tools/` | the cheap tier, the reached `node --test` files; `driver-process`'s reached classes where the CLI's lock helpers or the dispatcher's seam change (`:1178`, `:1186`) | 4 to 8 min; 10 to 15 with `driver-process` |
| the driver | the cheap tier; the driver's and the modules' fast halves whole (warm builds); the driver's other half by class (4.7 min by the model, 7 to 10 measured); the git half within its 8; the modules' half by class (1 to 2) | 15 to 28 min |
| a whole-suite file (`REACH`'s first rule, 6 of 80 merges) | everything above, every Process half whole but the git half's budget | about 30 min |

CISCOPE1e's proof times the next real merges per category, p50 and p90, with the queue's wait apart, and the walls replace these.

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
- **The sweep**, from a durable record, `local/ci-sweep.json`: for each Process half, its last whole pass and when its next is due
  (`SWEEP_BOUND_MS` after it). A half due is run whole by the next run, whatever its reach.
- **Nightly, unconditional**, at a set hour, on main's tip: every gate whole, the git half, the browser specs and the three
  rehearsals included, so a commit's deferred units are paid within the day even on a day of constant merges, when the idle window
  CI1 waits for rarely holds (Opus, the read's §2). A night missed (the machine asleep or off) is run at the next start, before any
  other background run.
- **Before a public release**, on the exact commit (`--ci --release <commit>`).

**A failed run is not a satisfied one.** Today's watch never gates a tip a run already gave a verdict on, failed or not. Round 2
keeps that for the tip, so a failure is not retried in a loop, but a failed or ended run satisfies no sweep and no night: the due
record stays due and the next run pays it. **Overdue** is reported: the CI's status and the parent's watch say a half whose sweep is
past its bound, and a night that did not complete.

**The bound, stated honestly.** A change the scoped run missed is found within the sweep's bound plus a whole run's runtime (a day
and about one to two hours) **provided a whole run completes**; when one does not, the overdue report says so, and the install
cannot stage meanwhile (§5.3), so the bound is on development's attribution, never on what the owner runs.

**Sharing the machine.** The background run never holds the scope tree, never ends for a merge, and never blocks one. It starts no
Process class group while a scoped run's Process tier is running, and waits between groups for it (Opus 17), so the two Process
lanes never run side by side and GATEORDER1's timing flakes stay away. A scoped run takes the machine first.

### 5.2 Its failures, and known red

A background failure is filed rather than waited on. The run writes `local/ci-findings.json` (the commit, the unit, the test, its
fingerprint, the first lines of its failure, the scoped verdict that unit last had), and the parent files each as a row or a FIX-LOG
entry and fixes it forward (D180 point 3).

**Known red is keyed by test and fingerprint** (Codex 8, Opus 5). A test, not a class or a file, that fails in the background with
the same **fingerprint** (its full name, the exception type and the first line of its assertion message, numbers and paths
normalised) on main's tip, and stays failing, is known red with its row. A candidate's failing test is waived only when it is
known red with the same fingerprint **and** it fails on the candidate's base too (§4.1 point 4). Everything else counts against the
candidate: a new failing test in the same class, a changed fingerprint, a build, discovery or host failure, and a known-red test
that passes on the base. **Clearing**: a pass of the test on any run clears it. **Expiry**: a test known red for more than 72 hours
stops being waived, and every candidate that reaches it fails until it is fixed or its row says it is skipped with the reason, so a
waiver cannot outlive the fix-forward D180 expects within a working day.

### 5.3 What a republish of the owner's install stages

**The steward's direction for round 2**: the owner's install is republished only on a commit the background run has passed. It is
never staged on scoped-green alone.

`publish:desktop --stage --from-gated <commit>` stays the one stage path, reading the gate tree's verdicts (`stageJudgement`,
`:1926`). It stages a commit only when:

1. **The background run has passed it**: every gate of the plan has a pass standing on its tree, the git half's deferred classes,
   the browser specs and the cheap checks included. A commit some scoped run passed and the background run has not is refused,
   named, with the background run to wait for.
2. **The sweep is within its bound**: `sweptOut` stays for the install (round 1 had it only for a public release).
3. **CIFAST1's rehearsals are paid**: the family and deployment rehearsals pass on its tree (`--ci --stage <commit>`), as today.
   Since every driver-lane and modules-lane source reaches the deployment rehearsal (`:974`, `:976`), every change to the install's
   update path pays it, and round 1's hand list of update-path files is withdrawn (Codex 10, Opus 10).
4. **Nothing known red that its change reaches**: a gate whose only failures are known-red tests (§5.2) counts for the stage only
   when the change since the commit last staged (`staged.commit` in the gate tree's record, `tools/desktop-publish.mjs:1020`)
   reaches none of those tests by the census. A known-red test the change reaches refuses the stage, as does any failure that is not
   known red.
5. **Its census was read**: a commit whose census could not be read ran whole, and its stage waits for that.

**Why not sooner.** Round 1 justified staging on scoped-green by D139's rollback. That is withdrawn (Codex 9, Opus 2): once a staged
build confirms, `InstallSwap.Finish` deletes `update/previous/` (`InstallSwap.cs:374-376`), D139 excludes failures after
confirmation, and what the build wrote into the home's stores stays. A regression found after that is fixed forward on the
install the owner develops with (DOGFOOD1), so the full set is the gate, as D180 point 4 and CIFAST1 already have it. Development
does not wait for it: merges go on through their scoped runs, and only the republish waits.

**A public release** needs more: the background run green on that exact commit with nothing deferred, swept or known red.

## 6. The rehearsals

### 6.1 Out of the per-merge path

No scoped run runs a rehearsal. The background run runs all three on every nightly pass and wherever a promotion reaches them, and
the stage pays CIFAST1's two on the commit it stages. The lane table keeps sending paths to them; that reach decides what a stage's
`--ci --stage` must run and what the background run runs, never what a merge waits for.

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
  workers and the owner's install (Opus, the read's §2). CISCOPE1f adds one admission ledger across the background and scope
  runners: a gate's need is reserved in it before it starts and released when it ends, and a background gate never runs past its
  bound; it waits.
- **Homes, pipes, mutexes, builds**: apart already. Each rehearsal sets `DAORIS_HOME` under its own scratch
  (`tools/family-rehearsal.mjs:69`, `tools/deployment-rehearsal.mjs:486`); the instance pipe is named by the home's id
  (`PipeName.For`, `Daoris.Desktop.Modules/Platform/InstancePipe.cs:244`) and the host's mutex by its service address
  (`HostSupervisor.cs:505`); each builds .NET into an output of its own with build servers of its own
  (`tools/rehearsal-kit.mjs:50`, `:76`).

**The fallback stays until it is proven**: the serial chain stays the default, and side by side is turned on only once CISCOPE1f's
collision tests pass and five timed background runs on working days, beside the scope runner's merges, hold every bound
(FAMILYFAIL1, REHLANE2b's note) and show the scoped runs' walls unchanged. Expected on a quiet machine: the rehearsal lane's wall
falls from the chain's sum (31 to 53 minutes) to its longest member, about 23 minutes; on a working day MEMGATE1's ledger will
serialise some of it, and the proof states the measured wall. The three trees' first installs and cold builds are not priced here;
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
CISCOPE1e, a merge blocks as today and the watch gates every merge's tip; the install's stage is today's throughout.

1. **CIFAST1** (landed, `0613afde2`): a plain `--ci` runs every gate but the family and deployment rehearsals, owed for a stage,
   and `--ci --stage` runs them on the commit it stages. It removed 44.1 minutes from `6ce6235`'s critical path (§2.1); a stage
   still pays them in turn. Proof: its own.
2. **CISCOPE1b — a half whose classes all stand passes without running, when nothing was hidden.** `classPlan`'s third whole rule
   (`:5181`) composes the half's verdict from its kept classes **only** when the half's newest gate-level verdict is a pass that
   went stale by reach (Opus 15, Codex 11): it ran to its end, its results are complete, and the census matched the runner's
   classes. A half that failed outside a test, a run with missing results and a discovery disagreement still run whole. The sweep
   (`:5178`) and `sweptOut` are unchanged. Proof: `classPlan`'s tool-tests, the refusal cases among them (a half that failed
   outside a test with every class kept; a trx missing; a class the census lacks), and `f296996`'s case replayed with zero classes
   run (31.5 minutes there).
3. **CISCOPE1c — the census of every test unit, and its recall** (§3): `tools/test-map.mjs`, reach and subject for every suite,
   absorbing `tools/process-reach.mjs`; the declared edges, the wide fallback and the expected-edge fixtures; its `check`, run in
   the CI's run and as a cheap check of a merge; the fixture rules of `REACH` derived and held. It changes nothing the CI runs.
   Proof: the inventory matching runner discovery on the same tree; §2.2 replayed with the tool, its figures within a tenth of
   these; **the recall proof** of §3.5 over 2026-09-24 to 2026-10-24, every miss listed with its edge and fixed or named.
4. **CISCOPE1d — the background run's schedule and its safeguards** (§5.1, §5.2): `local/ci-sweep.json`, the nightly run with its
   catch-up, a failed run satisfying nothing, the overdue report; `local/ci-findings.json`; known red by test and fingerprint, with
   clearing and expiry; the stage's point 4 (§5.3); `local/unit-verdicts.json` and `local/test-history.jsonl` as attempt-level
   records; the miss count. The sweep stays in `classPlan` until the record replaces it in the same row, and the stage keeps
   `sweptOut`. Proof: tool-tests for the due record (a night missed and caught up, a failed run leaving the sweep due, an overdue
   report), for known red (a second failing test in a known-red class counts; a changed fingerprint counts; expiry), and for the
   stage's refusals (scoped-only, swept, known red reached by the change since `staged.commit`, rehearsals unpaid).
5. **CISCOPE1e — the scoped run and the merge queue** (§4): `local/scope-tree`, the queue and its candidates, batching, the base
   re-run and bisection, promotion with main's movement handled, restart, the verbs; the scoped set of §4.2 with the git half's
   budget in its conservative order and the deferred list; `BLOCKING` and `PATH_CHECKS` as its cheap tier. GATESLIM1's "listed and
   skipped" becomes "run if reached, else deferred". The background run keeps gating every promoted tip, and the stage reads only
   the gate tree, so nothing that guards the install changes. Proof: tool-tests for the queue's states (two branches in one
   candidate, a conflict set aside mid-batch, a red candidate of four bisected to one member, an inherited failure, main moved by
   a direct commit before promotion, a runner killed in each state and resumed, a checkout refusing the fast-forward); 2026-10-21's
   arrivals replayed against the queue, each merge's wait reported; the next real merges timed per category against §4.4, p50 and
   p90; a red candidate leaving main where it was; the cheap tier's wall measured.
6. **CISCOPE1f — the rehearsals side by side** (§6.2, REHPAIR1 taken over): the rehearsals' trees, the port claims, the memory
   ledger across both runners. The serial chain stays the default until the proof holds. Proof: collision tests (two runs given the
   same port start; two gates' needs past the free memory); five timed background runs on working days with the three side by side,
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
- **CIFAST1**: unchanged. *Owed* keeps its meaning; the scoped run's word is *deferred*.

Until D207 is accepted, none of these changes anything. On acceptance, each of D115 and D180 gets a dated note pointing here.

## 10. For the readers of round 2

1. **The queue** (§4.1): is a candidate of up to eight with bisection on the failing tests alone right at the measured rate, and
   are the restart and main-moved cases complete? Is the runner-and-waiters shape of `merge-branch.mjs` workable for the parent?
2. **The scoped set's basis** (§4.2): per-unit standing on the candidate's tree, with the fast .NET halves judged at gate level.
   Does a direct commit's change reach every unit it should through standing?
3. **Known red** (§5.2): is test plus fingerprint, confirmed on the base, narrow enough, and is 72 hours the right expiry?
4. **The stage's point 4** (§5.3): is "the change since `staged.commit` reaches no known-red test" a sound reading of the
   steward's "nothing known red that its change reaches"?
5. **The recall proof** (§3.5): is the introducing-commit rule sound where the FIX-LOG does not name one, and is the miss count's
   range rule right?

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

### 11.2 Which reads round 2 asks

Round 2 rewrites §4.1, §4.2, §5.3 and §8, which both reads named as design work. It changes a foundation as round 1 did, so Codex
reads it and an independent Opus reader reads it as a foundation; D207 stays proposed until the steward accepts.
