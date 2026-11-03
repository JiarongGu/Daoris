using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>workflow</c> kind's writer (WORKFLOW1h-a, D157, design §8.1): a whole version of a workflow for a repository or a
/// workspace, and optionally its choice. The box judges the shape only; the driver judges the rest. Since WORKFLOWCLEAN1 1b the version is its
/// graph, and a list of steps is refused.
/// </summary>
public sealed class HelpWorkflowProposalTests : HelpProposalBoxFixture
{
    private const string Steps = """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","accept":"automatic"}]""";

    private const string Graph =
        """{"schemaVersion":2,"entries":["work"],"end":"land","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"land","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],"wires":[{"id":"a","kind":"after","from":"work","to":"land"}]}""";

    [Fact]
    public void A_new_workflow_is_written_with_its_graph_as_nested_json()
    {
        var (id, message) = Box().ProposeWorkflow(" web-app ", "docs-to-pr", null, null, false, null, null, null, "the person asked", "h1", Now, graph: Graph);

        var written = Written(id!);
        Assert.Equal("workflow", written.GetProperty("kind").GetString());
        Assert.Equal("web-app", written.GetProperty("scope").GetString());
        Assert.Equal("docs-to-pr", written.GetProperty("workflow").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("base").ValueKind);
        Assert.False(written.GetProperty("choose").GetBoolean());
        Assert.False(written.TryGetProperty("steps", out _));
        var nodes = written.GetProperty("graph").GetProperty("nodes");
        Assert.Equal(["work", "land"], nodes.EnumerateArray().Select(node => node.GetProperty("id").GetString()));
        Assert.Equal("the person asked", written.GetProperty("why").GetString());
        Assert.Contains($"Proposed `#{id}`", message);
    }

    /// <summary>WORKFLOWCLEAN1 1b: a list of steps is no longer proposed, alone or beside a graph, and nothing is written.</summary>
    [Theory]
    [InlineData(Steps, null)]
    [InlineData(Steps, Graph)]
    public void A_list_of_steps_is_refused_with_nothing_written(string steps, string? graph)
    {
        var (id, message) = Box().ProposeWorkflow("web-app", "docs-to-pr", null, steps, false, null, null, null, "why", "h1", Now, graph: graph);

        Assert.Null(id);
        Assert.Contains("a list of steps (`steps`) is no longer proposed: send the whole `graph`", message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    [Fact]
    public void An_edit_names_its_base_and_may_choose_the_workflow()
    {
        var (id, _) = Box().ProposeWorkflow("web-app", "docs-to-pr", 3, null, true, null, null, null, "why", "h1", Now, graph: Graph);

        var written = Written(id!);
        Assert.Equal(3, written.GetProperty("base").GetInt32());
        Assert.True(written.GetProperty("choose").GetBoolean());
    }

    /// <summary>FABRIC-PIPELINE1i: a verification pipeline's graph is written as nested JSON, beside the route it is for.</summary>
    [Fact]
    public void A_graph_is_written_with_its_route()
    {
        const string graph = """{"schemaVersion":2,"entries":["gates"],"end":"gates","nodes":[{"id":"gates","kind":"check","version":1,"reach":"performed"}],"wires":[]}""";
        var (id, _) = Box().ProposeWorkflow("web-app", "line-verify", null, null, false, null, null, null, "why", "h1", Now, graph: graph, route: "landed");

        var written = Written(id!);
        Assert.Equal("landed", written.GetProperty("route").GetString());
        Assert.Equal("check", written.GetProperty("graph").GetProperty("nodes")[0].GetProperty("kind").GetString());
        Assert.False(written.TryGetProperty("steps", out _));
    }

    /// <summary>FABRIC-PIPELINE1i: a route with no version is written alone, with no steps and no graph; the driver judges the workflow.</summary>
    [Fact]
    public void A_route_with_no_version_is_written_alone()
    {
        var (id, _) = Box().ProposeWorkflow("web-app", "docs-to-pr", null, null, false, null, null, null, "why", "h1", Now, route: "kind:docs");

        var written = Written(id!);
        Assert.Equal("kind:docs", written.GetProperty("route").GetString());
        Assert.False(written.TryGetProperty("steps", out _));
        Assert.False(written.TryGetProperty("graph", out _));
    }

    [Theory]
    [InlineData("[1]", null, "the graph's JSON object")]
    [InlineData("not json", null, "`graph` is not valid JSON")]
    [InlineData(null, "kind:", "is not a route")]
    [InlineData(null, "everything", "is not a route")]
    public void A_malformed_graph_or_route_is_refused_with_nothing_written(string? graph, string? route, string says)
    {
        var (id, message) = Box().ProposeWorkflow("web-app", "docs-to-pr", null, null, false, null, null, null, "why", "h1", Now, graph: graph, route: route);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
    }

    [Theory]
    [InlineData("", "docs-to-pr", 1, Graph, null, null, null, "a reason", "scope")]
    [InlineData("web app", "docs-to-pr", 1, Graph, null, null, null, "a reason", "one word")]
    [InlineData("web-app", "", 1, Graph, null, null, null, "a reason", "workflow")]
    [InlineData("web-app", "Docs To PR", 1, Graph, null, null, null, "a reason", "lower-case")]
    [InlineData("web-app", "current", 1, Graph, null, null, null, "a reason", "Current")]
    [InlineData("web-app", "docs-to-pr", 0, Graph, null, null, null, "a reason", "whole number from 1")]
    [InlineData("web-app", "docs-to-pr", 1, "", null, null, null, "a reason", "whole `graph`")]
    [InlineData("web-app", "docs-to-pr", 1, "not json", null, null, null, "a reason", "JSON")]
    [InlineData("web-app", "docs-to-pr", 1, "[]", null, null, null, "a reason", "JSON object")]
    [InlineData("web-app", "docs-to-pr", 1, Graph, "remove look", null, null, "a reason", "`changes`")]
    [InlineData("web-app", "docs-to-pr", 1, Graph, null, "landing", null, "a reason", "`kind`")]
    [InlineData("web-app", "docs-to-pr", 1, Graph, null, null, "file.json", "a reason", "`import`")]
    [InlineData("web-app", "docs-to-pr", 1, Graph, null, null, null, " ", "needs its reason")]
    public void A_malformed_workflow_proposal_is_refused_with_nothing_written(
        string scope, string workflow, int? baseVersion, string graph, string? changes, string? kind, string? import, string why, string says)
    {
        var (id, message) = Box().ProposeWorkflow(scope, workflow, baseVersion, null, false, changes, kind, import, why, "h1", Now, graph: graph);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    /// <summary>WORKFLOW1h-name: a new workflow's name is the person's words, trimmed, written only when given.</summary>
    [Fact]
    public void A_new_workflow_s_name_is_trimmed_and_written_only_when_given()
    {
        var (named, _) = Box().ProposeWorkflow("web-app", "docs-to-pr", null, null, false, null, null, null, "why", "h1", Now, name: "  Docs to a pull request ", graph: Graph);
        Assert.Equal("Docs to a pull request", Written(named!).GetProperty("name").GetString());

        var (blank, _) = Box().ProposeWorkflow("web-app", "docs-to-pr", null, null, false, null, null, null, "why", "h1", Now, name: "  ", graph: Graph);
        Assert.False(Written(blank!).TryGetProperty("name", out _));

        var (none, _) = Box().ProposeWorkflow("web-app", "docs-to-pr", null, null, false, null, null, null, "why", "h1", Now, graph: Graph);
        Assert.False(Written(none!).TryGetProperty("name", out _));
    }

    [Fact]
    public void A_name_over_sixty_characters_is_refused()
    {
        var (id, message) = Box().ProposeWorkflow("web-app", "docs-to-pr", null, null, false, null, null, null, "why", "h1", Now, name: new string('n', 61), graph: Graph);

        Assert.Null(id);
        Assert.Contains("at most 60", message);
        Assert.Contains("Nothing was proposed", message);
        var (ok, _) = Box().ProposeWorkflow("web-app", "docs-to-pr", null, null, false, null, null, null, "why", "h1", Now, name: new string('n', 60), graph: Graph);
        Assert.NotNull(ok);
    }

    [Fact]
    public void A_name_on_an_edit_is_refused_by_name()
    {
        var (id, message) = Box().ProposeWorkflow("web-app", "docs-to-pr", 2, null, false, null, null, null, "why", "h1", Now, name: "Renamed", graph: Graph);

        Assert.Null(id);
        Assert.Contains("`name`", message);
        Assert.Contains("keeps", message);
        Assert.Contains("Nothing was proposed", message);
    }

    private const string DraftGraph =
        """{"schemaVersion":2,"entries":["work"],"end":"line","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"}],"wires":[]}""";

    /// <summary>
    /// FLOWCHART2e (the flowchart design §4, §8.4): a change to the draft open in the editor is its fingerprint and the whole graph; the
    /// workflow it names may be none yet, and nothing it carries saves or chooses.
    /// </summary>
    [Fact]
    public void A_change_to_the_open_draft_is_written_with_its_fingerprint_and_its_graph()
    {
        var (id, message) = Box().ProposeWorkflow("web-app", "", null, null, false, null, null, null, "add a release", "h1", Now, graph: DraftGraph, draft: " g1-0123456789abcdef ");

        var written = Written(id!);
        Assert.Equal("g1-0123456789abcdef", written.GetProperty("draft").GetString());
        Assert.Equal("claimed", written.GetProperty("graph").GetProperty("nodes")[0].GetProperty("kind").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("base").ValueKind);
        Assert.False(written.GetProperty("choose").GetBoolean());
        Assert.False(written.TryGetProperty("steps", out _));
        Assert.Contains($"Proposed `#{id}`", message);

        var (named, _) = Box().ProposeWorkflow("web-app", "release-flow", null, null, false, null, null, null, "why", "h1", Now, graph: DraftGraph, draft: "g1-1");
        Assert.Equal("release-flow", Written(named!).GetProperty("workflow").GetString());
    }

    /// <summary>FLOWCHART2e (§8.4): a draft mixed with what saves or chooses is refused, with nothing written.</summary>
    [Theory]
    [InlineData(null, DraftGraph, 2, false, null, null, "g1-1", "`base`")]
    [InlineData(null, DraftGraph, null, true, null, null, "g1-1", "`choose`")]
    [InlineData(null, DraftGraph, null, false, "default", null, "g1-1", "`route`")]
    [InlineData(null, DraftGraph, null, false, null, "Release", "g1-1", "`name`")]
    [InlineData(Steps, null, null, false, null, null, "g1-1", "`graph`")]
    [InlineData(null, null, null, false, null, null, "g1-1", "`graph`")]
    [InlineData(null, DraftGraph, null, false, null, null, "g1 bad", "fingerprint")]
    public void A_draft_mixed_with_a_save_or_a_choice_is_refused_with_nothing_written(
        string? steps, string? graph, int? baseVersion, bool choose, string? route, string? name, string draft, string says)
    {
        var (id, message) = Box().ProposeWorkflow("web-app", "", baseVersion, steps, choose, null, null, null, "why", "h1", Now, name, graph, route, draft);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    [Fact]
    public void Changes_kind_and_import_are_refused_as_not_proposed_yet()
    {
        var (_, message) = Box().ProposeWorkflow("web-app", "docs-to-pr", 1, null, false, "x", null, null, "why", "h1", Now, graph: Graph);

        Assert.Contains("not proposed yet", message);
    }
}
