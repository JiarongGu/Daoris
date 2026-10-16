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
    /// Before FABRIC-END4a each was judged again by the press's list as well.
    /// </summary>
    [Fact]
    public async Task A_tidy_judges_no_branch_the_line_does_not_hold()
    {
        var one = await RepositoryAsync("one");
        var four = await RepositoryAsync("four");
        var trees = new SessionTrees(Home);
        await WorkAsync(trees, one, "a.txt", merged: false, repository: "one");
        for (var each = 0; each < 4; each++) await WorkAsync(trees, four, $"{each}.txt", merged: false, repository: "four");
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        var overOne = await BeyondTheKeeperAsync(trees, one);
        var overFour = await BeyondTheKeeperAsync(trees, four);

        // Read beside the D173 note: the starts the tidy makes beyond the keeper's, with one branch and with four.
        output.WriteLine($"END4a git starts beyond the keeper: one branch {overOne}, four branches {overFour}");
        // At most the follow's one read of each tree (TREEFOLLOW1), three here; before FABRIC-END4a the press's list judged each of
        // them again, nine starts a branch on this machine (16 with one, 43 with four).
        Assert.True(overFour - overOne <= 3, $"the tidy judged branches the line holds none of: {overOne} starts beyond the keeper with one, {overFour} with four");
    }

    /// <summary>
    /// 🔴 The second review's P2 (AutoTidies.cs:263, :268, :320): a recorded branch the keeper could not prove and the press's judgement kept
    /// is judged again once the <c>refs</c> deadline passed, with neither its tip nor the line moved, since what kept it (a busy or dirty
    /// tree, a landing checked out inside another) can clear with neither moving.
    /// </summary>
    [Fact]
    public async Task A_kept_unproven_branch_is_judged_again_once_the_refs_deadline_passed()
    {
        var root = await RepositoryAsync("engine");
        var now = DateTimeOffset.UtcNow;
        var trees = new SessionTrees(Home) { Clock = () => now };
        await WorkAsync(trees, root, "a.txt", merged: false);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        await TidyAsync(trees, root);

        var within = await CountedAsync(() => TidyAsync(trees, root));
        now += RefWatch.Deadline;
        var after = await CountedAsync(() => TidyAsync(trees, root));

        Assert.True(after > within, $"judged again at the deadline: {within} git starts within it, {after} after");
    }

    /// <summary>
    /// The git starts a pace's tidy of <paramref name="root"/> makes, less those its keeper alone makes over the same refs, once a first
    /// pass has judged its recorded branches the keeper could not prove: those are judged again only when their tip or the line moves.
    /// </summary>
    private static async Task<int> BeyondTheKeeperAsync(SessionTrees trees, string root)
    {
        await trees.TidyEmptyAsync(root, Path.GetFileName(root), "aurora", InUseAsync, CancellationToken.None);
        var keeper = await CountedAsync(() => trees.Ends.KeepAsync(root, Path.GetFileName(root), "aurora", "main", CancellationToken.None));
        var tidy = await CountedAsync(() => trees.TidyEmptyAsync(root, Path.GetFileName(root), "aurora", InUseAsync, CancellationToken.None));
        return tidy - keeper;
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
