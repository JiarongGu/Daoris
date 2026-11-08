using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A save against the newest version of a workflow, and a version added beside it: each writes only over the version it planned against.</summary>
public sealed class WorkflowSaveRaceTests : IDisposable
{
    private const string Repo = "engine";

    private const string At = "2026-11-02T09:00:00Z";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-workflow-save-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static JsonNode Integrating(string request, string conflicts = "session") => JsonNode.Parse($$$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"integrate",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed","options":{"method":"integrate","pattern":"feature/{quest}-{slug}"}},
                  {"id":"integrate","kind":"integrate","version":1,"reach":"waited","options":{"request":"{{{request}}}","conflicts":"{{{conflicts}}}"}}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"},{"id":"h>i","kind":"after","from":"handed","to":"integrate"}]}
        """)!;

    private int Add(JsonNode graph) => WorkflowStore.Add(_home, new WorkflowVersionAdded("wf", "Workflow", "terminal", At, null, graph), kept: []);

    /// <summary>A version another save wrote while a save awaited the reach of its own is not written over: the save is refused <c>stale</c>.</summary>
    [Fact]
    public async Task A_save_whose_workflow_gained_a_version_while_it_awaited_its_reach_is_refused_and_writes_nothing()
    {
        Add(Integrating("done"));
        var choices = WorkflowSelection.Apply(DriverConfig.Empty, new WorkflowUse("wf", Repo, null, null));
        Task<ChoicesRegistry> Quiet(CancellationToken _) => Task.FromResult(new ChoicesRegistry([], null));
        Task<WorkflowSaved> Save(WorkflowOperations.RegistryRead registry, bool plan, IReadOnlyCollection<string>? authorized = null) =>
            WorkflowOperations.SaveAsync(choices, _home, registry, "wf", null, null, Integrating("steward"), false, false, null, "1", authorized, plan, At, CancellationToken.None);
        var keys = (await Save(Quiet, plan: true)).Scopes.Select(each => each.Key).ToList();
        Assert.NotEmpty(keys);
        var competitor = Integrating("steward");
        Task<ChoicesRegistry> Competing(CancellationToken ct)
        {
            Add(competitor);
            return Quiet(ct);
        }

        var saved = await Save(Competing, plan: false, authorized: keys);

        Assert.Equal((false, "stale"), (saved.Done, saved.Refused));
        var versions = WorkflowStore.Load(_home, "wf")!.Read.Versions;
        Assert.Equal([1, 2], versions.Select(each => each.Version));
        Assert.Equal(NodeGraphs.Digest(NodeGraphs.Read(competitor.ToJsonString()).Graph!), NodeGraphs.Digest(versions[^1].Graph!));
    }

    /// <summary>A new workflow's first version is written only where no file stands: one a competing save made is never replaced.</summary>
    [Fact]
    public void A_first_version_is_not_written_over_a_file_another_save_made_meanwhile()
    {
        var store = WorkflowPorts.Store(_home);
        var competitor = Integrating("steward");
        Add(competitor);
        var before = WorkflowStore.Load(_home, "wf")!.Read.Versions;

        Assert.False(WorkflowStore.WriteIfAbsent(store, "wf", WorkflowNamed.PlanAdd(null, new WorkflowVersionAdded("wf", "Mine", "screen", At, null, Integrating("done")), []).Result!));

        Assert.Equal(before.Select(each => each.Version), WorkflowStore.Load(_home, "wf")!.Read.Versions.Select(each => each.Version));
        Assert.Equal(NodeGraphs.Digest(before[0].Graph!), NodeGraphs.Digest(WorkflowStore.Load(_home, "wf")!.Read.Versions[0].Graph!));
        Assert.True(WorkflowStore.WriteIfAbsent(store, "other", WorkflowNamed.PlanAdd(null, new WorkflowVersionAdded("other", "Other", "screen", At, null, Integrating("done")), []).Result!));
    }

    [Fact]
    public async Task A_saved_version_says_the_version_it_came_from_and_the_one_it_made()
    {
        Add(Integrating("done"));
        Task<ChoicesRegistry> Quiet(CancellationToken _) => Task.FromResult(new ChoicesRegistry([], null));

        var saved = await WorkflowOperations.SaveAsync(DriverConfig.Empty, _home, Quiet, "wf", null, null, Integrating("steward"), false, false, null, "1", null, false, At, CancellationToken.None);

        Assert.True(saved.Done, saved.Message);
        Assert.Equal("`wf` v1 → v2.", saved.Message);
    }
}
