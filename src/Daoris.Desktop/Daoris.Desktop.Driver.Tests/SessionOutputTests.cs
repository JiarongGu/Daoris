using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The live console (D49 §2): the transcript capture, teed into a bounded window a person can watch.
/// </summary>
/// <remarks>
/// The file on disk stays the record; this is what makes a running session visible without reading a
/// growing file. Everything here is about being honest under a bound — what was dropped, where the
/// reader is, and when the stream ended — because a console that silently skipped output would be a
/// worse lie than one that showed nothing.
/// </remarks>
public sealed class SessionOutputTests
{
    [Fact]
    public void A_session_that_has_said_nothing_answers_an_empty_tail_rather_than_nothing()
    {
        var tail = new SessionOutput().Tail("never-ran");

        Assert.Empty(tail.Lines);
        Assert.False(tail.Live);
        Assert.Equal(0, tail.Sequence);
    }

    [Fact]
    public void Lines_come_back_in_order_with_their_sequence()
    {
        var output = new SessionOutput();
        output.Append("s1", "first");
        output.Append("s1", "second");

        var tail = output.Tail("s1");

        Assert.Equal(["first", "second"], tail.Lines.Select(l => l.Text));
        Assert.Equal([1, 2], tail.Lines.Select(l => l.Sequence));
        Assert.Equal(2, tail.Sequence);
        Assert.True(tail.Live);
    }

    /// <summary>
    /// The reader says what it has seen and gets the rest. That is what lets a page ask once on open
    /// and then live on events, without either side keeping a cursor for the other.
    /// </summary>
    [Fact]
    public void A_reader_asks_for_what_it_has_not_seen()
    {
        var output = new SessionOutput();
        foreach (var text in new[] { "one", "two", "three" }) output.Append("s1", text);

        var tail = output.Tail("s1", after: 2);

        Assert.Equal(["three"], tail.Lines.Select(l => l.Text));
        Assert.Equal(3, tail.Sequence);
    }

    /// <summary>
    /// The window is bounded and SAYS SO. A console that quietly dropped the middle of a build log
    /// would leave a person reading two halves as though they were continuous.
    /// </summary>
    [Fact]
    public void The_oldest_lines_fall_out_and_the_drop_is_stated()
    {
        var output = new SessionOutput();
        for (var i = 1; i <= SessionOutput.LinesPerSession + 5; i++) output.Append("s1", $"line {i}");

        var tail = output.Tail("s1");

        Assert.Equal(SessionOutput.LinesPerSession, tail.Lines.Count);
        Assert.Equal("line 6", tail.Lines[0].Text);
        Assert.Equal(5, tail.Dropped);
        // The sequence keeps counting through the drop: it identifies a line, never its position.
        Assert.Equal(SessionOutput.LinesPerSession + 5, tail.Sequence);
    }

    [Fact]
    public void Sessions_do_not_share_a_stream()
    {
        var output = new SessionOutput();
        output.Append("s1", "mine");
        output.Append("s2", "theirs");

        Assert.Equal(["mine"], output.Tail("s1").Lines.Select(l => l.Text));
        Assert.Equal(1, output.Tail("s2").Lines.Single().Sequence);
    }

    /// <summary>A closed session keeps its last lines — the end is what someone most wants to read.</summary>
    [Fact]
    public void Closing_ends_the_stream_and_keeps_what_was_said()
    {
        var output = new SessionOutput();
        output.Append("s1", "the last thing it said");

        output.Close("s1");

        var tail = output.Tail("s1");
        Assert.False(tail.Live);
        Assert.Equal(["the last thing it said"], tail.Lines.Select(l => l.Text));
    }

    /// <summary>
    /// Eviction never takes a LIVE session's console. Memory is bounded by retiring the buffers of
    /// sessions that have ended — dropping the output of one somebody is watching would be the wrong
    /// answer to a full table.
    /// </summary>
    [Fact]
    public void Making_room_retires_ended_sessions_and_never_a_live_one()
    {
        var output = new SessionOutput();
        for (var i = 0; i < SessionOutput.SessionsRetained; i++)
        {
            output.Append($"ended-{i}", "done");
            output.Close($"ended-{i}");
        }

        output.Append("live", "still going");

        Assert.Contains("live", output.Buffered);
        Assert.Equal(SessionOutput.SessionsRetained, output.Buffered.Count);
        // The oldest ended one went; the rest stayed.
        Assert.DoesNotContain("ended-0", output.Buffered);
        Assert.Contains($"ended-{SessionOutput.SessionsRetained - 1}", output.Buffered);
    }

    /// <summary>
    /// A subagent or a background task is a stream of its own (CONSOLE2): its own tail, listed under
    /// the session that started it, and nothing of it in the session's.
    /// </summary>
    [Fact]
    public void A_sub_stream_is_its_own_tail_listed_under_its_session()
    {
        var output = new SessionOutput();
        output.Append("s1", "the session");
        output.Open("s1", new SessionStream("task/t1", SessionStreamKind.Task, "dev server"));

        output.Append(SessionOutput.Key("s1", "task/t1"), "listening on 4200");

        Assert.Equal(["the session"], output.Tail("s1").Lines.Select(l => l.Text));
        var tail = output.Tail(SessionOutput.Key("s1", "task/t1"));
        Assert.Equal(["listening on 4200"], tail.Lines.Select(l => l.Text));
        Assert.True(tail.Live);
        var listed = Assert.Single(output.Streams("s1"));
        Assert.Equal(("s1/task/t1", "task", "dev server", true, (string?)null),
            (listed.Key, listed.Kind, listed.Name, listed.Live, listed.State));
        Assert.Empty(output.Streams("s2"));
    }

    /// <summary>
    /// 🔴 The LAST ending is the one kept: the adapter says <c>stopped</c> and then <c>completed</c> in
    /// the same millisecond for a task that finished on its own (CONSOLE2a). And a line that arrives
    /// after the end is kept without calling the stream live again.
    /// </summary>
    [Fact]
    public void A_stream_keeps_its_last_ending_and_a_late_line_does_not_revive_it()
    {
        var output = new SessionOutput();
        output.Open("s1", new SessionStream("task/t1", SessionStreamKind.Task, "ticker"));

        output.End("s1", "task/t1", "stopped");
        output.End("s1", "task/t1", "completed");
        output.Append(SessionOutput.Key("s1", "task/t1"), "[exited with code 0]");

        var listed = Assert.Single(output.Streams("s1"));
        Assert.False(listed.Live);
        Assert.Equal("completed", listed.State);
        var tail = output.Tail(SessionOutput.Key("s1", "task/t1"));
        Assert.False(tail.Live);
        Assert.Equal(["[exited with code 0]"], tail.Lines.Select(l => l.Text));
    }

    /// <summary>A session's end is its streams' end: nothing it started is still talking afterwards.</summary>
    [Fact]
    public void Closing_a_session_closes_its_streams()
    {
        var output = new SessionOutput();
        output.Append("s1", "hello");
        output.Open("s1", new SessionStream("subagent/a1", SessionStreamKind.Subagent, "reader"));

        output.Close("s1");

        Assert.False(Assert.Single(output.Streams("s1")).Live);
        Assert.False(output.Tail(SessionOutput.Key("s1", "subagent/a1")).Live);
    }

    /// <summary>
    /// Streams ride with their session under the bound: they do not count as sessions retained, so a
    /// session that spawned five subagents does not cost five other sessions their consoles, and they
    /// leave when their session is evicted.
    /// </summary>
    [Fact]
    public void Streams_count_with_their_session_and_leave_with_it()
    {
        var output = new SessionOutput();
        output.Append("old", "done");
        output.Open("old", new SessionStream("task/t1", SessionStreamKind.Task, "old task"));
        output.Close("old");
        for (var i = 0; i < SessionOutput.SessionsRetained - 1; i++)
        {
            output.Append($"s{i}", "going");
            for (var n = 0; n < 3; n++) output.Open($"s{i}", new SessionStream($"subagent/{n}", SessionStreamKind.Subagent, $"agent {n}"));
        }

        // Sixteen sessions and forty-six streams: nothing evicted yet.
        Assert.Equal(SessionOutput.SessionsRetained, output.Buffered.Count);
        Assert.Contains("old", output.Buffered);

        output.Append("new", "one more");

        Assert.DoesNotContain("old", output.Buffered);
        Assert.Empty(output.Streams("old"));
        Assert.Empty(output.Tail(SessionOutput.Key("old", "task/t1")).Lines);
    }

    [Fact]
    public void Every_line_is_offered_live_as_it_arrives()
    {
        var output = new SessionOutput();
        var seen = new List<string>();
        output.Lined += (session, line) => seen.Add($"{session}:{line.Sequence}:{line.Text}");

        output.Append("s1", "hello");
        output.Append("s1", "again");

        Assert.Equal(["s1:1:hello", "s1:2:again"], seen);
    }

    /// <summary>
    /// A subscriber's failure is its own. A window that closed mid-line must not be able to stop a
    /// session's capture — the transcript is the durable record and it keeps being written.
    /// </summary>
    [Fact]
    public void A_broken_subscriber_costs_its_own_lines_and_nothing_else()
    {
        var output = new SessionOutput();
        output.Lined += (_, _) => throw new InvalidOperationException("the page went away");

        output.Append("s1", "said anyway");

        Assert.Equal(["said anyway"], output.Tail("s1").Lines.Select(l => l.Text));
    }

    /// <summary>
    /// Two windows, one buffer (SURF8): the monitor and the main window read the same session at
    /// once, each at its own position, and neither moves the other's.
    /// </summary>
    /// <remarks>
    /// This is the property the monitor window is built on and the one D55 §b flagged as unproven —
    /// "SES1's bounded per-session buffer was written for one". It holds because <b>the buffer keeps
    /// no cursor at all</b>: a reader says what it has seen and is told the rest. Asserted rather
    /// than assumed, because a later optimisation that remembered a position would be the natural
    /// way to break it and would break it silently.
    /// </remarks>
    [Fact]
    public void Two_readers_hold_their_own_positions_in_one_session()
    {
        var output = new SessionOutput();
        foreach (var text in new[] { "one", "two", "three" }) output.Append("s1", text);

        // The main window has read everything; the monitor has just opened.
        var caughtUp = output.Tail("s1", after: 3);
        var fresh = output.Tail("s1");

        Assert.Empty(caughtUp.Lines);
        Assert.Equal(["one", "two", "three"], fresh.Lines.Select(l => l.Text));

        output.Append("s1", "four");

        // Both are told about the new line, each from where it actually is.
        Assert.Equal(["four"], output.Tail("s1", after: 3).Lines.Select(l => l.Text));
        Assert.Equal(["four"], output.Tail("s1", after: 3).Lines.Select(l => l.Text));
    }

    /// <summary>
    /// A window opened halfway through is told what it missed — the same number every other reader
    /// is told, because <c>Dropped</c> is a property of the buffer rather than of who is asking.
    /// </summary>
    [Fact]
    public void A_reader_that_joined_late_is_told_the_same_truth_as_one_that_was_there()
    {
        var output = new SessionOutput();
        for (var line = 1; line <= SessionOutput.LinesPerSession + 20; line++)
        {
            output.Append("s1", $"line {line}");
        }

        var early = output.Tail("s1", after: 1);
        var late = output.Tail("s1");

        Assert.Equal(20, early.Dropped);
        Assert.Equal(early.Dropped, late.Dropped);
        // The late reader gets the whole window; the early one gets only what it had not seen, and
        // the window is all that is left of either.
        Assert.Equal(SessionOutput.LinesPerSession, late.Lines.Count);
        Assert.Equal(late.Lines.Count, early.Lines.Count);
    }

    /// <summary>
    /// A window watching several sessions at once — which is what the monitor is — reads each one
    /// independently, because a buffer is per session and a tail names which.
    /// </summary>
    [Fact]
    public void One_window_can_watch_every_live_session_at_once()
    {
        var output = new SessionOutput();
        output.Append("s1", "building");
        output.Append("s2", "testing");
        output.Append("s1", "built");

        Assert.Equal(["building", "built"], output.Tail("s1").Lines.Select(l => l.Text));
        Assert.Equal(["testing"], output.Tail("s2").Lines.Select(l => l.Text));
        Assert.Equal(["s1", "s2"], output.Buffered.Order(StringComparer.Ordinal));
    }
}

/// <summary>
/// The relay (D49 §2): lines out a window at a time, grouped by session. Per-line messages would be
/// correct and unkind — a session can emit thousands in a second, and a bridge carrying one message
/// each turns watching into a stalled window.
/// </summary>
public sealed class ConsoleRelayTests
{
    /// <summary>Flushed explicitly rather than waited for: a test that sleeps is a test that flakes.</summary>
    private static ConsoleRelay Relay(SessionOutput output, List<(string Session, int Count)> batches) =>
        new(output, (session, lines) =>
        {
            batches.Add((session, lines.Count));
            return Task.CompletedTask;
        }, TimeSpan.FromMinutes(5));

    [Fact]
    public async Task A_windows_worth_of_lines_goes_out_as_one_batch_per_session()
    {
        var output = new SessionOutput();
        var batches = new List<(string Session, int Count)>();
        using var relay = Relay(output, batches);

        output.Append("s1", "one");
        output.Append("s1", "two");
        output.Append("s2", "theirs");
        await relay.FlushAsync();

        Assert.Equal([("s1", 2), ("s2", 1)], batches);
    }

    /// <summary>
    /// A second destination on the pump costs the first one nothing (SURF8) — the tee is an event,
    /// not a single delegate, so subscribing does not consume.
    /// </summary>
    /// <remarks>
    /// The shell runs one relay onto an event bus and lets the bus fan out to every attached window,
    /// which is why this is not how the monitor is wired. It is here because the pump's fan-out is
    /// the property the second window rests on, and a change from an event to a handler would look
    /// like a simplification and would quietly make the monitor the only reader.
    /// </remarks>
    [Fact]
    public async Task A_second_destination_on_the_pump_takes_nothing_from_the_first()
    {
        var output = new SessionOutput();
        var main = new List<(string Session, int Count)>();
        var monitor = new List<(string Session, int Count)>();
        using var first = Relay(output, main);
        using var second = Relay(output, monitor);

        output.Append("s1", "one");
        output.Append("s1", "two");
        await first.FlushAsync();
        await second.FlushAsync();

        Assert.Equal([("s1", 2)], main);
        Assert.Equal(main, monitor);
    }

    [Fact]
    public async Task Nothing_said_is_nothing_sent()
    {
        var batches = new List<(string, int)>();
        using var relay = Relay(new SessionOutput(), batches);

        await relay.FlushAsync();

        Assert.Empty(batches);
    }

    /// <summary>
    /// A reader that is not listening is not a failure. A relay that let a closed window take the
    /// capture down with it would be — the transcript is still being written, and the buffer still
    /// holds these lines.
    /// </summary>
    [Fact]
    public async Task A_reader_that_throws_does_not_stop_the_stream()
    {
        var output = new SessionOutput();
        using var relay = new ConsoleRelay(
            output, (_, _) => throw new InvalidOperationException("the window closed"),
            TimeSpan.FromMinutes(5));

        output.Append("s1", "said anyway");
        await relay.FlushAsync();

        Assert.Equal(["said anyway"], output.Tail("s1").Lines.Select(l => l.Text));
    }

    /// <summary>Unsubscribed on dispose: a relay for a window that is gone must not hold the buffer.</summary>
    [Fact]
    public async Task Disposing_stops_the_subscription()
    {
        var output = new SessionOutput();
        var batches = new List<(string, int)>();
        var relay = Relay(output, batches);

        relay.Dispose();
        output.Append("s1", "after the window closed");
        await relay.FlushAsync();

        Assert.Empty(batches);
    }
}

/// <summary>
/// The tee itself (D49 §2): one pump, two destinations. The property worth holding is that a line
/// reaches BOTH — and that the file, the durable copy, is never the one that loses it.
/// </summary>
public sealed class CaptureTeeTests
{
    [Fact]
    public async Task Every_line_reaches_the_transcript_and_the_console()
    {
        var output = new SessionOutput();
        var file = new StringWriter();

        await Daoris.Driver.Driver.PumpAsync(
            new StringReader("first\nsecond\n"), file, "s1", output, CancellationToken.None);

        Assert.Equal(["first", "second"], output.Tail("s1").Lines.Select(l => l.Text));
        Assert.Contains("first", file.ToString());
        Assert.Contains("second", file.ToString());
    }

    /// <summary>
    /// With nobody watching — the headless driver — the transcript is written exactly as before. The
    /// console is an addition, never a dependency.
    /// </summary>
    [Fact]
    public async Task With_no_console_the_transcript_is_still_written()
    {
        var file = new StringWriter();

        await Daoris.Driver.Driver.PumpAsync(
            new StringReader("alone\n"), file, "s1", output: null, CancellationToken.None);

        Assert.Contains("alone", file.ToString());
    }
}
