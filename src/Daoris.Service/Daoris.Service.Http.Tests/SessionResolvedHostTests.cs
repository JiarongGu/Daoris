using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// KEEP1b (D195 point 6; the cleanup design §3.5, Fable F2.5): a session's record keeps the downloaded versions each of its starts was
/// handed, which the downloads' rule keeps while it runs. The driver names them through the record's own door before each spawn, and the
/// record answers them to a caller on this machine only (D47 §4), as a reviewer's <c>opinion</c> is; one told none carries none.
/// </summary>
public sealed class SessionResolvedHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private async Task<JsonElement> ListedAsync(string id, System.Net.IPAddress? from = null) =>
        (await host.GetAsync("/api/sessions?includeClosed=true", from)).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == id);

    private static string? Resolved(JsonElement session) =>
        session.TryGetProperty("resolved", out var resolved) && resolved.ValueKind != JsonValueKind.Null ? resolved.GetRawText() : null;

    private async Task<string> OpenAsync(string title, string tree)
    {
        var published = await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title, body = "Go on." });
        var quest = published.Json.GetProperty("quest").GetProperty("id").GetString()!;
        var opened = await host.PostAsync("/api/sessions", new { quest, adapter = "stub", tree = host.RootOf(tree) });
        return opened.Json.GetProperty("session").GetProperty("id").GetString()!;
    }

    /// <summary>🔴 Kept through its door, each start's added to the last's, and answered on this machine, absent off it.</summary>
    [Fact]
    public async Task A_session_s_handed_versions_are_kept_and_answered_to_this_machine_only()
    {
        var id = await OpenAsync("A quest on a pinned agent", "warden-resolved");
        Assert.Null(Resolved(await ListedAsync(id)));

        var first = await host.PostAsync($"/api/sessions/{id}/resolved", new { resolved = new[] { "agent/claude-code/2.0.1" } });
        var then = await host.PostAsync($"/api/sessions/{id}/resolved", new { resolved = new[] { "tool/gh/2.61.0" } });

        Assert.Equal((200, 200), (first.Status, then.Status));
        const string Kept = """["agent/claude-code/2.0.1","tool/gh/2.61.0"]""";
        Assert.Equal(Kept, Resolved(then.Json.GetProperty("session")));
        Assert.Equal(Kept, Resolved(await ListedAsync(id)));
        Assert.Null(Resolved(await ListedAsync(id, DaorisHost.OffMachine)));
    }

    /// <summary>A version that is no <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c>, or none, is a 400; an unknown record a 404.</summary>
    [Fact]
    public async Task A_malformed_list_is_refused_and_an_unknown_record_is_not_found()
    {
        var id = await OpenAsync("A quest told something odd", "warden-resolved-refused");

        Assert.Equal(400, (await host.PostAsync($"/api/sessions/{id}/resolved", new { resolved = new[] { "C:/elsewhere/claude.exe" } })).Status);
        Assert.Equal(400, (await host.PostAsync($"/api/sessions/{id}/resolved", new { })).Status);
        Assert.Equal(404, (await host.PostAsync("/api/sessions/nothere/resolved", new { resolved = new[] { "agent/claude-code/2.0.1" } })).Status);
        Assert.Null(Resolved(await ListedAsync(id)));
    }
}
