using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// FABRIC-MILESTONE1b: Ask Daoris's room says a unit's milestone (the fabric events design §D0), in the terminal's own words
/// (<see cref="WorkWords"/>), so the helper answers "did it end, what waits, what is kept" from the records and never by a guess.
/// </summary>
public sealed class HelpRoomWorkTests
{
    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private static InventoryEntry Entry(string unit, string kind, string resource, string role, string disposition, string detail) =>
        new(unit, kind, resource, role, disposition, detail);

    private static WorkMilestone Ended(string unit) => new(
        unit, "engine", $"daoris/{unit}", unit,
        new MilestoneEnd("ended", EndBy.Merged, "main", "a1b2c3d4e5f6", Then, "every commit of it is on `main`"),
        [Entry(unit, InventoryKind.Line, "main", InventoryRole.Result, InventoryDisposition.Kept, "the line holds it")],
        new MilestoneCleanup("completed", [], [Entry(unit, InventoryKind.SessionBranch, $"daoris/{unit}", InventoryRole.Temporary, InventoryDisposition.Removed, "went")]),
        [], []);

    private static WorkMilestone Waiting(string unit) => new(
        unit, "engine", $"daoris/{unit}", unit,
        new MilestoneEnd("ended", EndBy.Merged, "main", "a1b2c3d4e5f6", Then, "every commit of it is on `main`"),
        [Entry(unit, InventoryKind.Line, "main", InventoryRole.Result, InventoryDisposition.Kept, "the line holds it")],
        new MilestoneCleanup("pending", [Entry(unit, InventoryKind.Tree, "s1", InventoryRole.Temporary, InventoryDisposition.Blocked, "a session still holds this tree")], []),
        [Entry(unit, InventoryKind.MovedAside, "s1-20261017", InventoryRole.Recovery, InventoryDisposition.Retained, "kept until 2026-10-24")], []);

    [Fact]
    public void The_room_says_the_milestone_of_a_unit_whose_cleanup_waits_in_the_terminals_words()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine with { Work = [Waiting("u1"), Ended("u2")], WorkUnread = ["`engine` has no checkout here, so its recovery refs were not read"] });

        Assert.Contains("## Where work has got to", room);
        Assert.Contains("daoris-driver work state", room);
        Assert.Contains("end       reached: every commit of it is on `main`", room);
        Assert.Contains("cleanup   pending:", room);
        Assert.Contains("blocked  tree `s1`: a session still holds this tree", room);
        // A unit whose cleanup completed with nothing retained is counted, not listed: the room stays short.
        Assert.DoesNotContain("u2  engine", room);
        Assert.Contains("1 unit finished its cleanup with nothing retained", room);
        Assert.Contains("not read: `engine` has no checkout here", room);
    }

    private static InventoryEntry Unheld(string name) => new("", InventoryKind.RecoveryRef, $"refs/daoris/discarded/{name}", InventoryRole.Recovery, InventoryDisposition.Retained,
        $"no branch or tag of yours holds its commit, so Daoris keeps it until you delete it: `git update-ref -d refs/daoris/discarded/{name}` in `engine`")
    { Repository = "engine" };

    private static InventoryEntry Held(string name) => new("", InventoryKind.RecoveryRef, $"refs/daoris/discarded/{name}", InventoryRole.Recovery, InventoryDisposition.Retained,
        "a branch or a tag of yours holds its commit, so the look's tidy removes it once it is 30 days old, from 2026-11-16 09:00 UTC")
    { Until = Then.AddDays(30), Repository = "engine" };

    /// <summary>RECOVERYASK1: a recovery ref no unit names and nothing holds is said with its delete line, so the helper can give the person the command.</summary>
    [Fact]
    public void The_room_names_a_recovery_no_unit_names_and_says_its_delete_line()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine with { Work = [Ended("u2")], WorkRecovery = [Held("daoris/s-old"), Unheld("daoris/s-gone")] });

        Assert.Contains("kept on this machine that no unit names", room);
        Assert.Contains("git update-ref -d refs/daoris/discarded/daoris/s-gone", room);
        Assert.Contains("recovery-ref `refs/daoris/discarded/daoris/s-old`: a branch or a tag of yours holds its commit", room);
        // What the person must delete comes first.
        Assert.True(room.IndexOf("s-gone", StringComparison.Ordinal) < room.IndexOf("s-old", StringComparison.Ordinal));
    }

    /// <summary>Said even where no unit is recorded, and at most five are listed, the rest counted.</summary>
    [Fact]
    public void The_room_lists_five_recoveries_at_most_and_counts_the_rest_even_with_no_unit()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine with { Work = [], WorkRecovery = [.. Enumerable.Range(1, 8).Select(n => Unheld($"daoris/s-{n}"))] });

        Assert.Contains("No unit of work is recorded", room);
        Assert.Contains("daoris/s-5`", room);
        Assert.DoesNotContain("daoris/s-6`", room);
        Assert.Contains("and 3 more", room);
    }

    [Fact]
    public void The_room_says_nothing_of_recoveries_where_there_are_none_and_where_the_milestones_were_not_read()
    {
        Assert.DoesNotContain("no unit names", HelpRoom.Render(HelpRoomFixture.Machine with { Work = [Ended("u2")] }));
        Assert.DoesNotContain("no unit names", HelpRoom.Render(HelpRoomFixture.Machine with { Work = null, WorkRecovery = [Unheld("daoris/s-x")] }));
    }

    [Fact]
    public void The_room_says_when_none_was_read_and_when_there_is_none()
    {
        Assert.Contains("The milestones were not read, so do not say where any work stands", HelpRoom.Render(HelpRoomFixture.Machine with { Work = null }));
        Assert.Contains("No unit of work is recorded on this machine", HelpRoom.Render(HelpRoomFixture.Machine with { Work = [] }));
    }

    [Fact]
    public void The_room_lists_ten_units_at_most_and_counts_the_rest()
    {
        var many = Enumerable.Range(1, 13).Select(n => Waiting($"u{n}")).ToList();
        var room = HelpRoom.Render(HelpRoomFixture.Machine with { Work = many });

        Assert.Contains("u10  engine", room);
        Assert.DoesNotContain("u11  engine", room);
        Assert.Contains("and 3 more", room);
    }
}
