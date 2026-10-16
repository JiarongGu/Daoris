using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END4a's second review over real git: a judged branch goes in one ref transaction with what its proof rested on
/// (<c>git update-ref --stdin</c>, <c>verify</c> beside the <c>delete</c>), for every caller of the delete, so a line rewound as it goes
/// keeps the branch with no absence anyone can observe. The one window left, a checkout made after the trees were read, is looked at again
/// once it went: an unread list is not clear, and a put-back git refused is said as such, never as put back.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class JudgedDeleteGitTests : LandedFixture
{
    private static IReadOnlySet<string> NoneInUse { get; } = new HashSet<string>();

    private static Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult(NoneInUse);

    /// <summary>A session's tree with one commit, merged onto the line; its tree removed by hand where asked, so the branch stands alone.</summary>
    private async Task<(string Branch, string Tree, string Tip)> MergedAsync(string root, bool treeless)
    {
        var opened = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "a.txt", "a\n", "work on a");
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", $"merge {opened.Branch}", opened.Branch);
        if (treeless) await GitAsync(root, "worktree", "remove", opened.Path);
        return (opened.Branch, opened.Path, (await GitAsync(root, "rev-parse", opened.Branch)).Trim());
    }

    private static async Task<string?> TipAsync(string root, string branch)
    {
        var listed = (await GitAsync(root, "for-each-ref", "--format=%(objectname)", $"refs/heads/{branch}")).Trim();
        return listed.Length == 0 ? null : listed;
    }

    /// <summary>The line rewound under the delete, after the last proof: git's own verify refuses the transaction.</summary>
    private static Func<string, Task> Rewinds(string root) => async _ => await GitAsync(root, "reset", "--hard", "--quiet", "HEAD~1");

    /// <summary>🔴 The review's P1 (TreeTidies.cs:336): LAND3's door now passes what its proof rested on, so the line rewound as the branch goes keeps it.</summary>
    [Fact]
    public async Task The_doors_removal_keeps_a_branch_whose_line_moved_as_it_went()
    {
        var root = await RepositoryAsync("engine");
        var (branch, _, tip) = await MergedAsync(root, treeless: true);
        var trees = new SessionTrees(Home) { BeforeRefDeleted = Rewinds(root) };

        var removal = await trees.RemoveBranchAsync(root, "engine", branch);

        Assert.False(removal.Removed, removal.Message);
        Assert.Equal(tip, await TipAsync(root, branch));
        Assert.Contains("moved as it went", removal.Message, StringComparison.Ordinal);
    }

    /// <summary>🔴 The review's P1 (TreeRemovals.cs:189): a tree's removal keeps its branch where the line moved as it went.</summary>
    [Fact]
    public async Task A_trees_removal_keeps_its_branch_where_the_line_moved_as_it_went()
    {
        var root = await RepositoryAsync("engine");
        var (branch, tree, tip) = await MergedAsync(root, treeless: false);
        var trees = new SessionTrees(Home) { BeforeRefDeleted = Rewinds(root) };

        var removal = await trees.RemoveAsync(tree);

        Assert.Equal(tip, await TipAsync(root, branch));
        Assert.Contains($"kept its branch `{branch}`", removal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The review's P1 (ContentHolds.cs:302): a list of the trees git could not give once the branch went is no answer that no tree
    /// checked it out meanwhile, so the branch is put back.
    /// </summary>
    [Fact]
    public async Task An_unread_tree_list_after_the_delete_puts_the_branch_back()
    {
        var root = await RepositoryAsync("engine");
        var (branch, _, tip) = await MergedAsync(root, treeless: true);
        var unread = false;
        var trees = new SessionTrees(Home) { ListFails = _ => unread, BeforeRefDeleted = _ => { unread = true; return Task.CompletedTask; } };

        var removal = await trees.RemoveBranchAsync(root, "engine", branch);

        Assert.False(removal.Removed, removal.Message);
        Assert.Equal(tip, await TipAsync(root, branch));
        Assert.Contains("put back where it was", removal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The review's P1 (ContentHolds.cs:309): a late checkout's put-back git refuses (a lock, a competing creation) is said as such,
    /// never read as put back.
    /// </summary>
    [Fact]
    public async Task A_put_back_git_refuses_is_said_so()
    {
        var root = await RepositoryAsync("engine");
        var (branch, _, tip) = await MergedAsync(root, treeless: true);
        var late = Path.Combine(Path.GetDirectoryName(root)!, $"late-{Guid.NewGuid():N}"[..13]);
        var trees = new SessionTrees(Home) { BeforeRefDeleted = async _ => await GitAsync(root, "worktree", "add", "--quiet", late, branch) };
        WorkingTree.Answered.Value = arguments => arguments is ["update-ref", var reference, var at, ""] && reference == $"refs/heads/{branch}" && at == tip
            ? (1, "", $"fatal: cannot lock ref 'refs/heads/{branch}': Unable to create '{branch}.lock': File exists.")
            : null;
        try
        {
            var removal = await trees.RemoveBranchAsync(root, "engine", branch);

            Assert.False(removal.Removed, removal.Message);
            Assert.Contains("could not put it back", removal.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("put back where it was", removal.Message, StringComparison.Ordinal);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }
}
