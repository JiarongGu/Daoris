using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-RUN1 (the fabric events design §E2, §E3, §E7, §D6, §A3): the evaluation runtime in the lease's loop, over a stand-in port. An
/// install whose choices name no plugin's performed node reads, writes and starts nothing; a governed unit's performed node is started once
/// across wakes, its outcome heard and kept, its declared retry started after its persisted delay; a restart reconciles before any new start;
/// two wakes at once start nothing twice.
/// </summary>
public sealed class NodeRuntimeTests : IDisposable
{
    private const string Plugin = "acme.deploy";
    private const string Unit = "quest:q7";

    /// <summary>The plugin's step as its manifest declares it: the contract a graph's node of its kind is performed under.</summary>
    private static readonly NodeDeclaration Step = new(Plugin + "/deploy", Plugin + "/deploy", 1, NodeReach.Performed)
    {
        Retry = new NodeRetry(3, [30, 60]),
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: ["unavailable"]),
    };

    /// <summary>A repository's quests governed by a work cycle of one performed node, the plugin's deploy.</summary>
    private const string Governed = """
        { "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["deploy"], "end": "deploy",
            "nodes": [{ "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed" }],
            "wires": [] } } } }
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-runtime-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Port _port = new();

    private DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private int _unitsRead;

    private int _stepsRead;

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>The plugins' work processes as the runtime sees them: each call counted.</summary>
    private sealed class Port : IWorkRuntimePort
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

    private NodeRecordFiles Files => new(_home, () => true);

    /// <summary>A new runtime over the same home: what a loop holds, and what a loop started again after a restart holds.</summary>
    private NodeRuntime Runtime() => new(Files, _port, () => _now, () => true);

    private static WorkUnit Quest => new(Unit, WorkUnitKind.Quest, new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter),
        new WorkSubject("#q7", null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []);

    private NodeRuntimeLook Look(string config, bool whole = true) => new(DriverConfig.Parse(config), whole)
    {
        Units = _ =>
        {
            Interlocked.Increment(ref _unitsRead);
            return Task.FromResult(new NodeUnitsRead([Quest], new Dictionary<string, string> { ["engine"] = "checkout" }));
        },
        Steps = () =>
        {
            Interlocked.Increment(ref _stepsRead);
            return [Step];
        },
    };

    private NodeRecord Record() => Files.Read(Unit)!;

    private WorkStepHeard Completion(WorkOutcome outcome) =>
        new(Plugin, 1, new WorkStepFrame("deploy", outcome, _now, "e1", Invocation: Assert.Single(Record().Attempts, attempt => attempt.State != AttemptStates.Settled).Call.Invocation));

    [Theory]
    [InlineData("{}")]
    [InlineData("""
        { "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["qa"], "end": "qa",
            "nodes": [{ "id": "qa", "kind": "human", "version": 1, "reach": "pressed" }], "wires": [] } } } }
        """)]
    public async Task An_install_whose_choices_name_no_performed_node_reads_writes_and_starts_nothing(string config)
    {
        var runtime = Runtime();

        foreach (var whole in new[] { true, false, true })
        {
            var pass = await runtime.PassAsync(Look(config, whole), CancellationToken.None);
            Assert.False(pass.Armed);
            Assert.Empty(pass.Performed);
            Assert.Empty(pass.Evaluated);
        }

        Assert.False(NodeRuntime.Armed(DriverConfig.Parse(config)));
        Assert.Equal((0, 0), (_unitsRead, _stepsRead));
        Assert.Equal((0, 0, 0, 0), (_port.Told.Count, _port.Taken, _port.Sent.Count, _port.Asked.Count));
        Assert.False(Directory.Exists(NodeRecordFiles.FolderOf(_home)));
        Assert.Null(runtime.NextDue);
    }

    [Fact]
    public void A_quests_work_cycle_naming_a_plugins_performed_node_governs_it_under_its_plugins_contract()
    {
        var config = DriverConfig.Parse(Governed);
        Assert.True(NodeRuntime.Armed(config));

        var governed = Assert.Single(NodeRuntime.Govern(config, new NodeUnitsRead([Quest], new Dictionary<string, string>()), [Step]));
        Assert.Equal(Unit, governed.Unit.Id);
        var deploy = governed.Declared["deploy"];
        Assert.Equal((Step.Perform, Step.Retry), (deploy.Perform, deploy.Retry));
        Assert.Equal(["deploy"], governed.Dispatchable);

        // A run's identity is its unit's and its graph's: the same at every wake and every restart, another under another graph.
        Assert.Equal(governed.Run, NodeRuns.Of(Unit, governed.GraphDigest));
        Assert.NotEqual(governed.Run, NodeRuns.Of(Unit, "gph-other"));
        Assert.NotEqual(governed.Run, NodeRuns.Of("quest:q8", governed.GraphDigest));

        // Without the plugin's step, the node is declared as the graph has it and is never sent.
        Assert.Empty(Assert.Single(NodeRuntime.Govern(config, new NodeUnitsRead([Quest], new Dictionary<string, string>()), [])).Dispatchable);
    }

    [Fact]
    public async Task A_performed_node_is_started_once_across_two_wakes_and_its_unit_awaited()
    {
        var runtime = Runtime();

        var first = await runtime.PassAsync(Look(Governed), CancellationToken.None);
        var second = await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        var third = await runtime.PassAsync(Look(Governed), CancellationToken.None);

        Assert.True(first.Armed);
        Assert.Contains(first.Performed, done => done.What == PerformedWhat.Sent && done.Node == "deploy");
        Assert.DoesNotContain(second.Performed.Concat(third.Performed), done => done.What == PerformedWhat.Sent);
        Assert.Single(_port.Sent);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Sent, NodeStates.Active), (attempt.State, Record().Statuses["deploy"].State));
        // The look's deadline reads the units; the wake between judges the ones its answers concern, from what the last look read.
        Assert.Equal(2, _unitsRead);

        // The awaited set names the unit and its step, so the plugin's process is kept while the attempt waits.
        var told = _port.Told[^1];
        var watched = Assert.Single(told[Plugin]);
        Assert.Equal(["deploy"], watched.Awaiting);
        Assert.Equal(("engine", "checkout"), (watched.Repository, watched.Root));
    }

    [Fact]
    public async Task An_outcome_heard_is_kept_and_its_unit_no_longer_awaited()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);

        _port.Heard.Enqueue(Completion(new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence("acme.deploy/receipt@1:r1", null, null, "deployed"))));
        var pass = await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);

        Assert.Contains(pass.Performed, done => (done.What, done.Code) == (PerformedWhat.Applied, WorkStates.Reached));
        Assert.Equal(NodeStates.Reached, Record().Statuses["deploy"].State);
        Assert.Equal(AttemptStates.Settled, Assert.Single(Record().Attempts).State);
        Assert.Single(_port.Sent);
        Assert.Empty(_port.Told[^1].GetValueOrDefault(Plugin) ?? []);
    }

    [Fact]
    public async Task A_declared_retry_is_started_after_its_persisted_delay_and_not_before()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        _port.Heard.Enqueue(Completion(new WorkOutcome(WorkStates.Failed, "unavailable")));

        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        var failed = Assert.Single(Record().Attempts);
        Assert.Equal(_now.AddSeconds(30), failed.RetryAt);
        Assert.Equal(failed.RetryAt, runtime.NextDue);

        _now = _now.AddSeconds(29);
        await Runtime().PassAsync(Look(Governed), CancellationToken.None);
        Assert.Single(_port.Sent);

        // Past the delay, after a restart: a new attempt with a new key.
        _now = _now.AddSeconds(2);
        var pass = await Runtime().PassAsync(Look(Governed, whole: true), CancellationToken.None);
        Assert.Contains(pass.Performed, done => done.What == PerformedWhat.Retry);
        Assert.Equal(2, _port.Sent.Count);
        Assert.NotEqual(_port.Sent[0].IdempotencyKey, _port.Sent[1].IdempotencyKey);
        Assert.Equal(2, Record().Statuses["deploy"].Attempt);
    }

    [Fact]
    public async Task A_restart_reconciles_an_attempt_sent_by_the_loop_before_it_before_any_new_start()
    {
        await Runtime().PassAsync(Look(Governed), CancellationToken.None);
        var sent = Assert.Single(_port.Sent);

        // The next loop: the attempt is asked after with its key; running, it waits, and nothing new is sent.
        _port.Answer = _ => new(ReconcileStates.Running);
        var pass = await Runtime().PassAsync(Look(Governed), CancellationToken.None);

        var asked = Assert.Single(_port.Asked);
        Assert.Equal((sent.Invocation, sent.IdempotencyKey), (asked.Invocation, asked.IdempotencyKey));
        Assert.Contains(pass.Performed, done => done.What == PerformedWhat.Running);
        Assert.Single(_port.Sent);
        Assert.Single(Record().Attempts);
    }

    [Fact]
    public async Task Two_wakes_at_once_start_nothing_twice()
    {
        var runtime = Runtime();
        var sending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _port.OnPerform = async _ =>
        {
            sending.TrySetResult();
            await release.Task;
        };

        var look = runtime.PassAsync(Look(Governed), CancellationToken.None);
        await sending.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var wake = runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);

        // A verb beside the loop, or a loop that lost the lease: it holds no lease, so it reads no unit and writes nothing.
        var verb = await new NodeRuntime(new NodeRecordFiles(_home, () => false), _port, () => _now, () => false).PassAsync(Look(Governed), CancellationToken.None);
        Assert.Empty(verb.Performed);
        release.SetResult();
        await Task.WhenAll(look, wake).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Single(_port.Sent);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Sent, NodeStates.Active), (attempt.State, Record().Statuses["deploy"].State));
        Assert.Equal(1, _unitsRead);
    }

    [Fact]
    public async Task A_node_whose_plugin_is_not_up_is_awaited_and_not_sent_until_it_is()
    {
        _port.Generation = null;
        var runtime = Runtime();

        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        Assert.Empty(_port.Sent);
        Assert.Empty(Record().Attempts);
        Assert.Equal(["deploy"], Assert.Single(_port.Told[^1][Plugin]).Awaiting);

        _port.Generation = 1;
        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        Assert.Single(_port.Sent);
    }
}
