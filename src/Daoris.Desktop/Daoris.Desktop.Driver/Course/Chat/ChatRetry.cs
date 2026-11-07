namespace Daoris.Driver;

/// <summary>
/// What a conversation on the protocol door is handed to try a transient failure again in the same conversation: the one reader over the adapter set, the clock its waits are read off
/// (<c>.claude/knowledge/held-clocks.md</c>), and who it is. The conversation's turns supply the rest, since its process lives across
/// turns and its words wait in its own queue.
/// </summary>
/// <param name="Session">The conversation's record, which names the episode's go-on (<see cref="DaorisKeys.TransientRetry"/>).</param>
/// <param name="Agent">The adapter it runs on, which its lines name.</param>
/// <param name="Clock">The clock every wait is read off: the machine's in production, a held one in a test.</param>
/// <param name="Read">The one reader (<see cref="AgentErrors.Read"/>), closed over the adapter set and the adapter.</param>
internal sealed record ChatRetry(string Session, string Agent, TimeProvider Clock, Func<AcpRefusal, AgentError> Read)
{
    /// <summary>Told as each try goes, its number from one, and when the next would, or null where it is the last: the machine log's line.</summary>
    public Action<TransientEpisode, int, DateTimeOffset?>? Tried { get; init; }

    /// <summary>The tries its episode makes: its line's tries rule (<see cref="ConversationRules.Tries"/>), else five.</summary>
    public int Limit { get; init; } = TransientRetry.Bound;

    /// <summary>Told as a turn ends past its episode, before its end is recorded: the turn's end its rules then judge (<see cref="TransientMove"/>).</summary>
    public Action? PastBound { get; init; }
}
