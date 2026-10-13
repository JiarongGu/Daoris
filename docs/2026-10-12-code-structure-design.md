# Code structure: onion layers inside domain modules

The design under D166. Written 2026-10-12 from a structure pack (a branch-scout's map of the tree at 87f192a2 with
TREEFOLLOW1's merge open), the parallel-development design (`docs/2026-09-30-parallel-development-design.md` §4, §5,
§7) and the component READMEs.

## 0. The owner's direction

> *"I also find daoris currently does not have a proper dev folder structure, since this is growing we do need to properly
> separate into a good code design in modules (onion structure + modularized domain)"*

Two asks: **modules by domain** (§2), and **onion layers** inside them (§3), with a folder structure that says both (§4).

**Amended 2026-10-14** (D166's note of that day). The owner, after six modules were layered in three:

> *"you don't have to 100% follow onion structure since it will be over engineering"*; *"what we want is a clean 'domain'
> separation (project/module) and we more focus on modularize the code to reduce duplication and easier to manage. 'Onion
> structure' is a good way to do model/logic structuring but we don't need a full onion practice"*

What stands: the modules (§2), each a folder (§4), and the gate (§5) for its rules 1, 2 and 4. What changes: inside a module,
only a `Domain/` for its pure records, rules and words, held pure by rule 2; the rest of the module sits at its top. §3's
`Application` and `Infrastructure` layers, §4's layer folders, §5's rules 3 and 5, STRUCT3a's extension-member and port
pattern, and namespaces per module are not used: no port, forward or extension member is written for a layer's sake, and a
port is made where a test must stand something in. The next work is less duplication across the modules
(`docs/2026-10-14-duplication-survey.md`).

## 1. What is true

- **The driver is one flat folder of one namespace.** `Daoris.Desktop.Driver` holds 299 C# files (about 101,700 lines),
  about 260 of them at its top; only `Help/` has folders. All 299 are `Daoris.Driver`. Its tests are 403 files, 367 flat.
  Modules (55) and App (11) share `Daoris.Desktop`; the service's core is 104 flat files of `Daoris.Knowledge`; the CLI's
  `src/` is 84 flat files; the page has 112 files at `src/`'s top beside its domain folders.
- **No rule says what may call what inside a project.** Any driver file calls any other. Large files mix decisions and
  I/O: `Harnesses.cs` 3,775 lines, `Driver.cs` 2,783, `Adapters.cs` 2,023, `Acp.cs` 1,854, `ServiceClient.cs` 1,708,
  `ChatRunner.cs` 1,622, `SessionTrees.cs` 1,493.
- **The shape is there, unnamed.** Pure rules: `Planner`, `AccountRotation`, `AccountReadings`, the landing rules in
  `Landing.cs`, `OpinionRules`, `ReviewRules`, the workflow gate, `NoteCodes`, `ShellWord`, `Help/Room`. Ports already
  shaped as interfaces: `ISessionAdapter`, `IReviewWorld` and its children, `IWorkTrees`, `ISetupWorld`,
  `ITerminalFactory`, `IHookChannel`, plus about 35 `Func` stand-ins tests use.
- **Paths are read.** The lane map is per project, so a move inside a project keeps its lane. But `merge-branch.mjs`'s
  reach table names 11 driver files the page's tests parse, 36 .NET test files walk to a project folder by name (some
  enumerate a folder without its subfolders), and about 25 CLI and page tests read driver files by path. Generated
  `docs/index/` and `docs/code-map.json` follow the tree. The component READMEs declare the sources they describe.
- **More assemblies were rejected** (parallel-development design §7): collisions are within files, and a boundary adds
  build and twin cost. The MOD splits were by feature, in place, behaviour-preserving and proven by the gates.

## 2. Modules by domain

Each module owns one domain of Daoris's vocabulary (the glossary's names), its records, its rules, and the I/O that serves
them. For the driver:

| Module | Its domain | Today's files, by prefix |
|---|---|---|
| `Platform` | the shared kernel: home, atomic files, the machine log, coded notes, shell words, the clock | `AtomicFile`, `DaorisHome`, `CanonicalLine`, `MachineLog*`, `NoteCodes`, `ShellWord` |
| `Accounts` | agents' accounts: sign-ins, readings, limits, cooling, the walk, switching, usage | `Account*`, `Harnesses*`, `Codex*`, `Usage`, `ResumeAccount`, `InstructionAccount*`, `AgentSettings`, `ClaudeTrust` |
| `Agents` | the doors to agents and their processes: adapters, the protocol door, streams, terminals, toolchain | `Adapters`, `Acp*`, `ClaudeStreams`, `StructuredOutput`, `Terminals`, `PseudoConsole`, `ProcessJob`, `ConsoleRelay`, `SpawnServers`, `Tools*`, `ToolInstall*`, `ToolResources` |
| `Conversations` | a conversation's turns, words, files, going on, hand-overs and helpers | `ChatRunner*`, `ChatTurns`, `ChatFiles`, `GoOn*`, `HandOver*`, `Helper*`, `Continuation`, `TranscriptFile` |
| `Sessions` | session records and their life: events, requests, pause, abandon, archive, delete, history, trace | `Session*` (not trees), `RunningSessions`, `PausedWork`, `Work*` (not `WorkingTree`), `SessionEvents`, `History*`, `Trace*` |
| `Trees` | session trees, branches, landing, tidying, pull requests, and git | `SessionTrees*`, `SessionBranches`, `SessionGroups`, `Landing*`, `Landed*`, `AutoLand*`, `BranchTidying`, `PullRequest*`, `TreeGuard`, `TreeLock`, `Orphans`, `Git*`, `WorkingTree` |
| `Quests` | planning the loop's work: the planner, intake, parking, attention | `Planner`, `Intake*`, `Quest*`, `ParkedQuests`, `Attention`, `Observation` |
| `Review` | opinions and reviews | `Opinion*`, `Review*`, `LastOpinions` |
| `Workflows` | workflows and their runs | `Workflow*`, `WorkflowRun*` |
| `Plugins` | plugins, their hooks, kit and trials | `Plugin*`, `Plugins`, `Hooks`, `plugin-kit/` |
| `Help` | Ask Daoris | `Help*`, `Help/` |
| `Service` | the knowledge service as the driver meets it: its client, the host locator, remote sync, the registry | `ServiceClient*`, `ServiceHostLocator`, `KnowledgeConnector`, `RemoteSync*`, `RemoteTarget`, `Regist*` |
| `Setup` | setting up a machine and its workspaces, installing and updating | `Setup*`, `WorkspaceSetup*`, `StagedBuild`, `InstallUpdate`, `UpdateCommand`, `ReleaseChannel` |
| `Loop` | the driver itself: its watch, lock, config and commands, which composes the modules | `Driver*`, `DriverWatch`, `DriverHttp`, `DriverLock`, `DriverConfig*`, `*Command` |

A file's module is decided by STRUCT1's map, row by row; the table is its start. Where a file serves two domains it is
split, not shared. `Loop` is the composition root: it may use every module; no module uses it.

## 3. Onion layers inside each module

| Layer | Holds | May use |
|---|---|---|
| `Domain` | records and rules: what a reading means, which account goes first, what a landing form is | `Platform`'s domain; another module's `Domain` |
| `Application` | the module's use cases: a start's walk, a press, a hand-over, written against ports | its own `Domain`; another module's `Domain` and `Application`; ports |
| `Infrastructure` | the I/O behind the ports: git, files under the home, processes, HTTP, an agent's CLI | everything inward in its own module; `Platform` |

- **Domain is pure**: no file, directory, process, network, environment or clock read. Time and the home's contents come in
  as values; a rule that needs the world takes a port.
- **A port is an interface the inner layers own** and an outer layer implements (`ISessionAdapter`, `IReviewWorld`,
  `IWorkTrees` today). A `Func` stand-in that tests use is a port in waiting.
- **Across modules**, a module reaches another only through its `Domain` and `Application`, never its `Infrastructure`.
- **A module's `Application` is its composition point**: it names its own `Infrastructure` as a port's default, so no other
  module and no host needs to; how a mixed type is cut and kept callable is D166's STRUCT3a note.
- **The hosts are the outer ring**: `Driver.Host` (the terminal door), `Modules` (the page's bridge), `App` (the
  window), and the service's HTTP and MCP hosts. They compose; nothing inward knows them, as today.

## 4. The folders, and what stays

```
src/Daoris.Desktop/Daoris.Desktop.Driver/
  Platform/      Domain/ Infrastructure/
  Accounts/      Domain/ Application/ Infrastructure/
  …one folder per module…
  Loop/
src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/
  Accounts/ …    (the tests mirror the module; fixtures/ and golden/ stay where twins read them)
```

- **One driver assembly stays.** The rule is held by a structure gate (§5), not by project references: .NET's base library
  gives every assembly files and processes, so an assembly per layer would not make `Domain` pure, and §7's costs (build
  time, twins, gates per project) stand. Assemblies are measured again once the modules are layered (STRUCT6).
- **Namespaces stay `Daoris.Driver` while files move**, so a move touches no `using` and git follows each rename. A module
  takes `Daoris.Driver.<Module>` once it is layered (STRUCT4), with its embedded resources' names pinned first
  (`Daoris.Driver.tree-guard.mjs` and the plugin kit's templates are read by name).
- **Records keep their citations.** A decision, the archive and the fix log cite files as they were: history, not links.
  Living documents (component READMEs, knowledge, open designs) are updated by the change that moves what they describe.

## 5. The structure gate

A fast driver test, `StructureTests`, over the driver's sources:

1. **Every file is in a module folder**, except those on its allowlist, which starts as every file not yet moved and may
   only shrink: a new file lands in its module from STRUCT1 on.
2. **A `Domain` file does no I/O**: no `File.`, `Directory.`, `Process`, `HttpClient`, `Environment.`, `DateTimeOffset.Now`
   or `UtcNow`, `WorkingTree.` and the like, by a token table with its reasons.
3. **No file names a type declared in another module's `Infrastructure`**, and no `Domain` file names one declared in an
   `Application` or `Infrastructure` folder: a declaration index built from the sources, checked by name.
4. **`Loop` is used by no module.**
5. **A layered module keeps every file in a layer** (the map's `layered` list, from STRUCT3a), and a static member a C# 14
   `extension(T)` block declares is read as `T.Member`, in the layer of the file that declares it.

Each rule names the file, the line and the rule it breaks. The same gate, in vitest, holds the page's and the CLI's folders
once their rows land (STRUCT5).

## 6. How it moves without breaking

- **Path readers first** (STRUCT1): every test or tool that reads a driver file by path finds it by name under the driver's
  tree, and every folder enumeration includes subfolders, so a later move edits no reader. `merge-branch.mjs`'s named-file
  rules match a name wherever it lives under the driver, held by its own test as today.
- **One module per branch, files moved as they are**: no edit but what a move needs, so the gates prove the move. A move
  runs only while no branch in flight touches that module's files; the parent schedules it between batches.
- **Order** by least coupling and least work in flight: `Plugins`, `Help`, `Workflows`, `Review`, `Service`, `Setup`,
  `Quests`, then `Accounts` after the SWITCH rows, `Conversations`, `Sessions`, `Agents`, `Trees` after PROCESS1b, and
  `Platform` and `Loop` last.
- **Then layers, one module per branch** (STRUCT3): the module's mixed files split into `Domain`, `Application` and
  `Infrastructure`, its I/O behind ports, the gate's purity and reach rules switched on for that module as it lands. The
  large files split here.
- Each branch regenerates `docs/index/` and `docs/code-map.json`, and its README's declared sources follow.

## 7. Rows

| Row | What | Lanes |
|---|---|---|
| STRUCT1 | The gate (§5) with the whole tree allowlisted; the map, a file to its module; path readers found by name and folder walks recursive; `merge-branch.mjs`'s named rules by name | driver, tools, cli, web (tests only) |
| STRUCT2a–n | Move one driver module into its folder, tests mirrored, the allowlist shrunk; one row per module in §6's order | driver |
| STRUCT3a–n | Layer one module: `Domain`, `Application`, `Infrastructure`, ports; its gate rules on | driver |
| STRUCT4 | A layered module's namespace `Daoris.Driver.<Module>`, resource names pinned | driver |
| STRUCT5a–d | The same for `Modules` (the bridge per domain, browser, window, update), the service's core, the page's top folder and the CLI's `src/`, each with its gate | modules, service, web, cli |
| STRUCT6 | Assemblies, measured after STRUCT3: build time, merge collisions, what a split would buy; a decision then | — |

## 8. Rejected

- **A big-bang move**: every branch in flight would conflict, and one failing gate would hold three hundred files.
- **An assembly per module or layer now**: the costs of §7 stand and the purity it would claim is not enforced by it.
- **Renaming namespaces with the moves**: three hundred `using` edits per move, and git loses the renames.
- **Leaving the shape to convention**: the README already names roles and nothing holds them; the gate does.
