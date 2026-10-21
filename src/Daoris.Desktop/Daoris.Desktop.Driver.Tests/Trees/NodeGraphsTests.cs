using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-GRAPH1 (the fabric events design §E7): a graph's shape, its bounds, its entries and end, every node reachable, D157's protected
/// order before the landing, canary 40's closure deadlock and the cleanup, composed with the declarations' and the wires' own checks; the
/// digest over immutable declarations; and the graph read from its JSON. Pure: nothing evaluated, nothing stored.
/// </summary>
public sealed class NodeGraphsTests
{
    private static NodeDeclaration Node(string id, string kind) =>
        new(id, kind, 1, NodeAuthorities.BuiltIn.TryGetValue(kind, out var builtIn) ? builtIn.Reach : NodeReach.Performed);

    private static Dictionary<string, string> Ports(string name, string schema) => new() { [name] = schema };

    private static Wire After(string id, string from, string to) => new(id, WireKinds.After, new(from), new(to));

    private static Wire Data(string id, string from, string output, string to, string input) => new(id, WireKinds.Data, new(from, output), new(to, input));

    /// <summary>The design's §E7 example, its ports declared: a QA press after the line, a dependency, a deploy, and the quest's close.</summary>
    private static readonly NodeDeclaration[] Nodes =
    [
        Node("line", NodeKinds.Line),
        Node("qa", NodeKinds.Human) with
        {
            Evidence = "qa-proof@1",
            Options = new Dictionary<string, string> { ["brief"] = "\"Verify the release\"", ["attachmentSchema"] = "\"qa-proof@1\"" },
        },
        Node("dependency", NodeKinds.WaitUnit),
        Node("deploy", "acme.deploy/deploy") with { Evidence = "acme.deploy/receipt@1", Inputs = Ports("approval", "qa-proof@1") },
        Node("close", NodeKinds.QuestClose) with { Inputs = Ports("delivery", "acme.deploy/receipt@1") },
    ];

    private static readonly Wire[] Wires =
    [
        After("a", "line", "qa"),
        new("b", WireKinds.Wait, null, new("dependency")) { Waits = new WaitTarget("u2", "r2") { End = true, Commitment = "q2/take-7" } },
        After("c", "dependency", "deploy"),
        Data("d", "qa", WireEnd.Evidence, "deploy", "approval"),
        Data("e", "deploy", WireEnd.Evidence, "close", "delivery"),
    ];

    private static readonly NodeGraph Example = new(Nodes, Wires, ["line", "dependency"], "close") { Cleanup = new(GraphCleanupUntil.End) };

    /// <summary>The example as its JSON, as a named graph or a <c>workCycles</c> entry holds it.</summary>
    internal const string ExampleJson = """
        { "schemaVersion": 2, "entries": ["line", "dependency"], "end": "close", "cleanup": { "until": "end" },
          "nodes": [
            { "id": "line", "kind": "line", "version": 1, "reach": "observed" },
            { "id": "qa", "kind": "human", "version": 1, "reach": "pressed", "evidence": "qa-proof@1",
              "options": { "brief": "Verify the release", "attachmentSchema": "qa-proof@1" } },
            { "id": "dependency", "kind": "wait.unit", "version": 1, "reach": "waited" },
            { "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed", "evidence": "acme.deploy/receipt@1",
              "inputs": { "approval": "qa-proof@1" } },
            { "id": "close", "kind": "quest.close", "version": 1, "reach": "performed", "inputs": { "delivery": "acme.deploy/receipt@1" } } ],
          "wires": [
            { "id": "a", "kind": "after", "from": "line", "to": "qa" },
            { "id": "b", "kind": "wait", "from": { "commitment": "q2/take-7", "unit": "u2", "revision": "r2", "end": true }, "to": "dependency" },
            { "id": "c", "kind": "after", "from": "dependency", "to": "deploy" },
            { "id": "d", "kind": "data", "from": "qa.evidence", "to": "deploy.approval" },
            { "id": "e", "kind": "data", "from": "deploy.evidence", "to": "close.delivery" } ] }
        """;

    /// <summary>
    /// FABRIC-GRAPH1's re-check, finding 4: a look and a plugin in an <c>all</c> join, the landing on the join's pass or its failure. The
    /// plugin fails, the join fails at once while the look still waits (WireEvaluation's early failure), and its failure's wire releases the
    /// landing. A row of the shared table, which refuses it.
    /// </summary>
    private static NodeGraph EarlyFailure() => GraphProblemsTable.Graph(GraphProblemsTable.Row("graphs", "a landing on a join's early failure"));

    public static TheoryData<string> CaseNames() => GraphProblemsTable.Names("graphs");

    public static TheoryData<string> FragmentCaseNames() => GraphProblemsTable.Names("fragments");

    public static TheoryData<string> UnreadableNames() => GraphProblemsTable.Names("unreadable");

    [Fact]
    public void The_designs_example_holds_and_reads_from_its_json()
    {
        Assert.Empty(NodeGraphs.Problems(Example));
        var (read, problems) = NodeGraphs.Read(ExampleJson);
        Assert.Empty(problems);
        Assert.NotNull(read);
        Assert.Equal(NodeGraphs.Digest(Example), NodeGraphs.Digest(read));
        Assert.Equal(["line", "dependency"], read.Entries);
        Assert.Equal(new GraphCleanup(GraphCleanupUntil.End), read.Cleanup);
    }

    /// <summary>
    /// FABRIC-GRAPH1c: each row of the table (`graph-problems.json`'s `graphs`):
    /// each refusal of the graph's own alone, with its code, wire and node, and its words; the graphs that hold, the bounds at their limits
    /// among them; the wires' and the declarations' checks composed with the graph's.
    /// </summary>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Each_refusal_is_named_alone_with_its_code_and_words(string name)
    {
        var row = GraphProblemsTable.Row("graphs", name);
        Assert.Equal(GraphProblemsTable.Expected(row), GraphProblemsTable.Said(NodeGraphs.Problems(GraphProblemsTable.Graph(row))));
    }

    /// <summary>
    /// The re-check's finding 4, its other side: the same join with only its pass into the landing holds, since the join passes only once
    /// every member has, the look among them; and its failure's path may go anywhere but the landing.
    /// </summary>
    [Fact]
    public void A_join_holding_a_look_releases_the_landing_only_on_its_pass()
    {
        var graph = EarlyFailure();
        // FABRIC-GRAPH1i: the look is wired straight into the landing too, since a skip of the join's other member would pass its wire.
        var wired = After("x", "look", "landing");
        Assert.Empty(NodeGraphs.Problems(graph with { Wires = [.. graph.Wires.Where(wire => wire.Id != "d"), wired] }));
        var retry = graph with
        {
            Nodes = [.. graph.Nodes, Node("fix", NodeKinds.Human)],
            Wires = [.. graph.Wires.Where(wire => wire.Id != "d"), new Wire("d", WireKinds.Failed, new("checks"), new("fix")), wired],
        };
        Assert.Empty(NodeGraphs.Problems(retry));
    }

    /// <summary>
    /// The second re-check's finding 3: a member fails a join only where its kind's authority lets something fail it, so a join of a look
    /// and a person's press, neither of which anything fails, has no failed path, and its failure's wire beside its pass into the landing
    /// holds. A join of joins asks the same of its members' members: one holding a plugin may fail, and its failure into the landing is
    /// refused as before.
    /// </summary>
    [Fact]
    public void A_join_none_of_whose_members_can_fail_has_no_failed_path()
    {
        var graph = EarlyFailure();
        var pressed = graph with
        {
            Nodes = [.. graph.Nodes.Select(node => node.Id == "check" ? Node("check", NodeKinds.Human) : node)],
        };
        // The order holds by reach here; FABRIC-GRAPH1i asks only the look's wire into the landing besides.
        IEnumerable<string> Codes(NodeGraph each) => NodeGraphs.Problems(each).Select(problem => problem.Code);
        Assert.Equal([GraphRefusal.ProtectedWire], Codes(pressed));
        Assert.Empty(NodeGraphs.Problems(pressed with { Wires = [.. pressed.Wires, After("x", "look", "landing")] }));

        NodeGraph Nested(NodeDeclaration inner) => pressed with
        {
            Nodes = [.. pressed.Nodes, Node("inner", NodeKinds.All), inner],
            Wires = [.. pressed.Wires.Where(wire => wire.Id != "b"), After("b", "inner", "checks"), After("e", "check", "inner"), After("f", inner.Id, "inner")],
            Entries = ["look", "check", inner.Id],
        };
        Assert.Equal([GraphRefusal.ProtectedWire], Codes(Nested(Node("sign", NodeKinds.GoAhead))));
        Assert.Equal(new (string, string?)[] { (GraphRefusal.ProtectedOrder, "look") },
            NodeGraphs.Problems(Nested(Node("scan", "acme/scan"))).Select(problem => (problem.Code, problem.Node)));
    }

    /// <summary>
    /// Each row of the shared table's <c>fragments</c>, through the landing rule that holds the cycle: a fragment's own refusal alone, a
    /// cycle's graph that does not hold under its rule's landing, and the cycles that hold.
    /// </summary>
    [Theory]
    [MemberData(nameof(FragmentCaseNames))]
    public void Each_fragments_refusal_is_named_alone(string name)
    {
        var row = GraphProblemsTable.Row("fragments", name);
        var form = (JsonObject)row["rule"]!;
        var rule = new LandingRule((string)form["form"]!, (string?)form["pattern"], Plugin: (string?)form["plugin"]) { Cycle = row["cycle"]!.ToJsonString() };
        var home = NodeGraphHomes.OfLanding(rule, LandingSource.Repository);

        Assert.Equal(GraphProblemsTable.Expected(row), GraphProblemsTable.Said(home.Problems));
        Assert.Equal(home.Problems.Count == 0, home.Readable);
        Assert.Equal(home.Problems.Count == 0, home.Graph is not null);
    }

    /// <summary>Every code of the graph's own is held by a row of the shared table: one added without a row fails here.</summary>
    [Fact]
    public void Every_refusal_code_has_its_case()
    {
        var codes = typeof(GraphRefusal).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (string)field.GetValue(null)!).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Empty(codes.Intersect(typeof(WireRefusal).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (string)field.GetValue(null)!)));
        Assert.Empty(codes.Except(GraphProblemsTable.Codes()));
    }

    /// <summary>
    /// FABRIC-GRAPH1's review, finding 4: the protected order holds on every way to the landing, an <c>anyOf</c>'s members as alternatives.
    /// A landing that waits on the look and on another node as well holds; one the look's failure reaches does not, nor one a press reaches
    /// beside two looks in one <c>anyOf</c>, which is said once, naming the first look.
    /// </summary>
    [Fact]
    public void The_protected_order_holds_on_every_way_to_the_landing()
    {
        IEnumerable<string> Order(NodeGraph graph) => NodeGraphs.Problems(graph).Where(problem => problem.Code == GraphRefusal.ProtectedOrder).Select(problem => problem.Node!);
        NodeDeclaration[] nodes = [Node("look", NodeKinds.Look), Node("press", NodeKinds.Human), Node("landing", NodeKinds.Handed)];
        Wire Any(string id, string from) => After(id, from, "landing") with { Group = new WireGroup("g", WireGroupModes.AnyOf) };

        Assert.Empty(Order(new NodeGraph(nodes, [After("a", "look", "landing"), After("b", "press", "landing")], ["look", "press"], "landing")));
        Assert.Empty(Order(new NodeGraph(nodes, [After("a", "press", "look"), After("b", "look", "landing")], ["press"], "landing")));
        Assert.Equal(["look"], Order(new NodeGraph(nodes, [new Wire("a", WireKinds.Failed, new("look"), new("landing")), After("b", "press", "landing")], ["look", "press"], "landing")));
        Assert.Equal(["look"], Order(new NodeGraph([.. nodes, Node("second", NodeKinds.Look)], [Any("a", "look"), Any("b", "press"), Any("c", "second")], ["look", "press", "second"], "landing")));
    }

    /// <summary>
    /// FABRIC-GRAPH1i: a graph that wires each gate into each later one holds, a work's claim and a go-ahead beside them no gate; a gate
    /// absent asks nothing. FABRIC-GRAPH1g: observed nodes of different steps, of one built-in kind, or one observed beside a performed node
    /// of the same plugin kind hold.
    /// </summary>
    [Fact]
    public void A_graph_wiring_each_gate_into_each_later_one_holds_and_so_do_observed_nodes_of_different_steps()
    {
        NodeDeclaration[] gates = [Node("work", NodeKinds.Claimed), Node("opinion", NodeKinds.Opinion), Node("look", NodeKinds.Look), Node("press", NodeKinds.GoAhead), Node("landing", NodeKinds.Handed)];
        Wire[] wires = [After("a", "work", "opinion"), After("b", "opinion", "look"), After("c", "look", "press"), After("d", "press", "landing"),
            After("e", "opinion", "landing"), After("f", "look", "landing")];
        var graph = new NodeGraph(gates, wires, ["work"], "landing");
        Assert.Empty(NodeGraphs.Problems(graph));
        Assert.Empty(NodeGraphs.Problems(graph with { Nodes = [gates[0], gates[2], gates[4]], Wires = [After("a", "work", "look"), After("b", "look", "landing")] }));

        NodeDeclaration Watch(string id, string kind) => Node(id, kind) with { Reach = NodeReach.Observed };
        Assert.Empty(NodeGraphs.Problems(new NodeGraph([Watch("one", "acme/watch"), Watch("two", "acme/scan")], [], ["one", "two"], "two")));
        Assert.Empty(NodeGraphs.Problems(new NodeGraph([Watch("one", "acme/watch"), Node("two", "acme/watch")], [], ["one", "two"], "two")));
        Assert.Empty(NodeGraphs.Problems(new NodeGraph([Watch("one", "acme/watch"), Watch("two", "other/watch")], [], ["one", "two"], "two")));
    }

    /// <summary>The bounds (§E7) the table's rows hold at their limits are the driver's: 24 authored nodes, D157's 24 steps, and 96 wires.</summary>
    [Fact]
    public void The_bounds_hold_at_their_limits_and_synthetic_nodes_count_apart()
    {
        Assert.Equal(96, GraphProblemsTable.Dense(96).Wires.Count);
        Assert.Equal(24, NodeGraphs.MaxNodes);
        Assert.Equal(WorkflowKindTable.MaxSteps, NodeGraphs.MaxNodes);
        Assert.Equal(96, NodeGraphs.MaxWires);
    }

    /// <summary>The table's graph base is the design's example, read from its JSON as this class builds it.</summary>
    [Fact]
    public void The_tables_graph_base_is_the_designs_example()
    {
        Assert.Equal(NodeGraphs.Digest(Example), NodeGraphs.Digest(GraphProblemsTable.Graph(GraphProblemsTable.Row("graphs", "the design's example holds"))));
    }

    /// <summary>The digest: <c>gph-</c> and 32 hex, the same whatever order the nodes, wires, entries and ports were given in.</summary>
    [Fact]
    public void The_digest_is_stable_under_reordering()
    {
        var digest = NodeGraphs.Digest(Example);
        Assert.Matches(new Regex("^gph-[0-9a-f]{32}$"), digest);

        var reordered = Example with
        {
            Nodes = [.. Nodes.Reverse().Select(node => node with { Options = new Dictionary<string, string>(node.Options.Reverse()), Inputs = new Dictionary<string, string>(node.Inputs.Reverse()) })],
            Wires = [.. Wires.Reverse()],
            Entries = ["dependency", "line"],
        };
        Assert.Equal(digest, NodeGraphs.Digest(reordered));
        Assert.Equal(digest, NodeGraphs.Digest(NodeGraphs.Read(ExampleJson).Graph!));
    }

    /// <summary>Any declaration changed, a node's or a wire's or the graph's own, changes the digest, each to its own.</summary>
    [Fact]
    public void The_digest_moves_with_any_declaration()
    {
        NodeGraph WithNode(string id, Func<NodeDeclaration, NodeDeclaration> change) => Example with { Nodes = [.. Nodes.Select(node => node.Id == id ? change(node) : node)] };
        NodeGraph WithWire(string id, Func<Wire, Wire> change) => Example with { Wires = [.. Wires.Select(wire => wire.Id == id ? change(wire) : wire)] };

        NodeGraph[] changed =
        [
            WithNode("deploy", node => node with { Kind = "acme.deploy/ship" }),
            WithNode("deploy", node => node with { Version = 2 }),
            WithNode("deploy", node => node with { Reach = NodeReach.Observed }),
            WithNode("deploy", node => node with { Evidence = "acme.deploy/receipt@2" }),
            WithNode("deploy", node => node with { Retry = new NodeRetry(3, [5, 30]) }),
            WithNode("deploy", node => node with { Retry = new NodeRetry(3, [5, 31]) }),
            WithNode("deploy", node => node with { TimeoutSeconds = 600 }),
            WithNode("deploy", node => node with { Rejectable = true }),
            WithNode("deploy", node => node with { Skippable = false }),
            WithNode("deploy", node => node with { Inputs = Ports("approval", "qa-proof@2") }),
            WithNode("deploy", node => node with { Outputs = Ports("preview", "acme.deploy/preview@1") }),
            WithNode("deploy", node => node with { Synthetic = true }),
            WithNode("qa", node => node with { Options = new Dictionary<string, string> { ["brief"] = "\"Verify it\"", ["attachmentSchema"] = "\"qa-proof@1\"" } }),
            WithNode("qa", node => node with { Id = "check" }),
            WithWire("a", wire => wire with { Kind = WireKinds.Trigger }),
            WithWire("a", wire => wire with { Id = "a2" }),
            WithWire("a", wire => wire with { Group = new("g", WireGroupModes.AllOf) }),
            WithWire("c", wire => wire with { From = new("line") }),
            WithWire("c", wire => wire with { To = new("qa") }),
            WithWire("d", wire => wire with { To = new("deploy", "approval2") }),
            WithWire("b", wire => wire with { Waits = new WaitTarget("u2", "r3") { End = true, Commitment = "q2/take-7" } }),
            WithWire("b", wire => wire with { Waits = new WaitTarget("u2", "r2") { End = true } }),
            WithWire("b", wire => wire with { Waits = new WaitTarget("u2", "r2") { Node = "deploy" } }),
            Example with { Entries = ["line"] },
            Example with { End = "deploy" },
            Example with { Cleanup = new(GraphCleanupUntil.End, 14) },
            Example with { Cleanup = GraphCleanup.Held },
            Example with { SchemaVersion = 3 },
        ];

        var digests = changed.Select(NodeGraphs.Digest).ToList();
        Assert.DoesNotContain(NodeGraphs.Digest(Example), digests);
        Assert.Equal(digests.Count, digests.Distinct().Count());
    }

    /// <summary>
    /// What a file holds that is not a graph's shape is read as unreadable, naming the first place in the reader's order, never in part:
    /// the shared table's <c>unreadable</c> rows, each in the words the CLI's reader says too, read as their JSON's text (a number by its
    /// own, so <c>1.0</c> is no whole number).
    /// </summary>
    [Theory]
    [MemberData(nameof(UnreadableNames))]
    public void What_is_not_a_graphs_shape_is_unreadable(string name)
    {
        var row = GraphProblemsTable.Row("unreadable", name);
        var (graph, problems) = NodeGraphs.Read(row["value"]!.ToJsonString());
        Assert.Null(graph);
        Assert.Equal(GraphProblemsTable.Expected(row), GraphProblemsTable.Said(problems));
    }

    /// <summary>A text that is not JSON is unreadable too, in the driver's own words: the CLI reads a file and says its own.</summary>
    [Fact]
    public void Text_that_is_not_json_is_unreadable()
    {
        var (graph, problems) = NodeGraphs.Read("not json");
        Assert.Null(graph);
        var problem = Assert.Single(problems);
        Assert.Equal(GraphRefusal.Unreadable, problem.Code);
        Assert.StartsWith("the graph is not JSON: ", problem.Words);
    }
}
