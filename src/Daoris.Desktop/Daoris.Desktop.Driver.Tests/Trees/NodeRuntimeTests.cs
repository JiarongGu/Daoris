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

    /// <summary>The same cycle edited (FABRIC-RUN1e): its node given a timeout, so the graph's digest, and with it the run, is another.</summary>
    private const string Edited = """
        { "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["deploy"], "end": "deploy",
            "nodes": [{ "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed", "timeoutSeconds": 600 }],
            "wires": [] } } } }
        """;

    /// <summary>Another repository's quests governed (FABRIC-RUN1e): the runtime stays armed, and the engine's quests are governed no longer.</summary>
    private const string OtherGoverned = """
        { "workCycles": { "other": { "quest": {
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

    private static WorkUnit Quest => QuestOf(Unit);

    private static WorkUnit QuestOf(string id) => new(id, WorkUnitKind.Quest, new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter),
        new WorkSubject("#" + id[(id.IndexOf(':') + 1)..], null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []);

    /// <summary>The engine's quests a pass reads: the one quest, unless a test names more.</summary>
    private IReadOnlyList<WorkUnit> _units = [Quest];

    private NodeRuntimeLook Look(string config, bool whole = true) => new(DriverConfig.Parse(config), whole)
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
    };

    /// <summary>The plugin's build step (FABRIC-PERFORM1c): a pipeline's producer, its artifact a deploy's release.</summary>
    private static readonly NodeDeclaration Build = Step with { Id = Plugin + "/build", Kind = Plugin + "/build" };

    /// <summary>
    /// A repository's quests governed by a build whose artifact the deploy takes (FABRIC-PERFORM1c): a <c>data</c> wire, read from the build's
    /// accepted output, its evidence the plugin's receipt.
    /// </summary>
    private const string Pipeline = """
        { "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["build"], "end": "deploy",
            "nodes": [
              { "id": "build", "kind": "acme.deploy/build", "version": 1, "reach": "performed", "evidence": "acme.deploy/receipt@1",
                "outputs": { "artifact": "acme.deploy/artifact@1" } },
              { "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed", "inputs": { "release": "acme.deploy/artifact@1" } } ],
            "wires": [{ "id": "w1", "kind": "data", "from": "build.artifact", "to": "deploy.release" }] } } } }
        """;

    /// <summary>The build's completion for its attempt still waiting, with its receipt as evidence and its artifact as its output.</summary>
    private WorkStepHeard Built(string evidence, string artifact) =>
        new(Plugin, 1, new WorkStepFrame("build", new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence(evidence, null, null, "built"),
            Outputs: new Dictionary<string, WorkOutput> { ["artifact"] = new(artifact, null) }), _now, "e-" + artifact,
            Invocation: Assert.Single(Record().Attempts, attempt => attempt.Node == "build" && attempt.State != AttemptStates.Settled).Call.Invocation));

    [Fact]
    public async Task A_reach_on_evidence_not_authorised_starts_nothing_downstream_and_an_authorised_ones_output_is_read_by_the_next_node()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Pipeline), CancellationToken.None);
        Assert.Equal([Plugin + "/build"], _port.Sent.Select(call => call.Step));

        // A receipt of another schema than the build declares: not its evidence, so the build is held and the deploy never starts.
        _port.Heard.Enqueue(Built("acme.deploy/log@1:l1", "acme.deploy/artifact@1:a1"));
        var held = await runtime.PassAsync(Look(Pipeline, whole: false), CancellationToken.None);
        await runtime.PassAsync(Look(Pipeline), CancellationToken.None);

        Assert.Contains(held.Performed, done => (done.Node, done.What, done.Code) == ("build", PerformedWhat.Held, NodeRefusal.NoEvidence));
        Assert.Equal(NodeStates.Active, Record().Statuses["build"].State);
        Assert.Single(_port.Sent);

        // The build's own receipt: reached, and its accepted artifact is the deploy's release.
        _port.Heard.Enqueue(Built("acme.deploy/receipt@1:b1", "acme.deploy/artifact@1:a1"));
        await runtime.PassAsync(Look(Pipeline, whole: false), CancellationToken.None);

        Assert.Equal(NodeStates.Reached, Record().Statuses["build"].State);
        var deploy = Assert.Single(_port.Sent, call => call.Step == Plugin + "/deploy");
        Assert.Equal(["acme.deploy/artifact@1:a1"], deploy.Inputs);
    }

    [Fact]
    public async Task An_output_accepted_on_a_revision_the_work_has_moved_past_is_never_read()
    {
        // A revision the test moves, as a new tip moves a branch: the runtime reads every unit's revision alike.
        _units = [Quest with { Subject = new WorkSubject("#q7", "r1") }];
        var runtime = Runtime();
        await runtime.PassAsync(Look(Pipeline), CancellationToken.None);

        // The work moves to r2 before the build's completion on r1 is heard: its outcome is a stale attempt's, and the build starts again.
        _units = [Quest with { Subject = new WorkSubject("#q7", "r2") }];
        _port.Heard.Enqueue(Built("acme.deploy/receipt@1:b1", "acme.deploy/artifact@1:a1"));
        var moved = await runtime.PassAsync(Look(Pipeline), CancellationToken.None);

        Assert.Contains(moved.Performed, done => (done.Node, done.What, done.Code) == ("build", PerformedWhat.Stale, WorkRefusals.Stale));
        Assert.DoesNotContain(_port.Sent, call => call.Step == Plugin + "/deploy");
        Assert.Equal(["r1", "r2"], _port.Sent.Select(call => call.SubjectRevision));

        // The build on r2 reaches: the deploy reads its artifact, never r1's.
        _port.Heard.Enqueue(Built("acme.deploy/receipt@1:b2", "acme.deploy/artifact@1:a2"));
        await runtime.PassAsync(Look(Pipeline, whole: false), CancellationToken.None);

        var deploy = Assert.Single(_port.Sent, call => call.Step == Plugin + "/deploy");
        Assert.Equal(["acme.deploy/artifact@1:a2"], deploy.Inputs);
        Assert.Equal("r2", deploy.SubjectRevision);
    }

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

    /// <summary>FABRIC-RUN1e, a transition: an install armed at a wake reads its units then, and starts what its graph proposes.</summary>
    [Fact]
    public async Task An_install_armed_at_a_wake_reads_its_units_then_and_starts()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look("{}", whole: false), CancellationToken.None);
        Assert.Equal(0, _unitsRead);

        var pass = await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);

        Assert.True(pass.Armed);
        Assert.Equal(1, _unitsRead);
        Assert.Single(_port.Sent);
    }

    /// <summary>
    /// FABRIC-RUN1e, finding 1: a wake whose choices no longer govern a unit (its repository's graph removed while another's keeps the
    /// runtime armed) judges with the choices now, read afresh before any attempt: the unit's retry is not made and nothing is sent to it.
    /// </summary>
    [Fact]
    public async Task A_config_wake_after_a_units_graph_is_removed_retries_and_sends_nothing_on_it()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        _port.Heard.Enqueue(Completion(new WorkOutcome(WorkStates.Failed, "unavailable")));
        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        Assert.NotNull(Assert.Single(Record().Attempts).RetryAt);

        _now = _now.AddSeconds(31);
        var pass = await runtime.PassAsync(Look(OtherGoverned, whole: false), CancellationToken.None);

        Assert.True(pass.Armed);
        Assert.Equal(2, _unitsRead);
        Assert.DoesNotContain(pass.Performed, done => done.Unit == Unit);
        Assert.Single(_port.Sent);
        Assert.Equal(NodeStates.Failed, Record().Statuses["deploy"].State);
        Assert.Empty(_port.Told[^1]);
    }

    /// <summary>
    /// FABRIC-RUN1e, finding 2: disarming clears what the runtime held, though nothing was awaited (a failed node waiting for its retry): no
    /// timer is left to wake the loop, and a wake that arms it again reads the units afresh rather than judging on the units cached before.
    /// </summary>
    [Fact]
    public async Task Disarming_clears_a_pending_retrys_timer_and_the_units_held_though_nothing_was_awaited()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        _port.Heard.Enqueue(Completion(new WorkOutcome(WorkStates.Failed, "unavailable")));
        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        Assert.NotNull(runtime.NextDue);
        Assert.Empty(_port.Told[^1]);

        var disarmed = await runtime.PassAsync(Look("{}", whole: false), CancellationToken.None);
        Assert.False(disarmed.Armed);
        Assert.Null(runtime.NextDue);

        var reads = _unitsRead;
        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        Assert.Equal(reads + 1, _unitsRead);
    }

    /// <summary>
    /// FABRIC-RUN1e, finding 2: the awaited set is withdrawn before the plan's supervision once no graph arms the runtime, so the look that
    /// reads the last graph's removal starts no plugin for it; choices still armed withdraw nothing.
    /// </summary>
    [Fact]
    public async Task The_awaited_set_is_withdrawn_before_supervision_once_no_graph_arms_the_runtime()
    {
        _port.Generation = null;
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        Assert.NotEmpty(_port.Told[^1]);
        var told = _port.Told.Count;

        Assert.False(await runtime.WithdrawAsync(DriverConfig.Parse(Governed), CancellationToken.None));
        Assert.Equal(told, _port.Told.Count);

        Assert.True(await runtime.WithdrawAsync(DriverConfig.Parse("{}"), CancellationToken.None));
        Assert.Equal(told + 1, _port.Told.Count);
        Assert.Empty(_port.Told[^1]);
        Assert.Null(runtime.NextDue);

        // The pass after it has nothing more to withdraw, and tells nothing again.
        await runtime.PassAsync(Look("{}"), CancellationToken.None);
        Assert.Equal(told + 1, _port.Told.Count);
    }

    /// <summary>
    /// FABRIC-RUN1e, finding 3: a failure stays across graph edits back and forth (each a new run, its bindings another graph's), so a Setup
    /// save that changes the graph never restarts failed work; the person's new attempt does, under the graph that governs now.
    /// </summary>
    [Fact]
    public async Task A_failure_stays_across_graph_edits_back_and_forth_until_the_person_restarts_it()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        _port.Heard.Enqueue(Completion(new WorkOutcome(WorkStates.Failed, "broken")));
        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);

        foreach (var config in new[] { Edited, Governed, Edited, Governed, Edited })
        {
            await runtime.PassAsync(Look(config), CancellationToken.None);
        }

        Assert.Single(_port.Sent);
        var status = Record().Statuses["deploy"];
        Assert.Equal((NodeStates.Failed, "broken", 1), (status.State, status.Code, status.Attempt));

        var deploy = Assert.Single(NodeRuntime.Govern(DriverConfig.Parse(Edited), new NodeUnitsRead([Quest], new Dictionary<string, string>()), [Step])).Declared["deploy"];
        var (refused, _) = Files.Edit<bool>(Unit, record =>
            (record with { Statuses = new Dictionary<string, NodeStatus>(record.Statuses) { ["deploy"] = NodeLifecycle.Apply(deploy, status, new NodeMove(NodeStates.Ready, NodeCause.Person)).Status } }, true));
        Assert.Null(refused);
        await runtime.PassAsync(Look(Edited), CancellationToken.None);

        Assert.Equal(2, _port.Sent.Count);
        Assert.Equal(NodeRuns.Of(Unit, NodeGraphs.Digest(NodeGraphHomes.Work(DriverConfig.Parse(Edited), "engine", "default", WorkUnitKind.Quest).Graph!)),
            Record().Statuses["deploy"].Scope!.Run);
    }

    /// <summary>FABRIC-RUN1e, finding 4: the units past a pass's first 32 are judged at the next, not starved behind the same prefix.</summary>
    [Fact]
    public async Task Units_past_a_passs_first_32_are_judged_at_the_next()
    {
        _port.Generation = null;
        _units = [.. Enumerable.Range(0, NodeRuntime.UnitsPerPass + 2).Select(index => QuestOf($"quest:q{index:00}"))];
        var runtime = Runtime();

        var first = await runtime.PassAsync(Look(Governed), CancellationToken.None);
        var second = await runtime.PassAsync(Look(Governed), CancellationToken.None);

        Assert.Equal(NodeRuntime.UnitsPerPass, first.Evaluated.Count);
        Assert.Equal(NodeRuntime.UnitsPerPass, second.Evaluated.Count);
        Assert.Equal(_units.Select(unit => unit.Id).Order(StringComparer.Ordinal), first.Evaluated.Concat(second.Evaluated).Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// FABRIC-RUN1e, finding 5: reconciliation and the starts share the pass's four sends. A restart finds four attempts to ask after, each
    /// redelivered, and two nodes ready: the pass calls about four attempts, and the two starts wait for the next pass.
    /// </summary>
    [Fact]
    public async Task Reconciliation_and_the_starts_share_the_passs_four_sends()
    {
        _units = [.. Enumerable.Range(0, NodeRuntime.SendsPerPass + 2).Select(index => QuestOf($"quest:q{index}"))];
        await Runtime().PassAsync(Look(Governed), CancellationToken.None);
        Assert.Equal(NodeRuntime.SendsPerPass, _port.Sent.Count);

        _port.Answer = _ => new(ReconcileStates.NotStarted);
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);

        Assert.Equal(NodeRuntime.SendsPerPass, _port.Asked.Count);
        Assert.Equal(NodeRuntime.SendsPerPass * 2, _port.Sent.Count);

        // The next pass makes the two starts left over.
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        Assert.Equal(NodeRuntime.SendsPerPass * 2 + 2, _port.Sent.Count);
    }

    /// <summary>
    /// FABRIC-RUN1e, finding 6: the awaited set is published only while the loop holds the lease, asked after the pass's awaits: a loop that
    /// lost it while its send was out tells nothing.
    /// </summary>
    [Fact]
    public async Task A_lease_lost_while_a_send_is_out_publishes_no_awaited_set()
    {
        var held = true;
        _port.OnPerform = _ =>
        {
            held = false;
            return Task.CompletedTask;
        };
        var runtime = new NodeRuntime(Files, _port, () => _now, () => held);

        await runtime.PassAsync(Look(Governed), CancellationToken.None);

        Assert.Single(_port.Sent);
        Assert.Empty(_port.Told);
    }
}
