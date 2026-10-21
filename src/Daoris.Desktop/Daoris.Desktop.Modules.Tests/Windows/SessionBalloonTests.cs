namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The balloon's choice (NOTIFY1d): which body line and icon a kind of attention gets when the driver's event has no
/// detail. It lived in the application's notifier, which has no test project; here it is tested per kind.
/// </summary>
public sealed class SessionBalloonTests
{
    [Theory]
    [InlineData("Parked", "Open Daoris to answer it.", BalloonIcon.Warning)]
    [InlineData("QuestParked", "Open Daoris to try it again.", BalloonIcon.Warning)]
    [InlineData("Answered", "Open Daoris to read it.", BalloonIcon.Info)]
    [InlineData("Confirming", "Open Daoris to confirm or refuse it.", BalloonIcon.Warning)]
    [InlineData("Ended", "Open Daoris to see what it did.", BalloonIcon.Info)]
    [InlineData("Waiting", "Open Daoris to see what it did.", BalloonIcon.Info)]
    [InlineData(null, "Open Daoris to see what it did.", BalloonIcon.Info)]
    public void Each_kind_has_its_fallback_line_and_its_icon(string? kind, string text, BalloonIcon icon)
    {
        Assert.Equal(text, SessionBalloon.Body(kind, null));
        Assert.Equal(icon, SessionBalloon.Icon(kind));
    }

    [Theory]
    [InlineData("Parked")]
    [InlineData("Answered")]
    public void The_drivers_own_detail_is_shown_as_it_is(string kind)
    {
        Assert.Equal("It asked which branch.", SessionBalloon.Body(kind, "It asked which branch."));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_detail_gets_the_fallback_not_an_empty_body(string detail)
    {
        Assert.Equal("Open Daoris to answer it.", SessionBalloon.Body("Parked", detail));
    }
}
