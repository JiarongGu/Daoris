namespace Daoris.Driver;

/// <summary>
/// Which account the next start of a scope would take, why, and what holds each other account of the agent's:
/// the walk's answer asked without starting, counting or probing anything. Machine-local, as every account name is.
/// </summary>
/// <param name="Account">The account it takes, or null for the tool's own sign-in, and while it <see cref="NextReason.Waits"/>.</param>
/// <param name="Others">Every other account of the agent's, in the order the walk tried them, then the kept account a driven
/// start drops, then accounts the list names that this machine lacks, then accounts the scope does not use, by name.</param>
public sealed record NextStart(string? Account, NextReason Reason, IReadOnlyList<AccountHeld> Others)
{
    /// <summary>The account the reason weighed it against: where the list begins, or else the next ready account.</summary>
    public string? Over { get; init; }

    /// <summary>The moment the reason names: its week's reset (<see cref="NextReason.Lapsing"/>), or the first account offered again (<see cref="NextReason.Waits"/>).</summary>
    public DateTimeOffset? When { get; init; }

    /// <summary>Where it is <see cref="NextReason.Lapsing"/>, the window that won: <c>weekly</c>.</summary>
    public string? Window { get; init; }

    /// <summary>Where it is <see cref="NextReason.Lapsing"/>, the whole percent of that window unused, as the start's line says it.</summary>
    public int? Unused { get; init; }

    /// <summary>Where it is <see cref="NextReason.Lapsing"/>, when the reading that says it is made.</summary>
    public DateTimeOffset? Seen { get; init; }

    /// <summary>
    /// The other doors onto the agent's accounts whose own-home state differs from the door this answer speaks for (the account's own
    /// door): a plugin's door that starts in its own home where the account's is owed an account, or the reverse. One each, so the
    /// card says it of the door and never as the agent's.
    /// </summary>
    public IReadOnlyList<DoorStart> Doors { get; init; } = [];
}

/// <summary>One door's answer to whether a start on it runs in its own home, owing no Daoris account.</summary>
public sealed record DoorStart(string Door, bool StartsInOwnHome);
