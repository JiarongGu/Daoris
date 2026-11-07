using Microsoft.Data.Sqlite;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The room says each role's choices, its policy in the fold's words, its next new
/// start and its last seven days, read from the home's records, so <i>why did this run on Opus?</i> is answered from them.
/// </summary>
public sealed class HelpRoomRolesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "help-room-roles", Guid.NewGuid().ToString("N")[..8]);

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public HelpRoomRolesTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        System.IO.File.WriteAllText(File_, """{ "adapter": "claude-code" }""");
    }

    public void Dispose()
    {
        RoleStaffingPort.Unwire(File_);
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    [Fact]
    public void A_role_is_said_with_its_choices_policy_next_start_and_week_and_a_stale_policy_with_why()
    {
        var text = HelpRoom.Render(HelpRoomGoldenTests.Full);

        Assert.Contains("### Roles and their policies", text);
        Assert.Contains("`build` (Build) takes build, design; revision 7; choices 1 claude-code sonnet high, 2 claude-code opus high; "
            + "policy (revision 3): Spread by weight (3:1); on a limit: next by weight; at most 3 moves a day; "
            + "next new start: choice 1 (claude-code sonnet high), by weight, 3 of the last 4 were choice 2; "
            + "last 7 days: 14 starts (claude-code sonnet high ×10, claude-code opus high ×4), moved: limit ×1, transient ×1.", text);
        Assert.Contains("policy (revision 2) was set for other choices, so it runs them in order until it is saved again: its choices changed since it was set", text);
        Assert.Contains("`helper` (Helper) takes help; revision 0; off: it starts nothing; policy (revision 0): First that can run; last 7 days: no new start.", text);
        Assert.Contains("`policy_revision`", text);
    }

    [Fact]
    public void A_workspace_s_room_leaves_out_a_role_scoped_to_another_workspace_and_the_whole_machine_says_all()
    {
        var designer = HelpRoom.Render(HelpRoomGoldenTests.Full);
        Assert.Contains("`designer` (Designer) takes design, workspace work", designer);

        var other = HelpRoom.Render(HelpRoomGoldenTests.Full with { Workspace = "elsewhere" });
        Assert.DoesNotContain("`designer`", other);
        Assert.Contains("`build`", other);
        Assert.Contains("`designer`", HelpRoom.Render(HelpRoomGoldenTests.Full with { Workspace = "work" }));
    }

    [Fact]
    public void A_machine_with_no_roles_says_no_section()
    {
        Assert.DoesNotContain("Roles and their policies", HelpRoom.Render(new HelpMachine()));
    }

    /// <summary>The facts come from the store: the policy a person set, its revision, the next start the policy takes and the week's starts and moves.</summary>
    [Fact]
    public void The_facts_are_read_from_the_home_s_policy_and_its_week()
    {
        var staffing = RoleStaffing.Wire(_home, () => StoreSettled.Yes);
        staffing.ImportFile();
        var sonnet = new ActorChoice("claude-code", "sonnet", "high");
        var opus = new ActorChoice("claude-code", "opus", "high");
        staffing.Set("build", [sonnet, opus]);
        // A preset's moves are merged into the machine's rules a home's first start seeded.
        SeededRules.In(_home);
        RoleDoor.Apply(staffing, RoleChange.Set(RoleCatalog.Seed.Single(role => role.Id == "build")), null, null, new RolePolicyPress(Preset: "weighted"));
        var ledger = new StaffLedger(_home, () => StoreSettled.Yes);
        Assert.True(ledger.Record(new StaffStart("s1", "build", StaffKey.Of(sonnet), "weight")));
        Assert.True(ledger.Record(new StaffStart("s2", "build", StaffKey.Of(opus), "weight", StaffConditions.Limit)));
        Assert.True(ledger.Moves.Record("7", "build", "s1", StaffConditions.Limit, StaffActions.Next, "choice 1"));

        var roles = HelpRoleFacts.Read(_home);

        var build = roles.Single(role => role.Id == "build");
        Assert.Equal(["claude-code sonnet high", "claude-code opus high"], build.Choices);
        Assert.Equal((StaffPolicyRead.Set, 1L), (build.PolicyState, build.PolicyRevision));
        // The policy's moves are the supervisor's rules, which the room's rules section says; the policy says its spread.
        Assert.Equal("Spread by weight (1:1)", build.Policy);
        Assert.Equal("last 7 days: 2 starts (claude-code sonnet high ×1, claude-code opus high ×1), moved: limit ×1", build.Week);
        Assert.False(string.IsNullOrEmpty(build.Next));
        Assert.Equal(StaffPolicyRead.Default, roles.Single(role => role.Id == "intake").PolicyState);

        // Written into a room, the line is there for the helper to read.
        var room = HelpRoom.Prepare(_home, new HelpMachine());
        Assert.Contains("`build` (Build)", System.IO.File.ReadAllText(Path.Combine(room, "AGENTS.md")));
    }

    [Fact]
    public void A_home_that_does_not_read_says_no_roles_and_fails_nothing()
    {
        System.IO.File.WriteAllText(File_, "{ not json");

        Assert.Empty(HelpRoleFacts.Read(_home));

        // A home no one has spent, with nothing in its store, adds nothing to the order and says no section.
        System.IO.File.WriteAllText(File_, """{ "adapter": "claude-code" }""");
        Assert.Empty(HelpRoleFacts.Read(_home));
    }
}
