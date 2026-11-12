using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// End to end over real git: a run's passes over a graph that lands and then releases end the run's tree once its work is on the line and leave
/// the node reached saying so; a tree whose work no branch of the person's holds stays, with the node reached saying why and the tree list
/// reading the reason.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReleaseRunGitTests : ReviewLandingFixture
{
    private const string Graph = """
        {"schemaVersion":2,"entries":["work"],"end":"release","nodes":[
          {"id":"work","kind":"claimed","version":1,"reach":"observed"},
          {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}},
          {"id":"release","kind":"release","version":1,"reach":"performed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"landing"},{"id":"b","kind":"after","from":"landing","to":"release"}]}
        """;

    private sealed class NoPlugins : IWorkRuntimePort
    {
        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct) => throw new DriverException("no plugin runs here");

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            throw new DriverException("no plugin runs here");

        public Task<(WorkCleanupAnswer Answer, int Generation)> CleanupAsync(string plugin, WorkCleanupCall call, CancellationToken ct) =>
            throw new DriverException("no plugin runs here");

        public int? GenerationOf(string plugin) => null;

        public void Owe(IReadOnlySet<string> plugins)
        {
        }

        public void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited)
        {
        }

        public IReadOnlyList<WorkStepHeard> TakeSteps() => [];
    }

    private sealed record Run(string Root, string Tree, GrownBranch Part, string Tip);

    /// <summary>Run q1 bound to the graph, its session's tree holding one commit, its landing reached; <paramref name="onLine"/> merges that work into the line.</summary>
    private async Task<Run> RunAsync(bool onLine)
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var part = Assert.Single(new SessionBranches(_home).All());
        var tip = (await GitAsync(root, "rev-parse", part.Branch)).Trim();
        if (onLine) await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", $"merge {part.Branch}", part.Branch);
        var digest = RunHome.SaveGraph(_home, Graph);
        RunHome.Bind(_home, "q1", digest, DateTimeOffset.UtcNow);
        var unit = RunUnits.IdOf("q1");
        var branch = RunHome.Branch(part.Unit!, part.Branch, part.At, ["q1"]).Id;
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["landing"] = new(unit, "landing", NodeStates.Reached)
                {
                    Attempt = 1, Tries = 1,
                    Acceptance = new NodeAcceptance(unit, tip, digest, "landing", 1, 1)
                    {
                        Evidence = new NodePort("daoris/landing@1", "landing-1", tip, null), Subject = branch,
                    },
                },
            },
        });
        return new Run(root, tree, part, tip);
    }

    private async Task PassAsync(Run run, ReleaseDoor door)
    {
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), new NoPlugins(), () => DateTimeOffset.UtcNow, () => true)
        {
            Doors = NodeDoors.None.With(NodeKinds.Release, door),
        };
        var pass = await runtime.PassAsync(new NodeRuntimeLook(Config(), Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(
                [RunHome.QuestUnit(RunHome.Quest("q1", "Done")), RunHome.Branch(run.Part.Unit!, run.Part.Branch, run.Part.At, ["q1"])],
                new Dictionary<string, string> { ["engine"] = run.Root })),
            Tips = NodeTips.ReadAsync,
            Content = (_, _, _) => Task.FromResult<HoldContent?>(null),
        }, CancellationToken.None);
        Assert.True(pass.Armed);
        await door.IdleAsync();
    }

    private NodeStatus Status(string node) =>
        new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses.GetValueOrDefault(node) ?? new NodeStatus(RunUnits.IdOf("q1"), node, NodeStates.Blocked);

    private async Task PassesUntilReleasedAsync(Run run, ReleaseDoor door)
    {
        for (var at = 0; at < 6 && Status("release").State != NodeStates.Reached; at++) await PassAsync(run, door);
        Assert.True(Status("release").State == NodeStates.Reached, string.Join(", ", new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses.Select(each => $"{each.Key}={each.Value.State} {each.Value.Code} {each.Value.Words}")));
    }

    [Fact]
    public async Task A_run_from_its_landing_to_its_release_leaves_no_tree()
    {
        var run = await RunAsync(onLine: true);

        await PassesUntilReleasedAsync(run, new ReleaseDoor(_home, holdsLease: () => true));

        Assert.False(Directory.Exists(run.Tree));
        Assert.Empty((await GitAsync(run.Root, "branch", "--list", run.Part.Branch)).Trim());
        Assert.Contains("1 tree of its run ended", Status("release").Words, StringComparison.Ordinal);
        Assert.Empty(await new SessionTrees(_home).ListAsync(each => each == "engine"));
    }

    [Fact]
    public async Task An_unlanded_tree_is_kept_the_release_reached_saying_why_and_the_tree_list_reading_it()
    {
        var run = await RunAsync(onLine: false);

        await PassesUntilReleasedAsync(run, new ReleaseDoor(_home, holdsLease: () => true));

        Assert.True(Directory.Exists(run.Tree));
        Assert.Contains("1 kept", Status("release").Words, StringComparison.Ordinal);
        Assert.Contains("has not taken", Status("release").Words, StringComparison.Ordinal);
        var listed = Assert.Single(await new SessionTrees(_home).ListAsync(each => each == "engine"));
        Assert.Equal("q1", listed.Run);
        Assert.Contains("has not taken", listed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_release_no_way_from_a_landing_reaches_is_refused_at_the_save()
    {
        var alone = Graph.Replace("""{"id":"b","kind":"after","from":"landing","to":"release"}""", """{"id":"b","kind":"after","from":"work","to":"release"}""", StringComparison.Ordinal);

        var problems = NodeGraphs.Read(alone, onSave: true).Problems.Where(problem => problem.Node == "release").Select(problem => problem.Code);

        Assert.Equal([GraphRefusal.ReleaseLanding], problems);
        Assert.Empty(NodeGraphs.Read(Graph, onSave: true).Problems.Where(problem => problem.Node == "release"));
    }
}
