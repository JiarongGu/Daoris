# CISCOPE1: the reads of round 1 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-24-ci-scope-design.md` and D207 at
`8ec788f89`, each verbatim but for its headings set one level down. The steward ran both from outside the install, read-only, on
the design's own tree, read as landing after CIFAST1. Both ask for another round: Codex with 17 findings, 14 blocking; Opus with
21, 8 blocking. Round 2 answers each finding in the design's reads table.

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
