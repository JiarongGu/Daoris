using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.WireEvaluationTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A reach kept across a live graph's edit. Each node is scoped, bound, receipted and fired on
/// by its place (<see cref="NodeGraphs.Places"/>), so an edit elsewhere leaves it current.
/// </summary>
public sealed class GraphEditReachTests
{
    private const string Opinion = "opinion";

    /// <summary>
    /// A skip, a failure, a rejection, a reach a trigger fired on, an attempt not reconciled and an opinion begun, each with what rests on it: the
    /// records the proof keeps.
    /// </summary>
    private static readonly NodeDeclaration[] Nodes =
    [
        Node("s"), Node("f"), Node("r") with { Rejectable = true }, Node("t"), Node("u"), Node(Opinion, NodeKinds.Opinion),
        Node("a1", NodeKinds.Task), Node("a2", NodeKinds.Task), Node("a3", NodeKinds.Task), Node("a4", NodeKinds.Task),
    ];

    private static readonly Wire[] Wires =
    [
        After("w1", "s", "a1"), Failed("w2", "f", "a2"), Rejected("w3", "r", "a3"), new("w4", WireKinds.Trigger, new("t"), new("a4")),
    ];

    private static NodeGraph GraphOf(params NodeDeclaration[] more) =>
        new([.. Nodes, .. more], Wires, ["s", "f", "r", "t", "u", Opinion, .. more.Select(node => node.Id)], "a1");

    /// <summary>The runtime's run on <paramref name="graph"/>: named by the unit alone, each node by its place.</summary>
    private static WireRun Now(NodeGraph graph, IEnumerable<string> fired) => new(NodeGraphs.Digest(graph), NodeRuns.Of(Unit))
    {
        Places = NodeGraphs.Places(graph),
        Revisions = Base.Revisions,
        Fired = fired.ToHashSet(StringComparer.Ordinal),
    };

    /// <summary>The records the runtime writes on <paramref name="graph"/>, serialized and read back.</summary>
    private static (NodeRecord Record, Graph Written) Written()
    {
        var graph = GraphOf();
        var written = new Graph(Nodes, Wires) { Run = Now(graph, []) };
        written.Move("s", new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = "not needed", Scope = written.Run.ScopeOf("s") });
        var first = written.Evaluate();
        foreach (var id in new[] { "f", "r", "t", "u", Opinion }) written.Start(first, id);
        written.Fail("f");
        written.Reject("r");
        written.Reach("t");
        written.Move("u", new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = written.Statuses["u"].Attempt });

        var second = written.Evaluate();
        var fired = new List<string>();
        foreach (var id in new[] { "a1", "a2", "a3", "a4" })
        {
            written.Start(second, id);
            fired.AddRange(second.Starts.Single(start => start.Node == id).Firings.Select(firing => firing.Key));
        }

        Assert.Single(fired);
        var record = new NodeRecord(Unit) { Statuses = written.Statuses, Fired = fired };
        return (NodeRecordFiles.Parse(NodeRecordFiles.ToJson(record))!, written);
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
    public void Records_keep_their_receipt_ids_and_lapse_nothing_and_fire_nothing_when_evaluated_again()
    {
        var (record, written) = Written();
        var again = WireEvaluation.Evaluate(Nodes, Wires, record.Statuses, Now(GraphOf(), record.Fired));

        Assert.Null(again.Refusal);
        Assert.Empty(again.Refused);
        Assert.True(again.Settled);
        Assert.Empty(again.Starts);
        foreach (var id in new[] { "a1", "a2", "a3", "a4" })
        {
            // Each receipt is the one its consumer's binding stores, so the consumer stays bound and the trigger fires nothing again.
            var judged = again.Of(id)!;
            Assert.True(judged.Eligible);
            Assert.Equal(record.Statuses[id].Binding!.PrerequisiteReceipts, judged.Receipts);
            Assert.Empty(judged.Firings);
        }

        Assert.All(new[] { "a1", "a2", "a3", "a4" }, id => Assert.Equal(written.Evaluate().Of(id)!.Receipts, again.Of(id)!.Receipts));
    }

    [Fact]
    public void A_skip_and_a_start_on_a_status_bound_by_place_pass_the_lifecycles_scope_checks()
    {
        var (record, _) = Written();
        var run = Now(GraphOf(), record.Fired);
        var opinion = Nodes.Single(node => node.Id == Opinion);
        var status = record.Statuses[Opinion];
        var skip = new NodeMove(NodeStates.Skipped, NodeCause.Authority) { Source = NodeAuthorities.Of(opinion)!.SkipBy, Attempt = status.Attempt };

        // The record's skip of an attempt is scoped by its node's place, kept or worked out alike.
        Assert.Null(NodeLifecycle.Apply(opinion, status, skip with { Scope = status.Scope }).Refused);
        Assert.Equal(status.Scope, run.ScopeOf(Opinion));
        Assert.Null(NodeLifecycle.Apply(opinion, status with { Scope = null }, skip with { Scope = run.ScopeOf(Opinion) }).Refused);

        // The person's new attempt on a failure starts on the node's place, its scope and its binding alike.
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
    public void An_edit_elsewhere_in_the_graph_lapses_none_of_the_records()
    {
        var (record, _) = Written();
        var grown = GraphOf(Node("x"));
        var statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal) { ["x"] = NodeLifecycle.Start(Unit, "x") };
        var edited = WireEvaluation.Evaluate(grown.Nodes, grown.Wires, statuses, Now(grown, record.Fired));

        Assert.Empty(edited.Refused);
        Assert.DoesNotContain(edited.Moves, move => move.Move.To == NodeStates.Invalidated);
        Assert.Equal((NodeStates.Failed, NodeStates.Rejected), (edited.Statuses["f"].State, edited.Statuses["r"].State));
        Assert.DoesNotContain(edited.Starts, start => start.Node == "u");
    }
}
