using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomWindow"/>: how the window is laid out, and that the helper cannot see it.</summary>
public sealed class HelpRoomWindowTests
{
    /// <summary>
    /// HELP2: asked what the panel held, the helper guessed that the View menu names each view's region,
    /// which it does not — its room said nothing of the window. The room says how the window is laid out
    /// and how a view moves, by the names the window uses, and that where the views stand now arrives with
    /// the person's message.
    /// </summary>
    [Fact]
    public void The_room_says_how_the_window_is_laid_out_and_how_a_view_moves()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("## The window", agents);
        foreach (var said in new[]
        {
            "the right side bar", "the panel", "the timeline, the review and the console", "Every view sits in one frame",
            "tab list", "right-click", "drag", "Reset view locations",
            "`Ctrl+B`", "`Ctrl+J`", "`Ctrl+Alt+B`", "`F1`", "`Ctrl+Alt+I`", "`Ctrl+Shift+Alt+L`", "`Ctrl+K`",
            // The person's own terminal (CONSOLE4b), and that it is theirs rather than a session's.
            "terminal: the person's own shell", "never a session's",
        })
        {
            Assert.Contains(said, agents);
        }

        // It cannot see the window: what it is told of it comes with the message, and otherwise it asks.
        Assert.Contains("You cannot see the window", agents);
    }
}
