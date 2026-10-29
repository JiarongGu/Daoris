using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.WireEvaluationTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-RUN1f (the cutover design §9, D190 point 9): a reach kept across a live graph's edit. Each node is scoped, bound, receipted and fired on
/// by its place (<see cref="NodeGraphs.Places"/>), so an edit elsewhere leaves it current; a record written before names the whole graph's digest
/// and the run's old name, and is judged by that rule: nothing lapses or fires at the upgrade, and every one lapses at the first edit after.
/// </summary>
public sealed class GraphEditReachTests
{
    private const string Opinion = "opinion";

    /// <summary>
    /// A skip, a failure, a rejection, a reach a trigger fired on, an attempt not reconciled and an opinion begun, each with what rests on it: the
    /// records the proof keeps from before the build.
    /// </summary>
    private static readonly NodeDeclaration[] Nodes =
    [
        Node("s"), Node("f"), Node("r") with { Rejectable = true }, Node("t"), Node("u"), Node(Opinion, NodeKinds.Opinion),
        Node("a1", NodeKinds.Human), Node("a2", NodeKinds.Human), Node("a3", NodeKinds.Human), Node("a4", NodeKinds.Human),
    ];

    private static readonly Wire[] Wires =
    [
        After("w1", "s", "a1"), Failed("w2", "f", "a2"), Rejected("w3", "r", "a3"), new("w4", WireKinds.Trigger, new("t"), new("a4")),
    ];

    private static NodeGraph GraphOf(params NodeDeclaration[] more) =>
        new([.. Nodes, .. more], Wires, ["s", "f", "r", "t", "u", Opinion, .. more.Select(node => node.Id)], "a1");

    /// <summary>The runtime's run on <paramref name="graph"/> since FABRIC-RUN1f: named by the unit alone, each node by its place.</summary>
    private static WireRun Now(NodeGraph graph, IEnumerable<string> fired) => new(NodeGraphs.Digest(graph), NodeRuns.Of(Unit))
    {
        Places = NodeGraphs.Places(graph),
        Legacy = NodeRuns.Of(Unit, NodeGraphs.Digest(graph)),
        Revisions = Base.Revisions,
        Fired = fired.ToHashSet(StringComparer.Ordinal),
    };

    /// <summary>
    /// The records as the build before wrote them, serialized and read back: its run was named by the unit and the graph and knew no places,
    /// which is a <see cref="WireRun"/> with neither.
    /// </summary>
    private static (NodeRecord Record, Graph Before) Written()
    {
        var graph = GraphOf();
        var before = new Graph(Nodes, Wires) { Run = new WireRun(NodeGraphs.Digest(graph), NodeRuns.Of(Unit, NodeGraphs.Digest(graph))) { Revisions = Base.Revisions } };
        before.Skip("s");
        var first = before.Evaluate();
        foreach (var id in new[] { "f", "r", "t", "u", Opinion }) before.Start(first, id);
        before.Fail("f");
        before.Reject("r");
        before.Reach("t");
        before.Move("u", new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = before.Statuses["u"].Attempt });

        var second = before.Evaluate();
        var fired = new List<string>();
        foreach (var id in new[] { "a1", "a2", "a3", "a4" })
        {
            before.Start(second, id);
            fired.AddRange(second.Starts.Single(start => start.Node == id).Firings.Select(firing => firing.Key));
        }

        Assert.Single(fired);
        var record = new NodeRecord(Unit) { Statuses = before.Statuses, Fired = fired };
        return (NodeRecordFiles.Parse(NodeRecordFiles.ToJson(record))!, before);
    }

    [Fact]
    public void A_place_is_its_declaration_the_wires_into_it_and_whether_it_is_an_entry()
    {
        var graph = GraphOf();
        var places = NodeGraphs.Places(graph);
        Assert.All(places.Values, place => Assert.Matches("^plc-[0-9a-f]{32}$", place));
        Assert.Equal(Nodes.Length, places.Values.Distinct().Count());

        // A node added elsewhere moves no place, though it moves the graph's digest.
        var grown = GraphOf(Node("x"));
        Assert.NotEqual(NodeGraphs.Digest(graph), NodeGraphs.Digest(grown));
        Assert.All(places, place => Assert.Equal(place.Value, NodeGraphs.Places(grown)[place.Key]));

        // Its declaration, a wire into it and its entry each move its own place alone.
        var optioned = graph with { Nodes = [.. graph.Nodes.Select(node => node.Id == "t" ? node with { Options = new Dictionary<string, string> { ["region"] = "eu" } } : node)] };
        var rewired = graph with { Wires = [.. graph.Wires.Select(wire => wire.Id == "w1" ? wire with { Kind = WireKinds.Trigger } : wire)] };
        var unentered = graph with { Entries = [.. graph.Entries.Where(entry => entry != "u")] };
        foreach (var (edited, node) in new[] { (optioned, "t"), (rewired, "a1"), (unentered, "u") })
        {
            var now = NodeGraphs.Places(edited);
            Assert.Equal([node], places.Where(place => now[place.Key] != place.Value).Select(place => place.Key));
        }
    }

    [Fact]
    public void Records_written_before_keep_their_receipt_ids_and_lapse_nothing_and_fire_nothing_at_the_upgrade()
    {
        var (record, before) = Written();
        var upgrade = WireEvaluation.Evaluate(Nodes, Wires, record.Statuses, Now(GraphOf(), record.Fired));

        Assert.Null(upgrade.Refusal);
        Assert.Empty(upgrade.Refused);
        Assert.True(upgrade.Settled);
        Assert.Empty(upgrade.Starts);
        foreach (var id in new[] { "a1", "a2", "a3", "a4" })
        {
            // Each receipt is the one its consumer's binding stores, so the consumer stays bound and the trigger fires nothing again.
            var judged = upgrade.Of(id)!;
            Assert.True(judged.Eligible);
            Assert.Equal(record.Statuses[id].Binding!.PrerequisiteReceipts, judged.Receipts);
            Assert.Empty(judged.Firings);
        }

        // The same records under the build before, evaluated again: the same receipts, so the upgrade changed no id.
        var again = before.Evaluate();
        Assert.All(new[] { "a1", "a2", "a3", "a4" }, id => Assert.Equal(again.Of(id)!.Receipts, upgrade.Of(id)!.Receipts));
    }

    [Fact]
    public void A_skip_and_a_start_on_a_status_bound_before_pass_the_lifecycles_scope_checks()
    {
        var (record, _) = Written();
        var run = Now(GraphOf(), record.Fired);
        var opinion = Nodes.Single(node => node.Id == Opinion);
        var status = record.Statuses[Opinion];
        var skip = new NodeMove(NodeStates.Skipped, NodeCause.Authority) { Source = NodeAuthorities.Of(opinion)!.SkipBy, Attempt = status.Attempt };

        // The record's skip of an attempt bound before is scoped by that binding's own rule, the graph with the graph: kept or worked out alike.
        Assert.Null(NodeLifecycle.Apply(opinion, status, skip with { Scope = status.Scope }).Refused);
        Assert.Equal(status.Scope, run.Own(Opinion, status.Binding!.GraphDigest));
        Assert.Null(NodeLifecycle.Apply(opinion, status with { Scope = null }, skip with { Scope = run.Own(Opinion, status.Binding.GraphDigest) }).Refused);
        Assert.Equal(NodeRefusal.NotBound, NodeLifecycle.Apply(opinion, status, skip with { Scope = run.ScopeOf(Opinion) }).Refused);

        // The person's new attempt on a failure kept from before starts on the node's place, its scope and its binding alike.
        var f = Nodes.Single(node => node.Id == "f");
        var statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal)
        {
            ["f"] = NodeLifecycle.Apply(f, record.Statuses["f"], new NodeMove(NodeStates.Ready, NodeCause.Person)).Status,
        };
        var start = Assert.Single(WireEvaluation.Evaluate(Nodes, Wires, statuses, run).Starts, each => each.Node == "f");
        Assert.Equal((run.Places["f"], run.Places["f"], NodeRuns.Of(Unit)), (start.Move.Binding!.GraphDigest, start.Move.Scope!.GraphDigest, start.Move.Scope.Run));
        Assert.Null(NodeLifecycle.Apply(f, statuses["f"], start.Move).Refused);
    }

    [Fact]
    public void Records_written_before_lapse_at_the_first_edit_after_the_upgrade()
    {
        var (record, _) = Written();
        var grown = GraphOf(Node("x"));
        var statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal) { ["x"] = NodeLifecycle.Start(Unit, "x") };
        var edited = WireEvaluation.Evaluate(grown.Nodes, grown.Wires, statuses, Now(grown, record.Fired));

        Assert.Empty(edited.Refused);
        var lapsed = edited.Moves.Where(move => move.Move.To == NodeStates.Invalidated).ToDictionary(move => move.Node, move => (move.Why, move.Detail));
        Assert.Equal((WireWhy.SkipScope, (string?)null), lapsed["s"]);
        foreach (var id in new[] { "t", "u", Opinion, "a1", "a2", "a3", "a4" }) Assert.Equal((WireWhy.Lapsed, NodeLapse.Graph), lapsed[id]);

        // A failure and a rejection stay for the person (FABRIC-RUN1e), but are no longer receipts: nothing rests on them.
        Assert.Equal((NodeStates.Failed, NodeStates.Rejected), (edited.Statuses["f"].State, edited.Statuses["r"].State));
        Assert.Equal(["w2"], edited.Of("a2")!.Waiting);
        Assert.Equal(["w3"], edited.Of("a3")!.Waiting);

        // The attempt not reconciled stays held after its lapse, so nothing starts it again before its outcome is known.
        Assert.Equal(record.Statuses["u"].Attempt, edited.Statuses["u"].Unreconciled);
        Assert.DoesNotContain(edited.Starts, start => start.Node == "u");
    }
}
