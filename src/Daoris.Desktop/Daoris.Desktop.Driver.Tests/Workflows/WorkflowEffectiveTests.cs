using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SETUPUX1b (D221): the graph a route actually follows, read by <see cref="WorkflowEffectives"/>. Its identity is
/// <see cref="WorkflowSelection.Resolve"/>'s, Current's graph is the one <c>workflow new --from current:&lt;repository&gt;</c> writes
/// (and a workspace's the same handler over the workspace's own rules), each node carries the cell a run answers, and the read writes nothing.
/// </summary>
public sealed class WorkflowEffectiveTests : IDisposable
{
    private const string Repo = "engine";
    private const string Space = "aurora";

    private static readonly IReadOnlyList<WorkflowPlugin> Plugins = [new("example.pull-request", [HookPoints.Land, HookPoints.State])];

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workflow-effective-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private IWorkflowStore Store => WorkflowPorts.Store(_home);

    private int Save(string id, string name, JsonNode? steps, JsonNode? graph, string? pipeline = null) =>
        WorkflowStore.Add(_home, new WorkflowVersionAdded(id, name, "terminal", "2026-11-01T09:00:00Z", steps, graph) { Pipeline = pipeline }, kept: []);

    private static JsonObject Release() => WorkflowPresets.GraphTable.First(preset => preset.Id == SetupPresets.Merge).Graph();

    private static DriverConfig Edit(DriverConfig config, params WorkflowChoiceEdit[] edits) => edits.Aggregate(config, WorkflowSelection.Apply);

    private static DriverConfig Declared(DriverConfig config) =>
        Edit(config, new WorkflowDeclare("docs", Space, "Docs", ["docs"]), new WorkflowDeclare("ops", Space, "Operations", ["ops"]));

    private WorkflowEffective Read(DriverConfig config, string route, string? repository = Repo, string? workspace = Space) =>
        WorkflowEffectives.Read(config, Store, repository, workspace, route, Plugins);

    /// <summary>A saved default, an inherited kind, an explicit Current and a cleared choice each carry the identity <c>Resolve</c> gives.</summary>
    [Fact]
    public void Its_identity_is_the_selections_for_a_default_a_kind_Current_and_a_cleared_choice()
    {
        Save("release", "Release for engine", null, Release());
        Save("docs-flow", "Docs for aurora", null, Release());
        var cases = new (string Name, DriverConfig Config, string Route, string? Task)[]
        {
            ("a saved default", Edit(DriverConfig.Empty, new WorkflowUse("release", Repo, null, null)), "default", null),
            ("an inherited kind", Edit(Declared(DriverConfig.Empty), new WorkflowUse("docs-flow", null, Space, "docs")), "kind:docs", "docs"),
            ("explicit Current", Edit(DriverConfig.Empty, new WorkflowUse("release", null, Space, null), new WorkflowUse("current", Repo, null, null)), "default", null),
            ("a cleared choice", Edit(DriverConfig.Empty, new WorkflowUse("release", Repo, null, null), new WorkflowUse(null, Repo, null, null)), "default", null),
            ("the workspace's default", Edit(DriverConfig.Empty, new WorkflowUse("release", null, Space, null)), "default", null),
        };

        foreach (var (name, config, route, task) in cases)
        {
            var selected = WorkflowSelection.Resolve(config, Repo, Space, task is null ? null : new WorkflowTaskChoice(task, null));
            var effective = Read(config, route);
            Assert.True(selected.Workflow == effective.Workflow, name);
            Assert.True(selected.Level == effective.Source.Level, name);
            Assert.Equal(selected.Kind, effective.Source.Kind);
            Assert.Equal(selected.Label, effective.Source.Label);
            Assert.NotNull(effective.Graph);
            if (selected.Workflow == WorkflowSelection.Current)
            {
                Assert.Null(effective.Version);
                Assert.Equal(WorkflowCurrent.Derive(config, Repo, Space, Plugins).Version, effective.Digest);
            }
            else
            {
                Assert.Equal(1, effective.Version);
                Assert.Equal(Store.Load(selected.Workflow)!.Read.Versions[^1].Digest, effective.Digest);
            }
        }
    }

    /// <summary>A kind is a route only where its workspace declares it; a workspace's own read resolves for the workspace alone.</summary>
    [Fact]
    public void A_kind_its_workspace_does_not_declare_is_no_route_and_a_workspace_resolves_for_itself()
    {
        Save("release", "Release", null, Release());
        var config = Edit(Declared(DriverConfig.Empty), new WorkflowUse("release", null, Space, "docs"), new WorkflowUse("current", Repo, null, null));

        Assert.Throws<DriverException>(() => Read(config, "kind:missing"));
        Assert.Throws<DriverException>(() => Read(config, "nonsense"));
        // The repository names a development route, so its own default (Current) replaces the workspace's kinds (design 4.1).
        Assert.Equal(WorkflowSelection.Current, Read(config, "kind:docs").Workflow);
        var workspace = Read(config, "kind:docs", repository: null);
        Assert.Equal("release", workspace.Workflow);
        Assert.Equal(WorkflowLevels.WorkspaceKind, workspace.Source.Level);
    }

    /// <summary>Current's graph is what <c>new --from current:&lt;repository&gt;</c> writes, and a workspace's the same handler over its own rules.</summary>
    [Fact]
    public async Task Currents_graph_is_the_one_new_from_current_writes_for_a_repository_and_for_a_workspace()
    {
        var config = DriverConfig.Load(WriteConfig("""
            {
              "workspaceLandings": { "aurora": { "form": "branch", "pattern": "work/{quest}", "plugin": "example.pull-request" } },
              "reviews": { "engine": { "required": true, "environments": [{ "name": "local", "kind": "local", "procedure": "README.md", "address": "http://localhost:4200" }] } }
            }
            """));
        WorkflowOperations.RegistryRead registry = _ => Task.FromResult(new ChoicesRegistry([(Repo, (string?)Space, (string?)null)], null));

        var written = await WorkflowOperations.SourceAsync(config, _home, registry, "current:" + Repo, opinion: false, look: false, CancellationToken.None);
        var repository = Read(config, "default");
        Assert.Equal(written.Graph!.ToJsonString(), repository.Graph!.ToJsonString());
        Assert.Contains(repository.Graph!["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Look);

        var shared = WorkflowCurrent.Derive(config, null, Space, Plugins);
        var workspace = Read(config, "default", repository: null);
        Assert.Equal(WorkflowWords.GraphOfCurrent(shared, false, false).ToJsonString(), workspace.Graph!.ToJsonString());
        Assert.DoesNotContain(workspace.Graph!["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Look);
        Assert.Equal(shared.Version, workspace.Digest);
    }

    /// <summary>Each node of Current's graph has a cell: the step Current draws where it draws one, else the cell a run answers for the node.</summary>
    [Fact]
    public void Each_node_carries_the_cell_a_run_answers_and_the_steps_cell_where_Current_has_one()
    {
        var config = DriverConfig.Load(WriteConfig("""
            { "landings": { "engine": { "form": "branch", "pattern": "work/{session}", "plugin": "example.pull-request" } } }
            """));
        var drawn = WorkflowCurrent.Derive(config, Repo, Space, Plugins);

        var effective = Read(config, "default");

        var nodes = effective.Graph!["nodes"]!.AsArray().Select(node => (string)node!["id"]!).ToList();
        Assert.Equal(nodes, effective.Cells.Select(cell => cell.Node));
        foreach (var step in drawn.Steps)
        {
            Assert.Equal(WorkflowCurrent.ToJson(step), WorkflowCurrent.ToJson(effective.Cells.Single(cell => cell.Node == step.Id).Step));
        }

        var requests = effective.Cells.Single(cell => cell.Node == "requests").Step;
        Assert.Equal(NodeKinds.WorkRequests, requests.Kind);
        Assert.Equal(WorkflowRuntime.Built, requests.Runtime);
    }

    /// <summary>A saved version's cells are the cells a run bound to it answers, at the level that chose it; a steps version reads as the graph it decodes to.</summary>
    [Fact]
    public void A_saved_versions_graph_is_its_newest_and_a_steps_version_is_drawn_as_its_decoded_graph()
    {
        Save("release", "Release", null, Release());
        Save("release", "Release", null, Release());
        Save("legacy", "Legacy", WorkflowPresets.Table[0].Steps(), null);
        var config = Edit(DriverConfig.Empty, new WorkflowUse("release", Repo, null, null));

        var graph = Read(config, "default");
        Assert.Equal(2, graph.Version);
        Assert.Equal("Release", graph.Name);
        Assert.Equal(2, graph.Schema);
        Assert.All(graph.Cells, cell => Assert.Equal(WorkflowLevels.Repository, cell.Step.Source.Level));

        var steps = Read(Edit(DriverConfig.Empty, new WorkflowUse("legacy", Repo, null, null)), "default");
        Assert.Equal(1, steps.Schema);
        Assert.Equal(WorkflowGraphs.Saved(Store.Load("legacy")!.Read.Versions[^1].Steps!).ToJsonString(), steps.Graph!.ToJsonString());
        Assert.NotEmpty(steps.Cells);
    }

    /// <summary>A choice naming a workflow that is not saved here, or does not read, answers why and no graph.</summary>
    [Fact]
    public void A_workflow_that_is_not_saved_answers_why_and_draws_nothing()
    {
        var effective = Read(Edit(DriverConfig.Empty, new WorkflowUse("gone", Repo, null, null)), "default");

        Assert.Equal("gone", effective.Workflow);
        Assert.Null(effective.Graph);
        Assert.Empty(effective.Cells);
        Assert.Contains("gone", effective.Problem);
    }

    /// <summary>Landed <c>current</c> means no verification and draws nothing; a verification pipeline is drawn as saved.</summary>
    [Fact]
    public void The_landed_route_is_no_verification_under_Current_and_a_pipeline_when_one_is_chosen()
    {
        Save("web-verify", "Verify the line", null, JsonNode.Parse(
            """{"schemaVersion":2,"entries":["gates"],"end":"gates","nodes":[{"id":"gates","kind":"check","version":1,"reach":"performed"}],"wires":[]}"""),
            WorkflowPipelines.Verification);

        var none = Read(DriverConfig.Empty, "landed");
        Assert.Equal(WorkflowSelection.Current, none.Workflow);
        Assert.False(none.Verifies);
        Assert.Null(none.Graph);
        Assert.Empty(none.Cells);
        Assert.Equal(WorkflowLevels.Current, none.Source.Level);

        var config = Edit(DriverConfig.Empty, new WorkflowUse("web-verify", Repo, null, null) { Landed = true, Pipeline = WorkflowPipelines.Verification });
        var chosen = Read(config, "landed");
        Assert.True(chosen.Verifies);
        Assert.Equal("web-verify", chosen.Workflow);
        Assert.Equal(WorkflowLevels.RepositoryLanded, chosen.Source.Level);
        Assert.Equal(["gates"], chosen.Cells.Select(cell => cell.Node));
    }

    /// <summary>The read writes nothing: the driver's file and the workflows under the home are byte for byte as they were.</summary>
    [Fact]
    public void Reading_writes_nothing()
    {
        Save("release", "Release", null, Release());
        var path = WriteConfig("""{"workflows":{"engine":{"default":"release"}}}""");
        var config = DriverConfig.Load(path);
        string Snapshot() => string.Join('\n', Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(file => file + "=" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)))));
        var before = Snapshot();

        Read(config, "default");
        Read(config, "landed");
        Read(config, "default", repository: null);

        Assert.Equal(before, Snapshot());
    }

    private string WriteConfig(string json)
    {
        Directory.CreateDirectory(_home);
        var path = Path.Combine(_home, "driver.json");
        File.WriteAllText(path, json);
        return path;
    }
}
