using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// MIXAGENT1a-iii (D163 point 4; the mixed-agents design §4): a session that continues another says so on its own record. The
/// driver names the session continued, why and who asked through either open door, and the record answers the three to a caller
/// on this machine only (D47 §4), as a reviewer's `opinion` is; a record that continues none carries none.
/// </summary>
public sealed class ContinuesHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public async Task Actor_provenance_is_kept_through_its_later_door_and_answered_only_on_loopback()
    {
        var opened = await host.PostAsync("/api/sessions/chat", new
        {
            repository = "Keeper", adapter = "stub", tree = host.RootOf("actor-chat"), actor = "chat",
        });
        Assert.Equal(200, opened.Status);
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        var kept = await host.PostAsync($"/api/sessions/{id}/actor", new
        {
            choice = "before", asked = new { agent = "stub", model = new { source = "account-default", carried = false }, effort = new { source = "account-default", carried = false } },
            @override = new { model = "alias" }, ran = new { models = new[] { "reported" }, more = 0, entries = Array.Empty<object>(), entriesMore = 0 },
        });
        Assert.Equal(200, kept.Status);
        var local = (await ListedAsync(id))!.Value;
        Assert.Equal("chat", Text(local, "actor"));
        Assert.Equal("before", Text(local, "choice"));
        Assert.Equal("stub", local.GetProperty("asked").GetProperty("agent").GetString());
        Assert.Equal("alias", local.GetProperty("override").GetProperty("model").GetString());
        Assert.Equal("reported", local.GetProperty("ran").GetProperty("models")[0].GetString());
        var remote = (await ListedAsync(id, DaorisHost.OffMachine))!.Value;
        foreach (var key in new[] { "actor", "choice", "asked", "override", "ran" })
            Assert.True(!remote.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null, key);
    }

    private async Task<JsonElement?> ListedAsync(string id, System.Net.IPAddress? from = null) =>
        (await host.GetAsync("/api/sessions?includeClosed=true", from)).Json.EnumerateArray()
            .Cast<JsonElement?>().SingleOrDefault(row => row!.Value.GetProperty("id").GetString() == id);

    private static (string?, string?, string?) Link(JsonElement session) =>
        (Text(session, "from"), Text(session, "fromWhy"), Text(session, "fromBy"));

    private static string? Text(JsonElement session, string name) =>
        session.TryGetProperty(name, out var value) ? value.GetString() : null;

    /// <summary>
    /// 🔴 A conversation handed over is opened naming the one it continues, why and who asked: answered so at the open and in the
    /// list on this machine, absent off it, and absent on a conversation that continues none.
    /// </summary>
    [Fact]
    public async Task A_conversation_opened_from_another_names_it_to_this_machine_only()
    {
        var opened = await host.PostAsync("/api/sessions/chat", new
        {
            repository = "Keeper", adapter = "codex-acp", tree = host.RootOf("keeper-handed-chat"),
            from = "c0nv0001", fromWhy = "near", fromBy = "daoris",
        });
        var plain = await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", adapter = "stub", tree = host.RootOf("keeper-plain-chat") });

        Assert.Equal((200, 200), (opened.Status, plain.Status));
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        Assert.Equal(("c0nv0001", "near", "daoris"), Link(opened.Json.GetProperty("session")));
        Assert.Equal(("c0nv0001", "near", "daoris"), Link((await ListedAsync(id))!.Value));
        Assert.Equal(((string?)null, (string?)null, (string?)null), Link((await ListedAsync(id, DaorisHost.OffMachine))!.Value));
        var other = plain.Json.GetProperty("session").GetProperty("id").GetString()!;
        Assert.Equal(((string?)null, (string?)null, (string?)null), Link((await ListedAsync(other))!.Value));
    }

    /// <summary>A quest's session carried on after a hand-over or a limit is opened naming the session it carries on.</summary>
    [Fact]
    public async Task A_quest_s_session_carried_on_names_the_session_it_continues()
    {
        var published = await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title = "A quest handed over", body = "Go on." });
        var quest = published.Json.GetProperty("quest").GetProperty("id").GetString()!;

        var opened = await host.PostAsync("/api/sessions", new
        {
            quest, adapter = "stub", tree = host.RootOf("keeper-handed-quest"), from = "s0", fromWhy = "limit", fromBy = "daoris",
        });

        Assert.Equal(200, opened.Status);
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        Assert.Equal(("s0", "limit", "daoris"), Link((await ListedAsync(id))!.Value));
        Assert.Equal(((string?)null, (string?)null, (string?)null), Link((await ListedAsync(id, DaorisHost.OffMachine))!.Value));
    }
}
