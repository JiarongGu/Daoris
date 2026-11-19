using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation's read mark on this machine: the newest part and event the person had on screen, one row by the conversation
/// in the desktop store, which moves only forward, so every window that opens the conversation lands where the person left off.
/// </summary>
public sealed class ReadMarksTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "read-marks", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-19T10:00:00Z");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private ReadMarks Marks => new(_home);

    [Fact]
    public void A_conversation_never_read_has_no_mark()
    {
        Assert.Null(Marks.Read("s1"));
    }

    [Fact]
    public void The_mark_moves_on_within_a_part_and_never_back()
    {
        Marks.Move("s1", new ReadMark("s1", 12, At, At.AddMinutes(1), 1));

        var back = Marks.Move("s1", new ReadMark("s1", 9, At.AddMinutes(-2), At.AddMinutes(5), 2));
        var on = Marks.Move("s1", new ReadMark("s1", 30, At.AddMinutes(4), At.AddMinutes(6), 2));

        Assert.Equal(12, back.Seq);
        Assert.Equal(1, back.Steps);
        Assert.Equal(new ReadMark("s1", 30, At.AddMinutes(4), At.AddMinutes(6), 2), on);
        Assert.Equal(on, Marks.Read("s1"));
    }

    [Fact]
    public void The_same_event_again_moves_nothing()
    {
        Marks.Move("s1", new ReadMark("s1", 12, At, At.AddMinutes(1), null));

        var again = Marks.Move("s1", new ReadMark("s1", 12, At, At.AddMinutes(9), null));

        Assert.Equal(At.AddMinutes(1), again.Read);
    }

    /// <summary>Another part of the line is ordered by its event's time: a later part's event moves the mark, an earlier part's never.</summary>
    [Fact]
    public void Another_part_moves_the_mark_only_where_its_event_is_later()
    {
        Marks.Move("s1", new ReadMark("s2", 4, At, At, null));

        var earlier = Marks.Move("s1", new ReadMark("s1", 900, At.AddSeconds(-1), At.AddMinutes(1), null));
        var later = Marks.Move("s1", new ReadMark("s3", 2, At.AddMinutes(3), At.AddMinutes(4), null));

        Assert.Equal("s2", earlier.Part);
        Assert.Equal(new ReadMark("s3", 2, At.AddMinutes(3), At.AddMinutes(4), null), later);
    }

    [Fact]
    public void Each_conversation_keeps_its_own_mark()
    {
        Marks.Move("s1", new ReadMark("s1", 3, At, At, null));
        Marks.Move("h7", new ReadMark("h7", 8, At, At, null));

        Assert.Equal(3, Marks.Read("s1")!.Seq);
        Assert.Equal(8, Marks.Read("h7")!.Seq);
    }

    [Fact]
    public void An_id_that_names_no_conversation_is_refused()
    {
        Assert.Throws<DriverException>(() => Marks.Move("../x", new ReadMark("s1", 3, At, At, null)));
        Assert.Throws<DriverException>(() => Marks.Move("s1", new ReadMark("../x", 3, At, At, null)));
        Assert.Null(Marks.Read("../x"));
    }
}
