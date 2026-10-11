using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// How long the room's session look took, into the machine log as <c>help.room.look</c> (SESSUX1h3b, D161's note):
/// <c>SessionGroups.LookAsync</c> (with the review count's git judgement) runs on every room write, a conversation's going
/// on included, so its cost on an installed machine is read from the log, not probed. A number and flags, never a word.
/// </summary>
public static class HelpRoomLookLog
{
    /// <summary>The machine log's event (the machine log design §4).</summary>
    public const string Event = "help.room.look";

    /// <summary>
    /// Where a look is called slow: HELP5 made Ask Daoris's first answer fast, so a look that alone takes this long is
    /// a visible part of it. Over it the line is a warning with <c>slow</c> true.
    /// </summary>
    public const long SlowMs = 500;

    /// <summary>Write one line for a look that took <paramref name="took"/>; <paramref name="read"/> is whether it was taken.</summary>
    public static void Said(MachineLog? log, TimeSpan took, bool read)
    {
        var ms = (long)took.TotalMilliseconds;
        var slow = ms > SlowMs;
        (string, object?)[] data = [("ms", ms), ("slow", slow), ("boundMs", SlowMs), ("read", read)];
        if (slow) log?.Warn(Event, data);
        else log?.Info(Event, data);
    }
}
