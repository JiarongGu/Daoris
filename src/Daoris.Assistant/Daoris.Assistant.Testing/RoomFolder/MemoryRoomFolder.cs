using Daoris.Assistant.Ports;

namespace Daoris.Assistant.Testing;

/// <summary>The room folders in memory: each write checked whole first, then swapped in whole under one gate.</summary>
public sealed class MemoryRoomFolder : IRoomFolder
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _rooms = new(StringComparer.Ordinal);

    public Task WriteAsync(RoomFiles room, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(room.Room)) throw new ArgumentException("a room is written under its name, and this one is blank", nameof(room));
        var files = room.Files.ToDictionary(file => InsidePath.Require(file.Key, nameof(room)), file => file.Value, StringComparer.Ordinal);
        lock (_gate) _rooms[room.Room] = files;
        return Task.CompletedTask;
    }

    public Task<bool> WrittenAsync(string room, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_rooms.ContainsKey(room));
    }

    /// <summary>The text <paramref name="room"/> holds at <paramref name="path"/>, or null.</summary>
    public string? FileOf(string room, string path)
    {
        lock (_gate) return _rooms.TryGetValue(room, out var files) ? files.GetValueOrDefault(path.Replace('\\', '/')) : null;
    }
}
