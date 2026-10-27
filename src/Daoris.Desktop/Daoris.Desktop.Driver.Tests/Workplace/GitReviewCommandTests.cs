using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP1a-i's terminal door (D202 point 2, the git-app design P1.5): <c>daoris-driver git review &lt;session&gt; [--json] [--time]</c>,
/// its words read, its review said, and <c>review.read</c>'s fields, which name no path. The read itself is the route's code, which
/// <c>ReviewStartsTests</c> counts in real git; here git is a stand-in answer.
/// </summary>
public sealed class GitReviewCommandTests
{
    [Theory]
    [InlineData(new[] { "review", "s-1" }, "s-1", false, false)]
    [InlineData(new[] { "review", "--time", "s-1" }, "s-1", false, true)]
    [InlineData(new[] { "review", "s-1", "--json", "--time" }, "s-1", true, true)]
    public void The_words_name_the_session_and_the_forms(string[] args, string session, bool json, bool time) =>
        Assert.Equal(new GitReviewAsk(session, json, time), GitReviewCommand.Read(args, out _));

    [Theory]
    [InlineData(new[] { "review" }, "`git review` takes a session's id.")]
    [InlineData(new[] { "review", "s-1", "s-2" }, "`s-2` is not a word `git review` takes.")]
    [InlineData(new[] { "review", "s-1", "--all" }, "`--all` is not a word `git review` takes.")]
    public void Words_it_does_not_take_are_said(string[] args, string problem)
    {
        Assert.Null(GitReviewCommand.Read(args, out var said));
        Assert.Equal(problem, said);
    }

    /// <summary>Each start in the order the seam saw it, by node and budget, and the log line with its budgets and no path.</summary>
    [Fact]
    public async Task The_times_name_each_start_and_the_line_names_no_path()
    {
        WorkingTree.Answered.Value = _ => (0, "", "");
        try
        {
            using var count = GitLaunch.Count();
            using (GitLaunch.Within(GitBudget.Review))
            {
                await WorkingTree.GitAsync(AppContext.BaseDirectory, ["rev-parse", "--show-toplevel"], CancellationToken.None);
                using (GitLaunch.Within(GitBudget.Offer))
                {
                    await WorkingTree.GitAsync(AppContext.BaseDirectory, ["--no-optional-locks", "status", "--porcelain"], CancellationToken.None);
                }
            }

            var times = GitReviewCommand.Times(count);
            Assert.Contains("  1  T     review", times);
            Assert.Contains("  2  status  offer", times);
            Assert.Contains("git rev-parse --show-toplevel", times);
            Assert.Contains("2 starts, depth 2", times);

            var line = GitReviewCommand.LineJson(SessionReviews.Line("s-1", null, count));
            Assert.StartsWith("review.read {\"session\":\"s-1\",\"answer\":\"none\",", line);
            Assert.Contains("\"starts\":2,\"review\":1,\"landed\":0,\"offer\":1,\"wasted\":0,", line);
            Assert.DoesNotContain(AppContext.BaseDirectory.TrimEnd('\\', '/').Replace('\\', '/'), line.Replace("\\\\", "/"));
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }

    [Fact]
    public void Nothing_reviewable_is_said_in_a_sentence()
    {
        Assert.StartsWith("s-1: nothing to review here", GitReviewCommand.Said("s-1", null));
        Assert.Contains("\"reviewable\": false", GitReviewCommand.Json("s-1", null, null));
    }
}
