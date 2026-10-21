namespace Daoris.Driver;

/// <summary>
/// The usage curve, measured (SWITCH1b-i; the switch design §2; D165 point 2): how much of each window an account may have
/// used by now, and how much room it has left under that, so the walk (SWITCH1b-ii) can rank accounts by the person's curve
/// or the smart one. Pure: the moment comes in as a value, and nothing is read.
/// </summary>
/// <remarks>
/// A reading's <c>Used</c> is a fraction; a curve and a room are in percent points. A window with no reading is not measured
/// (absent is never zero, D57): an account with no readings has no room figure, and the walk ranks it between, as today.
/// The windows' lengths are also held in <c>ClaudeUsage</c> and <c>CodexUsage</c> (for the reads' own use) and in
/// <c>AccountWindows.Week</c>; this one is the curve's.
/// </remarks>
public static class UsageCurves
{
    /// <summary>
    /// The least room, in points under its curve on every window it said, an account must have for a running session to move
    /// to it (the switch design §2; <see cref="SwitchDecision"/>, SWITCH1c-i).
    /// </summary>
    public const int SwitchMargin = 10;

    /// <summary>
    /// How near its reset a week must be for what would lapse there to rank its account first (D130 §16.3 step 4, as
    /// USAGERESET1 weighs it): a day holds about five five-hour windows, so before an account's last day what it has left can
    /// still be spent in any order. A constant, not a setting; TOOL4h's report says whether it is right.
    /// </summary>
    public static readonly TimeSpan LastDay = TimeSpan.FromDays(1);

    // A read places a week not yet opened a whole week after its own moment (`ClaudeUsage`), a few seconds before the reading
    // is stamped `seen`, both to the second: a week read at 0% with its reset this close to a week on is that made-up reset.
    private static readonly TimeSpan MadeUp = TimeSpan.FromMinutes(1);

    // The smart week: its pace plus the lead up to the knee, then evenly to the end at its reset (USAGERESET1). The CLI's copy
    // left with `usagecurves.ts` and `daoris agent list` (PUSH1f2e4). The page holds none since PUSH1e: the accounts' answer
    // carries each scope's curve in effect (`AccountsRoutes`).
    private const int WeeklyLead = 20, WeeklyKnee = 95, WeeklyEnd = 100;

    /// <summary>A window's length, or null for a window this build does not know.</summary>
    public static TimeSpan? Length(string window) => window switch
    {
        "session" => TimeSpan.FromHours(5),
        "weekly" => TimeSpan.FromDays(7),
        _ => null,
    };

    /// <summary>
    /// When a window began: its reset less its length, or null for a window this build does not know (PUSH1e). The page places an
    /// account on a window's chart by it, so it never holds a window's length of its own.
    /// </summary>
    public static DateTimeOffset? From(string window, DateTimeOffset reset) => Length(window) is { } length ? reset - length : null;

    /// <summary>
    /// A curve's value, or a room, in whole points, a half away from zero, as every reader says it (PUSH1e): the accounts' answer the
    /// page words, and the start's line (<c>AccountRotation.Points</c>); the shared table's said clause holds the rule.
    /// </summary>
    public static int Points(double points) => (int)Math.Round(points, MidpointRounding.AwayFromZero);

    /// <summary>How much of the window has passed at <paramref name="now"/>, from its reset: 0 to 100, or null for an unknown window.</summary>
    public static double? Elapsed(string window, DateTimeOffset reset, DateTimeOffset now) =>
        Length(window) is { } length
            ? Math.Clamp(1 - (reset - now) / length, 0, 1) * 100
            : null;

    /// <summary>The curve's value at an elapsed share: straight lines between points, the first's used before the first, the last's after the last.</summary>
    public static double Value(IReadOnlyList<CurvePoint> points, double elapsed)
    {
        if (elapsed <= points[0].Elapsed) return points[0].Used;
        for (var i = 1; i < points.Count; i++)
        {
            var (from, to) = (points[i - 1], points[i]);
            if (elapsed > to.Elapsed) continue;
            return to.Elapsed == from.Elapsed
                ? to.Used
                : from.Used + (to.Used - from.Used) * (elapsed - from.Elapsed) / (to.Elapsed - from.Elapsed);
        }

        return points[^1].Used;
    }

    /// <summary>
    /// Daoris's own curve: <c>session</c> flat at <see cref="RotationUse.SmartSession"/>; <c>weekly</c> its pace plus twenty up
    /// to ninety-five at three-quarters gone, then rising evenly to the whole week at its reset (USAGERESET1): never in a jump,
    /// so what would lapse is spent as the reset nears while some headroom stays at every moment before it. Null for a
    /// window this build does not know.
    /// </summary>
    public static IReadOnlyList<CurvePoint>? Smart(string window) => window switch
    {
        "session" => [new CurvePoint(0, RotationUse.SmartSession)],
        "weekly" => [new CurvePoint(0, WeeklyLead), new CurvePoint(WeeklyKnee - WeeklyLead, WeeklyKnee), new CurvePoint(100, WeeklyEnd)],
        _ => null,
    };

    /// <summary>
    /// A scope's curve for a window: under <c>curve</c>, the person's points where they set the window, else the smart curve;
    /// under <c>smart</c> and <c>off</c> the smart curve (<c>off</c> still reads it, for passing an account at a start, §3).
    /// </summary>
    public static IReadOnlyList<CurvePoint>? For(RotationUse use, string window) =>
        use.Switch == "curve" && use.Curve[window] is { Count: > 0 } own ? own : Smart(window);

    /// <summary>
    /// The scope's curve on a window now: the most of it, in percent, an account should have used by <paramref name="now"/>
    /// in a window resetting at <paramref name="reset"/>; null for a window this build does not know.
    /// </summary>
    public static double? Value(RotationUse use, string window, DateTimeOffset reset, DateTimeOffset now) =>
        For(use, window) is { } points && Elapsed(window, reset, now) is { } elapsed ? Value(points, elapsed) : null;

    /// <summary>The points of room a window's reading leaves under the curve now, or null where it is not measured (no used, or an unknown window).</summary>
    public static double? Room(RotationUse use, WindowReading reading, DateTimeOffset now) =>
        reading.Used is { } used && Value(use, reading.Window, reading.Reset, now) is { } curve ? curve - used * 100 : null;

    /// <summary>The least room over the windows with a reading, or null where none is measured.</summary>
    public static double? Tightest(RotationUse use, IEnumerable<WindowReading> readings, DateTimeOffset now)
    {
        double? least = null;
        foreach (var reading in readings)
        {
            if (Room(use, reading, now) is { } room && (least is null || room < least)) least = room;
        }

        return least;
    }

    /// <summary>Whether the account is over its curve: its tightest room is below zero.</summary>
    public static bool Over(RotationUse use, IEnumerable<WindowReading> readings, DateTimeOffset now) =>
        Tightest(use, readings, now) is < 0;

    /// <summary>
    /// The window an account's agent said with the least room under its curve now, the curve's value there and that room, as
    /// the walk ranks it and its reasons say it (SWITCH1b-ii, the switch design §3); null where no window it said is measured.
    /// </summary>
    public static (WindowSaid Window, double Curve, double Room)? TightestSaid(RotationUse use, AccountSaid? said, DateTimeOffset now)
    {
        (WindowSaid Window, double Curve, double Room)? least = null;
        foreach (var window in said?.Windows ?? [])
        {
            if (window.Used is not { } used || Value(use, window.Window, window.Reset, now) is not { } curve) continue;
            var room = curve - used * 100;
            if (least is null || room < least.Value.Room) least = (window, curve, room);
        }

        return least;
    }

    /// <summary>
    /// What would lapse of an account's week at its reset, as the walk's step 4 weighs it (USAGERESET1, D165's note): within
    /// its <see cref="LastDay"/>, the week's room under the curve now and that room over the hours left to its reset, an hour
    /// at the least, so a sliver resetting in minutes does not outrank a large room a day off. Null where none is known.
    /// </summary>
    /// <remarks>
    /// <para><b>Only the whole plan's week</b>, <c>weekly</c>: the five-hour window recovers in hours, so its reset never
    /// lapses anything, and a model's own week draws only that model's work, which a start may not be (the walk reads neither).
    /// Credits never count.</para>
    /// <para><b>Nothing that is not known earns any</b> (absent is never zero, D57): a week with no reading or no number; a
    /// reset carried a week on, which has no reading; one now or past, whose week has already turned over; a reading older
    /// than the account's newest window, since the account ran after it; and a week read at 0% whose reset a read made up a
    /// week on, since one not yet opened lapses nothing. An explicit zero with its reset said is a week all unused, and earns
    /// its whole room.</para>
    /// </remarks>
    public static (WindowSaid Window, double Room, double Rate)? Lapsing(RotationUse use, AccountSaid? said, DateTimeOffset now)
    {
        if (said?.Of(AccountWindows.Weekly) is not { Used: { } used } week) return null;
        var left = week.Reset - now;
        if (left <= TimeSpan.Zero || left > LastDay || week.Seen < said.Seen) return null;
        if (used == 0 && Length(week.Window) is { } length && week.Reset - week.Seen >= length - MadeUp) return null;
        if (Value(use, week.Window, week.Reset, now) is not { } curve) return null;
        var room = curve - used * 100;

        return room > 0 ? (week, room, room / Math.Max(left.TotalHours, 1)) : null;
    }
}
