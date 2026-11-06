using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// G8b: a run's <c>&lt;plugin&gt;/pull-request</c> step that reached writes the address its evidence names, and its plugin, on the landing
/// record of the branch the run's <c>land</c> made, once, and only while the run's pass holds the lease.
/// </summary>
public sealed class PullRequestKeepingTests : IDisposable
{
    private const string Address = "https://example.test/org/engine/pull/7";
    private static readonly DateTimeOffset Then = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "pr-keeping", Guid.NewGuid().ToString("N")[..8]);
    private readonly LandedBranches _landings;

    public PullRequestKeepingTests()
    {
        Directory.CreateDirectory(_home);
        _landings = new LandedBranches(_home);
        _landings.Record(new LandedBranch("engine", "default", "feature/x", "main", "t1", "s1", "q1", "X", Then));
    }

    public void Dispose() => GitSeed.Discard(_home);

    private static LandedBranch Landed() =>
        new("engine", "default", "feature/x", "main", "t1", "s1", "q1", null, Then) { Units = ["unit-a"] };

    /// <summary>The run's governed unit: a <c>land</c> reached on the branch's tip, then a <c>push</c> and a pull-request step of <c>acme</c>.</summary>
    private static (GovernedUnit Governed, WorkUnit Branch) Governed()
    {
        var branch = Assert.Single(WorkInventory.Units(new InventoryFacts(
            [new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = "unit-a" }], [Landed()], [], [],
            new HashSet<string>(), _ => false, Then.AddDays(1))));
        NodeDeclaration[] nodes =
        [
            new("landing", NodeKinds.Land, 1, NodeReach.Performed),
            new("push", NodeKinds.Push, 1, NodeReach.Performed),
            new("pr", "acme/pull-request", 1, NodeReach.Performed),
            new("notify", "acme/notify", 1, NodeReach.Performed),
        ];
        var graph = new NodeGraph(nodes, [], ["landing"], "pr");
        var governed = new GovernedUnit(
            new WorkUnit("run:r1", WorkUnitKind.Run, branch.Owner, branch.Subject, branch.End, []), graph, "gph-1", "r1",
            nodes.ToDictionary(node => node.Id), new HashSet<string>(),
            new WatchedUnit("engine", "default", "C:/fam/engine", "feature/x", "t1", "main", null, []))
        {
            Scope = new RunScope(null!, branch) { Branch = branch },
        };
        return (governed, branch);
    }

    private static NodeStatus Reached(string unit, string node, NodePort evidence, string? subject = null) =>
        new(unit, node, NodeStates.Reached)
        {
            Attempt = 1, Tries = 1,
            Acceptance = new NodeAcceptance(unit, "t1", "gph-1", node, 1, 1) { Subject = subject, Evidence = evidence },
        };

    private NodeRunKeeping Keeping(NodeRecordFiles records, bool lease = true) => new(
        records, () => Then, () => lease, null!, null!, () => NodeDoors.None, () => null, () => new PullRequestRecords(_home));

    private NodeRecordFiles Recorded(GovernedUnit governed, WorkUnit branch, string? evidenceRef)
    {
        var records = new NodeRecordFiles(_home, () => true);
        var statuses = new Dictionary<string, NodeStatus>
        {
            ["landing"] = Reached(governed.Unit.Id, "landing", new NodePort("daoris/landing@1", "landing-1", "t1", null), branch.Id),
        };
        if (evidenceRef is not null) statuses["pr"] = Reached(governed.Unit.Id, "pr", new NodePort("acme/receipt@1", evidenceRef, null, null));
        statuses["notify"] = Reached(governed.Unit.Id, "notify", new NodePort("acme/receipt@1", Address, null, null));
        records.Write(new NodeRecord(governed.Unit.Id) { Statuses = statuses });
        return records;
    }

    [Fact]
    public void A_reached_pull_request_step_keeps_its_address_and_plugin_on_the_landing_once()
    {
        var (governed, branch) = Governed();
        var keeping = Keeping(Recorded(governed, branch, Address));

        keeping.KeepPullRequests(governed);

        var kept = _landings.Of("engine", "feature/x")!;
        Assert.Equal(Address, kept.PullRequest);
        Assert.Equal("acme", kept.Plugin);
        // Kept already: a second pass writes nothing more.
        keeping.KeepPullRequests(governed);
        Assert.Equal(Address, _landings.Of("engine", "feature/x")!.PullRequest);
    }

    [Fact]
    public void Nothing_is_kept_for_a_step_that_did_not_reach_or_whose_evidence_is_no_web_address()
    {
        var (governed, branch) = Governed();

        Keeping(Recorded(governed, branch, null)).KeepPullRequests(governed);
        Assert.Null(_landings.Of("engine", "feature/x")!.PullRequest);

        Keeping(Recorded(governed, branch, "inv-1")).KeepPullRequests(governed);
        Assert.Null(_landings.Of("engine", "feature/x")!.PullRequest);
    }

    [Fact]
    public void Another_plugin_step_that_reached_with_an_address_is_not_a_pull_request()
    {
        var (governed, branch) = Governed();

        Keeping(Recorded(governed, branch, null)).KeepPullRequests(governed);

        Assert.Null(_landings.Of("engine", "feature/x")!.Plugin);
    }

    [Fact]
    public void A_pass_without_the_lease_keeps_nothing()
    {
        var (governed, branch) = Governed();

        Keeping(Recorded(governed, branch, Address), lease: false).KeepPullRequests(governed);

        Assert.Null(_landings.Of("engine", "feature/x")!.PullRequest);
    }
}
