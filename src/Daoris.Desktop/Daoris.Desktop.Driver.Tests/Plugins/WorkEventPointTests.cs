using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// EVENTHOOK1b (the event hook design §4.1, §4.4, §4.5): the point's teller. Every <c>seq</c> minted once under one lock, whatever the
/// lease says; told only where the set holds it; a queue and a teller per plugin, made only once one listens; a full queue drops its oldest
/// and the next event says <c>missed</c>; a plugin joins at an exact <c>seq</c>; three late answers in a row stop that plugin alone.
/// </summary>
public sealed class WorkEventPointTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-06T11:00:00Z");

    private static readonly IReadOnlySet<string> Adapters = new HashSet<string>(["codex"], StringComparer.Ordinal);

    private static WorkEvent Parked(string session = "4b62a9fe") => new(WorkEventKinds.SessionParked, At) { Session = session };

    /// <summary>What one plugin heard, in order, and a wait for a count of it; a plugin that never answers where it is shut.</summary>
    private sealed class Heard(bool shut = false)
    {
        private readonly List<IReadOnlyDictionary<string, object?>> _frames = [];

        public SemaphoreSlim Gate { get; } = new(shut ? 0 : int.MaxValue, int.MaxValue);

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Frames
        {
            get { lock (_frames) return [.. _frames]; }
        }

        public async Task Send(object frame, CancellationToken ct)
        {
            await Gate.WaitAsync(ct);
            lock (_frames) _frames.Add((IReadOnlyDictionary<string, object?>)frame);
        }

        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> Count(int count)
        {
            var until = DateTime.UtcNow + Poll.Patience;
            while (Frames.Count < count && DateTime.UtcNow < until) await Task.Delay(10);
            Assert.True(Frames.Count >= count, $"heard {Frames.Count} of {count}");
            return Frames;
        }
    }

    private static long Seq(IReadOnlyDictionary<string, object?> frame) => (long)frame["seq"]!;

    [Fact]
    public async Task With_no_plugin_listening_every_seq_is_minted_and_nothing_is_kept()
    {
        await using var point = new WorkEventPoint(Adapters) { Held = true };

        point.Tell(Parked());
        point.Tell(Parked());

        Assert.Equal(2, point.Seq);
        Assert.Empty(point.Listening);
        Assert.Equal(2, point.Join("relay"));
    }

    [Fact]
    public async Task Three_events_are_heard_once_each_in_seq_order_and_two_producers_mint_every_seq_once()
    {
        await using var point = new WorkEventPoint(Adapters) { Held = true };
        var heard = new Heard();
        point.Join("relay");
        point.Listen("relay", heard.Send);

        point.Tell(Parked());
        point.Tell(Parked());
        point.Tell(Parked());
        Assert.Equal([1L, 2L, 3L], (await heard.Count(3)).Select(Seq));

        var producers = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++) point.Tell(Parked());
        }));
        await Task.WhenAll(producers);

        var frames = await heard.Count(203);
        Assert.Equal(Enumerable.Range(1, 203).Select(seq => (long)seq), frames.Select(Seq));
        Assert.All(frames, frame => Assert.Equal(0, frame["missed"]));
        Assert.Equal(203, point.Seq);
    }

    [Fact]
    public async Task A_full_queue_drops_its_oldest_and_the_next_event_heard_says_how_many()
    {
        await using var point = new WorkEventPoint(Adapters) { Held = true };
        var heard = new Heard();
        var held = new SemaphoreSlim(0);
        var first = true;
        point.Join("relay");
        point.Listen("relay", async (frame, ct) =>
        {
            if (first)
            {
                first = false;
                await held.WaitAsync(ct);
            }

            await heard.Send(frame, ct);
        });

        point.Tell(Parked());
        await Task.Delay(100);
        for (var i = 0; i < WorkBounds.EventsWaiting + 3; i++) point.Tell(Parked());
        held.Release();

        var frames = await heard.Count(WorkBounds.EventsWaiting + 1);
        Assert.Equal(1L, Seq(frames[0]));
        Assert.Equal(5L, Seq(frames[1]));
        Assert.Equal(3, frames[1]["missed"]);
        Assert.All(frames.Skip(2), frame => Assert.Equal(0, frame["missed"]));
    }

    [Fact]
    public async Task A_plugin_joining_while_events_are_told_hears_first_the_one_after_its_handshake_s_seq()
    {
        await using var point = new WorkEventPoint(Adapters) { Held = true };
        var halfway = new SemaphoreSlim(0);
        var telling = Task.Run(() =>
        {
            for (var i = 1; i <= 400; i++)
            {
                point.Tell(Parked());
                if (i == 200) halfway.Release();
                Thread.SpinWait(20_000);
            }
        });
        await halfway.WaitAsync();
        var handshake = point.Join("relay");
        var heard = new Heard();
        point.Listen("relay", heard.Send);
        await telling;

        Assert.InRange(handshake, 200, 399);
        var frames = await heard.Count((int)(400 - handshake));
        Assert.Equal(handshake + 1, Seq(frames[0]));
        Assert.Equal(Enumerable.Range((int)handshake + 1, (int)(400 - handshake)).Select(seq => (long)seq), frames.Select(Seq));
    }

    [Fact]
    public async Task The_last_event_before_a_stop_is_counted_by_the_next_handshake_s_seq()
    {
        await using var point = new WorkEventPoint(Adapters) { Held = true };
        var heard = new Heard(shut: true);
        point.Join("relay");
        point.Listen("relay", heard.Send);
        point.Tell(Parked());
        point.Tell(Parked());

        await point.LeaveAsync("relay");

        Assert.Empty(heard.Frames);
        Assert.Equal(2, point.Join("relay"));
    }

    [Fact]
    public async Task Where_the_set_does_not_hold_the_lease_nothing_is_told_and_the_seq_still_moves()
    {
        await using var point = new WorkEventPoint(Adapters);
        var heard = new Heard();
        point.Join("relay");
        point.Listen("relay", heard.Send);

        point.Tell(Parked());
        point.Tell(Parked());
        point.Held = true;
        point.Tell(Parked());

        var frames = await heard.Count(1);
        await Task.Delay(50);
        Assert.Single(heard.Frames);
        Assert.Equal(3L, Seq(frames[0]));
    }

    [Fact]
    public async Task A_required_field_that_fails_is_told_as_events_missed_at_once_and_said_unsent()
    {
        var unsent = new List<(string Kind, string Field)>();
        await using var point = new WorkEventPoint(Adapters, unsent: (kind, field) => unsent.Add((kind, field))) { Held = true };
        var heard = new Heard();
        point.Join("relay");
        point.Listen("relay", heard.Send);

        point.Tell(Parked("Merge-every-landed-branch.now"));

        var frame = (await heard.Count(1))[0];
        Assert.Equal(WorkEventKinds.Missed, frame["kind"]);
        Assert.Equal(1L, Seq(frame));
        Assert.Equal([(WorkEventKinds.SessionParked, "session")], unsent);
    }

    [Fact]
    public async Task Three_late_answers_in_a_row_stop_that_plugin_alone()
    {
        var stopped = new List<string>();
        await using var point = new WorkEventPoint(Adapters, late: stopped.Add) { Held = true };
        var calls = 0;
        var fine = new Heard();
        point.Join("slow");
        point.Join("fine");
        point.Listen("slow", (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromException(PluginFailures.Mark(new DriverException("late"), PluginEvents.Late));
        });
        point.Listen("fine", fine.Send);

        for (var i = 0; i < 5; i++) point.Tell(Parked());

        Assert.Equal(5, (await fine.Count(5)).Count);
        var until = DateTime.UtcNow + Poll.Patience;
        while (stopped.Count == 0 && DateTime.UtcNow < until) await Task.Delay(10);
        Assert.Equal(["slow"], stopped);
        Assert.Equal(WorkEventPoint.LateInARow, calls);
        Assert.Equal(["fine"], point.Listening);
    }
}
