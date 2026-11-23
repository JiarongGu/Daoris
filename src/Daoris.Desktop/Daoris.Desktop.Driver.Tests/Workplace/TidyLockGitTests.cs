using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The look's tidy holds a repository's writer lock around one tree's removal, never across the walk that judges its trees: over real git,
/// finished trees whose reads a stand-in slows, a promotion's lock taken in the middle of the walk, and a tree changed between its judgement
/// and its removal, which stays. The measured defect: one tidy pass held the lock for 40 minutes reading 56 trees it kept.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TidyLockGitTests : LandedFixture
{
    private static Task<IReadOnlySet<string>> NoneAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary>A finished session tree: one commit, merged onto the line.</summary>
    private async Task<(string Tree, string Branch)> FinishedAsync(SessionTrees trees, string root, string file)
    {
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, file, $"{file}\n", $"work on {file}");
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", $"merge {opened.Branch}", opened.Branch);
        return (opened.Path, opened.Branch);
    }

    [Fact]
    public async Task A_promotion_takes_the_writer_lock_while_the_tidy_reads_each_tree()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var finished = new List<(string Tree, string Branch)>();
        foreach (var file in new[] { "a.txt", "b.txt", "c.txt" }) finished.Add(await FinishedAsync(trees, root, file));
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        var free = new List<bool>();
        TidyPass pass;
        using (SetAsideFaults.Within(new SetAsideFault
        {
            WhileJudging = _ =>
            {
                // What a promotion asks at its step: the repository alone, for as long as it writes.
                using var writing = TreeLock.TryWriting(Home, root, "engine", TreeLock.Promoting);
                free.Add(writing is not null);
                return Task.CompletedTask;
            },
        }))
        {
            pass = await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync);
        }

        Assert.Equal(3, pass.Results.Count(result => result.Removed));
        Assert.Equal([true, true, true], free);
        Assert.Null(TreeLock.RefusedBy(Home, root));
    }

    [Fact]
    public async Task A_tree_that_changed_between_its_judgement_and_its_removal_is_kept()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (changed, changedBranch) = await FinishedAsync(trees, root, "a.txt");
        var (_, otherBranch) = await FinishedAsync(trees, root, "b.txt");
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        TidyPass pass;
        using (SetAsideFaults.Within(new SetAsideFault
        {
            WhileJudging = tree =>
            {
                if (Path.GetFullPath(tree) == Path.GetFullPath(changed)) File.WriteAllText(Path.Combine(tree, "written-meanwhile.txt"), "kept\n");
                return Task.CompletedTask;
            },
        }))
        {
            pass = await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync);
        }

        Assert.DoesNotContain(pass.Results, result => result.Removed && result.Item.Branch == changedBranch);
        Assert.Contains(pass.Results, result => result.Removed && result.Item.Branch == otherBranch);
        Assert.True(File.Exists(Path.Combine(changed, "written-meanwhile.txt")));
        Assert.Contains(changedBranch, (await GitAsync(root, "branch", "--list")));
    }

    [Fact]
    public async Task A_refused_hold_says_who_holds_it_and_for_how_long()
    {
        var root = await RepositoryAsync("engine");
        using (var writing = TreeLock.TryWriting(Home, root, "engine", TreeLock.Promoting))
        {
            Assert.NotNull(writing);
            Assert.DoesNotContain("held for", TreeLock.HeldNow(Home, root, "engine"), StringComparison.Ordinal);
            // Read forty-one minutes after the holder wrote its note: a long hold is named in the words of whoever it refuses.
            var note = Path.ChangeExtension(TreeLock.PathOf(Home, root), ".by");
            var later = long.Parse((await File.ReadAllTextAsync(note)).Split('\n')[3]) + (long)TimeSpan.FromMinutes(41).TotalMilliseconds;
            Assert.Equal("held for 41 minutes", TreeLock.HeldFor(Home, root, later));
            Assert.Contains("(held for 41 minutes)", TreeLock.HeldNow(Home, root, "engine", later), StringComparison.Ordinal);
            Assert.Contains("(held for 41 minutes)", TreeLock.Starting("engine", TreeLock.RefusedBy(Home, root), TreeLock.HeldFor(Home, root, later)), StringComparison.Ordinal);
        }

        Assert.Equal("held for 41 minutes", TreeLock.Age(TimeSpan.FromMinutes(41.4)));
        Assert.Equal("held for 1 hour 5 minutes", TreeLock.Age(TimeSpan.FromMinutes(65)));
        Assert.Null(TreeLock.Age(TimeSpan.FromSeconds(50)));
    }
}
