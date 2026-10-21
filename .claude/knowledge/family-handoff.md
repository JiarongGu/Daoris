---
name: family-handoff
applies_when: handing a `daoris` management family's verbs to Daoris's driver program (D194's PUSH1f2 rows, the way `driver` and `tool` were handed), or moving one more verb of a family across
enforces: a handed family goes into every one of its places in the same change, the host's command and route, the lease rows, the routes held to the census, the code table, the text table and the both-ways test, the CLI's forward and its goldens, and the TypeScript that leaves with its tests and twins rows; each place has a test, and several run only in another lane
---

# Family hand-off — every place a handed family goes

**`daoris driver|agent|plugin|tool …` runs `daoris-driver` with the same words (D194). A family handed over is spelled in the
driver library, the host, the modules, the CLI and the records, and nothing but this list ties them together. PUSH1f2a built
the seam with `driver`; PUSH1f2c walked these rows for `tool`, and PUSH1f2f for `plugin`, the last of the four. `browser` and
`remote` still edit their files in TypeScript (the alone-path design §7): a family handed later walks the same rows. Put a family
in every row below in the same branch.**

## Why

Each place is held by a test, and the tests sit in four suites. PUSH1f2c met these by running them, each passing its own
lane alone: the scratch census (`GitSeedTests`: a new class deleting its scratch itself), the host's routing test that reads
`Program.cs` as text (`DriverChoicesCommandTests`), the dogfood rule listing which CLI rows reach the service client, and the
keep table's rows for a file whose CLI writer left.

## How to apply

**Work test first.** Run the TypeScript verb once over its tests' cases and write the table before anything leaves (a
scratch generator under `_fixtures/`, as `tools-text.json`'s was); then the host's text test reads it, red until the command
exists.

| # | Place | Where | Held by |
|---|---|---|---|
| 1 | The command | `Daoris.Desktop.Driver/<module>/<Family>Command.cs`: its world (the terminal's `InstanceWorld`, and every machine fact as a seam a test hands), `Verbs`, `Usage`, `Changes`, `Plan` (each routed change's press: route, payload, operation, fields, words) and `RunAsync` | the text table's test, the command's tests |
| 2 | The operations and codes | each judgement a route made, taken out over typed facts; a coded refusal carries its check and facts; one table maps check to code (`ChoiceCodes`, `ToolCodes`) | the both-ways test (row 9) |
| 3 | The routes | `<Family>Routes`: each verb's route and class, or `alone:` with why | `PipeCensusTests` (modules), a row per family |
| 4 | The module's mapping | `DriverModule`'s catch of the coded refusal before `DriverException`; `Refusals`' constants read the library's codes | `RefusalCatalogueTests`, the route tests |
| 5 | The lease | `VerbLeases.Holds` reads the family through `Changes`; `Name` takes its sub-verbs | `VerbLeasesTests`' two theories and the name rows |
| 6 | The host | a thin console, and the family in `Program.cs`'s one `args is ["driver" or …]` branch under the one Ctrl+C handler; a line in `DriverCommand.Usage` | `DriverChoicesCommandTests`' routing test reads it as text |
| 7 | A long verb | stage holding nothing, publish under `HomeLease.ForPublicationAsync`; a file it writes holds its keep lock from its read (D173's notes) | the command's lease tests, a writer-race theory |
| 8 | The text table | `Driver.Tests/fixtures/<family>-text.json`, a changed row with its reason | the text test, row for row |
| 9 | Both ways | `Modules.Tests/Bridge/<Family>BothWaysTests`: alone vs the route's answer through `Plan`, bytes, text, exit equal | itself |
| 10 | The CLI's forward | `cli/<family>.ts` hands `args.argv` to `args.driverProgram ?? runDriverProgram`; until the family's last row, an allow-list held to `Verbs` | `cli-usage.test.ts`' handed rows; `refusals.json` loses the family's rows; `help.txt` and `cli.ts`' usage paragraph |
| 11 | What leaves | each module whose last reader goes, found by a search of `src/Daoris.Cli/src`, `tools/`, `src/Daoris.Web` and the tests, the driver's and the service's tests that read a CLI file among them (`SourceFiles.Cli`); the keep table's rows (`only: driver`); `structure.json`'s modules and tests; `home-census.test.ts`' rows; the dogfood rules naming them; a table stays where a twin still reads it | `npm run typecheck -w daoris`, the dogfood test, the structure and census tests |
| 12 | The records | `twins.md` rows losing the CLI's column; D194's dated note; the READMEs; the changelog; the archive; `docs/index/` regenerated | `doc-shapes`, `orient-index --check` |

A new test class's scratch goes under `_fixtures/` and is removed by `GitSeed.Discard`, never `Directory.Delete`.
