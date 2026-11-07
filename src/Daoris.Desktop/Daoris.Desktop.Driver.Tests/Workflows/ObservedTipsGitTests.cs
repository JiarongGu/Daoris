using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git, a batch of events over several branches reads each branch's tip from
/// the repository's ref files at every judgement, no git started and never a ref held from the judgement before, so a branch moved outside
/// Daoris between two judgements makes its event stale; and a repository's identity is its common git folder's, the same through a linked
/// worktree.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ObservedTipsGitTests : LandedFixture
{
    private const string Plugin = "acme.deploy";

    private static readonly NodeDeclaration Deployed = new(Plugin + "/deployed", Plugin + "/deployed", 1, NodeReach.Observed);

    /// <summary>A graph observing the plugin's deploy ahead of the landing, which every run here is bound to.</summary>
    private const string ObservedOnly = """
        {"schemaVersion":2,"entries":["deployed"],"end":"landing",
         "nodes":[{"id":"deployed","kind":"acme.deploy/deployed","version":1,"reach":"observed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"d>l","kind":"after","from":"deployed","to":"landing"}]}
        """;

    /// <summary>One run per branch, each bound to <see cref="ObservedOnly"/>: the unit of a run is <see cref="RunUnits.IdOf"/> of its id.</summary>
    private void Bind(params (string Run, string Repository)[] runs)
    {
        var digest = RunHome.SaveGraph(Home, ObservedOnly);
        foreach (var (run, repository) in runs)
        {
            Assert.True(new WorkflowRunFiles(Home).Bind(
                new WorkflowRunBinding(run, repository, "default", "docs", WorkflowLevels.Repository, DateTimeOffset.UtcNow) { Version = 1, Digest = digest }));
        }
    }

    /// <summary>
    /// Each run's quest and the branch unit its session worked, as the inventory gives them: the unit's revision the end fact's, never read
    /// again here, so it lags any move.
    /// </summary>
    private static IReadOnlyList<WorkUnit> Runs(params (string Run, string Branch, string Tip, string Repository)[] runs) =>
    [
        .. runs.Select(each => RunHome.QuestUnit(RunHome.Quest(each.Run, to: each.Repository))),
        .. runs.Select(each => RunHome.Branch("branch-" + each.Run, each.Branch, DateTimeOffset.UtcNow.AddHours(-1), [each.Run]) with
        {
            Owner = new WorkOwner(each.Repository, "default", SessionBranches.FileName, WorkRecord.DriverWriter),
            Subject = new WorkSubject(each.Branch, each.Tip),
        }),
    ];

    private NodeRuntimeLook Look(IReadOnlyList<WorkUnit> units, IReadOnlyDictionary<string, string> roots, bool whole) => new(DriverConfig.Empty, whole)
    {
        Units = _ => Task.FromResult(new NodeUnitsRead(units, roots)),
        Steps = () => [Deployed],
        Bound = NodeRuntime.BoundIn(Home),
        Tips = NodeTips.ReadAsync,
    };

    private static WorkStepHeard Event(ObservedPort port, string branch, string commit, string id, string repository = "engine") =>
        new(Plugin, port.Generation!.Value, new WorkStepFrame("deployed", new WorkOutcome(WorkStates.Reached), DateTimeOffset.UtcNow, id, repository, branch, commit));

    [Fact]
    public async Task A_batch_of_events_over_several_branches_reads_the_repositorys_refs_once_per_judgement_and_sees_an_outside_move()
    {
        var root = await RepositoryAsync("engine");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        string[] branches = ["feature/a", "feature/b", "feature/c"];
        foreach (var branch in branches) await GitAsync(root, "branch", branch);
        Bind(("r0", "engine"), ("r1", "engine"), ("r2", "engine"));
        var units = Runs([.. branches.Select((branch, at) => ($"r{at}", branch, tip, "engine"))]);

        var port = new ObservedPort();
        var records = new NodeRecordFiles(Home, () => true);
        var runtime = new NodeRuntime(records, port, () => DateTimeOffset.UtcNow, () => true);
        NodeRuntimeLook Look(bool whole) => this.Look(units, new Dictionary<string, string> { ["engine"] = root }, whole);

        var started = new List<string>();
        WorkingTree.Started.Value = arguments =>
        {
            lock (started) started.Add(string.Join(' ', arguments));
        };
        try
        {
            // Begun on each branch's tip and published: the three read from the repository's ref files, no git started.
            await runtime.PassAsync(Look(whole: true), CancellationToken.None);
            Assert.Empty(started);
            Assert.All(port.SentTo(Plugin, 1)!.Units, unit => Assert.Equal(RepositoryIdentity.Of(root), unit.Identity));

            // Two events over two branches in one judgement.
            port.Heard.Enqueue(Event(port, "feature/a", tip, "e-a"));
            port.Heard.Enqueue(Event(port, "feature/b", tip, "e-b"));
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.Empty(started);

            // A branch moved outside Daoris between two judgements: the next reads afresh, and the event bound to the old tip is stale.
            await CommitAsync(root, "moved.txt", "moved\n", "moved on");
            await GitAsync(root, "branch", "-f", "feature/c", "HEAD");
            port.Heard.Enqueue(Event(port, "feature/c", tip, "e-c"));
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.Empty(started);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        Assert.Equal(NodeStates.Reached, records.Read(RunUnits.IdOf("r0"))!.Statuses["deployed"].State);
        Assert.Equal(NodeStates.Reached, records.Read(RunUnits.IdOf("r1"))!.Statuses["deployed"].State);
        Assert.Equal(NodeStates.Active, records.Read(RunUnits.IdOf("r2"))!.Statuses["deployed"].State);
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
        Bind(("ea", "engine"), ("eb", "engine"), ("oa", "other"));
        var units = Runs(("ea", "feature/a", tip, "engine"), ("eb", "feature/b", tip, "engine"), ("oa", "feature/a", otherTip, "other"));

        var port = new ObservedPort();
        var records = new NodeRecordFiles(Home, () => true);
        var runtime = new NodeRuntime(records, port, () => DateTimeOffset.UtcNow, () => true);
        NodeRuntimeLook Look(bool whole) => this.Look(units, new Dictionary<string, string> { ["engine"] = engine, ["other"] = other }, whole);

        var started = new List<string>();
        WorkingTree.Started.Value = arguments =>
        {
            lock (started) started.Add(string.Join(' ', arguments));
        };
        string moved;
        try
        {
            // Each node begun on its branch's tip, read from each checkout's ref files.
            await runtime.PassAsync(Look(whole: true), CancellationToken.None);
            Assert.Empty(started);

            // engine's feature/a moved outside Daoris, its unit's revision lagging; events for the two other branches in the same pass. Each
            // checkout is read once, whatever the hearing and the rebinding ask of it, and the lagging unit's node rebinds to the moved tip.
            await CommitAsync(engine, "moved.txt", "moved\n", "moved on");
            await GitAsync(engine, "branch", "-f", "feature/a", "HEAD");
            moved = (await GitAsync(engine, "rev-parse", "feature/a")).Trim();
            port.Heard.Enqueue(Event(port, "feature/b", tip, "e-b"));
            port.Heard.Enqueue(Event(port, "feature/a", otherTip, "e-o", "other"));
            started.Clear();
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.Empty(started);
            Assert.Equal(moved, records.Read(RunUnits.IdOf("ea"))!.Statuses["deployed"].Binding!.SubjectRevision);

            // The moved tip's event reached at the next pass.
            port.Heard.Enqueue(Event(port, "feature/a", moved, "e-a"));
            started.Clear();
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.Empty(started);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        Assert.All(["ea", "eb", "oa"], run => Assert.Equal(NodeStates.Reached, records.Read(RunUnits.IdOf(run))!.Statuses["deployed"].State));
        Assert.Equal(moved, records.Read(RunUnits.IdOf("ea"))!.Statuses["deployed"].Acceptance!.SubjectRevision);
        Assert.Empty(port.Refusals);
    }

    [Fact]
    public async Task An_event_at_the_moved_tip_heard_in_the_pass_that_rebinds_is_reached_there_with_one_read_of_the_checkout()
    {
        var root = await RepositoryAsync("engine");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "branch", "feature/a");
        Bind(("ea", "engine"));
        var units = Runs(("ea", "feature/a", tip, "engine"));

        var port = new ObservedPort();
        var records = new NodeRecordFiles(Home, () => true);
        var runtime = new NodeRuntime(records, port, () => DateTimeOffset.UtcNow, () => true);
        NodeRuntimeLook Look(bool whole) => this.Look(units, new Dictionary<string, string> { ["engine"] = root }, whole);

        var started = new List<string>();
        WorkingTree.Started.Value = arguments =>
        {
            lock (started) started.Add(string.Join(' ', arguments));
        };
        string moved;
        try
        {
            await runtime.PassAsync(Look(whole: true), CancellationToken.None);
            Assert.Empty(started);

            // The branch moves outside Daoris and the plugin's events come before the node rebinds, one at the old tip and
            // one at the new. The old tip's is stale; the new tip's is taken once the pass has rebound the node, with no git started for it.
            await CommitAsync(root, "moved.txt", "moved\n", "moved on");
            await GitAsync(root, "branch", "-f", "feature/a", "HEAD");
            moved = (await GitAsync(root, "rev-parse", "feature/a")).Trim();
            port.Heard.Enqueue(Event(port, "feature/a", tip, "e-old"));
            port.Heard.Enqueue(Event(port, "feature/a", moved, "e-new"));
            started.Clear();
            await runtime.PassAsync(Look(whole: false), CancellationToken.None);
            Assert.Empty(started);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        var deployed = records.Read(RunUnits.IdOf("ea"))!.Statuses["deployed"];
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
