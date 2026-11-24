# Developing Daoris

Read [AGENTS.md](../AGENTS.md), the [document router](README.md), the
orientation index (`docs/index/README.md`, written in each checkout) and the relevant contract before exploring code, and make
every change through the `change` skill; `.claude/INDEX.md` lists the on-demand knowledge. [TASKS.md](../TASKS.md)
holds acceptance criteria and dependencies; [ROADMAP.md](../ROADMAP.md) gives the sequence.

## Prepare the workspace

Use Node 24 for development and the .NET 10 SDK for the service, devkit and desktop. The packaged
CLI supports Node ≥22 because packaging compiles TypeScript. Run commands from the workspace root:

```sh
npm ci
npm --prefix src/Daoris.Web ci
```

The root `npm ci` also configures the router's merge driver in this clone's own git configuration (its `prepare`
runs `node tools/merge-rows.mjs --install`; ROUTERMERGE1). A clone whose dependencies came another way runs that command
once; worktrees share it. Without it git merges `docs/README.md` as plain text, which conflicts on two neighbouring rows
rather than merging them.

The service and the driver build against Lyntai's released pins, or against a local build of its main line (D22's note, D248). With
the Lyntai checkout beside this one (or named by the argument or `DAORIS_LYNTAI_PATH`), check out its `master` and run
`node tools/lyntai-local.mjs [path]`: it packs the tip into the ignored `local/lyntai-feed/` and writes `local/lyntai-local.props`,
and reruns do nothing while the tip is packed. The service's, the desktop's and the contracts generator's package props import that
file (the generator restores the desktop modules' graph, so it restores the same packs); delete it to restore the released pins. The merge tool copies it into its gate trees; copy
the main checkout's into a worktree that builds the layer. Lyntai's agent layer is only in the local build, so the driver references it, and
compiles `Agents/Layer/`, only while the file is there (the driver project's Lyntai-layer condition, set by that file); without it the driver builds as it did before the
layer, which is what the CI builds. A change to the layer's reference is built both ways: with the file, then with it moved
aside and the projects restored again.

The desktop builds against Shenora's released pin (`DaorisShenoraReleased`) or against a local build of its main line (D22's note of
2026-11-08), the same way: with the Shenora checkout beside this one (or named by the argument or `DAORIS_SHENORA_PATH`) on its `main`,
`node tools/shenora-local.mjs [path]` packs `Shenora`, `Shenora.Windows` and `Shenora.Chromium` at one `<released>-local.<sha7>` into
the ignored `local/shenora-feed/` and writes `local/shenora-local.props`, which the desktop's and the contracts generator's package props import; delete it to
restore the pin. Both local-build tools write the feed and the props under the main checkout's `local/` even when run from a
worktree (and a copy of the props into the worktree); the merge tool and the trees' rule file copy the props into gate trees and
sessions. A fix that belongs to the desktop kit is made in Shenora by a quest, never patched over here.

The web package has its own lockfile and is not an npm workspace of the root. Windows is needed
for the desktop application and deployment rehearsal; native AOT publishing needs the target
platform's native toolchain. The individual [component guides](README.md#guides) explain their loops.

## Repository scratch cleanup

The owner chose repository-scoped Codex defaults in [`.codex/config.toml`](../.codex/config.toml).
They match the full-access, no-prompt session mode; trusted project configuration loads above user
defaults, subject to session overrides and managed requirements. These defaults do not override a
tool's refusal or authorize unrelated destructive actions. See [official configuration precedence](https://learn.chatgpt.com/docs/config-file/config-basic#configuration-precedence).

When cleanup is part of an authorized task, remove its own generated scratch using the normal
native filesystem command. An existing grant for the exact targets needs no repeated confirmation.
Immediately before removal, resolve each target inside this checkout, confirm it is ignored and
contains only disposable generated material, and reject reparse points. Preserve authored content,
backups and evidence still needed by the task.

Agent worktrees and what they leave go by the merge tool's prune, which every merge runs and
`node tools/merge-branch.mjs --prune [--plan]` runs alone; its rules are the tool header's *The prune*. A
merged worktree with no change and nothing under `local/` but logs, scratch, a quick run's base tree (`local/quick-base-tree`, which a killed run leaves) and the copies the worktree setup made is removed, the agent
harness's lock lifted when no running process names its folder and nothing in it was written for two
hours (a worker's shell shows on no command line). A folder under `.claude/worktrees/`
that `git worktree list` does not list and that holds no `.git` is a half-removed worktree, removed
without following a reparse point. A process holding either is named with its pid, never stopped.
Other registered worktrees use their owning lifecycle tool. Every prune also reports each builder worktree whose branch main does not
hold (an agent that ended without landing), oldest first with its last commit, which the steward lands or removes and the prune never
touches, and removes the main checkout's `_fixtures/` entries by the rule below.

A worktree with no commit of its own counts as merged. So the prune removes it, gitignored
`local/scratch/` with it, even while a worker still uses it, and the agent harness removes a worker's
isolated worktree when it ends without a tracked change. A docs-only or read-only dispatch whose product
is scratch (a brief, a proposal, a read's answer) writes it under the main checkout's
`local/scratch/`, or commits it early; otherwise it is lost with the worktree.

No test or tool leaves a folder in the system's temporary folder (the plugin examples' tests too: `scratchRoot()` of the same file): a node test makes its scratch with `scratchFolder` (removed when
its process ends) or `fixtureFolder` and names a path under `FIXTURE_HOME` (`tools/merge/fixture-home.ts`, in `_fixtures/tool-tests/`),
a C# test roots at `TestHomes`. `src/Daoris.Cli/tool-test/temp-folder-census.test.ts` fails, naming file and line, for a
`tmpdir()` or `env.TEMP` in any node source of the repository (`inNodeScope`: all but `node_modules` and `_fixtures`) or a `GetTempPath()` in the C# test projects unless the file is declared with why (the node
list is `KEPT` there; the C# list is `TestHomesCensusTests`'s, read by it), and a declared file removes what it makes.

A node test that starts real git with the commit and checkout hooks installed copies its repository from a seed built once for each
shape (`tools/hook-seeds.mjs`, as the C# tests do with `GitSeed`) and turns git's maintenance off in it (`maintenance.auto`), since on
this machine a case's `git init`, base commit and install, and the maintenance a commit starts, were most of its time (REACHCOST1).

A checkout's `_fixtures/` keeps by one rule, `node tools/fixtures-keep.mjs`, which every prune applies to the main checkout
(`sweepFixtures`) and which run alone is a plan (`--apply` removes, `--root <checkout>`
names another tree, `--hours <n>` the window): an entry it or anything directly in it was written in the last 24 hours stays, as
do the caches a run keeps (`pinned-mingit`, `rehearsal-builds`, `seeds`) and every link, which is never followed; the rest is
what a killed host, a failed rehearsal or a retired probe left, and goes. The tool refuses a folder git does not ignore and a
target outside it, and leaves an entry something still holds for a later run.

Use literal targets and one shell throughout; verify absence afterward. If execution is rejected,
record the action and exact reason, leave the task open, and resolve the actual permission setting
or surface it to the owner. Retry after new authorization or a real permission change through the
same review path. Do not substitute another deletion API, wrapper or shell to evade that rejection.

## Choose the checks

`npm run verify` is the baseline check, not a substitute for testing a component you changed. Its
exact steps are in `package.json`; the component gates are in `daoris.gates.json`. Run focused checks
during the task. Before done, run `node tools/reached-checks.mjs --base main --tier fast` and
`npm run verify:checks`, and fix what your change reddens: they are the commands `daoris.gates.json` marks `"handback"`, every brief
names them, and a workflow's `check` step verifies them at your tip while you are open: the fast tier, saying the classes it replayed
and the heavy-tier classes it leaves owed to the run after the landing, which runs them on main's new tip; the integration boundary's
full set runs every tier. Each group's passes are kept as it ends, so a check asked again at a new commit, a merge of main included,
replays every class whose inputs that commit left unchanged. `npm run quick` is a builder's
set before a hand-back: the index and the page's generated types written, then every check of the merge's `cli` gate
(`npm run verify:checks`, with the universal gates on the built-in patterns, and the layers' census's source counts) but the time
ratchet, so a comment, partial or sensitive finding is read in the builder's tree and the work is merged as handed back.

This workspace verifies a task in under ten minutes (D349: its own rule, not the product's and not canon; `daoris.gates.json`'s
`verifyBudget`, `.claude/knowledge/verification-budget.md`). One task's verification, a builder's checks, a run's check or a
merge's gates for one branch, is scoped to what its paths reach: the light gates on its paths and the reached classes before the
landing, the heavy tier after it in the background. A full run needs a reason the tools check against the change: a declared wide
path (the list the lane table's every-gate rule reads, its one source, apart from the budget), a reach of half or more of what
the run could leave out (the gates the lane table can skip at a merge, not those every merge runs or no rule names; the census's
classes in `reached-checks`), the run after a landing, or a release. The merge, `--plan`, `--preview` and `reached-checks` say
the scope first, by what runs (`verification: scoped to its reach: …`, naming a full run's reason the change bears and does not
take, or `verification: full: daoris.gates.json is a declared wide path`); `<branch> --full` is refused where the batch's changes
bear no reason, judged as one batch by the merge and `--plan` alike, and `--continue --full` during an open merge is judged on
what that merge changes, a fix made in it included; the refusal names the reasons it accepts, and `--full` on the checkout is a
release's run by its kind. A merge past the budget says so in its summary with each gate's time and the reached run's steps,
files a line in `local/verify-overruns.jsonl` for the steward (or says it could not), and keeps `verification` (budget, wall
time, the scope its change calls for, a full run's reason, and what ran) in its merge record; `tools/merge/verification.ts`
holds the rule until a supervisor actor does (WORKSPACERULES1). A repository whose gates file sets no `verifyBudget` has no such
rule.

The `cli` gate, `npm run verify:checks`, is `tools/verify-checks.mjs`: the steps of `tools/verify-checks/steps.mjs`, the index's
writer first and alone, the rest side by side (quick runs the same list through the same runner). Each step declares what it
reads, and `node tools/test-map.mjs --check` holds each declaration to what the step's code reads (its imports, the paths its
strings name, a tsconfig's files and theirs); a step whose tool lists the tree by a filter names that listing in its `reads`. A
merge in place, a preflight and the queue's scoped run hand every gate their changed paths (`MERGE_BRANCH_CHANGED`,
`tools/merge/gate-hands.ts`), and the gate then runs only the steps a changed path reaches, the devkit's universal step standing
aside since the `universal` gate runs it in every merge with the private patterns; a docs-only merge runs twelve of its
twenty-six steps. The layers' census's source counts (`tools/layers-census/counts.mjs`, about 12 s) are one, run where a `.cs` or
`.csproj` file, the lanes or the census's list changes, so a merge sees the allowance or boundary its sources move (MAINRED1). Handed no paths (a developer's run, `--full`, the watch after a landing, the release workflow) it runs every
step. `node tools/verify-checks.mjs --plan` says what it would run and why each other step would not. A new check is a step of
that list with its reads, never a link of a chain. Quick judges no test time; the merge's gates and the full set judge it, on classes measured again alone. Last,
committed and merged with main, a builder runs `node tools/merge-branch.mjs --preflight` in its worktree: its merge's fast plan
(the queue's own choice, on the paths from the merge base, of which it runs the fast gates: never a Process or git half, a
rehearsal, a browser spec or the whole `tool-e2e`, a known red's half included, even where the branch changes `daoris.gates.json`;
each is named deferred to the CI run on main, while its `reached` gate runs the three end-to-end specs nearest the change), main and
the queue untouched, each verdict kept where the merge of the same tree keeps it.
It runs within its bound, 25 minutes unless `--bound <minutes>` says otherwise: a gate waiting for the machine's heavy-run
admission says so as it starts to wait, with who holds the places, and again each minute; the preflight says each minute what
runs and what waits; at the bound it ends what it started, starts nothing more, and says which gates passed, which failed, which
it stopped and which never started, and why. A red writes `local/scratch/merge-<branch>/brief.md`: each failing test or first
error, its file and line, the first error lines, and whether main fails it too (a log's failing test is its first `not ok` or `✖` line, and its
error never a pass line, a count of none or a warning; a log with none says the run ended without a failing test, with its exit), from that selection run on main's tip in the
worktree's `local/scratch/main-tree`. The branch-worker hands back on its summary: its pass, its brief, or what its bound left.

Every time breach says why (`tools/time-why.mjs`): a test over its time ratchet limit, a unit test over the layers census's second,
a test the hang timeout killed and a gate killed at its budget are each one line of the merge's breach report, `breaches.json`
beside its gate logs (`local/scratch/merge-<branch>/`, a queue run's `scope-<sha7>/`), carried as `breaches` and `separations` in
the merge record and the queue's scope-run record and printed in the summary. Each names the time, the limit, the cause (`process`,
`disk`, `network`, `clock`, `scan`, `load`, or `unclassed` where nothing reads), what the cause was read from and the row that owns
the fix (UNITPURE1 for a unit test that is not pure, LAYERS1 f or COMPONENT1 for a real-process one, CENSUSDIFF1 for a scan,
E2EFAST1, REACHCOST1 and CLIREACH1 for their gates). No test host records yet what each test started, opened or waited on, so a
test's cause is its category (the Process trait, a Process or git half's record, an end-to-end spec) and its class's source, read
by the layers census's counters; a gate's is its longest step clock. Three or more classes of one domain over by one cause name
it a separation candidate. A gate past its budget while the load sampler saw more heavy runs than its own (the admission's
places and other trees' test runs) is never killed for the load (MERGENOKILL1, D346): it runs on to its own end, its verdict
its own, and its time is a `load` breach that fails nothing; only three times its budget ends it, as a runaway, and that
fails. A gate past its budget alone is killed and fails as its time; the budget itself never stretches with the load. The hang
bound is read differently, since its kill leaves the gate with no verdict: a .NET gate whose blame collector killed a test while
the sampler saw other runs is classed `load` (its hang breach fails nothing), and the gate runs again once, whole and alone,
after every other gate has ended and once the machine is free (read every 15 s, for up to 10 min); a hang in that run again
fails as the test's own. The bound is not raised under load: it is one fixed setting in `Directory.Build.props`, a longer one
would make every real hang cost more, and a rerun costs only when a kill happened. A gate that writes its own trx records into `_fixtures/test-timing` (the `censuses` gate, the service, devkit, driver
and modules halves) is sampled from its start and its records are marked with the peak (`stampLoad`, as `reached` marks its
own), so the time ratchet judges only timings taken alone. At a merge the ratchet fails only on what the branch made slow
(RATCHETOWN1, D346): a test over its limit whose file the merge adds or changes, or one over half slower than main's alone
record of it (`local/time-main.json`, `tools/time-main.mjs`: each judging run keeps each test's alone times with the commit it
ran on, and main's record is the slowest of the newest five whose commit is in main's history). Any other breach is inherited
from main: a known red, said in the summary with its cause and row, failing nothing.

| Change | Additional verification |
|---|---|
| Driver, modules, service, page or tool classes | `node tools/reached-checks.mjs --base main` in the worktree runs the classes the branch's changes reach (`--plan` says which, `--classes <name>[,<name>]` runs named ones) and records each pass under the key the merge computes, so the merge replays what you proved on the same inputs; a class that fails is run again alone once and a pass alone is said as a flake; a heavy group (Process tier, a rehearsal, a whole suite) also waits its turn in the machine's admission, the one the merge's gates share. A raw `dotnet test` of a Process class takes no place and is what the merge refuses to replay |
| CLI/package/install story | `npm run rehearse` exercises the packed consumer artifact |
| Service, driver, sync, toolchain or canon shape | `dotnet test src/Daoris.Service`, driver/modules checks as reached, and `npm run rehearse:family` |
| Devkit | `dotnet test src/Daoris.Devkit` |
| Web UI | `npm run test:web`: unit tests, production build, HTTP build and Playwright; stories and installed-window proof where the contract calls for them |
| Desktop/modules | Fast suites below; `npm run rehearse:deploy` at the full integration boundary |
| Canon | Sync root and both examples in the same change, then doctrine checks and family rehearsal |
| Documentation/tooling | `npm run verify` and relevant tooling tests; manually check behavioral prose against source |

The desktop fast loops are:

```sh
dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process
dotnet test src/Daoris.Desktop/Daoris.Desktop.Modules.Tests --filter Category!=Process
```

The driver's tests project also builds the launcher (a build-only reference, LAUNCHGATE1), which compiles driver files in
by link, so a driver file naming a type the launcher lacks fails this gate rather than the publish.

Real-process suites use `src/Daoris.Desktop/process.runsettings` and may take substantial time. The driver's
runs eight classes at once, and a class that sets the process's environment, measures the whole process or
holds a bound of a few seconds on a real process runs alone after the rest, in the `Alone` collection
(PROCPAR1); the modules' runs one class at a time, as its assembly says. The driver's test host, as it loads, takes out
the Daoris and git variables its shell held (git's locating, door `GIT_SSH*`, prompt, trace and identity ones, `GCM_*`: what a Daoris session's
shell carries, SESSIONTESTENV1), as the gates' environment does, and names a scratch home of its run's
(`HostEnvironment`), so a run in a person's shell is the gate's run and never reaches the install's home; no other source
sets the process's environment but those `ProcessEnvironmentCensusTests` names, each run alone. The driver's git-only Process
classes, marked `Parallel=Git` beside `Category=Process`, run four at once in one host with
`src/Daoris.Desktop/git.process.runsettings` (gate `driver-process-git`, PROC1c): four at once is load, so
that half runs after the other one, never beside it, and a class marked so is run alone with that file, not
the other, whose filter leaves it out. The git library's classes run once more on the MinGit the toolchain pins, in `driver-process-git-pinned`
(`src/Daoris.Desktop/git.pinned.process.runsettings`, which names those classes and sets `DAORIS_PINNED_GIT_LEG`). A git test copies its
repositories from a seed (`GitSeed`, PROC1h; the plain one is `GitSeed.PlainAsync`) unless it reads the first commit's id or
time, and removes its scratch with `GitSeed.Remove`: git writes its objects read-only, which `Directory.Delete` refuses on
Windows (PROCSPEED1). A test whose subject is not git leaves the product's git starts to the process seam (`ProcessStarts.Within(_ => null)`) rather than let a plain scratch folder under the checkout have git answer for the whole checkout around it (SESSIONTESTENV2). A seed's repositories start no maintenance git behind a commit, a fetch or a push
(`GitSeed.NoMaintenanceAsync`), and a platform's moves on a bare origin are git's plumbing in the origin, never a clone that
pushes (`LandedFixture.SquashOnPlatformAsync`, `CommitOnOriginAsync`): on this machine a git start costs about half a second,
and the git half is its starts (PROCSPEED1b). A real-process test that shows a loop or a shell doing nothing more waits on
what it would act through, never seconds of the clock: the loop's own looks with no start reserved
(`DrivenMoveHarness.QuietLooks`), or every coming-up shell's work in flight (`LiveLimitHarness.QuietAsync`); and a bound
the product waits on a stand-in that never answers is the product's seam, shortened by the test (`ChatHandOver.WireWait`,
TESTSPEED1). Rehearsals also run
serially, use repository scratch and record transcripts there; the merge tool runs the release, family and deployment
rehearsals beside the git half, one after the other, each building what the Process halves build only into an output of
its own (REHLANE1, REHLANE2, REHLANE2b). Do not overlap them with another full gate run.
`npm run test:web` runs `test:web:unit` (the vitests) then `test:web:e2e`, which rebuilds the HTTP host: a running
development instance can hold its assemblies. Identify the owner before stopping a process; do not
stop an installed application or an unrelated host merely to unblock a build. Never start a real sign-in (`claude auth login`,
`codex login`, an agent's own login) from a test, a probe or a transcription of today's words, even under a scratch home: it opens
the person's browser on their real account. Take the words from the code, or run the stub adapters (LIVELOGIN1). The sign-in
actions refuse the agent's binary from PATH where the home lies under `_fixtures/` or the OS's temporary folder, with nothing
started, and `node tools/live-logins.mjs` finds a test that names a real login command or lets an agent's command fall back to PATH.

A branch that runs only its own classes still owes the censuses that read every source. No list of them is kept here: a
census that reads sources by text is a test that names what it reads, and the selector reaches it from a changed path. The
desktop's carry `[Trait("Category","Census")]` and are the `censuses` gate; a CLI or tool census names its reads as
`// reaches: <glob> — <why>` lines (the home census and the provider census name `tools/**`, the tree-base census every code
file, the declarations test every `.d.mts` and script beside it), so `node tools/reached-checks.mjs --base main` (a builder's checks
and the merge's `reached` gate) runs it for a diff that touches those paths. A declared path is one read from the test, so no
suite's bound leaves it out, and it reaches the test even where the census cannot bound its reach (a computed import). Every
file of a declared test set (below) is a unit of the set's suite, run from the set's folder. The scripts (`partials`, `read-shares`,
`optional-locks`, `live-logins`, `test-patience`, `tool-test-census`, the layers' counts) are steps of `verify:checks`, which `npm
run quick` runs whole. A census that reads a place its test does not name is a defect of the test: add the line. Each of the six
reds that landed on 2026-11-17 and 18 past a green merge is replayed in `tools/merge/missed-reds.test.ts` (MAINRED1).

Which tests run is declared once, in `daoris.gates.json`'s `tests`: a list of sets, each a runner (`node`, `vitest`,
`playwright`) and the patterns of its files (`tools/*.test.mjs`, `src/Daoris.Cli/tool-test/**/*.test.ts`), with the gate that
runs it, the census's `suite` for its units, the folder its runner starts in and the runner's own `args`. A test file is in the
first set whose pattern holds it, so a new file under a pattern is run by its gate, censused and reached with no list to edit;
to scope a test apart, declare a narrower set before the wider one it carves from (`layers` before `tools`). A gate or script runs
its sets with `node tools/test-sets.mjs run --gate <gate>` (a set by its name; `list` says each set's files, `reach <path>` the
sets a changed path reaches); the tools' Vitest
projects read their files from their sets; a change reaches a set's units by the census's reach of each (`tools/test-map.mjs`),
a `bounded` set's only within its gate's `paths`; a pick (`of`) runs some of another set's files apart, as `web-rules` does.
`node tools/tool-test-census.mjs`, a step of `verify:checks`, refuses a test file no set holds, a set that holds none or names
an undeclared gate, a set run by its own config that would run an earlier set's file twice, and a script or gate command that
names a test file. The .NET halves are their gates' `dotnet test` commands: a test project and its filter.

A focused vitest run for a React warning (an `act` warning, a console error) passes `--reporter=default`: vitest 5 picks a
quiet reporter for an agent's run, which prints no console output of a passing test, so the warning is there and unseen
(FRAMEACT1). Count `not wrapped in act` in the output, and wrap the native `focus()`, timer or language change that
caused one in `act`, never the warning in a filter.

`node --test` alone is not the workspace's done command. The root `npm test` delegates to the CLI
workspace; `npm run verify` also runs types, doctrine and document/tooling checks. Installed proof
is not supplied by a mocked bridge, Storybook or a browser-only end-to-end test.

## The development cycle

Main is the development branch, and the cycle's aim is main green and the install current within the hour.

1. **Set up.** A builder runs `node tools/worktree-setup.mjs` from its worktree first (`--plan` says what it would do, `--build`
   builds the driver's and the modules' test projects too): the local inputs and sensitive-patterns file the Daoris home's record of the repository names (the gates file's members where the home names none) copied as files, never linked, as a gate tree and a session's tree take them;
   dependencies installed from the lockfiles where missing or not from the lockfile as it stands; the merge drivers set, the
   index written, each gate's declared `regenerate` command run and the test projects restored, then the base recorded: `npm run quick`
   runs once (`--record-base`) and its verdicts are kept in `local/quick-base.json`, so a later `npm run quick` says which reds are
   new and which the base already stood in. It refuses a `local/` or a `node_modules` that is a link.
2. **Land.** A finished branch lands through `node tools/merge-branch.mjs <branch>`, never a plain `git merge`. Its default runs
   the blocking checks, the CLI's verify and the reached checks: the test units the merge's paths reach in the driver's, modules',
   service's, page's and tools' suites (`tools/reached-checks.mjs`), nearest the change first and bounded per suite, the count left
   over named for the CI. The gate reads the paths from the base the run hands it in its `{base}`: HEAD for a merge in place, the
   candidate's base in the queue's committed scope tree, the merge base in a preflight's committed branch, so a committed tree's
   reached units run as an open merge's do. Its time goes to tests: it reads the census once per content tree (kept under
   `_fixtures/reached-census/`, so the test-map gate's reading or an earlier run's on the same tree serves it), restores and builds
   each test project once, not where its stamp under `_fixtures/reached-build/` still holds its inputs and output, and runs each
   project's reached classes in one `dotnet test --no-build --no-restore`; the censuses gate after it runs on that build. The
   projects whose build the change makes stale start building before the plan, in one `dotnet build` each, so the build runs
   through the plan rather than after it; and each light suite (the page's, the CLI's, the tools' units) runs in a lane of its own
   beside the build and the .NET runs, which need nothing of it, while an end-to-end or Process group waits for them: the gate's
   time is its longest lane. On 2026-11-18 every first run of a merge reaching the desktop was killed at its budget inside that
   build, after 42 to 147 s of census and plan and 153 to 260 s of build, no test begun; replayed as a first run (no census, no
   stamp, its C# files newer than their build, 3 to 5 heavy runs beside), PUSHBUSY1's took 299 s before and 238 s after,
   gates14's 418 and 303, ACTORFLOW1a's 810 and 660. The census and plan of a new tree (55 to 138 s) are what is left. Each step
   (census, plan, restore and build per project, discovery and run per suite) is said as it goes and kept in the merge record's
   `steps`, so a gate killed over its budget names the step it was in and its breach is classed by its longest step. A list, a census or a structure red a branch brings stops its merge, in minutes, instead of waiting for
   a CI run of hours. The run keeps going past every failure but a blocking light gate's (universal, code-map, test-map), and
   `--keep-going` past those too, so one round finds every red and writes them in one brief.
3. **Records.** `TASKS.md` merges by its rows' ids for every merge git makes (`tools/merge-backlog.mjs`, the merge tool's own row
   rule, set by `npm ci` and by the setup), so a builder's `git merge main` and the merge tool agree; the archive, the changelog
   and the fix log are append-only and merge by union.
4. **Stage, then the CI.** The install is staged from a commit its merge landed: the fast tier passed at the merge, and the heavy run
   after the landing (`local/heavy-runs.jsonl`) is green or red only on classes already filed as main's red (a run a newer
   landing superseded is no pass: the newer commit's run carries its classes) (`npm run publish:desktop -- --to <install> --service --stage`, which names what the commit lacks); the CI then runs the
   rehearsals on the staged commit (`--ci --stage <commit>`), and its reds are fixed as small commits on main.

## Integrate and record

`tools/merge-branch.mjs` is this repository's integration tool. It reads declared gates and lane
ownership, regenerates indexes, and records verdicts in the main checkout's `local/gate-verdicts.json`,
keyed by tree, whichever worktree runs it. The store keeps 40 verdicts of each gate for the scoped run and 40 for the rest,
and a verdict a week old is past its use unless it is its gate's newest or its newest pass (`tools/merge/verdict-keep.ts`):
no reader reads it and the next write drops it, since a tree a week behind main differs from it by thousands of paths and
stands for no gate. So `node tools/test-recall.mjs` finds a week of gate failures there; the CI's records hold more. A
judgement of the store (a preflight's or the queue's plan, the CI's, `--passed`) compares the checkout's tree with every
verdict's tree: it asks git each pair it lacks at once, 64 pairs a `git diff-tree --stdin`, keeps each answer by its pair of
tree ids under the main checkout's `local/scratch/tree-drift/` (a tree never changes; the newest eight judged trees' files
stay, `tools/merge/tree-drift.ts`), and places each changed path by the lane table once whichever gate asks (`reachingOf`).
The plan reads the store in seconds, not a git start and a placing of thousands of paths per verdict (VERDICTSCAN1). Inspect
a branch's plan before integration; use its resume/retry commands when a merge is already open:

```sh
node tools/merge-branch.mjs <branch> --preview
node tools/merge-branch.mjs <branch> --message <file under local/> [--keep-going]
node tools/merge-branch.mjs --queue [--recheck | --known]
node tools/merge-branch.mjs --preflight [--base <ref>] [--bound <minutes>]   (in the branch's worktree)
node tools/merge-branch.mjs --drop-batch [<branch>]
node tools/merge-branch.mjs --plan <branch>
node tools/merge-branch.mjs --continue
node tools/merge-branch.mjs --stale
node tools/merge-branch.mjs --rerun <gate>
node tools/merge-branch.mjs --full
node tools/merge-branch.mjs --full --beside [<commit>]
node tools/merge-branch.mjs --full --beside --daoris-verify
node tools/merge-branch.mjs --ci [<commit>]
node tools/merge-branch.mjs --ci --stage [<commit>]
node tools/merge-branch.mjs --ci --night [<commit>]
node tools/merge-branch.mjs --ci --watch
node tools/merge-branch.mjs --ci --stop
```

`--full` runs the full set on the current checkout, including the long gates; `--ci --night` runs
the whole set in the gate tree. The gates that
start no real process run beside the Process lane, one Process gate at a time (GATELANES1), up to three at once but never
two that build one .NET component (GATELANES2), and a Process half starts only
once the heavy light gates (the CLI, .NET and web suites) have ended, so its timing tests run with no
suite loading the CPU (GATEORDER1). A Process half runs `--no-build` when its fast half passed in the
same run on the same tree (GATEBUILD1). The release, family and deployment rehearsals run beside the git half, one after
the other, and every other Process-lane gate alone (REHLANE1, REHLANE2, REHLANE2b); the web gate's vitests are a light gate of their own, `web-unit`, beside the
service suite (WEBSPLIT1), and the measured-box check (`web-measured`: the specs under `src/Daoris.Web/measured/`, the page's stories laid out and measured in Chromium) is another that builds none (FLOWCHART1e). The driver's Process half runs eight classes at once (PROCPAR1), and its
slowest classes print by their own times beside the half's wall. Before a heavy gate (a suite that builds .NET or the
page, a Process half, a rehearsal) the tool reads the machine's free commit and free physical memory, and waits, up to
ten minutes, while either is below the gate's need (its class's, or the gate's `memoryGb` in the Daoris home's record); a gate
that died of memory reads MEMORY, not a failed check, and runs again once, alone, at the run's end (MEMGATE1), as does one
whose hang bound killed a test under load, once the machine is free; one past its budget under load runs on instead (D346,
*Choose the checks*). Add
`--one-at-a-time` on a loaded machine to run every gate in turn. `--full --beside` is a guest on the machine (CIPRIO1): it runs
at below normal priority with build servers of its own (its Process lane at normal: FAMILYFAIL1c), a second is refused
while one runs, and a run of main's tip ends the gates it is running, each with its whole tree, and starts no more once a
newer tip exists (CIPRIO1c), recorded as superseded and never as passed; a Ctrl+C ends its trees the same way. A stage requires that full verdict. The merge tool normally runs selected gates; a merge receipt alone does not
prove the long gates ran. Read D115 and the tool's usage before changing queue/merge behavior.

A branch lands through the merge queue, which waits on a scoped run in a tree of its own (*How a merge goes*,
below). `<branch>` alone is the merge in place, which follows. It runs the blocking checks alone by default: the universal gates (the index
regenerated, the sensitive scan), the code map and the census of test units' check (`test-map`, below), and the web's typecheck (`web-types`, `npm run typecheck:web`, about a minute) when its paths reach the
page's sources or its compiler's configuration: two sides that each typecheck may not together (MERGETSC1); and, where
its paths reach the page's sources or its test configuration, the web's design and structure checks (`web-rules`, `npm
run check:web-rules`: the design, structure, provider-census, presentational and page-measure tests alone, a minute or two), which a story or a
module broke twice before the CI beside's `web-unit` reached it (MERGEWEBRULES1); and, where its paths reach the desktop's sources, the
desktop's censuses (`censuses`: every test class that reads the sources by text carries `[Trait("Category","Census")]`, and the gate runs
`dotnet test <project> --filter Category=Census` for the driver's tests and the modules', REVIEW2 T6; the fast halves filter
`Category!=Process&Category!=Census`, so each census runs once a run). At a merge the censuses judge the diff (CENSUSDIFF1): the
merge writes its changed paths to a file under `_fixtures/census-scope/` and names it in `CENSUS_SCOPE` (`tools/merge/census-scope.ts`),
a census marked diff-able in its project's `CensusMarks.cs` judges only those files, and one marked whole (a rule across files, or
one not yet reading through `CensusScope`) judges the tree; each project's census of censuses holds every census marked once. A text
census whose rule judges a file on its own text is diff-able: it walks through `CensusScope.Judged` (or `SourceCensus.Driver(type)`),
a list it holds both ways judges the listed members of the scope's files (`Listed`, `Judged` with the member's path), and a count of the
walk's reach is held by a run with no scope. A class whose facts mix is marked by its per-file ones; a fact across files in it
(a uniqueness, a count, a structure of holders) reads the whole tree. The text censuses read each source once, and each one's
comment-stripped text once, through `SourceTexts`. What the driver's censuses share (the say door's scan, the stripped texts, the
driver's compile, the reference map, and under a scope the compiles it reaches) is made once before the first class runs, by the
assembly's runner (`CensusWarm`; xUnit 2 has no assembly fixture), waiting at most 30 s so the run's 60 s hang bound still sees a case
start; so a case's recorded time is its own work, never another's wait for the warm-up. The binding stays the semantic classes'
fixtures', made once a run and begun with the first class beside the cases, so the hang bound never sees a minute of fixtures with no
case; a reach read from a warm reference map runs on the case's own thread. With
no scope (the CI after the landing, a developer's run) every census judges the whole tree. A census class holds what a source says, never
behaviour: a class whose few source-reading facts sat beside bridge or graph cases has those facts in a census class of its own
(`ModuleSourcesCensusTests`), and its behaviour is the fast half's. A semantic census (the structure classes, the declarations'
literals, boundary and keepers, over one compile of each shipping project) is diff-able too (D313): under a scope it binds only the
trees of the scope's files and of the files that spell a type one of them declares, wherever they compile (`CensusReach`, from a
reference map of each source's syntax kept under `_fixtures/census-reach/` by text hash), compiles no project the scope leaves
untouched, and judges a list's row only where its file was read; the keepers' flow, which crosses a project's files, runs whole in
each project reached. Binding is the cost: alone, the driver's whole walk takes about 29 s and its error check 22 s more, a scoped
walk of a few files a second or two. A semantic census that must stay whole says why in `CensusMarks.SemanticWhole`, which the census
of censuses holds. Every other
gate its paths reach is listed and skipped, "in the CI beside": the CI (`--ci`, below) gates main's
tip after a merge or batch; fix what it reports. `--gated` runs what the paths reach, for a risky merge; the full set
is still required before any stage (GATESLIM1, D115).

The tests of the tools only the parent runs (the merge tool, the install's publisher and the family rehearsal's set-up
kit: `src/Daoris.Cli/tool-test/`) are two gates (TOOLTIER1). The unit tier, `tool-tests` (`npm run tooltests`, the folder's
`*.test.ts`), runs beside the other light gates. The end-to-end tier, `tool-e2e` (`npm run tooltests:e2e`: the folder's
`*.e2e.ts`, `tools/as-merged.test.mjs` and the merge tool's Vitest `e2e` project), holds every spec that seeds a scratch
repository and starts real git and the merge tool. It runs in the Process lane, alone, and a spec of the unit tier that takes
over ten seconds alone moves there or is made pure. A case of it is its git and tool starts, each slower the more the machine
starts at once, so each stays under two minutes (E2EFAST1, `tool-test/support/e2e-repository.ts`): a file runs three cases at
once and the tier four files, since the files one at a time took 57 minutes on a loaded machine against 33 at four; the merge
tool's start asks git once for each kind of fact (`tools/merge/home.ts`); a case copies the repository its file seeded once rather than build one, writes a branch in one
`git fast-import`, runs stand-in gates, and proves one mode. An end-to-end spec is reached only by a path its gate's `paths` declare,
the merge tool's own files (`tools/test-map.mjs`, `bounded`): a change to a tool the merge tool only imports reaches the unit
tier and no end-to-end spec. A merge's reached checks run the three specs nearest its change (`E2E_FAST`, `tools/merge/heavy-tier.ts`):
of those within two reads of a changed path, a spec named as a changed file first, then the fewest reads; they defer the rest to the
run after the landing, as they defer a Process class, and name each spec deferred. Three start side by side, so they cost a merge
about the longest of them, and only a merge whose paths the tier's gate declares (MAINRED1). One case fails after thirty minutes and the run exits once its cases are done, whatever they left running, so the gate never waits on
one unbounded; a case's invocation past its bound is ended with its tree, and one whose tree outlives the end is answered
after the patience with the leak named (`tool-test/support/bounded-node.ts`). `npm test` and `npm run verify` do not run them. A merge runs either gate only when its paths reach it (`tools/merge-branch.mjs`
and what it imports, the publisher, the kit, the folder); `--full` and the release workflow run both (GATESLIM1). A merge-queue
case runs on a scratch repository named for its test process, its children with an environment the case builds (no inherited git
variable that locates a repository, git's ceiling at the fixture), and no case changes the test process's environment: run a
probe of the runner in a process of its own, and write a stand-in gate's paths into the gate (QUEUETESTLEAK1, D115's note).

`--full --beside` runs the same full set on a commit (main's tip unless named) in `local/gate-tree`, a
detached worktree of its own with its dependencies installed from the lockfiles, so merges continue in
the main checkout meanwhile, under load (GATEWT1). One runs at a time. Stage what it gated from that
tree, never from the moving checkout:
`npm run publish:desktop -- --to <install> --service --stage --from-gated <commit>`. A merge's prune
removes the tree once its stage is done, and `--prune` once its run has ended; neither while it runs.
Each run copies the private files a gate reads under `local/` (the sensitive patterns file the home names, today
`local/sensitive-patterns.txt`) from the main checkout into the tree, refreshed, and keeps the run's logs in the main
checkout's `local/scratch/beside-<sha7>/` when it ends, pass, fail or error, so a prune loses none (GATEWT1b, GATEWT1c).
A run killed outright leaves its logs in the tree; the next run keeps them there before it clears them.

### Work the local install builds

The install's driven sessions build rows of this repository in their own trees. Six practices keep one line of work in one
place (LIMITGOON1, LIMITRESUME1d and REVIEWLANDED1):

- **A stuck session is resumed in place,** never re-published as a new quest. Answer its park, resume it (`session say`,
  `chat --session`), or let its quest go on. A new quest for the same work opens a second tree, and both must later rebase onto
  whichever lands first.
- **Land through Daoris's own `tree land`.** A squash done beside it, such as one to correct a commit's attribution, is read
  as its sessions' work by content (SQUASHSEEN1, D102's note), so those sessions are not reopened to rebase. Only a squash
  whose merge would conflict outside the backlog and the generated files is still unseen.
- **Publish a quest that builds on another row only once that row's landing is committed.** A session's tree starts from
  main as it stands. A quest that names a row as landed while its merge is still being tested sends the builder to ask
  whether to merge the unreviewed branch.
- **A router row both sides changed is a conflict, not a row twice.** `docs/README.md` merges row by row through
  `tools/merge-rows.mjs` (ROUTERMERGE1, `.gitattributes`): a landing whose branch changed a router row that `main` changed
  too stops on that row, marked, and a branch still carrying a row `main` has since replaced leaves `main`'s. A
  clone without the driver configured merges the file as text (see *Prepare the workspace*); a branch cut from such a
  merge can still carry a row twice, so run `node --test test/doc-duplicates.test.ts` in `src/Daoris.Cli` before committing
  a merge, and drop the older row.
- **A quest whose start failed on an account goes on by itself.** When a session fails at its start because its account refused it
  (an expired sign-in, a limit), Daoris carries its quest on once an account is ready; publishing the quest again makes a second
  session doing the same work, and only one of the two lands. Read the quest's sessions before publishing anything again.
- **A landing is done when Daoris says so.** After committing a session's landing, `daoris tree land <session>
  --plan` must answer that its branch holds nothing `main` does not. Where it lists commits, `main` lacks them: a session
  told to rebase after its landing may have made new ones, and they are work to land, not a stale row. Work on `main` as a
  replay, its `quest/` branch merged and pruned or not, is read as landed by content (REVIEWLANDED1, D102's note): its session
  leaves the review and its tree goes without `--force`. Only where a hand-resolved conflict left neither its patches nor a
  clean merge on `main` does a no-content merge (`git merge -s ours <its branch>`) still record it as held, and a press of
  *Accept* then lands nothing and clears its review row.

### The CI between releases

**How a merge goes** (CISCOPE1e, D207's design §4). A branch lands through the queue, in three steps and this order:

1. **The records commit, on the branch.** What the merge carries beyond the branch's own work: a finished row moved from
   `TASKS.md` to the archive with one outcome line, a row left open rewritten in place and kept inside the backlog's
   `words` budget in `daoris.json` (`daoris check` reports it; condense a row, never drop work), the changelog, and the
   fix log. A regression is a dated entry with its root cause. A sighting of a flake is not a backlog line: it goes
   under the open entry for its kind, `FLAKE1` (a test that fails under load and passes alone), `TEST1` (a Node process
   aborting on Windows) or `REH1` (the release rehearsal's canon-upgrade phase), and is fixed only where its cause is
   found; a capture is no permission to change timeout policy. The tool commits nothing it was not handed.
2. **`<branch> --preview`.** The merge against main's tip, made with `git merge-tree` so no file of the checkout moves; its
   diff, any conflict, and its scoped run are printed, and the tip you read is recorded.
3. **`<branch> --message <file>`**, the file under `local/` holding the merge commit's message, verbatim, with its
   `Co-Authored-By:` line. The branch's tip and the message's bytes are frozen here. A tip that differs from the last preview
   is refused, so a records commit made after the preview needs another preview.

The invocation starts the queue's runner where none runs (`node tools/merge-branch.mjs --queue-run`, detached, its log
`local/merge-queue.log`, its acknowledgement waited for thirty seconds), then waits and ends with *in main* or *contained* (0), or
*handed back* with why (1). Each enqueue has its own request id, retained through re-forms and outcomes, and its place in a sequence
the queue keeps; requeueing the same tip waits for that occurrence while the earlier hand-back stays in history. Ending the wait leaves
the request queued. A request `--drop-batch` takes off, queued or forming, is handed back as dropped, and its wait ends (1) saying so
and whether main holds its tip anyway; a wait whose request is in no record at all ends (1) instead of polling (QUEUEWAITEND1). Only
the queue's own checkout, the repository's first worktree, previews, queues, drops, rechecks or runs the runner: a verb started in a
linked worktree is refused, and so is a runner of another checkout's tool (QUEUETESTLEAK1). The runner forms a **candidate** in
`local/scope-tree`, a worktree of its own with warm builds: every queued branch, up to eight in arrival order, each merged on the last
with `--no-ff` and its own message; the requests it takes stand as `forming` until the candidate is written. A branch the resolvers
cannot merge is set aside and handed back. The resolvers take the backlog
and each row table `daoris.gates.json`'s `integration.merge` declares `by: rows` (read from the line: the main checkout's `HEAD` for a
merge there, the candidate's base for a queue member; never the branch's or an earlier member's copy) by the merge base: a row a side added
is kept, one removed is dropped, rows both add at one place are both kept, a row both changed
is a conflict (INTEGRATE1c; a `run` writer waits for its own acceptance). The row tables are read through the whole `integration`
key (`integrationOf`), so a declaration that breaks any key's shape settles none (INTEGRATE1d). This repository declares one row table, the
router `docs/README.md`, which nothing reads until the owner accepts the declaration's blob on the page; the declared runner
(`--full --beside --daoris-verify`) hands its gates the `integration.env.pass` names of the declaration it is handed, which is the accepted
blob, and a merge in place passes none (INTEGRATE1d part 4, D215). A branch whose work the candidate holds already is **contained**,
never set aside: one the line holds is settled at once; one an earlier member holds stays on the candidate, settled only when that member
promotes, and merged or found contained again, as its own request, when the candidate ends otherwise (a hand-back, a split, a re-form, a
drop), in the queue's order. One dropped by name alone leaves the candidate and its run standing. `--queue` shows it. The runner's gates
and probes get the environment it had at its start, without the tool tests' variables or the tool's `MERGE_BRANCH_*` switches. On the
candidate it runs the
**scoped set**: the blocking checks always; each gate the candidate's paths reach, a .NET fast half whole; the `Process` halves
by class; the git half within an eight-minute budget, the rest deferred; a gate holding an open known red, whatever is
reached. Browser specs and rehearsals are deferred to the background run. A gate whose pass stands on the candidate's tree is
kept. Each verdict is recorded as `scoped`, and each run is a line in `local/scope-runs.jsonl`. On green, main fast-forwards
to the candidate. That happens only when the main checkout is clean (no tracked change, no merge open, on `main`); otherwise the
candidate is held, said, and tried each tick. If main moved meanwhile by a direct commit, the candidate is formed again on it
from the same frozen requests. A move by records only (the records lane, `docs/**`) defers `service`.

On red, each failure is judged by its signature (`tools/test-failures.mjs`). A failing test still known red on the base is
inherited with no run. Every other failure first meets its gate's **confirm run** (INTEGRATE1b, D215's note): the gate runs once
more on the candidate's tip as it was composed, holding every place of the machine's admission (below), once per failing gate.
Failing again with its signature, it is judged as follows. Passing, it is a sighting (`local/integration-sightings.jsonl`, never
a flake), and the candidate goes by `local/integration-policy.json`'s `unconfirmed`: `split` by default, the owner's `hold` (held
for a decide, which has no door, so a drop or a commit on main moves it) or `land` (promoted on that tip alone, its pass standing
for no other). A confirm run whose
admission is not shown (the admission off, no `DAORIS_HOME`, a record it cannot write, another live hold beside it) is
inconclusive and read as `split`. Neither runs anything on the base. `local/scope-runs.jsonl` keeps each confirm run's
composition, admission evidence and wait. A failing test that fails the same way on the
candidate's base is inherited and filed as known red. If its known red has expired, the queue stops instead. A failing gate that
names no test (a check, a build, discovery, a host) and fails on the base **stops the queue**. Every merge then waits until a
direct commit moves main, `--queue --recheck`, or the runner's own recheck an hour on lifts it. Otherwise the failure's
selection runs on every prefix of the candidate. A .NET class selection intersects the gate's existing filter in one
`--filter` argument, preserving its category and other options. The member at the last transition into the signature is handed back, with
any members it needs, earlier transitions named as interacting. The prefixes either side of it run once more, and a
disagreement makes the failure unresolved. So does a prefix that cannot run the selection, or a whole candidate that does not
fail again. An unresolved candidate of several runs its members one at a time. One of one is handed back as unresolved, filed
as a flake, and the queue goes on. The members kept are formed again and judged on their own green. A gate that died of memory
twice holds the candidate until memory is free; it never runs on the base and never stops the queue. `--queue` prints the
stop, the candidate and its members, what is queued (and whether a branch has moved past its frozen tip), and what was handed
back. `--drop-batch <branch>` takes a queued branch off, or ends the candidate that holds it.

**Every red once, and main's seen.** A candidate's run keeps going past every failure but a blocking light gate's, or past
those too where a request was queued with `--keep-going`. Its reds go in one brief, `local/scratch/merge-<branch>/brief.md` for
each member, each failure marked main's or the branch's by its judgement's run on the base, and the waiter names it. The merge
in place writes the same brief, marking each failure by its selection run on main's tip in `local/scratch/main-tree`. After each
landing the cheap suites (`cli`, `cli-tests`, `tool-tests`, `censuses`) run on the new tip in the background, every one to its
end, in `local/main-watch-tree` (`--main-watch`, recorded in `local/main-watch.jsonl`; one started while another runs ends at
once, and the running one then watches main's newest tip). A red writes its brief in `local/scratch/main-watch-<sha7>/` and holds the queue's next merge, said
by `--queue` and to each waiter, until a watch of a later tip is green, the CI holds each failure as a known red, or
`--queue --known` files them as known reds. Every run writes its start to `_fixtures/test-timing/run-start.json` (no `DAORIS_`
variable reaches a gate), so a time ratchet inside a gate judges only that run's records; `npm run verify:checks` run on its own hands its steps its start as `VERIFY_CHECKS_STARTED_AT`, with the same effect.

**A drop ends the run it dropped** (QUEUEDRAIN1). The runner holds every step of a candidate's run, its gates and its failure
probes (prefix and class), in one set of step trees, the cancellation a superseded CI run uses, and every two seconds, and before each
probe, it reads the candidate under the queue's lock. Once it is no longer that frozen candidate (dropped, or replaced by a
requeue that forms another), the runner ends each step running with its whole tree (`taskkill /T` on Windows, `ps` elsewhere),
starts no gate and no probe more, files nothing the cut judgement had found, and forms the next queued work at once. The scoped
tier in `local/admission.json` is released only after the run's last step has ended. It ends only what that run started, by the pid of
each step: another runner, a CI run, a driven session's build, the install and a build server are not in its trees. `--drop-batch`
waits, for thirty seconds at most, for the runner's record of what it ended, and says it (the gates, the probe, the gates never started);
`local/merge-queue.log` has the same line, and `local/scope-runs.jsonl` an `ended` run carrying it. A drop that comes while the scope tree
installs its dependencies lets the install finish, and no gate starts after it. What a test of a step had already orphaned (a child
its killed run left, a detached runner and the gate it started) is in no step's tree, so once every step has ended the runner also ends,
on Windows, each process created since the run began whose parent is gone and which, or what it started, names the scope tree's folder
(`endStrandedUnder`, `tools/processes.mjs`; a gate run as `node qgate.mjs check` names nothing, its child does), each with its tree,
never the runner or what holds it and never a process older than the run. The drop's answer and `drained.strays` say which (QUEUEDROPLEAK1).

**The runner ends with its record, and the queue says what was left** (PROCLEAK1). Before every step, and every two seconds during a
run, the runner reads the queue: where the record names another process as its runner, or none, it ends the steps it owns with their
trees and ends, saying so in `local/merge-queue.log`, and never clears a record that names another. `MERGE_BRANCH_QUEUE_OWNER`, a pid
the tool tests set to their own for every case, ends a runner and its waiter when that process is gone (a waiter then starts no
runner), so a test killed before its cleanup leaves neither. At each candidate's end (promoted, handed back, split, re-formed or
dropped) the runner's log gains a `census` line: how many processes have a command line naming `local/scope-tree`, `local/gate-tree`,
`.claude/worktrees/` or `_fixtures/` and a parent that is gone, with the first three, and none stopped. A test stand-in that waits on
a file takes its test's pid or a bound of `Poll.Patience`.

`local/admission.json` is the admission record both runners are to share (§5.1): the scoped tier's state, and reservations
released only on a read of their job that finds it empty. The scoped runner writes its tier there. The background run does not
read it, and gates run in no job object (CISCOPE1e2). The scoped runner runs at normal priority with build servers of
its own. The CI's watch gates every promoted tip, and the stage's rules are CISCOPE1d's.

**The machine's heavy-run admission** (HEAVYQUEUE1) is a second record, in the Daoris home, not the repository: `locks/admission/admission.json`
(`DAORIS_HOME`, the home's `locks` entry), which the queue's heavy gates (`memoryNeed` above 0), its failure probes and a driven session's
heavy runs all take a place in before they start a heavy process, so a session's focused `dotnet test` beside a candidate no longer
doubles the candidate's gate times into timeouts and unresolved flakes. It holds the runs that hold a place and the runs that ask,
first come first served up to a capacity (one for each seven processors, two to six; `DAORIS_ADMISSION_SLOTS` sets it, 3 on 22
processors). A run is blocking (someone waits on it: a merge's gate, a check, a builder's run) or background (the heavy tier
after a landing and the CI beside, marked `background` by `DAORIS_ADMISSION_BACKGROUND` in their environment,
`tools/merge/background.ts`; MERGENOKILL1, D346): a blocking run waits only for the blocking runs asked before it, and a
background run runs below normal priority, holds one place at most between all of them, takes it only while no blocking run
waits, and starts no group, even one that takes no place, while one waits. A run waiting says in a sentence what it waits for (the holds, whose and for how long, and the runs asked before it)
and looks again every two seconds; **it never times out and a gate's clock starts only once it has its place**, so a wait is no
failure of the gate. An entry counts while its process runs and it has beaten within two minutes (its writer rewrites `beat` every
twenty seconds), so a holder that dies, however it dies, releases at once, and nothing is released by a timeout while it lives.
It changes no verdict, order or cancellation: a drop ends a gate waiting as it ends one running, holding nothing. It governs only
what the queue and driven sessions start, never the owner's own sessions or another process. The merge tool's side is
`tools/machine-admission.mjs` (off with `MERGE_BRANCH_ADMISSION=off`, and under `MERGE_BRANCH_MEMORY=off`, the tool's own tests, unless
`MERGE_BRANCH_ADMISSION=on`; with no `DAORIS_HOME` it shares nothing and nothing waits, and so a red candidate's confirm run is
inconclusive), the driver's is `MachineAdmission.cs`, and `fixtures/machine-admission.json` is the table both carry. A confirm
run takes every place at once, as that many ordinary entries in one write, and waits for every heavy run admitted before it. A session reaches it through `daoris gate run` (D180's
HEAVYQUEUE1 note): its marked gates take a place after the repository's slot, and `run-heavy -- <command>` runs a command of its own,
a filtered `dotnet test` or a test file's run, as a heavy run with no slot; a driven session is told to use it (`HeavyTold.Focused`).
The queue's admission is the machine's: it does not read `local/admission.json`, whose reservations remain CISCOPE1e2's.

**One heavy tier at a time** (HEAVYSTACK1). The heavy tier after a landing (`tools/heavy-run.mjs`) holds the heavy tree alone,
through `local/heavy-stack.json` (`tools/merge/heavy-stack.ts`), so landings in a row hold one admission place and run one git half,
not one each. A tier that lands while an older one runs waits, and asks it to stop through the older one's halt file
(`local/heavy-halts/`, read by its `reached-checks --halt-file`): the older one builds nothing and starts no class group more, ends
the group in flight by its own pid where the newer commit changes what its classes read and finishes it otherwise (its passes then
stand on the newer commit), and hands over its range and the classes it left unrun. The newer tier runs from the oldest base it was
handed, on its own commit: the union runs once, and each class the older one passed on inputs the newer commit keeps is replayed
from the store. A tier still waiting when a third lands is superseded at once, its range carried by the third. The older run settles
`superseded` (a `HANDED` line; `red` where it found a red first, filed on its own commit), with `handedTo` and `handed`; the newer
names the runs it carried (`took`, `from`) on its `heavy tier:` line and in each finding it files, which names the commit the class
ran on. A stage reads a superseded commit as no pass. A tier waiting for the tree, or in its reached run for the machine's admission,
past ten minutes says so on a `heavy tier:` line, and again at each ten minutes more; one waiting three hours for the tree gives up,
its range carried by the next tier. A holder or waiter counts while its process runs and has beaten within two minutes.

`--ci [<commit>]` is the local CI (CI1, D180's CI1 note). It gates main's tip, or the named commit, with every gate but the
two rehearsals it owes a stage (below), in the same
`local/gate-tree`, holding its lock, and never touches the main checkout's tree. It runs only the gates that commit's
verdicts lack. A gate runs again when it has no verdict, when a path changed since its last pass reaches it, or when its
last verdict failed, whatever changed since. Every other gate keeps the verdict that stands, and the first run on a machine
runs every gate. The records are not forgiven here as the stage forgives them. A plain run keeps going past a failure, for the whole picture; a stage's run (`--stage`) ends at its first failed gate (one that failed again alone, not a sighting that passed alone): it starts no gate, ends the running ones with their trees, lets the gate tree go and says nothing was staged. A run runs at
below normal priority with its Process lane at normal, and measures the machine before its gates start: the light lane
runs five wide when the machine is idle, three otherwise. It is scheduled by memory: a `Process` half waits only for the
fast half that builds its project, and the heavy light gates run beside it, each once its free memory allows (MEMGATE1).
A run of main's tip moves to the newest tip if a merge lands before its gates start. Once they start, it runs to its
verdict, and what lands meanwhile is the next run's.

Each run writes `local/scratch/ci-<sha7>/status.json` and its gates' logs beside it, and notes itself in `local/ci.json`
(its last green run, its runs). Read the status for:

- `state`: `running`, `green`, `failed`, `stopped` or `error`;
- `since`: the last green commit and how many paths changed since;
- `light`: the width and why;
- each gate of the plan with its `lane`, its `state` (`kept`, `waiting`, `running`, `done`, `not run`), its `verdict`,
  `ms`, `log` and `note`; a kept gate also names the commit and tree its verdict was given on and how it stands
  (`exact`, `unreached`);
- for each `Process` half it runs, its `classes`: how many the census read, `whole` (why the whole half ran, or null),
  `composed` (why it passed without running, or null), each class it ran with why, and each it kept with its verdict's time,
  commit and standing.

A `Process` half runs by reach (PROCREACH1, D180's PROCREACH1 note): only the classes a change reaches, the rest kept.
`tools/process-reach.mjs` reads each class's reach from its sources: the driver's or the modules' folders that define what
its files and the test helpers they use name, and the programs it starts. Each class's verdict is kept in
`local/class-verdicts.json` with its tree, and a class whose newest verdict that stands passed is kept. A file read
unnamed (a global using, a project file, `Directory.Build.props`, the run settings) or a path the census cannot place runs
the whole half. So does a run whose oldest kept verdict is more than a day old: a sweep. A half whose every class is kept
passes without running, `done` and `PASS` with no class run, only when its last verdict was a pass that went stale by a path
reaching none of its classes, its trx read and its classes the census's (CISCOPE1b, D207's note); otherwise, a half that
failed outside a test among it, it runs whole for a verdict of its own. A stage refuses a half whose pass
kept classes on verdicts past that day, and `--ci <commit>` sweeps it. `node tools/process-reach.mjs census` prints each
half's classes and reach; `node tools/process-reach.mjs reach <from> <to>` names the classes a range of commits reaches.

`tools/test-map.mjs` is the census of every test unit (CISCOPE1c, D207): each .NET class of every `dotnet test` gate (its
reach PROCREACH1's census, unchanged), each `node --test` file, vitest file and Playwright spec (its reach the closure of
its imports and the tracked files, folders and globs its strings name), and each rehearsal, with the module it tests. An
import the runtime erases or that takes types alone is no edge, and a re-export is followed by its importers for the names
each takes, so a page test reaches the bridge's barrel (`bridge/shell.ts`) and, of its domains, those it takes names from
(WEBREACH1, `tools/test-map/web-reach.mjs`). The page's catalogues are data read by key: a page source reaches, in every
language, the catalogue of each area whose key or dotted prefix it names, and the index hands them all only to a reader
taking `en`, `zh`, `catalogues` or `areas` (the parity and catalogue suites) unless it says `// test-map: reads the
catalogues by key` (the i18n runtime). What the web's setup file loads is in every web unit's reach, and a page source's bare
`.` or `..` names no folder. A change to one domain's page reaches its own tests, the shell's (`app/`, which renders every
page) and the suites that read every source, about 40 of the 745 files; a catalogue's reaches its readers by key and the
parity suites, a median of about 100.
`census` prints them, `reach <from> <to>` names the units a range reaches, and `check` (the `test-map` gate runs `--check`, about 20 s, a
blocking check of every merge and a gate of every CI run) fails on a tracked file no module, lane or laneless group
places, a fixture row of the lane table whose gates differ from the units the census finds reading it, a git-half class at
its project's root with no `// subject:` line, a file that acts on a suite unnamed missing from `tools/test-map.json`, or
a static skip whose reason names no open row. `check --commit <sha> --reports <dir>` also holds the census of that tree
to the classes a run's trx files ran there. An edge no string names is declared in the test (`// reaches: <path> —
<why>`); a file whose strings are a table of paths, not reads, says `// test-map: strings name paths as data`.
`tools/test-failures.mjs` reads a trx, vitest's JSON or `node --test`'s TAP report, keeping each failure's case, exception
type and location (the frame's method and its source line's text, never its number), and `tools/test-recall.mjs proof`
replays the month's recorded failures for D207's recall.

`--ci --watch` is the form to leave running. When main's tip moves and the gate tree is idle, it gates the newest tip. It
never gates again a tip a run gave a verdict on (green, failed or an error); a stopped run's tip it gates again. A second
watch says the first runs and exits. `--ci --stop` asks it to end: it
ends the gates it is running, each with its whole tree, keeps every finished verdict, and lets its records go. A watch
killed outright leaves a stale lock, which the next run takes over, ending what the dead run started (CIPRIO1d, CIPRIO1e).

The watch is the background run, on a schedule (CISCOPE1d, D207's design §5.1). `tools/ci-background.mjs` keeps its
records, each in the main checkout's `local/`. Each run has a kind, which its status (`kind`), its record and every
verdict it gives carry:

- **ordinary**: the watch's run of a new tip.
- **ci**: a plain `--ci`.
- **stage**: `--ci --stage`.
- **nightly**: every gate whole, the two rehearsals included. The watch runs one at 03:00 local time, and first at its next
  start when a night was missed; `--ci --night [<commit>]` runs one by hand.
- **sweep**: with no new tip to ride, a run of a due half alone.
- **full**: `--full --beside`.

`local/ci-sweep.json` holds each Process half's last sweep and the last night that finished. A half is due a day after its
last sweep, and the next run runs it whole, whatever its reach (`due a sweep` in its lines). A run that gives every class a
verdict sweeps the half, red or green. A night is satisfied by a nightly run that finished, red or green. A run stopped
before the classes ran, or a build that left them unrun, leaves either due. The run's lines, its status (`overdue`) and the
watch say what is overdue. `MERGE_BRANCH_CI_NIGHT_HOUR` sets the hour, and `off` runs no night.

Each failure a run finds is filed as **known red** in `local/ci-findings.json`. A Process half's failure is filed by test,
from its trx, with the fingerprint (the test with its case, the exception type, the assertion's file and line, and the
message line) and the signature `tools/test-failures.mjs` gives. Any other gate's failure is filed by its step: its first `not ok` or `✖` line, else its first error line (a build's the set of its compiler
errors), else that the run ended without a failing test, with its exit. A failure
whose signature an open finding holds only moves that finding's last sighting. A pass of the test, or of the gate, clears
it, but only on a commit on main's first-parent line from where it was last seen failing. A pass the run kept instead of
running clears it under the same rules: the kept verdict stands exactly on the run's tree, passed, and is of the same gate; a
test's finding needs a readable trx of that kept run listing the test passed, and is left open, and said, where none can be
read. The clearing commit is the run's own, and the ledger records the kept verdict's commit, tree, time and kind beside it. A
sighting within a day of a
clearing restores the finding with its first sighting. A test known red for 72 hours from its first sighting is past
inheriting: the run and the watch say so, and warn twelve hours before. A death of memory is never filed.

Each run appends its **receipt** to `local/ci-runs.jsonl` as it ends: the commit, its tree, the kind, its start and end,
whether it finished, and each gate's verdict with the kind that gave it.

A plain run, and each run of the watch, owes the family and deployment rehearsals to a stage (CIFAST1, D180's CIFAST1
note). They take 14 to 23 and 14 to 22 minutes of a 45 to 75 minute run. Such a run never starts them. One without a pass
that stands is `owed` in the status (its top-level `owed`, the gate's state `owed`) and in the record, neither failed nor
passed, and the run is green on the rest. A run therefore waits about as long as the git half. `--ci --stage [<commit>]` is
the stage's path. On the commit to stage it runs both rehearsals, one after the other, with whatever else its verdicts
lack. They do not run side by side: each of the deployment rehearsal's publishes packs the CLI in place, which writes and
removes the `dist/` the family's runs of the bin would load.

A release then waits only for the last delta and the rehearsals. Once the commit to release is green, run `--ci --stage
<commit>`. When that run's status says `green` and the gate tree holds the commit, stage it: `npm run publish:desktop --
--to <install> --service --stage --from-gated <commit>`. A run writes its ended status (`green`, `failed`, `stopped`) only
after it lets the gate tree go (CIWATCHHOLD1), so a status that says `green` is never read beside a hold the run keeps. The stage holds the gate tree's lock through its build, so no CI
run moves the tree meanwhile. It still refuses a commit the tree does not hold, a tree a run holds, and a commit whose tiers
have not both passed (no landing run, a heavy run going, in error or red on a class no finding holds). Its refusal names the command
that supplies what is lacking. `--full --beside` remains the run of every gate, for a release that
wants a fresh full set.

`--passed`, the judgement of the full set on the checkout (the stage reads the tiers instead), also asks for the background run's evidence (CISCOPE1d, D207's design §5.3):

- **A receipt.** A finished run's receipt on the commit's tree, matched by tree: an ordinary or nightly run, a plain `--ci`,
  `--ci --stage` or `--full --beside`. A sweep alone, a stopped run and a `--full` on the checkout give none.
- **A clean checkout.** The checkout's content is its commit's tree, so uncommitted changes are refused, whatever the verdicts
  say.
- **No sweep overdue** in `local/ci-sweep.json`.
- **No old whole pass.** A whole Process pass given on another tree counts only within a day; one on the commit's own tree
  counts at any age.

Each line starts `not staged`. The remedy is `--ci --stage <commit>`, whose run gives the receipt, runs a due or aged half
whole, and runs whatever the verdicts lack. A known-red test fails its gate here like any failure. Each successful stage
appends to `local/stage-history.jsonl` (the commit, its tree, where it went, when, and whether it was forced). A stage from
the gate tree first says what it brings since the last stage, or that the range is unknown.

Commits are local work and follow green gates (D37). Push, publish, release, history rewrites and
destructive actions retain their human boundary. Do not bypass provenance checks or hooks to make
a task look complete. Subagent branches, when authorized, follow
`.claude/skills/dispatch-subagent/SKILL.md`: a `branch-scout` orients the row and a `branch-worker`
builds it, each defined in `.claude/agents/` with its own model (D160), and
`tools/subagent-usage.mjs` measures what a batch cost. Documentation work does not implicitly
authorize dispatch.

A worker's fresh worktree has no `local/sensitive-patterns.txt`, so its scan and universal gate run
with `--allow-builtins-only`; the merge's universal gate runs the full set and is the first to read
the private patterns. A test that needs an example machine path spells it in pieces
(`string.Concat("D:", @"\", "Users", …)`, as `SessionSyncTests` does) so the scan does not read the
fixture as a leak; a literal home path in a test fails the merge, not the worker.

A change rewrites its area's sentence in `docs/areas/` where behaviour changed, and nothing else by
hand unless a gate asks (the `change` skill). A decision under `docs/decisions/` is written only for a
choice that rejected an alternative. A regression's root cause goes in its commit body, and in
`docs/FIX-LOG.md` only for a regression. A finished task moves to `docs/task-archive.md` with one
outcome line. The changelog describes what a user of a release sees, and the steward writes it at
the scope's end.

## Review and refactor

Use the generated orientation index to find existing helpers before adding another implementation.
Within repository tooling, `tools/fsx.mjs:repositoryFiles` supplies a deterministic inventory of
tracked and non-ignored new regular files, with an explicit repository root. The orientation
generator and document audit share it. `tools/doc-duplicates.mjs:fenced` supplies the existing fence
mask: quoted examples must not become live router, checklist or record entries.

Keep a refactor's public exports and observable behavior unless the task changes their contract.
Use behavior regressions for a reported defect and test the consumers reached by shared helpers.
An indented example in a real backlog row still counts toward its reading cost. Deliberate runtime
twins follow the [twins contract](../.claude/knowledge/twins.md); sharing a tooling helper does not
replace their independent implementations and parity fixtures.

## Maintain the documentation

Each fact has one home. User instructions belong in the root/component guides; current development
practice in this guide and the shared brief; how each area works now in `docs/areas/`; the reasoning
behind a choice in its decision; open work in the backlog; receipts in the archive. A design's baseline code snapshot stays
dated. Update its standing pointer when later decisions supersede it, rather than rewriting evidence
as if the observation had been made today.

`node tools/doc-system.mjs` checks routing coverage and guide freshness declarations. Each tracked
guide needs nonempty repository-relative source paths; every path must match a file or directory
in the Git inventory, including new non-ignored files. A renamed or deleted source needs its
mapping updated. Markdown router links may name sections; this audit checks their file targets,
not whether the section headings exist.

`--json` reports the inventory by role. It checks structure, not whether prose accurately describes
the code. Compare changed behavioral claims with implementation and tests, and record what remains
unmeasured. The devkit's `docs` gate compares commit dates; it is also a freshness signal, not a
semantic review. Word and entry-shape ceilings report advisory overruns (D54/D127).

`node tools/doc-current.mjs` reads the present records declared in `daoris.json`'s `documents.present`
(the guides, the component READMEs and the knowledge this repository owns) for paths, names, routes
and variables the code lacks, and reports without failing (D220). Before committing a change that
deletes or renames code, run it with `--since main`: it lists the records that quote what the diff
removed, and those are rewritten in the same change. A name only test source has is printed after
the rest. An external name goes on the declaration's `allow` list with its reason.

The date gate reads committed history, so an uncommitted guide correction still has its old date.
A proposed-commit Git view may verify universal gates before committing. Keep that environment
away from fixture tests: inherited `GIT_DIR`/`GIT_WORK_TREE` redirect their repository operations.
The merge tool starts every gate, probe, re-run and repository script with `gateEnvironment` (its start environment without `GATE_REMOVED`: every `DAORIS_*` name, git's locating variables, `MERGE_BRANCH_*`; INTEGRATE1a).

Regenerate owned output with its producer:

```sh
node src/Daoris.Cli/bin/daoris.mjs sync
node tools/orient-index.mjs
node tools/partials.mjs --write
dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- map
```

`npm ci` and `node tools/worktree-setup.mjs` also install a `pre-commit` hook (`tools/index-hook.mjs` writes it, `tools/commit-checks.mjs` is what it runs) that does the bookkeeping the `cli` gate would otherwise answer at a merge, on the staged paths, in seconds: the comment gate on the staged sources; the size ratchet (a shrink is written to `tools/size-ratchet.json` and staged into the commit, growth past the size main's list gives a file is refused with the file named); `tools/contracts.mjs --if-stale` where a contract source is staged; and the layers' counts (`tools/layers-census/counts.mjs`'s checks, the slowest step, about as long as the gate's own) where a `.cs` or `.csproj` is staged, after the layers list's shrink (`node tools/layers-census.mjs --shrink`: an allowance the tree fell under lowered and staged, nothing else written, and only where the working tree is the index for what the census reads: no unstaged or untracked `.cs`, `.csproj`, lane table or list, and both of git's reads of that answered). Only a refusal by one of these stops a commit; a step that cannot run says so and lets it go on. It prints what it ran and how long; `DAORIS_COMMIT_HOOK=0` skips it for one commit, a merge in progress and a detached head run nothing. The merge writes the size list's shrinks itself (`generated` in `daoris.gates.json`, `node tools/size-ratchet.mjs --shrink`), so a shrink never reddens it.

`docs/index/` is ignored and never committed. `npm ci` installs `post-checkout` and `post-merge` hooks (`tools/index-hook.mjs`) that write it after git moves a branch's work tree, a new worktree included, and writes it where none stands. A hook's write keeps the index of a clean tree in the repository's git folder (`daoris-index/<tree id>`, six kept), and a tree at a tree id already kept gets that index copied instead of generated, which is most of a new session tree's `files.hook` (SESSIONOPEN3); `DAORIS_INDEX_HOOK=0` skips them, and a detached checkout is skipped. The knowledge server writes the served checkout's again before a tool call once its `HEAD` has moved (`tools/index-fresh.mjs`). The same `npm ci` sets the `notes` merge driver (named in `.gitattributes`), which puts a blank line before a `**Note, ` line glued to the line above inside the merge of two branches' notes; a clone without the driver conflicts there rather than gluing.

Moving files: `node tools/move-files.mjs [--plan] from=to ... --expect-moved N --expect-rewritten N [--expect-files N]` (or `--moves file.json`)
runs `git mv`, respells every relative import of the moved files and in them, and checks that each still resolves.

Only run `map` when project metadata changed; `map --check` verifies freshness. `sync` preserves the
local brief outside the doctrine region. Canon edits also require syncing both example adopters.
A conflict in generated output is resolved by regeneration.

## Packaging, service and desktop loops

`tools/stage-package.mjs` stages the root canon, README, LICENSE and CHANGELOG into the CLI package during
`prepack`; `postpack` cleans those copies. The README is the package's page on npm, so every link in it
is absolute: `tools/doc-links.mjs` (in `verify`) reads it as its registry would, resolves every link in the
documents with its anchor, and holds its agent rules equal in `llms.txt` and `context7.json` (D211);
`tool-test/packed-readme.test.ts` reads it out of a packed tarball. The published entry prefers compiled `dist/cli.js`, so
a source checkout with old `dist/` can execute that build. Tests and the release rehearsal cover the
source/package distinction. `DAORIS_CANON` selects a fixture canon for offline doctrine tests.

`npm run knowledge:build` prepares the repository's read-only knowledge host. `.mcp.json` starts
that built host through `tools/knowledge-server.mjs`; it does not build at session startup. A
running session moves onto the build a merge made within a minute, its handshake replayed, so a
fix to the host reaches it without a restart (KNOWHOLD1). Its development home is isolated from
the install. With no build, its handshake explains the absence and offers no tools. See the [service guide](../src/Daoris.Service/README.md) for production wiring.

`npm run desktop -- doctor|build|run|shot|eval|click|restart|kill` is an instrumented development loop,
not a gate. The [desktop guide](../src/Daoris.Desktop/README.md) explains scratch versus installed
operation, window selection and process ownership. A screenshot must select the application window
being reviewed; the browser window is a separate target.

There is no push/PR CI. [The release workflow](../.github/workflows/release.yml) is manually
dispatched with dry-run as its default. It runs gates across its platform jobs, packages artifacts
and owns version/release stamping. A check not run locally has no push-triggered receipt to rely on.
