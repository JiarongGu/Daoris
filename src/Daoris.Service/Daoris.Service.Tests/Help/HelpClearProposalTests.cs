using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>clear</c> kind's writer (HIST1f-a, D153): a quest's, an ask's or a workspace's finished history cleared, naming
/// exactly one. What would be cleared and what kept is the driver's to judge.
/// </summary>
public sealed class HelpClearProposalTests : HelpProposalBoxFixture
{
    [Theory]
    [InlineData("quest", "q1", null, null, "q1")]
    [InlineData("failed", " #q1 ", null, null, "q1")]
    [InlineData("ask", null, " #a1 ", null, "a1")]
    [InlineData("workspace", null, null, " Main ", "Main")]
    public void A_clear_is_written_with_its_one_target(string door, string? quest, string? ask, string? workspace, string target)
    {
        var (id, message) = Box().ProposeClear(door, quest, ask, workspace, "the person asked to clear it", "h1", Now);

        var written = Written(id!);
        Assert.Equal(
            ("clear", door, target),
            (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(), written.GetProperty("target").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("workspace").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("value").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("sentence").ValueKind);
        Assert.Equal("the person asked to clear it", written.GetProperty("why").GetString());
        Assert.Contains($"Proposed `#{id}`", message);
    }

    [Theory]
    [InlineData("history", "q1", null, null, "a reason", "quest, ask, failed or workspace")]
    [InlineData("failed", null, "a1", null, "a reason", "failed door names a quest")]
    [InlineData("failed", "q 1", null, null, "a reason", "one word")]
    [InlineData("quest", null, null, null, "a reason", "name exactly one")]
    [InlineData("quest", "q1", "a1", null, "a reason", "name exactly one")]
    [InlineData("quest", null, "a1", null, "a reason", "door names")]
    [InlineData("workspace", null, null, "a b", "", "needs its reason")]
    [InlineData("quest", "q 1", null, null, "a reason", "one word")]
    [InlineData("ask", null, "a 1", null, "a reason", "one word")]
    public void A_malformed_clear_proposal_is_refused_with_nothing_written(string door, string? quest, string? ask, string? workspace, string why, string says)
    {
        var (id, message) = Box().ProposeClear(door, quest, ask, workspace, why, "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }
}
