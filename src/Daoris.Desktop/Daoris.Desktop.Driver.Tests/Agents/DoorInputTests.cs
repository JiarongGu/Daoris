using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The run's one queue of words (AGENTPROVIDER1m-ii, D243): a word is written as the write returns and acknowledged once the harness names
/// its conversation; a retry hands the next process every word the ended one never acknowledged, in order, and never one it did.
/// </summary>
public sealed class DoorInputTests
{
    private sealed class Process
    {
        public List<string> Lines { get; } = [];

        public bool Alive { get; set; } = true;

        public Task<bool> WriteAsync(string line, CancellationToken ct)
        {
            if (!Alive) return Task.FromResult(false);
            Lines.Add(line);
            return Task.FromResult(true);
        }
    }

    private static readonly CancellationToken Never = CancellationToken.None;

    [Fact]
    public async Task A_word_is_told_written_as_the_write_returns_and_once()
    {
        var input = new DoorInput();
        var process = new Process();
        await input.AttachAsync(process.WriteAsync, Never);
        var told = 0;

        var delivery = await input.SayAsync(new DoorWord("w1", "first", () => told++), Never);

        Assert.Equal(Delivery.Written, delivery);
        Assert.Equal(["first"], process.Lines);
        Assert.Equal(1, told);
        Assert.Equal(["w1"], input.Unacknowledged);
    }

    [Fact]
    public async Task A_retry_replays_what_the_ended_process_never_acknowledged_in_order_and_those_said_meanwhile_after_them()
    {
        var input = new DoorInput { MayRetry = true };
        var first = new Process();
        await input.AttachAsync(first.WriteAsync, Never);
        await input.SayAsync(new DoorWord("w1", "one"), Never);
        await input.SayAsync(new DoorWord("w2", "two"), Never);

        // The process ended before it named its conversation; a word said while the run restarts waits, never refused.
        first.Alive = false;
        var meanwhile = new List<string>();
        var delivery = await input.SayAsync(new DoorWord("w3", "three", () => meanwhile.Add("w3")), Never);
        Assert.Equal(Delivery.Held, delivery);

        var second = new Process();
        input.MayRetry = false;
        Assert.True(await input.AttachAsync(second.WriteAsync, Never));

        Assert.Equal(["one", "two", "three"], second.Lines);
        Assert.Equal(["w3"], meanwhile);
        Assert.Equal(["w1", "w2", "w3"], input.Unacknowledged);
    }

    [Fact]
    public async Task A_word_acknowledged_is_never_written_again()
    {
        var input = new DoorInput { MayRetry = true };
        var first = new Process();
        await input.AttachAsync(first.WriteAsync, Never);
        var told = 0;
        await input.SayAsync(new DoorWord("w1", "one", () => told++), Never);

        // The harness named its conversation: it opened the one `one` went to, and no retry can follow.
        var acknowledged = input.Acknowledge();
        await input.SayAsync(new DoorWord("w2", "two"), Never);
        var second = new Process();
        await input.AttachAsync(second.WriteAsync, Never);

        Assert.Equal(["w1"], acknowledged.SelectMany(word => word.Ids));
        Assert.False(input.MayRetry);
        Assert.Equal(["two"], second.Lines);
        Assert.Equal(1, told);
    }

    [Fact]
    public async Task A_word_is_told_written_once_though_a_retry_writes_it_again()
    {
        var input = new DoorInput { MayRetry = true };
        var first = new Process();
        await input.AttachAsync(first.WriteAsync, Never);
        var told = 0;
        await input.SayAsync(new DoorWord("w1", "one", () => told++), Never);

        await input.AttachAsync(new Process().WriteAsync, Never);

        Assert.Equal(1, told);
    }

    [Fact]
    public async Task A_word_a_process_cannot_take_is_refused_where_no_retry_comes()
    {
        var input = new DoorInput();
        var process = new Process { Alive = false };
        await input.AttachAsync(process.WriteAsync, Never);

        Assert.Equal(Delivery.Refused, await input.SayAsync(new DoorWord("w1", "one"), Never));
        Assert.Empty(input.Unacknowledged);

        input.Detach();
        Assert.Equal(Delivery.Refused, await input.SayAsync(new DoorWord("w2", "two"), Never));
    }

    [Fact]
    public async Task A_word_said_before_the_first_process_is_attached_waits_for_it()
    {
        var input = new DoorInput();

        Assert.Equal(Delivery.Held, await input.SayAsync(new DoorWord("w1", "one"), Never));

        var process = new Process();
        await input.AttachAsync(process.WriteAsync, Never);
        Assert.Equal(["one"], process.Lines);
    }

    [Fact]
    public async Task A_control_line_is_written_now_and_never_replayed()
    {
        var input = new DoorInput { MayRetry = true };
        var first = new Process();
        await input.AttachAsync(first.WriteAsync, Never);

        Assert.True(await input.ControlAsync("interrupt", Never));
        var second = new Process();
        await input.AttachAsync(second.WriteAsync, Never);

        Assert.Equal(["interrupt"], first.Lines);
        Assert.Empty(second.Lines);
    }

    [Fact]
    public async Task Held_words_open_the_next_process_with_their_own_opening()
    {
        var input = new DoorInput();
        var note = new SessionEvent { Kind = SessionEventKind.Note, Text = "resumed" };
        input.Hold(new DoorWord(["w1"], "go on", [note]));

        Assert.Equal([note], input.Openings());
        var process = new Process();
        await input.AttachAsync(process.WriteAsync, Never);
        Assert.Equal(["go on"], process.Lines);
        Assert.Empty(input.Openings());
    }
}
