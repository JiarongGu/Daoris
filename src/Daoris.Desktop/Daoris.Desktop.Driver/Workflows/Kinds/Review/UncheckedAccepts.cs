namespace Daoris.Driver;

/// <summary>
/// The person's Accept on a review whose run waits on an unchecked done: the Accept named each unchecked handback, which the person
/// read on the review, so it keeps two words in one edit of the run's record, the hold's pass with its provenance and the review's early
/// Accept, both bound to the record drawn. Both are kept or neither; a replay finds them kept; a record moved before the pass drops both.
/// </summary>
public static class UncheckedAccepts
{
    /// <summary>No <c>quest.holds</c> of the run waits on an unchecked done, so there is nothing for the Accept to pass.</summary>
    public const string NoneUnchecked = "none-unchecked";

    /// <summary>The words the hold's pass keeps, saying whose word passed it and why.</summary>
    public const string Words = "passed by your Accept on its review, which named it unchecked";

    /// <summary>
    /// Keep the person's Accept of <paramref name="node"/> on <paramref name="run"/> at <paramref name="revision"/>, and the pass of each
    /// <c>quest.holds</c> waiting unchecked before it, in one edit.
    /// </summary>
    public static NodeWorded Keep(NodeRecordFiles records, string run, string node, string revision, Func<DateTimeOffset>? clock = null)
    {
        run = run.Trim().TrimStart('#');
        NodeWorded Refuse(string code) => new(run, node, NodeWordActs.Accept, code);
        if (new GraphRuns(records).Find(run) is not { } bound) return Refuse(RunNodeWords.NotRun);
        if (bound.Version is not { Graph: { } graph, GraphDigest: { } digest }) return Refuse(RunNodeWords.UnreadVersion) with { Detail = bound.Version.Unreadable };
        if (graph.Nodes.FirstOrDefault(each => each.Id == node) is not { } review) return Refuse(RunNodeWords.NotNode);
        if (review.Kind != NodeKinds.Review) return Refuse(RunNodeWords.NotReview);

        var now = (clock ?? (() => DateTimeOffset.UtcNow))();
        var (refused, result) = records.Edit(bound.Unit, record =>
        {
            if (record.Ended is { } ended) return (null, Refuse(RunNodeWords.Ended) with { Detail = ended.Quest });
            if (record.Pending.Any(word => word.Review == node)) return (null, Refuse(RunNodeWords.Worded));
            var holds = graph.Nodes.Where(each => each.Kind == NodeKinds.QuestHolds
                && WorkPerformer.StatusOf(record, each.Id) is { State: NodeStates.Active } status && UncheckedDones.Says(status.Holds)).ToList();
            if (holds.Count == 0) return (null, Refuse(NoneUnchecked));
            var standing = WorkPerformer.StatusOf(record, node);
            if (standing.State is not (NodeStates.Blocked or NodeStates.Ready)) return (null, Refuse(NodeRefusal.NotInTable) with { State = standing.State });

            var passes = new List<NodeWord>();
            foreach (var hold in holds)
            {
                var status = WorkPerformer.StatusOf(record, hold.Id);
                var move = new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = Words, Scope = new NodeScope(digest, bound.Run) };
                if (NodeLifecycle.Apply(hold, status, move) is { Moved: false } judged) return (null, Refuse(judged.Refused!) with { State = status.State });
                passes.Add(new NodeWord(hold.Id, NodeWordActs.Skip, Words, now) { Review = node, Revision = revision });
            }

            var accept = new NodeWord(node, NodeWordActs.Accept, null, now) { Kept = true, Review = node, Revision = revision };
            var named = passes.Select(each => each.Node).Append(node).ToHashSet(StringComparer.Ordinal);
            return (record with { Pending = [.. record.Pending.Where(each => !named.Contains(each.Node)), .. passes, accept] }, new NodeWorded(run, node, NodeWordActs.Accept, null) { Early = true });
        });
        return refused is null ? result! : Refuse(refused);
    }
}
