using System.Collections.Immutable;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A staged build is never installed while a run's plugin step is in flight: with no driven session and no turn, a release its plugin
/// was sent and has not settled holds the drain, said as the steps it waits on, and the look once it settles installs the build (D301).
/// </summary>
public sealed class UpdatePluginStepTests : Bridge, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 3, 53, 0, TimeSpan.Zero);

    private readonly string _install;

    private int _closed;

    public UpdatePluginStepTests()
    {
        _install = Path.Combine(Home, "install");
        StagedInstalls.Prepare(_install);
        StagedInstalls.Stage(_install, "b1");
    }

    private string DataHome => Path.Combine(_install, "data");

    private InstallUpdater Updater() => new(
        _install, DataHome, new MachineLog(DataHome, "desktop", () => Now),
        work: () => new UpdateWork(0, 0),
        relaunch: _ => Task.FromResult(true),
        close: () => _closed++,
        bus: Bus, clock: () => Now, pid: 4242)
        { ClosingForUpdate = _ => { } };

    // A run's record as the runtime keeps one: the release's node holding its attempt, sent or settled.
    private void Release(string state) => new NodeRecordFiles(DataHome, () => true).Write(new NodeRecord(RunUnits.IdOf("r1"))
    {
        Statuses = ImmutableSortedDictionary<string, NodeStatus>.Empty.Add("release", new NodeStatus(
            RunUnits.IdOf("r1"), "release", state == AttemptStates.Settled ? NodeStates.Reached : NodeStates.Active) { Attempt = 1, Tries = 1 }),
        Attempts =
        [
            new NodeAttempt("release", 1, "acme.deploy",
                new WorkPerformCall("unit-1", "acme.deploy/release", 1, "i-1", "k-1", "r1", [], Now.AddMinutes(15)), state)
            {
                RecordedAt = Now,
                Outcome = state == AttemptStates.Settled ? "reached" : null,
            },
        ],
    });

    [Fact]
    public async Task A_release_sent_to_its_plugin_and_unsettled_holds_the_install_and_its_settling_installs_it()
    {
        Release(AttemptStates.Sent);
        using var updater = Updater();

        var held = await updater.LookAsync();

        Assert.Equal(UpdateStates.Draining, held.State);
        Assert.Equal((0, 0, 1), (held.Driven, held.Turns, held.Steps));
        Assert.Equal(0, _closed);

        Release(AttemptStates.Settled);

        Assert.Equal(UpdateStates.Applying, (await updater.LookAsync()).State);
        Assert.Equal(1, _closed);
    }
}
