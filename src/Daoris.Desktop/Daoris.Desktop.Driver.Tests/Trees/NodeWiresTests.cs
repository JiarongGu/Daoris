using System.Reflection;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-WIRE1a (the fabric events design §E3): the wires' validation when a graph is saved. The six kinds, each with what it may
/// connect; <c>allOf</c> and <c>anyOf</c> groups on a node's eligibility wires; every data input mandatory and fed once, at its schema;
/// and every refusal §E3 names, each with a stable code and words. Pure: nothing evaluated, nothing stored. The refusals are rows of the
/// shared table the CLI's twin reads too (FABRIC-GRAPH1c, <see cref="GraphProblemsTable"/>).
/// </summary>
public sealed class NodeWiresTests
{
    private static NodeDeclaration Node(string id, string kind) =>
        new(id, kind, 1, NodeAuthorities.BuiltIn.TryGetValue(kind, out var builtIn) ? builtIn.Reach : NodeReach.Performed);

    private static Dictionary<string, string> Ports(string name, string schema) => new() { [name] = schema };

    /// <summary>The design's §E7 example, with a trigger, a failure path and a rejection path added: every kind once at least.</summary>
    private static readonly NodeDeclaration[] Nodes =
    [
        Node("line", NodeKinds.Line),
        Node("qa", NodeKinds.Human) with { Evidence = "qa-proof@1" },
        Node("dependency", NodeKinds.WaitUnit),
        Node("deploy", "acme.deploy/deploy") with
        {
            Evidence = "acme.deploy/receipt@1",
            Inputs = Ports("approval", "qa-proof@1"),
            Outputs = Ports("preview", "acme.deploy/preview@1"),
        },
        Node("close", NodeKinds.QuestClose) with { Inputs = Ports("delivery", "acme.deploy/receipt@1") },
        Node("announce", "acme.chat/announce") with { Inputs = Ports("preview", "acme.deploy/preview@1") },
        Node("rollback", NodeKinds.Human),
        Node("redo", NodeKinds.Human),
    ];

    private static Wire After(string id, string from, string to) => new(id, WireKinds.After, new(from), new(to));

    private static Wire Data(string id, string from, string output, string to, string input) => new(id, WireKinds.Data, new(from, output), new(to, input));

    private static Wire Waiting(string id, string to) =>
        new(id, WireKinds.Wait, null, new(to)) { Waits = new WaitTarget("u2", "r2") { End = true, Commitment = "q2/take-7" } };

    private static readonly Wire[] Wires =
    [
        After("a", "line", "qa"),
        Waiting("b", "dependency"),
        After("c", "dependency", "deploy"),
        Data("d", "qa", WireEnd.Evidence, "deploy", "approval"),
        Data("e", "deploy", WireEnd.Evidence, "close", "delivery"),
        new("f", WireKinds.Trigger, new("deploy"), new("announce")),
        Data("g", "deploy", "preview", "announce", "preview"),
        new("h", WireKinds.Failed, new("deploy"), new("rollback")),
        new("i", WireKinds.Rejected, new("qa"), new("redo")),
    ];

    private static readonly WireGroup AnyOfG = new("g", WireGroupModes.AnyOf);

    public static TheoryData<string> CaseNames() => GraphProblemsTable.Names("wires");

    /// <summary>The design's example, with every kind: a save of it refuses nothing, and it is the table's base read from its JSON.</summary>
    [Fact]
    public void The_designs_example_with_each_kind_holds()
    {
        Assert.Empty(NodeWires.Problems(Nodes, Wires));
        Assert.Equal(WireKinds.All.Order(), Wires.Select(wire => wire.Kind).Distinct().Order());
        Assert.All(Nodes, node => Assert.Null(NodeDeclarations.Problem(node)));
        var read = GraphProblemsTable.Graph(GraphProblemsTable.Row("wires", "the design's example with every kind holds"));
        Assert.Equal(Nodes, read.Nodes);
        Assert.Equal(Wires, read.Wires);
    }

    /// <summary>
    /// FABRIC-GRAPH1c: each row of the shared table (`graph-problems.json`'s `wires`), which the CLI's <c>nodegraphs.ts</c> judges too:
    /// each refusal §E3 names alone, with its code, its wire and node, and its words; the wires that hold; several problems in a stable
    /// order.
    /// </summary>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Each_refusal_is_named_alone_with_its_code_and_words(string name)
    {
        var row = GraphProblemsTable.Row("wires", name);
        var graph = GraphProblemsTable.Graph(row);
        var problems = NodeWires.Problems(graph.Nodes, graph.Wires);

        Assert.Equal(GraphProblemsTable.Expected(row), GraphProblemsTable.Said(problems));
        Assert.Equal(problems, NodeWires.Problems(graph.Nodes, graph.Wires));
    }

    /// <summary>Every refusal code is held by a row of the shared table: a code added without one fails here.</summary>
    [Fact]
    public void Every_refusal_code_has_its_case()
    {
        var codes = typeof(WireRefusal).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (string)field.GetValue(null)!).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Empty(codes.Except(GraphProblemsTable.Codes()));
    }

    /// <summary>
    /// What each kind may connect, beyond the example: a rejection path from a plugin's node that declares it, a failure path from a
    /// join, an observed built-in's evidence as a data source at its record's schema, and an <c>anyOf</c> with a wire of every
    /// eligibility kind.
    /// </summary>
    [Fact]
    public void Each_kind_connects_what_it_may()
    {
        NodeDeclaration[] nodes =
        [
            .. Nodes,
            Node("review", "acme.review/review") with { Rejectable = true },
            Node("checks", NodeKinds.All),
            Node("audit", "acme.audit/audit") with { Inputs = Ports("landing", BuiltInNodes.EvidenceSchema(NodeKinds.Line)) },
        ];
        Wire[] wires =
        [
            .. Wires,
            new("j", WireKinds.Rejected, new("review"), new("redo")) { Group = AnyOfG },
            new("k", WireKinds.Failed, new("checks"), new("redo")) { Group = AnyOfG },
            Data("l", "line", WireEnd.Evidence, "audit", "landing"),
            new("m", WireKinds.Trigger, new("line"), new("redo")) { Group = AnyOfG },
            After("n", "line", "redo") with { Group = AnyOfG },
        ];

        Assert.Empty(NodeWires.Problems(nodes, wires));
        Assert.Equal("daoris/line@1", NodeWires.SourceSchema(Node("line", NodeKinds.Line), WireEnd.Evidence));
        Assert.Equal("acme.deploy/preview@1", NodeWires.SourceSchema(Nodes[3], "preview"));
        Assert.Equal("acme.deploy/receipt@1", NodeWires.SourceSchema(Nodes[3], WireEnd.Evidence));
        Assert.Null(NodeWires.SourceSchema(Node("rollback", NodeKinds.Human), WireEnd.Evidence));
        Assert.Null(NodeWires.SourceSchema(Node("dependency", NodeKinds.WaitUnit), WireEnd.Evidence));
        Assert.Null(NodeWires.SourceSchema(Nodes[3], "approval"));
        Assert.Equal("outputs", NodeDeclarations.Problem(Node("tag", "acme.git/tag") with { Outputs = Ports(WireEnd.Evidence, "acme.git/tag@1") }));
    }

    /// <summary>
    /// A node's eligibility groups combine by all: each declared group once, each ungrouped wire a group of its own, every group's wires
    /// in wire-id order (an <c>anyOf</c>'s witness order, WIRE1b), and no data or wait wire among them, since inputs stay mandatory.
    /// </summary>
    [Fact]
    public void A_nodes_groups_gather_its_eligibility_wires_in_wire_id_order()
    {
        Wire[] wires =
        [
            .. Wires,
            new("z", WireKinds.Rejected, new("qa"), new("deploy")) { Group = new("either", WireGroupModes.AnyOf) },
            After("y", "line", "deploy") with { Group = new("either", WireGroupModes.AnyOf) },
            After("w", "announce", "deploy"),
        ];

        Assert.Equal(
            new (string?, string, string[])[]
            {
                (null, WireGroupModes.AllOf, ["c"]),
                (null, WireGroupModes.AllOf, ["w"]),
                ("either", WireGroupModes.AnyOf, ["y", "z"]),
            },
            NodeWires.Groups("deploy", wires).Select(group => (group.Id, group.Mode, group.Wires.ToArray())));
        Assert.All(NodeWires.Groups("deploy", wires), group => Assert.Equal("deploy", group.Node));
        Assert.Empty(NodeWires.Groups("dependency", wires));
        Assert.Empty(NodeWires.Groups("close", wires));
    }
}
