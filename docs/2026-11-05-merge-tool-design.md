# MERGETOOL1: the merge tool, the queue and the rehearsals cut by what each becomes, their run folders kept by age and size, and QUEUEHANG1 (D237)

Design, 2026-11-05. BL15 of [the backlog plan](2026-11-05-backlog-plan.md). Every size and line is read at `b7f6fc204`; the scratch
counts are the main checkout's on the same day. It changes no code; its chunks (§4) do. The choice and what it rejected are D237.

The direction is the owner's: the merge tool, the queue and the rehearsals are used all day, and landing work is what Daoris itself
does, so they get a design before a refactor. This design takes the integration design's settlement of who integrates
([`2026-10-28-integration-design.md`](2026-10-28-integration-design.md) §6, §7.1, §18.3) as given and does not reopen it.

## 1. What each tool does today

### 1.1 The files

| File | Size | Its test | Size |
|---|---|---|---|
| `tools/merge-branch.mjs` | 525,455 B, 8,642 lines | `src/Daoris.Cli/tool-test/merge-branch.test.ts`, 218 tests | 565,135 B, 8,092 lines |
| `tools/merge-queue.mjs` | 67,204 B, 1,039 lines | `src/Daoris.Cli/tool-test/merge-queue.test.ts`, 157 tests | 240,596 B, 3,458 lines |
| `tools/family-rehearsal.mjs` | 384,354 B, 6,484 lines, 445 checks | itself the `rehearse-family` gate | — |
| `tools/deployment-rehearsal.mjs` | 110,110 B, 1,846 lines | `src/Daoris.Cli/test/deployment-rehearsal.test.ts` | 38,052 B |
| `tools/rehearsal-kit.mjs` | 57,033 B, 1,078 lines | `tools/rehearsal-kit.test.mjs` | 46,171 B |

Beside them, under 40 KB and outside this design's cut: `ci-background.mjs` (the background run's records), `release-rehearsal.mjs`,
`machine-admission.mjs`, `test-failures.mjs`, `process-reach.mjs` with its folder, `processes.mjs`, `run-lock.mjs`, `as-merged.mjs`
(a gate's command) and `merge-rows.mjs` (git's merge driver for the router). The seven files over 40 KB are on the ratchet
(`tools/size-ratchet.json:6-7`, `:120-125`), so none of them may grow.

### 1.2 `tools/merge-branch.mjs` by mode

| Mode | Verbs | Code | Size | What it writes |
|---|---|---|---|---|
| Merge in place | `<branch>`, `--batch`, `--continue`, `--plan <branch>`, `--rerun`/`--stale` on an open merge, `--gated`, `--drop-batch` | batch state `:3862-3893`; `gateMerge` `:4799`, `rerunGates` `:4857`; `mergeNext` `:5223`, `start` `:5259`, `resume` `:5274`, `planOnly` `:5315`, `dropBatch` `:5351`; the knowledge server's refresh `:1958-1975` | about 30 KB | the merge left uncommitted; `local/scratch/merge-<branch>/`, `merge-batch.json` |
| Its resolvers | (inside a merge and a queue member's merge) | generated folders `:1492`, the backlog by its base `:1790-1867`, row tables `:1607-1726`, union joins `:1868-1957` | 25 KB | the merged backlog, row tables, generated folders |
| Queue: its verbs | `<branch> --preview`, `<branch> --message <file>`, `--queue [--recheck]`, `--drop-batch <branch>` | `previewVerb` `:8382` to `dropQueuedVerb` `:8564-8601`; the records, `merge-queue.mjs:88-243` | 13 KB + 9 KB | `local/merge-queue.json` |
| Queue: its runner | `--queue-run`, detached by the first verb that finds none | `queueRunVerb` `:8300`, `ensureRunner` `:8354`; the candidate's run `:7840-8299`; its environment, scope tree, member merges and probes `:7380-7839`; `merge-queue.mjs:302-986` (candidate, containment, judge, confirm run, scoped set, admission) | 63 KB + 43 KB | `local/scope-tree`; `local/scratch/scope-<sha7>/`, `scope-recheck-<sha7>/`; `local/scope-runs.jsonl`, `merge-queue.log`, `admission.json`, `integration-sightings.jsonl` |
| The full set on the checkout | `--full`, `--rerun`/`--stale` with no merge open, `--passed` | `gateCheckout` `:4948`, `judgeCheckout` `:5061`, `rerunCheckout` `:5139-5206` | 10 KB | `local/scratch/full-<sha7>/` |
| The full set beside | `--full --beside [<commit>]` | the gate tree's lock, record, dependencies, private files, logs, priority and tip watch `:5363-5793`; `gateBeside` `:5794-5860` | 26 KB | `local/gate-tree`, `gate-tree.lock`, `gate-tree.json`; `local/scratch/beside-<sha7>/` |
| The declared runner | `--full --beside --daoris-verify`, modes `run`, `plan`, `probe`, `commits` | `:5861-6445`, `gateVerify` `:6272` | 37 KB | its result at `DAORIS_VERIFY_RESULT`; `local/scratch/probe-<sha>/` |
| The CI | `--ci [--stage \| --night] [<commit>]` | `ciPlan` `:6493`, `classPlan` `:6524`, status, load and record `:6700-6943`, `gateCi` `:6944-7214` | 49 KB | `local/scratch/ci-<sha7>/` with `status.json`; `local/ci.json`; through `ci-background.mjs` the findings, receipts and sweeps |
| Its watch | `--ci --watch`, `--ci --stop` | `watchCi` `:7215`, `ciWatchVerb` `:7279`, `ciStopVerb` `:7354-7379` | 7 KB | `local/ci-watch.json`, `ci-watch.stop` |
| The stage | `--passed`; `desktop-publish --stage --from-gated <commit>` | `stageJudgement` `:2373`, `stageRemedy` `:2418`, `stageLines` `:5011`, `stageEvidence` `:5091-5138`; `tools/desktop-publish/gate-tree.mjs`, `gates.mjs` | 8 KB + 13 KB | `local/stage-history.jsonl` |
| The prune | every merge's, `--prune [--plan]` | rules `:3286-3589`, plan and apply `:3590-3861` | 30 KB | removes merged branches and worktrees, half-removed folders and the gate tree |

What every mode shares:

| Part | Code | Size |
|---|---|---|
| Arguments and usage | `USAGE` `:706`, `parseArgs` `:740-934` | 14 KB |
| The gate plan, components, the lanes' schedule, the rehearsals beside the git half | `:454-705`, `gatePlan` `:957-1029` | 21 KB |
| The blocking and path checks, selection | `:1030-1103`, `selectGates` `:2005-2115` | 13 KB |
| The lane table `REACH` (data) | `:1104-1491` | 29 KB |
| The verdict store and class verdicts | `verdictsHome` `:2116` to `treeDrift` `:2263-2290` | 8 KB |
| A verdict's standing, sweeps, the stage's judgement | `:2291-2454` | 10 KB |
| Reports read, FLAKE1, LEFT1, timed and node re-runs | `:2455-2754` | 16 KB |
| Memory before a heavy gate (MEMGATE1) | `:2755-2932` | 10 KB |
| `daoris.lanes.json` and the lane report | `:2933-3091` | 8 KB |
| git, the refusals, the commit check | `:3092-3285` | 10 KB |
| A step's process and tree, its log | `runStep` `:3970`, `:3894-4091` | 8 KB |
| A gate's run and re-runs, the two lanes | `runGate` `:4092`, `runGates` `:4518-4646` | 32 KB |
| The header | `:1-425` | 38 KB |

The header is 38 KB of history by row id (GATE2 to WEBSPLIT1). The queue is the largest single responsibility: 76 KB of the tool and
the whole of `merge-queue.mjs`, 66 KB. The declared runner, the CI with its watch and the gate tree together are 119 KB.

### 1.3 The rehearsals

- **The family rehearsal** proves the router across two example repositories through the real artefacts: a script whose 30 phases run
  at the module's top level in order (`:355` to `:6412`), sharing state through top-level bindings (a quest's id read in a later phase),
  after a set-up of paths, environment, hosts and ports (`:36-354`, 18 KB). Its phases measure 5 to 63 KB: phase 7, *the driver
  drives*, is 56 KB (`:753-1649`); phase 11 with 11a, *the remote*, 63 KB (`:1921-3014`).
- **The deployment rehearsal** publishes the desktop to a folder and drives that: its predicates, which its test asserts, above the
  divider (`:91-449`), and ten phases in one function (`:673-1846`).
- **The kit** holds the transcript, the checker, ports, own builds and evidence (`:1-421`), and two stub agents as program text
  (`ACP_STUB_AGENT` `:483-944`, `ACP_TURN_END_AGENT` `:945-1068`).

### 1.4 What they leave behind

Each run writes a folder under the main checkout's `local/scratch/`, and nothing removes one. On 2026-11-05, after 204 `scope-*` and
`ci-*` folders (3.5 GB, about 17 MB each, the Process halves' trx files most of it) were removed by hand, the folder still held 4,637
entries and 804 MB:

| Kind | Writer | Folders | Size |
|---|---|---|---|
| `merge-<branch>` | the merge in place (`:4807`, `:4891`) | 2,195 | 157 MB |
| `full-<sha7>` | the full set on a checkout (`:4960`, `:5162`); a run beside and the runner write it in the gate tree (`:5625`, `:6365`) | 130 | 103 MB |
| `ci-<sha7>` | the CI (`:7027`) | 128 | 65 MB |
| `beside-<sha7>` | the run beside, copied out at its end (`:5615`) | 47 | 80 MB |
| `scope-<sha7>`, `scope-recheck-<sha7>` | the queue (`:7950`, `:8083`) | (removed by hand) | |
| `probe-<sha>` | the declared runner's probe (`:6231`) | 0 | |
| `_fixtures/rehearsal-logs/` | the kit's transcripts and evidence (`rehearsal-kit.mjs:32`, `:45`) | 1,121 entries | 26 MB |

The rest of `local/scratch/` is what people and sessions write there on purpose: briefs, proposals, shots (the cleanup procedure,
`docs/development.md` *Repository scratch cleanup*). A run folder is read after its run in one place: a CI run clears a kept finding
from the trx of the run that gave the kept pass, in `ci-<sha7>/` or `scope-<sha7>/` (`:6906-6912`).

### 1.5 QUEUEHANG1's case

On 2026-11-01 batches 1031g and 1031h were submitted as `timeout <seconds> node tools/merge-branch.mjs … --message …`. The timeout
ended the whole tree it had started: the runner the tool starts detached (`:8360`) and every gate under it. Test hosts exited 143,
MSBuild's nodes "exited prematurely", code-map threw from its console's encoding, and each was judged as the change's failure; the
runner vanished and `--queue` said *running* for 80 minutes (`docs/FIX-LOG.md`, 2026-11-01). Three mechanisms let it:

1. A detached child on Windows is still in its starter's job and tree, so a kill of the submitter's tree reaches the runner.
2. `runStep` reads every signal as exit 128 and every other end as its code (`:3992`); a gate ended from outside reads as failed, and
   only a rehearsal is ever asked whether it died rather than failed (LEFT1, `rehearsalDecision` `:2732`).
3. The queue's record names a runner by pid alone; `--queue` prints the candidate's state from the record (`merge-queue.mjs:998`)
   without asking whether the runner lives, and nothing that still lived was left to ask.

## 2. What the product already does, and one way between them

### 2.1 The overlap

| The tool's job | What Daoris has | Overlap |
|---|---|---|
| A request, its frozen tip, the preview | `daoris integrate <repository> --plan <branch> \| --request <branch>`; `Workplace/IntegrationRequests.cs`, `IntegrationPlans.cs` | whole |
| The candidate, a member's merge, containment, promotion | `Workplace/IntegrationCandidate.cs`, `IntegrationPromoteRun.cs` and the `IntegrationPromote*` files, `LineHolds.cs`, `TreeScratch.cs` | whole |
| The resolvers | `Workplace/BacklogMerge.cs`, `GeneratedWriters.cs`, `IntegrationRows.cs`, `IntegrationResolves.cs`, twinned row for row through `fixtures/backlog-merges.json`, `union-joins.json`, `integration-declarations.json` | whole, twice |
| The scoped run, the admission, the confirm run, the judge, known red | `Review/Integration/` (`CandidateJob.cs`, `RunnerGateRuns.cs`, `IntegrationJudging.cs`), `Review/Heavy/MachineAdmission.cs` | whole; Daoris runs the gates through the tool's declared runner |
| The merge in place | the landing door (`Workplace/TreeLands.cs`, `LandingPress.cs`) for the install's sessions, and integration | the same job: a branch into the line |
| The commit check | the runner's `commits` mode, which integration's first step calls | shared, one copy |
| The plan, lanes, reach, Process census, the gates' run | none: Daoris calls the runner the repository declares (D192, `Review/Integration/RunnerCaller.cs`) | none |
| The CI, its watch, its kinds | a line's verification pipeline, `Workflows/VerifyRuns.cs`, `Review/Heavy/HeavyExecutor.cs` (FABRIC-PIPELINE1d); the background check is FABRIC-PIPELINE1e and g, open | designed, not built |
| The stage | UPDATESTEP1 and FABRIC-PIPELINE1h, open | none built |
| The prune of the parent's subagents' worktrees | the install's tidy for its own sessions (`Workplace/BranchTidying.cs`, KEEPSIZE1) | none for the parent's harness |
| Run folders under `local/scratch/` | KEEPSIZE1's keep, for the home | none: the home's keep never walks a repository's checkout |
| The rehearsals | none, by design | none |

Integration is done twice today: by the tool's queue and merge in place for this repository, and by Daoris for every repository whose
declaration says so, with the resolvers twinned between them. The integration design already ends the first at INTEGRATE1k's flip
(§6.3), with a fence so the two never both promote. That flip is the D228 register entry for the tool's integrators: the condition
that ends them and the row that deletes them.

### 2.2 One way

**Stays the repository's own development tool.** What reads this repository's layout, which the product must not carry (the
integration design §6.2): the gate plan, the lanes, `REACH`, the censuses, the two lanes and their order, the re-runs, memory, the
verdict store and standing, the commit check, the gate tree, the declared runner Daoris calls; the CI and its watch until
FABRIC-PIPELINE1e and g run the background check; the stage's evidence until UPDATESTEP1 and FABRIC-PIPELINE1h; the prune until
SUBAGENTS1 makes the parent's builders the install's sessions; the run folders' keep (§3.4), since the repository's tool writes them
whoever runs it; and the rehearsals, which stay harnesses: a proof of the product does not run inside the product (§7.1 there).

**Daoris does.** This repository's integration, at INTEGRATE1k, through `daoris integrate` and the integrate workflow the steward sets
up once the install is staged (D240 point 6). After it, the parent's branches are requested there too, through the records-branch
door (the integration design §6.3 step 5).

**Retired.**

| What | When | Why |
|---|---|---|
| The queue's verbs and runner, `tools/merge-queue.mjs`, the merge in place with `--batch`, `--continue`, `--plan`, `--gated`, the resolvers' tool half, the knowledge server's refresh after a merge | at INTEGRATE1k's flip (chunk MT9) | Daoris integrates; two integrators are the defect |
| The full set on the checkout (`--full` in place, `--rerun` and `--stale` with no merge open) and the publisher's path from the checkout to an install | MT8 | `--ci` in the gate tree is the one way to gate a commit, and `--from-gated` the one way to stage it; the guide already says to stage only from the gate tree. `--force-ungated` stays the person's override |
| `--full --beside` as a run of its own | MT8 | `--ci --night [<commit>]` runs every gate whole in the gate tree and gives the same receipt; `--full --beside` is refused naming it. The runner's spelling, `--full --beside --daoris-verify`, stays as the pipeline design declares it |
| The publisher's second spelling of the gate tree's names (`tools/desktop-publish/gate-tree.mjs:8-11`) | MT3 | one spelling, imported |

Until the flip both of the tool's integrators stay: the merge in place, which the scope's merges use under D180's note of 2026-11-04,
and the queue. MERGETOOL1 adds no third and reworks neither (§3.1 rule 3).

## 3. The shape

### 3.1 The merge tool: a thin runner over modules by responsibility

`tools/orient-index.mjs` and `tools/desktop-publish.mjs` are the exemplars: a runner at the old path, a folder of modules beside it,
and every export importable from the old path.

| Module, under `tools/merge-branch/` | From today | Its job | Its end |
|---|---|---|---|
| `args.mjs` | `:706-934` | the verbs, their flags, the usage | stays; the retired verbs leave with their chunks |
| `plan.mjs` | `:454-705`, `:935-1029` | the plan from the declaration and the workflow, components, the lanes' schedule | stays |
| `reach.mjs` | `:1030-1103`, `:1976-2115` | the blocking and path checks, selection, `pathsReaching` | stays |
| `reach-table.mjs` | `:1104-1491` | `REACH`, as data | stays |
| `verdicts.mjs` | `:2116-2290` | the store, class verdicts, the tree a verdict is keyed by | stays |
| `standing.mjs` | `:2291-2454` | a verdict's standing, the sweep bound, the stage's judgement and remedy | stays |
| `failures.mjs` | `:2455-2754` | reports read; the flake, died and timed decisions | stays; `ending.mjs` beside it (§3.5) |
| `memory.mjs` | `:2755-2932` | memory before a heavy gate, a death of memory | stays |
| `lanes-file.mjs` | `:2933-3091` | `daoris.lanes.json`, globs, classification | stays |
| `git.mjs` | `:3092-3285` | git, status, the refusals, the commit check | stays |
| `steps.mjs` | `:3894-4091` | a step's process and tree, its log | stays |
| `gates.mjs` | `:4092-4646` | a gate's run and re-runs, the two lanes | stays |
| `gate-tree.mjs` | `:5363-5860` | the gate tree, its lock and record, its dependencies, its private files and logs, priority, the tip watch | stays; the publisher imports its names |
| `verify.mjs` | `:5861-6445` | the declared runner | stays: Daoris calls it |
| `stage.mjs` | `:5011-5138` | `--passed` and the stage's evidence | until UPDATESTEP1 and FABRIC-PIPELINE1h |
| `checkout.mjs` | `:4948-5010`, `:5139-5206` | the full set on the checkout, `--rerun` and `--stale` with no merge open | MT8 |
| `ci-plan.mjs`, `ci.mjs`, `ci-watch.mjs` | `:6446-6699`, `:6700-7214`, `:7215-7379` | what a run owes; its status, record and run; the watch | until FABRIC-PIPELINE1e and g |
| `prune.mjs` | `:3286-3861` | GATE2's rules, plan and removal | until SUBAGENTS1; `keep.mjs` beside it (§3.4) |
| `landing.mjs`, `resolvers.mjs` | `:3862-3893`, `:4647-5010` (the merge's part), `:5207-5362`, `:1958-1975`; `:1492-1957` | the merge in place, batch and resume; the resolvers | INTEGRATE1k |
| `queue/runner.mjs`, `queue/candidate.mjs`, `queue/verbs.mjs` | `:7380-7839` with `:8300-8381`; `:7840-8299`; `:8382-8601` | the runner; a candidate's run; the verbs | INTEGRATE1k, with `tools/merge-queue.mjs` |

The rules of the cut:

1. **The runner stays at its path.** `tools/merge-branch.mjs` keeps what every caller names: the declared runner's command, the
   publisher's, the tests' spawns, the guides. It holds the header (what the tool is, and which module holds each verb), `main`, and
   one `export … from` line per module. It ends under 20 KB.
2. **A module per responsibility, each under 40 KB, named for its job.** Imports run one way: the leaves (`args`, `plan`, `reach`,
   `lanes-file`, `verdicts`, `standing`, `failures`, `memory`, `git`, `steps`) import no mode; `gates` imports leaves; each mode
   (`landing`, `checkout`, `stage`, `gate-tree`, `verify`, `ci*`, `prune`, `queue/`) imports leaves and `gates`, and another mode
   only where it runs inside it (the CI and the runner in the gate tree, the CI's stage run in `stage`). A test reads the import
   graph and refuses a cycle or a leaf that imports a mode.
3. **Cut on the lines Daoris takes over.** What a named row retires is a module or folder of its own (the last column), so that row
   deletes files rather than lines spread through a hub. What retires at INTEGRATE1k is moved whole and unchanged; it gets only the
   fixes every merge until then needs (§3.5's queue half).
4. **A move is mechanical and asserted.** `tools/move-code.mjs` (MT1) moves named top-level declarations, with their doc comments,
   from one module to another; writes the imports each side then needs and the re-export at the old path; reads the source with the
   TypeScript compiler's parser, which the CLI's workspace installs; asserts the counts it is given; and with `--plan` changes
   nothing. Its second form wraps a top-level span between two markers into an exported function and lists the bindings the span
   reads from before it and those read after it (the rehearsals' split, §3.3). No builder retypes moved code. The move is one commit
   and the comments a second: cut to the why (D231 point 3), with no row id, decision number or history (D235), so a reviewer reads
   the move as a move and the gate COMMENTGATE1 holds the new files.
5. **The tests mirror the modules.** `src/Daoris.Cli/tool-test/merge-branch/<module>.test.ts`, each under 40 KB; the end-to-end
   cases (143 KB today, `merge-branch.test.ts:5961-8092`) by mode; the scratch repository's helpers (`:1-420`) beside
   `src/Daoris.Cli/test/_fixture.ts`, which the tool tests import already. The `tool-tests` gate globs `tool-test/**/*.ts`, so no gate
   changes; the tool-test census holds each file to one gate.
6. **The records follow in the same chunk.** The lane table's tool-tests row (`:1114-1121`) names `tools/merge-branch/**`, and the
   family rehearsal's (`:1151`) `tools/family-rehearsal/**`; the ratchet's entries tighten and, once a file is under 40 KB, go. The
   guide and the dispatch skill name verbs, not lines, so a move does not touch them.
7. **No record changes shape.** `local/merge-queue.json`, the verdict stores, the CI's and the gate tree's records are read and written
   as before, so a runner or a watch that loaded the code before a move and one that loaded it after read each other's records.

### 3.2 The queue

`tools/merge-queue.mjs` is not split: it retires at the flip with `queue/`. Until then it takes QUEUEHANG1's record changes alone (the
runner's beat, the lines that say a runner lost, §3.5). If the flip comes before MT5, MT5 leaves the queue where it is and MT9 deletes
it from there.

### 3.3 The rehearsals

They stay harnesses, run by the CI as gates; their scheduling is FABRIC-PIPELINE1g's.

- **The family rehearsal** becomes a runner at `tools/family-rehearsal.mjs` over `tools/family-rehearsal/`: `world.mjs` holds what
  `:36-354` sets up (paths, the person's environment, `run`, `api`, the hosts and their ports, the driver's helpers, the stop of
  everything at exit), and each phase group is a module exporting `async function (world, check)` that returns, by name, the facts a
  later phase reads. A phase reads only the world and what an earlier phase returned to it; the runner runs them in order and ends
  with the report. Phase 7 and phase 11 with 11a split along their own subsections, two modules each.
- **The deployment rehearsal** keeps the predicates its test imports (`:91-449`) in `deployment-rehearsal/checks.mjs`, re-exported
  from the old path, and its phases (`:673-1846`) in modules over a world, as the family's.
- **The kit's** stub agents (`:422-1078`) move to `rehearsal-kit/stubs.mjs`, re-exported.
- **The proof of a move** is the run's transcript: its check labels, in order, equal the last green run's, read from
  `_fixtures/rehearsal-logs/` by the parent after the chunk's `--ci --stage` run (a builder runs no rehearsal).

### 3.4 The run folders, kept by age and size

One unit, `tools/merge-branch/keep.mjs`, owns a run folder from its making to its removal.

- **One maker.** `runFolder(home, kind, id)` makes every run folder the tool writes (§1.4's kinds) and writes `.run.json` in it: its
  kind, commit, start and the tool that wrote it. The kit's transcripts and evidence keep their names under `_fixtures/rehearsal-logs/`.
- **What may go.** Only a folder with that mark; a folder from before the mark only where its name is one of the tool's kinds
  (`merge-`, `full-`, `beside-`, `ci-`, `scope-`, `scope-recheck-`, `probe-` with a commit) and every file in it is one the tool writes
  (`.log`, `.trx`, `.partial`, `.runsettings`, `status.json`, `.composition.json`), said as such; and a transcript or evidence folder
  of the kit's by its name. Nothing else under `local/scratch/` is ever touched: what a person or a session put there is theirs.
- **Never.** A folder a live run holds: the gate tree's run while its lock's process lives, the queue's candidate while its runner
  stands, a CI run whose status says `running`, an open merge's; the folder whose trx a standing kept verdict may be read from (each
  gate's newest verdict in `local/gate-verdicts.json`, by its commit); the last green CI run's (`local/ci.json`) and the last stage's
  (`local/stage-history.jsonl`); and the newest three of each kind. These sets are small and bounded.
- **By age.** Every other folder whose newest write is older than seven days goes. The age is read from the folder and its top-level
  entries, never a deeper walk.
- **By size.** Where what is left of the kinds is still over 2 GB together, the oldest go first until it is within. A folder under
  *never* is counted and never taken; where those alone are over, the prune says so.
- **The unknown keeps.** A size that cannot be read (a folder deeper than the tool writes, an entry that cannot be listed) counts as
  unknown, keeps, and is said, as KEEPSIZE1 keeps a size it has not counted (D195 §6).
- **One removal door: the prune.** Every merge's prune and `--prune` run the keep; `--prune --plan` lists what would go and why. A CI
  run's end, a candidate's end and a runner call's end run the prune's keep alone, so a day of runs never waits for a merge to be
  bounded. Removal is the prune's own `removeFolder` (`:3743`): resolved inside the checkout, ignored by git, no reparse point
  followed, as the cleanup procedure asks.
- **Said, never silent.** One line per kind (*kept 41 `ci-` folders, 0.9 GB; removed 12, 0.4 GB, older than 7 days*), and one per
  folder kept past the size bound and why.
- **The numbers.** Seven days covers a failure read the next working day and a weekend. 2 GB holds about 120 of the 17 MB scope and CI
  folders measured, a week at the scope's pace. `MERGE_BRANCH_KEEP_DAYS` and `MERGE_BRANCH_KEEP_GB` set them, as the tool's other
  switches are set.
- **Not here.** The records under `local/` (the verdict stores already keep 40 a gate, `VERDICTS_KEPT_PER_GATE` `:2175`; the `.jsonl`
  records hold 0.9 MB together and `local/merge-queue.log` 1.1 MB, which goes with the queue); `_fixtures/rehearsal-builds/` (105 MB,
  a build reused, no run's folder); the gate tree and the scope tree, which the prune and the flip own.

Whoever runs the runner, the parent's CI or Daoris's integration of this repository, writes these folders into the main checkout, so
the same keep bounds both. Daoris's own keep bounds its home and never walks a repository's checkout (KEEPSIZE1).

### 3.5 QUEUEHANG1: a gate's end said for what it was, and a runner that outlives its submitter

**A step's end** (`tools/merge-branch/ending.mjs`, beside `failures.mjs`). `runStep` answers its process's code and signal, and a pure
reading says which end it was:

| End | When | What follows |
|---|---|---|
| `ended` | the run ended it (a drop, a newer tip, a stop) | as today: no verdict, the run's own line |
| `passed`, `failed` | the gate's report or its own output says so | as today |
| `died` | a process end (`processExit`, `:2698`) or a report missing or unfinished (no trx summary, no TAP plan's end, no vitest JSON) | run once more alone, as LEFT1 runs a rehearsal today, now for every gate; dying again it is a failure that names no test, judged as today |
| `lost` | the end came from outside the gate and the run: a signal or a termination status (143, 137, 130, `0xC000013A`) the run did not send, or the run's own signal handler fired | no verdict, nothing filed as known red, no confirm run, no base or prefix run, never a stop of the queue; the candidate or the CI run ends `lost`, said with the gate's last lines, and runs again once its runner stands |

The declared runner's answer names a lost gate, and Daoris's `Review/Integration/RunnerGateRuns.cs` reads such a run as unknown,
never as a failure to judge. A check killed on Windows with exit 1 and no report reads as failed: the limit is said in `ending.mjs`,
and the runner's start below is what keeps the queue's gates out of reach of such a kill.

**A runner's liveness** (`tools/merge-branch/liveness.mjs`). The queue's runner record and the watch's carry `beat`, rewritten every 20
s, and stand while their process runs and has beat within two minutes: the machine admission's rule and numbers
(`machine-admission.mjs`, `BEAT_MS` and its stale bound). `--queue` names the runner, its pid, start and the beat's age, or says it
*lost*: since when, the pid gone or silent, with `local/merge-queue.log`'s last lines. A candidate whose runner is lost reads *running,
its runner lost*, never *running*. A waiter that finds its runner lost starts one, unless `MERGE_BRANCH_QUEUE_OWNER` says its owner
ended; the new runner resumes the candidate by ancestry (`resumeCandidate`, `merge-queue.mjs:428`). The CI's status and `--ci --watch`
say a lost watch the same way.

**The runner outlives its submitter.** The queue's runner is the one process the tool starts to outlive what started it: a wait may
end, and the request stays queued. On Windows it is started through `Win32_Process.Create`, over the CIM the tool already reads
processes by (`PROCESS_QUERY`, `:3522`), so it is no child of the submitter and in no job of its: a `timeout`, a harness ending a
session's tree, or `taskkill /T` of the submitter ends the submitter alone. The runner opens its own log rather than inheriting a
handle. Elsewhere it starts detached into a session of its own, as today. A runner started under a test (`MERGE_BRANCH_QUEUE_OWNER`)
starts as today, so the test's clean-up ends it. The watch is started by a person or the parent on purpose and lives in their terminal:
its loss is said, not prevented.

## 4. Chunks

### 4.1 The order

| Chunk | Model | Contents | Proof, failing first where it adds behaviour |
|---|---|---|---|
| **MT1** | Opus | `tools/move-code.mjs` with its test; the thin runner; `args`, `plan`, `reach`, `reach-table` moved with it; their tests to `tool-test/merge-branch/`; the reach row and the ratchet; comments cut | the mover's counts refuse a wrong plan; `--plan <branch>` on a scratch repository prints the same before and after; the moved tests pass unchanged |
| **MT2** | Sonnet | `verdicts`, `standing`, `stage`, `checkout`, `failures`, `memory`, `git`, `lanes-file` | the moved tests unchanged; `--passed` on a scratch repository answers as before |
| **MT3** | Sonnet | `steps`, `gates`, `gate-tree` (the publisher imports its names), `verify` | the moved tests; the runner's `plan` mode answer byte for byte on a fixture |
| **MT4** | Sonnet | `ci-plan`, `ci`, `ci-watch`, `prune` | the moved tests; `--prune --plan` unchanged on a scratch repository |
| **MT5** | Sonnet | `landing`, `resolvers`, `queue/` moved whole; the runner under 20 KB and its header a module map; the ratchet's entries for the tool and its test gone | the queue's tests unchanged; the import-graph test |
| **MT6** | Opus | QUEUEHANG1 (§3.5): `ending.mjs`, `liveness.mjs`, the runner's start, the `--queue` lines, the runner's answer and the driver's reading | the readings by unit; `--queue` with a dead pid and a silent beat; a stand-in gate ended from outside reads lost and its candidate is neither handed back nor bisected; on Windows the submitter's tree killed after the acknowledgement and the runner beating on to an outcome; a runner answer with a lost gate read as unknown |
| **MT7** | Sonnet | `keep.mjs` (§3.4) through the prune and each run's end | `planKeep` by unit: age, newest three, never-sets, size, unknown, unmarked; `--prune --plan` then `--prune` on a scratch repository |
| **MT8** | Sonnet | the retirements of §2.2 for MT8; `docs/development.md` and the dispatch skill rewritten where they name them | each retired verb refused naming its one way |
| **FR1** | Opus | the family rehearsal's world, runner and phases 1 to 7; the kit's stubs | the transcript's labels equal the last green run's |
| **FR2** | Sonnet | phases 8 to 15 | the same |
| **FR3** | Sonnet | phases 16 to 19; the deployment rehearsal's checks and phases | the same, and the deployment rehearsal's test unchanged |
| **MT9** | Sonnet | at INTEGRATE1k's flip: `queue/`, `landing.mjs`, `resolvers.mjs`, `tools/merge-queue.mjs` and their tests deleted; the twins' fixtures read by the driver alone; the guide and the dispatch skill name `daoris integrate` | every queue and merge verb refused naming `daoris integrate`; the driver's twin tests unchanged |

### 4.2 Keeping the CI working throughout

Every verification of the scope runs through this tool, so it changes under these rules.

1. **Built beside, switched after.** The chunks are built on their own branches during waves 7 and 8 and merge only after
   SCOPEVERIFY2 is green and the install is staged (the backlog plan's risk 4). A fix the tool takes on main meanwhile is carried into
   its new module by the chunk's next merge of main, found by the function's name.
2. **One at a time on main, in the table's order.** The MT chunks share the runner, so each starts from the one before it. The FR
   chunks touch no file of theirs and may run beside them.
3. **Each move changes no behaviour.** The old path re-exports every name, so every test and caller is unchanged; the move commit and
   the comment commit are reviewed apart.
4. **Each merge is proven by the tool it lands.** Before the next chunk merges, the parent runs the tool tests (`npm run tooltests`,
   which drive the merge, the queue and the CI end to end in scratch repositories) and one `--ci` with the merged tool on its own
   merge. A run that cannot start is undone by reverting that merge, which restores the tool that ran the CI before it.
5. **Running processes keep what they loaded.** After each merge the parent stops and starts the watch; the queue's runner drains and
   ends by itself, and the next request starts one from the new code. No record changes shape (§3.1 rule 7), so the two read each
   other's records meanwhile.
6. **Behaviour after the moves.** MT6 and MT7 change what the CI and the queue do, so each follows a green CI on the moved tool, and
   MT7's first removal on the main checkout follows the parent's read of `--prune --plan`.

### 4.3 Sizing

Twelve chunks: three Opus (MT1, MT6, FR1) and nine Sonnet, about 14 to 20 builder-hours. The backlog plan budgeted three for the merge
tool (MT1 to MT3), before the rehearsals, the keep and QUEUEHANG1 were cut. With the mover, a Sonnet move chunk emits the comments it
cuts and the tests it re-homes, never the moved code; without it, a builder would retype about 1.5 MB of code and tests through the
edit tools, which costs as much as the rest of the work and is where a move goes wrong.

## 5. Rejected

D237 holds the reasons; in short:
- **Refactoring the queue like the rest.** It retires at INTEGRATE1k; reworking code whose deletion is scheduled keeps two integrators
  longer and pays twice.
- **The flip inside MERGETOOL1.** INTEGRATE1k has its own fence, the owner's sitting and the steward's set-up (the integration design
  §6.3, D240 point 6); this design neither waits for it nor does it.
- **The development tool rewritten into the product.** The runner reads this repository's layout, which the product must not carry.
- **A split by verb.** The modes share the plan, the store and the gates' run; one module per verb would copy them or import across
  verbs.
- **One file with a better index.** The ratchet already stops it growing, so each new feature lands in a side file anyway, and every
  reader pays for 525 KB.
- **The rehearsals as many small gates.** Their phases share hosts and state built in order; separate gates would start the hosts once
  each.
- **Run folders by count alone, by age alone, or by size alone.** Count lets a few large folders fill the disk; age lets a busy day
  pass any bound; size alone removes yesterday's evidence while a week of small merge logs stands.
- **Each writer pruning its own folders, or removing a folder when its run ends.** Seven writers would carry seven rules; a failed
  run's logs are what is read the next day.
- **Daoris's keep removing them.** The home's keep never walks a repository's checkout; the repository's tool keeps what it writes.
- **For QUEUEHANG1: a timeout on a gate, every nonzero end read as a failure, a watchdog process, or a scheduled task hosting the
  runner.** The admission never times out a running holder, and neither does a gate; reading an outside kill as a failure is the defect;
  a watchdog is one more process to leak; a scheduled task is a registration left on the owner's machine.

## 6. What this design's checks do not cover

It is read from the code and the records at `b7f6fc204`; nothing was built or run but reads, the scratch counts and the document
checks. That `Win32_Process.Create` puts the runner outside a Git Bash `timeout`'s kill and the harness's job is the documented
behaviour of a process created by the WMI service, not measured here: MT6's Windows test is the measure. The 2 GB and seven days
rest on one day's counts after a hand removal; MT7's first `--prune --plan` on the main checkout is the read that confirms or moves
them. The family rehearsal's phases were sized by their markers, not by the bindings they share, which FR1's mover plan lists first.

## 7. For the owner

After INTEGRATE1k, `daoris integrate <repository> --request <branch>` is a person's route: each request is a card in Daoris's window
(`Workplace/IntegrateCommand.cs:126-176`), and the steward's own acts (INTEGRATE1i) are parked with STEWARDROLE1d. At the scope's pace
of dozens of merges a day, that is a press per builder's branch. Either INTEGRATE1i is unparked to land before the flip, or the flip
waits for it; until one is chosen, the tool's queue stays and MT9 waits.
