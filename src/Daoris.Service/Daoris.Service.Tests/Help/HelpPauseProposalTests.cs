using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>pause</c> kind's writer (PAUSE1f-a, D132): an ask's or a quest's work paused or resumed, naming exactly one.
/// Abandon is never proposed here: it is the person's own.
/// </summary>
public sealed class HelpPauseProposalTests : HelpProposalBoxFixture
{
    [Theory]
    [InlineData("pause", "q1", null, "q1")]
    [InlineData("resume", "q1", null, "q1")]
    [InlineData("pause", null, "a1", "a1")]
    [InlineData("resume", null, " #a1 ", "a1")]
    public void A_pause_or_resume_is_written_with_its_one_target(string door, string? quest, string? ask, string target)
    {
        var (id, message) = Box().ProposePause(door, quest, ask, "the person asked to stop it", "h1", Now);

        var written = Written(id!);
        Assert.Equal(
            ("pause", door, target),
            (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(), written.GetProperty("target").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("workspace").ValueKind);
        // PAUSE1f-c: which one was named, so the driver need not tell an ask from a quest by lookup.
        Assert.Equal(quest is null ? "ask" : "quest", written.GetProperty("value").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("sentence").ValueKind);
        Assert.Equal("the person asked to stop it", written.GetProperty("why").GetString());
        Assert.Contains($"Proposed `#{id}`", message);
    }

    [Theory]
    [InlineData("abandon", "q1", null, "a reason", "pause or resume")]
    [InlineData("pause", null, null, "a reason", "name exactly one")]
    [InlineData("pause", "q1", "a1", "a reason", "name exactly one")]
    [InlineData("pause", "q 1", null, "a reason", "one word")]
    [InlineData("pause", "q1", null, " ", "needs its reason")]
    public void A_malformed_pause_proposal_is_refused_with_nothing_written(string door, string? quest, string? ask, string why, string says)
    {
        var (id, message) = Box().ProposePause(door, quest, ask, why, "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }
}
