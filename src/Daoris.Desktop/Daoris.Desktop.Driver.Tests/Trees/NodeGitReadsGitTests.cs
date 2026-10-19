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

    private static readonly NodeDeclaration Notify = new(Plugin + "/notify", Plugin + "/notify", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    /// <summary>Branches landed on a branch the plugin pushes, the cycle notifying once the pull request is reached.</summary>
    private const string Cycle = """
        { "landings": { "engine": { "form": "branch", "pattern": "feature/{quest}-{slug}", "plugin": "acme.deploy", "cycle": {
            "schemaVersion": 2, "entries": [], "end": "notify",
            "nodes": [{ "id": "notify", "kind": "acme.deploy/notify", "version": 1, "reach": "performed" }],
            "wires": [{ "id": "w1", "kind": "after", "from": "pull-request", "to": "notify" }] } } } }
        """;

    [Fact]
    public async Task Pushed_reads_the_remote_tracking_ref_and_an_ahead_pull_request_gits_ancestry_once_a_pass()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", Branch);
        var landed = (await GitAsync(root, "rev-parse", Branch)).Trim();
        await GitAsync(root, "update-ref", "refs/remotes/origin/" + Branch, landed);
        await CommitAsync(root, "ahead.txt", "ahead\n", "the platform's merge took a newer commit");
        var source = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        var completed = new PullRequestState(PullRequestStates.Completed) { MergeCommit = source, SourceCommit = source, Target = "main", Plugin = Plugin };
        var landing = new LandedBranch("engine", "default", Branch, "main", landed, "s1", "q1", null, DateTimeOffset.UtcNow)
        {
            Units = ["unit-a"], Pushed = true, PushedTip = landed, Plugin = Plugin, PullRequestState = completed,
        };
        WorkUnit unit = new("unit-a", WorkUnitKind.Branch, new WorkOwner("engine", "default", "session-branches.json", WorkRecord.DriverWriter),
            new WorkSubject("daoris/s1", landed), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
        {
            Records = new WorkUnitRecords(null, [landing], null),
        };

        var port = new ObservedPort();
        var records = new NodeRecordFiles(Home, () => true);
        var runtime = new NodeRuntime(records, port, () => DateTimeOffset.UtcNow, () => true);
        var look = new NodeRuntimeLook(DriverConfig.Parse(Cycle), Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead([unit], new Dictionary<string, string> { ["engine"] = root })),
            Steps = () => [Notify],
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

        var record = records.Read("unit-a")!;
        Assert.Equal(NodeStates.Reached, record.Statuses["pushed"].State);
        Assert.Equal(NodeStates.Reached, record.Statuses["pull-request"].State);
        Assert.Equal(landed, Assert.Single(port.Sent).SubjectRevision);
        Assert.Equal(1, started.Count(line => line.StartsWith("for-each-ref", StringComparison.Ordinal)));
        Assert.Equal($"merge-base --is-ancestor {landed} {source}", Assert.Single(started, line => line.StartsWith("merge-base", StringComparison.Ordinal)));
        Assert.Equal(2, started.Count);
    }
}
