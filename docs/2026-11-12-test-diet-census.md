# Census: the test code and fixtures (TESTDIET1)

TESTDIET1 (`TASKS.md`, *Code structure and the process*) asks for a census before any cut: each suite's files, bytes, cases and
wall time; the tests that prove only a removed door or a word's spelling; the duplicated fixtures and stand-ins; the goldens and what
each stands for; the files over the size budget; and the scratch left under `_fixtures/`. This document is that census, and the cut it
proposes, in the order of what each saves. It measures and changes no test. Paths are repository-relative; the tree is main at
`91fc3fa49b`.

## 0. How it was measured, and what was not

- **Files, bytes, static cases:** one script over `git ls-files` (a test file is one holding a `[Fact]`/`[Theory]` or, in the web, an
  `it(`/`test(`; everything else of a suite's folder is a helper or a data file). Bytes are LF-ending bytes, as the size ratchet reads
  them. A *static case* is a `[Fact]`, a `[Theory]`'s `[InlineData]` rows (its member and class data counted as one source each), or a
  `it(`/`test(` call; a `.each` table and a member-data grid expand at run time, so the **run** figure is the truer count.
- **Run cases and wall time are recorded, not re-run.** The person's rule for this machine bars a whole test project, a Process half or
  `npm run verify` from a session (they take hours), so no suite was timed here. The figures are the newest full CI run's
  (`local/scratch/ci-70da37d`, main `70da37d6`, 2026-11-11, `status.json` and the gate logs and `.trx` files), the median and range of the
  36 CI runs under `local/scratch/` whose gate passed, and the *alone, quiet machine* figures already written in
  `docs/areas/code-structure-and-process.md` rule 17. **A CI run shares the machine between three light gates and a Process lane, which
  turns a minute of work into ten (rule 17): no figure below beside another run is a measure of the suite.** Step 1 of the cut is to
  take the before-figures alone.
- **Judgement findings** (a proof of a removed thing, a duplicate) come from two read-only agents' sweeps plus my own counts. They say
  what they read and what they inferred; items the sweeps did not reach are listed in §7.

## 1. The suites

All sizes are tracked files only.

| Suite (gate) | Test files | Helper / data files | Bytes (code) | Static cases | Run cases (CI `70da37d`) |
|---|---|---|---|---|---|
| **driver** (`driver`, `Category!=Process`) | 1,740 | 165 code, 127 data (2.85 MB) | 20.57 MB | 11,896 facts, 1,463 theories (6,604 rows) | 20,610 (24 failed in that run) |
| driver, Process halves (`driver-process`, `-git`, `-git-pinned`) | the same files | | | 446 classes (243 + 197 + 6) | 1,675 + 1,503 + 21 |
| **modules** (`modules`) | 317 | 26 code, 4 data | 3.66 MB | 2,105 facts, 233 theories (1,332 rows) | 3,443 (8 failed) |
| modules, Process half | the same files | | | 31 classes | 198 |
| **service** (`service`) | 215 + 49 (Http) | 17 + 7 | 2.91 MB + 0.65 MB | 1,856 + 276 facts, 209 + 22 theories | 3,103 + 391 |
| service, Process half | | | | 3 classes | 1 recorded (flake, 101 s) |
| devkit (`devkit`) | 15 | 0 | 0.09 MB | 100 facts, 15 theories | 147 |
| **web unit** (`web-unit`, vitest) | 679 | 21 | 7.18 MB | 7,737 `it(`/`test(` | 12,857 in 669 files |
| **web measured** (`web-measured`, Playwright) | 21 | 2 | 0.15 MB | 52 | not completing in the recorded runs |
| web e2e (`web`, Playwright) | 1 | 0 | 0.06 MB | 24 | 24 passed |
| **cli** (`src/Daoris.Cli/test`, in `verify`) | 75 | 6 data (42 KB) | 0.84 MB | 748 | about 750 (`node --test`, 98 s) |
| **tool tests** (`tool-tests`: `tool-test/**/*.ts` + `as-merged.test.mjs`) | 40 | | 1.15 MB | 605 | 734 |
| tests of `tools/` (`tools/*.test.mjs`, in `verify`) | 39 | | 0.40 MB | 442 | |

All suites together: **40.7 MB of tracked test code and data**, of which the driver alone is 23.4 MB (57%): 20.6 MB of C# across
1,905 files and 2.85 MB of fixtures and goldens. The driver's folders by bytes: `Workplace` 4.5 MB (441 files), `Agents` 3.0 MB, `Loop`
2.4 MB, the fixtures 2.3 MB, `Course` 1.6 MB, `Workflows` 1.5 MB, `WorkOrders` 1.2 MB.

### Wall time, recorded

CI run `70da37d` (loaded; the wall of the gate), the median and range of the passing runs, and where rule 17 has an alone figure:

| Gate | CI `70da37d` | Median (range), passing runs | Alone, rule 17 |
|---|---|---|---|
| driver (fast half) | 14.8 min (10 m 10 s of tests) | 14.6 (9.2–25.9), n=7 | 6 m 57 s for 19,184 tests; 3,161 s of test time over 1,029 classes |
| modules | 10.0 min (8 m 2 s) | 7.7 (2.4–22.0), n=13 | about 4 min of test time, 3,006 tests |
| service (both projects) | 7.8 min (5 m 34 s + 3 m 53 s) | 9.2 (6.3–22.6), n=15 | |
| web unit | 7.4 min (vitest 429.7 s) | 11.7 (7.6–18.9), n=11 | 12 m 57 s on 2026-11-08; 8 m 24 s with five workers on 2026-11-09 |
| web measured | 5.1 min, stopped | never passes in the recorded runs | |
| web (build + e2e) | 3.7 min (24 specs 32 s) | 6.4 (3.2–14.1), n=23 | |
| cli (`npm run verify`) | 2.5 min, red | 8.3 (6.4–19.7), n=6 | |
| tool tests | **172 min** | 13.5 (11.3–16.5), n=7 | 16 m 30 s beside others (2026-11-06) |
| devkit | 1.0 | 1.6 (0.6–5.5) | |
| driver-process | 99.5 min | | |
| driver-process-git | 173.5 min | 84.6 (35.3–174.3), n=10 | |
| driver-process-git-pinned | 2.9 | 2.9 | |
| modules-process | 10.5 | 4.9 (2.4–11.4) | |
| service-process | 135.8 (a flake) | 2.2 (1.0–4.1) | |

The tool-tests gate's 172 minutes in that run is one hang, not a cost of the tests: *the queue, end to end in a scratch repository*
spent 9,619 s in `TOOLTESTHANG1: an invocation with no verdict is ended at its bound` (the test that proves the bound) while the rest of
the 734 tests took 335 s. The same run's other slow cases are the merge tool's end-to-end cases (857 s for *the tool, end to end*, 775 s
for each of two INTEGRATE1b cases, 616 s for QUEUEDRAIN1).

**Where the Process halves spend it** (`.trx` of the same run, summed test time; a test's time there includes the load it ran
under, so read the shape, not the seconds):

- `driver-process`: 1,675 tests, 287 classes, 21,206 s. The top 25 classes are 55%; 77 tests of over 60 s make 8,282 s. Largest:
  `SessionMessagesTickTests` 1,973 s (26 tests), `GitSshPinnedProcessTests` 691 s, `GitSshProcessTests` 658 s,
  `DrivenMoveDowngradeProcessTests` 619 s, `AccountRotationTickTests` 573 s.
- `driver-process-git`: 1,503 tests, 212 classes, **38,874 s**. The top 25 classes are 51%; 141 tests of over 60 s make 16,369 s.
  Largest: `HelpTreesReadTests` 1,407 s (6 tests), `IntegrationFormGitTests` 1,390 s, `PoolUndoGitTests` 1,269 s,
  `PullRequestStateTests` 1,075 s, `LandBacklogLandingTests` 1,006 s, `TreePoolFinishGitTests` 996 s.
- `modules-process`: 198 tests, 32 classes, 416 s; `service-process`: one test (`RouteBudgetProcessTests`).

The light halves, alone: rule 17 names the driver's slowest classes (`LineAccountTests`, `StructureTests`, `AcpBackgroundTests`,
`SessionsOutliveTheirLookTests`, `StagedBuildTests`, `TreeScratchTests`) and says the web's unit time is flat: 4,287 tests of 100 ms or
more make 92% of it, and 805 tests of 0.5 to 2 s make 714 s of 1,702 s (`design/stories.test.tsx`, 3,434 renders, 164 s).

## 2. Tests that prove only a removed door or a word's spelling

Two read-only sweeps (method-name regexes over the C# tests, 212 hits sorted by hand, then reading about 90 files; the web's
catalogue suites; the fixtures' own notes). *Not reached:* `tools/*.test.mjs` (39 files, only counted), the devkit's tests, the CLI's
doctrine suites (their "retired" is the live sync semantics for retired canon files), and the service's tests beyond their names.

### 2a. A removed door (about 20 files touched, about 45 cases, about 30 KB of case code)

High confidence, whole file or whole case:

- `Driver.Tests/Platform/RetiredDriverGroupCensusTests.cs` (2.8 KB, 1 case): no tracked file spells the retired `daoris driver`.
- `Driver.Tests/Agents/OldSeamNamesCensusTests.cs` (2.1 KB, 1): no source names `ISessionAdapter`, `LegacyDoor`, `AdapterTable`.
- `Driver.Tests/Platform/DaorisDriverWordsTests.cs` (4.3 KB, 1): no taught word says `daoris-driver` (also a spelling pin).
- Six `ONEWAY1c_i_*` cases (one each in `Settings/ConfigSeamTests`, `DriverHostTests`, `Instance/VerbLeasesTests`,
  `Loop/SystemWiringTests`, `Roles/RoleStaffingTests`, `Settings/DriverChoicesCommandTests`): the retired variable
  `DAORIS_DRIVER_CONFIG` is read nowhere. About 7 KB of case code in files of 7 to 34 KB.
- One case each: `DriverChoicesCommandTests.Trees_is_no_verb_and_writes_nothing`, `HelpCommandTests.Resume_is_no_verb_of_this_family`,
  `WorkflowGraphPresetsTests.New_from_a_repository_s_Current_is_retired…`, `Modules.Tests/Bridge/DriverModuleTreesTests.The_merge_alone_is_no_route`.
- About 17 `DoesNotContain("…_propose")` assertions in `Knowledge/HelpRoomTests.cs:135-154`, which the `EndsWith("_propose")` assertion
  after them already covers; and five one-line absences inside live tests (`PlaceHandTests:76` `DAORIS_HOST_TOKEN`, `HelpRoomDoorsTests:287`,
  `PlacePlanTests:59`, `LandingSquashTests:220` for `place landing`/`SET_LANDING`, `DriverModuleSystemTests:65` `SYSTEM_TOOL_CLEAR`).

Medium confidence, half of a test is the absence and half is live: `DriverCommandTests.The_usage_names_place_adoption_and_the_setup_verb_is_gone`,
`WorkflowAsGraphCommandTests.New_from_a_version_of_steps_is_refused_and_the_step_presets_are_gone`, `DriverModuleActorTests` (`ACTORS` is no
route), one case each of `TerminalConversationCensusTests` and `ConversationRunCensusTests` (the same seam as `OldSeamNamesCensusTests`),
`InstructionAccountTests.The_pieces_the_standing_block_replaced_are_gone`, the web's `locales/names.test.ts` (old Setup words forbidden)
and `help/places.test.ts:184`, the CLI's `status --machine` refusal row (791 B). Low: `StoredOldNamesCensusTests`, `WorkflowDraftToldTests`,
`HelpRoomPlacesTests…neither_retired_domain`.

### 2b. Recordings of a retired implementation: the seven `*-text.json` fixtures (14 files, 1.47 MB, 797 rows)

Each fixture's own `note` says it records what a **retired TypeScript CLI verb** printed, run once before the verb retired (PUSH1f2a to
f2f, ONEWAY1u, D194), so the C# port could be held to the same words. The C# verbs are live and the fixtures now pin their exact
sentences (and each row's exit and the files it wrote). The recorded implementation is gone; the pin is a spelling pin.

| Fixture (`Driver.Tests/fixtures/`) | Bytes | Rows | Reader (bytes) | Stands for |
|---|---|---|---|---|
| `driver-workflows-text.json` | 485,943 | 177 | `Platform/DriverWorkflowsTextTests` (9,293) | `workflow`, `brainstorms` |
| `agents-text.json` | 399,592 | 261 | `Agents/AgentsTextTests` (13,862) | `agent rules`, `trust`, `settings`, `profile`, `key` |
| `driver-choices-text.json` | 325,852 | 140 | `Settings/DriverChoicesTextTests` (6,232) | the standing choices (`keep`, `review`, `across` …) |
| `plugins-text.json` | 72,578 | 66 | `Plugins/PluginsTextTests` (7,069) | `daoris plugin` |
| `driver-files-text.json` | 43,827 | 61 | `Settings/DriverFilesTextTests` (8,320) | `remote`, `browser` |
| `tools-text.json` | 41,500 | 52 | `Install/ToolsTextTests` (8,803) | `daoris tool` |
| `driver-registry-text.json` | 35,169 | 40 | `Service/DriverRegistryTextTests` (8,395) | `connect`, `retire`, `wire`, `import` |

The rows carry a `changed` field where the C# verb deliberately says something else than the TypeScript one (76 of the agents' rows,
21 of the workflows', 19 of the choices', 14 of the plugins', 7, 5, 1): a recording whose expected words were hand-edited is no longer
a recording. Whether each `*CommandTests` already holds the exit and the files-after is not checked (§7); `DriverChoicesTextTests` is
red on main (`docs/archive/2026-11-11-cleanup-census.md` §6). Also 19 shared-table fixtures say in `about` that "the CLI's X.ts and the
driver's Y both read it" where the CLI side is gone: only the wording is stale.

### 2c. Other spelling pins

- The goldens (§5): 64 files, 0.54 MB, deliberately a review aid; **left to the owner**.
- `Cli/test/one-word.test.ts` (2.8 KB, about 3 cases): "circle" and "workspace" are not both said for the scope. Pure spelling.
- The web's catalogue suites (`i18n`, `i18nCheck`, `catalogueKeys`, `kindWords`, `personWords`, `shippedNames`, `glossary`,
  `names`) are parity gates between languages, not spellings: they stay.

### 2d. Transitional, not deletable yet

Each stands on a condition not met, so none is a cut now: the migration steps' tests (23 files, 107 KB, about 91 cases) go with their
`nocompat1` steps at the next release that changes a stored shape (install rule 54); `Course/MoveStoreCensusTests.cs` (17.8 KB, 6 cases:
the older build's move boxes) names no ending row, which is a gap to close in the cut, not a deletion; the service's store-migration
cases (`SessionDoorColumnTests`, `MemoryAuthorColumnTests`, four `*_migrated_in_place`) cover stores still in the wild.

**Correction to `docs/archive/2026-11-11-cleanup-census.md` §1:** it says the text fixtures hold the `trees` refusal. They do not: `trees`
occurs twice in `driver-choices-text.json` (as a kind name, `keep trees`) and never in `driver-workflows-text.json`; the proof is
`DriverChoicesCommandTests.Trees_is_no_verb_and_writes_nothing`.

## 3. Duplicated fixtures and stand-ins

Counted by pattern over the C# test projects and the web's tests; the line and byte savings are estimates unless marked.

| # | Group | Measured | Proposed shared builder | Saves |
|---|---|---|---|---|
| 1 | **Scratch home field + `Dispose` delete** | 579 `Path.Combine(TestHomes.Root, "x" + Guid…[..8])` lines in 543 files; 672 `Directory.Delete(` lines, 271 of them one identical `try { … } catch (IOException) { } catch (UnauthorizedAccessException) { }`; Modules has `ScratchFolders.Remove` (retrying) that 270 plain deletes ignore; 119 files in the four service/devkit/Http projects root scratch in the OS temp, against the repository's rule | a linked `ScratchHome : IDisposable` (`Path`, `Combine`, retrying `Dispose` over `ScratchFolders.Remove`) | about 3,000 lines, 200 KB |
| 2 | **HTTP stand-ins for the service** | 166 `: HttpMessageHandler` classes in Driver and Modules, 5,800 lines, 306 KB, 106 of them ≤ 15 lines; names repeat (`StandIn` 41, `StandInService` 15, `EmptyService`, `Canned`, `Refusing`, `NoService`, `Throwing`); 166 `Content = new StringContent(` lines in 138 files; 17 files carry their own ~40-line `HttpListener` + port probe + `ServeAsync` shell; the same `/api/registry` body repeats across the `DriverModule*` classes | a `RoutedHandler` (`Get`, `Post`, recorded `Requests`, fallback `[]`), a `LoopbackService` that owns the listener shell, one registry-body helper | about 2,000 lines, 100 KB |
| 3 | **Git fixtures still hand-rolled** | `GitSeed` is in 509 driver files (the seeded copy exists), but 155 driver files and 13 more in Modules and Service still `git init` their own; `user.email` config in 149 files; about 27 private `Git`/`GitAsync(cwd, …)` runners beside `GitFixture.RunAsync` (whose header says the copies are where a 1 h 33 m hang came back); 30 files use `BuildPlainAsync`/`CopyOf` | `GitSeed.PlainAsync` as the default, a `GitSeed.WithRemoteAsync` shape, the private runners removed for `GitFixture.GitAsync`, and a census test that holds it | about 1,300 lines, 70 KB, and the larger saving is wall time (§1: 38,874 s in the git half) |
| 4 | **Web: `vi.mock('@shenora/react')` + `vi.hoisted` bridge** | 143 mock blocks in 143 files (936 lines, 38.8 KB, 61 distinct), 136 hoisted blocks (459 lines, 42 distinct); only 11 files use `test/workFrameBridge.shenoraMock()`, about 14 `sliceShell.mockedShell` | extend `test/sliceShell.ts` with `shenoraMock({ events?, original? })` and one `hoistedBridge()`; each file keeps a two-line `vi.mock` (it must stay literal per file) | about 1,000 lines, 45 KB |
| 5 | **Web: fetch routers, `QueryClient` wrappers, record builders** | `vi.stubGlobal('fetch', …)` 234 hits in 94 files (75 identical `vi.fn(async (input) => respond(String(input)))`); `function respond(url)` in 39 files, 12 identical; `new QueryClient(` 253 hits in 112 files, 26 own `wrapper`s; `session` builders in 39 files (34 distinct), `quest` in 25 (23 distinct) though `quests/aQuest.ts` exists | `test/fetchRoutes.ts`, `test/renderWithQuery.tsx`, `test/aSession.ts` | about 600 lines, 40 KB |
| 6 | **Domain record builders (C#)** | 33 `static QuestView Quest(…)` (7 with one identical tail), 36 `Record(…)` session-record builders and 11 typed ones, 33 `Ask(…)`, 28 `Lines(…)`; `HistoryStandIn.cs` already has `Ask/Quest/Record` | a linked `Fixtures` with named defaults | about 500 lines, 30 KB |
| 7 | **Private wait helpers** | 23 byte-identical `private static async Task UntilAsync(Func<bool>)`, 7 `Settled(Task)`, 12 `Until` variants; `Driver.Tests/Poll.cs` exists and Modules, Service and Http each carry a 0.6 KB copy | link the one `Poll.cs`, add `Poll.Settled`, delete the privates | about 400 lines, 15 KB |
| 8 | **Clocks** | four timer-firing manual clocks (`MovedClock` ×2, `MovableClock`, `HeldClock`+`HeldTimer`; the driver's is the service's plus `Waiting`/`Pending`) and about 10 tiny fixed ones (`FixedClock`, `Clock`, `FixedTime`, `SteppedTime`) | one linked `MovedClock` (the superset), `Clocks.Fixed(at)` | about 300 lines, 11 KB |
| 9 | **Per-project infra copies** | `StructureCheck.cs` 25.9 / 12.3 / 6.4 KB (the comments call two of them "a trimmed copy"), `SourceFiles.cs` 3.9 / 3.8 / 2.5 KB (only the namespace, project constant and a `Module()` differ), `Category.cs` 3.9 / 1.2 / 0.6 KB; Modules already links 17 driver files | link, as Modules does | about 20 KB |
| 10 | Devkit | four `FakeGit : IGit` in `Daoris.Devkit.Tests` | one | small |

Counted, not duplicated: `OwnerStandIn` (one 1.3 KB file), the fake ACP agent (`FakeAgent` in `AcpTests.cs`, one `TestAgent`). The shared
stand-ins that exist (`QuestStandIn`, `ReviewStandIn`, `OpinionStandIn` 36 KB, `HistoryStandIn`, `SetupStandIn`, `StandInLedger` 65 KB) each cover
only their own domain. Group totals: about 7,000 lines and 560 KB in the C# and web test code, before the shared builders' own size.
*Not compared:* fixture JSON against fixture JSON (the driver's `fixtures/` has 50 files with no overlap known), the CLI's and `tools/`'s
helpers (`tool-test/support/`, `tools/stand-ins.mjs`), and `LineAccountTests`' grid, which rule 17 says built a scratch home per case.

## 4. Goldens and what each stands for

No framework snapshots exist (no `toMatchSnapshot`, no `__snapshots__`). The goldens are hand-kept files a test renders against, byte for
byte; the whole set is 123 files (the driver's 2.76 MB, with its fixtures) and the goldens proper are:

| Golden | Files | Bytes | Stands for | Read by |
|---|---|---|---|---|
| `Knowledge/golden/help-room/` | 6 | 286,866 | Ask Daoris's room as the helper reads it: five renders of 53 to 62 KB (`empty`, `sparse`, `scoped`, `full`, `full-access`) and `settings.json` | `Knowledge/HelpRoomGoldenTests` |
| `golden/target-prompt/` | 34 | 148,298 | a quest's instruction as the harness reads it: 17 cases as `.md` plus `.account.json` (42.8 KB of accounting) | `Agents/TargetPromptGoldenTests` |
| `golden/opinion-prompt/` | 3 | 13,560 | the second-opinion instruction | `Review/OpinionPromptGoldenTests` |
| `golden/intake-prompt/` | 2 | 7,436 | the intake instruction | `WorkOrders/IntakePromptGoldenTests` |
| `golden/helper-prompt/` | 2 | 4,557 | a helper's instruction | `Participants/HelperPromptGoldenTests` |
| `golden/brainstorm-rules/` | 2 | 4,172 | the brainstorm rules | `BrainstormPostureTests` |
| `golden/partner-prompt/`, `opinion-command/`, `workflow-run/`, `trees-land-plan/` | 9 | 5,019 | small instructions and plans | their own tests |

What the headers say they are for: a sentence "added, moved or reworded shows here as the whole instruction's difference", so
review reads the instruction as the session will. That is a real purpose and also the cheapest thing to over-pay for: the five
help-room renders are 2,036 lines of which **471 are distinct** (`empty`, `sparse`, `full-access` share 343, 342 and 413 of their
lines with `full`), and the target prompt's 17 `.md` files are 1,518 lines of which **188 are distinct**. A golden of the one full
render plus an assertion per section that varies (the assertion the "three machines take every branch" comment already names) would
hold the same rules in a fifth of the bytes; the owner decides whether review's byte-for-byte reading is worth the rest (§6, row 5).

The other recorded data (`fixtures/`): the seven text fixtures of §2b (1.40 MB of the 2.22 MB), the next largest are
`graph-problems.json` (90 KB, a table shared with the CLI's twin test: its `about` names the CLI's `graphproblems.test.ts`, which should
be checked to exist), `workflow-named.json` (80 KB), `vendor/codex/0.156.1/release.json` (53 KB, a vendor record), `workflow-selection.json`
(54 KB), `workflow-graph-versions.json` (44 KB), `failure-judging.json` (42 KB), `provider-census.json` (42 KB).

## 5. Files over the size budget (`tools/size-ratchet.json`)

The budget is 40 KB (40,960 bytes). The list holds **115 files, 8,712,540 bytes**; **110 are test files, 7,684,733 bytes (88%)**, and the
test files stand **3.18 MB above the line** they are held to. Only five sources are on the list (`tools/merge-branch.mjs` 430 KB,
`family-rehearsal.mjs` 368 KB, `deployment-rehearsal.mjs` 107 KB, `merge-queue.mjs`, `rehearsal-kit.mjs`).

| Group | Files | Bytes | Above 40 KB |
|---|---|---|---|
| driver tests | 56 | 3,413,883 | 1,120,123 |
| web unit tests | 26 | 1,837,898 | 772,938 |
| service tests (+ Http) | 11 | 809,880 | 359,320 |
| CLI tool-test | 4 | 782,387 | 618,547 |
| modules tests | 7 | 512,450 | 225,730 |
| CLI test | 3 | 181,362 | 58,482 |
| web e2e / measured | 1 | 56,527 | 15,567 |
| `tools/` test, other | 2 | 90,346 | 8,426 |

The largest: `tool-test/merge-branch.test.ts` 408,768 (178 cases), `tool-test/merge-queue.test.ts` 239,201 (159), `Agents/AcpTests.cs`
181,209, `Http.Tests/LocalHostTests.cs` 168,117, `Agents/AcpBackgroundTests.cs` 143,413, `Modules.Tests/Loop/AttentionStateSliceTests.cs`
134,496, `Sessions/SessionLedgerTests.cs` 120,334, `WorkOrders/PlannerTests.cs` 118,458, `web/agents/AgentsView.test.tsx` 117,579,
`Remote/QuestSyncTests.cs` 110,685, `web/work/WorkFrame.landed.test.tsx` 109,241, `Loop/SessionMessagesTickTests.cs` 107,204 (also the
slowest Process class, 1,973 s). The ratchet only holds a file from growing; it does not say which of its bytes is a duplicate. The two
`tool-test` files carry 648 KB of the 782 KB the ratchet lists for the CLI's tool tests, and their end-to-end cases are the
600-to-860-second ones.

## 6. Scratch left under `_fixtures/`

`_fixtures/` is gitignored and every test project roots its scratch there (`GitSeed.RepoRoot()`, `TestHomes.Root`); a test removes its own
(`GitSeed.Remove`, `GitSeed.Discard`). What is on this machine, walked without a cap:

| Where | Entries | Files | Bytes |
|---|---|---|---|
| the main checkout's `_fixtures/` | 996 | 444,638 | **3.74 GB** |
| the main checkout's `local/gate-tree/_fixtures` (CI's tree) | 465 | 1,661 | 565 MB |
| `local/scope-tree/_fixtures` (the merge queue's tree) | 247 | 1,687 | 237 MB |
| `local/codex-build/_fixtures` | 138 | 254 | 240 MB |
| three agent worktrees under `.claude/worktrees/` | 56 + 16 + 2 | 175 + 242 + 9 | 241 MB, 242 MB, 1.3 MB |
| each live session tree (`s-adad536c`, `s-d99282c9`) | 34, 4 | 169, 170 | 238 MB each |
| about two dozen recovered worktrees' `src/Daoris.Cli/_fixtures` and root `_fixtures` under `local/recovered-worktrees/` | | | 3 KB each |
| `src/Daoris.Web/_fixtures`, the devkit's, `.claude/_fixtures` | | | 69 KB, 185 B, 0 |

In the main checkout's folder: **`deployment-rehearsal` 1.38 GB (1,675 files, 2026-10-17), `startlog-published-proof` 1.27 GB (2026-10-26),
`dsh` 276 MB (2026-10-23), `contracts-build` 241 MB (2026-11-05), `rehearsal-builds` 110 MB, `pinned-mingit` 96 MB** make 3.38 GB of
the 3.74; the other 990 entries are 0.36 GB. The entries' modification days run 2026-09-19 to 2026-11-11: **313 of the 996 entries date
from 2026-11-09 alone and 150, 146 and 144 from 2026-10-24, 10-25 and 10-19**, so entries are being left at about the rate a test
run creates them. The names are test scratch (`account-files-keep-lock`, `act-journal`, `agent-card`, `provider-conformance`,
`test-homes` and the like), several of the tree's own scratch folders are empty directories (0 bytes: `provider-conformance`,
`test-homes`), and the two largest are rehearsals' outputs, not unit-test scratch. Every tree that builds the contracts leaves a 238 to
242 MB `contracts-build` of its own (five on this machine). Nothing was removed here (measure only; the cleanup rules are
`docs/development.md#repository-scratch-cleanup`).

## 7. What the census did not reach

- A whole-suite wall time **taken alone**: none was run (the machine's rule). Every figure above is a loaded CI figure, a median of
  such, or rule 17's.
- `tools/*.test.mjs` for removed doors, the devkit's tests, the service's `DoesNotContain` tool-name assertions, the CLI's other tests
  (only `one-word`, `driver-names` and the usage fixtures were read).
- Whether each `*CommandTests` already holds what the seven text fixtures' rows assert beyond words (exit codes, files written).
- A comparison of fixture JSON between projects, and the CLI's and `tools/`'s helper duplication.
- The web's per-test times (rule 17 has them for 2026-11-09); the Process halves per class for the *quiet* machine.

## 8. The cut, in the order of what it saves

No assertion of a live rule is weakened by any step; each step names what its proof becomes. The gates' wall time is taken alone
before step 1 and after each step (step 0).

| # | Cut | Saves | Basis |
|---|---|---|---|
| 0 | **Take the before-figures**: each light gate and each Process half alone on a quiet machine, the slowest 25 classes by `.trx`, the web's per-test JSON; write them into rule 17 | the measure the rest is judged by | §1, §7 |
| 1 | **Seed every Process-half repository** (group 3): `GitSeed.PlainAsync` for the 168 files that still `git init`, and a class-level look at the git half's top 25 classes (51% of 38,874 s of test time) for a repository built once per class instead of once per test; remove the private `Git`/`GitAsync` runners | the largest wall time the suite has: the Process halves were 173.5 and 99.5 minutes in the recorded run, the longest gates of a run that took eight hours | §1, §3 row 3 |
| 2 | **Delete the seven recordings of the retired TypeScript verbs** (§2b): keep, per verb, the rows that hold a behaviour (an exit, a file written) as a small table in the verb's own `*CommandTests` and the one-line refusals, drop the 797 rows' exact sentences | 1.4 MB of fixtures and about 60 KB of readers, the biggest single file group (3.6% of all test bytes) and 797 of the driver's theory rows | §2b |
| 3 | **Stop leaving scratch** (§6): clear the main checkout's `_fixtures/` entries older than the day a run needs (a keep rule, not a `Directory.Delete`), make the runs that leave `contracts-build` and the rehearsals' outputs remove them, and find why 313 entries came from one day | 3.74 GB and 444,638 files in the main checkout and about 1.7 GB in the CI, queue and agent trees: disk, and every walk of the folder (a measure of it took minutes) | §6 |
| 4 | **Scratch home and delete into one `ScratchHome`** (group 1) and **the service stand-ins into a routed handler and a loopback shell** (group 2), moving the 119 OS-temp files to `TestHomes.Root` | about 5,000 lines and 300 KB, and the repository's own no-OS-temp rule | §3 |
| 5 | **Replace the help-room and target-prompt goldens** with one full render each plus an assertion per varying section (§4), after the owner's word on the byte-for-byte reading | about 0.35 MB of 0.47 MB | §4 |
| 6 | **The web's mocks, fetch routers and builders** (groups 4 and 5) | about 1,600 lines, 85 KB; fewer places for the next `@shenora/react` change to touch (143 files) | §3 |
| 7 | **The tool tests' end-to-end cases**: each of the merge tool's 600 to 860 s end-to-end cases is one scratch repository per case; share the seeded repository (step 1's builder, for `tool-test/support`) and split `merge-branch.test.ts` (409 KB) and `merge-queue.test.ts` (239 KB) as the ratchet already asks | the median 13.5 minutes and the 40 KB ratchet's 648 KB there | §1, §5 |
| 8 | **Delete the removed-door proofs** (§2a, high confidence): the three whole files, the six `ONEWAY1c_i_*` cases, the four one-case files, the redundant `_propose` assertions; then the medium rows one by one | about 45 cases and 30 KB: small, but each one is a proof of a thing that no longer exists and a door the next reader must learn is closed | §2a |
| 9 | **Shared builders for records, waits and clocks** (groups 6, 7, 8, 9): one `Poll.cs`, one `MovedClock`, one `Fixtures`, the infra copies linked | about 1,200 lines and 76 KB | §3 |
| 10 | **Later, on their conditions:** the migration steps' tests (23 files, 107 KB) at the next stored-shape change; `MoveStoreCensusTests` once a row ends the older build's move boxes; the 110 over-budget test files shrink as steps 1, 2, 4 and 6 land (the ratchet lists each shrink) | 107 KB and the list's 3.18 MB | §2d, §5 |

Rows 2, 5 and 8 each change a fixture or a test's proof and carry their own regression check (the `*CommandTests` row counts, the
section assertions); rows 1, 4, 6, 7 and 9 change no assertion, only how its setting is built, and are measured by step 0's
figures. Row 3's decision to clear scratch belongs to the owner (a deletion of data under a gitignored folder).

## 9. Closing

Reproduce the counts with `git ls-files` and the attribute and call patterns named in §0; the recorded times are in
`local/scratch/ci-70da37d/status.json` and its logs and `.trx` files. A re-take of this census after the cut compares those
figures.
