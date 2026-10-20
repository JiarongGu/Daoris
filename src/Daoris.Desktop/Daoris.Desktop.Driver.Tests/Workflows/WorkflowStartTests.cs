using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1a (D192 point 2, the pipeline design §1.3): the start node as a repository's or a workspace's Workflow tab draws it,
/// read from the choice table: the default, each kind the workspace declares by its label and paths, each with the pipeline it names and
/// the level that chose it, and the landed route with the gates accepted; beside them the verification pipelines saved here, which
/// Setup's route row offers.
/// </summary>
public sealed class WorkflowStartTests
{
    private static readonly IReadOnlyList<WorkflowFileFound> Saved =
    [
        Found("docs-to-pr", "Documentation to a pull request", null, """{"version":1,"at":"2026-10-09T09:00:00Z","door":"terminal","steps":[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]}"""),
        Found("web-verify", "Verify the line", WorkflowPipelines.Verification,
            """{"version":1,"at":"2026-10-21T09:00:00Z","door":"terminal","schema":2,"graph":{"schemaVersion":2,"entries":["gates"],"end":"gates","nodes":[{"id":"gates","kind":"check","version":1,"reach":"performed"}],"wires":[]}}"""),
    ];

    private static WorkflowFileFound Found(string id, string name, string? pipeline, string version)
    {
        var file = new JsonObject { ["id"] = id, ["name"] = name };
        if (pipeline is not null) file["pipeline"] = pipeline;
        file["versions"] = new JsonArray(JsonNode.Parse(version));
        return new WorkflowFileFound(id, file, WorkflowNamed.Read(WorkflowNamed.ElementOf(file)));
    }

    private static DriverConfig Config(string json) => DriverConfig.Parse(json);

    [Fact]
    public void A_repositorys_start_routes_its_default_each_kind_and_its_landed_work()
    {
        var config = Config("""
            {"workflows":{"web-app":{"landed":{"workflow":"web-verify","gates":"3b18e512dba79e4c8300dd08aeb37f8e728b8dad"}}},
             "workspaceWorkflows":{"work":{"kinds":{"docs":{"label":"Documentation","paths":["docs/**"],"workflow":"docs-to-pr"},"feature":{"label":"Feature"}}}}}
            """);

        var start = WorkflowStart.Read(config, "web-app", "work", Saved);

        Assert.Equal(
        [
            new WorkflowRouteRow(null, null, [], "current", WorkflowLevels.Current, null),
            new WorkflowRouteRow("docs", "Documentation", ["docs/**"], "docs-to-pr", WorkflowLevels.WorkspaceKind, "Documentation to a pull request"),
            new WorkflowRouteRow("feature", "Feature", [], "current", WorkflowLevels.Current, null),
        ], start.Routes, new RouteRows());
        Assert.Equal(new WorkflowLandedSelected("web-verify", "3b18e512dba79e4c8300dd08aeb37f8e728b8dad", WorkflowLevels.RepositoryLanded), start.Landed);
        Assert.Equal("Verify the line", start.LandedName);
        Assert.Equal([("web-verify", "Verify the line")], start.Verifications);
    }

    [Fact]
    public void A_workspaces_start_is_its_own_and_a_route_naming_none_says_so()
    {
        var start = WorkflowStart.Read(Config("""{"workflows":{"web-app":{"landed":{"workflow":"web-verify"}}}}"""), null, "work", Saved);

        Assert.Equal([new WorkflowRouteRow(null, null, [], "current", WorkflowLevels.Current, null)], start.Routes, new RouteRows());
        Assert.Equal(new WorkflowLandedSelected("current", null, WorkflowLevels.Current), start.Landed);
        Assert.Null(start.LandedName);
    }

    /// <summary>Records holding lists compare by reference; the rows compare by what they say.</summary>
    private sealed class RouteRows : IEqualityComparer<WorkflowRouteRow>
    {
        public bool Equals(WorkflowRouteRow? x, WorkflowRouteRow? y) =>
            x is not null && y is not null && (x.Kind, x.Label, x.Workflow, x.Level, x.Name) == (y.Kind, y.Label, y.Workflow, y.Level, y.Name)
            && x.Paths.SequenceEqual(y.Paths);

        public int GetHashCode(WorkflowRouteRow obj) => obj.Workflow.GetHashCode(StringComparison.Ordinal);
    }
}
