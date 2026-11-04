using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1q's first proof (the integration design §17.7, §18): the <i>A pull request</i> preset as <c>workflow new --from</c> saves it,
/// bound to a quest's run, over a stand-in landing plugin that pushes to a bare origin. The person's Accept makes the branch and the plugin
/// pushes it; the runtime's pass reaches <c>handed</c> and <c>pushed</c> (git's remote-tracking ref holds the pushed tip), and once the
/// platform completes the pull request, its answer kept on the landing's record reaches <c>pull-request</c>, the flow's end.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PullRequestPresetPluginTests : LandingPluginFixture
{
    private string ConfigPath => DaorisHome.Entry(Home, HomeEntry.DriverConfig);

    [Fact]
    public async Task The_pull_request_preset_run_pushes_through_the_plugin_and_reaches_its_end_when_the_platform_completes_it()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        InstallRealPlugin();
        DriverConfig.Empty.WithLine("engine", "main").Save(ConfigPath);

        var error = new StringWriter();
        var exit = await DriverChoicesCommand.RunAsync(["workflow", "new", "made", "--from", "pull-request"], new DriverChoicesWorld
        {
            Verbs = DriverChoiceVerbs.All,
            Instance = new InstanceWorld { Home = Home, Environment = _ => null, Out = new StringWriter(), Error = error, Verb = "daoris" },
            ConfigPath = ConfigPath,
            Folder = Home,
            Now = () => new DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.Zero),
            Registry = _ => Task.FromResult(new ChoicesRegistry([], null)),
        }, CancellationToken.None);
        Assert.True(exit == 0, error.ToString());
        var version = Assert.Single(new WorkflowFiles(Home).Load("made")!.Read.Versions);
        Assert.True(version.Problem is null, version.Problem);
        Assert.True(WorkflowRunBindings.Bind(Home,
            new WorkflowRunBinding("q1", "engine", "aurora", "made", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch) { Version = 1, Digest = version.Digest }));
        var process = WorkflowProcesses.Read(Home, DriverConfig.Load(ConfigPath), "engine", "aurora", "q1", new AcceptedGates([]));
        Assert.True(process is { Problem: null, Named: true }, process.Problem);
        var gate = new LandingGate(new OpinionGateState(OpinionGateStates.None, "engine"),
            new ReviewGateState(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = "engine" }))
        {
            Workflow = new WorkflowGateState(WorkflowGateStates.Follows, process),
        };

        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "q1.txt", "the work of q1\n", "the work of q1");
        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s-q1", "q1", "Fix the gap"), acceptedBy: AcceptedBy.Person, gate: gate);
        Assert.True(landed.Landed, landed.Message);
        var branch = landed.Branch!;
        var tip = (await GitAsync(root, "rev-parse", branch)).Trim();
        Assert.Equal(tip, (await GitAsync(origin, "rev-parse", branch)).Trim());

        var quest = new WorkUnit("quest:q1", WorkUnitKind.Quest, new WorkOwner("engine", "aurora", WorkRecord.Quests, WorkRecord.ServiceWriter),
            new WorkSubject("#q1", null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
        {
            Records = new WorkUnitRecords(null, [], new QuestView("q1", "", "engine", "Fix the gap", "", "Done")),
        };
        WorkUnit Branch() => new("branch-a", WorkUnitKind.Branch, new WorkOwner("engine", "aurora", "session-branches.json", WorkRecord.DriverWriter),
            new WorkSubject(branch, tip), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
        {
            Links = [new WorkLink(WorkRelation.ProducedBy, "quest:q1")],
            Records = new WorkUnitRecords(null, [trees.Recorded.Of("engine", branch)!], null),
        };

        var files = new NodeRecordFiles(Home, () => true);
        var runtime = new NodeRuntime(files, new ObservedPort(), () => DateTimeOffset.UtcNow, () => true);
        Task Pass() => runtime.PassAsync(new NodeRuntimeLook(DriverConfig.Load(ConfigPath), true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead([quest, Branch()], new Dictionary<string, string> { ["engine"] = root })),
            Bound = NodeRuntime.BoundIn(Home),
            Tips = NodeTips.ReadAsync,
            Remotes = NodeTips.RemotesAsync,
            Holds = NodeTips.HoldsAsync,
        }, CancellationToken.None);
        string State(string node) => files.Read(RunUnits.IdOf("q1"))!.Statuses[node].State;

        await Pass();

        Assert.Equal((NodeStates.Reached, NodeStates.Reached), (State("landing"), State("pushed")));
        Assert.NotEqual(NodeStates.Reached, State("pull-request"));

        // The platform completes the pull request: its answer, kept on the landing's record, as the plugin's state hook gives it.
        var answer = new PullRequestState(PullRequestStates.Completed)
        {
            PullRequest = "https://example.test/example-org/engine/pull/7", MergeCommit = tip, SourceCommit = tip, Target = "main", Plugin = Id,
            AskedAt = DateTimeOffset.UtcNow,
        };
        trees.Recorded.Answered(trees.Recorded.Of("engine", branch)!, answer);

        await Pass();

        Assert.Equal(NodeStates.Reached, State("pull-request"));
    }
}
