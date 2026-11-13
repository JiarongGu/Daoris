using Daoris.Contracts.Asks;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>An ask's plans as the wire carries them: each part with the quest it became, and no pack, whose pointers are a worker's to read.</summary>
internal static class TeamAnswers
{
    public static AskPlanResponse Plan(string ask, IReadOnlyList<TeamPlan> plans, IReadOnlyList<TeamPartRow> rows) =>
        new(ask.TrimStart('#'), [.. plans.OrderBy(plan => plan.Depth).ThenBy(plan => plan.Scope, StringComparer.Ordinal).Select(plan => Revision(plan, rows))]);

    private static AskPlanRevisionResponse Revision(TeamPlan plan, IReadOnlyList<TeamPartRow> rows) => new(
        plan.Scope, plan.Revision, plan.Depth, plan.Why,
        [.. plan.Parts.Select(part => new AskPlanPartResponse(
            part.Id, part.Repository, rows.FirstOrDefault(row => row.Part == part.Id)?.Quest, part.Outcome, part.Lanes, part.Claims))],
        [.. plan.Edges.Select(edge => new AskPlanEdgeResponse(edge.Part, edge.On, edge.Kind, edge.Why))],
        [.. plan.Flags.Select(flag => new AskPlanFlagResponse(flag.Kind, flag.Part, flag.Text))],
        [.. plan.Deployables.Select(each => new AskPlanDeployableResponse(
            each.Ref.Repository, each.Ref.Id, [.. each.Provides.Select(Use)], [.. each.Consumes.Select(Use)]))],
        [.. plan.Releases.Select(edge => new AskPlanReleaseResponse(
            edge.Deployable.ToString(), edge.On.ToString(), edge.Why, edge.Source,
            edge.Evidence is { } evidence ? $"{evidence.Repository}: {evidence.File}{(evidence.Range.Length > 0 ? ":" + evidence.Range : "")}" : null))],
        [.. DeliveryGraph.Queue(plan.Questions, plan.Releases, new Dictionary<string, string>()).Select(question =>
            new AskPlanQuestionResponse(question.Key, question.Kind, question.Text, question.Yes, question.Blocks, question.Default))]);

    private static string Use(DeliveryUse use) => $"{use.Resource} ({use.Mode})";
}
