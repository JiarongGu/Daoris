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
        new(BusTypes.CheckStarted, subject) { Payload = [("invocation", $"i-{n.ToString(CultureInfo.InvariantCulture)}")] };

    private static BusDraft Commit(string subject, int n = 0) => Plain(subject, n) with { Type = BusTypes.CheckPassed, Commit = true };

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

        // A field its type does not declare is dropped at emit and counted, the fact kept and the writer told nothing.
        Assert.True(bus.Append(Plain("run-1") with { Payload = [("words", "a sentence\nwith a line")] }));
        Assert.Equal(1, bus.Tally.Dropped[BusDrops.Undeclared]);
        Assert.False(bus.Tally.Missed.ContainsKey(BusMisses.Shape));
    }

    /// <summary>🔴 A type the registry does not declare is refused at emit and counted: one named nowhere, and a look-alike made elsewhere.</summary>
    [Fact]
    public async Task A_type_the_registry_does_not_declare_is_refused_and_counted()
    {
        var store = new CountingStore();
        await using var bus = BusLog.Over(store, Options());
        var nowhere = BusTypes.CheckStarted with { Name = "check.forged" };
        var lookAlike = BusTypes.CheckStarted with { };

        Assert.False(bus.Append(new BusDraft(nowhere, "run-1")));
        Assert.False(bus.Append(new BusDraft(lookAlike, "run-1")));
        Assert.Equal(2, bus.Tally.Missed[BusMisses.Unknown]);
        Assert.True(bus.Append(Plain("run-1")));
        await bus.FlushAsync();
        Assert.Equal([BusTypes.CheckStarted.Kind], store.All.SelectMany(batch => batch.Entries).Select(entry => entry.Kind));
    }

    /// <summary>
    /// 🔴 A value not of its field's shape is dropped at emit and the fact kept, so a repository named with a space, or a parameterized
    /// test class among the red, costs that value and never the fact: the field left unknown, a list keeping the ids that fit, each drop
    /// counted by reason, type and field, said once, and read by the stats.
    /// </summary>
    [Fact]
    public async Task A_value_not_of_its_fields_shape_is_dropped_and_the_fact_kept()
    {
        var clock = new MovedClock();
        using var log = new MachineLog(_home, "driver-test", () => clock.GetUtcNow());
        await using (var bus = BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = clock, Log = log }))
        {
            var send = new VerifySend("run-7", "inv-1", "My Engine", "c:/x", "0123456789abcdef", "none");
            Assert.True(bus.Append(CheckBusFacts.CheckStarted(send)));
            Assert.True(bus.Append(CheckBusFacts.CheckSettled(send, new VerifyResult("inv-1", send.Commit, VerifyVerdicts.Failed, "red", [])
            {
                Failed = "unit", Red = ["Engine.Tests|PlayerTests(speed: 2)", "Engine.Tests|MapTests"],
            })));
            Assert.True(bus.Append(Plain("run-7") with { Payload = [("gates", "three")] }));
            Assert.Equal(3, bus.Tally.Dropped[BusDrops.Shape]);
            Assert.False(bus.Tally.Missed.ContainsKey(BusMisses.Shape));
        }

        using var store = new DesktopBusLogStore(_home);
        var read = store.Read(0, 10);
        Assert.Equal(3, read.Count);
        Assert.Contains(read[0].Payload, pair => pair is { Key: "repository", Value: null });
        Assert.Contains(read[0].Payload, pair => pair.Key == "commit" && Equals(pair.Value, "0123456789abcdef"));
        Assert.Equal(["Engine.Tests|MapTests"], (IReadOnlyList<string>)read[1].Payload.Single(pair => pair.Key == "red").Value!);
        Assert.Contains(read[1].Payload, pair => pair.Key == "redCount" && Equals(pair.Value, 2L));
        Assert.Contains(read[2].Payload, pair => pair is { Key: "gates", Value: null });

        Assert.Equal(0, BusStatsCommand.Run(["stats"], _home, Today, out var said));
        output.WriteLine(said);
        Assert.Contains("dropped values, their facts kept: ", said, StringComparison.Ordinal);
        Assert.Contains("shape|check.started/1|repository 1", said, StringComparison.Ordinal);
        Assert.Contains("shape|check.failed/1|red 1", said, StringComparison.Ordinal);
        Assert.Contains("shape|check.started/1|gates 1", said, StringComparison.Ordinal);

        using var file = new FileStream(
            Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl").Single(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Assert.Equal(3, new StreamReader(file).ReadToEnd().Split('\n').Count(line => line.Contains("\"bus.dropped\"", StringComparison.Ordinal)));
    }

    /// <summary>
    /// 🔴 A log that lost the lease and regained it seats its batch after what the other holder wrote meanwhile, and every later batch
    /// goes on after it: no order collides, nothing is dropped as the store's refusal, and the replay keeps each log's entries in order.
    /// </summary>
    [Fact]
    public async Task A_log_that_regained_the_lease_seats_its_batch_after_the_other_holders()
    {
        bool? first = true;
        await using var regained = BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = new MovedClock(), Writer = () => first });
        for (var n = 0; n < 3; n++) Assert.True(regained.Append(Plain("run-a", n)));
        await regained.FlushAsync();

        first = false;
        await using (var other = BusLog.Over(new DesktopBusLogStore(_home), Options()))
        {
            for (var n = 0; n < 2; n++) Assert.True(other.Append(Plain("run-b", n)));
        }

        first = true;
        Assert.True(regained.Append(Plain("run-a", 3)));
        await regained.FlushAsync();
        Assert.True(regained.Append(Plain("run-a", 4)));
        await regained.FlushAsync();

        Assert.False(regained.Tally.Missed.ContainsKey(BusMisses.Store));
        using var store = new DesktopBusLogStore(_home);
        var read = store.Read(0, 20);
        Assert.Equal([1L, 2, 3, 4, 5, 6, 7], read.Select(entry => entry.Seq));
        Assert.Equal(["run-a", "run-a", "run-a", "run-b", "run-b", "run-a", "run-a"], read.Select(entry => entry.Subject));
        Assert.Equal(["i-3", "i-4"], read.Skip(5).Select(entry => (string)entry.Payload.Single(pair => pair.Key == "invocation").Value!));
    }

    /// <summary>🔴 The lease is asked inside the write's transaction, which holds the store for writing (another connection cannot take it), and a lost lease drops the batch.</summary>
    [Fact]
    public async Task The_lease_is_asked_inside_the_writes_transaction()
    {
        var locked = new System.Collections.Concurrent.ConcurrentQueue<bool>();
        var holder = true;
        var location = DesktopStore.LocationIn(_home);
        await using (var made = BusLog.Over(new DesktopBusLogStore(_home), Options()))
        {
            Assert.True(made.Append(Plain("run-0")));
        }

        await using var bus = BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions
        {
            Clock = new MovedClock(),
            Writer = () =>
            {
                using var probe = StoreEngineSource.Connect(location, Daoris.Ports.Store.DesktopOpen.ReadWrite);
                void Run(string sql)
                {
                    using var command = probe.CreateCommand();
                    command.CommandText = sql;
                    command.CommandTimeout = 1;
                    command.ExecuteNonQuery();
                }

                Run("PRAGMA busy_timeout = 0");
                try
                {
                    Run("BEGIN IMMEDIATE");
                    Run("ROLLBACK");
                    locked.Enqueue(false);
                }
                catch (Exception error) when (StoreEngineSource.Busy(error))
                {
                    locked.Enqueue(true);
                }

                return holder;
            },
        });

        Assert.True(bus.Append(Plain("run-1")));
        Assert.Equal([false], locked);
        await bus.FlushAsync();
        Assert.Equal([false, true], locked);

        holder = false;
        Assert.True(bus.Append(Plain("run-1", 1)));
        await bus.FlushAsync();
        Assert.Equal(1, bus.Tally.Missed[BusMisses.NotHolder]);
        using var store = new DesktopBusLogStore(_home);
        Assert.Equal(["run-0", "run-1"], store.Read(0, 10).Select(entry => entry.Subject));
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

    /// <summary>
    /// 🔴 A look stage timed while nothing is appended is written within the measures' period, never left in memory: the measures alone
    /// at most every fifteen minutes from the last write, and not before.
    /// </summary>
    [Fact]
    public async Task A_stage_timed_while_idle_is_written_within_the_measures_period()
    {
        var clock = new MovedClock();
        var store = new CountingStore();
        await using var bus = BusLog.Over(store, new BusLogOptions { Clock = clock });

        bus.Staged("opinions", 5200);
        await Poll.Until(() => clock.Waiting, () => "the loop never waited out the measures' period");
        clock.Advance(BusLog.MeasureEvery - TimeSpan.FromMinutes(1));
        Assert.Empty(store.All);
        clock.Advance(TimeSpan.FromMinutes(1));
        await Poll.Until(() => store.All.Count == 1, () => $"{store.All.Count} writes");
        Assert.Contains(store.All[0].Measures, row => row is { Measure: BusMeasures.Stage, Name: "opinions", Total: 5200 });
        Assert.Empty(store.All[0].Entries);

        bus.Staged("opinions", 4800);
        await Poll.Until(() => clock.Waiting, () => "the loop never waited out the next period");
        clock.Advance(BusLog.MeasureEvery - TimeSpan.FromMinutes(1));
        Assert.Single(store.All);
        clock.Advance(TimeSpan.FromMinutes(1));
        await Poll.Until(() => store.All.Count == 2, () => $"{store.All.Count} writes");
        Assert.Contains(store.All[1].Measures, row => row is { Measure: BusMeasures.Stage, Name: "opinions", Total: 4800 });
    }

    /// <summary>
    /// 🔴 A flush measures the pages it wrote to disk (the write-ahead log's frames) beside the rows' own bytes, and the stats say both;
    /// the bus's connection bounds its log file as the engine's does.
    /// </summary>
    [Fact]
    public async Task A_flush_measures_the_pages_it_wrote_beside_the_rows_bytes()
    {
        await using (var bus = BusLog.Over(new DesktopBusLogStore(_home), Options()))
        {
            for (var n = 0; n < 40; n++) bus.Append(Plain("run-1", n));
            await bus.FlushAsync();
            bus.Append(Commit("run-1", 40));
        }

        using var store = new DesktopBusLogStore(_home);
        var measures = store.Measures("2000-01-01");
        var rows = measures.Single(row => row is { Measure: BusMeasures.Bytes, Name: BusMeasures.Rows });
        var pages = measures.Single(row => row is { Measure: BusMeasures.Disk, Name: BusMeasures.Pages });
        var written = measures.Single(row => row is { Measure: BusMeasures.Disk, Name: BusMeasures.Bytes });
        Assert.Equal(41, rows.Count);
        Assert.True(pages.Total >= 2, $"{pages.Total} pages for two flushes of entries");
        Assert.True(pages.Count >= 2, $"{pages.Count} writes measured");
        Assert.Equal(pages.Total * store.Setting("page_size"), written.Total);
        Assert.Equal(DesktopBusLogStore.JournalLimit, store.Setting("journal_size_limit"));

        Assert.Equal(0, BusStatsCommand.Run(["stats"], _home, Today, out var said));
        output.WriteLine(said);
        Assert.Contains($"rows {rows.Total} bytes", said, StringComparison.Ordinal);
        Assert.Contains($"disk {pages.Total} pages", said, StringComparison.Ordinal);
    }

    /// <summary>🔴 A settle whose red classes are not known writes no count of them: absent evidence stays unknown, never zero.</summary>
    [Fact]
    public void A_settle_whose_red_classes_are_unknown_writes_no_count()
    {
        var send = new VerifySend("run-7", "inv-1", "Engine", "c:/x", "0123456789abcdef", "none");
        var unknown = CheckBusFacts.CheckSettled(send, new VerifyResult("inv-1", send.Commit, VerifyVerdicts.Failed, "a gate failed", []) { Failed = "unit" });
        Assert.Contains(unknown.Payload, pair => pair is { Key: "redCount", Value: null });
        Assert.Contains(unknown.Payload, pair => pair is { Key: "red", Value: null });

        var none = CheckBusFacts.CheckSettled(send, new VerifyResult("inv-1", send.Commit, VerifyVerdicts.Reached, null, []) { Red = [] });
        Assert.Contains(none.Payload, pair => pair.Key == "redCount" && Equals(pair.Value, 0L));
    }

    /// <summary>
    /// 🔴 The lease is asked off the writer's path: once for a burst, again only after its period; an unreadable lease keeps the last
    /// answer; asked afresh before each write, so entries taken while the lease is lost are never written by the old holder; and a stage
    /// is timed only by the holder.
    /// </summary>
    [Fact]
    public async Task The_lease_is_asked_off_the_writers_path_and_again_before_each_write()
    {
        var clock = new MovedClock();
        var store = new CountingStore();
        bool? holder = true;
        var asked = 0;
        await using var bus = BusLog.Over(store, new BusLogOptions
        {
            Clock = clock, Writer = () =>
            {
                Interlocked.Increment(ref asked);
                return holder;
            },
        });

        for (var n = 0; n < 100; n++) Assert.True(bus.Append(Plain("run-1", n)));
        Assert.Equal(1, asked);
        holder = null;
        clock.Advance(BusLog.WriterEvery);
        Assert.True(bus.Append(Plain("run-1", 100)), "an unreadable lease keeps the last answer");

        holder = false;
        await bus.FlushAsync();
        Assert.Empty(store.All);
        Assert.Equal(101, bus.Tally.Missed[BusMisses.NotHolder]);

        bus.Staged("opinions", 5200);
        clock.Advance(BusLog.WriterEvery);
        Assert.False(bus.Append(Plain("run-1", 101)));
        holder = true;
        clock.Advance(BusLog.WriterEvery);
        await bus.FlushAsync();
        Assert.DoesNotContain(store.All.SelectMany(batch => batch.Measures), row => row.Measure == BusMeasures.Stage);
    }

    /// <summary>
    /// 🔴 A store busy past its wait drops nothing: the batch goes back ahead of the next, in its order, tried again after a pause and
    /// counted; past the tries it is dropped and counted. The bus waits on the lock as long as the store's engine does.
    /// </summary>
    [Fact]
    public async Task A_busy_store_puts_the_batch_back_in_its_order_and_drops_it_only_past_the_tries()
    {
        var clock = new MovedClock();
        var store = new CountingStore { Busy = 2 };
        await using (var bus = BusLog.Over(store, new BusLogOptions { Clock = clock }))
        {
            Assert.True(bus.Append(Commit("run-1")));
            await Poll.Until(() => store.Refused == 1, () => $"{store.Refused} refusals");
            Assert.True(bus.Append(Plain("run-1", 1)));
            await Poll.Until(() => clock.Waiting, () => "the loop never paused");
            clock.Advance(BusLog.RetryAfter);
            await Poll.Until(() => store.Refused == 2, () => $"{store.Refused} refusals");
            await Poll.Until(() => clock.Waiting, () => "the loop never paused again");
            clock.Advance(BusLog.RetryAfter);
            await Poll.Until(() => store.Writes == 1, () => $"{store.Writes} writes");

            Assert.Equal([1L, 2], store.Batches.Single(batch => batch.Entries.Count > 0).Entries.Select(entry => entry.Seq));
            Assert.Equal(2, bus.Tally.Retried);
            Assert.False(bus.Tally.Missed.ContainsKey(BusMisses.Store));
        }

        var stuck = new CountingStore { Busy = int.MaxValue };
        var later = new MovedClock();
        await using var lost = BusLog.Over(stuck, new BusLogOptions { Clock = later });
        Assert.True(lost.Append(Commit("run-1")));
        for (var tried = 1; tried <= BusLog.MostRetries; tried++)
        {
            var refused = tried;
            await Poll.Until(() => stuck.Refused == refused && later.Waiting, () => $"{stuck.Refused} refusals");
            later.Advance(BusLog.RetryAfter);
        }

        await Poll.Until(() => lost.Tally.Missed.GetValueOrDefault(BusMisses.Store) == 1, () => "the batch was never dropped past its tries");
        Assert.Equal(BusLog.MostRetries, lost.Tally.Retried);

        using var real = new DesktopBusLogStore(_home);
        Assert.Equal((long)Daoris.Ports.Store.DesktopStoreEngineOptions.DefaultBusyWait.TotalMilliseconds, real.Setting("busy_timeout"));
    }

    /// <summary>🔴 A write a caller waits on says whether the store kept it: kept where written, not kept where refused.</summary>
    [Fact]
    public async Task A_write_waited_on_says_whether_the_store_kept_it()
    {
        await using (var refused = BusLog.Over(new ThrowingStore(), Options()))
        {
            Assert.False(await refused.AppendKeptAsync(Plain("run-1")));
        }

        var counting = new CountingStore();
        await using (var kept = BusLog.Over(counting, Options()))
        {
            Assert.True(await kept.AppendKeptAsync(Plain("run-1")));
            Assert.Equal(1, counting.Writes);
        }

        await using var refusing = BusLog.Over(new CountingStore(), Options());
        Assert.False(await refusing.AppendKeptAsync(Plain("")));
    }

    /// <summary>🔴 A busy store's batch is kept after a later try and not kept past the tries.</summary>
    [Fact]
    public async Task A_write_waited_on_that_a_busy_store_put_back_is_kept_after_a_later_try_and_not_kept_past_the_tries()
    {
        var clock = new MovedClock();
        var store = new CountingStore { Busy = 1 };
        await using (var bus = BusLog.Over(store, new BusLogOptions { Clock = clock }))
        {
            var waiting = bus.AppendKeptAsync(Commit("run-1"));
            await Poll.Until(() => store.Refused == 1, () => $"{store.Refused} refusals");
            Assert.False(waiting.IsCompleted);
            Assert.True(await Advancing(clock, waiting));
            Assert.Equal(1, store.Writes);
            Assert.Equal(1, store.Refused);
        }

        var stuck = new CountingStore { Busy = int.MaxValue };
        var later = new MovedClock();
        await using var lost = BusLog.Over(stuck, new BusLogOptions { Clock = later });
        Assert.False(await Advancing(later, lost.AppendKeptAsync(Commit("run-1"))));
        Assert.Equal(BusLog.MostRetries + 1, stuck.Refused);
        Assert.Equal(1, lost.Tally.Missed.GetValueOrDefault(BusMisses.Store));
    }

    /// <summary>🔴 A caller whose wait ran out withdraws its entry before any write takes it, so a store freed later never writes it.</summary>
    [Fact]
    public async Task A_write_waited_on_past_its_bound_is_withdrawn_and_never_written_later()
    {
        var clock = new MovedClock();
        var store = new CountingStore { Busy = 1 };
        await using var bus = BusLog.Over(store, new BusLogOptions { Clock = clock, KeptBound = TimeSpan.FromMilliseconds(50) });

        Assert.False(await bus.AppendKeptAsync(Commit("run-1")));
        Assert.Equal(1, store.Refused);

        for (var pause = 0; pause < 3; pause++) clock.Advance(BusLog.RetryAfter);
        await bus.FlushAsync();
        Assert.Equal(0, store.Writes);
    }

    /// <summary>The clock moved a pause at a time until <paramref name="waiting"/> is told.</summary>
    private static async Task<bool> Advancing(MovedClock clock, Task<bool> waiting)
    {
        await Poll.Until(() =>
        {
            clock.Advance(BusLog.RetryAfter);
            return waiting.IsCompleted;
        }, () => "the write's fate was never told");
        return await waiting;
    }

    /// <summary>🔴 A write the loop took before the caller's own flush is still told to the caller: the answer is the entry's own fate.</summary>
    [Fact]
    public async Task A_write_waited_on_is_told_its_own_fate_when_the_loop_wrote_it()
    {
        var store = new ThrowingStore();
        await using var bus = BusLog.Over(store, Options());
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(each => bus.AppendKeptAsync(Commit("run-1", each))));
        Assert.All(results, Assert.False);
    }

    /// <summary>
    /// 🔴 A value its field's shape cannot carry is dropped at emit, the fact and the batch beside it written: a number that is not
    /// finite, text that is not well-formed UTF-16, whitespace, a path, an e-mail or a sentence where an id is declared; an envelope whose
    /// subject is no id is refused whole.
    /// </summary>
    [Fact]
    public async Task A_value_its_shape_cannot_carry_is_dropped_at_emit_and_the_batch_is_written()
    {
        var store = new CountingStore();
        await using var bus = BusLog.Over(store, Options());

        object?[] misfits =
        [
            "a\uD800b", "\uDC00", @"D:\work\x", "d:/work/x", "/srv/x", @"\\server\share", "src/Daoris.Cli/test/version.test.ts", "../up",
            "someone@example.com", "the check failed because", "two\twords", "https://host/x", 2.5, 3L,
        ];
        foreach (var value in misfits)
        {
            Assert.False(BusShapes.Fits(BusShapes.Id, value), $"{value} fits an id");
            Assert.True(bus.Append(Plain("run-1") with { Payload = [("repository", value)] }), $"{value} refused the fact");
        }

        Assert.False(BusShapes.Fits(BusShapes.Number, double.NaN));
        Assert.False(BusShapes.Fits(BusShapes.Number, double.PositiveInfinity));
        Assert.False(BusShapes.Fits(BusShapes.Count, 2.5));
        Assert.Equal(misfits.Length, bus.Tally.Dropped[BusDrops.Shape]);
        Assert.False(bus.Append(Plain("run 1")), "a subject with a space was taken");
        Assert.Equal(1, bus.Tally.Missed[BusMisses.Shape]);

        string[] ids = ["check.started/1", "driver/checks", "web|ask.spec", "inv-1|0123456789abcdef", "Daoris.Desktop.Driver.Tests|BusLogTests", "", "v-0000000000000001"];
        foreach (var value in ids) Assert.True(BusShapes.Fits(BusShapes.Id, value), $"{value} does not fit an id");
        Assert.True(BusShapes.Fits(BusShapes.Number, 2.5));
        Assert.True(bus.Append(Commit("run-1") with { Payload = [("invocation", ids[3]), ("redCount", 2L), ("red", (IReadOnlyList<string>)[.. ids])] }));
        await bus.FlushAsync();
        Assert.Equal(1, store.Writes);
        var written = store.All.SelectMany(batch => batch.Entries).ToList();
        Assert.Equal(misfits.Length + 1, written.Count);
        Assert.All(written.Take(misfits.Length), entry => Assert.Contains(entry.Payload, pair => pair is { Key: "repository", Value: null }));

        // A red class named by its test file's path is kept by its suite and the file's name.
        var send = new VerifySend("run-7", "inv-1", "Engine", "c:/x", "0123456789abcdef", "none");
        var settled = CheckBusFacts.CheckSettled(send, new VerifyResult("inv-1", send.Commit, VerifyVerdicts.Failed, "red", [])
        {
            Red = ["cli|src/Daoris.Cli/test/version.test.ts", "Daoris.Desktop.Driver.Tests|BusLogTests"],
        });
        Assert.Equal(["cli|version.test.ts", "Daoris.Desktop.Driver.Tests|BusLogTests"], (IReadOnlyList<string>)settled.Payload.Single(pair => pair.Key == "red").Value!);
        Assert.True(bus.Append(settled));
    }

    /// <summary>
    /// 🔴 The close holds: a second close waits for the first's write; a loop that failed never makes the close throw, its failure
    /// counted and said, and what waited is written; an added cause never takes the causes past their bound; and the history reset
    /// keeps the measures, which are evidence.
    /// </summary>
    [Fact]
    public async Task The_close_waits_once_survives_a_failed_loop_and_the_reset_keeps_the_measures()
    {
        var store = new CountingStore { Held = new ManualResetEventSlim(false) };
        var bus = BusLog.Over(store, Options());
        Assert.True(bus.Append(Plain("run-1")));
        var first = bus.DisposeAsync().AsTask();
        await Poll.Until(() => store.Entered == 1, () => "the close never wrote");
        var second = bus.DisposeAsync().AsTask();
        Assert.False(second.IsCompleted, "a second close ended before the first's write");
        store.Held.Set();
        await Task.WhenAll(first, second);
        Assert.Equal(1, store.Writes);

        var failing = new CountingStore();
        var timerless = new TimerlessClock();
        var failed = BusLog.Over(failing, new BusLogOptions { Clock = timerless });
        Assert.True(failed.Append(Plain("run-1")));
        await Poll.Until(() => timerless.Asked, () => "the loop never waited");
        await failed.DisposeAsync();
        Assert.Equal(1, failed.Tally.Missed[BusMisses.Loop]);
        Assert.Equal(1, failing.Writes);

        var listed = new CountingStore();
        await using (var causes = BusLog.Over(listed, Options()))
        {
            Assert.True(causes.Append(Plain("run-1") with { Key = "first" }));
            Assert.True(causes.Append(Plain("run-1", 1) with { Causes = [.. Enumerable.Range(0, BusShapes.MostListed).Select(n => $"c-{n}")], After = ["first"] }));
        }

        Assert.Equal(BusShapes.MostListed, listed.Batches.SelectMany(batch => batch.Entries).Last().Causes.Count);

        Assert.Contains("bus_measures", HomeHistory.DesktopKept);
        Assert.DoesNotContain("bus_measures", HomeHistory.DesktopTables);
        Assert.Contains("bus_log", HomeHistory.DesktopTables);
    }

    /// <summary>A clock whose timers cannot be made: the writer's loop fails at its first wait.</summary>
    private sealed class TimerlessClock : TimeProvider
    {
        private int _asked;

        public bool Asked => Volatile.Read(ref _asked) > 0;

        public override DateTimeOffset GetUtcNow() => new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Increment(ref _asked);
            throw new InvalidOperationException("this clock makes no timer");
        }
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
            [BusTypes.OpinionAsked.Kind, BusTypes.CheckStarted.Kind, BusTypes.CheckFailed.Kind, BusTypes.OpinionAnswered.Kind],
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
        Assert.Contains("rows ", said, StringComparison.Ordinal);
        Assert.Contains("disk ", said, StringComparison.Ordinal);
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
        public BusWritten Write(BusBatch batch) => throw new IOException("the log's store cannot be written");

        public IReadOnlyList<BusEnvelope> Read(long after, int limit) => [];

        public IReadOnlyList<BusMeasureRow> Measures(string fromDay) => [];

        public IReadOnlyList<BusTimer> Timers(DateTimeOffset? by, int limit) => [];

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

        /// <summary>Every write, the measures' alone among them.</summary>
        public IReadOnlyList<BusBatch> All
        {
            get
            {
                lock (_gate) return [.. Batches];
            }
        }

        public int Writes
        {
            get
            {
                lock (_gate) return Batches.Count(batch => batch.Entries.Count > 0);
            }
        }

        public int Entered => Volatile.Read(ref _entered);

        /// <summary>How many writes are refused as busy before one is taken.</summary>
        public int Busy { get; init; }

        public int Refused => Volatile.Read(ref _refused);

        private int _refused;

        public BusWritten Write(BusBatch batch)
        {
            batch.AskWriter();
            if (Volatile.Read(ref _refused) < Busy)
            {
                Interlocked.Increment(ref _refused);
                throw new BusStoreBusyException(new IOException("the store's lock is held"));
            }

            if (batch.Entries.Count > 0) Interlocked.Increment(ref _entered);
            Held?.Wait(TimeSpan.FromMinutes(1));
            lock (_gate) Batches.Add(batch);
            return new BusWritten(batch.Entries.Count * 100L, 0);
        }

        public IReadOnlyList<BusEnvelope> Read(long after, int limit) => [];

        public IReadOnlyList<BusMeasureRow> Measures(string fromDay) => [];

        public IReadOnlyList<BusTimer> Timers(DateTimeOffset? by, int limit) => [];

        public void Dispose() => Held?.Dispose();
    }
}
