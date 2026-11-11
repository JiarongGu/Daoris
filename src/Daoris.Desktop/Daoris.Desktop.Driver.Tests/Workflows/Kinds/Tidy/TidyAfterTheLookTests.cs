using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An already-delivered chain whose landing the look's own tidy removed (on the line, its session branch gone) leaves the run's clean-up
/// nothing to clean: its door finds no standing landing to write on, so the tidy is reached from the record as it stands rather than
/// waiting for ever. A landing still standing, or one the look removed while a part of its chain stands, is not.
/// </summary>
public sealed class TidyAfterTheLookTests
{
    private static readonly NodeDeclaration Tidy = new("tidy", NodeKinds.Tidy, 1, NodeReach.Performed);

    private static LandedBranch Landing() =>
        new("engine", "default", "feature/q1-fix", "main", "t1", "s-q1", "q1", "Fix", WireEvaluationTests.Then)
        {
            Units = [WireEvaluationTests.Unit],
            Held = new HeldFact(WireEvaluationTests.Then, WireEvaluationTests.Then, EndBy.OnLine, "t1", "main", "origin/main", "m1", "r1"),
        };

    private static LandedBranch Removed(LandedBranch landing) =>
        landing with { GoneAt = WireEvaluationTests.Then.AddHours(1), RemovedAs = EndBy.OnLine, RemovedOn = "origin/main" };

    private static NodeObservation Observed(LandedBranch landing, bool partGone = true)
    {
        var grown = new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, WireEvaluationTests.Then) { Unit = WireEvaluationTests.Unit };
        if (partGone) grown = grown with { GoneAt = WireEvaluationTests.Then.AddHours(1), Held = landing.Held };
        var unit = Assert.Single(WorkInventory.Units(new InventoryFacts([grown], [landing], [], [], new HashSet<string>(), _ => false,
            WireEvaluationTests.Then.AddDays(1))));
        var reading = new NodeReading(unit, Tidy, new NodeAcceptance(unit.Id, "t1", "graph-1", Tidy.Id, 1, 1));
        return Assert.IsAssignableFrom<IObserves>(NodeKit.Of(NodeKinds.Tidy)).Observe(reading);
    }

    [Fact]
    public void A_chain_the_looks_tidy_already_ended_reaches_its_clean_up_with_nothing_to_clean()
    {
        var reached = Observed(Removed(Landing()));

        Assert.NotNull(reached.Acceptance);
        Assert.Equal("t1", reached.Acceptance!.Evidence?.Revision);
        Assert.Contains("nothing was left to clean", reached.Said, StringComparison.Ordinal);
    }

    [Fact]
    public void A_standing_landing_or_a_part_still_standing_keeps_the_clean_up_waiting_on_its_door()
    {
        Assert.Null(Observed(Landing()).Acceptance);
        Assert.Null(Observed(Removed(Landing()), partGone: false).Acceptance);
    }
}
