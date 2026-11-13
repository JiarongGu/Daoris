using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>*Try again* on a quest taken by a session that has ended names that session and how it ended, instead of the bare "neither parked nor held".</summary>
public sealed class QuestRetryDeadTakerTests
{
    /// <summary>The take a failed session still holds is carried on by the loop itself: the press says so, and that the failure is the take's holder.</summary>
    [Fact]
    public void A_quest_taken_by_a_session_that_failed_says_who_holds_it_and_why_it_failed()
    {
        var quest = new QuestView("q1", "ask #a1", "engine", "The work of #q1", "A body.", "Taken");
        var failed = new PriorSession("330f7d96", "trees/s1", "failed", "Failed to authenticate: Authentication required.");

        var refused = Assert.Throws<ChoiceRefusal>(() => QuestRetry.Decide(
            "q1", [new Consideration(quest, StartVerdict.Start, "carrying on") { Resumes = failed }]));

        Assert.Equal(ChoiceRefusal.NotHeld, refused.Check);
        Assert.Contains("taken by session 330f7d96, which failed: Failed to authenticate: Authentication required.", refused.Message);
        Assert.Contains("carries it on", refused.Message);
    }
}
