using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Host policy (the agent-lyntai design §5.3, round 5): what was hit is a payload the account lines say, so it is compared exactly and
/// filled from Daoris's own window names where today's sentence names that window, never from a vendor's (<c>seven_day</c>); anything
/// else is unknown until it is stated as the door read it. Daoris's own types, never a proof that the layer supplies the window.
/// </summary>
public sealed class HostPolicyHitTests
{
    [Fact]
    public void Host_policy_fills_what_was_hit_from_daoris_s_window_name_exactly_as_today_s_sentence_says_it()
    {
        var seen = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var today = AccountLimits.Read(ClaudeLimits.Words, "You've hit your weekly limit · resets Oct 6, 10pm (UTC)", seen, TimeZoneInfo.Utc)!;

        Assert.Equal(today.Hit, LimitHit.OfWindow(AccountWindows.Weekly));
    }

    [Theory]
    [InlineData("seven_day")]
    [InlineData("five_hour")]
    [InlineData("individual spend")]
    [InlineData(null)]
    public void Host_policy_names_no_hit_for_a_window_daoris_does_not_name(string? window)
    {
        Assert.Null(LimitHit.OfWindow(window));
    }
}
