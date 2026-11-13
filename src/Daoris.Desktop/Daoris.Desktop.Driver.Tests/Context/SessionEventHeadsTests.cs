using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A long record is read through its head in the desktop store, and every read answers what the file read whole answers: a head is built
/// by the first reader, brought up by the next that finds the file longer, built again where the file is not the one it read, and let go
/// with the record.
/// </summary>
public sealed class SessionEventHeadsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-heads-" + Guid.NewGuid().ToString("N")[..8]);

    // The same bytes in a folder that is no home's: read whole, the answer to compare with.
    private readonly string _whole;

    private readonly SessionEvents _headed;

    private readonly SessionEvents _plain;

    public SessionEventHeadsTests()
    {
        _headed = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        _whole = Path.Combine(_home, "whole");
        _plain = new SessionEvents(_whole);
        Directory.CreateDirectory(_whole);
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        SessionEventHeads.LetGoOfMemory();
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static SessionEvent[] Record(int lines)
    {
        var events = new List<SessionEvent> { new() { Kind = SessionEventKind.Note, Text = "start" }, new() { Kind = SessionEventKind.User, Origin = "person", Text = "go", Id = "w1", Choice = "yes" } };
        for (var line = 0; line < lines; line++)
        {
            events.Add((line % 50) switch
            {
                3 => new SessionEvent { Kind = SessionEventKind.Tool, Id = "call" + line, Text = "running" },
                4 => new SessionEvent { Kind = SessionEventKind.Tool, Id = "call" + (line - 1), Status = "failed", Text = "failed" },
                10 => new SessionEvent { Kind = SessionEventKind.Plan, Entries = [new PlanEntry("step " + line)] },
                49 => new SessionEvent { Kind = SessionEventKind.Turn, Tokens = new(Input: 3, Output: 1, CacheRead: null, CacheWrite: null) },
                _ => new SessionEvent { Kind = SessionEventKind.Message, Text = new string('m', 300) },
            });
        }

        return [.. events];
    }

    private static void Write(string path, IEnumerable<SessionEvent> events, long from = 1)
    {
        var seq = from;
        File.AppendAllLines(path, events.Select(e => JsonSerializer.Serialize(e with { Seq = seq, At = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(seq++) }, SessionEventText.Json)));
    }

    private void Make(string id, int lines)
    {
        Write(_headed.PathOf(id), Record(lines));
        File.Copy(_headed.PathOf(id), _plain.PathOf(id), overwrite: true);
    }

    private static string Said(object? value) => JsonSerializer.Serialize(value, SessionEventText.Json);

    private void AssertSame(string id)
    {
        foreach (var (before, limit) in new (long?, int)[] { (null, 200), (null, 1), (5000, 200), (300, 50), (3, 10), (1_000_000, 7) })
        {
            Assert.Equal(Said(_plain.Page(id, before, limit)), Said(_headed.Page(id, before, limit)));
        }

        foreach (var after in new long[] { 0, 1, 255, 256, 257, 4000, 9000 })
        {
            Assert.Equal(Said(_plain.After(id, after)), Said(_headed.After(id, after)));
        }

        Assert.Equal(_plain.TurnsEnded(id), _headed.TurnsEnded(id));
        Assert.Equal(_plain.Spent(id), _headed.Spent(id));
        Assert.Equal(Said(_plain.ChoicesOf(id)), Said(_headed.ChoicesOf(id)));
        Assert.Equal(Said(_plain.LastPlan(id)), Said(_headed.LastPlan(id)));
    }

    [Fact]
    public void A_long_record_read_through_its_head_answers_what_the_whole_file_answers()
    {
        Make("long1", 4000);
        Assert.True(new FileInfo(_headed.PathOf("long1")).Length > SessionEventHeads.HeadedFrom);

        AssertSame("long1");

        // The head is the store's now: a process come up since reads it from there, and answers the same.
        SessionEventHeads.LetGoOfMemory();
        AssertSame("long1");
    }

    [Fact]
    public void A_head_is_brought_up_by_the_events_written_since()
    {
        Make("grown1", 4000);
        AssertSame("grown1");

        var more = Record(300).Skip(2);
        Write(_headed.PathOf("grown1"), more, from: _headed.Page("grown1", limit: 1).Latest + 1);
        File.Copy(_headed.PathOf("grown1"), _plain.PathOf("grown1"), overwrite: true);

        AssertSame("grown1");
    }

    [Fact]
    public void A_record_that_is_not_the_one_the_head_read_is_read_again()
    {
        Make("again1", 4000);
        AssertSame("again1");

        // The same record written again from its start by another, shorter one: the last line the head read no longer stands.
        File.Delete(_headed.PathOf("again1"));
        Write(_headed.PathOf("again1"), Record(3000).Select(e => e.Kind == SessionEventKind.Message ? e with { Text = new string('n', 300) } : e));
        File.Copy(_headed.PathOf("again1"), _plain.PathOf("again1"), overwrite: true);

        AssertSame("again1");
    }

    [Fact]
    public void What_a_part_spent_is_the_heads_and_never_a_read_of_the_whole_record()
    {
        Make("spent1", 4000);
        var turns = _headed.TurnsEnded("spent1");
        Assert.True(turns > 1);

        // The first turn's line blanked where it stands, in a process come up since: only a head built before it still counts it.
        var path = _headed.PathOf("spent1");
        var lines = File.ReadAllText(path).Split('\n');
        var at = Array.FindIndex(lines, line => line.Contains("\"kind\":\"turn\"", StringComparison.Ordinal));
        lines[at] = new string(' ', System.Text.Encoding.UTF8.GetByteCount(lines[at]));
        File.WriteAllText(path, string.Join('\n', lines));
        SessionEventHeads.LetGoOfMemory();

        Assert.Equal(new SessionSpent((int)turns, (int)turns, 3 * turns, turns, null, null), _headed.Spent("spent1"));
    }

    [Fact]
    public void A_record_forgotten_leaves_no_head()
    {
        Make("gone1", 4000);
        AssertSame("gone1");

        using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(_home))!;
        Assert.NotEmpty(store.Query("SELECT session FROM session_event_heads WHERE session = 'gone1'", row => row.GetString(0)));
        Assert.NotEmpty(store.Query("SELECT session FROM session_event_offsets WHERE session = 'gone1'", row => row.GetString(0)));

        Assert.True(_headed.Forget("gone1"));

        Assert.Empty(store.Query("SELECT session FROM session_event_heads WHERE session = 'gone1'", row => row.GetString(0)));
        Assert.Empty(store.Query("SELECT session FROM session_event_offsets WHERE session = 'gone1'", row => row.GetString(0)));
    }

    [Fact]
    public void A_short_record_is_read_whole_and_headed_nowhere()
    {
        Make("short1", 100);

        AssertSame("short1");

        using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(_home));
        Assert.True(store is null || store.Query("SELECT session FROM session_event_heads", row => row.GetString(0)).Count == 0);
    }
}
