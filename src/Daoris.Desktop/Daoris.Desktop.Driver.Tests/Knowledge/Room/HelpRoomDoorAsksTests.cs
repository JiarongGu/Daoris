using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>A door's row says where Ask Daoris never asks its act on the card, from its typed answer; a row with none is pressed, asked on the card.</summary>
public sealed class HelpRoomDoorAsksTests
{
    private static HelpDoor Row(string command) =>
        HelpRoomDoors.Doors.Single(door => door.Terminal.Contains(command, StringComparison.Ordinal));

    [Fact]
    public void No_row_writes_how_ask_daoris_meets_it_by_hand()
    {
        foreach (var door in HelpRoomDoors.Doors)
        {
            Assert.DoesNotContain("Ask Daoris never", door.Act, StringComparison.Ordinal);
            Assert.DoesNotContain("Ask Daoris proposes", door.To, StringComparison.Ordinal);
            Assert.DoesNotContain("only when you ask", door.Act, StringComparison.Ordinal);
            Assert.DoesNotContain("never proposes", door.To, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_row_says_its_answer_in_the_words_its_answer_builds()
    {
        var drive = Row("`daoris place drive|undrive <repository>`");
        Assert.Null(drive.Ask);
        Assert.Equal(drive.Act, drive.To);

        var accept = Row("`daoris quest accept <id>`");
        Assert.Equal(new HelpDoorAsk.Never("the yes is yours"), accept.Ask);

        var abandon = Row("`daoris ask abandon <id>");
        Assert.Equal(new HelpDoorAsk.Never("the reason is your answer"), abandon.Ask);
        Assert.Equal($"{abandon.Act} (Ask Daoris never asks it on the card: the reason is your answer)", abandon.To);
    }

    [Fact]
    public void The_room_draws_each_row_as_its_answer_says_it()
    {
        var room = new HelpRoomDoors().Render(HelpRoomFixture.Machine);

        foreach (var door in HelpRoomDoors.Doors) Assert.Contains($"| {door.To} | {door.Screen} | {door.Terminal} |\n", room, StringComparison.Ordinal);
    }
}
