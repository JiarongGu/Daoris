using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A pooled line's branch is learned from its slot: the agent moved the slot onto a branch of its own, and the branch the tree is on now, a
/// chain's step grows from, and the follow records, is that one. Real git in scratch repositories.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PooledFollowsGitTests : LandedFixture
{
    private const string Workspace = "aurora";

    private async Task<(string Root, SessionTrees Trees, string Tree, string Line, PooledLines Pool)> PooledAsync()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", Workspace);
        var line = Path.GetFileName(opened.Path);
        await GitAsync(root, "worktree", "remove", "--force", opened.Path);

        // The slot is the line's tree now: git makes its folder on the line's branch, and the pool names the line in it.
        var pool = new PooledLines(Home, Workspace, "engine");
        pool.Slot(1, line, folder: false);
        await GitAsync(root, "worktree", "add", pool.SlotFolder(1), $"daoris/{line}");
        await GitAsync(pool.SlotFolder(1), "switch", "-c", "feature/own");
        return (root, trees, opened.Path, line, pool);
    }

    [Fact]
    public async Task A_pooled_line_s_branch_now_is_the_one_its_slot_is_on()
    {
        var (_, trees, tree, _, pool) = await PooledAsync();
        using var _1 = pool;

        Assert.Equal("feature/own", await trees.BranchNowAsync(tree));
    }

    [Fact]
    public async Task The_follow_learns_a_pooled_line_s_branch_from_its_slot()
    {
        var (root, trees, _, line, pool) = await PooledAsync();
        using var _1 = pool;

        await new TreeFollows(trees.Context).LearnTreesAsync(Workspace, "engine", CancellationToken.None, root);

        Assert.Equal("feature/own", new SessionBranches(Home).Following("engine", $"daoris/{line}")?.Now);
    }

    [Fact]
    public async Task A_line_the_pool_never_held_answers_the_layout_s_branch_as_before()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", Workspace);
        var line = Path.GetFileName(opened.Path);

        Assert.Equal($"daoris/{line}", await trees.BranchNowAsync(opened.Path));
        await GitAsync(opened.Path, "switch", "-c", "feature/own");
        Assert.Equal("feature/own", await trees.BranchNowAsync(opened.Path));
    }
}
