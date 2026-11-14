using Daoris.Ports.Repositories;
using Xunit;

namespace Daoris.Testing;

public abstract partial class RepositoryContract
{
    private async Task<(string Root, string First, string Tree)> WithTreeAsync(string branch)
    {
        var (root, first) = await OneAsync();
        var tree = Beside(root, "tree");
        Assert.True((await Backend.AddWorktreeAsync(root, new GitWorktreeAdd(tree, first, branch), Ct)).Ok);
        return (root, first, tree);
    }

    [Fact]
    public async Task A_tree_counts_its_uncommitted_paths_with_an_untracked_folder_as_one_and_reads_its_head_and_branch()
    {
        var (root, first) = await OneAsync();
        Assert.Equal(0, await Backend.UncommittedAsync(root, Ct));
        Assert.Equal(new GitTreeStatus(0, first, "main"), await Backend.TreeStatusAsync(root, Ct));

        await WriteAsync(root, "a.txt", "changed\n");
        await WriteAsync(root, "new/one.txt", "1\n");
        await WriteAsync(root, "new/two.txt", "2\n");

        Assert.Equal(2, await Backend.UncommittedAsync(root, Ct));
        Assert.Equal(new GitTreeStatus(2, first, "main"), await Backend.TreeStatusAsync(root, Ct));
    }

    [Fact]
    public async Task A_place_names_the_common_directory_the_working_root_and_the_branch_and_a_detached_tree_names_head()
    {
        var (root, first, tree) = await WithTreeAsync("feature/x");

        var own = await Backend.PlaceAsync(tree, Ct);
        var main = await Backend.PlaceAsync(root, Ct);

        Assert.Equal(0, own.Code);
        Assert.Equal("feature/x", own.Branch.Trim());
        Assert.Equal(Path.GetFullPath(Path.Combine(root, ".git")), Path.GetFullPath(own.CommonDir.Trim()));
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(own.TopLevel.Trim()));
        Assert.Equal("main", main.Branch.Trim());

        Assert.True((await Backend.SwitchAsync(tree, new GitSwitch(null, first), Ct)).Ok);
        Assert.Equal("HEAD", (await Backend.PlaceAsync(tree, Ct)).Branch.Trim());
    }

    [Fact]
    public async Task A_layout_puts_a_linked_trees_git_directory_under_the_common_one_the_main_checkouts_in_it()
    {
        var (root, _, tree) = await WithTreeAsync("feature/x");

        var main = await Backend.LayoutAsync(root, Ct);
        var linked = await Backend.LayoutAsync(tree, Ct);

        Assert.Equal(Path.GetFullPath(Path.Combine(root, ".git")), main!.CommonDir);
        Assert.Equal(main.CommonDir, main.GitDir);
        Assert.Equal(main.CommonDir, linked!.CommonDir);
        Assert.StartsWith(Path.Combine(main.CommonDir, "worktrees") + Path.DirectorySeparatorChar, linked.GitDir, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_tree_is_removed_clean_and_refused_with_changes_or_a_lock_until_forced()
    {
        var (root, _, tree) = await WithTreeAsync("feature/x");
        await WriteAsync(tree, "loose.txt", "x\n");

        Assert.False((await Backend.RemoveTreeAsync(root, tree, force: false, Ct)).Ok);
        Assert.Contains(await Backend.WorktreesAsync(root, Ct) ?? [], each => each.Branch == "feature/x");

        await RemoveAsync(tree, "loose.txt");
        Assert.True((await Backend.LockAsync(root, tree, "held", Ct)).Ok);
        Assert.False((await Backend.RemoveTreeAsync(root, tree, force: false, Ct)).Ok);
        Assert.True((await Backend.UnlockAsync(root, tree, Ct)).Ok);

        Assert.True((await Backend.RemoveTreeAsync(root, tree, force: false, Ct)).Ok);
        Assert.DoesNotContain(await Backend.WorktreesAsync(root, Ct) ?? [], each => each.Branch == "feature/x");
    }

    [Fact]
    public async Task The_commits_no_branch_of_the_persons_holds_are_listed_newest_first_and_daoris_branches_hold_none()
    {
        var (root, first, tree) = await WithTreeAsync("daoris/s-1");
        await WriteAsync(tree, "one.txt", "1\n");
        Assert.True((await Backend.CommitAllAsync(tree, "one", Ct)).Ok);
        await WriteAsync(tree, "two.txt", "2\n");
        Assert.True((await Backend.CommitAllAsync(tree, "two", Ct)).Ok);
        var tip = (await Backend.HeadAsync(tree, Ct))!;

        var lines = (await Backend.UnlandedAsync(tree, "HEAD", null, Ct))!;
        Assert.Equal(2, lines.Count);
        Assert.EndsWith(" two", lines[0]);
        Assert.EndsWith(" one", lines[1]);
        Assert.Empty((await Backend.UnlandedAsync(root, first, null, Ct))!);

        Assert.True((await Backend.UpdateRefAsync(root, "refs/heads/main", tip, first, Ct)).Ok);
        Assert.Empty((await Backend.UnlandedAsync(tree, "HEAD", null, Ct))!);
        Assert.Null(await Backend.UnlandedAsync(tree, "refs/heads/not-there", null, Ct));
    }
}
