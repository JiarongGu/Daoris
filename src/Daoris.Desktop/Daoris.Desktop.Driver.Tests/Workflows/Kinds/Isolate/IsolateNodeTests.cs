using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The <c>isolate</c> step: its options judged (the kind a place can make, a count of two to twenty-four or any, and the overlap's go or
/// wait), read into the step the start carries, the graph's one entry wired straight into its work, and a graph with none read as in place.
/// </summary>
public sealed class IsolateNodeTests
{
    private static NodeDeclaration Step(params (string Name, string Json)[] options) =>
        new("apart", NodeKinds.Isolate, 1, NodeReach.Observed) { Options = options.ToDictionary(each => each.Name, each => each.Json, StringComparer.Ordinal) };

    private static readonly NodeDeclaration Work = new("work", NodeKinds.Claimed, 1, NodeReach.Observed);

    private static NodeGraph Graph(IReadOnlyList<string> entries, params NodeDeclaration[] nodes) =>
        new(nodes, nodes.Any(node => node.Id == "apart") ? [new Wire("a", WireKinds.After, new WireEnd("apart"), new WireEnd("work"))] : [], entries, "work");

    [Theory]
    [InlineData(IsolateStep.KindOption, "\"auto\"", null)]
    [InlineData(IsolateStep.KindOption, "\"worktree\"", null)]
    [InlineData(IsolateStep.KindOption, "\"copy\"", null)]
    [InlineData(IsolateStep.KindOption, "\"clone\"", IsolateStep.KindOption)]
    [InlineData(IsolateStep.AtOnceOption, "\"any\"", null)]
    [InlineData(IsolateStep.AtOnceOption, "\"3\"", null)]
    [InlineData(IsolateStep.AtOnceOption, "\"24\"", null)]
    [InlineData(IsolateStep.AtOnceOption, "\"1\"", IsolateStep.AtOnceOption)]
    [InlineData(IsolateStep.AtOnceOption, "\"25\"", IsolateStep.AtOnceOption)]
    [InlineData(IsolateStep.AtOnceOption, "3", null)]
    [InlineData(IsolateStep.AtOnceOption, "\"three\"", IsolateStep.AtOnceOption)]
    [InlineData(IsolateStep.OverlapOption, "\"wait\"", null)]
    [InlineData(IsolateStep.OverlapOption, "\"warn\"", IsolateStep.OverlapOption)]
    public void Its_options_are_judged_by_the_kind(string option, string json, string? refused) =>
        Assert.Equal(refused, NodeKit.Of(NodeKinds.Isolate)!.Problem(Step((option, json))));

    [Fact]
    public void Its_options_read_into_the_step_the_start_carries_an_unwritten_one_as_its_default()
    {
        Assert.Equal(new IsolateStep(IsolateStep.Auto, null, false), IsolateSteps.Of(Step()));
        Assert.Equal(new IsolateStep(IsolateStep.Worktree, 3, true),
            IsolateSteps.Of(Step((IsolateStep.KindOption, "\"worktree\""), (IsolateStep.AtOnceOption, "\"3\""), (IsolateStep.OverlapOption, "\"wait\""))));
        Assert.Equal(IsolateStep.Trees, IsolateSteps.Of(IsolateSteps.Declaration(IsolateStep.Trees, "apart")));
    }

    [Fact]
    public void A_graph_with_no_isolate_step_is_read_as_in_place()
    {
        Assert.Null(IsolateSteps.In(Graph(["work"], Work)));
        Assert.Null(IsolateSteps.In(null));
        Assert.Equal(new IsolateStep(IsolateStep.Auto, 2, false), IsolateSteps.In(Graph(["apart"], Step((IsolateStep.AtOnceOption, "\"2\"")), Work)));
    }

    [Fact]
    public void It_is_the_graphs_one_entry_wired_straight_into_its_work()
    {
        var kind = NodeKit.Of(NodeKinds.Isolate)!;
        var entry = Graph(["apart"], Step(), Work);
        Assert.Empty(kind.Problems(entry, entry.Nodes[0]));

        var beside = Graph(["apart", "work"], Step(), Work);
        var refused = Assert.Single(kind.Problems(beside, beside.Nodes[0]));
        Assert.Equal(GraphRefusal.IsolateEntry, refused.Code);

        var unwired = new NodeGraph([Step(), Work], [], ["apart"], "work");
        Assert.Equal(GraphRefusal.IsolateEntry, Assert.Single(kind.Problems(unwired, unwired.Nodes[0])).Code);
    }

    [Fact]
    public void It_is_reached_on_the_ledgers_take_and_never_ends_a_graph()
    {
        var row = NodeKit.Row(NodeKinds.Isolate)!;
        Assert.Equal((NodeSource.Ledger, false), (row.ReachBy, row.MayEnd));
    }

    [Fact]
    public void It_comes_before_the_work_in_the_editors_first_group_and_waits_on_its_count_and_overlap()
    {
        Assert.Equal([NodeKinds.Isolate, NodeKinds.Claimed], NodeKit.Built.Order.Take(2).Select(part => part.Name));
        Assert.Equal(["at-once", "overlap"], Assert.IsAssignableFrom<BuiltInNode>(NodeKit.Of(NodeKinds.Isolate)).Waits);
        Assert.Equal("Work apart", NodeKit.Of(NodeKinds.Isolate)!.Title);
    }
}
