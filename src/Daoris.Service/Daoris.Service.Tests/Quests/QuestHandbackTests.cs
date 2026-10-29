using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// HANDBACK1 (D219): a quest's handback is the note its session closes it with through the connector, written once at that act. The
/// exchange refuses a session's close whose note only points elsewhere, before anything moves; every other door keeps its words, the
/// person's among them.
/// </summary>
public sealed class QuestHandbackTests : McpToolsFixture
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-29T10:00:00Z");

    private QuestExchange Exchange() => new(_service, _quests, files: _files);

    /// <summary>A quest the Asker asked of the Owner, taken.</summary>
    private async Task<Quest> TakenAsync(QuestExchange exchange, string title = "Draw the minimap")
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", title, "The map needs one.", Now);
        Assert.Equal(QuestRespondRefusal.None, (await exchange.RespondAsync(quest.Id, "take", null, Now, session: "s0a1b2c3")).Refusal);
        return quest;
    }

    /// <summary>
    /// 🔴 A done and a decline through the connector whose note only points elsewhere are refused in the refusal's words, and the
    /// quest stays taken: nothing was written.
    /// </summary>
    [Theory]
    [InlineData("done", "Details are in my last message")]
    [InlineData("decline", "Handback is in my last message")]
    public async Task A_sessions_close_that_only_points_elsewhere_is_refused_and_nothing_moves(string action, string note)
    {
        var exchange = Exchange();
        var quest = await TakenAsync(exchange);

        var refused = await exchange.RespondAsync(quest.Id, action, note, Now, session: "s0a1b2c3", handback: true);

        Assert.Equal(QuestRespondRefusal.PointsElsewhere, refused.Refusal);
        Assert.Equal(HandbackNotes.Refusal(note), refused.Message);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);
    }

    /// <summary>🔴 A ready whose note only points elsewhere is refused the same, and no submission is kept.</summary>
    [Fact]
    public async Task A_ready_that_only_points_elsewhere_is_refused_and_keeps_no_submission()
    {
        var exchange = Exchange();
        var quest = await TakenAsync(exchange);
        Assert.Equal(QuestRespondRefusal.None, (await exchange.BindAsync(quest.Id, "sha256:0f1e2d3c4b5a6978", Now)).Refusal);

        var refused = await exchange.RespondAsync(quest.Id, "ready", "See the commit.", Now, session: "s0a1b2c3", handback: true);

        Assert.Equal(QuestRespondRefusal.PointsElsewhere, refused.Refusal);
        Assert.Null((await _quests.FindAsync(quest.Id))!.Submission);
    }

    /// <summary>A session's short real handback closes the quest, its note kept as it gave it.</summary>
    [Fact]
    public async Task A_sessions_short_real_handback_closes_the_quest()
    {
        var exchange = Exchange();
        var quest = await TakenAsync(exchange);

        var closed = await exchange.RespondAsync(quest.Id, "done", "Fixed X in abc123; tests Y pass", Now, session: "s0a1b2c3", handback: true);

        Assert.Equal(QuestRespondRefusal.None, closed.Refusal);
        Assert.Equal("Fixed X in abc123; tests Y pass", (await _quests.FindAsync(quest.Id))!.Note);
    }

    /// <summary>
    /// 🔴 A session's done and decline keep the session on their operation, written in the same write as the close, as a ready's
    /// submission keeps it: which session wrote the handback. A close through a door that names no session keeps none.
    /// </summary>
    [Theory]
    [InlineData("done")]
    [InlineData("decline")]
    public async Task A_sessions_close_names_its_session_on_its_operation(string action)
    {
        var exchange = Exchange();
        var named = await TakenAsync(exchange);
        var unnamed = await TakenAsync(exchange, "Draw the legend");

        Assert.Equal(QuestRespondRefusal.None,
            (await exchange.RespondAsync(named.Id, action, "Drew it in 4f3e2d1.", Now, session: "s0a1b2c3", handback: true)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await exchange.RespondAsync(unnamed.Id, action, "Drew it in 4f3e2d1.", Now)).Refusal);

        Assert.Equal("s0a1b2c3", (await _quests.HistoryAsync(named.Id))[^1].Session);
        Assert.Null((await _quests.HistoryAsync(unnamed.Id))[^1].Session);
    }

    /// <summary>
    /// 🔴 The review of 2026-10-29: the read answers the quest's latest close, never an older one. A session's ready, then the person's
    /// decline through a door that names no session: the decline is the answer, said as the quest's own note, and the ready is not.
    /// </summary>
    [Fact]
    public async Task A_later_close_through_another_door_is_answered_over_a_sessions_earlier_ready()
    {
        var exchange = Exchange();
        var quest = await TakenAsync(exchange);
        Assert.Equal(QuestRespondRefusal.None, (await exchange.BindAsync(quest.Id, "sha256:0f1e2d3c4b5a6978", Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.None,
            (await exchange.RespondAsync(quest.Id, "ready", "Implemented the minimap in 4f3e2d1.", Now, session: "s0a1b2c3", handback: true)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await exchange.RespondAsync(quest.Id, "decline", "The person stopped this work: the map moved.", Now)).Refusal);

        var read = await new HandbackReader(_quests).ReadAsync(quest.Id);

        Assert.Null(read.Handback);
        Assert.Contains("No session's handback is recorded on it", read.Said);
        Assert.Contains("The person stopped this work: the map moved.", read.Said);
        Assert.DoesNotContain("Implemented the minimap", read.Said);
    }

    /// <summary>
    /// 🔴 The graph's variant: a session's ready, the person's reopen, a ready no session named, and the graph's close on that last
    /// ready. The close is the graph's copy of the last ready's note and is skipped; the last ready, naming no session, is the answer.
    /// </summary>
    [Fact]
    public async Task A_graph_close_on_a_ready_no_session_named_answers_that_ready_not_an_earlier_sessions()
    {
        var at = Now;
        const string id = "4a2db0c40002";
        var asked = new Quest(id, "Asker", "Owner", "Draw the legend", "The map needs one.", QuestStatus.Open, null, at, at);
        QuestOperation Op(QuestOperationKind kind, long sequence, string? note = null) => new(id, kind, "m7", sequence, at.AddMinutes(sequence), note, Number: sequence);
        await _quests.IntegrateAsync(Workspaces.Default,
        [
            Op(QuestOperationKind.Published, 1) with { Published = asked },
            Op(QuestOperationKind.Taken, 2),
            Op(QuestOperationKind.Bound, 3, "sha256:0f1e2d3c4b5a6978"),
            Op(QuestOperationKind.Submitted, 4, "Ready A: drew the legend in 4f3e2d1.") with { Session = "s0a1b2c3" },
            Op(QuestOperationKind.Reopened, 5, "The legend is wrong.") with { Submission = new QuestOperationRef("m7", 4) },
            Op(QuestOperationKind.Submitted, 6, "Ready B: drew it again in 1a2b3c4."),
            Op(QuestOperationKind.Done, 7, "Ready B: drew it again in 1a2b3c4.") with { Close = new QuestClose("close-1", "token-1") },
        ], 7);
        Assert.Equal(QuestStatus.Done, (await _quests.FindAsync(id))!.Status);

        var read = await new HandbackReader(_quests).ReadAsync(id);

        Assert.Null(read.Handback);
        Assert.Contains("No session's handback is recorded on it", read.Said);
        Assert.Contains("Ready B: drew it again in 1a2b3c4.", read.Said);
        Assert.DoesNotContain("Ready A", read.Said);
    }

    /// <summary>
    /// Another door's words are kept as given: the person's abandon and a page's decline come through the respond door with no
    /// handback to judge, so a reason that names where to look still declines.
    /// </summary>
    [Fact]
    public async Task Another_doors_decline_keeps_its_words()
    {
        var exchange = Exchange();
        var quest = await TakenAsync(exchange);

        var declined = await exchange.RespondAsync(quest.Id, "decline", "See the chat.", Now);

        Assert.Equal(QuestRespondRefusal.None, declined.Refusal);
        Assert.Equal(QuestStatus.Declined, (await _quests.FindAsync(quest.Id))!.Status);
    }
}
