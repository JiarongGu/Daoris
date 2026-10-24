using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// SESSONE1a (D167 points 2 and 3): the session's DTO carries <c>entry</c>, the key the ledger wrote at the open — to every caller,
/// since it names a line of work and a repository and no path and no account — and answers a derived one for a record from before.
/// </summary>
public sealed class EntryHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private async Task<JsonElement?> ListedAsync(string id, System.Net.IPAddress? from = null) =>
        (await host.GetAsync("/api/sessions?includeClosed=true", from)).Json.EnumerateArray()
            .Cast<JsonElement?>().SingleOrDefault(row => row!.Value.GetProperty("id").GetString() == id);

    private static string? Entry(JsonElement session) => session.TryGetProperty("entry", out var value) ? value.GetString() : null;

    /// <summary>🔴 A session for a quest an ask asked is opened under the ask, and the open's answer and the list say so, off this machine too.</summary>
    [Fact]
    public async Task A_session_for_a_quest_an_ask_asked_answers_its_entry_to_every_caller()
    {
        var published = await host.PostAsync("/api/quests", new { from = "ask #c98a7e", to = "Keeper", title = "An ask's quest", body = "Do it." });
        var quest = published.Json.GetProperty("quest").GetProperty("id").GetString()!;

        var opened = await host.PostAsync("/api/sessions", new { quest, adapter = "stub", tree = host.RootOf("keeper-entry-quest") });

        Assert.Equal(200, opened.Status);
        var session = opened.Json.GetProperty("session");
        Assert.Equal("ask:c98a7e/Keeper", Entry(session));
        var id = session.GetProperty("id").GetString()!;
        Assert.Equal("ask:c98a7e/Keeper", Entry((await ListedAsync(id))!.Value));
        Assert.Equal("ask:c98a7e/Keeper", Entry((await ListedAsync(id, DaorisHost.OffMachine))!.Value));
    }

    /// <summary>A conversation is its own entry.</summary>
    [Fact]
    public async Task A_conversation_answers_its_own_entry()
    {
        var opened = await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", adapter = "stub", tree = host.RootOf("keeper-entry-chat") });

        var session = opened.Json.GetProperty("session");
        Assert.Equal($"session:{session.GetProperty("id").GetString()}/Keeper", Entry(session));
    }

    /// <summary>
    /// 🔴 CHATFLOW1a (the chat-flow design §4.2): the quest a chat took (CHATTAKE1) is answered as <c>tookQuest</c> to this machine, as
    /// <c>opinion</c> is, and to nobody off it; a chat that took none answers none.
    /// </summary>
    [Fact]
    public async Task The_quest_a_chat_took_is_answered_to_this_machine_only()
    {
        var took = (await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", adapter = "stub", tree = host.RootOf("keeper-took-chat") }))
            .Json.GetProperty("session").GetProperty("id").GetString()!;
        var none = (await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", adapter = "stub", tree = host.RootOf("keeper-took-none") }))
            .Json.GetProperty("session").GetProperty("id").GetString()!;
        Assert.True(await host.Composed.Sessions.MarkTookAsync(took, "a1b2c3d4e5f6"));

        Assert.Equal("a1b2c3d4e5f6", (await ListedAsync(took))!.Value.GetProperty("tookQuest").GetString());
        Assert.False((await ListedAsync(took, DaorisHost.OffMachine))!.Value.TryGetProperty("tookQuest", out _));
        Assert.False((await ListedAsync(none))!.Value.TryGetProperty("tookQuest", out _));
    }
}
