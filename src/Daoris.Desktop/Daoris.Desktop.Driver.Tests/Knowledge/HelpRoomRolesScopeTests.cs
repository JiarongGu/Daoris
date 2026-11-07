using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>A workspace's room keeps the roles that are its own, says a role off or unreadable as it is, and says the week is the machine's.</summary>
public sealed class HelpRoomRolesScopeTests
{
    private static HelpMachine Machine(string? workspace) => new()
    {
        Workspace = workspace,
        Repositories = [new HelpRepository("engine", "alpha"), new HelpRepository("tools", "beta")],
        Roles =
        [
            new HelpRole("build", "Build", ["build"], HelpRole.MachineScope, null, 1),
            new HelpRole("mine", "Mine", ["design"], HelpRole.RepositoryScope, "ENGINE", 1) { Choices = ["claude-code"] },
            new HelpRole("theirs", "Theirs", ["design"], HelpRole.RepositoryScope, "elsewhere", 1) { Choices = ["claude-code"] },
            new HelpRole("quiet", "Quiet", ["help"], HelpRole.MachineScope, null, 0) { ChoiceState = "off" },
            new HelpRole("broken", "Broken", ["intake"], HelpRole.MachineScope, null, 0) { ChoiceState = "unreadable" },
        ],
    };

    [Fact]
    public void A_workspaces_room_leaves_out_a_repository_role_of_no_repository_of_its_own()
    {
        var room = new HelpRoomRoles().Render(Machine("alpha"));

        Assert.Contains("`mine`", room);
        Assert.DoesNotContain("`theirs`", room);
        Assert.Contains("`theirs`", new HelpRoomRoles().Render(Machine(null)));
    }

    [Fact]
    public void An_off_or_unreadable_role_is_not_said_to_run_the_defaults()
    {
        var room = new HelpRoomRoles().Render(Machine(null));

        Assert.Contains("`quiet` (Quiet) takes help; revision 0; off: it starts nothing", room);
        Assert.Contains("`broken` (Broken) takes intake; revision 0; its entry in `driver.json` does not read, so it starts nothing", room);
        Assert.Contains("`build` (Build) takes build; revision 1; no choices of its own, so this machine's defaults run", room);
    }

    [Fact]
    public void A_workspaces_room_says_the_last_seven_days_are_the_whole_machines()
    {
        Assert.Contains("are the whole machine's, every workspace's", new HelpRoomRoles().Render(Machine("alpha")));
        Assert.DoesNotContain("whole machine's", new HelpRoomRoles().Render(Machine(null)));
    }
}
