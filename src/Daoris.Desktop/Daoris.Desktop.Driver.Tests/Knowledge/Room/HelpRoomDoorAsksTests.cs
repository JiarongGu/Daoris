using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// A door's row says how Ask Daoris meets it (it proposes it, only when the person asks for it, or never) from its typed
/// answer, and a row that proposes names a door its proposal kind takes, so the room cannot say a proposal no kind makes, nor carry the
/// fact hand-written a second time.
/// </summary>
public sealed class HelpRoomDoorAsksTests
{
    private static HelpDoor Row(string command) =>
        HelpRoomDoors.Doors.Single(door => door.Terminal.Contains(command, StringComparison.Ordinal));

    [Fact]
    public void A_row_that_proposes_names_a_door_its_kind_takes()
    {
        var proposing = HelpRoomDoors.Doors.Where(door => door.Ask?.Through is not null).ToList();

        foreach (var door in proposing)
        {
            var (kind, name) = door.Ask!.Through!.Value;
            Assert.True(HelpProposals.DoorsOf(kind).Contains(name, StringComparer.Ordinal), $"{door.Act}: the `{kind}` kind takes no `{name}` door");
        }
    }

    [Fact]
    public void No_row_writes_how_ask_daoris_meets_it_by_hand()
    {
        foreach (var door in HelpRoomDoors.Doors)
        {
            Assert.DoesNotContain("Ask Daoris proposes", door.Act, StringComparison.Ordinal);
            Assert.DoesNotContain("Ask Daoris never", door.Act, StringComparison.Ordinal);
            Assert.DoesNotContain("never proposes", door.Act, StringComparison.Ordinal);
            Assert.DoesNotContain("only when you ask", door.Act, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_row_says_its_answer_in_the_words_its_answer_builds()
    {
        var drive = Row("`daoris place drive|undrive <repository>`");
        Assert.Null(drive.Ask);
        Assert.Equal(drive.Act, drive.To);

        var helpers = Row("`daoris place helpers <repository> --mode");
        Assert.Null(helpers.Ask);
        Assert.Equal(helpers.Act, helpers.To);

        var accept = Row("`daoris quest accept <id>`");
        Assert.Equal(new HelpDoorAsk.Never("the yes is yours"), accept.Ask);
        Assert.Null(accept.Ask!.Through);

        var abandon = Row("`daoris ask abandon <id>");
        Assert.Equal(new HelpDoorAsk.Never("the reason is your answer"), abandon.Ask);
        Assert.Equal($"{abandon.Act} (Ask Daoris never proposes it: the reason is your answer)", abandon.To);
        Assert.Null(abandon.Ask!.Through);
    }

    [Fact]
    public void An_act_done_at_a_terminal_is_never_said_to_be_proposed()
    {
        var terminal = HelpRoomTerminalActs.All.SelectMany(act => act.Terminal).ToList();

        foreach (var door in HelpRoomDoors.Doors.Where(door => door.Ask?.Through is not null))
        {
            Assert.DoesNotContain(terminal, command => door.Terminal.Contains($"`{command}`", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_room_draws_each_row_as_its_answer_says_it()
    {
        var room = new HelpRoomDoors().Render(HelpRoomFixture.Machine);

        foreach (var door in HelpRoomDoors.Doors) Assert.Contains($"| {door.To} | {door.Screen} | {door.Terminal} |\n", room, StringComparison.Ordinal);
    }
}
