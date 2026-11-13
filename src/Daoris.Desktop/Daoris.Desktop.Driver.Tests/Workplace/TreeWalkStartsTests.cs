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
        // The line holds the work by content: a copy of the commit, not its ancestry.
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
        await WalkAsync(trees, tree);
        await File.WriteAllTextAsync(Path.Combine(tree, "edit.txt"), "edit\n");

        var (work, starts) = await WalkAsync(trees, tree);

        Assert.NotNull(work);
        Assert.Equal(1, Asked(starts, "status"));
    }
}
