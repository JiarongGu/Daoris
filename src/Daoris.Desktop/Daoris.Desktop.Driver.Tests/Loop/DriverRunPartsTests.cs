using Daoris.Contracts.Parts;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A driven session's run takes the stages its modules register, phase by phase in their order, each built from the driver as its
/// wiring; an order the marks cannot make, or a stage the driver cannot build, is refused at the first run and never part-way through
/// one, and the run's state refuses a read before the stage that writes it.
/// </summary>
public sealed class DriverRunPartsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-run-parts-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    public DriverRunPartsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void The_run_takes_the_registered_stages_in_their_order_each_built_from_the_driver()
    {
        var driver = new Daoris.Driver.Driver(_ledger.Client(), DriverConfig.Empty, ProviderRegistry.Built(), _home);

        var order = driver.RunStages.Order;
        Assert.Equal(["agent", "clean", "account", "access", "tree", "steward", "bind", "setup", "trust", "continue", "open", "files", "hand"], order.Select(part => part.Name));
        Assert.Equal(
            [.. Enumerable.Repeat(RunPhase.Choose, 4), RunPhase.Grown, .. Enumerable.Repeat(RunPhase.Placed, 3), .. Enumerable.Repeat(RunPhase.Cleared, 2), RunPhase.Opened, RunPhase.Opened, RunPhase.Handed],
            order.Select(part => (RunPhase)part.Phase));

        // What each anchor runs, in the order it runs them.
        Assert.Collection(
            driver.RunStages.In(RunPhase.Choose),
            stage => Assert.IsType<StartAgent>(stage),
            stage => Assert.IsType<CleanCheckout>(stage),
            stage => Assert.IsType<AccountWalk>(stage),
            stage => Assert.IsType<StartAccess>(stage));
        Assert.Collection(driver.RunStages.In(RunPhase.Grown), stage => Assert.IsType<GrowTree>(stage));
        Assert.Collection(
            driver.RunStages.In(RunPhase.Placed),
            stage => Assert.IsType<StewardPlace>(stage),
            stage => Assert.IsType<RunBinding>(stage),
            stage => Assert.IsType<SetUpStep>(stage));
        Assert.Collection(
            driver.RunStages.In(RunPhase.Cleared),
            stage => Assert.IsType<TrustedTree>(stage),
            stage => Assert.IsType<GoOnRecord>(stage));
        Assert.Collection(driver.RunStages.In(RunPhase.Opened), stage => Assert.IsType<OpenRecord>(stage), stage => Assert.IsType<TreeFilesRun>(stage));
        Assert.Collection(driver.RunStages.In(RunPhase.Handed), stage => Assert.IsType<HandToSession>(stage));
    }

    [Fact]
    public void The_state_refuses_a_read_before_the_stage_that_writes_it_and_reads_a_null_once_written()
    {
        var state = new RunState();

        Assert.Equal(
            "the run's Agent is written by the agent stage, which has not run before the stage reading it: that stage runs after agent",
            Assert.Throws<InvalidOperationException>(() => state.Agent).Message);
        Assert.Contains("written by the agent stage", Assert.Throws<InvalidOperationException>(() => state.HandedQuest).Message);
        Assert.Contains("written by the account stage", Assert.Throws<InvalidOperationException>(() => state.Selection).Message);
        Assert.Contains("written by the account stage", Assert.Throws<InvalidOperationException>(() => state.Resume).Message);
        Assert.Contains("written by the account stage", Assert.Throws<InvalidOperationException>(() => state.CarriedModel(null)).Message);
        Assert.Contains("written by the access stage", Assert.Throws<InvalidOperationException>(() => state.Permissions).Message);
        Assert.Contains("written by the tree stage", Assert.Throws<InvalidOperationException>(() => state.WorkTree).Message);
        Assert.Contains("written by the tree stage", Assert.Throws<InvalidOperationException>(() => state.WorkFolder).Message);
        Assert.Contains("written by the tree stage", Assert.Throws<InvalidOperationException>(() => state.Tree).Message);
        Assert.Contains("written by the tree stage", Assert.Throws<InvalidOperationException>(() => state.Starting).Message);
        Assert.Contains("written by the open stage", Assert.Throws<InvalidOperationException>(() => state.Opened).Message);
        Assert.Contains("written by the open stage", Assert.Throws<InvalidOperationException>(() => state.Carry).Message);
        Assert.Contains("written by the continue stage", Assert.Throws<InvalidOperationException>(() => state.GoneOn).Message);
        Assert.Contains("written by the trust stage", Assert.Throws<InvalidOperationException>(() => state.Preflight).Message);
        Assert.Contains("written by the bind stage", Assert.Throws<InvalidOperationException>(() => state.Bound).Message);
        Assert.Contains("written by the setup stage", Assert.Throws<InvalidOperationException>(() => state.SetUp).Message);

        state.HandedQuest = null;
        state.MovedTo = null;
        Assert.Null(state.HandedQuest);
        Assert.Null(state.CarriedModel(null));
    }

    [Fact]
    public void A_stage_named_twice_a_cycle_and_a_stage_its_root_cannot_build_are_refused()
    {
        Assert.Equal(
            "the part agent is declared twice, by StartAgent and AccountWalk",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("agent", typeof(StartAgent)), Declared("agent", typeof(AccountWalk))])).Message);
        Assert.Equal(
            "the parts account → clean → account come after each other, so none of them can run first",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("clean", typeof(CleanCheckout), "account"), Declared("account", typeof(AccountWalk), "clean")])).Message);

        var unbuilt = Assert.Throws<PartOrderException>(() => Parts.Build<IRunStage>(
            [Declared("agent", typeof(StartAgent))], type => (IRunStage)Activator.CreateInstance(type, new object())!));
        Assert.StartsWith("the part agent (StartAgent) could not be built from its root's wiring: ", unbuilt.Message);
    }

    private static PartDeclaration Declared(string name, Type type, params string[] after) => new(name, RunPhase.Choose, after, type);
}
