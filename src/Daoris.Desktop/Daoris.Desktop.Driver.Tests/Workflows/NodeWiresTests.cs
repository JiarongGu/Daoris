using System.Reflection;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-WIRE1a (the fabric events design §E3): the wires' validation when a graph is saved. The kinds, each with what it may
/// connect; <c>allOf</c> and <c>anyOf</c> groups on a node's eligibility wires; every data input mandatory and fed once, at its schema;
/// and every refusal §E3 names, each with a stable code and words. Pure: nothing evaluated, nothing stored. The refusals are rows of the
/// shared table the CLI's twin reads too (FABRIC-GRAPH1c, <see cref="GraphProblemsTable"/>).
/// </summary>
public sealed class NodeWiresTests
{
    private static NodeDeclaration Node(string id, string kind) =>
        new(id, kind, 1, NodeKit.Row(kind)?.Reach ?? NodeReach.Performed);

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
        // DECIDE1b: a `when` and a `back` need a decision and an earlier node, so the table's own rows hold them, not this example.
        Assert.Equal(WireKinds.All.Except([WireKinds.When, WireKinds.Back]).Order(), Wires.Select(wire => wire.Kind).Distinct().Order());
        Assert.All(Nodes, node => Assert.Null(NodeDeclarations.Problem(node)));
        var read = GraphProblemsTable.Graph(GraphProblemsTable.Row("wires", "the design's example with every kind holds"));
        Assert.Equal(Nodes, read.Nodes);
        Assert.Equal(Wires, read.Wires);
    }

    /// <summary>
    /// FABRIC-GRAPH1c: each row of the table (`graph-problems.json`'s `wires`):
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
    /// DECIDE1b: a <c>back</c> wire is no eligibility wire, so no group gathers it and the run takes none, and it closes no cycle: a node it
    /// returns to waits on nothing more. The table's rows hold its refusals.
    /// </summary>
    [Fact]
    public void A_back_wire_is_gathered_by_no_group_and_closes_no_cycle()
    {
        Wire[] wires = [.. Wires, new("x", WireKinds.Back, new("qa"), new("line")) { On = NodeStates.Rejected }];

        Assert.Empty(NodeWires.Problems(Nodes, wires));
        Assert.Empty(NodeWires.Groups("line", wires));
        Assert.Contains(WireKinds.Back, WireKinds.All);
        Assert.DoesNotContain(WireKinds.Back, WireKinds.Eligibility);
        Assert.DoesNotContain(WireKinds.Back, WireKinds.Ordered);
    }

    private static Wire When(string id, string from, string outcome, string to) => new(id, WireKinds.When, new(from), new(to)) { Outcome = outcome };

    /// <summary>
    /// DECIDE1a: a <c>when</c> wire follows one of its source's declared outcomes, grouped like any eligibility wire and counted in a cycle.
    /// A source declaring none, an outcome it does not declare, a <c>when</c> naming none and another kind naming one are refused; the
    /// names a decision's outcomes keep are <see cref="NodeOutcomes.Problem"/>'s.
    /// </summary>
    [Fact]
    public void A_when_wire_follows_one_of_its_sources_declared_outcomes()
    {
        var either = new WireGroup("either", WireGroupModes.AnyOf);
        NodeDeclaration[] nodes =
        [
            Node("review", "acme.review/review") with { Outcomes = ["approved", "changes-asked"] },
            Node("land", NodeKinds.Human), Node("rework", NodeKinds.Human), Node("tell", NodeKinds.Human), Node("line", NodeKinds.Line),
        ];
        Wire[] wires =
        [
            When("a", "review", "approved", "land"), When("b", "review", "changes-asked", "rework"),
            When("c", "review", "approved", "tell") with { Group = either }, After("d", "line", "tell") with { Group = either },
        ];
        Assert.Empty(NodeWires.Problems(nodes, wires));
        Assert.Equal(["c", "d"], Assert.Single(NodeWires.Groups("tell", wires)).Wires);

        WireProblem One(Wire more) => Assert.Single(NodeWires.Problems(nodes, [.. wires, more]));
        Assert.Equal(new WireProblem(WireRefusal.NoOutcomes, "x", "line", "wire `x`: `line`, a `line`, declares no outcomes, so no `when` wire follows it."),
            One(When("x", "line", "approved", "rework")));
        Assert.Equal(new WireProblem(WireRefusal.UnknownOutcome, "x", "review", "wire `x`: `review` is reached with `approved` or `changes-asked`, never `merged`."),
            One(When("x", "review", "merged", "rework")));
        Assert.Equal(new WireProblem(WireRefusal.Shape, "x", null, "wire `x`: a `when` wire names the outcome of its source it follows."),
            One(new Wire("x", WireKinds.When, new("review"), new("rework"))));
        Assert.Equal(new WireProblem(WireRefusal.Shape, "x", null, "wire `x`: only a `when` wire names an outcome."),
            One(After("x", "review", "rework") with { Outcome = "approved" }));
        Assert.Equal(WireRefusal.Cycle, One(When("x", "review", "changes-asked", "review")).Code);

        Assert.Null(NodeOutcomes.Problem([]));
        Assert.Null(NodeOutcomes.Problem(["approved", "changes-asked"]));
        Assert.All(
            new IReadOnlyList<string>[]
            {
                ["approved"], [.. Enumerable.Range(1, 9).Select(route => $"route-{route}")], ["approved", "Approved"], ["approved", "changes_asked"],
                ["approved", "-asked"], ["approved", "approved"], ["approved", "reached"], ["failed", "approved"], ["rejected", "skipped"], ["stopped", "approved"],
            },
            outcomes => Assert.NotNull(NodeOutcomes.Problem(outcomes)));
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
            Node("audit", "acme.audit/audit") with { Inputs = Ports("landing", NodeObserving.EvidenceSchema(NodeKinds.Line)) },
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
