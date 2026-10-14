using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// SESSONE2a (D167 points 6 and 8; the co-working design §2.5): <c>GET</c> and <c>POST /api/asks/{id}/channel</c>. The post is the
/// person's, kept on the channel and on the ask as the <c>channel</c> kind; the read answers messages and Daoris's coded lines in time
/// order and moves a mark only when asked; a refusal is a code.
/// </summary>
public sealed class ChannelHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private async Task<string> AskedAsync(string sentence)
    {
        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence, to = "Keeper" });
        Assert.Equal(200, asked.Status);
        return asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
    }

    private static string[] Codes(JsonElement items) =>
        [.. items.EnumerateArray().Select(item => item.GetProperty("kind").GetString() + ":" + (item.TryGetProperty("code", out var code) ? code.GetString() : ""))];

    /// <summary>🔴 The person's post is answered whole, read back with Daoris's published line, and kept on the ask as a channel word.</summary>
    [Fact]
    public async Task The_persons_post_is_read_back_beside_daoriss_lines_and_kept_on_the_ask()
    {
        var ask = await AskedAsync("Channel the report through the bridge.");

        var posted = await host.PostAsync($"/api/asks/{ask}/channel", new { text = "  use the older bridge ", to = "Keeper" });

        Assert.Equal(200, posted.Status);
        var message = posted.Json.GetProperty("message");
        Assert.Equal("message", message.GetProperty("kind").GetString());
        Assert.Equal("use the older bridge", message.GetProperty("text").GetString());
        Assert.Equal("Keeper", message.GetProperty("to").GetString());
        Assert.True(message.GetProperty("person").GetBoolean());

        var read = await host.GetAsync($"/api/asks/{ask}/channel");
        Assert.Equal(200, read.Status);
        Assert.Equal(["line:published", "message:"], Codes(read.Json.GetProperty("items")));
        Assert.Equal(2, read.Json.GetProperty("unread").GetInt32());
        var line = read.Json.GetProperty("items")[0];
        Assert.Equal($"ask #{ask}", line.GetProperty("from").GetString());
        Assert.Equal("Keeper", line.GetProperty("to").GetString());

        var words = (await host.GetAsync($"/api/asks/{ask}")).Json.GetProperty("words").EnumerateArray().Select(w => w.GetProperty("kind").GetString());
        Assert.Equal(["asked", "channel"], words);
    }

    /// <summary>🔴 A read moves no mark; the POST to <c>/channel/read</c> does, for the entry it names, and the unread count is what is after it.</summary>
    [Fact]
    public async Task A_read_moves_no_mark_and_the_posts_move_the_named_entrys()
    {
        var ask = await AskedAsync("Channel the second report through the bridge.");
        await host.PostAsync($"/api/asks/{ask}/channel", new { text = "one" });

        Assert.Equal(2, (await host.GetAsync($"/api/asks/{ask}/channel")).Json.GetProperty("unread").GetInt32());
        Assert.Equal(2, (await host.GetAsync($"/api/asks/{ask}/channel")).Json.GetProperty("unread").GetInt32());

        var marked = await host.PostAsync($"/api/asks/{ask}/channel/read", new { });
        Assert.Equal(200, marked.Status);
        Assert.Equal(0, marked.Json.GetProperty("unread").GetInt32());
        Assert.Equal(2, marked.Json.GetProperty("items").GetArrayLength());
        Assert.Equal("person", marked.Json.GetProperty("entry").GetString());
        Assert.Equal(0, (await host.GetAsync($"/api/asks/{ask}/channel")).Json.GetProperty("unread").GetInt32());

        // A reader that is not the person keeps its own mark, under the key it names; `through` moves it to a moment.
        var keeper = $"ask:{ask}/Keeper";
        Assert.Equal(2, (await host.GetAsync($"/api/asks/{ask}/channel?entry={Uri.EscapeDataString(keeper)}")).Json.GetProperty("unread").GetInt32());
        var through = DateTimeOffset.UtcNow.AddDays(1);
        var moved = await host.PostAsync($"/api/asks/{ask}/channel/read", new { entry = keeper, through });
        Assert.Equal(0, moved.Json.GetProperty("unread").GetInt32());
        Assert.Equal(404, (await host.PostAsync("/api/asks/ffffff/channel/read", new { })).Status);
    }

    /// <summary>🔴 A refusal is a code with its status, never a sentence; an unknown ask is a 404 on both doors.</summary>
    [Fact]
    public async Task A_refusal_is_a_code_and_an_unknown_ask_is_not_found()
    {
        var ask = await AskedAsync("Channel the third report through the bridge.");

        var empty = await host.PostAsync($"/api/asks/{ask}/channel", new { text = " " });
        var long_ = await host.PostAsync($"/api/asks/{ask}/channel", new { text = new string('x', 4001) });
        var to = await host.PostAsync($"/api/asks/{ask}/channel", new { text = "hi", to = "Nowhere" });
        var re = await host.PostAsync($"/api/asks/{ask}/channel", new { text = "hi", re = "nosuchid" });

        Assert.Equal((400, "empty"), (empty.Status, empty.Json.GetProperty("code").GetString()));
        Assert.Equal((400, "too-long"), (long_.Status, long_.Json.GetProperty("code").GetString()));
        Assert.Equal((400, "bad-to"), (to.Status, to.Json.GetProperty("code").GetString()));
        Assert.Equal((400, "bad-re"), (re.Status, re.Json.GetProperty("code").GetString()));
        Assert.Equal(404, (await host.GetAsync("/api/asks/ffffff/channel")).Status);
        Assert.Equal(404, (await host.PostAsync("/api/asks/ffffff/channel", new { text = "hi" })).Status);
    }

    private static ChannelMessage Teammates(string id, string ask, string text, DateTimeOffset at) => new(
        id, ask, "default", at, Session: "ab12cd34", Repository: "Keeper", Agent: "claude", Entry: "ask-" + ask + "/keeper", To: null, Re: null,
        Question: false, text, Origin: "teammate-key");

    /// <summary>🔴 SESSONE2g: an ask held elsewhere reads from the mirrored messages by its id, with their origin, in time order and no lines.</summary>
    [Fact]
    public async Task A_teammates_ask_reads_from_the_mirrored_messages()
    {
        var now = DateTimeOffset.UtcNow;
        await host.Composed.Messages.MirrorAsync(Teammates("m2", "9a8b7c", "second", now.AddMinutes(1)));
        await host.Composed.Messages.MirrorAsync(Teammates("m1", "9a8b7c", "first", now));

        var read = await host.GetAsync("/api/asks/9a8b7c/channel");

        Assert.Equal(200, read.Status);
        var items = read.Json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(["first", "second"], items.Select(i => i.GetProperty("text").GetString()));
        Assert.Equal("teammate-key", items[0].GetProperty("origin").GetString());
        Assert.Equal(2, read.Json.GetProperty("unread").GetInt32());
        Assert.Equal(404, (await host.GetAsync("/api/asks/9a8b7d/channel")).Status);
    }

    /// <summary>🔴 Posting to a teammate's ask is refused with its own code, not as an unknown ask: the read is all this machine does there.</summary>
    [Fact]
    public async Task A_post_to_a_teammates_ask_is_refused_as_held_elsewhere()
    {
        await host.Composed.Messages.MirrorAsync(Teammates("m3", "9a8b7e", "theirs", DateTimeOffset.UtcNow));

        var posted = await host.PostAsync("/api/asks/9a8b7e/channel", new { text = "mine" });

        Assert.Equal((409, "held-elsewhere"), (posted.Status, posted.Json.GetProperty("code").GetString()));
        Assert.Single((await host.GetAsync("/api/asks/9a8b7e/channel")).Json.GetProperty("items").EnumerateArray());
    }

    /// <summary>Deleting an ask forgets its channel, so the same words asked again find none of it.</summary>
    [Fact]
    public async Task Deleting_the_ask_forgets_its_channel()
    {
        const string Sentence = "Channel the fourth report through the bridge.";
        var ask = await AskedAsync(Sentence);
        await host.PostAsync($"/api/asks/{ask}/channel", new { text = "about to go" });
        Assert.Equal(200, (await host.DeleteAsync($"/api/asks/{ask}")).Status);

        var again = await AskedAsync(Sentence);

        Assert.Equal(ask, again);
        Assert.Equal(["line:published"], Codes((await host.GetAsync($"/api/asks/{again}/channel")).Json.GetProperty("items")));
    }
}
