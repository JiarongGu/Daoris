namespace Daoris.Driver;

/// <summary>A pin the consumer's part of an ask still waits on: the producer, and why.</summary>
public sealed record PinWait(string Producer, AskPinStates State);

/// <summary>Where a consumer's pin of a producer stands for one ask.</summary>
public enum AskPinStates
{
    /// <summary>The producer's part has not landed on its line, so there is no commit to pin.</summary>
    Waiting,

    /// <summary>The producer's part landed, and the consumer's ask branch pins another commit or none.</summary>
    Owed,

    /// <summary>The consumer's ask branch pins the commit the producer's part landed.</summary>
    Pinned,

    /// <summary>The consumer's lock on its ask branch is no lock, or the branch is not read: it is left as it is.</summary>
    Unreadable,
}

/// <summary>
/// One edge of a family as one ask's consumer part owes it: <paramref name="Landed"/> is the commit the producer's part landed as on its line,
/// <paramref name="Pinned"/> what the consumer's ask branch pins of the producer now, and <paramref name="Readable"/> false where its lock is no lock.
/// </summary>
public sealed record AskPin(PinEdge Edge, string? Landed, string? Pinned, bool Readable)
{
    public AskPinStates State =>
        !Readable ? AskPinStates.Unreadable
        : Landed is null ? AskPinStates.Waiting
        : string.Equals(Landed, Pinned, StringComparison.OrdinalIgnoreCase) ? AskPinStates.Pinned
        : AskPinStates.Owed;

    /// <summary>Whether the consumer's part still waits on this pin: it lands with the pin moved, so any state but pinned holds it.</summary>
    public bool Holds => State != AskPinStates.Pinned;
}
