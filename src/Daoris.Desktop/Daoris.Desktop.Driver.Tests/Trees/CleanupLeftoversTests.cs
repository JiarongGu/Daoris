using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CLEANUP1, the leftovers that never cleared (the fabric events design §C3, §D3): a landing whose branch the person removed reads as the
/// person's end and retires; a doubt keeps it and says why; Daoris's own recoveries no record names are no unit, each kept by its rule; and
/// none of them waits on the person. Pure over <see cref="InventoryFacts"/>, and the landings record's new field in a home of its own.
/// </summary>
public sealed class CleanupLeftoversTests : IDisposable
{
    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly string Trees = Path.GetFullPath(Path.Combine("cleanup-trees", "default", "engine"));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-cleanup-" + Guid.NewGuid().ToString("N")[..8]);

    public CleanupLeftoversTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static GrownBranch Grown(string branch = "daoris/s1", string unit = "unit-a", DateTimeOffset? gone = null) =>
        new("engine", "default", branch, "main", "f0", null, Then) { Unit = unit, GoneAt = gone };

    private static LandedBranch Landed(string branch = "feature/x", string unit = "unit-a", DateTimeOffset? gone = null, string? goneAs = null) =>
        new("engine", "default", branch, "main", "t1", "s1", null, null, Then) { Units = [unit], GoneAt = gone, GoneAs = goneAs };

    private static SessionTree Aside(string name, DateTimeOffset? at) =>
        new(Path.Combine(Trees, ".tidied", $"{name}-{(at ?? Then):yyyyMMdd'T'HHmmssfff'Z'}"), "default", "engine", SessionTree.SetAside) { MovedAside = at };

    private static InventoryFacts Facts(
        GrownBranch[]? branches = null, LandedBranch[]? landings = null, SessionTree[]? trees = null, RecoveryRef[]? refs = null) =>
        new(branches ?? [], landings ?? [], trees ?? [], [], refs ?? [], new HashSet<string>(StringComparer.OrdinalIgnoreCase), _ => false, Then.AddDays(1));

    private static RecoveryRef Ref(string name, bool? held, DateTimeOffset? dated = null) =>
        new("engine", $"refs/daoris/discarded/{name}") { Commit = "c0ffee00", Dated = dated ?? Then, Held = held };

    // ---- (1) a landing whose branch the person removed

    /// <summary>The owner's 18: a landing alone, its branch gone outside Daoris on git's word. The person's end of it: dropped, nothing left to clean, retired.</summary>
    [Fact]
    public void A_landing_whose_branch_git_says_is_missing_reads_dropped_and_retires()
    {
        var facts = Facts(landings: [Landed(gone: Then, goneAs: LandedGone.Missing)]);

        var unit = Assert.Single(WorkInventory.Units(facts));
        var milestone = WorkMilestone.Of(unit);

        Assert.Equal(WorkEndState.Dropped, milestone.End.State);
        Assert.Contains("missing", milestone.End.Said, StringComparison.Ordinal);
        Assert.Contains("remote", milestone.End.Said, StringComparison.Ordinal);
        Assert.DoesNotContain("on the line", milestone.End.Said.Replace("not claim", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal("completed", milestone.Cleanup.State);
        var landing = Assert.Single(milestone.Cleanup.Gone);
        Assert.Equal(("landing-branch", "removed"), (landing.Kind, landing.Disposition));
        Assert.Null(landing.Proof);
        Assert.Equal(WorkUnitState.Retired, unit.State);
    }

    /// <summary>A landing carried by a session branch whose own trace went too reads the same: nothing of the unit stands.</summary>
    [Fact]
    public void A_landing_gone_as_missing_after_its_session_branch_went_reads_dropped()
    {
        var milestone = Assert.Single(WorkInventory.Derive(Facts(branches: [Grown(gone: Then)], landings: [Landed(gone: Then.AddHours(1), goneAs: LandedGone.Missing)])));

        Assert.Equal(WorkEndState.Dropped, milestone.End.State);
        Assert.Equal("completed", milestone.Cleanup.State);
    }

    /// <summary>🔴 Safety: where its session branch still stands, the unit holds work nothing proved anywhere, so it is not dropped.</summary>
    [Fact]
    public void A_landing_gone_as_missing_while_its_session_branch_stands_is_not_dropped()
    {
        var milestone = Assert.Single(WorkInventory.Derive(Facts(branches: [Grown()], landings: [Landed(gone: Then, goneAs: LandedGone.Missing)])));

        Assert.NotEqual(WorkEndState.Dropped, milestone.End.State);
        Assert.Equal("pending", milestone.Cleanup.State);
    }

    /// <summary>A doubt keeps it: a branch of its name stands that is not the landing's.</summary>
    [Fact]
    public void A_landing_whose_name_stands_again_is_kept_and_says_why()
    {
        var milestone = Assert.Single(WorkInventory.Derive(Facts(landings: [Landed(gone: Then, goneAs: LandedGone.Stands)])));

        Assert.Equal(WorkEndState.NotEnded, milestone.End.State);
        Assert.Contains("stands", milestone.End.Said, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing was handed on", milestone.End.Said, StringComparison.Ordinal);
    }

    /// <summary>A trace from before the field, or one git could not read the absence of: kept, saying git has not said it is missing.</summary>
    [Fact]
    public void A_landing_gone_that_git_has_not_said_is_missing_is_kept_and_says_so()
    {
        var milestone = Assert.Single(WorkInventory.Derive(Facts(landings: [Landed(gone: Then)])));

        Assert.Equal(WorkEndState.NotEnded, milestone.End.State);
        Assert.Contains("not yet said", milestone.End.Said, StringComparison.Ordinal);
    }

    // ---- (2) recovery refs, and (3) folders moved aside, that no record names

    /// <summary>A recovery ref no record names is no unit: a recovery kept by its rule, owned by it.</summary>
    [Fact]
    public void A_recovery_ref_nothing_else_holds_is_a_recovery_listed_with_how_to_remove_it_not_a_unit()
    {
        var facts = Facts(refs: [Ref("daoris/s-gone", held: false)]);

        Assert.Empty(WorkInventory.Derive(facts));
        var entry = Assert.Single(WorkInventory.Recoveries(facts));
        Assert.Equal(("recovery-ref", "recovery", "retained"), (entry.Kind, entry.Role, entry.Disposition));
        Assert.Contains("git update-ref -d refs/daoris/discarded/daoris/s-gone", entry.Detail, StringComparison.Ordinal);
        Assert.Null(entry.Until);
    }

    /// <summary>One another ref holds says from when the look removes it.</summary>
    [Fact]
    public void A_recovery_ref_another_ref_holds_says_when_the_look_removes_it()
    {
        var entry = Assert.Single(WorkInventory.Recoveries(Facts(refs: [Ref("daoris/s-held", held: true)])));

        Assert.Equal(Then + RecoveryRetention.Kept, entry.Until);
        Assert.Contains("removes it", entry.Detail, StringComparison.Ordinal);
    }

    /// <summary>One whose holders git could not read is kept by its rule, saying so.</summary>
    [Fact]
    public void A_recovery_ref_whose_holders_did_not_read_is_kept_and_says_so()
    {
        var entry = Assert.Single(WorkInventory.Recoveries(Facts(refs: [Ref("daoris/s-unread", held: null)])));

        Assert.Null(entry.Until);
        Assert.Contains("not read", entry.Detail, StringComparison.Ordinal);
    }

    /// <summary>A recovery ref a record names stays the recovery of that unit, with the same rule.</summary>
    [Fact]
    public void A_recovery_ref_a_record_names_stays_its_units_recovery()
    {
        var facts = Facts(branches: [Grown(gone: Then)], refs: [Ref("daoris/s1", held: false)]);

        var unit = Assert.Single(WorkInventory.Derive(facts));
        Assert.Contains(unit.Recovery, entry => entry.Kind == "recovery-ref" && entry.Detail.Contains("git update-ref -d", StringComparison.Ordinal));
        Assert.Empty(WorkInventory.Recoveries(facts));
    }

    /// <summary>A folder moved aside with its date, which no record names: a recovery kept until its date, not a pending unit with an unknown end.</summary>
    [Fact]
    public void A_moved_aside_folder_before_its_date_reads_as_recovery_not_a_pending_unit()
    {
        var facts = Facts(trees: [Aside("stray", Then)]);

        Assert.Empty(WorkInventory.Derive(facts));
        var entry = Assert.Single(WorkInventory.Recoveries(facts));
        Assert.Equal(("moved-aside", "recovery", "retained", Then + BranchTidying.SetAsideFor), (entry.Kind, entry.Role, entry.Disposition, entry.Until));
    }

    /// <summary>One whose name says no date is never purged by itself, so it stays a unit whose owner is unknown: it does need the person.</summary>
    [Fact]
    public void A_moved_aside_folder_with_no_date_stays_a_unit_its_owner_unknown()
    {
        var milestone = Assert.Single(WorkInventory.Derive(Facts(trees: [Aside("odd", null)])));

        Assert.Equal(WorkEndState.Unknown, milestone.End.State);
    }

    /// <summary>What needs you lists a unit only where a clean-up is blocked or its owner unknown: none of the three kinds is listed.</summary>
    [Fact]
    public void None_of_the_three_kinds_waits_on_the_person()
    {
        var facts = Facts(
            landings: [Landed("feature/a", "unit-a", Then, LandedGone.Missing), Landed("feature/b", "unit-b", Then, LandedGone.Stands)],
            trees: [Aside("stray", Then)],
            refs: [Ref("daoris/s-held", held: true), Ref("daoris/s-alone", held: false)]);

        Assert.All(WorkInventory.Derive(facts), unit =>
        {
            Assert.NotEqual(WorkEndState.Unknown, unit.End.State);
            Assert.DoesNotContain(unit.Cleanup.Waits, wait => wait.Disposition == InventoryDisposition.Blocked);
        });
    }

    // ---- the words and the record

    [Fact]
    public void The_terminal_says_a_dropped_end_and_each_recovery_no_unit_names()
    {
        var facts = Facts(landings: [Landed(gone: Then, goneAs: LandedGone.Missing)], trees: [Aside("stray", Then)], refs: [Ref("daoris/s-gone", held: false)]);

        var unit = string.Join("\n", WorkWords.Milestone(Assert.Single(WorkInventory.Derive(facts))));
        var recovery = string.Join("\n", WorkWords.Recovery(WorkInventory.Recoveries(facts)));

        Assert.Contains("end       dropped: ", unit, StringComparison.Ordinal);
        Assert.Contains("cleanup   completed", unit, StringComparison.Ordinal);
        Assert.Contains("moved-aside", recovery, StringComparison.Ordinal);
        Assert.Contains("recovery-ref `refs/daoris/discarded/daoris/s-gone`", recovery, StringComparison.Ordinal);
        Assert.Empty(WorkWords.Recovery([]));
    }

    /// <summary>The landings record keeps why a branch went (additive: an older trace reads with none), and leaves Daoris's own removal as it was.</summary>
    [Fact]
    public void The_landings_record_keeps_why_a_branch_went_and_never_rewrites_daoris_own_removal()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Landed("feature/x", "unit-a"));
        landings.Record(Landed("feature/y", "unit-b"));
        landings.Removed("engine", "feature/y", LandedKind.OnLine, "main");

        landings.GoneAs("engine", "feature/x", LandedGone.Missing);
        landings.GoneAs("engine", "feature/y", LandedGone.Missing);

        var read = new LandedBranches(_home).Entries();
        var x = Assert.Single(read, each => each.Branch == "feature/x");
        Assert.NotNull(x.GoneAt);
        Assert.Equal(LandedGone.Missing, x.GoneAs);
        var y = Assert.Single(read, each => each.Branch == "feature/y");
        Assert.Null(y.GoneAs);
        Assert.Equal(LandedKind.OnLine, y.RemovedAs);
        Assert.Empty(new LandedBranches(_home).All());
    }
}
