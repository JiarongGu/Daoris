using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where the loop works a workspace plan (WSSETUP6, D124 §4.1): each look, before it reads the quests, so a set-up the plan
/// publishes is planned and started in that same look; a home with no plan to work reads nothing; and a plan the service
/// could not answer for is said, and the look goes on. Over the stand-in ledger and the stand-in run, with the plan's world
/// standing in, so nothing here starts a process.
/// </summary>
public sealed class WorkspaceSetupLoopTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-wsplan-loop-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(60));

    private readonly WorkspaceSetupStandIn _world = new WorkspaceSetupStandIn().With(WorkspaceSetupStandIn.Row("atlas"));

    public WorkspaceSetupLoopTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("atlas", "/checkouts/atlas");
        // The service's quest list is the ledger's: what the plan publishes is what the look plans over.
        _world.Publishes = quest => _ledger.Publish(quest.Id, quest.To, quest.Title);
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Drivable here, in trees of its own, on an agent that rides the protocol door.</summary>
    private static DriverConfig Config => (DriverConfig.Empty with { Adapter = "claude-code-acp", Cap = 2 })
        .WithDrivable("atlas", true).WithTrees("atlas", true);

    [Fact]
    public async Task A_look_publishes_the_plans_next_set_up_before_it_plans_so_the_same_look_starts_it()
    {
        WorkspaceSetupFile.Save(_home, new WorkspaceSetupPlan("work", ["atlas"], 1, 0) { Created = DateTimeOffset.UtcNow });
        var (driver, runs) = Driver();

        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        var quest = Assert.Single(_world.Quests);
        Assert.Equal([quest.Id], runs.Started);
        Assert.Contains(look.Events, line => line.StartsWith("setup  work: asked `atlas` to set itself up", StringComparison.Ordinal));
        Assert.Equal(quest.Id, WorkspaceSetupFile.Load(_home, "work").Plan!.Published["atlas"]);
    }

    [Fact]
    public async Task A_look_with_no_plan_to_work_asks_the_plans_world_nothing()
    {
        var (driver, _) = Driver();

        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal(0, _world.RegistrationsRead);
        Assert.DoesNotContain(look.Events, line => line.StartsWith("setup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_plan_the_service_could_not_answer_for_is_said_and_the_look_goes_on()
    {
        WorkspaceSetupFile.Save(_home, new WorkspaceSetupPlan("work", ["atlas"], 1, 0) { Created = DateTimeOffset.UtcNow });
        _world.Fails = new HttpRequestException("the service went away");
        _ledger.Publish("q7", "atlas", "Say which tile sizes the map serves");
        var (driver, runs) = Driver();

        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Contains(look.Events, line => line.StartsWith("setup  the workspace plans could not be worked this look", StringComparison.Ordinal)
            && line.Contains("the service went away", StringComparison.Ordinal));
        Assert.Equal(["q7"], runs.Started);
    }

    private (Daoris.Driver.Driver Driver, StandInRuns Runs) Driver()
    {
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            service, Config, adapters, _home, harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")))
        {
            Runner = runs.Runner,
            SetupPlans = () => _world,
        };
        runs.Keeping = driver.Running;
        return (driver, runs);
    }
}
