using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The room's <see cref="HelpRoomIntro"/>: whose room it is, and, first, the mode its conversation runs in (HELPMODE1a1, D183
/// point 3), so the agent knows it too.
/// </summary>
public sealed class HelpRoomIntroTests
{
    /// <summary>The room opens with its mode: restricted until the person chooses full, read from the driver's file.</summary>
    [Fact]
    public void The_room_opens_with_restricted_mode_until_the_person_chooses_full()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.StartsWith("# Ask Daoris\n\nThis conversation runs in restricted mode:", room);
        Assert.Contains("you read, advise and propose, and the person applies", room);
        Assert.Equal(HelpModes.Restricted, HelpRoomFixture.Machine.Mode);
        Assert.Equal(
            HelpModes.Full,
            HelpRoom.Describe(DriverConfig.Empty.WithHelpMode(HelpModes.Full), new Snapshot([], [], []), [], [], _ => null, asks: 0).Mode);
    }

    /// <summary>
    /// In full access the room opens saying it may act on this machine itself, and the doctrine still speaks (D183 point 5): a
    /// repository's work is asked for through a quest, and Daoris's own housekeeping, its trees and its home, is its to do.
    /// </summary>
    [Fact]
    public void In_full_access_the_room_opens_saying_it_acts_itself_and_what_is_still_a_quest()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine with { Mode = HelpModes.Full });

        Assert.StartsWith("# Ask Daoris\n\nThis conversation runs with full access, as the person chose:", room);
        Assert.Contains("you may act on this machine yourself", room);
        Assert.Contains("A repository's own work is still asked for through a quest", room);
        Assert.Contains("Daoris's own housekeeping, its trees and its home, is yours to do", room);
        Assert.DoesNotContain("restricted mode", room);
    }
}
