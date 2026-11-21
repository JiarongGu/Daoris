using Daoris.Desktop.Modules;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a disputed second opinion puts in <i>What needs you</i>: a dispute the risk policy passed is answered and
/// waits for no one; one it gates on a role waits on that role, never the owner's list; only one classed <c>owner</c>, or one judged with no
/// policy read, reaches the person, as it always did.
/// </summary>
public sealed class OpinionDisputeWaitsTests
{
    private static OpinionJudged Judged(OpinionGateState gate, bool auto = false) =>
        new("s1", "q1", "web-app", "work", DateTimeOffset.UnixEpoch, auto, gate);

    private static OpinionView Read() =>
        new("o1", "landing", "first", "s1", "r-o1", "web-app", "0000000000000000000000000000000000000000", "1111111111111111111111111111111111111111", "codex-acp",
            ReviewerLabels.AnotherMaker, "given") { Product = "Codex", Maker = "OpenAI", Findings = [new OpinionFindingView(1, "must", "src/a.ts:1", "claim", "", "", "sure")] };

    private static RiskVerdict Verdict(string riskClass) =>
        new(RiskActs.WorkAccept, null, RiskVerdict.OutcomeOf(riskClass), riskClass, riskClass == RiskClasses.Role ? "reviewer" : null, "repository/web-app", "r1", "classed");

    [Fact]
    public void Nothing_reaches_the_owner_unless_the_policy_classes_the_dispute_owner()
    {
        var answered = new OpinionGateState(OpinionGateStates.Answered, "web-app") { Answered = OpinionGateStates.Disputed, Risk = Verdict(RiskClasses.None) };
        var role = new OpinionGateState(OpinionGateStates.Disputed, "web-app") { Session = "s1", Opinion = Read(), Disputes = new([1], []), Risk = Verdict(RiskClasses.Role) };
        var owner = role with { Risk = Verdict(RiskClasses.Owner) };
        var unread = role with { Risk = null };

        var waits = TreesRoutes.OpinionWaits([Judged(answered), Judged(role), Judged(owner), Judged(unread)]);

        Assert.Equal(2, waits.Count);
    }

    [Fact]
    public void Commits_nobody_read_reach_the_owner_only_where_they_land_by_themselves_and_the_policy_classes_them_owner()
    {
        var role = new OpinionGateState(OpinionGateStates.CommitsSince, "web-app") { Session = "s1", Opinion = Read(), Since = 2, Risk = Verdict(RiskClasses.Role) };
        var owner = role with { Risk = Verdict(RiskClasses.Owner) };

        Assert.False(TreesRoutes.WaitsOnPerson(role, auto: true));
        Assert.True(TreesRoutes.WaitsOnPerson(owner, auto: true));
        Assert.False(TreesRoutes.WaitsOnPerson(owner, auto: false));
    }
}
