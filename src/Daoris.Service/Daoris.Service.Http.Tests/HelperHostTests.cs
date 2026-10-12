using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// MIXAGENT2a-i (D163 points 2 to 4): a helper's record names the session it serves, and a helper's answer waits on the asker's
/// record marked as the helper's. Both are this machine's own (D47 §4), as a reviewer's `opinion` is, and the helper's answer
/// is never the person's words.
/// </summary>
public sealed class HelperHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private async Task<JsonElement?> ListedAsync(string id, System.Net.IPAddress? from = null) =>
        (await host.GetAsync("/api/sessions?includeClosed=true", from)).Json.EnumerateArray()
            .Cast<JsonElement?>().SingleOrDefault(row => row!.Value.GetProperty("id").GetString() == id);

    /// <summary>
    /// 🔴 `serves` is answered on a helper's record to a caller on this machine and to none off it, a session that serves none
    /// carries none, and a helper's word is marked `helper` (never `by`) and is absent from `answer`, the person's words.
    /// </summary>
    [Fact]
    public async Task A_helpers_record_names_its_asker_to_this_machine_only_and_its_word_is_marked()
    {
        var sessions = host.Composed.Sessions;
        var asker = await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat);
        var helper = await sessions.CreateAsync(null, "Keeper", "codex-acp", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, serves: asker.Id);

        Assert.Equal(asker.Id, (await ListedAsync(helper.Id))!.Value.GetProperty("serves").GetString());
        Assert.False((await ListedAsync(helper.Id, DaorisHost.OffMachine))!.Value.TryGetProperty("serves", out _));
        Assert.False((await ListedAsync(asker.Id))!.Value.TryGetProperty("serves", out _));

        await sessions.KeepSaidAsync(asker.Id, new Daoris.Knowledge.SaidWord("h1", "It stops at five.", DateTimeOffset.UtcNow, [], Reopens: true, Helper: helper.Id));
        var kept = (await sessions.FindAsync(asker.Id))!;
        Assert.Equal((helper.Id, null, false), (kept.Said[0].Helper, kept.Said[0].By, kept.Said[0].Persons));
        Assert.Null(kept.Answer);

        var word = (await ListedAsync(asker.Id))!.Value.GetProperty("said")[0];
        Assert.Equal(helper.Id, word.GetProperty("helper").GetString());
        Assert.False(word.TryGetProperty("by", out var by) && by.ValueKind != JsonValueKind.Null);
    }

    /// <summary>🔴 A helper takes no words and is never resumed: the say door refuses it by its own word, `helper`.</summary>
    [Fact]
    public async Task The_say_door_refuses_a_helpers_record_as_a_helper()
    {
        var asker = await host.Composed.Sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat);
        var helper = await host.Composed.Sessions.CreateAsync(null, "Keeper", "codex-acp", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, serves: asker.Id);
        await host.Composed.Sessions.SetStateAsync(helper.Id, Daoris.Knowledge.SessionState.Completed, "answered.", null, null, DateTimeOffset.UtcNow);

        var said = await host.PostAsync($"/api/sessions/{helper.Id}/say", new { text = "and one more thing" });

        Assert.Equal(409, said.Status);
        Assert.Equal("helper", said.Json.GetProperty("refusal").GetString());
        Assert.Contains($"`{asker.Id}`", said.Error);
    }
}
