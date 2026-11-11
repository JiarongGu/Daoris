using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The save's rules for the steps writing the work: each comes after the review where one exists and is fed the last review on every way to
/// it, and a graph ends anywhere no such step lies past its end, so a graph landing nothing may go on after its review.
/// </summary>
public sealed class EffectOrderTests
{
    /// <summary>A graph from the claim, read as a save reads it: refused, it reads no graph and its problems.</summary>
    private static (NodeGraph? Graph, IReadOnlyList<WireProblem> Problems) Graph(string end, string nodes, string wires) =>
        NodeGraphs.Read($$"""{"schemaVersion":2,"entries":["work"],"end":"{{end}}","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{{nodes}}],"wires":[{{wires}}]}""");

    private const string Review = """{"id":"review","kind":"review","version":1,"reach":"pressed"}""";

    private const string Apply = """{"id":"apply","kind":"apply","version":1,"reach":"performed"}""";

    private const string Receipt = """{"id":"receipt","kind":"acme.deploy/deploy","version":1,"reach":"performed"}""";

    private static string After(string from, string to) => $$"""{"id":"{{from}}>{{to}}","kind":"after","from":"{{from}}","to":"{{to}}"}""";

    private static IReadOnlyList<string> Codes((NodeGraph? Graph, IReadOnlyList<WireProblem> Problems) read) => [.. read.Problems.Select(problem => problem.Code)];

    [Theory]
    [InlineData("plan-review-apply")]
    [InlineData("in-copy")]
    [InlineData("in-place")]
    public void The_shipped_graphs_landing_nothing_save_and_their_effect_is_fed_their_review(string id)
    {
        var graph = NodeGraphs.Read(WorkflowDefaults.Of(id)!.GraphJson).Graph!;

        Assert.Empty(NodeGraphs.Problems(graph));
        foreach (var effect in graph.Nodes.Where(node => node.Kind is NodeKinds.Apply or NodeKinds.Return)) Assert.Equal("review", EffectOrder.Fed(graph, effect.Id));
    }

    [Fact]
    public void A_review_then_an_apply_then_a_plugins_step_saves_ending_at_the_step()
    {
        var graph = Graph("receipt", $"{Review},{Apply},{Receipt}", $"{After("work", "review")},{After("review", "apply")},{After("apply", "receipt")}");

        Assert.Empty(Codes(graph));
    }

    [Fact]
    public void An_effect_a_way_reaches_around_the_review_is_refused()
    {
        var graph = Graph("receipt", $"{Review},{Apply},{Receipt}", $"{After("work", "review")},{After("review", "receipt")},{After("work", "apply")}");

        Assert.Contains(GraphRefusal.ReviewOrder, Codes(graph));
    }

    [Fact]
    public void An_effect_two_reviews_feed_by_ways_of_their_own_is_refused()
    {
        var second = Review.Replace("\"id\":\"review\"", "\"id\":\"second\"", StringComparison.Ordinal);
        var graph = Graph("apply", $"{Review},{second},{Apply}", $"{After("work", "review")},{After("work", "second")},{After("review", "apply")},{After("second", "apply")}");

        Assert.Contains(GraphRefusal.ReviewAmbiguous, Codes(graph));
    }

    [Fact]
    public void An_effect_past_the_graphs_end_is_refused()
    {
        var graph = Graph("review", $"{Review},{Apply}", $"{After("work", "review")},{After("review", "apply")}");

        Assert.Contains(GraphRefusal.EffectPastEnd, Codes(graph));
    }

    [Fact]
    public void A_graph_landing_nothing_may_end_at_any_step_no_effect_lies_past()
    {
        var graph = Graph("receipt", Receipt, After("work", "receipt"));

        Assert.Empty(Codes(graph));
    }
}
