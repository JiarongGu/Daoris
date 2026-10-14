# Types split by hand across files: a survey (2026-10-14)

Evidence for the PART rows in `TASKS.md` and D166's note "no partial type without generated code" (the owner: *"also I
think we better not use partial class if we don't have auto-generated code"*). A read-only scout counted every type declared
`partial` in more than one hand-written `.cs` file under `src/` (build output and generated files excluded): 34. Line
counts are as surveyed and move with the code.

## Why they were made, and what must survive

The parallel-development design made the partial-per-domain and partial-per-kind shapes on purpose (MOD3, MOD5, MOD6 of
`docs/2026-09-30-parallel-development-design.md`; D106; D119): one new door or kind is one new file, so parallel branches stop
colliding on one file. The replacement keeps that: a class per domain or per kind, in its own file, not parts of one type.

Two types are discovered by a framework: `KnowledgeTools` (`[McpServerToolType]`, registered by `.WithTools<KnowledgeTools>()`
in the MCP host's `Program.cs`; tests reach its methods by `typeof(KnowledgeTools).GetMethod(…)`) and `DriverModule`
(`ReadTable()` reflects its non-public instance methods for `[DriverRoute]`: 148 marks over 19 parts). A generator needs
`partial` inside a multi-part type in only two places (`HelpProposalBox.Workflow.cs`'s workflow id pattern,
`McpToolDescriptionTests`' decision-number pattern); every other generated member sits in a type of one file, which keeps
`partial` there and is no offender.

## The 34, by shape

**Merge, or a second named class (mechanical).** `AcpSession` (`Acp.cs` and `Acp.Resume.cs`); `WorkflowNamed` (`.Versions.cs`'s
records and plan statics; the type keeps `partial` in one file for its patterns); `HistoryClearing` (`.Home.cs`, the home's
folders read: a class of its own); `PluginKit` (`PluginTrial.cs`: a `PluginTrials` class); `Tools` (`Tools.Children.cs`,
merged; `desktop-publish.test.ts:839` reads it by name); `ToolInstall` (`.Archives.cs` as `ToolArchives`, `.Actions.cs` merged);
the service core's short parts (`Asks.GoAheads`, `Asks.Review`, `Asks.Workflow` with its `WorkflowChoices` pattern kept,
`QuestExchange.Workflow`, `SessionLedger.GoAheads`, `Sessions.Review`); and four test classes (`McpToolsTests`,
`McpToolDescriptionTests`, `GoAheadTests`, `ReviewStepTests`).

**Records and interfaces a kind adds to (mechanical).** Help: the records `HelpProposal` (10 files), `HelpMachineFacts`
(13), `HelpPlan` (11), `HelpApplied` (3), `HelpMachine` (8, one property per room section) and the static `HelpProposals`
(2) take their kinds' members into one declaration each; `IHelpDoors` (15) becomes one interface inheriting a per-kind
interface each kind file declares, its one implementer (`ScreenDoors`, in the Modules bridge) and callers unchanged; the
test stand-in `HelpStandInDoors` (15) follows it. The client's views: `QuestView`, `QuestStepView` and `AskView` take the
properties Service's Domain adds into their one declaration in Quests' Domain, the element records staying in Service's.
`HarnessSettings` (3 files) and `HarnessRoster` (2) each become a file of their own out of the 3,776-line `Harnesses.cs`.

**A family of use cases over one class's state (design).** `ServiceClient` (seven parts in Service, Review and
Conversations: door classes over the client); `HelpProposalBox` (17 files in the service core: a shared proposal-file helper
and a writer class per kind, `ProposeX` kept on the box as one-line calls, about 150 call sites unchanged); the core's
`AskDesk`, `SessionLedger`, `SessionStore` and `QuestExchange` (a class per family with the stores handed in, where the
short parts are not merged).

**Large, each scouted alone (design).** `KnowledgeTools` (22 files, 1,792 lines, eleven services: several tool classes,
each registered, the tool names on the wire unchanged); `DriverModule` (20 files, 7,242 lines: a handler class per domain,
the table built from them, the routes test reworked); `ChatRunner` (8 files: classes over a shared context of its ten fields,
`ChatRunner` the front, `ClosedNote` kept in `ChatRunner.cs`); `SessionTrees` (18 files, 5,700 lines, stateless over
`(home, plugins)`: classes by concern behind a facade, `Sync`'s 1,234 lines split on their own); the loop's `Driver` (9
files: the parts D166 already places in Conversations, Quests and Review become classes there over a loop context).

## The gate

A universal gate over `src/**/*.cs` (`tools/partials.mjs` with its test and a `tools/partials.json` list), refusing a type
declared `partial` in more than one hand-written file, the offenders listed by type and files on a list that may only
shrink (a listed type no longer split is itself a breach, so the list is cut in the change that fixes it). One place for the
driver, Modules, the service, the hosts and the tests, with no .NET build to run.

## The large five, as scouted (2026-10-14)

After PART1-PART9 five types remain on the gate's list. Two were scouted for rows.

**`KnowledgeTools`** (the MCP host, `src/Daoris.Service/Daoris.Service.Mcp/`): one `[McpServerToolType]` class with eleven
injected services. The SDK (`ModelContextProtocol` 1.4.1) builds each tool type through dependency injection and takes
several types (`WithTools<T>()` per type, or `WithTools(IEnumerable<Type>)`). By what they use: the search tools
(`knowledge_search`, `_get`, `_repositories`, `_convergence`, `_refresh`, `registry`: the service and the ambient scope); the
quest tools (`quest_publish`, `quest_list`, `quest_respond`: the exchange, the quests, the intake, the asks, the ledger; their
records); `permission_propose` (the rule proposals); the review, go-ahead and helper tools (the ledger, the helpers); the
opinion tools; and sixteen Help kinds' `<kind>_propose` (only the help box and the intake's session). The workspace scope
(`ScopeAsync`, `Scoped`) is shared by search and `quest_list`. The tool names are the wire and do not change. Tests construct
the class in about 25 places (`McpToolsFixture` and its tests, the guard, helper, opinion, rule-proposal, newer-index and
knowledge-lines tests) and reflect on it for every tool method (`McpToolDescriptionTests`, `HelpProposalBoxKindsTests`, seven
Help tests' `GetMethod(nameof(ProposeX))`): one list of tool types serves the host and the tests. The driver's
`HelpProposalKindsTests.cs` (~:110-114) reads the MCP host's files for each `[McpServerTool(Name = …)]` and expects
`KnowledgeTools.Help.<Kind>.cs`: the Help row changes it in the same branch. `quest-operations.md` names the file.

**`DriverModule`** (the page bridge, `src/Daoris.Desktop/Daoris.Desktop.Modules/`): `DriverModule.cs` and eighteen domain
parts, about 7,400 lines and 127 `[DriverRoute]` handlers. Shared state: the loop (every part), the event bus (Agents,
Conversation, Help, Tools), the folder opener (Plugins, Sessions), the file picker (Tools), `Change(…)` over the config
(Across, Driver, Help, Lines, Rules, and `ScreenDoors`), and `Offers` (Plugins, set by seven tests); per-part fields (Agents'
actions, Git's reads, Tools' runs and versions) belong to their part alone. Shared statics `Optional`, `Flag`, `Number`,
`NotReady`, `TreeChoice`. The table reflects `typeof(DriverModule)`'s non-public methods for `[DriverRoute]`; `Routes` is read
by `DriverModuleRoutesTests`, which also derives each domain from a file name `DriverModule.<Domain>.cs` and holds a README row
per domain, and `tools/orient-index.mjs` (~:776-787) picks the same files for the index's route rows. Help is the hub: it calls
Accounts, Agents, History, Lines, Plugins, Sessions, Trees and Work, and `Driver.State` is used by eleven parts. About 125 test
calls to `DriverModule.X` statics in 21 test files re-spell to their owning class. Source readers by file name:
`DriverModuleLinesTests` (~:363-365, :461-471), `Driver.Tests/Trees/PullRequestOccasionTests.cs` (~:71, :89), and the CLI's
`orient-index.test.ts` fixtures. The shape: a `<Domain>Routes` class per domain in its own file beside `DriverModule.cs`, each
taking only what it uses; a small context for the loop, the bus, the opener, the picker and `Change`; one static helper for the
shared arguments; `DriverModule` building its table from an explicit list of handler instances (one new door is one new file
and one line in that list, where nothing is found by scanning); `partial` kept on `DriverModule` until the last domain moves.

**`ChatRunner`** (`Conversations/Chat/`, the front and seven parts): its state is the nine constructor fields (the service, the
home, the processes, the output, the browser, the usage, the plugins, the harnesses, the events) and two stores it makes
(`_conversations`, `_marks`), and, the real coupling, the live maps the parts share: `_talking`, `_watching`, `_turned`
(`ITurnedChat`), `_toldProcess` on the front, `_goingOn` and `_closing` (GoOn), `_rooms`, `_handing`, `_browsers` (Help),
`_helperSaying` (HelperWords), `_curveMoves` (Curve; HandOver reads it too), `_starts` (StartWith). Private nested types the
parts touch: `ITurnedChat`, `ProtocolChat`, `NativeChat`, `Watched`, `ChatPlace`, `GoingOn`, `Handing`. `StartFrom` and
`StartWith` are already static classes over delegates: only their thin partial fronts remain. About 28 files call the parts'
entries through the front (`TakeUpAsync`, `GoOnAsync`, `HandOverAsync`, `StartHelpFromAsync`, `StartFromAsync`,
`StartWithAsync`, `MoveByCurveAsync`): the front keeps one-line calls. Texts read by name: `ClosedNote` in `ChatRunner.cs`
(`deployment-rehearsal.test.ts`), `StartFrom`'s constants in `ChatRunner.StartFrom.cs` (`say.test.ts`, `merge-branch.mjs`).
`SayHandedOver` and `SayHelperWord` are near twins (a message put on a live chat), a later dedup. The shape: a `ChatContext`
holding the fields and the live maps, `ChatHandOver` and `CurveMove` (they share the curve's moves), `ChatGoOn`, a help chat
class and `HelperWords`, each over it, the front keeping its users' calls.

**`SessionTrees`** (`Trees/SessionTrees/`, the front and seventeen parts, about 5,700 lines): stateless over `(home,
plugins)` except two test seams (`BeforeDeleting`, `ListFails`) and four derived reads (`Recorded`, `Grown`, `TreesRoot`,
`Config()`); 245 test and 31 source constructions keep the constructor. `IWorkTrees` (OnlyHere) is implemented by it and a test
stand-in. The coupling: the worktree reads (seven parts), the content holds and branch deletion (`HeldByContentAsync`,
`KeepDiscardedAsync`, `DeleteJudgedAsync`, `HoldsAsync`: five), the landing's proof (`ProveAsync`: seven), and grab-bag git
words (`LineAsync` in twelve parts, `ComparableAsync`, `FirstLine`, `Failure`, `Normal`, `SamePath`, `OwnerOf`, `Said`). The
sync (1,234 lines) holds about 308 lines of records and words (`SyncWords`, `SyncBounds`, read by `SyncBoundsTests` and
named in `twins.md`) and, by concern, the plan and fetch, the press, the review range (not sync work), the line pulls, the
branch judging and the replay. Texts read by name: `SessionTrees.cs`'s `s-{Guid}` id (`orient-report.test.ts`), and
`PullRequestOccasionTests` reading `SessionTrees.Sync.cs`'s `SyncPlanAsync` body and the doors' file names (a delegating front
must not count as a door). The shape: a shared base (the git words, the worktrees, the scratch walk, the leftovers), the sync
in three classes and its types in files, review and work classes, tidying classes, landing classes, the front keeping its
callers' calls; extension methods and callers building new classes rejected, as in PART8.
