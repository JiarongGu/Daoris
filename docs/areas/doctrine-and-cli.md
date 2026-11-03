# Doctrine and the CLI — how it works now

## What it does

The canon is a set of rules, knowledge and skills that every repository takes up. `daoris` is the one command a person
types. The npm package `daoris` (the CLI) owns nine offline doctrine commands: `analyze`, `init`, `sync`, `check`,
`upstream`, `index`, `status`, `reconcile` and `doctor`. `sync` writes the canon into a repository and records each file's
hash in `daoris.lock`. `check` fails a build when a copy drifted from the lock. `status` and `reconcile` say what a newer
canon would change, and the repository's own session adopts, keeps or proposes each item. Every other `daoris` word is
handed to the desktop's program. Set-up of a repository is a quest that the driver's set-up press publishes to that
repository's own session. The devkit is a .NET binary that runs the universal gates and delegates doctrine to `daoris check`.
The lean canon (CONSOLIDATE1) is on main as of the merge `d2db27eb8`: three rules in full, five rules read on demand,
one `change` skill, and `co-working` as a pack.

## Its parts

| Unit | File | Its job |
|---|---|---|
| Dispatcher | `src/Daoris.Cli/src/cli.ts` | The doctrine rows' table, the usage, exit codes; hands every other word to the desktop's program |
| Command rows | `src/Daoris.Cli/src/cli/*.ts` | One module per doctrine verb: its name, usage lines and handler |
| Hand-off seam | `src/Daoris.Cli/src/platform/driverprogram.ts` | Finds the desktop's program and starts it, the CLI's one spawn, marked `DAORIS_HANDED=cli` |
| Canon reader | `src/Daoris.Cli/src/doctrine/canon.ts` | The canon root, packs and their API version, the selection with switches, the canon digest |
| Manifest and lock | `src/Daoris.Cli/src/doctrine/config.ts`, `records.ts` | `daoris.json` and `daoris.lock`: shapes, defaults, reading, writing |
| Sync | `src/Daoris.Cli/src/doctrine/materialize.ts` | `planSync` (the lock × disk × canon table), `planChanges`, `applySync`, the refusals `--force` cannot pass |
| Check | `src/Daoris.Cli/src/doctrine/drift.ts` | `inspect` and `check`: offline, with no canon read |
| Status and init | `src/Daoris.Cli/src/doctrine/commands.ts` | `status` (the update check), `init` (the manifest and the line-ending pin), the repository's own files |
| Keep | `src/Daoris.Cli/src/doctrine/keep.ts` | A kept file's state and its canon file digest |
| Reconcile | `src/Daoris.Cli/src/doctrine/reconcile.ts` | The offline brief of every difference from the canon |
| Upstream | `src/Daoris.Cli/src/doctrine/upstream.ts` | Writes into the canon in its own checkout; elsewhere composes a request's words |
| Newer lock, notes | `src/Daoris.Cli/src/doctrine/lockversion.ts`, `notes.ts` | Refuses a lock a newer canon wrote; the canon changelog entries between two versions |
| Region | `src/Daoris.Cli/src/doctrine/region.ts` | The marked region in `AGENTS.md` and the import region in `CLAUDE.md`: find, write, remove, refuse damage |
| Region text and index | `src/Daoris.Cli/src/doctrine/tierrender.ts`, `indexgen.ts` | The rules table, the on-demand rows, *Where things are*, and `<target>/INDEX.md` |
| Documents | `src/Daoris.Cli/src/layout/documents.ts` | The roles a manifest may bind in `documents`, and their checks |
| Layouts | `src/Daoris.Cli/src/layout/harness.ts`, `layout.ts`, `mirror.ts`, `rooms.ts`, `links.ts` | The `claude-code` and `agents` descriptors, the lock's layout, the skills mirror, rooms, links |
| Adoption reads | `src/Daoris.Cli/src/doctrine/analyze.ts`, `twins.ts`, `lineendings.ts` | `analyze` (what adopting would do), `doctor` (look-alike documents), the LF pin |
| The canon | `canon/core/`, `canon/packs/*/`, `canon/canon.json`, `canon/CHANGELOG.md` | The doctrine itself, its version and why each version changed |
| Command tree | `Daoris.Desktop.Driver/Platform/CommandWords.cs`, `Loop/CommandTree.cs` | Every group and verb of `daoris`, and the carried CLI's path |
| Doctrine hand-back | `Daoris.Desktop.Driver.Host/DoctrineConsole.cs` | Runs a doctrine word through the CLI an install carries, marked `DAORIS_HANDED=driver` |
| Set-up press | `Driver/Setup/SetupPress.cs`, `Setup/Domain/SetupPressRules.cs`, `SetupBrief.cs` | Which set-up a line calls for, its refusals, its quest and the exact-verb rule |
| Place and workspace plan | `Driver/Setup/Place*.cs`, `WorkspaceSetup*.cs`, `Domain/WorkspaceSetupRules.cs`, `SetupOrder.cs` | `daoris place`: a place's set-up, and a workspace's paced adoption |
| Doctrine status read | `Driver/Setup/DoctrineStatusRead.cs`, `Domain/DoctrineStatus.cs`, `ReconcileBrief.cs` | Reads `daoris status --json` in a checkout; the reconcile quest's words |
| Devkit | `src/Daoris.Devkit/Daoris.Devkit.Core/` (`GateRunner`, `*Gate.cs`, `GateDeclaration`, `VersionPin`), `Daoris.Devkit.Cli/Program.cs` | `daoris-devkit verify`, `scan`, `init`, `install-hooks`, `map` |

## Its rules

**The canon**

1. The canon lives at the workspace root and ships inside the npm package. A manifest's `source` (`daoris@0.0.1`) is
   provenance and the command to re-run, never fetched. `DAORIS_CANON` overrides the root; an installed package reads only
   its own copy. (from D10, D11, D20)
2. Three layers: core installs everywhere; packs are named in `daoris.json`'s `packs`; the repository's own documents are
   never synced or touched. (from D4)
3. The tier is the location, and there is no tier field. A `rules/` document renders into the region in full. A
   `knowledge/` document marked `rule: true` is a rule read on demand: a linked row in the region, its body in its file.
   `knowledge/` and `skills/` are folders under the target. (from D7, D59, CONSOLIDATE1)
4. Core today holds three rules (`file-tool-discipline`, `repository-owns-its-work`, `sensitive-info`), five on-demand
   rules, and the skills `caveman`, `change` and `set-up-documents`. Every change goes through `change`. (from CONSOLIDATE1)
5. A canon document carries `name` (its filename), `applies_when` and `enforces`; a skill carries `name` and `description`
   and ships its whole folder. A document missing frontmatter is listed and marked, never dropped. (from D14, D128)
6. Canon content is project-agnostic. It names no path, build command, private name or skill roster; a skill sends the
   reader to the generated index for repository specifics. (from D14)
7. A concern only some repositories have is a pack, never core. A pack is `canon/packs/<name>/pack.json` with a
   `description` and an `apiVersion`; one needing a newer API than the build speaks (`PACK_API` = 1) is refused before
   anything is planned. (from D61, PLUG1)
8. A pack may offer to switch a core row off, with a reason (`switchesOff`). Only the manifest's `switchedOff` confirms it.
   It is never silent: `sync` names it, `check` prints it every run, `status` gives the reason, the region lists it. (from D71)
9. The canon's version is `canon/canon.json` (0.0.1 through every 0.0.x build), stamped by the release. Each change is
   written under `## Unreleased` in `canon/CHANGELOG.md`; `status` prints the entries between the locked and carried
   versions. (from D11)

**Sync, the lock and drift**

10. Daoris reads and writes only paths the lock records. Anything else, even in the same folder, is the repository's.
    (from D5)
11. Every write and delete resolves inside the repository. An escaping path aborts the whole apply before the first write.
    (from D18)
12. Every file is a real copy. A link, or a link held as text, at a path `sync` writes is refused and never written
    through, `--force` included. (from D3, D117)
13. What `sync` does is a function of canon × lock × disk. Disk against lock is the only drift. Disk against canon is an
    update. A file not in the lock at a canonical path with other content is a collision. (from D12, D13, D19)
14. Drift, a collision and an edited retirement refuse without `--force`. A byte-identical file not in the lock is adopted
    silently. A retired file nobody edited is deleted. (from D19)
15. Switched-off and kept files have cells of their own. A kept file is passed over, `--force` included. A core file kept
    with no text is refused, `--force` included. (from D19, D71, D230)
16. `--force` discards an edit and nothing else. It never passes a link, a move that would leave the repository's own
    documents unlisted, a room with no text, a declared document behind a link, an absent kept core file, or a move
    carrying a kept file (`materialize.ts:540-593`). (from D117, D230)
17. Files are written as BOM-less UTF-8 with LF, beside the target and renamed over it. Comparisons normalise line
    endings on read. `init` pins `* text=auto eol=lf` where the repository says nothing. (from D25)
18. Each written file opens with a provenance header, and each span with a provenance comment. The hash, not the header,
    detects an edit. (from D6)
19. The lock records each file's hash, the canon version and digest, the layout, the switched-off rows and the mirrors.
    The lock, not the manifest, says where files are: a move reads and deletes at the lock's root and writes at the
    manifest's. (from D2, D117)
20. `check` is offline and reads no canon. Drift, a missing file, a stale pack, a stale index or region, and a switch the
    manifest and lock disagree on fail with exit 1. A test deletes the canon and requires a clean exit. (from D8, D71)
21. The always-loaded budget (`coreBudgetBytes`, default 30000, 26000 here) measures the region's bytes. It reports and
    never fails. Kept rows are reported on every run. (from D28, D54, D230)

**Status, the update check and the digest**

22. `status` may read the canon. It reports what a sync would change, add or retire whenever the version or the canon
    digest differs, whatever the stamps say. `--json` gives the same facts to an agent. (from D230)
23. The canon digest hashes every pack file by path and content. It tells two canons of one version apart and never
    orders them. An older lock version reads `newer`; equal versions with different digests read `unknown`, and
    `sync` is not offered for it. (from D124, D230)
24. An older tool never rewrites a newer lock. `sync`, `upstream` and `reconcile` refuse a lock whose canon version is
    newer (exit 1), naming `npx daoris@<locked>`. (from D124)

**The reconcile and the keep**

25. `reconcile` composes an offline brief of every difference: changed, added, retired, edited copies, reopened keeps and
    set-up gaps. Each item names the canon's text and the keep line. It writes nothing. (from D230)
26. The repository's own session decides each item on its own branch: adopt it (`sync`), keep it (`daoris.json`'s
    `kept`, with a reason and the canon file's digest), or propose it upstream. (from D230)
27. A keep reopens when its canon file's digest moves. A keep whose file the canon no longer ships is dropped. A keep is
    a text, never a switch: only a confirmed pack switch removes a core row. (from D4, D71, D230)

**`upstream`**

28. `upstream <file>` or `--all` writes into `canon/` only inside the canon's own checkout. Elsewhere it prints the
    request's words, each file's body under the canon's frontmatter, and writes nothing across. (from D9, D32, D230)

**The region and the agent layout**

29. The always-loaded tier is the `daoris:rules` region of `AGENTS.md`. `CLAUDE.md` holds a `daoris:import` region with
    `@AGENTS.md`. Daoris owns only the regions; a damaged marker, a reordered pair, a second region or a root file that is
    a link is refused, never guessed. (from D59, D117)
30. The region holds a header line, the `change` line when that skill is installed, the rules table (in-file rules named,
    on-demand rules linked), the switched-off rows, the pointer to `<target>/INDEX.md`, rooms and the mirror line only
    when there are any, and *Where things are*; then each rules-tier body in full. (from D59, D71, D128, CONSOLIDATE1)
31. The knowledge and skill tables live in `<target>/INDEX.md`, which `sync` writes, the lock names and `check` keeps
    fresh. A knowledge document with no frontmatter is listed by its first heading, and counted once. (from D128)
32. Two layouts, chosen by `harness`: `claude-code` (the default; knowledge and skills under `.claude/`) and `agents`
    (under `.agents/`, with a `.claude/skills/` mirror the lock measures like any copy). Other harnesses are detected and
    reported, never generated; an unknown name is a tool error. (from D23, D117)
33. A room is a folder with its own `AGENTS.md`, declared in `rooms`. `sync` keeps its `CLAUDE.md` pointer and never writes
    its text; a declared room with no text fails. (from D117)
34. A repository binds roles to paths in `documents`. The brief and a room take only a word ceiling. `check` fails on a fact
    (missing, escaping, a link, a stale table) and reports a judgement (a ceiling passed). (from D122)
35. This repository uses `claude-code` at `.claude` with the packs `co-working` and `localized-ui`. A canon change is synced
    here and in `examples/engine` and `examples/game`; a generated copy is never patched. (from D39, D59)

**The command tree and the hand-off**

36. `daoris` is the one name. The CLI owns the doctrine commands; every other word goes whole to the desktop's program,
    the one `DAORIS_DRIVER_PROGRAM` names (an existing apphost) or the install's `app/daoris-driver/`, never `PATH`. With
    neither it says where the program is, exit 2, and does nothing. (from D194)
37. `DAORIS_HANDED` keeps a word from being handed back: the CLI refuses a non-doctrine word the program handed, and the
    program refuses a doctrine word the CLI handed. An install's `app/bin/daoris` runs the program, which runs a doctrine
    word through the CLI in `app/cli/` on the tools' Node.js (22 or later). (from D124, D194)
38. No CLI module touches the network. The one spawn is the seam's `startChild`, and nothing a doctrine command reaches can
    spawn. The package has no runtime dependencies. (from D8, D33)
39. Exit codes are 0 clean, 1 policy failure, 2 tool error. A failure nobody anticipated is 2, said in one line.
    (from D8)
40. The groups and verbs are declared in `CommandWords.cs` and the driver tests' `fixtures/command-tree.json`; the CLI's
    rows are held to the same file. A management verb is a driver group, never a CLI row. No word is kept for an older name:
    an old form is no command. (from D194, D232)

**The devkit's universal gates**

41. `daoris-devkit verify` runs five universal gates (`sensitive`, `version`, `docs`, `links`, `doctrine`), then the
    declared ones, and stops at the first failure. A skipped gate says skipped; a disabled gate is printed every run.
    (from D20)
42. Gates are declared in `daoris.gates.json`, which the CLI never reads. Its `devkit` field pins the toolkit version,
    checked before any gate runs. (from D26, D27)
43. The `doctrine` gate runs `daoris check` and nothing else. (from D19, D20)
44. The binary is acquired explicitly, with its published sha256, and never downloaded implicitly. The binary does not
    verify itself; whoever installs it does. (from D27)

**Set-up of a repository**

45. A set-up is the repository's own session's work, on its branch, landed by the workspace's rule. The press reads the
    line, publishes one quest and adds a rule; Daoris runs no doctrine command into the repository. (from D32, D117, D124)
46. The line decides the case: not adopted is the whole set-up; an older layout or an unfinished move is a move; clean and
    declaring nothing is the declaration; no index is the index quest; otherwise done. A reconcile is asked for, never
    found. (from D117, D124, D151, D230)
47. The whole set-up runs `init --harness agents`, `analyze`, `sync`, then writes the domain, the knowledge a neighbour
    needs, the brief, the documents, the safe work and the checks. It never pushes, publishes, runs `connect` or
    `upstream`, or changes code. (from D124)
48. The rule is exact `daoris` verbs as `Bash(…)`, never a runner: `--version`, `init --harness agents`, `analyze --json`,
    `sync` (plain, `--dry-run`, `--force`), `check`, `status --json`, `doctor`. A reconcile adds `reconcile` and
    `upstream --all`. (from D122, D124, D230)
49. A child's `node` older than 22, or a `daoris` that answers no version, is the machine's refusal, and pauses a workspace
    plan. (from D124)
50. A set-up keeps the checks green: it runs them before its first change and at its close, moves only skills, declares
    knowledge where it stands, and adds no frontmatter to an existing document. (from D128)
51. A workspace plan opens one set-up at a time by default, never the cap's last slot, the most-touched repositories
    first, pausing after a pilot of two. It is `daoris place --workspace <w> --adoption`, kept in `<home>/setup/<w>.json`.
    (from D124)
52. The driver registers a repository from `daoris.json` and `daoris.lanes.json` read on its line, after a line moves, at
    start, and on `daoris repository register`. (from D34, D124)
53. A place page offers the reconcile where `daoris status --json` says the canon moved or a keep reopened. The press is
    the person's (`RECONCILE_PLAN`, `RECONCILE_APPLY`). (from D230)

## Where a change goes

**Which subdomain a new behaviour belongs in**

- New doctrine: `canon/`. Core only if nearly every task needs it; a long rule goes to `knowledge/` with `rule: true`; a
  concern only some repositories have is a pack. Read `.claude/knowledge/canon-authoring.md`, add a `canon/CHANGELOG.md`
  entry, then `daoris sync` here and in both examples.
- A new sync cell: `planSync` in `materialize.ts`. Write the cell into rule 13 to 16 above before building it.
- The region's shape: `tierrender.ts` (text) and `region.ts` (markers).
- The update check, keep or reconcile: `commands.ts` (`commandStatus`), `keep.ts`, `reconcile.ts`. A change to the
  `status --json` shape also changes the driver's `Setup/Domain/DoctrineStatus.cs`.
- A new doctrine command: a module under `src/Daoris.Cli/src/cli/`, its row in `COMMANDS`, its lines in
  `test/fixtures/cli-usage/help.txt`, and its name in `CommandWords.Doctrine` and `fixtures/command-tree.json`.
- A management verb: a driver group in `CommandWords.cs` and a command in the driver library. Never the CLI.
- A set-up behaviour: the pure rules in `Setup/Domain/` first, then `SetupPress.cs` or `WorkspaceSetup.cs`.
- A gate every repository runs: a new gate type in `Daoris.Devkit.Core`, registered in `GateRunner`.

**The tests that pin the area**

- CLI: `test/doctrine/` (`sync`, `check`, `switchoff`, `keep`, `reconcile`, `update-check`, `newer-lock`, `upstream`,
  `upstream-request`, `region`, `tierrender`, `indexgen`, `packapi`); `test/layout/` (`layout-move`, `layout-mirror`,
  `layout-rooms`, `layout-links`, `documents-*`); `test/cli/` (`cli`, `cli-usage`, `cli-tree`, `containment`);
  `test/platform/driverprogram.test.ts`.
- `test/dogfood.test.ts`: canon frontmatter, no private names in the canon, this repository checks clean, no CLI module on
  the network, one spawn, `check` with no canon. `test/structure.test.ts` holds the CLI's module structure.
- Driver: `Daoris.Desktop.Driver.Tests/Setup/` (`SetupPressTests`, `SetupBriefTests`, `SetupQuestsTests`,
  `WorkspaceSetup*Tests`, `PlacePlanTests`, `PlaceApplyTests`, `DoctrineStatusTests`) and `fixtures/command-tree.json`.
- Devkit: `src/Daoris.Devkit/Daoris.Devkit.Tests/`.

**The gates that bite**

- `cli` (`npm run verify`): typecheck, the CLI tests, `daoris check` on this repository, the document tools, the size
  ratchet, the transitional markers, and the devkit's universal gates.
- `devkit`, `driver` and `tool-tests` for their halves.
- The merge tool adds by path: `canon/**` reaches `service`, `rehearse` and `rehearse-family` (both examples must be
  current); CLI sources reach `driver`, `rehearse`, `rehearse-family`, `web` and `deployment`.

## Contradictions

1. D59 (rejects naming rules instead of carrying them) and D128 point 4 (every rule in full) vs `tierrender.ts:17,202`
   (five rules are linked rows read on demand): the code reads right.
2. D128 point 4 and its KNOW2 note (`doc-loader` reads `INDEX.md`) vs `canon/core/skills/` (no `doc-loader`; `change` step 1
   reads the area): the code reads right.
3. D230 point 2 (the lock's canon digest orders the canons) vs `canon.ts:141-145` and `commands.ts:168-172` (the digest
   tells two canons apart and never orders them): the code reads right.
4. D35 (`connect` is the CLI's one online command) vs `dogfood.test.ts:659` (no CLI module touches the network) and
   `CommandWords.cs:72,117` (`connect` is the driver's `repository connect`): the code reads right.
5. D194 point 1 (`daoris driver|agent|plugin|tool` families) vs its own 2026-11-03 note and `CommandWords.cs:41-89` (one tree
   of groups, no `driver` group): the note and the code read right.
6. D117 point 7 and D124 point 5 (`daoris-driver setup [--workspace]`) vs `CommandWords.cs:63` and
   `WorkspaceSetupCommand.cs:24` (`daoris place … --adoption`): the code reads right.
7. `CommandWords.cs` (one name, `daoris`) vs about a hundred doc comments and the older `CHANGELOG.md` entries that still
   spell `daoris-driver …` or `daoris driver …`: the code reads right. `app/daoris-driver/` is the program's folder, kept.
8. D27 (the devkit verifies itself against the hash) vs `src/Daoris.Devkit/README.md:255` (deliberately not built; the
   installer verifies): the README reads right.
9. `src/Daoris.Devkit/README.md:264` (the CLI's service client) and `AGENTS.md:23` (a service seam) vs `dogfood.test.ts:659`
   (no network in the CLI): the test reads right.
10. D9 (`upstream` promotes into the canon) vs `upstream.ts:256-264` (writes only inside the canon's checkout): D230 and the
    code read right.
11. D23 (one harness is supported) vs `layout/harness.ts:27-110` (two descriptors, `agents` serving several harnesses): D117
    and the code read right.
12. D28's amendment (Daoris declares no packs) vs `daoris.json:3` (`co-working`, `localized-ui`): the manifest reads right.
13. D122 point 1 (twelve roles) vs `canon/core/knowledge/development-documents.md:44-59` (adds `area` and `index`): the
    canon reads right.
14. D194's ONEWAY1ai-i note (the CLI starts on the node `PATH` finds) vs `DoctrineConsole.cs:67-78` (the tools' resolved node
    where a home exists, `PATH` only without one): the code reads right.

## Open

- **LAYOUT5**: move this repository to the `agents` layout; it is still `claude-code` at `.claude` (`daoris.json:4`).
- **LAYOUT6**: the brief and rooms, after LAYOUT5.
- **LAYOUT2**: measure the layout's unmeasured cells; named only in a sequence line (`TASKS.md:293`), with no row.
- **DOCSHOME1**: managed documents and skills in two portable homes, with the round trip through both.
- **WSSETUP7**: Ask Daoris's `setup` kind's `register` and workspace doors; named only in `TASKS.md:284`, with no row.
- **WSSETUP14b–e**: the set-up pilot's remaining parts; named only in `TASKS.md:284`, with no row.
- **CONSOLIDATE1**: the lean canon is merged; the area documents under `docs/areas/`, the decisions as history and the
  status table are not built yet.
- No row: a `PowerShell(…)` rule for a set-up waits on the canary that a driven Windows session uses that tool, and codex
  and dsh get no safe-work rules until measured (`SetupPress.cs` remarks; D122 point 6).
- No row: the newer-lock refusal cannot fire until a release moves the canon version past 0.0.1 (`lockversion.ts:30-31`).
