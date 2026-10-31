using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREEKEEPRETRY1: a waiting part whose tree and branch are gone lost nothing with them where it ended before it took its quest, from a
/// base the line holds, so its next start opens a fresh tree; any other gone tree still holds the start for the person (TREEGONE1).
/// </summary>
public sealed class TreeGoneFreshTests
{
    private static PriorSession Ended(SessionConclusion conclusion) =>
        new("s3", "trees/default/engine/s-77af1391", conclusion.State, conclusion.Note) { NoteParts = conclusion.Parts, BaseCommit = "4b1c0de" };

    [Fact]
    public void A_part_that_ended_before_it_took_its_quest_from_a_base_the_line_holds_left_nothing_beyond_the_line()
    {
        var before = Ended(Observation.Conclude(0, "Open", turnFailed: "the ACP agent refused the call: Authentication required"));

        Assert.True(TreeReturns.LeftNothing(before, baseOnLine: true));
        Assert.False(TreeReturns.LeftNothing(before, baseOnLine: false));
        Assert.False(TreeReturns.LeftNothing(before, baseOnLine: null));
    }

    [Fact]
    public void A_part_that_held_its_quest_or_ended_otherwise_may_have_worked_and_is_held()
    {
        var taken = Ended(Observation.Conclude(0, "Taken", turnFailed: "the ACP agent refused the call: Internal error"));
        var completed = Ended(new SessionConclusion("completed", "it finished."));

        Assert.False(TreeReturns.LeftNothing(taken, baseOnLine: true));
        Assert.False(TreeReturns.LeftNothing(completed, baseOnLine: true));
        Assert.False(TreeReturns.LeftNothing(Ended(Observation.Conclude(0, "Open", turnFailed: "refused")) with { BaseCommit = null }, baseOnLine: true));
    }
}
