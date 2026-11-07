namespace Daoris.Driver;

/// <summary>
/// Where one opinion stands as the driver delivers it: its state,
/// one of <see cref="OpinionDeliveryStates"/>, and the line a terminal and a look say, opening with that state.
/// </summary>
/// <param name="Line">What a terminal and a look say, one line. Never <i>no issues</i>: a pass that read nothing says so.</param>
public sealed record OpinionDelivery(string Opinion, string State, string Line)
{
    /// <summary>Why, a code: a failed pass's (<c>ended</c>, <c>out-of-time</c>) or why its findings went to the person.</summary>
    public string? Why { get; init; }

    /// <summary>The working session the findings are for.</summary>
    public string? Session { get; init; }

    /// <summary>The working session's answers as read when its turn ended; null until then.</summary>
    public OpinionReading? Reading { get; init; }

    /// <summary>What waits for the person: null until the answers are read, or the findings went to the person.</summary>
    public OpinionDisputes? Disputes { get; init; }

    /// <summary>The one recheck, where this delivery asked it.</summary>
    public OpinionPassRun? Recheck { get; init; }
}
