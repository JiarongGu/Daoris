using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREELINK1 (D51's note): the walk that takes a tree's links off before git removes it, its pure halves: a tree with no link is left
/// as it was, a gone tree is nothing to do, the tree's own <c>.git</c> is never looked into, and past its bound it takes nothing off
/// and says why the tree stays. The links themselves, made with a process on Windows, are <c>TreeLinkRemovalTests</c>'.
/// </summary>
public sealed class TreeLinksTests : IDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-links", Guid.NewGuid().ToString("N")[..8]);
    private string Tree => Path.Combine(_scratch, "tree");

    public TreeLinksTests() => Directory.CreateDirectory(Tree);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    private void Write(string relative)
    {
        var path = Path.Combine(Tree, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x\n");
    }

    [Fact]
    public void A_tree_with_no_link_is_left_as_it_was()
    {
        Write("README.md");
        Write(Path.Combine("src", "app", "index.ts"));

        Assert.Null(TreeLinks.Unlinked(Tree));

        Assert.True(File.Exists(Path.Combine(Tree, "README.md")));
        Assert.True(File.Exists(Path.Combine(Tree, "src", "app", "index.ts")));
    }

    [Fact]
    public void A_tree_already_gone_is_nothing_to_do() =>
        Assert.Null(TreeLinks.Unlinked(Path.Combine(_scratch, "gone")));

    [Fact]
    public void Past_its_bound_the_walk_says_why_the_tree_stays()
    {
        Write("a.txt");
        Write(Path.Combine("node_modules", "b.txt"));
        Write(Path.Combine("node_modules", "c.txt"));

        Assert.Equal(
            "its tree holds more than 3 entries, too many to look through for the links git's removal would follow",
            TreeLinks.Unlinked(Tree, limit: 3));
        Assert.Null(TreeLinks.Unlinked(Tree, limit: 4));
    }

    [Fact]
    public void The_trees_own_git_is_never_looked_into_nor_counted()
    {
        Write(Path.Combine(".git", "objects", "a"));
        Write(Path.Combine(".git", "objects", "b"));
        Write("README.md");

        Assert.Null(TreeLinks.Unlinked(Tree, limit: 1));
    }
}
