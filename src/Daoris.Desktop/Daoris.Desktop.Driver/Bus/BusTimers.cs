namespace Daoris.Driver;

/// <summary>
/// One armed timer: the obliged entry it watches, the subject and effect key its ends match, who is obliged, when it is armed, when it is
/// due next and how many times it has stalled so far. A row of <c>bus_timers</c>, durable across a restart.
/// </summary>
/// <param name="Entry">The obliged entry's id.</param>
/// <param name="Seq">The obliged entry's order in the log.</param>
/// <param name="Kind">The obliged entry's type and version.</param>
/// <param name="Subject">Its subject, which a subject end matches.</param>
/// <param name="Effect">Its effect key, which an effect end matches; null where it has none.</param>
/// <param name="Obliged">Who must act: a request's answerer, else its type's obliged handler.</param>
/// <param name="Armed">The obliged entry's append.</param>
/// <param name="Due">When it is due next: its deadline, then a deadline more after each stall.</param>
/// <param name="Retry">How many times it has stalled so far.</param>
public sealed record BusTimer(
    string Entry, long Seq, string Kind, string Subject, string? Effect, string Obliged, DateTimeOffset Armed, DateTimeOffset Due, int Retry);

/// <summary>What one written entry does to the timers, applied by the store in the transaction that writes the entry.</summary>
public abstract record BusTimerOp
{
    /// <summary>An obliged entry arms its timer.</summary>
    public sealed record Arm(BusTimer Timer) : BusTimerOp;

    /// <summary>An end clears the armed timers of <paramref name="Kind"/> whose subject or effect key (<paramref name="By"/>) is <paramref name="Key"/>.</summary>
    public sealed record ClearTimers(string Kind, string By, string Key) : BusTimerOp;

    /// <summary>A stall moves its timer to its next due time, once: only a timer that has stalled fewer than <paramref name="Retry"/> times moves.</summary>
    public sealed record Advance(string Entry, int Retry, DateTimeOffset Due) : BusTimerOp;

    /// <summary>A give-up takes its timer away.</summary>
    public sealed record Drop(string Entry) : BusTimerOp;
}

/// <summary>
/// What an entry does to the timers: an obliged one arms its own, due at its deadline (the registry's, stamped on its envelope);
/// an end clears what it ends (<see cref="BusTypes.Ends"/>); a stall advances its timer to the next due time and a give-up drops it. The
/// store applies them in the entry's own write, so a timer and the entry that moved it are kept or lost together, and a restart
/// neither loses a due time nor stalls one retry twice.
/// </summary>
public static class BusTimers
{
    /// <summary>The operations <paramref name="entry"/>, kept at <paramref name="seq"/>, makes: the clears it ends first, then its own arming.</summary>
    public static IReadOnlyList<BusTimerOp> Of(BusEnvelope entry, long seq)
    {
        if (BusTypes.Of(entry.Kind) is not { } type) return [];
        var ops = new List<BusTimerOp>();
        if (ReferenceEquals(type, BusTypes.Stalled))
        {
            if (Text(entry, "entry") is { } stalled && Number(entry, "retry") is { } retry && Moment(entry, "next") is { } next)
            {
                ops.Add(new BusTimerOp.Advance(stalled, (int)retry, next));
            }
        }
        else if (ReferenceEquals(type, BusTypes.GiveUp))
        {
            if (Text(entry, "entry") is { } gaveUp) ops.Add(new BusTimerOp.Drop(gaveUp));
        }

        foreach (var (armed, by) in BusTypes.EndedBy(entry.Kind))
        {
            var key = by == BusEnds.Effect ? entry.Effect : entry.Subject;
            if (key is not null) ops.Add(new BusTimerOp.ClearTimers(armed.Kind, by, key));
        }

        if (BusTypes.Obliges(type))
        {
            ops.Add(new BusTimerOp.Arm(new BusTimer(
                entry.Id, seq, entry.Kind, entry.Subject, entry.Effect, entry.Answerer ?? type.Obliged, entry.At,
                entry.Deadline ?? entry.At + type.Deadline.Within, 0)));
        }

        return ops;
    }

    private static object? Value(BusEnvelope entry, string key) => entry.Payload.FirstOrDefault(pair => pair.Key == key).Value;

    private static string? Text(BusEnvelope entry, string key) => Value(entry, key) as string;

    private static long? Number(BusEnvelope entry, string key) => Value(entry, key) switch
    {
        int number => number,
        long number => number,
        _ => null,
    };

    private static DateTimeOffset? Moment(BusEnvelope entry, string key) => Value(entry, key) switch
    {
        DateTimeOffset at => at,
        long ms => DateTimeOffset.FromUnixTimeMilliseconds(ms),
        _ => null,
    };
}

/// <summary>
/// What a give-up hands to whom: never the owner straight from a missed deadline, since the owner holds no role. A
/// <c>final-exit</c> hands the step to its holder chain, the obliged one's role holder, then the vacancy path, whose end is the owner's exit;
/// a <c>refused</c> request is answered to its requester and leaves the step a wait, and a <c>held</c> gate stays shut, said on Stalls, both
/// bounded by the vacancy and ended by the final exit. A role's executors and manager join the chain before the vacancy when they come.
/// </summary>
public static class BusChains
{
    /// <summary>The vacancy path, which bounds every wait.</summary>
    public const string Vacancy = "vacancy";

    /// <summary>The owner's final exit: one notice with everything tried, then the person's row.</summary>
    public const string OwnerExit = "owner-exit";

    /// <summary>What <paramref name="type"/>'s give-up hands (<c>step</c>, <c>refusal</c>, <c>held</c>) and the chain it walks, first to last.</summary>
    public static (string Hands, IReadOnlyList<string> To) Of(BusType type, string obliged) => type.GiveUp switch
    {
        BusGiveUps.Refused => ("refusal", ["requester:" + type.Emitter, Vacancy, OwnerExit]),
        BusGiveUps.Held => ("held", ["stalls", Vacancy, OwnerExit]),
        _ => ("step", ["holder:" + obliged, Vacancy, OwnerExit]),
    };
}
