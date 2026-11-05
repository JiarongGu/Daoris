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

    private static readonly IReadOnlyList<WorkflowPlugin> Plugins = [new("example.pull-request", [HookPoints.Perform, HookPoints.State])];

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workflow-effective-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private IWorkflowStore Store => WorkflowPorts.Store(_home);

    private int Save(string id, string name, JsonNode? steps, JsonNode? graph, string? pipeline = null) =>
        steps is not null ? StoredSteps.Add(Store, id, name, "2026-11-01T09:00:00Z", steps) : WorkflowStore.Add(_home, new WorkflowVersionAdded(id, name, "terminal", "2026-11-01T09:00:00Z", steps, graph) { Pipeline = pipeline }, kept: []);

    private static JsonObject Release() => WorkflowPresets.GraphTable.First(preset => preset.Id == SetupPresets.Merge).Graph();

    private static DriverConfig Edit(DriverConfig config, params WorkflowChoiceEdit[] edits) => edits.Aggregate(config, WorkflowSelection.Apply);

    private static DriverConfig Declared(DriverConfig config) =>
        Edit(config, new WorkflowDeclare("docs", Space, "Docs", ["docs"]), new WorkflowDeclare("ops", Space, "Operations", ["ops"]));

    private WorkflowEffective Read(DriverConfig config, string route, string? repository = Repo, string? workspace = Space) =>
        WorkflowEffectives.Read(config, Store, repository, workspace, route, Plugins);

    /// <summary>A file that still holds <c>current</c> for the repository: read as before, though no door writes it as a development choice any more.</summary>
    private static DriverConfig StoredCurrent(DriverConfig config) =>
        config with { Workflows = new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase) { [Repo] = new WorkflowChoice("current", []) } };

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
            ("explicit Current", StoredCurrent(Edit(DriverConfig.Empty, new WorkflowUse("release", null, Space, null))), "default", null),
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
            if (selected.Workflow == WorkflowSelection.Current)
            {
                Assert.Null(effective.Version);
                Assert.Null(effective.Graph);
            }
            else
            {
                Assert.NotNull(effective.Graph);
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
        var config = StoredCurrent(Edit(Declared(DriverConfig.Empty), new WorkflowUse("release", null, Space, "docs")));

        Assert.Throws<DriverException>(() => Read(config, "kind:missing"));
        Assert.Throws<DriverException>(() => Read(config, "nonsense"));
        // The repository names a development route, so its own default (Current) replaces the workspace's kinds (design 4.1).
        Assert.Equal(WorkflowSelection.Current, Read(config, "kind:docs").Workflow);
        var workspace = Read(config, "kind:docs", repository: null);
        Assert.Equal("release", workspace.Workflow);
        Assert.Equal(WorkflowLevels.WorkspaceKind, workspace.Source.Level);
    }

    /// <summary>A saved version's cells are the cells a run bound to it answers, at the level that chose it.</summary>
    [Fact]
    public void A_saved_versions_graph_is_its_newest()
    {
        Save("release", "Release", null, Release());
        Save("release", "Release", null, Release());
        var config = Edit(DriverConfig.Empty, new WorkflowUse("release", Repo, null, null));

        var graph = Read(config, "default");
        Assert.Equal(2, graph.Version);
        Assert.Equal("Release", graph.Name);
        Assert.Equal(2, graph.Schema);
        Assert.All(graph.Cells, cell => Assert.Equal(WorkflowLevels.Repository, cell.Step.Source.Level));
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
    /// <summary>REVIEW2: the chart follows the newest version as the run's bind does; a newest that does not read is drawn as its problem, not as an older one.</summary>
    [Fact]
    public void A_newest_version_that_does_not_read_is_the_problem_drawn_and_no_older_version_is()
    {
        Save("release", "Release", null, Release());
        var path = Path.Combine(WorkflowStore.FolderOf(_home), "release.json");
        var file = JsonNode.Parse(File.ReadAllText(path))!;
        file["versions"]!.AsArray().Add(new JsonObject { ["version"] = 2, ["schema"] = 7 });
        File.WriteAllText(path, file.ToJsonString());
        var config = DriverConfig.Load(WriteConfig("""{"workflows":{"engine":{"default":"release"}}}"""));

        var effective = Read(config, "default");

        Assert.Null(effective.Graph);
        Assert.Null(effective.Version);
        Assert.Contains("v2 cannot be read here", effective.Problem);
    }

    private void SaveCopy(string id, string digest, bool opinion = false)
    {
        var shipped = WorkflowDefaults.Of("integrated")!;
        var planned = WorkflowOperations.PlanGraph(_home, null, id, null, shipped.Name, "terminal", "2026-11-01T09:00:00Z",
            WorkflowPresets.WithGraphGates(shipped.Graph(), opinion, look: false), new WorkflowFrom(shipped.Id, digest));
        Store.Write(id, planned.Plan.Result!);
    }

    /// <summary>WORKFLOWCLEAN1h: a copy stamped with an older shipped digest is offered the shipped graph, with the gates the copy has.</summary>
    [Fact]
    public void A_copy_of_a_default_Daoris_ships_changed_since_offers_the_shipped_graph_with_its_gates()
    {
        var shipped = WorkflowDefaults.Of("integrated")!;
        SaveCopy("old", "0000", opinion: true);
        SaveCopy("uptodate", shipped.Digest);
        DriverConfig Choosing(string id) => Edit(DriverConfig.Empty, new WorkflowUse(id, Repo, null, null));

        var newer = Read(Choosing("old"), "default").Newer;

        Assert.Equal("integrated", newer?.Default);
        Assert.Equal(NodeGraphs.Digest(NodeGraphs.Read(WorkflowPresets.WithGraphGates(shipped.Graph(), opinion: true, look: false).ToJsonString()).Graph!),
            NodeGraphs.Digest(NodeGraphs.Read(newer!.Graph.ToJsonString()).Graph!));
        Assert.Null(Read(Choosing("uptodate"), "default").Newer);
    }

    /// <summary>WORKFLOWCLEAN1h: the update is saved as the copy's next version and keeps where it came from, so it is the shipped digest the copy follows after.</summary>
    [Fact]
    public async Task Saving_the_update_stamps_the_next_version_with_the_shipped_digest()
    {
        var shipped = WorkflowDefaults.Of("integrated")!;
        SaveCopy("old", "0000", opinion: true);

        var saved = await WorkflowOperations.SaveAsync(DriverConfig.Empty, _home, _ => Task.FromResult(new ChoicesRegistry(null, "unread")), "old", null, null,
            shipped.Graph(), false, false, null, "1", null, false, "2026-11-02T09:00:00Z", CancellationToken.None, updates: "integrated");

        Assert.True(saved.Done, saved.Message);
        Assert.Equal(new WorkflowFrom(shipped.Id, shipped.Digest), Store.Load("old")!.Read.Versions[^1].From);
        Assert.Null(Read(Edit(DriverConfig.Empty, new WorkflowUse("old", Repo, null, null)), "default").Newer);
    }

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
        var path = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(path, json);
        return path;
    }
}
