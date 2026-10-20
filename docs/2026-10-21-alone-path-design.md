# The alone path: a CLI management verb runs the driver's own program (proposed, 2026-10-21)

**Standing:** proposed under D194 (PUSH1f2), for the reads D191 asks. Revised the same day by Codex's read of the whole, then by
Fable's read of §1.1 and §1.4, the two sections that change a foundation (D191's note): every finding was checked against the code and
is taken or answered in §8, where the reads are kept. Nothing here is built; its rows (§5) build it. Nothing in it waits for the owner
(D191 point 3): it publishes nothing, spends no account and loses no data.

Read for it: D182 points 6 and its PUSH1a note's *Left* (lines 35-40, 98-104), its PUSH1e note's *Left* (497-502); D171's PUSH1f
note (392-428); D50 (1-24); D156 points 1-4 and 7, its rejected *positive identity for agents* and its D50 relation (lines 10-48, 70,
91); the terminal RPC design §4.2, §5, §7 and §9-§10; the PUSH1f and RPC1f archive entries; D193 part 10; D191 and its note;
`.claude/knowledge/twins.md`'s table (rows 78-142); D173's KEEPALL1 and CONFIG1 notes (lines 2304-2337, 4369-4391). Every statement
about today was read from the code at `49aa46e47` and names its file and line; the rows Codex's read touched were read again at
`ff01a5881`, and those Fable's touched at `65c47fcdc`, each citing its commit's lines. None was run.

## 0. The case, and what the design rests on

**The case.** PUSH1f made `daoris driver`, `agent`, `plugin` and `tool` the running Daoris's clients: a change the window has a route
for is sent over the pipe, confirmed in the window and written by the instance's own handler. With no instance, each verb still
writes with its own TypeScript editor, held to the driver's by the twins' tables. D182 point 6 asks for the same .NET implementation
under the home's lease instead, never a re-implementation in TypeScript. Those editors are larger than their row says: the four
families' sources are about 18,950 lines (the driver's choices and workflows 8,582, the agents and accounts 6,606, the plugins 1,898,
the tools 1,867), with 887 more they share (`jsonkeep.ts`, `keeplock.ts`, `lease.ts`, `door.ts`) and about 16,200 lines of their tests.

| Today | Read at |
|---|---|
| A family's change goes to its door: the running Daoris's route where it answers, else the verb's own TypeScript act under the home's lease, held for the whole act (`underLease`): a tool's download, update and managed use hold it from the first byte fetched to the last written | `src/Daoris.Cli/src/service/service.ts:473-545`, `tools/toolinstall.ts:518-531` |
| `agent install`, `update`, `login`, `pin` and `key` take no door at all: they hold nothing and send nothing | `agents/toolchain.ts:1663-1762, 1804-1826` |
| Each verb that changes the home names its route, or why it acts alone; `VerbPress.alone` is the verb's TypeScript edit, `words` its text for both paths | `src/Daoris.Cli/src/platform/door.ts:62-185` |
| A file `DAORIS_DRIVER_CONFIG` or `DAORIS_HARNESS_CONFIG` names is no instance's: written alone, holding nothing. Each names a home of its own (`homeOf`), whose workflows, accounts, keys and managed versions the family reads; but the agent family's rules and their proposals resolve from `DAORIS_HOME` whatever the variable names, and the whole family is still handed `ALONE` | `cli/driver.ts:169-175`, `cli/agent.ts:108-113`, `toolchain.ts:146-159, 1645`, `permissions.ts:140-143`, `ruleproposals.ts:99, 112, 130` |
| The driver's standing choices' routes are thin: read the payload into a typed edit (`EditOf`), let the rule's `Apply` refuse it, write through `DriverContext.Change` (`DriverConfig.Load`, the change, `Save`, then the loop's nudge), answer the state | `Modules/Bridge/Routes/LinesRoutes.cs:866-947`, `Modules/Bridge/DriverContext.cs:110-118`, `DriverRoutes.cs:44-66, 206-276` |
| A few judge more: a landing rule keeps its key and squash and asks the plugin catalogue; a review looks for its procedure in the registry's checkouts; *Try again* judges from the planner's verdict | `LinesRoutes.cs:188-259, 789-821`, `DriverRoutes.cs:339-398` |
| The answer is the whole state: the files' choices beside the loop's own facts, and a press's own `retried`, `reviewed` or `shownAgain` | `DriverRoutes.cs:430-446` (`State`); `Modules/Loop/DriverStateSlice.cs:31-49` (`shell`, `choices`, `running`) |
| Every `DriverException` reaches the page as `driver-refused`, an unreadable `driver.json` among them; a coded refusal is the modules' `Refusals`, on the window kit's exception. A tool's refusal carries its check (`ToolRefusal`), which the route maps to a code | `Modules/Bridge/DriverModule.cs:119-130`, `Driver/Platform/DriverConfigUnreadableException.cs:13-14`, `Modules/Platform/Refusals.cs:1-23`, `ToolsRoutes.cs:189-213` |
| Both paths exit 2 for every refusal but the person's refusal and the two minutes' end; `DriverException` is a tool error by its own word; the CLI's own checks exit 1 or 2 case by case | `Driver/Platform/InstanceDoor.cs:222-225`, `Driver/Platform/DriverException.cs:3`, `platform/errors.ts:13`, `test/fixtures/cli-usage/refusals.json:16-40` |
| The other families' bodies are mostly the driver library's already: `Tools`, `ToolInstall`, `HarnessKeys`, `HarnessSettings`, `PluginInstall` | `ToolsRoutes.cs:124-213`, `AgentsRoutes.cs:362-430`, `Driver/Plugins/PluginInstall.cs:26` |
| A plugin's remove and update stop its process before its folder moves: on Windows a running hook holds its folder | `Modules/Bridge/Routes/PluginsRoutes.cs:308-336, 545-560` |
| The host already edits `driver.json` from a terminal through a library command whose rules the route calls too (`access`, `heavy`, `help mode`) | `Driver.Host/AccessConsole.cs:8-30`, `Driver/Sessions/AccessCommand.cs:31-125`, `docs/index/verbs.md:51-53` |
| The host takes the home's lease at its top for a verb that holds it, and an unclassed verb holds it: `VerbLeases.Holds` reads a verb's first two words, and a later one only through a command's own reading (`access`, `heavy`). Beside an instance a verb's lease holds nothing; the desktop's loop, finding a verb holding it, waits with no deadline, saying so once the hold has lasted thirty seconds | `Driver.Host/Program.cs:293-317`, `Driver/Platform/VerbLeases.cs:38-71`, `HomeLease.cs:213-251`, `Modules/Loop/DriverLoop.cs:1075-1127` |
| A download stages at the fixed `<version>.part`, deleting what is there at its start and at its end, which no reader takes for a version (`Downloaded` takes version names alone), and becomes the version by one folder move, replacing a folder its start found holding nothing that runs. A crash's `.part` stays until that version is downloaded again, `tool delete` refusing its name. A channel pin stages alike at `<where>.part`; an npm pin installs in place. The window keeps its own downloads apart within its process; the CLI keeps two terminals' apart by the lease it holds for the whole run | `Driver/Agents/ToolInstall/ToolInstall.cs:56-57, 73-85, 181-239, 318-321`, `Driver/Setup/ClaudeReleaseDownloads.cs:81-84, 130-144`, `Driver/Accounts/Harnesses/HarnessActions.cs:102-106`, `Driver/Agents/ToolInstall/ToolActions.cs:100-104`, `service.ts:534-544` |
| A sign-in makes its account's folder before the agent's flow, in the window (`PrepareLogin`) as at the terminal: `Directory.CreateDirectory`, under an id drawn at random and never reused. The window removes it in a `finally` where the sign-in did not finish | `HarnessActions.cs:412-443`, `Driver/Accounts/Harnesses/HarnessSelection.cs:579-583`, `Driver/Accounts/AccountNames.cs:297-311`, `Modules/Bridge/AgentActions.cs:386-458`, `toolchain.ts:1181-1183` |
| A harness action's start redirects its three streams and opens no window; the window relays the output line by line and sends the page's typed line through `HarnessRun.Send`. The CLI's `relay` starts the agent with the terminal's own streams for that reason, and maps a failed start to its message and any status but 0 to 2 | `HarnessActions.cs:27-40, 445-517`, `toolchain.ts:2910-2943` |
| `KeepLock.Take` is taken by `DriverConfig.Save` and `JsonKeeping.Save` alone: `driver.json`; `plugins.json`, `permissions.json`, `keys.json`. `tools.json`, `accounts.json` and `cooling.json` are read at the write and written with no lock, `cooling.json`'s gate in its own process; `harnesses.json` is written whole from a copy read earlier | `Driver/Platform/DriverConfig.cs:866`, `Driver/Platform/JsonKeeping.cs:68-71`, `HarnessSelection.cs:177`, `Driver/Agents/Tools.cs:362-394, 425-457`, `AccountNames.cs:360-364`, `Driver/Accounts/AccountCooling.cs:161-182`, `Driver/Accounts/Harnesses/HarnessSettings.cs:153-163`; D173's KEEPALL1 note (KEEPALL1b) |
| The host's verbs are the running Daoris's clients through `InstanceDoor.PressAsync`; a refusal over the pipe is `{verb}: {message} ({code})`; a lost answer is asked by `RESULT`, never sent again; a pipe that speaks another protocol is refused (`protocol`) | `Driver/Platform/InstanceDoor.cs:78-154, 171-226` |
| The host registers its Ctrl+C handler for the loop alone, after every other verb is routed, cancelling `closing`, made at `:360`; `chat` and `run-heavy` each have their own. Its one catch takes a cancellation, a `DriverException`, a lease's exception and a request's, never an `IOException` | `Driver.Host/Program.cs:358-360, 642-662, 804-833`, `Driver.Host/ChatConsole.cs:189-273`, `Driver.Host/HeavyConsole.cs:31-50` |
| The host references the driver library alone, runs with invariant globalization, and is built as `daoris-driver`, an apphost beside its DLL; the modules sit on the window kit's package | `Driver.Host.csproj:5-15` (`:9`), `Modules.csproj:21-30` |
| The CLI starts a process only in `startChild`, which hands every child the managed tools' `PATH`; a `.cmd` or `.bat` goes through `cmd.exe`, quoted by the CLI; no doctrine command reaches it. The host's `agent install` finds the machine's npm on the `PATH` it inherited | `agents/toolchain.ts:2947-3016`, `test/dogfood.test.ts:610-705`, `HarnessActions.cs:73-86, 160-174` |
| An install carries the host framework-dependent in `app/daoris-driver/`, its launchers in `app/bin/`, the CLI package in `app/cli/`: one `app/`, replaced whole | `tools/desktop-publish.mjs:304, 316, 355-373` |
| A `daoris` finds its install by the folder it runs from; a checkout has none | `platform/home.ts:47-61` |
| Daoris hands a session it starts its record's id, `DAORIS_SESSION_ID`, where the session has one, and a heavy run reads it | `Driver/Agents/Adapters/Adapters.cs:1341-1409`, `Service/KnowledgeConnector.cs:114`, `Sessions/Heavy/HeavyRun.cs:220` |
| The family rehearsal runs `daoris driver`, `agent` and `plugin` from the checkout's CLI, its own driver a DLL run through `dotnet`, every child inheriting the caller's environment; it accepts and declines a rule proposal as the person | `tools/family-rehearsal.mjs:37-46, 63-67, 90-99, 1551, 1563, 3764-3897, 5492, 5910-6013` |
| Two tools import the agent's resolution from `toolchain.ts`; `browser.ts`, `remotemap.ts` and `service.ts` read the home's table | `tools/proof-kit.mjs:16-20, 93-95`, `tools/acp-trust-probe.mjs:41-50`; `browser/browser.ts:188`, `service/remotemap.ts:58-63` |
| The dispatcher's goldens run `runCli` in this process and read its writer | `test/cli/cli-usage.test.ts:24-58` |

**What it rests on.** The judgement is already .NET's: every rule an editor applies has its driver twin, and the route bodies are
thin wrappers over it. What the TypeScript holds that .NET does not is the verbs' grammar and their words. So the question is which
program is the verb, and then how little of each route has to move for that program to call it: the operation, never the route.

## 1. The shape: the CLI hands the family to the driver's program

`daoris driver|agent|plugin|tool …` runs `daoris-driver driver|agent|plugin|tool …` with the same words after the family's, and its
output and exit are the run's. The driver's program is the verb: its grammar, its checks, the switch (the running Daoris's client,
or alone under the lease), the act and the words. The families' TypeScript retires, row by row (§4).

Two other shapes were weighed. The narrower one, the CLI keeping its grammar, reads and words and asking .NET only for each write, is
rejected in §6: it keeps the CLI reading the files and deriving verdicts for its lists in TypeScript, which D182 rejects by name, and
leaves two pipe clients and two leases. Codex's smaller one keeps this shape and narrows what moves behind it: each change one typed
operation in the driver library, every route and its answer staying as they are. It is taken (§1.3).

### 1.1 The seam (point 1)

- **Where the program is**, in this order: the file `DAORIS_DRIVER_PROGRAM` names (new: the dev loop's and a rehearsal's own build,
  as `DAORIS_INSTANCE` names their pipe; classed in `WiringVariables.SetAside` with its reason); else, where this package runs from an
  install (`installOf`, `home.ts:51-61`), `<install>/app/daoris-driver/daoris-driver.exe` (`daoris-driver` off Windows), the
  publish's `DRIVER_HOME` and `DRIVER_EXE` (`desktop-publish.mjs:363-364`), a twin row `desktop-publish.test.ts` holds as it holds
  `INSTALL_BIN`; else **refused, exit 2**: *`daoris driver` is run by Daoris's own driver program, which this `daoris` cannot find: it
  is not the one a Daoris install carries, and `DAORIS_DRIVER_PROGRAM` names none. Run the install's `daoris` (its `app/bin/`), or
  `daoris-driver driver …` where it is installed; nothing was done.* Never `PATH`, never a checkout's build output found by itself,
  never the install that holds the home: each is a guess at which build acts, and from a checkout the last would make a gate's
  `daoris` a live install's client (FINDDAORIS1's hazard). `DAORIS_DRIVER_PROGRAM` names the apphost a build writes beside its DLL,
  `daoris-driver.exe` (`daoris-driver` off Windows), the host's `AssemblyName` (`Driver.Host.csproj:9`), never the DLL the family
  rehearsal runs through `dotnet` today (`family-rehearsal.mjs:91`). A value that is not an existing file of that name is refused,
  exit 2, naming it: a `.dll`, which no start runs; a quoted value, which names no file; a folder; and a `.cmd` or a `.bat`, which Node
  starts only through `cmd.exe` (`toolchain.ts:2947-2989`), where the person's words would be read again. No TypeScript fallback (§6).
- **Which build answers.** The program and this `daoris` are one build: an install's `app/` carries both and is replaced whole by a
  republish and by an update's swap (`desktop-publish.mjs:304, 363`); `DAORIS_DRIVER_PROGRAM` names a build of the same checkout, as
  the dev loop and the rehearsal set it (2a). No other pairing is supported, and none is checked. The CLI reads none of the host's
  output, so no answer can be misread across builds; a host that lacks the family, a sub-verb or a flag says its usage and exits 2,
  having done nothing (`Program.cs:642-650` for a family it does not know, each family command's usage for the rest).
- **How it starts.** Through the one spawn site, `startChild`, so D50's rule and the dogfood test hold as they are. The `.exe`
  itself, never its `.cmd` launcher. `shell: false`; the terminal's own standard input, output and error (`stdio: 'inherit'`), so
  `agent key` reads the person's typing and the agent's own prompts reach the person with no copy held by the CLI, the host handing
  its console on in turn (*The second hop*); and **the terminal's
  environment exactly**, every variable the host reads (`DAORIS_HOME`, `DAORIS_DRIVER_CONFIG`, `DAORIS_HARNESS_CONFIG`,
  `DAORIS_INSTANCE`, `DAORIS_SERVICE_URL`, `DAORIS_SESSION_ID`) as typed and nothing added. Not the tools' `PATH`, which `startChild`
  hands every other child (TOOLS5): the host's `agent install` is the machine's npm's job and finds it on the `PATH` it inherited
  (`HarnessActions.cs:73-86, 160-174`), so a managed node's npm first on it would install the agent into the managed node's folder.
  The host hands its own children the tools itself (`EveryChildIsHandedTheToolsTests`). So `startChild` takes an option only the seam
  passes, starting the driver's program with the environment it was given, and the dogfood test's TOOLS5 rule names that one
  exception.
- **The second hop.** At a terminal the host starts an agent's own command (its installer, its updater, its sign-in, a pin's npm) as
  the CLI's `relay` starts it today (`toolchain.ts:2910-2933`): with the console's three streams, un-redirected, so the person answers
  the agent's prompts and the agent finds a terminal, and Ctrl+C reaches the agent itself. The window's start redirects all three with
  no window (`HarnessActions.cs:459-477`), relays the output line by line (`:513-516`) and sends the page's typed line through
  `HarnessRun.Send` (`:27-40`); at a terminal nothing would feed `Send`, and a sign-in waiting for a pasted code would wait for ever.
  So the start takes a terminal's form beside the window's: the same facts (`HarnessProbe.Apply`, `Tools.Hand`, the account's
  variable, the pin's environment) through the same start gate, the streams left as the host's and no window flag set. A pump from
  `Console.In` into `Send`, as `chat` reads the person's lines (`ChatConsole.cs:202-212`), was weighed: it carries lines only, and an
  agent whose sign-in wants a terminal would find a pipe.
- **Nothing before the start.** The CLI writes nothing of its own before the start: Node writes to a Windows console asynchronously,
  so a line written before the synchronous start can land after the host's. The allow-list's forwarding (§5) says no line of its own.
  The seam's test holds it: the dispatcher's stand-in finds nothing written when it is called (§3).
- **Its exit**, as `relay` maps an agent's today (`toolchain.ts:2935-2943`), with the host's statuses kept. The host's 0, 1 or 2 is the
  run's. A start that fails (Node's `error`, with no status and no signal: `EACCES`, `EINVAL`, `UNKNOWN`) is 2, said in one line naming
  the program and the error's message. The status a Ctrl+C ends a process with, `0xC000013A` on Windows and `SIGINT` elsewhere, is 2
  with nothing said: the person pressed it. Any other status or signal is 2, said in one line naming the program and its status: a
  framework-dependent program (`desktop-publish.mjs:367-373`) that found no runtime, or one that crashed, ends with the runtime's own
  codes.
- **Ctrl+C** reaches every process on the console. The CLI keeps it from ending itself while the host runs: a `SIGINT` listener and a
  `SIGBREAK` one, each doing nothing, set before the synchronous start, so their events wait until the start returns and the run's
  status is the host's. `SIGBREAK` is Windows' Ctrl+Break, which would otherwise end the CLI and leave the host writing to a shell that
  has moved on. The host takes it in one handler, registered where the four families are routed, after `closing` is made
  (`Program.cs:360`): it keeps the process and cancels `closing`, whose token each family command is handed. One handler, where
  `run-heavy` and `chat` each wrote their own (`HeavyConsole.cs:31-50`, `ChatConsole.cs:189-273`); registered there, not at `:360`
  itself, so a verb routed in between keeps the runtime's own Ctrl+C rather than one it never reads. The token: a wait on the pipe
  stops (the press may still be confirmed in the window; its sent record is kept, so `daoris-driver result <id>` asks for it); a
  download stops and its staging goes; a key's read stops, nothing written; the wait for the lease to publish stops (§1.4); an agent's
  own command, sharing the console, has the key itself and ends by its own rule while the host waits for it, a sign-in that did not
  finish removed after (§1.4); and a write under way completes, being atomic. The command says what was done and what was not, exit 2.
- **The words arrive whole.** Node quotes each argument for the Windows command line by the convention .NET reads back. Held by a
  native fixture: a small .NET program the driver's tests build, printing its arguments as it read them, started through the seam by
  a driver test, since a Node stand-in would read them with Node's own parser. The hard cases: quotes, `&`, `%`, a backslash before a
  quote, Chinese, a path with spaces.
- **What stays offline.** No doctrine command reaches the spawn (the dogfood test's transitive rule, unchanged). The CLI's other
  management verbs (`connect`, `retire`, `wire`, `import`, `remote`, `brainstorm`, `browser`) are not these families and are unchanged.
- **What a person loses.** A machine with the CLI and no install keeps every doctrine verb, and these four families say the refusal
  above where today they edit the files: files that only a driver reads, so what is lost is an edit with no reader. A checkout's
  `daoris` loses them too unless `DAORIS_DRIVER_PROGRAM` names a build: the dev loop's scratch shell and the family rehearsal set it
  (2a), and a contributor's terminal sets it once. A headless machine whose `daoris-driver` is not this package's install's types
  `daoris-driver driver …`, or names it. The refusal says each way.
- **Its cost** is a process start per verb (the runtime's start for a framework-dependent program), measured in 2a and said there.
- **The host's culture is invariant** (`Driver.Host.csproj:10`), and it now words what the CLI worded: 2a holds that the driver's
  name comparisons (`NameCaseTests`) and its moments' words answer alike in that mode.

### 1.2 The host's families

- **Their words** are `daoris-driver driver|agent|plugin|tool …`: the CLI's words after its family word, so a person on a machine with
  no screen types the same verbs (D50). `plugin` (the CLI's add, update, remove, enable, disable, list) sits beside the host's own
  `plugins` (install, new, try, show, activity, approve, withdraw); no sub-verb is shared, and the usage says which is which.
- **Each family is a command in the driver library**, as `AccessCommand` is: it reads the words into a typed input, says its usage
  (exit 2), and for each change hands `InstanceDoor.PressAsync` the route, the payload its input writes, its alone act (the operation,
  §1.3) and its words; the switch, `RESULT`, the sent-press record and the *waiting for you to confirm* line are the host's as built
  (`InstanceDoor.cs:171-226`). A verb with no route acts alone. `Program.cs` routes each family through a thin console, as it routes
  `access` and `heavy`.
- **The lease** (`VerbLeases`): a short change holds it from the top, a read none (`list`, `show`, `tool path`, a bare `tool
  locations`, `agent which`, `workflow show|list|export`, any `--plan`, the usage). A long verb, one that downloads, runs the agent's
  own flow or reads the person's input, holds none at its top and takes it itself to publish (§1.4). `Holds` reads each family through
  its command's own reading, as it reads `access` and `heavy` (`VerbLeases.cs:64-66`): otherwise it reads no word past the second
  (`:42-43`), so `tool use <id> managed` could not be told from `tool use <id> system`, and it holds an unclassed verb (`:69`), so a
  family with no row would hold the home through a whole download or sign-in. Each family's row adds its words to `VerbLeasesTests`'
  two theories (`VerbLeasesTests.cs:49, 101`): every short change holds, and every read, long verb, `--plan` and usage holds none.
- **A named file isolates what it names.** With `DAORIS_DRIVER_CONFIG` set, the driver family acts on that file's home alone, sends
  nothing and holds nothing, as today: every file its changes write is that home's. With `DAORIS_HARNESS_CONFIG` set, the agent
  family's wiring, accounts, keys and managed versions are that file's home's and act alone there as today; but its rules and their
  proposals resolve from `DAORIS_HOME` (`permissions.ts:140-143`, `ruleproposals.ts:130`), so `agent rules …` keeps the door on that
  home whatever the variable names. Naming any file no longer makes `rules accept` alone beside a running Daoris. The family's verb
  table says which home each verb writes, held by a test that reads it (2d).

### 1.3 Where the bodies live (point 2)

**Chosen: one typed operation per change, in the driver library; the routes stay.** Each change is an operation that takes a typed
input and the typed facts it judges by, refuses or writes, and returns a typed result: the press's own fields (`retried`, `reviewed`,
a rename's `name`, a deleted version's `folder`, a look's `looks`, a location's `added`), never a state.

- **Most exist.** The standing choices are `DriverConfig.With*` and each rule's `Apply` over its typed edit (`LinesRoutes.cs:866-947`);
  their write is `DriverConfig.Load` then `Save`, which already re-reads and keeps under the file's lock (CONFIG1, CONFIGLOCK1), so
  `DriverContext.Change` adds only the nudge (`DriverContext.cs:110-118`). The tools' are `Tools.UseSystem`, `UseFile`, `UseManaged`
  and `ToolInstall.Delete`; the agents' `HarnessKeys`, `HarnessSettings`, `AccountNames`, `RotationUse`, `PermissionRules`,
  `RuleProposals` and `AgentSettings`; the plugins' `PluginInstall`, `PluginState` and `PluginSource`.
- **What becomes one** is a judgement a route makes inside its handler today, taken out over typed facts: the landing's plugin check
  (handed the plugin catalogue), the review's look for its procedure (handed the registry's checkouts), *Try again*'s verdict switch
  (handed the quest's records and the loop's last look where one is held, `DriverRoutes.cs:339-398`), a tool file's version check, and
  a plugin's remove after its stop.
- **The route keeps** its payload's reading (`EditOf` over the bridge's JSON), the live process's coordination (a plugin's stop before
  its update or its remove, `PluginsRoutes.cs:312-315, 556`; the loop's nudge), its `[Publishes]`, and its answer: `STATE` and every
  other, unchanged. No route's answer changes and `STATE` is not split.
- **The command keeps** its grammar into the same typed input, its reads through the library's own readers (`DriverConfig.Load`,
  `Tools.Read`, `PluginCatalog.Load`), and its words. To send, it writes the route's payload from its input; a test per route reads
  that payload back through the route's `EditOf` to the same input, so the two cannot drift. Alone, it calls the operation with the
  facts it reads: the home, the service where the operation asks it, this machine's agents with its plugins (built as `DriverLoop.cs:245`
  builds them). A plugin's remove or update alone stops nothing, since no instance runs its process; a headless loop's process holding
  the folder makes the move fail, refused as the route refuses it (`plugin-busy`), nothing taken.
- **Refusals.** An operation throws `DriverException`, which the module answers `driver-refused` (`DriverModule.cs:119-130`). Where the
  route answers a code from the operation's own judgement, the operation throws the library's coded form, as `ToolRefusal` carries its
  check today (`ToolsRoutes.cs:201-211`), and one library table maps each check to the code `Refusals` names, read by the route and by
  the command, so the line is the same both ways. A code a route makes from its own coordination (a busy plugin's) stays the module's.
- **The state's owner stays in the modules.** The command needs no composed state: it reads the files through the library's readers.
  So the PUSH1a note's *Left* (the owner moving into the library when a headless consumer needs it) is answered with nothing moved.
- **Rejected: the host referencing the modules** and running the route's handler. It would carry the window kit's package and the
  whole bridge into the headless host; build a `DriverLoop` per verb to call a handler that reads `_loop.Service` (null unless started,
  so the review and *Try again* refuse *still coming up*), answer a state no loop judged, and run `[Publishes]` to a bus no window
  hears. **And the first reading's move of each route's whole body** into the library, its payload's reading and a `ChoicesAnswer`
  split out of `STATE` with it: every bridge parser moved and the state's builder split, for no reader that needs either (§6).

### 1.4 The long verbs: staged, and published under the lease

A long verb waits on something no lease should hold the home for: a download, the agent's own installer or sign-in, the person's
typing. It holds nothing while it waits (`VerbLeases` holds none at its top, §1.2); what it makes is staged where no reader takes it
and no other run meets it; and it **publishes** under `HomeLease.ForVerb`, taken for the publication alone.

| Verb | Staged | Published under the lease |
|---|---|---|
| `tool download`, `update`; `tool use … managed` | at `<version>.part-<pid>`, its own process's (below) | a look for the version published meanwhile, then the one folder move (`ToolInstall.cs:218`) and, for a use or an update, `Tools.UseManaged`'s write; `DownloadAsync` is split at its move |
| `agent pin`, a pinned `agent update` | from a maker's channel, at `<where>.part-<pid>` (it stages at `<where>.part` today, `ClaudeReleaseDownloads.cs:81-84`); through npm, installed in place under the version's folder as today (`HarnessActions.cs:102-106`), which nothing resolves until a pin names it; two npm pins of one version at once meet in npm's prefix, as today (§7) | the channel's move after the same look; the pin's write |
| `agent install`, an unpinned `agent update` | nothing: the agent's own installer writes the machine, not the home | nothing; no lease |
| `agent login` | the account's folder, made before the flow (below) | its end: the sign-in marked, the cool-off ended, its name and its joins |
| `agent key` | the key read from standard input | the key's account written |

- **The lease is waited for, never refused.** Free, it is held. Held by another verb, a migration or a swap, the publication waits as
  the desktop's loop waits for an act (`DriverLoop.cs:1075-1127`): with no deadline, saying once the hold has lasted thirty seconds
  who holds it, and taking the lease the moment that one lets go. A refusal there would throw away what the person waited for, a
  verified archive or a finished sign-in, where a short verb refused at its top has done nothing; and a verb's hold is now a short
  act's, the long verbs holding only to publish. The person's Ctrl+C ends the wait (§1.1): a download's staging goes, and a sign-in
  that finished is kept (below). Held by an instance (the desktop started meanwhile), the lease is taken beside it holding nothing
  (`HomeLease.cs:242-246`).
- **Beside an instance, a file's keep lock is the publication's protection, and four files have none today.** `KeepLock.Take` is
  taken by `DriverConfig.Save` and `JsonKeeping.Save` alone (`DriverConfig.cs:866`, `JsonKeeping.cs:71`): `driver.json`,
  `plugins.json`, `permissions.json` and `keys.json`, the key's file (`HarnessSelection.cs:177`). `tools.json` (`Tools.SetWay`,
  `AddLocation`, `RemoveLocation`, `Tools.cs:362-394, 425-457`), `accounts.json` (`AccountNames.cs:360-364`) and `cooling.json`
  (`AccountCooling.cs:161-182`, its gate its own process's) are read at the write and written with no lock; `harnesses.json`, the
  pin's and the joins' file, is written whole from a copy read earlier (`HarnessSettings.cs:153-163`; D173's KEEPALL1 note). So a
  window's change and a terminal's landing together lose one, today, whenever the window runs. The row whose publication writes each
  file takes its lock first, before its long verbs publish: 2c `tools.json`'s three writers, and 2e `accounts.json`'s and
  `cooling.json`'s, each holding `KeepLock.Take` over the read it already makes at the write and the write (CONFIGLOCK1's guarantee,
  their merge unchanged); and 2e `harnesses.json`'s, through one change under the lock, `HarnessSettings.Change(path, edit)` (the
  lock, the file read, the edit, the whole write), every writer of the file through it, the routes' and the command's, so its
  whole-file layout (KEEPALL1b) stays as it is.
- **Each run stages under its own name.** The fixed `<version>.part` is deleted at a download's start and at its end
  (`ToolInstall.cs:196-200, 233-239`), so a second download of one version deletes the first's staging under it; on Windows the
  delete throws on the first's open archive, an `IOException` that neither of `DownloadAsync`'s catches (`:223-232`) nor the host's
  one catch (`Program.cs:804-833`) takes. Today the lease held for a terminal's whole run keeps two terminals apart
  (`service.ts:534-544`) and `ToolActions`' gate the window's own (`ToolActions.cs:100-104`), while a terminal's and the window's
  already meet; with the lease taken only to publish, two terminals would meet too. So a run stages at `<version>.part-<pid>`, its own
  process's id: two runs never touch each other's staging, and nothing waits. `Downloaded` takes version names alone
  (`ToolInstall.cs:82`), so no reader sees one. The publication looks again under the lease: a version another run published meanwhile
  is kept, this run's staging removed and the run said as *already downloaded*; a folder holding nothing that runs is replaced as today
  (`:210-215`), judged now rather than at the start; then the one move (`:218`). A run's start removes its folder's stagings whose
  process no longer runs, so a crash between the staging and the publication leaves nothing for good, where a `.part` stays today
  until its version is downloaded again (`tool delete` refuses its name, `:318-321`); a process id the system reused keeps its staging
  only until that process ends. A bare `.part` an older build left goes when its version is downloaded again, as today. A download
  stages so in 2c once `toolinstall.ts` has left, and a channel pin in 2e once `channels.ts` has, so no twin is held to the old name.
- **A sign-in.** Its account's folder is made before the agent's flow, as the window's sign-in makes it (`PrepareLogin`), and never
  moved after: an agent's own store may be keyed to its path. Its id is drawn at random and never reused (`AccountNames.NewId`,
  `AccountNames.cs:303-311`), so a sign-in the window starts at the same moment has a folder of its own; `Directory.CreateDirectory`
  makes it (`HarnessSelection.cs:582`), asking nothing of what is there. The desktop starting meanwhile reads it as a signed-out
  account until the end, as it reads its own sign-in's. The end decides as the window's does (`AgentActions.cs:402-458`): a new
  account whose sign-in did not finish (the agent failed, was stopped, or ended signed out) is removed in a `finally`, so a stop, a
  failure and an exception each leave the list as it was; a sign-in that finished is kept, and its end is then published. A Ctrl+C
  while that publication waits keeps the account, signed in, and says what was not written (its name, its joins) and how to finish
  (`daoris agent profile rename|join …`). A host killed mid-flow leaves the folder, a signed-out account until the person removes it,
  as the window killed mid-flow leaves its own today (§7).

Rejected: holding the lease for the whole run, the CLI's tools' way today (the desktop's loop waits on a verb's hold,
`DriverLoop.cs:1075-1127`, so a sign-in would hold its start for as long as the person takes); staging a sign-in's folder and moving it
after the agent signed in; refusing a publication after the lease's thirty seconds and removing what it staged (above); a lock of the
staging's own, where a name per process keeps two runs apart with nothing to wait on; the four unlocked writers moved onto
`JsonKeeping.Save`, where the three that read at the write need only the lock over it, and `harnesses.json`'s keeping is KEEPALL1b's
question, which the keeping write would answer by the way; a marker in a sign-in's folder, read as absent while its process runs and
swept once it is gone: it would close a host killed mid-sign-in at both doors, but every reader of the account list would read it, in
both twins until 2e, for a leftover the person sees as signed out and removes (§7).

## 2. The person's class alone (point 3)

**The lease is not a confirmation.** It keeps one writer per home and never said who writes. With no Daoris running there is no
window to confirm in, and D156's answer for the service's person-only doors with no window, the key in the terminal's environment,
does not reach a file edit: no host runs to present it to. So a person's press acting alone acts on the terminal's word, as `daoris`
does today and as D50 asks of a machine with no screen: the terminal is the person's own door.

**With the window running, a route's press is confirmed there; a route-less one is not.** Where a route takes the press and an
instance answers, the window confirms it (RPC1c). Where no route takes it, it acts on the terminal's word even while the window runs,
as today: the list D171's PUSH1f note decided, each with its reason in the family's verb table (`cap`, `timeout`, `adapter`,
`work-cycle`, `brainstorms` and a workflow's writes; `retry --at|--session`; `landing --cycle|--no-cycle`; a plugin `add` that
replaces; `tool use … managed`, `download`, `update`; `agent install`, `update`, `login`, `pin`, `key`, a workspace's `unpin` and
`trust`). It is retained here as written. This is the reach D156 point 7 says rather than assumes, the home's files being the
account's; a route for each, or a confirmation of its own, is the window's row when asked (§7).

**A shell Daoris started is told apart by its marker, as advice.** Daoris hands a session it starts its record's id,
`DAORIS_SESSION_ID` (driven sessions, intakes, reviewers, helpers, brainstorm passes and conversations, and their connectors, where
the session has a record). A person's press that would act in the verb's own process, with no instance's confirmation covering it
(none runs, a headless loop holds the home, or the verb has no route), is **refused, exit 1, from an environment carrying the
marker**: *this change is the person's, and this terminal is a session's (`DAORIS_SESSION_ID`): make it in Daoris's window, through
Ask Daoris's proposal and its Apply, or from your own terminal; nothing was done.* A person's press is a route the census classes
`person`, or a verb with no route: each changes a standing choice, an account, an install, a sign-in, a rule or a trust. An open press
and a read are not refused.

The marker refuses; its absence grants nothing. The session's agent is the person's account and can clear the variable or write the
file (D156 point 4), and D156 rejects reading a missing agent identity as the person: a start that forgot it would make an agent the
person. So where a window runs and a route takes the press, the window confirms it whatever the environment says. The marker keeps an
agent's honest attempt from passing as the person's, as `HeavyRun` reads the same marker for its slot. The window's terminal panel
removes it from the shells it starts, since they are the person's; 2a's test lists every start that hands it. The command reads it
through its world's environment (`InstanceWorld.Environment`), never the process's directly, so a test sets or clears it.

**A gate that acts as the person runs as a test person.** The family rehearsal inherits its caller's environment, and an agent may
run it: it removes `DAORIS_SESSION_ID` from the calls it makes as the person (the proposal it accepts, `family-rehearsal.mjs:3833-3897`,
and every person's press of its `daoris` phases), and keeps a separate check with the marker set that sees the refusal. The driver's
and the CLI's tests hand their own environment.

## 3. The words (point 4)

- **One function per verb, both paths.** The command's words read what it read before it acted and the press's own fields: on the pipe
  path from the route's answer, alone from the operation's result. So the text cannot differ by path. This is the rule D171's PUSH1f
  note set for the CLI, kept. `TreesWords` is the shape (`Driver/Trees/TreesWords.cs:46`).
- **A refusal is one line on both paths**: `{verb}: {sentence} ({code})` on the error stream, as `PressAsync` says one today; the alone
  act says the operation's refusal through the same function, with the code the library's table gives it (§1.3).
- **Exits as today, on both paths.** A refusal the command makes before it acts (its grammar, its own checks) keeps today's exit, case
  by case, held by the text table: some are 1 today (`refusals.json:22, 37-39`). A refusal of the operation or the route
  (`driver-refused` and each code, an unreadable `driver.json` among them) exits 2, as `DriverException` is a tool error by its own
  word and as both paths answer it today. The person's refusal and the two minutes' end exit 1 (RPC1c); another act's hold waited out
  1; a Daoris that lives and does not answer 2. So no failure category is carried: an unreadable file and a domain refusal exit alike,
  as they do today.
- **Held by** a modules test per family running each verb both ways over one home, the route through the module and the command's
  alone act, holding the bytes written, the text, the refusal's line and the exit equal: red where a route and its operation disagree.
  Beside it, the payload's round trip through each route's `EditOf` (§1.3).
- **The port keeps today's text.** Each family's cases, as the CLI's tests hold them, are run once through the TypeScript verb before
  it retires and written as a table under the driver's tests: the words, the home's files before and after, standard output and
  error, the exit, a machine path written `<home>`. The command's test reads it row for row. A sentence the port changes on purpose is
  a row changed in the same commit, with its reason.
- **The dispatcher's goldens.** They run `runCli` in this process and read its writer (`cli-usage.test.ts:24-58`), which inherited
  streams bypass. Each forwarded verb's rows (`refusals.json:16-40`) move to the host's table with it; `runCli` is handed its seam
  as the families are handed their door, and the dispatcher's test hands a stand-in that records what it was given, holding that each
  family forwards its words whole and that `--help` stays the dispatcher's own (`cli.ts:85-91`).

## 4. The twins it retires (point 5)

**The rule.** A module leaves the CLI in the row that frees its last reader, found by a search of `src/Daoris.Cli/src`, `tools/`,
`src/Daoris.Web` and the CLI's and tools' tests, recorded in the row's hand-back; its twins row loses its CLI column then. A table
another twin still reads (the driver's, the page's) stays, moved to the driver's fixtures with its readers and its merge rows
(`tools/merge-branch.mjs:915-920, 978-982`).

| Family (rows) | Leaves the CLI | `twins.md` rows losing the CLI's column | Tables |
|---|---|---|---|
| The driver's choices and workflows (2a, 2b) | `driver/*.ts`, `workflows/*.ts`; `service.ts`' `sessionRecords` and `registryCheckouts`; `keeplock.test.ts`' `driver.json` case retargeted to `writeKeeping` (`:124-132`) | 83 (for `driver.json`), 86-88, 108-127, 129; TWINWORDS1's `landing-says.json` words become one C# function | `driver-config-keeps.json`, `graph-homes.json`, `graph-problems.json`, `workflow-graphs.json`, `workflow-graph-versions.json`, `landing-says.json` stay for the driver's tests and move |
| The tools (2c) | `tools/toolinstall.ts`, `tools/resources.ts`, `platform/zipfile.ts`; `desktop-publish.test.ts`' reading of the list (`:29, 853-880`) left to the driver's (`ToolResources`) and the bytes it copies | 93, 94 (but `tarball.ts`), 96 | none of the CLI's |
| The agents' rules, trust and settings (2d) | `agents/permissions.ts`, `ruleproposals.ts`, `trust.ts`, `accounts/agentsettings.ts`; `toolchain.ts`' imports of them (`:109-110, 118-120`) and its `rules`, `trust` and `settings` branches (`:1785-1802`) | 131-135 (133 keeps `tools/usage-report.mjs`' reader, a tool's own) | none |
| The agents' tools and accounts (2e) | `agents/toolchain.ts` but the seam and `TOOLCHAINS`, `channels.ts`, `openpgp.ts`, `accounts/*.ts`, `platform/shellword.ts`, `service.ts`' `releaseFetcher` | 90, 91, 97-106, 130; D182's PUSH1e *Left*'s usage-curve and account-listing twins close, its home table's does not (below) | `rotation-use.json`, `usage-curves.json`, `shell-words.json`, `account-reads.json` stay for the driver and the page, and move |
| The plugins, and the last census (2f) | `plugins/*.ts`, `tools/tools.ts`, `TOOLCHAINS`, `platform/jsonkeep.ts`, `keeplock.ts`, `lease.ts`, `door.ts`, `service.ts`' pipe client | 136-142, 92, 95, 84 and 85 (`KeepLockProcessTests` loses its Node half), 80, 81; 79's pipe half and 82 where the census finds no reader | `reserved-harnesses.json` (the driver's), `home-files-keep.json` stays and moves |

**Readers outside the families, found today.** `tools/proof-kit.mjs:16-20, 93-95` and `tools/acp-trust-probe.mjs:41-50` import
`toolchain.ts`' resolution of the program an agent runs as. 2e gives the host the read they need, **`daoris-driver agent which
<agent> [--workspace <name>] --json`** (`daoris agent which` at a terminal): the program `HarnessSelection.CommandOf` resolves (D57
rule 4, `HarnessSelection.cs:407`) as one JSON object on standard output, `{ "agent", "run": [...], "managed": <path>|null, "pinned":
<version>|null, "missing": <bool> }` (`Harnesses.cs:111-114`); the home and the wiring file from the environment as the family reads
them (`DAORIS_HOME`, `DAORIS_HARNESS_CONFIG`); a read, holding nothing and sending nothing; exit 0 with the answer (a pin with nothing
installed answers `missing: true`), 2 for an agent no wiring declares or a file that does not read, its sentence on the error stream.
Without `--json` it says the same in words. Both tools run their checkout's build with it in 2e's first commit, before `toolchain.ts`
loses what they import. `tools/desktop-publish.mjs:75` imports `tarball.ts`, which stays as the publish's tool and leaves the twin row
when `channels.ts` goes. `tools/usage-report.mjs` reads proposals with its own reader. The page reads none of these modules, only the
shared tables. `check` reaches none (`doctrine/drift.ts` imports layout and platform modules alone). The family rehearsal's `daoris`
calls name `DAORIS_DRIVER_PROGRAM` from the build it already makes (2a).

**What stays in the CLI.** The doctrine commands; the service's verbs; `casefold.ts` (`connect.ts`, `remotemap.ts`); `home.ts` whole,
the home's table and its twin row 78 among it, which `browser.ts`, `remotemap.ts` and `service.ts` read (`browser.ts:188`,
`remotemap.ts:58-63`); `installOf` and `THIS_INSTALL`, which the seam finds the program by; `runningService` and `endpoint`, which the
service's verbs read; and the seam. `startChild` is the CLI's one spawn site to the end. After 2e `toolchain.ts` holds only the seam
and `TOOLCHAINS`, which the plugins' reserved names read; when 2f takes `TOOLCHAINS`, the seam moves to `platform/driverprogram.ts`
and the dogfood test's one spawning module is renamed with it.

## 5. The rows (point 6)

Each row is a family working end to end: the CLI forwards its verbs, the host has them, the operations are the library's, the
TypeScript and its tests leave, and the twins rows and tables move as §4 says. **Until a family's last row, the CLI forwards an
allow-list of its sub-verbs** and keeps the rest; the list is held to the host's usage by a test that reads both, and each row names
the list it adds and the CLI branches and imports it removes.

- **PUSH1f2a — the seam, proved by the driver's standing choices** (cli, driver, modules, tools; first). §1.1, §1.2, §2, §3; the
  choices' operations and the library's code table; `daoris-driver driver` with `list` and every `DRIVER_VERBS` row but `workflow`,
  `work-cycle`, `landing --cycle|--no-cycle` and `brainstorms`, the driver family's allow-list; `DAORIS_DRIVER_PROGRAM`, set by the dev
  loop's scratch environment to its build and by the family rehearsal to its own build output's apphost (beside the DLL it runs through
  `dotnet` today, `family-rehearsal.mjs:46`), the rehearsal's person's calls run with no marker. Proof: the both-ways modules test over
  every standing choice, `retry` and `review` included (bytes, text, refusal, exit; red first) and each payload's round trip; the
  person's press refused from a marked environment, a named file never sent and holding nothing; the seam's tests (the hard words read
  back by the native fixture; exits 0, 1 and 2 passed, a status outside them and a signal's end 2; a `.cmd` named refused; the
  terminal's environment arriving exactly, no tools' `PATH`; the missing program's sentence; Ctrl+C leaving the CLI alive until the
  program ends, its status the run's; doctrine reaching no spawn); the dispatcher's goldens on the injected seam; the table of today's
  text; the start's cost measured; the family rehearsal's `daoris driver` phases, with a marked check; on the window, a
  `daoris driver drive` with the window open, confirmed there and shown at once (PUSH1f's unproven line).
- **PUSH1f2b — the driver's workflows and cycles** (cli, driver; after 2a). `workflow new|edit|apply|import|export|use|kind|show|list`,
  `work-cycle`, `landing --cycle|--no-cycle`, `brainstorms`; the driver family's TypeScript and its rows leave (§4), the keep lock's
  `driver.json` case retargeted first. Proof: the table, both ways where a route exists, the rows and tables moved, the merge rows
  changed, the CLI's tests counted before and after.
- **PUSH1f2c — the tools** (cli, driver, modules; after 2a). Every `TOOL_VERBS` row, `tool list`, `tool path` and a bare `tool
  locations`; the download's progress said by the host; `DownloadAsync` split at its move and the long verbs published under the lease
  (§1.4). Proof: the table, both ways for `TOOLS_USE`, `TOOLS_DELETE`, `TOOLS_LOCATION` and `TOOLS_LOOK`; a download through a stand-in
  list in the driver's tests, stopped by its cancellation midway (the staging gone) and published beside an instance that started
  meanwhile; `desktop-publish.test.ts`' list checks moved off `resources.ts`.
- **PUSH1f2d — the agents' rules, trust and settings** (cli, driver, modules; after 2a). `agent rules …`, `agent trust`, `agent
  settings`, the agent family's first allow-list; `toolchain.ts`' four imports and three branches removed. Proof: the table, both ways
  for `RULE_ACTION`, `RULE_PROPOSAL` and `SET_AGENT_SETTINGS`; the marked refusal of `rules accept`; `rules accept` with
  `DAORIS_HARNESS_CONFIG` set taking the door on `DAORIS_HOME`, never alone beside an instance.
- **PUSH1f2e — the agents' tools and accounts** (cli, driver, modules, tools; after 2d). `agent which` first, `proof-kit.mjs` and
  `acp-trust-probe.mjs` asking it in the same commit; then `agent list|install|update|login|key|pin|unpin`, `agent profile …`, with
  §1.4's publication for the sign-in, the key and the pin. Proof: the table, both ways for `HARNESS_ACTION` and `ACCOUNT_USE`; a Process
  test signing a stub agent in and reading a key from standard input through the CLI, and one stopping a sign-in midway (its folder
  gone); `agent install` with a managed node finding the machine's npm; the probe lock held against the host's own reads.
- **PUSH1f2f — the plugins, and the last census** (cli, driver, modules; after 2b, 2c and 2e). Every `PLUGIN_VERBS` row and
  `plugin list`, the terminal's replace kept as the host's (`PluginInstall` adds and never replaces at the window), the plugin's stop
  kept in the route (§1.3); then every module §4 leaves last. Proof: the table, both ways for the four plugin routes, an alone remove
  of a held folder refused with nothing taken, the census recorded empty, `door.test.ts` and the lease's and door's CLI tests gone with
  the code, the dogfood rule on the renamed seam.

Each row runs its own tests and the quick checks; the merge and the CI beside run the rest (D180).

## 6. Rejected

- **A TypeScript fallback where the program is missing**: the second implementation this retires, kept for a machine with no driver
  to read what it writes.
- **The CLI keeping its grammar, reads and words, asking a host verb for each write** (a JSON press on the host): the lists keep
  reading the files and deriving verdicts in TypeScript, which D182 rejects; two pipe clients and two leases stay; and the press is a
  wire of its own to version, where the person's words are already answered by the host's usage (§1.1).
- **Each route's whole body moved into the library, its payload's reading with it, and `STATE` split into a `ChoicesAnswer`** (the
  first reading's §1.3): the typed operations give the alone path the same implementation without moving a bridge parser or the state's
  builder (§1.3).
- **The host referencing the modules** (§1.3).
- **A failure category carried on both paths so the domain's refusals exit 1** (the first reading's §3): an unreadable `driver.json` is
  a `DriverException` like any refusal (`DriverConfigUnreadableException.cs:13-14`), so it would need a new hierarchy to tell them
  apart, and every family's exits would change; both paths keep today's (§3).
- **The local service as the alone path**: it holds no machine-local act (D47) and the terminal RPC design §10 rejected it as a channel.
- **Finding the program on `PATH`, in a checkout's build output, or by the install that holds the home**; **starting it through its
  `.cmd` launcher, or a `.cmd` or `.bat` a variable names**; **handing it the tools' `PATH`** (§1.1).
- **Holding the home's lease for a sign-in's or a download's whole run**, and **staging a sign-in's folder to move it after** (§1.4).
- **A named harness file sending the whole agent family alone** (today's): its rules live under `DAORIS_HOME` (§1.2).
- **A person's press alone only with the person key**: with nothing running no key exists, and D50 asks that a machine with no screen
  be settable from a terminal.
- **Reading the marker's absence as the person** (D156's rejected positive identity for agents, §2).
- **Folding `daoris plugin` into the host's `plugins`**: the person's words would change spelling between the two programs, for no
  rule gained.

## 7. What this leaves

- **A verb with no route acts on the terminal's word even while the window runs**, D171's list retained (§2); the marker refuses an
  agent's shell there too, and a route for each (the window's surface, D50), or a confirmation of its own, is the window's row when
  asked.
- **A machine with the CLI alone, and a checkout with no `DAORIS_DRIVER_PROGRAM`**, lose the four families (§1.1).
- **The home table's twin** (row 78) stays in the CLI for `browser`, `remote` and the service's verbs (§4).
- **`browser` and `remote`** edit their files in TypeScript (twins rows 89 and 144); D182 point 6 names the four families, and the
  same shape applies when asked.
- **A read while Daoris runs** reads the files alone, as today.
- **Ctrl+C sends the instance no cancel** until RPC1h's client: it ends the terminal's wait, and the press may still be confirmed in
  the window (§1.1).

## 8. Reads (D191)

**Codex, 2026-10-21, on `2832338bc`.** Every finding was checked against the code at `ff01a5881` before it was taken (D160's step 9).
All hold; each is taken, two of them by a smaller fix than the one asked (4 and 6, said in the verdict), and three confirm what the
design keeps (7, 12 and the smaller shape's premise). Fable has not read (below).

| # | Finding | Checked at | Verdict | Taken in |
|---|---|---|---|---|
| 1 | Long verbs leasing only their final writes is not today's: a tool's download runs wholly inside `underLease`, and sign-in and key entry bypass the door; the lifecycle needs its own concurrency design | `toolinstall.ts:518-531`, `service.ts:526-545`, `toolchain.ts:1663-1738, 1804-1826` | holds: the tools' long verbs hold the lease for their whole run, the agents' hold none | §0's table; §1.2; §1.4 |
| 2 | The home table's twin cannot retire with these families: `browser` and the remotes read it; keep it and its tests | `browser.ts:188`, `remotemap.ts:58-63`, `twins.md:78` | holds | §4: `home.ts` and row 78 stay; §7 |
| 3 | Passing words does not remove version disagreement: a host can lack a verb, a flag or an output; say a supported-version rule or an explicit refusal | `desktop-publish.mjs:304, 363`, `Program.cs:642-650` | holds: the first reading's §6 claimed words could not disagree | §1.1 *Which build answers*: one build only, a missing family, sub-verb or flag the host's usage, exit 2; §6 reworded |
| 4 | `driver-refused` does not identify policy failures: an unreadable `driver.json` is a `DriverException`, contradicting *unreadable exits 2*; carry an explicit failure category on both paths | `DriverConfigUnreadableException.cs:13-14`, `DriverModule.cs:119-130`, `DriverException.cs:3`, `InstanceDoor.cs:222-225` | holds; taken by the smaller fix: the first reading's new exit rule is dropped and both paths keep today's, every refusal of the operation or the route 2, which needs no category | §3; §6 |
| 5 | A named harness file does not isolate every agent action: `agent.ts:112` hands the whole family `ALONE`, while the rules resolve from `DAORIS_HOME` | `agent.ts:108-113`, `toolchain.ts:146-159, 1645`, `permissions.ts:140-143`, `ruleproposals.ts:130` | holds: the named file names a home of its own for the wiring, accounts, keys and versions, but the rules and proposals stay `DAORIS_HOME`'s | §1.2: the rules keep the door on `DAORIS_HOME`; 2d's proof |
| 6 | Route-less actions stay unconfirmed while the window runs (design :250, D171's list): retain or amend them explicitly, or supply confirmation, before claiming the general rule | the first reading's :250, D171's PUSH1f note, `door.ts:89-97, 136-139, 162-167` | holds; taken by retaining the list explicitly, under D156 point 7's said reach, rather than a new confirmation | §2; §7 |
| 7 | Doctrine independence, one spawn site and the unknown result's handling fit: keep them | D182:35-40, `dogfood.test.ts:610-705`, `InstanceDoor.cs:198-215` | holds | kept (§1.1), the seam's environment the TOOLS5 test's one named exception (8) |
| 8 | The `PATH` hand-off changes which installer runs: `startChild` puts the managed tools first, and the host finds the machine's npm on its inherited `PATH`; hand the host the terminal's original environment from the first row | `toolchain.ts:3001-3016`, `HarnessActions.cs:73-86, 160-174` | holds: the first reading called it harmless | §1.1, from 2a; 2e's proof |
| 9 | The long verbs need a publication boundary: a download promotes into the visible version folder, a sign-in makes its visible account folder before prompting; say the staging, the publication under the lease and the desktop starting meanwhile (`ForVerb` holds nothing beside an instance) | `ToolInstall.cs:196-238`, `toolchain.ts:1181-1183`, `HarnessActions.cs:412-433`, `HomeLease.cs:213-251` | holds; a download already stages and promotes by one move, and the window's own sign-in makes its folder first too | §1.4 |
| 10 | The CLI-only and checkout refusals are compatibility losses: say them, and wire the workspace's and the rehearsal's program (the rehearsal's driver is a DLL run through `dotnet`) | `family-rehearsal.mjs:37-46, 90-99`, `desktop.mjs:113-124`, `Driver.Host.csproj:5-11` (an apphost is built beside the DLL) | holds | §1.1 *What a person loses*; §7; 2a wires the dev loop and the rehearsal |
| 11 | Normalize abnormal exits (a framework-dependent host's start or crash statuses outside 0, 1 and 2); refuse `.cmd` and `.bat` overrides; a native argument fixture; test Ctrl+C (`startChild` is synchronous; the host's cancellation is registered below its routing) | `desktop-publish.mjs:367-373`, `toolchain.ts:2947-2989, 3007-3016`, `Program.cs:642-662` | holds | §1.1; 2a's proof |
| 12 | The session marker is sound as advice, not authority | `Adapters.cs:1341-1409`, D156:70 | holds | §2: the marker refuses, its absence grants nothing |
| 13 | Agent-run rehearsals become false refusals: the family rehearsal inherits its caller's environment and accepts a rule proposal as the person; define a controlled test person, keep separate marked tests | `family-rehearsal.mjs:63-67, 3833-3897` | holds | §2; 2a, 2d |
| 14 | 2a: the dispatcher's goldens call `runCli` and read its writer, which inherited streams bypass; move them to process tests or inject the launcher; wire the rehearsal's apphost in 2a | `cli-usage.test.ts:24-58`, `refusals.json:16-40` | holds | §3: the seam injected, the forwarded rows moved to the host's table; 2a |
| 15 | 2b cannot delete `driverconfig.ts` while the keep lock's test imports it | `keeplock.test.ts:124-132` | holds | §4; 2b retargets it to `writeKeeping` first |
| 16 | 2c omits `tool path` and a bare `tool locations`; `desktop-publish.test.ts` imports `resources.ts` | `cli/tool.ts:15-31`, `desktop-publish.test.ts:29, 853-880` | holds | §1.2's reads, §4, 2c |
| 17 | 2d: `toolchain.ts` imports the four modules it deletes and dispatches the rules through them; say the forwarding allow-list and remove those branches in 2d | `toolchain.ts:108-120, 1785-1802` | holds | §5's allow-list rule; §4; 2d |
| 18 | 2e: the resolver's interface (arguments, environment, failure status, machine-readable output) is unspecified, and the two external readers need wiring first | `proof-kit.mjs:16-20, 93-95`, `acp-trust-probe.mjs:41-50`, `HarnessSelection.cs:407`, `Harnesses.cs:111-114` | holds | §4: `agent which`; 2e wires both in its first commit |
| 19 | 2f: removing or updating a plugin stops it before its folder moves; keep the stop in the route or name a callback | `PluginsRoutes.cs:308-336, 545-560` | holds | §1.3: the coordination stays in the route; 2f |
| 20 | Smaller shape, weighed first: keep whole-family forwarding but extract narrow typed operations, sharing each one's validation, write and result; keep the bridge's payload reading, the live process's coordination and the publication in the modules; the commands build typed inputs and own their words (most rule edits have that shape already): D182 without moving every bridge parser or splitting `STATE` | `LinesRoutes.cs:866-947`, `DriverContext.cs:110-118`, `ToolsRoutes.cs:124-213` | holds: the routes already parse into a typed edit and call the library's `Apply` | §1.3 rewritten; §3's words; §6 |

**The choices this round made**, where two shapes remained: the typed operation, not the route's body, as what moves (§1.3); today's
exits kept on both paths rather than a failure category (§3); the named harness file narrowed to what it names, not refused (§1.2);
D171's route-less list retained rather than a confirmation of its own (§2); a sign-in's folder made first, as the window makes it,
rather than staged and moved (§1.4); a pin's version written in place and published by the pin, as today (§1.4); and the resolver as
a read of the host's agent family (§4).

**Whether it changes a foundation.** It changes no record's data model and no wire's shape: the pipe, the sent-press record, the
lease's rules and every route's answer stay as built, and after this round `STATE` is not split and no exit changes. It does touch two
of D191's amendment's classes, narrowly. The long verbs' staging and publication under the lease, beside an instance that started
meanwhile (§1.4), is concurrency, though it adds no lock and uses `ForVerb` as built. The CLI's hand-off to the host (§1.1: the
environment it passes exactly, the statuses it normalizes, Ctrl+C held by each side) is a protocol between processes, though its words
are the person's. And it amends two decided notes: D171's PUSH1f note's named-file bullet (§1.2) and D50's as-built spawn note.
Whether Fable reads §1.1, §1.4 and §2 is the steward's to decide.
