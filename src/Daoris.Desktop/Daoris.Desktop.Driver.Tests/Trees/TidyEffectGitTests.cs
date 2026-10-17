using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END4a over real git (the records-only half is <see cref="TidyEffectTests"/>): the look's tidy takes its candidates from the
/// kept facts, so nothing goes before <i>held</i> (canary 15), the removal judges again and a contradicted end keeps its branch
/// (canary 8), and the branches the line holds none of are no longer judged at each pace: the git starts counted down.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TidyEffectGitTests(ITestOutputHelper output) : LandedFixture
{
    private static IReadOnlySet<string> NoneInUse { get; } = new HashSet<string>();

    private static Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult(NoneInUse);

    private SessionBranches Grown => new(Home);

    private static Task<TidyPass> TidyAsync(SessionTrees trees, string root) =>
        trees.TidyEmptyAsync(root, "engine", "aurora", InUseAsync, CancellationToken.None);

    /// <summary>A session's tree with one commit, merged onto the line where asked.</summary>
    private static async Task<string> WorkAsync(SessionTrees trees, string root, string file, bool merged, string repository = "engine")
    {
        var opened = await trees.OpenAsync(root, repository, "aurora");
        await CommitAsync(opened.Path, file, $"{file}\n", $"work on {file}");
        if (merged) await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", $"merge {opened.Branch}", opened.Branch);
        return opened.Branch;
    }

    private static async Task<bool> StandsAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    /// <summary>🔴 Canary 15: a branch the press's judgement would take goes only once the keeper has written that the line holds it.</summary>
    [Fact]
    public async Task Nothing_goes_before_the_line_is_known_to_hold_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var branch = await WorkAsync(trees, root, "a.txt", merged: true);

        // No lease, so no keeper writes: no fact, no candidate, though the work is on the line.
        var before = await TidyAsync(trees, root);
        Assert.DoesNotContain(before.Results, result => result.Removed);
        Assert.True(await StandsAsync(root, branch));
        Assert.Null(Grown.Of("engine", branch)!.Held);

        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        var after = await TidyAsync(trees, root);

        Assert.Contains(after.Results, result => result.Removed && result.Item.Branch == branch);
        Assert.False(await StandsAsync(root, branch));
        Assert.NotNull(Grown.Entries().Single(each => each.Branch == branch).GoneAt);
    }

    /// <summary>🔴 Canary 8: an end kept on the record but contradicted before the act (the line rewound under it) keeps the branch.</summary>
    [Fact]
    public async Task An_end_contradicted_before_the_removal_keeps_the_branch()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var branch = await WorkAsync(trees, root, "a.txt", merged: true);
        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            Assert.Equal(1, await trees.Ends.KeepAsync(root, "engine", "aurora", "main", CancellationToken.None));
        }

        // The line rewound past the merge, with no keeper to clear the fact: the kept fact still names the branch's tip.
        await GitAsync(root, "reset", "--hard", "--quiet", "HEAD~1");
        Assert.NotNull(Grown.Of("engine", branch)!.Held);

        var pass = await TidyAsync(trees, root);

        Assert.DoesNotContain(pass.Results, result => result.Removed);
        Assert.True(await StandsAsync(root, branch));
    }

    /// <summary>
    /// 🔴 The review's P1: the line rewound after the last proof and before its ref goes refuses the delete inside git's own transaction,
    /// so the branch never leaves (the second review: an absence another look could observe); the branch checked out meanwhile, which
    /// no ref says, is put back at the commit judged, kept.
    /// </summary>
    [Theory]
    [InlineData("rewound", "moved as it went")]
    [InlineData("checked-out", "put back where it was")]
    public async Task What_the_proof_rested_on_moving_as_the_branch_goes_keeps_it(string how, string said)
    {
        var root = await RepositoryAsync("engine");
        var branch = await WorkAsync(new SessionTrees(Home), root, "a.txt", merged: true);
        var tip = (await GitAsync(root, "rev-parse", branch)).Trim();
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        var late = Path.Combine(Path.GetDirectoryName(root)!, $"late-{Guid.NewGuid():N}"[..13]);
        var trees = new SessionTrees(Home)
        {
            BeforeRefDeleted = async _ =>
            {
                if (how == "rewound") await GitAsync(root, "reset", "--hard", "--quiet", "HEAD~1");
                else await GitAsync(root, "worktree", "add", "--quiet", late, branch);
            },
        };

        var pass = await TidyAsync(trees, root);

        Assert.DoesNotContain(pass.Results, result => result.Removed);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", branch)).Trim());
        Assert.Contains(pass.Results, result => result.Message.Contains(said, StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 The fourth review's P1 (AutoTidies.cs:262, TreeSweeps.cs:402): a session branch whose work the line holds through a landing's
    /// branch rests on what that landing's proof rested on, the line at the commit read, as well as on the landing's branch. The line rewound
    /// after the landing was proved keeps the session branch: its work is no longer on the line.
    /// </summary>
    [Fact]
    public async Task A_line_rewound_after_a_landing_was_proved_keeps_the_session_branch_it_holds()
    {
        var root = await RepositoryAsync("engine");
        var opened = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "a.txt", "a\n", "work on a");
        var landed = await new SessionTrees(Home).LandAsync(opened.Path, new LandingSubject("s1", "q1", "First"), inUse: InUseAsync);
        Assert.True(landed.Landed, landed.Message);
        await GitAsync(root, "worktree", "remove", opened.Path);
        await SquashAsync(root, landed.Branch!);
        var tip = (await GitAsync(root, "rev-parse", opened.Branch)).Trim();
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        var rewound = false;
        var trees = new SessionTrees(Home)
        {
            BeforeRefDeleted = async name =>
            {
                if (rewound || name != opened.Branch) return;
                rewound = true;
                await GitAsync(root, "reset", "--hard", "--quiet", "HEAD~1");
            },
        };

        var pass = await TidyAsync(trees, root);

        Assert.True(rewound, "the session branch's delete was reached");
        Assert.DoesNotContain(pass.Results, result => result.Removed && result.Item.Branch == opened.Branch);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", opened.Branch)).Trim());
    }

    /// <summary>
    /// 🔴 The second review's P2 (TreeFollows.cs:82 and :101): a session branch the tidy removed while its tree had moved onto a branch of
    /// its own leaves a trace, and the live tree it names is still followed: its next switch is learned, and known once the tree is gone.
    /// </summary>
    [Fact]
    public async Task A_removed_branchs_live_tree_is_followed_to_its_next_branch()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(opened.Path, "checkout", "--quiet", "-b", "feature/x");
        await CommitAsync(opened.Path, "work.txt", "work\n", "the work");
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        await TidyAsync(trees, root);
        Assert.False(await StandsAsync(root, opened.Branch));

        await GitAsync(opened.Path, "checkout", "--quiet", "-b", "feature/y");
        await TidyAsync(trees, root);

        Assert.Equal("feature/y", Grown.Entries().Last(entry => entry.Branch == opened.Branch).Now);
        await GitAsync(root, "worktree", "remove", "--force", opened.Path);
        Assert.Equal("feature/y", await trees.BranchNowAsync(opened.Path));
    }

    /// <summary>
    /// The git starts counted down (END3 measured the keeper at 0.4-4 s of them per repository): a pace's tidy of a repository whose
    /// branches the line holds none of starts no more git than the end judgement's keeper already did for each of them, however many.
    /// Before FABRIC-END4a each was judged again by the press's list as well. 🔴 FABRIC-END2d: the first pass is the second, since no
    /// recorded branch the keeper could not prove is judged again by the press's list (END4a's review fallback is gone).
    /// </summary>
    [Fact]
    public async Task A_tidy_judges_no_branch_the_line_does_not_hold()
    {
        var one = await RepositoryAsync("one");
        var four = await RepositoryAsync("four");
        var trees = new SessionTrees(Home);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        // The recovery refs' retention is held once a day for each repository in a process (CLEANUP1): asked here, before any branch.
        foreach (var root in new[] { one, four }) await trees.TidyEmptyAsync(root, Path.GetFileName(root), "aurora", InUseAsync, CancellationToken.None);
        await WorkAsync(trees, one, "a.txt", merged: false, repository: "one");
        for (var each = 0; each < 4; each++) await WorkAsync(trees, four, $"{each}.txt", merged: false, repository: "four");

        var overOne = await BeyondTheKeeperAsync(trees, one);
        var overFour = await BeyondTheKeeperAsync(trees, four);

        // Read beside the D173 note: the starts the tidy makes beyond the keeper's, with one branch and with four, first pass and second.
        output.WriteLine($"END2d git starts beyond the keeper: one branch {overOne.First} then {overOne.Second}, four branches {overFour.First} then {overFour.Second}");
        Assert.Equal(overOne.First, overOne.Second);
        Assert.Equal(overFour.First, overFour.Second);
        // At most the follow's one read of each tree (TREEFOLLOW1), three here; before FABRIC-END4a the press's list judged each of
        // them again, nine starts a branch on this machine (16 with one, 43 with four).
        Assert.True(overFour.Second - overOne.Second <= 3, $"the tidy judged branches the line holds none of: {overOne.Second} starts beyond the keeper with one, {overFour.Second} with four");
    }

    /// <summary>
    /// FABRIC-END2d, the review's older squash record (D173's FABRIC-END4a note, "After review"), a guard green before and after: a session
    /// branch from before units whose squashing landing the line holds goes at the look's tidy, on the fact the keeper keeps for it.
    /// </summary>
    [Fact]
    public async Task An_older_squash_records_session_branch_goes_once_the_line_holds_its_landing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (_, branch, landing) = await OlderSquashAsync(trees, root);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        var pass = await TidyAsync(trees, root);

        Assert.Contains(pass.Results, result => result.Removed && result.Item.Branch == branch);
        Assert.False(await StandsAsync(root, branch));
        Assert.Contains(pass.Landed, result => result.Removed && result.Item.Branch == landing);
    }

    /// <summary>
    /// FABRIC-END2d, the review's landing inside another, green before (END4a's fallback) and after: it goes at the look's tidy with the one it
    /// is inside, now on the fact the keeper keeps for it.
    /// </summary>
    [Fact]
    public async Task A_landing_inside_another_the_line_holds_goes_with_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (a, b, _, _) = await InsideLandingAsync(trees, root);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        var pass = await TidyAsync(trees, root);

        Assert.Contains(pass.Landed, result => result.Removed && result.Item.Branch == a.Branch && result.Item.Kind == LandedKind.Inside);
        Assert.Contains(pass.Landed, result => result.Removed && result.Item.Branch == b.Branch);
        Assert.False(await StandsAsync(root, a.Branch!));
    }

    /// <summary>
    /// 🔴 The second review's P2 (AutoTidies.cs:263, :268, :320), now with no memory at all (FABRIC-END2d): a session branch from before units
    /// whose work only a landing inside another holds, which the press kept because its tree held uncommitted work, goes at the next pace once
    /// the tree is clean, with neither its tip nor the line moved. END4a's fallback remembered it at its tip and the line's commit and judged
    /// it again only once the <c>refs</c> deadline passed; the keeper now keeps its end (through the landing whose branch holds it), so it is
    /// a candidate at every pace.
    /// </summary>
    [Fact]
    public async Task A_kept_unproven_branch_goes_at_the_next_pace_once_its_tree_is_clean()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (_, _, tree, branch) = await InsideLandingAsync(trees, root);
        ForgetUnits();
        await File.WriteAllTextAsync(Path.Combine(tree, "a.txt"), "not yet committed\n");
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        var kept = await TidyAsync(trees, root);
        Assert.DoesNotContain(kept.Results, result => result.Removed && result.Item.Branch == branch);
        await GitAsync(tree, "checkout", "--", "a.txt");

        var next = await TidyAsync(trees, root);

        Assert.Contains(next.Results, result => result.Removed && result.Item.Branch == branch);
    }

    /// <summary>
    /// The git starts a pace's tidy of <paramref name="root"/> makes, less those its keeper alone makes over the same refs, on its first pass
    /// and on its second.
    /// </summary>
    private static async Task<(int First, int Second)> BeyondTheKeeperAsync(SessionTrees trees, string root)
    {
        var keeper = await CountedAsync(() => trees.Ends.KeepAsync(root, Path.GetFileName(root), "aurora", "main", CancellationToken.None));
        var first = await CountedAsync(() => trees.TidyEmptyAsync(root, Path.GetFileName(root), "aurora", InUseAsync, CancellationToken.None));
        var second = await CountedAsync(() => trees.TidyEmptyAsync(root, Path.GetFileName(root), "aurora", InUseAsync, CancellationToken.None));
        return (first - keeper, second - keeper);
    }

    /// <summary>
    /// FABRIC-UNKNOWN1 over real git: a <c>daoris/*</c> branch no record names, though the line holds all of it, is read by the
    /// inventory as an unknown owner, blocked, and the look's tidy leaves it standing: only the person's press removes it.
    /// </summary>
    [Fact]
    public async Task An_unrecorded_daoris_branch_is_named_blocked_and_the_look_never_removes_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var recorded = await WorkAsync(trees, root, "a.txt", merged: true);
        await GitAsync(root, "branch", "daoris/orphan");
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        var unread = new List<string>();
        var roots = new Dictionary<string, string> { ["engine"] = root };
        var before = await WorkInventories.MilestonesAsync(Home, roots, NoneInUse, unread);
        var pass = await TidyAsync(trees, root);
        var after = await WorkInventories.MilestonesAsync(Home, roots, NoneInUse, unread);

        var unit = Assert.Single(before, each => each.Branch == "daoris/orphan");
        Assert.Equal("unknown", unit.End.State);
        Assert.Equal("blocked", Assert.Single(unit.Cleanup.Waits).Disposition);
        Assert.DoesNotContain(pass.Results, result => result.Item.Branch == "daoris/orphan");
        Assert.True(await StandsAsync(root, "daoris/orphan"));
        Assert.True(await StandsAsync(root, "daoris/orphan") && !await StandsAsync(root, recorded));
        Assert.Contains(after, each => each.Branch == "daoris/orphan");
        Assert.DoesNotContain(after, each => each.Branch == recorded && each.Entries.Any(entry => entry.Disposition == "blocked"));
    }

    private static async Task<int> CountedAsync<T>(Func<Task<T>> act)
    {
        var started = 0;
        using (ProcessStarts.Within(info =>
               {
                   if (Path.GetFileNameWithoutExtension(info.FileName).Equals("git", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref started);
                   return Process.Start(info);
               }))
        {
            await act();
        }

        return started;
    }
}
