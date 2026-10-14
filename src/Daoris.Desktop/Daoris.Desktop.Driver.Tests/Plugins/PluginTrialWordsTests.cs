using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A trial's `state` step says commits the way the driver shortens them; a short or missing id no longer throws (DEDUP7c).</summary>
public sealed class PluginTrialWordsTests
{
    [Fact]
    public void A_completed_state_shows_each_commit_cut_to_eight()
    {
        var state = new PullRequestState(PullRequestStates.Completed) { SourceCommit = "0123456789abcdef", MergeCommit = "fedcba9876543210" };
        Assert.Equal("completed, merged 01234567 as fedcba98", PluginTrials.StateWords(state));
    }

    [Fact]
    public void A_completed_state_with_a_short_or_missing_commit_shows_what_it_has()
    {
        var state = new PullRequestState(PullRequestStates.Completed) { SourceCommit = "abc12", MergeCommit = null };
        Assert.Equal("completed, merged abc12 as ", PluginTrials.StateWords(state));
    }
}
