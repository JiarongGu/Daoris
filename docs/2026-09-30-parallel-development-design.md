# Parallel development — where branches collide, and the shape that stops it

> The owner, 2026-09-30: *"since daoris is getting more and more complex we should modulize this
> project properly so that paralle development with subagent can run smoothly"*. The contract for the
> MOD rows in `TASKS.md`. Status: **MOD1–MOD9 built** (D106 for MOD1). *D115 (DEV1,
> `docs/2026-10-01-self-development-design.md`) designs what the driver makes of this: §5's map moves to
> `daoris.lanes.json` as the repository's own declaration, and §3's rules 6 and 7 become the driver's queue
> and a steward's lane. DEV2 has moved the map, with ids and the steward's `records` lane (§5). GATE3 and
> GATE4 narrow the merge tool's gates by lane (§3). Until the other rows land, everything else here stands as
> written.*

## 1. What was measured

Eighteen branches were merged into main in the three days to 2026-09-30, most of them built by
subagents in their own worktrees and merged by the parent. The files those merges touched, counted per
merge:

| Touched by | File | Lines | Kind |
|---|---|---|---|
| 17 of 18 | `TASKS.md`, `docs/task-archive.md` | — | shared record (the parent's alone) |
| 12 | `CHANGELOG.md` | — | shared record, append-only |
| 11 each | `src/Daoris.Web/src/locales/{en,zh}.json` | 1,529 each | one file for every string |
| 10 | `src/Daoris.Web/src/shell.ts` | 1,987 | the bridge's every type and call |
| 10 | `docs/README.md` | — | shared record, one row per document |
| 9 | `.claude/knowledge/twins.md` | — | shared record, one row per twin |
| 8 | `src/Daoris.Web/src/SettingsView.tsx` | 1,870 | every Settings domain in one file |
| 8 | `src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.cs` | 2,395 | every driver route in one switch |
| 8 | `src/Daoris.Desktop/README.md` | — | one table row lists the whole driver surface |
| 7 | `src/Daoris.Desktop/Daoris.Desktop.Driver/Help.cs` | — | the room: every door, every section |
| 6 | `docs/DECISIONS.md` | — | append-only, and **two branches took the same number** |
| 4–6 | `shell.test.tsx`, `HelpRoomTests.cs`, `HelpProposals{,Tests}.cs`, `cli.ts`, `CLAUDE.md` | | |

Every real conflict that day was in that table: `Plugins.cs`, `twins.md` and `docs/README.md` between
PLUG8 and PLUG9, and decision numbers between WSR5 and PLUG8. Two more costs were not conflicts:
- **Load made flakes.** Real-process tests failed when three worktrees built at once, and passed alone
  (FLAKE1, five classes, and a rehearsal that exited 127).
- **Noise.** One driver test fails in every worktree because of Windows' path length, and every hand-back
  had to say so.

## 2. Two kinds of collision, two answers

**A shared record** is a file every change appends to: a changelog line, a document's row, a
decision. Nothing about the work collides; only the insertion point does. The answer is to stop
sharing the insertion point, never to serialise the work.

**A god file** is one file that every feature in an area has to edit, because the area's
registrations live in it: a route switch, a type file, one catalogue, one screen holding ten domains.
Here the answer is a **registry with one file per entry**. A feature adds its own file and registers
it where registration is by convention, or in a table whose rows do not neighbour each other. The
central file then stops changing.

Neither answer changes what the product does. Every split below is behaviour-preserving, and the gates
that hold today's behaviour are what prove it.

## 3. The rules for work in parallel (the process half)

1. **Lanes.** The parent dispatches concurrent branches into lanes that do not share files. §5 is the
   lane map. A branch that must cross lanes waits for the other to merge, or is split.
2. **The parent owns the shared records.** Subagents never edit `TASKS.md` or the archive (already so).
   Decision numbers are **reserved at dispatch**: the prompt names the number, so two branches never
   take one.
3. **Append-only records merge by union.** `.gitattributes` marks `CHANGELOG.md`,
   `docs/task-archive.md`, `docs/FIX-LOG.md`, `docs/DECISIONS.md`, `docs/README.md` and
   `.claude/knowledge/twins.md` `merge=union`: two branches adding rows at one place keep both. Union
   also keeps both versions of a line two branches *changed*, so the parent still reads the merged diff
   (it always does), and a docs check refuses a duplicated table row or heading.
4. **No moving counters in always-read files.** `CLAUDE.md`'s *D1–D102* and similar ranges change with
   every decision and conflict for nothing. They say *the decision log* instead. Test counts keep their
   one home in `TASKS.md`'s State.
5. **Fast suites in the worktree, process suites in the parent.** Tests that start real processes or run
   real ticks carry a category (`Process`) and run serially in the parent's merge. A subagent runs the
   rest. FLAKE1's classes are exactly the ones that fail under another worktree's load. Built (MOD8):
   the trait is on the class, each desktop suite is two declared gates (`--filter Category!=Process`,
   and `--settings src/Daoris.Desktop/process.runsettings`, serial), and a dogfood test holds the pair.
6. **One merge tool.** `tools/merge-branch.mjs` merges `--no-ff --no-commit`, stops on a conflict it
   cannot resolve and names the files, then runs the declared gates in order, fast first. It keeps
   every gate's whole output under `local/scratch/`. This replaces the hand-typed chains, one of which
   lost a rehearsal's reason.
7. **A dispatch skill.** The brief every subagent got today (rules to read, gates it may run and may not,
   files it must not touch, what the hand-back carries) becomes a repository-local skill. A prompt then
   says only the task, its lane and its reserved decision number.

**Rules 6 and 7 are built (MOD9).** `tools/merge-branch.mjs` reads its plan from `daoris.gates.json`
plus the `npm run` steps the release workflow adds (the release and family rehearsals), so there is no
second list. It orders gates by kind: the devkit's checks, then the suites, then the rehearsals. The
deployment rehearsal runs last, after `dotnet build-server shutdown`. `--plan` shows the order and
merges nothing. Before merging, the tool prints which lanes of `daoris.lanes.json` (§5, held to this
table by a test) the branch touched, by id and title, and never refuses on a lane. A commit check
refuses a commit without its `Co-Authored-By:` line and work left uncommitted in the branch's worktree. When a
`dotnet test` gate of the `Process` half fails and names every failure, each failed test is re-run
alone once; if all pass, the gate reads FLAKE and the summary counts it. A rehearsal that died is run
again once, whole (LEFT1): one whose exit is a process ending (a shell's 127, a signal, a Windows crash
status) or that printed nothing of its own, as the family rehearsal once exited 127 with no transcript
while three worktrees built. If the second run passes it reads FLAKE. A rehearsal that reported failed
checks, or said why it stopped, has failed and is not run again. `--batch` is one merge at a time: git will
not merge over an open merge and the tool never commits, so the parent commits each merge and
`--continue` merges the next. The rehearsals run once, after the last. At each merge's start and a
batch's end the tool prunes (GATE2): a branch merged into main goes with its worktree, unless the
worktree is locked (an agent runs in it), holds changes or untracked files, or has anything under
`local/`; it never forces and never uses `-D`. A worktree made by hand is locked by whoever made it. The brief is the local skill
`dispatch-subagent`.

**GATE3 and GATE4 (2026-10-04) narrow rule 6.** A merge runs the baseline (the universal gates, the code map,
`verify`) and the gates its changed paths can reach, by the tool's lane table (`REACH`), and says why each runs or
is skipped. A path the table does not place runs every gate, and `--full` runs the plan. Tests hold the table to
every repository path a .NET suite reads and to everything a rehearsal imports or starts. Each verdict is kept
with the tree it ran on, and `publish:desktop` refuses a tree the full set has not passed, as `--passed` reads
it (`--full` alone runs the plan on the checkout), so a wrong table is caught before the install is built.
`--rerun <gate>…` re-runs a fixed gate on the merge in place and keeps the other verdicts, saying from when. A
Process half leaves a trx, and the ten slowest classes are printed after it (PROC1). D115's note says what this
amends. **GATE5** then took the long gates out of every merge, reached or not: the two `Process` halves and the
deployment rehearsal run only with `--full`, so rule 5's process suites run in the full set before staging.
**GATE6** then let a verdict stand until a path its gate reaches changes: `--passed` and `--rerun` read the paths
changed since a verdict against the same lane table, not the whole tree, so a fix re-gated by the gates it reaches
leaves the others' verdicts standing, and a verdict that no longer stands is named with the path that made it so.
**GATE6b** then opened `--rerun` on the checkout with no merge open: it runs the named gates and every stale one, and
`--stale` runs the stale ones alone, each verdict recorded where the stage reads it, on a clean tree only. A refused
stage names the smallest of `--stale`, `--rerun <gate>…` and `--full` that would pass the tree, so one flaked gate or
one fix committed after the full set no longer costs a second full run.
**GATE1 (2026-10-07)** made rule 6's gates judge the commit the merge would make. They run before it is committed,
and the devkit's docs gate dates paths from HEAD's history, so TOOL4e's merge passed and failed `verify` once
committed. The universal gates, in their gate and in `verify`, now run through `tools/as-merged.mjs`, where HEAD is
that commit; outside a merge it changes nothing. `docs/FIX-LOG.md` keeps the incident.

## 4. The splits (the code half)

In the order that removes the most collisions per unit of risk:

| Row | Split | From → to |
|---|---|---|
| MOD2 | **Catalogues by area** | `locales/{en,zh}.json` → `locales/{en,zh}/<area>.json` (settings, plugins, help, quests, work, sessions, agents, …), merged at load. Keys unchanged; the i18n check holds parity per area file, both directions. |
| MOD3 | **The bridge by domain** | `shell.ts` → `bridge/<domain>.ts` (plugins, help, trees, sessions, agents, settings, logs, …), with `shell.ts` left as the barrel so no import moves in the same change. |
| MOD4 | **Settings by domain** | `SettingsView.tsx` → the frame and the domain list, and one `settings/<Domain>.tsx` per domain; the domain list is the registry. |
| MOD5 | **The driver module by domain** | `DriverModule.cs` → a partial class per domain (`DriverModule.Plugins.cs`, `.Help.cs`, `.Trees.cs`, …), and a route table each partial adds to, replacing the one switch. |
| MOD6 | **The room and the proposals by feature** | `Help.cs`'s doors and sections, and `HelpProposals.cs`'s kinds, become entries: a section or a door row per file, and a judge per kind behind one interface. PLUG9 and WSR5 already put two kinds in files of their own; the rest follow. The service twin mirrors it. |
| MOD7 | **The CLI's commands as a table** | `cli.ts`'s dispatch and usage text → one module per command registering its verb and its usage lines. |
| MOD8 | **Tests follow their code** | `shell.test.tsx`, `HelpRoomTests.cs`, `HelpProposalsTests.cs` split the same way; the `Process` category; the worktree path-length test fixed at its cause. |
| MOD9 | **The records and the tools** | §3's union attributes, the counters, the duplicate-row check, `merge-branch.mjs` and the dispatch skill. Parts are small and done first (see §6). |

Deferred rather than rejected: `Harnesses.cs` (2,013), `Quests.cs` (1,708), `toolchain.ts` (1,654),
`Acp.cs`, and `tools/family-rehearsal.mjs` (3,771, phases into files). They are large, but few branches
touched them. Split one when two branches next meet in it.

## 5. The lane map

| Lane | Title | Owns |
|---|---|---|
| `web-shell` | Web shell | `src/Daoris.Web/` but its Settings: `shell.ts`, `bridge/`, `App.tsx`, `ui.tsx`, the work frame, the other screens and the modules they share, and the page's build |
| `web-settings` | Web settings | `SettingsView.tsx`, `settings/`, and their catalogue areas |
| `driver` | Driver library | `Daoris.Desktop.Driver/` by feature folder: loop and planner, trees and landing, plugins and hooks, help, toolchain |
| `modules` | Desktop modules | `Daoris.Desktop.Modules/` by partial, `Daoris.Desktop.App/`, the launcher, and the desktop tree's package versions |
| `service` | Service | `src/Daoris.Service/` |
| `cli` | CLI | `src/Daoris.Cli/` |
| `tools` | Tools | `tools/`, `.gitattributes`, the docs gates, the devkit, the gates' declaration and the release workflow |
| `records` | Records | The steward's lane: `TASKS.md`, `docs/task-archive.md` and the lane map itself. A subagent never edits it; the parent keeps it as the steward |

A feature usually crosses two or three lanes (a driver door, its module route, its screen). After the
splits, crossing a lane means adding a file in it, not editing that lane's god file, so two features
can cross the same lanes at once.

The map is the repository's own declaration, `daoris.lanes.json` at its root (DEV2, D115 §2.1): each
lane has the id this table names it by, a title and a one-line summary, and a test holds the two
tables together. What belongs to no lane is declared too, as `laneless` there: the docs, the doctrine
and the example family it is synced into, and the harness's settings. The merge tool reports those as
*no lane*. A test refuses a tracked file that is in no lane and not declared, so a new path is placed
when it is added, never left outside silently (LEFT1).

## 6. Order of work

1. **MOD9's records part first**, by the parent, while no branch is in flight: union attributes,
   counters, the duplicate-row check. It takes minutes and removes most shared-record conflicts at once.
2. **Wait for the branches in flight to merge** (PLUG9 (c, d)). Every split touches files they touch.
3. **The splits in parallel lanes**, one subagent each, at most three at once, because load makes flakes:
   MOD2 + MOD3 + MOD4 are one web lane and go in order in one branch; MOD5 is the modules lane; MOD6
   crosses the driver and service lanes; MOD7 is the CLI lane. Each is behaviour-preserving and proven
   by the gates as they stand, plus the family, Playwright and deployment rehearsals in the parent.
4. **MOD8 and the rest of MOD9** once the splits land: the tests follow their code, the `Process`
   category, the merge tool and the dispatch skill.

## 7. Not proposed

- **Splitting the solution into more assemblies or repositories.** The collisions measured are within
  files, not between assemblies. A new assembly boundary would add build and twin cost and remove no
  conflict counted above. The one boundary that would matter, a plugins repository, is PLUG9's and the
  owner's.
- **Locking files or queueing all merges.** It would serialise the work, which is the cost being removed.
- **Renumbering decisions into files** (one file per decision). Union merge plus reserved numbers remove
  the collision. Moving roughly a hundred decisions would break every anchor that cites one.
