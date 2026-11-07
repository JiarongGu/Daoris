using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-RUN1b over real git (NODE1's choice 8; D88's PROC1d-ii): the runtime's own read for a branch unit's <c>pushed</c>,
/// <see cref="NodeTips.RemotesAsync"/>. The remote-tracking ref is read from the checkout's ref files, and git is asked no ancestry.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class NodeGitReadsGitTests : LandedFixture
{
    private const string Plugin = "acme.deploy";
    private const string Branch = "feature/q1-ship";
    private const string Run = "r1";

    private static readonly NodeDeclaration Notify = new(Plugin + "/notify", Plugin + "/notify", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    /// <summary>Run <c>q1</c>'s graph: the work landed on a branch, pushed, then notifying once the push is reached.</summary>
    private const string Cycle = """
        {"schemaVersion":2,"entries":["claimed"],"end":"notify",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"feature/{quest}-{slug}"}},
                  {"id":"push","kind":"push","version":1,"reach":"performed"},
                  {"id":"notify","kind":"acme.deploy/notify","version":1,"reach":"performed"}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"},{"id":"l>p","kind":"after","from":"landing","to":"push"},
                  {"id":"w1","kind":"after","from":"push","to":"notify"}]}
        """;

    [Fact]
    public async Task Push_reads_the_remote_tracking_ref_once_a_pass_and_git_reads_no_ancestry()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", Branch);
        var landed = (await GitAsync(root, "rev-parse", Branch)).Trim();
        await GitAsync(root, "branch", "daoris/s1", landed);
        await GitAsync(root, "update-ref", "refs/remotes/origin/" + Branch, landed);
        var landing = RunHome.Landing("unit-a", Branch, landed, DateTimeOffset.UtcNow, Run) with { Pushed = true, PushedTip = landed };
        var unit = RunHome.Branch("unit-a", "daoris/s1", DateTimeOffset.UtcNow.AddHours(-1), [Run], landing) with { Subject = new WorkSubject("daoris/s1", landed) };
        RunHome.Bind(Home, Run, RunHome.SaveGraph(Home, Cycle), DateTimeOffset.UtcNow);

        var port = new ObservedPort();
        var records = new NodeRecordFiles(Home, () => true);
        var runtime = new NodeRuntime(records, port, () => DateTimeOffset.UtcNow, () => true);
        var look = new NodeRuntimeLook(DriverConfig.Empty, Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead([RunHome.QuestUnit(RunHome.Quest(Run, "Done")), unit], new Dictionary<string, string> { ["engine"] = root })),
            Steps = () => [Notify],
            Bound = NodeRuntime.BoundIn(Home),
            Tips = NodeTips.ReadAsync,
            Remotes = NodeTips.RemotesAsync,
        };

        var started = new List<string>();
        WorkingTree.Started.Value = arguments =>
        {
            lock (started) started.Add(string.Join(' ', arguments));
        };
        try
        {
            await runtime.PassAsync(look, CancellationToken.None);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        var record = records.Read(RunUnits.IdOf(Run))!;
        Assert.True(record.Statuses["push"].State == NodeStates.Reached, string.Join(", ", record.Statuses.Select(each => $"{each.Key}={each.Value.State}/{each.Value.Code}")));
        Assert.Equal(landed, Assert.Single(port.Sent).SubjectRevision);
        // The session branch's tip (the landing is judged on it) and the remote-tracking ref are read from the checkout's ref files.
        Assert.Empty(started);
    }
}
