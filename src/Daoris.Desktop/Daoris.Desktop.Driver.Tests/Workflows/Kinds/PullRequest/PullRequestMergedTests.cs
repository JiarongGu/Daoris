using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a monitor and the tidy take as a merged pull request: one completed into the line, as the proof judges it, never one completed into
/// another branch; and a pull request opened again for the same landing starts with no answer, not its abandoned predecessor's.
/// </summary>
public sealed class PullRequestMergedTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "pr-merged", Guid.NewGuid().ToString("N")[..8]);

    public PullRequestMergedTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static LandedBranch Landing(string? target) =>
        new("engine", "aurora", "feature/q1-fix", "main", "tip00001", "s1", "q1", "Fix the gap", At)
        {
            PullRequest = "https://example.test/pr/7",
            PullRequestState = new PullRequestState(PullRequestStates.Completed) { Target = target, MergeCommit = "m1", SourceCommit = "tip00001" },
        };

    [Fact]
    public void A_pull_request_completed_into_the_line_is_merged()
    {
        Assert.True(PullRequestReading.IsMerged(Landing("main")));
        Assert.Equal(PullRequestReading.Merged, PullRequestReading.Of(Landing("main")).State);
        Assert.True(PullRequestReading.IsMerged(Landing(null)));
    }

    [Fact]
    public void A_pull_request_completed_into_another_branch_is_not_merged()
    {
        Assert.False(PullRequestReading.IsMerged(Landing("feature/other")));
        var read = PullRequestReading.Of(Landing("feature/other"));
        Assert.Null(read.State);
        Assert.Contains("feature/other", read.Said);
    }

    [Fact]
    public void A_pull_request_opened_again_for_the_landing_starts_with_no_answer()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Landing(null) with { PullRequest = null, PullRequestState = null });
        landings.PullRequested("engine", "feature/q1-fix", "example.lands", "https://example.test/pr/7");
        landings.Answered(landings.Of("engine", "feature/q1-fix")!, new PullRequestState(PullRequestStates.Abandoned) { AskedAt = At });

        landings.PullRequested("engine", "feature/q1-fix", "example.lands", "https://example.test/pr/8");

        var standing = landings.Of("engine", "feature/q1-fix")!;
        Assert.Equal("https://example.test/pr/8", standing.PullRequest);
        Assert.Null(standing.PullRequestState);
    }
}
