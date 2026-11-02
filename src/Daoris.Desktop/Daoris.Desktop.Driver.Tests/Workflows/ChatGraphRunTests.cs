using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOWCLEAN1 row 1d (CHATFLOW1f, the chat-flow design §4.5): a conversation's run bound to a graph version has the person's press for its
/// candidate and the runtime for its landing. The candidate the press submitted stands where a quest stands for <c>claimed</c>, a real pass
/// of the runtime judges the run's branch, and the door lands the tip the Accept covered; a later candidate reopens the same run.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ChatGraphRunTests : AutoLandingFixture
{
    private const string Run = "chat-c1";

    private const string ReviewedMerge = """
        {"schemaVersion":2,"entries":["claimed"],"end":"landing",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"review"},{"id":"r>l","kind":"after","from":"review","to":"landing"}]}
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

    private LandingDoor Door() => new(() => new AutoLander(
        _home, _world, new SessionTrees(_home, Plugins()), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add) { Performs = true, HoldsLease = () => true });

    private NodeRecord Record() => new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf(Run))!;

    private async Task<bool> PassAsync(string root, GrownBranch branch, LandingDoor door)
    {
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), new NoPlugins(), () => DateTimeOffset.UtcNow, () => true) { Landing = door };
        var pass = await runtime.PassAsync(new NodeRuntimeLook(Config(), Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(
                [RunHome.Branch(branch.Unit!, branch.Branch, branch.At, [])], new Dictionary<string, string> { ["engine"] = root })),
            Tips = NodeTips.ReadAsync,
        }, CancellationToken.None);
        return pass.Armed;
    }

    /// <summary>A conversation's tree with one commit, its record a chat's (no quest), and its run bound to the reviewed merge.</summary>
    private async Task<(string Root, string Tree, GrownBranch Branch)> ChatAsync()
    {
        var root = await RepositoryAsync("engine");
        Rule(LandingRule.Merge);
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "c1.txt", "the work of c1", "the work of c1");
        _world.Records.Add(new SessionRecord("c1", "engine", "completed") { Tree = opened.Path, Created = DateTimeOffset.UtcNow });
        RunHome.Bind(_home, Run, RunHome.SaveGraph(_home, ReviewedMerge), DateTimeOffset.UtcNow);
        return (root, opened.Path, Assert.Single(new SessionBranches(_home).All()));
    }

    private ChatCandidate Submit(GrownBranch branch, string tip, Func<string, bool>? landed = null) =>
        ChatCandidates.Submit(_home, Run, "engine", "default", branch.Branch, tip, DateTimeOffset.UtcNow, landed ?? (_ => false)).Candidate;

    [Fact]
    public async Task A_press_submits_a_candidate_and_the_runtime_lands_its_tip_after_the_accept_and_not_before()
    {
        var (root, tree, branch) = await ChatAsync();
        var commit = await HeadAsync(tree);
        var door = Door();

        // No press, no candidate: a pass judges the run's nothing, so a turn's end starts no step.
        Assert.False(await PassAsync(root, branch, door));
        Assert.False(File.Exists(NodeRecordFiles.PathOf(_home, RunUnits.IdOf(Run))));

        Assert.Equal(1, Submit(branch, commit).Number);
        await PassAsync(root, branch, door);
        await door.IdleAsync();
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (Record().Statuses["claimed"].State, Record().Statuses["review"].State));
        Assert.Empty(new DeliveryStore(_home).OfUnit(RunUnits.IdOf(Run)));

        Assert.True(RunNodeWords.Word(new NodeRecordFiles(_home, () => true), Run, "review", NodeWordActs.Accept, null).Done);
        await PassAsync(root, branch, door);
        await door.IdleAsync();

        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
        Assert.True(LandOutcomes.Of(RunUnits.IdOf(Run), commit)?.Landed);
    }

    [Fact]
    public async Task A_later_candidate_reopens_the_same_run_and_lands_again_under_the_same_version()
    {
        var (root, tree, branch) = await ChatAsync();
        var door = Door();
        var first = await HeadAsync(tree);
        Submit(branch, first);
        await PassAsync(root, branch, door);
        Assert.True(RunNodeWords.Word(new NodeRecordFiles(_home, () => true), Run, "review", NodeWordActs.Accept, null).Done);
        await PassAsync(root, branch, door);
        await door.IdleAsync();
        Assert.True(LandOutcomes.Of(RunUnits.IdOf(Run), first)?.Landed);

        // The conversation goes on and commits again; a turn's end submits nothing, so claimed stays on candidate 1's tip.
        await CommitAsync(tree, "c1b.txt", "more of c1", "more of c1");
        var second = await HeadAsync(tree);
        await PassAsync(root, branch, door);
        Assert.Equal(first, Record().Statuses["claimed"].Acceptance!.SubjectRevision);

        // The next press submits candidate 2 past the landed one, and claimed observes it at the new tip.
        Assert.Equal(2, Submit(branch, second, tip => tip == first).Number);
        await PassAsync(root, branch, door);
        Assert.Equal(second, Record().Statuses["claimed"].Acceptance!.SubjectRevision);
        Assert.True(RunNodeWords.Word(new NodeRecordFiles(_home, () => true), Run, "review", NodeWordActs.Accept, null).Done);
        await PassAsync(root, branch, door);
        await door.IdleAsync();

        Assert.True(LandOutcomes.Of(RunUnits.IdOf(Run), second)?.Landed);
        Assert.Equal(Run, ChatCandidates.Read(_home, Run)!.Run);
    }

    [Fact]
    public async Task A_press_at_a_new_tip_over_a_submitted_unlanded_candidate_supersedes_it_in_place()
    {
        var (_, tree, branch) = await ChatAsync();
        var first = await HeadAsync(tree);
        var opened = ChatCandidates.Submit(_home, Run, "engine", "default", branch.Branch, first, DateTimeOffset.UtcNow, _ => false);
        Assert.Equal((1, CandidateSubmitted.Opened), (opened.Candidate.Number, opened.What));

        // The same tip submits nothing again.
        Assert.Equal(CandidateSubmitted.Unchanged,
            ChatCandidates.Submit(_home, Run, "engine", "default", branch.Branch, first, DateTimeOffset.UtcNow, _ => false).What);

        await CommitAsync(tree, "c1b.txt", "more of c1", "more of c1");
        var second = await HeadAsync(tree);
        var superseded = ChatCandidates.Submit(_home, Run, "engine", "default", branch.Branch, second, DateTimeOffset.UtcNow, _ => false);
        Assert.Equal((1, CandidateSubmitted.Superseded, second), (superseded.Candidate.Number, superseded.What, superseded.Candidate.Tip));
        Assert.Equal([first], superseded.Candidate.Superseded);
        Assert.Single(ChatCandidates.Read(_home, Run)!.Candidates);
    }

    [Fact]
    public async Task A_graph_that_closes_a_quest_is_bound_with_that_as_its_problem_and_any_other_graph_binds()
    {
        var graph = Config("""{"workflows":{"engine":{"default":"docs"}}}""");
        RunHome.SaveGraph(_home, ReviewedMerge);
        var bound = ChatRuns.Bind(graph, _home, Run, "engine", null, [], DateTimeOffset.UtcNow);
        Assert.Null(bound.Failed);
        Assert.True(GraphRun.Names(bound.Binding!));
        Assert.Null(bound.Binding!.Problem);

        var closing = """
            {"schemaVersion":2,"entries":["land"],"end":"close",
             "nodes":[{"id":"land","kind":"handed","version":1,"reach":"observed"},{"id":"close","kind":"quest.close","version":1,"reach":"performed"}],
             "wires":[{"id":"l>c","kind":"after","from":"land","to":"close"}]}
            """;
        RunHome.SaveGraph(_home, closing);
        var refused = ChatRuns.Bind(graph, _home, "chat-c2", "engine", null, [], DateTimeOffset.UtcNow);
        Assert.Null(refused.Failed);
        Assert.NotNull(refused.Binding!.Problem);
        Assert.Contains("quest.close", refused.Binding.Problem);
    }

    private DriverConfig Config(string json) => DriverConfig.Parse(json);
}
