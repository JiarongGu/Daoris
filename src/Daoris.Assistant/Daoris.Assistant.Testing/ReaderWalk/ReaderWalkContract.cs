using Daoris.Assistant.Ports;
using Xunit;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The cases every <see cref="IReaderWalk"/> passes: <see cref="ScriptedReader"/> in the assistant's suite, the desktop adapter's walk
/// on its stand-in supervisor in its own.
/// </summary>
public abstract class ReaderWalkContract
{
    private static readonly CancellationToken None = CancellationToken.None;

    /// <summary>The walk under test.</summary>
    protected abstract IReaderWalk Backend { get; }

    /// <summary>Makes the next reading answer <paramref name="state"/>: the fake is scripted, the real side's stand-in holder is set.</summary>
    protected abstract void Arrange(ReaderState state);

    private static ReaderAsk Ask(long from = 1, long to = 4, string packet = "turn 1: the person asked; turn 2: the assistant pressed") =>
        new("ask-daoris", "conversation-1", from, to, packet);

    [Fact]
    public async Task A_kept_reading_answers_its_sentence_and_its_reading()
    {
        Arrange(ReaderState.Kept);

        var answer = await Backend.ReadAsync(Ask(), None);

        Assert.Equal(ReaderState.Kept, answer.State);
        Assert.False(string.IsNullOrWhiteSpace(answer.Sentence));
        Assert.False(string.IsNullOrWhiteSpace(answer.Reading));
    }

    [Theory]
    [InlineData(ReaderState.Ended)]
    [InlineData(ReaderState.Queued)]
    public async Task A_reading_not_kept_answers_its_sentence_and_no_reading(ReaderState state)
    {
        Arrange(state);

        var answer = await Backend.ReadAsync(Ask(), None);

        Assert.Equal(state, answer.State);
        Assert.False(string.IsNullOrWhiteSpace(answer.Sentence));
        Assert.Null(answer.Reading);
    }

    [Fact]
    public async Task A_blank_packet_or_a_range_ending_before_it_starts_is_refused()
    {
        Arrange(ReaderState.Kept);

        await Assert.ThrowsAsync<ArgumentException>(() => Backend.ReadAsync(Ask(packet: " "), None));
        await Assert.ThrowsAsync<ArgumentException>(() => Backend.ReadAsync(Ask(from: 5, to: 4), None));
    }
}
