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

    private static LandedBranch Landed(
        string branch = "feature/x", string unit = "unit-a", DateTimeOffset? gone = null, string? goneAs = null, string tip = "t1", DateTimeOffset? at = null) =>
        new("engine", "default", branch, "main", tip, "s1", null, null, at ?? Then) { Units = [unit], GoneAt = gone, GoneAs = goneAs };

    private static SessionTree Aside(string name, DateTimeOffset? at) =>
        new(Path.Combine(Trees, ".tidied", $"{name}-{(at ?? Then):yyyyMMdd'T'HHmmssfff'Z'}"), "default", "engine", SessionTree.SetAside) { MovedAside = at };

    private static InventoryFacts Facts(
        GrownBranch[]? branches = null, LandedBranch[]? landings = null, SessionTree[]? trees = null, RecoveryRef[]? refs = null) =>
        new(branches ?? [], landings ?? [], trees ?? [], [], refs ?? [], new HashSet<string>(StringComparer.OrdinalIgnoreCase), _ => false, Then.AddDays(1));

    private static RecoveryRef Ref(string name, bool? held, DateTimeOffset? dated = null) =>
        new("engine", $"refs/daoris/discarded/{name}") { Commit = "c0ffee00", Dated = dated ?? Then, Held = held };

    // ---- (1) a landing whose branch the person removed

    /// <summary>A landing alone, its branch gone outside Daoris on git's word: the person's end of it, dropped, nothing left to clean, retired.</summary>
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

    /// <summary>
    /// 🔴 After review: a landing whose branch went while another local branch holds its recorded tip moved (a rename): kept, saying where
    /// its work goes on, never read as the person's end.
    /// </summary>
    [Fact]
    public void A_landing_whose_work_moved_to_another_branch_is_kept_and_says_where()
    {
        var milestone = Assert.Single(WorkInventory.Derive(Facts(landings: [Landed(gone: Then, goneAs: LandedGone.Moved) with { MovedTo = "feature/renamed" }])));

        Assert.Equal(WorkEndState.NotEnded, milestone.End.State);
        Assert.Contains("`feature/renamed`", milestone.End.Said, StringComparison.Ordinal);
        Assert.Equal("pending", milestone.Cleanup.State);
        Assert.DoesNotContain(milestone.Cleanup.Waits, wait => wait.Disposition == InventoryDisposition.Blocked);
    }

    /// <summary>A dropped landing whose commits no ref held says the recovery ref Daoris kept them on, not that they may survive elsewhere.</summary>
    [Fact]
    public void A_dropped_landing_whose_commits_daoris_kept_names_the_ref()
    {
        var milestone = Assert.Single(WorkInventory.Derive(Facts(
            landings: [Landed(gone: Then, goneAs: LandedGone.Missing) with { KeptOn = "refs/daoris/discarded/feature/x" }])));

        Assert.Equal(WorkEndState.Dropped, milestone.End.State);
        Assert.Contains("`refs/daoris/discarded/feature/x`", milestone.End.Said, StringComparison.Ordinal);
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

    /// <summary>
    /// 🔴 After review: one past its date whose purge a later write blocks is never finished by its own rule (the purge compares with the
    /// move), so it is a unit in What needs you, saying why; one past its date with nothing written since stays a recovery, which the next
    /// purge takes. Read from the home's own folders, as the gather reads them.
    /// </summary>
    [Fact]
    public async Task A_moved_aside_folder_whose_purge_a_late_write_blocked_needs_the_person()
    {
        var now = DateTimeOffset.UtcNow;
        var moved = now.AddDays(-15);
        var stamp = moved.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var engine = DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine");
        var written = Path.Combine(engine, $"s-late-{stamp}");
        Directory.CreateDirectory(written);
        await File.WriteAllTextAsync(Path.Combine(written, "late.db"), "a write that came after the move\n");
        var quiet = Path.Combine(engine, $"s-quiet-{stamp}");
        Directory.CreateDirectory(quiet);
        await File.WriteAllTextAsync(Path.Combine(quiet, "old.txt"), "written before the move\n");
        File.SetLastWriteTimeUtc(Path.Combine(quiet, "old.txt"), moved.AddDays(-1).UtcDateTime);

        var facts = await WorkInventories.GatherAsync(_home, new Dictionary<string, string>(), new HashSet<string>(), new List<string>(), now: now);
        var units = WorkInventory.Derive(facts);

        var unit = Assert.Single(units);
        var wait = Assert.Single(unit.Cleanup.Waits);
        Assert.Equal((InventoryKind.MovedAside, InventoryDisposition.Blocked, written), (wait.Kind, wait.Disposition, wait.Resource));
        Assert.Contains("late.db", wait.Detail, StringComparison.Ordinal);
        Assert.Contains(quiet, WorkInventory.Recoveries(facts).Select(entry => entry.Resource));
    }

    /// <summary>
    /// 🔴 Re-check: a folder past its date whose read finds nothing late, but whose delete the purge could not finish (a file held open
    /// against it), is a unit in What needs you with the purge's reason, kept where the gather reads it until a later purge takes it.
    /// </summary>
    [Fact]
    public async Task A_moved_aside_folder_whose_delete_failed_needs_the_person_until_a_purge_takes_it()
    {
        if (!OperatingSystem.IsWindows()) return; // a file held open blocks its delete on Windows alone
        var now = DateTimeOffset.UtcNow;
        var moved = now.AddDays(-15);
        var stamp = moved.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var held = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine"), $"s-held-{stamp}");
        Directory.CreateDirectory(held);
        var file = Path.Combine(held, "held.db");
        await File.WriteAllTextAsync(file, "written before the move\n");
        File.SetLastWriteTimeUtc(file, moved.AddDays(-1).UtcDateTime);
        var trees = new SessionTrees(_home);

        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(Assert.Single(trees.PurgeSetAside(now, BranchTidying.SetAsideFor)).Purged);
            var facts = await WorkInventories.GatherAsync(_home, new Dictionary<string, string>(), new HashSet<string>(), new List<string>(), now: now);
            var wait = Assert.Single(Assert.Single(WorkInventory.Derive(facts)).Cleanup.Waits);
            Assert.Equal((InventoryKind.MovedAside, InventoryDisposition.Blocked, held), (wait.Kind, wait.Disposition, wait.Resource));
            Assert.Contains("could not be read or deleted", wait.Detail, StringComparison.Ordinal);
        }

        Assert.True(Assert.Single(trees.PurgeSetAside(now, BranchTidying.SetAsideFor)).Purged);
        var after = await WorkInventories.GatherAsync(_home, new Dictionary<string, string>(), new HashSet<string>(), new List<string>(), now: now);
        Assert.Empty(WorkInventory.Derive(after));
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
        landings.Record(Landed("feature/z", "unit-c"));
        landings.Removed("engine", "feature/y", LandedKind.OnLine, "main");

        landings.GoneAs(Landed("feature/x", "unit-a"), LandedGone.Missing, keptOn: "refs/daoris/discarded/feature/x");
        landings.GoneAs(Landed("feature/y", "unit-b"), LandedGone.Missing);
        landings.GoneAs(Landed("feature/z", "unit-c"), LandedGone.Moved, movedTo: "feature/z2");

        var read = new LandedBranches(_home).Entries();
        var x = Assert.Single(read, each => each.Branch == "feature/x");
        Assert.NotNull(x.GoneAt);
        Assert.Equal((LandedGone.Missing, "refs/daoris/discarded/feature/x"), (x.GoneAs, x.KeptOn));
        var y = Assert.Single(read, each => each.Branch == "feature/y");
        Assert.Null(y.GoneAs);
        Assert.Equal(LandedKind.OnLine, y.RemovedAs);
        var z = Assert.Single(read, each => each.Branch == "feature/z");
        Assert.Equal((LandedGone.Moved, "feature/z2"), (z.GoneAs, z.MovedTo));
        Assert.Empty(new LandedBranches(_home).All());
    }

    /// <summary>
    /// 🔴 After review: an observation applies to the landing it was made of, by its identity (repository, branch, session, when recorded,
    /// and the tip read), never by name: a new landing of the same name recorded since keeps its standing.
    /// </summary>
    [Fact]
    public void An_old_observation_never_retires_a_new_landing_of_the_same_name()
    {
        var landings = new LandedBranches(_home);
        var observed = Landed("feature/x", "unit-a");
        landings.Record(observed);
        var newer = Landed("feature/x", "unit-b", tip: "t2", at: Then.AddHours(1));
        landings.Record(newer);

        landings.GoneAs(observed, LandedGone.Missing);

        var standing = Assert.Single(new LandedBranches(_home).Entries(), each => each.Branch == "feature/x");
        Assert.Equal("t2", standing.Tip);
        Assert.Null(standing.GoneAt);
        Assert.Null(standing.GoneAs);
    }
}
