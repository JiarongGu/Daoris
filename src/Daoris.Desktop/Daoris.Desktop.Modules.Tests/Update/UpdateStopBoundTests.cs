using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// 0.0.43 refused `stop-bound` at 180 s because the staged build's check passed that under load, and the retry's took 88 s; 0.0.44's took 147 s. The bound a stop is held to is learned from the stops this machine measured, with a stated floor and ceiling, and
/// the check takes its place in the machine's admission rather than competing with the heavy runs.
/// </summary>
public sealed class UpdateStopBoundTests : Bridge
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 8, 0, 0, TimeSpan.Zero);

    private readonly string _install;

    private DateTimeOffset _now = Now;

    private (string Code, string Sentence)? _refusal;

    private Func<CancellationToken, (string Code, string Sentence)?> _proof = _ => null;

    private readonly ManualResetEventSlim _proofRunning = new();

    private readonly ManualResetEventSlim _release = new();

    public UpdateStopBoundTests()
    {
        _install = Path.Combine(Home, "install");
        StagedInstalls.Prepare(_install);
        StagedInstalls.Stage(_install, "b1");
    }

    private string DataHome => Path.Combine(_install, "data");

    private InstallUpdater Updater() => new(
        _install, DataHome, new MachineLog(DataHome, "desktop", () => Now),
        work: () => new UpdateWork(Driven: 0, Turns: 0),
        relaunch: _ => Task.FromResult(true),
        close: () => { },
        bus: Bus, clock: () => _now, pid: 4242)
    {
        Look = TimeSpan.FromMilliseconds(250),
        Placing = new CheckPlacing(
            StopBounds.Floor,
            new AdmissionWaiting(TimeSpan.FromMilliseconds(20), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(20), () => _now, KeepLock.Lives), 1),
        Proof = (_, _, _, cancel) =>
        {
            _proofRunning.Set();
            return _proof(cancel);
        },
    };

    private (string Code, string Sentence)? Slow(CancellationToken cancel)
    {
        _release.Wait(cancel);
        return _refusal;
    }

    private void Measured(params double[] seconds)
    {
        for (var at = 0; at < seconds.Length; at++)
        {
            LearnedNeeds.Record(DataHome, StopBounds.Key, new RunSample(Now.AddDays(-1 - at), 0, 0, seconds[at], false));
        }
    }

    private static Task Until(Func<bool> condition, string seen) => Poll.Until(condition, () => seen);

    [Fact]
    public void Nothing_measured_holds_a_stop_to_the_floor_and_the_floor_is_three_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(3), StopBounds.Floor);
        Assert.Equal(StopBounds.Floor, StopBounds.Of([], StopBounds.Floor).Bound);
        Assert.Equal(0, StopBounds.Of([], StopBounds.Floor).Samples);
    }

    [Fact]
    public void The_bound_is_twice_the_longest_of_the_recent_stops_never_under_the_floor_nor_over_the_ceiling()
    {
        // The 0.0.43 and 0.0.44 stops: 88 s, 147 s.
        var seen = StopBounds.Of([88, 147], StopBounds.Floor);
        Assert.Equal((TimeSpan.FromSeconds(294), 2), (seen.Bound, seen.Samples));

        Assert.Equal(StopBounds.Floor, StopBounds.Of([20, 31], StopBounds.Floor).Bound);
        Assert.Equal(StopBounds.Ceiling, StopBounds.Of([500], StopBounds.Floor).Bound);
        Assert.Equal(TimeSpan.FromMinutes(10), StopBounds.Ceiling);

        // Only the newest few are read, so a machine that got faster is held to a shorter bound again.
        var older = new[] { 400.0 }.Concat(Enumerable.Range(0, StopBounds.Recent).Select(_ => 60.0)).ToList();
        Assert.Equal(StopBounds.Floor, StopBounds.Of(older, StopBounds.Floor).Bound);
    }

    [Fact]
    public async Task A_check_slowed_past_the_old_bound_by_load_applies_and_its_time_is_kept()
    {
        Measured(88, 147);
        _proof = Slow;
        using var updater = Updater();
        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");
        Assert.Equal(new UpdateWaiting(UpdateWaits.Check, 0, 294), (await updater.LookAsync()).Waiting);

        // 200 s: past the 180 s of 0.0.43, within what this machine has measured.
        _now = Now.AddSeconds(200);
        var waiting = await updater.LookAsync();
        Assert.Equal(new UpdateWaiting(UpdateWaits.Check, 200, 294), waiting.Waiting);
        Assert.Equal(UpdateStates.Stopping, waiting.State);

        _release.Set();
        await Until(() => updater.State.State == UpdateStates.Applying, "the check that finished within its learned bound did not apply");
        var kept = LearnedNeeds.Samples(DataHome, StopBounds.Key);
        Assert.Equal(200, kept.OrderBy(sample => sample.At).Last().Seconds);
        Assert.Equal(3, kept.Count);
    }

    [Fact]
    public async Task A_check_that_hangs_is_still_refused_with_the_bound_it_was_held_to_said()
    {
        Measured(88, 147);
        _proof = Slow;
        using var updater = Updater();
        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");

        _now = Now.AddSeconds(295);
        var refused = await updater.LookAsync();

        Assert.Equal((UpdateStates.Refused, UpdateWhy.StopBound, InstallUpdater.BoundCode), (refused.State, refused.Why, refused.Problem?.Code));
        Assert.Contains("294 seconds", refused.Problem?.Message);
        Assert.Contains("the check of the staged build", refused.Problem?.Message);
        Assert.Contains("learned", refused.Problem?.Message);
        Assert.Equal(2, LearnedNeeds.Samples(DataHome, StopBounds.Key).Count);
    }

    [Fact]
    public async Task With_nothing_measured_a_hung_check_is_refused_at_the_floor_saying_so()
    {
        _proof = Slow;
        using var updater = Updater();
        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");

        _now = Now.AddSeconds(181);
        var refused = await updater.LookAsync();

        Assert.Equal(UpdateWhy.StopBound, refused.Why);
        Assert.Contains("180 seconds", refused.Problem?.Message);
        Assert.Contains("floor", refused.Problem?.Message);
    }

    [Fact]
    public async Task A_check_refused_by_what_it_found_keeps_its_time_too()
    {
        _refusal = ("home", "the home does not read.");
        _proof = Slow;
        using var updater = Updater();
        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");
        _now = Now.AddSeconds(40);
        _release.Set();

        await Until(() => updater.State.State == UpdateStates.Refused, "the check's refusal was not said");
        Assert.Equal(40, LearnedNeeds.Samples(DataHome, StopBounds.Key).Single().Seconds);
    }

    [Fact]
    public async Task The_check_holds_a_place_in_the_machines_admission_while_it_runs_and_lets_it_go_after()
    {
        IReadOnlyList<AdmissionEntry>? held = null;
        _proof = _ =>
        {
            held = MachineAdmission.Look(DataHome, now: _now).Holds;
            return null;
        };
        using var updater = Updater();

        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => updater.State.State == UpdateStates.Applying, "the check did not pass");

        var entry = Assert.Single(held!);
        Assert.Equal(Environment.ProcessId, entry.Pid);
        Assert.Contains("staged build", entry.What);
        await Until(() => MachineAdmission.Look(DataHome, now: _now).Holds.Count == 0, "the place was kept after the check");
    }

    [Fact]
    public async Task A_check_waits_for_a_place_without_starting_and_its_bound_counts_from_the_place()
    {
        Measured(88, 147);
        using var other = await MachineAdmission.TakeAsync(DataHome, new AdmissionRequest("a gate"), TextWriter.Null, CancellationToken.None, capacity: 1);
        using var updater = Updater();

        var stopping = await updater.SayAsync(UpdateMode.Now, "terminal");

        Assert.Equal(new UpdateWaiting(UpdateWaits.Place, 0, 180), stopping.Waiting);
        Assert.False(_proofRunning.IsSet);

        // Past the check's 294 s but within the floor of the wait: a longer wait for a place is the machine's, not the check's.
        _now = Now.AddSeconds(100);
        Assert.Equal(new UpdateWaiting(UpdateWaits.Place, 100, 180), (await updater.LookAsync()).Waiting);
        other.Dispose();
        await Until(() => _proofRunning.IsSet, "the check did not start once the place was free");
        Assert.Equal(UpdateStates.Applying, await StateAsync(updater, UpdateStates.Applying));
        // The 100 s waited for the place are the machine's, not the check's: only the check's own time is kept.
        Assert.Equal(0, LearnedNeeds.Samples(DataHome, StopBounds.Key).OrderBy(sample => sample.At).Last().Seconds);
    }

    [Fact]
    public async Task A_wait_for_a_place_that_does_not_come_is_refused_at_the_floor_and_the_check_never_starts()
    {
        using var other = await MachineAdmission.TakeAsync(DataHome, new AdmissionRequest("a gate"), TextWriter.Null, CancellationToken.None, capacity: 1);
        using var updater = Updater();
        await updater.SayAsync(UpdateMode.Now, "terminal");

        _now = Now.AddSeconds(181);
        var refused = await updater.LookAsync();

        Assert.Equal((UpdateStates.Refused, UpdateWhy.StopBound, InstallUpdater.BoundCode), (refused.State, refused.Why, refused.Problem?.Code));
        Assert.Contains("a place in the machine's admission", refused.Problem?.Message);
        Assert.Contains("180 seconds", refused.Problem?.Message);
        Assert.False(_proofRunning.IsSet);
        await Until(() => MachineAdmission.Look(DataHome, now: _now).Waits.Count == 0, "the wait for a place was left standing");
    }

    private static async Task<string> StateAsync(InstallUpdater updater, string state)
    {
        await Until(() => updater.State.State == state, $"the state never reached {state}");
        return updater.State.State;
    }
}
