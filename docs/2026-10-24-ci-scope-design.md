# CISCOPE1 — a modular CI: a merge waits on what it reaches, the full set runs in the background

**Status: proposed (D207, 2026-10-24), round 6, the final round.** Written by Claude Opus under D191. It changes a foundation
(the rule D115, D180, CI1, GATESLIM1 and PROCREACH1 rest on: what a merge and a stage wait for), so under D191's amendments Codex
reads it and an independent Opus reader reads it as a foundation; the steward accepts after both. Codex read rounds 1 to 4; its
read of the final text is owed when its workspace's credits return, and a blocking finding of it becomes an amendment to D207.

**Round 6** (2026-10-24), the final round, answers round 5's one read (`docs/2026-10-24-ci-scope-reads.md`, *Opus (foundation),
round 5*, at `9a48b7f3d`: 10 findings, 3 blocking, *accept after named changes the steward can check without another read*;
Codex's workspace was out of credits, so round 5 has no Codex read). Every finding was checked against the code on main at
`0f086f0e4`, where nothing the design or the read cites has changed since `cdf6d5f55`, and each held. Round 6 makes exactly the
read's named changes, each a sentence with its proof case, and changes nothing else of substance; §11.5 tables each finding.
What moved:

- **A failure's location is its source text, and only a pass bounds recall's search** (§4.1 point 4, §3.5, §5.2, the read's
  first blocking finding, as it names it): a failing test's location is its frame's method and the text of its source line,
  never its line number; in recall's search only a run where the test passes moves the lower boundary, and a run where it fails
  with another signature, or does not exist, moves nothing.
- **The delegated child's binding** (§5.3, the read's second blocking finding, as it names it): a plain `--stage` builds only
  under `_fixtures`, as GATE3 has it, or as the `--from-gated` child under its parent's held lock; the child makes the second
  tree read just before it stages.
- **A death of memory is the gate's only past its own need** (§4.1 point 4, the read's third blocking finding, as it names it):
  only on the run after the hold, and only where the gate's own job's peak commit exceeded its reserved need.
- **The read's other seven**: an ended job's charge released, its close semantics chosen (§5.1); the preview taken after the
  records commit (§4.1); a build's signature as a set (§4.1 point 4); row 3 building the failure reader and the signature (§8);
  the scoped run's light lane below normal while its Process tier runs, D180's priority split kept (§4.4, §5.1, §9); recall's
  walk first (§3.5); and the slips.

**Round 5** (2026-10-24) answers round 4's two reads (`docs/2026-10-24-ci-scope-reads.md`, both at `e89f00ca4`: Codex's, 8
findings, 3 blocking, *another round*; the Opus foundation read, 10 findings, 3 blocking, *accept after named changes*). Every
finding was checked against the code on main at `cdf6d5f55`, where no file the design cites has changed since `a6991805f` (only
`docs/development.md`'s landing paragraph, which the design does not cite), and each held; §11.4 tables each. What moved:

- **A failure matched by what it is, not by its message** (§4.1 point 4, §5.2, the Opus read's first blocking finding, as it
  names it): within one judgement a failing test is matched by its full name with its case, its exception type and its
  assertion's location, the message line only where no location can be read; GUIDs and hex runs are normalised as numbers are;
  the full fingerprint stays the record's identity.
- **Every unresolved failure has an outcome** (§4.1 point 4, the Opus read's second blocking finding, as it names it): a candidate
  of one with an unresolved failure is handed back as *unresolved*, its flake filed, and the queue goes on; a prefix that cannot
  run the failing selection makes the failure unresolved.
- **A death of memory is the machine's** (§4.1 point 4, §4.2, the Opus read's third blocking finding, as it names it): it is
  never run on the base or over the prefixes, never stops the queue and never hands a member back; the scoped run keeps
  MEMGATE1's run again; FLAKE1's re-run is no longer called the only retry.
- **Recall's search made conservative** (§3.5, Codex's first blocking finding): an introduction is credited only when every commit
  that could still be the last transition gets the same verdict; otherwise the commits before the first failure are run one by
  one until it is found, within a stated cap, and past it the failure is unsettled. Its probes recurse, its price is restated
  over the month's 2,153 first-parent commits, and before CISCOPE1e it runs under a stated temporary rule.
- **The stage builds from a tree held for it** (§5.3, Codex's second blocking finding): every ordinary stage builds from the gate
  tree at the receipted commit under the tree's lock, as `--from-gated` does today; a plain `--stage` no longer builds from the
  checkout.
- **A reservation is held by a job, not found by a search** (§5.1, Codex's third blocking finding): each admitted gate starts in a
  job object of its own, as HEAVYRUN1a's gates do, whose membership survives an intermediate process's death; a membership that
  cannot be read keeps its charge.
- **The statements and corrections** of both reads: the scoped run scheduled by memory and the background's light lane in its
  set; the scoped runner's priority named as a departure from D180's budget note; the stop on a base failure recorded as a
  deliberate exception, with a recheck; known red limited to the units a scoped run may run; carried debt by standing evidence;
  the install's residual exposure stated; `recordMatcher`'s purpose; *records only* by its three rules; the frozen tip bound to
  the preview; the expiry counted per test; and the slips.

**Round 4** (2026-10-24) answers round 3's two reads (`docs/2026-10-24-ci-scope-reads.md`, both at `22427ff95`: Codex's, 12
findings, 6 blocking, *another round*; the Opus foundation read, 12 findings, 2 blocking, *accept after named changes*). Every
finding was checked against the code on main at `a6991805f`, where no file the design cites has changed since `22427ff95`, and each
held; §11.3 tables each. What moved:

- **A failing gate that names no test** (§4.1 point 4, the Opus read's first blocking finding, as it names it): a check, a
  typecheck, a discovery or a host failure is run on the candidate's base first, with the same selection; the queue stops if it
  fails there, as for a base that does not build; otherwise that gate's own selection is run over the prefixes. Prefix builds alone
  serve only a compilation failure (Codex 2).
- **A failure the base shares but does not inherit** (§4.1 point 4, §5.2, the Opus read's second blocking finding, as it names
  it): halving compares the candidate's failure fingerprint, not pass and fail, so a changed fingerprint on a failing base pins a
  member; an expired known red stops the queue until a direct commit fixes or skips it, and D207 records that true scope.
- **Attribution that is causal** (§3.5, §4.1, Codex 3): every prefix of a red candidate is run on the failing class or file, so the
  member at the last transition into the failure is the one handed back and an earlier one a later member repaired is not; a
  long range in the recall proof is checked between its halving's probes, and an ambiguous one is unsettled.
- **What a records-only re-form runs** (§4.1 point 3, Codex 1): the whole cheap tier, and only the `service` suite deferred, for
  paths whose lane rule reaches no other gate; `.claude/knowledge/adoption.md` is not such a path.
- **The load the two runners may make** (§5.1, Codex 5 and 6): the background's whole simultaneous set stated (its git half and
  one rehearsal today, two after CISCOPE1f), the scoped tier beside that set at worst, MEMGATE1's memory wait in the scoped run's
  latency, memory reserved and charged per process group in both physical memory and commit, and a reservation released only when
  its last process has ended.
- **The staged content bound to its receipt** (§5.3, Codex 8): the commit's tree, the receipt's tree and the content the build
  reads must be one tree; a dirty checkout is refused; the receipt's run kinds are named.
- **The statements and corrections** of both reads: the immutable queued request, the runner's detached launch, promotion moving
  the parent's checkout, the gate store kept per gate, `service` priced with its HTTP host, the twelve tool-test files, the release
  workflow's real shape, clearing known red by main's lineage, the recall proof's range start, price and threshold, row f's one
  concurrency limit, and the reversed D180 and CIFAST1 rejections named (§9).

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
D115, D180, CI1, GATESLIM1 and PROCREACH1; §10 the questions Codex's owed read of the final text is asked; §11 the reads of
rounds 1 to 5, answered.

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
  Process category is real and never re-run (`:3559`); D180 rejected retrying the Node suites in general. Two more retries are
  of the machine, not the test (round 4 left them out, Opus r4 3): MEMGATE1 runs a gate that died of memory (`memoryDeath`,
  `:2244`) again once, whole and alone, after its memory is free, a pass reading FLAKE of memory (`againOfMemory`, `:3744-3765`;
  the header at `:270-276`), except a Process half whose dead tests are named, which runs those tests alone as FLAKE1's are
  (`:275`, `:3533`, `:3569-3574`; round 5 said every such gate runs whole, Opus r5 10); and LEFT1 runs a rehearsal that died, rather than failed, again once, whole (`:3540-3557`).
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
7. **Skips** (§5.2; round 3 assigned it to the census without listing it, Opus r3 12): a skipped test whose reason does not begin
   with a row open in `TASKS.md` (`[Fact(Skip = "<ROW>: <why>")]`, `test.skip('<ROW>: <why>', …)`). It reads the backlog, so a
   records commit that closes a row a skip still names fails it (§4.1 point 4 says what that does to the queue).

### 3.5 Recall: the proof and the running miss count (round 3)

The checks above say which units exist and that the declared edges hold. They cannot say whether a unit's real dependencies fall
inside its reach. Recall is measured, twice, and both measures judge **causally** (Opus r2 3, Codex r2 7): a failure is held
against the one commit that introduced it, never against selection somewhere in a range.

**What each failure is judged by.**

- **Its introducing commit**: the first-parent commit of main that turned the failing unit red **with this failure's
  signature** (matched as §4.1 point 4 matches a failing test: its full name with its case, its exception type and its
  assertion's location, its frame's method and the text of its source line, never its line number and never its message line
  where a location can be read, Opus r4 1, Opus r5 1), and after which it stayed so. It is found by running the unit's whole
  class or file (never the test alone, §4.1 point 4) over main's first-parent commits between its last pass and its first
  failure, by the steps below. **Where the range starts** (Opus r3 11): at the newest recorded pass on main's line; where none is
  recorded (the month's FIX-LOG failures have none), at the newer of the commit that added the unit and the month's first commit,
  and the test must pass on a run there, else the range is unsettled, *no pass found*. A commit the FIX-LOG entry names is checked
  by one run on it and on its first parent rather than trusted; **where those two runs show the transition there** (the parent
  passing, the commit failing with the signature), the failure is judged as first seen at that commit, which is its **f** (Opus
  r5 9).
- **A transition is not yet an introduction** (Codex r3 3), **and a probe that shows the signature excludes nothing** (Codex r4
  1). Halving finds two adjacent commits, the first without the signature and the next with it; a fix and a later
  reintroduction can put that pair before the real one, and can sit wholly between two probes that both show the signature. Round
  4 stated that residue and still credited the pair's verdict, which let a real miss into the numerator. Round 5 credits a
  verdict only where the residue cannot change it. The search keeps two facts: **q**, the newest commit run on which the test
  **passes** (the real introduction lies after it), and **the verified suffix**, the commits from some **v** to the first
  failure **f**, every one of them run and showing the signature (the real introduction is v or earlier, since none of v's
  successors is a transition). **Only a passing run moves q** (Opus r5 1, as it names it): a run where the test fails with another
  signature, or where it does not exist (before it was added, or under its name before a rename), moves nothing, neither q nor
  the suffix. The commits that could still be the introduction are **the open set**, q's successor to v. **The walk comes first**
  (Opus r5 9, as it names it), since within its cap it finds the exact last transition whatever halving and the probes would
  have done:
  1. **The suffix is walked back**: f's parent, then its parent, each run, until the test passes on one (its successor is the
     last transition, exactly, and that commit's verdict is the failure's) or the open set's remaining commits all get one
     verdict by step 5's test. **A walked commit where the test fails with another signature, or does not exist, ends the
     search**: it is the newest run below the open set and not a pass, so the failure is **unsettled**, *signature changed*.
  2. **A cap of 48 runs a failure** on step 1. Only a failure the walk caps goes on to steps 3 to 5, its verified suffix kept.
  3. **Halving**, over the range below v, sets q at the newest passing run it finds.
  4. **Probes, recursive** (Opus r4 9): every stretch longer than sixteen commits between q and v is run at its midpoint, and the
     halves again, until no such stretch is longer than sixteen. A probe where the test passes moves q up to it and halving
     starts again above it; a probe that shows the signature, another signature, or no test, moves nothing.
  5. **The verdict's test.** Each commit of the open set is classified structurally, as the proof classifies an introducing
     commit (below): its kind (a merge or a direct commit) and whether its diff against its first parent reaches the unit. The
     classification needs no build and costs seconds a commit. **If every commit of the open set gets the same verdict**, that
     verdict is the failure's, whichever of them introduced it; otherwise the failure is **unsettled**, *open set disagrees*,
     with the open set's size and its verdicts' counts in the output, and counted as missed.

  So a transition within 48 commits of f is found exactly by the walk, and no verdict rests on an unprobed stretch: the residue
  round 4 stated is now either shown not to matter or counted as missed. A change of signature between the introduction and f (a
  renamed test, a changed exception type) is never credited to the commit that changed it: the walk stops there as *signature
  changed*. A line inserted above the failing assertion changes no signature, since the location is its source text (§4.1 point
  4), and the walk passes it.
- A **range that cannot be settled** (the unit no longer builds at an end, a commit in it does not build, the failure does not
  reproduce, no pass found, the signature changed, an open set past the cap that disagrees) is kept explicit, with the reason. A commit of the walk or the probes that does not
  build is skipped by the walk and named; where it lies in the open set and the open set's verdicts disagree, the range is
  unsettled.
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
- **One regression is counted once**: sightings that share the unit, the signature (§4.1 point 4, §5.2) and the introducing commit are one
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
  CI run names it. The output states each source's span and count, **with its observation lag beside its count** (how long after
  its introduction a failure is first seen in that source, which decides how far the walk must reach, Opus r5 9), and **the
  recall over the FIX-LOG's failures alone**, apart from the recall over all.
- **The first results are kept**: the first replay's list is written as it stands, before any repair. Each miss is then fixed in
  the census in the same row, by an edge, a declaration or a whole-suite entry, with an expected-edge fixture (§3.4 check 6); the
  replay runs again, and its list is reported beside the first. A miss the census cannot fix is named and stays in the count.
- **Its price, estimated** (Opus r3 11, restated: Opus r4 9, Codex r4 1, Opus r5 9). The month holds **2,153** first-parent
  commits (`git log --first-parent`, 2026-09-24 to 2026-10-24, at `a6991805f`; round 4 said "a thousand-odd"), and a failure with
  no recorded pass starts its range at the month's first commit or where its unit was added. A failure costs **the walk's runs,
  its transition's distance from f, at most 48**; only a failure the walk caps adds its halving, about **eleven runs** (log₂
  2,153), and **the recursive probes, about d/16 runs** where d is the distance from v down to halving's newest pass; the
  structural classification of step 5 is not a run. Each run builds a historical tree in a worktree of its own under the
  repository's ignored `local/`: a .NET build of three to eight minutes where the tree moved far, and the TypeScript suites'
  dependencies installed from that tree's lockfiles where they differ, two to four minutes. A failure whose transition sits by f
  costs one or two runs; one the walk caps costs 48 + 11 + d/16, 194 at most with d the whole month. Round 5's "most settle at
  step 3" is withdrawn (Opus r5 9): 991 of the month's 2,153 first-parent commits are direct commits, *unscoped*, so an open set
  of more than two or three commits nearly always holds two verdicts. **The cap meets the observation lag**: at about 69
  first-parent commits a day, 48 runs reach about seventeen hours back, so a failure first seen later than that is settled only
  where its FIX-LOG entry names the breaking commit and its two runs show the transition there (above), and otherwise is
  unsettled and counted as missed. At some forty failures that is tens of machine-hours, and some hundreds at the worst, run over
  nights. CISCOPE1c runs the first ten failures, states their measured cost and how many the walk settled, how many it capped and
  how many of those step 5 settled, and only then runs the rest.
- **Where it runs before CISCOPE1e** (Codex r4 5). The admission record of §5.1 lands in CISCOPE1e, which waits on this proof,
  so the proof cannot run at it. Until it exists, a historical run is a guest as a run beside is today: lowered to below normal
  priority before anything is built (`lowerPriority`, `:4865`, CIPRIO1), with build servers of its own (`BESIDE_ENVIRONMENT`,
  `:4879`), each heavy step behind MEMGATE1's `memoryGate` (`:2331`), one historical run at a time, and none started while the gate
  tree's lock is held (a CI run, a run beside or a stage). A run interrupted is run again later; no
  verdict of it is written anywhere but the proof's output. CISCOPE1g's coverage recording, which may land before CISCOPE1e, runs
  under the same rule until then, and moves to the admission record when CISCOPE1e lands it.
- **What it decides** (Opus r3 11). CISCOPE1e does not start while the repaired list's lower bound over all failures is below 0.9,
  or while a miss the census cannot fix lies outside the give-ways §4.3 names; either goes to the steward with the list, who
  decides whether CISCOPE1e starts, narrowed or not.

GENREGEN1's two driver censuses are the expected first miss of the class census, and *selected* under §4.2, since the change
reached the driver's fast half, which runs whole (§4.2 point 2).

**The running miss count** (CISCOPE1d's records, CISCOPE1e's scoped runs). It is fed from two places: a failure the background run
finds on a unit that passed on an earlier background run, and a failure a candidate finds inherited from its base (§4.1 point 4).
Each new failure, deduplicated as above, has its introducing commit found by the search above, run in the background at the
background's admission (§5.1). The verdict then reads the **scoped run of the candidate that reached main with that commit**
(`local/scope-runs.jsonl`, one line per candidate run: its base, its members, its tree, the units it selected, ran, deferred and
their verdicts): a run on a candidate that was abandoned, or bisected away, proves nothing about the promoted tree and is not read.
A candidate re-formed after a direct commit has several runs (Opus r3 11): its last form's run is read, with each earlier form's run
whose unit verdicts that last run took as standing, since those are the evidence its tree was promoted on.
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
- **The order is records commit, then preview, then queue** (Opus r5 5, as it names it): the parent makes its records commit
  on the branch first and previews after it, so the diff it reads holds its records and the tip it queues is the tip it
  previewed; a records commit made after the preview moves the tip, and the queue refuses it (below). The dispatch skill's merge
  step says that order, changed by row 5.
- **The tool's merge commit holds exactly**: the branch's merge, its records commit included; what the tool writes into every
  merge today (the orientation index, the generated folders); and the union records it resolves today. Its message is the
  parent's file, verbatim, with the trailer lines the file carries. Nothing else enters it.
- **Records written after promotion** (a record the parent could only write once the merge landed) are a direct commit on main,
  and move the candidate in flight's base (point 3 below), by records only.

**The queue.** `merge-branch.mjs <branch> --message <file>` puts the branch on `local/merge-queue.json`, a durable record under the
tool's lock, says its place, and waits for its outcome; the parent may end the wait without affecting the queue, and `--queue`
reports it. **The request is frozen at enqueue** (Codex r3 4): the record keeps the branch's tip commit at enqueue, and the
message file's bytes; `--preview` records the tip it showed, and the queue **refuses a tip that differs from the branch's last
`--preview`**, saying both, so what is queued is what the parent read (Opus r4 10). Every form and re-form of a candidate
merges that commit with that message, never the branch's ref or the file as they later stand. A branch that moves while queued, or a message file edited, changes nothing
queued; the parent requeues it to change them (`--drop-batch`, then the branch again), and `--queue` says when a queued tip is no
longer the branch's. Today's batch records branch tips the same way (`state.tips`, `:4452`).

**The runner is a detached process** (Opus r2 8). Round 3 said "as `--ci --watch` is", which is wrong: `ciWatchVerb` runs its loop
in the invoking process, and `main` awaits it (`:5645`, `:5723`; Codex r3 10). The first invocation that finds no live runner
starts `node tools/merge-branch.mjs --queue-run` detached (`spawn` with `detached`, its standard streams to
`local/merge-queue.log` in the main checkout's `local/`, no window), and waits for the runner to write its process id and start
time into the record, its acknowledgement; no acknowledgement within thirty seconds is said, with the log's last lines, and the
request stays queued. An invocation that finds the record's runner gone starts another the same way. The runner:

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
   direct commit, the candidate is formed again on the new tip from the same frozen requests, and the scoped set runs again; by
   standing it runs only the units the direct commit's paths reach.
   - **A base moved by records only** (Codex r3 1). A direct commit is *records only* when every path's own rule in `REACH`, the
     first that matches it in order (`ruleFor`, `:1574`, which `pathsReaching` asks at `:1645-1653`), is one of three: the
     records lane (`:1234`), `docs/**` and its neighbours (`:1249`), or `docs/index/**` (none, `:1244`) (round 4 said "names no gate
     but `service`", which also admitted the rules that name no gate, `src/Daoris.Cli/test/**` at `:1189`, the `tools` lane at
     `:1233` and `.mcp.json` at `:1253`; harmless, but not what was meant, Opus r4 10). A path whose earlier rule names more is not
     records only: `.claude/knowledge/adoption.md` reaches `driver` as well (`:1129`), and `daoris.gates.json` or `daoris.lanes.json`
     every gate (`:989-997`). The re-form runs **the whole cheap tier**, as every candidate does (§4.2 point 1: `universal`,
     `code-map`, the census check with the rest), and the `node --test` files and vitest files the paths reach by the closure;
     it defers **only the `service` suite**, written to `local/scope-runs.jsonl` as deferred with the reason, a give-way §4.3
     names. Round 3's four record checks alone are withdrawn. Today's `recordMatcher` (`:1842`), a narrower list that decides
     which record differences a standing verdict may forgive (round 4 said "what a merge may resolve", which is
     `resolveBacklog` and `mergeByBase`'s, `:1429`, `:1392`; Codex r4 6), is a different question and is not used here.
   - **Promotion moves the parent's checkout** (Opus r3 5): a fast-forward there changes its files under whatever the parent is
     doing in it. The runner promotes **only onto a clean checkout**: no tracked change and no merge in progress (`git status
     --porcelain --untracked-files=no` empty, `mergeInProgress`, `:2572`). Otherwise the candidate stays `green`, `--queue` and
     the waiting invocation say what holds it, and the runner tries again each minute and at `--continue`. Every member carries
     its records commit, so an uncommitted `TASKS.md` in the checkout holds promotion until the parent commits it, which is the
     parent's own records habit already. A green candidate whose base main has since left re-forms as above.
   - **Progress under direct commits** (Opus r3 5). Each direct commit re-forms the candidate in flight, and the re-form runs only
     what that commit reaches, so a candidate starves only while code direct commits arrive faster than their own reach runs. That
     is not ruled out, so it is reported: a candidate re-formed three times running is said by `--queue` and the parent's watch as
     *held by direct commits*, with their count, and the parent decides whether to hold its direct commits until it promotes. The
     replay of 2026-10-21 (§8 row 5) reports each candidate's re-forms and whether any was held so.
4. **On red, judges each failure, then bisects** (Codex r2 3, Opus r2 2 and 7; round 4: Opus r3 1, 2 and 4, Codex r3 2 and 3;
   round 5: Opus r4 1, 2 and 3).
   Every failure has a **signature** it is matched by throughout one judgement (the base run, the run that checks it reproduces
   on the whole candidate, each prefix, the adjacent re-runs, and §3.5's search). **A failing test's signature is its full name
   with its theory case or data row, its exception type and its assertion's location** (the first stack frame in the test's own
   file; for vitest and `node --test`, the full test path and the assertion's location), **never its message line** (Opus r4 1, as
   it names it): a message can carry a token each run makes fresh (a GUID, a session or tree id such as `s-a2762239`, a hash, a
   set printed in hash order, an assertion's actual value), and round 4's equality on it left a reproducible failure unresolved
   and never inherited. **The location is the frame's method and the text of its source line, never its line number** (Opus r5
   1, as it names it): the line's text is read from the tree the run used, normalised as messages are, with its ordinal among
   identical lines of that method, since every judgement below compares runs on different trees and a member or commit that adds
   a line above the assertion would otherwise read as another failure; §5.2's fingerprint keeps file and line as the record's
   identity. The message line enters the signature only for a failure whose location cannot be read. **For a gate that names no
   failing test**, the signature is the gate, the step that failed (build, discovery, host, or the check's own exit) and what the
   step names: for a build, **the set of distinct compiler error codes with their files** (`CS…`, `TS…`), **matched as sets**,
   since MSBuild prints each project's errors as its node finishes and the first error differs between runs (Opus r5 6, as it
   names it); otherwise the first error line. Every message or error line a signature uses is normalised for numbers, paths,
   **GUIDs and hex runs of eight characters or more**. §5.2's
   full fingerprint, message line included, stays the record's identity in `ci-findings.json` and the history; the signature is
   only how runs are matched. A prefix is the base plus the candidate's first *k* members, and every run below asks one question
   of it: **does it fail with this signature**, never only whether it passes.
   - **A death of memory is the machine's, never the candidate's** (Opus r4 3, as it names it). A gate whose failure `memoryDeath`
     reads (`:2244`) is **never run on the base and never over the prefixes, never stops the queue and never hands a member back**.
     The scoped run keeps MEMGATE1's run again: the gate runs again once, whole and alone, once the scoped tier's other gates have
     ended and its memory is free (`againOfMemory`, `:3744-3765`), a Process half whose dead tests are named running those tests
     alone as FLAKE1's are (`:275`, `:3533`, `:3569-3574`; Opus r5 10), and a pass reads FLAKE of memory, which counts as the
     gate's pass for the candidate, as it does today, and is reported. A second death of memory **holds the candidate**, said as
     memory on `--queue` and to every waiting invocation, until the admission record (§5.1) shows the gate's need free and
     reserves it; then it runs again. **Only on that run after the hold, and only where the gate's own job's peak committed
     memory exceeded its reserved need, is a death of memory the gate's** (Opus r5 3, as it names it; `PeakJobMemoryUsed`,
     `ProcessJob.cs:354`): it is then judged as a failing gate that names no test (below), its base run admitted the same way. A
     death within its need stays the machine's, since the admission record holds only the two runners' gates and the workers,
     the owner's install and its sessions may take the memory it was admitted on (they ran the machine out of commit on
     2026-10-22, `:259-261`), and it holds the candidate again, said. So under load the base is not run to die the same death and
     stop the queue, and a prefix that died of the machine's load pins nobody.
   - **A failing test already on the base is inherited, known red or not** (Opus r2 2): each failing unit's whole class or file is
     run on the candidate's base, and a test that fails there with the same signature is **inherited for that candidate**,
     **filed to `ci-findings.json` at once as known red** (its first sighting recorded then, §5.2), **never pinned to a member**,
     and does not count against it. A test already known red with the same signature skips the base run only when the base is
     the commit it was last seen failing on.
   - **A failing gate that names no test** (Opus r3 1, as it names it; Codex r3 2): a check (`universal`, `code-map`, the census
     check, `daoris check`, the documentation tools, `release-prep --check`), a typecheck (`web-types`, the CLI's), a compilation
     failure, a discovery failure (a suite that printed no result, `flakeDecision`, `:2041-2043`) or a host failure (a run that
     failed outside a test, `:2044`). **That gate is run on the candidate's base first, with the same selection and the same
     operation** (the same check; the same `dotnet test` filter or file list, which builds, discovers and hosts as the failing run
     did). If it fails there with the signature, **the queue stops**, as for a base that does not build: it says so on `--queue`
     and to every waiting invocation, files the failure to `ci-findings.json` for the parent, hands nothing back, and forms no
     candidate until main's tip moves; main is broken and the parent's to fix by a direct commit. The likeliest case is a direct
     records commit that breaks a documentation check, or closes a row a skip still names (§3.4 check 7). **This stop is a
     deliberate exception** to the blocking D180 removed (Opus r4 6), of the same scope as the expiry's below, and recorded so in
     D207 and §9: while main fails any gate that names no test (the documentation tools, `daoris check`, `release-prep --check`,
     the census check, `web-types`, the CLI's typecheck, or a fast half's compilation, discovery or host failure, such as a test
     that crashes its host), **every merge waits at once**, with no grace, where a failing test on main is inherited for 72
     hours. It is chosen because every candidate formed on that base fails with the same signature, so none can be judged, and the
     fix is one direct commit. Since a host failure can be transient, the stop is not left to main moving alone: **`--queue
     --recheck`** runs the stopping gate's selection on the base again, admitted as any gate, and a pass there lifts the stop and
     forms the next candidate; the runner also rechecks once by itself, an hour after the stop, and says each recheck. If it does not fail on
     the base, **that gate's selection is run over the prefixes**, not a build alone: a compilation failure may be found by
     building each prefix, since a build is that gate's failing operation; a check, typecheck, discovery or host failure only by
     running the gate's selection there.
   - **A failure the base shares but does not inherit** (Opus r3 2, as it names it). Two cases. *A changed signature on a base
     that already fails*: the base fails, but not with the candidate's signature, so halving on the signature still finds its
     transition and pins a member, which the pass-or-fail halving of round 3 could not. *A test whose known red has expired*
     (§5.2): the base fails with the same signature and it is no longer inherited, so no prefix lacks it and no member can be
     pinned. **The queue then stops**, as above: no candidate is promoted or handed back until a direct commit on main fixes the
     test or skips it under its row (§5.2), `--queue` and every waiting invocation say which test and which row, and the head
     candidate is kept to re-form on that commit. That is the true scope of the expiry: **every merge waits**, not only those that
     reach the test, and D207 records it so. It is chosen over handing back the members whose paths reach the test's gate, because
     under §4.2's basis a known-red unit the scoped run may run runs in every candidate whatever its members reach, so that rule
     would hand back members that did nothing, and because the fix is one direct commit the parent can make at once. **Only a
     unit the scoped run may run can stop the queue so** (Codex r4 4, Opus r4 7): a known red in a browser spec or a rehearsal,
     which no scoped run runs (§4.2 point 6), never meets a candidate and never stops it; it waits at the stage, which refuses it
     (§5.3), and the parent's watch says it past its 72 hours. **A partial fix is handed back**: a member that changes a known
     red's signature (another assertion now failing, another exception type) is pinned and handed back even when it moved main
     toward green, since the queue cannot tell a partial fix from a new failure; the parent's way through is a full fix, or the
     skip under its row.
   - **Bisection by the whole class or file**, never the test alone, so a test another polluted reproduces (FLAKE1 never re-runs
     such a failure alone, `:245-249`). The class, file or gate selection runs on **every prefix** of the candidate, at most
     eight, rather than halving to the first transition (Codex r3 3): over eight members the base and the whole candidate have
     run already, so that is seven runs where halving takes three, and it gives the whole sequence. Halving finds *a* transition; the sequence `PASS, FAIL, FAIL, PASS, FAIL` has the
     whole candidate's failure introduced by the last member, after the third repaired the first's. So:
     - **monotonic** (no signature, then the signature to the end): the member at the transition is **pinned**, and where the
       members before it are needed (it shows no signature alone on the base) it is handed back as **pinned with** those members
       named;
     - **non-monotonic**: the member at the **last** transition into the signature is handed back, its hand-back naming every
       earlier transition as an **interacting** group (a member whose failure a later one repaired is named and not handed back);
     - independent failing units are run side by side over the same prefixes' builds.
   - **A prefix that cannot run the selection** (Opus r4 2, as it names it): a prefix that does not build, or whose selection
     fails to discover or host, with a signature other than the one being judged (a member broke compilation and a later one
     repaired it) is neither the signature nor a pass, and **makes the failure unresolved**, below. A failure that is itself that
     build's, discovery's or host's is judged on it as on any prefix.
   - **A flake mid-bisection** (Opus r3 4): before a pinned member is handed back, the two prefixes either side of its transition
     run once more; a result that differs makes the failure unresolved, below.
   - **Unresolved**: a failure whose class, file or gate selection does not fail again with its signature on the whole candidate
     (a flake, an order across classes), whose prefix runs disagree on a second run, or which a prefix cannot run. It is run once
     more as its gate's selection on the candidate, and **a pass on that run, or on any run of this point, never makes the
     candidate green** (Opus r3 4): retrying a fast .NET or Node failure is what §4.2 keeps out (`:3559`; D180). The retries the
     scoped run keeps are today's, and none of them is this point's: FLAKE1's re-run alone of a Process half's failures,
     MEMGATE1's run again of a gate that died of memory (above), and LEFT1's of a rehearsal that died, which no scoped run runs
     (round 4 called FLAKE1's the only retry, Opus r4 3). The failure is written to `ci-findings.json` as a flake for its unit's
     row, and then:
     - **a candidate of several members** runs them **one at a time** as candidates of one;
     - **a candidate of one** (the quiet hours' usual case, every `--one-at-a-time` candidate, and every member split out above)
       is **handed back as *unresolved***, its failure, its runs and the filed flake named, and **the queue goes on** with the
       next candidate (Opus r4 2, as it names it). It is never formed again by the runner: queuing it again is the parent's act,
       said in the queue's record as a requeue of an unresolved member, so a fast failure is never retried unseen and never
       without bound.

     Nothing is pinned on a failure that cannot be placed; a member handed back unresolved is named as unplaced, not as guilty.
   - **No unit verdict is written** from a bisection's runs; they are attempts in the history (§7.1), not passes.
   - **The retained candidate is judged again** before promotion: the members not handed back are formed anew on the base, and
     its scoped set runs on the new tree, whose standing basis runs everything the removed members' paths reach. It promotes only
     on its own green.
   - **The cost**: one reproducible failure over eight members is seven runs of one class, file or gate selection on warm
     incremental builds, minutes; the second run of two prefixes adds two. Several failures, a check run on the base and an
     unresolved one cost more, and the queue's replay reports them.

**What the queue serves** (Codex r2 5, Opus r2 9). A merge waits for the run in flight and its own, plus re-forms and red
candidates. At the measured rate (§2.7) and §4.4's walls that is an **estimate**, not a bound: about two driver runs, 30 to 56
minutes, at the busiest hour, one in a quiet one. It is not bounded by capacity: a burst can queue more than eight, a union reaches
more than any one member, every direct commit re-forms the candidate in flight (10 to 94 a day, §2.7; a records-only one costs the
cheap tier), **debt carried by standing evidence** (Codex r4 8): a git-half class one candidate invalidated and deferred has no
pass standing on any later tree until the background pays it, so it is in every later candidate's basis, whatever that
candidate's own paths reach (round 4 said "every candidate that reaches them", which underpriced it), and so is every known-red
unit a scoped run may run (§4.2); each red candidate adds its prefix runs and a run of the retained members; and a stopped queue
(point 4) waits for the parent's direct commit or a recheck. CISCOPE1e's proof replays 2026-10-21's
127 merges and 41 direct commits at their timestamps against the queue, with each merge's union scope priced, the overflow past
eight, the re-forms, red and restart cases injected, and a driver candidate whose git classes are deferred followed by
unrelated records and page candidates carrying that debt until the background pays it, and reports the backlog over the day and each merge's wait, p50, p90 and
the worst.

**Durable state, restart and the verbs** (Codex r2 4, Opus r2 19). `local/merge-queue.json` holds the queue and the candidate: its
base, its members in order with their frozen tips and message bytes, each member's merge commit, its state (`forming`, `running`,
`bisecting`, `green`, `promoting`, `promoted`, `handed back` (pinned, pinned with, or unresolved), `held` (on memory, on a
checkout that is not clean), and `stopped` with the failure that stopped the queue and its rechecks), the runner's
process id and start time, and the run's directory. A runner that dies leaves it as it was;
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
on one whose changes since reach it not (GATE6's standing, per unit, as `classPlan` judges classes). A failure runs until it passes,
as today's CI runs any gate whose standing verdict is a failure (`ciPlan`, `:5109-5114`). **Eligibility comes first** (Codex r4
4, Opus r4 7): points 1 to 6 below say what a scoped run may run at all, and the known-red rule applies within them. So **a
known-red unit the scoped run may run runs in every candidate, whatever its members reach** (Opus r3 2), as its own class or
file, not as its whole half when that half is not otherwise reached; §4.4 counts it and §5.2's expiry depends on it. A known-red
browser spec or rehearsal stays deferred (point 6), and a known-red git-half class takes the budget first (point 5). Of those,
the scoped run runs:

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
   the twelve `tools/*.test.mjs` files `npm run verify` names (`package.json:15`; Opus r2 16), and the page's vitest files that
   the candidate's paths reach. The other four (`as-merged`, which builds and runs the devkit; `knowledge-bench`;
   `question-bench`; `ux-count`, which starts Playwright's chromium) are in no gate by their own headers, and stay out of the
   scoped run as of every gate (round 3 said every file, corrected, Opus r3 9). A whole-suite file (§3.4 check 5) or an
   unresolved read runs its suite whole.
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
   chooses classes; it never stops one. **A known-red git-half class runs first and consumes the budget**, and is never deferred
   by it (Codex r4 4): it is the evidence a candidate's inheritance rests on, and it is one class. Known-red classes that alone
   overrun the budget all run, said as an overrun, and every other reached class is then deferred. The run says which classes
   ran, which reached classes are **deferred** to the background run, and any overrun of the prediction.
6. **No browser spec and no rehearsal** (Codex 6). `web`, `web-measured` and their production build, and the three rehearsals,
   are deferred to the background run whenever reached. A merge never waits on them; the install does (§5.3).

**Retries are today's** (Codex 3; Opus r4 3 named the two round 4 left out): a Process half's failures are re-run alone once
(FLAKE1); a gate that died of memory runs again once, whole and alone, after its memory is free (MEMGATE1's `againOfMemory`,
`:3744-3765`), a Process half's named dead tests alone (`:275`), with §4.1 point 4's rule for a second death; a fast .NET half's failure is real and never re-run; the Node suites
are not retried (D180). LEFT1's run again of a rehearsal that died is the background's, since no scoped run runs a rehearsal. A
build, discovery or host failure fails the run.

**Verdicts** (Opus r2 10, Codex r2 12). Each unit's verdict goes to a per-unit store, `local/unit-verdicts.json`, keyed by tree,
six kept a unit, which absorbs `class-verdicts.json` (Opus 16). The gate store keeps gate verdicts only, and every gate verdict
now records **the kind of run that gave it**, which it does not today (`:4069`): `scoped` (a candidate's run), `ci` (a run of the
watch or a plain `--ci`: ordinary, sweep or nightly, its run kind in its receipt), `stage` (`--ci --stage`), `full` (`--full`
and `--full --beside`) and `remedy` (`--rerun`, `--stale`, GATE6b's); round 3's `release`, which nothing local would write, is
dropped (Opus r3 6). **The gate store is kept per gate** (Opus r3 7): today it keeps 400 verdicts overall, the oldest dropped
first (`addVerdicts`, `:1758-1761`), about a day and a half, and scoped runs would write tens of cheap-tier verdicts a day into
it and evict a night's or a stage's passes. It keeps instead, for each gate, its newest 20 verdicts of `scoped` and `remedy`
runs and, apart, its newest 20 of the other kinds, so a scoped run never evicts a background, stage or full verdict. A
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
- **A base moved by records only**: the re-formed candidate runs the whole cheap tier, and only the `service` suite's scan of the
  documents is deferred to the background run, recorded as deferred (§4.1 point 3).

The install waits for the background run's own receipt and every gate of its plan passing (§5.3), with no waiver, so the scoped
run's deferrals never reach it. **One exposure remains, and round 4 overstated its absence** (Codex r4 7): a background run's
Process half may itself pass by class, composing kept verdicts (PROCREACH1, and CISCOPE1b's narrowed rule), and a class whose real
dependency the name census lacks keeps its old pass standing until the half's sweep. So a receipt plus standing passes does not
prove that such a regression was exercised before the stage: a census miss in a Process half can reach the install **within the
sweep's bound** (a day, `SWEEP_BOUND_MS`, held at the stage by `sweptOut`, the due record and the standing whole pass's age,
§5.3 point 2), as D180's PROCREACH1 note already accepts for today's install. Closing it would take a whole Process-half run on
the commit to stage, which this design does not require: the stage's wall would gain the git half's 24 to 44 minutes on every
republish, and the miss count (§3.5) measures how often the exposure is met. Beyond that bound, what a miss costs is
attribution: a later candidate finds it inherited, or the background run finds it; either files it as known red, the miss count
judges it against its introducing commit (§3.5), and it is fixed forward, as D180 accepts.

### 4.4 The expected wall

Rebased on measured walls (§2.2's ratio of 1.6 to 2.1 for a Process half), not on class time over width. **The scoped run is
scheduled by memory, as a CI run is** (CI1's `byMemory`, `:623-647`), **with GATEORDER1's wait dropped** (Opus r4 4): its
Process halves run one after another in the Process lane, and its light gates beside them as the admission record admits them,
rather than the Process tier waiting until the heavy light gates end (`:290-291`, `:645-647`), which round 4 cited while its
driver row ran `service` beside the Process tier. **It drops that wait on the precondition the CI dropped it on** (Opus r5 8,
as it names it; D180.md:428-433): GATEORDER1's reason was the heavy light gates' CPU load on the Process lane's clocks (the
0b44c7a harm, `:510-511`), which FAMILYFAIL1c met by priority, so **the scoped run's light lane runs below normal while a scoped
Process tier runs**, its Process tier at normal, as the CI's do (§5.1), and these walls stand without the wait. Per category of
merge, before the queue's wait:

| A merge that changes… | Runs | Expected |
|---|---|---|
| records and documents only | the cheap tier; the `service` suite whole, which the records lane reaches (`:1234`, `:1249`): its build and the core's 1 m 38 s with the HTTP host's 3 m 17 s of tests | 7 to 11 min |
| the page | the cheap tier, the reached vitest files, `web-types`; the `service`, `driver` and `modules` fast halves whole, which the page lanes reach (`PAGE_READERS`, `:969`, `:1205`) | 10 to 16 min |
| the CLI or `tools/` | the cheap tier, the reached `node --test` files; the driver's fast half whole, which the CLI lane reaches (`CLI_READERS`, `:972`); the `service` suite whole, for the member's records commit; `driver-process`'s reached classes where the CLI's lock helpers or the dispatcher's seam change (`:1178`, `:1186`) | 9 to 14 min; 15 to 22 with `driver-process` |
| the driver | the cheap tier; the driver's and the modules' fast halves whole (warm builds); the `service` suite whole, for the member's records commit, in the light lane beside the Process tier; the driver's other half by class (4.7 min by the model, 7 to 10 measured); the git half within its 8; the modules' half by class (1 to 2) | 15 to 28 min |
| a whole-suite file (`REACH`'s first rule, 6 of 80 merges) | everything above, every Process half whole but the git half's budget | about 30 min |

**`service` is in nearly every row** (Opus r3 8): its gate runs `src/Daoris.Service`, whose solution holds the HTTP host's tests as
well as the core's (`daoris.gates.json:8`; §2.4), and every member carries a records commit (`TASKS.md` on the records lane,
`:1234`; the archive and the FIX-LOG under `docs/**`, `:1249`), so every candidate reaches it. Round 3 priced it at the core's
tests alone and left it out of the page's, CLI's and driver's rows; both corrected. In the driver's row it runs in the light lane
while the Process tier, which makes the wall, runs, so the row's figure stands.

Round 2's first three rows left out the fast halves §4.2 point 2 runs (Codex r2 2, Opus r2 9); the figures above add a warm
incremental build of each reached project, about one to three minutes, and the half's own test time (§2.4). They are unmeasured as
a whole. **Carried between candidates, by standing evidence** (Codex r4 8): a git-half class deferred has no pass standing on
any later tree, so it is in every later candidate's basis, whatever that candidate's paths reach, competing for the git half's
budget there, and a known-red unit a scoped run may run runs in every one (§4.2), until a pass stands; a candidate after a deferral or with main red costs more than its members alone,
a records or page candidate after a driver one included; a re-form
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
| public release | the release workflow's own jobs (`.github/workflows/release.yml`): `devkit` and `desktop` on the input revision, `release` on a worktree it stamps first | its jobs' gates, as §5.3 says | its own |

Round 2's local `--ci --release <commit>` is dropped (Opus r2 14): the public release's gate is the workflow's own jobs,
unchanged. A sweep-only run gives no receipt a stage counts (§5.3 point 1).

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
- **The converse case.** A scoped Process tier due while background Process-lane gates or rehearsals are already running **runs
  beside them**; its FLAKE1 re-run alone does not wait for them either. A background gate is never ended for it.
- **What may already be running** (Codex r3 5, Opus r3 3). Round 3's "one background gate, never a second" was wrong: today's
  `laneSchedule` runs a rehearsal beside the git half (`BESIDE_GIT_HALF`, `:490`; `:628-637`), one rehearsal at a time, and a
  stage or a night runs the family and deployment rehearsals there. So the background's **simultaneous Process-lane set** is at
  most **the git half and one rehearsal** (8 + 10 GB by `MEMORY_NEEDS`, `:2208`), and after CISCOPE1f **the git half and two
  rehearsals** (8 + 20 GB), the limit §6.2 sets. A pair already started keeps going; no member of it, and no next rehearsal of
  the chain, starts while a scoped tier runs or waits. **Its light lane runs too** (Opus r4 4): the background's light gates
  are scheduled by memory beside its git half (`byMemory`, `:623-647`), and admission holds back only its Process-lane gates and
  rehearsals, so the background's whole simultaneous set is the git half with up to three heavy light gates, **8 + 3 × 6 = 26
  GB** by `MEMORY_NEEDS`, or the git half and a rehearsal, 18 GB, whichever its plan holds. That light lane runs below normal
  priority (CIPRIO1) and each of its heavy gates is admitted only above the foreground headroom below. The scoped tier's worst
  case is its whole Process tier beside that set.
- **The scoped run's worst wait, in two parts** (Codex r3 7, Opus r3 3). *A Process slot*: none, since it never waits for a
  background gate. *Memory*: each heavy scoped gate may wait up to MEMGATE1's bound, ten minutes (`MEMORY_WAIT_MS`, `:2212`;
  `:2365`), when the free memory does not cover it, and then runs anyway, as today; background reservations can be part of why.
  §4.4's walls leave that wait out, and CISCOPE1e's proof measures it beside the maximum set above. A timing flake the overlap
  raises is reported as FLAKE1's are, never hidden.
- **The scoped runner's priority**: normal, every gate, since a merge waits on it, but for **D180's priority split, kept**
  (Opus r5 8, as it names it): while a scoped Process tier runs, the scoped run's heavy light gates run below normal, as the
  CI's light lane does, so their CPU load does not reach the Process tier's clocks at equal priority (the 0b44c7a harm,
  `:510-511`), which is the precondition D180's CI1 note, point 5, names for dropping GATEORDER1's wait (D180.md:428-433).
  Keeping GATEORDER1's wait instead was the other choice; it would add the heavy light gates' wall to §4.4's driver row. The
  background stays a guest, its Process lane at normal priority (CIPRIO1, FAMILYFAIL1c). **This departs from D180's budget note, point 2, and is named as an amendment to
  it** (Opus r4 5): "a CI run is a guest on the machine … at background priority … A CI run beside a full slate of workers is not
  started" (D180.md:68-69), written after a git half under load slowed the builders' git until the harness refused dispatches
  (D180.md:60-62). The scoped run is a CI run a merge waits on, so it is not a guest, and it starts beside a full slate of
  workers. Its bound is what replaces the guest rule: **one scoped Process tier at a time** (one scope runner, one candidate),
  and **the git half within its eight-minute budget** (§4.2 point 5), never whole, so the load the note was written against, a
  whole git half of 24 to 44 minutes four wide under workers, is not what the scoped run makes. CISCOPE1e's proof watches the
  harness's dispatches while scoped runs go, and reports every refusal and every git command slower than a second it sees; a
  refusal fails the proof.
- **Memory is reserved, not sampled, in both measures** (Codex r3 6). MEMGATE1 judges free physical memory and free commit
  (`readMemory`, `:2284`), so a reservation holds its need in both, and the admission reads both. A gate is admitted when each
  sampled free figure, less the unused part of every live reservation in that measure, covers its need: physical memory less
  (reserved minus the job's working set), commit less (reserved minus the job's committed bytes), each never below zero. A
  process whose pages leave its working set keeps its commit charged.
- **A reservation is held by a job, and released only when the job is known empty** (Codex r4 3, Opus r4 8). Round 4 found a
  gate's group as CIPRIO1d and CIPRIO1e find a killed run's: `endOrphans` lists the recorded process's children
  (`tools/processes.mjs:169`), `endStranded` the orphans whose command line names the run's tree (`:189-205`). Both are cleanup
  heuristics: a grandchild under an intermediate shell that ended, with a relative command line, is found by neither; a failed
  process query answers an empty list; and the tree is the run's, not the gate's, so a gate's group counted its siblings' strays.
  An empty answer from them proves nothing. So:
  - **Each admitted gate starts in a Windows job object of its own**, named for the runner, the run and the gate, made empty
    first, the gate's process started suspended, joined, then resumed, and the job allowing no breakaway, so every process the
    gate starts, detached or not, is in it whatever becomes of its parent. That is how HEAVYRUN1a already holds a session's heavy
    gates (`Agents/Consoles/ProcessJob.cs:23-26`: `Create`, `Active`, `Terminate`, `WaitEmptyAsync`; "a gate that could not join is
    not run"), and the runners take the same mechanism, through a launcher both use (CISCOPE1e chooses between the driver's
    `ProcessJob` behind a small verb and a tool-side launcher, and says which). A gate that cannot be joined is not run, and is said.
  - **Membership is the job's**, read by the job's own accounting (its active process count and process list, its committed
    memory), not by a search of the process table, so an intermediate process's death loses no member and a sibling gate's
    processes are never counted.
  - **The job kills on close, and its launcher holds it** (Opus r5 4, as it names it, its first option): as HEAVYRUN1a's jobs do
    (`KillOnJobClose`, `ProcessJob.cs:95`, `:285-292`), the job ends every process in it when its last handle closes, and its
    name goes with it. The gate's launcher holds the job's handle for the gate's life. An admission read opens the job by name
    for query only and closes it at once, so it never holds the last handle while the launcher lives; where the launcher died
    during a read, the read's close ends what the launcher's death left, as the launcher's own close would have, and no live
    holder's gate is ever ended by a read.
  - **Release**: a reservation is released on a read that succeeds and finds the job's active process count zero, and on **a
    name no job answers**, which under kill-on-close is an ended job, as `EndNamedAsync` reads it (`ProcessJob.cs:256-268`).
    Any other failure to open or read the job, or an answer that does not parse, **keeps the whole charge**, said by the
    admission's report; the next read tries again. Unknown membership is never read as empty.
  - **Ending processes is never an admission read's decision**: the runner that takes over a tree's lock ends a dead run's jobs, as
    CIPRIO1d and CIPRIO1e end a killed run's processes today, and `endOrphans` and `endStranded` stay as that cleanup's second
    pass, never as the reservation's evidence. A job whose holder is gone and whose name still answers is held only by another
    handle; its reservation is kept until that handle closes and the job ends, or the cleanup empties it.
  - **Build servers**: every scoped gate runs with build servers of its own (`BESIDE_ENVIRONMENT`, `:4879`: no MSBuild node
    reuse, no MSBuild server, no shared compiler), as the background's gates and the rehearsals do, so no gate leaves a server
    alive in its job, or borrows one outside it.
  - **Elsewhere than Windows** the job is the gate's process group, and a process that leaves its group is not seen; this
    machine, where every figure here was measured, is Windows, and the proof says the platform it ran on.
- **Foreground headroom, sized to the scoped peak.** Round 3's 14 GB covered one Process half and one heavy suite; the driver's
  row runs a Process half beside up to three heavy suites (the driver's, the modules' and the service's fast halves), 8 + 3 × 6
  = 26 GB. The background leaves **26 GB** unreserved when it admits a gate, a figure CISCOPE1e's proof measures and declares
  beside the gates. A scoped gate past MEMGATE1's bound runs anyway (`:2365`) and says so; a background gate never does: it waits.
- **Background progress, conditional on memory.** A background Process-lane gate that has waited more than 60 minutes on scoped
  tiers takes the Process slot at the next moment the scope runner is between tiers, and thereafter is a gate already running of
  the converse case. That bound is on the slot only: its memory wait is not bounded, since the scoped run never waits for the
  background's memory. So a night under continuous merges finishes **when memory admits it**; while it does not, the overdue
  report (above) says which gate waits on memory and for how long, and the stage stays refused by the due record (§5.3 point 2).
- **Where it lands**: in CISCOPE1e, with the scoped run that brings the second heavy runner (§8 row 5), not in CISCOPE1f.

The background run never holds the scope tree, never ends for a merge, and never blocks one.

### 5.2 Its failures, and known red

A background failure is filed rather than waited on. The run writes `local/ci-findings.json` (the commit, the unit, the test, its
fingerprint, the first lines of its failure, the scoped verdict that unit last had), and the parent files each as a row or a FIX-LOG
entry and fixes it forward (D180 point 3).

**Known red is keyed by test and fingerprint** (Codex 8, Opus 5), and has **one rule** (Opus r2 2, Codex r2 6):

- **How a test becomes known red.** Two ways, the same record: the background run finds it failing on main's tip; or a candidate
  finds it failing on its base with the same signature (§4.1 point 4), which files it to `local/ci-findings.json` at once. The
  base is main's tip, so a failure a direct commit or a promoted miss introduced is known red from the first candidate that meets
  it, not from the next background run.
- **The fingerprint** keeps what tells two failures apart (Codex r2 6): the test's full name **with its theory case or data row**
  (today's readers cut the case off, `:2023`, `:2119`, and CISCOPE1c's failure reader keeps it, Opus r5 7), the exception type, **the
  assertion's location** (the first stack frame in the test's own file, file and line), and the first line of its message, numbers,
  paths, **GUIDs and hex runs of eight characters or more** normalised (Opus r4 1). For vitest and `node --test`: the full test
  path through its `describe` blocks and `each` row, and the assertion's location. Two cases of one theory, and two assertions
  with the same first line, are two fingerprints. A failure whose location cannot be read keeps its whole message, normalised the
  same way (round 4 kept it unnormalised, which made a run-specific token a new failure each run).
- **The fingerprint is the record; the signature is the match** (Opus r4 1). The fingerprint is the identity of a known red, a
  finding and a history line. Whether two runs show *the same failure* (inherited from the base, reproduced on the candidate, a
  prefix, a recall commit) is judged by §4.1 point 4's signature: the name with its case, the exception type and the location by its method and source text, never its line,
  the message only where no location can be read. Two runs whose signatures match and whose messages differ are one failure,
  its *last seen* message updated; the waiver, the search and the clearing below all match by signature.
- **Where it waives: only in the queue, and only on the base.** A candidate's failing test is inherited, and does not count against
  the candidate, only when it fails on the candidate's base with the same signature. That run is the evidence, never the census's reach,
  so the exclusion holds whatever the census misses (Codex r2 10). A known-red test skips the base run only when the base is the
  commit it was last seen failing on. **The stage never waives** (§5.3).
- **What counts against a candidate**: a new failing test in the same class, a changed signature (another assertion's source text or exception
  type, halved on the candidate's signature, §4.1 point 4, a partial fix included), a failing gate that names no test and does not fail on the base, and a known-red test that passes
  on the base.
- **Clearing, by main's lineage** (Codex r3 9). Round 3 cleared on a pass on any run, so a late background run on an older commit,
  or a bisection's run on a passing ancestor, could clear a newer failure. A known red is cleared only by a pass of the test on a
  commit that **descends, on main's first-parent line, from the commit it was last seen failing on**, given by a run whose
  verdicts count for the stage (`ci`, `stage`, `full`) or by the scoped run of a candidate that was then promoted. A bisection's
  or the recall proof's runs, a candidate's that was not promoted, and any run on a commit that is not such a descendant never
  clear it. **The first sighting is kept**: a sighting of the same test and signature while it is known red only moves its
  *last seen* commit, and one after a clearing that a later sighting contradicts (the signature seen again within a day of the
  clearing) restores the original first sighting rather than starting a new one. The expiry counts from the first sighting.
- **Expiry, a deliberate exception** (Opus r2 11; its scope corrected, Opus r3 2 and r4 7): a test known red for more than 72
  hours from its first sighting is no longer inherited. **The 72 hours count per test** (Opus r4 10): from the first sighting of
  the test known red under any fingerprint, kept while it stays known red, so a direct commit that moves the failing assertion or
  changes its message renews nothing. Since a known-red unit the scoped run may run runs in every candidate whatever it reaches
  (§4.2), every candidate then fails with the signature its base shares, no member can be pinned, and **the queue stops** (§4.1
  point 4): **every merge waits**, not only those near the test, until a direct commit fixes it or skips it under its row. A
  known red in a browser spec or a rehearsal never stops the queue, since no candidate runs it; the stage refuses it (§5.3), and
  the parent's watch says it once it is past 72 hours. That is the blocking
  D207's *Rejected* list says known red exists to avoid, chosen here on purpose and recorded so in D207 with that scope: a waiver
  must not outlive the fix-forward D180 expects within a working day, and the one direct commit that ends it is the parent's to
  make. The way out short of a fix is a **skip that names its row**: `[Fact(Skip = "<ROW>: <why>")]` or
  `test.skip('<ROW>: <why>', …)`, and the census check (§3.4 check 7) fails on a skip whose row is not open in `TASKS.md`. The
  parent's watch says a known red twelve hours before it expires.

### 5.3 What a republish of the owner's install stages

**The steward's direction for round 2**: the owner's install is republished only on a commit the background run has passed. It is
never staged on scoped-green alone.

The stage's judgement is `--passed` on the merge tool (`stageJudgement`, `:1926`), and **every ordinary stage entry point asks
it** (Codex r2 12): a plain `publish:desktop --stage` through `ungatedRefusal` (`desktop-publish.mjs:871`, `:1163`), which round 5
turns into a refusal that names the gated command (point 1), and `--stage --from-gated <commit>` through the child it hands the
same flags (`:1002`, `:1137`). The rules below go into `--passed`,
so both paths meet them; `--force-ungated` stays the owner's explicit override, said in its output and recorded as forced in the
stage history below. A commit is staged only when:

1. **A background run has passed it, on that commit** (a *receipt*). Every run of the gate tree appends its receipt at its end to
   `local/ci-runs.jsonl` in the main checkout's `local/`: the commit, its tree, the run's kind (§5.1's matrix), when it started and
   ended, whether it finished or was ended, and each gate's verdict with the kind of run that gave it. The file is durable and
   extends `local/ci.json`, which stays the watch's record of its newest 50 runs (`CI_RUNS_KEPT`, `:5069`; `noteCiRun`, `:5351`),
   and is apart from the gate tree's record, which every run rewrites. **The receipts that count** (Opus r3 6) are of a run that
   **finished** and is an ordinary or a nightly run of the watch, a plain `--ci`, a `--ci --stage <commit>`, or a `--full
   --beside`, which `docs/development.md` keeps as the run of every gate; a sweep-only run's does not, since it ran one half. A
   `--ci --stage <commit>` on any commit counts: it runs every gate its verdicts lack on that commit, the rehearsals included,
   which is the full set's pass the stage asks for. A run that ran nothing because every verdict already stood is a receipt: the
   evidence is the standing verdicts, which §4.2's writing rule keeps to gates run whole, so every gate passing means nothing
   deferred is outstanding. A receipt is matched **by tree**, not by commit name alone.
   **The content staged is the tree received, throughout the build** (Codex r3 8; held through the build, Codex r4 2).
   `--passed` judges the checkout's content tree (`contentTree`, `:1819`; `judgeCheckout`, `:4326-4329`), which today may hold
   uncommitted changes. Round 4 required three trees to be one (**the commit's tree, the receipt's tree, and the content tree the
   build reads**), refused a dirty checkout, and read the source again after the build. That checks two moments: a plain
   `--stage` builds from the checkout through several build and pack steps in turn (`desktop-publish.mjs:1163` onward: the page's
   bundle, the doctrine tool's `npm pack` at `:1220-1235`, the .NET publishes), so a source changed after the first read, consumed by
   one step and restored before the last read passes both, with a mixed artifact; and a promotion (§4.1 point 3) fast-forwards
   the clean main checkout during that build. So **every ordinary stage builds from a tree held for it, never from the
   checkout**:
   - **The source is the gate tree at the receipted commit, under the gate tree's lock**, as `--from-gated` already builds
     (`gatedSource` with `hold`, `desktop-publish.mjs:947-994`: the lock taken with `holder: 'stage'`, the tree's HEAD read again
     under it, `:1137` building there, the lock released after). While the stage holds it no CI run moves the tree (a run is
     refused by the lock), no promotion touches it (promotion moves only the main checkout), and no tool writes in it.
   - **A plain `--stage` builds in two cases only** (Opus r5 2, as it names it). The `--from-gated` child is itself a plain
     `--stage`: `childArgs` drops only `--from-gated` and `--to` (`desktop-publish.mjs:997-1008`, spawned at `:1137`), so the child
     takes the plain path (`:1154-1167`) with the gate tree as its root. So a plain `--stage` builds only **under `_fixtures`**,
     as GATE3 has it (`ungatedRefusal`'s exemption, `:872`), which the deployment rehearsal's step 9b stages into
     (`tools/deployment-rehearsal.mjs:455-460`, `:1659-1661`); and **inside the gate tree, while the tree's lock is held with
     `holder: 'stage'` by its parent process**, the lock's commit being the tree's HEAD, which is the delegated child under its
     parent's held lock. Anywhere else, the main checkout or the gate tree without that lock, it refuses, naming the commit it
     would stage and the command that stages it from the gate tree (`--stage --from-gated <HEAD>`, after `--ci --stage <HEAD>`
     where the tree does not hold it); the driver's `update` verb, which tells the owner to stage with the plain command
     (`UpdateCommand.cs:87-88`), names `--from-gated` instead. `--force-ungated` keeps building from the checkout, as the owner's
     explicit override, said and recorded as forced.
   - The three trees must still be one, read under the lock before the build and again after it and before staging, refusing if
     either read differs. **The second read is the child's**, made just before it promotes the stage (`promoteStage`,
     `desktop-publish.mjs:1410`), since the child stages and the parent only waits for it. What this cannot see is a process outside the tools writing into the locked gate tree and restoring the
     file before the second read; the tree is the tools' own under `local/`, and nothing in this repository's workflow writes
     there but the lock's holder, which is the isolation CI1's stage hold already rests on, said here as its limit.

   The stage needs that receipt, and then **every gate of its plan passing with a verdict that stands on its
   tree**, as `stageJudgement` asks today: the
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

**A public release** keeps its own gate, the release workflow, unchanged by this design (§5.1). It is not one exact commit's
receipt (Codex r3 11): its `devkit` and `desktop` jobs check out the input revision and test it, and its `release` job stamps the
version (`release-prep.mjs --version`, `.github/workflows/release.yml:213`) and re-syncs the doctrine (`:222`) in its own
worktree, then runs its gates there (`:234-321`: the code map, the orientation index, the family rehearsal, the web's types, rules,
unit and end-to-end suites among them) before it commits the release. Round 3's "every gate itself on the exact commit" is
withdrawn; the workflow's multi-job, stamped-worktree verification is the release's gate as it stands.

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
(FAMILYFAIL1, REHLANE2b's note) and show the scoped runs' walls unchanged. **The limit is two at once** (Codex r3 12): round 3
allowed two reservations and asked a proof of all three side by side, which cannot be one policy. Two rehearsals run at once and
the third waits for either to end; the family rehearsal, the longest, starts first beside the release rehearsal, and the
deployment rehearsal takes the first slot freed. Expected on a quiet machine: the rehearsal lane's wall falls from the chain's sum
(31 to 53 minutes) to about the longer of the family rehearsal (14 to 23, §2.1) and the release then the deployment rehearsal
(1 to 4, CIFAST1's figure, then 14 to 22), **about 15 to 26 minutes** (round 4 said 23 to 30, Opus r4 10); on a working day §5.1's admission record will serialise some of it, and the proof states the measured wall. Side by side, a background rehearsal is still one Process-lane gate
for §5.1's admission: none starts while a scoped Process tier runs or waits. The three trees' first installs and cold builds are not priced here;
CISCOPE1f measures them once, and they are paid again only when a lockfile changes. With the rehearsals side by side, a stage's
`--ci --stage`, which owes only the family and deployment rehearsals, waits for the longer of the two, not their sum.

## 7. Each test's value: TESTVALUE1 folded in

### 7.1 What is measured, per unit

- **Runtime**: its own time from the runner's report (trx durations per class and test, vitest's JSON report, `node --test`'s),
  kept per run.
- **Attempt-level history** (Codex 14): `local/test-history.jsonl`, append-only, one line per **test attempt** per run (tree,
  commit, run kind, attempt: first or alone, verdict, fingerprint, time), written by every scoped and background run. It is kept
  for ninety days and compacted beyond to one line per test per day (Opus 16).
- **A catch** is a failure whose signature is new, that the scoped run's bisection or the base re-run attributes to one commit,
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
   the CI's run and as a cheap check of a merge; the fixture rules of `REACH` derived and held; **the failure reader and the
   signature** (Opus r5 7, as it names it): a reader of trx, vitest and `node --test` reports that keeps the case, the exception
   type and the location (today's readers cut the case, `:2023`, `:2119`), and §4.1 point 4's signature over it, with their
   tool-tests, which recall's search needs here and rows 4 and 5 reuse. It **adds** `check` to the CI's run
   and to a merge's blocking checks, and changes nothing else either runs (round 2 said it changed nothing the CI runs, corrected,
   Opus r2 16). Proof: the inventory matching runner discovery on the same tree; §2.2 replayed with the tool, its figures within a
   tenth of these; **the recall proof** of §3.5 over 2026-09-24 to 2026-10-24, with the selector pinned and run on the historical
   trees, each failure's introducing commit and its verdict, the recall as a lower bound overall and over the FIX-LOG apart, each
   source's span and count, and the first list kept beside the list after repair; its cost measured on the first ten failures
   before the rest, with how many failures reached the walk and the cap; tool-tests of the introduction's search over synthetic
   histories (a fix then a reintroduction, inside a short range and across a long one's probes; **a fix and a reintroduction
   hidden wholly between two probes that both show the signature**, the earlier introduction reached and the later not, which
   must come out *missed* or *unsettled*, never *selected* (Codex r4 1); an open set whose commits all get one verdict, settled
   without a walk; an open set that disagrees, walked to its transition, and one that reaches the cap, unsettled; the probes
   recursing until no stretch exceeds sixteen; a signature that changes partway, *unsettled, signature changed* (Opus r5 10);
   **lines inserted above a failing assertion between its introduction and its first failure, the introduction found and not the
   insertion**, and **a rename, never *selected* at the rename** (Opus r5 1); **a transition within the cap found by the walk
   with no halving run, and one past it settled by step 5** (Opus r5 9); a FIX-LOG entry's named commit whose two runs show the
   transition, judged first seen there; **a reproducible failure whose message carries a fresh GUID each run, found at its
   introducing commit** (Opus r4 1); the reader's case, exception type and location read from a trx, a vitest and a `node --test`
   report; a range with no recorded pass, settled and not), each
   reaching the verdict §3.5 gives it; the historical runs made under §3.5's temporary rule, lowered and behind `memoryGate`, none
   started while the gate tree's lock is held. Its lower bound decides whether CISCOPE1e starts (§3.5, *What it decides*).
4. **CISCOPE1d — the background run's schedule and its safeguards** (§5.1, §5.2, §5.3): `local/ci-sweep.json`, the nightly run
   with its catch-up, the run-kind matrix, a sweep and a night satisfied by a run that finished whatever its verdict, the overdue
   report; `local/ci-findings.json`; known red by test and fingerprint, with the fingerprint's case and location read by row 3's
   failure reader and matched by its signature, clearing, expiry
   and the census check's hold on a skip that names its row; the run kind on every gate verdict and the gate store kept per gate,
   `local/ci-runs.jsonl`'s receipts, `local/stage-history.jsonl`, and `--passed`'s new rules at every stage entry point (the
   receipt of a counted kind, matched by tree; the commit's, the receipt's and the build source's trees one; every ordinary stage
   built from the gate tree under its lock, a plain `--stage` refused but under `_fixtures` and as the child under its parent's
   held lock, the child making the second read before it stages, and `daoris-driver update` naming `--from-gated`; the due record; a standing whole pass's age); `local/unit-verdicts.json` and `local/test-history.jsonl` as attempt-level records; the miss count, structural until
   CISCOPE1e. The sweep stays in `classPlan` until the record replaces it in the same row, and the stage keeps `sweptOut`. Proof:
   tool-tests for the due record (a night missed and caught up; a red whole run satisfying the sweep; a run stopped before its
   classes ran leaving it due; an overdue report), for known red (a second failing test in a known-red class counts; a changed
   signature counts; another theory case and another assertion with the same first line are new fingerprints; **two runs of one
   failure whose messages differ by a GUID, a hex id and an actual value are one failure**, matched by signature; expiry from the
   first sighting, **counted per test, not renewed by a direct commit that moves the assertion**; **a known-red browser spec and a
   known-red rehearsal past 72 hours refused at the stage and said by the watch**; a skip naming a closed row refused; clearing out of order: an older background run finishing late, and a
   bisection's run on a passing ancestor, each clearing nothing, and a sighting after a contradicted clearing keeping the first
   sighting), for the gate store (a working day of scoped verdicts, replayed, leaving a night's and a stage's verdicts in place),
   and for the stage (a direct `--stage` and a delegated `--from-gated` child each refused on scoped-only evidence, with no
   receipt, with only a sweep-only run's receipt, with a ci-sweep record overdue, with an old whole pass standing by reach and an
   older one behind it, with a known-red failure, with rehearsals unpaid; **a dirty checkout whose HEAD has a valid receipt and
   otherwise passing scoped verdicts refused**; **a plain `--stage` refused, naming the gated command, and building nothing from
   the checkout**; **the delegated `--from-gated` child building under its parent's held lock; a plain `--stage` run in the gate
   tree without that lock refused; the deployment rehearsal's `_fixtures` stage unchanged; and `daoris-driver update` naming
   `--from-gated`** (Opus r5 2); **a source change during the build** (Codex r4 2): a tracked file changed in the main checkout between two of
   the build's steps and restored before its end, the artifact built from the gate tree unchanged by it; a promotion of the main
   checkout during the build, the same; and a change made inside the held gate tree and left, refused at the child's second read, before it stages; a CI
   run started during the stage refused by the gate tree's lock; a receipt of a `--ci --stage` run and of a run that ran nothing counted; stage,
   then a prune and another CI run, then a later stage reading its baseline from the history; a first stage and a non-ancestor
   reporting an unknown range). Round 3 listed "an unfiled failure on the base filed at once" here; it needs row 5's base run, and
   moves there (Opus r3 12).
5. **CISCOPE1e — the scoped run, the merge queue and the admission between the runners** (§4, §5.1): `local/scope-tree`, the queue
   and its candidates, `--preview` and `--message`, the records commit on the branch, the detached runner, batching, the base
   re-run of a failing test and of a failing gate that names no test, the queue's stop, every prefix run on the failure's signature
   with its outcomes, the retained candidate judged again, promotion
   recorded by ancestry, the records-only re-form, the verbs, `local/scope-runs.jsonl`, the dispatch skill's merge step in the
   order records commit, preview, queue; the scoped set of §4.2 with the git half's
   budget in its conservative order and the deferred list; `BLOCKING` and `PATH_CHECKS` as its cheap tier; **`local/admission.json`,
   the one admission policy for both runners and the memory reservation** (§5.1), which lands here because this row starts the
   second heavy runner (Opus r2 4, Codex r2 15). GATESLIM1's "listed and skipped" becomes "run if reached, else deferred". The
   background run keeps gating every promoted tip, and the stage's rules are CISCOPE1d's, so nothing that guards the install
   changes. Proof: tool-tests for the queue (two branches in one candidate; a conflict set aside mid-batch and queued again; a red
   candidate of four bisected to one member; a failure two members cause together, pinned with the first; **a fix then a
   reintroduction over the prefixes** (`PASS, FAIL, FAIL, PASS, FAIL`), handing back the last transition's member and naming the
   first as interacting; **a changed signature on a failing base**, pinned on the candidate's signature; **a partial fix of a
   known red**, its changed signature pinned and handed back; **a candidate meeting
   an expired known red**, stopping the queue until a direct commit skips it under its row; **a known-red browser spec and a
   known-red rehearsal**, run by no candidate and stopping nothing; **a known-red git class past the budget**, run first and the
   rest deferred; two independent failures; a
   compilation failure found by prefix builds; **a direct docs commit that breaks a documentation check before a candidate
   forms**, stopping the queue, and **`--queue --recheck` lifting a stop whose host failure does not recur**, with the runner's
   own recheck an hour on; **a member that breaks `code-map`**, found by running the check over the prefixes; **a discovery
   failure and a host failure on one prefix**, each found by running the gate's selection there, every prefix build passing;
   **a reproducible failure whose message carries a fresh GUID each run** (Opus r4 1), pinned to its member, and the same failure
   on a failing base inherited; **a member that adds lines above a known-red assertion**, the failure inherited and nobody
   pinned, and **member 2 breaking a test and member 5 adding lines above it**, member 2 pinned (Opus r5 1); **two projects
   failing to build, their errors printed in either order**, one signature on the base and the prefix (Opus r5 6); **a prefix
   that does not build** in a failing test's sequence (a member breaks compilation and a
   later one repairs it), making the failure unresolved (Opus r4 2); a
   flake mid-bisection, the adjacent prefixes' second run making it unresolved; an unresolved failure whose re-run passes, the
   candidate run one member at a time rather than promoted; **an unresolved failure on a candidate of one**, handed back as
   unresolved with its flake filed and the queue going on to the next candidate (Opus r4 2); **a death of memory on a candidate
   and on its base** (Opus r4 3): neither run on the base nor over the prefixes, the queue not stopped, the gate run again after
   its memory is free and its pass read as FLAKE of memory; a second death holding the candidate as memory until the admission
   record reserves its need, then run again; on that run after the hold, **a death within its need under load from outside the
   runners held, not judged, and one whose job's peak commit passed its need judged as the gate's failure** (Opus r5 3); a
   Process half whose dead tests are named running them alone;
   an inherited failure on the base filed at once and pinned to none
   (moved from row 4); main moved by a direct commit and by records only before promotion, the re-form running the whole cheap
   tier; **a direct commit to `.claude/knowledge/adoption.md` and one to `daoris.lanes.json`**, neither records only, and one to
   `src/Daoris.Cli/test/**`, not records only either; a branch
   and its message file changed while queued, the frozen request merged; a branch whose tip differs from its last `--preview`
   refused at enqueue, and **a records commit made before the preview queued unrefused**, every queue of the proof made in that
   order (Opus r5 5); the runner killed before the fast-forward, between it
   and the record, and after both, resuming without a second merge; the runner's detached start acknowledged, and its absence
   said; a checkout with a tracked change holding promotion until it is clean; a branch queued without a message refused); for
   admission (**the background's git half and a rehearsal already running when a scoped tier starts**, and the scoped tier's
   FLAKE1 re-run beside them; no next rehearsal of the chain starting while the scoped tier waits; both acquiring at once; a
   background gate taking the slot after its 60-minute wait under sustained scoped load, and one held on memory reported overdue;
   **a reservation charged apart in physical memory and commit, its holder's working set falling while its commit stays**; a
   reservation released after a failure; **a holder killed with children surviving, its reservation held until they end**;
   **an intermediate shell that ends while its grandchild runs on with a command line naming no tree**, the job still counting
   it and the reservation held (Codex r4 3); **a process query that fails**, the charge kept and said; **two gates of one run in
   one tree**, each charged only its own job's processes (Opus r4 8); **an admission read that ends nothing**, a dead run's job
   ended only by the run that takes its tree's lock; **a holder that dies and its job gone, the next read releasing the charge;
   a job that refuses to open, its charge kept** (Opus r5 4); a scoped gate's build leaving no build server in its job; a gate
   that cannot join its job not run;
   restart); 2026-10-21's 127 merges and 41 direct commits replayed against the queue at their timestamps, the backlog, each
   merge's wait (p50, p90, the worst) and each candidate's re-forms reported, any held by direct commits named, and **a driver
   candidate with deferred git classes followed by unrelated records and page candidates**, each carrying that debt until the
   background pays it (Codex r4 8); the next real
   merges timed per category against §4.4, p50 and p90, with MEMGATE1's waits apart, **beside a stage's or a night's git half
   with a rehearsal beside it for some of them, and beside the background's heavy light gates for others** (Opus r4 4), the
   harness's dispatches watched throughout and any refusal failing the proof (Opus r4 5), **the scoped run's heavy light gates
   below normal while its Process tier runs, and the scoped Process halves' FLAKE1 re-runs counted** (Opus r5 8); a red candidate leaving main where it
   was; the cheap tier's wall measured.
6. **CISCOPE1f — the rehearsals side by side** (§6.2, REHPAIR1 taken over): the rehearsals' trees and the port claims, with two
   rehearsals allowed to hold admission reservations at once, never three. The serial chain stays the default until the proof
   holds. Proof: collision tests (two runs given the same port start); five timed background runs on working days with two
   rehearsals side by side and the third waiting for a slot (§6.2), every bound holding and the scoped runs' walls beside them
   unchanged; the trees' first install and build priced.
7. **CISCOPE1g — the value measures** (§7.1): the history's report, per-unit coverage in the background for every runner but the
   named exclusions, a value report (`tools/test-value.mjs`), the coverage pass's cost measured on one suite first. It can land
   before CISCOPE1e, its coverage runs then under §3.5's temporary rule (Codex r4 5), moving to the admission record when
   CISCOPE1e lands it. Proof: the report over every suite, its figures reproducible from the history.
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
  **It reverses one item D180 rejected, named** (Opus r3 10): "every reached gate before every merge" (D180.md:49), rejected
  because it made the merge the bottleneck and blocked every dispatch. The scoped run comes close to it, and answers both reasons:
  it runs in a tree of its own, so no dispatch waits on the main checkout (§4.1), and the queue batches merges so the busiest
  hours do not queue behind one run each (§2.7, §4.1); it still defers the browser specs, the rehearsals and the git half past its
  budget, so it is not every reached gate.
  **It amends the budget note's point 2, named** (Opus r4 5): "a CI run is a guest on the machine … at background priority …
  A CI run beside a full slate of workers is not started" (D180.md:68-69). The scoped run is a CI run at normal priority that
  starts beside a full slate of workers, since a merge waits on it; the background run stays the guest the note describes. The
  bound that replaces the guest rule for it is one scoped Process tier at a time and the git half within its eight-minute
  budget, and CISCOPE1e's proof watches for the dispatch refusals the note was written after (§5.1). **Beside it, D180's
  priority split is kept, by choice** (Opus r5 8): the CI1 note's point 5 dropped GATEORDER1's wait on the Process lane at
  normal and the light lane below it (D180.md:428-433), so the scoped run, which drops the wait too, runs its heavy light gates
  below normal while its Process tier runs, rather than keeping the wait (§4.4, §5.1).
  **It makes two deliberate exceptions to the blocking D180 removed, both of the same scope, every merge waiting** (Opus r4 6):
  the expiry of a known red past 72 hours (§5.2), and **the stop when a gate that names no test fails on the base** (§4.1 point
  4), which takes effect at once with no grace, where a failing test is inherited for 72 hours, and is lifted by a direct commit,
  `--queue --recheck`, or the runner's own recheck an hour on.
- **CI1**: `--ci --watch` is the background run with its schedule (§5.1). It still gates each promoted tip, coalescing the
  candidates since its last run.
- **GATESLIM1**: `BLOCKING` and `PATH_CHECKS` fold into the scoped run's cheap tier. `--gated` is the scoped run, and `--full` is
  unchanged.
- **PROCREACH1**: its census becomes CISCOPE1c's for every suite, at the same area grain (§2.3), and its class choice applies in the
  Process halves only. Its sweep moves from the CI's per-run plan to the background schedule's due record (CISCOPE1d), the stage
  keeping `sweptOut`. Its third whole rule is narrowed, not removed (CISCOPE1b). A subject and a budget join reach for the git half.
- **CIFAST1**: unchanged for an ordinary run, which still owes the family and deployment rehearsals, and for the stage, which
  pays them. One mode overrides it: the nightly run runs them (§5.1's matrix), recorded in D180's CIFAST1 note on acceptance.
  **That reverses an item CIFAST1's note rejected, named** (Opus r3 10): "the rehearsals in a nightly or every Nth run"
  (D180.md:573), rejected because a stage would still meet failures found late, and nothing asked for a clock. The night runs
  them **in addition to** the stage's payment, never instead of it, so a stage still runs them on its own commit and meets nothing
  late; and the owner has now asked for a background schedule (§0). *Owed* keeps its meaning; the scoped run's word is *deferred*.
- **The stage** (`stageJudgement`, `--passed`): stricter, never looser. It adds a background run's receipt on the commit's tree,
  of a counted kind, the content built held to that tree by building from the gate tree under its lock (a plain `--stage`
  refused but under `_fixtures` and as the delegated child under its parent's lock, `--force-ungated` apart), the sweep's due record and a standing whole pass's age, at every stage entry point
  (§5.3); it waives nothing. Its residual exposure, a Process-half census miss within the sweep's bound, is today's and stated
  (§4.3).
- **The public release**: unchanged; the release workflow's own jobs, as §5.3 describes them.

Until D207 is accepted, none of these changes anything. On acceptance, each of D115 and D180 gets a dated note pointing here.

## 10. For the reads still owed

Round 6 is the final round: the steward checks its named changes against §11.5's table, without another read. Codex read
rounds 1 to 4; its read of the final text is owed when its workspace's credits return, and a blocking finding of it becomes an
amendment to D207. It is asked round 5's questions, as round 6 answers them:

1. **Recall's conservative measure** (§3.5): is walking the suffix back from the first failure first, within 48 runs, and past
   the cap crediting a verdict only when every commit of the open set (after the newest passing run, up to the verified suffix)
   gets it, with only a pass moving that lower boundary and a changed signature unsettled, a lower bound with no real miss in its
   numerator?
2. **The stage's source** (§5.3): does building every ordinary stage from the gate tree under its lock, a plain `--stage`
   building only under `_fixtures` or as the delegated child under its parent's held lock, the child reading the trees again
   before it stages, keep the receipted source through the whole build, with the limit stated?
3. **Reservation ownership** (§5.1): does a kill-on-close job object per admitted gate, held by its launcher, released on a
   successful read of an empty job or on a name no job answers, survive an intermediate process's death, and keep a charge whose
   membership cannot be read?
4. **The queue's outcomes** (§4.1 point 4): with failures matched by name, case, exception type and a location by source text,
   a build by its set of errors, a candidate of one handed back unresolved, a prefix that cannot run making a failure unresolved,
   and a death of memory the gate's only past its job's own need on the run after the hold, is any failure a candidate can
   produce still placed wrongly or not at all?
5. **The two exceptions** (§9, D207): are the expiry's stop and the base-failure stop, each making every merge wait, with
   `--queue --recheck` and the runner's own recheck for the second, the right exceptions to record?

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
| 16 | Two gaps in what the rows run | Taken: every `tools/*.test.mjs` file run by file (`npm run verify` names twelve of the 16); row 3 says it adds `check` | §4.2 point 3, §8 row 3 |
| 17 | The proof's sources do not span its month | Taken: each source's span and count stated in the output; the recall over the FIX-LOG's failures given apart | §3.5 |

**The Opus read's §2 claims** are its findings 6, 12 and 9, answered above. Its note that a merge waiting on its scoped run is
strictly more than today's, which waits on `BLOCKING` alone, holds and is kept.

### 11.3 Round 3's reads, answered in round 4

Both reads are verbatim in `docs/2026-10-24-ci-scope-reads.md` (*Codex, round 3*; *Opus (foundation), round 3*), both at
`22427ff95`. Every finding was checked against the code on main at `a6991805f`; no file the design cites changed since `22427ff95`
(`git diff --stat 22427ff95 HEAD` over `tools/`, `daoris.gates.json`, `package.json`, `.github/` and D180 is empty), each line
they cite is where they cite it, and each finding held. None is answered by disagreeing with its facts. The Opus read's two
blocking findings are taken as it names them. The round-2 findings each read left open (Codex's §1 *Open* rows 3, 7, 12, 13 and 15
and the Opus row 6; the Opus read's *incomplete* rows F9 and Codex 3, and its F4, F11 and Codex 12 notes) point to round-3
findings and are answered there.

**Codex, round 3 (12 findings, 6 blocking; verdict another round).** Its six blocking findings ask what must run (1), which member
or commit introduced a failure (2, 3), what load the two runners may create (5, 6), and whether the staged content has its
background proof (8); each is resolved in the row named.

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | The records-only re-form bypasses more than the service deferral | Taken: the whole cheap tier always; *records only* means every path's own `REACH` rule names no gate but `service`, read in order (`:1645`), so `adoption.md` (`:1129`) and the declaration files (`:989`) are not; only `service` deferred, recorded; `recordMatcher` (`:1842`) named as a different question; both cases proven | §4.1 point 3, §4.3, §8 row 5 |
| 2 | Discovery and host failures cannot be bisected by builds | Taken: prefix builds for a compilation failure only; a discovery or host failure (`:2041-2044`) run with the gate's own selection and operation on the base, then over the prefixes; unresolved otherwise; separate proofs | §4.1 point 4, §8 row 5 |
| 3 | Halving finds a transition, not the introduction | Taken: the queue runs every prefix (seven runs over eight members) and hands back the last transition's member, earlier ones named as interacting; the recall search runs every commit of a short range, checks a long one between its probes, makes a second ambiguity unsettled, and states the residue; signatures matched throughout; fix-then-reintroduce and changing-fingerprint fixtures in rows c and e | §3.5, §4.1 point 4, §8 rows 3, 5 |
| 4 | The queued request is not immutable | Taken: the tip commit and the message bytes frozen at enqueue and used by every re-form; requeueing changes them (as `state.tips`, `:4452`); proven | §4.1, §8 row 5 |
| 5 | The one-background-gate bound contradicts the scheduler | Taken: the background's simultaneous set stated, the git half and one rehearsal (`:490`, `:628`), two after CISCOPE1f; nothing more of it starts while a scoped tier runs or waits; the proof runs beside that maximum | §5.1, §6.2, §8 row 5 |
| 6 | Reservations use the wrong unit and outlive their holder | Taken: physical memory and commit accounted apart (`readMemory`, `:2284`); a reservation held by the process group found as CIPRIO1d/e find it (`:4628`), released only when none of it lives; unequal working-set and commit, and a killed holder with surviving children, proven | §5.1, §8 row 5 |
| 7 | "No wait" and nightly progress overstate admission | Taken: the Process slot's wait (none) and MEMGATE1's memory wait (`:2212`, `:2365`) stated apart, the latter in the latency the proof measures; progress stated as conditional on memory, a gate held on memory reported overdue | §5.1, §8 row 5 |
| 8 | The receipt is not bound to the tree staged | Taken: the commit's tree, the receipt's tree and the build source's content tree must be one (`contentTree`, `:1819`; `:4326`; dp:1163); a dirty checkout refused; the source read again before staging; the dirty-checkout case proven | §5.3 point 1, §8 row 4 |
| 9 | Clearing on any run loses causal order | Taken: cleared only by a pass on a first-parent descendant of the commit last seen failing, from a run that counts or a promoted candidate's; bisection and proof runs never clear; the first sighting kept; out-of-order completion proven | §5.2, §8 row 4 |
| 10 | Today's watch does not detach itself | Taken: the analogy removed (`:5645`, `:5723`); the runner's detached launch, its log and its startup acknowledgement specified | §4.1 |
| 11 | The release workflow does not gate one exact tree | Taken: described as it is, `devkit` and `desktop` on the input revision, `release` on a stamped, re-synced worktree (`release.yml:213`, `:222`) before its commit; kept unchanged | §5.1, §5.3 |
| 12 | Row f's proof needs concurrency its mechanism forbids | Taken: two at once, the third waiting; the proof, the accounting and the expected wall (23 to 30 minutes) made one | §6.2, §8 row 6 |

**Opus foundation read, round 3 (12 findings, 2 blocking; verdict accept after named changes).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | A failing gate that names no test has no rule on the base | Taken as named: that gate run on the base first with the same selection; the queue stops if it fails there, as for a base that does not build, and the failure is filed; otherwise that gate's selection is run over the prefixes; the three proofs named (a direct docs commit, a member breaking `code-map`, a host failure on one prefix) | §4.1 point 4, §8 row 5 |
| 2 | A failure the base shares but does not inherit; the expiry's scope | Taken as named: (a) prefixes judged by the candidate's failure signature, so a changed fingerprint on a failing base pins a member; (b) the queue stops on an expired known red until a direct commit fixes or skips it, said by `--queue`, the alternative rejected with its reason; (c) a known-red unit runs in every candidate whatever it reaches, in §4.2, §4.4, §5.2 and D207 alike; (d) both proofs added; D207's exception now says every merge waits | §4.1 point 4, §4.2, §4.4, §5.2, §8 row 5, D207 |
| 3 | §5.1 misstates the scoped run's worst case | Taken: the worst case the git half plus a rehearsal; MEMGATE1's wait per heavy gate stated; the headroom sized to the scoped peak, 26 GB; the scoped runner at normal priority; the proof timed beside a stage's or a night's git half with a rehearsal | §5.1, §8 row 5 |
| 4 | The unresolved path can retry; a flake can hand back an innocent | Taken: a pass on any re-run never makes a candidate green, the members run one at a time and the failure is filed as a flake; the adjacent prefixes run once more before a hand-back | §4.1 point 4 |
| 5 | Promotion moves the parent's checkout; direct commits can starve | Taken: promotion said to move it, and made only onto a clean checkout, else held and retried; starvation not ruled out but reported after three re-forms, and the 2026-10-21 replay reports re-forms | §4.1 point 3, §8 row 5 |
| 6 | The receipt does not say which runs count | Taken: ordinary, nightly, plain `--ci`, `--ci --stage` and `--full --beside` count, a sweep-only run does not; each producer's verdict kind named, `release` dropped; matched by tree; a run that ran nothing counts; `ci-runs.jsonl` extends `ci.json` (`:5069`, `:5351`) | §4.2, §5.1, §5.3 point 1 |
| 7 | The gate store's 400 cap takes every candidate's verdicts | Taken: kept per gate, the newest 20 of scoped and remedy runs apart from the newest 20 of the other kinds (`:1758`); the working-day replay in row 4's proof | §4.2, §8 row 4 |
| 8 | §4.4 under-prices `service` and omits it | Taken: priced with the HTTP host's 3 m 17 s (`daoris.gates.json:8`); added to every row, every member carrying a records commit (`:1234`, `:1249`); repriced; D207's figures follow | §4.4, D207 |
| 9 | "Every `tools/*.test.mjs`" adds four files no gate runs | Taken: the twelve verify names (`package.json:15`); the four named and kept out | §4.2 point 3 |
| 10 | Two reversed D180 rejections unnamed | Taken: both named with their answers, in §9 and in D207's Changes | §9, D207 |
| 11 | Four details of the recall measure | Taken: the range's start without a recorded pass, *no pass found* unsettled; the proof priced, measured on ten first; every run a re-formed candidate's verdicts rest on read; the lower bound decides whether CISCOPE1e starts, below 0.9 going to the steward | §3.5, §8 row 3 |
| 12 | Small slips in the records | Taken: `TASKS.md`'s CISCOPE1 row (a republish waits for the background run) and TESTVALUE1 row (CISCOPE1g measures, the target set from its report) corrected; D207's figures follow §4.4; §3.4 lists the skip check; the base-run proof moved to row 5 | `TASKS.md`, D207, §3.4, §8 rows 4-5 |

**The Opus read's §3 claims** are its findings, answered above; its note that the foundation holds is kept. Its answers to the
§10 questions of round 3 are taken where they named a change (question 4's receipt kinds, question 2's records commit reaching
`service`).

### 11.4 Round 4's reads, answered in round 5

Both reads are verbatim in `docs/2026-10-24-ci-scope-reads.md` (*Codex, round 4*; *Opus (foundation), round 4*), both at
`e89f00ca4`. Every finding was checked against the code on main at `cdf6d5f55`: `git diff --stat a6991805f HEAD` over `tools/`,
`daoris.gates.json`, `daoris.lanes.json`, `package.json`, `.github/`, D115, D180 and `src/Daoris.Cli/tool-test` is empty, and
`docs/development.md` changed only in its landing paragraph, which the design does not cite. Each line the reads cite is where they
cite it (`againOfMemory` `:3744`, `memoryDeath` `:2244`, LEFT1 `:3540-3557`, `endOrphans` `tools/processes.mjs:169`,
`endStranded` `:189`, `gatedSource` `desktop-publish.mjs:947`, `ruleFor` `:1574`, `recordMatcher` `:1842`), the month's
first-parent count at `a6991805f` is 2,153, and each finding held. None is answered by disagreeing with its facts. The Opus read's
three blocking findings are taken as it names them. The round-3 findings each read left open (Codex's §1 *Open* rows 3, 6, 8, Opus
2 and Opus 11; the Opus read's qualified rows F2, F3, F4, F8, F11 and Codex 3, 5 and 6) point to round-4 findings and are answered
there. The one existing mechanism the answers newly rest on is HEAVYRUN1a's job object (`Agents/Consoles/ProcessJob.cs`), which holds a
session's heavy gates today.

**Codex, round 4 (8 findings, 3 blocking; verdict another round).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | The historical search cannot establish its lower bound | Taken, the second of its named changes: a verdict is credited only when every commit of the open set (after the newest commit run without the signature, up to the verified suffix before the first failure) gets it, the structural classification costing no build; otherwise the suffix is walked back to the exact transition, within 48 runs, and past that the failure is unsettled and counted as missed; the fix and reintroduction hidden between probes is a row-3 fixture; repriced over 2,153 commits | §3.5, §8 row 3, D207 point 2 |
| 2 | Two matching snapshots do not bind the artifact to its source | Taken as named: every ordinary stage builds from the gate tree at the receipted commit under its lock, as `--from-gated` does (`gatedSource`, dp:947-994); a plain `--stage` refuses and names the gated command; the trees read under the lock before and after; source-change-during-build, promotion-during-build and change-inside-the-tree cases in row 4; the limit (a writer outside the tools inside the locked tree, restoring before the read) stated | §5.3 point 1, §9, §8 row 4, D207 point 5 |
| 3 | Orphan heuristics cannot implement process-group ownership | Taken as named: each admitted gate in a job object of its own, joined suspended with no breakaway, as HEAVYRUN1a holds a session's heavy gates; membership and committed memory read from the job; released only on a successful read of an empty job, unknown membership keeping the charge; `endOrphans`/`endStranded` kept only as the lock-taker's cleanup; private build servers (`BESIDE_ENVIRONMENT`, `:4879`) for every scoped gate; the vanished shell, the tree-less grandchild and the failed query proven | §5.1, §8 row 5, D207 point 4 |
| 4 | "Every known-red unit" contradicts browser and rehearsal deferral | Taken: eligibility before known-red selection; browser specs and rehearsals stay deferred and never stop the queue, refused at the stage and said by the watch past 72 hours; a known-red git class runs first and consumes the budget; the expiry applies to the eligible set; one fixture per deferred kind | §4.1 point 4, §4.2, §5.2, §8 rows 4-5, D207 |
| 5 | The historical proof consumes admission before it exists | Taken, the first of its named changes: before CISCOPE1e, historical runs and CISCOPE1g's coverage run as a guest does today (`lowerPriority` `:4865`, `BESIDE_ENVIRONMENT`, `memoryGate` `:2331`), one at a time, none while the gate tree's lock is held; they move to the admission record when CISCOPE1e lands it | §3.5, §8 rows 3, 7 |
| 6 | `recordMatcher` described as merge resolution | Taken as named: it decides which record differences a standing verdict may forgive; resolution is `resolveBacklog`/`mergeByBase` (`:1429`, `:1392`) | §4.1 point 3 |
| 7 | Receipt provenance does not remove dependency-map exposure at install | Taken, its first option: the contract kept and its residue stated (a Process-half census miss may reach the install within the sweep's bound, as D180's PROCREACH1 note accepts); a whole Process half before every stage not required, with the reason (24 to 44 minutes a republish) | §4.3, §9, D207 *Why* |
| 8 | Carried git-half debt understated for unrelated candidates | Taken as named: carry-over by standing evidence, whatever a later candidate's paths reach; the replay adds a deferred driver candidate followed by unrelated records and page candidates | §4.1, §4.4, §8 row 5 |

**Opus foundation read, round 4 (10 findings, 3 blocking; verdict accept after named changes).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | A failure whose message varies is never matched | Taken as named: within one judgement a failing test is matched by its full name with its case, its exception type and its assertion's location, the message line only where no location can be read; GUIDs and hex runs of eight or more normalised in a message and in a gate's error line (a compiler's error code and file for a build); the full fingerprint the record's identity; the GUID case in rows 3, 4 and 5 | §4.1 point 4, §3.5, §5.2, §8 rows 3-5, D207 |
| 2 | An unresolved failure on a candidate of one has no outcome; a prefix that cannot run has no reading | Taken as named, its first option: a candidate of one with an unresolved failure handed back as *unresolved*, its flake filed, the queue going on, never formed again by the runner (a requeue is the parent's, recorded); a prefix that cannot run the selection makes the failure unresolved; both in row 5 | §4.1 point 4, §8 row 5, D207 point 3 |
| 3 | A death of memory meets the base-first stop; today's retries misstated | Taken as named: a failure `memoryDeath` reads (`:2244`) never run on the base or over the prefixes, never stopping the queue or handing a member back; MEMGATE1's run again kept; a second death holding the candidate as memory until the admission record reserves its need, then run again; a death with its need reserved judged as the gate's; FLAKE1, MEMGATE1 and LEFT1 named as today's retries in §1, §4.1 and §4.2; row 5's case | §1, §4.1 point 4, §4.2, §8 row 5, D207 |
| 4 | The two runners' load understated in two places | Taken as named: the scoped run scheduled by memory with GATEORDER1's wait dropped; the background's light lane in its set (26 GB, below normal priority, admitted above the headroom); row 5 timed beside background heavy light gates too | §4.4, §5.1, §8 row 5 |
| 5 | Normal priority departs from D180's budget note, unnamed | Taken as named: an amendment to point 2 (D180.md:68-69), its bound one scoped Process tier at a time and the git half within eight minutes, CISCOPE1e's proof watching dispatch refusals | §5.1, §9, §8 row 5, D207 Changes |
| 6 | The base-failure stop is an unrecorded exception | Taken as named: recorded as a deliberate exception beside the expiry in §4.1, §9 and D207, its scope and asymmetry (at once, against 72 hours) said; `--queue --recheck`, and the runner's own recheck an hour on | §4.1 point 4, §9, §8 row 5, D207 |
| 7 | "Every candidate" broader than §4.2 allows; a partial fix handed back | Taken as named: the claim limited to units the scoped run may run in §4.2, §5.2 and D207 (points 3 and *Rejected*); a partial fix's changed signature handed back, the parent's way through a full fix or the skip | §4.1 point 4, §4.2, §5.2, D207 |
| 8 | A reservation's group found per run; an admission read ends processes | Taken, by Codex 3's mechanism (round 5 said Codex 4's, Opus r5 10), which subsumes its named change: a job per gate is that gate's descendants whatever their parents, so no sibling's strays are counted and strays need no run-wide charge; ending processes is the lock-taker's only, never an admission read's | §5.1, §8 row 5 |
| 9 | Stretch probes unbounded; price assumes short stretches | Taken as named: the probes recurse until no stretch exceeds sixteen; each failure priced at about eleven halving runs plus d/16 probes, plus the walk's 48 at most; the count corrected to 2,153 | §3.5 |
| 10 | Small slips | Taken: *records only* defined by its three rules (`:1234`, `:1244`, `:1249`); D207's wording follows; `ruleFor` at `:1574`; §6.2's figure about 15 to 26 (the read's "about 16 to 26" by the same figures; its low end is the deployment rehearsal's 14 after the release's 1); the frozen tip refused when it differs from the last `--preview`; the expiry counted per test | §4.1, §5.2, §6.2, D207 |

**The Opus read's §2 wrong claims** are its findings 3, 4, 9 and 10, answered above. Its answers to round 4's §10 questions are
taken where they named a change (question 1: findings 1 to 3; question 2: finding 6; question 5: findings 1 and 9), and its note
that the foundation holds is kept. **Codex's §2** (the absolute claim that dependency misses cannot reach the install; CISCOPE1c's
price referring to later admission; `recordMatcher`) is its findings 7, 5 and 6.

### 11.5 Round 5's read, answered in round 6

The one read is verbatim in `docs/2026-10-24-ci-scope-reads.md` (*Opus (foundation), round 5*), at `9a48b7f3d`; Codex's
workspace was out of credits, so round 5 has no Codex read. Every finding was checked against the code on main at `0f086f0e4`:
`git diff --stat cdf6d5f55 HEAD` over `tools/`, `daoris.gates.json`, `daoris.lanes.json`, `package.json`, `.github/`, D115, D180,
`src/Daoris.Cli/tool-test`, `ProcessJob.cs` and `UpdateCommand.cs` is empty, each line the read cites is where it cites it
(`childArgs` dp:997-1008, `ungatedRefusal`'s exemption dp:872, `promoteStage` dp:1410, the rehearsal's step 9b
`deployment-rehearsal.mjs:455-460` and `:1659-1661`, `UpdateCommand.cs:87-88`, `KillOnJobClose` PJ:95 and `:285-292`,
`EndNamedAsync` PJ:256-268, `PeakJobMemoryUsed` PJ:354, MEMGATE1's named dead tests mb:275 and `:3569-3574`, the 0b44c7a harm
mb:510-511, D180.md:428-433), and each finding held. None is answered by disagreeing with its facts. Its verdict, *accept after
named changes the steward can check without another read*, is taken: each change below is the read's own, a sentence with its
proof case, and nothing else of substance moved. Its §1 rows (round 4's findings) and §2 (claims, holes, rows, D207, the §10
answers) point to its findings 1 to 10, answered here.

**Opus foundation read, round 5 (10 findings, 3 blocking; verdict accept after named changes).**

| # | Finding | Verdict | Where |
|---|---|---|---|
| 1 | The signature's location is a line number; across commits a line shift reads as another failure, in the queue and in recall | Taken as named: a failing test's location is its frame's method and the text of its source line, read from the run's tree, normalised, with its ordinal among identical lines of that method, never its line number; the fingerprint keeps file and line as the record's identity; in recall's search only a passing run moves q, and a run where the test fails with another signature or does not exist moves nothing, the walk ending there *unsettled, signature changed*; row 5's two fixtures (lines added above a known-red assertion, inherited; member 2 and member 5, member 2 pinned) and row 3's two (lines inserted before the first failure, the introduction found; a rename, never *selected*) | §4.1 point 4, §3.5, §5.2, §8 rows 3, 5, D207 points 2-3 |
| 2 | The plain-`--stage` refusal does not say how the delegated child builds, and the child is itself a plain `--stage` | Taken as named: a plain `--stage` builds only under `_fixtures` (GATE3, dp:872) or inside the gate tree while its lock is held with `holder: 'stage'` by its parent process, the lock's commit the tree's HEAD; otherwise it refuses and names the gated command, as `daoris-driver update` then does; the child makes the second three-tree read just before it promotes the stage (dp:1410); row 4's four cases | §5.3 point 1, §9, §8 row 4, D207 point 5 |
| 3 | A death of memory on a reserved run is judged as the gate's, which is every normally admitted run, and reservation does not cover the load that kills | Taken as named: only on the run after the hold, and only where the gate's own job's peak committed memory exceeded its reserved need (`PeakJobMemoryUsed`, PJ:354); a death within its need stays the machine's and holds the candidate again, said; row 5's two cases | §4.1 point 4, §8 row 5, D207 point 3 |
| 4 | Release cannot tell an ended job from an unreadable one | Taken, its first option: the job kills on close, as HEAVYRUN1a's do; its launcher holds the handle for the gate's life; a read opens for query only and closes at once; a name no job answers is an ended job and its charge released, any other failure to open or read keeping it; row 5's two cases | §5.1, §8 row 5, D207 point 4 |
| 5 | The parent's records commit moves the previewed tip | Taken as named: records commit, then `--preview`, then the queue, in §4.1 and in the dispatch skill's merge step, which row 5 changes; row 5's proof queues in that order | §4.1, §8 row 5 |
| 6 | A build's signature depends on output order | Taken as named: the set of distinct error codes with their files, matched as sets; row 5's case | §4.1 point 4, §8 row 5 |
| 7 | Row 3 uses the signature before any row builds it | Taken as named: row 3 builds the failure reader (trx, vitest, `node --test`, keeping the case, the exception type and the location) and the signature, with their tool-tests; rows 4 and 5 reuse them | §8 rows 3-4, §5.2 |
| 8 | The scoped run drops GATEORDER1's wait without the priority split that let the CI drop it | Taken, its first option: the scoped run's heavy light gates below normal while a scoped Process tier runs, as the CI's are; the choice named beside the budget-note amendment; row 5 counts the scoped Process halves' FLAKE1 re-runs | §4.4, §5.1, §9, §8 row 5, D207 point 4 and Changes |
| 9 | Recall's search pays for halving and the probes before the step that settles it, and its cap meets the observation lag | Taken as named: the walk first, halving, the probes and the verdict's test only for a failure the walk caps; a FIX-LOG entry's named commit whose two runs show the transition is the failure's f; each source's observation lag beside its count; "most settle at step 3" withdrawn and the price restated | §3.5, §8 row 3, D207 point 2 |
| 10 | Slips | Taken: MEMGATE1's run again of a Process half with named dead tests runs those tests alone (§1, §4.1, §4.2); §11.4's Opus row 8 reads Codex 3's mechanism; row 3's fixture is a signature that changes partway, *unsettled, signature changed*; D207 point 3 carries finding 3's rule | §1, §4.1 point 4, §4.2, §11.4, §8 row 3, D207 |

### 11.6 Which reads round 5 asked, and round 6's close

Round 5 takes the Opus read's three named changes, which it said the steward can check without another read, with its seven
statements and corrections, and makes concrete the three things Codex's read asked another round for: a conservative historical
recall measure (§3.5), publication keeping its receipted source through the build (§5.3), and reservation ownership surviving an
intermediate process's death (§5.1). Codex reads round 5. Whether the Opus foundation reader reads it again, or the steward checks
its named changes against the text, is the steward's call under D191. D207 stays proposed until the steward accepts.

Round 5 was read by the Opus foundation reader alone, since Codex's workspace was out of credits. Round 6 is the final round: it
makes that read's named changes and no others, and the steward checks them against §11.5 without another read. Codex read rounds
1 to 4; its read of the final text is owed when its credits return (§10), and a blocking finding of it becomes an amendment to
D207. D207 stays proposed until the steward checks round 6.
