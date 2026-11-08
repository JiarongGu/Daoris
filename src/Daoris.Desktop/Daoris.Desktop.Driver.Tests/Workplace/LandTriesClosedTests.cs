using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A try closed for what a pass reads again before it asks <see cref="LandTries.ShouldTry"/> (its quest no longer done, its tree gone, a newer
/// session on it) is kept with no tip; once what closed it no longer holds, it is tried, not left until a press or a restart.
/// </summary>
public sealed class LandTriesClosedTests
{
    private static LandEntry Closed(string code, string? tip) =>
        new("s1", "q1", "web-app", "work", "tree", DateTimeOffset.UnixEpoch)
        {
            Tries = [new AutoTry(DateTimeOffset.UnixEpoch, code) { Tip = tip }],
        };

    [Theory]
    [InlineData(LandingCode.Undone)]
    [InlineData(LandingCode.Gone)]
    [InlineData(LandingCode.Superseded)]
    public void A_try_closed_with_no_tip_is_tried_at_the_next_look(string code)
    {
        Assert.True(LandTries.ShouldTry(Closed(code, null), "6f1c0de", "clean"));
    }

    [Fact]
    public void A_try_that_closed_at_a_tip_is_still_not_tried_at_that_tip()
    {
        Assert.False(LandTries.ShouldTry(Closed(LandingCode.Already, "6f1c0de"), "6f1c0de", "clean"));
    }
}
