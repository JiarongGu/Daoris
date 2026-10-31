using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// KNOWCENTRAL2d (the memory coordinator design §1, §3, §4) through the local host's doors: the driver hands a session's turn lines on its
/// state's move, a session's memory changes read for its page without the row's token, and the person decides an awaiting end and keeps
/// or drops an ended session's staged changes, each answered to nothing off this machine.
/// </summary>
public sealed class MemoryHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    /// <summary>A chat of Keeper's in its main tree, working, and a change it staged at <paramref name="at"/>.</summary>
    private async Task<(string Session, MemoryOp Op)> StagedAsync(MemoryChange change, DateTimeOffset at)
    {
        var opened = await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", adapter = "stub" });
        var session = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var state in new[] { "starting", "working" }) Assert.Equal(200, (await host.PostAsync($"/api/sessions/{session}/state", new { state })).Status);
        var op = await host.Composed.Sessions.Memory.StageAsync(
            "Keeper", change, new MemoryAuthor(session, "default", null, "stub", null, null, null, at));
        return (session, op);
    }

    /// <summary>🔴 The turn line handed on the move to completed names the model of the row its instant falls in.</summary>
    [Fact]
    public async Task A_completion_s_turn_lines_name_a_row_s_model()
    {
        var at = DateTimeOffset.UtcNow;
        var (session, op) = await StagedAsync(new MemoryChange(MemoryOpKind.Write, Text: "Keeper keeps its own area."), at);

        var moved = await host.PostAsync($"/api/sessions/{session}/state", new
        {
            state = "completed",
            turns = new Dictionary<string, object>
            {
                [session] = new { turns = new[] { new { seq = 1, started = at.AddSeconds(-5), ended = at.AddSeconds(5), model = "model-one" } }, asked = Array.Empty<DateTimeOffset>() },
            },
        });

        Assert.Equal(200, moved.Status);
        Assert.Equal("model-one", (await host.Composed.Sessions.Memory.FindAsync(op.Id))!.Models);
    }

    /// <summary>
    /// 🔴 An awaiting end is listed on its session's page with its reason as written, approved by the person through its row, and answered
    /// nothing off this machine; a decision with no answer is a 400, and a second one finds no end awaiting.
    /// </summary>
    [Fact]
    public async Task The_person_decides_an_awaiting_end_through_its_row()
    {
        var (session, op) = await StagedAsync(
            new MemoryChange(MemoryOpKind.End, Text: "Keeper moved its area.", Target: "1", Revision: 1), DateTimeOffset.UtcNow);
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{session}/state", new { state = "completed" })).Status);

        var page = await host.GetAsync($"/api/sessions/{session}/memory");
        var listed = Assert.Single(page.Json.GetProperty("ops").EnumerateArray());
        Assert.Equal((MemoryOpState.Awaiting, "Keeper moved its area."), (listed.GetProperty("state").GetString(), listed.GetProperty("change").GetProperty("text").GetString()));
        Assert.Equal(404, (await host.GetAsync($"/api/sessions/{session}/memory", DaorisHost.OffMachine)).Status);
        Assert.Equal(404, (await host.PostAsync($"/api/memory/{op.Id}/decide", new { approve = true }, DaorisHost.OffMachine)).Status);
        Assert.Equal(400, (await host.PostAsync($"/api/memory/{op.Id}/decide", new { })).Status);

        var decided = await host.PostAsync($"/api/memory/{op.Id}/decide", new { approve = true });
        var again = await host.PostAsync($"/api/memory/{op.Id}/decide", new { approve = true });

        Assert.Equal(200, decided.Status);
        Assert.Equal(MemoryDesk.Person, decided.Json.GetProperty("op").GetProperty("approver").GetString());
        Assert.Equal(404, again.Status);
    }

    /// <summary>
    /// 🔴 KNOWCENTRAL2f: what a line read, as the driver reads it for a hand-over's brief, is answered as it stands now: an entry ended since
    /// its session read it is answered ended, with its reason; nothing is answered off this machine.
    /// </summary>
    [Fact]
    public async Task A_line_s_reads_are_answered_as_they_stand_for_its_brief()
    {
        var engine = host.Composed.Memory!.Open("Keeper", MemoryReinforcement.None)!;
        var tiles = await engine.RememberAsync("Keeper draws its map in tiles.");
        var opened = await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", adapter = "stub" });
        var session = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var state in new[] { "starting", "working", "stopped" }) Assert.Equal(200, (await host.PostAsync($"/api/sessions/{session}/state", new { state })).Status);
        await host.Composed.Sessions.Memory.KeepReadsAsync(session, [new MemoryRead(new("Keeper", tiles.Id), tiles.Revision)]);
        Assert.NotNull(await engine.EndAsync(tiles.Id, tiles.Revision, "Keeper draws in vectors now."));

        Assert.Equal(404, (await host.PostAsync($"/api/sessions/{session}/memory/read", new { }, DaorisHost.OffMachine)).Status);
        var read = await host.PostAsync($"/api/sessions/{session}/memory/read", new { });

        Assert.Equal(200, read.Status);
        var shown = Assert.Single(read.Json.GetProperty("shown").EnumerateArray());
        Assert.Equal(($"Keeper:{tiles.Id}", MemoryStanding.Ended), (shown.GetProperty("reference").GetString(), shown.GetProperty("standing").GetString()));
        Assert.Contains("Keeper draws in vectors now.", shown.GetProperty("line").GetString());
    }

    /// <summary>
    /// 🔴 KNOWCENTRAL2e: the page reads a proposal as its quest left it, a decline settled before the rows are answered, so a proposer's
    /// page never shows open what the owner declined.
    /// </summary>
    [Fact]
    public async Task The_page_reads_a_proposal_as_its_quest_left_it()
    {
        var (session, _) = await StagedAsync(new MemoryChange(MemoryOpKind.Write, Text: "Keeper keeps a pond."), DateTimeOffset.UtcNow);
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{session}/state", new { state = "stopped" })).Status);
        var quest = await host.Composed.Quests.PublishAsync("Keeper", "Other", $"Memory proposals ({session})", "A proposal.", DateTimeOffset.UtcNow);
        var by = new MemoryAuthor(session, "default", null, "stub", null, null, null, DateTimeOffset.UtcNow);
        await host.Composed.Sessions.Memory.ProposeAsync(
            Guid.NewGuid().ToString("N")[..16], "Other", new MemoryChange(MemoryOpKind.Propose, Text: "Its pond is gone.", Target: "1", Revision: 1, Relation: "end"), by, quest.Id);
        await host.Composed.Quests.MoveAsync(quest.Id, QuestStatus.Declined, "The pond stays.", DateTimeOffset.UtcNow);

        var page = await host.GetAsync($"/api/sessions/{session}/memory");

        var proposal = page.Json.GetProperty("ops").EnumerateArray().Single(op => op.GetProperty("change").GetProperty("kind").GetString() == "propose");
        Assert.Equal((MemoryOpState.Declined, "The pond stays."), (proposal.GetProperty("state").GetString(), proposal.GetProperty("answer").GetString()));
    }

    /// <summary>🔴 A stopped chat's staged change is dropped by the person's word through its session's door, and a running one's refused.</summary>
    [Fact]
    public async Task The_person_drops_a_stopped_session_s_staged_change()
    {
        var (session, op) = await StagedAsync(new MemoryChange(MemoryOpKind.Write, Text: "Keeper keeps a garden."), DateTimeOffset.UtcNow);
        Assert.Equal(409, (await host.PostAsync($"/api/sessions/{session}/memory", new { keep = false })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{session}/state", new { state = "stopped" })).Status);

        var dropped = await host.PostAsync($"/api/sessions/{session}/memory", new { keep = false });

        Assert.Equal(200, dropped.Status);
        Assert.Equal(1, dropped.Json.GetProperty("moved").GetInt32());
        Assert.Equal(MemoryOpState.Dropped, (await host.Composed.Sessions.Memory.FindAsync(op.Id))!.State);
    }
}
