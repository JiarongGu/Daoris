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
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-drain-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(60));

    public UpdateDrainTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine")).Register("tools", Path.Combine(_home, "tools"));
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
            await Poll.Until(() => Count(reports) >= 2, () => $"{Count(reports)} look(s)", Bound);

            Assert.Empty(runs.Started);
            var sitting = Last(reports)!.Considerations.Single(c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
            Assert.Equal(InstallUpdate.HoldReason, sitting.Reason);
            Assert.True(InstallUpdate.IsHeldForUpdate(sitting));

            // *Not now*: the next look starts it.
            draining = false;
            watch.Nudge();
            await Poll.Until(() => runs.Started.Count == 1, () => $"started: {string.Join(", ", runs.Started)}", Bound);
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
            await Poll.Until(() => runs.Started.Count == 1, () => "q1 never started", Bound);
            draining = true;
            _ledger.Publish("q2", "tools");
            watch.Nudge();
            await Poll.Until(
                () => Last(reports)?.Considerations.Any(c => c.Quest.Id == "q2" && InstallUpdate.IsHeldForUpdate(c)) == true,
                () => "q2 was never held for the update", Bound);

            Assert.Equal(1, watch.Running.Running);
            Assert.Equal("working", _ledger.Session("s1")["state"]!.GetValue<string>());

            runs.End("q1");
            await Poll.Until(
                () => Snapshot(reports).Any(report => report.Concluded.Any(ended => ended.Quest == "q1")),
                () => "the ending was never reported", Bound);
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
            await Poll.Until(() => Count(reports) >= 1, () => "no look", Bound);

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
        try { await running.WaitAsync(Bound); }
        catch (OperationCanceledException) { }
    }

    private (DriverWatch Watch, StandInRuns Runs) Watch(Func<bool> draining, string holds = "[]")
    {
        var config = Path.Combine(_home, "driver.json");
        File.WriteAllText(config, $$"""
            { "drivable": ["engine", "tools"], "holds": {{holds}}, "adapter": "stub", "cap": 3, "pollSeconds": 1, "trees": ["engine", "tools"] }
            """);
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var watch = new DriverWatch(
            service, config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")))
        {
            Runner = runs.Runner,
            Draining = draining,
        };
        runs.Keeping = watch.Running;
        return (watch, runs);
    }
}
