using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A line an account's limit cut off carries its waiting words the way a line with none goes on: the role's choices are walked
/// where nothing of the words' own agent is ready, and never where the person named the account or the role lists nothing else to walk.
/// </summary>
public sealed class CutLineWalkTests
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.Zero);

    private static ResumeChoice Waiting()
    {
        var cooling = new CoolingEntry("stub", "account-1", Seen.AddHours(1), true, "weekly", Seen, "s0");
        var wait = new LineAnswer.Wait(cooling, [cooling], ResetSet.Walk, NextHold.Cooling);
        return new ResumeChoice(new HarnessSelection("every account is cooling") { Cooling = cooling }, NextHold.Cooling) { Answer = wait };
    }

    private static ResumeChoice Ready() =>
        new(new HarnessSelection(null, "account-1"), NextHold.Ready) { Answer = new LineAnswer.Resume() };

    [Fact]
    public void Words_with_no_ready_account_on_their_agent_walk_the_role_s_choices()
    {
        Assert.True(CutLineWalk.Leaves(Waiting(), limitWalks: true, picked: false));
    }

    [Fact]
    public void An_account_ready_for_the_words_keeps_them_there()
    {
        Assert.False(CutLineWalk.Leaves(Ready(), limitWalks: true, picked: false));
    }

    [Fact]
    public void A_role_with_nothing_else_to_walk_or_an_account_the_person_named_keeps_the_wait()
    {
        Assert.False(CutLineWalk.Leaves(Waiting(), limitWalks: false, picked: false));
        Assert.False(CutLineWalk.Leaves(Waiting(), limitWalks: true, picked: true));
    }
}
