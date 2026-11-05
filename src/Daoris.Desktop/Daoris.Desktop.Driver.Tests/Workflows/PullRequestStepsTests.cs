using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// G8's pull-request step (D241 point 4): the plugin a step id names, the address its evidence gives, the bind's refusal of a graph whose pull
/// request no plugin serves, and the landing record its address is kept on.
/// </summary>
public sealed class PullRequestStepsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "pr-steps", Guid.NewGuid().ToString("N")[..8]);

    public PullRequestStepsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static NodeGraph Graph(params string[] kinds)
    {
        var nodes = kinds.Select(kind => new NodeDeclaration(kind.Replace("example.github/", ""), kind, 1, NodeReach.Performed)).ToArray();
        return new NodeGraph(nodes, [], [nodes[0].Id], nodes[^1].Id);
    }

    [Theory]
    [InlineData("example.github/pull-request", "example.github")]
    [InlineData("acme.devops/pull-request", "acme.devops")]
    [InlineData("pull-request", null)]
    [InlineData("/pull-request", null)]
    [InlineData("example.github/deploy", null)]
    public void A_pull_request_step_names_its_plugin(string kind, string? plugin) => Assert.Equal(plugin, PullRequestSteps.PluginOf(kind));

    [Theory]
    [InlineData("https://example.test/pull/7", true)]
    [InlineData("http://example.test/pull/7", true)]
    [InlineData("ftp://example.test/7", false)]
    [InlineData("pull/7", false)]
    [InlineData(null, false)]
    public void Only_a_web_address_is_a_pull_request(string? address, bool is_address) => Assert.Equal(is_address, PullRequestSteps.IsAddress(address));

    [Fact]
    public void A_graph_whose_pull_request_has_no_plugin_is_refused_at_the_bind_and_one_with_it_is_not()
    {
        var graph = Graph(NodeKinds.Land, NodeKinds.Push, "example.github/pull-request");

        var none = PullRequestSteps.Unserved(graph, [], "ship", 1);
        Assert.NotNull(none);
        Assert.Equal(BindingProblem.PullRequestUnserved, none.Code);
        Assert.Equal(
            new Dictionary<string, string> { ["workflow"] = "ship", ["version"] = "1", ["step"] = "pull-request", ["plugin"] = "example.github" },
            none.Values);
        Assert.Contains("example.github", none.Said);
        Assert.Contains("daoris plugin list", none.Said);

        // A plugin on but speaking on no perform point serves no step.
        Assert.NotNull(PullRequestSteps.Unserved(graph, [new WorkflowPlugin("example.github", [HookPoints.State])], "ship", 1));

        Assert.Null(PullRequestSteps.Unserved(graph, [new WorkflowPlugin("EXAMPLE.GITHUB", [HookPoints.Perform, HookPoints.State])], "ship", 1));
    }

    [Fact]
    public void A_graph_with_no_pull_request_step_is_never_refused_for_a_plugin() =>
        Assert.Null(PullRequestSteps.Unserved(Graph(NodeKinds.Land, NodeKinds.Push), [], "ship", 1));

    [Fact]
    public void A_pull_request_steps_evidence_address_is_its_reference_and_another_steps_is_not()
    {
        var step = new NodeDeclaration("pull-request", "example.github/pull-request", 1, NodeReach.Performed);
        var other = new NodeDeclaration("deploy", "example.github/deploy", 1, NodeReach.Performed);
        var said = new WorkEvidence(null, "https://example.test/pull/7", null, null);

        Assert.Equal("https://example.test/pull/7", WorkBelief.Evidence(step, "inv-1", "abc", said)!.Ref);
        Assert.Equal("inv-1", WorkBelief.Evidence(other, "inv-1", "abc", said)!.Ref);
        Assert.Equal("inv-1", WorkBelief.Evidence(step, "inv-1", "abc", new WorkEvidence(null, "pull/7", null, null))!.Ref);
    }

    [Fact]
    public void A_pull_request_is_kept_on_the_standing_landing_once_with_its_plugin()
    {
        var landings = new LandedBranches(_home);
        landings.Record(new LandedBranch("engine", "work", "feature/q1-a", "main", "abc1234", "s1a2b3c4", "q1", "A", DateTimeOffset.UnixEpoch));
        var records = new PullRequestRecords(_home);

        Assert.True(records.Keep("engine", "feature/q1-a", "example.github", "https://example.test/pull/7"));
        var kept = landings.Of("engine", "feature/q1-a")!;
        Assert.Equal("https://example.test/pull/7", kept.PullRequest);
        Assert.Equal("example.github", kept.Plugin);

        Assert.False(records.Keep("engine", "feature/q1-a", "example.github", "https://example.test/pull/7"));
        Assert.False(records.Keep("engine", "feature/none", "example.github", "https://example.test/pull/8"));
    }
}
