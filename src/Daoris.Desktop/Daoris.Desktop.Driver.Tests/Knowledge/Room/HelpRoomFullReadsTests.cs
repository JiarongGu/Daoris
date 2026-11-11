using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Full access's room names the checkouts it may read and where each is (<see cref="HelpRoomFullReads"/>).</summary>
public sealed class HelpRoomFullReadsTests
{
    /// <summary>
    /// In full access the room names each readable checkout with its workspace and place, says to read the code there before
    /// answering and never to search the disk, and leaves out one the person turned reading off in.
    /// </summary>
    [Fact]
    public void Full_access_names_each_readable_checkout_and_where_it_is()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine with { Mode = HelpModes.Full, CheckoutsOff = ["reports-db"] });

        Assert.Contains("- `console-ui` (workspace `work`): `/work/console-ui`", room);
        Assert.DoesNotContain("`reports-db` (workspace", room);
        Assert.Contains("never search the disk for one", room);
        Assert.Contains("`checkout_read`", room);
        Assert.DoesNotContain("You have no shell", room);
    }

    /// <summary>
    /// The update's row says a stage is the person's alone and drains the install, so Ask Daoris never stages one, asks a session to,
    /// or answers a session's question with one.
    /// </summary>
    [Fact]
    public void The_room_says_a_stage_is_the_persons_alone()
    {
        foreach (var mode in new[] { HelpModes.Restricted, HelpModes.Full })
        {
            var room = HelpRoom.Render(HelpRoomFixture.Machine with { Mode = mode });

            Assert.Contains("A build is staged by the person alone", room);
            Assert.Contains("a stage drains the install; a session's terminal is refused both", room);
            Assert.Contains("it never stages a build, asks a session to, or answers a session's question with one", room);
        }
    }

    [Fact]
    public void The_room_sends_a_hold_s_resume_to_the_place_page_that_has_it()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("Agents → Setup → the repository's place → *Hold*, *Resume* in its header", room);
    }

    /// <summary>With nothing readable, nothing is said.</summary>
    [Fact]
    public void Nothing_readable_says_nothing()
    {
        Assert.Equal("", HelpRoomFullReads.Render(HelpRoomFixture.Machine with { Mode = HelpModes.Full, Reads = [] }));
    }
}
