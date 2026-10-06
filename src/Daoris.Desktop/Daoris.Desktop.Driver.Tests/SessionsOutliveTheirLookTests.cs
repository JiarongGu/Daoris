using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// DEV3 (D115 §3.1): a session outlives the look that started it. A look starts what may start and returns;
/// the sessions still running are the driver's to keep, each ending joins the next look's report, and the
/// modes a gate drives wait until nothing runs.
/// </summary>
/// <remarks>
/// Driven over the real client and the real planner, with the service's doors and each start's run standing
/// in, in-process: what is held here is the scheduling, which the suite's fast half has to hold. The same
/// behaviour over a real stub harness is <c>SessionsOutliveTheirLookProcessTests</c>, in the <c>Process</c>
/// half.
/// </remarks>
public sealed class SessionsOutliveTheirLookTests : IDisposable
{
    /// <summary>How long anything here may take. A look that waits for its sessions waits forever here.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-outlive-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    // Every look and watch here rides this, so a test that fails never leaves a stand-in session waiting.
    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(60));

    public SessionsOutliveTheirLookTests()
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

    /// <summary>
    /// A look starts its session and returns while that session still works: it no longer holds the loop
    /// for the session's whole run.
    /// </summary>
    [Fact]
    public async Task A_look_returns_while_the_session_it_started_still_runs()
    {
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config());

        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.True(look.Progressed);
        Assert.Equal(["q1"], runs.Started);
        Assert.Equal("working", State("s1"));
        Assert.Empty(look.Concluded);
    }

    /// <summary>
    /// 🔴 The row's own case: a quest published while a long session runs starts at the next look, beside it,
    /// rather than after it. Where the repository opens a tree per session, the two run side by side (PAR1).
    /// </summary>
    [Fact]
    public async Task A_quest_published_during_a_long_session_starts_at_the_next_look()
    {
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config());
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        _ledger.Publish("q2", "engine");
        var next = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal(StartVerdict.Start, next.Considerations.Single(c => c.Quest.Id == "q2").Verdict);
        Assert.Equal(["q1", "q2"], runs.Started);
        Assert.Equal("working", State("s1"));
        Assert.Equal("working", State("s2"));
    }

    /// <summary>
    /// The watch looks again on its own pace while a session runs, so a quest published meanwhile starts
    /// without waiting for the first to end — the loop both hosts run.
    /// </summary>
    [Fact]
    public async Task The_watch_looks_again_while_a_session_runs_and_starts_what_was_published_meanwhile()
    {
        _ledger.Publish("q1", "engine");
        var (watch, runs) = Watch();
        var published = 0;

        var watching = watch.RunAsync(
            (_, _) =>
            {
                if (Interlocked.Exchange(ref published, 1) == 0) _ledger.Publish("q2", "engine");
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => runs.Started.Count == 2, () => $"started: {string.Join(", ", runs.Started)}", Bound);

            Assert.Equal(["q1", "q2"], runs.Started);
            Assert.Equal("working", State("s1"));
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// The quest a session serves stays its while it works, however long it stays open: where each session
    /// opens its own tree, nothing else would stop a second one starting on it at the next look.
    /// </summary>
    [Fact]
    public async Task A_quest_its_session_has_not_yet_taken_is_not_started_again_at_the_next_look()
    {
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config());
        runs.Takes = false;
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        var next = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        var sitting = next.Considerations.Single(c => c.Quest.Id == "q1");
        Assert.Equal(StartVerdict.RepositoryBusy, sitting.Verdict);
        Assert.Contains("`s1`", sitting.Reason);
        Assert.Single(_ledger.SessionsFor("q1"));
    }

    /// <summary>
    /// An ending is said once, in the report of the look after it — the line a person reads and the fact a
    /// watcher acts on (SURF5b) — and never again.
    /// </summary>
    [Fact]
    public async Task An_ending_joins_the_next_looks_report_and_no_other()
    {
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config());
        var first = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        runs.End("q1");
        await driver.Running.EndedAsync(_closing.Token).WaitAsync(Bound);
        var next = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        var after = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Empty(first.Concluded);
        Assert.Contains(next.Events, line => line.StartsWith("completed  session s1 (#q1 → engine)", StringComparison.Ordinal));
        var ended = Assert.Single(next.Concluded);
        Assert.Equal(("s1", "completed", "q1"), (ended.Session, ended.State, ended.Quest));
        Assert.Empty(after.Concluded);
        Assert.DoesNotContain(after.Events, line => line.Contains("session s1", StringComparison.Ordinal));
        Assert.True(driver.Running.Idle);
    }

    /// <summary>
    /// Where a repository's sessions run in its root, the root is its one tree (D51): a quest published while
    /// one works there waits for it, says whose session holds it, and starts at the look after it ends.
    /// </summary>
    [Fact]
    public async Task Without_trees_a_quest_waits_for_the_session_in_its_root_and_starts_the_look_after()
    {
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config(trees: false));
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        _ledger.Publish("q2", "engine");
        var busy = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        runs.End("q1");
        await driver.Running.EndedAsync(_closing.Token).WaitAsync(Bound);
        var freed = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        var waited = busy.Considerations.Single(c => c.Quest.Id == "q2");
        Assert.Equal(StartVerdict.RepositoryBusy, waited.Verdict);
        Assert.Contains("`s1`", waited.Reason);
        Assert.Equal(StartVerdict.Start, freed.Considerations.Single(c => c.Quest.Id == "q2").Verdict);
        Assert.Equal(["q1", "q2"], runs.Started);
    }

    /// <summary>The machine's cap counts the sessions still running from earlier looks, across repositories.</summary>
    [Fact]
    public async Task The_machine_cap_counts_the_sessions_still_running_from_earlier_looks()
    {
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config(cap: 1));
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        _ledger.Publish("q2", "tools");
        var full = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        runs.End("q1");
        await driver.Running.EndedAsync(_closing.Token).WaitAsync(Bound);
        var freed = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal(StartVerdict.AtCapacity, full.Considerations.Single(c => c.Quest.Id == "q2").Verdict);
        Assert.Equal(StartVerdict.Start, freed.Considerations.Single(c => c.Quest.Id == "q2").Verdict);
    }

    /// <summary>
    /// The gates' mode looks until nothing runs and a look starts nothing: it starts a quest published while
    /// the first session works, beside it, and returns only once both have ended and been reported.
    /// </summary>
    [Fact]
    public async Task Until_idle_looks_until_nothing_runs_and_a_look_starts_nothing()
    {
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config());
        string? firstWhenSecondOpened = null;
        runs.OnOpen = quest =>
        {
            if (quest == "q1")
            {
                _ledger.Publish("q2", "engine");
                return;
            }

            firstWhenSecondOpened = State("s1");
            runs.End("q1");
            _ = Task.Delay(300).ContinueWith(_ => runs.End("q2"), TaskScheduler.Default);
        };

        var reports = await driver.RunUntilIdleAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal("working", firstWhenSecondOpened);
        Assert.Equal(["s1", "s2"], reports.SelectMany(r => r.Concluded).Select(e => e.Session).Order());
        Assert.Equal(["completed", "completed"], _ledger.Sessions.Select(s => s["state"]!.GetValue<string>()));
        Assert.False(reports[^1].Progressed);
        Assert.True(driver.Running.Idle);
    }

    /// <summary>
    /// A single run — the headless host's <c>--once</c> — still reads one start whole: its look's plan, and the
    /// ending of what that look started, in one report.
    /// </summary>
    [Fact]
    public async Task Run_once_waits_for_what_its_look_started_and_joins_its_ending_to_the_report()
    {
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config());
        runs.OnOpen = quest => _ = Task.Delay(300).ContinueWith(_ => runs.End(quest), TaskScheduler.Default);

        var report = await driver.RunOnceAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal(StartVerdict.Start, Assert.Single(report.Considerations).Verdict);
        Assert.True(report.Progressed);
        Assert.Contains(report.Events, line => line.StartsWith("completed  session s1", StringComparison.Ordinal));
        Assert.Equal("s1", Assert.Single(report.Concluded).Session);
        Assert.Equal("completed", State("s1"));
    }

    /// <summary>
    /// The sync still runs beside the sessions (D68 §6), and after it the lost-claim stop asks where this
    /// machine's claim on each running session's quest stands (D68 §5) — now at every look while one works,
    /// and never once it has ended.
    /// </summary>
    [Fact]
    public async Task Every_look_asks_the_claim_of_each_session_still_running_and_none_after_it_ends()
    {
        using var sync = new RemoteSyncSet([]);
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config(), sync);
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        var whileRunning = _ledger.ClaimsAsked("q1");
        runs.End("q1");
        await driver.Running.EndedAsync(_closing.Token).WaitAsync(Bound);
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal(2, whileRunning);
        Assert.Equal(2, _ledger.ClaimsAsked("q1"));
    }

    /// <summary>
    /// 🔴 D104's shutdown, for sessions started at two looks: closing the watch ends both on its own token, and
    /// it lets go only once each has written how it ended — stopped, and interrupted, so a take either held is
    /// carried on at the next start.
    /// </summary>
    [Fact]
    public async Task Closing_the_watch_lets_go_only_once_every_running_session_is_recorded()
    {
        _ledger.Publish("q1", "engine");
        var (watch, runs) = Watch();
        var published = 0;
        var watching = watch.RunAsync(
            (_, _) =>
            {
                if (Interlocked.Exchange(ref published, 1) == 0) _ledger.Publish("q2", "engine");
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        await Poll.Until(() => runs.Started.Count == 2, () => $"started: {string.Join(", ", runs.Started)}", Bound);

        await _closing.CancelAsync();
        await Settled(watching);

        Assert.All(_ledger.Sessions, record =>
        {
            Assert.Equal("stopped", record["state"]!.GetValue<string>());
            Assert.True(record["interrupted"]?.GetValue<bool>(), record.ToJsonString());
        });
        Assert.Equal(2, _ledger.Sessions.Count);
        Assert.Equal(0, watch.Running.Running);
    }

    /// <summary>
    /// A session ending wakes the watch: the ending is said, and its slot used, at once rather than at the end
    /// of the pace — which is fifteen seconds unless the person set it.
    /// </summary>
    [Fact]
    public async Task An_ending_wakes_the_watch_rather_than_waiting_out_its_pace()
    {
        _ledger.Publish("q1", "engine");
        var (watch, runs) = Watch(pollSeconds: 60);
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
            await Poll.Until(() => runs.Started.Count == 1, within: Bound);
            runs.End("q1");

            await Poll.Until(
                () => { lock (reports) return reports.Any(r => r.Concluded.Any(e => e.Session == "s1")); },
                () => $"{reports.Count} report(s), none with s1's ending", Bound);
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// A nudge that lands while a look runs — a person's control, pressed as the loop looked — is not lost for
    /// the rest of the pace: the watch looks again at once.
    /// </summary>
    [Fact]
    public async Task A_nudge_that_lands_during_a_look_is_not_lost()
    {
        _ledger.Publish("q1", "engine");
        var (watch, runs) = Watch(pollSeconds: 60);
        runs.OnOpen = _ => watch.Nudge();
        var looks = 0;
        var watching = watch.RunAsync(
            (_, _) =>
            {
                Interlocked.Increment(ref looks);
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => Volatile.Read(ref looks) >= 2, () => $"{looks} look(s)", Bound);
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// A look that fails leaves the endings it never reported waiting, and the watch must not wake on them at
    /// once, again and again, against a service that is not answering: it waits out its pace, as it always has.
    /// </summary>
    [Fact]
    public async Task A_failed_look_waits_out_the_pace_rather_than_waking_on_the_endings_it_left()
    {
        _ledger.Publish("q1", "engine");
        var (watch, runs) = Watch(pollSeconds: 60);
        var errors = 0;
        var watching = watch.RunAsync(
            (_, _) => Task.CompletedTask,
            error =>
            {
                Interlocked.Increment(ref errors);
                return Task.CompletedTask;
            },
            _closing.Token);
        try
        {
            await Poll.Until(() => runs.Started.Count == 1, within: Bound);
            _ledger.Down = true;
            runs.End("q1");
            await Poll.Until(() => Volatile.Read(ref errors) >= 1, within: Bound);
            await Task.Delay(1000);

            Assert.Equal(1, Volatile.Read(ref errors));
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    // ——— DEV3a: a run the headless host makes (`--once`, `--until-idle`) lets go only once what it caused has landed: each
    // session's ending written and said, and what a pass beside the sessions met said, rather than lost with the process.

    /// <summary>
    /// 🔴 The family rehearsal's lost-claim case, failed once under load: the pass beside a running session asks where this
    /// machine's claim stands, and the host stalls past the client's timeout. That question unanswered is no answer, as a host
    /// that is down is; the single run does not let go there, but once its session has ended, its ending written and in the
    /// report.
    /// </summary>
    [Fact]
    public async Task Run_once_lets_go_only_once_its_session_has_ended_when_a_claim_question_times_out_beside_it()
    {
        using var sync = new RemoteSyncSet([]);
        _ledger.Publish("q1", "engine");
        _ledger.ClaimHangs = true;
        var (driver, runs) = Driver(Config(), sync, timeout: TimeSpan.FromMilliseconds(200));
        // Ended only once a claim question beside it has met the client's timeout.
        runs.OnOpen = quest => _ = Task.Run(async () =>
        {
            await Poll.Until(() => _ledger.ClaimsAsked(quest) >= 1, within: Bound);
            await Task.Delay(600);
            runs.End(quest);
        });

        var report = await driver.RunOnceAsync(_closing.Token).WaitAsync(Bound);

        Assert.True(_ledger.ClaimsAsked("q1") >= 1);
        Assert.Equal("completed", State("s1"));
        Assert.Contains(report.Events, line => line.StartsWith("completed  session s1", StringComparison.Ordinal));
        Assert.Equal("s1", Assert.Single(report.Concluded).Session);
        Assert.True(driver.Running.Idle);
    }

    /// <summary>
    /// A pass beside the running session that fails for any reason (here the remotes map, unreadable while it is rewritten) is
    /// said, and the next pass tries again: the single run still waits for its session, and says its ending.
    /// </summary>
    [Fact]
    public async Task Run_once_says_a_failed_pass_beside_its_session_and_still_waits_for_its_ending()
    {
        // The set reads the map once as it is made and once at the look; every pass after finds it unreadable.
        var loads = 0;
        using var sync = RemoteSyncSet.Watching(StandInLedger.Url, null, () =>
            Interlocked.Increment(ref loads) <= 2
                ? new Dictionary<string, RemoteTarget>()
                : throw new IOException("the remotes map is being written"));
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config(), sync);
        runs.OnOpen = quest => _ = Task.Run(async () =>
        {
            await Poll.Until(() => Volatile.Read(ref loads) >= 3, within: Bound);
            runs.End(quest);
        });

        var report = await driver.RunOnceAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal("completed", State("s1"));
        Assert.Contains(report.Events, line => line.StartsWith("completed  session s1", StringComparison.Ordinal));
        Assert.Contains(
            report.Events,
            line => line == "sync  a pass beside the running sessions failed, and the next tries again: the remotes map is being written");
    }

    /// <summary>
    /// <c>--until-idle</c> says each look as it ends, not all of them once the run is over, and a look that fails lets go only
    /// once every session it started has ended, its record written and its ending said: the looks before it and the endings after
    /// it reach the person, where they used to go with the process.
    /// </summary>
    [Fact]
    public async Task Until_idle_says_each_look_as_it_ends_and_a_failed_look_says_the_endings_it_waited_for()
    {
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config());
        var said = new List<TickReport>();
        string? whenSaid = null;
        runs.OnOpen = quest =>
        {
            // The next look fails, as against a service that went away, while the session still works; it ends after.
            _ledger.Down = true;
            _ = Task.Delay(1500).ContinueWith(_ => runs.End(quest), TaskScheduler.Default);
        };

        var failed = await Assert.ThrowsAsync<DriverException>(() => driver.RunUntilIdleAsync(_closing.Token, said: report =>
        {
            lock (said) said.Add(report);
            if (report.Concluded.Count > 0) whenSaid = State("s1");
        }).WaitAsync(Bound));

        Assert.Contains("503", failed.Message);
        lock (said)
        {
            Assert.Equal(2, said.Count);
            Assert.Equal(StartVerdict.Start, Assert.Single(said[0].Considerations).Verdict);
            Assert.Contains(said[1].Events, line => line.StartsWith("completed  session s1", StringComparison.Ordinal));
            Assert.Equal("s1", Assert.Single(said[1].Concluded).Session);
        }

        Assert.Equal("completed", whenSaid);
        Assert.True(driver.Running.Idle);
    }

    /// <summary>A single run says its look as soon as it ends, then what ended, which together are the report it returns.</summary>
    [Fact]
    public async Task Run_once_says_its_look_then_what_ended_which_together_are_its_report()
    {
        _ledger.Publish("q1", "engine");
        var (driver, runs) = Driver(Config());
        var said = new List<TickReport>();
        string? whenLookSaid = null;
        runs.OnOpen = quest => _ = Task.Delay(300).ContinueWith(_ => runs.End(quest), TaskScheduler.Default);

        var report = await driver.RunOnceAsync(_closing.Token, said: each =>
        {
            lock (said)
            {
                if (said.Count == 0) whenLookSaid = State("s1");
                said.Add(each);
            }
        }).WaitAsync(Bound);

        Assert.Equal("working", whenLookSaid);
        Assert.Equal(2, said.Count);
        Assert.Equal(report.Considerations, said[0].Considerations);
        Assert.Equal(report.Events, said.SelectMany(each => each.Events));
        Assert.Equal(report.Concluded, said.SelectMany(each => each.Concluded));
    }

    // ——— DEV3b (D115's DEV3a note): a run whose own look meets the client's timeout fails, and the headless host says so and
    // exits 2; only the person's own close is a close, *driver: stopped.*, exit 0. The host hands what ended the run to
    // `DriverCommand.Cancelled` and prints what it answers, so these are the host's ending, held over the real throw.

    /// <summary>🔴 A look whose own request the service never answers ends the run as a failure that names the timeout.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_run_whose_own_look_times_out_ends_the_host_naming_the_timeout_exit_2(bool once)
    {
        _ledger.Publish("q1", "engine");
        _ledger.QuestsHang = true;
        var (driver, _) = Driver(Config(), timeout: TimeSpan.FromMilliseconds(200));
        using var closing = new CancellationTokenSource();

        var failed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            (once ? (Task)driver.RunOnceAsync(closing.Token) : driver.RunUntilIdleAsync(closing.Token)).WaitAsync(Bound));
        var ended = DriverCommand.Cancelled(failed, closing.IsCancellationRequested);

        Assert.Equal(2, ended.Exit);
        Assert.True(ended.Failed);
        Assert.StartsWith("driver: the service did not answer in time — ", ended.Said, StringComparison.Ordinal);
        Assert.Contains("Timeout", ended.Said, StringComparison.Ordinal);
    }

    /// <summary>The person's close while a look waits on the service is still the close: <i>driver: stopped.</i>, exit 0.</summary>
    [Fact]
    public async Task A_close_while_a_look_waits_on_the_service_is_still_the_close_exit_0()
    {
        _ledger.QuestsHang = true;
        var (driver, _) = Driver(Config());
        using var closing = new CancellationTokenSource();
        var run = driver.RunOnceAsync(closing.Token);
        await Poll.Until(() => _ledger.QuestsAsked >= 1, within: Bound);

        await closing.CancelAsync();
        var closed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Bound));

        Assert.Equal(new LoopEnded(0, "driver: stopped.", Failed: false), DriverCommand.Cancelled(closed, closing.IsCancellationRequested));
    }

    private string State(string session) => _ledger.Session(session)["state"]!.GetValue<string>();

    /// <summary>A look's or a watch's end, waited for within the bound, whatever it ended on.</summary>
    private static async Task Settled(Task running)
    {
        try { await running.WaitAsync(Bound); }
        catch (OperationCanceledException) { }
    }

    private static DriverConfig Config(bool trees = true, int cap = 3) => DriverConfig.Empty with
    {
        Drivable = ["engine", "tools"],
        Adapter = "stub",
        Cap = cap,
        PollSeconds = 1,
        Trees = trees ? ["engine"] : [],
    };

    /// <param name="timeout">The client's own timeout, where a test meets it; the client's default otherwise.</param>
    private (Daoris.Driver.Driver Driver, StandInRuns Runs) Driver(DriverConfig config, RemoteSyncSet? sync = null, TimeSpan? timeout = null)
    {
        var service = _ledger.Client(timeout);
        var runs = new StandInRuns(service, _ledger);
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            service, config, adapters, _home, sync: sync, harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")))
        {
            Runner = runs.Runner,
        };
        runs.Keeping = driver.Running;
        return (driver, runs);
    }

    private (DriverWatch Watch, StandInRuns Runs) Watch(int pollSeconds = 1)
    {
        var config = Path.Combine(_home, "driver.json");
        File.WriteAllText(config, $$"""
            { "drivable": ["engine", "tools"], "adapter": "stub", "cap": 3, "pollSeconds": {{pollSeconds}}, "trees": ["engine"] }
            """);
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var watch = new DriverWatch(
            service, config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")))
        {
            Runner = runs.Runner,
        };
        runs.Keeping = watch.Running;
        return (watch, runs);
    }
}
