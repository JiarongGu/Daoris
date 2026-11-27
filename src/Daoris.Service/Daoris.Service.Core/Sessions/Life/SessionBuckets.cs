namespace Daoris.Knowledge;

/// <summary>Where one second of a session goes: each second falls in exactly one.</summary>
public enum TimeBucket
{
    /// <summary>A turn ran.</summary>
    Working,

    /// <summary>It waited on the machine room's start.</summary>
    WaitingStart,

    /// <summary>It waited on a step of its run.</summary>
    WaitingStep,

    /// <summary>It waited on its own check.</summary>
    Checks,

    /// <summary>A turn fixed a check that came back red. No fact witnesses a verdict yet, so it reads zero until one does.</summary>
    FixingChecks,

    /// <summary>It waited on a reviewer. No fact names a step's kind yet, so it reads zero until one does.</summary>
    Reviewer,

    /// <summary>It waited on its landing. No fact names a step's kind yet, so it reads zero until one does.</summary>
    Landing,

    /// <summary>It waited on a person: a question open, or an end a person's resume reopened.</summary>
    OnPerson,

    /// <summary>It waited on nothing named: the fold's breach of a wait with no holder.</summary>
    Still,

    /// <summary>The application is down: from the newest fact its generation wrote to the next start.</summary>
    Down,
}

/// <summary>A span with the application down, from <paramref name="From"/> to <paramref name="To"/>.</summary>
public sealed record DownSpan(DateTimeOffset From, DateTimeOffset To);

/// <summary>A session's wall time, from its first fact to its end (or now), and where each second of it went.</summary>
public sealed record SessionTimes(TimeSpan Wall, IReadOnlyDictionary<TimeBucket, TimeSpan> Buckets);

/// <summary>
/// The time buckets of one session, from the same journal the fold reads: each fact holds the session in its bucket until the next,
/// the last until its end or now, and a span with the application down moves its seconds to <see cref="TimeBucket.Down"/>. The
/// buckets sum to the wall time exactly: a fact written on a clock behind the one before it holds nothing, never a negative span.
/// </summary>
public static class SessionBuckets
{
    /// <summary>The buckets of <paramref name="journal"/> (one session's facts, in journal order) at <paramref name="now"/>.</summary>
    public static SessionTimes Of(IReadOnlyList<SessionFact> journal, DateTimeOffset now, IReadOnlyList<DownSpan> down)
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
        for (var at = 0; at < journal.Count; at++)
        {
            var from = moments[at];
            var to = at + 1 < journal.Count ? moments[at + 1] : end;
            var downed = Overlap(spans, from, to);
            buckets[BucketOf(journal[at])] += to - from - downed;
            buckets[TimeBucket.Down] += downed;
        }

        return new(end - moments[0], buckets);
    }

    /// <summary>
    /// The spans with the application down, from its generations: from the newest fact each wrote (else its start) to the next
    /// generation's start. <paramref name="lastSeen"/> is each generation's newest fact.
    /// </summary>
    public static IReadOnlyList<DownSpan> Down(IReadOnlyList<AppGeneration> generations, IReadOnlyDictionary<long, DateTimeOffset> lastSeen)
    {
        var spans = new List<DownSpan>();
        for (var at = 0; at + 1 < generations.Count; at++)
        {
            var from = lastSeen.TryGetValue(generations[at].Number, out var seen) && seen > generations[at].Started ? seen : generations[at].Started;
            var to = generations[at + 1].Started;
            if (to > from) spans.Add(new(from, to));
        }

        return spans;
    }

    /// <summary>The bucket a fact holds its session in until the next fact.</summary>
    public static TimeBucket BucketOf(SessionFact fact) => fact.Kind switch
    {
        SessionFactKind.TurnBegan => TimeBucket.Working,
        SessionFactKind.Opened or SessionFactKind.Starting => TimeBucket.WaitingStart,
        SessionFactKind.QuestionOpened or SessionFactKind.Ended => TimeBucket.OnPerson,
        _ => fact.Holder switch
        {
            null => TimeBucket.Still,
            SessionFact.CheckHolder => TimeBucket.Checks,
            SessionFact.StartHolder => TimeBucket.WaitingStart,
            _ => TimeBucket.WaitingStep,
        },
    };

    /// <summary>The bucket's spelling on the wire.</summary>
    public static string Spell(TimeBucket bucket) => bucket switch
    {
        TimeBucket.Working => "working",
        TimeBucket.WaitingStart => "waiting-start",
        TimeBucket.WaitingStep => "waiting-step",
        TimeBucket.Checks => "checks",
        TimeBucket.FixingChecks => "fixing-checks",
        TimeBucket.Reviewer => "reviewer",
        TimeBucket.Landing => "landing",
        TimeBucket.OnPerson => "on-person",
        TimeBucket.Still => "still",
        _ => "down",
    };

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
