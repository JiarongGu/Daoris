using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A step done outside Daoris is done by its holder, and the run goes on (DONEBYHOLDER1, D304 point 5): <i>Done outside Daoris</i> with its
/// link settles a plugin's step under way or failed, a proof-carrying one included; skip, stop and done each stand the attempt in flight
/// down, written as <c>settled</c> with its outcome code, its plugin told, its later outcome a replay; and the skip a proof-carrying step
/// refuses names the done-outside press.
/// </summary>
public sealed class DoneByHolderTests : IDisposable
{
    private const string Unit = "unit-r";
    private const string Plugin = "acme.deploy";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private const string Link = "https://deploy.example/releases/42";

    private static readonly NodePerform Performs = new(Idempotent: true, Reconcile: true, Transient: []);

    // The release carries its proof as evidence and is never passed over; the step after it waits on it.
    private static readonly NodeGraph Read = NodeGraphs.Read("""
        { "schemaVersion": 2, "entries": ["release"], "end": "after",
          "nodes": [
            { "id": "release", "kind": "acme.deploy/release", "version": 1, "reach": "performed" },
            { "id": "after", "kind": "acme.deploy/after", "version": 1, "reach": "performed" } ],
          "wires": [{ "id": "w1", "kind": "after", "from": "release", "to": "after" }] }
        """).Graph!;

    private static readonly NodeGraph Graph = Read with
    {
        Nodes = [Read.Nodes[0] with { Evidence = "acme.deploy/release@1", Skippable = false, Perform = Performs }, Read.Nodes[1] with { Perform = Performs }],
    };

    private static readonly NodeDeclaration Release = Graph.Nodes[0];

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-outside-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Port _port = new();

    private readonly DateTimeOffset _now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class Port : IWorkPerformPort
    {
        public List<WorkPerformCall> Sent { get; } = [];

        public List<WorkStandDownCall> StoodDown { get; } = [];

        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            Sent.Add(call);
            return Task.FromResult(1);
        }

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            Task.FromResult((new WorkReconcileAnswer(ReconcileStates.Running), 1));

        public int? GenerationOf(string plugin) => 1;

        public void StandDown(string plugin, WorkStandDownCall call) => StoodDown.Add(call);
    }

    private NodeRecordFiles Files => new(_home, () => true);

    private WorkPerformer Performer() => new(Files, _port, () => _now);

    private NodeDeclaration? Declared(string unit, string node) => Graph.Nodes.FirstOrDefault(each => each.Id == node);

    private NodeRecord Record() => Files.Read(Unit)!;

    private NodeStatus Status(string node) => WorkPerformer.StatusOf(Record(), node);

    private WatchedUnit? Watched(string unit) => new WatchedUnit("engine", "default", "checkout", "", Tip, null, null, []);

    private WireEvaluated Evaluate(WorkPerformer performer)
    {
        var record = Files.Read(Unit) ?? new NodeRecord(Unit);
        var before = Graph.Nodes.ToDictionary(node => node.Id, node => record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(Unit, node.Id), StringComparer.Ordinal);
        var run = new WireRun("graph-1", "r1") { Revisions = new Dictionary<string, string?> { [Unit] = Tip }, Fired = record.Fired.ToHashSet(StringComparer.Ordinal) };
        var evaluated = WireEvaluation.Evaluate(Graph.Nodes, Graph.Wires, before, run);
        performer.Keep(before, evaluated);
        return evaluated;
    }

    private async Task<WorkPerformCall> StartReleaseAsync(WorkPerformer performer)
    {
        var start = Assert.Single(Evaluate(performer).Starts);
        Assert.Equal("release", start.Node);
        await performer.StartAsync(Release, start, CancellationToken.None);
        return _port.Sent[^1];
    }

    private WorkStepHeard Heard(string invocation, WorkOutcome outcome) => new(Plugin, 1, new WorkStepFrame("release", outcome, _now, "e-" + invocation, Invocation: invocation));

    [Fact]
    public async Task A_release_failed_no_release_after_one_made_by_hand_is_settled_by_its_holders_word_with_a_link_and_the_next_step_starts()
    {
        var performer = Performer();
        var call = await StartReleaseAsync(performer);
        performer.Hear([Heard(call.Invocation, new WorkOutcome(WorkStates.Failed, Code: "no-release"))], Declared, Watched);
        Assert.Equal(NodeStates.Failed, Status("release").State);

        // The link is the proof: none, and nothing is recorded.
        Assert.Equal(WorkPerformer.NoLink, performer.Word(Unit, "release", 1, HeldResolutions.DoneOutside).Code);
        Assert.Equal(PerformedWhat.Worded, performer.Word(Unit, "release", 1, HeldResolutions.DoneOutside, link: Link, note: "made by hand").What);
        Assert.Equal(HeldResolutions.DoneOutside, Assert.Single(performer.Resolve(Declared)).Code);

        var release = Status("release");
        Assert.Equal((NodeStates.Reached, NodeSource.Person, "made by hand"), (release.State, release.By, release.Words));
        Assert.Equal((NodeSource.Person, Link), (release.Acceptance!.Evidence!.Schema, release.Acceptance.Evidence.Ref));
        Assert.Equal(WorkStates.Failed, Assert.Single(Record().Attempts).Outcome);

        var next = Assert.Single(Evaluate(performer).Starts);
        Assert.Equal("after", next.Node);
    }

    [Fact]
    public async Task Done_outside_on_a_step_under_way_stands_its_attempt_down_tells_its_plugin_and_its_later_outcome_is_a_replay()
    {
        var performer = Performer();
        var call = await StartReleaseAsync(performer);
        Assert.Equal(NodeStates.Active, Status("release").State);

        Assert.Equal(PerformedWhat.Worded, performer.Word(Unit, "release", 1, HeldResolutions.DoneOutside, link: Link).What);
        performer.Resolve(Declared);

        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Settled, AttemptOutcomes.StoodDown, HeldResolutions.DoneOutside), (attempt.State, attempt.Outcome, attempt.Code));
        Assert.Equal((NodeStates.Reached, (int?)null), (Status("release").State, Status("release").Unreconciled));
        Assert.Equal((call.Invocation, HeldResolutions.DoneOutside), (Assert.Single(_port.StoodDown).Invocation, _port.StoodDown[0].Why));

        var late = performer.Hear([Heard(call.Invocation, new WorkOutcome(WorkStates.Failed, Code: "rejected"))], Declared, Watched);
        Assert.Equal(PerformedWhat.NotTaken, Assert.Single(late).What);
        Assert.Equal(NodeStates.Reached, Status("release").State);
        Assert.Equal(AttemptOutcomes.StoodDown, Assert.Single(Record().Attempts).Outcome);
    }

    [Theory]
    [InlineData(NodeWordActs.Skip)]
    [InlineData(NodeWordActs.Stop)]
    public async Task A_skip_or_a_stop_with_an_attempt_in_flight_stands_it_down_so_nothing_holds_the_run(string act)
    {
        var graph = Graph with { Nodes = [Release with { Skippable = true }, Graph.Nodes[1]] };
        var performer = Performer();
        var call = await StartReleaseAsync(performer);
        Files.Write(Record() with { Pending = [new NodeWord("release", act, "not needed this run", _now)] });

        var (next, applied) = RunNodeWords.Apply(Record(), graph.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal), new NodeScope("graph-1", "r1"), _now);
        Assert.Null(Assert.Single(applied).Refused);
        Files.Write(next!);
        performer.StandDown(Unit, ["release"]);

        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Settled, AttemptOutcomes.StoodDown, act), (attempt.State, attempt.Outcome, attempt.Code));
        Assert.Null(Status("release").Unreconciled);
        Assert.False(NodeRunKeeping.Waits(graph, Record()));
        Assert.Equal(call.Invocation, Assert.Single(_port.StoodDown).Invocation);
    }

    [Fact]
    public void The_skip_a_proof_carrying_step_refuses_names_done_outside_and_says_ending_the_work_leaves_its_quest_to_the_person()
    {
        var said = RunNodeWords.Said(new NodeWorded("r1", "release", NodeWordActs.Skip, NodeRefusal.NotSkippable));

        Assert.Contains("done outside Daoris", said, StringComparison.Ordinal);
        Assert.Contains(HeldResolutions.DoneOutside, said, StringComparison.Ordinal);
        Assert.Contains("leaves its quest to close by your word", said, StringComparison.Ordinal);
        Assert.DoesNotContain("stop it instead", said, StringComparison.Ordinal);
    }
}
