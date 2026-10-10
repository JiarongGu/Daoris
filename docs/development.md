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
backups and evidence still needed by the task. For a leftover worktree directory, confirm it is empty
and absent from `git worktree list`; registered managed worktrees use their owning lifecycle tool.

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

Real-process suites use `src/Daoris.Desktop/process.runsettings`; they run serially and may take
substantial time. Rehearsals also run serially, use repository scratch and record transcripts there.
Do not overlap them with another full gate run. `npm run test:web` rebuilds the HTTP host: a running
development instance can hold its assemblies. Identify the owner before stopping a process; do not
stop an installed application or an unrelated host merely to unblock a build.

`node --test` alone is not the workspace's done command. The root `npm test` delegates to the CLI
workspace; `npm run verify` also runs types, doctrine and document/tooling checks. Installed proof
is not supplied by a mocked bridge, Storybook or a browser-only end-to-end test.

## Integrate and record

`tools/merge-branch.mjs` is this repository's integration tool. It reads declared gates and lane
ownership, regenerates indexes, and records verdicts under `local/merge-branch/`. Inspect a branch's
plan before integration; use its resume/retry commands when a merge is already open:

```sh
node tools/merge-branch.mjs --plan <branch>
node tools/merge-branch.mjs --continue
node tools/merge-branch.mjs --stale
node tools/merge-branch.mjs --rerun <gate>
node tools/merge-branch.mjs --full
```

The last form runs the full set on the current checkout, including the long gates. A stage requires
that full verdict. The merge tool normally runs selected gates; a merge receipt alone does not
prove the long gates ran. Read D115 and the tool's usage before changing queue/merge behavior.

Commits are local work and follow green gates (D37). Push, publish, release, history rewrites and
destructive actions retain their human boundary. Do not bypass provenance checks or hooks to make
a task look complete. Subagent branches, when authorized, follow
`.claude/skills/dispatch-subagent/SKILL.md`; documentation work does not implicitly authorize dispatch.

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
dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- map
```

Only run `map` when project metadata changed; `map --check` verifies freshness. `sync` preserves the
local brief outside the doctrine region. Canon edits also require syncing both example adopters.
`docs/index/` is checked with `--check`; a conflict in generated output is resolved by regeneration.

## Packaging, service and desktop loops

`tools/stage-package.mjs` stages the root canon, README and LICENSE into the CLI package during
`prepack`; `postpack` cleans those copies. The published entry prefers compiled `dist/cli.js`, so
a source checkout with old `dist/` can execute that build. Tests and the release rehearsal cover the
source/package distinction. `DAORIS_CANON` selects a fixture canon for offline doctrine tests.

`npm run knowledge:build` prepares the repository's read-only knowledge host. `.mcp.json` starts
that built host through `tools/knowledge-server.mjs`; it does not build at session startup. Its
development home is isolated from the install. With no build, its handshake explains the absence
and offers no tools. See the [service guide](../src/Daoris.Service/README.md) for production wiring.

`npm run desktop -- doctor|build|run|shot|eval|click|restart|kill` is an instrumented development loop,
not a gate. The [desktop guide](../src/Daoris.Desktop/README.md) explains scratch versus installed
operation, window selection and process ownership. A screenshot must select the application window
being reviewed; the browser window is a separate target.

There is no push/PR CI. [The release workflow](../.github/workflows/release.yml) is manually
dispatched with dry-run as its default. It runs gates across its platform jobs, packages artifacts
and owns version/release stamping. A check not run locally has no push-triggered receipt to rely on.
