namespace Daoris.Driver;

/// <summary>
/// The one thing Ask Daoris's conversations share: a workspace's room folder, whose files every open and go-on writes whole.
/// Two conversations of one workspace run at once, so two writes of one room can meet; each takes the room's own gate for its
/// write, and no conversation's turn waits on it.
/// </summary>
internal static class HelpRoomWrites
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The gate of the room at <paramref name="room"/>, one per folder whatever its spelling.</summary>
    public static object Of(string room) => Gates.GetOrAdd(Path.GetFullPath(room), _ => new object());
}
