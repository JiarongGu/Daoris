using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A subagent's own record over the bridge (`SESSION_CHILD_HISTORY`): the session's page opens a subagent's row in place
/// and reads its words and calls from the record kept beside the session's, never from the session's own.
/// </summary>
public sealed class DriverModuleChildHistoryTests : DriverModuleBridge
{
    [Fact]
    public async Task A_subagents_own_record_is_read_whole_and_a_child_with_none_kept_answers_no_events()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        loop.Events.Append("s1", new SessionEvent { Kind = SessionEventKind.Child, Id = "a1", Call = "toolu_ag", Name = "find expiry tests" });
        var children = new ChildRecords(Path.GetDirectoryName(loop.Events.PathOf("s1"))!);
        children.Keep("s1", "a1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "toolu_r1", Title = "Read test/auth.test.ts" });
        children.Keep("s1", "a1", new SessionEvent { Kind = SessionEventKind.Message, Id = "m1", Text = "Two tests cover expiry." });

        var read = await AnswerAsync(module, "SESSION_CHILD_HISTORY", new { id = "s1", child = "a1" });

        Assert.Equal(("s1", "a1"), (read.GetProperty("session").GetString(), read.GetProperty("child").GetString()));
        Assert.Equal(["tool", "message"], read.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("kind").GetString()));
        Assert.Equal([1L, 2L], read.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("seq").GetInt64()));
        // The session's own record is not the child's: its start stays there alone.
        var none = await AnswerAsync(module, "SESSION_CHILD_HISTORY", new { id = "s1", child = "a9" });
        Assert.False(none.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array);
    }
}
