using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The quests the loop's last tick parked by their failed sessions (DRV6): what the quest drawer shows its Retry by,
/// and what Ask Daoris's <c>retry</c> is judged against (HELP10).
/// </summary>
public sealed class ParkedQuestsTests
{
    private static Consideration Considered(string id, string to, StartVerdict verdict) =>
        new(new QuestView(id, "game", to, "A title", "A body.", "Open"), verdict, "the driver's sentence");

    [Fact]
    public void Only_a_quest_its_strikes_parked_is_kept_with_the_repository_it_is_addressed_to()
    {
        var parked = new ParkedQuests();

        parked.Record(
        [
            Considered("q1", "engine", StartVerdict.Exhausted),
            Considered("q2", "engine", StartVerdict.Held),
            Considered("q3", "game", StartVerdict.Start),
            Considered("q4", "game", StartVerdict.Exhausted),
            Considered("q5", "game", StartVerdict.Waiting),
        ]);

        Assert.Equal([new ParkedQuest("q1", "engine"), new ParkedQuest("q4", "game")], parked.Latest);
    }

    /// <summary>Replaced whole each tick, as the drawer's list is: a quest retried, or gone, is no longer parked.</summary>
    [Fact]
    public void Each_tick_replaces_what_the_last_one_parked()
    {
        var parked = new ParkedQuests();
        parked.Record([Considered("q1", "engine", StartVerdict.Exhausted)]);

        parked.Record([Considered("q1", "engine", StartVerdict.Start)]);

        Assert.Empty(parked.Latest);
    }

    [Fact]
    public void Before_any_tick_nothing_is_parked()
    {
        Assert.Empty(new ParkedQuests().Latest);
    }
}
