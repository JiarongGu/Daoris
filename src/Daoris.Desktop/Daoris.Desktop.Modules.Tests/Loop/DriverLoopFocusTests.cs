using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FABRIC-END6c (finding 5): the main window's focus heard before the loop's watch exists is kept, not dropped, so a start whose window
/// takes the focus first still asks about each open pull request once the moments exist. One pending flag, not a queue.
/// </summary>
public sealed class DriverLoopFocusTests : Bridge
{
    [Fact]
    public void A_focus_heard_before_the_watch_exists_is_kept_for_it()
    {
        var loop = new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0");
        Assert.False(loop.FocusPending);

        loop.Focused();
        loop.Focused();

        Assert.True(loop.FocusPending);
    }
}
