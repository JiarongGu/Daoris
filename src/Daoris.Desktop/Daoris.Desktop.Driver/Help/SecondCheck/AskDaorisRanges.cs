namespace Daoris.Driver;

/// <summary>
/// A selected turn's place in its conversation's ranges: the queued range while the packet's bound holds, else the next, a taken range never
/// growing again and a turn alone over the bound a range of its own, marked; while the check is off, a report closed <i>disabled</i>, never
/// read later. A turn that only read is written nowhere. A range has one session, so one writer.
/// </summary>
public sealed class AskDaorisRanges(AskDaorisReports reports, Func<DateTimeOffset> clock, int bound = AskDaorisRanges.Bound)
{
    /// <summary>The most characters a range's packet holds.</summary>
    public const int Bound = 40_000;

    /// <summary>How long a range waits queued for a reader before it closes unread.</summary>
    public static readonly TimeSpan Waits = TimeSpan.FromHours(24);

    /// <summary>The report that holds <paramref name="turn"/> now; null for a turn that only read.</summary>
    public AskDaorisReport? Take(string session, ReadTurn turn, bool on) => turn.Selected ? reports.Locked(() => Taken(session, turn, on)) : null;

    private AskDaorisReport Taken(string session, ReadTurn turn, bool on)
    {
        var now = clock();
        if (!on)
        {
            var off = Opened(session, turn, now) with { State = AskDaorisStates.Disabled, Closed = now };
            reports.Write(off);
            return off;
        }

        if (reports.Of(session).LastOrDefault(report => report.State == AskDaorisStates.Queued) is { } queued && queued.Size + turn.Size <= bound)
        {
            var joined = queued with { Turns = [.. queued.Turns, turn], Limits = [.. queued.Limits.Union(turn.Limits)] };
            reports.Write(joined);
            return joined;
        }

        var opened = Opened(session, turn, now);
        reports.Write(opened);
        return opened;
    }

    /// <summary>
    /// The turns of <paramref name="session"/> that ended past its seen mark, each taken, then the conversation seen to its last. Of a
    /// conversation never seen, the turns that ended from <paramref name="running"/> on, this run's start, so one older than the check is
    /// never read back.
    /// </summary>
    public IReadOnlyList<AskDaorisReport> Ended(string session, IReadOnlyList<ReadTurn> turns, bool on, DateTimeOffset running) =>
        reports.Seen(session) is { } seen ? Since(session, turns, on, seen)
        : Since(session, turns, on, turns.Where(turn => turn.Ended < running).Select(turn => turn.To).DefaultIfEmpty(0).Max());

    /// <summary>
    /// At a start, what ended while Daoris ran nowhere to select it: as <see cref="Ended"/>, but a conversation never seen is seen
    /// from now, so a conversation older than the check is never read back.
    /// </summary>
    public IReadOnlyList<AskDaorisReport> CatchUp(string session, IReadOnlyList<ReadTurn> turns, bool on)
    {
        if (reports.Seen(session) is { } seen) return Since(session, turns, on, seen);
        if (turns.Count > 0) reports.MarkSeen(session, turns.Max(turn => turn.To));
        return [];
    }

    /// <summary>Every range queued past <see cref="Waits"/> closed unread with why; how many.</summary>
    public int CloseStale()
    {
        var now = clock();
        var stale = reports.InState(AskDaorisStates.Queued).Where(report => now - report.Opened >= Waits).ToList();
        return stale.Count(report => reports.Change(report.Session, report.Range, each => each.State != AskDaorisStates.Queued ? null : each with
        {
            State = AskDaorisStates.Unread, Closed = now, Why = "no reader could take it within 24 hours" + (each.Waiting is { } waiting ? ": " + waiting : ""),
        }) is not null);
    }

    private IReadOnlyList<AskDaorisReport> Since(string session, IReadOnlyList<ReadTurn> turns, bool on, long seen)
    {
        var taken = new List<AskDaorisReport>();
        foreach (var turn in turns.Where(turn => turn.From > seen).OrderBy(turn => turn.From))
        {
            if (Take(session, turn, on) is { } report) taken.Add(report);
            reports.MarkSeen(session, turn.To);
        }

        return taken;
    }

    private AskDaorisReport Opened(string session, ReadTurn turn, DateTimeOffset now) => new(
        "r-" + Guid.NewGuid().ToString("N")[..12], session, AskDaorisStates.Queued, now, [turn],
        turn.Size > bound ? [.. turn.Limits, new ReadLimitAt(ReadLimit.OverBound)] : turn.Limits);
}
