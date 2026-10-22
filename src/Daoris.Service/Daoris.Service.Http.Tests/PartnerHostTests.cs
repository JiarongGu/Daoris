using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// COAGENT1b (D199 points 1 and 2; the co-agent design §2.3 step 5, §7.3): the driver opens a partner's record through
/// <c>POST /api/partners</c>, idempotent by the piece's id. The record serves its lead, carries its piece and an entry of its own, and
/// both `serves` and `piece` are this machine's own (D47 §4).
/// </summary>
public sealed class PartnerHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private void Ask(string id, string lead)
    {
        Directory.CreateDirectory(DaorisHome.Entry(host.Home, HomeEntry.Partners));
        File.WriteAllText(
            PartnerBox.PathOf(host.Home, id, "ask"),
            $"{{ \"id\": \"{id}\", \"asker\": \"{lead}\", \"piece\": \"Make it seven.\", \"paths\": [], \"agent\": \"codex-acp\", \"base\": \"abc123\", "
            + "\"uncommitted\": [], \"askedBy\": \"person\", \"at\": \"2026-10-22T12:00:00.0000000+00:00\" }");
    }

    private async Task<JsonElement?> ListedAsync(string id, System.Net.IPAddress? from = null) =>
        (await host.GetAsync("/api/sessions?includeClosed=true", from)).Json.EnumerateArray()
            .Cast<JsonElement?>().SingleOrDefault(row => row!.Value.GetProperty("id").GetString() == id);

    /// <summary>
    /// 🔴 The open makes a chat that serves the lead and carries the piece, in its own clone with an entry of its own; asked again for the
    /// same piece it answers the same record and makes none; `piece` is answered on this machine and to nobody off it.
    /// </summary>
    [Fact]
    public async Task The_door_opens_one_record_per_piece_that_serves_its_lead_and_names_the_piece_to_this_machine_only()
    {
        var lead = await host.Composed.Sessions.CreateAsync(
            null, "Partnered", "claude-code", DateTimeOffset.UtcNow, workspace: "default", tree: "trees/default/Partnered/s-aaaaaaaa");
        Ask("p1a2b3c4", lead.Id);

        var opened = await host.PostAsync("/api/partners", new
        {
            lead = lead.Id, piece = "p1a2b3c4", adapter = "codex-acp", tree = "trees/default/Partnered/p-p1a2b3c4", account = "work", baseCommit = "ABC123",
        });

        Assert.Equal(200, opened.Status);
        var session = opened.Json.GetProperty("session");
        var id = session.GetProperty("id").GetString()!;
        Assert.Equal((lead.Id, "p1a2b3c4", "chat", "abc123"), (
            session.GetProperty("serves").GetString(), session.GetProperty("piece").GetString(), session.GetProperty("kind").GetString(),
            session.GetProperty("baseCommit").GetString()));
        Assert.StartsWith($"session:{id}", (await host.Composed.Sessions.FindAsync(id))!.Entry);

        var again = await host.PostAsync("/api/partners", new { lead = lead.Id, piece = "p1a2b3c4", adapter = "codex-acp", tree = "trees/default/Partnered/p-p1a2b3c4" });
        Assert.Equal((200, id), (again.Status, again.Json.GetProperty("session").GetProperty("id").GetString()));

        Assert.Equal("p1a2b3c4", (await ListedAsync(id))!.Value.GetProperty("piece").GetString());
        Assert.False((await ListedAsync(id, DaorisHost.OffMachine))!.Value.TryGetProperty("piece", out _));
        Assert.False((await ListedAsync(lead.Id))!.Value.TryGetProperty("piece", out _));
    }

    /// <summary>
    /// 🔴 The hand (the design §2.3 step 9, §7.6): a running lead takes the answer at its turn's end (409); an ended one keeps it as a word
    /// marked with the partner's record and the piece's key, reopening it, never the person's; a second hand answers the word waiting
    /// under the key and keeps none; a lead handed over passes it to its newest successor by `from`; a record that is no partner is not found.
    /// </summary>
    [Fact]
    public async Task The_hand_door_keeps_one_word_under_the_pieces_key_on_the_newest_record_of_the_leads_line()
    {
        var sessions = host.Composed.Sessions;
        var lead = await sessions.CreateAsync(null, "Handing", "stub", DateTimeOffset.UtcNow, workspace: "default", tree: "trees/default/Handing/s-cccccccc");
        Ask("h1a2b3c4", lead.Id);
        var partner = (await host.Composed.Partners.OpenAsync(
            new PartnerOpen(lead.Id, "h1a2b3c4", "codex-acp", "trees/default/Handing/p-h1a2b3c4"), DateTimeOffset.UtcNow)).Session!;

        var running = await host.PostAsync($"/api/partners/{partner.Id}/hand", new { text = "Partner made it seven." });
        Assert.Equal(409, running.Status);
        Assert.Contains("never into a step in flight", running.Error);

        await sessions.SetStateAsync(lead.Id, SessionState.Completed, "done.", null, null, DateTimeOffset.UtcNow);
        var handed = await host.PostAsync($"/api/partners/{partner.Id}/hand", new { text = "Partner made it seven." });
        Assert.Equal(200, handed.Status);
        var word = handed.Json.GetProperty("said");
        Assert.Equal((partner.Id, "partner:h1a2b3c4", true), (
            word.GetProperty("helper").GetString(), word.GetProperty("key").GetString(), word.GetProperty("reopens").GetBoolean()));
        var kept = Assert.Single((await sessions.FindAsync(lead.Id))!.Said);
        Assert.False(kept.Persons);

        var twice = await host.PostAsync($"/api/partners/{partner.Id}/hand", new { text = "Partner made it seven." });
        Assert.Equal((200, kept.Id), (twice.Status, twice.Json.GetProperty("said").GetProperty("id").GetString()));
        Assert.Single((await sessions.FindAsync(lead.Id))!.Said);

        // A lead handed over: its line's newest record takes the next piece's answer.
        var moved = await sessions.CreateAsync(null, "Handing", "stub", DateTimeOffset.UtcNow, workspace: "default", tree: "trees/default/Handing/s-dddddddd");
        Ask("h2a2b3c4", moved.Id);
        var second = (await host.Composed.Partners.OpenAsync(
            new PartnerOpen(moved.Id, "h2a2b3c4", "codex-acp", "trees/default/Handing/p-h2a2b3c4"), DateTimeOffset.UtcNow)).Session!;
        var successor = await sessions.CreateAsync(
            null, "Handing", "stub", DateTimeOffset.UtcNow, workspace: "default", tree: "trees/default/Handing/s-dddddddd", from: moved.Id);
        await sessions.SetStateAsync(moved.Id, SessionState.Completed, "handed over.", null, null, DateTimeOffset.UtcNow);
        await sessions.SetStateAsync(successor.Id, SessionState.Completed, "done.", null, null, DateTimeOffset.UtcNow);
        Assert.Equal(200, (await host.PostAsync($"/api/partners/{second.Id}/hand", new { text = "Partner made it eight." })).Status);
        Assert.Empty((await sessions.FindAsync(moved.Id))!.Said);
        Assert.Equal("partner:h2a2b3c4", Assert.Single((await sessions.FindAsync(successor.Id))!.Said).Key);

        Assert.Equal(404, (await host.PostAsync($"/api/partners/{lead.Id}/hand", new { text = "x" })).Status);
        Assert.Equal(404, (await host.PostAsync($"/api/partners/{partner.Id}/hand", new { text = "x" }, DaorisHost.OffMachine)).Status);
    }

    /// <summary>
    /// A piece not on file, a lead other than the ask's, a lead that serves another itself, the lead's own tree and a shape with a part
    /// missing are each refused in a sentence, and nothing is kept.
    /// </summary>
    [Fact]
    public async Task The_door_refuses_what_cannot_be_a_partners_record()
    {
        var sessions = host.Composed.Sessions;
        var lead = await sessions.CreateAsync(null, "Refusing", "claude-code", DateTimeOffset.UtcNow, workspace: "default", tree: "trees/default/Refusing/s-bbbbbbbb");
        var helper = await sessions.CreateAsync(null, "Refusing", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: lead.Id);
        Ask("r1a2b3c4", lead.Id);
        Ask("r2a2b3c4", helper.Id);

        Assert.Equal(404, (await host.PostAsync("/api/partners", new { lead = lead.Id, piece = "nope0000", adapter = "codex-acp", tree = "trees/default/Refusing/p-1" })).Status);
        Assert.Equal(400, (await host.PostAsync("/api/partners", new { lead = helper.Id, piece = "r1a2b3c4", adapter = "codex-acp", tree = "trees/default/Refusing/p-2" })).Status);
        Assert.Equal(400, (await host.PostAsync("/api/partners", new { lead = helper.Id, piece = "r2a2b3c4", adapter = "codex-acp", tree = "trees/default/Refusing/p-3" })).Status);
        Assert.Equal(400, (await host.PostAsync("/api/partners", new { lead = lead.Id, piece = "r1a2b3c4", adapter = "codex-acp", tree = "trees/default/Refusing/s-bbbbbbbb" })).Status);
        Assert.Equal(400, (await host.PostAsync("/api/partners", new { lead = lead.Id, piece = "r1a2b3c4", tree = "trees/default/Refusing/p-4" })).Status);
        Assert.Null(await sessions.ForPieceAsync("r1a2b3c4"));
        Assert.Equal(404, (await host.PostAsync("/api/partners", new { lead = lead.Id, piece = "r1a2b3c4", adapter = "codex-acp", tree = "trees/default/Refusing/p-5" }, DaorisHost.OffMachine)).Status);
    }
}
