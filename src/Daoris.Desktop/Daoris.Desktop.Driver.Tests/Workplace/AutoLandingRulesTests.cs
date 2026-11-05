using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The performer's rule for its tries: a run's <c>land</c> is started once per node and tip, so a try that closed at one tip is not tried there
/// again, and says nothing of a later tip, which is judged afresh. A closing try kept with no tip waits as it did.
/// </summary>
public sealed class AutoLandingRulesTests
{
    private static AutoLanding After(string code, string? tip) => new("s1", "", "engine", "work", "tree", DateTimeOffset.UnixEpoch)
    {
        Tries = [new AutoTry(DateTimeOffset.UnixEpoch, code) { Tip = tip, Status = "clean" }],
    };

    [Theory]
    [InlineData(AutoLandingCode.Landed)]
    [InlineData(AutoLandingCode.Nothing)]
    public void A_try_that_closed_at_one_tip_is_not_made_there_again_and_a_later_tip_is_tried(string code)
    {
        Assert.True(AutoLandingCode.Closes(code));
        Assert.False(AutoLandingRules.ShouldTry(After(code, "tip-1"), "tip-1", "clean"));
        Assert.True(AutoLandingRules.ShouldTry(After(code, "tip-1"), "tip-2", "clean"));
    }

    [Fact]
    public void A_closing_try_kept_with_no_tip_is_not_tried_again()
    {
        Assert.False(AutoLandingRules.ShouldTry(After(AutoLandingCode.Undone, null), "tip-2", "clean"));
    }
}
