# Parallel development — where branches collide, and the shape that stops it

> The owner, 2026-09-30: *"since daoris is getting more and more complex we should modulize this
> project properly so that paralle development with subagent can run smoothly"*. The contract for the
> MOD rows in `TASKS.md`. Status: **MOD1–MOD7 built** (D106 for MOD1); MOD8 and MOD9 open.

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
   rest. FLAKE1's classes are exactly the ones that fail under another worktree's load.
6. **One merge tool.** `tools/merge-branch.mjs` merges `--no-ff --no-commit`, stops on a conflict it
   cannot resolve and names the files, then runs the declared gates in order, fast first. It keeps
   every gate's whole output under `local/scratch/`. This replaces the hand-typed chains, one of which
   lost a rehearsal's reason.
7. **A dispatch skill.** The brief every subagent got today (rules to read, gates it may run and may not,
   files it must not touch, what the hand-back carries) becomes a repository-local skill. A prompt then
   says only the task, its lane and its reserved decision number.

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

| Lane | Owns |
|---|---|
| Web shell | `src/Daoris.Web/src/{shell.ts,bridge/,App.tsx,ui.tsx}`, the work frame |
| Web settings | `SettingsView.tsx`, `settings/`, and their catalogue areas |
| Driver library | `Daoris.Desktop.Driver/` by feature folder: loop and planner, trees and landing, plugins and hooks, help, toolchain |
| Desktop modules | `Daoris.Desktop.Modules/` by partial, and `Daoris.Desktop.App/` |
| Service | `src/Daoris.Service/` |
| CLI | `src/Daoris.Cli/` |
| Tools and records | `tools/`, `.gitattributes`, the docs gates |

A feature usually crosses two or three lanes (a driver door, its module route, its screen). After the
splits, crossing a lane means adding a file in it, not editing that lane's god file, so two features
can cross the same lanes at once.

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
