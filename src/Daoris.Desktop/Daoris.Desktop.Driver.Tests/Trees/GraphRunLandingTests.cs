using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CUTOVER1d (D190 point 4; the cutover design §4): a run bound to a graph version lands where its record says, and only the commit its
/// <c>handed</c> is bound to. A real pass of the runtime makes <c>handed</c> active on the session's branch at its tip, read from git, and the
/// door lands that commit; before any pass the door holds and asks for one. A commit made between the gate's judgement and the landing lands
/// nothing, on the branch path and on the merge path, which gains the pin a branch landing already had; the pass after it judges the new tip,
/// and the door lands that.
/// </summary>
/// <remarks>Real git, as the other landing tests, a stand-in for the service's quests and records, and no plugin: nothing reaches a network.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GraphRunLandingTests : AutoLandingFixture
{
    /// <summary>The least a named version's graph holds: <c>claimed</c>, then <c>handed</c>.</summary>
    private const string ClaimedThenHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    private static readonly LandingSubject Work = new("s1", "q1", "Fix the gap");

    /// <summary>No plugin's process: a graph of built-ins sends, asks and tells nothing that answers.</summary>
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

    /// <summary>Run q1 bound to a graph version, under <paramref name="rule"/>, its session s1's tree done with one commit.</summary>
    private async Task<(string Root, string Tree, GrownBranch Branch)> BoundRunAsync(LandingRule rule)
    {
        var root = await RepositoryAsync("engine");
        Rule(rule);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, ClaimedThenHanded), DateTimeOffset.UtcNow);
        return (root, tree, Assert.Single(new SessionBranches(_home).All()));
    }

    /// <summary>One whole pass of the runtime over the run's quest, done, and its session's branch, whose tip is read from git.</summary>
    private async Task PassAsync(string root, GrownBranch branch)
    {
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), new NoPlugins(), () => DateTimeOffset.UtcNow, () => true);
        var pass = await runtime.PassAsync(new NodeRuntimeLook(Config(), Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(
                [RunHome.QuestUnit(RunHome.Quest("q1", "Done")), RunHome.Branch(branch.Unit!, branch.Branch, branch.At, ["q1"])],
                new Dictionary<string, string> { ["engine"] = root })),
            Tips = NodeTips.ReadAsync,
        }, CancellationToken.None);
        Assert.True(pass.Armed);
    }

    private NodeStatus Handed() => new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses["handed"];

    [Fact]
    public async Task The_door_lands_where_the_pass_made_handed_active_and_holds_before_any_pass()
    {
        var (root, tree, branch) = await BoundRunAsync(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);

        var before = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.NotJudged, true), (before.Graph!.State, before.Graph.PressWaits));
        var held = await trees.LandAsync(tree, Work, gate: before);
        Assert.False(held.Landed, held.Message);
        Assert.Equal(AutoLandingCode.Refused, held.Refusal);
        Assert.StartsWith("Daoris has not judged run q1's graph at", held.Message);
        Assert.False(await BranchAsync(root, PushBranch));

        await PassAsync(root, branch);
        var commit = await HeadAsync(tree);
        Assert.Equal((NodeStates.Active, branch.Unit, commit), (Handed().State, Handed().Binding!.Observes, Handed().Binding!.SubjectRevision));

        var gate = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.Lands, commit), (gate.Graph!.State, gate.Tip));
        var landed = await trees.LandAsync(tree, Work, gate: gate);
        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(commit, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
    }

    [Theory]
    [InlineData(LandingForm.Branch)]
    [InlineData(LandingForm.Merge)]
    public async Task A_commit_inserted_between_the_gates_judgement_and_the_landing_lands_nothing(string form)
    {
        var rule = form == LandingForm.Branch ? new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}") : new LandingRule(LandingForm.Merge);
        var (root, tree, branch) = await BoundRunAsync(rule);
        var trees = new SessionTrees(_home);
        await PassAsync(root, branch);
        var judged = await HeadAsync(tree);
        var gate = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.Lands, judged), (gate.Graph!.State, gate.Tip));
        var line = await HeadAsync(root);

        await CommitAsync(tree, "late.txt", "made after the judgement", "a late change");
        var moved = await trees.LandAsync(tree, Work, gate: gate);

        Assert.False(moved.Landed, moved.Message);
        Assert.Equal(AutoLandingCode.Refused, moved.Refusal);
        Assert.Contains("moved after its landing was judged", moved.Message);
        Assert.False(await BranchAsync(root, PushBranch), "nothing is made from a commit nobody judged");
        Assert.Equal(line, await HeadAsync(root));

        // The next pass judges the new tip; the door holds until it has, then lands that commit.
        var late = await HeadAsync(tree);
        Assert.Equal(GraphGateStates.NotJudged, (await trees.GateAsync(tree, "q1", _world, "s1")).Graph!.State);
        await PassAsync(root, branch);
        var again = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.Lands, late), (again.Graph!.State, again.Tip));
        var landed = await trees.LandAsync(tree, Work, gate: again);
        Assert.True(landed.Landed, landed.Message);
        if (form == LandingForm.Branch) Assert.Equal(late, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        else Assert.Equal(late, (await GitAsync(root, "merge-base", "main", late)).Trim());
    }
}
