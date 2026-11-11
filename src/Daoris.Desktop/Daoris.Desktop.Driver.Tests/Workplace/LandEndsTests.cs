using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each end of a <c>land</c>'s try, as the press that waits on it reads it: a landing as it is, and every other end (closed, nothing, a gate's
/// wait, a moved tip, uncommitted work, a carry-on) not landed, under its own code and with words that say it.
/// </summary>
public sealed class LandEndsTests
{
    [Fact]
    public void A_landing_is_its_own_end()
    {
        var landed = new TreeLanding(true, "landed on main.", "main");

        Assert.Same(landed, LandEnds.Of(LandingCode.Landed, landed, "said"));
    }

    [Theory]
    [InlineData(LandingCode.Already)]
    [InlineData(LandingCode.Gone)]
    [InlineData(LandingCode.Superseded)]
    [InlineData(LandingCode.Undone)]
    [InlineData(LandingCode.Nothing)]
    [InlineData(LandingCode.Uncommitted)]
    public void An_end_with_no_landing_is_said_by_its_try_under_its_code(string code)
    {
        var end = LandEnds.Of(code, landed: null, "its quest is no longer done, so nothing lands.");

        Assert.Equal((false, code, "its quest is no longer done, so nothing lands."), (end.Landed, end.Refusal, end.Message));
    }

    /// <summary>A gate's wait and a moved tip keep the sentence they were refused with.</summary>
    [Theory]
    [InlineData(LandingCode.Unreviewed)]
    [InlineData(LandingCode.Opinion)]
    [InlineData(LandingCode.Moved)]
    public void An_end_refused_with_a_sentence_keeps_it(string code)
    {
        var held = new TreeLanding(false, "the work moved since it was judged; press Accept again.") { Refusal = LandingCode.Moved };

        var end = LandEnds.Of(code, held, "the try's own line");

        Assert.Equal((false, code, held.Message), (end.Landed, end.Refusal, end.Message));
    }

    /// <summary>A carry-on is read as one by its code, whatever landing it carried on from.</summary>
    [Fact]
    public void A_carry_on_is_marked_as_one()
    {
        var conflicted = new TreeLanding(false, "replaying onto main conflicts.") { Refusal = LandingCode.Refused };

        Assert.Equal(LandingCode.CarriedOn, LandEnds.Of(LandingCode.CarriedOn, conflicted, "said").Refusal);
        Assert.Equal(LandingCode.CarriedOn, LandEnds.Of(LandingCode.CarriedOn, null, "said").Refusal);
    }
}
