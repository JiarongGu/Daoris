using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the tests of a run bound to a graph version share: the least graph a run lands by, a run bound to it, and a pass of the runtime over it.</summary>
/// <remarks>Real git, as the other landing tests, a stand-in for the service's quests and records, and no plugin: nothing reaches a network.</remarks>
public abstract class GraphRunFixture : ReviewLandingFixture
{
    /// <summary>The least a graph run lands by: <c>claimed</c>, then a <c>land</c> by <paramref name="land"/>'s options.</summary>
    protected static string ClaimedThenLand(string land) => $$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"landing",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{{land}}}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
        """;

    protected static readonly LandingSubject Work = new("s1", "q1", "Fix the gap");

    /// <summary>No plugin's process: a graph of built-ins sends, asks and tells nothing that answers.</summary>
    protected sealed class NoPlugins : IWorkRuntimePort
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

    /// <summary>Run q1 bound to a graph version landing by <paramref name="land"/>'s options, its session s1's tree done with one commit.</summary>
    protected async Task<(string Root, string Tree, GrownBranch Branch)> BoundRunAsync(string land)
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, ClaimedThenLand(land)), DateTimeOffset.UtcNow);
        return (root, tree, Assert.Single(new SessionBranches(_home).All()));
    }

    /// <summary>
    /// One whole pass of the runtime over the run's quest, done (held for the person's yes where <paramref name="held"/>), and its session's branch,
    /// whose tip is read from git; what the line holds of it read in <paramref name="tree"/>, where given.
    /// </summary>
    protected async Task PassAsync(string root, GrownBranch branch, string? tree = null, bool held = false, ILandingDoor? door = null)
    {
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), new NoPlugins(), () => DateTimeOffset.UtcNow, () => true) { Doors = NodeDoors.None.With(NodeKinds.Land, door) };
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

    /// <summary>A branch made for the person, no plugin asked.</summary>
    protected const string BranchLand = """{"method":"branch","pattern":"feature/{quest}-{slug}"}""";

    protected static string? CodeOf(LandingLine line) => line.Data.First(each => each.Key == "code").Value as string;
}
