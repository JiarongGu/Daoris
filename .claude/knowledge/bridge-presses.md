---
name: bridge-presses
applies_when: adding a route the page sends the driver's bridge module (`DAORIS.DRIVER`), a press or a read, the way `SESSION_HELPER_DECLINE` and `HELPER_WAITS` were added, or a new route domain, or changing what an existing press changes
enforces: a new route goes into every one of its places in the same change, the domain's handler and its mark, what it changes and the page's copy of it, the README's row, the page's call, Ask Daoris's row for its hook, a landing's count; each place has a test that fails on the place it misses, and some of those tests run only in another lane
---

# Bridge presses — every place a new route goes

**The page and the desktop share no code. A route the page sends `DAORIS.DRIVER` is spelled in the handler class that
answers it, in what it declares it changes, in the page's copy of that declaration, in the desktop README, in the bridge
hook that sends it, in the class a terminal's press of it takes over the running Daoris's pipe, and, for a press, in Ask
Daoris's coverage of every control. Nothing but this list ties them together. Put a new route in every row below in the
same change.**

## Why

Each place is held by a test, and the tests sit in three suites in three lanes, so a branch that runs its own gates can
pass every one of them and still fail at the merge:

- **The coverage row is the driver's.** LAND3b's `useDiscardSessionBranch` and MIXAGENT2b's `useAskHelper` and
  `useApproveHelper` passed every gate their branches could run and failed `HelpCoverageTests`, which lives in the driver's
  suite and reads the page's hooks as text. A web or modules branch never runs it.
- **A place's view widens what a hook may press.** PROPOSEHERE1b's shared answer used `useSay` from a file a place's view
  imports, and the hook became one of that place's controls, which answered for no such press.
- **The page's copy of a mark drifts silently.** Before PUBLISHTWIN2 eighteen presses had no entry in the page's
  publishing seam, so a test of the page pressed them and heard no change; the twin is held both ways now.
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
| 1 | The handler | `Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/<Domain>Routes.cs`, the class named as the bridge file that sends it (`SessionsRoutes.cs` for `bridge/sessions/sessions.ts`) | A private `Task<object?> …Async(IpcRequest, CancellationToken)` marked `[DriverRoute("ROUTE")]`, its payload read with `PayloadHelper`. A refusal the person can act on is answered as data, `{ …, why, message }` with a code the page words, never thrown; one thrown is a `Refusals` code with its catalogue entries. A comment above it names the row and the decision point. | `DriverModuleRoutesTests`: `Each_route_is_marked_in_the_domain_named_as_the_bridge_file_that_sends_it`, `Every_route_the_page_calls_has_a_handler_in_the_table`, `Every_handler_in_the_table_is_called_by_the_page_or_a_test`; `RefusalCatalogueTests` for a thrown code |
| 2 | A new domain | `Daoris.Desktop/Daoris.Desktop.Modules/Bridge/DriverModule.cs`, `Domains` | One line, in alphabetical order, building the class over `context.Loop`, or over the `DriverContext` where it uses more; and its bridge file `Daoris.Web/src/bridge/<domain>.ts` and its README row (row 5). A route in an existing domain needs no line. | A class with no line is no part of the table, and `Each_route_is_marked_…` fails on its marks |
| 3 | What it changes | Beside the press's mark, the same file | `[Publishes("ROUTE", StateScopes.A \| StateScopes.B, Session = "field")]`: the scopes a successful answer moved, and the payload field naming the session it was about, where it is one session's (`Subject` for an entry or an ask, ACCEPTALL1e). A read carries none. A new scope is added after the others, with its family of query keys in `Daoris.Web/src/bridge/state/stateHub.ts` (D169 point 3). | `StatePublisherTests` (`Daoris.Desktop.Modules.Tests/Platform/`): a row `[InlineData("ROUTE", "scopes,in,wire,order", "field")]` in the theory of the note that declared it, and a read's row with `""` and `null` |
| 4 | The page's copy | `Daoris.Web/src/test/publishing.ts`, `DECLARED` | `ROUTE: { scopes: [...], session: '...' }` for every press that declares a change, in the shape the test reads; a read has no entry. | `StatePublisherTests`: `The_page_s_publishing_seam_copies_each_press_s_mark` and `Every_mark_has_an_entry_in_the_page_s_publishing_seam` (a twin, `twins.md`); and every web test file that imports it |
| 5 | The README | `Daoris.Desktop/README.md`, the routes table's row that begins with the domain's name in backticks | The route in backticks with its payload and what it answers, in the row's own prose, beside its siblings. | `DriverModuleRoutesTests.The_readme_names_each_route_in_its_domains_row` |
| 6 | The page's call | `Daoris.Web/src/bridge/<domain>.ts` | A hook calling `call('ROUTE', payload)` (or the forwarder `useDriverChange`): `useMutation` for a press, which asks nothing again by hand since the state hub does (D169 point 6); `useQuery` for a read, keyed under the family its scope invalidates (a session's helper asks sit under `['sessions', …]`, so `sessions` asks them again). The answer is read field by field, so a refusal keeps its code. | Rows 1's route tests read the call's literal as text; the hook's own test presses it through `publishing` with the hub mounted |
| 7 | Ask Daoris's row | `Daoris.Desktop/Daoris.Desktop.Driver.Tests/Help/HelpCoverageTests.cs` | A hook that changes something, pressed anywhere on the page, is a row of `Starts` named by the top folder it is pressed from (`("work", "useDeclineHelper", null, …)`), or of `Controls` where a Settings domain or a place presses it: a `Door` a kind takes, an `Exempt` with its reason, an `Owed` with what it waits on, or `Own(place, …)`, the session's or quest's own act. A file a place's view imports (AgentsView, ProjectsView, PluginsView) joins that place's controls, so a hook shared with it may press only what that place answers for. A read is no row. | `Every_start_on_the_page_is_a_door_owed_exempt_or_its_own_conversation`, in the **driver's** suite: a branch outside the driver lane names the row in its hand-back, and the parent adds it at the merge |
| 8 | A landing press | The route that builds `new LandingPlugins(` | The loop's `log:` and `health:` handed to it, and `PluginRecordWiringTests`' count of the landings the routes build raised by one (four today: the trees domain's two, Ask Daoris's, and an entry's or an ask's Accept). | `PluginRecordWiringTests.The_hook_set_and_every_landing_a_route_builds_are_handed_the_loops_log_and_record` |
| 9 | The words | `Daoris.Web/src/locales/{en,zh}/<area>.json` and the page's set of codes it words | Each refusal code answered as data, said in both languages (`work.<area>.refused.<code>`), and the page's set of known codes, so one a later driver adds is said in the driver's own sentence (`helperSaid` in `work/sessionActs.ts`). `translation-parity` holds the rest. | `src/locales` tests, `npm --prefix src/Daoris.Web run i18n:check` and `names:check -- --strict` |
| 10 | The records | The decision the route serves, `docs/index/` | A dated note naming the route, its payload, its answer, its codes and what it declares; the index regenerated by `node tools/orient-index.mjs`, never edited; the changelog for what a person sees. | `npm run -s verify` |
| 11 | The pipe's census | `Daoris.Desktop/Daoris.Desktop.Modules/Platform/PipeCensus.cs`, `Driver()` | The route under the class a terminal's press of it takes over the running Daoris's pipe (RPC1a, D171 point 3, the terminal RPC design §4.2): `Open` for a read or a press D156 §3 leaves to an agent or the driver, `Person` for the person's yes, delete, clear, words or standing choice, and for a press the driver makes that moves their lines or branches, runs on their account or lets a refused account through; `Window` for what only a window does. Unsure is `Person`. A route that is a look in one form and a press in another takes a `PipeRule` on the payload's field that tells them apart (`TOOLS_GIT`, `PLUGIN_UPDATE`). A terminal's press of a `Person` route waits for the person's Confirm on the window's card in *Notifications* and runs only then (RPC1c, `PipeConfirmations`); the card words it by its act, `person.route.<ROUTE>` in both catalogues (another module's `person.route.<MODULE>.<ROUTE>`), and names the values its row in `Platform/PipeWording.cs` gives a role (the session, the words, the files, a form), every other value listed under it (NOTIFY2b): a `Person` route needs both its row and its words, which `PipeWordingTests` holds; a verb that sends one names it in the driver library's `PersonPresses`, which the census test holds. A route a terminal verb sends is also named in the driver library with the class its verb reads before it looks (`TreesRoutes`, RPC1b): reclassing it is a change to both, and opening one is its verb's client path, its words from the answer (the trees presses' routes carry them as `said`, `{ out, error, code }`, built by the driver library's `TreesWords`, which the alone path prints through too, RPC1b3). | `PipeCensusTests.Every_route_of_the_driver_is_classed_and_every_class_names_a_route`, `Each_trees_press_names_its_route_as_the_census_classes_it`; `PipeWordingTests` for a `Person` route's row and words |

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
   has no entry in `DECLARED` (row 4). A read that fans out over every checkout (git per repository) says
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
5. **The index regenerated** (`node tools/orient-index.mjs`, row 10). A read is no coverage row (row 7): it changes nothing.
6. **Its class in the pipe's census** (row 11): `Open`, since a read changes nothing.

A route that is a list in one form and a press in another (a `PipeRule` in the census, row 11) carries the press's `[Publishes]`,
which the publisher tells after every answer, the list's included. So the page asks its list through a `useMutation`, never a
`useQuery` keyed under a family that mark invalidates, which would read the list again after each of its own answers without end
(KEEP1e's `SYSTEM_SPACE_FREE`, `useSpaceFree`).

A read whose shape keeps a `null` as a field (a fact not read, beside its `…Why`) cannot answer an object: the bridge leaves a
null out of what it serializes. Answer the reader's own JSON as a `JsonElement`, which is written as it is (`SystemRoutes`'
`SYSTEM_WIRING`, WIRING1b; `WorkflowRoutes`' steps). A read that looks at files leaves the window's thread first
(`Task.Run`), since a route's part before its first wait runs there (UIFREEZE1).

### Check before saying done

1. `dotnet test src/Daoris.Desktop/Daoris.Desktop.Modules.Tests --filter Category!=Process`, which holds rows 1 to 5, 8 and 11.
2. `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process`, which holds row 7, where the
   lane allows it; otherwise the hand-back names the row.
3. The hook's own test file, every web test file that imports `test/publishing.ts`, `src/locales`, the web typecheck and
   the i18n checks.
4. `npm run -s verify`, with the index regenerated.

## Worked examples

- **MIXAGENT2b** (D163's note): `SESSION_HELPER_ASK` and `SESSION_HELPER_APPROVE`, presses declaring `sessions` and `driver`,
  and `SESSION_HELPERS`, a read under the sessions' key; its two hooks' coverage rows were added at HELPERNO1.
- **HELPERNO1** (D163's note): `SESSION_HELPER_DECLINE`, a press beside the yes, walked through rows 1, 3 to 7, 9 and 10 in
  one change; and MIXAGENT2c's `HELPER_WAITS` (D168's note), a read: no mark, its `StatePublisherTests` row saying so, no
  `DECLARED` entry, no coverage row, its key under the sessions' family.
- **ACCEPTALL1e** (D169's note): a declaration's `Subject`, a second payload field a press carries under way.

## What this does not cover

A press the page sends the service over HTTP (`data/queries.ts`): no bridge middleware sees it, so it declares nothing and
reaches other windows by the tick (D169's STATEHUB1d note), though its hook is still a coverage row. The browser module's
presses (`DAORIS.BROWSER`, `BrowserModule.Declared`) are another module's, read by the publisher beside the driver's. A
`setting` door that makes a press something Ask Daoris proposes is `setting-doors`.
