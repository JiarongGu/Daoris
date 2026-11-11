using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Configured access grants families of presses, one shape for every grant: housekeeping's clean-up and discard, the chosen
/// settings and the pauses each move their routes from asked to allowed, and a route in no family is asked.
/// </summary>
public sealed class ActFamiliesTests
{
    [Theory]
    [InlineData(ActFamilies.CleanUp, "SWEEP")]
    [InlineData(ActFamilies.Discard, "DISCARD_SESSION_BRANCH")]
    [InlineData(ActFamilies.Settings, "SET_NOTIFY")]
    [InlineData(ActFamilies.Settings, "SET_LANGUAGE")]
    [InlineData(ActFamilies.Settings, "SET_CAP")]
    [InlineData(ActFamilies.Settings, "SET_COOLOFF")]
    [InlineData(ActFamilies.Settings, "SET_QUIET")]
    [InlineData(ActFamilies.Pause, "WORK_PAUSE")]
    public void A_granted_family_covers_its_presses_and_no_other_family_does(string family, string route)
    {
        Assert.True(ActFamilies.Covers([family], route));
        Assert.False(ActFamilies.Covers([.. ActFamilies.Built.Where(each => each != family)], route));
    }

    [Theory]
    [InlineData("SET_KEEP")]
    [InlineData("SET_TERMINAL")]
    [InlineData("DISCARD_SESSION_TREE")]
    [InlineData("WITHDRAW_LANDING_BRANCH")]
    [InlineData("TREES_SYNC")]
    [InlineData("SET_HELP_GRANT")]
    public void A_press_outside_every_family_is_asked_whatever_is_granted(string route) =>
        Assert.False(ActFamilies.Covers(ActFamilies.Built, route));

    [Fact]
    public void The_page_grants_the_families_in_its_order()
    {
        Assert.Equal(["pause", "clean-up", "discard", "settings"], ActFamilies.Built);
        Assert.Empty(ActFamilies.RoutesOf("inspect"));
    }
}
