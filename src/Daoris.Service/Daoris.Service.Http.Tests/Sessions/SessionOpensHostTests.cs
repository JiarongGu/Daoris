using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

public sealed class SessionOpensHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Theory]
    [InlineData(null, "stub")]
    [InlineData("", "stub")]
    [InlineData("published", "")]
    public async Task A_new_key_still_validates_required_fields_after_its_lookup(string? quest, string adapter)
    {
        var now = DateTimeOffset.UtcNow;
        var published = await host.Composed.Quests.PublishAsync("Asker", "Keeper", "Work", "Why", now);
        var left = await host.Composed.Sessions.CreateAsync(null, "Keeper", "stub", now, kind: SessionKind.Chat);
        await host.Composed.Sessions.SetStateAsync(left.Id, SessionState.Completed, null, null, null, now);
        var key = $"invalid/{Guid.NewGuid():N}";
        var answer = await host.PostAsync("/api/sessions", new { quest = quest == "published" ? published.Id : quest, adapter, from = left.Id, opens = key });
        Assert.Equal(400, answer.Status);
        Assert.Equal(key, answer.Json.GetProperty("opens").GetString());
        Assert.Null(await host.Composed.Sessions.Opens.FindAsync(key));
    }

    [Fact]
    public async Task A_new_chat_key_still_requires_its_repository()
    {
        var now = DateTimeOffset.UtcNow;
        var left = await host.Composed.Sessions.CreateAsync(null, "Keeper", "stub", now, kind: SessionKind.Chat);
        await host.Composed.Sessions.SetStateAsync(left.Id, SessionState.Completed, null, null, null, now);
        var answer = await host.PostAsync("/api/sessions/chat", new { from = left.Id, opens = "invalid/chat" });
        Assert.Equal(400, answer.Status);
        Assert.Equal("invalid/chat", answer.Json.GetProperty("opens").GetString());
        Assert.Null(await host.Composed.Sessions.Opens.FindAsync("invalid/chat"));
    }

    [Fact]
    public async Task A_key_is_echoed_with_empty_transfer_and_replayed_before_request_checks()
    {
        var now = DateTimeOffset.UtcNow;
        var left = await host.Composed.Sessions.CreateAsync(null, "Keeper", "stub", now, kind: SessionKind.Chat);
        await host.Composed.Sessions.SetStateAsync(left.Id, SessionState.Completed, null, null, null, now);
        await host.Composed.Sessions.KeepSaidAsync(left.Id, new SaidWord("waiting", "not moved", now, []));
        var first = await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", adapter = "stub", from = left.Id, opens = "host/key" });
        Assert.Equal(200, first.Status);
        Assert.Equal("host/key", first.Json.GetProperty("opens").GetString());
        Assert.Empty(first.Json.GetProperty("transfer").EnumerateArray());
        Assert.False(first.Json.GetProperty("session").TryGetProperty("opens", out _));
        var id = first.Json.GetProperty("session").GetProperty("id").GetString();
        var retry = await host.PostAsync("/api/sessions/chat", new { repository = "", from = "missing", opens = "host/key" });
        Assert.Equal(200, retry.Status);
        Assert.Equal(id, retry.Json.GetProperty("session").GetProperty("id").GetString());
        Assert.Single((await host.Composed.Sessions.FindAsync(left.Id))!.Said);
        Assert.Empty((await host.Composed.Sessions.FindAsync(id!))!.Said);
    }

    [Fact]
    public async Task The_protocol_and_every_marked_step_answer_the_same_host_run()
    {
        var protocol = await host.Server.SendAsync(context =>
        {
            context.Request.Path = "/api/protocol";
            context.Connection.RemoteIpAddress = DaorisHost.Loopback;
        });
        var run = protocol.Response.Headers["x-daoris-run"].ToString();
        Assert.Matches("^[0-9a-f]{32}$", run);
        foreach (var path in new[] { "/api/sessions/missing/say", "/api/sessions/missing/taken", "/api/sessions/missing/attempt", "/api/sessions/chat" })
        {
            var answer = await host.Server.SendAsync(context =>
            {
                context.Request.Method = "POST";
                context.Request.Path = path;
                context.Connection.RemoteIpAddress = DaorisHost.Loopback;
                context.Request.ContentType = "application/json";
                context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}"));
                context.Request.ContentLength = 2;
            });
            Assert.Equal(run, answer.Response.Headers["x-daoris-run"].ToString());
        }
    }
}
