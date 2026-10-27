using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREEPLACE1, on real git: a tree opened before the home's layout moved stays at the old place of <see cref="HomeEntry.Trees"/>
/// (D172 point 6), and is landed, merged, removed and tidied as one at the new place is. On the install, eleven such trees were
/// refused as not a session tree by `trees land` and `trees remove`, and the tidy kept them forever. Each tree here is opened
/// where a new one opens, then moved with `git worktree move` to the old place, which is how it stands on the install; a tree
/// moved outside both places is still refused by every door. The paths alone are <see cref="TreePlacesTests"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreePlacesGitTests : IDisposable
{
    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    private readonly string _scratch;
    private readonly string _home;

    public TreePlacesGitTests()
    {
        _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "tree-places-git", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// LandingTidyTests' chain, both trees at the old place: the later one lands, and the landing's own tidy takes the earlier
    /// branch with its tree, which it kept as "not a session tree" before.
    /// </summary>
    [Fact]
    public async Task A_tree_at_the_old_place_lands_and_the_landings_tidy_takes_an_old_place_tree_it_holds()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true))
            .Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "first.txt", "the first step");
        var second = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(second.Path, "second.txt", "the verify step");
        var firstOld = await ToOldPlaceAsync(root, first.Path);
        var secondOld = await ToOldPlaceAsync(root, second.Path);

        var landed = await trees.LandAsync(secondOld, new LandingSubject("s2", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        Assert.True(await BranchAsync(root, "feature/0fda18-fix-the-gap"), "the landed branch stands");
        Assert.False(await BranchAsync(root, second.Branch), "the landed session's branch goes");
        Assert.False(await BranchAsync(root, first.Branch), "the earlier step's branch the landing holds goes too");
        Assert.False(Directory.Exists(firstOld), "with its tree at the old place");
        Assert.Equal(new TidiedBranch(first.Branch, Removed: true, Tree: true), Assert.Single(landed.Tidied!));
    }

    [Fact]
    public async Task A_tree_at_the_old_place_merges()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "budget.ts", "cap hydration");
        var old = await ToOldPlaceAsync(root, opened.Path);

        var merged = await trees.MergeAsync(old);

        Assert.True(merged.Merged, merged.Message);
        Assert.Contains("cap hydration", await GitAsync(root, "log", "--oneline"));
    }

    [Fact]
    public async Task A_tree_at_the_old_place_is_removed_by_its_path_and_by_its_branch()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        var trees = new SessionTrees(_home);
        var byPath = await trees.OpenAsync(root, "engine", "aurora");
        var byBranch = await trees.OpenAsync(root, "engine", "aurora");
        var pathOld = await ToOldPlaceAsync(root, byPath.Path);
        var branchOld = await ToOldPlaceAsync(root, byBranch.Path);

        var removal = await trees.RemoveAsync(pathOld);
        var tidied = await trees.RemoveBranchAsync(root, "engine", byBranch.Branch);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(pathOld));
        Assert.False(await BranchAsync(root, byPath.Branch));
        Assert.True(tidied.Removed, tidied.Message);
        Assert.False(Directory.Exists(branchOld), "the branch's door takes its tree at the old place with it");
        Assert.False(await BranchAsync(root, byBranch.Branch));
    }

    /// <summary>A tree of this repository's moved outside both places is somebody's checkout: every door refuses it and leaves it.</summary>
    [Fact]
    public async Task A_tree_outside_both_places_is_still_refused_by_every_door()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the work");
        var outside = Path.Combine(_scratch, "elsewhere", "trees", "aurora", "engine", Path.GetFileName(opened.Path));
        Directory.CreateDirectory(Path.GetDirectoryName(outside)!);
        Assert.Equal(0, (await RunAsync(root, "worktree", "move", opened.Path, outside)).ExitCode);

        var landed = await trees.LandAsync(outside, new LandingSubject("s1", "0fda18", "Fix the gap"), inUse: NoneRunning);
        var merged = await trees.MergeAsync(outside);
        var removal = await trees.RemoveAsync(outside, force: true);
        var tidied = await trees.RemoveBranchAsync(root, "engine", opened.Branch, force: true);

        Assert.False(landed.Landed);
        Assert.Contains("not a session tree", landed.Message);
        Assert.False(merged.Merged);
        Assert.Contains("not a session tree", merged.Message);
        Assert.False(removal.Removed);
        Assert.Contains("not a session tree", removal.Message);
        Assert.False(tidied.Removed);
        Assert.Contains("not a session tree", tidied.Message);
        Assert.True(File.Exists(Path.Combine(outside, "work.txt")));
        Assert.True(await BranchAsync(root, opened.Branch));
    }

    /// <summary>The tree moved to where the same name sits at the old place of the trees, as an install's earlier trees are.</summary>
    private async Task<string> ToOldPlaceAsync(string root, string tree)
    {
        var old = DaorisHome.Places(_home, HomeEntry.Trees, "aurora", "engine", Path.GetFileName(tree))[^1];
        Assert.NotEqual(Path.GetFullPath(tree), Path.GetFullPath(old));
        Directory.CreateDirectory(Path.GetDirectoryName(old)!);
        var moved = await RunAsync(root, "worktree", "move", tree, old);
        Assert.True(moved.ExitCode == 0, moved.Stderr);
        return old;
    }

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    private static async Task CommitAsync(string tree, string file, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), message + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }
}
