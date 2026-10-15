---
name: setting-doors
applies_when: adding a door to Ask Daoris's `setting` kind, the way a `daoris driver` verb becomes something a helper proposes and a person applies, or changing what an existing one takes
enforces: a new door goes into every one of its places in the same change, the driver's judge, the service's writer, the tool's words, the coverage row, the room's row and its goldens, the index; the coverage test fails on the place it misses
---

# Setting doors — every place a new `setting` door goes

**A `daoris driver` verb reaches Ask Daoris as a door of the `setting` kind (D110): the helper proposes it, a card says
what it changes and the terminal command that does the same, and nothing changes until the person presses Apply. The
door is spelled in two runtimes that share no code (the driver and the service, `twins`) and in the words a helper reads,
and nothing but this list ties them together. Put a new door in every row below in the same change.**

## Why

DOORS1a (`helpers` and `commits`, D164's note) went into every place below, and two commits carried it (driver
`7012d4308`, service `614c4361b`). A missed place fails in a different room from the one that missed it:

- **The driver's list and the service's differ.** The service refuses the door as no door, or the driver takes a
  door the helper was never told. `HelpProposalKindsTests` holds the two lists equal and in one order.
- **The tool's words lag.** The helper is told which doors exist only by the tool's description. A door it is not told
  of is never proposed, and a flag it is not told of is never spelled.
- **The coverage row stays `Exempt`.** D110 holds every verb and control as a door, a door owed or an exemption with its
  reason; a row that still says *owed* when the door is built fails nothing but lies to the next reader.
- **The room does not teach it.** Ask Daoris answers *how do I* from the room's doors; a door absent from them is one
  the person is never shown, and the three goldens and the settings golden fail to say so.

## How to apply

**Work test first.** Add the door's cases to the driver's table (`HelpSettingProposalsTests`) and the service's
(`HelpSettingProposalTests`: its `InlineData` rows for the accepted and the refused words), watch them fail, then place
the door one row at a time. The coverage and room tests come last and hold the rest.

### The places

Paths are under `src/`. The verb itself (the CLI's `driver.ts` table, its usage and its edit, the twin in
`Daoris.Desktop.Driver`) exists before the door; the door judges by the verb's own rule, never a second one.

| # | Place | Where | What goes in |
|---|---|---|---|
| 1 | The driver's list | `HelpSettingProposals.Doors`, `Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpSettingProposals.cs`, and the `<remarks>` above it naming the row that added the door | The door's name, as the verb spells it. The order is the helper's reading order and the service's must match it. |
| 2 | The driver's case | `HelpSettingProposals.Plan`, the same file: a `case "<door>":` that calls a private method (`Opinion`, `Helpers`, `Commits`) or, for a bare value, plans inline (`cap`, `cooloff`) | Refusals return `Refused(words, "", "")`. A planned door gives its sentence, the terminal command, and the edit of `DriverConfig`. The method splits the words as the verb does (`Words`, `RuleWords` over the verb's value flags and switches), judges by the rule's own `Apply` or `Problem`, and says the change in the terminal's sentences. A door that edits a rule the verb replaces whole keeps what the card does not name, read when the person applies (`landing`'s key and squash). |
| 3 | The service's list | `HelpSettingProposalWriter.Doors`, `Daoris.Service/Daoris.Service.Core/Help/HelpSettingProposalWriter.cs`, and its summary naming the row | The same name in the same position (row 1). The `<param name="Door">` sentence on `SettingChange` at the top of the file spells the list too. |
| 4 | The service's shape check | `HelpSettingProposalWriter.Refusal(SettingChange)`, the same file: a `case`, and its flags list (`OpinionFlags`, `HelpersFlags`, `CommitsFlags`) when the door takes flags | Only the shape: target or workspace and exactly one of them for a rule set for a repository or a workspace, a value that is present, every flag one the verb takes. The rule's own judgement is the driver's, which knows the registry and the machine. The refusal sentence names the verb's usage. |
| 5 | The tool's words | `HelpSettingTool`, `Daoris.Service/Daoris.Service.Mcp/HelpSettingTool.cs`: the tool's description (the *Each door is a `daoris driver` verb:* list, and a sentence on what the door takes and what it never is), and the `ProposeSetting` parameters `door`, `target`, `workspace` and `value` | The name in the door list and in the `door` parameter's list; `target` and `workspace` name the door where it is set for a repository or a workspace; `value` spells the flags in the verb's own words. A seed from a repository's own instructions is said to be a proposal the person applies, never the process (D164's amendment point 5). Every one of these four is a sentence a helper reads, so a flag missing in one is a flag never proposed. |
| 6 | The coverage row | `HelpCoverageTests`, `Daoris.Desktop/Daoris.Desktop.Driver.Tests/Help/HelpCoverageTests.cs`: the verbs table, `("<verb>", new Door("setting", "<door>"))` in place of an `Exempt` or an owed row, and a comment naming the row that built it | One row per verb (or per form: `across read`, `across write-to`). Where the Setup page has a control for it, the controls table gets `("projects", "useSet<Name>", null, new Door("setting", "<door>"))` (D110 §1; the hook the page presses, not the route). A door in neither table fails the test. |
| 7 | The room's row | `HelpRoomDoors.Doors`, `Daoris.Desktop/Daoris.Desktop.Driver/Help/Room/HelpRoomDoors.cs`: a `(to, screen, terminal)` row, and its comment naming the row and the decision point | `to` says what the person wants done and that Ask Daoris proposes it; `screen` the Setup name (and the workspace's *Defaults* name) or, while Setup has none, the reason; `terminal` the verb's usage, backticked. The order is the order a person meets them: a process rule's row (`commits`, `helpers`) sits before the review and opinion rows; the workflow rows (`current`, `name a workflow`, `choose which workflow`) come **after** the opinion row, and `HelpRoomDoorsTests` fixes that order (`At("declare which other agent reads") + 1` is `Current`, then `name a workflow`). A row placed past opinion breaks it. A changed verb (a new flag) changes its row's terminal text, in place. |
| 8 | The goldens | `Daoris.Desktop.Driver.Tests/Help/golden/help-room/`: `empty.md`, `full.md`, `sparse.md`, `settings.json` | Never by hand. `DAORIS_GOLDEN_WRITE=1 dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter "FullyQualifiedName~HelpRoomGoldenTests"` writes them from the render (PowerShell: `$env:DAORIS_GOLDEN_WRITE = '1'` first, unset after); read `git diff` on that folder and see only the new row. Run it again unset: it compares. (`src/Daoris.Desktop/README.md`, GOLDEN1.) |
| 9 | The index | `docs/index/`, by `node tools/orient-index.mjs` | The generated code map names the new case and method; `verify` runs the test that it is current. Never edited. |
| 10 | The records | The decision's dated note (D110 for the mechanism, the decision the verb's rule belongs to for the door), `.claude/knowledge/twins.md` where the door's rule is a twin's (its row names the card's sentences copied from the verb), `CHANGELOG.md` for what a person sees | Point to the commits. D164's DOORS1a note is the worked example of what to say: the door, how its words are read, what a card keeps, what is seeded and what is never authority, the room's rows, what is left. |

### Check before saying done

1. `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process`, which holds rows 1, 2, 6, 7, 8
   (`HelpSettingProposalsTests`, `HelpCoverageTests`, `HelpRoomDoorsTests`, `HelpRoomGoldenTests`) and row 3 against row 1
   through `HelpProposalKinds`.
2. `dotnet test src/Daoris.Service/Daoris.Service.Tests`, which holds rows 3, 4 and 5 (`HelpSettingProposalTests`,
   `HelpProposalBoxKindsTests`).
3. `npm run -s verify`, with the index regenerated.

A branch outside the driver lane cannot run the first; the hand-back then names the door's coverage row and room row for
the parent to add at the merge (the hand-back rule on bridge hooks).

## Worked examples

- **DOORS1a** (`7012d4308`, `614c4361b`; D164's note): two doors, `helpers` and `commits`, and the `landing` door's new
  switches. Rows 1 to 8 in two commits, the records in a third.
- **TOOL4l-b** (`156e7bfd6`): `cooloff`, the smallest: a bare number, planned inline, one `case` on each side, one
  clause in the tool's words, the coverage row.
- **XAGENT1a** and **REVIEWENV1a**: the first rules with flags (`opinion`, `review`), the pattern DOORS1a followed.

## What this does not cover

A door for another kind (`workflow`, `session`, `sync`, ...) has its own `Help<Kind>Proposals.cs` and writer
(`twins`' row on Ask Daoris's proposals) and the same rows 3 to 10 in its own names. A new *kind* is wider: its card in
the page (`help/ProposalCard.tsx`), `HelpProposalKinds` and `HelpProposalBox`'s `Propose…`, counted by their tables.

LAYOUT8's `setup` kind is the worked example of a new kind, in three commits (service, driver with modules, web). Beyond a
door's rows: the service's `Help<Kind>ProposalWriter.cs`, its line in `HelpProposalBox.Kinds`, `Help<Kind>Tool.cs` in
`KnowledgeServer.ToolTypes`, and `McpToolRegistrationTests`' two lists; the driver's line in `HelpProposalKinds.All`, the
fields it judges by under a comment of its own in `HelpMachineFacts` and `HelpPlan`, its `IHelp<Kind>Doors` in `IHelpDoors`
and `HelpStandInDoors`; the modules' gate and the facts read only for it in `HelpRoutes.HelpFactsAsync`, its card data in
`HELP_PROPOSALS`, its door in `ScreenDoors`, a nudge after `HELP_APPLY` where it moves work, and what it changes in `Applies`
(with `StatePublisherTests` and `test/publishing.ts`); the page's union in `ProposalCard.tsx`, its title in both catalogues,
the room's `HelpRoomMayPropose` row and the Ask Daoris design's §3 list. A kind whose facts start a process takes its world
through a seam the modules' fast half stands in for (`DriverContext.SetupWorlds`).
