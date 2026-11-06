namespace Daoris.Service.Http.Tests;

/// <summary>
/// CHANNELJOIN1 (D244 points 9 to 11): on a host holding the person's key, an outside agent is admitted by the person's door alone, and
/// then reads, posts and leaves at its three agent doors without the key, each message its own and never the person's; the person's key at
/// the leave is their <i>Remove</i>.
/// </summary>
public sealed class ChannelMemberDoorsTests(KeyedHost host) : IClassFixture<KeyedHost>
{
    private const string Key = KeyedHost.Key;

    [Fact]
    public async Task CHANNELJOIN1a_the_person_admits_and_the_member_speaks_as_itself_at_its_own_doors()
    {
        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence = "Check the export with an outside agent.", to = "Keeper" }, person: Key);
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;

        Assert.Equal(403, (await host.PostAsync("/api/channel/members", new { ask, agent = "codex" })).Status);
        var joined = await host.PostAsync("/api/channel/members", new { ask, agent = "codex", repository = "Keeper", confirmation = "c1" }, person: Key);
        Assert.Equal(200, joined.Status);
        var member = joined.Json.GetProperty("id").GetString()!;

        var posted = await host.PostAsync($"/api/channel/members/{member}/messages", new { text = "the export reads fine here" });
        Assert.Equal(200, posted.Status);
        var message = posted.Json.GetProperty("message");
        Assert.Equal((member, false, true), (message.GetProperty("session").GetString(), message.GetProperty("person").GetBoolean(), message.GetProperty("outside").GetBoolean()));

        var read = await host.GetAsync($"/api/channel/members/{member}/messages");
        Assert.Equal(200, read.Status);
        Assert.Contains(read.Json.GetProperty("items").EnumerateArray(), item => item.TryGetProperty("text", out var text) && text.GetString() == "the export reads fine here");
        var person = await host.GetAsync($"/api/asks/{ask}/channel");
        var seen = person.Json.GetProperty("items").EnumerateArray().Single(item => item.TryGetProperty("session", out var by) && by.GetString() == member);
        Assert.False(seen.GetProperty("person").GetBoolean());

        var left = await host.PostAsync($"/api/channel/members/{member}/leave", new { });
        Assert.Equal((200, "left"), (left.Status, left.Json.GetProperty("why").GetString()));
        var after = await host.PostAsync($"/api/channel/members/{member}/messages", new { text = "still here?" });
        Assert.Equal((404, "not-member"), (after.Status, after.Json.GetProperty("code").GetString()));

        var second = (await host.PostAsync("/api/channel/members", new { ask, agent = "codex" }, person: Key)).Json.GetProperty("id").GetString()!;
        var removed = await host.PostAsync($"/api/channel/members/{second}/leave", new { }, person: Key);
        Assert.Equal("removed", removed.Json.GetProperty("why").GetString());
    }
}
