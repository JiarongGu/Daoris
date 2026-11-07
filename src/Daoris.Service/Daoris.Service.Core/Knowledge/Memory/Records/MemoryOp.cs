namespace Daoris.Knowledge;

/// <summary>
/// One row of <c>memory_ops</c>, the log of record of a repository's memory (§3). <see cref="By"/> is null on a row the service's sweep wrote.
/// </summary>
public sealed record MemoryOp(string Id, string Repository, string State, MemoryChange Change, MemoryAuthor? By, DateTimeOffset At)
{
    /// <summary>The models of the turn that made it, named at its commit point; null before.</summary>
    public string? Models { get; init; }

    /// <summary>Who decided an awaiting end.</summary>
    public string? Approver { get; init; }

    /// <summary>When a recheck held the target while this end awaited.</summary>
    public DateTimeOffset? Holds { get; init; }

    /// <summary>The entry the engine answered: a write's new or repeated entry, an end's or confirmation's target.</summary>
    public string? Entry { get; init; }

    /// <summary>The revision the engine answered.</summary>
    public long? Applied { get; init; }

    /// <summary>When the service settled it.</summary>
    public DateTimeOffset? Settled { get; init; }

    /// <summary>The quest a proposal travels on (2e); null on every other row.</summary>
    public string? Carried { get; init; }

    /// <summary>The owner's note on the quest that settled a proposal: its decline's reason or its handback.</summary>
    public string? Answer { get; init; }

    /// <summary>When the proposer is told how its proposal settled.</summary>
    public DateTimeOffset? Told { get; init; }

    /// <summary>When the service marked it <c>applying</c>: the instant an engine's receipt for its key could have begun; null on a row from before.</summary>
    public DateTimeOffset? Began { get; init; }
}
