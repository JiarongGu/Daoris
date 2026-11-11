namespace Daoris.Driver;

/// <summary>
/// The person's *Send now* on a door that takes no word into a running turn but stops one keeping the conversation (Claude Code's
/// native wire, <c>codex-acp</c>): the words go first among what waits and the turn is stopped, so they are the next turn's first
/// words. Measured, Claude Code's own <c>now</c> line does the same: the step in flight is aborted, the cut turn ends with a result
/// of its own, and the words run as the next turn. Its state is <see cref="TurnState"/>'s.
/// </summary>
/// <param name="stopKeeping">The keeping stop, the person's press this one is (<see cref="TurnStops.StopKeepingAsync"/>).</param>
internal sealed class TurnCutIn(TurnState state, Func<Task<TurnStop>> stopKeeping)
{
    /// <summary>
    /// Place the words first among what waits, marked <see cref="ChatMessage.StoppedFor"/>, where they cut in: never with a word on
    /// its way, which a stop would answer cancelled while the agent may still act on it; never while a transient episode waits on
    /// the turn or the person's press holds a word. Called under the lock; false leaves the words to the caller.
    /// </summary>
    public bool Place(ChatMessage message, bool onTheirWay)
    {
        if (onTheirWay || state.Retrying || state.Holding is not null) return false;
        state.Waiting.Insert(0, state.Stamped(message with { StoppedFor = true }));
        state.Publish();
        return true;
    }

    /// <summary>The turn stopped, with the gate released, and what the person is answered: the words are the next turn's.</summary>
    public async Task<SentNow?> StopAsync()
    {
        await stopKeeping().ConfigureAwait(false);
        return new SentNow(SendNowWhy.NextTurn, Now: true, Why: null);
    }
}
