using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a press on a run whose <c>land</c> the runtime performs answers from the end it waited for: <i>accepted; landing</i> only where no end
/// came within the bound, the carry-on's words only for a carry-on, and every other end in its own words.
/// </summary>
public sealed class PerformedAnswersTests
{
    [Fact]
    public void No_end_within_the_bound_is_answered_landing()
    {
        Assert.Equal(LandPresses.Landing, PerformedAnswers.Of(null).Message);
    }

    [Fact]
    public void A_carry_on_is_answered_as_one()
    {
        var end = new TreeLanding(false, "accepted: its tree has 1 uncommitted path(s).") { Refusal = LandingCode.CarriedOn };

        Assert.Equal((false, LandPresses.Carried), (PerformedAnswers.Of(end).Landed, PerformedAnswers.Of(end).Message));
    }

    /// <summary>A refusal other than a carry-on says itself, even on a run whose review holds a repair from an earlier carry-on.</summary>
    [Theory]
    [InlineData(LandingCode.Refused, "the work moved since it was judged; press Accept again.")]
    [InlineData(LandingCode.Undone, "its quest is no longer done, so nothing lands.")]
    [InlineData(LandingCode.Nothing, "its quest was done and its work made no commits, so nothing was landed.")]
    public void Any_other_end_is_answered_as_it_was_said(string code, string said)
    {
        var end = new TreeLanding(false, said) { Refusal = code };

        Assert.Same(end, PerformedAnswers.Of(end));
    }

    [Fact]
    public void A_landing_is_answered_as_it_landed()
    {
        var landed = new TreeLanding(true, "landed on main.", "main");

        Assert.Same(landed, PerformedAnswers.Of(landed));
    }
}
