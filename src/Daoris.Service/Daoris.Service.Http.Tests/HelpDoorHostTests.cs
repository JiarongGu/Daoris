namespace Daoris.Service.Http.Tests;

/// <summary>
/// Ask Daoris's open takes the workspace the person's window is on, and its record
/// keeps it; none is <c>default</c>, as a driver from before sends it; a workspace this machine does not hold is refused, saying
/// which it holds. The ledger's rules are <c>HelpSessionTests</c>'; this is the door's reading of its body.
/// </summary>
public sealed class HelpDoorHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public async Task The_open_keeps_the_workspace_named_and_refuses_one_this_machine_does_not_hold()
    {
        var room = Path.Combine(host.Repositories, "rooms", "askscope1a");

        var unknown = await host.PostAsync("/api/sessions/help", new { door = "stub", room, workspace = "nowhere" });

        Assert.Equal(404, unknown.Status);
        Assert.Contains("`nowhere` is not a workspace this machine holds", unknown.Json.GetProperty("error").GetString());

        var opened = await host.PostAsync("/api/sessions/help", new { door = "stub", room, workspace = "Default" });

        Assert.Equal(200, opened.Status);
        Assert.Equal("default", opened.Json.GetProperty("session").GetProperty("workspace").GetString());

        // A driver from before names none: the default workspace's, opened beside the one running, as every conversation opens.
        var unnamed = await host.PostAsync("/api/sessions/help", new { door = "stub", room });

        Assert.Equal(200, unnamed.Status);
        Assert.Equal("default", unnamed.Json.GetProperty("session").GetProperty("workspace").GetString());
        Assert.NotEqual(opened.Json.GetProperty("session").GetProperty("id").GetString(), unnamed.Json.GetProperty("session").GetProperty("id").GetString());
    }
}

/// <summary>
/// The open keeps the mode its start got, answered to this machine as <c>helpMode</c>; a mode the ledger
/// does not know is a bad request. A conversation going on records the mode it goes on under through its own door, which knows Ask
/// Daoris's records alone. A host of its own, so the default workspace's conversation is this class's alone.
/// </summary>
public sealed class HelpModeDoorHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public async Task The_open_keeps_the_mode_its_start_got_and_a_going_on_records_another()
    {
        var room = Path.Combine(host.Repositories, "rooms", "helpmode1a2");

        var unknown = await host.PostAsync("/api/sessions/help", new { door = "stub", room, mode = "sometimes" });

        Assert.Equal(400, unknown.Status);
        Assert.Contains("`sometimes`", unknown.Json.GetProperty("error").GetString());

        var opened = await host.PostAsync("/api/sessions/help", new { door = "stub", room, mode = "full" });

        Assert.Equal(200, opened.Status);
        var session = opened.Json.GetProperty("session");
        Assert.Equal("full", session.GetProperty("helpMode").GetString());
        var id = session.GetProperty("id").GetString()!;

        var kept = await host.PostAsync($"/api/sessions/{id}/help-mode", new { mode = "restricted" });

        Assert.Equal(200, kept.Status);
        Assert.Equal("restricted", kept.Json.GetProperty("session").GetProperty("helpMode").GetString());
        Assert.Equal(400, (await host.PostAsync($"/api/sessions/{id}/help-mode", new { mode = "sometimes" })).Status);
        Assert.Equal(400, (await host.PostAsync($"/api/sessions/{id}/help-mode", new { })).Status);
        Assert.Equal(404, (await host.PostAsync("/api/sessions/nothere/help-mode", new { mode = "full" })).Status);

        // 🔴 This machine's own: a caller off it is answered no mode.
        var off = (await host.GetAsync("/api/sessions?includeClosed=true", DaorisHost.OffMachine)).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == id);
        Assert.False(off.TryGetProperty("helpMode", out _));
    }
}
