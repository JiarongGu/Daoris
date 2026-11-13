using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A walk of a tree the line holds by content asks git once for each of its status, its object reads and its layout, and its kept read
/// after an edit one <c>status</c>: the start budgets of <see cref="TreeReadMemo"/>'s walk, measured on real git with <see cref="GitLaunch.Count"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeWalkStartsTests(ITestOutputHelper output) : LandedFixture
{
    private async Task<(string Tree, SessionTrees Trees)> SquashedTreeAsync()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "part.txt", "part\n", "part");
        // The line holds the work by content: a copy of the commit, not its ancestry. The line moves first, so the copy has another
        // parent and is never the same commit as the original, as it would be were both made in one second on one parent.
        await CommitAsync(root, "line.txt", "line\n", "line");
        await GitAsync(root, "cherry-pick", "--allow-empty", (await GitAsync(opened.Path, "rev-parse", "HEAD")).Trim());
        return (opened.Path, trees);
    }

    private async Task<(TreeWork? Work, string[][] Starts)> WalkAsync(SessionTrees trees, string tree)
    {
        using var count = GitLaunch.Count();
        var work = await TreeReadMemo.Of(Home).Begin(trees).WorkAsync(tree, CancellationToken.None);
        var starts = count.Starts.Select(start => start.Arguments.ToArray()).ToArray();
        foreach (var start in starts) output.WriteLine($"git {string.Join(' ', start)}");
        return (work, starts);
    }

    private static int Asked(string[][] starts, string command) => starts.Count(start => start.Contains(command));

    [Fact]
    public async Task A_tree_the_line_holds_by_content_is_walked_with_a_status_an_object_read_and_a_layout_each_once()
    {
        var (tree, trees) = await SquashedTreeAsync();

        var (work, starts) = await WalkAsync(trees, tree);

        Assert.NotNull(work?.Held);
        Assert.Equal(1, Asked(starts, "status"));
        Assert.Equal(1, Asked(starts, "cat-file"));
        Assert.Equal(starts.Length, starts.Select(start => string.Join(' ', start)).Distinct().Count());
        Assert.True(starts.Length <= 5, $"the walk started {starts.Length} gits, over its 5");
    }

    [Fact]
    public async Task A_kept_read_of_a_tree_the_line_holds_by_content_asks_one_status_after_an_edit()
    {
        var (tree, trees) = await SquashedTreeAsync();
        var before = (await WalkAsync(trees, tree)).Work;
        Assert.Equal(0, before?.Uncommitted);
        await File.WriteAllTextAsync(Path.Combine(tree, "edit.txt"), "edit\n");

        // The kept read trusts the tree's watcher, which delivers the edit a moment after it is written, so the reads before it are
        // the kept one and ask nothing; the first to see the edit asks its one status.
        (TreeWork? Work, string[][] Starts) seen = (before, []);
        for (var attempt = 0; attempt < 100 && seen.Work?.Uncommitted is not > 0; attempt++)
        {
            await Task.Delay(100);
            seen = await WalkAsync(trees, tree);
        }

        Assert.True(seen.Work?.Uncommitted > 0, "the kept read never saw the edit");
        Assert.Equal(1, Asked(seen.Starts, "status"));
    }
}
