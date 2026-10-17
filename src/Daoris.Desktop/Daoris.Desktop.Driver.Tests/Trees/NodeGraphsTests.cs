using System.Reflection;
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

    /// <summary>A chain of <paramref name="count"/> person's tasks, each after the one before: entries its first, its end its last.</summary>
    private static NodeGraph Chain(int count, int synthetic = 0)
    {
        var nodes = Enumerable.Range(0, count).Select(index => Node($"h{index}", NodeKinds.Human)).ToList();
        var wires = Enumerable.Range(1, count - 1).Select(index => After($"w{index}", $"h{index - 1}", $"h{index}")).ToList();
        for (var index = 0; index < synthetic; index++)
        {
            nodes.Add(Node($"s{index}", NodeKinds.Human) with { Synthetic = true });
            wires.Add(After($"x{index}", "h0", $"s{index}"));
        }

        return new(nodes, wires, ["h0"], $"h{count - 1}");
    }

    /// <summary>24 person's tasks wired every earlier to every later, up to <paramref name="wires"/> wires: a graph with no cycle.</summary>
    private static NodeGraph Dense(int wires)
    {
        var chain = Chain(24);
        var extra = (from source in Enumerable.Range(0, 24)
                     from target in Enumerable.Range(source + 2, Math.Max(0, 22 - source))
                     select After($"z{source}-{target}", $"h{source}", $"h{target}"))
            .Take(wires - chain.Wires.Count);
        return chain with { Wires = [.. chain.Wires, .. extra] };
    }

    private static NodeGraph Adding(NodeDeclaration node, params Wire[] wires) => Example with { Nodes = [.. Nodes, node], Wires = [.. Wires, .. wires] };

    /// <summary>Each of the graph's own refusals, alone: the graph that makes it, and the one problem the save names.</summary>
    private static readonly Dictionary<string, (NodeGraph Graph, (string Code, string? Wire, string? Node) Expected)> Cases = new()
    {
        ["a schema this build does not read"] = (Example with { SchemaVersion = 1 }, (GraphRefusal.SchemaVersion, null, null)),
        ["25 authored nodes"] = (Chain(25), (GraphRefusal.NodesBound, null, null)),
        ["97 wires"] = (Dense(97), (GraphRefusal.WiresBound, null, null)),
        ["more synthetic nodes than three an authored node"] = (Chain(1, synthetic: 4), (GraphRefusal.SyntheticBound, null, null)),
        ["a declaration that does not hold"] = (Example with { Nodes = [.. Nodes.Select(node => node.Id == "deploy" ? node with { Version = 0 } : node)] },
            (GraphRefusal.Declaration, null, "deploy")),
        ["no entry"] = (Example with { Entries = [] }, (GraphRefusal.NoEntry, null, null)),
        ["an entry naming no node"] = (Example with { Entries = ["line", "dependency", "nowhere"] }, (GraphRefusal.Entry, null, "nowhere")),
        ["an entry named twice"] = (Example with { Entries = ["line", "dependency", "line"] }, (GraphRefusal.Entry, null, "line")),
        ["an entry another node wires into"] = (Example with { Entries = ["line", "dependency", "qa"] }, (GraphRefusal.EntryWired, "a", "qa")),
        ["an end naming no node"] = (Example with { End = "nowhere" }, (GraphRefusal.End, null, "nowhere")),
        ["the agent's claim as the end"] = (new NodeGraph([Node("done", NodeKinds.Claimed)], [], ["done"], "done"), (GraphRefusal.EndKind, null, "done")),
        ["a node no entry reaches"] = (Adding(Node("orphan", NodeKinds.Human)), (GraphRefusal.Unreachable, null, "orphan")),
        ["the look after the landing"] = (new NodeGraph([Node("landing", NodeKinds.Handed), Node("look", NodeKinds.Look)], [After("a", "landing", "look")], ["landing"], "look"),
            (GraphRefusal.ProtectedOrder, null, "look")),
        ["the look beside the landing"] = (new NodeGraph([Node("landing", NodeKinds.Handed), Node("look", NodeKinds.Look)], [], ["landing", "look"], "landing"),
            (GraphRefusal.ProtectedOrder, null, "look")),
        // FABRIC-GRAPH1's review, finding 4: the look reaches the landing, and a person's press reaches it past the look in one `anyOf`.
        ["a way to the landing past the look"] = (new NodeGraph([Node("look", NodeKinds.Look), Node("bypass", NodeKinds.Human), Node("landing", NodeKinds.Handed)],
            [After("a", "look", "landing") with { Group = new WireGroup("g", WireGroupModes.AnyOf) }, After("b", "bypass", "landing") with { Group = new WireGroup("g", WireGroupModes.AnyOf) }],
            ["look", "bypass"], "landing"), (GraphRefusal.ProtectedOrder, null, "look")),
        // FABRIC-GRAPH1's re-check, finding 4: a look and a plugin in an `all` join, the landing on the join's pass or its failure. The plugin
        // fails, the join fails at once while the look still waits (WireEvaluation's early failure), and its failure's wire releases the landing.
        ["a landing on a join's early failure"] = (new NodeGraph(
            [Node("look", NodeKinds.Look), Node("check", "acme/check"), Node("checks", NodeKinds.All), Node("landing", NodeKinds.Handed)],
            [After("a", "look", "checks"), After("b", "check", "checks"),
                After("c", "checks", "landing") with { Group = new WireGroup("g", WireGroupModes.AnyOf) },
                new Wire("d", WireKinds.Failed, new("checks"), new("landing")) { Group = new WireGroup("g", WireGroupModes.AnyOf) }],
            ["look", "check"], "landing"), (GraphRefusal.ProtectedOrder, null, "look")),
        // FABRIC-WORKFLOW2: the opinion is a node, so D157 §2.2's order holds it before the look and the landing.
        ["the opinion after the look"] = (new NodeGraph([Node("look", NodeKinds.Look), Node("opinion", NodeKinds.Opinion), Node("landing", NodeKinds.Handed)],
            [After("a", "look", "opinion"), After("b", "opinion", "landing")], ["look"], "landing"), (GraphRefusal.ProtectedOrder, null, "opinion")),
        ["the opinion after the landing"] = (new NodeGraph([Node("landing", NodeKinds.Handed), Node("opinion", NodeKinds.Opinion)], [After("a", "landing", "opinion")], ["landing"], "opinion"),
            (GraphRefusal.ProtectedOrder, null, "opinion")),
        ["a sink as the end"] = (new NodeGraph([Node("stopped", NodeKinds.Stop)], [], ["stopped"], "stopped"), (GraphRefusal.EndKind, null, "stopped")),
        ["the close not the end"] = (Example with { End = "deploy" }, (GraphRefusal.CloseEnd, null, "close")),
        ["a node after the close"] = (Adding(Node("notify", NodeKinds.Human), After("x", "close", "notify")), (GraphRefusal.AfterClose, "x", "close")),
        ["the quest's own done before its close"] = (Adding(Node("done", NodeKinds.Claimed), After("x", "done", "close")) with { Entries = ["line", "dependency", "done"] },
            (GraphRefusal.ClosureDeadlock, null, "done")),
        ["a cleanup of no kind"] = (Example with { Cleanup = new("later") }, (GraphRefusal.Cleanup, null, null)),
        ["a cleanup's bound out of range"] = (Example with { Cleanup = new(GraphCleanupUntil.End, 91) }, (GraphRefusal.Cleanup, null, null)),
        ["a bound at held"] = (Example with { Cleanup = new(GraphCleanupUntil.Held, 14) }, (GraphRefusal.Cleanup, null, null)),
        ["a wait for an end at the line"] = (new NodeGraph([Node("line", NodeKinds.Line)], [], ["line"], "line") { Cleanup = new(GraphCleanupUntil.End) },
            (GraphRefusal.Cleanup, null, "line")),
    };

    /// <summary>Each fragment's own refusal (a landing rule's <c>cycle</c>, after the landing), alone.</summary>
    internal static readonly Dictionary<string, (LandingRule Rule, (string Code, string? Wire, string? Node) Expected)> FragmentCases = new()
    {
        ["a landing's kind declared again"] = (LandingRule.Merge with { Cycle = """{ "nodes": [ { "id": "proof", "kind": "line", "version": 1, "reach": "observed" } ], "wires": [ { "id": "a", "kind": "after", "from": "line", "to": "proof" } ] }""" },
            (GraphRefusal.LandingKind, null, "proof")),
        ["a wire into the landing"] = (LandingRule.Merge with { Cycle = """{ "nodes": [ { "id": "qa", "kind": "human", "version": 1, "reach": "pressed" } ], "wires": [ { "id": "a", "kind": "after", "from": "qa", "to": "handed" } ], "entries": ["qa"], "end": "line" }""" },
            (GraphRefusal.IntoLanding, "a", "handed")),
        ["a cycle that is not a graph's shape"] = (LandingRule.Merge with { Cycle = """{ "nodes": 3 }""" }, (GraphRefusal.Unreadable, null, null)),
    };

    public static TheoryData<string> CaseNames() => new([.. Cases.Keys]);

    public static TheoryData<string> FragmentCaseNames() => new([.. FragmentCases.Keys]);

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

    /// <summary>Each refusal of the graph's own, alone: the one problem, with its code, wire and node, and words naming them.</summary>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Each_refusal_is_named_alone_with_its_code_and_words(string name)
    {
        var (graph, expected) = Cases[name];
        AssertAlone(name, NodeGraphs.Problems(graph), expected);
    }

    /// <summary>
    /// The re-check's finding 4, its other side: the same join with only its pass into the landing holds, since the join passes only once
    /// every member has, the look among them; and its failure's path may go anywhere but the landing.
    /// </summary>
    [Fact]
    public void A_join_holding_a_look_releases_the_landing_only_on_its_pass()
    {
        var (graph, _) = Cases["a landing on a join's early failure"];
        Assert.Empty(NodeGraphs.Problems(graph with { Wires = [.. graph.Wires.Where(wire => wire.Id != "d")] }));
        var retry = graph with
        {
            Nodes = [.. graph.Nodes, Node("fix", NodeKinds.Human)],
            Wires = [.. graph.Wires.Where(wire => wire.Id != "d"), new Wire("d", WireKinds.Failed, new("checks"), new("fix"))],
        };
        Assert.Empty(NodeGraphs.Problems(retry));
    }

    /// <summary>Each fragment's refusal, alone, through the landing rule that holds it.</summary>
    [Theory]
    [MemberData(nameof(FragmentCaseNames))]
    public void Each_fragments_refusal_is_named_alone(string name)
    {
        var (rule, expected) = FragmentCases[name];
        var home = NodeGraphHomes.OfLanding(rule, LandingSource.Repository);
        AssertAlone(name, home.Problems, expected);
        Assert.Null(home.Graph);
        Assert.False(home.Readable);
    }

    private static void AssertAlone(string name, IReadOnlyList<WireProblem> problems, (string Code, string? Wire, string? Node) expected)
    {
        Assert.Equal(new[] { (name, expected) }, problems.Select(problem => (name, (problem.Code, problem.Wire, problem.Node))));
        var problem = problems[0];
        Assert.False(string.IsNullOrWhiteSpace(problem.Words));
        if (problem.Wire is { Length: > 0 } wire) Assert.Contains($"`{wire}`", problem.Words);
        if (problem.Node is { } node) Assert.Contains($"`{node}`", problem.Words);
    }

    /// <summary>Every code of the graph's own is held by a case: one added without a case fails here.</summary>
    [Fact]
    public void Every_refusal_code_has_its_case()
    {
        var codes = typeof(GraphRefusal).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (string)field.GetValue(null)!).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Empty(codes.Intersect(typeof(WireRefusal).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (string)field.GetValue(null)!)));
        Assert.Equal(codes.Order(), Cases.Values.Select(each => each.Expected.Code).Concat(FragmentCases.Values.Select(each => each.Expected.Code)).Distinct().Order());
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

    /// <summary>The bounds (§E7): 24 authored nodes and 96 wires hold; synthetic nodes and their wires are counted apart, three nodes an authored one.</summary>
    [Fact]
    public void The_bounds_hold_at_their_limits_and_synthetic_nodes_count_apart()
    {
        Assert.Empty(NodeGraphs.Problems(Chain(24)));
        Assert.Equal(96, Dense(96).Wires.Count);
        Assert.Empty(NodeGraphs.Problems(Dense(96)));
        Assert.Empty(NodeGraphs.Problems(Chain(1, synthetic: 3)));
        Assert.Empty(NodeGraphs.Problems(Chain(24, synthetic: 4)));
        Assert.Equal(24, NodeGraphs.MaxNodes);
        Assert.Equal(WorkflowKindTable.MaxSteps, NodeGraphs.MaxNodes);
    }

    /// <summary>
    /// The graph's save composes the declarations' checks and the wires' (WIRE1a, WIRE1d) with its own, every problem named: a wire's
    /// problem is a <see cref="WireRefusal"/>'s, by its code.
    /// </summary>
    [Fact]
    public void The_wires_and_declarations_checks_are_composed()
    {
        var graph = Adding(Node("checks", NodeKinds.All), After("x", "line", "checks") with { Group = new("g", WireGroupModes.AnyOf) }, After("y", "checks", "close"));
        Assert.Equal(new[] { (WireRefusal.GroupedMember, "x", "checks") }, NodeGraphs.Problems(graph).Select(problem => (problem.Code, problem.Wire, problem.Node)));

        var cyclic = Example with { Wires = [.. Wires, After("x", "deploy", "dependency")] };
        Assert.Contains(NodeGraphs.Problems(cyclic), problem => problem.Code == WireRefusal.Cycle);
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

    /// <summary>What a file holds that is not a graph's shape is read as unreadable, naming where, never in part.</summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("""{ "nodes": {} }""")]
    [InlineData("""{ "nodes": [ { "id": "a", "kind": "human", "version": "1", "reach": "pressed" } ], "entries": ["a"], "end": "a" }""")]
    [InlineData("""{ "nodes": [ { "id": "a", "kind": "human", "version": 1, "reach": "pressed" } ], "wires": [ { "id": "w", "kind": "after", "from": 3, "to": "a" } ], "entries": ["a"], "end": "a" }""")]
    [InlineData("""{ "nodes": [], "entries": "a", "end": "a" }""")]
    [InlineData("""{ "nodes": [], "entries": [], "end": "a", "cleanup": { "until": "end", "days": "14" } }""")]
    [InlineData("""{ "nodes": [ { "id": "a", "kind": "human", "version": 1, "reach": "pressed", "synthetic": true } ], "entries": ["a"], "end": "a" }""")]
    [InlineData("not json")]
    public void What_is_not_a_graphs_shape_is_unreadable(string json)
    {
        var (graph, problems) = NodeGraphs.Read(json);
        Assert.Null(graph);
        var problem = Assert.Single(problems);
        Assert.Equal(GraphRefusal.Unreadable, problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.Words));
    }
}
