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
