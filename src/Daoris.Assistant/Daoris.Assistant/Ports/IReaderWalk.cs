namespace Daoris.Assistant.Ports;

/// <summary>
/// One packet-only reading of a conversation's range by a supervisor role's holder: the walk answers it kept, ended or still queued,
/// with its sentence. The role is named in the ask, so the walk is any role's, never the assistant's alone.
/// </summary>
public interface IReaderWalk
{
    /// <summary>
    /// Reads <paramref name="ask"/>'s packet. A packet that is blank, or a range that ends before it starts, is refused
    /// (<see cref="ArgumentException"/>) before any walk.
    /// </summary>
    Task<ReaderAnswer> ReadAsync(ReaderAsk ask, CancellationToken cancel);
}

/// <summary>What one reading is handed: the role whose holder reads, the conversation, the range of its events, and the packet itself.</summary>
public sealed record ReaderAsk(string Role, string Conversation, long From, long To, string Packet);

/// <summary>The walk's answer: its state and its sentence, and the reading where it was kept.</summary>
public sealed record ReaderAnswer(ReaderState State, string Sentence)
{
    public string? Reading { get; init; }
}

/// <summary>Where a reading stands: kept with its reading, ended without one, or still queued behind another.</summary>
public enum ReaderState
{
    Kept,
    Ended,
    Queued,
}
