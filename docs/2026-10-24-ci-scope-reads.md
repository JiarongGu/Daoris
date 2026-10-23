# CISCOPE1: the reads of rounds 1 and 2 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-24-ci-scope-design.md` and D207 at
`8ec788f89`, each verbatim but for its headings set one level down. The steward ran both from outside the install, read-only, on
the design's own tree, read as landing after CIFAST1. Both ask for another round: Codex with 17 findings, 14 blocking; Opus with
21, 8 blocking. Round 2 answers each finding in the design's reads table. Round 2 was read the same way at `aceced648`: Codex
asks another round (15 findings, 11 blocking); the Opus foundation reader finds the foundation holds and would accept after named
changes the steward can check (17 findings, 6 blocking). Round 3 answers both.

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
