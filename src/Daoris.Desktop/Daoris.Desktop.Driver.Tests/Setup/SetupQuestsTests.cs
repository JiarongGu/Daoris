using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSSETUP11 (D124 §7.3): a set-up's session is told from any other by its quest's title, which the set-up
/// press composes (D117 §6.2, D124 §2.1–§2.2): the title, with the day after it in brackets. The machine log
/// marks such a session's start, so the usage report can say what each set-up cost.
/// </summary>
public sealed class SetupQuestsTests
{
    [Theory]
    [InlineData("Set up this repository for every agent (2026-10-01)", true)]
    [InlineData("Set up this repository for every agent", true)]
    [InlineData("Declare and document what this repository owns (2026-10-01)", true)]
    [InlineData("Move this repository to the agents layout (2026-10-01)", true)]
    [InlineData("Give this repository an index of where things are (2026-10-01)", true)]
    // Only the press's own words, as it writes them: a title that merely starts the same way is another quest.
    [InlineData("Set up this repository for every agent's laptop", false)]
    [InlineData("Set up this repository for every agent, then the next", false)]
    [InlineData("set up this repository for every agent (2026-10-01)", false)]
    [InlineData(" Set up this repository for every agent (2026-10-01)", false)]
    [InlineData("Add the note field", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_set_up_is_a_quest_titled_as_the_press_titles_one(string? title, bool setup) =>
        Assert.Equal(setup, SetupQuests.IsSetup(title));

    /// <summary>Each title the press composes is one the log knows, whatever the day.</summary>
    [Fact]
    public void Every_title_the_press_composes_is_a_set_up()
    {
        Assert.Equal(4, SetupQuests.Titles.Count);
        foreach (var title in SetupQuests.Titles)
        {
            Assert.True(SetupQuests.IsSetup($"{title} (2027-01-31)"), title);
        }
    }

    /// <summary>ORIENT2c: the index quest is a set-up to the log, and the only one a workspace plan asks once one has landed.</summary>
    [Fact]
    public void The_index_quest_is_a_set_up_and_told_from_the_others()
    {
        var title = SetupQuests.Title(SetupQuests.Index, new DateOnly(2026, 10, 22));

        Assert.True(SetupQuests.IsSetup(title));
        Assert.True(SetupQuests.IsIndex(title));
        Assert.False(SetupQuests.IsIndex(SetupQuests.Title(SetupQuests.SetUp, new DateOnly(2026, 10, 22))));
        Assert.False(SetupQuests.IsIndex("Give this repository an index of where things are, then more"));
    }
}
