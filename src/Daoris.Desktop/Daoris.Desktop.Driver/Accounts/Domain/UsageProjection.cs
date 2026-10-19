namespace Daoris.Driver;

/// <summary>One reading of a window as the projection reads the usage history (USAGEPROBE1): Daoris's name for it, its use, its reset, when, and its account's epoch.</summary>
/// <param name="Window">Daoris's name for the window: <c>session</c> or <c>weekly</c>.</param>
/// <param name="Used">How much of it was used, from 0 to 1.</param>
/// <param name="Reset">When it turns over, or null where none was said.</param>
/// <param name="Seen">When it was observed.</param>
/// <param name="Epoch">The account's binding epoch, or null where the epochs file did not read.</param>
public sealed record ReadPoint(string Window, double Used, DateTimeOffset? Reset, DateTimeOffset Seen, int? Epoch = null);

/// <summary>One turn of Daoris's on an account as the projection weighs it (USAGEPROBE1): when it ended, its work, and its account's epoch.</summary>
/// <param name="Work">Its tokens in reference-equivalent tokens (<see cref="UsageProjection.Work"/>).</param>
public sealed record SpentTurn(DateTimeOffset Ended, double Work, int? Epoch = null);

/// <summary>How a window's projection was made (USAGEPROBE1).</summary>
public static class ProjectedBy
{
    /// <summary>Its last reading alone: nothing spent since, or too little history to convert what was.</summary>
    public const string Reading = "reading";

    /// <summary>Its last reading plus Daoris's own turns since, by the account's measured ratio.</summary>
    public const string Measured = "measured";
}

/// <summary>One window projected to now (USAGEPROBE1).</summary>
/// <param name="Window">Daoris's name for it.</param>
/// <param name="Read">What its last reading said was used, from 0 to 1.</param>
/// <param name="Projected">What it is projected to have used now, from 0 to 1: never less than <paramref name="Read"/>, never more than 1.</param>
/// <param name="Seen">When its last reading was.</param>
/// <param name="Reset">When it turns over.</param>
/// <param name="How"><see cref="ProjectedBy.Reading"/> or <see cref="ProjectedBy.Measured"/>.</param>
/// <param name="Ratio">The share of the window one reference-equivalent token of Daoris's spends on this account, where measured.</param>
/// <param name="Spent">The reference-equivalent tokens Daoris's turns on the account spent since its last reading.</param>
public sealed record WindowProjection(
    string Window, double Read, double Projected, DateTimeOffset Seen, DateTimeOffset Reset, string How, double? Ratio = null, double Spent = 0);

/// <summary>An account's windows projected to now (USAGEPROBE1), each that its last reading gave a number for.</summary>
public sealed record AccountProjection(IReadOnlyList<WindowProjection> Windows)
{
    /// <summary>The window by Daoris's name, or null where it has no number.</summary>
    public WindowProjection? Of(string window) =>
        Windows.FirstOrDefault(each => string.Equals(each.Window, window, StringComparison.OrdinalIgnoreCase));

    /// <summary>When the oldest of its readings was: how long the account has gone unread.</summary>
    public DateTimeOffset Oldest => Windows.Min(each => each.Seen);
}

/// <summary>Why a start asks an account's maker for its windows (USAGEPROBE1): one of the set points, never every start.</summary>
public enum ProbeWhy
{
    /// <summary>Nothing is known of it: never read, read with no number, or its readings past their reset.</summary>
    Unread,

    /// <summary>No reading for <see cref="UsageProjection.Interval"/>.</summary>
    Stale,

    /// <summary>Its projection is within <see cref="UsageProjection.Margin"/> of its curve, or over it.</summary>
    Near,

    /// <summary>A limit's word on its reading, or a limit's sentence since its reading.</summary>
    Limit,

    /// <summary>Its reading said it is drawing on usage credits.</summary>
    Credits,
}

/// <summary>
/// Each account's use projected from what Daoris already holds, and when a start must ask its maker instead (USAGEPROBE1;
/// D165's note, D179's note). A start once asked every account it could take whose reading was older than a minute (Claude) or
/// five (Codex), each a process start; now it asks only at set points, and otherwise ranks on what the readings say. Pure: the
/// readings, the turns and the moment are arguments.
/// </summary>
/// <remarks>
/// <para><b>The projection</b> is a window's last reading plus Daoris's own spend on the account since it, converted by the
/// account's own measured ratio: the points its readings rose over the reference-equivalent tokens its turns spent, inside one
/// reset and one epoch, over <see cref="Horizon"/>. Use Daoris never sees only adds to a rise, so the ratio errs high and a
/// projection asks sooner, never later. With too little history the last reading stands alone: never a guess upward from
/// nothing (D57).</para>
/// <para><b>It decides only whether to ask</b> (D130 point 9, D179 point 7): near and over stay the maker's reading's, and the
/// walk ranks on the readings as before. The ratio is not USAGECAP1b's validated estimate, which alone may order a walk.</para>
/// <para><b>The weights</b> are D179 point 3's ratios between kinds for Claude (input 1, output 5, a cache read 0.1, a cache
/// write 1.25), used for every agent until USAGECAP1b's table stands: a ratio measured per account and window absorbs its own
/// model mix, so only the kinds' proportions matter. A turn whose report's meaning is unknown (Codex's, D179 point 8) is no spend
/// it can weigh, so such an account projects as its last reading.</para>
/// </remarks>
public static class UsageProjection
{
    /// <summary>
    /// How far under its curve, in points, a projection must stay for a start to rank on it without asking: the switch's own
    /// margin (<see cref="UsageCurves.SwitchMargin"/>), within which a running session would not move to the account either.
    /// </summary>
    public const int Margin = UsageCurves.SwitchMargin;

    /// <summary>
    /// How long an account may go unread before a start asks it anyway: what Daoris never sees (the person's own sessions, the
    /// web app) is bounded only by asking. Thirty minutes is <see cref="Margin"/> at the five-hour window's even pace (100 points
    /// over 300 minutes): an account the margin under its curve crosses it within the interval only where unseen use runs faster
    /// than the whole window's even pace.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    /// <summary>How far back a ratio reads: a week, the longest window either maker resets.</summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromDays(7);

    /// <summary>The least rise, as a fraction, a ratio is read on: D179 point 4's five points, so whole-percent rounding cannot carry it.</summary>
    public const double LeastRise = 0.05;

    /// <summary>The kinds' weights (D179 point 3): input, output, a cache read and a cache write (five minutes).</summary>
    public const double InputWeight = 1, OutputWeight = 5, CacheReadWeight = 0.1, CacheWriteWeight = 1.25;

    // Two readings of one window name the same reset within this: a reset is said to the second, and a made-up one a whole
    // window on is hours from any said.
    private static readonly TimeSpan ResetSlack = TimeSpan.FromMinutes(10);

    // A fall inside one reset larger than either reader's rounding is a window begun again that did not say so.
    private const double DropSlack = 0.02;

    /// <summary>A turn's tokens in reference-equivalent tokens, or null where it said none of the four: absent is never zero (D57).</summary>
    public static double? Work(TurnTokens? tokens)
    {
        if (tokens is null || (tokens.Input, tokens.Output, tokens.CacheRead, tokens.CacheWrite) is (null, null, null, null)) return null;
        return (tokens.Input ?? 0) * InputWeight + (tokens.Output ?? 0) * OutputWeight
            + (tokens.CacheRead ?? 0) * CacheReadWeight + (tokens.CacheWrite ?? 0) * CacheWriteWeight;
    }

    /// <summary>
    /// The share of <paramref name="window"/> one reference-equivalent token of Daoris's spent on the account, over the readings
    /// and turns within <see cref="Horizon"/> of <paramref name="now"/>: each run of readings inside one reset and one epoch rose
    /// from its first to its last, beside the turns that ended between them; the rises summed over the work summed. Null where the
    /// rises come to less than <see cref="LeastRise"/> or no turn of Daoris's ran inside any run.
    /// </summary>
    /// <param name="readings">The account's readings, any window, any order.</param>
    /// <param name="turns">The account's turns whose report is each turn's own.</param>
    public static double? Ratio(IEnumerable<ReadPoint> readings, IEnumerable<SpentTurn> turns, string window, DateTimeOffset now)
    {
        var since = now - Horizon;
        var points = readings
            .Where(point => string.Equals(point.Window, window, StringComparison.OrdinalIgnoreCase) && point.Seen >= since && point.Seen <= now)
            .OrderBy(point => point.Seen)
            .ToList();
        var spent = turns.Where(turn => turn.Ended >= since && turn.Ended <= now).ToList();

        double rose = 0, work = 0;
        var start = 0;
        for (var at = 1; at <= points.Count; at++)
        {
            if (at < points.Count && SameRun(points[at - 1], points[at])) continue;

            var (first, last) = (points[start], points[at - 1]);
            if (at - 1 > start)
            {
                var between = spent.Where(turn => turn.Ended > first.Seen && turn.Ended <= last.Seen && SameEpoch(turn.Epoch, first.Epoch)).Sum(turn => turn.Work);
                if (between > 0)
                {
                    rose += Math.Max(0, last.Used - first.Used);
                    work += between;
                }
            }

            start = at;
        }

        return rose >= LeastRise && work > 0 ? rose / work : null;
    }

    /// <summary>
    /// Each window <paramref name="said"/> gives a number for, projected to <paramref name="now"/>: its reading plus the work of
    /// the turns that ended since, by its window's <see cref="Ratio"/>, at most the whole window; its reading alone where nothing
    /// was spent since or no ratio stands. Null where no window has a number.
    /// </summary>
    public static AccountProjection? Of(AccountSaid? said, IReadOnlyList<ReadPoint> readings, IReadOnlyList<SpentTurn> turns, DateTimeOffset now)
    {
        if (said is null) return null;
        var windows = new List<WindowProjection>();
        foreach (var window in said.Windows)
        {
            if (window.Used is not { } read) continue;
            var spent = turns.Where(turn => turn.Ended > window.Seen && turn.Ended <= now).Sum(turn => turn.Work);
            var ratio = spent > 0 ? Ratio(readings, turns, window.Window, now) : null;
            windows.Add(ratio is { } measured
                ? new WindowProjection(window.Window, read, Math.Max(read, Math.Min(1, read + measured * spent)), window.Seen, window.Reset, ProjectedBy.Measured, measured, spent)
                : new WindowProjection(window.Window, read, read, window.Seen, window.Reset, ProjectedBy.Reading));
        }

        return windows.Count == 0 ? null : new AccountProjection(windows);
    }

    /// <summary>The least room, in points, a projection leaves under the scope's curve now; null where no window it holds is measured.</summary>
    public static double? Room(AccountProjection? projection, RotationUse use, DateTimeOffset now)
    {
        double? least = null;
        foreach (var window in projection?.Windows ?? [])
        {
            if (UsageCurves.Value(use, window.Window, window.Reset, now) is not { } curve) continue;
            var room = curve - window.Projected * 100;
            if (least is null || room < least) least = room;
        }

        return least;
    }

    /// <summary>
    /// Whether a start owes the account's maker a question now, and why; null where it ranks on what it holds. Asked: an account
    /// nothing is known of; one whose reading says a limit is reached or credits are drawn, or that a limit's sentence followed
    /// (<paramref name="limitSaid"/>); one unread for <see cref="Interval"/>; and one whose projection is within
    /// <see cref="Margin"/> of its curve or over it, so near and over are always a reading's. The person's press asks
    /// whatever this says.
    /// </summary>
    /// <param name="limitSaid">When a limit was last said of the account, from its cool-off or this process's own hearing.</param>
    public static ProbeWhy? Owed(AccountSaid? said, AccountProjection? projection, RotationUse use, DateTimeOffset now, DateTimeOffset? limitSaid = null)
    {
        if (said is null) return ProbeWhy.Unread;
        if (said.Windows.Any(window => window.Standing == AccountReadings.RefusedWord)) return ProbeWhy.Limit;
        if (said.Windows.Any(window => window.Credits)) return ProbeWhy.Credits;
        if (limitSaid is { } limit && limit >= said.Seen) return ProbeWhy.Limit;
        if (projection is not { Windows.Count: > 0 }) return ProbeWhy.Unread;
        if (now - projection.Oldest >= Interval) return ProbeWhy.Stale;
        return Room(projection, use, now) is { } room && room < Margin ? ProbeWhy.Near : null;
    }

    private static bool SameRun(ReadPoint before, ReadPoint after) =>
        before.Reset is { } was && after.Reset is { } next && (next - was).Duration() <= ResetSlack
        && SameEpoch(before.Epoch, after.Epoch) && after.Used >= before.Used - DropSlack;

    // An epoch not read is not another: only two known and different split.
    private static bool SameEpoch(int? a, int? b) => a is null || b is null || a == b;
}
