---
name: command-group
applies_when: adding a group or a sub-verb to the one command tree (`daoris <group> …`, D228; the oneway design §11), renaming one, or moving a command from the CLI to the desktop's program
enforces: a group goes into every one of its places in the same change, its mark on its command, the router, the usage, the command and its routes, the lease, the host, Ask Daoris's doors and the records; each place has a test, and several run only in another lane
---

# A command group — every place it goes

**`daoris` is one tree (D228). The CLI runs the doctrine commands and hands every other word, whole, to the desktop's program,
which routes it once at its entry and hands the CLI any word no group owns. A group is spelled in the driver library, the host,
the modules and the records, and nothing but this list ties them together. Put a group in every row below in the same branch.**

## Why

Each place is held by a test, and the tests sit in four suites. The tree's first build met these by running them: the table
twice (the driver's and the CLI's), the routing, the usage, the lease's name, the help room's commands and the guide's table each
failed on its own.

## How to apply

**Work test first.** Add the group's mark first; `CommandTreeTests`' every-word test and `DriverCommandTests`' usage test are red
until the router and the usage take it.

| # | Place | Where | Held by |
|---|---|---|---|
| 1 | The mark | `[CommandGroup("<group>", <sub-verbs>…, Takes, Whole, Nested)]` on the library unit that runs the group, or whose words the host's console reads; one owner per group | `CommandTreeTests` (every declared word routes), `CommandWordsTests` (a name marked twice is refused) |
| 2 | The router | `Loop/CommandTree.cs`' `Inward`, where the group's words are not its command's own | `CommandTreeTests`' route theory; an old form's refusal by its theory |
| 3 | The usage | a line in `DriverCommand.Usage` beginning with the group's name, so `daoris help <group>` finds it | `DriverCommandTests`: every group has lines naming each sub-verb, and the tree reaches every verb the host answers |
| 4 | The command | `Daoris.Desktop.Driver/<subdomain>/<Group>Command.cs`: its world (the terminal's `InstanceWorld`, every machine fact a seam a test hands), `Verbs`, `Usage`, `Changes`, `Plan` and `RunAsync` | the text table's test, the command's tests |
| 5 | The operations and codes | each judgement a route makes, over typed facts; a coded refusal carries its check and facts | the both-ways test |
| 6 | The routes | `<Group>Routes`: each verb's route and class, or `alone:` with why; `DriverModule`'s catch of the coded refusal | `PipeCensusTests` (modules), `RefusalCatalogueTests` |
| 7 | The lease | `VerbLeases.Holds` reads the routed words; `CommandWords.Named` names the command by group and sub-verb | `VerbLeasesTests`, `CommandWordsTests`' name rows |
| 8 | The host | a thin console, dispatched in `Program.cs` after the route, under the one Ctrl+C handler | `DriverHostTests` (Process) |
| 9 | Ask Daoris | a door or a room's row only where one is wanted (D233); each command the room's lines name is one the tree runs | `HelpRoomDoorAsksTests`, `HelpRoomTerminalActsTests` |
| 10 | The records | the guide's command table; D228's dated note; the READMEs; the changelog | `version.test.ts` holds the table to the tree; `doc-shapes` |

A new test class's scratch goes under `_fixtures/` and is removed by `GitSeed.Discard`, never `Directory.Delete`.
