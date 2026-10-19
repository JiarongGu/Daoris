using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// AUTONOMY1a (D184 point 1): a driven session's record keeps the access setup its start resolved. The driver names it at the open, and
/// the record answers it to a caller on this machine only (D47 §4), as a reviewer's <c>opinion</c> is; a record told none carries none,
/// and a setup that is not a short map of words is refused before any record opens.
/// </summary>
public sealed class SessionAccessHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private async Task<JsonElement> ListedAsync(string id, System.Net.IPAddress? from = null) =>
        (await host.GetAsync("/api/sessions?includeClosed=true", from)).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == id);

    private static string? Access(JsonElement session) =>
        session.TryGetProperty("access", out var access) && access.ValueKind != JsonValueKind.Null ? access.GetRawText() : null;

    private async Task<string> QuestAsync(string title)
    {
        var published = await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title, body = "Go on." });
        return published.Json.GetProperty("quest").GetProperty("id").GetString()!;
    }

    /// <summary>🔴 Answered at the open and in the list on this machine, absent off it.</summary>
    [Fact]
    public async Task A_driven_session_s_access_setup_is_answered_to_this_machine_only()
    {
        var quest = await QuestAsync("A quest under a setup");

        var opened = await host.PostAsync("/api/sessions", new
        {
            quest, adapter = "stub", tree = host.RootOf("warden-access"), access = new { permissions = "unrestricted", landing = "rule" },
        });

        Assert.Equal(200, opened.Status);
        const string Kept = """{"permissions":"unrestricted","landing":"rule"}""";
        Assert.Equal(Kept, Access(opened.Json.GetProperty("session")));
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        Assert.Equal(Kept, Access(await ListedAsync(id)));
        Assert.Null(Access(await ListedAsync(id, DaorisHost.OffMachine)));
    }

    /// <summary>A setup that is no short map of words is the caller's mistake: a 400, and no record.</summary>
    [Fact]
    public async Task An_access_setup_that_is_not_a_short_map_of_words_is_refused()
    {
        var quest = await QuestAsync("A quest under a setup it cannot keep");

        var refused = await host.PostAsync("/api/sessions", new
        {
            quest, adapter = "stub", tree = host.RootOf("warden-refused"), access = new Dictionary<string, string> { ["permissions"] = "a word\nand a line" },
        });

        Assert.Equal(400, refused.Status);
        Assert.Contains("access", refused.Json.GetProperty("error").GetString());
    }
}
