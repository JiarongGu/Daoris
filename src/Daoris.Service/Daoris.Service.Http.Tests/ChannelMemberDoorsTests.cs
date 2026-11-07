namespace Daoris.Service.Http.Tests;

/// <summary>
/// On a host holding the person's key, an outside agent is admitted by the person's door alone, and
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

    /// <summary>
    /// The listing of a channel's members is the driver's read, with the key alone, naming only those still on it; the
    /// look's end of one is the key's leave with <c>expired</c>, and a member's own leave cannot say it.
    /// </summary>
    [Fact]
    public async Task CHANNELJOIN1c_the_listing_is_the_keyed_read_and_the_look_ends_a_member_as_expired()
    {
        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence = "Check the export again.", to = "Keeper" }, person: Key);
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var member = (await host.PostAsync("/api/channel/members", new { ask, agent = "codex" }, person: Key)).Json.GetProperty("id").GetString()!;

        Assert.Equal(403, (await host.GetAsync($"/api/channel/members?ask={ask}")).Status);
        var listed = await host.GetAsync($"/api/channel/members?ask={ask}", person: Key);
        Assert.Equal(200, listed.Status);
        var row = Assert.Single(listed.Json.GetProperty("members").EnumerateArray());
        Assert.Equal((member, true), (row.GetProperty("id").GetString(), row.TryGetProperty("spent", out _)));

        var ended = await host.PostAsync($"/api/channel/members/{member}/leave", new { why = "expired" }, person: Key);
        Assert.Equal("expired", ended.Json.GetProperty("why").GetString());
        Assert.Empty((await host.GetAsync($"/api/channel/members?ask={ask}", person: Key)).Json.GetProperty("members").EnumerateArray());

        var other = (await host.PostAsync("/api/channel/members", new { ask, agent = "codex" }, person: Key)).Json.GetProperty("id").GetString()!;
        var itself = await host.PostAsync($"/api/channel/members/{other}/leave", new { why = "expired" });
        Assert.Equal("left", itself.Json.GetProperty("why").GetString());
    }
}
