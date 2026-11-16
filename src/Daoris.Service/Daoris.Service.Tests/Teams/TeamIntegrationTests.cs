using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A plan's integration. <c>one-scope</c> builds the ask's parts as one scope (merged into the ask's branch with no
/// gate each, the full set and two reviews once at the end); <c>per-part</c> is each part gated, as before. Only the ask's own
/// plan names it, a revision that names none keeps the standing one, and a plan written before reads as per-part.
/// </summary>
public sealed class TeamIntegrationTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-16T09:00:00Z");

    [Fact]
    public void A_plan_s_integration_is_written_and_read_back_and_a_plan_written_before_reads_as_each_part_gated()
    {
        var plan = new TeamPlan("a1", null, 1, "s1", "Why.", At) { Integration = TeamIntegration.OneScope };

        Assert.Equal(TeamIntegration.OneScope, TeamPlanCodec.Read(TeamPlanCodec.Write(plan))!.Integration);
        var before = TeamPlanCodec.Read(TeamPlanCodec.Write(plan with { Integration = null }))!;
        Assert.Null(before.Integration);
        Assert.False(TeamIntegration.IsOneScope(before));
        Assert.True(TeamIntegration.IsOneScope(plan));
    }

    [Fact]
    public void Only_the_ask_s_own_plan_names_a_known_integration()
    {
        Assert.Null(TeamIntegration.Refusal(null, null));
        Assert.Null(TeamIntegration.Refusal(TeamIntegration.OneScope, null));
        Assert.Null(TeamIntegration.Refusal(TeamIntegration.PerPart, null));
        Assert.Contains("ask's own plan", TeamIntegration.Refusal(TeamIntegration.OneScope, "p1"), StringComparison.Ordinal);
        Assert.Contains("`one-scope` or `per-part`", TeamIntegration.Refusal("batch", null), StringComparison.Ordinal);
    }

    [Fact]
    public void A_revision_that_names_none_keeps_the_standing_integration()
    {
        var standing = new TeamPlan("a1", null, 1, "s1", "Why.", At) { Integration = TeamIntegration.OneScope };

        Assert.Equal(TeamIntegration.OneScope, TeamIntegration.Carried(null, standing));
        Assert.Equal(TeamIntegration.PerPart, TeamIntegration.Carried(TeamIntegration.PerPart, standing));
        Assert.Null(TeamIntegration.Carried(null, null));
    }
}
