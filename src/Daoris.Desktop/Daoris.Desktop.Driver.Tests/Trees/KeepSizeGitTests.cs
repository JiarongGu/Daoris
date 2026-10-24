using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEPSIZE1b over real git (D195's keep-size note; the keep-size design §2 and §3): a finished tree's record kept, and its tree let go an
/// hour after. The look holds the exact tip, writes a schema-2 manifest for every finished tree and marks its steps; the record pass after
/// the hold composes the record from the manifest by <c>git log --first-parent</c> and posts it; the one purge takes a recorded folder an
/// hour after its record, renamed into <c>.going</c> first, and every other class by its days or never. The service's half (the tables, the
/// post's identity, the delete and the clear) is <c>WorkRecordStoreTests</c>'; the service not answering is <c>KeepingSlimTests</c>'.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class KeepSizeGitTests : BranchTidyingFixture
{
    private readonly List<FinishedPost> _posts = [];
    private string? _refused;

    private static Task<IReadOnlySet<string>> NoneRunning(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary>The service's door as the record pass is handed it: kept, or refused with <see cref="_refused"/>.</summary>
    private Task<RecordRefusal?> PostAsync(FinishedPost post, CancellationToken ct)
    {
        if (_refused is { } why) return Task.FromResult<RecordRefusal?>(new(why));
        _posts.Add(post);
        return Task.FromResult<RecordRefusal?>(null);
    }

    private static Func<string, string, string?>? Roots(string? root) => (_, repository) => repository == "engine" ? root : null;

    /// <summary>The look's own tidy of <c>engine</c>: the one branch it removed, its folder moved aside.</summary>
    private static async Task<string> SetAsideAsync(SessionTrees trees, string root)
    {
        var pass = await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);
        var removed = Assert.Single(pass.Results, result => result.Removed);
        return Assert.IsType<string>(removed.MovedTo);
    }

    private static SlimManifest Manifest(string folder) => Assert.IsType<SlimManifest>(SlimManifest.Read(SlimManifest.PathFor(folder)).Manifest);

    private async Task RecordAsync(SessionTrees trees, string? root, DateTimeOffset now) =>
        await trees.RecordSetAsideAsync(Roots(root), PostAsync, now, CancellationToken.None);

    private static SetAsideRun Purge(SessionTrees trees, DateTimeOffset now, KeepPolicy? policy = null) =>
        trees.KeepSetAside(policy ?? KeepPolicy.Default, now, TimeSpan.FromMinutes(5), CancellationToken.None);

    private static string Going(SessionTrees trees) => Path.Combine(trees.SetAsideRoot, SetAsideEligibility.Going);

    /// <summary>
    /// 🔴 A landed tree's record kept with its commits, its tip and its named scratch (§2.1), its manifest schema 2 at each step; its folder
    /// kept within the hour after its record and gone after it, by the hour's rule, through <c>.going</c>, which holds nothing after.
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
        // The manifest keeps its moments to the millisecond.
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
        Assert.True(Math.Abs((Manifest(aside).Recorded!.Value - now).TotalMilliseconds) < 1);
        Assert.Empty(Purge(trees, now.AddMinutes(30)).Purged);
        Assert.True(Directory.Exists(aside));

        // The hour's check passes and stamps the manifest, the purge's ground (the second maker's read, finding 1).
        await RecordAsync(trees, root, now.AddMinutes(61));
        Assert.NotNull(Manifest(aside).Held);
        var gone = Assert.Single(Purge(trees, now.AddMinutes(61)).Purged);

        Assert.Equal((true, SetAsideClass.Recorded, true), (gone.Purged, gone.Class, gone.Hour));
        Assert.False(Directory.Exists(aside));
        Assert.False(File.Exists(SlimManifest.PathFor(aside)));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(Going(trees), "*", SearchOption.AllDirectories), File.Exists);
    }

    /// <summary>
    /// 🔴 Codex finding 1: a multi-commit session squashed at its landing. The tip's own ref is written before the branch goes, so the
    /// session's commits and messages read from it after both the session branch and the squash's landing branch are removed.
    /// </summary>
    [Fact]
    public async Task A_squashed_sessions_own_commits_read_from_its_tips_ref_after_both_branches_go()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await CommitAsync(tree.Path, "second.txt", "more\n", "the second commit");
        var tip = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/squashed");
        await SquashAsync(root, tree.Branch);
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", "feature/squashed");

        // Held by content on the line, never by its own ancestry: the look writes the tip's ref.
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
    /// 🔴 Codex finding 2: the finished folder's threshold is the judgement's <c>read</c>, not the moment in its name: a file written after the
    /// read and dated before the move keeps the folder, said <c>written</c>.
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
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);

        await RecordAsync(trees, root, now.AddMinutes(61));
        var kept = Assert.Single(Purge(trees, now.AddMinutes(61)).Purged);

        Assert.Equal((false, SetAsideKept.Written), (kept.Purged, kept.Code));
        Assert.True(Directory.Exists(aside));
    }

    /// <summary>
    /// 🔴 Opus finding 7: a tree with no package folder gets its manifest all the same, and goes at its hour; under <c>whole</c> its record is
    /// kept and its folder waits its days.
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

        Assert.Empty(Purge(trees, now.AddHours(2), whole).Purged);
        Assert.True(Directory.Exists(aside));
        Assert.Single(_posts);
        var gone = Assert.Single(Purge(trees, now.AddDays(15), whole).Purged);
        Assert.Equal((true, false), (gone.Purged, gone.Hour));
    }

    /// <summary>
    /// 🔴 Round 3's finding 6: the hour is measured from <c>recorded</c>: a record kept a day late leaves its folder an hour, and a folder
    /// awaiting its record is never purged, past its days too.
    /// </summary>
    [Fact]
    public async Task The_hour_is_measured_from_the_record_and_an_awaiting_folder_waits_past_its_days()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;
        _refused = "the service is not answering";
        await RecordAsync(trees, root, now);
        Assert.Equal((SlimStates.Deleted, "the service is not answering"), (Manifest(aside).State, Manifest(aside).Why));
        Assert.Empty(Purge(trees, now.AddDays(20)).Purged);

        _refused = null;
        var late = now.AddDays(1);
        await RecordAsync(trees, root, late);

        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
        Assert.Empty(Purge(trees, late.AddMinutes(30)).Purged);
        await RecordAsync(trees, root, late.AddMinutes(61));
        Assert.True(Assert.Single(Purge(trees, late.AddMinutes(61)).Purged).Purged);
    }

    /// <summary>
    /// 🔴 Round 3's finding 1: a stop at <c>judged</c> with its folder present keeps its manifest through the slim, and reaches its record once
    /// its branch is gone; a <c>refused</c> one whose branch stands is never armed, and the press's delete of the branch with no tree marks it
    /// <c>deleted</c>, so the next pass records it.
    /// </summary>
    [Fact]
    public async Task A_stop_at_judged_and_a_refused_delete_reach_their_record_once_the_branch_goes()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await LandedTreeAsync(trees, root);
        var tip = (await GitAsync(root, "rev-parse", tree.Branch)).Trim();
        var aside = await SetAsideAsync(trees, root);
        var path = SlimManifest.PathFor(aside);
        (Manifest(aside) with { State = SlimStates.Judged }).Write(path);
        var now = DateTimeOffset.UtcNow;

        Purge(trees, now.AddHours(2));
        Assert.Equal(SlimStates.Judged, Manifest(aside).State);
        await RecordAsync(trees, root, now);
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);

        // Refused: the branch stands again at its tip, so the resume waits, saying why, and no rule arms it.
        (Manifest(aside) with { State = SlimStates.Refused, Recorded = null }).Write(path);
        await GitAsync(root, "branch", tree.Branch, tip);
        await RecordAsync(trees, root, now);
        Assert.Equal((SlimStates.Refused, "its branch stands"), (Manifest(aside).State, Manifest(aside).Why));
        Assert.Empty(Purge(trees, now.AddDays(1)).Purged);

        await trees.CleanAsync([("engine", "default", root)], new HashSet<string>());
        Assert.Equal("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Equal(SlimStates.Deleted, Manifest(aside).State);
        await RecordAsync(trees, root, now);
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
        Assert.Equal(2, _posts.Count);
    }

    /// <summary>🔴 A ref dropped by hand makes a recorded folder unmarked at its hour: it waits its days, and says why.</summary>
    [Fact]
    public async Task A_ref_dropped_by_hand_makes_a_recorded_folder_unmarked()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/squashed");
        await SquashAsync(root, tree.Branch);
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", "feature/squashed");
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);
        await GitAsync(root, "update-ref", "-d", Manifest(aside).Finished!.Ref!);

        await RecordAsync(trees, root, now.AddMinutes(61));

        Assert.Equal(SlimStates.Unmarked, Manifest(aside).State);
        Assert.Contains("is gone, so nothing holds its tip", Manifest(aside).Why, StringComparison.Ordinal);
        Assert.Empty(Purge(trees, now.AddMinutes(62)).Purged);
        Assert.True(Directory.Exists(aside));
    }

    /// <summary>🔴 Round 3's finding 7: a repository the registry cannot resolve makes a recorded folder unmarked; it waits its days.</summary>
    [Fact]
    public async Task A_repository_the_registry_cannot_resolve_makes_the_folder_unmarked()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);

        await RecordAsync(trees, null, now.AddMinutes(61));

        Assert.Equal(SlimStates.Unmarked, Manifest(aside).State);
        Assert.Empty(Purge(trees, now.AddMinutes(62)).Purged);
    }

    /// <summary>
    /// 🔴 Round 3's finding 7 and Opus finding 19: a folder left in <c>.going</c> by a stop is passed over by the press's list, the slim and
    /// the trees' list, and deleted by the next pass.
    /// </summary>
    [Fact]
    public async Task A_folder_left_in_going_is_passed_over_and_deleted_by_the_next_pass()
    {
        var trees = new SessionTrees(_home);
        var left = Path.Combine(Going(trees), "default", "engine", "s-left-20261001T090000000Z");
        Directory.CreateDirectory(left);
        await File.WriteAllTextAsync(Path.Combine(left, "half.txt"), "half deleted\n");

        Assert.Empty(trees.ListSetAside(KeepPolicy.Default, DateTimeOffset.UtcNow));
        Assert.DoesNotContain(await trees.ListAsync(), each => each.Path.Contains(SetAsideEligibility.Going, StringComparison.Ordinal));
        Purge(trees, DateTimeOffset.UtcNow);

        Assert.False(Directory.Exists(left));
    }

    /// <summary>🔴 Opus finding 8: a stray and a pass's leftover, no manifest, are never purged at the hour, only at their days.</summary>
    [Fact]
    public void A_stray_is_never_purged_at_the_hour()
    {
        var trees = new SessionTrees(_home);
        var moved = DateTimeOffset.UtcNow.AddHours(-2);
        var stray = Path.Combine(trees.SetAsideRoot, "default", "engine",
            $"devtools-{moved.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture)}");
        Directory.CreateDirectory(stray);
        var file = Path.Combine(stray, "a.txt");
        File.WriteAllText(file, "a stray\n");
        File.SetCreationTimeUtc(file, moved.UtcDateTime.AddMinutes(-1));
        File.SetLastWriteTimeUtc(file, moved.UtcDateTime.AddMinutes(-1));

        Assert.Empty(Purge(trees, DateTimeOffset.UtcNow).Purged);
        Assert.Equal(SetAsideClass.Unmarked, Assert.Single(Purge(trees, DateTimeOffset.UtcNow.AddDays(15)).Purged).Class);
    }

    /// <summary>🔴 Codex finding 6: a manifest that cannot be written keeps the tree and the branch, and the tip's ref with them goes.</summary>
    [Fact]
    public async Task A_manifest_not_written_keeps_the_tree_and_the_branch()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await LandedTreeAsync(trees, root);
        // Where the manifest goes is a file, so neither it nor the folder beside it can be made.
        Directory.CreateDirectory(Path.Combine(trees.SetAsideRoot, "default"));
        await File.WriteAllTextAsync(Path.Combine(trees.SetAsideRoot, "default", "engine"), "in the way\n");

        var pass = await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);

        Assert.DoesNotContain(pass.Results, result => result.Removed);
        Assert.True(Directory.Exists(tree.Path));
        Assert.NotEqual("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
    }

    /// <summary>🔴 Opus claim 6: a line held only by <c>origin</c> is no hold that stays, so the look writes the tip's ref.</summary>
    [Fact]
    public async Task A_line_held_only_by_origin_gets_the_tips_ref()
    {
        var root = await RepositoryAsync("engine");
        var origin = Path.Combine(_scratch, "origin.git");
        await GitAsync(_scratch, "clone", "--quiet", "--bare", root, origin);
        await GitAsync(root, "remote", "add", "origin", origin);
        await GitAsync(root, "fetch", "--quiet", "origin");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "push", "--quiet", "origin", $"{tree.Branch}:main");
        await GitAsync(root, "fetch", "--quiet", "origin");

        var aside = await SetAsideAsync(trees, root);

        Assert.Equal($"refs/daoris/discarded/{tree.Branch}", Manifest(aside).Finished!.Ref);
    }

    /// <summary>🔴 Opus finding 19: a file held open in a recorded folder keeps it whole, <c>busy</c>: it is renamed before anything is deleted.</summary>
    [Fact]
    public async Task A_file_held_open_keeps_the_folder_whole()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);
        var files = Directory.GetFiles(aside, "*", SearchOption.AllDirectories).Length;

        await RecordAsync(trees, root, now.AddMinutes(61));
        SetAside kept;
        using (File.Open(Path.Combine(aside, "README.md"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            kept = Assert.Single(Purge(trees, now.AddMinutes(61)).Purged);
        }

        Assert.Equal((false, TidyKept.Busy), (kept.Purged, kept.Code));
        Assert.Equal(files, Directory.GetFiles(aside, "*", SearchOption.AllDirectories).Length);
    }

    /// <summary>
    /// 🔴 The second maker's read, finding 1: the hour's purge takes a recorded folder only on a holding check that passed at or after its
    /// hour, within the last hour. Purged with no record pass since its hour, or after a pass whose registry did not read, or a day after the
    /// last check passed, it is kept; a pass whose check passes lets the purge take it.
    /// </summary>
    [Fact]
    public async Task A_recorded_folder_no_check_has_passed_since_its_hour_is_kept()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);
        // A check before the hour stamps nothing the hour reads.
        await RecordAsync(trees, root, now.AddMinutes(30));

        Assert.Empty(Purge(trees, now.AddMinutes(61)).Purged);
        await trees.RecordSetAsideAsync(null, PostAsync, now.AddMinutes(61), CancellationToken.None);
        Assert.Null(Manifest(aside).Held);
        Assert.Empty(Purge(trees, now.AddMinutes(62)).Purged);
        Assert.True(Directory.Exists(aside));

        await RecordAsync(trees, root, now.AddMinutes(63));
        Assert.Empty(Purge(trees, now.AddDays(1)).Purged);
        Assert.True(Assert.Single(Purge(trees, now.AddMinutes(64)).Purged).Purged);
    }

    /// <summary>
    /// 🔴 The second maker's read, finding 2: a junction at <c>.going</c>'s workspace level. The pass's clearing of <c>.going</c> deletes nothing
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
        await RecordAsync(trees, root, now.AddMinutes(61));

        var kept = Assert.Single(Purge(trees, now.AddMinutes(61)).Purged);

        Assert.Equal((false, SetAsideKept.Linked), (kept.Purged, kept.Code));
        Assert.True(File.Exists(precious));
        Assert.True(Directory.Exists(aside));
        Assert.False(Directory.Exists(Path.Combine(outside, "engine", Path.GetFileName(aside))));
    }

    /// <summary>
    /// 🔴 The second maker's read, finding 4: a service with no door for the record (an older one, a bare 404) leaves the folder unmarked, so
    /// it keeps its days rather than awaiting a record no pass can post. Through the driver's own client over the stand-in's door.
    /// </summary>
    [Fact]
    public async Task A_service_with_no_record_door_leaves_the_folder_its_days()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        _ledger.RecordsMissing = true;
        var now = DateTimeOffset.UtcNow;

        await trees.RecordSetAsideAsync(Roots(root), _ledger.Client().PostWorkRecordAsync, now, CancellationToken.None);

        Assert.Equal(SlimStates.Unmarked, Manifest(aside).State);
        Assert.Contains("no door", Manifest(aside).Why, StringComparison.Ordinal);
        Assert.Empty(Purge(trees, now.AddDays(1)).Purged);
        Assert.Equal(SetAsideClass.Unmarked, Assert.Single(Purge(trees, now.AddDays(15)).Purged).Class);
    }

    /// <summary>
    /// The second maker's read, finding 3: a registry that did not read runs no step, so a folder awaiting its record is not posted without its
    /// commits, and stays awaiting for the next pass.
    /// </summary>
    [Fact]
    public async Task A_registry_that_did_not_read_runs_no_step()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);

        await trees.RecordSetAsideAsync(null, PostAsync, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Empty(_posts);
        Assert.Equal(SlimStates.Deleted, Manifest(aside).State);
        await RecordAsync(trees, root, DateTimeOffset.UtcNow);
        Assert.NotNull(Assert.Single(_posts).Commits);
    }
}
