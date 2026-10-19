using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The push path's host half (PUSH1a, D182 points 3 and 4): a slice judged once, each partition numbered within the slice's
/// epoch and pushed whole when it changed, a snapshot read from memory, and a manifest of the numbers through the same bus, all
/// told by one judgement at a time so the window receives them in the order they were numbered.
/// </summary>
/// <remarks>Over the kit's real bus, subscribed to as each window's pump is: what is asserted is what a window would be handed.</remarks>
public sealed class StateSlicesTests
{
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);
    private readonly List<EventMessage> _told = [];
    private readonly Dictionary<string, object> _now = new(StringComparer.Ordinal) { ["a"] = new { Count = 1 }, ["b"] = new { Name = "x" } };

    public StateSlicesTests() =>
        _bus.SubscribeToModule(StateSlice.Module, message =>
        {
            lock (_told) _told.Add(message);
            return Task.CompletedTask;
        });

    private StateSlice Slice(TimeSpan? manifestEvery = null, IReadOnlyList<SliceWake>? wakes = null, Action<Exception>? failed = null) =>
        new("test", _bus, _ => Task.FromResult(Judged()), epoch: "e1")
        {
            ManifestEvery = manifestEvery ?? TimeSpan.FromHours(1),
            WakesOn = wakes ?? [],
            Failed = failed,
        };

    private IReadOnlyList<SliceJudged>? Judged()
    {
        lock (_now) return [.. _now.Select(each => new SliceJudged(each.Key, each.Value))];
    }

    private void Now(string partition, object? data)
    {
        lock (_now)
        {
            if (data is null) _now.Remove(partition);
            else _now[partition] = data;
        }
    }

    private List<EventMessage> Told()
    {
        lock (_told) return [.. _told];
    }

    private List<SlicePush> Pushes() => [.. Told().Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)];

    private List<SliceManifest> Manifests() => [.. Told().Where(each => each.Type == StateSlice.ManifestType).Select(each => (SliceManifest)each.Payload!)];

    private static string Versions(SliceSnapshot snapshot) =>
        string.Join(",", snapshot.Partitions.Select(each => $"{each.Partition}@{each.Seq}:{each.Status}"));

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(10);
        }
    }

    [Fact]
    public void Before_its_first_judgement_a_slice_answers_not_judged_from_memory_and_tells_nothing()
    {
        var slice = Slice();

        var snapshot = slice.Snapshot();

        Assert.Equal(("test", "e1", StateSlice.NotJudged), (snapshot.Slice, snapshot.Epoch, snapshot.Status));
        Assert.Empty(snapshot.Partitions);
        Assert.Equal(0, slice.Judgements);
        Assert.Empty(Told());
    }

    [Fact]
    public async Task A_first_judgement_pushes_each_partition_whole_at_its_first_number()
    {
        var slice = Slice();

        await slice.JudgeAsync();

        Assert.Equal(["a@1:ready", "b@1:ready"], Pushes().Select(push => $"{push.Partition}@{push.Seq}:{push.Status}").Order());
        Assert.All(Pushes(), push => Assert.Equal(("test", "e1", (long?)null), (push.Slice, push.Epoch, push.BaseSeq)));
        Assert.Equal("a@1:ready,b@1:ready", Versions(slice.Snapshot()));
        Assert.Equal(StateSlice.Ready, slice.Snapshot().Status);
        Assert.Same(_now["a"], slice.Snapshot().Partitions.Single(each => each.Partition == "a").Data);
    }

    [Fact]
    public async Task An_unchanged_judgement_pushes_nothing()
    {
        var slice = Slice();

        await slice.JudgeAsync();
        Now("a", new { Count = 1 });
        await slice.JudgeAsync();

        Assert.Equal(2, slice.Judgements);
        Assert.Equal(2, Pushes().Count);
        Assert.Equal("a@1:ready,b@1:ready", Versions(slice.Snapshot()));
    }

    [Fact]
    public async Task A_changed_partition_alone_is_pushed_with_its_next_number()
    {
        var slice = Slice();
        await slice.JudgeAsync();

        Now("a", new { Count = 2 });
        await slice.JudgeAsync();

        var last = Pushes()[^1];
        Assert.Equal(3, Pushes().Count);
        Assert.Equal(("a", 2L), (last.Partition, last.Seq));
        Assert.Equal("a@2:ready,b@1:ready", Versions(slice.Snapshot()));
    }

    [Fact]
    public async Task A_partition_no_longer_judged_is_pushed_gone_and_a_return_carries_its_numbers_on()
    {
        var slice = Slice();
        await slice.JudgeAsync();

        Now("b", null);
        await slice.JudgeAsync();

        var gone = Pushes()[^1];
        Assert.Equal(("b", 2L, StateSlice.Gone), (gone.Partition, gone.Seq, gone.Status));
        Assert.Null(gone.Data);
        Assert.Equal("a@1:ready", Versions(slice.Snapshot()));

        Now("b", new { Name = "y" });
        await slice.JudgeAsync();

        Assert.Equal(("b", 3L, StateSlice.Ready), (Pushes()[^1].Partition, Pushes()[^1].Seq, Pushes()[^1].Status));
    }

    [Fact]
    public async Task A_whole_replacement_coalesces_by_its_partition_and_a_manifest_by_its_slice()
    {
        var slice = Slice();

        await slice.JudgeAsync();
        await slice.TellManifestAsync();

        Assert.Equal(["test/a", "test/b"], Told().Where(each => each.Type == StateSlice.PushType).Select(each => each.CoalesceKey).Order());
        Assert.Equal("test", Told().Single(each => each.Type == StateSlice.ManifestType).CoalesceKey);
    }

    [Fact]
    public async Task The_manifest_names_each_partition_s_number_and_follows_the_pushes_it_covers()
    {
        var slice = Slice();

        await slice.JudgeAsync();
        await slice.TellManifestAsync();

        var manifest = Assert.Single(Manifests());
        Assert.Equal(("test", "e1"), (manifest.Slice, manifest.Epoch));
        Assert.Equal(["a@1", "b@1"], manifest.Partitions.Select(each => $"{each.Partition}@{each.Seq}").Order());
        Assert.Equal(StateSlice.ManifestType, Told()[^1].Type);
    }

    [Fact]
    public async Task A_failed_judgement_keeps_what_was_judged_and_pushes_nothing()
    {
        var failures = new List<Exception>();
        var fail = false;
        var slice = new StateSlice("test", _bus, _ => fail ? throw new InvalidOperationException("unreadable") : Task.FromResult(Judged()), "e1")
        {
            Failed = failures.Add,
        };
        await slice.JudgeAsync();

        fail = true;
        await slice.JudgeAsync();
        // Said once while it keeps failing, so a file that stays unreadable is not a line at every look.
        await slice.JudgeAsync();

        Assert.Equal("unreadable", Assert.Single(failures).Message);
        Assert.Equal(2, Pushes().Count);
        Assert.Equal("a@1:ready,b@1:ready", Versions(slice.Snapshot()));

        fail = false;
        await slice.JudgeAsync();
        fail = true;
        await slice.JudgeAsync();
        Assert.Equal(2, failures.Count);
    }

    [Fact]
    public async Task Judgements_made_at_once_tell_each_partition_in_the_order_they_numbered_it()
    {
        var count = 0;
        var slice = new StateSlice("test", _bus, async _ =>
        {
            await Task.Yield();
            return (IReadOnlyList<SliceJudged>)[new SliceJudged("a", new { Count = Interlocked.Increment(ref count) })];
        }, "e1");

        await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Task.Run(() => slice.JudgeAsync())));

        var told = Pushes().Select(push => push.Seq).ToList();
        Assert.Equal(40, told.Count);
        Assert.Equal(told.Order(), told);
    }

    [Fact]
    public async Task A_wake_only_asks_and_the_worker_judges_once_for_the_wakes_that_waited()
    {
        var slice = Slice();
        for (var i = 0; i < 100; i++) slice.Wake();
        Assert.Equal(0, slice.Judgements);

        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        // Its own first judgement, then one for every wake that waited.
        await UntilAsync(() => slice.Judgements == 2);
        await Task.Delay(100);

        Assert.Equal(2, slice.Judgements);
        await stop.CancelAsync();
        await running;
    }

    [Fact]
    public async Task The_worker_judges_on_a_wake_from_the_bus_and_tells_its_manifest_at_its_period_pushing_nothing_unchanged()
    {
        var slice = Slice(TimeSpan.FromMilliseconds(100), [new SliceWake("POKE", payload => payload is "a")]);
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count == 2);

        // Each period reconciles: judged again, and nothing unchanged is pushed.
        await UntilAsync(() => Manifests().Count >= 3);
        Assert.Equal(2, Pushes().Count);

        Now("a", new { Count = 5 });
        _bus.Emit(StateSlice.Module, "POKE", "b");
        _bus.Emit(StateSlice.Module, "POKE", "a");
        await UntilAsync(() => Pushes().Any(push => push is { Partition: "a", Seq: 2 }));
        await UntilAsync(() => Manifests()[^1].Partitions.Any(each => each is { Partition: "a", Seq: 2 }));

        await stop.CancelAsync();
        await running;
    }

    /// <summary>
    /// PUSH1b1: a slice judged from facts that have not landed (the machine's facts, which start after the slices) answers nothing
    /// to judge from yet: it stays not judged, so a window never installs an empty slice as the truth, and pushes nothing.
    /// </summary>
    [Fact]
    public async Task A_judge_with_nothing_to_judge_from_yet_leaves_the_slice_not_judged_and_pushes_nothing()
    {
        var landed = false;
        var slice = new StateSlice("test", _bus, _ => Task.FromResult(landed ? Judged() : null), "e1");

        await slice.JudgeAsync();

        Assert.Equal((StateSlice.NotJudged, 1L), (slice.Snapshot().Status, slice.Judgements));
        Assert.Empty(slice.Snapshot().Partitions);
        Assert.Empty(Told());

        landed = true;
        await slice.JudgeAsync();
        Assert.Equal("a@1:ready,b@1:ready", Versions(slice.Snapshot()));
        Assert.Equal(StateSlice.Ready, slice.Snapshot().Status);

        // Once judged, a judgement with nothing to judge from keeps what was judged: nothing is pushed gone.
        landed = false;
        await slice.JudgeAsync();
        Assert.Equal(2, Pushes().Count);
        Assert.Equal("a@1:ready,b@1:ready", Versions(slice.Snapshot()));
    }

    /// <summary>
    /// PUSH1b1: where a move reaches the slice with no bus event (the machine's facts landing), its owner hands a hearing that wakes
    /// it: heard while the worker runs, and no longer once it stops.
    /// </summary>
    [Fact]
    public async Task A_slice_hears_what_its_owner_hands_it_while_its_worker_runs_and_not_after()
    {
        Action? heard = null;
        var stopped = 0;
        var slice = new StateSlice("test", _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(Judged()), "e1")
        {
            ManifestEvery = TimeSpan.FromHours(1),
            Hears =
            [
                wake =>
                {
                    heard = wake;
                    return new Stop(() => Interlocked.Increment(ref stopped));
                },
            ],
        };
        Assert.Null(heard);

        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => slice.Judgements == 1);
        Now("a", new { Count = 3 });
        heard!();
        await UntilAsync(() => Pushes().Any(push => push is { Partition: "a", Seq: 2 }));

        await stop.CancelAsync();
        await running;
        Assert.Equal(1, stopped);
    }

    private sealed class Stop(Action stop) : IDisposable
    {
        public void Dispose() => stop();
    }

    [Fact]
    public void A_slice_is_found_by_its_name_and_one_not_held_is_none()
    {
        var slice = Slice();
        var slices = new StateSlices(slice);

        Assert.Same(slice, slices.Find("test"));
        Assert.Null(slices.Find("lines"));
    }
}
