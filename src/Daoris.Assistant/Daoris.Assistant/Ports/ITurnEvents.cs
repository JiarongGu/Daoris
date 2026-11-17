namespace Daoris.Assistant.Ports;

/// <summary>
/// The turns of the conversations the assistant holds: each turn's end as it happens, and a conversation's events after an index.
/// Nothing here names the assistant, so a second package that reads turns takes this shape whole.
/// </summary>
public interface ITurnEvents
{
    /// <summary>Hears each turn's end from now until the answer is disposed.</summary>
    IDisposable OnTurnEnded(Action<TurnEnded> heard);

    /// <summary>The conversation's events with an index above <paramref name="index"/>, in order; none for a conversation unknown.</summary>
    Task<IReadOnlyList<TurnEvent>> AfterAsync(string conversation, long index, CancellationToken cancel);
}

/// <summary>A turn's end: its conversation, the index of its last event, and how it ended.</summary>
public sealed record TurnEnded(string Conversation, long Index, TurnOutcome Outcome);

/// <summary>One event of a conversation: its index, its kind and its text, and the name of what it called where it called something.</summary>
public sealed record TurnEvent(string Conversation, long Index, string Kind, string Text)
{
    public string? Name { get; init; }
}

/// <summary>How a turn ended.</summary>
public enum TurnOutcome
{
    Answered,
    Failed,
    Stopped,
}
