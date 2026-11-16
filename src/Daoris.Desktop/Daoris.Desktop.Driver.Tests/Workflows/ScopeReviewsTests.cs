using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// METHODS1 c: a one-scope plan's end. Its parts merge into the ask's branch with no gate each; the full set runs once on the
/// branch; and two reviews, wide and close, are the landing role's first ready reviewers of two makers, else one maker's twice,
/// said as not independent.
/// </summary>
public sealed class ScopeReviewsTests
{
    private static ScopeReviewer Ready(string adapter, string maker) => new(adapter, maker, Ready: true);

    [Fact]
    public void A_one_scope_plan_gates_no_part_and_runs_the_full_set_once_at_its_end()
    {
        Assert.False(ScopeLanding.GatesEachPart("one-scope"));
        Assert.True(ScopeLanding.GatesEachPart("per-part"));
        Assert.True(ScopeLanding.GatesEachPart(null));
        Assert.Equal(ScopeLanding.Full, ScopeLanding.EndTier("one-scope"));
        Assert.Null(ScopeLanding.EndTier(null));
    }

    [Fact]
    public void The_two_reviews_come_from_two_makers_where_two_are_ready()
    {
        var chosen = ScopeLanding.Reviews([Ready("claude-a", "anthropic"), Ready("claude-b", "anthropic"), Ready("codex", "openai")]);

        Assert.Equal(2, chosen.Count);
        Assert.Equal((ScopeLanding.Wide, "claude-a"), (chosen[0].Breadth, chosen[0].Adapter));
        Assert.Equal((ScopeLanding.Close, "codex"), (chosen[1].Breadth, chosen[1].Adapter));
        Assert.All(chosen, review => Assert.True(review.Independent));
    }

    [Fact]
    public void With_one_maker_ready_both_reviews_are_its_and_say_they_are_not_independent()
    {
        var chosen = ScopeLanding.Reviews([new ScopeReviewer("codex", "openai", Ready: false), Ready("claude-a", "anthropic"), Ready("claude-b", "anthropic")]);

        Assert.Equal(["claude-a", "claude-b"], chosen.Select(review => review.Adapter));
        Assert.All(chosen, review => Assert.False(review.Independent));
        Assert.Empty(ScopeLanding.Reviews([new ScopeReviewer("codex", "openai", Ready: false)]));
        Assert.Equal(["claude-a", "claude-a"], ScopeLanding.Reviews([Ready("claude-a", "anthropic")]).Select(review => review.Adapter));
    }
}
