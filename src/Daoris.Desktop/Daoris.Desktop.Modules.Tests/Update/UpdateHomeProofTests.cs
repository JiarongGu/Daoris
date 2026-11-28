using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A staged build is swapped in only after it starts clean on a copy of the home: a refusal of its proof closes nothing,
/// the old build keeps running, the state says why, and it is said again at every look; the person's next word asks the proof again, and
/// a clean one closes for the build.
/// </summary>
public sealed class UpdateHomeProofTests : Bridge, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private const string Why = "the staged build did not start clean on a copy of this home: the account `claude-code/acct-b` is no longer listed. Nothing was replaced and this build keeps running.";

    private readonly string _install;

    private DateTimeOffset _now = Now;

    private int _closed;

    private int _proofs;

    private bool _clean;

    public UpdateHomeProofTests()
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
        bus: Bus, clock: () => _now, pid: 4242)
    {
        Running = "0.0.1",
        ClosingForUpdate = _ => { },
        Proof = (_, _, _, _) =>
        {
            _proofs++;
            return _clean ? null : (HomeProbe.Code, Why);
        },
    };

    [Fact]
    public async Task A_build_whose_proof_is_refused_closes_nothing_says_why_and_the_next_word_asks_again()
    {
        using var updater = Updater();

        var refused = await updater.SayAsync(UpdateMode.Now, "screen");

        Assert.Equal(UpdateStates.Refused, refused.State);
        Assert.Equal(HomeProbe.Code, refused.Problem?.Code);
        Assert.Equal(Why, refused.Problem?.Message);
        Assert.Equal(0, _closed);

        // A look with no new word says it again and asks nothing again: the copy of a home is no look's work.
        Assert.Equal(UpdateStates.Refused, (await updater.LookAsync()).State);
        Assert.Equal(1, _proofs);

        _clean = true;
        _now = Now.AddMinutes(1);
        Assert.Equal(UpdateStates.Applying, (await updater.SayAsync(UpdateMode.Now, "screen")).State);
        Assert.Equal(2, _proofs);
        Assert.Equal(1, _closed);
    }

    [Fact]
    public async Task A_build_staged_for_when_idle_is_proved_at_the_moment_it_would_close_and_not_before()
    {
        using var updater = Updater();

        var seen = await updater.LookAsync();

        // Idle: the look reaches the moment, asks the proof, and the refusal holds the close.
        Assert.Equal(UpdateStates.Refused, seen.State);
        Assert.Equal(HomeProbe.Code, seen.Problem?.Code);
        Assert.Equal(0, _closed);
    }

    [Fact]
    public async Task A_clean_proof_swaps_in()
    {
        _clean = true;
        using var updater = Updater();

        Assert.Equal(UpdateStates.Applying, (await updater.SayAsync(UpdateMode.Now, "screen")).State);
        Assert.Equal(1, _closed);
        Assert.Equal(1, _proofs);
    }
}
