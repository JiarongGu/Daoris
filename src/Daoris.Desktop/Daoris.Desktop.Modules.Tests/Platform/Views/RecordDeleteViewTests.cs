using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A quest's and an ask's delete as their presses' cards show them: refused where the service's record says it stays, else shown with its command.</summary>
public sealed class RecordDeleteViewTests
{
    private static StubGrounds Records()
    {
        var grounds = new StubGrounds();
        grounds.Quests["q1"] = "Open";
        grounds.Quests["q2"] = "Taken";
        grounds.Quests["q3"] = "Done";
        grounds.Quests["q4"] = "Declined";
        grounds.Asks["a1"] = "Open";
        return grounds;
    }

    private static Task<PressViewed> Quest(object payload) =>
        new QuestDeleteView().ViewAsync(Records(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    private static Task<PressViewed> Ask(object payload) =>
        new AskDeleteView().ViewAsync(Records(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task An_open_quest_is_shown_with_its_command()
    {
        var viewed = await Quest(new { quest = "#q1", said = true });

        Assert.Equal("quest-delete", viewed.View!.Kind);
        Assert.Equal("q1", viewed.View.Facts["quest"]);
        Assert.Equal("daoris quest delete q1", viewed.View.Command);
    }

    [Theory]
    [InlineData("q2", "quest `#q2` is taken: someone is working it, and its record stays. Decline it instead, with the reason.")]
    [InlineData("q3", "quest `#q3` is done: it is the record of work that was answered. Decline it instead, with the reason.")]
    [InlineData("q4", "quest `#q4` is declined: the decline is the trace of a decision. Decline it instead, with the reason.")]
    [InlineData("nope", "there is no quest `#nope` on this machine.")]
    [InlineData("", "a delete names the quest by its id.")]
    public async Task A_quest_whose_record_stays_is_refused_in_its_words(string quest, string words)
    {
        var viewed = await Quest(new { quest });

        Assert.Null(viewed.View);
        Assert.Equal(words, viewed.Refusal);
    }

    [Fact]
    public async Task An_ask_the_service_holds_is_shown_with_its_command()
    {
        var viewed = await Ask(new { ask = "a1", said = true });

        Assert.Equal("ask-delete", viewed.View!.Kind);
        Assert.Equal("daoris ask --delete a1", viewed.View.Command);
    }

    [Theory]
    [InlineData("nope", "there is no ask `#nope` on this machine.")]
    [InlineData(" ", "a delete names the ask by its id.")]
    public async Task An_ask_the_service_lacks_is_refused_in_its_words(string ask, string words)
    {
        var viewed = await Ask(new { ask });

        Assert.Equal(words, viewed.Refusal);
    }

    [Fact]
    public void The_routes_views_are_found_by_their_module_and_route()
    {
        Assert.IsType<QuestDeleteView>(PressViews.Find("daoris.driver", "QUEST_DELETE"));
        Assert.IsType<AskDeleteView>(PressViews.Find("daoris.driver", "ASK_DELETE"));
    }
}
