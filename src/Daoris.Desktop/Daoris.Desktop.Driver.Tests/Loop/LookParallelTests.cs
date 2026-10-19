using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARALLEL1b (D181 point 3): the look's independent work leaves its serial path. The remote sync runs on its own task and clock, so
/// a slow remote holds no local start; a start is reserved (its quest, its tree, its place under the cap) and runs beside the look
/// past that, so a stalled start holds only itself, repeated wakes start it once, and the cap holds; the plugins are asked about
/// each planned start at once, each answer its own start's veto.
/// </summary>
/// <remarks>
/// Over the real client and planner with the service's doors and each start's run standing in, in-process, as the DEV3 tests drive
/// the watch. The watch runs on the pool, so a part of it that blocks never blocks the test.
/// </remarks>
public sealed class LookParallelTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(20);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-parallel-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public LookParallelTests()
    {
        Directory.CreateDirectory(_home);
        // The checkouts sit under a folder of their own: `tools` beside the home would read as its entry (HOMELAYOUT2's census).
        _ledger.Register("engine", Path.Combine(_home, "repos", "engine")).Register("tools", Path.Combine(_home, "repos", "tools"))
            .Register("docs", Path.Combine(_home, "repos", "docs"));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 A remote that does not answer holds the sync, never the local plan: a quest published meanwhile starts.</summary>
    [Fact]
    public async Task A_slow_remote_sync_holds_no_local_start()
    {
        using var remote = new ManualResetEventSlim(false);
        var passes = -1;
        // A pass reads the remotes map first, here a read that waits for the test, as a remote that does not answer holds a pass.
        // The set's own read as it is made goes through.
        using var sync = RemoteSyncSet.Watching("http://127.0.0.1:9", null, () =>
        {
            if (Interlocked.Increment(ref passes) > 0) remote.Wait(_closing.Token);
            return new Dictionary<string, RemoteTarget>();
        });
        var (watch, runs) = Watch(sync);

        var watching = Task.Run(() => watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token));
        try
        {
            await Poll.Until(() => Volatile.Read(ref passes) >= 1, () => "the whole look's sync never began", Bound);

            _ledger.Publish("q1", "engine");
            watch.Nudge(ChangeTopics.Ledger, "quests");
            await Poll.Until(() => runs.Started.Count == 1, () => "q1 waited for the remote", Bound);

            Assert.Equal(["q1"], runs.Started);
        }
        finally
        {
            remote.Set();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// 🔴 The row's own case: a start whose preparation stalls (git under a scan, an account's probe) holds its own quest, never the
    /// next: a quest published meanwhile starts at the next wake, while the first has still not opened its record.
    /// </summary>
    [Fact]
    public async Task A_stalled_start_does_not_hold_the_next()
    {
        _ledger.Publish("q1", "engine");
        var gate = new StartGate("q1");
        var (watch, runs) = Watch(gate: gate);
        var reports = new List<TickReport>();

        var watching = Task.Run(() => watch.RunAsync(Into(reports), onError: null, _closing.Token));
        try
        {
            await Poll.Until(() => gate.Asked("q1") == 1, () => "q1's start never began", Bound);

            _ledger.Publish("q2", "tools");
            watch.Nudge(ChangeTopics.Ledger, "quests");
            await Poll.Until(() => runs.Started.Contains("q2"), () => "q2 waited for q1's start", Bound);

            Assert.DoesNotContain("q1", runs.Started);
        }
        finally
        {
            gate.ReleaseAll();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// 🔴 A start reserved and still opening is started once, however many wakes come while it opens: each look between says its quest
    /// is starting already, and when it opens, it is the one session on its quest.
    /// </summary>
    [Fact]
    public async Task Repeated_wakes_start_a_stalled_start_once()
    {
        _ledger.Publish("q1", "engine");
        var gate = new StartGate("q1");
        var (watch, runs) = Watch(gate: gate);
        var reports = new List<TickReport>();

        var watching = Task.Run(() => watch.RunAsync(Into(reports), onError: null, _closing.Token));
        try
        {
            await Poll.Until(() => gate.Asked("q1") == 1, () => "q1's start never began", Bound);
            for (var wake = 0; wake < 5; wake++)
            {
                var seen = Count(reports);
                watch.Nudge(ChangeTopics.Ledger, "quests");
                await Poll.Until(() => Count(reports) > seen, () => $"wake {wake} never looked: q1's start held the look", Bound);
            }

            var sitting = Snapshot(reports)[^1].Considerations.Single(c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.RepositoryBusy, sitting.Verdict);
            Assert.Contains("starting", sitting.Reason);

            gate.Release("q1");
            await Poll.Until(() => runs.Started.Contains("q1"), () => "q1 never opened", Bound);
            var after = Count(reports);
            watch.Nudge(ChangeTopics.Ledger, "quests");
            await Poll.Until(() => Count(reports) > after, () => "no look after q1 opened", Bound);

            Assert.Equal(1, gate.Asked("q1"));
            Assert.Single(_ledger.SessionsFor("q1"));
        }
        finally
        {
            gate.ReleaseAll();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>🔴 Starts still opening take their places under the cap: with two of two reserved, a third quest waits for capacity.</summary>
    [Fact]
    public async Task The_cap_holds_while_starts_are_still_opening()
    {
        _ledger.Publish("q1", "engine");
        _ledger.Publish("q2", "tools");
        var gate = new StartGate("q1", "q2");
        var (watch, _) = Watch(gate: gate, cap: 2);
        var reports = new List<TickReport>();

        var watching = Task.Run(() => watch.RunAsync(Into(reports), onError: null, _closing.Token));
        try
        {
            await Poll.Until(() => gate.Asked("q1") == 1 && gate.Asked("q2") == 1, () => "the two starts never began", Bound);

            _ledger.Publish("q3", "docs");
            watch.Nudge(ChangeTopics.Ledger, "quests");
            await Poll.Until(
                () => Snapshot(reports).Any(report => report.Considerations.Any(c => c.Quest.Id == "q3")),
                () => "no look considered q3: the starts held the look", Bound);

            var third = Snapshot(reports).Last(report => report.Considerations.Any(c => c.Quest.Id == "q3"))
                .Considerations.Single(c => c.Quest.Id == "q3");
            Assert.Equal(StartVerdict.AtCapacity, third.Verdict);
            Assert.Equal(0, gate.Asked("q3"));
        }
        finally
        {
            gate.ReleaseAll();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// 🔴 Each planned start asks the plugins at once, and each answer is its own start's veto: one plugin slow to answer for one quest
    /// holds that quest's start alone.
    /// </summary>
    [Fact]
    public async Task A_slow_plugin_consideration_holds_only_its_own_start()
    {
        _ledger.Publish("q1", "engine");
        _ledger.Publish("q2", "tools");
        var slow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var considered = new System.Collections.Concurrent.ConcurrentQueue<string>();
        Plugin("acme.gate", ["quest/consider"]);
        await using var hooks = new HookSet(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(new Considering(async quest =>
        {
            considered.Enqueue(quest);
            if (quest == "q1") await slow.Task.WaitAsync(_closing.Token);
            return HookDecision.Allow;
        })));
        var (watch, runs) = Watch(hooks: hooks);

        var watching = Task.Run(() => watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token));
        try
        {
            await Poll.Until(() => runs.Started.Contains("q2"), () => $"q2 waited for q1's plugin; asked: {string.Join(", ", considered)}", Bound);

            Assert.DoesNotContain("q1", runs.Started);
            Assert.Contains("q1", considered);
        }
        finally
        {
            slow.TrySetResult();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// 🔴 A plugin slow to hear of an ending holds no later look: the plugins hear each ending as their topic's judge beside the look,
    /// so a quest published after it starts while the plugin still has not answered.
    /// </summary>
    [Fact]
    public async Task A_plugin_slow_to_hear_of_an_ending_holds_no_later_start()
    {
        _ledger.Publish("q1", "engine");
        var hearing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var heard = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Plugin("acme.ears", ["session/ended"]);
        await using var hooks = new HookSet(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(new Considering(
            _ => Task.FromResult(HookDecision.Allow),
            async () =>
            {
                heard.TrySetResult();
                await hearing.Task.WaitAsync(_closing.Token);
            },
            ["session/ended"])));
        var (watch, runs) = Watch(hooks: hooks);

        var watching = Task.Run(() => watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token));
        try
        {
            await Poll.Until(() => runs.Started.Contains("q1"), () => "q1 never started", Bound);
            runs.End("q1");
            await heard.Task.WaitAsync(Bound);

            _ledger.Publish("q2", "tools");
            watch.Nudge(ChangeTopics.Ledger, "quests");
            await Poll.Until(() => runs.Started.Contains("q2"), () => "q2 waited for the plugin to hear of q1's end", Bound);
        }
        finally
        {
            hearing.TrySetResult();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// 🔴 The lost-claim checks are asked at once, bounded: one host slow to answer for one session's quest no longer holds the next
    /// session's check until the client gives up on the first.
    /// </summary>
    [Fact]
    public async Task The_lost_claim_checks_are_asked_at_once()
    {
        _ledger.Publish("q1", "engine");
        _ledger.Publish("q2", "tools");
        using var sync = new RemoteSyncSet([]);
        var service = _ledger.Client(TimeSpan.FromSeconds(5));
        var runs = new StandInRuns(service, _ledger);
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with { Drivable = ["engine", "tools"], Adapter = "stub", Cap = 3, PollSeconds = 1, Trees = ["engine", "tools"] };
        var driver = new Daoris.Driver.Driver(
            service, config, adapters, _home, sync: sync, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = runs.Runner,
            Stops = runs.Stops,
        };
        runs.Keeping = driver.Running;
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        Assert.Equal(2, runs.Started.Count);

        // Each claim is answered only once the other has been asked too: one after another, the first asked waits out the client's
        // five seconds, whichever it is, and its session is not stopped.
        var asked = new Dictionary<string, TaskCompletionSource>
        {
            ["/api/quests/q1/claim"] = new(TaskCreationOptions.RunContinuationsAsynchronously),
            ["/api/quests/q2/claim"] = new(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        _ledger.Claim = "lost";
        _ledger.Before = async (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (!asked.TryGetValue(path, out var mine)) return;
            mine.TrySetResult();
            await Task.WhenAll(asked.Values.Select(each => each.Task)).WaitAsync(token);
        };

        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Contains(look.Events, line => line.StartsWith("stop  session s1 (#q1)", StringComparison.Ordinal));
        Assert.Contains(look.Events, line => line.StartsWith("stop  session s2 (#q2)", StringComparison.Ordinal));
        await driver.Running.SettledAsync().WaitAsync(Bound);
    }

    /// <summary>🔴 A snapshot's reads are asked at once: one door slow to answer holds its own read, not the reads after it.</summary>
    [Fact]
    public async Task A_snapshot_s_reads_are_asked_at_once()
    {
        _ledger.Publish("q1", "engine");
        var registryAsked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ledger.Before = async (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/registry") registryAsked.TrySetResult();
            if (path == "/api/quests") await registryAsked.Task.WaitAsync(token);
        };
        using var service = _ledger.Client(TimeSpan.FromSeconds(5));

        var snapshot = await service.SnapshotAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal("q1", Assert.Single(snapshot.Quests).Id);
        Assert.Equal(3, snapshot.Repositories.Count);
    }

    private (DriverWatch Watch, StandInRuns Runs) Watch(
        RemoteSyncSet? sync = null, int cap = 3, int pollSeconds = 60, StartGate? gate = null, HookSet? hooks = null)
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, $$"""
            { "drivable": ["engine", "tools", "docs"], "adapter": "stub", "cap": {{cap}}, "pollSeconds": {{pollSeconds}}, "trees": ["engine", "tools", "docs"] }
            """);
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var watch = new DriverWatch(
            service, config, _home, new SessionProcesses(), sync,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)), hooks: hooks)
        {
            Runner = gate?.Over(runs) ?? runs.Runner,
            Stops = runs.Stops,
        };
        runs.Keeping = watch.Running;
        return (watch, runs);
    }

    /// <summary>A plugin's manifest in the home, its process a test's channel.</summary>
    private void Plugin(string id, string[] points)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            id,
            version = "1.0.0",
            hooks = new { command = new[] { "node", "hooks.mjs" }, points },
        }));
    }

    /// <summary>Each start's run, held before its preparation for the quests named until the test lets it go, and counted.</summary>
    private sealed class StartGate
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource> _held = new(StringComparer.Ordinal);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _asked = new(StringComparer.Ordinal);

        public StartGate(params string[] held)
        {
            foreach (var quest in held) _held[quest] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public int Asked(string quest) => _asked.GetValueOrDefault(quest);

        public void Release(string quest) => _held[quest].TrySetResult();

        public void ReleaseAll()
        {
            foreach (var held in _held.Values) held.TrySetResult();
        }

        public Func<Consideration, Action, CancellationToken, Task<StartRun>> Over(StandInRuns runs) => async (start, opened, token) =>
        {
            _asked.AddOrUpdate(start.Quest.Id, 1, (_, count) => count + 1);
            if (_held.TryGetValue(start.Quest.Id, out var held)) await held.Task.WaitAsync(token);
            return await runs.Runner(start, opened, token);
        };
    }

    /// <summary>A plugin's channel that answers each consideration as the test says, by its quest, and hears each ending as it says.</summary>
    private sealed class Considering(
        Func<string, Task<HookDecision>> consider, Func<Task>? ended = null, IReadOnlyList<string>? points = null) : IHookChannel
    {
        public IReadOnlyList<string> Points => points ?? ["quest/consider"];

        public bool Alive => true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) =>
            consider(System.Text.Json.JsonSerializer.SerializeToElement(payload).GetProperty("quest").GetProperty("id").GetString()!);

        public Task EndedAsync(object payload, CancellationToken ct) => ended?.Invoke() ?? Task.CompletedTask;

        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) =>
            Task.FromResult(new PluginLanding("acme", true, null, "pushed"));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

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
        catch (TimeoutException) { }
    }
}
