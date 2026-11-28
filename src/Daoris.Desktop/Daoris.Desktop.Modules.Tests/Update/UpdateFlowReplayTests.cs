using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The install's three failures of 2026-11-26 and 2026-11-27 , each replayed against a real updater on a scratch install with its
/// work, launcher, close, clock and the build's start on a copy of the home handed in: a build that never found idle with sessions always in a turn;
/// an update refused by the home proof that held every ask for hours, and a cancel that did not release it; a build restaged over a deferred one that
/// stayed unread, and a stop of twenty minutes that said nothing of what it waited on. Every transition is said in the machine log with its why.
/// </summary>
public sealed class UpdateFlowReplayTests : Bridge
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    private readonly string _install;

    private DateTimeOffset _now = Now;

    // Nine driven sessions, each at some stage of a turn: the work that never reads idle.
    private UpdateWork _work = new(Driven: 9, Turns: 9);

    private int _relaunched;

    private int _closed;

    private string? _closing;

    // How the build's start on a copy of the home ends: a refusal, a clean read, or a check that waits to be ended.
    private Func<CancellationToken, (string Code, string Sentence)?> _proof = _ => null;

    private int _proofs;

    private int _proofsEnded;

    private readonly ManualResetEventSlim _proofRunning = new();

    private readonly ManualResetEventSlim _unwound = new();

    private Action? _duringClose;

    public UpdateFlowReplayTests()
    {
        _install = Path.Combine(Home, "install");
        StagedInstalls.Prepare(_install);
        StagedInstalls.Stage(_install, "b1");
    }

    private string DataHome => Path.Combine(_install, "data");

    private InstallUpdater Updater() => new(
        _install, DataHome, new MachineLog(DataHome, "desktop", () => Now),
        work: () => _work,
        relaunch: _ =>
        {
            _relaunched++;
            return Task.FromResult(true);
        },
        close: () => _closed++,
        bus: Bus, clock: () => _now, pid: 4242)
    {
        ClosingForUpdate = build =>
        {
            _closing = build;
            _duringClose?.Invoke();
        },
        Look = TimeSpan.FromMilliseconds(250),
        Proof = (_, _, _, cancel) =>
        {
            Interlocked.Increment(ref _proofs);
            using var ended = cancel.Register(() => Interlocked.Increment(ref _proofsEnded));
            return _proof(cancel);
        },
    };

    /// <summary>A check that does not end until it is ended: the home proof of the install's 8.7 GB home.</summary>
    private (string Code, string Sentence)? Waits(CancellationToken cancel)
    {
        _proofRunning.Set();
        cancel.WaitHandle.WaitOne(Poll.Patience);
        cancel.ThrowIfCancellationRequested();
        return null;
    }

    // A check that ignores its token until it is let go: a copy or the scratch's removal that unwinds slowly after a cancel.
    private (string Code, string Sentence)? Stubborn(CancellationToken cancel)
    {
        _proofRunning.Set();
        _unwound.Wait(Poll.Patience);
        return null;
    }

    private static Task Until(Func<bool> condition, string seen) => Poll.Until(condition, () => seen);

    /// <summary>
    /// 2026-11-26: a build staged to install at idle waited 1.5 hours with 9+ sessions and never found idle. The wait says what it waits on and why it
    /// holds (deferred, past its bound), and *Update now* closes at once, each driven session recorded stopped for the update.
    /// </summary>
    [Fact]
    public async Task A_build_that_never_finds_idle_says_what_it_waits_on_and_update_now_closes_at_once()
    {
        using var updater = Updater();

        var draining = await updater.LookAsync();
        Assert.Equal((UpdateStates.Draining, UpdateWhy.Seen, 9, 9), (draining.State, draining.Why, draining.Driven, draining.Turns));

        _now = Now.AddMinutes(10);
        var deferred = await updater.LookAsync();
        Assert.Equal((UpdateStates.Deferred, UpdateWhy.Bound, 9, 9), (deferred.State, deferred.Why, deferred.Driven, deferred.Turns));
        Assert.Equal(new UpdateTransition("draining", "deferred", "bound", _now, "b1"), deferred.Transition);

        _now = Now.AddMinutes(90);
        Assert.Equal(UpdateStates.Deferred, (await updater.LookAsync()).State);
        Assert.Equal(0, _relaunched);

        var applying = await updater.SayAsync(UpdateMode.Now, "terminal");

        Assert.Equal(UpdateStates.Applying, applying.State);
        Assert.Equal((1, 1, "b1"), (_relaunched, _closed, _closing));
        Assert.Equal(
            ["none>draining:seen", "draining>deferred:bound", "deferred>stopping:now", "stopping>applying:checked"], UpdateLines.Said(DataHome));
        Assert.Equal(5400, UpdateLines.To(DataHome, "applying").Single()["waitedSeconds"]!.GetValue<long>());
    }

    /// <summary>
    /// 2026-11-26 16:28: an update refused by the home proof left the desktop answering every ask "Daoris is updating" for about three hours. A refusal
    /// returns to taking work at once: the presses go on, the starts are no longer held, and the state says why.
    /// </summary>
    [Fact]
    public async Task A_build_refused_by_the_home_proof_returns_to_taking_work_at_once()
    {
        _proof = _ => (HomeProbe.Code, "the staged build did not start clean on a copy of this home: the account `claude-code/acct-b` is no longer listed.");
        using var updater = Updater();

        var refused = await updater.SayAsync(UpdateMode.Now, "terminal");

        Assert.Equal(UpdateStates.Refused, refused.State);
        Assert.Equal(("home", UpdateWhy.Refusal), (refused.Problem?.Code, refused.Why));
        Assert.False(updater.Applying);
        Assert.False(updater.Draining);
        Assert.Equal((0, 0), (_relaunched, _closed));
        Assert.Equal(["none>stopping:now", "stopping>refused:refusal"], UpdateLines.Said(DataHome));
        var said = UpdateLines.To(DataHome, "refused").Single();
        Assert.Equal("home", said["detail"]!.GetValue<string>());
        Assert.Contains("claude-code/acct-b", said["sentence"]!.GetValue<string>());
        Assert.Contains("claude-code/acct-b", UpdateStateFile.Read(_install)!.Problem);
        // Said again at every look, checked no more: the proof is asked once.
        Assert.Equal(UpdateStates.Refused, (await updater.LookAsync()).State);
        Assert.Equal(1, _proofs);
    }

    /// <summary>
    /// 2026-11-26: `update --cancel` did not release the desktop, because the check ran under the close's hold and a word is read only after it ends.
    /// A word is judged at the next look, which never waits on the check: the check is ended, the presses go on and the state is `staged`.
    /// </summary>
    [Fact]
    public async Task A_cancel_ends_a_check_under_way_and_releases_the_presses_within_one_look()
    {
        _proof = Waits;
        using var updater = Updater();

        var stopping = await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");

        Assert.Equal(UpdateStates.Stopping, stopping.State);
        Assert.Equal(UpdateWaits.Check, stopping.Waiting?.On);
        Assert.True(updater.Applying);
        Assert.True(updater.Draining);

        var cancelled = await updater.SayAsync(UpdateMode.NotNow, "terminal");

        Assert.Equal((UpdateStates.Staged, UpdateWhy.NotNow), (cancelled.State, cancelled.Why));
        Assert.False(updater.Applying);
        Assert.False(updater.Draining);
        Assert.Equal(1, _proofsEnded);
        Assert.Equal((0, 0), (_relaunched, _closed));
        Assert.Equal(["none>stopping:now", "stopping>staged:not-now"], UpdateLines.Said(DataHome));
    }

    /// <summary>A look never waits on the check: with the check running a look answers at once, whoever asks.</summary>
    [Fact]
    public async Task A_look_while_the_check_runs_answers_without_waiting_for_it()
    {
        _proof = Waits;
        using var updater = Updater();
        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");

        var look = updater.LookAsync();

        await look.WaitAsync(Poll.Patience);
        Assert.Equal(UpdateStates.Stopping, look.Result.State);
        Assert.Equal(0, _proofsEnded);
        await updater.SayAsync(UpdateMode.NotNow, "terminal");
    }

    /// <summary>
    /// 2026-11-27: 0.0.39 restaged over the deferred 0.0.38 stayed unread until 11:22. A build restaged over a deferred one is read at the next look and
    /// drains anew; *Update now* then installs the new build in the one call.
    /// </summary>
    [Fact]
    public async Task A_build_restaged_over_a_deferred_one_is_read_at_once_and_update_now_installs_it()
    {
        using var updater = Updater();
        await updater.LookAsync();
        _now = Now.AddMinutes(11);
        Assert.Equal(UpdateStates.Deferred, (await updater.LookAsync()).State);

        StagedInstalls.Stage(_install, "b2");
        var read = await updater.LookAsync();

        Assert.Equal((UpdateStates.Draining, UpdateWhy.Restaged, "b2"), (read.State, read.Why, read.Staged?.Id));
        Assert.Equal(_now, read.Since);
        Assert.Equal(_now.AddMinutes(DrainSetting.Default), read.Until);

        var applying = await updater.SayAsync(UpdateMode.Now, "terminal");

        Assert.Equal(UpdateStates.Applying, applying.State);
        Assert.Equal((1, 1, "b2"), (_relaunched, _closed, _closing));
        Assert.Equal(
            ["none>draining:seen", "draining>deferred:bound", "deferred>draining:restaged", "draining>stopping:now", "stopping>applying:checked"],
            UpdateLines.Said(DataHome));
    }

    /// <summary>A build restaged while the stop for the one before checks it ends that stop at once, its check ended, and reads the new build.</summary>
    [Fact]
    public async Task A_build_restaged_while_the_stop_checks_the_one_before_ends_that_stop_and_reads_the_new_build()
    {
        _proof = Waits;
        using var updater = Updater();
        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");

        StagedInstalls.Stage(_install, "b2");
        var read = await updater.LookAsync();

        // The word named b1: b2 has none, so it drains by default.
        Assert.Equal((UpdateStates.Draining, UpdateWhy.Restaged, "b2"), (read.State, read.Why, read.Staged?.Id));
        Assert.Equal(1, _proofsEnded);
        Assert.False(updater.Applying);
        Assert.Equal(0, _relaunched);

        _proof = _ => null;
        var applying = await updater.SayAsync(UpdateMode.Now, "terminal");

        Assert.Equal(UpdateStates.Applying, applying.State);
        Assert.Equal("b2", _closing);
        Assert.Equal(
            ["none>stopping:now", "stopping>draining:restaged", "draining>stopping:now", "stopping>applying:checked"], UpdateLines.Said(DataHome));
    }

    /// <summary>
    /// 2026-11-27 11:23 to 11:46: stopping, refusing asks, with no word of what it waited on. A stop says each second what it waits on and for how long
    /// against its bound, to the page and in the machine log, and past its bound it is refused and work goes on.
    /// </summary>
    [Fact]
    public async Task A_stop_says_each_second_what_it_waits_on_and_past_its_bound_returns_to_taking_work()
    {
        _proof = Waits;
        using var updater = Updater();
        var stopping = await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");
        Assert.Equal(new UpdateWaiting(UpdateWaits.Check, 0, 180), stopping.Waiting);

        var told = Raised.Count(message => message.Type == "UPDATE_STATE");
        _now = Now.AddSeconds(5);
        Assert.Equal(new UpdateWaiting(UpdateWaits.Check, 5, 180), (await updater.LookAsync()).Waiting);
        _now = Now.AddSeconds(6);
        Assert.Equal(new UpdateWaiting(UpdateWaits.Check, 6, 180), (await updater.LookAsync()).Waiting);
        await Until(() => Raised.Count(message => message.Type == "UPDATE_STATE") >= told + 2, "the page was not told each second");

        _now = Now.AddSeconds(12);
        await updater.LookAsync();
        var waiting = UpdateLines.Of(DataHome, "update.waiting").Single();
        Assert.Equal((UpdateWaits.Check, 10, 180), (waiting["on"]!.GetValue<string>(), waiting["seconds"]!.GetValue<int>(), waiting["bound"]!.GetValue<int>()));

        _now = Now + updater.StopBound + TimeSpan.FromSeconds(1);
        var refused = await updater.LookAsync();

        Assert.Equal((UpdateStates.Refused, UpdateWhy.StopBound, InstallUpdater.BoundCode), (refused.State, refused.Why, refused.Problem?.Code));
        Assert.Contains("the check of the staged build", refused.Problem?.Message);
        Assert.False(updater.Applying);
        Assert.False(updater.Draining);
        Assert.Equal(1, _proofsEnded);
        Assert.Equal("check", UpdateLines.To(DataHome, "refused").Single()["detail"]!.GetValue<string>());
        Assert.Equal(["none>stopping:now", "stopping>refused:stop-bound"], UpdateLines.Said(DataHome));

        // Kept until the person's next word, which asks again.
        Assert.Equal(UpdateStates.Refused, (await updater.LookAsync()).State);
        _proof = _ => null;
        Assert.Equal(UpdateStates.Applying, (await updater.SayAsync(UpdateMode.Now, "terminal")).State);
    }

    /// <summary>The terminal reads the state without asking the application, which refuses every ask while it stops: it is kept under the install at every look.</summary>
    [Fact]
    public async Task The_state_is_kept_beside_the_install_for_the_terminal_at_every_look()
    {
        _proof = Waits;
        using var updater = Updater();
        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");

        var kept = UpdateStateFile.Read(_install)!;

        Assert.Equal(("stopping", "now", "b1", 4242), (kept.State, kept.Why, kept.Build, kept.Pid));
        Assert.Equal(new UpdateStateWait("check", 0, 180), kept.Waiting);
        Assert.Equal(new UpdateStateStep("none", "stopping", "now", Now, null), kept.Last);

        await updater.SayAsync(UpdateMode.NotNow, "terminal");

        var after = UpdateStateFile.Read(_install)!;
        Assert.Equal(("staged", "not-now"), (after.State, after.Why));
        Assert.Null(after.Waiting);
    }

    /// <summary>A swap this start confirmed is the state `installed` until the person puts it away; one the launcher put back or refused is `refused`.</summary>
    [Fact]
    public async Task A_confirmed_swap_is_the_state_installed_until_it_is_put_away()
    {
        Directory.Delete(Path.Combine(_install, "update", "staged"), recursive: true);
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Started, "b1", "0.0.2", "def5678", Now, []));
        using var updater = Updater();

        await updater.StartedAsync();

        Assert.Equal((UpdateStates.Installed, UpdateWhy.Started), (updater.State.State, updater.State.Why));
        Assert.Equal(UpdateStates.Installed, (await updater.LookAsync()).State);

        var dismissed = await updater.DismissAsync();

        Assert.Equal((UpdateStates.None, UpdateWhy.Dismissed), (dismissed.State, dismissed.Why));
        Assert.Equal(["none>installed:started", "installed>none:dismissed"], UpdateLines.Said(DataHome));
    }

    [Fact]
    public async Task A_swap_the_launcher_put_back_is_the_state_refused_with_the_reason_it_gave()
    {
        Directory.Delete(Path.Combine(_install, "update", "staged"), recursive: true);
        StagedBuild.WriteJournal(_install, new SwapRecord(
            SwapPhase.RolledBack, "b1", "0.0.2", null, Now, [], Reason: "exited", Detail: "the new build's application ended before it came up."));
        using var updater = Updater();

        await updater.StartedAsync();

        Assert.Equal((UpdateStates.Refused, UpdateWhy.Swap, "exited"), (updater.State.State, updater.State.Why, updater.State.Problem?.Code));
        Assert.Equal(["none>refused:swap"], UpdateLines.Said(DataHome));
    }

    /// <summary>A build staged while the swap's outcome stands takes the state over: what is staged is what the update is about.</summary>
    [Fact]
    public async Task A_build_staged_after_a_swap_leaves_the_state_of_the_swap_for_its_own()
    {
        Directory.Delete(Path.Combine(_install, "update", "staged"), recursive: true);
        StagedBuild.WriteJournal(_install, new SwapRecord(SwapPhase.Started, "b1", "0.0.2", "def5678", Now, []));
        using var updater = Updater();
        await updater.StartedAsync();
        Assert.Equal(UpdateStates.Installed, updater.State.State);

        StagedInstalls.Stage(_install, "b2");
        var read = await updater.LookAsync();

        Assert.Equal((UpdateStates.Draining, "b2"), (read.State, read.Staged?.Id));
        Assert.Equal("installed>draining:seen", UpdateLines.Said(DataHome).Last());
    }

    /// <summary>A cancel returns the presses within one look, however slowly the check it ended unwinds.</summary>
    [Fact]
    public async Task A_cancel_does_not_wait_for_a_check_that_unwinds_slowly()
    {
        _proof = Stubborn;
        using var updater = Updater();
        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");

        var cancelled = await updater.SayAsync(UpdateMode.NotNow, "terminal").WaitAsync(Poll.Patience);

        Assert.Equal((UpdateStates.Staged, false, false), (cancelled.State, updater.Applying, updater.Draining));
        Assert.Equal((0, 0), (_relaunched, _closed));
        _unwound.Set();
    }

    /// <summary>The same word said again is the word already acted on: the stop goes on, its check not restarted, and nothing is said twice.</summary>
    [Fact]
    public async Task Update_now_said_again_while_it_stops_does_not_restart_the_check()
    {
        _proof = Waits;
        using var updater = Updater();
        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _proofRunning.IsSet, "the check never began");

        _now = Now.AddSeconds(30);
        var again = await updater.SayAsync(UpdateMode.Now, "terminal");

        Assert.Equal((UpdateStates.Stopping, 30), (again.State, again.Waiting?.Seconds));
        Assert.Equal((1, 0), (_proofs, _proofsEnded));
        Assert.Equal(["none>stopping:now"], UpdateLines.Said(DataHome));
        await updater.SayAsync(UpdateMode.NotNow, "terminal");
    }

    /// <summary>A look while the application closes finds the state applying, never a stopping state with no stop: one launcher, one close.</summary>
    [Fact]
    public async Task A_look_while_the_application_closes_finds_applying_and_starts_nothing_more()
    {
        using var updater = Updater();
        UpdateState? met = null;
        _duringClose = () => met = Task.Run(() => updater.LookAsync()).GetAwaiter().GetResult();

        await updater.SayAsync(UpdateMode.Now, "terminal");
        await Until(() => _closed == 1, "the application never closed");

        Assert.Equal((UpdateStates.Applying, 1), (met?.State, _relaunched));
        Assert.Equal(["none>stopping:now", "stopping>applying:checked"], UpdateLines.Said(DataHome));
    }
}
