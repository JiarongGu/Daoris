using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A <c>push</c> whose landing no entry of the landings file names any more (its trace dropped past the traces kept) is read from its session
/// branch's own record: held on the line (the same change reached it as a squash-merged pull request), the push is passed over with words and
/// its lost attempt cleared; standing off the line, it is started on its door once; neither, the door fails it once, saying why.
/// </summary>
public sealed class UnrecordedLandingTests : IDisposable
{
    private const string Unit = WireEvaluationTests.Unit;
    private static readonly DateTimeOffset Then = WireEvaluationTests.Then;
    private static readonly NodeDeclaration Push = WireEvaluationTests.Node("push", NodeKinds.Push);
    private static readonly NodeDeclaration Land = WireEvaluationTests.Node("landing", NodeKinds.Land);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "unrecorded-push", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<string> _said = [];

    public UnrecordedLandingTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static HeldFact Held(string by) => new(Then, Then, by, "t1", "main", "origin/main", "f9", "repo-1");

    /// <summary>The run's branch unit with no landing on record, its session branch's record as given, or none.</summary>
    private static WorkUnit UnitOf(HeldFact? held, bool recorded = true, bool gone = false)
    {
        GrownBranch[] grown = recorded
            ? [new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = Unit, Held = held, GoneAt = gone ? Then.AddHours(1) : null }]
            : [];
        var facts = new InventoryFacts(grown, [], [], [], new HashSet<string>(), _ => false, Then.AddDays(1));
        return WorkInventory.Units(facts).SingleOrDefault()
            ?? new WorkUnit(Unit, WorkUnitKind.Branch, new WorkOwner("engine", "default", null, "driver"), new WorkSubject("daoris/s1", "t1"),
                new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "no record"), []);
    }

    [Fact]
    public void A_push_active_past_a_landing_no_entry_names_whose_branch_the_line_holds_is_passed_over_and_its_attempt_cleared()
    {
        var g = new WireEvaluationTests.Graph([Land, Push], [WireEvaluationTests.After("c", "landing", "push")])
        {
            Run = WireEvaluationTests.Base with
            {
                Revisions = new Dictionary<string, string?> { [Unit] = "t1" }, Units = new Dictionary<string, WorkUnit> { [Unit] = UnitOf(null) },
            },
        };
        g.Through("landing");
        g.Start(g.Evaluate(), "push");
        Assert.Equal((NodeStates.Active, 1), (g.State("push"), g.Statuses["push"].Unreconciled));

        g.Run = g.Run with { Units = new Dictionary<string, WorkUnit> { [Unit] = UnitOf(Held(EndBy.OnLine)) } };
        g.Evaluate();
        g.Evaluate();

        var push = g.Statuses["push"];
        Assert.Equal((NodeStates.Skipped, NodeSource.Path, (int?)null), (push.State, push.By, push.Unreconciled));
        Assert.Contains("nothing left to deliver: `daoris/s1` is on `origin/main` already", push.Words);
        Assert.Contains("no record names the branch its landing made any more", push.Words);
    }

    /// <summary>A door that keeps what the keeping starts.</summary>
    private sealed class Door : IPushDoor
    {
        public List<PushStart> Starts { get; } = [];

        public bool Start(PushStart start)
        {
            Starts.Add(start);
            return true;
        }
    }

    /// <summary>The run's active push, its <c>land</c> reached on <c>feature/q1-fix</c> at t1, and its branch unit as given.</summary>
    private NodeKept Kept(WorkUnit branch, IPushDoor door)
    {
        var self = new WorkUnit("run:r1", WorkUnitKind.Run, branch.Owner, branch.Subject, branch.End, []);
        var graph = new NodeGraph([Land, Push], [], ["landing"], "push");
        var governed = new GovernedUnit(self, graph, "gph-1", "r1", graph.Nodes.ToDictionary(node => node.Id), new HashSet<string>(),
            new WatchedUnit("engine", "default", _home, "daoris/s1", "t1", "main", null, []))
        {
            Scope = new RunScope(null!, branch) { Branch = branch },
        };
        var landed = new NodeStatus(self.Id, Land.Id, NodeStates.Reached)
        {
            Attempt = 1, Tries = 1,
            Acceptance = new NodeAcceptance(self.Id, "t1", "gph-1", Land.Id, 1, 1)
            {
                Subject = branch.Id, Evidence = new NodePort("daoris/land@1", $"{LandedBranches.FileName}#feature/q1-fix", "t1", null),
            },
        };
        var pushing = new NodeStatus(self.Id, Push.Id, NodeStates.Active)
        {
            Attempt = 1, Tries = 1, Unreconciled = 1, Binding = new NodeAcceptance(self.Id, "t1", "gph-1", Push.Id, 1, 1),
        };
        var record = new NodeRecord(self.Id) { Statuses = new Dictionary<string, NodeStatus> { [Land.Id] = landed, [Push.Id] = pushing } };
        new NodeRecordFiles(_home, () => true).Write(record);
        return new NodeKept(governed, governed.Scope!, Push, pushing, record, NodeDoors.None.With(NodeKinds.Push, door)) { Home = _home, At = Then };
    }

    [Fact]
    public void A_push_whose_branch_stands_off_the_line_is_started_once_on_the_landed_branch()
    {
        var door = new Door();
        var kept = Kept(UnitOf(null), door);

        new PushNode().Keep(kept);
        new PushNode().Keep(kept);

        var start = Assert.Single(door.Starts);
        Assert.Equal(("feature/q1-fix", "t1", (string?)null), (start.Branch, start.Tip, start.Unrecorded));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task A_push_whose_branch_neither_stands_nor_is_held_fails_once_saying_why(bool recorded, bool gone)
    {
        var door = new Door();
        var kept = Kept(UnitOf(null, recorded, gone), door);

        new PushNode().Keep(kept);
        var start = Assert.Single(door.Starts);
        Assert.NotNull(start.Unrecorded);
        await new PushDoor(_home, _said.Add, holdsLease: () => true).PerformAsync(start, CancellationToken.None);

        var failed = new NodeRecordFiles(_home, () => true).Read("run:r1")!.Statuses[Push.Id];
        Assert.Equal((NodeStates.Failed, PushLineRead.BranchGone, (int?)null), (failed.State, failed.Code, failed.Unreconciled));
        Assert.Contains(_said, line => line.Contains("no record names the landing of `feature/q1-fix` any more", StringComparison.Ordinal));
    }
}
