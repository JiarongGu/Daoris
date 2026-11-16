using System.Collections.Immutable;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A staged build is never installed while a run's plugin step is in flight, as the loop's work says it beside its sessions and turns: with
/// no driven session and no turn, a release in flight holds the drain, said as the steps it waits on, and the look once it settles installs
/// the build. The home's run records are never read by the updater, so an orphaned record nothing will ever ask after holds nothing.
/// </summary>
public sealed class UpdatePluginStepTests : Bridge, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 3, 53, 0, TimeSpan.Zero);

    private readonly string _install;

    private int _closed;

    private int _steps;

    public UpdatePluginStepTests()
    {
        _install = Path.Combine(Home, "install");
        StagedInstalls.Prepare(_install);
        StagedInstalls.Stage(_install, "b1");
    }

    private string DataHome => Path.Combine(_install, "data");

    private InstallUpdater Updater() => new(
        _install, DataHome, new MachineLog(DataHome, "desktop", () => Now),
        work: () => new UpdateWork(0, 0) { Steps = _steps },
        relaunch: _ => Task.FromResult(true),
        close: () => _closed++,
        bus: Bus, clock: () => Now, pid: 4242)
        { ClosingForUpdate = _ => { } };

    [Fact]
    public async Task A_release_the_loop_says_is_in_flight_holds_the_install_and_its_settling_installs_it()
    {
        _steps = 1;
        using var updater = Updater();

        var held = await updater.LookAsync();

        Assert.Equal(UpdateStates.Draining, held.State);
        Assert.Equal((0, 0, 1), (held.Driven, held.Turns, held.Steps));
        Assert.Equal(0, _closed);

        _steps = 0;

        Assert.Equal(UpdateStates.Applying, (await updater.LookAsync()).State);
        Assert.Equal(1, _closed);
    }

    /// <summary>
    /// A run's record left by a retired repository, its node declared by no graph and its plugin gone, keeps a release unknown that nothing
    /// will ever ask after: the loop counts no step for it, and the install goes on.
    /// </summary>
    [Fact]
    public async Task An_orphaned_run_record_holding_a_release_sent_does_not_hold_the_install()
    {
        new NodeRecordFiles(DataHome, () => true).Write(new NodeRecord(RunUnits.IdOf("r1"))
        {
            Statuses = ImmutableSortedDictionary<string, NodeStatus>.Empty.Add("release",
                new NodeStatus(RunUnits.IdOf("r1"), "release", NodeStates.Active) { Attempt = 1, Tries = 1 }),
            Attempts =
            [
                new NodeAttempt("release", 1, "acme.deploy",
                    new WorkPerformCall("unit-1", "acme.deploy/release", 1, "i-1", "k-1", "r1", [], Now.AddMinutes(15)), AttemptStates.Unknown)
                {
                    RecordedAt = Now,
                },
            ],
        });
        using var updater = Updater();

        Assert.Equal(UpdateStates.Applying, (await updater.LookAsync()).State);
        Assert.Equal(1, _closed);
    }
}
