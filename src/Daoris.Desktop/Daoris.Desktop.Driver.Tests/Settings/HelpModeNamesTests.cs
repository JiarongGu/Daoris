using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Ask Daoris's access mode as <c>driver.json</c>'s <c>helpMode</c> names it (FOUNDATION1 mod-iii): the settings' own reading, which
/// Help's <c>HelpModes</c> answers through, so the key is read alike wherever a mode is named.
/// </summary>
public sealed class HelpModeNamesTests
{
    [Fact]
    public void The_modes_are_restricted_then_full_and_help_answers_through_them()
    {
        Assert.Equal(["restricted", "full"], HelpModeNames.All);
        Assert.Equal(HelpModeNames.Restricted, HelpModes.Restricted);
        Assert.Equal(HelpModeNames.Full, HelpModes.Full);
        Assert.Equal(HelpModeNames.All, HelpModes.All);
    }

    [Theory]
    [InlineData("full", "full")]
    [InlineData("  FULL ", "full")]
    [InlineData("Restricted", "restricted")]
    [InlineData("everything", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void A_mode_is_named_trimmed_and_in_any_case_or_none(string? spelled, string? mode)
    {
        Assert.Equal(mode, HelpModeNames.Named(spelled));
        Assert.Equal(mode, HelpModes.Named(spelled));
    }
}
