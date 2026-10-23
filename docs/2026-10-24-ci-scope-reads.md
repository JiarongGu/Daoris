# CISCOPE1: the reads of rounds 1 to 4 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-24-ci-scope-design.md` and D207 at
`8ec788f89`, each verbatim but for its headings set one level down. The steward ran both from outside the install, read-only, on
the design's own tree, read as landing after CIFAST1. Both ask for another round: Codex with 17 findings, 14 blocking; Opus with
21, 8 blocking. Round 2 answers each finding in the design's reads table. Round 2 was read the same way at `aceced648`: Codex
asks another round (15 findings, 11 blocking); the Opus foundation reader finds the foundation holds and would accept after named
changes the steward can check (17 findings, 6 blocking). Round 3 answers both. Round 3 was read at `22427ff95`: Codex asks
another round (12 findings, 6 blocking); the Opus foundation reader's check finds every round-2 blocking change taken as named
and would accept after named changes (12 findings, 2 blocking). Round 4 answers both. Round 4 was read at `e89f00ca4`: Codex
asks another round (8 findings, 3 blocking); the Opus foundation reader's check would accept after named changes (10 findings,
3 blocking, each a local rule of the queue's base run). Round 5 answers both.

## Codex

Codex's read of round 1.

### 1. Claims about the code

The description of `REACH`, the Process lanes, class verdicts and `--from-gated` is substantially accurate for this tree. I read CIFAST1 as the preceding change: plain `--ci` leaves the family and deployment rehearsals owed; `--ci --stage` pays them before staging. CIFAST1 preserves D115’s requirement that the full set stand before an install stage. CISCOPE1 deliberately removes that requirement for most local stages.

The incorrect claims concern the attributed 74-minute saving, CLI changes supposedly reaching no Process classes, FLAKE1 supposedly retrying every unit, ports and memory already being isolated, and D139 providing a retained rollback build. These are findings 1–3, 9 and 16 below. The proposed claim that the git half eventually becomes exact also fails both arithmetically and mechanically: finding 13.

### 2. The mechanism’s holes

A missed dependency can produce this sequence:

1. A change breaks a test whose dependency the map misses.
2. The scoped run skips that test or keeps its earlier pass.
3. Main advances, and `--from-scoped` permits staging.
4. The install can apply the build automatically when idle.
5. A later whole run discovers the regression.

The census’s listed checks do not prevent that sequence: agreement about **which tests exist** is not proof of **which files affect them**. The whole background run is the proposed backstop, provided the regression remains present and the relevant test detects it. “Nightly” currently specifies neither a reliable completion deadline nor recovery after an interrupted or missed run. Discovery can therefore follow installation, and D139 cannot generally undo the installed build then.

Public release protection should remain stronger. The design explicitly requires a whole green run on the exact release commit, without owed or known-red units. That is a sound distinction, but its enforcement and proof must arrive before scoped receipts become usable for staging or publication.

Known red is independently unsafe at class/file granularity: an inherited failure can hide an additional regression in the same unit. Test pruning likewise needs more than coverage equality: two tests can execute identical branches while checking different obligations.

### 3. The rows

| Row | Can it land alone, in this order? | Does its proof establish its claim? |
|---|---|---|
| CIFAST1 | Yes; already built on the supplied branch. | Its plan and stage tests establish the deferral. Its timing is a scaled stand-in experiment, not a measured product saving. |
| CISCOPE1b | Only its safe composition part. | The replay proves the saving, not preservation of gate-level failures. Removing the sweep depends on the scheduler in **e**. |
| CISCOPE1c | Yes, initially as reporting and checks. | Unit counts and historical cost estimates do not prove dependency completeness. The last-whole-report comparison also needs a bootstrap rule. |
| CISCOPE1d | No, as specified. | It depends on **e** for staging safeguards and on **f** for value ordering. Ten merges and one failure do not prove candidate recovery, concurrent landing safety or scope soundness. |
| CISCOPE1e | Its scheduler and safeguards should precede their consumers. | One timed concurrent run cannot establish isolation or non-starvation. Stage refusal tests must include public/full-proof separation. |
| CISCOPE1f | The recorder/report can land alone. | Coverage collection is unspecified for some suites; reproducibility does not establish mature failure history or valid catch attribution. |
| TESTVALUE1 | After usable measurements and sufficient history. | Before/after coverage is necessary, but cannot alone justify removal or replacement of real-process cases. |

CIFAST1 was a plausible first reduction in recurring CI cost, but it moves the rehearsal cost to staging. After CIFAST1, the smallest immediate win is the narrowly guarded all-classes-kept composition fix—not removing the sweep or building the whole new census.

### 4. Findings

1. **Non-blocking — the claimed 74-minute rehearsal saving does not follow from the table.**  
   `docs/2026-10-24-ci-scope-design.md:59`, `:62` and `:367` attribute 74.3 minutes entirely to rehearsals whose listed durations total **44.1 minutes**. Another approximately 30 minutes is unexplained.  
   **Smallest change:** account separately for preparation, scheduling and memory waits; state the removable critical-path time. Correct D207’s corresponding claim at `docs/decisions/D207.md:11`.

2. **Non-blocking — the latency estimate excludes changes that do reach Process classes.**  
   `docs/2026-10-24-ci-scope-design.md:243` says page, CLI and tools changes reach no Process class. `tools/merge-branch.mjs:1146` sends CLI lock helpers to `driver-process`; `:1154` does the same for the dispatcher and driver-program seam. The every-gate rule at `:960` covers several tooling/configuration paths too. The driver estimate also combines means and a budget without establishing an upper bound.  
   **Smallest change:** qualify the estimates by change category and report distributions, including whole-suite fallbacks, preparation and waits.

3. **Non-blocking — “each unit” being retried is not FLAKE1’s current policy.**  
   `docs/2026-10-24-ci-scope-design.md:226` says every failing unit reruns alone “as FLAKE1 does today.” `tools/merge-branch.mjs:3527` explicitly treats non-Process .NET failures as real and never retries them. Rehearsals reporting failed checks are also not retried (`:2154`). D180 explicitly rejected general Node-suite retries (`docs/decisions/D180.md:225`).  
   **Smallest change:** preserve the existing retry classes, or explicitly propose and justify the extension.

4. **Blocking — the map is not complete, and its census cannot certify completeness.**  
   `docs/2026-10-24-ci-scope-design.md:160`, `:179` and `:186` promote lexical reach into an “exact” selection. Today’s census follows named types, not production call closure (`tools/process-reach.mjs:695`), and can return no reader for fixtures (`:756`). Existing dependencies include:
   
   - directory scans: `SystemWiringTests.cs:574`;
   - reflection over an assembly: `PipeCensusTests.cs:272`;
   - source reads by basename: `src/Daoris.Web/src/locales/note.test.ts:40`;
   - catalogue globs: `src/Daoris.Web/src/locales/index.ts:26`;
   - another project’s usage-bearing source: `DriverCommandTests.cs:103`.

   Listing every unit and placing every tracked file does not establish those edges, including newly added members of a scanned set.  
   **Smallest change:** define conservative folder/assembly/glob and generator-input edges, explicit indirect-reach declarations, and a wider fallback for unresolved reads. Require independent expected-edge tests for reflection, fixtures/twins, generated outputs, source-text reads, censuses, catalogues and cross-project words.

5. **Blocking — comparing against the last whole run rejects legitimate test additions.**  
   `docs/2026-10-24-ci-scope-design.md:186` requires equality with the last whole run’s unit list. A candidate adding or deleting a class/file necessarily differs from that report; running the check before every merge creates either a false failure or an undocumented exemption.  
   **Smallest change:** bind reports to their source tree. Compare candidate membership with current runner discovery, and compare execution reports with the census for the tree actually executed. Missing or incompatible evidence must have an explicit conservative fallback.

6. **Blocking — reached browser checks and build obligations have no scoped disposition.**  
   The units include Playwright specs (`docs/2026-10-24-ci-scope-design.md:157`), but the executable selection at `:212` covers fast suites, Process classes and no rehearsals. It never assigns reached `web` or `web-measured` units, or their production-build prerequisites. Both are declared gates (`daoris.gates.json:19`, `:21`); `gateKind` also calls `npm run test:*` a rehearsal (`tools/merge-branch.mjs:812`).  
   **Smallest change:** enumerate every gate’s scoped treatment and prerequisites. If browser checks are deferred, record them as owed and state that exactness also gives way there.

7. **Blocking — nightly execution does not establish the promised detection bound.**  
   `docs/2026-10-24-ci-scope-design.md:260` promises a missed change is at most a day old. Even a punctual nightly start adds the run’s duration; sleep, shutdown, a held tree, failure or continuous scoped work can extend it. Today’s watch suppresses repeat runs after failed/error verdicts (`tools/merge-branch.mjs:5041`, `:5320`).  
   **Smallest change:** define durable due/completed sweep records, restart catch-up, retry rules, idle-window duration, nightly precedence and overdue reporting. State the bound as conditional on successful whole-run completion, including its runtime.

8. **Blocking — known red can mask a new regression.**  
   `docs/2026-10-24-ci-scope-design.md:270` exempts a failing **unit**, although a unit can contain many tests. A candidate can introduce a second failing test, or worsen the original failure, while retaining the exemption. Filing a row does not prove “the merge did not make it.”  
   **Smallest change:** waive only confirmed inherited test failures with stable identities and failure fingerprints. New failures, changed signatures and build/discovery failures must still fail the scoped run. Define clearing and expiry.

9. **Blocking — the staging justification overstates rollback.**  
   `docs/2026-10-24-ci-scope-design.md:276` says D139 keeps the running version and its rollback. `InstallSwap.Finish` deletes the previous build (`src/Daoris.Desktop/Daoris.Desktop.Driver/Setup/InstallSwap.cs:374`); subsequent cleanup retries that deletion (`Setup/StagedBuild.cs:494`). D139 expressly excludes failures after confirmation (`docs/decisions/D139.md:74`).  
   **Smallest change:** describe scoped staging as accepting exposure to regressions found later, including after automatic installation. Remove the retained-rollback justification; any promised later rollback requires a separate mechanism and proof.

10. **Blocking — the protected update path is incomplete and has no comparison baseline.**  
    `docs/2026-10-24-ci-scope-design.md:283` omits the modules’ `Update/InstallUpdater.cs` and `Update/UpdateLauncher.cs`, and application wiring at `src/Daoris.Desktop/Daoris.Desktop.App/Program.cs:174`. The launcher also compiles supporting driver files by link (`Daoris.Desktop.Launcher.csproj:27`, `:30`, `:35`). A filename list does not capture their dependencies. Nor does “the commit reaches” say whether earlier, unstaged changes count.  
    **Smallest change:** derive a conservative update-path dependency set and judge changes since the last deployment proof that stands for the target tree. Test an update-path change followed by an unrelated commit and then a stage.

11. **Blocking — composing class passes can erase a gate-level failure.**  
    `docs/2026-10-24-ci-scope-design.md:369` removes the all-kept whole-run fallback unconditionally. Its current contract explicitly includes a half that “failed outside a test” (`tools/merge-branch.mjs:5097`). Class passes can survive a later build, discovery or host failure without any class verdict recording that failure (`:2012`, `:4034`).  
    **Smallest change:** compose only when gate-level execution evidence is valid and the debt consists solely of invalidated aggregate reach. Retain rerunning for outside-test failures, missing results and discovery discrepancies. Add those refusal cases to the proof.

12. **Blocking — earlier rows consume safeguards and measurements built later.**  
    `docs/2026-10-24-ci-scope-design.md:370` removes the sweep before **e** builds its replacement. **d** exposes `--from-scoped` before **e** builds the never-list (`:377`, `:380`), and orders by value before **f** supplies it. Thus “each lands alone” is false.  
    **Smallest change:** keep the sweep until its scheduler exists; ship stage/publication refusals with the first scoped-stage capability; specify a conservative ordering when value data is absent. Keep scoped, composed and whole proofs distinguishable through both `--from-scoped` and legacy publication paths.

13. **Blocking — the git-half target neither fits the budget nor makes selection exact.**  
    `docs/2026-10-24-ci-scope-design.md:355` claims under 40 class-minutes fits eight wall-minutes at four wide. That capacity is **32 class-minutes**, before overhead and scheduling imbalance. Moreover, `:220` excludes reached classes whose subjects did not change, including subjectless classes; additional capacity never selects them.  
    **Smallest change:** when the complete reached set fits, select it regardless of subject. Set the target using measured wall time, longest-class duration and overhead, and define handling of unknown durations and overruns.

14. **Blocking — the value measurements omit essential coverage and failure evidence.**  
    `docs/2026-10-24-ci-scope-design.md:331` specifies one verdict per unit/run, but pruning needs the initial failures, individual retries, failure identities and confirmed fixes. Existing class verdicts lose that distinction when some tests pass alone but the gate still fails (`tools/merge-branch.mjs:2101`, `:3551`). The coverage recipe at design `:335` covers .NET and vitest, but not the Node suites despite **f** promising every suite. A later non-test change alone also does not establish a catch.  
    **Smallest change:** specify attempt-level history, confirmed catch/flake attribution, coverage collection or explicit exclusions for every runner, and coverage invalidation by relevant source/test/fixture changes. Do not prune before the history window matures.

15. **Blocking — equal coverage does not prove a test adds nothing.**  
    `docs/2026-10-24-ci-scope-design.md:343` authorizes removal from coverage and catch counts. Two tests can cover the same lines and branches while checking different outputs, ordering, persistence or refusal rules. Replacing process cases with stand-ins has the same problem. The last-cover invariant at `:350` would still pass after losing those assertions.  
    **Smallest change:** use the report to nominate candidates. Require a recorded explanation of which retained assertions/cases preserve each obligation, alongside the before/after line-and-branch guard required by `TASKS.md:493`. Preserve data rows when folding tests.

16. **Blocking — rehearsal concurrency assumes resource isolation the code does not provide.**  
    `docs/2026-10-24-ci-scope-design.md:310` overstates `takePorts`: uniqueness is within one call; ports are checked, not reserved (`tools/rehearsal-kit.mjs:258`, `:266`). Concurrent runs can select the same still-free port. At design `:317`, memory is also shared rather than reserved: each gate samples independently, and after ten minutes `memoryGate` runs anyway (`tools/merge-branch.mjs:2334`). Separate worktrees correctly address the CLI-pack collision, and separate homes address home state; neither solves these races or contention with a later scoped run.  
    **Smallest change:** coordinate port allocation and aggregate memory admission across the background and scope runners. Retain serial rehearsal fallback until collision cases and repeated loaded runs establish safe concurrency and acceptable foreground impact.

17. **Blocking — the candidate lifecycle assumes exclusive landing that this repository does not have.**  
    `docs/2026-10-24-ci-scope-design.md:204` assumes main cannot move because the parent merges serially. The documented install workflow lands through `trees land` (`docs/development.md:198`), whose gate does not run these repository checks (`Trees/SessionTrees/TreeGates.cs:43`). It can bypass scoped verification and move main during a candidate run. Current continuation also identifies an open merge through `MERGE_HEAD`, then otherwise treats it as landed or abandoned (`tools/merge-branch.mjs:4464`, `:4474`). Aborting the merge changes that protocol.  
    **Smallest change:** define how every supported landing path participates, pin the candidate and base durably, recheck the base and checkout before promotion, and recreate/reverify on movement. Specify resume, cancellation, conflict handling and refresh-after-promotion; prove concurrent landing and restart cases.

**17 findings; 14 blocking.**

**Verdict: another round.** The scoped/background direction is workable, and CIFAST1 provides a useful preceding improvement. Acceptance needs a conservative dependency-map contract, bounded and recoverable background obligations, narrowly identified inherited failures, an accurate scoped-install risk model, complete stage/publication safeguards, corrected budget arithmetic and selection, and a build order that never removes a backstop before its replacement exists. Those changes affect the foundation enough to warrant another read.

## Opus (foundation)

The independent Opus 5.5 reader's foundation read of round 1. Its paths are relative to the design's tree.

### 1. Claims about the code

All paths are relative to the design's tree. "design" means `docs/2026-10-24-ci-scope-design.md`. I read the design as landing after CIFAST1 (`ef68f4eaa` and `02ad7bc17`).

**What holds.** Most of the code citations check out:
- `BLOCKING` :902, `PATH_CHECKS` :916, `REACH` :958 and its first rule, `DRIVER_READERS` :945, `BESIDE_GIT_HALF` :481 and `laneSchedule` :606.
- `mergeNext` :4400 and :4423, `ciPlan` :5077, and `classPlan`'s three whole rules at :5141, :5144 and :5147.
- `SWEEP_BOUND_MS` :1867, `sweptOut` :1877, `stageJudgement` :1895, `addVerdicts` :1727, `CLASS_VERDICTS_KEPT` :1748, `MEMORY_NEEDS` :2177, `CI_IDLE` :5067 and `gateCi` :5345.
- In `tools/process-reach.mjs`: `areaOf` :555, `censusOf` :573 and `pathReach` :744.
- `TreeGates.cs:41-56`.
- The rehearsals' scratch, home, port, pipe and mutex lines.
- `Driver.cs` has 4,103 lines, and there are 15 module folders.

**What is wrong or overstated:**

1. **The run is not exact for the .NET suites.** design:232 says "The run is exact for every suite but the git half", and D207 point 2 repeats it. But the .NET reach is "PROCREACH1's census unchanged" (design:160), and that census follows only what a class's files name, one level deep (`tools/process-reach.mjs:27-29`).
   - D180.md:489 records that a full closure reached 88% of the driver's files from every class.
   - D180.md:520-522 records the gap and its only bound: "goes unseen until the next sweep".
   - FIX-LOG.md:960-972 is a real case. GENREGEN1's change to `Trees/SessionTrees/GeneratedWriters.cs` broke `NoConsoleWindowTests` and `EveryChildIsHandedTheToolsTests`. Both walk every driver source (`Daoris.Desktop.Driver.Tests/NoConsoleWindowTests.cs:15,44`; `Agents/EveryChildIsHandedTheToolsTests.cs:22,71`) and name no `Trees` type, so a name census does not select them for that change.
   - Two reflection tests have the same blind spot: `Daoris.Desktop.Modules.Tests/Platform/PipeCensusTests.cs:272` and `Daoris.Service.Tests/Mcp/McpToolRegistrationTests.cs:76` each read their whole assembly's types.
2. **The install is not reversible as claimed.** design:276-277 says "The install is local and reversible: D139's staged update keeps the running version, and its rollback". That holds only until the new build confirms. `InstallSwap.Finish` deletes `update/previous/` at that point (`src/Daoris.Desktop/Daoris.Desktop.Driver/Setup/InstallSwap.cs:373-376`; D139.md:40). Whatever the new build wrote into the home's stores also stays (`.claude/knowledge/compatible-builds.md`).
3. **`6ce6235`'s 74 minutes were not "the rehearsals alone".** design:59, :62-63, :367 and D207.md:11 say they were. By the design's own table the two rehearsals were 23.4 + 20.7 = 44.1 of 74.3 minutes. A rehearsal waits for the whole light lane first (`merge-branch.mjs:625`, `component === null ? light`).
4. **Not every driver-lane path reaches both rehearsals.** design:39-40 says it does. The driver's tests are in the driver lane, but `REACH` sends them only to `driver`, `driver-process` and `driver-process-git` (`merge-branch.mjs:1070`).
5. **The cheap tier is not "under a minute" by any measure taken.** design:212-215 claims it was measured. The 27.8 s in §2.5 (design:129-131) ran none of these:
   - `web-types`, about a minute alone (MERGETSC1);
   - the locale vitests, which run in jsdom with a setup file (`src/Daoris.Web/vite.config.ts:29,38`);
   - `code-map`;
   - the CLI's typecheck;
   - the census rows at `docs/development.md:113,115,117`, which need builds.
6. **The per-merge cost model disagrees with the measured walls.** The model at design:72-73 is class time divided by width. It puts the driver's other half, run whole, at about 4.75 minutes (38/8). The same half ran whole in 7.4 and 10.0 minutes (design:55, 57), partly because the `Alone` tail runs one class at a time (`docs/development.md:83-85`). So §4.4's "about 5" and "10 to 16" are low.
7. **The git half's target does not fit its budget.** design:235-236 says four times the budget, which is 32 minutes. design:355-356 and D207.md:42 say 40 minutes of class time, which at four wide is 10 minutes, not 8.
8. **The driver's other half has three class counts:** 114 (design:56), 115 (design:58, 78) and 118 (design:86-88).
9. **"Coalescing made the CI slow" is half right.** D207.md:49-50 blames coalescing. Coalescing makes one run wide but lowers total work. §2.3's own per-merge sum is 1,141 git-half class-minutes over 40 merges; a few whole runs of 31.5 to 43.6 minutes covered the same merges. Per-merge runs buy attribution, not less work.

### 2. The mechanism's holes

**A dependency the map misses.**
- **What gets missed.** Mainly the .NET naming census (claim 1): a source walked as text, types read by reflection, and helpers called through named code. In TypeScript, "the static import closure of the file within its package" (design:162-163) stops at the package boundary. Cases it cuts off:
  - `src/Daoris.Cli/tool-test/*.test.ts` import `../../../tools/desktop-publish.mjs`, `deployment-rehearsal.mjs`, `rehearsal-kit.mjs`, `processes.mjs` and others. Today `REACH`'s hand rows cover these (`merge-branch.mjs:969-1003`).
  - The page's tests use literal `await import(…)` (for example `src/Daoris.Web/src/bridge/bridge.test.ts`).
  - The page's twins name a basename and walk a folder (`src/test/driverSource.ts:11-29`; `locales/note.test.ts:40`), not a path.
  - .NET classes that run the CLI under node (`merge-branch.mjs:1146-1160`) cannot be derived; "each row becomes a check" (design:177-178) then fails until a `[Reaches]` is written.
  - The page builds into the HTTP host's `wwwroot` (`vite.config.ts:16`), a generated link no string names.
- **What catches a miss, and how soon.** No scoped check catches it. The census check (§3.4) tests the inventory of units, not their reach. Two things do:
  - A later merge that does reach the broken unit. It fails, its merge is blamed, and it goes back.
  - The background run's whole pass. "Idle and no scoped run in flight" (design:258-259) seldom holds here: main took 127, 71 and 28 merges on 2026-10-21, 22 and 23, plus 21 to 31 direct commits a day, and 13 commits in the 02:00 hour of the 21st. So the bound is the nightly run plus its own hour, about a day, the same as PROCREACH1's.
- **What the install may have staged meanwhile.** Today `sweptOut` refuses a stage past the day, and the fast halves run whole whenever their gate is reached. Under §5.3 and CISCOPE1b, the install can be republished any number of times, on known-red units too. Once a build confirms, its predecessor is gone (claim 2).
- **What the design gives way to.** It gives way only to the git half's cost (§4.3), never to the map's misses. The cheapest give-way is in its own table. The .NET fast halves take 1 m 39 s and 1 m 43 s of tests (design:109, 111), and the build dominates. Running a reached .NET fast half whole costs under two minutes and restores the net that caught FIX-LOG.md:960. Unit scoping would then apply where it pays and where reach is a real closure: the TypeScript suites (418, 441 and 239 s) and the .NET Process halves, which keep the day's sweep.

**The republish against D115.**
- §5.3 lets the install stage on scoped-green or `owed`. It drops:
  - the sweep bound (CISCOPE1b);
  - the family rehearsal that CIFAST1's stage path just made a stage requirement;
  - every owed git-half class. 75 of the 89 are `Trees` tests (design:101-103), and `Trees` is the landing the install lands this repository with (`docs/development.md:198`);
  - any unit already known red.
- This reverses D180's title and its rejected "Letting an install out without a green run" (D180.md:1, :52). It also reverses CIFAST1's note of the day before: "the full set stands before a stage", with nightly rehearsals rejected because "a stage would still meet failures found late".
- The owner's direction is met by a merge that waits minutes. It does not require staging the install ahead of the background run. The install is the tool development runs on (DOGFOOD1), so a broken landing there blocks development more than an hour's wait would.
- The public-release rule is sound, and stricter than today's (D180.md:521 lets a release ship within the day).
- The design contradicts itself on what a stage waits for:
  - design:297-298 says "that reach now decides what a stage must wait for";
  - design:283-288 says the stage waits only on the deployment rehearsal, for a listed set of files;
  - "The stage runs it (CIFAST1's stage path)" runs every gate the commit lacks, in the gate tree that the background run holds for an hour;
  - `--from-scoped` would hold the scope tree through a publish build, which holds every merge (design:248-249).
- **The update-path list is incomplete** against what the deployment rehearsal drives (`tools/deployment-rehearsal.mjs:31`). It misses:
  - `Daoris.Desktop.Modules/Update/`: `InstallUpdater.cs`, `UpdateLauncher.cs:24` and `HomePlacing.cs`;
  - `Daoris.Desktop.Driver.Host/Program.cs`;
  - `Driver/Platform/SpaceFree.cs` and `DaorisHome.cs`.

  All of these name `StagedBuild`, `InstallSwap` or `update.json`. The desktop's `Directory.*.props` are missing too.

**The merge's own wait.**
- **Throughput.** Candidates are fast-forwarded one at a time, so each merge waits through its scoped run (design:199-205). At 127 merges a day, merges arrive about every 11 minutes. About 78% reach the Process halves (31 of 40, design:67) and take 10 to 16 or more minutes. That is roughly 98% busy on a peak day, so the queue grows. D180 rejected the merge as the bottleneck (D180.md:49).
- **What the scoped set judges is unclear.** It could be the merge's own diff (§2.6's method, `<merge>^1 <merge>`) or each unit's standing verdict. Direct commits on main never get a scoped run, for example `6ce6235b3 fix(driver)`, `59a2db074 fix(modules-tests)` and `e02a5f06d test(web)`. §5.2's fix-forward commits are of that kind. A later candidate's scoped run then judges only its own merge, and `--from-scoped` would stage the direct commit's unverified change.
- **`--rerun` and `--stale` stop working as now.** They dispatch on `mergeInProgress` (`merge-branch.mjs:5684`), and the candidate's `git merge --abort` makes that false.
- **The verdict store would overflow.** Per-unit verdicts in "the one store" (design:226-227) would evict, within a single run, the gate verdicts the stage reads, because `gate-verdicts.json` keeps 400 (:1727).
- **`owed` means two opposite things.** CIFAST1's `ciPlan`, status and record all use `owed` for "a stage must pay this". The design uses `owed` for "a stage may pass over this".

**Background run, failures, fix-forward.**
- The background run's Process lane runs at normal priority (FAMILYFAIL1c). Beside a scoped run's Process classes, that brings back the timing flakes GATEORDER1 removed.
- Known red is keyed by unit, meaning a class or a file (design:270-272). A merge's new failure in another test of that unit would therefore be discounted. A unit that fails is never re-run on the candidate's first parent, so "the merge did not make it" is asserted rather than tested.

**The census that keeps the map honest.** None of the five checks in §3.4 measures recall, that is, whether a unit's real dependencies fall inside its reach. Two of the checks also have problems:
- Check 1 compares against "the last whole run". In a scoped run that is ambiguous: a merge that adds a class either fails the check or the check is empty.
- Check 4 is unsized. Classes at a project's root, with no subject, sit in 70 root files of the driver's tests, 11 of the modules', all 37 of `Service.Http.Tests` and 4 of `Service.Tests`.

**The rehearsals side by side.** The reasons the design gives hold: ports, homes, pipes, mutexes and builds are apart, and a worktree for each rehearsal still packs in place. Three gaps:
- REHPAIR1, which CIFAST1 filed for this pairing, is not named.
- Three more worktrees' dependency installs and cold builds are not priced.
- Memory: 30 GB for the rehearsals, plus 8 for the background's Process half, 8 for a scoped run's and 6 for a heavy suite, is 52 of 63.7 GB. Beside five workers and the owner's install, MEMGATE1 will serialise them, so "about 23" is a quiet-machine figure.

**TESTVALUE1's rule and target.** The rule measures each test by the lines and branches it covers in its own language. That measure is blind to two kinds of test:
- A git-half test whose subject is real git. A fast stand-in covers the same C# lines.
- A twin, census or golden that reads across languages. A vitest parsing `NoteCodes.cs` covers only the parser's TS lines.

Both would read as redundant and be removed or moved down a tier. Three more problems:
- "No unique coverage" selects both members of a pair that cover only each other. The after-proof catches it, but the rule still picks them.
- A real race that first shows as a flake earns no catch.
- The per-unit coverage runs are not priced: about 2,000 classes, each with its own test-host start, 423 jsdom files, and the git half serial.

The target of 40 minutes (claim 7) has no measured basis for cutting 75% of the git half.

### 3. The rows

- **CIFAST1** is built (`ef68f4eaa`) and lands alone. Its tool-tests and scratch timing prove its plan. It is the fastest win for the CI's wall. It does not shorten a republish, which still pays both rehearsals one after the other. It is rightly first.
- **CISCOPE1b**: the composed verdict alone is a fast second win (`f296996`'s 31.5 minutes). But moving the sweep "to the background schedule" cannot land alone: that schedule is CISCOPE1e's, and the watch starts no run for a sweep alone (D180.md:521-522). Between rows 2 and 5 nothing would sweep and the stage would stop asking. The composed verdict would also pass a half that "failed outside a test" (`merge-branch.mjs:5097`). Its proof shows the composition works, not that nothing is hidden.
- **CISCOPE1c** can be built alone. Its proof covers inventory and cost, not recall. Its `check` "runs in every scoped run", which does not exist until row 4.
- **CISCOPE1d** cannot stand alone in this order:
  - It depends on row 6 for its value order.
  - It depends on row 5 for known red. Without known red, one failure on main blocks every merge near it, which D207 itself rejects.
  - It depends on row 5 for the never-list that `--from-scoped` needs (design:380), yet it ships `--from-scoped`.
  - Its proof measures latency against figures that are low (claim 6), not misses or throughput.
- **CISCOPE1e** can be built after row 4. Its proof should add the known-red keying and the schedule's maximum age.
- **CISCOPE1f** can be built alone, and could come before row 4. Its coverage cost is not priced.
- **TESTVALUE1** comes after row 6. Its proof (no line or branch losing its last cover) cannot show what the rule is blind to.

### 4. Findings

1. **The map is not exact for .NET.** design:160, :232; `tools/process-reach.mjs:27-29`; D180.md:489, :520; FIX-LOG.md:960-972. **Blocking.** Smallest change: §4.3 says the .NET reach is one level of naming, bounded by the sweep. §4.2 point 2 runs a reached .NET fast half whole, and unit scoping applies only to the TypeScript suites and the Process halves.
2. **The install's floor is too low.** design:276-288; D207 point 4; `InstallSwap.cs:373-376`; D180.md:1, :52. **Blocking.** Smallest change: `--from-scoped` stages only a commit that meets all of these:
   - nothing owed;
   - no known-red unit in a module its delta reaches;
   - kept Process verdicts within `SWEEP_BOUND_MS` (keep `sweptOut` for the install);
   - CIFAST1's rehearsals paid.

   Drop "reversible". If the owner wants the install staged sooner than that, D207 records it as the owner's acceptance of D180's rejected item, with the exposure stated.
3. **No queue model for merges.** design:199-205, :241-243; D180.md:49. **Blocking.** Smallest change: §4.1 states the measured arrival rate it serves. Merges that arrive while a scoped run is in flight join one candidate together, run on the union of their reach, and are bisected on red.
4. **The scoped set's basis, and direct commits.** design:68-69, :216-223. **Blocking.** Smallest change: the scoped set is every unit with no pass standing on the candidate's tree, using per-unit GATE6 standing as `classPlan` does. `--from-scoped` refuses a commit with an ancestor since the last background pass that no scoped run judged.
5. **Known red is keyed by unit.** design:270-272. **Blocking.** Smallest change: re-run a scoped failure on the candidate's first parent. Only a test that fails there too is known red for this merge; every other failure counts against it.
6. **Row order.** design:369-381; D180.md:521-522. **Blocking.** Smallest change:
   - CISCOPE1b changes only the third whole rule; the sweep and `sweptOut` stay.
   - The sweep's move and `--from-scoped` with its never-list go to CISCOPE1e.
   - CISCOPE1d orders the git half's budget by the recent failures in `class-verdicts.json`, then shortest first, until CISCOPE1f lands.
7. **No proof of recall.** design:373-378, §3.4. **Blocking.** Smallest change: CISCOPE1c's proof replays a month of recorded failures (FIX-LOG.md:960 among them) through the census at each failure's introducing commit and lists any the scope would not have selected. The background run then records each failure the census held unreached as a `miss`, which gives the map a running recall figure.
8. **TESTVALUE1 measures value in the wrong currency.** design:335-351; D207 point 6. **Blocking.** Smallest change:
   - A Process unit counts as covered only by other Process units.
   - A unit that starts a program or reads a file in another language may be merged into a table, never removed or moved down a tier.
   - Removals are chosen one at a time, with uniqueness recomputed after each.
   - A flake that recurs counts toward catches.
9. **What a stage waits for, and in which tree.** design:248-249, :283-288, :296-298. Not blocking. Smallest change: one rule for which rehearsals a stage needs (Finding 2's) and where each runs. A `--from-scoped` build runs in a tree of its own, not the scope tree.
10. **The update-path list is incomplete.** design:283-288. Not blocking. Smallest change: add `Modules/Update/**`, `Driver.Host/Program.cs`, `Platform/SpaceFree.cs`, `DaorisHome.cs` and the desktop's `Directory.*.props`. Hold the list with a test that every non-test source naming `StagedBuild`, `InstallSwap` or `update.json` is on it.
11. **`owed` collides with CIFAST1's meaning.** design:223, :227. Not blocking. Smallest change: a different word for "deferred to the background".
12. **Claims 3 to 6, 8 and 9 in §1 are wrong.** Not blocking. Smallest change: correct the text, rebase §4.4 on measured walls, and price the cheap tier by what it actually runs.
13. **The budget arithmetic does not close.** design:235-236 vs :355-356; D207.md:42. Not blocking. Smallest change: a target of 32 minutes, or a budget of 10.
14. **TypeScript reach stops too early.** design:162-163. Not blocking. Smallest change: follow every relative import and literal `import()` anywhere in the repository. A string naming a folder reaches every file under it, and a basename reaches that file. `setupFiles` goes on the whole-suite list.
15. **The composed verdict can hide a failure.** `merge-branch.mjs:5097`, :5147. Not blocking. Smallest change: compose only when the half's newest verdict is a pass that went stale by reach.
16. **The verdict store overflows.** design:226-227; `merge-branch.mjs:1727`. Not blocking. Smallest change: a per-unit store, plus a retention rule for `test-history.jsonl`.
17. **Background schedule and machine sharing.** design:258-264. Not blocking. Smallest change: the nightly run is unconditional, with a stated maximum age. The background starts no Process class group while a scoped run's Process tier is running.
18. **Census checks 1 and 4.** design:186-193. Not blocking. Smallest change: check 1 runs in the background run against its own reports. In a scoped run, every unit run must be in the census. Size check 4 by the root files counted in §2.
19. **The candidate breaks the tool's verbs.** design:199-205; `merge-branch.mjs:5684`. Not blocking. Smallest change: say how `--rerun`, `--stale`, `--continue`, `--batch` and conflict resolution work on a candidate, and what happens if main has moved before the fast-forward.
20. **Coverage runs are not priced.** design:335-338. Not blocking. Smallest change: estimate the per-unit coverage pass and how often it re-records.
21. **REHPAIR1 and the side-by-side cost.** design:300-323. Not blocking. Smallest change: CISCOPE1e takes over REHPAIR1; price the three worktrees; state the expected wall under MEMGATE1 on a working day.

**21 findings, 8 blocking.**

**Verdict: another round.** The foundation holds for a merge but not for a republish. A reach map can be good enough for a merge to wait on. A miss on main is fixed forward, as D180 accepted, provided the map's recall is measured and the background sweep pays for its misses within a day. It cannot be exact for the driver. PROCREACH1 already showed that the closure is nearly everything, which is why the census follows names one level only, and a real miss of that kind is in FIX-LOG.md:960. So a republish of the owner's install cannot rest on the map alone. The design gives way only to the git half's cost and never to the map's misses. Its one backstop, the background run, is a run the install no longer waits for. On top of that it drops the sweep, the family rehearsal, the owed landing tests and the known-red guard from the stage, on a reversibility the code does not provide once a build confirms. Findings 1, 2, 5, 6 and 8 are textual and checkable. Findings 3, 4 and 7 (the merge queue at the measured arrival rate, the scoped set's basis with direct commits, and a recall proof) are design work, so round 2 should rewrite §4.1, §4.2, §5.3 and §8 and be read again as a foundation.

## Codex, round 2

Codex's read of round 2, at `aceced648`: 15 findings, 11 blocking; another round.

### 1. Each round-1 finding

Here, **design** means `docs/2026-10-24-ci-scope-design.md`. “Taken” judges the revised design contract; it does not claim implementation or runtime proof.

| Finding | Verdict | Where | Why |
|---|---|---|---|
| Codex 1 — rehearsal saving | Taken | design:104; D207:113 | The rehearsals account for 44.1 minutes; preparation and the preceding light lane are separated. |
| Codex 2 — latency categories and Process exceptions | Taken | design:131,136,455 | CLI exceptions and whole-suite fallbacks are acknowledged, and estimates await measured distributions; finding 2 below identifies a remaining omission. |
| Codex 3 — retry policy | Taken | design:427 | Preserves Process retries and fast-.NET refusal, consistent with `merge-branch.mjs:3540,3559,3560`. |
| Codex 4 — dependency completeness | Taken | design:247,256,262,300 | Adds declared indirect edges, conservative fallback and independent edge fixtures; no longer treats inventory as completeness. |
| Codex 5 — candidate inventory versus old reports | Taken | design:286 | Discovery and execution evidence are compared with their own source tree. |
| Codex 6 — browser/build disposition | Taken | design:424 | Browser gates and their production build are explicitly deferred. |
| Codex 7 — background detection bound | Open | design:480,488,497 | Recovery records improve matters, but admission and overdue-stage enforcement remain incomplete: findings 11 and 13. |
| Codex 8 — known-red masking | Open | design:363,507 | Test-level identity improves isolation, but waiver eligibility and fingerprint collisions remain: finding 6. |
| Codex 9 — rollback overstatement | Taken | design:537 | Withdraws reversibility; `InstallSwap.cs:374` removes the previous build after confirmation. |
| Codex 10 — incomplete update-path list | Answered with a reason that holds | design:528 | The list is withdrawn; every stage must satisfy the background and rehearsal requirements. |
| Codex 11 — composition hiding gate failures | Taken | design:652 | Requires a completed passing aggregate, complete results and matching discovery; outside-test failures still force execution. |
| Codex 12 — unsafe build order | Open | design:664,671,680 | Removes the original stage/value dependencies, but cross-run resource admission still arrives after its consumer: finding 15. |
| Codex 13 — budget arithmetic and selection | Taken | design:416,634 | Selects the entire reached set when it fits, includes overhead and longest-class constraints, and withdraws the unsupported 40-minute target. |
| Codex 14 — measurement evidence | Taken | design:593,600,616 | Adds attempts, fingerprints, linked fixes, Node coverage, invalidation and a maturity window. |
| Codex 15 — equal coverage versus equal obligations | Taken | design:614 | Removal now requires an explanation of retained assertions and cases, alongside coverage. |
| Codex 16 — rehearsal isolation | Open | design:562,579,680 | The proposed claims, ledger and loaded proof address pairing, but their placement leaves earlier cross-run admission unprotected: finding 15. |
| Codex 17 — candidate lifecycle and competing landings | Open | design:85,358,376 | DOGFOOD1 corrects the install-bypasses-main premise; the replacement lifecycle still lacks the recovery cases in finding 4. |
| Opus 1 — .NET reach is not exact | Taken | design:160,405,438 | Runs reached fast halves whole and explicitly retains the Process census’s limitation. |
| Opus 2 — install floor | Open | design:518 | The steward’s stronger floor is adopted, but its baseline, waiver and enforcement mechanisms remain incomplete: findings 9–12. |
| Opus 3 — merge queue | Open | design:351,363,370 | Batching is specified; bisection and the claimed wait bound do not yet hold: findings 3–5. |
| Opus 4 — standing basis and direct commits | Taken | design:356,396 | Selection uses standing evidence on the candidate tree, including differences introduced by direct commits. |
| Opus 5 — known red keyed by unit | Open | design:507 | Class-wide waivers are removed, but the revised test-level policy still has finding 6’s holes. |
| Opus 6 — row order | Open | design:646 | Original dependencies are reordered correctly; finding 15 identifies the remaining admission dependency. |
| Opus 7 — recall proof | Open | design:309,330 | Adds the requested replay and counter, but neither currently measures the claimed recall: findings 7–8. |
| Opus 8 — TESTVALUE1’s currency | Taken | design:598,617,621 | Protects program-starting and cross-language obligations, recomputes uniqueness after each removal, and counts recurring flakes. |
| Opus 9 — stage requirements and tree | Taken | design:465,521 | Withdraws scoped staging and keeps publishing out of the scope tree. |
| Opus 10 — update-path omissions | Answered with a reason that holds | design:528 | Withdrawing the selective update-path exception removes the need for that list. |
| Opus 11 — `owed` collision | Taken | design:422 | Uses `deferred`; CIFAST1’s `owed` retains its stage meaning. |
| Opus 12 — inaccurate measurements and claims | Taken | design:104,111,131,194; D207:113 | Corrects the named arithmetic, historical counts, cost-model qualification and coalescing explanation. |
| Opus 13 — budget arithmetic | Taken | design:634 | States capacity below 32 class-minutes before overhead and leaves the achievable target to measurement. |
| Opus 14 — TypeScript package boundary | Taken | design:249 | Follows repository-wide relative and literal dynamic imports, plus folder, basename and glob dependencies. |
| Opus 15 — unsafe composed verdict | Taken | design:652 | Narrows composition to a complete passing aggregate invalidated by reach. |
| Opus 16 — verdict-store overflow | Taken | design:430,593 | Separates unit evidence from the 400-entry gate store and gives attempt history its own retention. |
| Opus 17 — background schedule and sharing | Open | design:482,497 | Nightly catch-up is specified; one-sided admission cannot establish non-overlap or progress: finding 13. |
| Opus 18 — inventory and root subjects | Taken | design:286,294 | Binds inventory to its tree and limits mandatory root-subject declarations to the git half. |
| Opus 19 — candidate verbs | Open | design:354,376 | Conflict set-aside and `--continue` describe incompatible ownership of the same scope tree: finding 4. |
| Opus 20 — coverage cost | Taken | design:607 | Supplies an explicitly provisional estimate and requires a pilot measurement before recording every suite. |
| Opus 21 — REHPAIR1 and pairing cost | Taken | design:559,579 | Names REHPAIR1, retains serial fallback, and requires cold-cost and repeated working-day measurements. |

### 2. What round 2 introduced

The scoped-merge/background-install distinction follows the steward’s direction. The raised install floor, whole reached .NET fast halves, explicit browser deferral and narrowed composition rule address substantial round-1 defects.

The new foundation is not settled, however. Prefix bisection assumes an attribution property tests do not guarantee. Queue recovery does not cover promotion’s irreversible boundary. The recall rules can count a selection made before a regression as evidence that its introduction was covered. The install checks depend on a last-staged record that normal CI clears, and use the acknowledged incomplete census to excuse background failures. Background execution also needs one coherent admission policy before the scoped runner starts sharing its machine.

### 3. Findings

1. **Non-blocking — the description of modules’ rehearsal reach is wrong.**  
   `docs/2026-10-24-ci-scope-design.md:76` says every driver-lane and modules-lane source reaches both family and deployment rehearsals. `tools/merge-branch.mjs:974` defines `MODULES_READERS` as `modules`, `modules-process`, and `deployment`; neither the modules rule at `:1215` nor its C# exception at `:1136` adds the family rehearsal.

   **Smallest change:** distinguish the two lanes’ readers. Preserve the narrower, correct statement at design `:529` that both reach deployment.

2. **Non-blocking — the revised non-driver cost table omits required whole .NET suites.**  
   `docs/2026-10-24-ci-scope-design.md:457` assigns records/documents only the cheap tier, and `:458` assigns page changes cheap checks and vitests. But the design’s own rule at `:405` runs reached .NET fast halves whole. Today’s reach sends documents to `service` (`tools/merge-branch.mjs:1249`), page changes to `service`, `driver`, and `modules` (`:969,1205`), and CLI sources to `driver` (`:972`).

   **Smallest change:** enumerate those suites in the table and reprice preparation, builds and tests before using its durations in the queue model.

3. **Blocking — failing-test prefix bisection does not cover the failures the scoped run can produce.**  
   `docs/2026-10-24-ci-scope-design.md:363` promises to pin every failure to one member using failing tests alone. Yet `:428` correctly makes build, discovery and host failures red; those may name no runnable failing test (`tools/merge-branch.mjs:2043`). Test outcomes also need not be monotonic across prefixes: an earlier failure can be fixed and a different failure introduced later, and a failure can require two members together. Multiple failing tests need not follow the same three bisection steps.

   **Smallest change:** define an unresolved/group attribution outcome; handle infrastructure and compilation failures separately; preserve the execution context needed to reproduce order-dependent failures. Fully rejudge the retained candidate before promotion, and prove build failure, interacting members and multiple independent failures. Limit the three-step claim to one reproducible monotonic failure over eight members.

4. **Blocking — candidate recovery is not idempotent across promotion, and conflict ownership contradicts itself.**  
   At `docs/2026-10-24-ci-scope-design.md:358`, promotion changes main before the queue can record `promoted`. A crash between those operations leaves a green candidate whose base differs from main. The restart rule at `:379` reforms it instead of recognising that it already landed. Existing `resume` explicitly checks ancestry (`tools/merge-branch.mjs:4505`); the replacement needs an equivalent recovery rule.

   Separately, design `:354` sets a conflicting member aside and continues forming the candidate, while `:381` promises that its conflict remains open in that same scope tree for `--continue`.

   **Smallest change:** recognise already-promoted candidates before treating base movement as new work, with crash probes before and after the git update and record write. Choose one conflict policy: pause formation with exclusive ownership, or abort/set aside and requeue a separately resolved member.

5. **Non-blocking — batching does not establish the advertised two-run wait bound.**  
   `docs/2026-10-24-ci-scope-design.md:370` promises at most two runs and says arrival rate cannot grow the queue. Eight-member capacity only bounds service capacity. Bursts can exceed eight waiting entries; union scopes can cost more than a single merge; direct commits can invalidate repeated candidates; and finding 3 removes the claimed fixed bisection cost. The measured peak hourly count does not bound arrivals in every run-sized interval.

   **Smallest change:** call 30–56 minutes an estimate, not a maximum. Replay arrival timestamps together with direct commits, union-scope costs, capacity overflow and red/restart cases; report backlog and tail waits.

6. **Blocking — known-red eligibility is inconsistent, and its fingerprint can merge distinct failures.**  
   `docs/2026-10-24-ci-scope-design.md:363` waives any same-fingerprint base failure after filing it. At `:509`, waiver requires an already known-red background failure *and* base confirmation. Those rules disagree for an unverified direct commit that introduced a failure before its first background run.

   The fingerprint at `:508` also retains only the assertion’s first line and normalises numbers. Distinct failing assertions or theory cases can therefore share the same signature. Current helpers already collapse theory results to their method (`tools/merge-branch.mjs:2023,2119`), so they cannot supply the stronger identity without changing.

   **Smallest change:** specify exactly how base-discovered inherited failures become eligible and apply that rule everywhere. Preserve case identity and assertion location or an equivalent discriminating diagnostic. Add proofs for an unfiled base failure, another theory case, and a different assertion with the same first-line message.

7. **Blocking — the replay and running counter use selection somewhere in an interval as causal coverage.**  
   `docs/2026-10-24-ci-scope-design.md:316` guesses an introducing commit from overlap with the eventual fix, then accepts selection by *some* commit in an ambiguous range. At `:331`, the running counter likewise excludes a miss whenever any scoped run in the range selected the unit.

   A unit can pass when selected early in the range, then break on a later change whose dependency is missing. These rules report that regression as covered. Selecting a unit on an abandoned candidate is likewise not proof about the promoted tree. Conversely, deliberately deferred browser tests or budgeted classes can be counted as census misses.

   **Smallest change:** relate selection and its verdict to the introducing change and the tree that reached main. Keep uncertain introductions explicit. Count missing dependencies, deliberate deferrals, unjudged direct changes and execution failures separately; deduplicate repeated sightings of one regression.

8. **Blocking — the historical proof lacks the inputs needed to replay the claimed scoped decision.**  
   `docs/2026-10-24-ci-scope-design.md:319` asks for “the census tool at that commit” to decide what §4.2 would have run. The new tool does not exist at those historical commits. More importantly, §4.2 depends on standing verdicts, durations, failure ordering and budget state, while `:188` acknowledges that the retained stores cover only a short period. Starting each historical commit with no evidence would select broadly and inflate recall.

   **Smallest change:** pin the new selector version and run it against historical source trees, as `tools/process-reach.mjs:819` already supports for source reading. Separate structural reach replay from historical scheduling replay. Declare unavailable state unknown, preserve the initial results before repairing misses, and use explicit fixtures for the standing/budget decisions that cannot be reconstructed.

9. **Blocking — `staged.commit` is not a durable last-staged baseline.**  
   `docs/2026-10-24-ci-scope-design.md:532` bases the install’s known-red exclusion on that field. `recordStaged` writes it (`tools/desktop-publish.mjs:1020`), but each CI run replaces it with `staged: null` at `tools/merge-branch.mjs:5486,5536`. Pruning deletes the entire gate-tree record at `:3106`.

   **Smallest change:** retain successful stage history independently of the disposable gate-tree record. Define first-stage, missing/unreadable history and non-ancestor cases conservatively. Prove stage → prune or another CI run → later stage, including an earlier direct change followed by an unrelated commit.

10. **Blocking — the install’s known-red exception trusts the same incomplete map the background run is meant to backstop.**  
    `docs/2026-10-24-ci-scope-design.md:531` permits a failed gate when the last-staged delta reaches none of its known-red tests “by the census.” But `:438` explicitly admits that the Process census misses dependencies.

    A change since the last install can introduce a failure through precisely such a missing edge. The background run discovers it; filing it as known red then lets the same map say that the change did not reach it and permits staging. That contradicts `:447`’s claim that scope misses cannot reach the install.

    **Smallest change:** use a conservative gate-level exclusion for .NET known red, or require a separately justified complete dependency proof before waiving it. Refuse when reach is unknown. Add a stage test where an indirect dependency is intentionally absent from the class census.

11. **Blocking — keeping `sweptOut` does not enforce the new overdue-stage promise.**  
    `docs/2026-10-24-ci-scope-design.md:494` says an overdue whole run prevents staging, and `:527` presents retained `sweptOut` as the safeguard. In the code, a whole pass never expires: `tools/merge-branch.mjs:1909` returns false whenever `classes.kept` is zero, and `stageJudgement` can fall back to another standing whole pass (`:1937`).

    **Smallest change:** explicitly make the stage consult the durable sweep obligation if overdue status must refuse staging. Alternatively narrow the promise to the existing kept-verdict rule. Prove an old whole pass and an older standing whole pass behind an expired composed result.

12. **Blocking — background proof and all stage consumers are not specified together.**  
    `docs/2026-10-24-ci-scope-design.md:521` says `--from-gated` “stays the one stage path,” and `:525` requires refusal without background proof. Today, plain `--stage` remains supported through `tools/desktop-publish.mjs:1154,1163`; `--from-gated` delegates to a child with that flag removed (`:1002,1137`). Both ultimately ask `--passed`, whose shared gate evidence records no run kind (`tools/merge-branch.mjs:4069`).

    A tree’s location and a collection of gate passes do not themselves establish that a qualifying background run judged it. Nor does the row specify how the new checks reach the existing direct-stage path.

    **Smallest change:** define a qualifying background receipt and its provenance, including permitted standing evidence and known-red treatment. Enforce it at every ordinary install-stage entry point, preserving explicit owner overrides as such. Prove direct stage, delegated child stage, scoped-only evidence and exact public-release proof.

13. **Blocking — one-sided Process admission cannot guarantee non-overlap or background progress.**  
    `docs/2026-10-24-ci-scope-design.md:497` prevents a background group starting while the scope tier runs. It says nothing about a scope tier starting after a background group has begun. The simultaneous promises—no overlap, no interruption of background work, and no scope wait—cannot all hold then. Current class groups are whole half invocations (`tools/merge-branch.mjs:5193`), potentially tens of minutes.

    Strict foreground priority can also leave a nightly run started but unable to execute its Process work through continuous scoped candidates.

    **Smallest change:** define symmetric admission with bounded work groups, explicit foreground priority and a stated progress policy. Account for any bounded wait in scoped latency. Prove background-first arrival, simultaneous acquisition, retry-alone execution, restart and sustained foreground load.

14. **Non-blocking — the background rehearsal policy contradicts “CIFAST1 unchanged.”**  
    `docs/2026-10-24-ci-scope-design.md:549` runs rehearsals wherever promotions reach them, while `:707` says CIFAST1 is unchanged. CIFAST1 deliberately leaves family and deployment owed on ordinary watch runs (`tools/merge-branch.mjs:5456`; `docs/decisions/D180.md:540`). Nightly execution is an understandable amendment, but ordinary post-promotion execution is another policy change.

    **Smallest change:** specify the execution matrix for ordinary, due-sweep, nightly, stage and release runs. State which modes override CIFAST1 and amend the decision note accordingly.

15. **Blocking — cross-run memory admission is built after cross-run execution starts.**  
    `docs/2026-10-24-ci-scope-design.md:671` enables the scope runner beside the background runner. Their shared memory ledger arrives only in CISCOPE1f at `:680`. Serialising rehearsals with one another does not protect them, or background light suites, from a newly starting scoped run. Existing `memoryGate` samples independently and eventually runs despite insufficient memory (`tools/merge-branch.mjs:2355,2365`).

    **Smallest change:** move shared admission and foreground headroom into the background/scoped-run prerequisites. Leave rehearsal pairing and its five-run proof in CISCOPE1f. Prove reservation accounting against current free memory without double-counting, simultaneous starts, release after failure, and recovery after a killed holder.

**15 findings; 11 blocking.**

**Verdict: another round.** Preserve the steward’s direction: merges wait on scoped verification, and the install waits on qualifying background proof. Another read is needed after the queue’s attribution and recovery rules, causal recall measurement, durable stage baseline, conservative known-red exclusion, and shared admission policy are made concrete. Those changes determine whether the proposed proofs establish the foundation’s claims; they are more than corrections the steward can check mechanically.

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2, at `aceced648`: 17 findings, 6 blocking; accept after named changes the steward can check.

### 1. Each round-1 finding, answered or not

I checked each verdict against `tools/merge-branch.mjs`, `tools/process-reach.mjs`, the lane and gate files, D115 and D180 at `aceced648`. CIFAST1 is on main. "Fn" points to a finding under heading 3.

**Codex**

| # | Verdict | Where | Why |
|---|---|---|---|
| 1 | taken | §2.1, §8 row 1, D207 note | 44.1 of the 74.3 minutes were the two rehearsals. The rest is the light-lane wait (`:647`) and preparation. Holds. |
| 2 | taken, incomplete | §2.2, §4.4 | The CLI reaching `driver-process` (`:1178`, `:1186`) is now stated. But §4.4 leaves out the fast .NET halves that REACH sends page, CLI and docs changes to (F9). |
| 3 | taken | §1, §4.2 | Today's retry classes are kept (`:3559`; D180). Holds. |
| 4 | taken | §3.2, §3.4, §3.5, §4.2 pt 2 | Declared edges, a wide fallback and an expected-edge fixture per kind are added, and a reached .NET fast half runs whole. Holds. |
| 5 | taken | §3.4 check 1 | The inventory is now bound to the tree that ran. Holds. |
| 6 | taken | §4.2 pt 6, §4.3 | Browser specs, their build and the rehearsals are deferred and named as a give-way. Holds. |
| 7 | taken, new hole | §5.1 | The due record, catch-up, overdue report and conditional bound are added. But "a failed run satisfies nothing" makes every run whole through a red period (F5). |
| 8 | taken, incomplete | §5.2, §4.1 pt 4 | Known red is now keyed by test and fingerprint. But §4.1 and §5.2 disagree on a failure on the base that is not yet known red (F2). |
| 9 | taken | §1, §5.3 | "Reversible" is withdrawn. Checked: `InstallSwap.cs:374-376` deletes the previous build. |
| 10 | answered | §5.3 pt 3 | The answer holds. The stage still needs a deployment pass standing on its tree, every driver- and modules-lane path reaches `deployment` (`:974`, `:976`), and a rehearsal failure carries no test identity that known red could waive. |
| 11 | taken | §8 row 2 | Composing is narrowed to a gate pass gone stale by reach. Its proof has a caveat (F15). |
| 12 | taken, incomplete | §8 | The backstops are kept. But the coordination between the two runners lands in row 6, after the scope runner in row 5 (F4). |
| 13 | taken | §4.2 pt 5, §7.3 | The whole reached set runs when it fits, and 28 class-minutes closes the arithmetic. |
| 14 | taken | §7.1, §7.2 | Attempt-level history, Node coverage, named exclusions and a maturity window are added. |
| 15 | taken | §7.2 | The report only nominates, and each removal records the obligations kept. |
| 16 | taken | §6.2, §8 row 6 | A port claim, a memory ledger, and the serial chain as the default. |
| 17 | taken, incomplete | §1, §4.1, §8 row 5 | The correction holds: DOGFOOD1 says the install never merges. But who commits a candidate and where the records go is unsaid (F6), and conflict handling contradicts itself (F8). |

**Opus foundation read**

| # | Verdict | Where | Why |
|---|---|---|---|
| 1 | taken | §2.3, §3.2, §4.2 pt 2, §4.3 | Checked: GENREGEN1's two tests carry no Process trait (they are in the driver's fast half), and its change reached `driver` through the driver lane. So it is selected, as claimed. |
| 2 | taken, then reopened | §5.3 | The background pass, the sweep, the paid rehearsals and dropping "reversible" all hold. But point 4 lets a failing gate count for the stage, judged by the census (F1). |
| 3 | taken | §2.7, §4.1 | The arrival rate matches main's first-parent history (my per-day counts differ only at a day boundary). Batching and bisection are added. The holes are F2, F4 and F7. |
| 4 | answered | §4.2, §5.3 | Holds for the install, which stages only on the background's verdicts. For the queue, a direct commit's breakage meets the base-failure gap (F2). |
| 5 | taken, incomplete | §4.1 pt 4, §5.2 | The base re-run is added, but the two sections disagree (F2). |
| 6 | taken, incomplete | §8 | Same as Codex 12 (F4). |
| 7 | taken, incomplete | §3.5, §8 row 3 | The replay proof and the running count exist. Both count a miss as selected when a later scoped run found it (F3, F17). |
| 8 | taken | §7.1, §7.2 | Coverage counted within its currency, one removal at a time, recurring flakes count. |
| 9 | taken | §4.5, §5.3 | One stage rule, and a publish never holds the scope tree. But §6.1 now contradicts CIFAST1 (F12). |
| 10 | answered | §5.3 pt 3 | Same as Codex 10. |
| 11 | taken | §4.2, §9 | *Deferred* in the design. D207's body still says *owed* (F13). |
| 12 | taken | as named | Corrected. But §1 now sends modules-lane paths to the family rehearsal (F12), and §4.4 is still low (F9). |
| 13 | taken | §7.3 | Closes. |
| 14 | taken | §3.2 | As named. |
| 15 | taken | §8 row 2 | As named. |
| 16 | taken, incomplete | §4.2, §7.1 | The stores are now separate. Which verdicts a scoped run writes to the gate store is unsaid (F10). |
| 17 | taken, incomplete | §5.1 | The nightly run is now unconditional. A background Process half that is already running when a scoped Process tier starts is not handled (F4). |
| 18 | taken | §3.4 | Check 1 bound to its tree; check 4 sized. |
| 19 | taken, incomplete | §4.1 | Each verb is mapped to the scope tree. A conflict is both set aside and resumed (F8). |
| 20 | taken | §7.1 | Estimated, and measured on one suite first. |
| 21 | taken | §6.2, §8 row 6 | REHPAIR1 is taken over and the trees are priced once. |

### 2. What round 2 introduced

**Claims about the code.** Nearly every citation holds at `b61325e6c`. I checked `BLOCKING` :933, `PATH_CHECKS` :947, `REACH` :989, `MODULES_READERS` :974, `DRIVER_READERS` :976, :1101, :1178, :1186, `SWEEP_BOUND_MS` :1898, `sweptOut` :1908, `stageJudgement` :1926, `ciPlan` :5109, and `classPlan` :5134 with its rules at :5175, :5178 and :5181. Also `mergeInProgress` :2572, `mergeNext` :4431/:4454, :3559, `MEMORY_NEEDS` :2208, `memoryGate` :2331, `STAGE_REHEARSALS` :500, `staged` at `desktop-publish.mjs:1020`, `InstallSwap.cs:374-376` and `takePorts`.

Four claims are wrong:
- §4.1 says the merge is "today's", but today the tool never commits (F6).
- §1 sends modules-lane paths to the family rehearsal, which the lane table does not (F12).
- §6.1 runs the stage rehearsals on promotion, against CIFAST1 (F12).
- §4.4 leaves out the fast halves that REACH reaches (F9).

**Holes in the mechanisms:**
- **The merge queue and its bisection.** A failure already on the base has no consistent rule (F2). Bisecting on single tests misses failures that need their suite (F7). The commit step and the records have no place (F6). The runner and conflicts are underspecified (F8).
- **The scoped set's basis and direct commits.** Standing does cover direct commits, but only for detection: their breakage lands as an inherited failure (F2). Deferred and known-red units carry into every later candidate (F9).
- **Known red.** It waives a failure at the stage (F1). Its 72-hour expiry contradicts D207's own Rejected list (F11).
- **The recall proof and the miss count.** Both credit a miss to whichever later scoped run found it (F3). Their sources do not reach back a month (F17).
- **The install's floor.** It matches today's, except point 4 (F1).
- **The background run's schedule and its share of the machine.** A scoped Process tier cannot both "take the machine first" and never run beside a background half that will not end (F4). Failed runs re-sweep without end (F5). Which verdicts each runner writes is unsaid (F10).

**The rows.**
- Row 4 lands the stage waiver first, against §8's "the install's stage is today's throughout" (F1).
- Row 5 starts a second heavy runner before row 6 coordinates the two (F4), and needs F6 decided.
- Row 2's replay may contradict its own narrowed rule (F15).
- The public-release rule has no row (F14).

**On the foundation.**
- **A merge waiting only on its scoped run holds.** It is strictly more than today's merge, which waits on `BLOCKING` alone. The recall proof (row 3) comes before the scoped run (row 5). The .NET fast halves are judged at gate level by a table held by tests. A miss costs attribution, since the install waits for the background run. But "recall measured" needs F3, and "misses bounded" needs F2 and F5.
- **The install's floor keeps D115's and D180's guarantees**, through today's `stageJudgement`, `sweptOut` and CIFAST1's paid rehearsals, except §5.3 point 4.
- **The public release is unaffected.** `.github/workflows/release.yml` runs every gate itself.

### 3. Findings

1. **Blocking. §5.3 point 4 lets an install out on a run that is not green, judged by the census.**
   - Design lines 531-534 count a gate "whose only failures are known-red tests" when the change since `staged.commit` reaches none of them "by the census".
   - Today's `stageJudgement` (`merge-branch.mjs:1926-1956`) passes only when every gate's newest standing verdict passes. D180 rejects "letting an install out without a green run" (D180.md:52). D115's CIFAST1 note says "the full set still stands before any stage" (D115.md:558).
   - Round 2 contradicts itself: point 1 (524-526), the D207 note (D207.md:100-101) and §8's "the install's stage is today's throughout" (647) all say the opposite, yet row 4 (666) builds point 4.
   - "By the census" uses the name census, which §2.3 says is not a closure. So a test turned red by a census miss since the last stage, the very case the census cannot see, is the one waived.
   - Point 4 also has no defined interaction with `sweptOut` or with "a failed run satisfies nothing".
   - **Smallest change:** delete the waiver. The stage needs every gate passing (point 1, today's rule), and known red waives only in the queue. If the steward wants the waiver, D207 must record it as the owner accepting D180's rejected item, and judge reach by REACH at gate level, never by the census.

2. **Blocking. A failure that is on the candidate's base but not yet known red has no rule, and bisection cannot place it.**
   - §4.1 point 4 (363-364) treats a failure that repeats on the base as inherited and waives it. §5.2 (509-511) waives it only if it is also already known red, and counts "everything else".
   - Under §5.2 the failure counts against the candidate. That happens with a parent's direct commit (on 2026-10-21 these included `fix:`, `test(driver)` and `refactor(web)` code commits) and with a promoted miss that a later candidate finds (447-448). Bisection over the members then pins it to none of them, since the base itself fails.
   - Every candidate that reaches that unit is then red until a background run (38 to 74 minutes, once the gate tree is free) makes it known red.
   - **Smallest change:** one rule. A test that fails on the candidate's base with the same fingerprint is inherited for that candidate, known red or not. It is filed to `ci-findings.json` at once as known red, its 72 hours starting then, and it is never pinned to a member. §5.2's "known red and" then only decides whether the base re-run can be skipped.

3. **Blocking. The miss count, and the replay's rule for an unsettled range, count a miss as "selected" when a later scoped run found it.**
   - The running count (330-333) calls a failure a miss only if "no scoped run in that range selected" it. When a later candidate runs the unit and finds the failure inherited (F2), that later run selected it, so the miss is never counted, even though the introducing commit's own scoped run missed it.
   - That later-candidate path is the first one §4.3 names (447-448).
   - The replay (316-318) counts an unsettled range as selected "if some commit's scope selected it", which overstates recall the same way.
   - **Smallest change:**
     - Judge each failure against its introducing commit alone, found by running the failing test over main's range (the queue's own bisection). Count a miss when that commit's own scope did not select the unit.
     - Feed the count from inherited failures that candidates find as well as from the background run.
     - Count direct commits apart, as unscoped.
     - Count a range that cannot be settled as missed, in a stated lower bound.
     - Say that before CISCOPE1e the count measures today's `BLOCKING`-only merges.

4. **Blocking. The rules for sharing the machine contradict each other when a background Process gate is already running.**
   - §5.1 (497-499) says the background never ends for a merge and never blocks one, the two Process lanes never run side by side, and "a scoped run takes the machine first".
   - A "class group" is a whole half (D180.md:436), and a rehearsal cannot yield partway either. So a scoped Process tier that is due while a background git half (31 to 44 minutes on every sweep and night) or the nightly rehearsal chain (31 to 53 minutes) is running must either:
     - wait, so the merge waits on the full set, against the steward's direction;
     - end the background gate, which §5.1 and D180's CI1 note (D180.md:473) forbid; or
     - run beside it, which §5.1 says never happens.
   - Merges also gain Process halves for the first time (`isLongGate` keeps them off merges today), beside five workers and the install's sessions.
   - The coordination between the two runners and the memory ledger come in CISCOPE1f (row 6, 680-683), after CISCOPE1e (row 5) starts the second heavy runner.
   - **Smallest change:**
     - State the converse case. Either a scoped Process tier runs beside a background Process-lane gate that is already running, with its FLAKE1 re-run alone not waiting for that gate, and the background starts no Process-lane gate while a scoped tier is running or queued. Or the background runs its halves in short class groups and the scoped tier waits at most one group.
     - State the scoped run's worst wait.
     - Move the coordination between the runners, and the memory reservation, into CISCOPE1e. Its proof should time scoped runs with a background half already running.

5. **Blocking. "A failed run satisfies no sweep and no night" makes background runs whole through every red period.**
   - Design 488-491. Today a failed whole run records a fresh verdict for every class (`addClassVerdicts`), so `classPlan`'s sweep (`:5176-5180`) is satisfied for a day whatever the half's verdict.
   - Under round 2 the sweep record stays due after any failure, and the watch never re-gates a tip that failed. So each run after the next promotion runs the half whole again, for the whole time a test is known red (up to 72 hours).
   - A failed night makes every following run a run of every gate (one to one and a half hours) until one is green.
   - The background takes the most machine, and the republish waits longest, exactly while main is red.
   - **Smallest change:**
     - A half's sweep is satisfied by a run that gave every class a verdict, pass or fail, as today.
     - A night is satisfied by a whole run that finished with its failures filed.
     - Only a run that was ended or killed before its classes ran leaves the sweep or the night due.

6. **Blocking. Who commits a candidate's merges, when the parent reads the diff, and where the records go are not said. "The merge itself is today's" is wrong.**
   - Design 341-345 has the tool `git commit` each merge in the scope tree.
   - Today the tool never commits: "Committing stays the parent's, after it reads the diff and writes the records" (`merge-branch.mjs:69`; the refusal at :4517). The parent puts the message and records into the merge commit: `b61325e6c` adds the reads file, `TASKS.md` and the indexes beyond its second parent.
   - With batches of up to eight committed by the tool, the parent's review and record step has no place.
   - If the records follow promotion as direct commits, each one forces the candidate in flight to be re-formed (358-362), with no lighter path for a records-only move.
   - **Smallest change:**
     - The parent hands over the message and records with the branch (a records commit on it, or a message file when it queues), or writes them after promotion as a records-only commit.
     - A candidate whose base moved only by records is re-formed and runs only the cheap tier's record checks.

7. **Not blocking. Bisecting "the failing tests alone" (365-366) cannot place a failure that needs its suite.**
   - A test polluted by another passes alone. FLAKE1 treats that as a defect and never re-runs it alone (`merge-branch.mjs:245-249`, `:3559`). So it passes alone on every prefix and on the base.
   - Runs of single tests could also be recorded as a unit's pass.
   - **Smallest change:** run the failing test's whole class or file on the base and on each prefix, falling back to the members one at a time. Record no unit verdict from these runs.

8. **Not blocking. The queue's runner and its conflicts.**
   - The first invocation becomes the runner (347-349), so the queue lives only as long as the parent's waiting tool call.
   - A conflicting branch is "set aside ... handed back" (353-355), yet `--continue` "resumes a conflict left open" in the scope tree (380-382), which the next candidate needs.
   - **Smallest change:** make the runner a detached process like `--ci --watch`. Pick one conflict rule: hand the branch back, or hold the conflict in a tree of its own.

9. **Not blocking. §4.4 (455-461) leaves out the fast halves its own §4.2 point 2 runs, and the carry-over between candidates.**
   - REACH sends the page lanes to `service`, `driver` and `modules` (`:969`, `:1205`), the CLI lane to `driver` (`:972`, `:1229`), and docs and records to `service` (`:1234`, `:1249`). So a docs merge runs the service suite whole, about five minutes of tests plus a build, not 2 to 4 minutes.
   - Deferred git-half classes and known-red units run in every later candidate.
   - "Two runs" of waiting (370-374) leaves out re-forming after a direct commit.
   - **Smallest change:** add those fast halves to the rows, and state the carry-over and the re-forms.

10. **Not blocking. Which verdicts a scoped run writes to the gate store is unsaid (430-432).**
    - `stageJudgement` counts any standing gate verdict, whatever tree gave it.
    - If a scoped git half with deferred classes, or a suite run by file, writes a gate pass, the stage counts it.
    - If a scoped run writes none, the background re-runs on the promoted tip, the same tree, everything the scoped run just passed.
    - **Smallest change:**
      - A scoped run writes a gate verdict only for a gate it ran whole, or a Process half run by class with nothing deferred. It writes none otherwise.
      - The background keeps gate-level standing for every gate that is not a Process half.
      - Say whether "the background run has passed it" (525) means standing passes from either runner or the background's own record of that commit.

11. **Not blocking. The 72-hour expiry (512-514) blocks every merge that reaches the test.**
    - That is what D207's Rejected list says known red exists to avoid (D207.md:61-62).
    - "Its row says it is skipped" names no mechanism.
    - **Smallest change:** record the expiry as a deliberate exception in D207, and name the skip: a `Skip` carrying the row's id, held by the census check.

12. **Not blocking. Two wrong claims.**
    - §1 (76-77) says modules-lane paths reach the family rehearsal. `MODULES_READERS` (`:974`) is `modules`, `modules-process` and `deployment` only.
    - §6.1 (549-550) has the background run all three rehearsals "wherever a promotion reaches them". A watch run owes the family and deployment rehearsals (`STAGE_REHEARSALS`, `:500`), which §9 (707) keeps unchanged.
    - **Smallest change:** correct both sentences.

13. **Not blocking. D207's body and its "Changes" paragraph are still round 1.**
    - Points 1 to 7 (D207.md:14-46) still say `commit-tree`, *owed*, 10 to 16 minutes, "a scoped pass clears it", `--from-scoped` and 40 minutes.
    - "Changes" (65-68) still says PROCREACH1's "third whole rule removed", which round 2 narrows instead. The note overrides points 1 to 7 but not that paragraph.
    - **Smallest change:** rewrite the body to round 2 before acceptance.

14. **Not blocking. The public-release rule (486, 543) has no row.**
    - `--ci --release` exists in no row. The release workflow already runs every gate itself (`release.yml:234-321`).
    - **Smallest change:** assign the rule to CISCOPE1d, or name the workflow's own run as the release's gate and drop the local rule.

15. **Not blocking. Row 2's proof may contradict its own rule (656-658).**
    - Replaying `f296996` "with zero classes run" holds only if the git half's previous gate verdict was a pass gone stale by reach.
    - The run before it, `73c1fe0` (98), failed with the git half run whole. If that half failed outside a test, the narrowed rule runs `f296996`'s half whole.
    - **Smallest change:** state, from that run's `status.json`, which gate failed `73c1fe0`, and what the rule then does with `f296996`.

16. **Not blocking. Two gaps in what the rows run.**
    - `npm run verify` also runs `node --test tools/*.test.mjs` (twelve files), which §4.2 point 3 (409-411) does not assign.
    - Row 3 "changes nothing the CI runs" (661) while it adds `check` to the CI's run and to a merge.
    - **Smallest change:** name those files among the units run by file, and correct row 3's sentence.

17. **Not blocking. The recall proof's sources do not span its month (312-315).**
    - `gate-verdicts.json` holds about a day and a half (§2.5), class verdicts six per class, and the `ci-*` folders start with CI1.
    - A batch's record is dropped when the batch ends (`dropState`, `:4507`), so only the FIX-LOG reaches back to 2026-09-24.
    - **Smallest change:** state each source's span and count in the proof's output, and give the recall over the FIX-LOG's failures separately.

**17 findings, 6 blocking.**

**Verdict: accept after named changes the steward can check without another read.** The foundation now holds. A merge that waits only on its scoped run is strictly safer than today's merge, which waits on `BLOCKING` alone. The recall proof comes before the scoped run is relied on. The .NET fast halves run whole by a lane table held by tests. Since the install waits for the background run, a miss costs attribution, not the owner's install. The install's floor is today's `stageJudgement`, `sweptOut` and CIFAST1's paid rehearsals. The public release keeps its own full run. What remains is a set of local rules, not a redesign, and each can be checked against the text:
- delete §5.3 point 4's waiver (F1);
- one rule for a failure already on the base (F2);
- a miss judged against its introducing commit alone (F3);
- the converse case of machine sharing, with the coordination between the runners moved into CISCOPE1e (F4);
- a sweep and a night satisfied by a run that finished, whatever its verdict (F5);
- where a candidate's commit, message and records come from (F6).

Findings 7 to 17 are corrections and statements the steward can check line by line.

## Codex, round 3

Codex's read of round 3, at `22427ff95`: 12 findings, 6 blocking; another round.

### 1. Each round-2 finding

Here, **design** means `docs/2026-10-24-ci-scope-design.md`, and **merge** means `tools/merge-branch.mjs`. “Taken” judges the proposed contract, not completed implementation. Private run records were not inspected.

| Finding | Verdict | Where | Why |
|---|---|---|---|
| Codex 1 — modules’ rehearsal reach | Taken | design:103; merge:974 | Modules reach deployment; the erroneous family-rehearsal claim is corrected. |
| Codex 2 — omitted whole .NET suites | Taken | design:589 | The category table now includes the fast halves selected by `REACH`, with estimates explicitly unmeasured together. |
| Codex 3 — incomplete bisection | Open | design:464,468 | Retained candidates are rejudged, but discovery/host failures and non-monotonic attribution still have findings 2–3 below. |
| Codex 4 — promotion recovery and conflicts | Taken | design:441,448,498 | Ancestry-first recovery covers the promotion boundary; conflicts are consistently aborted and handed back. |
| Codex 5 — unsupported two-run bound | Taken | design:485,491 | The bound becomes an estimate; the replay includes overflow, direct commits, union costs and recovery. |
| Codex 6 — known-red eligibility and fingerprints | Taken | design:686,690,696 | One base-confirmation rule replaces the contradiction; fingerprints retain case identity and assertion location. Clearing still needs finding 9’s correction. |
| Codex 7 — non-causal recall | Open | design:347,395 | Selection elsewhere in a range no longer counts, but the introducing-commit algorithm can still choose the wrong transition: finding 3. |
| Codex 8 — unavailable replay inputs | Answered with a reason that holds | design:370,373 | A pinned selector reads historical trees; structural reach is separated from scheduling state that cannot be reconstructed. |
| Codex 9 — disposable stage baseline | Answered with a reason that holds | design:738,742 | The waiver that needed the baseline is deleted; independent stage history now serves reporting only. |
| Codex 10 — census-based stage waiver | Taken | design:724,728,738 | Every planned gate must pass; known red provides no stage exemption. |
| Codex 11 — overdue-stage enforcement | Taken | design:729; merge:1908 | The stage explicitly consults the due record and ages standing whole passes, covering what `sweptOut` does not expire. |
| Codex 12 — background provenance and stage consumers | Open | design:714,720 | Both ordinary entry points are covered, but the receipt must be explicitly bound to the content actually built: finding 8. |
| Codex 13 — admission and background progress | Open | design:657,661,671 | Overlap is now an explicit choice, but its maximum and progress claims do not follow: findings 5 and 7. |
| Codex 14 — CIFAST1 policy contradiction | Taken | design:627,762 | The matrix preserves ordinary owing and makes nightly execution an explicit amendment. |
| Codex 15 — admission built too late | Open | design:665,900 | Admission moves into the correct row; reservation accounting and recovery remain incomplete: finding 6. |
| Opus 1 — stage waiver | Taken | design:724,738 | The waiver is deleted rather than narrowed through the incomplete census. |
| Opus 2 — unfiled base failure | Taken | design:459,686 | A matching base failure is immediately inherited and filed, without waiting for a background run. |
| Opus 3 — miss credited to later selection | Open | design:347,392 | The counter reads the promoted candidate’s evidence, but causal introduction still rests on unsound halving: finding 3. |
| Opus 4 — converse admission case | Open | design:659,665,671 | The chosen overlap policy is stated and moved earlier; findings 5–7 prevent its claimed bounds holding. |
| Opus 5 — repeated whole runs while red | Taken | design:638 | Completed execution satisfies sweep/night obligations independently of whether its tests passed. |
| Opus 6 — candidate commits, messages and records | Open | design:422,424,451 | The handoff is specified, but queued inputs are not explicitly frozen, and the records-only shortcut is too broad: findings 1 and 4. |
| Opus 7 — isolated-test bisection | Taken | design:468,474,478 | It now runs the class/file, provides a wider-context fallback and writes no unit verdict from attribution attempts. |
| Opus 8 — runner and conflicts | Taken | design:436,441 | A detached runner and one conflict policy are specified; the comparison with today’s watch is factually wrong: finding 10. |
| Opus 9 — omitted halves and carry-over | Taken | design:589,597 | Whole fast halves, deferred work and re-forms are included in the stated cost model. |
| Opus 10 — scoped gate-store writes | Taken | design:553,724 | Partial file runs cannot manufacture gate passes; eligible whole/composed evidence is distinguished from the background receipt. |
| Opus 11 — expiry blocks merges | Answered with a reason that holds | design:703; D207:92 | Blocking after expiry is now a deliberate exception, with a concrete skip mechanism tied to an open row. |
| Opus 12 — two incorrect claims | Taken | design:103,762 | Modules’ narrower readers and ordinary watch owing now agree with the code. |
| Opus 13 — stale D207 body | Taken | D207:18,98 | The decision body and Changes paragraph now describe round 3. |
| Opus 14 — public-release rule has no row | Answered with a reason that holds | design:633,755 | Retaining the workflow’s existing checks removes the unassigned local release mode; its exact-commit description needs finding 11’s correction. |
| Opus 15 — composition replay premise | Answered with a reason that holds | design:874; merge:5181 | The supplied prior git-half PASS premise satisfies the narrowed rule; its private historical status is not independently verified here. |
| Opus 16 — omitted tool tests and row wording | Taken | design:530,880 | Tool test files receive a disposition, and row c acknowledges adding a blocking check. |
| Opus 17 — historical sources’ spans | Taken | design:378 | The proof reports each source’s span/count and separates FIX-LOG recall from the combined sample. |

### 2. What round 3 introduced

The install waiver’s deletion, ancestry-first promotion recovery, explicit message/records handoff, and separation of structural replay from historical scheduling are substantive corrections. The build order now places shared admission with the second runner.

The remaining foundation problems concern the new exceptions and proofs: records-only re-forms bypass mandatory checks; build-only bisection cannot attribute discovery or host failures; halving does not establish the claimed causal introduction; admission understates existing overlap and mishandles reservation lifetime; and the new receipt predicate does not explicitly bind the ordinary stage’s actual content.

Two new descriptions of existing code are also wrong: `--ci --watch` does not detach itself, and the release workflow does not run every gate on one exact committed tree.

### 3. Findings

1. **Blocking — the records-only re-form bypasses more than the declared service-suite deferral.**  
   `docs/2026-10-24-ci-scope-design.md:451` permits only four record checks after a records-only base move. That omits `universal`, `code-map` and the census check, although `:521` makes the cheap tier mandatory. Its path list also includes `.claude/knowledge/adoption.md`, which explicitly reaches `driver` as well as `service` (`tools/merge-branch.mjs:1129`). Today’s `recordMatcher` deliberately uses a much narrower whitelist (`:1842`).

   **Smallest change:** always rerun the mandatory cheap tier. Define a narrow records exception that respects earlier `REACH` rules; defer only explicitly named suite obligations, and record those deferrals. Prove the adoption-file and declaration-file cases.

2. **Blocking — discovery and host failures cannot be bisected by successful builds.**  
   `docs/2026-10-24-ci-scope-design.md:464` groups build, discovery and host failures together, then builds prefixes to find the failing member. A test adapter or test host can fail after compilation succeeded. Every prefix build may pass, leaving this procedure with no failing endpoint. The current code explicitly distinguishes missing test results from build success (`tools/merge-branch.mjs:2041`).

   **Smallest change:** use prefix builds only for compilation failures. Reproduce discovery/host failures with the operation and execution context that failed, on the base and prefixes; otherwise retain an unresolved outcome. Add separate discovery and host-failure proofs to row e.

3. **Blocking — halving finds a transition, not necessarily this regression’s introduction.**  
   `docs/2026-10-24-ci-scope-design.md:347` uses halving to identify the introducing commit; `:470` expressly allows non-monotonic prefixes. Consider outcomes `PASS, FAIL, FAIL, PASS, FAIL`, with the same fingerprint reintroduced at the last commit. Halving can return the first transition, although that failure was subsequently fixed. The recall replay can then credit the earlier commit’s correct reach while missing the later introduction. Queue attribution can likewise hand back a member whose defect a later member already repaired.

   **Smallest change:** distinguish an adjacent failing boundary from causal attribution. Match the particular failure throughout, verify the relevant transitions, and classify ambiguous histories as unsettled or interacting groups. For eight queue members, checking the remaining prefixes is bounded. Add fix-then-reintroduce and changing-fingerprint fixtures to rows c and e.

4. **Non-blocking — the reviewed queue request is not explicitly immutable.**  
   `docs/2026-10-24-ci-scope-design.md:422`, `:434` and `:495` retain branches and messages, but do not require capturing the reviewed source tip and message bytes at enqueue. Re-formation later merges “the same branches,” whose refs can have advanced. Today’s batch at least records branch tips (`tools/merge-branch.mjs:4452`).

   **Smallest change:** store the source commit ID and message content with each request, use those immutable inputs for every re-form, and require requeueing to change them. Prove a branch and message file changing while queued.

5. **Blocking — the one-background-gate overlap bound contradicts the retained scheduler.**  
   `docs/2026-10-24-ci-scope-design.md:661` promises overlap with at most one already-running background gate. Today `laneSchedule` expressly allows a git half and a rehearsal simultaneously (`tools/merge-branch.mjs:628`); round 3 retains that pairing. Both can already be running when the scoped tier arrives. Row f can increase the number further. Row e’s proof at design `:913` tests only one running half.

   **Smallest change:** either enforce one background Process-lane gate globally, or state the actual permitted simultaneous set and prove scoped execution beside that maximum. Keep the foreground independent of background completion.

6. **Blocking — memory reservations use the wrong accounting unit and can outlive their holder.**  
   `docs/2026-10-24-ci-scope-design.md:665` subtracts a holder’s working set from its reservation when accounting against free memory. MEMGATE1 checks both free physical memory and free commit (`tools/merge-branch.mjs:2284`); working set cannot represent both. A process can retain committed memory while pages leave its working set.

   The same paragraph frees a reservation when its holder process disappears. This repository already records that descendants can survive a killed runner, requiring explicit orphan cleanup (`tools/merge-branch.mjs:4628`, D180’s CIPRIO1d/e notes). Releasing on holder death alone can admit another gate while the old gate still runs.

   **Smallest change:** specify separate physical/commit accounting and track the full owned process group. Release only after its descendants have ended or their live usage remains charged. Test unequal working-set/commit usage and a killed holder with surviving children.

7. **Non-blocking — “no wait” and guaranteed nightly progress overstate the admission policy.**  
   `docs/2026-10-24-ci-scope-design.md:661` says the scoped run’s worst background wait is none, but `:670` retains its ten-minute memory wait. Background reservations can contribute to that wait. Conversely, the 60-minute exception at `:671` only relaxes Process-lane priority; it does not override the background’s indefinite memory wait. A night therefore does not necessarily finish under sustained foreground load.

   **Smallest change:** distinguish Process-slot waiting from memory waiting, include the latter in latency, and make progress conditional on memory admission or define a fair memory-admission policy. Extend the sustained-load proof accordingly.

8. **Blocking — the receipt must be bound to the actual tree being staged.**  
   `docs/2026-10-24-ci-scope-design.md:723` requires a receipt on the exact commit, then standing gate passes. It does not explicitly require the receipt’s tree, that commit’s tree and the publisher’s current content tree to agree. This matters for plain `--stage`: today `--passed` deliberately judges uncommitted content through `contentTree`, rather than requiring a clean commit (`tools/merge-branch.mjs:4311,4326`; `tools/desktop-publish.mjs:1163`).

   Merely finding a receipt for HEAD can therefore be combined with later scoped evidence for different checkout content.

   **Smallest change:** require equality between the requested commit tree, receipt tree and actual build-source tree at every ordinary stage entry point. Refuse dirty content and preserve the source through the build. Add a dirty-checkout case with a valid HEAD receipt and otherwise passing scoped verdicts.

9. **Non-blocking — clearing known red on “any run” loses causal ordering.**  
   `docs/2026-10-24-ci-scope-design.md:702` lets any passing run clear a failure. An older background run finishing late, or a historical attribution run on a passing ancestor, can clear a newer failure on main. Rediscovery can then restart the expiry described at `:461`, undermining the deliberate 72-hour limit.

   **Smallest change:** clear only with qualifying evidence on the affected main lineage that supersedes the recorded failing tree. Historical/bisection attempts must not clear current known red; repeated sightings must preserve its original first-seen time. Prove out-of-order completion.

10. **Non-blocking — today’s watch does not detach itself.**  
    `docs/2026-10-24-ci-scope-design.md:436` describes the detached runner “as `--ci --watch` is.” `ciWatchVerb` runs and awaits its loop in the invoking process; `main` dispatches directly to it (`tools/merge-branch.mjs:5650,5723`).

    **Smallest change:** remove that analogy and specify the new runner’s detached launch, output destination and startup acknowledgement explicitly.

11. **Non-blocking — the release workflow does not gate one exact committed tree.**  
    `docs/2026-10-24-ci-scope-design.md:755` says every gate runs on the exact commit. The workflow’s devkit and desktop jobs check out the input revision, while the release job changes versions at `.github/workflows/release.yml:213`, resyncs doctrine at `:222`, then runs its gates before the eventual release commit.

    **Smallest change:** describe the existing multi-job, stamped-worktree verification accurately. Keep that workflow unchanged as intended; do not present it as a single exact-commit receipt.

12. **Non-blocking — row f’s proof requires concurrency its mechanism forbids.**  
    `docs/2026-10-24-ci-scope-design.md:921` permits two rehearsal reservations simultaneously, while `:922` requires five runs with all three rehearsals side by side. Those cannot be the same execution policy.

    **Smallest change:** choose the concurrency limit and make the proof, resource accounting and expected wall use it consistently. If the limit is two, prove that schedule with the third rehearsal waiting.

**12 findings; 6 blocking.**

**Verdict: another round.** Preserve the scoped-merge/background-install direction and the deleted stage waiver. Another read is needed after findings 1–3, 5–6 and 8 are resolved: they determine what must run, which member or commit actually introduced a failure, what load the two runners may create, and whether the staged content has the required background proof.

## Opus (foundation), round 3

The independent Opus 5.5 reader's check of round 3, at `22427ff95`: 12 findings, 2 blocking; accept after named changes the steward can check.

## CISCOPE1, round 3: the independent Opus foundation read (D191)

Read at `22427ff95` (round 3: `800ce9197` the design, `22427ff95` D207), against the code at `b0d68c641`. No tool, gate, lane or
decision file the design cites changed since `b61325e6c` (`git diff --stat b61325e6c HEAD` over them is empty), so round 2's line
numbers still hold. "design:N" is `docs/2026-10-24-ci-scope-design.md` line N, "mb:N" is `tools/merge-branch.mjs`, "dp:N" is
`tools/desktop-publish.mjs`. I did not open `local/`, so the figures drawn from `status.json` and the verdict stores are taken as
stated. Every code citation I checked holds.

### 1. The Opus round-2 findings

| # | Finding | Verdict | Where | Why |
|---|---|---|---|---|
| F1 | §5.3 point 4's waiver | Taken as named | design:723-728, 738-740, 950-951; D207:51-55, 87-88 | Deleted. Every gate of the plan must pass, as `stageJudgement` requires (mb:1926-1956). Known red waives only in the queue, on a base run. Holds. |
| F2 | One rule for a failure on the base | Taken as named, for a failing test | design:459-463, 686-699 | Inherited whether known red or not, filed at once, never pinned to a member; known red only lets the base run be skipped. Holds for a test. A failure that names no test, and a base failure that is not inherited, still have no rule (findings 1, 2). |
| F3 | A miss credited to a later scoped run | Taken as named | design:345-366, 392-406 | Each failure is judged by its introducing commit, found by running. The count is also fed from inherited failures. Direct commits are counted as *unscoped*, an unsettled range as missed (a lower bound), and before CISCOPE1e the count is structural, as it says. Holds (finding 11 is detail). |
| F4 | Sharing the machine: the converse case | Taken as named (first option); its worst case is misstated | design:651-676, 900-919 | A scoped tier runs beside a background gate already running, and none starts while it runs or waits. Background progress is bounded at 60 minutes, and admission and memory reservation move to CISCOPE1e with a timed proof. But "worst wait none" and "never a second" are wrong (finding 3). |
| F5 | A failed run satisfies no sweep or night | Taken as named | design:638-644, 886-894 | A sweep is satisfied by a run that gave every class a verdict, a night by a finished whole run. Only a run ended before its classes ran leaves either due, as `classPlan` behaves today (mb:5176-5180). |
| F6 | Who commits; message and records | Taken as named | design:418-432, 451-457; §1 at design:80-84 | `--preview`, a `--message` file, and a records commit on the branch. The tool commits only what it is handed plus what it writes into every merge today (mb:69 and :4517 cited correctly). A records-only re-form runs only the record checks. Holds; see findings 5 and 8 for consequences. |
| F7 | Bisecting tests alone | Taken as named | design:468-478 | The whole class or file runs on the base and each prefix, members one at a time as the fallback, and no unit verdict is written. Holds; finding 4 is about its unresolved branch. |
| F8 | The runner and conflicts | Taken as named | design:436-444 | A detached runner, as `--ci --watch` is. One conflict rule: set aside and handed back, never left open in the scope tree. |
| F9 | §4.4 omits fast halves and carry-over | Taken, incomplete | design:587-600 | Fast halves and re-forms are added. But `service` is priced at core's 1 m 38 s alone, and the records commit every member carries puts `service` in nearly every candidate (finding 8). The carry-over says "that reaches them", which disagrees with §4.2's basis (finding 2). |
| F10 | Which verdicts a scoped run writes | Taken as named | design:553-561, 720-728 | A gate verdict only for a gate run whole, or a Process half with nothing deferred. Every verdict carries a run kind and the background counts them as standing. "Passed" means a receipt plus standing passes of either kind. |
| F11 | The 72-hour expiry | Taken; its recorded scope is wrong | design:703-707; D207:92-94 | Recorded as a deliberate exception, with a skip that names its row. But under §4.2's basis a failing unit runs in every candidate, and a red candidate that pins no member has no outcome. So expiry stops the whole queue, not only the merges that reach the test (finding 2). §3.4 does not list the skip check (finding 12). |
| F12 | Two wrong claims | Taken | design:103-107, 762-764 | Both corrected. `MODULES_READERS` (mb:974) holds no family rehearsal, and the watch owes the two rehearsals (mb:500, :5456). |
| F13 | D207 still round 1 | Taken | D207:18-107 | Rewritten to round 3. It matches the design except for the slips in findings 2, 6, 10 and 12. |
| F14 | The public release has no row | Taken (second option) | design:633-636, 755-756 | The release workflow's own run is the release's gate. Checked: `release.yml` runs every declared gate across its jobs. |
| F15 | Row 2's replay | Answered; the answer holds | design:873-877 | `DriverHostTests` carries `[Trait(Category.Name, Category.Process)]`, so a `driver-process` failure fits. A git half whose pass went stale by reach is the case the narrowed rule composes. I did not read the `status.json` it rests on. |
| F16 | Two gaps in what the rows run | Taken; overshoots | design:530-534, 878-882 | Checked: verify names 12 of the 16 files (`package.json:15`). "Every" `tools/*.test.mjs` adds four that no gate runs (finding 9). Row 3 now says it adds `check`. |
| F17 | The proof's sources do not span its month | Taken as named | design:378-384 | Each source's span and count is stated, and recall over the FIX-LOG's failures is given apart. |

### 2. Codex's round-2 blocking findings

| # | Finding | Verdict | Where | Why |
|---|---|---|---|---|
| 3 | Bisection does not cover the scoped run's failures | Taken, incomplete | design:458-483 | Added: build failures handled apart, bisection by class or file, *pinned with*, independent failures side by side, *unresolved*, the retained candidate judged again, the three-step claim limited. Still open: building prefixes cannot reproduce a check, typecheck or host failure, and a base failing with another fingerprint breaks halving's premise (findings 1, 2, 4). |
| 4 | Recovery across promotion; conflict ownership | Taken as named | design:448-450, 495-505 | `promoting` is written before the fast-forward, and a restart checks ancestry first, as `resume` does (mb:4505). `--ff-only` itself refuses a main that moved. One conflict rule. Holds. |
| 6 | Known-red eligibility; fingerprint collisions | Taken as named | design:686-701, 892-896 | One rule. The fingerprint keeps the theory case and the assertion's location, which today's readers drop (mb:2023, :2119). Row 4 names the three proofs. |
| 7 | Selection in a range used as coverage | Taken as named | design:345-366, 392-403 | Each failure is judged by its introducing commit. Only the promoted candidate's run is read, the six verdicts are counted apart, and one regression is counted once. |
| 8 | The proof lacks scheduling inputs | Taken as named | design:370-387 | The selector is pinned and run on historical trees through `repository(root, commit)` (`process-reach.mjs:819`, checked). Only structure is replayed: the scheduling state is declared unknown and proven by fixtures, and the first results are kept. |
| 9 | `staged.commit` is not a durable baseline | Taken; its use removed | design:742-747 | The stage history is a file of its own. With the waiver gone no judgement reads it, and unknown ranges are reported. |
| 10 | The stage waiver trusts the census | Taken | design:728, 738-740 | The stage waives nothing. |
| 11 | `sweptOut` does not enforce overdue | Taken as named | design:729-731 | The stage reads the due record and ages a whole pass that counts only by standing. Confirmed that a whole pass never expires today (mb:1909). |
| 12 | Stage evidence and every consumer | Taken; one question open | design:714-728, 555 | A receipt, a run kind on every verdict, and the rules in `--passed`, which both `ungatedRefusal` call sites reach (dp:1127, :1163). Open: which kinds' receipts count, and a receipt from a run that ran nothing (finding 6). |
| 13 | One-sided admission | Taken | design:651-676 | Symmetric admission with a progress bound; its worst case is misstated (finding 3). |
| 15 | Memory admission after cross-run execution | Taken as named | design:665-674, 900-906 | Admission and the reservation land in CISCOPE1e; pairing stays in CISCOPE1f. |

Codex's non-blocking 1, 2, 5 and 14 are taken. Number 2 carries finding 8's slips, and number 14 leaves a rejection unanswered (finding 10).

### 3. What round 3 introduced

**Claims checked against the code.** These hold: `BLOCKING` :933, `PATH_CHECKS` :947, the reader lists :969-976, `REACH` :989 with
the records rules :1234 and :1249 and `docs/index/**` reaching no gate (:1244), `addVerdicts` :1758 (400 kept), `SWEEP_BOUND_MS` :1898,
`sweptOut` :1908-1909, `stageJudgement` :1926-1956, :2023 and :2119, :2044, `MEMORY_NEEDS` :2208, `memoryGate` :2331 and :2365,
`mergeInProgress` :2572, the prune :3106, :3559, :4069, `mergeNext` :4431 and :4454, `resume` :4505 and :4507, :4517, `ciPlan`
:5109, `classPlan` :5134 and :5175-5181, `gateCi` :5388, :5456, :5486 and :5536. In the publisher, dp:871, :1002, :1020, :1137, :1154
and :1163. Also `process-reach.mjs:819`. `b61325e6c` adds records beyond its second parent: `TASKS.md`, the reads file,
`docs/README.md` and the decisions index.

**Wrong claims.**
- §5.1's "never a second" and "worst wait none" (design:661-664; finding 3).
- §4.4's 1 m 38 s for `service` (design:589; finding 8).
- "Every candidate that reaches it" (design:489, 597-598, 704-705; D207:93; finding 2).
- "Every `tools/*.test.mjs`" (design:531; finding 9).
- A verdict kind `release` that nothing local writes (design:555; finding 6).
- D207's "8 to 20" (finding 12).

**New holes.**
- In the queue's attribution (§10 question 1):
  - a failing gate that names no test (finding 1);
  - a failure the base shares but does not inherit (finding 2);
  - the unresolved path and a flake mid-halving (finding 4).
- In promotion: it moves the parent's checkout, and nothing guarantees progress under a stream of direct commits (finding 5).
- In the stage's receipt (finding 6).
- In the gate store's cap once scoped runs write to it (finding 7).

**D115, D180, CIFAST1.**
- D115's full set before a stage holds, and GATESLIM1's line changes as §9 says.
- D180 point 4 and its rejection of letting an install out without a green run (D180.md:52) hold, stricter.
- Two items D180 rejected are reversed without being named: "every reached gate before every merge" (D180.md:49) and CIFAST1's "the rehearsals in a nightly or every Nth run" (D180.md:573). See finding 10.
- No other contradiction.

**The rows.**
- The order holds: no backstop goes before its replacement, admission comes with the second runner, and the stage only tightens.
- Row 4's proof item "an unfiled failure on the base filed at once" needs row 5's base run.
- Row 5's proof lacks the cases of findings 1 and 2.
- With those added, each proof proves its row's claim.

**D207.** The body and the Changes paragraph match the design, except for:
- the expiry's scope (D207:92-94);
- which receipt counts (D207:51-52);
- the low end of the page/CLI/tools wall (D207:40-41);
- the two reversed rejections, which Changes does not name.

**The §10 questions, briefly.**
1. Not every failure is placed (findings 1, 2), and a flake can hand back an innocent member (finding 4).
2. Yes, workable. Deferring `service` on a records-only re-form is acceptable, since the install waits for the background run. But each member's own records commit reaches `service` anyway (finding 8), and promotion moves the parent's checkout (finding 5).
3. Yes. Running beside a background gate is right; short class groups are not available, since a half is one `dotnet test` invocation (mb:5193). Its true worst case still needs stating (finding 3).
4. Yes, provided the stage's own `--ci --stage` run counts as a receipt and a sweep-only run does not, and the text says plainly that a run which ran nothing, because every verdict stood, is a receipt. The real guarantee is §4.2's writing rule: a scoped gate verdict exists only for a gate run whole, so every gate passing means nothing deferred is outstanding.
5. Yes, once the text says where a range starts when no pass was recorded (finding 11).

### 4. Findings

1. **Blocking. A failing gate that names no test has no rule on the base, and building prefixes cannot reproduce it.**
   - §4.1 point 4 has rules for two kinds of failure (design:458-478):
     - a failing test: a base run compared by fingerprint, then bisection by class or file;
     - a build, discovery or host failure: the base is built first, then the prefixes are built.
   - The cheap tier's gates (design:521-525) fail with no test and no fingerprint, and fit neither rule. They are `universal`, `code-map`, the census check, `daoris check`, the documentation tools, `release-prep --check`, `web-types` and the CLI's typecheck.
   - The likeliest case is on the base. The parent makes 10 to 94 direct commits a day, mostly records (design:256), and no gate runs on them. Two examples:
     - a direct docs commit that breaks a documentation check (budgets, duplicates);
     - a records commit that closes a row a skip still names, which fails the census check (design:706-707).

     Either fails the cheap tier of every later candidate. Nothing re-runs that gate on the base, and halving's premise "the base passes it" (design:470) is false. A records-only re-form runs exactly these checks (design:453-455), so the candidate in flight turns red through no member's fault.
   - A host failure (a run that "failed outside a test", mb:2044) and a discovery failure are not reproduced by building the prefixes either (design:466).
   - **Smallest change:** one rule for any failing gate that names no failing test:
     - run that gate, with the same selection, on the candidate's base first;
     - if it fails there, the queue stops and says so, as for a base that does not build (design:465), and the failure is filed for the parent;
     - if it passes there, bisect by running that gate's selection over the prefixes, not by building alone.

     Add to row 5's proof: a direct docs commit that breaks a documentation check before a candidate forms, a member that breaks `code-map`, and a host failure on one prefix.

2. **Blocking. A failure the base shares but does not inherit has no bisection and no queue outcome, and the expiry's recorded scope is wrong.**
   - §4.2's basis runs every unit with no pass standing, and "a failure runs until it passes" (design:517-518).
     - A fast half is "reached" in the sense today's CI uses, and today's CI runs any gate whose standing verdict is a failure (`ciPlan`, mb:5109-5114).
     - So a known-red unit runs in every candidate, not only in "every candidate that reaches it" (design:489, 597-598, 704-705; D207:93).
   - At expiry the test fails on every candidate's base with the same fingerprint and is no longer inherited, so every candidate is red. Two things stop the queue:
     - halving needs the base to pass (design:470), so no member is pinned;
     - "Nothing is handed back on a failure that cannot be placed" (design:476-477), so the head candidate is held and every later merge waits behind it.

     The deliberate exception D207 records is therefore "every merge stops", not "every merge near it".
   - The same broken premise applies to a changed fingerprint on a base that fails. It counts against the candidate (design:700-701), but pass/fail halving finds no transition when the base also fails.
   - **Smallest change:**
     - (a) Halve on "fails with the candidate's fingerprint" rather than on pass/fail. That pins a member for a changed fingerprint.
     - (b) State the outcome for a red candidate whose failure the base shares without inheriting it. Either the queue stops until the test is fixed or skipped under its row (said by `--queue`, and recorded so in D207's exception), or the members whose paths reach the test's gate by `REACH` are handed back with the reason and the rest go on.
     - (c) Make §4.2, §4.4, §5.2 and D207:93 agree on whether a candidate runs a known-red unit that none of its members reaches.
     - (d) Add row-5 proofs: a candidate meeting an expired known red, and a changed fingerprint on a failing base.

3. **Not blocking. §5.1 misstates the scoped run's worst case.**
   - "Never a second, since none starts while it runs or waits" (design:661-664) does not hold. The background already runs a rehearsal beside its git half (`BESIDE_GIT_HALF`, mb:490), and a stage or a night runs the family and deployment rehearsals there. So a scoped tier can meet the git half and a rehearsal together: 8 + 10 GB (`MEMORY_NEEDS`, mb:2208).
   - "Worst wait none" does not hold either. A scoped heavy gate still waits for memory up to MEMGATE1's bound of ten minutes (`MEMORY_WAIT_MS`, mb:2212, :2365), which design:669-670 keeps.
   - The 14 GB of headroom covers one Process half and one fast half. The driver row runs a Process half beside two or three fast halves (design:592).
   - The scope runner's priority is not stated; the background's is CIPRIO1's.
   - **Smallest change:**
     - state the worst case as the git half plus one rehearsal;
     - state the wait as MEMGATE1's bound per heavy gate when headroom is short;
     - size the headroom to the scoped run's peak, or say that it is not;
     - name the scope runner's priority;
     - time row 5's proof beside a stage's or a night's git half with a rehearsal beside it.

4. **Not blocking. The unresolved path can retry a failure, and a flake mid-halving can hand back an innocent member.**
   - A class that does not fail again on the whole candidate is "run once more as its gate's selection" (design:474-477). The text does not say what happens if that run passes. Promoting on it would be a retry of a fast .NET or Node failure, which §4.2 keeps out (design:550-551; mb:3559; D180).
   - Each halving step runs a prefix once. A flaky class, or a FLAKE1 re-run beside a background half (design:659-660), can pin a member that did nothing.
   - **Smallest change:**
     - a pass on any of these re-runs never makes a candidate green; the candidate goes one member at a time, and the failure is recorded as a flake for its row;
     - before a pinned member is handed back, run the two adjacent prefixes once more.

5. **Not blocking. Promotion moves the parent's checkout, and direct commits can keep re-forming the candidate.**
   - A detached process fast-forwards the main checkout at any moment (design:448-450).
     - Git refuses only when the checkout's own changes overlap the candidate's. Otherwise files change under the parent's own runs and edits there.
     - Every member carries a records commit (design:425-427), so an uncommitted `TASKS.md` in the checkout refuses most promotions, holding green candidates until `--continue`.
   - Each direct code commit re-forms the candidate in flight (design:450-451). Nothing guarantees that the queue makes progress under a stream of them.
   - **Smallest change:**
     - say that promotion moves the parent's checkout, or promote only onto a clean checkout and otherwise hold the candidate for `--continue`;
     - add a progress rule, or show in the 2026-10-21 replay that its 41 direct commits never starve a candidate.

6. **Not blocking. The receipt does not say which runs count.**
   - "A receipt of a run that finished on that exact commit" (design:723) does not name the run kinds whose receipt counts.
     - If `--ci --stage <commit>` counts, any commit can be staged.
     - If only ordinary and nightly runs count, only commits the coalescing watch happened to gate can be.
     - A sweep-only run (design:630) ran a single half.
   - `--full --beside`, which `docs/development.md` keeps as "the run of every gate", writes verdicts without a kind or a receipt. So do `--full`, `--rerun` and `--stale`, which are GATE6b's remedies.
   - There are two vocabularies:
     - verdict kinds: `scoped`, `background`, `stage`, `release` (design:555);
     - run kinds in the matrix: ordinary, sweep, nightly, stage, release (design:627-633).

     Nothing local writes `release` once `--ci --release` is dropped (design:635).
   - `--passed` judges the checkout's content tree (mb:4326-4329), and every publish asks it, staged or not (dp:1163). A dirty checkout has no "exact commit".
   - `local/ci.json` already keeps the newest 50 runs with their commit and state (mb:5062-5069, :5351).
   - **Smallest change:**
     - name the kinds whose receipt counts (ordinary, nightly, stage, `--full --beside`; not a sweep-only run) and the kind each verdict producer writes, and drop `release`;
     - match a receipt by tree;
     - say that a receipt may be of a run that ran nothing because every verdict stood;
     - say whether `local/ci-runs.jsonl` replaces or extends `local/ci.json`.

7. **Not blocking. The gate store's 400-verdict cap now takes every candidate's verdicts.**
   - The store keeps 400 verdicts, dropping the oldest first (mb:1758-1761). That is about a day and a half today (design:224-226).
   - Under §4.2, every candidate run, re-form and one-member candidate writes gate verdicts for the cheap tier and for each fast half it runs whole (design:556-557).
   - At tens of runs a day this can evict a night's and a stage's passes (the rehearsals, `web`, a whole git half) within a day. That is never looser, but a later stage re-runs what it already had.
   - **Smallest change:** keep gate verdicts per gate (the newest N each) or by age, and show in CISCOPE1e's replay that a night's verdicts survive a working day.

8. **Not blocking. §4.4 under-prices `service` and leaves it out of most rows.**
   - design:589 prices `service` at "about 1 m 38 s of tests". The gate runs `src/Daoris.Service` (`daoris.gates.json:8`), whose `Daoris.Service.slnx` includes the HTTP host's tests, 3 m 17 s more (design:211).
   - Every member now carries a records commit: the archive, `TASKS.md`, a FIX-LOG entry (design:425-427). `TASKS.md` is in the records lane, which reaches `service` (mb:1234), and `docs/**` reaches it as well (mb:1249). So nearly every candidate runs `service` whole, and the page, CLI and driver rows omit it.
   - **Smallest change:** add `service` to every row that carries a records commit, and reprice.

9. **Not blocking. "Every `tools/*.test.mjs`" adds four files that no gate runs.**
   - design:530-531 runs every `tools/*.test.mjs` file. Four are in no gate, and their headers say they are kept out of verify on purpose:
     - `as-merged`, which builds and runs the devkit;
     - `knowledge-bench`;
     - `question-bench`;
     - `ux-count`, which runs Playwright's chromium.
   - A scoped failure in them has no background run to backstop or clear it, and `ux-count` brings a browser into the scoped run, which §4.2 point 6 keeps out.
   - **Smallest change:** limit point 3 to the twelve files verify runs, or name the gate the four join.

10. **Not blocking. D207 reverses two items D180 rejected without naming them.**
    - CIFAST1's note rejects "the rehearsals in a nightly or every Nth run" (D180.md:573). §5.1's night runs them (design:631), and §9 (design:947-948) calls this an amendment without answering the rejection. An answer exists: the night runs in addition to the stage's payment, not instead of it, and the owner has now asked for a background schedule.
    - D180 rejects "every reached gate before every merge" (D180.md:49). The scoped run comes close to it. The design answers it in effect, through the separate tree and the queue (design:412-416, 485-493), but never names the rejection.
    - **Smallest change:** name both rejected items, with those answers, in §9 and in D207's Changes. The D180 notes written on acceptance then append to the record rather than silently reverse it.

11. **Not blocking. Four details of the recall measure.**
    - The introducing commit is searched for "between its last pass and its first failure" (design:347-349). For the month's FIX-LOG failures no last pass is recorded, and "no recorded pass" is not among the unsettled reasons (design:350-351).
    - Running a class over historical commits means building old trees, and installing old lockfiles for the TypeScript suites. The proof's machine cost is not priced, unlike the coverage pass's (design:822-825).
    - The running count reads "the scoped run of the candidate that reached main" (design:396-397). A re-formed candidate has several runs, and the later ones run only what the direct commit reached.
    - Nothing says what recall figure would stop CISCOPE1e.
    - **Smallest change:**
      - state where a range starts without a recorded pass, and count it as unsettled otherwise;
      - price the proof;
      - read every run of a re-formed candidate;
      - name the decision the lower bound feeds.

12. **Not blocking. Small slips in the records.**
    - `TASKS.md:503`: the CISCOPE1 row still says the scoped run is what "a merge and a republish wait on". That contradicts the steward's direction and §5.3.
    - `TASKS.md:504`: the TESTVALUE1 row says "CISCOPE1f measures first" and "under 40 minutes". The design has CISCOPE1g measure, and a target of about 28 class-minutes set from its report (design:849-853, 924-929).
    - D207:40-41 says "8 to 20" for the page, CLI or tools. §4.4's CLI row starts at 7 (design:591).
    - §3.4's checks (design:319-337) do not include the skip check that §5.2 (design:706-707) and D207:24 assign to the census.
    - Row 4's proof item "an unfiled failure on the base filed at once" (design:895-896) needs row 5's base run, so it belongs to row 5.
    - **Smallest change:** correct each line.

**12 findings, 2 blocking.**

**Verdict: accept after named changes the steward can check without another read.** Round 3 takes all six of round 2's
blocking changes as named, and I checked each against the text and the code. The foundation holds. A merge waits on a scoped run in
a tree of its own, which is strictly more than today's wait on `BLOCKING` alone. The install waits for the background run with no
waiver, through today's `stageJudgement`, `sweptOut`, the due record and CIFAST1's paid rehearsals. Admission arrives with the second
runner. Recall is judged causally, as a stated lower bound. What remains are local rules in §4.1 point 4 and §5.2, with their D207
sentence and their row-5 proof cases:
- a failing gate that names no test is run on the base first, and stops the queue if it fails there (finding 1);
- halving compares fingerprints, and the queue states its outcome when the base shares a failure it does not inherit, with the expiry's true scope recorded in D207 (finding 2).

Findings 3 to 12 are statements and corrections the steward can check line by line. Codex's read of round 3 is a separate matter.

## Codex, round 4

Codex's read of round 4, at `e89f00ca4`: 8 findings, 3 blocking; another round.

### 1. Each round-3 finding

`design` means `docs/2026-10-24-ci-scope-design.md`; `merge` means `tools/merge-branch.mjs`. **Taken** judges the proposed contract against the existing code, not completed implementation. Private run records were not inspected.

| Finding | Verdict | Where | One line of why |
|---|---|---|---|
| Codex 1 — records-only re-form bypasses mandatory checks | Taken | design:515–523, 1104–1106; merge:989, 1129 | The whole cheap tier returns, earlier reach rules win, and only `service` is explicitly deferred; finding 6 corrects the description of `recordMatcher`. |
| Codex 2 — builds cannot bisect discovery/host failures | Taken | design:546–556, 1099–1101; merge:2041 | Attribution now repeats the failing operation and selection, with builds alone limited to compilation failures. |
| Codex 3 — transition mistaken for introduction | Open | design:385–405, 568–580 | Every queue prefix fixes the bounded queue case, but unprobed historical stretches still permit false causal credit: finding 1. |
| Codex 4 — mutable queue request | Taken | design:488–493, 1106–1107; merge:4452 | Source commit and message bytes are frozen and reused, with a mutation fixture specified. |
| Codex 5 — understated background overlap | Taken | design:788–798, 1110–1126; merge:628 | The git half plus one rehearsal, later two, is explicitly admitted and tested beside scoped work. |
| Codex 6 — reservation accounting and surviving children | Open | design:801–812; merge:4628; `tools/processes.mjs:169,189` | Separate physical/commit accounting is specified, but the cited cleanup cannot identify the complete surviving group: finding 3. |
| Codex 7 — overstated no-wait/progress guarantee | Taken | design:795–798, 817–821; merge:2365 | Process-slot waiting is separated from memory waiting, and background completion is conditional on memory admission. |
| Codex 8 — receipt not bound to staged source | Open | design:889–896, 1085–1087 | Initial dirtiness is rejected, but endpoint tree checks do not preserve the source throughout the build: finding 2. |
| Codex 9 — clearing loses causal order | Taken | design:849–857, 1078–1081 | Clearing requires superseding main-line evidence; attribution runs cannot clear, and contradicted clearing preserves first sighting. |
| Codex 10 — watch falsely described as detached | Taken | design:495–501; merge:5645, 5723 | The analogy is withdrawn and the new launch, log and acknowledgement are specified independently. |
| Codex 11 — release does not verify one exact committed tree | Taken | design:927–933; `.github/workflows/release.yml:213,222` | The description now distinguishes input-revision jobs from the stamped release worktree. |
| Codex 12 — pairing proof contradicts concurrency limit | Taken | design:970–980, 1123–1127 | Mechanism and proof both allow two rehearsals, with the third waiting. |
| Opus 1 — no-test failure has no base rule | Taken | design:546–556, 1098–1101; merge:2041–2044 | The same gate operation runs on the base first; matching base failure stops the queue and is filed. |
| Opus 2 — non-inherited base failure and expiry scope | Open | design:557–567, 629–632, 858–866 | Signature matching and the explicit queue stop are taken, but blanket known-red execution conflicts with deferred kinds: finding 4. |
| Opus 3 — scoped worst case understated | Taken | design:788–821, 1110–1126 | Overlap, per-gate memory waits, normal priority and nominal foreground headroom are explicit. |
| Opus 4 — attribution retries can green a candidate | Taken | design:581–595, 1101–1103 | Attribution passes confer no green verdict; adjacent prefixes repeat and unresolved batches fall back to individual candidates. |
| Opus 5 — promotion changes checkout; direct commits can starve | Answered with a reason that holds | design:524–535 | Promotion requires a clean checkout; direct-commit starvation is admitted and reported rather than falsely bounded. |
| Opus 6 — qualifying receipt kinds unspecified | Taken | design:669–683, 879–890 | Producers and qualifying receipts are named, sweep-only receipts excluded, and zero-work receipts explicitly rely on standing evidence. |
| Opus 7 — scoped writes evict background evidence | Taken | design:673–677, 1080–1082; merge:1758 | Retention is per gate, with scoped/remedy entries separated from background/stage/full entries. |
| Opus 8 — `service` cost and presence understated | Taken | design:709–724; `daoris.gates.json:8` | HTTP tests and records-induced service runs are included; the estimates remain explicitly unmeasured together. |
| Opus 9 — four intentionally excluded tool tests added | Taken | design:644–650; `package.json:15` | Selection is restricted to verify’s twelve files, and the four exclusions are named. |
| Opus 10 — reversed rejections unnamed | Taken | design:1142–1159; D207:130–136 | Both D180 rejections are named and answered without weakening the stage requirement. |
| Opus 11 — recall range, price, evidence chain and threshold | Open | design:380–439, 448–449 | Range bootstrap, evidence chains and threshold are supplied; the lower-bound calculation and pre-admission proof order remain defective: findings 1 and 5. |
| Opus 12 — slips in records and proof placement | Taken | design:366–368, 1088–1089; `TASKS.md:508–509`; D207:47 | Backlog wording, measurement row, estimates, skip check and base-run proof placement are corrected. |

### 2. What round 4 introduced

The queue’s bounded attribution procedure is substantially stronger: it uses signatures, repeats the actual failing operation, examines every prefix, confirms adjacent results, and rejudges retained members. Its explicit stops for broken base checks and expired inherited failures are understandable exceptions; neither makes a merge wait for background completion.

The remaining blocking gaps are the historical recall claim, preservation of publish inputs, and identification of reservation owners after process death. The scoped-set rules also need consistent treatment of deferred failures and carried debt. The install retains the required background receipt and no known-red waiver, but that receipt still permits standing Process evidence; the absolute claim that dependency misses cannot reach the install is therefore too strong.

The normal implementation order preserves the principal backstops. However, CISCOPE1c’s newly priced historical execution refers to admission that CISCOPE1e builds later, and the existing helper `recordMatcher` is assigned the wrong purpose.

### 3. Findings

1. **Blocking — the historical search still cannot establish its claimed lower bound.**

   **Where:** `docs/2026-10-24-ci-scope-design.md:385–405`, `:436–439`, `:1066–1068`; `docs/decisions/D207.md:25–31`.

   A long history can contain an earlier, correctly reached introduction, followed by a fix and an unreached reintroduction entirely between two probes. Every probe shows the fingerprint, so the search credits the earlier introduction as *selected*. Stating that residue does not make the result a lower bound: a real miss has entered its numerator. The 0.9 admission threshold can consequently pass on overstated evidence.

   **Smallest change:** inspect every remaining commit after the proposed boundary, or count the unresolved suffix as *unsettled* unless every possible introducing change would receive the same classification. Add a fix/reintroduction fixture hidden wholly between probes. Reprice the search accordingly; checking stretches across a thousand commits also exceeds the provisional fourteen-runs-per-failure model.

2. **Blocking — two matching source snapshots do not bind the artifact to that source.**

   **Where:** `docs/2026-10-24-ci-scope-design.md:889–896`, `:1085–1087`; `tools/desktop-publish.mjs:1137`, `:1163`, `:1220`.

   Plain `--stage` builds from the checkout, through several sequential build and pack operations. A source can change after the initial check, be consumed, then be restored before the final check. Both content trees equal the receipted tree, while the staged artifact contains different or mixed content. Promotion can also change a clean main checkout during that build. The dirty-at-entry fixture does not exercise either case.

   **Smallest change:** build every ordinary stage from a dedicated worktree pinned to the receipted commit and held against movement through publication, as the existing gated-source path already does against CI movement. Add a controlled source-change-during-build case; the source must remain isolated or publication must refuse.

3. **Blocking — the cited orphan heuristics cannot implement complete process-group ownership.**

   **Where:** `docs/2026-10-24-ci-scope-design.md:807–812`, `:1114–1116`; `tools/merge-branch.mjs:4628–4634`; `tools/processes.mjs:169–204`.

   `endOrphans` finds children of the recorded PID. `endStranded` finds orphan roots whose command lines contain the tree path. A grandchild surviving an ended intermediate shell, with a relative command line that names no tree, satisfies neither search. A failed process query can also produce an empty result. These are cleanup heuristics, not proof that every member has ended; using their empty result can release the reservation while work survives.

   **Smallest change:** specify an owned process container whose membership survives intermediate-parent death, or another durable ownership mechanism that cannot lose those descendants. Unknown membership must retain its charge. Ensure scoped builds also use private build servers. Extend the proof to a vanished intermediate shell, a surviving descendant with no tree path, and an unreadable process query.

4. **Non-blocking — “every known-red unit” contradicts unconditional browser and rehearsal deferral.**

   **Where:** `docs/2026-10-24-ci-scope-design.md:629–632`, `:662–663`, `:858–866`; `docs/decisions/D207.md:37–38`.

   A background browser failure can become known red, yet the new blanket rule runs its unit in every candidate while point 6 says no scoped run runs a browser spec. Likewise, known-red git classes need a precedence rule against the budget. These choices also determine which expiry can stop the queue.

   **Smallest change:** define eligibility before applying known-red selection: browser specs and rehearsals remain deferred; state whether known-red git classes override or consume the budget. Apply the expiry language to that same eligible set and add one fixture for each deferred kind.

5. **Non-blocking — the historical proof consumes admission before its implementation exists.**

   **Where:** `docs/2026-10-24-ci-scope-design.md:430–435`, `:1058–1068`, `:1094–1096`, `:1128–1130`.

   CISCOPE1c must complete historical builds “at the background’s admission,” but CISCOPE1e introduces that admission and cannot start until c’s recall proof qualifies. CISCOPE1g can also land before e while scheduling background coverage work.

   **Smallest change:** give pre-e proof and coverage execution an explicit temporary rule using existing serialization and memory controls, or move the standalone admission prerequisite earlier. Do not require e to execute its own prerequisite proof.

6. **Non-blocking — `recordMatcher` is described as a merge-resolution policy.**

   **Where:** `docs/2026-10-24-ci-scope-design.md:522–523`; `tools/merge-branch.mjs:1842–1867`, `:1429`.

   `recordMatcher` determines which record differences standing verdicts forgive. Backlog resolution uses `resolveBacklog`/`mergeByBase`; union handling uses the attributes and join repair. The new sentence assigns the helper a responsibility it does not have.

   **Smallest change:** replace “decides what a merge may resolve” with “decides which record differences a standing verdict may forgive.”

7. **Non-blocking — background receipt provenance does not eliminate dependency-map exposure at installation.**

   **Where:** `docs/2026-10-24-ci-scope-design.md:699–702`, `:887–903`; `docs/decisions/D207.md:87–89`; `tools/merge-branch.mjs:1908–1912`, `:5109–5115`.

   An ordinary background run may retain a composed Process-half pass. A dependency missing from the class census can leave an affected class’s old pass standing, within the sweep bound. A finished background receipt plus those standing passes therefore does not prove the regression was exercised before installation. D180’s PROCREACH1 note explicitly acknowledges this within-day exposure.

   **Smallest change:** preserve the agreed receipt/standing contract but state its residual exposure accurately. If “no census miss reaches the install” is required, require a whole Process-half execution on the target tree before staging; the current receipt predicate does not establish it.

8. **Non-blocking — carried git-half debt is understated for unrelated later candidates.**

   **Where:** `docs/2026-10-24-ci-scope-design.md:605–609`, `:629–630`, `:725–728`; `tools/merge-branch.mjs:5134–5183`.

   The basis is every unit without a standing pass. After candidate A invalidates a git class and defers it, an unrelated candidate B still lacks a standing pass for that class. The statements that it runs again only in later candidates “that reach it” omit this accumulated debt and can underprice queue replay.

   **Smallest change:** describe carry-over by standing evidence, independently of B’s own changed paths. Require the replay to include a deferred driver candidate followed by unrelated records/page candidates before the background pays that debt.

**8 findings; 3 blocking.**

**Verdict: another round.** Preserve the scoped-merge/background-install direction, the deleted stage waiver, and the revised queue attribution. Another read is needed after the historical search produces a valid conservative measure, publication preserves its receipted source throughout the build, and reservation ownership survives intermediate-process death. The remaining five findings are local corrections the steward can check directly.

## Opus (foundation), round 4

The independent Opus 5.5 reader's check of round 4, at `e89f00ca4`: 10 findings, 3 blocking; accept after named changes.

## CISCOPE1, round 4: the independent Opus foundation read (D191)

Read at `e89f00ca4` (round 4: the design and D207), against the code at `a6991805f`. `git diff --stat` from `22427ff95` to
`a6991805f`, and from `a6991805f` to `e89f00ca4`, is empty over `tools/`, `daoris.gates.json`, `daoris.lanes.json`,
`package.json`, `.github/`, `docs/development.md`, D115 and D180. So round 3's line numbers hold, and round 4's claim that
nothing it cites changed is true. "design:N" is `docs/2026-10-24-ci-scope-design.md` line N, "mb:N" `tools/merge-branch.mjs`,
"dp:N" `tools/desktop-publish.mjs`, "D207:N" `docs/decisions/D207.md`. I did not open `local/`, so the figures drawn from the
run records are taken as stated.

### 1. Round 3's findings

#### The Opus foundation read, round 3

| # | Finding | Verdict | Where | Why |
|---|---|---|---|---|
| F1 | A failing gate that names no test has no rule on the base | Taken as named; holds, with one new hole in its stop | design:536-539, 545-556; row 5 design:1103-1106; D207:41-43 | The gate runs on the base first, with the same selection. The queue stops if it fails there; otherwise the gate's own selection runs over the prefixes. Prefix builds serve a compilation failure only. The three proofs are in row 5. A gate that died of memory now reaches this stop (finding 3). |
| F2 | A failure the base shares but does not inherit; the expiry's scope | Taken as named, (a) to (d); holds, with two qualifications | design:557-566, 629-633, 724-726, 847-866; D207:39, 45, 108-114 | Prefixes are judged by the candidate's signature, so a changed fingerprint on a failing base pins a member. An expired known red stops the queue, said by `--queue`. The alternative is rejected with a reason that holds. §4.2, §4.4, §5.2 and D207 agree. But "every candidate" is broader than §4.2's own deferrals allow (finding 7). And matching by signature breaks for a failure whose message varies between runs (finding 1). |
| F3 | §5.1 misstates the scoped run's worst case | Taken; holds for the Process lane | design:789-801, 812-815; row 5 design:1121-1122 | Stated: the git half and one rehearsal (8 + 10 GB), two rehearsals after CISCOPE1f; MEMGATE1's ten minutes per heavy gate; 26 GB of headroom; normal priority; a proof beside a stage's or a night's pair. Not stated: the background's light lane, which also runs beside a scoped tier. §4.4 also contradicts itself on the scoped run's own schedule (finding 4). Normal priority departs from D180's budget note, and that is not named (finding 5). |
| F4 | The unresolved path can retry; a flake can hand back an innocent | Taken as named; incomplete for a candidate of one | design:578-586 | A pass on a re-run never makes a candidate green, and the adjacent prefixes run again before a hand-back. A candidate that already has one member has no outcome (finding 2). |
| F5 | Promotion moves the parent's checkout; direct commits can starve | Taken as named; holds | design:524-534; row 5 design:1113, 1119-1120 | Promotion happens only onto a clean checkout: no tracked change and no merge in progress (`mergeInProgress`, mb:2572). Otherwise it is held and retried. Three re-forms are reported as *held*, and the replay reports re-forms. |
| F6 | The receipt does not say which runs count | Taken as named; holds | design:670-673, 879-889 | Ordinary, nightly, plain `--ci`, `--ci --stage` and `--full --beside` count; a sweep-only run does not. `release` is dropped. A receipt is matched by tree, and a run that ran nothing counts. `ci-runs.jsonl` extends `ci.json` (mb:5069, :5351). |
| F7 | The gate store's 400 cap | Taken as named; holds | design:673-677; row 4 design:1082-1083 | Kept per gate: 20 scoped-and-remedy verdicts apart from 20 of the other kinds (checked against mb:1758-1761). The working-day replay is in row 4. |
| F8 | §4.4 under-prices and omits `service` | Taken as named; holds | design:710-720; D207:49-50 | The HTTP host's 3 m 17 s is in. `service` is in every row, and D207 follows. The driver row's "beside the Process tier" rests on a schedule that §4.4 names otherwise (finding 4). |
| F9 | Four tool tests that no gate runs | Taken as named; holds | design:644-648; D207:37-38 | Checked: there are 16 `tools/*.test.mjs` files, and `package.json:15` names 12 of them. The four others are named and kept out. |
| F10 | Two reversed D180 rejections unnamed | Taken as named; holds | design:1144-1148, 1158-1161; D207:130-135 | Both are named, with the answers the read gave. |
| F11 | Four details of the recall measure | Taken; holds except the stretch search's recursion and price | design:381-393, 430-438, 449-450 | Done: the range start without a recorded pass (*no pass found*); the price, measured on the first ten; every run of a re-formed candidate read; the 0.9 decision. Not said: whether the stretch checks recurse. The price assumes short stretches (finding 9). |
| F12 | Small slips in the records | Taken; holds | TASKS.md:508-509; D207:49-50; design:366-368; row 4 design:1088-1089; row 5 design:1108-1109 | Each line is corrected. |

#### Codex, round 3: its six blocking findings

| # | Finding | Verdict | Where | Why |
|---|---|---|---|---|
| 1 | The records-only re-form bypasses more than the service deferral | Taken; holds | design:515-523, 696-697; row 5 design:1109-1110 | The whole cheap tier runs, and only `service` is deferred, recorded. `adoption.md` (mb:1129) and the declaration files (mb:989-997) are excluded. `recordMatcher` (mb:1842) is kept apart as a different question. Both cases are proven. The definition also admits the rules that name no gate, which is harmless (finding 10). |
| 2 | Discovery and host failures cannot be bisected by builds | Taken as named; holds | design:545-556; row 5 design:1104-1106 | A compilation failure is found by prefix builds. A discovery failure (mb:2043) or a host failure (mb:2044) is found by the gate's own selection, on the base and then over the prefixes. Each has its own proof. |
| 3 | Halving finds a transition, not the introduction | Taken; holds for the queue, and for recall once the search is stated (finding 9) | design:385-393, 567-579; rows 3 and 5 | The queue runs every prefix (at most seven runs) and hands back the member at the last transition, naming earlier ones as interacting. Recall runs a short range at every commit and probes a long one, and states the residue. Both rest on signature equality, which a varying message breaks (finding 1). |
| 5 | The one-background-gate bound contradicts the scheduler | Taken otherwise: the Process-lane set is stated; does not fully hold | design:789-794 | The git half and one rehearsal is today's Process-lane maximum (mb:490, :628-637), two rehearsals after CISCOPE1f. The background's light lane runs beside its git half, scheduled by memory (mb:623-647). It is not held while a scoped tier runs, and it is not in the stated set or in the proof. Finding 4, not blocking: that lane runs below normal priority and is admitted only above the headroom. |
| 6 | Reservations use the wrong unit and outlive their holder | Taken; holds for the units | design:802-811; row 5 design:1117-1119 | Physical memory and commit are charged apart (`readMemory`, mb:2284). A reservation is held until no process of its group lives. Both are proven. The group is found per run, not per gate (finding 8). |
| 8 | The receipt is not bound to the tree staged | Taken as named; holds | design:890-901; row 4 design:1085-1086 | The commit's, the receipt's and the build source's content trees must be one (`contentTree` mb:1819; `judgeCheckout` mb:4326-4329; `ungatedRefusal` dp:871, :1163). A dirty checkout is refused, and the source is read again after the build. Proven. |

Codex's non-blocking 4, 7, 9, 10, 11 and 12 are taken and hold (design:487-493, 795-799 with 816-820, 850-857, 495-500,
928-933, 970-977). On 10: the citation `:5723` is `main` itself; it dispatches the watch at mb:5743.

### 2. What round 4 introduced

**Claims checked against the code: these hold.**
- In the merge tool: `BESIDE_GIT_HALF` mb:490, `STAGE_REHEARSALS` :500, `laneSchedule` :628-657 (its `byMemory` at :623-647),
  `REACH`'s first rule :989-997, `adoption.md` :1129, the records lane :1234, `docs/index/**` :1244, `docs/**` :1249,
  `pathsReaching` :1645-1653, `addVerdicts` :1758-1761, `contentTree` :1819, `recordMatcher` :1842, `flakeDecision`
  :2041-2044, `MEMORY_NEEDS` :2208, `MEMORY_WAIT_MS` :2212, `readMemory` :2284, :2365, `judgeCheckout` :4326-4329, `state.tips`
  :4452, CIPRIO1d/e :4628-4634, `CI_RUNS_KEPT` :5069, `noteCiRun` :5351, `ciWatchVerb` :5645.
- In `tools/processes.mjs`: `endOrphans` :169 and `endStranded` :189.
- Elsewhere: `release.yml:213` and `:222`; `package.json:15`; `daoris.gates.json:8`.

**Wrong claims.**
- "FLAKE1's own re-run alone of a Process half's failures is the only retry, as today" (design:583-584). There are two more
  today. MEMGATE1 runs any gate that died of memory again once, whole (`againOfMemory`, mb:3744-3765; mb:270-276). LEFT1 runs a
  rehearsal that died again (mb:3540-3557). Finding 3.
- "The Process halves run one after another in the Process lane, as GATEORDER1 has them" (design:705-706). The driver row has
  `service` "in the light lane beside the Process tier" (design:713, 719-720). GATEORDER1 holds a Process half until the heavy
  light gates end (mb:290-291, :645-647); running them beside each other is CI1's `byMemory`. Finding 4.
- "The scoped tier's worst case is its whole Process tier beside that set" (design:794), where that set is the background's
  Process lane alone. Finding 4.
- "about ten runs over a month's thousand-odd first-parent commits" (design:430). `git log --first-parent` from 2026-09-24 to
  2026-10-24 at `a6991805f` counts 2,153; §2.7's own table counts 1,038 in ten days. Finding 9.
- "`ruleFor`, `:1645-1653`" (design:516): `ruleFor` is mb:1574; :1645 is `pathsReaching`. Finding 10.
- "23 to 30 minutes" for two rehearsals at once (design:976). By §2.1 and CIFAST1's 1 to 4 minutes for the release rehearsal,
  the longer of the family rehearsal (14 to 23) and the release then the deployment rehearsal (15 to 26) is about 16 to 26.
  Finding 10.

**New holes.**
- Signatures are matched for equality throughout (design:538-539). The fingerprint's message line is normalised only for
  numbers and paths (design:839-842). So a failure whose message carries a run-specific token is never the same twice.
  Finding 1.
- An unresolved failure on a candidate of one has no outcome (design:580-586). Finding 2.
- A death of memory meets the base-first stop (design:545-556 with design:796-797, 815). Finding 3.
- A reservation's process group is found by the run's tree (design:807-811). Finding 8.

**D115, D180, CIFAST1.**
- D115: GATESLIM1's line changes as §9 says; the full set before a stage holds, stricter. No contradiction.
- D180: point 4 and its rejection of letting an install out without a green run hold, stricter. The CI1 note's rejection of
  forgiving the records (D180.md:475) is respected: the records-only re-form runs the whole cheap tier. Two departures that
  round 4 makes explicit are not named:
  - the scoped runner at normal priority against the budget note's point 2, "a CI run is a guest on the machine ... A CI run
    beside a full slate of workers is not started" (D180.md:68-69, the harm at :60-62): finding 5;
  - the queue's stop when a gate fails on the base, a second deliberate exception to the blocking D180 removed, recorded
    nowhere as one: finding 6.
- CIFAST1: unchanged but for the nightly run, which is named with its rejection's answer.

**The rows.**
- The order holds:
  - no backstop goes before its replacement;
  - admission arrives with the second runner;
  - the stage only tightens;
  - row 4's skip check extends row 3's tool, after it.
- Row 5's proof names every round-4 rule but lacks four cases:
  - a failure whose message varies between runs (finding 1);
  - an unresolved failure on a candidate of one, and a prefix that does not build (finding 2);
  - a death of memory on the candidate and its base (finding 3);
  - the background's light lane beside a scoped tier (finding 4).
- Row 6's proof matches the two-at-once limit.
- With those cases added, each proof proves its row's claim.

**D207.** Its body and Changes match the design, with these exceptions:
- D207:47 says a re-form "whose paths reach no gate but `service`". By `pathsReaching` no path satisfies that: every path
  reaches the baseline gates (mb:1648). The design means each path's own rule (finding 10).
- D207:39 and :110 carry §4.2's over-broad "every candidate" (finding 7).
- Changes omits the two D180 departures above (findings 5 and 6).

**The §10 questions, briefly.**
1. Not yet. Three failures are still placed wrongly or not at all:
   - a reproducible failure whose message varies between runs (finding 1);
   - an unresolved failure on a candidate of one (finding 2);
   - a death of memory, which can stop the queue or pin a member (finding 3).

   The rest are placed.
2. Yes. Stopping the queue is the right exception. A known-red unit the scoped run runs, it runs in every candidate. Handing
   back the members that reach it would hand back members that did nothing, and one direct commit ends the stop. It should be
   recorded beside the base-failing gate's stop, as an exception of the same scope (finding 6).
3. For memory, yes. It is reserved in both measures and released after the group ends, and the background is admitted only
   above 26 GB. The stated maximum leaves out the background's light lane (finding 4), and the per-gate group is found per run
   (finding 8).
4. Yes. The three trees made one, the dirty checkout refused and the source read again after the build close the gap. The
   named receipt kinds settle which runs count.
5. Yes, with two changes:
   - the search must say it probes recursively until every stretch is sixteen commits or fewer, and price that (finding 9);
   - the fingerprint must survive a varying message (finding 1). Otherwise a reproducible failure reads as *does not
     reproduce*, which is unsettled and counted as missed.

### 3. Findings

1. **Blocking. A failure whose message varies between runs is never matched. It is unresolved even when it reproduces, and
   it is never inherited from the base.**
   - Round 4 judges every run by signature equality: the base run, the run that checks the failure reproduces on the whole
     candidate, each prefix, and the adjacent re-runs (design:538-539, 580-581).
     - A test's fingerprint normalises only numbers and paths in its message line. A failure whose location cannot be read
       keeps its whole message, unnormalised (design:839-842).
     - A gate's signature takes the first error line, normalised the same way (design:537-538).
   - Some messages carry a token that each run makes fresh, so they change every run. Examples: a GUID, a session or tree id
     such as `s-a2762239`, a hash, a set printed in hash order. The first line of a vitest or chai assertion also carries the
     actual value. Three cases go wrong:
     - **A member breaks such a test reproducibly.** The reproduce run fails "without its signature", so the failure is
       *unresolved*. Its candidate is split into one member at a time, and the guilty member is never pinned. With finding 2,
       it is never placed at all.
     - **Main is red with such a test.** The base fails with another fingerprint, so the failure is not inherited and counts
       against every candidate. Every candidate goes unresolved, the queue promotes nothing, and the expiry never applies.
     - **Recall.** The introducing-commit search finds no commit "with this fingerprint", so the failure *does not
       reproduce*. It is unsettled and counted as missed (design:392-393, 403). That lowers, for a reason of method, the bound
       that decides whether CISCOPE1e starts (design:436-438).
   - **Smallest change:**
     - Within one judgement (the base run, the reproduce run, the prefixes, the adjacent re-runs, the recall search), match a
       failing test by its full name with its case, its exception type and its assertion's location. Use the message line only
       where no location can be read.
     - Normalise GUIDs, and hex runs of eight characters or more, as numbers are normalised. Do this both in a test's message
       and in a gate signature's error line.
     - Keep the full fingerprint as the record's identity.
     - Add to rows 3 and 5: a reproducible failure whose message carries a fresh GUID each run is pinned, is inherited from a
       base that fails it, and is found at its introducing commit.

2. **Blocking. An unresolved failure on a candidate of one has no outcome, and a prefix that cannot run the selection has no
   reading.**
   - An unresolved failure runs the candidate's members "one at a time as candidates of one". It hands nothing back, and no
     re-run makes the candidate green (design:580-586).
   - Candidates of one are common: the quiet hours' usual case (design:596-597), every `--one-at-a-time` candidate, and every
     member split out of an unresolved candidate. For a candidate that already has one member, the text allows only two
     outcomes, and neither works:
     - it stays where it is, with the queue behind it; or
     - it is formed again as a candidate of one, without bound, and promoted on the first fresh run that passes. That is
       retrying a fast .NET or Node failure, which design:582-583 and D180 keep out, here without a bound.
   - A prefix that does not build can appear in a failing test's or a check's prefix sequence: a member breaks compilation and
     a later member repairs it. Such a prefix is neither "the signature" nor a pass, and design:567-577 does not say which it
     counts as.
   - **Smallest change:**
     - Say that a candidate of one with an unresolved failure is handed back as *unresolved*, with the flake filed for its
       row, and the queue goes on. Or say that it is formed again once only, and handed back the same way if it fails again.
     - Say that a prefix that cannot run the selection makes the failure unresolved.
     - Add both cases to row 5.

3. **Blocking. A gate that died of memory meets the base-first stop, and the design misstates today's retries.**
   - design:583-584 says FLAKE1's re-run is "the only retry, as today". Today has two more:
     - MEMGATE1 runs any gate that died of memory again once, whole, after its memory is free, and a pass reads FLAKE of memory
       (`againOfMemory`, mb:3744-3765; mb:270-276);
     - LEFT1 runs a rehearsal that died again (mb:3540-3557).
   - §4.2's "Retries are today's" (design:665-666) names neither.
   - Round 4 makes deaths of memory likely in the scoped run:
     - its heavy gates run past MEMGATE1's bound (design:796-797, 815);
     - they run beside the background's git half and a rehearsal, and beside five workers;
     - MEMGATE1 was written after 2026-10-22, when the sensitive scan, `web-rules` and the rehearsals died of memory, "each
       read as the change's failure" (mb:259-262).
   - A death of memory names no test, so §4.1 point 4 runs that gate on the base first (design:545-556).
     - Under the same pressure the base dies the same way, and the queue stops until main moves, though main is not broken.
     - When the base survives, each prefix dies or passes according to the machine's load, and that can pin a member. The
       adjacent re-run guards against this only in part.
   - **Smallest change:**
     - Say that the scoped run keeps MEMGATE1's run again, and correct design:583-584.
     - A failure that `memoryDeath` reads (mb:2244) is the machine's:
       - it is never judged on the base or over the prefixes;
       - it never stops the queue and never hands a member back;
       - a second death holds the candidate, said as memory, until the admission record shows its need free, and then it runs
         again.
     - Add to row 5: a death of memory on a candidate and on its base, the queue not stopped.

4. **Not blocking. The load the two runners may make is understated in two places.**
   - **The background's simultaneous set.** It is given for the background's Process lane alone (design:789-794). Its light
     lane is scheduled by memory beside its git half (`byMemory`, mb:623-647). While a scoped tier runs or waits, admission
     holds back only Process-lane gates and rehearsals (design:785-786).
     - So the git half with up to three heavy light gates is a larger set: 8 + 3 × 6 = 26 GB by `MEMORY_NEEDS`, against 18 GB
       for the git half and a rehearsal.
     - It is not blocking because that lane runs below normal priority (CIPRIO1) and is admitted only above the 26 GB
       headroom.
   - **The scoped run's own schedule.** §4.4 says the Process halves run "as GATEORDER1 has them" (design:705-706), and
     GATEORDER1 holds them until the heavy light gates end (mb:290-291). Its driver row then runs `service` beside the Process
     tier (design:713, 719-720), which is CI1's `byMemory`.
   - **Smallest change:**
     - Say that the scoped run is scheduled by memory, as a CI run is, with GATEORDER1's wait dropped.
     - Name the background's light lane in the set, with its priority and the headroom.
     - Time row 5's proof with background heavy light gates running too.

5. **Not blocking. The scoped runner at normal priority departs from D180's budget note, and the departure is not named.**
   - D180's note of 2026-10-19, point 2 (D180.md:68-69): a CI run is a guest at background priority, and is not started beside
     a full slate of workers. The note records the harm behind it: a git half under load slowed builders' git until the harness
     refused dispatches (D180.md:60-62).
   - Round 4 sets the scoped runner to normal priority for every gate (design:800-801). That includes its git half, eight
     minutes four wide, beside five workers.
   - §9's D180 item (design:1140-1148) and D207's Changes (D207:122-136) carry the budget note's thirty minutes, but not this.
   - **Smallest change:**
     - Name it as an amendment to the budget note's point 2.
     - Give its bound: one scoped Process tier at a time, and the git half's eight-minute budget.
     - Have CISCOPE1e's proof watch for dispatch refusals.

6. **Not blocking. The stop for a gate that fails on the base is a second deliberate exception to the blocking D180 removed,
   and it is recorded only as a rule.**
   - While main fails any gate that names no test, every merge waits, at once and with no grace. Today a merge waits on
     `BLOCKING` alone. Round 4's set is much wider:
     - the documentation tools, `daoris check`, `release-prep --check` and the census check;
     - `web-types` and the CLI's typecheck;
     - a compilation, discovery or host failure of any fast half.
   - A test that crashes its host on main (`Environment.FailFast`, a stack overflow) stops the queue at once. A test that fails
     normally is inherited for 72 hours.
   - D207 names only the expiry as an exception (D207:108-114).
   - Nothing re-checks a stop but main moving (design:551-552). So a stop caused by a transient host failure needs a direct
     commit to clear.
   - **Smallest change:**
     - Record this stop in D207's exception and in §9's D180 item, with its scope and that asymmetry.
     - Add a way to run the stopping failure on the base again: `--queue --recheck`, or the runner trying again when the parent
       asks.

7. **Not blocking. "A known-red unit runs in every candidate, whatever it reaches" is broader than §4.2 allows, and a partial
   fix is handed back.**
   - Points 5 and 6 of §4.2 never run a browser spec or a rehearsal in a scoped run, and defer git-half classes past the budget
     (design:654-663).
     - A known red among those is never run by a candidate, so the expiry's stop never reaches it. The stage still refuses it.
     - The claim is made at design:631-632 and 859-860, and at D207:39 and D207:110.
   - A member that partly fixes a known red changes its fingerprint. It is then pinned and handed back (design:557-560, 848),
     though it moved main toward green.
   - **Smallest change:**
     - In those four places, limit the claim to the units the scoped run may run.
     - Say that a changed fingerprint is handed back even when it comes from a partial fix, and that the parent's way through
       is a full fix or the skip.

8. **Not blocking. A reservation's process group is found per run, not per gate, and an admission read ends processes.**
   - The group is "the gate's process and every process started under it ... and the strays whose command line names the
     run's tree" (design:807-811, citing mb:4628-4634). The strays rule (`endStranded`, `tools/processes.mjs:189-205`) matches
     on a run's tree, and every gate of that run shares the tree. Two consequences follow:
     - Each gate's group counts its siblings' stray processes too. That overstates its use and understates the unused part of
       its reservation, so admission is too generous.
     - "The next read ends that group": either runner's admission read can end processes that name a tree another run has
       since taken over. `endStranded` spares only processes whose parent is still alive.
   - **Smallest change:**
     - A gate's group is the descendants of the gate's own process, by pid and creation time.
     - A run's strays are charged to that run's reservations together.
     - Ending processes is left to the run that takes over the tree's lock, as CIPRIO1d and CIPRIO1e do, and never to an
       admission read.

9. **Not blocking. The recall search's stretch probes are unbounded as written, and their price assumes short stretches.**
   - The search runs "at the midpoint of every stretch longer than sixteen commits" (design:388-391), and states the residue
     as a stretch "of sixteen or fewer". That residue holds only if the probing recurses until every stretch is sixteen commits
     or fewer.
     - The cost is then about the distance from halving's pair to the first failure, divided by sixteen.
   - The price assumes about ten halving runs and fourteen runs in all (design:430-435), over "a month's thousand-odd
     first-parent commits".
     - The month holds 2,153 first-parent commits (`git log --first-parent`, 2026-09-24 to 2026-10-24, at `a6991805f`).
     - A FIX-LOG failure with no recorded pass starts its range at the month's first commit, or where the unit was added
       (design:381-383).
   - **Smallest change:**
     - Say that the probing recurses.
     - Price each failure at about eleven halving runs, plus that distance over sixteen.
     - Correct the count.

10. **Not blocking. Small slips.**
    - **What *records only* admits** (design:515-519). "Names no gate but `service`" also admits the rules that name no gate:
      `src/Daoris.Cli/test/**` (mb:1189), the `tools` lane with `.github/**` (mb:1233), and `.mcp.json` (mb:1253). This is
      harmless: nothing more is deferred, and the closure runs their files. List them, or define *records only* by the rules at
      mb:1234, :1244 and :1249.
    - **D207's wording.** D207:47 says "whose paths reach no gate but `service`". Make it "whose paths' own rules name".
    - **A citation.** `ruleFor` is at mb:1574, not :1645-1653.
    - **A figure.** §6.2's "23 to 30 minutes" (design:976) is about 16 to 26 by the figures it cites.
    - **The frozen tip.** It is the branch's tip at enqueue, though design:489-490 says "as the parent previewed and queued
      it". Either refuse a tip that differs from the branch's last `--preview`, or drop "previewed".
    - **The expiry.** Say whether the 72 hours count per test, or per test and fingerprint (design:858). Counted per
      fingerprint, a direct commit that moves the failing assertion renews the waiver.

**10 findings, 3 blocking.**

**Verdict: accept after named changes the steward can check without another read.** Round 4 takes both of the round-3 Opus
read's blocking changes as named, and answers Codex's six blocking findings. Codex's load finding holds for the Process lane;
the background's light lane remains (finding 4). I checked each answer against the text and the code.

The foundation holds and is unchanged:
- a merge waits on a scoped run in a tree of its own, which is strictly more than today's wait on `BLOCKING` alone;
- the install waits for the background run, with no waiver;
- the staged content is bound to its receipt's tree;
- admission reserves memory in both measures;
- recall is a causal lower bound.

What remains is three local rules in §4.1 point 4. Each is a sentence, with its own row-5 proof case:
- match a failure by test, case, exception type and location, not by its message line (finding 1);
- give an unresolved failure on a candidate of one an outcome (finding 2);
- keep a death of memory out of the base run, the prefixes and the queue's stop (finding 3).

Findings 4 to 10 are statements and corrections the steward can check line by line. Codex's read of round 4 is a separate
matter.
