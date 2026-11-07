using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// A helper's record names the session it serves, and a helper's answer waits on the asker's
/// record marked as the helper's. Both are this machine's own, as a reviewer's `opinion` is, and the helper's answer
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
        Assert.False(kept.PersonSaid);

        var word = (await ListedAsync(asker.Id))!.Value.GetProperty("said")[0];
        Assert.Equal(helper.Id, word.GetProperty("helper").GetString());
        Assert.False(word.TryGetProperty("by", out var by) && by.ValueKind != JsonValueKind.Null);
    }

    /// <summary>
    /// The creation door the driver's helper session opens its record by. A chat that serves the asker and no
    /// quest, in the asker's repository and workspace, in the clone the driver names; an asker that is itself a helper, a reviewer,
    /// an intake, unknown, or whose tree is the helper's own is refused in a sentence.
    /// </summary>
    [Fact]
    public async Task The_creation_door_opens_a_record_that_serves_its_asker_and_refuses_what_cannot_ask()
    {
        var sessions = host.Composed.Sessions;
        // Its own repository: a session of another test that holds no tree yet holds every tree of its repository.
        var asker = await sessions.CreateAsync(null, "Opener", "stub", DateTimeOffset.UtcNow, workspace: "default", kind: Daoris.Knowledge.SessionKind.Chat, tree: "trees/default/Opener/s-aaaaaaaa");

        var opened = await host.PostAsync("/api/helpers", new { asker = asker.Id, door = "codex-acp", tree = "trees/default/Opener/o-bbbbbbbb", account = "work", baseCommit = "ABCDEF1" });

        Assert.Equal(200, opened.Status);
        var session = opened.Json.GetProperty("session");
        Assert.Equal(asker.Id, session.GetProperty("serves").GetString());
        Assert.Equal("codex-acp", session.GetProperty("door").GetString());
        Assert.Equal("Opener", session.GetProperty("repository").GetString());
        Assert.Equal("chat", session.GetProperty("kind").GetString());
        Assert.False(session.TryGetProperty("quest", out var quest) && quest.ValueKind == JsonValueKind.String);

        var helperId = session.GetProperty("id").GetString()!;
        var again = await host.PostAsync("/api/helpers", new { asker = helperId, door = "codex-acp", tree = "trees/default/Opener/o-cccccccc" });
        Assert.Equal(400, again.Status);
        Assert.Contains("a helper cannot ask for a helper", again.Error);

        var unknown = await host.PostAsync("/api/helpers", new { asker = "nope0000", door = "codex-acp", tree = "trees/default/Keeper/o-dddddddd" });
        Assert.Equal(404, unknown.Status);

        var own = await host.PostAsync("/api/helpers", new { asker = asker.Id, door = "codex-acp", tree = "trees/default/Opener/s-aaaaaaaa" });
        Assert.Equal(400, own.Status);

        var reviewer = await sessions.CreateAsync(null, "Opener", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, opinion: "op000001");
        Assert.Equal(400, (await host.PostAsync("/api/helpers", new { asker = reviewer.Id, door = "codex-acp", tree = "trees/default/Opener/o-eeeeeeee" })).Status);
    }

    /// <summary>
    /// The answer's return. A word marked `helper` waits on an ended asker's record and reopens it; a running
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

        // A word already waiting is answered with its holder and word, never refused; nothing is kept twice.
        var twice = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." });
        Assert.Equal(200, twice.Status);
        Assert.True(twice.Json.GetProperty("waiting").GetBoolean());
        Assert.Equal(kept.Said[0].Id, twice.Json.GetProperty("said").GetProperty("id").GetString());
        Assert.Single((await sessions.FindAsync(asker.Id))!.Said);

        Assert.Equal(404, (await host.PostAsync($"/api/helpers/{asker.Id}/hand", new { text = "x" })).Status);
    }

    /// <summary>
    /// An asker its line has left (a hand-over, a move, a carry-on) is never reopened by its helper's answer: the
    /// answer goes to the newest record continuing it, as a partner's piece does, and is keyed by its helper; the record left keeps nothing.
    /// </summary>
    [Fact]
    public async Task The_hand_door_follows_the_askers_line_to_its_newest_record_and_keys_the_word()
    {
        var sessions = host.Composed.Sessions;
        var asker = await sessions.CreateAsync(null, "Relay", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Driven, tree: "trees/default/Relay/s-11111111");
        var helper = await sessions.CreateAsync(null, "Relay", "codex-acp", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, serves: asker.Id);
        await sessions.SetStateAsync(asker.Id, Daoris.Knowledge.SessionState.Stopped, "handed over.", null, null, DateTimeOffset.UtcNow);
        var successor = await sessions.CreateAsync(
            null, "Relay", "codex-acp", DateTimeOffset.UtcNow.AddSeconds(1), kind: Daoris.Knowledge.SessionKind.Driven, tree: "trees/default/Relay/s-11111111",
            from: asker.Id);
        await sessions.SetStateAsync(successor.Id, Daoris.Knowledge.SessionState.Completed, "done.", null, null, DateTimeOffset.UtcNow);

        var handed = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." });

        Assert.Equal(200, handed.Status);
        var word = Assert.Single((await sessions.FindAsync(successor.Id))!.Said);
        Assert.Equal((helper.Id, $"helper:{helper.Id}"), (word.Helper, word.Key));
        Assert.Empty((await sessions.FindAsync(asker.Id))!.Said);
    }

    /// <summary>
    /// The door walks the line as the say door does, to the store's one bound (<see cref="Daoris.Knowledge.Lines.Bound"/>), so a line
    /// past the helper's sixteen links reaches its newest part too, and the parts it left keep nothing.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    public async Task The_hand_door_follows_a_long_line_to_its_newest_part_and_keeps_nothing_on_a_part_it_left(int links)
    {
        var sessions = host.Composed.Sessions;
        var now = DateTimeOffset.UtcNow;
        var asker = await sessions.CreateAsync(null, "Relay", "stub", now, kind: Daoris.Knowledge.SessionKind.Driven, tree: "trees/default/Relay/s-11111111");
        var helper = await sessions.CreateAsync(null, "Relay", "codex-acp", now, kind: Daoris.Knowledge.SessionKind.Chat, serves: asker.Id);
        var parts = new List<string> { asker.Id };
        for (var link = 1; link <= links; link++)
        {
            await sessions.SetStateAsync(parts[^1], Daoris.Knowledge.SessionState.Stopped, "handed over.", null, null, now);
            var next = await sessions.CreateAsync(
                null, "Relay", "stub", now.AddSeconds(link), kind: Daoris.Knowledge.SessionKind.Driven, tree: "trees/default/Relay/s-11111111",
                from: parts[^1]);
            parts.Add(next.Id);
        }

        await sessions.SetStateAsync(parts[^1], Daoris.Knowledge.SessionState.Completed, "done.", null, null, now);

        var handed = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." });

        Assert.Equal(200, handed.Status);
        Assert.Single((await sessions.FindAsync(parts[^1]))!.Said);
        foreach (var part in parts.Take(links)) Assert.Empty((await sessions.FindAsync(part))!.Said);
    }

    /// <summary>
    /// A park waits on the person, so its helper's answer is refused while they have not
    /// answered, and kept beside their answer once they have: its go-on then hands both in one prompt, the person's answer still theirs.
    /// </summary>
    [Fact]
    public async Task A_park_takes_its_helpers_answer_only_once_the_person_answered_it()
    {
        var sessions = host.Composed.Sessions;
        var asker = await sessions.CreateAsync(null, "Parked", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Driven, tree: "trees/default/Parked/s-22222222");
        await sessions.SetStateAsync(asker.Id, Daoris.Knowledge.SessionState.AwaitingPerson, "It stopped to ask you.", null, null, DateTimeOffset.UtcNow);
        var helper = await sessions.CreateAsync(null, "Parked", "codex-acp", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, serves: asker.Id);

        Assert.Equal(409, (await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." })).Status);
        Assert.Empty((await sessions.FindAsync(asker.Id))!.Said);

        await sessions.KeepSaidAsync(asker.Id, new Daoris.Knowledge.SaidWord("person01", "Port 8080.", DateTimeOffset.UtcNow, []));
        Assert.Equal(200, (await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five." })).Status);

        var record = (await sessions.FindAsync(asker.Id))!;
        Assert.Equal(Daoris.Knowledge.SessionState.AwaitingPerson, record.State);
        Assert.Equal(["person01", null], record.Said.Select(word => word.Helper is null ? word.Id : null));
        Assert.Equal("Port 8080.", record.Said.Single(word => word.Persons).Text);
    }

    /// <summary>
    /// A conversation that asked is handed the answer while it is open (working, between turns), since the chat
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
        Assert.Equal(200, twice.Status);
        Assert.True(twice.Json.GetProperty("waiting").GetBoolean());

        var driven = await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Driven, tree: "trees/default/Keeper/s-abababab");
        var drivenHelper = await sessions.CreateAsync(null, "Keeper", "codex-acp", DateTimeOffset.UtcNow, kind: Daoris.Knowledge.SessionKind.Chat, serves: driven.Id);
        await sessions.SetStateAsync(driven.Id, Daoris.Knowledge.SessionState.Working, null, null, null, DateTimeOffset.UtcNow);
        Assert.Equal(409, (await host.PostAsync($"/api/helpers/{drivenHelper.Id}/hand", new { text = "x" })).Status);
    }

    /// <summary>
    /// 🔴 An urgent ask's answer reaches its running asker at its next step, so the driver that
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
        var again = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five.", running = true });
        Assert.Equal(200, again.Status);
        Assert.True(again.Json.GetProperty("waiting").GetBoolean());
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
