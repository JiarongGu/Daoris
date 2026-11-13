# Ports and adapters: the application on ports, each infrastructure adapter its own package pinned by a fingerprint, its dependency tests run only when it changes (LAYERS1)

**Decided 2026-11-13 (D286).** The owner directed on 2026-11-13 that the application be separated from its infrastructure, that each
real dependency be a package of its own, and that a dependency's tests not run at a merge that changed nothing below it. Two
independent proposals answered one brief, each having read the repository and not the other: **Codex** (gpt-6.1-sol) and **Fable**
(Fable 5.2). The brief and both answers are the steward's untracked working notes, so each point below names the proposal its detail
comes from, *(Codex)* or *(Fable)*, and stands alone; a point marked *(this design)* is this document's reading of the steward's
decision where neither proposal settles it. The steward chose between them in eight decisions, which §2 to §9 follow in order.
Written by Claude Opus 5.5 on 2026-11-13 from main at `09313e2f1d`. Read with D166, D228, D285 (its verdict store),
`docs/areas/code-structure-and-process.md` (rules 3, 9, 17-21), `docs/areas/agents-and-harnesses.md`,
`docs/areas/git-and-remotes.md` and `docs/2026-10-30-agent-lyntai-design.md`. TESTTIME1's `Needs` traits and duration records, and
MERGEFLOW1's verdict key, are read as their branches build them. Nothing here is built. Counts are `git grep` over main at that
commit. Driver paths start at `src/Daoris.Desktop/Daoris.Desktop.Driver/`, module paths at `src/Daoris.Desktop/Daoris.Desktop.Modules/`,
service paths at `src/Daoris.Service/Daoris.Service.Core/`.

## 0. In short

- **Ports and adapters.** `Daoris.Ports` (the BCL only) holds the effect contracts; `Daoris.Application` the driver's domain and
  orchestration; `Daoris.Service.Application` the service's policy; `Daoris.Testing` the fakes and the shared contract cases; one
  `Daoris.Adapters.*` package per real dependency under `src/Daoris.Adapters/`. The modules and the composition roots stay inbound
  adapters and roots. `Daoris.Desktop.Driver` dissolves into the application and is never kept as a forwarding assembly.
- **Ports cut at the funnels.** About eight funnels already carry every real effect. The platform's seam is the port where one exists
  (`TimeProvider`, `HttpMessageHandler`, `DbConnection` behind a unit-of-work gate); a new interface is written only for git,
  processes, the browser, the harness and the store's unit of work. No filesystem port in the first wave.
- **Versioned by fingerprint.** A package's version is its folder's git tree hash plus the pins of what it drives. The tracked
  `daoris.adapters.lock` holds the fingerprint each package's dependency suite passed; a merge reads it in milliseconds and runs
  nothing below an unchanged package. One build per fingerprint serves every worktree. No feed for packages inside the repository.
- **Twin-proved fakes.** Each adapter's fake is in `Daoris.Testing`, and one contract suite runs on the fake and on the real adapter.
  Merges run the fake side; the real side runs when the fingerprint moves. Git goes in two rings.
- **A census by construction.** The application and the unit tests cannot reference an adapter; the banned-API analyzer refuses process
  starts, sleeps, the real clock and sockets there at compile time; a unit test over a second fails.
- **The siblings.** Agent transports and their corpora are Lyntai's, and the `Agent.*` packages are thin registrations over its runtime.
  Process ownership and the engine's bring-up graduate to Shenora under a stated rule.
- **Rows a to l, then z,** each one builder's package; b to k as up to three parallel builders once a has merged.
- **The figure:** dependency-test minutes of a merge that changed no package, zero, read from the lock.

## 1. What was measured, and what the layers are for

Measured on 2026-11-12 (TESTDIET1's and TESTTIME1's censuses):

- the .NET fast halves took 7 to 11 minutes, because tests that start processes, open real repositories or wait on clocks sit among
  the unit tests, and each gate rebuilt the test projects;
- the driver's Process suite took 48 minutes, and one merge's path-reached gate (`reached`) 10 to 21;
- the owner's budget is a unit test in seconds, any gate under 5 minutes and a whole suite under 10.

What the code is on main:

- `Daoris.Desktop.Driver` is one assembly of about 2,980 source files, with 1,970 test files. Its `fixtures/structure.json` orders 24
  subdomains from Platform and Store up to Loop, and `StructureTests` holds that order and the purity of each `Domain/` folder; the
  rest of a subdomain may perform any effect, and the permitted seams include concrete types (`WorkingTree`, `ServiceClient`,
  `HarnessRoster`, `DesktopStore`). That is modularity, not isolation *(Codex)*.
- Effects reached directly from the application: `DateTime(Offset).UtcNow` or `.Now` in about 245 files, `TimeProvider` in 43;
  `Task.Delay` or `Thread.Sleep` in 60; `Process.Start` or `ProcessStartInfo` in 80; `GitRuns`, `WorkingTree` or `TreeGit` called from
  about 224; `Microsoft.Data.Sqlite` named in 64 files across nine subdomains besides Store; `File.` or `Directory.` in about 517
  *(Fable's census, counted again)*.
- The service's Core mixes its policy with SQLite: `Quests/Operations/QuestTx` carries a `SqliteTransaction` and exposes
  `SqliteCommand` *(Codex)*.
- Lyntai and Shenora are consumed as NuGet at released pins (`DaorisLyntaiReleased` 3.5.3 and `DaorisShenoraReleased` 0.25.0 in
  `src/Daoris.Desktop/Directory.Packages.props`), with the local builds' overrides written by `tools/lyntai-local.mjs` and
  `tools/shenora-local.mjs`.

The layers make one rule true: a merge that changed nothing below an adapter runs none of that adapter's dependency tests, and the
application's own tests run on fakes in milliseconds. Isolation does not by itself shorten the 48-minute suite; it takes that suite
off every merge that does not change what it proves *(Codex)*. TESTDIET1 and TESTTIME1 cut the suites themselves.

## 2. The package map (decision 1)

### 2.1 The assemblies

| Assembly | Holds | May reference |
|---|---|---|
| `Daoris.Ports` (`src/Daoris.Ports/`) | the effect contracts and their value records, one folder per port family (`Git/`, `Processes/`, `Store/`, `Network/`, `Browser/`, `Agents/`) | the BCL |
| `Daoris.Contracts` (exists) | the route records and the parts kit, kept apart from the ports *(Codex)* | the BCL |
| `Daoris.Application` | the driver's domain rules and orchestration: every subdomain from Settings up to Loop (`Planner`, `LineCourse`, `Observation`, roles, participants, workflows, conversation and session policy, `Loop/Run`'s stages, the look's passes) *(Codex)*, and Platform's pure part (`Moments`, `JsonText`, `TextBounds`, `SafeNames`, `TomlReader`, `DaorisHome`'s paths) *(Fable)* | Ports, Contracts; no package but analyzers *(Fable)* |
| `Daoris.Service.Application` | Service.Core's policy: `QuestExchange` and its judgement parts, `QuestTransitions`, `SessionLedger`, ask and review policy, knowledge coordination, remote-sync policy *(Codex)* | Ports, Contracts |
| `Daoris.Testing` (`src/Daoris.Testing/`) | every fake (§5.1), the virtual clock (`MovedClock`, one copy where the driver's and the service's tests keep one each), `RoutedHandler`, `ScratchFolders`, `Poll` on virtual time, and the shared contract cases *(Codex; Fable)* | Ports, the test libraries |
| `Daoris.Desktop.Modules` | the Shenora bridge's inbound adapters: decode a request, call the application, present the answer, publish its slices *(Codex)* | Application, Contracts, Shenora's managed bridge |
| The roots: `Daoris.Desktop.App`, `Daoris.Desktop.Driver.Host`, `Daoris.Service.Http`, `Daoris.Service.Mcp` | composition: each port's implementation and its lifetime chosen; the only projects that reference adapters *(both)* | their application and the adapters they compose |
| `Daoris.Adapters.*` (`src/Daoris.Adapters/`) | one package per real dependency (§2.2, §2.3), each with its dependency test project beside it | Ports, their vendor packages, an adapter they are built over; never an application *(Codex)* |

Until row l, `Daoris.Desktop.Driver` is the application assembly, and the census holds it as one; adapters are extracted from it row by
row, and l moves what remains to `src/Daoris.Application/` and deletes the old project. It is never kept as a forwarding assembly
*(Codex)*. `Daoris.Service.Core` goes the same way in row h: its policy to `Daoris.Service.Application`, its SQLite to
`ServiceStore.Sqlite`, its `Composition/` to the service's roots *(this design)*. `Daoris.Desktop.Driver.Host` keeps its name: it is the
driver program, the window's *driver*, not the assembly that dissolves.

The small programs stay programs. `Daoris.Desktop.GitDoor` keeps compiling linked sources so that it starts small; the files it links
(`GitDoors`, `GitPushJudge`, `GitAccessRules`, `HomeLease`, `MachineLog` and the rest) are its declared source set, and a census holds
that none of them names a port *(Fable)*. The service's roots compose only the service's adapters (its store, the network, the clock,
Lyntai's memory, the files' knowledge source): a shared host is handed no process, browser or checkout adapter *(Codex)*.

### 2.2 The adapters

Each is a package with its dependency test project beside it: `src/Daoris.Adapters/Daoris.Adapters.<Name>/` and
`Daoris.Adapters.<Name>.Tests/`. The rows are Codex's; what each takes is Codex's list, with Fable's file names where Fable read further.

| Package | Implements | What moves into it |
|---|---|---|
| `Daoris.Adapters.Network` | the real `HttpMessageHandler` and its timeouts; `IServiceDoors`', `IRemote`'s and the downloads' transports; the instance pipe's stream | `Service/ServiceHttp`, `Platform/DriverHttp`'s handler, Service.Core's `HttpRemote` (`Remote/Remote.cs`) *(Codex)*; `Platform/Downloads`, `Install/ToolDownloads`, the loopback listens, `Instance/UserPipe`, `PipeName`, `InstanceClient`'s transport, the modules' `Platform/InstancePipe` *(Fable)* |
| `Daoris.Adapters.Clock` | `TimeProvider.System` and the quiet clock's machine reading | the system clock's and the timers' construction; the platform half of `Events/QuietClock` *(Codex)* |
| `Daoris.Adapters.Machine` | `IProcessHost`, `IMachineAdmission`, `IProcessLiveness`; KeySeal's seal (§3.4) | `Platform/ProcessStarts`, `ChildProcess`, `OwnedProcesses`, `ProcessJob`, `HeldChild`, `ProcessLiveness`, the launcher's containment, `Agents/KeySeal` *(Codex)*; `PseudoConsole`, `PieceLauncher`, `PieceJobs`, `TerminalLaunch`, `TerminalHandles`, `ObjectNamespace` *(Fable)* |
| `Daoris.Adapters.DesktopStore.Sqlite` | `IDesktopUnitOfWork` | `Store/`'s engine: `DesktopStore`, `DesktopStoreReader`, `DesktopWrite`, `StoreProcess`, `KeepLock`, `RecordLock`, the numbered-step runner; `Microsoft.Data.Sqlite` and its native bundle *(Codex; Fable)*. The steps (`DesktopStoreSteps`, the schema) stay the application's, handed to the engine at open *(Fable)* |
| `Daoris.Adapters.ServiceStore.Sqlite` | `IServiceUnitOfWork`, `IKnowledgeStore` | the SQLite halves of `Quests/Quests.cs` and of the session and ask stores, `Platform/ConnectionGate`, `Knowledge/Store/SqliteKnowledgeStore` *(Codex)* |
| `Daoris.Adapters.Git` | `IGit` (ring 1); `IGitRepository`, `IGitWorktrees`, `IGitTransport` (ring 2) | `Workplace/GitRuns`, `GitLaunch`, `Workplace/Git/*` (`GitLibrary*`, `GitCommands`, `GitCommandReaders`, `GitConfigFile`…), `GitForWindows`, `RefFiles`, `RefTransaction`, `Platform/GitScheduler`, `GitStartGate`, `GitPlace`, `GitPlaces`, `GitCallers`, `Install/ChildGit`, the git door's transport; LibGit2Sharp *(Codex; Fable)* |
| `Daoris.Adapters.Browser.Chromium` | `IInAppBrowser`, `IBrowserCaches` and the browser's session operations | the modules' `Browser/EngineBrowser`, `EngineBringUp`, `EngineProfile`, `EngineProfilePrepare`, `CdpChannel`, `CdpCookies`, `ProcessBrowserChild`, and `InAppBrowser`'s port allocation *(Codex; Fable)* |
| `Daoris.Adapters.Browser.Edge` | the same browser ports | `Browser/EdgeBrowser` and Edge's profile and process discovery *(Codex)* |
| `Daoris.Adapters.Knowledge.Files` | `IKnowledgeSource` | the filesystem acquisition of `Knowledge/Store/FileSystemKnowledgeSource`; parsing a document stays pure, in the application *(Codex)* |
| `Daoris.Adapters.Memory.Lyntai` | the service's memory-engine port | the code that constructs and calls Lyntai's memory and storage objects *(Codex)* |

Codex's `Files` package (`IHomeFiles`, `IRepositoryFiles`, `IFileChanges`, `IVolumeTime`) is not in the first wave (§3.5): `VolumeClock`,
`HomeLease`, `StatFence`, `TreeCopy`, `AtomicFile` and the watchers stay in the application with the filesystem. The modules keep
`BrowserModule` on `IInAppBrowser` and reference Shenora's cross-platform half alone *(Fable)*; the browser's profiles, a session's
ownership of its browser and the lazy endpoint go with the browser adapters *(Codex)*.

### 2.3 The agent packages

| Package | Holds |
|---|---|
| `Daoris.Adapters.AgentRuntime.Lyntai` | the one assembly that names Lyntai's agent layer: `Agents/Layer/*` (`LayerRun`, `LayerStart`, `LauncherRunner` and the rest), the translation of its readings and failures, and the launcher behind Lyntai's `IProcessRunner` *(Codex; Fable)* |
| `Daoris.Adapters.Agent.ClaudeNative`, `.ClaudeAcp`, `.CodexAcp`, `.Dsh` | one harness each: its registration, capabilities, vendor configuration and evidence translation *(Codex)*, that is `Agents/Providers/<Name>/` whole: its facts, spawns, word tables, usage question, own-home reads, hooks, `provider.json`, `pending.json` and its recorded frames *(Fable)* |
| `Daoris.Adapters.Agent.StandIns` | `Providers/Stub`, `AltStub` and their programs, composed by development roots and dependency suites only, never published *(Fable)* |

Each implements `IAgentProvider` over Lyntai's runtime and carries no transport (§7.1). A provider is found, not listed:
`Agents/Registry/Providers.cs`'s built list becomes a provider part read with `Parts.Declared` over the assemblies its root references,
so a new agent provider is a new package and one reference in its root, with no change to the application *(Fable)*. A plugin's
declared protocol agent (`DeclaredDoor`) stays data the plugin declares, read by the application's registry and run over the
runtime's protocol door *(Fable)*; Codex's package per plugin provider is not taken, since a plugin ships from its own repository
*(this design)*. Whether a build carries Lyntai's layer becomes whether its root references `AgentRuntime.Lyntai`: the
`DaorisLyntaiLayer` compile condition and `Compile Remove="Agents\Layer\**"` go *(Fable)*.

### 2.4 Policy stays in the application

Git's policy stays in the application: `GitAccessRules`, push approvals, the landing's choices, recovery-retention judgement and a
tree's end. Git's execution, its object parsing and the atomic ref update are the adapter's *(Codex)*. The scheduler's places, the
doors, the identities and the holds are Daoris's policy over git, so the Git adapter stays in this repository *(Fable)*.

Daoris keeps account selection and cooling, the quest and session decisions, role choices, connector permissions, repository ownership,
process admission and the translation into its records *(Codex)*; an `Agent.*` package holds what the host knows of its agent and
selects nothing *(Fable)*.

`ProcessStarts.StartAsync` calls `GitPlace.Admit` today, so moving those types blindly makes a cycle between Machine and Git. The
concrete call becomes an admission contract injected at the root, still refusing a git start without a place the scheduler issued
*(Codex)*.

### 2.5 The name

The packages are **ports and adapters** *(the steward)*: the name says what each package is, an adapter for one port, and
`Daoris.Ports` keeps its name so the pair reads as one pattern. It frees *driver*, which today names the application assembly that
dissolves into `Daoris.Application`. Codex's `Daoris.Drivers.*` and Fable's `Daoris.Infra.*` are not taken. The window's *driver* (the
glossary's word for the part that starts and watches sessions) is unchanged, and *adapter* reaches no window, so the glossary takes no
row.

## 3. Ports cut at the funnels (decision 2)

### 3.1 The funnels

Every real effect already passes one of about eight funnels *(Fable)*:

| Funnel | Today | Port |
|---|---|---|
| git | `Workplace/GitLaunch`, `GitRuns` | `IGit`, then `IGitRepository` (new) |
| processes | `Platform/ProcessStarts`, `ChildProcess` | `IProcessHost`, `IMachineAdmission`, `IProcessLiveness` (new) |
| network | `Platform/DriverHttp.Client(key, handler)` | `HttpMessageHandler` (the platform's) |
| store | `Store/DesktopStore`, the service's `Platform/ConnectionGate` | `DbConnection` behind `IDesktopUnitOfWork` and `IServiceUnitOfWork` (a new gate over the platform's types) |
| clock | `TimeProvider`, where it is used | `TimeProvider` (the platform's) |
| browser | `IInAppBrowser` (`Agents/InAppBrowserHost.cs`), `IBrowserCaches` | the same, moved to Ports |
| harness | `AgentRun` (`Agents/Contract/`), `HeldRun` | `IAgentProvider` and `IAgentRun` (new, over today's contract) |
| instance pipe | `Instance/UserPipe`, the modules' `Platform/InstancePipe` | a `Stream` the root's connect function opens (§3.4) |

LAYERS1's work is to route what bypasses a funnel through it (the 245 direct clock reads, the 80 process starts, the 64 SQLite names),
not to wrap every class.

### 3.2 The platform's seams

Where the base library already has the abstraction, it is the port, and no interface or adapter type is invented *(Fable)*:

- **The clock is `TimeProvider`,** required in constructors with no application default to `TimeProvider.System`, and delays take it
  (`Task.Delay(span, provider, ct)`) *(Codex)*. The quiet clock (machine-active time that stops while the machine sleeps, which
  quiet-session policy reads apart *(Codex)*) stays the delegate `Events/QuietClock` takes, its machine reading supplied by the Clock
  adapter; Codex's `IActiveClock` is not added.
- **The network is `HttpMessageHandler`.** `DriverHttp.Client(key, handler)` already takes one; the application builds no handler and
  no client over a URL. `ServiceClient(IServiceDoors)` stays the portable client, and its construction from a URL moves to the root
  *(Codex)*. The fake is `RoutedHandler` *(Fable)*.
- **The store is `DbConnection` and `DbTransaction`** (`System.Data.Common`) behind a unit-of-work gate (§3.3). The application
  writes its SQL against the base types and never names `SqliteConnection`, `SqliteCommand` or `SqliteDataReader` *(Fable)*. Today's
  `DesktopStore.Write<T>(family, Func<DesktopWrite, T>)` is the gate's shape already.

### 3.3 The new interfaces

Five families, in Codex's shape: asynchronous, cancellable, naming no `Process`, `SqliteConnection`, libgit2 object or Lyntai type
*(Codex)*.

```csharp
// Daoris.Ports/Git — ring 1: today's GitRuns at the argument level (Fable)
public interface IGit
{
    Task<GitAnswer> RunAsync(GitAsk ask, CancellationToken ct);
    Task<GitBytes> ReadBytesAsync(GitAsk ask, CancellationToken ct);
    IAsyncEnumerable<string> ReadLinesAsync(GitAsk ask, CancellationToken ct);
    string? InUse(string? home);
}
public sealed record GitAsk(string Folder, IReadOnlyList<string> Arguments, GitWait Wait, bool Writes,
                            string? Input = null, TimeSpan? Bound = null, string? Home = null);

// ring 2: typed, grown family by family (Codex's shape)
public interface IGitRepository
{
    ValueTask<GitAnswer> ReadAsync(RepositoryId repository, GitQuestion question, CancellationToken ct);
    ValueTask<RefCommitResult> CommitRefsAsync(RepositoryId repository, RefTransaction transaction, CancellationToken ct);
}

// Daoris.Ports/Processes (Codex)
public interface IProcessHost
{
    ValueTask<IChildRun> StartAsync(ProcessSpec specification, CancellationToken ct);
}
public interface IChildRun : IAsyncDisposable
{
    IAsyncEnumerable<ProcessOutput> Output(CancellationToken ct);
    ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken ct);
    ValueTask<ProcessEnd> EndAsync(CancellationToken ct);
    ValueTask StopAsync(CancellationToken ct);
}

// Daoris.Ports/Store — the gate over the platform's types (Codex's shape, Fable's seam)
public interface IDesktopUnitOfWork
{
    ValueTask<T> ReadAsync<T>(Func<DbConnection, CancellationToken, ValueTask<T>> read, CancellationToken ct);
    ValueTask<T> WriteAsync<T>(string family, Func<DbTransaction, CancellationToken, ValueTask<T>> write, CancellationToken ct);
}

// Daoris.Ports/Agents (Codex)
public interface IAgentProvider
{
    ProviderDescription Description { get; }
    ValueTask<IAgentRun> CreateAsync(AgentStart start, CancellationToken ct);
}
```

- **Git.** `GitAsk` carries what `GitRuns` takes today, and `InUse` is `GitRuns.GitInUse` *(Fable)*. Ring 2's `GitQuestion` is a typed
  family of supported observations, never a free command string, and `RefTransaction` carries every expected ref value with every
  update, kept as one `update-ref --stdin` transaction so a proof and its deletion stay atomic *(Codex)*. `IGitWorktrees` and
  `IGitTransport` take the same shape as their families move.
- **Processes.** `ProcessSpec` is today's `ProcessRecipe` (`Agents/Contract/ProcessRecipe.cs`) with its purpose and admission class:
  arguments, the environment added and removed, the working folder *(Codex; Fable)*. The application can neither make nor inspect a
  real `Process`; a `ProcessStartInfo` stays allowed as a bag of data where a provider hands its servers (`ISpawn.HandServers`), and
  starting one is what is banned *(Fable)*. `IMachineAdmission` places a start of a class and answers the place to release, and
  `IProcessLiveness` says whether the process of an id and a creation time still runs *(Codex; Fable)*.
- **Store.** A write is `BEGIN IMMEDIATE`, the writer's stamp, the family's revision moved and the wake after the commit, as
  `DesktopStore.Write` does now; a read takes no write lock. `IServiceUnitOfWork` is the same shape over the service's database and
  replaces `QuestTx`'s `SqliteTransaction`: log-and-projection writes stay atomic, leases exclusive, and the commit before the wake
  *(Codex)*.
- **Harness.** `IAgentRun` is today's `AgentRun` as an interface: open, resume, its two word ports (a native door's `Input`,
  acknowledged when the harness names its conversation and replayed to the next process; a protocol door's `Words`, each a request its
  session answers), stop and end, never flattened into one `SendText` *(Codex)*. `ProviderDescription` is today's `Provider` record and
  its parts in `Agents/Contract/`, which move to Ports with it *(Fable)*.
- **Browser.** `IInAppBrowser` and `IBrowserCaches` exist and move to Ports as they are; the session operations the modules call join
  them in row i *(Codex; Fable)*.

### 3.4 What else crosses

Anything else an adapter supplies crosses as a delegate or a value record in Ports, never a new interface *(this design)*: the quiet
clock's reading, the instance pipe's connect (a function answering a `Stream`), KeySeal's seal and unseal. The interfaces that are
ports already move to Ports unchanged: `IServiceDoors`, `IRemote`, `IRemotes`, `IKnowledgeStore`, `IKnowledgeSource`, `IInAppBrowser`,
`IBrowserCaches` *(Codex; Fable)*. Those that are the application's own seams (`IWorkflowStore`, `IWorkflowRunStore`,
`IWorkflowRunRecords`, and the about 150 wiring interfaces) stay in the application and are called directly *(Fable)*; `WorkflowPorts`
stops constructing its file-backed stores, which the root hands it *(Codex)*.

### 3.5 The filesystem, deferred

The first wave adds no filesystem port *(Fable)*. `File.` and `Directory.` are named in about 517 application files, and an
`IFileSystem` across them is interfaces everywhere under another name. A unit test writes under `ScratchFolders.New` and nowhere else,
which `TestHomesCensusTests` holds today. Where the timing census shows a unit class over its second because of the files it touches,
Codex's `Files` port (`IHomeFiles`, `IRepositoryFiles`, `IFileChanges`, `IVolumeTime`) is added for that family alone, with its fake
and its contract, never as a sweep *(Codex's shape, Fable's trigger)*.

## 4. Versioning by fingerprint pin (decision 3)

### 4.1 The fingerprint

An in-repository adapter's version is its **fingerprint**, one hash over *(Fable)*:

1. its folder's git tree hash (`git rev-parse <commit>:src/Daoris.Adapters/Daoris.Adapters.Git`), exact and free;
2. the tree hash of each `Daoris.Ports` folder it implements or calls;
3. the fingerprint of each adapter it is built over (an `Agent.*` package over `AgentRuntime.Lyntai`); an adapter reaches another
   through its port wherever one exists, never its implementation *(Codex)*;
4. the pinned version of each outside thing it drives, read where that pin is written: the git floor and MinGit in `resources.json`,
   LibGit2Sharp and `Microsoft.Data.Sqlite` in `Directory.Packages.props`, the CEF build through `Shenora.Chromium`'s pin, an agent's
   binary pin, Lyntai's pin for the Lyntai adapters.

The fourth makes the owner's rule hold: a dependency suite's verdict is a function of the adapter's code **and** of what it drives,
so moving the git floor, the CEF build or an agent's pin runs that one suite, where today only a comment says so *(Fable)*. A pin the
package does not read moves nothing *(Codex)*. The build files that reach every project (the root's and the desktop tree's
`Directory.Build.props`) are named in every fingerprint, at the price of every suite running when they move *(this design, from
Codex's build identity)*.

The manifest is `adapter.json` in the package's folder: the outside pins it reads and the name of its dependency suite. Its references
are read from its project file and never repeated *(Fable, narrowed to what the project file does not say)*. The fingerprint is the
version: there is no hand-kept SemVer for an in-repository package, so nothing is edited by hand and the release workflow stamps
nothing here. `Daoris.Ports` keeps an API baseline file in its folders, so every change to a port's surface is a line a reviewer reads
and moves its implementers' fingerprints *(Fable)*.

### 4.2 The lock

`daoris.adapters.lock`, tracked at the root, holds one row per package *(Fable)*:

```json
{
  "schema": 1,
  "adapters": {
    "Daoris.Adapters.Git": {
      "fingerprint": "<sha256>",
      "tree": "<the folder's tree hash>",
      "ports": { "Git": "<tree hash>", "Processes": "<tree hash>" },
      "outside": { "tool:git": "2.56.0", "nuget:LibGit2Sharp": "<pin>" },
      "suite": "adapters-git",
      "toolchain": "<the gate's toolchain digest>",
      "passed": "<when the suite passed>"
    }
  }
}
```

A row is written only by a complete green run of that package's dependency suite and its contract cases on the real side at that
fingerprint; an interrupted, partial or flaky run writes nothing *(Codex)*. The lock is a portable verdict: the machine's store is one
machine's, while the lock travels with the commit to every worktree, the CI and the next session *(Fable)*.

### 4.3 What a merge reads

The gate `adapters-lock` runs at every merge, in milliseconds *(Fable)*. For each package it computes the fingerprint and compares it
with the row:

- **equal:** the package's dependency gate is recorded as kept, the row its verdict, and nothing below the package runs;
- **different:** that package's dependency suite runs, and green rewrites its row in the merge's commit. Only that package's: a change
  to Codex's provider runs Codex's suite, and not git's, SQLite's, the browsers', Claude's or dsh's *(Codex)*;
- **a row a branch brings** stands where the machine's verdict store holds that pass under the same key, and otherwise the merge runs
  the suite itself; a row on main stands, since it came through a merge *(this design)*;
- **a package no manifest declares** fails the plan, naming the declaration it lacks *(Codex)*.

A merge never runs an unchanged package's suite to be safe; a stale or missing row is reported as the run the package needs *(Codex)*.
The full set and the product's acceptance are not a label under which an unchanged package's suite runs again *(Codex)*.

### 4.4 One build per fingerprint

`tools/merge/adapters.ts`, typed as MERGETS1 writes the merge tool's new modules, builds a package once per fingerprint into
`local/adapters/<name>/<fingerprint>/` in the main checkout, the verdict store's home, for every worktree on the machine *(Fable)*. A
folder is written once, complete, then only read, so a test host that holds an assembly never meets a half-written one *(Fable)*.
Each consuming project keeps its `ProjectReference` to the package; a generated, ignored props file in each tree, as
`local/lyntai-local.props` sets Lyntai's feed, resolves an unchanged package to its cached build, which the build copies into the
tree's own output, never linked (D3). A package edited in the tree builds from its source like any project, and its fingerprint is
taken at the commit. So no gate rebuilds an unchanged adapter, which answers Codex's objection to project references *(Codex's
objection; Fable's cache)*.

### 4.5 How a change in an adapter reaches the application

A branch that changes an adapter carries its suite's pass in the same commit: `node tools/merge/adapters.ts release <name>` runs the package's dependency
suite and its contract cases at the current fingerprint and, green, rewrites the row *(Fable)*. The merge runs the same command where a
row is stale, so every commit of main is pinned to builds whose suites passed. The install's `build.json` lists each package with its
fingerprint, so an update can say which dependency moved *(Fable)*. A change to a port moves the fingerprint of every adapter that
implements or calls that folder, and each of them runs *(Codex)*.

### 4.6 The verdict store and the merge's key

The lock joins the two records that exist and adds no third store:

- **MERGEFLOW1's key.** A gate that declares `reuse` replays a standing pass whose reaching inputs, command and toolchain are unchanged,
  kept in the one store under the merge tool's one reach table, whose digest the key holds. An adapter's dependency gate declares its
  reuse by fingerprint: its reaching inputs' digest **is** the package's fingerprint, so its key is the gate, the fingerprint, the
  command and the toolchain, and the reach table has no part in it, since the fingerprint's inputs are its reach.
- **D285's store.** The machine's one verdict store keeps every run of an adapter gate with its log under that key, as it keeps every
  other gate's. The lock row is that key with the moment it passed, made tracked so that it travels; the store backs a branch's row on
  the machine that wrote it (§4.3) and keeps the logs the lock does not.
- **`verdictStanding`.** For a fingerprint gate only `exact` stands: no `records` or `unreached` standing, and no sweep bound or 24-hour
  expiry, so an unchanged package never runs again because the application moved or time passed *(Codex)*.
- **The declarations.** An adapter gate's `paths` in `daoris.gates.json` stop being hand lists (the `driver-process` row lists
  `.gitignore`, `.codex/`, `package.json`…) and are generated from the manifests and the project graph, checked by `tools/test-map.mjs`
  as it checks `REACH`'s rows *(Fable)*; `daoris.lanes.json` gains each package's folder as its lane *(Codex)*.

Whether a merge runs git's dependency tests therefore has one answer in one place, the lock row; the key and the store are what the
row is made of.

### 4.7 No feed inside the repository

Packages inside this repository are consumed through the fingerprint cache, never a NuGet feed *(Fable)*: a feed puts a pack and a
restore between an edit and its test, is machine state in each of many worktrees, needs a version edited at every change, and serves
stale bytes from the package cache whenever a version is reused. `Daoris.Ports` becomes a NuGet package only when a consumer outside
this repository needs it. A package that graduates to a sibling (§7.2) becomes a NuGet pin through `tools/sibling-local.mjs`, the
pin's name, version and content the same shape on both sides *(Fable)*. Lyntai and Shenora stay NuGet at released pins (D22).

## 5. Twin-proved fakes (decision 4)

### 5.1 One contract suite, two sides

Each adapter ships its fake in `Daoris.Testing`, and its contract is written once against its port, as cases handed a backend
*(Fable; Codex)*:

| Port | Fake | Contract |
|---|---|---|
| `IGit` | `ScriptedGit`: answers by a predicate on the arguments, as `GitRuns.Answered` does today | `GitContract` |
| `IGitRepository` | `MemoryRepository`: commits, refs, an index and worktree entries, atomic expected-value updates; bounded by the questions ring 2 declares, never by git | `RepositoryContract` |
| `IProcessHost`, `IMachineAdmission`, `IProcessLiveness` | `ScriptedProcesses`: output, exit and cancellation as events | `ProcessContract` |
| `TimeProvider` | `MovedClock` | the platform's |
| `HttpMessageHandler`, `IServiceDoors`, `IRemote` | `RoutedHandler`, `MemoryServiceDoors`, `MemoryRemote`, with scripted transport failures | `NetworkContract` |
| `IDesktopUnitOfWork`, `IServiceUnitOfWork` | `MemoryUnitOfWork`: the gate on SQLite in memory, run on the application's own steps | `UnitOfWorkContract`: rollback, the write lock's exclusion, the stamp, the revision, the commit before the wake |
| `IInAppBrowser`, `IBrowserCaches` | `StillBrowser`: endpoint, tabs, visibility, a session's ownership | `BrowserContract` |
| `IAgentProvider`, `IAgentRun` | `ScriptedAgentProvider` and `ScriptedAgentRun`, until Lyntai's scripted provider stands (§7.3) | the provider conformance cases on recorded frames |

The cases on the fake run in `Daoris.Testing.Tests`, the unit tier, at every merge. The same cases on the real adapter run in
`Daoris.Adapters.<Name>.Tests` beside the adapter's own dependency cases, only when its fingerprint differs from its row. A fake that
drifts from the real thing fails its own package's run, at the package's change, not an application test months later *(Fable)*. The
house already works this way in places: `IUsageQuestion`'s recorded frames, `ProviderConformanceLive.cs`, the agent-Lyntai design's
§6.2 *(Fable)*.

The store's fake is the one that is not a fake: SQLite in memory is still SQLite *(Codex)*. It is taken because the application keeps
its SQL (§3.2), and it runs in milliseconds. What the file adds (the write lock across processes, the journal, a crash, a downgrade, an
older build's open) is the real side's to prove, and `DesktopStoreTests`, which opens real files today with no Process tag, moves there
*(Codex)*.

### 5.2 The tiers

| Tier | Projects | May touch | Runs |
|---|---|---|---|
| Unit | `Daoris.Application.Tests`, `Daoris.Service.Application.Tests`, `Daoris.Desktop.Modules.Tests`, `Daoris.Testing.Tests` | fakes, virtual time, the store in memory, files under the scratch root | at every merge |
| Dependency | `Daoris.Adapters.<Name>.Tests` | the package's one real dependency | when the package's fingerprint differs from its lock row |
| Rehearsal | `Daoris.Application.Rehearsals` and `Daoris.Service.Application.Rehearsals` (the application over real adapters: §5.3's twin runs, the retiring transports' stand-in cases), the family and deployment rehearsals, the installed proofs | everything | the CI beside, and before a stage |

*(Fable's three tiers; Codex's contract and census tiers are the unit tier's contract cases and §6, and its product acceptance is the
rehearsal tier.)* A class in a rehearsal project that has no fake twin yet stays in `reached`'s selection, as it is today, until its
family moves to a fake; a twin run never runs at a merge *(this design)*. The unit tier runs whole once it runs under a minute, and
then `tools/merge-branch/reached.mjs`' nearness, bounds and counts go for the .NET suites *(Fable)*. Each test project is built once per
tree and its later runs take `--no-build --no-restore` *(Codex; MERGEFLOW1 builds once per tree)*.

### 5.3 Git in two rings

Git is the widest funnel, about 224 calling files, and moves in two rings *(Fable)*:

- **Ring 1 (row e) is mechanical.** `GitRuns`' three runners become `IGit`; its three `AsyncLocal` seams (`Started`, `Answered`, `Runs`)
  are deleted and the test files that set them move to `ScriptedGit`; the application's statics read the port. The classes that prove
  git itself (`GitLaunchTests`, `GitSchedulerTests`, the git library's `GitLibrary*Tests` and `GitLibraryParity`, the pinned-MinGit
  cases, the doors' SSH stand-in cases) move into the Git adapter's suite *(Fable; Codex)*.
- **Ring 2 (row f) is where the milliseconds come from.** A family that asks git more than a handful of questions moves them to
  `IGitRepository` members, with their output parsers into the adapter; its fake is `MemoryRepository`. Its scenario classes change
  their fixture from `GitFixture` to a `GitWorld` handed a backend: on the fake it is a unit test, on real git a twin run in the
  rehearsal tier, and a divergence is a fake's defect that becomes a contract case *(Fable)*. Families move one at a time, in the order
  the timing census ranks them by their minutes (Fable's reading: Pool, then Integration, Landing and Review's graph).

## 6. A census by construction (decision 5)

Four checks, the cheapest first:

1. **The reference graph.** `StructureTests` gains one assertion per project: the application assemblies and every unit-test project
   reference no `Daoris.Adapters.*` and no package but analyzers and the test libraries *(Fable; Codex)*. In a unit test process
   nothing real is reachable.
2. **The compiler.** The banned-API analyzer already runs on every shipping desktop project (`src/Daoris.Desktop/Directory.Build.props`,
   `BannedSymbols.txt`, the one `src/Daoris.Desktop/.editorconfig`, held by `DeclarationBoundaryTests`). Its list gains the effects and
   the analyzer reaches the application and the unit-test projects: `Process.Start`, `Process.Kill`, `Process.GetProcess*`;
   `Thread.Sleep` and the `Task.Delay` overloads with no `TimeProvider`; `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow`,
   `DateTimeOffset.Now`, `TimeProvider.System`; `TcpListener`, `HttpListener`, `Socket`, `NamedPipeServerStream`,
   `NamedPipeClientStream`, and `HttpClient`'s constructor with no handler; and from row b `SqliteConnection` *(Fable; Codex)*. A
   violation is a build error in seconds and costs no test time. Each current use enters the `.editorconfig`'s allowance as a section of
   its file, and the allowance only shrinks; each row takes its own symbols to zero for the code it moves *(Fable)*. The analyzer binds
   symbols, not spellings, so an alias, a shared helper or a constructor that starts a process is caught at the call, and planted cases
   (an alias, a helper, a "fake" that launches a process) prove it *(Codex)*.
3. **The clock on the tests.** A unit test over one second fails, named, read from the duration records each unit gate writes under
   `_fixtures/test-timing/` (TESTTIME1) and the merge's record (MERGEFLOW1) *(Fable)*. It catches what no symbol names: a busy loop, a
   slow walk of files.
4. **The bridge.** Until a package is extracted, TESTTIME1's `[Trait(Category.Needs, Needed.X)]` names what a class needs, and
   `NeedsTraitTests` holds that no Process class goes without; `GitStartTally`, `FixtureSpawns` and `HeldOpens` write a ledger per class,
   and an untagged class with a start or an open fails *(Fable)*. When the package is extracted its tagged classes leave the unit
   projects in the same row: those that prove the package to its suite, those that prove the application over the real thing to a
   rehearsal project, and a class that can run on the fake becomes a unit class; the gates that ran them run them where they moved
   *(this design)*. From then on the tier is the assembly, and the last
   tagged class takes `CategoryTests`, `Alone.cs`'s collections and the three run-settings files with it *(Fable)*.

**The ambient bridge.** Where a part has a wiring interface (`ILookWiring`, `ITurnWiring`, `IExecutorWiring`), the wiring carries the
ports. A static funnel (`WorkingTree`, `ChildProcess`, `DriverHttp`, `Moments`) may read an ambient `Machine` record of the ports,
set once by the root, only as a bridge: a ratchet file counts its reads per subdomain and only tightens, as `tools/size-ratchet.json`
does, and no new code reads it *(Fable)*. It is a service locator, named as one, and never a new seam; in a unit test process nothing
real is behind it, because nothing real is referenced.

Each row's census is named in §8.

## 7. The siblings (decision 6)

### 7.1 Lyntai

- Agent transports and their conformance corpora belong in Lyntai (AGENTLYNTAI1; D213, D248; the agent-Lyntai design's §2.1, the layer
  is what a door is) *(Fable; Codex)*. Lyntai owns the native and protocol transports and their session machinery, the vendors'
  protocol parsing, the normalised capabilities and the failure and usage evidence, the reusable provider implementations, and its
  memory engine *(Codex)*.
- A door's conformance suite and recorded wire corpus are that transport's dependency tests: run at Lyntai's release, they run at no
  Daoris merge, which is the owner's rule kept by a repository boundary *(Fable)*.
- So Daoris's `Agent.*` packages are thin registrations over `AgentRuntime.Lyntai` *(Codex's row)*: Lyntai translates a door, and
  Daoris selects *(Fable)*.
- **No transport AGENTLYNTAI1 is retiring is packaged** *(Fable)*. `TransportRun`, `Acp*`, `Native*`, `ClaudeStreams`, `ReadingStreams`
  and `ConsoleRelay` stay in the application, starting their processes through `IProcessHost` once row d stands, until AGENTLYNTAI1
  retires each with its last door (`AcpSession` with 1e-iv, the native transport with 1f-ii); their stand-in cases run in the rehearsal
  tier meanwhile. Row j takes a door's `Agent.*` package when AGENTLYNTAI1 has made that door layer-run, never before.
- Lyntai's layer needs a process started for it and never owns one: `LauncherRunner` already hands it the host's launcher behind
  `IProcessRunner`, which is `IProcessHost` handed to the sibling, keeping Lyntai free of Shenora and Shenora of Lyntai *(Fable)*.

### 7.2 Shenora, and the graduation rule

- **Process ownership** (`ProcessJob`, `OwnedProcesses`, `HeldChild`, `PseudoConsole`, `ProcessLiveness`, `ObjectNamespace`,
  `TerminalHandles`): Windows interop with no Daoris noun, and any host that starts children needs a job that ends them; Shenora
  already holds the shell's launcher and the single instance *(Fable)*.
- **The engine's bring-up and its channel** (`EngineBringUp`, `EngineProfile`, `CdpChannel`, `CdpCookies`): they drive
  `Shenora.Chromium`'s own engine, and their dependency tests are CEF's *(Fable)*. Daoris's browser adapter keeps its profiles, a
  session's ownership of its browser, the lazy endpoint and what is Daoris's own *(Codex)*.
- **The rule** *(Fable's)*. A package, or a part of one, graduates to a sibling when its public surface names no Daoris noun, when a
  second consumer exists or the sibling's owner asks for it, and when that surface has stood unchanged across two of Daoris's releases
  (Fable's "two of its own minor versions", read through §4.1's versions). Until then it incubates in `Daoris.Adapters.Machine` or
  `Daoris.Adapters.Browser.Chromium`, and its move is a request to that repository, never an edit. After the move the adapter is a thin
  layer over the sibling's package, its lock row's outside pin the sibling's version.
- **Staying here** *(Fable)*: Git, since no sibling runs git and its places, doors, identities and holds are Daoris's policy; both
  stores, small engines over Daoris's own steps (the driver takes Lyntai's two agent packages and not its storage, deliberately); the
  `Agent.*` registrations.

### 7.3 The quests

Daoris would send each as a quest to that repository, saying what is needed and why, with the evidence, not the change *(Fable's list,
this design's wording)*:

- **LY1, to Lyntai: a scripted provider in a testing package,** proved by Lyntai's own contract suite, so the harness fake Daoris's
  unit tests use is Lyntai's and not a second one of Daoris's. Sent in row j.
- **LY2, to Lyntai: the conformance suites and recorded corpora Daoris's provider tests hold today** (`ProviderConformanceLive.cs`'s
  cases, each door's recorded frames) kept and run at Lyntai's release with the transports AGENTLYNTAI1 retires. Sent in row j, beside
  that row's own requests (`docs/2026-11-06-lyntai-requests.md`).
- **SH1, to Shenora: process ownership as a package of its own,** with its dependency suite (a job that ends a host's children when
  the host exits, owned processes tracked by id and creation time, a held child, a pseudo console). Sent when §7.2's rule holds for
  Machine's parts.
- **SH2, to Shenora: the engine's bring-up, its CDP channel and its cookies beside `Shenora.Chromium`,** their dependency tests run
  against CEF at Shenora's release. Sent when the rule holds for the browser adapter's parts.

No row waits on them: each package stands while its part incubates here.

## 8. Rows, in order (decision 7)

Each row is one builder's package. It lands with its proof seen failing first and the build green, and rewrites in the same chunk the
area sentences its change makes untrue (D228). Rows b to k may run as parallel builders once a has merged, at most three at a time.
Their edges: e, i and j after d, since they start processes through its port; f after e; j's doors as AGENTLYNTAI1 makes them
layer-run; l after b to k; z last.

| Row (lanes; after) | What moves | The census that holds it | Proof | What it removes |
|---|---|---|---|---|
| **a** the skeletons and the census (driver, service, modules, tools; first) | `src/Daoris.Ports/` with its folders, the ports of §3.4 that name only base types, and the ambient `Machine` record; `src/Daoris.Testing/` with `MovedClock`, `RoutedHandler`, `ScratchFolders`, `Poll` on virtual time; `Daoris.Testing.Tests`; the two rehearsal projects, empty | the reference graph; the banned effects with every current use allowed by its file; the one-second unit budget with today's slower tests listed; the ambient ratchet at zero | `StructureTests`: Ports references the BCL alone; a planted `Process.Start`, `Thread.Sleep`, `DateTime.UtcNow` and `TcpListener` in the application fail the build; a planted two-second unit test fails the budget | the second `MovedClock` |
| **b** DesktopStore.Sqlite, the pipeline's proof (driver, tools; a) | `Store/`'s engine to the adapter; `IDesktopUnitOfWork`; the 64 files naming `Microsoft.Data.Sqlite` onto `System.Data.Common`; `adapter.json`, `daoris.adapters.lock`, the `adapters-lock` gate, the cache, `adapters release`; `MemoryUnitOfWork` and `UnitOfWorkContract` on both sides; the unit classes that open a real store (about 41 files *(Fable)*, named by the compiler once the engine has left) onto `MemoryUnitOfWork` or into the adapter's suite | the application references no SQLite package; `SqliteConnection`, `SqliteCommand`, `SqliteDataReader` banned there | a merge that changes no package records the store's gate as kept from its row and runs nothing; a change in the package's folder, or in the SQLite pin alone, runs its suite and rewrites the row; a second tree reuses the cached build; the cold build's time against today's | the driver's SQLite reference; `DesktopStoreTests` in the unit half |
| **c** Clock (driver, service; a) | `TimeProvider` required in constructors, Workplace and Loop first; `Poll` on virtual time; the Clock adapter with `TimeProvider.System` and the quiet clock's reading | the allowance for the clock and delay symbols at zero per subdomain, then whole | the fast halves' 17 classes with a wait of 500 ms or more (TESTTIME1 §6) each under a second on `MovedClock` | `TreeContext.Clock`'s default to the wall clock; `WatchRun`'s real delay *(Codex)* |
| **d** Machine, processes and admission (driver, modules; a) | §2.2's Machine row behind `IProcessHost`, `IMachineAdmission`, `IProcessLiveness`; the `GitPlace.Admit` call injected at the root; KeySeal; every class tagged `Needs=Process` out of the unit projects (§6, point 4), the widest move of the rows, since every Process class starts one | process starts banned in the application; `TerminalStartsCensusTests` reads the port; no unit project carries `Needs=Process` | every start still admitted, owned and contained *(Codex)*; `ChildProcessTests` and the job and ownership cases on the adapter; `ProcessContract` on both sides; the `driver-process*` gates run their classes from where they moved | the application's concrete `Process` handles; the Machine-to-Git call |
| **e** Git, ring 1 (driver; d) | `IGit`; `GitContract` on git and on `ScriptedGit`; git's own proofs to the adapter's suite | LibGit2Sharp referenced by one project; no application file reads `.git` (`RefFiles` moved) *(Fable)* | §5.3's ring-1 classes green on the adapter; a git floor moved in `resources.json` runs this suite alone | `GitRuns.Started`, `Answered`, `Runs` |
| **f** Git, ring 2 (driver; e; one family per merge, in the timing census's order) | a family's questions to `IGitRepository` with their parsers; `MemoryRepository`, `RepositoryContract`; its scenarios on `GitWorld` | per family: its classes carry no `Needs=Git` and no `GitFixture` | the family's classes under a second each on the fake; their twin run green on real git | that family's real-git unit classes |
| **g** Network (driver, service, modules; a) | `ServiceHttp`, `DriverHttp`'s handler, `HttpRemote`, the downloads, the listens and the instance pipe; `ServiceClient`'s construction from a URL to the roots | sockets, listeners and pipes banned in the application | `NetworkContract` on both sides; `HttpRemoteTests` on the adapter | `ServiceClient(string baseUrl, …)` |
| **h** ServiceStore.Sqlite and Service.Application (service; a, b) | `IServiceUnitOfWork` in place of `QuestTx`'s transaction; the service's SQLite to the adapter; Service.Core's policy to `Daoris.Service.Application`; `Composition/` to the roots | the service's structure fixture gains the reference assertion; SQLite's types banned in its application | log-and-projection writes and leases atomic through the gate *(Codex)*; `ConnectionGateTests` on the adapter | `Daoris.Service.Core` |
| **i** Browser (modules; d) | Browser.Chromium and Browser.Edge out of the modules | the modules reference Shenora's cross-platform half alone and start no browser process *(Codex; Fable)* | `BrowserContract` on both sides; `EngineBrowserTests` and `ProcessBrowserChildTests` on the adapter | `InAppBrowser.FreePort()`'s listener in the modules |
| **j** the `Agent.*` registrations over AgentRuntime.Lyntai (driver; d; each door as AGENTLYNTAI1 makes it layer-run) | `AgentRuntime.Lyntai` from `Agents/Layer/`; one `Agent.*` package per door; providers found as parts; LY1 and LY2 sent | the application names no vendor: no `claude`, `codex` or `dsh` literal outside their packages *(Fable)*; its events name no Lyntai type *(Codex)* | a change to Codex's provider runs Codex's suite alone; the provider parity cases on recorded frames | `Providers.cs`'s list; the `DaorisLyntaiLayer` condition |
| **k** Knowledge.Files and Memory.Lyntai (service; a) | the files' acquisition and Lyntai's memory construction | the service's application names no Lyntai storage type and no knowledge file read | each adapter's contract on both sides | — |
| **l** the rest, and the driver deleted (driver, modules, tools; b to k) | what remains to `src/Daoris.Application/`; `Daoris.Desktop.Driver` deleted; the broad Process gates' hand reach removed | no forwarding type, no duplicate implementation, no `Needs` trait in a unit project | the whole build and the unit tier green; `daoris.gates.json` lists no `driver-process*` row | `Daoris.Desktop.Driver`; `CategoryTests`, `Alone.cs` and the run-settings files; `reached`'s .NET half once the unit tier runs whole under a minute |
| **z** measured (the steward; all) | — | — | §9's figures before a and after l, and its three exercises | — |

**Row f, measured before it moved anything (2026-11-14, this machine; the count is from the classes' own `Needs` traits, the time from a
sample run).** The driver's unit half (`Category!=Process`) already starts no real git: no class there carries `Needs=Git`, and 20 classes
script the git they ask (`GitFlow`, `ScriptedGit`), so the "mixed" classes are unit tests whose only real git is a scratch cleanup, or fault
injection over a real repository. The real git is in the Process classes: `Daoris.Desktop.Driver.Tests` holds 261 `Needs=Git` classes with
1,199 cases (127 classes and 678 cases in the `Parallel=Git` half), and the rehearsal project 72 classes with 661 cases (the landings', pool's,
integrations', clean-up's, keep-size's and help trees' scenario classes, already moved there from the driver tests). A sample of 12 of the
heaviest classes still in the driver tests, run four at once under `git.process.runsettings`, was 63 cases, 782 case-seconds and 259 s of
wall: 12.4 s a case, which is a worktree opened and a few dozen git starts each. Those cases test the product over real worktrees, files and
refs (a tree opened, a commit made, a landing judged), so a scripted answer table cannot hold their assertions; they need ring 2's typed
repository port and its `MemoryRepository` first, which no family has yet.

## 9. Measurement (decision 8)

| Measure | 2026-11-12 | Target |
|---|---|---|
| Dependency-test minutes of a merge that changed no package | 10 to 21 in `reached`, and the Process halves in the CI | **0, read from the lock** |
| Dependency-test minutes of a merge that changed one package | — | that package's suite alone, under 10 |
| Unit tier wall time, every .NET unit project, prebuilt | the fast halves 7 to 11 min | under 60 s, the application's under 20 s *(Codex; Fable)* |
| The slowest unit test | not recorded | under 1 s (§6) |
| Merge wall time, median and 95th percentile | the path-reached gate 10 to 21 min | under 5 min *(both)* |
| Test-project builds per merge | one per gate | one *(Fable; MERGEFLOW1)* |
| A package's whole dependency suite | the driver's Process suite 48 min | under 10 each *(Fable)* |
| The cold build | not recorded | measured at b; the extraction stops if it grows *(Fable)* |
| Classes with a `Needs` trait in a unit project | about 470 Process classes | zero at l *(Fable)* |
| Reads of the ambient `Machine` | — | falling at every row from e *(Fable)* |

Each figure is taken with `tools/dev-metrics.mjs` from the merge's record and the gates' duration records, one gate alone on a quiet
machine (rule 17 of the code-structure area), cold and warm labelled, before row a and after each row. Every run records the packages
it changed, the rows it kept, the tests it selected, its restore, build and test time and its total dependency minutes, so concurrency
cannot hide a cost *(Codex)*.

Row z's three exercises *(Codex)*: change only `Planner`; change only a workflow rule; change only Codex's provider. The first two run
no dependency test; the third runs Codex's suite and not git's, SQLite's, the browsers', Claude's or dsh's. The whole-suite budget
stays unmet until a complete run measures under ten minutes, and no rename or exclusion of a test counts as meeting it *(Codex)*.

## 10. What is removed (D228: one way)

Each goes in the row that replaces it, with its words, tests and area sentences:

- `Daoris.Desktop.Driver` (l) and `Daoris.Service.Core` (h), with no forwarding assembly.
- The second `MovedClock` (a).
- The driver's SQLite reference and the application's SQLite types (b).
- The application's defaults to the system clock and its real delays (c).
- The application's concrete process handles and the call from process starts into git's places (d).
- `GitRuns`' three `AsyncLocal` seams (e).
- `ServiceClient`'s construction from a URL inside the application (g).
- The hand-listed `paths` of each dependency gate, and the sweep bound for a fingerprint gate (b onward, by package).
- `Providers.cs`'s built list, the `DaorisLyntaiLayer` condition and `Compile Remove="Agents\Layer\**"` (j).
- TESTTIME1's `Needs` traits, `CategoryTests`, `Alone.cs`'s collections and the run-settings files, and `reached`'s .NET nearness and
  bounds once the unit tier runs whole (l).

## 11. Rejected

- **One big infrastructure assembly** *(both)*: it gives the layering and nothing else; any change below runs every dependency suite, a
  new agent is not a new package, and "nothing below changed" has no unit to be true of.
- **Interfaces everywhere** *(both)*: a repository interface per store family, an interface per git command, `IFileSystem`, interfaces
  on pure planners, parsers and graph operations. Thousands of seams whose fakes nobody proves, beside about 150 wiring interfaces
  already; ports sit at the funnels, and inside the application a type is called directly.
- **A service split** *(both)*: git, the agents or the store as processes behind a wire add a process, a socket and a wait to every
  call, which the census forbids in a test, and break the rule that every process runs under the application and the local-first
  constraint. This is a build and test boundary, not a deployment one, and the one process boundary that is right, the service,
  exists.
- **A NuGet feed for packages inside the repository** *(Codex's proposal; Fable's rejection)*: §4.7.
- **Porting the filesystem now** *(Codex's `Files`; Fable's rejection)*: §3.5. Codex's point, that moving processes, SQLite and sockets
  while `File` and `Directory` stay leaves coupling, stands as a risk.
- **Project references with a content hash rejected as the consumption** *(Codex)*: overruled, since the cache resolves an unchanged
  package to its one build and no gate rebuilds it (§4.4).
- **A tracked `daoris.packages.json` of identities, hashes and receipts** *(Codex)*: the lock is that file, keyed by fingerprint.
- **Adapters pinned to older versions of one another** *(Codex)*: one tree holds one version of each, proved by its row.
- **A hand-kept SemVer per package, checked by a tool** *(Fable)*: the fingerprint is the version, and versions are never edited by hand.
- **Verdicts that expire with time, or move with the application's paths** *(Codex)*: §4.6.
- **An adapter that references the application** *(Codex)*: it reverses the dependency, and an application change would rebuild it.
- **New interfaces beyond the five** (Codex's `IActiveClock`, `ISecretVault`, Fable's `IInstanceChannel`): a delegate or a value record
  crosses instead (§3.4).
- **A package per plugin-declared agent** *(Codex)*: a plugin ships from its own repository; its door is data over the runtime's
  protocol door *(Fable)*.
- **Packaging Daoris's own transports** (Fable's `Infra.Agents.Transports`, "last or never"): never, since AGENTLYNTAI1 retires them.
- **A repository per package now** *(Fable)*: a quest per change across a boundary before the API has stood still; graduation is the
  path.
- **Real git made cheap in place of a fake** *(Fable)*: seeded repositories and libgit2 in process help the dependency tier and belong
  there, but still open a real repository in a unit test.
- **The names `Daoris.Drivers.*` and `Daoris.Infra.*`**: §2.5.

## 12. Risks

- **A fake that lies** *(Fable)*, **weak fakes** *(Codex)*: the contract twin, fakes kept beside the port they imitate, twin runs before
  a stage. `MemoryRepository` is the costly one, bounded by ring 2's declared questions.
- **The store's fake is SQLite** *(Codex)*: it proves the SQL and the gate, not the file; the file's guarantees stay on the real side,
  and a defect a twin run finds becomes a contract case.
- **Moving the store changes its behaviour** *(Codex)*: transaction boundaries, writer compatibility, crash recovery and the commit
  before the wake are contract cases before row b moves a line.
- **The ambient `Machine`** *(Fable)*: the ratchet, and wirings carry the ports wherever one exists.
- **Colliding with AGENTLYNTAI1** *(Fable)*: LAYERS1 moves `Agents/Contract/` and the providers' folders only in row j, door by door
  as that row moves them, and packages no transport it retires.
- **More projects, a slower cold build** *(Fable)*: the cache; measured at b, and the extraction stops if it grows.
- **A shared build cache on Windows**, where a running test host locks an assembly *(Fable)*: a fingerprint's folder is written once,
  then only read.
- **The published layout** *(both)*: `git2-*.dll`, `e_sqlite3.dll` and the launcher's programs arrive through packages, and
  `tools/desktop-publish/` lays them as before; the deployment rehearsal holds it.
- **Internal types made public in Ports** *(Fable)*: the Ports' API baseline makes each one a line read.
- **The GitDoor's linked sources taking a port** *(Fable)*: its source-set census.
- **An outside tool changing under a package** *(Codex)*: its pin is in the fingerprint, and the runtime's admission still checks the
  binary it finds.
- **A port change breaking an adapter** *(Codex)*: the Ports folder is in every implementer's fingerprint, so each one runs.
- **A cache or a row hiding a failure** *(Codex)*: only a complete green run writes a row, and a branch's row stands only where the
  store holds its pass.
- **The cycle between Machine and Git** *(Codex)*: §2.4.
- **Rows that conflict** *(this design)*: two branches that change two packages change two rows; a row both changed is settled by the
  suite's run, never by hand, as `daoris.lock` is written again rather than merged.
- **Proofs moved out of a merge's reach** *(this design)*: each extraction moves its tagged classes, row d the most; a class moved to a
  rehearsal project stays in `reached`'s selection there until its family has its fake (§5.2).
- **Isolation does not shorten the 48-minute suite** *(Codex)*: TESTDIET1 and TESTTIME1 cut it; LAYERS1 takes it off the merges.

## 13. Open

- **One SQLite engine under both stores** *(Fable)*: whether `ServiceStore.Sqlite` builds over `DesktopStore.Sqlite`'s engine (the open,
  the pragmas, the write lock, the step runner), each keeping its own schema, and so ends the twin `Platform/` copies the two trees each
  keep (`Moments`, `AtomicFile`, `GitForWindows`, `HomeIdentity`, `MachineLog`), is row h's to read; decision 1 takes Codex's two rows.
- **The parts kit and Ports.** A provider found as a part needs its part attribute, whose base `PartAttribute` is in `Daoris.Contracts`,
  while Ports is BCL-only; the `Agent.*` packages reference Contracts beside Ports, or the attribute's base moves. Row j settles which.
- **The ambient record's name.** `Machine` names both the bridge record and the Machine adapter; row a names the record apart if the two
  read as one. Settled in row a: the record is `PortSet`, read through `AmbientPorts`, and `Machine` stays the adapter's name.
- **Rows a to f and `reached`.** Whether a rehearsal class leaves `reached`'s selection per family in row f or all at once in row l is
  f's to read from the timing census.
- **Verdicts across machines.** A lock row written on another machine, or by a CI, stands as main's rows do; verdicts shared through the
  service as a permitted record are not in these rows (D285's design §15).
