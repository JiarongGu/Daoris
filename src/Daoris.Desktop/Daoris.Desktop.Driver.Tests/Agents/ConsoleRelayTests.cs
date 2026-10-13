using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

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
