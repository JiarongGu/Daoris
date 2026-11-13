using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A branch already landed, pushed and carrying a pull request moves on after a second Accept: the <c>push</c> that follows the landing is started at the
/// moved tip, and the plugin's <c>pull-request</c> step is performed again at it, so the platform sees the new head. The push act and the plugin are stand-ins
/// that record what they were asked; the landing, the review and the runtime are the real ones.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReLandingPullRequestTests : GraphRunFixture
{
    private const string Plugin = "acme.host";

    private static readonly NodeDeclaration PullRequest = new(Plugin + "/pull-request", Plugin + "/pull-request", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    private const string Graph = $$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"pr",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{{BranchLand}}},
                  {"id":"push","kind":"push","version":1,"reach":"performed"},
                  {"id":"pr","kind":"{{Plugin}}/pull-request","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"review"},{"id":"r>l","kind":"after","from":"review","to":"landing"},
                  {"id":"l>p","kind":"after","from":"landing","to":"push"},{"id":"p>pr","kind":"after","from":"push","to":"pr"}]}
        """;

    /// <summary>The push act's stand-in: git pushes the branch to the checkout's origin and the landing record keeps it, as the real door's done push does.</summary>
    private sealed class PushStandIn(string home) : IPushDoor
    {
        public List<(string Branch, string Tip)> Pushed { get; } = [];

        public bool Start(PushStart start)
        {
            lock (Pushed) Pushed.Add((start.Branch, start.Tip));
            GitAsync(start.Root, "push", "--quiet", "origin", $"{start.Tip}:refs/heads/{start.Branch}").GetAwaiter().GetResult();
            GitAsync(start.Root, "fetch", "--quiet", "origin").GetAwaiter().GetResult();
            new LandedBranches(home).Pushed(start.Repository, start.Branch, start.Tip);
            return true;
        }
    }

    /// <summary>How far past now the runtime's clock reads: the first attempt's wait for its plugin has lapsed by the time the line moves.</summary>
    private TimeSpan _later = TimeSpan.Zero;

    private LandingDoor Door() => new(() => new LandPerformer(
        _home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add) { HoldsLease = () => true });

    private async Task PassAsync(string root, LandingDoor door, PushStandIn push, ObservedPort plugin)
    {
        var sessions = _world.Records;
        var branches = WorkInventory.Units(await WorkInventories.GatherAsync(
            _home, new Dictionary<string, string>(), new HashSet<string>(), new List<string>(), recorded: WorkInventories.RecordedOf(sessions)));
        var units = WorkUnits.Linked(branches, QuestUnits.Read([.. _world.Quests.Values], sessions), [], sessions);
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), plugin, () => DateTimeOffset.UtcNow + _later, () => true)
        {
            Doors = NodeDoors.None.With(NodeKinds.Land, door).With(NodeKinds.Push, push),
        };
        var pass = await runtime.PassAsync(new NodeRuntimeLook(Config(), Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(units, new Dictionary<string, string> { ["engine"] = root })),
            Steps = () => [PullRequest],
            Tips = NodeTips.ReadAsync,
            Remotes = NodeTips.RemotesAsync,
            Content = (unit, tip, ct) => sessions.LastOrDefault(each => unit.Executors.Contains(each.Id, StringComparer.Ordinal) && Directory.Exists(each.Tree))?.Tree is { } tree
                ? HoldReads.ContentAsync(new SessionTrees(_home), tree, tip, ct)
                : Task.FromResult<HoldContent?>(null),
        }, CancellationToken.None);
        Assert.True(pass.Armed);
        await door.IdleAsync();
    }

    [Fact]
    public async Task A_second_accept_pushes_the_moved_branch_and_the_pull_request_step_is_performed_at_its_new_head()
    {
        var root = await RepositoryAsync("engine");
        var origin = Path.Combine(_home, "origin.git");
        await GitAsync(root, "init", "--quiet", "--bare", origin);
        await GitAsync(root, "remote", "add", "origin", origin);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        await CommitAsync(tree, "s2.txt", "the work of s2", "the work of s2");
        var made = _world.Records.Single(record => record.Id == "s1").Created;
        _world.Records.Add(new SessionRecord("s2", "engine", "completed") { Quest = "q1", Tree = tree, Created = made.AddSeconds(1) });
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, Graph), made);
        var files = new NodeRecordFiles(_home, () => true);
        var door = Door();
        var push = new PushStandIn(_home);
        var plugin = new ObservedPort { Reconciles = _ => new WorkReconcileAnswer(ReconcileStates.NotStarted) };

        await PassAsync(root, door, push, plugin);
        Assert.True(RunNodeWords.Word(files, "q1", "review", NodeWordActs.Accept, null).Done);
        for (var pass = 0; pass < 3; pass++) await PassAsync(root, door, push, plugin);
        var first = await HeadAsync(tree);
        Assert.Equal(first, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Equal([(PushBranch, first)], push.Pushed);
        var opened = Assert.Single(plugin.Sent);
        Assert.Equal(first, opened.SubjectRevision);

        // The line moves and the later part merges it; the person accepts that merge.
        await CommitAsync(root, "line.txt", "the line moved", "the line moved");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "merge", "--no-ff", "--no-edit", "main");
        var merged = await HeadAsync(tree);
        _later = TimeSpan.FromHours(2);
        await PassAsync(root, door, push, plugin);
        Assert.True(RunNodeWords.Word(files, "q1", "review", NodeWordActs.Accept, null).Done);
        for (var pass = 0; pass < 7; pass++) await PassAsync(root, door, push, plugin);

        Assert.Equal(merged, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Equal([(PushBranch, first), (PushBranch, merged)], push.Pushed);

        // The plugin is asked at each head in turn; this stand-in never acknowledges, so a delivery is repeated at its own head.
        Assert.Equal([first, merged], plugin.Sent.Select(call => call.SubjectRevision).Distinct());
        Assert.Equal(merged, files.Read(RunUnits.IdOf("q1"))!.Statuses["pr"].Binding!.SubjectRevision);
    }
}
