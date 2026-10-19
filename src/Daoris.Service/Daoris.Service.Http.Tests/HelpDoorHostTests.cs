namespace Daoris.Service.Http.Tests;

/// <summary>
/// ASKSCOPE1a (D158's ASKSCOPE1 note, point 1): Ask Daoris's open takes the workspace the person's window was on, and its record
/// keeps it; none is <c>default</c>, as a driver from before sends it; a workspace this machine does not hold is refused, saying
/// which it holds. The ledger's rules are <c>HelpSessionTests</c>'; this is the door's reading of its body.
/// </summary>
public sealed class HelpDoorHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public async Task The_open_keeps_the_workspace_named_and_refuses_one_this_machine_does_not_hold()
    {
        var room = Path.Combine(host.Repositories, "rooms", "askscope1a");

        var unknown = await host.PostAsync("/api/sessions/help", new { adapter = "stub", room, workspace = "nowhere" });

        Assert.Equal(404, unknown.Status);
        Assert.Contains("`nowhere` is not a workspace this machine holds", unknown.Json.GetProperty("error").GetString());

        var opened = await host.PostAsync("/api/sessions/help", new { adapter = "stub", room, workspace = "Default" });

        Assert.Equal(200, opened.Status);
        Assert.Equal("default", opened.Json.GetProperty("session").GetProperty("workspace").GetString());

        // A driver from before names none: the default workspace's, whose conversation is running.
        var unnamed = await host.PostAsync("/api/sessions/help", new { adapter = "stub", room });

        Assert.Equal(409, unnamed.Status);
        Assert.Contains(opened.Json.GetProperty("session").GetProperty("id").GetString()!, unnamed.Json.GetProperty("error").GetString());
    }
}
