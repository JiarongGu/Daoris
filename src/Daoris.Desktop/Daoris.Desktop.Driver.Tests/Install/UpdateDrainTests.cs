using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// UPDATE1 (D139 §2): while an update drains, the watch starts nothing new — a quest, a resume, a carry-on, an answered
/// park, an intake — and lets what runs run on; the hold is said as the update's, and a hold the person made keeps its
/// own sentence. The looks go on, so an ending is still reported.
/// </summary>
/// <remarks>
/// Over the real client and planner with the service's doors and each start's run standing in, in-process, as the DEV3
/// tests drive the watch: the scheduling is what is held here.
/// </remarks>
public sealed class UpdateDrainTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-drain-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(60));

    public UpdateDrainTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine")).Register("tools", DaorisHome.Entry(_home, HomeEntry.ToolVersions));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task While_an_update_drains_nothing_new_starts_and_the_hold_is_said_as_the_update_s()
    {
        _ledger.Publish("q1", "engine");
        var draining = true;
        var (watch, runs) = Watch(() => draining);
        var reports = new List<TickReport>();

        var watching = watch.RunAsync(
            (report, _) =>
            {
                lock (reports) reports.Add(report);
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => Count(reports) >= 2, () => $"{Count(reports)} look(s)", Poll.Patience);

            Assert.Empty(runs.Started);
            var sitting = Last(reports)!.Considerations.Single(c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
            Assert.Equal(InstallUpdate.HoldReason, sitting.Reason);
            Assert.True(InstallUpdate.IsHeldForUpdate(sitting));

            // *Not now*: the next look starts it.
            draining = false;
            watch.Nudge();
            await Poll.Until(() => runs.Started.Count == 1, () => $"started: {string.Join(", ", runs.Started)}", Poll.Patience);
            Assert.Equal(["q1"], runs.Started);
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    [Fact]
    public async Task A_session_running_when_the_drain_begins_runs_on_and_its_ending_is_still_reported()
    {
        _ledger.Publish("q1", "engine");
        var draining = false;
        var (watch, runs) = Watch(() => draining);
        var reports = new List<TickReport>();

        var watching = watch.RunAsync(
            (report, _) =>
            {
                lock (reports) reports.Add(report);
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => runs.Started.Count == 1, () => "q1 never started", Poll.Patience);
            draining = true;
            _ledger.Publish("q2", "tools");
            watch.Nudge();
            await Poll.Until(
                () => Last(reports)?.Considerations.Any(c => c.Quest.Id == "q2" && InstallUpdate.IsHeldForUpdate(c)) == true,
                () => "q2 was never held for the update", Poll.Patience);

            Assert.Equal(1, watch.Running.Running);
            Assert.Equal("working", _ledger.Session("s1")["state"]!.GetValue<string>());

            runs.End("q1");
            await Poll.Until(
                () => Snapshot(reports).Any(report => report.Concluded.Any(ended => ended.Quest == "q1")),
                () => "the ending was never reported", Poll.Patience);
            Assert.Equal(["q1"], runs.Started);
            Assert.Equal(0, watch.Running.Running);
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    [Fact]
    public async Task A_repository_the_person_held_keeps_the_person_s_sentence_while_the_update_drains()
    {
        _ledger.Publish("q1", "engine");
        var (watch, runs) = Watch(() => true, holds: """["engine"]""");
        var reports = new List<TickReport>();

        var watching = watch.RunAsync(
            (report, _) =>
            {
                lock (reports) reports.Add(report);
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => Count(reports) >= 1, () => "no look", Poll.Patience);

            var sitting = Last(reports)!.Considerations.Single(c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.Held, sitting.Verdict);
            Assert.Contains("held by the person", sitting.Reason);
            Assert.Empty(runs.Started);
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// DRAINHOLD1 (the driver follow-up): the loop's work beside the looks that removes trees or opens turns waits out the drain too. The
    /// tidy and keeping pass stays due and begins no pass, and the wake of the conversations waiting for an account is not asked; after
    /// *Not now* the tidy begins and the wake is asked.
    /// </summary>
    [Fact]
    public async Task While_an_update_drains_the_tidy_and_the_limits_wake_begin_nothing_and_after_not_now_they_do()
    {
        var draining = true;
        var tidy = new BranchTidying(_home) { Pace = TimeSpan.FromMinutes(5) };
        var woken = 0;
        var (watch, _) = Watch(() => draining, tidying: tidy, limits: (_, _) =>
        {
            Interlocked.Increment(ref woken);
            return Task.CompletedTask;
        });
        var reports = new List<TickReport>();

        var watching = watch.RunAsync(
            (report, _) =>
            {
                lock (reports) reports.Add(report);
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => Count(reports) >= 3, () => $"{Count(reports)} look(s)", Poll.Patience);

            Assert.True(tidy.Due);
            Assert.Equal(0, Volatile.Read(ref woken));

            draining = false;
            watch.Nudge();
            await Poll.Until(() => !tidy.Due && Volatile.Read(ref woken) > 0, () => $"due {tidy.Due}, woken {woken}", Poll.Patience);
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>DRAINHOLD1: the tidy of a ref move is refused with no try while an update drains, stays pending, and is tried after it.</summary>
    [Fact]
    public async Task A_tidy_of_a_ref_move_is_refused_while_an_update_drains_and_tried_after_it()
    {
        var draining = true;
        var inner = new CountingEffect();
        var held = new DrainHeldEffect(inner, () => draining);

        Assert.Equal(ChainOutcome.Refused, (await held.TryAsync("engine", CancellationToken.None)).Outcome);
        Assert.Equal(ChainOutcome.Refused, (await held.TryAsync("engine", due: true, CancellationToken.None)).Outcome);
        Assert.Equal(0, inner.Tried);
        Assert.Equal("engine", Assert.Single(await held.PendingAsync(CancellationToken.None)).Unit);

        draining = false;
        Assert.Equal(ChainOutcome.Done, (await held.TryAsync("engine", CancellationToken.None)).Outcome);
        Assert.Equal(1, inner.Tried);
    }

    private sealed class CountingEffect : IChainEffect
    {
        public int Tried { get; private set; }

        public Task<IReadOnlyList<ChainPending>> PendingAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ChainPending>>([new ChainPending("engine")]);

        public Task<ChainTry> TryAsync(string unit, CancellationToken ct)
        {
            Tried++;
            return Task.FromResult(ChainTry.Done());
        }
    }

    private static int Count(List<TickReport> reports)
    {
        lock (reports) return reports.Count;
    }

    private static TickReport? Last(List<TickReport> reports)
    {
        lock (reports) return reports.Count == 0 ? null : reports[^1];
    }

    private static List<TickReport> Snapshot(List<TickReport> reports)
    {
        lock (reports) return [.. reports];
    }

    private static async Task Settled(Task running)
    {
        try { await running.WaitAsync(Poll.Patience); }
        catch (OperationCanceledException) { }
    }

    private (DriverWatch Watch, StandInRuns Runs) Watch(
        Func<bool> draining, string holds = "[]", BranchTidying? tidying = null, Func<DriverConfig, CancellationToken, Task>? limits = null)
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, $$"""
            { "drivable": ["engine", "tools"], "holds": {{holds}}, "adapter": "stub", "cap": 3, "pollSeconds": 1, "trees": ["engine", "tools"] }
            """);
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var watch = new DriverWatch(
            service, config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = runs.Runner,
            Draining = draining,
            Tidying = tidying,
            Limits = limits,
        };
        runs.Keeping = watch.Running;
        return (watch, runs);
    }
}
