# The alone path: a CLI management verb runs the driver's own program (proposed, 2026-10-21)

**Standing:** proposed under D194 (PUSH1f2), for the reads D191 asks. Codex reads it, as it reads every design. The author judges
that it changes no foundation in D191's amendment's sense: no record's data model, no wire's shape and no lock's rule changes; the
pipe, the lease and the sent-press record stay the driver's as built, and the design removes the CLI's second implementation of
each. So Fable is not asked; the steward decides. Nothing here is built; its rows (§5) build it. Nothing in it waits for the owner
(D191 point 3): it publishes nothing, spends no account and loses no data.

Read for it: D182 points 6 and its PUSH1a note's *Left* (lines 35-40, 98-104), its PUSH1e note's *Left* (497-502); D171's PUSH1f
note (392-428); D50 (1-24); D156 points 1-4 and its D50 relation (lines 10-33, 91); the terminal RPC design §4.2, §5, §7 and
§9-§10; the PUSH1f and RPC1f archive entries; D193 part 10; D191 and its note; `.claude/knowledge/twins.md`'s table (rows 78-142).
Every statement about today was read from the code at `49aa46e47` and names its file and line; none was run.

## 0. The case, and what the design rests on

**The case.** PUSH1f made `daoris driver`, `agent`, `plugin` and `tool` the running Daoris's clients: a change the window has a route
for is sent over the pipe, confirmed in the window and written by the instance's own handler. With no instance, each verb still
writes with its own TypeScript editor, held to the driver's by the twins' tables. D182 point 6 asks for the same .NET implementation
under the home's lease instead, never a re-implementation in TypeScript. Those editors are larger than their row says: the four
families' sources are about 18,950 lines (the driver's choices and workflows 8,582, the agents and accounts 6,606, the plugins 1,898,
the tools 1,867), with 887 more they share (`jsonkeep.ts`, `keeplock.ts`, `lease.ts`, `door.ts`) and about 16,200 lines of their tests.

| Today | Read at |
|---|---|
| A family's change goes to its door: the running Daoris's route where it answers, else the verb's own TypeScript write under the home's lease, taken only around the write | `src/Daoris.Cli/src/service/service.ts:473-545` |
| Each verb that changes the home names its route, or why it acts alone; `VerbPress.alone` is the verb's TypeScript edit, `words` its text for both paths | `src/Daoris.Cli/src/platform/door.ts:67-185` |
| A file `DAORIS_DRIVER_CONFIG` or `DAORIS_HARNESS_CONFIG` names is no instance's: written alone, holding nothing | `cli/driver.ts:169-175`, `cli/agent.ts:111-113`, `door.ts:187-191` |
| The driver's standing choices' routes are thin: read the payload, refuse a missing scope, call `DriverConfig.With*` or a rule's `EditOf`/`Apply` through `DriverContext.Change`, answer the state | `Modules/Bridge/Routes/DriverRoutes.cs:44-62, 155-220`, `LinesRoutes.cs:37-74, 866-947`, `Modules/Bridge/DriverContext.cs:110-118` |
| A few judge more: a landing rule keeps its key and squash and asks the plugin catalogue; a review looks for its procedure in the registry's checkouts; *Try again* judges from the planner's verdict | `LinesRoutes.cs:188-259, 789-821`, `DriverRoutes.cs:300-338` |
| The answer is the whole state: the files' choices beside the loop's own facts | `DriverRoutes.cs:395-474`; `Modules/Loop/DriverStateSlice.cs:31-49` (`shell`, `choices`, `running`) |
| A route's `DriverException` reaches the page as `driver-refused`; a coded refusal is the modules' `Refusals`, on the window kit's exception | `Modules/Bridge/DriverModule.cs:119-130`, `Modules/Platform/Refusals.cs:1-23`, `ToolsRoutes.cs:139-213` |
| The other families' bodies are mostly the driver library's already: `Tools`, `ToolInstall`, `HarnessKeys`, `HarnessSettings`, `PluginInstall` | `ToolsRoutes.cs:124-253`, `AgentsRoutes.cs:362-430`, `Driver/Plugins/PluginInstall.cs:26` |
| The host already edits `driver.json` from a terminal through a library command whose rules the route calls too (`access`, `heavy`, `help mode`) | `Driver.Host/AccessConsole.cs:8-30`, `Driver/Sessions/AccessCommand.cs:31-125`, `docs/index/verbs.md:51-53` |
| The host takes the home's lease at its top for a verb that holds it; beside an instance it holds nothing | `Driver.Host/Program.cs:292-316`, `Driver/Platform/VerbLeases.cs:38-71`, `HomeLease.cs:213-251` |
| The host's verbs are the running Daoris's clients through `InstanceDoor.PressAsync`; a refusal over the pipe is `{verb}: {message} ({code})`, exit 1 only for the person's refusal or the two minutes' end | `Driver/Platform/InstanceDoor.cs:78-117, 171-226` |
| The host references the driver library alone, runs with invariant globalization; the modules sit on the window kit's package | `Driver.Host.csproj:9-15`, `Modules.csproj:21-30` |
| The CLI starts a process only in `startChild`, held by the dogfood test; no doctrine command reaches it | `agents/toolchain.ts:3001-3016`, `test/dogfood.test.ts:610-640` |
| An install carries the host framework-dependent in `app/daoris-driver/`, its launchers in `app/bin/` | `tools/desktop-publish.mjs:355-373` |
| A `daoris` finds its install by the folder it runs from; a checkout has none | `platform/home.ts:47-61` |
| Daoris hands a session it starts its record's id, `DAORIS_SESSION_ID`, and a heavy run reads it | `Driver/Agents/Adapters/Adapters.cs:1343-1381, 1409`, `Service/KnowledgeConnector.cs:114`, `Sessions/Heavy/HeavyRun.cs:220` |
| The family rehearsal runs `daoris driver`, `agent` and `plugin` | `tools/family-rehearsal.mjs:1551, 1563, 3764-3907, 5492, 5910-6013` |

**What it rests on.** The judgement is already .NET's: every rule an editor applies has its driver twin, and the route bodies are
thin wrappers over it. What the TypeScript holds that .NET does not is the verbs' grammar and their words. So the question is not
how to call .NET for a write, but which program is the verb.

## 1. The shape: the CLI hands the family to the driver's program

`daoris driver|agent|plugin|tool …` runs `daoris-driver driver|agent|plugin|tool …` with the same words after the family's, and its
output and exit are the run's. The driver's program is the verb: its grammar, its checks, the switch (the running Daoris's client,
or alone under the lease), the edit and the words. The families' TypeScript retires, row by row (§4).

The narrower shape, the CLI keeping its grammar, reads and words and asking .NET only for each write, is rejected in §6: it keeps the
CLI reading the files and deriving verdicts for its lists in TypeScript, which D182 rejects by name, leaves two pipe clients and two
leases, and adds a new local wire between the CLI and the host that an npm `daoris` and an install's host could disagree on.

### 1.1 The seam (point 1)

- **Where the program is**, in this order: the file `DAORIS_DRIVER_PROGRAM` names (new: the dev loop's and a rehearsal's own build,
  as `DAORIS_INSTANCE` names their pipe; classed in `WiringVariables.SetAside` with its reason); else, where this package runs from an
  install (`installOf`, `home.ts:51-61`), `<install>/app/daoris-driver/daoris-driver.exe` (`daoris-driver` off Windows), the
  publish's `DRIVER_HOME` and `DRIVER_EXE` (`desktop-publish.mjs:363-364`), a twin row `desktop-publish.test.ts` holds as it holds
  `INSTALL_BIN`; else **refused, exit 2**: *`daoris driver` is run by Daoris's own driver program, which this `daoris` cannot find: it
  is not the one a Daoris install carries, and `DAORIS_DRIVER_PROGRAM` names none. Run the install's `daoris` (its `app/bin/`), or
  `daoris-driver driver …` where it is installed; nothing was done.* Never `PATH`, never a checkout's build output found by itself,
  never the install that holds the home: each is a guess at which build acts, and from a checkout the last would make a gate's
  `daoris` a live install's client (FINDDAORIS1's hazard). No TypeScript fallback (§6).
- **How it starts.** Through the one spawn site, `startChild`, so D50's rule and the dogfood test hold as they are. The `.exe`
  itself, never its `.cmd` launcher, which would hand the person's words to `cmd.exe` (`&`, `%`, `^`, quotes). `shell: false`; the
  terminal's own standard input, output and error (`stdio: 'inherit'`), so `agent key` reads the person's typing and `agent login` the
  agent's prompts with no copy held by the CLI; the terminal's environment, so every variable the host reads (`DAORIS_HOME`,
  `DAORIS_DRIVER_CONFIG`, `DAORIS_HARNESS_CONFIG`, `DAORIS_INSTANCE`, `DAORIS_SERVICE_URL`) reaches it as typed. Until `tools.ts`
  leaves (2f), `startChild` also hands the tools' `PATH` (TOOLS5); harmless, since the host hands its own children the tools itself
  (`EveryChildIsHandedTheToolsTests`). The run's exit is the host's status; an end by a signal is 2. The CLI keeps Ctrl+C from
  ending itself while the host runs, so the host's own handling decides (RPC1h's) and its exit is the run's.
- **The words arrive whole.** Node quotes each argument for the Windows command line by the convention .NET's start reads back; the
  seam's test holds the hard cases: quotes, `&`, `%`, a backslash before a quote, Chinese, a path with spaces.
- **What stays offline.** No doctrine command reaches the spawn (the dogfood test's transitive rule, unchanged). The CLI's other
  management verbs (`connect`, `retire`, `wire`, `import`, `remote`, `brainstorm`, `browser`) are not these families and are unchanged.
- **A machine with the CLI and no install** keeps every doctrine verb; these four families say the refusal above. A machine with no
  driver has no reader of the files these verbs edit.
- **Its cost** is a process start per verb (the runtime's start for a framework-dependent program), measured in 2a and said there.
- **The host's culture is invariant** (`Driver.Host.csproj:10`), and it now words what the CLI worded: 2a holds that the driver's
  name comparisons (`NameCaseTests`) and its moments' words answer alike in that mode.

### 1.2 The host's families

- **Their words** are `daoris-driver driver|agent|plugin|tool …`: the CLI's words after its family word, so a person on a machine with
  no screen types the same verbs (D50). `plugin` (the CLI's add, update, remove, enable, disable, list) sits beside the host's own
  `plugins` (install, new, try, show, activity, approve, withdraw); no sub-verb is shared, and the usage says which is which.
- **Each family is a command in the driver library**, as `AccessCommand` is: it reads the words, says its usage (exit 2), and for each
  change hands `InstanceDoor.PressAsync` the route, the payload, its alone act and its words; the switch, `RESULT`, the sent-press
  record and the *waiting for you to confirm* line are the host's as built (`InstanceDoor.cs:171-226`). A verb with no route acts
  alone. `Program.cs` routes each family through a thin console, as it routes `access` and `heavy`.
- **The lease** (`VerbLeases`): a change holds it from the top, a read none (`list`, `show`, `workflow show|list|export`, any
  `--plan`). A verb that runs a child or waits on the person (`agent install|update|login|pin|key`, `tool download|update`,
  `tool use … managed`) holds none from the top and takes it around the writes that end it, as the CLI's `underLease` does today, so
  the desktop's start never waits on a sign-in.
- **A named file is no instance's**: with `DAORIS_DRIVER_CONFIG` (the driver family) or `DAORIS_HARNESS_CONFIG` (the agent family)
  set, the command acts alone on that file, sends nothing and holds nothing, as the CLI does today.

### 1.3 Where the bodies live (point 2)

**Chosen: in the driver library.** Each route a verb sends has its body as a door in the driver library (the payload read, the
refusals, the edit, the write and the press's own answer), called by the route, which adds the nudge, the publish and the state, and
by the command's alone act. For the driver's choices this is mostly a move: the edits are `DriverConfig.With*` and each rule's
`EditOf`/`Apply` already; what moves is the payload's reading (`ReviewEditOf`, `LandingKept`, `HeavyGatesOf`, `Number`), the scope's
refusals, the landing's plugin check, the review's look in the registry and *Try again*'s verdict switch. The tools' bodies call
`Tools` and `ToolInstall`; the agents' call `HarnessKeys`, `HarnessSettings`, `AccountNames`, `RotationUse`, `PermissionRules`,
`RuleProposals` and `AgentSettings`; the plugins' call `PluginInstall`, `PluginState` and `PluginSource`.

- **What a door reads beside the files** is a world: the home, the driver's file, the service where the body asks it (the review's
  registry, *Try again*'s records and quests), this machine's agents with its plugins (built as `DriverLoop.cs:245` builds them), the
  machine log, and the loop's last look where one is held (*Try again* reads it before a fresh plan, `DriverRoutes.cs:305-308`). The
  route hands the loop's; the host builds one from the home, with no loop.
- **The answer** is the press's own fields (`retried`, `reviewed`, a rename's `name`, a deleted version's `folder`, a look's `looks`,
  a location's `added`) and, for the driver's standing choices, which answer the state, `ChoicesAnswer`: `STATE`'s fields read from
  the files alone (the `choices` partition and the pull-only `configPath`, `cap`, `adapter`, `pollSeconds`, `released`), which
  `DriverRoutes.State` composes with the loop's own (`ready`, `home`, its notices, what runs). This is the PUSH1a note's *Left*: the
  files' half of the state's owner moves into the driver library because a headless consumer needs it; the loop's half stays.
- **Refusals.** A body throws `DriverException`, the route's `driver-refused`, or where the route answers a code today a
  `CodedRefusal(code, message, parameters)`, a `DriverException` of the driver library, which the module maps to
  `Refusals.Because(code, …)` so the page reads what it read. The codes these bodies throw move into the driver library
  (`RefusalCodes`); `Refusals` names each as before, so its catalogue test is unchanged.
- **Rejected: the host referencing the modules** and running the route's handler. It would carry the window kit's package and the
  whole bridge into the headless host; build a `DriverLoop` per verb to call a handler that reads `_loop.Service` (null unless started,
  so the review and *Try again* refuse *still coming up*), answer a state no loop judged, and run `[Publishes]` to a bus no window
  hears. D182's PUSH1a note already chose the direction.

## 2. The person's class alone (point 3)

**The lease is not a confirmation.** It keeps one writer per home and never said who writes. With no Daoris running there is no
window to confirm in, and D156's answer for the service's person-only doors with no window, the key in the terminal's environment,
does not reach a file edit: no host runs to present it to. So a person's press acting alone acts on the terminal's word, as `daoris`
does today and as D50 asks of a machine with no screen: the terminal is the person's own door.

**A shell Daoris started is told apart by its marker.** Daoris hands a session it starts its record's id, `DAORIS_SESSION_ID`
(driven sessions, intakes, reviewers, helpers, brainstorm passes and conversations, and their connectors). A person's press that would
act in the verb's own process, with no instance's confirmation covering it (none runs, a headless loop holds the home, or the verb has
no route), is **refused, exit 1, from an environment carrying the marker**: *this change is the person's, and this terminal is a
session's (`DAORIS_SESSION_ID`): make it in Daoris's window, through Ask Daoris's proposal and its Apply, or from your own terminal;
nothing was done.* A person's press is a route the census classes `person`, or a verb with no route: each changes a standing choice,
an account, an install, a sign-in, a rule or a trust. An open press and a read are not refused.

It is a marker, not a proof: the session's agent is the person's account and can clear the variable or write the file (D156 point 4).
It keeps an agent's honest attempt from passing as the person's, as `HeavyRun` reads the same marker for its slot. Over the pipe
nothing changes: the window's card names the terminal and says Daoris cannot tell who ran it. The window's terminal panel removes the
marker from the shells it starts, since they are the person's; 2a's test lists every start that hands it.

## 3. The words (point 4)

- **One function per verb, both paths.** The command's words are called by the pipe path over the route's answer and by the alone
  path over the answer its door returned, the same shape (§1.3), so the text cannot differ by path. `TreesWords` is the shape
  (`Driver/Trees/TreesWords.cs:46`).
- **A refusal is one line on both paths**: `{verb}: {sentence} ({code})` on the error stream, as `PressAsync` says one today; the alone
  act says its door's refusal through the same function. Exit: the domain's refusals (`driver-refused` and each code of
  `RefusalCodes`) 1 on both paths, the person's refusal and the two minutes' end in the window 1 (RPC1c), everything else 2 (a service
  that did not answer, a file that does not read, the kit's own errors). The four families hand `PressAsync` this rule; the host's
  other verbs keep theirs.
- **Held by** a modules test per family running each verb both ways over one home, the route through the module and the command's
  alone act, holding the bytes written, the text, the refusal's line and the exit equal: red where a route and its door disagree.
- **The port keeps today's text.** Each family's cases, as the CLI's tests hold them, are run once through the TypeScript verb before
  it retires and written as a table under the driver's tests: the words, the home's files before and after, standard output and
  error, the exit, a machine path written `<home>`. The command's test reads it row for row. A sentence the port changes on purpose is
  a row changed in the same commit, with its reason.

## 4. The twins it retires (point 5)

**The rule.** A module leaves the CLI in the row that frees its last reader, found by a search of `src/Daoris.Cli/src`, `tools/`,
`src/Daoris.Web` and the CLI's and tools' tests, recorded in the row's hand-back; its twins row loses its CLI column then. A table
another twin still reads (the driver's, the page's) stays, moved to the driver's fixtures with its readers and its merge rows
(`tools/merge-branch.mjs:915-920, 978-982`).

| Family (rows) | Leaves the CLI | `twins.md` rows losing the CLI's column | Tables |
|---|---|---|---|
| The driver's choices and workflows (2a, 2b) | `driver/*.ts`, `workflows/*.ts`; `service.ts`' `sessionRecords` and `registryCheckouts` | 83 (for `driver.json`), 86-88, 108-127, 129; TWINWORDS1's `landing-says.json` words become one C# function | `driver-config-keeps.json`, `graph-homes.json`, `graph-problems.json`, `workflow-graphs.json`, `workflow-graph-versions.json`, `landing-says.json` stay for the driver's tests and move |
| The tools (2c) | `tools/toolinstall.ts`, `tools/resources.ts`, `platform/zipfile.ts` | 93, 94 (but `tarball.ts`), 96 | none of the CLI's |
| The agents' rules, trust and settings (2d) | `agents/permissions.ts`, `ruleproposals.ts`, `trust.ts`, `accounts/agentsettings.ts` | 131-135 (133 keeps `tools/usage-report.mjs`' reader, a tool's own) | none |
| The agents' tools and accounts (2e) | `agents/toolchain.ts` but the seam and `TOOLCHAINS`, `channels.ts`, `openpgp.ts`, `accounts/*.ts`, `platform/shellword.ts`, `service.ts`' `releaseFetcher` | 90, 91, 97-106, 130; D182's PUSH1e *Left* (the usage-curve, account-listing and home-table twins) closes | `rotation-use.json`, `usage-curves.json`, `shell-words.json`, `account-reads.json` stay for the driver and the page, and move |
| The plugins, and the last census (2f) | `plugins/*.ts`, `tools/tools.ts`, `TOOLCHAINS`, `platform/jsonkeep.ts`, `keeplock.ts`, `lease.ts`, `door.ts`, `service.ts`' pipe client | 136-142, 92, 95, 84 and 85 (`KeepLockProcessTests` loses its Node half), 80, 81; 79's pipe half and 82 where the census finds no reader | `reserved-harnesses.json` (the driver's), `home-files-keep.json` stays and moves |

**Readers outside the families, found today.** `tools/proof-kit.mjs:18-20` and `tools/acp-trust-probe.mjs:41` import
`toolchain.ts`'s resolution of an agent's program and home: before 2e they ask the host (a read verb printing the resolved program
and home), never a third reader. `tools/desktop-publish.mjs:75` imports `tarball.ts`, which stays as the publish's tool and leaves the
twin row when `channels.ts` goes. `tools/usage-report.mjs` reads proposals with its own reader. The page reads none of these modules,
only the shared tables. `check` reaches none (`doctrine/drift.ts` imports layout and platform modules alone). The family rehearsal's
`daoris` calls name `DAORIS_DRIVER_PROGRAM` from the build it already makes.

**What stays in the CLI.** The doctrine commands; the service's verbs; `casefold.ts` (`connect.ts`, `remotemap.ts`); `installOf` and
`THIS_INSTALL`, which the seam finds the program by; `runningService` and `endpoint`, which the service's verbs read; and the seam.
`startChild` is the CLI's one spawn site to the end. After 2e `toolchain.ts` holds only the seam and `TOOLCHAINS`, which the plugins'
reserved names read; when 2f takes `TOOLCHAINS`, the seam moves to `platform/driverprogram.ts` and the dogfood test's one spawning
module is renamed with it.

## 5. The rows (point 6)

Each row is a family working end to end: the CLI forwards its verbs, the host has them, the route bodies are the library's, the
TypeScript and its tests leave, and the twins rows and tables move as §4 says. Until 2b, the CLI forwards the driver sub-verbs 2a
built and keeps the rest; that list is held to the host's usage by a test that reads it, and goes in 2b.

- **PUSH1f2a — the seam, proved by the driver's standing choices** (cli, driver, modules; first). §1.1, §1.2, §2, §3; `ChoicesAnswer`,
  `CodedRefusal`, `RefusalCodes`; the choice doors moved from `DriverRoutes`, `LinesRoutes` and `AcrossRoutes`; `daoris-driver driver`
  with `list` and every `DRIVER_VERBS` row but `workflow`, `work-cycle`, `landing --cycle|--no-cycle` and `brainstorms`;
  `DAORIS_DRIVER_PROGRAM`. Proof: the both-ways modules
  test over every standing choice, `retry` and `review` included (bytes, text, refusal, exit; red first); the person's press refused
  from a marked environment, a named file never sent and holding nothing; the CLI's seam test with a stand-in program (the hard words
  arriving whole, exits 0, 1, 2 and a signal's passed, the missing program's sentence, doctrine reaching no spawn); the table of
  today's text; the start's cost measured; the family rehearsal's `daoris driver` phases; on the window, a `daoris driver drive` with
  the window open, confirmed there and shown at once (PUSH1f's unproven line).
- **PUSH1f2b — the driver's workflows and cycles** (cli, driver; after 2a). `workflow new|edit|apply|import|export|use|kind|show|list`,
  `work-cycle`, `landing --cycle|--no-cycle`, `brainstorms`; the driver family's TypeScript and its rows leave
  (§4). Proof: the table, both ways where a route exists, the rows and tables moved, the merge rows changed, the CLI's tests counted
  before and after.
- **PUSH1f2c — the tools** (cli, driver, modules; after 2a). Every `TOOL_VERBS` row, `tool list`; the download's progress said by the
  host; `tool use … managed` alone holding the lease only for its end. Proof: the table, both ways for `TOOLS_USE`, `TOOLS_DELETE`,
  `TOOLS_LOCATION` and `TOOLS_LOOK`, a download through a stand-in list in the driver's tests.
- **PUSH1f2d — the agents' rules, trust and settings** (cli, driver, modules; after 2a). `agent rules …`, `agent trust`, `agent
  settings`. Proof: the table, both ways for `RULE_ACTION`, `RULE_PROPOSAL` and `SET_AGENT_SETTINGS`, the marked refusal of
  `rules accept`.
- **PUSH1f2e — the agents' tools and accounts** (cli, driver, modules; after 2d). `agent list|install|update|login|key|pin|unpin`,
  `agent profile …`; `proof-kit.mjs` and `acp-trust-probe.mjs` asking the host. Proof: the table, both ways for `HARNESS_ACTION` and
  `ACCOUNT_USE`, a Process test signing a stub agent in and reading a key from standard input through the CLI, the probe lock held
  against the host's own reads.
- **PUSH1f2f — the plugins, and the last census** (cli, driver, modules; after 2b, 2c and 2e). Every `PLUGIN_VERBS` row and
  `plugin list`, the terminal's replace kept as the host's (`PluginInstall` adds and never replaces at the window); then every module
  §4 leaves last. Proof: the table, both ways for the four plugin routes, the census recorded empty, `door.test.ts` and the lease's and
  door's CLI tests gone with the code, the dogfood rule on the renamed seam.

Each row runs its own tests and the quick checks; the merge and the CI beside run the rest (D180).

## 6. Rejected

- **A TypeScript fallback where the program is missing**: the second implementation this retires, kept for a machine with no driver
  to read what it writes.
- **The CLI keeping its grammar, reads and words, asking a host verb for each write** (a JSON press on the host): the lists keep
  reading the files and deriving verdicts in TypeScript, which D182 rejects; two pipe clients and two leases stay; and a new wire
  between an npm `daoris` and an install's host could disagree across versions, where words passed to a program cannot.
- **The host referencing the modules** (§1.3).
- **The local service as the alone path**: it holds no machine-local act (D47) and the terminal RPC design §10 rejected it as a channel.
- **Finding the program on `PATH`, in a checkout's build output, or by the install that holds the home** (§1.1).
- **Starting the program through its `.cmd` launcher** (§1.1).
- **Holding the home's lease for a sign-in's or a download's whole run** (§1.2).
- **A person's press alone only with the person key**: with nothing running no key exists, and D50 asks that a machine with no screen
  be settable from a terminal.
- **Folding `daoris plugin` into the host's `plugins`**: the person's words would change spelling between the two programs, for no
  rule gained.

## 7. What this leaves

- **A verb with no route acts in its own process even while the window runs**, as today; the marker refuses an agent's shell there
  too, and a route for each (the window's surface, D50) is the window's row when asked.
- **`browser` and `remote`** edit their files in TypeScript (twins rows 89 and 144); D182 point 6 names the four families, and the
  same shape applies when asked.
- **A read while Daoris runs** reads the files alone, as today.
- **Ctrl+C sends no cancel** until RPC1h's client, as today.

## 8. Reads (D191)

| Reader | Finding | Rests on | Verdict |
|---|---|---|---|
| Codex | pending | | |
