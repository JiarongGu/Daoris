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
    private async Task<Quest> TakenAsync(QuestExchange exchange)
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Draw the minimap", "The map needs one.", Now);
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
