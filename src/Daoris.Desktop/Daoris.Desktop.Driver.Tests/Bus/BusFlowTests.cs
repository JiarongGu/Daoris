using System.Collections.Concurrent;
using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 The dispatch at the layer, each of the driving area's five rules held once: (1) no wait stands without a wake: a fact heard while its
/// key runs is worked by the next pass at once, with no timer between; (2) an acknowledgement ends an obligation only when the job moved: a move
/// ends the timer, a not-moved is handled with its timer armed; (3) one pass works one state at a time, never nested: the facts waiting on a key
/// become one piece of work for the next pass; (4) work is bounded and a fact names its reach: keys at once, keys within a group, only the keys
/// a fact names; (5) a failure costs one entry: the entry that carried the key's work is parked and tried again, no other key's. Homes under
/// <c>_fixtures/</c>.
/// </summary>
public sealed class BusFlowTests : IDisposable
{
    private static readonly TimeSpan Soon = Poll.Patience;

    private static TimeProvider Clock => OpinionGateMovesTests.Machine;

    private static readonly BusQueueOptions Backoff = new() { Clock = Clock, RetryFirst = TimeSpan.FromMilliseconds(300), RetryMost = TimeSpan.FromMilliseconds(300) };

    private static Task Wait(int milliseconds) => Task.Delay(TimeSpan.FromMilliseconds(milliseconds), Clock);

    private static readonly BusQueueOptions Quick = new() { Clock = Clock, RetryFirst = TimeSpan.FromMilliseconds(20), RetryMost = TimeSpan.FromMilliseconds(60) };

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "bus-flow", Guid.NewGuid().ToString("N")[..8]);

    public BusFlowTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private BusLog Open() =>
        BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = Clock, Interval = TimeSpan.FromMilliseconds(20), Home = _home });

    private IReadOnlyList<BusTimer> Armed()
    {
        using var store = new DesktopBusLogStore(_home);
        return [.. store.Timers(null, 1000).Where(timer => timer.Obliged == BusTypes.OpinionGate)];
    }

    /// <summary>A unit's node begun: keyed by <paramref name="key"/> (the node named first), grouped by the second where one is named.</summary>
    private static BusDraft Begun(string unit, string key, string? group = null) => OpinionBusFacts.NodeBegun(unit, group is null ? [key] : [key, group]);

    [Fact]
    public async Task A_fact_heard_while_its_key_runs_is_worked_by_the_next_pass_at_once()
    {
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var passes = new ConcurrentQueue<string>();
        var probe = new Probe(new BusFlowBounds(4), async (key, work) =>
        {
            passes.Enqueue(string.Join("+", work.Order()));
            if (passes.Count == 1)
            {
                holding.SetResult();
                await release.Task;
            }

            return BusFlowOutcome.Moved;
        });
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [probe], Quick);

        bus.Append(Begun("u-1", "k"));
        await holding.Task.WaitAsync(Soon);
        bus.Append(Begun("u-2", "k"));
        await bus.FlushAsync();
        release.SetResult();

        // No timer stands between the pass and the fact it did not read: the next pass is the fact's own wake.
        await Poll.Until(() => passes.Count == 2, () => "the fact heard during the pass was not worked: " + string.Join(", ", passes), Soon);
        Assert.Equal(["u-1", "u-2"], passes);
        await Unarmed(bus);
    }

    [Fact]
    public async Task A_move_ends_the_timer_and_a_not_moved_is_handled_and_leaves_it_armed()
    {
        var probe = new Probe(new BusFlowBounds(4), (key, _) => Task.FromResult(key == "stays" ? BusFlowOutcome.NotMoved("not ready") : BusFlowOutcome.Moved));
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [probe], Quick);

        bus.Append(Begun("u-1", "ends"));
        bus.Append(Begun("u-2", "stays"));
        await bus.FlushAsync();
        await Poll.Until(() => probe.Moves.Count == 2, () => $"moves {probe.Moves.Count}", Soon);
        var last = queue.Store.Read(0, [BusTypes.NodeBegun.Kind], 100).Max(entry => entry.Seq);
        await Poll.Until(() => queue.Store.Consumer("probe")?.Cursor == last, () => "the cursor did not pass both: " + queue.Store.Consumer("probe"), Soon);

        // Handled both (the cursor passed them), ended one: the stall view keeps the other.
        await Poll.Until(() => Armed().Count == 1, () => string.Join(", ", Armed().Select(timer => $"{timer.Kind} {timer.Subject}")), Soon);
        Assert.Equal("u-2", Armed().Single().Subject);
    }

    [Fact]
    public async Task The_facts_waiting_on_one_key_are_one_piece_of_work_for_one_pass_at_a_time()
    {
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inside = 0;
        var overlap = 0;
        var passes = new ConcurrentQueue<string>();
        var probe = new Probe(new BusFlowBounds(4), async (key, work) =>
        {
            if (Interlocked.Increment(ref inside) > 1) Interlocked.Increment(ref overlap);
            passes.Enqueue(string.Join("+", work.Order()));
            if (passes.Count == 1)
            {
                holding.SetResult();
                await release.Task;
            }

            Interlocked.Decrement(ref inside);
            return BusFlowOutcome.Moved;
        });
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [probe], Quick);

        bus.Append(Begun("u-0", "k"));
        await holding.Task.WaitAsync(Soon);
        // A fact waiting on a key holds no queue slot: every one that waits is merged into the next pass.
        for (var each = 1; each <= 5; each++) bus.Append(Begun($"u-{each}", "k"));
        await bus.FlushAsync();
        await Wait(100);
        release.SetResult();

        await Poll.Until(() => passes.Count == 2, () => "passes: " + string.Join(" | ", passes), Soon);
        await queue.QuietAsync(Soon, CancellationToken.None);
        Assert.Equal(0, Volatile.Read(ref overlap));
        // Five facts that waited cost one pass that reads them all, their names merged, none lost.
        Assert.Equal(["u-0", "u-1+u-2+u-3+u-4+u-5"], passes);
        await Unarmed(bus);
    }

    [Fact]
    public async Task Keys_at_once_and_keys_within_a_group_are_bounded_and_a_fact_moves_only_the_keys_it_names()
    {
        var inside = 0;
        var most = 0;
        var perGroup = new ConcurrentDictionary<string, int>();
        var mostInGroup = new ConcurrentDictionary<string, int>();
        var probe = new Probe(new BusFlowBounds(KeysAtOnce: 3, PerGroup: 1), async (key, _) =>
        {
            var group = key[..1];
            var now = Interlocked.Increment(ref inside);
            var here = perGroup.AddOrUpdate(group, 1, (_, was) => was + 1);
            lock (mostInGroup)
            {
                most = Math.Max(most, now);
                mostInGroup[group] = Math.Max(mostInGroup.GetValueOrDefault(group), here);
            }

            await Wait(15);
            perGroup.AddOrUpdate(group, 0, (_, was) => was - 1);
            Interlocked.Decrement(ref inside);
            return BusFlowOutcome.Moved;
        });
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [probe], Quick);

        // One fact naming twelve keys in three groups (a-, b-, c-): never more than three at once, never two of one group.
        bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames([.. Enumerable.Range(0, 12).Select(each => $"{(char)('a' + (each % 3))}-{each}")], [])));
        await Poll.Until(() => probe.Moves.Count == 12, () => $"moved {probe.Moves.Count}", Soon);
        Assert.InRange(most, 1, 3);
        Assert.All(mostInGroup, pair => Assert.Equal(1, pair.Value));

        // A fact that names one key moves that key alone.
        bus.Append(Begun("u-1", "z-1"));
        await Poll.Until(() => probe.Moves.Count == 13, () => $"moved {probe.Moves.Count}", Soon);
        await queue.QuietAsync(Soon, CancellationToken.None);
        Assert.Equal(13, probe.Moves.Count);
        Assert.Equal("z-1", probe.Moves.Last().Key);
    }

    [Fact]
    public async Task A_failure_costs_its_key_alone_even_on_the_same_subject_and_a_later_fact_naming_a_moved_key_is_worked()
    {
        var fail = 1;
        var probe = new Probe(new BusFlowBounds(4), (key, _) => Task.FromResult(key == "bad" && Interlocked.Exchange(ref fail, 0) == 1 ? BusFlowOutcome.Failed("refused") : BusFlowOutcome.Moved));
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [probe], Backoff);

        // One fact names a key that fails and one that moves.
        bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["bad", "good"], [])));
        await Poll.Until(() => probe.Moves.Count(move => move.Key == "good") == 1, () => "the healthy key did not move", Soon);
        // Facts on the very subject, while the failed key waits its backoff, are worked at once: one names a moved key again, one another.
        bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["good", "next"], [])));
        await Poll.Until(() => probe.Moves.Count(move => move.Key == "good") == 2 && probe.Moves.Any(move => move.Key == "next"), () => "the lane was held by the failure: " + string.Join(", ", probe.Moves.Select(move => move.Key)), Soon);
        Assert.Equal(1, probe.Moves.Count(move => move.Key == "bad"));
        // The failed key is tried again alone, and what it held ends once it moves.
        await Poll.Until(() => probe.Moves.Count(move => move.Key == "bad") == 2, () => "the failed key was not tried again", Soon);
        await Unarmed(bus);
        Assert.Equal(2, probe.Moves.Count(move => move.Key == "good"));
    }

    [Fact]
    public async Task A_row_folded_stays_open_until_its_result_so_a_failure_loses_no_name_a_move_named()
    {
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fail = 1;
        var probe = new Probe(new BusFlowBounds(4), async (key, _) =>
        {
            if (key == "a")
            {
                holding.TrySetResult();
                await release.Task;
            }

            return key == "b" && Interlocked.Exchange(ref fail, 0) == 1 ? BusFlowOutcome.Failed("refused") : BusFlowOutcome.Moved;
        });
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [probe], Quick);

        bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["a"], [])));
        await holding.Task.WaitAsync(Soon);
        // Two moves wait in a row on the one subject while the first is held: handed together, open until the result.
        bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["b"], [])));
        bus.Append(OpinionBusFacts.QuestMoved(["quests"], new QuestMoveNames(["c"], [])));
        await bus.FlushAsync();
        release.SetResult();

        await Poll.Until(() => probe.Moves.Count(move => move.Key == "b") == 2, () => "the failed name was lost: " + string.Join(", ", probe.Moves.Select(move => move.Key)), Soon);
        await Unarmed(bus);
        Assert.Equal(1, probe.Moves.Count(move => move.Key == "c"));
    }

    [Fact]
    public async Task A_pause_lets_the_facts_of_a_burst_become_one_pass()
    {
        var passes = new ConcurrentQueue<string>();
        var probe = new Probe(new BusFlowBounds(4), (key, work) =>
        {
            passes.Enqueue(string.Join("+", work.Order()));
            return Task.FromResult(BusFlowOutcome.Moved);
        }, pause: TimeSpan.FromMilliseconds(300));
        await using var bus = Open();
        await using var queue = await BusQueue.StartAsync(bus, [probe], Quick);

        bus.Append(Begun("u-1", "k"));
        await Wait(100);
        bus.Append(Begun("u-2", "k"));
        await Poll.Until(() => !passes.IsEmpty, () => "no pass", Soon);
        Assert.Equal(["u-1+u-2"], passes);
    }

    private async Task Unarmed(BusLog bus)
    {
        await bus.FlushAsync();
        await Poll.Until(() => Armed().Count == 0, () => "armed: " + string.Join(", ", Armed().Select(timer => $"{timer.Kind} {timer.Subject}")), Soon);
    }

    /// <summary>A consumer of the layer over the opinion's gate's node and quest facts: keyed by the node named first (a quest move, by each quest it names), its work the subjects that named it.</summary>
    private sealed class Probe(BusFlowBounds bounds, Func<string, IReadOnlyList<string>, Task<BusFlowOutcome>> move, TimeSpan pause = default)
        : BusFlowConsumer<IReadOnlyList<string>>(bounds, Clock, pause)
    {
        public ConcurrentQueue<(string Key, DateTimeOffset At)> Moves { get; } = new();

        public override BusSubscription Subscription { get; } = BusTypes.Subscribe("probe", [BusTypes.NodeBegun.Kind, BusTypes.QuestMoved.Kind]);

        public override string Obliged => BusTypes.OpinionGate;

        protected override bool Coalesces(string kind) => kind == BusTypes.QuestMoved.Kind;

        protected override Task<IReadOnlyList<BusFlowPiece<IReadOnlyList<string>>>> PiecesAsync(BusEnvelope entry, CancellationToken ct)
        {
            IReadOnlyList<string> named = entry.Payload.FirstOrDefault(pair => pair.Key == (entry.Kind == BusTypes.NodeBegun.Kind ? "nodes" : "quests")).Value as IReadOnlyList<string> ?? [];
            IReadOnlyList<BusFlowPiece<IReadOnlyList<string>>> pieces = entry.Kind == BusTypes.NodeBegun.Kind
                ? [new(named[0], [entry.Subject], named.Count > 1 ? named[1] : null)]
                : [.. named.Select(key => new BusFlowPiece<IReadOnlyList<string>>(key, [entry.Subject], key[..1]))];
            return Task.FromResult(pieces);
        }

        protected override IReadOnlyList<string> Merge(IReadOnlyList<string> earlier, IReadOnlyList<string> later) => [.. earlier.Union(later)];

        protected override async Task<BusFlowOutcome> MoveAsync(string key, IReadOnlyList<string> work, CancellationToken ct)
        {
            Moves.Enqueue((key, Clock.GetUtcNow()));
            return await move(key, work).ConfigureAwait(false);
        }
    }
}
