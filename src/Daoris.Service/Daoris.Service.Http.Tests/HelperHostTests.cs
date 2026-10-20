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

    /// <summary>
    /// MIXAGENT2a-ii: the creation door the driver's helper session opens its record by. A chat that serves the asker and no
    /// quest, in the asker's repository and workspace, in the clone the driver names; an asker that is itself a helper, a reviewer,
    /// an intake, unknown, or whose tree is the helper's own is refused in a sentence.
    /// </summary>
    [Fact]
    public async Task The_creation_door_opens_a_record_that_serves_its_asker_and_refuses_what_cannot_ask()
    {
        var sessions = host.Composed.Sessions;
        // Its own repository: a session of another test that holds no tree yet holds every tree of its repository (D51).
        var asker = await sessions.CreateAsync(null, "Opener", "stub", DateTimeOffset.UtcNow, workspace: "default", kind: Daoris.Knowledge.SessionKind.Chat, tree: "trees/default/Opener/s-aaaaaaaa");

        var opened = await host.PostAsync("/api/helpers", new { asker = asker.Id, adapter = "codex-acp", tree = "trees/default/Opener/o-bbbbbbbb", account = "work", baseCommit = "ABCDEF1" });

        Assert.Equal(200, opened.Status);
        var session = opened.Json.GetProperty("session");
        Assert.Equal(asker.Id, session.GetProperty("serves").GetString());
        Assert.Equal("codex-acp", session.GetProperty("adapter").GetString());
        Assert.Equal("Opener", session.GetProperty("repository").GetString());
        Assert.Equal("chat", session.GetProperty("kind").GetString());
        Assert.False(session.TryGetProperty("quest", out var quest) && quest.ValueKind == JsonValueKind.String);

        var helperId = session.GetProperty("id").GetString()!;
        var again = await host.PostAsync("/api/helpers", new { asker = helperId, adapter = "codex-acp", tree = "trees/default/Opener/o-cccccccc" });
        Assert.Equal(400, again.Status);
        Assert.Contains("a helper cannot ask for a helper", again.Error);

        var unknown = await host.PostAsync("/api/helpers", new { asker = "nope0000", adapter = "codex-acp", tree = "trees/default/Keeper/o-dddddddd" });
        Assert.Equal(404, unknown.Status);

        var own = await host.PostAsync("/api/helpers", new { asker = asker.Id, adapter = "codex-acp", tree = "trees/default/Opener/s-aaaaaaaa" });
        Assert.Equal(400, own.Status);

        var reviewer = await sessions.CreateAsync(null, "Opener", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, opinion: "op000001");
        Assert.Equal(400, (await host.PostAsync("/api/helpers", new { asker = reviewer.Id, adapter = "codex-acp", tree = "trees/default/Opener/o-eeeeeeee" })).Status);
    }

    /// <summary>
    /// MIXAGENT2a-ii: the answer's return. A word marked `helper` waits on an ended asker's record and reopens it; a running
    /// asker takes it at its turn's end (409), and it goes once.
    /// </summary>
    [Fact]
    public async Task The_hand_door_keeps_the_answer_on_an_ended_asker_once_and_never_into_a_running_one()
    {
        var sessions = host.Composed.Sessions;
        var asker = await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Driven, tree: "trees/default/Keeper/s-ffffffff");
        var helper = await sessions.CreateAsync(null, "Keeper", "codex-acp", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, serves: asker.Id);

        var running = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." });
        Assert.Equal(409, running.Status);
        Assert.Contains("never into a step in flight", running.Error);

        await sessions.SetStateAsync(asker.Id, Daoris.Knowledge.SessionState.Completed, "done.", null, null, DateTimeOffset.UtcNow);
        var handed = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." });

        Assert.Equal(200, handed.Status);
        var word = handed.Json.GetProperty("said");
        Assert.Equal(helper.Id, word.GetProperty("helper").GetString());
        Assert.True(word.GetProperty("reopens").GetBoolean());
        var kept = (await sessions.FindAsync(asker.Id))!;
        Assert.Equal((helper.Id, false), (kept.Said[0].Helper, kept.Said[0].Persons));

        var twice = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." });
        Assert.Equal(409, twice.Status);

        Assert.Equal(404, (await host.PostAsync($"/api/helpers/{asker.Id}/hand", new { text = "x" })).Status);
    }

    /// <summary>
    /// MIXAGENT2a-iii: a conversation that asked is handed the answer while it is open (working, between turns), since the chat
    /// runner says it as the conversation's next turn; a conversation still being started, an intake, a helper and a stood-down
    /// record keep their refusals.
    /// </summary>
    [Fact]
    public async Task The_hand_door_keeps_the_answer_on_an_open_conversation_and_still_refuses_a_conversation_not_yet_open()
    {
        var sessions = host.Composed.Sessions;
        var chat = await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat);
        var helper = await sessions.CreateAsync(null, "Keeper", "codex-acp", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, serves: chat.Id);

        var starting = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." });
        Assert.Equal(409, starting.Status);

        await sessions.SetStateAsync(chat.Id, Daoris.Knowledge.SessionState.Working, null, null, null, DateTimeOffset.UtcNow);
        var handed = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." });

        Assert.Equal(200, handed.Status);
        var kept = (await sessions.FindAsync(chat.Id))!;
        Assert.Equal((helper.Id, false, true), (kept.Said[0].Helper, kept.Said[0].Persons, kept.Said[0].Reopens));

        var twice = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." });
        Assert.Equal(409, twice.Status);

        var driven = await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Driven, tree: "trees/default/Keeper/s-abababab");
        var drivenHelper = await sessions.CreateAsync(null, "Keeper", "codex-acp", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, serves: driven.Id);
        await sessions.SetStateAsync(driven.Id, Daoris.Knowledge.SessionState.Working, null, null, null, DateTimeOffset.UtcNow);
        Assert.Equal(409, (await host.PostAsync($"/api/helpers/{drivenHelper.Id}/hand", new { text = "x" })).Status);
    }

    /// <summary>
    /// 🔴 COWORK1d (the message-urgency design §5): an urgent ask's answer reaches its running asker at its next step, so the driver that
    /// runs it hands it with `running`: kept on the running record's words with its level, for that run's inbox, and still once. Without
    /// `running` a running asker refuses it, as before.
    /// </summary>
    [Fact]
    public async Task The_hand_door_keeps_an_urgent_answer_on_a_running_asker_where_its_driver_asks()
    {
        var sessions = host.Composed.Sessions;
        var asker = await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Driven, tree: "trees/default/Keeper/s-cdcdcdcd");
        var helper = await sessions.CreateAsync(null, "Keeper", "codex-acp", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, serves: asker.Id);
        await sessions.SetStateAsync(asker.Id, Daoris.Knowledge.SessionState.Working, null, null, null, DateTimeOffset.UtcNow);

        Assert.Equal(409, (await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." })).Status);
        var handed = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five.", running = true, level = "urgent" });

        Assert.Equal(200, handed.Status);
        var kept = (await sessions.FindAsync(asker.Id))!;
        Assert.Equal((helper.Id, "urgent", false), (kept.Said[0].Helper, kept.Said[0].Level, kept.Said[0].Persons));
        Assert.Equal(409, (await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five.", running = true })).Status);
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
