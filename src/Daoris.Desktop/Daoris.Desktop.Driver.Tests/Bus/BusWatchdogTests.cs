using System.Globalization;
using Daoris.Driver;
using Daoris.Testing;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The bus's watchdog in shadow: an obliged entry arms a durable timer in its own write and its end clears it; past due a stall
/// is appended for each retry and then the give-up it would apply, handing the step to its holder chain; a kill and a restart keep every
/// due time and make nothing twice; the Stalls view and its comparer read it all back. Homes under <c>_fixtures/</c>.
/// </summary>
public sealed class BusWatchdogTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _home = Fixture();

    private static string Fixture()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "bus-watchdog", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        return home;
    }

    public void Dispose() => GitSeed.Discard(_home);

    private BusLog Open(TimeProvider clock, IBusLogStore? store = null) => BusLog.Over(store ?? new DesktopBusLogStore(_home), new BusLogOptions { Clock = clock });

    private IReadOnlyList<BusTimer> Armed()
    {
        using var store = new DesktopBusLogStore(_home);
        return store.Timers(null, 1000);
    }

    private IReadOnlyList<BusEnvelope> Logged()
    {
        using var store = new DesktopBusLogStore(_home);
        return store.Read(0, 100_000);
    }

    private static BusDraft Line(LandingLine line, Dictionary<string, string> passes) => OpinionBusFacts.OfLanding(line, passes)!;

    private static LandingLine Asked(string opinion, string pass = OpinionViews.FirstPass) => OpinionLines.Asked(opinion, "s-1", "done", "Codex", "codex", pass);

    private static LandingLine Given(string opinion, int must) =>
        new("opinion.given", [("opinion", opinion), ("must", must), ("should", 0), ("note", 0), ("seconds", 180L), ("tier", "agent")]);

    private static LandingLine Answered(string opinion) => OpinionLines.Answered(opinion, new OpinionReading("s-1", null, DateTimeOffset.UnixEpoch, []));

    private static VerifySend Send(string run, string invocation, string commit) => new(run, invocation, "engine", "c:/x", commit, "none");

    private static VerifyResult Passed(VerifySend send) => new(send.Invocation, send.Commit, VerifyVerdicts.Reached, null, []);

    /// <summary>
    /// 🔴 An obliged entry arms its timer in the write that keeps it, due at its type's deadline (the registry's, stamped on the envelope),
    /// and the fact that ends it clears it: a start by its own attempt's verdict, a red by the run's next start, a pass asked by its
    /// opinion's given, a given by its delivery, a delivery by its answers. The watchdog's own facts arm nothing.
    /// </summary>
    [Fact]
    public async Task An_obliged_entry_arms_a_durable_timer_and_its_end_clears_it()
    {
        var clock = new MovedClock();
        var passes = new Dictionary<string, string>(StringComparer.Ordinal);
        var start = Send("run-1", "inv-1", "aaaa1111");
        await using (var bus = Open(clock))
        {
            Assert.True(bus.Append(CheckBusFacts.CheckStarted(start)));
            await bus.FlushAsync();
            var started = Assert.Single(Armed());
            Assert.Equal((BusTypes.CheckStarted.Kind, "run-1", "inv-1|aaaa1111", BusTypes.Checks), (started.Kind, started.Subject, started.Effect, started.Obliged));
            Assert.Equal(clock.GetUtcNow() + BusTypes.CheckStarted.Deadline.Within, started.Due);
            Assert.Equal(started.Due, Logged().Single().Deadline);

            // A start of another attempt on the same run does not clear this one: a start ends by its own attempt's effect.
            Assert.True(bus.Append(CheckBusFacts.CheckSettled(Send("run-1", "inv-0", "aaaa1111"), Passed(Send("run-1", "inv-0", "aaaa1111")))));
            await bus.FlushAsync();
            Assert.Contains(Armed(), timer => timer.Entry == started.Entry);

            Assert.True(bus.Append(CheckBusFacts.CheckSettled(start, new VerifyResult("inv-1", "aaaa1111", VerifyVerdicts.Failed, "red", []) { Failed = "unit" })));
            await bus.FlushAsync();
            Assert.DoesNotContain(Armed(), timer => timer.Entry == started.Entry);
            Assert.Contains(Armed(), timer => timer.Kind == BusTypes.CheckFailed.Kind);

            // The step sent the red back: the run's next attempt clears the red.
            Assert.True(bus.Append(CheckBusFacts.CheckStarted(Send("run-1", "inv-2", "bbbb2222"))));
            Assert.True(bus.Append(Line(Asked("op-1"), passes)));
            await bus.FlushAsync();
            Assert.DoesNotContain(Armed(), timer => timer.Kind == BusTypes.CheckFailed.Kind);
            var asked = Assert.Single(Armed(), timer => timer.Kind == BusTypes.OpinionAsked.Kind);
            Assert.Equal("codex", asked.Obliged);

            Assert.True(bus.Append(Line(Given("op-1", 1), passes)));
            await bus.FlushAsync();
            Assert.DoesNotContain(Armed(), timer => timer.Kind == BusTypes.OpinionAsked.Kind);
            Assert.True(bus.Append(OpinionBusFacts.OpinionDelivered("op-1", "s-1", 1, "claims")));
            await bus.FlushAsync();
            Assert.DoesNotContain(Armed(), timer => timer.Kind == BusTypes.OpinionGiven.Kind);
            Assert.True(bus.Append(Line(Answered("op-1"), passes)));
            await bus.FlushAsync();
        }

        Assert.Equal(
            [BusTypes.CheckPassed.Kind, BusTypes.CheckStarted.Kind, BusTypes.OpinionAnswered.Kind],
            Armed().Select(timer => timer.Kind).Order(StringComparer.Ordinal));
        Assert.All(BusTypes.All.Where(type => !BusTypes.Obliges(type)), type => Assert.Empty(BusTimers.Of(
            new BusEnvelope("e", 1, type.Kind, "s", type.Emitter, null, null, null, [], [], DateTimeOffset.UnixEpoch), 1).OfType<BusTimerOp.Arm>()));
    }

    /// <summary>
    /// 🔴 Past due the watchdog appends a stall for each retry, counted down, the look's reading beside it, and past the last the give-up it
    /// would apply, applying nothing: a request it would refuse, answered to its requester, the step left a wait the vacancy bounds and the
    /// owner's exit ends. The Stalls view lists it.
    /// </summary>
    [Fact]
    public async Task Past_due_the_watchdog_stalls_each_retry_then_records_the_give_up_it_would_apply()
    {
        var clock = new MovedClock();
        var passes = new Dictionary<string, string>(StringComparer.Ordinal);
        var deadline = BusTypes.OpinionAsked.Deadline.Within;
        await using (var bus = Open(clock))
        {
            var watchdog = new BusWatchdog(bus, new ReadLook(_ => new BusLookReading(BusLookStates.Open, "reading")));
            Assert.True(bus.Append(Line(Asked("op-1"), passes)));
            await bus.FlushAsync();
            clock.Advance(deadline - TimeSpan.FromMinutes(1));
            Assert.Equal(0, await watchdog.TickAsync());

            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Equal(1, await watchdog.TickAsync());
            await bus.FlushAsync();
            Assert.Equal(0, await watchdog.TickAsync());
            Assert.Equal(1, Assert.Single(Armed()).Retry);

            clock.Advance(deadline);
            Assert.Equal(2, await watchdog.TickAsync());
            await bus.FlushAsync();
            Assert.Equal(0, await watchdog.TickAsync());
        }

        Assert.Empty(Armed());
        var logged = Logged();
        Assert.Equal(2, logged.Count(entry => entry.Kind == BusTypes.Stalled.Kind));
        var gaveUp = Assert.Single(logged, entry => entry.Kind == BusTypes.GiveUp.Kind);
        Assert.Contains(gaveUp.Payload, pair => pair is { Key: "consequence", Value: BusGiveUps.Refused });
        Assert.Contains(gaveUp.Payload, pair => pair is { Key: "applied", Value: false });
        Assert.Equal(["requester:driver/opinions", BusChains.Vacancy, BusChains.OwnerExit], (IReadOnlyList<string>)gaveUp.Payload.Single(pair => pair.Key == "to").Value!);

        Assert.Equal(0, BusStallsCommand.Run(["stalls"], _home, clock.GetUtcNow, out var said));
        output.WriteLine(said);
        Assert.Contains("armed: no timer.", said, StringComparison.Ordinal);
        // The last stall's due time is now: its second, one deadline after the first.
        var since = clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd HH:mm'Z'", CultureInfo.InvariantCulture);
        Assert.Contains(
            $"opinion.asked/1 op-1 (chain {logged.Single(entry => entry.Kind == BusTypes.OpinionAsked.Kind).Id}) since 2026-10-20 09:00Z: obliged codex, stall 2 of 2, past due since {since}; gave up (would): refused, hand the refusal to requester:driver/opinions, then vacancy, then owner-exit",
            said, StringComparison.Ordinal);
        Assert.Equal(2, BusStallsCommand.Run(["stalls", "--all"], _home, clock.GetUtcNow, out var usage));
        Assert.Contains(BusStallsCommand.Usage, usage, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A kill before the due time and a restart keep the timer's due time, the original append's, never the restart's; a stall made and
    /// lost to a kill before its write is made once after the restart, and every retry and the give-up are in the log exactly once.
    /// </summary>
    [Fact]
    public async Task A_kill_and_a_restart_keep_every_due_time_and_make_nothing_twice()
    {
        var clock = new MovedClock();
        var passes = new Dictionary<string, string>(StringComparer.Ordinal);
        var t0 = clock.GetUtcNow();
        var within = BusTypes.OpinionGiven.Deadline.Within;
        var look = new ReadLook(_ => new BusLookReading(BusLookStates.Open, "working"));

        var first = new KillableStore(new DesktopBusLogStore(_home));
        await using (var bus = Open(clock, first))
        {
            Assert.True(bus.Append(Line(Given("op-1", 1), passes)));
            await bus.FlushAsync();
            clock.Advance(within / 2);
            first.Killed = true;
        }

        Assert.Equal(t0 + within, Assert.Single(Armed()).Due);

        clock.Advance(within);
        var second = new KillableStore(new DesktopBusLogStore(_home));
        await using (var bus = Open(clock, second))
        {
            var watchdog = new BusWatchdog(bus, look);
            Assert.Equal(1, await watchdog.TickAsync());
            Assert.Equal(0, await watchdog.TickAsync());
            second.Killed = true;
        }

        Assert.DoesNotContain(Logged(), entry => entry.Kind == BusTypes.Stalled.Kind);
        Assert.Equal(0, Assert.Single(Armed()).Retry);

        await using (var bus = Open(clock))
        {
            var watchdog = new BusWatchdog(bus, look);
            for (var tick = 0; tick < 12; tick++)
            {
                await watchdog.TickAsync();
                await bus.FlushAsync();
                await watchdog.TickAsync();
                clock.Advance(within / 4);
            }
        }

        var logged = Logged();
        var stalls = logged.Where(entry => entry.Kind == BusTypes.Stalled.Kind).ToList();
        Assert.Equal([1L, 2L, 3L], stalls.Select(stall => (long)stall.Payload.Single(pair => pair.Key == "retry").Value!));
        Assert.Equal(t0 + within, DateTimeOffset.FromUnixTimeMilliseconds((long)stalls[0].Payload.Single(pair => pair.Key == "due").Value!));
        var gaveUp = Assert.Single(logged, entry => entry.Kind == BusTypes.GiveUp.Kind);
        // A given pass is the opinion's gate's to deliver or pass so its give-up hands the step to the gate's holder chain.
        Assert.Equal(["holder:driver/opinion-gate", BusChains.Vacancy, BusChains.OwnerExit], (IReadOnlyList<string>)gaveUp.Payload.Single(pair => pair.Key == "to").Value!);
        Assert.Empty(Armed());
    }

    /// <summary>
    /// 🔴 The seven runs stalled on 2026-11-20, replayed: a first pass asked, given and answered at the old tip, the tip moved and its checks
    /// clean, a recheck asked at the new tip and given (two of them clean), a restart, then answers late or never. Where the look sat at
    /// <i>Second opinion working</i>, the watchdog marks each run's opinion step overdue, the restart keeps the due times, nothing is made
    /// twice, and the comparer counts the look's reading beside each stall, the checks' step settling (the look's, on no fact yet) its
    /// disagreements.
    /// </summary>
    [Fact]
    public async Task The_seven_runs_stalled_on_2026_11_20_replayed_raise_their_stalls()
    {
        var clock = new MovedClock();
        var passes = new Dictionary<string, string>(StringComparer.Ordinal);
        var t0 = clock.GetUtcNow();
        var runs = Enumerable.Range(1, 7).ToList();
        // The look as it stood that day: every run's opinion step working, every run's checks step settled.
        var look = new ReadLook(timer => timer.Kind.StartsWith("check.", StringComparison.Ordinal)
            ? new BusLookReading(BusLookStates.Handled, "settled")
            : new BusLookReading(BusLookStates.Open, "working"));
        string Op(int run, string pass) => string.Create(CultureInfo.InvariantCulture, $"op-{run}-{pass}");

        async Task At(TimeSpan since, BusLog bus, Action append)
        {
            clock.Advance(t0 + since - clock.GetUtcNow());
            append();
            await bus.FlushAsync();
        }

        await using (var bus = Open(clock))
        {
            foreach (var run in runs) bus.Append(Line(Asked(Op(run, "a")), passes));
            await At(TimeSpan.FromMinutes(3), bus, () => runs.ForEach(run => bus.Append(Line(Given(Op(run, "a"), 1), passes))));
            await At(TimeSpan.FromMinutes(4), bus, () => runs.ForEach(run => bus.Append(OpinionBusFacts.OpinionDelivered(Op(run, "a"), "s-1", 1, "claims"))));
            await At(TimeSpan.FromMinutes(5), bus, () => runs.ForEach(run => bus.Append(Line(Answered(Op(run, "a")), passes))));
            await At(TimeSpan.FromMinutes(6), bus, () => runs.ForEach(run => bus.Append(CheckBusFacts.CheckStarted(Send($"run-{run}", $"inv-{run}", "cccc3333")))));
            await At(TimeSpan.FromMinutes(8), bus, () => runs.ForEach(run =>
                bus.Append(CheckBusFacts.CheckSettled(Send($"run-{run}", $"inv-{run}", "cccc3333"), Passed(Send($"run-{run}", $"inv-{run}", "cccc3333"))))));
            await At(TimeSpan.FromMinutes(9), bus, () => runs.ForEach(run => bus.Append(Line(Asked(Op(run, "b"), OpinionViews.RecheckPass), passes))));
            await At(TimeSpan.FromMinutes(15), bus, () => runs.ForEach(run => bus.Append(Line(Given(Op(run, "b"), run <= 2 ? 0 : 1), passes))));
            await At(TimeSpan.FromMinutes(16), bus, () => runs.Where(run => run > 2).ToList().ForEach(run => bus.Append(OpinionBusFacts.OpinionDelivered(Op(run, "b"), "s-1", 1, "claims"))));
        }

        // The restart, its timers read at open; answers after it for three runs, none ever for two.
        Assert.Equal(7 * 3, Armed().Count);
        await using (var bus = Open(clock))
        {
            var watchdog = new BusWatchdog(bus, look);
            for (var minutes = 20; minutes <= 300; minutes += 5)
            {
                if (minutes == 40) runs.Where(run => run is >= 3 and <= 5).ToList().ForEach(run => bus.Append(Line(Answered(Op(run, "b")), passes)));
                clock.Advance(t0 + TimeSpan.FromMinutes(minutes) - clock.GetUtcNow());
                await watchdog.TickAsync();
                await bus.FlushAsync();
                await watchdog.TickAsync();
            }
        }

        var logged = Logged();
        var stalls = logged.Where(entry => entry.Kind == BusTypes.Stalled.Kind).ToList();
        string Of(BusEnvelope entry, string key) => (string)entry.Payload.Single(pair => pair.Key == key).Value!;
        long Count(BusEnvelope entry, string key) => (long)entry.Payload.Single(pair => pair.Key == key).Value!;

        // Each run's opinion step marked overdue where the look sat at working.
        foreach (var run in runs)
        {
            Assert.Contains(stalls, stall => stall.Subject.StartsWith($"op-{run}-", StringComparison.Ordinal) && Of(stall, "look") == BusLookStates.Open);
        }

        // The clean rechecks' given stalls at its own due time, kept across the restart.
        var clean = stalls.First(stall => stall.Subject == Op(1, "b"));
        Assert.Equal(BusTypes.OpinionGiven.Kind, Of(clean, "kind"));
        Assert.Equal(t0 + TimeSpan.FromMinutes(15) + BusTypes.OpinionGiven.Deadline.Within, DateTimeOffset.FromUnixTimeMilliseconds(Count(clean, "due")));

        // Nothing made twice: one stall for each entry and retry, one give-up for each entry.
        Assert.All(stalls.GroupBy(stall => (Of(stall, "entry"), Count(stall, "retry"))), group => Assert.Single(group));
        var gaveUp = logged.Where(entry => entry.Kind == BusTypes.GiveUp.Kind).ToList();
        Assert.All(gaveUp.GroupBy(entry => Of(entry, "entry")), group => Assert.Single(group));
        Assert.Contains(gaveUp, entry => entry.Subject == Op(6, "b") && Of(entry, "consequence") == BusGiveUps.FinalExit
                                         && ((IReadOnlyList<string>)entry.Payload.Single(pair => pair.Key == "to").Value!)[0] == "holder:session");
        Assert.Contains(gaveUp, entry => entry.Subject == Op(3, "b") && Of(entry, "consequence") == BusGiveUps.Held);

        Assert.Equal(0, BusStallsCommand.Run(["stalls", "--compare"], _home, clock.GetUtcNow, out var said));
        output.WriteLine(said);
        Assert.Contains("check.passed/1 agree 0, disagree 7, unknown 0", said, StringComparison.Ordinal);
        Assert.Contains("opinion.given/1 agree 2, disagree 0, unknown 0", said, StringComparison.Ordinal);
        Assert.Contains("opinion.delivered/1 agree 2, disagree 0, unknown 0", said, StringComparison.Ordinal);
        Assert.Contains("opinion.answered/1 agree 10, disagree 0, unknown 0", said, StringComparison.Ordinal);
        Assert.Contains("check.passed/1 run-7: the bus: past due since 2026-10-20 09:38Z, stall 3 of 3; the look: handled (settled)", said, StringComparison.Ordinal);
    }

    /// <summary>The opinion record's reading: a pass asked is open while read, a given open until answered unless clean, a delivery until answered.</summary>
    [Fact]
    public void The_opinion_records_look_reads_each_obligation()
    {
        static OpinionView View(string state) => new("op-1", "done", "first", "s-1", "s-2", "engine", "base", "tip", "codex", "Codex", state);
        Assert.Equal(BusLookStates.Open, OpinionRecordLook.Of(BusTypes.OpinionAsked.Kind, View(OpinionViews.Reading)).State);
        Assert.Equal(BusLookStates.Handled, OpinionRecordLook.Of(BusTypes.OpinionAsked.Kind, View(OpinionViews.GivenState)).State);
        Assert.Equal(new BusLookReading(BusLookStates.Handled, "clean"), OpinionRecordLook.Of(BusTypes.OpinionGiven.Kind, View(OpinionViews.GivenState) with { Findings = [] }));
        Assert.Equal(BusLookStates.Unknown, OpinionRecordLook.Of(BusTypes.OpinionDelivered.Kind, null).State);
        Assert.Equal(BusCompare.Agree, BusCompare.Verdict(BusLookStates.Open));
        Assert.Equal(BusCompare.Disagree, BusCompare.Verdict(BusLookStates.Handled));
        Assert.Equal(BusCompare.Unknown, BusCompare.Verdict(null));
    }

    /// <summary>
    /// 🔴 A timer of a kind the registry no longer declares is dropped, never left to hide the rest; the last stall and its
    /// give-up are one write and neither is commit-class in shadow; a process that does not hold the lease reads no look.
    /// </summary>
    [Fact]
    public async Task An_undeclared_kind_is_dropped_the_last_stall_and_its_give_up_are_one_write_and_no_lease_reads_no_look()
    {
        var clock = new MovedClock();
        var passes = new Dictionary<string, string>(StringComparer.Ordinal);
        var batches = new List<BusBatch>();
        var reads = 0;
        var look = new ReadLook(_ =>
        {
            Interlocked.Increment(ref reads);
            return new BusLookReading(BusLookStates.Open, "working");
        });
        await using (var bus = Open(clock, new RecordingStore(new DesktopBusLogStore(_home), batches)))
        {
            bus.Append(Line(Asked("op-1"), passes));
            await bus.FlushAsync();
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DesktopStore.LocationIn(_home)}"))
            {
                connection.Open();
                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO bus_timers (entry, seq, kind, subject, effect, obliged, armed, due, retry) VALUES ('gone', 1, 'gone.kind/1', 's', NULL, 'x', 0, 0, 0)";
                insert.ExecuteNonQuery();
            }

            var watchdog = new BusWatchdog(bus, look);
            for (var retry = 0; retry <= BusTypes.OpinionAsked.Retries; retry++)
            {
                clock.Advance(BusTypes.OpinionAsked.Deadline.Within + TimeSpan.FromMinutes(1));
                await watchdog.TickAsync();
                await bus.FlushAsync();
            }
        }

        Assert.DoesNotContain(Armed(), timer => timer.Entry == "gone");
        var last = Assert.Single(batches, batch => batch.Entries.Any(entry => entry.Kind == BusTypes.GiveUp.Kind));
        Assert.Contains(last.Entries, entry => entry.Kind == BusTypes.Stalled.Kind);
        Assert.False(last.Commit);
        Assert.Equal(BusTypes.OpinionAsked.Retries + 1, reads);

        await using var other = BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = clock, Writer = () => false });
        clock.Advance(TimeSpan.FromDays(2));
        Assert.Equal(0, await new BusWatchdog(other, look).TickAsync());
        Assert.Equal(BusTypes.OpinionAsked.Retries + 1, reads);
    }

    /// <summary>A store that keeps each batch it is handed, then writes it.</summary>
    private sealed class RecordingStore(DesktopBusLogStore inner, List<BusBatch> batches) : IBusLogStore
    {
        public BusWritten Write(BusBatch batch)
        {
            lock (batches) batches.Add(batch);
            return inner.Write(batch);
        }

        public IReadOnlyList<BusEnvelope> Read(long after, int limit) => inner.Read(after, limit);

        public IReadOnlyList<BusMeasureRow> Measures(string fromDay) => inner.Measures(fromDay);

        public IReadOnlyList<BusTimer> Timers(DateTimeOffset? by, int limit) => inner.Timers(by, limit);

        public void DropTimers(IReadOnlyList<string> entries) => inner.DropTimers(entries);

        public void Dispose() => inner.Dispose();
    }

    private sealed class ReadLook(Func<BusTimer, BusLookReading> read) : IBusLook
    {
        public Task<BusLookReading> ReadAsync(BusTimer timer, CancellationToken ct) => Task.FromResult(read(timer));
    }

    /// <summary>A store whose process is killed: from then on nothing it is handed is written, as if the process had ended before the write.</summary>
    private sealed class KillableStore(DesktopBusLogStore inner) : IBusLogStore
    {
        public bool Killed { get; set; }

        public BusWritten Write(BusBatch batch) => Killed ? throw new InvalidOperationException("the process was killed") : inner.Write(batch);

        public IReadOnlyList<BusEnvelope> Read(long after, int limit) => inner.Read(after, limit);

        public IReadOnlyList<BusMeasureRow> Measures(string fromDay) => inner.Measures(fromDay);

        public IReadOnlyList<BusTimer> Timers(DateTimeOffset? by, int limit) => inner.Timers(by, limit);

        public void Dispose() => inner.Dispose();
    }
}
