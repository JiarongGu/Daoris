using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// When a <c>rebase</c> may start (<see cref="RebaseWaits"/>, over <see cref="TogetherBinding"/>): a member bound to an open operation never
/// starts; a head waits, said, until its operation is <c>ready</c> at its tip; a session mid-turn holds it, said. And the press's read of a
/// member delivering (§22), and the target its <c>onto</c> names.
/// </summary>
public sealed class RebaseStartTests
{
    private const string Tree = "C:/trees/default/engine/s-41";

    private const string HeadTree = "C:/trees/default/engine/s-40";

    private static TogetherOperation Operation(string phase, string? checkedAt = null) =>
        new("t-1", "engine", new TogetherHead("main", "s-40", HeadTree, "feature/head", "h0"),
            [new TogetherMember("s-41", Tree, "feature/member", "b0", "m1"), new TogetherMember("s-42", "C:/trees/default/engine/s-42", "feature/other", "b0", "o1")],
            phase, DateTimeOffset.UnixEpoch)
        {
            Checked = checkedAt,
            Attempt = phase == TogetherPhase.Landed ? new TogetherAttempt(TogetherForms.Merge, "main", null, "h1", DateTimeOffset.UnixEpoch) : null,
        };

    [Theory]
    [InlineData(TogetherPhase.Asked)]
    [InlineData(TogetherPhase.Replaying)]
    [InlineData(TogetherPhase.Checking)]
    [InlineData(TogetherPhase.Ready)]
    [InlineData(TogetherPhase.Landing)]
    [InlineData(TogetherPhase.Uncertain)]
    public void A_member_bound_to_an_open_operation_never_starts(string phase)
    {
        var hold = TogetherBinding.Of([Operation(phase, "h1")], "engine", "feature/member", Tree, "m1");

        Assert.Equal(TogetherHolds.Member, hold!.How);
        Assert.Equal("Waits for `s-40`'s Accept, which brings this work in with it.", RebaseWaits.Of(turning: false, hold));
    }

    [Theory]
    [InlineData(TogetherPhase.Asked, "Waits for `s-41` and `s-42` to be brought in: asked.")]
    [InlineData(TogetherPhase.Replaying, "Waits for `s-41` and `s-42` to be brought in: replaying.")]
    [InlineData(TogetherPhase.Checking, "Waits for `s-41` and `s-42` to be brought in: checking.")]
    public void A_head_before_ready_stays_ready_said(string phase, string said)
    {
        var hold = TogetherBinding.Of([Operation(phase)], "engine", "feature/head", HeadTree, "h1");

        Assert.Equal(TogetherHolds.HeadBefore, hold!.How);
        Assert.Equal(said, RebaseWaits.Of(turning: false, hold));
    }

    [Fact]
    public void A_head_starts_at_ready_at_its_tip_and_waits_where_its_tip_moved_since()
    {
        Assert.Null(RebaseWaits.Of(turning: false, TogetherBinding.Of([Operation(TogetherPhase.Ready, "h1")], "engine", "feature/head", HeadTree, "h1")));
        var moved = TogetherBinding.Of([Operation(TogetherPhase.Ready, "h1")], "engine", "feature/head", HeadTree, "h2");
        Assert.Equal(TogetherHolds.HeadBefore, moved!.How);
        Assert.StartsWith("Waits for `s-41` and `s-42` to be brought in: its tip moved", RebaseWaits.Of(turning: false, moved));
        Assert.Equal(TogetherHolds.HeadLanding, TogetherBinding.Of([Operation(TogetherPhase.Landing, "h1")], "engine", "feature/head", HeadTree, "h1")!.How);
    }

    [Fact]
    public void A_session_mid_turn_holds_it_ready_said_and_a_landed_operation_holds_nothing()
    {
        Assert.Equal("Waits for its session's turn to end.", RebaseWaits.Of(turning: true, null));
        Assert.Null(TogetherBinding.Of([Operation(TogetherPhase.Landed, "h1")], "engine", "feature/member", Tree, "m1"));
        Assert.Equal("m1", TogetherBinding.BroughtIn([Operation(TogetherPhase.Landed, "h1")], "engine", "feature/member", Tree));
        Assert.Null(TogetherBinding.BroughtIn([Operation(TogetherPhase.Ready, "h1")], "engine", "feature/member", Tree));
    }

    /// <summary>
    /// §22: a member whose <c>rebase</c> reached at its tip while a step its commit feeds has not settled at that commit is delivering, refused by
    /// the press, said; once that step settled at the commit it may be bound. A member with an unsettled delivery is refused the same.
    /// </summary>
    [Fact]
    public void A_member_whose_rebase_reached_is_delivering_until_what_its_commit_feeds_settles()
    {
        NodeDeclaration[] nodes = [new("rebase", NodeKinds.Rebase, 1, NodeReach.Performed), new("merge", "acme.git/merge", 1, NodeReach.Performed)];
        Wire[] wires = [WireEvaluationTests.Data("w1", "rebase", RebaseNode.Commit, "merge", "commit")];
        var reached = new NodeStatus("run:q1", "rebase", NodeStates.Reached)
        {
            Acceptance = new NodeAcceptance("run:q1", "m1", "g", "rebase", 1, 1)
            {
                Outputs = new Dictionary<string, NodePort>(StringComparer.Ordinal) { [RebaseNode.Commit] = new(WireValues.Commit, "refs/daoris/steps/q1/rebase/1", "c1", null) },
            },
        };
        Dictionary<string, NodeStatus> waiting = new(StringComparer.Ordinal) { ["rebase"] = reached, ["merge"] = new NodeStatus("run:q1", "merge", NodeStates.Active) };
        Dictionary<string, NodeStatus> settled = new(StringComparer.Ordinal)
        {
            ["rebase"] = reached,
            ["merge"] = new NodeStatus("run:q1", "merge", NodeStates.Reached) { Acceptance = new NodeAcceptance("run:q1", "c1", "g", "merge", 1, 1) },
        };

        Assert.Equal("`s-41` is delivering: bring it in once its merge settles", RebaseDelivering.Of("s-41", (nodes, wires, waiting), "m1", deliveryUnsettled: false));
        Assert.Null(RebaseDelivering.Of("s-41", (nodes, wires, settled), "m1", deliveryUnsettled: false));
        Assert.Null(RebaseDelivering.Of("s-41", (nodes, wires, waiting), "m2", deliveryUnsettled: false));
        Assert.Equal("`s-41` is delivering: bring it in once its delivery settles", RebaseDelivering.Of("s-41", null, "m1", deliveryUnsettled: true));
    }

    [Fact]
    public void Its_target_is_the_line_by_default_or_a_branch_by_name()
    {
        var node = new NodeDeclaration("rebase", NodeKinds.Rebase, 1, NodeReach.Performed);

        Assert.Equal("refs/heads/main", RebaseOnto.Of(node, "main"));
        Assert.Null(RebaseOnto.Of(node, null));
        Assert.Equal("refs/heads/feature/chain", RebaseOnto.Of(node with { Options = new Dictionary<string, string>(StringComparer.Ordinal) { ["onto"] = "\"feature/chain\"" } }, "main"));
        Assert.True(NodeKit.Built.IsArriving(NodeKinds.Rebase));
    }
}
