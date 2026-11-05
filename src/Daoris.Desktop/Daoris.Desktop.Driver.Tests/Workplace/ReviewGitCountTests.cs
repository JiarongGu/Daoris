using System.Text.Json;
using Daoris.Driver;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A review's read asks git few times (PERF1d): on the benchmark home it made 34 to 42 git starts, 7.1 to 7.3 s where one start costs a
/// third of a second to two seconds. The refs it names are read in one listing of the repository's, the commits it asks after in one
/// batch of object questions, and the files of a range in one <c>diff</c>; each answer is the one it gave before.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReviewGitCountTests(ITestOutputHelper output) : BranchTidyingFixture
{
    [Fact]
    public async Task A_tree_ahead_of_its_line_is_reviewed_in_few_git_starts()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "default");
        var baseCommit = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "part.txt", "part\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "loose.txt"), "not committed\n");

        var (review, starts) = await WarmReviewAsync(trees, tree.Path, baseCommit);

        Assert.NotNull(review?.Offer);
        Assert.Equal(["part.txt"], review!.Diff!.Files.Select(file => file.Path));
        Assert.True(starts.Count <= SmallCeiling, Report(starts));
    }

    [Fact]
    public async Task A_tree_the_line_holds_by_content_is_reviewed_in_few_git_starts()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "default");
        var baseCommit = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "part.txt", "part\n", "the work");
        // Picked by another committer, so the line's commit is never the tree's own made again within the same second.
        await GitAsync(root, "-c", "user.name=Line", "cherry-pick", "--allow-empty", (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());

        var (review, starts) = await WarmReviewAsync(trees, tree.Path, baseCommit);

        Assert.NotNull(review);
        Assert.True(starts.Count <= HeldCeiling, Report(starts));
    }

    // Before: 36 starts on the first fixture (42 and 34 on the benchmark's small and large trees), each question asked by every stage
    // that needed it. What is left is the offer's own judgement of the tree's commits, one question of each.
    private const int SmallCeiling = 25;
    private const int HeldCeiling = 24;

    private string Report(List<string> starts)
    {
        var report = $"{starts.Count} starts\n{string.Join('\n', starts)}";
        output.WriteLine(report);
        return report;
    }

    /// <summary>The git a review asks, its second over the same tree: the first reads what a review opened once before reads.</summary>
    private async Task<(SessionReview? Review, List<string> Starts)> WarmReviewAsync(SessionTrees trees, string tree, string baseCommit)
    {
        Task<SessionReview?> ReadAsync() =>
            SessionReviews.ReadAsync("s-review", tree, baseCommit, trees, (_, _) => Task.FromResult<string?>(null), null, _closing.Token);

        await ReadAsync().WaitAsync(Bound);
        List<string> starts;
        SessionReview? review;
        using (var count = GitLaunch.Count())
        {
            review = await ReadAsync().WaitAsync(Bound);
            starts = [.. count.Starts.Select(start => string.Join(' ', start.Arguments.Where(each => !each.StartsWith("--no-optional", StringComparison.Ordinal))))];
        }

        // Each answer is the one today's chain gives, asking every question of git as it comes.
        var chain = await SessionReviews.ChainReadAsync("s-review", tree, baseCommit, trees, (_, _) => Task.FromResult<string?>(null), _closing.Token).WaitAsync(Bound);
        Assert.Equal(JsonSerializer.Serialize(chain), JsonSerializer.Serialize(review));
        return (review, starts);
    }
}
