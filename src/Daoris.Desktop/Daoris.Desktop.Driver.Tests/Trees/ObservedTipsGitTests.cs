using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PERFORM1i over real git (§C4's fourth rule; D88's PROC1d-ii): a batch of events over several branches reads each repository's refs
/// once per judgement, never a ref held from the judgement before, so a branch moved outside Daoris between two judgements makes its event
/// stale; and a repository's identity is its common git folder's, the same through a linked worktree.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ObservedTipsGitTests : LandedFixture
{
    private const string Plugin = "acme.deploy";

    private static readonly NodeDeclaration Deployed = new(Plugin + "/deployed", Plugin + "/deployed", 1, NodeReach.Observed);

    private const string ObservedOnly = """
        { "landings": { "engine": { "form": "merge", "cycle": {
            "schemaVersion": 2, "entries": ["deployed"], "end": "deployed",
            "nodes": [{ "id": "deployed", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" }], "wires": [] } } } }
        """;

    private static WorkUnit BranchUnit(string id, string branch, string tip) => new(id, WorkUnitKind.Branch,
        new WorkOwner("engine", "default", "session-branches.json", WorkRecord.DriverWriter), new WorkSubject(branch, tip),
        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []);

    private static WorkStepHeard Event(ObservedPort port, string branch, string commit, string id) =>
        new(Plugin, port.Generation!.Value, new WorkStepFrame("deployed", new WorkOutcome(WorkStates.Reached), DateTimeOffset.UtcNow, id, "engine", branch, commit));

    [Fact]
    public async Task A_batch_of_events_over_several_branches_reads_the_repositorys_refs_once_per_judgement_and_sees_an_outside_move()
    {
        var root = await RepositoryAsync("engine");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        string[] branches = ["feature/a", "feature/b", "feature/c"];
        foreach (var branch in branches) await GitAsync(root, "branch", branch);
        IReadOnlyList<WorkUnit> units = [.. branches.Select((branch, at) => BranchUnit($"engine~{at}", branch, tip))];

        var port = new ObservedPort();
        var records = new NodeRecordFiles(Home, () => true);
        var runtime = new NodeRuntime(records, port, () => DateTimeOffset.UtcNow, () => true);
        NodeRuntimeLook Look(bool whole) => new(DriverConfig.Parse(ObservedOnly), whole)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(units, new Dictionary<string, string> { ["engine"] = root })),
            Steps = () => [Deployed],
            Tips = NodeTips.ReadAsync,
        };

        var started = new List<string>();
        WorkingTree.Started.Value = arguments =>
        {
            lock (started) started.Add(string.Join(' ', arguments));
        };
        try
        {
            // Begun and published with no git at all.
            await runtime.PassAsync(Look(whole: true), CancellationToken.None);
            Assert.Empty(started);
            Assert.All(port.SentTo(Plugin, 1)!.Units, unit => Assert.Equal(RepositoryIdentity.Of(root), unit.Identity));

            // Two events over two branches in one judgement: one read of the repository's refs.
            port.Heard.Enqueue(Event(port, "feature/a", tip, "e-a"));
            port.Heard.Enqueue(Event(port, "feature/b", tip, "e-b"));
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.StartsWith("for-each-ref", Assert.Single(started), StringComparison.Ordinal);

            // A branch moved outside Daoris between two judgements: the next reads afresh, and the event bound to the old tip is stale.
            await CommitAsync(root, "moved.txt", "moved\n", "moved on");
            await GitAsync(root, "branch", "-f", "feature/c", "HEAD");
            port.Heard.Enqueue(Event(port, "feature/c", tip, "e-c"));
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.Equal(2, started.Count(line => line.StartsWith("for-each-ref", StringComparison.Ordinal)));
            Assert.Equal(2, started.Count);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        Assert.Equal(NodeStates.Reached, records.Read("engine~0")!.Statuses["deployed"].State);
        Assert.Equal(NodeStates.Reached, records.Read("engine~1")!.Statuses["deployed"].State);
        Assert.Equal(NodeStates.Active, records.Read("engine~2")!.Statuses["deployed"].State);
        Assert.Equal([(Plugin, WorkRefusals.Stale)], port.Refusals);
    }

    [Fact]
    public async Task A_repositorys_identity_is_its_common_folders_through_a_linked_worktree_and_another_repository_is_another()
    {
        var root = await RepositoryAsync("engine");
        var linked = Path.Combine(Scratch, "engine-linked");
        await GitAsync(root, "worktree", "add", "--quiet", "-b", "feature/w", linked);
        var other = await RepositoryAsync("other");

        Assert.Equal(RepositoryIdentity.Of(root), RepositoryIdentity.Of(linked));
        Assert.NotEqual(RepositoryIdentity.Of(root), RepositoryIdentity.Of(other));
        Assert.Equal("", RepositoryIdentity.Of(""));
    }
}
