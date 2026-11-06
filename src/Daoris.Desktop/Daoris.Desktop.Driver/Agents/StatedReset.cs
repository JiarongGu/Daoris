namespace Daoris.Driver;

/// <summary>How precisely a limit's reset was stated (the agent-lyntai design §5.3, round 4): what decides whether D125's grace applies.</summary>
public enum ResetPrecision
{
    /// <summary>An absolute moment a field gave (a rejected frame's Unix seconds): dated.</summary>
    Absolute,

    /// <summary>A date and a time, the year named or not: dated.</summary>
    DateAndTime,

    /// <summary>A weekday and a time, standing where the date was dropped: never graced.</summary>
    WeekdayAndTime,

    /// <summary>A time of day, standing where the date was dropped: never graced.</summary>
    TimeOfDay,

    /// <summary>A wait from now (<i>try again in 5 minutes</i>): never graced.</summary>
    RelativeWait,

    /// <summary>An instant another reader resolved, how it was stated not kept: never graced, until the statement itself is handed.</summary>
    Resolved,
}

/// <summary>
/// A limit's reset as it was stated, read against this machine's clock: the moment the statement names next, how precisely it named it,
/// and whether this machine's zone stood in for the one it named or for none.
/// </summary>
public sealed record StatedReset(DateTimeOffset Moment, ResetPrecision Precision, bool AssumedZone)
{
    /// <summary>Whether it names one date, so a moment just behind the clock is a disagreement of clocks rather than tomorrow's.</summary>
    public bool Dated => Precision is ResetPrecision.Absolute or ResetPrecision.DateAndTime;
}

/// <summary>
/// D125's bounds over a stated reset (D125 §2.1, the agent-lyntai design §5.3): the one function a sentence's reading and the agent layer's
/// ingress both call, so the policy is Daoris's whichever reader stated the reset.
/// </summary>
public static class ResetBounds
{
    /// <summary>
    /// The limit <paramref name="reset"/> gives: the margin added to it; a dated one up to <see cref="AccountLimits.Grace"/> behind
    /// <paramref name="seen"/> read as <paramref name="seen"/>; one behind it otherwise, or beyond <see cref="AccountLimits.Furthest"/>,
    /// not believed, the default standing in; none stated, the default.
    /// </summary>
    /// <param name="hit">What was hit, as the record says it.</param>
    /// <param name="window">The window the reset named, or null.</param>
    /// <param name="coolOff">The default cool-off, from <paramref name="seen"/>.</param>
    public static LimitSeen Limit(string hit, string? window, StatedReset? reset, DateTimeOffset seen, TimeSpan coolOff)
    {
        var byDefault = new LimitSeen(hit, window, seen + coolOff, Stated: false, AssumedZone: false);
        if (reset is null) return byDefault;

        var moment = reset.Dated && reset.Moment <= seen && seen - reset.Moment <= AccountLimits.Grace ? seen : reset.Moment;
        if (moment < seen || moment - seen > AccountLimits.Furthest) return byDefault with { NotBelieved = true };
        return new LimitSeen(hit, window, moment + AccountLimits.Margin, Stated: true, AssumedZone: reset.AssumedZone);
    }
}
