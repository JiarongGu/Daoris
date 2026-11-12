using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <i>Integrated into the line</i> and <i>A pull request</i> as graph presets beside the four of
/// steps, each saved by <c>daoris workflow new --from</c> as a graph version that reads and binds a run, with the opinion and look
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
        // The preset holds a `land`, and the tail its method brings.
        var land = Assert.Single(version.Graph.Nodes, node => node.Kind == NodeKinds.Land);
        Assert.Equal(preset == "pull-request" ? "\"branch\"" : "\"integrate\"", land.Options["method"]);
        Assert.Equal(preset == "pull-request", kinds.Contains(NodeKinds.Push) && kinds.Any(kind => kind.EndsWith("/pull-request", StringComparison.Ordinal)));
        Assert.Equal(preset != "pull-request", kinds.Contains(NodeKinds.Integrate));
        // The pull request's monitor routes merged to the tidy and closed to a person's task.
        var routes = version.Graph.Wires.Where(wire => wire.Kind == WireKinds.When)
            .Select(wire => (wire.From!.Node, wire.Outcome, version.Graph.Nodes.Single(node => node.Id == wire.To.Node).Kind));
        (string, string?, string)[] shipped = preset == "pull-request" ? [("monitor", "merged", NodeKinds.Tidy), ("monitor", "closed", NodeKinds.Human)] : [];
        Assert.Equal(shipped, routes);
        // The person's Accept is a review before the land, and a preset with no press holds none.
        Assert.Equal(preset != "integrated-no-press", kinds.Contains(NodeKinds.Review));
    }

    /// <summary>
    /// <i>Merge with no press</i> is a merge land with no review before it, which a version may hold (the press is the flow's);
    /// its id is an alias of <i>Merge automatically</i>, which the copy names.
    /// </summary>
    [Fact]
    public async Task A_merge_with_no_press_is_a_merge_land_with_no_review()
    {
        var exit = await Run("workflow new made --from merge-no-press");

        Assert.Equal((0, ""), (exit, _error.ToString()));
        var version = Saved("made");
        Assert.Null(version.Problem);
        Assert.Equal("\"merge\"", Assert.Single(version.Graph!.Nodes, node => node.Kind == NodeKinds.Land).Options["method"]);
        Assert.DoesNotContain(version.Graph.Nodes, node => node.Kind == NodeKinds.Review);
        Assert.Equal("merge-automatic", version.From?.Default);
    }

    /// <summary>The defaults Daoris ships, in their order, each the graph preset of its id.</summary>
    [Fact]
    public void The_defaults_are_shipped_data_and_the_graph_presets_read_them()
    {
        Assert.Equal(
            ["merge-after-accept", "merge-automatic", "documents", "branch-to-push", "pull-request", "integrated", "integrated-no-press", "release", "in-place",
             "plan-review-apply", "in-copy"],
            WorkflowDefaults.All.Select(each => each.Id));
        Assert.Equal(WorkflowDefaults.All.Select(each => (each.Id, each.Name)), WorkflowPresets.GraphTable.Select(preset => (preset.Id, preset.Name)));
        Assert.Equal("merge-automatic", WorkflowPresets.GraphOf("merge-no-press")?.Id);
    }

    /// <summary>Each default is judged by the graph rules, its delivery is what its graph says, and its line is composed from it.</summary>
    [Theory]
    [InlineData("merge-after-accept", "You accept each piece of work, and it merges into the line.")]
    [InlineData("merge-automatic", "Each piece of work merges into the line once its agent is done.")]
    [InlineData("documents", "Each piece of work merges into the line once its agent is done.")]
    [InlineData("branch-to-push", "You accept each piece of work onto a branch, and push it yourself.")]
    [InlineData("pull-request", "You accept; the plugin pushes and opens a pull request; you merge it.")]
    [InlineData("integrated", "You accept each piece of work onto a branch, and you ask for it to be integrated into the line.")]
    [InlineData("integrated-no-press", "Each piece of work is integrated into the line once its agent is done, on gates the repository accepted.")]
    [InlineData("release", "You accept each piece of work, and it merges into the line.")]
    [InlineData("in-place", "Each piece of work is done in its department's own place, and you accept it there: nothing lands.")]
    [InlineData("plan-review-apply", "Each piece of work is done in its department's own place, and you accept it there: nothing lands.")]
    [InlineData("in-copy", "Each piece of work is done in its department's own place, and you accept it there: nothing lands.")]
    public void Each_default_is_sound_says_its_delivery_and_has_its_line(string id, string landing)
    {
        var shipped = WorkflowDefaults.Of(id)!;

        var (graph, problems) = NodeGraphs.Read(shipped.GraphJson, onSave: true);
        Assert.True(graph is not null, string.Join(" ", problems.Select(problem => problem.Words)));
        Assert.Empty(NodeGraphs.Problems(graph!));
        Assert.Equal(shipped.Digest, NodeGraphs.Digest(graph!));
        Assert.Equal(shipped.Delivery, SetupPresets.LandOf(Involvement.StepsOf(graph!)));
        Assert.Equal($"{landing} Its agent's done closes the quest; the flow's end is the work's.", WorkflowPresets.GraphLine(shipped.Graph()));
    }

    private static NodeGraph ReleaseGraph() => NodeGraphs.Read(WorkflowDefaults.Of("release")!.GraphJson, onSave: true).Graph!;

    private static IReadOnlyList<NodeDeclaration> LocalReleaseSteps()
    {
        var (manifest, problem) = PluginCatalog.ReadFolder(Path.Combine(GitSeed.RepoRoot(), "examples", "plugins", "local-release"));
        Assert.Null(problem);
        return manifest.Steps;
    }

    /// <summary>A declaration's outputs carried on some outcomes alone, each as <c>output=outcome|outcome</c>.</summary>
    private static List<string> Carried(NodeDeclaration node) => [.. node.OutputsOn.Select(each => $"{each.Key}={string.Join('|', each.Value)}")];

    /// <summary>Which outcomes carry an output is digested where a graph names it, so the release default's digest moves without it.</summary>
    [Fact]
    public void Which_outcomes_carry_an_output_is_digested_where_it_is_named()
    {
        var graph = ReleaseGraph();
        var without = graph with
        {
            Nodes = [.. graph.Nodes.Select(node => node.Id == "find" ? node with { OutputsOn = new Dictionary<string, IReadOnlyList<string>>() } : node)],
        };

        Assert.NotEqual(NodeGraphs.Digest(graph), NodeGraphs.Digest(without));
    }

    /// <summary>The release default's plugin nodes declare what local-release's manifest says, outcomes and ports included.</summary>
    [Fact]
    public void The_release_default_declares_what_the_local_release_manifest_says()
    {
        var graph = ReleaseGraph();
        Assert.Empty(NodeGraphs.Problems(graph));
        foreach (var step in LocalReleaseSteps())
        {
            var node = Assert.Single(graph.Nodes, each => each.Kind == step.Kind);
            Assert.Equal((step.Reach, step.Evidence), (node.Reach, node.Evidence));
            Assert.Equal(step.Outcomes.ToList(), node.Outcomes.ToList());
            Assert.Equal(step.Inputs.OrderBy(each => each.Key), node.Inputs.OrderBy(each => each.Key));
            Assert.Equal(step.Outputs.OrderBy(each => each.Key), node.Outputs.OrderBy(each => each.Key));
            Assert.Equal(Carried(step), Carried(node));
        }

        Assert.Equal(["release", "none"], graph.Nodes.Single(node => node.Id == "find").Outcomes);
        Assert.Equal(["release=release"], Carried(graph.Nodes.Single(node => node.Id == "find")));
        Assert.Contains(graph.Wires, wire => wire is { Kind: WireKinds.Back, On: NodeStates.Rejected } && wire.From?.Node == "review" && wire.To.Node == "work");
        Assert.Equal("local-release/find", graph.Nodes.Single(node => node.Id == "find").Kind);
    }

    /// <summary>
    /// A run of the release default after the line: find saying none ends the run there, release and publish not taken;
    /// find saying release takes both.
    /// </summary>
    [Theory]
    [InlineData("none")]
    [InlineData("release")]
    public void A_release_run_ends_at_the_line_when_find_says_none_and_publishes_when_it_says_release(string outcome)
    {
        var graph = ReleaseGraph();
        var g = new WireEvaluationTests.Graph([.. graph.Nodes], [.. graph.Wires]);
        foreach (var id in new[] { "work", "requests", "holds", "content", "check", "review", "landing", "line" }) g.Through(id);

        void ReachAs(string id, string? said, bool port)
        {
            var node = graph.Nodes.Single(each => each.Id == id);
            var status = g.Statuses[id];
            g.Move(id, new NodeMove(NodeStates.Reached, NodeCause.Authority)
            {
                Source = NodeAuthorities.Of(node)!.ReachBy,
                Attempt = status.Attempt,
                Acceptance = status.Binding! with
                {
                    Evidence = new NodePort(node.Evidence!, $"{id}#{status.Attempt}", status.Binding.SubjectRevision, null),
                    Outputs = port ? new Dictionary<string, NodePort> { ["release"] = new("local-release/release@1", $"{id}.release", "tip-1", null) } : NodeAcceptance.NoPorts,
                    Outcome = said,
                },
            });
        }

        g.Start(g.Evaluate(), "find");
        // Find carries its release on `release` alone, so `none` reaches with no release port.
        ReachAs("find", outcome, outcome == "release");
        var decided = g.Evaluate();
        if (outcome == "none")
        {
            Assert.Empty(decided.Starts);
            Assert.Equal(["publish", "release"], decided.NotTaken);
            return;
        }

        Assert.Equal(["release"], decided.Starts.Select(start => start.Node));
        g.Start(decided, "release");
        ReachAs("release", null, true);
        Assert.Equal(["publish"], g.Evaluate().Starts.Select(start => start.Node));
    }

    /// <summary>Where local-release is not installed, its nodes are declared as the graph has them and none is ever sent.</summary>
    [Fact]
    public void Without_the_local_release_plugin_none_of_its_nodes_is_sent()
    {
        var config = DriverConfig.Parse("{}");
        var graph = ReleaseGraph();
        var quest = new QuestView("q7", "game", "engine", "Ship", "", "Taken") { Closure = NodeGraphs.Digest(graph) };
        var unit = new WorkUnit("quest:q7", WorkUnitKind.Quest, new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter),
            new WorkSubject("#q7", null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
        { Records = new WorkUnitRecords(null, [], quest) };
        var read = new NodeUnitsRead([unit], new Dictionary<string, string>());

        var without = Assert.Single(NodeRuntime.Govern(config, read, [], _ => graph));
        Assert.Equal(["check"], without.Dispatchable.Order());
        var with = Assert.Single(NodeRuntime.Govern(config, read, LocalReleaseSteps(), _ => graph));
        Assert.Equal(["check", "find", "publish", "release"], with.Dispatchable.Order());
    }

    /// <summary>A version copied from a default keeps where it came from in its file, the default's id and its digest.</summary>
    [Fact]
    public async Task A_version_copied_from_a_default_keeps_the_default_s_id_and_digest()
    {
        var exit = await Run("workflow new made --from documents --with-opinion");

        Assert.Equal((0, ""), (exit, _error.ToString()));
        var shipped = WorkflowDefaults.Of("documents")!;
        Assert.Equal(new WorkflowFrom("documents", shipped.Digest), Saved("made").From);
        var file = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(WorkflowStore.PathOf(_home, "made")))!;
        Assert.Equal(shipped.Digest, (string?)file["versions"]![0]!["from"]!["digest"]);
        Assert.Contains("A copy of the default `documents`", _out.ToString());
    }

    /// <summary><c>defaults</c> lists what Daoris ships, shows one, and writes one's graph to a file that <c>apply</c> reads.</summary>
    [Fact]
    public async Task The_defaults_are_listed_one_is_shown_and_its_graph_written_to_a_file()
    {
        Assert.Equal(0, await Run("workflow defaults"));
        Assert.All(WorkflowDefaults.All, each => Assert.Contains($"`{each.Id}` — {each.Name}", _out.ToString()));

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run("workflow defaults documents"));
        Assert.StartsWith($"daoris: `documents` — Documents workflow, a default", _out.ToString());

        Assert.Equal((0, ""), (await Run("workflow defaults documents --to documents.json"), _error.ToString()));
        var (graph, _) = NodeGraphs.Read(File.ReadAllText(Path.Combine(_home, "documents.json")));
        Assert.Equal(WorkflowDefaults.Of("documents")!.Digest, NodeGraphs.Digest(graph!));

        Assert.Equal(2, await Run("workflow defaults nothing"));
        Assert.Contains("`nothing` is no default Daoris ships", _error.ToString());
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
        Assert.Contains("`integrated` — Integration workflow, a graph.", said);
        Assert.Contains("`pull-request` — Hosted merge workflow, a graph.", said);
        Assert.Contains("integrated into the line once its agent is done", said);
    }

    [Fact]
    public async Task With_no_press_is_refused_for_a_line_that_declares_no_gates_and_writes_no_choice()
    {
        await Run("workflow new quiet --from integrated-no-press");
        File.WriteAllText(Config, "{}");

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
