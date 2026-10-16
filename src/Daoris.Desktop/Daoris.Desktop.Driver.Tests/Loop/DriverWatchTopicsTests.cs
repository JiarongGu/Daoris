using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The look's topics (FABRIC-EVENT3a, the fabric events design §A3, §C5, §C6 link 5): a wake runs the local plan alone, and the
/// remote sync keeps its own clock, the <c>look</c> topic's deadline (<c>pollSeconds</c>), where the whole look runs as it did.
/// Canary 4: more wakes are not more whole looks, so a thousand wakes run no remote sync.
/// </summary>
/// <remarks>
/// Over the real client and planner with the service's doors and each start's run standing in, in-process, as the DEV3 tests
/// drive the watch. The remote sync is a set over an empty map that counts its passes, since each pass reads the map first.
/// </remarks>
public sealed class DriverWatchTopicsTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-topics-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(60));

    // The remote sync's passes: each reads the remotes map once, as does the set's making.
    private int _passes;

    public DriverWatchTopicsTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Canary 4: a thousand wakes, the ledger's and a terminal's words, run the local plan and no remote sync; a plain nudge still means everything.</summary>
    [Fact]
    public async Task A_thousand_wakes_run_no_remote_sync()
    {
        using var sync = Counted();
        var (watch, runs) = Watch(sync, pollSeconds: 60);
        var reports = new List<TickReport>();

        var watching = watch.RunAsync(Into(reports), onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => Count(reports) >= 1, () => "no look", Bound);
            var synced = Passes();

            for (var wake = 0; wake < 1000; wake++)
            {
                if (wake % 2 == 0) watch.Nudge(ChangeTopics.Ledger, "quests");
                else watch.Said($"s{wake}");
            }

            // The last wake carries a quest an agent published: the look after it plans it, so every wake before it was looked at.
            _ledger.Publish("q1", "engine");
            watch.Nudge(ChangeTopics.Ledger, "quests");
            await Poll.Until(() => runs.Started.Count == 1, () => "q1 never started", Bound);

            Assert.True(Count(reports) >= 2, $"{Count(reports)} look(s)");
            Assert.Equal(synced, Passes());

            // A plain nudge carries no topic, and means everything, the remote sync with it, as it always did.
            watch.Nudge();
            await Poll.Until(() => Passes() == synced + 1, () => $"{Passes() - synced} pass(es) after a plain nudge", Bound);
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// A <c>ledger</c> wake takes a quest an agent published at once, without the remote sync, and its <c>watch.looked</c> line says
    /// the look ran the plan alone; the loop's start ran the whole look.
    /// </summary>
    [Fact]
    public async Task A_ledger_wake_takes_a_quest_an_agent_published_without_the_remote_sync()
    {
        using (var log = new MachineLog(_home, "driver"))
        using (var sync = Counted())
        {
            var (watch, runs) = Watch(sync, pollSeconds: 60, log);
            var reports = new List<TickReport>();
            var watching = watch.RunAsync(Into(reports), onError: null, _closing.Token);
            try
            {
                await Poll.Until(() => Count(reports) >= 1, () => "no look", Bound);
                var synced = Passes();

                _ledger.Publish("q1", "engine", from: "agent");
                watch.Nudge(ChangeTopics.Ledger, "quests");
                await Poll.Until(() => runs.Started.Count == 1, () => "q1 never started", Bound);
                await Poll.Until(() => Count(reports) >= 2, () => "the wake's look never reported", Bound);

                Assert.Equal(["q1"], runs.Started);
                Assert.Equal(synced, Passes());
            }
            finally
            {
                await _closing.CancelAsync();
                await Settled(watching);
            }
        }

        var looks = Looked();
        Assert.Equal(("start", "look"), (looks[0].GetProperty("by").GetString(), looks[0].GetProperty("part").GetString()));
        var woken = looks.First(data => data.GetProperty("topic").GetString() == ChangeTopics.Ledger);
        Assert.Equal(("nudge", "quests", "plan"),
            (woken.GetProperty("by").GetString(), woken.GetProperty("subject").GetString(), woken.GetProperty("part").GetString()));
    }

    /// <summary>
    /// FABRIC-EVENT3b: the loop holds one wait on the service's change stream, so a quest an agent published starts at once, with
    /// no nudge, long before <c>pollSeconds</c>, and without the remote sync.
    /// </summary>
    [Fact]
    public async Task An_agent_s_quest_starts_without_the_poll()
    {
        _ledger.Changes = StandInLedger.ChangeDoor.Answers;
        using var sync = Counted();
        var (watch, runs) = Watch(sync, pollSeconds: 60);
        var reports = new List<TickReport>();

        var watching = watch.RunAsync(Into(reports), onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => Count(reports) >= 1 && _ledger.ChangesHeld == 1, () => "no look, or no wait held", Bound);
            var synced = Passes();

            _ledger.Publish("q1", "engine", from: "agent");
            await Poll.Until(() => runs.Started.Count == 1, () => "q1 never started", Bound);

            Assert.Equal(["q1"], runs.Started);
            Assert.Equal(synced, Passes());
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }

        // One wait held at a time: the bootstrap, the one the publish answered, and the next.
        Assert.InRange(_ledger.ChangesAsked, 2, 4);
    }

    /// <summary>
    /// The <c>look</c> topic's deadline holds under a stream of wakes: <c>pollSeconds</c> after the last whole look the whole look
    /// runs, the remote sync first, whatever woke the looks between, and the wakes between sync nothing.
    /// </summary>
    [Fact]
    public async Task The_whole_look_still_runs_at_its_deadline_under_a_stream_of_wakes()
    {
        using var sync = Counted();
        var (watch, _) = Watch(sync, pollSeconds: 1);
        var reports = new List<TickReport>();

        var watching = watch.RunAsync(Into(reports), onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => Count(reports) >= 1, () => "no look", Bound);
            var synced = Passes();

            var wakes = 0;
            var until = DateTimeOffset.UtcNow.AddSeconds(4);
            while (DateTimeOffset.UtcNow < until)
            {
                watch.Nudge(ChangeTopics.Ledger, "quests");
                wakes++;
                await Task.Delay(100);
            }

            var deadlines = Passes() - synced;
            Assert.True(Count(reports) > 10, $"{Count(reports)} look(s) for {wakes} wakes");
            Assert.InRange(deadlines, 2, 8);
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// A session's end is a ledger fact: the look it wakes runs the local plan, and syncs once, after the ending it reports (the sync
    /// design §8), never at its head as well.
    /// </summary>
    [Fact]
    public async Task A_session_s_end_runs_the_plan_and_syncs_once_after_the_ending()
    {
        using var sync = Counted();
        _ledger.Publish("q1", "engine");
        var (watch, runs) = Watch(sync, pollSeconds: 60);
        var reports = new List<TickReport>();

        var watching = watch.RunAsync(Into(reports), onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => runs.Started.Count == 1, () => "q1 never started", Bound);
            await Poll.Until(() => Count(reports) >= 1, () => "no look", Bound);
            var synced = Passes();

            runs.End("q1");
            await Poll.Until(
                () => Snapshot(reports).Any(report => report.Concluded.Any(ended => ended.Quest == "q1")),
                () => "the ending was never reported", Bound);

            Assert.Equal(synced + 1, Passes());
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    private RemoteSyncSet Counted() =>
        RemoteSyncSet.Watching("http://127.0.0.1:9", null, () =>
        {
            Interlocked.Increment(ref _passes);
            return new Dictionary<string, RemoteTarget>();
        });

    private int Passes() => Volatile.Read(ref _passes);

    private static Func<TickReport, DriverConfig, Task> Into(List<TickReport> reports) => (report, _) =>
    {
        lock (reports) reports.Add(report);
        return Task.CompletedTask;
    };

    private static int Count(List<TickReport> reports)
    {
        lock (reports) return reports.Count;
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

    private JsonElement[] Looked() =>
        Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl")
            .SelectMany(File.ReadAllLines)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(line => line.GetProperty("event").GetString() == "watch.looked")
            .Select(line => line.GetProperty("data"))
            .ToArray();

    private (DriverWatch Watch, StandInRuns Runs) Watch(RemoteSyncSet sync, int pollSeconds, MachineLog? log = null)
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, $$"""
            { "drivable": ["engine"], "adapter": "stub", "cap": 3, "pollSeconds": {{pollSeconds}}, "trees": ["engine"] }
            """);
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var watch = new DriverWatch(
            service, config, _home, new SessionProcesses(), sync,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = runs.Runner,
            Stops = runs.Stops,
            Log = log,
        };
        runs.Keeping = watch.Running;
        return (watch, runs);
    }
}
