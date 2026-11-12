using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.WireEvaluationTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The bound on a target's move (<see cref="WireTargets"/>): a step with a target is bound to the target's tip it read, and a move of the
/// target rebinds it only while nothing it feeds is active or reached. Run over a fixture step the pass hands a target and over every kind of
/// the kit with one (<see cref="ITargets"/>), so a kind added with a target meets the rule as it lands.
/// </summary>
public sealed class RebindBoundTests
{
    private const string Main = "refs/heads/main";

    /// <summary>The fixture step, then each kind of the kit that names a target.</summary>
    public static TheoryData<string> Kinds()
    {
        var kinds = new TheoryData<string> { "acme.git/replay" };
        foreach (var kind in NodeKit.Built.Units.Where(unit => unit is ITargets).Select(unit => unit.Row.Kind)) kinds.Add(kind);
        return kinds;
    }

    private static Graph Fed(string kind)
    {
        NodeDeclaration[] nodes =
        [
            Node("step", kind) with { Outputs = WirePorts("made", WireValues.Commit) },
            Node("check") with { Inputs = WirePorts("made", WireValues.Commit), Outputs = WirePorts("checked", WireValues.Commit) },
            Node("merge") with { Inputs = WirePorts("checked", WireValues.Commit) },
        ];
        Wire[] wires = [Data("w1", "step", "made", "check", "made"), Data("w2", "check", "checked", "merge", "checked")];
        return new Graph(nodes, wires) { Run = At("tip-m1") };
    }

    private static WireRun At(string tip) =>
        Base with { Targets = new Dictionary<string, NodePort>(StringComparer.Ordinal) { ["step"] = new(WireValues.Ref, Main, tip, null) } };

    private static Dictionary<string, NodePort> Made(string commit) => new() { ["made"] = new(WireValues.Commit, "refs/daoris/steps/r/step/1", commit, null) };

    /// <summary>The step is bound to the target's tip it read, so its binding names the target.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void A_step_with_a_target_is_bound_to_its_tip(string kind)
    {
        var graph = Fed(kind);
        graph.Start(graph.Evaluate(), "step");
        Assert.Equal(new NodePort(WireValues.Ref, Main, "tip-m1", null), graph.Statuses["step"].Binding!.Target);
    }

    /// <summary>A target moved while the step runs, or reached with nothing it feeds started, rebinds it.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void A_target_moved_while_nothing_it_feeds_is_active_or_reached_rebinds_it(string kind)
    {
        var running = Fed(kind);
        running.Start(running.Evaluate(), "step");
        running.Run = At("tip-m2");
        var lapsed = running.Evaluate().Moves.Single(move => move.Node == "step");
        Assert.Equal((NodeStates.Invalidated, WireWhy.Lapsed, NodeLapse.Target), (lapsed.Move.To, lapsed.Why, lapsed.Detail));

        var reached = Fed(kind);
        reached.Through("step", Made("c-1"));
        reached.Evaluate();
        Assert.Equal(NodeStates.Ready, reached.State("check"));
        reached.Run = At("tip-m2");
        reached.Evaluate();
        Assert.Equal(NodeStates.Invalidated, reached.State("step"));
        reached.Start(reached.Evaluate(), "step");
        Assert.Equal("tip-m2", reached.Statuses["step"].Binding!.Target!.Revision);
    }

    /// <summary>
    /// A target moved once a step it feeds has started, directly or through another, or has reached, rebinds nothing: the step stays reached
    /// on the tip it read, and what it feeds keeps its binding.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void A_target_moved_while_a_node_it_feeds_is_active_or_reached_rebinds_nothing(string kind)
    {
        foreach (var fed in new[] { "check-active", "check-reached", "merge-active" })
        {
            var graph = Fed(kind);
            graph.Through("step", Made("c-1"));
            if (fed == "check-active") graph.Start(graph.Evaluate(), "check");
            else
            {
                graph.Through("check", new() { ["checked"] = new(WireValues.Commit, "refs/daoris/steps/r/step/1", "c-1", null) });
                if (fed == "merge-active") graph.Start(graph.Evaluate(), "merge");
            }

            var bound = graph.Statuses.ToDictionary(each => each.Key, each => each.Value.Binding?.Id, StringComparer.Ordinal);
            graph.Run = At("tip-m2");
            var moved = graph.Evaluate();
            Assert.DoesNotContain(moved.Moves, move => move.Move.To == NodeStates.Invalidated);
            Assert.Equal((fed, NodeStates.Reached), (fed, graph.State("step")));
            Assert.Equal(bound, graph.Statuses.ToDictionary(each => each.Key, each => each.Value.Binding?.Id, StringComparer.Ordinal));
        }
    }
}
