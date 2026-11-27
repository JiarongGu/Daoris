using Daoris.Driver;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 What the line holds of a run's branch, judged again at a look over a branch nothing moved, starts no git: the
/// judgement is kept with what it rests on, read from git's files, so only a moved tip, ref or working file asks git again.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ContentReadGitCountTests(ITestOutputHelper output) : LandedFixture
{
    private async Task<(HoldContent? Read, List<string> Starts)> ReadAsync(SessionTrees trees, string tree, string tip)
    {
        using var count = GitLaunch.Count();
        var read = await HoldReads.ContentAsync(trees, tree, tip, CancellationToken.None);
        var starts = count.Starts.Select(start => string.Join(' ', start.Arguments.Where(each => !each.StartsWith("--no-optional", StringComparison.Ordinal)))).ToList();
        // The library's answers count as git's asks too: a read the memo spares is neither a start nor one of these.
        starts.AddRange(count.Reads.Select(read => "library " + read.Question));
        output.WriteLine($"{starts.Count} asks: {string.Join(" | ", starts)}");
        return (read, starts);
    }

    [Fact]
    public async Task A_look_over_an_unchanged_branch_judges_what_the_line_holds_with_no_git_start_and_a_new_commit_asks_again()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the session's work\n", "the session's work");
        var tip = TreeTips.Read(tree.Path)!;

        var (first, firstStarts) = await ReadAsync(trees, tree.Path, tip);
        var (second, secondStarts) = await ReadAsync(trees, tree.Path, tip);
        var (third, thirdStarts) = await ReadAsync(trees, tree.Path, tip);

        Assert.NotNull(first);
        Assert.NotEmpty(firstStarts);
        Assert.Empty(secondStarts);
        Assert.Empty(thirdStarts);
        Assert.Equal((first.Carried, first.Unlanded, first.Ahead), (second!.Carried, second.Unlanded, second.Ahead));
        Assert.Equal((first.Carried, first.Unlanded, first.Ahead), (third!.Carried, third.Unlanded, third.Ahead));

        // A file the work changes is another reading: its tree's work is asked again.
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "draft.txt"), "not committed\n");
        var edited = false;
        // The working files' watch hears the edit on its own thread, so the read that sees it may come a moment later.
        for (var look = 0; look < 100 && !edited; look++)
        {
            edited = (await ReadAsync(trees, tree.Path, tip)).Starts.Count > 0;
            if (!edited) await Task.Delay(50);
        }

        Assert.True(edited, "an edit to the tree's files never asked git again");

        // A new commit is another tip: the read at the old one is nothing, and the new one is judged afresh.
        await CommitAsync(tree.Path, "more.txt", "more\n", "more of the work");
        var moved = TreeTips.Read(tree.Path)!;
        var (stale, _) = await ReadAsync(trees, tree.Path, tip);
        Assert.Null(stale);
        var (again, againStarts) = await ReadAsync(trees, tree.Path, moved);
        Assert.NotNull(again);
        Assert.NotEmpty(againStarts);
    }
}
