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

    [Fact]
    public void A_commit_that_failed_the_check_is_not_asked_again_but_another_is()
    {
        var record = new NodeRecord("run:r") { Attempts = [Red(1), Red(2)] };

        Assert.Equal(2, CheckRounds.RedAt(record, "check", "tip")?.Attempt);
        Assert.Null(CheckRounds.RedAt(record, "check", "other"));
        Assert.Null(CheckRounds.RedAt(record, "opinion", "tip"));
        Assert.Null(CheckRounds.RedAt(record, "check", ""));
        Assert.Null(CheckRounds.RedAt(null, "check", "tip"));
    }

    private static readonly DateTimeOffset Asked = new(2026, 11, 15, 13, 22, 10, TimeSpan.Zero);

    private static SessionRecord Waiting(string id, string run, DateTimeOffset since) =>
        new(id, "Daoris", SessionStates.AwaitingStep) { NoteParts = new StepWait(run, "work").Note().Parts, Updated = since };

    [Fact]
    public void The_work_answered_a_red_once_its_session_waits_on_the_runs_next_step_since_the_red_and_none_of_its_sessions_still_works()
    {
        Assert.True(CheckRounds.Answered([Waiting("s1", "r", Asked.AddMinutes(1))], "r", ["s1"], Asked));
        Assert.False(CheckRounds.Answered([Waiting("s1", "r", Asked.AddMinutes(-1))], "r", ["s1"], Asked));
        Assert.False(CheckRounds.Answered([Waiting("s1", "other", Asked.AddMinutes(1))], "r", ["s1"], Asked));
        Assert.False(CheckRounds.Answered([new SessionRecord("s1", "Daoris", "working") { Updated = Asked.AddMinutes(1) }], "r", ["s1"], Asked));
        Assert.False(CheckRounds.Answered(
            [Waiting("s1", "r", Asked.AddMinutes(1)), new SessionRecord("s2", "Daoris", "working")], "r", ["s1", "s2"], Asked));
        Assert.True(CheckRounds.Answered(
            [Waiting("s1", "r", Asked.AddMinutes(1)), new SessionRecord("s3", "Daoris", "working")], "r", ["s1"], Asked));
    }

    [Fact]
    public void A_red_past_the_bound_is_owed_a_telling_once_though_no_way_back_takes_it()
    {
        var graph = NodeGraphs.Read(WorkflowDefaults.Of("merge-after-accept")!.GraphJson, onSave: true).Graph!;
        var check = Assert.Single(graph.Nodes, node => node.Kind == NodeKinds.Check).Id;
        var declared = graph.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        NodeAttempt Of(int attempt) => Red(attempt) with { Node = check };
        NodeRecord Failed(int attempt, params NodeAttempt[] attempts) => new("run:r")
        {
            Attempts = attempts,
            Statuses = new Dictionary<string, NodeStatus> { [check] = NodeLifecycle.Start("run:r", check) with { State = NodeStates.Failed, Attempt = attempt } },
        };

        Assert.Empty(CheckRounds.SpentOwed(Failed(2, Of(1), Of(2)), graph.Wires, declared));
        var spent = Failed(3, Of(1), Of(2), Of(3));
        Assert.Equal([(check, 3, 3)], CheckRounds.SpentOwed(spent, graph.Wires, declared));
        var told = spent with { Told = [new NodeTold(check, 3, "s1", null, Asked) { Spent = true }] };
        Assert.Empty(CheckRounds.SpentOwed(told, graph.Wires, declared));
        Assert.Empty(CheckRounds.SpentOwed(Failed(3, Of(1), Of(2), Of(3) with { State = AttemptStates.Sent }), graph.Wires, declared));
        Assert.Equal([(check, 4, 4)], CheckRounds.SpentOwed(told with { Attempts = [Of(1), Of(2), Of(3), Of(4)],
            Statuses = new Dictionary<string, NodeStatus> { [check] = NodeLifecycle.Start("run:r", check) with { State = NodeStates.Failed, Attempt = 4 } } },
            graph.Wires, declared));
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
