using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-RUN1b over real git (NODE1's choice 8; D88's PROC1d-ii): the runtime's own reads for a branch unit's <c>pushed</c> and an ahead
/// <c>pull-request</c>, <see cref="NodeTips.RemotesAsync"/> and <see cref="NodeTips.HoldsAsync"/>. The remote-tracking ref is one
/// <c>for-each-ref</c> of the checkout a pass, and the ancestry one <c>merge-base --is-ancestor</c>.
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

    /// <summary>Run <c>q1</c>'s graph: the work landed on a branch the plugin pushes, then notifying once the pull request is reached.</summary>
    private const string Cycle = """
        {"schemaVersion":2,"entries":["claimed"],"end":"notify",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"pull-request","plugin":"acme.deploy","pattern":"feature/{quest}-{slug}"}},
                  {"id":"pushed","kind":"pushed","version":1,"reach":"observed"},{"id":"pull-request","kind":"pull-request","version":1,"reach":"observed"},
                  {"id":"notify","kind":"acme.deploy/notify","version":1,"reach":"performed"}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"},{"id":"l>p","kind":"after","from":"landing","to":"pushed"},
                  {"id":"p>pr","kind":"after","from":"pushed","to":"pull-request"},{"id":"w1","kind":"after","from":"pull-request","to":"notify"}]}
        """;

    [Fact]
    public async Task Pushed_reads_the_remote_tracking_ref_and_an_ahead_pull_request_gits_ancestry_once_a_pass()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", Branch);
        var landed = (await GitAsync(root, "rev-parse", Branch)).Trim();
        await GitAsync(root, "branch", "daoris/s1", landed);
        await GitAsync(root, "update-ref", "refs/remotes/origin/" + Branch, landed);
        await CommitAsync(root, "ahead.txt", "ahead\n", "the platform's merge took a newer commit");
        var source = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        var completed = new PullRequestState(PullRequestStates.Completed) { MergeCommit = source, SourceCommit = source, Target = "main", Plugin = Plugin };
        var landing = RunHome.Landing("unit-a", Branch, landed, DateTimeOffset.UtcNow, Run) with
        {
            Pushed = true, PushedTip = landed, Plugin = Plugin, PullRequestState = completed,
        };
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
            Holds = NodeTips.HoldsAsync,
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
        Assert.True(record.Statuses["pushed"].State == NodeStates.Reached, string.Join(", ", record.Statuses.Select(each => $"{each.Key}={each.Value.State}/{each.Value.Code}")));
        Assert.Equal(NodeStates.Reached, record.Statuses["pull-request"].State);
        Assert.Equal(landed, Assert.Single(port.Sent).SubjectRevision);
        // One read of the checkout's refs for the session branch's tip (the landing is judged on it) and one for the remote-tracking refs.
        Assert.Equal(2, started.Count(line => line.StartsWith("for-each-ref", StringComparison.Ordinal)));
        Assert.Equal($"merge-base --is-ancestor {landed} {source}", Assert.Single(started, line => line.StartsWith("merge-base", StringComparison.Ordinal)));
        Assert.Equal(3, started.Count);
    }
}
