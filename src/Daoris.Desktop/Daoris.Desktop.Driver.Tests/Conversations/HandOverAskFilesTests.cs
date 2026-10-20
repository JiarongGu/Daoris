using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1c: the agent's ask to hand over and the driver's state, two files per session under the home's sessions folder. The ask
/// is read as the connector's <c>handover_propose</c> writes it, the state is written as the service's <c>HandOverAskBox</c> reads it
/// (its <c>McpHandOverToolTests</c> hold the same shapes), and neither is a hand-over's box, which whoever runs the session takes.
/// </summary>
public sealed class HandOverAskFilesTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "hand-over-asks", Guid.NewGuid().ToString("N")[..8]);

    public HandOverAskFilesTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private void Put(string name, string text) => File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, name), text);

    [Fact]
    public void An_ask_is_read_as_handover_propose_writes_it_and_an_unreadable_one_is_none()
    {
        Put("s1.hand-over-ask.json", """
            {
              "id": "a1b2c3d4",
              "session": "s1",
              "note": "The parser is done; ParserTests are left.",
              "askedBy": "agent",
              "at": "2026-10-20T10:00:00.0000000+00:00"
            }
            """);
        Put("s2.hand-over-ask.json", "{ not json");
        Put("s3.hand-over-ask.json", """{ "id": "c1", "session": "s3" }""");
        var files = new HandOverAskFiles(_home);

        var ask = Assert.Single(files.Asks());

        Assert.Equal(("a1b2c3d4", "s1", "The parser is done; ParserTests are left."), (ask.Id, ask.Session, ask.Note));
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T10:00:00Z"), ask.At);
        Assert.Equal(ask, files.Ask("s1"));
        Assert.Null(files.Ask("s2"));
        Assert.Empty(new HandOverBox(_home).Pending());
    }

    /// <summary>The state is the driver's alone, written whole and atomically in the shape the service reads; read back only for its ask.</summary>
    [Fact]
    public void The_state_is_written_as_the_service_reads_it_and_read_back()
    {
        var files = new HandOverAskFiles(_home);
        files.WriteState("s1", new HandOverAskState("a1b2c3d4", HandOverAskStates.Waiting, null, "codex-acp", DateTimeOffset.Parse("2026-10-20T10:00:00Z")));

        var text = File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.hand-over-state.json"));
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        Assert.Equal(("a1b2c3d4", "waiting", JsonValueKind.Null, "codex-acp"),
            (root.GetProperty("id").GetString(), root.GetProperty("state").GetString(), root.GetProperty("code").ValueKind, root.GetProperty("agent").GetString()));
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("at").GetString(), out _));
        Assert.DoesNotContain("\r", text);
        Assert.Equal(HandOverAskStates.Waiting, files.ReadState("s1")!.State);

        files.WriteState("s1", new HandOverAskState("a1b2c3d4", HandOverAskStates.Refused, HandOverAskWhy.Off, null, DateTimeOffset.UtcNow));
        using var refused = JsonDocument.Parse(File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.hand-over-state.json")));
        Assert.Equal(("refused", "off", JsonValueKind.Null),
            (refused.RootElement.GetProperty("state").GetString(), refused.RootElement.GetProperty("code").GetString(), refused.RootElement.GetProperty("agent").ValueKind));
        Put("s9.hand-over-state.json", """{ "id": "x", "state": "sleeping" }""");
        Assert.Null(files.ReadState("s9"));
    }

    /// <summary>A session's two files go together: a session gone from this machine leaves nothing behind.</summary>
    [Fact]
    public void Clearing_a_session_takes_both_files()
    {
        Put("s1.hand-over-ask.json", """{ "id": "a1", "session": "s1", "note": "n", "askedBy": "agent", "at": "2026-10-20T10:00:00Z" }""");
        var files = new HandOverAskFiles(_home);
        files.WriteState("s1", new HandOverAskState("a1", HandOverAskStates.Refused, HandOverAskWhy.AskerGone, null, DateTimeOffset.UtcNow));

        files.Clear("s1");

        Assert.Empty(Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Sessions)));
    }
}
