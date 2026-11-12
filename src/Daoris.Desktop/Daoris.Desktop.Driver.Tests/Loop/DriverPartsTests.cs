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
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-parts-" + Guid.NewGuid().ToString("N")[..8]);

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
        var driver = new Daoris.Driver.Driver(_ledger.Client(), DriverConfig.Empty, ProviderRegistry.Built(), _home);

        var order = driver.LookPasses.Order;
        Assert.Equal(
            ["kept", "sync", "claims", "plugins", "rules", "watch", "lines", "together", "setup", "snapshot", "paused", "supervisor",
                "alone", "unable", "pieces", "handovers", "policy", "committed", "overlaps", "overlaps-told", "roster", "channel", "reopens",
                "reviews", "opinions", "helpers", "checks", "partners", "brainstorms", "meetings", "plan",
                "answered", "intakes", "start", "rule-acts", "course-release", "landing-release", "supervise", "bind", "endings", "landing", "members", "run-outcomes", "plugin-commands",
                "through-notices"],
            order.Select(part => part.Name));
        Assert.Equal(
            [.. Enumerable.Repeat(LookPhase.Before, 3), .. Enumerable.Repeat(LookPhase.Local, 6), .. Enumerable.Repeat(LookPhase.Read, 22),
                .. Enumerable.Repeat(LookPhase.Planned, 7), LookPhase.Begun, LookPhase.Begun, .. Enumerable.Repeat(LookPhase.After, 5)],
            order.Select(part => (LookPhase)part.Phase));

        // What each anchor runs, in the order it runs them.
        Assert.Collection(
            driver.LookPasses.In(LookPhase.Before),
            pass => Assert.IsType<LookKept>(pass),
            pass => Assert.IsType<LookSync>(pass),
            pass => Assert.IsType<LostClaimStop>(pass));
        Assert.Collection(
            driver.LookPasses.In(LookPhase.Local),
            pass => Assert.IsType<PluginReload>(pass),
            pass => Assert.IsType<RuleSettling>(pass),
            pass => Assert.IsType<WatchLook>(pass),
            pass => Assert.IsType<MovedLines>(pass),
            pass => Assert.IsType<TogetherSteps>(pass),
            pass => Assert.IsType<WorkspacePlansLook>(pass));
        Assert.Collection(
            driver.LookPasses.In(LookPhase.Read),
            pass => Assert.IsType<SnapshotLook>(pass),
            pass => Assert.IsType<PausedLook>(pass),
            pass => Assert.IsType<SupervisorLook>(pass),
            pass => Assert.IsType<AloneLook>(pass),
            pass => Assert.IsType<UnableLook>(pass),
            pass => Assert.IsType<PiecesOnFileLook>(pass),
            pass => Assert.IsType<HandOversLook>(pass),
            pass => Assert.IsType<StaffPolicyLook>(pass),
            pass => Assert.IsType<CommittedLook>(pass),
            pass => Assert.IsType<OverlapsLook>(pass),
            pass => Assert.IsType<OverlapsToldLook>(pass),
            pass => Assert.IsType<RosterLook>(pass),
            pass => Assert.IsType<ChannelLook>(pass),
            pass => Assert.IsType<ReopensLook>(pass),
            pass => Assert.IsType<ReviewsLook>(pass),
            pass => Assert.IsType<OpinionsLook>(pass),
            pass => Assert.IsType<HelpersLook>(pass),
            pass => Assert.IsType<ChecksLook>(pass),
            pass => Assert.IsType<PartnersLook>(pass),
            pass => Assert.IsType<BrainstormsLook>(pass),
            pass => Assert.IsType<MeetingsLook>(pass),
            pass => Assert.IsType<PlanLook>(pass));
        Assert.Collection(
            driver.LookPasses.In(LookPhase.Planned),
            pass => Assert.IsType<AnsweredIntakesLook>(pass),
            pass => Assert.IsType<IntakesLook>(pass),
            pass => Assert.IsType<StartsLook>(pass),
            pass => Assert.IsType<RuleActsLook>(pass),
            pass => Assert.IsType<CourseReleaseLook>(pass),
            pass => Assert.IsType<LandingReleaseLook>(pass),
            pass => Assert.IsType<SuperviseLook>(pass));
        Assert.Collection(
            driver.LookPasses.In(LookPhase.Begun),
            pass => Assert.IsType<BindRunsLook>(pass),
            pass => Assert.IsType<EndingsLook>(pass));
        Assert.Collection(
            driver.LookPasses.In(LookPhase.After),
            pass => Assert.IsType<UnresolvedDeliveries>(pass),
            pass => Assert.IsType<ChannelMembersLook>(pass),
            pass => Assert.IsType<RunOutcomesLook>(pass),
            pass => Assert.IsType<PluginCommandsLook>(pass),
            pass => Assert.IsType<ThroughNoticesLook>(pass));
    }

    [Fact]
    public void The_state_refuses_a_read_before_the_pass_that_writes_it_and_carries_what_ended_as_the_look_handed_it()
    {
        List<SessionEnded> concluded = [];
        var state = new LookState(concluded);

        Assert.Equal(
            "the look's Snapshot is written by the snapshot pass, which has not run before the pass reading it: that pass runs after snapshot",
            Assert.Throws<InvalidOperationException>(() => state.Snapshot).Message);
        Assert.Contains("written by the snapshot pass", Assert.Throws<InvalidOperationException>(() => state.Mark).Message);
        Assert.Contains("written by the snapshot pass", Assert.Throws<InvalidOperationException>(() => state.ReservedBefore).Message);
        Assert.Contains("written by the supervisor pass", Assert.Throws<InvalidOperationException>(() => state.Alone).Message);
        Assert.Contains("written by the roster pass", Assert.Throws<InvalidOperationException>(() => state.Looked).Message);
        Assert.Contains("written by the opinions pass", Assert.Throws<InvalidOperationException>(() => state.Passes).Message);
        Assert.Contains("written by the opinions pass", Assert.Throws<InvalidOperationException>(() => state.JudgedOpinions).Message);
        Assert.Contains("written by the plan pass", Assert.Throws<InvalidOperationException>(() => state.Plan).Message);
        Assert.Contains("written by the start pass", Assert.Throws<InvalidOperationException>(() => state.Outcome).Message);
        Assert.Same(concluded, state.Concluded);

        state.Mark = 0;
        Assert.Equal(0, state.Mark);
    }

    [Fact]
    public async Task The_read_phase_takes_the_starts_opening_and_the_mark_then_the_snapshot_and_leaves_the_supervisor_s_bound_for_the_look()
    {
        var driver = new Daoris.Driver.Driver(_ledger.Client(), DriverConfig.Empty, ProviderRegistry.Built(), _home);
        var opening = new StartReservation("7", null, "engine", null);
        Assert.True(driver.Context.Runs.TryReserve(opening));
        var round = new LookRound(LookPart.Look, [], new LookState([]));

        foreach (var pass in driver.LookPasses.In(LookPhase.Read)) await pass.RunAsync(round, CancellationToken.None);

        Assert.Equal([opening], round.State.ReservedBefore);
        Assert.Equal(driver.Context.Harnesses.Mark(), round.State.Mark);
        Assert.Empty(round.State.Snapshot.Quests);
        Assert.NotNull(round.State.Alone);
        Assert.Empty(round.Events);
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
            "the part sync is declared twice, by LookSync and UnresolvedDeliveries",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("sync", LookPhase.Before), Declared("sync", LookPhase.After, type: typeof(UnresolvedDeliveries))])).Message);
        Assert.Equal(
            "the part claims comes after sink, which is no part",
            Assert.Throws<PartOrderException>(() => PartOrder.Of([Declared("claims", LookPhase.Before, "sink")])).Message);
        Assert.Equal(
            "the part claims runs in Before and comes after landing, which runs later, in After",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("claims", LookPhase.Before, "landing"), Declared("landing", LookPhase.After)])).Message);

        // A constructor asking for wiring the root does not hand fails at the first look, naming the part.
        var unbuilt = Assert.Throws<PartOrderException>(() => Parts.Build<ILookPass>(
            [Declared("landing", LookPhase.After, type: typeof(UnresolvedDeliveries))], type => (ILookPass)Activator.CreateInstance(type, new object())!));
        Assert.StartsWith("the part landing (UnresolvedDeliveries) could not be built from its root's wiring: ", unbuilt.Message);
    }

    private static PartDeclaration Declared(string name, LookPhase phase, params string[] after) =>
        Declared(name, phase, typeof(LookSync), after);

    private static PartDeclaration Declared(string name, LookPhase phase, Type type, params string[] after) =>
        new(name, phase, after, type);
}
