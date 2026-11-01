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

    /// <summary>
    /// One whole pass of the runtime over the run's quest, done (held for the person's yes where <paramref name="held"/>), and its session's branch,
    /// whose tip is read from git; what the line holds of it (LANDNODE1k) read in <paramref name="tree"/>, where given.
    /// </summary>
    private async Task PassAsync(string root, GrownBranch branch, string? tree = null, bool held = false)
    {
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), new NoPlugins(), () => DateTimeOffset.UtcNow, () => true);
        var pass = await runtime.PassAsync(new NodeRuntimeLook(Config(), Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(
                [RunHome.QuestUnit(RunHome.Quest("q1", "Done") with { Held = held }), RunHome.Branch(branch.Unit!, branch.Branch, branch.At, ["q1"])],
                new Dictionary<string, string> { ["engine"] = root })),
            Tips = NodeTips.ReadAsync,
            Content = (_, tip, ct) => tree is null ? Task.FromResult<HoldContent?>(null) : HoldReads.ContentAsync(new SessionTrees(_home), tree, tip, ct),
        }, CancellationToken.None);
        Assert.True(pass.Armed);
    }

    private NodeStatus Handed() => Status("handed");

    private NodeStatus Status(string node) => new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses[node];

    /// <summary>A version copied from the default <paramref name="from"/>, as <c>new --from</c> copies it, saved, and the run bound to it.</summary>
    private async Task<NodeGraph> CopyAsync(string from)
    {
        var source = await WorkflowOperations.SourceAsync(Config(), _home, _ => Task.FromResult(new ChoicesRegistry(null, "unread")),
            from, opinion: false, look: false, CancellationToken.None);
        var copied = WorkflowOperations.PlanGraph(_home, null, "docs", null, source.Name, "terminal", "2026-11-01T09:00:00Z", source.Graph, source.From);
        WorkflowPorts.Store(_home).Write("docs", copied.Plan.Result!);
        RunHome.Bind(_home, "q1", copied.Next!.Digest!, DateTimeOffset.UtcNow);
        return copied.Next.Graph!;
    }

    /// <summary>
    /// LANDNODE1k: the quest's own holds are a step of the default, which the run's pass keeps active while the done is held for the person's yes;
    /// the look's automatic landing judges nothing of the quest itself and holds at the graph, in the step's words, landing nothing.
    /// </summary>
    [Fact]
    public async Task A_held_done_holds_a_defaults_run_at_its_quest_holds_step_and_the_look_says_it()
    {
        var root = await RepositoryAsync("engine");
        Rule(LandingRule.Merge with { AutoAccept = true });
        var tree = await DoneSessionAsync(root, "s1", "q1", held: true);
        await CopyAsync("merge-automatic");
        var line = await HeadAsync(root);

        await PassAsync(root, Assert.Single(new SessionBranches(_home).All()), tree, held: true);
        Assert.Equal((NodeStates.Active, NodeStates.Blocked), (Status("holds").State, Status("landing").State));

        var said = await LookAsync();
        Assert.Contains(said, each => each.Contains("waits on `holds` (quest.holds)", StringComparison.Ordinal));
        Assert.Equal(line, await HeadAsync(root));
    }

    /// <summary>LANDNODE1k: a version that holds no `quest.holds` lands a done held for the person's yes, as it is configured to: the look judges none.</summary>
    [Fact]
    public async Task A_version_without_the_holds_lands_a_held_done_with_no_press()
    {
        var root = await RepositoryAsync("engine");
        Rule(LandingRule.Merge with { AutoAccept = true });
        var tree = await DoneSessionAsync(root, "s1", "q1", held: true);
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"landing",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
             "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
            """), DateTimeOffset.UtcNow);

        await PassAsync(root, Assert.Single(new SessionBranches(_home).All()), tree, held: true);
        var commit = await HeadAsync(tree);
        Assert.Equal(NodeStates.Active, Status("landing").State);

        await LookAsync();
        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
    }

    [Fact]
    public async Task The_door_lands_where_the_pass_made_handed_active_and_holds_before_any_pass()
    {
        var (root, tree, branch) = await BoundRunAsync(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);

        var before = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.NotJudged, true), (before.Graph!.State, before.GraphPart()!.PressWaits));
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

    /// <summary>
    /// DEFAULTCONFIG1a: a version copied from <i>Merge automatically</i>, as <c>new --from</c> copies it, holds no review, so once its work's
    /// requests are answered a pass readies its landing with no Accept, and the door merges the session's work into the line.
    /// </summary>
    [Fact]
    public async Task A_version_copied_from_merge_automatically_lands_the_session_with_no_press()
    {
        var root = await RepositoryAsync("engine");
        Rule(LandingRule.Merge);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        Assert.DoesNotContain((await CopyAsync("merge-automatic")).Nodes, node => node.Kind == NodeKinds.Review);
        var branch = Assert.Single(new SessionBranches(_home).All());
        var trees = new SessionTrees(_home);

        // LANDNODE1k: its holds are steps the pass judges, what the line holds read in the session's tree.
        await PassAsync(root, branch, tree);
        var commit = await HeadAsync(tree);
        var gate = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.Lands, commit), (gate.Graph!.State, gate.Tip));
        var landed = await trees.LandAsync(tree, Work, gate: gate);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
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
