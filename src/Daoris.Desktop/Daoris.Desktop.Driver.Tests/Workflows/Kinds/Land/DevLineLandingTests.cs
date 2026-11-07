using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run bound to a saved copy of <i>Merge automatically</i> (work, requests, holds, content, landing by merge, line) lands by itself while its
/// session's tree stands with a commit beyond the line: the loop's passes, with the ref watch between them as the loop runs it, make its
/// <c>land</c> active at the tree's tip and the door merges it, with no press.
/// </summary>
/// <remarks>Real git, the landing tests' stand-in for the service's quests and records, and no plugin: nothing reaches a network.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class DevLineLandingTests : ReviewLandingFixture
{
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

    private static readonly string[] Steps = ["work", "requests", "holds", "content", "landing", "line"];

    [Theory]
    [InlineData("plain")]
    [InlineData("line-moved")]
    [InlineData("remote")]
    [InlineData("ignored")]
    [InlineData("quest-moved")]
    [InlineData("own-branch")]
    [InlineData("moved-on")]
    public async Task A_dev_line_run_whose_tree_stands_with_a_commit_beyond_its_line_lands_with_no_press(string shape)
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        var root = await RepositoryAsync("engine");
        if (shape == "ignored") await CommitAsync(root, ".gitignore", "bin/\n", "ignore the build");
        await DevLineAsync();
        var tree = await NamedDoneAsync(root, "s1", "q1", ownBranch: shape == "own-branch");
        var commit = await HeadAsync(tree);
        Assert.NotEqual(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
        var door = Door();
        switch (shape)
        {
            case "line-moved":
                await CommitAsync(root, "other.txt", "another session's work", "another session's work");
                break;
            case "remote":
                var origin = Path.Combine(_scratch, "origin.git");
                await GitAsync(_scratch, "init", "--bare", origin);
                await GitAsync(root, "remote", "add", "origin", origin);
                await GitAsync(root, "push", "origin", "main");
                await GitAsync(root, "fetch", "origin");
                break;
            case "ignored":
                Directory.CreateDirectory(Path.Combine(tree, "bin"));
                await File.WriteAllTextAsync(Path.Combine(tree, "bin", "out.dll"), "built\n");
                break;
            case "moved-on":
                // The agent moved its tree onto a branch of its own after its first commit, and committed more there (TREEFOLLOW1).
                await GitAsync(tree, "checkout", "--quiet", "-b", "feature/own");
                await CommitAsync(tree, "own.txt", "more work of s1", "more work of s1");
                break;
            case "quest-moved":
                _world.Quests["q1"] = _world.Quests["q1"] with { Status = "Taken", Updated = DateTimeOffset.UtcNow.AddMinutes(-5) };
                await InventoryPassAsync(root, door);
                _world.Quests["q1"] = _world.Quests["q1"] with { Status = "Done", Updated = DateTimeOffset.UtcNow };
                break;
        }

        // The loop's turn: the look learns where each tree is (TREEFOLLOW1), the ref watch keeps what the line holds, then a pass judges the runs
        // and starts their doors.
        for (var turn = 0; turn < 3; turn++)
        {
            await new TreeFollows(new TreeContext(_home, new LandingPlugins(_home))).LearnTreesAsync("aurora", "engine", CancellationToken.None, root);
            await new SessionTrees(_home).KeepEndsAsync(root, "engine", "aurora");
            await InventoryPassAsync(root, door);
            await door.IdleAsync();
        }

        // A line that moved replays the work onto it, under another commit; the line holds the session's file either way.
        Assert.True((await GitAsync(root, "show", "main:s1.txt")).Trim() == "the work of s1", Said());
        if (shape == "moved-on") Assert.True((await GitAsync(root, "show", "main:own.txt")).Trim() == "more work of s1", Said());
        var said = Said();
        Assert.True(said.StartsWith(string.Join(", ", Steps.Select(node => $"{node} {NodeStates.Reached}")) + "\n", StringComparison.Ordinal), said);

        // Its merge is requested through the automatic landing's own door, for the branch its work stood on, and settled in its words, never as
        // the person's press.
        var store = new IntegrationStore(_home);
        var grown = Assert.Single(new SessionBranches(_home).All());
        var request = Assert.Single(store.Of("engine", grown.Now ?? grown.Branch));
        Assert.Equal((IntegrationRequester.Landing, IntegrationRequestCloses.Settled), (request.Requester, request.ClosedWhy));
        Assert.Equal("merged by its workflow's landing, with no press.", store.MemberOf(request.Id)?.Said);
    }

    /// <summary>The run's steps as its record holds them, each with its state and the revision it is bound to.</summary>
    private string Said() => (new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1")) is { } record
        ? string.Join(", ", Steps.Select(node => record.Statuses.GetValueOrDefault(node) is { } status ? $"{node} {status.State}" : $"{node} none"))
          + "\nbound at " + string.Join(", ", Steps.Select(node => $"{node} {record.Statuses.GetValueOrDefault(node)?.Binding?.SubjectRevision ?? "-"}"))
        : "no record") + "\n" + string.Join("\n", _passes);

    /// <summary>A version copied from the default <i>Merge automatically</i>, as <c>new --from</c> copies it, saved, and the run bound to it.</summary>
    private async Task DevLineAsync()
    {
        var source = await WorkflowOperations.SourceAsync(Config(), _home, _ => Task.FromResult(new ChoicesRegistry(null, "unread")),
            "merge-automatic", opinion: false, look: false, CancellationToken.None);
        var copied = WorkflowOperations.PlanGraph(_home, null, "dev-line", null, source.Name, "terminal", "2026-11-01T09:00:00Z", source.Graph, source.From);
        WorkflowPorts.Store(_home).Write("dev-line", copied.Plan.Result!);
        RunHome.Bind(_home, "q1", copied.Next!.Digest!, DateTimeOffset.UtcNow, workflow: "dev-line");
    }

    /// <summary>The runtime's door, landing as the performer does: over the fixture's world, trees and faked plugin.</summary>
    private LandingDoor Door() => new(() => new LandPerformer(
        _home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add) { HoldsLease = () => true });

    /// <summary>
    /// One whole pass of the runtime over the units the home's records and the stand-in service name, linked as the loop's pass reads them, and
    /// what the line holds read in a unit's newest session's tree, as the loop's gates read it.
    /// </summary>
    private async Task InventoryPassAsync(string root, LandingDoor door)
    {
        var sessions = _world.Records;
        var branches = WorkInventory.Units(await WorkInventories.GatherAsync(
            _home, new Dictionary<string, string>(), new HashSet<string>(), new List<string>(), recorded: WorkInventories.RecordedOf(sessions)));
        var units = WorkUnits.Linked(branches, QuestUnits.Read([.. _world.Quests.Values], sessions), [], sessions);
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), new NoPlugins(), () => DateTimeOffset.UtcNow, () => true) { Doors = NodeDoors.None.With(NodeKinds.Land, door) };
        var pass = await runtime.PassAsync(new NodeRuntimeLook(Config(), Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(units, new Dictionary<string, string> { ["engine"] = root })),
            Tips = NodeTips.ReadAsync,
            Content = (unit, tip, ct) => sessions.LastOrDefault(each => unit.Executors.Contains(each.Id, StringComparer.Ordinal) && Directory.Exists(each.Tree))?.Tree is { } tree
                ? HoldReads.ContentAsync(new SessionTrees(_home), tree, tip, ct)
                : Task.FromResult<HoldContent?>(null),
        }, CancellationToken.None);
        _passes.Add($"armed {pass.Armed}: {string.Join(" | ", pass.Said)}");
    }

    private readonly List<string> _passes = [];
}
