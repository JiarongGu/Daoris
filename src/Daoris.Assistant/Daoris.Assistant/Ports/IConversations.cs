namespace Daoris.Assistant.Ports;

/// <summary>
/// The conversations the assistant holds, on the one conversation runtime: placed and started in a room folder under a mode with the
/// rules and reads handed, gone on, stopped, their access swapped at a turn's end, and read back.
/// </summary>
public interface IConversations
{
    /// <summary>Places and starts a conversation; it answers turning, under the mode and access asked.</summary>
    Task<ConversationSeen> StartAsync(ConversationStart start, CancellationToken cancel);

    /// <summary>Hands <paramref name="words"/> to a conversation that is not stopped; one unknown or stopped is refused (<see cref="InvalidOperationException"/>).</summary>
    Task GoOnAsync(string conversation, string words, CancellationToken cancel);

    /// <summary>Stops a conversation; stopping one already stopped changes nothing, and one unknown is refused (<see cref="InvalidOperationException"/>).</summary>
    Task StopAsync(string conversation, CancellationToken cancel);

    /// <summary>Asks for <paramref name="access"/> at the conversation's turn's end: until then it keeps its access and names the one asked.</summary>
    Task SwapAccessAsync(string conversation, string access, CancellationToken cancel);

    /// <summary>The conversation as it stands, or null for one unknown.</summary>
    Task<ConversationSeen?> ReadAsync(string conversation, CancellationToken cancel);
}

/// <summary>What a start hands: the room folder, the mode, the access, the person's first words, and the rules, reads and focus told.</summary>
public sealed record ConversationStart(string Room, string Mode, string Access, string Words)
{
    public IReadOnlyList<string> Rules { get; init; } = [];

    public IReadOnlyList<string> Reads { get; init; } = [];

    public string? Focus { get; init; }
}

/// <summary>A conversation as it stands: its id, the agent it runs on, its mode, access and state, its focus, and a swap asked.</summary>
public sealed record ConversationSeen(string Id, string Agent, string Mode, string Access, ConversationState State)
{
    public string? Focus { get; init; }

    /// <summary>The access a swap asked for, made at the turn's end; null where none waits.</summary>
    public string? AccessAsked { get; init; }
}

/// <summary>Where a conversation is: in a turn, waiting for the person's next words, or stopped.</summary>
public enum ConversationState
{
    Turning,
    Waiting,
    Stopped,
}
