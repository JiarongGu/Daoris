using Daoris.Assistant.Ports;
using Xunit;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The cases every <see cref="IRoomFolder"/> passes: <see cref="MemoryRoomFolder"/> in the assistant's suite, the desktop adapter over
/// the home's room folders in its own.
/// </summary>
public abstract class RoomFolderContract
{
    private static readonly CancellationToken None = CancellationToken.None;

    /// <summary>The room folders under test, none written.</summary>
    protected abstract IRoomFolder Backend { get; }

    /// <summary>The text <paramref name="room"/> holds at <paramref name="path"/>, or null where it holds none.</summary>
    protected abstract string? FileOf(string room, string path);

    private static RoomFiles Room(string name, params (string Path, string Text)[] files) =>
        new(name, files.ToDictionary(file => file.Path, file => file.Text, StringComparer.Ordinal));

    [Fact]
    public async Task A_room_not_written_says_so()
    {
        await Backend.WriteAsync(Room("one", ("AGENTS.md", "the room")), None);

        Assert.False(await Backend.WrittenAsync("two", None));
    }

    [Fact]
    public async Task A_room_written_holds_each_file()
    {
        await Backend.WriteAsync(Room("one", ("AGENTS.md", "the room"), (".claude/settings.json", "{}")), None);

        Assert.True(await Backend.WrittenAsync("one", None));
        Assert.Equal("the room", FileOf("one", "AGENTS.md"));
        Assert.Equal("{}", FileOf("one", ".claude/settings.json"));
    }

    [Fact]
    public async Task A_second_write_replaces_the_room_whole()
    {
        await Backend.WriteAsync(Room("one", ("AGENTS.md", "first"), ("CLAUDE.md", "first")), None);

        await Backend.WriteAsync(Room("one", ("AGENTS.md", "second")), None);

        Assert.Equal("second", FileOf("one", "AGENTS.md"));
        Assert.Null(FileOf("one", "CLAUDE.md"));
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData(".claude/../../outside.md")]
    [InlineData("/outside.md")]
    [InlineData("C:/outside.md")]
    [InlineData(" ")]
    public async Task A_path_that_is_blank_rooted_or_climbs_out_is_refused_and_nothing_is_written(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Backend.WriteAsync(Room("one", ("AGENTS.md", "the room"), (path, "out")), None));

        Assert.False(await Backend.WrittenAsync("one", None));
        Assert.Null(FileOf("one", "AGENTS.md"));
    }

    [Fact]
    public async Task Two_writes_at_once_leave_one_of_them_whole()
    {
        await Task.WhenAll(
            Backend.WriteAsync(Room("one", ("AGENTS.md", "first"), ("CLAUDE.md", "first")), None),
            Backend.WriteAsync(Room("one", ("AGENTS.md", "second"), ("CLAUDE.md", "second")), None));

        Assert.Contains(FileOf("one", "AGENTS.md"), new[] { "first", "second" });
        Assert.Equal(FileOf("one", "AGENTS.md"), FileOf("one", "CLAUDE.md"));
    }
}
