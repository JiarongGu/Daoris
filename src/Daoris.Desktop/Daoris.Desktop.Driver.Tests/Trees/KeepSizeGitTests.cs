using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEPSIZE1b over real git (D195's keep-size notes; the keep-size design §2 and §3, as amended after Codex's reads): a finished tree's record
/// kept, and its tree let go an hour after. The look holds the exact tip and writes a schema-2 manifest; the record pass after the hold
/// composes the record from the manifest by <c>git log --first-parent</c> and posts it; the one purge takes a recorded folder an hour after
/// its record is acknowledged, on a holding check run right before its rename, renamed into <c>.going</c> first, and every other class by its
/// days or never. The stops, the faults and the press are <see cref="KeepSizeStopsGitTests"/>'; the service's half is
/// <c>WorkRecordStoreTests</c>' and <c>WorkRecordHostTests</c>'; the service not answering is <c>KeepingSlimTests</c>'.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class KeepSizeGitTests : KeepSizeFixture
{
    /// <summary>
    /// 🔴 A landed tree's record kept with its commits, its tip and its named scratch (§2.1); its folder kept within the hour after its record,
    /// kept past it by a purge that cannot run a holding check, and gone after it by one that can, through <c>.going</c>, which holds nothing after.
    /// </summary>
    [Fact]
    public async Task A_landed_trees_record_is_kept_and_its_folder_goes_an_hour_after()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await CommitAsync(tree.Path, "second.txt", "more\n", "the second commit");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "probe.db"), "the session's own scratch\n");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", tree.Branch);
        var tip = (await GitAsync(root, "rev-parse", tree.Branch)).Trim();

        var aside = await SetAsideAsync(trees, root);

        var deleted = Manifest(aside);
        Assert.Equal((SlimStates.Deleted, tree.Branch, tip), (deleted.State, deleted.Branch, deleted.Finished!.Tip));
        // Merged onto the line, its tip is an ancestor of the line: held by the line itself, so no ref is written.
        Assert.Equal(("empty", (string?)null), (deleted.Finished.Hold.Kind, deleted.Finished.Ref));
        Assert.Contains("probe.db", deleted.Finished.Left.Names);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());

        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);

        var post = Assert.Single(_posts);
        Assert.Equal(["the work", "the second commit"], post.Commits!.Select(commit => commit.Subject));
        Assert.Equal(tip, post.Commits![^1].Id);
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
        Assert.Empty(await PurgeAsync(trees, now.AddMinutes(30), root));
        Assert.Equal(SetAsideKept.Unheld, Assert.Single(await PurgeAsync(trees, now.AddMinutes(61))).Code);
        Assert.True(Directory.Exists(aside));

        var gone = Assert.Single(await PurgeAsync(trees, now.AddMinutes(61), root));

        Assert.Equal((true, SetAsideClass.Recorded, true), (gone.Purged, gone.Class, gone.Hour));
        Assert.False(Directory.Exists(aside));
        Assert.False(File.Exists(SlimManifest.PathFor(aside)));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(Going(trees), "*", SearchOption.AllDirectories), File.Exists);
    }

    /// <summary>
    /// 🔴 Codex's read of the branch, finding 1: the hour is authorized by a holding check run right before the rename, never by an earlier
    /// one. A record pass's check passing, then the ref dropped, the branch made again, or the registry not read before the purge: each keeps
    /// the folder; a ref gone for certain unmarks it, and a pass whose check passes again lets it go.
    /// </summary>
    [Fact]
    public async Task An_old_holding_check_never_authorizes_a_new_deletion()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await SquashOntoLineAsync(root, tree.Branch);
        var aside = await SetAsideAsync(trees, root);
        var reference = Manifest(aside).Finished!.Ref!;
        var tip = (await GitAsync(root, "rev-parse", reference)).Trim();
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);
        await RecordAsync(trees, root, now.AddMinutes(61));
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);

        // The branch made again after the record pass's check: the purge's own check finds it, and keeps the folder.
        await GitAsync(root, "branch", tree.Branch, tip);
        var stands = Assert.Single(await PurgeAsync(trees, now.AddMinutes(62), root));
        Assert.Equal((false, SetAsideKept.Unheld), (stands.Purged, stands.Code));
        Assert.Contains("its branch stands", stands.Kept, StringComparison.Ordinal);
        await GitAsync(root, "branch", "-D", tree.Branch);
        (Manifest(aside) with { State = SlimStates.Recorded, Why = null }).Write(SlimManifest.PathFor(aside));

        // The registry not read: no check can run, so nothing goes by the hour.
        Assert.Equal(SetAsideKept.Unheld, Assert.Single(await PurgeAsync(trees, now.AddMinutes(63))).Code);
        Assert.True(Directory.Exists(aside));

        // The ref dropped after the record pass's check: the purge's check finds it gone, and the folder waits its days.
        await GitAsync(root, "update-ref", "-d", reference);
        var dropped = Assert.Single(await PurgeAsync(trees, now.AddMinutes(64), root));
        Assert.Equal((false, SetAsideKept.Unheld), (dropped.Purged, dropped.Code));
        Assert.Equal(SlimStates.Unmarked, Manifest(aside).State);
        Assert.True(Directory.Exists(aside));
    }

    /// <summary>
    /// 🔴 Codex finding 1 of round 2: a multi-commit session squashed onto the line. The tip's own ref is written before the branch goes, so the
    /// session's commits and messages read from it after both the session branch and the squash's branch are removed.
    /// </summary>
    [Fact]
    public async Task A_squashed_sessions_own_commits_read_from_its_tips_ref_after_both_branches_go()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await CommitAsync(tree.Path, "second.txt", "more\n", "the second commit");
        var tip = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await SquashOntoLineAsync(root, tree.Branch);

        var aside = await SetAsideAsync(trees, root);
        await GitAsync(root, "branch", "-D", "feature/squashed");

        var finished = Manifest(aside).Finished!;
        Assert.Equal($"refs/daoris/discarded/{tree.Branch}", finished.Ref);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", finished.Ref!)).Trim());
        await RecordAsync(trees, root, DateTimeOffset.UtcNow);
        Assert.Equal(["the work", "the second commit"], Assert.Single(_posts).Commits!.Select(commit => commit.Subject));
    }

    /// <summary>
    /// 🔴 Round 3's finding 10: a branch that merged its line has its record's commits read by <c>--first-parent</c>: the merge is one of the
    /// tree's commits, and the line's own commit it brought in is none of them.
    /// </summary>
    [Fact]
    public async Task A_branch_that_merged_its_line_records_only_its_own_commits()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await CommitAsync(root, "line.txt", "the line's own\n", "the line moves on");
        await GitAsync(tree.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "merge", "--no-ff", "-m", "merge the line in", "main");
        await CommitAsync(tree.Path, "after.txt", "after\n", "after the merge");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", tree.Branch);

        await SetAsideAsync(trees, root);
        await RecordAsync(trees, root, DateTimeOffset.UtcNow);

        Assert.Equal(["the work", "merge the line in", "after the merge"], Assert.Single(_posts).Commits!.Select(commit => commit.Subject));
    }

    /// <summary>
    /// Codex's read of the branch, finding 10: a commit's files are named as git names them, read NUL-delimited: a name with spaces, an
    /// apostrophe and characters past ASCII, which git quotes and escapes for display, survives exactly.
    /// </summary>
    [Fact]
    public async Task A_commits_file_names_survive_exactly()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "default");
        // Windows refuses a quote in a name; spaces, an apostrophe and characters past ASCII are what git would quote for display.
        const string Named = "笔记 with 'spaces' & more.txt";
        await CommitAsync(tree.Path, Named, "words\n", "a named file");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", tree.Branch);

        await SetAsideAsync(trees, root);
        await RecordAsync(trees, root, DateTimeOffset.UtcNow);

        Assert.Equal([(Named, 1, 0)], Assert.Single(Assert.Single(_posts).Commits!).Files);
    }

    /// <summary>
    /// 🔴 Codex finding 2 of round 2: the finished folder's threshold is the judgement's <c>read</c>, not the moment in its name: a file written
    /// after the read and dated before the move keeps the folder, said <c>written</c>.
    /// </summary>
    [Fact]
    public async Task A_write_after_the_judgements_read_and_before_the_move_keeps_the_folder()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var read = Manifest(aside).Finished!.Read;
        var moved = TreeSetAside.MovedAt(Path.GetFileName(aside))!.Value;
        Assert.True(read < moved);
        var late = Path.Combine(aside, "late.txt");
        await File.WriteAllTextAsync(late, "written between the read and the move\n");
        var between = read.UtcDateTime + (moved - read) / 2;
        File.SetCreationTimeUtc(late, between);
        File.SetLastWriteTimeUtc(late, between);
        Directory.SetLastWriteTimeUtc(aside, between);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);

        var kept = Assert.Single(await PurgeAsync(trees, now.AddMinutes(61), root));

        Assert.Equal((false, SetAsideKept.Written), (kept.Purged, kept.Code));
        Assert.True(Directory.Exists(aside));
    }

    /// <summary>
    /// 🔴 Opus finding 7: a tree with no package folder gets its manifest all the same; under <c>whole</c> its record is kept and its folder
    /// waits its days.
    /// </summary>
    [Fact]
    public async Task A_tree_with_no_package_folder_gets_its_manifest_and_whole_keeps_it_its_days()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        Assert.Empty(Manifest(aside).Paths);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);
        var whole = KeepPolicy.Default with { SetAside = KeepPolicy.Default.SetAside with { Whole = true } };

        Assert.Empty(await PurgeAsync(trees, now.AddHours(2), root, whole));
        Assert.True(Directory.Exists(aside));
        Assert.Single(_posts);
        var gone = Assert.Single(await PurgeAsync(trees, now.AddDays(15), root, whole));
        Assert.Equal((true, false), (gone.Purged, gone.Hour));
    }

    /// <summary>
    /// 🔴 Codex's read of the branch, finding 5: the hour starts at the moment read after the service's answer. A post answered ten minutes
    /// after the pass began leaves the folder a whole hour from then.
    /// </summary>
    [Fact]
    public async Task The_hour_starts_after_the_records_acknowledgement()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var began = DateTimeOffset.UtcNow;
        var clock = began;

        await trees.RecordSetAsideAsync(Roots(root), (post, ct) =>
        {
            clock = began.AddMinutes(10);
            return PostAsync(post, ct);
        }, () => clock, CancellationToken.None);

        Assert.True(Math.Abs((Manifest(aside).Recorded!.Value - began.AddMinutes(10)).TotalMilliseconds) < 1);
        Assert.Empty(await PurgeAsync(trees, began.AddMinutes(61), root));
        Assert.True(Assert.Single(await PurgeAsync(trees, began.AddMinutes(71), root)).Purged);
    }

    /// <summary>
    /// 🔴 Codex's read of the branch, finding 3: a service with no record door (a 404) never abandons the awaited record nor lets the days take
    /// the folder: it stays <c>deleted</c>, said, past its days; once the service has the door, the next pass keeps it.
    /// </summary>
    [Fact]
    public async Task A_404_never_abandons_an_awaited_record()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        _ledger.RecordsMissing = true;
        var now = DateTimeOffset.UtcNow;

        await trees.RecordSetAsideAsync(Roots(root), _ledger.Client().PostWorkRecordAsync, () => now, CancellationToken.None);

        Assert.Equal(SlimStates.Deleted, Manifest(aside).State);
        Assert.Contains("no door", Manifest(aside).Why, StringComparison.Ordinal);
        Assert.Empty(await PurgeAsync(trees, now.AddDays(20), root));

        _ledger.RecordsMissing = false;
        await trees.RecordSetAsideAsync(Roots(root), _ledger.Client().PostWorkRecordAsync, () => now.AddDays(20), CancellationToken.None);

        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
        Assert.Single(_ledger.Records);
    }

    /// <summary>
    /// 🔴 The keep-size design §2.2, through the driver's own client: a service whose records a newer build stamped answers 409
    /// <c>RECORDS_NEWER</c>, and the folder stays <c>deleted</c>, awaiting its record past its days, said.
    /// </summary>
    [Fact]
    public async Task A_store_stamped_newer_keeps_the_folder_awaiting()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        _ledger.RecordsNewer = true;
        var now = DateTimeOffset.UtcNow;

        await trees.RecordSetAsideAsync(Roots(root), _ledger.Client().PostWorkRecordAsync, () => now, CancellationToken.None);

        Assert.Equal(SlimStates.Deleted, Manifest(aside).State);
        Assert.Contains("newer Daoris", Manifest(aside).Why, StringComparison.Ordinal);
        Assert.Empty(await PurgeAsync(trees, now.AddDays(20), root));
    }

    /// <summary>🔴 A ref dropped by hand makes a recorded folder unmarked at its hour: it waits its days, and says why.</summary>
    [Fact]
    public async Task A_ref_dropped_by_hand_makes_a_recorded_folder_unmarked()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await SquashOntoLineAsync(root, tree.Branch);
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);
        await GitAsync(root, "update-ref", "-d", Manifest(aside).Finished!.Ref!);

        await RecordAsync(trees, root, now.AddMinutes(61));

        Assert.Equal(SlimStates.Unmarked, Manifest(aside).State);
        Assert.Contains("is gone, so nothing holds its tip", Manifest(aside).Why, StringComparison.Ordinal);
        Assert.Empty(await PurgeAsync(trees, now.AddMinutes(62), root));
        Assert.True(Directory.Exists(aside));
    }

    /// <summary>
    /// 🔴 Codex's read of the design, finding 3: a repository the registry cannot resolve is unread, never certain: the recorded folder keeps its
    /// class, past its days too, and goes once its checkout is named again and its check passes.
    /// </summary>
    [Fact]
    public async Task A_repository_the_registry_cannot_resolve_keeps_the_folder_recorded()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);

        await RecordAsync(trees, null, now.AddMinutes(61));
        var kept = Assert.Single(await PurgeAsync(trees, now.AddDays(20), Path.Combine(_scratch, "nowhere")));

        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
        Assert.Equal((false, SetAsideKept.Unheld), (kept.Purged, kept.Code));
        Assert.True(Assert.Single(await PurgeAsync(trees, now.AddDays(20), root)).Purged);
    }

    /// <summary>
    /// 🔴 Codex's read of the design, finding 3: a manifest that is there and does not read is never a known legacy folder: past its days it is
    /// kept, and once it reads again the folder reaches its record and goes.
    /// </summary>
    [Fact]
    public async Task A_manifest_that_does_not_read_keeps_the_folder_until_it_reads()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var path = SlimManifest.PathFor(aside);
        var whole = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, "{ not a manifest");
        var now = DateTimeOffset.UtcNow;

        await RecordAsync(trees, root, now);
        Assert.Empty(await PurgeAsync(trees, now.AddDays(20), root));
        Assert.Empty(_posts);
        Assert.Equal(SetAsideClass.Unread, Assert.Single(trees.ListSetAside(KeepPolicy.Default, now)).Class);

        await File.WriteAllTextAsync(path, whole);
        await RecordAsync(trees, root, now);
        await RecordAsync(trees, root, now.AddMinutes(61));
        Assert.True(Assert.Single(await PurgeAsync(trees, now.AddMinutes(61), root)).Purged);
    }

    /// <summary>
    /// 🔴 The second maker's read, finding 2: a junction at <c>.going</c>'s workspace level. The purge's resume of <c>.going</c> deletes nothing
    /// through it, and the hour's removal keeps the folder, <c>linked</c>, rather than moving it into the link.
    /// </summary>
    [Fact]
    public async Task A_link_under_going_is_never_followed()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var outside = Path.Combine(_scratch, "outside");
        var precious = Path.Combine(outside, "engine", "s-x-20261001T090000000Z", "precious.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(precious)!);
        await File.WriteAllTextAsync(precious, "not Daoris's\n");
        await JunctionAsync(Path.Combine(Going(trees), "default"), outside);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);

        var kept = Assert.Single(await PurgeAsync(trees, now.AddMinutes(61), root), each => each.Folder == aside);

        Assert.Equal((false, SetAsideKept.Linked), (kept.Purged, kept.Code));
        Assert.True(File.Exists(precious));
        Assert.True(Directory.Exists(aside));
        Assert.False(Directory.Exists(Path.Combine(outside, "engine", Path.GetFileName(aside))));
    }

    /// <summary>
    /// 🔴 Codex's read of the design, finding 3: a registry that did not read runs no step, so a folder awaiting its record is not posted without
    /// its commits, and stays awaiting for the next pass.
    /// </summary>
    [Fact]
    public async Task A_registry_that_did_not_read_runs_no_step()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);

        await trees.RecordSetAsideAsync(null, PostAsync, () => DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Empty(_posts);
        Assert.Equal(SlimStates.Deleted, Manifest(aside).State);
        await RecordAsync(trees, root, DateTimeOffset.UtcNow);
        Assert.NotNull(Assert.Single(_posts).Commits);
    }

    /// <summary>
    /// 🔴 The last fixes' check, finding 2: a registry that reads and names no checkout of the repository is a git read that failed, never one
    /// with no commits: nothing is posted, the folder stays awaiting, said, past its days; once the registry names it, its record is posted with
    /// its commits.
    /// </summary>
    [Fact]
    public async Task A_repository_the_registry_does_not_name_posts_nothing_until_it_does()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;

        await RecordAsync(trees, null, now);

        Assert.Empty(_posts);
        Assert.Equal((SlimStates.Deleted, SetAsideEligibility.NoCheckout), (Manifest(aside).State, Manifest(aside).Why));
        Assert.Empty(await PurgeAsync(trees, now.AddDays(20), root));
        Assert.True(Directory.Exists(aside));

        await RecordAsync(trees, root, now.AddDays(20));

        var post = Assert.Single(_posts);
        Assert.Null(post.Unread);
        Assert.NotEmpty(Assert.IsAssignableFrom<IReadOnlyList<FinishedCommit>>(post.Commits));
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
    }

    /// <summary>
    /// 🔴 Codex's read of the design, finding 6: the landing that carried the tree's work is captured before the move, so the record names it
    /// after the landings' trace is gone: its branch and when it landed, never the move's moment.
    /// </summary>
    [Fact]
    public async Task The_landing_is_recorded_after_its_trace_is_gone()
    {
        var root = await RepositoryAsync("engine");
        var trees = LandingTrees();
        var (tree, landing) = await LandingAsync(trees, root, new LandingSubject("s1", "q1", "The work"));
        var landedAt = Assert.Single(trees.Recorded.Entries(), each => each.Branch == landing).LandedAt;
        await GitAsync(root, "merge", "--no-ff", "--no-edit", landing);

        var aside = await SetAsideAsync(trees, root);
        File.Delete(DaorisHome.Entry(_home, HomeEntry.Landings));
        await RecordAsync(trees, root, DateTimeOffset.UtcNow);

        var landed = Assert.Single(_posts).Tree.Landing;
        Assert.NotNull(landed);
        // The manifest keeps its moments to the millisecond.
        Assert.Equal(landing, landed.Branch);
        Assert.True(Math.Abs((landed.At - landedAt).TotalMilliseconds) < 1);
        Assert.NotEqual(TreeSetAside.MovedAt(Path.GetFileName(aside)), landed.At);
        Assert.NotEqual(tree.Branch, landed.Branch);
    }

    /// <summary>
    /// The last fixes' check, finding 6: a session held by its landing's branch, which a squash took onto the line, is the landing hold, and its
    /// record names that landing, found by the landing's own branch rather than the form of the line the hold names.
    /// </summary>
    [Fact]
    public async Task A_landing_holds_record_names_its_landing_by_its_own_branch()
    {
        var root = await RepositoryAsync("engine");
        var trees = LandingTrees();
        var (_, landing) = await LandingAsync(trees, root, new LandingSubject("s1", "q1", "The work"));
        await SquashAsync(root, landing);

        var aside = await SetAsideAsync(trees, root);
        await RecordAsync(trees, root, DateTimeOffset.UtcNow);

        Assert.Equal(("landing", "main"), (Manifest(aside).Finished!.Hold.Kind, Manifest(aside).Finished!.Hold.Where));
        Assert.Equal(landing, Assert.Single(_posts).Tree.Landing?.Branch);
    }

    /// <summary>
    /// 🔴 Codex's read of the branch, finding 4: the press removes a set-aside folder only while its class and holder read as the list gave
    /// them. Listed as recorded on its ref, then unmarked by a check that found the ref gone, it is kept <c>changed</c>; and the press's
    /// request carries both, read back by the run.
    /// </summary>
    [Fact]
    public async Task The_press_keeps_a_folder_whose_class_changed_since_its_list()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await SquashOntoLineAsync(root, tree.Branch);
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);
        var listed = Assert.Single(trees.ListSetAside(KeepPolicy.Default, now));
        var reference = listed.Ref!;
        Assert.Equal(SetAsideClass.Recorded, ListedClass.Of(listed));
        using var payload = JsonDocument.Parse($$"""{ "early": [ { "kind": "set-aside", "id": "{{listed.Id}}", "class": "recorded", "ref": "{{reference}}" } ] }""");
        var asked = Assert.Single(SpaceFree.Asked(payload.RootElement));
        Assert.Equal((SetAsideClass.Recorded, reference), (asked.Class, asked.Ref));

        await GitAsync(root, "update-ref", "-d", reference);
        await RecordAsync(trees, root, now.AddMinutes(61));
        var (bytes, kept) = trees.RemoveSetAsideEarly(aside, listedOwn: false, asked.Class, asked.Ref);

        Assert.Equal((0L, SpaceKept.Changed), (bytes, kept));
        Assert.True(Directory.Exists(aside));
    }

    /// <summary>
    /// 🔴 Opus finding 8: a stray and a pass's leftover, no manifest, are never purged at the hour, only at their days, a checkout given or not.
    /// </summary>
    [Fact]
    public async Task A_stray_and_a_leftover_are_never_purged_at_the_hour()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var moved = DateTimeOffset.UtcNow.AddHours(-2);
        var stray = Path.Combine(trees.SetAsideRoot, "default", "engine",
            $"devtools-{moved.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture)}");
        Directory.CreateDirectory(stray);
        var file = Path.Combine(stray, "a.txt");
        await File.WriteAllTextAsync(file, "a stray\n");
        File.SetCreationTimeUtc(file, moved.UtcDateTime.AddMinutes(-1));
        File.SetLastWriteTimeUtc(file, moved.UtcDateTime.AddMinutes(-1));
        // A pass's leftover goes aside by the same move a stray does (`MoveFolderAside`), with no judgement and no manifest: the same folder.
        var to = Path.Combine(trees.SetAsideRoot, "default", "engine",
            $"leftover-{moved.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture)}");
        Directory.CreateDirectory(to);
        var left = Path.Combine(to, "b.txt");
        await File.WriteAllTextAsync(left, "a pass's leftover\n");
        File.SetCreationTimeUtc(left, moved.UtcDateTime.AddMinutes(-1));
        File.SetLastWriteTimeUtc(left, moved.UtcDateTime.AddMinutes(-1));

        Assert.Empty(await PurgeAsync(trees, DateTimeOffset.UtcNow.AddHours(2), root));
        var gone = await PurgeAsync(trees, DateTimeOffset.UtcNow.AddDays(15), root);
        Assert.Equal([SetAsideClass.Unmarked, SetAsideClass.Unmarked], gone.Select(each => each.Class));
        Assert.All(gone, each => Assert.True(each.Purged));
        Assert.False(Directory.Exists(to));
    }

    /// <summary>A squash merge of <paramref name="branch"/> onto the line through a branch of its own, as a squashing landing leaves the line.</summary>
    private static async Task SquashOntoLineAsync(string root, string branch)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/squashed");
        await SquashAsync(root, branch);
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", "feature/squashed");
    }
}
