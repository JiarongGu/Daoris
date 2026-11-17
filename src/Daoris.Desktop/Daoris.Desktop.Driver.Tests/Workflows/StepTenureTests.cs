using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A holder record per attempt: <c>{role, holder, epoch, since, why}</c> in a file of its own that no older build reads, never in
/// the node record; a holder change starts a new epoch without restarting the step; and a word from an ended epoch is refused, saying who
/// holds the step now and since when.
/// </summary>
public sealed class StepTenureTests : IDisposable
{
    private const string Unit = "unit-r";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private const string Link = "https://deploy.example/releases/42";

    private static readonly NodeGraph Read = NodeGraphs.Read("""
        { "schemaVersion": 2, "entries": ["release"], "end": "release",
          "nodes": [{ "id": "release", "kind": "acme.deploy/release", "version": 1, "reach": "performed" }], "wires": [] }
        """).Graph!;

    private static readonly NodeGraph Graph = Read with
    {
        Nodes = [Read.Nodes[0] with { Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []) }],
    };

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-tenure-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Port _port = new();

    private readonly DateTimeOffset _now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class Port : IWorkPerformPort
    {
        public List<WorkPerformCall> Sent { get; } = [];

        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            Sent.Add(call);
            return Task.FromResult(1);
        }

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            Task.FromResult((new WorkReconcileAnswer(ReconcileStates.Running), 1));

        public int? GenerationOf(string plugin) => 1;

        public void StandDown(string plugin, WorkStandDownCall call)
        {
        }
    }

    private NodeRecordFiles Files => new(_home, () => true);

    private StepTenureFiles Tenures => new(_home, () => true);

    private WorkPerformer Performer() => new(Files, _port, () => _now);

    private async Task StartAsync(WorkPerformer performer)
    {
        var record = Files.Read(Unit) ?? new NodeRecord(Unit);
        var before = Graph.Nodes.ToDictionary(node => node.Id, node => record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(Unit, node.Id), StringComparer.Ordinal);
        var run = new WireRun("graph-1", "r1") { Revisions = new Dictionary<string, string?> { [Unit] = Tip }, Fired = record.Fired.ToHashSet(StringComparer.Ordinal) };
        var evaluated = WireEvaluation.Evaluate(Graph.Nodes, Graph.Wires, before, run);
        performer.Keep(before, evaluated);
        await performer.StartAsync(Graph.Nodes[0], Assert.Single(evaluated.Starts), CancellationToken.None);
    }

    [Fact]
    public async Task A_word_from_the_previous_holder_after_a_hand_over_is_refused_and_the_step_goes_on_unrestarted()
    {
        var performer = Performer();
        await StartAsync(performer);
        var cast = Tenures.Hold(Unit, "release", 1, "release-manager", "agent:alpha", TenureWhy.Cast, _now);
        var status = () => WorkPerformer.StatusOf(Files.Read(Unit)!, "release") is var read ? (read.State, read.Attempt, read.Tries, read.Since, read.Binding?.Id) : default;
        var before = status();

        var handed = Tenures.HandOver(Unit, "release", 1, "agent:beta", TenureWhy.HandOver, _now.AddMinutes(5));
        Assert.Equal((1, 2, "release-manager", "agent:beta"), (cast!.Epoch, handed!.Epoch, handed.Role, handed.Holder));

        // The step is not restarted: its status, its attempt and what it sent are unchanged.
        Assert.Equal(before, status());
        Assert.Single(Files.Read(Unit)!.Attempts);
        Assert.Single(_port.Sent);

        var refused = performer.Word(Unit, "release", 1, HeldResolutions.DoneOutside, link: Link, epoch: 1);
        Assert.Equal((PerformedWhat.Refused, StepTenureFiles.EndedEpoch), (refused.What, refused.Code));
        Assert.Contains("agent:beta", refused.Detail, StringComparison.Ordinal);
        Assert.Null(Assert.Single(Files.Read(Unit)!.Attempts).Resolution);

        Assert.Equal(SettleDoor.NotHolder, performer.Word(Unit, "release", 1, HeldResolutions.DoneOutside, link: Link, epoch: 2).Code);
        Assert.Equal(PerformedWhat.Worded, performer.Word(Unit, "release", 1, HeldResolutions.DoneOutside, link: Link, epoch: 2, settle: new SettleBy("agent:beta", 2)).What);
    }

    [Fact]
    public void The_same_holder_again_keeps_its_epoch_and_each_attempt_has_its_own_tenures()
    {
        var first = Tenures.Hold(Unit, "release", 1, "release-manager", "person", TenureWhy.Cast, _now)!;
        Assert.Equal(first, Tenures.Hold(Unit, "release", 1, "release-manager", "person", TenureWhy.Cast, _now.AddMinutes(1)));
        Assert.Equal(1, Tenures.Hold(Unit, "release", 2, "release-manager", "person", TenureWhy.Cast, _now.AddMinutes(2))!.Epoch);
        Assert.Equal(2, Tenures.HandOver(Unit, "release", 1, "agent:beta", TenureWhy.TakeBack, _now.AddMinutes(3))!.Epoch);

        var kept = Tenures.Of(Unit, "release", 1);
        Assert.Equal([(1, "person", TenureWhy.Cast, _now), (2, "agent:beta", TenureWhy.TakeBack, _now.AddMinutes(3))],
            kept.Select(each => (each.Epoch, each.Holder, each.Why, each.Since)));
        Assert.Equal("agent:beta", Tenures.Current(Unit, "release", 1)!.Holder);
        Assert.Null(Tenures.HandOver(Unit, "release", 3, "agent:beta", TenureWhy.HandOver, _now));
    }

    [Fact]
    public async Task Tenures_sit_where_no_older_build_reads_and_a_word_naming_no_epoch_is_judged_against_the_holder_now()
    {
        var performer = Performer();
        await StartAsync(performer);
        Tenures.Hold(Unit, "release", 1, "release-manager", "agent:alpha", TenureWhy.Cast, _now);
        Tenures.HandOver(Unit, "release", 1, "agent:beta", TenureWhy.HandOver, _now);

        var path = StepTenureFiles.PathOf(_home, Unit);
        Assert.Equal(StepTenureFiles.FolderOf(_home), Path.GetDirectoryName(path));
        Assert.False(path.StartsWith(NodeRecordFiles.FolderOf(_home) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(path));
        Assert.Empty(Files.All().Unreadable);
        Assert.DoesNotContain("tenure", File.ReadAllText(NodeRecordFiles.PathOf(_home, Unit)), StringComparison.OrdinalIgnoreCase);

        Assert.Equal(SettleDoor.NotHolder, performer.Word(Unit, "release", 1, HeldResolutions.DoneOutside, link: Link).Code);
        Assert.Equal(PerformedWhat.Worded, performer.Word(Unit, "release", 1, HeldResolutions.DoneOutside, link: Link, settle: new SettleBy("agent:beta")).What);
    }
}
