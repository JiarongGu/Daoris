using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A drain holds new work for its bound at most (<see cref="DrainSetting"/>): one long turn keeps the work from idle, an ask published
/// meanwhile waits while the drain holds, and past the bound the drain defers itself, so the look plans with the intake again and the
/// ask's intake starts; the build then installs at the first look that finds nothing running, or at the person's word.
/// </summary>
public sealed class UpdateDrainBoundTests : Bridge
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 0, 29, 0, TimeSpan.Zero);

    private readonly string _install;

    private DateTimeOffset _now = Now;

    // One builder session's long turn: a driven session running and its turn in flight.
    private UpdateWork _work = new(Driven: 1, Turns: 1);

    private Func<UpdateWork>? _reads;

    private TimeSpan _bound = TimeSpan.FromMinutes(DrainSetting.Default);

    private int _closed;

    public UpdateDrainBoundTests()
    {
        _install = Path.Combine(Home, "install");
        StagedInstalls.Prepare(_install);
        StagedInstalls.Stage(_install, "b1");
    }

    private string DataHome => Path.Combine(_install, "data");

    private InstallUpdater Updater() => new(
        _install, DataHome, new MachineLog(DataHome, "desktop", () => Now),
        work: () => _reads?.Invoke() ?? _work,
        relaunch: _ => Task.FromResult(true),
        close: () => _closed++,
        bus: Bus, clock: () => _now, pid: 4242)
        { ClosingForUpdate = _ => { }, Bound = () => _bound };

    // The config a look plans with, as the watch asks it of the drain at each look: drained while starts are held.
    private static DriverConfig Planned(InstallUpdater updater)
    {
        var config = ActorChoices.With(DriverConfig.Empty, "intake", [new ActorChoice("codex-acp")]);
        return updater.Draining ? InstallUpdate.Drained(config) : config;
    }

    [Fact]
    public async Task A_long_turn_holds_a_new_ask_s_intake_until_the_bound_and_past_it_the_intake_starts()
    {
        using var updater = Updater();

        var draining = await updater.LookAsync();
        Assert.Equal(UpdateStates.Draining, draining.State);
        Assert.Equal(Now, draining.Since);
        Assert.Equal(Now.AddMinutes(10), draining.Until);
        Assert.Null(ActorChoices.FirstAgent("intake", Planned(updater)));

        // The ask arrives during the drain, a minute before the bound: it still waits.
        _now = Now.AddMinutes(9);
        Assert.Equal(UpdateStates.Draining, (await updater.LookAsync()).State);
        Assert.True(updater.Draining);
        Assert.Null(ActorChoices.FirstAgent("intake", Planned(updater)));

        _now = Now.AddMinutes(10);
        var deferred = await updater.LookAsync();

        Assert.Equal(UpdateStates.Deferred, deferred.State);
        Assert.False(updater.Draining);
        Assert.False(updater.Applying);
        Assert.Equal("codex-acp", ActorChoices.FirstAgent("intake", Planned(updater)));
        // What it waits on and since when, said beside it.
        Assert.Equal((1, 1, Now, Now.AddMinutes(10)), (deferred.Driven, deferred.Turns, deferred.Since, deferred.Until));
        Assert.Equal(UpdateMode.WhenIdle, deferred.Mode);
        Assert.Equal(0, _closed);

        _now = Now.AddMinutes(30);
        Assert.Equal(UpdateStates.Deferred, (await updater.LookAsync()).State);
        var said = Logged("update.deferred").Single();
        Assert.Equal("b1", said["build"]!.GetValue<string>());
        Assert.Equal(600, said["waitedSeconds"]!.GetValue<long>());
    }

    /// <summary>Deferred, the build installs at the first look that finds nothing running, as a drain's would.</summary>
    [Fact]
    public async Task A_deferred_build_installs_at_the_first_look_nothing_runs()
    {
        using var updater = Updater();
        await updater.LookAsync();
        _now = Now.AddMinutes(10);
        Assert.Equal(UpdateStates.Deferred, (await updater.LookAsync()).State);

        _work = new UpdateWork(0, 0);
        var applying = await updater.LookAsync();

        Assert.Equal(UpdateStates.Applying, applying.State);
        Assert.Equal(1, _closed);
        Assert.Equal(Now, applying.Since);
        Assert.Equal("idle", Logged("update.applying").Single()["by"]!.GetValue<string>());
    }

    /// <summary>Work that began after a deferred look read none is read again under the hold, and the build stays deferred.</summary>
    [Fact]
    public async Task Work_begun_as_a_deferred_build_meets_idle_keeps_it_deferred_not_draining()
    {
        using var updater = Updater();
        await updater.LookAsync();
        _now = Now.AddMinutes(10);
        await updater.LookAsync();
        var answers = new Queue<UpdateWork>([new UpdateWork(0, 0), new UpdateWork(1, 0)]);
        _reads = () => answers.Count > 0 ? answers.Dequeue() : new UpdateWork(1, 0);

        var state = await updater.LookAsync();

        Assert.Equal(UpdateStates.Deferred, state.State);
        Assert.False(updater.Draining);
        Assert.Equal(0, _closed);
    }

    /// <summary>The bound is the machine's setting, read at each look.</summary>
    [Fact]
    public async Task The_bound_is_the_machine_s_setting_read_at_each_look()
    {
        _bound = TimeSpan.FromMinutes(3);
        using var updater = Updater();
        Assert.Equal(Now.AddMinutes(3), (await updater.LookAsync()).Until);

        _now = Now.AddMinutes(3);

        Assert.Equal(UpdateStates.Deferred, (await updater.LookAsync()).State);
    }

    /// <summary>The person's words still hold: *Update now* installs a deferred build, *Not now* puts it off; a newer build drains anew.</summary>
    [Fact]
    public async Task The_person_s_word_holds_over_a_deferred_build_and_a_newer_build_drains_from_its_own_since()
    {
        using var updater = Updater();
        await updater.LookAsync();
        _now = Now.AddMinutes(10);
        await updater.LookAsync();

        Assert.Equal(UpdateStates.Waiting, (await updater.SayAsync(UpdateMode.NotNow, "screen")).State);

        StagedInstalls.Stage(_install, "b2");
        _now = Now.AddMinutes(12);
        var again = await updater.LookAsync();
        Assert.Equal(UpdateStates.Draining, again.State);
        Assert.Equal(Now.AddMinutes(12), again.Since);
        Assert.True(updater.Draining);

        _now = Now.AddMinutes(22);
        Assert.Equal(UpdateStates.Deferred, (await updater.LookAsync()).State);
        Assert.Equal(UpdateStates.Applying, (await updater.SayAsync(UpdateMode.Now, "screen")).State);
        Assert.Equal(1, _closed);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(25L, 25)]
    [InlineData(0L, 10)]
    [InlineData(500L, 10)]
    public void The_setting_is_read_in_its_bounds_or_is_the_default(long? stored, int minutes)
    {
        var config = DriverConfig.Empty with { DrainMinutes = DrainSetting.Read(stored) };

        Assert.Equal(TimeSpan.FromMinutes(minutes), DrainSetting.Of(config));
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithDrain(0));
    }

    private List<JsonObject> Logged(string name)
    {
        var folder = DaorisHome.Entry(DataHome, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .Where(line => line["event"]!.GetValue<string>() == name)
            .Select(line => line["data"]!.AsObject())
            .ToList();
    }
}
