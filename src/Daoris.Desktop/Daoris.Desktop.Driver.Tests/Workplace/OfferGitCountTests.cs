using Daoris.Driver;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What an ended session's tree offers is judged with few git starts: the offer of a tree with work to land asks the head, the
/// tree's own branch, the commits no branch of the person's holds, and the content proof's own reads, once each. The figures are said
/// in the test's output. A tree of two commits the line does not hold made 17 starts: its top asked twice, the line's branch and origin's
/// asked one at a time, and an empty range and the files it differs in asked of a line that had not moved.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class OfferGitCountTests(ITestOutputHelper output) : LandedFixture
{
    // The offer's own work, measured on the starts of its judgement.
    private const int Ceiling = 5;

    [Fact]
    public async Task A_tree_with_work_to_land_is_judged_with_no_git_asked_twice_and_within_its_ceiling()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(tree.Path, "more.txt", "more\n", "more of the work");
        await trees.WorkAsync(tree.Path);

        List<string> starts;
        TreeWork? work;
        using (var count = GitLaunch.Count())
        {
            work = await trees.WorkAsync(tree.Path);
            starts = [.. count.Starts.Select(start => string.Join(' ', start.Arguments.Where(each => !each.StartsWith("--no-optional", StringComparison.Ordinal))))];
        }

        var report = $"{starts.Count} starts\n{string.Join('\n', starts)}";
        output.WriteLine(report);
        Assert.Equal(2, work!.Commits);
        Assert.True(starts.Count <= Ceiling, report);
        // The refs are listed once for the repository, whichever of its folders asks; nothing is asked twice.
        Assert.All(starts.GroupBy(each => each), asked =>
            Assert.True(asked.Count() <= 1, $"asked {asked.Count()} times: {asked.Key}\n{report}"));
        Assert.DoesNotContain(starts, each => each.StartsWith("log --format=%T", StringComparison.Ordinal));
    }
}
