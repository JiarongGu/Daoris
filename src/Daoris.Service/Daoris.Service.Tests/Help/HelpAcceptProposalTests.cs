namespace Daoris.Service.Tests;

/// <summary>
/// The <c>accept</c> kind's writer (DRIFT1d2, D133 §4): the person's yes to a done's departure from what they required,
/// by the quest's id — the quest page's *Accept the departure* and <c>daoris-driver quest accept</c>.
/// </summary>
public sealed class HelpAcceptProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_yes_is_written_with_the_quest_it_accepts()
    {
        var (id, message) = Box().ProposeAccept("#q1a2b3c4", "the person said the departure is what they want", "h1", Now);

        var written = Written(id!);
        Assert.Equal(
            ("accept", "accept", "q1a2b3c4"),
            (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(), written.GetProperty("target").GetString()));
        Assert.Equal("the person said the departure is what they want", written.GetProperty("why").GetString());
        Assert.Equal("h1", written.GetProperty("by").GetProperty("session").GetString());
        Assert.Equal("proposed", written.GetProperty("state").GetString());
        Assert.Contains($"Proposed `#{id}`", message);
    }

    /// <summary>The shape, checked here and nothing more; whether a departure holds the quest is the driver's to judge.</summary>
    [Theory]
    [InlineData("", "a reason", "names the quest whose departure it accepts")]
    [InlineData("#", "a reason", "names the quest whose departure it accepts")]
    [InlineData("q1 q2", "a reason", "one word")]
    [InlineData("q1a2b3c4", " ", "needs its reason")]
    public void A_malformed_accept_proposal_is_refused_with_nothing_written(string quest, string why, string says)
    {
        var (id, message) = Box().ProposeAccept(quest, why, "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
