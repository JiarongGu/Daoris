using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// REVIEW2 T12: a door's row says how Ask Daoris meets it (it proposes it, only when the person asks for it, or never) from its typed
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

        Assert.NotEmpty(proposing);
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
        Assert.Equal(new HelpDoorAsk.Proposes("setting", "helpers"), helpers.Ask);
        Assert.Equal($"{helpers.Act} (Ask Daoris proposes it)", helpers.To);

        var accept = Row("`daoris quest accept <id>`");
        Assert.IsType<HelpDoorAsk.OnlyWhenAsked>(accept.Ask);
        Assert.Equal(("accept", "accept"), accept.Ask!.Through);
        Assert.StartsWith($"{accept.Act} (Ask Daoris proposes it only when you ask for it: ", accept.To, StringComparison.Ordinal);

        var abandon = Row("`daoris ask abandon <id>");
        Assert.Equal(new HelpDoorAsk.Never("the reason is your answer"), abandon.Ask);
        Assert.Equal($"{abandon.Act} (Ask Daoris never proposes it: the reason is your answer)", abandon.To);
        Assert.Null(abandon.Ask!.Through);
    }

    [Fact]
    public void A_door_still_owed_is_never_said_to_be_proposed()
    {
        var owed = HelpRoomOwed.All.SelectMany(door => door.Terminal).ToList();

        foreach (var door in HelpRoomDoors.Doors.Where(door => door.Ask?.Through is not null))
        {
            Assert.DoesNotContain(owed, command => door.Terminal.Contains($"`{command}`", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_room_draws_each_row_as_its_answer_says_it()
    {
        var room = new HelpRoomDoors().Render(HelpRoomFixture.Machine);

        foreach (var door in HelpRoomDoors.Doors) Assert.Contains($"| {door.To} | {door.Screen} | {door.Terminal} |\n", room, StringComparison.Ordinal);
    }
}
