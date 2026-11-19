using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A conversation's read mark over the bridge: <c>READ_MARK</c> answers where the person left it on this machine, none before they read
/// it, and <c>READ_MARK_MOVE</c> moves it to the newest event they had on screen, only ever forward, answering the mark that stands.
/// </summary>
public sealed class DriverModuleReadMarkTests : DriverModuleBridge
{
    [Fact]
    public async Task The_mark_is_read_moved_forward_and_never_back()
    {
        var module = new DriverModule(Bus, Loop());

        var none = await AnswerAsync(module, "READ_MARK", new { conversation = "s1" });
        Assert.Equal(JsonValueKind.Null, none.GetProperty("mark").ValueKind);

        await AnswerAsync(module, "READ_MARK_MOVE",
            new { conversation = "s1", part = "s2", seq = 40, at = "2026-11-19T10:04:00Z", steps = 2 });
        var back = await AnswerAsync(module, "READ_MARK_MOVE", new { conversation = "s1", part = "s2", seq = 12, at = "2026-11-19T10:01:00Z" });
        var read = await AnswerAsync(module, "READ_MARK", new { conversation = "s1" });

        Assert.Equal(40, back.GetProperty("mark").GetProperty("seq").GetInt64());
        var mark = read.GetProperty("mark");
        Assert.Equal(("s1", "s2", 40L, 2), (read.GetProperty("conversation").GetString(), mark.GetProperty("part").GetString(),
            mark.GetProperty("seq").GetInt64(), mark.GetProperty("steps").GetInt32()));
        Assert.StartsWith("2026-11-19T10:04:00", mark.GetProperty("at").GetString());
        Assert.True(mark.TryGetProperty("read", out _));
    }

    [Fact]
    public async Task A_mark_naming_no_conversation_or_no_time_is_refused()
    {
        var module = new DriverModule(Bus, Loop());

        Assert.Contains("no conversation", await RefusalAsync(module, "READ_MARK_MOVE",
            new { conversation = "../x", part = "s1", seq = 3, at = "2026-11-19T10:00:00Z" }));
        Assert.Contains("time", await RefusalAsync(module, "READ_MARK_MOVE", new { conversation = "s1", part = "s1", seq = 3, at = "soon" }));
    }
}
