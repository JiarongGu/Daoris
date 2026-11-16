using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An act is keyed by its effect (EFFECTKEY1, D309): a digest of its run, its node, its slot and its target subject, the same for every attempt
/// on that target and a new one for a new target; written on the record before the send; carried on the wire beside the attempt's own key;
/// and read as the attempt's own key on an attempt written before it, so an attempt in flight keeps the key it was sent with.
/// </summary>
public sealed class EffectKeyTests : IDisposable
{
    private const string Unit = "unit-r";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private static readonly NodeGraph Read = NodeGraphs.Read("""
        { "schemaVersion": 2, "entries": ["release"], "end": "release",
          "nodes": [{ "id": "release", "kind": "acme.deploy/release", "version": 1, "reach": "performed" }], "wires": [] }
        """).Graph!;

    private static readonly NodeGraph Graph = Read with
    {
        Nodes = [Read.Nodes[0] with { Retry = new NodeRetry(3, [30]), Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: ["unavailable"]) }],
    };

    private static readonly NodeDeclaration Release = Graph.Nodes[0];

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-effect-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Port _port;

    private DateTimeOffset _now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    public EffectKeyTests() => _port = new Port(this);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>A plugin's port that reads, at each send, the effect key the record already holds for the attempt sent.</summary>
    private sealed class Port(EffectKeyTests test) : IWorkPerformPort
    {
        public List<WorkPerformCall> Sent { get; } = [];

        public List<string?> JournaledAtSend { get; } = [];

        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            JournaledAtSend.Add(test.Files.Read(Unit)!.Invoked(call.Invocation)?.Call.EffectKey);
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

    private WorkPerformer Performer() => new(Files, _port, () => _now);

    private NodeDeclaration? Declared(string unit, string node) => Graph.Nodes.FirstOrDefault(each => each.Id == node);

    private WatchedUnit? Watched(string unit) => new("engine", "default", "checkout", "", Tip, null, null, []);

    private async Task<WorkPerformCall> StartAsync(WorkPerformer performer, string tip = Tip)
    {
        var record = Files.Read(Unit) ?? new NodeRecord(Unit);
        var before = Graph.Nodes.ToDictionary(node => node.Id, node => record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(Unit, node.Id), StringComparer.Ordinal);
        var run = new WireRun("graph-1", "r1") { Revisions = new Dictionary<string, string?> { [Unit] = tip }, Fired = record.Fired.ToHashSet(StringComparer.Ordinal) };
        var evaluated = WireEvaluation.Evaluate(Graph.Nodes, Graph.Wires, before, run);
        performer.Keep(before, evaluated);
        await performer.StartAsync(Release, Assert.Single(evaluated.Starts), CancellationToken.None);
        return _port.Sent[^1];
    }

    [Fact]
    public async Task A_retry_on_the_same_target_carries_the_same_effect_key_beside_a_new_attempt_key_each_written_before_its_send()
    {
        var performer = Performer();
        var first = await StartAsync(performer);
        performer.Hear([new WorkStepHeard("acme.deploy", 1, new WorkStepFrame("release", new WorkOutcome(WorkStates.Failed, Code: "unavailable"), _now, "e-1", Invocation: first.Invocation))], Declared, Watched);
        _now = _now.AddSeconds(31);
        Assert.Equal(PerformedWhat.Retry, Assert.Single(Performer().Due(Declared)).What);
        var second = await StartAsync(Performer());

        Assert.NotEqual(first.IdempotencyKey, second.IdempotencyKey);
        Assert.Equal(EffectKeys.Of(Unit, "release", "acme.deploy/release", $"{Unit}@{Tip}"), first.EffectKey);
        Assert.Equal(first.EffectKey, second.EffectKey);
        Assert.Equal([first.EffectKey, second.EffectKey], _port.JournaledAtSend);
    }

    [Fact]
    public void An_effect_key_names_its_run_node_slot_and_target_and_no_attempt()
    {
        var key = EffectKeys.Of("run:7", "release", "acme.deploy/release", "unit-r@" + Tip);
        Assert.Equal(key, EffectKeys.Of("run:7", "release", "acme.deploy/release", "unit-r@" + Tip));
        Assert.StartsWith(EffectKeys.Prefix, key, StringComparison.Ordinal);
        Assert.NotEqual(key, EffectKeys.Of("run:8", "release", "acme.deploy/release", "unit-r@" + Tip));
        Assert.NotEqual(key, EffectKeys.Of("run:7", "after", "acme.deploy/release", "unit-r@" + Tip));
        Assert.NotEqual(key, EffectKeys.Of("run:7", "release", "acme.deploy/watch", "unit-r@" + Tip));
        Assert.NotEqual(key, EffectKeys.Of("run:7", "release", "acme.deploy/release", "unit-r@" + new string('f', 40)));
        // Joined by a separator no part holds, so two splits of the same text are two keys.
        Assert.NotEqual(EffectKeys.Of("a", "bc", "d", "e"), EffectKeys.Of("ab", "c", "d", "e"));
    }

    [Fact]
    public async Task The_effect_key_survives_the_record_and_an_attempt_written_without_one_reads_its_own_key_as_its_effect()
    {
        var call = await StartAsync(Performer());
        var kept = Files.Read(Unit)!.Invoked(call.Invocation)!.Call;
        Assert.Equal((call.EffectKey, call.EffectKey), (kept.EffectKey, kept.Effect));

        // An attempt an older build wrote, or wrote over, holds no effect key: its own key is its effect, and it is sent with that alone.
        var path = NodeRecordFiles.PathOf(_home, Unit);
        var written = JsonNode.Parse(File.ReadAllText(path))!;
        foreach (var attempt in written["attempts"]!.AsArray()) Assert.True(attempt!["call"]!.AsObject().Remove("effectKey"));
        File.WriteAllText(path, written.ToJsonString());
        var older = new NodeRecordFiles(_home, () => true).Read(Unit)!.Invoked(call.Invocation)!.Call;
        Assert.Null(older.EffectKey);
        Assert.Equal(call.IdempotencyKey, older.Effect);
        Assert.False(WorkWire.Perform(older).ContainsKey("effectKey"));
    }

    [Fact]
    public void The_wire_carries_the_effect_key_beside_the_attempt_key_and_reads_it_back()
    {
        var call = new WorkPerformCall("u", "acme.deploy/release", 1, "i-1", "k-1", Tip, [], _now) { EffectKey = "e-abc" };
        var written = WorkWire.Perform(call);
        Assert.Equal(("k-1", "e-abc"), (written["idempotencyKey"]!.GetValue<string>(), written["effectKey"]!.GetValue<string>()));
        using var document = JsonDocument.Parse(written.ToJsonString());
        Assert.Equal("e-abc", WorkWire.ReadPerform(document.RootElement)!.EffectKey);
    }
}
