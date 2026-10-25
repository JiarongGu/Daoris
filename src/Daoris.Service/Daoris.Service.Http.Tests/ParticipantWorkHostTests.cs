using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// FOUNDATION1c (D212; the foundation design §2.2): the host answers a record's <c>work</c> to a caller on this machine, in the list and in
/// the answer of an open, and to no caller off it, as a reviewer's <c>opinion</c> is (D47 §4); the reading is the marks' and is stored
/// nowhere. The rule's table is <c>ParticipantWorkTests</c>'.
/// </summary>
public sealed class ParticipantWorkHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public async Task A_record_answers_its_work_on_this_machine_and_to_nobody_off_it()
    {
        var published = await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title = "A quest with work", body = "Go on." });
        var quest = published.Json.GetProperty("quest").GetProperty("id").GetString()!;
        var driven = await host.PostAsync("/api/sessions", new { quest, adapter = "stub", tree = host.RootOf("keeper-work-driven") });
        var chat = await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", adapter = "stub", tree = host.RootOf("keeper-work-chat") });
        var help = await host.PostAsync("/api/sessions/help", new { adapter = "stub", room = Path.Combine(host.Repositories, "rooms", "foundation1c") });

        Assert.Equal((200, 200, 200), (driven.Status, chat.Status, help.Status));
        var ids = new[] { driven, chat, help }.Select(opened => opened.Json.GetProperty("session").GetProperty("id").GetString()!).ToArray();
        Assert.Equal("build", Work(opened: driven.Json.GetProperty("session")));
        Assert.Equal("chat", Work(opened: chat.Json.GetProperty("session")));
        Assert.Equal("ask-daoris", Work(opened: help.Json.GetProperty("session")));

        Assert.Equal(["build", "chat", "ask-daoris"], (await ListedAsync(null)).Where(row => ids.Contains(Id(row))).OrderBy(row => Array.IndexOf(ids, Id(row))).Select(row => Work(row)));
        Assert.All((await ListedAsync(DaorisHost.OffMachine)).Where(row => ids.Contains(Id(row))), row => Assert.Null(Work(row)));
    }

    private async Task<IReadOnlyList<JsonElement>> ListedAsync(System.Net.IPAddress? from) =>
        [.. (await host.GetAsync("/api/sessions?includeClosed=true", from)).Json.EnumerateArray()];

    private static string Id(JsonElement session) => session.GetProperty("id").GetString()!;

    private static string? Work(JsonElement opened) =>
        opened.TryGetProperty("work", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
