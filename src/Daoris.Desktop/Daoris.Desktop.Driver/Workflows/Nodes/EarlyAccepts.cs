namespace Daoris.Driver;

/// <summary>
/// The person's Accept pressed while the run is still on its way to its review: its graph not judged at the press's commit yet, or a
/// node before the review still to settle. Kept on the run's record as the review's Accept and applied at the first pass that finds the review
/// waiting, never refused while the run is on its way there. The review beginning at another commit than the one pressed on (the work moved,
/// a new attempt), the review settling another way, and the person's own later word on it each end it.
/// </summary>
public static class EarlyAccepts
{
    /// <summary>A kept Accept the pass leaves on the record: its review has not begun yet.</summary>
    public const string Waiting = "waiting";

    /// <summary>The review began at another commit than the one pressed on: the work moved since the press, and the Accept is not its.</summary>
    public const string Moved = "moved";

    /// <summary>
    /// Keep the person's Accept on <paramref name="run"/>'s review for the commit they pressed on (<paramref name="tip"/>; null where the door
    /// read none). Refused where the run is not bound, its graph holds no review, its record ended, its review has settled, or another word of
    /// the person's already waits on it; a kept Accept pressed again is kept anew.
    /// </summary>
    public static NodeWorded Keep(NodeRecordFiles records, string run, string? tip, Func<DateTimeOffset>? clock = null)
    {
        run = run.Trim().TrimStart('#');
        if (new GraphRuns(records).Find(run) is not { } bound) return new(run, "", NodeWordActs.Accept, RunNodeWords.NotRun);
        if (bound.Version is not { Graph: { } graph }) return new(run, "", NodeWordActs.Accept, RunNodeWords.UnreadVersion);
        if (graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Review) is not { } review) return new(run, "", NodeWordActs.Accept, RunNodeWords.NotReview);

        NodeWorded Refuse(string code) => new(run, review.Id, NodeWordActs.Accept, code);
        var now = (clock ?? (() => DateTimeOffset.UtcNow))();
        var (refused, result) = records.Edit(bound.Unit, record =>
        {
            if (record.Ended is { } ended) return (null, Refuse(RunNodeWords.Ended) with { Detail = ended.Quest });
            if (record.Pending.FirstOrDefault(each => each.Node == review.Id) is { Kept: false }) return (null, Refuse(RunNodeWords.Worded));
            var status = WorkPerformer.StatusOf(record, review.Id);
            if (status.State is not (NodeStates.Blocked or NodeStates.Ready or NodeStates.Active)) return (null, Refuse(NodeRefusal.NotInTable) with { State = status.State });

            var word = new NodeWord(review.Id, NodeWordActs.Accept, null, now) { Kept = true, Tip = tip };
            return (record with { Pending = [.. record.Pending.Where(each => each.Node != review.Id), word] }, new NodeWorded(run, review.Id, NodeWordActs.Accept, null));
        });
        return refused is null ? result! : Refuse(refused);
    }

    /// <summary>
    /// What the pass does with <paramref name="word"/> at <paramref name="standing"/>: leaves it waiting (<see cref="Waiting"/>) while its
    /// review has not begun, drops it (<see cref="Moved"/>) where the review began at another commit, and otherwise applies it now (null),
    /// where the one table judges it as any word.
    /// </summary>
    public static string? Held(NodeWord word, NodeStatus standing) =>
        !word.Kept ? null
        : standing.State is NodeStates.Blocked or NodeStates.Ready ? Waiting
        : standing.State == NodeStates.Active && word.Tip is { } tip && standing.Binding?.SubjectRevision is { } at
          && !string.Equals(tip, at, StringComparison.OrdinalIgnoreCase) ? Moved
        : null;

    /// <summary>The word standing on <paramref name="node"/> that another must wait behind: a kept Accept gives way to the person's later word.</summary>
    public static NodeWord? Standing(NodeRecord record, string node, NodeWordBy? by) =>
        record.Pending.FirstOrDefault(each => each.Node == node) is { } standing && !(standing.Kept && by is null) ? standing : null;

    /// <summary>What the press is answered once its Accept is kept: that it is accepted, and when it lands, by the step the graph waits on.</summary>
    public static string Said(GraphGateState? gate) => gate is { State: GraphGateStates.Waits, Node: { } node }
        ? $"Accepted; it lands once `{node}` is through, at the look after. Your Accept is kept on run {gate.Run}'s review until then."
        : "Accepted; it lands at the next look, once Daoris has judged this work. Your Accept is kept on its review until then.";
}
