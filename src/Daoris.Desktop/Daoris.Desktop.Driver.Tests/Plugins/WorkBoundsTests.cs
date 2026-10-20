using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The work wire's bounds and its restart policy (FABRIC-PERFORM1a; the fabric events design §C4): a frame over 64 KB dropped unread, 60
/// events a minute, a refusal's line once a minute with its count, and an unasked exit's back-off of 1, 5 and 15 minutes with three
/// within an hour marking the plugin unsound until the person tries again.
/// </summary>
public sealed class WorkBoundsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private static async Task<List<string?>> ReadAll(string text, int limit = WorkBounds.FrameBytes)
    {
        var lines = new BoundedLines(new StringReader(text), limit);
        var read = new List<string?>();
        while (await lines.ReadAsync(CancellationToken.None) is { } line) read.Add(line.Text);
        return read;
    }

    [Fact]
    public async Task A_frame_over_64_KB_is_dropped_unread_and_the_next_is_read()
    {
        var full = new string('x', WorkBounds.FrameBytes);
        var over = new string('y', WorkBounds.FrameBytes + 1);

        var read = await ReadAll($"a\n{full}\n{over}\nb");

        Assert.Equal(["a", full, null, "b"], read);
    }

    [Fact]
    public async Task The_bound_counts_utf8_bytes_not_characters()
    {
        // 预 is three bytes: 21845 of them are 65535 bytes, 21846 are 65538.
        var under = new string('预', 21845);
        var over = new string('预', 21846);

        Assert.Equal([under, null, "z"], await ReadAll($"{under}\n{over}\nz\n"));
    }

    [Fact]
    public async Task Line_ends_are_either_kind_and_an_empty_line_is_a_line()
    {
        Assert.Equal(["a", "", "b"], await ReadAll("a\r\n\nb\r\n"));
        Assert.Empty(await ReadAll(""));
    }

    /// <summary>
    /// FABRIC-PERFORM1a, round 2: a line ends at LF, CRLF or a bare CR, as <see cref="TextReader.ReadLineAsync()"/> ends one, which the wire
    /// read before the bound — a plugin answering <c>JSON\r</c> was answered, and must still be.
    /// </summary>
    [Fact]
    public async Task A_bare_CR_ends_a_line_as_it_always_did()
    {
        Assert.Equal(["a", "b", "c", "", "d"], await ReadAll("a\rb\r\nc\n\rd\r"));

        // A CR that ends one block and the LF that begins the next are one line's end, not two.
        var block = new string('x', 4095);
        Assert.Equal([block, "b"], await ReadAll($"{block}\r\nb"));
    }

    [Fact]
    public async Task A_long_line_across_many_reads_is_dropped_whole()
    {
        var huge = new string('h', 1_000_000);

        Assert.Equal(["first", null, "last"], await ReadAll($"first\n{huge}\nlast", limit: 100));
    }

    /// <summary>
    /// FABRIC-PERFORM1a, round 2: passing over a dropped frame keeps its top-level <c>id</c> and whether it named a <c>method</c>, wherever they
    /// stand in it, so an answer too long to read fails its call at once; a nested <c>id</c> or <c>method</c> is not the frame's.
    /// </summary>
    [Fact]
    public async Task A_dropped_frame_says_which_call_it_answered_wherever_its_id_stands()
    {
        var pad = new string('p', 500);
        var text = string.Join("\n",
            $$$"""{"jsonrpc":"2.0","id":7,"result":{"pad":"{{{pad}}}"}}""",
            $$$"""{"jsonrpc":"2.0","result":{"id":5,"method":"x","pad":"{{{pad}}}\"}"},"id" : 12}""",
            $$$"""{"jsonrpc":"2.0","method":"work/step","params":{"id":"{{{pad}}}"}}""",
            pad);
        var lines = new BoundedLines(new StringReader(text), 100);

        var read = new List<BoundedLine>();
        while (await lines.ReadAsync(CancellationToken.None) is { } line) read.Add(line);

        Assert.Equal(
            [new BoundedLine(null, 7), new BoundedLine(null, 12), new BoundedLine(null, null, Method: true), new BoundedLine(null)],
            read);
    }

    /// <summary>
    /// PLUGSTDERR1: a clipping reader, a plugin's stderr's, keeps a long line's start up to the bound, in whole characters, and reads the next;
    /// a line at the bound is whole and not marked.
    /// </summary>
    [Fact]
    public async Task A_clipping_reader_keeps_a_long_lines_start_and_reads_the_next()
    {
        var full = new string('x', 100);
        var huge = "预" + new string('h', 1_000_000);
        // 😀 is a pair, four bytes: after 95 bytes it fits once and the second does not; after 97 its first half alone would, and is not kept.
        var pairs = new string('a', 95) + "😀😀";
        var half = new string('a', 97) + "😀";
        var lines = new BoundedLines(new StringReader($"first\n{full}\n{huge}\n{pairs}\n{half}\nlast"), 100, clip: true);

        var read = new List<BoundedLine>();
        while (await lines.ReadAsync(CancellationToken.None) is { } line) read.Add(line);

        Assert.Equal(
            [
                new BoundedLine("first"), new BoundedLine(full), new BoundedLine("预" + new string('h', 97), Clipped: true),
                new BoundedLine(new string('a', 95) + "😀", Clipped: true), new BoundedLine(new string('a', 97), Clipped: true),
                new BoundedLine("last"),
            ],
            read);
    }

    [Fact]
    public void Sixty_events_a_minute_are_taken_and_the_next_one_is_not()
    {
        var rate = new EventRate();

        for (var at = 0; at < WorkBounds.EventsPerMinute; at++) Assert.True(rate.Admit(T0.AddMilliseconds(at * 500)));
        Assert.False(rate.Admit(T0.AddSeconds(59)));

        // A minute after the first, one more is taken.
        Assert.True(rate.Admit(T0.AddMinutes(1)));
        Assert.False(rate.Admit(T0.AddMinutes(1).AddMilliseconds(1)));
    }

    [Fact]
    public void A_refusal_is_written_once_a_minute_per_code_with_its_count()
    {
        var tally = new RefusalTally();

        Assert.Equal(1, tally.Refused(WorkRefusals.Shape, T0));
        for (var at = 1; at <= 4; at++) Assert.Null(tally.Refused(WorkRefusals.Shape, T0.AddSeconds(at)));
        // Another code has its own minute.
        Assert.Equal(1, tally.Refused(WorkRefusals.TooFast, T0.AddSeconds(5)));
        Assert.Empty(tally.Due(T0.AddSeconds(30)));

        // The minute past: the four held and this one are one line.
        Assert.Equal(5, tally.Refused(WorkRefusals.Shape, T0.AddSeconds(61)));

        // Held counts are written when their minute passes, though no refusal comes to carry them.
        Assert.Null(tally.Refused(WorkRefusals.Shape, T0.AddSeconds(70)));
        Assert.Empty(tally.Due(T0.AddSeconds(100)));
        Assert.Equal([(WorkRefusals.Shape, 1)], tally.Due(T0.AddSeconds(122)));
        Assert.Empty(tally.Due(T0.AddSeconds(200)));
    }

    [Fact]
    public void An_unasked_exit_waits_1_then_5_minutes_and_the_third_within_an_hour_is_unsound()
    {
        var restarts = new WorkRestarts();
        restarts.Started(T0);

        Assert.Equal(TimeSpan.FromMinutes(1), restarts.Exited(T0.AddMinutes(2)));
        Assert.False(restarts.MayStart(T0.AddMinutes(2.5)));
        Assert.True(restarts.MayStart(T0.AddMinutes(3)));
        restarts.Started(T0.AddMinutes(3));

        Assert.Equal(TimeSpan.FromMinutes(5), restarts.Exited(T0.AddMinutes(4)));
        Assert.False(restarts.MayStart(T0.AddMinutes(8)));
        Assert.True(restarts.MayStart(T0.AddMinutes(9)));
        restarts.Started(T0.AddMinutes(9));

        Assert.Null(restarts.Exited(T0.AddMinutes(10)));
        Assert.True(restarts.Unsound);
        Assert.False(restarts.MayStart(T0.AddDays(1)));

        // The person's Try again: a fresh record.
        restarts.TryAgain();
        Assert.False(restarts.Unsound);
        Assert.True(restarts.MayStart(T0.AddMinutes(10)));
        Assert.Equal(TimeSpan.FromMinutes(1), restarts.Exited(T0.AddMinutes(11)));
    }

    [Fact]
    public void Exits_spaced_beyond_the_hour_wait_15_minutes_and_are_not_unsound()
    {
        var restarts = new WorkRestarts();
        restarts.Started(T0);
        Assert.Equal(TimeSpan.FromMinutes(1), restarts.Exited(T0));
        restarts.Started(T0.AddMinutes(1));
        Assert.Equal(TimeSpan.FromMinutes(5), restarts.Exited(T0.AddMinutes(41)));
        restarts.Started(T0.AddMinutes(46));

        // The first exit has left the hour, so two stand within it: the wait grows, and the plugin is not unsound.
        Assert.Equal(TimeSpan.FromMinutes(15), restarts.Exited(T0.AddMinutes(87)));
        Assert.False(restarts.Unsound);
        Assert.Equal(T0.AddMinutes(102), restarts.NotBefore);
    }

    [Fact]
    public void A_process_up_for_an_hour_starts_its_waits_afresh()
    {
        var restarts = new WorkRestarts();
        restarts.Started(T0);
        Assert.Equal(TimeSpan.FromMinutes(1), restarts.Exited(T0.AddMinutes(1)));
        restarts.Started(T0.AddMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(1), restarts.Exited(T0.AddMinutes(63)));
    }

    [Fact]
    public void A_start_that_fails_waits_as_an_exit_does()
    {
        var restarts = new WorkRestarts();

        Assert.Equal(TimeSpan.FromMinutes(1), restarts.Exited(T0));
        Assert.Equal(TimeSpan.FromMinutes(5), restarts.Exited(T0.AddMinutes(1)));
        Assert.Null(restarts.Exited(T0.AddMinutes(6)));
    }
}
