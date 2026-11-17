using Daoris.Assistant.Ports;
using Xunit;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The cases every <see cref="ITurnEvents"/> passes: <see cref="ScriptedTurnEvents"/> in the assistant's suite, the desktop adapter
/// over the session events in its own.
/// </summary>
public abstract class TurnEventsContract
{
    private static readonly CancellationToken None = CancellationToken.None;

    /// <summary>The turns under test, no event yet.</summary>
    protected abstract ITurnEvents Backend { get; }

    /// <summary>Adds an event to <paramref name="conversation"/> and answers its index.</summary>
    protected abstract Task<long> AppendAsync(string conversation, string kind, string text);

    /// <summary>Ends <paramref name="conversation"/>'s turn as <paramref name="outcome"/>.</summary>
    protected abstract Task EndTurnAsync(string conversation, TurnOutcome outcome);

    [Fact]
    public async Task The_events_after_an_index_are_the_later_ones_of_that_conversation_in_order()
    {
        var first = await AppendAsync("a", "said", "What is running?");
        await AppendAsync("b", "said", "Another conversation.");
        var second = await AppendAsync("a", "tool", "daoris status");
        var third = await AppendAsync("a", "answer", "Two sessions.");

        var after = await Backend.AfterAsync("a", first, None);

        Assert.Equal([second, third], after.Select(each => each.Index));
        Assert.Equal(["tool", "answer"], after.Select(each => each.Kind));
        Assert.Equal(["daoris status", "Two sessions."], after.Select(each => each.Text));
        Assert.All(after, each => Assert.Equal("a", each.Conversation));
        Assert.True(first < second && second < third);
    }

    [Fact]
    public async Task A_conversation_unknown_has_no_events()
    {
        await AppendAsync("a", "said", "What is running?");

        Assert.Empty(await Backend.AfterAsync("nobody", 0, None));
    }

    [Fact]
    public async Task A_turns_end_is_heard_with_its_last_events_index_and_how_it_ended()
    {
        var heard = new TaskCompletionSource<TurnEnded>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var hearing = Backend.OnTurnEnded(ended => heard.TrySetResult(ended));
        await AppendAsync("a", "said", "What is running?");
        var last = await AppendAsync("a", "answer", "Two sessions.");

        await EndTurnAsync("a", TurnOutcome.Answered);

        Assert.Equal(new TurnEnded("a", last, TurnOutcome.Answered), await heard.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }
}
