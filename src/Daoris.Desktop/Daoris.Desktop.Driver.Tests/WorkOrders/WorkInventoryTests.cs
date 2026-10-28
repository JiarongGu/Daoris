using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-MILESTONE1a (the fabric events design §D0 and §D3): the branch adapter's inventory derived from the records it already
/// keeps, a role and a disposition per entry, and the milestone's answer per unit. Pure over <see cref="InventoryFacts"/>: no git, no
/// home. Canary 27's derivation half: every entry has a disposition, and an unknown owner blocks and says so.
/// </summary>
public sealed class WorkInventoryTests
{
    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly string Trees = Path.GetFullPath(Path.Combine("inventory-trees", "default", "engine"));

    private static HeldFact Held(string by = EndBy.Merged, string on = "main", string tip = "a1b2c3d4e5f6") =>
        new(Then, Then, by, tip, "main", on, "m1", "repo-id");

    private static GrownBranch Grown(string branch = "daoris/s1", string unit = "unit-a", HeldFact? held = null, DateTimeOffset? gone = null, string? now = null) =>
        new("engine", "default", branch, "main", "f0", null, Then, now) { Unit = unit, Held = held, GoneAt = gone };

    private static LandedBranch Landed(string branch = "feature/x", string unit = "unit-a", HeldFact? held = null, DateTimeOffset? gone = null, string? removedAs = null) =>
        new("engine", "default", branch, "main", "t1", "s1", null, null, Then)
        { Units = [unit], Held = held, GoneAt = gone, RemovedAs = removedAs, RemovedOn = removedAs is null ? null : "main" };

    private static SessionTree Tree(string name = "s1", string branch = "daoris/s1") =>
        new(Path.Combine(Trees, name), "default", "engine", branch);

    private static SessionTree Aside(string name = "s1", DateTimeOffset? at = null) =>
        new(Path.Combine(Trees, ".tidied", $"{name}-{(at ?? Then):yyyyMMdd'T'HHmmssfff'Z'}"), "default", "engine", SessionTree.SetAside) { MovedAside = at ?? Then };

    private static InventoryFacts Facts(
        GrownBranch[]? branches = null, LandedBranch[]? landings = null, SessionTree[]? trees = null, AutoLanding[]? autos = null,
        RecoveryRef[]? refs = null, string[]? inUse = null, string[]? history = null, StandingBranch[]? standing = null,
        RecordedTree[]? recorded = null) =>
        new(branches ?? [], landings ?? [], trees ?? [], autos ?? [], refs ?? [], (inUse ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase),
            session => (history ?? []).Contains(session), Then.AddDays(1))
        { Standing = standing ?? [], Recorded = recorded ?? [] };

    private static WorkMilestone One(InventoryFacts facts) => Assert.Single(WorkInventory.Derive(facts));

    // ---- the milestone, for the four cases the row names

    /// <summary>Merged and cleaned: the end reached, the result kept on the line, the cleanup completed with what went and its proof.</summary>
    [Fact]
    public void A_unit_merged_and_cleaned_reads_ended_kept_and_completed()
    {
        var unit = One(Facts(branches: [Grown(held: Held(), gone: Then.AddHours(2))]));

        Assert.Equal("ended", unit.End.State);
        Assert.Equal(EndBy.Merged, unit.End.By);
        Assert.Equal("a1b2c3d4e5f6", unit.End.Revision);
        Assert.Equal(Then, unit.End.At);
        var kept = Assert.Single(unit.Result);
        Assert.Equal(("line", "main", "kept"), (kept.Kind, kept.Resource, kept.Disposition));
        Assert.Equal("completed", unit.Cleanup.State);
        Assert.Empty(unit.Cleanup.Waits);
        var gone = Assert.Single(unit.Cleanup.Gone);
        Assert.Equal(("session-branch", "daoris/s1", "removed"), (gone.Kind, gone.Resource, gone.Disposition));
        Assert.Equal("every commit of it is on `main`", gone.Proof);
        Assert.Empty(unit.Recovery);
    }

    /// <summary>Merged with its tree busy: the end is reached and the cleanup waits, naming the tree and why.</summary>
    [Fact]
    public void A_unit_merged_with_a_busy_tree_reads_cleanup_pending_and_says_why()
    {
        var tree = Tree();
        var unit = One(Facts(branches: [Grown(held: Held())], trees: [tree], inUse: [tree.Path]));

        Assert.Equal("ended", unit.End.State);
        Assert.Equal("pending", unit.Cleanup.State);
        var waiting = unit.Cleanup.Waits.Single(entry => entry.Kind == "tree");
        Assert.Equal("pending", waiting.Disposition);
        Assert.Contains("running or waiting", waiting.Detail);
        Assert.Contains(unit.Cleanup.Waits, entry => entry.Kind == "session-branch" && entry.Disposition == "pending");
    }

    /// <summary>Not ended: nothing is due, and the cleanup waits for the end, said as the judgement says it.</summary>
    [Fact]
    public void A_unit_not_yet_ended_reads_not_reached_and_its_cleanup_waits_for_the_end()
    {
        var unit = One(Facts(branches: [Grown()], trees: [Tree()]));

        Assert.Equal("not-ended", unit.End.State);
        Assert.Null(unit.End.By);
        Assert.Empty(unit.Result);
        Assert.Equal("pending", unit.Cleanup.State);
        Assert.All(unit.Cleanup.Waits, entry => Assert.Equal("waits for the work to end", entry.Detail));
    }

    /// <summary>Recovery retained: the moved-aside folder until its purge date, and the recovery refs, of the unit that went.</summary>
    [Fact]
    public void A_unit_with_recovery_retained_names_the_folder_until_its_purge_date_and_the_refs()
    {
        var unit = One(Facts(
            branches: [Grown(held: Held(), gone: Then)], trees: [Aside("s1", Then)],
            refs: [new RecoveryRef("engine", "refs/daoris/discarded/daoris/s1-2")]));

        Assert.Equal("completed", unit.Cleanup.State);
        Assert.Equal(2, unit.Recovery.Count);
        var folder = Assert.Single(unit.Recovery, entry => entry.Kind == "moved-aside");
        Assert.Equal(Then.AddDays(14), folder.Until);
        Assert.Equal("retained", folder.Disposition);
        var reference = Assert.Single(unit.Recovery, entry => entry.Kind == "recovery-ref");
        Assert.Equal("retained", reference.Disposition);
        Assert.Null(reference.Until);
    }

    // ---- one test per entry kind and disposition

    [Fact]
    public void A_standing_session_branch_is_temporary_and_pending_with_what_waits()
    {
        var entry = One(Facts(branches: [Grown(held: Held())])).Entries.Single(each => each.Kind == "session-branch");

        Assert.Equal(("temporary", "pending"), (entry.Role, entry.Disposition));
        Assert.Contains("tidy removes it", entry.Detail);
    }

    [Fact]
    public void A_removed_session_branch_is_temporary_and_removed_with_its_proof()
    {
        var entry = One(Facts(branches: [Grown(held: Held(EndBy.OnLine), gone: Then)])).Entries.Single(each => each.Kind == "session-branch");

        Assert.Equal(("temporary", "removed"), (entry.Role, entry.Disposition));
        Assert.Equal("its files read on `main` as it left them", entry.Proof);
    }

    [Fact]
    public void A_removed_branch_the_person_removed_has_no_proof_to_show()
    {
        var entry = One(Facts(branches: [Grown(gone: Then)], landings: [Landed()])).Entries.Single(each => each.Kind == "session-branch");

        Assert.Equal("removed", entry.Disposition);
        Assert.Null(entry.Proof);
    }

    [Fact]
    public void A_landing_branch_the_line_does_not_hold_is_a_result_kept_and_the_unit_is_handed()
    {
        var unit = One(Facts(branches: [Grown(gone: Then)], landings: [Landed()]));

        Assert.Equal("handed", unit.End.State);
        var result = Assert.Single(unit.Result);
        Assert.Equal(("landing-branch", "feature/x", "result", "kept"), (result.Kind, result.Resource, result.Role, result.Disposition));
        Assert.Equal("pending", unit.Cleanup.State);
    }

    [Fact]
    public void A_landing_branch_the_line_holds_is_temporary_and_pending_and_the_line_is_the_result()
    {
        var unit = One(Facts(branches: [Grown(gone: Then)], landings: [Landed(held: Held(on: "origin/main"))]));

        Assert.Equal("ended", unit.End.State);
        var landing = unit.Entries.Single(each => each.Kind == "landing-branch");
        Assert.Equal(("temporary", "pending"), (landing.Role, landing.Disposition));
        Assert.Equal("origin/main", Assert.Single(unit.Result).Resource);
    }

    [Fact]
    public void A_landing_branch_the_clean_up_removed_is_removed_with_the_proof_it_kept()
    {
        var unit = One(Facts(branches: [Grown(gone: Then)], landings: [Landed(gone: Then.AddHours(1), removedAs: LandedKind.OnLine)]));

        Assert.Equal("ended", unit.End.State);
        var landing = unit.Entries.Single(each => each.Kind == "landing-branch");
        Assert.Equal("removed", landing.Disposition);
        Assert.Equal("its files read on `main` as it left them", landing.Proof);
        Assert.Equal("completed", unit.Cleanup.State);
    }

    [Fact]
    public void A_tree_is_temporary_and_pending_and_a_session_holding_it_is_said()
    {
        var entry = One(Facts(branches: [Grown()], trees: [Tree()])).Entries.Single(each => each.Kind == "tree");

        Assert.Equal(("temporary", "pending"), (entry.Role, entry.Disposition));
    }

    [Fact]
    public void A_tree_on_the_branch_its_agent_moved_to_belongs_to_the_unit_that_recorded_it()
    {
        var unit = One(Facts(branches: [Grown(now: "daoris/other")], trees: [Tree(branch: "daoris/other")]));

        Assert.Contains(unit.Entries, entry => entry.Kind == "tree");
    }

    /// <summary>
    /// KEEP1a (the cleanup design §5): a folder set aside whose package folders the slim dropped says which, in the line the work
    /// inventory and <c>work state</c> show, beside its date.
    /// </summary>
    [Fact]
    public void A_slimmed_folder_s_line_names_the_package_folders_it_dropped()
    {
        var entry = One(Facts(branches: [Grown(gone: Then)], trees: [Aside("s1", Then) with { Slimmed = ["node_modules", "web/node_modules"] }]))
            .Entries.Single(each => each.Kind == "moved-aside");

        Assert.Equal(
            "moved aside 2026-10-17 09:00 UTC; its package folders went an hour after (node_modules, web/node_modules), which `npm ci` "
            + "makes again; deleted once it is left untouched until 2026-10-31 09:00 UTC",
            entry.Detail);
    }

    [Fact]
    public void A_moved_aside_folder_is_recovery_retained_until_fourteen_days_after_it_moved()
    {
        var entry = One(Facts(branches: [Grown(gone: Then)], trees: [Aside("s1", Then)])).Entries.Single(each => each.Kind == "moved-aside");

        Assert.Equal(("recovery", "retained", Then.AddDays(14)), (entry.Role, entry.Disposition, entry.Until));
    }

    [Fact]
    public void A_recovery_ref_is_matched_to_its_branch_past_the_number_a_second_discard_adds()
    {
        var facts = Facts(branches: [Grown(gone: Then)], refs: [new RecoveryRef("engine", "refs/daoris/discarded/daoris/s1"), new RecoveryRef("engine", "refs/daoris/discarded/daoris/s1-3")]);

        var unit = One(facts);

        Assert.Equal(2, unit.Recovery.Count(entry => entry.Kind == "recovery-ref"));
    }

    [Fact]
    public void An_auto_landing_still_open_is_temporary_and_pending_with_its_last_try()
    {
        var auto = new AutoLanding("s1", "q1", "engine", "default", Path.Combine(Trees, "s1"), Then) { Tries = [new AutoTry(Then.AddMinutes(5), AutoLandingCode.Held)] };
        var unit = One(Facts(branches: [Grown()], landings: [Landed()], autos: [auto]));

        var entry = unit.Entries.Single(each => each.Kind == "auto-landing");
        Assert.Equal(("temporary", "pending"), (entry.Role, entry.Disposition));
        Assert.Contains("held", entry.Detail);
    }

    [Fact]
    public void A_closed_auto_landing_is_evidence_kept()
    {
        var auto = new AutoLanding("s1", "q1", "engine", "default", Path.Combine(Trees, "s1"), Then) { Closed = Then.AddHours(1) };
        var entry = One(Facts(branches: [Grown()], landings: [Landed()], autos: [auto])).Entries.Single(each => each.Kind == "auto-landing");

        Assert.Equal(("evidence", "kept"), (entry.Role, entry.Disposition));
    }

    [Fact]
    public void A_sessions_history_on_this_machine_is_evidence_kept_and_never_the_ends()
    {
        var unit = One(Facts(branches: [Grown(held: Held(), gone: Then)], landings: [Landed(held: Held(), gone: Then)], history: ["s1"]));

        var entry = unit.Entries.Single(each => each.Kind == "history");
        Assert.Equal(("evidence", "kept", "s1"), (entry.Role, entry.Disposition, entry.Resource));
        Assert.DoesNotContain(unit.Cleanup.Waits, wait => wait.Kind == "history");
        Assert.DoesNotContain(unit.Cleanup.Gone, gone => gone.Kind == "history");
    }

    [Fact]
    public void A_session_whose_history_is_not_here_has_no_history_entry()
    {
        Assert.DoesNotContain(One(Facts(branches: [Grown()], landings: [Landed()])).Entries, entry => entry.Kind == "history");
    }

    // ---- old records, and canary 27

    [Fact]
    public void A_branch_recorded_before_units_were_kept_is_a_unit_named_for_its_branch()
    {
        var old = new GrownBranch("engine", "default", "daoris/s9", "main", "f0", null, Then);

        var unit = One(Facts(branches: [old]));

        Assert.Equal("branch:engine/daoris/s9", unit.Unit);
        Assert.Equal("not-ended", unit.End.State);
    }

    [Fact]
    public void A_landing_recorded_before_units_were_kept_is_a_unit_of_its_own()
    {
        var old = new LandedBranch("engine", "default", "feature/old", "main", "t0", "s0", null, null, Then);

        var unit = One(Facts(landings: [old]));

        Assert.Equal("landing:engine/feature/old", unit.Unit);
        Assert.Equal("handed", unit.End.State);
    }

    /// <summary>Canary 27: a tree no record names has an unknown owner. It is blocked, says so, and is never read as ended.</summary>
    [Fact]
    public void A_tree_no_record_names_is_blocked_with_its_owner_unknown()
    {
        var unit = One(Facts(trees: [Tree("stray", "daoris/stray")]));

        Assert.Equal("unknown", unit.End.State);
        Assert.Equal("pending", unit.Cleanup.State);
        var entry = Assert.Single(unit.Entries);
        Assert.Equal("blocked", entry.Disposition);
        Assert.Contains("owner is unknown", entry.Detail);
        Assert.Same(entry, Assert.Single(unit.Cleanup.Waits));
    }

    [Theory]
    [InlineData("(not a tree)", "no repository is linked")]
    [InlineData("(unreadable)", "could not read which branch")]
    public void A_folder_git_cannot_read_is_blocked_with_its_owner_unknown(string branch, string said)
    {
        var entry = Assert.Single(One(Facts(trees: [Tree("odd", branch)])).Entries);

        Assert.Equal("blocked", entry.Disposition);
        Assert.Contains(said, entry.Detail);
    }

    [Fact]
    public void A_tree_git_cannot_read_that_a_record_names_blocks_that_units_cleanup()
    {
        var unit = One(Facts(branches: [Grown(held: Held())], trees: [Tree("s1", "(unreadable)")]));

        Assert.Equal("pending", unit.Cleanup.State);
        Assert.Contains(unit.Cleanup.Waits, entry => entry.Kind == "tree" && entry.Disposition == "blocked");
    }

    /// <summary>Canary 27's derivation half, over every kind at once: each entry has a role and a disposition from the closed sets.</summary>
    [Fact]
    public void Every_entry_of_every_kind_has_a_role_and_a_disposition()
    {
        var autoOpen = new AutoLanding("s1", "q1", "engine", "default", Path.Combine(Trees, "s1"), Then);
        var facts = Facts(
            branches: [Grown(), Grown("daoris/s2", "unit-b", Held(), Then), new GrownBranch("engine", "default", "daoris/old", "main", "f0", null, Then)],
            landings: [Landed(), Landed("feature/y", "unit-b", Held(), Then, LandedKind.Merged), new LandedBranch("engine", "default", "feature/o", "main", "t", "so", null, null, Then)],
            trees: [Tree(), Tree("stray", "daoris/stray"), Tree("odd", "(unreadable)"), Aside("s2", Then), Aside("zz", Then)],
            autos: [autoOpen], refs: [new RecoveryRef("engine", "refs/daoris/discarded/daoris/s2"), new RecoveryRef("engine", "refs/daoris/discarded/daoris/nobody")],
            history: ["s1"]);

        var units = WorkInventory.Derive(facts);

        var roles = new[] { "temporary", "result", "evidence", "recovery" };
        var dispositions = new[] { "kept", "retained", "removed", "blocked", "pending" };
        var entries = units.SelectMany(unit => unit.Entries).ToList();
        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            Assert.Contains(entry.Role, roles);
            Assert.Contains(entry.Disposition, dispositions);
            Assert.False(string.IsNullOrWhiteSpace(entry.Detail), $"{entry.Kind} `{entry.Resource}` says nothing");
        });
        // The ones nobody owns are blocked, or recovery kept by its own rule: never a guess at a unit.
        Assert.All(units.Where(unit => unit.End.State == "unknown"), unit =>
            Assert.All(unit.Entries, entry => Assert.True(entry.Disposition is "blocked" or "retained")));
        Assert.Contains(units, unit => unit.End.State == "unknown" && unit.Cleanup.Waits.Any(wait => wait.Disposition == "blocked"));
        // Every entry is in one unit only.
        Assert.Equal(entries.Count, entries.Distinct().Count());
    }

    // ---- FABRIC-UNKNOWN1: a daoris/* branch no record names

    /// <summary>No record names it and no tree holds it: an unknown owner, blocked, saying why and which press removes it.</summary>
    [Fact]
    public void An_unrecorded_daoris_branch_is_an_unknown_owner_blocked_naming_the_press()
    {
        var unit = One(Facts(standing: [new StandingBranch("engine", "daoris/orphan")]));

        Assert.Equal("unknown", unit.End.State);
        Assert.Equal("daoris/orphan", unit.Branch);
        Assert.Equal("pending", unit.Cleanup.State);
        var wait = Assert.Single(unit.Cleanup.Waits);
        Assert.Equal(("session-branch", "daoris/orphan", "temporary", "blocked"), (wait.Kind, wait.Resource, wait.Role, wait.Disposition));
        Assert.Contains("Daoris did not record `daoris/orphan`", wait.Detail);
        Assert.Contains("Discard branch", wait.Detail);
        Assert.Contains("blocked", string.Join("\n", WorkWords.Milestone(unit)));
    }

    /// <summary>A branch a record names (standing or traced), a landing carries, or a tree holds is not this unit's to name.</summary>
    [Fact]
    public void A_daoris_branch_a_record_or_a_tree_names_is_not_an_unknown_owner()
    {
        var standing = new[]
        {
            new StandingBranch("engine", "daoris/s1"), new StandingBranch("engine", "daoris/now"), new StandingBranch("engine", "daoris/gone"),
            new StandingBranch("engine", "daoris/landed"), new StandingBranch("Engine", "daoris/s9"), new StandingBranch("engine", "feature/x"),
        };
        var units = WorkInventory.Derive(Facts(
            branches: [Grown(), Grown("daoris/old", "unit-b", now: "daoris/now"), Grown("daoris/gone", "unit-c", gone: Then)],
            landings: [Landed("daoris/landed", "unit-a")], trees: [Tree("s9", "daoris/s9")], standing: standing));

        Assert.DoesNotContain(units, unit => unit.End.State == "unknown" && unit.Cleanup.Waits.Any(wait => wait.Kind == "session-branch"));
        Assert.Equal(1, units.Count(unit => unit.Entries.Any(entry => entry.Kind == "session-branch" && entry.Resource == "daoris/s1")));
    }

    // ---- OVERVIEWNOISE1b: a tree joined to the session that recorded it, and each blocked entry worded by a kept code

    /// <summary>
    /// The owner's report: a tree made again on its branch after that branch's record went (TREEGONE1) is named by no standing record,
    /// yet its session recorded it. It joins that session, named by it, its owner known and never titled by its folder's path; and since
    /// no branch record follows it, the look's tidy never takes it: it waits on the clean-up's press, said by its code. While its
    /// session still runs, it is that session's to hold.
    /// </summary>
    [Fact]
    public void A_tree_reopened_after_its_branch_record_went_joins_the_session_that_recorded_it()
    {
        var tree = Tree("s-e0a8e0fd", "daoris/s-e0a8e0fd");
        var trace = Grown("daoris/s-e0a8e0fd", held: Held(), gone: Then.AddHours(1));
        RecordedTree[] recorded = [new("c0ffee01", tree.Path + Path.DirectorySeparatorChar), new("0ther001", Path.Combine(Trees, "s9"))];

        var units = WorkInventory.Derive(Facts(branches: [trace], trees: [tree], recorded: recorded));

        Assert.DoesNotContain(units, unit => unit.End.State == "unknown");
        var joined = Assert.Single(units, unit => unit.Session == "c0ffee01");
        Assert.Equal("session:c0ffee01", joined.Unit);
        Assert.DoesNotContain(tree.Path, joined.Unit, StringComparison.OrdinalIgnoreCase);
        var wait = Assert.Single(joined.Cleanup.Waits);
        Assert.Equal((InventoryKind.Tree, InventoryDisposition.Blocked, InventoryCode.Unfollowed), (wait.Kind, wait.Disposition, wait.Code));
        Assert.Equal("daoris/s-e0a8e0fd", wait.Facts!["branch"]);
        Assert.DoesNotContain("owner is unknown", wait.Detail);
        // The trace keeps its own unit, which the tree never joins.
        Assert.DoesNotContain(units.Single(unit => unit.Unit == "unit-a").Entries, entry => entry.Kind == InventoryKind.Tree);

        var running = WorkInventory.Derive(Facts(branches: [trace], trees: [tree], inUse: [tree.Path], recorded: recorded))
            .Single(unit => unit.Session == "c0ffee01");
        Assert.Equal(InventoryDisposition.Pending, Assert.Single(running.Cleanup.Waits).Disposition);
    }

    /// <summary>A tree its branch's record names is named by the session that recorded it, so its milestone says whose work it is.</summary>
    [Fact]
    public void A_tree_its_branch_record_names_is_named_by_the_session_that_recorded_it()
    {
        var tree = Tree();
        var unit = One(Facts(branches: [Grown()], trees: [tree], recorded: [new("0ther001", Path.Combine(Trees, "s9")), new("c0ffee01", tree.Path)]));

        Assert.Equal("c0ffee01", unit.Session);
        Assert.Equal("unit-a", unit.Unit);
        Assert.Equal(InventoryDisposition.Pending, unit.Cleanup.Waits.Single(entry => entry.Kind == InventoryKind.Tree).Disposition);
    }

    /// <summary>
    /// FABRIC-END4a's blocked branch says its guard once, by its kept code and when, the tidy's own sentence where one was kept and the
    /// guard's words otherwise, never both; and it promises a retry only where the guard frees by itself.
    /// </summary>
    [Theory]
    [InlineData(TidyKept.Nested, "its tree holds another repository at docs/inner", "another repository", false)]
    [InlineData(TidyKept.Busy, null, "holds its tree's folder open", true)]
    [InlineData(SweepKind.Dirty, null, "uncommitted or untracked", false)]
    public void A_branch_the_tidy_kept_says_its_guard_once_by_its_code_and_a_retry_only_where_one_comes(
        string why, string? reason, string said, bool retried)
    {
        var blocked = Grown(held: Held()) with { Blocked = new TidyBlock(why, Then.AddHours(3)) { Reason = reason } };

        var wait = One(Facts(branches: [blocked])).Cleanup.Waits.Single(entry => entry.Kind == InventoryKind.SessionBranch);

        Assert.Equal((InventoryDisposition.Blocked, InventoryCode.Kept), (wait.Disposition, wait.Code));
        Assert.Equal(why, wait.Facts!["why"]);
        Assert.Equal(Moments.Write(Then.AddHours(3)), wait.Facts["at"]);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(wait.Detail, System.Text.RegularExpressions.Regex.Escape(said)));
        Assert.Equal(retried, wait.Detail.Contains("tried again", StringComparison.Ordinal));
    }

    /// <summary>What no record names is coded by why its owner is unknown, with the branch it is on where git named one.</summary>
    [Fact]
    public void An_unknown_owner_is_coded_by_why_with_its_branch()
    {
        var tree = Assert.Single(One(Facts(trees: [Tree("stray", "daoris/stray")])).Entries);
        var folder = Assert.Single(One(Facts(trees: [Tree("odd", SessionTree.NotATree)])).Entries);
        var unread = Assert.Single(One(Facts(trees: [Tree("odd", "(unreadable)")])).Entries);
        var branch = Assert.Single(One(Facts(standing: [new StandingBranch("engine", "daoris/orphan")])).Entries);

        Assert.Equal((InventoryCode.UnknownBranch, "daoris/stray"), (tree.Code, tree.Facts!["branch"]));
        Assert.Equal(InventoryCode.UnknownFolder, folder.Code);
        Assert.Equal(InventoryCode.UnknownUnread, unread.Code);
        Assert.Equal((InventoryCode.Unrecorded, "daoris/orphan"), (branch.Code, branch.Facts!["branch"]));
    }

    [Fact]
    public void Naming_a_unit_finds_it_by_id_session_or_either_branch()
    {
        var all = WorkInventory.Derive(Facts(branches: [Grown()], landings: [Landed()]));

        Assert.Single(WorkInventories.Named(all, "unit-a"));
        Assert.Single(WorkInventories.Named(all, "s1"));
        Assert.Single(WorkInventories.Named(all, "feature/x"));
        Assert.Single(WorkInventories.Named(all, "daoris/s1"));
        Assert.Empty(WorkInventories.Named(all, "nobody"));
    }
}
