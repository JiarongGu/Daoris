using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The person's stop where the agent can no longer be reached.</summary>
public sealed class TurnStopFailureTests
{
    /// <summary>
    /// 🔴 The words a stop withdrew are handed back to the person even where the door's interrupt cannot reach the agent: they are off the
    /// queue and the record by then, so a stop that threw would lose them.
    /// </summary>
    [Fact]
    public async Task A_stop_the_agent_cannot_be_reached_for_still_hands_back_the_words_it_withdrew()
    {
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var took = 0;
        var turns = new ChatTurns(
            ready: () => Task.FromResult(true),
            take: (_, sent) =>
            {
                Interlocked.Increment(ref took);
                sent();
                return ended.Task;
            },
            interrupt: () => throw new IOException("the pipe is closed"),
            changed: _ => { });

        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => Volatile.Read(ref took) == 1, () => "the first turn never reached the door");
        turns.Take(new ChatMessage("and tidy the readme", []) { Level = MessageLevel.Ordinary });

        var stop = await turns.StopAsync();

        Assert.True(stop.Cancelled);
        Assert.Equal(["and tidy the readme"], stop.Withdrawn.Select(word => word.Text));
        ended.SetResult();
        turns.Gone();
    }
}
