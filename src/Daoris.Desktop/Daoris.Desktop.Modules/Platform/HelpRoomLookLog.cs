using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// How long the room's session look took, into the machine log as <c>help.room.look</c> (SESSUX1h3b, D161's note):
/// <c>SessionGroups.LookAsync</c> (with the review count's git judgement) runs on every room write, a conversation's going
/// on included, so its cost on an installed machine is read from the log, not probed. A number and flags, never a word.
/// </summary>
/// <remarks>
/// Since HELPSTART1 (D89's note) the room reads the look this shell took last where it is no older than <see cref="Recent"/>:
/// the line then says <c>kept</c>, and its milliseconds are the read of nothing. On a loaded machine a fresh look took 24.9 s.
/// </remarks>
public static class HelpRoomLookLog
{
    /// <summary>The machine log's event (the machine log design §4).</summary>
    public const string Event = "help.room.look";

    /// <summary>
    /// Where a look is called slow: HELP5 made Ask Daoris's first answer fast, so a look that alone takes this long is
    /// a visible part of it. Over it the line is a warning with <c>slow</c> true.
    /// </summary>
    public const long SlowMs = 500;

    /// <summary>
    /// How old a look the room reads instead of a fresh one may be (HELPSTART1): the room is a reading of a moment either way, and
    /// a proposal it leads to is judged against the sessions as they are when the person applies it.
    /// </summary>
    public static readonly TimeSpan Recent = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Write one line for a look that took <paramref name="took"/>; <paramref name="read"/> is whether it was taken, and
    /// <paramref name="kept"/> whether it was the look kept from the last <see cref="Recent"/> rather than a fresh one.
    /// </summary>
    public static void Said(MachineLog? log, TimeSpan took, bool read, bool kept = false)
    {
        var ms = (long)took.TotalMilliseconds;
        var slow = ms > SlowMs;
        (string, object?)[] data = [("ms", ms), ("slow", slow), ("boundMs", SlowMs), ("read", read), ("kept", kept)];
        if (slow) log?.Warn(Event, data);
        else log?.Info(Event, data);
    }
}
