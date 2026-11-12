namespace Daoris.Driver;

/// <summary>
/// The person's through words on a run's ask: their own words asking for the work to be delivered with no press of theirs, which
/// the runtime applies as their word, never as a supervisor's. Step by step, each kept for the next pass as the person's door keeps it: a
/// <c>quest.holds</c> waiting on an unchecked done is passed with them, then the <c>review</c> waiting on its record is accepted with them,
/// bound to the record standing. Only where no requirement of the run's quests departed or waits unmet, no other step waits on the person
/// (a hold, an open look or second opinion, a go-ahead, a task or approval, a choice), nothing is stopped or paused and no word is kept;
/// else the run waits for the person as it does with no words. What follows the review runs as the graph says.
/// </summary>
public static class ThroughAccepts
{
    /// <summary>The words each word kept on them says, naming the quote and the ask it is kept on.</summary>
    public static string Words(string ask, string quote) => $"accepted on your words, \"{quote}\", kept on ask #{ask}";

    /// <summary>The step the through words act on now, with the act, or null where the run waits for the person.</summary>
    public static (string Node, string Act)? Due(NodeGraph graph, NodeRecord record, IReadOnlyList<QuestView> quests)
    {
        if (record.Ended is not null || record.Pending.Count > 0 || !Kept(quests)) return null;
        if (graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Review) is not { } review) return null;
        // The person's own changes on this run's review replace the standing words for it: its next attempt waits for them.
        if (record.History.Any(entry => entry.Was.Node == review.Id && entry.Was.State == NodeStates.Rejected)) return null;

        (string, string)? due = null;
        foreach (var node in graph.Nodes)
        {
            var status = WorkPerformer.StatusOf(record, node.Id);
            if (status.State == NodeStates.Stopped) return null;
            if (node.Id == review.Id) continue;
            if (node.Kind == NodeKinds.QuestHolds && status.State == NodeStates.Active && UncheckedDones.Says(status.Holds))
            {
                due ??= (node.Id, NodeWordActs.Skip);
                continue;
            }

            if (PersonWaitReader.WhyOf(node, status) is not null) return null;
            if (node.Kind is NodeKinds.Look or NodeKinds.Opinion or NodeKinds.QuestHolds or NodeKinds.ContentHolds or NodeKinds.WorkRequests
                && status.State == NodeStates.Active)
            {
                return null;
            }
        }

        return due ?? (WorkPerformer.StatusOf(record, review.Id).State == NodeStates.Active ? (review.Id, NodeWordActs.Accept) : null);
    }

    /// <summary>
    /// Keep the word due on <paramref name="governed"/>'s run where its ask holds through words, for the next pass to apply; the line said,
    /// or null where nothing is kept.
    /// </summary>
    public static string? Keep(NodeRecordFiles records, GovernedUnit governed, RunScope scope, DriverConfig? config, Func<DateTimeOffset> clock)
    {
        if (scope is not { Ask: { } ask, Through.Words: { } quote } || records.Read(governed.Unit.Id) is not { } record) return null;
        if (config is not null && Paused(config, ask, scope.Quests)) return null;
        if (Due(governed.Graph, record, scope.Quests) is not var (node, act)) return null;
        var kept = RunNodeWords.Word(records, governed.Run, node, act, Words(ask, quote), clock);
        return kept.Done
            ? $"work  {governed.Unit.Id}: `{node}` {(act == NodeWordActs.Accept ? "accepted" : "passed")} on the person's words kept on ask #{ask}."
            : null;
    }

    public static bool Paused(DriverConfig config, string ask, IReadOnlyList<QuestView> quests) =>
        config.PausedAsk(ask) is not null || quests.Any(quest => config.PausedQuest(quest.Id) is not null);

    /// <summary>Whether every requirement of the run's quests is answered met, none departed and none held on a departure.</summary>
    private static bool Kept(IReadOnlyList<QuestView> quests) =>
        quests.All(quest => !quest.Held && quest.Answers.All(answer => answer.Departed is null)
            && Enumerable.Range(1, quest.Requirements.Count).All(number => quest.Answers.Any(answer => answer.Requirement == number && answer.Met is not null)));
}
