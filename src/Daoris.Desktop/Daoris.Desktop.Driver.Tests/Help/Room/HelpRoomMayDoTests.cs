using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomMayDo"/>: it reads, advises and proposes, and routes what it cannot reach.</summary>
public sealed class HelpRoomMayDoTests
{
    /// <summary>
    /// HELP4: asked to tidy a repository's branches, the helper's first move was a shell command, refused
    /// before it ran, and it then rebuilt the repository's branches from its quests and presented the
    /// guess as the tree. The room says it has no shell; that a repository's own work is routed there, as
    /// an ask or a conversation in that repository; that what it could not see is said as such; and it
    /// points at the cleanup the person wanted, which is a door of Daoris's own.
    /// HELP5: its fourth move on a real conversation was a fetch of a ticket's URL, refused the same way,
    /// so the same sentence says it has no web either.
    /// </summary>
    [Fact]
    public void The_room_says_it_has_no_shell_nor_web_and_routes_a_repositorys_own_work_there()
    {
        foreach (var machine in new[] { HelpRoomFixture.Machine, HelpRoomFixture.Machine with { Reads = [] } })
        {
            var agents = HelpRoom.Render(machine);

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

    /// <summary>
    /// D107: with reading across on, the room names each checkout it may read, by name, workspace and path,
    /// and how — the files where they lie, and two read-only git commands — and that it changes nothing there.
    /// </summary>
    [Fact]
    public void With_reading_on_the_room_says_which_checkouts_it_may_read_where_and_how()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("You may read the checkouts below and change nothing in them", agents);
        Assert.Contains("- `console-ui` (workspace `work`) — `/work/console-ui`", agents);
        Assert.Contains("- `reports-db` (workspace `work`) — `\"/work/reports db\"`", agents);
        Assert.Contains("`git -C <path> status`", agents);
        Assert.Contains("`git -C <path> branch --list`", agents);
        Assert.DoesNotContain("you read no checkout", agents);
    }

    /// <summary>
    /// D107: with checkouts here and none it may read, the room says it reads none and why — reading is
    /// switched off — naming both doors, so the person knows where it is turned on.
    /// </summary>
    [Fact]
    public void With_reading_off_everywhere_the_room_says_so_and_names_both_doors()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine with { Reads = [] });

        Assert.Contains("you read no checkout", agents);
        Assert.Contains("Reading the checkouts here is switched off", agents);
        Assert.Contains("Settings → Permissions → Reading and writing across", agents);
        Assert.Contains("`daoris driver across <repository> read on`", agents);
    }

    /// <summary>A machine with no checkout here has nothing switched off to speak of.</summary>
    [Fact]
    public void With_no_checkout_here_nothing_is_said_about_reading()
    {
        var agents = HelpRoom.Render(new HelpMachine());

        Assert.Contains("you read no checkout", agents);
        Assert.DoesNotContain("switched off", agents);
    }

    /// <summary>The checkouts it may read are the driver's own answer: each readable one, in every workspace.</summary>
    [Fact]
    public void The_checkouts_it_may_read_are_described_from_the_drivers_setting_and_registry()
    {
        var config = DriverConfig.Empty.WithReadAcross("secret", false);
        var snapshot = new Snapshot(
            [],
            [
                new RepoView("console-ui", Adopted: false, Root: "/work/console-ui", Workspace: "work"),
                new RepoView("secret", Adopted: false, Root: "/work/secret", Workspace: "work"),
                new RepoView("engine", Adopted: true, Root: null),
                new RepoView("notes", Adopted: true, Root: "/srv/notes", Workspace: "home"),
            ],
            []);

        var machine = HelpRoom.Describe(config, snapshot, [], [], _ => null, asks: 0);

        Assert.Equal(
            [new HelpRead("console-ui", "work", "/work/console-ui"), new HelpRead("notes", "home", "/srv/notes")],
            machine.Reads);
    }
}
