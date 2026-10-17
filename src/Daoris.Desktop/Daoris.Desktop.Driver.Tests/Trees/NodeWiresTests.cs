using System.Reflection;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-WIRE1a (the fabric events design §E3): the wires' validation when a graph is saved. The six kinds, each with what it may
/// connect; <c>allOf</c> and <c>anyOf</c> groups on a node's eligibility wires; every data input mandatory and fed once, at its schema;
/// and every refusal §E3 names, each with a stable code and words. Pure: nothing evaluated, nothing stored.
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

    private static Wire Wired(string id) => Wires.Single(wire => wire.Id == id);

    private static Wire[] Adding(Wire extra) => [.. Wires, extra];

    private static Wire[] Replacing(Wire wire) => [.. Wires.Select(each => each.Id == wire.Id ? wire : each)];

    private static Wire[] Without(string id) => [.. Wires.Where(wire => wire.Id != id)];

    private static readonly WireGroup AnyOfG = new("g", WireGroupModes.AnyOf);

    /// <summary>Each refusal, alone: the wires and nodes that make it, and the one problem the save names, by code, wire and node.</summary>
    private static readonly Dictionary<string, (NodeDeclaration[] Nodes, Wire[] Wires, (string Code, string? Wire, string? Node) Expected)> Cases = new()
    {
        ["no id"] = (Nodes, Adding(After("", "line", "rollback")), (WireRefusal.NoId, "", null)),
        ["two wires, one id"] = (Nodes, Adding(After("a", "line", "rollback")), (WireRefusal.WireTwice, "a", null)),
        ["two nodes, one id"] = ([.. Nodes, Node("qa", NodeKinds.Human)], Wires, (WireRefusal.NodeTwice, null, "qa")),
        ["a kind none of the six"] = (Nodes, Adding(new("x", "when", new("line"), new("rollback"))), (WireRefusal.UnknownKind, "x", null)),
        ["a wait from a node"] = (Nodes, Replacing(Wired("b") with { From = new("line") }), (WireRefusal.Shape, "b", null)),
        ["a wait waiting on nothing"] = (Nodes, Replacing(Wired("b") with { Waits = null }), (WireRefusal.Shape, "b", null)),
        ["an after with a wait target"] = (Nodes, Adding(After("x", "line", "rollback") with { Waits = new WaitTarget("u2", "r2") { End = true } }), (WireRefusal.Shape, "x", null)),
        ["an after with no source"] = (Nodes, Adding(new("x", WireKinds.After, null, new("rollback"))), (WireRefusal.Shape, "x", null)),
        ["an after with a port"] = (Nodes, Adding(new("x", WireKinds.After, new("line", WireEnd.Evidence), new("rollback"))), (WireRefusal.Shape, "x", null)),
        ["a data wire with no ports"] = (Nodes, Adding(new("x", WireKinds.Data, new("line"), new("rollback"))), (WireRefusal.Shape, "x", null)),
        ["a source on another unit"] = (Nodes, Adding(new("x", WireKinds.After, new("deploy") { Unit = "u2" }, new("rollback"))), (WireRefusal.ForeignNode, "x", "deploy")),
        ["a target on another unit"] = (Nodes, Adding(new("x", WireKinds.Trigger, new("line"), new("deploy") { Unit = "u2" })), (WireRefusal.ForeignNode, "x", "deploy")),
        ["a source not in the graph"] = (Nodes, Adding(After("x", "nowhere", "rollback")), (WireRefusal.MissingNode, "x", "nowhere")),
        ["a target not in the graph"] = (Nodes, Adding(After("x", "line", "nowhere")), (WireRefusal.MissingNode, "x", "nowhere")),
        ["an output not declared"] = (Nodes, Replacing(Data("g", "deploy", "previews", "announce", "preview")), (WireRefusal.MissingPort, "g", "deploy")),
        ["an input not declared"] = (Nodes, Adding(Data("x", "deploy", "preview", "announce", "banner")), (WireRefusal.MissingPort, "x", "announce")),
        ["evidence a node does not declare"] = (Nodes, Replacing(Data("d", "redo", WireEnd.Evidence, "deploy", "approval")), (WireRefusal.MissingPort, "d", "redo")),
        ["an incompatible schema"] = (Nodes, Replacing(Data("e", "deploy", "preview", "close", "delivery")), (WireRefusal.IncompatibleSchema, "e", "close")),
        ["a wait into another kind"] = (Nodes, Adding(Waiting("x", "rollback")), (WireRefusal.NotWaitUnit, "x", "rollback")),
        ["a wait with no revision"] = (Nodes, Replacing(Wired("b") with { Waits = new WaitTarget("u2", " ") { End = true } }), (WireRefusal.WaitTarget, "b", "dependency")),
        ["a wait on a node and the end"] = (Nodes, Replacing(Wired("b") with { Waits = new WaitTarget("u2", "r2") { Node = "deploy", End = true } }), (WireRefusal.WaitTarget, "b", "dependency")),
        ["a wait on neither a node nor the end"] = (Nodes, Replacing(Wired("b") with { Waits = new WaitTarget("u2", "r2") }), (WireRefusal.WaitTarget, "b", "dependency")),
        ["a wait.unit waiting on nothing"] = (Nodes, Without("b"), (WireRefusal.NoWait, null, "dependency")),
        ["a wait.unit waiting twice"] = (Nodes, Adding(Waiting("x", "dependency")), (WireRefusal.WaitTwice, "x", "dependency")),
        ["an input unfed"] = (Nodes, Without("d"), (WireRefusal.InputUnfed, null, "deploy")),
        ["an input fed twice"] = (Nodes, Adding(Data("x", "qa", WireEnd.Evidence, "deploy", "approval")), (WireRefusal.InputTwice, "x", "deploy")),
        ["a data wire grouped"] = (Nodes, Replacing(Wired("d") with { Group = AnyOfG }), (WireRefusal.GroupedInput, "d", "deploy")),
        ["a wait wire grouped"] = (Nodes, Replacing(Wired("b") with { Group = AnyOfG }), (WireRefusal.GroupedInput, "b", "dependency")),
        ["a group of no mode"] = (Nodes, Replacing(Wired("a") with { Group = new("g", "oneOf") }), (WireRefusal.GroupMode, "a", "qa")),
        ["a group with no id"] = (Nodes, Replacing(Wired("a") with { Group = new(" ", WireGroupModes.AllOf) }), (WireRefusal.GroupMode, "a", "qa")),
        ["a group of two modes"] = (Nodes,
            [.. Replacing(Wired("a") with { Group = new("g", WireGroupModes.AllOf) }), After("x", "dependency", "qa") with { Group = AnyOfG }],
            (WireRefusal.GroupMode, "x", "qa")),
        ["a group over two nodes"] = (Nodes,
            [.. Replacing(Wired("a") with { Group = AnyOfG }), After("x", "line", "rollback") with { Group = AnyOfG }],
            (WireRefusal.GroupSplit, "x", "rollback")),
        ["a failure path from a proof"] = (Nodes, Adding(new("x", WireKinds.Failed, new("line"), new("rollback"))), (WireRefusal.NeverFails, "x", "line")),
        ["a failure path from a person's task"] = (Nodes, Adding(new("x", WireKinds.Failed, new("qa"), new("rollback"))), (WireRefusal.NeverFails, "x", "qa")),
        ["a rejection path from a plugin's node not declared rejectable"] = (Nodes, Adding(new("x", WireKinds.Rejected, new("deploy"), new("redo"))), (WireRefusal.NeverRejected, "x", "deploy")),
        ["a rejection path from a proof"] = (Nodes, Adding(new("x", WireKinds.Rejected, new("line"), new("redo"))), (WireRefusal.NeverRejected, "x", "line")),
        ["a node's own trigger loop"] = (Nodes, Adding(new("x", WireKinds.Trigger, new("announce"), new("announce"))), (WireRefusal.Cycle, null, "announce")),
        ["two nodes after each other"] = (Nodes, Adding(After("x", "deploy", "dependency")), (WireRefusal.Cycle, null, "dependency")),
        ["a failure path back to a producer"] = (Nodes, Adding(new("x", WireKinds.Failed, new("deploy"), new("qa"))), (WireRefusal.Cycle, null, "deploy")),
    };

    public static TheoryData<string> CaseNames() => new([.. Cases.Keys]);

    /// <summary>The design's example, with every kind: a save of it refuses nothing.</summary>
    [Fact]
    public void The_designs_example_with_each_kind_holds()
    {
        Assert.Empty(NodeWires.Problems(Nodes, Wires));
        Assert.Equal(WireKinds.All.Order(), Wires.Select(wire => wire.Kind).Distinct().Order());
        Assert.All(Nodes, node => Assert.Null(NodeDeclarations.Problem(node)));
    }

    /// <summary>Each refusal §E3 names, alone: the one problem, with its code, its wire and node, and words naming the wire.</summary>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Each_refusal_is_named_alone_with_its_code_and_words(string name)
    {
        var (nodes, wires, expected) = Cases[name];
        var problems = NodeWires.Problems(nodes, wires);

        Assert.Equal(new[] { (name, expected) }, problems.Select(problem => (name, (problem.Code, problem.Wire, problem.Node))));
        var problem = problems[0];
        Assert.False(string.IsNullOrWhiteSpace(problem.Words));
        if (problem.Wire is { Length: > 0 } wire) Assert.Contains($"`{wire}`", problem.Words);
        if (problem.Node is { } node) Assert.Contains($"`{node}`", problem.Words);
    }

    /// <summary>Every refusal code is held by a case: a code added without one fails here.</summary>
    [Fact]
    public void Every_refusal_code_has_its_case()
    {
        var codes = typeof(WireRefusal).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (string)field.GetValue(null)!).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal(codes.Order(), Cases.Values.Select(each => each.Expected.Code).Distinct().Order());
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

    /// <summary>Several problems are all named, in a stable order: the nodes', each wire's in wire order, then the inputs', then cycles.</summary>
    [Fact]
    public void Every_problem_is_named_in_a_stable_order()
    {
        NodeDeclaration[] nodes = [.. Nodes, Node("qa", NodeKinds.Human)];
        Wire[] wires = [.. Without("d"), After("x", "line", "nowhere"), new("y", WireKinds.Trigger, new("redo"), new("redo"))];

        var problems = NodeWires.Problems(nodes, wires);

        Assert.Equal(
            new (string, string?, string?)[]
            {
                (WireRefusal.NodeTwice, null, "qa"),
                (WireRefusal.MissingNode, "x", "nowhere"),
                (WireRefusal.InputUnfed, null, "deploy"),
                (WireRefusal.Cycle, null, "redo"),
            },
            problems.Select(problem => (problem.Code, problem.Wire, problem.Node)));
        Assert.Equal(problems, NodeWires.Problems(nodes, wires));
    }
}
