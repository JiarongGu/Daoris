using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The folders trees leave behind (the first real post-merge run): git let go of a tree whose folder something held
/// open, and the clean-up tries the empty folder again. Only an empty folder under the trees home is ever deleted.
/// </summary>
/// <remarks>Files only: with no repository to judge, the clean-up asks git nothing.</remarks>
public sealed class TreeLeftoversTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-leftovers", Guid.NewGuid().ToString("N")[..8]);

    public TreeLeftoversTests() => Directory.CreateDirectory(_home);

    public void Dispose() => TreeSetAside.DeleteNeverFollowing(_home);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task The_cleanup_never_enters_or_removes_a_link_at_any_level_of_its_scan(int level)
    {
        var trees = new SessionTrees(_home);
        var outside = Path.Combine(_home, "outside");
        string[] parts = ["aurora", "engine", "s-linked"];
        var link = parts.Take(level).Aggregate(trees.TreesRoot, Path.Combine);
        var emptyOutside = parts.Skip(level).Aggregate(outside, Path.Combine);
        Directory.CreateDirectory(emptyOutside);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        TreeDependencies.Link(link, outside);
        var ordinary = Path.Combine(trees.TreesRoot, "ordinary", "engine", "s-empty");
        if (level > 0) Directory.CreateDirectory(ordinary);

        var done = await trees.CleanAsync([], new HashSet<string>());

        Assert.True(Directory.Exists(emptyOutside), "the linked target's empty directory must remain");
        Assert.True(Directory.Exists(link), "the link is not an ordinary leftover to remove");
        if (level == 0) Assert.Empty(done.Folders!);
        else
        {
            Assert.False(Directory.Exists(ordinary));
            Assert.Contains(ordinary, Assert.Single(done.Folders!));
        }
    }

    [Fact]
    public async Task The_clean_up_removes_an_empty_folder_a_tree_left_and_leaves_one_that_holds_anything()
    {
        var trees = new SessionTrees(_home);
        var empty = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-0000dead");
        var full = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-0000beef");
        Directory.CreateDirectory(empty);
        Directory.CreateDirectory(full);
        await File.WriteAllTextAsync(Path.Combine(full, "keep.txt"), "somebody's\n");

        var done = await trees.CleanAsync([], new HashSet<string>());

        Assert.False(Directory.Exists(empty));
        Assert.True(File.Exists(Path.Combine(full, "keep.txt")));
        var said = Assert.Single(done.Folders!);
        Assert.StartsWith("removed the empty folder a tree left at ", said);
        Assert.Contains("s-0000dead", said);
    }

    /// <summary>
    /// TREECLEANFREE1: what outlives a holder's exit (a scanner's read of the folder) is a handle the clean-up meets as
    /// well as the holder's. One that refuses every open fails the clean-up's look at the folder, and a clean-up that
    /// passed over what it could not look at left the folder and said nothing: every press says what became of it.
    /// </summary>
    [Theory]
    [InlineData(FolderHold.Exclusive)]
    [InlineData(FolderHold.NoDelete)]
    public async Task A_folder_a_handle_outlives_its_holder_in_is_said_held_each_press_and_goes_the_press_after_it_is_let_go(uint share)
    {
        if (!OperatingSystem.IsWindows()) return;
        var trees = new SessionTrees(_home);
        var left = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-0000f00d");
        Directory.CreateDirectory(left);

        using (FolderHold.Take(left, share))
        {
            for (var press = 0; press < 2; press++)
            {
                var done = await trees.CleanAsync([], new HashSet<string>());

                Assert.True(Directory.Exists(left), "a held folder cannot have gone");
                var said = Assert.Single(done.Folders!);
                Assert.Contains(left, said);
                Assert.Contains("stays", said);
                Assert.Contains("hold", said);
                Assert.Contains("open", said);
            }
        }

        var freed = await trees.CleanAsync([], new HashSet<string>());

        Assert.False(Directory.Exists(left));
        Assert.StartsWith("removed the empty folder a tree left at ", Assert.Single(freed.Folders!));
    }

    [Fact]
    public async Task With_no_trees_home_the_clean_up_has_no_folder_to_say_anything_of()
    {
        var done = await new SessionTrees(_home).CleanAsync([], new HashSet<string>());

        Assert.Empty(done.Folders!);
    }
}
