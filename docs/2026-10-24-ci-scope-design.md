# CISCOPE1 — a modular CI: a merge waits on what it reaches, the full set runs in the background

**Status: proposed (D207, 2026-10-24), round 1, not yet read.** Written by Claude Opus under D191. It changes a foundation
(the rule D115, D180, CI1, GATESLIM1 and PROCREACH1 rest on: what a merge and a stage wait for), so under D191's amendments
Codex reads it and an independent Opus reader reads it as a foundation; the steward accepts after both. Neither read has run:
the quest that asked for this design asked for no helper. Every figure below was measured on this machine on 2026-10-23/24
from the main checkout's own records (the CI's `local/scratch/ci-<sha7>/status.json` and `.trx` files, `local/class-verdicts.json`,
`local/gate-verdicts.json`) or from the tree at `013b5d090`, by the methods §2.6 gives. No full CI, Process half or rehearsal ran
for it.

The backlog rows are CISCOPE1 (this design), CIFAST1 (its first row, filed before it) and TESTVALUE1 (folded in, §7).

## 0. The direction and what this design reads it as

The owner's direction of 2026-10-24, as the quest gives it: CI must be properly scoped, running only what a change really needs,
module by module; the full run only in the background, never blocking development; and the tests are too many and too verbose.

Read here as five commitments, one per section: a map of modules and what every test reaches (§3); a scoped run per merge that a
merge and a republish of the owner's install wait on, in minutes (§4); the full set in the background, never blocking, its failures
fixed forward (§5); the rehearsals out of the per-merge path and side by side (§6); and each test's value measured, with a rule to
prune or merge and a target size (§7). §8 gives the rows in build order; §9 what each changes of D115, D180, CI1, GATESLIM1 and
PROCREACH1; §10 the questions the readers should test hardest.

## 1. Today, as the code has it

- **A merge** (`tools/merge-branch.mjs`, the parent's integration tool) blocks on `BLOCKING` (`universal`, `code-map`,
  `orient-index`, `merge-branch.mjs:902`) and on the path checks `web-types` and `web-rules` where their paths are reached
  (`PATH_CHECKS`, `:916`). Every other reached gate is listed and skipped, "in the CI beside" (GATESLIM1, D115's 2026-10-19
  second part). The merge is committed in the main checkout (`mergeNext`, `:4400`; `git merge --no-ff --no-commit`, `:4423`).
- **The CI** (`--ci`, `gateCi`, `:5345`) gates main's tip in `local/gate-tree` under its lock, running each gate whose verdict
  does not stand on the tip's tree (`ciPlan`, `:5077`): a pass stands while no path changed since reaches its gate by the lane
  table `REACH` (`:958`). Its watch gates the newest tip once the tree is idle, so one run covers every merge since the last.
- **A Process half** runs by class (PROCREACH1): `tools/process-reach.mjs` reads each class's reach (`censusOf`,
  `process-reach.mjs:573`) as the *areas* its files name, an area being a source project's first folder (`areaOf`, `:555`), a
  driver module. `classPlan` (`merge-branch.mjs:5100`) keeps a class whose newest standing verdict passed and runs the rest; it runs
  the **whole** half when no class's pass stands (`:5141`), when its oldest kept verdict is past a day (the sweep, `:5144`,
  `SWEEP_BOUND_MS` at `:1867`), and when every class is kept but the half owes a verdict (`:5147`).
- **The rehearsals** run beside the git half one after the other, release then family then deployment (`BESIDE_GIT_HALF`,
  `:481`; `laneSchedule`, `:606`; REHLANE2, REHLANE2b). The lane table sends every driver-lane path to both the family and the
  deployment rehearsals (`DRIVER_READERS`, `:945`; the `driver` lane rule, `:1176`).
- **A republish of the owner's install** stages with `publish:desktop --stage --from-gated <commit>`, which asks `--passed`
  of the gate tree: every gate of the plan must have a passing verdict that stands on that commit's tree (`stageJudgement`,
  `:1895`), the swept rule included (`sweptOut`, `:1877`).
- **The install's own landing** (`trees land`) judges the review, the second opinion and the workflow (`TreeGates.GateAsync`,
  `Trees/SessionTrees/TreeGates.cs:41-56`) and runs none of this repository's gates; the parent's merge tool is what gates.

## 2. What was measured

### 2.1 The CI runs

From each run's `status.json` (minutes of wall; "whole" is `classPlan`'s reason):

| Run | State | Wall | Git half | Family | Deployment | Driver's other half | What made the wall |
|---|---|---|---|---|---|---|---|
| `73c1fe0` | failed | 59.5 | 32.8, whole: no class's pass stands | 22.4 | 16.7 | 7.4, whole: `examples/plugins/…` | the git half and the rehearsal chain beside it |
| `f296996` | green | 38.4 | 31.5, whole: **every class kept, but the half owes a verdict** | kept | kept | 0.4 (4 of 114 classes) | a git half that ran 89 classes it had just kept |
| `9977845` | failed | 62.7 | 43.6, whole: no class's pass stands | 18.3 | 21.8 | 10.0, whole: `examples/plugins/…` | the git half |
| `59a2db0` | failed | 44.7 | 24.2 (87 of 89 run) | 14.1, FAIL | 14.1 | 8.0 (90 of 115 run) | the git half, the chain beside it |
| `6ce6235` | green | 74.3 | 1.1 (1 of 89 run) | 23.4 | 20.7 | 0.7 (5 of 115 run) | **the rehearsals alone**, one after the other |

Three facts follow. A run gates every merge since the last (96 to 183 paths changed between consecutive runs), so the union of
several merges reaches nearly every class. A run whose Process halves had nothing to do still took 74 minutes, all of it the family
and deployment rehearsals in turn. And `f296996` spent 31.5 minutes running a half whose every class had a standing pass.

### 2.2 What a single merge reaches

The last 40 first-parent merges into main (to `42fbf1fb4`), each judged alone with the census at `013b5d090`: 31 reach the three
Process halves; the driver's other half runs whole for 6 of them, the git half for 5, the modules' for 4 (`package.json`,
`daoris.lanes.json`, a test project file, an example plugin's file, a test source the census at the newer tree no longer holds).
Six of the last 80 merges reach every gate through `REACH`'s first rule (`:960-966`).

Class time is summed per class from `9977845`'s trx files, the last run that ran the three halves whole. A merge's cost is
the class time it reaches divided by the half's width (8, 4, 1), never less than its slowest class. Mean per merge that reaches
the half:

| Half (classes) | Class time, whole | By today's reach | By subject (§3.2) |
|---|---|---|---|
| driver's other half (115) | 38 min, 724 tests | 4.7 min | 2.9 min |
| git half (89) | 158 min, 746 tests | 36.8 min | 18.1 min (median 11.8, p90 36.9) |
| modules' half (20) | about 1 min, 167 tests | 1.3 min | 0.5 min |

The driver's other half and the modules' half already fit "minutes" by reach. The git half does not, by reach or by subject.

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

`local/class-verdicts.json` keeps six verdicts a class (`CLASS_VERDICTS_KEPT`, `:1748`): the git half's 349 hold no failure,
the driver's other half's 434 hold 13, the modules' 77 hold one. `local/gate-verdicts.json` keeps 400 (`addVerdicts`, `:1727`),
about a day and a half of runs here (2026-10-22 07:07 to 2026-10-23 15:48). Neither is long enough to say what a test has ever
caught: §7 needs a history of its own.

The cheap checks (`partials`, `read-shares`, `optional-locks`, `live-logins`, the four documentation tools, `release-prep
--check`, `orient-index --check`, the one-word, home and CLI-usage censuses, `daoris check`) took 27.8 s together, one after
another, on this tree.

The machine has 22 logical processors and 63.7 GB of memory. MEMGATE1 asks 10 GB free for a rehearsal, 8 for a Process half and
6 for a heavy suite (`MEMORY_NEEDS`, `:2177`).

### 2.6 How it was measured

Scratch scripts under the repository's ignored `_fixtures/`, not kept, read the main checkout's records and this tree. They
imported `censusOf`, `classesReached`, `pathReach`, `typesIn`, `namesIn` and `readCs` from `tools/process-reach.mjs`, and
`pathsReaching`, `readLanes` and `slowestClasses` from the merge tool. A merge's paths are `git diff --name-only <merge>^1
<merge>`, and a class's subject is the first folder of its own file under the test project. Every figure uses today's census for
every merge, so it is an estimate for the older merges. CISCOPE1c's proof (§8) measures again with the tool the row builds, which
keeps the method.

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

- **Its reach**: what it can see. For .NET this is PROCREACH1's census unchanged, at the area grain (§2.3 shows the area is the
  right grain). It is extended to the fast halves, whose filter comes from the command line (`--filter Category!=Process`) rather
  than a settings file. For TypeScript it is the static `import` closure of the file within its package, plus every repository
  file whose path its strings name. Through the latter, a vitest that parses a C# source (`src/test/driverSource.ts`, MOD9e) and a
  test that reads a fixture or golden reach that file. For a rehearsal it is its tool's import closure and the programs and scripts
  it starts, which `REACH`'s hand rules for `tools/` list today (`:974-1003`).
- **Its subject**: the module it tests, read from where its own file sits. The test projects mirror the source modules: the
  driver's tests have `Trees/`, `Platform/`, `Loop/` and the rest, the page's tests sit beside their sources. A unit whose file sits
  at its project's root has no subject. That holds 12 of the driver's other half's classes and 3 of the git half's.

Reach answers "can this change break this test". Subject answers "is this the test of what changed". §4 uses both: the scoped run
takes every reached unit whose cost is minutes, and, where a half's reached cost is not minutes, the reached units whose subject
changed, within a budget.

### 3.3 Fixtures and twins

A twin's table (`kept-names.json`, `hand-over-order.json`, `review-rules.json` and the rest of `REACH`'s fixture rules,
`:1009-1090`) reaches the units whose strings name it, in any suite. The census derives what those hand rows say today. Each row
becomes a check rather than a rule: the census's reading must agree with it, and a disagreement fails the census (§3.4). A fixture
no unit names reaches none, as PROCREACH1's `pathReach` already says for the test project's files (`process-reach.mjs:744-781`).

### 3.4 The census that keeps the map honest

`node tools/test-map.mjs census | reach <from> <to> | check` (CISCOPE1c), one tool over every suite, generalising
`tools/process-reach.mjs`, which it absorbs. `check` runs in every scoped run (§4) and fails when:

1. a runner ran a unit the census did not list, or the census lists one no runner ran. The trx, the vitest JSON report and the
   `node --test` report of the last whole run are compared, as PROCREACH1 compared 199 classes at `c557800`;
2. a tracked file is placed by no module and is not on the laneless list's reasons (LEFT1's rule, now for modules);
3. a hand row of `REACH` names a gate whose units the census finds no reader in, or the census finds a reader the row omits;
4. a unit has neither a subject nor a reason for its place at its project's root (an attribute or a comment line the census reads,
   as `[Reaches(…, Why = …)]` already is, PROCREACH1);
5. a file acts on every unit unnamed (a `global using`, an assembly attribute, a module initializer, a vitest setup file) and is
   not on the census's list of whole-suite files. Such a file runs its suite whole, as today, and the list makes that visible.

## 4. The scoped run: what a merge and a republish wait on

### 4.1 Where it runs, and when the merge commits

The merge tool merges into a **candidate**, not into main. Today it merges into the index with `--no-commit` (`:4423`), writes the
generated folders and runs its gates. Instead it writes the merged tree (`git write-tree`) and makes the merge commit without
moving a ref (`git commit-tree`, both parents, the message given). It then lets the checkout go (`git merge --abort`), so the main
checkout is free while the run goes: D180's reason for keeping verification out of it. It runs the scoped set on the candidate in
`local/scope-tree`, a second detached worktree with its own lock, whose builds stay warm between merges. On green it fast-forwards
main to the candidate (`git merge --ff-only <candidate>` in the main checkout, which has not moved: the parent merges one branch at
a time). On red nothing reaches main. The branch goes back with the failing units named, as a merge's failure did before GATESLIM1.

The scope tree is apart from the gate tree, so the background run (§5) never holds up a merge, and a merge never ends a
background run.

### 4.2 What it runs

1. **The cheap checks, always**: `universal` (the sensitive scan), `code-map`, `orient-index`, the census check (§3.4), the
   censuses `docs/development.md` lists (*Choose the checks*), the records' checks (`daoris check`, the documentation tools,
   `release-prep --check`), and the words: the catalogue tests (`src/Daoris.Web/src/locales/*.test.ts`) and `web-types` and the
   CLI's typecheck where reached. Measured: under a minute beside the universal gate's own 1 to 2.
2. **Every reached unit of the fast suites**: the .NET fast halves by class (one build per reached test project, which the scope
   tree keeps warm; the tests themselves take seconds), the CLI's and the tools' `node --test` files, the page's vitest files.
3. **Every reached class of the driver's other Process half and the modules' Process half**, as PROCREACH1 runs them, the classes
   that stand kept. By §2.2 that is a mean of 4.7 and 1.3 minutes a merge.
4. **The git half by subject within a budget.** The reached classes whose subject is a changed module, in value order (§7.2:
   recent failures first, then the most unique coverage per minute), until the half's budget is spent. The budget is declared on the
   gate in `daoris.gates.json` (`"scope": { "budgetMinutes": 8 }`, eight minutes of wall at four wide), and the cut is said: which
   classes ran, and which reached classes are **owed** to the background run (§5). The rest of the reached git classes are owed too.
5. **No rehearsal** (§6), unless the stage's rule in §5.3 asks for one.

A unit's failure is re-run alone once, as FLAKE1 does today. Each unit's verdict goes into the one store, keyed by tree, as a
scoped verdict. A gate's scoped pass that owes classes is recorded as `owed`, which counts for a merge and for an install stage and
never for a public release.

### 4.3 Exactness, and the one place it gives way

The run is exact for every suite but the git half: every reached unit runs, and the census decides what is reached. The git half is
where "exactly the reached tests" and "in minutes" cannot both hold today. A `Trees` change reaches nearly all of it (§2.3), at 25
to 44 minutes of wall. This design chooses minutes for the per-merge wait and makes the cut visible: the run names every owed
class, the background run pays them, and §7's first target is the git half's size, so the budget covers more of each merge as that
half shrinks. Once the git half's whole class time is under four times its budget, the budget covers a whole `Trees` change, and
the git half is exact as well. The readers are asked to test this choice (§10).

### 4.4 The expected wall

From §2: the cheap checks and `universal` 2 to 3 minutes; the reached fast suites beside them, each a warm build and seconds of
tests, 2 to 6 minutes; then the Process lane in turn: the driver's other half about 5, the git half at most 8, the modules' about 1.
**About 10 to 16 minutes for a driver merge; 3 to 6 for a page, CLI or tools merge, which reach no Process class; 2 to 3 for a
records or documents merge.** The design's first proof (CISCOPE1d) times the next ten real merges against this.

### 4.5 The republish

`publish:desktop --stage` gains `--from-scoped <commit>`. It stages a commit whose scoped run is green, or `owed`, from the
scope tree that holds it, under that tree's lock, as `--from-gated` holds the gate tree's. §5.3 says what it may never stage.

## 5. The full set in the background

### 5.1 Its schedule

`--ci --watch` becomes the background run. It runs in `local/gate-tree` as CI1 built it, a guest on the machine (CIPRIO1), and its
schedule is its own:

- **When the machine is idle**: CI1's measure (`CI_IDLE`, `:5067`) holding for a sustained window rather than one second, and no
  scoped run in flight;
- **nightly**, whatever ran in the day, so the sweep PROCREACH1 needs (a change the census misses) is at most a day old;
- **before a public release**, on the exact commit (`--ci --release <commit>`).

It runs every gate whole, the git half and all three rehearsals included (§6): its job is what scoping cannot see. It never holds the
scope tree, never ends for a merge, and never blocks one.

### 5.2 Its failures

A background failure is filed rather than waited on. The run writes `local/ci-findings.json` (the commit, the unit, the first lines of
its failure, the scoped verdict that unit last had), and the parent files each as a row or a FIX-LOG entry and fixes it forward
(D180 point 3). A unit that fails in the background and stays failing is **known red** with its row: a scoped run that reaches it
says so and does not count its failure against the merge, since the merge did not make it, and a scoped pass of it clears it. That
keeps D180's "the work does not stop for it" true of a failure that was already on main.

### 5.3 What a republish of the owner's install may stage on scoped-green, and what never

**May**: any commit whose scoped run is green or `owed`, known-red units listed. The install is local and reversible: D139's staged
update keeps the running version, and its rollback.

**Never on scoped-green alone**:

1. **A public release** (the npm package, the release workflow, a published desktop build). That needs the background run green on
   that exact commit, with no swept, owed or known-red unit, as D115's full set before a stage required.
2. **A commit that reaches the install's own update path**: the publisher, the stager, the launcher, the staged update and its swap,
   the hosts' start (`tools/desktop-publish.mjs`, `tools/stage-package.mjs`, `tools/service-publish.mjs`,
   `Daoris.Desktop.Launcher`, the driver's install files in `Setup/` (`InstallSwap.cs`, `InstallUpdate.cs`, `StagedBuild.cs`,
   `UpdateCommand.cs`; the module's workspace set-up is not among them), the modules' `Loop/HostSupervisor.cs`).
   The deployment rehearsal must pass on it first. The stage runs it
   (CIFAST1's stage path), because a broken update path is the one failure the install cannot roll back from by itself.
3. **A commit whose census could not be read**: its scope is unknown. Its run is whole and its stage waits for it.
4. **A commit with a failing cheap check**: those always run and never owe.

## 6. The rehearsals

### 6.1 Out of the per-merge path

No scoped run runs a rehearsal, except the deployment rehearsal that §5.3 point 2 asks of a stage. The background run runs all three
on every pass. The lane table keeps sending paths to them, and that reach now decides what a stage must wait for and what the
background run owes, never what a merge waits for. CIFAST1 builds the first part of this on today's `--ci` (§8).

### 6.2 Side by side

REHLANE2 and REHLANE2b keep the three apart, one after another, for one reason each: the release and deployment rehearsals both pack
the CLI's folder in place (`npm pack`, `tools/release-rehearsal.mjs:58`; the stager's `--clean`, `:96`). The family and deployment
rehearsals both run the CLI's bin, which prefers a staged `dist/` (D180's REHLANE2 note). That is state in **the tree**, so each
rehearsal gets a tree of its own: the background run prepares `local/gate-tree-rehearse-<name>` worktrees at the commit, with
dependencies installed from the lockfiles as GATEWT1 does. Each runs there beside the others.

What else they share was checked:

- **Ports**: each run takes its own from a band, none twice (`takePorts`, `tools/rehearsal-kit.mjs:266`, REHEARSEPORT1);
- **Homes**: each sets `DAORIS_HOME` under its own scratch (`tools/family-rehearsal.mjs:69`, `tools/deployment-rehearsal.mjs:486`),
  and its scratch is its own folder (`:49`, `:455`, `tools/release-rehearsal.mjs:30`). The instance pipe is named by the home's id
  (`PipeName.For`, `Daoris.Desktop.Modules/Platform/InstancePipe.cs:244`) and the host's mutex by its service address
  (`HostSupervisor.cs:505`), so two homes on two ports do not meet;
- **Builds**: each builds .NET into an output of its own with build servers of its own (`tools/rehearsal-kit.mjs:50`, `:76`,
  REHLANE2b);
- **Memory**: three rehearsals ask 30 GB between them (`MEMORY_NEEDS`), beside a Process half's 8, of 63.7 GB. MEMGATE1 already
  holds a gate until its memory is free.

What was not measured is load. The family and deployment rehearsals' waits are bounded by clocks and counts that stretch under load
(FAMILYFAIL1, REHLANE2b's note). So CISCOPE1e's proof is a timed background run with the three side by side, against the chain's 31
to 53 minutes. If a bound fails there, the fallback is the pairs REHLANE2b measured. Expected: the rehearsal lane's wall falls from
the chain's sum to its longest member, about 23 minutes.

## 7. Each test's value: TESTVALUE1 folded in

### 7.1 What is measured, per unit

- **Runtime**: its own time from the runner's report (trx durations per class and test, vitest's JSON report, `node --test`'s), kept
  per run.
- **Failure history**: `local/test-history.jsonl`, append-only, one line per unit per run (tree, commit, verdict, time), written by
  every scoped and background run. `class-verdicts.json`'s six per class and `gate-verdicts.json`'s 400 are too short (§2.5). A
  **catch** is a failure, not a flake, on a tree where the unit passed before, followed by a fix commit that changes a non-test file.
  The history links the two by the next pass's commit range.
- **Overlap**: the lines and branches each unit covers, recorded once in the background, then again when its sources change. For
  .NET: one filtered run per class with `--collect "Code Coverage;Format=cobertura"`, whose collector `Microsoft.NET.Test.Sdk`
  already brings (`Microsoft.CodeCoverage`). For vitest: one run per file with `@vitest/coverage-v8`, a new development dependency,
  so CISCOPE1f's lockfile change. A unit's **unique coverage** is the lines and branches it covers that no other unit does.

### 7.2 The rule

- **Value** is unique coverage and catches per minute of runtime. The scoped run orders a budgeted half by it (§4.2 point 4).
- **Remove** a unit with no unique coverage and no catch over the history window (30 days or 20 runs that reached it, whichever is
  longer): another unit covers everything it covers.
- **Merge** units that cover the same lines with different data into one table-driven test (a theory, a `test.each`), its rows kept.
- **Move down a tier**: a Process class whose unique coverage a fast-half stand-in can reach becomes a fast-half test, and one
  representative real-process case stays.
- **Sentences**: a test that pins an English sentence where the words are not its subject asserts the catalogue key or the code
  instead; the golden and catalogue tests keep the words (TESTVALUE1's own clause).
- **Never** remove the last unit covering a line or a branch. That is the proof each pass carries: the suites' coverage before and
  after, no line or branch losing its last cover.

### 7.3 The target

- **The git half: under 40 minutes of class time** (158 today), so a `Trees` change fits its eight-minute budget at four wide and
  the git half becomes exact (§4.3). It goes first: it is 80% of the Process halves' time.
- **The driver's other half and the modules': no target on time.** They fit already. The rule runs on them for upkeep.
- **The fast suites: the redundant set removed, and no sentence pinned outside the golden and catalogue tests.** The count is
  whatever §7.1's measure finds redundant, not a figure chosen before it. The planning figure is a quarter of the driver's 14,114 and
  the page's 9,145, and the proof states the measured one.

## 8. Rows, in build order

Each lands alone, with its proof.

1. **CIFAST1** (filed 2026-10-24, unchanged): a plain `--ci` runs every gate but the family and deployment rehearsals, recorded as
   owed for a stage, and the stage path runs them on the commit it stages. *The fastest win.* `6ce6235`'s 74 minutes were those two
   rehearsals alone, and every driver merge reaches both. Proof: CIFAST1's own.
2. **CISCOPE1b — a half whose classes all stand passes without running**: `classPlan`'s third whole rule (`:5147`) gives the half a
   verdict composed from its kept classes, with `sweep` the oldest. The sweep's own rule (`:5144`) moves to the background schedule
   (§5.1), and the install's stage no longer refuses a half for its sweep's age; a public release still does. Proof: the tool-tests
   for `classPlan` and `sweptOut`, and `f296996`'s case replayed with zero classes run (31.5 minutes there).
3. **CISCOPE1c — the census of every test unit** (§3): `tools/test-map.mjs`, reach and subject for both .NET halves, the
   TypeScript suites and the rehearsals, its `check` (§3.4), the fixture rules of `REACH` derived and held. Proof: the census lists
   exactly the units the last whole run's reports ran; §2.2 replayed with the tool, its figures within a tenth of these.
4. **CISCOPE1d — the scoped run per merge** (§4): the candidate commit, `local/scope-tree`, the scoped set with the git half's
   budget, scoped and `owed` verdicts, and `--from-scoped`. `BLOCKING` and `PATH_CHECKS` become its cheap tier. Proof: the next ten
   real merges timed (p50 and p90 against §4.4's figures), and a merge whose scoped run fails leaving main where it was.
5. **CISCOPE1e — the background run and its rehearsals side by side** (§5, §6): the schedule, `ci-findings.json`, known red, the
   rehearsals in trees of their own, the never-list of §5.3. Proof: a timed background run with the three rehearsals side by side
   against the chain's 31 to 53 minutes, and the stage's refusals tested.
6. **CISCOPE1f — the value measures** (§7.1): `local/test-history.jsonl`, per-unit coverage in the background, a value report
   (`tools/test-value.mjs`). Proof: the report over every suite, its figures reproducible from the history.
7. **TESTVALUE1** (re-scoped by §7.2 and §7.3): the passes, the git half first. Proof: TESTVALUE1's own, and the git half's class
   time against its target.

## 9. What it changes

- **D115** (the gates of a landing, GATE3, GATESLIM1's second part): a merge waits on its scoped run, which is minutes and runs in
  a tree of its own. GATESLIM1's "listed and skipped, in the CI beside" becomes "run if reached, else owed to the background". D115's
  "full set before a stage" holds for a public release; an install stage needs the scoped run, and the deployment rehearsal where
  §5.3 point 2 applies.
- **D180**: point 1's blocking set grows from the cheap checks to the scoped set, still never in the main checkout. Point 2's "every
  other gate runs beside" becomes the background run. Point 4's three lines become the builder's own tests, the scoped run, and the
  background run, with the install's stage reading the second. The budget note's "a full set under thirty minutes" applies to the
  background run, and the per-merge wait is §4.4's.
- **CI1**: `--ci --watch` is the background run with its own schedule (§5.1). The per-merge delta moves to the scoped run, so the
  watch no longer coalesces merges into one wide delta.
- **GATESLIM1**: `BLOCKING` and `PATH_CHECKS` fold into the scoped run's cheap tier. `--gated` is the scoped run, and `--full` is
  unchanged.
- **PROCREACH1**: its census becomes CISCOPE1c's for every suite, at the same area grain (§2.3). Its daily sweep moves from the CI's
  per-run plan to the background schedule, and the third whole rule goes (CISCOPE1b). A subject and a budget join reach for the git
  half.

Until D207 is accepted, none of these changes anything. On acceptance, each of D115 and D180 gets a dated note pointing here.

## 10. For the readers

1. **The git half's budget** (§4.3): is minutes the right side of the trade against exactness, and is subject-plus-value the right
   cut? The alternative is the git half by reach, exact, at a median of 12 and a p90 of 37 minutes a merge.
2. **The candidate commit** (§4.1): `commit-tree` and a fast-forward keep main untouched until green, and free the checkout during
   the run. Does anything in the merge tool assume the merge commit is made in the checkout? The orientation index is written into
   the merged tree before the gates (ORIENT1a); the union records are resolved in the index (MERGETASKS1).
3. **Known red** (§5.2): does not counting a filed failure against a merge open a hole a real regression can pass through? The
   alternative is that one broken test on main blocks every merge that reaches it until it is fixed.
4. **§5.3 point 2's update-path list**: is it complete? It is read from what the deployment rehearsal drives.
5. **The rehearsals' side-by-side under load** (§6.2): which bounds would fail first?
