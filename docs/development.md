# Developing Daoris

Read [AGENTS.md](../AGENTS.md), the [document router](README.md), the
[orientation index](index/README.md) and the relevant contract before exploring code. The
discovery skills in `.claude/INDEX.md` route the on-demand knowledge. [TASKS.md](../TASKS.md)
holds acceptance criteria and dependencies; [ROADMAP.md](../ROADMAP.md) gives the sequence.

## Prepare the workspace

Use Node 24 for development and the .NET 10 SDK for the service, devkit and desktop. The packaged
CLI supports Node ≥22 because packaging compiles TypeScript. Run commands from the workspace root:

```sh
npm ci
npm --prefix src/Daoris.Web ci
```

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
`node tools/merge-branch.mjs --prune [--plan]` runs alone; its rules are the tool header's GATE2. A
merged worktree with no change and nothing under `local/` but logs and scratch is removed, the agent
harness's lock lifted when no running process names its folder and nothing in it was written for two
hours (a worker's shell shows on no command line). A folder under `.claude/worktrees/`
that `git worktree list` does not list and that holds no `.git` is a half-removed worktree, removed
without following a reparse point. A process holding either is named with its pid, never stopped.
Other registered worktrees use their owning lifecycle tool.

A worktree with no commit of its own counts as merged. So the prune removes it, gitignored
`local/scratch/` with it, even while a worker still uses it, and the agent harness removes a worker's
isolated worktree when it ends without a tracked change. A docs-only or read-only dispatch whose product
is scratch (a brief, a proposal, a read's answer) writes it under the main checkout's
`local/scratch/`, or commits it early; otherwise it is lost with the worktree (2026-10-22, COAGENT1's
design, twice).

Use literal targets and one shell throughout; verify absence afterward. If execution is rejected,
record the action and exact reason, leave the task open, and resolve the actual permission setting
or surface it to the owner. Retry after new authorization or a real permission change through the
same review path. Do not substitute another deletion API, wrapper or shell to evade that rejection.

## Choose the checks

`npm run verify` is the baseline check, not a substitute for testing a component you changed. Its
exact steps are in `package.json`; the component gates are in `daoris.gates.json`. Run focused checks
during the task and the complete reached set before recording completion.

| Change | Additional verification |
|---|---|
| CLI/package/install story | `npm run rehearse` exercises the packed consumer artifact |
| Service, driver, sync, toolchain or canon shape | `dotnet test src/Daoris.Service`, driver/modules checks as reached, and `npm run rehearse:family` |
| Devkit | `dotnet test src/Daoris.Devkit` |
| Web UI | `npm run test:web`: unit tests, production build, HTTP build and Playwright; stories and installed-window proof where the contract calls for them |
| Desktop/modules | Fast suites below; `npm run rehearse:deploy` at the full integration boundary |
| Canon | Sync root and both examples in the same change, then doctrine checks and family rehearsal |
| Documentation/tooling | `npm run verify`, orientation freshness and relevant tooling tests; manually check behavioral prose against source |

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
(PROCPAR1); the modules' runs one class at a time, as its assembly says. The driver's git-only Process
classes, marked `Parallel=Git` beside `Category=Process`, run four at once in one host with
`src/Daoris.Desktop/git.process.runsettings` (gate `driver-process-git`, PROC1c): four at once is load, so
that half runs after the other one, never beside it, and a class marked so is run alone with that file, not
the other, whose filter leaves it out. A git test copies its
repositories from a seed (`GitSeed`, PROC1h; the plain one is `GitSeed.PlainAsync`) unless it reads the first commit's id or
time, and removes its scratch with `GitSeed.Remove`: git writes its objects read-only, which `Directory.Delete` refuses on
Windows (PROCSPEED1). A seed's repositories start no maintenance git behind a commit, a fetch or a push
(`GitSeed.NoMaintenanceAsync`), and a platform's moves on a bare origin are git's plumbing in the origin, never a clone that
pushes (`LandedFixture.SquashOnPlatformAsync`, `CommitOnOriginAsync`): on this machine a git start costs about half a second,
and the git half is its starts (PROCSPEED1b). Rehearsals also run
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

A branch that runs only its own classes still owes the censuses that read every source, since no merge runs them
before the CI beside: each is quick, and on 2026-10-20 each failed a full set its branch never ran. Run the ones a
change can reach before handing it back:

| A change that… | The census to run |
|---|---|
| makes a page test read a file outside the page (a C# source, a fixture) | `node --test src/Daoris.Cli/tool-test/merge-branch.test.ts --test-name-pattern SESSDEL1c` (read a C# source through `src/test/driverSource.ts`, and name it in `tools/merge-branch.mjs`'s MOD9e row) |
| adds or renames a CLI verb | `node --test src/Daoris.Cli/test/cli/cli-usage.test.ts` (the refusals fixture) |
| names a `DAORIS_` variable in a source | `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter FullyQualifiedName~SystemWiringTests` |
| joins a home path in a test or a source | `node --test src/Daoris.Cli/test/platform/home-census.test.ts` (ask `DaorisHome.Entry` or `homeEntry`) |
| opens a listener in a test | `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter FullyQualifiedName~LoopbackListensTests` |
| writes a string that names a workspace, or a `partial` class | `node --test src/Daoris.Cli/test/one-word.test.ts`, and `node tools/partials.mjs` |
| reads or writes a file in the driver | `node tools/read-shares.mjs` (read a file `AtomicFile` writes through `AtomicFile.ReadText`, `ReadBytes` or `OpenRead`) |
| asks git for a `status` or a `diff` in the driver | `node tools/optional-locks.mjs` (pass `--no-optional-locks` before the command, so a read takes no `index.lock` in a session's tree) |
| names an agent's command, a login, or a driver config's `commands` in a test | `node tools/live-logins.mjs` (a script or a stub at a path, never the agent's binary from PATH) |

`node --test` alone is not the workspace's done command. The root `npm test` delegates to the CLI
workspace; `npm run verify` also runs types, doctrine and document/tooling checks. Installed proof
is not supplied by a mocked bridge, Storybook or a browser-only end-to-end test.

## Integrate and record

`tools/merge-branch.mjs` is this repository's integration tool. It reads declared gates and lane
ownership, regenerates indexes, and records verdicts in the main checkout's `local/gate-verdicts.json`,
keyed by tree, whichever worktree runs it. Inspect a branch's plan before integration; use its
resume/retry commands when a merge is already open:

```sh
node tools/merge-branch.mjs --plan <branch>
node tools/merge-branch.mjs --continue
node tools/merge-branch.mjs --stale
node tools/merge-branch.mjs --rerun <gate>
node tools/merge-branch.mjs --full
node tools/merge-branch.mjs --full --beside [<commit>]
node tools/merge-branch.mjs --ci [<commit>]
node tools/merge-branch.mjs --ci --stage [<commit>]
node tools/merge-branch.mjs --ci --night [<commit>]
node tools/merge-branch.mjs --ci --watch
node tools/merge-branch.mjs --ci --stop
```

The last form runs the full set on the current checkout, including the long gates. The gates that
start no real process run beside the Process lane, one Process gate at a time (GATELANES1), up to three at once but never
two that build one .NET component (GATELANES2), and a Process half starts only
once the heavy light gates (the CLI, .NET and web suites) have ended, so its timing tests run with no
suite loading the CPU (GATEORDER1). A Process half runs `--no-build` when its fast half passed in the
same run on the same tree (GATEBUILD1). The release, family and deployment rehearsals run beside the git half, one after
the other, and every other Process-lane gate alone (REHLANE1, REHLANE2, REHLANE2b); the web gate's vitests are a light gate of their own, `web-unit`, beside the
service suite (WEBSPLIT1), and the measured-box check (`web-measured`: the flowchart's and the setting rows' stories in Chromium) is another that builds none (FLOWCHART1e). The driver's Process half runs eight classes at once (PROCPAR1), and its
slowest classes print by their own times beside the half's wall. Before a heavy gate (a suite that builds .NET or the
page, a Process half, a rehearsal) the tool reads the machine's free commit and free physical memory, and waits, up to
ten minutes, while either is below the gate's need (its class's, or `"memoryGb"` on the gate in `daoris.gates.json`); a gate
that died of memory reads MEMORY, not a failed check, and runs again once, alone, at the run's end (MEMGATE1). Add
`--one-at-a-time` on a loaded machine to run every gate in turn. `--full --beside` is a guest on the machine (CIPRIO1): it runs
at below normal priority with build servers of its own (its Process lane at normal: FAMILYFAIL1c), a second is refused
while one runs, and a run of main's tip ends the gates it is running, each with its whole tree, and starts no more once a
newer tip exists (CIPRIO1c), recorded as superseded and never as passed; a Ctrl+C ends its trees the same way. A stage requires that full verdict. The merge tool normally runs selected gates; a merge receipt alone does not
prove the long gates ran. Read D115 and the tool's usage before changing queue/merge behavior.

A merge runs the blocking checks alone by default: the universal gates (with the sensitive scan), the code map, the
index check and the census of test units' check (`test-map`, below), and the web's typecheck (`web-types`, `npm run typecheck:web`, about a minute) when its paths reach the
page's sources or its compiler's configuration: two sides that each typecheck may not together (MERGETSC1); and, where
its paths reach the page's sources or its test configuration, the web's design and structure checks (`web-rules`, `npm
run check:web-rules`: `design/tokens.test.ts` and `test/structure.test.ts` alone, a minute or two), which a story or a
module broke twice before the CI beside's `web-unit` reached it (MERGEWEBRULES1). Every other
gate its paths reach is listed and skipped, "in the CI beside": the CI (`--ci`, below) gates main's
tip after a merge or batch; fix what it reports. `--gated` runs what the paths reach, for a risky merge; the full set
is still required before any stage (GATESLIM1, D115).

The tests of the tools only the parent runs (the merge tool, the install's publisher and the family rehearsal's set-up
kit: `src/Daoris.Cli/tool-test/`) are the `tool-tests` gate, `npm run tooltests`, about four minutes of real git. `npm
test` and `npm run verify` do not run them. A merge runs the gate only when its paths reach it (`tools/merge-branch.mjs`
and what it imports, the publisher, the kit, the folder); `--full` and the release workflow run it (GATESLIM1).

`--full --beside` runs the same full set on a commit (main's tip unless named) in `local/gate-tree`, a
detached worktree of its own with its dependencies installed from the lockfiles, so merges continue in
the main checkout meanwhile, under load (GATEWT1). One runs at a time. Stage what it gated from that
tree, never from the moving checkout:
`npm run publish:desktop -- --to <install> --service --stage --from-gated <commit>`. A merge's prune
removes the tree once its stage is done, and `--prune` once its run has ended; neither while it runs.
Each run copies the private files a gate reads under `local/` (the declared `sensitive.patternsFile`, today
`local/sensitive-patterns.txt`) from the main checkout into the tree, refreshed, and keeps the run's logs in the main
checkout's `local/scratch/beside-<sha7>/` when it ends, pass, fail or error, so a prune loses none (GATEWT1b, GATEWT1c).
A run killed outright leaves its logs in the tree; the next run keeps them there before it clears them.

### Work the local install builds

The install's driven sessions build rows of this repository in their own trees. Four practices keep one line of work in one
place (learned 2026-10-23 and 24, LIMITGOON1, LIMITRESUME1d and REVIEWLANDED1):

- **A stuck session is resumed in place,** never re-published as a new quest. Answer its park, resume it (`sessions say`,
  `help resume`), or let its quest go on. A new quest for the same work opens a second tree, and both must later rebase onto
  whichever lands first.
- **Land through Daoris's own `trees land`.** A squash done beside it, such as one to correct a commit's attribution, is read
  as its sessions' work by content (SQUASHSEEN1, D102's note), so those sessions are not reopened to rebase. Only a squash
  whose merge would conflict outside the backlog and the generated files is still unseen.
- **Publish a quest that builds on another row only once that row's landing is committed.** A session's tree starts from
  main as it stands. A quest that names a row as landed while its merge is still being tested sends the builder to ask
  whether to merge the unreviewed branch.
- **A landing is done when Daoris says so.** After committing a session's landing, `daoris-driver trees land <session>
  --plan` must answer that its branch holds nothing `main` does not. Where it lists commits, `main` lacks them: a session
  told to rebase after its landing may have made new ones, and they are work to land, not a stale row. Where the work is on
  `main` only as a replay or by a hand-resolved conflict, a no-content merge (`git merge -s ours <its branch>`) records it as
  held, and a press of *Accept* then lands nothing and clears its review row (REVIEWLANDED1 is the fix in Daoris).

### The CI between releases

`--ci [<commit>]` is the local CI (CI1, D180's CI1 note). It gates main's tip, or the named commit, with every gate but the
two rehearsals it owes a stage (below), in the same
`local/gate-tree`, holding its lock, and never touches the main checkout's tree. It runs only the gates that commit's
verdicts lack. A gate runs again when it has no verdict, when a path changed since its last pass reaches it, or when its
last verdict failed, whatever changed since. Every other gate keeps the verdict that stands, and the first run on a machine
runs every gate. The records are not forgiven here as the stage forgives them. A run keeps going past a failure, runs at
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
its imports and the tracked files, folders and globs its strings name), and each rehearsal, with the module it tests.
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
message line) and the signature `tools/test-failures.mjs` gives. Any other gate's failure is filed by its step. A failure
whose signature an open finding holds only moves that finding's last sighting. A pass of the test, or of the gate, clears
it, but only on a commit on main's first-parent line from where it was last seen failing. A sighting within a day of a
clearing restores the finding with its first sighting. A test known red for 72 hours from its first sighting is past
inheriting: the run and the watch say so, and warn twelve hours before. A death of memory is never filed.

Each run appends its **receipt** to `local/ci-runs.jsonl` as it ends: the commit, its tree, the kind, its start and end,
whether it finished, and each gate's verdict with the kind that gave it.

A plain run, and each run of the watch, owes the family and deployment rehearsals to a stage (CIFAST1, D180's CIFAST1
note). They were 14 to 23 and 14 to 22 minutes of a 45 to 75 minute run. Such a run never starts them. One without a pass
that stands is `owed` in the status (its top-level `owed`, the gate's state `owed`) and in the record, neither failed nor
passed, and the run is green on the rest. A run therefore waits about as long as the git half. `--ci --stage [<commit>]` is
the stage's path. On the commit to stage it runs both rehearsals, one after the other, with whatever else its verdicts
lack. They do not run side by side: each of the deployment rehearsal's publishes packs the CLI in place, which writes and
removes the `dist/` the family's runs of the bin would load.

A release then waits only for the last delta and the rehearsals. Once the commit to release is green, run `--ci --stage
<commit>`. When that run's status says `green` and the gate tree holds the commit, stage it: `npm run publish:desktop --
--to <install> --service --stage --from-gated <commit>`. The stage holds the gate tree's lock through its build, so no CI
run moves the tree meanwhile. It still refuses a commit the tree does not hold, a tree a run holds, and a commit whose
verdicts do not pass (`--passed`, GATE6), the rehearsals' among them. Its refusal names `--ci --stage <commit>`, which gates
that commit there again, running only what it lacks. `--full --beside` remains the run of every gate, for a release that
wants a fresh full set.

`--passed` also asks for the background run's evidence (CISCOPE1d, D207's design §5.3), so an install is staged only on a
commit the background run passed, with no waiver:

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

Record plans and findings while working, in the backlog or the matching review/design record. A
load-bearing decision belongs under `docs/decisions/`, with its rejected alternatives and limits.
A non-trivial defect needs the `fix-log` entry. Run `post-feature` over the actual diff, then move
completed task wording and a short dated outcome to `docs/task-archive.md`. The changelog describes
release-facing behavior; it does not retell a documentation audit.

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
practice in this guide and the shared brief; contracts in the router; build decisions in their dated
notes; open work in the backlog; receipts in the archive. A design's baseline code snapshot stays
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

The date gate reads committed history, so an uncommitted guide correction still has its old date.
A proposed-commit Git view may verify universal gates before committing. Keep that environment
away from fixture tests: inherited `GIT_DIR`/`GIT_WORK_TREE` redirect their repository operations.

Regenerate owned output with its producer:

```sh
node src/Daoris.Cli/bin/daoris.mjs sync
node tools/orient-index.mjs
node tools/partials.mjs --write
dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- map
```

Moving files: `node tools/move-files.mjs [--plan] from=to ... --expect-moved N --expect-rewritten N` (or `--moves file.json`)
runs `git mv`, respells every relative import of the moved files and in them, and checks that each still resolves.

Only run `map` when project metadata changed; `map --check` verifies freshness. `sync` preserves the
local brief outside the doctrine region. Canon edits also require syncing both example adopters.
`docs/index/` is checked with `--check`; a conflict in generated output is resolved by regeneration.

## Packaging, service and desktop loops

`tools/stage-package.mjs` stages the root canon, README and LICENSE into the CLI package during
`prepack`; `postpack` cleans those copies. The published entry prefers compiled `dist/cli.js`, so
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
