# CODESHAPE1: build plan

Goal: each contract is declared once, beside its handler, and everything else is registered at start or generated. Then the hubs
become domain modules that register their parts, and the roots hold only the order and the wiring (D231 point 1, D228, area rules 3
and 7). Read: `TASKS.md` CODESHAPE1, D231, D228, `docs/areas/code-structure-and-process.md`, and the outlines of the hubs.

## 1. Mechanisms

### 1.1 The service's HTTP API: a declaration of our own and a small tool of our own

**Chosen.** A new project, `src/Daoris.Contracts/`, that uses only the BCL. Service.Http, Desktop.Driver and the tool reference it.
It holds the wire records, moved from `ApiContracts.cs` into one file per domain. Each domain's file has its own
`JsonSerializerContext`, and the contexts are combined with `JsonTypeInfoResolver.Combine`. A route is declared once, as its request
record:

```csharp
public sealed record RespondQuest([property: FromPath] string Id, string Word, …) : IRoute<QuestActionResponse>
{ public static string Method => "POST"; public static string Path => "/api/quests/{id}/respond"; }
```

- **Service.** There is one binder, `RouteMap.Map<TReq,TAns>(handler)`. It takes path values plus the query (GET) or the body
  (other methods) into `TReq` through its `JsonTypeInfo`. A handler returns `Answer<TAns>`, which is either the answer or a refusal
  with its status. Each domain has a `Routes/<Domain>Routes.cs : IRouteModule` that maps its handlers. `Program` finds the modules
  by reflection over its own assembly and keeps only composition, middleware and their order.
- **Driver.** One method, `ServiceClient.SendAsync<TAns>(IRoute<TAns>)`, replaces the one method per route. Callers send the record.
  Mapping into the driver's views moves to `Service/Wire/<Domain>Views.cs`. The client is **registered, not generated**: the
  declaration carries its method, path and answer.
- **Page.** It is generated. `tools/contracts/ContractsGen.csproj` follows the precedent of `tools/chromium-probe`. It reflects over
  the `IRoute<>` implementors and reads each type's `JsonTypeInfo`: wire names, `IsGetNullable`, required. From that it writes
  `src/Daoris.Web/src/data/generated/service.ts`, with every wire type and a `ROUTES` table. `api.ts` keeps one
  `send(name, request)` (filling the path, the person key and the origin) and the page's helpers.
- **Where it lives, and staleness.** `src/Daoris.Web/src/{data,bridge}/generated/` and the tool's `bin/` and `obj/` are ignored.
  - `tools/contracts.mjs` writes the output (by running the tool). With `--if-stale`, the web's `pretypecheck`, `pretest` and
    `prebuild` run it, and the checkout and merge hook runs it where `dotnet` is present.
  - With `--check`, it runs in `npm run quick` and `verify`. It hashes the declaring sources (`src/Daoris.Contracts/**`, the
    modules' `Bridge/Routes/**` and their payload records) in pure Node and compares that with the hash stamped on the generated
    file's first line. If they differ it fails with exit 1 and names the command to run. Typecheck catches any read of a field that
    no longer exists.
- **Rejected.**
  - *OpenAPI plus a generator.* `Microsoft.AspNetCore.OpenApi` is a new runtime package. The routes answer `IResult` refusals, so
    every route would need `.Produces<T>()`, which is a second declaration. C# client generators (NSwag, Kiota) emit their own DTO
    copies and bring runtime libraries.
  - *A Roslyn source generator.* It needs the `Microsoft.CodeAnalysis` package, and an analyzer may not write files. The page's
    TypeScript would then need a second tool, which is two ways to do one job.

### 1.2 The window routes: the marks carry everything; C# reads them at start and TypeScript is generated

- **The marks.**
  - `[DriverRoute("X", Class = PipeClass.Person, Drain = PipeDrain.Waits, LookWhen = "field=value")]` replaces the census and
    drain tables and `PipeRule`.
  - Grounds become attributes of their own beside the mark, named by route as `[Publishes]` is. Each kind of ground derives from
    one `RouteGroundAttribute`: `[SessionGround("SESSION_DELETE","id")]`, `[SettingGround(…)]`,
    `[NotAlready("SET_LANDING","…")]`, `[Owner("RETRY_QUEST", OwnerField.Quest)]`.
- **Read at start.** `RouteDeclarations.Of(assembly)` is DriverModule's existing reflection pass. From the marks it builds the driver
  half of `PipeCensus`, the target rows, the owner rows and `PipeWording`'s fields. A route with no class fails the module's first
  use, as an unmarked `Publishes` does today.
- **Typed payloads.** A handler takes a payload record, for example `SetCoolOff(CoolOff p)`, which DriverModule binds from
  `IpcRequest.Payload`. Its answer is the method's record type.
- **Generated.** The same tool writes `src/Daoris.Web/src/bridge/generated/routes.ts`: each module's route union, the payload and
  answer types, the class, and `PUBLISHES`.
  - `call<R extends DriverRoute>(route: R, p: Payload<R>): Promise<Answer<R>>`.
  - `publishing.ts` loses `DECLARED`, and `StatePublisherTests` stops reading a copy of it.
  - `person.json` stays the authored catalogue (D222). The names check reads the generated list of person-classed routes and
    requires each `person.route.*` key in each language.

### 1.3 The command tree: each half is declared by the code that owns it

- **The program's groups.** Each desktop command class carries `[CommandGroup("session", Verbs = …, Nested = …, Takes, Whole)]`, and
  `CommandWords.Groups` is collected from those at start.
- **Doctrine.** The CLI's `COMMANDS` rows declare it.
- **Neither half holds the other's list.**
  - The program hands any word it has no group for to the CLI it carries.
  - The CLI hands any word that is not doctrine to the program.
  - Each hands a word only once (`DAORIS_HANDED`). A handed word that neither owns is refused with exit 2 and the usage.
- **Deleted.** `fixtures/command-tree.json`, the tables in `CommandWords` (Doctrine and Groups), and the equality checks in
  `CommandWordsTests` and `cli-tree.test.ts`. `docs/index/verbs.md` is already the generated listing of the whole tree.
- **Rejected:** keeping the JSON as the source. The verbs would still be written in each command and in the CLI's rows, so there
  would be two copies, not one.

### 1.4 The driver's file map: derived from folders

- **Derived.** A file's subdomain is its first folder. A test's subdomain is its folder in the test project. All 1,024 entries in
  `files` equal their folder today, so nothing is lost.
- **Deleted.** `files` (42 KB) and `tests` (667 entries, 24 KB), together with the `[place]` and `[map]` checks that guard them. The
  test files at the test project's root move into their subdomain's folders.
- **Kept.** `subdomains`, `seams`, `edges`, `pure`, `loopRules` and `testFolders`, about 3.5 KB.

### 1.5 The hubs as registered modules

**One kit**, in `src/Daoris.Contracts/Parts/`, used by both the driver and the service:

- **Declaration.** Each seam has its own subclass of `PartAttribute(name) { Phase; After[] }`.
- **Discovery.** `Parts.Discover<T>(assembly, factory)` finds them by reflection. The constructor takes the root's wiring bag.
- **Order.** `PartOrder.Of` runs the phases in their enum order. Within a phase it is a Kahn sort by `After`, stable by name.
- **Refusals at start.** A duplicate name, an `After` that names no part (or a part in a later phase), and a cycle, named by its
  members. Each root's `<Root>PartsTests` builds the order from the real assembly, so a cycle fails a unit test, not an install.
- **The list.** `tools/orient-index.mjs` reads the part attributes as it already reads `[DriverRoute]`. It writes
  `docs/index/registered.md`: seam, phase, order and `file:line`. The area document's parts row points at it.

| Hub | Seam | How the root runs it | First three modules |
|---|---|---|---|
| `Loop/Driver.cs` | `[LookPass(name, LookPhase, After)]` on `ILookPass.RunAsync(LookRound, ct)`. The phases are Before, Snapshot, Beside, Starts and After. `LookRound` carries the snapshot, events, judged, taken and slots. | At the anchor of each phase in `LookAsync`, it runs that phase's passes, timed by `LookStages` under the pass's name. Code that has not moved stays inline between the anchors. | `sync` (1898-1937), `claims` (1992-2035, after `sync`), `landing` (1702-1822, After) |
| `Loop/Chat/ChatRunner.cs` | `[Executor(kind)]` on `IExecutor.StartAsync(ExecutorStart, ct) → RunHandle`. Executors are keyed by kind, and a duplicate kind is refused. | It keeps the table of runs, stop-all and dispose. Its `Start*` doors dispatch by kind. | `help` (892-1407), `replacement` (962-1032, 1960-2055), `start-from` (327-394) |
| `Agents/Acp.cs` | `[TurnStage(name, TurnPhase, After)]` on `ITurnStage`. The phases are Update (each `session/update` passes every stage, in order), Request (one answerer per protocol method, keyed) and End. | `DispatchAsync` hands each frame to the registered stages. | `render` (3722-3957), `permission` (3340-3560), `measure` with the options (3674-3704) |
| `Quests/Quests.cs` | `[QuestOperation(kind)]` on `IQuestOperation`: Kind, WritePayload, ReadPayload, ApplyAsync(QuestTx). Operations are keyed by kind; replay follows the log. | The store's `PayloadJson`, `ReadOperation`, `ReceiveAsync` and `RebaseAsync` dispatch by kind. | `close` (2258-2473), `reopen` with continue (2106-2235), `conflicts`, dismiss and bring forward (2735-2873) |

The service is not published AOT, since only the devkit sets `PublishAot`. So the service also discovers by reflection, and there is
one way. Each moved part has its comments cut, as it moves, to why, the trap and the contract (D231 point 3).

**Register entries until the series ends (D228):**

- `routes-by-hand`: ended (A9). No `app.Map*` call is left in `Program` and `ApiContracts.cs` is gone. The per-route reads of
  `ServiceClient` that parse a route's JSON by field are what remains: the sessions, quests and asks readers (`ReadSessions`,
  `ReadRecords`, `ReadClosed` and their kin), `RegistryAsync`/`ReadRegistry`, and `PostJsonAsync` callers.
- `route-payload-by-hand`: ends when no handler reads `IpcRequest`. B8 deletes it.
- `inline-look-passes`, with the follow-on rows below.

## 2. Chunks

Each chunk is about an hour, compiles on its own and runs no tests. The KB are cuts to the files listed in
`tools/size-ratchet.json`. A file that falls under 40 KB leaves the list whole.

| # | Chunk | Needs | KB off the list |
|---|---|---|---|
| A1 | The Contracts project, `IRoute`, binder, discovery of `IRouteModule`, `SendAsync`. Exemplar, end to end: the knowledge reads (status, protocol, repositories, search, entry, entries, convergence). | none | ≈15 |
| A2 | `ContractsGen`, `tools/contracts.mjs` (write, `--check`, `--if-stale`), ignores, the web's pre-scripts, quick and verify. `api.ts` takes the knowledge types from the generated file. | A1 | ≈3 |
| A3 | Quests I: reads, publish, respond, accept, done, reopen, continue, forward, evidence, bind; the `ToQuest` family; the quest records (`ApiContracts` 63-289); the client's quest methods and `ReadQuests`; `api.ts` quest types | A2 | ≈70 |
| A4 | Quests II: close and outcome, set-up, review, set-up step, workflow, confirmations, dismiss, delete, attachments; the client's set-ups, review and workflow (3148-3409) | A3 | ≈35 |
| A5 | Asks and the channel (`Program` 1109-1332, `ToAsk`); the client's asks (1088-1471); `AskResponse` and its records | A2 | ≈50 |
| A6 | Sessions I: intake, help, help-mode, access, actor, resolved, records, memory, deletion | A2 | ≈45 |
| A7 | Sessions II: added, say, restore, taken, attempt, take-back, move, list, open, chat, state; `ToSession`; `ReadSessions` (2296-2938) | A6 | ≈65 |
| A8 | Opinions, helpers, partners, brainstorms, history, memory waits; the client's history and brainstorm | A2 | ≈38 |
| A9 | Registry, code map, refresh, feeds, sync, operations, retired. `ApiContracts.cs` is deleted; `Program.cs`, `ServiceClient.cs` and `api.ts` leave the list; `routes-by-hand` is deleted. | A3-A8 | the rest: **551 for A1-A9** |
| B1 | Class, Drain and LookWhen in the marks. `PipeCensus.Driver()` and `DriverDrains()` are deleted. Each listed route file the chunk touches has comments cut so it does not grow. | none | 45 (`PipeCensus` leaves) |
| B2 | Ground, not-already and owner marks. `PipeTargetRows.cs` and `OwnerTargetRows.cs` are deleted; `PipeWording`'s field list comes from the grounds. | B1 | 43 |
| B3 | The tool writes `bridge/generated/routes.ts`; `call` is typed by route; `DECLARED` and the twin read are deleted; the names check reads the person-classed list | A2, B2 | ≈5 |
| B4 | Exemplar typed payloads: DriverModule binds the record; the `SET_*` routes in `DriverRoutes.cs`; the payloads of `bridge/driver.ts` | B3 | ≈12 |
| B5-B8 | The other 32 route files in four groups: Sessions, Conversation and Lines; Trees, Git and Workflow; Help and Agents; the rest. Each deletes its hand-written payload reads and the page's payload types. B8 deletes `route-payload-by-hand`. | B4 | ≈25 each |
| C1 | The command tree, as in 1.3 | none | 0 (12 KB of copies deleted) |
| D1 | The file map, as in 1.4 | none | ≈8 (`StructureTests`); 66 KB off the fixture |
| E1 | The parts kit, `registered.md`, the Driver's look anchors with `LookRound`, and `sync`, `claims` and `landing` | A1 | ≈11 |
| E2 | ChatRunner executors: `help`, `replacement`, `start-from` | E1 | ≈46 |
| E3 | Acp turn stages: `render`, `permission`, `measure` | E1 | ≈27 |
| E4 | Quest operations: `close`, `reopen`, `conflicts` | E1 | ≈30 |
| E5 | Moves by responsibility, with no seam: `QuestSchema` (847-1164), `QuestRecordCodec` (4083-4675), and ChatRunner's `ProtocolChat` and `NativeChat` transports | E2, E4 | ≈86 |
| F1-F5 | Comment cuts on what stays in the hubs, which are 40-53% comments: Driver 1-2234 and 2235-end, ChatRunner, Acp, Quests | E1-E5 | ≈330 |

**Order.** These tracks are independent and can run up to five at once: A (serial, since every chunk touches `Program` and
`ServiceClient`), B (serial, on DriverModule), C, D, and E (E2, E3 and E4 in parallel, since each is its own hub).

**Follow-on, sized after the exemplars land.** Each root under 40 KB needs the rest of each hub moved by the same recipe:

- the Driver's other ≈27 passes, and the driven executor (`RunAsync` 106 KB, `Task` 24 KB, `DrivenProtocol` 14 KB), split into
  its turn stages: about 10 chunks;
- ChatRunner: about 4 chunks;
- Acp: about 4 chunks;
- Quests: about 5 chunks;
- the host's `Program` (46 KB): 1 chunk.

## 3. Risks

1. **The ratchet against the marks.** Adding attributes or call sites to a listed file grows it. Each chunk cuts comments in the
   files it touches, or splits them first.
2. **Wire tolerance.** Today's hand-written reads accept an older host's missing fields and wrong kinds. A typed record reads a
   missing field as its default and throws on a wrong kind. Optional fields must be nullable, and `SendAsync` reports a
   `JsonException` as the host's refusal. The stand-in hosts in the tests may answer shapes that now fail.
3. **Wire stability for other machines.** A shared host serves drivers of older builds, so paths, wire names, status codes and the
   shapes of refusals must stay exactly as they are. The binder replaces the binding of minimal APIs (bools, arrays, 400s), so a
   difference in it is a regression.
4. **The web loop now needs `dotnet`.** After a contract edit, the next web typecheck builds the tool, which takes about 30-60
   seconds. A machine with Node alone cannot regenerate.
5. **The cross-tree project.** `Daoris.Contracts` sits beside two trees, each with its own build props and central package files.
   It must take no package and set its own props, and the publish tools must carry its assembly.
6. **The hand-off of commands.** A typo is now refused by whichever program sees it last, with exit 2 kept. With no CLI carried,
   the program says so. `daoris help` takes the doctrine half from the CLI's usage.
7. **Failures appear late.** No chunk runs tests (D180's note), so drift appears only at SCOPEVERIFY1. Keep the A and B chunks
   mechanical: the same names, paths and answers.
8. **Start-time failures.** A missing constructor dependency for a part fails at start. The parts test of each root builds them.
