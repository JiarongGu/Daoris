using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1q (the integration design §17.7): <i>Integrated into the line</i> and <i>A pull request</i> as graph presets beside the four of
/// steps, each saved by <c>daoris driver workflow new --from</c> as a graph version that reads and binds a run, with the opinion and look
/// variants read by the graph's own order; <i>with no press</i> refused where the line does not declare its gates and have them accepted.
/// </summary>
public sealed class WorkflowGraphPresetsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "workflow-graph-presets", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public WorkflowGraphPresetsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private Task<int> Run(string words, params (string Repository, string? Workspace, string? Root)[] registry) =>
        DriverChoicesCommand.RunAsync(words.Split(' '), new DriverChoicesWorld
        {
            Verbs = DriverChoiceVerbs.All,
            Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = _out, Error = _error, Verb = "daoris" },
            ConfigPath = Config,
            Folder = _home,
            Now = () => new DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.Zero),
            Registry = _ => Task.FromResult(new ChoicesRegistry(registry, null)),
        }, CancellationToken.None);

    private NamedVersion Saved(string id)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(WorkflowStore.PathOf(_home, id)));
        return WorkflowNamed.Read(document.RootElement).Versions.Single();
    }

    [Theory]
    [InlineData("integrated", "")]
    [InlineData("integrated", " --with-opinion --with-look")]
    [InlineData("integrated-no-press", " --with-opinion")]
    [InlineData("integrated-no-press", " --with-look")]
    [InlineData("pull-request", "")]
    [InlineData("pull-request", " --with-opinion --with-look")]
    public async Task A_graph_preset_saved_reads_as_a_graph_version_and_binds_a_run(string preset, string variant)
    {
        var exit = await Run($"workflow new made --from {preset}{variant}");

        Assert.Equal((0, ""), (exit, _error.ToString()));
        var version = Saved("made");
        Assert.Null(version.Problem);
        Assert.NotNull(version.Graph);
        Assert.StartsWith("gph-", version.Digest);
        var kinds = version.Graph!.Nodes.Select(node => node.Kind).ToList();
        Assert.Contains("work.requests", kinds);
        Assert.DoesNotContain("quest.close", kinds);
        Assert.Equal(variant.Contains("opinion"), kinds.Contains("opinion"));
        Assert.Equal(variant.Contains("look"), kinds.Contains("look"));
        // LANDNODE1h: the preset holds a `land`, and the tail its method brings.
        var land = Assert.Single(version.Graph.Nodes, node => node.Kind == NodeKinds.Land);
        Assert.DoesNotContain(NodeKinds.Handed, kinds);
        Assert.Equal(preset == "pull-request" ? "\"pull-request\"" : "\"integrate\"", land.Options["method"]);
        Assert.Equal(preset == "pull-request", kinds.Contains(NodeKinds.PullRequest) && kinds.Contains(NodeKinds.Pushed));
        Assert.Equal(preset != "pull-request", kinds.Contains(NodeKinds.Integrate));
        // LANDNODE1c2: the person's Accept is a review before the land, and a preset with no press holds none.
        Assert.Equal(preset != "integrated-no-press", kinds.Contains(NodeKinds.Review));
    }

    /// <summary>LANDNODE1c2: <i>Merge with no press</i> is a merge land with no review before it, which a version may hold (D145's press is the flow's).</summary>
    [Fact]
    public async Task A_merge_with_no_press_is_a_merge_land_with_no_review()
    {
        var exit = await Run("workflow new made --from merge-no-press");

        Assert.Equal((0, ""), (exit, _error.ToString()));
        var version = Saved("made");
        Assert.Null(version.Problem);
        Assert.Equal("\"merge\"", Assert.Single(version.Graph!.Nodes, node => node.Kind == NodeKinds.Land).Options["method"]);
        Assert.DoesNotContain(version.Graph.Nodes, node => node.Kind == NodeKinds.Review);
    }

    /// <summary>LANDNODE1h: <c>new --from current:&lt;repository&gt;</c> writes Current's landing out as a <c>land</c> node and its tail.</summary>
    [Fact]
    public async Task Current_is_written_out_as_a_land_node_with_its_tail()
    {
        File.WriteAllText(Config, """{"landings":{"web-app":{"form":"branch","pattern":"w/{quest}","plugin":"acme.pr","autoAccept":true}}}""");

        var exit = await Run("workflow new made --from current:web-app", ("web-app", "work", null));

        Assert.Equal((0, ""), (exit, _error.ToString()));
        var graph = Saved("made").Graph!;
        var land = Assert.Single(graph.Nodes, node => node.Kind == NodeKinds.Land);
        Assert.Equal(
            ["\"pull-request\"", "\"w/{quest}\"", "\"acme.pr\""],
            [land.Options["method"], land.Options["pattern"], land.Options["plugin"]]);
        Assert.DoesNotContain(LandNodes.AcceptOption, land.Options.Keys);
        Assert.Equal(["claimed", "work.requests", "land", "pushed", "pull-request"], graph.Nodes.Select(node => node.Kind));
        Assert.Equal("pull-request", graph.End);
    }

    /// <summary>LANDNODE1c2: a rule that waits for the person's Accept is written out with a <c>review</c> before the <c>land</c>.</summary>
    [Fact]
    public async Task Current_waiting_for_your_accept_is_written_out_with_a_review_before_the_land()
    {
        File.WriteAllText(Config, """{"landings":{"web-app":{"form":"branch","pattern":"w/{quest}","plugin":"acme.pr"}}}""");

        var exit = await Run("workflow new made --from current:web-app", ("web-app", "work", null));

        Assert.Equal((0, ""), (exit, _error.ToString()));
        var graph = Saved("made").Graph!;
        Assert.Equal(["claimed", "work.requests", "review", "land", "pushed", "pull-request"], graph.Nodes.Select(node => node.Kind));
        Assert.Contains(graph.Wires, wire => wire.From?.Node == "review" && wire.To.Node == "landing");
    }

    [Fact]
    public async Task A_variant_wires_each_gate_into_the_landing_and_the_opinion_never_into_the_look()
    {
        await Run("workflow new made --from integrated --with-opinion --with-look");

        var wires = Saved("made").Graph!.Wires.Select(wire => $"{wire.From!.Node}>{wire.To.Node}").ToList();

        Assert.Contains("opinion>landing", wires);
        Assert.Contains("look>landing", wires);
        Assert.DoesNotContain("opinion>look", wires);
        Assert.DoesNotContain("requests>landing", wires);
    }

    [Fact]
    public async Task The_presets_are_listed_with_a_line_composed_from_their_nodes()
    {
        await Run("workflow list");

        var said = _out.ToString();
        Assert.Contains("`integrated` — Integrated into the line, a graph.", said);
        Assert.Contains("`pull-request` — A pull request, a graph.", said);
        Assert.Contains("integrated into the line once its agent is done", said);
    }

    [Fact]
    public async Task With_no_press_is_refused_for_a_line_that_declares_no_gates_and_writes_no_choice()
    {
        await Run("workflow new quiet --from integrated-no-press");
        File.WriteAllText(Config, """{"landings":{"web-app":{"form":"branch","pattern":"w/{quest}"}}}""");

        var exit = await Run("workflow use quiet --repository web-app", ("web-app", "work", null));

        Assert.Equal(1, exit);
        Assert.Contains("`web-app`", _error.ToString());
        Assert.DoesNotContain("\"workflows\"", File.ReadAllText(Config));
    }

    [Fact]
    public async Task An_unknown_preset_names_the_graph_presets_among_those_it_offers()
    {
        var exit = await Run("workflow new made --from nothing");

        Assert.NotEqual(0, exit);
        Assert.Contains("`integrated`", _error.ToString());
        Assert.Contains("`pull-request`", _error.ToString());
    }
}
