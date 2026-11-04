using Daoris.Contracts.Parts;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's look runs the passes its modules register, phase by phase in their order, each built from the driver as its
/// wiring; an order the marks cannot make, or a pass the driver cannot build, is refused at the first look and never part-way
/// through one.
/// </summary>
public sealed class DriverPartsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-parts-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    public DriverPartsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void The_look_runs_the_registered_passes_in_their_order_each_built_from_the_driver()
    {
        var driver = new Daoris.Driver.Driver(_ledger.Client(), DriverConfig.Empty, AdapterSet.Built(), _home);

        var order = driver.LookPasses.Order;
        Assert.Equal(["sync", "claims", "landing"], order.Select(part => part.Name));
        Assert.Equal([LookPhase.Before, LookPhase.Before, LookPhase.After], order.Select(part => (LookPhase)part.Phase));

        // What each anchor runs, in the order it runs them.
        Assert.Collection(
            driver.LookPasses.In(LookPhase.Before),
            pass => Assert.IsType<LookSync>(pass),
            pass => Assert.IsType<LostClaimStop>(pass));
        Assert.IsType<DueLandings>(Assert.Single(driver.LookPasses.In(LookPhase.After)));
    }

    [Fact]
    public void A_phase_runs_each_part_after_the_ones_it_names_ties_by_name_and_a_cycle_is_refused_by_its_members()
    {
        var order = PartOrder.Of(
        [
            Declared("landing", LookPhase.After),
            Declared("zeta", LookPhase.Before),
            Declared("claims", LookPhase.Before, "zeta"),
            Declared("alpha", LookPhase.Before),
        ]);
        Assert.Equal(["alpha", "zeta", "claims", "landing"], order.Select(part => part.Name));

        var cycle = Assert.Throws<PartOrderException>(() => PartOrder.Of(
            [Declared("sync", LookPhase.Before, "claims"), Declared("claims", LookPhase.Before, "sync"), Declared("landing", LookPhase.After)]));
        Assert.Equal("the parts claims → sync → claims come after each other, so none of them can run first", cycle.Message);
    }

    [Fact]
    public void A_name_declared_twice_an_after_that_is_no_part_or_runs_later_and_a_part_its_root_cannot_build_are_refused()
    {
        Assert.Equal(
            "the part sync is declared twice, by LookSync and DueLandings",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("sync", LookPhase.Before), Declared("sync", LookPhase.After, type: typeof(DueLandings))])).Message);
        Assert.Equal(
            "the part claims comes after sink, which is no part",
            Assert.Throws<PartOrderException>(() => PartOrder.Of([Declared("claims", LookPhase.Before, "sink")])).Message);
        Assert.Equal(
            "the part claims runs in Before and comes after landing, which runs later, in After",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("claims", LookPhase.Before, "landing"), Declared("landing", LookPhase.After)])).Message);

        // A constructor asking for wiring the root does not hand fails at the first look, naming the part.
        var unbuilt = Assert.Throws<PartOrderException>(() => Parts.Build<ILookPass>(
            [Declared("landing", LookPhase.After, type: typeof(DueLandings))], type => (ILookPass)Activator.CreateInstance(type, new object())!));
        Assert.StartsWith("the part landing (DueLandings) could not be built from its root's wiring: ", unbuilt.Message);
    }

    private static PartDeclaration Declared(string name, LookPhase phase, params string[] after) =>
        Declared(name, phase, typeof(LookSync), after);

    private static PartDeclaration Declared(string name, LookPhase phase, Type type, params string[] after) =>
        new(name, phase, after, type);
}
