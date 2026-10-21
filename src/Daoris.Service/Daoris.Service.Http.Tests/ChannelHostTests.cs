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
        // NOTIFY2g: the person's count passes over their own post and Daoris's line.
        Assert.Equal(0, read.Json.GetProperty("unread").GetInt32());
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
        // An agent's message, a teammate's: the person's count is it alone (NOTIFY2g), never their own post nor Daoris's line.
        await host.Composed.Messages.MirrorAsync(Teammates("r-" + ask, ask, "agent words", DateTimeOffset.UtcNow));

        Assert.Equal(1, (await host.GetAsync($"/api/asks/{ask}/channel")).Json.GetProperty("unread").GetInt32());
        Assert.Equal(1, (await host.GetAsync($"/api/asks/{ask}/channel")).Json.GetProperty("unread").GetInt32());

        var marked = await host.PostAsync($"/api/asks/{ask}/channel/read", new { });
        Assert.Equal(200, marked.Status);
        Assert.Equal(0, marked.Json.GetProperty("unread").GetInt32());
        Assert.Equal(3, marked.Json.GetProperty("items").GetArrayLength());
        Assert.Equal("person", marked.Json.GetProperty("entry").GetString());
        Assert.Equal(0, (await host.GetAsync($"/api/asks/{ask}/channel")).Json.GetProperty("unread").GetInt32());

        // A reader that is not the person keeps its own mark, under the key it names, and counts every item; `through` moves it to a moment.
        var keeper = $"ask:{ask}/Keeper";
        Assert.Equal(3, (await host.GetAsync($"/api/asks/{ask}/channel?entry={Uri.EscapeDataString(keeper)}")).Json.GetProperty("unread").GetInt32());
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

    /// <summary>
    /// 🔴 NOTIFY2g (D196 point 6; the notification design §2.4): the person's read mark is kept on a teammate's ask, the same door as
    /// a local ask's, so its count falls to none; a session's entry there is not found, and so is an ask nowhere.
    /// </summary>
    [Fact]
    public async Task The_persons_mark_is_kept_on_a_teammates_ask_and_an_ask_nowhere_is_not_found()
    {
        var now = DateTimeOffset.UtcNow;
        await host.Composed.Messages.MirrorAsync(Teammates("k1", "5d4c3b", "first", now));
        await host.Composed.Messages.MirrorAsync(Teammates("k2", "5d4c3b", "second", now.AddMinutes(1)));
        Assert.Equal(2, (await host.GetAsync("/api/asks/5d4c3b/channel")).Json.GetProperty("unread").GetInt32());

        var marked = await host.PostAsync("/api/asks/5d4c3b/channel/read", new { });

        Assert.Equal(200, marked.Status);
        Assert.Equal((0, 2), (marked.Json.GetProperty("unread").GetInt32(), marked.Json.GetProperty("items").GetArrayLength()));
        Assert.Equal(0, (await host.GetAsync("/api/asks/5d4c3b/channel")).Json.GetProperty("unread").GetInt32());
        var listed = (await host.GetAsync("/api/channel/elsewhere")).Json.GetProperty("asks").EnumerateArray()
            .Single(a => a.GetProperty("id").GetString() == "5d4c3b");
        Assert.Equal(0, listed.GetProperty("unread").GetInt32());
        Assert.Equal(404, (await host.PostAsync("/api/asks/5d4c3b/channel/read", new { entry = "ask:5d4c3b/Keeper" })).Status);
        Assert.Equal(404, (await host.PostAsync("/api/asks/5d4c3d/channel/read", new { })).Status);
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

    private static ChannelMessage From(string origin, string id, string ask, DateTimeOffset at) => new(
        id, ask, "default", at, Session: "ab12cd34", Repository: "Keeper", Agent: "claude", Entry: "ask-" + ask + "/keeper", To: null, Re: null,
        Question: false, "words " + id, Origin: origin);

    /// <summary>
    /// 🔴 SESSONE2h: <c>GET /api/channel/elsewhere</c> lists the asks the mirrored messages name that this machine does not hold: each with
    /// its origins, its latest message, its count and its unread against the person's local mark, newest first; an ask held here is not in it.
    /// </summary>
    [Fact]
    public async Task The_asks_held_elsewhere_are_listed_with_their_origins_latest_and_unread()
    {
        var now = DateTimeOffset.UtcNow;
        await host.Composed.Messages.MirrorAsync(From("key-a", "e1", "7e6f50", now.AddMinutes(-30)));
        await host.Composed.Messages.MirrorAsync(From("key-b", "e2", "7e6f50", now.AddMinutes(-20)));
        await host.Composed.Messages.MirrorAsync(From("key-a", "e3", "7e6f51", now.AddMinutes(-10)));
        await host.Composed.Messages.MirrorAsync(From("key-a", "e4", "7e6f51", now.AddMinutes(-5)));
        await host.Composed.Messages.MoveMarkAsync("7e6f51", ChannelDesk.PersonEntry, now.AddMinutes(-7));
        var held = await AskedAsync("Channel the fifth report through the bridge.");
        await host.PostAsync($"/api/asks/{held}/channel", new { text = "mine" });

        var listed = await host.GetAsync("/api/channel/elsewhere");

        Assert.Equal(200, listed.Status);
        var asks = listed.Json.GetProperty("asks").EnumerateArray().ToArray();
        Assert.DoesNotContain(asks, a => a.GetProperty("id").GetString() == held);
        var ours = asks.Where(a => a.GetProperty("id").GetString()!.StartsWith("7e6f5", StringComparison.Ordinal)).ToArray();
        Assert.Equal(["7e6f51", "7e6f50"], ours.Select(a => a.GetProperty("id").GetString()));
        var older = ours[1];
        Assert.Equal(["key-a", "key-b"], older.GetProperty("origins").EnumerateArray().Select(o => o.GetString()));
        Assert.Equal((2, 2), (older.GetProperty("messages").GetInt32(), older.GetProperty("unread").GetInt32()));
        Assert.Equal("default", older.GetProperty("workspace").GetString());
        Assert.Equal((2, 1), (ours[0].GetProperty("messages").GetInt32(), ours[0].GetProperty("unread").GetInt32()));
        Assert.True(ours[0].GetProperty("latest").GetDateTimeOffset() > older.GetProperty("latest").GetDateTimeOffset());
        Assert.False(ours[0].TryGetProperty("title", out _));
    }

    /// <summary>
    /// 🔴 COWORK1d (the message-urgency design §6, §8.4, §9): a message answers its level, the person's urgent unless they asked
    /// ordinary; <c>/channel/ack</c> keeps an entry's acknowledgment by message id, one part's claim once, which a session's read
    /// answers as <c>ack</c> and the person's as each addressed repository's receipt; an unknown state is refused.
    /// </summary>
    [Fact]
    public async Task A_message_answers_its_level_and_an_entrys_acknowledgment_shows_as_its_receipt()
    {
        var ask = await AskedAsync("Channel the fifth report through the bridge.");
        var urgent = (await host.PostAsync($"/api/asks/{ask}/channel", new { text = "now please", to = "Keeper" })).Json.GetProperty("message");
        var ordinary = (await host.PostAsync($"/api/asks/{ask}/channel", new { text = "later", urgent = false })).Json.GetProperty("message");
        Assert.True(urgent.GetProperty("urgent").GetBoolean());
        Assert.False(ordinary.GetProperty("urgent").GetBoolean());

        var id = urgent.GetProperty("id").GetString()!;
        var keeper = $"ask:{ask}/Keeper";
        var claimed = await host.PostAsync($"/api/asks/{ask}/channel/ack", new { entry = keeper, session = "s1", state = "taking", messages = new[] { id } });
        var again = await host.PostAsync($"/api/asks/{ask}/channel/ack", new { entry = keeper, session = "s2", state = "taking", messages = new[] { id } });
        Assert.Equal(200, claimed.Status);
        Assert.Equal([id], claimed.Json.GetProperty("acknowledged").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(0, again.Json.GetProperty("acknowledged").GetArrayLength());

        var forEntry = (await host.GetAsync($"/api/asks/{ask}/channel?entry={Uri.EscapeDataString(keeper)}")).Json.GetProperty("items")
            .EnumerateArray().Single(item => item.TryGetProperty("id", out var each) && each.GetString() == id);
        Assert.Equal(("taking", "s1"), (forEntry.GetProperty("ack").GetString(), forEntry.GetProperty("ackSession").GetString()));

        var forPerson = (await host.GetAsync($"/api/asks/{ask}/channel")).Json.GetProperty("items")
            .EnumerateArray().Single(item => item.TryGetProperty("id", out var each) && each.GetString() == id);
        var receipt = Assert.Single(forPerson.GetProperty("receipts").EnumerateArray());
        Assert.Equal(("Keeper", "held"), (receipt.GetProperty("repository").GetString(), receipt.GetProperty("state").GetString()));

        Assert.Equal(400, (await host.PostAsync($"/api/asks/{ask}/channel/ack", new { entry = keeper, state = "obeyed", messages = new[] { id } })).Status);
        Assert.Equal(404, (await host.PostAsync("/api/asks/ffffff/channel/ack", new { entry = keeper, state = "taken", messages = new[] { id } })).Status);
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
