using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// End to end over real git: a run's passes over the shipped chain (<c>land</c>, <c>push</c>, a plugin's <c>pull-request</c>, a
/// <c>monitor</c>, a <c>tidy</c>) reach the monitor when its request merged, start the tidy on the merged route, and leave the tidy reached
/// with the chain's part ended on a recovery ref and the landing's record; a request closed without merging reaches the monitor and routes to a
/// person's task, touching nothing.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TidyRunGitTests : ReviewLandingFixture
{
    private const string Address = "https://example.test/org/engine/pull/7";

    private const string Wires = """{"id":"m","kind":"after","from":"pull-request","to":"monitor"},""" + MonitorNodeTests.Routes;

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

    private sealed record Chain(string Root, string Tree, GrownBranch Part, string Tip, string Digest, LandedBranch Landing);

    /// <summary>Run q1 bound to the shipped chain, its part's work landed on a branch and pushed, its request opened and answered as <paramref name="answered"/>.</summary>
    private async Task<Chain> ChainAsync(string answered)
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var part = Assert.Single(new SessionBranches(_home).All());
        var tip = (await GitAsync(root, "rev-parse", part.Branch)).Trim();
        await GitAsync(root, "branch", "feature/q1-fix", tip);
        var digest = RunHome.SaveGraph(_home, MonitorNodeTests.Graph(MonitorNodeTests.Watching, Wires));
        RunHome.Bind(_home, "q1", digest, DateTimeOffset.UtcNow);
        var landing = new LandedBranch("engine", "default", "feature/q1-fix", "main", tip, "s1", "q1", "Fix", DateTimeOffset.UtcNow)
        {
            PullRequest = Address, Plugin = "acme-host", PullRequestState = new PullRequestState(answered)
            {
                // A completed answer is whole on the wire: the commit the merge put on its target, and the source commit it took.
                PullRequest = Address, Plugin = "acme-host", Target = "main", At = DateTimeOffset.UtcNow,
                MergeCommit = answered == PullRequestStates.Completed ? (await GitAsync(root, "rev-parse", "main")).Trim() : null,
                SourceCommit = answered == PullRequestStates.Completed ? tip : null,
                How = answered == PullRequestStates.Completed ? MergeHow.Squash : null,
            },
            Units = [part.Unit!],
        };
        new LandedBranches(_home).Record(landing);
        var unit = RunUnits.IdOf("q1");
        var branch = RunHome.Branch(part.Unit!, part.Branch, part.At, ["q1"]).Id;
        NodeStatus Reached(string node, NodePort evidence) => new(unit, node, NodeStates.Reached)
        {
            Attempt = 1, Tries = 1, Acceptance = new NodeAcceptance(unit, tip, digest, node, 1, 1) { Evidence = evidence, Subject = node == "landing" ? branch : null },
        };

        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["landing"] = Reached("landing", new NodePort("daoris/landing@1", "landing-1", tip, null)),
                ["push"] = Reached("push", new NodePort("daoris/push@1", "push-1", tip, null)),
                ["pull-request"] = Reached("pull-request", new NodePort("acme-host/receipt@1", Address, null, null)),
            },
        });
        return new Chain(root, tree, part, tip, digest, landing);
    }

    private async Task PassAsync(Chain chain, TidyDoor door)
    {
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), new NoPlugins(), () => DateTimeOffset.UtcNow, () => true)
        {
            Doors = NodeDoors.None.With(NodeKinds.Tidy, door),
        };
        var pass = await runtime.PassAsync(new NodeRuntimeLook(Config(), Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(
                [RunHome.QuestUnit(RunHome.Quest("q1", "Done")), RunHome.Branch(chain.Part.Unit!, chain.Part.Branch, chain.Part.At, ["q1"], new LandedBranches(_home).Of("engine", "feature/q1-fix")!)],
                new Dictionary<string, string> { ["engine"] = chain.Root })),
            Tips = NodeTips.ReadAsync,
            Content = (_, _, _) => Task.FromResult<HoldContent?>(null),
        }, CancellationToken.None);
        Assert.True(pass.Armed);
        await door.IdleAsync();
    }

    private NodeStatus Status(string node) =>
        new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses.GetValueOrDefault(node) ?? new NodeStatus(RunUnits.IdOf("q1"), node, NodeStates.Blocked);

    // A node is made ready by one pass and reached by a later one: the passes a run takes are the loop's, as many as the chain needs.
    private async Task PassesUntilAsync(Chain chain, TidyDoor door, string node)
    {
        var seen = new List<string>();
        for (var at = 0; at < 6 && Status(node).State != NodeStates.Reached; at++)
        {
            await PassAsync(chain, door);
            seen.Add(Dump());
        }

        Assert.True(Status(node).State == NodeStates.Reached, string.Join("\n", seen));
    }

    private string Dump() => string.Join(", ", new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses.Select(each => $"{each.Key}={each.Value.State} {each.Value.Words}"))
                             + $" | landing state {new LandedBranches(_home).Of("engine", "feature/q1-fix")?.PullRequestState?.State}";

    [Fact]
    public async Task A_merged_request_reaches_the_monitor_and_the_next_passes_end_the_chains_part_with_the_tidy_reached()
    {
        var chain = await ChainAsync(PullRequestStates.Completed);
        var door = new TidyDoor(_home);

        await PassesUntilAsync(chain, door, "monitor");
        Assert.Equal(PullRequestReading.Merged, Status("monitor").Acceptance!.Outcome);
        Assert.NotEqual(NodeStates.Reached, Status("tidy").State);

        await PassesUntilAsync(chain, door, "tidy");

        var tidied = new LandedBranches(_home).Of("engine", "feature/q1-fix")!.Tidied!;
        var part = Assert.Single(tidied.Parts);
        Assert.Equal((chain.Part.Branch, chain.Tip), (part.Branch, part.Tip));
        Assert.StartsWith("refs/daoris/discarded/", part.KeptOn, StringComparison.Ordinal);
        Assert.Equal(NodeStates.Reached, Status("tidy").State);
        Assert.NotEqual(NodeStates.Reached, Status("closed").State);
    }

    [Fact]
    public async Task A_request_closed_without_merging_reaches_the_monitor_routes_to_the_persons_task_and_ends_nothing()
    {
        var chain = await ChainAsync(PullRequestStates.Abandoned);
        var door = new TidyDoor(_home);

        await PassesUntilAsync(chain, door, "monitor");
        await PassAsync(chain, door);
        await PassAsync(chain, door);

        Assert.Equal(PullRequestReading.Closed, Status("monitor").Acceptance!.Outcome);
        Assert.Null(new LandedBranches(_home).Of("engine", "feature/q1-fix")!.Tidied);
        Assert.NotEqual(NodeStates.Reached, Status("tidy").State);
        Assert.Equal(chain.Tip, (await GitAsync(chain.Root, "rev-parse", chain.Part.Branch)).Trim());
    }
}
