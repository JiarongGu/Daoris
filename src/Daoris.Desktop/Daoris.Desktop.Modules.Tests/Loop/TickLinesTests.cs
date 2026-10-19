namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a look's report says, as the loop decides whether to tell the windows (IDLECPU1): a line that only repeats the last
/// look's is no change, so it sends no <c>DRIVER_TICK</c>, and the page asks nothing again for it.
/// </summary>
/// <remarks>
/// A standing condition says its line at every look: a plugin holding a quest (<c>held  #q1 → engine: …</c>), a remote the sync
/// cannot reach (<c>sync  …</c>), a note the remote gives at each pass (<c>held  …</c>). Forwarded each time, every look made
/// the page ask its lists again, LINES and SWEEP_PLAN among them, which ask git of every checkout, on a machine whose every
/// program start costs a quarter of a second or more (DEVDRIVE1). The page already says a repeated line as no news
/// (<c>newsFrom</c>); the loop now forwards by the same rule: changed since the last look, and a line that stops and comes back is
/// news again.
/// </remarks>
public sealed class TickLinesTests
{
    private const string Held = "held  #q1 → engine: the plugin `gate` says wait";
    private const string Sync = "sync  the remote for `aurora` did not answer";

    [Fact]
    public void The_same_lines_said_again_are_no_change()
    {
        Assert.Equal(DriverLoop.LinesSignature([Held, Sync]), DriverLoop.LinesSignature([Held, Sync]));
        // In any order, as the page's news is a set.
        Assert.Equal(DriverLoop.LinesSignature([Held, Sync]), DriverLoop.LinesSignature([Sync, Held]));
        // A line said twice in one look is one piece of news.
        Assert.Equal(DriverLoop.LinesSignature([Held]), DriverLoop.LinesSignature([Held, Held]));
    }

    [Fact]
    public void A_line_new_or_gone_is_a_change()
    {
        Assert.NotEqual(DriverLoop.LinesSignature([Held]), DriverLoop.LinesSignature([Held, Sync]));
        Assert.NotEqual(DriverLoop.LinesSignature([Held, Sync]), DriverLoop.LinesSignature([Sync]));
        // A condition that ended is told, so the page's last lines are empty and its return is news again.
        Assert.NotEqual(DriverLoop.LinesSignature([Held]), DriverLoop.LinesSignature([]));
    }

    [Fact]
    public void A_look_that_says_nothing_signs_as_the_loop_starts()
    {
        // The loop starts from nothing said, so a first look with no line tells nothing, as it never did.
        Assert.Equal("", DriverLoop.LinesSignature([]));
    }
}
