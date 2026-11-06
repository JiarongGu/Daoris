using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The protected order (D157 §2.2: holds, opinion, look, landing) is one graph rule the kit runs once per save, beside each kind's own rules
/// (D247): the gates it holds before every landing are the kinds that declare a guard, read from their units and kept in no list beside them.
/// </summary>
public sealed class ProtectedOrderTests
{
    private static NodeDeclaration Node(string id, string kind) => new(id, kind, 1, NodeKit.Row(kind)?.Reach ?? NodeReach.Performed)
    {
        Options = kind == NodeKinds.Land ? new Dictionary<string, string> { ["method"] = "\"merge\"" } : new Dictionary<string, string>(),
    };

    private static NodeGraph Graph(NodeDeclaration[] nodes, params (string From, string To)[] after) => new(
        nodes, [.. after.Select(wire => new Wire($"{wire.From}>{wire.To}", WireKinds.After, new(wire.From), new(wire.To)))], [nodes[0].Id], nodes[^1].Id);

    [Fact]
    public void The_gates_before_the_landing_are_the_kinds_that_declare_a_guard()
    {
        var guards = NodeKit.Built.Units.Where(unit => unit.Guard != LandingGuard.None).ToDictionary(unit => unit.Row.Kind, unit => unit.Guard);

        Assert.Equal(new Dictionary<string, LandingGuard>
        {
            [NodeKinds.Opinion] = LandingGuard.Before, [NodeKinds.Look] = LandingGuard.Passed, [NodeKinds.Review] = LandingGuard.Before,
        }, guards);
        Assert.Equal(LandingGuard.None, NodeKit.Of("acme/deploy")!.Guard);
    }

    [Fact]
    public void The_kit_runs_the_order_once_beside_each_kinds_own_rules()
    {
        // The look stands beside the landing, not before it, and the push follows a merge, which makes no branch to push.
        var graph = Graph(
            [Node("work", NodeKinds.Claimed), Node("look", NodeKinds.Look), Node("land", NodeKinds.Land), Node("push", NodeKinds.Push)],
            ("work", "look"), ("work", "land"), ("land", "push"));

        var said = NodeKit.Problems(GraphStanding.Of(graph), graph).Select(problem => (problem.Code, problem.Node)).ToList();

        Assert.Equal([(GraphRefusal.ProtectedOrder, "look"), (GraphRefusal.LandTail, "push")], said);
        Assert.Equal(said, NodeGraphs.Problems(graph).Where(problem => problem.Code is GraphRefusal.ProtectedOrder or GraphRefusal.LandTail)
            .Select(problem => (problem.Code, problem.Node)));
    }

    [Fact]
    public void A_guard_that_must_be_passed_refuses_a_landing_some_way_reaches_without_it()
    {
        // Each gate is wired straight into the landing; made one of an `anyOf`, the opinion's wire lets it start with no look passed.
        var graph = Graph(
            [Node("work", NodeKinds.Claimed), Node("opinion", NodeKinds.Opinion), Node("look", NodeKinds.Look), Node("land", NodeKinds.Land)],
            ("work", "opinion"), ("opinion", "look"), ("look", "land"), ("opinion", "land"));
        var around = graph with
        {
            Wires = [.. graph.Wires.Select(wire => wire.To.Node == "land" ? wire with { Group = new WireGroup("either", WireGroupModes.AnyOf) } : wire)],
        };

        Assert.Empty(NodeKit.Problems(GraphStanding.Of(graph), graph));
        Assert.Equal([(GraphRefusal.ProtectedOrder, "look")], NodeKit.Problems(GraphStanding.Of(around), around).Select(problem => (problem.Code, problem.Node)));
    }
}
