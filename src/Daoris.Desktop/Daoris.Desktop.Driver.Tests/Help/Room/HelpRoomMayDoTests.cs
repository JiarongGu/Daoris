using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomMayDo"/>: it reads, advises and proposes, and routes what it cannot reach.</summary>
public sealed class HelpRoomMayDoTests
{
    /// <summary>
    /// HELP4: asked to tidy a repository's branches, the helper's first move was a shell command, refused
    /// before it ran, and it then rebuilt the repository's branches from its quests and presented the
    /// guess as the tree. The room says it has no shell and reads no checkout; that a repository's own
    /// work is routed there, as an ask or a conversation in that repository; that what it could not see
    /// is said as such; and it points at the cleanup the person wanted, which is a door of Daoris's own.
    /// HELP5: its fourth move on a real conversation was a fetch of a ticket's URL, refused the same way,
    /// so the same sentence says it has no web either.
    /// </summary>
    [Fact]
    public void The_room_says_it_has_no_shell_nor_web_and_routes_a_repositorys_own_work_there()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("You have no shell", agents);
        Assert.Contains("no web fetch or search", agents);
        Assert.Contains("never try one", agents);
        Assert.Contains("a repository's own work", agents);
        Assert.Contains("`ask_propose`", agents);
        Assert.Contains("Sessions → Start a session", agents);
        Assert.Contains("say what you could not see", agents);
        Assert.Contains("Session branches", agents);
    }
}
