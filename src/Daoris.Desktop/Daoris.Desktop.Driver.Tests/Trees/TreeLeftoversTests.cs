using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The folders trees leave behind (the first real post-merge run): git let go of a tree whose folder something held
/// open, and the clean-up tries the empty folder again. Only an empty folder under the trees home is ever deleted.
/// </summary>
/// <remarks>Files only: with no repository to judge, the clean-up asks git nothing.</remarks>
public sealed class TreeLeftoversTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-tree-leftovers-" + Guid.NewGuid().ToString("N")[..8]);

    public TreeLeftoversTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
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

    [Fact]
    public async Task With_no_trees_home_the_clean_up_has_no_folder_to_say_anything_of()
    {
        var done = await new SessionTrees(_home).CleanAsync([], new HashSet<string>());

        Assert.Empty(done.Folders!);
    }
}
