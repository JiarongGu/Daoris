using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The line <c>daoris session list</c> says beside a session's unlanded commits names the verb that lands them: its run's Accept.
/// <c>tree land</c> presses nothing (it says the plan with <c>--plan</c>), so it is never offered as the door.
/// </summary>
public sealed class LandingHintTests
{
    private static readonly LandOffer Offer = new("daoris/s-4e6837ed", "s-4e6837ed", 1, 0);
    private static readonly SessionRecord Record = new("f41led00", "engine", "completed") { Quest = "q1" };

    /// <summary>With no run waiting to name, the run is opened, where its review's Accept lands the work.</summary>
    [Fact]
    public void A_session_with_no_waiting_accept_opens_its_run()
    {
        var failed = new SessionGrouping("f41led00", SessionGroup.Done, "failed") { Lands = Offer };
        var finished = new SessionGrouping("f1n1sh00", SessionGroup.Done, "completed") { Lands = new LandOffer("daoris/s-56cb4d29", "s-56cb4d29", 2, null) };

        Assert.Contains(
            "1 commit on daoris/s-4e6837ed in its tree s-4e6837ed, not landed: its run's Accept lands it; daoris workflow run --session f41led00",
            SessionsCommand.Facts(failed, Record));
        Assert.Contains(
            "2 commits on daoris/s-56cb4d29 in its tree s-56cb4d29, not landed: its run's Accept lands it; daoris workflow run --session f1n1sh00",
            SessionsCommand.Facts(finished, Record with { Id = "f1n1sh00" }));
        Assert.DoesNotContain(SessionsCommand.Facts(failed, Record), fact => fact.Contains("daoris tree land", StringComparison.Ordinal));
    }

    /// <summary>
    /// Where its run waits for the person's Accept, the hint names that verb with the run and node as they are, which the install
    /// accepts; a wait that is not an Accept (an approval) is no door to land by, so the run is opened instead.
    /// </summary>
    [Fact]
    public void A_session_whose_run_waits_for_its_accept_names_the_accept_that_lands_it()
    {
        var accepting = new SessionGrouping("f41led00", SessionGroup.NeedsYou, "completed")
        {
            Lands = Offer, Waits = [new PersonWait("r7n00001", "review", PersonWaits.Accept, "engine", null)],
        };
        var approving = accepting with { Waits = [new PersonWait("r7n00001", "gate", PersonWaits.Approval, "engine", null)] };

        Assert.Contains(
            "1 commit on daoris/s-4e6837ed in its tree s-4e6837ed, not landed: its run's Accept lands it; daoris workflow accept r7n00001 review",
            SessionsCommand.Facts(accepting, Record));
        Assert.Contains(
            "1 commit on daoris/s-4e6837ed in its tree s-4e6837ed, not landed: its run's Accept lands it; daoris workflow run --session f41led00",
            SessionsCommand.Facts(approving, Record));
    }

    /// <summary>A row with no landing offer says nothing of landing.</summary>
    [Fact]
    public void A_row_with_nothing_to_land_says_nothing_of_landing()
    {
        var bare = new SessionGrouping("n0th1ng0", SessionGroup.Done, "failed");

        Assert.DoesNotContain(SessionsCommand.Facts(bare, Record with { Id = "n0th1ng0" }), fact => fact.Contains("land", StringComparison.Ordinal));
    }
}
