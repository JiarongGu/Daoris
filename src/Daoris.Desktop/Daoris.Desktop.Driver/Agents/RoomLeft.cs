namespace Daoris.Driver;

/// <summary>
/// How much room an account has left in the tightest window its agent said, as a start weighs it before it counts running
/// sessions: a session started on an account near the end of a window meets its limit within minutes whatever else it runs.
/// </summary>
/// <remarks>
/// Every window the account's provider reported takes part, by what the reading says and not by the window's name. A window
/// the curve table has a curve for leaves its points under that curve; any other leaves what is unused of it, so a window a
/// provider declares later counts without the walk knowing it. A window with no number says nothing (absent is never zero).
/// </remarks>
public static class RoomLeft
{
    /// <summary>
    /// Points of room that count as one: readings are whole percents taken minutes apart, so a few points apart is noise
    /// and not a reason to leave the account running fewer sessions. Rooms are compared by the band they fall in, which keeps
    /// the ranking a total preorder where a tolerance between pairs would not be transitive.
    /// </summary>
    public const double Band = 5;

    /// <summary>The window with the least room, and that room in points, or null where no window said a number.</summary>
    public static (WindowSaid Window, double Room)? Tightest(RotationUse use, AccountSaid? said, DateTimeOffset now)
    {
        (WindowSaid Window, double Room)? least = null;
        foreach (var window in said?.Windows ?? [])
        {
            if (window.Used is not { } used) continue;
            var room = (UsageCurves.Value(use, window.Window, window.Reset, now) ?? 100) - used * 100;
            if (least is null || room < least.Value.Room) least = (window, room);
        }

        return least;
    }

    /// <summary>The band an account's room falls in; an account that said no number ranks as one with no room.</summary>
    public static double BandOf(RotationUse use, AccountSaid? said, DateTimeOffset now) =>
        Tightest(use, said, now) is { } tightest ? Math.Floor(tightest.Room / Band) : 0;
}
