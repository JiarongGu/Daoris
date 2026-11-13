using Daoris.Contracts.Asks;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>An ask's plans as the wire carries them: each part with the quest it became, and no pack, whose pointers are a worker's to read.</summary>
internal static class TeamAnswers
{
    public static AskPlanResponse Plan(
        string ask, IReadOnlyList<TeamPlan> plans, IReadOnlyList<TeamPartRow> rows, IReadOnlyList<TeamMember> members, IReadOnlyList<TeamDecision> decisions) =>
        new(
            ask.TrimStart('#'), [.. plans.OrderBy(plan => plan.Depth).ThenBy(plan => plan.Scope, StringComparer.Ordinal).Select(plan => Revision(plan, rows))],
            [.. members.Select(member => new AskPlanMemberResponse(member.Part, member.Role, member.Session, member.Revision))],
            [.. decisions.Select(decision => new AskPlanDecisionResponse(decision.Scope, decision.Revision, decision.Decision, decision.Reason, decision.Author, decision.Act))]);

    private static AskPlanRevisionResponse Revision(TeamPlan plan, IReadOnlyList<TeamPartRow> rows) => new(
        plan.Scope, plan.Revision, plan.Depth, plan.Why,
        [.. plan.Parts.Select(part => new AskPlanPartResponse(
            part.Id, part.Repository, rows.FirstOrDefault(row => row.Part == part.Id)?.Quest, part.Outcome, part.Lanes, part.Claims))],
        [.. plan.Edges.Select(edge => new AskPlanEdgeResponse(edge.Part, edge.On, edge.Kind, edge.Why))],
        [.. plan.Flags.Select(flag => new AskPlanFlagResponse(flag.Kind, flag.Part, flag.Text))]);
}
