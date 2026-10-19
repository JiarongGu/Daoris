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

    // The smart week: its pace plus the lead up to the knee, then evenly to the end at its reset (USAGERESET1). Duplicated
    // deliberately in the CLI's and the page's `usagecurves.ts` / `usageCurves.ts`; the shared table holds the three as one.
    private const int WeeklyLead = 20, WeeklyKnee = 95, WeeklyEnd = 100;

    /// <summary>A window's length, or null for a window this build does not know.</summary>
    public static TimeSpan? Length(string window) => window switch
    {
        "session" => TimeSpan.FromHours(5),
        "weekly" => TimeSpan.FromDays(7),
        _ => null,
    };

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
}
