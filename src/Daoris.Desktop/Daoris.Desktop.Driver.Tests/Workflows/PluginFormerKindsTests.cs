using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A saved workflow naming a renamed plugin's step by its old id: the version stays as written, and its graph is read with the step's
/// kind under the id the plugin has now, at its saved digest, so a new run binds on the new plugin and a run pinned to that
/// version continues on its records.
/// </summary>
public sealed class PluginFormerKindsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-former-kinds-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private const string Old = "acme-tickets/pull-request";
    private const string New = "acme-desk/pull-request";

    private void Renamed()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme-desk");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "acme-desk", "version": "2.0.0", "formerly": ["acme-tickets"],
              "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/perform", "work/state"] },
              "steps": [{ "id": "pull-request", "reach": "performed", "perform": { "point": "work/perform" } }] }
            """);
    }

    /// <summary>The shipped pull-request delivery saved as v1 of <c>ship</c>, its pull-request step naming the old id.</summary>
    private string Saved()
    {
        var graph = WorkflowDefaults.All.First(each => each.Id == "pull-request").Graph();
        foreach (var node in graph["nodes"]!.AsArray().OfType<JsonObject>().Where(node => NodeKit.IsPlugin((string)node["kind"]!))) node["kind"] = Old;
        var file = new JsonObject
        {
            ["id"] = "ship", ["name"] = "Ship",
            ["versions"] = new JsonArray(new JsonObject { ["version"] = 1, ["at"] = "2026-11-01T09:00:00Z", ["door"] = "terminal", ["schema"] = 2, ["graph"] = graph }),
        };
        Directory.CreateDirectory(WorkflowStore.FolderOf(_home));
        File.WriteAllText(WorkflowStore.PathOf(_home, "ship"), file.ToJsonString());
        return NodeGraphs.Digest(NodeGraphs.Read(graph.ToJsonString()).Graph!);
    }

    [Fact]
    public void A_saved_workflow_naming_the_old_step_kind_reads_as_the_new_plugin_s_and_binds_on_it()
    {
        var digest = Saved();
        Renamed();
        var before = File.ReadAllText(WorkflowStore.PathOf(_home, "ship"));

        var version = Assert.Single(WorkflowPorts.Store(_home).Load("ship")!.Read.Versions);

        Assert.Contains(version.Graph!.Nodes, node => node.Kind == New);
        Assert.DoesNotContain(version.Graph.Nodes, node => node.Kind == Old);
        Assert.Equal(digest, version.Digest);
        Assert.Equal("acme-desk", PullRequestKind.PluginOf(version.Graph.Nodes.Single(node => node.Kind == New).Kind));
        Assert.Null(PullRequestUnserved.Of(version.Graph, [new WorkflowPlugin("acme-desk", [HookPoints.Perform, HookPoints.State])], "ship", 1));
        Assert.Equal(before, File.ReadAllText(WorkflowStore.PathOf(_home, "ship")));
    }

    [Fact]
    public void A_run_pinned_to_a_version_naming_the_old_kind_continues_at_its_digest_on_the_new_plugin()
    {
        var digest = Saved();
        var binding = new WorkflowRunBinding("run-7", "game", "studio", "ship", "repository", DateTimeOffset.Parse("2026-11-01T09:00:00Z"))
        {
            Version = 1, Digest = digest,
        };
        Assert.Contains(WorkflowGraphs.ForRun(WorkflowPorts.Store(_home).Load("ship")!.Read, binding)!.Graph!.Nodes, node => node.Kind == Old);
        Renamed();

        var decoded = WorkflowGraphs.ForRun(WorkflowPorts.Store(_home).Load("ship")!.Read, binding)!;

        Assert.Null(decoded.Unreadable);
        Assert.Equal(digest, decoded.GraphDigest);
        Assert.Contains(decoded.Graph!.Nodes, node => node.Kind == New);
    }

    [Fact]
    public void With_no_plugin_declaring_it_the_old_kind_is_read_as_written()
    {
        var digest = Saved();

        var version = Assert.Single(WorkflowPorts.Store(_home).Load("ship")!.Read.Versions);

        Assert.Contains(version.Graph!.Nodes, node => node.Kind == Old);
        Assert.Equal(digest, version.Digest);
    }
}
