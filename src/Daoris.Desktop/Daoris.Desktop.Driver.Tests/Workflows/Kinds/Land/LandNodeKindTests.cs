using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>land</c> is a built-in kind Daoris performs, reached by the landing record; its options are
/// each held to their choices; and the graph rules count it as the landing (one landing, its tail, the refusals of automatic).
/// Declaration and validation only: nothing here performs a landing.
/// </summary>
public sealed class LandNodeKindTests
{
    private static NodeDeclaration Land(string options = """{"method":"merge"}""") => Node("land", NodeKinds.Land, options);

    private static NodeDeclaration Node(string id, string kind, string? options = null)
    {
        var reach = NodeKit.Row(kind)?.Reach ?? NodeReach.Performed;
        return new NodeDeclaration(id, kind, 1, reach)
        {
            Options = options is null ? new Dictionary<string, string>()
                : JsonDocument.Parse(options).RootElement.EnumerateObject().ToDictionary(option => option.Name, option => option.Value.GetRawText()),
        };
    }

    /// <summary>The nodes in order, each after the one before, the first the entry and the last the end.</summary>
    private static NodeGraph Chain(params NodeDeclaration[] nodes) => new(
        nodes, [.. nodes.Skip(1).Select((node, at) => new Wire($"{nodes[at].Id}>{node.Id}", WireKinds.After, new(nodes[at].Id), new(node.Id)))],
        [nodes[0].Id], nodes[^1].Id);

    private static string[] Codes(NodeGraph graph) => [.. NodeGraphs.Problems(graph).Where(GraphProblemsTable.IsRule).Select(problem => problem.Code)];

    [Fact]
    public void Land_is_performed_by_daoris_reached_and_failed_by_the_landing_record_and_never_skipped()
    {
        Assert.Contains(NodeKinds.Land, NodeKit.Kinds);
        Assert.Equal("land", NodeKinds.Land);
        var authority = NodeKit.Row(NodeKinds.Land)!;
        Assert.Equal((NodeReach.Performed, NodeSource.Landing, NodeSource.Landing), (authority.Reach, authority.ReachBy, authority.FailureBy));
        Assert.Equal((false, false, true, true), (authority.PersonJudges, authority.PersonSkips, authority.MayEnd, authority.Evidenced));
        Assert.Null(authority.SkipBy);
        Assert.False(authority.OnPluginWord);
        Assert.Null(NodeDeclarations.Problem(Land()));
        Assert.Equal("reach", NodeDeclarations.Problem(Land() with { Reach = NodeReach.Observed }));
        Assert.Equal("rejectable", NodeDeclarations.Problem(Land() with { Rejectable = true }));
        Assert.Equal("skippable", NodeDeclarations.Problem(Land() with { Skippable = false }));
    }

    /// <summary>An older build's table has no <c>land</c> row, so it reads the kind as none it knows and holds the graph whole.</summary>
    [Fact]
    public void An_older_build_whose_table_omits_land_reads_it_as_no_kind_and_holds_the_graph_whole()
    {
        var older = NodeKit.Built.Without(NodeKinds.Land);

        Assert.Equal(NodeRefusal.UnknownKind, NodeDeclarations.Problem(Land(), older));
        Assert.Null(NodeAuthorities.Of(Land(), older));
        Assert.Null(NodeDeclarations.Problem(Node("line", NodeKinds.Line), older));
        Assert.Null(NodeDeclarations.Problem(Land()));
    }

    [Theory]
    [InlineData("""{"method":"merge"}""", null)]
    [InlineData("""{"method":"branch","pattern":"feature/{quest}-{slug}"}""", null)]
    // A land names no plugin and is none of a push or a pull request; those are the steps after a branch landing.
    [InlineData("""{"method":"branch","plugin":"acme.pr","pattern":"x/{quest}"}""", "plugin")]
    [InlineData("""{"method":"pull-request","plugin":"acme.pr"}""", "method")]
    [InlineData("""{"method":"integrate","target":"release/1"}""", null)]
    [InlineData("""{}""", null)]
    [InlineData("""{"method":"rebase"}""", "method")]
    [InlineData("""{"method":"Merge"}""", "method")]
    [InlineData("""{"method":true}""", "method")]
    // The Accept is a `review` before the land, so a land saved with the option is held rather than landed with no press.
    [InlineData("""{"method":"merge","accept":"you"}""", "accept")]
    [InlineData("""{"method":"branch","accept":"automatic","pattern":"x/{quest}"}""", "accept")]
    [InlineData("""{"accept":null}""", "accept")]
    // Only an integrate may name a line other than the tree's; the others land on the tree's line, which a target would only misname.
    [InlineData("""{"method":"merge","target":"release"}""", "target")]
    [InlineData("""{"method":"branch","target":"release","pattern":"x/{quest}"}""", "target")]
    [InlineData("""{"target":"has space"}""", "target")]
    [InlineData("""{"target":""}""", "target")]
    [InlineData("""{"method":"branch","pattern":"feature/{nothing}"}""", "pattern")]
    [InlineData("""{"method":"branch","pattern":"fixed-name"}""", "pattern")]
    [InlineData("""{"method":"merge","pattern":"feature/{quest}"}""", "pattern")]
    [InlineData("""{"plugin":"Not A Plugin"}""", "plugin")]
    [InlineData("""{"method":"branch","plugin":"none","pattern":"x/{quest}"}""", "plugin")]
    [InlineData("""{"method":"pull-request"}""", "method")]
    [InlineData("""{"method":"pull-request","plugin":"none"}""", "method")]
    [InlineData("""{"method":"push"}""", "method")]
    [InlineData("""{"method":"integrate","plugin":"acme.pr"}""", "plugin")]
    [InlineData("""{"method":"merge","plugin":"acme.pr"}""", "plugin")]
    [InlineData("""{"method":"merge","squash":true}""", "squash")]
    [InlineData("""{"method":"merge","squash":false}""", null)]
    [InlineData("""{"method":"branch","squash":false,"pattern":"x/{quest}"}""", null)]
    [InlineData("""{"method":"integrate","squash":true}""", "squash")]
    [InlineData("""{"squash":"yes"}""", "squash")]
    [InlineData("""{"message":"{title} ({quest}) {ticket} {session}"}""", null)]
    [InlineData("""{"message":"Land {slug}"}""", "message")]
    [InlineData("""{"message":"   "}""", "message")]
    [InlineData("""{"message":3}""", "message")]
    [InlineData("""{"later":"anything"}""", null)]
    public void A_lands_options_are_each_held_to_their_choices(string options, string? problem)
    {
        Assert.Equal(problem, NodeDeclarations.Problem(Land(options)));
    }

    private static NodeDeclaration Claimed() => Node("work", NodeKinds.Claimed);

    private static NodeDeclaration Line() => Node("line", NodeKinds.Line);

    [Fact]
    public void A_land_holds_as_the_graphs_one_landing()
    {
        Assert.Empty(Codes(Chain(Claimed(), Land(), Line())));
        Assert.Empty(Codes(Chain(Claimed(), Land("""{"method":"integrate"}"""), Node("merge", NodeKinds.Integrate), Line())));
    }

    [Fact]
    public void A_graph_holds_one_land()
    {
        var two = Chain(Claimed(), Land(), Node("again", NodeKinds.Land, """{"method":"merge"}"""), Line());

        Assert.Equal([GraphRefusal.LandOnce], Codes(two));
        Assert.Equal("again", NodeGraphs.Problems(two).Where(GraphProblemsTable.IsRule).Single().Node);
    }

    [Theory]
    [InlineData("merge", "push")]
    [InlineData("merge", "integrate")]
    [InlineData("branch", "integrate")]
    [InlineData("integrate", "push")]
    public void A_land_refuses_a_tail_its_method_does_not_bring(string method, string tail)
    {
        var land = Land($$"""{"method":"{{method}}"}""");
        // The method's own tail first, so the only fault is the extra node.
        NodeDeclaration[] own = method switch
        {
            "integrate" => [Node("i0", NodeKinds.Integrate)],
            _ => [],
        };
        var extra = Node("extra", tail);
        var graph = Chain([Claimed(), land, .. own.Where(node => node.Kind != tail), extra, Line()]);

        Assert.Contains(GraphRefusal.LandTail, Codes(graph));
        Assert.Contains("extra", NodeGraphs.Problems(graph).Where(problem => problem.Code == GraphRefusal.LandTail).Select(problem => problem.Node));
    }

    [Theory]
    [InlineData("integrate", "integrate")]
    public void A_land_refuses_a_method_whose_tail_is_missing(string method, string missing)
    {
        NodeDeclaration[] tail = [Node("i0", NodeKinds.Integrate)];
        var graph = Chain([Claimed(), Land($$"""{"method":"{{method}}"}"""), .. tail.Where(node => node.Kind != missing), Node("done", NodeKinds.Line)]);

        var said = NodeGraphs.Problems(graph).Where(problem => problem.Code == GraphRefusal.LandTail).ToList();
        Assert.Equal("land", Assert.Single(said).Node);
    }

    /// <summary>A pull request is a branch, then a push, then a plugin's performed step; a push stands after the land that made its branch.</summary>
    [Fact]
    public void A_push_and_a_plugins_pull_request_follow_a_branch_landing_in_order()
    {
        var land = Land("""{"method":"branch"}""");
        var pr = Node("pr", "acme-pr/pull-request");
        Assert.Empty(Codes(Chain(Claimed(), land, Node("push", NodeKinds.Push), pr, Line())));
        Assert.Empty(Codes(Chain(Claimed(), Land("""{"method":"branch","pattern":"x/{quest}"}"""), Line())));
        Assert.Contains(GraphRefusal.LandTail, Codes(Chain(Claimed(), Land("""{"method":"merge"}"""), Node("push", NodeKinds.Push), Line())));
    }

    /// <summary>A land names no plugin, and no method of it pushes or opens a pull request.</summary>
    [Theory]
    [InlineData("""{"method":"push"}""")]
    [InlineData("""{"method":"pull-request"}""")]
    [InlineData("""{"method":"branch","plugin":"none"}""")]
    [InlineData("""{"method":"branch","plugin":"acme.pr"}""")]
    public void A_land_refuses_the_old_methods_and_its_plugin_option(string options)
    {
        Assert.NotNull(NodeDeclarations.Problem(Land(options)));
    }

    /// <summary>
    /// A land with no review before it lands with no press, a merge and an integrate under any request alike; one with a review
    /// waits for the person's Accept, which the review is wired straight into the land to hold.
    /// </summary>
    [Theory]
    [InlineData("""{"method":"merge"}""", null)]
    [InlineData("""{"method":"branch","pattern":"x/{quest}"}""", null)]
    [InlineData("""{"method":"integrate"}""", """{"request":"owner"}""")]
    [InlineData("""{"method":"integrate"}""", """{"request":"steward"}""")]
    [InlineData("""{"method":"integrate"}""", """{"request":"done"}""")]
    public void A_land_with_or_without_a_review_before_it_holds_whatever_its_method(string land, string? integrate)
    {
        NodeDeclaration[] tail = JsonDocument.Parse(land).RootElement.GetProperty("method").GetString() switch
        {
            "integrate" => [Node("merge", NodeKinds.Integrate, integrate), Line()],
            _ => [Line()],
        };

        Assert.Empty(Codes(Chain([Claimed(), Land(land), .. tail])));
        Assert.Empty(Codes(Chain([Claimed(), Node("review", NodeKinds.Review), Land(land), .. tail])));
    }

    [Fact]
    public void A_review_is_the_persons_accept_before_every_landing_and_never_skipped()
    {
        var authority = NodeKit.Row(NodeKinds.Review)!;
        Assert.Equal((NodeReach.Pressed, NodeSource.Person, (string?)null), (authority.Reach, authority.ReachBy, authority.FailureBy));
        Assert.Equal((false, true, false), (authority.PersonSkips, authority.MayEnd, authority.Evidenced));

        var review = Node("review", NodeKinds.Review);
        Assert.Empty(Codes(Chain(Claimed(), Node("requests", NodeKinds.WorkRequests), review)));
        Assert.Contains(GraphRefusal.HoldLate, Codes(new NodeGraph(
            [Claimed(), Node("requests", NodeKinds.WorkRequests), review],
            [new("a", WireKinds.After, new("work"), new("review")), new("b", WireKinds.After, new("work"), new("requests"))],
            ["work"], "review")));
        var beside = new NodeGraph(
            [Claimed(), review, Land(), Line()],
            [new("a", WireKinds.After, new("work"), new("review")), new("b", WireKinds.After, new("work"), new("land")), new("c", WireKinds.After, new("land"), new("line"))],
            ["work"], "line");
        Assert.Contains(GraphRefusal.ProtectedOrder, Codes(beside));
        Assert.Contains(GraphRefusal.ProtectedWire, Codes(Chain(Claimed(), review, Node("qa", NodeKinds.Human), Land(), Line())));

        var older = NodeKit.Built.Without(NodeKinds.Review);
        Assert.Equal(NodeRefusal.UnknownKind, NodeDeclarations.Problem(review, older));
    }

    [Fact]
    public void An_integrate_needs_a_landing_and_a_land_is_one()
    {
        Assert.Contains(GraphRefusal.IntegrateUnlanded, Codes(Chain(Claimed(), Node("merge", NodeKinds.Integrate))));
        Assert.DoesNotContain(GraphRefusal.IntegrateUnlanded, Codes(Chain(Claimed(), Land("""{"method":"integrate"}"""), Node("merge", NodeKinds.Integrate))));
    }

    [Fact]
    public void A_node_that_observes_the_landing_needs_a_way_that_passes_the_land()
    {
        var around = new NodeGraph(
            [Claimed(), Land(), Line()],
            [new("a", WireKinds.After, new("work"), new("land")), new("b", WireKinds.After, new("work"), new("line")), new("c", WireKinds.After, new("land"), new("line"))],
            ["work"], "line");

        Assert.Contains(GraphRefusal.BeforeLanding, Codes(around));
        Assert.DoesNotContain(GraphRefusal.BeforeLanding, Codes(Chain(Claimed(), Land(), Line())));
    }

    [Fact]
    public void The_work_requests_hold_comes_before_the_land()
    {
        var requests = Node("requests", NodeKinds.WorkRequests);
        // The land is an entry, so it can start before the hold is reached.
        var late = new NodeGraph(
            [Claimed(), requests, Land(), Line()],
            [new("a", WireKinds.After, new("work"), new("requests")), new("b", WireKinds.After, new("land"), new("line"))],
            ["work", "land"], "line");

        Assert.Contains(GraphRefusal.WorkRequestsLate, Codes(late));
        Assert.Empty(Codes(Chain(Claimed(), requests, Land(), Line())));
    }

    [Fact]
    public void The_look_and_the_opinion_come_before_the_land_in_the_protected_order()
    {
        var look = Node("look", NodeKinds.Look);
        var beside = new NodeGraph(
            [Claimed(), look, Land(), Line()],
            [new("a", WireKinds.After, new("work"), new("look")), new("b", WireKinds.After, new("work"), new("land")), new("c", WireKinds.After, new("land"), new("line"))],
            ["work"], "line");
        var distant = Chain(Claimed(), look, Node("qa", NodeKinds.Human), Land(), Line());

        Assert.Contains(GraphRefusal.ProtectedOrder, Codes(beside));
        Assert.Contains(GraphRefusal.ProtectedWire, Codes(distant));
        Assert.Empty(Codes(Chain(Claimed(), Node("opinion", NodeKinds.Opinion), look, Land(), Line()))
            .Where(code => code is GraphRefusal.ProtectedOrder));
    }

    [Fact]
    public void A_land_before_the_line_is_not_a_cleanup_wait()
    {
        var graph = Chain(Claimed(), Land()) with { Cleanup = new(GraphCleanupUntil.End, 7) };

        Assert.Contains(GraphRefusal.Cleanup, Codes(graph));
    }

}
