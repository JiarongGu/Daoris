using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A red check goes back to the work's session for its first rounds and reaches the person once, at the bound: the reds are counted from
/// the run's record, and a development graph shipped with a check carries the way back to its work.
/// </summary>
public sealed class CheckRoundsTests
{
    private static readonly NodeDeclaration Check = new("check", NodeKinds.Check, 1, NodeReach.Performed);

    private static NodeAttempt Red(int attempt) =>
        new("check", attempt, NodeKinds.Check, new WorkPerformCall("run:r", NodeKinds.Check, 1, $"i-{attempt}", $"k-{attempt}", "tip", [], DateTimeOffset.UnixEpoch),
            AttemptStates.Settled) { Outcome = WorkStates.Failed };

    [Fact]
    public void Each_settled_failure_of_a_check_is_a_round_and_the_bound_one_is_not_sent_back()
    {
        var record = new NodeRecord("run:r") { Attempts = [Red(1), Red(2), Red(2) with { Outcome = WorkStates.Reached, Node = "check" }] };
        var reds = CheckRounds.Reds(record);

        Assert.Equal(2, reds["check"]);
        Assert.True(CheckRounds.SendsBack(Check, reds));
        Assert.False(CheckRounds.SendsBack(Check, CheckRounds.Reds(record with { Attempts = [Red(1), Red(2), Red(3)] })));
        Assert.True(CheckRounds.SendsBack(new NodeDeclaration("review", NodeKinds.Review, 1, NodeReach.Pressed), new Dictionary<string, int> { ["review"] = 9 }));
    }

    [Theory]
    [InlineData("merge-after-accept")]
    [InlineData("merge-automatic")]
    [InlineData("branch-to-push")]
    [InlineData("pull-request")]
    [InlineData("integrated")]
    [InlineData("integrated-no-press")]
    [InlineData("release")]
    public void A_shipped_development_graph_checks_after_its_content_and_sends_a_red_back_to_its_work(string id)
    {
        var graph = NodeGraphs.Read(WorkflowDefaults.Of(id)!.GraphJson, onSave: true).Graph!;
        var check = Assert.Single(graph.Nodes, node => node.Kind == NodeKinds.Check);

        Assert.Equal("\"reached\"", check.Options["gates"]);
        Assert.Contains(graph.Wires, wire => wire is { Kind: WireKinds.After } && wire.From?.Node == "content" && wire.To.Node == check.Id);
        Assert.Contains(graph.Wires, wire => wire is { Kind: WireKinds.Back, On: NodeStates.Failed } && wire.From?.Node == check.Id && wire.To.Node == "work");
        Assert.DoesNotContain(graph.Wires, wire => wire.Kind == WireKinds.After && wire.From?.Node == "content" && wire.To.Node != check.Id);
    }
}
