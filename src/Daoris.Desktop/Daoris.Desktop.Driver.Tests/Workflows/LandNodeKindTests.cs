using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDNODE1a (the land node design §2, §6): <c>land</c> is a built-in kind Daoris performs, reached by the landing record; its options are
/// each held to their choices; and the graph rules count it as the landing (one landing, its tail, the refusals of automatic).
/// Declaration and validation only: nothing here performs a landing.
/// </summary>
public sealed class LandNodeKindTests
{
    private static NodeDeclaration Land(string options = """{"method":"merge","accept":"you"}""") => Node("land", NodeKinds.Land, options);

    private static NodeDeclaration Node(string id, string kind, string? options = null)
    {
        var reach = NodeAuthorities.BuiltIn.TryGetValue(kind, out var builtIn) ? builtIn.Reach : NodeReach.Performed;
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

    private static string[] Codes(NodeGraph graph) => [.. NodeGraphs.Problems(graph).Select(problem => problem.Code)];

    [Fact]
    public void Land_is_performed_by_daoris_reached_and_failed_by_the_landing_record_and_never_skipped()
    {
        Assert.Contains(NodeKinds.Land, NodeKinds.BuiltIn);
        Assert.Equal("land", NodeKinds.Land);
        var authority = NodeAuthorities.BuiltIn[NodeKinds.Land];
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
        var older = NodeAuthorities.BuiltIn.Where(pair => pair.Key != NodeKinds.Land).ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.Equal(NodeRefusal.UnknownKind, NodeDeclarations.Problem(Land(), older));
        Assert.Null(NodeAuthorities.Of(Land(), older));
        Assert.Null(NodeDeclarations.Problem(Node("line", NodeKinds.Line), older));
        Assert.Null(NodeDeclarations.Problem(Land()));
    }

    [Theory]
    [InlineData("""{"method":"merge"}""", null)]
    [InlineData("""{"method":"branch","pattern":"feature/{quest}-{slug}"}""", null)]
    [InlineData("""{"method":"push","plugin":"acme.pr"}""", null)]
    [InlineData("""{"method":"pull-request","plugin":"acme.pr","accept":"automatic"}""", null)]
    [InlineData("""{"method":"integrate","target":"release/1","accept":"you"}""", null)]
    [InlineData("""{}""", null)]
    [InlineData("""{"method":"rebase"}""", "method")]
    [InlineData("""{"method":"Merge"}""", "method")]
    [InlineData("""{"method":true}""", "method")]
    [InlineData("""{"accept":"later"}""", "accept")]
    [InlineData("""{"accept":null}""", "accept")]
    [InlineData("""{"target":"has space"}""", "target")]
    [InlineData("""{"target":""}""", "target")]
    [InlineData("""{"method":"branch","pattern":"feature/{nothing}"}""", "pattern")]
    [InlineData("""{"method":"branch","pattern":"fixed-name"}""", "pattern")]
    [InlineData("""{"method":"merge","pattern":"feature/{quest}"}""", "pattern")]
    [InlineData("""{"plugin":"Not A Plugin"}""", "plugin")]
    [InlineData("""{"method":"branch","plugin":"none","pattern":"x/{quest}"}""", null)]
    [InlineData("""{"method":"pull-request"}""", "plugin")]
    [InlineData("""{"method":"pull-request","plugin":"none"}""", "plugin")]
    [InlineData("""{"method":"push"}""", "plugin")]
    [InlineData("""{"method":"integrate","plugin":"acme.pr"}""", "plugin")]
    [InlineData("""{"method":"merge","plugin":"acme.pr"}""", "plugin")]
    [InlineData("""{"method":"merge","squash":true}""", null)]
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
    public void A_graph_holds_one_land_and_no_handed_beside_it()
    {
        var two = Chain(Claimed(), Land(), Node("again", NodeKinds.Land, """{"method":"merge"}"""), Line());
        var beside = Chain(Claimed(), Land(), Node("old", NodeKinds.Handed), Line());

        Assert.Equal([GraphRefusal.LandOnce], Codes(two));
        Assert.Equal("again", NodeGraphs.Problems(two).Single().Node);
        Assert.Equal([GraphRefusal.LandOnce], Codes(beside));
        // A stored graph of two handed is as it was: the rule is the new kind's.
        Assert.Empty(Codes(Chain(Claimed(), Node("a", NodeKinds.Handed), Node("b", NodeKinds.Handed), Line())));
    }

    [Theory]
    [InlineData("merge", "pushed")]
    [InlineData("merge", "pull-request")]
    [InlineData("merge", "integrate")]
    [InlineData("branch", "pushed")]
    [InlineData("branch", "integrate")]
    [InlineData("push", "integrate")]
    [InlineData("pull-request", "integrate")]
    [InlineData("integrate", "pushed")]
    [InlineData("integrate", "pull-request")]
    public void A_land_refuses_a_tail_its_method_does_not_bring(string method, string tail)
    {
        var plugin = method is "push" or "pull-request" ? ",\"plugin\":\"acme.pr\"" : "";
        var land = Land($$"""{"method":"{{method}}"{{plugin}}}""");
        // The method's own tail first, so the only fault is the extra node.
        NodeDeclaration[] own = method switch
        {
            "push" => [Node("p0", NodeKinds.Pushed)],
            "pull-request" => [Node("p0", NodeKinds.Pushed), Node("r0", NodeKinds.PullRequest)],
            "integrate" => [Node("i0", NodeKinds.Integrate)],
            _ => [],
        };
        var extra = Node("extra", tail);
        var graph = Chain([Claimed(), land, .. own.Where(node => node.Kind != tail), extra, Line()]);

        Assert.Contains(GraphRefusal.LandTail, Codes(graph));
        Assert.Contains("extra", NodeGraphs.Problems(graph).Where(problem => problem.Code == GraphRefusal.LandTail).Select(problem => problem.Node));
    }

    [Theory]
    [InlineData("push", "pushed")]
    [InlineData("pull-request", "pushed")]
    [InlineData("pull-request", "pull-request")]
    [InlineData("integrate", "integrate")]
    public void A_land_refuses_a_method_whose_tail_is_missing(string method, string missing)
    {
        var plugin = method is "push" or "pull-request" ? ",\"plugin\":\"acme.pr\"" : "";
        NodeDeclaration[] tail = method switch
        {
            "push" => [Node("p0", NodeKinds.Pushed)],
            "pull-request" => [Node("p0", NodeKinds.Pushed), Node("r0", NodeKinds.PullRequest)],
            _ => [Node("i0", NodeKinds.Integrate)],
        };
        var graph = Chain([Claimed(), Land($$"""{"method":"{{method}}"{{plugin}}}"""), .. tail.Where(node => node.Kind != missing), Node("done", NodeKinds.Line)]);

        var said = NodeGraphs.Problems(graph).Where(problem => problem.Code == GraphRefusal.LandTail).ToList();
        Assert.Equal("land", Assert.Single(said).Node);
    }

    [Fact]
    public void A_pull_request_comes_after_the_push_and_both_after_the_land()
    {
        var land = Land("""{"method":"pull-request","plugin":"acme.pr"}""");
        var right = Chain(Claimed(), land, Node("push", NodeKinds.Pushed), Node("pr", NodeKinds.PullRequest), Line());
        var swapped = Chain(Claimed(), land, Node("pr", NodeKinds.PullRequest), Node("push", NodeKinds.Pushed), Line());

        Assert.Empty(Codes(right));
        Assert.Contains(GraphRefusal.LandTail, Codes(swapped));
    }

    [Fact]
    public void A_push_and_a_branch_hold_with_their_tails()
    {
        Assert.Empty(Codes(Chain(Claimed(), Land("""{"method":"push","plugin":"acme.pr"}"""), Node("push", NodeKinds.Pushed), Line())));
        Assert.Empty(Codes(Chain(Claimed(), Land("""{"method":"branch","pattern":"x/{quest}"}"""), Line())));
    }

    [Theory]
    [InlineData("""{"method":"merge","accept":"automatic"}""", null, true)]
    [InlineData("""{"method":"merge","accept":"you"}""", null, false)]
    [InlineData("""{"method":"branch","accept":"automatic","pattern":"x/{quest}"}""", null, false)]
    [InlineData("""{"method":"pull-request","accept":"automatic","plugin":"acme.pr"}""", null, false)]
    [InlineData("""{"method":"integrate","accept":"automatic"}""", null, true)]
    [InlineData("""{"method":"integrate","accept":"automatic"}""", """{"request":"owner"}""", true)]
    [InlineData("""{"method":"integrate","accept":"automatic"}""", """{"request":"steward"}""", true)]
    [InlineData("""{"method":"integrate","accept":"automatic"}""", """{"request":"done"}""", false)]
    [InlineData("""{"method":"integrate","accept":"you"}""", """{"request":"owner"}""", false)]
    public void Automatic_is_refused_on_a_merge_and_on_an_integrate_but_under_request_done(string land, string? integrate, bool refused)
    {
        var tail = method(land) switch
        {
            "merge" => Chain(Claimed(), Land(land), Line()),
            "integrate" => Chain(Claimed(), Land(land), Node("merge", NodeKinds.Integrate, integrate), Line()),
            "branch" => Chain(Claimed(), Land(land), Line()),
            _ => Chain(Claimed(), Land(land), Node("push", NodeKinds.Pushed), Node("pr", NodeKinds.PullRequest), Line()),
        };

        Assert.Equal(refused, Codes(tail).Contains(GraphRefusal.LandAutomatic));
        Assert.Equal(refused ? [GraphRefusal.LandAutomatic] : [], Codes(tail));

        static string method(string json) => JsonDocument.Parse(json).RootElement.GetProperty("method").GetString()!;
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

    [Fact]
    public void A_handed_graphs_integrate_rules_are_as_they_were()
    {
        var merge = Chain(Claimed(), Node("landing", NodeKinds.Handed, """{"form":"merge"}"""), Node("merge", NodeKinds.Integrate), Line());

        Assert.Equal([GraphRefusal.IntegratePlugin], Codes(merge));
    }
}
