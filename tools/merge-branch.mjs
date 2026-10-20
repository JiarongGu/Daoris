#!/usr/bin/env node
/**
 * The parent's one merge tool (MOD9, `docs/2026-09-30-parallel-development-design.md` §3 rule 6).
 *
 * ## Why
 *
 * The parent merges subagents' branches into main many times a day, and until this tool it typed each
 * gate chain by hand. On 2026-09-30 that went wrong twice: a hand-typed chain lost a rehearsal's reason,
 * and a batch of a "web only" branch skipped the .NET suites, so two C# tests that read the web's
 * catalogues broke on main unseen. So the plan is read from what the repository already declares.
 *
 * Every merge ran all of it, and that became the cost (GATE3, 2026-10-04): the driver's real-process half
 * took 30, 48 and 126 minutes on one day's three integrations, and everything else about 25. So a merge
 * now runs the gates its changed paths can reach, by the lane table below (`REACH`), and never the long
 * ones (GATE5: the real-process halves and the deployment rehearsal, which any driver change still
 * reached), which only the full set runs. The lesson of that batch is kept in two places. The table is held by tests to what each suite reads and what each
 * rehearsal runs, so a skipped gate is one that could not see the change. And the full set still runs
 * before the install is built: `publish:desktop` refuses a tree no full set passed (`--passed` below),
 * the same day, so a table that is wrong is caught before the person runs the build.
 *
 * ## What it does
 *
 * 1. Refuses unless the checkout is on `main` with a clean tree, and the branch exists and adds commits.
 * 2. Prints the lanes the branch touched, by id and title, from the repository's `daoris.lanes.json`
 *    (D115 §2.1, the design's §5), and names the steward's records if it edited them. It informs; it
 *    never refuses on a lane.
 * 3. The commit check: every commit the branch adds carries its `Co-Authored-By:` line (a merge of main
 *    into the branch needs none), and the branch's worktree, if it has one, holds nothing uncommitted
 *    (work a hand-back calls done and left out of the merge). `--no-commit-check` skips it.
 * 4. The prune (below), unless `--no-prune`: one line per branch, and per half-removed folder, removed or kept.
 * 5. `git merge --no-ff --no-commit`. A conflict stops here: the files are named and the merge is left
 *    for the parent. Code is never resolved for anyone; the append-only records resolve themselves by
 *    their `merge=union` attribute (D106). The orientation index is written from the merged tree and
 *    staged before the gates choose (`GENERATED`, ORIENT1a), so a conflict in it alone does not stop the
 *    merge, and one beside others is written once they are resolved. Before it, each decision file the merge
 *    changed has a blank line set before a note's label union left under another note, one line said per
 *    note (`setApart`, MERGEJOIN1).
 * 6. The gates, in a fixed order, fast first, in two lanes (below). The plan is every gate `daoris.gates.json`
 *    declares, plus every `npm run` step the release workflow runs that the declaration does not (the
 *    release and family rehearsals). The order is by kind (a devkit check, then the suites, then the
 *    rehearsals); within a kind the declared order holds, and the workflow's own rehearsals go before the
 *    declared ones, so the declaration's last gate, the deployment rehearsal, ends the plan. On 2026-09-30 a
 *    long-lived build server carried a broken environment into a publish, so that rehearsal started after
 *    `dotnet build-server shutdown`; since REHLANE2b its builds keep servers of their own, and no gate has a step before it.
 *    GATESLIM1: by default a merge runs the blocking checks alone (`BLOCKING`: the universal gates, the code map and the
 *    orientation index's check); every other gate it reaches is listed and skipped, "in the CI beside" (`--full --beside`).
 *    `--gated` runs what follows: of the plan, the baseline (`BASELINE`: the blocking checks and the CLI's `verify`, at every
 *    merge) and each gate a path the merge changes can reach (`REACH`). A path no rule
 *    places reaches every gate, and so does a gate the table never names. A long gate (`isLongGate`) is
 *    never run by a merge, reached or not (the Process halves, and the release, family and deployment rehearsals): it says it is in the full set before staging. The lines before
 *    the run say why each gate runs or is skipped. `--full` runs the whole plan, and `--rerun` runs a long
 *    gate when it is named. The merge is gated before it is committed, so the devkit's universal gates, in their
 *    own gate and in `verify`, run through `tools/as-merged.mjs` (GATE1): its docs gate dates each path from
 *    HEAD's history, and there HEAD is the commit the merge would make.
 * 7. Each gate's whole output goes to `local/scratch/merge-<branch>/<gate>.log` (gitignored), written
 *    beside and renamed when the gate ends, and one line per gate says its result and that file. The
 *    first failing gate starts nothing more, in either lane (`--keep-going` runs the rest), and what did not run is named. A
 *    Process gate also writes `<gate>.trx` there, each test's duration, and the ten slowest classes are
 *    printed after its line (PROC1), so where its time goes is measured at every merge that runs it.
 * 8. Each verdict is recorded with the tree it ran on, in `local/gate-verdicts.json` (gitignored), which
 *    `--rerun`, `--stale` and the stage read (below): the main checkout's, whichever worktree the run is in (GATEWT1).
 * 9. Once the gates pass, the knowledge server agents ask is rebuilt from the merged tree when the service's
 *    sources changed (`tools/knowledge-server.mjs build`, ORIENT1c); one line says so, and a build that fails
 *    is said and fails nothing.
 * 10. Nothing is committed. Committing stays the parent's, after it reads the diff and writes the records.
 *
 * ## A fixed gate re-runs alone (GATE4)
 *
 * `--rerun <gate>…` runs the named gates again on the merge in place, then any gate of its selection that
 * has no verdict yet (the ones a failure stopped, or one a fix's paths now reach), and any whose verdict a
 * path changed since reaches (GATE6, below); it keeps every other verdict. The summary names each kept
 * verdict and when it was given, and each run again with the path that made it stale. `--continue` runs
 * every selected gate again, as it always did. With no merge open it acts on the checkout (GATE6b, below).
 *
 * ## The full set before the install (GATE3)
 *
 * `--full` with no branch runs the whole plan on the checkout as it stands, merging nothing. `--passed`
 * answers whether the full set passed the checkout's tree: every gate's newest verdict that still stands
 * there is a pass. `tools/desktop-publish.mjs` asks it before it publishes anywhere but a scratch folder
 * under `_fixtures`, and a refusal prints its lines. A tree is the checkout's content as `git add -A` would
 * stage it, written with an index of its own in the git folder, so an uncommitted fix and an untracked
 * file count, and the person's index is untouched.
 *
 * ## A verdict stands until a path its gate reaches changes (GATE6)
 *
 * A verdict given on another tree still stands for this one when every path the two differ by is a record
 * the parent writes after the gates (the steward's lane and the records that merge by union, which the
 * install carries none of), or a path that cannot reach its gate by the lane table (`pathsReaching`, which
 * reads `REACH` as a merge's selection does: every path reaches the baseline). Whole trees were compared
 * before, so a one-line fix to a browser test, re-gated by the web gate alone, voided every other verdict
 * and cost a second full run before a stage. A gate whose verdict no longer stands is said as stale, with
 * the paths that made it so, at `--passed` and at `--rerun`, which runs it again rather than keep it.
 *
 * ## A gate re-run on the checkout as it stands (GATE6b)
 *
 * `--rerun <gate>…` with no merge open runs the named gates on the checkout, merging nothing, and every gate the stage
 * calls stale with them; `--stale` runs the stale ones alone. Each verdict is recorded where `--passed` reads it, every
 * other verdict is kept as the stage counts it, and the run ends with the stage's answer. A gate with no verdict at
 * all runs only when named. Outside a merge `--rerun` used to refuse, so one gate that flaked in the full set, or a fix
 * committed after it, left `--full` again, about an hour and a half, as the only way to a stage. Both refuse a tree that
 * is not clean, naming each path: a re-run gates a commit, and `--full` is the verb for the tree as it stands.
 *
 * A refused stage, `--passed` and a failed `--full` end with the smallest command that would pass the checkout
 * (`stageRemedy`): `--stale` when every gate it lacks is stale, `--rerun` naming each that failed or has no verdict
 * when that runs fewer than the plan, else `--full`. `publish:desktop` names the same command in its refusal.
 *
 * ## The full set beside the merges (GATEWT1)
 *
 * `--full` ran in the main checkout, so no merge could run for its hour to an hour and a half. `--full --beside
 * [<commit>]` runs it on `commit`, main's tip unless named, in the gate tree (`GATE_TREE`, `local/gate-tree` under the
 * main checkout, ignored): a worktree of its own, detached at that commit, made where none stands, reused where it
 * holds the commit, moved there where it holds another, refused where it holds changes. Each folder of
 * `DEPENDENCY_FOLDERS` with a lockfile is installed there (`npm ci`) when its `node_modules` is missing or was installed
 * from another lockfile, a stamp inside it says which. Then the whole plan runs there as `--full` does, and merges land
 * in the main checkout meanwhile.
 *
 * - **One store.** Every run records into the main checkout's `local/gate-verdicts.json` (`verdictsHome`: the first
 *   worktree git lists), whichever worktree it is in, under a lock beside the file, so a merge's verdict and the full
 *   set's never write over each other. Verdicts are keyed by tree, so `--passed` in the main checkout judges its own tree
 *   and `--passed` in the gate tree, which the stage asks, the gate tree's.
 * - **One at a time.** The run holds `GATE_TREE_LOCK` (its pid, commit and start) in the main checkout; a second is refused,
 *   naming it. A lock whose process no longer runs is taken over, and said.
 * - **The stage builds the gated tree.** `npm run publish:desktop -- --to <install> --service --stage --from-gated <commit>`
 *   builds in the gate tree, never in the main checkout as it moves, and refuses a tree no full set passed as before.
 * - **The prune sees it.** No rule above did: it is detached, so no merged branch has it out, and it is under `local/`,
 *   not `.claude/worktrees/`. It is never removed while its run holds the lock, nor while a process is inside it (one a
 *   full set started by hand holds no lock), read as CLEANUP2 reads a harness-locked tree's. A merge's prune removes it once its stage
 *   is done (`GATE_TREE_RECORD` notes the stage of the commit it holds); `--prune` removes it once its run has ended,
 *   staged or not. Either says so, a line of its own; changes in it are never forced away, and a folder git no longer
 *   lists there is the half-removed rule's (CLEANUP2).
 * - **Under load.** A full set beside merges shares the machine with their gates (GATEORDER1's lesson: a Process half's
 *   timing tests failed beside heavy gates). Nothing of FLAKE1's changes: a Process half's failures are re-run alone once
 *   its lane is quiet, and a pass alone reads FLAKE. The run says so before its gates start.
 * - **A guest (CIPRIO1).** After the lock is taken and before anything is built, the run lowers its own process to below
 *   normal (`startBesideRun`), so every gate's descendants start there. Its gates get build servers of their own
 *   (`BESIDE_ENVIRONMENT`), and it never shuts the owner's down. A run of main's tip, not one given a commit, asks
 *   before each gate whether main has moved (`watchTip`), and every few seconds while its gates run (CIPRIO1c); once it
 *   has, the run ends each gate running with its whole tree (`stepTrees`, `endTree`) and starts nothing more, saying
 *   which gates ran, which it ended and which were left; an ended gate gives no verdict, and the record says
 *   `superseded`, never passed. A person's Ctrl+C ends them the same way and the run winds down, its lock let go. I/O
 *   priority is left out (CIPRIO1b).
 * - **Its private files, its logs (GATEWT1b, GATEWT1c).** Before its gates start, the private files a gate reads under
 *   `local/` (`privateGateFiles`: the declared `sensitive.patternsFile`, nothing outside `local/`) are copied from the main
 *   checkout into the tree on every run; a missing one is said, and the gate that reads it answers. When the run ends, pass,
 *   fail or a thrown error, the tree's run folder is copied to the main checkout's `local/scratch/beside-<sha7>` (the newest
 *   run's replaces an older), so a prune loses none. A run killed outright never gets there: the next run, before it clears
 *   its logs, keeps any run folder it finds in the tree whose `beside-<sha7>` is absent.
 *
 * ## The prune (GATE2)
 *
 * Every subagent leaves a branch and a worktree, and so does every integration: 36 of 41 local branches
 * were merged into main with 41 worktrees standing when this was built. So each merge starts by removing
 * the merged ones, after its refusals and before `git merge` (a refusal still touches nothing), and a
 * batch's last `--continue` does it again. `--prune [--plan]` runs it alone; its plan changes nothing.
 *
 * A candidate is a local branch merged into main, but main, the branch the main checkout has out, the one
 * this runs in, and a batch's branches still to merge. It goes with its worktree, if one has it out, only
 * when that worktree has no modified or staged tracked file, has no untracked file (`git worktree remove`
 * refuses those without `--force`, and one may be work), and holds nothing under its gitignored `local/`
 * but a worker's logs and scratch: `local/scratch/` and a `*.log` anywhere under `local/` go with it, and
 * anything else there is an agent's private output, which nothing else keeps, and is named.
 *
 * A locked worktree is in use, never looked into or unlocked, but for the agent harness's own lock,
 * `claude agent <id> (pid N)` (CLEANUP2): the harness holds it for its session's whole life, not only
 * while the agent runs, and nine finished agents' merged worktrees stood under it. That lock is lifted
 * (`git worktree unlock`, and locked again if the removal is refused) when, besides the rules above, the
 * branch is not at main's tip, as a just-dispatched agent's is before its first commit, and no running
 * process is inside: none whose executable path or command line names the folder, read once per prune
 * (one CIM query on Windows, `ps` elsewhere) and only when a harness-locked worktree or a folder below
 * needs it. It also stays locked unless it has been idle (CLEANUP2b): nothing in it written for
 * `HARNESS_IDLE_MINUTES` (two hours), read by `newestWrite` from the folder's top-level entries, their children but build
 * output, and the git folder's HEAD, index and HEAD log, never a full walk. A process's command line does not show a shell's
 * working folder, the lock is held for the agent's whole run, and the branch guard fails once main moves past a fresh
 * branch, so a worker that had not yet committed lost its tree (2026-10-18); the plan says "written N minutes ago, a worker
 * may be in it". Removal is `git worktree remove`, never forced: ignored build output does not stop it, and
 * whatever does is work to keep. Then `git branch -d`, never `-D`, and one `git worktree prune` at the
 * end. What git refuses is kept and says why.
 *
 * A folder directly under the main checkout's `.claude/worktrees/` that git does not list and that holds
 * no `.git` is what a `git worktree remove` left when it could not finish (a 320 MB one, held by two
 * processes a test leaked, stood when CLEANUP2 was built). It is removed, looked at again just before: a
 * link (a reparse point) is never followed or removed, and a link inside it is removed as a link. One a
 * running process is inside is kept, naming each pid and command line; a process is never stopped here.
 * What cannot be removed is named, and the next prune tries again. Any other folder git does not list is
 * never touched. The prune never stops a merge: what it cannot do it says, and the merge goes on.
 *
 * ## Flakes
 *
 * Real-process tests fail under load and pass alone (FLAKE1 in `TASKS.md`). When a `dotnet test` gate
 * fails and every test it names is in one of FLAKE1's classes, each is re-run alone, once. If they all
 * pass the gate reads FLAKE, not PASS, and the summary counts it: a flake is reported, never hidden. A
 * failure outside those classes is never re-run, because a test that passes alone after failing in the
 * suite may be one test polluting another, and that is a defect.
 *
 * A rehearsal that died is run again once, whole, with the steps before it (LEFT1): one whose exit is a
 * process ending (a shell's 126 or 127, cmd's 9009, a signal, a Windows crash status), or that printed
 * nothing of its own. The family rehearsal once exited 127 with no transcript while three worktrees
 * built, and passed alone. If the second run passes, the gate reads FLAKE. A rehearsal that reported a
 * failed check has failed whatever its exit, and so has one that said why it stopped; neither is run again.
 *
 * ## Two lanes (GATELANES1)
 *
 * The real-process halves and the rehearsals start real processes and run one at a time (AGENTS.md); the other gates
 * start none, and a full set of about 55 minutes waited about ten of them on those. So the gates run in two lanes at
 * once: the Process lane (`isProcessLane`: a gate run with a `process.runsettings` file, or an `npm run rehearse*`
 * script), one gate at a time, and the light lane beside it, up to three gates at once (GATELANES2, below). The lane is
 * read from what a gate runs, as `isProcessGate` is, rather than declared: the workflow's own rehearsals are in no
 * declaration.
 *
 * Two builds of one .NET project at once fight over its `obj/` and `bin/`. So a Process-lane gate starts only once the
 * light gates of its component (`dotnetComponent`: the first two folders of the project its command names) have ended,
 * and the light lane runs those first: the desktop's fast halves build what its Process halves then run. A Process-lane
 * gate also waits for the heavy light gates (`isHeavyGate`: the CLI's verify, the .NET suites, the web gate), GATEORDER1:
 * the 0b44c7a run started `driver-process` beside cli, service and web and two timing tests failed there. The cost is
 * the light lane's tail, about 7 to 8 minutes of a full set of an hour or more, since the Process lane is the long pole
 * either way. The git half and the driver's other Process half never overlap: the Process lane is one gate at a time, though
 * each gate's classes run at once, but for the release, family and deployment rehearsals beside the git half (REHLANE1,
 * REHLANE2, REHLANE2b, below). `--stale` and `--rerun` run through the same `runGates`, so they follow
 * the same order. A Process-lane gate whose command names no project, a rehearsal, starts once the light lane has ended:
 * it builds the hosts behind its script's name. A
 * Process gate's failures are re-run alone only once the light lane has ended, and the gate beside it in its own lane,
 * so a pass alone is still a pass with nothing beside it (FLAKE1).
 *
 * Each gate's log, verdict and line are its own, the line printed as the gate ends; once both lanes have ended, every
 * gate's line is printed again in the plan's order, a gate not started as NOT RUN. The first failure, unless
 * `--keep-going`, starts nothing more in either lane, and the gate running in the other lane runs to its end and keeps
 * its verdict: a gate is never stopped midway. `--one-at-a-time` runs every gate in turn in the plan's order, as before,
 * for a loaded machine; a batch keeps it once given. The web gate keeps its own cap on its workers (WEBGATE1).
 *
 * ## The light lane's gates side by side (GATELANES2)
 *
 * A merge's light gates ran one at a time, 394 to 434 s on 2026-10-18, and in a full set every Process gate waited for
 * the heavy ones. So up to `LIGHT_WIDTH` (three) light gates run at once, each starting, in the light lane's order, once
 * a place is free and no gate running builds a component it builds. A gate's components (`gateComponents`) are its
 * command's and those of the root scripts it names, read through: the CLI's `npm run verify` ends in the devkit's
 * universal gates, so it runs in turn with universal, code-map and devkit; `npm run test:web` builds the HTTP host its
 * own build writes into, so it runs in turn with service; driver and modules share the desktop's. A gate that builds
 * none (orient-index) runs beside any. `LIGHT_WIDTH` says why three. Each gate keeps its own process, log, verdict and
 * line; the lines print as gates end, and the summary in the plan's order. The first failure starts nothing more, and
 * the gates running end and keep their verdicts. GATEORDER1's waits are unchanged, and `--one-at-a-time` is as before.
 * A run of one light gate alone, or of Process gates alone, runs in turn.
 *
 * ## A Process half on its fast half's build (GATEBUILD1)
 *
 * Each `dotnet test` gate built its project again. A Process half whose test project's fast half (`reuses`: the light
 * gate whose `testBuild` is the same, the project and every option but `--filter`, `--settings` and the logger) passed
 * in this run, on the tree the half starts on, runs with `--no-build`, and its line says whose build it ran
 * (`no build: driver built this tree in this run`). The tree is the one each verdict is recorded with (`contentTree`), so
 * a changed tree, or one git cannot name, builds. A `--rerun` or `--stale` that names a Process half without its fast
 * half has no fast half in its run, and builds; one that names both reuses as a full set does. The flake re-run alone
 * was `--no-build` already. One at a time reuses too.
 *
 * ## The family rehearsal beside the git half (REHLANE1)
 *
 * After GATELANES2 a full set's wall was mostly the Process lane, one gate at a time: the git half about 45 to 50
 * minutes, the driver's other Process half (then one class at a time, now eight at once, PROCPAR1) 15 to 19, the family rehearsal 10 to 26 and the deployment rehearsal 13 to 15. So a rehearsal
 * of `BESIDE_GIT_HALF` (`runsBesideGitHalf`) runs beside a git half (`isGitHalf`, the driver's `git.process.runsettings`
 * half, `beside` in the schedule): it goes in the lane right after the last git half, and a Process-lane gate starts once
 * every gate before it in the lane has ended but those it may run beside. Every other pair still runs one at a time, the
 * serial halves among them, and the rehearsal still waits for the whole light lane.
 *
 * A rehearsal runs beside a Process half only where they share no build output. The Process halves build the desktop's
 * projects into their own `bin/` and `obj/` and run from them, and only the desktop's (a test holds that). So the family
 * rehearsal builds the driver host into an output of its own, `_fixtures/rehearsal-builds/family` (`ownBuild` in
 * `tools/rehearsal-kit.mjs`, the SDK's `--artifacts-path`), and the service's hosts where the driver finds a development
 * build, in folders no Process half builds; and it shuts no build server down. A test reads its tool for both.
 *
 * The deployment rehearsal ran alone and last then, where its build-server shutdown stopped no build (D115's REHLANE1
 * note); REHLANE2b, below, moved it beside.
 *
 * A git half that re-runs its failures alone (FLAKE1) waits for the rehearsal beside it to end, and no Process-lane gate
 * starts meanwhile, the next rehearsal beside it neither. A rehearsal that died and is run again whole (LEFT1) does not
 * wait: that re-run is about a process that ended, and two gates each waiting for the other to end would never start.
 *
 * ## The release rehearsal beside the git half too (REHLANE2)
 *
 * The release rehearsal packs the CLI in its own folder: `prepack` stages the canon, the README, the LICENSE and a built
 * `dist/` there, which the CLI's bin prefers over its sources, and `postpack` removes them. It builds no .NET project,
 * starts no host, takes no port and runs no git of its own. No desktop test reads the CLI's folder outside `src/` and
 * `test/`, which no pack writes, nor runs its bin (a test reads every desktop source for both), so no Process half sees the
 * staging. So it is of `BESIDE_GIT_HALF`, before the family in the plan's order, and runs beside the git half, whose
 * classes no neighbour's load can fail; the family, which runs the CLI's bin, starts once it has ended, and two
 * Process-lane gates at once stays the ceiling. The driver's other Process half and the modules' run beside nothing: they
 * hold the clock-bound classes. It waits for the whole light lane, whose cli gate runs the bin, and the deployment
 * rehearsal, which packs the same folder, waits for it. D180's REHLANE2 note has what was rejected.
 *
 * ## The deployment rehearsal beside the git half last (REHLANE2b)
 *
 * The deployment rehearsal (12 to 14 minutes) publishes every desktop project and both service hosts, packs the CLI in its
 * folder as the release rehearsal does, builds the driver's host, runs the CLI's bin and opens the installed window. Its
 * publishes are told an output of its own (`desktop-publish --artifacts-path`, `_fixtures/rehearsal-builds/deployment`),
 * where their `obj/`, `bin/` and stages go, and its host's build goes there by `ownBuild`; and every build it makes keeps
 * build servers of its own (`OWN_BUILD_SERVERS` in the kit, a run beside's `BESIDE_ENVIRONMENT`), so it no longer starts
 * after a shutdown of every server on the machine, which beside the git half would stop the builds of all beside it. So it
 * is of `BESIDE_GIT_HALF`, last: it starts once the release rehearsal (which packs the same folder) and the family (which
 * runs the bin it would shadow) have ended, still beside the git half, and the modules' Process half waits for it. D180's
 * REHLANE2b note says why its window's checks share nothing with the git half but load, the walls, and what was rejected.
 *
 * ## The web gate's unit run beside service (WEBSPLIT1)
 *
 * In a full set the light lane's floor was the service suite then the web gate, about 10 minutes under load, because the
 * web gate builds the HTTP host the service suite builds. Its first step, the page's vitests, builds nothing. So it is a
 * light gate of its own, `web-unit` (`npm run test:web:unit`), of no component, and runs beside service; `web` runs the
 * page's build, the HTTP host's and Playwright (`npm run test:web:e2e`), after service as before. `npm run test:web` runs
 * both, for a person. Both are heavy (GATEORDER1), and `web` is declared first (`LIGHT_WIDTH` says why). WEBGATE1's cap on
 * vitest's workers is the page's own config, so it holds wherever the unit run runs. A vitest's read outside the page
 * reaches `web-unit`, and the browser suite's reads reach `web`.
 *
 * ## A batch
 *
 * `--batch` names more branches. A merge left uncommitted blocks the next one (git refuses to merge over
 * an open merge), and this tool never commits, so a batch goes one merge at a time: each is gated by
 * the checks and suites, the parent commits it, and `--continue` merges the next. The rehearsals run
 * once, after the last. The batch is recorded in `local/scratch/merge-batch.json`.
 *
 *   node tools/merge-branch.mjs <branch> [--batch <branch>…] [--full | --gated] [--keep-going] [--one-at-a-time] [--no-commit-check] [--no-prune]
 *   node tools/merge-branch.mjs --continue [--full | --gated] [--keep-going] [--one-at-a-time] [--no-prune]
 *   node tools/merge-branch.mjs --rerun <gate>… [--keep-going] [--one-at-a-time]
 *   node tools/merge-branch.mjs --stale [--keep-going] [--one-at-a-time]
 *   node tools/merge-branch.mjs --plan <branch> [--batch <branch>…] [--full]
 *   node tools/merge-branch.mjs --full [--keep-going] [--one-at-a-time]
 *   node tools/merge-branch.mjs --full --beside [<commit>] [--keep-going] [--one-at-a-time]
 *   node tools/merge-branch.mjs --passed
 *   node tools/merge-branch.mjs --prune [--plan]
 *   node tools/merge-branch.mjs --drop-batch
 *
 * Exit codes: 0 merged and every gate passed (flakes named), or pruned (a branch kept is the rule working),
 * or the full set passed the checkout, after a re-run on it or not · 1 a conflict, a failed gate, a failed
 * commit check, or a checkout the full set has not passed · 2 refused (usage, not on main, a dirty tree, an
 * unknown branch or gate, a full set beside already running, a gate tree with changes) or a tool error.
 */
import { spawn, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import {
  closeSync, cpSync, existsSync, lstatSync, mkdirSync, openSync, readFileSync, readdirSync, realpathSync, rmSync, statSync, writeFileSync, writeSync,
} from 'node:fs';
import { constants as osConstants, setPriority as osSetPriority } from 'node:os';
import { basename, dirname, join, relative, resolve } from 'node:path';
import { gluedLabels, records } from './doc-duplicates.mjs';
import { isMain, renameHeld, stagedTree, writeAtomic } from './fsx.mjs';
import { endOrphans, endStranded } from './processes.mjs';

export const MAIN = 'main';
const SCRATCH = 'local/scratch';
const STATE = `${SCRATCH}/merge-batch.json`;
const KINDS = ['check', 'suite', 'rehearsal'];

/**
 * Which gates passed which tree (GATE3): gitignored, under `local/`, and outside `local/scratch/`, which
 * holds what a merge can throw away. `--rerun` reads the merge's own verdicts from the batch record; this
 * one outlives the batch, for the stage.
 */
export const VERDICTS_FILE = 'local/gate-verdicts.json';

/** The command that runs the full set on the checkout as it stands: what a refused stage names when nothing smaller would do. */
export const FULL_COMMAND = 'node tools/merge-branch.mjs --full';

/** The command that runs each stale gate again on the checkout as it stands (GATE6b): what a refused stage names when it would do. */
export const STALE_COMMAND = 'node tools/merge-branch.mjs --stale';

/**
 * GATEWT1: the full set's own worktree, detached at the commit it gates, under the main checkout's ignored `local/`;
 * the lock a run beside the merges holds while it runs; and the record of its last run and its stage. All three are
 * the main checkout's, whichever worktree asks. `tools/desktop-publish.mjs` names the same three (a test holds them equal).
 */
export const GATE_TREE = 'local/gate-tree';
export const GATE_TREE_LOCK = 'local/gate-tree.lock';
export const GATE_TREE_RECORD = 'local/gate-tree.json';

/** GATEWT1: the folders whose `package-lock.json` the gate tree installs from, as AGENTS.md has a fresh checkout do. */
export const DEPENDENCY_FOLDERS = ['.', 'src/Daoris.Web'];

/**
 * The gates that run the `Process` test category (MOD8): every test in one starts a real process or runs a
 * real tick, so a failure there may be the load and is re-run alone once. Named by the settings file those
 * gates run with, not by a list of classes: a list goes stale the day a new real-process class is written,
 * and one did (a landing plugin's and a protocol chat's flakes read as plain failures).
 */
export const isProcessGate = (gate) => /process\.runsettings/.test(gate.run);

/**
 * The long gates (GATE5): the real-process halves and the deployment rehearsal, which took 30 to 70 minutes
 * of a merge between them. A merge never runs one; the full set does, and a stage requires it. Named by
 * what they run, as `isProcessGate` is, so a renamed gate or a new Process half is still one.
 */
export const isLongGate = (gate) => isProcessGate(gate) || /\brehearse:deploy\b/.test(gate.run);

/**
 * The Process lane (GATELANES1): the gates that start real processes, run one at a time beside the light lane. The
 * real-process halves, and every `npm run rehearse*` script. Read from what each runs, as `isProcessGate` is: the
 * workflow's own rehearsals are in no declaration, and the devkit reads `daoris.gates.json` too.
 */
export const isProcessLane = (gate) => isProcessGate(gate) || /^npm run rehearse/.test(gate.run.trim());

/** The driver's git half (PROC1c): a Process half run with `git.process.runsettings`, its git-only classes four at once. */
export const isGitHalf = (gate) => /git\.process\.runsettings/.test(gate.run);

/**
 * The rehearsals that may run beside a git half (REHLANE1), by the root script each runs. Each builds the desktop's projects,
 * which the Process halves build into and run from, only into an output of its own (`ownBuild` in `tools/rehearsal-kit.mjs`,
 * and `desktop-publish --artifacts-path` for the deployment rehearsal's publishes), or builds no .NET project at all, and
 * shuts no build server down: a test reads each one's tool for both. They run one after the other, in this order, never
 * beside each other; the header's REHLANE2 and REHLANE2b sections say why the release and deployment rehearsals may run here
 * and not beside the family or each other.
 */
export const BESIDE_GIT_HALF = Object.freeze(['rehearse', 'rehearse:family', 'rehearse:deploy']);

/** Whether a gate runs a rehearsal of `BESIDE_GIT_HALF` (REHLANE1), read from its command as the lanes are. */
export const runsBesideGitHalf = (gate) => BESIDE_GIT_HALF.includes(scriptNamed(gate.run) ?? '');

/**
 * The heavy light gates (GATEORDER1): the suites that load the CPU for minutes (the CLI's verify, the .NET test suites,
 * the web's build and end-to-end run, and its unit run, WEBSPLIT1). The 0b44c7a run started `driver-process` beside cli, service and web, and two
 * timing tests failed there and passed alone. A Process gate starts after these, not only after its own component's.
 * Named by what a gate runs, as `isProcessLane` is. The cheap light gates (universal, code-map, orient-index) are not.
 */
export const isHeavyGate = (gate) => !isProcessLane(gate)
  && (/^npm run (verify|test:web)\b/.test(gate.run.trim()) || /^dotnet test\b/.test(gate.run.trim()));

/**
 * The component a `dotnet test|build|run|publish` command builds (GATELANES1): the first two folders of the project it
 * names (`src/Daoris.Desktop`), or null when it names none. A project references only projects of its own component
 * (a test holds it), so gates of two components never build one project.
 */
export function dotnetComponent(run) {
  const command = run.trim().replace(/^node tools\/as-merged\.mjs\s+/, '');
  const verb = /^dotnet\s+(?:test|build|run|publish)(?=\s|$)(.*)$/.exec(command);
  if (!verb) return null;
  const args = verb[1].trim().split(/\s+/).filter(Boolean);
  const at = args.indexOf('--project');
  const path = at >= 0 ? args[at + 1] : args[0];
  if (!path || path.startsWith('-')) return null;
  const folders = path.replace(/^["']|["']$/g, '').split(/[\\/]+/).filter((part) => part && part !== '.');
  return folders.length ? folders.slice(0, 2).join('/') : null;
}

/**
 * How many light gates run at once (GATELANES2). This repository's light gates build three components between them
 * (the devkit's, with the CLI's verify, which ends in its universal gates; the service's, with the web gate, which builds
 * the HTTP host its own build writes into; the desktop's) and one builds none (orient-index, 2 to 3 s). So three places
 * let each component's gates run in their own while a fourth would only ever hold orient-index. Measured from the
 * 2026-10-18 merges' logs: the seven light gates of a merge took 394 to 434 s in turn; two places would take the longer
 * of the devkit's 175 s and the service's and the desktop's together, 257 s; three take the devkit's 175 s. A full set
 * that runs the web gate ends no sooner with three than with two (the service's gates then web, about 10 minutes under
 * load, are its floor), so three is the width, on 22 logical processors.
 *
 * WEBSPLIT1 took the web gate's vitests off that floor as a gate that builds none, `web-unit`. It takes the first free
 * place `web` does not take: `web` is declared before it, so a place service frees goes to `web`, and the floor is service
 * then the page's build and browser suite. By the figures above the desktop's fast halves end before service, so it runs
 * beside service. A
 * fourth place would start it at once, beside three heavy gates, for no shorter floor: TESTWAIT2's 600 ms bound already
 * failed beside two, so the width stays three until the parent's full sets measure otherwise.
 */
export const LIGHT_WIDTH = 3;

/** The root script an `npm run <script>` command names, past its flags, or null. */
const scriptNamed = (command) => /^npm\s+run\s+(?:-\S+\s+)*([^\s-]\S*)/.exec(command.trim())?.[1] ?? null;

/**
 * The .NET components a gate builds (GATELANES2): its command's (`dotnetComponent`), and each one a root script it names
 * builds, read through the scripts those name in turn. The CLI's `npm run verify` ends in the devkit's universal gates
 * and `npm run test:web` builds the HTTP host, so each shares a component with a .NET gate, and no two gates that build
 * one component run at once. A gate that builds none (orient-index) is a component of its own.
 */
export function gateComponents(gate, scripts = {}) {
  const found = new Set();
  const read = (command, seen) => {
    const own = dotnetComponent(command);
    if (own) found.add(own);
    const script = scriptNamed(command);
    if (!script || seen.has(script) || typeof scripts[script] !== 'string') return;
    for (const part of scripts[script].split(/&&|\|\||;/)) read(part, new Set([...seen, script]));
  };
  read(gate.run, new Set());
  return [...found];
}

/** The options of `dotnet test` that change what it runs, not what it builds: each takes a value. */
const RUN_ONLY = new Set(['--filter', '--settings', '-s', '--logger', '-l', '--results-directory']);

/**
 * What a `dotnet test <project>` command builds (GATEBUILD1), as a key two commands share when they build the same: the
 * project, its path's separators and quotes read away, and every option but those that only choose what runs. A Process
 * half and the fast half of one test project differ by their `--filter` and `--settings` alone. Null for any other
 * command, and for one whose project is not its first word: a build this cannot read is a build that runs.
 */
export function testBuild(run) {
  const words = run.trim().match(/"[^"]*"|'[^']*'|\S+/g) ?? [];
  if (words[0] !== 'dotnet' || words[1] !== 'test' || !words[2] || words[2].startsWith('-')) return null;
  const project = words[2].replace(/^["']|["']$/g, '').split(/[\\/]+/).filter((part) => part && part !== '.').join('/');
  const options = [];
  for (let i = 3; i < words.length; i += 1) {
    const [name] = words[i].split('=');
    if (!RUN_ONLY.has(name)) options.push(words[i]);
    else if (!words[i].includes('=')) i += 1;
  }
  return ['dotnet test', project, ...options].join(' ');
}

/** A `dotnet test` command that runs what is already built (GATEBUILD1). */
export const noBuildCommand = (run) => `${run.trim()} --no-build`;

/**
 * Whether a Process half runs its fast half's build (GATEBUILD1): only when that half passed in this run on the tree the
 * Process half starts on. A changed tree builds, and so does a tree git could not name.
 */
export const reuseBuild = (built, tree) => Boolean(built?.tree && tree && built.tree === tree);

/**
 * The two lanes of a run (GATELANES1), and what each Process-lane gate starts after. Two builds of one project at once
 * fight over its `obj/` and `bin/`, so a Process-lane gate whose command names a component waits for the light gates of
 * that component, which run first in their lane, and one that names none (a rehearsal: it builds the hosts behind its
 * script's name, and the release and deployment rehearsals stage the dist/ the cli gate's bin prefers) waits for the whole
 * light lane. Within each rule, the plan's order holds.
 *
 * GATELANES2: up to `width` light gates run at once, never two that build one component (`components`, read through
 * the root `scripts` a gate names). GATEBUILD1: `reuses` names, for each Process half, the light gate that builds the
 * same test project the same way (`testBuild`), its fast half.
 *
 * REHLANE1: `beside` names, for each Process-lane gate, the ones it may run beside: a git half and each rehearsal of
 * `BESIDE_GIT_HALF`. The lane's order is the plan's, but each such rehearsal goes right after the last git half, and a
 * Process-lane gate starts once every gate before it in the lane has ended but those it may run beside. REHLANE2: those
 * rehearsals are never beside each other, so each starts once the one before it has ended, all beside the git half
 * (REHLANE2b: the release, the family, then the deployment).
 */
export function laneSchedule(gates, { scripts = {}, width = LIGHT_WIDTH } = {}) {
  const lane = gates.filter(isProcessLane);
  const besideGit = lane.some(isGitHalf) ? lane.filter(runsBesideGitHalf) : [];
  const process = lane.filter((gate) => !besideGit.includes(gate));
  process.splice(process.findLastIndex(isGitHalf) + 1, 0, ...besideGit);
  const beside = {};
  for (const gate of process) {
    const others = isGitHalf(gate) ? besideGit : besideGit.includes(gate) ? process.filter(isGitHalf) : [];
    if (others.length) beside[gate.name] = others.map((other) => other.name);
  }
  const planned = gates.filter((gate) => !isProcessLane(gate));
  const components = Object.fromEntries(planned.map((gate) => [gate.name, gateComponents(gate, scripts)]));
  const sharing = (component) => planned.filter((other) => component !== null && components[other.name].includes(component));
  const first = new Set(process.flatMap((gate) => sharing(dotnetComponent(gate.run))).map((gate) => gate.name));
  const light = [...planned.filter((gate) => first.has(gate.name)), ...planned.filter((gate) => !first.has(gate.name))];
  const waits = Object.fromEntries(process.map((gate) => {
    const component = dotnetComponent(gate.run);
    // GATEORDER1: beside the component's own gates, the heavy light gates, so a Process half's timing tests run with no suite loading the CPU.
    const after = component === null ? light : light.filter((other) => sharing(component).includes(other) || isHeavyGate(other));
    return [gate.name, { after: after.map((other) => other.name), component }];
  }));
  const reuses = {};
  for (const gate of process) {
    const build = testBuild(gate.run);
    const fast = build && planned.find((other) => testBuild(other.run) === build);
    if (fast) reuses[gate.name] = fast.name;
  }
  return { light, process, waits, width: Math.max(1, width), components, reuses, beside };
}

/** The root `package.json`'s scripts, which `gateComponents` reads behind a gate's `npm run`; none when it has none. */
function rootScripts(root) {
  try {
    const { scripts } = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));
    return scripts && typeof scripts === 'object' ? scripts : {};
  } catch {
    return {};
  }
}

export const USAGE = [
  'usage: node tools/merge-branch.mjs <branch> [--batch <branch>…] [--full | --gated] [--keep-going] [--one-at-a-time] [--no-commit-check] [--no-prune]',
  '       node tools/merge-branch.mjs --continue [--full | --gated] [--keep-going] [--one-at-a-time] [--no-prune]',
  '       node tools/merge-branch.mjs --rerun <gate>… [--keep-going] [--one-at-a-time]',
  '       node tools/merge-branch.mjs --stale [--keep-going] [--one-at-a-time]',
  '       node tools/merge-branch.mjs --plan <branch> [--batch <branch>…] [--full]',
  '       node tools/merge-branch.mjs --full [--keep-going] [--one-at-a-time]',
  '       node tools/merge-branch.mjs --full --beside [<commit>] [--keep-going] [--one-at-a-time]',
  '       node tools/merge-branch.mjs --passed',
  '       node tools/merge-branch.mjs --prune [--plan]',
  '       node tools/merge-branch.mjs --drop-batch',
].join('\n');

/** Why the tool stopped before doing its job. Exit 2 is a refusal; 1 is the branch failing a rule. */
export class Refusal extends Error {
  constructor(message, exitCode = 2, usage = false) {
    super(message);
    this.exitCode = exitCode;
    this.usage = usage;
  }
}

const usageError = (message) => new Refusal(message, 2, true);

// ---------------------------------------------------------------------------------------------------
// Arguments

export function parseArgs(argv) {
  const options = {
    branches: [], keepGoing: false, commitCheck: true, resume: false, dropBatch: false, plan: false, prune: false, autoPrune: true,
    full: false, gated: false, rerun: [], passed: false, stale: false, oneAtATime: false, beside: false, besideCommit: null,
  };
  let batch = false;
  let rerun = false;
  let positional = 0;
  let batched = 0;
  let besideNext = false;
  const flags = [];
  for (const arg of argv) {
    if (arg.startsWith('-')) flags.push(arg);
    // GATEWT1: the word right after --beside, if it is no option, is the commit it gates.
    const besideCommit = besideNext && !arg.startsWith('-');
    besideNext = false;
    if (besideCommit && !rerun) options.besideCommit = arg;
    else if (arg === '--beside') {
      if (options.beside) throw usageError('--beside is given once, with the commit it gates after it');
      options.beside = true;
      besideNext = true;
    } else if (arg === '--batch') {
      if (batch) throw usageError('--batch is given once, before the branches it adds');
      if (positional === 0) throw usageError('--batch follows the first branch: <branch> --batch <branch>…');
      batch = true;
    } else if (arg === '--keep-going') options.keepGoing = true;
    else if (arg === '--one-at-a-time') options.oneAtATime = true;
    else if (arg === '--no-commit-check') options.commitCheck = false;
    else if (arg === '--continue') options.resume = true;
    else if (arg === '--drop-batch') options.dropBatch = true;
    else if (arg === '--plan') options.plan = true;
    else if (arg === '--prune') options.prune = true;
    else if (arg === '--no-prune') options.autoPrune = false;
    else if (arg === '--full') options.full = true;
    else if (arg === '--gated') options.gated = true;
    else if (arg === '--passed') options.passed = true;
    else if (arg === '--stale') options.stale = true;
    else if (arg === '--rerun') rerun = true;
    else if (arg.startsWith('-')) throw usageError(`unknown option '${arg}'`);
    else if (rerun) {
      // GATE4: what follows --rerun are gates, not branches.
      if (options.rerun.includes(arg)) throw usageError(`'${arg}' is named twice`);
      options.rerun.push(arg);
    } else {
      if (batch) batched += 1;
      else if (++positional > 1) throw usageError('two branches without --batch: name the rest after --batch');
      if (options.branches.includes(arg)) throw usageError(`'${arg}' is named twice`);
      options.branches.push(arg);
    }
  }
  if (options.beside) {
    if (!options.full) throw usageError('--beside runs the full set: --full --beside [<commit>]');
    if (options.branches.length) throw usageError("--beside takes no branch: it gates a commit, main's tip unless named, in a worktree of its own");
    if (flags.some((flag) => !['--full', '--beside', '--keep-going', '--one-at-a-time'].includes(flag))) {
      throw usageError('--full --beside takes only a commit, --keep-going and --one-at-a-time');
    }
  }
  // `--plan` is a verb of its own, or the prune's plan; `--full` is one when it names no branch and modifies nothing else.
  const fullAlone = options.full && options.branches.length === 0 && !options.plan && !options.resume && !options.prune && !rerun && !options.stale;
  const verbs = [options.resume, options.dropBatch, options.plan && !options.prune, options.prune, rerun, options.passed, options.stale, fullAlone];
  if (verbs.filter(Boolean).length > 1) {
    throw usageError('give one of --continue, --rerun, --stale, --drop-batch, --plan, --passed, --prune (which takes --plan) and --full on its own');
  }
  if (batch && batched === 0) throw usageError('--batch names at least one branch after it');
  if (rerun) {
    if (options.rerun.length === 0) throw usageError('--rerun names the gates to run again: --rerun <gate>…');
    if (flags.some((flag) => !['--rerun', '--keep-going', '--one-at-a-time'].includes(flag))) {
      throw usageError('--rerun takes gate names, --keep-going and --one-at-a-time');
    }
    if (options.branches.length) throw usageError('--rerun takes no branch: it acts on the merge in place, or on the checkout when none is open');
  } else if (options.stale) {
    if (options.branches.length) throw usageError('--stale takes no branch: it acts on the checkout as it stands');
    if (flags.some((flag) => !['--stale', '--keep-going', '--one-at-a-time'].includes(flag))) {
      throw usageError('--stale takes only --keep-going and --one-at-a-time');
    }
  } else if (options.passed) {
    if (options.branches.length) throw usageError('--passed takes no branch: it asks about the checkout as it stands');
  } else if (options.prune) {
    if (!options.autoPrune) throw usageError('--no-prune is for a merge; --prune is the prune itself');
    if (options.full) throw usageError('--full is for a merge or the checkout; --prune runs no gate');
    if (options.gated) throw usageError('--gated is for a merge; --prune runs no gate');
    if (options.oneAtATime) throw usageError('--one-at-a-time is for a run of gates; --prune runs no gate');
    if (options.branches.length) throw usageError('--prune takes no branch: it acts on every branch merged into main');
  } else if (options.resume || options.dropBatch) {
    if (options.branches.length) {
      throw usageError(`${options.resume ? '--continue' : '--drop-batch'} takes no branch: it acts on the batch already recorded`);
    }
  } else if (options.branches.length === 0 && !(options.full && !options.plan)) {
    // `--full` alone gates the checkout as it stands (GATE3); everything else names a branch.
    throw usageError('name the branch to merge');
  }
  return options;
}

// ---------------------------------------------------------------------------------------------------
// The plan: the declared gates, plus the workflow's own rehearsals, fast first

/** A name safe as one path segment: a log file, a scratch folder. */
export const slug = (name) => name.replace(/[^A-Za-z0-9._-]+/g, '-').replace(/^-+|-+$/g, '') || 'gate';

/**
 * What kind of gate a command is, which orders it and never drops it: a devkit `dotnet run` and a tool's
 * `--check` (the orientation index, ORIENT1a) are checks, `npm run rehearse*` and `npm run test:*` are
 * rehearsals, and anything else is a suite. A command run through `tools/as-merged.mjs` (GATE1) is the kind of
 * what it runs: the universal gates read history as the merge would commit it, and are still the devkit's check.
 */
export function gateKind(run) {
  const command = run.trim().replace(/^node tools\/as-merged\.mjs\s+/, '');
  if (/^dotnet run\b/.test(command) || /^node tools\/\S+\.mjs --check$/.test(command)) return 'check';
  const script = /^npm run (\S+)$/.exec(command)?.[1];
  if (script && /^(rehearse|test:)/.test(script)) return 'rehearsal';
  return 'suite';
}

/** The scripts a workflow runs as whole steps (`run: npm run <script>`), in file order, each once. */
export function workflowScripts(text) {
  const scripts = text.replace(/\r\n/g, '\n').split('\n')
    .filter((line) => !/^\s*#/.test(line))
    .map((line) => /^\s*(?:-\s+)?run:\s*npm run (\S+)\s*$/.exec(line)?.[1])
    .filter(Boolean);
  return [...new Set(scripts)];
}

/**
 * The ordered gates: every declared gate, and every workflow script the declaration does not run. By
 * kind, fast first; within a kind, workflow-only gates first, then the declared order.
 */
export function gatePlan(declared, workflow = '') {
  const runs = new Set(declared.map((gate) => gate.run.trim()));
  const extras = workflowScripts(workflow)
    .filter((script) => !runs.has(`npm run ${script}`))
    .map((script) => ({ name: script, run: `npm run ${script}` }));
  const taken = new Set();
  return [...extras, ...declared]
    .map((gate, index) => ({ gate, index, kind: gateKind(gate.run) }))
    .sort((a, b) => KINDS.indexOf(a.kind) - KINDS.indexOf(b.kind) || a.index - b.index)
    .map(({ gate, kind }) => {
      let name = slug(gate.name);
      for (let n = 2; taken.has(name); n += 1) name = `${slug(gate.name)}-${n}`;
      taken.add(name);
      return {
        name,
        run: gate.run.trim(),
        kind,
        ...(gate.cwd ? { cwd: gate.cwd } : {}),
        ...(gate.clock ? { clock: true } : {}),
      };
    });
}

/** `daoris.gates.json`'s gates, read as the devkit reads them: a name, a command line, an optional cwd. */
export function readDeclared(root) {
  const file = join(root, 'daoris.gates.json');
  if (!existsSync(file)) throw new Refusal(`no daoris.gates.json at ${root}: there is no declared gate to run`);
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    throw new Refusal(`daoris.gates.json is not valid JSON: ${error.message}`);
  }
  const gates = Array.isArray(parsed?.gates) ? parsed.gates : [];
  if (gates.length === 0) throw new Refusal('daoris.gates.json declares no gate');
  for (const gate of gates) {
    if (typeof gate?.name !== 'string' || typeof gate?.run !== 'string') {
      throw new Refusal("daoris.gates.json: every gate needs both 'name' and 'run'");
    }
  }
  // FAMILYFAIL1d: `"clock": true` marks a gate whose tests are bounded by the wall clock, which a run beside raises.
  return gates.map(({ name, run, cwd, clock }) => ({
    name, run, ...(typeof cwd === 'string' ? { cwd } : {}), ...(clock === true ? { clock: true } : {}),
  }));
}

export function readPlan(root) {
  const workflow = join(root, '.github', 'workflows', 'release.yml');
  return gatePlan(readDeclared(root), existsSync(workflow) ? readFileSync(workflow, 'utf8') : '');
}

// ---------------------------------------------------------------------------------------------------
// GATE3: the gates a merge's paths can reach

/**
 * The gates every merge runs, whatever it changed: the universal gates scan every file, the code map
 * maps all the code, the orientation index reads every lane (ORIENT1a), and the CLI's `verify` holds the
 * records, the lanes, the canon and the tools' tests.
 */
export const BASELINE = Object.freeze(['universal', 'code-map', 'orient-index', 'cli']);

/**
 * GATESLIM1: what a merge blocks on by default: the universal gates (which hold the sensitive-patterns scan, never to reach a
 * commit), the code map and the orientation index's check. Every other gate is listed and skipped, "in the CI beside"
 * (`--full --beside`); `--gated` runs what the merge's paths reach, `--full` every gate, `--rerun <gate>` a named one.
 */
export const BLOCKING = Object.freeze(['universal', 'code-map', 'orient-index']);

// The .NET suites with a test that reads the page's catalogues or sources: MOD9's incident was two of them.
const PAGE_READERS = ['service', 'driver', 'modules'];
const DESKTOP_SUITES = ['driver', 'modules', 'driver-process', 'driver-process-git', 'modules-process'];
// What the CLI's sources reach; a narrow rule for one of its files names these as well, since the first rule decides.
const CLI_READERS = ['driver', 'rehearse', 'rehearse-family', 'web', 'deployment'];
// What the modules lane's sources reach, named by its narrow rule for the C# sources as well (MOD9c).
const MODULES_READERS = ['modules', 'modules-process', 'deployment'];
// What the driver lane's sources reach, named by its narrow rule for the sources the page's twins parse as well (MOD9e).
const DRIVER_READERS = [...DESKTOP_SUITES, 'rehearse-family', 'deployment'];

/**
 * Which gates can see a changed path, beyond the baseline: the first rule that places the path decides.
 * A rule places paths by glob (`paths`, the lanes file's grammar) or by a lane of `daoris.lanes.json`
 * (`lane`). Narrow rules come before the lanes, and the laneless paths after. `'*'` is every gate. A
 * path no rule places runs every gate, and so does a gate no rule names. A gate named here that the
 * repository does not run names nothing.
 *
 * Held by `merge-branch.test.ts` to what each .NET suite reads of the repository, to what each rehearsal
 * imports and starts, and to every tracked file and lane. What a fast suite may read is given generously:
 * it costs minutes. The precision is in the Process halves and the rehearsals, which cost the hours.
 */
export const REACH = Object.freeze([
  {
    paths: ['daoris.gates.json', 'daoris.lanes.json', 'package.json', 'package-lock.json', '.gitattributes', '.gitignore', 'tools/fsx.mjs'],
    gates: '*',
    why: 'it changes how every gate runs or reaches a path: the gates, lanes, scripts, line endings, what is tracked, or the file helpers every rehearsal imports',
  },
  // GATESLIM1: the tests of the tools only the parent runs (the merge tool, the install's publisher, the family rehearsal's kit) are
  // the tool-tests gate, no longer part of verify. These rules come first and keep what each path reached before.
  {
    paths: ['tools/merge-branch.mjs', 'tools/doc-duplicates.mjs', 'src/Daoris.Cli/tool-test/**', 'src/Daoris.Cli/test/_fixture.ts'],
    gates: ['tool-tests'],
    why: "the merge tool's, the publisher's and the set-up kit's tests, which `tool-tests` runs and verify does not (GATESLIM1): the merge tool and what it imports, the tests, and the fixture they share",
  },
  {
    paths: ['tools/rehearsal-kit.mjs'],
    gates: ['tool-tests', 'rehearse', 'rehearse-family', 'deployment'],
    why: "the rehearsals' kit, which the set-up kit's tool-tests import, and the port test it takes each run's ports with (REHEARSEPORT1, GATESLIM1)",
  },
  {
    paths: ['tools/cdp.mjs'],
    gates: ['rehearse', 'rehearse-family', 'deployment'],
    why: "the port test the rehearsals take each run's ports with (REHEARSEPORT1)",
  },
  { paths: ['tools/release-rehearsal.mjs', 'tools/release-prep.mjs'], gates: ['rehearse'], why: 'release tooling' },
  {
    paths: ['tools/service-publish.mjs'],
    gates: ['tool-tests', 'rehearse', 'deployment', 'rehearse-family'],
    why: "release tooling, and the hosts' one build recipe (CONNECTOR1), which desktop-publish imports: the deployment rehearsal drives it, the family rehearsal runs usage-report, which imports it, and the publish's tool-tests call it (GATESLIM1)",
  },
  { paths: ['tools/stage-package.mjs'], gates: ['rehearse', 'deployment'], why: "every pack runs it: the release rehearsal's, and the install's doctrine tool" },
  { paths: ['tools/family-rehearsal.mjs', 'tools/usage-report.mjs'], gates: ['rehearse-family'], why: 'the family rehearsal, what it imports, and what it runs' },
  { paths: ['tools/setup-kit.mjs'], gates: ['tool-tests', 'rehearse-family'], why: "the family rehearsal's set-up kit, and its tool-tests, which run it for real in a scratch repository (GATESLIM1)" },
  {
    paths: ['tools/desktop-publish.mjs'],
    gates: ['tool-tests', 'deployment', 'rehearse-family'],
    why: 'the publish the deployment rehearsal drives, and what it imports; the family rehearsal runs usage-report, which imports it; and its tool-tests (GATESLIM1)',
  },
  {
    paths: ['tools/processes.mjs'],
    gates: ['tool-tests', 'deployment', 'rehearse-family', 'rehearse'],
    why: "what the publish imports (the rule above), and what the rehearsals' kit ends a timed-out command's leftovers with, so all three rehearsals (FAMILYFAIL1)",
  },
  { paths: ['tools/deployment-rehearsal.mjs', 'tools/desktop.mjs'], gates: ['deployment'], why: 'the deployment rehearsal and what it imports' },
  { paths: ['src/Daoris.Devkit/**'], gates: ['devkit'], why: 'its own suite; it is also the universal gates and the code map, which every merge runs' },
  { paths: ['src/Daoris.Desktop/process.runsettings'], gates: ['driver', 'driver-process', 'modules-process'], why: "the Process halves' settings (MOD8), which the driver's CategoryTests read (PROCPAR1)" },
  { paths: ['src/Daoris.Desktop/git.process.runsettings'], gates: ['driver-process-git'], why: "the driver's git-only Process classes' settings, four at once (PROC1c)" },
  // Before the driver's tests, which would place it first: the page's twin reads it too.
  // WEBSPLIT1: a vitest's read reaches the web gate's unit run, which runs the vitests; the web gate builds and runs Playwright.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/kept-names.json'],
    gates: ['driver', 'web-unit'],
    why: "the driver's KeptNamesTwinTests and the page's sessionActs vitest read the kept names' table (SESSDEL1c)",
  },
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/workflow-run-said.json'],
    gates: ['driver', 'web-unit'],
    why: "the driver's WorkflowRunSaidTwinTests and the page's runSaid vitest read a run's English table (WORKFLOW1c5)",
  },
  {
    paths: ['src/Daoris.Desktop/Directory.Build.props','src/Daoris.Desktop/Directory.Packages.props'],
    gates: [...DESKTOP_SUITES, 'rehearse-family', 'deployment'],
    why: 'every desktop project builds with it: the suites, the headless host the family rehearsal drives, and the deployed shell',
  },
  { paths: ['src/Daoris.Desktop/README.md'], gates: ['modules', 'service'], why: "the modules' route test reads its table, and the service's suite reads this repository's documents" },
  // Before the driver's tests, which would place it first: the service's tool test reads it too. No Process class reads it.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/review-rules.json'],
    gates: ['driver', 'service'],
    why: "the driver's ReviewRulesTests and the service's HelpSettingProposalTests read the review rules' table (REVIEWENV1c4); the CLI's twin runs in verify",
  },
  // XAGENT1f4: the same for the second-opinion rules' table, whose `gate` rows the service's tool test reads too.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/opinion-rules.json'],
    gates: ['driver', 'service'],
    why: "the driver's OpinionRulesTests and the service's HelpSettingProposalTests read the second-opinion rules' table (XAGENT1f4); the CLI's twin runs in verify",
  },
  // BRAINSTORM1a2: the brainstorm answers' table sits among the driver's tests, and the service's twin validator reads it too.
  // BRAINSTORM1a3: so does the brainstorm files' table, which the service's brainstorm desk reads.
  {
    paths: [
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/brainstorm-answers.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/brainstorm-files.json',
    ],
    gates: ['driver', 'service'],
    why: "the driver's BrainstormValidationTests and BrainstormFilesTwinTests and the service's McpBrainstormToolTests and BrainstormDeskTests read the brainstorm tables (BRAINSTORM1a2, BRAINSTORM1a3)",
  },
  // Before the driver's tests, which would place it first: the modules' test reads it too (BRSCOPE1b).
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/checkout-scope.json'],
    gates: ['driver', 'modules'],
    why: "the driver's TreesCommandTests and the modules' DriverModuleLinesTests read the checkout-scope table (BRSCOPE1b)",
  },
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/**'], gates: ['driver', 'driver-process', 'driver-process-git'], why: "the driver's tests, the fast half and both Process halves" },
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/**'], gates: ['modules', 'modules-process'], why: "the modules' tests, both halves" },
  // Before the service's tests, which would place it first: the driver's twin reads it too.
  {
    paths: ['src/Daoris.Service/Daoris.Service.Tests/fixtures/history-words.json'],
    gates: ['service', 'driver'],
    why: "the service's and the driver's HistoryWordsTwinTests read the history words' table (HIST1m)",
  },
  { paths: ['src/Daoris.Service/Daoris.Service.Tests/**', 'src/Daoris.Service/Daoris.Service.Http.Tests/**'], gates: ['service'], why: "the service's tests" },
  { paths: ['src/Daoris.Cli/test/fixtures/vendor/**'], gates: ['driver', 'driver-process'], why: "the driver's release-channel tests read the vendor's files" },
  { paths: ['src/Daoris.Cli/test/fixtures/shell-words.json'], gates: ['driver', 'web-unit'], why: "the driver's ShellWord twin and the page's shellWord read the shell-word table (ACCTQUOTE1b)" },
  { paths: ['src/Daoris.Cli/test/fixtures/account-reads.json'], gates: ['driver'], why: "the driver's AccountReads twin reads the shared reads table (AGENTREAD1b)" },
  { paths: ['src/Daoris.Cli/test/fixtures/rotation-use.json'], gates: ['driver', 'web-unit'], why: "the driver's RotationUseTwinTests and the page's curve rules read the shared rotation-use table (SWITCH1a, SWITCH1d)" },
  { paths: ['src/Daoris.Cli/test/fixtures/landing-says.json'], gates: ['driver'], why: "the driver's LandingSaysTwinTests read the shared landing, helpers and commits sentences (TWINWORDS1)" },
  { paths: ['src/Daoris.Cli/test/fixtures/usage-curves.json'], gates: ['driver', 'modules', 'web-unit'], why: "the driver's UsageCurvesTwinTests, the modules' DriverModuleAccountsTests (the verdicts .NET pushes, PUSH1e) and the page's curve rules read the shared usage curve table (SWITCH1b-iii, SWITCH1d)" },
  { paths: ['src/Daoris.Cli/test/fixtures/name-case.json'], gates: ['driver'], why: "the driver's NameCaseTests reads the shared name-case table (CASEFOLD1d)" },
  { paths: ['src/Daoris.Cli/test/fixtures/graph-homes.json'], gates: ['driver'], why: "the driver's NodeGraphHomesTests reads the shared table of which cycle governs a unit (FABRIC-GRAPH1)" },
  { paths: ['src/Daoris.Cli/test/fixtures/driver-config-keeps.json'], gates: ['driver'], why: "the driver's DriverConfigKeepsTests reads the shared table of the bytes an edit of driver.json writes (FABRIC-GRAPH1's review)" },
  { paths: ['src/Daoris.Cli/test/fixtures/home-files-keep.json'], gates: ['driver'], why: "the driver's HomeFilesKeepTests reads the shared table of the bytes an edit of each other home file a door edits writes (KEEPALL1)" },
  { paths: ['src/Daoris.Cli/test/fixtures/workflow-graphs.json'], gates: ['driver'], why: "the driver's WorkflowGraphsTests reads the shared table of D157's versions decoded as graphs (FABRIC-WORKFLOW2)" },
  { paths: ['src/Daoris.Cli/test/fixtures/workflow-graph-versions.json'], gates: ['driver'], why: "the driver's WorkflowGraphVersionsTests reads the shared table of a named graph as a named workflow's version (FABRIC-GRAPH1b)" },
  { paths: ['src/Daoris.Cli/test/fixtures/graph-problems.json'], gates: ['driver'], why: "the driver's NodeWiresTests and NodeGraphsTests read the shared table of a graph judged when saved (FABRIC-GRAPH1c)" },
  {
    paths: ['src/Daoris.Cli/test/fixtures/fence-cases.json'],
    gates: ['service', 'driver'],
    why: "the service's MarkdownFence and the driver's SelfDescription fence twins read the shared fence table (ORIENT2h6, ORIENT2h6b)",
  },
  { paths: ['tools/orient-index-fixtures/**'], gates: ['service'], why: "the service's DecisionNotes twin reads the digest's note table (ORIENT1h)" },
  // STRUCT5d (D166): by its name at any depth of the CLI's sources, which sit in module folders.
  {
    paths: ['src/Daoris.Cli/src/**/documents.ts'],
    gates: ['service', ...CLI_READERS],
    why: "the CLI's own, and the service's roles twin reads its ROLES from the file (ORIENT2e2)",
  },
  {
    paths: ['.claude/knowledge/adoption.md'],
    gates: ['service', 'driver'],
    why: "the driver's SetupBrief twin reads the playbook's steps (LAYOUT7, MOD9b), and the service's suite scans this repository's documents",
  },
  // MOD9c: the driver's scans enumerate every C# source of the modules and the app, to hold the next spawn site or ask
  // written there, so the files are not named one by one: today's would miss that next one.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Modules/**/*.cs', 'src/Daoris.Desktop/Daoris.Desktop.App/**/*.cs'],
    gates: [...MODULES_READERS, 'driver'],
    why: "the modules' own, and the driver's NoConsoleWindow and PullRequestOccasion scans read every C# source of the modules and the app (MOD9c)",
  },
  // MOD9e: the page's twin vitests parse these sources' declarations, so they are named by file: the driver's other sources
  // reach no vitest. WEBSPLIT1: the vitests are the web gate's unit run, which builds nothing. STRUCT1 (D166): each by its name at any depth of the
  // driver's folder, since the driver's files move into module folders one module per branch.
  {
    paths: [
      'NoteCodes.cs', 'SessionEvents.cs', 'InstructionAccount.cs', 'Trace.Chain.cs', 'AutoLanding.cs', 'Landing.cs',
      // MSG1g3: the page's say twin holds its refusal tables to these codes.
      'GoOnNew.cs', 'SessionSay.cs', 'Continuation.cs', 'ChatRunner.StartFrom.cs',
      // MIXAGENT1b-i: the say twin holds the hand-over's refusal table to its codes.
      'HandOver.cs',
    ].map((name) => `src/Daoris.Desktop/Daoris.Desktop.Driver/**/${name}`),
    gates: [...DRIVER_READERS, 'web-unit'],
    why: "the driver's own, and the page's note, conversation, handed, trace and say vitests parse their declarations (MOD9e, MSG1g3)",
  },
  // BRAINSTORM1a2: the service's McpToolRegistrationTests holds the brainstorm posture's list of the connector's tools to its
  // host's, reading the driver's file by its name; a path rule replaces its lane's, so the driver's readers stay.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver/**/BrainstormPermissions.cs'],
    gates: [...DRIVER_READERS, 'service'],
    why: "the driver's own, and the service's McpToolRegistrationTests reads the brainstorm posture's list of the connector's tools (BRAINSTORM1a2)",
  },
  // GATESLIM1: desktop-publish.test.ts (the tool-tests) imports the tarball reader from the CLI's sources; a path rule replaces the cli lane's.
  { paths: ['src/Daoris.Cli/src/**/tarball.ts'], gates: [...CLI_READERS, 'tool-tests'], why: "the CLI's own readers (the lane's), and the publish's tool-tests import it (GATESLIM1)" },
  // CONFIG1: the driver's KeepLockProcessTests runs the CLI's keeping write under node beside the driver's, so a change to
  // either side of the lock reaches the driver's Process half; a path rule replaces the cli lane's.
  {
    paths: ['src/Daoris.Cli/src/**/keeplock.ts', 'src/Daoris.Cli/src/**/jsonkeep.ts', 'src/Daoris.Cli/src/driver/driverconfig.ts'],
    gates: [...CLI_READERS, 'driver-process'],
    why: "the CLI's own readers (the lane's), and the driver's KeepLockProcessTests runs these under node against the driver's lock (CONFIG1)",
  },
  { paths: ['src/Daoris.Cli/test/**'], gates: [], why: "the CLI's tests, which verify runs at every merge" },
  // GATE6: a fix to a browser test was re-gated by the web gate alone, and the stage then voided every other verdict.
  { paths: ['src/Daoris.Web/e2e/**'], gates: ['web'], why: "the page's end-to-end specs, which only the web gate runs: no .NET suite reads them" },
  { lane: 'web-shell', gates: ['web', 'web-unit', ...PAGE_READERS], why: "the page's own suites, and the .NET suites that read its catalogues and sources (MOD9's incident)" },
  { lane: 'web-settings', gates: ['web', 'web-unit', ...PAGE_READERS], why: "the page's own suites, and the .NET suites that read its catalogues and sources (MOD9's incident)" },
  {
    lane: 'driver',
    gates: DRIVER_READERS,
    why: 'the modules build on it, the family rehearsal drives its headless host, and the deployed shell runs its loop (D60): ServiceHostLocator, the staged build, a session',
  },
  // LAUNCHGATE1: the driver's tests project builds the launcher (it compiles driver files in by link), so a launcher change
  // is compiled by the driver gate as well as by the publish. A path rule replaces its lane's, so the modules' readers stay.
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Launcher/**'], gates: [...MODULES_READERS, 'driver'], why: 'the modules lane\'s own, and the driver gate builds the launcher, which links driver files (LAUNCHGATE1)' },
  { lane: 'modules', gates: MODULES_READERS, why: 'the application, its launcher and the modules it hosts: the deployed shell (D60)' },
  // PUSH1d2: the modules' SessionsStateSliceTests holds the sessions slice's fields to the service's `SessionResponse`, reading
  // the file by its path; a path rule replaces the service lane's, so its readers stay.
  {
    paths: ['src/Daoris.Service/Daoris.Service.Http/ApiContracts.cs'],
    gates: ['service', 'devkit', 'driver', 'rehearse-family', 'web', 'web-unit', 'deployment', 'modules'],
    why: "the service lane's own, and the modules' SessionsStateSliceTests holds the sessions slice's fields to its `SessionResponse` (PUSH1d2)",
  },
  {
    lane: 'service',
    gates: ['service', 'devkit', 'driver', 'rehearse-family', 'web', 'web-unit', 'deployment'],
    why: "its suite; the devkit's, the driver's and a page vitest read its sources; the family rehearsal, the page's end-to-end suite and the install run its HTTP host",
  },
  {
    lane: 'cli',
    gates: CLI_READERS,
    why: "the release rehearsal packs it, the family rehearsal and the page's end-to-end suite run it, the install carries it, and the driver's tests read its permissions",
  },
  { lane: 'tools', gates: [], why: "the rest of the tooling, which the CLI's suite tests" },
  { lane: 'records', gates: ['service'], why: "the service's suite scans this repository's own documents" },
  { paths: ['canon/**'], gates: ['service', 'rehearse', 'rehearse-family'], why: 'the package ships it, the examples must be current with it, and the service scans the doctrine synced from it' },
  {
    paths: ['examples/plugins/**'],
    gates: ['service', 'driver', 'rehearse-family', 'web', 'deployment'],
    why: "the example family the rehearsals and the end-to-end suite run on, and the offers the install lays out, which the driver's PluginOffer test reads as they stand (D103, MOD9b)",
  },
  { paths: ['examples/**'], gates: ['service', 'rehearse-family', 'web'], why: 'the example family the family rehearsal and the end-to-end suite run on' },
  { paths: ['README.md', 'LICENSE'], gates: ['service', 'rehearse'], why: 'staged into the package the release rehearsal packs' },
  {
    paths: ['docs/index/**'],
    gates: [],
    why: 'written into every merge by tools/orient-index.mjs and held by its check, which every merge runs; no suite reads it',
  },
  {
    paths: ['docs/**', 'CHANGELOG.md', 'CLAUDE.md', 'ROADMAP.md', 'AGENTS.md', 'daoris.json', 'daoris.lock', '.claude/**'],
    gates: ['service'],
    why: "the service's suite scans this repository's own doctrine and decisions",
  },
  { paths: ['.mcp.json'], gates: [], why: "the harness's settings" },
]);

// ---------------------------------------------------------------------------------------------------
// ORIENT1a: what a merge writes rather than merges

/**
 * Folders a tool writes from the tree, never merged: the orientation index's line numbers move with nearly
 * every change, so two branches' copies disagree wherever both touched a large file, and neither copy speaks
 * for the merge. So each merge writes them again from the merged tree before its gates, a conflict in one is
 * resolved by that writing, and the parent reads the result in the merge's diff. A repository without the
 * tool writes nothing.
 */
export const GENERATED = Object.freeze([{ folder: 'docs/index', tool: 'tools/orient-index.mjs' }]);

/** Whether a path is one a merge writes rather than merges. */
export const isGenerated = (path) => GENERATED.some(({ folder }) => path.startsWith(`${folder}/`));

/** Each generated folder written from the tree as it stands, and staged; one line each. A tool that fails refuses the merge's gates. */
function writeGenerated(root) {
  for (const { folder, tool } of GENERATED) {
    if (!existsSync(join(root, tool))) continue;
    const run = spawnSync(process.execPath, [tool], { cwd: root, encoding: 'utf8' });
    if (run.status !== 0) throw new Refusal(`${tool} could not write ${folder}/ into the merge:\n${(run.stderr || run.stdout).trim()}`, 1);
    git(root, ['add', '-A', '--', folder]);
    console.log(`${folder}/: written from the merged tree (${run.stdout.trim().replace(/^[\w-]+: /, '')})`);
  }
}

// ---------------------------------------------------------------------------------------------------
// MERGEJOIN1: two notes a union merge leaves touching

/**
 * A decision's file with one blank line set before each note's label that follows a non-blank line outside a fence,
 * and every other byte as it was; with each label set apart and its line once set (1-based).
 *
 * Two branches that each append a note to one decision both open it with a blank line. Git's union merge takes that
 * line, the same on both sides, as common to them and keeps it once, then keeps both notes one after the other, so
 * the second label lands straight under the first note's last line. `doc-duplicates` refuses that (D134 §3.4), and
 * on 2026-10-04 three merges in a row failed `verify` on it, each mended by hand with one blank line. What is set
 * apart is the check's own fact (`gluedLabels`), so the two never disagree. A CRLF file gains a CRLF blank line.
 */
export function setApart(text) {
  const lines = text.split('\n');
  const glued = new Set(gluedLabels(lines));
  if (glued.size === 0) return { text, set: [] };
  const out = [];
  const set = [];
  lines.forEach((line, i) => {
    if (glued.has(i)) {
      out.push(lines[i - 1].endsWith('\r') ? '\r' : '');
      set.push({ line: out.length + 1, label: line.replace(/\r$/, '') });
    }
    out.push(line);
  });
  return { text: out.join('\n'), set };
}

/** How a note set apart is named: by the first task its label names (`UX6e`, not the decision `D130`), else by its label. */
export function noteName(label) {
  const span = label.replace(/^\*+/, '').split('*')[0].trim();
  const task = /\b[A-Z]{2,}\d+[a-z]*\b/.exec(span)?.[0];
  return task ? `${task} note` : `note "${span.length > 60 ? `${span.slice(0, 60)}…` : span}"`;
}

/**
 * Each decision file the merge in place changed, with its glued notes set apart and staged; one line per note. Only
 * the decisions record as a folder carries the fact: the other union records `doc-duplicates` checks are checked for
 * a line twice, which a join cannot make, and a record still one file is checked for a number twice.
 */
function setApartNotes(root) {
  const folders = records(root).filter((record) => record.kind === 'decisions').map((record) => record.file.slice(0, -'/*.md'.length));
  if (folders.length === 0) return;
  // What the merge brought, the parent's resolutions with it; a deleted file has nothing to set apart.
  const changed = zPaths(git(root, ['diff', '-z', '--name-only', '--no-renames', '--diff-filter=d', 'HEAD']).out);
  for (const path of changed) {
    if (!folders.some((folder) => path.startsWith(`${folder}/`) && /^[^/]+\.md$/.test(path.slice(folder.length + 1)))) continue;
    const file = join(root, path);
    const { text, set } = setApart(readFileSync(file, 'utf8'));
    if (set.length === 0) continue;
    writeAtomic(file, text);
    git(root, ['add', '--', path]);
    for (const note of set) console.log(`set apart: ${basename(path, '.md')}'s ${noteName(note.label)} (${path}:${note.line})`);
  }
}

// ORIENT1c: the workspace's knowledge server, rebuilt from the merged tree

/** The tool that builds the knowledge server agents ask; a repository without it builds nothing. */
export const KNOWLEDGE_SERVER = 'tools/knowledge-server.mjs';

/**
 * Once a merge's gates pass, the knowledge server is rebuilt from the merged tree when the service's sources
 * changed, so a session started after the merge, in any worktree, runs what main holds and never builds it at
 * its start. An instrument, not a gate: a build that fails is said and fails nothing, and sessions keep the
 * build they had.
 */
function refreshKnowledgeServer(root) {
  if (!existsSync(join(root, KNOWLEDGE_SERVER))) return;
  const run = spawnSync(process.execPath, [KNOWLEDGE_SERVER, 'build'], { cwd: root, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  const last = (text) => (text ?? '').trim().split('\n').filter(Boolean).at(-1) ?? '';
  console.log(run.status === 0
    ? `knowledge server: ${last(run.stdout).replace(/^knowledge-server: /, '')}`
    : `knowledge server: NOT rebuilt, and the merge stands (${last(run.stderr) || last(run.stdout) || `exit ${run.status}`}); npm run knowledge:build says why`);
}

/** The first rule that places a path, or null: by glob, or by a lane whose paths own it. */
function ruleFor(path, rules, lanes) {
  return rules.find((rule) => {
    if (rule.paths) return laneMatcher(rule.paths)(path);
    const lane = lanes.find((candidate) => candidate.id === rule.lane);
    return lane ? laneMatcher(lane.paths)(path) : false;
  }) ?? null;
}

const andMore = (paths) => `${paths[0]}${paths.length > 1 ? ` and ${paths.length - 1} more` : ''}`;

/** Every gate some rule of the table names: a gate none names is reached by every path. */
const namedGates = (reach) => new Set(reach.flatMap((rule) => (rule.gates === '*' ? [] : rule.gates)));

/**
 * Which of the plan's gates a merge runs, and why each runs or is skipped, in the plan's order. A gate is
 * `reached` when it is the baseline, a changed path's rule names it, a path has no rule, or no rule names
 * the gate at all. A merge runs what is reached but the long gates (GATE5), which say they are in the full
 * set before staging; `full` runs every gate. `reached` is the lane table's answer, run or not, which is what
 * its tests hold to the code. `unplaced` is the paths no rule placed.
 */
export function selectGates(plan, changed, { lanes = [], full = false, gated = false, reach = REACH } = {}) {
  const names = new Set(plan.map((gate) => gate.name));
  const named = namedGates(reach);
  const reachedBy = new Map();
  const unplaced = [];
  for (const path of changed) {
    const rule = ruleFor(path, reach, lanes);
    if (!rule) {
      unplaced.push(path);
      continue;
    }
    for (const gate of rule.gates === '*' ? names : rule.gates) {
      if (!names.has(gate)) continue;
      if (!reachedBy.has(gate)) reachedBy.set(gate, { paths: [], rule });
      reachedBy.get(gate).paths.push(path);
    }
  }
  const reachOf = (gate) => {
    if (unplaced.length) return { reached: true, why: `everything runs: no rule of the lane table places ${andMore(unplaced)}` };
    const by = reachedBy.get(gate.name);
    if (by) return { reached: true, why: `${andMore(by.paths)} (${by.rule.lane ? `${by.rule.lane}: ` : ''}${by.rule.why})` };
    if (!named.has(gate.name)) return { reached: true, why: 'the lane table does not name it, so it runs at every merge' };
    return { reached: false, why: 'no changed path reaches it' };
  };
  const gates = plan.map((gate) => {
    if (BLOCKING.includes(gate.name) || (gated && BASELINE.includes(gate.name))) return { gate, run: true, reached: true, why: 'runs at every merge' };
    const { reached, why } = BASELINE.includes(gate.name) ? { reached: true, why: 'the baseline' } : reachOf(gate);
    if (full) return { gate, run: true, reached, why: '--full runs every gate' };
    const detail = reached ? `; ${why.replace(/^everything runs: /, '')}` : '';
    if (isLongGate(gate)) return { gate, run: false, reached, why: `in the full set before staging${detail}` };
    // GATESLIM1: a merge blocks on the checks alone; the CI beside (--full --beside) runs the rest, and --gated runs what is reached.
    if (!gated) return { gate, run: false, reached, why: `in the CI beside${detail}` };
    return { gate, run: reached, reached, why };
  });
  return { gates, unplaced };
}

/**
 * Of `paths`, the ones that reach `gate` by the lane table, read as `selectGates` reads it (GATE6): every path reaches
 * a baseline gate and a gate no rule names, a path no rule places reaches every gate, and any other reaches the gates
 * its rule names. A verdict stays good until one of these changes, so the stage and `--rerun` ask this of the paths
 * changed since it was given. The test of the table checks that it and `selectGates` agree on every case.
 */
export function pathsReaching(gate, paths, { lanes = [], reach = REACH } = {}) {
  if (BASELINE.includes(gate) || !namedGates(reach).has(gate)) return [...paths];
  return paths.filter((path) => {
    const rule = ruleFor(path, reach, lanes);
    return !rule || rule.gates === '*' || rule.gates.includes(gate);
  });
}

/** The selection as lines: each gate run, numbered, with why; then each skipped, with why. */
export function selectionLines(gates) {
  const run = gates.filter((entry) => entry.run);
  const skipped = gates.filter((entry) => !entry.run);
  const out = run.map((entry, i) => `  ${pad(`${i + 1}.`, 4)}${pad(entry.gate.name, 16)} ${pad(entry.gate.kind, 10)} ${entry.why}`);
  if (skipped.length) {
    out.push(`  skipped (${skipped.length}):`);
    for (const entry of skipped) out.push(`      ${pad(entry.gate.name, 16)} ${pad(entry.gate.kind, 10)} ${entry.why}`);
  }
  return out;
}

// ---------------------------------------------------------------------------------------------------
// GATE4: a fixed gate re-runs alone

/**
 * What `--rerun` runs and keeps, in the plan's order: each named gate, each chosen gate with no verdict yet
 * (one a failure stopped, or one a fix's paths now reach), and each chosen gate whose verdict a path changed
 * since reaches (GATE6: `stale` maps it to those paths, none when its tree cannot be compared); every other
 * chosen gate's verdict is kept. `fresh` is the gates run for want of a verdict, and `stale` the ones run
 * again for a changed path, each with the verdict it no longer keeps.
 */
export function rerunPlan(plan, chosen, results, named, stale = {}) {
  const isStale = (name) => Object.hasOwn(stale, name);
  const run = plan.filter((name) => named.includes(name) || (chosen.includes(name) && (!results[name] || isStale(name))));
  const kept = plan.filter((name) => chosen.includes(name) && !named.includes(name) && results[name] && !isStale(name))
    .map((name) => ({ name, verdict: results[name].verdict, at: results[name].at }));
  const fresh = run.filter((name) => !named.includes(name) && !results[name]);
  const again = run.filter((name) => !named.includes(name) && results[name])
    .map((name) => ({ name, verdict: results[name].verdict, at: results[name].at, paths: stale[name] }));
  return { run, kept, fresh, stale: again };
}

/** A moment as people read it in a summary: minutes, in UTC, which is how the record keeps it. */
export const when = (iso) => (typeof iso === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(iso) ? `${iso.slice(0, 10)} ${iso.slice(11, 16)} UTC` : 'an unknown time');

const nowIso = () => new Date().toISOString().replace(/\.\d+Z$/, 'Z');

// ---------------------------------------------------------------------------------------------------
// The verdicts record (GATE3): which gate passed which tree

/**
 * GATEWT1: the checkout whose verdict store a run in `root` reads and writes: the repository's main checkout, the first
 * worktree git lists, whichever worktree the run is in (the gate tree's own run among them), or `root` itself where git
 * cannot say or the repository is bare. Verdicts are keyed by tree, so one store serves every worktree: a stage asked in
 * the gate tree and `--passed` in the main checkout read the same verdicts, each judging its own tree.
 */
const homes = new Map();
export function verdictsHome(root) {
  if (!homes.has(root)) {
    const listed = git(root, ['worktree', 'list', '--porcelain'], { allowFail: true });
    const first = listed.status === 0 ? parseWorktrees(listed.out)[0] : null;
    homes.set(root, first && first.path && !first.bare ? resolve(first.path) : root);
  }
  return homes.get(root);
}

/**
 * `fn` run holding a lock beside `file`, so two runs writing one store (a merge's and a full set's beside it, GATEWT1)
 * never write over each other's verdict. A write takes milliseconds, so a lock older than `staleMs` was left by a run
 * that died, and one still held after `waitMs` is taken: a lost verdict is better than a run that never ends.
 */
function holdingLock(file, fn, { waitMs = 10_000, staleMs = 30_000 } = {}) {
  const lock = `${file}.lock`;
  mkdirSync(dirname(lock), { recursive: true });
  const started = Date.now();
  let fd = null;
  while (fd === null) {
    try {
      fd = openSync(lock, 'wx');
    } catch (error) {
      if (error?.code !== 'EEXIST') throw error;
      let age = 0;
      try {
        age = Date.now() - statSync(lock).mtimeMs;
      } catch {
        continue;
      }
      if (age > staleMs || Date.now() - started > waitMs) rmSync(lock, { force: true });
      else Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 20);
    }
  }
  try {
    return fn();
  } finally {
    closeSync(fd);
    rmSync(lock, { force: true });
  }
}

/** `local/gate-verdicts.json`, or an empty record where there is none or it does not read. */
export function readVerdicts(root) {
  const file = join(root, VERDICTS_FILE);
  if (!existsSync(file)) return { schema: 1, verdicts: [] };
  try {
    const parsed = JSON.parse(readFileSync(file, 'utf8'));
    return { schema: 1, verdicts: Array.isArray(parsed?.verdicts) ? parsed.verdicts.filter((entry) => typeof entry?.gate === 'string') : [] };
  } catch {
    return { schema: 1, verdicts: [] };
  }
}

/** Verdicts appended, newest last, the oldest dropped past `keep`: written beside, then renamed. */
export function addVerdicts(root, entries, { keep = 400 } = {}) {
  const file = join(root, VERDICTS_FILE);
  holdingLock(file, () => {
    const verdicts = [...readVerdicts(root).verdicts, ...entries].slice(-keep);
    const record = {
      schema: 1,
      _why: 'Written by tools/merge-branch.mjs (GATE3): each gate verdict with the tree it ran on. publish:desktop asks `merge-branch --passed`, which reads it.',
      verdicts,
    };
    writeAtomic(file, `${JSON.stringify(record, null, 2)}\n`);
  });
}

/**
 * The checkout's content as a tree id, as `git add -A` would stage it (`stagedTree`, which `as-merged.mjs`
 * builds its commit on too): the person's index and a merge in progress untouched. Null when git cannot say.
 */
export function contentTree(root) {
  try {
    return stagedTree(root);
  } catch {
    return null;
  }
}

/** The paths that differ between two trees, or null when git cannot say (a tree long gone). */
function treeDrift(root, from, to) {
  if (!from || !to) return null;
  if (from === to) return [];
  const diff = git(root, ['diff', '--name-only', '--no-renames', from, to], { allowFail: true });
  return diff.status === 0 ? lines(diff.out) : null;
}

/**
 * The known records the parent writes after gates, when declared in the steward's lane or as union.
 * Neither declaration may make gate policy or source a record: both come from the current checkout,
 * so trusting their patterns alone would let an edit forgive itself and untested code. A union pattern
 * names the record at the root here, where git reads a pattern with no slash at any depth: the canon's
 * own `CHANGELOG.md` merges by union too, and the package ships it.
 */
export function recordMatcher({ lanes = [], union = [] } = {}) {
  const steward = lanes.find((lane) => lane.steward);
  const stewardOwns = steward ? laneMatcher(steward.paths) : () => false;
  const unionPatterns = union.map((pattern) => globToRegExp(pattern.replace(/^\//, '')));
  const known = new Set(['TASKS.md', 'docs/task-archive.md', 'CHANGELOG.md', 'docs/FIX-LOG.md', 'docs/README.md', '.claude/knowledge/twins.md']);
  const safe = (path) => known.has(path) || /^docs\/decisions\/D\d+\.md$/.test(path);
  return (path) => safe(path) && (stewardOwns(path) || unionPatterns.some((pattern) => pattern.test(path)));
}

/**
 * What a verdict is judged by against the checkout's `tree` (GATE6): the paths another tree differs from it by, asked
 * of git once per tree; which of them are the records; which reach a gate, by the lane table and this repository's
 * lanes. `--passed` and `--rerun` judge by the same.
 */
function standingFor(root, tree) {
  const lanes = readLanes(root)?.lanes ?? [];
  const drifts = new Map();
  return {
    tree,
    drift: (from) => {
      if (!drifts.has(from)) drifts.set(from, treeDrift(root, from, tree));
      return drifts.get(from);
    },
    isRecord: recordMatcher({ lanes, union: unionRecords(root) }),
    reaching: (gate, paths) => pathsReaching(gate, paths, { lanes }),
  };
}

/** A verdict that passed: PASS, or FLAKE, a pass that was said. */
const passing = (verdict) => verdict === 'PASS' || verdict === 'FLAKE';

/**
 * How a verdict given on tree `from` stands for `tree`, for its gate (GATE6), as `{ standing, paths, records }`:
 * `exact` on this tree; `records` when the two differ only by the records (`isRecord`, forgiven since GATE3);
 * `unreached` when every other path they differ by is one the gate cannot see (`reaching`, the lane table), those
 * paths in `paths`; else `stale`, `paths` naming the ones that reach it, or none when the trees cannot be compared
 * (a verdict with no tree, or one git no longer has: `drift` says null). `records` is the records they differ by.
 */
export function verdictStanding({ from, tree, gate, drift, isRecord, reaching }) {
  if (!from || !tree) return { standing: 'stale', paths: [], records: [] };
  if (from === tree) return { standing: 'exact', paths: [], records: [] };
  const changed = drift(from);
  if (!changed) return { standing: 'stale', paths: [], records: [] };
  const records = changed.filter(isRecord);
  const rest = changed.filter((path) => !isRecord(path));
  if (!rest.length) return { standing: 'records', paths: [], records };
  const reached = reaching(gate, rest);
  return reached.length ? { standing: 'stale', paths: reached, records } : { standing: 'unreached', paths: rest, records };
}

/**
 * Whether the full set passed `tree`: for each gate of the plan, its newest verdict that still stands for this tree
 * (`verdictStanding`: on it, or on one that differs from it only by the records and by paths that gate cannot reach)
 * must pass. `missing` names each gate that has no such passing verdict. Each gate carries the verdict that counts
 * and how it stands; a gate that does not pass, when a newer verdict no longer stands, carries that one as `stale`,
 * with the paths that made it so.
 */
export function stageJudgement({ plan, verdicts, tree, drift, isRecord, reaching }) {
  const drifts = new Map();
  const driftOf = (from) => {
    if (!drifts.has(from)) drifts.set(from, drift(from));
    return drifts.get(from);
  };
  const gates = plan.map((name) => {
    let newest = null;
    let standing = null;
    let stale = null;
    for (const entry of verdicts) {
      if (entry.gate !== name) continue;
      const how = verdictStanding({ from: entry.tree, tree, gate: name, drift: driftOf, isRecord, reaching });
      if (how.standing !== 'stale') {
        if (!newest || String(entry.at) >= String(newest.at)) [newest, standing] = [entry, how];
      } else if (!stale || String(entry.at) >= String(stale.verdict.at)) {
        stale = { ...how, verdict: entry };
      }
    }
    // A newer verdict that no longer stands explains a gate that does not count; one that counts needs no excuse.
    const said = stale && (!newest || (String(stale.verdict.at) > String(newest.at) && !passing(newest.verdict)));
    return { name, verdict: newest, exact: standing?.standing === 'exact', standing, stale: said ? stale : null };
  });
  const missing = gates.filter((gate) => !gate.verdict || !passing(gate.verdict.verdict)).map((gate) => gate.name);
  return { passed: missing.length === 0, missing, gates };
}

// ---------------------------------------------------------------------------------------------------
// GATE6b: a gate re-run on the checkout as it stands

/**
 * The smallest command that would make the stage pass the checkout, from its judgement's `gates` (in the plan's order),
 * or null when it passes: `--stale` when every gate it lacks is stale; `--rerun` naming each that failed or has no
 * verdict, when that runs fewer gates than the plan (it runs each stale one too, `checkoutRerun`); else `--full`. `runs`
 * is what the command would run. A refusal named `--full` whatever was missing, which cost a whole run, about an hour
 * and a half, for one gate that flaked or one fix committed after the full set.
 */
export function stageRemedy(gates) {
  const stale = gates.filter((gate) => gate.stale).map((gate) => gate.name);
  const lacking = gates.filter((gate) => !gate.stale && !(gate.verdict && passing(gate.verdict.verdict))).map((gate) => gate.name);
  if (!stale.length && !lacking.length) return null;
  if (!lacking.length) return { command: STALE_COMMAND, what: stale.length === 1 ? 'the stale gate' : `the ${stale.length} stale gates`, runs: stale };
  const runs = gates.map((gate) => gate.name).filter((name) => stale.includes(name) || lacking.includes(name));
  if (runs.length === gates.length) return { command: FULL_COMMAND, what: 'every gate', runs };
  return { command: `node tools/merge-branch.mjs --rerun ${lacking.join(' ')}`, what: runs.length === 1 ? 'the gate it lacks' : `the ${runs.length} gates it lacks`, runs };
}

/** The line a refused stage ends with, which `publish:desktop` reads for the command to name. */
const remedyLine = (remedy) => `  Run ${remedy.what} on it: ${remedy.command}`;

/**
 * What `--rerun <gate>…` and `--stale` run on the checkout, from the stage's judgement of its `gates`, as `rerunPlan`
 * reads a merge's, in the plan's order: each named gate, then each the stage calls stale (its newest verdict no longer
 * stands for this tree, and no newer one that does passed); every other gate's verdict is kept as the stage counts it,
 * a failure a failure. A gate with no verdict at all runs only when named, and `none` lists the rest of those: the
 * full set never ran them on anything this tree can be judged by.
 */
export function checkoutRerun(gates, named) {
  const results = {};
  const stale = {};
  for (const gate of gates) {
    if (gate.stale) {
      results[gate.name] = gate.stale.verdict;
      stale[gate.name] = gate.stale.paths;
    } else if (gate.verdict) results[gate.name] = gate.verdict;
  }
  const names = gates.map((gate) => gate.name);
  const { run, kept, stale: again } = rerunPlan(names, names.filter((name) => results[name]), results, named, stale);
  return { run, kept, stale: again, none: names.filter((name) => !results[name] && !named.includes(name)) };
}

// ---------------------------------------------------------------------------------------------------
// Flakes

const FAILED_TEST = /^\s+Failed (\S.*?) \[[^\]]*\]\s*$/;
const SUMMARY = /^\s*(?:Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)/;

/** The simple name of a test's class: the segment before the method, a nested class's outer name. */
export const testClass = (test) => (test.split('.').at(-2) ?? '').split('+')[0];
const short = (test) => test.split('.').slice(-2).join('.');

/**
 * What a `dotnet test` run says it failed: each failed test once (a theory by its method), how many
 * `Failed` lines there were, and the summary lines' counts summed (a solution prints one per project).
 */
export function testSummary(output) {
  const summary = { failed: [], failedLines: 0, failedCount: 0, passedCount: 0, summaries: 0 };
  for (const line of output.replace(/\r\n/g, '\n').split('\n')) {
    const failed = FAILED_TEST.exec(line);
    if (failed) {
      summary.failedLines += 1;
      const test = failed[1].replace(/\(.*$/, '');
      if (!summary.failed.includes(test)) summary.failed.push(test);
      continue;
    }
    const counts = SUMMARY.exec(line);
    if (counts) {
      summary.summaries += 1;
      summary.failedCount += Number(counts[1]);
      summary.passedCount += Number(counts[2]);
    }
  }
  return summary;
}

/**
 * Whether a failed run's failures are re-run alone: all of them named, and all real-process tests. The caller
 * asks only of a Process gate, where every test is one, so the default says yes.
 */
export function flakeDecision(output, isProcess = () => true) {
  const summary = testSummary(output);
  if (summary.summaries === 0) return { rerun: [], reason: 'the suite printed no result: it did not build, or its run was cut off' };
  if (summary.failedLines === 0) return { rerun: [], reason: 'no failing test is named: the run failed outside a test' };
  if (summary.failedLines !== summary.failedCount) {
    return { rerun: [], reason: `${summary.failedCount} failed but ${summary.failedLines} are named` };
  }
  const others = summary.failed.filter((test) => !isProcess(test));
  if (others.length) return { rerun: [], reason: `not a real-process test: ${others.map(short).join(', ')}` };
  return { rerun: summary.failed };
}

export const rerunCommand = (run, test) => `${run.trim()} --no-build --filter "FullyQualifiedName=${test}"`;

// ---------------------------------------------------------------------------------------------------
// PROC1: where a Process half's time goes

/** A Process gate's command with a trx logger writing each test's duration to `file`. Its re-runs add none. */
export const trxCommand = (run, file) => `${run.trim()} --logger "trx;LogFileName=${file}"`;

const XML_ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" };
const attributes = (tag) => Object.fromEntries([...tag.matchAll(/([A-Za-z_:][\w:.-]*)="([^"]*)"/g)]
  .map(([, name, value]) => [name, value.replace(/&(amp|lt|gt|quot|apos);/g, (_, entity) => XML_ENTITIES[entity])]));

/** A trx duration, `hh:mm:ss.fffffff`, in milliseconds; absent is none. */
function trxMs(duration) {
  const parts = /^(\d+):(\d{2}):(\d{2}(?:\.\d+)?)$/.exec(duration ?? '');
  return parts ? Math.round(((Number(parts[1]) * 60 + Number(parts[2])) * 60 + Number(parts[3])) * 1000) : 0;
}

/**
 * The slowest classes of a trx, at most `count`: each class's tests' durations summed, slowest first, by the
 * class's simple name (a nested class keeps its outer name, `Outer+Inner`). A test's class is its
 * definition's `TestMethod className`, matched to its result by `testId`; a test with no definition is
 * placed by its name. A Process half runs classes at once (the driver's eight, PROCPAR1; the git-only half's four,
 * PROC1c), so its sums are each class's own time, not its wall, and its slowest class is its wall's floor. The wall is
 * the run's, which `slowestLines` prints beside them (PROCPAR1b); only the modules' half runs one class at a time.
 */
export function slowestClasses(trx, count = 10) {
  const classOf = new Map();
  for (const [, id, body] of trx.matchAll(/<UnitTest\b[^>]*?\bid="([^"]+)"[^>]*>([\s\S]*?)<\/UnitTest>/g)) {
    const method = /<TestMethod\b[^>]*>/.exec(body);
    if (method) classOf.set(id, attributes(method[0]).className ?? '');
  }
  const classes = new Map();
  for (const [tag] of trx.matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const result = attributes(tag);
    const fullName = classOf.get(result.testId) || (result.testName ?? '').replace(/\(.*$/, '').split('.').slice(0, -1).join('.');
    if (!fullName) continue;
    const name = fullName.split('.').at(-1);
    const entry = classes.get(name) ?? { name, ms: 0, tests: 0 };
    entry.ms += trxMs(result.duration);
    entry.tests += 1;
    classes.set(name, entry);
  }
  return [...classes.values()].sort((a, b) => b.ms - a.ms || a.name.localeCompare(b.name)).slice(0, count);
}

/** A re-run passes only when a test ran and none failed: a filter that matches nothing exits 0 too. */
export function rerunPassed(output) {
  const summary = testSummary(output);
  return summary.summaries > 0 && summary.failedCount === 0 && summary.passedCount > 0;
}

// ---------------------------------------------------------------------------------------------------
// A rehearsal that died, rather than failed (LEFT1)

/**
 * An exit that says a process ended rather than that a gate judged: a POSIX shell that could not run the
 * command (126, 127), cmd's for one it cannot find (9009), a signal (128 to 255; this tool reads one as
 * 128), and a Windows crash status, which reaches this tool through cmd and npm unsigned (0xC0000142,
 * a process that could not start under load, is 3221225794) and elsewhere in its signed form.
 */
export function processExit(code) {
  return code === 126 || code === 127 || code === 9009 || (code >= 128 && code <= 255) || code >= 0xC0000000 || code < 0;
}

/**
 * The lines of a gate's log that are the gate's own: after the steps before it (`command` names the
 * gate's, and its output starts after the last `$ <command>` line), and without what this tool writes
 * around a step (`$ …`, `(exited …)`, `could not start: …`), npm's lines around a script
 * (`> pkg@version script`, `> command`, `npm error …`), and blank lines.
 */
export function ownOutput(log, command) {
  const all = log.replace(/\r\n/g, '\n').split('\n');
  const from = command === undefined ? -1 : all.lastIndexOf(`$ ${command}`);
  return all.slice(from + 1).filter((line) => line.trim()
    && !/^\$ /.test(line)
    && !/^\(exited /.test(line)
    && !/^could not start: /.test(line)
    && !/^> /.test(line)
    && !/^npm (?:error|ERR!|warn|WARN|notice)\b/.test(line));
}

/**
 * A rehearsal's report of a failed check: the rehearsal kit's `FAIL` line (and vitest's for a failed
 * file, inside `test:web`), vitest's summary, and Playwright's.
 */
const FAILED_CHECKS = [/^\s*FAIL\s/, /^\s*(?:Test Files|Tests)\s+\d+ failed\b/, /^\s*\d+ failed\s*$/];

/**
 * Whether a rehearsal that failed is run again, once. One that reported failed checks has failed,
 * whatever exit follows the report. One whose process ended, or that printed nothing of its own, never
 * reached a verdict: on 2026-09-30 the family rehearsal exited 127 straight after building the HTTP host,
 * with no transcript, while three worktrees built beside it, and passed run alone (FLAKE1). Anything else
 * said why it stopped, and has failed. `command` is the gate's, so a step before it is not its output.
 */
export function rehearsalDecision(log, code, command) {
  const own = ownOutput(log, command);
  if (own.some((line) => FAILED_CHECKS.some((pattern) => pattern.test(line)))) {
    return { rerun: false, reason: 'it reported failed checks, so it is never run again' };
  }
  if (processExit(code)) return { rerun: true, reason: `exit ${code} ended its process before a verdict` };
  if (own.length === 0) return { rerun: true, reason: 'it printed nothing of its own' };
  return { rerun: false, reason: 'it said why it stopped and reported no failed check, so it is never run again' };
}

// ---------------------------------------------------------------------------------------------------
// Lanes

const escapeRegExp = (text) => text.replace(/[.+^${}()|[\]\\]/g, '\\$&');

/** A lane path as a pattern: `*` stays in a folder, `**` crosses folders, `{a,b}` is either. */
export function globToRegExp(glob) {
  let source = '';
  for (let i = 0; i < glob.length; i += 1) {
    const c = glob[i];
    if (c === '*') {
      if (glob[i + 1] === '*') {
        i += 1;
        if (glob[i + 1] === '/') {
          i += 1;
          source += '(?:.*/)?';
        } else source += '.*';
      } else source += '[^/]*';
    } else if (c === '?') source += '[^/]';
    else if (c === '{' && glob.indexOf('}', i) > i) {
      const end = glob.indexOf('}', i);
      source += `(?:${glob.slice(i + 1, end).split(',').map(escapeRegExp).join('|')})`;
      i = end;
    } else source += escapeRegExp(c);
  }
  return new RegExp(`^${source}$`);
}

/**
 * A lane's paths as one test: a path is the lane's when one of its globs matches it and none of its `!`
 * globs does. A `!` glob carves a narrower lane's paths out of a wider one: the web's shell owns the
 * page but its Settings (LEFT1).
 */
export function laneMatcher(paths) {
  const include = paths.filter((path) => !path.startsWith('!')).map(globToRegExp);
  const exclude = paths.filter((path) => path.startsWith('!')).map((path) => globToRegExp(path.slice(1)));
  return (path) => include.some((pattern) => pattern.test(path)) && !exclude.some((pattern) => pattern.test(path));
}

/** The repository's lanes, at its root beside `daoris.json` and `daoris.gates.json` (D115 §2.1, DEV2). */
export const LANES_FILE = 'daoris.lanes.json';

/** How a quest will address a lane (`repository:lane`), so an id is one word of a known alphabet. */
const LANE_ID = /^[a-z][a-z0-9-]*$/;

/**
 * Why a parsed lanes file cannot be read, one line each, or none. Absence is a rule (D115 §2.1): no
 * `lanes` and an empty list both mean no lanes. A malformed or repeated id, a lane that owns nothing,
 * two stewards, and a `gates` entry naming no gate in `gateNames` (the declared set) each make it
 * unreadable. Fields it has no rule for are left alone.
 */
export function lanesProblems(parsed, gateNames = []) {
  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) return ['the file is not a JSON object'];
  const problems = [];
  if (parsed.laneless !== undefined && !Array.isArray(parsed.laneless)) problems.push("'laneless' is not a list");
  if (parsed.lanes === undefined) return problems;
  if (!Array.isArray(parsed.lanes)) return [...problems, "'lanes' is not a list"];
  const seen = new Set();
  parsed.lanes.forEach((lane, i) => {
    const id = lane?.id;
    const name = typeof id === 'string' ? `lane '${id}'` : `lane ${i + 1}`;
    if (typeof id !== 'string' || !LANE_ID.test(id)) {
      problems.push(`${name}: its id must be lower-case letters, digits and dashes, starting with a letter`);
    } else if (seen.has(id)) problems.push(`${name}: the id is used twice`);
    else seen.add(id);
    const paths = Array.isArray(lane?.paths) ? lane.paths : [];
    if (!paths.some((path) => typeof path === 'string' && path && !path.startsWith('!'))) problems.push(`${name}: it has no paths`);
    if (lane?.gates !== undefined) {
      if (!Array.isArray(lane.gates)) problems.push(`${name}: 'gates' is not a list`);
      else {
        for (const gate of lane.gates.filter((entry) => !gateNames.includes(entry))) {
          problems.push(`${name}: its gate '${gate}' is not declared in daoris.gates.json`);
        }
      }
    }
  });
  const stewards = parsed.lanes
    .map((lane, i) => (lane?.steward === true ? (typeof lane.id === 'string' ? lane.id : `lane ${i + 1}`) : null))
    .filter(Boolean);
  if (stewards.length > 1) problems.push(`two stewards (${stewards.join(', ')}): at most one lane keeps the records`);
  return problems;
}

/**
 * `daoris.lanes.json`, or null where a repository has none. An unreadable file is refused, naming each
 * problem. `laneless` is read flat: its groups are for the person reading the map, each saying why its
 * paths have no lane.
 */
export function readLanes(root) {
  const file = join(root, LANES_FILE);
  if (!existsSync(file)) return null;
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    throw new Refusal(`${LANES_FILE} is not valid JSON: ${error.message}`);
  }
  const gateNames = existsSync(join(root, 'daoris.gates.json')) ? readDeclared(root).map((gate) => gate.name) : [];
  const problems = lanesProblems(parsed, gateNames);
  if (problems.length) throw new Refusal(`${LANES_FILE} cannot be read:\n${problems.map((problem) => `  ${problem}`).join('\n')}`);
  return {
    lanes: (parsed.lanes ?? []).map((lane) => ({
      id: lane.id,
      title: typeof lane.title === 'string' ? lane.title : '',
      summary: typeof lane.summary === 'string' ? lane.summary : '',
      steward: lane.steward === true,
      paths: lane.paths,
      ...(lane.gates ? { gates: lane.gates } : {}),
    })),
    laneless: (parsed.laneless ?? []).flatMap((group) => group?.paths ?? []),
  };
}

/** The paths `.gitattributes` marks `merge=union`: the records that resolve themselves. */
export function unionRecords(root) {
  const file = join(root, '.gitattributes');
  if (!existsSync(file)) return [];
  return readFileSync(file, 'utf8').replace(/\r\n/g, '\n').split('\n')
    .map((line) => /^(\S+)\s(?:.*\s)?merge=union(?:\s|$)/.exec(line)?.[1])
    .filter((pattern) => pattern && !pattern.startsWith('#'));
}

/** A `.gitattributes` pattern with no slash matches at any depth, as git reads it. */
const attributePattern = (pattern) => (pattern.includes('/')
  ? globToRegExp(pattern.replace(/^\//, ''))
  : new RegExp(`^(?:.*/)?${globToRegExp(pattern).source.slice(1)}`));

/**
 * Where each changed path belongs, in D115 §2.3's order: the steward's lane first (its records, which a
 * subagent never edits, the parent's list before DEV2), then the records that merge by union, then the
 * first other lane that owns it, then the paths the map declares laneless (the docs, the doctrine),
 * else outside every lane: a path the map does not place, which the lanes test refuses (LEFT1). Lanes
 * come back by id and title in the map's order, only those touched; the steward's paths come back on
 * their own, where the parent's did.
 */
export function classify(paths, { lanes = [], union = [], laneless = [] } = {}) {
  const steward = lanes.find((lane) => lane.steward);
  const stewardOwns = steward ? laneMatcher(steward.paths) : () => false;
  const lanePatterns = lanes.filter((lane) => lane !== steward)
    .map((lane) => ({ id: lane.id, title: lane.title, owns: laneMatcher(lane.paths), files: [] }));
  const unionPatterns = union.map(attributePattern);
  const declared = laneMatcher(laneless);
  const placed = { lanes: [], steward: [], shared: [], laneless: [], outside: [] };
  for (const path of paths) {
    if (stewardOwns(path)) placed.steward.push(path);
    else if (unionPatterns.some((pattern) => pattern.test(path))) placed.shared.push(path);
    else {
      const lane = lanePatterns.find((candidate) => candidate.owns(path));
      if (lane) lane.files.push(path);
      else if (declared(path)) placed.laneless.push(path);
      else placed.outside.push(path);
    }
  }
  placed.lanes = lanePatterns.filter((lane) => lane.files.length).map(({ id, title, files }) => ({ id, title, files }));
  return placed;
}

// ---------------------------------------------------------------------------------------------------
// Git

function git(cwd, args, { allowFail = false, env } = {}) {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, ...(env ? { env: { ...process.env, ...env } } : {}) });
  if (result.error) throw new Refusal(`git could not start: ${result.error.message}`);
  if (result.status !== 0 && !allowFail) {
    throw new Refusal(`git ${args.join(' ')} failed: ${(result.stderr || result.stdout).trim()}`);
  }
  return { status: result.status, out: result.stdout.trimEnd(), err: result.stderr.trim() };
}

/**
 * What a `git check-ignore -q` probe answered: status 0 is ignored, 1 is not, and anything else (128, or a
 * git that could not answer under load) is a tool error, not a false "not ignored" (GATEIGNORE1).
 */
export function ignoredVerdict({ status, err }, path) {
  if (status === 0) return true;
  if (status === 1) return false;
  throw new Refusal(`git check-ignore ${path} failed (status ${status}): ${err || 'no message'}`);
}

const isIgnored = (cwd, path) => ignoredVerdict(git(cwd, ['check-ignore', '-q', path], { allowFail: true }), path);

const lines = (text) => text.split('\n').filter((line) => line.trim());

// Each git start costs tens of milliseconds on Windows, so the checkout's git folder is asked for once,
// with its top level, and an open merge is read from the MERGE_HEAD file git writes there.
const gitDirs = new Map();
function locate(cwd) {
  const [top, gitDir] = git(cwd, ['rev-parse', '--show-toplevel', '--absolute-git-dir']).out.split('\n').map((line) => line.trim());
  const root = resolve(top);
  gitDirs.set(root, resolve(gitDir));
  return root;
}
const mergeInProgress = (root) => existsSync(join(gitDirs.get(root), 'MERGE_HEAD'));
const unmergedPaths = (root) => lines(git(root, ['diff', '--name-only', '--diff-filter=U']).out);

/**
 * `git status --porcelain=v2 --branch`, read: the current branch ('' when detached) and each changed or
 * untracked path with its two-letter code (`??` untracked).
 */
export function parseStatus(text) {
  let branch = '';
  const changed = [];
  for (const line of text.replace(/\r\n/g, '\n').split('\n')) {
    if (line.startsWith('# branch.head ')) {
      const head = line.slice('# branch.head '.length).trim();
      branch = head === '(detached)' ? '' : head;
    } else if (line.startsWith('? ')) changed.push({ code: '??', path: line.slice(2) });
    else if (/^[12u] /.test(line)) {
      // Fields before the path: 8 for an ordinary entry, 9 for a rename (then `path<TAB>from`), 10 unmerged.
      const fields = line.split(' ');
      const skip = { 1: 8, 2: 9, u: 10 }[line[0]];
      changed.push({ code: fields[1], path: fields.slice(skip).join(' ').split('\t')[0] });
    }
  }
  return { branch, changed };
}

/** A tree that is not clean, each changed or untracked path with its code, the first twelve. */
const notClean = (changed) => {
  const shown = changed.slice(0, 12).map(({ code, path }) => `  ${code} ${path}`).join('\n');
  return `the tree is not clean:\n${shown}${changed.length > 12 ? `\n  … ${changed.length - 12} more` : ''}`;
};

/** Why a merge cannot start from what git reported, or null. */
export function startRefusal({ branch, merging, changed, ignored }) {
  if (branch !== MAIN) {
    return `the current branch is '${branch || 'a detached HEAD'}', not ${MAIN}: merges land on ${MAIN}, from its own checkout`;
  }
  if (merging) return 'a merge is already in progress: resolve it and run --continue, or git merge --abort';
  if (changed.length) return notClean(changed);
  if (!ignored) return `${SCRATCH}/ is not ignored here, so the gate logs would dirty the tree`;
  return null;
}

const treeState = (root) => parseStatus(git(root, ['status', '--porcelain=v2', '--branch']).out);

function requireMain(root) {
  const refusal = startRefusal({ branch: treeState(root).branch, merging: false, changed: [], ignored: true });
  if (refusal) throw new Refusal(refusal);
}

/** On main, no merge open, a clean tree, and a scratch folder git ignores (or the logs would dirty it). */
function preconditions(root) {
  const facts = { ...treeState(root), merging: mergeInProgress(root), ignored: true };
  const refusal = startRefusal(facts);
  if (refusal) throw new Refusal(refusal);
  // Asked last, and only of a checkout that passed the rest: it is one more git start.
  if (!isIgnored(root, `${SCRATCH}/merge-probe.log`)) {
    throw new Refusal(startRefusal({ ...facts, ignored: false }));
  }
  requireVerdictsIgnored(root);
}

/** The verdicts record is written as each gate ends: tracked or untracked, it would change the tree it judges. In the main checkout (GATEWT1). */
function requireVerdictsIgnored(root) {
  const home = verdictsHome(root);
  if (!isIgnored(home, VERDICTS_FILE)) {
    throw new Refusal(`${VERDICTS_FILE} is not ignored ${samePath(home, root) ? 'here' : 'in the main checkout, where every worktree records'}, so each verdict written would change the tree it is a verdict on`);
  }
}

/** The branch's tip, or a refusal naming it. */
function requireBranch(root, branch) {
  const tip = git(root, ['rev-parse', '--verify', '--quiet', `${branch}^{commit}`], { allowFail: true });
  if (tip.status !== 0) throw new Refusal(`no branch '${branch}'`);
  return tip.out.trim();
}

/** The log format `parseCommits` reads: short sha, parents, subject, the Co-Authored-By values. */
export const COMMIT_FORMAT = '--format=%h%x1f%p%x1f%s%x1f%(trailers:key=Co-Authored-By,valueonly)%x1e';

/** A branch's commits from `git log` in `COMMIT_FORMAT`: sha, whether it is a merge, subject, trailer. */
export function parseCommits(text) {
  return text.split('\x1e').map((entry) => entry.trim()).filter(Boolean).map((entry) => {
    const [sha = '', parents = '', subject = '', trailer = ''] = entry.split('\x1f');
    return { sha, merge: parents.trim().split(/\s+/).length > 1, subject, trailer: trailer.trim() };
  });
}

/** The commits main lacks, one git start for both the count and the commit check. */
function branchCommits(root, branch) {
  const commits = parseCommits(git(root, ['log', COMMIT_FORMAT, `HEAD..${branch}`]).out);
  if (commits.length === 0) throw new Refusal(`'${branch}' has nothing main lacks: it is merged already`);
  return commits;
}

/**
 * `git worktree list --porcelain`, read: each worktree's path, the branch it has out ('' when detached),
 * whether it is locked and git's reason, and whether git says its folder is gone. The first is the main
 * checkout.
 */
export function parseWorktrees(porcelain) {
  const trees = [];
  for (const line of porcelain.replace(/\r\n/g, '\n').split('\n')) {
    const tree = trees.at(-1);
    if (line.startsWith('worktree ')) {
      trees.push({ path: line.slice('worktree '.length).trim(), branch: '', locked: false, lockReason: '', prunable: false, bare: false });
    } else if (!tree) continue;
    else if (line.startsWith('branch refs/heads/')) tree.branch = line.slice('branch refs/heads/'.length).trim();
    else if (/^locked(?: |$)/.test(line)) Object.assign(tree, { locked: true, lockReason: line.slice('locked'.length).trim() });
    else if (/^prunable(?: |$)/.test(line)) tree.prunable = true;
    else if (line.trim() === 'bare') tree.bare = true;
  }
  return trees;
}

/** The path of the worktree `git worktree list --porcelain` shows a branch checked out in, or null. */
export function worktreeFor(porcelain, branch) {
  return (branch && parseWorktrees(porcelain).find((tree) => tree.branch === branch)?.path) || null;
}

/**
 * What the commit check finds wrong: a commit of the branch's own without the trailer (a merge of main
 * into it carries none, and needs none), and work left uncommitted in its worktree.
 */
function commitProblems(root, branch, commits) {
  const problems = commits.filter((commit) => !commit.merge && !commit.trailer)
    .map((commit) => `${commit.sha} ${commit.subject}: no Co-Authored-By line`);
  const tree = worktreeFor(git(root, ['worktree', 'list', '--porcelain']).out, branch);
  if (tree && existsSync(tree)) {
    const left = lines(git(tree, ['status', '--porcelain']).out);
    if (left.length) {
      problems.push(`its worktree holds uncommitted work, which the merge would leave out:\n${left.slice(0, 12).map((line) => `    ${line}`).join('\n')}`);
    }
  }
  return problems;
}

// ---------------------------------------------------------------------------------------------------
// The prune (GATE2): merged branches go, with their worktrees, unless a worktree is in use or holds work

/**
 * The branches a prune considers: each local branch merged into main, but main, the branch the main
 * checkout has out, the one this runs in, and the `hold` names: a batch's branches still to merge, since
 * one merged already is refused by name at its turn, and a deleted one would read as a misspelling. Each
 * comes with the worktree that has it out, if any; a held one says why it is held.
 *
 * A Daoris session's tree and branch are Daoris's, never this tool's (DOGFOOD1): an install driving this repository makes
 * each session a worktree under its own home and a branch at main's tip, which reads as merged until its first commit, and
 * a prune took one mid-session. So a branch Daoris names, `daoris/` for a session (D51) and `quest/` for this repository's
 * landings, is held with its worktree; Daoris's own housekeeping removes them. A tree's place is no sign: the agent harness's
 * trees may stand beside the checkout too.
 */
export function pruneCandidates({ merged, worktrees, current, hold = [] }) {
  const mainCheckout = worktrees[0]?.branch ?? '';
  return merged.filter((branch) => branch !== MAIN).map((branch) => {
    let held = null;
    if (branch === mainCheckout) held = 'the main checkout has it checked out';
    else if (branch === current) held = 'this runs in its checkout';
    else if (hold.includes(branch)) held = 'named in this batch, still to merge';
    else if (/^(daoris|quest)\//.test(branch)) held = "a Daoris session's or landing's branch: Daoris's own housekeeping removes it";
    return { branch, worktree: worktrees.find((tree) => tree.branch === branch) ?? null, held };
  });
}

const listed = (files, max = 3) => `${files.slice(0, max).join(', ')}${files.length > max ? `, … ${files.length - max} more` : ''}`;

/**
 * Whether a lock is the agent harness's own, `claude agent <id> (pid N)` (CLEANUP2). The harness holds it for its
 * session's whole life, not only while the agent runs, so on a finished agent's worktree it says nothing of use.
 */
export function harnessLock(reason) {
  return /^claude agent \S+ \(pid \d+\)$/.test(reason ?? '');
}

/**
 * The running processes, read from the CIM query's JSON on Windows or `ps -A -o pid= -o args=` elsewhere: each one's
 * pid, its executable's path ('' when not given) and its command line, or its path when the command line is not given.
 * One with neither names no folder and is left out.
 */
export function parseProcesses(text, platform = process.platform) {
  if (platform === 'win32') {
    // Set to UTF-8, a console may write its preamble first, which JSON.parse refuses.
    const json = text.replace(/^﻿/, '').trim();
    const parsed = json ? JSON.parse(json) : [];
    return (Array.isArray(parsed) ? parsed : [parsed])
      .map((one) => ({ pid: Number(one.ProcessId), exe: one.ExecutablePath ?? '', command: one.CommandLine ?? one.ExecutablePath ?? '' }))
      .filter((one) => one.command);
  }
  return lines(text).map((line) => /^\s*(\d+)\s+(.*)$/.exec(line)).filter(Boolean)
    .map(([, pid, command]) => ({ pid: Number(pid), exe: '', command: command.trim() }));
}

/**
 * The processes inside `folder`: each whose executable path or command line names it, as itself or a path under it,
 * with `\` or `/`, and on Windows in any case and in the `/d/…` form a POSIX shell gives a drive. `agent-ab` is not
 * inside `agent-a`: the folder's name must end where a path, a quote or a space would.
 */
export function processesInside(folder, processes, platform = process.platform) {
  const windows = platform === 'win32';
  const norm = (text) => {
    const slashed = text.split('\\').join('/');
    return windows ? slashed.toLowerCase() : slashed;
  };
  const base = norm(folder).replace(/\/+$/, '');
  const forms = [base];
  const drive = /^([a-z]):\//.exec(base);
  if (windows && drive) forms.push(`/${drive[1]}/${base.slice(3)}`);
  const names = (text) => {
    const seen = norm(text);
    return forms.some((form) => {
      for (let at = seen.indexOf(form); at !== -1; at = seen.indexOf(form, at + 1)) {
        const next = seen[at + form.length];
        if (next === undefined || /[/"'\s]/.test(next)) return true;
      }
      return false;
    });
  };
  return processes.filter((one) => names(one.command) || (one.exe && names(one.exe)));
}

/** How long a harness-locked worktree must have had no write before the prune takes it (CLEANUP2b). */
export const HARNESS_IDLE_MINUTES = 120;

// Folders whose children are not read: build output and dependencies, thousands of entries a worker's edits rarely reach first.
const WRITE_SKIP = new Set(['.git', 'node_modules', 'bin', 'obj', 'dist', 'wwwroot']);

/**
 * The newest write time, in ms, of a worktree's folder, 0 when nothing could be read (CLEANUP2b). Read cheaply, never a
 * full walk (a tree is 300 MB): the times of the folder's top-level entries (a file or folder made or removed there moves
 * the folder's own time, and a worker's builds, logs and scratch land there), of the children of each top-level folder
 * but build output (`src/`, `local/`, `docs/`), and of the git folder the worktree keeps beside the repository: its HEAD,
 * its index and its HEAD log, which every commit, checkout and `git add` writes. A file edited in place deeper down, with
 * nothing made or committed, is not seen; the two hours the caller waits cover that.
 */
export function newestWrite(path) {
  let newest = 0;
  const seen = (file) => {
    try {
      newest = Math.max(newest, lstatSync(file).mtimeMs);
    } catch { /* gone between the listing and the stat: nothing to read */ }
  };
  const children = (folder) => {
    try {
      return readdirSync(folder, { withFileTypes: true });
    } catch {
      return [];
    }
  };
  seen(path);
  for (const entry of children(path)) {
    const at = join(path, entry.name);
    seen(at);
    if (entry.isDirectory() && !WRITE_SKIP.has(entry.name)) for (const child of children(at)) seen(join(at, child.name));
  }
  try {
    const pointer = /^gitdir: (.+)$/m.exec(readFileSync(join(path, '.git'), 'utf8'));
    if (pointer) for (const name of ['HEAD', 'index', join('logs', 'HEAD')]) seen(join(resolve(path, pointer[1].trim()), name));
  } catch { /* no .git file: the folder's own entries are all there is */ }
  return newest;
}

/** One process, as a prune names it: its pid and its command line, cut short. */
const processSaid = (one) => `pid ${one.pid}: ${one.command.length > 120 ? `${one.command.slice(0, 120)}…` : one.command}`;

/**
 * Whether a path under `local/` (as `local/…`, `/`-separated) is an agent's disposable output: logs and scratch under
 * `local/scratch/`, and a `*.log` file anywhere under `local/`. Anything else there may be work, and keeps its worktree.
 */
export function disposableLocal(path, directory = false) {
  if (path === 'local/scratch' || path.startsWith('local/scratch/')) return true;
  return !directory && path.endsWith('.log');
}

/**
 * Whether a folder under `.claude/worktrees/` that git does not list goes (CLEANUP2): what a `git worktree remove`
 * left when it could not finish. It goes when it is no link (a reparse point is never followed), holds no `.git` (then
 * it is no half-removed worktree), and no running process is inside it, which is never stopped here.
 */
export function folderVerdict(folder) {
  if (folder.link) return { remove: false, why: 'a link (a reparse point), never followed or removed here' };
  if (folder.git) return { remove: false, why: 'a folder git does not list, but it holds a .git, so it is no half-removed worktree and is never touched here' };
  if (!folder.processes) {
    return { remove: false, why: `a folder git does not list, kept: whether a process runs inside it could not be read (${folder.processError ?? 'not looked into'})` };
  }
  if (folder.processes.length) {
    return {
      remove: false,
      why: `a folder git does not list, kept: a running process is inside it (${folder.processes.map(processSaid).join('; ')}), and a process is never stopped here: end it, and the next prune removes the folder`,
    };
  }
  return { remove: true, why: '' };
}

/**
 * Whether a candidate goes, and if it stays, why. `facts` is what was found in its worktree, and null
 * where nothing was looked into: a worktree locked for any reason but the agent harness's is in use, so
 * the lock decides and what it holds does not, and nothing runs in it. The harness's own lock (CLEANUP2)
 * is lifted, `unlock`, when its branch is not at main's tip (a just-dispatched agent's is), it has no
 * change, nothing under `local/` but logs and scratch, and no running process inside it. Every reason to
 * keep is said, not only the first.
 */
export function pruneVerdict(candidate, facts) {
  if (candidate.held) return { remove: false, why: candidate.held };
  const tree = candidate.worktree;
  if (!tree) return { remove: true, why: '' };
  const harness = tree.locked && harnessLock(tree.lockReason);
  if (tree.locked && !harness) {
    return { remove: false, why: `its worktree is locked, so in use${tree.lockReason ? ` (${tree.lockReason})` : ''}; it is never looked into or unlocked here` };
  }
  if (!facts) return { remove: false, why: 'its worktree was not looked into' };
  const unlock = harness ? { unlock: true } : {};
  if (facts.gone) return { remove: true, why: '', gone: true, ...unlock };
  const has = [];
  if (facts.error) has.push(`a state git could not read (${facts.error})`);
  if (facts.tracked.length) has.push(`modified or staged tracked files (${listed(facts.tracked)})`);
  if (facts.local) has.push(`private output under local/ (${facts.local}), which nothing else keeps`);
  if (facts.untracked.length) has.push(`untracked files git removes only with --force (${listed(facts.untracked)})`);
  if (!harness) return has.length ? { remove: false, why: `its worktree has ${has.join('; ')}` } : { remove: true, why: '' };

  const kept = [];
  if (facts.atTip) kept.push("its branch is at main's tip, as a just-dispatched agent's is");
  kept.push(...has.map((one) => `it has ${one}`));
  if (!facts.processes) kept.push(`whether a process runs inside it could not be read (${facts.processError ?? 'not looked into'})`);
  else if (facts.processes.length) kept.push(`a running process is inside it (${facts.processes.map(processSaid).join('; ')})`);
  // A shell's working folder is on no command line, so a quiet tree is the only sign a worker has left it (CLEANUP2b).
  if (facts.idleMinutes === undefined) kept.push('when it was last written could not be read');
  else if (facts.idleMinutes < HARNESS_IDLE_MINUTES) kept.push(`written ${facts.idleMinutes} minute${facts.idleMinutes === 1 ? '' : 's'} ago, a worker may be in it`);
  if (kept.length) return { remove: false, why: `its worktree is locked by the agent harness (${tree.lockReason}), and stays locked: ${kept.join('; ')}` };
  return { remove: true, why: '', ...unlock };
}

/** The line of git's refusal worth printing: its `error:` or `fatal:` line, else its first. */
const gitSays = (text) => {
  const all = lines(text.replace(/\r\n/g, '\n')).map((line) => line.trim());
  return all.find((line) => /^(?:error|fatal):/.test(line)) ?? all[0] ?? 'no reason given';
};

const samePath = (a, b) => {
  const norm = (path) => resolve(path).split('\\').join('/').replace(/\/+$/, '');
  return process.platform === 'win32' ? norm(a).toLowerCase() === norm(b).toLowerCase() : norm(a) === norm(b);
};

/**
 * The first file under `dir`, as a `/` path from the folder that holds it, or null when it holds none.
 * `skip` passes over a path (a folder's whole content with it). Links are not followed. A folder that
 * cannot be read throws, and its worktree is kept.
 */
export function firstFile(dir, skip = () => false) {
  const shownFrom = (path) => relative(dirname(dir), path).split('\\').join('/');
  const walk = (at) => {
    let entries;
    try {
      entries = readdirSync(at, { withFileTypes: true });
    } catch (error) {
      if (error?.code === 'ENOENT') return null;
      throw error;
    }
    for (const entry of entries) {
      const path = join(at, entry.name);
      if (skip(shownFrom(path), entry.isDirectory())) continue;
      const found = entry.isDirectory() ? walk(path) : path;
      if (found) return found;
    }
    return null;
  };
  const found = walk(dir);
  return found && shownFrom(found);
}

/** What a prune needs to know of a worktree it may remove: its folder gone, its changes, its `local/` but logs and scratch. */
function inspectWorktree(tree) {
  if (tree.prunable || !existsSync(tree.path)) return { gone: true };
  const facts = { tracked: [], untracked: [], local: null };
  // git walks up: in a folder that lost its .git file, a status would answer for the repository above it.
  if (!existsSync(join(tree.path, '.git'))) return { ...facts, error: 'its folder has no .git file' };
  const status = git(tree.path, ['--no-optional-locks', 'status', '--porcelain'], { allowFail: true });
  if (status.status !== 0) return { ...facts, error: gitSays(status.err || status.out) };
  for (const line of lines(status.out)) (line.startsWith('?? ') ? facts.untracked : facts.tracked).push(line.slice(3));
  try {
    facts.local = firstFile(join(tree.path, 'local'), disposableLocal);
  } catch (error) {
    facts.local = `local/ itself, which could not be read: ${error?.code ?? error?.message}`;
  }
  return facts;
}

// One CIM query lists every process with its path and command line (2.4 s on the machine it was built on);
// `Get-Process` gives no command line. The encoding is set so a path that is not ASCII arrives whole.
const PROCESS_QUERY = '[Console]::OutputEncoding = [Text.Encoding]::UTF8; '
  + 'ConvertTo-Json -Compress -InputObject @(Get-CimInstance Win32_Process | Select-Object ProcessId,ExecutablePath,CommandLine)';

/** The running processes but this one, or why they could not be read. Asked once per prune, and only when needed. */
function listProcesses() {
  const windows = process.platform === 'win32';
  const result = windows
    ? spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', PROCESS_QUERY], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, windowsHide: true })
    : spawnSync('ps', ['-A', '-o', 'pid=', '-o', 'args='], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  const what = windows ? 'the process query' : 'ps';
  if (result.error) return { error: `${what} could not start: ${result.error.message}` };
  if (result.status !== 0) return { error: `${what} failed: ${gitSays(result.stderr || result.stdout || '')}` };
  try {
    return { processes: parseProcesses(result.stdout, process.platform).filter((one) => one.pid !== process.pid) };
  } catch (error) {
    return { error: `${what} answered what could not be read: ${error?.message ?? error}` };
  }
}

/** Where the agent harness makes its worktrees, under the main checkout. */
const WORKTREES = join('.claude', 'worktrees');

/**
 * The folders under the main checkout's `.claude/worktrees/` that git does not list (CLEANUP2): each one's path,
 * whether it is a link (a reparse point, never followed), and whether it holds a `.git`. A `.claude/worktrees/` that
 * is itself a link, or resolves outside the checkout, is one such link and nothing under it is looked at.
 */
function strayFolders(mainCheckout, worktrees) {
  const base = join(mainCheckout, WORKTREES);
  let entries;
  try {
    if (lstatSync(base).isSymbolicLink() || !samePath(realpathSync(base), base)) return [{ path: base, link: true, git: false }];
    entries = readdirSync(base, { withFileTypes: true });
  } catch (error) {
    if (error?.code === 'ENOENT') return [];
    throw error;
  }
  return entries.filter((entry) => entry.isDirectory() || entry.isSymbolicLink())
    .map((entry) => join(base, entry.name))
    .filter((path) => !worktrees.some((tree) => samePath(tree.path, path)))
    .map((path) => {
      const link = lstatSync(path).isSymbolicLink();
      return { path, link, git: !link && existsSync(join(path, '.git')) };
    });
}

/**
 * The prune's plan, changing nothing: every candidate with its verdict, every folder under `.claude/worktrees/`
 * git does not list with its own, and the branch the checkout this runs in has out, since `git branch -d`
 * asks whether a branch is merged into that one. The processes are read once, and only when a
 * harness-locked worktree or a folder is to be looked into.
 */
export function planPrune(root, { hold = [], atMerge = false } = {}) {
  const refs = lines(git(root, ['for-each-ref', `--merged=refs/heads/${MAIN}`, '--format=%(objectname) %(refname)', 'refs/heads/']).out)
    .map((line) => {
      const [sha, ref] = line.trim().split(' ');
      return { sha, branch: ref.replace(/^refs\/heads\//, '') };
    });
  const merged = refs.map((ref) => ref.branch);
  const tip = refs.find((ref) => ref.branch === MAIN)?.sha;
  const worktrees = parseWorktrees(git(root, ['worktree', 'list', '--porcelain']).out);
  const current = worktrees.find((tree) => samePath(tree.path, root))?.branch ?? '';
  let found = null;
  const inside = (path) => {
    found ??= listProcesses();
    return found.error ? { processError: found.error } : { processes: processesInside(path, found.processes) };
  };

  const entries = pruneCandidates({ merged, worktrees, current, hold }).map((candidate) => {
    const tree = candidate.worktree;
    let facts = null;
    if (!candidate.held && tree && (!tree.locked || harnessLock(tree.lockReason))) {
      facts = inspectWorktree(tree);
      if (tree.locked && !facts.gone) {
        const written = newestWrite(tree.path);
        Object.assign(facts, { atTip: refs.find((ref) => ref.branch === candidate.branch)?.sha === tip }, inside(tree.path));
        if (written > 0) facts.idleMinutes = Math.max(0, Math.floor((Date.now() - written) / 60_000));
      }
    }
    return { ...candidate, ...pruneVerdict(candidate, facts) };
  });
  const folders = strayFolders(worktrees[0]?.path ?? root, worktrees).map((folder) => {
    const looked = folder.link || folder.git ? folder : { ...folder, ...inside(folder.path) };
    return { ...looked, ...folderVerdict(looked) };
  });
  const gate = gateTreeFacts(worktrees[0]?.path ?? root, worktrees, inside);
  const gateTree = gate && { ...gate, ...gateTreeVerdict(gate, { atMerge }) };
  return { current, entries, folders, gateTree };
}

/**
 * GATEWT1: what the prune knows of the gate tree, which no rule above sees: it is detached, so no merged branch has it
 * out, and it is under `local/`, not `.claude/worktrees/`. Null when it does not stand. A run's lock is read first, and a
 * tree whose full set runs is not looked into.
 */
function gateTreeFacts(home, worktrees, inside) {
  const path = join(home, GATE_TREE);
  const tree = worktrees.find((one) => samePath(one.path, path));
  if (!tree && !existsSync(path)) return null;
  const lock = readGateLock(home);
  const record = readGateRecord(home);
  const staged = record?.staged?.commit ? { commit: record.staged.commit, at: record.staged.at } : null;
  const facts = { home, path, listed: Boolean(tree), head: null, changes: [], lock, staged };
  if (lock?.running) return facts;
  if (!tree) {
    const link = lstatSync(path).isSymbolicLink();
    const folder = { path, link, git: !link && existsSync(join(path, '.git')) };
    return { ...facts, folder: folder.link || folder.git ? folder : { ...folder, ...inside(path) } };
  }
  if (tree.prunable || !existsSync(join(path, '.git'))) return { ...facts, error: 'its folder has no .git file' };
  Object.assign(facts, inside(path));
  const head = git(path, ['rev-parse', 'HEAD'], { allowFail: true });
  const status = git(path, ['--no-optional-locks', 'status', '--porcelain'], { allowFail: true });
  if (head.status !== 0 || status.status !== 0) return { ...facts, error: gitSays(head.err || status.err || head.out || status.out) };
  return { ...facts, head: head.out.trim(), changes: lines(status.out).map((line) => line.slice(3)) };
}

/**
 * The gate tree removed, its record and a lock its ended run left with it: `git worktree remove`, never forced, or the
 * half-removed folder's removal. Its lock is read again first, and a run started meanwhile keeps it.
 */
function removeGateTree(home, entry) {
  const lock = readGateLock(home);
  if (lock?.running) return { ...entry, remove: false, ...gateTreeVerdict({ ...entry, lock }) };
  let left = '';
  if (entry.listed) {
    const removed = git(home, ['worktree', 'remove', entry.path], { allowFail: true });
    if (removed.status !== 0) {
      const still = parseWorktrees(git(home, ['worktree', 'list', '--porcelain']).out).some((known) => samePath(known.path, entry.path));
      if (still) return { ...entry, remove: false, why: `git refused to remove it: ${gitSays(removed.err || removed.out)}` };
      left = `; git could not delete all of its folder (${gitSays(removed.err || removed.out)}), and what is left is no longer git's`;
    }
    git(home, ['worktree', 'prune'], { allowFail: true });
  } else {
    const gone = removeFolder(entry.folder, parseWorktrees(git(home, ['worktree', 'list', '--porcelain']).out));
    if (!gone.removed) return { ...entry, remove: false, why: gone.why };
  }
  rmSync(join(home, GATE_TREE_RECORD), { force: true });
  if (lock) rmSync(join(home, GATE_TREE_LOCK), { force: true });
  return { ...entry, removed: true, left };
}

/** The gate tree's line, in the prune's columns. */
function gateTreeLine(entry, applied) {
  const goes = applied ? entry.removed === true : entry.remove;
  const verb = applied ? (goes ? 'removed' : 'kept') : (goes ? 'remove' : 'keep');
  const text = goes
    ? (entry.listed ? `${entry.said}${entry.left ?? ''}` : 'a folder git does not list, with no .git: what a removal of the gate tree that did not finish left')
    : entry.why;
  return `  ${pad(verb, 8)} ${pad(GATE_TREE, 34)} ${text}`;
}

/** One removal: the worktree, unlocked first when the harness's lock is lifted, then the branch. What git refuses is kept, with git's own reason. */
function removeOne(root, entry) {
  const tree = entry.worktree;
  let left = '';
  if (tree) {
    if (entry.unlock) {
      const unlocked = git(root, ['worktree', 'unlock', tree.path], { allowFail: true });
      if (unlocked.status !== 0) return { ...entry, remove: false, why: `git refused to unlock its worktree: ${gitSays(unlocked.err || unlocked.out)}` };
    }
    const removed = git(root, ['worktree', 'remove', tree.path], { allowFail: true });
    if (removed.status !== 0) {
      const still = parseWorktrees(git(root, ['worktree', 'list', '--porcelain']).out).some((known) => samePath(known.path, tree.path));
      if (still) {
        // A worktree that stays keeps the lock it had, so what the harness holds is never left unlocked.
        if (entry.unlock) git(root, ['worktree', 'lock', '--reason', tree.lockReason, tree.path], { allowFail: true });
        return { ...entry, remove: false, why: `git refused to remove its worktree: ${gitSays(removed.err || removed.out)}` };
      }
      // git can let go of a worktree whose folder it could not wholly delete (a file held open): the
      // branch is free, and what is left of the folder is no longer git's.
      left = `; git could not delete all of its folder (${gitSays(removed.err || removed.out)}), and what is left is no longer git's`;
    }
  }
  const deleted = git(root, ['branch', '-d', entry.branch], { allowFail: true });
  if (deleted.status !== 0) {
    return { ...entry, remove: false, why: `${tree ? 'its worktree is removed, but ' : ''}git refused to delete the branch, which is never forced: ${gitSays(deleted.err || deleted.out)}` };
  }
  return { ...entry, removed: true, left };
}

/**
 * The plan, applied: `git worktree remove`, never forced (ignored build output does not stop it, and
 * whatever does is work to keep), then `git branch -d`, never `-D`, then one `git worktree prune`.
 * `onEach` hears each entry as it ends, so a long removal is not a silence.
 */
export function applyPrune(root, entries, onEach = () => {}) {
  const done = entries.map((entry) => {
    const outcome = entry.remove ? removeOne(root, entry) : entry;
    onEach(outcome);
    return outcome;
  });
  if (entries.some((entry) => entry.remove)) git(root, ['worktree', 'prune'], { allowFail: true });
  return done;
}

/**
 * One half-removed folder's removal, looked at again first: git may list it by now, or it may hold a `.git` or be a
 * link, and then it is not touched. A link inside it is removed as a link, never followed. What cannot be removed
 * stays, and the next prune tries again.
 */
function removeFolder(folder, listedNow) {
  if (listedNow.some((tree) => samePath(tree.path, folder.path))) {
    return { ...folder, remove: false, why: 'git lists it now, so it is a worktree and is never touched here' };
  }
  let stat;
  try {
    stat = lstatSync(folder.path);
  } catch (error) {
    if (error?.code === 'ENOENT') return { ...folder, removed: true };
    return { ...folder, remove: false, why: `it could not be looked at again (${error?.code ?? error?.message}); the next prune tries again` };
  }
  if (stat.isSymbolicLink()) return { ...folder, remove: false, ...folderVerdict({ ...folder, link: true }) };
  if (existsSync(join(folder.path, '.git'))) return { ...folder, remove: false, ...folderVerdict({ ...folder, git: true }) };
  try {
    rmSync(folder.path, { recursive: true, maxRetries: 2 });
    return { ...folder, removed: true };
  } catch (error) {
    return {
      ...folder,
      remove: false,
      why: `it could not be removed (${error?.code ?? 'error'}: ${error?.message ?? error}); no running process names it in its command line, so one holds it another way (its working folder, an open file); the next prune tries again`,
    };
  }
}

/** The half-removed folders the plan let go, removed, each heard by `onEach` as it ends. */
export function removeFolders(root, folders, onEach = () => {}) {
  const listedNow = folders.some((folder) => folder.remove) ? parseWorktrees(git(root, ['worktree', 'list', '--porcelain']).out) : [];
  return folders.map((folder) => {
    const outcome = folder.remove ? removeFolder(folder, listedNow) : folder;
    onEach(outcome);
    return outcome;
  });
}

function pruneHeader(entries, { plan = false, atMerge = false, folders = [] } = {}) {
  const what = entries.length === 0
    ? `no branch but ${MAIN} is merged into ${MAIN}`
    : `${entries.length} ${entries.length === 1 ? 'branch' : 'branches'} merged into ${MAIN}`;
  const stray = folders.length
    ? `; ${folders.length} ${folders.length === 1 ? 'folder' : 'folders'} under ${WORKTREES.split('\\').join('/')}/ git does not list`
    : '';
  const note = [atMerge && "at the merge's start", plan && 'a plan, so nothing is removed'].filter(Boolean).join('; ');
  return `prune${note ? ` (${note})` : ''}: ${what}${stray}`;
}

/** One branch's line, in the lane report's columns: what happens to it (or would), and why. */
function pruneLine(root, entry, applied) {
  const goes = applied ? entry.removed === true : entry.remove;
  const verb = applied ? (goes ? 'removed' : 'kept') : (goes ? 'remove' : 'keep');
  let text = entry.why;
  if (goes) {
    const at = entry.worktree && shown(root, entry.worktree.path);
    const unlocked = entry.unlock ? `, unlocked from the agent harness (${entry.worktree.lockReason}),` : '';
    if (!entry.worktree) text = 'the branch (no worktree has it checked out)';
    else if (entry.gone) text = `its worktree's record (the folder ${at} is gone already)${unlocked} and the branch`;
    else text = `its worktree ${at}${unlocked} and the branch${entry.left ?? ''}`;
  }
  return `  ${pad(verb, 8)} ${pad(entry.branch, 34)} ${text}`;
}

/** One half-removed folder's line, in the same columns. */
function folderLine(root, folder, applied) {
  const goes = applied ? folder.removed === true : folder.remove;
  const verb = applied ? (goes ? 'removed' : 'kept') : (goes ? 'remove' : 'keep');
  const text = goes ? 'a folder git does not list, with no .git: what a worktree removal that did not finish left' : folder.why;
  return `  ${pad(verb, 8)} ${pad(shown(root, folder.path), 34)} ${text}`;
}

/** The plan's lines, changing nothing: each branch, then each folder. */
function printPrunePlan(root, { entries, folders, gateTree }, atMerge = false) {
  console.log(pruneHeader(entries, { plan: true, atMerge, folders }));
  for (const entry of entries) console.log(pruneLine(root, entry, false));
  for (const folder of folders) console.log(folderLine(root, folder, false));
  if (gateTree) console.log(gateTreeLine(gateTree, false));
}

/** The plan applied, a line for each branch and folder as it ends, and the gate tree's last (GATEWT1). */
function applyPrunePlan(root, { entries, folders, gateTree }) {
  console.log(pruneHeader(entries, { folders }));
  applyPrune(root, entries, (entry) => console.log(pruneLine(root, entry, true)));
  removeFolders(root, folders, (folder) => console.log(folderLine(root, folder, true)));
  if (gateTree) console.log(gateTreeLine(gateTree.remove ? removeGateTree(gateTree.home, gateTree) : gateTree, true));
}

/** The prune a merge runs at its start and a batch at its end. It never stops the merge. */
function pruneAround(root, hold) {
  try {
    const plan = planPrune(root, { hold, atMerge: true });
    if (plan.current !== MAIN) {
      console.log(`prune: skipped: this checkout has ${plan.current || 'a detached HEAD'} out, not ${MAIN}`);
      return;
    }
    applyPrunePlan(root, plan);
  } catch (error) {
    console.log(`prune: skipped (${error?.message ?? error})`);
  }
}

/** `--prune`: the prune alone, or its plan. Applying it runs from main's checkout, as a merge does. */
function pruneOnly(root, options) {
  const plan = planPrune(root);
  if (options.plan) {
    printPrunePlan(root, plan);
    return 0;
  }
  if (plan.current !== MAIN) {
    throw new Refusal(`--prune runs from ${MAIN}'s checkout: git branch -d asks whether a branch is merged into the one checked out, and here that is ${plan.current || 'a detached HEAD'}`);
  }
  applyPrunePlan(root, plan);
  return 0;
}

// ---------------------------------------------------------------------------------------------------
// The batch record

const statePath = (root) => join(root, STATE);
const readState = (root) => (existsSync(statePath(root)) ? JSON.parse(readFileSync(statePath(root), 'utf8')) : null);

function writeState(root, state) {
  writeAtomic(statePath(root), `${JSON.stringify(state, null, 2)}\n`);
}

const dropState = (root) => rmSync(statePath(root), { force: true });

// ---------------------------------------------------------------------------------------------------
// Running gates

const shown = (root, file) => relative(root, file).split('\\').join('/');
const pad = (text, width) => (text.length >= width ? `${text} ` : text.padEnd(width));
const count = (n, word) => `${n} ${word}${n === 1 ? '' : 's'}`;

function duration(ms) {
  const seconds = Math.round(ms / 1000);
  return seconds < 60 ? `${seconds} s` : `${Math.floor(seconds / 60)} m ${seconds % 60} s`;
}

/**
 * CIPRIO1c: what a step the run ended answers, never a gate's exit: a process's code is never negative (POSIX's are
 * 0 to 255, and Windows reports its unsigned), so it is never read as a failure.
 */
export const STEP_ENDED = -1;

/**
 * CIPRIO1c: the step trees a run has running, each by its step's own pid, so a superseded or stopped run ends every one at
 * once. `end(why)` ends each held tree (`endTree`, the seam) and leaves `ended` saying why; a step started after it starts
 * nothing (`guarded`). `release` answers whether the run ended that step, so its own exit is not read as the gate's.
 */
export function stepTrees({ end = endTree } = {}) {
  const live = new Set();
  const ended = new Set();
  const trees = {
    ended: null,
    hold(pid) {
      live.add(pid);
    },
    release(pid) {
      live.delete(pid);
      return ended.delete(pid);
    },
    end(why) {
      trees.ended ??= why;
      for (const pid of [...live]) {
        ended.add(pid);
        end(pid);
      }
    },
  };
  return trees;
}

/**
 * CIPRIO1c: end a step's whole tree by the step's own pid, never by image name (the owner's processes share the names).
 * On Windows a kill reaches the one process it names, and a shell's command outlives it (FAMILYFAIL1), so `taskkill /T`
 * ends what the step started too. Elsewhere `ps` says what is under it, each then killed. What a process of the tree left
 * running after its own parent ended is in no tree, and no build server is shut down: the run's are its own
 * (`BESIDE_ENVIRONMENT`), and the owner's are not to be touched.
 */
export function endTree(pid, { platform = process.platform, run = spawnSync, kill = (each, signal) => process.kill(each, signal) } = {}) {
  if (platform === 'win32') {
    run('taskkill', ['/T', '/F', '/PID', String(pid)], { stdio: 'ignore', windowsHide: true });
    return;
  }
  const listed = run('ps', ['-A', '-o', 'pid=', '-o', 'ppid='], { encoding: 'utf8' });
  for (const each of [pid, ...descendants(listed.stdout ?? '', pid)]) {
    try {
      kill(each, 'SIGKILL');
    } catch {
      // Already ended.
    }
  }
}

/** The pids under `pid` in a `ps -o pid= -o ppid=` listing, at any depth. */
export function descendants(listing, pid) {
  const children = new Map();
  for (const line of listing.split('\n')) {
    const [child, parent] = line.trim().split(/\s+/).map(Number);
    if (Number.isInteger(child) && Number.isInteger(parent)) children.set(parent, [...(children.get(parent) ?? []), child]);
  }
  const found = [];
  const walk = (at) => {
    for (const child of children.get(at) ?? []) {
      if (child === pid || found.includes(child)) continue;
      found.push(child);
      walk(child);
    }
  };
  walk(pid);
  return found;
}

/**
 * One command through the platform shell, as the devkit runs a declared gate, its output into `fd`.
 * The gate runners take it as a parameter, so their logic is tested without starting a process. Given the run's
 * `trees`, its child is held there while it runs, and a step the run ended answers `STEP_ENDED` (CIPRIO1c).
 */
export function runStep(command, cwd, fd, { raise = null, trees = null } = {}) {
  writeSync(fd, `$ ${command}\n`);
  return new Promise((done) => {
    let settled = false;
    let child = null;
    const finish = (code) => {
      if (!settled) {
        settled = true;
        done(child?.pid && trees?.release(child.pid) ? STEP_ENDED : code);
      }
    };
    child = spawn(command, { cwd, shell: true, stdio: ['ignore', fd, fd], windowsHide: true });
    // FAMILYFAIL1b: a rehearsal's step is raised to normal at once, so what it starts inherits normal. The shell has not yet
    // created its own child when `spawn` returns, so the race is small. `raise` says its own failure once.
    if (raise && child.pid) raise(child.pid);
    // CIPRIO1c: held before anything else can run, so the run's end cannot miss it.
    if (child.pid) trees?.hold(child.pid);
    child.on('error', (error) => {
      writeSync(fd, `could not start: ${error.message}\n`);
      finish(127);
    });
    child.on('close', (code, signal) => finish(code ?? (signal ? 128 : 1)));
  });
}

/** CIPRIO1c: a step that starts nothing once the run has ended its trees, and is handed them otherwise. */
const guarded = (step, trees) => (command, cwd, fd, options = {}) => {
  if (!trees) return step(command, cwd, fd, options);
  if (trees.ended) {
    writeSync(fd, `(not started: ${command})\n`);
    return Promise.resolve(STEP_ENDED);
  }
  return step(command, cwd, fd, { ...options, trees });
};

/**
 * CIPRIO1c: whether a step's answer is the run's end rather than the gate's: one it ended, or one that failed once the run
 * had ended (a person's Ctrl+C reaches the console's processes too, and one may exit on its own before it is ended).
 */
const endedBy = (code, trees) => code === STEP_ENDED || (code !== 0 && Boolean(trees?.ended));

const endedNote = (trees) => `ended, not failed: ${trees?.ended ?? 'the run ended it'}`;

/**
 * A log is written beside and renamed when its gate ends, so a half-written one never reads as whole. Not `writeAtomic`
 * (REFAC2): the gate streams into it for minutes, a re-run reads it as it grows, and it lands whatever the gate did,
 * where `writeAtomic` throws away what a failure leaves.
 */
async function withLog(file, work) {
  const partial = `${file}.partial`;
  writeFileSync(partial, '');
  const fd = openSync(partial, 'a');
  try {
    return await work(fd, partial);
  } finally {
    closeSync(fd);
    renameHeld(partial, file);
  }
}

async function rerunAlone(root, gate, dir, tests, step, trees = null) {
  const log = join(dir, `${gate.name}.rerun.log`);
  const cwd = gate.cwd ? join(root, gate.cwd) : root;
  const failed = [];
  let ended = false;
  await withLog(log, async (fd, partial) => {
    for (const test of tests) {
      const from = statSync(partial).size;
      const code = await step(rerunCommand(gate.run, test), cwd, fd, { raise: rehearsalRaise() });
      if (endedBy(code, trees)) {
        writeSync(fd, `\n(${endedNote(trees)})\n`);
        ended = true;
        break;
      }
      const output = readFileSync(partial).subarray(from).toString('utf8');
      writeSync(fd, `\n(exited ${code})\n\n`);
      if (code !== 0 || !rerunPassed(output)) failed.push(test);
    }
  });
  // Which failed again, not only whether any did: a note naming every re-run test as failed sends a reader after ones that passed.
  return { passed: !ended && failed.length === 0, failed, log, ended };
}

/**
 * The gate (as `command`, its own run unless given) into its log: the gate's exit, or `STEP_ENDED` once the run ended its
 * trees (CIPRIO1c). A gate has one step: BEFORESTEP1 removed the steps before it.
 */
function runSteps(root, gate, log, step, command = gate.run, trees = null) {
  const cwd = gate.cwd ? join(root, gate.cwd) : root;
  return withLog(log, async (fd) => {
    const exit = await step(command, cwd, fd, { raise: isProcessLane(gate) || gate.clock ? rehearsalRaise() : null });
    if (endedBy(exit, trees)) {
      writeSync(fd, `\n(${endedNote(trees)})\n`);
      return STEP_ENDED;
    }
    writeSync(fd, `\n(exited ${exit})\n`);
    return exit;
  });
}

/** CIPRIO1c: a gate the run ended: no verdict on its tree, and its note says so, after what it had done before. */
function endedResult(result, started, trees, before = '') {
  return Object.assign(result, { verdict: 'ENDED', note: [before, endedNote(trees)].filter(Boolean).join('; '), ms: Date.now() - started });
}

/**
 * One gate, its steps before it, and its flake re-run: a result with its verdict, note and log. `settled`, handed to a
 * Process-lane gate, resolves once the light lane beside it has ended (GATELANES1). `noBuild` runs a `dotnet test`
 * gate with `--no-build`, on the build `built` (its fast half's pass in this run) made, and its note says so (GATEBUILD1).
 *
 * CIPRIO1c: given the run's `trees`, each step's child is held there; a gate whose step the run ended is `ENDED`, never
 * FAIL, and nothing of it starts after: no step before it, no rehearsal run again, no test run alone.
 */
export async function runGate(root, gate, dir, { step = runStep, settled = null, noBuild = false, built = null, trees = null } = {}) {
  const log = join(dir, `${gate.name}.log`);
  const started = Date.now();
  const go = guarded(step, trees);
  // PROC1: a Process half also leaves each test's duration beside its log, read once it ends.
  const trx = isProcessGate(gate) ? join(dir, `${gate.name}.trx`) : null;
  if (trx) rmSync(trx, { force: true });
  const reused = noBuild && testBuild(gate.run) !== null;
  const command = reused ? noBuildCommand(gate.run) : gate.run;
  const code = await runSteps(root, gate, log, go, trx ? trxCommand(command, trx) : command, trees);
  const result = { gate, code, log, verdict: code === 0 ? 'PASS' : 'FAIL', note: code === 0 ? '' : `exit ${code}` };
  if (code === STEP_ENDED) return endedResult(result, started, trees);
  if (trx && existsSync(trx)) Object.assign(result, { trx, slowest: slowestClasses(readFileSync(trx, 'utf8')) });
  if (code !== 0 && gateKind(gate.run) === 'rehearsal') {
    // A rehearsal that died is run again once, whole, into a log of its own beside the first (LEFT1).
    const decision = rehearsalDecision(readFileSync(log, 'utf8'), code, gate.run);
    if (!decision.rerun) {
      result.note = `exit ${code}; ${decision.reason}`;
    } else if (trees?.ended) {
      return endedResult(result, started, trees, `exit ${code}; ${decision.reason}; not run again`);
    } else {
      const again = join(dir, `${gate.name}.rerun.log`);
      const second = await runSteps(root, gate, again, go, gate.run, trees);
      if (second === STEP_ENDED) {
        return endedResult(result, started, trees, `exit ${code}; ${decision.reason}; run again, ${shown(root, again)}`);
      } else if (second === 0) {
        Object.assign(result, { verdict: 'FLAKE', flakes: [gate.name], note: `${decision.reason}, and it passed when run again; ${shown(root, again)}` });
      } else {
        result.note = `${decision.reason}; run again, it failed again (exit ${second}); ${shown(root, again)}`;
      }
    }
  } else if (code !== 0 && /^dotnet test\b/.test(gate.run) && !isProcessGate(gate)) {
    result.note = `exit ${code}; outside the Process category, a failure is real and never re-run`;
  } else if (code !== 0 && /^dotnet test\b/.test(gate.run)) {
    const decision = flakeDecision(readFileSync(log, 'utf8'));
    if (decision.rerun.length === 0) {
      result.note = `exit ${code}; ${decision.reason}`;
    } else {
      // GATELANES1: alone means with nothing beside it, as FLAKE1 measured it, so the light lane ends first.
      if (settled) await settled();
      // CIPRIO1c: main may move on, or a person stop the run, while the half waits to be alone.
      if (trees?.ended) return endedResult(result, started, trees, `exit ${code}; not run alone`);
      const rerun = await rerunAlone(root, gate, dir, decision.rerun, go, trees);
      const names = decision.rerun.map(short).join(', ');
      if (rerun.ended) {
        return endedResult(result, started, trees, `exit ${code}; run alone, ${shown(root, rerun.log)}`);
      } else if (rerun.passed) {
        Object.assign(result, { verdict: 'FLAKE', flakes: decision.rerun, note: `failed in the full run and passed alone: ${names}; ${shown(root, rerun.log)}` });
      } else {
        const alone = decision.rerun.filter((test) => !rerun.failed.includes(test));
        const passedAlone = alone.length ? `; passed alone: ${alone.map(short).join(', ')}` : '';
        result.note = `exit ${code}; failed again alone: ${rerun.failed.map(short).join(', ')}${passedAlone}; ${shown(root, rerun.log)}`;
      }
    }
  }
  if (reused) result.note = [result.note, `no build: ${built?.gate ?? 'its fast half'} built this tree in this run`].filter(Boolean).join('; ');
  result.ms = Date.now() - started;
  return result;
}

function resultLine(root, result) {
  const note = result.note ? `  (${result.note})` : '';
  return `  ${pad(result.verdict, 8)} ${pad(result.gate.name, 16)} ${pad(duration(result.ms), 9)} ${shown(root, result.log)}${note}`;
}

/**
 * PROC1: the slowest classes of a Process gate's run, under its line. PROCPAR1b: the classes run at once, so their times
 * are their own and the wall is the run's (`result.ms`), said beside them.
 */
function slowestLines(root, result) {
  if (!result.slowest?.length) return [];
  const wall = result.ms === undefined ? '' : `; the half's wall was ${duration(result.ms)} (its classes ran at once, so the times do not add up to it)`;
  return [
    `           slowest classes of ${result.gate.name}, by their own times${wall}, from ${shown(root, result.trx)}:`,
    ...result.slowest.map((slow, i) => `           ${pad(`${i + 1}.`, 4)}${pad(duration(slow.ms), 10)} ${pad(count(slow.tests, 'test'), 10)} ${slow.name}`),
  ];
}

const notRunLine = (gate) => `  ${pad('NOT RUN', 8)} ${gate.name}`;

/** A promise resolved from outside: a gate's end, which a gate of the other lane waits for. */
function signal() {
  let resolve;
  const promise = new Promise((done) => { resolve = done; });
  return { promise, resolve };
}

/** What a two-lane run says before it starts: each lane, and what each Process-lane gate waits for and why. */
function laneLines(schedule) {
  const names = (list) => list.join(', ');
  const turn = '--one-at-a-time runs every gate in turn, for a loaded machine';
  const out = schedule.process.length
    ? [`  two lanes at once, the light one up to ${schedule.width} gates wide (${turn}):`]
    : [`  the light lane, up to ${schedule.width} gates at once (${turn}):`];
  out.push(`    light    ${names(schedule.light.map((gate) => gate.name))}`);
  if (schedule.process.length) out.push(`    Process  ${names(schedule.process.map((gate) => gate.name))}, one at a time`);
  // REHLANE1: the rehearsals that run beside a git half; REHLANE2: one after the other when there are more than one.
  const besideGit = schedule.process.filter((gate) => runsBesideGitHalf(gate) && schedule.beside[gate.name]?.length);
  if (besideGit.length === 1) {
    const [gate] = besideGit;
    out.push(`    ${gate.name} runs beside ${names(schedule.beside[gate.name])}: it builds nothing into a folder a Process half runs from, and shuts no build server down`);
  } else if (besideGit.length > 1) {
    out.push(`    ${names(besideGit.map((gate) => gate.name))} run beside ${names(schedule.beside[besideGit[0].name])}, one after the other: none builds into a folder a Process half runs from, or shuts a build server down`);
  }
  // GATELANES2: the light gates that build one component, which run in turn.
  const building = new Map();
  for (const gate of schedule.light) {
    for (const component of schedule.components[gate.name] ?? []) building.set(component, [...(building.get(component) ?? []), gate.name]);
  }
  for (const [component, gates] of building) if (gates.length > 1) out.push(`    ${names(gates)} build ${component}: one at a time`);
  const groups = new Map();
  for (const gate of schedule.process) {
    const { after, component } = schedule.waits[gate.name];
    if (!after.length) continue;
    const key = `${component}\0${names(after)}`;
    if (!groups.has(key)) groups.set(key, { after, component, gates: [] });
    groups.get(key).gates.push(gate.name);
  }
  for (const { after, component, gates } of groups.values()) {
    const verb = gates.length === 1 ? 'starts' : 'start';
    const own = after.filter((name) => schedule.components[name]?.includes(component));
    const loud = after.filter((name) => !own.includes(name));
    out.push(component === null
      ? `    ${names(gates)} ${verb} once the light lane has ended: a rehearsal builds what its command does not name`
      : `    ${names(gates)} ${verb} once ${names(after)} ${after.length === 1 ? 'has' : 'have'} ended: ${own.length ? `${names(own)} build ${component} first${loud.length ? ', ' : ''}` : ''}${loud.length ? `${names(loud)} load${loud.length === 1 ? 's' : ''} the machine` : ''}`);
  }
  // GATEBUILD1: the Process halves that run their fast half's build.
  const reusing = new Map();
  for (const gate of schedule.process) {
    const fast = schedule.reuses[gate.name];
    if (fast) reusing.set(fast, [...(reusing.get(fast) ?? []), gate.name]);
  }
  for (const [fast, gates] of reusing) {
    out.push(`    ${names(gates)} ${gates.length === 1 ? 'runs' : 'run'} --no-build when ${fast} has passed in this run on the tree ${gates.length === 1 ? 'it starts' : 'they start'} on`);
  }
  return out;
}

/**
 * One gate after another, in the plan's order: every run before GATELANES1, and `--one-at-a-time`. `beside` names what
 * each gate is handed (GATEBUILD1: a Process half, its fast half's pass), and `heard` hears each result.
 */
async function runInTurn(root, gates, dir, { keepGoing, run, print, beside = () => ({}), heard = () => {}, halt = () => false }) {
  const results = [];
  for (let i = 0; i < gates.length; i += 1) {
    if (halt()) {
      for (const skipped of gates.slice(i)) print(notRunLine(skipped));
      break;
    }
    const result = await run(root, gates[i], dir, beside(gates[i]));
    heard(result);
    results.push(result);
    print(resultLine(root, result));
    for (const line of slowestLines(root, result)) print(line);
    if (result.verdict === 'FAIL' && !keepGoing) {
      for (const skipped of gates.slice(i + 1)) print(notRunLine(skipped));
      break;
    }
  }
  return results;
}

/**
 * The gates, one line each as it ends (and a Process gate's slowest classes under it). The first failure starts
 * nothing more and every gate not started is named as not run, unless `keepGoing`.
 *
 * GATELANES1: the light lane runs beside the Process lane (`laneSchedule`), and once both have ended every gate's line
 * is printed again in the plan's order. A failure in either lane starts nothing more in either; the gates running run to
 * their end and keep their verdicts, since a failure stops no gate midway (a superseded or stopped run beside ends them
 * through its trees, and `halt` then starts nothing more: CIPRIO1c). `oneAtATime`, or a run with no light gate
 * or one light gate alone, runs every gate in turn as before. The results are in the plan's order.
 *
 * GATELANES2: the light lane runs up to `width` gates at once, each starting, in the lane's order, once a place is free
 * and no gate running builds a component it builds; the Process lane stays one gate at a time. `scripts` are the root
 * scripts a gate's `npm run` is read through, the root `package.json`'s unless given.
 *
 * GATEBUILD1: a Process half is handed `built`, its fast half's pass in this run and the tree that ran on, which the
 * runner compares with the tree the half starts on (`reuseBuild`). A run with no fast half (a `--rerun` or `--stale`
 * that names a Process half alone) hands none, and the half builds.
 */
export async function runGates(root, gates, dir, { keepGoing = false, oneAtATime = false, width = LIGHT_WIDTH, scripts = null, run = runGate, print = console.log, halt = () => false } = {}) {
  const schedule = laneSchedule(gates, { scripts: scripts ?? rootScripts(root), width });
  const passed = new Map();
  const heard = (result) => {
    if (result.verdict === 'PASS' && !isProcessLane(result.gate)) passed.set(result.gate.name, { gate: result.gate.name, tree: result.tree ?? null });
  };
  const built = (gate) => (passed.has(schedule.reuses[gate.name]) ? { built: passed.get(schedule.reuses[gate.name]) } : {});
  if (oneAtATime || !schedule.light.length || (schedule.light.length === 1 && !schedule.process.length)) {
    return runInTurn(root, gates, dir, { keepGoing, run, print, beside: built, heard, halt });
  }

  for (const line of laneLines(schedule)) print(line);
  const results = new Map();
  const ended = new Map(schedule.light.map((gate) => [gate.name, signal()]));
  const lightEnded = signal();
  let stopped = false;
  // CIPRIO1: asked before each gate starts; true stops everything not started, as a failure does. The gates running
  // when it turned were ended by the run's trees (CIPRIO1c), not here.
  const halted = () => {
    if (!stopped && halt()) stopped = true;
    return stopped;
  };
  const one = async (gate, beside) => {
    let result;
    try {
      result = await run(root, gate, dir, beside);
    } catch (error) {
      // A tool error ends the run as it always did, once the gates running beside it have ended.
      stopped = true;
      throw error;
    }
    results.set(gate.name, result);
    heard(result);
    print(resultLine(root, result));
    for (const line of slowestLines(root, result)) print(line);
    if (result.verdict === 'FAIL' && !keepGoing) stopped = true;
  };
  const light = (async () => {
    const waiting = [...schedule.light];
    const running = new Map();
    const held = new Set();
    let thrown = null;
    try {
      for (;;) {
        for (const gate of [...waiting]) {
          if (halted() || running.size >= schedule.width) break;
          const builds = schedule.components[gate.name];
          if (builds.some((component) => held.has(component))) continue;
          waiting.splice(waiting.indexOf(gate), 1);
          for (const component of builds) held.add(component);
          running.set(gate.name, one(gate, {}).catch((error) => { thrown ??= error; }).finally(() => {
            for (const component of builds) held.delete(component);
            running.delete(gate.name);
            ended.get(gate.name).resolve();
          }));
        }
        if (!running.size) break;
        await Promise.race(running.values());
      }
    } finally {
      // A gate the stop left unstarted has ended too, for whatever waits on it.
      for (const each of ended.values()) each.resolve();
      lightEnded.resolve();
    }
    if (thrown) throw thrown;
  })();
  // REHLANE1: a Process-lane gate starts once every gate before it in the lane has ended, but those it may run beside.
  const processEnded = new Map(schedule.process.map((gate) => [gate.name, signal()]));
  const processRunning = new Set();
  // A gate re-running its failures alone holds every other Process-lane start until it ends. Only a Process half asks, and
  // no two halves run at once, so one holds at a time.
  const holds = new Map();
  let alone = null;
  // GATELANES1: alone means once the light lane has ended, and now once the gates beside it in its own lane have ended too.
  const settled = (gate) => async () => {
    const hold = signal();
    holds.set(gate.name, hold);
    alone = hold;
    await lightEnded.promise;
    await Promise.all([...processRunning].filter((name) => name !== gate.name).map((name) => processEnded.get(name).promise));
  };
  const processLane = (async () => {
    const lane = schedule.process.map((gate, i) => (async () => {
      try {
        await Promise.all(schedule.waits[gate.name].after.map((name) => ended.get(name).promise));
        const beside = schedule.beside[gate.name] ?? [];
        await Promise.all(schedule.process.slice(0, i).filter((other) => !beside.includes(other.name)).map((other) => processEnded.get(other.name).promise));
        while (alone) await alone.promise;
        if (halted()) return;
        processRunning.add(gate.name);
        await one(gate, { settled: settled(gate), ...built(gate) });
      } finally {
        processRunning.delete(gate.name);
        const hold = holds.get(gate.name);
        if (hold) {
          holds.delete(gate.name);
          if (alone === hold) alone = null;
          hold.resolve();
        }
        processEnded.get(gate.name).resolve();
      }
    })());
    const ends = await Promise.allSettled(lane);
    const failed = ends.find((end) => end.status === 'rejected');
    if (failed) throw failed.reason;
  })();
  const outcomes = await Promise.allSettled([light, processLane]);
  const thrown = outcomes.find((outcome) => outcome.status === 'rejected');
  if (thrown) throw thrown.reason;

  print("  every gate, in the plan's order:");
  for (const gate of gates) print(results.has(gate.name) ? resultLine(root, results.get(gate.name)) : notRunLine(gate));
  return gates.filter((gate) => results.has(gate.name)).map((gate) => results.get(gate.name));
}

function clearLogs(dir) {
  mkdirSync(dir, { recursive: true });
  for (const name of readdirSync(dir)) {
    if (/\.(log|partial|trx)$/.test(name)) rmSync(join(dir, name), { force: true });
  }
}

// ---------------------------------------------------------------------------------------------------
// Reports

/** A lane's row label: its id, then its title, so a lane reads as a dispatch prompt names it. */
const laneLabel = (lane) => `${pad(lane.id, 14)}${lane.title}`;

function laneReport(root, branch) {
  const changed = lines(git(root, ['diff', '--name-only', `HEAD...${branch}`]).out);
  const map = readLanes(root);
  const out = [`lanes: ${branch} changes ${count(changed.length, 'file')}`];
  if (!map || map.lanes.length === 0) {
    out.push(`  (${map ? `${LANES_FILE} declares no lanes` : `no ${LANES_FILE} here`})`);
    return out;
  }
  const placed = classify(changed, { ...map, union: unionRecords(root) });
  const steward = map.lanes.find((lane) => lane.steward);
  const row = (label, text) => `  ${pad(label, 32)} ${text}`;
  const list = (files, max = 6) => `${files.slice(0, max).join(', ')}${files.length > max ? `, … ${files.length - max} more` : ''}`;
  for (const lane of placed.lanes) out.push(row(laneLabel(lane), count(lane.files.length, 'file')));
  if (placed.steward.length) out.push(row(laneLabel(steward), `${count(placed.steward.length, 'file')} (the steward's)`));
  if (placed.shared.length) out.push(row('shared records', `${count(placed.shared.length, 'file')}: ${list(placed.shared)} (merge by union)`));
  if (placed.laneless.length) out.push(row('no lane', `${count(placed.laneless.length, 'file')}: ${list(placed.laneless)} (the map declares them laneless)`));
  if (placed.outside.length) out.push(row('outside every lane', `${count(placed.outside.length, 'file')}: ${list(placed.outside)} (the map places no such path; the lanes test refuses it)`));
  if (placed.lanes.length > 1) out.push(`  note: it crosses ${placed.lanes.length} lanes`);
  if (placed.steward.length) out.push(`  note: it edits the steward's records (${placed.steward.join(', ')}); a subagent never does`);
  return out;
}

function planLines(plan) {
  return plan.map((gate, i) => {
    return `  ${pad(`${i + 1}.`, 4)}${pad(gate.name, 16)} ${pad(gate.kind, 10)} ${gate.run}`;
  });
}

// ---------------------------------------------------------------------------------------------------
// The verbs

/** Paths from a `-z` listing: git quotes nothing there, so a name with any character comes back whole. */
const zPaths = (text) => text.split('\0').map((path) => path.trim()).filter(Boolean);

/** What the merge in place changes on main: tracked paths against HEAD, the parent's fixes included, and new files. */
function mergeChanges(root) {
  const tracked = zPaths(git(root, ['diff', '-z', '--name-only', '--no-renames', 'HEAD']).out);
  const untracked = zPaths(git(root, ['ls-files', '-z', '--others', '--exclude-standard']).out);
  return [...new Set([...tracked, ...untracked])];
}

/** What a branch changes since it left main: a plan's question, asked before anything is merged. */
const branchChanges = (root, branch) => zPaths(git(root, ['diff', '-z', '--name-only', '--no-renames', `HEAD...${branch}`]).out);

/**
 * The gates the merge in place runs, and every gate of the plan with why it runs or is skipped. Checks and
 * suites run at each merge of a batch by its own paths; the rehearsals run once, at the batch's last merge,
 * each one any of the batch's merges reached. `state.reached` keeps what each merge reached, for that.
 */
function chooseGates(root, state, plan) {
  const branch = state.branches[state.at];
  const last = state.at === state.branches.length - 1;
  const selection = selectGates(plan, mergeChanges(root), { lanes: readLanes(root)?.lanes ?? [], full: state.full === true, gated: state.gated === true });
  state.reached = { ...(state.reached ?? {}), [branch]: selection.gates.filter((entry) => entry.run).map((entry) => entry.gate.name) };
  const waiting = new Set();
  const display = selection.gates.map((entry) => {
    if (entry.gate.kind !== 'rehearsal') return entry;
    if (!last) {
      if (!entry.run) return entry;
      waiting.add(entry.gate.name);
      return { ...entry, run: false, why: `waits for the batch's last branch (${state.branches.at(-1)}); ${entry.why}` };
    }
    if (entry.run) return entry;
    const by = state.branches.filter((other) => other !== branch && state.reached[other]?.includes(entry.gate.name));
    return by.length ? { ...entry, run: true, why: `reached by ${by.join(', ')}, earlier in this batch` } : entry;
  });
  return {
    gates: display,
    chosen: display.filter((entry) => entry.run).map((entry) => entry.gate),
    // What the lane table left out of this merge, not counting a rehearsal that waits for the batch's end.
    skipped: display.filter((entry) => !entry.run && !waiting.has(entry.gate.name)).length,
  };
}

const headCommit = (root) => git(root, ['rev-parse', 'HEAD'], { allowFail: true }).out.trim() || null;

/**
 * The gates run in order, each verdict recorded with the tree it ran on (GATE3), as its gate ends, so a run
 * cut short keeps what it learned. `onResult` hears each result. A gate the run's `trees` ended gives no verdict (CIPRIO1c).
 */
function runRecorded(root, gates, dir, { keepGoing = false, oneAtATime = false, branch = null, onResult = () => {}, halt = undefined, trees = null } = {}) {
  const commit = headCommit(root);
  return runGates(root, gates, dir, {
    keepGoing,
    oneAtATime,
    halt,
    run: async (where, gate, logs, beside) => {
      const tree = contentTree(where);
      const started = nowIso();
      // GATEBUILD1: a Process half runs its fast half's build when that passed in this run on this same tree.
      const result = await runGate(where, gate, logs, { ...beside, noBuild: reuseBuild(beside?.built, tree), trees });
      Object.assign(result, { tree, at: started });
      if (result.verdict !== 'ENDED') addVerdicts(verdictsHome(where), [{ gate: gate.name, verdict: result.verdict, tree, at: started, commit, branch }]);
      onResult(result);
      return result;
    },
  });
}

/** The line that ends a merge's run with gates skipped: the full set is still owed before a stage. */
const owedLine = (skipped) => (skipped
  ? `  ${count(skipped, 'gate')} of the plan skipped; the full set runs before the install is staged (${FULL_COMMAND}, or --full on a merge).`
  : null);

async function gateMerge(root, state, plan) {
  const branch = state.branches[state.at];
  const last = state.at === state.branches.length - 1;
  // Before the gates choose, so the merge's changed paths include what the writing changed (ORIENT1a). The notes
  // first (MERGEJOIN1): a blank line moves the lines the decisions digest points to.
  setApartNotes(root);
  writeGenerated(root);
  const { gates: selection, chosen: gates, skipped } = chooseGates(root, state, plan);
  const dir = join(root, SCRATCH, `merge-${slug(branch)}`);
  clearLogs(dir);
  // `--continue` gates the merge again whole: the verdicts of an earlier run are dropped.
  state.results = { [branch]: {} };
  writeState(root, state);

  console.log(`gates for ${branch}: ${gates.length} of ${plan.length}, fast first${state.full ? ' (--full)' : ', by the lanes it reaches'}${last ? '' : `; the rehearsals wait for the batch's last branch (${state.branches.at(-1)})`}`);
  for (const line of selectionLines(selection)) console.log(line);
  const results = await runRecorded(root, gates, dir, {
    keepGoing: state.keepGoing,
    oneAtATime: state.oneAtATime === true,
    branch,
    onResult: (result) => {
      state.results[branch][result.gate.name] = { verdict: result.verdict, at: result.at, tree: result.tree, note: result.note, log: shown(root, result.log) };
      writeState(root, state);
    },
  });
  const failed = results.filter((result) => result.verdict === 'FAIL');
  const flakes = results.filter((result) => result.verdict === 'FLAKE');
  const notRun = gates.length - results.length;

  console.log('');
  if (failed.length) {
    const names = failed.map((result) => result.gate.name).join(' ');
    console.log(`merge-branch: ${branch} is merged into ${MAIN}, NOT committed, and ${count(failed.length, 'gate')} failed: ${failed.map((result) => result.gate.name).join(', ')}${notRun ? `; ${notRun} not run` : ''}.`);
    console.log('  The merge is left in place. Read the failing log and fix it in the merge; then');
    console.log(`  node tools/merge-branch.mjs --rerun ${names} runs it again, keeping each other verdict no path your fix changes reaches (and runs what did not run),`);
    console.log('  or --continue gates the merge again whole. Or git merge --abort.');
    return 1;
  }
  const flakeNote = flakes.length ? `, with ${count(flakes.length, 'flake')} (${flakes.map((result) => result.gate.name).join(', ')}): record it under FLAKE1` : '';
  console.log(`merge-branch: ${branch} is merged into ${MAIN}, NOT committed; ${count(results.length, 'gate')} passed${flakeNote}.`);
  refreshKnowledgeServer(root);
  const owed = owedLine(skipped);
  if (owed) console.log(owed);
  if (last) {
    console.log('  Next: read the diff (git diff --cached), write the records, and commit the merge.');
  } else {
    console.log(`  Next: read the diff (git diff --cached), write the records, commit the merge; then node tools/merge-branch.mjs --continue merges ${state.branches[state.at + 1]}.`);
  }
  return 0;
}

/**
 * `--rerun <gate>…` (GATE4): the named gates again on the merge in place, then any gate of its selection with
 * no verdict yet, and any whose verdict a path changed since reaches (GATE6), keeping every other verdict and
 * saying when each was given. Every verdict kept is one a stage counts. With no merge open, `rerunCheckout`.
 */
async function rerunGates(root, options) {
  const state = readState(root);
  if (!state) {
    throw new Refusal('a merge is open here that this tool did not leave, so it has no verdicts to keep: '
      + 'commit it or git merge --abort; with no merge open, --rerun gates the checkout');
  }
  requireMain(root);
  const branch = state.branches[state.at];
  if (git(root, ['rev-parse', 'MERGE_HEAD']).out.trim() !== state.tips[branch]) {
    throw new Refusal(`the merge in progress is not ${branch}'s, which is the one recorded`);
  }
  const { blocking, generated } = conflicts(root);
  if (blocking.length) return reportConflict(branch, blocking, generated);
  // A fix made in the merge may have moved what the index points to: it is written again before any gate, and a
  // note a fix glued is set apart first.
  setApartNotes(root);
  writeGenerated(root);
  const plan = readPlan(root);
  const unknown = options.rerun.filter((name) => !plan.some((gate) => gate.name === name));
  if (unknown.length) {
    throw new Refusal(`--rerun: no gate ${unknown.map((name) => `'${name}'`).join(', ')} in the plan: ${plan.map((gate) => gate.name).join(', ')}`);
  }

  const { chosen } = chooseGates(root, state, plan);
  const results = state.results?.[branch] ?? {};
  // GATE6: a verdict is kept only while no path changed since it was given reaches its gate; one a fix's path reaches
  // runs again, so every verdict kept is one the stage counts. A tree git cannot write judges none stale.
  const tree = contentTree(root);
  const judging = standingFor(root, tree);
  const standings = Object.fromEntries(chosen.filter((gate) => results[gate.name] && tree)
    .map((gate) => [gate.name, verdictStanding({ ...judging, from: results[gate.name].tree, gate: gate.name })]));
  const stale = Object.fromEntries(Object.entries(standings).filter(([, how]) => how.standing === 'stale').map(([name, how]) => [name, how.paths]));
  const planned = rerunPlan(plan.map((gate) => gate.name), chosen.map((gate) => gate.name), results, options.rerun, stale);
  const gates = plan.filter((gate) => planned.run.includes(gate.name));
  const dir = join(root, SCRATCH, `merge-${slug(branch)}`);
  mkdirSync(dir, { recursive: true });
  for (const gate of gates) {
    for (const suffix of ['.log', '.rerun.log', '.trx']) rmSync(join(dir, `${gate.name}${suffix}`), { force: true });
  }

  const again = planned.stale.length
    ? `, then ${planned.stale.map((entry) => entry.name).join(', ')}, whose ${planned.stale.length === 1 ? 'verdict' : 'verdicts'} a changed path reaches`
    : '';
  const fresh = planned.fresh.length ? `, then ${planned.fresh.join(', ')}, which ${planned.fresh.length === 1 ? 'has' : 'have'} no verdict yet` : '';
  console.log(`re-run on the merge of ${branch}: ${options.rerun.join(', ')}${again}${fresh}; ${count(planned.kept.length, 'verdict')} kept`);
  for (const entry of planned.stale) {
    const why = entry.paths.length ? `${listed(entry.paths, 4)} changed since, which reaches it` : 'its tree cannot be compared with this one';
    console.log(`  ${pad('stale', 8)} ${pad(entry.name, 16)} ${pad(entry.verdict, 8)} from ${when(entry.at)}: ${why}`);
  }
  for (const kept of planned.kept) {
    const how = standings[kept.name];
    const note = how && how.standing !== 'exact' ? `  (an earlier tree: ${how.standing === 'records' ? 'only the records changed since' : 'no path changed since reaches it'})` : '';
    console.log(`  ${pad('kept', 8)} ${pad(kept.name, 16)} ${pad(kept.verdict, 8)} from ${when(kept.at)}  ${results[kept.name]?.log ?? ''}${note}`);
  }
  state.results = { [branch]: results };
  const ran = await runRecorded(root, gates, dir, {
    keepGoing: state.keepGoing || options.keepGoing,
    oneAtATime: state.oneAtATime === true || options.oneAtATime,
    branch,
    onResult: (result) => {
      results[result.gate.name] = { verdict: result.verdict, at: result.at, tree: result.tree, note: result.note, log: shown(root, result.log) };
      writeState(root, state);
    },
  });

  const failed = [...ran.filter((result) => result.verdict === 'FAIL').map((result) => result.gate.name),
    ...planned.kept.filter((kept) => kept.verdict === 'FAIL').map((kept) => kept.name)];
  const notRun = gates.length - ran.length;
  const keptNote = planned.kept.length
    ? `${count(planned.kept.length, 'verdict')} kept (${planned.kept.map((kept) => `${kept.name}, from ${when(kept.at)}`).join('; ')})`
    : 'no verdict kept';
  console.log('');
  if (failed.length || notRun) {
    console.log(`merge-branch: ${branch} is merged into ${MAIN}, NOT committed, and ${count(failed.length, 'gate')} failed: ${failed.join(', ')}${notRun ? `; ${notRun} not run` : ''}; ${keptNote}.`);
    console.log('  The merge is left in place: fix it and --rerun the gate again, or --continue gates the merge again whole, or git merge --abort.');
    return 1;
  }
  console.log(`merge-branch: ${branch} is merged into ${MAIN}, NOT committed; ${count(ran.length, 'gate')} run and passed; ${keptNote}.`);
  refreshKnowledgeServer(root);
  console.log('  Next: read the diff (git diff --cached), write the records, and commit the merge.');
  return 0;
}

/**
 * `--full` on its own (GATE3): the whole plan on the checkout as it stands, merging nothing, every verdict recorded.
 * A run beside hands its `watch` on main's tip and its step `trees` (CIPRIO1c): the tip is polled while the gates run,
 * never after the last has ended, so a pass already earned is not taken back by a later move.
 */
async function gateCheckout(root, options, watch = null, trees = null) {
  if (mergeInProgress(root)) {
    throw new Refusal('a merge is open here: node tools/merge-branch.mjs --continue --full gates it with every gate, or git merge --abort');
  }
  const head = headCommit(root);
  if (!head) throw new Refusal('this checkout has no commit to gate');
  if (!isIgnored(root, `${SCRATCH}/full-probe.log`)) {
    throw new Refusal(`${SCRATCH}/ is not ignored here, so the gate logs would change the tree being gated`);
  }
  requireVerdictsIgnored(root);
  const plan = readPlan(root);
  const dir = join(root, SCRATCH, `full-${head.slice(0, 7)}`);
  clearLogs(dir);
  const dirty = treeState(root).changed.length > 0;
  console.log(`the full set on ${head.slice(0, 7)}${dirty ? ' and its uncommitted changes' : ''}: ${plan.length} gates, fast first; nothing is merged`);
  const halt = watch || trees ? () => Boolean(trees?.ended) || Boolean(watch?.check()) : undefined;
  const unwatch = watch ? watch.poll(TIP_POLL_MS) : () => {};
  let results;
  try {
    results = await runRecorded(root, plan, dir, { keepGoing: options.keepGoing, oneAtATime: options.oneAtATime, halt, trees });
  } finally {
    unwatch();
  }
  const failed = results.filter((result) => result.verdict === 'FAIL');
  const flakes = results.filter((result) => result.verdict === 'FLAKE');
  console.log('');
  if (watch?.superseded || trees?.ended) {
    // CIPRIO1, CIPRIO1c: never judged as a fail or a pass; the verdicts of the gates that ran stay recorded, and the gates
    // it ended gave none.
    const names = (list) => list.map((each) => each.gate?.name ?? each.name);
    const ran = names(results.filter((result) => result.verdict !== 'ENDED'));
    const ended = names(results.filter((result) => result.verdict === 'ENDED'));
    const left = names(plan.filter((gate) => !results.some((result) => result.gate.name === gate.name)));
    const lines = watch?.superseded ? supersededLines(watch.superseded, ran, left, ended) : stoppedLines(ran, left, ended);
    for (const line of lines) console.log(line);
    return 1;
  }
  if (failed.length || results.length < plan.length) {
    console.log(`merge-branch: the full set did NOT pass ${head.slice(0, 7)}: ${failed.map((result) => result.gate.name).join(', ')} failed${results.length < plan.length ? `; ${plan.length - results.length} not run` : ''}.`);
    // GATE6b: once the failure is fixed and committed, or at once when it flaked, the smaller command does the rest.
    const remedy = stageRemedy(judgeCheckout(root, plan)?.gates ?? []);
    if (remedy) console.log(remedyLine(remedy));
    return 1;
  }
  const flakeNote = flakes.length ? `, with ${count(flakes.length, 'flake')} (${flakes.map((result) => result.gate.name).join(', ')}): record it under FLAKE1` : '';
  console.log(`merge-branch: the full set passed ${head.slice(0, 7)}: ${count(results.length, 'gate')}${flakeNote}. A stage of this tree is allowed.`);
  return 0;
}

/**
 * The stage's judgement as lines, a gate each (GATE6): the verdict that counts and why it still stands, or, when a
 * newer one no longer does, that one as stale with the paths that made it so, which is what a refused stage says.
 */
function stageLines(gates) {
  return gates.map((gate) => {
    const head = (label) => `  ${pad(gate.name, 16)} ${pad(label, 6)}`;
    if (gate.stale) {
      const { verdict, paths } = gate.stale;
      const why = paths.length ? `on a tree before ${listed(paths, 4)} changed, which reaches it` : 'on a tree that cannot be compared with this one';
      return `${head('stale')} ${verdict.verdict} ${when(verdict.at)}, ${why}`;
    }
    if (!gate.verdict) return `${head('none')} no verdict on this tree`;
    const { standing, paths, records } = gate.standing;
    const others = standing === 'unreached' ? `paths it does not reach (${listed(paths, 4)})` : '';
    const recorded = records.length ? `the records (${listed(records, 4)})` : '';
    const where = standing === 'exact' ? 'on this tree' : `on a tree that differs from this one only by ${[others, recorded].filter(Boolean).join(' and ')}`;
    return `${head(gate.verdict.verdict)} ${when(gate.verdict.at)}  ${where}`;
  });
}

/**
 * `--passed` (GATE3): whether the full set passed the checkout as it stands, gate by gate, read from the
 * verdicts record: each gate's newest verdict that still stands (GATE6), or the stale one and the paths that
 * made it so. What `publish:desktop` asks before it builds. Exit 0 when it did, 1 when it did not.
 */
function passedOnly(root) {
  const plan = readPlan(root);
  const judged = judgeCheckout(root, plan);
  if (!judged) throw new Refusal("git could not write the checkout's tree, so whether the full set passed it cannot be told");
  const head = git(root, ['rev-parse', '--short', 'HEAD'], { allowFail: true }).out.trim() || 'no commit';
  console.log(`merge-branch: the full set has ${judged.passed ? '' : 'NOT '}passed this checkout (tree ${judged.tree.slice(0, 7)}, HEAD ${head}): `
    + `${plan.length - judged.missing.length} of ${plan.length} gates.`);
  for (const line of stageLines(judged.gates)) console.log(line);
  // GATE6b: the smallest command that would pass it, which the publish's refusal names in turn.
  const remedy = stageRemedy(judged.gates);
  if (remedy) console.log(remedyLine(remedy));
  return judged.passed ? 0 : 1;
}

/** The stage's judgement of the checkout as it stands, with its tree; null when git cannot write the tree. */
function judgeCheckout(root, plan) {
  const tree = contentTree(root);
  if (!tree) return null;
  return { tree, ...stageJudgement({ ...standingFor(root, tree), plan: plan.map((gate) => gate.name), verdicts: readVerdicts(verdictsHome(root)).verdicts }) };
}

/** Whether the full set passed the checkout, as a clause: what `--passed` and a re-run on the checkout say. */
const stageVerdict = (judged, plan) => `the full set has ${judged.passed ? '' : 'NOT '}passed this checkout: ${plan.length - judged.missing.length} of ${plan.length} gates`;

/**
 * `--rerun <gate>…` with no merge open, and `--stale` (GATE6b): on the checkout as it stands, merging nothing, the named
 * gates and each the stage calls stale (`checkoutRerun`), every verdict recorded where `--passed` reads it; then the
 * stage's answer, and the smallest command for what it still lacks. Outside a merge, `--rerun` used to refuse, so a gate
 * that flaked in the full set, or a fix committed after it, left `--full` again, about an hour and a half, as the only
 * way to a stage.
 *
 * A clean tree only, naming what is not: the verdicts are recorded with HEAD's commit, and a re-run is for a commit (one
 * whose gate flaked, or a fix committed past the full set), so an uncommitted change would be gated as part of a tree
 * no commit holds. `--full` is the verb that gates the working tree as it stands.
 */
async function rerunCheckout(root, options) {
  const verb = options.rerun.length ? '--rerun' : '--stale';
  const head = headCommit(root);
  if (!head) throw new Refusal('this checkout has no commit to gate');
  const { changed } = treeState(root);
  if (changed.length) {
    throw new Refusal(`${notClean(changed)}\n  ${verb} on the checkout gates a commit: commit the change first, or ${FULL_COMMAND} gates the tree as it stands`);
  }
  if (!isIgnored(root, `${SCRATCH}/full-probe.log`)) {
    throw new Refusal(`${SCRATCH}/ is not ignored here, so the gate logs would change the tree being gated`);
  }
  requireVerdictsIgnored(root);
  const plan = readPlan(root);
  const unknown = options.rerun.filter((name) => !plan.some((gate) => gate.name === name));
  if (unknown.length) {
    throw new Refusal(`--rerun: no gate ${unknown.map((name) => `'${name}'`).join(', ')} in the plan: ${plan.map((gate) => gate.name).join(', ')}`);
  }
  const judged = judgeCheckout(root, plan);
  if (!judged) throw new Refusal("git could not write the checkout's tree, so which verdicts still stand on it cannot be told");
  const planned = checkoutRerun(judged.gates, options.rerun);
  const gates = plan.filter((gate) => planned.run.includes(gate.name));
  const sha = head.slice(0, 7);
  // Beside the full set's logs for this commit, which it keeps but for the gates it runs again.
  const dir = join(root, SCRATCH, `full-${sha}`);
  mkdirSync(dir, { recursive: true });
  for (const gate of gates) {
    for (const suffix of ['.log', '.rerun.log', '.trx']) rmSync(join(dir, `${gate.name}${suffix}`), { force: true });
  }

  const staleNames = planned.stale.map((entry) => entry.name);
  const whose = `whose ${staleNames.length === 1 ? 'verdict' : 'verdicts'} a changed path reaches`;
  const kept = `${count(planned.kept.length, 'verdict')} kept; nothing is merged`;
  if (options.rerun.length) {
    console.log(`re-run on the checkout (HEAD ${sha}): ${options.rerun.join(', ')}${staleNames.length ? `, then ${staleNames.join(', ')}, ${whose}` : ''}; ${kept}`);
  } else if (staleNames.length) {
    console.log(`the stale gates on the checkout (HEAD ${sha}): ${staleNames.join(', ')}, ${whose}; ${kept}`);
  } else {
    console.log(`nothing is stale on the checkout (HEAD ${sha}): no verdict a changed path reaches; ${kept}`);
  }
  for (const entry of planned.stale) {
    const why = entry.paths.length ? `${listed(entry.paths, 4)} changed since, which reaches it` : 'its tree cannot be compared with this one';
    console.log(`  ${pad('stale', 8)} ${pad(entry.name, 16)} ${pad(entry.verdict, 8)} from ${when(entry.at)}: ${why}`);
  }
  // The rest as the stage reads them: each verdict that stands, a failure kept, and a gate with none.
  for (const line of stageLines(judged.gates.filter((gate) => !planned.run.includes(gate.name)))) console.log(line);
  const ran = await runRecorded(root, gates, dir, { keepGoing: options.keepGoing, oneAtATime: options.oneAtATime });

  const failed = ran.filter((result) => result.verdict === 'FAIL').map((result) => result.gate.name);
  const notRun = gates.length - ran.length;
  const flakes = ran.filter((result) => result.verdict === 'FLAKE').map((result) => result.gate.name);
  // The stage's answer, read again from the record the run just wrote, on the tree as it is now.
  const after = judgeCheckout(root, plan);
  const remedy = after ? stageRemedy(after.gates) : null;
  const stage = after ? stageVerdict(after, plan) : "git could not write the checkout's tree, so whether the full set passed it cannot be told";
  console.log('');
  if (failed.length || notRun) {
    console.log(`merge-branch: the re-run on ${sha} did NOT pass: ${failed.join(', ')} failed${notRun ? `; ${notRun} not run` : ''}; ${stage}.`);
  } else {
    const flakeNote = flakes.length ? `, with ${count(flakes.length, 'flake')} (${flakes.join(', ')}): record it under FLAKE1` : '';
    const ranNote = ran.length ? `${count(ran.length, 'gate')} run and passed on ${sha}${flakeNote}` : `no gate run on ${sha}`;
    const passed = after?.passed === true;
    console.log(`merge-branch: ${ranNote}; ${stage}.${passed ? ' A stage of this tree is allowed.' : ''}`);
  }
  if (remedy) console.log(remedyLine(remedy));
  return !failed.length && !notRun && after?.passed === true ? 0 : 1;
}

function reportConflict(branch, unmerged, generated = []) {
  console.log(`merge-branch: merging ${branch} stopped on a conflict in ${count(unmerged.length, 'file')}:`);
  for (const file of unmerged) console.log(`  ${file}`);
  if (generated.length) console.log(`  (and ${count(generated.length, 'generated file')}, written again from the merged tree once these are resolved)`);
  console.log('  Resolve them (code is never resolved for you; the union records resolve themselves), git add them,');
  console.log('  then node tools/merge-branch.mjs --continue to run the gates. Or git merge --abort.');
  return 1;
}

/** The unmerged paths a person resolves, and the generated ones a merge writes again (ORIENT1a). */
function conflicts(root) {
  const unmerged = unmergedPaths(root);
  return { blocking: unmerged.filter((path) => !isGenerated(path)), generated: unmerged.filter(isGenerated) };
}

async function mergeNext(root, state) {
  const branch = state.branches[state.at];
  const plan = readPlan(root);
  const tip = requireBranch(root, branch);
  const commits = branchCommits(root, branch);
  for (const line of laneReport(root, branch)) console.log(line);

  if (state.commitCheck) {
    const problems = commitProblems(root, branch, commits);
    if (problems.length) {
      throw new Refusal(`the commit check refuses ${branch}:\n${problems.map((problem) => `  ${problem}`).join('\n')}\n  (--no-commit-check merges it anyway)`, 1);
    }
    console.log(`commit check: ${count(commits.length, 'commit')}, each of its own with its Co-Authored-By line; nothing left uncommitted`);
  } else {
    console.log('commit check: skipped (--no-commit-check)');
  }

  // After every refusal, so a refused merge still touches nothing; the batch's branches are its own.
  if (state.autoPrune === false) console.log('prune: skipped (--no-prune)');
  else pruneAround(root, state.branches.slice(state.at));

  state.tips[branch] = tip;
  writeState(root, state);
  const merged = git(root, ['merge', '--no-ff', '--no-commit', branch], { allowFail: true });
  if (merged.status !== 0) {
    const { blocking, generated } = mergeInProgress(root) ? conflicts(root) : { blocking: [], generated: [] };
    if (blocking.length) return reportConflict(branch, blocking, generated);
    // A conflict only in what the merge writes anyway goes on to that writing (ORIENT1a).
    if (!generated.length) throw new Refusal(`git merge ${branch} failed:\n${merged.err || merged.out}`);
  }
  console.log(`merged ${branch} --no-ff --no-commit${state.branches.length > 1 ? ` (${state.at + 1} of ${state.branches.length})` : ''}`);
  return gateMerge(root, state, plan);
}

async function start(root, options) {
  preconditions(root);
  const pending = readState(root);
  if (pending && pending.at < pending.branches.length - 1) {
    throw new Refusal(`a batch is recorded with ${pending.branches.slice(pending.at + 1).join(', ')} still to merge: run --continue, or --drop-batch to forget it`);
  }
  // A misspelt later branch refuses before the first is merged; the first is checked as it merges.
  for (const branch of options.branches.slice(1)) requireBranch(root, branch);
  readPlan(root);
  return mergeNext(root, {
    branches: options.branches, at: 0, tips: {}, keepGoing: options.keepGoing, commitCheck: options.commitCheck, autoPrune: options.autoPrune,
    full: options.full, gated: options.gated, oneAtATime: options.oneAtATime,
  });
}

async function resume(root, options) {
  const state = readState(root);
  if (!state) throw new Refusal('nothing to continue: this tool has no merge recorded here');
  state.keepGoing = state.keepGoing || options.keepGoing;
  state.full = state.full === true || options.full;
  state.gated = state.gated === true || options.gated;
  // GATELANES1: a loaded machine stays loaded through its batch, so one at a time, once given, is kept.
  state.oneAtATime = state.oneAtATime === true || options.oneAtATime;
  state.commitCheck = state.commitCheck && options.commitCheck;
  // A batch recorded before the prune existed has no field, and prunes.
  state.autoPrune = state.autoPrune !== false && options.autoPrune;
  const branch = state.branches[state.at];

  if (mergeInProgress(root)) {
    requireMain(root);
    const open = git(root, ['rev-parse', 'MERGE_HEAD']).out.trim();
    if (open !== state.tips[branch]) throw new Refusal(`the merge in progress is not ${branch}'s, which is the one recorded`);
    const { blocking, generated } = conflicts(root);
    if (blocking.length) return reportConflict(branch, blocking, generated);
    writeState(root, state);
    return gateMerge(root, state, readPlan(root));
  }

  const landed = git(root, ['merge-base', '--is-ancestor', state.tips[branch], 'HEAD'], { allowFail: true }).status === 0;
  if (state.at === state.branches.length - 1) {
    dropState(root);
    console.log(landed
      ? `merge-branch: the batch is done: ${state.branches.join(', ')} ${state.branches.length === 1 ? 'is' : 'are'} in ${MAIN}.`
      : `merge-branch: the batch is done, but ${branch} is not in ${MAIN}: its merge was abandoned.`);
    // The batch's last branch landed after the last merge's prune ran, so it goes here.
    if (state.autoPrune) pruneAround(root, []);
    return 0;
  }
  preconditions(root);
  if (!landed) {
    throw new Refusal(`the merge of ${branch} is not committed (it is not in ${MAIN}'s history): commit it after reading the diff and writing the records, or --drop-batch`);
  }
  state.at += 1;
  return mergeNext(root, state);
}

function planOnly(root, options) {
  const plan = readPlan(root);
  const lanes = readLanes(root)?.lanes ?? [];
  const reachedAny = new Set();
  console.log(`plan (nothing is merged; the current branch is ${treeState(root).branch || 'a detached HEAD'}):`);
  options.branches.forEach((branch, i) => {
    requireBranch(root, branch);
    const commits = parseCommits(git(root, ['log', COMMIT_FORMAT, `HEAD..${branch}`]).out);
    console.log('');
    console.log(`${i + 1}. ${branch}: ${count(commits.length, 'commit')} ${MAIN} lacks`);
    for (const line of laneReport(root, branch)) console.log(`  ${line}`);
    const problems = options.commitCheck ? commitProblems(root, branch, commits) : [];
    console.log(problems.length ? `  commit check would refuse it:\n${problems.map((problem) => `    ${problem}`).join('\n')}` : '  commit check: nothing to refuse');
    // GATE3: what this branch's paths reach, each gate with why it runs or is skipped.
    const selection = selectGates(plan, branchChanges(root, branch), { lanes, full: options.full, gated: options.gated });
    const runs = selection.gates.filter((entry) => entry.run);
    console.log(`  gates it reaches: ${runs.length} of ${plan.length}${options.full ? ' (--full)' : ''}`);
    for (const line of selectionLines(selection.gates)) console.log(`  ${line}`);
    for (const entry of runs) reachedAny.add(entry.gate.name);
  });
  console.log('');
  if (options.autoPrune) {
    printPrunePlan(root, planPrune(root, { hold: options.branches, atMerge: true }), true);
  } else {
    console.log('prune: skipped (--no-prune)');
  }
  console.log('');
  const order = plan.filter((gate) => reachedAny.has(gate.name));
  console.log(`gates, in order${options.branches.length > 1 ? ' (the rehearsals run after the last branch only, each one any branch reached)' : ''}:`);
  for (const line of planLines(order)) console.log(line);
  const owed = owedLine(plan.length - order.length);
  if (owed) console.log(owed);
  console.log(`logs: ${SCRATCH}/merge-<branch>/<gate>.log`);
  return 0;
}

function dropBatch(root) {
  const state = readState(root);
  dropState(root);
  console.log(state ? `merge-branch: forgot the batch (${state.branches.join(', ')}).` : 'merge-branch: no batch was recorded.');
  if (mergeInProgress(root)) console.log('  A merge is still open in the tree: git merge --abort drops it.');
  return 0;
}

// ---------------------------------------------------------------------------------------------------
// GATEWT1: the full set in its own worktree, beside the merges

/** Whether a process runs with this pid: signal 0 asks without sending, and EPERM is one that runs as someone else. */
export function pidAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error?.code === 'EPERM';
  }
}

/**
 * The lock a full set beside the merges holds (`GATE_TREE_LOCK`, in the main checkout): its pid, the commit it gates,
 * since when, and whether that process still runs. Null when none is held. A lock that does not read is no run's.
 */
export function readGateLock(home, alive = pidAlive) {
  let held;
  try {
    held = JSON.parse(readFileSync(join(home, GATE_TREE_LOCK), 'utf8'));
  } catch (error) {
    if (error?.code === 'ENOENT') return null;
    held = {};
  }
  const pid = Number(held?.pid);
  return { pid, commit: String(held?.commit ?? ''), at: String(held?.at ?? ''), running: alive(pid) };
}

/**
 * The lock taken for `commit`, created only where none is (`wx`), or a refusal naming the run that holds it: one full
 * set beside the merges at a time, since two would load the machine twice over and share one gate tree. A lock whose
 * process no longer runs (its window closed on it) is taken over, and returned as `stale` for the caller to say, with
 * `ended`: the pids `end` (CIPRIO1d, `endOrphans`) ended, the dead pid's direct children started since the lock's `at`
 * with their trees, and (CIPRIO1e, `endStrays`) the processes since then naming the gate tree's folder whose parent no longer
 * runs. Only Windows ends any: elsewhere both are no-ops.
 */
export function takeGateLock(home, { commit, alive = pidAlive, now = nowIso, end = endOrphans, endStrays = endStranded } = {}) {
  const file = join(home, GATE_TREE_LOCK);
  mkdirSync(dirname(file), { recursive: true });
  let stale = null;
  for (let attempt = 0; attempt < 3; attempt += 1) {
    let fd;
    try {
      fd = openSync(file, 'wx');
    } catch (error) {
      if (error?.code !== 'EEXIST') throw error;
      const held = readGateLock(home, alive);
      if (held?.running) {
        throw new Refusal(`a full set beside the merges is running already: on ${held.commit.slice(0, 7)} since ${when(held.at)} (pid ${held.pid}, ${GATE_TREE}); one runs at a time`);
      }
      if (held) {
        // CIPRIO1d: a run killed outright left its steps' tree running; end what its pid started since the lock's `at`.
        const since = Date.parse(held.at);
        // CIPRIO1e: and what its shells' orphans still run, found by the gate tree's folder in their command line.
        const ended = Number.isNaN(since) ? [] : end(held.pid, since);
        if (!Number.isNaN(since)) ended.push(...endStrays(join(home, GATE_TREE), since).filter((pid) => !ended.includes(pid)));
        stale = { pid: held.pid, commit: held.commit, at: held.at, ended };
      }
      rmSync(file, { force: true });
      continue;
    }
    try {
      writeSync(fd, `${JSON.stringify({ pid: process.pid, commit, at: now() })}\n`);
    } finally {
      closeSync(fd);
    }
    return { stale };
  }
  throw new Refusal(`${GATE_TREE_LOCK} could not be taken: another run took it at the same moment`);
}

/** The lock let go, when it is this process's: one taken over since is another run's. */
export function releaseGateLock(home) {
  if (readGateLock(home, () => false)?.pid === process.pid) rmSync(join(home, GATE_TREE_LOCK), { force: true });
}

/** The gate tree's record (`GATE_TREE_RECORD`): the commit its last run gated, when it started and ended, whether it passed, and its stage. */
function readGateRecord(home) {
  try {
    const parsed = JSON.parse(readFileSync(join(home, GATE_TREE_RECORD), 'utf8'));
    return parsed && typeof parsed === 'object' ? parsed : null;
  } catch {
    return null;
  }
}

function writeGateRecord(home, fields) {
  const record = {
    schema: 1,
    _why: 'Written by tools/merge-branch.mjs --full --beside (GATEWT1): the gate tree\'s last run; publish:desktop --from-gated adds its stage, which lets the next merge\'s prune remove the tree.',
    ...fields,
  };
  writeAtomic(join(home, GATE_TREE_RECORD), `${JSON.stringify(record, null, 2)}\n`);
}

/**
 * Whether the prune removes the gate tree, and why it keeps it, from `gateTreeFacts`. Never while its full set runs, at
 * either prune. A merge's prune removes it once its stage is done (the record's stage is of the commit it holds), and
 * `--prune` once its run has ended, staged or not; each says so (`said`). Changes in it are never forced away. A folder
 * git no longer lists is the half-removed rule's (`folderVerdict`).
 */
export function gateTreeVerdict(facts, { atMerge = false } = {}) {
  const { lock } = facts;
  if (lock?.running) {
    return { remove: false, why: `its full set is running (pid ${lock.pid}, since ${when(lock.at)}, on ${lock.commit.slice(0, 7)}): never removed while it runs` };
  }
  if (!facts.listed) return folderVerdict(facts.folder);
  // A full set started there by hand, as before GATEWT1, holds no lock: what runs inside is the only sign of it.
  if (facts.processError) return { remove: false, why: `whether a process runs inside it could not be read (${facts.processError})` };
  if (facts.processes?.length) {
    return { remove: false, why: `a running process is inside it (${facts.processes.map(processSaid).join('; ')}), and no lock says whose run: it is never removed while one runs` };
  }
  if (facts.error) return { remove: false, why: `it is in a state git could not read (${facts.error})` };
  if (facts.changes.length) return { remove: false, why: `it holds changes git removes only with --force (${listed(facts.changes)}), and is never forced here` };
  const sha = (facts.head ?? '').slice(0, 7);
  const leftLock = lock ? `; a lock left by pid ${lock.pid}, which no longer runs, goes with it` : '';
  if (facts.staged && facts.staged.commit === facts.head) {
    return { remove: true, why: '', said: `the gate tree at ${sha}: its stage is done (${when(facts.staged.at)})${leftLock}` };
  }
  if (atMerge) {
    return { remove: false, why: `it holds ${sha}, and its stage has not read it: npm run publish:desktop -- --to <install> --service --stage --from-gated ${sha}; --prune removes it` };
  }
  return { remove: true, why: '', said: `the gate tree at ${sha}, never staged${leftLock}` };
}

/** Where a folder's install notes the lockfile it installed from: inside `node_modules`, which `npm ci` lays anew. */
const DEPENDENCY_STAMP = '.gate-tree-lockfile';
const fileHash = (file) => createHash('sha256').update(readFileSync(file)).digest('hex');

/**
 * The folders of `DEPENDENCY_FOLDERS` with a `package-lock.json` whose dependencies are missing or were installed from
 * another lockfile than the one there now: a gate tree moved to a commit that changed one installs again.
 */
export function staleDependencies(tree) {
  return DEPENDENCY_FOLDERS.filter((folder) => {
    const lockfile = join(tree, folder, 'package-lock.json');
    if (!existsSync(lockfile)) return false;
    try {
      return readFileSync(join(tree, folder, 'node_modules', DEPENDENCY_STAMP), 'utf8').trim() !== fileHash(lockfile);
    } catch {
      return true;
    }
  });
}

/** The note that a folder's dependencies were installed from its lockfile as it is now. */
export function stampDependencies(tree, folder) {
  const stamp = join(tree, folder, 'node_modules', DEPENDENCY_STAMP);
  mkdirSync(dirname(stamp), { recursive: true });
  writeAtomic(stamp, `${fileHash(join(tree, folder, 'package-lock.json'))}\n`);
}

/** Each folder's dependencies the gate tree lacks, installed from its lockfile (`npm ci`, as AGENTS.md has a fresh checkout do). */
function installDependencies(tree) {
  for (const folder of staleDependencies(tree)) {
    console.log(`the gate tree: installing ${folder} from its lockfile (npm ci)…`);
    const ran = spawnSync('npm ci --no-audit --no-fund', { cwd: join(tree, folder), shell: true, stdio: ['ignore', 'inherit', 'inherit'], windowsHide: true });
    if (ran.error || ran.status !== 0) {
      throw new Refusal(`npm ci failed in the gate tree's ${folder} (${ran.error?.message ?? `exit ${ran.status}`}), so the full set did not start`);
    }
    stampDependencies(tree, folder);
    console.log(`the gate tree: installed ${folder} from its lockfile (npm ci)`);
  }
}

/**
 * The gate tree at `commit`: made (`git worktree add --detach`) where none stands, reused where it holds it, moved there
 * (`git checkout --detach`) where it holds another. One with changes, or a folder git does not list, is refused, naming
 * them: the full set gates a commit, and nothing here is forced.
 */
function prepareGateTree(home, commit) {
  const path = join(home, GATE_TREE);
  const sha = commit.slice(0, 7);
  const tree = parseWorktrees(git(home, ['worktree', 'list', '--porcelain']).out).find((one) => samePath(one.path, path));
  if (!tree) {
    if (existsSync(path)) {
      throw new Refusal(`${GATE_TREE} is a folder git does not list as a worktree: node tools/merge-branch.mjs --prune removes what a removal left, or look at it`);
    }
    git(home, ['worktree', 'add', '--detach', path, commit]);
    console.log(`the gate tree: made at ${sha} (${GATE_TREE})`);
    return path;
  }
  const changes = lines(git(path, ['--no-optional-locks', 'status', '--porcelain']).out);
  if (changes.length) {
    throw new Refusal(`the gate tree (${GATE_TREE}) holds changes, which the full set would gate as part of a tree no commit holds:\n`
      + `${changes.slice(0, 12).map((line) => `  ${line}`).join('\n')}${changes.length > 12 ? `\n  … ${changes.length - 12} more` : ''}\n`
      + '  Look at them; once it is clean, --prune removes it or --full --beside moves it');
  }
  const head = git(path, ['rev-parse', 'HEAD']).out.trim();
  if (head === commit) {
    console.log(`the gate tree: reused at ${sha} (${GATE_TREE})`);
    return path;
  }
  git(path, ['checkout', '--quiet', '--detach', commit]);
  console.log(`the gate tree: moved from ${head.slice(0, 7)} to ${sha} (${GATE_TREE})`);
  return path;
}

/**
 * The private files a gate reads under `local/`, as the declaration names them (GATEWT1b): today the sensitive scan's
 * patterns file (`sensitive.patternsFile`, the devkit's default where undeclared). A path not under `local/` is not one
 * of these (a tracked file is in the tree already), and none may climb out of it.
 */
export function privateGateFiles(home) {
  let declared = 'local/sensitive-patterns.txt';
  try {
    const parsed = JSON.parse(readFileSync(join(home, 'daoris.gates.json'), 'utf8'));
    if (typeof parsed?.sensitive?.patternsFile === 'string') declared = parsed.sensitive.patternsFile;
  } catch {
    // No readable declaration: the plan's own reading refuses it, with its reason.
  }
  const path = declared.replace(/\\/g, '/');
  return path.startsWith('local/') && !path.split('/').includes('..') ? [path] : [];
}

/** Copies each of `files` from `home` into `tree`, refreshed on every run; returns the lines to say (GATEWT1b). */
export function copyPrivateFiles(home, tree, files) {
  return files.map((file) => {
    const from = join(home, file);
    if (!existsSync(from)) return `${file} is missing in the main checkout, so it is not copied into the gate tree: the gate that reads it answers for itself`;
    mkdirSync(dirname(join(tree, file)), { recursive: true });
    writeAtomic(join(tree, file), readFileSync(from));
    return `${file}: copied into the gate tree`;
  });
}

/** Copies the run folder at `from` over `home`'s `local/scratch/beside-<sha7>`, the newest run's logs only. */
function replaceKept(from, home, sha7) {
  const to = join(home, SCRATCH, `beside-${sha7}`);
  rmSync(to, { recursive: true, force: true });
  cpSync(from, to, { recursive: true });
}

/**
 * Keeps the tree's run logs for `sha` in `home`'s `local/scratch/beside-<sha7>` (GATEWT1c): a prune after the stage
 * removes the tree, and the logs of a failed run were in it. Replaces an older run's at that commit; true when kept.
 */
export function keepGateLogs(tree, home, sha) {
  const from = join(tree, SCRATCH, `full-${sha.slice(0, 7)}`);
  if (!existsSync(from)) return false;
  replaceKept(from, home, sha.slice(0, 7));
  return true;
}

/**
 * Keeps any run folder a hard-killed run left in the tree (its `finally` never ran), before the next run clears it:
 * copied to `beside-<sha7>` where that is absent in `home`; returns the lines to say (GATEWT1c).
 */
export function rescueGateLogs(tree, home) {
  const scratch = join(tree, SCRATCH);
  if (!existsSync(scratch)) return [];
  const said = [];
  for (const name of readdirSync(scratch)) {
    const found = /^full-([0-9a-f]{7,40})$/.exec(name);
    if (!found || existsSync(join(home, SCRATCH, `beside-${found[1].slice(0, 7)}`))) continue;
    replaceKept(join(scratch, name), home, found[1].slice(0, 7));
    said.push(`the logs of a run that did not end are kept: ${SCRATCH}/beside-${found[1].slice(0, 7)}`);
  }
  return said;
}

/** What a full set beside the merges says before its gates start: it shares the machine with them. */
const UNDER_LOAD = 'under load: a full set beside the merges shares the machine with their gates, so a Process half\'s timing tests run '
  + 'loaded; its failures are still re-run alone once its lane is quiet, and a pass alone still reads FLAKE (GATEORDER1, FLAKE1)';

/**
 * A full set beside the merges is a guest on the machine (CIPRIO1, D180's budget). It lowers its own process to below
 * normal: on Windows a process created by a below-normal one starts in that class, so every gate's shell, npm, dotnet,
 * test host and git runs below normal with no race, and the run ends with the process, so nothing is restored. Below
 * normal, not idle: idle starves behind a busy machine and a Process half's timing tests would flake. I/O priority is
 * left out (Node has no API for it, and whether children inherit background mode is unsettled; CIPRIO1b measures).
 * Returns what went wrong, or null.
 */
export function lowerPriority(setPriority = osSetPriority) {
  try {
    setPriority(0, osConstants.priority.PRIORITY_BELOW_NORMAL);
    return null;
  } catch (error) {
    return error.message;
  }
}

/**
 * The gates beside get build servers of their own: none reused after the run (`MSBUILDDISABLENODEREUSE`), no MSBuild
 * server and no shared compiler server, so a lowered run leaves nothing to serve the owner's normal builds and borrows
 * none of theirs. The CI never runs `dotnet build-server shutdown`: it would stop the owner's servers too.
 */
export const BESIDE_ENVIRONMENT = Object.freeze({ MSBUILDDISABLENODEREUSE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0', UseSharedCompilation: 'false' });

/** The start of a run beside, once its lock is taken and before anything is built: lower the priority, set the environment. */
export function startBesideRun({ setPriority = osSetPriority, env = process.env, say = console.log } = {}) {
  const failed = lowerPriority(setPriority);
  if (failed) say(`the run could not lower its priority (${failed}); it goes on at the priority it has`);
  Object.assign(env, BESIDE_ENVIRONMENT);
  raiser = failed ? null : raiseTo(setPriority, say);
}

/**
 * The Process lane's bounds are wall-clock and starved below normal (FAMILYFAIL1b, D180: the rehearsals; FAMILYFAIL1c: the
 * Process halves and each test re-run alone too, 59 of whose timing tests failed beside five workers): its step is raised
 * to normal and what it starts inherits that. A failure to raise is said once and the step goes on.
 */
function raiseTo(setPriority, say) {
  let told = false;
  return (pid) => {
    try {
      setPriority(pid, osConstants.priority.PRIORITY_NORMAL);
    } catch (error) {
      if (!told) say(`the run could not raise a rehearsal to normal priority (${error.message}); it goes on lowered`);
      told = true;
    }
  };
}

let raiser = null;

/** The raise a run beside hands a Process-lane step (a rehearsal's, a Process half's, a test re-run alone); null in any run that did not lower itself. */
export const rehearsalRaise = () => raiser;

/**
 * CIPRIO1c: how often a run of main's tip reads the tip while its gates run. A gate on an old tip runs this long at most
 * after main moves, against a Process half's or a rehearsal's hour; each read is one `git rev-parse` at the run's lowered
 * priority, tens of milliseconds every five seconds.
 */
export const TIP_POLL_MS = 5_000;

/**
 * A run of main's tip (`commit`; null for a run given a named commit, which is never superseded) asks, before each gate,
 * whether main has moved on, and `poll(ms)` asks every `ms` while the gates run (CIPRIO1c), answering its stop; the timer
 * keeps no process alive. `resolveTip` is the seam: main's tip now, null when it cannot be read (not a newer one).
 * Once moved, the run is `superseded` and stays so, and `onMove` hears it once: a run beside ends its trees there.
 */
export function watchTip(commit, resolveTip = () => null, { onMove = () => {} } = {}) {
  const watch = {
    superseded: null,
    check() {
      if (watch.superseded) return true;
      if (!commit) return false;
      const tip = resolveTip();
      if (tip && tip !== commit) {
        watch.superseded = { from: commit, to: tip };
        onMove(watch.superseded);
      }
      return watch.superseded !== null;
    },
    poll(ms) {
      if (!commit) return () => {};
      const timer = setInterval(() => {
        // A throw here would end the run uncaught and leave its gates running; a tip not read is not a newer one.
        try {
          watch.check();
        } catch {
          // Asked again at the next tick.
        }
      }, ms);
      timer.unref();
      return () => clearInterval(timer);
    },
  };
  return watch;
}

/** CIPRIO1c: which gates ran to their verdict, which the run ended running, which it never started. */
const endLines = (head, ran, left, ended) => [
  head,
  `  ran: ${ran.join(', ') || 'none'}`,
  `  ended: ${ended.join(', ') || 'none'}`,
  `  left: ${left.join(', ') || 'none'}`,
];

/** What a superseded run says: that it ended what it ran and started nothing more, which gates ran, were ended and were left. */
export function supersededLines(superseded, ran, left, ended = []) {
  const moved = `main moved from ${superseded.from.slice(0, 7)} to ${superseded.to.slice(0, 7)}`;
  return endLines(`superseded: ${moved}, so this run ended the gates it was running and started no more; it is not a pass`, ran, left, ended);
}

/** CIPRIO1c: what a run a person stopped says, as a superseded one does. */
export function stoppedLines(ran, left, ended = []) {
  return endLines('stopped: a person stopped this run, so it ended the gates it was running and started no more; it is not a pass', ran, left, ended);
}

/**
 * CIPRIO1c: a person's stop (Ctrl+C) ends the run's trees and lets the run wind down as a superseded one does: its logs
 * kept, its record written, its lock let go. Heard once, so a second Ctrl+C is the default exit, for a run that hangs.
 * Answers the unhooking, for a run that ends without one. A run killed outright hears nothing (CIPRIO1d).
 */
export function stopOnInterrupt(trees, { on = process, say = console.log } = {}) {
  const stop = () => {
    say('stopped: ending the gates running, with everything they started; Ctrl+C again exits at once');
    trees.end('a person stopped the run');
  };
  on.once('SIGINT', stop);
  return () => on.off('SIGINT', stop);
}

/** The gate tree record's verdict on a run: a superseded run is never passed, whatever its gates said. */
export function besideOutcome(code, superseded) {
  return { passed: code === 0 && !superseded, superseded: superseded ? superseded.to : null };
}

/**
 * `--full --beside [<commit>]` (GATEWT1): the full set on `commit`, main's tip unless named, in the gate tree, a worktree
 * of its own, so merges land in the main checkout meanwhile. One at a time (`takeGateLock`). The tree is made or reused,
 * its dependencies installed when missing or stale, and the whole plan run there by `gateCheckout`, each verdict in the
 * main checkout's store (`verdictsHome`). The record says when it ended and whether it passed; the stage it names builds
 * from the tree, never from the main checkout.
 */
async function gateBeside(root, options) {
  const home = verdictsHome(root);
  if (!isIgnored(home, GATE_TREE)) {
    throw new Refusal(`${GATE_TREE} is not ignored in the main checkout, so the gate tree would be part of the tree its merges gate`);
  }
  requireVerdictsIgnored(home);
  const asked = options.besideCommit ?? MAIN;
  const resolved = git(home, ['rev-parse', '--verify', '--quiet', `${options.besideCommit ?? `refs/heads/${MAIN}`}^{commit}`], { allowFail: true });
  if (resolved.status !== 0) throw new Refusal(`no commit '${asked}' to gate`);
  const commit = resolved.out.trim();
  const sha = commit.slice(0, 7);
  const { stale } = takeGateLock(home, { commit });
  // CIPRIO1c: every step the run starts is held here, so a newer tip or a person's stop ends the gates running.
  const trees = stepTrees();
  const unhook = stopOnInterrupt(trees);
  try {
    if (stale) console.log(`the gate tree's lock was left by pid ${stale.pid} (on ${stale.commit.slice(0, 7)} since ${when(stale.at)}), which no longer runs: taken over${stale.ended.length ? `; ended what it left running (pid ${stale.ended.join(', ')})` : ''}`);
    // CIPRIO1: before the tree is made or anything installed or built, so every descendant inherits the lowered class.
    startBesideRun();
    const started = nowIso();
    writeGateRecord(home, { commit, started, ended: null, passed: null, staged: null });
    const tree = locate(prepareGateTree(home, commit));
    installDependencies(tree);
    for (const line of copyPrivateFiles(home, tree, privateGateFiles(home))) console.log(`  ${line}`);
    for (const line of rescueGateLogs(tree, home)) console.log(`  ${line}`);
    console.log(UNDER_LOAD);
    let code;
    // Only a run of main's tip is superseded by a newer tip; one given a commit gates that commit whatever main does.
    // CIPRIO1c: the move ends the gates running at once, each with its tree, rather than before the next.
    const tip = () => git(home, ['rev-parse', '--verify', '--quiet', `refs/heads/${MAIN}^{commit}`], { allowFail: true }).out.trim() || null;
    const watch = watchTip(options.besideCommit ? null : commit, tip, {
      onMove: (moved) => trees.end(`main moved from ${moved.from.slice(0, 7)} to ${moved.to.slice(0, 7)}`),
    });
    try {
      code = await gateCheckout(tree, options, watch, trees);
    } finally {
      // Pass, fail or a thrown error: the tree may be pruned next, and its logs go with it.
      try {
        if (keepGateLogs(tree, home, commit)) console.log(`  The run's logs are kept: ${SCRATCH}/beside-${sha}`);
      } catch (error) {
        console.log(`  The run's logs could not be kept (${error.message}); they stay in the gate tree`);
      }
    }
    const outcome = besideOutcome(code, watch.superseded);
    writeGateRecord(home, { commit, started, ended: nowIso(), passed: outcome.passed, ...(outcome.superseded ? { superseded: outcome.superseded } : {}), staged: null });
    if (watch.superseded) {
      console.log(`  Run it again on the new tip: node tools/merge-branch.mjs --full --beside; the gate tree stays at ${sha} until then (${GATE_TREE}).`);
    } else if (code === 0) {
      console.log(`  Stage it from the gate tree, never from the main checkout as it moves: npm run publish:desktop -- --to <install> --service --stage --from-gated ${sha}`);
    } else {
      console.log(`  The gate tree stays at ${sha} (${GATE_TREE}): run that there, or --full --beside ${sha} again; --prune removes it.`);
    }
    return code;
  } finally {
    unhook();
    releaseGateLock(home);
  }
}

export async function main(argv) {
  const options = parseArgs(argv);
  const root = locate(process.cwd());
  if (options.dropBatch) return dropBatch(root);
  if (options.prune) return pruneOnly(root, options);
  if (options.plan) return planOnly(root, options);
  if (options.passed) return passedOnly(root);
  // GATE6b: with no merge open, --rerun and --stale act on the checkout as it stands.
  if (options.stale) {
    if (mergeInProgress(root)) throw new Refusal('a merge is open here: --rerun <gate>… re-gates it, and runs each gate a fixed path reaches; or git merge --abort');
    return rerunCheckout(root, options);
  }
  if (options.rerun.length) return mergeInProgress(root) ? rerunGates(root, options) : rerunCheckout(root, options);
  if (options.resume) return resume(root, options);
  if (options.beside) return gateBeside(root, options);
  if (options.full && options.branches.length === 0) return gateCheckout(root, options);
  return start(root, options);
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then(
    (code) => { process.exitCode = code; },
    (error) => {
      if (error instanceof Refusal) {
        console.error(`merge-branch: ${error.message}`);
        if (error.usage) console.error(USAGE);
        process.exitCode = error.exitCode;
      } else {
        console.error(error?.stack ?? String(error));
        process.exitCode = 2;
      }
    },
  );
}
