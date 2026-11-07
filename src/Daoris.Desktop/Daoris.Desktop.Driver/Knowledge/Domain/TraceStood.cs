namespace Daoris.Driver;

/// <summary>What stood when a driven session on a quest started, by the moments the stores keep.</summary>
public sealed record TraceStood
{
    /// <summary><see cref="TraceStoodGaps.NoMoment"/> where its record says no moment it opened.</summary>
    public string? Missing { get; init; }

    /// <summary>The standing answer for its repository; null for a teammate's, whose machine keeps its own.</summary>
    public TraceStanding? Standing { get; init; }

    public IReadOnlyList<TraceGoAheadStood> GoAheads { get; init; } = [];

    /// <summary>How many of the person's words on its ask came before it.</summary>
    public TraceWordsBefore? Words { get; init; }
}

/// <summary>A repository's standing answer at a start: one of <see cref="TraceStandings"/>, when it is set, and its words.</summary>
public sealed record TraceStanding(string Repository, string State)
{
    /// <summary>Its store: <see cref="TraceStores.Config"/>.</summary>
    public string Source => TraceStores.Config;

    public DateTimeOffset? At { get; init; }

    /// <summary>The person's words, where it stood before the start.</summary>
    public string? Says { get; init; }
}

/// <summary>A go-ahead at a start: one of <see cref="TraceGoAheadStands"/>, the answer's moment, and whether this session asked it.</summary>
public sealed record TraceGoAheadStood(int Number, string State)
{
    public DateTimeOffset? At { get; init; }

    public bool Mine { get; init; }
}

public sealed record TraceWordsBefore(string Ask, int Before, int Of);
