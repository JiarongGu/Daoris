using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the evaluation runtime's test classes share (PARTWHOLE1): a scratch home, a stand-in port that counts each call, a held clock, the
/// plugin's deploy and build steps, and a look over the units a test sets, every read it may make counted. One class over three hand-split
/// parts until PARTWHOLE1 gave each part a class of its own: <see cref="NodeRuntimeTests"/>, <see cref="NodeRuntimeRunTests"/> and
/// <see cref="RunNodeWordsTests"/>, the last two over <see cref="NodeRunFixture"/>.
/// </summary>
public abstract class NodeRuntimeFixture : IDisposable
{
    protected const string Plugin = "acme.deploy";
    protected const string Unit = "quest:q7";

    /// <summary>The plugin's step as its manifest declares it: the contract a graph's node of its kind is performed under.</summary>
    protected static readonly NodeDeclaration Step = new(Plugin + "/deploy", Plugin + "/deploy", 1, NodeReach.Performed)
    {
        Retry = new NodeRetry(3, [30, 60]),
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: ["unavailable"]),
    };

    /// <summary>The plugin's build step (FABRIC-PERFORM1c): a pipeline's producer, its artifact a deploy's release.</summary>
    protected static readonly NodeDeclaration Build = Step with { Id = Plugin + "/build", Kind = Plugin + "/build" };

    protected readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-runtime-" + Guid.NewGuid().ToString("N")[..8]);

    protected readonly Port _port = new();

    protected DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    protected int _unitsRead;

    protected int _stepsRead;

    /// <summary>The git and gate reads a pass made (FABRIC-RUN1b, RUN1c).</summary>
    protected int _gitRead;

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>The plugins' work processes as the runtime sees them: each call counted.</summary>
    protected sealed class Port : IWorkRuntimePort
    {
        public int? Generation { get; set; } = 1;

        public List<WorkPerformCall> Sent { get; } = [];

        public List<WorkReconcileCall> Asked { get; } = [];

        public List<IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>>> Told { get; } = [];

        public Queue<WorkStepHeard> Heard { get; } = new();

        public int Taken { get; private set; }

        public Func<WorkPerformCall, Task>? OnPerform { get; set; }

        public Func<WorkReconcileCall, WorkReconcileAnswer> Answer { get; set; } = _ => new(ReconcileStates.Unknown);

        public async Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            Assert.Equal(Plugin, plugin);
            if (OnPerform is { } before) await before(call);
            var generation = Generation ?? throw new DriverException("not running");
            lock (Sent) Sent.Add(call);
            return generation;
        }

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct)
        {
            var generation = Generation ?? throw new DriverException("not running");
            lock (Asked) Asked.Add(call);
            return Task.FromResult((Answer(call), generation));
        }

        public int? GenerationOf(string plugin) => Generation;

        /// <summary>The cleanups asked (FABRIC-PERFORM1d), and what each is answered.</summary>
        public List<WorkCleanupCall> Cleaned { get; } = [];

        public Func<WorkCleanupCall, WorkCleanupAnswer> Cleanup { get; set; } = _ => new(CleanupDispositions.Removed);

        /// <summary>The plugins told owed a cleanup, each time (FABRIC-PERFORM1d).</summary>
        public List<IReadOnlySet<string>> Owed { get; } = [];

        public Task<(WorkCleanupAnswer Answer, int Generation)> CleanupAsync(string plugin, WorkCleanupCall call, CancellationToken ct)
        {
            Assert.Equal(Plugin, plugin);
            lock (Cleaned) Cleaned.Add(call);
            return Task.FromResult((Cleanup(call), Generation ?? throw new DriverException("not running")));
        }

        public void Owe(IReadOnlySet<string> plugins)
        {
            lock (Owed) Owed.Add(plugins);
        }

        public void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited)
        {
            lock (Told) Told.Add(awaited);
        }

        public IReadOnlyList<WorkStepHeard> TakeSteps()
        {
            lock (Heard)
            {
                Taken++;
                var taken = Heard.ToList();
                Heard.Clear();
                return taken;
            }
        }
    }

    protected NodeRecordFiles Files => new(_home, () => true);

    /// <summary>A new runtime over the same home: what a loop holds, and what a loop started again after a restart holds.</summary>
    protected NodeRuntime Runtime() => new(Files, _port, () => _now, () => true);

    protected static WorkUnit Quest => QuestOf(Unit);

    protected static WorkUnit QuestOf(string id) => new(id, WorkUnitKind.Quest, new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter),
        new WorkSubject("#" + id[(id.IndexOf(':') + 1)..], null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []);

    /// <summary>The engine's quests a pass reads: the one quest, unless a test names more.</summary>
    protected IReadOnlyList<WorkUnit> _units = [Quest];

    protected NodeRuntimeLook Look(string config, bool whole = true) => new(DriverConfig.Parse(config), whole)
    {
        Units = _ =>
        {
            Interlocked.Increment(ref _unitsRead);
            return Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { ["engine"] = "checkout" }));
        },
        Steps = () =>
        {
            Interlocked.Increment(ref _stepsRead);
            return [Step, Build];
        },
        // FABRIC-RUN1b, RUN1c: every git read and gate read the runtime may make, counted, so an install with no graph is seen to make none.
        Tips = (_, _, _) =>
        {
            Interlocked.Increment(ref _gitRead);
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        },
        Remotes = (_, _, _) =>
        {
            Interlocked.Increment(ref _gitRead);
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        },
        Holds = (_, _, _, _) =>
        {
            Interlocked.Increment(ref _gitRead);
            return Task.FromResult<bool?>(null);
        },
        Gates = (_, _, _) =>
        {
            Interlocked.Increment(ref _gitRead);
            return Task.FromResult<(OpinionGateState?, ReviewGateState?)>((null, null));
        },
    };
}
