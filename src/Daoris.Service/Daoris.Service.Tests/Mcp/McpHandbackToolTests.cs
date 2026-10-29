using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// HANDBACK1 (D219): a session's handback is the note it closes its quest with through the connector, and anyone entitled to the work
/// reads it whole: the steward, another session of the workspace, a terminal. `quest_respond` refuses a note that only points elsewhere.
/// </summary>
public sealed class McpHandbackToolTests : McpToolsFixture
{
    /// <summary>The Owner's connector for a session the driver started.</summary>
    private QuestTools Owner(string session = "s0a1b2c3") =>
        new(_quests, new QuestExchange(_service, _quests, files: _files), In("Owner"), intake: new IntakeScope(null, session));

    /// <summary>
    /// 🔴 The connector's `quest_respond` refuses a done whose note only points at the session's last message, in the refusal's words,
    /// and the quest stays taken; the same session's real handback then closes it.
    /// </summary>
    [Fact]
    public async Task Quest_respond_refuses_a_note_that_only_points_elsewhere_and_takes_the_handback_itself()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Draw the minimap", "The map needs one.", DateTimeOffset.UtcNow);
        var owner = Owner();
        await owner.RespondToQuestAsync(quest.Id, "take");

        var refused = await owner.RespondToQuestAsync(quest.Id, "done", "Details are in my last message");

        Assert.Equal(HandbackNotes.Refusal("Details are in my last message"), refused);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);

        Assert.Contains("is now Done", await owner.RespondToQuestAsync(quest.Id, "done", "Drew the minimap in 4f3e2d1; its render test passes."));
    }
}
