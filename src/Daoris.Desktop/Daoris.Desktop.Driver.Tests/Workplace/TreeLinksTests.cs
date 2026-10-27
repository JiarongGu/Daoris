using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREELINK1 (D51's note): the walk that takes a tree's links off before git removes it, over files alone: a link goes as a link and
/// what it points at stands, a link is never walked into, a tree whose own folder is a link stays, a tree with no link is left as it
/// was, a gone tree is nothing to do, the tree's own <c>.git</c> is never looked into, and past its bound it takes nothing off and
/// says why the tree stays. Each link is made as TREEDEPS1's copy makes one, a junction with no process on Windows. Git's removal
/// over them is <c>TreeLinkRemovalTests</c>'.
/// </summary>
public sealed class TreeLinksTests : IDisposable
{
    private const string Persons = "the person's own\n";
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-links", Guid.NewGuid().ToString("N")[..8]);
    private string Tree => Path.Combine(_scratch, "tree");
    private string Outside => Path.Combine(_scratch, "outside");

    public TreeLinksTests()
    {
        Directory.CreateDirectory(Tree);
        Directory.CreateDirectory(Outside);
        File.WriteAllText(Path.Combine(Outside, "notes.md"), Persons);
    }

    // A link a failed test left goes as a link, never followed.
    public void Dispose() => TreeSetAside.DeleteNeverFollowing(_scratch);

    [Fact]
    public void A_link_in_the_tree_is_taken_off_as_a_link_and_what_it_points_at_stands()
    {
        Write(Path.Combine("node_modules", "kept", "index.js"));
        var link = Path.Combine(Tree, "node_modules", "@fixture", "web");
        TreeDependencies.Link(link, Outside);

        Assert.Null(TreeLinks.Unlinked(Tree));

        Assert.False(Path.Exists(link));
        Assert.True(File.Exists(Path.Combine(Tree, "node_modules", "kept", "index.js")));
        Assert.Equal(Persons, File.ReadAllText(Path.Combine(Outside, "notes.md")));
    }

    [Fact]
    public void A_link_is_never_walked_into_nor_what_it_holds_counted()
    {
        foreach (var name in new[] { "a", "b", "c" }) File.WriteAllText(Path.Combine(Outside, $"{name}.md"), Persons);
        TreeDependencies.Link(Path.Combine(Tree, "linked"), Outside);

        Assert.Null(TreeLinks.Unlinked(Tree, limit: 1));

        Assert.Equal(4, Directory.GetFiles(Outside).Length);
    }

    [Fact]
    public void A_tree_whose_own_folder_is_a_link_stays_and_nothing_through_it_is_touched()
    {
        TreeDependencies.Link(Path.Combine(Outside, "inner"), Path.Combine(_scratch, "tree"));
        var linked = Path.Combine(_scratch, "linked-tree");
        TreeDependencies.Link(linked, Outside);

        Assert.Equal("its tree's folder is itself a link, which git's removal would reach through", TreeLinks.Unlinked(linked));

        Assert.True(Path.Exists(Path.Combine(Outside, "inner")));
        Assert.Equal(Persons, File.ReadAllText(Path.Combine(linked, "notes.md")));
    }

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
