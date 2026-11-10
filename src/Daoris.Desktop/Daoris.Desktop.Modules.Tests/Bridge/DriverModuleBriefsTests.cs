using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>The brief a continuing part's start is given rides the openings' answer beside them (<c>briefs</c>).</summary>
public sealed class DriverModuleBriefsTests : DriverModuleBridge
{
    [Fact]
    public async Task The_brief_a_start_was_handed_is_answered_beside_the_openings()
    {
        var loop = Loop();
        loop.Events.Append("part1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Continues = "a1b2c3d4", Text = "Go on with the hydrator fix" });
        loop.Events.Append("chat1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Cap the hydration" });
        var module = new DriverModule(Bus, loop);

        var answer = await AnswerAsync(module, "SESSION_OPENINGS", new { ids = new[] { "part1", "chat1" } });

        var briefs = answer.GetProperty("briefs");
        Assert.Equal("Go on with the hydrator fix", briefs.GetProperty("part1").GetString());
        Assert.False(briefs.TryGetProperty("chat1", out _));
        Assert.False(answer.GetProperty("openings").TryGetProperty("part1", out _));
    }
}
