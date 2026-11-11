using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landing refused because its tree moved past the tip its <c>land</c> started at is kept with both tips, the tree's it read and the
/// start's: tried again once the tree's tip differs from the one it read (back at the start's, it lands), or once a start is made at another
/// tip, and at no pass where neither moved.
/// </summary>
public sealed class LandTriesMovedTests
{
    private const string Start = "1111111";

    private const string Moved = "2222222";

    private static LandEntry Refused() =>
        new("s1", "q1", "web-app", "work", "tree", DateTimeOffset.UnixEpoch)
        {
            Tries = [new AutoTry(DateTimeOffset.UnixEpoch, LandingCode.Moved) { Tip = Moved, Start = Start, Status = "clean" }],
        };

    /// <summary>A moved head waits for the tree to move again and closes nothing.</summary>
    [Fact]
    public void A_moved_head_waits_for_a_change_and_closes_nothing()
    {
        Assert.True(LandingCode.OnChange(LandingCode.Moved));
        Assert.False(LandingCode.Closes(LandingCode.Moved));
    }

    [Fact]
    public void Nothing_moved_since_the_refusal_tries_nothing()
    {
        Assert.False(LandTries.ShouldTry(Refused(), Moved, "clean", start: Start));
    }

    /// <summary>The tree back at the tip its land started at, with the status of its refusal: tried again, so it lands.</summary>
    [Fact]
    public void A_tree_back_at_its_start_is_tried_again()
    {
        Assert.True(LandTries.ShouldTry(Refused(), Start, "clean", start: Start));
    }

    /// <summary>A start made at the tip the tree moved to shares the tree's tip with the refusal, and is tried all the same.</summary>
    [Fact]
    public void A_start_at_the_tip_the_tree_moved_to_is_tried_again()
    {
        Assert.True(LandTries.ShouldTry(Refused(), Moved, "clean", start: Moved));
    }
}
