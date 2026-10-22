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
