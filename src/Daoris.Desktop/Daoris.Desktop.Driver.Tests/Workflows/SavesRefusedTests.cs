using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOWCLEAN1 1b (the workflow clean-up design §3, §13 point 2): the one judge at the one save point. A list of steps, a <c>handed</c> and a
/// <c>land</c> with no method are refused when saved, by <see cref="WorkflowNamed.ReadVersion"/> and <see cref="NodeGraphs.Read"/> with
/// <c>onSave</c>; a stored version of each still reads, shows and binds as it did.
/// </summary>
public sealed class SavesRefusedTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "saves-refused", Guid.NewGuid().ToString("N")[..8]);

    public SavesRefusedTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private const string NoMethod = """{"schemaVersion":2,"entries":["claim"],"end":"land","nodes":[{"id":"claim","kind":"claimed","version":1,"reach":"observed"},{"id":"land","kind":"land","version":1,"reach":"performed"}],"wires":[{"id":"a","kind":"after","from":"claim","to":"land"}]}""";

    private const string Steps = """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""";

    private static IReadOnlyList<WireProblem> Saved(string graph) => NodeGraphs.Read(graph, onSave: true).Problems;

    [Fact]
    public void A_land_with_no_method_is_refused_when_saved_and_a_stored_one_still_reads()
    {
        var said = Assert.Single(Saved(NoMethod));

        Assert.Equal((GraphRefusal.LandMethod, "land"), (said.Code, said.Node));
        Assert.Equal("node `land`: a `land` is saved with its method chosen: merge, branch, push, pull-request or integrate.", said.Words);
        Assert.Empty(NodeGraphs.Read(NoMethod).Problems);
    }

    [Fact]
    public void A_land_with_an_accept_says_where_the_review_goes()
    {
        var graph = NoMethod.Replace("\"reach\":\"performed\"}", "\"reach\":\"performed\",\"options\":{\"method\":\"merge\",\"accept\":\"you\"}}", StringComparison.Ordinal);

        var said = Assert.Single(Saved(graph));

        Assert.Equal(GraphRefusal.Accept, said.Code);
        Assert.Equal("node `land`: a `land` takes no `accept`. Where you accept the work, put *Your review* before it; with no review it lands once the steps before it are done.", said.Words);
    }

    [Fact]
    public void A_tail_without_its_method_says_which_method_brings_it()
    {
        var graph = NoMethod.Replace("\"reach\":\"performed\"}", "\"reach\":\"performed\",\"options\":{\"method\":\"merge\"}},{\"id\":\"push\",\"kind\":\"pushed\",\"version\":1,\"reach\":\"observed\"}", StringComparison.Ordinal)
            .Replace("\"wires\":[", "\"wires\":[{\"id\":\"b\",\"kind\":\"after\",\"from\":\"land\",\"to\":\"push\"},", StringComparison.Ordinal);

        var said = Saved(graph).Single(problem => problem.Code == GraphRefusal.LandTail);

        Assert.Equal("node `push` is a `pushed`, which comes with a `land` whose method is `push`: choose that method on the landing and it brings this step.", said.Words);
    }

    [Fact]
    public void A_version_of_steps_is_refused_when_saved_by_the_judge_and_read_when_stored()
    {
        var version = new JsonObject { ["version"] = 1, ["at"] = "2026-10-09T09:00:00Z", ["door"] = "screen", ["steps"] = JsonNode.Parse(Steps) };
        var element = WorkflowNamed.ElementOf(version);

        var refused = WorkflowNamed.ReadVersion(element, 1, [], onSave: true, id: "docs");
        Assert.Equal((null, WorkflowNamed.StepsRefused("docs")), (refused.Steps, refused.Problem));
        Assert.StartsWith("`docs` is not saved: a workflow is saved as a graph of steps now, and a list of steps (schema 1) is no longer saved.", refused.Problem, StringComparison.Ordinal);
        Assert.Contains("`daoris convert --plan`", refused.Problem);

        var stored = WorkflowNamed.ReadVersion(element, 1, []);
        Assert.Null(stored.Problem);
        Assert.NotNull(stored.Steps);
    }

    [Fact]
    public async Task The_window_s_save_refuses_steps_with_the_code_and_writes_nothing()
    {
        var saved = await WorkflowOperations.SaveAsync(DriverConfig.Empty, _home, _ => Task.FromResult(new ChoicesRegistry(null, "unread")), "docs", null,
            JsonNode.Parse(Steps), null, false, false, null, null, null, false, "2026-10-09T09:00:00Z", CancellationToken.None);

        Assert.Equal((false, WorkflowNamed.StepsRefusedCode, WorkflowNamed.StepsRefused("docs")), (saved.Done, saved.Refused, saved.Message));
        Assert.Null(WorkflowStore.Load(_home, "docs"));
    }

    [Fact]
    public async Task The_window_s_save_refuses_a_copy_of_a_stored_steps_version_and_a_step_preset()
    {
        StoredSteps.Add(WorkflowPorts.Store(_home), "old", "Old", "2026-10-09T09:00:00Z", JsonNode.Parse(Steps)!);
        Task<ChoicesRegistry> Registry(CancellationToken _) => Task.FromResult(new ChoicesRegistry(null, "unread"));

        var copy = await WorkflowOperations.SaveAsync(DriverConfig.Empty, _home, Registry, "fresh", "old@1", null, null, false, false, null, null, null, false, "2026-10-09T09:00:00Z", CancellationToken.None);
        Assert.Equal(WorkflowNamed.StepsRefused("fresh"), copy.Message);

        var preset = await WorkflowOperations.SaveAsync(DriverConfig.Empty, _home, Registry, "fresh", "pull-request-after-accept", null, null, false, false, null, null, null, false, "2026-10-09T09:00:00Z", CancellationToken.None);
        Assert.False(preset.Done);
        Assert.Contains("is no preset", preset.Message);
        Assert.Null(WorkflowStore.Load(_home, "fresh"));
    }

    [Fact]
    public void The_step_presets_are_deleted_and_the_palette_offers_by_kind()
    {
        Assert.DoesNotContain(WorkflowPresets.GraphTable, preset => preset.Id.StartsWith("pull-request-", StringComparison.Ordinal) && preset.Id != "pull-request");

        var palette = WorkflowPalette.Build([]);
        string Offered(string kind) => palette.Kinds.Single(each => each.Kind == kind).Offered;

        Assert.Equal("tail:integrate", Offered(NodeKinds.Integrate));
        Assert.All(new[] { NodeKinds.Claimed, NodeKinds.Land, NodeKinds.Line, NodeKinds.Push, NodeKinds.Review, NodeKinds.Look }, kind => Assert.Equal(PaletteKind.Alone, Offered(kind)));
    }
}
