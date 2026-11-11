using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a part spent is read once and then only as far as its record grew: the Parts strip asks it of every part of a line
/// as a session opens, on the window's thread, and a long conversation's whole record took most of a second to read again.
/// </summary>
public sealed class SessionSpentKeptTests : IDisposable
{
    private readonly string _directory = Path.Combine(TestHomes.Root, "daoris-spent-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static SessionEvent Turn(long input) => new() { Kind = SessionEventKind.Turn, Tokens = new(Input: input, Output: 1, CacheRead: null, CacheWrite: null) };

    [Fact]
    public void A_second_read_takes_only_the_turns_written_since()
    {
        var events = new SessionEvents(_directory);
        events.Append("kept1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "go" });
        events.Append("kept1", Turn(4));
        events.Append("kept1", Turn(6));
        Assert.Equal(new SessionSpent(2, 2, 10, 2, null, null), events.Spent("kept1"));

        // The first turn's line blanked where it stands: a read of the whole record again would no longer count it.
        Blank(events.PathOf("kept1"), "\"input\":4");
        events.Append("kept1", Turn(5));

        Assert.Equal(new SessionSpent(3, 3, 15, 3, null, null), events.Spent("kept1"));
    }

    [Fact]
    public void A_record_written_again_from_its_start_is_read_whole()
    {
        var events = new SessionEvents(_directory);
        events.Append("again1", Turn(4));
        events.Append("again1", Turn(6));
        Assert.Equal(10, events.Spent("again1").Input);

        events.Forget("again1");
        events.Append("again1", Turn(7));
        Assert.Equal(new SessionSpent(1, 1, 7, 1, null, null), events.Spent("again1"));

        // Replaced by a longer record whose end the last read never saw: read whole, never added to what was kept.
        var path = events.PathOf("again1");
        var other = new SessionEvents(Path.Combine(_directory, "other"));
        foreach (var input in new long[] { 1, 2, 3 }) other.Append("again1", Turn(input));
        File.Copy(other.PathOf("again1"), path, overwrite: true);

        Assert.Equal(new SessionSpent(3, 3, 6, 3, null, null), events.Spent("again1"));
    }

    [Fact]
    public void A_torn_last_line_is_counted_once_it_is_whole()
    {
        var events = new SessionEvents(_directory);
        events.Append("torn1", Turn(4));
        var path = events.PathOf("torn1");
        var line = File.ReadAllText(path).Replace("\"input\":4", "\"input\":9", StringComparison.Ordinal).Replace("\"seq\":1", "\"seq\":2", StringComparison.Ordinal);
        File.AppendAllText(path, line[..(line.Length / 2)]);

        Assert.Equal(new SessionSpent(1, 1, 4, 1, null, null), events.Spent("torn1"));

        File.AppendAllText(path, line[(line.Length / 2)..]);

        Assert.Equal(new SessionSpent(2, 2, 13, 2, null, null), events.Spent("torn1"));
    }

    /// <summary>Overwrite the line holding <paramref name="text"/> with spaces, its length and its newline kept.</summary>
    private static void Blank(string path, string text)
    {
        var lines = File.ReadAllText(path).Split('\n');
        var at = Array.FindIndex(lines, line => line.Contains(text, StringComparison.Ordinal));
        lines[at] = new string(' ', Encoding.UTF8.GetByteCount(lines[at]));
        File.WriteAllText(path, string.Join('\n', lines));
    }
}
