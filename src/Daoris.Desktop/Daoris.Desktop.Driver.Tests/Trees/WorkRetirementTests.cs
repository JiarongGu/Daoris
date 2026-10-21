using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PERFORM1d (the fabric events design §D3, canary 35): a unit retires apart from its branch's removal. A branch removed while an
/// attempt or a resource is unresolved leaves its unit open on every adapter, the traces' pruning skips it whatever its branch's removal
/// said, and it retires once its last entry is terminal.
/// </summary>
public sealed class WorkRetirementTests : IDisposable
{
    private static readonly DateTimeOffset Then = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private const string Plugin = "acme.deploy";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-retire-" + Guid.NewGuid().ToString("N")[..8]);

    public WorkRetirementTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static HeldFact Fact() => new(Then, Then, EndBy.Merged, "t1", "main", "main", "m1", "r1");

    private static GrownBranch Grown(string branch, string unit) => new("engine", "default", branch, "main", "f0", null, Then) { Unit = unit };

    private static ResourceReceipt Receipt(string resource, string role = PluginResourceRoles.Temporary) =>
        new("r-" + resource, Plugin, "acme.deploy/preview@1", resource, role, "deploy", 1, "i-1", Then);

    private static NodeRecord Attempting(string unit, string state)
    {
        var call = new WorkPerformCall(unit, Plugin + "/deploy", 1, "i-1", "k-1", "t1", [], Then.AddMinutes(15));
        return new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal) { ["deploy"] = new NodeStatus(unit, "deploy", NodeStates.Active) { Attempt = 1 } },
            Attempts = [new NodeAttempt("deploy", 1, Plugin, call, state) { RecordedAt = Then }],
        };
    }

    /// <summary>The branch unit as the inventory derives it: its session branch removed with the line's proof, so its end is reached.</summary>
    private WorkUnit Unit(string unit)
    {
        var facts = new InventoryFacts([Grown("daoris/s1", unit) with { GoneAt = Then, Held = Fact() }], [], [], [], [], new HashSet<string>(), _ => false, Then)
        {
            Owed = WorkObligations.Read(_home, Then),
        };
        return Assert.Single(WorkInventory.Units(facts));
    }

    /// <summary>🔴 Canary 35: ended and its branch gone, the unit stays open while an attempt or a resource is unresolved, and retires once each is terminal.</summary>
    [Fact]
    public void A_removed_branchs_unit_stays_open_while_an_attempt_or_a_resource_is_unresolved_and_retires_once_they_are_terminal()
    {
        Assert.Equal((WorkEndState.Ended, WorkUnitState.Retired), (Unit("unit-a").End.State, Unit("unit-a").State));

        var nodes = new NodeRecordFiles(_home, () => true);
        var resources = new ResourceRecordFiles(_home, () => true);
        nodes.Write(Attempting("unit-a", AttemptStates.Sent));
        resources.Write(new ResourceRecord("unit-a") { Receipts = [Receipt("p-1"), Receipt("s-1", PluginResourceRoles.Result)] });

        var open = Unit("unit-a");
        Assert.Equal(WorkUnitState.Open, open.State);
        Assert.Contains(open.Entries, entry => entry is { Kind: InventoryKind.Attempt, Disposition: InventoryDisposition.Pending });
        var milestone = WorkMilestone.Of(open);
        Assert.Equal("pending", milestone.Cleanup.State);
        Assert.Equal([InventoryKind.PluginResource, InventoryKind.Attempt], milestone.Cleanup.Waits.Select(entry => entry.Kind));
        Assert.Contains(milestone.Result, entry => entry.Kind == InventoryKind.PluginResource && entry.Disposition == InventoryDisposition.Kept);

        // The attempt settles: the temporary resource still waits on its plugin.
        nodes.Write(Attempting("unit-a", AttemptStates.Settled));
        Assert.Equal(WorkUnitState.Open, Unit("unit-a").State);

        // Its plugin removed it: every entry is terminal, and the unit retires.
        resources.Write(new ResourceRecord("unit-a")
        {
            Receipts =
            [
                Receipt("p-1") with { Cleanup = new ResourceCleanup(CleanupStates.Settled, "c-1", Then, 0) { Disposition = CleanupDispositions.Removed, SettledAt = Then } },
                Receipt("s-1", PluginResourceRoles.Result),
            ],
        });
        var retired = Unit("unit-a");
        Assert.Equal(WorkUnitState.Retired, retired.State);
        Assert.Equal("completed", WorkMilestone.Of(retired).Cleanup.State);
    }

    /// <summary>A held attempt and an undeclared resource block, and say so: nothing establishes them by themselves.</summary>
    [Fact]
    public void A_held_attempt_and_an_undeclared_resource_block_their_units_retirement()
    {
        new NodeRecordFiles(_home, () => true).Write(Attempting("unit-a", AttemptStates.Held));
        new ResourceRecordFiles(_home, () => true).Write(new ResourceRecord("unit-a") { Receipts = [Receipt("l-1", ResourceReceipt.Undeclared)] });

        var unit = Unit("unit-a");

        Assert.Equal(WorkUnitState.Open, unit.State);
        Assert.Equal(
            [(InventoryKind.PluginResource, InventoryCode.Undeclared), (InventoryKind.Attempt, InventoryCode.AttemptHeld)],
            unit.Entries.Where(entry => entry.Disposition == InventoryDisposition.Blocked).Select(entry => (entry.Kind, entry.Code)));
    }

    /// <summary>A quest's or an ask's obligations join it by its id, as a branch unit's do.</summary>
    [Fact]
    public void Obligations_join_any_kind_of_unit_by_its_id()
    {
        var quest = new WorkUnit("quest:q1", WorkUnitKind.Quest, new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter),
            new WorkSubject("#q1", null), new MilestoneEnd(WorkEndState.Ended, null, null, null, Then, "done"), []);
        var owed = WorkObligations.Of([Attempting("quest:q1", AttemptStates.Unknown)], [], Then);

        var joined = Assert.Single(WorkObligations.Attach([quest], owed));

        Assert.Equal(WorkUnitState.Retired, quest.State);
        Assert.Equal(WorkUnitState.Open, joined.State);
        Assert.Same(quest, Assert.Single(WorkObligations.Attach([quest], WorkObligations.Of([], [], Then))));
    }

    /// <summary>
    /// 🔴 Canary 35, the traces: forgetting keeps the trace of a unit that owes anything, and the pruning never drops it, even held by the line;
    /// once it owes nothing, it is pruned as any retired trace is.
    /// </summary>
    [Fact]
    public void The_traces_pruning_skips_a_unit_that_still_owes_and_takes_it_once_it_retires()
    {
        var resources = new ResourceRecordFiles(_home, () => true);
        resources.Write(new ResourceRecord("unit-owes") { Receipts = [Receipt("p-1")] });
        new NodeRecordFiles(_home, () => true).Write(Attempting("unit-waits", AttemptStates.Unknown));
        var grown = new SessionBranches(_home);
        grown.Record(Grown("daoris/owes", "unit-owes"));
        grown.Hold("engine", "daoris/owes", Fact());
        grown.Record(Grown("daoris/waits", "unit-waits"));
        grown.Forget("engine", ["daoris/owes", "daoris/waits"]);

        Assert.Equal(["daoris/owes", "daoris/waits"], grown.Entries().Select(each => each.Branch));
        var owing = WorkObligations.Owing(_home);
        Assert.Equal(["unit-owes", "unit-waits"], owing.Order(StringComparer.Ordinal));
        Assert.All(grown.Entries(), trace => Assert.True(SessionBranches.Open(trace, new HashSet<string>(), owing)));

        for (var at = 0; at < SessionBranches.TracesKept + 2; at++)
        {
            grown.Record(Grown($"daoris/s{at}", $"unit-{at}"));
            grown.Hold("engine", $"daoris/s{at}", Fact());
            grown.Forget("engine", [$"daoris/s{at}"]);
        }

        Assert.Contains(grown.Entries(), each => each.Branch == "daoris/owes");
        Assert.Contains(grown.Entries(), each => each.Branch == "daoris/waits");

        resources.Write(new ResourceRecord("unit-owes")
        {
            Receipts = [Receipt("p-1") with { Cleanup = new ResourceCleanup(CleanupStates.Settled, "c-1", Then, 0) { Disposition = CleanupDispositions.AlreadyGone } }],
        });
        grown.Record(Grown("daoris/last", "unit-last"));
        grown.Hold("engine", "daoris/last", Fact());
        grown.Forget("engine", ["daoris/last"]);

        Assert.DoesNotContain(grown.Entries(), each => each.Branch == "daoris/owes");
        Assert.Contains(grown.Entries(), each => each.Branch == "daoris/waits");
    }
}
