using System.Globalization;
using Daoris.Driver;
using Daoris.Testing;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The bus's log in its first form: envelopes appended behind, flushed at most about once a second while dirty, a
/// commit class at once and grouped, nothing written while idle; a shadow append that can never fail the write it rides beside; the
/// opinion and check facts teed in, replayed in order; the measures `daoris bus stats` reads. Homes under <c>_fixtures/</c>.
/// </summary>
public sealed class BusLogTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _home = Fixture();

    private static string Fixture()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "bus-log", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        return home;
    }

    public void Dispose() => GitSeed.Discard(_home);

    /// <summary>A log's options on a clock that stands still: its entries stamped on <see cref="Today"/>, flushed by a commit or a test.</summary>
    private static BusLogOptions Options() => new() { Clock = new MovedClock() };

    private static DateTimeOffset Today() => new MovedClock().GetUtcNow();

    private static BusDraft Plain(string subject, int n = 0) =>
        new(CheckBusFacts.CheckStartedKind, subject, "test") { Payload = [("invocation", $"i-{n.ToString(CultureInfo.InvariantCulture)}")] };

    private static BusDraft Commit(string subject, int n = 0) => Plain(subject, n) with { Kind = CheckBusFacts.CheckPassedKind, Commit = true };

    /// <summary>
    /// 🔴 A log whose store throws keeps today's write green: the opinion's machine-log line is written, the append answers at once,
    /// and the miss is counted and said once in the machine log, never thrown at the writer.
    /// </summary>
    [Fact]
    public async Task A_refused_store_keeps_the_write_beside_it_green_and_counts_the_miss()
    {
        var clock = new MovedClock();
        var store = new ThrowingStore();
        using var log = new MachineLog(_home, "driver-test", () => clock.GetUtcNow());
        using var service = new ServiceClient("http://127.0.0.1:9", null);
        using var sessionLog = new SessionLog(log, service, new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)));
        await using var bus = BusLog.Over(store, new BusLogOptions { Clock = clock, Log = log });
        using var tee = new OpinionBusTee(service, bus);

        service.LandingSaid(OpinionLines.Asked("op-1", "s-1", "done", "Codex", "codex", OpinionViews.FirstPass));
        service.LandingSaid(OpinionLines.Asked("op-2", "s-1", "done", "Codex", "codex", OpinionViews.FirstPass));
        await bus.FlushAsync();
        await bus.FlushAsync();

        using var file = new FileStream(
            Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl").Single(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var lines = new StreamReader(file).ReadToEnd().Split('\n');
        Assert.Equal(2, lines.Count(line => line.Contains("\"opinion.asked\"", StringComparison.Ordinal)));
        Assert.Single(lines, line => line.Contains("\"bus.refused\"", StringComparison.Ordinal));
        Assert.True(bus.Tally.Missed.GetValueOrDefault(BusMisses.Store) >= 1, "the store's refusal is counted");
        Assert.Equal(2, bus.Tally.Appended);

        // A payload that is no id, code, count or time is refused at emit and counted, the writer told nothing.
        Assert.False(bus.Append(Plain("run-1") with { Payload = [("words", "a sentence\nwith a line")] }));
        Assert.Equal(1, bus.Tally.Missed[BusMisses.Shape]);
    }

    /// <summary>🔴 An idle minute writes nothing; a plain entry is written behind, at most about once a second.</summary>
    [Fact]
    public async Task An_idle_minute_writes_nothing_and_a_plain_entry_waits_out_the_second()
    {
        var clock = new MovedClock();
        var store = new CountingStore();
        await using var bus = BusLog.Over(store, new BusLogOptions { Clock = clock });

        for (var second = 0; second < 60; second++) clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(0, store.Writes);

        Assert.True(bus.Append(Plain("run-1")));
        await Poll.Until(() => clock.Waiting, () => "the loop never waited out the second");
        Assert.Equal(0, store.Writes);
        clock.Advance(TimeSpan.FromMilliseconds(400));
        Assert.True(bus.Append(Plain("run-1", 1)));
        Assert.Equal(0, store.Writes);
        clock.Advance(TimeSpan.FromMilliseconds(700));
        await Poll.Until(() => store.Writes == 1, () => $"{store.Writes} writes");
        Assert.Equal(2, store.Batches[0].Entries.Count);

        for (var second = 0; second < 60; second++) clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, store.Writes);
    }

    /// <summary>🔴 A burst of commit-class entries flushes at once and grouped: one write in flight, the rest in the next.</summary>
    [Fact]
    public async Task A_burst_of_commit_class_entries_flushes_at_once_and_grouped()
    {
        var clock = new MovedClock();
        var store = new CountingStore { Held = new ManualResetEventSlim(false) };
        await using var bus = BusLog.Over(store, new BusLogOptions { Clock = clock });

        Assert.True(bus.Append(Commit("run-1")));
        await Poll.Until(() => store.Entered == 1, () => "the commit class waited for the second");
        for (var n = 1; n < 100; n++) Assert.True(bus.Append(Commit("run-1", n)));
        store.Held.Set();

        await Poll.Until(() => store.Writes == 2, () => $"{store.Writes} writes");
        Assert.Equal([1, 99], store.Batches.Select(batch => batch.Entries.Count));
        Assert.All(store.Batches, batch => Assert.True(batch.Commit));
        Assert.Equal(Enumerable.Range(1, 100).Select(n => (long)n), store.Batches.SelectMany(batch => batch.Entries).Select(entry => entry.Seq));
    }

    /// <summary>
    /// 🔴 The teed facts replay in order from the home's log: an opinion asked, given and answered, a check started and failed with its
    /// red classes, each envelope whole, a given caused by its ask; a log opened again goes on after the newest.
    /// </summary>
    [Fact]
    public async Task A_replay_rebuilds_the_teed_facts_in_order()
    {
        using var service = new ServiceClient("http://127.0.0.1:9", null);
        await using (var bus = BusLog.Over(new DesktopBusLogStore(_home), Options()))
        {
            using var tee = new OpinionBusTee(service, bus);
            service.LandingSaid(OpinionLines.Asked("op-1", "s-1", "done", "Codex", "codex", OpinionViews.FirstPass));
            var send = new VerifySend("run-7", "inv-1", "Engine", "c:/x", "0123456789abcdef", "none");
            bus.Append(CheckBusFacts.CheckStarted(send));
            bus.Append(CheckBusFacts.CheckSettled(send, new VerifyResult("inv-1", send.Commit, VerifyVerdicts.Failed, "two red", [])
            {
                Failed = "reached", Red = ["Daoris.Desktop.Driver.Tests|BusLogTests", "web|ask.spec"], WallSeconds = 42.5,
            }));
            service.LandingSaid(OpinionLines.Answered("op-1", new OpinionReading("s-1", null, Today(), [])));
        }

        using var store = new DesktopBusLogStore(_home);
        var read = store.Read(0, 100);
        Assert.Equal(
            [OpinionBusFacts.OpinionAskedKind,CheckBusFacts.CheckStartedKind, CheckBusFacts.CheckFailedKind, OpinionBusFacts.OpinionAnsweredKind],
            read.Select(entry => entry.Kind));
        Assert.Equal([1L, 2, 3, 4], read.Select(entry => entry.Seq));
        Assert.Equal("op-1", read[0].Subject);
        Assert.Equal("codex", read[0].Answerer);
        Assert.Equal(read[0].Id, Assert.Single(read[3].Causes));
        var failed = read[2];
        Assert.Equal("run-7", failed.Subject);
        Assert.Equal(read[1].Id, Assert.Single(failed.Causes));
        Assert.Equal("inv-1|0123456789abcdef", failed.Effect);
        Assert.Contains(failed.Payload, pair => pair.Key == "red" && pair.Value is IReadOnlyList<string> { Count: 2 });
        Assert.Contains(failed.Payload, pair => pair.Key == "redCount" && Equals(pair.Value, 2L));

        await using (var again = BusLog.Over(new DesktopBusLogStore(_home), Options()))
        {
            again.Append(Plain("run-8"));
        }

        using var reread = new DesktopBusLogStore(_home);
        Assert.Equal(5, reread.Read(4, 10).Single().Seq);
    }

    /// <summary>🔴 Retention keeps the newest rows under the cap, the oldest cut first, and counts what it cut.</summary>
    [Fact]
    public async Task Retention_keeps_the_newest_rows_under_the_cap()
    {
        await using (var bus = BusLog.Over(new DesktopBusLogStore(_home, cap: 50), Options()))
        {
            for (var n = 0; n < 80; n++) bus.Append(Plain("run-1", n));
            await bus.FlushAsync();
            for (var n = 80; n < 120; n++) bus.Append(Plain("run-1", n));
        }

        using var store = new DesktopBusLogStore(_home, cap: 50);
        var kept = store.Read(0, 500);
        Assert.Equal(50, kept.Count);
        Assert.Equal(71, kept[0].Seq);
        Assert.Equal(120, kept[^1].Seq);
        Assert.Equal(70, store.Measures("2000-01-01").Where(row => row is { Measure: "cut" }).Sum(row => row.Total));
    }

    /// <summary>
    /// 🔴 `daoris bus stats` reads the measures: events a day by kind, the flushes' count and time, the bytes, a look stage's time and a
    /// handling time; a bare `bus` says its usage; a home with no log says so.
    /// </summary>
    [Fact]
    public async Task The_stats_verb_reads_the_days_measures()
    {
        Assert.Equal(0, BusStatsCommand.Run(["stats"], _home, Today, out var none));
        Assert.Contains("no bus log", none, StringComparison.Ordinal);

        await using (var bus = BusLog.Over(new DesktopBusLogStore(_home), Options()))
        {
            var send = new VerifySend("run-7", "inv-1", "Engine", "c:/x", "0123456789abcdef", "none");
            bus.Append(CheckBusFacts.CheckStarted(send));
            bus.Append(CheckBusFacts.CheckSettled(send, new VerifyResult("inv-1", send.Commit, VerifyVerdicts.Reached, null, []) { WallSeconds = 30 }));
            bus.Staged("opinions", 5200);
            bus.Staged("opinions", 4800);
        }

        Assert.Equal(0, BusStatsCommand.Run(["stats"], _home, Today, out var said));
        output.WriteLine(said);
        Assert.Contains("check.started/1 1", said, StringComparison.Ordinal);
        Assert.Contains("check.passed/1 1", said, StringComparison.Ordinal);
        Assert.Contains("flushes ", said, StringComparison.Ordinal);
        Assert.Contains("bytes ", said, StringComparison.Ordinal);
        Assert.Contains("opinions n=2 mean 5000 ms most 5200 ms", said, StringComparison.Ordinal);
        Assert.Contains("check.passed/1 n=1 mean 30000 ms", said, StringComparison.Ordinal);

        Assert.Equal(2, BusStatsCommand.Run([], _home, Today, out var usage));
        Assert.Contains(BusStatsCommand.Usage, usage, StringComparison.Ordinal);
        Assert.Equal(2, BusStatsCommand.Run(["stats", "--days", "0"], _home, Today, out _));
    }

    /// <summary>
    /// A day's mix through the home's real log, measured: the rows' bytes, each flush's time, the cap's bound in bytes. The first figures
    /// the bus's budget reads before the install's own (`daoris bus stats`); asserted only against a generous ceiling.
    /// </summary>
    [Fact]
    public async Task A_day_of_teed_facts_is_measured()
    {
        await using (var bus = BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = new MovedClock() }))
        {
            for (var n = 0; n < 1000; n++)
            {
                var send = new VerifySend($"run-{n % 40}", $"inv-{n}", "Engine", "c:/x", "0123456789abcdef0123456789abcdef01234567", "none");
                bus.Append(n % 2 == 0 ? CheckBusFacts.CheckStarted(send) : CheckBusFacts.CheckSettled(send, new VerifyResult($"inv-{n}", send.Commit, VerifyVerdicts.Reached, null, []) { WallSeconds = 61 }));
                if (n % 50 == 49) await bus.FlushAsync();
            }
        }

        using var store = new DesktopBusLogStore(_home);
        var measures = store.Measures("2000-01-01");
        var bytes = measures.Single(row => row is { Measure: "bytes", Name: "rows" });
        var flushes = measures.Single(row => row is { Measure: "flush", Name: "us" });
        var perRow = bytes.Total / (double)bytes.Count;
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"rows {bytes.Count}, {bytes.Total} B, {perRow:0} B a row, most {bytes.Most} B; flushes {flushes.Count}, mean {flushes.Total / (double)flushes.Count / 1000:0.00} ms, most {flushes.Most / 1000.0:0.00} ms; cap {DesktopBusLogStore.Cap} rows ~ {DesktopBusLogStore.Cap * perRow / 1048576:0.0} MiB of rows"));
        Assert.Equal(1000, bytes.Count);
        Assert.InRange(perRow, 60, 400);
    }

    private sealed class ThrowingStore : IBusLogStore
    {
        public long Newest() => throw new IOException("the log's store cannot be opened");

        public BusWritten Write(BusBatch batch) => throw new IOException("the log's store cannot be written");

        public IReadOnlyList<BusEnvelope> Read(long after, int limit) => [];

        public IReadOnlyList<BusMeasureRow> Measures(string fromDay) => [];

        public void Dispose()
        {
        }
    }

    private sealed class CountingStore : IBusLogStore
    {
        private readonly Lock _gate = new();
        private int _entered;

        public ManualResetEventSlim? Held { get; init; }

        public List<BusBatch> Batches { get; } = [];

        public int Writes
        {
            get
            {
                lock (_gate) return Batches.Count(batch => batch.Entries.Count > 0);
            }
        }

        public int Entered => Volatile.Read(ref _entered);

        public long Newest() => 0;

        public BusWritten Write(BusBatch batch)
        {
            if (batch.Entries.Count > 0) Interlocked.Increment(ref _entered);
            Held?.Wait(TimeSpan.FromMinutes(1));
            lock (_gate) Batches.Add(batch);
            return new BusWritten(batch.Entries.Count * 100L, 0);
        }

        public IReadOnlyList<BusEnvelope> Read(long after, int limit) => [];

        public IReadOnlyList<BusMeasureRow> Measures(string fromDay) => [];

        public void Dispose() => Held?.Dispose();
    }
}
