using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREEGONE1 (D51, D131): a session's tree asked for before it goes on, over real git. One git lists stands as it is; one whose
/// folder went while its branch stands is made again in its place on that branch, its commits there; one whose branch went too, or
/// whose branch another tree holds, or that this home never opened, is made nowhere, each said.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeReturnsTests : BranchTidyingFixture
{
    [Fact]
    public async Task A_tree_git_lists_stands_and_nothing_is_said()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "default");

        var stands = await trees.EnsureAsync(root, tree.Path);

        Assert.Equal(new TreeStanding(TreeStand.Stands, tree.Branch, null), stands);
    }

    [Fact]
    public async Task A_removed_tree_whose_branch_stands_is_made_again_in_its_place_with_its_commits()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        var head = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "worktree", "remove", "--force", tree.Path);

        var remade = await trees.EnsureAsync(root, tree.Path);

        Assert.Equal((TreeStand.Remade, tree.Branch), (remade.Stand, remade.Branch));
        Assert.Equal(NoteCodes.TreeRemade.Code, Assert.Single(remade.Said!.Parts).Code);
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal(head, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
        Assert.Equal(TreeStand.Stands, (await trees.EnsureAsync(root, tree.Path)).Stand);
    }

    [Fact]
    public async Task A_tree_whose_branch_is_gone_too_is_made_nowhere_and_said()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "default");
        await GitAsync(root, "worktree", "remove", "--force", tree.Path);
        await GitAsync(root, "branch", "-D", tree.Branch);

        var gone = await trees.EnsureAsync(root, tree.Path);

        Assert.Equal((TreeStand.Gone, tree.Branch), (gone.Stand, gone.Branch));
        Assert.Equal(NoteCodes.TreeGone.Code, Assert.Single(gone.Said!.Parts).Code);
        Assert.False(Directory.Exists(tree.Path));
        Assert.DoesNotContain(tree.Branch, await GitAsync(root, "branch", "--list"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tree_whose_branch_another_tree_holds_is_not_made_again()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "default");
        await GitAsync(root, "worktree", "remove", "--force", tree.Path);
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        await GitAsync(root, "worktree", "add", "--quiet", elsewhere, tree.Branch);

        var unmade = await trees.EnsureAsync(root, tree.Path);

        Assert.Equal(TreeStand.Unmade, unmade.Stand);
        Assert.Contains("is checked out in", unmade.Said!.Note, StringComparison.Ordinal);
        Assert.False(Directory.Exists(tree.Path));
    }

    [Fact]
    public async Task A_folder_this_home_never_opened_is_never_made()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);

        var unmade = await trees.EnsureAsync(root, Path.Combine(_scratch, "not-a-tree"));

        Assert.Equal((TreeStand.Unmade, (string?)null), (unmade.Stand, unmade.Branch));
        Assert.False(Directory.Exists(Path.Combine(_scratch, "not-a-tree")));
    }
}
