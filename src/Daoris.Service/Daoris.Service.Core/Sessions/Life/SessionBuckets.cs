namespace Daoris.Knowledge;

/// <summary>Where one second of a session goes: each second falls in exactly one.</summary>
public enum TimeBucket
{
    /// <summary>A turn ran.</summary>
    Working,

    /// <summary>It waited on the machine room's start, or on the memory or account a start needs.</summary>
    WaitingStart,

    /// <summary>It waited on a step of its run.</summary>
    WaitingStep,

    /// <summary>It waited on a check slot: its own check.</summary>
    Checks,

    /// <summary>A turn fixed a check that came back red. No fact witnesses a verdict yet, so it reads zero until one does.</summary>
    FixingChecks,

    /// <summary>It waited on a reviewer.</summary>
    Reviewer,

    /// <summary>It waited on its landing.</summary>
    Landing,

    /// <summary>It waited on a person or the steward: a question open, or an end a resume reopened.</summary>
    OnPerson,

    /// <summary>Nothing held it: the fold's breach.</summary>
    Still,

    /// <summary>The application is down: from its generation's last sign to the next start.</summary>
    Down,
}

/// <summary>A span with the application down, from <paramref name="From"/> to <paramref name="To"/>.</summary>
public sealed record DownSpan(DateTimeOffset From, DateTimeOffset To);

/// <summary>A session's wall time, from its first fact to its end (or now), and where each second of it went.</summary>
public sealed record SessionTimes(TimeSpan Wall, IReadOnlyDictionary<TimeBucket, TimeSpan> Buckets);

/// <summary>
/// The time buckets of one session, from the same journal and the same reading the fold uses (<see cref="SessionFold.Reading"/>): each
/// fact holds the session in the bucket the fold's reading of it names until the next fact, the last until its end or now. A turn's
/// process is taken as alive until the application's next start after it began, and void after; a span with the application down moves
/// its seconds to <see cref="TimeBucket.Down"/>. The buckets sum to the wall time exactly: a fact written on a clock behind the one before
/// it holds nothing, never a negative span.
/// </summary>
public static class SessionBuckets
{
    /// <summary>
    /// The buckets of <paramref name="journal"/> (one session's facts, in journal order) at <paramref name="now"/>, with the application
    /// down for <paramref name="down"/> and started anew at each of <paramref name="starts"/>.
    /// </summary>
    public static SessionTimes Of(
        IReadOnlyList<SessionFact> journal, DateTimeOffset now, IReadOnlyList<DownSpan> down, IReadOnlyList<DateTimeOffset>? starts = null)
    {
        var buckets = Enum.GetValues<TimeBucket>().ToDictionary(bucket => bucket, _ => TimeSpan.Zero);
        if (journal.Count == 0) return new(TimeSpan.Zero, buckets);

        // Each fact's moment, never before the one before it.
        var moments = new DateTimeOffset[journal.Count];
        for (var at = 0; at < journal.Count; at++)
        {
            moments[at] = at == 0 || journal[at].At > moments[at - 1] ? journal[at].At : moments[at - 1];
        }

        var end = journal[^1].Kind == SessionFactKind.Ended || now < moments[^1] ? moments[^1] : now;
        var spans = Merged(down);
        var restarts = (starts ?? []).Order().ToList();
        for (var at = 0; at < journal.Count; at++)
        {
            var from = moments[at];
            var to = at + 1 < journal.Count ? moments[at + 1] : end;
            // A turn's lease is live until the next start after it began, and void from that start on.
            var turn = journal[at].Kind == SessionFactKind.TurnBegan;
            var voided = to;
            if (turn && restarts.FindIndex(start => start > from) is >= 0 and var next && restarts[next] < to) voided = restarts[next];
            Hold(buckets, spans, from, voided, BucketOf(journal[at], turn ? Leasing.Live : Leasing.None));
            Hold(buckets, spans, voided, to, BucketOf(journal[at], Leasing.Void));
        }

        return new(end - moments[0], buckets);
    }

    /// <summary>
    /// The spans with the application down, from its generations: from each one's last sign (its newest fact, its newest tick's audit,
    /// else its start) to the next generation's start. <paramref name="lastFact"/> is each generation's newest fact.
    /// </summary>
    public static IReadOnlyList<DownSpan> Down(IReadOnlyList<AppGeneration> generations, IReadOnlyDictionary<long, DateTimeOffset> lastFact)
    {
        var spans = new List<DownSpan>();
        for (var at = 0; at + 1 < generations.Count; at++)
        {
            var generation = generations[at];
            var from = generation.Started;
            if (generation.Seen is { } seen && seen > from) from = seen;
            if (lastFact.TryGetValue(generation.Number, out var wrote) && wrote > from) from = wrote;
            var to = generations[at + 1].Started;
            if (to > from) spans.Add(new(from, to));
        }

        return spans;
    }

    /// <summary>The bucket the fold's reading of <paramref name="fact"/> names, against <paramref name="leasing"/>.</summary>
    public static TimeBucket BucketOf(SessionFact fact, Leasing leasing)
    {
        var (state, holder, breach) = SessionFold.Reading(fact, leasing);
        return breach is not null ? TimeBucket.Still : state switch
        {
            FoldedState.Working => TimeBucket.Working,
            FoldedState.NeedsYou or FoldedState.Ended => TimeBucket.OnPerson,
            _ => holder?.Kind switch
            {
                HolderKind.MachineRoom or HolderKind.Memory or HolderKind.Account => TimeBucket.WaitingStart,
                HolderKind.CheckSlot => TimeBucket.Checks,
                HolderKind.Step => TimeBucket.WaitingStep,
                HolderKind.Reviewer => TimeBucket.Reviewer,
                HolderKind.Landing => TimeBucket.Landing,
                HolderKind.Person or HolderKind.Steward => TimeBucket.OnPerson,
                _ => TimeBucket.Still,
            },
        };
    }

    private static void Hold(Dictionary<TimeBucket, TimeSpan> buckets, List<DownSpan> spans, DateTimeOffset from, DateTimeOffset to, TimeBucket bucket)
    {
        if (to <= from) return;
        var downed = Overlap(spans, from, to);
        buckets[bucket] += to - from - downed;
        buckets[TimeBucket.Down] += downed;
    }

    private static List<DownSpan> Merged(IReadOnlyList<DownSpan> down)
    {
        var merged = new List<DownSpan>();
        foreach (var span in down.Where(span => span.To > span.From).OrderBy(span => span.From))
        {
            if (merged.Count > 0 && span.From <= merged[^1].To)
            {
                if (span.To > merged[^1].To) merged[^1] = merged[^1] with { To = span.To };
            }
            else
            {
                merged.Add(span);
            }
        }

        return merged;
    }

    private static TimeSpan Overlap(List<DownSpan> spans, DateTimeOffset from, DateTimeOffset to)
    {
        var total = TimeSpan.Zero;
        foreach (var span in spans)
        {
            var start = span.From > from ? span.From : from;
            var stop = span.To < to ? span.To : to;
            if (stop > start) total += stop - start;
        }

        return total;
    }
}
