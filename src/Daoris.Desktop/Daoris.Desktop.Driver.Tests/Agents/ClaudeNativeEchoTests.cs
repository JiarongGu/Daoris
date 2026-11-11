using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Words sent now on Claude Code's native wire: framed as one user line with no priority under a uuid, and echoed under it where the
/// binary takes them (2.1.322 under <c>--replay-user-messages</c>, measured). In process, so the fast half.
/// </summary>
public sealed class ClaudeNativeEchoTests
{
    private const string Uuid = "11111111-2222-4333-8444-555555555555";

    /// <summary>The echo is read as its uuid and records nothing, since Daoris recorded the words before it wrote them.</summary>
    [Fact]
    public void A_replayed_user_line_is_read_as_its_echo_by_uuid_and_records_nothing()
    {
        var mapper = new ClaudeStreamJson();

        var echoed = mapper.Read($$"""{"type":"user","message":{"role":"user","content":[{"type":"text","text":"put MANGO in your reply"}]},"parent_tool_use_id":null,"session_id":"s","uuid":"{{Uuid}}","isReplay":true}""");
        var result = mapper.Read("""{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"t","content":"done"}]},"uuid":"22222222-2222-4333-8444-555555555555"}""");

        Assert.Equal(Uuid, echoed.Echoed);
        Assert.Empty(echoed.Events);
        Assert.Null(result.Echoed);
    }

    [Fact]
    public void Words_sent_now_are_one_user_line_with_no_priority_under_their_uuid()
    {
        var framed = new ClaudeNativeDialect().FrameSteer("put MANGO in your reply", Uuid);

        using var json = System.Text.Json.JsonDocument.Parse(framed);
        Assert.Equal("user", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(Uuid, json.RootElement.GetProperty("uuid").GetString());
        Assert.False(json.RootElement.TryGetProperty("priority", out _));
        Assert.DoesNotContain('\n', framed);
    }
}
