using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The bus's durable queue : a consumer woken by each write, its entries ordered per subject and parallel across
/// subjects, acknowledged after handling with its cursor and the timers it ends, resumed from its cursor at a restart (at least once), a
/// throw retried with backoff and handed past its budget, a second claim fencing the first; frames at most 60 a second through an ordered
/// middleware chain that passes, drops and survives a throw; and the opinion's gate as its first consumer. Homes under <c>_fixtures/</c>.
/// </summary>
public sealed class BusQueueTests : IDisposable
{
    private static readonly TimeSpan Soon = Poll.Patience;

    private readonly string _home = Fixture();

    private static string Fixture()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "bus-queue", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        return home;
    }

    public void Dispose() => GitSeed.Discard(_home);

    private BusLog Open(string? home = null) =>
        BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = TimeProvider.System, Interval = TimeSpan.FromMilliseconds(20), Home = home });

    private static readonly BusQueueOptions Quick = new() { Clock = TimeProvider.System, RetryFirst = TimeSpan.FromMilliseconds(20), RetryMost = TimeSpan.FromMilliseconds(60) };

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

    private static readonly Dictionary<string, string> Passes = new(StringComparer.Ordinal);

    private static BusDraft Asked(string opinion, string session = "s-1") =>
        OpinionBusFacts.OfLanding(OpinionLines.Asked(opinion, session, "landing", "Codex", "codex", OpinionViews.FirstPass), Passes)!;

    private static BusDraft Given(string opinion, int must) =>
        OpinionBusFacts.OfLanding(new LandingLine("opinion.given", [("opinion", opinion), ("must", must), ("should", 0), ("note", 0), ("seconds", 60L), ("tier", "agent")]), Passes)!;

    private static BusDraft Answered(string opinion) =>
        OpinionBusFacts.OfLanding(OpinionLines.Answered(opinion, new OpinionReading("s-1", null, DateTimeOffset.UnixEpoch, [])), Passes)!;

    /// <summary>
    /// 🔴 Woken by the write (no polling), each subject's entries go one at a time in order while another subject goes on: a slow handler
    /// holds its own subject alone. Each acknowledgement moves the cursor in the store.
    /// </summary>
    [Fact]
    public async Task Entries_go_in_order_per_subject_and_a_slow_subject_holds_no_other()
    {
        var slow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new Recording("ordered", entry => entry.Subject == "op-slow" && entry.Kind == BusTypes.OpinionAsked.Kind ? slow.Task : Task.CompletedTask);
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

        bus.Append(Asked("op-slow"));
        bus.Append(Given("op-slow", 1));
        bus.Append(Asked("op-fast"));
        bus.Append(Given("op-fast", 0));
        await Poll.Until(() => consumer.Of("op-fast").Count == 2, () => $"op-fast saw {consumer.Of("op-fast").Count}", Soon);
        Assert.Equal([BusTypes.OpinionAsked.Kind], consumer.Of("op-slow"));

        slow.SetResult();
        await Poll.Until(() => consumer.Of("op-slow").Count == 2, () => $"op-slow saw {consumer.Of("op-slow").Count}", Soon);
        Assert.Equal([BusTypes.OpinionAsked.Kind, BusTypes.OpinionGiven.Kind], consumer.Of("op-slow"));
        var last = Logged().Max(entry => entry.Seq);
        await Poll.Until(() => queue.Store.Consumer("ordered")?.Cursor == last, () => $"cursor {queue.Store.Consumer("ordered")}", Soon);
    }

    /// <summary>
    /// 🔴 The gate's acknowledgement ends what is owed it: an owed opinion and answers judged, and a clean pass; a pass with findings stays
    /// armed until its delivery, and a pass asked of a reviewer is not the gate's to end.
    /// </summary>
    [Fact]
    public async Task The_gates_acknowledgement_ends_its_timers_and_a_given_with_findings_waits_for_its_delivery()
    {
        var consumer = new Recording(OpinionGateConsumer.Name, _ => Task.CompletedTask) { Ends = OpinionGateConsumer.Ends };
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

        bus.Append(OpinionBusFacts.OpinionOwed("s-1", "q-1", "landing", "due"));
        bus.Append(Asked("op-asked"));
        bus.Append(Given("op-clean", 0));
        bus.Append(Given("op-found", 2));
        bus.Append(Answered("op-answered"));
        await Poll.Until(() => consumer.Seen.Count == 5, () => $"saw {consumer.Seen.Count}", Soon);
        await Poll.Until(() => Armed().Count == 2, () => string.Join(", ", Armed().Select(timer => $"{timer.Kind} {timer.Subject}")), Soon);

        Assert.Equal(
            [(BusTypes.OpinionAsked.Kind, "op-asked"), (BusTypes.OpinionGiven.Kind, "op-found")],
            Armed().Select(timer => (timer.Kind, timer.Subject)).OrderBy(pair => pair.Kind, StringComparer.Ordinal));
    }

    /// <summary>
    /// 🔴 A restart resumes each consumer from its cursor: what is acknowledged is not handed again, what is written while it is away is,
    /// and an entry whose handling a kill cut before its acknowledgement is handed again (at least once).
    /// </summary>
    [Fact]
    public async Task A_restart_resumes_from_the_cursor_and_hands_again_what_a_kill_cut_before_its_acknowledgement()
    {
        await using var bus = Open();
        var first = new Recording("resumed", entry => entry.Subject == "op-cut" ? Task.Delay(Timeout.Infinite) : Task.CompletedTask);
        var queue = await BusQueue.StartAsync(bus, [first], Quick);
        bus.Append(Asked("op-1"));
        await Poll.Until(() => first.Seen.Count == 1, () => "op-1 was not handed", Soon);
        await Poll.Until(() => queue.Store.Consumer("resumed")?.Cursor > 0, () => "op-1 was not acknowledged", Soon);
        bus.Append(Asked("op-cut"));
        await Poll.Until(() => first.Of("op-cut").Count == 1, () => "op-cut was not handed", Soon);
        await queue.DisposeAsync();

        bus.Append(Asked("op-away"));
        await bus.FlushAsync();
        var second = new Recording("resumed", _ => Task.CompletedTask);
        await using var again = await BusQueue.StartAsync(bus, [second], Quick);
        await Poll.Until(() => second.Seen.Count == 2, () => string.Join(", ", second.Seen.Select(entry => entry.Subject)), Soon);
        // Two subjects, handled in parallel: the cut one again (at least once) and the one written while away, never op-1.
        Assert.Equal(["op-away", "op-cut"], second.Seen.Select(entry => entry.Subject).Order(StringComparer.Ordinal));
        Assert.Equal(2, again.Store.Consumer("resumed")!.Value.Epoch);
    }

    /// <summary>
    /// 🔴 A consumer that throws is tried again with backoff on that subject alone, the others going on; past its type's retries the queue
    /// appends the give-up it hands to the holder chain, and the subject goes on to its next entry.
    /// </summary>
    [Fact]
    public async Task A_throwing_consumer_is_retried_then_handed_past_its_budget_and_its_subject_goes_on()
    {
        var tries = 0;
        var consumer = new Recording("throwing", entry =>
        {
            if (entry.Subject == "op-bad" && entry.Kind == BusTypes.OpinionAsked.Kind)
            {
                Interlocked.Increment(ref tries);
                throw new IOException("the service did not answer");
            }

            return Task.CompletedTask;
        });
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

        bus.Append(Asked("op-bad"));
        bus.Append(Given("op-bad", 1));
        bus.Append(Asked("op-ok"));
        await Poll.Until(() => consumer.Of("op-ok").Count == 1, () => "op-ok waited behind op-bad", Soon);
        await Poll.Until(() => consumer.Of("op-bad").Count == 1 + BusTypes.OpinionAsked.Retries + 1, () => $"op-bad tried {consumer.Of("op-bad").Count}", Soon);
        await bus.FlushAsync();
        var gaveUp = Assert.Single(Logged(), entry => entry.Kind == BusTypes.GiveUp.Kind);
        Assert.Equal("op-bad", gaveUp.Subject);
        Assert.Equal(BusTypes.OpinionAsked.Retries + 1, Volatile.Read(ref tries));
        Assert.Contains(("consumer", (object?)"throwing"), gaveUp.Payload);
        Assert.Equal(BusTypes.OpinionGiven.Kind, consumer.Of("op-bad")[^1]);
    }

    /// <summary>🔴 A handling that throws <see cref="BusEntryNotKeptException"/> is handed again however often, and the queue gives up no entry for it.</summary>
    [Fact]
    public async Task A_row_handed_again_for_one_entrys_refused_retry_is_never_given_up_by_the_queue()
    {
        var tries = 0;
        var consumer = new Recording("refusing", entry =>
        {
            if (entry.Subject != "op-bad" || entry.Kind != BusTypes.OpinionAsked.Kind) return Task.CompletedTask;
            return Interlocked.Increment(ref tries) <= BusTypes.OpinionAsked.Retries + 3 ? throw new BusEntryNotKeptException("another", tries) : Task.CompletedTask;
        });
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

        bus.Append(Asked("op-bad"));
        await Poll.Until(() => Volatile.Read(ref tries) == BusTypes.OpinionAsked.Retries + 4, () => $"op-bad tried {tries}", Soon);
        await bus.FlushAsync();
        await Poll.Until(() => queue.Store.Consumer("refusing") is { Parked: 0 }, () => "the entry was not handled at last: " + queue.Store.Consumer("refusing"), Soon);
        Assert.DoesNotContain(Logged(), entry => entry.Kind == BusTypes.GiveUp.Kind);
    }

    /// <summary>🔴 A second claim raises the epoch: the first run is fenced off and executes nothing more, so two never both execute it.</summary>
    [Fact]
    public async Task A_second_claim_fences_the_first_so_two_never_both_execute()
    {
        await using var bus = Open();
        var older = new Recording("fenced", _ => Task.CompletedTask);
        await using var first = await BusQueue.StartAsync(bus, [older], Quick);
        var newer = new Recording("fenced", _ => Task.CompletedTask);
        await using var second = await BusQueue.StartAsync(bus, [newer], Quick);

        bus.Append(Asked("op-1"));
        await Poll.Until(() => newer.Seen.Count == 1, () => "the newer claim was not handed op-1", Soon);
        await Poll.Until(() => first.Run("fenced")!.Fenced, () => "the older claim was not fenced", Soon);
        Assert.Empty(older.Seen);
        Assert.Throws<BusFencedException>(() => first.Store.Acknowledge(new BusAck("fenced", 1, 99, Logged()[0])));
    }

    /// <summary>
    /// 🔴 The middleware runs in its registered order; one that throws is counted and the frame goes on as handed; what one
    /// drops is counted and never reaches a consumer.
    /// </summary>
    [Fact]
    public async Task The_middleware_runs_in_order_a_throw_passes_the_frame_on_and_a_drop_is_counted()
    {
        var order = new ConcurrentQueue<string>();
        IReadOnlyList<IBusMiddleware> chain =
        [
            new Step("first", (frame, next) => { order.Enqueue("first"); return next(frame); }),
            new Step("boom", (_, _) => throw new InvalidOperationException("a middleware's fault")),
            new Step("drop", (frame, next) => next(frame.Where(delivery => delivery.Entry.Kind != BusTypes.OpinionAsked.Kind))),
            new Step("last", (frame, next) => { order.Enqueue("last"); return next(frame); }),
        ];
        var consumer = new Recording("chained", _ => Task.CompletedTask);
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick, chain);

        bus.Append(Asked("op-1"));
        bus.Append(Given("op-1", 0));
        await Poll.Until(() => consumer.Seen.Count == 1, () => $"saw {consumer.Seen.Count}", Soon);
        Assert.Equal(BusTypes.OpinionGiven.Kind, Assert.Single(consumer.Seen).Kind);
        Assert.Equal(["first", "last"], order.Take(2));
        await bus.FlushAsync();
        using var store = new DesktopBusLogStore(_home);
        var measures = store.Measures(DesktopBusLogStore.Day(DateTimeOffset.UtcNow.AddDays(-1)));
        Assert.Contains(measures, row => row is { Measure: "middleware", Name: "boom:threw" });
        Assert.Contains(measures, row => row is { Measure: "middleware", Name: "drop:dropped" } && row.Total >= 1);
    }

    /// <summary>🔴 Each acknowledgement measures its entry's handling time, append to acknowledgement, by kind and day.</summary>
    [Fact]
    public async Task An_acknowledgement_measures_the_entrys_handling_time_by_kind()
    {
        var consumer = new Recording("timed", _ => Task.CompletedTask);
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [consumer], Quick);

        bus.Append(Asked("op-1"));
        bus.Append(Given("op-1", 0));
        await Poll.Until(() => consumer.Seen.Count == 2, () => $"saw {consumer.Seen.Count}", Soon);
        var last = Logged().Max(entry => entry.Seq);
        await Poll.Until(() => queue.Store.Consumer("timed")?.Cursor == last, () => $"cursor {queue.Store.Consumer("timed")}", Soon);
        await bus.FlushAsync();
        using var store = new DesktopBusLogStore(_home);
        var measures = store.Measures("2000-01-01");
        var given = Assert.Single(measures, row => row is { Measure: "handled" } && row.Name == BusTypes.OpinionGiven.Kind);
        Assert.Equal(1, given.Count);
        Assert.InRange(given.Total, 0, 60_000);
    }

    /// <summary>🔴 A burst of writes is handed in frames at most 60 a second, none lost, in order.</summary>
    [Fact]
    public async Task A_burst_is_handed_in_frames_at_most_sixty_a_second_none_lost()
    {
        var frames = new ConcurrentQueue<long>();
        var consumer = new Recording("framed", _ => Task.CompletedTask);
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(
            bus, [consumer], Quick, [new Step("clock", (frame, next) => { frames.Enqueue(Stopwatch.GetTimestamp()); return next(frame); })]);

        for (var n = 0; n < 300; n++)
        {
            bus.Append(OpinionBusFacts.OpinionOwed(string.Create(CultureInfo.InvariantCulture, $"s-{n % 7}"), null, "landing", "due"));
            if (n % 10 == 0) await Task.Delay(2);
        }

        await Poll.Until(() => consumer.Seen.Count == 300, () => $"saw {consumer.Seen.Count}", Soon);
        // At most 60 a second over the burst's span, one more for the first.
        var at = frames.ToList();
        var span = Stopwatch.GetElapsedTime(at[0], at[^1]);
        Assert.True(at.Count <= (int)(span.TotalSeconds * 60) + 2, $"{at.Count} frames in {span}");

        foreach (var subject in consumer.Seen.GroupBy(entry => entry.Subject))
        {
            Assert.Equal(subject.Select(entry => entry.Seq).Order(), subject.Select(entry => entry.Seq));
        }
    }

    /// <summary>
    /// 🔴 The opinion's gate as the queue's consumer: an owed opinion kept reaches its gate as a fact, and a fixture run's opinion step is
    /// judged within seconds of its <c>opinion.given</c>, not at a look; a start judges every owed opinion once.
    /// </summary>
    [Fact]
    public async Task A_fixture_runs_opinion_step_is_judged_within_seconds_of_its_given_with_no_look()
    {
        var judged = new ConcurrentQueue<(string Subject, long At)>();
        var dues = new OpinionDues(_home);
        dues.Due(new OpinionDue("s-1", "q-1", "engine", "", "tree-1", OpinionRules.Landing, DateTimeOffset.UtcNow));
        using var gate = Gate(judged, _ => new OpinionFact(new Dictionary<string, OpinionSit>(), null, null));
        await using var bus = Open(_home);
        await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
        await Poll.Until(() => judged.Count == 1, () => "the start did not judge the owed opinion", Soon);

        // Kept while the log is open, the owed opinion is teed and judged at once.
        dues.Due(new OpinionDue("s-2", "q-2", "engine", "", "tree-2", OpinionRules.Landing, DateTimeOffset.UtcNow));
        await Poll.Until(() => judged.Any(each => each.Subject == "s-2/landing"), () => "the owed opinion was not judged", Soon);

        var given = Stopwatch.GetTimestamp();
        bus.Append(Given("op-s-1", 1));
        await Poll.Until(() => judged.Count(each => each.Subject == "s-1/landing") == 2, () => "the given was not judged", Soon);
        var took = Stopwatch.GetElapsedTime(given, judged.Last(each => each.Subject == "s-1/landing").At);
        Assert.True(took < TimeSpan.FromSeconds(5), $"the gate judged {took} after the given");
    }

    /// <summary>
    /// 🔴 The seven runs stalled on 2026-11-20, replayed through the gate's consumer across a restart, settle with no look: the two clean
    /// rechecks pass by the policy at their given, the three answered after the restart are judged at their answers, and the two never
    /// answered wait, named, their delivery's timer armed for the watchdog. Nothing owed to the gate is left armed.
    /// </summary>
    [Fact]
    public async Task The_seven_runs_stalled_on_2026_11_20_settle_through_the_consumer_with_no_look()
    {
        var runs = Enumerable.Range(1, 7).ToList();
        string Op(int run, string pass) => string.Create(CultureInfo.InvariantCulture, $"op-{run}-{pass}");
        var dues = new OpinionDues(_home);
        foreach (var run in runs) dues.Due(new OpinionDue($"s-{run}", $"q-{run}", "engine", "", $"tree-{run}", OpinionRules.Landing, DateTimeOffset.UtcNow));
        var judged = new ConcurrentQueue<(string Subject, long At)>();
        var ends = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        // The gate's rule as it read each run's record that day: a clean recheck passes and closes, answers are judged, else it waits.
        OpinionFact? Rule(OpinionDue due)
        {
            var run = int.Parse(due.Session[2..], CultureInfo.InvariantCulture);
            var facts = Logged().Where(entry => entry.Subject.StartsWith($"op-{run}-", StringComparison.Ordinal)).ToList();
            if (facts.Any(entry => entry.Subject == Op(run, "b") && entry.Kind == BusTypes.OpinionGiven.Kind && OpinionGateConsumer.Ends(entry)))
            {
                ends[due.Session] = "passed";
                dues.Close(due.Session, due.Occasion, "passed", DateTimeOffset.UtcNow);
                return null;
            }

            ends[due.Session] = facts.Any(entry => entry.Subject == Op(run, "b") && entry.Kind == BusTypes.OpinionAnswered.Kind) ? "judged" : "waiting";
            return new OpinionFact(new Dictionary<string, OpinionSit>(), null, null);
        }

        async Task Settle(BusLog bus, Func<bool> done) => await Poll.Until(done, () => string.Join(", ", ends.Select(pair => $"{pair.Key}={pair.Value}")), Soon);

        await using (var bus = Open(_home))
        {
            using var gate = Gate(judged, Rule);
            await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
            foreach (var run in runs)
            {
                bus.AppendAll([Asked(Op(run, "a"), $"s-{run}"), Given(Op(run, "a"), 1), OpinionBusFacts.OpinionDelivered(Op(run, "a"), $"s-{run}", 1, "claims"), Answered(Op(run, "a"))]);
                bus.AppendAll([Asked(Op(run, "b"), $"s-{run}"), Given(Op(run, "b"), run <= 2 ? 0 : 1)]);
                if (run > 2) bus.Append(OpinionBusFacts.OpinionDelivered(Op(run, "b"), $"s-{run}", 1, "claims"));
            }

            await Settle(bus, () => ends.Count(pair => pair.Value == "passed") == 2 && ends.Count(pair => pair.Value == "waiting") == 5);
            await bus.FlushAsync();
        }

        // The restart: the gate resumes from its cursor, answers come for three runs, never for two.
        await using (var bus = Open(_home))
        {
            using var gate = Gate(judged, Rule);
            await using var queue = await BusQueue.StartAsync(bus, [gate], Quick);
            foreach (var run in runs.Where(run => run is >= 3 and <= 5)) bus.Append(Answered(Op(run, "b")));
            await Settle(bus, () => ends.Count(pair => pair.Value == "judged") == 3);
            var cursor = Logged().Max(entry => entry.Seq);
            await Poll.Until(() => queue.Store.Consumer(OpinionGateConsumer.Name)?.Cursor == cursor, () => "the gate's cursor stood short", Soon);
        }

        Assert.Equal(["passed", "passed", "judged", "judged", "judged", "waiting", "waiting"], runs.Select(run => ends[$"s-{run}"]));
        var armed = Armed();
        Assert.DoesNotContain(armed, timer => timer.Obliged == BusTypes.OpinionGate);
        Assert.Equal(
            ["op-6-b", "op-7-b"],
            armed.Where(timer => timer.Kind == BusTypes.OpinionDelivered.Kind).Select(timer => timer.Subject).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// 🔴 A word to a harness goes once by its effect key: a crash between its begin and its end reads uncertain and it is never sent again;
    /// a send that throws reads failed, its key let go, so a later try sends it.
    /// </summary>
    [Fact]
    public async Task No_word_is_sent_twice_across_a_crash()
    {
        var journal = new ActJournal(_home, () => StoreSettled.Yes);
        var sent = 0;
        Task<bool> Send()
        {
            Interlocked.Increment(ref sent);
            return Task.FromResult(true);
        }

        // Crashed after the begin, before the end: nothing in the process ended it.
        Assert.Equal(ActEffects.Fresh, journal.BeginEffect("opinion-word:op-1:s-1", "opinion-word"));
        var (before, _) = await OnceWords.SendAsync(journal, "opinion-word:op-1:s-1", "opinion-word", Send, went => went);
        Assert.Equal((ActEffects.Uncertain, 0), (before, sent));

        // A send that threw in the process's hands failed: its key is free again, and the next try goes.
        await Assert.ThrowsAsync<IOException>(() => OnceWords.SendAsync<bool>(journal, "opinion-word:op-4:s-1", "opinion-word", () => throw new IOException("refused"), went => went));
        Assert.Equal(ActEffects.Fresh, (await OnceWords.SendAsync(journal, "opinion-word:op-4:s-1", "opinion-word", Send, went => went)).Before);

        // One that went out is done; one refused lets its key go, so a later try goes.
        Assert.Equal(ActEffects.Fresh, (await OnceWords.SendAsync(journal, "opinion-word:op-2:s-1", "opinion-word", Send, went => went)).Before);
        Assert.Equal(ActEffects.Done, (await OnceWords.SendAsync(journal, "opinion-word:op-2:s-1", "opinion-word", Send, went => went)).Before);
        Assert.Equal(ActEffects.Fresh, (await OnceWords.SendAsync(journal, "opinion-word:op-3:s-1", "opinion-word", Send, _ => false)).Before);
        Assert.Equal(ActEffects.Fresh, (await OnceWords.SendAsync(journal, "opinion-word:op-3:s-1", "opinion-word", Send, went => went)).Before);
        Assert.Equal(4, sent);
    }

    /// <summary>The gate's consumer over the home's owed opinions, its rule a stand-in, each opinion's record naming its run's session.</summary>
    private OpinionGateConsumer Gate(ConcurrentQueue<(string Subject, long At)> judged, Func<OpinionDue, OpinionFact?> rule) => new(
        (due, _, _) =>
        {
            judged.Enqueue((OpinionGateJudge.Subject(due), Stopwatch.GetTimestamp()));
            return Task.FromResult(rule(due));
        },
        (opinion, _) =>
        {
            var session = opinion.StartsWith("op-s", StringComparison.Ordinal) ? opinion[3..] : "s-" + opinion.Split('-')[1];
            return Task.FromResult<OpinionView?>(new OpinionView(opinion, OpinionRules.Landing, "first", session, session, "engine", "base", "tip", "codex", "Codex", OpinionViews.GivenState));
        },
        () => new OpinionDues(_home).Open(), TimeProvider.System, (_, _) => { });

    private sealed class Recording(string name, Func<BusEnvelope, Task> handle) : IBusConsumer
    {
        public ConcurrentQueue<BusEnvelope> Seen { get; } = new();

        public Func<BusEnvelope, bool> Ends { get; init; } = _ => true;

        public BusSubscription Subscription { get; } = BusTypes.Subscribe(name, [.. BusTypes.All.Where(type => type.Name.StartsWith("opinion.", StringComparison.Ordinal)).Select(type => type.Kind)]);

        public string Obliged => BusTypes.OpinionGate;

        public List<string> Of(string subject) => [.. Seen.Where(entry => entry.Subject == subject).Select(entry => entry.Kind)];

        public async Task<BusHandled> HandleAsync(BusEnvelope entry, CancellationToken ct)
        {
            Seen.Enqueue(entry);
            await handle(entry).WaitAsync(ct);
            return Ends(entry) ? BusHandled.Ended : BusHandled.Judged;
        }
    }

    private sealed class Step(string name, Func<BusFrame, Func<BusFrame, ValueTask>, ValueTask> step) : IBusMiddleware
    {
        public string Name => name;

        public ValueTask HandleAsync(BusFrame frame, Func<BusFrame, ValueTask> next, CancellationToken ct) => step(frame, next);
    }
}
