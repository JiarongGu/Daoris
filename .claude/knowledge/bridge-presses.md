---
name: bridge-presses
applies_when: adding a route the page sends the driver's bridge module (`DAORIS.DRIVER`), a press or a read, the way `SESSION_HELPER_DECLINE` and `HELPER_WAITS` were added, or a new route domain, or changing what an existing press changes
enforces: a new route goes into every one of its places in the same change, the domain's handler and its mark, what it changes and the page's copy of it, the README's row, the page's call, a landing's count; each place has a test that fails on the place it misses, and some of those tests run only in another lane
---

# Bridge presses — every place a new route goes

**The page and the desktop share no code. A route the page sends `DAORIS.DRIVER` is spelled in the handler class that
answers it, in what it declares it changes, in the page's copy of that declaration, in the desktop README, in the bridge
hook that sends it, and in the class a terminal's press of it takes over the running Daoris's pipe. Ask Daoris names it
only where one is wanted (D233). Nothing but this list ties them together. Put a new route in every row below in the
same change.**

## Why

Each place is held by a test, and the tests sit in three suites in three lanes, so a branch that runs its own gates can
pass every one of them and still fail at the merge:

- **A copy of a mark drifts silently.** Eighteen presses once had no entry in the page's hand-copied publishing table, so a
  test of the page pressed them and heard no change; the page's table is generated from the marks now.
- **A shape is read too.** EVID1a added `evidence: []` to a route's answer and failed four family-rehearsal checks that
  compare that answer whole, which no branch runs.

## How to apply

**Work test first.** Write the modules case for the route (`Daoris.Desktop.Modules.Tests/Bridge/DriverModule*Tests.cs`: the
answer, each refusal by its code, the files or records it wrote) and the hook's test on the page, watch them fail, then
place the route one row at a time. The table tests below come last and hold the rest.

### The places

Paths are under `src/`. A **press** changes something; a **read** changes nothing and declares nothing (D169 point 2).

| # | Place | Where | What goes in | Held by |
|---|---|---|---|---|
| 1 | The handler | `Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/<Domain>Routes.cs`, the class named as the bridge file that sends it (`SessionsRoutes.cs` for `bridge/sessions/sessions.ts`) | A private `Task<object?> …Async(Payload, CancellationToken)` marked `[DriverRoute("ROUTE", PipeClass.…)]` (row 11), taking its payload record, named for its route, from the domain's `<Domain>Payloads.cs` (`RoutePayload` reads it off the request: a required field with no `?`, an optional one nullable, a flag `bool X = false`); an older handler still takes the `IpcRequest`. A refusal the person can act on is answered as data, `{ …, why, message }` with a code the page words, never thrown; one thrown is a `Refusals` code with its catalogue entries. A comment above it names the row and the decision point. | `DriverModuleRoutesTests`: `Each_route_is_marked_in_the_domain_named_as_the_bridge_file_that_sends_it`, `Every_route_the_page_calls_has_a_handler_in_the_table`, `Every_handler_in_the_table_is_called_by_the_page_or_a_test`; `RefusalCatalogueTests` for a thrown code |
| 2 | A new domain | `Daoris.Desktop/Daoris.Desktop.Modules/Bridge/DriverModule.cs`, `Domains` | One line, in alphabetical order, building the class over `context.Loop`, or over the `DriverContext` where it uses more; and its bridge file `Daoris.Web/src/bridge/<domain>.ts` and its README row (row 5). A route in an existing domain needs no line. | A class with no line is no part of the table, and `Each_route_is_marked_…` fails on its marks |
| 3 | What it changes | Beside the press's mark, the same file | `[Publishes("ROUTE", StateScopes.A \| StateScopes.B, Session = "field")]`: the scopes a successful answer moved, and the payload field naming the session it was about, where it is one session's (`Subject` for an entry or an ask, ACCEPTALL1e). A read carries none. A new scope is added after the others, with its family of query keys in `Daoris.Web/src/bridge/state/stateHub.ts` (D169 point 3). | `StatePublisherTests` (`Daoris.Desktop.Modules.Tests/Platform/`): a row `[InlineData("ROUTE", "scopes,in,wire,order", "field")]` in the theory of the note that declared it, and a read's row with `""` and `null` |
| 4 | The page's names | Generated: `node tools/contracts.mjs` writes `Daoris.Web/src/bridge/generated/routes.ts` from the marks (the web's pre-scripts do it when stale) | Nothing by hand: the route joins `DriverRoute`, which `call` takes, a press's `[Publishes]` joins `PUBLISHES`, which `test/publishing.ts` reads, and a person's press joins `PERSON_WORDS`, which the names check requires worded in every language. | `PageRoutesTests`; the web typecheck; `names-check --strict` in the web's build |
| 5 | The README | `Daoris.Desktop/README.md`, the routes table's row that begins with the domain's name in backticks | The route in backticks with its payload and what it answers, in the row's own prose, beside its siblings. | `DriverModuleRoutesTests.The_readme_names_each_route_in_its_domains_row` |
| 6 | The page's call | `Daoris.Web/src/bridge/<domain>.ts` | A hook calling `call('ROUTE', payload)` (or the forwarder `useDriverChange('ROUTE')`), with no type arguments for a route whose handler takes a record, since the generated `DriverPayloads` types its payload and answer: `useMutation` for a press, which asks nothing again by hand since the state hub does (D169 point 6); `useQuery` for a read, keyed under the family its scope invalidates (a session's helper asks sit under `['sessions', …]`, so `sessions` asks them again). The answer is read field by field, so a refusal keeps its code. The types of an answer a molecule draws live beside the molecule and the bridge file re-exports them, since `presentational.test.ts` refuses a molecule's import from `bridge/`, a type's included (`workflow/edit/answers.ts`, WORKFLOWEDIT1b). | Rows 1's route tests read the call's literal as text; the hook's own test presses it through `publishing` with the hub mounted |
| 7 | Ask Daoris's row | `Daoris.Desktop/Daoris.Desktop.Driver/Knowledge/Room/HelpRoomDoors.cs` | Only where one is wanted (D233 point 1): a row saying the act, its screen and its command, and whether Ask Daoris proposes it through a door its kind takes. No census asks a row of every press. | `HelpRoomDoorAsksTests` |
| 8 | A landing press | The route that builds `new LandingPlugins(` | The loop's `log:` and `health:` handed to it, and `PluginRecordWiringTests`' count of the landings the routes build raised by one (four today: the trees domain's two, an entry's or an ask's Accept, and the lines' clean-up). | `PluginRecordWiringTests.The_hook_set_and_every_landing_a_route_builds_are_handed_the_loops_log_and_record` |
| 9 | The words | `Daoris.Web/src/locales/{en,zh}/<area>.json` and the page's set of codes it words | Each refusal code answered as data, said in both languages (`work.<area>.refused.<code>`), and the page's set of known codes, so one a later driver adds is said in the driver's own sentence (`helperSaid` in `work/sessionActs.ts`). `translation-parity` holds the rest. | `src/locales` tests, `npm --prefix src/Daoris.Web run i18n:check` and `names:check -- --strict` |
| 10 | The records | The decision the route serves | A dated note naming the route, its payload, its answer, its codes and what it declares; the changelog for what a person sees. | `npm run -s verify` |
| 11 | The pipe's marks | Beside the handler's mark, the same file (`Platform/PipeMarks.cs` holds the marks; `RouteMarks` reads them at start into `PipeCensus`) | The class in the mark, `[DriverRoute("ROUTE", PipeClass.…)]`, the class a terminal's press of it takes over the running Daoris's pipe: `Open` for a read or a press D156 §3 leaves to an agent or the driver, `Person` for the person's yes, delete, clear, words or standing choice, and for a press the driver makes that moves their lines or branches, runs on their account or lets a refused account through; `Window` for what only a window does. Unsure is `Person`. A route that is a look in one form and a press in another says `[ClassedBy("ROUTE", field, class, values…)]` on the payload's field that tells them apart (`TOOLS_GIT`, `PLUGIN_UPDATE`). A `Person` or `Window` route says `[Owner("ROUTE", …)]`: the payload fields that name what it acts on, by kind, or `None` with its closed reason, or `ByPlan`; a setting that is never already says `[NotAlready("ROUTE", why)]`. A route whose marks are incomplete fails the module's first use. A terminal's press of a `Person` route waits for the person's Confirm on the window's card in *Notifications* and runs only then (RPC1c, `PipeConfirmations`); the card words it by its act, `person.route.<ROUTE>` in both catalogues (another module's `person.route.<MODULE>.<ROUTE>`), and names the values its `[Says("ROUTE", "field:role", …)]` gives a role (the session, the words, the files, a form), every other value listed under it, its words held by `PipeWordingTests`; and its grounds beside the mark (`Platform/GroundMarks.cs`: `[GroundSession]`, `[GroundRepository]`, `[GroundRead("ROUTE", nameof(PipeReads.…))]` and the rest, in the order they are read at its ask and again at its Confirm, or `[Timeless]` with its reason only where it deletes, moves and starts nothing); and its drain in the mark, `Drain = PipeDrain.…` (D156's note: `Waits` where, on any payload, it starts work an update's drain waits on, lets the look start what the drain's holds do not reach (every setting asks the look at once), starts a process that runs on past its answer, or changes a repository through git, `Passes` otherwise), said for every route with a person's or a role's form and only for those; a verb that sends one names it in the driver library's `PersonPresses`, which the census test holds. A route a terminal verb sends is also named in the driver library with the class its verb reads before it looks (`TreesRoutes`, RPC1b): reclassing it is a change to both, and opening one is its verb's client path, its words from the answer (the trees presses' routes carry them as `said`, `{ out, error, code }`, built by the driver library's `TreesWords`, which the alone path prints through too, RPC1b3). | `PipeCensusTests.Every_route_is_marked`, `Each_trees_press_names_its_route_as_the_census_classes_it`; `OwnerSetCensusTests.Every_mark_holds_against_its_handler`; `PipeWordingTests` for a `Person` route's row and words; `PipeGroundsCensusTests` for its grounds |

**A terminal verb as the running Daoris's client** (RPC1d, RPC1e) touches four places with row 11. The verb's library command
takes an `InstanceWorld?` and sends through `InstanceDoor.ThroughAsync` the payload `InstanceDoor.Said(...)` builds, the route's own
fields beside `said: true`, its own checks said before the send. The route's `said` branch runs the verb's own path with the
instance's world through `TerminalSaid`, so its lines are the alone path's and a refusal is an exit code, never thrown. The host's
console hands `InstanceHost.World(home, verb)`. The verb is named in `PersonPresses.Presses` or `OpenRoutes.Verbs`, or left alone there
with its reason, a list `PipeCensusTests` holds exact; a verb whose words name this machine's folders stays alone (the terminal RPC
design §4.3). Tested by the driver's `…ThroughInstanceTests` against `InstanceStandIn`, and a modules case holding the route's `said`
equal to the alone path over one home.

A route's answer is a wire shape: a field is added, never renamed or changed in place, and a rehearsal that compares the
answer whole reads its neighbours too (`tools/*rehearsal*.mjs`). Search them for the fields beside the one you add.

### A read

A route that only answers what is on disk or in the loop (a list, a plan, a gate, what waits) goes into fewer places, and
each is still held:

1. **Its handler** in the domain class named after the bridge file that sends it (row 1), with no `[Publishes]`: a read
   never declares a change (D169 point 2), and one that did would make every window read again without end (the
   `HARNESSES` split, D169's HARNESSREFRESH1 note). Its `StatePublisherTests` row says so, `""` and `null` (row 3), and it
   has no entry in the generated `PUBLISHES` (row 4). A read that fans out over every checkout (git per repository) says
   `[OneFlight("ROUTE")]` beside its mark, so the identical asks every window makes share the one under way (SWEEPFLIGHT1);
   `DriverModuleOneFlightTests` names the routes that do. A read whose answer is computed (a file, the service, git or a
   process before it answers) says `[ByEvent("ROUTE")]` instead (NOBLOCK1, D56's note): it answers from what is held and tells
   its work's answer as `DAORIS.ANSWERED`, its page hook asks through `askHeld` (`bridge/state/held.ts`), and its row goes in
   `DriverModuleHeldTests.Payloads`; a read that reads only memory is named in `WindowCensusTests.Held`. That census refuses a
   new read that is neither.
2. **Its domain's README row** (row 5), held by the same census, `The_readme_names_each_route_in_its_domains_row`.
3. **Its hook in that bridge file** (row 6), a `useQuery` enabled only where the shell answers it (`useShenora`'s
   `isAvailable`), its answer read defensively, an older shell's none read as empty.
4. **Its query key under the scope whose `STATE_CHANGED` should refresh it**: the hub's map in
   `Daoris.Web/src/bridge/state/stateHub.ts` turns each scope into key families (`sessions` into `keys.allSessions`), so a key that
   begins `['sessions', …]` is read again by every press that declares `sessions`, and by each tick the loop sends
   (`app/ShellSignals.tsx` asks the sessions again whatever the report says). A key under no scope's family is read again
   only by its own stale time and a window's focus.
5. **No coverage row** (row 7): a read changes nothing.
6. **Its class in its mark** (row 11): `PipeClass.Open`, since a read changes nothing.

A route that is a list in one form and a press in another (`[ClassedBy]`, row 11) carries the press's `[Publishes]`,
which the publisher tells after every answer, the list's included. So the page asks its list through a `useMutation`, never a
`useQuery` keyed under a family that mark invalidates, which would read the list again after each of its own answers without end
(KEEP1e's `SYSTEM_SPACE_FREE`, `useSpaceFree`).

A read whose shape keeps a `null` as a field (a fact not read, beside its `…Why`) cannot answer an object: the bridge leaves a
null out of what it serializes. Answer the reader's own JSON as a `JsonElement`, which is written as it is (`SystemRoutes`'
`SYSTEM_WIRING`, WIRING1b; `WorkflowRoutes`' steps). A read that looks at files leaves the window's thread first
(`Task.Run`), since a route's part before its first wait runs there (UIFREEZE1).

### Check before saying done

1. `dotnet test src/Daoris.Desktop/Daoris.Desktop.Modules.Tests --filter Category!=Process`, which holds rows 1 to 5, 8 and 11.
2. `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter FullyQualifiedName~HelpRoomDoorAsksTests` where row 7
   is wanted, where the lane allows it; otherwise the hand-back names the row.
3. The hook's own test file, every web test file that imports `test/publishing.ts`, `src/locales`, the web typecheck and
   the i18n checks.
4. `npm run -s verify`, with the index regenerated.

## Worked examples

- **MIXAGENT2b** (D163's note): `SESSION_HELPER_ASK` and `SESSION_HELPER_APPROVE`, presses declaring `sessions` and `driver`,
  and `SESSION_HELPERS`, a read under the sessions' key; its two hooks' coverage rows were added at HELPERNO1.
- **HELPERNO1** (D163's note): `SESSION_HELPER_DECLINE`, a press beside the yes, walked through rows 1, 3 to 7, 9 and 10 in
  one change; and MIXAGENT2c's `HELPER_WAITS` (D168's note), a read: no mark, its `StatePublisherTests` row saying so, no
  `PUBLISHES` entry, no coverage row, its key under the sessions' family.
- **ACCEPTALL1e** (D169's note): a declaration's `Subject`, a second payload field a press carries under way.

## What this does not cover

A press the page sends the service over HTTP (`data/queries.ts`): no bridge middleware sees it, so it declares nothing and
reaches other windows by the tick (D169's STATEHUB1d note), though its hook is still a coverage row. The browser module's
presses (`DAORIS.BROWSER`, `BrowserModule.Declared`) are another module's, read by the publisher beside the driver's. A
`setting` door that makes a press something Ask Daoris proposes is `setting-doors`.
