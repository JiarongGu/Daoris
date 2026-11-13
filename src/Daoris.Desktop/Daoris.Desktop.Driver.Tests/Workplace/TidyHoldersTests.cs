using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A declined part is never carried on in its tree, so it holds none: the clean-up judges that tree by its content alone, as any
/// ended session's, and no writer of the checkout waits for it.
/// </summary>
public sealed class TidyHoldersTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Open")]
    [InlineData("Taken")]
    public void The_declined_newest_part_of_a_quest_still_open_holds_no_tree(string status)
    {
        QuestView[] quests = [new("q1", "game", "engine", "Cap", "Cap the work", status)];
        PriorSession[] records =
        [
            new("s0", "/trees/s0", "failed") { Quest = "q1", Created = Now.AddMinutes(-9) },
            new("s1", "/trees/s1", "declined") { Quest = "q1", Created = Now },
        ];

        Assert.Empty(TidyHolders.LineTrees(records, quests));
        Assert.Empty(TidyHolders.LineWaiters(records, quests));
    }

    /// <summary>A part before the declined one is no newest part, so it holds nothing either: the decline ended the line there.</summary>
    [Fact]
    public void An_earlier_part_than_the_declined_one_is_not_held_in_its_place()
    {
        QuestView[] quests = [new("q1", "game", "engine", "Cap", "Cap the work", "Taken")];
        PriorSession[] records =
        [
            new("s0", "/trees/s0", "stopped") { Quest = "q1", Created = Now.AddMinutes(-9) },
            new("s1", "/trees/s1", "declined") { Quest = "q1", Created = Now },
        ];

        Assert.DoesNotContain("/trees/s0", TidyHolders.LineTrees(records, quests));
    }
}
