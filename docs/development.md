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

Real-process suites use `src/Daoris.Desktop/process.runsettings`; they run serially and may take
substantial time. The driver's git-only Process classes, marked `Parallel=Git` beside `Category=Process`,
run four at once in one host with `src/Daoris.Desktop/git.process.runsettings` (gate `driver-process-git`,
PROC1c): four at once is load, so that half runs after the serial one, never beside it, and a class
marked so is run alone with that file, not the other, whose filter leaves it out. Rehearsals also run
serially, use repository scratch and record transcripts there; the merge tool runs only the family rehearsal beside the
git half, which builds the driver host into its own output (REHLANE1). Do not overlap them with another full gate run.
`npm run test:web` runs `test:web:unit` (the vitests) then `test:web:e2e`, which rebuilds the HTTP host: a running
development instance can hold its assemblies. Identify the owner before stopping a process; do not
stop an installed application or an unrelated host merely to unblock a build.

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
```

The last form runs the full set on the current checkout, including the long gates. The gates that
start no real process run beside the serial Process lane (GATELANES1), up to three at once but never
two that build one .NET component (GATELANES2), and a Process half starts only
once the heavy light gates (the CLI, .NET and web suites) have ended, so its timing tests run with no
suite loading the CPU (GATEORDER1). A Process half runs `--no-build` when its fast half passed in the
same run on the same tree (GATEBUILD1). The family rehearsal runs beside the git half, and every other
Process-lane gate alone (REHLANE1); the web gate's vitests are a light gate of their own, `web-unit`, beside the
service suite (WEBSPLIT1). Add `--one-at-a-time` on a
loaded machine to run every gate in turn. A stage requires that full verdict. The merge tool normally runs selected gates; a merge receipt alone does not
prove the long gates ran. Read D115 and the tool's usage before changing queue/merge behavior.

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
