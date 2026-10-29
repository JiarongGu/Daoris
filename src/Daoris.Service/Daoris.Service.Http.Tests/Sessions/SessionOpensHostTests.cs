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

    /// <summary>
    /// 🔴 Row d-ii on d-i's keyed open: once a keyed open has left a record, a say to it is refused <c>continued</c> naming the record the open
    /// made, and the say at the opened record is kept there.
    /// </summary>
    [Fact]
    public async Task A_say_to_the_record_a_keyed_open_left_is_refused_continued_naming_the_opened_record()
    {
        var now = DateTimeOffset.UtcNow;
        var left = await host.Composed.Sessions.CreateAsync(null, "Keeper", "stub", now, kind: SessionKind.Chat);
        await host.Composed.Sessions.SetStateAsync(left.Id, SessionState.Completed, null, null, null, now);
        // A tree of its own, so another case's open conversation in Keeper's main tree holds nothing this open needs.
        var opened = await host.PostAsync("/api/sessions/chat", new
        {
            repository = "Keeper", adapter = "stub", from = left.Id, opens = $"head/{Guid.NewGuid():N}", tree = $"trees/default/Keeper/s-{Guid.NewGuid():N}"[..36],
        });
        Assert.Equal(200, opened.Status);
        var head = opened.Json.GetProperty("session").GetProperty("id").GetString()!;

        var refused = await host.PostAsync($"/api/sessions/{left.Id}/say", new { text = "A late word." });

        Assert.Equal(409, refused.Status);
        Assert.Equal("continued", refused.Json.GetProperty("refusal").GetString());
        Assert.Equal(head, refused.Json.GetProperty("head").GetString());
        Assert.Empty((await host.Composed.Sessions.FindAsync(left.Id))!.Said);
    }

    /// <summary>
    /// 🔴 Row d-ii part 3 over the wire: a keyed open's <c>moves</c> (the record words move from, the ids the course named, the ids it read)
    /// moves the words, and its answer's <c>transfer</c> names each moved word's id on the record left (<c>word</c>) and on the record opened
    /// (<c>id</c>), its producer key and its transfer id, in order; the record answered holds them, and the key's replay answers the same transfer.
    /// </summary>
    [Fact]
    public async Task A_keyed_open_naming_its_moves_answers_each_moved_word_and_its_replay_the_same_transfer()
    {
        var now = DateTimeOffset.UtcNow;
        var left = await host.Composed.Sessions.CreateAsync(null, "Keeper", "stub", now, kind: SessionKind.Chat);
        await host.Composed.Sessions.SetStateAsync(left.Id, SessionState.Completed, null, null, null, now);
        await host.Composed.Sessions.KeepSaidAsync(left.Id, new SaidWord("a1b2c3d4", "the person's words", now, ["notes.md"], Key: "press/wire"));
        await host.Composed.Sessions.KeepSaidAsync(left.Id, new SaidWord("e5f60718", "a late word", now, []));
        var key = $"moves/{Guid.NewGuid():N}";
        var body = new
        {
            repository = "Keeper", adapter = "stub", from = left.Id, opens = key, tree = $"trees/default/Keeper/s-{Guid.NewGuid():N}"[..36],
            moves = new { from = left.Id, words = new[] { "a1b2c3d4" }, read = new[] { "a1b2c3d4" } },
        };

        var first = await host.PostAsync("/api/sessions/chat", body);

        Assert.Equal(200, first.Status);
        var transfer = first.Json.GetProperty("transfer").EnumerateArray().ToList();
        Assert.Equal(
            [("a1b2c3d4", "a1b2c3d4", "press/wire", $"{key}/a1b2c3d4", 0), ("e5f60718", "e5f60718", null, $"{key}/e5f60718", 1)],
            // A keyless word's `key` is written as no member at all, as every null of this host's answers is.
            transfer.Select(word => (
                word.GetProperty("word").GetString(), word.GetProperty("id").GetString(),
                word.TryGetProperty("key", out var producer) ? producer.GetString() : null,
                word.GetProperty("transfer").GetString(), word.GetProperty("order").GetInt32())));
        var opened = first.Json.GetProperty("session");
        Assert.Equal(["a1b2c3d4", "e5f60718"], opened.GetProperty("said").EnumerateArray().Select(word => word.GetProperty("id").GetString()));
        Assert.Empty((await host.Composed.Sessions.FindAsync(left.Id))!.Said);

        var replay = await host.PostAsync("/api/sessions/chat", body);

        Assert.Equal(200, replay.Status);
        Assert.Equal(opened.GetProperty("id").GetString(), replay.Json.GetProperty("session").GetProperty("id").GetString());
        Assert.Equal(first.Json.GetProperty("transfer").GetRawText(), replay.Json.GetProperty("transfer").GetRawText());
    }

    /// <summary>
    /// 🔴 Moves without a key are refused, 400, nothing opened; moves that do not read beside a key, 400 with its echo and <c>malformed</c>, no
    /// receipt and nothing opened; the driven door alike.
    /// </summary>
    [Fact]
    public async Task Moves_without_a_key_or_that_do_not_read_are_refused_with_nothing_opened()
    {
        var now = DateTimeOffset.UtcNow;
        var left = await host.Composed.Sessions.CreateAsync(null, "Keeper", "stub", now, kind: SessionKind.Chat);
        await host.Composed.Sessions.SetStateAsync(left.Id, SessionState.Completed, null, null, null, now);
        await host.Composed.Sessions.KeepSaidAsync(left.Id, new SaidWord("a1b2c3d4", "waits", now, []));
        var before = (await host.Composed.Sessions.ListAsync(includeClosed: true)).Count;
        var published = await host.Composed.Quests.PublishAsync("Asker", "Keeper", "Work", "Why", now);

        // Each in a tree of its own, so an open this case's guard failed to refuse holds nothing another case's open needs.
        var unkeyed = await host.PostAsync("/api/sessions/chat", new
        {
            repository = "Keeper", adapter = "stub", from = left.Id, tree = $"trees/default/Keeper/s-{Guid.NewGuid():N}"[..36],
            moves = new { from = left.Id, words = new[] { "a1b2c3d4" }, read = Array.Empty<string>() },
        });
        var elsewhere = await host.PostAsync("/api/sessions/chat", new
        {
            repository = "Keeper", adapter = "stub", from = left.Id, opens = "moves/elsewhere", tree = $"trees/default/Keeper/s-{Guid.NewGuid():N}"[..36],
            moves = new { from = "another", words = new[] { "a1b2c3d4" }, read = Array.Empty<string>() },
        });
        var unread = await host.PostAsync("/api/sessions", new
        {
            quest = published.Id, adapter = "stub", from = left.Id, opens = "moves/unread", moves = new { from = left.Id, words = new[] { "a1b2c3d4" } },
        });

        Assert.Equal(400, unkeyed.Status);
        Assert.Contains("key", unkeyed.Json.GetProperty("error").GetString(), StringComparison.Ordinal);
        foreach (var (answer, key) in new[] { (elsewhere, "moves/elsewhere"), (unread, "moves/unread") })
        {
            Assert.Equal(400, answer.Status);
            Assert.Equal((key, "malformed"), (answer.Json.GetProperty("opens").GetString(), answer.Json.GetProperty("code").GetString()));
            Assert.Null(await host.Composed.Sessions.Opens.FindAsync(key));
        }

        Assert.Equal(before, (await host.Composed.Sessions.ListAsync(includeClosed: true)).Count);
        Assert.Single((await host.Composed.Sessions.FindAsync(left.Id))!.Said);
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
