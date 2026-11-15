using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's active <c>land</c> its keeping cannot start on a door says why in the machine log, by a code, once per change of reason: no door
/// wired, no branch in the run, no commit on its binding. Kept again for the same reason, it says nothing more.
/// </summary>
public sealed class LandKeepWaitsTests
{
    private static readonly DateTimeOffset Then = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly NodeDeclaration Land = new("landing", NodeKinds.Land, 1, NodeReach.Performed);
    private readonly List<(string Code, string Said)> _said = [];
    private readonly string _home = Guid.NewGuid().ToString("N");

    private sealed class Door : ILandingDoor
    {
        public int Starts { get; private set; }

        public bool Start(LandStart start)
        {
            Starts++;
            return true;
        }
    }

    private static GovernedUnit Governed(WorkUnit? branch)
    {
        var grown = Branch();
        var self = new WorkUnit("run:r1", WorkUnitKind.Run, grown.Owner, grown.Subject, grown.End, []);
        var graph = new NodeGraph([Land], [], ["landing"], "landing");
        return new GovernedUnit(self, graph, "gph-1", "r1", new Dictionary<string, NodeDeclaration> { ["landing"] = Land }, new HashSet<string>(),
            new WatchedUnit("engine", "default", "C:/fam/engine", "daoris/s1", "t1", "main", null, []))
        {
            Scope = new RunScope(null!, self) { Branch = branch },
        };
    }

    private NodeKept Kept(NodeDoors doors, WorkUnit? branch, string? tip)
    {
        var governed = Governed(branch);
        var status = new NodeStatus(governed.Unit.Id, Land.Id, NodeStates.Active)
        {
            Attempt = 1, Tries = 1, Binding = new NodeAcceptance(governed.Unit.Id, tip, "gph-1", Land.Id, 1, 1),
        };
        return new NodeKept(governed, governed.Scope!, Land, status, new NodeRecord(governed.Unit.Id), doors.Saying((code, said) => _said.Add((code, said))))
        {
            Home = _home, At = Then,
        };
    }

    private static WorkUnit Branch() =>
        Assert.Single(WorkInventory.Units(new InventoryFacts(
            [new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = "unit-a" }], [], [], [],
            new HashSet<string>(), _ => false, Then.AddDays(1))));

    [Fact]
    public void Each_reason_the_keeping_starts_nothing_is_said_once()
    {
        var land = new LandNode();
        var door = new Door();

        land.Keep(Kept(NodeDoors.None, Branch(), "t1"));
        land.Keep(Kept(NodeDoors.None, Branch(), "t1"));
        land.Keep(Kept(NodeDoors.None.With(NodeKinds.Land, door), null, "t1"));
        land.Keep(Kept(NodeDoors.None.With(NodeKinds.Land, door), Branch(), null));
        land.Keep(Kept(NodeDoors.None.With(NodeKinds.Land, door), Branch(), null));

        Assert.Equal(0, door.Starts);
        Assert.Equal([LandWaits.Event, LandWaits.Event, LandWaits.Event], _said.Select(each => each.Code));
        Assert.Collection(_said,
            each => Assert.Contains($"({LandWaits.NoDoor})", each.Said),
            each => Assert.Contains($"({LandWaits.NoBranch})", each.Said),
            each => Assert.Contains($"({LandWaits.NoTip})", each.Said));
        Assert.All(_said, each => Assert.StartsWith("run r1: `landing` waits", each.Said, StringComparison.Ordinal));
    }

    [Fact]
    public void A_land_its_keeping_starts_says_nothing()
    {
        var door = new Door();

        new LandNode().Keep(Kept(NodeDoors.None.With(NodeKinds.Land, door), Branch(), "t1"));

        Assert.Equal(1, door.Starts);
        Assert.Empty(_said);
    }
}
