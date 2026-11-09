using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The library's answers are counted in <see cref="GitLaunch"/> beside git's own starts and told apart from them, so the
/// counts the bench and the Git page show include both.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryCountedTests : LandedFixture
{
    [Fact]
    public async Task A_library_answer_is_a_read_in_the_flow_s_count_and_never_a_start()
    {
        var root = await BuildRepositoryAsync("counted-answer");

        using var outer = GitLaunch.Count();
        using (var inner = GitLaunch.Count())
        {
            var (code, count, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", "HEAD"], CancellationToken.None);

            Assert.Equal((0, "1\n"), (code, count));
            Assert.Empty(inner.Starts);
            var read = Assert.Single(inner.Reads);
            Assert.Equal(GitLibraryQuestion.Walks.ToString(), read.Question);
            Assert.True(read.Ticks >= 0);
        }

        Assert.Single(outer.Reads);
        Assert.Empty(outer.Starts);
    }

    [Fact]
    public async Task A_read_the_library_leaves_to_git_is_a_start_and_no_read()
    {
        var root = await BuildRepositoryAsync("counted-refused");
        Directory.CreateDirectory(Path.Combine(root, ".git", "reftable"));

        using var count = GitLaunch.Count();
        Assert.Equal(0, (await WorkingTree.GitAsync(root, ["rev-list", "--count", "HEAD"], CancellationToken.None)).Code);

        Assert.Empty(count.Reads);
        Assert.Single(count.Starts);
    }

    [Fact]
    public async Task The_review_s_log_line_and_the_bench_s_times_say_reads_and_starts_apart()
    {
        var root = await BuildRepositoryAsync("counted-line");
        using var count = GitLaunch.Count();
        await WorkingTree.GitAsync(root, ["rev-list", "--count", "HEAD"], CancellationToken.None);
        await WorkingTree.GitAsync(root, ["rev-parse", "--abbrev-ref", "HEAD"], CancellationToken.None);

        var line = SessionReviews.Line("s-1", null, count).ToDictionary(each => each.Key, each => each.Value);

        Assert.Equal(1, line["starts"]);
        Assert.Equal(1, line["reads"]);
        Assert.True((long)line["readMs"]! >= 0);
        Assert.Contains("1 starts", GitReviewCommand.Times(count));
        Assert.Contains("1 reads", GitReviewCommand.Times(count));
    }
}
