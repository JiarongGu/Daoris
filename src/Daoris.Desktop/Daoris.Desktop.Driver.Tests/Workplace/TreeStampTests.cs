using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A tree's stamps (<see cref="TreeStamp"/>) over a real session tree: what the tidy's hold reads of it instead of walking it.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeStampTests : LandedFixture
{
    private async Task<string> TreeAsync()
    {
        var root = await RepositoryAsync("engine");
        var opened = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "a.txt", "a\n", "a");
        Directory.CreateDirectory(Path.Combine(opened.Path, "docs"));
        await File.WriteAllTextAsync(Path.Combine(opened.Path, "docs", "b.txt"), "b");
        // A file system stamps a change with a clock that ticks in milliseconds: a change made at once after a stamp waits it out.
        await Task.Delay(100);
        return opened.Path;
    }

    [Fact]
    public async Task A_tree_nothing_touched_differs_in_nothing()
    {
        var tree = await TreeAsync();
        var before = TreeStamp.Take(tree)!;
        Assert.Null(TreeStamp.Take(tree)!.Differences(before));
    }

    [Fact]
    public async Task A_path_made_or_removed_at_the_top_is_named()
    {
        var tree = await TreeAsync();
        var before = TreeStamp.Take(tree)!;
        await File.WriteAllTextAsync(Path.Combine(tree, "new.txt"), "x");
        File.Delete(Path.Combine(tree, "a.txt"));
        var said = TreeStamp.Take(tree)!.Differences(before);
        Assert.StartsWith("2 path(s) changed between the judgement and the move: ", said, StringComparison.Ordinal);
        Assert.Contains("a.txt", said, StringComparison.Ordinal);
        Assert.Contains("new.txt", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_top_file_written_and_what_git_did_to_the_tree_are_seen()
    {
        var tree = await TreeAsync();
        var before = TreeStamp.Take(tree)!;
        await File.WriteAllTextAsync(Path.Combine(tree, "a.txt"), "longer than before\n");
        await GitAsync(tree, "add", "a.txt");
        await GitAsync(tree, "checkout", "--quiet", "-b", "elsewhere");
        var said = TreeStamp.Take(tree)!.Differences(before);
        Assert.StartsWith("3 path(s) changed", said, StringComparison.Ordinal);
        Assert.Contains("git/index", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_folder_that_cannot_be_read_has_no_stamp()
    {
        var tree = await TreeAsync();
        Assert.Null(TreeStamp.Take(Path.Combine(tree, "absent")));
    }
}
