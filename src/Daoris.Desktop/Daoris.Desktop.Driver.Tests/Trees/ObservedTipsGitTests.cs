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

    /// <summary>Two repositories, each observing the deploy (FABRIC-PERFORM1m's count, one read of each checkout a pass).</summary>
    private const string ObservedInBoth = """
        { "landings": {
          "engine": { "form": "merge", "cycle": {
            "schemaVersion": 2, "entries": ["deployed"], "end": "deployed",
            "nodes": [{ "id": "deployed", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" }], "wires": [] } },
          "other": { "form": "merge", "cycle": {
            "schemaVersion": 2, "entries": ["deployed"], "end": "deployed",
            "nodes": [{ "id": "deployed", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" }], "wires": [] } } } }
        """;

    /// <summary>A branch unit as the inventory gives it: its revision the end fact's, never read again here, so it lags any move.</summary>
    private static WorkUnit BranchUnit(string id, string branch, string tip, string repository = "engine") => new(id, WorkUnitKind.Branch,
        new WorkOwner(repository, "default", "session-branches.json", WorkRecord.DriverWriter), new WorkSubject(branch, tip),
        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []);

    private static WorkStepHeard Event(ObservedPort port, string branch, string commit, string id, string repository = "engine") =>
        new(Plugin, port.Generation!.Value, new WorkStepFrame("deployed", new WorkOutcome(WorkStates.Reached), DateTimeOffset.UtcNow, id, repository, branch, commit));

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
            // Begun on each branch's tip and published (FABRIC-PERFORM1m): one read of the repository's refs for the three.
            await runtime.PassAsync(Look(whole: true), CancellationToken.None);
            Assert.StartsWith("for-each-ref", Assert.Single(started), StringComparison.Ordinal);
            Assert.All(port.SentTo(Plugin, 1)!.Units, unit => Assert.Equal(RepositoryIdentity.Of(root), unit.Identity));

            // Two events over two branches in one judgement: one read of the repository's refs.
            port.Heard.Enqueue(Event(port, "feature/a", tip, "e-a"));
            port.Heard.Enqueue(Event(port, "feature/b", tip, "e-b"));
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.Equal(2, started.Count);

            // A branch moved outside Daoris between two judgements: the next reads afresh, and the event bound to the old tip is stale.
            await CommitAsync(root, "moved.txt", "moved\n", "moved on");
            await GitAsync(root, "branch", "-f", "feature/c", "HEAD");
            port.Heard.Enqueue(Event(port, "feature/c", tip, "e-c"));
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.Equal(3, started.Count(line => line.StartsWith("for-each-ref", StringComparison.Ordinal)));
            Assert.Equal(3, started.Count);
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
    public async Task A_lagging_units_node_rebinds_to_its_moved_tip_and_each_checkout_is_read_once_a_pass_for_its_events_and_its_bindings()
    {
        var engine = await RepositoryAsync("engine");
        var other = await RepositoryAsync("other");
        var tip = (await GitAsync(engine, "rev-parse", "HEAD")).Trim();
        var otherTip = (await GitAsync(other, "rev-parse", "HEAD")).Trim();
        await GitAsync(engine, "branch", "feature/a");
        await GitAsync(engine, "branch", "feature/b");
        await GitAsync(other, "branch", "feature/a");
        IReadOnlyList<WorkUnit> units =
            [BranchUnit("engine~a", "feature/a", tip), BranchUnit("engine~b", "feature/b", tip), BranchUnit("other~a", "feature/a", otherTip, "other")];

        var port = new ObservedPort();
        var records = new NodeRecordFiles(Home, () => true);
        var runtime = new NodeRuntime(records, port, () => DateTimeOffset.UtcNow, () => true);
        NodeRuntimeLook Look(bool whole) => new(DriverConfig.Parse(ObservedInBoth), whole)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(units, new Dictionary<string, string> { ["engine"] = engine, ["other"] = other })),
            Steps = () => [Deployed],
            Tips = NodeTips.ReadAsync,
        };

        var started = new List<string>();
        WorkingTree.Started.Value = arguments =>
        {
            lock (started) started.Add(string.Join(' ', arguments));
        };
        string moved;
        try
        {
            // Each node begun on its branch's tip: one read of each checkout's refs.
            await runtime.PassAsync(Look(whole: true), CancellationToken.None);
            Assert.Equal(2, started.Count);
            Assert.All(started, line => Assert.StartsWith("for-each-ref", line, StringComparison.Ordinal));

            // engine's feature/a moved outside Daoris, its unit's revision lagging; events for the two other branches in the same pass. Each
            // checkout is read once, whatever the hearing and the rebinding ask of it, and the lagging unit's node rebinds to the moved tip.
            await CommitAsync(engine, "moved.txt", "moved\n", "moved on");
            await GitAsync(engine, "branch", "-f", "feature/a", "HEAD");
            moved = (await GitAsync(engine, "rev-parse", "feature/a")).Trim();
            port.Heard.Enqueue(Event(port, "feature/b", tip, "e-b"));
            port.Heard.Enqueue(Event(port, "feature/a", otherTip, "e-o", "other"));
            started.Clear();
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.Equal(2, started.Count);
            Assert.All(started, line => Assert.StartsWith("for-each-ref", line, StringComparison.Ordinal));
            Assert.Equal(moved, records.Read("engine~a")!.Statuses["deployed"].Binding!.SubjectRevision);

            // The moved tip's event reached at the next pass: engine read once; other awaits nothing, so its checkout is not read.
            port.Heard.Enqueue(Event(port, "feature/a", moved, "e-a"));
            started.Clear();
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.StartsWith("for-each-ref", Assert.Single(started), StringComparison.Ordinal);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        Assert.All(["engine~a", "engine~b", "other~a"], unit => Assert.Equal(NodeStates.Reached, records.Read(unit)!.Statuses["deployed"].State));
        Assert.Equal(moved, records.Read("engine~a")!.Statuses["deployed"].Acceptance!.SubjectRevision);
        Assert.Empty(port.Refusals);
    }

    [Fact]
    public async Task An_event_at_the_moved_tip_heard_in_the_pass_that_rebinds_is_reached_there_with_one_read_of_the_checkout()
    {
        var root = await RepositoryAsync("engine");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "branch", "feature/a");
        IReadOnlyList<WorkUnit> units = [BranchUnit("engine~a", "feature/a", tip)];

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
        string moved;
        try
        {
            await runtime.PassAsync(Look(whole: true), CancellationToken.None);
            Assert.StartsWith("for-each-ref", Assert.Single(started), StringComparison.Ordinal);

            // FABRIC-PERFORM1o: the branch moved outside Daoris and the plugin's events came before the node rebound, one at the old tip and
            // one at the new. The old tip's is stale; the new tip's is taken once the pass has rebound the node, with no git started for it.
            await CommitAsync(root, "moved.txt", "moved\n", "moved on");
            await GitAsync(root, "branch", "-f", "feature/a", "HEAD");
            moved = (await GitAsync(root, "rev-parse", "feature/a")).Trim();
            port.Heard.Enqueue(Event(port, "feature/a", tip, "e-old"));
            port.Heard.Enqueue(Event(port, "feature/a", moved, "e-new"));
            started.Clear();
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.StartsWith("for-each-ref", Assert.Single(started), StringComparison.Ordinal);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        var deployed = records.Read("engine~a")!.Statuses["deployed"];
        Assert.Equal((NodeStates.Reached, moved), (deployed.State, deployed.Acceptance!.SubjectRevision));
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
