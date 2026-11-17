namespace Daoris.Assistant.Ports;

/// <summary>
/// The room folders a conversation starts in: a room's files written whole under the room's own gate, so a reader never sees two
/// writes mixed, and whether a room was written.
/// </summary>
public interface IRoomFolder
{
    /// <summary>
    /// Writes <paramref name="room"/>'s files whole: after it the room holds those files and no other. A path that is blank, rooted or
    /// climbs out of the room is refused (<see cref="ArgumentException"/>) and nothing is written.
    /// </summary>
    Task WriteAsync(RoomFiles room, CancellationToken cancel);

    /// <summary>Whether <paramref name="room"/> was written.</summary>
    Task<bool> WrittenAsync(string room, CancellationToken cancel);
}

/// <summary>A room's files: the room's name and each file's text by its path within the room, with forward slashes.</summary>
public sealed record RoomFiles(string Room, IReadOnlyDictionary<string, string> Files);
