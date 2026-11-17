namespace Daoris.Assistant.Tests;

/// <summary>The room folders' contract on the memory fake.</summary>
public sealed class MemoryRoomFolderContractTests : RoomFolderContract
{
    private readonly MemoryRoomFolder _rooms = new();

    protected override IRoomFolder Backend => _rooms;

    protected override string? FileOf(string room, string path) => _rooms.FileOf(room, path);
}
