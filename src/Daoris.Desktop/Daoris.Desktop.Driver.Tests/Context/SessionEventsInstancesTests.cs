using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A session's record numbered by every instance that writes it: the driver builds one per caller, over one folder.</summary>
public sealed class SessionEventsInstancesTests : IDisposable
{
    private readonly string _directory = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-events-instances", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_directory);
    }

    private static SessionEvent Message(string text) => new() { Kind = SessionEventKind.Message, Text = text };

    [Fact]
    public void Instances_alternating_appends_over_one_folder_number_every_event_once()
    {
        var first = new SessionEvents(_directory);
        var second = new SessionEvents(_directory);

        var seqs = new List<long>
        {
            first.Append("a1b2c3", Message("one")).Seq,
            second.Append("a1b2c3", Message("two")).Seq,
            first.Append("a1b2c3", Message("three")).Seq,
            second.Append("a1b2c3", Message("four")).Seq,
        };

        Assert.Equal([1L, 2L, 3L, 4L], seqs);
        Assert.Equal(["one", "two", "three", "four"], first.Page("a1b2c3").Events.Select(e => e.Text));
    }

    [Fact]
    public void An_event_after_a_torn_last_line_starts_its_own_line()
    {
        var events = new SessionEvents(_directory);
        events.Append("a1b2c3", Message("one"));
        File.AppendAllText(events.PathOf("a1b2c3"), "{\"seq\":2,\"kind\":\"mess");

        events.Append("a1b2c3", Message("three"));

        Assert.Equal(["one", "three"], new SessionEvents(_directory).Page("a1b2c3").Events.Select(e => e.Text));
    }
}
